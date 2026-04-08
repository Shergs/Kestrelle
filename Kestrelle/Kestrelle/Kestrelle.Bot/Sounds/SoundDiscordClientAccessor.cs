using Discord;
using Discord.WebSocket;

namespace Kestrelle.Bot.Sounds;

public sealed class SoundDiscordClientAccessor
{
    public SoundDiscordClientAccessor()
    {
        Client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
            EnableVoiceDaveEncryption = true,
        });
    }

    public DiscordSocketClient Client { get; }
}
