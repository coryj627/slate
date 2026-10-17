// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// #1412: every keyboard focus move inside the vault-search overlay is spoken
/// exactly once, by VoiceOver's own read of the newly focused row.
///
/// The defect, traced in the 2026-10-16 VoiceOver pass:
/// - The overlay announced its result summary from
///   `.onReceive($searchSummary.removeDuplicates())`.
/// - In the running app SwiftUI re-subscribed that publisher on every key and
///   focus event, and `@Published` hands each new subscriber its current value.
/// - So every arrow or Tab move re-posted "Search returned N results." (twice).
///   That drowned both VoiceOver's read of the new row and the overlay's own
///   "Selected: <file>".
///
/// With the replay gone, VoiceOver read every move by itself. Posting
/// `RowSelected` / `RecentSearchFocused` as well then spoke the row twice or
/// cut the read short. So the summary is announced only when it changes, and a
/// focus move posts nothing; contract 29 SD-1's note records the choice.
@MainActor
final class SearchOverlayVoiceTests: XCTestCase {
    final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
        var posts: [String] = []
        func post(_ message: String, priority: AnnouncementPriority) {
            posts.append(message)
        }
    }

    /// The root cause, pinned on a hosted overlay. A new subscription to the
    /// summary receives its current value, and the announcement must not
    /// follow it. This test makes the new subscription by mounting a fresh
    /// overlay over unchanged results, as SwiftUI's rebuild did in the app.
    /// Re-renders and focus changes must not re-announce either, while a
    /// summary that changes is still announced, once.
    func testTheSummaryIsAnnouncedOnlyWhenItChanges() async throws {
        let recorder = RecordingAnnouncer()
        let (state, root) = try await openVault(
            files: [
                "a.md": "# A\n\nfindme one\n",
                "b.md": "# B\n\nfindme two\n",
                "c.md": "# C\n\nfindme three\n",
            ],
            announcer: recorder)
        defer { try? FileManager.default.removeItem(at: root) }
        var hosted = host(state)
        defer { hosted.close() }

        state.toggleSearchOverlay()
        await pump(0.3)
        state.searchQuery = "findme"
        state.bumpSearchQuery()
        await waitUntil { summaryPosts(recorder).count == 1 }
        XCTAssertEqual(summaryPosts(recorder), ["Search returned 3 results."])

        // Re-render without changing the summary: observed state changes,
        // and keyboard focus leaving the field and coming back.
        for _ in 0..<3 {
            state.objectWillChange.send()
            await pump(0.1)
        }
        hosted.window.makeFirstResponder(nil)
        await pump(0.2)
        hosted.window.makeFirstResponder(hosted.view)
        await pump(0.2)

        // A new subscription over the same, unchanged summary: a fresh
        // overlay mounted while the results stay up (its appear re-arms the
        // search, which publishes the same summary again).
        hosted.close()
        hosted = host(state)
        await pump(0.6)
        XCTAssertEqual(
            summaryPosts(recorder), ["Search returned 3 results."],
            "a re-render, a focus change or a new subscription must not re-announce "
                + "an unchanged summary (#1412)")
        XCTAssertEqual(
            recorder.posts.filter { $0.hasPrefix("Selected:") || $0.hasPrefix("Recent search:") },
            [], "a focus change posts nothing of its own; VoiceOver reads the row")

        // A summary that changes is still announced, once.
        state.searchQuery = "findme two"
        state.bumpSearchQuery()
        await waitUntil { summaryPosts(recorder).count == 2 }
        await pump(0.3)
        XCTAssertEqual(
            summaryPosts(recorder), ["Search returned 3 results.", "Search returned 1 result."])
    }

    /// A focus move has no announcement of its own. VoiceOver reads the
    /// focused row itself, so `RowSelected` or `RecentSearchFocused` posted
    /// as well would speak the row twice (VoiceOver pass 2026-10-16). Rows
    /// take keyboard focus only with Keyboard navigation on (#1377), which
    /// the test runner does not have, so the absence is pinned at the source:
    /// the overlay constructs neither event.
    func testAFocusMoveHasNoAnnouncementOfItsOwn() throws {
        let source = try String(
            contentsOf: Self.sourcesRoot.appendingPathComponent("SearchOverlay.swift"),
            encoding: .utf8)
        let code = SwiftSourceStripping.strippingCommentsAndStrings(source)
        XCTAssertFalse(code.contains(".rowSelected("), "the overlay must not post RowSelected")
        XCTAssertFalse(
            code.contains(".recentSearchFocused("),
            "the overlay must not post RecentSearchFocused")
    }

    /// What VoiceOver reads when a result row takes focus: the file name and
    /// the snippet, with the hit markers stripped. The row's identity is in it.
    func testAResultRowReadsItsFileNameAndSnippet() {
        let hit = QueryHit(
            path: "daily/2026-05-28.md", snippet: "…the \u{2}xyzzyplover\u{3} triple-hit…",
            score: 1)
        XCTAssertEqual(
            SearchOverlay.rowAccessibilityLabel(for: hit),
            "2026-05-28.md: …the xyzzyplover triple-hit…")
    }

    // MARK: - Fixtures

    private static let hostApplication: NSApplication = {
        let application = NSApplication.shared
        _ = application.setActivationPolicy(.accessory)
        return application
    }()

    private static var sourcesRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .appendingPathComponent("Sources/SlateMac")
    }

    private func summaryPosts(_ recorder: RecordingAnnouncer) -> [String] {
        recorder.posts.filter { $0.hasPrefix("Search returned") }
    }

    private func openVault(
        files: [String: String], announcer: AnnouncementPosting
    ) async throws -> (AppState, URL) {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-search-voice-\(UUID().uuidString)")
        let vault = root.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for (name, body) in files {
            try Data(body.utf8).write(to: vault.appendingPathComponent(name))
        }
        let store = RecentVaultsStore(fileURL: root.appendingPathComponent("recents.json"))
        let state = AppState(
            recentsStore: store, externalOpener: { _ in true }, announcer: announcer)
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, root)
    }

    private struct Hosted {
        let window: NSWindow
        let view: NSView

        @MainActor func close() {
            window.orderOut(nil)
            window.contentView = nil
            window.close()
        }
    }

    /// The overlay in a real window, the way `MainSplitView` lays it.
    private func host(_ state: AppState) -> Hosted {
        _ = Self.hostApplication
        let view = NSHostingView(rootView: AnyView(SearchOverlay().environmentObject(state)))
        view.frame = NSRect(x: 0, y: 0, width: 600, height: 420)
        let window = NSWindow(
            contentRect: view.frame, styleMask: [.titled], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        window.contentView = view
        window.orderFrontRegardless()
        return Hosted(window: window, view: view)
    }

    /// Run the main run loop, yielding so main-actor work (the search's
    /// publication) gets its turn.
    private func pump(_ seconds: TimeInterval) async {
        let deadline = Date(timeIntervalSinceNow: seconds)
        while Date() < deadline {
            spinRunLoop(0.02)
            await Task.yield()
        }
    }

    private func spinRunLoop(_ seconds: TimeInterval) {
        RunLoop.main.run(until: Date(timeIntervalSinceNow: seconds))
    }

    private func waitUntil(
        timeout: TimeInterval = 5, file: StaticString = #filePath, line: UInt = #line,
        _ condition: () -> Bool
    ) async {
        let deadline = Date(timeIntervalSinceNow: timeout)
        while !condition(), Date() < deadline {
            await pump(0.05)
        }
        XCTAssertTrue(condition(), "condition never held", file: file, line: line)
    }
}
