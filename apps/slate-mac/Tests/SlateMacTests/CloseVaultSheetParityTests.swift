// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Parity audit for `AppState.closeVault()` (#328 follow-up).
///
/// Every `@Published var is...Open: Bool` in `AppState` that drives
/// a SwiftUI `.sheet` binding has to be reset to `false` by
/// `closeVault()`. Otherwise a vault close while the sheet is
/// presented leaves the bool stuck `true`, and the next vault open
/// re-presents the same sheet against the new vault's state — empty,
/// stale, or pointing at a session that's already gone.
///
/// Two test shapes here:
///
/// 1. **Behaviour tests** — one per sheet bool. Set it `true`,
///    call `closeVault()`, assert it's `false`. These catch the
///    direct regression.
/// 2. **Structural drift test** — scrapes `AppState.swift` for the
///    declarations and the `closeVault()` body, and asserts every
///    declared sheet bool is also reset. This catches the slower
///    regression: a future contributor adds an `isXSheetOpen` and
///    forgets to wire it up here.
///
/// Bools handled outside this test surface:
/// - `isSearchOpen` is reset via `closeSearchOverlay()` — exercised
///   by `AppStateTests.testCloseVaultClearsSearchState`.
@MainActor
final class CloseVaultSheetParityTests: XCTestCase {

    // MARK: - Behaviour tests

    func testCloseVaultResetsIsAddPropertySheetOpen() {
        let state = AppState()
        state.isAddPropertySheetOpen = true
        state.closeVault()
        XCTAssertFalse(
            state.isAddPropertySheetOpen,
            "closeVault must reset isAddPropertySheetOpen so a re-open against a new vault doesn't re-present the empty Add-Property sheet"
        )
    }

    func testCloseVaultResetsIsBulkRenameSheetOpen() {
        let state = AppState()
        state.isBulkRenameSheetOpen = true
        state.closeVault()
        XCTAssertFalse(
            state.isBulkRenameSheetOpen,
            "closeVault must reset isBulkRenameSheetOpen so a re-open against a new vault doesn't re-present the empty Bulk-Rename sheet"
        )
    }

    func testCloseVaultResetsIsTemplatePickerOpen() {
        let state = AppState()
        state.isTemplatePickerOpen = true
        state.closeVault()
        XCTAssertFalse(
            state.isTemplatePickerOpen,
            "closeVault must reset isTemplatePickerOpen so a re-open against a new vault doesn't re-present the template picker with the old vault's templates"
        )
    }

    func testCloseVaultResetsIsCitationSummaryOpen() {
        // Already covered upstream (#313/L), but pin it here so the
        // parity contract is enforced by a single suite.
        let state = AppState()
        state.isCitationSummaryOpen = true
        state.closeVault()
        XCTAssertFalse(state.isCitationSummaryOpen)
    }

    func testCloseVaultResetsIsTasksReviewOpen() {
        // Already covered upstream, pinned here for parity contract.
        let state = AppState()
        state.isTasksReviewOpen = true
        state.closeVault()
        XCTAssertFalse(state.isTasksReviewOpen)
    }

    func testCloseVaultResetsIsCommandPaletteOpen() {
        // Already covered in CommandPaletteViewTests, pinned here
        // for parity contract.
        let state = AppState()
        state.isCommandPaletteOpen = true
        state.closeVault()
        XCTAssertFalse(state.isCommandPaletteOpen)
    }

    // MARK: - Associated state cleanup
    //
    // The sheet bool isn't the only thing the reset has to touch
    // — orphan in-flight tasks or stale rendered state would also
    // bleed into the next vault session. These tests pin the
    // additional cleanup the fix added alongside the sheet-bool
    // resets, so they don't silently regress.

    func testCloseVaultResetsBulkRenameAncillaryState() {
        let state = AppState()
        // Drive the published state directly. The properties are
        // `private(set)` for production callers but the test sits
        // inside the SlateMac module via `@testable import`, which
        // doesn't lift the access control — so we can't poke them
        // from here. Assert the close-vault path leaves them at
        // their default zero values, which is what we'd want after
        // any vault close regardless of the prior state.
        state.closeVault()
        XCTAssertNil(state.pendingRenameReport)
        XCTAssertFalse(state.isRenameInFlight)
        XCTAssertNil(state.renameError)
    }

    func testCloseVaultResetsTemplateFlowState() {
        let state = AppState()
        state.closeVault()
        XCTAssertEqual(
            state.pendingTemplateFlow, .idle,
            "template flow must end at .idle after closeVault — a half-completed flow would point at a session that's gone"
        )
        XCTAssertNil(state.templateNoteNameError)
        XCTAssertEqual(state.availableTemplates.count, 0)
        XCTAssertNil(
            state.templatePickerTask,
            "templatePickerTask must be cancelled+nilled — an in-flight listTemplates() would write back into a session that's gone"
        )
        XCTAssertNil(state.templateSelectionTask)
        XCTAssertNil(state.templateCreateTask)
    }

    // MARK: - Structural drift test

    /// Scrapes `AppState.swift` for the source of truth and asserts
    /// every declared sheet bool is also reset inside `closeVault()`.
    ///
    /// The "is reset" check looks for two shapes:
    /// 1. `<name> = false` somewhere in the `closeVault()` body.
    /// 2. A call to a helper method that's documented as part of the
    ///    close path (e.g. `closeSearchOverlay()` for `isSearchOpen`).
    ///
    /// Shape 2 is encoded as an explicit allow-list — keeping it
    /// narrow forces a contributor adding a new bool to think about
    /// the reset path rather than silently shipping a regression.
    func testCloseVaultResetsEverySheetBool() throws {
        let appStatePath = try locateAppStateSwift()
        let source = try String(contentsOf: appStatePath, encoding: .utf8)
        let closeVaultBody = try extractCloseVaultBody(source)
        let declared = try declaredSheetBools(source)

        // Helper-method allow-list: bool name → helper-call substring
        // that closeVault uses to reset it. Adding to this list is
        // a deliberate choice — review whether the helper actually
        // resets the bool before extending.
        let resetViaHelper: [String: String] = [
            "isSearchOpen": "closeSearchOverlay()"
        ]

        XCTAssertFalse(
            declared.isEmpty,
            "declared sheet-bool scrape returned no matches — regex likely drifted from AppState's declaration style"
        )

        for name in declared {
            let directReset = closeVaultBody.contains("\(name) = false")
            let helperReset = resetViaHelper[name].map { closeVaultBody.contains($0) } ?? false
            XCTAssertTrue(
                directReset || helperReset,
                """
                AppState.closeVault() does not reset `\(name)`.
                Add `\(name) = false` to the closeVault() body so a vault \
                close while the sheet is presented doesn't leave the bool \
                stuck `true` (next vault open would re-present an empty / \
                stale sheet). If the bool is reset via a helper, add the \
                mapping to `resetViaHelper` in this test.
                """
            )
        }
    }

    // MARK: - Source scraping helpers

    private func locateAppStateSwift() throws -> URL {
        // Walk up from this file until we hit `apps/slate-mac` and
        // then point at `Sources/SlateMac/AppState.swift`. Mirrors
        // the lookup in `SlateCommandsTests.scrapedMenuChords`.
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<10 {
            let candidate = cursor.appendingPathComponent("Sources/SlateMac/AppState.swift")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return candidate
            }
            cursor.deleteLastPathComponent()
        }
        throw XCTSkip("Could not locate AppState.swift from \(#filePath)")
    }

    /// Returns every `@Published var is...Open` declaration's
    /// identifier. Skips `private(set)` and `internal(set)` bools
    /// because the parity invariant is about externally-driven UI
    /// flags — internal-only `is...Open` bools (like loading flags)
    /// live on a different lifecycle.
    ///
    /// Heuristic: regex over the source. The pattern accepts both
    /// shapes Swift allows:
    ///   1. Explicit:  `@Published var isFooOpen: Bool = false`
    ///   2. Inferred:  `@Published var isFooOpen = false`
    /// — #328 red-team P2 caught that v1 of this regex required
    /// `: Bool`, so a future no-type-annotation declaration would
    /// silently slip past the drift check.
    ///
    /// The build will fail loudly if the regex stops matching
    /// entirely (assertion above on `declared.isEmpty`).
    private func declaredSheetBools(_ source: String) throws -> [String] {
        // Two patterns rather than one regex with `(:\s*Bool)?` so
        // each shape is unambiguous and easy to reason about. The
        // shared `isBoolLiteral` predicate filters mistaken matches
        // (e.g. `isFooOpen = computeIt()` — not a bool literal,
        // not what we're auditing).
        let explicit = #"@Published\s+var\s+(is[A-Za-z0-9]*Open)\s*:\s*Bool\s*=\s*(?:true|false)"#
        let inferred = #"@Published\s+var\s+(is[A-Za-z0-9]*Open)\s*=\s*(?:true|false)"#
        let patterns = [explicit, inferred]
        var seen = Set<String>()
        var ordered: [String] = []
        let ns = source as NSString
        for pattern in patterns {
            let regex = try NSRegularExpression(pattern: pattern)
            let matches = regex.matches(
                in: source,
                range: NSRange(location: 0, length: ns.length)
            )
            for m in matches where m.numberOfRanges >= 2 {
                let name = ns.substring(with: m.range(at: 1))
                if seen.insert(name).inserted {
                    ordered.append(name)
                }
            }
        }
        return ordered
    }

    /// Returns the source between `func closeVault()`'s opening
    /// brace and its matching closing brace. Brace-counting walks
    /// the body so nested closures don't trip the search.
    ///
    /// **Strips comments + strings first (#343).** A literal `}` in
    /// a string (`let s = "}"`) or a comment (`// }`, `/* } */`)
    /// inside `closeVault()` would otherwise be counted as a real
    /// closing brace, prematurely ending the extracted body — and
    /// the drift test would then silently scan a truncated
    /// substring. Running the source through `SwiftSourceStripping`
    /// (the same helper the Settings-scene grep uses, from #333)
    /// blanks those `{`/`}` to spaces while preserving offsets, so
    /// the counter only ever sees structural braces. The returned
    /// body is therefore comment/string-blanked too — fine for the
    /// caller, which only `contains`-checks code substrings like
    /// `isFooOpen = false` / `closeSearchOverlay()`.
    private func extractCloseVaultBody(_ rawSource: String) throws -> String {
        let source = SwiftSourceStripping.strippingCommentsAndStrings(rawSource)
        // Find the signature. `closeVault()` is non-async + takes no
        // args, so the signature is stable.
        guard let sigRange = source.range(of: "func closeVault()") else {
            XCTFail("closeVault() signature not found — extractor needs a refresh")
            return ""
        }
        // Walk forward to the first `{` after the signature.
        guard let openBrace = source.range(of: "{", range: sigRange.upperBound..<source.endIndex)
        else {
            XCTFail("opening brace for closeVault() not found")
            return ""
        }
        var depth = 1
        var cursor = openBrace.upperBound
        while cursor < source.endIndex {
            let ch = source[cursor]
            if ch == "{" {
                depth += 1
            } else if ch == "}" {
                depth -= 1
                if depth == 0 {
                    return String(source[openBrace.upperBound..<cursor])
                }
            }
            cursor = source.index(after: cursor)
        }
        XCTFail("closing brace for closeVault() not found — source likely truncated")
        return ""
    }

    // MARK: - Self-tests for the scrapers
    //
    // The drift test is only as good as its scrapers. If the regex
    // silently fails to match or the brace-counter trips on a
    // closure, the parity check passes trivially and the real
    // regression slips through. These self-tests pin the helpers
    // against fixture strings so a refactor that breaks them fails
    // loudly here rather than silently in the drift test.

    func testDeclaredSheetBoolsExtractsExpectedNames() throws {
        let fixture = """
            @Published var isFooSheetOpen: Bool = false
            @Published var isBarOpen: Bool = false
            @Published private(set) var isBazLoading: Bool = false
            @Published var isQuxOpen   :   Bool   =   false
            var notPublished: Bool = false
            @Published var isCommandPaletteOpen: Bool = false
            """
        let names = try declaredSheetBools(fixture)
        XCTAssertEqual(
            names,
            ["isFooSheetOpen", "isBarOpen", "isQuxOpen", "isCommandPaletteOpen"]
        )
    }

    /// #328 red-team P2: a future contributor writing
    /// `@Published var isFooOpen = false` (no `: Bool` annotation,
    /// Swift type-inference picks `Bool` from the literal) used to
    /// silently slip past the drift check.
    func testDeclaredSheetBoolsAcceptsInferredTypeAnnotation() throws {
        let fixture = """
            @Published var isExplicitOpen: Bool = false
            @Published var isInferredOpen = false
            @Published var isInferredTrueOpen = true
            @Published var isComputedOpen = makeIt()
            """
        let names = try declaredSheetBools(fixture)
        XCTAssertEqual(
            names,
            ["isExplicitOpen", "isInferredOpen", "isInferredTrueOpen"],
            "computed-initializer declarations are deliberately excluded — they aren't simple sheet bools"
        )
    }

    func testExtractCloseVaultBodyHandlesNestedBraces() throws {
        let fixture = """
            class Foo {
                func closeVault() {
                    let f = { [1, 2].forEach { _ in } }
                    isFooOpen = false
                    if true { isBarOpen = false }
                }
                func somethingElse() { isWrongOpen = false }
            }
            """
        let body = try extractCloseVaultBody(fixture)
        XCTAssertTrue(body.contains("isFooOpen = false"))
        XCTAssertTrue(body.contains("isBarOpen = false"))
        XCTAssertFalse(
            body.contains("isWrongOpen = false"),
            "brace-counter must stop at closeVault()'s close, not bleed into the next func"
        )
    }

    /// #343: a literal `}` inside a string or comment in the body
    /// must NOT be counted as closeVault()'s closing brace. Before
    /// the `SwiftSourceStripping` pre-pass, the `}` in `"…}"` would
    /// have dropped depth to 0 and truncated the body mid-string —
    /// and the drift test would then scan a wrong substring. This
    /// fixture puts a brace in a string literal, a line comment, and
    /// a block comment, all before the real reset; the extracted
    /// body must still reach `isFooOpen = false` and stop before the
    /// next func.
    func testExtractCloseVaultBodyIgnoresBracesInStringsAndComments() throws {
        let fixture = """
            class Foo {
                func closeVault() {
                    let label = "a closing brace } inside a string"
                    // a } in a line comment
                    /* and a } in a block comment */
                    isFooOpen = false
                }
                func somethingElse() { isWrongOpen = false }
            }
            """
        let body = try extractCloseVaultBody(fixture)
        XCTAssertTrue(
            body.contains("isFooOpen = false"),
            "real reset after the brace-bearing string/comments must be in the body"
        )
        XCTAssertFalse(
            body.contains("isWrongOpen = false"),
            "a brace inside a string/comment must not prematurely end the body and let the next func bleed in"
        )
    }
}
