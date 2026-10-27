// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// WB-9 (#1296): the palette's seal says when it lifts, for the window's
/// parked handoff — once, when its last cause ends.
/// </summary>
public sealed partial class CommandPaletteTests
{
    /// <summary>A command holds the seal through a modal loop of its own and
    /// past it: the seal lifts once, when the command returns.</summary>
    [Fact]
    public void TheSealReleasesOnceWhenTheCommandReturns() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        int released = 0;
        palette.SealReleased += (_, _) => released++;
        int afterItsLoop = -1;
        int beforeItReturns = -1;
        host.Harness.Source.OnInvoke = _ =>
        {
            palette.SetModalLoop(true);
            palette.SetModalLoop(false);
            afterItsLoop = released;
            PumpedDispatcher.Drain();
            beforeItReturns = released;
        };

        palette.InvokeSelected();

        Assert.Equal(0, afterItsLoop);
        Assert.Equal(0, beforeItReturns);
        Assert.Equal(1, released);
    });

    /// <summary>A modal loop over the shell with no command running: the
    /// seal lifts once, when the last loop ends.</summary>
    [Fact]
    public void TheSealReleasesOnceWhenTheLastModalLoopEnds() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        int released = 0;
        palette.SealReleased += (_, _) => released++;

        palette.SetModalLoop(true);
        palette.SetModalLoop(true);
        Assert.Equal(0, released);
        palette.SetModalLoop(false);
        Assert.Equal(1, released);
        palette.SetModalLoop(false);
        Assert.Equal(1, released);
    });
}
