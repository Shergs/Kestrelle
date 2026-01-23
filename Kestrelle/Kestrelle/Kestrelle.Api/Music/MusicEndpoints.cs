using Kestrelle.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Kestrelle.Api.Music;

public static class MusicEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        // Client snapshot endpoints (authenticated)
        api.MapGet("/music/guilds/{guildId}/state", (string guildId, MusicStateStore store) =>
        {
            store.TryGetNowPlaying(guildId, out var np);
            store.TryGetQueue(guildId, out var q);

            return Results.Ok(new
            {
                guildId,
                nowPlaying = np,
                queue = q
            });
        })
        .RequireAuthorization();
    }
}
