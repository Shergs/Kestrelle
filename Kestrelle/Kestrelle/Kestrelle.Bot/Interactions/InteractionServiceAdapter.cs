using Discord.Interactions;
using Discord.WebSocket;

namespace Kestrelle.Bot.Interactions;

public sealed class InteractionServiceAdapter
{
    public InteractionServiceAdapter(DiscordSocketClient client)
    {
        // InteractionService needs an IDiscordClient. Using the REST client is standard practice.
        Service = new InteractionService(client.Rest);
    }

    public InteractionService Service { get; }
}
