// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// #1455: before XCTest runs a single test, route this test process's Trash
// into a scratch bin, so no test moves its fixtures into the ~/.Trash of
// whoever runs the suite.
//
// Most mac tests open their vault through AppState's own open (the shipping
// VaultSession.openFilesystem) and trash through core, as the app does; some
// open a session themselves. Core can route every session of one process,
// but something must ask it before the first one opens, and a SwiftPM test
// bundle runs no code of its own before its tests: it has no principal class
// and Swift has no load hook. A C constructor does run, once per process, as
// the bundle loads (for `swift test --parallel`, in every worker). It calls
// the Swift half, ScratchTrashBootstrap.swift in SlateMacTests, which makes
// the bin and calls core's routeTrashToScratchBinForTests. This target is
// linked into the test bundle only; the app never contains it.

#include "SlateMacTestBootstrap.h"

// Defined in Swift (@_cdecl) by ScratchTrashBootstrap.swift.
extern void slate_mac_tests_route_trash_to_scratch_bin(void);

__attribute__((constructor)) static void slate_mac_tests_bootstrap(void) {
    slate_mac_tests_route_trash_to_scratch_bin();
}
