// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1230: the Windows sidebar-settings retry. A blocked read stands as
/// a notice with a Retry action; retry rereads the same file and adopts
/// it on success — with the renames and moves made during the outage
/// replayed onto it first from the sidebar write journal (#1514, as mac's
/// Retry replays its journal), while pin, unpin and shortcut edits made
/// then are discarded — keeps defaults and the notice otherwise, and
/// speaks core's three sidebar-settings events. The stale-references
/// outcome is reserved for a rewrite that could not be carried over.
/// Contracts: docs/plans/38_notification_dispatcher_contracts.md (D-13,
/// amended by #1514).
/// </summary>
/// <remarks>Each fact journals into its own temporary folder, as
/// <see cref="SidebarWriteJournalTests"/> do, so a fact can read what the
/// retry left live.</remarks>
public sealed class SidebarSettingsRetryTests : IDisposable
{
    private const string MalformedReason = "Sidebar settings are malformed and are read-only.";
    private const string Malformed = "{\"version\":1,\"sort\":\"bogus\"}";

    private readonly string _scratch = Path.Combine(
        Path.GetTempPath(),
        $"slate-sidebar-settings-retry-tests-{Guid.NewGuid():N}");

    private readonly SidebarWriteJournal _journal;

    public SidebarSettingsRetryTests()
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
    public void ABlockedReadStandsAsANoticeWithRetryAvailable()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "settings-retry-notice");
        WriteSettings(fixture, Malformed);
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);

        Assert.Equal(MalformedReason, sidebar.SettingsNotice);
        Assert.True(sidebar.HasSettingsNotice);
        Assert.True(sidebar.RetrySettingsCommand.CanExecute(null));
        // Construction is silent: the notice is the standing surface,
        // the three settings events belong to the retry outcome.
        Assert.Empty(announced);

        // The refresh count keeps carrying the reason (unchanged).
        sidebar.Refresh(reportCount: true);
        Assert.EndsWith(MalformedReason, sidebar.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void RetryAfterRepairAdoptsTheFileClearsTheNoticeAndSpeaksReloaded()
    {
        using FixtureVault fixture = FixtureVault.Create(3, "settings-retry-repair");
        WriteSettings(fixture, Malformed);
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);
        Assert.Equal(SidebarSortMode.NameAscending, sidebar.SortMode);

        WriteSettings(fixture, """
            {
              "version": 1,
              "sort": { "field": "created", "direction": "asc" },
              "pins": { "": ["note2.md"] },
              "shortcuts": [ { "kind": "file", "path": "note1.md" } ]
            }
            """);
        sidebar.RetrySettingsCommand.Execute(null);

        Assert.Null(sidebar.SettingsNotice);
        Assert.False(sidebar.HasSettingsNotice);
        Assert.False(sidebar.RetrySettingsCommand.CanExecute(null));
        // The file's authored sections are adopted as written.
        Assert.Equal(SidebarSortMode.CreatedOldest, sidebar.SortMode);
        Assert.False(sidebar.GroupByDate);
        Assert.Equal("note2.md", sidebar.RootNodes[0].Path);
        Assert.Equal("note1.md", Assert.Single(sidebar.Shortcuts).Path);
        A11yEvent spoken = Assert.Single(announced);
        Assert.IsType<A11yEvent.SidebarSettingsReloaded>(spoken);
        Assert.Equal(SlateUniffiMethods.A11yRender(spoken).Text, sidebar.Status);

        // The store is writable again: the next pin lands in the file.
        sidebar.SelectedNode = Node(sidebar, "note0.md");
        sidebar.PinCommand.Execute(null);
        Assert.Equal("Pinned note0.md.", sidebar.Status);
        Assert.Equal(["note0.md", "note2.md"], RootPins(fixture));
    }

    [Fact]
    public void RetryWhileStillBlockedKeepsDefaultsAndSpeaksTheReason()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "settings-retry-blocked");
        WriteSettings(fixture, Malformed);
        byte[] original = File.ReadAllBytes(SettingsPath(fixture));
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);

        sidebar.RetrySettingsCommand.Execute(null);

        Assert.Equal(MalformedReason, sidebar.SettingsNotice);
        Assert.True(sidebar.RetrySettingsCommand.CanExecute(null));
        A11yEvent spoken = Assert.Single(announced);
        var stillDefaults = Assert.IsType<A11yEvent.SidebarSettingsStillDefaults>(spoken);
        Assert.Equal(MalformedReason, stillDefaults.Detail);
        Assert.Equal(SlateUniffiMethods.A11yRender(spoken).Text, sidebar.Status);

        // Fail-closed: the file is untouched and writes still refuse.
        sidebar.SelectedNode = Node(sidebar, "note0.md");
        sidebar.PinCommand.Execute(null);
        Assert.StartsWith("Could not save pins:", sidebar.Status, StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllBytes(SettingsPath(fixture)));
    }

    [Fact]
    public void RetryReportsANewerVersionUntilTheFileIsDowngraded()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "settings-retry-newer");
        WriteSettings(fixture, "{\"version\":99,\"future\":true}");
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);
        const string newerReason = "Sidebar settings use newer version 99 and are read-only.";
        Assert.Equal(newerReason, sidebar.SettingsNotice);

        sidebar.RetrySettingsCommand.Execute(null);
        var stillDefaults = Assert.IsType<A11yEvent.SidebarSettingsStillDefaults>(
            Assert.Single(announced));
        Assert.Equal(newerReason, stillDefaults.Detail);
        Assert.Equal(newerReason, sidebar.SettingsNotice);

        announced.Clear();
        WriteSettings(fixture, "{\"version\":1}");
        sidebar.RetrySettingsCommand.Execute(null);
        Assert.IsType<A11yEvent.SidebarSettingsReloaded>(Assert.Single(announced));
        Assert.Null(sidebar.SettingsNotice);
    }

    [Fact]
    public void RetryAfterARenameDuringTheOutageCarriesThePinAndDiscardsThePinMadeThen()
    {
        using FixtureVault fixture = FixtureVault.Create(3, "settings-retry-rename");
        WriteSettings(fixture, Malformed);
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);

        // A rename while the file is read-only rewrites the stored paths
        // in memory and journals the rename, but cannot land it; the
        // sidebar says so at once.
        sidebar.SelectedNode = Node(sidebar, "note0.md");
        sidebar.MutationName = "renamed.md";
        Assert.True(sidebar.TryRenameSelected());
        Assert.Contains(
            "Sidebar pins and shortcuts could not be saved.",
            sidebar.Status,
            StringComparison.Ordinal);
        // A pin made during the outage is a failed save.
        sidebar.SelectedNode = Node(sidebar, "note1.md");
        sidebar.PinCommand.Execute(null);
        Assert.StartsWith("Could not save pins:", sidebar.Status, StringComparison.Ordinal);
        announced.Clear();

        // The repaired file still pins the OLD name: the rename replays
        // onto it before it is adopted, so the pin follows the note (#1514),
        // while the pin made during the outage is discarded — the file is
        // the authority for those.
        WriteSettings(fixture, "{\"version\":1,\"pins\":{\"\":[\"note0.md\"]}}");
        sidebar.RetrySettingsCommand.Execute(null);

        Assert.Null(sidebar.SettingsNotice);
        A11yEvent spoken = Assert.Single(announced);
        Assert.IsType<A11yEvent.SidebarSettingsReloaded>(spoken);
        Assert.Equal(SlateUniffiMethods.A11yRender(spoken).Text, sidebar.Status);
        Assert.Equal("renamed.md", sidebar.RootNodes[0].Path);
        Assert.Equal(["renamed.md"], RootPins(fixture));
        // The replay's write carried the outage's entry.
        Idle();
        Assert.Equal(0, _journal.LiveEntryCount(Identity(fixture)));
    }

    [Fact]
    public void RetryAfterAMoveDuringTheOutageCarriesTheShortcutInItsSlot()
    {
        using FixtureVault fixture = FixtureVault.Create(3, "settings-retry-move");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "Destination"));
        WriteSettings(fixture, Malformed);
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);

        // A move while the file is read-only: journaled, not landed.
        Node(sidebar, "note1.md").IsBatchSelected = true;
        sidebar.MoveDestination = "Destination";
        sidebar.BatchMoveCommand.Execute(null);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "Destination", "note1.md")));
        announced.Clear();

        // The repaired file's middle slot still names the old path: the
        // move replays onto it, and the slot keeps its place.
        WriteSettings(fixture, """
            {
              "version": 1,
              "shortcuts": [
                { "kind": "file", "path": "note0.md" },
                { "kind": "file", "path": "note1.md" },
                { "kind": "file", "path": "note2.md" }
              ]
            }
            """);
        sidebar.RetrySettingsCommand.Execute(null);

        Assert.Null(sidebar.SettingsNotice);
        Assert.IsType<A11yEvent.SidebarSettingsReloaded>(Assert.Single(announced));
        string[] slots = [.. sidebar.Shortcuts.Select(slot => slot.Path)];
        Assert.Equal(["note0.md", "Destination/note1.md", "note2.md"], slots);
        Assert.Equal(["note0.md", "Destination/note1.md", "note2.md"], ShortcutsOnDisk(fixture));
        Idle();
        Assert.Equal(0, _journal.LiveEntryCount(Identity(fixture)));
    }

    [Fact]
    public void RetryAfterARenameTheJournalCouldNotRecordSpeaksStaleReferences()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "settings-retry-unrecorded");
        WriteSettings(fixture, Malformed);
        // A journal file of a newer format: this build never touches it,
        // so the outage's rename is kept nowhere but memory.
        WriteNewerFormatJournal(fixture);
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);
        sidebar.SelectedNode = Node(sidebar, "note0.md");
        sidebar.MutationName = "renamed.md";
        Assert.True(sidebar.TryRenameSelected());
        Assert.Contains(
            "Sidebar pins and shortcuts could not be saved.",
            sidebar.Status,
            StringComparison.Ordinal);
        announced.Clear();

        // The repaired file still pins the OLD name, and nothing can carry
        // the rename over: adopted as authored, and the outcome says the
        // references may be stale.
        WriteSettings(fixture, "{\"version\":1,\"pins\":{\"\":[\"note0.md\"]}}");
        sidebar.RetrySettingsCommand.Execute(null);

        Assert.Null(sidebar.SettingsNotice);
        Assert.IsType<A11yEvent.SidebarSettingsReloadedStaleRefs>(announced[0]);
        Assert.Equal(["note0.md"], RootPins(fixture));
        // The journal that could not record is said after the outcome.
        Assert.Contains(FilesSidebarViewModel.UnreadableJournalReason, Spoken(announced));
    }

    [Fact]
    public void RetryWhoseReplayWriteFailsSpeaksStaleReferencesAndKeepsTheEntryForTheNextWrite()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "settings-retry-replay-fails");
        WriteSettings(fixture, Malformed);
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);
        sidebar.SelectedNode = Node(sidebar, "note0.md");
        sidebar.MutationName = "renamed.md";
        Assert.True(sidebar.TryRenameSelected());
        announced.Clear();

        // The file is readable again, but another process holds the
        // settings lock while Retry runs: the replay's write cannot land.
        WriteSettings(fixture, "{\"version\":1,\"pins\":{\"\":[\"note0.md\"]}}");
        string lockPath = Path.Combine(fixture.Root, ".slate", "sidebar.json.lock");
        using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            sidebar.RetrySettingsCommand.Execute(null);
        }

        // Adopted with the rename replayed in memory; the file still names
        // the old path, the outcome says so, and the failure is reported.
        Assert.Null(sidebar.SettingsNotice);
        Assert.IsType<A11yEvent.SidebarSettingsReloadedStaleRefs>(announced[0]);
        Assert.Contains(
            Spoken(announced),
            text => text.StartsWith("Could not save pins:", StringComparison.Ordinal));
        Assert.Equal("renamed.md", sidebar.RootNodes[0].Path);
        Assert.Equal(["note0.md"], RootPins(fixture));
        Assert.Equal(1, _journal.LiveEntryCount(Identity(fixture)));

        // The next pins write that lands carries the entry.
        sidebar.SelectedNode = Node(sidebar, "note1.md");
        sidebar.PinCommand.Execute(null);
        Assert.Equal("Pinned note1.md.", sidebar.Status);
        Assert.Equal(["note1.md", "renamed.md"], RootPins(fixture));
        Idle();
        Assert.Equal(0, _journal.LiveEntryCount(Identity(fixture)));
    }

    [Fact]
    public void TheStaleReferencesMemoryBelongsToTheOutageThatMadeIt()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "settings-retry-stale-clears");
        WriteSettings(fixture, Malformed);
        WriteNewerFormatJournal(fixture);
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);
        sidebar.SelectedNode = Node(sidebar, "note0.md");
        sidebar.MutationName = "renamed.md";
        Assert.True(sidebar.TryRenameSelected());
        WriteSettings(fixture, "{\"version\":1}");
        announced.Clear();
        sidebar.RetrySettingsCommand.Execute(null);
        Assert.IsType<A11yEvent.SidebarSettingsReloadedStaleRefs>(announced[0]);

        // A second outage with no structural change in between recovers
        // as a plain reload: the memory belonged to the first outage.
        WriteSettings(fixture, Malformed);
        sidebar.SelectedNode = Node(sidebar, "note1.md");
        sidebar.PinCommand.Execute(null);
        Assert.Equal(MalformedReason, sidebar.SettingsNotice);
        WriteSettings(fixture, "{\"version\":1}");
        announced.Clear();
        sidebar.RetrySettingsCommand.Execute(null);

        Assert.IsType<A11yEvent.SidebarSettingsReloaded>(Assert.Single(announced));
    }

    [Fact]
    public void RetryIsUnavailableAndSilentWhileSettingsAreWritable()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "settings-retry-writable");
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);
        string status = sidebar.Status;

        Assert.Null(sidebar.SettingsNotice);
        Assert.False(sidebar.HasSettingsNotice);
        Assert.False(sidebar.RetrySettingsCommand.CanExecute(null));

        sidebar.RetrySettings();
        Assert.Empty(announced);
        Assert.Equal(status, sidebar.Status);
    }

    [Fact]
    public void AWriteThatFindsTheFileBlockedRaisesTheNoticeAndRetryRecovers()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "settings-retry-midlife");
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned(fixture.Root);
        FilesSidebarViewModel sidebar = NewSidebar(session, fixture, announced);
        sidebar.SelectedNode = Node(sidebar, "note0.md");
        sidebar.PinCommand.Execute(null);
        Assert.Equal(["note0.md"], RootPins(fixture));
        Assert.Null(sidebar.SettingsNotice);

        // The file goes bad under a live sidebar: the next write finds
        // it blocked, and the notice with its Retry appears then.
        WriteSettings(fixture, Malformed);
        sidebar.SelectedNode = Node(sidebar, "note1.md");
        sidebar.PinCommand.Execute(null);
        Assert.StartsWith("Could not save pins:", sidebar.Status, StringComparison.Ordinal);
        Assert.Equal(MalformedReason, sidebar.SettingsNotice);
        Assert.True(sidebar.RetrySettingsCommand.CanExecute(null));

        WriteSettings(fixture, "{\"version\":1,\"pins\":{\"\":[\"note0.md\"]}}");
        announced.Clear();
        sidebar.RetrySettingsCommand.Execute(null);
        Assert.IsType<A11yEvent.SidebarSettingsReloaded>(Assert.Single(announced));
        Assert.Null(sidebar.SettingsNotice);
        // The in-memory pin made during the outage is not salvaged: the
        // file is the authority and it never held note1.md, so unpinning
        // note0.md leaves no pins at all.
        Assert.Equal("note0.md", sidebar.RootNodes[0].Path);
        sidebar.SelectedNode = Node(sidebar, "note0.md");
        sidebar.UnpinCommand.Execute(null);
        Assert.Equal("Unpinned note0.md.", sidebar.Status);
        Assert.Empty(RootPins(fixture));
    }

    private static string SettingsPath(FixtureVault fixture) =>
        Path.Combine(fixture.Root, ".slate", "sidebar.json");

    private static void WriteSettings(FixtureVault fixture, string json)
    {
        Directory.CreateDirectory(Path.Combine(fixture.Root, ".slate"));
        File.WriteAllText(SettingsPath(fixture), json);
    }

    /// <summary>A journal file for the fixture's vault in a format newer
    /// than this build's: it is never replayed and never written, so a
    /// rename made with it in place is journaled nowhere.</summary>
    private void WriteNewerFormatJournal(FixtureVault fixture)
    {
        (SidebarJournalIdentity? identity, long? birth) = SidebarJournalIdentity.Fingerprint(fixture.Root);
        Assert.NotNull(identity);
        Directory.CreateDirectory(JournalDirectory);
        File.WriteAllBytes(
            _journal.FilePath(identity),
            SidebarWriteJournal.EncodeHeader(identity, birth, version: SidebarWriteJournal.FormatVersion + 1));
    }

    private static string[] RootPins(FixtureVault fixture)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(SettingsPath(fixture)));
        if (!document.RootElement.TryGetProperty("pins", out JsonElement pins)
            || !pins.TryGetProperty("", out JsonElement root))
        {
            return [];
        }

        return root
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ShortcutsOnDisk(FixtureVault fixture)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(SettingsPath(fixture)));
        if (!document.RootElement.TryGetProperty("shortcuts", out JsonElement shortcuts))
        {
            return [];
        }

        return [.. shortcuts.EnumerateArray().Select(item => item.GetProperty("path").GetString()!)];
    }

    private static string[] Spoken(List<A11yEvent> announced)
    {
        lock (announced)
        {
            return [.. announced.OfType<A11yEvent.HostComposed>().Select(item => item.Text)];
        }
    }

    private static SidebarJournalIdentity Identity(FixtureVault fixture) =>
        SidebarJournalIdentity.Observe(fixture.Root)
            ?? throw new Xunit.Sdk.XunitException("the fixture's volume answers FileIdInfo");

    private void Idle() =>
        Assert.True(_journal.WhenIdle().Wait(TimeSpan.FromSeconds(10)), "the journal's lane went idle");

    private static FileTreeNodeViewModel Node(FilesSidebarViewModel sidebar, string path) =>
        Assert.Single(sidebar.RootNodes, node => node.Path == path);

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
            localAppDataRoot: Path.Combine(fixture.Root, "device-state"),
            writeJournal: _journal);

    private static VaultSession OpenScanned(string root)
    {
        var session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }
}
