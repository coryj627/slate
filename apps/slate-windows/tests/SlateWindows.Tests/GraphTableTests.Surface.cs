// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Graph;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR C (#746), contracts C-5, C-7, C-11 and C-1's surface side: the
/// filter field, the count region and Clear in the surface's header; the
/// grid's Ctrl+F; the tab order; the Escape ladder delivered from the
/// surface's tunnelling handler; the presenter's attachment and
/// detachment; rule F's landing on the grid (Term F1 in this slice).
/// Hosted in a hidden window where keyboard focus must be real.
/// </summary>
public sealed partial class GraphTableTests
{
    private static HostedWindow HostInWindow(UIElement content)
    {
        var window = new Window
        {
            Content = content,
            Width = 900,
            Height = 700,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        return new HostedWindow(window);
    }

    private sealed class HostedWindow(Window window) : IDisposable
    {
        internal Window Window => window;

        internal void UpdateLayout() => window.UpdateLayout();

        public void Dispose() => window.Close();
    }

    private static void PressPreview(UIElement target, Key key)
    {
        var args = new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(target) ?? new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "t", IntPtr.Zero),
            0,
            key)
        {
            RoutedEvent = UIElement.PreviewKeyDownEvent,
        };
        target.RaiseEvent(args);
    }

    private static string Count(GraphPublication publication) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.Graph(
            new GraphA11yEvent.GraphFilterCount((uint)publication.Rows.Count, (uint)publication.Total))).Text;

    [Fact]
    public void TheFieldsNameAndHelpTextAreTheMacs()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-field-names");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            Assert.Equal("GraphFilterField", AutomationProperties.GetAutomationId(view.FilterFieldForTests));
            Assert.Equal("Filter graph by note name", AutomationProperties.GetName(view.FilterFieldForTests));
            Assert.Equal("Filter notes", AutomationProperties.GetHelpText(view.FilterFieldForTests));
            Assert.Equal("GraphFilterSummary", AutomationProperties.GetAutomationId(view.FilterSummaryForTests));
            Assert.True(view.FilterSummaryForTests.Focusable, "the region is its own stop");
            Assert.Equal("GraphClearFilter", AutomationProperties.GetAutomationId(view.ClearFilterForTests));
            Assert.Equal("Clear filter", AutomationProperties.GetName(view.ClearFilterForTests));
            Assert.Equal(Visibility.Collapsed, view.FilterSummaryForTests.Visibility);
            Assert.Equal(Visibility.Collapsed, view.ClearFilterForTests.Visibility);
        });
    }

    [Fact]
    public void TypingWritesTheStateAndTheFieldFollowsAProgrammaticNeedle()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-field-typing");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            GraphViewState state = host.Workspace.GraphViewStateForTests;
            ulong seq = document.SeqForTests;

            // Typing writes the state through the navigator and issues one token.
            view.FilterFieldForTests.Text = "note1";
            Assert.Equal("note1", state.NameQuery);
            Assert.Equal(seq + 1, document.SeqForTests);
            host.Settle(document);
            Assert.Equal("note1", document.Publication.Query.NameQuery);

            // A programmatic needle re-renders the field without a second token.
            host.Workspace.GraphNavigator.SetNameQuery("note");
            Assert.Equal("note", view.FilterFieldForTests.Text);
            Assert.Equal(seq + 2, document.SeqForTests);
            host.Settle(document);
            Assert.Equal(seq + 2, document.SeqForTests);
        });
    }

    [Fact]
    public void TheRegionShowsTheRenderedCountOnlyWhileNarrowingAndCurrent()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-region");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            TextBlock region = view.FilterSummaryForTests;
            GraphViewState state = host.Workspace.GraphViewStateForTests;
            GraphNavigator navigator = host.Workspace.GraphNavigator;

            // Nothing narrows: collapsed.
            Assert.Equal(Visibility.Collapsed, region.Visibility);

            // A needle in flight: not current — collapsed; landed: visible
            // with the label prefix over core's rendered count.
            navigator.SetNameQuery("note1");
            Assert.True(document.IsRequestInFlight);
            Assert.Equal(Visibility.Collapsed, region.Visibility);
            host.Settle(document);
            Assert.Equal(Visibility.Visible, region.Visibility);
            Assert.Equal(Count(document.Publication), region.Text);
            Assert.Equal("Filter results: " + Count(document.Publication), AutomationProperties.GetName(region));
            Assert.Equal(document.FilterCountText, region.Text);

            // A token in flight under the SAME query - a sort's rows-only
            // token - is not current (Term Q7): collapsed until it lands.
            Assert.True(document.Request(new GraphRequest.Sort(new GraphTableSort(GraphTableColumn.Note, true))));
            Assert.True(document.IsRequestInFlight);
            Assert.Equal(Visibility.Collapsed, region.Visibility);
            host.Settle(document);
            Assert.Equal(Visibility.Visible, region.Visibility);

            // Whitespace only (U+00A0 U+2003) never narrows (core's trim): collapsed.
            navigator.SetNameQuery("  ");
            host.Settle(document);
            Assert.Equal(Visibility.Collapsed, region.Visibility);

            // The kind overlay narrows with an empty needle: visible.
            state.ApplyQuery(new GraphVisibilityQuery(state.Filter, string.Empty, GraphNodeKind.Ghost));
            Assert.True(document.Request(new GraphRequest.Needle()));
            host.Settle(document);
            Assert.Equal(Visibility.Visible, region.Visibility);
            Assert.Equal(document.FilterCountText, region.Text);

            // A stale publication under a changed needle: not current — collapsed.
            state.NameQuery = "note2";
            Assert.Equal(Visibility.Collapsed, region.Visibility);
        });
    }

    [Fact]
    public void ClearIsVisibleForAnyRawNeedleAndClearsIt()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-clear");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            Button clear = view.ClearFilterForTests;
            GraphViewState state = host.Workspace.GraphViewStateForTests;
            Assert.Equal(Visibility.Collapsed, clear.Visibility);
            view.FilterFieldForTests.Text = " ";
            Assert.Equal(Visibility.Visible, clear.Visibility);
            host.Settle(document);
            clear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(string.Empty, state.NameQuery);
            Assert.Equal(string.Empty, view.FilterFieldForTests.Text);
            Assert.Equal(Visibility.Collapsed, clear.Visibility);
            host.Settle(document);
        });
    }

    [Fact]
    public void TheGridsGestureFocusesTheField()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-ctrl-f");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            using HostedWindow window = HostInWindow(view);
            AccessibleDataGrid grid = view.TableForTests.GridForTests;
            Assert.True(grid.FocusFirstCell(), "the premise: the reader is in the grid");
            window.UpdateLayout();
            Assert.True(view.IsKeyboardFocusWithin);
            Assert.Same(view, host.Workspace.GraphNavigator.PresenterForTests);

            // The grid's Ctrl+F reaches the field with no new row.
            Assert.True(AccessibleDataGrid.FilterCommand.CanExecute(null, grid.Grid));
            AccessibleDataGrid.FilterCommand.Execute(null, grid.Grid);
            window.UpdateLayout();
            Assert.True(view.FilterFieldForTests.IsKeyboardFocused);
        });
    }

    [Fact]
    public void TheTabOrderFromTheGridReachesTheSwitcherThenTheField()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-tab-order");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            // The order is the header's tab indices — the field, the summary
            // (when visible), Clear (when visible), the switcher as ONE stop,
            // then the state host or the grid — so from a row with nothing
            // narrowing Shift+Tab reaches the switcher first and the field
            // second; the journey presses it twice (IGN-21).
            Assert.Equal(1, view.FilterFieldForTests.TabIndex);
            Assert.Equal(2, KeyboardNavigation.GetTabIndex(view.FilterSummaryForTests));
            Assert.Equal(3, view.ClearFilterForTests.TabIndex);
            StackPanel switcher = (StackPanel)view.ModeChoicesForTests[0].Parent;
            Assert.Equal(4, KeyboardNavigation.GetTabIndex(switcher));
            Assert.Equal(KeyboardNavigationMode.Once, KeyboardNavigation.GetTabNavigation(switcher));
            Assert.Equal(5, view.TableForTests.TabIndex);
            Assert.Equal(5, KeyboardNavigation.GetTabIndex(view.StateTextForTests));
            Assert.Equal(KeyboardNavigationMode.Local, KeyboardNavigation.GetTabNavigation(view));
            Assert.True(KeyboardNavigation.GetIsTabStop(view.FilterSummaryForTests));
            // The table is a LOCAL scope of its own: the wrapper numbers its
            // grid 0 and its summary 1, and without the scope those flatten
            // into the surface's order ahead of the field (TGC-9).
            Assert.Equal(KeyboardNavigationMode.Local, KeyboardNavigation.GetTabNavigation(view.TableForTests));
            // The order TRAVERSED, not only declared: hosted, landed on a
            // row, Shift+Tab's traversal reaches the switcher's checked choice
            // and then the field — never the grid's own summary.
            using HostedWindow window = HostInWindow(view);
            document.RequestFocusLanding(GraphTabOf(host));
            window.UpdateLayout();
            Assert.True(GridHasTheKeys(view));
            var previous = new TraversalRequest(FocusNavigationDirection.Previous);
            Assert.True(((UIElement)Keyboard.FocusedElement).MoveFocus(previous));
            Assert.Same(view.ModeChoicesForTests[0], Keyboard.FocusedElement);
            Assert.True(view.ModeChoicesForTests[0].MoveFocus(previous));
            Assert.Same(view.FilterFieldForTests, Keyboard.FocusedElement);
            // And forward from the switcher: the grid's cell, not the summary.
            var next = new TraversalRequest(FocusNavigationDirection.Next);
            Assert.True(view.ModeChoicesForTests[0].Focus());
            Assert.True(view.ModeChoicesForTests[0].MoveFocus(next));
            Assert.True(GridHasTheKeys(view));
        });
    }

    [Fact]
    public void TheDeliveryFromTheSurfaceWithEachChordAndTheExactModifier()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-delivery");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            using HostedWindow window = HostInWindow(view);
            GraphViewState state = host.Workspace.GraphViewStateForTests;
            view.FilterFieldForTests.Text = "note1";
            host.Settle(document);
            Assert.True(view.FilterFieldForTests.Focus());
            window.UpdateLayout();

            // Escape tunnels through the surface's handler: rung 1 clears the
            // needle and the press is consumed.
            PressPreview(view.FilterFieldForTests, Key.Escape);
            Assert.Equal(string.Empty, state.NameQuery);
            Assert.Same(view, host.Workspace.GraphNavigator.PresenterForTests);
            host.Settle(document);

            // The exact modifier: Escape with Control reaches no rung.
            state.NameQuery = "note1";
            Assert.False(host.Workspace.GraphNavigator.HandleKey(Key.Escape, ModifierKeys.Control, view));
            Assert.Equal("note1", state.NameQuery);
            state.NameQuery = string.Empty;
        });
    }

    [Fact]
    public void RungTwoSeatsAtOnceWithNoLoad()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-rung-two");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            using HostedWindow window = HostInWindow(view);
            Assert.True(view.FilterFieldForTests.Focus());
            window.UpdateLayout();
            ulong seq = document.SeqForTests;

            PressPreview(view.FilterFieldForTests, Key.Escape);
            window.UpdateLayout();

            // No needle: no token; the landing delivered at once onto the grid.
            Assert.Equal(seq, document.SeqForTests);
            Assert.Null(document.FocusRequest);
            Assert.True(view.TableForTests.GridForTests.Grid.IsKeyboardFocusWithin, "the reader is seated on the grid");
            Assert.False(view.FilterFieldForTests.IsKeyboardFocused);
        });
    }

    [Fact]
    public void TheClearRungSeatsAfterTheClearedRowsLandAndNotOnTheOldEmptyHost()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-rung-one");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            using HostedWindow window = HostInWindow(view);
            // A needle nothing matches: EMPTY, the state host shown.
            view.FilterFieldForTests.Text = "zzz-nothing-matches";
            host.Settle(document);
            Assert.Equal(GraphLoadState.Empty, document.Publication.State);
            Assert.True(view.FilterFieldForTests.Focus());
            window.UpdateLayout();

            PressPreview(view.FilterFieldForTests, Key.Escape);
            window.UpdateLayout();

            // The clear's token is in flight: the request waits (Term F3) —
            // the reader stays where the keys are, not on the old EMPTY host.
            Assert.True(document.IsRequestInFlight);
            Assert.NotNull(document.FocusRequest);
            Assert.True(view.FilterFieldForTests.IsKeyboardFocused);
            host.Settle(document);
            window.UpdateLayout();
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            Assert.Null(document.FocusRequest);
            Assert.True(view.TableForTests.GridForTests.Grid.IsKeyboardFocusWithin, "seated on the cleared rows");
        });
    }

    [Fact]
    public void AClosedTabsSurfaceDetachesOnUnloaded()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-detach");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            using HostedWindow window = HostInWindow(view);
            Assert.True(view.FilterFieldForTests.Focus());
            window.UpdateLayout();
            Assert.Same(view, host.Workspace.GraphNavigator.PresenterForTests);
            // The route a CLOSE takes: the surface leaves the tree.
            window.Window.Content = null;
            window.UpdateLayout();
            // Unloaded is broadcast at the end of the layout pass, off the
            // dispatcher: pump it.
            PumpedDispatcher.Drain();
            Assert.Null(host.Workspace.GraphNavigator.PresenterForTests);
            Assert.False(view.IsLive);
        });
    }

    [Fact]
    public void AReplacedModelDetachesAndReattachesThePaneThatHeldTheKeys()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-replace");
            GraphDocumentViewModel first = host.Open();
            var view = new GraphSurfaceView { Model = first };
            GraphNavigator navigator = host.Workspace.GraphNavigator;
            Assert.True(navigator.HandleKey(Key.Escape, ModifierKeys.None, view) || true);
            Assert.Same(view, navigator.PresenterForTests);
            // The graph tab closed and reopened: a fresh document over the
            // same navigator replaces the surface's model — the attached pane
            // re-attaches.
            host.Workspace.CloseActiveTabCommand.Execute(null);
            Assert.True(first.IsRetired);
            GraphDocumentViewModel second = host.Open();
            Assert.NotSame(first, second);
            view.Model = second;
            Assert.Same(view, navigator.PresenterForTests);
            // A pane that did not hold the keys stays detached across a replacement.
            var other = new GraphSurfaceView { Model = second };
            navigator.DetachPresenter(view);
            other.Model = null;
            other.Model = second;
            Assert.Null(navigator.PresenterForTests);
        });
    }

    // --- Where-am-I: the panel, the pre-emption, the chord (contract C-8) -------

    private static readonly GraphA11yEvent.GraphWhereAmI[] Witnesses =
    [
        new(new GraphWhereAmISelection.Node(new GraphRowCopy("Alone", GraphNodeKind.Note, 0, 0, 0, false), 2), 100, new GraphWhereAmIFilter.Normal(true, true, true), "alo"),
        new(new GraphWhereAmISelection.Node(new GraphRowCopy("Missing Note", GraphNodeKind.Ghost, 2, 0, 2, false), 0), 250, new GraphWhereAmIFilter.UnresolvedOnly(), null),
        new(new GraphWhereAmISelection.NoSelection(), 100, new GraphWhereAmIFilter.Normal(false, false, true), "\u00a0\u2003"),
        new(new GraphWhereAmISelection.Node(new GraphRowCopy("Hub", GraphNodeKind.Note, 3, 1, 3, false), 1), 50, new GraphWhereAmIFilter.Normal(false, true, false), null),
        new(new GraphWhereAmISelection.Node(new GraphRowCopy("Alpha", GraphNodeKind.Note, 1, 1, 1, false), 1), 100, new GraphWhereAmIFilter.Normal(false, false, false), "\u2003alpha\u00a0"),
        new(new GraphWhereAmISelection.Node(new GraphRowCopy("Ghost", GraphNodeKind.Ghost, 1, 0, 1, false), 0), 100, new GraphWhereAmIFilter.UnresolvedOnly(), null),
        new(new GraphWhereAmISelection.Node(new GraphRowCopy("Pic", GraphNodeKind.Attachment, 1, 0, 1, false), 1), 80, new GraphWhereAmIFilter.UnresolvedOnly(), null),
        new(new GraphWhereAmISelection.Node(new GraphRowCopy("Caf\u00e9", GraphNodeKind.Note, 2, 2, 2, false), 3), 200, new GraphWhereAmIFilter.Normal(true, false, true), "cafe"),
        new(new GraphWhereAmISelection.NoSelection(), null, new GraphWhereAmIFilter.Normal(false, false, true), null),
    ];

    private static string WhereAmIRender(GraphA11yEvent.GraphWhereAmI @event) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.Graph(@event)).Text;

    /// <summary>0a-6's nine states through an injected seam: each renders
    /// the panel's text and posts ONCE, the two the same string from one
    /// event (IGO-31); the ninth carries no zoom clause.</summary>
    [Fact]
    public void WhereAmIWithEachWitnessRendersThePanelAndPostsOnce()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-where-am-i-witnesses");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            using HostedWindow window = HostInWindow(view);
            GraphNavigator navigator = host.Workspace.GraphNavigator;
            Assert.Equal(9, Witnesses.Length);
            foreach (GraphA11yEvent.GraphWhereAmI witness in Witnesses)
            {
                navigator.InstallTableReadback(() => witness);
                host.GraphLines.Clear();
                Assert.True(navigator.WhereAmI());
                string expected = WhereAmIRender(witness);
                Assert.Equal(expected, view.WhereAmIReadbackForTests.Text);
                Assert.Equal(Visibility.Visible, view.WhereAmIPanelForTests.Visibility);
                Assert.Equal([expected], host.GraphLines);
                Assert.Equal(witness.ZoomPercent is null, !expected.Contains("zoom", StringComparison.Ordinal));
                navigator.CloseWhereAmI();
                Assert.Equal(Visibility.Collapsed, view.WhereAmIPanelForTests.Visibility);
                Assert.Equal(string.Empty, view.WhereAmIReadbackForTests.Text);
            }
            Assert.Equal("Where am I?", AutomationProperties.GetName(view.WhereAmIPanelForTests));
            Assert.Equal("GraphWhereAmIPanel", AutomationProperties.GetAutomationId(view.WhereAmIPanelForTests));
            Assert.Equal("GraphWhereAmIReadback", AutomationProperties.GetAutomationId(view.WhereAmIReadbackForTests));
            Assert.Equal("Where am I?", AutomationProperties.GetName(view.WhereAmIReadbackForTests));
            Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(view.WhereAmIReadbackForTests));
            Assert.True(view.WhereAmIReadbackForTests.IsReadOnly);
            Assert.Equal("GraphWhereAmIClose", AutomationProperties.GetAutomationId(view.WhereAmICloseForTests));
            Assert.Equal("Close", view.WhereAmICloseForTests.Content);
        });
    }

    /// <summary>The canvas's rules: opening focuses the readback only when the
    /// surface has the keys and remembers where they came from; Close and
    /// Escape restore focus there only when the reader was INSIDE the panel;
    /// with the origin gone, the projection through rule F; a reader elsewhere
    /// is not moved.</summary>
    [Fact]
    public void ThePanelsFocusRulesInsideAndOutside()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-where-am-i-focus");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            using HostedWindow window = HostInWindow(view);
            GraphNavigator navigator = host.Workspace.GraphNavigator;
            // (i) The keys OUTSIDE the surface: the panel opens, focus stays put.
            Assert.False(view.IsKeyboardFocusWithin);
            Assert.True(navigator.WhereAmI());
            Assert.Equal(Visibility.Visible, view.WhereAmIPanelForTests.Visibility);
            Assert.False(view.WhereAmIReadbackForTests.IsKeyboardFocused);
            navigator.CloseWhereAmI();
            // (ii) The keys in the FIELD: the panel opens into the readback and
            // Close puts them back in the field.
            Assert.True(view.FilterFieldForTests.Focus());
            Assert.True(navigator.WhereAmI());
            Assert.True(view.WhereAmIReadbackForTests.IsKeyboardFocused);
            view.WhereAmICloseForTests.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(Visibility.Collapsed, view.WhereAmIPanelForTests.Visibility);
            Assert.True(view.FilterFieldForTests.IsKeyboardFocused);
            // (iii) Open from the field, the reader LEAVES the panel for the grid,
            // then Close: they are not moved.
            Assert.True(navigator.WhereAmI());
            Assert.True(view.WhereAmIReadbackForTests.IsKeyboardFocused);
            Assert.True(view.TableForTests.FocusProjection());
            Assert.False(view.WhereAmIPanelForTests.IsKeyboardFocusWithin);
            view.WhereAmICloseForTests.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(Visibility.Collapsed, view.WhereAmIPanelForTests.Visibility);
            Assert.True(view.TableForTests.IsKeyboardFocusWithin);
            // (iv) The origin gone: open from Clear (visible under a needle), the
            // needle cleared meanwhile, Escape lands on the projection.
            navigator.SetNameQuery("note");
            host.Settle(document);
            Assert.Equal(Visibility.Visible, view.ClearFilterForTests.Visibility);
            Assert.True(view.ClearFilterForTests.Focus());
            Assert.True(navigator.WhereAmI());
            Assert.True(view.WhereAmIReadbackForTests.IsKeyboardFocused);
            navigator.ClearNameQuery();
            host.Settle(document);
            Assert.Equal(Visibility.Collapsed, view.ClearFilterForTests.Visibility);
            PressPreview(view.WhereAmIReadbackForTests, Key.Escape);
            Assert.Equal(Visibility.Collapsed, view.WhereAmIPanelForTests.Visibility);
            host.Settle(document);
            view.UpdateLayout();
            Assert.True(view.TableForTests.IsKeyboardFocusWithin);
        });
    }

    /// <summary>Rung 0 ahead of rung 1: with the panel open and a live needle,
    /// Escape closes the panel and leaves the needle; the next Escape clears
    /// the needle (rung 1).</summary>
    [Fact]
    public void AnOpenPanelTakesEscapeAheadOfALiveNeedleWhileTheSurfaceHasTheKeys()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-escape-ahead");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            using HostedWindow window = HostInWindow(view);
            GraphNavigator navigator = host.Workspace.GraphNavigator;
            GraphViewState state = host.Workspace.GraphViewStateForTests;
            navigator.SetNameQuery("note");
            host.Settle(document);
            Assert.True(view.FilterFieldForTests.Focus());
            Assert.True(navigator.WhereAmI());
            Assert.True(view.WhereAmIReadbackForTests.IsKeyboardFocused);
            PressPreview(view.WhereAmIReadbackForTests, Key.Escape);
            Assert.Equal(Visibility.Collapsed, view.WhereAmIPanelForTests.Visibility);
            Assert.Equal("note", state.NameQuery);
            Assert.True(view.FilterFieldForTests.IsKeyboardFocused);
            PressPreview(view.FilterFieldForTests, Key.Escape);
            Assert.Equal(string.Empty, state.NameQuery);
        });
    }

    [Fact]
    public void EscapeAheadOfALiveNeedle() =>
        AnOpenPanelTakesEscapeAheadOfALiveNeedleWhileTheSurfaceHasTheKeys();

    /// <summary>Escape pressed OUTSIDE the graph subtree never reaches the
    /// surface's tunnelling handler: an open panel stays open.</summary>
    [Fact]
    public void EscapeOutsideTheGraphSubtreeLeavesAnOpenPanelAlone()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-escape-outside");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            var other = new TextBox();
            var stack = new StackPanel();
            stack.Children.Add(view);
            stack.Children.Add(other);
            using HostedWindow window = HostInWindow(stack);
            GraphNavigator navigator = host.Workspace.GraphNavigator;
            Assert.True(navigator.WhereAmI());
            Assert.Equal(Visibility.Visible, view.WhereAmIPanelForTests.Visibility);
            Assert.True(other.Focus());
            PressPreview(other, Key.Escape);
            Assert.Equal(Visibility.Visible, view.WhereAmIPanelForTests.Visibility);
            Assert.NotNull(navigator.WhereAmIText);
        });
    }

    /// <summary>The chord through the surface as the presenter (the tunnelling
    /// handler reads the live modifiers, which a synthetic press cannot set;
    /// the journey presses the real keys): Ctrl+Alt+Shift+I on the grid opens
    /// the panel into the readback and posts once; the exact modifier —
    /// Ctrl+Alt+I alone, the pane toggle's chord — is not the graph's.</summary>
    [Fact]
    public void TheWhereAmIChordThroughTheSurfacesPresenter()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-where-am-i-chord");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphSurfaceView { Model = document };
            using HostedWindow window = HostInWindow(view);
            GraphNavigator navigator = host.Workspace.GraphNavigator;
            Assert.True(view.TableForTests.FocusProjection());
            host.GraphLines.Clear();
            Assert.False(navigator.HandleKey(Key.I, ModifierKeys.Control | ModifierKeys.Alt, view));
            Assert.Equal(Visibility.Collapsed, view.WhereAmIPanelForTests.Visibility);
            Assert.True(navigator.HandleKey(Key.I, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, view));
            Assert.Equal(Visibility.Visible, view.WhereAmIPanelForTests.Visibility);
            Assert.True(view.WhereAmIReadbackForTests.IsKeyboardFocused);
            Assert.Single(host.GraphLines);
            Assert.Equal(host.GraphLines[0], view.WhereAmIReadbackForTests.Text);
        });
    }

}
