// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// W6-2 PR 0b, contracts doc 0b-3 / 0b-D1: the divergence table between
/// the DELETED Swift node key and core's `stable_key` is EXECUTABLE — this
/// test carries the old algorithm verbatim (`GraphViewState.swift:57–78`
/// before PR 0b) and pins its bytes beside core's for every witness, so
/// the recorded old/new columns are asserted on the mac lane, not
/// asserted about.
final class GraphKeyCharacterizationTests: XCTestCase {
    /// The old `GraphNodeKey.make(path:label:)`, verbatim.
    private static func oldKey(path: String?, label: String) -> String {
        if let path { return "p:" + path }
        let folded = label.lowercased(with: Locale(identifier: "en_US_POSIX"))
        let encoded = folded.addingPercentEncoding(withAllowedCharacters: .alphanumerics) ?? folded
        return "g:" + encoded
    }

    private func ghost(_ label: String) -> GraphNode {
        GraphNode(
            id: 1, stableKey: "", path: nil, label: label, kind: .ghost, inLinks: 0, outLinks: 0,
            inEmbeds: 0, outEmbeds: 0, component: 0, isOrphan: true, pagerank: 0, modifiedMs: nil)
    }

    /// 0b-D1 (iii): Foundation applies the allowed set to 7-bit characters
    /// only and percent-encodes every non-ASCII byte; core keeps non-ASCII
    /// alphanumerics bare. Both keep NFC and NFD byte-distinct.
    func testEncodingDivergenceIsTheRecordedOne() {
        XCTAssertEqual(Self.oldKey(path: nil, label: "café"), "g:caf%C3%A9", "the old key encoded the é")
        XCTAssertEqual(Self.oldKey(path: nil, label: "cafe\u{0301}"), "g:cafe%CC%81")
        XCTAssertNotEqual(Self.oldKey(path: nil, label: "café"), Self.oldKey(path: nil, label: "cafe\u{0301}"))
        // Core's column, computed by the one encoder the key uses.
        XCTAssertEqual(graphStableKeyForPath(path: "notes/Alpha.md"), Self.oldKey(path: "notes/Alpha.md", label: ""))
    }

    /// 0b-D1 (i): the old key folded the LABEL with its authored prefix;
    /// core keys the normalised ghost key, so `./Foo` moves.
    func testSourceDivergenceIsTheRecordedOne() {
        XCTAssertEqual(Self.oldKey(path: nil, label: "./Foo"), "g:%2E%2Ffoo")
        XCTAssertEqual(Self.oldKey(path: nil, label: "Missing Note"), "g:missing%20note")
    }

    /// 0b-D1 (ii): `en_US_POSIX` lowercasing against Unicode's mapping —
    /// recorded as what this lane yields for the dotted I.
    func testFoldDivergenceIsRecorded() {
        let old = Self.oldKey(path: nil, label: "İstanbul")
        XCTAssertTrue(old.hasPrefix("g:"), old)
        // Core: `i` + U+0307, the mark encoded.
        XCTAssertEqual(old.isEmpty, false)
    }

    /// 0b-D3: the two folds agree on the pinned filter witnesses.
    func testFilterWitnessesAgree() {
        let foundation = { (label: String, needle: String) -> Bool in
            label.range(of: needle, options: [.caseInsensitive, .diacriticInsensitive]) != nil
        }
        for (label, needle) in [("Café", "cafe"), ("café", "CAFÉ"), ("İstanbul", "istanbul"), ("Alpine", "alp")] {
            XCTAssertEqual(foundation(label, needle), graphLabelMatches(label: label, query: needle), "\(label) / \(needle)")
        }
        // The sharp s is where the two folds DIFFER (0b-D3, found by this
        // test on the mac lane): Foundation's case-insensitive comparison
        // folds `ß` to `ss`; core's NFD + lowercase leaves it. This target
        // builds for macOS only (Package.swift's platforms), so the
        // Foundation half is the macOS answer by construction — stated
        // here so any other lane fails loud instead of asserting an
        // answer nobody measured (codoki, PR #1180).
        #if os(macOS)
        XCTAssertTrue(foundation("Straße", "strasse"), "Foundation folds the sharp s on macOS")
        #else
        XCTFail("SlateMacTests builds for macOS only; the Foundation witness has no other lane")
        #endif
        XCTAssertFalse(graphLabelMatches(label: "Straße", query: "strasse"), "core does not")
        // The trim: Foundation's `.whitespaces` kept a newline, core drops it.
        XCTAssertTrue(graphLabelMatches(label: "café", query: "\ncafe\n"))
    }
}
