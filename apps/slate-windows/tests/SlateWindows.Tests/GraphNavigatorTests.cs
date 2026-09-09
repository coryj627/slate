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

        ICommand ISlateCommandHost.OpenVaultCommand => throw new NotSupportedException();

        ICommand ISlateCommandHost.CloseVaultCommand => throw new NotSupportedException();

        ICommand ISlateCommandHost.ToggleSearchCommand => throw new NotSupportedException();

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

    [Fact]
    public void ADuplicateChordRegistrationThrows()
    {
        // The map's Add throws on a duplicate key (C-1's wall): the
        // registration is a dictionary whose Add is the only writer.
        var chords = new Dictionary<(Key Key, ModifierKeys Modifiers), Func<bool>>();
        chords.Add((Key.Escape, ModifierKeys.None), () => true);
        Assert.Throws<ArgumentException>(() => chords.Add((Key.Escape, ModifierKeys.None), () => false));
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
}
