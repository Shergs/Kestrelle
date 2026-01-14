using Discord;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
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
        logger.LogInformation($"Play invoked with query: {query}");
        await DeferAsync().ConfigureAwait(false);
        logger.LogInformation("After defer");

        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);
        if (player is null) return;

        logger.LogInformation("Player is not null");

        var track = await audioService.Tracks
            .LoadTrackAsync(query, TrackSearchMode.YouTube)
            .ConfigureAwait(false);

        logger.LogInformation("Track load result.");

        if (track is null)
        {
            await FollowupAsync("No results.").ConfigureAwait(false);
            return;
        }

        await player.PlayAsync(track).ConfigureAwait(false);
        await FollowupAsync($"Playing: {track.Uri}").ConfigureAwait(false);
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

        // Default player documents StopAsync(disconnect: true). :contentReference[oaicite:9]{index=9}
        await player.StopAsync().ConfigureAwait(false);
        await FollowupAsync("Disconnected.").ConfigureAwait(false);
    }
}