// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// #1901: a wall-clock budget for a fact that runs inside the full suite.
/// The measurement runs once untimed, then <see cref="Samples"/> times, and
/// the caller asserts the median of those readings against its budget.
/// </summary>
/// <remarks>
/// <para>Why warm: in the full suite a fact's first reading is first use only
/// of whatever earlier facts did not already load, so it measured test order
/// as much as the code. Measured locally (2026-11-12), the 2,000-row outline
/// projection read 115 ms on its first construction in a process and
/// 2.6 to 3 ms warm; #1901's failing 598 ms was a first construction on a
/// loaded runner.</para>
/// <para>Why the median: a loaded runner stretches some readings, and
/// sustained load stretches all of them, but warm readings sit two orders
/// of magnitude under the 500 ms budget, so load does not reach it. A
/// regression in the code's own cost that crosses the budget is in every
/// warm reading, so the median crosses it too.</para>
/// <para>First-use cost (JIT, WPF's type loading) is not guarded here, and
/// the old single reading did not guard it either: it depended on which
/// facts ran first. The canvas benchmark runner warms up too.</para>
/// </remarks>
internal static class WallClockBudget
{
    public const int Samples = 5;

    public static TimeSpan WarmMedian(Func<TimeSpan> measure)
    {
        _ = measure();
        var readings = new TimeSpan[Samples];
        for (int sample = 0; sample < Samples; sample++)
        {
            readings[sample] = measure();
        }
        return Median(readings);
    }

    public static async Task<TimeSpan> WarmMedianAsync(Func<Task<TimeSpan>> measure)
    {
        _ = await measure();
        var readings = new TimeSpan[Samples];
        for (int sample = 0; sample < Samples; sample++)
        {
            readings[sample] = await measure();
        }
        return Median(readings);
    }

    private static TimeSpan Median(TimeSpan[] readings)
    {
        Array.Sort(readings);
        return readings[readings.Length / 2];
    }
}
