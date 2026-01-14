using Discord;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class MusicModule(
    IAudioService audioService,
    IOptions<QueuedLavalinkPlayerOptions> playerOptions,
    ILogger<MusicModule> logger
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
            return;
        }

        await player.PlayAsync(track).ConfigureAwait(false);

        var embed = BuildNowPlayingEmbed(track, Context.User);

        var components = new ComponentBuilder()
            .WithButton("Pause/Resume", customId: $"np:toggle:{Context.Guild.Id}", style: ButtonStyle.Primary)
            .WithButton("Skip", customId: $"np:skip:{Context.Guild.Id}", style: ButtonStyle.Secondary)
            .WithButton("Stop", customId: $"np:stop:{Context.Guild.Id}", style: ButtonStyle.Danger)
            .Build();

        await FollowupAsync(embed: embed, components: components).ConfigureAwait(false);
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

        var track = player.CurrentTrack;
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
        await FollowupAsync("Stopped playing.").ConfigureAwait(false);
    }

    [SlashCommand("leave", "Disconnects from voice", runMode: RunMode.Async)]
    public async Task LeaveAsync()
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;

        await player.StopAsync().ConfigureAwait(false);
        await FollowupAsync("Disconnected.").ConfigureAwait(false);
    }
}