// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using SlateWindows.Commands;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR C (#746), contracts C-1, C-3, C-6, C-7 and rule P: the graph
/// navigator's verb half and chord half through a real workspace over
/// the graph vault — the preset's two routes and one load, the admission,
/// the arm's survival across funnel calls, the restore; the needle's
/// write and its token; the Escape ladder's rungs against a presenter
/// the fact controls; the registrar's resolution with no window.
/// </summary>
public sealed class GraphNavigatorTests
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
            string root = Path.Combine(Path.GetTempPath(), $"slate-graph-nav-{label}-{Guid.NewGuid():N}");
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

    /// <summary>A workspace over a scanned session, the graph relay's
    /// rendered lines captured.</summary>
    private sealed class Host : IDisposable, ISlateCommandHost
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

        public GraphNavigator Navigator => Workspace.GraphNavigator;

        public GraphDocumentViewModel Document => Workspace.GraphDocument!;

        public WorkspaceTabViewModel GraphTab =>
            Workspace.Groups.SelectMany(g => g.Tabs).First(t => t.IsGraph);

        public void Settle()
        {
            if (Workspace.GraphDocument is { } document)
            {
                PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            }
            PumpedDispatcher.Drain();
        }

        /// <summary>Open the graph, settle it, then open a note in a NEW
        /// tab so the graph is open but not effective.</summary>
        public void GraphOpenBehindANote()
        {
            Workspace.OpenGraph();
            Settle();
            string note = Document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            Settle();
            Assert.False(Workspace.GraphTabIsEffective());
            GraphLines.Clear();
        }

        WorkspaceViewModel? ISlateCommandHost.Workspace => Workspace;

        FilesSidebarViewModel? ISlateCommandHost.FileSidebar => null;

        QuickSwitcherViewModel? ISlateCommandHost.QuickSwitcher => null;

        bool ISlateCommandHost.IsVaultOpen => true;

        /// <summary>The registrar's enumerating refresh resolves every row
        /// (C-8's availability fact): the shell verbs answer an inert command.</summary>
        private static readonly RelayCommand Inert = new(_ => { }, _ => false);

        ICommand ISlateCommandHost.OpenVaultCommand => Inert;

        ICommand ISlateCommandHost.CloseVaultCommand => Inert;

        ICommand ISlateCommandHost.ToggleSearchCommand => Inert;

        public void Dispose()
        {
            Workspace.Dispose();
            Session.Dispose();
        }
    }

    /// <summary>A presenter the fact controls: what it answers, what it was asked.</summary>
    private sealed class FakePresenter : IGraphSurfacePresenter
    {
        public int ProjectionRequests { get; private set; }

        public int FieldRequests { get; private set; }

        public bool ProjectionHasFocus { get; set; }

        public bool FilterRegionHasKeys { get; set; }

        public bool IsLive { get; set; } = true;

        public void RequestProjectionFocus() => ProjectionRequests++;

        public void FocusFilterField() => FieldRequests++;

        public bool DismissTransientRegion() => false;
    }

    private static string Render(GraphA11yEvent @event) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.Graph(@event)).Text;

    private static string Headline(GraphPreset preset, GraphPublication publication) =>
        Render(new GraphA11yEvent.GraphPreset(SlateUniffiMethods.GraphPresetOutcome(
            preset, (ulong)publication.Rows.Count, publication.Rows.Count == 0 ? null : publication.Rows[0])));

    private static string Count(GraphPublication publication) =>
        Render(new GraphA11yEvent.GraphFilterCount((uint)publication.Rows.Count, (uint)publication.Total));

    // --- Rule P: the preset's two routes, one load (contract C-3) ------------

    [Fact]
    public void APresetFromANoteTabLoadsOnceThroughTheFollowMethodWithTheHeadlineAlone()
    {
        using GraphVault vault = GraphVault.Copy("preset-note-tab");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.GraphOpenBehindANote();
            int loads = host.Workspace.GraphLoadsForTests;
            int seq = (int)host.Document.SeqForTests;

            host.Navigator.RunPreset(GraphPreset.Orphans);

            // Route (a): the follow method's one load under the arm — no
            // Opened, the query core's, the sort the default.
            Assert.True(host.Workspace.GraphTabIsEffective());
            Assert.Equal(loads + 1, host.Workspace.GraphLoadsForTests);
            Assert.Null(host.Workspace.GraphPresetArmForTests);
            GraphLoadToken token = host.Document.CurrentForTests!;
            Assert.Equal(GraphAnnouncePolicy.Preset, token.Announce);
            Assert.Equal(GraphPreset.Orphans, token.Preset);
            Assert.Equal(SlateUniffiMethods.GraphPresetQuery(GraphPreset.Orphans), token.Request.Query);
            Assert.Equal(host.Document.DefaultSort, token.Request.Sort);
            host.Settle();
            Assert.Equal(seq + 1, (int)host.Document.SeqForTests);
            Assert.Equal([Headline(GraphPreset.Orphans, host.Document.Publication)], host.GraphLines);
            Assert.Equal(1, host.Document.CrossingsForTests["graph_preset_outcome"]);
        });
    }

    [Fact]
    public void APresetFromTheEffectiveGraphLoadsOnceThroughTheRequestEntry()
    {
        using GraphVault vault = GraphVault.Copy("preset-effective");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            host.GraphLines.Clear();
            int loads = host.Workspace.GraphLoadsForTests;
            ulong seq = host.Document.SeqForTests;

            host.Navigator.RunPreset(GraphPreset.MostLinked);

            // Route (b): the follow method issued nothing (no transition); the
            // document's request entry loaded once.
            Assert.Equal(loads, host.Workspace.GraphLoadsForTests);
            Assert.Equal(seq + 1, host.Document.SeqForTests);
            Assert.Equal(GraphAnnouncePolicy.Preset, host.Document.CurrentForTests!.Announce);
            // The unconsumed arm was cleared by the boundary (Term P2): it never leaks.
            Assert.Null(host.Workspace.GraphPresetArmForTests);
            host.Settle();
            Assert.Equal([Headline(GraphPreset.MostLinked, host.Document.Publication)], host.GraphLines);
            Assert.Equal(SlateUniffiMethods.GraphPresetQuery(GraphPreset.MostLinked), host.Document.Publication.Query);
        });
    }

    [Fact]
    public void APresetFromTheGraphVisibleInTheOtherGroupOntoReadyLoadsOnceThroughTheRequestEntry()
    {
        using GraphVault vault = GraphVault.Copy("preset-visible-group");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            string note = host.Document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            host.Workspace.SplitRightCommand.Execute(null);
            WorkspaceGroupViewModel other = host.Workspace.ActiveGroup;
            host.Workspace.OpenGraph();
            host.Settle();
            host.Workspace.SelectGroupFromKeyboardFocus(other);
            Assert.True(host.Workspace.GraphTabIsVisible());
            Assert.False(host.Workspace.GraphTabIsEffective());
            host.GraphLines.Clear();
            int loads = host.Workspace.GraphLoadsForTests;
            ulong seq = host.Document.SeqForTests;

            host.Navigator.RunPreset(GraphPreset.Unresolved);

            // The BY-GROUP transition onto READY consumed nothing (Term 4 as
            // frozen); route (b) loaded once afterwards.
            Assert.True(host.Workspace.GraphTabIsEffective());
            Assert.Equal(loads, host.Workspace.GraphLoadsForTests);
            Assert.Equal(seq + 1, host.Document.SeqForTests);
            host.Settle();
            Assert.Equal([Headline(GraphPreset.Unresolved, host.Document.Publication)], host.GraphLines);
            Assert.All(host.Document.Publication.Rows, row => Assert.Equal(GraphNodeKind.Ghost, row.Kind));
        });
    }

    [Fact]
    public void APresetFromTheGraphHiddenInTheOtherGroupLoadsOnceThroughTheFollowMethod()
    {
        using GraphVault vault = GraphVault.Copy("preset-hidden-group");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            string note = host.Document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            // The graph hidden behind a note in ITS group; another group active.
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            host.Workspace.SplitRightCommand.Execute(null);
            host.Settle();
            Assert.False(host.Workspace.GraphTabIsVisible());
            host.GraphLines.Clear();
            int loads = host.Workspace.GraphLoadsForTests;
            ulong seq = host.Document.SeqForTests;

            host.Navigator.RunPreset(GraphPreset.Orphans);

            // TryFocusGlobalGraph's two assignments: the first makes the group
            // active with its own active tab (the arm survives that funnel
            // call), the second makes the graph effective BY TAB — the follow
            // method's one load under the arm.
            Assert.True(host.Workspace.GraphTabIsEffective());
            Assert.Equal(loads + 1, host.Workspace.GraphLoadsForTests);
            Assert.Equal(seq + 1, host.Document.SeqForTests);
            Assert.Equal(GraphAnnouncePolicy.Preset, host.Document.CurrentForTests!.Announce);
            host.Settle();
            Assert.Equal([Headline(GraphPreset.Orphans, host.Document.Publication)], host.GraphLines);
        });
    }

    [Fact]
    public void APresetWithNoGraphTabOpensIt()
    {
        using GraphVault vault = GraphVault.Copy("preset-no-tab");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            Assert.Null(host.Workspace.GraphDocument);

            host.Navigator.RunPreset(GraphPreset.Orphans);

            Assert.NotNull(host.Workspace.GraphDocument);
            Assert.True(host.Workspace.GraphTabIsEffective());
            Assert.Equal(1, host.Workspace.GraphLoadsForTests);
            Assert.Equal(GraphAnnouncePolicy.Preset, host.Document.CurrentForTests!.Announce);
            host.Settle();
            // No Opened (Term P2): the headline alone.
            Assert.Equal([Headline(GraphPreset.Orphans, host.Document.Publication)], host.GraphLines);
            Assert.Equal(GraphActivationCause.Activation, host.Workspace.GraphCauseForTests);
        });
    }

    [Fact]
    public void ARefusedAdmissionWritesNothing()
    {
        using GraphVault vault = GraphVault.Copy("preset-refused");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.GraphOpenAdmissionReason = () => "a property edit is navigating";
            GraphViewState state = host.Workspace.GraphViewStateForTests;

            host.Navigator.RunPreset(GraphPreset.Orphans);

            Assert.Null(host.Workspace.GraphDocument);
            Assert.Equal(GraphViewState.DefaultFilter(), state.Filter);
            Assert.Null(state.KindOnly);
            Assert.Null(host.Workspace.GraphPresetArmForTests);
            Assert.Empty(host.GraphLines);
        });
    }

    [Fact]
    public void AnOpenThatNeverMadeTheGraphEffectiveRestoresTheQueryAndLoadsNothing()
    {
        using GraphVault vault = GraphVault.Copy("preset-restore");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.GraphOpenBehindANote();
            GraphViewState state = host.Workspace.GraphViewStateForTests;
            state.NameQuery = "hub";
            int loads = host.Workspace.GraphLoadsForTests;
            ulong seq = host.Document.SeqForTests;
            // The funnel that never makes the graph effective: the open's
            // target refused. Modelled by retiring the seated document's
            // route — an unseated document refuses the request entry — and
            // a graph the mutation cannot make effective is the funnel's
            // report; here the seam refuses AFTER the write.
            var navigator = new GraphNavigator(
                state,
                host.Workspace.GraphPreferences,
                () => host.Workspace.GraphDocument,
                () => null,
                _ => new GraphPresetOpenReport(ArmConsumed: false, GraphEffective: false));

            navigator.RunPreset(GraphPreset.Orphans);

            // The query written at (ii) is restored; nothing loaded.
            Assert.Equal("hub", state.NameQuery);
            Assert.Equal(GraphViewState.DefaultFilter(), state.Filter);
            Assert.Null(state.KindOnly);
            Assert.Equal(loads, host.Workspace.GraphLoadsForTests);
            Assert.Equal(seq, host.Document.SeqForTests);
        });
    }

    [Fact]
    public void TheArmSurvivesTheFunnelCallsThatSeeAnotherTab()
    {
        using GraphVault vault = GraphVault.Copy("preset-arm");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            string note = host.Document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            host.Workspace.SplitRightCommand.Execute(null);
            host.Settle();
            Assert.False(host.Workspace.GraphTabIsVisible());
            // Observe the arm at every funnel call of the preset's mutation:
            // the first assignment (the group made active with its own tab)
            // sees the arm standing; the second consumes it.
            var armAtCalls = new List<GraphPreset?>();
            host.Workspace.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WorkspaceViewModel.ActiveGroup))
                {
                    armAtCalls.Add(host.Workspace.GraphPresetArmForTests);
                }
            };

            host.Navigator.RunPreset(GraphPreset.Orphans);

            Assert.Contains(GraphPreset.Orphans, armAtCalls);
            Assert.Null(host.Workspace.GraphPresetArmForTests);
            Assert.Equal(GraphAnnouncePolicy.Preset, host.Document.CurrentForTests!.Announce);
            host.Settle();
        });
    }

    // --- The needle (contract C-6) ----------------------------------------

    [Fact]
    public void ANeedleEqualToTheCurrentIssuesNothing()
    {
        using GraphVault vault = GraphVault.Copy("needle-equal");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            ulong seq = host.Document.SeqForTests;
            host.Navigator.SetNameQuery("hub");
            Assert.Equal(seq + 1, host.Document.SeqForTests);
            host.Settle();
            host.Navigator.SetNameQuery("hub");
            Assert.Equal(seq + 1, host.Document.SeqForTests);
            host.Navigator.ClearNameQuery();
            Assert.Equal(seq + 2, host.Document.SeqForTests);
            host.Settle();
            host.Navigator.ClearNameQuery();
            Assert.Equal(seq + 2, host.Document.SeqForTests);
        });
    }

    [Fact]
    public void SetNameQueryWithNoDocumentWritesTheStateAndIssuesNothing()
    {
        using GraphVault vault = GraphVault.Copy("needle-no-document");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            Assert.Null(host.Workspace.GraphDocument);
            host.Navigator.SetNameQuery("hub");
            Assert.Equal("hub", host.Workspace.GraphViewStateForTests.NameQuery);
            Assert.Null(host.Workspace.GraphDocument);
            Assert.Empty(host.GraphLines);
        });
    }

    [Fact]
    public void SetNameQueryOnARetiredDocumentWritesTheStateAndIssuesNothing()
    {
        using GraphVault vault = GraphVault.Copy("needle-retired");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphDocumentViewModel document = host.Document;
            host.Workspace.CloseActiveTabCommand.Execute(null);
            Assert.True(document.IsRetired);
            ulong seq = document.SeqForTests;
            host.Navigator.SetNameQuery("hub");
            Assert.Equal("hub", host.Workspace.GraphViewStateForTests.NameQuery);
            Assert.Equal(seq, document.SeqForTests);
        });
    }

    // --- The chord half and the Escape ladder (contracts C-1, C-7) ---------

    [Fact]
    public void TheChordHalfConsumesExactlyTheTwoChordsAndNothingElse()
    {
        using GraphVault vault = GraphVault.Copy("chords");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            var presenter = new FakePresenter { FilterRegionHasKeys = true };
            // Escape from the filter region: consumed (rung 2). Every other
            // key, and Escape with a modifier, falls through.
            Assert.True(host.Navigator.HandleKey(Key.Escape, ModifierKeys.None, presenter));
            Assert.False(host.Navigator.HandleKey(Key.Escape, ModifierKeys.Control, presenter));
            Assert.False(host.Navigator.HandleKey(Key.Escape, ModifierKeys.Shift, presenter));
            Assert.False(host.Navigator.HandleKey(Key.F, ModifierKeys.Control, presenter));
            Assert.False(host.Navigator.HandleKey(Key.Down, ModifierKeys.None, presenter));
            Assert.False(host.Navigator.HandleKey(Key.Enter, ModifierKeys.None, presenter));
            Assert.Same(presenter, host.Navigator.PresenterForTests);
        });
    }

    [Fact]
    public void EachRungWithTheArrangementThatReachesItAndTheOneThatFallsThrough()
    {
        using GraphVault vault = GraphVault.Copy("rungs");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphViewState state = host.Workspace.GraphViewStateForTests;
            var presenter = new FakePresenter();

            // Rung 1: a raw needle — cleared through the navigator, the
            // landing requested; consumed.
            host.Navigator.SetNameQuery("hub");
            host.Settle();
            ulong seq = host.Document.SeqForTests;
            Assert.True(host.Navigator.HandleKey(Key.Escape, ModifierKeys.None, presenter));
            Assert.Equal(string.Empty, state.NameQuery);
            Assert.Equal(seq + 1, host.Document.SeqForTests);
            Assert.Equal(1, presenter.ProjectionRequests);
            host.Settle();

            // Rung 2: the filter region holds the keys with no needle — the
            // landing requested with no token; consumed.
            presenter.FilterRegionHasKeys = true;
            seq = host.Document.SeqForTests;
            Assert.True(host.Navigator.HandleKey(Key.Escape, ModifierKeys.None, presenter));
            Assert.Equal(seq, host.Document.SeqForTests);
            Assert.Equal(2, presenter.ProjectionRequests);

            // Rung 3: nothing to do — not consumed; the press bubbles.
            presenter.FilterRegionHasKeys = false;
            presenter.ProjectionHasFocus = true;
            Assert.False(host.Navigator.HandleKey(Key.Escape, ModifierKeys.None, presenter));
            Assert.Equal(2, presenter.ProjectionRequests);
        });
    }

    [Fact]
    public void ThePressIsConsumedExactlyOnce()
    {
        using GraphVault vault = GraphVault.Copy("rung-once");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            // A needle AND the keys in the field: rung 1 alone answers — one
            // clear, one landing request, one consumption.
            var presenter = new FakePresenter { FilterRegionHasKeys = true };
            host.Navigator.SetNameQuery("hub");
            host.Settle();
            ulong seq = host.Document.SeqForTests;
            Assert.True(host.Navigator.HandleKey(Key.Escape, ModifierKeys.None, presenter));
            Assert.Equal(seq + 1, host.Document.SeqForTests);
            Assert.Equal(1, presenter.ProjectionRequests);
            host.Settle();
        });
    }

    [Fact]
    public void AVerbOnAStalePresenterMovesNothing()
    {
        using GraphVault vault = GraphVault.Copy("stale-presenter");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            var presenter = new FakePresenter { IsLive = false, FilterRegionHasKeys = true };
            // Every verb that moves focus asks IsLive first: a stale
            // presenter is asked for nothing, and the press bubbles.
            Assert.False(host.Navigator.HandleKey(Key.Escape, ModifierKeys.None, presenter));
            Assert.Equal(0, presenter.ProjectionRequests);
            host.Navigator.FocusFilterField();
            Assert.Equal(0, presenter.FieldRequests);
            // A detach of another presenter leaves this one attached.
            host.Navigator.DetachPresenter(new FakePresenter());
            Assert.Same(presenter, host.Navigator.PresenterForTests);
            host.Navigator.DetachPresenter(presenter);
            Assert.Null(host.Navigator.PresenterForTests);
        });
    }

    /// <summary>C-1's map, read off the NAVIGATOR (IPG-6): Bind registered
    /// exactly the scope's two chords, each once. The fact this replaces
    /// asserted that `Dictionary.Add` throws on a dictionary of its own,
    /// which is true of .NET and says nothing about this type; the
    /// registration's shape — one writer, the throwing Add — is the
    /// census's (TheChordMapHasOneWriterAndAThrowingAdd).</summary>
    [Fact]
    public void TheNavigatorRegistersTheScopesChordsOnceEach()
    {
        using GraphVault vault = GraphVault.Copy("chord-map");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            (Key Key, ModifierKeys Modifiers)[] chords = [.. host.Navigator.ChordsForTests];
            Assert.Equal(chords.Length, chords.Distinct().Count());
            // Ordered by BOTH components: two rows may one day share a Key
            // with different modifiers, and a comparison keyed on Key alone
            // would then rest on OrderBy's stability rather than on the set.
            Assert.Equal(
                [
                    (Key.Escape, ModifierKeys.None),
                    (Key.I, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift),
                ],
                chords.OrderBy(c => c.Key).ThenBy(c => c.Modifiers).ToArray());
        });
    }

    // --- The registrar (rule R-E; contract C-3) ------------------------------

    [Fact]
    public void EveryVerbResolvesThroughTheRegistrarWithNoWindow()
    {
        using GraphVault vault = GraphVault.Copy("registrar");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            foreach ((string id, GraphPreset preset) in new[]
            {
                (ChordTable.Ids.GraphOrphans, GraphPreset.Orphans),
                (ChordTable.Ids.GraphUnresolved, GraphPreset.Unresolved),
                (ChordTable.Ids.GraphMostLinked, GraphPreset.MostLinked),
            })
            {
                ICommand? command = SlateCommandRegistrar.Resolve(host, id);
                Assert.NotNull(command);
                Assert.True(command.CanExecute(null), $"{id} is always enabled");
                ChordTableEntry row = ChordTable.Entries.Single(r => r.Id == id);
                Assert.Equal(CommandSection.Graph, row.Section);
                Assert.Equal(ChordScope.None, row.Scope);
                Assert.Null(row.WindowsChord);

                host.GraphLines.Clear();
                command.Execute(null);
                host.Settle();
                Assert.Equal(SlateUniffiMethods.GraphPresetQuery(preset), host.Document.Publication.Query);
                Assert.Equal([Headline(preset, host.Document.Publication)], host.GraphLines);
            }
            Assert.Equal(3, host.Document.CrossingsForTests["graph_preset_outcome"]);
        });
    }

    [Fact]
    public void ANeedleTypedAfterAPresetSpeaksTheCountThroughTheNavigator()
    {
        using GraphVault vault = GraphVault.Copy("needle-after-preset");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Navigator.RunPreset(GraphPreset.Unresolved);
            host.Settle();
            host.GraphLines.Clear();
            host.Navigator.SetNameQuery("g");
            host.Settle();
            host.Document.AnnouncerForTests.FlushForTests();
            Assert.Equal(GraphNodeKind.Ghost, host.Document.Publication.Query.KindOnly);
            Assert.Equal([Count(host.Document.Publication)], host.GraphLines);
        });
    }

    // --- Where-am-I (contract C-8) -------------------------------------------

    private static string WhereAmIRender(GraphA11yEvent.GraphWhereAmI @event) => Render(@event);

    /// <summary>C-8: the row — the mac's label, hint and chord, ChordScope.Graph,
    /// the Shift disambiguation, in the Graph section — and the shared chord's
    /// disposition beside the canvas row (C-11).</summary>
    [Fact]
    public void TheRowItsScopeItsDivergenceAndTheSharedChordDisposition()
    {
        ChordTableEntry row = ChordTable.Entries.Single(r => r.Id == ChordTable.Ids.GraphWhereAmI);
        Assert.Equal("slate.graph.whereAmI", row.Id);
        Assert.Equal("Graph: Where Am I?", row.Label);
        Assert.Equal(CommandSection.Graph, row.Section);
        Assert.Equal("⌃⌘I", row.MacChord);
        Assert.Equal("Ctrl+Alt+Shift+I", row.WindowsChord);
        Assert.Equal(ChordScope.Graph, row.Scope);
        Assert.True(row.IsRegistered);
        Assert.Contains("Shift", row.Divergence, StringComparison.Ordinal);
        ChordTableEntry canvas = ChordTable.Entries.Single(r => r.Id == ChordTable.Ids.CanvasWhereAmI);
        Assert.Equal(canvas.WindowsChord, row.WindowsChord);
        Assert.Equal(canvas.Divergence, row.Divergence);
        Assert.NotEqual(canvas.Scope, row.Scope);
    }

    [Fact]
    public void WhereAmIIsRefusedWithNoSeatedDocumentAndWithANullReturningSeam()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-refused");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            int raised = 0;
            host.Navigator.WhereAmIAvailabilityChanged += () => raised++;
            // No graph tab, no seam: refused, nothing rendered, nothing posted.
            Assert.False(host.Navigator.CanWhereAmI);
            Assert.False(host.Navigator.WhereAmI());
            Assert.Null(host.Navigator.WhereAmIText);
            Assert.Empty(host.GraphLines);
            ICommand? command = SlateCommandRegistrar.Resolve(host, ChordTable.Ids.GraphWhereAmI);
            Assert.NotNull(command);
            Assert.False(command.CanExecute(null));
            Assert.Equal(SlateCommandRegistrar.UnavailableReason, SlateCommandRegistrar.DisabledReason(host, ChordTable.Ids.GraphWhereAmI));
            // A seam that answers null — the table's under a load in flight —
            // refuses the same way; the chord falls through (unconsumed).
            host.Workspace.OpenGraph();
            Assert.True(host.Document.IsRequestInFlight);
            Assert.False(host.Navigator.CanWhereAmI);
            Assert.False(host.Navigator.WhereAmI());
            var presenter = new FakePresenter();
            Assert.False(host.Navigator.HandleKey(Key.I, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, presenter));
            Assert.Null(host.Navigator.WhereAmIText);
            Assert.True(raised >= 1, "the seat and the issue raised the availability");
        });
    }

    [Fact]
    public void InstallingTheDiagramsSeamRaisesAvailabilityAndTheRowEnables()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-diagram-seam");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ICommand command = SlateCommandRegistrar.Resolve(host, ChordTable.Ids.GraphWhereAmI)!;
            int canExecuteChanged = 0;
            command.CanExecuteChanged += (_, _) => canExecuteChanged++;
            int raised = 0;
            host.Navigator.WhereAmIAvailabilityChanged += () => raised++;
            host.Workspace.OpenGraph();
            host.Settle();
            Assert.True(command.CanExecute(null));
            // Diagram mode with no diagram seam (PR D's): the verb is refused
            // even though the table's seam would answer.
            host.Workspace.GraphViewStateForTests.Mode = GraphSurfaceMode.Diagram;
            Assert.False(host.Navigator.CanWhereAmI);
            int before = canExecuteChanged;
            var witness = new GraphA11yEvent.GraphWhereAmI(
                new GraphWhereAmISelection.NoSelection(), 100, new GraphWhereAmIFilter.UnresolvedOnly(), null);
            host.Navigator.InstallDiagramReadback(() => witness);
            Assert.Equal(before + 1, canExecuteChanged);
            Assert.True(raised >= 1);
            Assert.True(command.CanExecute(null));
            Assert.True(host.Navigator.WhereAmI());
            Assert.Equal(WhereAmIRender(witness), host.Navigator.WhereAmIText);
            host.Navigator.InstallDiagramReadback(null);
            Assert.False(command.CanExecute(null));
            host.Workspace.GraphViewStateForTests.Mode = GraphSurfaceMode.Table;
            Assert.True(command.CanExecute(null));
        });
    }

    [Fact]
    public void TheTableReadbackNamesTheSharedKeysSnapshotNodeWithNoZoomClause()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-node");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphTableRow row = host.Document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note && r.LinksIn > 0);
            Assert.True(host.Document.SelectRow(row.StableKey));
            host.GraphLines.Clear();
            GraphA11yEvent.GraphWhereAmI? @event = host.Document.TableWhereAmI();
            Assert.NotNull(@event);
            GraphNode node = host.Document.Publication.Snapshot!.Nodes.Single(n => n.StableKey == row.StableKey);
            Assert.Equal(
                new GraphWhereAmISelection.Node(
                    new GraphRowCopy(node.Label, node.Kind, node.InLinks, node.OutLinks, node.InLinks, false), node.Component),
                @event.Selection);
            Assert.Null(@event.ZoomPercent);
            Assert.Equal(new GraphWhereAmIFilter.Normal(false, false, true), @event.Filter);
            Assert.Equal(string.Empty, @event.NameFilter);
            // The verb: the panel's text and ONE post render the one event.
            Assert.True(host.Navigator.WhereAmI());
            Assert.Equal(WhereAmIRender(@event), host.Navigator.WhereAmIText);
            Assert.Equal([WhereAmIRender(@event)], host.GraphLines);
            Assert.DoesNotContain("zoom", host.Navigator.WhereAmIText, StringComparison.Ordinal);
            Assert.Contains("component", host.Navigator.WhereAmIText, StringComparison.Ordinal);
            // The needle rides along as the name filter, trimmed by core.
            host.Navigator.SetNameQuery("  " + row.Label + "  ");
            host.Settle();
            Assert.Equal("  " + row.Label + "  ", host.Document.TableWhereAmI()!.NameFilter);
            host.GraphLines.Clear();
            Assert.True(host.Navigator.WhereAmI());
            Assert.Contains(row.Label, host.GraphLines.Single(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TheTableReadbackReadsNoSelectionWithoutAKey()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-no-key");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            host.Workspace.GraphViewStateForTests.SelectedKey = null;
            GraphA11yEvent.GraphWhereAmI? @event = host.Document.TableWhereAmI();
            Assert.NotNull(@event);
            Assert.Equal(new GraphWhereAmISelection.NoSelection(), @event.Selection);
            Assert.Null(@event.ZoomPercent);
            // A key absent from the snapshot reads the same.
            host.Workspace.GraphViewStateForTests.SelectedKey = "p:nowhere.md";
            Assert.Equal(new GraphWhereAmISelection.NoSelection(), host.Document.TableWhereAmI()!.Selection);
            Assert.True(host.Navigator.WhereAmI());
            Assert.StartsWith("No node selected", host.Navigator.WhereAmIText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TheTableReadbackReadsUnresolvedOnlyUnderTheKindOverlay()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-unresolved");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Navigator.RunPreset(GraphPreset.Unresolved);
            host.Settle();
            Assert.Equal(GraphNodeKind.Ghost, host.Document.Publication.Query.KindOnly);
            GraphA11yEvent.GraphWhereAmI? @event = host.Document.TableWhereAmI();
            Assert.NotNull(@event);
            Assert.Equal(new GraphWhereAmIFilter.UnresolvedOnly(), @event.Filter);
            host.GraphLines.Clear();
            Assert.True(host.Navigator.WhereAmI());
            Assert.Contains("unresolved", host.GraphLines.Single(), StringComparison.Ordinal);
            // The orphans preset: Normal with its backend flags.
            host.Navigator.RunPreset(GraphPreset.Orphans);
            host.Settle();
            GraphFilter backend = host.Workspace.GraphViewStateForTests.Filter;
            Assert.Equal(
                new GraphWhereAmIFilter.Normal(backend.OrphansOnly, backend.IncludeAttachments, backend.IncludeGhosts),
                host.Document.TableWhereAmI()!.Filter);
            Assert.True(backend.OrphansOnly);
        });
    }

    /// <summary>C-8 as TGC-1 records it (IPG-3): the readback resolves the
    /// shared key among the SHOWN rows, never the snapshot's nodes. A-7 keeps
    /// the key when an overlay hides its row, so the snapshot still holds the
    /// node — and the reader was told they were on a row the table does not
    /// show, under filter prose that made it unreachable.</summary>
    /// <summary>Rule P, Term P3 (IPG-14): the admission seam is the one
    /// "which OpenGraph() reads too". The preset funnel asked it and the
    /// plain open did not, so a refused navigation still opened the tab and
    /// started its load.</summary>
    [Fact]
    public void TheRefusedAdmissionStopsThePlainOpenAsWellAsThePreset()
    {
        using GraphVault vault = GraphVault.Copy("open-admission");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.GraphOpenAdmissionReason = () => "navigation unavailable";
            host.Workspace.OpenGraph();
            host.Settle();
            Assert.Null(host.Workspace.GraphDocument);
            Assert.DoesNotContain(host.Workspace.Groups.SelectMany(g => g.Tabs), tab => tab.IsGraph);
            Assert.Empty(host.GraphLines);
            // Admitted again, the same command opens.
            host.Workspace.GraphOpenAdmissionReason = null;
            host.Workspace.OpenGraph();
            host.Settle();
            Assert.NotNull(host.Workspace.GraphDocument);
        });
    }

    [Fact]
    public void TheTableReadbackReadsNoSelectionWhenAnOverlayHidesTheSelectedRow()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-hidden");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            GraphTableRow note = host.Document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note);
            Assert.True(host.Document.SelectRow(note.StableKey));
            Assert.IsType<GraphWhereAmISelection.Node>(host.Document.TableWhereAmI()!.Selection);

            // (a) the NEEDLE hides it: a needle no label carries.
            host.Navigator.SetNameQuery("zzz-no-label-carries-this");
            host.Settle();
            Assert.DoesNotContain(host.Document.Publication.Rows, r => r.StableKey == note.StableKey);
            // The key SURVIVES (A-7: the snapshot still holds the node) —
            // the readback answers on the rows anyway.
            Assert.Equal(note.StableKey, host.Workspace.GraphViewStateForTests.SelectedKey);
            Assert.NotNull(host.Document.Publication.Snapshot);
            Assert.Contains(host.Document.Publication.Snapshot!.Nodes, n => n.StableKey == note.StableKey);
            Assert.Equal(new GraphWhereAmISelection.NoSelection(), host.Document.TableWhereAmI()!.Selection);
            host.GraphLines.Clear();
            Assert.True(host.Navigator.WhereAmI());
            Assert.StartsWith("No node selected", host.GraphLines.Single(), StringComparison.Ordinal);

            // (b) the KIND overlay hides it: the unresolved preset over a note.
            host.Navigator.ClearNameQuery();
            host.Settle();
            Assert.IsType<GraphWhereAmISelection.Node>(host.Document.TableWhereAmI()!.Selection);
            host.Navigator.RunPreset(GraphPreset.Unresolved);
            host.Settle();
            Assert.Equal(note.StableKey, host.Workspace.GraphViewStateForTests.SelectedKey);
            Assert.DoesNotContain(host.Document.Publication.Rows, r => r.StableKey == note.StableKey);
            Assert.Equal(new GraphWhereAmISelection.NoSelection(), host.Document.TableWhereAmI()!.Selection);
        });
    }

    [Fact]
    public void TheTableReadbackReadsASelectedGhostUnderTheUnresolvedPreset()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-ghost");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            host.Navigator.RunPreset(GraphPreset.Unresolved);
            host.Settle();
            GraphPublication publication = host.Document.Publication;
            Assert.Equal(GraphLoadState.Ready, publication.State);
            Assert.NotEmpty(publication.Rows);
            // The grid's current row under the preset is a ghost; the
            // document's guarded selection admits it (the snapshot under the
            // preset's backend filter holds the ghost node), and the readback
            // reads it as a Node with its component — never "No node selected".
            GraphTableRow ghost = publication.Rows[0];
            Assert.Equal(GraphNodeKind.Ghost, ghost.Kind);
            Assert.True(host.Document.SelectRow(ghost.StableKey));
            GraphA11yEvent.GraphWhereAmI? @event = host.Document.TableWhereAmI();
            Assert.NotNull(@event);
            var node = Assert.IsType<GraphWhereAmISelection.Node>(@event.Selection);
            Assert.Equal(GraphNodeKind.Ghost, node.Row.Kind);
            Assert.Equal(ghost.Label, node.Row.Label);
            host.GraphLines.Clear();
            Assert.True(host.Navigator.WhereAmI());
            Assert.Contains("component", host.GraphLines.Single(), StringComparison.Ordinal);
            Assert.StartsWith(host.Navigator.WhereAmIText, host.GraphLines.Single(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TheTableReadbackIsRefusedWhileARequestIsInFlightAndAnswersAtInstall()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-in-flight");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            ICommand command = SlateCommandRegistrar.Resolve(host, ChordTable.Ids.GraphWhereAmI)!;
            int canExecuteChanged = 0;
            command.CanExecuteChanged += (_, _) => canExecuteChanged++;
            Assert.NotNull(host.Document.TableWhereAmI());
            // A needle in flight: refused — an old node is never read under new
            // filter prose (IGO-29) — and the command re-evaluated at the issue.
            host.Navigator.SetNameQuery("hub");
            Assert.True(host.Document.IsRequestInFlight);
            Assert.Null(host.Document.TableWhereAmI());
            Assert.False(command.CanExecute(null));
            Assert.Equal(1, canExecuteChanged);
            Assert.False(host.Navigator.WhereAmI());
            // The install: answers again, with the new needle, re-evaluated once more.
            host.Settle();
            Assert.False(host.Document.IsRequestInFlight);
            Assert.Equal("hub", host.Document.TableWhereAmI()!.NameFilter);
            Assert.True(command.CanExecute(null));
            Assert.Equal(2, canExecuteChanged);
            // A stale publication under a changed view state is not current
            // either: the state written by hand with no request issued.
            host.Workspace.GraphViewStateForTests.NameQuery = "elsewhere";
            Assert.Null(host.Document.TableWhereAmI());
            host.Workspace.GraphViewStateForTests.NameQuery = "hub";
            Assert.NotNull(host.Document.TableWhereAmI());
            // A SORT in flight keeps the query: only quiescence refuses (Term Q7's
            // "nothing in flight" arm on its own) — the rows land, it answers.
            using var reached = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            host.Document.FetchGateForTests = () =>
            {
                reached.Set();
                release.Wait(TimeSpan.FromSeconds(10));
            };
            Assert.True(host.Document.Request(new GraphRequest.Sort(new GraphTableSort(GraphTableColumn.Note, true))));
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(host.Document.IsRequestInFlight);
            Assert.Null(host.Document.TableWhereAmI());
            Assert.False(command.CanExecute(null));
            release.Set();
            host.Settle();
            Assert.NotNull(host.Document.TableWhereAmI());
            Assert.True(command.CanExecute(null));
        });
    }

    [Fact]
    public void WhereAmIWithNoGraphTabIsRefused()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-no-tab");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ICommand command = SlateCommandRegistrar.Resolve(host, ChordTable.Ids.GraphWhereAmI)!;
            Assert.False(command.CanExecute(null));
            command.Execute(null);
            Assert.Null(host.Navigator.WhereAmIText);
            Assert.Empty(host.GraphLines);
            Assert.Null(host.Workspace.GraphDocument);
            // After a close, the retired document's seam is gone: refused again.
            host.Workspace.OpenGraph();
            host.Settle();
            Assert.True(command.CanExecute(null));
            host.Workspace.CloseActiveTabCommand.Execute(null);
            host.Settle();
            Assert.False(command.CanExecute(null));
            Assert.False(host.Navigator.WhereAmI());
        });
    }

    [Fact]
    public void WhereAmIIsUnavailableWhileTheGraphIsBehindANote()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-hidden-tab");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ICommand command = SlateCommandRegistrar.Resolve(host, ChordTable.Ids.GraphWhereAmI)!;
            var availability = new List<bool>();
            command.CanExecuteChanged += (_, _) => availability.Add(command.CanExecute(null));
            host.GraphOpenBehindANote();

            Assert.NotNull(host.Document.TableWhereAmI());
            Assert.False(command.CanExecute(null));
            Assert.False(availability[^1]);
            command.Execute(null);
            Assert.False(host.Navigator.WhereAmI());
            Assert.Null(host.Navigator.WhereAmIText);
            Assert.Empty(host.GraphLines);

            host.Workspace.ActiveGroup.ActiveTab = host.GraphTab;
            host.Settle();
            Assert.True(command.CanExecute(null));
            Assert.True(availability[^1]);
            Assert.True(host.Navigator.WhereAmI());
            Assert.NotNull(host.Navigator.WhereAmIText);
        });
    }

    [Fact]
    public void WhereAmIAvailabilityFollowsTheActivePaneWithoutReloading()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-inactive-pane");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.GraphOpenBehindANote();
            host.Workspace.SplitRightCommand.Execute(null);
            WorkspaceGroupViewModel other = host.Workspace.ActiveGroup;
            host.Workspace.OpenGraph();
            host.Settle();
            WorkspaceGroupViewModel graphGroup = host.Workspace.ActiveGroup;
            ICommand command = SlateCommandRegistrar.Resolve(host, ChordTable.Ids.GraphWhereAmI)!;
            var availability = new List<bool>();
            command.CanExecuteChanged += (_, _) => availability.Add(command.CanExecute(null));
            GraphPublication held = host.Document.Publication;
            ulong seq = host.Document.SeqForTests;
            host.GraphLines.Clear();

            host.Workspace.SelectGroupFromKeyboardFocus(other);
            Assert.True(host.Workspace.GraphTabIsVisible());
            Assert.False(command.CanExecute(null));
            Assert.False(availability[^1]);
            command.Execute(null);
            Assert.False(host.Navigator.WhereAmI());
            Assert.Null(host.Navigator.WhereAmIText);
            Assert.Empty(host.GraphLines);

            host.Workspace.SelectGroupFromKeyboardFocus(graphGroup);
            Assert.True(command.CanExecute(null));
            Assert.True(availability[^1]);
            Assert.Same(held, host.Document.Publication);
            Assert.Equal(seq, host.Document.SeqForTests);
            Assert.False(host.Document.IsRequestInFlight);
            Assert.True(host.Navigator.WhereAmI());
            Assert.NotNull(host.Navigator.WhereAmIText);
            Assert.Single(host.GraphLines);
        });
    }

    [Fact]
    public void TheAvailabilitySeamRaisesCanExecuteChangedAndTheRegistrarsRefresh()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-availability");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ICommand command = SlateCommandRegistrar.Resolve(host, ChordTable.Ids.GraphWhereAmI)!;
            Assert.Same(command, SlateCommandRegistrar.Resolve(host, ChordTable.Ids.GraphWhereAmI));
            var edges = new List<bool>();
            command.CanExecuteChanged += (_, _) => edges.Add(command.CanExecute(null));
            // The seat (false: in flight), the install (true).
            host.Workspace.OpenGraph();
            host.Settle();
            Assert.Contains(true, edges);
            Assert.True(edges[^1]);
            int count = edges.Count;
            // The registrar's enumerating refresh reaches this command too.
            SlateCommandRegistrar.RaiseCommandStates(host);
            Assert.Equal(count + 1, edges.Count);
            // The retirement: false.
            host.Workspace.CloseActiveTabCommand.Execute(null);
            host.Settle();
            Assert.False(edges[^1]);
        });
    }


    /// <summary>C-8: the panel's text is cleared with the pane that showed it —
    /// a detach of THAT presenter; a detach of another leaves it.</summary>
    [Fact]
    public void ADetachClearsThePanelsTextForThePaneThatShowedIt()
    {
        using GraphVault vault = GraphVault.Copy("where-am-i-detach");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.OpenGraph();
            host.Settle();
            var presenter = new FakePresenter();
            var other = new FakePresenter();
            Assert.True(host.Navigator.HandleKey(Key.I, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, presenter));
            Assert.NotNull(host.Navigator.WhereAmIText);
            host.Navigator.DetachPresenter(other);
            Assert.NotNull(host.Navigator.WhereAmIText);
            host.Navigator.DetachPresenter(presenter);
            Assert.Null(host.Navigator.WhereAmIText);
        });
    }

}
