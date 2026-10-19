// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 43 PR 20b (#1267; the shell gate's two failures of 2026-10-18): a
/// rescan's reconcile — every watcher run's — keeps every Files row it did not
/// change: the same node, the same row and the same UI Automation element, so
/// a screen reader on a row, or a scan that collected the tree a moment
/// before, still holds a row the tree shows. Read here as UI Automation reads
/// it, through the rows' automation peers: the Templates journey's scan read
/// a row the publication had let go of — no rectangle, no place in its set,
/// its item's type name for its name — and the shell journey's held note row
/// refused SetFocus.
/// </summary>
public sealed partial class FilesRegionLandingTests
{
    /// <summary>A note created outside Slate at the top of the tree, a note
    /// edited outside it and a note added to a folder that was opened and
    /// closed again, then one rescan. Every row is the element held before it:
    /// on screen, with its rectangle, its place in its set and its own name —
    /// the edited note's too, updated in place (#1515), and the rows the open
    /// folder last in the tree shows, whose container WPF's recycling panel
    /// replaced on this insertion at the top until #1515 (measured on the
    /// Windows lane). The new note's row reads as fully. The closed folder,
    /// opened, lists the note added to it.</summary>
    [Fact]
    public void ARescansReconcileKeepsEveryRowItDidNotChange() => RunSta(() =>
    {
        using var host = new Host();
        Directory.CreateDirectory(Path.Combine(host.Root, "open"));
        File.WriteAllText(Path.Combine(host.Root, "open", "inside.md"), "# Inside\n");
        host.Initialize(nested: true);
        FileTreeNodeViewModel open = host.Sidebar.RootNodes.Single(node => node.Path == "open");
        FileTreeNodeViewModel closed = host.Sidebar.RootNodes.Single(node => node.Path == "folder");
        ExpandAndList(host, closed);
        closed.IsExpanded = false;
        ExpandAndList(host, open);
        AutomationPeer tree = Assert.IsAssignableFrom<AutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(host.Tree));
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Assert.True(held.ContainsKey("open/inside.md"), "premise: the open folder shows no row for its note.");
        Assert.False(held.ContainsKey("folder/inner.md"), "premise: the closed folder still shows its note.");
        foreach ((string path, AutomationPeer row) in held)
        {
            AssertReadsAsAShownRow(row, path);
        }

        File.WriteAllText(Path.Combine(host.Root, "0-created-outside.md"), "# Outside\n");
        File.AppendAllText(Path.Combine(host.Root, "note2.md"), "\nEdited outside Slate.\n");
        File.WriteAllText(Path.Combine(host.Root, "folder", "added.md"), "# Added\n");
        RescanAndRefresh(host);

        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        foreach ((string path, AutomationPeer row) in held)
        {
            var node = Assert.IsType<FileTreeNodeViewModel>(Assert.IsAssignableFrom<ItemAutomationPeer>(row).Item);
            Assert.True(shown.TryGetValue(path, out AutomationPeer? now), $"the rescan dropped {path}'s row");
            ItemAutomationPeer current = Assert.IsAssignableFrom<ItemAutomationPeer>(now);
            Assert.Same(node, current.Item);
            Assert.True(ReferenceEquals(current, row), $"the rescan gave {path}'s row a new element");
            AssertReadsAsAShownRow(row, path);
        }

        AssertReadsAsAShownRow(shown["0-created-outside.md"], "0-created-outside.md");
        Assert.Same(closed, host.Sidebar.RootNodes.Single(node => node.Path == "folder"));
        ExpandAndList(host, closed);
        Assert.Contains(closed.Children, child => child.Path == "folder/added.md");
    });

    /// <summary>A row a rescan removes — its note deleted outside Slate —
    /// leaves the tree, and the element a reader still holds for it reads the
    /// row's own name: never the item's type name, which is what the gate's
    /// scan reported ("SlateWindows.FileTreeNodeViewModel").</summary>
    [Fact]
    public void ARowARescanRemovesStillReadsItsOwnName() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        AutomationPeer tree = Assert.IsAssignableFrom<AutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(host.Tree));
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        FileTreeNodeViewModel gone = host.Sidebar.RootNodes.Single(node => node.Path == "note1.md");

        File.Delete(Path.Combine(host.Root, gone.Path));
        RescanAndRefresh(host);

        Assert.False(ShownRowElements(tree).ContainsKey("note1.md"), "the deleted note still has a row.");
        Assert.Equal(gone.AutomationName, held["note1.md"].GetName());
        Assert.Equal(gone.AutomationName, gone.ToString());
    });

    /// <summary>#1515 (a row changed in place, the selected one among them):
    /// the reader's own note and another note edited outside Slate, and a
    /// folder note created outside it, then one rescan. Every row is the node,
    /// the container and the element held before it — the edited notes' and
    /// the folder's too — and each reads its file as it is now: the fresh
    /// summary, and the folder's ", has folder note", with a change raised for
    /// each property that reads them. The keys never moved off the reader's
    /// row, and nothing was opened or said. Until #1515 a changed row was a
    /// new row, and a changed selected row replaced the whole tree.</summary>
    [Fact]
    public void ARescanUpdatesTheRowsItChangedInPlace() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(nested: true);
        var opened = new List<string>();
        FileTreeNodeViewModel reader = LandOnSelected(host, "note1.md", opened);
        FileTreeNodeViewModel edited = host.Sidebar.RootNodes.Single(node => node.Path == "note2.md");
        FileTreeNodeViewModel folder = host.Sidebar.RootNodes.Single(node => node.Path == "folder");
        AutomationPeer tree = Assert.IsAssignableFrom<AutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(host.Tree));
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> containers = ShownRowContainers(host);
        FileSummary? readerSummary = reader.Summary;
        FileSummary? editedSummary = edited.Summary;
        string editedMetadata = edited.MetadataText;
        var changes = new List<string>();
        foreach (FileTreeNodeViewModel row in new[] { reader, edited, folder })
        {
            row.PropertyChanged += (_, e) => changes.Add($"{row.Path} {e.PropertyName}");
        }

        File.AppendAllText(Path.Combine(host.Root, "note1.md"), "\nThe reader's own note, edited outside Slate.\n");
        File.AppendAllText(Path.Combine(host.Root, "note2.md"), "\nEdited outside Slate, with more words than it had.\n");
        File.WriteAllText(Path.Combine(host.Root, "folder", "folder.md"), "# Folder note\n");
        RescanAndRefresh(host);

        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> now = ShownRowContainers(host);
        Assert.Equal(held.Keys.Order(StringComparer.Ordinal).ToArray(), shown.Keys.Order(StringComparer.Ordinal).ToArray());
        foreach ((string path, AutomationPeer row) in held)
        {
            Assert.True(ReferenceEquals(row, shown[path]), $"the rescan gave {path}'s row a new element");
            Assert.True(ReferenceEquals(containers[path], now[path]), $"the rescan gave {path}'s row a new container");
            AssertReadsAsAShownRow(row, path);
        }

        Assert.NotEqual(readerSummary, reader.Summary);
        Assert.NotEqual(editedSummary, edited.Summary);
        Assert.NotEqual(editedMetadata, edited.MetadataText);
        Assert.Contains("note1.md Summary", changes);
        Assert.Contains("note2.md Summary", changes);
        Assert.Contains("note2.md MetadataText", changes);
        Assert.True(folder.HasFolderNote, "the folder never took its folder note.");
        Assert.EndsWith(", has folder note", folder.AutomationName, StringComparison.Ordinal);
        Assert.Contains("folder HasFolderNote", changes);
        Assert.Contains("folder AutomationName", changes);
        Assert.Same(reader, FocusedNode());
        Assert.Same(reader, host.Sidebar.SelectedNode);
        Assert.Empty(host.FocusChanges);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
    });

    /// <summary>#1515 (a change under an open folder): the reader on a note
    /// inside an open folder, and outside Slate the note beside it edited, a
    /// note in the folder's open subfolder edited, one of its notes deleted
    /// and one created, then one rescan. The folder's rows are reconciled in
    /// place, level by level: every row still there is the node and the
    /// element held before — the edited notes' updated in place — the deleted
    /// note's row is gone and the new note's is shown, in the fresh order.
    /// The keys never moved off the reader's row, and nothing was opened or
    /// said. Until #1515 one change under an open folder replaced the
    /// folder's rows, and with the reader among them the whole tree.</summary>
    [Fact]
    public void ARescanReconcilesAnOpenFoldersRowsInPlace() => RunSta(() =>
    {
        using var host = new Host();
        string open = Path.Combine(host.Root, "open");
        Directory.CreateDirectory(Path.Combine(open, "deep"));
        File.WriteAllText(Path.Combine(open, "gone.md"), "# Gone\n");
        File.WriteAllText(Path.Combine(open, "inside.md"), "# Inside\n");
        File.WriteAllText(Path.Combine(open, "sibling.md"), "# Sibling\n");
        File.WriteAllText(Path.Combine(open, "deep", "deeper.md"), "# Deeper\n");
        host.Initialize();
        FileTreeNodeViewModel folder = host.Sidebar.RootNodes.Single(node => node.Path == "open");
        ExpandAndList(host, folder);
        FileTreeNodeViewModel deep = folder.Children.Single(node => node.Path == "open/deep");
        ExpandAndList(host, deep);
        var opened = new List<string>();
        FileTreeNodeViewModel reader = LandOnRow(host, folder.Children.Single(node => node.Path == "open/inside.md"), opened);
        FileTreeNodeViewModel sibling = folder.Children.Single(node => node.Path == "open/sibling.md");
        FileTreeNodeViewModel deeper = deep.Children.Single(node => node.Path == "open/deep/deeper.md");
        AutomationPeer tree = Assert.IsAssignableFrom<AutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(host.Tree));
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Assert.True(held.ContainsKey("open/deep/deeper.md"), "premise: the open subfolder shows no row for its note.");
        var changes = new List<string>();
        sibling.PropertyChanged += (_, e) => changes.Add($"sibling {e.PropertyName}");
        deeper.PropertyChanged += (_, e) => changes.Add($"deeper {e.PropertyName}");

        File.AppendAllText(Path.Combine(open, "sibling.md"), "\nEdited outside Slate, beside the reader's note.\n");
        File.AppendAllText(Path.Combine(open, "deep", "deeper.md"), "\nEdited outside Slate, one folder down.\n");
        File.Delete(Path.Combine(open, "gone.md"));
        File.WriteAllText(Path.Combine(open, "new.md"), "# New\n");
        RescanAndRefresh(host);

        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        foreach ((string path, AutomationPeer row) in held.Where(pair => pair.Key != "open/gone.md"))
        {
            Assert.True(shown.TryGetValue(path, out AutomationPeer? now), $"the rescan dropped {path}'s row");
            Assert.True(ReferenceEquals(row, now), $"the rescan gave {path}'s row a new element");
            AssertReadsAsAShownRow(row, path);
        }

        Assert.False(shown.ContainsKey("open/gone.md"), "the deleted note still has a row.");
        AssertReadsAsAShownRow(shown["open/new.md"], "open/new.md");
        Assert.Equal(
            ["open/deep", "open/inside.md", "open/new.md", "open/sibling.md"],
            folder.Children.Select(node => node.Path));
        Assert.Same(deeper, Assert.Single(deep.Children));
        Assert.Equal(FileTreeChildLoadState.Loaded, folder.ChildLoadState);
        Assert.Contains("sibling Summary", changes);
        Assert.Contains("deeper Summary", changes);
        Assert.Same(reader, FocusedNode());
        Assert.Same(reader, host.Sidebar.SelectedNode);
        Assert.Empty(host.FocusChanges);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertKeysNeverOnAWindow();
    });

    /// <summary>#1515 (a kept open folder's recycled container): the reader on
    /// a note inside one open folder, another open folder last in the tree,
    /// and a note created outside Slate that sorts first, then one rescan.
    /// Every row keeps its node, its container and its element — the last
    /// folder's rows too. On this insertion WPF's recycling panel recycled the
    /// last folder's container, judged from the range of its previous pass,
    /// and every row under it came back with a new element (20b's scratch
    /// run), until the tree held its realized containers through the
    /// publication's layout (<c>RealizedRowHold</c>). The keys never
    /// moved.</summary>
    [Fact]
    public void AKeptOpenFoldersRowsKeepTheirContainersWhenARowIsInsertedAbove() => RunSta(() =>
    {
        using var host = new Host();
        Directory.CreateDirectory(Path.Combine(host.Root, "open"));
        File.WriteAllText(Path.Combine(host.Root, "open", "inside.md"), "# Inside\n");
        File.WriteAllText(Path.Combine(host.Root, "open", "other.md"), "# Other\n");
        host.Initialize(nested: true);
        FileTreeNodeViewModel folder = host.Sidebar.RootNodes.Single(node => node.Path == "folder");
        FileTreeNodeViewModel open = host.Sidebar.RootNodes.Single(node => node.Path == "open");
        Assert.Same(open, host.Sidebar.RootNodes[^1]);
        ExpandAndList(host, folder);
        ExpandAndList(host, open);
        var opened = new List<string>();
        FileTreeNodeViewModel reader = LandOnRow(host, folder.Children.Single(node => node.Path == "folder/inner.md"), opened);
        AutomationPeer tree = Assert.IsAssignableFrom<AutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(host.Tree));
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> containers = ShownRowContainers(host);
        Assert.True(containers.ContainsKey("open/other.md"), "premise: the last folder shows no row for its notes.");

        File.WriteAllText(Path.Combine(host.Root, "0-created-outside.md"), "# Outside\n");
        RescanAndRefresh(host);

        Assert.Equal("0-created-outside.md", host.Sidebar.RootNodes[0].Path);
        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        Dictionary<string, TreeViewItem> now = ShownRowContainers(host);
        foreach ((string path, AutomationPeer row) in held)
        {
            Assert.True(shown.TryGetValue(path, out AutomationPeer? current), $"the rescan dropped {path}'s row");
            Assert.True(
                now.TryGetValue(path, out TreeViewItem? container) && ReferenceEquals(containers[path], container),
                $"the rescan gave {path}'s row a new container");
            Assert.True(ReferenceEquals(row, current), $"the rescan gave {path}'s row a new element");
            AssertReadsAsAShownRow(row, path);
        }

        AssertReadsAsAShownRow(shown["0-created-outside.md"], "0-created-outside.md");
        Assert.Same(reader, FocusedNode());
        Assert.Empty(host.FocusChanges);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
    });

    /// <summary>#1515: the reader's own note retitled outside Slate so that it
    /// sorts last, then one rescan. Its row stays where it is — the same node,
    /// container and element, renamed in place, with the keys and no focus
    /// change — and the rows it sorted before move up around it: a row that
    /// moves gets a new container, which the keys would not follow, so the
    /// reconcile moves the others and never the reader's. The moved rows keep
    /// their nodes and their elements, and the tree reads in the fresh
    /// order.</summary>
    [Fact]
    public void AReadersRowWhoseTitleMovesItStaysWhileTheOtherRowsMove() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        FilesSidebarViewModel sidebar = host.Sidebar;
        var opened = new List<string>();
        FileTreeNodeViewModel reader = LandOnSelected(host, "note0.md", opened);
        TreeViewItem readerRow = Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement);
        FileTreeNodeViewModel[] before = [.. sidebar.RootNodes];
        AutomationPeer tree = Assert.IsAssignableFrom<AutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(host.Tree));
        Dictionary<string, AutomationPeer> held = ShownRowElements(tree);

        File.WriteAllText(
            Path.Combine(host.Root, "note0.md"),
            "---\ntitle: zz retitled outside Slate\ntags: [test]\n---\n\n# Note 0\n\nRetitled outside Slate.\n");
        RescanAndRefresh(host);

        Assert.Equal(["note1.md", "note2.md", "note0.md"], sidebar.RootNodes.Select(node => node.Path));
        Assert.Equal("zz retitled outside Slate", reader.DisplayName);
        Assert.All(before, node => Assert.Contains(node, sidebar.RootNodes));
        Assert.Same(readerRow, Keyboard.FocusedElement);
        Assert.Same(reader, FocusedNode());
        Assert.Empty(host.FocusChanges);
        Dictionary<string, AutomationPeer> shown = ShownRowElements(tree);
        foreach ((string path, AutomationPeer row) in held)
        {
            Assert.True(ReferenceEquals(row, shown[path]), $"the rescan gave {path}'s row a new element");
            AssertReadsAsAShownRow(row, path);
        }

        Assert.Empty(opened);
        Assert.Empty(host.Announced);
    });

    /// <summary>Every row the tree SHOWS, by path, with its
    /// container.</summary>
    private static Dictionary<string, TreeViewItem> ShownRowContainers(Host host)
    {
        var rows = new Dictionary<string, TreeViewItem>(StringComparer.Ordinal);
        Visit(host.Tree, host.Sidebar.RootNodes);
        return rows;

        void Visit(ItemsControl owner, IEnumerable<FileTreeNodeViewModel> nodes)
        {
            foreach (FileTreeNodeViewModel node in nodes.Where(node => !node.IsPlaceholder))
            {
                if (owner.ItemContainerGenerator.ContainerFromItem(node) is TreeViewItem container)
                {
                    rows[node.Path] = container;
                    if (node.IsExpanded)
                    {
                        Visit(container, node.Children);
                    }
                }
            }
        }
    }

    /// <summary>Open <paramref name="folder"/>'s row and wait until it lists
    /// its rows, laid out.</summary>
    private static void ExpandAndList(Host host, FileTreeNodeViewModel folder)
    {
        folder.IsExpanded = true;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => folder.HasLoadedChildren),
            $"{folder.Path} never listed its rows.");
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();
    }

    /// <summary>Every row the tree SHOWS, by path, as a UI Automation client
    /// reaches it after a change: the tree's item peers and each open
    /// folder's, every level's children read afresh.</summary>
    private static Dictionary<string, AutomationPeer> ShownRowElements(AutomationPeer tree)
    {
        var rows = new Dictionary<string, AutomationPeer>(StringComparer.Ordinal);
        Visit(tree);
        return rows;

        void Visit(AutomationPeer parent)
        {
            parent.ResetChildrenCache();
            foreach (AutomationPeer child in parent.GetChildren() ?? [])
            {
                if (child is ItemAutomationPeer { Item: FileTreeNodeViewModel { IsPlaceholder: false } node })
                {
                    rows[node.Path] = child;
                    if (node.IsExpanded)
                    {
                        Visit(child);
                    }
                }
            }
        }
    }

    /// <summary>The row reads as one the tree shows: on screen, with a
    /// rectangle, a place in its set and its row's name. A row the tree let
    /// go of throws <see cref="ElementNotAvailableException"/> for the first
    /// three, as UI Automation did for the gate's scan.</summary>
    private static void AssertReadsAsAShownRow(AutomationPeer row, string path)
    {
        var node = Assert.IsType<FileTreeNodeViewModel>(Assert.IsAssignableFrom<ItemAutomationPeer>(row).Item);
        try
        {
            Assert.False(row.IsOffscreen(), $"{path}'s row reads off-screen");
            Rect bounds = row.GetBoundingRectangle();
            Assert.True(bounds is { IsEmpty: false, Width: > 0, Height: > 0 }, $"{path}'s row has no rectangle ({bounds})");
            Assert.True(
                row.GetPositionInSet() > 0 && row.GetSizeOfSet() > 0,
                $"{path}'s row has no place in its set ({row.GetPositionInSet()} of {row.GetSizeOfSet()})");
        }
        catch (ElementNotAvailableException exception)
        {
            Assert.Fail($"{path}'s row is one the tree let go of: {exception.Message}");
        }

        Assert.Equal(node.AutomationName, row.GetName());
    }
}
