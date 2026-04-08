using Kestrelle.Shared;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kestrelle.Bot.Realtime;

internal sealed class MusicControlSubscriber(
    IConfiguration config,
    IAudioService audioService,
    IOptions<QueuedLavalinkPlayerOptions> playerOptions,
    MusicRealtimePublisher realtimePublisher,
    ILogger<MusicControlSubscriber> logger) : BackgroundService
{
    private HubConnection? _connection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseUrl = config["KestrelleApi:BaseAddress"];
        var botKey = config["Kestrelle:BotApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(botKey))
        {
            logger.LogWarning("Music control subscriber disabled: missing KestrelleApi:BaseAddress or Kestrelle:BotApiKey.");
            return;
        }

        var hubUrl = new Uri(new Uri(baseUrl, UriKind.Absolute), "/hubs/music-control");

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Headers["X-Kestrelle-BotKey"] = botKey;
            })
            .WithAutomaticReconnect()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        _connection.On<MusicControlRequest>("ControlRequested", request =>
            HandleControlAsync(request, stoppingToken));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_connection.State == HubConnectionState.Disconnected)
                    await _connection.StartAsync(stoppingToken).ConfigureAwait(false);

                logger.LogInformation("Connected to music control hub.");
                await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Music control hub connection failed. Retrying in 5s.");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.StopAsync(cancellationToken).ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleControlAsync(MusicControlRequest request, CancellationToken ct)
    {
        logger.LogInformation("Control requested: {Action} guild={GuildId} user={User}", request.Action, request.GuildId, request.User);
        if (!ulong.TryParse(request.GuildId, out var guildId))
        {
            logger.LogWarning("Control request ignored: invalid guild id {GuildId}.", request.GuildId);
            return;
        }

        var action = request.Action.ToLowerInvariant();
        var channelBehavior = PlayerChannelBehavior.None;
        ulong? memberVoiceChannel = null;

        if (action == "play")
        {
            if (string.IsNullOrWhiteSpace(request.VoiceChannelId) ||
                !ulong.TryParse(request.VoiceChannelId, out var voiceId))
            {
                await PublishToastAsync(request.GuildId, "warning", "Select a voice channel first.", ct, request.User).ConfigureAwait(false);
                return;
            }

            channelBehavior = PlayerChannelBehavior.Join;
            memberVoiceChannel = voiceId;
        }

        var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: channelBehavior);

        var result = await audioService.Players.RetrieveAsync(
                guildId: guildId,
                memberVoiceChannel: memberVoiceChannel,
                playerFactory: PlayerFactory.Queued,
                options: playerOptions,
                retrieveOptions: retrieveOptions)
            .ConfigureAwait(false);

        if (!result.IsSuccess || result.Player is null)
        {
            return;
        }

        var player = result.Player;

        switch (action)
        {
            case "pause":
                if (player.State is not PlayerState.Paused)
                {
                    await player.PauseAsync().ConfigureAwait(false);
                    await PublishToastAsync(request.GuildId, "info", "Paused playback.", ct, request.User).ConfigureAwait(false);
                }
                break;

            case "resume":
                if (player.State is PlayerState.Paused)
                {
                    await player.ResumeAsync().ConfigureAwait(false);
                    await PublishToastAsync(request.GuildId, "info", "Resumed playback.", ct, request.User).ConfigureAwait(false);
                }
                break;

            case "seek":
                if (request.PositionMs is null)
                    break;

                await player.SeekAsync(TimeSpan.FromMilliseconds(request.PositionMs.Value)).ConfigureAwait(false);
                await PublishToastAsync(request.GuildId, "info", "Seeked playback.", ct, request.User).ConfigureAwait(false);
                break;

            case "set-volume":
                if (request.Volume is null)
                    break;

                await SetPlayerVolumeAsync(player, request.Volume.Value).ConfigureAwait(false);
                await PublishToastAsync(request.GuildId, "info", "Updated volume.", ct, request.User).ConfigureAwait(false);
                break;

            case "sync":
                // no-op action; fall through to state publish
                break;

            case "skip":
                await player.SkipAsync().ConfigureAwait(false);
                await PublishToastAsync(request.GuildId, "info", "Skipped track.", ct, request.User).ConfigureAwait(false);
                break;

            case "stop":
                await player.StopAsync().ConfigureAwait(false);
                await PublishToastAsync(request.GuildId, "warning", "Stopped playback.", ct, request.User).ConfigureAwait(false);
                break;

            case "leave":
                await player.StopAsync().ConfigureAwait(false);
                await PublishToastAsync(request.GuildId, "warning", "Disconnected from voice.", ct, request.User).ConfigureAwait(false);
                break;

            case "move-queue":
                if (request.FromIndex is null || request.ToIndex is null)
                    break;

                if (await TryMoveQueueItemAsync(player, request.FromIndex.Value, request.ToIndex.Value).ConfigureAwait(false))
                    await PublishToastAsync(request.GuildId, "info", "Reordered the queue.", ct, request.User).ConfigureAwait(false);
                break;

            case "play":
                if (string.IsNullOrWhiteSpace(request.Query))
                    break;

                var track = await audioService.Tracks
                    .LoadTrackAsync(request.Query, TrackSearchMode.YouTube)
                    .ConfigureAwait(false);

                if (track is null)
                {
                    await PublishToastAsync(request.GuildId, "warning", "No results found.", ct, request.User).ConfigureAwait(false);
                    break;
                }

                if (player.CurrentTrack is null)
                {
                    await player.PlayAsync(track).ConfigureAwait(false);
                    await PublishToastAsync(request.GuildId, "success", $"Now playing: {track.Title}", ct, request.User).ConfigureAwait(false);
                }
                else
                {
                    if (!await TryEnqueueAsync(player, track).ConfigureAwait(false))
                    {
                        await player.PlayAsync(track).ConfigureAwait(false);
                        await PublishToastAsync(request.GuildId, "info", $"Queued (fallback): {track.Title}", ct, request.User).ConfigureAwait(false);
                    }
                    else
                    {
                        await PublishToastAsync(request.GuildId, "success", $"Queued: {track.Title}", ct, request.User).ConfigureAwait(false);
                    }
                }
                break;

            case "clear-queue":
                ClearQueue(player);
                await PublishToastAsync(request.GuildId, "info", "Cleared the queue.", ct, request.User).ConfigureAwait(false);
                break;

            default:
                logger.LogWarning("Unsupported control action: {Action}", action);
                return;
        }

        await PublishNowPlayingAsync(request.GuildId, player, ct).ConfigureAwait(false);
        await PublishQueueAsync(request.GuildId, player, ct).ConfigureAwait(false);
    }

    private static object? TrackSummary(LavalinkTrack? track)
    {
        if (track is null) return null;

        return new
        {
            title = track.Title,
            author = track.Author,
            uri = track.Uri?.ToString(),
            durationMs = (long)track.Duration.TotalMilliseconds,
            artworkUrl = (string?)null,
            requestedBy = (string?)null
        };
    }

    private async Task PublishNowPlayingAsync(string guildId, QueuedLavalinkPlayer player, CancellationToken ct)
    {
        var positionMs = GetPlayerPositionMs(player);
        var volume = GetPlayerVolume(player) ?? 100;
        var payload = new
        {
            guildId = guildId,
            track = TrackSummary(player.CurrentTrack),
            positionMs = positionMs,
            isPaused = player.State is PlayerState.Paused,
            volume = volume,
            updatedUtc = DateTimeOffset.UtcNow
        };

        await realtimePublisher.PublishNowPlayingAsync(payload, ct).ConfigureAwait(false);
    }

    private async Task PublishQueueAsync(string guildId, QueuedLavalinkPlayer player, CancellationToken ct)
    {
        var tracks = player.Queue
            .Select(queueItem => new
            {
                title = queueItem.Track.Title,
                author = queueItem.Track.Author,
                uri = queueItem.Track.Uri?.ToString(),
                durationMs = (long)queueItem.Track.Duration.TotalMilliseconds,
                artworkUrl = (string?)null,
                requestedBy = (string?)null
            })
            .ToList();

        var payload = new
        {
            guildId = guildId,
            tracks = tracks,
            updatedUtc = DateTimeOffset.UtcNow
        };

        await realtimePublisher.PublishQueueAsync(payload, ct).ConfigureAwait(false);
    }

    private static void ClearQueue(QueuedLavalinkPlayer player)
    {
        var queue = player.Queue;
        var clear = queue.GetType().GetMethod("Clear", Type.EmptyTypes);
        clear?.Invoke(queue, null);
    }

    private static async Task<bool> TryMoveQueueItemAsync(QueuedLavalinkPlayer player, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0)
            return false;

        var queue = player.Queue;

        if (queue is System.Collections.IList list)
        {
            if (fromIndex >= list.Count || toIndex >= list.Count)
                return false;

            var item = list[fromIndex];
            list.RemoveAt(fromIndex);
            list.Insert(toIndex, item);
            return true;
        }

        var countProp = queue.GetType().GetProperty("Count");
        if (countProp?.GetValue(queue) is int count && (fromIndex >= count || toIndex >= count))
            return false;

        var move = queue.GetType().GetMethod("Move", new[] { typeof(int), typeof(int) });
        if (move is not null)
        {
            move.Invoke(queue, new object?[] { fromIndex, toIndex });
            return true;
        }

        var removeAt = queue.GetType().GetMethod("RemoveAt", new[] { typeof(int) });
        var insert = queue.GetType().GetMethod("Insert", new[] { typeof(int), typeof(object) });
        if (removeAt is not null && insert is not null)
        {
            var indexer = queue.GetType().GetProperty("Item");
            var itemValue = indexer?.GetValue(queue, new object?[] { fromIndex });
            if (itemValue is null)
                return false;

            removeAt.Invoke(queue, new object?[] { fromIndex });
            insert.Invoke(queue, new object?[] { toIndex, itemValue });
            return true;
        }

        if (queue is System.Collections.IEnumerable enumerable)
        {
            var items = enumerable.Cast<object>().ToList();
            if (fromIndex >= items.Count || toIndex >= items.Count)
                return false;

            var moved = items[fromIndex];
            items.RemoveAt(fromIndex);
            items.Insert(toIndex, moved);

            var clear = queue.GetType().GetMethod("Clear", Type.EmptyTypes);
            if (clear is null)
                return false;

            clear.Invoke(queue, null);

            var enqueue = queue.GetType().GetMethod("Enqueue", new[] { moved.GetType() })
                         ?? queue.GetType().GetMethod("Add", new[] { moved.GetType() });
            var enqueueAsync = queue.GetType().GetMethod("EnqueueAsync", new[] { moved.GetType() })
                              ?? queue.GetType().GetMethod("AddAsync", new[] { moved.GetType() });

            foreach (var item in items)
            {
                if (enqueue is not null)
                {
                    enqueue.Invoke(queue, new[] { item });
                    continue;
                }

                if (enqueueAsync is not null)
                {
                    var task = (Task)enqueueAsync.Invoke(queue, new[] { item })!;
                    await task.ConfigureAwait(false);
                    continue;
                }

                return false;
            }

            return true;
        }

        return false;
    }

    private static async Task<bool> TryEnqueueAsync(QueuedLavalinkPlayer player, LavalinkTrack track)
    {
        var queue = player.Queue;

        var enqueueAsync = queue.GetType().GetMethod("EnqueueAsync", new[] { track.GetType() });
        if (enqueueAsync is not null)
        {
            var task = (Task)enqueueAsync.Invoke(queue, new object?[] { track })!;
            await task.ConfigureAwait(false);
            return true;
        }

        var enqueue = queue.GetType().GetMethod("Enqueue", new[] { track.GetType() })
                  ?? queue.GetType().GetMethod("Add", new[] { track.GetType() });
        if (enqueue is not null)
        {
            enqueue.Invoke(queue, new object?[] { track });
            return true;
        }

        if (queue is System.Collections.IList list)
        {
            list.Add(track);
            return true;
        }

        return false;
    }

    private static long GetPlayerPositionMs(QueuedLavalinkPlayer player)
    {
        var type = player.GetType();

        var posProp = type.GetProperty("Position");
        if (posProp?.GetValue(player) is TimeSpan ts)
            return (long)ts.TotalMilliseconds;

        var currentPosProp = type.GetProperty("CurrentPosition");
        if (currentPosProp?.GetValue(player) is TimeSpan ts1)
            return (long)ts1.TotalMilliseconds;

        var playbackPosProp = type.GetProperty("PlaybackPosition");
        if (playbackPosProp?.GetValue(player) is TimeSpan ts2)
            return (long)ts2.TotalMilliseconds;

        var trackPosProp = type.GetProperty("TrackPosition");
        if (trackPosProp?.GetValue(player) is TimeSpan ts3)
            return (long)ts3.TotalMilliseconds;

        var positionMsProp = type.GetProperty("PositionMilliseconds");
        if (positionMsProp?.GetValue(player) is long ms)
            return ms;

        return 0L;
    }

    private static int? GetPlayerVolume(QueuedLavalinkPlayer player)
    {
        var volProp = player.GetType().GetProperty("Volume");
        if (volProp?.GetValue(player) is int volume)
            return NormalizeIntVolume(volume);

        if (volProp?.GetValue(player) is float f)
            return NormalizeVolume(f);

        if (volProp?.GetValue(player) is double d)
            return NormalizeVolume(d);

        return null;
    }

    private static int NormalizeVolume(double value)
    {
        if (value <= 1.0)
            return (int)Math.Round(value * 100);

        return (int)Math.Round(value);
    }

    private static int NormalizeIntVolume(int value)
    {
        if (value > 100)
            return (int)Math.Round(value / 10d);

        return value;
    }

    private static Task SetPlayerVolumeAsync(QueuedLavalinkPlayer player, int volume)
    {
        var type = player.GetType();
        var useNormalizedFloat = InferFloatVolumeScale(player);
        var preferIntScaled = string.Equals(
            type.FullName,
            "Lavalink4Net.Players.Queued.QueuedLavalinkPlayer",
            StringComparison.Ordinal);

        if (preferIntScaled)
        {
            var scaled = Math.Clamp(volume * 10, 0, 1000);
            var intTask = TryInvokeVolumeInt(type, player, scaled);
            if (intTask is not null) return intTask;
        }

        var intTaskFallback = TryInvokeVolumeInt(type, player, volume);
        if (intTaskFallback is not null) return intTaskFallback;

        var ushortMethods = new[]
        {
            type.GetMethod("SetVolumeAsync", new[] { typeof(ushort) }),
            type.GetMethod("UpdateVolumeAsync", new[] { typeof(ushort) }),
            type.GetMethod("SetVolume", new[] { typeof(ushort) }),
            type.GetMethod("UpdateVolume", new[] { typeof(ushort) }),
            type.GetMethod("SetVolumeAsync", new[] { typeof(ushort), typeof(CancellationToken) }),
            type.GetMethod("UpdateVolumeAsync", new[] { typeof(ushort), typeof(CancellationToken) })
        };

        foreach (var method in ushortMethods)
        {
            if (method is null) continue;
            var arg = (ushort)Math.Clamp(volume, 0, ushort.MaxValue);
            var result = method.GetParameters().Length == 2
                ? method.Invoke(player, new object?[] { arg, CancellationToken.None })
                : method.Invoke(player, new object?[] { arg });
            return result is Task t ? t : Task.CompletedTask;
        }

        var floatTaskFallback = TryInvokeVolumeFloat(type, player, volume, useNormalizedFloat);
        if (floatTaskFallback is not null) return floatTaskFallback;

        var doubleMethods = new[]
        {
            type.GetMethod("SetVolumeAsync", new[] { typeof(double) }),
            type.GetMethod("UpdateVolumeAsync", new[] { typeof(double) }),
            type.GetMethod("SetVolume", new[] { typeof(double) }),
            type.GetMethod("UpdateVolume", new[] { typeof(double) }),
            type.GetMethod("SetVolumeAsync", new[] { typeof(double), typeof(CancellationToken) }),
            type.GetMethod("UpdateVolumeAsync", new[] { typeof(double), typeof(CancellationToken) })
        };

        foreach (var method in doubleMethods)
        {
            if (method is null) continue;
            var scaled = useNormalizedFloat ? Math.Clamp(volume / 100d, 0d, 1d) : volume;
            var result = method.GetParameters().Length == 2
                ? method.Invoke(player, new object?[] { scaled, CancellationToken.None })
                : method.Invoke(player, new object?[] { scaled });
            return result is Task t ? t : Task.CompletedTask;
        }

        var prop = type.GetProperty("Volume");
        if (prop?.CanWrite == true)
        {
            var propType = prop.PropertyType;
            if (propType == typeof(int) || propType == typeof(uint))
                prop.SetValue(player, preferIntScaled ? Math.Clamp(volume * 10, 0, 1000) : volume);
            else if (propType == typeof(ushort))
                prop.SetValue(player, (ushort)Math.Clamp(preferIntScaled ? volume * 10 : volume, 0, ushort.MaxValue));
            else if (propType == typeof(float))
                prop.SetValue(player, useNormalizedFloat ? Math.Clamp(volume / 100f, 0f, 1f) : volume);
            else if (propType == typeof(double))
                prop.SetValue(player, useNormalizedFloat ? Math.Clamp(volume / 100d, 0d, 1d) : volume);

            return Task.CompletedTask;
        }

        var volumeMethod = type.GetMethods()
            .FirstOrDefault(m =>
                m.Name.Contains("Volume", StringComparison.OrdinalIgnoreCase) &&
                m.GetParameters().Length == 1 &&
                m.ReturnType == typeof(Task));

        if (volumeMethod is not null)
        {
            var paramType = volumeMethod.GetParameters()[0].ParameterType;
            object arg = volume;
            if (paramType == typeof(ushort))
                arg = (ushort)Math.Clamp(volume, 0, ushort.MaxValue);
            else if (paramType == typeof(float))
                arg = Math.Clamp(volume / 100f, 0f, 1f);
            else if (paramType == typeof(double))
                arg = Math.Clamp(volume / 100d, 0d, 1d);

            return (Task)volumeMethod.Invoke(player, new[] { arg })!;
        }

        return Task.CompletedTask;
    }

    private static Task? TryInvokeVolumeInt(Type type, QueuedLavalinkPlayer player, int volume)
    {
        var intMethods = new[]
        {
            type.GetMethod("SetVolumeAsync", new[] { typeof(int) }),
            type.GetMethod("UpdateVolumeAsync", new[] { typeof(int) }),
            type.GetMethod("SetVolume", new[] { typeof(int) }),
            type.GetMethod("UpdateVolume", new[] { typeof(int) }),
            type.GetMethod("SetVolumeAsync", new[] { typeof(int), typeof(CancellationToken) }),
            type.GetMethod("UpdateVolumeAsync", new[] { typeof(int), typeof(CancellationToken) })
        };

        foreach (var method in intMethods)
        {
            if (method is null) continue;
            var result = method.GetParameters().Length == 2
                ? method.Invoke(player, new object?[] { volume, CancellationToken.None })
                : method.Invoke(player, new object?[] { volume });
            return result is Task t ? t : Task.CompletedTask;
        }

        return null;
    }

    private static Task? TryInvokeVolumeFloat(Type type, QueuedLavalinkPlayer player, int volume, bool useNormalizedFloat)
    {
        var floatMethods = new[]
        {
            type.GetMethod("SetVolumeAsync", new[] { typeof(float) }),
            type.GetMethod("UpdateVolumeAsync", new[] { typeof(float) }),
            type.GetMethod("SetVolume", new[] { typeof(float) }),
            type.GetMethod("UpdateVolume", new[] { typeof(float) }),
            type.GetMethod("SetVolumeAsync", new[] { typeof(float), typeof(CancellationToken) }),
            type.GetMethod("UpdateVolumeAsync", new[] { typeof(float), typeof(CancellationToken) })
        };

        foreach (var method in floatMethods)
        {
            if (method is null) continue;
            var scaled = useNormalizedFloat ? Math.Clamp(volume / 100f, 0f, 1f) : volume;
            var result = method.GetParameters().Length == 2
                ? method.Invoke(player, new object?[] { scaled, CancellationToken.None })
                : method.Invoke(player, new object?[] { scaled });
            return result is Task t ? t : Task.CompletedTask;
        }

        var doubleMethods = new[]
        {
            type.GetMethod("SetVolumeAsync", new[] { typeof(double) }),
            type.GetMethod("UpdateVolumeAsync", new[] { typeof(double) }),
            type.GetMethod("SetVolume", new[] { typeof(double) }),
            type.GetMethod("UpdateVolume", new[] { typeof(double) }),
            type.GetMethod("SetVolumeAsync", new[] { typeof(double), typeof(CancellationToken) }),
            type.GetMethod("UpdateVolumeAsync", new[] { typeof(double), typeof(CancellationToken) })
        };

        foreach (var method in doubleMethods)
        {
            if (method is null) continue;
            var scaled = useNormalizedFloat ? Math.Clamp(volume / 100d, 0d, 1d) : volume;
            var result = method.GetParameters().Length == 2
                ? method.Invoke(player, new object?[] { scaled, CancellationToken.None })
                : method.Invoke(player, new object?[] { scaled });
            return result is Task t ? t : Task.CompletedTask;
        }

        return null;
    }

    private static bool InferFloatVolumeScale(QueuedLavalinkPlayer player)
    {
        var prop = player.GetType().GetProperty("Volume");
        if (prop?.CanRead == true)
        {
            var value = prop.GetValue(player);
            if (value is float f)
                return f <= 1.5f;
            if (value is double d)
                return d <= 1.5d;
        }

        return true;
    }

    private Task PublishToastAsync(string guildId, string kind, string message, CancellationToken ct, string? user = null)
    {
        var payload = new
        {
            guildId = guildId,
            kind = kind,
            message = message,
            user = user,
            occurredUtc = DateTimeOffset.UtcNow
        };

        return realtimePublisher.PublishToastAsync(payload, ct);
    }
}

