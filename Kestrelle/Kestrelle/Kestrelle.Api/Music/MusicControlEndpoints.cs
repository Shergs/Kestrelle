using Kestrelle.Api.Hubs;
using Kestrelle.Shared;
using Microsoft.AspNetCore.SignalR;

namespace Kestrelle.Api.Music;

public static class MusicControlEndpoints
{
    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "pause",
        "resume",
        "skip",
        "stop",
        "leave",
        "clear-queue",
        "seek",
        "move-queue",
        "set-volume",
        "sync",
        "play"
    };

    public static void Map(RouteGroupBuilder api)
    {
        var group = api.MapGroup("/music/guilds/{guildId}/controls")
            .RequireAuthorization();

        group.MapPost("/{action}", async (
            string guildId,
            string action,
            MusicControlBody? body,
            HttpContext http,
            IHubContext<MusicControlHub> hub,
            CancellationToken ct) =>
        {
            if (!AllowedActions.Contains(action))
                return Results.BadRequest(new { error = "Unsupported control action." });

            var normalized = action.ToLowerInvariant();

            if (normalized == "seek" && body?.PositionMs is null)
                return Results.BadRequest(new { error = "seek requires positionMs." });

            if (normalized == "move-queue" && (body?.FromIndex is null || body?.ToIndex is null))
                return Results.BadRequest(new { error = "move-queue requires fromIndex and toIndex." });

            if (normalized == "set-volume" && body?.Volume is null)
                return Results.BadRequest(new { error = "set-volume requires volume." });

            if (normalized == "play" && string.IsNullOrWhiteSpace(body?.Query))
                return Results.BadRequest(new { error = "play requires query." });

            var payload = new MusicControlRequest(
                guildId,
                normalized,
                DateTimeOffset.UtcNow,
                body?.PositionMs,
                body?.FromIndex,
                body?.ToIndex,
                body?.Volume,
                http.User?.Identity?.Name,
                body?.Query,
                body?.VoiceChannelId);

            await hub.Clients.All.SendAsync("ControlRequested", payload, ct);

            return Results.Accepted();
        });
    }

    public sealed record MusicControlBody(
        long? PositionMs,
        int? FromIndex,
        int? ToIndex,
        int? Volume,
        string? Query,
        string? VoiceChannelId);
}
