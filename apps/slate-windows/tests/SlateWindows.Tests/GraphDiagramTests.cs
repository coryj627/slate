// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR D (#746), the diagram — this partial is rule M (contract D-7):
/// the switch writes Mode through the document's one writer and persists
/// it, speaks the mode line once, refuses a retired or unseated document,
/// keeps exactly one projection in the tree, keeps the header in both
/// modes, raises the landing for a user switch alone, and raises the
/// diagram's availability at the switch and the effective edge. Every fact
/// runs under the pumped dispatcher on an STA thread; the windowed facts
/// host the surface in a hidden window where keyboard focus is real.
/// </summary>
public sealed partial class GraphDiagramTests
{
    private sealed class Host : IDisposable
    {
        public FixtureVault Vault { get; }
        public VaultSession Session { get; }
        public WorkspaceViewModel Workspace { get; }
        public List<string> GraphLines { get; } = [];

        public Host(int notes, string label)
        {
            Vault = FixtureVault.Create(notes, label);
            Session = VaultSession.OpenFilesystem(Vault.Root);
            using var cancel = new CancelToken();
            Session.ScanInitial(cancel);
            Workspace = new WorkspaceViewModel(
                Session,
                Vault.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                announceRendered: line => GraphLines.Add(line.Text));
        }

        public GraphDocumentViewModel Open()
        {
            Workspace.OpenGraph();
            GraphDocumentViewModel document = Workspace.GraphDocument!;
            Settle(document);
            return document;
        }

        public void Settle(GraphDocumentViewModel document)
        {
            PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
        }

        public WorkspaceTabViewModel GraphTab =>
            Workspace.Groups.SelectMany(g => g.Tabs).First(t => t.IsGraph);

        public void Dispose()
        {
            Workspace.Dispose();
            Session.Dispose();
            Vault.Dispose();
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class HostedWindow(Window window) : IDisposable
    {
        internal Window Window => window;

        internal void UpdateLayout() => window.UpdateLayout();

        public void Dispose() => window.Close();
    }

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

    /// <summary>The surface as the shell hosts it: its DataContext the graph
    /// tab — the owner key the shell's routes address.</summary>
    private static GraphSurfaceView SurfaceFor(Host host, GraphDocumentViewModel document) =>
        new() { Model = document, DataContext = host.GraphTab };

    private static RadioButton Choice(GraphSurfaceView surface, GraphSurfaceMode mode) =>
        surface.ModeChoicesForTests.First(c => (GraphSurfaceMode)c.Tag == mode);

    private static string Render(GraphA11yEvent @event) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.Graph(@event)).Text;

    private static string ModeLine(GraphSurfaceMode mode) => Render(new GraphA11yEvent.GraphMode(mode));

    // --- Rule M, Term M1: the switcher, the document's one writer, the save ---

    [Fact]
    public void TheSwitcherWritesModeThroughTheDocumentAndPersistsIt()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-switch-writes");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView surface = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(surface);
            Assert.Equal(GraphSurfaceMode.Table, document.ViewState.Mode);
            Assert.False(host.Workspace.GraphPreferences.HasPendingForTests);

            Choice(surface, GraphSurfaceMode.Diagram).IsChecked = true;
            Assert.Equal(GraphSurfaceMode.Diagram, document.ViewState.Mode);
            // Term W7: the persisted mode and its scheduled save (the mac's
            // setGraphMode).
            Assert.Equal(GraphSurfaceMode.Diagram, host.Workspace.GraphPreferences.CurrentConfig.Mode);
            Assert.True(host.Workspace.GraphPreferences.HasPendingForTests);
            Assert.True(Choice(surface, GraphSurfaceMode.Diagram).IsChecked);
            Assert.False(Choice(surface, GraphSurfaceMode.Table).IsChecked);

            Choice(surface, GraphSurfaceMode.Table).IsChecked = true;
            Assert.Equal(GraphSurfaceMode.Table, document.ViewState.Mode);
            Assert.Equal(GraphSurfaceMode.Table, host.Workspace.GraphPreferences.CurrentConfig.Mode);

            // The document's writer, addressed directly, re-checks the
            // switcher under the syncing guard (no second write).
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            Assert.True(Choice(surface, GraphSurfaceMode.Diagram).IsChecked);
            Assert.False(document.SetMode(GraphSurfaceMode.Diagram));
        });
    }

    [Fact]
    public void ASwitchSpeaksTheModeLineOnceAndNothingElse()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-switch-line");
            GraphDocumentViewModel document = host.Open();
            host.GraphLines.Clear();

            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            host.Settle(document);
            Assert.Equal([ModeLine(GraphSurfaceMode.Diagram)], host.GraphLines);

            host.GraphLines.Clear();
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            host.Settle(document);
            Assert.Equal([ModeLine(GraphSurfaceMode.Table)], host.GraphLines);

            // The current mode again: a no-op, no line (Term M1).
            host.GraphLines.Clear();
            Assert.False(document.SetMode(GraphSurfaceMode.Table));
            host.Settle(document);
            Assert.Empty(host.GraphLines);
        });
    }

    [Fact]
    public void ASwitchWhenRetiredOrUnseatedWritesNothing()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-switch-retired");
            GraphDocumentViewModel document = host.Open();
            host.Workspace.CloseActiveTabCommand.Execute(null);
            host.Settle(document);
            Assert.True(document.IsRetired);
            host.GraphLines.Clear();
            GraphSurfaceMode before = document.ViewState.Mode;
            GraphSurfaceMode persisted = host.Workspace.GraphPreferences.CurrentConfig.Mode;

            Assert.False(document.SetMode(GraphSurfaceMode.Diagram));
            Assert.Equal(before, document.ViewState.Mode);
            Assert.Equal(persisted, host.Workspace.GraphPreferences.CurrentConfig.Mode);
            Assert.False(host.Workspace.GraphPreferences.HasPendingForTests);
            Assert.Empty(host.GraphLines);

            // An UNSEATED document (Term 15's predicate false) is refused the
            // same way — the SelectRow guard.
            var announcer = new GraphAnnouncer(_ => { });
            var viewState = new GraphViewState();
            var bare = new GraphDocumentViewModel(
                host.Session,
                announcer,
                viewState,
                isEffectiveActive: () => true,
                verbosity: () => GraphVerbosity.Standard,
                isSeated: () => false);
            Assert.False(bare.SetMode(GraphSurfaceMode.Diagram));
            Assert.Equal(GraphSurfaceMode.Table, viewState.Mode);
            bare.Retire();
        });
    }

    // --- Term M5: one projection in the tree; the header in both modes -----------

    [Fact]
    public void ExactlyOneProjectionIsInTheTreePerMode()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-one-projection");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView surface = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(surface);
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            Assert.Equal(Visibility.Visible, surface.TableForTests.Visibility);
            Assert.Equal(Visibility.Collapsed, surface.StateHostForTests.Visibility);

            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            window.UpdateLayout();
            // Term M5: the table leaves the tree; the diagram's state host
            // stands in, named by the build's state (T20) — the table's READY
            // never shows under Diagram mode.
            Assert.Equal(Visibility.Collapsed, surface.TableForTests.Visibility);
            Assert.Equal(Visibility.Visible, surface.StateHostForTests.Visibility);
            Assert.True(document.DiagramLoading);
            Assert.Equal(GraphPhrase.LoadingDiagramText, surface.StateTextForTests.Text);
            Assert.Equal(GraphPhrase.LoadingDiagramAccessibleName, AutomationProperties.GetName(surface.StateHostForTests));
            Assert.Equal(GraphSurfaceMode.Diagram, ((IGraphSurfacePresenter)surface).ProjectionKind);

            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            window.UpdateLayout();
            Assert.Equal(Visibility.Visible, surface.TableForTests.Visibility);
            Assert.Equal(Visibility.Collapsed, surface.StateHostForTests.Visibility);
            Assert.False(document.DiagramLoading);
            Assert.Equal(GraphSurfaceMode.Table, ((IGraphSurfacePresenter)surface).ProjectionKind);
        });
    }

    [Fact]
    public void TheHeaderStaysInBothModes()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-header");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView surface = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(surface);
            foreach (GraphSurfaceMode mode in new[] { GraphSurfaceMode.Diagram, GraphSurfaceMode.Table })
            {
                Assert.True(document.SetMode(mode));
                window.UpdateLayout();
                // C-D15: the field, the switcher and the title in both modes.
                Assert.True(surface.FilterFieldForTests.IsVisible);
                Assert.All(surface.ModeChoicesForTests, choice => Assert.True(choice.IsVisible && choice.IsEnabled));
            }
        });
    }

    // --- Term M4: the landing; Term M3: the availability edges ---------------------------

    [Fact]
    public void AUserSwitchRaisesTheLandingAndAProgrammaticReCheckRaisesNone()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-switch-landing");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView surface = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(surface);
            Assert.Null(document.FocusRequest);

            // A document-driven change (the persisted restore's shape): the
            // switcher re-checks under the syncing guard, no landing raised.
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            window.UpdateLayout();
            Assert.Null(document.FocusRequest);
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            window.UpdateLayout();
            Assert.Null(document.FocusRequest);

            // A USER switch with the keys inside the surface: the presenter's
            // request is raised (Term F1); under a build it waits (Term F3's
            // rule for a presenter's request — no provisional seat).
            RadioButton diagram = Choice(surface, GraphSurfaceMode.Diagram);
            Assert.True(diagram.Focus());
            Assert.True(surface.IsKeyboardFocusWithin);
            diagram.IsChecked = true;
            window.UpdateLayout();
            Assert.Equal(GraphSurfaceMode.Diagram, document.ViewState.Mode);
            Assert.NotNull(document.FocusRequest);
            Assert.Same(host.GraphTab, document.FocusRequest!.Owner);
            Assert.True(document.DiagramLoading);
            Assert.True(diagram.IsKeyboardFocused, "a presenter's request takes no provisional seat under a build");
        });
    }

    [Fact]
    public void DiagramAvailabilityChangedIsRaisedAtTheSwitchAndTheEffectiveEdge()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-availability");
            GraphDocumentViewModel document = host.Open();
            int raised = 0;
            document.DiagramAvailabilityChanged += () => raised++;
            Assert.False(document.IsDiagramEffective);

            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            Assert.True(raised >= 1, "the switch raised the availability");
            // No model has landed (T2's build): Diagram is not yet effective.
            Assert.False(document.IsDiagramEffective);

            int beforeEdge = raised;
            string note = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            host.Settle(document);
            Assert.False(host.Workspace.GraphTabIsEffective());
            Assert.True(raised > beforeEdge, "the effective edge raised the availability");
        });
    }
}
