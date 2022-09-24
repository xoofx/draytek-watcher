// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace DrayTekWatcher.Core.Connectors;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Base class for a session based connector.
/// </summary>
public abstract class SessionClient : IDisposable
{
    private readonly BlockingCollection<string> _queue;
    private bool _isDisposed;
    private Task? _runningReadStream;

    protected SessionClient()
    {
        _queue = new BlockingCollection<string>();
        PromptMatcher = _ => true;
    }

    public abstract bool Connected { get; }

    public Func<string, bool> PromptMatcher { get; set; }

    public TextWriter? OutputDebug { get; set; }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _runningReadStream?.Wait();
        DisposeImpl();
    }

    public async Task Connect(string host, int port, CancellationToken cancellationToken = default)
    {
        if (Connected) return;

        await ConnectImpl(host, port, cancellationToken);

        _runningReadStream = Task.Run(ReadStreamTask);
    }

    public async Task<string> Login(string username, string password, CancellationToken cancellationToken = default, int loginTimeOutMs = 1000)
    {
        EnsureConnected();
        return await LoginImpl(username, password, cancellationToken, loginTimeOutMs);
    }

    public async Task WriteLine(string cmd, CancellationToken cancellationToken = default)
    {
        await Write(cmd + "\n", cancellationToken);
    }

    public async Task Write(string cmd, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await WriteImpl(cmd, cancellationToken);
    }

    protected abstract Task WriteImpl(string cmd, CancellationToken cancellationToken = default);

    public async Task<string> Read(CancellationToken cancellationToken = default, int timeout = 20000)
    {
        EnsureConnected();
        var builder = new StringBuilder();
        // Check if we don't have more data packed and recompose the strings
        var clock = Stopwatch.StartNew();
        bool isFirst = true;
        while (!_queue.IsCompleted)
        {
            bool promptFound = false;
            if (_queue.TryTake(out var data))
            {
                data = NormalizeLines(data);
                var reader = new StringReader(data);
                string? line;
                // ReSharper disable once MethodHasAsyncOverload
                while ((line = reader.ReadLine()) != null)
                {
                    if (!isFirst)
                    {
                        builder.Append('\n');
                    }
                    builder.Append(line);
                    isFirst = false;

                    if (PromptMatcher != null && PromptMatcher(line))
                    {
                        promptFound = true;
                        break;
                    }
                }

                // Append a line only if we have a pending line at the end
                if (data.EndsWith('\n'))
                {
                    builder.Append('\n');
                }
            }

            if (promptFound) break;

            await Task.Delay(1, cancellationToken);
            if (timeout > 0 && clock.ElapsedMilliseconds > timeout)
            {
                throw new TimeoutException($"Reading from session has expired after waiting for input for {(double)timeout / 1000}s. Received: {builder}");
            }
        }

        if (OutputDebug is not null) await OutputDebug.WriteLineAsync($"Received {builder}");

        return builder.ToString();
    }

    protected abstract Task ConnectImpl(string host, int port, CancellationToken cancellationToken);

    protected abstract void DisposeImpl();

    protected abstract Task<string> LoginImpl(string username, string password, CancellationToken cancellationToken, int loginTimeOutMs);

    private void ReadStreamTask()
    {
        var sb = new StringBuilder();
        while (!_isDisposed && Connected)
        {
            if (!ReadFromStreamImpl(sb))
            {
                _queue.CompleteAdding();
                return;
            }

            Thread.Sleep(1);
            if (sb.Length > 0)
            {
                var str = sb.ToString();
                _queue.Add(str);
                sb.Clear();
            }
        }
    }

    protected abstract bool ReadFromStreamImpl(StringBuilder sb);

    private static string NormalizeLines(string lines)
    {
        // Lines can come with \n\r, which would then be read as 2 lines by ReadLine()
        // So we fix this here and remapping \r\n or \n\r to a single \n
        return lines.Replace("\r\n", "\n").Replace("\n\r", "\n");
    }

    protected static Func<string, bool> CreateMatcherFromRegex(Regex regex)
    {
        return s => regex.Match(s).Success;
    }

    protected void EnsureConnected()
    {
        if (!Connected)
        {
            throw new IOException("Session not connected.");
        }
    }
}