// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-39 (#1670): a Reveal in File Tree into a folder that has not
/// listed its rows seats when the folder lists, at any depth and under date
/// groups, and lands the keys on its row once; and the newest intent wins: a
/// Reveal, a mutation's named row, a gesture that names no row and the
/// reader's own selection are ordered by one number, so an older intent never
/// seats after a newer one, and a wait the folder cannot satisfy ends.
/// </summary>
public sealed partial class FilesRegionLandingTests
{
    /// <summary>The keys are outside Files; the note's folder has not listed
    /// its rows. When the folder's own load publishes them, the note is
    /// selected on both halves and the keys land on its row in one move; it
    /// opens beside without the editor's focus and its selection is said once.
    /// Main waited for a later whole-tree publication.</summary>
    [Fact]
    public void ARevealIntoAnUnlistedFolderSeatsWhenTheFolderLists() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);

        host.Sidebar.SelectPathFromSurface("docs/n.md");
        SettleLoads(host);

        AssertSeatedOnce(host, "docs/n.md", log);
    });

    /// <summary>The same through the headless synchronous load, which lists
    /// inside the request: main seated there but left the keys where they
    /// were.</summary>
    [Fact]
    public void ARevealIntoAnUnlistedFolderSeatsWhenItListsSynchronously() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize(synchronousTree: true);
        RevealLog log = KeysOutsideFiles(host);

        host.Sidebar.SelectPathFromSurface("docs/n.md");
        SettleLoads(host);

        AssertSeatedOnce(host, "docs/n.md", log);
    });

    /// <summary>Two unlisted folders down: the first folder's load lists the
    /// second collapsed, and the Reveal expands it in turn and seats when it
    /// lists. Main never seated it, whatever published later.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARevealTwoUnlistedFoldersDownSeats(bool synchronousTree) => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize(synchronousTree: synchronousTree);
        RevealLog log = KeysOutsideFiles(host);

        host.Sidebar.SelectPathFromSurface("deep/mid/n2.md");
        SettleLoads(host);

        Assert.True(Row(host, "deep").IsExpanded, "deep is not expanded");
        Assert.True(Row(host, "deep/mid").IsExpanded, "deep/mid is not expanded");
        AssertSeatedOnce(host, "deep/mid/n2.md", log);
    });

    /// <summary>Group by date on: a folder lists its files under its own date
    /// group headers, so the note is not a row of the folder's level. It still
    /// seats at the folder's load.</summary>
    [Fact]
    public void ARevealIntoAnUnlistedFolderSeatsUnderDateGroups() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        UseDateGroups(host);
        RevealLog log = KeysOutsideFiles(host);

        host.Sidebar.SelectPathFromSurface("docs/n.md");
        SettleLoads(host);

        FileTreeNodeViewModel docs = Row(host, "docs");
        Assert.True(docs.Children.All(child => child.IsGroupHeader), "premise: docs lists its files under date groups");
        AssertSeatedOnce(host, "docs/n.md", log);
    });

    /// <summary>A folder listed and then collapsed keeps its rows, so a Reveal
    /// into it seats at the request: the folder opens and the keys land on the
    /// note once.</summary>
    [Fact]
    public void ARevealIntoAListedCollapsedFolderSeatsAtTheRequest() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        FileTreeNodeViewModel docs = Row(host, "docs");
        ExpandAndList(host, docs);
        docs.IsExpanded = false;
        Settle(host);
        RevealLog log = KeysOutsideFiles(host);

        host.Sidebar.SelectPathFromSurface("docs/n.md");

        Assert.Equal("docs/n.md", host.Sidebar.SelectedNode?.Path);
        Assert.True(docs.IsExpanded, "the Reveal did not open docs");
        SettleLoads(host);
        AssertSeatedOnce(host, "docs/n.md", log);
    });

    /// <summary>The first folder is listed and collapsed, the second has not
    /// listed: the Reveal opens the first without a load and seats after the
    /// second's one load.</summary>
    [Fact]
    public void ARevealUnderAListedCollapsedFolderSeatsAfterOneLoad() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        FileTreeNodeViewModel deep = Row(host, "deep");
        ExpandAndList(host, deep);
        deep.IsExpanded = false;
        Settle(host);
        Assert.False(Row(host, "deep/mid").HasLoadedChildren, "premise: deep/mid has listed its rows");
        RevealLog log = KeysOutsideFiles(host);

        host.Sidebar.SelectPathFromSurface("deep/mid/n2.md");
        SettleLoads(host);

        AssertSeatedOnce(host, "deep/mid/n2.md", log);
    });

    /// <summary>The reader selects another row while the Reveal waits: that is
    /// the newer intent. The folder's load and a later whole-tree publication
    /// leave the reader's row selected with the keys, and the revealed note is
    /// never selected, opened or said. Main took the selection back at the
    /// next publication.</summary>
    [Fact]
    public void TheReadersOwnSelectionDropsAWaitingReveal() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        host.Sidebar.SelectPathFromSurface("docs/n.md");

        FileTreeNodeViewModel reader = Row(host, "a.md");
        ReaderFocuses(host, reader);
        host.ReleaseTreeWork();
        SettleLoads(host);
        host.Sidebar.Refresh();
        Settle(host);

        Assert.Same(reader, host.Sidebar.SelectedNode);
        Assert.True(reader.IsSelected, "the reader's row lost its selection");
        TreeViewItem container = ContainerOf(host, reader);
        Assert.True(container.IsKeyboardFocused, $"the keys are on {Keyboard.FocusedElement}");
        Assert.Same(container, Assert.Single(host.FocusChanges));
        Assert.Equal([("a.md", false)], log.Opened);
        Assert.Equal(["Selected: a.md"], SelectionLines(host));
        AssertNeverSeated(host, "docs/n.md", log);
    });

    /// <summary>A gesture that names no row, made while the Reveal waits, is
    /// newer: its publication, the folder's load and a later rescan never seat
    /// the Reveal, so nothing opens over what the gesture opened. Main seated
    /// it at the next publication.</summary>
    [Theory]
    [InlineData("NewCanvas")]
    [InlineData("NewNoteWithACaveat")]
    [InlineData("DuplicateWithACaveat")]
    [InlineData("GraphNoteLanded")]
    [InlineData("CanvasNoteLanded")]
    [InlineData("EnterOnARow")]
    public void ARowlessGestureDropsAnOlderWaitingReveal(string gesture) => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        _ = LandOnSelected(host, "a.md", []);
        var log = new RevealLog();
        host.Sidebar.OpenTargetRequested += (_, request) => log.Opened.Add((request.Path, request.FocusEditor));
        host.HoldTreeWork();
        host.Sidebar.SelectPathFromSurface("docs/n.md");

        switch (gesture)
        {
            case "NewCanvas":
                host.Sidebar.CreateCanvasCommand.Execute(null);
                break;
            case "NewNoteWithACaveat":
                WithAfterWriteFault("Untitled", () => host.Sidebar.CreateNoteCommand.Execute(null));
                break;
            case "DuplicateWithACaveat":
                WithAfterWriteFault("a copy", host.Sidebar.DuplicateSelected);
                break;
            case "GraphNoteLanded":
                Assert.IsType<NoteCreateResult.Landed>(((ISurfaceNoteCreator)host.Sidebar).TryCreateNote("g.md", "# G\n"));
                host.Sidebar.NoteLanded("g.md");
                break;
            case "CanvasNoteLanded":
                _ = host.Sidebar.TryCreateNote("c.md", "# C\n");
                host.Sidebar.NoteLanded("c.md", null);
                break;
            default:
                host.Press(Key.Enter);
                break;
        }

        host.ReleaseTreeWork();
        SettleLoads(host);
        RescanAndRefresh(host);

        AssertNeverSeated(host, "docs/n.md", log);
    });

    /// <summary>A Reveal asked in a mutation's own turn, before its
    /// publication, is the newer intent: the revealed row ends selected on both
    /// halves with the keys, in one move, and the mutation's row does not take
    /// the selection back. Main let the older mutation's row win (a listed
    /// target) or lost the Reveal with its folder collapsed (an unlisted
    /// one).</summary>
    [Theory]
    [InlineData("duplicate", "b.md")]
    [InlineData("duplicate", "docs/n.md")]
    [InlineData("rename", "b.md")]
    [InlineData("rename", "docs/n.md")]
    public void ARevealAskedWhileAMutationPublishesWins(string mutation, string target) => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        _ = LandOnSelected(host, "a.md", []);
        RevealLog log = KeysOutsideFiles(host);

        string mutated = Mutate(host, mutation);
        host.Sidebar.SelectPathFromSurface(target);
        SettleLoads(host);

        AssertSeatedOnce(host, target, log);
        Assert.False(Row(host, mutated).IsSelected, $"{mutated} took the selection back");
    });

    /// <summary>The reader selects another row in a mutation's own turn: the
    /// publication leaves the reader's row selected with the keys. Main
    /// re-seated the mutation's row and took the keys back.</summary>
    [Theory]
    [InlineData("duplicate")]
    [InlineData("rename")]
    public void TheReadersOwnSelectionWinsOverAPendingMutationsRow(string mutation) => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        _ = LandOnSelected(host, "a.md", []);

        string mutated = Mutate(host, mutation);
        FileTreeNodeViewModel reader = Row(host, "b.md");
        ReaderFocuses(host, reader);
        SettleLoads(host);

        Assert.Same(reader, host.Sidebar.SelectedNode);
        Assert.True(reader.IsSelected, "the reader's row lost its selection");
        TreeViewItem container = ContainerOf(host, reader);
        Assert.True(container.IsKeyboardFocused, $"the keys are on {Keyboard.FocusedElement}");
        Assert.Same(container, Assert.Single(host.FocusChanges));
        Assert.False(Row(host, mutated).IsSelected, $"{mutated} took the selection back");
    });

    /// <summary>The reader collapses the folder the Reveal is waiting on: the
    /// wait ends. Expanded again by hand and published, nothing seats.</summary>
    [Fact]
    public void ACollapseEndsAWaitingReveal() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        host.Sidebar.SelectPathFromSurface("docs/n.md");

        FileTreeNodeViewModel docs = Row(host, "docs");
        docs.IsExpanded = false;
        docs.IsExpanded = true;
        host.ReleaseTreeWork();
        SettleLoads(host);
        host.Sidebar.Refresh();
        Settle(host);

        AssertNeverSeated(host, "docs/n.md", log);
        Assert.Empty(host.FocusChanges);
    });

    /// <summary>A refused close cancels every folder load, and nothing lists
    /// the folder after it: the wait ends, so a refresh minutes later seats
    /// nothing. Main seated it there.</summary>
    [Fact]
    public void ACancelThatListsNothingEndsAWaitingReveal() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        host.Sidebar.SelectPathFromSurface("docs/n.md");

        host.Sidebar.CancelChildExpansions();
        host.ReleaseTreeWork();
        SettleLoads(host);
        host.Sidebar.Refresh();
        Settle(host);

        AssertNeverSeated(host, "docs/n.md", log);
        Assert.Empty(host.FocusChanges);
        Assert.Empty(log.Opened);
    });

    /// <summary>The folder cannot be listed: the wait ends with the folder's
    /// own failure, and a later refresh seats nothing. Main seated it
    /// there.</summary>
    [Fact]
    public void ARealLoadFailureEndsAWaitingReveal() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.FailNextTreeWork();

        host.Sidebar.SelectPathFromSurface("docs/n.md");
        FileTreeNodeViewModel docs = Row(host, "docs");
        Assert.True(PumpedDispatcher.PumpUntil(() => docs.ChildLoadFailed), "premise: docs's load did not fail");
        SettleLoads(host);
        host.Sidebar.Refresh();
        Settle(host);

        AssertNeverSeated(host, "docs/n.md", log);
        Assert.Empty(log.Opened);
    });

    /// <summary>A Reveal of a note that does not exist ends when its folder
    /// lists without it: a note written there later and published by a rescan
    /// is not selected or opened. Main seated it then.</summary>
    [Fact]
    public void ARevealOfAMissingNoteEndsWhenItsFolderLists() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);

        host.Sidebar.SelectPathFromSurface("docs/missing.md");
        SettleLoads(host);
        File.WriteAllText(Path.Combine(host.Root, "docs", "missing.md"), "# Missing\n");
        RescanAndRefresh(host);

        AssertNeverSeated(host, "docs/missing.md", log);
        Assert.Empty(log.Opened);
    });

    /// <summary>A guard: a note a surface just created is listed by the
    /// refresh its landing started, so a Reveal of it in that turn waits for
    /// that publication and seats there, though its folder is already listed
    /// without it.</summary>
    [Fact]
    public void ARevealOfANoteAPendingRefreshWillListSeatsAtIt() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        ExpandAndList(host, Row(host, "docs"));
        RevealLog log = KeysOutsideFiles(host);

        Assert.IsType<NoteCreateResult.Landed>(((ISurfaceNoteCreator)host.Sidebar).TryCreateNote("docs/g.md", "# G\n"));
        host.Sidebar.NoteLanded("docs/g.md");
        host.Sidebar.SelectPathFromSurface("docs/g.md");
        SettleLoads(host);

        FileTreeNodeViewModel row = Row(host, "docs/g.md");
        Assert.Same(row, host.Sidebar.SelectedNode);
        Assert.True(row.IsSelected, "the created note's node does not hold the selection");
        Assert.Same(row, host.Tree.SelectedItem);
        Assert.Contains(("docs/g.md", false), log.Opened);
    });

    /// <summary>A guard: a Reveal asked before the tree's first publication
    /// seats at it.</summary>
    [Fact]
    public void ARevealBeforeTheFirstPublicationSeatsAtIt() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize(beforeFirstPublication: sidebar =>
        {
            Assert.Empty(sidebar.RootNodes);
            sidebar.SelectPathFromSurface("a.md");
        });
        Settle(host);

        FileTreeNodeViewModel row = Row(host, "a.md");
        Assert.Same(row, host.Sidebar.SelectedNode);
        Assert.True(row.IsSelected, "a.md's node does not hold the selection");
        Assert.Same(row, host.Tree.SelectedItem);
    });

    /// <summary>A whole-tree refresh started in the Reveal's own turn cancels
    /// the folder's load and lists the folder itself: the Reveal seats at its
    /// publication, the keys on the row in one move.</summary>
    [Fact]
    public void AWholeTreeRefreshInTheRevealsTurnSeatsIt() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);

        host.Sidebar.SelectPathFromSurface("docs/n.md");
        host.Sidebar.Refresh();
        SettleLoads(host);

        AssertSeatedOnce(host, "docs/n.md", log);
    });

    /// <summary>A surface a watcher can republish keeps the reader's element
    /// and focus: after the Reveal seats, a rescan keeps the revealed row's
    /// element and container and the keys on it, and says and opens
    /// nothing.</summary>
    [Fact]
    public void ARevealedRowKeepsItsElementAndTheKeysThroughARescan() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.Sidebar.SelectPathFromSurface("docs/n.md");
        SettleLoads(host);
        AssertSeatedOnce(host, "docs/n.md", log);
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);
        host.ForgetFocusChanges();
        host.Announced.Clear();
        log.Opened.Clear();

        RescanAndRefresh(host);

        Assert.True(ReferenceEquals(held["docs/n.md"], ShownRowElements(tree)["docs/n.md"]), "the rescan gave the revealed row a new element");
        AssertTheSameContainers(host, heldContainers, ["docs/n.md"]);
        Assert.True(ContainerOf(host, Row(host, "docs/n.md")).IsKeyboardFocused, $"the keys left the row for {Keyboard.FocusedElement}");
        Assert.Empty(host.FocusChanges);
        Assert.Empty(host.Announced);
        Assert.Empty(log.Opened);
    });

    /// <summary>A second Reveal replaces one still waiting: the listed note
    /// seats at once with the keys, and the first note's folder load seats
    /// nothing.</summary>
    [Fact]
    public void ASecondRevealReplacesAWaitingOne() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();

        host.Sidebar.SelectPathFromSurface("docs/n.md");
        host.Sidebar.SelectPathFromSurface("b.md");
        host.ReleaseTreeWork();
        SettleLoads(host);

        AssertSeatedOnce(host, "b.md", log);
        AssertNeverSeated(host, "docs/n.md", log);
    });

    /// <summary>A guard: a trash drops a Reveal asked before its gesture, at
    /// the gesture. The Reveal's folder lists during the trash's work, and the
    /// Reveal never seats.</summary>
    [Fact]
    public void ATrashDropsARevealAskedBeforeItsGesture() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        _ = LandOnSelected(host, "a.md", []);
        var log = new RevealLog();
        host.Sidebar.OpenTargetRequested += (_, request) => log.Opened.Add((request.Path, request.FocusEditor));
        host.HoldTreeWork();
        host.Sidebar.SelectPathFromSurface("docs/n.md");
        host.Sidebar.TrashStagedForTesting = _ =>
        {
            host.ReleaseTreeWork();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Row(host, "docs").HasLoadedChildren),
                "premise: docs never listed during the trash's work");
        };

        host.Sidebar.DeleteCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => host.Sidebar.TrashCompletion.IsCompleted), "the trash never finished");
        SettleLoads(host);

        Assert.False(File.Exists(Path.Combine(host.Root, "a.md")), "premise: the trash left the note");
        AssertNeverSeated(host, "docs/n.md", log);
    });

    // ---- helpers --------------------------------------------------------

    /// <summary>What a Reveal opened, with whether the editor took the
    /// keys.</summary>
    private sealed class RevealLog
    {
        public List<(string Path, bool FocusEditor)> Opened { get; } = [];
    }

    private static void SeedRevealVault(Host host) => SeedNotes(
        host,
        ("a.md", DateTime.Now),
        ("b.md", DateTime.Now),
        ("docs/n.md", DateTime.Now),
        ("docs/other.md", DateTime.Now),
        ("deep/mid/n2.md", DateTime.Now),
        ("deep/sibling.md", DateTime.Now));

    /// <summary>The keys on the button above the pane, outside Files, the
    /// focus log and the announcements forgotten, and the opens from here
    /// recorded.</summary>
    private static RevealLog KeysOutsideFiles(Host host)
    {
        var log = new RevealLog();
        host.Sidebar.OpenTargetRequested += (_, request) => log.Opened.Add((request.Path, request.FocusEditor));
        Assert.True(host.Above.Focus());
        PumpedDispatcher.Drain();
        host.ForgetFocusChanges();
        host.Announced.Clear();
        return log;
    }

    /// <summary>The reader puts the keys on <paramref name="row"/>, which
    /// selects itself as a row does when it takes the keys.</summary>
    private static void ReaderFocuses(Host host, FileTreeNodeViewModel row)
    {
        host.Pane.UpdateLayout();
        Assert.True(ContainerOf(host, row).Focus(), $"the reader's keys did not reach {row.Path}'s row");
        PumpedDispatcher.Drain();
    }

    /// <summary>Every folder load and the tree's latest publication, settled
    /// round by round, since one load can start the next.</summary>
    private static void SettleLoads(Host host)
    {
        for (int round = 0; round < 4; round++)
        {
            PumpedDispatcher.PumpUntilDrained(host.Sidebar.ChildExpansionCompletion);
            Settle(host);
        }
    }

    /// <summary>A rename to <c>aa.md</c> or a duplicate of the selected
    /// <c>a.md</c>, answering the path of the row it names.</summary>
    private static string Mutate(Host host, string mutation)
    {
        if (mutation == "rename")
        {
            host.Sidebar.MutationName = "aa.md";
            Assert.True(host.Sidebar.TryRenameSelected());
            return "aa.md";
        }

        host.Sidebar.DuplicateSelected();
        return "a copy.md";
    }

    private static void WithAfterWriteFault(string trigger, Action create)
    {
        using (EnvFaultLock.Acquire())
        {
            Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_AFTER_WRITE", trigger);
            try
            {
                create();
            }
            finally
            {
                Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_AFTER_WRITE", null);
            }
        }
    }

    private static FileTreeNodeViewModel Row(Host host, string path) =>
        EveryRow(host.Sidebar.RootNodes).Single(node => node.Path == path && !node.IsPlaceholder);

    private static bool IsSelectionLine(A11yEvent said) =>
        said is A11yEvent.RowSelected or A11yEvent.TreeFolderSelected;

    private static List<string> SelectionLines(Host host) =>
        [.. host.Announced.Where(IsSelectionLine).Select(said => SlateUniffiMethods.A11yRender(said).Text)];

    /// <summary>The note at <paramref name="path"/> is selected on both halves,
    /// its container holds the keys after exactly one focus change, it opened
    /// once without the editor's focus, and its selection was said
    /// once.</summary>
    private static void AssertSeatedOnce(Host host, string path, RevealLog log)
    {
        FileTreeNodeViewModel row = Row(host, path);
        Assert.Same(row, host.Sidebar.SelectedNode);
        Assert.True(row.IsSelected, $"{path}'s node does not hold the selection");
        Assert.Same(row, host.Tree.SelectedItem);
        TreeViewItem container = ContainerOf(host, row);
        Assert.True(container.IsSelected, $"{path}'s container is not selected");
        Assert.True(
            container.IsKeyboardFocused,
            $"the keys are on {Keyboard.FocusedElement} ({(Keyboard.FocusedElement as System.Windows.FrameworkElement)?.DataContext}), not on {path}'s row");
        Assert.True(
            host.FocusChanges.Count == 1 && ReferenceEquals(host.FocusChanges[0], container),
            $"the keys moved {host.FocusChanges.Count} times: "
            + string.Join(" -> ", host.FocusChanges.Select(focus => (focus as System.Windows.FrameworkElement)?.DataContext?.ToString() ?? focus.GetType().Name)));
        Assert.Equal([(path, false)], log.Opened);
        Assert.Equal([$"Selected: {Path.GetFileName(path)}"], SelectionLines(host));
    }

    /// <summary>The note at <paramref name="path"/> was never selected on
    /// either half, never opened, never said, and the keys never reached its
    /// row.</summary>
    private static void AssertNeverSeated(Host host, string path, RevealLog log)
    {
        FileTreeNodeViewModel? row = EveryRow(host.Sidebar.RootNodes).FirstOrDefault(node => node.Path == path && !node.IsPlaceholder);
        Assert.False(row?.IsSelected == true, $"{path}'s node holds the selection");
        Assert.NotEqual(path, host.Sidebar.SelectedNode?.Path);
        Assert.NotEqual(path, (host.Tree.SelectedItem as FileTreeNodeViewModel)?.Path);
        Assert.DoesNotContain(log.Opened, open => open.Path == path);
        Assert.DoesNotContain(SelectionLines(host), line => line == $"Selected: {Path.GetFileName(path)}");
        Assert.DoesNotContain(host.FocusChanges, focus => focus is TreeViewItem { DataContext: FileTreeNodeViewModel node } && node.Path == path);
    }
}
