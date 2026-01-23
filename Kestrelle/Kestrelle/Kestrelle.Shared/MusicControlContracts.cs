namespace Kestrelle.Shared;

public sealed record MusicControlRequest(
    string GuildId,
    string Action,
    DateTimeOffset RequestedUtc,
    long? PositionMs = null,
    int? FromIndex = null,
    int? ToIndex = null,
    int? Volume = null,
    string? User = null,
    string? Query = null,
    string? VoiceChannelId = null);
