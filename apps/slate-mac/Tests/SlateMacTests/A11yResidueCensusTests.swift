// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// W0.5-3 residue census: the source-level enforcement behind the module
/// docs' claims (`slate_core::a11y` and `AnnouncementPosting.swift`).
/// Residue — copy composed by a dedicated engine and posted verbatim via
/// `.hostComposed` — is tolerated but never invisible: every site carries
/// an adjacent `// W0.5-3 residue:` marker naming its owner, the total is
/// pinned so it shrinks DELIBERATELY (engine-level vocabularies are
/// follow-on batches), and no interaction site may bypass the vocabulary
/// by reaching the string primitive directly.
final class A11yResidueCensusTests: XCTestCase {

    /// The deliberate residue count. Growing it means adding a marker that
    /// names the owning engine AND raising this number in the same commit —
    /// the diff is the reviewable claim that the copy cannot be a
    /// vocabulary template yet. Shrinking it (converting residue) is the
    /// intended direction.
    private static let pinnedResidueSites = 49

    private struct SwiftSource {
        let path: String
        let lines: [String]
    }

    /// All production sources (`Sources/SlateMac`), comment-only lines
    /// dropped so prose mentioning the APIs cannot trip the census.
    private static func productionSources() throws -> [SwiftSource] {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .appendingPathComponent("Sources/SlateMac")
        guard
            let enumerator = FileManager.default.enumerator(
                at: root, includingPropertiesForKeys: nil)
        else {
            XCTFail("cannot enumerate \(root.path)")
            return []
        }
        var sources: [SwiftSource] = []
        for case let url as URL in enumerator where url.pathExtension == "swift" {
            let text = try String(contentsOf: url, encoding: .utf8)
            let lines = text.split(separator: "\n", omittingEmptySubsequences: false)
                .map { line -> String in
                    line.trimmingCharacters(in: .whitespaces).hasPrefix("//")
                        ? "" : String(line)
                }
            sources.append(
                SwiftSource(path: url.lastPathComponent, lines: lines))
        }
        XCTAssertFalse(sources.isEmpty, "no Swift sources found under \(root.path)")
        return sources
    }

    /// Marker lines are comments, so they need the RAW text (the comment
    /// stripper above blanks them). Count both populations from the same
    /// walk to keep the census one source of truth.
    func testEveryHostComposedSiteCarriesAResidueMarkerAndTheTotalIsPinned() throws {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/SlateMac")
        guard
            let enumerator = FileManager.default.enumerator(
                at: root, includingPropertiesForKeys: nil)
        else {
            return XCTFail("cannot enumerate \(root.path)")
        }
        var markers = 0
        var sites = 0
        var unmarked: [String] = []
        for case let url as URL in enumerator where url.pathExtension == "swift" {
            let lines = try String(contentsOf: url, encoding: .utf8)
                .split(separator: "\n", omittingEmptySubsequences: false)
                .map(String.init)
            for (index, line) in lines.enumerated() {
                if line.contains("W0.5-3 residue:") {
                    markers += 1
                    continue
                }
                guard line.contains(".hostComposed(") else { continue }
                sites += 1
                let window = lines[max(0, index - 3)...index]
                if !window.contains(where: { $0.contains("W0.5-3 residue:") }) {
                    unmarked.append("\(url.lastPathComponent):\(index + 1)")
                }
            }
        }
        XCTAssertTrue(
            unmarked.isEmpty,
            "every .hostComposed site needs an adjacent `// W0.5-3 residue: <owner>` "
                + "marker (within the 3 preceding lines); missing at: \(unmarked)")
        XCTAssertEqual(
            sites, Self.pinnedResidueSites,
            "the residue census changed — update pinnedResidueSites in the same "
                + "commit and justify the delta (new residue names its owning engine; "
                + "conversions shrink the number)")
        XCTAssertEqual(
            markers, Self.pinnedResidueSites,
            "markers and .hostComposed sites must stay 1:1 — a stray or orphaned "
                + "`W0.5-3 residue:` marker is a census lie")
    }

    /// The string primitive (`postAccessibilityAnnouncement(_:priority:)`)
    /// belongs to the poster layer alone. A call whose TOP-LEVEL argument
    /// list carries a `priority:` label is that primitive (the label inside
    /// `.hostComposed(text:priority:)` sits one paren deeper); interaction
    /// sites must post typed events so core owns every template — the
    /// compiler already forces single-argument calls onto the event
    /// overload, so this closes the only textual bypass.
    func testNoInteractionSiteCallsTheStringPrimitiveDirectly() throws {
        for file in try Self.productionSources() {
            // The poster layer is the primitive's home: the AppKit poster and
            // the event overload both render into it there.
            if file.path == "AnnouncementPosting.swift" { continue }
            let source = file.lines.joined(separator: "\n")
            var violations: [String] = []
            var searchStart = source.startIndex
            while let call = source.range(
                of: "postAccessibilityAnnouncement(",
                range: searchStart..<source.endIndex)
            {
                searchStart = call.upperBound
                var depth = 1
                var cursor = call.upperBound
                var topLevel = ""
                while cursor < source.endIndex, depth > 0 {
                    let character = source[cursor]
                    if character == "(" { depth += 1 }
                    if character == ")" { depth -= 1 }
                    if depth >= 1 { topLevel.append(depth == 1 ? character : "_") }
                    cursor = source.index(after: cursor)
                }
                if topLevel.contains("priority:") {
                    violations.append(String(topLevel.prefix(80)))
                }
            }
            XCTAssertTrue(
                violations.isEmpty,
                "\(file.path) calls the string primitive directly; post a typed "
                    + "event (or .hostComposed with a residue marker) instead: "
                    + "\(violations)")
        }
    }
}
