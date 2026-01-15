using Kestrelle.Api.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace Kestrelle.Api.Auth;

public static class DiscordOAuthExtensions
{
    public static AuthenticationBuilder AddDiscordOAuth(this AuthenticationBuilder builder, IConfiguration config)
    {
        var clientId = config["Discord:ClientId"];
        var clientSecret = config["Discord:ClientSecret"];
        var redirectUri = config["Discord:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId)) throw new InvalidOperationException("Discord:ClientId is missing.");
        if (string.IsNullOrWhiteSpace(clientSecret)) throw new InvalidOperationException("Discord:ClientSecret is missing.");
        if (string.IsNullOrWhiteSpace(redirectUri)) throw new InvalidOperationException("Discord:RedirectUri is missing.");

        return builder.AddOAuth(DiscordAuthDefaults.Scheme, options =>
        {
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;

            // Discord OAuth2 endpoints
            options.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
            options.TokenEndpoint = "https://discord.com/api/oauth2/token";
            options.UserInformationEndpoint = "https://discord.com/api/users/@me";

            // Important: callback is under /api so nginx can proxy it along with other /api requests
            options.CallbackPath = "/api/auth/discord/callback";

            options.Scope.Clear();
            options.Scope.Add("identify");
            options.Scope.Add("guilds"); // required for /users/@me/guilds

            options.SaveTokens = true;

            // Claims
            options.ClaimActions.MapJsonKey(ClaimsPrincipalExtensions.DiscordUserIdClaim, "id");
            options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");

            options.Events = new OAuthEvents
            {
                OnCreatingTicket = async ctx =>
                {
                    // Fetch /users/@me using the access token.
                    using var req = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);

                    using var res = await ctx.Backchannel.SendAsync(req, ctx.HttpContext.RequestAborted);
                    res.EnsureSuccessStatusCode();

                    using var stream = await res.Content.ReadAsStreamAsync(ctx.HttpContext.RequestAborted);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ctx.HttpContext.RequestAborted);

                    ctx.RunClaimActions(doc.RootElement);
                }
            };
        });
    }
}
