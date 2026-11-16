// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

/// Plan 43 PR 29 §3 (W§3): a buffer's baseline hash and its file identity
/// always describe one file version, so the host never pairs a hash with a
/// separately read identity. Every statement that assigns a note buffer's
/// content hash assigns its `BaselineIdentity` beside it, from the same core
/// result, and nothing in the app reads `noteFileIdentity`.
final class HotExitIdentityCensusTests: XCTestCase {

    /// How far below a hash assignment its identity must be assigned.
    private static let window = 12

    private func sources() throws -> [(name: String, lines: [String])] {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .appendingPathComponent("Sources/SlateMac")
        let enumerator = try XCTUnwrap(FileManager.default.enumerator(at: root, includingPropertiesForKeys: nil))
        var files: [(String, [String])] = []
        for case let url as URL in enumerator
        where url.pathExtension == "swift" && url.lastPathComponent != "slate_uniffi.swift" {
            let text = try String(contentsOf: url, encoding: .utf8)
            files.append((url.lastPathComponent, text.components(separatedBy: "\n")))
        }
        XCTAssertGreaterThan(files.count, 50, "the census reads the app's sources")
        return files
    }

    func testHostCodeNeverPairsAHashWithASeparateIdentity() throws {
        let hash = try NSRegularExpression(
            pattern: #"^\s*(currentNoteContentHash|(doc|document|rebound|parked)\.contentHash)\s*="#)
        var checked = 0
        for (name, lines) in try sources() {
            for (index, line) in lines.enumerated() {
                let trimmed = line.trimmingCharacters(in: .whitespaces)
                if trimmed.hasPrefix("//") || trimmed.hasPrefix("///") { continue }
                XCTAssertFalse(
                    line.contains("noteFileIdentity("),
                    "\(name):\(index + 1) reads an identity apart from the read or save that gave the hash")
                let range = NSRange(line.startIndex..., in: line)
                guard let match = hash.firstMatch(in: line, range: range),
                    let ownerRange = Range(match.range(at: 1), in: line)
                else { continue }
                checked += 1
                let owner = String(line[ownerRange])
                let identity = owner == "currentNoteContentHash"
                    ? "currentNoteBaselineIdentity ="
                    : owner.replacingOccurrences(of: ".contentHash", with: ".baselineIdentity =")
                let following = lines[index..<min(lines.count, index + Self.window)]
                XCTAssertTrue(
                    following.contains { $0.contains(identity) },
                    "\(name):\(index + 1) assigns `\(owner)` without `\(identity)` beside it")
            }
        }
        XCTAssertGreaterThanOrEqual(checked, 15, "every hash site is in the census")
    }

    /// §4: every store the close prompts read for drafts that are not tab
    /// buffers bumps `hotExitDraftRevision`, so an edit to one during a
    /// settle or a Discard All's wait fails the re-check.
    func testEveryDraftStoreBumpsTheDraftRevision() throws {
        let appState = try XCTUnwrap(try sources().first { $0.name == "AppState.swift" })
        let text = appState.lines.joined(separator: "\n")
        let stores = [
            "propertiesSourceDraft", "propertiesSourceDraftPath", "parkedPropertiesSourceDrafts",
            "preservedPropertyDrafts", "propertyPublicationUncertainties",
            "currentPropertyEditConflict", "missingNoteRecoveryDrafts",
        ]
        for store in stores {
            let declaration = try XCTUnwrap(
                text.range(
                    of: #"var \#(store)\b[^\n]*"#, options: .regularExpression),
                "\(store) is declared")
            let after = text[declaration.lowerBound...].prefix(400)
            XCTAssertTrue(
                after.contains("hotExitDraftRevision &+= 1"),
                "\(store) bumps the draft revision in its observer")
        }
        // Every draft store the close prompts' aggregation reads is listed.
        for function in ["propertyRecoveryPaths", "uncommittedNoteDraftPaths", "dirtyVaultClosePaths"] {
            let start = try XCTUnwrap(text.range(of: "func \(function)("), function)
            let body = String(text[start.upperBound...].prefix(2_500))
            let end = body.range(of: "\n    }\n").map { body[..<$0.lowerBound] } ?? Substring(body)
            let regex = try NSRegularExpression(pattern: #"\b([a-z][A-Za-z]*(?:Drafts?|Uncertainties|Conflict))\b"#)
            let names = regex.matches(in: String(end), range: NSRange(end.startIndex..., in: end))
                .compactMap { Range($0.range(at: 1), in: end).map { String(end[$0]) } }
            for name in Set(names) where !name.hasSuffix("Draft") || stores.contains(name) {
                XCTAssertTrue(
                    stores.contains(name) || name.first == "u" || name.hasPrefix("discard")
                        || name.hasPrefix("has") || name.hasPrefix("recoverable"),
                    "\(function) reads \(name), not in the census list")
            }
        }
    }
}
