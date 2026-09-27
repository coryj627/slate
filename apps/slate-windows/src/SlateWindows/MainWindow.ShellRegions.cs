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
    /// region's (final review, #1240). W7-7 PR 8 (R-10): every editor arm is
    /// verified by focus in its stop; one whose content is still arriving (a
    /// reading projection, a canvas load, a graph surface not yet shown)
    /// answers <see cref="ShellRegionLanding.Pending"/> and later either
    /// speaks through <paramref name="announceWhenLanded"/> when focus
    /// arrives or calls <paramref name="fallThroughWhenRefused"/> when the
    /// landing cannot be completed.</summary>
    ShellRegionLanding IShellRegionHost.TryLand(
        ShellRegionKind region, Action announceWhenLanded, Action fallThroughWhenRefused)
    {
        if (_viewModel.Workspace is not WorkspaceViewModel workspace)
        {
            return ShellRegionLanding.Refused;
        }

        switch (region)
        {
            case ShellRegionKind.MenuBar:
                if (MainMenu.Items.Count == 0
                    || MainMenu.ItemContainerGenerator.ContainerFromIndex(0) is not MenuItem first)
                {
                    return ShellRegionLanding.Refused;
                }

                first.Focus();
                break;
            case ShellRegionKind.Files:
                _ = FilterResultsList.IsVisible
                    ? FilterResultsList.Focus() || FilesTree.Focus()
                    : FilesTree.Focus();
                break;
            case ShellRegionKind.TabBar:
                {
                    WorkspaceGroupViewModel group = workspace.ActiveGroup;
                    if (group.ActiveTab is not { } activeTab)
                    {
                        return ShellRegionLanding.Refused;
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
                // R-10: the ring's editor landing goes through the one entry
                // point every route shares, and its answer is the landing's own
                // — Landed (spoken now), Pending (held: spoken when it seats,
                // resumed from here when it is refused) or Refused (nothing
                // held, nothing moved: the press goes on).
                _ = _editorLandings.Withdraw();
                return FocusEditorPane(
                    workspace.ActiveGroup, announceWhenLanded, fallThroughWhenRefused, forTheRing: true);
            case ShellRegionKind.EmptyEditor:
                if (workspace.ActiveGroup.ActiveTab is not null)
                {
                    return ShellRegionLanding.Refused;
                }

                ContentPaneBorder.Focus();
                break;
            case ShellRegionKind.RightPaneContent:
                if (!workspace.IsRightPaneVisible)
                {
                    return ShellRegionLanding.Refused;
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
                    stop.Focus();
                }

                break;
            case ShellRegionKind.RightPaneRail:
                if (!workspace.IsRightPaneVisible)
                {
                    return ShellRegionLanding.Refused;
                }

                if (RightPaneLeavesList.SelectedItem is { } selected
                    && RightPaneLeavesList.ItemContainerGenerator.ContainerFromItem(selected) is ListBoxItem row)
                {
                    row.Focus();
                }
                else
                {
                    RightPaneLeavesList.Focus();
                }

                break;
            case ShellRegionKind.StatusBar:
                ShellStatusBar.Focus();
                break;
            default:
                return ShellRegionLanding.Refused;
        }

        return ((IShellRegionHost)this).FocusedRegion() == region
            ? ShellRegionLanding.Landed
            : ShellRegionLanding.Refused;
    }

    /// <summary>W7-7 PR 8 (R-10, OD-12; W7-6 §4's modal rule): a modal surface
    /// OPENING withdraws the editor landing the window holds, whoever asked for
    /// it — the ring, a route, a surface-less route's request — synchronously:
    /// the modal owns the keys, so nothing may seat beneath it or speak through
    /// it. The edge, not the level: a modal's own changes while it stays open
    /// (the palette clearing its query as a command's route runs) leave what
    /// its commit raised to the landing that route queued, and a route landing
    /// is never created while a modal is open (<see cref="LandEditorForRoute"/>).
    /// Every view model whose flag feeds <see cref="OpenModalSurface"/> reports
    /// its changes here.</summary>
    private void ModalSource_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        bool open = OpenModalSurface is not null;
        bool opened = open && !_modalSurfaceWasOpen;
        _modalSurfaceWasOpen = open;
        if (opened)
        {
            _editorLandings.ModalOpened();
        }
    }

    /// <summary>Whether a modal surface was open at the last report (the
    /// modal hook's edge).</summary>
    private bool _modalSurfaceWasOpen;

    /// <summary>R-10 (OD-12): the ONE editor landing this window holds, whoever
    /// asked for it, and everything that cancels it.</summary>
    private readonly EditorLandingSlot _editorLandings;

    /// <summary>The window's landing slot, for the facts that hold a landing
    /// of their own in it.</summary>
    internal EditorLandingSlot EditorLandings => _editorLandings;

    bool IShellRegionHost.HoldsLanding => _editorLandings.Held is not null;

    ShellRegionKind? IShellRegionHost.HeldRingRegion => _editorLandings.Held?.RingRegion;

    bool IShellRegionHost.WithdrawHeldLanding() => _editorLandings.Withdraw();

    /// <summary>What a held landing asked for group <paramref name="group"/>
    /// is scoped to: the workspace (its active group) and the group (its active
    /// tab).</summary>
    private IReadOnlyList<System.ComponentModel.INotifyPropertyChanged> LandingScope(WorkspaceGroupViewModel group) =>
        _viewModel.Workspace is WorkspaceViewModel workspace ? [workspace, group] : [group];

    /// <summary>Whether <paramref name="group"/> is still the active group and
    /// <paramref name="tab"/> its active tab — where a held landing was asked
    /// for.</summary>
    private bool IsStillWhereAsked(WorkspaceGroupViewModel group, WorkspaceTabViewModel? tab) =>
        _viewModel.Workspace is WorkspaceViewModel workspace
        && ReferenceEquals(workspace.ActiveGroup, group)
        && ReferenceEquals(group.ActiveTab, tab);

    /// <summary>R-10's canvas and graph arms. The tab's document is asked for
    /// its landing, which the document's surface seats — usually inside that
    /// request, completing it. A request the document still holds for the tab
    /// is Pending, EVEN with focus already in the surface: a graph whose load is
    /// in flight seats a shell request PROVISIONALLY (its state host, or the
    /// grid it still shows) and keeps the request live for the terminal
    /// delivery to re-seat. The window's slot holds it (OD-12) — with or without
    /// a realized surface; its line is spoken when the document completes it
    /// seated, it falls through when the document lets go of it unseated, and
    /// the slot cancels it (released, so the terminal delivery reclaims
    /// nothing). Ended inside the request, the document's own account decides:
    /// a request of THIS landing it records as
    /// <see cref="DocumentLandingEnd.Seated"/> is Landed; anything else is
    /// Refused (released unseated, or never taken: retired, shut down). Where
    /// focus sits is never read: a provisional seat puts it in the surface
    /// without the landing.</summary>
    private ShellRegionLanding LandDocument(
        WorkspaceGroupViewModel group, WorkspaceTabViewModel tab, Action? onLanded, Action onRefused, bool forTheRing)
    {
        FrameworkElement? surface = DocumentSurfaceOf(tab);
        if (surface is null && forTheRing)
        {
            // Finding 3 (codex round 5): the ring answers NOW — a press moves on
            // to the next region — so with no surface realized it creates NO
            // request: one left behind would seat focus behind the region the
            // press moved on to. A route's request is the document's durable
            // one (contract A14), held by the window's slot until the surface's
            // realization delivers it.
            return ShellRegionLanding.Refused;
        }

        // How the document last ended a request, so the answer below reads how
        // THIS landing's request ended.
        DocumentLandingEnded? endedBefore = LastDocumentLandingEnd(tab);
        if (tab is { IsCanvas: true, Canvas: { } canvasToAsk })
        {
            canvasToAsk.RequestFocusLanding(tab);
        }
        else if (tab is { IsGraph: true, Graph: { } graphToAsk })
        {
            graphToAsk.RequestFocusLanding(tab);
        }

        if (HoldDocumentRequest(group, tab, onLanded, onRefused, forTheRing ? ShellRegionKind.Editor : null))
        {
            return ShellRegionLanding.Pending;
        }

        return LastDocumentLandingEnd(tab) is { End: DocumentLandingEnd.Seated } ended
            && !ReferenceEquals(ended, endedBefore)
            && ReferenceEquals(ended.Owner, tab)
            ? ShellRegionLanding.Landed
            : ShellRegionLanding.Refused;
    }

    /// <summary>R-10 (OD-12): hold the request <paramref name="tab"/>'s canvas or
    /// graph document holds for it — pending, whoever raised it — in the
    /// window's slot, answering whether there was one. The slot resolves the
    /// tab's surface each time it reads a move, so a surface realized after the
    /// request still takes its entry.</summary>
    private bool HoldDocumentRequest(
        WorkspaceGroupViewModel group,
        WorkspaceTabViewModel tab,
        Action? onLanded,
        Action onRefused,
        ShellRegionKind? ringRegion)
    {
        FrameworkElement? surface = DocumentSurfaceOf(tab);
        HeldDocumentLanding? held = tab switch
        {
            { IsCanvas: true, Canvas: { FocusRequest: { } request } canvas }
                when ReferenceEquals(request.Owner, tab)
                => new HeldDocumentLanding(
                    Dispatcher, surface, canvas, nameof(canvas.FocusRequest), request, () => canvas.FocusRequest,
                    () => canvas.LastFocusLandingEnd, () => canvas.ReleaseFocusLanding(request),
                    onLanded, onRefused),
            { IsGraph: true, Graph: { FocusRequest: { } request } graph }
                when ReferenceEquals(request.Owner, tab)
                => new HeldDocumentLanding(
                    Dispatcher, surface, graph, nameof(graph.FocusRequest), request, () => graph.FocusRequest,
                    () => graph.LastFocusEnd, () => graph.ReleaseFocus(request),
                    onLanded, onRefused),
            _ => null,
        };
        if (held is null)
        {
            return false;
        }

        FrameworkElement? seen = surface;
        _editorLandings.Hold(new HeldEditorLanding(
            target: () =>
            {
                if (seen is null || !ReferenceEquals(seen.DataContext, tab) || !seen.IsLoaded)
                {
                    seen = DocumentSurfaceOf(tab);
                }

                return seen;
            },
            isLive: () => held.IsHeld,
            withdraw: held.Withdraw,
            stillWhereAsked: () => IsStillWhereAsked(group, tab),
            scope: LandingScope(group),
            ringRegion: ringRegion));
        return true;
    }

    /// <summary>The canvas or graph surface realized for <paramref name="tab"/>
    /// in the content pane, or null.</summary>
    private FrameworkElement? DocumentSurfaceOf(WorkspaceTabViewModel tab) => tab.IsCanvas
        ? FindVisualDescendants<Canvas.CanvasSurfaceView>(ContentPaneBorder)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, tab))
        : FindVisualDescendants<Graph.GraphSurfaceView>(ContentPaneBorder)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, tab));

    /// <summary>The canvas or graph document's own record of the last
    /// request it ended (R-10), or null for any other tab.</summary>
    private static DocumentLandingEnded? LastDocumentLandingEnd(WorkspaceTabViewModel tab) => tab switch
    {
        { IsCanvas: true, Canvas: { } canvas } => canvas.LastFocusLandingEnd,
        { IsGraph: true, Graph: { } graph } => graph.LastFocusEnd,
        _ => null,
    };

    /// <summary>A canvas or graph document's request an editor landing waits
    /// on (R-10). The document ending the request is its one signal, and the
    /// document's own account of HOW decides (<see cref="DocumentLandingEnd"/>)
    /// — never where focus sits, because a graph seats a shell request
    /// provisionally while its load is in flight and keeps it pending: the line
    /// when the document records it ended SEATED (a declared terminal seat took
    /// focus, then completed it), the fall-through when it let go of it any
    /// other way (a rows-only failure or a rejection after a provisional seat, a
    /// load that failed, the document torn down while its surface still shows
    /// the tab). It is CANCELLED instead — silently, the request released so the
    /// document seats nobody later — by the window's slot (OD-12: a newer
    /// landing, the reader's departure, a modal opening, the window
    /// deactivating, the active tab changing), by a newer request in its place,
    /// and by the surface it was held with leaving the tab (the shared cell
    /// rebinds on a tab switch or close) or the tree (an unload: a closed pane).
    /// Cancelled and Refused are exclusive: a refusal is decided after the move
    /// it may travel with, and stands only if nothing cancelled the landing
    /// first.</summary>
    private sealed class HeldDocumentLanding
    {
        private readonly System.Windows.Threading.Dispatcher _dispatcher;
        private readonly FrameworkElement? _surface;
        private readonly System.ComponentModel.INotifyPropertyChanged _document;
        private readonly string _requestProperty;
        private readonly object _request;
        private readonly Func<object?> _currentRequest;
        private readonly Func<DocumentLandingEnded?> _lastEnd;
        private readonly Action _release;
        private readonly Action? _announce;
        private readonly Action _fallThrough;
        private bool _done;

        /// <param name="surface">The surface realized for the tab when the
        /// landing was held; null for a route's request whose surface is not
        /// realized yet — the document's durable request, which its realization
        /// delivers.</param>
        public HeldDocumentLanding(
            System.Windows.Threading.Dispatcher dispatcher,
            FrameworkElement? surface,
            System.ComponentModel.INotifyPropertyChanged document,
            string requestProperty,
            object request,
            Func<object?> currentRequest,
            Func<DocumentLandingEnded?> lastEnd,
            Action release,
            Action? announce,
            Action fallThrough)
        {
            _dispatcher = dispatcher;
            _surface = surface;
            _document = document;
            _requestProperty = requestProperty;
            _request = request;
            _currentRequest = currentRequest;
            _lastEnd = lastEnd;
            _release = release;
            _announce = announce;
            _fallThrough = fallThrough;
            _document.PropertyChanged += RequestChanged;
            if (surface is not null)
            {
                surface.DataContextChanged += SurfaceRebound;
                surface.Unloaded += SurfaceUnloaded;
            }
        }

        /// <summary>Whether the landing is still held: not seated, refused or
        /// withdrawn.</summary>
        public bool IsHeld => !_done;

        /// <summary>Withdraw the landing; answers whether it was still held.
        /// (One its document let go of still counts until the refusal is
        /// decided: a press that cancels it goes on past the editor, where
        /// the refusal would have resumed.)</summary>
        public bool Withdraw()
        {
            if (!Stop())
            {
                return false;
            }

            _release();
            return true;
        }

        private bool Stop()
        {
            if (_done)
            {
                return false;
            }

            _done = true;
            if (_surface is not null)
            {
                _surface.DataContextChanged -= SurfaceRebound;
                _surface.Unloaded -= SurfaceUnloaded;
            }
            _document.PropertyChanged -= RequestChanged;
            return true;
        }

        private void SurfaceRebound(object sender, DependencyPropertyChangedEventArgs e) => _ = Withdraw();

        private void SurfaceUnloaded(object sender, RoutedEventArgs e) => _ = Withdraw();

        private void RequestChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != _requestProperty || ReferenceEquals(_currentRequest(), _request))
            {
                return;
            }

            // Replaced by a newer request — another route asked — it is a
            // withdrawal, silent, wherever focus is: the ring's line belongs
            // to its own landing.
            if (_currentRequest() is not null)
            {
                _ = Stop();
                return;
            }

            // Ended SEATED, by the document's own account — a declared
            // terminal seat took focus, then completed the request — it is the
            // landing: the line. Never read from where focus sits: a graph's
            // provisional seat leaves focus in the surface when a rows-only
            // failure or a rejection then releases the request unseated.
            if (_lastEnd() is { End: DocumentLandingEnd.Seated } ended && ReferenceEquals(ended.Request, _request))
            {
                _ = Stop();
                _announce?.Invoke();
                return;
            }

            // Let go of unseated — a failure, or the document torn down. A
            // teardown travels with its tab's close or rebind, which CANCELS
            // the landing, so the refusal is decided once that move has run:
            // it stands (the press resumes past the editor) only if nothing
            // ended the landing first; otherwise it is stale. One terminal
            // transition per landing, in either order.
            _ = _dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                () =>
                {
                    if (Stop())
                    {
                        _fallThrough();
                    }
                });
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

    // Named apart from FrameworkElement.Parent, which a same-named
    // method would hide (CS0108).
    private static DependencyObject? ParentOf(DependencyObject current) =>
        current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
}
