// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §E TE-5a: the funnel's spine — the admission table in order,
/// E3's record-before-fallible, the conflict record's construction at
/// refusal, the unindexed commit arm, and the mid-apply displacement
/// receipt.
/// </summary>
public sealed class CanvasMutationFunnelTests
{
    private sealed class Harness
    {
        internal readonly CanvasPublicationSlot Slot = new(CanvasPublication.Seed());
        internal readonly CanvasMutationGate Gate = new();
        internal readonly CanvasUndoStack History = new();
        internal readonly CanvasBusyGate Busy = new();
        internal readonly CanvasFakeMutationSource Writes = new();
        internal readonly CanvasFakeLoadSource Reads = new();
        internal readonly List<CanvasA11yEvent> Announced = [];
        internal CanvasMutationFunnel Funnel = null!;

        internal Harness()
        {
            var population = new CanvasPopulation(null, null, null, null, null, "rev-1");
            _ = Slot.Publish(s => s
                .WithLoaded(
                    new CanvasHandleLease(7, _ => { }),
                    population,
                    CanvasProjectionUnit.Unfiltered(population))
                .WithLoadState(CanvasLoadState.Ready, null));
            History.Rebase("rev-1");
            Funnel = new CanvasMutationFunnel(
                Slot, Gate, History, Busy, Writes,
                new CanvasLoadPipeline(Slot, Reads, onReseeded: null),
                run: job => job(), announce: Announced.Add);
        }

        internal CanvasMutationOperation Operation(string label) => new(
            new CanvasOperationId(label), new object(), null,
            Slot.Current.Loaded!, CanvasMutationEffect.KeepSelection);

        internal CanvasMutationAdmission Apply(string label) =>
            Funnel.Apply(Operation(label), _ => new CanvasAction(label, []), label);
    }

    /// <summary>The happy transaction: entry recorded with the apply's
    /// successor basis, rows republished on the SAME lease, gate free
    /// after.</summary>
    [Fact]
    public void ACommitRecordsThenRepublishesOnTheSameLease()
    {
        var h = new Harness();
        CanvasHandleLease before = h.Slot.Current.Lease!;
        Assert.Equal(CanvasMutationAdmission.Admitted, h.Apply("create card"));

        Assert.NotNull(h.History.OfferedUndo);
        Assert.Equal("rev-2", h.History.AttachedBasis);
        Assert.Same(before, h.Slot.Current.Lease);
        Assert.Null(h.Gate.Held);
        Assert.Equal(1, h.Writes.Applies);
    }

    /// <summary>E3's order, observed through a refresh that faults:
    /// the entry EXISTS even though the publish never happened — the
    /// commit is never lost to a presentation failure.</summary>
    [Fact]
    public void TheEntryIsRecordedBeforeTheFallibleRefresh()
    {
        var h = new Harness();
        h.Reads.ReadFault = new InvalidOperationException("refresh died");
        Assert.ThrowsAny<Exception>(() => h.Apply("create card"));
        Assert.NotNull(h.History.OfferedUndo);
        Assert.Null(h.Gate.Held);
    }

    /// <summary>A WriteConflict builds the retained record at refusal —
    /// operation, name, pre-conflict snapshot — and announces the
    /// conflict; history does not move.</summary>
    [Fact]
    public void AConflictBuildsTheRecordAndAnnounces()
    {
        var h = new Harness();
        h.Writes.ApplyScript = _ => new VaultException.WriteConflict(
            "current", "expected", 42);
        Assert.Equal(CanvasMutationAdmission.Admitted, h.Apply("edit card"));

        Assert.NotNull(h.Funnel.Conflict);
        Assert.Equal("edit card", h.Funnel.Conflict!.AttemptedName);
        Assert.Equal("pre-conflict", h.Funnel.Conflict.PreConflictSnapshot.ContentHash);
        Assert.Contains(h.Announced, e => e is CanvasA11yEvent.CanvasSaveConflict);
        Assert.Null(h.History.OfferedUndo);
        // And the table refuses the NEXT write while the record stands
        // - AUDIBLY (§E TE-11c): the pending-conflict refusal speaks
        // the conflict's own event again, never silence.
        Assert.Equal(CanvasMutationAdmission.ConflictPending, h.Apply("another"));
        Assert.Equal(
            2,
            h.Announced.Count(e => e is CanvasA11yEvent.CanvasSaveConflict));
    }

    /// <summary>The unindexed commit arm: the entry lands with the
    /// landed hash and the publication is committed-unpresented, which
    /// the admission table then refuses on.</summary>
    [Fact]
    public void ALandedUnindexedWriteRecordsAndBlocksOnRecovery()
    {
        var h = new Harness();
        h.Writes.ApplyScript = _ => new VaultException.SavedButUnindexed(
            "rev-landed", "index step died");
        Assert.Equal(CanvasMutationAdmission.Admitted, h.Apply("create card"));

        Assert.NotNull(h.Slot.Current.CommittedUnpresented);
        Assert.Equal("rev-landed", h.History.AttachedBasis);
        Assert.Equal(CanvasMutationAdmission.RecoveryPending, h.Apply("another"));
    }

    /// <summary>The mid-apply displacement (IE-5's receipt arm): the
    /// entry is retained — quarantined against the successor's basis —
    /// and nothing publishes over the reload.</summary>
    [Fact]
    public void ADisplacedApplyRetainsItsReceiptAndPublishesNothing()
    {
        var h = new Harness();
        CanvasPopulation reloaded = new(null, null, null, null, null, "rev-external");
        h.Writes.OnApply = () =>
        {
            _ = h.Slot.Publish(s => s.WithLoaded(
                new CanvasHandleLease(8, _ => { }),
                reloaded,
                CanvasProjectionUnit.Unfiltered(reloaded)));
            h.History.Rebase("rev-external");
        };
        Assert.Equal(CanvasMutationAdmission.Admitted, h.Apply("edit card"));

        Assert.True(h.History.UndoQuarantined, "the receipt was lost or offered.");
        Assert.Same(reloaded, h.Slot.Current.Population);
        Assert.Null(h.Gate.Held);
    }

    /// <summary>The busy arm: one audible refusal per hold (IE-34) and
    /// refusal-not-queue (ED-5) — the second verb never applies.</summary>
    [Fact]
    public void ABusyGateRefusesAudiblyOncePerHold()
    {
        var h = new Harness();
        CanvasMutationOperation holder = h.Operation("slow");
        Assert.True(h.Gate.TryAcquire(holder));
        Assert.Equal(CanvasMutationAdmission.Busy, h.Apply("eager"));
        Assert.Equal(CanvasMutationAdmission.BusyAlreadyAnnounced, h.Apply("eager 2"));
        Assert.Equal(0, h.Writes.Applies);
        // §E TE-11c: audible means SPOKEN - exactly once per hold.
        Assert.Single(
            h.Announced,
            e => e is CanvasA11yEvent.CanvasBlocked
            {
                Reason: CanvasBlockedReason.ModeBusy,
            });
        h.Gate.Release(holder);
    }

    /// <summary>Codoki on §E TE-11 (the PR review's one finding):
    /// the HISTORY path must take the verb path's landed-but-unindexed
    /// arm - the receipt crosses with its REAL inverse, the publication
    /// marks committed-unpresented, and nothing publishes or speaks
    /// against the stale index (no Undid confirmation).</summary>
    [Fact]
    public void AnUnindexedHistoryApplyCrossesTheReceiptAndBlocksOnRecovery()
    {
        var h = new Harness();
        h.History.PushAndClearRedo(
            new CanvasHistoryEntry(
                "create card", new CanvasAction("undo create card", []), "rev-1"),
            "rev-1");
        CanvasHistorySnapshot snapshot = h.History.SnapshotUndo()!;
        h.Writes.ReportUnindexed = true;

        Assert.Equal(
            CanvasMutationAdmission.Admitted,
            h.Funnel.ApplyHistory(h.Operation("undo"), snapshot, redo: false));

        Assert.NotNull(h.History.OfferedRedo);
        Assert.Null(h.History.OfferedUndo);
        Assert.NotNull(h.Slot.Current.CommittedUnpresented);
        Assert.DoesNotContain(
            h.Announced, e => e is CanvasA11yEvent.CanvasHistoryApplied);
    }

    /// <summary>§F TF-0: the completion seam end to end — a
    /// submitted operation delivers its terminal outcome to the
    /// completion callback, Installed on the happy path, from the
    /// transaction's own seam.</summary>
    [Fact]
    public void ASubmittedOperationDeliversItsCompletion()
    {
        var h = new Harness();
        var outcomes = new List<CanvasOperationOutcome>();
        CanvasMutationOperation operation = h.Operation("mode commit");
        operation.Completion = outcomes.Add;
        Assert.Equal(
            CanvasMutationAdmission.Admitted,
            h.Funnel.Apply(
                operation, _ => new CanvasAction("mode commit", []), "mode commit"));
        Assert.Equal([CanvasOperationOutcome.Installed], outcomes);

        // The conflict arm reports Conflict, and exactly once.
        var h2 = new Harness();
        var outcomes2 = new List<CanvasOperationOutcome>();
        h2.Writes.ApplyScript = _ => new VaultException.WriteConflict(
            "current", "expected", 42);
        CanvasMutationOperation second = h2.Operation("mode commit 2");
        second.Completion = outcomes2.Add;
        Assert.Equal(
            CanvasMutationAdmission.Admitted,
            h2.Funnel.Apply(
                second, _ => new CanvasAction("mode commit 2", []), "mode commit 2"));
        Assert.Equal([CanvasOperationOutcome.Conflict], outcomes2);
    }

    /// <summary>§E TE-11c (E8a/E2): a not-ready admission SPEAKS the
    /// typed refusal - mac's exact reason table over the publication's
    /// load state.</summary>
    [Fact]
    public void ANotReadyAdmissionSpeaksTheTypedRefusal()
    {
        var h = new Harness();
        _ = h.Slot.Publish(s => s.WithLoadState(CanvasLoadState.Loading, null));
        Assert.Equal(CanvasMutationAdmission.NotReady, h.Apply("early"));
        Assert.Single(
            h.Announced,
            e => e is CanvasA11yEvent.CanvasMutationRefused
            {
                Reason: CanvasMutationRefusal.Opening,
            });
    }

    /// <summary>§E TE-11c (IE-10): the committed-but-unpresented
    /// admission speaks core's RefreshPending sentence - Refresh, not
    /// Reload, because the recovery re-runs the refresh, never the
    /// committed action.</summary>
    [Fact]
    public void ARecoveryPendingAdmissionSpeaksRefreshPending()
    {
        var h = new Harness();
        _ = h.Slot.Publish(s =>
            s.WithCommittedUnpresented(new CanvasOperationId("landed")));
        Assert.Equal(CanvasMutationAdmission.RecoveryPending, h.Apply("blocked"));
        Assert.Single(
            h.Announced,
            e => e is CanvasA11yEvent.CanvasMutationRefused
            {
                Reason: CanvasMutationRefusal.RefreshPending,
            });
    }

    /// <summary>§E TE-11c: a foreign mode transient refuses audibly
    /// with the mode sentence (mac's #521 guard).</summary>
    [Fact]
    public void AForeignModeTokenRefusesAudibly()
    {
        var h = new Harness();
        h.Funnel.SetModeToken(new object());
        Assert.Equal(CanvasMutationAdmission.ModeHeld, h.Apply("verb"));
        Assert.Single(
            h.Announced,
            e => e is CanvasA11yEvent.CanvasBlocked
            {
                Reason: CanvasBlockedReason.ModeBusy,
            });
    }
}
