using Discord;
using Discord.Interactions;
using Kestrelle.Bot.Realtime; // <-- MusicRealtimePublisher namespace
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

public sealed class MusicModule(
    IAudioService audioService,
    IOptions<QueuedLavalinkPlayerOptions> playerOptions,
    ILogger<MusicModule> logger,
    MusicRealtimePublisher realtimePublisher
    ) : InteractionModuleBase<SocketInteractionContext>
{
    private async ValueTask<QueuedLavalinkPlayer?> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        logger.LogInformation("In GetPlayerAsync()");

        if (Context.Guild is null)
        {
            await FollowupAsync("This command can only be used in a server.").ConfigureAwait(false);
            return null;
        }

        var channelBehavior = connectToVoiceChannel
            ? PlayerChannelBehavior.Join
            : PlayerChannelBehavior.None;

        var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: channelBehavior);

        var guildUser = Context.Guild.GetUser(Context.User.Id);
        if (guildUser is null)
        {
            await FollowupAsync("Unable to resolve your guild user. Try again in a moment.").ConfigureAwait(false);
            return null;
        }

        var userVoiceChannelId = guildUser.VoiceChannel?.Id;
        if (userVoiceChannelId is null)
        {
            await FollowupAsync("You are not connected to a voice channel.").ConfigureAwait(false);
            return null;
        }

        logger.LogInformation("User {User} in voice channel {VoiceChannelId}",
            guildUser.Username, userVoiceChannelId);

        var result = await audioService.Players.RetrieveAsync(
                guildId: Context.Guild.Id,
                memberVoiceChannel: userVoiceChannelId,
                playerFactory: PlayerFactory.Queued,
                options: playerOptions,
                retrieveOptions: retrieveOptions)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
                PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
                _ => $"Failed to get player: {result.Status}"
            };

            await FollowupAsync(errorMessage).ConfigureAwait(false);
            return null;
        }

        return result.Player;
    }

    // ----------------------------
    // Realtime publishing helpers
    // ----------------------------

    private string GuildIdString => Context.Guild?.Id.ToString() ?? "0";

    private static object TrackSummary(LavalinkTrack t, IUser requestedBy)
        => new
        {
            title = t.Title,
            author = t.Author,
            uri = t.Uri?.ToString(),
            durationMs = (long)t.Duration.TotalMilliseconds,
            artworkUrl = (string?)null, // add if you can derive thumbnails later
            requestedBy = requestedBy.Username
        };

    private async Task PublishNowPlayingAsync(QueuedLavalinkPlayer player, LavalinkTrack? track, bool isPaused)
    {
        var positionMs = GetPlayerPositionMs(player);
        var volume = GetPlayerVolume(player) ?? 100;
        // Position can be improved later via events; start with 0 or keep last-known.
        var payload = new
        {
            guildId = GuildIdString,
            track = track is null ? null : TrackSummary(track, Context.User),
            positionMs = positionMs,
            isPaused = isPaused,
            volume = volume,
            updatedUtc = DateTimeOffset.UtcNow
        };

        await realtimePublisher.PublishNowPlayingAsync(payload, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task PublishQueueAsync(QueuedLavalinkPlayer player)
    {
        var tracks = player.Queue
            .Select(queueItem => new
            {
                title = queueItem.Track.Title,
                author = queueItem.Track.Author,
                uri = queueItem.Track.Uri,
                durationMs = (long)queueItem.Track.Duration.TotalMilliseconds,
                artworkUrl = (string?)null,
                requestedBy = (string?)null
            })
            .ToList();

        var payload = new
        {
            guildId = GuildIdString,
            tracks = tracks,
            updatedUtc = DateTimeOffset.UtcNow
        };

        await realtimePublisher.PublishQueueAsync(payload, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task PublishToastAsync(string kind, string message)
    {
        var payload = new
        {
            guildId = GuildIdString,
            kind = kind,
            message = message,
            user = Context.User.Username,
            occurredUtc = DateTimeOffset.UtcNow
        };

        await realtimePublisher.PublishToastAsync(payload, CancellationToken.None).ConfigureAwait(false);
    }

    // ----------------------------
    // Commands
    // ----------------------------

    [SlashCommand("play", "Plays music", runMode: RunMode.Async)]
    public async Task Play(string query)
    {
        logger.LogInformation("Play invoked. Query='{Query}'", query);

        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);
        if (player is null) return;

        var track = await audioService.Tracks
            .LoadTrackAsync(query, TrackSearchMode.YouTube)
            .ConfigureAwait(false);

        if (track is null)
        {
            await FollowupAsync("No results.").ConfigureAwait(false);
            await PublishToastAsync("warning", "No results found.").ConfigureAwait(false);
            return;
        }

        await player.PlayAsync(track).ConfigureAwait(false);

        // Publish realtime state + toast
        await PublishToastAsync("success", $"Now playing: {track.Title}").ConfigureAwait(false);
        await PublishNowPlayingAsync(player, track, isPaused: false).ConfigureAwait(false);
        await PublishQueueAsync(player).ConfigureAwait(false);

        var embed = BuildNowPlayingEmbed(track, Context.User);

        var components = new ComponentBuilder()
            .WithButton("Pause/Resume", customId: $"np:toggle:{Context.Guild!.Id}", style: ButtonStyle.Primary)
            .WithButton("Skip", customId: $"np:skip:{Context.Guild!.Id}", style: ButtonStyle.Secondary)
            .WithButton("Stop", customId: $"np:stop:{Context.Guild!.Id}", style: ButtonStyle.Danger)
            .Build();

        await FollowupAsync(embed: embed, components: components).ConfigureAwait(false);
    }

    [SlashCommand("pause", "Pauses the player", runMode: RunMode.Async)]
    public async Task PauseAsync()
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;

        if (player.State is PlayerState.Paused)
        {
            await FollowupAsync("Player is already paused.").ConfigureAwait(false);
            return;
        }

        await player.PauseAsync().ConfigureAwait(false);

        await PublishToastAsync("info", "Paused playback.").ConfigureAwait(false);
        await PublishNowPlayingAsync(player, player.CurrentTrack, isPaused: true).ConfigureAwait(false);

        await FollowupAsync("Paused.").ConfigureAwait(false);
    }

    [SlashCommand("resume", "Resumes the player", runMode: RunMode.Async)]
    public async Task ResumeAsync()
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;

        if (player.State is not PlayerState.Paused)
        {
            await FollowupAsync("Player is not paused.").ConfigureAwait(false);
            return;
        }

        await player.ResumeAsync().ConfigureAwait(false);

        await PublishToastAsync("info", "Resumed playback.").ConfigureAwait(false);
        await PublishNowPlayingAsync(player, player.CurrentTrack, isPaused: false).ConfigureAwait(false);

        await FollowupAsync("Resumed.").ConfigureAwait(false);
    }

    [SlashCommand("skip", "Skips the current track", runMode: RunMode.Async)]
    public async Task SkipAsync()
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;

        if (player.CurrentTrack is null)
        {
            await FollowupAsync("Nothing playing!").ConfigureAwait(false);
            return;
        }

        await player.SkipAsync().ConfigureAwait(false);

        // After skip, current track may be next track or null
        var track = player.CurrentTrack;

        if (track is not null)
        {
            await PublishToastAsync("info", $"Skipped. Now playing: {track.Title}").ConfigureAwait(false);
            await PublishNowPlayingAsync(player, track, isPaused: player.State is PlayerState.Paused).ConfigureAwait(false);
        }
        else
        {
            await PublishToastAsync("info", "Skipped. Queue is empty; playback stopped.").ConfigureAwait(false);
            await PublishNowPlayingAsync(player, null, isPaused: false).ConfigureAwait(false);
        }

        await PublishQueueAsync(player).ConfigureAwait(false);

        await FollowupAsync(track is not null
            ? $"Skipped. Now playing: {track.Uri}"
            : "Skipped. Stopped playing because the queue is now empty.").ConfigureAwait(false);
    }

    [SlashCommand("stop", "Stops playback and clears queue", runMode: RunMode.Async)]
    public async Task StopAsync()
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;

        if (player.CurrentTrack is null)
        {
            await FollowupAsync("Nothing playing!").ConfigureAwait(false);
            return;
        }

        await player.StopAsync().ConfigureAwait(false);

        await PublishToastAsync("warning", "Stopped playback.").ConfigureAwait(false);
        await PublishNowPlayingAsync(player, null, isPaused: false).ConfigureAwait(false);
        await PublishQueueAsync(player).ConfigureAwait(false);

        await FollowupAsync("Stopped playing.").ConfigureAwait(false);
    }

    [SlashCommand("leave", "Disconnects from voice", runMode: RunMode.Async)]
    public async Task LeaveAsync()
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;

        await player.StopAsync().ConfigureAwait(false);

        await PublishToastAsync("warning", "Disconnected from voice.").ConfigureAwait(false);
        await PublishNowPlayingAsync(player, null, isPaused: false).ConfigureAwait(false);
        await PublishQueueAsync(player).ConfigureAwait(false);

        await FollowupAsync("Disconnected.").ConfigureAwait(false);
    }

    private static Embed BuildNowPlayingEmbed(LavalinkTrack track, IUser requestedBy)
    {
        var embedBuilder = new EmbedBuilder()
            .WithTitle("Now Playing")
            .WithDescription($"[{track.Title}]({track.Uri})")
            .AddField("Requested by", requestedBy.Mention, inline: true)
            .AddField("Duration", FormatDuration(track.Duration), inline: true)
            .WithCurrentTimestamp();

        return embedBuilder.Build();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration == TimeSpan.Zero)
        {
            return "Live";
        }

        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes}:{duration.Seconds:D2}";
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
}
