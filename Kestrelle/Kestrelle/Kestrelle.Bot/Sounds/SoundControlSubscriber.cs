using Kestrelle.Shared;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kestrelle.Bot.Sounds;

internal sealed class SoundControlSubscriber(
    IConfiguration config,
    SoundPlaybackService playbackService,
    ILogger<SoundControlSubscriber> logger) : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private HubConnection? _connection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseUrl = config["KestrelleApi:BaseAddress"];
        var botKey = config["Kestrelle:BotApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(botKey))
        {
            logger.LogWarning("Sound control subscriber disabled: missing KestrelleApi:BaseAddress or Kestrelle:BotApiKey.");
            return;
        }

        var hubUrl = new Uri(new Uri(baseUrl, UriKind.Absolute), "/hubs/sound-control");
        _connection = BuildConnection(hubUrl, botKey, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_connection.State == HubConnectionState.Disconnected)
                    await _connection.StartAsync(stoppingToken).ConfigureAwait(false);

                logger.LogInformation("Connected to sound control hub.");
                await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Sound control hub connection failed. Retrying in {DelaySeconds}s.", ReconnectDelay.TotalSeconds);

                try
                {
                    await Task.Delay(ReconnectDelay, stoppingToken).ConfigureAwait(false);
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

    private HubConnection BuildConnection(Uri hubUrl, string botKey, CancellationToken stoppingToken)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Headers["X-Kestrelle-BotKey"] = botKey;
            })
            .WithAutomaticReconnect()
            .Build();

        connection.On<SoundControlRequest>("ControlRequested", request => HandleControlAsync(request, stoppingToken));
        return connection;
    }

    private async Task HandleControlAsync(SoundControlRequest request, CancellationToken ct)
    {
        logger.LogInformation("Sound control requested: {Action} guild={GuildId} sound={SoundId}", request.Action, request.GuildId, request.SoundId);

        if (!ulong.TryParse(request.GuildId, out var guildId))
            return;

        switch (request.Action.ToLowerInvariant())
        {
            case "play":
                if (request.SoundId is null)
                    return;

                if (string.IsNullOrWhiteSpace(request.VoiceChannelId) || !ulong.TryParse(request.VoiceChannelId, out var voiceChannelId))
                {
                    logger.LogWarning("Ignoring sound play request for guild {GuildId}: missing voice channel id.", request.GuildId);
                    return;
                }

                var result = await playbackService.PlayAsync(guildId, voiceChannelId, request.SoundId.Value, request.User, ct).ConfigureAwait(false);
                if (!result.Success)
                    logger.LogWarning("Sound control play failed for guild {GuildId}: {Message}", request.GuildId, result.Message);
                break;

            case "stop":
                await playbackService.StopAsync(guildId, ct).ConfigureAwait(false);
                break;

            default:
                logger.LogWarning("Unsupported sound control action {Action}", request.Action);
                break;
        }
    }
}
