using Discord;
using Discord.WebSocket;
using Kestrelle.Bot.Interactions;
using Kestrelle.Shared.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kestrelle.Bot.Music;

internal sealed class MusicBot(
    DiscordSocketClient client,
    IConfiguration config,
    InteractionHandler interactionHandler,
    ILogger<MusicBot> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Network.WaitForPortAsync("lavalink", 2333, TimeSpan.FromSeconds(60));

        client.Log += msg =>
        {
            logger.Log(
                msg.Severity switch
                {
                    LogSeverity.Critical => LogLevel.Critical,
                    LogSeverity.Error => LogLevel.Error,
                    LogSeverity.Warning => LogLevel.Warning,
                    LogSeverity.Info => LogLevel.Information,
                    LogSeverity.Verbose => LogLevel.Debug,
                    LogSeverity.Debug => LogLevel.Debug,
                    _ => LogLevel.Information
                },
                msg.Exception,
                "{Message}", msg.Message);

            return Task.CompletedTask;
        };

        await interactionHandler.InitializeAsync();

        var token = config["Discord:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Discord:Token is missing from configuration.");
        }

        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();

        logger.LogInformation("Bot started.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
        await client.LogoutAsync();
    }
}
