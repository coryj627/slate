// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR B, slice B1 (#746): the receiver's rejections (rule C, Term 8;
/// B-2) — each token field and each echo of both calls, the centre key
/// and the depth, the parked worker released after each and NOTHING
/// changed by any rejection; supersession (Term 4); the golden's
/// projection (B-17).
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    /// <summary>Park the leaf's worker after both crossings; the caller
    /// moves the world on the dispatcher, then releases.</summary>
    private static (ManualResetEventSlim Gate, ManualResetEventSlim Reached) Park(ConnectionsLeafViewModel leaf)
    {
        var gate = new ManualResetEventSlim(false);
        var reached = new ManualResetEventSlim(false);
        bool armed = true;
        leaf.FetchGateForTests = () =>
        {
            if (armed)
            {
                armed = false;
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(10));
            }
        };
        return (gate, reached);
    }

    private static void AssertNothingChanged(Host host, ConnectionsPublication before, int loadsBefore)
    {
        Assert.Same(before, host.Leaf.Publication);
        Assert.Equal(loadsBefore, host.Loads);
        Assert.Empty(host.RelayLines);
        Assert.False(host.Leaf.InFlight);
    }

    /// <summary>Each token field and each echo of both calls, the centre
    /// key and the depth — one rewrite per rejection Term 8 names.</summary>
    private static ConnectionsLoadEnvelope Rewrite(string label, ConnectionsLoadEnvelope e) => label switch
    {
        // A stale SEQUENCE is the reversed completion's witness below: an
        // older sequence landing leaves the newer one in flight, by design.
        "lifecycle" => e with { Token = e.Token with { LifecycleGeneration = e.Token.LifecycleGeneration + 1 } },
        "request-depth" => e with { Token = e.Token with { Request = e.Token.Request with { Depth = e.Token.Request.Depth + 1 } } },
        "request-epoch" => e with { Token = e.Token with { Request = e.Token.Request with { Epoch = e.Token.Request.Epoch + 1 } } },
        "tree-path" => e with { TreePath = "2.md" },
        "tree-depth" => e with { TreeDepth = e.TreeDepth + 1 },
        "tree-filter" => e with { TreeFilter = new GraphFilter(false, true, false) },
        "bundle-path" => e with { BundlePath = "2.md" },
        "bundle-paging" => e with { BundlePaging = new Paging(null, 50) },
        "centre-key" => e with { Tree = e.Tree! with { CenterKey = "p:2.md" } },
        "centre-depth" => e with { Tree = e.Tree! with { Depth = e.Tree.Depth + 1 } },
        _ => throw new ArgumentOutOfRangeException(nameof(label)),
    };

    [Theory]
    [InlineData("lifecycle")]
    [InlineData("request-depth")]
    [InlineData("request-epoch")]
    [InlineData("tree-path")]
    [InlineData("tree-depth")]
    [InlineData("tree-filter")]
    [InlineData("bundle-path")]
    [InlineData("bundle-paging")]
    [InlineData("centre-key")]
    [InlineData("centre-depth")]
    public void AnEnvelopeWhoseTokenOrEchoDisagreesIsRejectedAndChangesNothing(string label)
    {
        Func<ConnectionsLoadEnvelope, ConnectionsLoadEnvelope> rewrite = envelope => Rewrite(label, envelope);
        using GraphVault vault = GraphVault.Copy("reject-" + label);
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            ConnectionsPublication before = leaf.Publication;
            Assert.Equal(ConnectionsLoadState.Ready, before.State);
            host.Clear();

            // A same-root reload (a Show) whose envelope is rewritten on the
            // way back: the presentation is kept at the start (Term 7) and
            // the rejection keeps it kept.
            bool armed = true;
            leaf.EnvelopeForTests = envelope =>
            {
                if (!armed)
                {
                    return envelope;
                }
                armed = false;
                return rewrite(envelope);
            };
            int loads = host.Loads;
            host.Workspace.ShowConnections();
            Assert.Equal(loads + 1, host.Loads);
            host.Settle();

            Assert.Same(before, leaf.Publication);
            Assert.Equal(loads + 1, host.Loads);
            Assert.False(leaf.InFlight);
            // The panel line is the workspace's; nothing from the receiver.
            Assert.Equal([PanelLine()], host.RelayLines);
        });
    }

    [Fact]
    public void AReversedCompletionIsRejected()
    {
        using GraphVault vault = GraphVault.Copy("reversed");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            Assert.Equal(ConnectionsLoadState.Ready, leaf.Publication.State);
            (ManualResetEventSlim gate, ManualResetEventSlim reached) = Park(leaf);
            using (gate)
            using (reached)
            {
                // The depth-2 load parks; the depth-3 load lands first; the
                // parked one is stale by seq when released.
                host.Clear();
                leaf.SetDepth(2);
                Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the load never reached the gate");
                leaf.SetDepth(3);
                gate.Set();
                host.Settle();

                Assert.Equal(3u, leaf.Publication.Tree!.Depth);
                Assert.Equal([Summary(leaf)], host.RelayLines);
                Assert.False(leaf.InFlight);
            }
        });
    }

    [Fact]
    public void AnUnmountedRootChangeWhileALoadIsInFlightMakesItsResultForeign()
    {
        using GraphVault vault = GraphVault.Copy("unmounted-race");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Two);
            host.Settle();
            host.Clear();
            (ManualResetEventSlim gate, ManualResetEventSlim reached) = Park(leaf);
            using (gate)
            using (reached)
            {
                // A's load parks; the pane collapses; the editor moves to B
                // (no load: unmounted); A's result must not install or speak
                // (IGH-3: the live root and epoch).
                host.OpenNote(Hub);
                Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the load never reached the gate");
                host.Workspace.IsRightPaneVisible = false;
                host.OpenNote(Two);
                int loads = host.Loads;
                gate.Set();
                host.Settle();

                // A's result is foreign: the presentation stays what the
                // start transition installed for A — Loading(A), now STALE
                // for B — and nothing speaks; B loads on the next mount.
                Assert.Equal(Two, leaf.Root);
                Assert.Equal(loads, host.Loads);
                Assert.Equal(ConnectionsLoadState.Loading, leaf.Publication.State);
                Assert.Equal(Hub, leaf.Publication.ProducedBy?.Root);
                Assert.True(leaf.IsStale);
                Assert.False(leaf.InFlight);
                Assert.Empty(host.RelayLines);
            }
        });
    }

    [Fact]
    public void AShutdownBeforeDispatchDropsTheResult()
    {
        using GraphVault vault = GraphVault.Copy("shutdown");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            (ManualResetEventSlim gate, ManualResetEventSlim reached) = Park(leaf);
            using (gate)
            using (reached)
            {
                Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the load never reached the gate");
                host.Clear();
                leaf.Retire();
                gate.Set();
                PumpedDispatcher.PumpUntilDrained(leaf.WhenAllWorkDrained());
                PumpedDispatcher.Drain();

                Assert.True(leaf.IsRetired);
                Assert.Equal(ConnectionsLoadState.NoNote, leaf.Publication.State);
                Assert.Empty(host.RelayLines);
            }
        });
    }

    [Fact]
    public void ALifecycleReplacementMakesTheResultForeign()
    {
        using GraphVault vault = GraphVault.Copy("lifecycle");
        PumpedDispatcher.Run(() =>
        {
            int generation = 1;
            using var host = new Host(vault.Root, () => generation);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            (ManualResetEventSlim gate, ManualResetEventSlim reached) = Park(leaf);
            using (gate)
            using (reached)
            {
                Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the load never reached the gate");
                host.Clear();
                generation = 2;
                gate.Set();
                host.Settle();

                Assert.Equal(ConnectionsLoadState.Loading, leaf.Publication.State);
                Assert.Empty(host.RelayLines);
            }
        });
    }

    // --- Supersession (Term 4) --------------------------------------------------------

    [Fact]
    public void AnAudibleLoadAfterAnotherSpeaksItsOwnLineOnly()
    {
        using GraphVault vault = GraphVault.Copy("supersede");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            host.Clear();
            (ManualResetEventSlim gate, ManualResetEventSlim reached) = Park(leaf);
            using (gate)
            using (reached)
            {
                host.Workspace.ShowConnections();
                Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the load never reached the gate");
                host.Workspace.ShowConnections();
                gate.Set();
                host.Settle();

                Assert.Equal([PanelLine(), PanelLine(), Summary(leaf)], host.RelayLines);
            }
        });
    }

    // --- The golden's projection (B-17) ------------------------------------------------

    [Theory]
    [InlineData(Hub, 1u)]
    [InlineData(Hub, 2u)]
    [InlineData(Hub, 3u)]
    [InlineData(Deep, 2u)]
    [InlineData("10.md", 1u)]
    [InlineData("self.md", 1u)]
    public void TheGoldensConnectionsEntryIsTheLeafsTreeProjectedToTheSevenIdFreeFields(string path, uint depth)
    {
        using GraphVault vault = GraphVault.Copy("golden");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(path);
            host.Leaf.SetDepth(depth);
            host.Settle();
            GraphConnectionsTree tree = host.Leaf.Publication.Tree!;

            string goldenPath = Path.Combine(
                SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures", "parity_golden", "graph_queries.json");
            using JsonDocument golden = JsonDocument.Parse(File.ReadAllBytes(goldenPath));
            JsonElement entry = golden.RootElement.GetProperty("connections")
                .EnumerateArray()
                .Single(e => e.GetProperty("path").GetString() == path && e.GetProperty("depth").GetUInt32() == depth);

            Assert.Equal(entry.GetProperty("center_key").GetString(), tree.CenterKey);
            Assert.Equal(entry.GetProperty("tree_depth").GetUInt32(), tree.Depth);
            JsonElement summary = entry.GetProperty("summary");
            Assert.Equal(summary.GetProperty("center_label").GetString(), tree.SummaryCounts.CenterLabel);
            Assert.Equal(summary.GetProperty("in_links").GetUInt32(), tree.SummaryCounts.InLinks);
            Assert.Equal(summary.GetProperty("out_links").GetUInt32(), tree.SummaryCounts.OutLinks);
            Assert.Equal(summary.GetProperty("note_count").GetUInt64(), tree.SummaryCounts.NoteCount);
            Assert.Equal(summary.GetProperty("depth").GetUInt32(), tree.SummaryCounts.Depth);
            foreach ((string name, GraphConnectionRow[] rows) in new[] { ("incoming", tree.Incoming), ("outgoing", tree.Outgoing) })
            {
                JsonElement[] expected = [.. entry.GetProperty(name).EnumerateArray()];
                Assert.Equal(expected.Length, rows.Length);
                for (int i = 0; i < rows.Length; i++)
                {
                    Assert.Equal(expected[i].GetProperty("occurrence").GetString(), rows[i].Id);
                    Assert.Equal(
                        expected[i].GetProperty("parent").ValueKind == JsonValueKind.Null ? null : expected[i].GetProperty("parent").GetString(),
                        rows[i].ParentId);
                    Assert.Equal(expected[i].GetProperty("level").GetUInt32(), rows[i].Level);
                    Assert.Equal(expected[i].GetProperty("key").GetString(), rows[i].StableKey);
                    Assert.Equal(expected[i].GetProperty("kind").GetString(), KindName(rows[i].Kind));
                    Assert.Equal(expected[i].GetProperty("embed_only").GetBoolean(), rows[i].EmbedOnly);
                    Assert.Equal(expected[i].GetProperty("references").GetUInt32(), rows[i].References);
                }
            }
        });
    }

    private static string KindName(GraphNodeKind kind) => kind switch
    {
        GraphNodeKind.Note => "note",
        GraphNodeKind.Attachment => "attachment",
        GraphNodeKind.Ghost => "ghost",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
