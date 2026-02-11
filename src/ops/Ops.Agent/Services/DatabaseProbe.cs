using System.Net.Sockets;

namespace Ops.Agent.Services;

public sealed class DatabaseProbe
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public async Task<DatabaseProbeResult> CheckAsync(string connectionString, CancellationToken ct)
    {
        var info = BackupRunner.ParseConnectionInfo(connectionString);
        var host = info.Host;
        var port = info.Port;

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(DefaultTimeout, ct);
            var completed = await Task.WhenAny(connectTask, timeoutTask);
            if (completed == timeoutTask)
                return new DatabaseProbeResult(host, port, false, "Connection timed out");

            if (!client.Connected)
                return new DatabaseProbeResult(host, port, false, "Connection failed");

            return new DatabaseProbeResult(host, port, true, "OK");
        }
        catch (Exception ex)
        {
            return new DatabaseProbeResult(host, port, false, ex.Message);
        }
    }
}

public sealed record DatabaseProbeResult(
    string Host,
    int Port,
    bool Reachable,
    string Message);
