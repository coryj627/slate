// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Graph;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR C (#746), contract C-17 — rule F Terms F2–F6: the landing over
/// the surface in a hidden window — the arms (the grid's row, the state
/// host), the provisional seats under a token in flight, a request raised
/// with no load delivered at once, the withdrawal on a rows-only failure
/// and a rejection, the departure (a pane change withdraws a restoration)
/// and the hold (an overlay keeps it; the return delivers), the effective
/// rule (a graph visible in another pane never takes the keys), the
/// shell's routes, silence, the retired document.
/// </summary>
public sealed partial class GraphTableTests
{
    /// <summary>The parked fetch (the request-lineage tests' Park).</summary>
    private sealed class ParkedFetch : IDisposable
    {
        private readonly ManualResetEventSlim _release = new(false);
        private readonly ManualResetEventSlim _reached = new(false);
        private int _fetches;

        public ParkedFetch(GraphDocumentViewModel document, int which)
        {
            document.FetchGateForTests = () =>
            {
                if (Interlocked.Increment(ref _fetches) == which)
                {
                    _reached.Set();
                    _release.Wait(TimeSpan.FromSeconds(10));
                }
            };
        }

        public void WaitReached() =>
            Assert.True(_reached.Wait(TimeSpan.FromSeconds(10)), "the parked fetch never reached the gate");

        public void Release() => _release.Set();

        public void Dispose()
        {
            _release.Set();
            _release.Dispose();
            _reached.Dispose();
        }
    }

    private static WorkspaceTabViewModel GraphTabOf(Host host) =>
        host.Workspace.Groups.SelectMany(g => g.Tabs).First(t => t.IsGraph);

    /// <summary>The surface as the shell hosts it: its DataContext the graph
    /// tab — the owner key the shell's routes address.</summary>
    private static GraphSurfaceView SurfaceFor(Host host, GraphDocumentViewModel document) =>
        new() { Model = document, DataContext = GraphTabOf(host) };

    /// <summary>WPF raises Loaded and Unloaded through a dispatcher
    /// operation at Loaded priority, so a layout pass alone does not deliver
    /// them: flush that queue before reading what they did.</summary>
    private static void PumpLoadedState() =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            () => { }, System.Windows.Threading.DispatcherPriority.Loaded);

    private static bool GridHasTheKeys(GraphSurfaceView view) => view.TableForTests.GridForTests.Grid.IsKeyboardFocusWithin;

    // --- The arms (Term F4) and the at-once delivery (Term F2) -------------------

    [Fact]
    public void AFreshOpenLandsFocusOnTheGridsRow()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-fresh");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            int lines = host.GraphLines.Count;
            // The CURRENT row, not the first: the shared key names the second.
            GraphTableRow second = document.Publication.Rows[1];
            Assert.True(document.SelectRow(second.StableKey));
            // The shell's route after the open: the workspace addresses the
            // graph tab's document (Term F6) — delivered onto the grid's row.
            host.Workspace.RequestActiveEditorFocus();
            window.UpdateLayout();
            Assert.Null(document.FocusRequest);
            Assert.True(GridHasTheKeys(view));
            Assert.Same(second, view.TableForTests.GridForTests.Grid.CurrentItem);
            Assert.Equal(lines, host.GraphLines.Count);
        });
    }

    [Fact]
    public void ARePublicationUnderNoSharedKeyKeepsTheLandedRowCurrentAndEnterOpensIt()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-republish");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            host.Workspace.RequestActiveEditorFocus();
            window.UpdateLayout();
            Assert.True(GridHasTheKeys(view));
            // The landing seated the first row silently: no shared key.
            Assert.Null(document.ViewState.SelectedKey);
            AccessibleDataGrid grid = view.TableForTests.GridForTests;
            GraphTableRow landed = Assert.IsType<GraphTableRow>(grid.Grid.CurrentCell.Item);
            // The journey's sort: a rows-only re-publication under the same
            // null key. The wrapper restores the reader's row by identity and
            // the re-seat leaves it — the row stays CURRENT, so Enter opens it
            // (A-9) instead of acting on nothing.
            Assert.True(document.Request(new GraphRequest.Sort(new GraphTableSort(GraphTableColumn.Note, true))));
            host.Settle(document);
            window.UpdateLayout();
            Assert.Null(document.ViewState.SelectedKey);
            GraphTableRow current = Assert.IsType<GraphTableRow>(grid.Grid.CurrentCell.Item);
            Assert.Equal(landed.StableKey, current.StableKey);
            Assert.Contains(document.Publication.Rows, row => ReferenceEquals(row, current));
            Assert.True(grid.ActivateCurrentRow(modified: false));
            Assert.Equal(current.Path, host.Workspace.ActiveGroup.ActiveTab!.Path);
        });
    }

    [Fact]
    public void ARequestRaisedWithNoLoadIsDeliveredAtOnce()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-at-once");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            Assert.False(document.IsRequestInFlight);
            document.RequestFocusLanding(GraphTabOf(host));
            // No load followed: the request's own change delivered it.
            Assert.Null(document.FocusRequest);
            Assert.True(GridHasTheKeys(view));
        });
    }

    [Fact]
    public void DeliverySaysNothing()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-silent");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            GraphViewState state = host.Workspace.GraphViewStateForTests;
            string? key = state.SelectedKey;
            host.GraphLines.Clear();
            document.RequestFocusLanding(GraphTabOf(host));
            Assert.True(GridHasTheKeys(view));
            Assert.Empty(host.GraphLines);
            Assert.Equal(key, state.SelectedKey);
        });
    }

    [Fact]
    public void TheStateHostHasAGroupPeerNamedByTheState()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-host");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            // EMPTY: a needle nothing matches.
            host.Workspace.GraphNavigator.SetNameQuery("zzz-nothing-matches");
            host.Settle(document);
            Assert.Equal(GraphLoadState.Empty, document.Publication.State);
            GraphStateHost stateHost = view.StateHostForTests;
            Assert.Equal(Visibility.Visible, stateHost.Visibility);
            Assert.True(stateHost.Focusable);
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(stateHost)!;
            Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
            Assert.Equal(GraphSurfaceView.EmptyText, peer.GetName());
            Assert.Equal("GraphStateHost", peer.GetAutomationId());
            Assert.True(peer.IsKeyboardFocusable());
            document.RequestFocusLanding(GraphTabOf(host));
            Assert.Null(document.FocusRequest);
            Assert.True(stateHost.IsKeyboardFocused);
            // ERROR: a pair that fails names the host by the error's accessible name.
            document.FetchGateForTests = () => throw new InvalidOperationException("injected pair failure");
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.Orphans)));
            host.Settle(document);
            Assert.Equal(GraphLoadState.Error, document.Publication.State);
            Assert.StartsWith(GraphSurfaceView.ErrorAccessiblePrefix, peer.GetName(), StringComparison.Ordinal);
            document.RequestFocusLanding(GraphTabOf(host));
            Assert.Null(document.FocusRequest);
            Assert.True(stateHost.IsKeyboardFocused);
        });
    }

    // --- Term F3: currency, the provisional seats, the terminal deliveries ------

    [Fact]
    public void AnOpenUnderLoadingSeatsTheStateHostProvisionallyThenTheRowWhenCurrent()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-loading");
            // A bare document whose FIRST pair is parked: LOADING with nothing held.
            var document = new GraphDocumentViewModel(
                host.Session,
                new GraphAnnouncer(line => host.GraphLines.Add(line.Text)),
                new GraphViewState(),
                isEffectiveActive: () => true,
                verbosity: () => GraphVerbosity.Standard);
            using var park = new ParkedFetch(document, 1);
            var owner = new object();
            var view = new GraphSurfaceView { Model = document, DataContext = owner };
            using HostedWindow window = HostInWindow(view);
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            park.WaitReached();
            Assert.Equal(GraphLoadState.Loading, document.Publication.State);
            Assert.False(document.Publication.HoldsSnapshot);
            // A SHELL request under LOADING: the state host provisionally, the
            // request still pending.
            document.RequestFocusLanding(owner);
            Assert.True(view.StateHostForTests.IsKeyboardFocused);
            Assert.NotNull(document.FocusRequest);
            park.Release();
            PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
            window.UpdateLayout();
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            Assert.Null(document.FocusRequest);
            Assert.True(GridHasTheKeys(view));
            document.Retire();
        });
    }

    [Fact]
    public void AnOldReadyPublicationUnderAChangedQueryIsNotALanding()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-old-ready");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            using var park = new ParkedFetch(document, 1);
            host.Workspace.GraphNavigator.SetNameQuery("note");
            park.WaitReached();
            Assert.True(document.IsRequestInFlight);
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            // The shell's request: seated on the HELD grid provisionally, and NOT
            // completed by the old publication.
            document.RequestFocusLanding(GraphTabOf(host));
            Assert.NotNull(document.FocusRequest);
            Assert.True(GridHasTheKeys(view));
            park.Release();
            host.Settle(document);
            window.UpdateLayout();
            Assert.Equal("note", document.Publication.Query.NameQuery);
            Assert.Null(document.FocusRequest);
            Assert.True(GridHasTheKeys(view));
        });
    }

    [Fact]
    public void AnOldEmptyPublicationUnderAChangedQueryIsNotALanding()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-old-empty");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            host.Workspace.GraphNavigator.SetNameQuery("zzz-nothing-matches");
            host.Settle(document);
            Assert.Equal(GraphLoadState.Empty, document.Publication.State);
            using var park = new ParkedFetch(document, 1);
            host.Workspace.GraphNavigator.SetNameQuery("note");
            park.WaitReached();
            // The shell's request under the HELD EMPTY host: the host
            // provisionally, the request pending; the rows land it on the grid.
            document.RequestFocusLanding(GraphTabOf(host));
            Assert.NotNull(document.FocusRequest);
            Assert.True(view.StateHostForTests.IsKeyboardFocused);
            park.Release();
            host.Settle(document);
            window.UpdateLayout();
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            Assert.Null(document.FocusRequest);
            Assert.True(GridHasTheKeys(view));
        });
    }

    [Fact]
    public void TheEscapeSeatWaitsForTheClearedRows() => TheClearRungSeatsAfterTheClearedRowsLandAndNotOnTheOldEmptyHost();

    [Fact]
    public void ARejectionWithdrawsThePendingRequest()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-rejection");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            Assert.True(view.FilterFieldForTests.Focus());
            using var park = new ParkedFetch(document, 1);
            Assert.True(document.Request(new GraphRequest.Sort(new GraphTableSort(GraphTableColumn.Note, true))));
            park.WaitReached();
            GraphLoadToken token = document.CurrentForTests!;
            // A presenter's restoration, waiting on the sort's token.
            view.RequestProjectionFocus();
            Assert.NotNull(document.FocusRequest);
            Assert.True(view.FilterFieldForTests.IsKeyboardFocused);
            // The envelope for the CURRENT token whose query is not the request's: a REJECTION.
            GraphVisibilityQuery foreign = token.Request.Query with { NameQuery = "not-the-request" };
            GraphTableRows rows = host.Session.GraphTableRows(foreign, token.Request.Sort);
            document.ReceiveForTests(new GraphLoadEnvelope(token, foreign.Filter, foreign, token.Request.Sort, null, rows, null));
            Assert.False(document.IsRequestInFlight);
            // Withdrawn: the reader stays where the keys are.
            Assert.Null(document.FocusRequest);
            Assert.Null(view.DeferredRestorationForTests);
            Assert.True(view.FilterFieldForTests.IsKeyboardFocused);
            park.Release();
            host.Settle(document);
            Assert.True(view.FilterFieldForTests.IsKeyboardFocused);
        });
    }

    [Fact]
    public void ARowsOnlyFailureWithdrawsAndAPairFailureLandsOnTheErrorHost()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-failures");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            Assert.True(view.FilterFieldForTests.Focus());
            // (i) A rows-only token that FAILS: the pending request is withdrawn,
            // the old publication stands, the reader stays in the field.
            using var reached = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            document.FetchGateForTests = () =>
            {
                reached.Set();
                release.Wait(TimeSpan.FromSeconds(10));
                throw new InvalidOperationException("injected rows failure");
            };
            Assert.True(document.Request(new GraphRequest.Sort(new GraphTableSort(GraphTableColumn.Note, true))));
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
            // The presenter's restoration (a shell request would take a provisional seat).
            view.RequestProjectionFocus();
            Assert.NotNull(document.FocusRequest);
            Assert.Same(document.FocusRequest, view.DeferredRestorationForTests);
            release.Set();
            host.Settle(document);
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            Assert.Null(document.FocusRequest);
            Assert.True(view.FilterFieldForTests.IsKeyboardFocused);
            // (ii) A PAIR that fails: ERROR, and the request lands on the state host.
            document.FetchGateForTests = () => throw new InvalidOperationException("injected pair failure");
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.Orphans)));
            document.RequestFocusLanding(GraphTabOf(host));
            host.Settle(document);
            window.UpdateLayout();
            Assert.Equal(GraphLoadState.Error, document.Publication.State);
            Assert.Null(document.FocusRequest);
            Assert.True(view.StateHostForTests.IsKeyboardFocused);
        });
    }

    [Fact]
    public void ALaterRequestSupersedesAnEarlier()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-supersede");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            using var park = new ParkedFetch(document, 1);
            host.Workspace.GraphNavigator.SetNameQuery("note");
            park.WaitReached();
            var elsewhere = new object();
            document.RequestFocusLanding(GraphTabOf(host));
            GraphFocusRequest first = document.FocusRequest!;
            // The same owner raised again is a NEW request by reference — the
            // record's value equality must not swallow it (Term F1).
            int raised = 0;
            document.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(GraphDocumentViewModel.FocusRequest) ? 1 : 0;
            document.RequestFocusLanding(GraphTabOf(host));
            Assert.NotSame(first, document.FocusRequest);
            Assert.Equal(first, document.FocusRequest);
            Assert.Equal(1, raised);
            first = document.FocusRequest!;
            document.RequestFocusLanding(elsewhere);
            Assert.NotSame(first, document.FocusRequest);
            Assert.Same(elsewhere, document.FocusRequest!.Owner);
            // The superseded request completes nothing: a completion of the
            // earlier record is a no-op, and this pane never delivers the later.
            document.CompleteFocus(first);
            Assert.NotNull(document.FocusRequest);
            park.Release();
            host.Settle(document);
            window.UpdateLayout();
            Assert.NotNull(document.FocusRequest);
            Assert.Same(elsewhere, document.FocusRequest.Owner);
        });
    }

    [Fact]
    public void ARetiredDocumentsRequestReadsAbsent()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-retired");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            WorkspaceTabViewModel tab = GraphTabOf(host);
            // A request PENDING at retirement reads absent afterwards.
            using var park = new ParkedFetch(document, 1);
            host.Workspace.GraphNavigator.SetNameQuery("note");
            park.WaitReached();
            document.RequestFocusLanding(tab);
            Assert.NotNull(document.FocusRequest);
            host.Workspace.CloseActiveTabCommand.Execute(null);
            park.Release();
            host.Settle(document);
            Assert.True(document.IsRetired);
            Assert.Null(document.FocusRequest);
            document.RequestFocusLanding(tab);
            Assert.Null(document.FocusRequest);
            view.RequestProjectionFocus();
            Assert.Null(document.FocusRequest);
            Assert.Null(view.DeferredRestorationForTests);
        });
    }

    // --- Term F2: the departure, the hold, the effective rule --------------------

    [Fact]
    public void APaneChangeWithdrawsThePresentersRestoration()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-depart");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            var elsewhere = new TextBox();
            var stack = new StackPanel();
            stack.Children.Add(view);
            stack.Children.Add(elsewhere);
            using HostedWindow window = HostInWindow(stack);
            host.Workspace.GraphNavigator.SetNameQuery("note");
            host.Settle(document);
            Assert.True(view.FilterFieldForTests.Focus());
            using var park = new ParkedFetch(document, 1);
            // Rung 1: the clear's token in flight, the restoration pending.
            PressPreview(view.FilterFieldForTests, Key.Escape);
            park.WaitReached();
            Assert.NotNull(document.FocusRequest);
            Assert.Same(document.FocusRequest, view.DeferredRestorationForTests);
            // The reader LEAVES for another pane: withdrawn.
            Assert.True(elsewhere.Focus());
            Assert.Null(document.FocusRequest);
            Assert.Null(view.DeferredRestorationForTests);
            Assert.Null(view.AwayBecauseForTests);
            park.Release();
            host.Settle(document);
            window.UpdateLayout();
            // The load that finished afterwards seated nobody.
            Assert.True(elsewhere.IsKeyboardFocused);
            Assert.False(GridHasTheKeys(view));
        });
    }

    /// <summary>Rule F, Term F4 (IPG-7): a MENU is a hold, not a departure.
    /// The landing facts covered the overlay alone, so the menu arm of
    /// <c>ClassifyFocusLoss</c> and of <c>RestorationMustWait</c> was
    /// asserted nowhere. The menu is a child of THIS window — that is what a
    /// WPF menu is; hosting one in a second window makes the OS deactivate
    /// this one and the arrangement would be about the window arm instead
    /// (the canvas's note, `CanvasNavigatorTests.cs:1112-1120`).</summary>
    [Fact]
    public void AMenuHoldsTheRestorationAndTheReturnDelivers()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-menu");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            var row = new MenuItem { Header = new TextBox { Text = "Graph", Width = 80 } };
            var menu = new Menu();
            System.Windows.Input.FocusManager.SetIsFocusScope(menu, false);
            menu.Items.Add(row);
            var stack = new StackPanel();
            stack.Children.Add(view);
            stack.Children.Add(menu);
            using HostedWindow window = HostInWindow(stack);
            host.Workspace.GraphNavigator.SetNameQuery("note");
            host.Settle(document);
            Assert.True(view.FilterFieldForTests.Focus());
            using var park = new ParkedFetch(document, 1);
            // Rung 1: the clear's token in flight, the restoration deferred.
            PressPreview(view.FilterFieldForTests, Key.Escape);
            park.WaitReached();
            GraphFocusRequest pending = document.FocusRequest!;
            Assert.Same(pending, view.DeferredRestorationForTests);

            // The keys go into the menu — production's own walk decides
            // whether they did, so the fact cannot disagree with the code
            // it tests.
            var target = (TextBox)row.Header;
            bool took = target.Focus();
            window.UpdateLayout();
            bool inAMenu = Canvas.CanvasSurfaceView.FocusIsInAMenu(Keyboard.FocusedElement);
            if (!took || !inAMenu)
            {
                // The desktop refused to open a menu. Assert the refusal
                // rather than waving it through: if the keys DID land on the
                // row this fact built, production must agree it is in a menu.
                Assert.False(
                    ReferenceEquals(target, Keyboard.FocusedElement) && !inAMenu,
                    "the keys landed on the menu row this fact built and production says it is not a menu");
                return;
            }

            // HELD, not withdrawn: the request stands and the reason is the menu.
            Assert.Same(pending, document.FocusRequest);
            Assert.Equal(GraphFocusDeparture.MenuOpen, view.AwayBecauseForTests);
            // The load finishes behind the menu: still held, nobody seated.
            park.Release();
            host.Settle(document);
            window.UpdateLayout();
            Assert.Same(pending, document.FocusRequest);
            Assert.False(GridHasTheKeys(view));
            // The menu closes and the keys return to the surface: delivered.
            _ = view.FilterFieldForTests.Focus();
            window.UpdateLayout();
            Assert.Null(document.FocusRequest);
            Assert.Null(view.AwayBecauseForTests);
            Assert.True(GridHasTheKeys(view));
        });
    }

    /// <summary>Rule F, Term F2 (IPG-11): a hold ends the OTHER way too —
    /// the reader closes the menu by choosing another pane. The graph's own
    /// focus was already false and the window never deactivated, so without
    /// the window's GotKeyboardFocus edge nothing observed it: the
    /// restoration stood, and a later activation seated the graph, taking
    /// the keys from the pane the reader had chosen.</summary>
    [Fact]
    public void AMenuThatEndsInAnotherPaneWithdrawsTheRestoration()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-menu-elsewhere");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            var row = new MenuItem { Header = new TextBox { Text = "Graph", Width = 80 } };
            var menu = new Menu();
            System.Windows.Input.FocusManager.SetIsFocusScope(menu, false);
            menu.Items.Add(row);
            var elsewhere = new TextBox { Width = 80 };
            var stack = new StackPanel();
            stack.Children.Add(view);
            stack.Children.Add(menu);
            stack.Children.Add(elsewhere);
            using HostedWindow window = HostInWindow(stack);
            host.Workspace.GraphNavigator.SetNameQuery("note");
            host.Settle(document);
            Assert.True(view.FilterFieldForTests.Focus());
            using var park = new ParkedFetch(document, 1);
            PressPreview(view.FilterFieldForTests, Key.Escape);
            park.WaitReached();
            GraphFocusRequest pending = document.FocusRequest!;

            var target = (TextBox)row.Header;
            bool took = target.Focus();
            window.UpdateLayout();
            bool inAMenu = Canvas.CanvasSurfaceView.FocusIsInAMenu(Keyboard.FocusedElement);
            if (!took || !inAMenu)
            {
                Assert.False(
                    ReferenceEquals(target, Keyboard.FocusedElement) && !inAMenu,
                    "the keys landed on the menu row this fact built and production says it is not a menu");
                return;
            }
            Assert.Same(pending, document.FocusRequest);
            Assert.Equal(GraphFocusDeparture.MenuOpen, view.AwayBecauseForTests);
            // The load finishes behind the menu, and the menu then ends by
            // the reader moving to ANOTHER PANE.
            park.Release();
            host.Settle(document);
            window.UpdateLayout();
            Assert.True(elsewhere.Focus());
            window.UpdateLayout();
            Assert.Null(document.FocusRequest);
            Assert.Null(view.DeferredRestorationForTests);
            Assert.False(GridHasTheKeys(view));
            Assert.True(elsewhere.IsKeyboardFocused);
        });
    }

    /// <summary>IPG-12: the document and the WORKSPACE-scoped view state
    /// outlive a surface. A view left subscribed after it leaves the tree is
    /// reachable for the workspace's life and renders every later
    /// publication, and a split collapse that re-realises the template adds
    /// one more of them each time.</summary>
    [Fact]
    public void AnUnloadedSurfaceObservesNothingAndAReloadObservesAgain()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-unload-observers");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            var stack = new StackPanel();
            stack.Children.Add(view);
            using HostedWindow window = HostInWindow(stack);
            Assert.True(view.ObservingForTests);
            Assert.True(view.TableForTests.ObservingForTests);

            // Out of the tree, with the MODEL UNCHANGED — the route a split
            // collapse takes, which the model-changed handler never sees.
            stack.Children.Remove(view);
            window.UpdateLayout();
            PumpLoadedState();
            Assert.False(view.ObservingForTests);
            Assert.False(view.TableForTests.ObservingForTests);
            int rowsWhileOut = view.TableForTests.GridForTests.Grid.Items.Count;
            Assert.True(document.Request(new GraphRequest.Needle()));
            host.Settle(document);
            window.UpdateLayout();
            Assert.Equal(rowsWhileOut, view.TableForTests.GridForTests.Grid.Items.Count);

            // Back in the tree: observing again, and re-bound from the record
            // as it is NOW rather than as it was when it left.
            stack.Children.Add(view);
            window.UpdateLayout();
            PumpLoadedState();
            Assert.True(view.ObservingForTests);
            Assert.True(view.TableForTests.ObservingForTests);
            Assert.Equal(document.Publication.Rows.Count, view.TableForTests.GridForTests.Grid.Items.Count);
        });
    }

    [Fact]
    public void AnOverlayHoldsTheRestorationAndTheReturnDelivers()
    {
        RunSta(() =>
        {
            Func<bool> overlayWas = Canvas.CanvasSurfaceView.ShellOverlayIsOpen;
            try
            {
                using var host = new Host(4, "graph-landing-hold");
                GraphDocumentViewModel document = host.Open();
                GraphSurfaceView view = SurfaceFor(host, document);
                var overlayField = new TextBox();
                var stack = new StackPanel();
                stack.Children.Add(view);
                stack.Children.Add(overlayField);
                using HostedWindow window = HostInWindow(stack);
                host.Workspace.GraphNavigator.SetNameQuery("note");
                host.Settle(document);
                Assert.True(view.FilterFieldForTests.Focus());
                using var park = new ParkedFetch(document, 1);
                PressPreview(view.FilterFieldForTests, Key.Escape);
                park.WaitReached();
                GraphFocusRequest pending = document.FocusRequest!;
                // A shell overlay opens and takes the keys: the restoration is KEPT and held.
                Canvas.CanvasSurfaceView.ShellOverlayIsOpen = () => true;
                Assert.True(overlayField.Focus());
                Assert.Same(pending, document.FocusRequest);
                Assert.Equal(GraphFocusDeparture.ModalOverlay, view.AwayBecauseForTests);
                // The load finishes behind the overlay: still held, nobody seated.
                park.Release();
                host.Settle(document);
                window.UpdateLayout();
                Assert.Same(pending, document.FocusRequest);
                Assert.True(overlayField.IsKeyboardFocused);
                // The overlay closes and the keys return to the surface: delivered.
                Canvas.CanvasSurfaceView.ShellOverlayIsOpen = () => false;
                // The keys return to the surface — and the delivery moves them on
                // to the grid inside the same focus change, so Focus() reports false.
                _ = view.FilterFieldForTests.Focus();
                window.UpdateLayout();
                Assert.Null(document.FocusRequest);
                Assert.True(GridHasTheKeys(view));
            }
            finally
            {
                Canvas.CanvasSurfaceView.ShellOverlayIsOpen = overlayWas;
            }
        });
    }

    [Fact]
    public void AGraphVisibleInAnotherPaneNeverTakesTheKeys()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-other-pane");
            GraphDocumentViewModel document = host.Open();
            // Two panes: the graph in one, a note tab active in the OTHER.
            string note = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            host.Workspace.SplitRightCommand.Execute(null);
            WorkspaceGroupViewModel other = host.Workspace.ActiveGroup;
            host.Workspace.OpenGraph();
            host.Settle(document);
            WorkspaceGroupViewModel graphGroup = host.Workspace.ActiveGroup;
            Assert.NotSame(other, graphGroup);
            host.Workspace.SelectGroupFromKeyboardFocus(other);
            Assert.False(host.Workspace.GraphTabIsEffective());
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            // Visible, its owner addressed — but not effective: no delivery.
            document.RequestFocusLanding(GraphTabOf(host));
            Assert.NotNull(document.FocusRequest);
            Assert.False(GridHasTheKeys(view));
            // The graph's group made active: the shell's route lands it.
            host.Workspace.SelectGroupFromKeyboardFocus(graphGroup);
            Assert.True(host.Workspace.GraphTabIsEffective());
            host.Workspace.RequestActiveEditorFocus();
            window.UpdateLayout();
            Assert.Null(document.FocusRequest);
            Assert.True(GridHasTheKeys(view));
        });
    }

    [Fact]
    public void APresetFromTheEffectiveGraphLandsTheProjection()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-preset");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            Assert.True(view.FilterFieldForTests.Focus());
            using var park = new ParkedFetch(document, 1);
            // Route (b): the palette's preset over the effective graph, then the
            // palette's closing route — the reader must land somewhere.
            host.Workspace.GraphNavigator.RunPreset(GraphPreset.Orphans);
            park.WaitReached();
            host.Workspace.RequestActiveEditorFocus();
            Assert.NotNull(document.FocusRequest);
            park.Release();
            host.Settle(document);
            window.UpdateLayout();
            Assert.True(document.Publication.Query.Filter.OrphansOnly);
            Assert.Null(document.FocusRequest);
            // The fixture has no orphans: EMPTY, the host; the projection either way.
            Assert.True(view.ProjectionHasFocus);
            Assert.Equal(document.Publication.State == GraphLoadState.Ready, GridHasTheKeys(view));
        });
    }

    /// <summary>A SHELL landing is an instruction, not a restoration: a
    /// departure leaves it standing, and the load that ends afterwards
    /// delivers it — the reader is put into the tab the shell addressed.</summary>
    [Fact]
    public void AShellLandingSurvivesADepartureAndLandsWhenTheLoadEnds()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-shell-survives");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            var elsewhere = new TextBox();
            var stack = new StackPanel();
            stack.Children.Add(view);
            stack.Children.Add(elsewhere);
            using HostedWindow window = HostInWindow(stack);
            using var park = new ParkedFetch(document, 1);
            host.Workspace.GraphNavigator.SetNameQuery("note");
            park.WaitReached();
            // The shell's request under the held grid: provisionally seated, pending.
            document.RequestFocusLanding(GraphTabOf(host));
            Assert.NotNull(document.FocusRequest);
            Assert.True(GridHasTheKeys(view));
            // The reader leaves for another pane: the shell's landing STANDS.
            Assert.True(elsewhere.Focus());
            Assert.NotNull(document.FocusRequest);
            Assert.Null(view.DeferredRestorationForTests);
            park.Release();
            host.Settle(document);
            window.UpdateLayout();
            Assert.Null(document.FocusRequest);
            Assert.True(GridHasTheKeys(view));
        });
    }

    /// <summary>The tab body hidden (a tab switch, the close) is a departure
    /// the reader LEAVES by: a restoration is withdrawn, not held.</summary>
    [Fact]
    public void ATabSwitchWithdrawsTheRestoration()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-landing-tab-switch");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            host.Workspace.GraphNavigator.SetNameQuery("note");
            host.Settle(document);
            Assert.True(view.FilterFieldForTests.Focus());
            using var park = new ParkedFetch(document, 1);
            PressPreview(view.FilterFieldForTests, Key.Escape);
            park.WaitReached();
            Assert.Same(document.FocusRequest, view.DeferredRestorationForTests);
            view.Visibility = Visibility.Collapsed;
            window.UpdateLayout();
            Assert.Null(document.FocusRequest);
            Assert.Null(view.DeferredRestorationForTests);
            Assert.Null(view.AwayBecauseForTests);
            park.Release();
            host.Settle(document);
            Assert.Null(document.FocusRequest);
        });
    }

}
