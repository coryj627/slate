// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR B, slice B1 (#746): the Connections leaf's document through the
/// real workspace and a real <c>VaultSession</c> — rule C's terms (the
/// levels, the trigger matrix, supersession, the probe's state machine,
/// the presentation and the request, the two-echo envelope) and the
/// contracts they carry (B-1..B-12). Every fact runs under the pumped
/// dispatcher: the leaf's document has no inline mode.
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    private const string Hub = "hub.md";
    private const string Two = "2.md";
    private const string Deep = "notes/nested/deep.md";

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
            string root = Path.Combine(Path.GetTempPath(), $"slate-connections-{label}-{Guid.NewGuid():N}");
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
    /// events and the relay's rendered lines in ONE timeline — the merged
    /// timelines of B-10 are stated over it.</summary>
    private sealed class Host : IDisposable
    {
        public VaultSession Session { get; }
        public WorkspaceViewModel Workspace { get; }
        public List<A11yEvent> ShellEvents { get; } = [];
        public List<string> RelayLines { get; } = [];
        public List<string> Timeline { get; } = [];

        public string Root { get; }

        public Host(
            string root,
            Func<int>? lifecycleGeneration = null,
            Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>? dirtyNavigationDecision = null)
        {
            Root = root;
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
                    Timeline.Add(@event.GetType().Name);
                },
                dirtyNavigationDecision: dirtyNavigationDecision,
                startInteractionBackgroundWork: false,
                announceRendered: line =>
                {
                    RelayLines.Add(line.Text);
                    Timeline.Add(line.Text);
                },
                lifecycleGeneration: lifecycleGeneration);
        }

        public ConnectionsLeafViewModel Leaf => Workspace.Connections;

        public static WorkspaceLeafOption ConnectionsLeaf =>
            WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "connections");

        public static WorkspaceLeafOption OutlineLeaf =>
            WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "outline");

        public void ActivateLeaf() => Workspace.ActiveLeaf = ConnectionsLeaf;

        public void OpenNote(string path) => Workspace.OpenPath(path, WorkspaceOpenTarget.CurrentTab);

        /// <summary>Loads ISSUED (a synchronous witness on the owner thread);
        /// the FFI crossing is counted on the pool and read after a settle.</summary>
        public int Loads => Leaf.LoadsIssuedForTests;

        public int Crossings => Leaf.CrossingsForTests["graph_connections_tree"];

        /// <summary>Pump until the leaf's tracked work drained.</summary>
        public void Settle()
        {
            Task drain = Leaf.WhenAllWorkDrained();
            PumpedDispatcher.PumpUntilDrained(drain);
            PumpedDispatcher.Drain();
        }

        public void Clear()
        {
            ShellEvents.Clear();
            RelayLines.Clear();
            Timeline.Clear();
        }

        public void Dispose()
        {
            Workspace.Dispose();
            Session.Dispose();
        }
    }

    private static string Render(GraphA11yEvent @event) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.Graph(@event)).Text;

    private static string Summary(ConnectionsLeafViewModel leaf) =>
        Render(new GraphA11yEvent.GraphNeighborhoodSummary(leaf.Publication.Tree!.SummaryCounts));

    private static string PanelLine() =>
        Render(new GraphA11yEvent.GraphStatus(new GraphStatusNote.ConnectionsPanel()));

    private static string LoadingLine() =>
        Render(new GraphA11yEvent.GraphStatus(new GraphStatusNote.LoadingConnections()));

    private static string NoConnectionsLine() =>
        Render(new GraphA11yEvent.GraphStatus(new GraphStatusNote.NoConnections()));

    // --- The record (B-17) and the queries (B-15) ---------------------------------

    [Theory]
    [InlineData(Hub, 1u)]
    [InlineData(Hub, 2u)]
    [InlineData(Hub, 3u)]
    [InlineData(Deep, 2u)]
    [InlineData("10.md", 1u)]
    [InlineData("self.md", 1u)]
    public void TheLeafsTreeIsTheSessionsRecordFieldByFieldForEveryPinnedPair(string path, uint depth)
    {
        using GraphVault vault = GraphVault.Copy("record");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(path);
            host.Leaf.SetDepth(depth);
            host.Settle();

            Assert.Equal(ConnectionsLoadState.Ready, host.Leaf.Publication.State);
            GraphConnectionsTree expected = host.Session.GraphConnectionsTree(path, depth, host.Leaf.Filter);
            GraphConnectionsTree actual = host.Leaf.Publication.Tree!;
            Assert.Equal(expected.CenterKey, actual.CenterKey);
            Assert.Equal(expected.Depth, actual.Depth);
            Assert.Equal(expected.SummaryCounts, actual.SummaryCounts);
            Assert.Equal(expected.Incoming, actual.Incoming);
            Assert.Equal(expected.Outgoing, actual.Outgoing);
            // The thirteen fields, one by one on the first row of each list —
            // the record IS the binding's, no host copy.
            foreach ((GraphConnectionRow[] rows, GraphConnectionRow[] theirs) in new[] { (actual.Incoming, expected.Incoming), (actual.Outgoing, expected.Outgoing) })
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    Assert.Equal(theirs[i].Id, rows[i].Id);
                    Assert.Equal(theirs[i].Level, rows[i].Level);
                    Assert.Equal(theirs[i].ParentId, rows[i].ParentId);
                    Assert.Equal(theirs[i].NodeId, rows[i].NodeId);
                    Assert.Equal(theirs[i].StableKey, rows[i].StableKey);
                    Assert.Equal(theirs[i].Label, rows[i].Label);
                    Assert.Equal(theirs[i].Path, rows[i].Path);
                    Assert.Equal(theirs[i].TargetRaw, rows[i].TargetRaw);
                    Assert.Equal(theirs[i].Kind, rows[i].Kind);
                    Assert.Equal(theirs[i].EmbedOnly, rows[i].EmbedOnly);
                    Assert.Equal(theirs[i].InLinks, rows[i].InLinks);
                    Assert.Equal(theirs[i].OutLinks, rows[i].OutLinks);
                    Assert.Equal(theirs[i].References, rows[i].References);
                }
            }
            // B-7: the bundle rides at depth one only.
            Assert.Equal(depth == 1, host.Leaf.Publication.Bundle is not null);
        });
    }

    [Fact]
    public void TheFilterIsCoresLocalFilterFetchedOnceAndTheDepthIsClampedThroughCore()
    {
        using GraphVault vault = GraphVault.Copy("filter-clamp");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            Assert.Equal(SlateUniffiMethods.GraphConnectionsFilter(), leaf.Filter);
            Assert.True(leaf.Filter.IncludeAttachments, "attachments ON — the mac's local filter");
            Assert.Equal(1, leaf.CrossingsForTests["graph_connections_filter"]);
            // B-5: the initial depth is core's clamp of one; every write is
            // the FFI clamp's result (the census proves the dataflow).
            Assert.Equal(1u, leaf.Depth);
            int before = leaf.CrossingsForTests["graph_clamp_connections_depth"];
            leaf.SetDepth(99);
            Assert.Equal(3u, leaf.Depth);
            leaf.SetDepth(0);
            Assert.Equal(1u, leaf.Depth);
            leaf.Deeper();
            leaf.Deeper();
            leaf.Deeper();
            Assert.Equal(3u, leaf.Depth);
            leaf.Shallower();
            Assert.Equal(2u, leaf.Depth);
            Assert.Equal(before + 6, leaf.CrossingsForTests["graph_clamp_connections_depth"]);
            // No root: no load for any of them (Term 3(e)).
            Assert.Equal(0, host.Loads);
        });
    }

    // --- The presentation and the request (Term 7, B-3) ---------------------------

    [Fact]
    public void TheFourStatesAndTheInFlightFlag()
    {
        using GraphVault vault = GraphVault.Copy("states");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            Assert.Equal(ConnectionsLoadState.NoNote, leaf.Publication.State);
            Assert.Null(leaf.Request);
            Assert.False(leaf.InFlight);
            Assert.True(leaf.IsCurrent);

            host.ActivateLeaf();
            host.OpenNote(Hub);
            // A different root installs Loading and a request in flight.
            Assert.Equal(ConnectionsLoadState.Loading, leaf.Publication.State);
            Assert.True(leaf.InFlight);
            Assert.Equal(Hub, leaf.Request!.Root);
            Assert.Equal(leaf.RootEpoch, leaf.Request.Epoch);
            host.Settle();
            Assert.Equal(ConnectionsLoadState.Ready, leaf.Publication.State);
            Assert.False(leaf.InFlight);
            Assert.True(leaf.IsCurrent);
            Assert.Same(leaf.Request, leaf.Publication.ProducedBy);

            // Error: a path that is no graph node fails the tree call.
            host.Workspace.OpenPath("missing-note.md", WorkspaceOpenTarget.NewTab);
            host.Settle();
            Assert.Equal(ConnectionsLoadState.Error, leaf.Publication.State);
            Assert.NotNull(leaf.Publication.Failure);
            Assert.False(leaf.InFlight);
        });
    }

    [Fact]
    public void ASameRootReloadKeepsThePresentationAndADifferentRootInstallsLoading()
    {
        using GraphVault vault = GraphVault.Copy("start-transition");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            ConnectionsPublication ready = leaf.Publication;

            // Same root (a depth change): Ready is KEPT until the swap — no
            // indicator (the mac's `isPayloadCurrent`).
            leaf.SetDepth(2);
            Assert.Same(ready, leaf.Publication);
            Assert.True(leaf.InFlight);
            Assert.Equal(2u, leaf.Request!.Depth);
            host.Settle();
            Assert.NotSame(ready, leaf.Publication);
            Assert.Equal(2u, leaf.Publication.Tree!.Depth);

            // A different root: Loading from any state, at once.
            host.OpenNote(Two);
            Assert.Equal(ConnectionsLoadState.Loading, leaf.Publication.State);
            Assert.Equal(Two, leaf.Publication.ProducedBy!.Root);
            host.Settle();
            Assert.Equal(ConnectionsLoadState.Ready, leaf.Publication.State);
        });
    }

    [Fact]
    public void ARootChangeWhileInactiveLeavesThePresentationStaleUntilTheSwitch()
    {
        using GraphVault vault = GraphVault.Copy("stale");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
            int loads = host.Loads;

            // Inactive: the root is tracked, nothing loads, the held tree
            // is STALE (Term 3(d), Term 7).
            host.OpenNote(Two);
            Assert.Equal(Two, leaf.Root);
            Assert.Equal(loads, host.Loads);
            Assert.True(leaf.IsStale);
            Assert.Equal(ConnectionsLoadState.Ready, leaf.Publication.State);
            Assert.Equal(Hub, leaf.Publication.ProducedBy!.Root);

            // The mounted switch to a STALE leaf loads (Term 3(b), B-D11).
            host.Clear();
            host.ActivateLeaf();
            Assert.Equal(loads + 1, host.Loads);
            host.Settle();
            Assert.True(leaf.IsCurrent);
            Assert.Equal(["LeafPanelShown", Summary(leaf)], host.Timeline);
        });
    }

    // --- The trigger matrix (Term 3): one load per route --------------------------

    [Fact]
    public void AMountedSwitchToACurrentLeafLoadsNothingAndSpeaksThePanelLineAlone()
    {
        using GraphVault vault = GraphVault.Copy("switch-current");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
            int loads = host.Loads;
            host.Clear();

            host.ActivateLeaf();
            host.Settle();

            Assert.Equal(loads, host.Loads);
            Assert.Equal(["LeafPanelShown"], host.Timeline);
        });
    }

    [Fact]
    public void ShowLoadsOnceWhetherCurrentOrNotAndNamesThePanelOnce()
    {
        using GraphVault vault = GraphVault.Copy("show");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.OpenNote(Hub);
            host.Clear();

            // Not active: the setter's line, then ONE load's summary.
            host.Workspace.ShowConnections();
            Assert.Equal(1, host.Loads);
            host.Settle();
            Assert.Equal(["LeafPanelShown", Summary(leaf)], host.Timeline);

            // Active and current: the graph family's line, then one load.
            host.Clear();
            host.Workspace.ShowConnections();
            Assert.Equal(2, host.Loads);
            host.Settle();
            Assert.Equal([PanelLine(), Summary(leaf)], host.Timeline);
        });
    }

    [Fact]
    public void ShowFromACollapsedPaneIssuesOneLoadAfterTheShellsPaneLine()
    {
        using GraphVault vault = GraphVault.Copy("show-collapsed");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.OpenNote(Hub);
            host.Workspace.IsRightPaneVisible = false;
            host.Clear();

            host.Workspace.ShowConnections();

            // B-D7: one load, not the mac's two; B-D9: the shell's pane line.
            Assert.Equal(1, host.Loads);
            Assert.False(host.Workspace.ConnectionsMountPendingForTests);
            host.Settle();
            Assert.Equal(["RightPaneShown", "LeafPanelShown", Summary(host.Leaf)], host.Timeline);
        });
    }

    [Fact]
    public void APaneRevealWithTheLeafActiveMountsAndLoadsOnce()
    {
        using GraphVault vault = GraphVault.Copy("mount");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            host.Workspace.ToggleRightPaneCommand.Execute(null);
            Assert.False(host.Workspace.IsRightPaneVisible);
            int loads = host.Loads;
            host.Clear();

            host.Workspace.ToggleRightPaneCommand.Execute(null);

            Assert.True(host.Workspace.IsRightPaneVisible);
            Assert.Equal(loads + 1, host.Loads);
            Assert.False(host.Workspace.ConnectionsMountPendingForTests);
            host.Settle();
            Assert.Equal(["RightPaneShown", Summary(host.Leaf)], host.Timeline);
        });
    }

    [Fact]
    public void ARevealThenSwitchCommandMountsTheOtherLeafAndLoadsNothingHere()
    {
        using GraphVault vault = GraphVault.Copy("reveal-then-switch");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            host.Workspace.IsRightPaneVisible = false;
            int loads = host.Loads;

            // IGG-3: Windows reveals BEFORE it switches; MOUNT is evaluated
            // at the route's end with the review active — no leaf load.
            host.Workspace.OpenTasksReview();
            host.Settle();
            Assert.Equal(loads, host.Loads);
            Assert.False(host.Workspace.ConnectionsMountPendingForTests);
            Assert.Equal("tasksReview", host.Workspace.ActiveLeaf.Id);
        });
    }

    [Fact]
    public void ARootChangeLoadsOnlyWhileActiveAndMounted()
    {
        using GraphVault vault = GraphVault.Copy("root-change");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            int loads = host.Loads;

            host.OpenNote(Two);
            Assert.Equal(loads + 1, host.Loads);
            host.Settle();

            host.Workspace.IsRightPaneVisible = false;
            host.OpenNote(Hub);
            Assert.Equal(loads + 1, host.Loads);
            Assert.True(host.Leaf.IsStale);
        });
    }

    [Fact]
    public void ADepthChangeLoadsWithARootAndNotWithoutAndABoundIsANoOp()
    {
        using GraphVault vault = GraphVault.Copy("depth");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            int loads = host.Loads;
            host.Clear();

            host.Workspace.ConnectionsDeeperCommand.Execute(null);
            Assert.Equal(loads + 1, host.Loads);
            host.Settle();
            Assert.Equal([Summary(host.Leaf)], host.Timeline);

            host.Clear();
            host.Workspace.ConnectionsDeeperCommand.Execute(null);
            host.Workspace.ConnectionsDeeperCommand.Execute(null);
            Assert.Equal(loads + 2, host.Loads);
            host.Settle();
            Assert.Equal(3u, host.Leaf.Depth);
            Assert.Equal([Summary(host.Leaf)], host.Timeline);

            // Both commands stay enabled at the bounds (B-14).
            Assert.True(host.Workspace.ConnectionsDeeperCommand.CanExecute(null));
            Assert.True(host.Workspace.ConnectionsShallowerCommand.CanExecute(null));
        });
    }

    [Fact]
    public void RootToNoneInstallsNoNoteSynchronouslyAndDropsTheInFlightResult()
    {
        using GraphVault vault = GraphVault.Copy("root-to-none");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            Assert.True(leaf.InFlight);
            host.Clear();

            // The graph tab is not a note: NoNote at once, the in-flight
            // load's result foreign (Term 3(g)).
            host.Workspace.OpenGraph();
            Assert.Equal(ConnectionsLoadState.NoNote, leaf.Publication.State);
            Assert.Null(leaf.Root);
            Assert.False(leaf.InFlight);
            host.Settle();
            Assert.Equal(ConnectionsLoadState.NoNote, leaf.Publication.State);
            Assert.DoesNotContain(host.RelayLines, line => line.Contains("Links", StringComparison.Ordinal) && line.Contains("hub", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void LaunchWithTheLeafActiveAndANoteRestoredIsTheSeededMountsOneLoad()
    {
        using GraphVault vault = GraphVault.Copy("launch");
        PumpedDispatcher.Run(() =>
        {
            using (var first = new Host(vault.Root))
            {
                first.ActivateLeaf();
                first.OpenNote(Hub);
                first.Settle();
            }

            using var host = new Host(vault.Root);
            Assert.Equal("connections", host.Workspace.ActiveLeaf.Id);
            Assert.Equal(Hub, host.Leaf.Root);
            // ONE load: the seeded mount's; the first sync was the initial
            // value (Term 3(a), 3(d), B-1); no RightPaneShown.
            Assert.Equal(1, host.Loads);
            Assert.False(host.Workspace.ConnectionsMountPendingForTests);
            Assert.DoesNotContain(host.ShellEvents, e => e is A11yEvent.RightPaneShown);
            host.Settle();
            Assert.Equal(ConnectionsLoadState.Ready, host.Leaf.Publication.State);
        });
    }

    [Fact]
    public void LaunchWithAnotherLeafActiveLoadsNothing()
    {
        using GraphVault vault = GraphVault.Copy("launch-other");
        PumpedDispatcher.Run(() =>
        {
            using (var first = new Host(vault.Root))
            {
                first.OpenNote(Hub);
                first.Settle();
            }

            using var host = new Host(vault.Root);
            Assert.NotEqual("connections", host.Workspace.ActiveLeaf.Id);
            Assert.Equal(Hub, host.Leaf.Root);
            Assert.Equal(0, host.Loads);
            Assert.True(host.Leaf.IsStale);
        });
    }

    // --- The probe's state machine (Term 6, B-12) -----------------------------------

    [Fact]
    public void TheProbeReloadsSilentlyWhenTheHeldTreeIsOlderAndNotWhenEqual()
    {
        using GraphVault vault = GraphVault.Copy("probe");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            int loads = host.Loads;
            host.Clear();

            // Equal: nothing.
            host.Workspace.NotifyGraphOfVaultChange();
            host.Settle();
            Assert.Equal(loads, host.Loads);
            Assert.Empty(host.RelayLines);

            // Older: a new link into hub bumps the generation — a SILENT load.
            File.WriteAllText(Path.Combine(vault.Root, "probe-new.md"), "[[hub]]\n");
            using (var cancel = new CancelToken())
            {
                host.Session.ScanInitial(cancel);
            }
            host.Workspace.NotifyGraphOfVaultChange();
            host.Settle();
            Assert.Equal(loads + 1, host.Loads);
            Assert.Empty(host.RelayLines);
            Assert.Equal(ConnectionsLoadState.Ready, leaf.Publication.State);
        });
    }

    [Fact]
    public void TheProbeDefersToALoadInFlightAndNeverSupersedesAnAudibleOne()
    {
        using GraphVault vault = GraphVault.Copy("probe-in-flight");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            Assert.True(leaf.InFlight);
            int loads = host.Loads;
            host.Clear();

            // In flight: the mark only — no second tree crossing; the
            // audible load lands and speaks (B-D12).
            host.Workspace.NotifyGraphOfVaultChange();
            host.Settle();
            Assert.Equal(loads, host.Loads);
            Assert.Equal([Summary(leaf)], host.RelayLines);
        });
    }

    /// <summary>The high-water mark (Term 6, B-D12): a generation marked
    /// while a load is in flight reloads silently at the install ONLY when
    /// it is strictly above the installed tree's generation, and that
    /// reload clears the mark; a mark equal to the tree's generation
    /// reloads nothing and stays (codoki on PR #1184 asked for the pin).</summary>
    [Fact]
    public void TheHighWaterMarkReloadsOnlyWhenStrictlyAboveTheInstalledTreeAndClearsAfterwards()
    {
        using GraphVault vault = GraphVault.Copy("high-water");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();

            // EQUAL: the fetch parks after its crossing (its tree carries the
            // current generation), the probe marks that same generation, and
            // the install finds the mark not above the tree — no reload.
            (ManualResetEventSlim gate, ManualResetEventSlim reached) = Park(leaf);
            host.OpenNote(Hub);
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the fetch never parked");
            host.Workspace.NotifyGraphOfVaultChange();
            Assert.True(
                SpinWait.SpinUntil(() =>
                {
                    PumpedDispatcher.Drain();
                    return leaf.HighWaterForTests > 0;
                }, TimeSpan.FromSeconds(10)),
                "the probe marked nothing while the load was in flight");
            ulong marked = leaf.HighWaterForTests;
            int loads = host.Loads;
            gate.Set();
            host.Settle();
            Assert.Equal(loads, host.Loads);
            Assert.Equal(marked, leaf.HighWaterForTests);
            Assert.Equal(ConnectionsLoadState.Ready, leaf.Publication.State);

            // STRICTLY ABOVE: the fetch parks with the OLD generation in its
            // tree, the vault moves on, the probe marks the new one; the
            // install speaks the audible load's summary (the old tree's),
            // reloads silently ONCE, installs the new tree and clears the mark.
            string spokenForTheOldTree = Render(new GraphA11yEvent.GraphNeighborhoodSummary(
                host.Session.GraphConnectionsTree(Two, 1, SlateUniffiMethods.GraphConnectionsFilter()).SummaryCounts));
            (gate, reached) = Park(leaf);
            host.OpenNote(Two);
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the second fetch never parked");
            File.WriteAllText(Path.Combine(vault.Root, "high-water-new.md"), "[[2]]\n");
            using (var cancel = new CancelToken())
            {
                host.Session.ScanInitial(cancel);
            }
            host.Workspace.NotifyGraphOfVaultChange();
            Assert.True(
                SpinWait.SpinUntil(() =>
                {
                    PumpedDispatcher.Drain();
                    return leaf.HighWaterForTests > marked;
                }, TimeSpan.FromSeconds(10)),
                "the probe did not mark the moved generation");
            loads = host.Loads;
            host.Clear();
            gate.Set();
            host.Settle();
            Assert.Equal(loads + 1, host.Loads);
            Assert.Equal(0UL, leaf.HighWaterForTests);
            // An explicit array here, not the collection expression the rest
            // of the file uses: the PR's bot reviewer read the expression as
            // invalid syntax on two heads running (the build passed on both);
            // the form is a concession to its fixation, not a defect.
            Assert.Equal(new[] { spokenForTheOldTree }, host.RelayLines);
            Assert.NotEqual(spokenForTheOldTree, Summary(leaf));
        });
    }

    [Fact]
    public void TheProbeReloadsAnErroredAndAStaleLeafOnce()
    {
        using GraphVault vault = GraphVault.Copy("probe-error-stale");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.Workspace.OpenPath("missing-note.md", WorkspaceOpenTarget.CurrentTab);
            host.Settle();
            Assert.Equal(ConnectionsLoadState.Error, leaf.Publication.State);
            int loads = host.Loads;

            // Error: ONE silent load (IGH-2).
            host.Workspace.NotifyGraphOfVaultChange();
            host.Settle();
            Assert.Equal(loads + 1, host.Loads);
            Assert.Equal(ConnectionsLoadState.Error, leaf.Publication.State);

            // Stale: the root moved while inactive; the probe loads it.
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
            host.OpenNote(Hub);
            Assert.True(leaf.IsStale);
            loads = host.Loads;
            host.Clear();
            host.Workspace.NotifyGraphOfVaultChange();
            host.Settle();
            Assert.Equal(loads + 1, host.Loads);
            Assert.True(leaf.IsCurrent);
            Assert.Empty(host.RelayLines);
        });
    }

    [Fact]
    public void ClosingASplitsSoleTabIsOneReconciliationAtTheBoundary()
    {
        using GraphVault vault = GraphVault.Copy("split-close");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            // A split duplicates the note into a second group: the same root.
            host.Workspace.SplitRightCommand.Execute(null);
            host.Settle();
            Assert.Equal(2, host.Workspace.Groups.Count);
            ConnectionsPublication held = leaf.Publication;
            int loads = host.Loads;
            host.Clear();

            // Closing the split's sole tab passes through A → none → A inside
            // ONE mutation (IGH-4): reconciled once at the boundary with the
            // final root — no NoNote flash, no load, the presentation kept.
            host.Workspace.CloseActiveTabCommand.Execute(null);
            host.Settle();

            Assert.Single(host.Workspace.Groups);
            Assert.Equal(Hub, leaf.Root);
            Assert.Equal(loads, host.Loads);
            Assert.Same(held, leaf.Publication);
            Assert.Empty(host.RelayLines);
        });
    }

    [Fact]
    public void TheProbeWithNoRootDoesNothing()
    {
        using GraphVault vault = GraphVault.Copy("probe-no-root");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.Workspace.NotifyGraphOfVaultChange();
            host.Settle();
            Assert.Equal(0, host.Loads);
            Assert.Equal(1, host.Leaf.CrossingsForTests["graph_generation"]);
        });
    }
}
