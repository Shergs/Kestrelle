using System.Collections.Concurrent;

namespace Kestrelle.Api.Music;

public sealed class PlaybackStateCache
{
    private readonly ConcurrentDictionary<ulong, PlaybackStateDto> _states = new();

    public void Set(PlaybackStateDto state) => _states[state.GuildId] = state;

    public bool TryGet(ulong guildId, out PlaybackStateDto? state) =>
        _states.TryGetValue(guildId, out state);
}
