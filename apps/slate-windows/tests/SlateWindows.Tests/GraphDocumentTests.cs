// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR A (#746): the graph document through the real workspace and a
/// real <c>VaultSession</c> — rule L's paths (contract A-1), the load and
/// its receiver (A-2), the probe (A-3), the states (A-4), the selection
/// (A-7), the actions and the create funnel (A-8), the activation (A-9),
/// and the §W-A comparison against the 0b artifact (A-14). Every fact
/// runs under the pumped dispatcher (AR-6): the graph document has no
/// inline mode.
/// </summary>
public sealed class GraphDocumentTests
{
    /// <summary>The graph vault of 0b-13, copied into a temp root.</summary>
    private sealed class GraphVault : IDisposable
    {
        public string Root { get; }

        private GraphVault(string root)
        {
            Root = root;
        }

        public static GraphVault Copy(string label)
        {
            string source = Path.Combine(
                SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures", "graph_vault");
            Assert.True(Directory.Exists(source), $"the graph vault is missing at {source}");
            string root = Path.Combine(Path.GetTempPath(), $"slate-graph-{label}-{Guid.NewGuid():N}");
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                string target = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            return new GraphVault(root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>A workspace over a scanned session, capturing the shell's
    /// events and the graph relay's rendered lines separately.</summary>
    private sealed class Host : IDisposable
    {
        public VaultSession Session { get; }
        public WorkspaceViewModel Workspace { get; }
        public List<A11yEvent> ShellEvents { get; } = [];
        public List<string> GraphLines { get; } = [];

        /// <summary>The graph lines AND the shell's reopen line in the order
        /// they were posted — the projection rule L's Term 6 sequences are
        /// stated over (the graph family plus <c>ReopenedGraph</c>).</summary>
        public List<string> Timeline { get; } = [];

        /// <param name="lifecycleGeneration">The lifecycle's counter the
        /// workspace is CONSTRUCTED with (IPB-1); a fact's constant by
        /// default.</param>
        public Host(string root, Func<int>? lifecycleGeneration = null)
        {
            Session = VaultSession.OpenFilesystem(root);
            using var cancel = new CancelToken();
            Session.ScanInitial(cancel);
            Workspace = new WorkspaceViewModel(
                Session,
                root,
                () => [],
                @event =>
                {
                    ShellEvents.Add(@event);
                    if (@event is A11yEvent.ReopenedGraph)
                    {
                        Timeline.Add(Reopened());
                    }
                },
                startInteractionBackgroundWork: false,
                announceRendered: line =>
                {
                    GraphLines.Add(line.Text);
                    Timeline.Add(line.Text);
                },
                lifecycleGeneration: lifecycleGeneration);
        }

        public GraphDocumentViewModel Document => Workspace.GraphDocument!;

        public WorkspaceTabViewModel GraphTab =>
            Workspace.Groups.SelectMany(g => g.Tabs).First(t => t.IsGraph);

        /// <summary>Two panes, the graph effective in one; then the OTHER
        /// pane made active — the graph VISIBLE but not EFFECTIVE, the
        /// layout the addressed actions (A-8, A-9) are stated against.</summary>
        public (WorkspaceGroupViewModel GraphGroup, WorkspaceGroupViewModel Other) GraphVisibleInAnUnfocusedPane()
        {
            Workspace.OpenGraph();
            Settle();
            string note = Document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            Workspace.SplitRightCommand.Execute(null);
            WorkspaceGroupViewModel other = Workspace.ActiveGroup;
            Workspace.OpenGraph();
            Settle();
            WorkspaceGroupViewModel graphGroup = Workspace.ActiveGroup;
            Assert.NotSame(other, graphGroup);
            Assert.True(graphGroup.ActiveTab!.IsGraph);
            Workspace.SelectGroupFromKeyboardFocus(other);
            Assert.Same(other, Workspace.ActiveGroup);
            Assert.True(Workspace.GraphTabIsVisible());
            Assert.False(Workspace.GraphTabIsEffective());
            return (graphGroup, other);
        }

        /// <summary>Pump until the document's tracked work drained.</summary>
        public void Settle()
        {
            Task drain = Document.WhenAllWorkDrained();
            PumpedDispatcher.PumpUntilDrained(drain);
            PumpedDispatcher.Drain();
        }

        public void Dispose()
        {
            Workspace.Dispose();
            Session.Dispose();
        }
    }

    private static string Render(GraphA11yEvent @event) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.Graph(@event)).Text;

    private static string Opened() => Render(new GraphA11yEvent.GraphStatus(new GraphStatusNote.Opened()));

    private static string Summary(GraphDocumentViewModel document) =>
        Render(new GraphA11yEvent.GraphSnapshotSummary(document.Publication.Snapshot!.SummaryCounts));

    private static string Reopened() => SlateUniffiMethods.A11yRender(new A11yEvent.ReopenedGraph()).Text;

    private static string LoadFailed(string message) =>
        Render(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.LoadFailed(message)));

    // --- Rule L: the paths (contract A-1) ---------------------------------

    [Fact]
    public void AFreshOpenSeatsTheDocumentSpeaksOpenedThenTheSummaryAndLoadsOnce()
    {
        using GraphVault vault = GraphVault.Copy("fresh-open");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            Assert.Null(host.Workspace.GraphDocument);

            host.Workspace.OpenGraph();

            // Seated by the funnel, the load started by the follow method,
            // Opened posted before anything else.
            GraphDocumentViewModel document = host.Document;
            Assert.Same(document, host.GraphTab.Graph);
            Assert.Equal(GraphLoadState.Loading, document.Publication.State);
            Assert.Equal([Opened()], host.GraphLines);
            Assert.Equal(1, host.Workspace.GraphLoadsForTests);

            host.Settle();
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            Assert.Equal([Opened(), Summary(document)], host.GraphLines);
            Assert.Equal(1, document.CrossingsForTests["graph_snapshot"]);
            Assert.Equal(1, document.CrossingsForTests["graph_table_rows"]);
            Assert.Equal(GraphActivationCause.Activation, host.Workspace.GraphCauseForTests);
        });
    }

    [Fact]
    public void AnOpenOfTheEffectiveReadyTabSpeaksOpenedAloneAndLoadsNothing()
    {
        using GraphVault vault = GraphVault.Copy("open-active");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            host.GraphLines.Clear();

            host.Workspace.OpenGraph();
            host.Settle();

            Assert.Equal([Opened()], host.GraphLines);
            Assert.Equal(1, host.Workspace.GraphLoadsForTests);
            Assert.Single(host.Workspace.ActiveGroup.Tabs, t => t.IsGraph);
        });
    }

    [Fact]
    public void SwitchingBackToTheGraphTabReloadsWithTheSummaryAlone()
    {
        using GraphVault vault = GraphVault.Copy("switch-back");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            host.Workspace.OpenPath("a.md", WorkspaceOpenTarget.NewTab);
            host.Settle();
            host.GraphLines.Clear();

            // The header click's binding: the group's ActiveTab setter.
            host.Workspace.ActiveGroup.ActiveTab = host.GraphTab;
            host.Settle();

            Assert.Equal([Summary(host.Document)], host.GraphLines);
            Assert.Equal(2, host.Workspace.GraphLoadsForTests);
        });
    }

    [Fact]
    public void AnOpenAfterAPairFailureRestartsThePair()
    {
        using GraphVault vault = GraphVault.Copy("open-after-error");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            // A pair that FAILS (IPA-9): the gate throws inside the worker
            // after both crossings — the failure arm's envelope, exactly as
            // a session error produces it. The pair is a BY-TAB activation.
            bool armed = true;
            document.FetchGateForTests = () =>
            {
                if (armed)
                {
                    armed = false;
                    throw new InvalidOperationException("injected pair failure");
                }
            };
            string note = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            host.GraphLines.Clear();
            host.Workspace.ActiveGroup.ActiveTab = host.GraphTab;
            host.Settle();
            // ERROR without a snapshot, and the failure where the summary
            // would have been (Term 6's failure arm).
            Assert.Equal(GraphLoadState.Error, document.Publication.State);
            Assert.False(document.Publication.HoldsSnapshot);
            Assert.Equal([LoadFailed("injected pair failure")], host.GraphLines);

            // The explicit Open of the effective ERROR tab restarts the pair
            // (the mac's guard: no load only when READY, IGA-39) — LOADING
            // shows, Opened first, then the summary when it publishes.
            host.GraphLines.Clear();
            int loads = host.Workspace.GraphLoadsForTests;
            host.Workspace.OpenGraph();
            Assert.Equal(GraphLoadState.Loading, document.Publication.State);
            Assert.Equal([Opened()], host.GraphLines);
            Assert.Equal(loads + 1, host.Workspace.GraphLoadsForTests);
            host.Settle();
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            Assert.Equal([Opened(), Summary(document)], host.GraphLines);
        });
    }

    [Fact]
    public void ClosingTheLastGraphTabRetiresTheDocumentAndAReopenSeatsAFreshOne()
    {
        using GraphVault vault = GraphVault.Copy("retire");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel first = host.Document;
            first.ViewState.SelectedKey = first.Publication.Rows[0].StableKey;

            host.Workspace.CloseActiveTabCommand.Execute(null);
            Assert.Null(host.Workspace.GraphDocument);
            Assert.True(first.IsRetired);
            Assert.True(first.AnnouncerForTests.IsRetired);
            Assert.Null(first.ViewState.SelectedKey);

            host.GraphLines.Clear();
            host.Workspace.OpenGraph();
            host.Settle();
            Assert.NotSame(first, host.Document);
            Assert.Equal([Opened(), Summary(host.Document)], host.GraphLines);
        });
    }

    [Fact]
    public void AResultForARetiredDocumentInstallsNothing()
    {
        using GraphVault vault = GraphVault.Copy("stale-result");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            GraphDocumentViewModel document = host.Document;
            // Retire while the first pair is in flight.
            host.Workspace.CloseActiveTabCommand.Execute(null);
            PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
            Assert.Equal(GraphLoadState.Loading, document.Publication.State);
            Assert.Equal([Opened()], host.GraphLines);
        });
    }

    // --- The load and the receiver (contract A-2) --------------------------

    [Fact]
    public void AStaleSequenceDropsAndTheLastTokenPublishes()
    {
        using GraphVault vault = GraphVault.Copy("stale-seq");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            ulong before = document.SeqForTests;

            // Two sort requests back to back: only the second's token is
            // current; the first's result drops at step (i).
            document.SetSort(new GraphTableSort(GraphTableColumn.Note, true));
            document.SetSort(new GraphTableSort(GraphTableColumn.Note, false));
            Assert.Equal(before + 2, document.SeqForTests);
            host.Settle();

            Assert.Equal(new GraphTableSort(GraphTableColumn.Note, false), document.Publication.AcceptedSort);
            Assert.Null(document.RequestedSortForTests);
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);

            // Two IDENTICAL requests: the request check cannot tell them
            // apart, so the sequence is what drops the first — exactly
            // one install (the mutation sweep's `stale-seq-installs`).
            int installs = 0;
            document.PublicationInstalled += _ => installs++;
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            host.Settle();
            Assert.Equal(1, installs);
        });
    }

    [Fact]
    public void ASortIsRowsOnlyAndTheSameSortIsANoOpOnlyWhileNothingIsPending()
    {
        using GraphVault vault = GraphVault.Copy("sort-rows-only");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            int snapshots = document.CrossingsForTests["graph_snapshot"];
            GraphTableSort accepted = document.Publication.AcceptedSort;

            // The accepted sort again, nothing pending: a no-op.
            document.SetSort(accepted);
            Assert.Equal(1UL, document.SeqForTests);

            // A different sort: rows only.
            document.SetSort(new GraphTableSort(GraphTableColumn.Note, true));
            ulong pending = document.SeqForTests;
            // The accepted sort again WHILE pending: supersedes (the mac's
            // whole guard).
            document.SetSort(accepted);
            Assert.Equal(pending + 1, document.SeqForTests);
            host.Settle();

            Assert.Equal(accepted, document.Publication.AcceptedSort);
            Assert.Equal(snapshots, document.CrossingsForTests["graph_snapshot"]);
            Assert.Equal(3, document.CrossingsForTests["graph_table_rows"]);
        });
    }

    [Fact]
    public void ThePublicationIsOneRecordAndTheObserverSeesItWhole()
    {
        using GraphVault vault = GraphVault.Copy("one-record");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            GraphDocumentViewModel document = host.Document;
            var seen = new List<(GraphLoadState State, int Rows, ulong Total, ulong Generation)>();
            document.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GraphDocumentViewModel.Publication))
                {
                    GraphPublication p = document.Publication;
                    seen.Add((p.State, p.Rows.Count, p.Total, p.Generation));
                }
            };
            host.Settle();
            (GraphLoadState state, int rows, ulong total, ulong generation) = Assert.Single(seen);
            Assert.Equal(GraphLoadState.Ready, state);
            Assert.True(rows > 0);
            Assert.Equal((ulong)rows, total);
            Assert.Equal(document.Publication.Snapshot!.Generation, generation);
        });
    }

    // --- The probe (contract A-3) ----------------------------------------

    [Fact]
    public void AChangedGenerationReloadsSilentlyAndAnUnchangedOneFetchesNothing()
    {
        using GraphVault vault = GraphVault.Copy("probe");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            int rowsBefore = document.Publication.Rows.Count;
            ulong generationBefore = document.Publication.Generation;
            host.GraphLines.Clear();

            // Unchanged: a probe, no pair.
            host.Workspace.NotifyGraphOfVaultChange();
            host.Settle();
            Assert.Equal(1, document.CrossingsForTests["graph_generation"]);
            Assert.Equal(1, document.CrossingsForTests["graph_snapshot"]);

            // A Slate write that adds a note linking an EXISTING note (a
            // link to a missing target would add a ghost row as well): the
            // generation moves and exactly one row joins.
            string target = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Label;
            _ = host.Session.CreateExclusive("zeta.md", $"# Zeta\n\n[[{target}]]\n");
            host.Workspace.NotifyGraphOfVaultChange();
            host.Settle();

            Assert.Equal(2, document.CrossingsForTests["graph_snapshot"]);
            Assert.NotEqual(generationBefore, document.Publication.Generation);
            Assert.Equal(rowsBefore + 1, document.Publication.Rows.Count);
            Assert.Empty(host.GraphLines);
        });
    }

    [Fact]
    public void AHiddenGraphIsNotProbedAndLoadsOnItsNextActivation()
    {
        using GraphVault vault = GraphVault.Copy("hidden-probe");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            host.Workspace.OpenPath("a.md", WorkspaceOpenTarget.NewTab);
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            Assert.False(host.Workspace.GraphTabIsVisible());

            _ = host.Session.CreateExclusive("eta.md", "# Eta\n\n[[a]]\n");
            host.Workspace.NotifyGraphOfVaultChange();
            host.Settle();
            Assert.Equal(0, document.CrossingsForTests["graph_generation"]);

            host.Workspace.ActiveGroup.ActiveTab = host.GraphTab;
            host.Settle();
            Assert.Contains(document.Publication.Rows, row => row.Path == "eta.md");
        });
    }

    [Fact]
    public void AGenerationThatArrivesDuringAnInFlightPairSupersedesItAndTheStaleResultInstallsNothing()
    {
        using GraphVault vault = GraphVault.Copy("gated-generation");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            ulong g1 = document.Publication.Generation;
            var installed = new List<GraphPublication>();
            document.PublicationInstalled += install => installed.Add(install.Current);
            // The pair PARKS in the worker after both crossings (IPA-10): its
            // envelope carries G1 while, on the dispatcher, the world moves
            // to G2 and the probe runs against the held READY G1 snapshot.
            using var gate = new ManualResetEventSlim(false);
            using var reached = new ManualResetEventSlim(false);
            bool armed = true;
            document.FetchGateForTests = () =>
            {
                if (armed)
                {
                    armed = false;
                    reached.Set();
                    gate.Wait(TimeSpan.FromSeconds(10));
                }
            };
            document.ViewState.Filter = new GraphFilter(true, true, false);
            GraphLoadToken inFlight = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            // The pair has made BOTH crossings at G1 before the world moves
            // (IPB-6): its envelope is the stale one by construction.
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the pair never reached the gate");
            _ = host.Session.CreateExclusive("theta.md", "# Theta\n\n[[a]]\n");
            host.Workspace.NotifyGraphOfVaultChange();
            // The probe's apply issues the superseding silent pair: a fresh
            // seq while the first pair is still parked.
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.SeqForTests > inFlight.Seq),
                "the probe never superseded the in-flight pair");
            Assert.Empty(installed);
            gate.Set();
            host.Settle();

            // Exactly ONE install — the superseding pair's; the stale G1
            // result dropped at step (i) of the receiver.
            GraphPublication only = Assert.Single(installed);
            Assert.Same(only, document.Publication);
            Assert.True(only.Generation > g1);
            Assert.Contains(only.Rows, row => row.Path == "theta.md");
            Assert.Equal(new GraphFilter(true, true, false), only.Filter);
        });
    }

    [Fact]
    public void AProbeWhileNothingIsHeldKeepsTheHighWaterMarkAndTheFirstPairFollowsIt()
    {
        using GraphVault vault = GraphVault.Copy("high-water");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            // A document of its own, so the gate is armed BEFORE its first
            // pair starts: the gate holds that pair's compute until a
            // probe's apply has recorded a newer generation.
            var lines = new List<string>();
            var document = new GraphDocumentViewModel(
                host.Session,
                new GraphAnnouncer(line => lines.Add(line.Text)),
                isEffectiveActive: () => true,
                verbosity: () => GraphVerbosity.Standard);
            Assert.False(document.Publication.HoldsSnapshot);
            bool armed = true;
            document.FetchGateForTests = () =>
            {
                if (!armed)
                {
                    return;
                }
                armed = false;
                _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[a]]\n");
                // The probe from the worker: its compute reads G2, its apply
                // (on the pumped dispatcher) finds no snapshot held and keeps
                // the mark; the pair's compute waits for that apply.
                document.Probe();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (document.HighWaterForTests == 0 && clock.Elapsed < TimeSpan.FromSeconds(10))
                {
                    Thread.Sleep(10);
                }
            };
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            PumpedDispatcher.Drain();

            // The first pair installed G1, saw the mark above it, and a
            // silent pair followed to G2 (the sweep's `high-water-dropped`).
            Assert.True(document.Publication.HoldsSnapshot);
            Assert.Contains(document.Publication.Rows, row => row.Path == "iota.md");
            Assert.Equal(0UL, document.HighWaterForTests);
            Assert.Equal(2, document.CrossingsForTests["graph_snapshot"]);
            Assert.Empty(lines);
            document.Retire();
        });
    }

    // --- Selection (contract A-7) -----------------------------------------

    [Fact]
    public void TheSharedKeySurvivesAReorderAndClearsOnlyWhenTheSnapshotDropsTheNode()
    {
        using GraphVault vault = GraphVault.Copy("selection");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            GraphTableRow chosen = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note);
            document.ViewState.SelectedKey = chosen.StableKey;

            document.SetSort(new GraphTableSort(GraphTableColumn.Note, false));
            host.Settle();
            Assert.Equal(chosen.StableKey, document.ViewState.SelectedKey);

            // A name-query overlay hides the node from the ROWS while the
            // snapshot keeps it: the key survives (the sweep's
            // `selection-against-rows`).
            document.ViewState.NameQuery = "zzz-nothing-matches";
            _ = document.Load(GraphLoadKind.RowsOnly, GraphAnnouncePolicy.Silent);
            host.Settle();
            Assert.Empty(document.Publication.Rows);
            Assert.True(document.Publication.ContainsNode(chosen.StableKey));
            Assert.Equal(chosen.StableKey, document.ViewState.SelectedKey);
            document.ViewState.NameQuery = string.Empty;

            // Exclude every note: the snapshot under orphans-only drops the node.
            document.ViewState.Filter = new GraphFilter(false, false, true);
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            host.Settle();
            Assert.False(document.Publication.ContainsNode(chosen.StableKey));
            Assert.Null(document.ViewState.SelectedKey);
        });
    }

    // --- Actions and the create funnel (contract A-8) ----------------------

    [Fact]
    public void RowActionsAreCoresVectorsFetchedOnceAndUnionedInCoresOrder()
    {
        using GraphVault vault = GraphVault.Copy("actions");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            Assert.Equal(3, document.ActionInventoryCrossings);
            IReadOnlyList<GraphRowActionSpec> union = document.ActionUnion();
            Assert.Equal(
                [GraphRowAction.Open, GraphRowAction.OpenInNewTab, GraphRowAction.ShowConnections, GraphRowAction.Reveal, GraphRowAction.CreateNote],
                union.Select(spec => spec.Action));
            Assert.Equal(
                SlateUniffiMethods.GraphRowActions(GraphNodeKind.Note).Select(s => s.Title),
                union.Where(s => document.ActionAppliesTo(s.Action, GraphNodeKind.Note)).Select(s => s.Title));
            Assert.Equal(
                SlateUniffiMethods.GraphRowActions(GraphNodeKind.Ghost).Select(s => s.Title),
                union.Where(s => document.ActionAppliesTo(s.Action, GraphNodeKind.Ghost)).Select(s => s.Title));

            GraphTableRow note = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note);
            Assert.True(document.IsActionEnabled(GraphRowAction.Open, note));
            Assert.True(document.IsActionEnabled(GraphRowAction.Reveal, note));
            Assert.False(document.IsActionEnabled(GraphRowAction.ShowConnections, note));
            document.ShowConnectionsFromSurface = _ => { };
            Assert.True(document.IsActionEnabled(GraphRowAction.ShowConnections, note));
            // The inventory did not grow with the rows.
            Assert.Equal(3, document.ActionInventoryCrossings);
        });
    }

    [Fact]
    public void ActivationOpensInTheGraphsPaneAndPostsOpenedFileThroughTheWorkspace()
    {
        using GraphVault vault = GraphVault.Copy("activate");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            GraphTableRow note = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note);
            host.ShellEvents.Clear();

            document.Activate(note, modified: false);

            Assert.Equal(note.Path, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Contains(host.ShellEvents, e => e is A11yEvent.OpenedFile opened && opened.Filename == Path.GetFileName(note.Path!));
            // The graph tab was replaced: the last graph tab is gone and the
            // document retired.
            Assert.Null(host.Workspace.GraphDocument);
            Assert.True(document.IsRetired);
        });
    }

    [Fact]
    public void ModifiedActivationOpensANewTabAndTheGraphStays()
    {
        using GraphVault vault = GraphVault.Copy("activate-new-tab");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            GraphTableRow note = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note);

            document.Activate(note, modified: true);

            Assert.Equal(note.Path, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Same(document, host.Workspace.GraphDocument);
            Assert.False(document.IsRetired);
            Assert.Equal(2, host.Workspace.ActiveGroup.Tabs.Count);
        });
    }

    [Fact]
    public void AGhostsActivationCreatesItsNoteOpensItAndPostsOneNoteCreatedAfterTheOpen()
    {
        using GraphVault vault = GraphVault.Copy("create");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var creator = new RecordingCreator(host.Session);
            host.Workspace.GraphNoteCreator = creator;
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            GraphTableRow ghost = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Ghost);
            string expectedPath = SlateUniffiMethods.GraphGhostNotePath(ghost.Label);
            host.ShellEvents.Clear();

            document.Activate(ghost, modified: false);
            PumpedDispatcher.PumpUntilDrained(host.Workspace.DrainGraphNoteCreationForTests());
            PumpedDispatcher.Drain();

            Assert.Equal([expectedPath], creator.Created);
            Assert.Equal(string.Empty, creator.Content);
            Assert.Equal([expectedPath], creator.Landed);
            Assert.True(File.Exists(Path.Combine(vault.Root, expectedPath)));
            Assert.Equal(expectedPath, host.Workspace.ActiveGroup.ActiveTab!.Path);
            A11yEvent created = Assert.Single(host.ShellEvents, e => e is A11yEvent.Graph);
            var status = Assert.IsType<GraphA11yEvent.GraphStatus>(((A11yEvent.Graph)created).Event);
            var note = Assert.IsType<GraphStatusNote.NoteCreated>(status.Note);
            Assert.Equal(Path.GetFileName(expectedPath), note.Name);
            Assert.DoesNotContain(host.ShellEvents, e => e is A11yEvent.OpenedFile);
        });
    }

    [Fact]
    public void ACreateThatLandsAfterTheGraphClosedStillCompletesWithTheOpenSuppressed()
    {
        using GraphVault vault = GraphVault.Copy("create-after-close");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var gate = new ManualResetEventSlim(false);
            var creator = new RecordingCreator(host.Session, beforeCreate: () => gate.Wait(TimeSpan.FromSeconds(10)));
            host.Workspace.GraphNoteCreator = creator;
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            GraphTableRow ghost = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Ghost);
            string expectedPath = SlateUniffiMethods.GraphGhostNotePath(ghost.Label);

            document.Activate(ghost, modified: false);
            host.Workspace.CloseActiveTabCommand.Execute(null);
            Assert.Null(host.Workspace.GraphDocument);
            host.ShellEvents.Clear();
            gate.Set();
            PumpedDispatcher.PumpUntilDrained(host.Workspace.DrainGraphNoteCreationForTests());
            PumpedDispatcher.Drain();

            Assert.Equal([expectedPath], creator.Landed);
            Assert.Contains(host.ShellEvents, e => e is A11yEvent.Graph g && g.Event is GraphA11yEvent.GraphStatus { Note: GraphStatusNote.NoteCreated });
            Assert.DoesNotContain(host.Workspace.Groups.SelectMany(g => g.Tabs), t => t.Path == expectedPath);
        });
    }

    [Fact]
    public void AnExistingDestinationAnnouncesTheHighEventWithItsMessage()
    {
        using GraphVault vault = GraphVault.Copy("create-exists");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var creator = new RecordingCreator(host.Session);
            host.Workspace.GraphNoteCreator = creator;
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            GraphTableRow ghost = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Ghost);
            string expectedPath = SlateUniffiMethods.GraphGhostNotePath(ghost.Label);
            File.WriteAllText(Path.Combine(vault.Root, expectedPath), "# taken\n");
            host.ShellEvents.Clear();

            document.Activate(ghost, modified: false);
            PumpedDispatcher.PumpUntilDrained(host.Workspace.DrainGraphNoteCreationForTests());
            PumpedDispatcher.Drain();

            A11yEvent blocked = Assert.Single(host.ShellEvents, e => e is A11yEvent.Graph);
            var reason = Assert.IsType<GraphA11yEvent.GraphBlocked>(((A11yEvent.Graph)blocked).Event);
            var failed = Assert.IsType<GraphBlockedReason.NoteCreateFailed>(reason.Reason);
            Assert.False(string.IsNullOrEmpty(failed.Message));
            Assert.Empty(creator.Landed);
        });
    }

    // --- Rule L, Term 6: the reopen sequences (IPA-3) ----------------------

    /// <summary>A reopen whose graph tab still exists, effective and READY:
    /// an Open of that tab with the reopen line AFTER it — `Opened`,
    /// `ReopenedGraph`, no load, no summary (the mac's `:81` guard under
    /// `openGraphTab`, then `reopenedGraph`).</summary>
    [Fact]
    public void AReopenOfTheEffectiveReadyGraphSpeaksOpenedThenTheReopenLineAndLoadsNothing()
    {
        using GraphVault vault = GraphVault.Copy("reopen-effective");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            // A closed record that outlives the graph's next open.
            host.Workspace.CloseActiveTabCommand.Execute(null);
            host.Workspace.OpenGraph();
            host.Settle();
            Assert.True(host.Workspace.GraphTabIsEffective());
            int loads = host.Workspace.GraphLoadsForTests;
            host.Timeline.Clear();

            host.Workspace.ReopenClosedTabCommand.Execute(null);
            host.Settle();

            Assert.Equal([Opened(), Reopened()], host.Timeline);
            Assert.Equal(loads, host.Workspace.GraphLoadsForTests);
        });
    }

    /// <summary>A reopen whose graph tab exists but is hidden behind a note
    /// in its group: the activation is a transition BY TAB under the Reopen
    /// cause — `Opened`, the reopen line, then the summary.</summary>
    [Fact]
    public void AReopenOfAHiddenGraphSpeaksOpenedTheReopenLineThenTheSummary()
    {
        using GraphVault vault = GraphVault.Copy("reopen-hidden");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            host.Workspace.CloseActiveTabCommand.Execute(null);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            string note = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            Assert.True(host.Workspace.GraphTabIsVisible() is false);
            int loads = host.Workspace.GraphLoadsForTests;
            host.Timeline.Clear();

            host.Workspace.ReopenClosedTabCommand.Execute(null);
            host.Settle();

            Assert.Equal([Opened(), Reopened(), Summary(document)], host.Timeline);
            Assert.Equal(loads + 1, host.Workspace.GraphLoadsForTests);
            Assert.True(host.Workspace.GraphTabIsEffective());
        });
    }

    /// <summary>The reopen's failure arm (Term 6): `Opened`, the reopen
    /// line, then `LoadFailed` where the summary would have been.</summary>
    [Fact]
    public void AReopenWhosePairFailsSpeaksOpenedTheReopenLineThenTheFailure()
    {
        using GraphVault vault = GraphVault.Copy("reopen-failure");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            host.Workspace.CloseActiveTabCommand.Execute(null);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            string note = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            bool armed = true;
            document.FetchGateForTests = () =>
            {
                if (armed)
                {
                    armed = false;
                    throw new InvalidOperationException("injected reopen failure");
                }
            };
            int loads = host.Workspace.GraphLoadsForTests;
            host.Timeline.Clear();

            host.Workspace.ReopenClosedTabCommand.Execute(null);
            host.Settle();

            Assert.Equal([Opened(), Reopened(), LoadFailed("injected reopen failure")], host.Timeline);
            Assert.Equal(loads + 1, host.Workspace.GraphLoadsForTests);
            Assert.Equal(GraphLoadState.Error, document.Publication.State);
        });
    }

    /// <summary>A reopen that recreates the graph tab: `Opened`, the reopen
    /// line, then the summary — the AddTab arm.</summary>
    [Fact]
    public void AReopenThatRecreatesTheGraphTabSpeaksOpenedTheReopenLineThenTheSummary()
    {
        using GraphVault vault = GraphVault.Copy("reopen-recreate");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            host.Workspace.CloseActiveTabCommand.Execute(null);
            Assert.Null(host.Workspace.GraphDocument);
            int loads = host.Workspace.GraphLoadsForTests;
            host.Timeline.Clear();

            host.Workspace.ReopenClosedTabCommand.Execute(null);
            host.Settle();

            GraphDocumentViewModel document = host.Document;
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            Assert.Equal([Opened(), Reopened(), Summary(document)], host.Timeline);
            Assert.Equal(loads + 1, host.Workspace.GraphLoadsForTests);
        });
    }

    // --- The addressed actions (contracts A-8, A-9; IPA-4) -----------------

    /// <summary>Every action activates its address at invocation (IGA-41):
    /// Reveal from a graph visible in a pane that is NOT the active group
    /// makes that pane active before the sidebar seam runs.</summary>
    [Fact]
    public void RevealFromAnUnfocusedPaneActivatesTheGraphsPaneFirst()
    {
        using GraphVault vault = GraphVault.Copy("reveal-addressed");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            (WorkspaceGroupViewModel graphGroup, WorkspaceGroupViewModel other) = host.GraphVisibleInAnUnfocusedPane();
            var revealed = new List<string>();
            WorkspaceGroupViewModel? activeAtReveal = null;
            host.Workspace.GraphRevealInSidebar = path =>
            {
                // The address is active WHEN the seam runs (IPB-4), not
                // merely afterwards.
                activeAtReveal = host.Workspace.ActiveGroup;
                revealed.Add(path);
            };
            GraphTableRow note = host.Document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note);
            Assert.Same(other, host.Workspace.ActiveGroup);

            host.Document.Execute(GraphRowAction.Reveal, note);

            Assert.Same(graphGroup, activeAtReveal);
            Assert.Same(graphGroup, host.Workspace.ActiveGroup);
            Assert.True(host.Workspace.GraphTabIsEffective());
            Assert.Equal([note.Path], revealed);
        });
    }

    /// <summary>Create from an unfocused pane: the address is activated at
    /// invocation, so the landed note opens in the graph's pane (AD-8), and
    /// the completion compares the address without activating anything.</summary>
    [Fact]
    public void CreateFromAnUnfocusedPaneActivatesTheGraphsPaneAndOpensTheNoteThere()
    {
        using GraphVault vault = GraphVault.Copy("create-addressed");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var creator = new RecordingCreator(host.Session);
            host.Workspace.GraphNoteCreator = creator;
            (WorkspaceGroupViewModel graphGroup, WorkspaceGroupViewModel other) = host.GraphVisibleInAnUnfocusedPane();
            GraphTableRow ghost = host.Document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Ghost);
            string expectedPath = SlateUniffiMethods.GraphGhostNotePath(ghost.Label);
            Assert.Same(other, host.Workspace.ActiveGroup);

            host.Document.Activate(ghost, modified: false);
            Assert.Same(graphGroup, host.Workspace.ActiveGroup);
            PumpedDispatcher.PumpUntilDrained(host.Workspace.DrainGraphNoteCreationForTests());
            PumpedDispatcher.Drain();

            Assert.Equal([expectedPath], creator.Landed);
            Assert.Same(graphGroup, host.Workspace.ActiveGroup);
            Assert.Equal(expectedPath, graphGroup.ActiveTab!.Path);
            Assert.DoesNotContain(other.Tabs, t => t.Path == expectedPath);
        });
    }

    // --- Rule A: the lifecycle generation (IPA-6) --------------------------

    /// <summary>The token carries the lifecycle generation it was started
    /// under; a result arriving after the lifecycle advanced installs
    /// nothing and speaks nothing, and the next body under the new
    /// generation publishes.</summary>
    [Fact]
    public void AResultFromAnEarlierLifecycleGenerationInstallsNothing()
    {
        using GraphVault vault = GraphVault.Copy("lifecycle-generation");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            int generation = 1;
            var lines = new List<string>();
            var document = new GraphDocumentViewModel(
                host.Session,
                new GraphAnnouncer(line => lines.Add(line.Text)),
                isEffectiveActive: () => true,
                verbosity: () => GraphVerbosity.Standard,
                lifecycleGeneration: () => Volatile.Read(ref generation));
            int installs = 0;
            document.PublicationInstalled += _ => installs++;
            // The lifecycle advances while the body is on the pool, after
            // its crossings.
            document.FetchGateForTests = () => Interlocked.Increment(ref generation);
            GraphLoadToken token = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Summary);
            Assert.Equal(1, token.LifecycleGeneration);
            PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
            Assert.Equal(0, installs);
            Assert.False(document.Publication.HoldsSnapshot);
            Assert.Empty(lines);

            document.FetchGateForTests = null;
            GraphLoadToken next = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            Assert.Equal(2, next.LifecycleGeneration);
            PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
            Assert.Equal(1, installs);
            Assert.True(document.Publication.HoldsSnapshot);
            document.Retire();
        });
    }

    /// <summary>A create whose completion arrives after the lifecycle
    /// advanced is dropped whole (AD-8): the file landed, but no
    /// bookkeeping, no open, no announcement — its session and sidebar are
    /// gone.</summary>
    [Fact]
    public void ACreateCompletingAfterTheLifecycleAdvancedIsDroppedWhole()
    {
        using GraphVault vault = GraphVault.Copy("create-lifecycle");
        PumpedDispatcher.Run(() =>
        {
            int generation = 1;
            using var host = new Host(vault.Root, () => Volatile.Read(ref generation));
            var creator = new RecordingCreator(host.Session, beforeCreate: () => Interlocked.Increment(ref generation));
            host.Workspace.GraphNoteCreator = creator;
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            GraphTableRow ghost = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Ghost);
            string expectedPath = SlateUniffiMethods.GraphGhostNotePath(ghost.Label);
            host.ShellEvents.Clear();

            document.Activate(ghost, modified: false);
            PumpedDispatcher.PumpUntilDrained(host.Workspace.DrainGraphNoteCreationForTests());
            PumpedDispatcher.Drain();

            Assert.Equal([expectedPath], creator.Created);
            Assert.Empty(creator.Landed);
            Assert.DoesNotContain(host.ShellEvents, e => e is A11yEvent.Graph);
            Assert.DoesNotContain(host.Workspace.Groups.SelectMany(g => g.Tabs), t => t.Path == expectedPath);
        });
    }

    /// <summary>A create parked while the reader moves on (IPB-4, IGA-56):
    /// the address captured at invocation is no longer current at
    /// completion, so the visual open is suppressed — and the completion
    /// activates nothing — while the landing's bookkeeping and the ONE
    /// `NoteCreated` still happen.</summary>
    [Fact]
    public void ACreateThatLandsAfterTheReaderMovedCompletesWithTheOpenSuppressed()
    {
        using GraphVault vault = GraphVault.Copy("create-reader-moved");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            using var gate = new ManualResetEventSlim(false);
            var creator = new RecordingCreator(host.Session, beforeCreate: () => gate.Wait(TimeSpan.FromSeconds(10)));
            host.Workspace.GraphNoteCreator = creator;
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            GraphTableRow ghost = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Ghost);
            string expectedPath = SlateUniffiMethods.GraphGhostNotePath(ghost.Label);
            string note = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;

            document.Activate(ghost, modified: false);
            // The reader moves to a note while the create is parked.
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            WorkspaceTabViewModel moved = host.Workspace.ActiveGroup.ActiveTab!;
            Assert.False(moved.IsGraph);
            host.ShellEvents.Clear();
            gate.Set();
            PumpedDispatcher.PumpUntilDrained(host.Workspace.DrainGraphNoteCreationForTests());
            PumpedDispatcher.Drain();

            Assert.Equal([expectedPath], creator.Landed);
            Assert.Same(moved, host.Workspace.ActiveGroup.ActiveTab);
            Assert.True(File.Exists(Path.Combine(vault.Root, expectedPath)));
            Assert.DoesNotContain(host.Workspace.Groups.SelectMany(g => g.Tabs), t => t.Path == expectedPath);
            A11yEvent created = Assert.Single(host.ShellEvents, e => e is A11yEvent.Graph);
            Assert.IsType<GraphStatusNote.NoteCreated>(
                Assert.IsType<GraphA11yEvent.GraphStatus>(((A11yEvent.Graph)created).Event).Note);
        });
    }

    /// <summary>IPB-1: a persisted graph tab is restored and loaded INSIDE
    /// the workspace's constructor, so the generation its token carries
    /// must be the lifecycle's — handed in at construction — or the
    /// restored graph never publishes. Under a non-zero lifecycle counter
    /// the restore reaches READY and speaks the summary alone (Activation).</summary>
    [Fact]
    public void ARestoredGraphTabLoadsUnderTheLifecyclesGenerationAndSpeaksTheSummary()
    {
        using GraphVault vault = GraphVault.Copy("restore-lifecycle");
        PumpedDispatcher.Run(() =>
        {
            var persistence = new WorkspacePersistence(vault.Root);
            WorkspaceGroupState group = new(
                Guid.NewGuid(),
                null,
                [new WorkspaceTabState(Guid.NewGuid(), new WorkspaceItemState(WorkspaceItemKind.Graph, "graph:singleton"))]);
            persistence.Save(new WorkspaceSnapshot(WorkspacePersistence.SchemaVersion, group.Id, group, null, []));

            int generation = 7;
            using var host = new Host(vault.Root, () => Volatile.Read(ref generation));
            Assert.True(host.Workspace.ActiveGroup.ActiveTab!.IsGraph);
            GraphDocumentViewModel document = host.Document;
            host.Settle();

            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            Assert.Equal([Summary(document)], host.GraphLines);
            Assert.Equal(1, host.Workspace.GraphLoadsForTests);
            // The counter the workspace reads IS the lifecycle's, and a
            // token carries it — a workspace that read a constant of its
            // own would agree with itself and still be wrong (the sweep's
            // `ipb1-provider-not-injected`).
            Assert.Equal(7, host.Workspace.LifecycleGeneration());
            GraphLoadToken token = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            Assert.Equal(7, token.LifecycleGeneration);
            host.Settle();
            generation = 8;
            Assert.Equal(8, document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent).LifecycleGeneration);
            host.Settle();
        });
    }

    /// <summary>IPB-3: the workspace's teardown drains the graph's work on
    /// the owner's own thread WITHOUT pumping it. A pair parked in the
    /// worker is released from another thread; the tracked task must then
    /// complete without an apply ever running on the blocked dispatcher —
    /// the shutdown settles the pending apply — so the bounded drain
    /// returns promptly, nothing installs, and the document is retired.</summary>
    [Fact]
    public void ATeardownDrainsAParkedPairWithoutPumpingTheDispatcher()
    {
        using GraphVault vault = GraphVault.Copy("teardown-drain");
        PumpedDispatcher.Run(() =>
        {
            var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            GraphPublication before = document.Publication;
            int installs = 0;
            document.PublicationInstalled += _ => installs++;
            using var gate = new ManualResetEventSlim(false);
            using var reached = new ManualResetEventSlim(false);
            document.FetchGateForTests = () =>
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(10));
            };
            document.ViewState.Filter = new GraphFilter(true, true, false);
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the pair never reached the gate");
            // The release comes from the pool ONLY once the teardown has
            // retired the document (IPC-2's deterministic barrier: the
            // retirement happens inside Dispose, before its bounded wait);
            // this thread never pumps.
            _ = Task.Run(() =>
            {
                var spin = new SpinWait();
                while (!document.IsRetired)
                {
                    spin.SpinOnce();
                }
                gate.Set();
            });
            var clock = System.Diagnostics.Stopwatch.StartNew();
            host.Workspace.Dispose();
            clock.Stop();

            Assert.True(document.IsRetired);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"the teardown took {clock.Elapsed}");
            Assert.True(document.WhenAllWorkDrained().IsCompleted, "tracked work outlived the teardown");
            PumpedDispatcher.Drain();
            Assert.Equal(0, installs);
            Assert.Same(before, document.Publication);
            host.Session.Dispose();
        });
    }

    /// <summary>IPB-3, the other half: the pair's compute FINISHED and its
    /// apply is already queued on the owner's thread when the teardown
    /// starts blocking that thread. The shutdown settles the queued apply,
    /// so the bounded drain returns promptly; the callback runs later and
    /// applies nothing (the sweep's `ipb3-teardown-stalls`).</summary>
    [Fact]
    public void ATeardownDrainsAPostedApplyWithoutPumpingTheDispatcher()
    {
        using GraphVault vault = GraphVault.Copy("teardown-posted");
        PumpedDispatcher.Run(() =>
        {
            var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            GraphPublication before = document.Publication;
            int installs = 0;
            document.PublicationInstalled += _ => installs++;
            using var queued = new ManualResetEventSlim(false);
            document.ApplyQueuedForTests = queued.Set;
            document.ViewState.Filter = new GraphFilter(true, true, false);
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            // The envelope's apply is QUEUED on this thread — the seam fires
            // under the work lock the instant it is (IPC-2) — and this
            // thread does not pump it.
            Assert.True(queued.Wait(TimeSpan.FromSeconds(10)), "the apply was never queued");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            host.Workspace.Dispose();
            clock.Stop();

            Assert.True(document.IsRetired);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"the teardown took {clock.Elapsed}");
            Assert.True(document.WhenAllWorkDrained().IsCompleted, "tracked work outlived the teardown");
            PumpedDispatcher.Drain();
            Assert.Equal(0, installs);
            Assert.Same(before, document.Publication);
            host.Session.Dispose();
        });
    }

    // --- §W-A (contract A-14) ---------------------------------------------

    [Fact]
    public void TheDocumentsRowsAndSummaryEqualTheArtifactsUnderTheArtifactsFilter()
    {
        using GraphVault vault = GraphVault.Copy("artifact");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;

            string goldenPath = Path.Combine(
                SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures", "parity_golden", "graph_queries.json");
            using JsonDocument golden = JsonDocument.Parse(File.ReadAllBytes(goldenPath));
            JsonElement table = golden.RootElement.GetProperty("table");
            Assert.Equal(16, table.GetArrayLength());

            // The artifact's `all` is the harness's INCLUSIVE filter, not
            // core's default: attachments and ghosts in, orphans-only off.
            document.ViewState.Filter = new GraphFilter(true, true, false);
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            host.Settle();

            int modified = document.CellIndexOf(GraphTableColumn.Modified);
            string[] artifactCellNames = ["note", "links_in", "links_out", "embeds_in", "embeds_out", "component", "modified", "folder", "kind"];
            foreach (JsonElement entry in table.EnumerateArray())
            {
                Assert.Equal("all", entry.GetProperty("query").GetString());
                (GraphTableColumn column, bool ascending) = ParseSort(entry.GetProperty("sort").GetString()!);
                document.SetSort(new GraphTableSort(column, ascending));
                host.Settle();
                GraphPublication publication = document.Publication;
                Assert.Equal(new GraphTableSort(column, ascending), publication.AcceptedSort);
                Assert.Equal(entry.GetProperty("total").GetUInt64(), publication.Total);
                Assert.Equal(entry.GetProperty("summary").GetString(), publication.Summary);
                JsonElement rows = entry.GetProperty("rows");
                Assert.Equal(rows.GetArrayLength(), publication.Rows.Count);
                int r = 0;
                foreach (JsonElement row in rows.EnumerateArray())
                {
                    GraphTableRow published = publication.Rows[r++];
                    Assert.Equal(row.GetProperty("key").GetString(), published.StableKey);
                    for (int c = 0; c < document.ColumnSpecs.Count; c++)
                    {
                        if (c == modified)
                        {
                            continue;
                        }
                        string expected = row.GetProperty(artifactCellNames[c]).GetString()!;
                        string actual = document.CellAt(published, c);
                        if (artifactCellNames[c] == "folder")
                        {
                            actual = actual.Replace('\\', '/');
                        }
                        Assert.Equal(expected, actual);
                    }
                }
            }
            // The session saw the artifact's filter as the pair's argument.
            Assert.Equal(new GraphFilter(true, true, false), document.Publication.Filter);
        });
    }

    private static (GraphTableColumn Column, bool Ascending) ParseSort(string name)
    {
        string[] parts = name.Split(' ');
        GraphTableColumn column = parts[0] switch
        {
            "note" => GraphTableColumn.Note,
            "links_in" => GraphTableColumn.LinksIn,
            "links_out" => GraphTableColumn.LinksOut,
            "embeds_in" => GraphTableColumn.EmbedsIn,
            "embeds_out" => GraphTableColumn.EmbedsOut,
            "component" => GraphTableColumn.Component,
            "folder" => GraphTableColumn.Folder,
            "kind" => GraphTableColumn.Kind,
            _ => throw new InvalidOperationException(name),
        };
        return (column, parts[1] == "asc");
    }

    /// <summary>A creator that writes through the session the way the
    /// sidebar does and records each phase.</summary>
    private sealed class RecordingCreator(VaultSession session, Action? beforeCreate = null) : FileManagement.ISurfaceNoteCreator
    {
        public List<string> Created { get; } = [];
        public List<string> Landed { get; } = [];
        public List<string> Caveats { get; } = [];
        public string? Content { get; private set; }

        public FileManagement.NoteCreateResult TryCreateNote(string path, string content)
        {
            beforeCreate?.Invoke();
            Content = content;
            try
            {
                string? caveat = CreateOutcomes.CreateReporting(session, path, content, Path.GetFileName(path));
                Created.Add(path);
                return new FileManagement.NoteCreateResult.Landed(caveat);
            }
            catch (VaultException.DestinationExists exception)
            {
                return new FileManagement.NoteCreateResult.Exists(exception.Message);
            }
            catch (VaultException exception)
            {
                return new FileManagement.NoteCreateResult.Failed(exception.Message);
            }
        }

        public void NoteLanded(string path) => Landed.Add(path);

        public void SpeakCaveat(string caveat) => Caveats.Add(caveat);
    }
}
