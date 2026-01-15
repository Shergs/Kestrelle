namespace Kestrelle.Api.Status;

public static class GetStatus
{
    public sealed record Response(string Service, string Version, DateTimeOffset UtcNow);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/status", () =>
        {
            var version = typeof(GetStatus).Assembly.GetName().Version?.ToString() ?? "unknown";
            return Results.Ok(new Response("Kestrelle.Api", version, DateTimeOffset.UtcNow));
        });
    }
}