// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace DrayTekWatcher.Core.Connectors;

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

// minimalistic telnet implementation
// Inspired by Tom Janssens on 2007/06/06  for CodeProject
// Improved to better handle connection process to telnet

/// <summary>
/// Telnet connector.
/// </summary>
public class TelnetClient : SessionClient
{
    private TcpClient? _tcpSocket;

    public override bool Connected => _tcpSocket?.Connected ?? false;

    protected override Task ConnectImpl(string host, int port, CancellationToken cancellationToken)
    {
        _tcpSocket = new TcpClient(host, port);
        // SENT DO SUPPRESS GO AHEAD
        //SendCommand(Command.Do, OptionCode.SuppressGoAhead);
        // SENT DONT ECHO
        SendCommand(Command.Dont, OptionCode.Echo);
        // SENT WILL LINEMODE
        SendCommand(Command.Will, OptionCode.LineMode);
        // SENT WILL LFLOW
        SendCommand(Command.Will, OptionCode.ControlFlow);

        return Task.CompletedTask;
    }

    protected override void DisposeImpl()
    {
        _tcpSocket?.Dispose();
    }

    protected override async Task<string> LoginImpl(string username, string password, CancellationToken cancellationToken, int loginTimeOutMs)
    {
        var promptMatcher = PromptMatcher;
        string line;
        var result = new StringBuilder();
        try
        {
            // Wait for 500ms to let the session exchange IAC controls
            await Task.Delay(500, cancellationToken);

            PromptMatcher = CreateMatcherFromRegex(new Regex(@"^.*:\s*"));
            try
            {
                line = await Read(cancellationToken, loginTimeOutMs);
            }
            catch (TimeoutException timeout)
            {
                throw new TimeoutException($"Failed to connect: no login prompt. {timeout.Message}");
            }
            result.Append(line);
            await WriteLine(username, cancellationToken);

            try
            {
                line = await Read(cancellationToken, loginTimeOutMs);
            }
            catch (TimeoutException timeout)
            {
                throw new TimeoutException($"Failed to connect: no login prompt. {timeout.Message}");
            }
            result.Append(line);
            await WriteLine(password, cancellationToken);
        }
        finally
        {
            PromptMatcher = promptMatcher;
        }
        // Expecting some output after login
        line = await Read(cancellationToken);
        result.Append(line);
        return result.ToString();
    }

    protected override async Task WriteImpl(string cmd, CancellationToken cancellationToken = default)
    {
        if (OutputDebug is not null) await OutputDebug.WriteLineAsync($"[{DateTime.Now:O}] SEND text: {cmd}");
        Debug.Assert(_tcpSocket != null);
        byte[] buf = Encoding.ASCII.GetBytes(cmd.Replace("\0xFF", "\0xFF\0xFF"));
        var stream = _tcpSocket.GetStream();
        await stream.WriteAsync(buf, 0, buf.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    // https://www.iana.org/assignments/telnet-options/telnet-options.xhtml
    protected override bool ReadFromStreamImpl(StringBuilder sb)
    {
        if (_tcpSocket == null) return false;

        var stream = _tcpSocket.GetStream();
        while (_tcpSocket.Available > 0)
        {
            int input = stream.ReadByte();
            switch ((Command)input)
            {
                case Command.EndOfStream:
                    break;
                case Command.IAC:
                    // interpret as command
                    var cmd = (Command)stream.ReadByte();
                    if (cmd == Command.EndOfStream) break;
                    switch (cmd)
                    {
                        case Command.IAC:
                            //literal IAC = 255 escaped, so append char 255 to string
                            sb.Append(cmd);
                            break;
                        case Command.SubBegin:
                        {
                            // reply to all commands with "WONT", unless it is SGA (suppress go ahead)
                            var option = (OptionCode)stream.ReadByte();
                            if (OutputDebug is not null) OutputDebug.WriteLine($"[{DateTime.Now:O}] RECV IAC {cmd} {option}");
                            if (option == OptionCode.EndOfStream) break;
                            switch (option)
                            {
                                case OptionCode.TerminalType:
                                    //Handle: RCVD IAC SB TERMINAL-TYPE SEND(1) IAC SE
                                    var send = stream.ReadByte();
                                    if (send != 1)
                                    {
                                        throw new InvalidOperationException($"Expecting 1 for SEND instead of {send}");
                                    }

                                    // Parse IAC SE
                                    ExpectSubEnd();

                                    //SENT IAC SB TERMINAL-TYPE IS(0) "xterm-256color" IAC SE
                                    SendSubCommand(OptionCode.TerminalType, 0, "xterm-256color");
                                    break;
                                default:
                                    throw new NotSupportedException($"The telnet optionCode 0x{(byte)option:x2}");
                            }

                            break;
                        }
                        case Command.Dont:
                        case Command.Wont:
                        {
                            // Skip them to avoid loops
                            var optNotCode = (OptionCode)stream.ReadByte();
                            if (OutputDebug is not null) OutputDebug.WriteLine($"[{DateTime.Now:O}] RECV IAC {cmd} {optNotCode}");
                            break;
                        }
                        case Command.Do:
                        case Command.Will:
                        {
                            // reply to all commands with "WONT", unless it is SGA (suppress go ahead)
                            var option = (OptionCode)stream.ReadByte();
                            if (OutputDebug is not null) OutputDebug.WriteLine($"[{DateTime.Now:O}] RECV IAC {cmd} {option}");

                            if (option == OptionCode.EndOfStream) break;

                            if (option == OptionCode.SGA)
                            {
                                Debug.Assert(cmd == Command.Will);
                            }
                            else if (option == OptionCode.TerminalType)
                            {
                                //RCVD DO TERMINAL TYPE
                                // Don't do anything, will be handled by SB above
                                if (cmd == Command.Do)
                                {
                                    // SENT WILL TERMINAL TYPE
                                    SendCommand(Command.Will, OptionCode.TerminalType);
                                }
                            }
                            else
                            {
                                SendCommand(cmd == Command.Do ? Command.Wont : Command.Dont, option);
                            }

                            break;
                        }

                        case Command.InterruptProcess:
                            // Exit
                            return false;

                        case Command.GoAhead:
                            return true;

                        case Command.SubEnd:
                        case Command.Nop:
                        case Command.DataMark:
                        case Command.Break:
                        case Command.AbortOutput:
                        case Command.AreYouThere:
                        case Command.EraseCharacter:
                        case Command.EraseLine:
                            if (OutputDebug is not null) OutputDebug.WriteLine($"[{DateTime.Now:O}] RECV IAC {cmd}");
                            break;
                        default:
                            if (OutputDebug is not null) OutputDebug.WriteLine($"[{DateTime.Now:O}] RECV IAC 0x{(byte)cmd:x2}");
                            break;
                        // Do nothing
                    }

                    break;
                default:
                    sb.Append((char)input);
                    break;
            }
        }

        return true;
    }

    // TODO: add async/await to these methods

    private void ExpectSubEnd()
    {
        Debug.Assert(_tcpSocket != null);
        var stream = _tcpSocket.GetStream();
        var iac = (Command)stream.ReadByte();
        var se = (Command)stream.ReadByte();
        if (iac != Command.IAC && se != Command.SubEnd)
            throw new InvalidOperationException($"Expecting IAC SE command. Got {iac} {se}");
    }

    private void SendCommand(Command command, OptionCode? optionCode = null)
    {
        if (OutputDebug is not null) OutputDebug.WriteLine($"[{DateTime.Now:O}] SEND {command} {optionCode}");
        Debug.Assert(_tcpSocket != null);
        var stream = _tcpSocket.GetStream();
        stream.WriteByte((byte)Command.IAC);
        stream.WriteByte((byte)command);
        if (optionCode.HasValue)
        {
            stream.WriteByte((byte)optionCode);
        }
        //stream.Flush();
    }

    private void SendSubCommand(OptionCode optionCode, byte subCommand, string? data = null)
    {
        if (OutputDebug is not null) OutputDebug.WriteLine($"[{DateTime.Now:O}] SEND IAC SubBegin {optionCode} {subCommand} {data}; SEND IAC SubEnd");
        Debug.Assert(_tcpSocket != null);
        var stream = _tcpSocket.GetStream();
        stream.WriteByte((byte)Command.IAC);
        stream.WriteByte((byte)Command.SubBegin);
        stream.WriteByte((byte)optionCode);
        stream.WriteByte(subCommand);
        if (data != null)
        {
            stream.Write(Encoding.ASCII.GetBytes(data));
        }

        stream.WriteByte((byte)Command.IAC);
        stream.WriteByte((byte)Command.SubEnd);
    }

    // https://datatracker.ietf.org/doc/html/rfc854
    private enum Command
    {
        EndOfStream = -1,

        /// <summary>
        /// End of sub negotiation parameters
        /// </summary>
        SubEnd = 240,

        /// <summary>
        /// No operation.
        /// </summary>
        Nop = 241,

        /// <summary>
        /// The data stream portion of a Synch.
        /// This should always be accompanied
        /// by a TCP Urgent notification.
        /// </summary>
        DataMark = 242,

        /// <summary>
        /// NVT character BRK
        /// </summary>
        Break = 243,

        /// <summary>
        /// The function IP.
        /// </summary>
        InterruptProcess = 244,

        /// <summary>
        /// The function AO.
        /// </summary>
        AbortOutput = 245,

        /// <summary>
        /// The function AYT. 
        /// </summary>
        AreYouThere = 246,

        /// <summary>
        /// The function EC.
        /// </summary>
        EraseCharacter = 247,

        /// <summary>
        /// The function EL.
        /// </summary>
        EraseLine = 248,

        /// <summary>
        /// The GA signal.
        /// </summary>
        GoAhead = 249,

        /// <summary>
        /// Indicates that what follows is
        /// sub negotiation of the indicated
        /// option.
        /// </summary>
        SubBegin = 250,

        /// <summary>
        /// Indicates the desire to begin
        /// performing, or confirmation that
        /// you are now performing, the
        /// indicated option.
        /// </summary>
        Will = 251,

        /// <summary>
        /// Indicates the refusal to perform,
        /// or continue performing, the
        /// indicated option.
        /// </summary>
        Wont = 252,

        /// <summary>
        /// Indicates the request that the
        /// other party perform, or
        /// confirmation that you are expecting
        /// the other party to perform, the
        /// indicated option.
        /// </summary>
        Do = 253,

        /// <summary>
        /// Indicates the demand that the
        /// other party stop performing,
        /// or confirmation that you are no
        /// longer expecting the other party
        /// to perform, the indicated option.
        /// </summary>
        Dont = 254,

        /// <summary>
        /// Interpret as Command. Data Byte 255.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        IAC = 255,
    }

    private enum OptionCode
    {
        EndOfStream = -1,
        Echo = 1,
        SuppressGoAhead = 3,

        // ReSharper disable once InconsistentNaming
        SGA = 3,
        ControlFlow = 33, // https://www.rfc-editor.org/rfc/rfc1372.html
        LineMode = 34,
        TerminalType = 24,
    }
}