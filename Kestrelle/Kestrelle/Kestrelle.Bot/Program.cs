using Discord;
using Discord.WebSocket;
using Kestrelle.Bot;
using Kestrelle.Models.Data;
using Kestrelle.Bot.Interactions;
using Kestrelle.Bot.Music;
using Kestrelle.Bot.Realtime;
using Kestrelle.Bot.Sounds;
using Kestrelle.Shared;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DaveRuntimeBootstrapper");
    DaveRuntimeBootstrapper.EnsureInitialized(logger);

    var config = new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
        EnableVoiceDaveEncryption = true,
    };

    return new DiscordSocketClient(config);
});

builder.Services.AddSingleton<SoundDiscordClientAccessor>();

builder.Services.AddKestrelleData(builder.Configuration);
builder.Services.Configure<SoundStorageOptions>(builder.Configuration.GetSection("Sounds"));

builder.Services.AddSingleton<InteractionServiceAdapter>();
builder.Services.AddSingleton<InteractionHandler>();
builder.Services.AddSingleton<SoundInteractionServiceAdapter>();
builder.Services.AddSingleton<SoundInteractionHandler>();

builder.Services.AddSingleton<MusicRealtimePublisher>();
builder.Services.AddSingleton<SoundPlaybackService>();

builder.Services.AddLavalink();

builder.Services.ConfigureLavalink(options =>
{
    var lavalinkSection = builder.Configuration.GetSection("Lavalink");
    options.BaseAddress = new Uri(lavalinkSection["BaseAddress"]!);
    options.WebSocketUri = new Uri(lavalinkSection["WebSocketUri"]!);
    options.Passphrase = lavalinkSection["Passphrase"]!;
});

builder.Services.AddHostedService<MusicBot>();
builder.Services.AddHostedService<SoundBot>();
builder.Services.AddHostedService<MusicControlSubscriber>();
builder.Services.AddHostedService<SoundControlSubscriber>();

builder.Services.AddHttpClient<MusicRealtimePublisher>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["KestrelleApi:BaseAddress"];

    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("KestrelleApi:BaseAddress is missing.");

    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(5);
});

var host = builder.Build();
await host.RunAsync();
