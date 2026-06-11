// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #422 (VO test F-E1): the menu-driven search entry point must work
/// from any focus location — and be safely guarded on the welcome
/// screen, where the overlay has no host (mirrors the command
/// palette's guard contract).
@MainActor
final class SearchErgonomicsTests: XCTestCase {
    private func makeState() throws -> (AppState, URL) {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-search-ergo-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let store = RecentVaultsStore(fileURL: dir.appendingPathComponent("recents.json"))
        return (AppState(recentsStore: store, externalOpener: { _ in true }), dir)
    }

    func testRequestSearchOverlayIsNoOpWithoutVault() throws {
        let (state, _) = try makeState()
        state.requestSearchOverlay()
        XCTAssertFalse(
            state.isSearchOpen,
            "no vault → search overlay must not open (welcome screen has no host)"
        )
    }

    /// Red-team F1: the reopen re-arm was dead code — the pipeline's
    /// removeDuplicates had pipeline-lifetime memory, so re-feeding
    /// the retained query after close→reopen was swallowed and the
    /// overlay showed idle over a non-empty field. This drives the
    /// full subject→debounce→runSearch path twice with the same
    /// query and pins that the second pass produces results again.
    func testReopenWithRetainedQueryReArmsSearch() async throws {
        let (state, dir) = try makeState()
        let vault = dir.appendingPathComponent("vault-rearm")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# Note\n\nfindme target text\n".utf8)
            .write(to: vault.appendingPathComponent("a.md"))
        state.openVault(at: vault)
        await state.scanTask?.value

        func awaitResults(timeoutMs: Int = 3000) async -> Bool {
            for _ in 0..<(timeoutMs / 50) {
                if case .results = state.searchState { return true }
                try? await Task.sleep(nanoseconds: 50_000_000)
            }
            return false
        }

        state.toggleSearchOverlay()
        state.searchQuery = "findme"
        state.bumpSearchQuery()
        let first = await awaitResults()
        XCTAssertTrue(first, "first search must produce results")

        state.closeSearchOverlay()
        XCTAssertEqual(state.searchQuery, "findme", "close retains the query by design")

        state.toggleSearchOverlay()
        // The overlay's onAppear re-arm path: same query, bumped again.
        state.bumpSearchQuery()
        let second = await awaitResults()
        XCTAssertTrue(
            second,
            "reopen re-arm must run the retained query again — pipeline dedup made this dead (red-team F1)"
        )
    }

    func testRequestSearchOverlayTogglesWithVaultOpen() async throws {
        let (state, dir) = try makeState()
        let vault = dir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# A\n".utf8).write(to: vault.appendingPathComponent("a.md"))
        state.openVault(at: vault)
        await state.scanTask?.value

        state.requestSearchOverlay()
        XCTAssertTrue(state.isSearchOpen, "vault open → menu entry point opens the overlay")
        state.requestSearchOverlay()
        XCTAssertFalse(state.isSearchOpen, "second invocation toggles closed")
    }
}
