using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Kestrelle.Api.Auth;

namespace Kestrelle.Api.Discord.Guilds;

public static class GetAvailableGuilds
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/discord/available-guilds", async (HttpContext http, IConfiguration config, IHttpClientFactory httpFactory) =>
        {
            if (http.User?.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            // Access token stored in auth ticket because SaveTokens = true.
            var userAccessToken = await http.GetTokenAsync("access_token");
            if (string.IsNullOrWhiteSpace(userAccessToken))
                return Results.Unauthorized();

            var botToken = config["Discord:Token"];
            if (string.IsNullOrWhiteSpace(botToken))
                return Results.Problem("Discord:Token (bot token) is missing from configuration.");

            var client = httpFactory.CreateClient();

            var userGuilds = await GetUserGuildsAsync(client, userAccessToken, http.RequestAborted);
            var botGuildIds = await GetBotGuildIdsAsync(client, botToken, http.RequestAborted);

            var available = userGuilds
                .Where(g => botGuildIds.Contains(g.Id))
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    IconUrl = BuildGuildIconUrl(g.Id, g.Icon)
                })
                .OrderBy(g => g.Name)
                .ToList();

            return Results.Ok(available);
        })
        .RequireAuthorization();
    }

    private static async Task<List<DiscordGuild>> GetUserGuildsAsync(HttpClient client, string userAccessToken, CancellationToken ct)
    {
        // /users/@me/guilds requires OAuth2 scope "guilds".
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me/guilds");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAccessToken);

        using var res = await client.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<DiscordGuild>>(json, JsonOpts) ?? [];
    }

    private static async Task<HashSet<string>> GetBotGuildIdsAsync(HttpClient client, string botToken, CancellationToken ct)
    {
        // Bots can exceed 200 guilds; use limit+after pagination.
        const int limit = 200;

        var ids = new HashSet<string>(StringComparer.Ordinal);
        string? after = null;

        while (true)
        {
            var url = after is null
                ? $"https://discord.com/api/v10/users/@me/guilds?limit={limit}"
                : $"https://discord.com/api/v10/users/@me/guilds?limit={limit}&after={after}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);

            using var res = await client.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync(ct);
            var page = JsonSerializer.Deserialize<List<DiscordGuild>>(json, JsonOpts) ?? [];

            foreach (var g in page)
                ids.Add(g.Id);

            if (page.Count < limit)
                break;

            after = page[^1].Id;
        }

        return ids;
    }

    private static string? BuildGuildIconUrl(string guildId, string? iconHash)
        => string.IsNullOrWhiteSpace(iconHash)
            ? null
            : $"https://cdn.discordapp.com/icons/{guildId}/{iconHash}.png?size=128";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private sealed record DiscordGuild(string Id, string Name, string? Icon);
}
