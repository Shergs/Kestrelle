using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kestrelle.Bot.Realtime;

public sealed class MusicRealtimePublisher
{
    private const string HeaderName = "X-Kestrelle-BotKey";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<MusicRealtimePublisher> _logger;

    public MusicRealtimePublisher(HttpClient http, IConfiguration config, ILogger<MusicRealtimePublisher> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    private void ApplyBotKey(HttpRequestMessage req)
    {
        var key = _config["Kestrelle:BotApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            req.Headers.TryAddWithoutValidation(HeaderName, key);
    }

    private async Task PostAsync(string path, object payload, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(payload),
            };
            ApplyBotKey(req);

            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogWarning("Realtime publish failed: {Status} {Path}. Body: {Body}", (int)res.StatusCode, path, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Realtime publish threw for {Path}", path);
        }
    }

    public Task PublishNowPlayingAsync(object payload, CancellationToken ct)
        => PostAsync("/api/internal/now-playing", payload, ct);

    public Task PublishQueueAsync(object payload, CancellationToken ct)
        => PostAsync("/api/internal/queue", payload, ct);

    public Task PublishToastAsync(object payload, CancellationToken ct)
        => PostAsync("/api/internal/toast", payload, ct);
}