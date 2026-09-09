// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Commands;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR C (#746), contracts C-9 and C-10, rule W Terms W3–W7: the
/// preferences object — the loaded level, the parameterised setter, the
/// triggers updating CurrentConfig by field, the schedule's reserved
/// generation, the tick's and the shutdown's one transfer, the drain —
/// and, over a real workspace, the seed, the fresh open's re-apply, the
/// live level read by the document and the leaf, the aggregate that is
/// CurrentConfig alone, and the lifecycle's flush-before-read order.
/// </summary>
public sealed class GraphPreferencesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"slate-graph-prefs-{Guid.NewGuid():N}");

    public GraphPreferencesTests() => Directory.CreateDirectory(_root);

    public void Dispose() => DeleteVault(_root);

    /// <summary>The graph vault of 0b-13, copied into a temp root.</summary>
    private static string CopyGraphVault(string label)
    {
        string source = Path.Combine(SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures", "graph_vault");
        string root = Path.Combine(Path.GetTempPath(), $"slate-graph-prefs-{label}-{Guid.NewGuid():N}");
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(root, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        return root;
    }

    private static void DeleteVault(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>A workspace over a scanned session, the graph relay's
    /// rendered lines captured.</summary>
    private sealed class Host : IDisposable
    {
        public VaultSession Session { get; }

        public WorkspaceViewModel Workspace { get; }

        public List<string> GraphLines { get; } = [];

        public Host(string root)
        {
            Session = VaultSession.OpenFilesystem(root);
            using var cancel = new CancelToken();
            Session.ScanInitial(cancel);
            Workspace = new WorkspaceViewModel(
                Session,
                root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                announceRendered: line => GraphLines.Add(line.Text));
        }

        public GraphDocumentViewModel Document => Workspace.GraphDocument!;

        public GraphPreferencesViewModel Preferences => Workspace.GraphPreferences;

        public GraphViewState State => Workspace.GraphViewStateForTests;

        public WorkspaceTabViewModel GraphTab => Workspace.Groups.SelectMany(g => g.Tabs).First(t => t.IsGraph);

        public void OpenGraph()
        {
            Workspace.OpenGraph();
            Settle();
        }

        public void Settle()
        {
            if (Workspace.GraphDocument is { } document)
            {
                PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            }
            PumpedDispatcher.Drain();
        }

        public void Dispose()
        {
            Workspace.Dispose();
            Session.Dispose();
        }
    }

    private GraphConfig OnDisk() => new GraphConfigStore(_root).Read().Config;

    private static GraphConfig OnDisk(string root) => new GraphConfigStore(root).Read().Config;

    private static void Drain(GraphPreferencesViewModel preferences) =>
        Assert.True(preferences.WhenWritesDrained().Wait(TimeSpan.FromSeconds(10)), "the writes never drained");

    /// <summary>The application writer's queue for a root, idle.</summary>
    private static void WaitForTheSharedWriter(string root)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (GraphConfigWriter.Shared.StateForTests(root).Outstanding > 0 && clock.Elapsed < TimeSpan.FromSeconds(10))
        {
            Thread.Sleep(10);
        }
        Assert.Equal(0, GraphConfigWriter.Shared.StateForTests(root).Outstanding);
    }

    private static GraphVerbositySpec Level(GraphPreferencesViewModel preferences, GraphVerbosity verbosity) =>
        preferences.Levels.Single(level => level.Verbosity == verbosity);

    // --- C-9: the level ------------------------------------------------------

    [Fact]
    public void TheDefaultAndTheLoadedLevel()
    {
        PumpedDispatcher.Run(() =>
        {
            var fresh = new GraphPreferencesViewModel(_root, new GraphConfigWriter());
            Assert.Equal(GraphVerbosity.Standard, fresh.Verbosity);
            Assert.True(fresh.IsWritable);
            Assert.Null(fresh.LoadFailure);
            // Core's vector, in its order (design B: fetched once per process).
            Assert.Equal(SlateUniffiMethods.GraphVerbosities().Select(l => l.Tag), fresh.Levels.Select(l => l.Tag));
            Assert.Equal(["terse", "standard", "verbose"], fresh.Levels.Select(l => l.Tag));
            Assert.Equal(fresh.Levels.Select(l => l.Title), fresh.Choices.Select(c => c.Title));
            Assert.Equal(fresh.Levels.Select(l => l.Tag), fresh.Choices.Select(c => c.Tag));
            Assert.Equal([false, true, false], fresh.Choices.Select(c => c.IsSelected));
            Assert.True(fresh.IsSelected(Level(fresh, GraphVerbosity.Standard)));
            Assert.False(fresh.IsSelected(Level(fresh, GraphVerbosity.Terse)));
            fresh.Shutdown();

            new GraphConfigStore(_root).Write(SlateUniffiMethods.GraphConfigDefault() with { Verbosity = GraphVerbosity.Verbose });
            var loaded = new GraphPreferencesViewModel(_root, new GraphConfigWriter());
            Assert.Equal(GraphVerbosity.Verbose, loaded.Verbosity);
            Assert.Equal(GraphVerbosity.Verbose, loaded.CurrentConfig.Verbosity);
            Assert.True(loaded.IsSelected(Level(loaded, GraphVerbosity.Verbose)));
            Assert.Equal([false, false, true], loaded.Choices.Select(c => c.IsSelected));
            loaded.Shutdown();
        });
    }

    [Fact]
    public void TheSetterAcceptsTheVectorsTagsAndIgnoresAnUnknownOne()
    {
        PumpedDispatcher.Run(() =>
        {
            var preferences = new GraphPreferencesViewModel(_root, new GraphConfigWriter());
            var changed = new List<string>();
            int raised = 0;
            preferences.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
            preferences.VerbosityChanged += () => raised++;
            Assert.True(preferences.SetVerbosityCommand.CanExecute("terse"));
            foreach (GraphVerbositySpec level in preferences.Levels.Where(l => l.Verbosity != GraphVerbosity.Standard))
            {
                preferences.SetVerbosityCommand.Execute(level.Tag);
                Assert.Equal(level.Verbosity, preferences.Verbosity);
                Assert.Equal(level.Verbosity, preferences.CurrentConfig.Verbosity);
                Assert.Equal(level.Verbosity, preferences.Choices.Single(c => c.IsSelected).Spec.Verbosity);
            }
            Assert.Equal(2, raised);
            Assert.Equal(2, changed.Count(name => name == nameof(GraphPreferencesViewModel.Verbosity)));
            GraphVerbosity before = preferences.Verbosity;
            preferences.SetVerbosityCommand.Execute("bogus");
            preferences.SetVerbosityCommand.Execute("TERSE");
            preferences.SetVerbosityCommand.Execute(null);
            preferences.SetVerbosityCommand.Execute(GraphVerbosity.Terse);
            Assert.Equal(before, preferences.Verbosity);
            Assert.Equal(2, raised);
            preferences.Shutdown();
        });
    }

    [Fact]
    public void ReselectingTheLevelReassertsAndStoresNothing()
    {
        PumpedDispatcher.Run(() =>
        {
            var preferences = new GraphPreferencesViewModel(_root, new GraphConfigWriter());
            int reasserted = 0;
            int raised = 0;
            preferences.VerbosityChanged += () => raised++;
            foreach (GraphVerbosityChoice choice in preferences.Choices)
            {
                choice.PropertyChanged += (_, e) => reasserted += e.PropertyName == nameof(GraphVerbosityChoice.IsSelected) ? 1 : 0;
            }
            preferences.SetVerbosityCommand.Execute("standard");
            // m1's rule: every check item re-pushes its binding; nothing
            // stored, nothing scheduled, no change raised.
            Assert.Equal(3, reasserted);
            Assert.Equal(0, raised);
            Assert.False(preferences.HasPendingForTests, "nothing stored");
            Assert.False(preferences.TimerEnabledForTests);
            Assert.Equal(GraphVerbosity.Standard, preferences.Verbosity);
            Assert.Equal([false, true, false], preferences.Choices.Select(c => c.IsSelected));
            preferences.Shutdown();
        });
    }

    [Fact]
    public void ALevelChangeSchedulesASaveCarryingIt()
    {
        PumpedDispatcher.Run(() =>
        {
            var writer = new GraphConfigWriter();
            var preferences = new GraphPreferencesViewModel(_root, writer);
            preferences.SetVerbosityCommand.Execute("terse");
            Assert.True(preferences.HasPendingForTests);
            Assert.Equal(1UL, preferences.PendingGenerationForTests);
            Assert.Equal(GraphVerbosity.Terse, preferences.PendingAggregateForTests!.Verbosity);
            Assert.True(preferences.TimerEnabledForTests);
            preferences.FireTickForTests();
            Assert.False(preferences.HasPendingForTests);
            Drain(preferences);
            Assert.Equal(GraphVerbosity.Terse, OnDisk().Verbosity);
            preferences.Shutdown();
        });
    }

    [Fact]
    public void TheDispositionsArePresentUnregisteredAndTagTrue()
    {
        string[] tags = SlateUniffiMethods.GraphVerbosities().Select(l => l.Tag).ToArray();
        Assert.Equal(["terse", "standard", "verbose"], tags);
        foreach (string tag in tags)
        {
            string id = "windows.graph.setVerbosity" + char.ToUpperInvariant(tag[0]) + tag[1..];
            ChordTableEntry row = Assert.Single(ChordTable.Entries, r => r.Id == id);
            Assert.False(row.IsRegistered);
            Assert.False(row.IsCommandId);
            Assert.Equal(CommandSection.Graph, row.Section);
            Assert.Null(row.WindowsChord);
            Assert.Null(row.MacChord);
            Assert.NotNull(row.Reason);
            Assert.Contains("parameterized setter", row.Reason, StringComparison.Ordinal);
        }
        Assert.Equal(3, ChordTable.Entries.Count(r => r.Id.StartsWith("windows.graph.setVerbosity", StringComparison.Ordinal)));
    }

    // --- Term W7: CurrentConfig by field ------------------------------------

    [Fact]
    public void TheAggregatesFields()
    {
        PumpedDispatcher.Run(() =>
        {
            var preferences = new GraphPreferencesViewModel(_root, new GraphConfigWriter());
            GraphConfig initial = preferences.CurrentConfig;
            preferences.SetNameQuery("hub");
            GraphConfigs.AssertEqual(initial with { Filters = initial.Filters with { NameQuery = "hub" } }, preferences.CurrentConfig);
            preferences.SetConnectionsDepth(3);
            Assert.Equal(3u, preferences.CurrentConfig.ConnectionsDepth);
            Assert.Equal("hub", preferences.CurrentConfig.Filters.NameQuery);
            preferences.SetMode(GraphSurfaceMode.Diagram);
            Assert.Equal(GraphSurfaceMode.Diagram, preferences.CurrentConfig.Mode);
            preferences.SetVerbosityCommand.Execute("verbose");
            Assert.Equal(GraphVerbosity.Verbose, preferences.CurrentConfig.Verbosity);
            // Each trigger its field and no other: the rest as held.
            Assert.Same(initial.Groups, preferences.CurrentConfig.Groups);
            Assert.Equal(initial.Display, preferences.CurrentConfig.Display);
            Assert.Equal(initial.Forces, preferences.CurrentConfig.Forces);
            Assert.Equal(initial.Filters with { NameQuery = "hub" }, preferences.CurrentConfig.Filters);
            Assert.Equal(3u, preferences.CurrentConfig.ConnectionsDepth);
            Assert.Equal(GraphSurfaceMode.Diagram, preferences.CurrentConfig.Mode);
            preferences.Shutdown();
        });
    }

    [Fact]
    public void CurrentConfigIsUpdatedBeforeEverySchedule()
    {
        PumpedDispatcher.Run(() =>
        {
            var preferences = new GraphPreferencesViewModel(_root, new GraphConfigWriter());
            preferences.SetNameQuery("hub");
            Assert.Equal("hub", preferences.PendingAggregateForTests!.Filters.NameQuery);
            preferences.SetConnectionsDepth(2);
            Assert.Equal(2u, preferences.PendingAggregateForTests!.ConnectionsDepth);
            Assert.Equal("hub", preferences.PendingAggregateForTests!.Filters.NameQuery);
            preferences.SetMode(GraphSurfaceMode.Diagram);
            Assert.Equal(GraphSurfaceMode.Diagram, preferences.PendingAggregateForTests!.Mode);
            preferences.SetVerbosityCommand.Execute("terse");
            Assert.Equal(GraphVerbosity.Terse, preferences.PendingAggregateForTests!.Verbosity);
            // The pending aggregate IS CurrentConfig (an immutable record).
            Assert.Same(preferences.CurrentConfig, preferences.PendingAggregateForTests);
            preferences.Shutdown();
        });
    }

    [Fact]
    public void SetModeUpdatesCurrentConfigAndSchedules()
    {
        PumpedDispatcher.Run(() =>
        {
            var preferences = new GraphPreferencesViewModel(_root, new GraphConfigWriter());
            preferences.SetMode(GraphSurfaceMode.Diagram);
            Assert.Equal(GraphSurfaceMode.Diagram, preferences.CurrentConfig.Mode);
            Assert.True(preferences.HasPendingForTests);
            Assert.Equal(1UL, preferences.PendingGenerationForTests);
            preferences.Shutdown();
            Drain(preferences);
            Assert.Equal(GraphSurfaceMode.Diagram, OnDisk().Mode);
        });
    }

    // --- Terms W3–W5: the schedule, the hand-off, the flush -----------------

    [Fact]
    public void TenSchedulesIn400msEnqueueOnceWithTheLastAggregate()
    {
        PumpedDispatcher.Run(() =>
        {
            var writer = new GraphConfigWriter();
            var preferences = new GraphPreferencesViewModel(_root, writer);
            for (int i = 0; i < 10; i++)
            {
                preferences.SetNameQuery($"n{i}");
            }
            // Term W3: a generation reserved at EACH schedule; one pair pends.
            Assert.Equal(10UL, preferences.PendingGenerationForTests);
            Assert.Equal("n9", preferences.PendingAggregateForTests!.Filters.NameQuery);
            Assert.Equal(0, preferences.OutstandingForTests);
            Assert.Equal((10UL, 0UL, 0UL, 0), writer.StateForTests(_root));
            preferences.FireTickForTests();
            Assert.Equal(1, preferences.OutstandingForTests);
            Drain(preferences);
            Assert.Equal("n9", OnDisk().Filters.NameQuery);
            Assert.Equal((10UL, 10UL, 10UL, 0), writer.StateForTests(_root));
            preferences.Shutdown();
        });
    }

    [Fact]
    public void TheTimerFiresTheTickAfterTheWindow()
    {
        PumpedDispatcher.Run(() =>
        {
            var preferences = new GraphPreferencesViewModel(_root, new GraphConfigWriter(), window: TimeSpan.FromMilliseconds(20));
            preferences.SetNameQuery("hub");
            Assert.True(preferences.HasPendingForTests);
            Assert.True(PumpedDispatcher.PumpUntil(() => !preferences.HasPendingForTests), "the timer never ticked");
            Assert.False(preferences.TimerEnabledForTests);
            Drain(preferences);
            Assert.Equal("hub", OnDisk().Filters.NameQuery);
            preferences.Shutdown();
        });
    }

    [Fact]
    public void ATickAndAShutdownTransferOnePendingPairExactlyOnce()
    {
        PumpedDispatcher.Run(() =>
        {
            var writer = new GraphConfigWriter();
            var preferences = new GraphPreferencesViewModel(_root, writer);
            preferences.SetNameQuery("one");
            preferences.FireTickForTests();
            preferences.FireTickForTests();
            Assert.Equal(1, preferences.OutstandingForTests);
            Assert.False(preferences.HasPendingForTests);
            preferences.SetNameQuery("two");
            preferences.Shutdown();
            Assert.True(preferences.IsShutForTests);
            Assert.False(preferences.HasPendingForTests);
            preferences.FireTickForTests();
            preferences.Shutdown();
            Drain(preferences);
            Assert.Equal("two", OnDisk().Filters.NameQuery);
            Assert.Equal((2UL, 2UL, 2UL, 0), writer.StateForTests(_root));
        });
    }

    [Fact]
    public void AScheduleAfterShutdownIsRefused()
    {
        PumpedDispatcher.Run(() =>
        {
            var preferences = new GraphPreferencesViewModel(_root, new GraphConfigWriter());
            preferences.SetNameQuery("hub");
            preferences.Shutdown();
            preferences.SetNameQuery("later");
            preferences.SetConnectionsDepth(3);
            preferences.SetMode(GraphSurfaceMode.Diagram);
            preferences.SetVerbosityCommand.Execute("terse");
            preferences.ScheduleSave();
            Assert.Equal(5, preferences.RefusedForTests);
            Assert.False(preferences.HasPendingForTests);
            Assert.False(preferences.TimerEnabledForTests);
            // The fields still move (the workspace is gone; nothing persists).
            Assert.Equal("later", preferences.CurrentConfig.Filters.NameQuery);
            Drain(preferences);
            Assert.Equal("hub", OnDisk().Filters.NameQuery);
            Assert.Equal(GraphVerbosity.Standard, OnDisk().Verbosity);
        });
    }

    [Fact]
    public void AnEditWithin400msOfCloseIsEnqueuedAndDrained()
    {
        PumpedDispatcher.Run(() =>
        {
            var preferences = new GraphPreferencesViewModel(_root, new GraphConfigWriter());
            preferences.SetNameQuery("hub");
            Assert.True(preferences.TimerEnabledForTests);
            // The timer never ticked: the shutdown itself enqueues the pair.
            preferences.Shutdown();
            Assert.False(preferences.HasPendingForTests);
            Assert.Equal(0, preferences.RefusedForTests);
            Drain(preferences);
            Assert.Equal("hub", OnDisk().Filters.NameQuery);
        });
    }

    [Fact]
    public void TheDrainWaitsForAnOutstandingWrite()
    {
        PumpedDispatcher.Run(() =>
        {
            var writer = new GraphConfigWriter();
            using var gate = new ManualResetEventSlim(false);
            using var reached = new ManualResetEventSlim(false);
            writer.WriteGateForTests = (_, _) =>
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(10));
            };
            var preferences = new GraphPreferencesViewModel(_root, writer);
            preferences.SetNameQuery("hub");
            preferences.Shutdown();
            Task drain = preferences.WhenWritesDrained();
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(drain.IsCompleted, "the drain waits for the parked write");
            gate.Set();
            Assert.True(drain.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal("hub", OnDisk().Filters.NameQuery);
        });
    }

    // --- C-10 over a real workspace ------------------------------------------

    [Fact]
    public void NoTimerRetainsADisposedWorkspace()
    {
        string vault = CopyGraphVault("no-timer");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                var host = new Host(vault);
                GraphPreferencesViewModel preferences = host.Preferences;
                ConnectionsLeafViewModel leaf = host.Workspace.Connections;
                var leafChanges = new List<string>();
                leaf.PropertyChanged += (_, e) => leafChanges.Add(e.PropertyName ?? string.Empty);
                host.Workspace.GraphNavigator.SetNameQuery("hub");
                Assert.True(preferences.TimerEnabledForTests);
                host.Dispose();
                // The retired leaf unsubscribed: a level change forwards nothing.
                preferences.SetVerbosityCommand.Execute("terse");
                Assert.True(leaf.IsRetired);
                Assert.DoesNotContain(nameof(ConnectionsLeafViewModel.Verbosity), leafChanges);
                Assert.True(preferences.IsShutForTests);
                Assert.False(preferences.TimerEnabledForTests);
                Assert.False(preferences.HasPendingForTests);
                // A later tick or schedule reaches nothing.
                preferences.FireTickForTests();
                preferences.ScheduleSave();
                Assert.Equal(2, preferences.RefusedForTests);
                Assert.False(preferences.TimerEnabledForTests);
            });
            WaitForTheSharedWriter(vault);
            Assert.Equal("hub", OnDisk(vault).Filters.NameQuery);
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    private static GraphConfig Persisted() => SlateUniffiMethods.GraphConfigDefault() with
    {
        Filters = new GraphFilterConfig(IncludeAttachments: true, IncludeGhosts: true, OrphansOnly: false, NameQuery: "hub"),
        ConnectionsDepth = 3,
        Mode = GraphSurfaceMode.Diagram,
        Verbosity = GraphVerbosity.Verbose,
        Groups = [new GraphGroup("tag:x", GraphColorToken.Blue, GraphRingStyle.Solid)],
    };

    [Fact]
    public void TheSeedOfEachViewStateFieldAndTheLeafsDepth()
    {
        string vault = CopyGraphVault("seed");
        try
        {
            new GraphConfigStore(vault).Write(Persisted());
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphViewState state = host.State;
                // The one mapper: the four persisted fields onto core's query; no overlay.
                Assert.Equal(new GraphFilter(true, true, false), state.Filter);
                Assert.Equal("hub", state.NameQuery);
                Assert.Null(state.KindOnly);
                Assert.Equal(Persisted().Groups, state.Groups);
                Assert.Equal(GraphVerbosity.Verbose, host.Preferences.Verbosity);
                // The persisted 3 reaches the leaf, clamped through core.
                Assert.Equal(3u, host.Workspace.Connections.Depth);
                host.OpenGraph();
                Assert.Equal(GraphVerbosity.Verbose, host.Document.Verbosity);
                Assert.Equal(new GraphVisibilityQuery(new GraphFilter(true, true, false), "hub", null), host.Document.Publication.Query);
            });
            // A persisted 99 clamps through core to the ceiling.
            new GraphConfigStore(vault).Write(Persisted() with { ConnectionsDepth = 99 });
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                Assert.Equal(GraphCoreConstants.Once.ConnectionsDepthMax, host.Workspace.Connections.Depth);
                Assert.Equal(3u, host.Workspace.Connections.Depth);
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    [Fact]
    public void ADiagramModeSeedsTable()
    {
        string vault = CopyGraphVault("diagram-mode");
        try
        {
            new GraphConfigStore(vault).Write(Persisted());
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                // C-D6: the file keeps diagram — CurrentConfig holds it — while
                // the view state seeds Table, the one mode reachable in this PR.
                Assert.Equal(GraphSurfaceMode.Table, host.State.Mode);
                Assert.Equal(GraphSurfaceMode.Diagram, host.Preferences.CurrentConfig.Mode);
                // A save under it persists the file's diagram, never the Table seed.
                host.Preferences.SetVerbosityCommand.Execute("terse");
                host.Preferences.FireTickForTests();
                Drain(host.Preferences);
            });
            Assert.Equal(GraphSurfaceMode.Diagram, OnDisk(vault).Mode);
            Assert.Equal(GraphVerbosity.Terse, OnDisk(vault).Verbosity);
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    [Fact]
    public void APersistedDepthReachesTheLeafBeforeAnyGraphTabExists()
    {
        string vault = CopyGraphVault("depth-before-tab");
        try
        {
            new GraphConfigStore(vault).Write(SlateUniffiMethods.GraphConfigDefault() with { ConnectionsDepth = 2 });
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                Assert.Null(host.Workspace.GraphDocument);
                Assert.Equal(2u, host.Workspace.Connections.Depth);
                Assert.Equal(2u, host.Preferences.CurrentConfig.ConnectionsDepth);
                // The seed schedules nothing: only a CHANGE is a trigger.
                Assert.False(host.Preferences.HasPendingForTests);
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    [Fact]
    public void TheDocumentAndTheLeafReadTheLevelLiveAndForwardItsChange()
    {
        string vault = CopyGraphVault("live-level");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                host.OpenGraph();
                var documentChanges = new List<string>();
                var leafChanges = new List<string>();
                host.Document.PropertyChanged += (_, e) => documentChanges.Add(e.PropertyName ?? string.Empty);
                host.Workspace.Connections.PropertyChanged += (_, e) => leafChanges.Add(e.PropertyName ?? string.Empty);
                Assert.Equal(GraphVerbosity.Standard, host.Document.Verbosity);
                Assert.Equal(GraphVerbosity.Standard, host.Workspace.Connections.Verbosity);
                GraphTableRow row = host.Document.Publication.Rows[0];
                string standard = host.Document.RowName(row);
                ulong seq = host.Document.SeqForTests;
                int lines = host.GraphLines.Count;
                // A row line queued on the relay before the change, beside a
                // gated count: the change drops the NAVIGATION class alone.
                GraphAnnouncer relay = host.Workspace.GraphRelayForTests;
                relay.Announce(new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, host.Document.RowCopy(row)));
                relay.AnnounceGatedFilterCount(new GraphA11yEvent.GraphFilterCount(1, 2), () => true);
                Assert.Equal(2, relay.PendingForTests);

                host.Preferences.SetVerbosityCommand.Execute("terse");

                Assert.Equal(1, relay.PendingForTests);
                relay.FlushForTests();
                Assert.Equal(lines + 1, host.GraphLines.Count);
                Assert.Equal(GraphAnnouncer.RenderLabel(new GraphA11yEvent.GraphFilterCount(1, 2)), host.GraphLines[^1]);
                lines = host.GraphLines.Count;
                Assert.Equal(GraphVerbosity.Terse, host.Document.Verbosity);
                Assert.Equal(GraphVerbosity.Terse, host.Workspace.Connections.Verbosity);
                Assert.Equal([nameof(GraphDocumentViewModel.Verbosity)], documentChanges);
                Assert.Equal([nameof(ConnectionsLeafViewModel.Verbosity)], leafChanges);
                // The row copy renders at the new level — the bare label —
                // with no load and nothing spoken.
                Assert.Equal(row.Label, host.Document.RowName(row));
                Assert.NotEqual(standard, host.Document.RowName(row));
                Assert.Equal(seq, host.Document.SeqForTests);
                Assert.Equal(lines, host.GraphLines.Count);

                // A retired document forwards nothing; the leaf still does.
                GraphDocumentViewModel retired = host.Document;
                host.Workspace.CloseActiveTabCommand.Execute(null);
                host.Settle();
                Assert.True(retired.IsRetired);
                documentChanges.Clear();
                leafChanges.Clear();
                host.Preferences.SetVerbosityCommand.Execute("verbose");
                Assert.Empty(documentChanges);
                Assert.Equal([nameof(ConnectionsLeafViewModel.Verbosity)], leafChanges);
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    [Fact]
    public void TheFreshOpenReappliesTheLatestSavedFilterAfterAPreset()
    {
        string vault = CopyGraphVault("reapply");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphViewState state = host.State;
                host.OpenGraph();
                host.Workspace.GraphNavigator.SetNameQuery("hub");
                host.Settle();
                Assert.Equal("hub", host.Preferences.CurrentConfig.Filters.NameQuery);
                // A preset writes the transient query over the live document
                // (route (b)); the saved filter is untouched.
                host.Workspace.GraphNavigator.RunPreset(GraphPreset.Orphans);
                host.Settle();
                Assert.True(state.Filter.OrphansOnly);
                Assert.Equal(string.Empty, state.NameQuery);
                Assert.Equal("hub", host.Preferences.CurrentConfig.Filters.NameQuery);
                Assert.False(host.Preferences.CurrentConfig.Filters.OrphansOnly);
                // A close, then a FRESH open: the latest saved filter is
                // re-applied through the one mapper before the transition's load.
                host.Workspace.CloseActiveTabCommand.Execute(null);
                host.Settle();
                Assert.Null(host.Workspace.GraphDocument);
                host.OpenGraph();
                Assert.False(state.Filter.OrphansOnly);
                Assert.Equal("hub", state.NameQuery);
                Assert.Null(state.KindOnly);
                Assert.Equal(
                    GraphPreferencesViewModel.VisibilityQueryOf(host.Preferences.CurrentConfig.Filters),
                    host.Document.Publication.Query);
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    [Fact]
    public void NoReapplyWithTheArmSetOrOnAnActivation()
    {
        string vault = CopyGraphVault("no-reapply");
        try
        {
            new GraphConfigStore(vault).Write(SlateUniffiMethods.GraphConfigDefault() with
            {
                Filters = new GraphFilterConfig(IncludeAttachments: false, IncludeGhosts: false, OrphansOnly: false, NameQuery: "hub"),
            });
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphViewState state = host.State;
                Assert.Equal("hub", state.NameQuery);
                // With the ARM set — a preset with no graph tab — the fresh
                // open does not re-apply: the preset's write stands.
                Assert.Null(host.Workspace.GraphDocument);
                host.Workspace.GraphNavigator.RunPreset(GraphPreset.Unresolved);
                host.Settle();
                Assert.NotNull(host.Workspace.GraphDocument);
                Assert.Equal(GraphNodeKind.Ghost, state.KindOnly);
                Assert.Equal(string.Empty, state.NameQuery);
                Assert.Equal(SlateUniffiMethods.GraphPresetQuery(GraphPreset.Unresolved), host.Document.Publication.Query);
                Assert.Null(host.Workspace.GraphPresetArmForTests);

                // An activation preserves the view: a note tab opened, the
                // graph tab activated again — the overlay still stands, and
                // the document is the same one.
                GraphDocumentViewModel document = host.Document;
                host.Workspace.OpenPath("hub.md", WorkspaceOpenTarget.NewTab);
                host.Settle();
                Assert.False(host.Workspace.ActiveGroup.ActiveTab!.IsGraph);
                host.Workspace.ActiveGroup.ActiveTab = host.GraphTab;
                host.Settle();
                Assert.Same(document, host.Document);
                Assert.Equal(GraphNodeKind.Ghost, state.KindOnly);
                Assert.Equal(string.Empty, state.NameQuery);
                Assert.Equal(GraphNodeKind.Ghost, host.Document.Publication.Query.KindOnly);
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    [Fact]
    public void EachTriggerSchedulesAndAPresetDoesNot()
    {
        string vault = CopyGraphVault("triggers");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphPreferencesViewModel preferences = host.Preferences;
                Assert.False(preferences.HasPendingForTests);
                // The needle (the navigator's one writer).
                host.Workspace.GraphNavigator.SetNameQuery("hub");
                Assert.Equal(1UL, preferences.PendingGenerationForTests);
                Assert.Equal("hub", preferences.CurrentConfig.Filters.NameQuery);
                // The depth (the leaf's DepthChanged seam), clamped.
                uint depth = host.Workspace.Connections.Depth;
                host.Workspace.Connections.Deeper();
                Assert.Equal(2UL, preferences.PendingGenerationForTests);
                Assert.Equal(depth + 1, preferences.CurrentConfig.ConnectionsDepth);
                Assert.Equal(host.Workspace.Connections.Depth, preferences.CurrentConfig.ConnectionsDepth);
                // The level.
                preferences.SetVerbosityCommand.Execute("terse");
                Assert.Equal(3UL, preferences.PendingGenerationForTests);
                // The mode.
                preferences.SetMode(GraphSurfaceMode.Diagram);
                Assert.Equal(4UL, preferences.PendingGenerationForTests);
                preferences.FireTickForTests();
                Drain(preferences);
                // A bound depth is no change: nothing scheduled.
                host.Workspace.Connections.SetDepth(host.Workspace.Connections.Depth);
                Assert.False(preferences.HasPendingForTests);
                // A preset schedules nothing (Term P5), with no graph tab
                // (route (a)) and with one (route (b)).
                host.Workspace.GraphNavigator.RunPreset(GraphPreset.Orphans);
                host.Settle();
                Assert.True(host.State.Filter.OrphansOnly);
                Assert.False(preferences.HasPendingForTests);
                host.Workspace.GraphNavigator.RunPreset(GraphPreset.MostLinked);
                host.Settle();
                Assert.False(preferences.HasPendingForTests);
                Assert.Equal(4UL, GraphConfigWriter.Shared.StateForTests(vault).Counter);
            });
            WaitForTheSharedWriter(vault);
            GraphConfig written = OnDisk(vault);
            Assert.Equal("hub", written.Filters.NameQuery);
            Assert.False(written.Filters.OrphansOnly);
            Assert.Equal(GraphVerbosity.Terse, written.Verbosity);
            Assert.Equal(GraphSurfaceMode.Diagram, written.Mode);
            Assert.Equal(2u, written.ConnectionsDepth);
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    [Fact]
    public void AVerbosityChangeUnderAPresetPersistsThePrePresetFilter()
    {
        string vault = CopyGraphVault("under-preset");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphPreferencesViewModel preferences = host.Preferences;
                host.OpenGraph();
                host.Workspace.GraphNavigator.SetNameQuery("hub");
                preferences.FireTickForTests();
                Drain(preferences);
                host.Workspace.GraphNavigator.RunPreset(GraphPreset.Orphans);
                host.Settle();
                Assert.True(host.State.Filter.OrphansOnly);
                // The level changes under the transient preset: the aggregate
                // is CurrentConfig — the PRE-PRESET filter — never the live
                // view state (IGO-24; C-D7).
                preferences.SetVerbosityCommand.Execute("verbose");
                Assert.False(preferences.PendingAggregateForTests!.Filters.OrphansOnly);
                Assert.Equal("hub", preferences.PendingAggregateForTests!.Filters.NameQuery);
                preferences.FireTickForTests();
                Drain(preferences);
                GraphConfig written = OnDisk(vault);
                Assert.False(written.Filters.OrphansOnly);
                Assert.Equal("hub", written.Filters.NameQuery);
                Assert.Equal(GraphVerbosity.Verbose, written.Verbosity);
                // And a depth change under it, the same.
                host.Workspace.Connections.Deeper();
                preferences.FireTickForTests();
                Drain(preferences);
                Assert.False(OnDisk(vault).Filters.OrphansOnly);
                Assert.Equal("hub", OnDisk(vault).Filters.NameQuery);
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    [Fact]
    public void TheAggregateIsCurrentConfigAlone()
    {
        string vault = CopyGraphVault("aggregate-alone");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphPreferencesViewModel preferences = host.Preferences;
                host.Workspace.GraphNavigator.SetNameQuery("hub");
                GraphConfig before = preferences.CurrentConfig;
                // The live view state moved by hands other than a trigger: a
                // preset's overlay and backend filter.
                host.Workspace.GraphNavigator.RunPreset(GraphPreset.Unresolved);
                host.Settle();
                Assert.Equal(GraphNodeKind.Ghost, host.State.KindOnly);
                Assert.True(host.State.Filter.IncludeGhosts);
                Assert.Equal(string.Empty, host.State.NameQuery);
                preferences.SetVerbosityCommand.Execute("terse");
                GraphConfig aggregate = preferences.PendingAggregateForTests!;
                Assert.Same(preferences.CurrentConfig, aggregate);
                GraphConfigs.AssertEqual(before with { Verbosity = GraphVerbosity.Terse }, aggregate);
                Assert.Equal("hub", aggregate.Filters.NameQuery);
                Assert.Equal(before.Filters, aggregate.Filters);
                Assert.Same(before.Groups, aggregate.Groups);
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    [Fact]
    public void TheOldWorkspacesFlushPrecedesTheNewWorkspacesRead()
    {
        string vault = CopyGraphVault("flush-order");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                // The lifecycle's order (IGP-12): the old workspace disposes —
                // its shutdown flush enqueues the pending pair — before the
                // new one constructs and reads the writer's newest outstanding
                // aggregate, else the file the flush wrote.
                var first = new Host(vault);
                first.Workspace.GraphNavigator.SetNameQuery("hub");
                Assert.True(first.Preferences.HasPendingForTests);
                first.Dispose();
                using var second = new Host(vault);
                Assert.Equal("hub", second.Preferences.CurrentConfig.Filters.NameQuery);
                Assert.Equal("hub", second.State.NameQuery);
                Assert.True(second.Preferences.IsWritable);
            });
            WaitForTheSharedWriter(vault);
            Assert.Equal("hub", OnDisk(vault).Filters.NameQuery);
        }
        finally
        {
            DeleteVault(vault);
        }
    }
}
