// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

/// <summary>W6-2 PR D (#746), D-6 and D-15 (xiv): the graph document's
/// diagrams against the FFI's live-object counter — built and torn down
/// twenty times through the switch, the count is back at its baseline, the
/// gate having freed every handle (Term G7; DD-19).</summary>
public partial class HandleLifetimeCensus
{
    [Fact]
    public void TheLayoutSessionCountReturnsToBaseline()
    {
        GraphDiagramTests.RunSta(() =>
        {
            using var host = new GraphDiagramTests.Host(6, "layout-baseline");
            GraphDocumentViewModel document = host.Open();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long baseline = SlateUniffiMethods.CensusLiveObjectCounts().LayoutSessions;
            int rounds = CensusTier.Scale(20, 60);
            for (int round = 0; round < rounds; round++)
            {
                Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
                GraphDiagramModel model = GraphDiagramTests.SettledModel(host, document);
                Assert.Equal(baseline + 1, SlateUniffiMethods.CensusLiveObjectCounts().LayoutSessions);
                Assert.True(document.SetMode(GraphSurfaceMode.Table));
                Assert.True(model.IsHandleFreedForTests);
                Assert.Equal(baseline, SlateUniffiMethods.CensusLiveObjectCounts().LayoutSessions);
            }
            Assert.Equal(rounds, document.CrossingsForTests["start_graph_layout"]);
            // A leak is GROWTH: a collection pass may reap earlier garbage.
            bool settled = Waiting.WaitFor(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return SlateUniffiMethods.CensusLiveObjectCounts().LayoutSessions <= baseline;
            }, 15_000);
            Assert.True(settled, $"layout sessions leaked: {SlateUniffiMethods.CensusLiveObjectCounts().LayoutSessions} (baseline {baseline})");
        });
    }
}
