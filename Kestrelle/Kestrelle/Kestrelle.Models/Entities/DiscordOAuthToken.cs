namespace Kestrelle.Models.Entities;

public sealed class DiscordOAuthToken
{
    public ulong DiscordUserId { get; set; }

    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string Scope { get; set; } = "";

    public DateTimeOffset ExpiresAtUtc { get; set; }

    // Optional navigation if you want it:
    public DiscordUser? User { get; set; }
}
