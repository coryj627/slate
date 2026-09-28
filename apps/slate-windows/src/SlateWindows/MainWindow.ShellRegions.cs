// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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

    /// <summary>W7-7 PR 4b (#1247, AR-38): the row the sidebars and the
    /// editor share tells the workspace how much room a sidebar resize
    /// step has.</summary>
    private void WorkspaceColumns_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_viewModel.Workspace is WorkspaceViewModel workspace)
        {
            workspace.WorkspaceRowWidth = e.NewSize.Width;
        }
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

    /// <summary>
    /// A leaf's first stop, in visual order. Layout is no stop: a border
    /// never is, and every scroll viewer the code builds is unfocusable
    /// (W7-7 PR 4b, the sweep's G19 — the Sync leaf's first stop was an
    /// unnamed scroll viewer; <c>ScrollViewerStopCensus</c>). An EMPTY list
    /// gives way to a populated list that follows it (the owner's S5, the
    /// sweep's G14: a note whose tasks are all done landed on the empty
    /// "Open tasks" list, its rows one list below); with nothing populated
    /// after it, the empty list is the stop (AR-6).
    /// </summary>
    private static UIElement? FirstFocusable(DependencyObject root)
    {
        UIElement? emptyList = null;
        foreach (DependencyObject candidate in FindVisualDescendants<DependencyObject>(root))
        {
            if (candidate is not UIElement { Focusable: true, IsEnabled: true, IsVisible: true } element
                || candidate is Border)
            {
                continue;
            }

            bool list = element is Selector selector && SelectorFocus.IsListLanding(selector);
            if (list && !((Selector)element).HasItems)
            {
                emptyList ??= element;
                continue;
            }

            if (emptyList is not null && emptyList.IsAncestorOf(element))
            {
                continue;
            }

            return emptyList is null || list ? element : emptyList;
        }

        return emptyList;
    }

    /// <summary>
    /// W7-7 PR 4b (#1247, R-5; the owner's S3): every region root owns its
    /// landing, which <see cref="RegionFocusGuard"/> takes when the element
    /// holding the keys in it goes away — disabled, collapsed, rebuilt —
    /// instead of WPF's hand-up to the tab control, a scroll viewer or the
    /// window. The Files pane, the editor, each right-pane leaf, the rail,
    /// the status bar, the welcome view and every sheet
    /// (<c>RegionGuardCensus</c>). The menu bar is not one: its items are
    /// in their own popups, and it takes no arrows.
    /// </summary>
    private void GuardRegions()
    {
        var host = (IShellRegionHost)this;
        // The welcome view and the workspace (codex PR 4b r1 F8's widened
        // census): opening or closing the vault swaps them under the keys,
        // and every scope inside the one that goes goes with it. The keys land
        // in the one that replaced it. (No landing sits above both: the
        // sheets share their parent, and a sheet's close is its own
        // restore's.)
        RegionFocusGuard.SetGoneLanding(
            WorkspaceRoot, () => FirstFocusable(WelcomeRoot) is { } welcome && SelectorFocus.LandOnStop(welcome));
        RegionFocusGuard.SetGoneLanding(WelcomeRoot, EditorRegionLanding);
        // The three workspace columns (codex PR 4b r1 F1): when a
        // whole region goes — the right pane hidden under the keys (Ctrl+Alt+I,
        // the View menu, the palette) — every scope inside it is gone, and
        // WPF's re-evaluation found no focusable ancestor short of the window.
        // The keys land in the editor region, as the pane's own Left boundary
        // does. A modal overlay (it disables the workspace) and the welcome
        // view (it collapses it) leave this scope dead, and it takes nothing.
        RegionFocusGuard.SetLanding(WorkspaceColumns, EditorRegionLanding);
        RegionFocusGuard.SetLanding(FilesPaneBorder, () => host.TryLand(ShellRegionKind.Files));
        RegionFocusGuard.SetLanding(ContentPaneBorder, EditorRegionLanding);
        RegionFocusGuard.SetLanding(RightPaneLeavesList, () => SelectorFocus.FocusFirstOrSelectedItem(RightPaneLeavesList));
        // The leaves' host: a leaf switched under the keys (a command, a
        // reveal, the model) collapses the leaf they were in; they land in
        // the leaf now shown — its first stop, else the rail's row — not out
        // of the right pane.
        RegionFocusGuard.SetLanding(RightPaneLeafHost, () => VisibleLeafBody() is { } shown && LandInLeaf(shown));
        foreach (FrameworkElement body in RightPaneLeafHost.Children.OfType<FrameworkElement>()
            .Where(child => Grid.GetColumn(child) == 0 && !ReferenceEquals(child, RightPaneDockedPlaceholder)))
        {
            RegionFocusGuard.SetLanding(body, () => LandInLeaf(body));
        }

        RegionFocusGuard.SetLanding(
            (UIElement)LogicalTreeHelper.GetParent(ShellStatusBar), () => host.TryLand(ShellRegionKind.StatusBar));
        RegionFocusGuard.SetLanding(WelcomeRoot, () => FirstFocusable(WelcomeRoot) is { } stop && SelectorFocus.LandOnStop(stop));
        foreach (UIElement sheet in FocusScopeOverlays(this))
        {
            RegionFocusGuard.SetLanding(sheet, () => FirstFocusable(sheet) is { } stop && SelectorFocus.LandOnStop(stop));
        }

        // The query builder's conditions, a finer scope inside its sheet (codex
        // PR 4b r1 F5; R-5 (h), "a removed row's keys come back to the rows
        // they were in"): a condition removed from its own Remove button lands
        // the keys in a remaining condition, not on the sheet's first stop —
        // its footer. The last removal collapses the conditions, and the
        // sheet's landing takes them.
        ItemsControl conditions = FindWithAutomationId<ItemsControl>(BaseQueryBuilderOverlay, "BuilderConditions")
            ?? throw new InvalidOperationException("BuilderConditions is not in the shell's XAML.");
        RegionFocusGuard.SetLanding(conditions, () => SelectorFocus.LandOnStop(conditions));

        // The review's "Load more" collapses under the keys when the last
        // page arrives (the sweep's G4): they go to the first row it
        // appended — where the reading continues — else the list's last
        // row, else the leaf's landing.
        RegionFocusGuard.SetStrandedLanding(PanelReviewLoadMore, LandAfterLoadMore);
    }

    /// <summary>The editor region's landing: the active tab's editor, else
    /// the empty editor's.</summary>
    private bool EditorRegionLanding() =>
        ((IShellRegionHost)this).TryLand(
            _viewModel.Workspace is { ActiveGroup.ActiveTab: not null } ? ShellRegionKind.Editor : ShellRegionKind.EmptyEditor);

    /// <summary>An arrow in the rail is choosing the leaf, for the length of
    /// its key press.</summary>
    private bool _railArrow;

    /// <summary>
    /// W7-7 PR 4b (#1247; the completeness sweep's G21, AR-59): an arrow on
    /// the rail CHOOSES the leaf — its selection switches the shown leaf — and
    /// the row taking the keys says so ("Outline, 3 of 12"); the authored
    /// "Outline panel." on top repeated it, one arrow, two utterances. The
    /// line stays silent on the arrow route only (OD-11(d)'s rule for a radio
    /// group's arrow); a reveal, a command, a click and the ring still speak
    /// it. The flag lasts the key press: the list handles the arrow — its
    /// selection, the leaf switch — inside the same input dispatch, and the
    /// flag is cleared at Input priority, after it (never by a listener past
    /// handled: #1275's seal, ShellSealAdmissionCensus).
    /// </summary>
    private void WatchRailArrows() =>
        RightPaneLeavesList.PreviewKeyDown += (_, e) =>
        {
            _railArrow = e.KeyboardDevice.Modifiers == ModifierKeys.None
                && e.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown
                && e.OriginalSource is ListBoxItem;
            if (_railArrow)
            {
                _ = Dispatcher.BeginInvoke(() => _railArrow = false, DispatcherPriority.Input);
            }
        };

    private bool LandAfterLoadMore()
    {
        if (!PanelReviewList.IsVisible || !PanelReviewList.HasItems || _viewModel.Workspace is not { } workspace)
        {
            return false;
        }

        int index = workspace.TasksReview.LastAppendStart is int start && start < PanelReviewList.Items.Count
            ? start
            : PanelReviewList.Items.Count - 1;
        return SelectorFocus.FocusItem(PanelReviewList, PanelReviewList.Items[index]);
    }

    /// <summary>The window's sheets: every focus scope in its logical tree
    /// but a menu.</summary>
    private static IEnumerable<UIElement> FocusScopeOverlays(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject element)
            {
                continue;
            }

            if (element is UIElement scope and not MenuBase && FocusManager.GetIsFocusScope(scope))
            {
                yield return scope;
            }

            foreach (UIElement nested in FocusScopeOverlays(element))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// W7-7 PR 4 (#1247, R-5 as the owner amended it; spec review round 23):
    /// every landing in the Files tree — the ring's, the Files boundary's, a
    /// rename's, a mutation's restore, Move To's, the empty editor's last
    /// resort (the launch landing with no tab restored, W7-5), a restore
    /// whose token is the tree or one of its rows, the tree's own hand-on
    /// from Tab or a click — goes through here: the selected file's ROW,
    /// realized; else the tree's first row, UNSELECTED.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They were <c>FilesTree.Focus()</c>, which reaches a row only when the
    /// tree holds a selection it has realized. The sidebar's selected node
    /// is the source of truth — a recycled container drops the tree's own —
    /// so its path is handed to the tree landing, which focuses that row.
    /// </para>
    /// <para>
    /// With no selected row to land on — nothing selected, the selection
    /// hidden under a collapsed folder — the landing is the first row,
    /// focused without selecting it (the owner's focus-without-select,
    /// which replaced codex round 5's filter-field fallback): selecting a
    /// file OPENS it (OD-2), and a landing opens nothing, says nothing and
    /// leaves the selection where it was. It is never the bare tree, a
    /// populated container. An EMPTY tree is its own stop (AR-6); a tree
    /// the filter has replaced cannot take the keys, and the region's
    /// stable stop, the filter field, does.
    /// </para>
    /// </remarks>
    /// <returns>Whether the keys landed in the Files region.</returns>
    internal bool LandOnFilesTree() => LandOnSidebarTree(FilesTree, SelectedFilesPath());

    /// <summary>A Files-region tree's landing: its selected row, else its
    /// first row unselected; an empty tree on show is its own stop (AR-6);
    /// else — a tree the filter replaced, rows not realized — the region's
    /// stable stop, the filter field.</summary>
    private bool LandOnSidebarTree(TreeView tree, IReadOnlyList<object>? selectedPath) =>
        SelectorFocus.FocusSelectedOrFirstRow(tree, selectedPath) || SidebarFilterTextBox.Focus();

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
    internal void LandInRightPane()
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

    /// <summary>A leaf's own landing — the reveal's and the ring's: its
    /// first stop, else the pane's stable stop, the rail's row.</summary>
    private bool LandInLeaf(FrameworkElement body) =>
        (body.IsVisible && FirstFocusable(body) is { } stop && SelectorFocus.LandOnStop(stop))
        || SelectorFocus.FocusFirstOrSelectedItem(RightPaneLeavesList);

    /// <summary>
    /// W7-7 PR 4 (#1247, R-5, spec §5.2.2; codex round 5): the leaves'
    /// publications under the keys keep them on a stop
    /// (<see cref="SelectorFocus.KeepKeysThroughPublications"/>). The four
    /// list leaves and the Tasks leaf: rows that fill an empty list holding
    /// the keys while it loaded — or a list WPF handed a removed row's keys
    /// to — land them on a row; a list left empty lands them on its notice;
    /// a notice whose sentence turns to "Loading…" hands them to its list
    /// first. The Embeds leaf's cards are its stops, its host none. The
    /// Citations leaf restores its own publications
    /// (<see cref="RestoreCitationFocus"/>), so only its notices' hand-off
    /// is kept; the Files filter's results re-land like a leaf's list.
    /// </summary>
    private void KeepLeafKeysThroughPublications()
    {
        (ItemsControl[] Rows, string Notice)[] leaves =
        [
            ([PanelBacklinksList], "PanelBacklinksNotice"),
            ([PanelOutgoingLinksList], "PanelOutgoingLinksNotice"),
            ([PanelOutlineList], "PanelOutlineNotice"),
            ([ElementWithAutomationId<ItemsControl>(RightPaneLeafHost, "PanelEmbedsList")], "PanelEmbedsNotice"),
            ([PanelTasksOpenList, PanelTasksDoneList], "PanelTasksNotice"),
        ];
        foreach ((ItemsControl[] rows, string noticeId) in leaves)
        {
            FrameworkElement body = LeafBodyOf(rows[0]);
            SelectorFocus.KeepKeysThroughPublications(
                body, rows, [ElementWithAutomationId<UIElement>(body, noticeId)], () => LandInLeaf(body));
        }

        // The review's page one re-queries after a toggle, a filter or a
        // refresh and republishes under the reader (codex PR 4 round 6
        // high 2): a removed row's keys land on a row, and an emptied
        // page's on the leaf's landing — its checked filter.
        FrameworkElement review = LeafBodyOf(PanelReviewList);
        SelectorFocus.KeepKeysThroughPublications(review, [PanelReviewList], [], () => LandInLeaf(review));
        // The Queries leaf's three registry lists are rebuilt on every
        // refresh (G5): a removed row's keys land on the fresh row of the
        // same item, which the window re-selects by identity.
        FrameworkElement queries = LeafBodyOf(QueriesSavedList);
        SelectorFocus.KeepKeysThroughPublications(
            queries, [QueriesSavedList, QueriesBaseFilesList, QueriesDashboardsList], [], () => LandInLeaf(queries));
        FrameworkElement citations = LeafBodyOf(PanelCitationsList);
        SelectorFocus.KeepKeysThroughPublications(
            citations, [PanelCitationsList], CitationNotices, () => LandInLeaf(citations), reLandPublications: false);
        SelectorFocus.KeepKeysThroughPublications(
            FilterResultsList,
            [FilterResultsList],
            [],
            () => SelectorFocus.FocusFirstOrSelectedItem(FilterResultsList) || LandOnFilesTree());
    }

    /// <summary>The leaf body — a direct child of the leaf host — that
    /// holds <paramref name="element"/>.</summary>
    private FrameworkElement LeafBodyOf(DependencyObject element)
    {
        DependencyObject current = element;
        while (LogicalTreeHelper.GetParent(current) is { } parent && !ReferenceEquals(parent, RightPaneLeafHost))
        {
            current = parent;
        }

        return (FrameworkElement)current;
    }

    private static T ElementWithAutomationId<T>(DependencyObject root, string automationId)
        where T : DependencyObject =>
        FindWithAutomationId<T>(root, automationId)
            ?? throw new InvalidOperationException($"{automationId} is not in the shell's XAML.");

    private static T? FindWithAutomationId<T>(DependencyObject root, string automationId)
        where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject element)
            {
                continue;
            }

            if (element is T match && AutomationProperties.GetAutomationId(element) == automationId)
            {
                return match;
            }

            if (FindWithAutomationId<T>(element, automationId) is { } nested)
            {
                return nested;
            }
        }

        return null;
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
