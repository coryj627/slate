// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;

namespace SlateWindows.Tests;

/// <summary>
/// §D task TD-6 (D11/DD-4): the text-scale owner's observable
/// contract. The live registry is the box's own, so the facts pin the
/// RANGE, the no-change stability, and the lifecycle — the
/// registry-change delivery itself is the windowed journey's to
/// observe, and the marshal is the same dispatcher shape TD-1's
/// engine facts already pin.
/// </summary>
public sealed class CanvasTextScaleServiceTests
{
    /// <summary>The seed read is sane on any machine: the documented
    /// range, revision zero, no phantom change event.</summary>
    [Fact]
    public void TheSeedReadIsInRangeAtRevisionZero()
    {
        using var service = new CanvasTextScaleService();
        Assert.True(
            service.Factor is >= 1.0 and <= 2.25,
            $"the seed factor {service.Factor} is outside the slider's "
            + "1.0–2.25 range — the read is not the accessibility value.");
        Assert.True(service.Revision == 0, "a construction is not a change.");
    }

    /// <summary>An unchanged registry refresh bumps nothing — the
    /// engine's revision commit dedupes on this stability.</summary>
    [Fact]
    public void AnUnchangedRefreshBumpsNothing()
    {
        using var service = new CanvasTextScaleService();
        var changes = 0;
        service.Changed += () => changes++;
        service.RefreshForTests();
        service.RefreshForTests();
        Assert.True(
            service.Revision == 0 && changes == 0,
            "a refresh with an unmoved registry bumped the revision or "
            + "raised Changed: every consumer would rebuild for nothing.");
    }

    /// <summary>Dispose detaches and is idempotent; a refresh after
    /// disposal is a no-op rather than a resurrection.</summary>
    [Fact]
    public void DisposeIsIdempotentAndEndsRefreshes()
    {
        var service = new CanvasTextScaleService();
        service.Dispose();
        service.Dispose();
        var changes = 0;
        service.Changed += () => changes++;
        service.RefreshForTests();
        Assert.True(
            service.Revision == 0 && changes == 0,
            "a disposed service refreshed: the unsubscribe half of the "
            + "W1-1 shape is the whole reason the service is disposable.");
    }
}
