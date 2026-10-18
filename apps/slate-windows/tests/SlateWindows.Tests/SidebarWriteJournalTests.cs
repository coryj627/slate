// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Text.Json;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1435 (the Windows twin of mac's #944, plan 43 PR 23, structural half;
/// mac's <c>SidebarWriteJournalTests</c>): a rename or move journals its
/// renames before core can commit them, in a write-ahead journal keyed by the
/// vault root's identity under local app data, so a crash between core's
/// commit and the pins and shortcuts write, or a write that fails, is
/// replayed by the next open of the same vault — never evicted, and said
/// rather than replayed when it is too old.
/// </summary>
/// <remarks>
/// "Relaunch" is <see cref="Relaunch"/>: every queued line lands, then a
/// journal with nothing loaded reads the same folder, as a new process would.
/// A crash is <see cref="SimulatedCrashException"/>, thrown from the seam that
/// runs after core reported and before any stored path follows. Each fact
/// journals into its own temporary folder, never the user's app data.
/// </remarks>
public sealed class SidebarWriteJournalTests : IDisposable
{
    private readonly string _scratch = Path.Combine(
        Path.GetTempPath(),
        $"slate-sidebar-write-journal-tests-{Guid.NewGuid():N}");

    private SidebarWriteJournal _journal;

    public SidebarWriteJournalTests()
    {
        _journal = new SidebarWriteJournal(JournalDirectory);
    }

    private string JournalDirectory => Path.Combine(_scratch, "journal");

    public void Dispose()
    {
        _ = _journal.WhenIdle().Wait(TimeSpan.FromSeconds(10));
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void TheJournalIsWrittenBeforeAStructuralCommit()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "journal-write-ahead");
        string journalFile = JournalFile(fixture);
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, []);
        Pin(sidebar, "note0.md");
        bool sourceStillThere = false;
        SidebarJournalEntry[] beforeCore = [];
        sidebar.BeforeStructuralCommitForTesting = () =>
        {
            sourceStillThere = File.Exists(Path.Combine(fixture.Root, "note0.md"));
            beforeCore = [.. EntriesOnDisk(journalFile)];
        };

        Assert.True(Rename(sidebar, "note0.md", "renamed.md"));

        Assert.True(sourceStillThere, "the line was on disk before core renamed anything");
        SidebarJournalEntry pending = Assert.Single(beforeCore);
        Assert.False(pending.Committed);
        Assert.Equal(
            new SidebarJournalRename("note0.md", "renamed.md", IsDirectory: false),
            Assert.Single(pending.Renames));
        Assert.Equal(["renamed.md"], PinsOnDisk(fixture));
        Idle();
        Assert.False(
            File.Exists(journalFile),
            "settled once the pins write carried it, and a journal with nothing live is removed");
    }

    /// <summary>mac's fact commits the rename after its vault closed. Windows
    /// renames on the UI thread, so the same window here is the process
    /// ending between core's commit and the sidebar's pins and shortcuts
    /// write: a crash, or a power loss.</summary>
    [Fact]
    public void ARenameThatCommitsAfterItsVaultClosedReplaysOnTheNextLaunch()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "journal-late-rename");
        string journalFile = JournalFile(fixture);
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            FilesSidebarViewModel sidebar = NewSidebar(session, fixture, []);
            Pin(sidebar, "note0.md");
            AddShortcut(sidebar, "note0.md");
            sidebar.StructuralCommitLandedForTesting = () => throw new SimulatedCrashException();

            Assert.Throws<SimulatedCrashException>(() =>
            {
                _ = Rename(sidebar, "note0.md", "renamed.md");
            });

            Assert.True(File.Exists(Path.Combine(fixture.Root, "renamed.md")), "core committed the rename");
            Assert.Equal(["note0.md"], PinsOnDisk(fixture));
            Assert.Equal(["note0.md"], ShortcutsOnDisk(fixture));
        }

        Relaunch();
        var announced = new List<A11yEvent>();
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            FilesSidebarViewModel relaunched = NewSidebar(session, fixture, announced);
            Assert.Equal("renamed.md", Assert.Single(relaunched.Shortcuts).Path);
        }

        Assert.Equal(["renamed.md"], PinsOnDisk(fixture));
        Assert.Equal(["renamed.md"], ShortcutsOnDisk(fixture));
        Assert.Empty(Spoken(announced));
        Idle();
        Assert.False(File.Exists(journalFile), "the replay's write settled the entry");
    }

    [Fact]
    public void APendingRenameThatNeverCommittedIsNotReplayed()
    {
        // A crash between the journal line and core's rename: the entry is
        // still pending and the source still exists, so the replay verifies
        // it away — the pin stays where the file is.
        using FixtureVault fixture = FixtureVault.Create(2, "journal-never-committed");
        string journalFile = JournalFile(fixture);
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            FilesSidebarViewModel sidebar = NewSidebar(session, fixture, []);
            Pin(sidebar, "note0.md");
            AppendDurablePending(fixture, [new("note0.md", "renamed.md", IsDirectory: false)]);
        }

        Relaunch();
        var announced = new List<A11yEvent>();
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            _ = NewSidebar(session, fixture, announced);
        }

        Assert.Equal(["note0.md"], PinsOnDisk(fixture));
        Assert.Empty(Spoken(announced));
        Idle();
        Assert.False(File.Exists(journalFile), "the verified-away entry is settled");
    }

    [Fact]
    public void ATornTailIsDroppedAndSurfacedAndEarlierRecordsStillReplay()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "journal-torn");
        string journalFile = JournalFile(fixture);
        CrashAfterCommittedRename(fixture, "note0.md", "renamed.md");
        Idle();
        // A crash mid-append: the last line lost its end and its newline.
        using (var stream = new FileStream(journalFile, FileMode.Append, FileAccess.Write))
        {
            stream.Write("0badf00d {\"op\":\"put\",\"id\":"u8);
        }

        Assert.Equal(1, OnDisk(journalFile).UnreadableLines);
        Relaunch();
        var announced = new List<A11yEvent>();
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            _ = NewSidebar(session, fixture, announced);
        }

        Assert.Equal(["renamed.md"], PinsOnDisk(fixture));
        Assert.Contains(FilesSidebarViewModel.UnreadableJournalReason, Spoken(announced));
        Idle();
        Assert.False(
            File.Exists(journalFile),
            "rewritten without the damage, then removed once nothing is live");
    }

    [Fact]
    public void AReplacedVaultNeverMatchesItsJournal()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "journal-replaced");
        SidebarJournalIdentity original = Identity(fixture);
        CrashAfterCommittedRename(fixture, "note0.md", "renamed.md");
        Relaunch();

        // Another vault takes the path: the same names, another directory.
        DeleteDirectory(fixture.Root);
        Directory.CreateDirectory(fixture.Root);
        File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), "# Note 0\n");
        File.WriteAllText(Path.Combine(fixture.Root, "renamed.md"), "# Renamed\n");
        WriteSettings(fixture, "{\"version\":1,\"pins\":{\"\":[\"note0.md\"]}}");
        SidebarJournalIdentity replacement = Identity(fixture);
        Assert.NotEqual(original, replacement);
        var announced = new List<A11yEvent>();
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            _ = NewSidebar(session, fixture, announced);
        }

        Assert.Equal(["note0.md"], PinsOnDisk(fixture));
        Assert.Empty(Spoken(announced));
        Idle();
        _ = Assert.Single(EntriesOnDisk(_journal.FilePath(original)));

        // A journal that claims this vault's identity with another birth time
        // belongs to a vault that is gone: never replayed, said, and removed.
        string forged = _journal.FilePath(replacement);
        var foreign = new SidebarJournalEntry(
            Guid.NewGuid(),
            0,
            DateTimeOffset.UtcNow,
            Committed: true,
            [new("note0.md", "renamed.md", IsDirectory: false)]);
        byte[] forgedBytes =
        [
            .. SidebarWriteJournal.EncodeHeader(replacement, birth: 42),
            .. SidebarWriteJournal.EncodePut(foreign),
        ];
        File.WriteAllBytes(forged, forgedBytes);
        Relaunch();
        announced.Clear();
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            _ = NewSidebar(session, fixture, announced);
        }

        Assert.Equal(["note0.md"], PinsOnDisk(fixture));
        Assert.Contains(FilesSidebarViewModel.ReplacedVaultJournalReason, Spoken(announced));
        Idle();
        Assert.False(File.Exists(forged), "the foreign journal is removed");
    }

    [Fact]
    public void AFailedPinsWriteAfterARenameReplaysOnTheNextOpen()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "journal-failed-pins");
        string journalFile = JournalFile(fixture);
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            var announced = new List<A11yEvent>();
            FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);
            Pin(sidebar, "note0.md");
            AddShortcut(sidebar, "note0.md");
            // Another process holds the settings lock: the rename lands, and
            // its pins and shortcuts writes cannot.
            string lockPath = Path.Combine(fixture.Root, ".slate", "sidebar.json.lock");
            using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.True(Rename(sidebar, "note0.md", "renamed.md"));
            }

            Assert.Contains(
                "Sidebar pins and shortcuts could not be saved.",
                sidebar.Status,
                StringComparison.Ordinal);
            Assert.Contains(
                Spoken(announced),
                text => text.StartsWith("Could not save pins:", StringComparison.Ordinal));
            Assert.Equal("renamed.md", sidebar.RootNodes[0].Path);
            Assert.Equal(["note0.md"], PinsOnDisk(fixture));
        }

        Idle();
        SidebarJournalEntry kept = Assert.Single(EntriesOnDisk(journalFile));
        Assert.True(kept.Committed, "the divergence survives the quit");
        Relaunch();
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            _ = NewSidebar(session, fixture, []);
        }

        Assert.Equal(["renamed.md"], PinsOnDisk(fixture));
        Assert.Equal(["renamed.md"], ShortcutsOnDisk(fixture));
        Idle();
        Assert.False(File.Exists(journalFile));
    }

    [Fact]
    public void AFailedWriteIsCarriedUntilTheNextPinsAndShortcutsWritesLand()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "journal-carried");
        string journalFile = JournalFile(fixture);
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, []);
        Pin(sidebar, "note0.md");
        AddShortcut(sidebar, "note0.md");
        string lockPath = Path.Combine(fixture.Root, ".slate", "sidebar.json.lock");
        using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.True(Rename(sidebar, "note0.md", "renamed.md"));
        }

        Idle();
        _ = Assert.Single(EntriesOnDisk(journalFile));

        // The next pins write lands every pin in memory, the rename's among
        // them, and the entry still waits for its shortcuts...
        Pin(sidebar, "note1.md");
        Idle();
        Assert.Equal(["note1.md", "renamed.md"], PinsOnDisk(fixture));
        _ = Assert.Single(EntriesOnDisk(journalFile));

        // ...which the next shortcuts write that lands carries.
        AddShortcut(sidebar, "note1.md");
        Assert.Equal(["renamed.md", "note1.md"], ShortcutsOnDisk(fixture));
        Idle();
        Assert.False(File.Exists(journalFile), "settled once both sections carried it");
    }

    [Fact]
    public void ACommittedRenameReplaysWhereItsSourceIsBackAndAPendingOneDoesNot()
    {
        // Both sources exist at the next open: note0.md is a new note at the
        // old path of a rename core reported committed, and note1.md's rename
        // never ran. A committed entry needs no verification and replays; a
        // pending one is verified against the disk and skipped.
        using FixtureVault fixture = FixtureVault.Create(2, "journal-sources-back");
        File.WriteAllText(Path.Combine(fixture.Root, "renamed0.md"), "# Renamed 0\n");
        string journalFile = JournalFile(fixture);
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            FilesSidebarViewModel sidebar = NewSidebar(session, fixture, []);
            Pin(sidebar, "note0.md");
            Pin(sidebar, "note1.md");
            SidebarJournalRename committed = new("note0.md", "renamed0.md", IsDirectory: false);
            Guid id = AppendDurablePending(fixture, [committed]);
            _journal.ResolveCommitted(Identity(fixture), id, [committed]);
            _ = AppendDurablePending(fixture, [new("note1.md", "renamed1.md", IsDirectory: false)]);
        }

        Relaunch();
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            _ = NewSidebar(session, fixture, []);
        }

        Assert.Equal(["note1.md", "renamed0.md"], PinsOnDisk(fixture));
        Idle();
        Assert.False(File.Exists(journalFile));
    }

    [Fact]
    public void AFolderRenameWithItsNoteJournalsItsFixUpAndReplaysIt()
    {
        // The compound rename moves the folder and renames its note to the
        // new stem. The pending entry anticipates both renames, the committed
        // entry holds the report's fix-up, and the replay lands the note's pin
        // at guide/guide.md — not the prefix image guide/docs.md.
        using FixtureVault fixture = FixtureVault.Create(0, "journal-folder-note");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "docs"));
        File.WriteAllText(Path.Combine(fixture.Root, "docs", "docs.md"), "# docs\n");
        File.WriteAllText(Path.Combine(fixture.Root, "docs", "other.md"), "O\n");
        string journalFile = JournalFile(fixture);
        SidebarJournalRename[] folderRenames =
        [
            new("docs", "guide", IsDirectory: true),
            new("guide/docs.md", "guide/guide.md", IsDirectory: false),
        ];
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            FilesSidebarViewModel sidebar = NewSidebar(session, fixture, []);
            FileTreeNodeViewModel docs = Node(sidebar, "docs");
            sidebar.LoadChildren(docs);
            foreach (string path in new[] { "docs/docs.md", "docs/other.md" })
            {
                sidebar.SelectedNode = Assert.Single(docs.Children, node => node.Path == path);
                sidebar.PinCommand.Execute(null);
            }

            SidebarJournalEntry[] beforeCore = [];
            SidebarJournalEntry[] afterCore = [];
            sidebar.BeforeStructuralCommitForTesting = () => beforeCore = [.. EntriesOnDisk(journalFile)];
            sidebar.StructuralCommitLandedForTesting = () =>
            {
                Idle();
                afterCore = [.. EntriesOnDisk(journalFile)];
                throw new SimulatedCrashException();
            };

            Assert.Throws<SimulatedCrashException>(() =>
            {
                _ = Rename(sidebar, "docs", "guide");
            });

            SidebarJournalRename[] planned = [.. Assert.Single(beforeCore).Renames];
            Assert.Equal(folderRenames, planned);
            SidebarJournalEntry committed = Assert.Single(afterCore);
            Assert.True(committed.Committed);
            SidebarJournalRename[] landed = [.. committed.Renames];
            Assert.Equal(folderRenames, landed);
            Assert.True(File.Exists(Path.Combine(fixture.Root, "guide", "guide.md")));
            Assert.Equal(["docs/docs.md", "docs/other.md"], PinsOnDisk(fixture));
        }

        Relaunch();
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            _ = NewSidebar(session, fixture, []);
        }

        Assert.Equal(["guide/guide.md", "guide/other.md"], PinsOnDisk(fixture));
        Idle();
        Assert.False(File.Exists(journalFile));
    }

    [Fact]
    public void APendingBatchReplaysOnlyTheRenamesThatStood()
    {
        // A crash after one of two renames committed: the entry is pending,
        // so each rename is verified against the disk on its own.
        using FixtureVault fixture = FixtureVault.Create(0, "journal-partial-batch");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "# A\n");
        File.WriteAllText(Path.Combine(fixture.Root, "b.md"), "# B\n");
        string journalFile = JournalFile(fixture);
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            FilesSidebarViewModel sidebar = NewSidebar(session, fixture, []);
            Pin(sidebar, "a.md");
            Pin(sidebar, "b.md");
            AppendDurablePending(
                fixture,
                [new("a.md", "a2.md", IsDirectory: false), new("b.md", "b2.md", IsDirectory: false)]);
        }

        File.Move(Path.Combine(fixture.Root, "a.md"), Path.Combine(fixture.Root, "a2.md"));
        Relaunch();
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            _ = NewSidebar(session, fixture, []);
        }

        Assert.Equal(["a2.md", "b.md"], PinsOnDisk(fixture));
        Idle();
        Assert.False(File.Exists(journalFile));
    }

    [Fact]
    public void ABatchMoveRecordsItsStandingAndReplaysOnlyWhatStood()
    {
        // Two items, one already in the destination: core skips it, so only
        // one of the two renames the entry anticipated stands.
        using FixtureVault fixture = FixtureVault.Create(1, "journal-batch-standing");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "Destination"));
        File.WriteAllText(Path.Combine(fixture.Root, "Destination", "kept.md"), "# Kept\n");
        string journalFile = JournalFile(fixture);
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            FilesSidebarViewModel sidebar = NewSidebar(session, fixture, []);
            Pin(sidebar, "note0.md");
            FileTreeNodeViewModel folder = Node(sidebar, "Destination");
            sidebar.LoadChildren(folder);
            FileTreeNodeViewModel kept = Assert.Single(
                folder.Children,
                node => node.Path == "Destination/kept.md");
            sidebar.SelectedNode = kept;
            sidebar.PinCommand.Execute(null);
            kept.IsBatchSelected = true;
            Node(sidebar, "note0.md").IsBatchSelected = true;
            SidebarJournalEntry[] beforeCore = [];
            SidebarJournalEntry[] afterCore = [];
            sidebar.BeforeStructuralCommitForTesting = () => beforeCore = [.. EntriesOnDisk(journalFile)];
            sidebar.StructuralCommitLandedForTesting = () =>
            {
                Idle();
                afterCore = [.. EntriesOnDisk(journalFile)];
                throw new SimulatedCrashException();
            };
            sidebar.MoveDestination = "Destination";

            Assert.Throws<SimulatedCrashException>(() => sidebar.BatchMoveCommand.Execute(null));

            SidebarJournalEntry pending = Assert.Single(beforeCore);
            Assert.False(pending.Committed);
            Assert.Equal(2, pending.Renames.Count);
            Assert.Contains(
                new SidebarJournalRename("note0.md", "Destination/note0.md", IsDirectory: false),
                pending.Renames);
            SidebarJournalEntry committed = Assert.Single(afterCore);
            Assert.True(committed.Committed);
            Assert.Equal(
                new SidebarJournalRename("note0.md", "Destination/note0.md", IsDirectory: false),
                Assert.Single(committed.Renames));
            Assert.True(File.Exists(Path.Combine(fixture.Root, "Destination", "note0.md")));
            Assert.Equal(["Destination/kept.md", "note0.md"], PinsOnDisk(fixture));
        }

        Relaunch();
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            _ = NewSidebar(session, fixture, []);
        }

        Assert.Equal(["Destination/kept.md", "Destination/note0.md"], PinsOnDisk(fixture));
        Idle();
        Assert.False(File.Exists(journalFile));
    }

    [Fact]
    public void AnAgedOutRenameIsSurfacedNotReplayed()
    {
        Assert.Equal(
            "A sidebar change waiting more than 30 days to be saved was not applied.",
            FilesSidebarViewModel.AgedOutJournalReason(1, 30));
        Assert.Equal(
            "2 sidebar changes waiting more than 30 days to be saved were not applied.",
            FilesSidebarViewModel.AgedOutJournalReason(2, 30));
        using FixtureVault fixture = FixtureVault.Create(2, "journal-aged");
        string journalFile = JournalFile(fixture);
        CrashAfterCommittedRename(fixture, "note0.md", "renamed.md");
        Relaunch();

        // The vault stays closed longer than the journal replays.
        _journal.Now = () => DateTimeOffset.UtcNow + SidebarWriteJournal.MaxEntryAge + TimeSpan.FromMinutes(1);
        var announced = new List<A11yEvent>();
        using (VaultSession session = OpenScanned(fixture.Root))
        {
            _ = NewSidebar(session, fixture, announced);
        }

        Assert.Contains(FilesSidebarViewModel.AgedOutJournalReason(1, 30), Spoken(announced));
        Assert.Equal(["note0.md"], PinsOnDisk(fixture));
        Idle();
        Assert.False(File.Exists(journalFile), "once said it is settled, never said twice");
    }

    [Fact]
    public void ARetryAfterRepairReplaysWhatWaitedForAReadableFile()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "journal-retry");
        string journalFile = JournalFile(fixture);
        CrashAfterCommittedRename(fixture, "note0.md", "renamed.md");
        // While Slate was closed the settings file went bad: the next open
        // runs read-only on defaults and adopts nothing from the journal.
        WriteSettings(fixture, "{\"version\":1,\"sort\":\"bogus\"}");
        Relaunch();
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);
        Assert.NotNull(sidebar.SettingsNotice);
        Idle();
        _ = Assert.Single(EntriesOnDisk(journalFile));

        // The repaired file still pins the old name; the retry adopts it with
        // the journal's rename replayed, as an open would have.
        WriteSettings(fixture, "{\"version\":1,\"pins\":{\"\":[\"note0.md\"]}}");
        sidebar.RetrySettingsCommand.Execute(null);

        Assert.Null(sidebar.SettingsNotice);
        Assert.IsType<A11yEvent.SidebarSettingsReloaded>(Assert.Single(announced));
        Assert.Equal(["renamed.md"], PinsOnDisk(fixture));
        Idle();
        Assert.False(File.Exists(journalFile));
    }

    [Fact]
    public void TheRecordFormatIsChecksummedLineDelimitedAndVersioned()
    {
        // CRC-32 (IEEE) check value.
        Assert.Equal(0xCBF4_3926u, SidebarWriteJournalCrc32.Checksum("123456789"u8));

        var identity = new SidebarJournalIdentity(7UL, 99UL);
        var entry = new SidebarJournalEntry(
            Guid.NewGuid(),
            0,
            DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            Committed: false,
            [new("Projects/a.md", "Projects/b.md", IsDirectory: false)]);
        byte[] header = SidebarWriteJournal.EncodeHeader(identity, birth: 5);
        byte[] put = SidebarWriteJournal.EncodePut(entry);
        // `<8 hex> <json>\n`, one line per record, in mac's shape.
        string text = Encoding.UTF8.GetString(put);
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", text[..^1], StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{8} \\{", text);
        Assert.Contains("\"content\":{\"structural\":{\"_0\":{", text, StringComparison.Ordinal);
        Assert.Contains(
            "\"renames\":[{\"isDirectory\":false,\"newPath\":\"Projects/b.md\",\"oldPath\":\"Projects/a.md\"}]",
            text,
            StringComparison.Ordinal);
        string idText = entry.Id.ToString("D").ToUpperInvariant();
        Assert.Contains($"\"id\":\"{idText}\"", text, StringComparison.Ordinal);

        // A damaged line in the middle is dropped and counted; its neighbours stay.
        byte[] damaged = [.. put];
        damaged[20] ^= 0x01;
        byte[] settle = SidebarWriteJournal.EncodeSettle([Guid.NewGuid()]);
        SidebarWriteJournal.ParsedJournal parsed = SidebarWriteJournal.Parse([.. header, .. damaged, .. put, .. settle]);
        Assert.Equal(SidebarWriteJournal.FormatVersion, parsed.Header?.Version);
        Assert.Equal("7", parsed.Header?.Device);
        Assert.Equal("99", parsed.Header?.Inode);
        Assert.Equal("5", parsed.Header?.Birth);
        Assert.Equal(1, parsed.UnreadableLines);
        Assert.Equal(2, parsed.Lines.Count);
        Assert.Equal("put", parsed.Lines[0].Op);
        Assert.Equal("settle", parsed.Lines[1].Op);

        // A damaged line ahead of the header is skipped like any other: the
        // first line that verifies is the header, and the entries still load.
        SidebarWriteJournal.ParsedJournal skipped = SidebarWriteJournal.Parse([.. damaged, .. header, .. put]);
        Assert.Equal("header", skipped.Header?.Op);
        Assert.Equal(1, skipped.UnreadableLines);
        Assert.Equal("put", Assert.Single(skipped.Lines).Op);

        // A file of damaged lines only has no header, and every line counts —
        // the unterminated tail included.
        byte[] garbage = [.. "not a journal line\n"u8];
        byte[] tail = [.. "0badf00d {"u8];
        SidebarWriteJournal.ParsedJournal onlyDamaged = SidebarWriteJournal.Parse([.. garbage, .. damaged, .. tail]);
        Assert.Null(onlyDamaged.Header);
        Assert.Equal(3, onlyDamaged.UnreadableLines);
        Assert.Empty(onlyDamaged.Lines);

        // A file whose first verified line is not a header has none.
        Assert.Null(SidebarWriteJournal.Parse([.. put, .. header]).Header);

        // A newer format is never replayed and never touched.
        string vaultRoot = Path.Combine(_scratch, "newer");
        Directory.CreateDirectory(vaultRoot);
        Directory.CreateDirectory(JournalDirectory);
        byte[] newer =
        [
            .. SidebarWriteJournal.EncodeHeader(identity, birth: null, version: SidebarWriteJournal.FormatVersion + 1),
            .. put,
        ];
        string newerPath = _journal.FilePath(identity);
        File.WriteAllBytes(newerPath, newer);
        SidebarJournalAdoption adoption = _journal.Adopt(identity, vaultRoot);
        Assert.Empty(adoption.Entries);
        Assert.Equal(SidebarJournalNotice.Unreadable, Assert.Single(adoption.Notices));
        Assert.Null(_journal.AppendPending(identity, vaultRoot, [new("a.md", "b.md", IsDirectory: false)]));
        Idle();
        Assert.Equal(newer, File.ReadAllBytes(newerPath));
    }

    [Fact]
    public void SettledLinesAreReclaimedAndOnlyLiveEntriesStay()
    {
        string vaultRoot = Path.Combine(_scratch, "compaction");
        Directory.CreateDirectory(vaultRoot);
        var identity = new SidebarJournalIdentity(11UL, 1_234UL);
        string padding = new('p', 1_000);
        var ids = new List<Guid>();
        for (int index = 0; index < 300; index++)
        {
            SidebarJournalAppend append = _journal.AppendPending(
                identity,
                vaultRoot,
                [new($"{padding}/n{index}.md", $"m{index}.md", IsDirectory: false)])
                ?? throw new Xunit.Sdk.XunitException("the entry was journaled");
            ids.Add(append.Id);
        }

        Idle();
        string path = _journal.FilePath(identity);
        Assert.True(new FileInfo(path).Length > SidebarWriteJournal.CompactionThresholdBytes);

        // Settling all but the last makes the file mostly dead lines: it is
        // rewritten with the header and the one live entry.
        _journal.Settle(identity, ids.Take(ids.Count - 1));
        Idle();
        Assert.True(new FileInfo(path).Length < 4 * 1024, "settled lines are reclaimed");
        SidebarWriteJournal.JournalOnDisk onDisk = OnDisk(path);
        Assert.Equal(ids[^1], Assert.Single(onDisk.Entries).Key);
        Assert.Equal(0, onDisk.UnreadableLines);
        Assert.Equal(1, _journal.LiveEntryCount(identity));

        _journal.Settle(identity, [ids[^1]]);
        Idle();
        Assert.False(File.Exists(path), "an all-settled journal is removed");
    }

    [Fact]
    public void AJournalThatCannotWriteNeverStopsARename()
    {
        // A file where the journal's folder belongs: no line can land (the
        // lane answers false), and the rename and its pins write still go on.
        Directory.CreateDirectory(_scratch);
        File.WriteAllText(JournalDirectory, "not a folder");
        using FixtureVault fixture = FixtureVault.Create(2, "journal-cannot-write");
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, []);
        Pin(sidebar, "note0.md");

        Assert.True(Rename(sidebar, "note0.md", "renamed.md"));

        Assert.True(File.Exists(Path.Combine(fixture.Root, "renamed.md")));
        Assert.Equal(["renamed.md"], PinsOnDisk(fixture));
        Idle();
        Assert.False(Directory.Exists(JournalDirectory));
    }

    [Fact]
    public void ATestProcessNeverJournalsIntoTheUsersAppData()
    {
        // Only the app installs the production location (App.OnStartup); a
        // test host keeps the process's private temporary folder.
        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Slate",
                "sidebar-write-journal"),
            SidebarWriteJournal.ProductionDirectory);
        Assert.NotEqual(
            Path.GetFullPath(SidebarWriteJournal.ProductionDirectory),
            SidebarWriteJournal.Shared.JournalDirectory,
            StringComparer.OrdinalIgnoreCase);
        Assert.StartsWith(
            Path.GetFullPath(Path.GetTempPath()),
            SidebarWriteJournal.Shared.JournalDirectory,
            StringComparison.OrdinalIgnoreCase);
    }

    // ---- Helpers ------------------------------------------------------

    private sealed class SimulatedCrashException : Exception
    {
        public SimulatedCrashException()
            : base("The process ended here.")
        {
        }
    }

    /// <summary>Every queued line lands, then a journal with nothing loaded
    /// reads the same folder — a new process.</summary>
    private void Relaunch()
    {
        Idle();
        _journal = new SidebarWriteJournal(JournalDirectory);
    }

    private void Idle() =>
        Assert.True(_journal.WhenIdle().Wait(TimeSpan.FromSeconds(10)), "the journal's lane went idle");

    /// <summary>A launch that pins <paramref name="path"/>, renames it, and
    /// ends after core committed the rename and before the pins write.</summary>
    private void CrashAfterCommittedRename(FixtureVault fixture, string path, string newName)
    {
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, []);
        Pin(sidebar, path);
        sidebar.StructuralCommitLandedForTesting = () => throw new SimulatedCrashException();
        Assert.Throws<SimulatedCrashException>(() =>
        {
            _ = Rename(sidebar, path, newName);
        });
        Assert.Equal([path], PinsOnDisk(fixture));
    }

    private Guid AppendDurablePending(FixtureVault fixture, IReadOnlyList<SidebarJournalRename> renames)
    {
        SidebarJournalAppend append = _journal.AppendPending(Identity(fixture), fixture.Root, renames)
            ?? throw new Xunit.Sdk.XunitException("the renames were journaled");
        Assert.True(append.Durable.Wait(TimeSpan.FromSeconds(10)) && append.Durable.Result);
        return append.Id;
    }

    private FilesSidebarViewModel NewSidebar(
        VaultSession session,
        FixtureVault fixture,
        List<A11yEvent> announced) => new(
            session,
            item =>
            {
                lock (announced)
                {
                    announced.Add(item);
                }
            },
            vaultRoot: fixture.Root,
            localAppDataRoot: Path.Combine(_scratch, "recents"),
            writeJournal: _journal);

    private string JournalFile(FixtureVault fixture) => _journal.FilePath(Identity(fixture));

    private static SidebarJournalIdentity Identity(FixtureVault fixture) =>
        SidebarJournalIdentity.Observe(fixture.Root)
            ?? throw new Xunit.Sdk.XunitException("the fixture's volume answers FileIdInfo");

    private static SidebarWriteJournal.JournalOnDisk OnDisk(string path) =>
        SidebarWriteJournal.ReadForTesting(path)
            ?? throw new Xunit.Sdk.XunitException($"no journal at {path}");

    private static SidebarJournalEntry[] EntriesOnDisk(string path) =>
        SidebarWriteJournal.ReadForTesting(path) is SidebarWriteJournal.JournalOnDisk onDisk
            ? [.. onDisk.Entries.Values]
            : [];

    private static string[] Spoken(List<A11yEvent> announced)
    {
        lock (announced)
        {
            return [.. announced.OfType<A11yEvent.HostComposed>().Select(item => item.Text)];
        }
    }

    private static FileTreeNodeViewModel Node(FilesSidebarViewModel sidebar, string path) =>
        Assert.Single(sidebar.RootNodes, node => node.Path == path);

    private static void Pin(FilesSidebarViewModel sidebar, string path)
    {
        sidebar.SelectedNode = Node(sidebar, path);
        sidebar.PinCommand.Execute(null);
    }

    private static void AddShortcut(FilesSidebarViewModel sidebar, string path)
    {
        sidebar.SelectedNode = Node(sidebar, path);
        sidebar.AddShortcutCommand.Execute(null);
    }

    private static bool Rename(FilesSidebarViewModel sidebar, string path, string newName)
    {
        sidebar.SelectedNode = Node(sidebar, path);
        sidebar.MutationName = newName;
        return sidebar.TryRenameSelected();
    }

    private static string SettingsPath(FixtureVault fixture) =>
        Path.Combine(fixture.Root, ".slate", "sidebar.json");

    private static void WriteSettings(FixtureVault fixture, string json)
    {
        Directory.CreateDirectory(Path.Combine(fixture.Root, ".slate"));
        File.WriteAllText(SettingsPath(fixture), json);
    }

    private static string[] PinsOnDisk(FixtureVault fixture)
    {
        if (!File.Exists(SettingsPath(fixture)))
        {
            return [];
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(SettingsPath(fixture)));
        if (!document.RootElement.TryGetProperty("pins", out JsonElement pins))
        {
            return [];
        }

        return
        [
            .. pins.EnumerateObject()
                .SelectMany(folder => folder.Value.EnumerateArray())
                .Select(item => item.GetString()!)
                .Order(StringComparer.Ordinal),
        ];
    }

    private static string[] ShortcutsOnDisk(FixtureVault fixture)
    {
        if (!File.Exists(SettingsPath(fixture)))
        {
            return [];
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(SettingsPath(fixture)));
        if (!document.RootElement.TryGetProperty("shortcuts", out JsonElement shortcuts))
        {
            return [];
        }

        return [.. shortcuts.EnumerateArray().Select(item => item.GetProperty("path").GetString()!)];
    }

    private static VaultSession OpenScanned(string root)
    {
        var session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    /// <summary>A just-closed session's files can stay busy for a moment on
    /// Windows; the replacement vault waits for them.</summary>
    private static void DeleteDirectory(string path)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (++attempt < 50)
            {
                Thread.Sleep(100);
            }
        }
    }
}
