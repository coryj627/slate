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
    [InlineData("ShortcutOpen")]
    [InlineData("HistoryBack")]
    public void ARowlessGestureDropsAnOlderWaitingReveal(string gesture) => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        if (gesture == "HistoryBack")
        {
            // Two notes in the sidebar's history, so Back has one to open.
            _ = LandOnSelected(host, "b.md", []);
        }

        _ = LandOnSelected(host, "a.md", []);
        if (gesture == "ShortcutOpen")
        {
            host.Sidebar.AddShortcutCommand.Execute(null);
            Assert.Contains(host.Sidebar.Shortcuts, shortcut => shortcut.Path == "a.md");
        }

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
            case "ShortcutOpen":
                host.Sidebar.OpenShortcut(1);
                break;
            case "HistoryBack":
                Assert.True(host.Sidebar.HistoryBackCommand.CanExecute(null), "premise: the history has nothing to go back to");
                host.Sidebar.HistoryBackCommand.Execute(null);
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

    /// <summary>The same with the keys still on the mutated row, in the tree: a
    /// mutation's row that seated after the newer Reveal would take the keys on
    /// its way, so the keys never reach it, and the Reveal's row ends holding
    /// them on both halves. The rename's row leaves the tree under the keys,
    /// which land on its neighbour first; the duplicate's original stays, so
    /// its keys move once.</summary>
    [Theory]
    [InlineData("duplicate")]
    [InlineData("rename")]
    public void ARevealAskedWhileAMutationPublishesWinsWithTheKeysOnTheMutatedRow(string mutation) => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        _ = LandOnSelected(host, "a.md", []);
        var log = new RevealLog();
        host.Sidebar.OpenTargetRequested += (_, request) => log.Opened.Add((request.Path, request.FocusEditor));

        string mutated = Mutate(host, mutation);
        host.Sidebar.SelectPathFromSurface("docs/n.md");
        SettleLoads(host);

        FileTreeNodeViewModel row = Row(host, "docs/n.md");
        Assert.Same(row, host.Sidebar.SelectedNode);
        Assert.True(row.IsSelected, "docs/n.md's node does not hold the selection");
        Assert.Same(row, host.Tree.SelectedItem);
        TreeViewItem container = ContainerOf(host, row);
        Assert.True(container.IsKeyboardFocused, $"the keys are on {Keyboard.FocusedElement}, not on docs/n.md's row");
        Assert.DoesNotContain(host.FocusChanges, focus => focus is TreeViewItem { DataContext: FileTreeNodeViewModel node } && node.Path == mutated);
        Assert.Same(container, host.FocusChanges[^1]);
        Assert.True(host.FocusChanges.Count <= (mutation == "rename" ? 2 : 1), $"the keys moved {host.FocusChanges.Count} times");
        Assert.False(Row(host, mutated).IsSelected, $"{mutated} took the selection back");
        Assert.Equal([("docs/n.md", false)], log.Opened);
        Assert.Equal(["Selected: n.md"], SelectionLines(host));
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

    /// <summary>A filter typed, or a tag applied, while the Reveal waits is the
    /// reader's newer gesture: the wait ends, so the folder's load neither
    /// clears that filter nor takes the keys out of it.</summary>
    [Theory]
    [InlineData("text")]
    [InlineData("tag")]
    public void AFilterTypedWhileARevealWaitsEndsIt(string filter) => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        File.WriteAllText(Path.Combine(host.Root, "tagged.md"), "# Tagged\n\n#alpha\n");
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        host.Sidebar.SelectPathFromSurface("docs/n.md");

        if (filter == "text")
        {
            host.Sidebar.FilterText = "b";
        }
        else
        {
            host.Sidebar.ActivateTag("alpha");
        }

        string filterText = host.Sidebar.FilterText;
        string? scopeTag = host.Sidebar.ScopeTag;
        host.ReleaseTreeWork();
        SettleLoads(host);
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.FilterCompletion);
        Settle(host);

        Assert.True(host.Sidebar.IsFilterActive, "the filter was cleared");
        Assert.Equal(filterText, host.Sidebar.FilterText);
        Assert.Equal(scopeTag, host.Sidebar.ScopeTag);
        Assert.DoesNotContain(host.Announced, said => said is A11yEvent.SidebarFilterCleared);
        AssertNeverSeated(host, "docs/n.md", log);
        Assert.Empty(host.FocusChanges);
    });

    /// <summary>A filter keystroke while a create's publication is pending is
    /// the reader's newer gesture, and it names no row (Codex round 1): the
    /// create's rename arm drops, so the publication neither selects the new
    /// note nor pulls the keys out of the filter field into the name
    /// field.</summary>
    [Fact]
    public void AFilterKeystrokeDuringACreatesPublicationDropsItsRenameArm() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        bool renameArmed = false;
        host.Sidebar.InlineRenameRequested += () => renameArmed = true;
        host.HoldTreeWork();
        host.Sidebar.CreateNoteCommand.Execute(null);
        Assert.True(File.Exists(Path.Combine(host.Root, "Untitled.md")), "premise: the create wrote no note");
        PumpedDispatcher.Drain();
        Assert.False(renameArmed, "premise: the rename was armed before the publication");

        Assert.True(host.FilterField.Focus(), "premise: the filter field refused the keys");
        PumpedDispatcher.Drain();
        host.FilterField.Text = "b";
        Assert.Equal("b", host.Sidebar.FilterText);
        host.ReleaseTreeWork();
        SettleLoads(host);
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.FilterCompletion);
        Settle(host);

        Assert.False(renameArmed, "the create's rename was armed after the filter keystroke");
        Assert.Same(host.FilterField, Keyboard.FocusedElement);
        Assert.NotEqual("Untitled.md", host.Sidebar.SelectedNode?.Path);
        Assert.Equal("b", host.Sidebar.FilterText);
    });

    /// <summary>The red team's minor 5: a late seat's silent arm into a date
    /// group the reader collapsed. A surface's landed note waits for the
    /// refresh that lists it, with the keys moved off the gesture's place
    /// (the button above the pane to the one beside it). The refresh seats it
    /// silently: the group opens, the row holds the selection on both halves,
    /// and nothing moves the keys, opens or is said.</summary>
    [Fact]
    public void ALateSilentSeatOpensTheCollapsedDateGroupHoldingItsRow() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        UseDateGroups(host);
        FileTreeNodeViewModel today = GroupHeader(host, "Today");
        today.IsExpanded = false;
        Settle(host);
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        Assert.IsType<NoteCreateResult.Landed>(((ISurfaceNoteCreator)host.Sidebar).TryCreateNote("g.md", "# G\n"));
        host.Sidebar.NoteLanded("g.md");
        host.Sidebar.SelectPathFromSurface("g.md");
        Assert.True(host.Beside.Focus(), "premise: the button beside the pane refused the keys");
        PumpedDispatcher.Drain();
        host.ForgetFocusChanges();
        host.Announced.Clear();

        host.ReleaseTreeWork();
        SettleLoads(host);

        Assert.Same(today, GroupHeader(host, "Today"));
        Assert.True(today.IsExpanded, "the silent seat left the date group collapsed");
        FileTreeNodeViewModel row = Row(host, "g.md");
        Assert.Same(row, host.Sidebar.SelectedNode);
        Assert.True(row.IsSelected, "the row's node does not hold the selection");
        Assert.True(ContainerOf(host, row).IsSelected, "the row's container is not selected");
        Assert.True(host.Beside.IsKeyboardFocused, "the keys left the button beside the pane");
        Assert.Empty(host.FocusChanges);
        Assert.Empty(log.Opened);
        Assert.Empty(host.Announced);
    });

    /// <summary>A filter keystroke while a rename's publication is pending
    /// keeps the rename's named row (the merge judge's minor 2; main kept it):
    /// the publication selects the renamed row silently, nothing opens or is
    /// said, and the keys stay in the filter field.</summary>
    [Fact]
    public void AFilterKeystrokeDuringARenamesPublicationKeepsItsRenamedRow() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        host.Sidebar.SelectedNode = Row(host, "a.md");
        Settle(host);
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        Assert.Equal("aa.md", Mutate(host, "rename"));

        Assert.True(host.FilterField.Focus(), "premise: the filter field refused the keys");
        PumpedDispatcher.Drain();
        host.FilterField.Text = "b";
        Assert.Equal("b", host.Sidebar.FilterText);
        host.ReleaseTreeWork();
        SettleLoads(host);
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.FilterCompletion);
        Settle(host);

        Assert.Equal("aa.md", host.Sidebar.SelectedNode?.Path);
        Assert.True(Row(host, "aa.md").IsSelected, "the renamed row's node is not selected");
        Assert.Same(host.FilterField, Keyboard.FocusedElement);
        Assert.Empty(SelectionLines(host));
        Assert.Empty(log.Opened);
        Assert.Equal("b", host.Sidebar.FilterText);
    });

    /// <summary>Expand loaded folders cancels the Reveal's own folder load and
    /// lists the folder itself: the wait goes on, and the Reveal seats when the
    /// run lists its row, while the run is still going, the keys on the row
    /// once.</summary>
    [Fact]
    public void ExpandLoadedFoldersKeepsAndSeatsAWaitingReveal() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        bool? expandingAtSeat = null;
        host.Sidebar.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FilesSidebarViewModel.SelectedNode) && host.Sidebar.SelectedNode?.Path == "docs/n.md")
            {
                expandingAtSeat = host.Sidebar.IsExpandingLoaded;
            }
        };
        host.HoldTreeWork();
        host.Sidebar.SelectPathFromSurface("docs/n.md");

        host.Sidebar.ExpandLoadedCommand.Execute(null);
        Assert.True(host.Sidebar.IsExpandingLoaded, "premise: Expand loaded folders is not running");
        host.ReleaseTreeWork();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.ExpandLoadedCompletion);
        SettleLoads(host);

        Assert.True(expandingAtSeat == true, $"the Reveal seated {(expandingAtSeat is null ? "never" : "after the run ended")}");
        AssertSeatedOnce(host, "docs/n.md", log);
    });

    /// <summary>The Reveal's first folder is listed by Expand loaded folders,
    /// which opens no folder it did not snapshot: when the run ends, the
    /// Reveal's step opens the next folder, and it seats when that lists.</summary>
    [Fact]
    public void ARevealUnderAFolderExpandLoadedListsExpandsTheNextOne() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        host.Sidebar.SelectPathFromSurface("deep/mid/n2.md");

        host.Sidebar.ExpandLoadedCommand.Execute(null);
        host.ReleaseTreeWork();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.ExpandLoadedCompletion);
        SettleLoads(host);

        Assert.True(Row(host, "deep/mid").IsExpanded, "deep/mid is not expanded");
        AssertSeatedOnce(host, "deep/mid/n2.md", log);
    });

    /// <summary>An Expand loaded folders run cancelled after it opened a folder
    /// and before it listed it leaves the folder open and unlisted. A Reveal
    /// through that folder loads it, and seats when it lists.</summary>
    [Fact]
    public void ARevealIntoAFolderACancelledExpandLoadedLeftOpenLoadsIt() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        ExpandAndList(host, Row(host, "deep"));
        FileTreeNodeViewModel mid = Row(host, "deep/mid");
        host.HoldTreeWork();
        host.Sidebar.ExpandLoadedCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => mid.IsExpanded), "premise: the run never opened deep/mid");

        // The reader opens another folder by hand, which cancels the run.
        Row(host, "docs").IsExpanded = true;
        Assert.True(PumpedDispatcher.PumpUntil(() => !host.Sidebar.IsExpandingLoaded), "premise: the run was not cancelled");
        host.ReleaseTreeWork();
        SettleLoads(host);
        Assert.True(mid.IsExpanded && mid.ChildLoadState == FileTreeChildLoadState.Unloaded, $"premise: deep/mid is expanded={mid.IsExpanded}, {mid.ChildLoadState}");
        RevealLog log = KeysOutsideFiles(host);

        host.Sidebar.SelectPathFromSurface("deep/mid/n2.md");
        SettleLoads(host);

        AssertSeatedOnce(host, "deep/mid/n2.md", log);
    });

    /// <summary>A folder whose load failed before the Reveal is asked is loaded
    /// once more by the Reveal: it seats when the folder lists, or, failing
    /// again, the wait ends with one failure line.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARevealIntoAFailedFolderRetriesItOnce(bool failsAgain) => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        host.FailNextTreeWork();
        host.Sidebar.SelectPathFromSurface("docs/n.md");
        FileTreeNodeViewModel docs = Row(host, "docs");
        Assert.True(PumpedDispatcher.PumpUntil(() => docs.ChildLoadFailed), "premise: docs's load did not fail");
        SettleLoads(host);
        RevealLog log = KeysOutsideFiles(host);
        if (failsAgain)
        {
            host.FailNextTreeWork();
        }

        host.Sidebar.SelectPathFromSurface("docs/n.md");
        SettleLoads(host);

        if (!failsAgain)
        {
            AssertSeatedOnce(host, "docs/n.md", log);
            return;
        }

        Assert.True(docs.ChildLoadFailed, "the retry did not fail");
        Assert.Single(host.Announced, said => SlateUniffiMethods.A11yRender(said).Text == "Could not load folder. Collapse and expand to retry.");
        host.Sidebar.Refresh();
        Settle(host);
        AssertNeverSeated(host, "docs/n.md", log);
        Assert.Empty(host.FocusChanges);
    });

    /// <summary>A close cancels the refresh a Reveal waits for, and that
    /// refresh never publishes: the wait ends (Call 15), so a later refresh
    /// seats nothing.</summary>
    [Fact]
    public void AClosesCancelledRefreshEndsAWaitingReveal() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        host.Sidebar.SelectPathFromSurface("docs/n.md");

        host.Sidebar.Refresh();
        Assert.True(host.Sidebar.CancelTreeRefresh(), "premise: no refresh was pending");
        host.ReleaseTreeWork();
        SettleLoads(host);
        host.Sidebar.Refresh();
        Settle(host);

        AssertNeverSeated(host, "docs/n.md", log);
        Assert.Empty(host.FocusChanges);
    });

    /// <summary>A close refused while Expand loaded folders runs cancels the
    /// run and every folder load (the lifecycle's order); nothing lists the
    /// folder for the Reveal after it, so the wait ends (Call 15), and a later
    /// refresh seats nothing.</summary>
    [Fact]
    public void ARefusedCloseDuringExpandLoadedEndsAWaitingReveal() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        host.Sidebar.SelectPathFromSurface("docs/n.md");
        host.Sidebar.ExpandLoadedCommand.Execute(null);
        Assert.True(host.Sidebar.IsExpandingLoaded, "premise: Expand loaded folders is not running");

        host.Sidebar.CancelExpandLoaded();
        host.Sidebar.CancelChildExpansions();
        host.ReleaseTreeWork();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.ExpandLoadedCompletion);
        SettleLoads(host);
        host.Sidebar.Refresh();
        Settle(host);

        AssertNeverSeated(host, "docs/n.md", log);
        Assert.Empty(log.Opened);
        Assert.Empty(host.FocusChanges);
    });

    /// <summary>A refresh (a watcher's, after any Slate write, or Refresh)
    /// cancels Expand loaded folders before the run lists the Reveal's folder.
    /// The folder lists once and the Reveal seats there, the keys on its row in
    /// one move, and the row stays: a folder load the cancelled run's end starts
    /// is the one that lists it, since the refresh leaves a folder whose own
    /// load runs to that load, so nothing lists the folder a second time under
    /// the seated row.</summary>
    [Fact]
    public void ARefreshDuringExpandLoadedSeatsTheRevealOnceAndKeepsItsRow() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        host.Sidebar.SelectPathFromSurface("docs/n.md");
        host.Sidebar.ExpandLoadedCommand.Execute(null);
        Assert.True(host.Sidebar.IsExpandingLoaded, "premise: Expand loaded folders is not running");

        host.Sidebar.Refresh();
        Assert.True(PumpedDispatcher.PumpUntil(() => !host.Sidebar.IsExpandingLoaded), "premise: the refresh did not end the run");
        host.ReleaseTreeWork();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.ExpandLoadedCompletion);
        SettleLoads(host);

        AssertSeatedOnce(host, "docs/n.md", log);
    });

    /// <summary>A folder the reader opens by hand cancels Expand loaded folders
    /// while the run holds the Reveal's own folder unlisted, and that other
    /// folder's load fails (Codex round 1). The run's end takes the Reveal's
    /// step, which lists its folder, so the Reveal seats at the time, the keys
    /// on its row once; a later good refresh seats nothing more.</summary>
    [Fact]
    public void AnUnrelatedFailingExpansionThatCancelsExpandLoadedLeavesTheRevealItsOwnWalk() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        FileTreeNodeViewModel docs = Row(host, "docs");
        host.HoldTreeWork();
        int started = host.TreeWorkStarted;
        host.Sidebar.SelectPathFromSurface("deep/mid/n2.md");
        Assert.True(PumpedDispatcher.PumpUntil(() => host.TreeWorkStarted > started), "premise: deep's load never reached the tree worker");

        host.Sidebar.ExpandLoadedCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => host.TreeWorkStarted > started + 1), "premise: the run's load never reached the tree worker");
        Assert.True(host.Sidebar.IsExpandingLoaded, "premise: Expand loaded folders is not running");

        // The reader opens docs by hand, which cancels the run, and docs's load
        // fails. Nothing is pumped until it has reached the worker, so docs's
        // load is the one that fails.
        host.FailNextTreeWork();
        docs.IsExpanded = true;
        Assert.True(SpinWait.SpinUntil(() => host.TreeWorkStarted > started + 2, TimeSpan.FromSeconds(10)), "premise: docs's load never reached the tree worker");
        Assert.True(PumpedDispatcher.PumpUntil(() => docs.ChildLoadFailed), "premise: docs's load did not fail");
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.ExpandLoadedCompletion);
        Assert.False(host.Sidebar.IsExpandingLoaded, "premise: the run was not cancelled");
        host.ReleaseTreeWork();
        SettleLoads(host);

        Assert.True(
            host.Sidebar.SelectedNode?.Path == "deep/mid/n2.md",
            $"the Reveal did not seat at the time: {host.Sidebar.SelectedNode?.Path ?? "nothing"} is selected, and deep is {Row(host, "deep").ChildLoadState}");
        AssertSeatedOnce(host, "deep/mid/n2.md", log);

        host.Sidebar.Refresh();
        Settle(host);
        SettleLoads(host);

        AssertSeatedOnce(host, "deep/mid/n2.md", log);
    });

    /// <summary>Expand loaded folders cancels the Reveal's own folder load to
    /// list the folder itself, and its read of that folder fails (Codex round
    /// 2): the wait ends there, as it does when the folder's own load fails.
    /// The run's end starts no load for the folder, and a later good refresh
    /// seats nothing: no focus change, nothing opened, nothing said
    /// selected.</summary>
    [Fact]
    public void AFailedExpandLoadedReadOfTheRevealsFolderEndsTheWait() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        int started = host.TreeWorkStarted;
        host.Sidebar.SelectPathFromSurface("docs/n.md");
        Assert.True(PumpedDispatcher.PumpUntil(() => host.TreeWorkStarted > started), "premise: docs's load never reached the tree worker");

        host.Sidebar.ExpandLoadedCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => host.TreeWorkStarted > started + 1), "premise: the run's first read never reached the tree worker");

        // The run's first read (deep's) is held; its next read, docs's, fails.
        host.FailNextTreeWork();
        host.ReleaseTreeWork();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.ExpandLoadedCompletion);
        Assert.True(Row(host, "deep").Children.Any(child => !child.IsPlaceholder), "premise: the run's first read was not deep's");
        Assert.Contains(host.Announced, said => SlateUniffiMethods.A11yRender(said).Text == "Could not expand loaded folders.");
        SettleLoads(host);
        host.Sidebar.Refresh();
        Settle(host);
        SettleLoads(host);

        AssertNeverSeated(host, "docs/n.md", log);
        Assert.Empty(log.Opened);
        Assert.Empty(host.FocusChanges);
        Assert.Empty(SelectionLines(host));
    });

    /// <summary>A whole-tree refresh cancels the Reveal's folder load to list
    /// the folder itself, then fails: nothing will list it, so the wait ends,
    /// and the next good refresh seats nothing.</summary>
    [Fact]
    public void ARefreshThatFailsEndsAWaitingReveal() => RunSta(() =>
    {
        using var host = new Host();
        SeedRevealVault(host);
        host.Initialize();
        RevealLog log = KeysOutsideFiles(host);
        host.HoldTreeWork();
        int started = host.TreeWorkStarted;
        host.Sidebar.SelectPathFromSurface("docs/n.md");
        Assert.True(PumpedDispatcher.PumpUntil(() => host.TreeWorkStarted > started), "premise: docs's load never reached the tree worker");

        host.FailNextTreeWork();
        host.Sidebar.Refresh();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.TreeRefreshCompletion);
        host.ReleaseTreeWork();
        SettleLoads(host);
        host.Sidebar.Refresh();
        Settle(host);

        AssertNeverSeated(host, "docs/n.md", log);
        Assert.Empty(host.FocusChanges);
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
