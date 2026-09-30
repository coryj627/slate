// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 (#1252, R-9) over PR 4 (#1247, R-5): the rescan's tree refresh
/// republishes the Files tree under the reader — fresh nodes for every row —
/// through the same publication the organic refresh takes, so the tree's
/// landing and the selection's reconcile keep the keys on the reader's row.
/// </summary>
public sealed partial class FilesRegionLandingTests
{
    /// <summary>With the keys on the selected note, a note created outside
    /// Slate that sorts ahead of it, published by the rescan's refresh,
    /// leaves them on the SAME note's fresh row — never on the bare tree and
    /// never on the first row — and the refresh opens nothing and says
    /// nothing.</summary>
    [Fact]
    public void ARescansTreeRefreshKeepsTheKeysOnTheReadersRow() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        FileTreeNodeViewModel reader = host.Sidebar.RootNodes[^1];
        host.Sidebar.SelectedNode = reader;
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();
        Assert.True(host.Shell.LandOnFilesTree());
        PumpedDispatcher.Drain();
        Assert.Same(reader, FocusedNode());
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        host.ForgetFocusChanges();
        host.Announced.Clear();

        File.WriteAllText(Path.Combine(host.Root, "0-created-outside.md"), "# Outside\n");
        using (var scan = new CancelToken())
        {
            _ = host.Session.Rescan(scan);
        }

        Task<ulong> refresh = host.Sidebar.RefreshForRescanAsync(reportCount: false, CancellationToken.None);
        Assert.True(PumpedDispatcher.PumpUntil(() => refresh.IsCompleted), "the rescan's tree refresh");
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();

        Assert.Equal(0UL, refresh.Result);
        Assert.Equal("0-created-outside.md", host.Sidebar.RootNodes[0].Path);
        FileTreeNodeViewModel? focused = FocusedNode();
        Assert.True(
            focused is not null && focused.Path == reader.Path,
            $"the rescan's refresh left the keys on {Keyboard.FocusedElement}, not on {reader.Path}'s row");
        Assert.NotSame(reader, focused);
        Assert.Equal(reader.Path, host.Sidebar.SelectedNode?.Path);
        Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();
        host.AssertKeysNeverOnAWindow();
    });

    /// <summary>Codex's merge-delta check (finding 1): with the keys on the
    /// selected note, the note deleted outside Slate and the rescan's refresh
    /// published, the keys land on the nearest surviving row — the next
    /// sibling — focused WITHOUT selecting it (R-5's focus-without-select):
    /// never the window, never the bare tree, nothing opened, nothing
    /// said.</summary>
    [Fact]
    public void ARescanThatDeletesTheFocusedRowLandsTheKeysOnTheNextRowUnselected() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        FileTreeNodeViewModel reader = LandOnSelected(host, "note1.md", opened);

        File.Delete(Path.Combine(host.Root, reader.Path));
        RescanAndRefresh(host);

        FileTreeNodeViewModel? focused = FocusedNode();
        Assert.True(
            focused is { Path: "note2.md" },
            $"the deletion left the keys on {Keyboard.FocusedElement?.GetType().Name ?? "nothing"} "
            + $"({focused?.Path ?? "no row"}), not on note2.md's row");
        Assert.False(Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement).IsSelected, "the landing selected the row.");
        Assert.Null(host.Sidebar.SelectedNode);
        Assert.DoesNotContain(host.Sidebar.RootNodes, node => node.IsSelected);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();
        host.AssertKeysNeverOnAWindow();
    });

    /// <summary>Finding 1's other arm: the deletion empties the tree, which is
    /// the Files region's stop (AR-6) — the keys land on the empty tree,
    /// never on the window.</summary>
    [Fact]
    public void ARescanThatEmptiesTheTreeLandsTheKeysOnTheEmptyTree() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        _ = LandOnSelected(host, "note1.md", opened);

        foreach (string note in Directory.EnumerateFiles(host.Root, "*.md"))
        {
            File.Delete(note);
        }

        RescanAndRefresh(host);

        Assert.False(host.Tree.HasItems, "premise: the refresh left rows in the tree.");
        Assert.True(
            ReferenceEquals(Keyboard.FocusedElement, host.Tree),
            $"the deletion left the keys on {Keyboard.FocusedElement?.GetType().Name ?? "nothing"}, not on the empty tree");
        Assert.Null(host.Sidebar.SelectedNode);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertNeverFocusedPopulated(host.Tree);
        host.AssertKeysNeverOnAWindow();
    });

    /// <summary>
    /// #1318's final merge (W7-7 PR 4b over PR 7): the Files pane's focus
    /// guard and the tree's restore of a rescan's publication move the keys
    /// ONCE between them. The restore lands them on a live row before WPF
    /// re-evaluates the removed one. The guard, finding them on a live row,
    /// adds no second landing and declines nothing. The keys end on the
    /// reader's fresh row, on the nearest survivor unselected, or on the
    /// emptied FilesTree, the region's stop.
    /// </summary>
    [Theory]
    [InlineData("kept")]
    [InlineData("deleted")]
    [InlineData("emptied")]
    public void ARescansTreePublicationMovesTheKeysOnceBesideTheGuard(string arm) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        Assert.Same(host.Shell.FilesTree, host.Tree);
        Assert.True(RegionFocusGuard.HasLanding(host.Pane), "premise: the guard is armed on the Files pane");
        var opened = new List<string>();
        string readerPath = arm == "kept" ? host.Sidebar.RootNodes[^1].Path : "note1.md";
        _ = LandOnSelected(host, readerPath, opened);
        switch (arm)
        {
            case "kept":
                File.WriteAllText(Path.Combine(host.Root, "0-created-outside.md"), "# Outside\n");
                break;
            case "deleted":
                File.Delete(Path.Combine(host.Root, readerPath));
                break;
            default:
                foreach (string note in Directory.EnumerateFiles(host.Root, "*.md"))
                {
                    File.Delete(note);
                }

                break;
        }

        RescanAndRefresh(host);

        IInputElement landed = Assert.Single(host.FocusChanges);
        Assert.Same(landed, Keyboard.FocusedElement);
        switch (arm)
        {
            case "kept":
                Assert.True(
                    landed is TreeViewItem { IsSelected: true, DataContext: FileTreeNodeViewModel kept } && kept.Path == readerPath,
                    $"the keys moved to {landed}, not to {readerPath}'s fresh row");
                break;
            case "deleted":
                Assert.True(
                    landed is TreeViewItem { IsSelected: false, DataContext: FileTreeNodeViewModel { Path: "note2.md" } },
                    $"the keys moved to {landed}, not to note2.md's row unselected");
                break;
            default:
                Assert.Same(host.Tree, landed);
                Assert.False(host.Tree.HasItems);
                break;
        }

        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertKeysNeverOnAWindow();
    });

    /// <summary>Select <paramref name="path"/>'s row, land the keys on it, then
    /// forget the setup's focus changes and announcements and start counting
    /// opens.</summary>
    private static FileTreeNodeViewModel LandOnSelected(Host host, string path, List<string> opened)
    {
        FileTreeNodeViewModel reader = host.Sidebar.RootNodes.Single(node => node.Path == path);
        host.Sidebar.SelectedNode = reader;
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();
        Assert.True(host.Shell.LandOnFilesTree());
        PumpedDispatcher.Drain();
        Assert.Same(reader, FocusedNode());
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        host.ForgetFocusChanges();
        host.Announced.Clear();
        return reader;
    }

    /// <summary>The rescan's scan, then its tree refresh — silent, awaited —
    /// pumped to its publication and the landings it schedules.</summary>
    private static void RescanAndRefresh(Host host)
    {
        using (var scan = new CancelToken())
        {
            _ = host.Session.Rescan(scan);
        }

        Task<ulong> refresh = host.Sidebar.RefreshForRescanAsync(reportCount: false, CancellationToken.None);
        Assert.True(PumpedDispatcher.PumpUntil(() => refresh.IsCompleted), "the rescan's tree refresh");
        Assert.Equal(0UL, refresh.Result);
        for (int round = 0; round < 2; round++)
        {
            host.Pane.UpdateLayout();
            PumpedDispatcher.Drain();
        }
    }

    private sealed partial class Host
    {
        public VaultSession Session => _session ?? throw new InvalidOperationException("The host is not initialized.");

        public string Root => _fixture.Root;

        /// <summary>Every focus change since the last forget, in order.</summary>
        public IReadOnlyList<IInputElement> FocusChanges => _focusChanges;

        /// <summary>No focus change since the last forget put the keys on a
        /// window — the stranded state a removed row leaves.</summary>
        public void AssertKeysNeverOnAWindow() =>
            Assert.True(
                !_focusChanges.Any(focus => focus is Window),
                "the keys reached the window; focus went "
                + string.Join(" → ", _focusChanges.Select(focus => focus.GetType().Name)));
    }
}
