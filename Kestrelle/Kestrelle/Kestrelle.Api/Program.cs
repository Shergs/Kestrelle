using Kestrelle.Api.Auth;
using Kestrelle.Api.Discord;
using Kestrelle.Api.Discord.Guilds;
using Kestrelle.Api.Hubs;
using Kestrelle.Api.Music;
using Kestrelle.Api.Status;
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
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    })
    .AddDiscordOAuth(builder.Configuration);

builder.Services.AddAuthorization();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient<DiscordApiClient>(client =>
{
    client.BaseAddress = new Uri("https://discord.com/api/");
});

builder.Services.AddSignalR();

builder.Services.AddSingleton<MusicStateStore>();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders =
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;

    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");

GetStatus.Map(api);
DiscordAuthEndpoints.Map(api);
GetAvailableGuilds.Map(api);
MusicEndpoints.Map(api);
MusicRealtimeIngestEndpoints.Map(api);
MusicControlEndpoints.Map(api);

api.MapGetVoiceChannels();

app.MapHub<MusicHub>("/hubs/music");
app.MapHub<MusicControlHub>("/hubs/music-control");

app.Run();
