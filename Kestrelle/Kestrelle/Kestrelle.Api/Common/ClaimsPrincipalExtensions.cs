using System.Security.Claims;

namespace Kestrelle.Api.Common;

public static class ClaimsPrincipalExtensions
{
    // You set this claim when you complete OAuth (in your callback).
    public const string DiscordUserIdClaim = "discord_user_id";

    public static ulong GetDiscordUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(DiscordUserIdClaim);
        if (string.IsNullOrWhiteSpace(raw) || !ulong.TryParse(raw, out var id))
        {
            throw new InvalidOperationException("Missing or invalid discord_user_id claim. User is not authenticated.");
        }

        return id;
    }
}
