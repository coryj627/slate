// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §E TE-4: the effect resolution's four arms (IE-11/IE-35), the
/// completion state's at-most-once marks, and the busy gate's
/// one-refusal-per-hold (IE-34).
/// </summary>
public sealed class CanvasOperationEffectsTests
{
    private static CanvasPopulation Population(params string[] ids)
    {
        CanvasOutlineRow Row(string id) => new(NodeId: id, Depth: 0, Kind: "text", Title: id, SpeakableName: id, GroupPath: [], OrdinalN: 1, TotalM: 1, ConnectionCount: 0, ColorName: null);
        return new CanvasPopulation([.. ids.Select(Row)], null, null, null);
    }

    /// <summary>IE-35: a REQUIRED created target the refresh cannot
    /// resolve is the typed failure — never a silent clear — while the
    /// OPTIONAL current seat resolving to nothing is the truth the
    /// drop rule states.</summary>
    [Fact]
    public void ARequiredTargetMissingIsTypedAndAnOptionalOneIsNot()
    {
        CanvasPopulation refreshed = Population("a");

        CanvasEffectResolution created = CanvasEffectPlan.ResolveSelection(
            CanvasMutationEffect.SelectCreated, refreshed, currentIntent: null,
            createdId: "ghost");
        Assert.True(
            created.IsRequiredTargetMissing,
            "a missing REQUIRED target fell through the drop rule: the commit "
            + "is invisible and nobody said so (IE-35).");

        CanvasEffectResolution kept = CanvasEffectPlan.ResolveSelection(
            CanvasMutationEffect.KeepSelection, refreshed, currentIntent: "deleted",
            createdId: null);
        Assert.False(kept.IsRequiredTargetMissing);
        Assert.Null(kept.SeatValue);
    }

    /// <summary>The two seat arms: created resolves to itself; clear
    /// seats null by declaration.</summary>
    [Fact]
    public void TheSeatArmsResolveAgainstTheRefreshedPopulation()
    {
        CanvasPopulation refreshed = Population("a", "b-new");
        CanvasEffectResolution created = CanvasEffectPlan.ResolveSelection(
            CanvasMutationEffect.SelectCreated, refreshed, currentIntent: "a",
            createdId: "b-new");
        Assert.Equal("b-new", created.SeatValue);

        CanvasEffectResolution cleared = CanvasEffectPlan.ResolveSelection(
            CanvasMutationEffect.ClearSelection, refreshed, currentIntent: "a",
            createdId: null);
        Assert.Null(cleared.SeatValue);
        Assert.False(cleared.IsRequiredTargetMissing);

        CanvasEffectResolution kept = CanvasEffectPlan.ResolveSelection(
            CanvasMutationEffect.KeepSelection, refreshed, currentIntent: "a",
            createdId: null);
        Assert.Equal("a", kept.SeatValue);
    }

    /// <summary>IE-11: each addressed effect runs at most once under
    /// the operation identity — the winner runs it, retries see
    /// false.</summary>
    [Fact]
    public void CompletionMarksAreAtMostOnce()
    {
        var completion = new CanvasOperationCompletion(new CanvasOperationId("op"));
        Assert.True(completion.TryMarkAnnounced());
        Assert.False(
            completion.TryMarkAnnounced(),
            "a retry re-won the announce mark: the duplicate announcement "
            + "IE-11 names is spellable again.");
        Assert.True(completion.TryMarkEditorOpened());
        Assert.True(completion.TryMarkFocusReturned());
        Assert.False(completion.TryMarkEditorOpened());
    }

    /// <summary>IE-34: one audible refusal per HOLD — key repeat under
    /// one slow apply speaks once; a new hold speaks again.</summary>
    [Fact]
    public void TheBusyGateSpeaksOncePerHold()
    {
        var population = new CanvasPopulation(null, null, null, null);
        CanvasPublication published = CanvasPublication.Seed().WithLoaded(
            new CanvasHandleLease(7, _ => { }),
            population,
            CanvasProjectionUnit.Unfiltered(population));
        CanvasMutationOperation Hold(string label) => new(
            new CanvasOperationId(label), new object(), null,
            published.Loaded!, CanvasMutationEffect.KeepSelection);

        var gate = new CanvasBusyGate();
        CanvasMutationOperation first = Hold("first");
        Assert.True(gate.ShouldAnnounce(first));
        Assert.False(gate.ShouldAnnounce(first), "key repeat flooded the announcer.");
        Assert.False(gate.ShouldAnnounce(first));

        CanvasMutationOperation second = Hold("second");
        Assert.True(
            gate.ShouldAnnounce(second),
            "a NEW hold was merged into the old one: a true refusal went silent.");
    }
}
