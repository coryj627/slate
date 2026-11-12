// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// #1901: a wall-clock budget for a fact that runs inside the full suite.
/// A loaded runner slows one attempt; a regression slows every attempt. So
/// the measurement runs up to <see cref="Attempts"/> times, stops at the
/// first reading under the budget, and otherwise returns the fastest, which
/// the caller asserts against the same budget.
/// </summary>
/// <remarks>
/// A normal run measures once, cold. Only a run whose first reading misses
/// the budget measures again, and those readings are warm (JIT done, caches
/// filled). A regression in the code's own cost still misses every warm
/// reading; a cost that exists only cold, such as JIT, is the benchmark
/// lane's to record.
/// </remarks>
internal static class WallClockBudget
{
    public const int Attempts = 3;

    public static TimeSpan Fastest(TimeSpan budget, Func<TimeSpan> measure)
    {
        TimeSpan fastest = TimeSpan.MaxValue;
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            TimeSpan reading = measure();
            if (reading < fastest)
            {
                fastest = reading;
            }
            if (reading < budget)
            {
                break;
            }
        }
        return fastest;
    }

    public static async Task<TimeSpan> FastestAsync(TimeSpan budget, Func<Task<TimeSpan>> measure)
    {
        TimeSpan fastest = TimeSpan.MaxValue;
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            TimeSpan reading = await measure();
            if (reading < fastest)
            {
                fastest = reading;
            }
            if (reading < budget)
            {
                break;
            }
        }
        return fastest;
    }
}
