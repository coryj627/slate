// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SlateWindows;

/// <summary>
/// Per-user, per-session single-instance ownership and bounded activation IPC.
/// The callback runs on a worker thread; App owns dispatcher marshalling.
/// </summary>
internal sealed class SingleInstanceCoordinator : IDisposable
{
    internal const int MaxMessageBytes = 1 << 16;
    internal const int MaxArguments = 32;
    internal static readonly TimeSpan ConnectionReadTimeout = TimeSpan.FromSeconds(2);

    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenerTask;
    private bool _disposed;

    internal SingleInstanceCoordinator(string identity, int? sessionId = null)
    {
        string suffix = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
        int effectiveSessionId = sessionId ?? Process.GetCurrentProcess().SessionId;
        string sessionSuffix = $"{suffix}-S{effectiveSessionId}";
        _pipeName = $"Slate-{sessionSuffix}";
        _mutex = new Mutex(
            initiallyOwned: true,
            name: $@"Local\Slate-{sessionSuffix}",
            createdNew: out bool createdNew);
        IsPrimary = createdNew;
    }

    public bool IsPrimary { get; }

    internal string PipeNameForTesting => _pipeName;

    public static SingleInstanceCoordinator CreateForCurrentUser() => new(
        $@"{Environment.UserDomainName}\{Environment.UserName}");

    public void StartListening(Action<string[]> onActivation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPrimary)
        {
            throw new InvalidOperationException("Only the primary instance can listen.");
        }

        if (_listenerTask is not null)
        {
            throw new InvalidOperationException("The activation listener is already running.");
        }

        _listenerTask = Task.Run(
            () => ListenLoopAsync(onActivation, _cancellation.Token),
            _cancellation.Token);
    }

    public bool SendActivation(IEnumerable<string> arguments, TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsPrimary)
        {
            throw new InvalidOperationException("The primary instance cannot activate itself.");
        }

        string[] boundedArguments = arguments.Take(MaxArguments).ToArray();
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(boundedArguments);
        if (payload.Length > MaxMessageBytes)
        {
            return false;
        }

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            int remainingMilliseconds = Math.Max(
                1,
                (int)Math.Min(250, (timeout - stopwatch.Elapsed).TotalMilliseconds));
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);
                pipe.Connect(remainingMilliseconds);

                Span<byte> length = stackalloc byte[sizeof(int)];
                BinaryPrimitives.WriteInt32LittleEndian(length, payload.Length);
                pipe.Write(length);
                pipe.Write(payload);
                pipe.Flush();
                return true;
            }
            catch (TimeoutException)
            {
            }
            catch (IOException)
            {
            }

            Thread.Sleep(25);
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        if (_listenerTask is not null)
        {
            try
            {
                _listenerTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException exception) when (
                exception.InnerExceptions.All(inner => inner is OperationCanceledException))
            {
            }
        }

        _cancellation.Dispose();
        if (IsPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _mutex.Dispose();
    }

    private async Task ListenLoopAsync(
        Action<string[]> onActivation,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken);

                using var readDeadline = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                readDeadline.CancelAfter(ConnectionReadTimeout);
                CancellationToken readToken = readDeadline.Token;

                byte[] lengthBytes = new byte[sizeof(int)];
                if (!await ReadExactAsync(pipe, lengthBytes, readToken))
                {
                    continue;
                }

                int length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
                if (length is < 0 or > MaxMessageBytes)
                {
                    continue;
                }

                byte[] payload = new byte[length];
                if (!await ReadExactAsync(pipe, payload, readToken))
                {
                    continue;
                }

                string[]? arguments = JsonSerializer.Deserialize<string[]>(payload);
                if (arguments is not null && arguments.Length <= MaxArguments)
                {
                    onActivation(arguments);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                HostLog.Write(HostDiagnosticEvent.SingleInstanceActivationTimedOut);
            }
            catch (Exception exception) when (
                exception is IOException or JsonException or UnauthorizedAccessException)
            {
                HostLog.Write(HostDiagnosticEvent.SingleInstanceActivationFailed, exception);
            }
        }
    }

    private static async Task<bool> ReadExactAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int count = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (count == 0)
            {
                return false;
            }

            offset += count;
        }

        return true;
    }
}

internal static class ActivationArguments
{
    public static string? OptionValue(IEnumerable<string> arguments, string option)
    {
        using IEnumerator<string> enumerator = arguments.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (string.Equals(enumerator.Current, option, StringComparison.Ordinal)
                && enumerator.MoveNext())
            {
                return enumerator.Current;
            }
        }

        return null;
    }

    public static string? FindVaultPath(IEnumerable<string> arguments)
    {
        bool optionsEnded = false;
        foreach (string argument in arguments)
        {
            if (!optionsEnded && argument == "--")
            {
                optionsEnded = true;
                continue;
            }

            if (!optionsEnded && argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(argument))
            {
                return argument;
            }
        }

        return null;
    }

    public static string QuoteForWindowsCommandLine(string argument)
    {
        if (argument.Length > 0
            && !argument.Any(character => char.IsWhiteSpace(character) || character == '"'))
        {
            return argument;
        }

        var quoted = new StringBuilder(argument.Length + 2);
        quoted.Append('"');
        int backslashes = 0;
        foreach (char character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                quoted.Append('\\', (backslashes * 2) + 1);
                quoted.Append('"');
                backslashes = 0;
                continue;
            }

            quoted.Append('\\', backslashes);
            quoted.Append(character);
            backslashes = 0;
        }

        quoted.Append('\\', backslashes * 2);
        quoted.Append('"');
        return quoted.ToString();
    }
}
