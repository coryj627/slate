// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// Plan 47 CB-7: the settled-frame wait on fed frames. These run without a
/// window: the wait is a function of the frames its capture answers.
/// </summary>
public sealed class SettledPixelsTests
{
    private static readonly int[] A = [0xFF0000, 0xFF0000];
    private static readonly int[] B = [0x00FF00, 0x00FF00];
    private static readonly int[] C = [0x0000FF, 0x0000FF];
    private static readonly int[] D = [0xFFFF00, 0xFFFF00];
    private static readonly int[] T1 = [0x101010, 0x101010];
    private static readonly int[] T2 = [0x202020, 0x202020];
    private static readonly int[] T3 = [0x303030, 0x303030];

    /// <summary>A capture that answers <paramref name="frames"/> in turn,
    /// each as an array of its own, and counts how many were asked for.</summary>
    private static (Func<int[]?> Capture, Func<int> Asked) Feed(params int[]?[] frames)
    {
        int asked = 0;
        return (
            () =>
            {
                Assert.True(asked < frames.Length, $"the wait asked for a capture past the {frames.Length} it was fed.");
                return (int[]?)frames[asked++]?.Clone();
            },
            () => asked);
    }

    private static void AssertSettles(string how, int atCapture, params int[]?[] frames)
    {
        (Func<int[]?> capture, Func<int> asked) = Feed(frames);

        SettledPixels.Outcome outcome = SettledPixels.Wait(capture, interval: TimeSpan.Zero);

        Assert.True(outcome.Settled, $"the wait did not settle: {outcome.Why}");
        Assert.Equal(how, outcome.How);
        Assert.Equal(atCapture, asked());
        Assert.Equal(atCapture, outcome.Captures);
    }

    [Fact]
    public void ThreeEqualFramesInARowSettle() =>
        AssertSettles(SettledPixels.AtRest, 4, A, B, B, B);

    /// <summary>A change after two equal frames starts the wait over: the
    /// two before it do not count towards the three.</summary>
    [Fact]
    public void AChangeStartsTheWaitOver() =>
        AssertSettles(SettledPixels.AtRest, 7, A, A, B, B, C, C, C);

    /// <summary>A capture with nothing drawn counts for nothing, and the
    /// frames on either side of it are neither one run nor a blink.</summary>
    [Fact]
    public void ACaptureWithNothingDrawnStartsTheWaitOver() =>
        AssertSettles(SettledPixels.AtRest, 8, null, A, B, null, A, B, B, B);

    /// <summary>A caret that blinks, captured slowly: each phase gets one
    /// capture, so no two in a row are equal, and the surface has settled
    /// when the first frame comes back.</summary>
    [Fact]
    public void ABlinkCapturedSlowlySettlesWhenTheFirstFrameComesBack() =>
        AssertSettles(SettledPixels.Blinking, 3, A, B, A, B, A, B);

    /// <summary>The same blink with two captures a phase, which three equal
    /// captures in a row never see.</summary>
    [Fact]
    public void ABlinkWithTwoCapturesAPhaseSettles() =>
        AssertSettles(SettledPixels.Blinking, 5, A, A, B, B, A, A, B, B);

    /// <summary>A blink captured fast is at rest inside its first
    /// phase.</summary>
    [Fact]
    public void ABlinkCapturedFastIsAtRestInsideOnePhase() =>
        AssertSettles(SettledPixels.AtRest, 3, A, A, A, B, B, B);

    /// <summary>The caret's own clock: a phase of 530 ms, and a capture every
    /// 300 ms, which is what one UI Automation read took on a loaded job VM
    /// (#1611). The frames go A, A, B, B, A: never three equal in a row, and
    /// settled at the fifth.</summary>
    [Fact]
    public void ACaretBlinkingAtItsOwnPaceSettlesAtASlowCapturePace()
    {
        int clock = 0;
        int asked = 0;

        SettledPixels.Outcome outcome = SettledPixels.Wait(
            () =>
            {
                int[] frame = clock / 530 % 2 == 0 ? A : B;
                clock += 300;
                asked++;
                return (int[])frame.Clone();
            },
            bound: TimeSpan.FromMilliseconds(50),
            interval: TimeSpan.Zero);

        Assert.True(outcome.Settled, $"the wait did not settle: {outcome.Why}");
        Assert.Equal(SettledPixels.Blinking, outcome.How);
        Assert.Equal(5, asked);
    }

    /// <summary>A repaint that moves on, two captures a frame, never
    /// settles: it does not come back to a frame it has left.</summary>
    [Fact]
    public void ATransitionNeverSettlesAndFailsAtTheBound()
    {
        int next = 0;

        SettledPixels.Outcome outcome = SettledPixels.Wait(
            () => [next++ / 2], bound: TimeSpan.FromMilliseconds(50), interval: TimeSpan.Zero);

        Assert.False(outcome.Settled);
        Assert.Contains("neither came to rest", outcome.Why, StringComparison.Ordinal);
        Assert.Contains($"{next} captures", outcome.Why, StringComparison.Ordinal);
        Assert.Contains("longest run of equal ones was 2", outcome.Why, StringComparison.Ordinal);
        Assert.Equal(next, outcome.Captures);
    }

    /// <summary>A transition that ends on a blinking surface settles only
    /// after its end: the frames it passed through are not part of the
    /// blink.</summary>
    [Fact]
    public void ATransitionThatEndsInABlinkSettlesOnlyAfterItsEnd() =>
        AssertSettles(SettledPixels.Blinking, 6, T1, T2, T3, A, B, A, B);

    /// <summary>Three distinct frames are a surface still moving; it settles
    /// once the last of them rests.</summary>
    [Fact]
    public void ThreeDistinctFramesThenRestSettlesAtRest() =>
        AssertSettles(SettledPixels.AtRest, 5, A, B, C, C, C, D);

    /// <summary>A surface that is never drawn fails, and says that every
    /// capture was empty.</summary>
    [Fact]
    public void ASurfaceThatIsNeverDrawnFailsAndSaysSo()
    {
        int asked = 0;

        SettledPixels.Outcome outcome = SettledPixels.Wait(
            () =>
            {
                asked++;
                return null;
            },
            bound: TimeSpan.Zero,
            interval: TimeSpan.Zero);

        Assert.False(outcome.Settled);
        Assert.Contains($"{asked} captures", outcome.Why, StringComparison.Ordinal);
        Assert.Contains($"{asked} of them with nothing drawn", outcome.Why, StringComparison.Ordinal);
    }

    /// <summary>A popup that has not painted: the screen in its rectangle is
    /// the window under it, pixel for pixel, and the wait goes on until the
    /// popup's own pixels are there.</summary>
    [Fact]
    public void APopupIsOnTheScreenOnceItsRectangleDiffersFromTheWindowUnderIt()
    {
        int[] under = new int[400];
        int[] popup = Enumerable.Repeat(0x2B2B2B, 400).ToArray();
        int asked = 0;

        SettledPixels.Outcome outcome = SettledPixels.WaitUntilCovered(
            () => ++asked < 3 ? ((int[])under.Clone(), under) : (popup, under), interval: TimeSpan.Zero);

        Assert.True(outcome.Settled, $"the wait did not end: {outcome.Why}");
        Assert.Equal(SettledPixels.Covered, outcome.How);
        Assert.Equal(3, asked);
    }

    /// <summary>A popup whose surface is the colour of what lies under it
    /// still shows by its text: one pixel in a hundred is enough, and fewer
    /// is not.</summary>
    [Fact]
    public void APopupOfTheSameColourShowsByItsTextAndAStrayPixelDoesNot()
    {
        int[] under = new int[400];
        int[] text = new int[400];
        Array.Fill(text, 0xFFFFFF, 0, 4);
        int[] stray = new int[400];
        stray[0] = 0xFFFFFF;

        Assert.True(SettledPixels.WaitUntilCovered(() => (text, under), bound: TimeSpan.Zero, interval: TimeSpan.Zero).Settled);

        SettledPixels.Outcome outcome = SettledPixels.WaitUntilCovered(() => (stray, under), bound: TimeSpan.Zero, interval: TimeSpan.Zero);
        Assert.False(outcome.Settled);
        Assert.Contains("1 of 400 pixels differed", outcome.Why, StringComparison.Ordinal);
    }

    /// <summary>A popup that never paints fails at the bound with both
    /// counts, and a capture that cannot be read is no observation.</summary>
    [Fact]
    public void APopupThatNeverPaintsFailsWithBothCounts()
    {
        int[] under = new int[400];
        int asked = 0;

        SettledPixels.Outcome outcome = SettledPixels.WaitUntilCovered(
            () => ++asked % 2 == 0 ? (null, under) : ((int[])under.Clone(), under),
            bound: TimeSpan.FromMilliseconds(50),
            interval: TimeSpan.Zero);

        Assert.False(outcome.Settled);
        Assert.Contains("0 of 400 pixels differed", outcome.Why, StringComparison.Ordinal);
        Assert.Contains($"{asked} captures", outcome.Why, StringComparison.Ordinal);
        Assert.Contains("unreadable", outcome.Why, StringComparison.Ordinal);
        Assert.Equal(asked, outcome.Captures);
    }
}
