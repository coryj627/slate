// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Tests for the BibliographyPanel's search filter (#280). Full
/// integration of the vault-wide list / unresolved segments lands
/// in #283.
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
            abstractText: nil,
            rawCslJson: "{}"
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
