using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Kestrelle.Api.Auth;

public static class DiscordAuthEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/auth/discord/login", (HttpContext http, string? returnUrl) =>
        {
            // Redirect back into your SPA route after login (e.g. /dashboard)
            var redirect = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;

            var props = new AuthenticationProperties
            {
                RedirectUri = redirect
            };

            return Results.Challenge(props, new[] { DiscordAuthDefaults.Scheme });
        });

        api.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new { ok = true });
        });

        api.MapGet("/auth/me", (HttpContext http) =>
        {
            var isAuth = http.User?.Identity?.IsAuthenticated == true;
            if (!isAuth)
                return Results.Ok(new { authenticated = false });

            // We mapped ClaimTypes.Name = username
            var username = http.User.Identity?.Name;

            // We mapped discord_user_id in OnCreatingTicket
            var id = http.User.FindFirst(ClaimsPrincipalExtensions.DiscordUserIdClaim)?.Value;

            return Results.Ok(new
            {
                authenticated = true,
                discordUserId = id,
                username
            });
        });
    }
}
