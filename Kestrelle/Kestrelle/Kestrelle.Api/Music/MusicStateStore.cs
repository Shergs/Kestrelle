using System.Collections.Concurrent;

namespace Kestrelle.Api.Music;

public sealed class MusicStateStore
{
    private readonly ConcurrentDictionary<string, NowPlayingState> _nowPlaying = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, QueueState> _queues = new(StringComparer.Ordinal);

    public void UpsertNowPlaying(NowPlayingState state)
    {
        _nowPlaying.AddOrUpdate(
            state.GuildId,
            _ => state,
            (_, existing) =>
            {
                var incoming = state.Track;
                var previous = existing.Track;

                if (incoming is not null && previous is not null)
                {
                    var sameTrack =
                        string.Equals(incoming.Uri, previous.Uri, StringComparison.Ordinal) ||
                        string.Equals(incoming.Title, previous.Title, StringComparison.Ordinal);

                    if (sameTrack && string.IsNullOrWhiteSpace(incoming.RequestedBy))
                    {
                        incoming = incoming with { RequestedBy = previous.RequestedBy };
                    }

                    if (sameTrack && state.PositionMs <= 0 && existing.PositionMs > 0)
                    {
                        return state with
                        {
                            Track = incoming,
                            PositionMs = existing.PositionMs,
                            UpdatedUtc = existing.UpdatedUtc
                        };
                    }
                }

                return state with { Track = incoming };
            });
    }
    public void UpsertQueue(QueueState state) => _queues[state.GuildId] = state;

    public bool TryGetNowPlaying(string guildId, out NowPlayingState? state)
    {
        if (_nowPlaying.TryGetValue(guildId, out var s))
        {
            state = s;
            return true;
        }

        state = null;
        return false;
    }

    public bool TryGetQueue(string guildId, out QueueState? state)
    {
        if (_queues.TryGetValue(guildId, out var s))
        {
            state = s;
            return true;
        }

        state = null;
        return false;
    }
}
