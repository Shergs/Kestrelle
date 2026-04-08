using Discord.Interactions;

namespace Kestrelle.Bot.Sounds;

internal sealed class SoundInteractionServiceAdapter
{
    public SoundInteractionServiceAdapter(SoundDiscordClientAccessor clientAccessor)
    {
        Service = new InteractionService(clientAccessor.Client.Rest);
    }

    public InteractionService Service { get; }
}

