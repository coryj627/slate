// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// Tests for the BibliographyPanel's search filter (#280), the
/// files-citing sheet's count line and the view picker as an
/// assistive-technology client reads them (#447). Full integration of the
/// vault-wide list / unresolved segments lands in #283.
final class BibliographyPanelTests: XCTestCase {

    private func entry(
        key: String,
        title: String,
        familyNames: [String],
        year: Int? = nil,
        journal: String? = nil
    ) -> BibEntry {
        let authors = familyNames.map { Author(family: $0, given: nil) }
        return BibEntry(
            key: key,
            itemType: "article-journal",
            title: title,
            authors: authors,
            year: year.map(Int32.init),
            journal: journal,
            doi: nil,
            url: nil,
            publisher: nil,
            abstractText: nil
        )
    }

    func testEmptyQueryReturnsAllEntries() {
        let entries = [
            entry(key: "a", title: "Alpha", familyNames: ["Anderson"]),
            entry(key: "b", title: "Beta", familyNames: ["Brown"]),
        ]
        XCTAssertEqual(filterBibliographyEntries(entries, query: "").count, 2)
    }

    func testQueryFiltersByTitleSubstringCaseInsensitive() {
        let entries = [
            entry(key: "a", title: "On the Nature of Reading", familyNames: ["Smith"]),
            entry(key: "b", title: "A Survey of Surveys", familyNames: ["Jones"]),
        ]
        let hits = filterBibliographyEntries(entries, query: "READING")
        XCTAssertEqual(hits.count, 1)
        XCTAssertEqual(hits[0].key, "a")
    }

    func testQueryMatchesAuthorFamilyName() {
        let entries = [
            entry(key: "a", title: "Alpha", familyNames: ["Smith", "Jones"]),
            entry(key: "b", title: "Beta", familyNames: ["Brown"]),
        ]
        let hits = filterBibliographyEntries(entries, query: "jones")
        XCTAssertEqual(hits.count, 1)
        XCTAssertEqual(hits[0].key, "a")
    }

    func testQueryMatchesCitationKey() {
        let entries = [
            entry(key: "smith2020", title: "Alpha", familyNames: ["Smith"]),
            entry(key: "jones2019", title: "Beta", familyNames: ["Jones"]),
        ]
        let hits = filterBibliographyEntries(entries, query: "smith2020")
        XCTAssertEqual(hits.count, 1)
        XCTAssertEqual(hits[0].key, "smith2020")
    }

    func testQueryTrimsWhitespace() {
        let entries = [
            entry(key: "a", title: "Alpha", familyNames: ["Smith"])
        ]
        XCTAssertEqual(filterBibliographyEntries(entries, query: "   smith   ").count, 1)
    }

    func testQueryMatchesGivenName() {
        let entries = [
            entry(key: "a", title: "Alpha", familyNames: ["Smith"])
                .with(given: "Alice")
        ]
        XCTAssertEqual(filterBibliographyEntries(entries, query: "alice").count, 1)
    }

    // MARK: - Files citing this entry (#447, WCAG 4.1.2)

    @MainActor
    private func hostedFilesCitingSheet(_ paths: [String]) async throws -> AXElementSnapshot {
        let window = HostedAccessibilityTree.host(FilesCitingSheet(paths: paths, onClose: {}))
        defer { window.close() }
        return try await HostedAccessibilityTree.snapshot(of: window) { tree in
            tree.subtree.contains { $0.role == "AXHeading" && $0.name == "Files citing this entry" }
                && tree.subtree.contains { $0.role == "AXButton" && $0.name == "Close" }
        }
    }

    /// The count is its own traversable line under the heading, and the
    /// `.contain` container carries no name of its own: a container name
    /// competes with child traversal, so the count was heard twice or not
    /// at all depending on how the reader moved through the sheet. The
    /// plural rides `CountCopy`.
    @MainActor
    func testFilesCitingSheetSpeaksItsCountAsItsOwnLine() async throws {
        for (paths, line) in [
            (["a.md", "b.md", "c.md"], "3 files citing this entry."),
            (["a.md"], "1 file citing this entry."),
        ] {
            let tree = try await hostedFilesCitingSheet(paths)
            XCTAssertTrue(
                tree.staticTexts.contains(line),
                "the count must be a traversable line reading \"\(line)\":\n\(tree)")
            XCTAssertEqual(
                tree.elements(role: "AXGroup").compactMap(\.name).filter { !$0.isEmpty }, [],
                "no container may carry a name of its own:\n\(tree)")
        }
    }

    /// The empty arm stays the one line it was, with no count line beside it.
    @MainActor
    func testFilesCitingSheetEmptyArmIsUnchanged() async throws {
        let tree = try await hostedFilesCitingSheet([])
        XCTAssertTrue(
            tree.staticTexts.contains("No files in this vault cite this entry."),
            "the empty arm keeps its own line:\n\(tree)")
        XCTAssertFalse(
            tree.staticTexts.contains { $0.hasSuffix("citing this entry.") },
            "the empty arm carries no count line:\n\(tree)")
        XCTAssertEqual(
            tree.elements(role: "AXGroup").compactMap(\.name).filter { !$0.isEmpty }, [],
            "no container may carry a name of its own:\n\(tree)")
    }

    // MARK: - The view picker (#447, dismissed: WCAG 4.1.2)

    /// The scanner's segmented-picker rule fires on the empty visible title,
    /// but the picker's own `.accessibilityLabel` and `.accessibilityValue`
    /// reach the client: a radio group named "Bibliography view" whose value
    /// is the selected segment. These are the attributes Accessibility
    /// Inspector shows (AXDescription, AXValue), pinned so the dismissal of
    /// that finding stays true.
    @MainActor
    func testViewPickerIsNamedBibliographyViewAndValuedBySegment() async throws {
        let window = HostedAccessibilityTree.host(BibliographyPanel().environmentObject(AppState()))
        defer { window.close() }
        let entries = try await HostedAccessibilityTree.snapshot(of: window) { tree in
            !tree.elements(role: "AXRadioGroup").isEmpty
        }
        let picker = try XCTUnwrap(entries.elements(role: "AXRadioGroup").first)
        XCTAssertEqual(picker.name, "Bibliography view", "\(entries)")
        XCTAssertEqual(picker.value, "Entries", "\(entries)")
        XCTAssertEqual(
            picker.elements(role: "AXRadioButton").map(\.name), ["Entries", "Unresolved"],
            "\(entries)")

        try HostedAccessibilityTree.press(in: window) {
            $0.role == "AXRadioButton" && $0.name == "Unresolved"
        }
        let unresolved = try await HostedAccessibilityTree.snapshot(of: window) { tree in
            tree.elements(role: "AXRadioGroup").first?.value == "Unresolved"
        }
        XCTAssertEqual(
            unresolved.elements(role: "AXRadioGroup").first?.name, "Bibliography view",
            "\(unresolved)")
    }
}

extension BibEntry {
    /// Test helper that returns a copy with the given name set on
    /// the first author. The FFI types are structs so this is cheap.
    fileprivate func with(given: String) -> BibEntry {
        var copy = self
        if !copy.authors.isEmpty {
            copy.authors[0].given = given
        }
        return copy
    }
}
