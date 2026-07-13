// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #862 acceptance: the Settings window uses a fixed pane width and a
/// single uniform inset (macOS settings.md — a "toolbar-based
/// multi-pane layout" with "no need to … resize it"). SwiftUI's
/// rendered layout isn't XCTest-introspectable, so alongside pinning
/// the shared `SettingsLayout` constants, the layout WIRING is pinned
/// by source inspection (the repo's established `…ByInspection`
/// pattern — see HistoryPanelTests) so the specific regressions #862
/// fixed can't silently return.
final class SettingsLayoutTests: XCTestCase {

    // MARK: - Shared constants (the single source of truth for the values)

    func testPaneWidthIsFixed() {
        XCTAssertEqual(SettingsLayout.paneWidth, 520)
    }

    func testInsetIsUniform() {
        XCTAssertEqual(SettingsLayout.inset, 20)
    }

    func testPaneMinHeightBaseline() {
        XCTAssertEqual(SettingsLayout.paneMinHeight, 400)
    }

    /// The pane width must be a positive, finite value the frame
    /// modifier can pin min == max to (guards a `0`/NaN edit).
    func testPaneWidthIsUsableFixedDimension() {
        XCTAssertGreaterThan(SettingsLayout.paneWidth, 0)
        XCTAssertTrue(SettingsLayout.paneWidth.isFinite)
    }

    // MARK: - Layout wiring (source inspection — a constant is immune to
    // a view-body regression, so the actual #862 fixes are pinned here)

    /// The SettingsView source with ALL comments dropped — the #862 doc
    /// comments quote the REMOVED code verbatim (`.padding(20)`,
    /// `.navigationTitle("History")`), so asserting on raw source would
    /// match the explanation, not real code (Codex red-team). Strips
    /// `/* … */` block comments AND `//`-to-EOL line comments (this file
    /// has no `://` string literals, so the line strip can't clip code).
    private func settingsSource() throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/SettingsView.swift")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return Self.stripComments(
                    try String(contentsOf: candidate, encoding: .utf8))
            }
            cursor = cursor.deletingLastPathComponent()
        }
        throw XCTSkip("SettingsView.swift not found relative to the test file")
    }

    static func stripComments(_ source: String) -> String {
        var s = source
        // Block comments (may span lines) — remove /* … */ spans.
        while let open = s.range(of: "/*"),
            let close = s.range(of: "*/", range: open.upperBound..<s.endIndex)
        {
            s.removeSubrange(open.lowerBound..<close.upperBound)
        }
        // Line comments — everything from // to end of line (covers both
        // full-line ///doc comments and trailing comments).
        return s.split(separator: "\n", omittingEmptySubsequences: false)
            .map { line -> String in
                if let r = line.range(of: "//") { return String(line[..<r.lowerBound]) }
                return String(line)
            }
            .joined(separator: "\n")
    }

    /// The body slice of a top-level `struct <name>` — from its
    /// declaration to the NEAREST following top-level `struct`/`enum` —
    /// so a per-tab assertion can't be satisfied (or broken) by another
    /// tab's code.
    private func structSlice(_ name: String, in source: String) -> String {
        guard let start = source.range(of: "struct \(name)") else { return "" }
        let rest = source[start.lowerBound...]
        let after = rest.dropFirst()
        let boundaries = ["\nstruct ", "\nenum "]
            .compactMap { after.range(of: $0)?.lowerBound }
        if let nearest = boundaries.min() {
            return String(rest[..<nearest])
        }
        return String(rest)
    }

    /// The brace-balanced body of `struct <name>`'s `var body` — so an
    /// assertion pins the LIVE container chain, not a substring an unused
    /// helper elsewhere could supply (Codex red-team).
    private func viewBody(_ structName: String, in source: String) -> String {
        let slice = structSlice(structName, in: source)
        guard let decl = slice.range(of: "var body: some View") else { return "" }
        let after = slice[decl.upperBound...]
        guard let open = after.firstIndex(of: "{") else { return "" }
        var depth = 0
        var i = open
        while i < after.endIndex {
            switch after[i] {
            case "{": depth += 1
            case "}":
                depth -= 1
                if depth == 0 { return String(after[open...i]) }
            default: break
            }
            i = after.index(after: i)
        }
        return String(after[open...])
    }

    /// Collapse every whitespace run to one space so a multi-line
    /// modifier chain can be matched as a contiguous sequence.
    private func flattened(_ s: String) -> String {
        s.split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")
    }

    /// SettingsView.body's OWN container chain pins a FIXED width
    /// (min == max) with the inset applied once, RIGHT AFTER the frame —
    /// asserted as one contiguous chain on the live body so a resizable
    /// revert, a moved inset, or the modifiers surviving only in an
    /// unused helper all fail (Codex red-team).
    func testContainerBodyPinsFixedWidthThenInsetContiguously() throws {
        let rawBody = viewBody("SettingsView", in: try settingsSource())
        XCTAssertFalse(rawBody.isEmpty, "found SettingsView.body")

        XCTAssertTrue(
            flattened(rawBody).contains(
                ".frame( minWidth: SettingsLayout.paneWidth, "
                    + "maxWidth: SettingsLayout.paneWidth, "
                    + "minHeight: SettingsLayout.paneMinHeight ) "
                    + ".padding(SettingsLayout.inset)"),
            "the body's container must pin min == max width and apply the "
                + "single inset immediately after — a resizable revert or a "
                + "detached inset breaks this contiguous chain")

        // Codex red-team: the chain must be a TOP-LEVEL modifier on the
        // body's root container, not nested inside a `.background { … }` /
        // `.overlay { … }` closure (which wouldn't constrain the parent).
        // The body slice opens with the root `{` (depth 1); a modifier on
        // the root sits at depth 1, a closure-nested one at depth ≥ 2.
        let anchor = try XCTUnwrap(
            rawBody.range(of: "minWidth: SettingsLayout.paneWidth"))
        var depth = 0
        for ch in rawBody[..<anchor.lowerBound] {
            if ch == "{" { depth += 1 } else if ch == "}" { depth -= 1 }
        }
        XCTAssertEqual(
            depth, 1,
            "the fixed-width frame must modify the body's ROOT container "
                + "(top level), not a Color.clear inside a .background/.overlay")
    }

    /// Exactly one window title, on the container — not the divergent
    /// per-tab `.navigationTitle("History")` #862 removed.
    func testSingleContainerTitleNoPerTabTitle() throws {
        let source = try settingsSource()
        XCTAssertEqual(
            source.components(separatedBy: ".navigationTitle(").count - 1, 1,
            "exactly one .navigationTitle in the whole file (the container's)")
        XCTAssertTrue(source.contains(".navigationTitle(\"Slate preferences\")"))
        XCTAssertFalse(
            structSlice("HistorySettingsTab", in: source).contains(".navigationTitle"),
            "the History tab must not re-introduce its own window title")
    }

    /// The Bibliography tab must not re-add ANY padding of its own — the
    /// double-inset that made the window jump width between tabs. The tab
    /// currently has zero code `.padding(` (its inner `noVaultState` uses
    /// a `.frame`, not padding), so asserting NO `.padding(` at all
    /// catches every re-drift form — `.padding(20)`, `.padding(.all, 20)`,
    /// `.padding(SettingsLayout.inset)` (Codex red-team: the literal
    /// `.padding(20)`-only check was gameable).
    func testBibliographyTabHasNoOwnPadding() throws {
        let bib = structSlice("BibliographySettingsTab", in: try settingsSource())
        XCTAssertFalse(bib.isEmpty, "found the Bibliography tab slice")
        XCTAssertFalse(
            bib.contains(".padding("),
            "the Bibliography tab must carry NO padding of its own — the "
                + "single inset lives once on the SettingsView container (#862)")
    }
}
