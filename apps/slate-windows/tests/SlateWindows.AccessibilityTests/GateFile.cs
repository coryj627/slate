// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text;

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// How the shell gate reads a file the app writes (plan 47 CB-7, #1788). The
/// unit project's <c>TestWaitCensus</c> rule 4 refuses any other read in
/// this project.
/// </summary>
/// <remarks>
/// <para>
/// The app saves by renaming a staged file over the old one, with Windows'
/// classic replacing rename. Windows refuses that rename with access denied
/// while any handle is open on the target, whatever the handle shares. So no
/// way of opening a file makes a read harmless to a save: a gate read that
/// is open at the instant of the rename refuses it. Core's note save and the
/// graph config's writer wait and try again; the device stores
/// (<c>preferences.json</c>, <c>recent-vaults.json</c>) try once, and a
/// refused save there is lost. The gate lost a save this way for the product
/// it was measuring, from a read that spun with no pause between opens.
/// </para>
/// <para>
/// What a read can do is hold the file briefly and seldom. Every read here
/// opens, reads and closes. A poll (<see cref="PollText"/>) pauses for
/// <see cref="PollInterval"/> between two reads, so the file is open for a
/// small part of the wait. Where the writer does not try again, a journey
/// first waits for the app state that follows the save and only then reads.
/// </para>
/// <para>
/// A read shares reading, writing and deleting. That does not help a rename.
/// It does let the app delete the file and write it in place while the read
/// is open, and it lets the read open a log the app holds for writing.
/// </para>
/// </remarks>
internal static class GateFile
{
    /// <summary>The pause between two reads of a poll. A read of one of
    /// these files takes well under a millisecond, so at 100 ms a poll has a
    /// handle open for less than a hundredth of its wait and a save finds
    /// the file free almost every time it looks; the writers that try again
    /// first do so after 5 ms (core) and 25 ms (the graph config), inside
    /// the pause. A journey that waits on a save notices it at most this
    /// much later.</summary>
    internal static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>How long <see cref="ReadText(string)"/> keeps trying a file
    /// another handle refuses it.</summary>
    internal static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>The file, open for reading and sharing reading, writing and
    /// deleting. The caller holds it for as long as it keeps the stream: a
    /// rename over the file is refused for that long.</summary>
    internal static FileStream Open(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    /// <summary>The file's bytes, or null when it is absent or the open or
    /// the read was refused.</summary>
    internal static byte[]? TryReadBytes(string path)
    {
        try
        {
            using FileStream stream = Open(path);
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The file's text as <see cref="File.ReadAllText(string)"/>
    /// decodes it (UTF-8 unless a byte-order mark says otherwise), or null
    /// when it is absent or the open or the read was refused.</summary>
    internal static string? TryReadText(string path) =>
        TryReadBytes(path) is { } bytes ? Decode(bytes) : null;

    /// <summary>One read of a file that should be there: its text. A refused
    /// open or read is tried again every <see cref="PollInterval"/> for
    /// <see cref="Patience"/>; an absent file, or one still refused after
    /// that, throws naming the path.</summary>
    internal static string ReadText(string path) => ReadText(path, Patience, refused: null);

    /// <summary><see cref="ReadText(string)"/> with its patience given, and
    /// a hook that runs after each refused attempt with the attempts so
    /// far.</summary>
    internal static string ReadText(string path, TimeSpan patience, Action<int>? refused)
    {
        var clock = Stopwatch.StartNew();
        int attempts = 0;
        while (true)
        {
            try
            {
                using FileStream stream = Open(path);
                using var bytes = new MemoryStream();
                stream.CopyTo(bytes);
                return Decode(bytes.ToArray());
            }
            catch (Exception exception) when (
                exception is FileNotFoundException or DirectoryNotFoundException)
            {
                throw new IOException($"The gate expected to read {path}, and it is not there.", exception);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                attempts++;
                refused?.Invoke(attempts);
                if (clock.Elapsed >= patience)
                {
                    throw new IOException(
                        $"The gate could not read {path}: {attempts} attempts in {patience.TotalSeconds:0.#} s were refused, "
                        + $"the last with {exception.GetType().Name}: {exception.Message}",
                        exception);
                }

                Thread.Sleep(PollInterval);
            }
        }
    }

    /// <summary>Polls the file until its text satisfies
    /// <paramref name="ready"/> and returns that text, or null when
    /// <paramref name="timeout"/> passes first. Each read opens, reads and
    /// closes, and the poll pauses <see cref="PollInterval"/> between two
    /// reads; an absent or refused file is one more read that was not
    /// ready. <paramref name="betweenReads"/> runs after a read that was not
    /// ready, while the poll holds no handle.</summary>
    internal static string? PollText(string path, Func<string, bool> ready, TimeSpan timeout, Action? betweenReads = null)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            if (TryReadText(path) is { } text && ready(text))
            {
                return text;
            }

            if (clock.Elapsed >= timeout)
            {
                return null;
            }

            betweenReads?.Invoke();
            Thread.Sleep(PollInterval);
        }
    }

    private static string Decode(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
