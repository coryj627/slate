// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// The batch-trash retry announcement, branch by branch, singular and plural.
///
/// This copy was unpinned by any test, which is how the mixed-outcome branch
/// came to name the noun for the present half but not the absent half —
/// "3 items are still in the vault and writable; 1 is no longer in the vault."
/// The elision is only recoverable from the earlier clause, which is a poor
/// bet for someone hearing the sentence once through VoiceOver.
///
/// Asserting whole strings (rather than `contains`) is deliberate: agreement
/// bugs live in the join between clauses, which a substring check walks past.
final class BatchTrashRetryAnnouncementTests: XCTestCase {

    private func announcement(present: Int, absent: Int, unresolved: Int) -> String {
        AppState.batchTrashRetryAnnouncement(
            present: present, absent: absent, unresolved: unresolved)
    }

    // MARK: - Mixed outcome — the branch that carried the defect

    func testMixedOutcomeNamesTheNounOnBothHalves() {
        XCTAssertEqual(
            announcement(present: 3, absent: 1, unresolved: 0),
            "Checked again. 3 items are still in the vault and writable; "
                + "1 item is no longer in the vault.")
        XCTAssertEqual(
            announcement(present: 1, absent: 2, unresolved: 0),
            "Checked again. 1 item is still in the vault and writable; "
                + "2 items are no longer in the vault.")
    }

    /// Both halves singular at once — the case where a shared plural noun or a
    /// shared verb would read wrong on one side.
    func testMixedOutcomeAgreesEachHalfIndependently() {
        XCTAssertEqual(
            announcement(present: 1, absent: 1, unresolved: 0),
            "Checked again. 1 item is still in the vault and writable; "
                + "1 item is no longer in the vault.")
    }

    // MARK: - Single-outcome branches

    func testAllStillPresent() {
        XCTAssertEqual(
            announcement(present: 1, absent: 0, unresolved: 0),
            "Checked again. The item is still in the vault and writable.")
        XCTAssertEqual(
            announcement(present: 4, absent: 0, unresolved: 0),
            "Checked again. The items are still in the vault and writable.")
    }

    func testAllAbsent() {
        XCTAssertEqual(
            announcement(present: 0, absent: 1, unresolved: 0),
            "Checked again. The item is no longer in the vault.")
        XCTAssertEqual(
            announcement(present: 0, absent: 3, unresolved: 0),
            "Checked again. The items are no longer in the vault.")
    }

    // MARK: - Unresolved wins outright

    /// Any unresolved item short-circuits: the user is told verification
    /// failed rather than given a present/absent tally they cannot trust.
    func testUnresolvedShortCircuitsAndAgreesCountWithPronoun() {
        XCTAssertEqual(
            announcement(present: 0, absent: 0, unresolved: 1),
            "Slate still couldn’t verify whether 1 item moved to Trash. "
                + "It remains read-only.")
        XCTAssertEqual(
            announcement(present: 0, absent: 0, unresolved: 2),
            "Slate still couldn’t verify whether 2 items moved to Trash. "
                + "They remain read-only.")
    }

    func testUnresolvedTakesPrecedenceOverResolvedCounts() {
        XCTAssertEqual(
            announcement(present: 5, absent: 5, unresolved: 1),
            "Slate still couldn’t verify whether 1 item moved to Trash. "
                + "It remains read-only.")
    }

    // MARK: - Agreement is never hard-coded

    /// Zero is plural everywhere in this project (see `CountCopyTests`), so a
    /// count that reaches these strings as zero must not read "0 item".
    func testZeroStaysPlural() {
        XCTAssertTrue(
            announcement(present: 0, absent: 0, unresolved: 0)
                .contains("The items are"),
            "zero must take the plural, matching CountCopy's zero-is-plural rule")
    }
}
