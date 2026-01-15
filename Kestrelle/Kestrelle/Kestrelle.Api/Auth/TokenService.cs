using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Kestrelle.Models.Data;
using Kestrelle.Models.Entities;

namespace Kestrelle.Api.Auth;

public sealed class DiscordAuthOptions
{
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string RedirectUri { get; init; } = "";
}

public sealed class TokenService
{
    private readonly KestrelleDbContext _db;
    private readonly HttpClient _http;
    private readonly DiscordAuthOptions _opts;

    public TokenService(KestrelleDbContext db, HttpClient http, IOptions<DiscordAuthOptions> opts)
    {
        _db = db;
        _http = http;
        _opts = opts.Value;
    }

    public async Task<string?> GetAccessTokenAsync(ulong discordUserId, CancellationToken ct)
    {
        var token = await _db.DiscordOAuthTokens
            .SingleOrDefaultAsync(x => x.DiscordUserId == discordUserId, ct);

        if (token is null) return null;

        // Refresh if expiring soon
        if (token.ExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            token = await RefreshAsync(token, ct);
        }

        return token.AccessToken;
    }

    private async Task<DiscordOAuthToken> RefreshAsync(DiscordOAuthToken token, CancellationToken ct)
    {
        // Discord expects application/x-www-form-urlencoded.
        // Token endpoint: https://discord.com/api/oauth2/token
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _opts.ClientId,
            ["client_secret"] = _opts.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = token.RefreshToken,
            ["redirect_uri"] = _opts.RedirectUri
        };

        using var res = await _http.PostAsync(
            "https://discord.com/api/oauth2/token",
            new FormUrlEncodedContent(form),
            ct);

        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync(ct);
        var refreshed = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize token refresh response.");

        token.AccessToken = refreshed.AccessToken;
        token.RefreshToken = refreshed.RefreshToken ?? token.RefreshToken;
        token.Scope = refreshed.Scope ?? token.Scope;
        token.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn);

        await _db.SaveChangesAsync(ct);
        return token;
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn,
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("scope")] string? Scope
    );
}
