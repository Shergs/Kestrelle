namespace Kestrelle.Models.Entities;

public sealed class DiscordGuild
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Sound> Sounds { get; set; } = new List<Sound>();
}
