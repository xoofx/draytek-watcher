namespace DrayTekWatcher.Core.DrayTek;

using Microsoft.Extensions.Logging;

public class DrayTekRouterVerifier : RouterVerifier
{
    private readonly DrayTekRouterClient _routerClient;

    public DrayTekRouterVerifier(DrayTekRouterClient routerClient, ILogger logger) : base(logger)
    {
        _routerClient = routerClient;
    }

    public override void Dispose()
    {
        _routerClient.Dispose();
    }

    public override async Task Login(string username, string password, int loginTimeOutInMillis)
    {
        await _routerClient.Login(username, password, loginTimeOutInMillis);
    }

    public override async Task<bool> Verify(int wanNumber, int retryCount, int sleepBetweenRetryInMillis)
    {
        if (await _routerClient.IsWANOnline(wanNumber)) return true;

        Logger.LogWarning($"is offline.");

        for (int i = 0; i < retryCount; i++)
        {
            Logger.LogWarning($"Trying [{i + 1}/{retryCount}] to restart it.");
            await RestartWan(wanNumber);

            Logger.LogWarning($"Waiting to restart for {sleepBetweenRetryInMillis / 1000.0}s.");
            await Task.Delay(sleepBetweenRetryInMillis);

            if (await _routerClient.IsWANOnline(wanNumber))
            {
                Logger.LogInformation($"Restarted successfully.");
                return true;
            }
            else
            {
                Logger.LogError($"Restarting failed.");
            }
        }

        return false;
    }

    internal async Task RestartWan(int wanNumber)
    {
        Logger.LogWarning($"Stopping WAN.");
        var result = await _routerClient.StopWan(wanNumber);
        Logger.LogTrace(result);

        Logger.LogWarning($"Waiting WAN for 2s before trying to start .");
        Thread.Sleep(2000);

        Logger.LogWarning($"Starting WAN.");
        result = await _routerClient.StartWan(wanNumber);
        Logger.LogTrace(result);
    }
}