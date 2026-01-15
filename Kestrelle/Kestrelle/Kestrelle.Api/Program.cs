using Kestrelle.Api.Auth;
using Kestrelle.Api.Discord;
using Kestrelle.Api.Discord.Guilds;
using Kestrelle.Api.Status;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Auth: cookies + Discord OAuth
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = DiscordAuthDefaults.Scheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "kestrelle_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        // Local dev behind http://localhost:8080
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    })
    .AddDiscordOAuth(builder.Configuration);

builder.Services.AddAuthorization();

// Used by the Discord slice(s)
builder.Services.AddHttpClient();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders =
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;

    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddHttpClient<DiscordApiClient>(client =>
{
    client.BaseAddress = new Uri("https://discord.com/api/");
});

var app = builder.Build();

app.UseForwardedHeaders();

app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");
api.MapGetVoiceChannels();

GetStatus.Map(api);
DiscordAuthEndpoints.Map(api);
GetAvailableGuilds.Map(api);

app.Run();
