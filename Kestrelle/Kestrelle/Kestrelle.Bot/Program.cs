using Discord;
using Discord.WebSocket;
using Kestrelle.Bot.Music;
using Kestrelle.Bot.Interactions;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(_ =>
{
    var config = new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates
    };

    return new DiscordSocketClient(config);
});

builder.Services.AddSingleton<InteractionServiceAdapter>();
builder.Services.AddSingleton<InteractionHandler>();

builder.Services.AddLavalink();

builder.Services.ConfigureLavalink(options =>
{
    var lavalinkSection = builder.Configuration.GetSection("Lavalink");
    options.BaseAddress = new Uri(lavalinkSection["BaseAddress"]!);
    options.WebSocketUri = new Uri(lavalinkSection["WebSocketUri"]!);
    options.Passphrase = lavalinkSection["Passphrase"]!;
});

builder.Services.AddHostedService<MusicBot>();

var host = builder.Build();
await host.RunAsync();