// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// #880 — the customizable toolbar (`toolbar(id: "main")`) adoption.
///
/// SOURCE-INSPECTION tests: SwiftUI's toolbar-customization model can't be
/// exercised from XCTest (no window server in the test runner), so the
/// durable invariants are pinned against the `MainSplitView.swift` /
/// `SlateMacApp.swift` source. The customization model's hard requirement
/// is a STABLE, STATIC item set with unique ids — a conditionally-emitted
/// `ToolbarItem` corrupts the persisted layout — so that is what these lock,
/// along with the Save-in-its-own-group and order-preservation choices.
final class ToolbarCustomizationTests: XCTestCase {

    /// The seven persisted customization ids, in default (leading→trailing)
    /// order. NEVER reorder or rename — each is baked into the user's saved
    /// toolbar layout. The first two are the `.primaryAction` Save group;
    /// the rest are the `.secondaryAction` reference cluster.
    private static let orderedItemIDs = [
        "saveStatus", "save",
        "search", "template", "tasksReview", "citationSummary", "bibliography",
    ]

    // MARK: - Customizable form + stable ids

    func testToolbarUsesCustomizableIdForm() throws {
        let stripped = try normalizedSource("MainSplitView.swift")
        XCTAssertTrue(
            stripped.contains(".toolbar(id: )"),
            "the toolbar must adopt the customizable `.toolbar(id:)` form")
        XCTAssertTrue(
            stripped.contains("mainToolbar: some CustomizableToolbarContent"),
            "mainToolbar must vend CustomizableToolbarContent for toolbar(id:)")
        // The customization id itself is load-bearing: renaming it silently
        // discards every user's persisted toolbar layout. `normalizedSource`
        // blanks string literals, so this must check the RAW source for the
        // exact literal.
        let raw = try rawSource("MainSplitView.swift")
        XCTAssertTrue(
            raw.contains(".toolbar(id: \"main\")"),
            "the customization id must stay exactly \"main\" (a rename drops saved layouts)")
    }

    func testEveryItemHasAStableUniqueId() throws {
        let raw = try rawSource("MainSplitView.swift")
        for id in Self.orderedItemIDs {
            XCTAssertTrue(
                raw.contains("ToolbarItem(id: \"\(id)\""),
                "toolbar item id \"\(id)\" must be present and stable")
        }
        // Exactly seven customizable items — the stable set, no more, no fewer.
        let stripped = try normalizedSource("MainSplitView.swift")
        XCTAssertEqual(
            countOccurrences(of: "ToolbarItem(id: ", in: stripped), 7,
            "exactly seven customizable toolbar items")
    }

    func testDefaultOrderIsPreservedLeadingToTrailing() throws {
        let raw = try rawSource("MainSplitView.swift")
        let positions = try Self.orderedItemIDs.map { id -> Int in
            guard let r = raw.range(of: "ToolbarItem(id: \"\(id)\"") else {
                throw XCTSkip("id \(id) not found in source")
            }
            return raw.distance(from: raw.startIndex, to: r.lowerBound)
        }
        XCTAssertEqual(
            positions, positions.sorted(),
            "declared order must match the default leading→trailing layout")
    }

    // MARK: - Save is its own group, visually distinct

    func testSaveGroupIsPrimaryActionAndReferenceClusterIsSecondary() throws {
        let raw = try rawSource("MainSplitView.swift")
        // Save + its status are the leading .primaryAction group.
        XCTAssertTrue(
            raw.contains("ToolbarItem(id: \"saveStatus\", placement: .primaryAction)"),
            "the save-status indicator anchors the primary Save group")
        XCTAssertTrue(
            raw.contains("ToolbarItem(id: \"save\", placement: .primaryAction)"),
            "the Save button is the primary action, visually distinct (toolbars.md:69)")
        // The five reference actions are the .secondaryAction cluster.
        for id in ["search", "template", "tasksReview", "citationSummary", "bibliography"] {
            XCTAssertTrue(
                raw.contains("ToolbarItem(id: \"\(id)\", placement: .secondaryAction)"),
                "\(id) belongs to the secondary reference cluster")
        }
    }

    func testSaveGroupIsPinnedAgainstRemovalOrMove() throws {
        let raw = try rawSource("MainSplitView.swift")
        // Each Save-group item must carry .customizationBehavior(.disabled)
        // ATTACHED TO ITSELF — pinned against removal/reorder. A bare count
        // of two occurrences somewhere is not enough: the modifier must bind
        // to `saveStatus` + `save` specifically, or Save could become
        // removable while two secondary items carry the pins.
        for id in ["saveStatus", "save"] {
            let slice = try toolbarItemSlice(in: raw, id: id)
            XCTAssertTrue(
                slice.contains(".customizationBehavior(.disabled)"),
                "the \(id) item must be pinned with .customizationBehavior(.disabled)")
        }
        // The five reference actions are NOT pinned — they stay customizable.
        for id in ["search", "template", "tasksReview", "citationSummary", "bibliography"] {
            let slice = try toolbarItemSlice(in: raw, id: id)
            XCTAssertFalse(
                slice.contains(".customizationBehavior(.disabled)"),
                "the \(id) reference item must remain user-customizable (not pinned)")
        }
    }

    // MARK: - The conditional-emission gotcha (#880 critical)

    func testNoConditionalToolbarItemEmission() throws {
        let raw = try rawSource("MainSplitView.swift")
        // Conditional visibility uses the sanctioned `.hidden(_:)` modifier:
        // it removes the item from the LIVE toolbar while the stable id stays
        // in the customization palette — NOT an inner content `if`, which is
        // not a documented equivalent and risks a blank slot / empty AX stop.
        // Bound to the `saveStatus` declaration slice (not a global contains)
        // so the modifier can't drift onto a different item and false-pass.
        let saveStatusSlice = try toolbarItemSlice(in: raw, id: "saveStatus")
        XCTAssertTrue(
            saveStatusSlice.contains(".hidden(appState.loadedFilePath == nil)"),
            "the save-status item must use .hidden(_:) for conditional visibility")

        let stripped = try normalizedSource("MainSplitView.swift")
        // The pre-#880 shape wrapped the save-status ToolbarItem in
        // `if appState.loadedFilePath != nil { ToolbarItem(...) }` — a
        // conditional item set that corrupts customization persistence.
        XCTAssertFalse(
            stripped.contains("if appState.loadedFilePath != nil { ToolbarItem"),
            "no ToolbarItem may be conditionally emitted (breaks customization persistence)")
        // Stronger, formatting-robust guard: in the flat builder the only
        // `{` directly preceding a `ToolbarItem` is the builder's own opening
        // brace. Any conditional/loop wrapping an item (`if …{ ToolbarItem`,
        // `ForEach …{ ToolbarItem`) would push this above 1.
        XCTAssertEqual(
            countOccurrences(of: "{ ToolbarItem", in: stripped), 1,
            "the item set is static — no ToolbarItem nested inside a conditional or loop")
    }

    // MARK: - AX + disabled preserved verbatim (VoiceOver parity gate)

    func testSaveStatusAndSaveAccessibilityPreserved() throws {
        let raw = try rawSource("MainSplitView.swift")
        XCTAssertTrue(
            raw.contains("Modified. Unsaved changes in the editor."),
            "the Modified status label must be preserved verbatim")
        XCTAssertTrue(
            raw.contains("Saved. Editor matches the on-disk file."),
            "the Saved status label must be preserved verbatim")
        XCTAssertTrue(
            raw.contains("Save the current note to disk. Command-S."),
            "the Save button hint must be preserved verbatim")
        // The Save button's disabled condition is unchanged.
        let stripped = try normalizedSource("MainSplitView.swift")
        XCTAssertTrue(
            stripped.contains(
                "appState.loadedFilePath == nil || appState.isSaving || !appState.hasUnsavedChanges"),
            "the Save button's disabled condition must be preserved verbatim")
    }

    // MARK: - The customization editor is reachable

    func testToolbarCommandsWired() throws {
        let stripped = try normalizedSource("SlateMacApp.swift")
        XCTAssertTrue(
            stripped.contains("ToolbarCommands()"),
            "ToolbarCommands() surfaces the Customize Toolbar… editor + Control-click menu on macOS")
    }

    // MARK: - Source helpers

    /// Raw `Sources/SlateMac/<name>` text (comments + strings intact) for
    /// assertions on exact id/label string literals.
    private func rawSource(_ name: String) throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent("Sources/SlateMac/\(name)")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        throw XCTSkip("\(name) not found relative to the test file")
    }

    /// Comment- and string-stripped source with whitespace runs collapsed —
    /// structural assertions can't false-match a pattern inside a comment or
    /// a string literal.
    private func normalizedSource(_ name: String) throws -> String {
        let raw = try rawSource(name)
        let stripped = SwiftSourceStripping.strippingCommentsAndStrings(raw)
        return stripped.split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")
    }

    /// The RAW-source slice of a single ToolbarItem declaration: from its
    /// `ToolbarItem(id: "<id>",` anchor up to the next `ToolbarItem(id: "`
    /// (or end of file). Trailing item modifiers (`.customizationBehavior`,
    /// `.hidden`) that bind to THIS item live in this range, so the slice is
    /// what proves a modifier is attached to a specific item. The `,` in the
    /// anchor disambiguates `"save"` from the `"saveStatus"` prefix.
    private func toolbarItemSlice(in raw: String, id: String) throws -> String {
        guard let start = raw.range(of: "ToolbarItem(id: \"\(id)\",") else {
            throw XCTSkip("ToolbarItem id \(id) not found in source")
        }
        let rest = raw[start.upperBound...]
        if let next = rest.range(of: "ToolbarItem(id: \"") {
            return String(raw[start.lowerBound..<next.lowerBound])
        }
        return String(raw[start.lowerBound...])
    }

    private func countOccurrences(of needle: String, in haystack: String) -> Int {
        guard !needle.isEmpty else { return 0 }
        var count = 0
        var idx = haystack.startIndex
        while let r = haystack.range(of: needle, range: idx..<haystack.endIndex) {
            count += 1
            idx = r.upperBound
        }
        return count
    }
}
