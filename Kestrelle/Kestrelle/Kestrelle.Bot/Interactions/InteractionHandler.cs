using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

namespace Kestrelle.Bot.Interactions;

internal class InteractionHandler(
    DiscordSocketClient client,
    InteractionServiceAdapter adapter,
    IServiceProvider services,
    ILogger<InteractionHandler> logger)
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
        logger.LogInformation(
            "Interaction: type={Type} guildId={GuildId} channelType={ChannelType} userType={UserType} cachedGuild={CachedGuild}",
            interaction.Type,
            interaction.GuildId,
            interaction.Channel?.GetType().Name,
            interaction.User?.GetType().Name,
            interaction.GuildId is ulong gid && client.GetGuild(gid) is not null);

        try
        {
            var context = new SocketInteractionContext(client, interaction);
            await interactions.ExecuteCommandAsync(context, services);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Interaction handling failed.");
            try { await interaction.RespondAsync("Command failed.", ephemeral: true); } catch { /* ignored */ }
        }
    }
}
