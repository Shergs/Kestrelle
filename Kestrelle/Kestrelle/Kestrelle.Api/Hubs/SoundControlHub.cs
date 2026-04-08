using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;

namespace Kestrelle.Api.Hubs;

public sealed class SoundControlHub(IConfiguration config) : Hub
{
    private const string HeaderName = "X-Kestrelle-BotKey";

    public override Task OnConnectedAsync()
    {
        var expected = config["Kestrelle:BotApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            Context.Abort();
            return Task.CompletedTask;
        }

        var got = Context.GetHttpContext()?.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(got) || !FixedTimeEquals(got, expected))
        {
            Context.Abort();
            return Task.CompletedTask;
        }

        return base.OnConnectedAsync();
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
