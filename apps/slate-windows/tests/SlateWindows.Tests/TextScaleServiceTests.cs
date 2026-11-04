// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// §D task TD-6 (D11/DD-4): the text-scale owner's observable
/// contract. The live registry is the box's own, so the facts pin the
/// RANGE, the no-change stability, and the lifecycle — the
/// registry-change delivery itself is the windowed journey's to
/// observe, and the marshal is the same dispatcher shape TD-1's
/// engine facts already pin.
/// </summary>
public sealed class TextScaleServiceTests
{
    /// <summary>The seed read is sane on any machine: the documented
    /// range, revision zero, no phantom change event.</summary>
    [Fact]
    public void TheSeedReadIsInRangeAtRevisionZero()
    {
        using var service = new TextScaleService();
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
        using var service = new TextScaleService();
        var changes = 0;
        service.Changed += () => changes++;
        service.RefreshForTests();
        service.RefreshForTests();
        Assert.True(
            service.Revision == 0 && changes == 0,
            "a refresh with an unmoved registry bumped the revision or "
            + "raised Changed: every consumer would rebuild for nothing.");
    }

    /// <summary>The seam's read drives everything a consumer rides.</summary>
    [Fact]
    public void TheReadDrivesTheFactorTheRevisionAndChanged()
    {
        double read = 1.5;
        using var service = new TextScaleService(() => read);
        var changes = 0;
        service.Changed += () => changes++;
        Assert.Equal(1.5, service.Factor);

        read = 2;
        service.RefreshForTests();

        Assert.Equal(2, service.Factor);
        Assert.Equal(1, service.Revision);
        Assert.Equal(1, changes);
    }

    /// <summary>A launched test sets its factor only under the census's instance, so a
    /// stray variable can't change a reader's text size.</summary>
    [Fact]
    public void TheTestFactorNeedsTheCensusInstance()
    {
        string? instance = Environment.GetEnvironmentVariable("SLATE_CENSUS_INSTANCE_ID");
        string? forced = Environment.GetEnvironmentVariable(TextScaleService.TestFactorVariable);
        try
        {
            Environment.SetEnvironmentVariable(TextScaleService.TestFactorVariable, "2.25");
            Environment.SetEnvironmentVariable("SLATE_CENSUS_INSTANCE_ID", "a-census");
            Assert.Equal(2.25, TextScaleService.ReadFactor());
            Environment.SetEnvironmentVariable(TextScaleService.TestFactorVariable, "9");
            Assert.Equal(2.25, TextScaleService.ReadFactor());

            // With no instance it reads the desktop, which can be set to anything.
            Environment.SetEnvironmentVariable("SLATE_CENSUS_INSTANCE_ID", null);
            Environment.SetEnvironmentVariable(TextScaleService.TestFactorVariable, null);
            double desktop = TextScaleService.ReadFactor();
            Environment.SetEnvironmentVariable(TextScaleService.TestFactorVariable, desktop == 2 ? "1.5" : "2");
            Assert.Equal(desktop, TextScaleService.ReadFactor());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SLATE_CENSUS_INSTANCE_ID", instance);
            Environment.SetEnvironmentVariable(TextScaleService.TestFactorVariable, forced);
        }
    }

    /// <summary>Dispose detaches and is idempotent; a refresh after
    /// disposal is a no-op rather than a resurrection.</summary>
    [Fact]
    public void DisposeIsIdempotentAndEndsRefreshes()
    {
        var service = new TextScaleService();
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
