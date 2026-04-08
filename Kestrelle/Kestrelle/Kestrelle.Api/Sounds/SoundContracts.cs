namespace Kestrelle.Api.Sounds;

public sealed record SoundSummaryDto(
    Guid Id,
    string DisplayName,
    string Trigger,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    long DurationMs,
    string UploadedByUsername,
    string ContentUrl,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record UpdateSoundRequest(string DisplayName, string Trigger);

public sealed record PlaySoundRequest(string? VoiceChannelId);
