using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kestrelle.Bot.Interactions;

internal class InteractionHandler(
    DiscordSocketClient client,
    InteractionServiceAdapter adapter,
    IAudioService audioService,
    IServiceProvider services,
    ILogger<InteractionHandler> logger,
    IOptions<QueuedLavalinkPlayerOptions> playerOptions)
{
    private InteractionService interactions => adapter.Service;

    public async Task InitializeAsync()
    {
        client.InteractionCreated += HandleInteractionAsync;

        interactions.Log += msg =>
        {
            if (msg.Exception is not null)
            {
                logger.LogError(msg.Exception, "[{Source}] {Message}", msg.Source, msg.Message ?? "<no message>");
            }
            else
            {
                logger.LogInformation("[{Source}] {Severity}: {Message}",
                    msg.Source,
                    msg.Severity,
                    msg.Message ?? "<no message>");
            }

            return Task.CompletedTask;
        };


        await interactions.AddModulesAsync(typeof(InteractionHandler).Assembly, services);

        client.Ready += async () =>
        {
            const ulong bigOneGuildId = 783190942806835200;
            await interactions.RegisterCommandsToGuildAsync(bigOneGuildId);

            //await interactions.RegisterCommandsGloballyAsync();
            logger.LogInformation("Slash commands registered.");
        };
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            if (interaction is SocketMessageComponent component)
            {
                await HandleNowPlayingButtonsAsync(component).ConfigureAwait(false);
                return;
            }

            var context = new SocketInteractionContext(client, interaction);
            await interactions.ExecuteCommandAsync(context, services).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Interaction handling failed.");
            try
            {
                if (!interaction.HasResponded)
                    await interaction.RespondAsync("Command failed.", ephemeral: true).ConfigureAwait(false);
            }
            catch { }
        }
    }

    private async Task HandleNowPlayingButtonsAsync(SocketMessageComponent component)
    {
        if (component.Data.CustomId is null || !component.Data.CustomId.StartsWith("np:"))
        { 
            return;
        }

        var parts = component.Data.CustomId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            await component.RespondAsync("Invalid button payload.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var action = parts[1];

        if (!ulong.TryParse(parts[2], out var guildId) || component.GuildId != guildId)
        {
            await component.RespondAsync("This control is not for this server.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        // Optional: restrict to users in voice channel
        // var guild = client.GetGuild(guildId);
        // var user = guild?.GetUser(component.User.Id);
        // if (user?.VoiceChannel is null) { ... }

        // Always acknowledge quickly to avoid "interaction failed"
        await component.DeferAsync(ephemeral: true).ConfigureAwait(false);

        var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: PlayerChannelBehavior.None);

        var result = await audioService.Players.RetrieveAsync(
                guildId: guildId,
                memberVoiceChannel: null,
                playerFactory: PlayerFactory.Queued,
                options: playerOptions,
                retrieveOptions: retrieveOptions)
            .ConfigureAwait(false);

        if (!result.IsSuccess || result.Player is null)
        {
            await component.FollowupAsync("Nothing is currently playing.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var player = result.Player;

        switch (action)
        {
            case "toggle":
                if (player.State == PlayerState.Paused)
                {
                    await player.ResumeAsync().ConfigureAwait(false);
                    await component.FollowupAsync("Resumed.", ephemeral: true).ConfigureAwait(false);
                }
                else
                {
                    await player.PauseAsync().ConfigureAwait(false);
                    await component.FollowupAsync("Paused.", ephemeral: true).ConfigureAwait(false);
                }
                break;

            case "skip":
                await player.SkipAsync().ConfigureAwait(false);
                await component.FollowupAsync("Skipped.", ephemeral: true).ConfigureAwait(false);
                break;

            case "stop":
                await player.StopAsync().ConfigureAwait(false);
                await component.FollowupAsync("Stopped.", ephemeral: true).ConfigureAwait(false);
                break;

            default:
                await component.FollowupAsync("Unknown action.", ephemeral: true).ConfigureAwait(false);
                break;
        }
    }
}
