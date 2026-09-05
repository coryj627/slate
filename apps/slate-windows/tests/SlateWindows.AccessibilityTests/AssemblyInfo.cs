// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Every class in this assembly drives the ONE interactive desktop —
// foreground windows, real keyboard input, UIA focus. xunit's default
// cross-class parallelism made the shell suite and the W4-1 grid
// conformance suite fight for the foreground the moment a second
// class existed (measured on CI, 2026-08-01: stolen keyboard chords,
// a COM-faulted UIA walk, and a focus assertion failing in a suite
// that was green alone). Desktop tests run strictly one at a time.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
