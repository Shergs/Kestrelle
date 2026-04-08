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

        var filePath = ResolveAbsolutePath(sound.StorageKey);
        if (!File.Exists(filePath))
            return (false, "Sound file is missing from storage.", null);

        await StopAsync(guildId, CancellationToken.None);

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var audioClient = await voiceChannel.ConnectAsync(selfDeaf: true, selfMute: false);
        var playback = new ActivePlayback(audioClient, cancellation);
        _playbacks[guildId] = playback;

        playback.Execution = RunPlaybackAsync(guildId, playback, filePath, cancellation.Token);
        _ = WatchPlaybackAsync(guildId, playback);

        logger.LogInformation("Playing sound {SoundId} in guild {GuildId} for {User}", sound.Id, guildId, requestedBy ?? "unknown");
        return (true, $"Playing {sound.DisplayName}.", sound.DisplayName);
    }

    public async Task<(bool Success, string Message)> StopAsync(ulong guildId, CancellationToken ct)
    {
        if (!_playbacks.TryRemove(guildId, out var playback))
            return (false, "No sound is currently playing.");

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

    private async Task RunPlaybackAsync(ulong guildId, ActivePlayback playback, string filePath, CancellationToken ct)
    {
        using var ffmpeg = CreateFfmpegProcess(filePath);
        ffmpeg.Start();

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

        await using var discordStream = playback.AudioClient.CreatePCMStream(AudioApplication.Mixed);

        try
        {
            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(discordStream, ct).ConfigureAwait(false);
            await discordStream.FlushAsync().ConfigureAwait(false);
            await ffmpeg.WaitForExitAsync(ct).ConfigureAwait(false);

            if (ffmpeg.ExitCode != 0)
                logger.LogWarning("ffmpeg exited with code {ExitCode} while playing {FilePath} in guild {GuildId}.", ffmpeg.ExitCode, filePath, guildId);
        }
        catch (OperationCanceledException)
        {
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
                Arguments = $"-hide_banner -loglevel error -i \"{filePath.Replace("\\", "\\\\")}\" -ac 2 -f s16le -ar 48000 pipe:1",
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
