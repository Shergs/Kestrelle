namespace Kestrelle.Models.Entities;

public sealed class DiscordUser
{
    public ulong Id { get; set; }                    // Discord User ID
    public string Username { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Sound> UploadedSounds { get; set; } = new List<Sound>();
}