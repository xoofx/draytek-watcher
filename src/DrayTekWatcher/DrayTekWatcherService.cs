// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace DrayTekWatcher.Core;

using DrayTek;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class DrayTekWatcherService : IHostedService
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly CancellationToken _cancellationToken;

    public const string ConfigurationFileKey = $"{nameof(DrayTekWatcher)}.ConfigurationFile";

    public DrayTekWatcherService(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IHostApplicationLifetime applicationLifetime)
    {
        _logger = loggerFactory.CreateLogger("draytek-watcher");
        _configuration = configuration;
        _applicationLifetime = applicationLifetime;
        _cancellationToken = applicationLifetime.ApplicationStopping;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Starting with arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");

        _applicationLifetime.ApplicationStarted.Register(OnStarted);
        _applicationLifetime.ApplicationStopping.Register(OnStopping);
        _applicationLifetime.ApplicationStopped.Register(OnStopped);

        return Task.CompletedTask;
    }

    private void OnStarted()
    {
        _logger.LogInformation("Service started.");
        _ = Task.Run(async () =>
        {
            try
            {
                await MonitorAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not TaskCanceledException)
            {
                _logger.LogError(exception, "Unexpected error.");
            }
        }, _cancellationToken);
    }

    private void OnStopping()
    {
        _logger.LogInformation("Stopping service.");
    }

    private void OnStopped()
    {
        _logger.LogInformation("Service stopped.");
    }

    private async ValueTask MonitorAsync()
    {
        foreach (var keyPair in _configuration.AsEnumerable())
        {
            _logger.LogInformation($"{keyPair.Key} = {keyPair.Value}");
        }

        if (!RouterConfiguration.TryReadFromFile(_configuration[ConfigurationFileKey], _logger, out var configuration))
        {
            _logger.LogError("Service suspended due to previous errors while trying to load the configuration file.");
            return;
        }

        bool isFirstVerify = true;
        while (!_cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var routerVerifier = CreateRouterVerifier(configuration);
                if (routerVerifier is not null)
                {
                    using var scope = _logger.BeginScope($"WAN{configuration.Wan} -");

                    await routerVerifier.Login(configuration.Username ?? "admin", configuration.Password ?? "admin", 2000);
                    var verifyResult = await routerVerifier.Verify(configuration.Wan, configuration.RetryCount, configuration.SleepInMillisBetweenRetryCount);
                    if (!verifyResult)
                    {
                        _logger.LogError($"Restarting failed after {configuration.RetryCount} attempts. Waiting for {configuration.VerifyDelayInSeconds}s for next verify round.");
                    }
                    else
                    {
                        if (isFirstVerify)
                        {
                            _logger.LogInformation($"is online.");
                            _logger.LogInformation($"Verifying every {configuration.VerifyDelayInSeconds}s.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error when checking router. Reason: {ex.Message}");
            }

            await Task.Delay(configuration.VerifyDelayInSeconds * 1000, _cancellationToken);

            isFirstVerify = false;
        }
    }

    private RouterVerifier? CreateRouterVerifier(RouterConfiguration configuration)
    {
        if (string.Compare(configuration.Type, "DrayTek", StringComparison.OrdinalIgnoreCase) == 0)
        {
            if (string.Compare(configuration.Protocol, "telnet", StringComparison.OrdinalIgnoreCase) == 0)
            {
                var routerClient = DrayTekRouterClient.Telnet(configuration.Address, configuration.Port, _cancellationToken);
                return new DrayTekRouterVerifier(routerClient, _logger);
            }
        }

        _logger.LogError("No router client found for this type/protocol.");
        return null;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}