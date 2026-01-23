using Discord;
using Discord.WebSocket;
using Kestrelle.Models.Data;
using Kestrelle.Bot.Interactions;
using Kestrelle.Bot.Music;
using Kestrelle.Bot.Sounds;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Kestrelle.Bot.Realtime;

var builder = Host.CreateApplicationBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Services.AddSingleton(_ =>
{
    var config = new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates
    };

    return new DiscordSocketClient(config);
});

builder.Services.AddKestrelleData(builder.Configuration);

builder.Services.AddSingleton<InteractionServiceAdapter>();
builder.Services.AddSingleton<InteractionHandler>();

builder.Services.AddSingleton<MusicRealtimePublisher>();

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
