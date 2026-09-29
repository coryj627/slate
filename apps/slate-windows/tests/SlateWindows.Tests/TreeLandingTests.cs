// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5 as the owner amended it; spec review round
/// 23): a TREE's landing is a ROW — <see cref="SelectorFocus.FocusSelectedOrFirstRow"/>
/// puts the keys on the selected row, else the first, UNSELECTED — and never
/// the tree itself. The first fact is the platform's own behavior, measured:
/// WPF hands a focused tree's keys to its selected row, but a tree with no
/// selection keeps them, and Left leaves it for whatever lies beside it. The
/// shell's trees are <see cref="LandingTreeView"/>s, which never keep them.
/// Every key here is a real press through the input manager, so WPF's
/// directional navigation answers it the way it answers a physical key.
/// </summary>
public sealed class TreeLandingTests
{
    [Fact]
    public void WpfHandsABareTreesKeysToItsSelectedRowOnly() => RunSta(() =>
    {
        (TreeView tree, TreeViewItem first, TreeViewItem second) = Tree();
        using Hosted host = Host(tree);

        Assert.True(host.Above.Focus());
        Assert.True(tree.Focus());
        Assert.Same(tree, Keyboard.FocusedElement);
        Press(host, Key.Left);
        Assert.Same(host.Left, Keyboard.FocusedElement);

        second.IsSelected = true;
        Assert.True(host.Above.Focus());
        Assert.False(tree.Focus());
        Assert.Same(second, Keyboard.FocusedElement);
        Assert.NotSame(first, Keyboard.FocusedElement);
    });

    /// <summary>With nothing selected the first row takes the keys WITHOUT
    /// selecting itself (the owner's focus-without-select: a selection in the
    /// shell's trees opens a note or applies a tag); the tree itself never has
    /// them, not even for a moment.</summary>
    [Fact]
    public void WithNoSelectionTheFirstRowTakesTheKeysUnselected() => RunSta(() =>
    {
        (TreeView tree, TreeViewItem first, TreeViewItem second) = LandingTree();
        using Hosted host = Host(tree);
        Assert.True(host.Above.Focus());

        Assert.True(SelectorFocus.FocusSelectedOrFirstRow(tree));

        Assert.Same(first, Keyboard.FocusedElement);
        Assert.False(first.IsSelected, "the landing selected the first row.");
        Assert.Null(tree.SelectedItem);
        host.AssertNeverFocused(tree);

        // The row is a row like any other from there: Down selects the next,
        // as an arrow always has.
        Press(host, Key.Down);
        Assert.Same(second, Keyboard.FocusedElement);
        Assert.True(second.IsSelected);
    });

    /// <summary>The keys on their way to a populated landing tree itself —
    /// Tab, a click on its empty area (TreeView.HandleMouseButtonDown focuses
    /// the tree), a restore, UI Automation's SetFocus — go on to its landing
    /// in the same focus change: the tree never takes them.</summary>
    [Fact]
    public void KeysSentToABareLandingTreeGoOnToItsFirstRowUnselected() => RunSta(() =>
    {
        (TreeView tree, TreeViewItem first, _) = LandingTree();
        using Hosted host = Host(tree);
        Assert.True(host.Above.Focus());

        _ = tree.Focus();

        Assert.Same(first, Keyboard.FocusedElement);
        Assert.False(first.IsSelected, "the hand-on selected the first row.");
        host.AssertNeverFocused(tree);
    });

    /// <summary>UI Automation's SetFocus on a bare landing tree succeeds and
    /// lands on its first row, unselected: WPF's own peer threw whenever the
    /// tree's Focus() answered false, which a landing tree's always does
    /// while it has rows.</summary>
    [Fact]
    public void UiAutomationSetFocusOnALandingTreeLandsOnARow() => RunSta(() =>
    {
        (TreeView tree, TreeViewItem first, _) = LandingTree();
        using Hosted host = Host(tree);
        Assert.True(host.Above.Focus());

        UIElementAutomationPeer.CreatePeerForElement(tree).SetFocus();

        Assert.Same(first, Keyboard.FocusedElement);
        Assert.False(first.IsSelected);
        host.AssertNeverFocused(tree);
    });

    /// <summary>Tab — WPF's own traversal, no landing of ours — reaches a
    /// populated landing tree with nothing selected on its first row,
    /// unselected: the bare tree was Tab's resting place (codex PR 4 round 6,
    /// the repro's R6_5x).</summary>
    [Fact]
    public void TabIntoALandingTreeRestsOnItsFirstRowUnselected() => RunSta(() =>
    {
        (TreeView tree, TreeViewItem first, _) = LandingTree();
        using Hosted host = Host(tree);
        Assert.True(host.Left.Focus());

        for (int press = 0; press < 8 && !tree.IsKeyboardFocusWithin; press++)
        {
            Press(host, Key.Tab);
        }

        Assert.Same(first, Keyboard.FocusedElement);
        Assert.False(first.IsSelected);
        host.AssertNeverFocused(tree);
    });

    /// <summary>An EMPTY landing tree is its own stop (AR-6) — the launch
    /// landing can reach the Files tree before its first publication — and
    /// rows published under the keys land them on the first row, unselected,
    /// once the rows are laid out.</summary>
    [Fact]
    public void RowsPublishedUnderAnEmptyLandingTreeLandTheKeysOnARow() => RunSta(() =>
    {
        var tree = new LandingTreeView();
        using Hosted host = Host(tree);
        Assert.True(SelectorFocus.FocusSelectedOrFirstRow(tree));
        Assert.Same(tree, Keyboard.FocusedElement);

        var first = new LandingTreeViewItem { Header = "A" };
        tree.Items.Add(first);
        tree.Items.Add(new LandingTreeViewItem { Header = "B" });
        PumpedDispatcher.Drain();

        Assert.Same(first, Keyboard.FocusedElement);
        Assert.False(first.IsSelected);
    });

    [Fact]
    public void TheSelectedRowTakesTheKeysAtAnyDepth() => RunSta(() =>
    {
        (TreeView tree, TreeViewItem first, _) = Tree();
        var nested = new TreeViewItem { Header = "A1" };
        first.Items.Add(nested);
        first.IsExpanded = true;
        using Hosted host = Host(tree);
        nested.IsSelected = true;
        Assert.True(host.Above.Focus());

        Assert.True(SelectorFocus.FocusSelectedOrFirstRow(tree));

        Assert.Same(nested, Keyboard.FocusedElement);
        host.AssertNeverFocused(tree);
    });

    /// <summary>A selected row the virtualizing panel has not realized — far
    /// down a long tree — is brought into view and takes the keys in the same
    /// call.</summary>
    [Fact]
    public void ASelectedRowOutsideTheViewportIsRealizedAndTakesTheKeys() => RunSta(() =>
    {
        Node[] nodes = Enumerable.Range(0, 500).Select(index => new Node($"Row {index}")).ToArray();
        var tree = new TreeView { ItemsSource = nodes, Height = 120 };
        ScrollViewer.SetCanContentScroll(tree, true);
        VirtualizingPanel.SetIsVirtualizing(tree, true);
        VirtualizingPanel.SetVirtualizationMode(tree, VirtualizationMode.Recycling);
        using Hosted host = Host(tree);
        Assert.Null(tree.ItemContainerGenerator.ContainerFromItem(nodes[400]));
        Assert.True(host.Above.Focus());

        Assert.True(SelectorFocus.FocusSelectedOrFirstRow(tree, [nodes[400]]));

        TreeViewItem row = Assert.IsType<TreeViewItem>(Keyboard.FocusedElement);
        Assert.Same(nodes[400], tree.ItemContainerGenerator.ItemFromContainer(row));
        host.AssertNeverFocused(tree);
    });

    /// <summary>A selected row under a collapsed row is hidden: it is not a
    /// landing, and it gives way to the first row, UNSELECTED — the landing
    /// neither expands the folder nor selects it.</summary>
    [Fact]
    public void AHiddenSelectedRowGivesWayToTheFirstRowUnselected() => RunSta(() =>
    {
        var tree = new LandingTreeView();
        var folder = new LandingTreeViewItem { Header = "Folder" };
        var file = new LandingTreeViewItem { Header = "File" };
        folder.Items.Add(file);
        tree.Items.Add(folder);
        using Hosted host = Host(tree);
        folder.IsExpanded = true;
        tree.UpdateLayout();
        folder.IsExpanded = false;
        tree.UpdateLayout();
        Assert.True(host.Above.Focus());

        Assert.True(SelectorFocus.FocusSelectedOrFirstRow(tree, [folder, file]));

        Assert.Same(folder, Keyboard.FocusedElement);
        Assert.False(folder.IsSelected, "the landing selected the folder.");
        Assert.False(folder.IsExpanded, "the landing expanded the folder.");
        host.AssertNeverFocused(tree);
    });

    /// <summary>An empty tree is its own stop (AR-6), as an empty list is;
    /// one that is not on show is no landing at all.</summary>
    [Fact]
    public void AnEmptyTreeIsItsOwnStop() => RunSta(() =>
    {
        var tree = new LandingTreeView();
        using Hosted host = Host(tree);
        Assert.True(host.Above.Focus());

        Assert.True(SelectorFocus.FocusSelectedOrFirstRow(tree));
        Assert.Same(tree, Keyboard.FocusedElement);

        Assert.True(host.Above.Focus());
        tree.Visibility = Visibility.Collapsed;
        Assert.False(SelectorFocus.FocusSelectedOrFirstRow(tree));
        Assert.Same(host.Above, Keyboard.FocusedElement);
    });

    /// <summary>The arrow witness: from the landed row every arrow keeps the
    /// keys in the tree — at its first row, its last, a root and a leaf.</summary>
    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    public void FromTheLandedRowEveryArrowStaysInTheTree(Key key) => RunSta(() =>
    {
        (TreeView tree, _, _) = LandingTree();
        using Hosted host = Host(tree);
        Assert.True(host.Above.Focus());
        Assert.True(SelectorFocus.FocusSelectedOrFirstRow(tree));

        Press(host, key);

        Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement);
        Assert.True(tree.IsKeyboardFocusWithin, $"{key} took the keys out of the tree to {Keyboard.FocusedElement}");
        host.AssertNeverFocused(tree);
    });

    private sealed record Node(string Name);

    private static (TreeView Tree, TreeViewItem First, TreeViewItem Second) Tree()
    {
        var tree = new TreeView();
        var first = new TreeViewItem { Header = "A" };
        var second = new TreeViewItem { Header = "B" };
        tree.Items.Add(first);
        tree.Items.Add(second);
        return (tree, first, second);
    }

    /// <summary>The shell's kind of tree: rows that can take the keys
    /// unselected, and a tree that never keeps them itself.</summary>
    private static (TreeView Tree, TreeViewItem First, TreeViewItem Second) LandingTree()
    {
        var tree = new LandingTreeView();
        var first = new LandingTreeViewItem { Header = "A" };
        var second = new LandingTreeViewItem { Header = "B" };
        tree.Items.Add(first);
        tree.Items.Add(second);
        return (tree, first, second);
    }

    private static void Press(Hosted host, Key key)
    {
        InputManager.Current.ProcessInput(new KeyEventArgs(
            Keyboard.PrimaryDevice, PresentationSource.FromVisual(host.Window)!, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        });
        PumpedDispatcher.Drain();
    }

    /// <summary>The tree in the middle of a window with a button on each side,
    /// so an arrow that leaves it lands somewhere observable; every keyboard
    /// focus change is recorded.</summary>
    private static Hosted Host(Control center)
    {
        var grid = new Grid();
        for (int index = 0; index < 3; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        Button Place(string text, int row, int column)
        {
            var button = new Button { Content = text };
            Grid.SetRow(button, row);
            Grid.SetColumn(button, column);
            grid.Children.Add(button);
            return button;
        }

        Button above = Place("Above", 0, 1);
        _ = Place("Below", 2, 1);
        Button left = Place("Left", 1, 0);
        _ = Place("Right", 1, 2);
        Grid.SetRow(center, 1);
        Grid.SetColumn(center, 1);
        grid.Children.Add(center);
        var window = new Window
        {
            Content = grid,
            Width = 600,
            Height = 400,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        return new Hosted(window, above, left);
    }

    private sealed class Hosted : IDisposable
    {
        private readonly List<IInputElement> _focusChanges = [];

        internal Hosted(Window window, Button above, Button left)
        {
            Window = window;
            Above = above;
            Left = left;
            window.AddHandler(
                Keyboard.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler((_, e) => _focusChanges.Add(e.NewFocus)),
                handledEventsToo: true);
        }

        internal Window Window { get; }

        internal Button Above { get; }

        internal Button Left { get; }

        internal void AssertNeverFocused(UIElement tree) =>
            Assert.True(
                !_focusChanges.Any(focus => ReferenceEquals(focus, tree)),
                "the tree itself took the keys; focus went "
                + string.Join(" → ", _focusChanges.Select(focus => focus.GetType().Name)));

        public void Dispose() => Window.Close();
    }

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "STA test body timed out.");
}
