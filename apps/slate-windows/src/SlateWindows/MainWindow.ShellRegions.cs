// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
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
                    // A stop that cannot be landed leaves the region
                    // unfocused, so the ring moves on to the rail.
                    _ = SelectorFocus.LandOnStop(stop);
                }

                break;
            case ShellRegionKind.RightPaneRail:
                if (!workspace.IsRightPaneVisible)
                {
                    return ShellRegionLanding.Refused;
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

    /// <summary>#1275's monitor reporting a modal loop over the shell — a
    /// message box the shell owns (the History alert, the Bases questions, the
    /// unsaved-changes prompt), a common dialog, a WPF <c>ShowDialog</c> —
    /// beginning (<see langword="true"/>) or the last one ending. It seals the
    /// palette for the loop (contract 28 T13/T14), and the loop BEGINNING is a
    /// modal opening for the editor landing (W7-7 PR 8, R-10, OD-12): the slot
    /// withdraws what it holds — the window's deactivation does not cover a
    /// box raised while the shell is not the active window, which an owned box
    /// still disables — and the one entry creates nothing while the loop runs
    /// (<see cref="FocusEditorPane"/>). The edge again: its end restores
    /// nothing.</summary>
    private void ModalLoopChanged(bool modalLoop)
    {
        _viewModel.Palette.SetModalLoop(modalLoop);
        if (modalLoop)
        {
            _editorLandings.ModalOpened();
        }
    }

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

    /// <summary>R-10's canvas and graph arms — the document half of the one
    /// landing entry (OD-12). The window's slot holds the landing BEFORE the
    /// document's request exists: its departure observer, modal hook and
    /// scope own the request from the instant it is raised, so no surface can
    /// seat or refuse it unowned. Then the tab's document is asked for its
    /// landing (addressed to the tab; <paramref name="canvasNode"/> names a
    /// canvas card, a jump's), which the document's surface seats — usually
    /// inside that request, completing it. Ended inside the request, the
    /// document's own account decides: seated is Landed (the caller speaks);
    /// released, or never raised (a retired document), is Refused (the caller
    /// goes on). A request the document still holds for the tab is Pending,
    /// EVEN with focus already in the surface: a graph whose load is in flight
    /// seats a shell request PROVISIONALLY (its state host, or the grid it
    /// still shows) and keeps the request live for the terminal delivery to
    /// re-seat. Its line is spoken when the document completes it seated, it
    /// falls through when the document lets go of it unseated, and the slot
    /// cancels it (released, so the terminal delivery reclaims nothing). Where
    /// focus sits is never read: a provisional seat puts it in the surface
    /// without the landing.</summary>
    private ShellRegionLanding LandDocument(
        WorkspaceGroupViewModel group,
        WorkspaceTabViewModel tab,
        Action? onLanded,
        Action onRefused,
        bool forTheRing,
        string? canvasNode = null)
    {
        if (DocumentSurfaceOf(tab) is null && forTheRing)
        {
            // Finding 3 (codex round 5): the ring answers NOW — a press moves on
            // to the next region — so with no surface realized it creates NO
            // request: one left behind would seat focus behind the region the
            // press moved on to. A route's request is the document's durable
            // one (contract A14), held by the window's slot until the surface's
            // realization delivers it.
            return ShellRegionLanding.Refused;
        }

        HeldDocumentLanding? held = tab switch
        {
            { IsCanvas: true, Canvas: { } canvas } => new HeldDocumentLanding(
                Dispatcher,
                () => DocumentSurfaceOf(tab),
                ContentPaneBorder,
                tab,
                canvas,
                nameof(canvas.FocusRequest),
                () => canvas.FocusRequest,
                request => ((Canvas.CanvasFocusRequest)request).Owner,
                () => canvas.LastFocusLandingEnd,
                request => canvas.ReleaseFocusLanding((Canvas.CanvasFocusRequest)request),
                onLanded,
                onRefused),
            { IsGraph: true, Graph: { } graph } => new HeldDocumentLanding(
                Dispatcher,
                () => DocumentSurfaceOf(tab),
                ContentPaneBorder,
                tab,
                graph,
                nameof(graph.FocusRequest),
                () => graph.FocusRequest,
                request => ((Graph.GraphFocusRequest)request).Owner,
                () => graph.LastFocusEnd,
                request => graph.ReleaseFocus((Graph.GraphFocusRequest)request),
                onLanded,
                onRefused),
            _ => null,
        };
        if (held is null)
        {
            return ShellRegionLanding.Refused;
        }

        // OD-12: owned first, raised second.
        _editorLandings.Hold(new HeldEditorLanding(
            target: held.Surface,
            isLive: () => held.IsHeld,
            withdraw: held.Withdraw,
            stillWhereAsked: () => IsStillWhereAsked(group, tab),
            scope: LandingScope(group),
            ringRegion: forTheRing ? ShellRegionKind.Editor : null));
        held.BeginRaising();
        try
        {
            if (tab is { IsCanvas: true, Canvas: { } canvasToAsk })
            {
                canvasToAsk.RequestFocusLanding(tab, canvasNode);
            }
            else if (tab is { IsGraph: true, Graph: { } graphToAsk })
            {
                graphToAsk.RequestFocusLanding(tab);
            }
        }
        finally
        {
            held.EndRaising();
        }

        return held.Outcome;
    }

    /// <summary>The canvas or graph surface realized for <paramref name="tab"/>
    /// in the content pane, or null.</summary>
    private FrameworkElement? DocumentSurfaceOf(WorkspaceTabViewModel tab) => tab.IsCanvas
        ? FindVisualDescendants<Canvas.CanvasSurfaceView>(ContentPaneBorder)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, tab))
        : FindVisualDescendants<Graph.GraphSurfaceView>(ContentPaneBorder)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, tab));

    /// <summary>A canvas or graph document's request an editor landing waits
    /// on (R-10), held from BEFORE it is raised (OD-12). The document ending
    /// the request is its one signal, and the document's own account of HOW
    /// decides (<see cref="DocumentLandingEnd"/>) — never where focus sits,
    /// because a graph seats a shell request provisionally while its load is
    /// in flight and keeps it pending: the line when the document records it
    /// ended SEATED (a declared terminal seat took focus, then completed it),
    /// the fall-through when it let go of it any other way (a rows-only
    /// failure or a rejection after a provisional seat, a load that failed,
    /// the document torn down while its surface still shows the tab); ended
    /// while it is being raised, neither — the entry answers Landed or Refused
    /// and its caller speaks or goes on. It is CANCELLED instead — silently,
    /// the request released so the document seats nobody later — by the
    /// window's slot (OD-12: a newer landing, the reader's departure, a modal
    /// opening, the window deactivating, the active tab changing), by a newer
    /// request in its place, and by the tab's surface leaving the tab (the
    /// shared cell rebinds on a tab switch or close) or the tree (an unload: a
    /// closed pane) — the surface the landing was held with, or one realized
    /// for the tab afterwards: while the landing has none it watches each
    /// layout pass for one, and takes that surface's lifecycle when it
    /// appears. Cancelled and Refused are exclusive: a refusal is decided after
    /// the move it may travel with, and stands only if nothing cancelled the
    /// landing first.</summary>
    private sealed class HeldDocumentLanding
    {
        private readonly System.Windows.Threading.Dispatcher _dispatcher;
        private readonly Func<FrameworkElement?> _findSurface;
        private readonly UIElement _layoutRoot;
        private readonly WorkspaceTabViewModel _tab;
        private readonly System.ComponentModel.INotifyPropertyChanged _document;
        private readonly string _requestProperty;
        private readonly Func<object?> _currentRequest;
        private readonly Func<object, object> _ownerOf;
        private readonly Func<DocumentLandingEnded?> _lastEnd;
        private readonly Action<object> _release;
        private readonly Action? _announce;
        private readonly Action _fallThrough;
        private object? _request;
        private FrameworkElement? _surface;
        private bool _watchingLayout;
        private bool _raising;
        private DocumentLandingEnded? _endedBeforeRaising;
        private DocumentLandingEnd? _endedWhileRaising;
        private bool _done;

        /// <param name="findSurface">The surface realized for the tab now, or
        /// null.</param>
        /// <param name="layoutRoot">Whose layout passes the landing watches
        /// while no surface is realized for the tab.</param>
        public HeldDocumentLanding(
            System.Windows.Threading.Dispatcher dispatcher,
            Func<FrameworkElement?> findSurface,
            UIElement layoutRoot,
            WorkspaceTabViewModel tab,
            System.ComponentModel.INotifyPropertyChanged document,
            string requestProperty,
            Func<object?> currentRequest,
            Func<object, object> ownerOf,
            Func<DocumentLandingEnded?> lastEnd,
            Action<object> release,
            Action? announce,
            Action fallThrough)
        {
            _dispatcher = dispatcher;
            _findSurface = findSurface;
            _layoutRoot = layoutRoot;
            _tab = tab;
            _document = document;
            _requestProperty = requestProperty;
            _currentRequest = currentRequest;
            _ownerOf = ownerOf;
            _lastEnd = lastEnd;
            _release = release;
            _announce = announce;
            _fallThrough = fallThrough;
            _document.PropertyChanged += RequestChanged;
            Track();
        }

        /// <summary>Whether the landing is still held: not seated, refused or
        /// withdrawn.</summary>
        public bool IsHeld => !_done;

        /// <summary>How the raise ended: Landed when the document seated it
        /// inside the raise, Pending while it holds it, Refused otherwise
        /// (released, replaced, or never raised).</summary>
        public ShellRegionLanding Outcome =>
            _endedWhileRaising == DocumentLandingEnd.Seated ? ShellRegionLanding.Landed
            : !_done && _request is not null ? ShellRegionLanding.Pending
            : ShellRegionLanding.Refused;

        /// <summary>The surface realized for the tab — the one the landing
        /// tracks, found again when it has none — or null.</summary>
        public DependencyObject? Surface()
        {
            if (!_done && _surface is null)
            {
                Track();
            }

            return _surface;
        }

        /// <summary>The document is about to raise the request: its end inside
        /// the raise is the entry's answer, not a continuation.</summary>
        public void BeginRaising()
        {
            _endedBeforeRaising = _lastEnd();
            _raising = true;
        }

        /// <summary>The raise returned. A request the document still holds for
        /// the tab is this landing's, pending. One a surface delivered INSIDE
        /// the raise's own change notification — ahead of this landing's
        /// handler, so it never saw the request — is read from the document's
        /// record of how it ended; no request and no new record: nothing was
        /// raised (a retired document refuses one).</summary>
        public void EndRaising()
        {
            _raising = false;
            if (_done || _request is not null)
            {
                return;
            }

            if (_currentRequest() is { } pending && ReferenceEquals(_ownerOf(pending), _tab))
            {
                _request = pending;
                return;
            }

            if (_lastEnd() is { } ended
                && !ReferenceEquals(ended, _endedBeforeRaising)
                && ReferenceEquals(ended.Owner, _tab))
            {
                _endedWhileRaising = ended.End;
            }

            _ = Stop();
        }

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

            if (_request is { } request)
            {
                _release(request);
            }

            return true;
        }

        private bool Stop()
        {
            if (_done)
            {
                return false;
            }

            _done = true;
            Follow(null);
            WatchLayout(false);
            _document.PropertyChanged -= RequestChanged;
            return true;
        }

        /// <summary>Take the lifecycle of the surface realized for the tab:
        /// its rebind or unload cancels the landing. With none realized, each
        /// layout pass looks again.</summary>
        private void Track()
        {
            FrameworkElement? found = _findSurface();
            Follow(found);
            WatchLayout(found is null);
        }

        private void Follow(FrameworkElement? surface)
        {
            if (ReferenceEquals(_surface, surface))
            {
                return;
            }

            if (_surface is { } previous)
            {
                previous.DataContextChanged -= SurfaceRebound;
                previous.Unloaded -= SurfaceUnloaded;
            }

            _surface = surface;
            if (surface is not null)
            {
                surface.DataContextChanged += SurfaceRebound;
                surface.Unloaded += SurfaceUnloaded;
            }
        }

        private void WatchLayout(bool watch)
        {
            if (_watchingLayout == watch)
            {
                return;
            }

            _watchingLayout = watch;
            if (watch)
            {
                _layoutRoot.LayoutUpdated += LayoutUpdated;
            }
            else
            {
                _layoutRoot.LayoutUpdated -= LayoutUpdated;
            }
        }

        private void LayoutUpdated(object? sender, EventArgs e)
        {
            if (!_done && _surface is null)
            {
                Track();
            }
        }

        private void SurfaceRebound(object sender, DependencyPropertyChangedEventArgs e) => _ = Withdraw();

        private void SurfaceUnloaded(object sender, RoutedEventArgs e) => _ = Withdraw();

        private void RequestChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != _requestProperty)
            {
                return;
            }

            object? current = _currentRequest();
            if (_request is null)
            {
                // The entry's own raise: the first request addressed to the tab
                // is this landing's.
                if (_raising && current is not null && ReferenceEquals(_ownerOf(current), _tab))
                {
                    _request = current;
                }

                return;
            }

            if (ReferenceEquals(current, _request))
            {
                return;
            }

            // Replaced by a newer request — another route asked — it is a
            // withdrawal, silent, wherever focus is: the ring's line belongs
            // to its own landing.
            if (current is not null)
            {
                _ = Stop();
                return;
            }

            // Ended SEATED, by the document's own account — a declared
            // terminal seat took focus, then completed the request — it is the
            // landing: the line (or, inside the raise, the entry's Landed).
            // Never read from where focus sits: a graph's provisional seat
            // leaves focus in the surface when a rows-only failure or a
            // rejection then releases the request unseated.
            if (_lastEnd() is { End: DocumentLandingEnd.Seated } ended && ReferenceEquals(ended.Request, _request))
            {
                _ = Stop();
                if (_raising)
                {
                    _endedWhileRaising = DocumentLandingEnd.Seated;
                    return;
                }

                _announce?.Invoke();
                return;
            }

            // Let go of unseated inside the raise: the entry's Refused.
            if (_raising)
            {
                _ = Stop();
                _endedWhileRaising = DocumentLandingEnd.Released;
                return;
            }

            // Let go of unseated later — a failure, or the document torn down.
            // A teardown travels with its tab's close or rebind, which CANCELS
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
    private IReadOnlyList<object>? SelectedFilesPath() =>
        _viewModel.FileSidebar is { SelectedNode: { } selected } ? FilesRowPath(selected) : null;

    /// <summary><paramref name="target"/> and its ancestors in the sidebar's
    /// tree, root first; null when it is not in the tree.</summary>
    private IReadOnlyList<object>? FilesRowPath(FileTreeNodeViewModel target)
    {
        if (_viewModel.FileSidebar is not { } sidebar)
        {
            return null;
        }

        var path = new List<object>();
        return PathTo(sidebar.RootNodes, target, path) ? path : null;

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

    /// <summary>
    /// W7-7 PR 7 (#1252, R-9 over R-5), codex's merge-delta check (finding
    /// 1): where the keys go when a publication removed the selected row
    /// they were on — the nearest row that survived, focused WITHOUT
    /// selecting it (a selection would open the note), else the tree's own
    /// landing: its first row unselected, or the empty tree, the region's
    /// stop (AR-6).
    /// </summary>
    private bool LandNearVanishedSelection(FileTreeNodeViewModel? survivor) =>
        (survivor is not null
            && FilesRowPath(survivor) is { } path
            && SelectorFocus.FocusRowUnselected(FilesTree, path))
        || LandOnFilesTree();

    /// <summary>The keys are on a Files-tree row a publication just removed:
    /// WPF still counts them within the tree, on a row no window shows. Its
    /// own re-evaluation would move them to the window next.</summary>
    private bool KeysOnARemovedFilesRow() =>
        FilesTree.IsKeyboardFocusWithin
        && Keyboard.FocusedElement is Visual row
        && PresentationSource.FromVisual(row) is null;

    /// <summary>Whether the keys are stranded from the Files region, so the
    /// tree's restore is to land them: on nothing, on the window that holds
    /// the tree, on the bare tree, or on a tree row a publication removed.
    /// Keys on a live row of the tree — the one a publication's selected row
    /// took them to — or held anywhere else are never taken.</summary>
    private bool KeysStrandedFromFilesTree()
    {
        IInputElement? focused = Keyboard.FocusedElement;
        return focused is null
            || ReferenceEquals(focused, Window.GetWindow(FilesTree))
            || (FilesTree.IsKeyboardFocusWithin
                && (ReferenceEquals(focused, FilesTree) || KeysOnARemovedFilesRow()));
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
    /// is kept; the Files filter's results, and Quick Open's (W7-7 PR 7),
    /// re-land like a leaf's list.
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
        // W7-7 PR 7 (#1252, R-9 over R-5), codex's merge-delta check (finding
        // 3): every rank rebuilds Quick Open's results — the rescan's silent
        // re-rank among them, under a reader on a result row. The removed
        // row's keys land on the fresh row the switcher kept selected (the
        // same path), else the first, else the search field.
        SelectorFocus.KeepKeysThroughPublications(
            QuickSwitcherResultsList,
            [QuickSwitcherResultsList],
            [],
            () => SelectorFocus.FocusFirstOrSelectedItem(QuickSwitcherResultsList) || QuickSwitcherSearchTextBox.Focus());
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

    // Named apart from FrameworkElement.Parent, which a same-named
    // method would hide (CS0108).
    private static DependencyObject? ParentOf(DependencyObject current) =>
        current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
}
