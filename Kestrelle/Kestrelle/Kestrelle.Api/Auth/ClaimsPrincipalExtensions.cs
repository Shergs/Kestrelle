using System.Security.Claims;

namespace Kestrelle.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public const string DiscordUserIdClaim = "discord_user_id";

    public static ulong GetDiscordUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(DiscordUserIdClaim);
        if (string.IsNullOrWhiteSpace(raw) || !ulong.TryParse(raw, out var id))
            throw new InvalidOperationException("Missing discord user id claim; user not authenticated.");

        return id;
    }
}
