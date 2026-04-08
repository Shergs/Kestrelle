using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Kestrelle.Bot.Sounds;

internal sealed class SoundInteractionHandler(
    SoundDiscordClientAccessor clientAccessor,
    SoundInteractionServiceAdapter adapter,
    IServiceProvider services,
    ILogger<SoundInteractionHandler> logger)
{
    private readonly DiscordSocketClient _client = clientAccessor.Client;
    private InteractionService Interactions => adapter.Service;

    public async Task InitializeAsync()
    {
        _client.InteractionCreated += HandleInteractionAsync;

        Interactions.Log += message =>
        {
            if (message.Exception is not null)
            {
                logger.LogError(message.Exception, "[Sound:{Source}] {Message}", message.Source, message.Message ?? "<no message>");
            }
            else
            {
                logger.LogInformation("[Sound:{Source}] {Severity}: {Message}", message.Source, message.Severity, message.Message ?? "<no message>");
            }

            return Task.CompletedTask;
        };

        await Interactions.AddModuleAsync<SoundModule>(services);

        _client.Ready += async () =>
        {
            const ulong bigOneGuildId = 783190942806835200;
            await Interactions.RegisterCommandsToGuildAsync(bigOneGuildId);
            logger.LogInformation("Sound slash commands registered.");
        };
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(_client, interaction);
            await Interactions.ExecuteCommandAsync(context, services).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sound interaction handling failed.");
            try
            {
                if (!interaction.HasResponded)
                    await interaction.RespondAsync("Command failed.", ephemeral: true).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }
}
