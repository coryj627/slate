// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-6 (#1522, #1515): every publication of the Files tree keeps the
/// rows it did not change — a date group's header and its rows too, an
/// organic publication's (after Slate's own mutation) too — and a selected row
/// the publication removed or rebuilt never takes the tree with it. A row that
/// is gone hands the keys to the nearest row that survived, focused without a
/// selection, with nothing opened and nothing said; a selected note the
/// publication only rebuilt (its date group changed, the grouping changed)
/// stays selected on its new row and takes the keys.
/// </summary>
public sealed partial class FilesRegionLandingTests
{
    /// <summary>Group by date on, notes in two buckets, and a note edited
    /// outside Slate that stays in its bucket: after the rescan every group
    /// header and every row under it is the element held before it, on screen
    /// with its own name. Until WB-6 no date group was ever kept, so every
    /// group and every row under it had a new element on every run
    /// (#1522).</summary>
    [Fact]
    public void ARescanKeepsEveryDateGroupAndEveryRowUnderIt() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("a.md", DateTime.Today.AddMinutes(3)), ("b.md", DateTime.Today.AddMinutes(1)), ("o.md", DaysBack(60)), ("p.md", DaysBack(61)));
        host.Initialize();
        UseDateGroups(host);
        Assert.Equal(["Today", "Older"], host.Sidebar.RootNodes.Select(node => node.Name));
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);
        Assert.Equal(["a.md", "b.md", "group:Older", "group:Today", "o.md", "p.md"], held.Keys.Order(StringComparer.Ordinal));

        RewriteOutsideSlate(host, "a.md", DateTime.Today.AddMinutes(4));
        RescanAndRefresh(host);

        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        foreach ((string key, AutomationPeer row) in held)
        {
            Assert.True(shown.TryGetValue(key, out AutomationPeer? now), $"the rescan dropped {key}'s row");
            Assert.True(ReferenceEquals(row, now), $"the rescan gave {key}'s row a new element");
            AssertReadsAsAShownRow(row, key);
        }
        AssertTheSameContainers(host, heldContainers, held.Keys);
    });

    /// <summary>A date group the reader collapsed stays collapsed through a
    /// rescan: the same header, still closed. A fresh group is always built
    /// open, so the published header's expansion is the truth; opened, it
    /// lists its rows as they are now — a note that joined it while it was
    /// closed among them.</summary>
    [Fact]
    public void ACollapsedDateGroupStaysCollapsedThroughARescan() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("a.md", DateTime.Today.AddMinutes(3)), ("b.md", DateTime.Today.AddMinutes(1)), ("o.md", DaysBack(60)), ("p.md", DaysBack(61)));
        host.Initialize();
        UseDateGroups(host);
        FileTreeNodeViewModel older = GroupHeader(host, "Older");
        older.IsExpanded = false;
        Settle(host);

        RewriteOutsideSlate(host, "a.md", DateTime.Today.AddMinutes(4));
        SeedNote(host, "q.md", DaysBack(62));
        RescanAndRefresh(host);

        Assert.Same(older, GroupHeader(host, "Older"));
        Assert.False(older.IsExpanded, "the rescan reopened the collapsed date group");
        older.IsExpanded = true;
        Settle(host);
        Assert.Equal(["o.md", "p.md", "q.md"], older.Children.Select(node => node.Path));
    });

    /// <summary>A note in "Older" rewritten now leaves "Older" for "Today":
    /// its old row is released, a fresh row for it is under "Today", and every
    /// other row and both headers keep their elements — a removal and an
    /// insertion, never the tree replaced.</summary>
    [Fact]
    public void ANoteWhoseDateBucketChangedLeavesOneGroupAndEntersAnother() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("a.md", DateTime.Today.AddMinutes(3)), ("b.md", DateTime.Today.AddMinutes(1)), ("o.md", DaysBack(60)), ("p.md", DaysBack(61)));
        host.Initialize();
        UseDateGroups(host);
        FileTreeNodeViewModel before = RowIn(GroupHeader(host, "Older"), "o.md");
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);

        RewriteOutsideSlate(host, "o.md", DateTime.Now);
        RescanAndRefresh(host);

        FileTreeNodeViewModel moved = RowIn(GroupHeader(host, "Today"), "o.md");
        Assert.NotSame(before, moved);
        Assert.DoesNotContain(GroupHeader(host, "Older").Children, node => node.Path == "o.md");
        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        foreach ((string key, AutomationPeer row) in held.Where(pair => pair.Key != "o.md"))
        {
            Assert.True(ReferenceEquals(row, shown[key]), $"the rescan gave {key}'s row a new element");
            AssertReadsAsAShownRow(row, key);
        }

        AssertReadsAsAShownRow(shown["o.md"], "o.md");
        AssertTheSameContainers(host, heldContainers, held.Keys.Where(key => key != "o.md"));
    });

    /// <summary>A note created through the sidebar — an organic publication,
    /// not a rescan's — keeps every row that was there: the same elements,
    /// on screen. The new note's row reads as a shown row, and the create's
    /// pending hand-off selects it and arms its inline rename. Until WB-6 an
    /// organic publication replaced the whole tree (#1515 case 1).</summary>
    [Fact]
    public void AnOrganicPublicationKeepsEveryRowItDidNotChange() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(nested: true);
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);
        bool renameArmed = false;
        host.Sidebar.InlineRenameRequested += () => renameArmed = true;

        host.Sidebar.CreateNoteCommand.Execute(null);
        Settle(host);

        FileTreeNodeViewModel created = host.Sidebar.RootNodes.Single(node => node.Path == "Untitled.md");
        Assert.Same(created, host.Sidebar.SelectedNode);
        Assert.True(renameArmed, "the create's inline rename was never armed");
        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        foreach ((string key, AutomationPeer row) in held)
        {
            Assert.True(ReferenceEquals(row, shown[key]), $"the create gave {key}'s row a new element");
            AssertReadsAsAShownRow(row, key);
        }

        AssertReadsAsAShownRow(shown["Untitled.md"], "Untitled.md");
        AssertTheSameContainers(host, heldContainers, held.Keys);
    });

    /// <summary>A rename through the sidebar names its successor
    /// (<c>RequestSelectionAt</c>), and the organic publication's pending
    /// selection seats it on the kept tree: the renamed row is selected, and
    /// every other row keeps its element.</summary>
    [Fact]
    public void ARenameCommitsPendingSelectionOnTheKeptTree() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(nested: true);
        host.Sidebar.SelectedNode = host.Sidebar.RootNodes.Single(node => node.Path == "note1.md");
        Settle(host);
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);

        host.Sidebar.MutationName = "renamed.md";
        Assert.True(host.Sidebar.TryRenameSelected());
        Settle(host);

        FileTreeNodeViewModel renamed = host.Sidebar.RootNodes.Single(node => node.Path == "renamed.md");
        Assert.Same(renamed, host.Sidebar.SelectedNode);
        Assert.True(renamed.IsSelected, "the renamed row's container was not selected");
        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        foreach ((string key, AutomationPeer row) in held.Where(pair => pair.Key != "note1.md"))
        {
            Assert.True(ReferenceEquals(row, shown[key]), $"the rename gave {key}'s row a new element");
        }
        AssertTheSameContainers(host, heldContainers, held.Keys.Where(key => key != "note1.md"));
    });

    /// <summary>#1515 case 3: the reader's selected note deleted outside
    /// Slate, then a rescan. The keys are on the next sibling's row — the
    /// same node, kept — focused without a selection: no container is
    /// selected, the tree has no selected item, the sidebar has no selected
    /// node, nothing opened and nothing said. Every other row keeps its
    /// element. Until WB-6 the removed selected row replaced the whole tree,
    /// because WPF's tree selects its first row — and opens its note — when
    /// the selected row's container leaves in place.</summary>
    [Fact]
    public void ARemovedSelectedRowLandsTheKeysOnItsNeighbourWithoutSelectingIt() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        _ = LandOnSelected(host, "note1.md", opened);
        FileTreeNodeViewModel next = host.Sidebar.RootNodes.Single(node => node.Path == "note2.md");
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);

        File.Delete(Path.Combine(host.Root, "note1.md"));
        RescanAndRefresh(host);

        AssertKeysOnUnselected(next);
        AssertNothingSelected(host);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        foreach ((string key, AutomationPeer row) in held.Where(pair => pair.Key != "note1.md"))
        {
            Assert.True(ReferenceEquals(row, shown[key]), $"the rescan gave {key}'s row a new element");
        }

        host.AssertKeysNeverOnAWindow();
        AssertTheSameContainers(host, heldContainers, held.Keys.Where(key => key != "note1.md"));
    });

    /// <summary>R-9 (a)'s "else": every row the reader's note had around it
    /// deleted outside Slate, and a new note created, then a rescan. No row
    /// survived, so the keys take the tree's own landing — its first row,
    /// unselected — through the same one resolver, on the publication that
    /// replaces the tree.</summary>
    [Fact]
    public void ARemovedSelectedRowWithNoSurvivorLandsOnTheTree() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        _ = LandOnSelected(host, "note1.md", opened);

        foreach (string note in Directory.EnumerateFiles(host.Root, "*.md"))
        {
            File.Delete(note);
        }

        File.WriteAllText(Path.Combine(host.Root, "new.md"), "# New\n");
        RescanAndRefresh(host);

        AssertKeysOnUnselected(Assert.Single(host.Sidebar.RootNodes));
        AssertNothingSelected(host);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertKeysNeverOnAWindow();
    });

    /// <summary>The reader's note inside an open folder deleted outside
    /// Slate: the keys are on its next sibling in the folder, the same kept
    /// node, unselected, and the folder, its other notes and the root's rows
    /// keep their elements. The survivor is read from the row's place before
    /// the publication moves anything: the reconcile removes the row from the
    /// kept folder's own rows, so a walk of the old tree afterwards finds
    /// nothing below the root.</summary>
    [Fact]
    public void ARemovedSelectedRowInsideAnOpenFolderLandsTheKeysOnItsNextSibling() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("notes/a.md", DateTime.Now), ("notes/b.md", DateTime.Now), ("notes/c.md", DateTime.Now), ("y.md", DateTime.Now), ("z.md", DateTime.Now));
        host.Initialize();
        FileTreeNodeViewModel notes = host.Sidebar.RootNodes.Single(node => node.Path == "notes");
        ExpandAndList(host, notes);
        FileTreeNodeViewModel next = RowIn(notes, "notes/c.md");
        var opened = new List<string>();
        _ = LandOnRow(host, RowIn(notes, "notes/b.md"), opened);
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);

        File.Delete(Path.Combine(host.Root, "notes", "b.md"));
        RescanAndRefresh(host);

        AssertKeysOnUnselected(next);
        AssertNothingSelected(host);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        foreach ((string key, AutomationPeer row) in held.Where(pair => pair.Key != "notes/b.md"))
        {
            Assert.True(ReferenceEquals(row, shown[key]), $"the rescan gave {key}'s row a new element");
        }
        AssertTheSameContainers(host, heldContainers, held.Keys.Where(key => key != "notes/b.md"));
    });

    /// <summary>R-9 (a)'s "else its parent": with the reader on a note in an
    /// open folder, every note in the folder deleted outside Slate (the
    /// folder stays): the keys are on the folder's row, unselected.</summary>
    [Fact]
    public void ARemovedSelectedRowWhoseOpenFolderEmptiedLandsTheKeysOnTheFolder() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("notes/a.md", DateTime.Now), ("notes/b.md", DateTime.Now), ("notes/c.md", DateTime.Now), ("y.md", DateTime.Now), ("z.md", DateTime.Now));
        File.WriteAllText(Path.Combine(host.Root, "notes", ".keep"), string.Empty);
        host.Initialize();
        FileTreeNodeViewModel notes = host.Sidebar.RootNodes.Single(node => node.Path == "notes");
        ExpandAndList(host, notes);
        var opened = new List<string>();
        _ = LandOnRow(host, RowIn(notes, "notes/b.md"), opened);

        foreach (string note in new[] { "a.md", "b.md", "c.md" })
        {
            File.Delete(Path.Combine(host.Root, "notes", note));
        }

        RescanAndRefresh(host);

        Assert.Same(notes, host.Sidebar.RootNodes.Single(node => node.Path == "notes"));
        AssertKeysOnUnselected(notes);
        AssertNothingSelected(host);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
    });

    /// <summary>Group by date on (newest first; Group by date sets that sort
    /// in place of a name sort): the reader's note in "Today" deleted
    /// outside Slate. The keys are on the next note in the kept group,
    /// unselected, and both headers keep their elements.</summary>
    [Fact]
    public void ARemovedSelectedRowUnderADateGroupLandsTheKeysOnItsNextSiblingInTheGroup() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("a.md", DateTime.Today.AddMinutes(3)), ("b.md", DateTime.Today.AddMinutes(2)), ("c.md", DateTime.Today.AddMinutes(1)), ("o.md", DaysBack(60)));
        host.Initialize();
        UseDateGroups(host);
        FileTreeNodeViewModel today = GroupHeader(host, "Today");
        Assert.Equal(["a.md", "b.md", "c.md"], today.Children.Select(node => node.Path));
        FileTreeNodeViewModel next = RowIn(today, "c.md");
        var opened = new List<string>();
        _ = LandOnRow(host, RowIn(today, "b.md"), opened);
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);

        File.Delete(Path.Combine(host.Root, "b.md"));
        RescanAndRefresh(host);

        AssertKeysOnUnselected(next);
        AssertNothingSelected(host);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        Assert.True(ReferenceEquals(held["group:Today"], shown["group:Today"]), "the rescan gave Today's header a new element");
        Assert.True(ReferenceEquals(held["group:Older"], shown["group:Older"]), "the rescan gave Older's header a new element");
        AssertTheSameContainers(host, heldContainers, ["group:Today", "group:Older", "a.md", "c.md", "o.md"]);
    });

    /// <summary>Group by date on, a collapsed folder at the root (folders sort
    /// before the groups), "Today" holding only the reader's note and "Older"
    /// one more: the note deleted outside Slate empties "Today", and the keys
    /// are on the "Older" header — the next row at the gone group's level —
    /// unselected. A surviving date group's header is a row that can take
    /// the keys.</summary>
    [Fact]
    public void ARemovedSelectedRowThatEmptiesItsDateGroupLandsTheKeysOnTheNextGroup() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("archive/x.md", DaysBack(90)), ("t.md", DateTime.Today.AddMinutes(1)), ("o.md", DaysBack(60)));
        host.Initialize();
        UseDateGroups(host);
        Assert.Equal(["archive", string.Empty, string.Empty], host.Sidebar.RootNodes.Select(node => node.Path));
        FileTreeNodeViewModel older = GroupHeader(host, "Older");
        var opened = new List<string>();
        _ = LandOnRow(host, RowIn(GroupHeader(host, "Today"), "t.md"), opened);

        File.Delete(Path.Combine(host.Root, "t.md"));
        RescanAndRefresh(host);

        Assert.DoesNotContain(host.Sidebar.RootNodes, node => node is { IsGroupHeader: true, Name: "Today" });
        AssertKeysOnUnselected(older);
        AssertNothingSelected(host);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
    });

    /// <summary>The reader's selected note in "Older" rewritten now, with a
    /// rename half typed in the name field: the rescan moves it to "Today"
    /// as a new row, and that row is the selection — the tree's selected
    /// item, its container selected and holding the keys, the only element
    /// the keys reached. The name field still holds what was typed, nothing
    /// opened, nothing was said, and every other row and both headers keep
    /// their elements. A row the reconcile did not keep is not always gone:
    /// a note of the same path the publication rebuilt is re-selected
    /// silently.</summary>
    [Fact]
    public void ASelectedNoteWhoseDateBucketChangedStaysSelectedOnItsNewRow() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("t.md", DateTime.Today.AddMinutes(1)), ("n.md", DaysBack(60)), ("o.md", DaysBack(61)));
        host.Initialize();
        UseDateGroups(host);
        FileTreeNodeViewModel before = RowIn(GroupHeader(host, "Older"), "n.md");
        var opened = new List<string>();
        _ = LandOnRow(host, before, opened);
        host.Sidebar.MutationName = "half-typed";
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);
        host.ForgetFocusChanges();

        RewriteOutsideSlate(host, "n.md", DateTime.Now);
        RescanAndRefresh(host);

        FileTreeNodeViewModel moved = RowIn(GroupHeader(host, "Today"), "n.md");
        Assert.NotSame(before, moved);
        Assert.Same(moved, host.Sidebar.SelectedNode);
        Assert.Same(moved, host.Tree.SelectedItem);
        TreeViewItem container = ContainerOf(host, moved);
        Assert.True(container.IsSelected, "the new row's container is not selected");
        Assert.Same(container, Keyboard.FocusedElement);
        Assert.All(host.FocusChanges, focus => Assert.Same(container, focus));
        Assert.Equal("half-typed", host.Sidebar.MutationName);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        foreach (string key in new[] { "t.md", "o.md", "group:Today", "group:Older" })
        {
            Assert.True(ReferenceEquals(held[key], shown[key]), $"the rescan gave {key}'s row a new element");
        }
        AssertTheSameContainers(host, heldContainers, ["t.md", "o.md", "group:Today", "group:Older"]);
    });

    /// <summary>Group by date turned on under the reader's selected note —
    /// an organic publication, which now keeps rows — moves the note from the
    /// root into "Today". It stays the selection on its new row, which holds
    /// the keys and is the only element they reached; nothing opens and no
    /// selection is announced (the setter's own sort sentence may be), and
    /// the folder at the root keeps its element.</summary>
    [Fact]
    public void TurningGroupByDateOnKeepsTheSelectedNoteSelected() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("archive/x.md", DaysBack(90)), ("a.md", DateTime.Today.AddMinutes(3)), ("b.md", DateTime.Today.AddMinutes(2)), ("c.md", DateTime.Today.AddMinutes(1)));
        host.Initialize();
        host.Sidebar.SortMode = SidebarSortMode.ModifiedNewest;
        Settle(host);
        var opened = new List<string>();
        _ = LandOnSelected(host, "b.md", opened);
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);

        host.Sidebar.GroupByDate = true;
        Settle(host);

        FileTreeNodeViewModel moved = RowIn(GroupHeader(host, "Today"), "b.md");
        Assert.Same(moved, host.Sidebar.SelectedNode);
        TreeViewItem container = ContainerOf(host, moved);
        Assert.True(container.IsSelected, "the note's new row is not selected");
        Assert.Same(container, Keyboard.FocusedElement);
        Assert.All(host.FocusChanges, focus => Assert.Same(container, focus));
        Assert.Empty(opened);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.RowSelected or A11yEvent.TreeFolderSelected);
        Assert.True(ReferenceEquals(held["archive"], ShownRowElements(tree)["archive"]), "the folder's row got a new element");
        AssertTheSameContainers(host, heldContainers, ["archive"]);
    });

    /// <summary>The reader's selected note in "Today" dated sixty days back
    /// outside Slate while "Older" is collapsed: the note's new row is under
    /// the collapsed "Older" and is the selection, "Older" is the same header
    /// and still collapsed, and the keys are on its header — the row the
    /// reader sees — unselected. The tree has no selected item to show,
    /// nothing opened and nothing was said; opened, the group shows the note
    /// selected.</summary>
    [Fact]
    public void ASelectedNoteThatMovesIntoACollapsedDateGroupStaysSelectedWithTheKeysOnTheGroup() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("m.md", DateTime.Today.AddMinutes(2)), ("n.md", DateTime.Today.AddMinutes(1)), ("o.md", DaysBack(60)));
        host.Initialize();
        UseDateGroups(host);
        FileTreeNodeViewModel older = GroupHeader(host, "Older");
        older.IsExpanded = false;
        Settle(host);
        var opened = new List<string>();
        _ = LandOnRow(host, RowIn(GroupHeader(host, "Today"), "n.md"), opened);

        RewriteOutsideSlate(host, "n.md", DaysBack(60));
        RescanAndRefresh(host);

        Assert.Same(older, GroupHeader(host, "Older"));
        Assert.False(older.IsExpanded, "the rescan opened the collapsed group");
        FileTreeNodeViewModel moved = RowIn(older, "n.md");
        Assert.Same(moved, host.Sidebar.SelectedNode);
        TreeViewItem header = ContainerOf(host, older);
        Assert.Same(header, Keyboard.FocusedElement);
        Assert.False(header.IsSelected, "the keys' landing selected the group's header");
        Assert.Null(host.Tree.SelectedItem);
        Assert.True(moved.IsSelected, "the note's new row does not hold the selection");
        Assert.Empty(opened);
        Assert.Empty(host.Announced);

        older.IsExpanded = true;
        Settle(host);
        Assert.Same(moved, host.Tree.SelectedItem);
        Assert.True(ContainerOf(host, moved).IsSelected, "opened, the group does not show the note selected");
        Assert.Empty(opened);
    });

    /// <summary>Codex round 2: the hidden selection left on a note the
    /// publication moved into a collapsed group lets go when the reader
    /// selects another row. Expanding the group later shows the note
    /// unselected: its container never comes back selected, so WPF never
    /// re-selects it, and nothing reopens or is announced.</summary>
    [Fact]
    public void AHiddenSelectedNoteLetsGoWhenAnotherRowIsSelected() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("m.md", DateTime.Today.AddMinutes(2)), ("n.md", DateTime.Today.AddMinutes(1)), ("o.md", DaysBack(60)));
        host.Initialize();
        UseDateGroups(host);
        FileTreeNodeViewModel older = GroupHeader(host, "Older");
        older.IsExpanded = false;
        Settle(host);
        var opened = new List<string>();
        _ = LandOnRow(host, RowIn(GroupHeader(host, "Today"), "n.md"), opened);
        RewriteOutsideSlate(host, "n.md", DaysBack(60));
        RescanAndRefresh(host);
        FileTreeNodeViewModel hidden = RowIn(older, "n.md");
        Assert.True(hidden.IsSelected, "premise: the moved note does not hold the hidden selection");

        var later = new List<string>();
        FileTreeNodeViewModel other = LandOnRow(host, RowIn(GroupHeader(host, "Today"), "m.md"), later);
        Assert.False(hidden.IsSelected, "the hidden note kept its selection when another row took it");
        older.IsExpanded = true;
        Settle(host);

        Assert.Same(other, host.Sidebar.SelectedNode);
        Assert.Same(other, host.Tree.SelectedItem);
        Assert.False(ContainerOf(host, hidden).IsSelected, "the expanded group shows the abandoned note selected");
        Assert.Empty(later);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.RowSelected or A11yEvent.TreeFolderSelected);
    });

    /// <summary>Codex round 2: the tree's selected row deleted outside Slate
    /// while a list's row — another note, which survives — holds the
    /// selection, with the keys on the tree's row. The keys land on the
    /// tree row's nearest survivor in one move, unselected, and nothing opens
    /// or is announced; the list's selection is re-bound as before.</summary>
    [Fact]
    public void ARemovedTreeRowBesideASurvivingListSelectionLandsTheKeysOnItsNeighbour() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("a.md", DateTime.Now), ("c.md", DateTime.Now), ("z.md", DateTime.Now));
        host.Initialize();
        var opened = new List<string>();
        FileTreeNodeViewModel treeRow = LandOnSelected(host, "a.md", opened);
        FileTreeNodeViewModel next = host.Sidebar.RootNodes.Single(node => node.Path == "c.md");
        HoldAListSelection(host, "z.md");
        Assert.True(treeRow.IsSelected, "premise: the tree's row let go of its selection");
        Assert.Same(treeRow, FocusedNode());
        opened.Clear();
        host.Announced.Clear();
        host.ForgetFocusChanges();

        File.Delete(Path.Combine(host.Root, "a.md"));
        RescanAndRefresh(host);

        AssertKeysOnUnselected(next);
        Assert.Single(host.FocusChanges);
        Assert.Empty(opened);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.RowSelected or A11yEvent.TreeFolderSelected);
    });

    /// <summary>Codex round 2: the tree's selected row rebuilt — its date
    /// group changed — while a list's row holds the selection, with the keys
    /// on the tree's row. The keys land on the rebuilt row, not on a
    /// neighbour as if it were gone, and it is not selected: the selection
    /// is the list's; nothing opens or is announced.</summary>
    [Fact]
    public void ARebuiltTreeRowBesideAListSelectionTakesTheKeysOnItsNewRow() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("t.md", DateTime.Today.AddMinutes(1)), ("n.md", DaysBack(60)), ("o.md", DaysBack(61)));
        host.Initialize();
        UseDateGroups(host);
        var opened = new List<string>();
        _ = LandOnRow(host, RowIn(GroupHeader(host, "Older"), "n.md"), opened);
        HoldAListSelection(host, "t.md");
        opened.Clear();
        host.Announced.Clear();

        RewriteOutsideSlate(host, "n.md", DateTime.Now);
        RescanAndRefresh(host);

        AssertKeysOnUnselected(RowIn(GroupHeader(host, "Today"), "n.md"));
        Assert.Empty(opened);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.RowSelected or A11yEvent.TreeFolderSelected);
    });

    /// <summary>Codex round 1 (high): the reader's selected note deleted
    /// through the sidebar. The delete lets go of the selection before its
    /// refresh, so the tree's own selected row is what leaves: it is
    /// deselected before its container does, and the keys land on its
    /// neighbour, unselected. WPF's tree selects no first row, nothing opens
    /// and no selection is announced; every other row keeps its
    /// element.</summary>
    [Fact]
    public void ADeleteThroughTheSidebarLandsTheKeysOnTheNeighbourAndOpensNothing() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        _ = LandOnSelected(host, "note1.md", opened);
        FileTreeNodeViewModel next = host.Sidebar.RootNodes.Single(node => node.Path == "note2.md");
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);

        host.Sidebar.DeleteCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => host.Sidebar.TrashCompletion.IsCompleted), "the delete never finished");
        Settle(host);

        Assert.False(File.Exists(Path.Combine(host.Root, "note1.md")), "premise: the delete left the note");
        AssertKeysOnUnselected(next);
        AssertNothingSelected(host);
        Assert.Empty(opened);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.RowSelected or A11yEvent.TreeFolderSelected);
        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        foreach ((string key, AutomationPeer row) in held.Where(pair => pair.Key != "note1.md"))
        {
            Assert.True(ReferenceEquals(row, shown[key]), $"the delete gave {key}'s row a new element");
        }
        AssertTheSameContainers(host, heldContainers, held.Keys.Where(key => key != "note1.md"));
    });

    /// <summary>Codex round 1 (high, its sibling): the tree's selected row is
    /// a folder while the dual pane holds the selection — a note of that
    /// folder — and the folder is deleted outside Slate. The tree's row is
    /// deselected before its container leaves, so WPF selects no first row:
    /// nothing opens, no selection is announced, and the tree has no
    /// selected item.</summary>
    [Fact]
    public void ATreeRowSelectedBesideTheDualPanesSelectionLeavesWithoutOpeningAnything() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("docs/a.md", DateTime.Now), ("docs/b.md", DateTime.Now), ("y.md", DateTime.Now), ("z.md", DateTime.Now));
        host.Initialize();
        host.Sidebar.IsDualPaneEnabled = true;
        var opened = new List<string>();
        FileTreeNodeViewModel docs = LandOnRow(host, host.Sidebar.RootNodes.Single(node => node.Path == "docs"), opened);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => host.Sidebar.DualPaneFiles.Any(node => node.Path == "docs/a.md")),
            "premise: the dual pane never listed the folder");
        host.Sidebar.SelectedNode = host.Sidebar.DualPaneFiles.Single(node => node.Path == "docs/a.md");
        Settle(host);
        Assert.True(docs.IsSelected, "premise: the tree's row let go of its selection");
        opened.Clear();
        host.Announced.Clear();

        Directory.Delete(Path.Combine(host.Root, "docs"), recursive: true);
        RescanAndRefresh(host);

        Assert.Null(host.Tree.SelectedItem);
        Assert.DoesNotContain(ShownRowContainers(host), pair => pair.Value.IsSelected);
        Assert.Empty(opened);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.RowSelected or A11yEvent.TreeFolderSelected);
    });

    /// <summary>The red team's M1: the tree's selected row is not the
    /// sidebar's selection — a list's row holds it (the filter results, the
    /// dual pane; a node outside the tree stands in for one) — and the tree's
    /// row is edited outside Slate so that the sort would move it. It is
    /// anchored like the reader's row, so it stays: its container never
    /// leaves selected, WPF selects no first row, and nothing opens or is
    /// announced.</summary>
    [Fact]
    public void ATreeRowSelectedBesideAListsSelectionStaysThroughAMoveAndOpensNothing() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("d.md", DateTime.Today.AddMinutes(4)), ("c.md", DateTime.Today.AddMinutes(3)), ("b.md", DateTime.Today.AddMinutes(2)), ("a.md", DateTime.Today.AddMinutes(1)));
        host.Initialize();
        host.Sidebar.SortMode = SidebarSortMode.ModifiedNewest;
        Settle(host);
        Assert.Equal(["d.md", "c.md", "b.md", "a.md"], host.Sidebar.RootNodes.Select(node => node.Path));
        var opened = new List<string>();
        FileTreeNodeViewModel treeRow = LandOnSelected(host, "b.md", opened);
        HoldAListSelection(host, "elsewhere.md");
        Assert.True(treeRow.IsSelected, "premise: the tree's row let go of its selection");
        opened.Clear();
        host.Announced.Clear();

        RewriteOutsideSlate(host, "b.md", DateTime.Today.AddMinutes(5));
        RescanAndRefresh(host);

        Assert.Same(treeRow, host.Tree.SelectedItem);
        Assert.Empty(opened);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.RowSelected or A11yEvent.TreeFolderSelected);
    });

    /// <summary>Codex round 1: a note created through the sidebar from a
    /// selected row takes the selection on both halves — the tree's
    /// selected item, its node and its container — and the row it was
    /// created from, which the publication now keeps, lets go of
    /// it.</summary>
    [Fact]
    public void CreatingFromASelectedRowMovesBothHalvesOfTheSelectionToTheNewRow() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        FileTreeNodeViewModel from = LandOnSelected(host, "note1.md", opened);

        host.Sidebar.CreateNoteCommand.Execute(null);
        Settle(host);

        FileTreeNodeViewModel created = host.Sidebar.RootNodes.Single(node => node.Path == "Untitled.md");
        Assert.Same(created, host.Sidebar.SelectedNode);
        Assert.Same(created, host.Tree.SelectedItem);
        Assert.True(created.IsSelected, "the new row's node is not selected");
        Assert.True(ContainerOf(host, created).IsSelected, "the new row's container is not selected");
        Assert.False(from.IsSelected, "the row the note was created from still holds the selection");
        Assert.False(ContainerOf(host, from).IsSelected, "the row the note was created from still shows selected");
    });

    /// <summary>Codex round 1: a note duplicated from "Older" while "Today"
    /// is collapsed. The copy, modified now, joins the collapsed "Today",
    /// and the duplicate's named successor takes the selection — the copy's
    /// node, the original's let go of — and the keys land on the "Today"
    /// header holding it, which stays collapsed and unselected.</summary>
    [Fact]
    public void ADuplicateIntoACollapsedDateGroupLandsTheKeysOnTheGroupHoldingIt() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("t.md", DateTime.Today.AddMinutes(1)), ("o.md", DaysBack(60)), ("p.md", DaysBack(61)));
        host.Initialize();
        UseDateGroups(host);
        FileTreeNodeViewModel today = GroupHeader(host, "Today");
        today.IsExpanded = false;
        Settle(host);
        var opened = new List<string>();
        FileTreeNodeViewModel original = LandOnRow(host, RowIn(GroupHeader(host, "Older"), "o.md"), opened);

        host.Sidebar.DuplicateCommand.Execute(null);
        Settle(host);

        Assert.Same(today, GroupHeader(host, "Today"));
        Assert.False(today.IsExpanded, "the duplicate opened the collapsed group");
        FileTreeNodeViewModel copy = Assert.Single(today.Children, node => node.Path != "t.md");
        Assert.Same(copy, host.Sidebar.SelectedNode);
        Assert.True(copy.IsSelected, "the copy's node does not hold the selection");
        Assert.False(original.IsSelected, "the original still holds the selection");
        TreeViewItem header = ContainerOf(host, today);
        Assert.Same(header, Keyboard.FocusedElement);
        Assert.False(header.IsSelected, "the keys' landing selected the group's header");

        // Codex round 2's sibling: another row selected, the hidden copy lets
        // go, and the group opened later shows it unselected.
        var later = new List<string>();
        _ = LandOnRow(host, RowIn(GroupHeader(host, "Older"), "p.md"), later);
        Assert.False(copy.IsSelected, "the hidden copy kept its selection when another row took it");
        today.IsExpanded = true;
        Settle(host);
        Assert.False(ContainerOf(host, copy).IsSelected, "the expanded group shows the copy selected");
        Assert.Empty(later);
    });

    /// <summary>The reader's selected note renamed through the sidebar: the
    /// rename names its successor, the publication's pending selection seats
    /// it, and the keys go to the renamed row in one move — the focus log
    /// names only that row's container, never a neighbour's on the way. The
    /// notes beside it keep their elements.</summary>
    [Fact]
    public void RenamingTheSelectedRowLandsTheKeysOnTheRenamedRowInOneFocusChange() => RunSta(() =>
    {
        using var host = new Host();
        SeedNotes(host, ("a.md", DateTime.Now), ("b.md", DateTime.Now), ("c.md", DateTime.Now));
        host.Initialize();
        var opened = new List<string>();
        _ = LandOnSelected(host, "b.md", opened);
        AutomationPeer tree = TreePeer(host);
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> heldContainers = ShownRowContainers(host);

        host.Sidebar.MutationName = "bb.md";
        Assert.True(host.Sidebar.TryRenameSelected());
        Settle(host);

        FileTreeNodeViewModel renamed = host.Sidebar.RootNodes.Single(node => node.Path == "bb.md");
        Assert.Same(renamed, host.Sidebar.SelectedNode);
        TreeViewItem container = ContainerOf(host, renamed);
        Assert.True(container.IsSelected, "the renamed row is not selected");
        Assert.Same(container, Keyboard.FocusedElement);
        Assert.NotEmpty(host.FocusChanges);
        Assert.All(host.FocusChanges, focus => Assert.Same(container, focus));
        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        Assert.True(ReferenceEquals(held["a.md"], shown["a.md"]), "the rename gave a.md's row a new element");
        Assert.True(ReferenceEquals(held["c.md"], shown["c.md"]), "the rename gave c.md's row a new element");
        AssertTheSameContainers(host, heldContainers, ["a.md", "c.md"]);
    });

    // ---- helpers --------------------------------------------------------

    /// <summary>Replaces the fixture's notes with <paramref name="notes"/>,
    /// each modified at its time (a note's date group reads it).</summary>
    private static void SeedNotes(Host host, params (string Path, DateTime Modified)[] notes)
    {
        foreach (string note in Directory.EnumerateFiles(host.Root, "note*.md"))
        {
            File.Delete(note);
        }

        foreach ((string path, DateTime modified) in notes)
        {
            string full = Path.Combine(host.Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, $"# {Path.GetFileNameWithoutExtension(path)}\n");
            File.SetLastWriteTime(full, modified);
        }
    }

    /// <summary>A list's row holds the selection, as when the reader selects
    /// a dual-pane row: a row of the dual pane's own listing, at
    /// <paramref name="path"/>, set as the sidebar's selection the way the
    /// list's selection handler sets it. The tree keeps its own selected row
    /// and the keys.</summary>
    private static void HoldAListSelection(Host host, string path)
    {
        var listRow = new FileTreeNodeViewModel(host.Sidebar, path, Path.GetFileName(path), isDirectory: false, level: 0, hasChildren: false);
        host.Sidebar.DualPaneFiles.Add(listRow);
        host.Sidebar.SelectedNode = listRow;
        Settle(host);
    }

    /// <summary>Every row of <paramref name="keys"/> still has the container
    /// it had: not merely the same node and peer, which outlive a
    /// container WPF recycles or regenerates.</summary>
    private static void AssertTheSameContainers(Host host, Dictionary<string, TreeViewItem> held, IEnumerable<string> keys)
    {
        Dictionary<string, TreeViewItem> now = ShownRowContainers(host);
        foreach (string key in keys)
        {
            Assert.True(now.TryGetValue(key, out TreeViewItem? container), $"{key}'s row has no container now");
            Assert.True(ReferenceEquals(held[key], container), $"{key}'s row has a new container");
        }
    }

    /// <summary>One more note, modified at <paramref name="modified"/>.</summary>
    private static void SeedNote(Host host, string path, DateTime modified)
    {
        string full = Path.Combine(host.Root, path);
        File.WriteAllText(full, $"# {Path.GetFileNameWithoutExtension(path)}\n");
        File.SetLastWriteTime(full, modified);
    }

    /// <summary>A note rewritten outside Slate, its modified time set to
    /// <paramref name="modified"/>.</summary>
    private static void RewriteOutsideSlate(Host host, string path, DateTime modified)
    {
        string full = Path.Combine(host.Root, path);
        File.WriteAllText(full, $"# {Path.GetFileNameWithoutExtension(path)}\n\nRewritten outside Slate.\n");
        File.SetLastWriteTime(full, modified);
    }

    private static DateTime DaysBack(int days) => DateTime.Now.AddDays(-days);

    /// <summary>Group by date on (it sets a newest-first sort in place of a
    /// name sort), published and laid out; its sort sentence forgotten.</summary>
    private static void UseDateGroups(Host host)
    {
        host.Sidebar.GroupByDate = true;
        Settle(host);
        Assert.Contains(host.Sidebar.RootNodes, node => node.IsGroupHeader);
        host.Announced.Clear();
    }

    /// <summary>The sidebar's latest publication, and the layout and
    /// landings it schedules.</summary>
    private static void Settle(Host host)
    {
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.TreeRefreshCompletion);
        for (int round = 0; round < 2; round++)
        {
            host.Pane.UpdateLayout();
            PumpedDispatcher.Drain();
        }
    }

    private static FileTreeNodeViewModel GroupHeader(Host host, string label) =>
        host.Sidebar.RootNodes.Single(node => node.IsGroupHeader && node.Name == label);

    private static FileTreeNodeViewModel RowIn(FileTreeNodeViewModel parent, string path) =>
        parent.Children.Single(node => node.Path == path);

    private static AutomationPeer TreePeer(Host host) =>
        Assert.IsAssignableFrom<AutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(host.Tree));

    /// <summary>The realized container of <paramref name="node"/>, reached
    /// through each ancestor's own container.</summary>
    private static TreeViewItem ContainerOf(Host host, FileTreeNodeViewModel node)
    {
        var path = new List<FileTreeNodeViewModel>();
        Assert.True(PathTo(host.Sidebar.RootNodes, node, path), $"{node} is not in the tree");
        ItemsControl owner = host.Tree;
        TreeViewItem? container = null;
        foreach (FileTreeNodeViewModel step in path)
        {
            container = owner.ItemContainerGenerator.ContainerFromItem(step) as TreeViewItem;
            Assert.True(container is not null, $"{step} has no container");
            owner = container;
        }

        return container!;

        static bool PathTo(IEnumerable<FileTreeNodeViewModel> level, FileTreeNodeViewModel target, List<FileTreeNodeViewModel> path)
        {
            foreach (FileTreeNodeViewModel row in level)
            {
                path.Add(row);
                if (ReferenceEquals(row, target) || PathTo(row.Children, target, path))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
            }

            return false;
        }
    }

    /// <summary>The keys are on <paramref name="row"/>'s container, which is
    /// not selected.</summary>
    private static void AssertKeysOnUnselected(FileTreeNodeViewModel row)
    {
        Assert.True(
            Keyboard.FocusedElement is TreeViewItem { DataContext: FileTreeNodeViewModel focused } && ReferenceEquals(focused, row),
            $"the keys are on {Keyboard.FocusedElement} ({(Keyboard.FocusedElement as FrameworkElement)?.DataContext}), not on {row}'s row");
        Assert.False(((TreeViewItem)Keyboard.FocusedElement).IsSelected, $"the landing selected {row}'s row");
    }

    /// <summary>Nothing is selected, on either half: the tree has no
    /// selected item, no container in it is selected, no node says it is,
    /// and the sidebar has no selected node.</summary>
    private static void AssertNothingSelected(Host host)
    {
        Assert.Null(host.Tree.SelectedItem);
        Assert.Null(host.Sidebar.SelectedNode);
        Assert.DoesNotContain(ShownRowContainers(host), pair => pair.Value.IsSelected);
        Assert.DoesNotContain(EveryRow(host.Sidebar.RootNodes), node => node.IsSelected);

        static IEnumerable<FileTreeNodeViewModel> EveryRow(IEnumerable<FileTreeNodeViewModel> level) =>
            level.SelectMany(node => EveryRow(node.Children).Prepend(node));
    }
}
