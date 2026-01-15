namespace Kestrelle.Api.Music;

public sealed record QueueItemDto(string Title, string Url, long DurationMs);

public sealed record PlaybackStateDto(
    ulong GuildId,
    bool IsPlaying,
    bool IsPaused,
    string? TrackTitle,
    string? TrackUrl,
    long PositionMs,
    long DurationMs,
    IReadOnlyList<QueueItemDto> Queue
);
