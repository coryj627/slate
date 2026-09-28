// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5 as the owner amended it; spec review round
/// 23): the Files region's landing, on the shipped Files pane. Nine landings
/// in the window were <c>FilesTree.Focus()</c> — the ring's, the Files
/// boundary's, a rename's, a mutation's restore, Move To's, the empty
/// editor's last resort (the launch landing) — and all of them, a restore
/// whose token is the tree, and the tree's own hand-on from Tab or a click,
/// now go through <see cref="MainWindow.LandOnFilesTree"/>: the selected
/// file's row; else the first row, UNSELECTED (the owner's
/// focus-without-select, which replaced codex round 5's filter-field
/// fallback). Never the bare tree, a populated container, and the landing
/// never selects: selecting a file opens it (OD-2). The pane is the real one
/// — built by the window's own XAML, bound to a sidebar over a real vault —
/// lifted into a window of its own with a button above it and one beside it,
/// so an arrow that leaves the region lands somewhere observable. Keys are
/// real presses through the input manager.
/// </summary>
public sealed class FilesRegionLandingTests
{
    /// <summary>The owner's focus-without-select: with nothing selected the
    /// landing is the tree's first row, focused WITHOUT selecting it — it
    /// selects nothing, opens nothing, says nothing, and the tree never holds
    /// the keys. (Codex round 5's filter-field stop is withdrawn.)</summary>
    [Fact]
    public void WithNothingSelectedTheFirstRowTakesTheKeysUnselected() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        Assert.True(host.Above.Focus());
        host.Announced.Clear();

        Assert.True(host.Shell.LandOnFilesTree());

        Assert.Same(host.Sidebar.RootNodes[0], FocusedNode());
        Assert.Null(host.Sidebar.SelectedNode);
        Assert.DoesNotContain(host.Sidebar.RootNodes, node => node.IsSelected);
        Assert.Null(host.Tree.SelectedItem);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();
    });

    /// <summary>The keys sent to the bare tree itself — where a click on its
    /// empty area sends them (TreeView.HandleMouseButtonDown), where Tab and a
    /// restore token captured on it send them — go on to the region's landing
    /// in the same focus change: the first row, unselected. Nothing is
    /// selected, opened or said, and the tree never holds them.</summary>
    [Fact]
    public void KeysSentToTheBareFilesTreeLandOnItsFirstRowUnselected() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        Assert.True(host.Above.Focus());
        host.ForgetFocusChanges();
        host.Announced.Clear();

        _ = host.Tree.Focus();
        PumpedDispatcher.Drain();

        Assert.Same(host.Sidebar.RootNodes[0], FocusedNode());
        Assert.Null(host.Sidebar.SelectedNode);
        Assert.DoesNotContain(host.Sidebar.RootNodes, node => node.IsSelected);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();

        // A restore whose token is the tree itself lands the same way.
        Assert.True(host.Above.Focus());
        Assert.True(host.Shell.TryFocus(host.Tree));
        Assert.Same(host.Sidebar.RootNodes[0], FocusedNode());
        Assert.Null(host.Sidebar.SelectedNode);
        Assert.Empty(opened);
        host.AssertTreeNeverFocused();
    });

    /// <summary>Codex round 5: the Tags tree's selection APPLIES a tag filter
    /// (R-3). The keys sent to the bare Tags tree — a restore's token, a click
    /// on its empty area — land on its selected tag, else its first tag
    /// UNSELECTED: no tag is applied and nothing is said.</summary>
    [Fact]
    public void KeysSentToTheBareTagsTreeApplyNoTag() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(tagged: true);
        host.Sidebar.ShowTags = true;
        host.Pane.UpdateLayout();
        TreeView tags = host.ElementWithId<TreeView>("SidebarTagTree");
        Assert.True(PumpedDispatcher.PumpUntil(() => tags.HasItems), "premise: the Tags tree never listed the fixture's tag.");
        host.Pane.UpdateLayout();
        Assert.True(host.Above.Focus());
        host.ForgetFocusChanges();
        host.Announced.Clear();

        Assert.True(host.Shell.TryFocus(tags));
        PumpedDispatcher.Drain();

        TreeViewItem row = Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement);
        Assert.Same(tags.ItemContainerGenerator.ContainerFromIndex(0), row);
        Assert.False(row.IsSelected, "the landing selected the first tag.");
        Assert.Null(tags.SelectedItem);
        Assert.False(host.Sidebar.IsFilterActive, "the landing applied a tag filter.");
        Assert.Empty(host.Announced);
        host.AssertNeverFocusedPopulated(tags);
    });

    /// <summary>The owner's S4 (the completeness sweep's G6): a restore whose
    /// token is a Tags ROW lands through the Tags tree's own landing. The row
    /// applied its tag when the reader chose it; Clear Sidebar Filter then
    /// released the tag, and the palette's restore focused the row — which
    /// selected it and applied the tag the reader had just cleared. Now it
    /// lands on the first tag, unselected: nothing applies, nothing is
    /// said.</summary>
    [Fact]
    public void ARestoreTokenOnAReleasedTagRowAppliesNothing() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(tagged: true);
        host.Sidebar.ShowTags = true;
        host.Pane.UpdateLayout();
        TreeView tags = host.ElementWithId<TreeView>("SidebarTagTree");
        Assert.True(PumpedDispatcher.PumpUntil(() => tags.HasItems), "premise: the Tags tree never listed the fixture's tag.");
        host.Pane.UpdateLayout();
        var row = Assert.IsAssignableFrom<TreeViewItem>(tags.ItemContainerGenerator.ContainerFromIndex(0));
        Assert.True(row.Focus());
        Assert.True(PumpedDispatcher.PumpUntil(() => host.Sidebar.IsFilterActive), "premise: choosing the tag applied no filter.");
        IInputElement token = Keyboard.FocusedElement;
        host.Sidebar.ClearFilterCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.False(host.Sidebar.IsFilterActive, "premise: the clear left the tag applied.");
        Assert.False(row.IsSelected, "premise: the clear did not release the tag's row.");
        Assert.True(host.Above.Focus());
        host.Announced.Clear();

        Assert.True(host.Shell.TryFocus(token));
        PumpedDispatcher.Drain();

        TreeViewItem landed = Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement);
        Assert.False(landed.IsSelected, "the restore selected a tag.");
        Assert.False(host.Sidebar.IsFilterActive, "the restore re-applied the tag the reader cleared.");
        Assert.Empty(host.Announced);
    });

    /// <summary>The owner's S4 (G6): a restore whose token is a Files row
    /// that is NOT the selection — a landing's unselected row, a recycled
    /// container — lands through the Files region's own landing: the
    /// selected row, else the first row unselected. The token's own focus
    /// selected its row, and a Files selection OPENS the note.</summary>
    [Fact]
    public void ARestoreTokenOnAnUnselectedFilesRowOpensNothing() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        FileTreeNodeViewModel second = host.Sidebar.RootNodes[1];
        var row = Assert.IsAssignableFrom<TreeViewItem>(host.Tree.ItemContainerGenerator.ContainerFromItem(second));
        Assert.True(LandingTreeViewItem.FocusUnselected(row));
        IInputElement token = Keyboard.FocusedElement;
        Assert.True(host.Above.Focus());
        host.Announced.Clear();

        Assert.True(host.Shell.TryFocus(token));
        PumpedDispatcher.Drain();

        Assert.Same(host.Sidebar.RootNodes[0], FocusedNode());
        Assert.False(second.IsSelected, "the restore selected the token's row.");
        Assert.Null(host.Sidebar.SelectedNode);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
    });

    [Fact]
    public void TheSelectedFilesRowTakesTheKeys() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        FileTreeNodeViewModel selected = host.Sidebar.RootNodes[^1];
        selected.IsSelected = true;
        host.Sidebar.SelectedNode = selected;
        host.Pane.UpdateLayout();
        Assert.True(host.Above.Focus());
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        host.Announced.Clear();

        Assert.True(host.Shell.LandOnFilesTree());

        Assert.Same(selected, FocusedNode());
        Assert.Same(selected, host.Sidebar.SelectedNode);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();
    });

    /// <summary>With PR 2 (R-2) a Files selection OPENS its note, fired on a
    /// selection CHANGE. The landing focuses only the sidebar's selected
    /// row; when its container has lost the tree's selection (a recycled
    /// container), taking the keys re-selects it, and the echo re-states
    /// the node already selected — no change, so nothing opens and nothing
    /// is said.</summary>
    [Fact]
    public void LandingOnTheSelectedRowOpensNothingEvenWhenItsContainerLostTheSelection() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        FileTreeNodeViewModel selected = host.Sidebar.RootNodes[^1];
        host.Sidebar.SelectedNode = selected;
        selected.IsSelected = false;
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();
        Assert.Same(selected, host.Sidebar.SelectedNode);
        Assert.Null(host.Tree.SelectedItem);
        Assert.True(host.Above.Focus());
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        host.Announced.Clear();

        Assert.True(host.Shell.LandOnFilesTree());
        PumpedDispatcher.Drain();

        Assert.Same(selected, FocusedNode());
        Assert.Same(selected, host.Tree.SelectedItem);
        Assert.Same(selected, host.Sidebar.SelectedNode);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();
    });

    /// <summary>Codex rounds 4-5: a selected file beneath a COLLAPSED folder
    /// has no row to land on. The landing must not change what is selected,
    /// expand the folder or open anything — F6 is navigation, not a
    /// selection — so the keys go to the first row, UNSELECTED, and the
    /// selection stays on the hidden note.</summary>
    [Fact]
    public void AHiddenSelectedFileLandsOnTheFirstRowAndKeepsItsSelection() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(nested: true);
        FileTreeNodeViewModel folder = Assert.Single(host.Sidebar.RootNodes, node => node.IsDirectory);
        folder.IsExpanded = true;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => folder.Children.Any(child => child is { IsPlaceholder: false, IsDirectory: false })),
            "premise: the folder's note never loaded.");
        FileTreeNodeViewModel inner = folder.Children.Single(child => child is { IsPlaceholder: false, IsDirectory: false });
        host.Pane.UpdateLayout();
        folder.IsExpanded = false;
        host.Pane.UpdateLayout();
        // Selected while its folder is collapsed — the sidebar's own route
        // when a filter result is chosen and the filter then cleared. (A
        // collapse through the tree moves WPF's selection to the folder, so
        // the collapse comes first.)
        inner.IsSelected = true;
        host.Sidebar.SelectedNode = inner;
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();
        Assert.True(host.Above.Focus());
        Assert.Same(inner, host.Sidebar.SelectedNode);
        Assert.False(folder.IsExpanded);
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        host.Announced.Clear();
        host.ForgetFocusChanges();

        Assert.True(host.Shell.LandOnFilesTree());
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();

        FileTreeNodeViewModel first = host.Sidebar.RootNodes[0];
        Assert.Same(first, FocusedNode());
        Assert.False(first.IsSelected, "the landing selected the first row.");
        Assert.Same(inner, host.Sidebar.SelectedNode);
        Assert.False(folder.IsSelected, "the landing selected the collapsed folder.");
        Assert.False(folder.IsExpanded, "the landing expanded the collapsed folder.");
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();
    });

    /// <summary>With the filter active the tree is replaced by the results
    /// and no row of it can take the keys: the region's stable stop, the
    /// filter field, does.</summary>
    [Fact]
    public void AFilesTreeTheFilterReplacedLandsOnTheFilterField() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        host.Sidebar.FilterText = "note";
        host.Pane.UpdateLayout();
        Assert.False(host.Tree.IsVisible);
        Assert.True(host.Above.Focus());

        Assert.True(host.Shell.LandOnFilesTree());

        Assert.Same(host.FilterField, Keyboard.FocusedElement);
        host.AssertTreeNeverFocused();
    });

    /// <summary>The arrow witness for the landing with nothing selected: from
    /// the first row, unselected — or, with the filter replacing the tree,
    /// from the filter field — every arrow keeps the keys in the region. An
    /// arrow on the row acts from there as always (Down shows the next note);
    /// none of them leaves.</summary>
    [Theory]
    [InlineData(Key.Left, false)]
    [InlineData(Key.Right, false)]
    [InlineData(Key.Up, false)]
    [InlineData(Key.Down, false)]
    [InlineData(Key.Left, true)]
    [InlineData(Key.Right, true)]
    [InlineData(Key.Up, true)]
    [InlineData(Key.Down, true)]
    public void FromTheFilesLandingEveryArrowStaysInTheRegion(Key key, bool filtered) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        if (filtered)
        {
            host.Sidebar.FilterText = "note";
            host.Pane.UpdateLayout();
        }

        Assert.True(host.Above.Focus());
        Assert.True(host.Shell.LandOnFilesTree());
        if (filtered)
        {
            Assert.Same(host.FilterField, Keyboard.FocusedElement);
        }
        else
        {
            Assert.Same(host.Sidebar.RootNodes[0], FocusedNode());
        }

        host.Press(key);

        Assert.True(
            filtered ? ReferenceEquals(host.FilterField, Keyboard.FocusedElement) : host.Tree.IsKeyboardFocusWithin,
            $"{key} took the keys out of the Files landing, to {Keyboard.FocusedElement}");
    });

    /// <summary>The arrow witness for the landing with a file selected: from
    /// the selected file's row each arrow keeps the keys in the tree — at
    /// the first row and a root (Up, Left) and at the last row and a leaf
    /// (Down, Right), where the tree has no row further to go. There the
    /// arrow reaches the row's own check box (the batch-trash mark), which
    /// is inside the row, never out of the region.</summary>
    [Theory]
    [InlineData(Key.Left, 0)]
    [InlineData(Key.Up, 0)]
    [InlineData(Key.Right, -1)]
    [InlineData(Key.Down, -1)]
    public void FromTheSelectedFilesRowEveryArrowStaysInTheRegion(Key key, int row) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        FileTreeNodeViewModel selected = row < 0 ? host.Sidebar.RootNodes[^1] : host.Sidebar.RootNodes[row];
        selected.IsSelected = true;
        host.Sidebar.SelectedNode = selected;
        host.Pane.UpdateLayout();
        Assert.True(host.Above.Focus());
        Assert.True(host.Shell.LandOnFilesTree());
        Assert.Same(selected, FocusedNode());

        host.Press(key);

        Assert.True(
            host.Tree.IsKeyboardFocusWithin,
            $"{key} took the keys out of the Files tree, to {Keyboard.FocusedElement}");
    });

    /// <summary>Codex round 5: the filter's result list is its own stop
    /// while it is empty (AR-6) — the ring's Files landing with the filter
    /// active and nothing found yet. Results published under the keys land
    /// them on the first result's row, unselected (a selection would open
    /// the note), never left on the bare populated list.</summary>
    [Fact]
    public void ResultsPublishedUnderTheKeysLandThemOnARow() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        var results = Assert.IsType<ListBox>(host.Shell.FindName("FilterResultsList"));
        host.Sidebar.FilterText = "zzz-nothing-matches";
        Assert.True(
            PumpedDispatcher.PumpUntil(() => host.Sidebar.IsFilterActive && results.IsVisible && !results.HasItems),
            "premise: the filter never showed its empty result list.");
        host.Pane.UpdateLayout();
        Assert.True(results.Focus());
        host.ForgetFocusChanges();
        host.Announced.Clear();

        host.Sidebar.FilterText = "note";
        Assert.True(PumpedDispatcher.PumpUntil(() => results.HasItems), "the results never published.");
        PumpedDispatcher.Drain();

        Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(results.ItemContainerGenerator.ContainerFromIndex(0), Keyboard.FocusedElement);
        Assert.Equal(-1, results.SelectedIndex);
        Assert.Empty(opened);
        host.AssertNeverFocusedPopulated(results);
    });

    /// <summary>
    /// W7-7 PR 4b (the completeness sweep's G7): Pin Note re-sorts the tree
    /// (FilesSidebarViewModel.Resort: its rows cleared and re-added) under
    /// the reader's row. WPF ejected the keys (the W5-4 red team measured
    /// the window), and Down then did nothing. They land on the selected note's row, once, without opening
    /// anything; the bare tree never holds them.
    /// </summary>
    [Fact]
    public void PinNoteRebuildsTheTreeUnderTheKeysAndTheyStayOnTheRow() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        FileTreeNodeViewModel note = host.Sidebar.RootNodes.Last(node => !node.IsDirectory);
        note.IsSelected = true;
        host.Sidebar.SelectedNode = note;
        host.Pane.UpdateLayout();
        var row = Assert.IsAssignableFrom<TreeViewItem>(host.Tree.ItemContainerGenerator.ContainerFromItem(note));
        Assert.True(row.Focus());
        PumpedDispatcher.Drain();
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        host.ForgetFocusChanges();

        host.Sidebar.PinCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.Same(note, FocusedNode());
        Assert.Empty(opened);
        host.AssertTreeNeverFocused();
        Assert.True(Keyboard.FocusedElement is TreeViewItem, $"the keys ended on {Keyboard.FocusedElement}");
    });

    /// <summary>
    /// The sweep's G7, the Tags tree: every refresh — a save reaches one —
    /// rebuilt the tags with nothing selected under the reader's applied tag,
    /// and the keys left the tree. The applied tag is selected again in the
    /// rebuilt tree, the keys land on its row, the filter is untouched and
    /// nothing is re-applied.
    /// </summary>
    [Fact]
    public void ATagsRefreshUnderTheKeysKeepsTheAppliedTagsRow() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(tagged: true);
        host.Sidebar.ShowTags = true;
        host.Pane.UpdateLayout();
        TreeView tags = host.ElementWithId<TreeView>("SidebarTagTree");
        Assert.True(PumpedDispatcher.PumpUntil(() => tags.HasItems), "premise: the Tags tree never listed the fixture's tag.");
        host.Pane.UpdateLayout();
        var row = Assert.IsAssignableFrom<TreeViewItem>(tags.ItemContainerGenerator.ContainerFromIndex(0));
        Assert.True(row.Focus());
        Assert.True(PumpedDispatcher.PumpUntil(() => host.Sidebar.IsFilterActive), "premise: choosing the tag applied no filter.");
        string filter = host.Sidebar.FilterText;
        string applied = Assert.IsType<SidebarTagViewModel>(row.DataContext).Full;
        host.ForgetFocusChanges();

        host.Sidebar.Refresh();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.TreeRefreshCompletion);
        PumpedDispatcher.Drain();

        var landed = Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement);
        Assert.Equal(applied, Assert.IsType<SidebarTagViewModel>(landed.DataContext).Full);
        Assert.True(landed.IsSelected, "the applied tag's row lost its selection in the rebuild");
        Assert.NotSame(row, landed);
        Assert.Equal(filter, host.Sidebar.FilterText);
        Assert.True(host.Sidebar.IsFilterActive);
        host.AssertNeverFocusedPopulated(tags);
    });

    /// <summary>
    /// Codex PR 4b r1 F4 (G7): a NESTED applied tag — alpha/beta, under alpha.
    /// The rebuilt tree came back collapsed, so the re-selected tag had no
    /// row, and the landing fell to the first root, unselected. The rebuild
    /// keeps the reader's expansion and opens the applied tag's ancestors:
    /// the keys land on alpha/beta's own row, selected, the filter untouched.
    /// </summary>
    [Fact]
    public void ANestedTagsRefreshUnderTheKeysKeepsTheAppliedTagsRow() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(tagged: true, nestedTag: true);
        TreeView tags = ShowTags(host);
        TreeViewItem nested = ExpandToNestedTag(host, tags);
        Assert.True(nested.Focus());
        Assert.True(PumpedDispatcher.PumpUntil(() => host.Sidebar.IsFilterActive), "premise: choosing the nested tag applied no filter.");
        string filter = host.Sidebar.FilterText;
        Assert.Equal("alpha/beta", Assert.IsType<SidebarTagViewModel>(nested.DataContext).Full);
        host.ForgetFocusChanges();

        host.Sidebar.Refresh();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.TreeRefreshCompletion);
        PumpedDispatcher.Drain();

        var landed = Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement);
        var tag = Assert.IsType<SidebarTagViewModel>(landed.DataContext);
        Assert.Equal("alpha/beta", tag.Full);
        Assert.True(landed.IsSelected, "the nested tag's row lost its selection in the rebuild");
        Assert.Same(tag, tags.SelectedItem);
        Assert.Equal(filter, host.Sidebar.FilterText);
        host.AssertNeverFocusedPopulated(tags);
    });

    /// <summary>
    /// Codex PR 4b r1 F4's companion: a tag chosen AFTER a nested refresh is
    /// the tag the next refresh keeps. The nested tag re-selected with no row
    /// stayed selected in the model when the reader arrowed to another tag,
    /// and the next rebuild restored IT — a phantom selection — instead of
    /// the tag applied. One tag is ever selected, and it is the filter's.
    /// </summary>
    [Fact]
    public void ATagChosenAfterANestedRefreshIsTheTagTheNextRefreshKeeps() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(tagged: true, nestedTag: true);
        TreeView tags = ShowTags(host);
        TreeViewItem nested = ExpandToNestedTag(host, tags);
        Assert.True(nested.Focus());
        Assert.True(PumpedDispatcher.PumpUntil(() => host.Sidebar.IsFilterActive), "premise: choosing the nested tag applied no filter.");
        host.Sidebar.Refresh();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.TreeRefreshCompletion);
        PumpedDispatcher.Drain();

        string? chosen = null;
        for (int press = 0; press < 10 && chosen is null; press++)
        {
            host.Press(Key.Down);
            PumpedDispatcher.Drain();
            if ((Keyboard.FocusedElement as TreeViewItem)?.DataContext is SidebarTagViewModel { Depth: 0 } root
                && root.Full != "alpha")
            {
                chosen = root.Full;
            }
        }

        Assert.True(chosen is not null, "premise: Down never reached another root tag.");
        Assert.True(PumpedDispatcher.PumpUntil(() => host.Sidebar.FilterText.Contains(chosen!, StringComparison.Ordinal)), "premise: the chosen tag applied no filter.");
        string filter = host.Sidebar.FilterText;

        host.Sidebar.Refresh();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.TreeRefreshCompletion);
        PumpedDispatcher.Drain();

        var landed = Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement);
        Assert.Equal(chosen, Assert.IsType<SidebarTagViewModel>(landed.DataContext).Full);
        Assert.True(landed.IsSelected);
        Assert.Equal(filter, host.Sidebar.FilterText);
        Assert.Equal([chosen], SelectedTags(host.Sidebar.Tags));
    });

    private static TreeView ShowTags(Host host)
    {
        host.Sidebar.ShowTags = true;
        host.Pane.UpdateLayout();
        TreeView tags = host.ElementWithId<TreeView>("SidebarTagTree");
        Assert.True(PumpedDispatcher.PumpUntil(() => tags.HasItems), "premise: the Tags tree never listed the fixture's tags.");
        host.Pane.UpdateLayout();
        return tags;
    }

    /// <summary>The alpha row, expanded as the reader would (Right), and
    /// its nested alpha/beta row.</summary>
    private static TreeViewItem ExpandToNestedTag(Host host, TreeView tags)
    {
        SidebarTagViewModel alpha = host.Sidebar.Tags.Single(tag => tag.Full == "alpha");
        Assert.True(alpha.Children.Any(child => child.Full == "alpha/beta"), "premise: alpha/beta is not alpha's child.");
        var alphaRow = Assert.IsAssignableFrom<TreeViewItem>(tags.ItemContainerGenerator.ContainerFromItem(alpha));
        alphaRow.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();
        return Assert.IsAssignableFrom<TreeViewItem>(
            alphaRow.ItemContainerGenerator.ContainerFromItem(alpha.Children.Single(child => child.Full == "alpha/beta")));
    }

    private static List<string> SelectedTags(IEnumerable<SidebarTagViewModel> level) =>
    [
        .. level.SelectMany(tag => (tag.IsSelected ? [tag.Full] : Array.Empty<string>()).Concat(SelectedTags(tag.Children))),
    ];

    /// <summary>Codex PR 4 round 6 (the repro's R6_5x): Tab — WPF's own
    /// traversal, no landing of ours — used to rest on a bare POPULATED tree
    /// when nothing in it was selected; the Files tree and the Tags tree are
    /// Tab stops themselves. Now Tab lands on the first row, unselected: no
    /// note opens, no tag filter applies, nothing is said.</summary>
    [Theory]
    [InlineData("FilesTree")]
    [InlineData("SidebarTagTree")]
    public void TabIntoAPopulatedTreeWithNothingSelectedLandsOnItsFirstRowUnselected(string treeId) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        host.Sidebar.ShowTags = true;
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();
        TreeView tree = host.ElementWithId<TreeView>(treeId);
        Assert.True(tree.HasItems && tree.SelectedItem is null, $"premise: {treeId} is empty or has a selection.");
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        Assert.True(host.FilterField.Focus());
        host.ForgetFocusChanges();
        host.Announced.Clear();
        bool reached = false;
        for (int press = 0; press < 40 && !reached; press++)
        {
            host.Press(Key.Tab);
            reached = tree.IsKeyboardFocusWithin;
        }

        Assert.True(reached, $"premise: Tab never reached {treeId}.");
        TreeViewItem row = Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement);
        Assert.Same(tree.ItemContainerGenerator.ContainerFromIndex(0), row);
        Assert.False(row.IsSelected, $"Tab selected {treeId}'s first row.");
        Assert.Null(tree.SelectedItem);
        Assert.False(host.Sidebar.IsFilterActive, "Tab applied a tag filter.");
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertNeverFocusedPopulated(tree);
    });

    public static TheoryData<string, Key> EmptyFilesSelectors()
    {
        var data = new TheoryData<string, Key>();
        foreach (string selector in new[] { "SidebarDualPane", "SidebarTagTree", "SidebarShortcuts" })
        {
            foreach (Key key in new[] { Key.Right, Key.Left, Key.Up, Key.Down })
            {
                data.Add(selector, key);
            }
        }

        return data;
    }

    /// <summary>Codex PR 4 round 6 high 5 (the repro's R6_5, inverted; the
    /// owner's S1): PR 2's selectors sat outside the Files tree's Contained
    /// group, and the Files pane was no boundary. Each, EMPTY — its own stop
    /// (AR-6): the dual pane before a folder with files is chosen, the Tags
    /// tree over a vault with no tags, the shortcuts list with none added —
    /// keeps every arrow in the Files region now that the pane contains
    /// them.</summary>
    [Theory]
    [MemberData(nameof(EmptyFilesSelectors))]
    public void AnEmptyFilesSelectorKeepsItsArrowsInTheRegion(string selector, Key key) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(untagged: true);
        ItemsControl target = ShowEmpty(host, selector);
        Assert.True(target.Focus(), $"premise: the empty {selector} refused the keys.");
        Assert.Same(target, Keyboard.FocusedElement);

        host.Press(key);

        Assert.True(
            host.Pane.IsKeyboardFocusWithin,
            $"{key} from the empty {selector} left the Files region, to {Keyboard.FocusedElement}");
    });

    private static ItemsControl ShowEmpty(Host host, string selector)
    {
        switch (selector)
        {
            case "SidebarDualPane":
                host.Sidebar.IsDualPaneEnabled = true;
                host.Sidebar.SelectedNode = host.Sidebar.RootNodes.Single(node => node.IsDirectory && node.Name == "empty");
                PumpedDispatcher.Drain();
                break;
            case "SidebarTagTree":
                host.Sidebar.ShowTags = true;
                break;
            default:
                host.ElementWithId<Expander>("SidebarShortcutsActions").IsExpanded = true;
                break;
        }

        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();
        ItemsControl target = host.ElementWithId<ItemsControl>(selector);
        Assert.True(target.IsVisible, $"premise: {selector} is not shown.");
        Assert.False(target.HasItems, $"premise: {selector} is not empty.");
        return target;
    }

    private static FileTreeNodeViewModel? FocusedNode() =>
        (Keyboard.FocusedElement as TreeViewItem)?.DataContext as FileTreeNodeViewModel;

    private static void SetState(object target, string name, object? value) =>
        (target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing state property: {name}"))
        .SetValue(target, value);

    /// <summary>The shipped window's Files pane over a three-note vault,
    /// lifted into a shown window of its own. No Application and no shown
    /// MainWindow (the MoveToFocusTests fixture's reasons): its constructor
    /// still builds the shipped XAML and wires the pane's bindings.</summary>
    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(3, "files-landing");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly List<IInputElement> _focusChanges = [];
        private readonly List<IInputElement> _populatedFocus = [];
        private VaultSession? _session;
        private Window? _window;

        public MainWindow Shell { get; private set; } = null!;

        public FilesSidebarViewModel Sidebar { get; private set; } = null!;

        public FrameworkElement Pane { get; private set; } = null!;

        public TreeView Tree { get; private set; } = null!;

        public TextBox FilterField { get; private set; } = null!;

        public Button Above { get; private set; } = null!;

        public List<A11yEvent> Announced { get; } = [];

        /// <param name="nested">Adds a folder holding one note, so a selected
        /// file can sit beneath a collapsed row.</param>
        /// <param name="tagged">Adds a note carrying a tag, so the Tags tree
        /// has a row.</param>
        /// <param name="untagged">Strips the fixture's tags, so the Tags tree
        /// is empty, and adds a folder with no notes, whose dual-pane listing
        /// is empty.</param>
        /// <param name="nestedTag">Adds a note tagged <c>alpha/beta</c>, a
        /// child of the <c>alpha</c> tag.</param>
        public void Initialize(bool nested = false, bool tagged = false, bool untagged = false, bool nestedTag = false)
        {
            Assert.Null(Application.Current);
            if (nestedTag)
            {
                File.WriteAllText(Path.Combine(_fixture.Root, "nested-tag.md"), "---\ntags: [alpha/beta]\n---\n# Nested\n");
            }

            if (nested)
            {
                Directory.CreateDirectory(Path.Combine(_fixture.Root, "folder"));
                File.WriteAllText(Path.Combine(_fixture.Root, "folder", "inner.md"), "# Inner\n");
            }

            if (untagged)
            {
                foreach (string note in Directory.EnumerateFiles(_fixture.Root, "*.md"))
                {
                    File.WriteAllText(note, "# Untagged\n\nNo tags here.\n");
                }

                Directory.CreateDirectory(Path.Combine(_fixture.Root, "empty"));
                File.WriteAllText(Path.Combine(_fixture.Root, "empty", ".keep"), string.Empty);
            }

            if (tagged)
            {
                File.WriteAllText(Path.Combine(_fixture.Root, "tagged.md"), "# Tagged\n\n#alpha\n");
            }

            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            Sidebar = new FilesSidebarViewModel(_session, Announced.Add, localAppDataRoot: _fixture.Root);
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion);
            Assert.True(Sidebar.RootNodes.Count >= 3, "premise: the vault's notes are the tree's rows.");

            Shell = new MainWindow();
            var lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            SetState(lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), Sidebar);
            Pane = Assert.IsAssignableFrom<FrameworkElement>(Shell.FindName("FilesPaneBorder"));
            Tree = Assert.IsType<LandingTreeView>(Shell.FindName("FilesTree"));
            FilterField = Assert.IsType<TextBox>(Shell.FindName("SidebarFilterTextBox"));
            Assert.IsAssignableFrom<Panel>(Pane.Parent).Children.Remove(Pane);

            Above = new Button { Content = "Above" };
            var beside = new Button { Content = "Beside" };
            var root = new DockPanel { DataContext = lifecycle };
            DockPanel.SetDock(Above, Dock.Top);
            DockPanel.SetDock(beside, Dock.Right);
            DockPanel.SetDock(Pane, Dock.Left);
            root.Children.Add(Above);
            root.Children.Add(beside);
            root.Children.Add(Pane);
            _window = new Window
            {
                Content = root,
                Width = 700,
                Height = 600,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            _window.Show();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
            Assert.True(Tree.IsVisible, "premise: the lifted pane shows its tree.");
            _window.AddHandler(
                Keyboard.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler((_, e) =>
                {
                    _focusChanges.Add(e.NewFocus);
                    if (e.NewFocus is ItemsControl { HasItems: true })
                    {
                        _populatedFocus.Add(e.NewFocus);
                    }
                }),
                handledEventsToo: true);
        }

        public void Press(Key key)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(_window!)!, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            PumpedDispatcher.Drain();
        }

        public T ElementWithId<T>(string automationId)
            where T : DependencyObject =>
            Descendants(Pane).OfType<T>().Single(element => AutomationProperties.GetAutomationId(element) == automationId);

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, index);
                yield return child;
                foreach (DependencyObject nested in Descendants(child))
                {
                    yield return nested;
                }
            }
        }

        public void AssertTreeNeverFocused() =>
            Assert.True(
                !_focusChanges.Any(focus => ReferenceEquals(focus, Tree)),
                "the bare Files tree took the keys; focus went "
                + string.Join(" → ", _focusChanges.Select(focus => focus.GetType().Name)));

        /// <summary>The list never held the keys itself while it had rows —
        /// judged at each focus change, by the rows it had then.</summary>
        public void AssertNeverFocusedPopulated(ItemsControl list) =>
            Assert.True(
                !_populatedFocus.Contains(list),
                "a populated list took the keys itself; focus went "
                + string.Join(" → ", _focusChanges.Select(focus => focus.GetType().Name)));

        /// <summary>Forget the focus changes so far — a fact's own setup
        /// may put the keys anywhere.</summary>
        public void ForgetFocusChanges()
        {
            _focusChanges.Clear();
            _populatedFocus.Clear();
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            void CleanUp(Action action)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            try
            {
                if (Shell?.DataContext is VaultLifecycleViewModel lifecycle)
                {
                    CleanUp(() => SetState(lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), null));
                }

                if (Sidebar is not null)
                {
                    CleanUp(() => PumpedDispatcher.PumpUntilDrained(Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork));
                }

                CleanUp(() => _window?.Close());
                CleanUp(() => Shell?.Close());
                CleanUp(() => _session?.Dispose());
                CleanUp(_fixture.Dispose);
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
            }

            if (failures.Count > 0)
            {
                throw new AggregateException("Files landing fixture cleanup failed.", failures);
            }
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "Files landing fixture timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
