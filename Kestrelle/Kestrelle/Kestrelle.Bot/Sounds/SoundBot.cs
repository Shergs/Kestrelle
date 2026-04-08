using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kestrelle.Bot.Sounds;

internal sealed class SoundBot(
    SoundDiscordClientAccessor clientAccessor,
    IConfiguration config,
    SoundInteractionHandler interactionHandler,
    ILogger<SoundBot> logger) : IHostedService
{
    private readonly DiscordSocketClient _client = clientAccessor.Client;
    private bool _started;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += msg =>
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
                "[SoundBot] {Message}", msg.Message);

            return Task.CompletedTask;
        };

        var token = config["Discord:SoundToken"];
        if (string.IsNullOrWhiteSpace(token) || token.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Sound bot disabled: Discord:SoundToken is missing or still set to a placeholder value.");
            return;
        }

        await interactionHandler.InitializeAsync().ConfigureAwait(false);
        await _client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
        await _client.StartAsync().ConfigureAwait(false);

        _started = true;
        logger.LogInformation("Sound bot started.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_started)
            return;

        await _client.StopAsync().ConfigureAwait(false);
        await _client.LogoutAsync().ConfigureAwait(false);
    }
}
