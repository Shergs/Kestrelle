using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using Discord.Audio;
using Discord.WebSocket;
using Kestrelle.Models.Data;
using Kestrelle.Models.Entities;
using Kestrelle.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kestrelle.Bot.Sounds;

public sealed class SoundPlaybackService(
    IServiceScopeFactory scopeFactory,
    SoundDiscordClientAccessor clientAccessor,
    IOptions<SoundStorageOptions> storageOptions,
    ILogger<SoundPlaybackService> logger)
{
    private readonly ConcurrentDictionary<ulong, ActivePlayback> _playbacks = new();
    private readonly string _rootPath = ResolveRootPath(storageOptions.Value.RootPath);
    private readonly DiscordSocketClient _client = clientAccessor.Client;

    public async Task<(bool Success, string Message, string? DisplayName)> PlayAsync(
        ulong guildId,
        ulong voiceChannelId,
        Guid soundId,
        string? requestedBy,
        CancellationToken ct)
    {
        Sound? sound;

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KestrelleDbContext>();
            sound = await db.Sounds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == soundId && x.GuildId == guildId, ct);
        }

        if (sound is null)
            return (false, "Sound not found.", null);

        if (sound.StorageProvider is not SoundStorageProvider.LocalDisk)
            return (false, "Only locally stored sounds are supported right now.", null);

        var guild = _client.GetGuild(guildId);
        if (guild is null)
            return (false, "Sound bot is not connected to that guild.", null);

        var voiceChannel = guild.GetVoiceChannel(voiceChannelId);
        if (voiceChannel is null)
            return (false, "Voice channel not found.", null);

        var permissions = guild.CurrentUser.GetPermissions(voiceChannel);
        if (!permissions.Connect)
            return (false, "Sound bot does not have permission to connect to that voice channel.", null);

        if (!permissions.Speak)
            return (false, "Sound bot does not have permission to speak in that voice channel.", null);

        var filePath = ResolveAbsolutePath(sound.StorageKey);
        if (!File.Exists(filePath))
            return (false, "Sound file is missing from storage.", null);

        await StopAsync(guildId, CancellationToken.None);

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        logger.LogInformation(
            "Connecting sound bot to guild {GuildId} channel {ChannelId} for sound {SoundId} ({DisplayName}) requested by {User}. File: {FilePath}",
            guildId,
            voiceChannelId,
            sound.Id,
            sound.DisplayName,
            requestedBy ?? "unknown",
            filePath);

        var audioClient = await voiceChannel.ConnectAsync(selfDeaf: false, selfMute: false);
        var currentVoiceState = guild.CurrentUser.VoiceState;
        logger.LogInformation(
            "Connected sound bot to guild {GuildId}. Voice state={ConnectionState} websocket latency={Latency} udp latency={UdpLatency} currentChannel={CurrentChannelId} muted={IsMuted} deafened={IsDeafened}",
            guildId,
            audioClient.ConnectionState,
            audioClient.Latency,
            audioClient.UdpLatency,
            currentVoiceState?.VoiceChannel?.Id,
            currentVoiceState?.IsMuted,
            currentVoiceState?.IsDeafened);

        var playback = new ActivePlayback(audioClient, cancellation);
        _playbacks[guildId] = playback;

        playback.Execution = RunPlaybackAsync(guildId, playback, sound, filePath, cancellation.Token);
        _ = WatchPlaybackAsync(guildId, playback);

        return (true, $"Playing {sound.DisplayName}.", sound.DisplayName);
    }

    public async Task<(bool Success, string Message)> StopAsync(ulong guildId, CancellationToken ct)
    {
        if (!_playbacks.TryRemove(guildId, out var playback))
            return (false, "No sound is currently playing.");

        logger.LogInformation("Stopping sound playback in guild {GuildId}.", guildId);
        await CleanupPlaybackAsync(playback).ConfigureAwait(false);
        return (true, "Stopped sound playback.");
    }

    private async Task WatchPlaybackAsync(ulong guildId, ActivePlayback playback)
    {
        try
        {
            if (playback.Execution is not null)
                await playback.Execution.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sound playback failed for guild {GuildId}.", guildId);
        }
        finally
        {
            if (_playbacks.TryGetValue(guildId, out var current) && ReferenceEquals(current, playback))
                _playbacks.TryRemove(guildId, out _);

            await CleanupPlaybackAsync(playback).ConfigureAwait(false);
        }
    }

    private async Task RunPlaybackAsync(ulong guildId, ActivePlayback playback, Sound sound, string filePath, CancellationToken ct)
    {
        using var ffmpeg = CreateFfmpegProcess(filePath);
        ffmpeg.Start();

        var stderrTask = ffmpeg.StandardError.ReadToEndAsync();
        logger.LogInformation("ffmpeg started for sound {SoundId} in guild {GuildId}.", sound.Id, guildId);

        using var registration = ct.Register(() =>
        {
            try
            {
                if (!ffmpeg.HasExited)
                    ffmpeg.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        });

        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        long bytesSent = 0;

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
            await playback.AudioClient.SetSpeakingAsync(true).ConfigureAwait(false);
            await using var discordStream = playback.AudioClient.CreatePCMStream(AudioApplication.Mixed, bitrate: 128_000, bufferMillis: 1000, packetLoss: 30);

            while (true)
            {
                var bytesRead = await ffmpeg.StandardOutput.BaseStream
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), ct)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                    break;

                bytesSent += bytesRead;
                await discordStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
            }

            await discordStream.FlushAsync(ct).ConfigureAwait(false);
            await ffmpeg.WaitForExitAsync(ct).ConfigureAwait(false);

            var ffmpegError = await stderrTask.ConfigureAwait(false);

            if (ffmpeg.ExitCode != 0)
            {
                logger.LogWarning(
                    "ffmpeg exited with code {ExitCode} while playing sound {SoundId} in guild {GuildId}. stderr: {StandardError}",
                    ffmpeg.ExitCode,
                    sound.Id,
                    guildId,
                    string.IsNullOrWhiteSpace(ffmpegError) ? "<empty>" : ffmpegError.Trim());
            }
            else if (bytesSent == 0)
            {
                logger.LogWarning(
                    "ffmpeg produced no PCM output while playing sound {SoundId} in guild {GuildId}. stderr: {StandardError}",
                    sound.Id,
                    guildId,
                    string.IsNullOrWhiteSpace(ffmpegError) ? "<empty>" : ffmpegError.Trim());
            }
            else
            {
                logger.LogInformation(
                    "Finished sound playback for sound {SoundId} in guild {GuildId}. Bytes sent: {BytesSent}.",
                    sound.Id,
                    guildId,
                    bytesSent);

                if (!string.IsNullOrWhiteSpace(ffmpegError))
                    logger.LogDebug("ffmpeg stderr for sound {SoundId} in guild {GuildId}: {StandardError}", sound.Id, guildId, ffmpegError.Trim());
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Sound playback canceled for sound {SoundId} in guild {GuildId}.", sound.Id, guildId);
        }
        finally
        {
            try
            {
                await playback.AudioClient.SetSpeakingAsync(false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to clear speaking state for guild {GuildId}.", guildId);
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task CleanupPlaybackAsync(ActivePlayback playback)
    {
        try
        {
            if (!playback.Cancellation.IsCancellationRequested)
                playback.Cancellation.Cancel();
        }
        catch
        {
        }

        try
        {
            await playback.AudioClient.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Sound audio client stop failed.");
        }

        playback.Cancellation.Dispose();
    }

    private Process CreateFfmpegProcess(string filePath)
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel error -i \"{filePath.Replace("\\", "\\\\")}\" -vn -sn -dn -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
    }

    private string ResolveAbsolutePath(string storageKey)
    {
        var relativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Sound path resolves outside the configured sound storage root.");

        return fullPath;
    }

    private static string ResolveRootPath(string? configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
            configuredRoot = "sounds";

        var rootPath = Path.GetFullPath(configuredRoot);
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    private sealed class ActivePlayback(IAudioClient audioClient, CancellationTokenSource cancellation)
    {
        public IAudioClient AudioClient { get; } = audioClient;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public Task? Execution { get; set; }
    }
}

