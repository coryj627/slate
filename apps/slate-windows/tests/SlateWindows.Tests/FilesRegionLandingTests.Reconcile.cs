// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;

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
    /// closed again, then one rescan. Every other root row is the element held
    /// before it: on screen, with its rectangle, its place in its set and its
    /// own name. The open folder's row keeps its node, and so does every row
    /// it shows; those read as fully, and their elements are their folder
    /// container's, which WPF's recycling panel replaced on this insertion at
    /// the top (measured on the Windows lane; #1515). The edited note's row is
    /// a new row and reads as fully, and so does the new note's. The closed
    /// folder, opened, lists the note added to it.</summary>
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
        foreach ((string path, AutomationPeer row) in held.Where(pair => pair.Key != "note2.md"))
        {
            var node = Assert.IsType<FileTreeNodeViewModel>(Assert.IsAssignableFrom<ItemAutomationPeer>(row).Item);
            Assert.True(shown.TryGetValue(path, out AutomationPeer? now), $"the rescan dropped {path}'s row");
            ItemAutomationPeer current = Assert.IsAssignableFrom<ItemAutomationPeer>(now);
            Assert.Same(node, current.Item);
            if (node.Level == 1)
            {
                Assert.True(ReferenceEquals(current, row), $"the rescan replaced {path}'s row, which it did not change");
                AssertReadsAsAShownRow(row, path);
            }
            else
            {
                AssertReadsAsAShownRow(current, path);
            }
        }

        Assert.NotSame(held["note2.md"], shown["note2.md"]);
        AssertReadsAsAShownRow(shown["note2.md"], "note2.md");
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
