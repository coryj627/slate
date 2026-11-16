// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// Plan 47 CB-7: the settled-frame wait on fed frames. These run without a
/// window: the wait is a function of the frames its capture answers.
/// </summary>
public sealed class SettledPixelsTests
{
    private static readonly int[] Red = [0xFF0000, 0xFF0000];
    private static readonly int[] Green = [0x00FF00, 0x00FF00];

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

    [Fact]
    public void ThreeEqualFramesInARowSettle()
    {
        (Func<int[]?> capture, Func<int> asked) = Feed(Red, Green, Green, Green);

        string? why = SettledPixels.Wait(capture, interval: TimeSpan.Zero);

        Assert.Null(why);
        Assert.Equal(4, asked());
    }

    /// <summary>A change after two equal frames starts the run again: the
    /// two before it do not count towards the three.</summary>
    [Fact]
    public void AChangeStartsTheRunAgain()
    {
        (Func<int[]?> capture, Func<int> asked) = Feed(Red, Red, Green, Red, Red, Red);

        string? why = SettledPixels.Wait(capture, interval: TimeSpan.Zero);

        Assert.Null(why);
        Assert.Equal(6, asked());
    }

    /// <summary>A capture with nothing drawn counts for nothing, and the
    /// frames on either side of it are not one run.</summary>
    [Fact]
    public void ACaptureWithNothingDrawnStartsTheRunAgain()
    {
        (Func<int[]?> capture, Func<int> asked) = Feed(null, Red, Red, null, Red, Red, Red);

        string? why = SettledPixels.Wait(capture, interval: TimeSpan.Zero);

        Assert.Null(why);
        Assert.Equal(7, asked());
    }

    /// <summary>Frames that never repeat fail at the bound, and the failure
    /// says how many captures were made and how long the longest run was.</summary>
    [Fact]
    public void FramesThatNeverRepeatFailAtTheBound()
    {
        int next = 0;

        string? why = SettledPixels.Wait(() => [next++], bound: TimeSpan.FromMilliseconds(50), interval: TimeSpan.Zero);

        Assert.NotNull(why);
        Assert.Contains("no 3 captures in a row were equal", why, StringComparison.Ordinal);
        Assert.Contains($"{next} captures", why, StringComparison.Ordinal);
        Assert.Contains("longest run of equal ones was 1", why, StringComparison.Ordinal);
    }

    /// <summary>A surface that is never drawn fails too, and says that every
    /// capture was empty.</summary>
    [Fact]
    public void ASurfaceThatIsNeverDrawnFailsAndSaysSo()
    {
        int asked = 0;

        string? why = SettledPixels.Wait(
            () =>
            {
                asked++;
                return null;
            },
            bound: TimeSpan.Zero,
            interval: TimeSpan.Zero);

        Assert.NotNull(why);
        Assert.Contains($"{asked} captures, {asked} of them with nothing drawn", why, StringComparison.Ordinal);
    }
}
