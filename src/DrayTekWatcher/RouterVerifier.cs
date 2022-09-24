// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

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



