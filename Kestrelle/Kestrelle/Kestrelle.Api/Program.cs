using Kestrelle.Api.Auth;
using Kestrelle.Api.Discord;
using Kestrelle.Api.Discord.Guilds;
using Kestrelle.Api.Hubs;
using Kestrelle.Api.Music;
using Kestrelle.Api.Sounds;
using Kestrelle.Api.Status;
using Kestrelle.Models.Data;
using Kestrelle.Shared;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddKestrelleData(builder.Configuration);
builder.Services.Configure<SoundStorageOptions>(builder.Configuration.GetSection("Sounds"));
builder.Services.AddScoped<GuildAccessService>();
builder.Services.AddSingleton<ISoundStorage, LocalDiskSoundStorage>();
builder.Services.AddSingleton<ISoundFileMetadataReader, FfprobeSoundFileMetadataReader>();

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

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KestrelleDbContext>();
    await db.Database.MigrateAsync();
}

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
SoundEndpoints.Map(api);

api.MapGetVoiceChannels();

app.MapHub<MusicHub>("/hubs/music");
app.MapHub<MusicControlHub>("/hubs/music-control");
app.MapHub<SoundControlHub>("/hubs/sound-control");

app.Run();
