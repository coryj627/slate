// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// The §W-E censuses assert against process-global native live-object
// counters (census_live_object_counts) and measure latency/concurrency
// under controlled load — parallel test classes would cross-talk both.
// Serial execution keeps every census's baseline and timing honest.

[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

// Every fact, whichever helper started its threads: a WPF dispatcher it
// abandons (thread ended, never shut down) leaks window classes into the
// desktop heap for the rest of the run, and CI's non-interactive 768 KB
// heap cannot afford them (see Support/StaThread.cs).
[assembly: SlateWindows.Tests.LeakedDispatcherGuard]

// #1826: records how the test process exits after its last test, and dumps
// a process still alive just before xUnit forces the exit (Support/ExitWatch.cs).
[assembly: Xunit.AssemblyFixture(typeof(SlateWindows.Tests.ExitWatch))]
