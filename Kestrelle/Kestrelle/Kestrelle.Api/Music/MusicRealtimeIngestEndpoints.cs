using Kestrelle.Api.Auth;
using Kestrelle.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Kestrelle.Api.Music;

public static class MusicRealtimeIngestEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        // Bot-only endpoints (protected by shared API key middleware/extension)
        var internalApi = api.MapGroup("/internal").RequireBotApiKey();

        internalApi.MapPost("/now-playing", async (
            NowPlayingState payload,
            MusicStateStore store,
            IHubContext<MusicHub> hub,
            CancellationToken ct) =>
        {
            store.UpsertNowPlaying(payload);

            await hub.Clients.Group(MusicHub.GroupName(payload.GuildId))
                .SendAsync("NowPlayingUpdated", payload, ct);

            return Results.Ok();
        });

        internalApi.MapPost("/queue", async (
            QueueState payload,
            MusicStateStore store,
            IHubContext<MusicHub> hub,
            CancellationToken ct) =>
        {
            store.UpsertQueue(payload);

            await hub.Clients.Group(MusicHub.GroupName(payload.GuildId))
                .SendAsync("QueueUpdated", payload, ct);

            return Results.Ok();
        });

        internalApi.MapPost("/toast", async (
            BotToast payload,
            IHubContext<MusicHub> hub,
            CancellationToken ct) =>
        {
            await hub.Clients.Group(MusicHub.GroupName(payload.GuildId))
                .SendAsync("Toast", payload, ct);

            return Results.Ok();
        });

        // Authenticated snapshot for initial page load
        api.MapGet("/music/{guildId}/state", (
            string guildId,
            MusicStateStore store) =>
        {
            store.TryGetNowPlaying(guildId, out var nowPlaying);
            store.TryGetQueue(guildId, out var queue);

            return Results.Ok(new
            {
                guildId,
                nowPlaying,
                queue
            });
        })
        .RequireAuthorization();
    }
}
