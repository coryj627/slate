// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR E (#746), contract E-2; rule I Terms I6, I7; rule X Terms X1,
/// X3, X4; rule Y Terms Y1–Y6; rule Z Terms Z1, Z4; rule K Term K3: the
/// inspector's view model over a real workspace — the reads through to
/// the two sources and the notifications outside writes earn, the filter
/// route through the document, the needle through the navigator, the
/// groups' list to both sources in core's styles, the display's trigger,
/// the changed control's table, the effectiveness gate's three states and
/// the read-only state.
/// </summary>
public sealed class GraphInspectorTests
{
    /// <summary>The graph vault of 0b-13, copied into a temp root.</summary>
    private static string CopyGraphVault(string label)
    {
        string source = Path.Combine(SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures", "graph_vault");
        string root = Path.Combine(Path.GetTempPath(), $"slate-graph-inspector-{label}-{Guid.NewGuid():N}");
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

        public GraphInspectorViewModel Inspector => Workspace.Inspector;

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

        public void FlushLines()
        {
            if (Workspace.GraphDocument is { } document)
            {
                document.AnnouncerForTests.FlushForTests();
            }
        }

        public void Dispose()
        {
            Workspace.Dispose();
            Session.Dispose();
        }
    }

    private static string[] DisplayNames =>
    [
        nameof(GraphInspectorViewModel.Arrows),
        nameof(GraphInspectorViewModel.TextFadeZoom),
        nameof(GraphInspectorViewModel.NodeSizeMultiplier),
        nameof(GraphInspectorViewModel.LinkThickness),
    ];

    private static string[] ForceNames =>
    [
        nameof(GraphInspectorViewModel.Center),
        nameof(GraphInspectorViewModel.Repel),
        nameof(GraphInspectorViewModel.Link),
        nameof(GraphInspectorViewModel.LinkDistance),
    ];

    private static void AssertSameGroups(Host host)
    {
        Assert.Equal(host.State.Groups, host.Preferences.CurrentConfig.Groups);
        Assert.Equal(host.State.Groups.Count, host.Inspector.Groups.Count);
        for (int i = 0; i < host.State.Groups.Count; i++)
        {
            GraphInspectorGroupRow row = host.Inspector.Groups[i];
            Assert.Equal(i + 1, row.Index);
            Assert.Equal(host.State.Groups[i].Query, row.Query);
            Assert.Equal(host.State.Groups[i].ColorToken, row.ColorToken);
            Assert.Equal(host.State.Groups[i].RingStyle, row.RingStyle);
        }
        Assert.Equal(host.State.Groups.Count == 0, host.Inspector.HasNoGroups);
    }

    // --- E-2: the reads and the notifications --------------------------------------

    [Fact]
    public void TheReadsAreTheSourcesAndOutsideWritesNotify()
    {
        string vault = CopyGraphVault("reads");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphInspectorViewModel inspector = host.Inspector;
                GraphViewState state = host.State;
                GraphPreferencesViewModel preferences = host.Preferences;
                var raised = new List<string>();
                inspector.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);
                // The reads, through to the sources.
                Assert.Equal(state.Filter.IncludeAttachments, inspector.IncludeAttachments);
                Assert.Equal(state.Filter.IncludeGhosts, inspector.IncludeGhosts);
                Assert.Equal(state.Filter.OrphansOnly, inspector.OrphansOnly);
                Assert.Equal(state.NameQuery, inspector.NameQuery);
                AssertSameGroups(host);
                GraphDisplay display = preferences.CurrentConfig.Display;
                Assert.Equal(display.Arrows, inspector.Arrows);
                Assert.Equal(display.TextFadeZoom, inspector.TextFadeZoom);
                Assert.Equal(display.NodeSizeMultiplier, inspector.NodeSizeMultiplier);
                Assert.Equal(display.LinkThickness, inspector.LinkThickness);
                GraphForcesConfig forces = preferences.CurrentConfig.Forces;
                Assert.Equal(forces.Center, inspector.Center);
                Assert.Equal(forces.Repel, inspector.Repel);
                Assert.Equal(forces.Link, inspector.Link);
                Assert.Equal(forces.LinkDistance, inspector.LinkDistance);
                // Core's vectors, in order, fetched once (Term Y4).
                Assert.Equal(SlateUniffiMethods.GraphColorTokens().Select(t => t.Title), inspector.ColorTokens.Select(t => t.Title));
                Assert.Equal(SlateUniffiMethods.GraphRingStyles().Select(s => s.Title), inspector.RingStyles.Select(s => s.Title));
                Assert.Same(inspector.ColorTokens, inspector.ColorTokens);
                Assert.Same(inspector.RingStyles, inspector.RingStyles);
                // An outside write of the view state's query: a preset's flags and overlay.
                host.OpenGraph();
                raised.Clear();
                host.Workspace.GraphNavigator.RunPreset(GraphPreset.Orphans);
                host.Settle();
                Assert.True(inspector.OrphansOnly);
                Assert.Contains(nameof(GraphInspectorViewModel.IncludeAttachments), raised);
                Assert.Contains(nameof(GraphInspectorViewModel.IncludeGhosts), raised);
                Assert.Contains(nameof(GraphInspectorViewModel.OrphansOnly), raised);
                // The needle by the header's writer.
                raised.Clear();
                host.Workspace.GraphNavigator.SetNameQuery("hub");
                Assert.Equal("hub", inspector.NameQuery);
                Assert.Equal([nameof(GraphInspectorViewModel.NameQuery)], raised);
                // The display and the forces by the preferences' events.
                raised.Clear();
                preferences.SetDisplay(display with { Arrows = !display.Arrows });
                Assert.Equal(DisplayNames, raised);
                Assert.Equal(!display.Arrows, inspector.Arrows);
                raised.Clear();
                preferences.SetForces(forces with { Repel = 0.9 });
                Assert.Equal(ForceNames, raised);
                Assert.Equal(0.9, inspector.Repel);
                // The groups by an outside write of the list.
                raised.Clear();
                GraphGroupStyle style = SlateUniffiMethods.GraphConfigNextGroupStyle(0);
                state.Groups = [new GraphGroup("note", style.ColorToken, style.RingStyle)];
                Assert.Single(inspector.Groups);
                Assert.Equal(1, inspector.Groups[0].Index);
                Assert.Equal("note", inspector.Groups[0].Query);
                Assert.False(inspector.HasNoGroups);
                Assert.Equal([nameof(GraphInspectorViewModel.Groups), nameof(GraphInspectorViewModel.HasNoGroups)], raised);
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    // --- Rule X: the filter route and the needle ----------------------------------------

    [Fact]
    public void SetBackendFilterRunsChangeFilterThenPersistsAndRefusesWithoutADocument()
    {
        string vault = CopyGraphVault("filter");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphInspectorViewModel inspector = host.Inspector;
                GraphPreferencesViewModel preferences = host.Preferences;
                GraphFilter seeded = host.State.Filter;
                GraphFilter flipped = seeded with { IncludeAttachments = !seeded.IncludeAttachments };
                // No document: nothing written, nothing scheduled (Term X4; Term I7).
                ulong? before = preferences.PendingGenerationForTests;
                Assert.False(inspector.IsGraphEffective);
                inspector.SetBackendFilter(flipped);
                Assert.Equal(seeded, host.State.Filter);
                Assert.Equal(before, preferences.PendingGenerationForTests);
                // The graph open and effective: the view state, the pair, the flags persisted.
                host.OpenGraph();
                host.FlushLines();
                host.GraphLines.Clear();
                before = preferences.PendingGenerationForTests;
                Assert.True(inspector.IsGraphEffective);
                inspector.SetBackendFilter(flipped);
                Assert.Equal(flipped, host.State.Filter);
                Assert.Null(host.State.KindOnly);
                Assert.True(host.Document.IsRequestInFlight);
                Assert.NotEqual(before, preferences.PendingGenerationForTests);
                Assert.Equal(flipped.IncludeAttachments, preferences.CurrentConfig.Filters.IncludeAttachments);
                Assert.Equal(flipped.IncludeGhosts, preferences.CurrentConfig.Filters.IncludeGhosts);
                Assert.Equal(flipped.OrphansOnly, preferences.CurrentConfig.Filters.OrphansOnly);
                host.Settle();
                host.FlushLines();
                Assert.Equal(flipped, host.Document.Publication.Filter);
                Assert.Single(host.GraphLines);
                // The same flags again: nothing written, nothing requested, nothing scheduled, nothing spoken.
                preferences.FireTickForTests();
                Assert.False(preferences.HasPendingForTests);
                host.GraphLines.Clear();
                inspector.SetBackendFilter(flipped);
                Assert.False(host.Document.IsRequestInFlight);
                Assert.False(preferences.HasPendingForTests);
                host.Settle();
                host.FlushLines();
                Assert.Empty(host.GraphLines);
                Assert.True(preferences.WhenWritesDrained().Wait(TimeSpan.FromSeconds(10)));
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    [Fact]
    public void SetNameQueryIsTheNavigatorsWriter()
    {
        string vault = CopyGraphVault("needle");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphInspectorViewModel inspector = host.Inspector;
                var raised = new List<string>();
                inspector.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);
                inspector.SetNameQuery("hub");
                Assert.Equal("hub", host.State.NameQuery);
                Assert.Equal("hub", host.Preferences.CurrentConfig.Filters.NameQuery);
                Assert.Equal("hub", inspector.NameQuery);
                Assert.Equal([nameof(GraphInspectorViewModel.NameQuery)], raised);
                // The same needle: the navigator's no-op.
                raised.Clear();
                inspector.SetNameQuery("hub");
                Assert.Empty(raised);
                Assert.True(host.Preferences.WhenWritesDrained().Wait(TimeSpan.FromSeconds(10)));
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    // --- Rule Y: the groups -------------------------------------------------------------

    [Fact]
    public void GroupsAddEditAndRemoveWriteTheListToBothSourcesInCoresStylesSilently()
    {
        string vault = CopyGraphVault("groups");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphInspectorViewModel inspector = host.Inspector;
                host.OpenGraph();
                host.FlushLines();
                host.GraphLines.Clear();
                Assert.True(inspector.HasNoGroups);
                // Add: core's style by index, successive groups differing on both channels (Term Y3).
                inspector.AddGroup();
                GraphGroupStyle first = SlateUniffiMethods.GraphConfigNextGroupStyle(0);
                Assert.Single(inspector.Groups);
                Assert.Equal(string.Empty, inspector.Groups[0].Query);
                Assert.Equal(first.ColorToken, inspector.Groups[0].ColorToken);
                Assert.Equal(first.RingStyle, inspector.Groups[0].RingStyle);
                AssertSameGroups(host);
                inspector.AddGroupCommand.Execute(null);
                GraphGroupStyle second = SlateUniffiMethods.GraphConfigNextGroupStyle(1);
                Assert.Equal(2, inspector.Groups.Count);
                Assert.Equal(second.ColorToken, inspector.Groups[1].ColorToken);
                Assert.Equal(second.RingStyle, inspector.Groups[1].RingStyle);
                Assert.NotEqual(first.ColorToken, second.ColorToken);
                Assert.NotEqual(first.RingStyle, second.RingStyle);
                AssertSameGroups(host);
                // Edit: the query, the colour, the ring — each one write of the whole list.
                inspector.SetGroupQuery(0, "note");
                inspector.SetGroupColor(1, GraphColorToken.Pink);
                inspector.SetGroupRing(1, GraphRingStyle.Dotted);
                Assert.Equal("note", inspector.Groups[0].Query);
                Assert.Equal(GraphColorToken.Pink, inspector.Groups[1].ColorToken);
                Assert.Equal(GraphRingStyle.Dotted, inspector.Groups[1].RingStyle);
                AssertSameGroups(host);
                // An unchanged edit and an index outside the list: nothing written.
                ulong? generation = host.Preferences.PendingGenerationForTests;
                inspector.SetGroupQuery(0, "note");
                inspector.SetGroupQuery(7, "x");
                inspector.RemoveGroup(7);
                Assert.Equal(generation, host.Preferences.PendingGenerationForTests);
                // Remove: the row's command, then the index (Term Y5).
                inspector.Groups[0].RemoveCommand.Execute(null);
                Assert.Single(inspector.Groups);
                Assert.Equal(1, inspector.Groups[0].Index);
                Assert.Equal(GraphColorToken.Pink, inspector.Groups[0].ColorToken);
                AssertSameGroups(host);
                inspector.RemoveGroup(0);
                Assert.True(inspector.HasNoGroups);
                AssertSameGroups(host);
                // Silent (ED-Q4): no group edit spoke.
                host.Settle();
                host.FlushLines();
                Assert.Empty(host.GraphLines);
                Assert.True(host.Preferences.WhenWritesDrained().Wait(TimeSpan.FromSeconds(10)));
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    // --- Rule Z: the display ------------------------------------------------------------

    [Fact]
    public void SetDisplayIsThePreferencesTriggerAndSpeaksNothing()
    {
        string vault = CopyGraphVault("display");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphInspectorViewModel inspector = host.Inspector;
                host.OpenGraph();
                host.FlushLines();
                host.GraphLines.Clear();
                bool arrows = inspector.Arrows;
                inspector.SetArrows(!arrows);
                inspector.SetTextFadeZoom(1.5);
                inspector.SetNodeSizeMultiplier(2.0);
                inspector.SetLinkThickness(3.0);
                Assert.Equal(new GraphDisplay(!arrows, 1.5, 2.0, 3.0), host.Preferences.CurrentConfig.Display);
                Assert.Equal(new GraphDisplay(!arrows, 1.5, 2.0, 3.0), host.Document.DiagramDisplay);
                Assert.Equal(!arrows, inspector.Arrows);
                Assert.Equal(1.5, inspector.TextFadeZoom);
                Assert.Equal(2.0, inspector.NodeSizeMultiplier);
                Assert.Equal(3.0, inspector.LinkThickness);
                // Equal values: no schedule.
                host.Preferences.FireTickForTests();
                Assert.False(host.Preferences.HasPendingForTests);
                inspector.SetDisplay(new GraphDisplay(!arrows, 1.5, 2.0, 3.0));
                Assert.False(host.Preferences.HasPendingForTests);
                host.Settle();
                host.FlushLines();
                Assert.Empty(host.GraphLines);
                Assert.True(host.Preferences.WhenWritesDrained().Wait(TimeSpan.FromSeconds(10)));
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    // --- Rule K: the changed control's table (Term K3) ----------------------------------

    [Fact]
    public void ChangedForceIsTheFirstDifferingControlAtItsPercentRoundedAwayFromZero()
    {
        var baseline = new GraphForcesConfig(0.5, 0.5, 0.5, 0.5);
        Assert.Null(GraphInspectorViewModel.ChangedForce(baseline, baseline with { }));
        Assert.Equal<(GraphForceControl, uint)?>((GraphForceControl.Center, 13u), GraphInspectorViewModel.ChangedForce(baseline, baseline with { Center = 0.125 }));
        Assert.Equal<(GraphForceControl, uint)?>((GraphForceControl.Repel, 80u), GraphInspectorViewModel.ChangedForce(baseline, baseline with { Repel = 0.8 }));
        Assert.Equal<(GraphForceControl, uint)?>((GraphForceControl.Link, 88u), GraphInspectorViewModel.ChangedForce(baseline, baseline with { Link = 0.875 }));
        Assert.Equal<(GraphForceControl, uint)?>((GraphForceControl.LinkDistance, 0u), GraphInspectorViewModel.ChangedForce(baseline, baseline with { LinkDistance = 0.004 }));
        Assert.Equal<(GraphForceControl, uint)?>((GraphForceControl.LinkDistance, 100u), GraphInspectorViewModel.ChangedForce(baseline, baseline with { LinkDistance = 1.0 }));
        // Two changed in one write: the FIRST in the mac's order (ED-6).
        Assert.Equal<(GraphForceControl, uint)?>((GraphForceControl.Repel, 30u), GraphInspectorViewModel.ChangedForce(baseline, baseline with { Repel = 0.3, LinkDistance = 0.9 }));
        // Never below zero.
        Assert.Equal<(GraphForceControl, uint)?>((GraphForceControl.Center, 0u), GraphInspectorViewModel.ChangedForce(baseline, baseline with { Center = -0.5 }));
    }

    // --- Term I7: the effectiveness gate ---------------------------------------------------

    [Fact]
    public void TheEffectivenessGateFollowsTheSeatTheEdgeAndTheRetirement()
    {
        string vault = CopyGraphVault("effective");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphInspectorViewModel inspector = host.Inspector;
                var flips = new List<bool>();
                inspector.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(GraphInspectorViewModel.IsGraphEffective))
                    {
                        flips.Add(inspector.IsGraphEffective);
                    }
                };
                // No graph tab.
                Assert.False(inspector.IsGraphEffective);
                // Opened: seated and effective.
                host.OpenGraph();
                Assert.True(inspector.IsGraphEffective);
                // Behind another tab.
                host.Workspace.OpenPath("hub.md", WorkspaceOpenTarget.NewTab);
                host.Settle();
                Assert.False(host.Workspace.ActiveGroup.ActiveTab!.IsGraph);
                Assert.False(inspector.IsGraphEffective);
                // The graph tab activated again.
                host.Workspace.ActiveGroup.ActiveTab = host.GraphTab;
                host.Settle();
                Assert.True(inspector.IsGraphEffective);
                // Visible in another group while the other group is active: not
                // effective. The split is taken from the note tab, so the graph's
                // singleton stays in its own group and the reopen activates it there.
                host.Workspace.ActiveGroup.ActiveTab = host.Workspace.ActiveGroup.Tabs.First(t => !t.IsGraph);
                host.Settle();
                Assert.False(inspector.IsGraphEffective);
                host.Workspace.SplitRightCommand.Execute(null);
                WorkspaceGroupViewModel other = host.Workspace.ActiveGroup;
                host.Workspace.OpenGraph();
                host.Settle();
                WorkspaceGroupViewModel graphGroup = host.Workspace.ActiveGroup;
                Assert.NotSame(other, graphGroup);
                Assert.True(inspector.IsGraphEffective);
                host.Workspace.SelectGroupFromKeyboardFocus(other);
                host.Settle();
                Assert.True(host.Workspace.GraphTabIsVisible());
                Assert.False(host.Workspace.GraphTabIsEffective());
                Assert.False(inspector.IsGraphEffective);
                host.Workspace.SelectGroupFromKeyboardFocus(graphGroup);
                host.Settle();
                Assert.True(inspector.IsGraphEffective);
                // The graph tab closed: the document retired.
                host.Workspace.CloseActiveTabCommand.Execute(null);
                host.Settle();
                Assert.Null(host.Workspace.GraphDocument);
                Assert.False(inspector.IsGraphEffective);
                // Every notification a real change.
                for (int i = 1; i < flips.Count; i++)
                {
                    Assert.NotEqual(flips[i - 1], flips[i]);
                }
                Assert.True(flips[0]);
                Assert.False(flips[^1]);
            });
        }
        finally
        {
            DeleteVault(vault);
        }
    }

    // --- Term Y6: the read-only state --------------------------------------------------------

    [Fact]
    public void TheReadOnlyStateIsExposedAndEditsStayLive()
    {
        string vault = CopyGraphVault("read-only");
        try
        {
            string path = Path.Combine(vault, ".slate", GraphConfigStore.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            byte[] bytes = GraphConfigs.InvalidUtf8(withBom: false);
            File.WriteAllBytes(path, bytes);
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(vault);
                GraphInspectorViewModel inspector = host.Inspector;
                Assert.False(inspector.IsWritable);
                Assert.NotNull(inspector.LoadFailure);
                Assert.Equal(host.Preferences.LoadFailure, inspector.LoadFailure);
                host.OpenGraph();
                // Live: the view state and CurrentConfig move; the save is refused.
                int refused = host.Preferences.RefusedForTests;
                inspector.AddGroup();
                Assert.Single(host.State.Groups);
                Assert.Single(host.Preferences.CurrentConfig.Groups);
                inspector.SetArrows(!inspector.Arrows);
                Assert.Equal(refused + 2, host.Preferences.RefusedForTests);
                Assert.False(host.Preferences.HasPendingForTests);
                host.Settle();
            });
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally
        {
            DeleteVault(vault);
        }
    }
}
