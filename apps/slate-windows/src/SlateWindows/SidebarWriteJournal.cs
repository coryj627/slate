// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Win32.SafeHandles;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlateWindows;

/// <summary>One stored-path rewrite a rename or move implies: the entry at
/// <see cref="OldPath"/> is now at <see cref="NewPath"/>, and a folder takes
/// everything beneath it along.</summary>
internal sealed record SidebarJournalRename(string OldPath, string NewPath, bool IsDirectory);

/// <summary>One live entry of the sidebar write journal: a rename's or a
/// move's renames, <see cref="Committed"/> once core reported what
/// committed.</summary>
internal sealed record SidebarJournalEntry(
    Guid Id,
    long Position,
    DateTimeOffset RecordedAt,
    bool Committed,
    IReadOnlyList<SidebarJournalRename> Renames);

/// <summary>Something the next open of the vault must say.</summary>
internal enum SidebarJournalNotice
{
    /// <summary>Lines that failed their checksum or were torn by a crash, or
    /// a file this build cannot read (a damaged header, a newer format).</summary>
    Unreadable,

    /// <summary>The file belongs to a vault that is gone: same volume and
    /// file id, another birth time.</summary>
    ReplacedVault,
}

/// <summary>What an open takes from the journal: the entries to replay in
/// order, the ones too old to replay (already settled, to be said), and the
/// notices.</summary>
internal sealed record SidebarJournalAdoption(
    IReadOnlyList<SidebarJournalEntry> Entries,
    IReadOnlyList<SidebarJournalEntry> AgedOut,
    IReadOnlyList<SidebarJournalNotice> Notices);

/// <summary>A pending entry: its id, and a task that finishes once its line
/// is written and flushed to disk — false when it could not be.</summary>
internal sealed record SidebarJournalAppend(Guid Id, Task<bool> Durable);

/// <summary>
/// #1435: the vault root's physical identity on Windows, as the journal keys
/// it — the volume serial number and the 128-bit file id of
/// <c>FILE_ID_INFO</c>, the identity <see cref="ExternalLinkPolicy"/> trusts
/// (ReFS-safe; the 64-bit <c>nFileIndex</c> is not unique there). Core's
/// <c>VaultSession.RootIdentity()</c> observes nothing on Windows (its
/// <c>observe_root_identity</c> leaves the observation to this host), so the
/// journal reads it here. A replacement vault at the same path is another
/// directory with another file id, so it never finds the file.
/// </summary>
internal sealed record SidebarJournalIdentity(ulong Device, UInt128 Inode)
{
    internal string DeviceText => Device.ToString(CultureInfo.InvariantCulture);

    internal string InodeText => Inode.ToString(CultureInfo.InvariantCulture);

    /// <summary><c>&lt;device&gt;-&lt;inode&gt;.journal</c>, mac's name.</summary>
    internal string FileName => $"{DeviceText}-{InodeText}.journal";

    /// <summary>The identity of the directory at <paramref name="vaultRoot"/>,
    /// or null when this volume cannot say (a filesystem that does not answer
    /// <c>FileIdInfo</c>, such as FAT32): then nothing is journaled.</summary>
    internal static SidebarJournalIdentity? Observe(string vaultRoot) =>
        Fingerprint(vaultRoot).Identity;

    /// <summary>The identity and the root's birth time (its creation time, in
    /// nanoseconds since the Unix epoch, as mac records it), read from one
    /// handle.</summary>
    internal static (SidebarJournalIdentity? Identity, long? Birth) Fingerprint(string vaultRoot)
    {
        try
        {
            using SafeFileHandle handle = NativeIdentity.CreateFileW(
                WindowsNativePath.ForCreateFile(Path.GetFullPath(vaultRoot)),
                NativeIdentity.FileReadAttributes,
                NativeIdentity.FileShareAll,
                IntPtr.Zero,
                NativeIdentity.OpenExisting,
                NativeIdentity.FileFlagBackupSemantics,
                IntPtr.Zero);
            if (handle.IsInvalid
                || !NativeIdentity.GetFileInformationByHandleEx(
                    handle,
                    NativeIdentity.FileIdInfoClass,
                    out NativeIdentity.FileIdInformation id,
                    (uint)Marshal.SizeOf<NativeIdentity.FileIdInformation>()))
            {
                return (null, null);
            }

            var identity = new SidebarJournalIdentity(
                id.VolumeSerialNumber,
                new UInt128(id.FileIdHigh, id.FileIdLow));
            long? birth = NativeIdentity.GetFileInformationByHandleEx(
                handle,
                NativeIdentity.FileBasicInfoClass,
                out NativeIdentity.FileBasicInformation basic,
                (uint)Marshal.SizeOf<NativeIdentity.FileBasicInformation>())
                ? (basic.CreationTime - UnixEpochFileTime) * 100
                : null;
            return (identity, birth);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException
                or NotSupportedException)
        {
            return (null, null);
        }
    }

    /// <summary>1970-01-01 as a FILETIME (100-ns ticks since 1601).</summary>
    private const long UnixEpochFileTime = 116_444_736_000_000_000;

    private static class NativeIdentity
    {
        internal const uint FileReadAttributes = 0x00000080;
        internal const uint FileShareAll = 0x00000007;
        internal const uint OpenExisting = 3;

        /// <summary>Lets a directory handle be opened. Reparse points are
        /// followed: a vault opened through a link is the directory it names,
        /// as mac resolves the root once.</summary>
        internal const uint FileFlagBackupSemantics = 0x02000000;

        /// <summary>FILE_INFO_BY_HANDLE_CLASS.FileBasicInfo.</summary>
        internal const int FileBasicInfoClass = 0;

        /// <summary>FILE_INFO_BY_HANDLE_CLASS.FileIdInfo.</summary>
        internal const int FileIdInfoClass = 18;

        /// <summary>FILE_ID_INFO: 64-bit volume serial + 128-bit file id.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct FileIdInformation
        {
            public ulong VolumeSerialNumber;
            public ulong FileIdLow;
            public ulong FileIdHigh;
        }

        /// <summary>FILE_BASIC_INFO: four FILETIMEs and the attributes.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct FileBasicInformation
        {
            public long CreationTime;
            public long LastAccessTime;
            public long LastWriteTime;
            public long ChangeTime;
            public uint FileAttributes;
        }

        // DllImport rather than LibraryImport: the app project compiles
        // without AllowUnsafeBlocks (HostLog's rule).
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFileW(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandleEx(
            SafeFileHandle file,
            int informationClass,
            out FileIdInformation information,
            uint bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandleEx(
            SafeFileHandle file,
            int informationClass,
            out FileBasicInformation information,
            uint bufferSize);
    }
}

/// <summary>
/// #1435 (the Windows twin of mac's #944, plan 43 PR 23 — its structural
/// half): the sidebar's write-ahead journal. A rename or move commits through
/// core, and only then does the sidebar rewrite its pins and shortcuts in
/// <c>.slate/sidebar.json</c>; a crash between the two, or a pins or
/// shortcuts write that fails, left the file naming the old paths. So every
/// rename or move records its anticipated renames here, flushed to disk
/// before core is called, records what committed when core reports, and is
/// settled once the pins and shortcuts writes that carry it succeed. Until
/// then a quit or a crash leaves it on disk, and the next open of the same
/// vault replays it before the settings file is adopted.
/// </summary>
/// <remarks>
/// <para>
/// Windows writes <c>.slate/sidebar.json</c> synchronously and speaks an
/// organization command only after its write succeeded, so mac's other half
/// — acknowledged intents — has nothing to keep here (#1435).
/// </para>
/// <para>
/// Location and identity. One append-only file per vault identity
/// (<see cref="SidebarJournalIdentity"/>) under
/// <c>%LOCALAPPDATA%\Slate\sidebar-write-journal</c> — never inside the
/// vault, whose filesystem is what failed. Only the app installs that
/// location (<see cref="InstallProductionLocation"/>); every other process,
/// a test host included, journals into a private temporary folder, so no
/// test touches the user's journal. The header also records the root's birth
/// time: a journal whose header names this vault's identity and another birth
/// time belongs to a vault that is gone, and is said and removed. The volume
/// serial travels with its volume, so a drive that comes back under another
/// letter keeps its key; mac's volume-UUID relocation has nothing to do here.
/// </para>
/// <para>
/// Record format, version 1, as on mac. Every line is
/// <c>&lt;crc32&gt; &lt;json&gt;\n</c> — eight lowercase hex digits of the
/// CRC-32 (IEEE) of the JSON bytes, a space, one JSON object (JSON never holds
/// a raw newline). The first line is the header; later lines are
/// <c>put</c> (an entry, or its committed form, which takes its place in the
/// replay order) and <c>settle</c> (tombstones). A line that fails its
/// checksum, or the unterminated last line a crash leaves, is dropped and
/// counted — every other line still replays, the file is rewritten without
/// the damage before the next append, and the drop is said. A newer version
/// is never replayed and never touched.
/// </para>
/// <para>
/// Durability and threading. Every file operation runs in order on one lane
/// off the UI thread (the command palette's lane shape). A pending entry's
/// line is flushed to disk there, and the caller waits for it, bounded,
/// before it lets core run; committed lines and settles are written without
/// a flush — losing one replays an entry once more, and a replay is
/// idempotent. A file whose entries are all settled is removed; settled
/// lines are reclaimed once they outweigh 256 KiB. Entries are never evicted:
/// one older than 30 days when an open would replay it is said instead.
/// </para>
/// </remarks>
internal sealed class SidebarWriteJournal
{
    internal const string FormatName = "slate-sidebar-write-journal";
    internal const int FormatVersion = 1;

    /// <summary>The bounded read at open. A journal usually holds a few
    /// entries; a pending batch move of thousands of items is one large
    /// line, and the bound leaves room for several.</summary>
    internal const int MaxReadBytes = 32 * 1024 * 1024;

    /// <summary>Once settled lines outweigh this, the file is rewritten
    /// with its live entries only.</summary>
    internal const int CompactionThresholdBytes = 256 * 1024;

    /// <summary>An entry older than this when an open would replay it is
    /// said, not replayed: the vault has had a month of other changes.</summary>
    internal static readonly TimeSpan MaxEntryAge = TimeSpan.FromDays(30);

    /// <summary>How long a rename waits for its pending line to reach the
    /// disk before it proceeds without it (mac's five-second sidebar
    /// persistence bound). The line still lands in order.</summary>
    internal static readonly TimeSpan DurableWaitBudget = TimeSpan.FromSeconds(5);

    private const string PutOp = "put";
    private const string SettleOp = "settle";
    private const string HeaderOp = "header";

    private static readonly JsonSerializerOptions LineJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Paths stay readable, as mac's lines are: the JSON is never
        // embedded in markup, and control characters are still escaped,
        // so a line never holds a raw newline.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly object _gate = new();
    private readonly Dictionary<SidebarJournalIdentity, VaultLog> _logs = [];
    private Task _tail = Task.CompletedTask;
    private long _nextPosition;

    internal SidebarWriteJournal(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        JournalDirectory = Path.GetFullPath(directory);
    }

    /// <summary>The process's journal. A private temporary folder until the
    /// app installs the production location at launch.</summary>
    internal static SidebarWriteJournal Shared { get; private set; } = new(TemporaryDirectory());

    /// <summary>The device-state root's <c>sidebar-write-journal</c>
    /// (<c>%LOCALAPPDATA%\Slate\sidebar-write-journal</c> unless
    /// <c>SLATE_LOCAL_APP_DATA</c> moves the root).</summary>
    internal static string ProductionDirectory => Path.Combine(
        SlateDeviceState.Root,
        "sidebar-write-journal");

    internal string JournalDirectory { get; }

    /// <summary>Test seam: the clock entries are stamped with and aged
    /// against.</summary>
    internal Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;

    /// <summary>The app's launch-time install of the production location
    /// (App.OnStartup, before any vault opens).</summary>
    internal static void InstallProductionLocation()
    {
        string production = Path.GetFullPath(ProductionDirectory);
        if (!string.Equals(Shared.JournalDirectory, production, StringComparison.OrdinalIgnoreCase))
        {
            Shared = new SidebarWriteJournal(production);
        }
    }

    internal static string TemporaryDirectory() => Path.Combine(
        Path.GetTempPath(),
        $"slate-sidebar-write-journal-{Environment.ProcessId}-{Guid.NewGuid():N}");

    internal string FilePath(SidebarJournalIdentity identity) =>
        Path.Combine(JournalDirectory, identity.FileName);

    /// <summary>The open's adoption: every live entry no rename still in
    /// flight holds, in replay order. Entries too old to replay are settled
    /// here and returned to be said; the notices are handed over once.
    /// The identity's file is read once per process — bounded and
    /// synchronous, the budget the settings store's own load has.</summary>
    internal SidebarJournalAdoption Adopt(SidebarJournalIdentity identity, string vaultRoot)
    {
        lock (_gate)
        {
            VaultLog log = OpenLocked(identity, vaultRoot);
            SidebarJournalNotice[] notices = [.. log.Notices];
            log.Notices.Clear();
            DateTimeOffset now = Now();
            var entries = new List<SidebarJournalEntry>();
            var agedOut = new List<SidebarJournalEntry>();
            foreach (SidebarJournalEntry entry in log.Entries.Values
                .Where(entry => !log.InFlight.Contains(entry.Id))
                .OrderBy(entry => entry.Position))
            {
                (now - entry.RecordedAt > MaxEntryAge ? agedOut : entries).Add(entry);
            }

            SettleLocked(identity, log, agedOut.Select(entry => entry.Id));
            return new SidebarJournalAdoption(entries, agedOut, notices);
        }
    }

    /// <summary>Journal a rename's or move's anticipated renames before core
    /// can commit them. Null when nothing can be journaled (a newer-format
    /// file this build must not touch, or no renames).</summary>
    internal SidebarJournalAppend? AppendPending(
        SidebarJournalIdentity identity,
        string vaultRoot,
        IReadOnlyList<SidebarJournalRename> renames)
    {
        lock (_gate)
        {
            VaultLog log = OpenLocked(identity, vaultRoot);
            if (log.IsReadOnly || renames.Count == 0)
            {
                return null;
            }

            var entry = new SidebarJournalEntry(
                Guid.NewGuid(),
                TakePosition(),
                Now(),
                Committed: false,
                [.. renames]);
            byte[] line = EncodePut(entry);
            log.Entries[entry.Id] = entry;
            log.InFlight.Add(entry.Id);
            log.EntryBytes[entry.Id] = line.Length;
            Task<bool> durable = WriteLocked(identity, log, line, flushToDisk: true);
            return new SidebarJournalAppend(entry.Id, durable);
        }
    }

    /// <summary>Core reported: record what committed — for a batch, its
    /// standing changes, a subset of what was anticipated — so its replay
    /// needs no verification, or settle the entry when nothing did.</summary>
    internal void ResolveCommitted(
        SidebarJournalIdentity identity,
        Guid id,
        IReadOnlyList<SidebarJournalRename> committed)
    {
        lock (_gate)
        {
            if (!_logs.TryGetValue(identity, out VaultLog? log))
            {
                return;
            }

            log.InFlight.Remove(id);
            if (committed.Count == 0)
            {
                SettleLocked(identity, log, [id]);
                return;
            }

            if (log.IsReadOnly || !log.Entries.TryGetValue(id, out SidebarJournalEntry? entry))
            {
                return;
            }

            SidebarJournalEntry resolved = entry with
            {
                Position = TakePosition(),
                Committed = true,
                Renames = [.. committed],
            };
            log.Entries[id] = resolved;
            byte[] line = EncodePut(resolved);
            _ = WriteLocked(identity, log, line, flushToDisk: false);
            log.EntryBytes[id] = line.Length;
            CompactIfWastefulLocked(identity, log);
        }
    }

    /// <summary>Core ended without saying what committed (a batch threw, or
    /// a structural mutation stopped incomplete): the entry stays as
    /// anticipated, no longer in flight, and the next open replays only the
    /// renames whose source is definitely gone.</summary>
    internal void ReleaseForVerification(SidebarJournalIdentity identity, Guid id)
    {
        lock (_gate)
        {
            if (_logs.TryGetValue(identity, out VaultLog? log))
            {
                log.InFlight.Remove(id);
            }
        }
    }

    /// <summary>Tombstone entries whose renames reached
    /// <c>.slate/sidebar.json</c>, or whose outcome reached the user. Removes
    /// the file once nothing is live.</summary>
    internal void Settle(SidebarJournalIdentity identity, IEnumerable<Guid> ids)
    {
        lock (_gate)
        {
            if (_logs.TryGetValue(identity, out VaultLog? log))
            {
                SettleLocked(identity, log, ids);
            }
        }
    }

    internal int LiveEntryCount(SidebarJournalIdentity identity)
    {
        lock (_gate)
        {
            return _logs.TryGetValue(identity, out VaultLog? log) ? log.Entries.Count : 0;
        }
    }

    /// <summary>Completes when every file operation handed over so far has
    /// finished. Never faults.</summary>
    internal Task WhenIdle()
    {
        lock (_gate)
        {
            return _tail.ContinueWith(
                tail => { _ = tail.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    // MARK: Format

    internal static byte[] EncodeHeader(
        SidebarJournalIdentity identity,
        long? birth,
        int version = FormatVersion)
    {
        var header = new JournalLine
        {
            Birth = birth?.ToString(CultureInfo.InvariantCulture),
            Device = identity.DeviceText,
            Format = FormatName,
            Inode = identity.InodeText,
            Op = HeaderOp,
            Version = version,
        };
        return Encode(header);
    }

    internal static byte[] EncodePut(SidebarJournalEntry entry)
    {
        JournalRename[] renames = [.. entry.Renames.Select(RenameLine)];
        var transform = new JournalTransform
        {
            Id = IdText(entry.Id),
            Renames = renames,
        };
        var put = new JournalLine
        {
            At = entry.RecordedAt.ToUnixTimeMilliseconds(),
            Content = new JournalContent
            {
                Structural = new JournalStructural
                {
                    Transform = transform,
                    Committed = entry.Committed,
                },
            },
            Id = IdText(entry.Id),
            Op = PutOp,
        };
        return Encode(put);
    }

    internal static byte[] EncodeSettle(IEnumerable<Guid> ids)
    {
        var settle = new JournalLine
        {
            Ids = [.. ids.Select(IdText).Order(StringComparer.Ordinal)],
            Op = SettleOp,
        };
        return Encode(settle);
    }

    /// <summary>Split, verify and decode. Any line that fails — the
    /// unterminated last one included — is counted and skipped, so damaged
    /// lines ahead of the header are skipped too; the first line that
    /// verifies must be the header, or the file has none.</summary>
    internal static ParsedJournal Parse(ReadOnlySpan<byte> data)
    {
        JournalLine? header = null;
        var lines = new List<JournalLine>();
        int unreadable = 0;
        bool sawReadableLine = false;
        int start = 0;
        for (int index = 0; index < data.Length; index++)
        {
            if (data[index] != (byte)'\n')
            {
                continue;
            }

            JournalLine? line = ReadLine(data[start..index]);
            start = index + 1;
            if (line is null)
            {
                unreadable++;
            }
            else if (!sawReadableLine)
            {
                sawReadableLine = true;
                if (line.Op == HeaderOp)
                {
                    header = line;
                }
                else
                {
                    unreadable++;
                }
            }
            else
            {
                lines.Add(line);
            }
        }

        if (start < data.Length)
        {
            // The torn tail: a crash mid-append left a line without its newline.
            unreadable++;
        }

        return new ParsedJournal(header, lines, unreadable);
    }

    /// <summary>Test seam: a journal file's live entries straight from disk,
    /// and how many of its lines did not verify. Null without a file or a
    /// header.</summary>
    internal static JournalOnDisk? ReadForTesting(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        ParsedJournal parsed = Parse(
            SafeFile.ReadAllBytesBounded(path, MaxReadBytes, FileShare.ReadWrite | FileShare.Delete));
        if (parsed.Header is null)
        {
            return null;
        }

        var entries = new Dictionary<Guid, SidebarJournalEntry>();
        foreach (JournalLine line in parsed.Lines)
        {
            if (line.Op == PutOp && DecodeEntry(line) is SidebarJournalEntry entry)
            {
                entries[entry.Id] = entry;
            }
            else if (line.Op == SettleOp)
            {
                foreach (string id in line.Ids ?? [])
                {
                    if (Guid.TryParse(id, out Guid settled))
                    {
                        entries.Remove(settled);
                    }
                }
            }
        }

        return new JournalOnDisk(entries, parsed.UnreadableLines);
    }

    // MARK: Internals

    private VaultLog OpenLocked(SidebarJournalIdentity identity, string vaultRoot)
    {
        if (_logs.TryGetValue(identity, out VaultLog? existing))
        {
            return existing;
        }

        var log = new VaultLog();
        _logs[identity] = log;
        long? birth = BirthOf(vaultRoot, identity);
        log.Header = EncodeHeader(identity, birth);
        string path = FilePath(identity);
        byte[] data;
        try
        {
            data = SafeFile.ReadAllBytesBounded(path, MaxReadBytes, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return log;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HostLog.Write(HostDiagnosticEvent.SidebarWriteJournalFailed, exception);
            log.Notices.Add(SidebarJournalNotice.Unreadable);
            Enqueue(() => Quarantine(path));
            return log;
        }

        if (data.Length == 0)
        {
            // A crash between creating the file and its first write leaves it
            // empty; the next append writes the header into it.
            return log;
        }

        ParsedJournal parsed = Parse(data);
        if (parsed.Header is not { Format: FormatName, Version: int version } header)
        {
            log.Notices.Add(SidebarJournalNotice.Unreadable);
            Enqueue(() => Quarantine(path));
            return log;
        }

        if (version > FormatVersion)
        {
            // A newer Slate wrote this; it can still replay it. Never touch it.
            log.IsReadOnly = true;
            log.Notices.Add(SidebarJournalNotice.Unreadable);
            return log;
        }

        if (header.Device != identity.DeviceText || header.Inode != identity.InodeText)
        {
            log.Notices.Add(SidebarJournalNotice.Unreadable);
            Enqueue(() => Quarantine(path));
            return log;
        }

        if (birth is long observed
            && header.Birth is string recorded
            && recorded != observed.ToString(CultureInfo.InvariantCulture))
        {
            // These entries belong to a vault that no longer exists and can
            // never replay.
            log.Notices.Add(SidebarJournalNotice.ReplacedVault);
            Enqueue(() => File.Delete(path));
            return log;
        }

        int unreadable = parsed.UnreadableLines;
        foreach (JournalLine line in parsed.Lines)
        {
            if (line.Op == PutOp && DecodeEntry(line) is SidebarJournalEntry decoded)
            {
                // A committed put takes the place of its pending one and keeps
                // the age of the first record.
                DateTimeOffset recordedAt = log.Entries.TryGetValue(decoded.Id, out SidebarJournalEntry? earlier)
                    ? earlier.RecordedAt
                    : decoded.RecordedAt;
                log.Entries[decoded.Id] = decoded with { Position = TakePosition(), RecordedAt = recordedAt };
            }
            else if (line.Op == SettleOp && line.Ids is string[] ids)
            {
                foreach (string id in ids)
                {
                    if (Guid.TryParse(id, out Guid settled))
                    {
                        log.Entries.Remove(settled);
                    }
                    else
                    {
                        unreadable++;
                    }
                }
            }
            else
            {
                unreadable++;
            }
        }

        if (unreadable > 0)
        {
            log.Notices.Add(SidebarJournalNotice.Unreadable);
        }

        byte[] live = LiveBytesLocked(log);
        if (log.Entries.Count == 0)
        {
            Enqueue(() => File.Delete(path));
        }
        else
        {
            log.ApproximateBytes = live.Length;
            if (unreadable > 0 || live.Length != data.Length)
            {
                // Drop the damage and the settled lines before the next
                // append, so a torn tail never fuses with the line after it.
                Enqueue(() => Rewrite(path, live));
            }
        }

        return log;
    }

    private void SettleLocked(SidebarJournalIdentity identity, VaultLog log, IEnumerable<Guid> ids)
    {
        if (log.IsReadOnly)
        {
            return;
        }

        Guid[] live = [.. ids.Distinct().Where(log.Entries.ContainsKey)];
        if (live.Length == 0)
        {
            return;
        }

        foreach (Guid id in live)
        {
            log.Entries.Remove(id);
            log.EntryBytes.Remove(id);
            log.InFlight.Remove(id);
        }

        string path = FilePath(identity);
        if (log.Entries.Count == 0)
        {
            log.ApproximateBytes = 0;
            Enqueue(() => File.Delete(path));
            return;
        }

        _ = WriteLocked(identity, log, EncodeSettle(live), flushToDisk: false);
        CompactIfWastefulLocked(identity, log);
    }

    /// <summary>Queue one line; the header goes first into an empty file.</summary>
    private Task<bool> WriteLocked(
        SidebarJournalIdentity identity,
        VaultLog log,
        byte[] line,
        bool flushToDisk)
    {
        if (log.ApproximateBytes == 0)
        {
            log.ApproximateBytes = log.Header.Length;
        }

        log.ApproximateBytes += line.Length;
        byte[] header = log.Header;
        string path = FilePath(identity);
        return Submit(() => AppendLine(path, header, line, flushToDisk));
    }

    /// <summary>Rewrite the file live-only once settled lines outweigh the
    /// threshold — a large live entry (a big pending batch) never forces a
    /// rewrite alone.</summary>
    private void CompactIfWastefulLocked(SidebarJournalIdentity identity, VaultLog log)
    {
        if (log.DeadBytes <= CompactionThresholdBytes)
        {
            return;
        }

        byte[] live = LiveBytesLocked(log);
        log.ApproximateBytes = live.Length;
        string path = FilePath(identity);
        Enqueue(() => Rewrite(path, live));
    }

    /// <summary>The file as only its live entries — the header, then each
    /// entry's put in replay order — recording what each entry's line
    /// weighs.</summary>
    private static byte[] LiveBytesLocked(VaultLog log)
    {
        var output = new List<byte>(log.Header);
        log.EntryBytes.Clear();
        foreach (SidebarJournalEntry entry in log.Entries.Values.OrderBy(entry => entry.Position))
        {
            byte[] line = EncodePut(entry);
            output.AddRange(line);
            log.EntryBytes[entry.Id] = line.Length;
        }

        return [.. output];
    }

    private long TakePosition() => _nextPosition++;

    /// <summary>The root's birth time, read only while the path still
    /// resolves to the identity the journal is keyed by.</summary>
    private static long? BirthOf(string vaultRoot, SidebarJournalIdentity identity)
    {
        (SidebarJournalIdentity? observed, long? birth) = SidebarJournalIdentity.Fingerprint(vaultRoot);
        return observed == identity ? birth : null;
    }

    private Task<bool> Submit(Func<bool> work)
    {
        // Callers hold _gate. One link per operation, in hand-over order: a
        // settle can never overtake the line it tombstones, and a rewrite
        // lands before the next append.
        Task<bool> next = _tail.ContinueWith(
            _ => RunFileWork(work),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
        _tail = next;
        return next;
    }

    private void Enqueue(Action work)
    {
        _ = Submit(() =>
        {
            work();
            return true;
        });
    }

    /// <summary>A failed file operation is logged and answers false: it never
    /// faults the lane, so the next link and a waiting rename both go
    /// on.</summary>
    private static bool RunFileWork(Func<bool> work)
    {
        try
        {
            return work();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.Write(HostDiagnosticEvent.SidebarWriteJournalFailed, exception);
            return false;
        }
    }

    /// <summary>Append one line, writing the header first into an empty file.
    /// A short write is cut back, so the file never ends mid-line by Slate's
    /// hand.</summary>
    private static bool AppendLine(string path, byte[] header, byte[] line, bool flushToDisk)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using SafeFileHandle handle = File.OpenHandle(
            path,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.Read | FileShare.Delete);
        long original = RandomAccess.GetLength(handle);
        byte[] bytes = original == 0 ? [.. header, .. line] : line;
        try
        {
            RandomAccess.Write(handle, bytes, original);
            if (flushToDisk)
            {
                RandomAccess.FlushToDisk(handle);
            }

            return true;
        }
        catch (IOException)
        {
            try
            {
                RandomAccess.SetLength(handle, original);
            }
            catch (IOException)
            {
                // The next open drops what the cut could not.
            }

            throw;
        }
    }

    /// <summary>Atomic rewrite: temporary file, flush, replace.</summary>
    private static void Rewrite(string path, byte[] contents)
    {
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (SafeFileHandle handle = File.OpenHandle(
                temporary,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                RandomAccess.Write(handle, contents, 0);
                RandomAccess.FlushToDisk(handle);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            SafeFile.TryDelete(temporary);
        }
    }

    /// <summary>Set an unreadable file aside so new entries start a clean
    /// file; the copy keeps the damage inspectable.</summary>
    private static void Quarantine(string path) =>
        File.Move(path, path + ".unreadable", overwrite: true);

    private static byte[] Encode(JournalLine line)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(line, LineJson);
        byte[] checksum = Encoding.ASCII.GetBytes(
            SidebarWriteJournalCrc32.Checksum(json).ToString("x8", CultureInfo.InvariantCulture) + " ");
        return [.. checksum, .. json, (byte)'\n'];
    }

    private static JournalLine? ReadLine(ReadOnlySpan<byte> line)
    {
        if (line.Length <= 9 || line[8] != (byte)' '
            || !uint.TryParse(
                Encoding.ASCII.GetString(line[..8]),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out uint checksum))
        {
            return null;
        }

        ReadOnlySpan<byte> json = line[9..];
        if (SidebarWriteJournalCrc32.Checksum(json) != checksum)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JournalLine>(json, LineJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SidebarJournalEntry? DecodeEntry(JournalLine line)
    {
        if (!Guid.TryParse(line.Id, out Guid id)
            || line.At is not long at
            || line.Content?.Structural is not { Transform.Renames: JournalRename[] renames } structural)
        {
            return null;
        }

        var decoded = new List<SidebarJournalRename>(renames.Length);
        foreach (JournalRename rename in renames)
        {
            if (rename.OldPath is not string oldPath || rename.NewPath is not string newPath)
            {
                return null;
            }

            // An unknown kind takes both readings, as mac's nil does: the
            // folder transform also rewrites the exact path.
            decoded.Add(new SidebarJournalRename(oldPath, newPath, rename.IsDirectory ?? true));
        }

        try
        {
            return new SidebarJournalEntry(
                id,
                0,
                DateTimeOffset.FromUnixTimeMilliseconds(at),
                structural.Committed,
                decoded);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static JournalRename RenameLine(SidebarJournalRename rename)
    {
        var line = new JournalRename
        {
            IsDirectory = rename.IsDirectory,
            NewPath = rename.NewPath,
            OldPath = rename.OldPath,
        };
        return line;
    }

    /// <summary>Uppercase, as mac's UUIDs are.</summary>
    private static string IdText(Guid id) => id.ToString("D").ToUpperInvariant();

    private sealed class VaultLog
    {
        internal Dictionary<Guid, SidebarJournalEntry> Entries { get; } = [];

        /// <summary>Pending entries whose core operation has not reported:
        /// an open never adopts one.</summary>
        internal HashSet<Guid> InFlight { get; } = [];

        internal List<SidebarJournalNotice> Notices { get; } = [];

        /// <summary>What each live entry's line weighs: the rest of the file
        /// is settled or superseded, and compaction reclaims it.</summary>
        internal Dictionary<Guid, int> EntryBytes { get; } = [];

        internal byte[] Header { get; set; } = [];

        /// <summary>The file's length as appended since it was last
        /// (re)written.</summary>
        internal long ApproximateBytes { get; set; }

        /// <summary>A newer-format file this build must not touch.</summary>
        internal bool IsReadOnly { get; set; }

        internal long DeadBytes =>
            ApproximateBytes - Header.Length - EntryBytes.Values.Sum(weight => (long)weight);
    }

    internal sealed record ParsedJournal(
        JournalLine? Header,
        IReadOnlyList<JournalLine> Lines,
        int UnreadableLines);

    internal sealed record JournalOnDisk(
        IReadOnlyDictionary<Guid, SidebarJournalEntry> Entries,
        int UnreadableLines);

    /// <summary>One line's JSON object. Properties are declared in key
    /// order, so a line reads as mac's sorted-key encoder writes it.</summary>
    internal sealed class JournalLine
    {
        [JsonPropertyName("at")]
        public long? At { get; set; }

        [JsonPropertyName("birth")]
        public string? Birth { get; set; }

        [JsonPropertyName("content")]
        public JournalContent? Content { get; set; }

        [JsonPropertyName("device")]
        public string? Device { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("ids")]
        public string[]? Ids { get; set; }

        [JsonPropertyName("inode")]
        public string? Inode { get; set; }

        [JsonPropertyName("op")]
        public string? Op { get; set; }

        [JsonPropertyName("version")]
        public int? Version { get; set; }
    }

    internal sealed class JournalContent
    {
        [JsonPropertyName("structural")]
        public JournalStructural? Structural { get; set; }
    }

    /// <summary>mac's Codable encoding of <c>.structural(transform,
    /// committed:)</c>: the unlabelled transform is <c>_0</c>.</summary>
    internal sealed class JournalStructural
    {
        [JsonPropertyName("_0")]
        public JournalTransform? Transform { get; set; }

        [JsonPropertyName("committed")]
        public bool Committed { get; set; }
    }

    /// <summary>mac's <c>SidebarStructuralTransform</c>. Windows journals
    /// renames and moves only; the delete lists stay empty, as a mac
    /// rename's do.</summary>
    internal sealed class JournalTransform
    {
        [JsonPropertyName("deletedFiles")]
        public string[] DeletedFiles { get; set; } = [];

        [JsonPropertyName("deletedFolders")]
        public string[] DeletedFolders { get; set; } = [];

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("renames")]
        public JournalRename[]? Renames { get; set; }
    }

    internal sealed class JournalRename
    {
        [JsonPropertyName("isDirectory")]
        public bool? IsDirectory { get; set; }

        [JsonPropertyName("newPath")]
        public string? NewPath { get; set; }

        [JsonPropertyName("oldPath")]
        public string? OldPath { get; set; }
    }
}

/// <summary>CRC-32 (IEEE 802.3, reflected, polynomial 0xEDB88320) — the
/// per-line checksum that tells a torn or damaged line from a whole one.</summary>
internal static class SidebarWriteJournalCrc32
{
    private static readonly uint[] Table = BuildTable();

    internal static uint Checksum(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFF_FFFF;
        foreach (byte value in bytes)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFF_FFFF;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < 256; index++)
        {
            uint value = index;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) == 1 ? 0xEDB8_8320 ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
