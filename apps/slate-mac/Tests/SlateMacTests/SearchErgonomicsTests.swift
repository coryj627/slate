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

    // MARK: - Tag scope (#508)

    /// Closing the overlay must reset a tag scope back to `.vault` — a
    /// sticky invisible tag filter would silently scope the next ⌘F
    /// search to a tag the user can't see.
    func testCloseSearchOverlayResetsTagScopeToVault() throws {
        let (state, _) = try makeState()
        state.setSearchScope(.tag(name: "alpha"))
        XCTAssertEqual(state.searchScope, .tag(name: "alpha"))
        state.closeSearchOverlay()
        XCTAssertEqual(state.searchScope, .vault, "tag scope must not survive overlay close")
    }

    /// The retained-query re-arm (Cmd+F → Esc → Cmd+F) runs under
    /// `.vault` scope: closing reset the scope, so the retained query
    /// re-runs vault-wide — the invariant `testReopenWithRetainedQuery…`
    /// depends on. Guards against the scope reset regressing that flow.
    func testScopeResetsToVaultBeforeRetainedQueryReArm() throws {
        let (state, _) = try makeState()
        state.setSearchScope(.tag(name: "alpha"))
        state.searchQuery = "kept"
        state.closeSearchOverlay()
        XCTAssertEqual(state.searchScope, .vault)
        XCTAssertEqual(state.searchQuery, "kept", "close still retains the query")
    }

    /// Empty query under `.tag` scope must reach `.results` (list the
    /// tag's files), NOT short-circuit to `.idle` the way an empty
    /// vault-scope query does. Drives the real subject→debounce→FFI
    /// path against a vault with one tagged and one untagged note.
    func testEmptyQueryUnderTagScopeReachesResults() async throws {
        let (state, dir) = try makeState()
        let vault = dir.appendingPathComponent("vault-tag-empty")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("has an inline #alpha tag\n".utf8)
            .write(to: vault.appendingPathComponent("tagged.md"))
        try Data("no tags here\n".utf8)
            .write(to: vault.appendingPathComponent("plain.md"))
        state.openVault(at: vault)
        await state.scanTask?.value

        func awaitResults(timeoutMs: Int = 3000) async -> [QueryHit]? {
            for _ in 0..<(timeoutMs / 50) {
                if case .results(let rows, _) = state.searchState { return rows }
                try? await Task.sleep(nanoseconds: 50_000_000)
            }
            return nil
        }

        state.toggleSearchOverlay()
        // Empty query + tag scope: setSearchScope re-arms the debouncer.
        state.searchQuery = ""
        state.setSearchScope(.tag(name: "alpha"))
        let rows = await awaitResults()
        let paths = try XCTUnwrap(rows, "tag scope + empty query must reach .results, not .idle")
            .map { ($0.path as NSString).lastPathComponent }
        XCTAssertEqual(paths, ["tagged.md"], "only the tagged file is listed")

        // Clearing the scope drops back to vault; an empty vault-scope
        // query idles.
        state.clearSearchScope()
        for _ in 0..<20 {
            if case .idle = state.searchState { break }
            try? await Task.sleep(nanoseconds: 50_000_000)
        }
        XCTAssertEqual(state.searchScope, .vault)
        if case .idle = state.searchState {
        } else {
            XCTFail("empty query under vault scope must idle after clearing the tag scope")
        }
    }

    // MARK: - Snippet match emphasis

    /// FTS5's STX/ETX markers must become styled runs, not vanish:
    /// the visible characters carry no markers, and exactly the
    /// marked span gets an explicit font (the semibold emphasis).
    func testEmphasizedSnippetStylesMarkedSpanAndStripsMarkers() {
        let attributed = SearchOverlay.emphasizedSnippet(
            "before \u{2}match\u{3} after")
        XCTAssertEqual(
            String(attributed.characters), "before match after",
            "STX/ETX markers must not survive into the visible string")

        let styledText = attributed.runs
            .filter { $0.font != nil }
            .map { String(attributed.characters[$0.range]) }
            .joined()
        XCTAssertEqual(
            styledText, "match",
            "exactly the marked span carries the emphasis font")
    }

    /// A snippet with no markers passes through unstyled — the view-
    /// level `.caption`/`.secondary` modifiers stay in charge.
    func testEmphasizedSnippetWithoutMarkersIsUnstyled() {
        let attributed = SearchOverlay.emphasizedSnippet("plain text")
        XCTAssertEqual(String(attributed.characters), "plain text")
        XCTAssertTrue(
            attributed.runs.allSatisfy { $0.font == nil },
            "no marker, no explicit style")
    }

    /// Multiple marked terms each get their own styled run ("foo AND
    /// bar" queries mark every hit in the snippet).
    func testEmphasizedSnippetHandlesMultipleMarkedSpans() {
        let attributed = SearchOverlay.emphasizedSnippet(
            "\u{2}alpha\u{3} mid \u{2}beta\u{3}")
        XCTAssertEqual(String(attributed.characters), "alpha mid beta")
        let styled = attributed.runs
            .filter { $0.font != nil }
            .map { String(attributed.characters[$0.range]) }
        XCTAssertEqual(styled, ["alpha", "beta"])
    }
}
