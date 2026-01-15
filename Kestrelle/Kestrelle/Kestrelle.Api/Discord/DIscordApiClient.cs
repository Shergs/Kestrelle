using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kestrelle.Api.Discord;

public sealed class DiscordApiClient
{
    private static readonly Uri BaseUri = new("https://discord.com/api/v10/");
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json;

    public DiscordApiClient(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = BaseUri;

        _json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    // ----- User endpoints (Bearer token) -----

    public async Task<DiscordUserDto> GetCurrentUserAsync(string userAccessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "users/@me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAccessToken);

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<DiscordUserDto>(json, _json)
            ?? throw new InvalidOperationException("Failed to deserialize Discord user.");
    }

    /// <summary>
    /// Requires OAuth2 scope: "guilds".
    /// </summary>
    public async Task<IReadOnlyList<DiscordGuildDto>> GetUserGuildsAsync(string userAccessToken, CancellationToken ct)
    {
        // Optional query params include limit/after/before/with_counts.
        using var req = new HttpRequestMessage(HttpMethod.Get, "users/@me/guilds?with_counts=true");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAccessToken);

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<DiscordGuildDto>>(json, _json) ?? [];
    }

    // ----- Bot endpoints (Bot token) -----

    /// <summary>
    /// Lists guilds the bot account is in.
    /// For larger bots, page with limit/after.
    /// </summary>
    public async Task<HashSet<string>> GetBotGuildIdsAsync(string botToken, CancellationToken ct)
    {
        const int limit = 200;

        var ids = new HashSet<string>(StringComparer.Ordinal);
        string? after = null;

        while (true)
        {
            var url = after is null
                ? $"users/@me/guilds?limit={limit}"
                : $"users/@me/guilds?limit={limit}&after={after}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);

            using var res = await _http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync(ct);
            var page = JsonSerializer.Deserialize<List<DiscordGuildDto>>(json, _json) ?? [];

            foreach (var g in page)
            {
                ids.Add(g.Id);
            }

            if (page.Count < limit)
            {
                break;
            }

            after = page[^1].Id;
        }

        return ids;
    }

    public async Task<IReadOnlyList<DiscordChannelDto>> GetGuildChannelsAsync(string botToken, string guildId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"guilds/{guildId}/channels");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<DiscordChannelDto>>(json, _json) ?? [];
    }

    // ----- DTOs -----

    public sealed record DiscordUserDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("global_name")] string? GlobalName,
        [property: JsonPropertyName("avatar")] string? Avatar
    );

    public sealed record DiscordGuildDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("icon")] string? Icon,
        [property: JsonPropertyName("owner")] bool Owner,
        // OAuth guild list often returns permissions as a string bitset.
        [property: JsonPropertyName("permissions")] string? Permissions
    );

    public sealed record DiscordChannelDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] int Type
    );

    public static string? BuildGuildIconUrl(string guildId, string? iconHash, int size = 128)
        => string.IsNullOrWhiteSpace(iconHash)
            ? null
            : $"https://cdn.discordapp.com/icons/{guildId}/{iconHash}.png?size={size}";
}
