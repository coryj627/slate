// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Plan 43 PR 20c (#1267; design §4.7, the test switch): every vault this
/// test process opens keeps core's vault watcher off. `SlateMacTestBootstrap`
/// (bootstrap.c) sets the switch as the test bundle loads, before any test
/// runs, because AppState's open arms the watcher on every vault: a fact that
/// changes a vault from outside and then asserts what a Refresh counted would
/// otherwise race a Watcher run, which could index the change first and leave
/// the Refresh saying "No changes". Core's `start_watching` reads the
/// variable at every call, so a watcher fact lifts it around its own open and
/// puts it back once the open has armed (`VaultRescanTests.open(watching:)`).
/// XCTest runs one test at a time in a process, so no other fact's open sees
/// it lifted.
enum VaultWatcherSwitch {
    static let variable = "SLATE_TEST_DISABLE_VAULT_WATCHER"

    /// The switch's value in this process's environment, the one core reads.
    static var value: String? {
        getenv(variable).map { String(cString: $0) }
    }

    /// Lift the switch: the next `startWatching()` arms the real watcher.
    static func lift() {
        unsetenv(variable)
    }

    /// Put the switch back, as the bootstrap set it.
    static func restore() {
        setenv(variable, "1", 1)
    }
}
