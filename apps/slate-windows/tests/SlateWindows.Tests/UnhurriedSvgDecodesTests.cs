// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace SlateWindows.Tests;

/// <summary>
/// #1635: the unit-test host's SVG decode configuration
/// (<see cref="UnhurriedSvgDecodes"/>), and the app's own put back for one
/// fact each. The suite runs serially, so a process-wide seam a fact changes
/// reaches no other fact; each fact puts the host's configuration back in a
/// <c>finally</c>.
/// </summary>
public sealed class UnhurriedSvgDecodesTests
{
    /// <summary>The host turns the process's own timeout memory off and leaves
    /// a projection's deadline open; with both deadline seams cleared, a
    /// projection gets the app's 20 seconds.</summary>
    [Fact]
    public void UnhurriedSvgDecodesKeepsTheProcessMemoryOffAndTheDeadlineOpen()
    {
        Assert.True(EditorInteractionCoordinator.SvgDecodeProcessMemoryOffForTests);
        Assert.Equal(
            int.MaxValue,
            EditorInteractionCoordinator.SvgProjectionDecodeDeadlineDefaultMillisecondsForTests);

        EditorInteractionCoordinator.SvgProjectionDecodeDeadlineMillisecondsForTests = null;
        EditorInteractionCoordinator.SvgProjectionDecodeDeadlineDefaultMillisecondsForTests = null;
        try
        {
            Assert.Equal(
                TimeSpan.FromMilliseconds(20_000),
                EditorInteractionCoordinator.SvgDecodeDeadline.ForProjection().Left);
        }
        finally
        {
            UnhurriedSvgDecodes.LetEveryRealDecodeFinish();
        }
    }

    /// <summary>#1610 (the merge judge's minor 2): the host charges a
    /// projection's deadline nothing for the guard's checks, so #1635's
    /// exact waits hold; with the seams cleared, as in the app, a check is
    /// charged the time it took. A document the guard takes some
    /// milliseconds over is charged more than nothing and no more than the
    /// call around it took.</summary>
    [Fact]
    public void TheHostChargesNoGuardTimeAndTheAppChargesWhatTheCheckTook()
    {
        Assert.NotNull(EditorInteractionCoordinator.SvgGuardChargeDefaultForTests);
        Assert.Equal(
            TimeSpan.Zero,
            EditorInteractionCoordinator.SvgGuardChargeDefaultForTests!(TimeSpan.FromSeconds(1)));

        byte[] costly = Encoding.UTF8.GetBytes(SlowSvgDocuments.Generate("A_star_g R=3000 E=13000"));
        var deadline = new EditorInteractionCoordinator.SvgDecodeDeadline(60_000);
        EditorInteractionCoordinator.SvgDecodeSlotsForTests = new EditorInteractionCoordinator.SvgDecodeSlots();
        EditorInteractionCoordinator.SvgGuardChargeForTests = null;
        EditorInteractionCoordinator.SvgGuardChargeDefaultForTests = null;
        try
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            string? refusal = EditorInteractionCoordinator.SvgDecodeRefusalBeforeDecoding(costly, deadline);
            TimeSpan took = System.Diagnostics.Stopwatch.GetElapsedTime(started);
            Assert.Equal(EditorInteractionCoordinator.SvgDecodeCostRefusedMessage, refusal);
            TimeSpan charged = TimeSpan.FromMilliseconds(60_000) - deadline.Left;
            Assert.True(
                charged > TimeSpan.Zero && charged <= took,
                $"a check that took {took.TotalMilliseconds:F1} ms was charged {charged.TotalMilliseconds:F1} ms");
        }
        finally
        {
            EditorInteractionCoordinator.SvgDecodeSlotsForTests = null;
            UnhurriedSvgDecodes.LetEveryRealDecodeFinish();
        }
    }

    /// <summary>With the memory on, as in the app, the process's own decoder
    /// slots remember a decode that ran out its budget: the same bytes
    /// decoded again never reach the decoder.</summary>
    [Fact]
    public void TheProcessMemoryRemembersATimeout()
    {
        EditorInteractionCoordinator.SvgDecodeSlots process =
            EditorInteractionCoordinator.ProcessSvgDecodeSlotsForTests;
        int liveBefore = Volatile.Read(ref process.Live);
        var decodes = new ParkedSvgDecodes();
        // A comment of its own makes these bytes no other fact's: the
        // process's memory outlives this fact.
        byte[] svg = Encoding.UTF8.GetBytes(
            "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'>"
            + "<!-- UnhurriedSvgDecodesTests.TheProcessMemoryRemembersATimeout -->"
            + "<rect width='8' height='8' fill='red'/></svg>");
        EditorInteractionCoordinator.SvgDecodeProcessMemoryOffForTests = false;
        EditorInteractionCoordinator.SvgDecodeBodyForTests = decodes.Parked;
        EditorInteractionCoordinator.SvgDecodeWaitForTests = decodes.TimeoutOnceEntered;
        try
        {
            Assert.Null(EditorInteractionCoordinator.DecodeImage(svg, "image/svg+xml", out string? first));
            Assert.Equal(EditorInteractionCoordinator.SvgDecodeTimeExceededMessage, first);
            Assert.Null(EditorInteractionCoordinator.DecodeImage(svg, "image/svg+xml", out string? repeat));
            Assert.Equal(1, decodes.Runs);
            Assert.Equal(EditorInteractionCoordinator.SvgDecodeTimeExceededMessage, repeat);
        }
        finally
        {
            EditorInteractionCoordinator.SvgDecodeWaitForTests = null;
            EditorInteractionCoordinator.SvgDecodeBodyForTests = null;
            UnhurriedSvgDecodes.LetEveryRealDecodeFinish();
            decodes.Release();
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref process.Live) == liveBefore, TimeSpan.FromSeconds(10)),
                "the parked decode never ended");
        }
    }
}
