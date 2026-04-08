namespace Kestrelle.Shared;

public sealed record SoundControlRequest(
    string GuildId,
    string Action,
    DateTimeOffset RequestedUtc,
    Guid? SoundId = null,
    string? VoiceChannelId = null,
    string? User = null);
