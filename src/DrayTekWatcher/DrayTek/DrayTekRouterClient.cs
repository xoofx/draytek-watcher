namespace DrayTekWatcher.Core.DrayTek;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Connectors;
using Microsoft.Extensions.Logging;

public class DrayTekRouterClient : IDisposable
{
    private readonly SessionClient _client;
    private readonly string _host;
    private readonly int _port;
    private readonly CancellationToken _cancellationToken;

    private DrayTekRouterClient(SessionClient connector, string host, int port, CancellationToken cancellationToken)
    {
        _client = connector;
        _host = host;
        _port = port;
        _cancellationToken = cancellationToken;

        // Telnet prompt asking for input for telnet with Draytek
        _client.PromptMatcher = line => line.StartsWith("DrayTek>", StringComparison.OrdinalIgnoreCase) || line.StartsWith("--- MORE ---", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    public SessionClient Session => _client;


    public static DrayTekRouterClient Telnet(string host, int port = 23, CancellationToken cancellationToken = default)
    {
        return new DrayTekRouterClient(new TelnetClient(), host, port, cancellationToken);
    }

    private async Task Connect()
    {
        if (!_client.Connected)
        {
            await _client.Connect(_host, _port);
        }

        if (!_client.Connected)
        {
            throw new InvalidOperationException("Unable to connect");
        }
    }

    public async Task<string> Login(string user, string password, int timeOutInMillis = 2000)
    {
        if (!_client.Connected)
        {
            await Connect();
        }

        Debug.Assert(_client != null);
        var result = await _client.Login(user, password, _cancellationToken, timeOutInMillis);
        return result;
    }

    public async Task<bool> IsWANOnline(int wanNumber)
    {
        var wanList = await WanStatus();
        var wan = wanList.FirstOrDefault(x => string.Compare(x.Name, $"wan{wanNumber}", StringComparison.OrdinalIgnoreCase) == 0);
        if (wan == null)
        {
            // Log an error
            return false;
        }

        return wan.Status == WANStatus.Online;
    }

    public async Task<string> StartWan(int wanNumber)
    {
        await WriteLine($"internet -W {wanNumber} -M 2");
        var result = await Read();
        return result;
    }

    public async Task<string> StopWan(int wanNumber)
    {
        await WriteLine($"internet -W {wanNumber} -M 0");
        var result = await Read();
        return result;
    }

    public async Task<List<WANInfo>> WanStatus()
    {
        await WriteLine("wan status");
        var status = await Read();
        return DrayTekCliParser.ParseWanStatus(status);
    }

    public async Task WriteLine(string command)
    {
        await _client.WriteLine(command, _cancellationToken);
    }

    public async Task<string> Read()
    {
        var builder = new StringBuilder();
        while (true)
        {
            var data = await _client.Read(_cancellationToken);
            builder.Append(data);
            if (data.Contains("--- MORE ---"))
            {
                await _client.Write(" ", _cancellationToken);
                // Wait for response
                Thread.Sleep(100);
            }
            else
            {
                break;
            }
        }

        return builder.ToString().Replace("\r\n", "\n").Replace("\n\r", "\n");
    }
}