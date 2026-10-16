// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// The host side of core's typed create outcome (#1123, #1140): the mac
/// twin of Windows' `CreateOutcomes.CreateReporting`. The note-create
/// funnels (the untitled note, the folder note, Duplicate, the template
/// flow, the history Restore As…) call `createExclusiveReporting` through
/// it and finish their flow against the LANDED file on a post-publish
/// failure: the bytes are real and readable (core serves reads from
/// disk), the index catches up on the next scan, and the one thing a host
/// must never do is retry under another name.
extension AppState {
    /// Run a create and separate the two landed arms from the refusal.
    /// Returns the landed arm's caveat — core's CreatePublishedUnindexed
    /// event, which the funnel speaks right after its own created sentence
    /// and through the same channel — or nil when the create committed. A
    /// refusal rethrows its `VaultError` unchanged, so `DestinationExists`
    /// keeps driving the auto-suffix and the name sheet. `displayName` is
    /// the LEAF the user sees, never a vault-relative path, so the caveat
    /// reads the same wherever it comes from.
    nonisolated static func createReporting(
        session: VaultSession, path: String, content: String, displayName: String
    ) throws -> A11yEvent? {
        switch try session.createExclusiveReporting(path: path, content: content) {
        case .committed:
            return nil
        case .publishedUnindexed(_, _, let errorMessage):
            return .createPublishedUnindexed(name: displayName, reason: errorMessage)
        }
    }

    /// A structural create's caveat, spoken on the channel its created
    /// sentence used (`postMutationAnnouncement(_:)` with a message) and
    /// recorded the same way, so it follows that sentence in order and the
    /// verbatim-string facts see both. Core owns the words and the
    /// priority; this only routes the event.
    func postMutationAnnouncement(_ event: A11yEvent) {
        recordMutationAnnouncement(a11yRender(event: event).text)
        postAccessibilityAnnouncement(event)
    }
}
