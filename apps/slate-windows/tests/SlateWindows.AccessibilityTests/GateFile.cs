// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text;

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// How the shell gate reads a file the app writes (plan 47 CB-7, #1788), and
/// a fixture that travels with its own binaries. The unit project's
/// <c>TestWaitCensus</c> rule 4 refuses any other read in this project, with
/// no exemption.
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
/// What a read can do is hold the file briefly and seldom, and the reader
/// keeps both itself. Briefly: every read here opens, reads and closes, and
/// no stream leaves this class. Seldom: a read of a path starts at least
/// <see cref="PollInterval"/> after the last read of that path here closed
/// (<see cref="ReadPace"/>). A caller that comes sooner waits out the rest
/// before it opens. So no caller can camp on a file, whatever its shape: a
/// lambda a wait spins on, a delegate, a helper three calls deep, a loop
/// made with <c>goto</c>, a timer. Rule 4's clauses about spinning waits and
/// loops are a second line behind this, not the guarantee. Where the writer
/// does not try again, a journey still waits first for the app state that
/// follows the save and only then reads.
/// </para>
/// <para>
/// A read shares reading, writing and deleting. That does not help a rename.
/// It does let the app delete the file and write it in place while the read
/// is open, and it lets the read open a log the app holds for writing.
/// </para>
/// </remarks>
internal static class GateFile
{
    /// <summary>The least time between two reads of one path. A read of one
    /// of these files takes well under a millisecond, so at 100 ms the
    /// reader has a handle open for less than a hundredth of any wait and a
    /// save finds the file free almost every time it looks; the writers
    /// that try again first do so after 5 ms (core) and 25 ms (the graph
    /// config), inside the gap. A journey that waits on a save notices it at
    /// most this much later.</summary>
    internal static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>How long <see cref="ReadText(string)"/> keeps trying a file
    /// another handle refuses it.</summary>
    internal static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private static readonly Stopwatch Running = Stopwatch.StartNew();

    /// <summary>The pace every read in this process keeps.</summary>
    private static readonly ReadPace Shared = new(() => Running.Elapsed, pause: null, opened: null);

    /// <summary>The reader's pace: for each path, when the last read of it
    /// here closed, and so when the next may open. <see cref="Take"/> waits
    /// a caller's turn and <see cref="Release"/> ends it. Paths are compared
    /// in full and without case, as Windows compares them. The clock and the
    /// pause are seams for <c>GateFileTests</c>, which feed both; a journey
    /// never makes one (rule 4 refuses it), so every journey's read keeps
    /// the one shared pace.</summary>
    internal sealed class ReadPace
    {
        /// <summary>A turn in progress: nothing is this late.</summary>
        private static readonly TimeSpan Held = TimeSpan.MaxValue;

        private readonly Func<TimeSpan> _now;
        private readonly Action<TimeSpan>? _pause;
        private readonly Action<string>? _opened;
        private readonly Dictionary<string, TimeSpan> _freeFrom = new(StringComparer.OrdinalIgnoreCase);
        private readonly Lock _turns = new();

        /// <param name="now">The clock: time since any fixed start.</param>
        /// <param name="pause">What stands in for the pause; null sleeps.</param>
        /// <param name="opened">Told the path each time a turn is given.</param>
        internal ReadPace(Func<TimeSpan> now, Action<TimeSpan>? pause, Action<string>? opened)
        {
            _now = now;
            _pause = pause;
            _opened = opened;
        }

        /// <summary>Waits until <paramref name="path"/> may be opened and
        /// takes the turn: the answer is the key to give
        /// <see cref="Release"/>. The wait is a loop on the path's own
        /// record, pausing for what is left of the interval and looking
        /// again; a path nobody has read yet does not wait.</summary>
        internal string Take(string path)
        {
            string key = KeyOf(path);
            while (true)
            {
                TimeSpan wait;
                lock (_turns)
                {
                    TimeSpan now = _now();
                    if (!_freeFrom.TryGetValue(key, out TimeSpan freeFrom) || freeFrom <= now)
                    {
                        _freeFrom[key] = Held;
                        break;
                    }

                    // Held by another read on another thread: look again
                    // in an interval. Otherwise wait out what is left.
                    wait = freeFrom == Held ? PollInterval : freeFrom - now;
                }

                if (_pause is { } standIn)
                {
                    standIn(wait);
                }
                else
                {
                    Thread.Sleep(wait);
                }
            }

            _opened?.Invoke(key);
            return key;
        }

        /// <summary>Ends a turn: the path is free an interval from now.</summary>
        internal void Release(string key)
        {
            lock (_turns)
            {
                _freeFrom[key] = _now() + PollInterval;
            }
        }

        private static string KeyOf(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
            {
                // No full path can be made of it: the open will say why.
                return path;
            }
        }
    }

    /// <summary>One open, read and close of a file, at its turn. Every read
    /// in this class comes through here, so every read keeps the pace.</summary>
    private static byte[] ReadOnce(string path, ReadPace pace)
    {
        string key = pace.Take(path);
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }
        finally
        {
            pace.Release(key);
        }
    }

    /// <summary>The file's bytes, or null when it is absent or the open or
    /// the read was refused.</summary>
    internal static byte[]? TryReadBytes(string path, ReadPace? pace = null)
    {
        try
        {
            return ReadOnce(path, pace ?? Shared);
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
    internal static string? TryReadText(string path, ReadPace? pace = null) =>
        TryReadBytes(path, pace) is { } bytes ? Decode(bytes) : null;

    /// <summary>One read of a file that should be there: its text. A refused
    /// open or read is tried again, at the reader's pace, for
    /// <see cref="Patience"/>; an absent file, or one still refused after
    /// that, throws naming the path.</summary>
    internal static string ReadText(string path) => ReadText(path, Patience, refused: null);

    /// <summary><see cref="ReadText(string)"/> with its patience given, and
    /// a hook that runs after each refused attempt with the attempts so
    /// far.</summary>
    internal static string ReadText(string path, TimeSpan patience, Action<int>? refused, ReadPace? pace = null) =>
        Decode(ReadBytes(path, patience, refused, pace ?? Shared));

    private static byte[] ReadBytes(string path, TimeSpan patience, Action<int>? refused, ReadPace pace)
    {
        var clock = Stopwatch.StartNew();
        int attempts = 0;
        while (true)
        {
            try
            {
                return ReadOnce(path, pace);
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
            }
        }
    }

    /// <summary>Polls the file until its text satisfies
    /// <paramref name="ready"/> and returns that text, or null when
    /// <paramref name="timeout"/> passes first. Each read opens, reads and
    /// closes, and the reader's pace puts <see cref="PollInterval"/> between
    /// two of them; an absent or refused file is one more read that was not
    /// ready. <paramref name="betweenReads"/> runs after a read that was not
    /// ready, while the poll holds no handle.</summary>
    internal static string? PollText(
        string path, Func<string, bool> ready, TimeSpan timeout, Action? betweenReads = null, ReadPace? pace = null)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            if (TryReadText(path, pace) is { } text && ready(text))
            {
                return text;
            }

            if (clock.Elapsed >= timeout)
            {
                return null;
            }

            betweenReads?.Invoke();
        }
    }

    /// <summary>Holds a file open, for the one kind of fact whose subject
    /// is the hold: a lock file the product must wait on. It is not a read
    /// and keeps no pace. The caller holds the file for as long as it keeps
    /// the stream, and a rename over the file is refused for that long,
    /// which for a file the app replaces would be the defect this class is
    /// here to prevent. So rule 4 admits a call to this by the name of the
    /// member that makes it, and refuses it anywhere else.</summary>
    internal static FileStream HoldForTheFact(string path, FileAccess access) =>
        new(path, FileMode.Open, access, FileShare.ReadWrite | FileShare.Delete);

    /// <summary>The text of a fixture that travels with the test binaries:
    /// the file at <paramref name="relativeParts"/> under
    /// <c>AppContext.BaseDirectory</c>, read once. This is the gate's one
    /// way to read a fixture. Rule 4 used to let a read through when its
    /// path was written as the base directory combined with constants, and
    /// what such a path really is cannot be told from how the call is
    /// written: <c>Path.Combine</c> drops the base for a rooted part, and
    /// <c>..</c> parts climb out of it. So the path is built here, and
    /// checked when it is built. A part that is empty or rooted throws,
    /// naming the part. A path that ends outside the base directory, or at
    /// it, once its <c>..</c> parts are resolved throws, naming where it
    /// ends. A <c>..</c> that stays under the base is let through: where the
    /// path ends is what is checked.</summary>
    internal static string ReadFixture(params string[] relativeParts) => ReadText(FixturePath(relativeParts));

    /// <summary>Copies a fixture (<see cref="ReadFixture"/>'s path and
    /// checks) to <paramref name="destination"/>, byte for byte: how a
    /// journey puts a fixture into the vault it is about to open.
    /// <c>File.Copy</c> is not admitted by rule 4, because a copy reads its
    /// source and the rule cannot see what the source is.</summary>
    internal static void CopyFixture(string destination, params string[] relativeParts) =>
        File.WriteAllBytes(destination, ReadBytes(FixturePath(relativeParts), Patience, refused: null, Shared));

    private static string FixturePath(string[] relativeParts)
    {
        ArgumentNullException.ThrowIfNull(relativeParts);
        if (relativeParts.Length == 0)
        {
            throw new ArgumentException("A fixture needs at least one part under the base directory.", nameof(relativeParts));
        }

        foreach (string part in relativeParts)
        {
            if (string.IsNullOrEmpty(part))
            {
                throw new ArgumentException("A fixture's part is empty.", nameof(relativeParts));
            }

            if (Path.IsPathRooted(part))
            {
                throw new ArgumentException(
                    $"A fixture's part must be relative, and \"{part}\" is rooted: Path.Combine would drop the base directory for it.",
                    nameof(relativeParts));
            }
        }

        string root = Path.GetFullPath(AppContext.BaseDirectory);
        if (!Path.EndsInDirectorySeparator(root))
        {
            root += Path.DirectorySeparatorChar;
        }

        string full = Path.GetFullPath(Path.Combine([root, .. relativeParts]));
        if (full.Length <= root.Length || !full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"A fixture lives under the base directory, and \"{string.Join(Path.DirectorySeparatorChar, relativeParts)}\" ends at "
                + $"{full}, which is not under {root}",
                nameof(relativeParts));
        }

        return full;
    }

    private static string Decode(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
