// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>WB-21 (#1415): the three graph verbosity rows, run through the
/// shipped registry. Typing "verbosity" lists all three; Enter on one sets
/// its level through the setter the Graph menu uses, and invents no
/// announcement — the menu route speaks none either.</summary>
public sealed partial class CommandPaletteTests
{
    [Fact]
    public void TypingGraphVerbosityListsTheThreeRowsAndEnterSetsTheLevel()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "palette-graph-verbosity");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        var workspaceAnnounced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], workspaceAnnounced.Add,
            startInteractionBackgroundWork: false);
        Assert.Equal(GraphVerbosity.Standard, workspace.GraphPreferences.Verbosity);

        string recents = Path.Combine(fixture.Root + "-device", "command-palette-recents.json");
        try
        {
            using var source = new PaletteCommandSource(
                new WorkspaceCommandHost(workspace),
                Dispatcher.CurrentDispatcher,
                new CommandPaletteRecentsStore(recents));
            var announced = new List<A11yEvent>();
            var palette = new CommandPaletteViewModel(source, announced.Add, lane: new InlineWorkLane());
            palette.Open();
            palette.Query = "graph verbosity";

            string[] verbosityRows = palette.Rows
                .Where(row => row.Id.StartsWith("slate.graph.setVerbosity", StringComparison.Ordinal))
                .Select(row => row.Label)
                .ToArray();
            Assert.Equal(
                new[] { "Graph Verbosity: Standard", "Graph Verbosity: Terse", "Graph Verbosity: Verbose" },
                verbosityRows.Order(StringComparer.Ordinal).ToArray());

            // Each row, in an order that leaves the default last, so a row
            // resolved to a neighbour's command cannot pass.
            (string Id, GraphVerbosity Level)[] runs =
            [
                (ChordTable.Ids.GraphSetVerbosityTerse, GraphVerbosity.Terse),
                (ChordTable.Ids.GraphSetVerbosityVerbose, GraphVerbosity.Verbose),
                (ChordTable.Ids.GraphSetVerbosityStandard, GraphVerbosity.Standard),
            ];
            foreach ((string id, GraphVerbosity level) in runs)
            {
                palette.Open();
                palette.Query = "graph verbosity";
                CommandPaletteRowViewModel row = Assert.Single(palette.Rows, candidate => candidate.Id == id);
                int heard = announced.Count;
                int workspaceHeard = workspaceAnnounced.Count;
                palette.Invoke(row);

                Assert.Equal(level, workspace.GraphPreferences.Verbosity);
                Assert.Equal(
                    level,
                    workspace.GraphPreferences.Choices.Single(choice => choice.IsSelected).Spec.Verbosity);
                // The menu route speaks nothing for a level change; the
                // palette route invents nothing either.
                Assert.Equal(heard, announced.Count);
                Assert.Equal(workspaceHeard, workspaceAnnounced.Count);
            }
        }
        finally
        {
            if (Directory.Exists(fixture.Root + "-device"))
            {
                Directory.Delete(fixture.Root + "-device", recursive: true);
            }
        }
    }
}
