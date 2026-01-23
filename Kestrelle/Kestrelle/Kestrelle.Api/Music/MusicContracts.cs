namespace Kestrelle.Api.Music;

public sealed record TrackDto(
    string Title,
    string? Author,
    string? Uri,
    long DurationMs,
    string? ArtworkUrl,
    string? RequestedBy);

public sealed record NowPlayingState(
    string GuildId,
    TrackDto? Track,
    long PositionMs,
    bool IsPaused,
    int Volume,
    DateTimeOffset UpdatedUtc);

public sealed record QueueItemDto(
    string Title,
    string? Author,
    string? Uri,
    long DurationMs,
    string? ArtworkUrl,
    string? RequestedBy);

public sealed record QueueState(
    string GuildId,
    IReadOnlyList<QueueItemDto> Tracks,
    DateTimeOffset UpdatedUtc);

public sealed record BotToast(
    string GuildId,
    string Kind,
    string Message,
    string? User,
    DateTimeOffset OccurredUtc);
