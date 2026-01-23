using System.Security.Cryptography;
using System.Text;

namespace Kestrelle.Api.Auth;

public sealed class BotApiKeyEndpointFilter : IEndpointFilter
{
    private const string HeaderName = "X-Kestrelle-BotKey";
    private readonly IConfiguration _config;

    public BotApiKeyEndpointFilter(IConfiguration config) => _config = config;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var expected = _config["Kestrelle:BotApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
            return Results.Problem("Server misconfigured: Kestrelle:BotApiKey is missing.", statusCode: 500);

        var got = ctx.HttpContext.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(got) || !FixedTimeEquals(got, expected))
            return Results.Unauthorized();

        return await next(ctx);
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}

public static class BotApiKeyExtensions
{
    public static RouteGroupBuilder RequireBotApiKey(this RouteGroupBuilder group)
        => group.AddEndpointFilter<BotApiKeyEndpointFilter>();

    public static RouteHandlerBuilder RequireBotApiKey(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<BotApiKeyEndpointFilter>();
}
