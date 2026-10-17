// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

@testable import SlateMac

/// Core's post-publish fault seam for the create funnels' facts (#1140):
/// `SLATE_TEST_FAULT_AFTER_WRITE` holds a vault-relative path substring,
/// and a create whose path contains it lands its bytes, then fails before
/// the index commit — `CreateExclusiveOutcome.publishedUnindexed`
/// (#1123), the arm a funnel must finish as a create. (`save_text`
/// shares the seam and reports `SavedButUnindexed`.)
///
/// The variable is process-global, as `MutationHarnessTests`' faults are.
/// Every use names a trigger unique to its test (a UUID-bearing leaf), sets
/// it only around the one funnel it drives and clears it before returning,
/// so no other test's write can trip it.
enum AfterWriteFault {
    /// The `errorMessage` core reports for the injected failure (the
    /// error's display text), which the caveat splices verbatim.
    static let reason =
        "invalid argument: test fault: injected failure after write, before index commit"

    /// The caveat core renders for `name` landing under this seam.
    static func caveat(for name: String) -> String {
        a11yRender(event: .createPublishedUnindexed(name: name, reason: reason)).text
    }

    /// Run `body` with the seam armed for paths containing `trigger`.
    @MainActor
    static func during<T>(
        _ trigger: String, _ body: () async throws -> T
    ) async rethrows -> T {
        setenv("SLATE_TEST_FAULT_AFTER_WRITE", trigger, 1)
        defer { unsetenv("SLATE_TEST_FAULT_AFTER_WRITE") }
        return try await body()
    }
}
