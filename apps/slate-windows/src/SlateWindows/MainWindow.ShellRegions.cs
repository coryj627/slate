// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SlateWindows;

/// <summary>The window's half of F6 cycling (W7-6, #1240): which region
/// holds focus, and how each region takes it. The ring and the speech
/// are the view model's (<see cref="WorkspaceViewModel.ShellRegionHost"/>).</summary>
public partial class MainWindow : IShellRegionHost
{
    bool IShellRegionHost.ModalSurfaceOpen =>
        ModalSurfaces.TopmostOpen(CurrentModalSurfaceState) is not null;

    string IShellRegionHost.StatusText => _viewModel.StatusText ?? string.Empty;

    bool IShellRegionHost.RightPaneHasContentStop =>
        _viewModel.Workspace is WorkspaceViewModel workspace
        && (workspace.ConnectionsLeafIsActive()
            || (workspace.IsGraphInspectorShown && GraphInspectorSurface.IsVisible)
            || VisibleLeafBody() is { } body && FirstFocusable(body) is not null);

    ShellRegionKind? IShellRegionHost.FocusedRegion()
    {
        if (Keyboard.FocusedElement is not DependencyObject focused
            || !ReferenceEquals(Window.GetWindow(focused), this))
        {
            return null;
        }

        if (IsWithin(focused, MainMenu))
        {
            return ShellRegionKind.MenuBar;
        }

        if (IsWithin(focused, FilesPaneBorder))
        {
            return ShellRegionKind.Files;
        }

        if (IsWithin(focused, ContentPaneBorder))
        {
            // The border itself is the EMPTY pane's stop, and only then
            // (final review, #1240): with tabs open it is just the content
            // pane's own chrome, and calling that EmptyEditor made the ring
            // believe the tabless shape while a note was open.
            if (ReferenceEquals(focused, ContentPaneBorder)
                && _viewModel.Workspace is WorkspaceViewModel { } w
                && w.ActiveGroup.Tabs.Count == 0)
            {
                return ShellRegionKind.EmptyEditor;
            }

            return HasAncestor<TabItem>(focused) || HasAncestor<TabPanel>(focused)
                ? ShellRegionKind.TabBar
                : ShellRegionKind.Editor;
        }

        if (IsWithin(focused, RightPaneBorder))
        {
            return IsWithin(focused, RightPaneLeavesList)
                ? ShellRegionKind.RightPaneRail
                : ShellRegionKind.RightPaneContent;
        }

        if (IsWithin(focused, ShellStatusBar))
        {
            return ShellRegionKind.StatusBar;
        }

        return null;
    }

    /// <summary>A landing succeeds when focus ENDS UP in the region, not
    /// when a container's <c>Focus()</c> call reports true (W7-6 fix
    /// round 1, #1240): WPF redirects keyboard focus to a
    /// <c>TreeViewItem</c>'s or <c>ListBoxItem</c>'s selected container, so
    /// <c>Focus()</c> on the <c>TreeView</c>/<c>ListBox</c> itself can
    /// report false even though the press landed inside it — the ring then
    /// treated the landing as refused and skipped the region entirely. Each
    /// case still performs the same landing call(s); only the guard clauses
    /// (no workspace, hidden right pane, no tab) return false before any of
    /// them run. <see cref="ShellRegionKind.Editor"/> answers early only
    /// for canvas and graph tabs, whose <c>FocusEditorPane</c> landing is
    /// asynchronous; a text tab's end state is checked like every other
    /// region's (final review, #1240).</summary>
    bool IShellRegionHost.TryLand(ShellRegionKind region)
    {
        if (_viewModel.Workspace is not WorkspaceViewModel workspace)
        {
            return false;
        }

        switch (region)
        {
            case ShellRegionKind.MenuBar:
                if (MainMenu.Items.Count == 0
                    || MainMenu.ItemContainerGenerator.ContainerFromIndex(0) is not MenuItem first)
                {
                    return false;
                }

                first.Focus();
                break;
            case ShellRegionKind.Files:
                // R-5 (#1247): the filter's results land on a result row,
                // or on the list itself when it is empty (AR-6); a row that
                // cannot be landed yet leaves the keys to the tree's own
                // landing, a row or the filter field.
                _ = FilterResultsList.IsVisible
                    ? SelectorFocus.FocusFirstOrSelectedItem(FilterResultsList) || LandOnFilesTree()
                    : LandOnFilesTree();
                break;
            case ShellRegionKind.TabBar:
                {
                    WorkspaceGroupViewModel group = workspace.ActiveGroup;
                    if (group.ActiveTab is not { } activeTab)
                    {
                        return false;
                    }

                    TabControl? tabs = FindVisualDescendants<TabControl>(ContentPaneBorder)
                        .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, group));
                    tabs?.UpdateLayout();
                    if (tabs?.ItemContainerGenerator.ContainerFromItem(activeTab) is TabItem item)
                    {
                        item.Focus();
                    }

                    break;
                }
            case ShellRegionKind.Editor:
                if (workspace.ActiveGroup.ActiveTab is not { } editorTab)
                {
                    return false;
                }

                FocusEditorPane(workspace.ActiveGroup);
                // Canvas and graph tabs seat focus asynchronously through their
                // own landing; the text editor is synchronous, so its end state
                // is judged like every other region (it can fall back to the tab
                // item or the Files tree, which must read as a refusal).
                return editorTab is { IsCanvas: true } or { IsGraph: true }
                    || ((IShellRegionHost)this).FocusedRegion() == ShellRegionKind.Editor;
            case ShellRegionKind.EmptyEditor:
                if (workspace.ActiveGroup.ActiveTab is not null)
                {
                    return false;
                }

                ContentPaneBorder.Focus();
                break;
            case ShellRegionKind.RightPaneContent:
                if (!workspace.IsRightPaneVisible)
                {
                    return false;
                }

                if (workspace.ConnectionsLeafIsActive())
                {
                    ConnectionsLeafSurface.FocusAnchor();
                }
                else if (workspace.IsGraphInspectorShown && GraphInspectorSurface.IsVisible)
                {
                    GraphInspectorSurface.FocusFirstStop();
                }
                else if (VisibleLeafBody() is { } body && FirstFocusable(body) is { } stop)
                {
                    // A stop that cannot be landed leaves the region
                    // unfocused, so the ring moves on to the rail.
                    _ = SelectorFocus.LandOnStop(stop);
                }

                break;
            case ShellRegionKind.RightPaneRail:
                if (!workspace.IsRightPaneVisible)
                {
                    return false;
                }

                // R-5 (#1247): the shown leaf's row (the first row if the
                // rail names none), never the bare list, from which Down
                // walked into the menu bar.
                _ = SelectorFocus.FocusFirstOrSelectedItem(RightPaneLeavesList);
                break;
            case ShellRegionKind.StatusBar:
                ShellStatusBar.Focus();
                break;
            default:
                return false;
        }

        return ((IShellRegionHost)this).FocusedRegion() == region;
    }

    /// <summary>WPF's menu mode routes keys to the menu; F6 is handed to
    /// the ring so a press from the menu-bar region moves on (spec §4).</summary>
    private void MainMenu_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Only the two ring chords: Ctrl+F6, Alt+F6 and the rest are not
        // ours to swallow (final review, #1240).
        if (e.Key != Key.F6
            || Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift)
            || _viewModel.Workspace is not WorkspaceViewModel workspace)
        {
            return;
        }

        bool back = Keyboard.Modifiers == ModifierKeys.Shift;
        (back ? workspace.FocusPreviousPaneCommand : workspace.FocusNextPaneCommand).Execute(null);
        e.Handled = true;
    }

    /// <summary>The leaf body currently shown in the right pane's content
    /// column: the visible child of <c>RightPaneLeafHost</c> in column 0
    /// that is not the docked placeholder. The placeholder is excluded BY
    /// REFERENCE (final review, #1240): the old <c>is not StackPanel</c>
    /// test would silently skip any future leaf body that happened to be a
    /// <c>StackPanel</c>.</summary>
    private FrameworkElement? VisibleLeafBody() =>
        RightPaneLeafHost.Children.OfType<FrameworkElement>()
            .Where(child => Grid.GetColumn(child) == 0 && child.IsVisible)
            .FirstOrDefault(child => !ReferenceEquals(child, RightPaneDockedPlaceholder));

    private static UIElement? FirstFocusable(DependencyObject root)
    {
        foreach (DependencyObject candidate in FindVisualDescendants<DependencyObject>(root))
        {
            if (candidate is UIElement { Focusable: true, IsEnabled: true, IsVisible: true } element
                && candidate is not Border)
            {
                return element;
            }
        }

        return null;
    }

    /// <summary>
    /// W7-7 PR 4 (#1247, R-5; spec review round 23): every landing in the
    /// Files tree — the ring's, the Files boundary's, a rename's, a
    /// mutation's restore, Move To's, the empty editor's last resort — is a
    /// ROW: the selected file's, else the first.
    /// </summary>
    /// <remarks>
    /// They were <c>FilesTree.Focus()</c>, which reaches a row only when
    /// the tree holds a selection: with none the bare tree kept the keys,
    /// and Left and Right left the region (<c>TreeLandingTests</c>). The
    /// sidebar's selected node is the source of truth — a recycled
    /// container drops the tree's own — so its path is handed over. When no
    /// row can take the keys (an empty vault, a tree the filter has
    /// replaced) the region's stable stop is the filter field, which keeps
    /// all four arrows.
    /// </remarks>
    /// <returns>Whether the keys landed in the Files region.</returns>
    internal bool LandOnFilesTree() =>
        SelectorFocus.FocusSelectedOrFirstRow(FilesTree, SelectedFilesPath())
        || SidebarFilterTextBox.Focus();

    /// <summary>The sidebar's selected node and its ancestors, root first;
    /// null when nothing is selected or the node is no longer in the
    /// tree.</summary>
    private IReadOnlyList<object>? SelectedFilesPath()
    {
        if (_viewModel.FileSidebar is not { SelectedNode: { } selected } sidebar)
        {
            return null;
        }

        var path = new List<object>();
        return PathTo(sidebar.RootNodes, selected, path) ? path : null;

        static bool PathTo(IEnumerable<FileTreeNodeViewModel> level, FileTreeNodeViewModel target, List<object> path)
        {
            foreach (FileTreeNodeViewModel node in level)
            {
                path.Add(node);
                if (ReferenceEquals(node, target) || PathTo(node.Children, target, path))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
            }

            return false;
        }
    }

    /// <summary>W7-7 PR 4 (#1247, R-5): where a leaf REVEAL puts the keys —
    /// Ctrl+R's review, Show History — when the shown leaf has no landing
    /// of its own: the leaf's first stop, the same landing as the ring's
    /// right-pane content stop, so Ctrl+R puts the reader on the review's
    /// "All, N tasks" filter; the rail's selected row when the leaf has no
    /// stop, or its stop took nothing. It was the bare rail, from which Down
    /// walked into the menu bar. Ctrl+Alt+Right's edge is not a reveal and keeps the rail
    /// (<see cref="WorkspaceFocusBoundary.RightPaneEdge"/>).</summary>
    private void LandInRightPane()
    {
        if (VisibleLeafBody() is { } body && FirstFocusable(body) is { } stop && SelectorFocus.LandOnStop(stop))
        {
            return;
        }

        // The leaf has no stop, or its stop took nothing (a list whose row
        // cannot be landed yet among them): the pane's stable stop, the
        // rail's row.
        _ = SelectorFocus.FocusFirstOrSelectedItem(RightPaneLeavesList);
    }

    private static bool IsWithin(DependencyObject element, DependencyObject scope)
    {
        for (DependencyObject? current = element; current is not null; current = ParentOf(current))
        {
            if (ReferenceEquals(current, scope))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        for (DependencyObject? current = element; current is not null; current = ParentOf(current))
        {
            if (current is T)
            {
                return true;
            }
        }

        return false;
    }

    // Not `Parent`: that name hid FrameworkElement.Parent (CS0108), the
    // one warning the app build carried (#1238's zero-warning bar).
    private static DependencyObject? ParentOf(DependencyObject current) =>
        current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
}
