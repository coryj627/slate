// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation;
using System.Windows.Documents;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>WB-21b (#1526) over WB-20's diagram stage: the verbosity is not
/// in the memo key, so a change reaches the code names only because the
/// memo the change drops stays dropped while a diagram renders.</summary>
public sealed partial class ReadingDiagramTests
{
    [Fact]
    public void AVerbosityChangeWhileADiagramRendersReachesTheCodeNamesAndTheDiagramLands()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open(
                "reading-diagram-code-verbosity",
                "```rust\nfn main() {\n}\n```\n\n" + StageNote);
            FlowDocument stageOne = harness.Model.Document!;
            ReadingDiagramElement placeholder = harness.Placeholder();
            int runs = harness.Model.DiagramStageRunsForTests;
            Assert.Equal("Code block, rust, 2 lines.", CodeName(harness));

            // The change's refresh fails with stage 1 preserved.
            harness.Model.FetchFaultForTests = () => new VaultException.Io("injected");
            harness.Tab.EditorPreferences.SetCodePreambleVerbosityCommand.Execute("preambleFirstLine");
            harness.Model.InvalidateForPrefsChange();
            Assert.True(PumpedDispatcher.PumpUntil(() => !harness.Model.RefreshInFlight));
            Assert.True(harness.Model.LastRefreshFailed);
            Assert.Same(stageOne, harness.Model.Document);

            // The stage still lands, on the stage-1 document.
            harness.Land();
            Assert.Same(placeholder, harness.Placeholder());
            Assert.NotNull(placeholder.Content);
            Assert.Contains(stageOne, harness.Completed);

            // An unchanged refresh rebuilds (the dropped memo stayed
            // dropped) and the code speaks the first line; the diagram is
            // complete with no new stage.
            harness.Model.FetchFaultForTests = null;
            int applies = harness.Applies;
            harness.Model.Refresh();
            PumpedDispatcher.Drain();
            Assert.Equal(applies + 1, harness.Applies);
            Assert.Equal("Code block, rust, 2 lines. fn main() {", CodeName(harness));
            Assert.NotNull(harness.Placeholder().Content);
            Assert.Equal(runs, harness.Model.DiagramStageRunsForTests);
        });
    }

    private static string CodeName(StageHarness harness) =>
        AutomationProperties.GetName(
            Flatten(harness.Surface.Document.Blocks).OfType<Paragraph>().Single(ReadingSemantics.IsCodeBlock));

    private static IEnumerable<Block> Flatten(BlockCollection blocks) =>
        blocks.SelectMany(block => block is Section section
            ? Flatten(section.Blocks).Prepend(block)
            : [block]);
}
