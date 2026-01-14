using System.Net.Sockets;

namespace Kestrelle.Shared.Util;

public class Network
{
    public static async Task WaitForPortAsync(string host, int port, TimeSpan timeout)
    {
        var start = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync(host, port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(1000));
                if (completed == connectTask && tcp.Connected)
                {
                    return;
                }
            }
            catch
            {
                // ignore and retry
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Timed out waiting for {host}:{port}");
    }
}
