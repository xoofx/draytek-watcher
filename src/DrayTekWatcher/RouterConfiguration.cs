// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace DrayTekWatcher.Core;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

public class RouterConfiguration
{
    private int _verifyDelayInSeconds;
    private int _retryCount;
    private int _sleepInMillisBetweenRetryCount;

    /// <summary>
    /// Default timer is 60s
    /// </summary>
    public const int DefaultVerifyTimeInSeconds = 60;

    public const int DefaultRetryCount = 4;

    public const int DefaultSleepInMillisBetweenRetryCount = 10000;

    public RouterConfiguration()
    {
        VerifyDelayInSeconds = DefaultVerifyTimeInSeconds;
        RetryCount = DefaultRetryCount;
        SleepInMillisBetweenRetryCount = DefaultSleepInMillisBetweenRetryCount;
        Type = string.Empty;
        Address = string.Empty;
        Protocol = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        
    }

    public string Type { get; set; }

    public string Address { get; set; }

    public int Port { get; set; }
    
    public string Protocol { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    public int Wan { get; set; }

    public int VerifyDelayInSeconds
    {
        get => _verifyDelayInSeconds;
        set
        {
            value = value <= 0 ? DefaultVerifyTimeInSeconds : value;
            _verifyDelayInSeconds = value;
        }
    }

    public int RetryCount
    {
        get => _retryCount;
        set
        {
            value = value <= 0 ? DefaultRetryCount : value;
            _retryCount = value;
        }
    }

    /// <summary>
    /// Gets or sets the sleeping time in millis
    /// </summary>
    public int SleepInMillisBetweenRetryCount
    {
        get => _sleepInMillisBetweenRetryCount;
        set
        {
            value = value < 0 ? DefaultSleepInMillisBetweenRetryCount : value;
            _sleepInMillisBetweenRetryCount = value;
        }
    }

    public override string ToString()
    {
        return $"{nameof(Type)}: {Type}, {nameof(Address)}: {Address}, {nameof(Port)}: {Port}, {nameof(Protocol)}: {Protocol}, {nameof(Username)}: {Username}, {nameof(Password)}: {(Password == null ? null : new string('*', Password.Length))}, {nameof(VerifyDelayInSeconds)}: {VerifyDelayInSeconds}v";
    }

    public static bool TryReadFromFile(string? filePath, ILogger logger, [NotNullWhen(true)] out RouterConfiguration? configuration)
    {
        configuration = null;
        if (filePath == null)
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            var assemblyPath = Path.GetDirectoryName(entryAssembly!.Location);
            filePath = Path.Combine(assemblyPath!, $"{entryAssembly.GetName().Name}.toml");

            logger.LogWarning($"Missing configuration file. Defaulting to {filePath}.");
        }

        if (!File.Exists(filePath))
        {
            logger.LogError($"Configuration file `{filePath}' not found.");
            return false;
        }

        var content = File.ReadAllText(filePath);
        if (Tomlyn.Toml.TryToModel<RouterConfiguration>(content, out configuration, out var diagnostics, filePath))
        {
            return true;
        }

        // Log any messages
        foreach (var message in diagnostics)
        {
            if (message.Kind == DiagnosticMessageKind.Error)
            {
                logger.LogError(message.ToString());
            }
            else if (message.Kind == DiagnosticMessageKind.Warning)
            {
                logger.LogWarning(message.ToString());
            }
        }

        return false;
    }
}