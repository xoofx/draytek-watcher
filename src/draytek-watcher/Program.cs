// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Microsoft.Extensions.Configuration;
 using Microsoft.Extensions.Configuration.CommandLine;
 using Microsoft.Extensions.DependencyInjection;
 using Microsoft.Extensions.Hosting;
 using Microsoft.Extensions.Hosting.Systemd;
 using Microsoft.Extensions.Hosting.WindowsServices;
 using Microsoft.Extensions.Logging;
 using Microsoft.Extensions.Logging.Console;
 using DrayTekWatcher.Core;

// https://dfederm.com/building-a-console-app-with-.net-generic-host
// https://devblogs.microsoft.com/dotnet/net-core-and-systemd/
 //var api = new DrayTekRouterApi();
 //await api.Run();
 //return;

var isConsole = !WindowsServiceHelpers.IsWindowsService() && !SystemdHelpers.IsSystemdService();

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(configurationBuilder =>
    {
        configurationBuilder.Sources.Clear();
        if (args.Length > 0)
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { DrayTekWatcherService.ConfigurationFileKey, args[0] }
            });
        }
    })
    .ConfigureLogging(loggingBuilder =>
    {
        if (isConsole)
        {
            loggingBuilder.AddSimpleConsole(options =>
            {
                options.TimestampFormat = "yyyy/MM/dd HH:mm:ss.fff ";
                options.SingleLine = true;
            });
        }
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.Configure<ConsoleFormatterOptions>(options =>
        {
            options.IncludeScopes = true;
        });
        services.AddHostedService<DrayTekWatcherService>();
        services.Configure<ConsoleLifetimeOptions>(options =>
        {
            options.SuppressStatusMessages = true;
        });
    })
    .UseSystemd()
    .UseWindowsService();

if (isConsole)
{
    await builder.RunConsoleAsync();
}
else
{
    await builder.Build().RunAsync();

}