using Microsoft.AspNetCore.Authentication;
using Kestrelle.Api.Auth;

namespace Kestrelle.Api.Discord.Guilds;

public sealed record GuildAccessContext(
    ulong GuildId,
    string GuildIdRaw,
    string GuildName,
    ulong UserId,
    string Username,
    string AccessToken);

public sealed class GuildAccessService(
    IConfiguration config,
    DiscordApiClient discord)
{
    public async Task<(GuildAccessContext? Context, IResult? Failure)> AuthorizeAsync(HttpContext http, string guildIdRaw, CancellationToken ct)
    {
        if (http.User?.Identity?.IsAuthenticated != true)
            return (null, Results.Unauthorized());

        if (!ulong.TryParse(guildIdRaw, out var guildId))
            return (null, Results.BadRequest(new { error = "Invalid guild id." }));

        var accessToken = await http.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
            return (null, Results.Unauthorized());

        var botToken = config["Discord:Token"];
        if (string.IsNullOrWhiteSpace(botToken))
            return (null, Results.Problem("Discord:Token is missing from configuration."));

        var userGuilds = await discord.GetUserGuildsAsync(accessToken, ct);
        var guild = userGuilds.FirstOrDefault(x => string.Equals(x.Id, guildIdRaw, StringComparison.Ordinal));
        if (guild is null)
            return (null, Results.Forbid());

        var botGuildIds = await discord.GetBotGuildIdsAsync(botToken, ct);
        if (!botGuildIds.Contains(guildIdRaw))
            return (null, Results.Forbid());

        var username = http.User.Identity?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(username))
            username = "Unknown";

        var context = new GuildAccessContext(
            GuildId: guildId,
            GuildIdRaw: guildIdRaw,
            GuildName: guild.Name,
            UserId: http.User.GetDiscordUserId(),
            Username: username,
            AccessToken: accessToken);

        return (context, null);
    }
}
