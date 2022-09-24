namespace DrayTekWatcher.Core;

using Microsoft.Extensions.Logging;

public abstract class RouterVerifier : IDisposable
{
    protected RouterVerifier(ILogger logger)
    {
        Logger = logger;
    }

    public ILogger Logger { get; }
    
    public abstract void Dispose();

    public abstract Task Login(string username, string password, int loginTimeOutInMillis);

    public abstract Task<bool> Verify(int wanNumber, int retryCount, int sleepBetweenRetryInMillis);
}



