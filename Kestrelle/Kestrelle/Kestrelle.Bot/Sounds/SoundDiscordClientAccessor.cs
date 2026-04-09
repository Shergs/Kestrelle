using Discord;
using Discord.WebSocket;
using Kestrelle.Bot;
using Microsoft.Extensions.Logging;

namespace Kestrelle.Bot.Sounds;

public sealed class SoundDiscordClientAccessor
{
    public SoundDiscordClientAccessor(ILogger<SoundDiscordClientAccessor> logger)
    {
        DaveRuntimeBootstrapper.EnsureInitialized(logger);

        Client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates | GatewayIntents.GuildMembers,
            AlwaysDownloadUsers = true,
            LargeThreshold = 250,
            EnableVoiceDaveEncryption = true,
        });
    }

    public DiscordSocketClient Client { get; }
}
