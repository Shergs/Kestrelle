namespace Kestrelle.Models.Entities;

public enum SoundStorageProvider
{
    Unknown = 0,
    LocalDisk = 1,
    S3 = 2,
    AzureBlob = 3
}

public sealed class Sound
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ulong GuildId { get; set; }
    public DiscordGuild Guild { get; set; } = null!;

    public ulong UploadedByUserId { get; set; }
    public DiscordUser UploadedByUser { get; set; } = null!;

    public string DisplayName { get; set; } = string.Empty;

    // File metadata
    public SoundStorageProvider StorageProvider { get; set; } = SoundStorageProvider.Unknown;
    public string StorageKey { get; set; } = string.Empty;          // e.g. "guild/123/sounds/abc.mp3" or local path
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }

    public TimeSpan? Duration { get; set; }

    public bool IsPublicWithinGuild { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
