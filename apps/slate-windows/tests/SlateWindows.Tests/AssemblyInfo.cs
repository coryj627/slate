// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// The §W-E censuses assert against process-global native live-object
// counters (census_live_object_counts) and measure latency/concurrency
// under controlled load — parallel test classes would cross-talk both.
// Serial execution keeps every census's baseline and timing honest.

[assembly: CollectionBehavior(DisableTestParallelization = true)]
