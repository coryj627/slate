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
                if (workspace.ActiveGroup.ActiveTab is not { } editorTab)
                {
                    return ShellRegionLanding.Refused;
                }

                FocusEditorPane(workspace.ActiveGroup, announceWhenLanded, fallThroughWhenRefused);
                // Canvas and graph tabs seat focus through their document's own
                // landing, most often inside the request itself; R-10 verifies it
                // before the ring speaks and holds what is still to come.
                if (editorTab is { IsCanvas: true } or { IsGraph: true })
                {
                    return DocumentLanding(editorTab, announceWhenLanded, fallThroughWhenRefused);
                }

                // A reading-mode tab's stop is its surface (R-10), verified by
                // focus in it: an applied note — the empty one included — lands
                // now. One whose projection is still arriving HOLDS the landing:
                // no line until focus arrives, and no refusal — the ring would
                // move on and the held landing then pull focus back.
                if (editorTab.IsReadingMode)
                {
                    if (ReadingSurfaceOf(editorTab) is not { } reading)
                    {
                        return ShellRegionLanding.Refused;
                    }

                    if (reading.IsKeyboardFocusWithin)
                    {
                        return ShellRegionLanding.Landed;
                    }

                    if (reading.IsFocusLandingPending)
                    {
                        _withdrawHeldLanding = () => reading.CancelFocusLanding(announceWhenLanded);
                        return ShellRegionLanding.Pending;
                    }

                    return ShellRegionLanding.Refused;
                }

                // The text editor is synchronous, so its end state is judged like
                // every other region (it can fall back to the tab item or the
                // Files tree, which must read as a refusal).
                return ((IShellRegionHost)this).FocusedRegion() == ShellRegionKind.Editor
                    ? ShellRegionLanding.Landed
                    : ShellRegionLanding.Refused;
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

    /// <summary>W7-7 PR 8 (R-10; W7-6 §4's modal rule): a modal surface
    /// opening withdraws the landing the F6 ring holds, synchronously — the
    /// modal owns the keys, so the ring's late completion must neither seat
    /// focus beneath it nor speak through it. Every view model whose flag
    /// feeds <see cref="OpenModalSurface"/> reports its changes here.</summary>
    private void ModalSource_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (_viewModel.Workspace is { HoldsShellRegionLanding: true } workspace
            && OpenModalSurface is not null)
        {
            workspace.WithdrawHeldShellRegionLanding();
        }
    }

    bool IShellRegionHost.WithdrawHeldLanding()
    {
        Func<bool>? withdraw = _withdrawHeldLanding;
        _withdrawHeldLanding = null;
        return withdraw?.Invoke() ?? false;
    }

    /// <summary>How the landing the last Pending answer holds is let go of
    /// (R-10), answering whether it was still held; a stale one is harmless —
    /// each lets go of its own request only.</summary>
    private Func<bool>? _withdrawHeldLanding;

    /// <summary>R-10's canvas and graph arms. FocusEditorPane asked the tab's
    /// document for its landing, which the document's surface seats — usually
    /// inside that request, completing it. A request the document still
    /// holds for the tab is Pending, EVEN with focus already in the surface:
    /// a graph whose load is in flight seats a shell request PROVISIONALLY
    /// (its state host, or the grid it still shows) and keeps the request
    /// live for the terminal delivery to re-seat. Its line is spoken when the
    /// document completes it seated, it falls through when the document lets
    /// go of it unseated, and a newer press or request withdraws it —
    /// releasing it, so the terminal delivery reclaims nothing. Completed
    /// inside the request with focus in THIS tab's surface is Landed; anything
    /// else is Refused (the document would not take the landing: retired,
    /// shut down).</summary>
    private ShellRegionLanding DocumentLanding(
        WorkspaceTabViewModel tab, Action announceWhenLanded, Action fallThroughWhenRefused)
    {
        FrameworkElement? surface = tab.IsCanvas
            ? FindVisualDescendants<Canvas.CanvasSurfaceView>(ContentPaneBorder)
                .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, tab))
            : FindVisualDescendants<Graph.GraphSurfaceView>(ContentPaneBorder)
                .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, tab));
        if (surface is null)
        {
            return ShellRegionLanding.Refused;
        }

        HeldDocumentLanding? held = tab switch
        {
            { IsCanvas: true, Canvas: { FocusRequest: { } request } canvas }
                when ReferenceEquals(request.Owner, tab)
                => new HeldDocumentLanding(
                    surface, canvas, nameof(canvas.FocusRequest), request, () => canvas.FocusRequest,
                    () => canvas.CompleteFocusLanding(request), announceWhenLanded, fallThroughWhenRefused),
            { IsGraph: true, Graph: { FocusRequest: { } request } graph }
                when ReferenceEquals(request.Owner, tab)
                => new HeldDocumentLanding(
                    surface, graph, nameof(graph.FocusRequest), request, () => graph.FocusRequest,
                    () => graph.CompleteFocus(request), announceWhenLanded, fallThroughWhenRefused),
            _ => null,
        };
        if (held is not null)
        {
            _withdrawHeldLanding = held.Withdraw;
            return ShellRegionLanding.Pending;
        }

        return surface.IsKeyboardFocusWithin ? ShellRegionLanding.Landed : ShellRegionLanding.Refused;
    }

    /// <summary>A canvas or graph landing the ring is waiting on (R-10). The
    /// document completing the request is its one signal — the surface seats
    /// a request, then completes it, from every edge that re-asks, its own
    /// focus-within edge included, so focus arriving is never seen first: the
    /// line when the completion finds focus in the surface, the fall-through
    /// when the document let go of the request unseated (a failure, or the
    /// document torn down — a pane closed under it drops the tab's request
    /// with the tab). It is withdrawn instead, releasing the request so the
    /// document seats nobody later, by a newer press, by a newer request in
    /// its place, by the surface leaving the tab (the shared cell rebinds on
    /// a tab switch), and the moment the reader leaves the element the press
    /// found them on (<see cref="FocusDepartureWatch"/>).</summary>
    private sealed class HeldDocumentLanding
    {
        private readonly FrameworkElement _surface;
        private readonly System.ComponentModel.INotifyPropertyChanged _document;
        private readonly string _requestProperty;
        private readonly object _request;
        private readonly Func<object?> _currentRequest;
        private readonly Action _release;
        private readonly Action _announce;
        private readonly Action _fallThrough;
        private readonly FocusDepartureWatch _departure;
        private bool _done;

        public HeldDocumentLanding(
            FrameworkElement surface,
            System.ComponentModel.INotifyPropertyChanged document,
            string requestProperty,
            object request,
            Func<object?> currentRequest,
            Action release,
            Action announce,
            Action fallThrough)
        {
            _surface = surface;
            _document = document;
            _requestProperty = requestProperty;
            _request = request;
            _currentRequest = currentRequest;
            _release = release;
            _announce = announce;
            _fallThrough = fallThrough;
            _surface.DataContextChanged += SurfaceRebound;
            _document.PropertyChanged += RequestChanged;
            _departure = new FocusDepartureWatch(surface, () => _ = Withdraw());
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
            _surface.DataContextChanged -= SurfaceRebound;
            _document.PropertyChanged -= RequestChanged;
            _departure.Dispose();
            return true;
        }

        private void SurfaceRebound(object sender, DependencyPropertyChangedEventArgs e) => _ = Withdraw();

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

            // Completed seated — focus is in the surface, and the document
            // completes only after the seat — it is the landing: the line.
            if (_surface.IsKeyboardFocusWithin)
            {
                _ = Stop();
                _announce();
                return;
            }

            // Let go of unseated — a failure, or the document torn down. A
            // teardown travels with its tab's close or rebind, which CANCELS
            // the landing, so the refusal is decided once that move has run:
            // it stands (the press resumes past the editor) only if nothing
            // ended the landing first; otherwise it is stale. One terminal
            // transition per landing, in either order.
            _ = _surface.Dispatcher.BeginInvoke(
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
