// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-42 (#1625, CD30): the model's host registers no session
/// listener (<see cref="Host"/>), so it derives its cells without the events
/// core sends. This fact drives one composed cell, <c>RenamePinInDialog</c>
/// from FOLLOWING, through the real lifecycle instead: core's listener posts
/// the rename's two events (a <c>Modified</c> for <c>hub.md</c>, whose link
/// the rename rewrote, and the <c>Renamed</c>) to
/// <c>VaultLifecycleViewModel.HandleFileChange</c>, whose file-change arm
/// retargets the pin and probes the graph. The open's dirty-tab question is
/// <c>MessageBox.Show</c> in the shell, whose modal loop runs the work posted
/// while it is up before it takes the reader's click; the leaf's apply runs
/// inside it only when the pool's read has returned first. So there are two
/// orders, and the fact pins both. Click first, the cell is the model's own
/// derivation. Pool settled, the loads and the state are the same and the
/// tree's line comes before the failure.
///
/// What the harness does not reproduce: production has one queue, the
/// dispatcher's, where the harness drains the lifecycle's own queue before the
/// dispatcher; the lifecycle's workspace runs task queries inline here and on
/// the pool in production; and the vault watcher is off for the test process.
/// So this pins the listener and the file-change routing in two
/// interleavings, not every timing production can meet.
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    public enum DialogOrder
    {
        /// <summary>The leaf's read returns before the click: its apply runs inside the box.</summary>
        PoolSettlesBeforeTheClick,

        /// <summary>The click comes first: the apply runs after the open.</summary>
        ClickBeforeThePoolSettles,
    }

    [Theory]
    [InlineData(DialogOrder.PoolSettlesBeforeTheClick)]
    [InlineData(DialogOrder.ClickBeforeThePoolSettles)]
    public void ARenameOfThePinInsideItsOpensDialogReachesTheLeafThroughTheLifecycle(DialogOrder order) => StaThread.Run(() =>
    {
        using GraphVault vault = GraphVault.Copy("lifecycle-rename-pin");
        var timeline = new List<string>();
        var heard = new ConcurrentQueue<string>();
        var issued = new List<string>();
        RescanTests.Harness? h = null;
        ConnectionsLeafViewModel? leaf = null;
        ParkedFetch? parked = null;
        int asked = 0;
        int handledInTheBox = -1;

        WorkspaceDirtyNavigationDecision Decide(WorkspaceTabViewModel tab, WorkspaceItemState destination)
        {
            asked++;
            int handled = h!.Lifecycle.FileChangesHandledForTests;
            // Core posts both events to the lifecycle's queue before the rename returns.
            h.Lifecycle.SessionForTests!.RenameFile(ReRootTarget, Path.GetFileName(RenamedReRootTarget));
            // The box's modal loop runs the work posted while it is up.
            h.Context.Drain();
            handledInTheBox = h.Lifecycle.FileChangesHandledForTests - handled;
            if (order == DialogOrder.PoolSettlesBeforeTheClick)
            {
                parked!.Gate.Set();
                h.PumpUntil(() => !leaf!.InFlight && leaf.WhenAllWorkDrained().IsCompleted, "the leaf's work inside the box");
            }
            return WorkspaceDirtyNavigationDecision.Discard;
        }

        h = RescanTests.Harness.OpenExistingWithDialog(vault.Root, Decide, line => timeline.Add(line.Text));
        using (h)
        {
            ulong token = h.Lifecycle.SessionForTests!.RegisterEventListener(new HeardEvents(heard));
            try
            {
                leaf = h.Workspace.Connections;
                leaf.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ConnectionsLeafViewModel.Request) && leaf.InFlight && leaf.Request is { } request)
                    {
                        issued.Add($"{leaf.PendingPolicyForTests} {request.Root}");
                    }
                };
                h.AfterAnnounce = announced => timeline.Add(announced.GetType().Name);
                h.Workspace.ActiveLeaf = Host.ConnectionsLeaf;
                h.Workspace.OpenPath(Hub, WorkspaceOpenTarget.CurrentTab);
                h.PumpUntil(
                    () => leaf.Publication.HoldsTree && leaf.IsCurrent && !leaf.InFlight && leaf.WhenAllWorkDrained().IsCompleted,
                    "the leaf's first tree");
                h.Settle();
                WorkspaceTabViewModel dirty = h.Workspace.ActiveGroup.ActiveTab!;
                dirty.Text = "# edited and unsaved\n";
                Assert.True(dirty.IsDirty);
                timeline.Clear();
                issued.Clear();
                heard.Clear();
                int before = leaf.LoadsIssuedForTests;

                // The pin's own load stays in flight across the rename, as the
                // model's ParkedRoutes hold it.
                using ParkedFetch pinsLoad = Park(leaf);
                parked = pinsLoad;
                Assert.True(h.Workspace.ReRootConnectionsOn(ReRootTarget), "the funnel refused");
                pinsLoad.Gate.Set();
                h.PumpUntil(
                    () => !leaf.InFlight && leaf.WhenAllWorkDrained().IsCompleted && !h.Workspace.ConnectionsMountPendingForTests,
                    "the route settles");
                h.Settle();

                Assert.Equal(1, asked);
                Assert.Equal(2, handledInTheBox);
                Assert.True(pinsLoad.Reached.IsSet, "the pin's load never reached its fetch");
                Assert.False(pinsLoad.TimedOut, "the pin's load resumed on its own");
                Assert.Equal(["Modified hub.md", $"Renamed {RenamedReRootTarget} from {ReRootTarget}"], heard);
                Assert.Equal(
                    [$"{GraphAnnouncePolicy.Summary} {ReRootTarget}", $"{GraphAnnouncePolicy.Summary} {RenamedReRootTarget}"],
                    issued);
                Assert.Equal(2, leaf.LoadsIssuedForTests - before);

                ComposedDerivation derived = DeriveComposed(new ComposedCell(Mode.Following, Composed.RenamePinInDialog), FixtureOf());
                string line = LineFor(h.Lifecycle.SessionForTests!, derived.Root!, derived.Depth);
                string[] modelsTimeline = [.. derived.Timeline.Select(entry => entry == LinePlaceholder ? line : entry)];
                string[] expected = order == DialogOrder.ClickBeforeThePoolSettles
                    ? modelsTimeline
                    : [ReRooted(ReRootTarget), line, OpenFailed];
                Assert.Equal(expected, timeline);

                Assert.Equal(derived.Root, leaf.Root);
                Assert.Equal(derived.Mode.Pin, leaf.Pin);
                Assert.Equal(derived.Mode.NoteInView, leaf.NoteInView);
                Assert.Equal(derived.Mode.Stack, leaf.BackStack);
                Assert.Equal(derived.Mode.Key, h.Workspace.GraphViewStateForTests.SelectedKey);
                Assert.Equal(derived.Depth, leaf.Depth);
                Assert.Equal(ConnectionsLoadState.Ready, leaf.Publication.State);
                Assert.True(leaf.IsCurrent, "the leaf is stale after the route");
                Assert.False(leaf.InFlight, "a load is still in flight after the route");

                WorkspaceTabViewModel only = Assert.Single(h.Workspace.Groups.SelectMany(group => group.Tabs));
                Assert.Equal(ReRootTarget, only.Path);
                Assert.False(only.IsMissingFromDisk, "the failed open's tab was marked missing");
                Assert.False(only.IsDirty);
            }
            finally
            {
                h.Lifecycle.SessionForTests!.UnregisterEventListener(token);
            }
        }
    }, TimeSpan.FromSeconds(120), "the lifecycle fact timed out");

    /// <summary>The events core sent, as kind, path and the path a rename left.</summary>
    private sealed class HeardEvents(ConcurrentQueue<string> heard) : VaultEventListener
    {
        public void OnError(EventErrorCode code, string path, string message) => heard.Enqueue($"Error {code} {path}");

        public void OnFileChange(FileChangeEvent @event) =>
            heard.Enqueue(@event.PreviousPath is { } from ? $"{@event.Kind} {@event.Path} from {from}" : $"{@event.Kind} {@event.Path}");

        public void OnIndexPhase(IndexPhase phase, ulong filesSeen)
        {
        }

        public void OnExternalChange() => heard.Enqueue("ExternalChange");
    }
}
