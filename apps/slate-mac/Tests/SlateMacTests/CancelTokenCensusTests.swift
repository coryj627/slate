// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

/// Plan 43 PR 12 (#1324; locked 05 §4, principle 3: every method that
/// scans, indexes or queries the vault accepts a `CancelToken`). A token is
/// worth passing only if something can cancel it, so every paged or long core
/// call on mac carries the ONE token of its operation: tied to its task
/// (cancelled through `withTaskCancellationHandler`) or to its owner (a
/// document, a view, a sheet, cancelled in its teardown and replaced per
/// operation). A fresh `CancelToken()` written straight into a call is a token
/// nothing can reach.
///
/// This census walks every production source under `Sources/SlateMac` and
/// fails on such a token in any spelling — labelled (`cancel: CancelToken()`),
/// positional (`fetch(path, nil, CancelToken())`), split across lines,
/// inferred (`cancel: .init()`) or defaulted into a parameter
/// (`cancel: CancelToken = CancelToken()`) — outside the reviewed allowlist.
/// A token bound to a name (`let cancel = CancelToken()`) or stored on its
/// owner is its operation's own and is not flagged; the per-owner facts pin
/// that it is cancelled (`CanvasMediaListingCancellationTests`,
/// `BaseExecuteTokenOwnershipTests`, `ListingTokenOwnershipTests`).
///
/// It widens `BaseQueryBuilderTests`' single-file assertion
/// (`XCTAssertFalse(source.contains("cancel: CancelToken()"))`) to every
/// source, and to the positional spelling that assertion could not see: the
/// files tree's synchronous first page passed `fetcher(path, nil,
/// CancelToken())`, which #1324 named and a `cancel:` grep missed.
final class CancelTokenCensusTests: XCTestCase {

    struct Site: Hashable, CustomStringConvertible {
        /// Relative to `Sources/SlateMac`, e.g. `Bases/BaseDocument.swift`.
        let file: String
        let line: Int
        var description: String { "\(file):\(line)" }
    }

    /// The reviewed exceptions, as `file:line` and the reason. Each entry
    /// must still name a fresh-token site, exactly, or
    /// `testEveryAllowlistEntryStillNamesAFreshTokenSite` fails: an entry
    /// cannot outlive its site, and a site that moves needs its entry moved
    /// in the same commit, so the list cannot rot.
    ///
    /// Empty since plan 43 PR 12: the only candidate, the test-only helper
    /// `GraphDiagramView.settleReduceMotionForTesting`, now takes its token
    /// from the test (the spec's preferred option), so no source is excused.
    static let allowlist: [(site: String, reason: String)] = []

    // MARK: - The census

    func testNoProductionSourcePassesAFreshTokenOutsideTheAllowlist() throws {
        let census = try Self.productionCensus()
        let allowed = Set(Self.allowlist.map(\.site))
        let unexpected = census.sites.filter { !allowed.contains($0.description) }
        XCTAssertTrue(
            unexpected.isEmpty,
            "a fresh CancelToken() passed straight into a call is a token nothing "
                + "can cancel (#1324). Give the operation one token tied to its task "
                + "(withTaskCancellationHandler) or to its owner (cancelled in its "
                + "teardown), or add a reviewed `file:line reason` entry to the "
                + "allowlist:\n"
                + unexpected.map(\.description).joined(separator: "\n"))
    }

    func testEveryAllowlistEntryStillNamesAFreshTokenSite() throws {
        let census = try Self.productionCensus()
        let failures = Self.allowlistFailures(Self.allowlist, sites: census.sites)
        XCTAssertTrue(failures.isEmpty, failures.joined(separator: "\n"))
    }

    /// A vacuous walk would pass the census: it must read the production tree
    /// and see the tokens the operations own (every `let x = CancelToken()`,
    /// and a token stored on its owner).
    func testTheCensusReadsTheProductionTreeAndItsOwnedTokens() throws {
        let census = try Self.productionCensus()
        XCTAssertGreaterThan(census.files, 100, "the walk must see the whole app")
        XCTAssertGreaterThanOrEqual(
            census.tokenConstructions, 10,
            "the walk must see the tokens the operations own, or it proves nothing")
    }

    // MARK: - The matcher, pinned on synthetic sources

    func testTheMatcherFlagsEverySpellingOfAFreshTokenInACall() {
        let source = """
            a(cancel: CancelToken())
            b(path, nil, CancelToken())
            c(cancel:
                CancelToken())
            func d(cancel: CancelToken = CancelToken()) {}
            func e(_ cancel: CancelToken? = CancelToken()) {}
            f(cancel: .init())
            g(cancel: CancelToken.init())
            h(cancel: existing ?? CancelToken())
            i([CancelToken()])
            """
        XCTAssertEqual(Self.freshTokenLines(in: source).lines, [1, 2, 4, 5, 6, 7, 8, 9, 10])
    }

    func testTheMatcherLeavesOwnedTokensCommentsAndStringsAlone() {
        let source = """
            let cancel = CancelToken()
            var token: CancelToken = CancelToken()
            self.searchCancelToken = CancelToken()
            tokens[key] = CancelToken()
            let make = { CancelToken() }
            // a(cancel: CancelToken())
            /* b(cancel: CancelToken()) /* nested */ c(CancelToken()) */
            let s = "cancel: CancelToken()"
            let raw = #"cancel: CancelToken()"#
            let block = \"""
                fetch(path, nil, CancelToken())
                \"""
            h(cancel: cancel)
            return CancelToken()
            let id = UUIDToken.init()
            """
        let found = Self.freshTokenLines(in: source)
        XCTAssertEqual(found.lines, [], "only tokens passed straight into a call are flagged")
        XCTAssertEqual(found.constructions, 6, "the bindings and stores are counted as owned")
    }

    func testAStaleOrDuplicateAllowlistEntryFails() {
        let live = [Site(file: "Graph/GraphDiagramView.swift", line: 12)]
        XCTAssertEqual(
            Self.allowlistFailures(
                [(site: "Graph/GraphDiagramView.swift:12", reason: "test-only helper")],
                sites: live),
            [])
        XCTAssertFalse(
            Self.allowlistFailures(
                [(site: "Graph/GraphDiagramView.swift:13", reason: "test-only helper")],
                sites: live
            ).isEmpty,
            "an entry whose site moved or went away must fail")
        XCTAssertFalse(
            Self.allowlistFailures(
                [
                    (site: "Graph/GraphDiagramView.swift:12", reason: "test-only helper"),
                    (site: "Graph/GraphDiagramView.swift:12", reason: "test-only helper"),
                ],
                sites: live
            ).isEmpty,
            "a duplicated entry must fail")
        XCTAssertFalse(
            Self.allowlistFailures(
                [(site: "Graph/GraphDiagramView.swift:12", reason: " ")], sites: live
            ).isEmpty,
            "an entry needs a reviewed reason")
    }

    // MARK: - Implementation

    static func allowlistFailures(
        _ allowlist: [(site: String, reason: String)], sites: [Site]
    ) -> [String] {
        let live = Set(sites.map(\.description))
        var failures: [String] = []
        var seen = Set<String>()
        for entry in allowlist {
            if !seen.insert(entry.site).inserted {
                failures.append("duplicate allowlist entry \(entry.site)")
            }
            if entry.reason.trimmingCharacters(in: .whitespaces).isEmpty {
                failures.append("allowlist entry \(entry.site) has no reason")
            }
            if !live.contains(entry.site) {
                failures.append(
                    "stale allowlist entry \(entry.site) (\(entry.reason)): no fresh "
                        + "token is passed there any more — remove or move the entry")
            }
        }
        return failures
    }

    struct Census {
        var sites: [Site] = []
        var files = 0
        var tokenConstructions = 0
    }

    /// Walked once per test process: the sources do not change under it.
    private static let cachedCensus = Result { try walkProductionSources() }

    static func productionCensus() throws -> Census {
        try cachedCensus.get()
    }

    private static func walkProductionSources() throws -> Census {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .appendingPathComponent("Sources/SlateMac")
        guard
            let enumerator = FileManager.default.enumerator(
                at: root, includingPropertiesForKeys: nil)
        else {
            throw CocoaError(.fileReadNoSuchFile, userInfo: [NSFilePathErrorKey: root.path])
        }
        var census = Census()
        let prefix = root.standardizedFileURL.path + "/"
        for case let url as URL in enumerator where url.pathExtension == "swift" {
            // The uniffi-generated bindings (materialized into Sources by the
            // build, not tracked in git) define the type; they make no calls.
            if url.lastPathComponent == "slate_uniffi.swift" { continue }
            let path = url.standardizedFileURL.path
            let relative = path.hasPrefix(prefix)
                ? String(path.dropFirst(prefix.count)) : url.lastPathComponent
            let found = freshTokenLines(in: try String(contentsOf: url, encoding: .utf8))
            census.files += 1
            census.tokenConstructions += found.constructions
            census.sites += found.lines.map { Site(file: relative, line: $0) }
        }
        census.sites.sort { ($0.file, $0.line) < ($1.file, $1.line) }
        return census
    }

    /// The 1-based lines on which a fresh token is passed straight into a
    /// call, and how many token constructions the source holds in all.
    static func freshTokenLines(in source: String) -> (lines: [Int], constructions: Int) {
        let code = codeOnly(Array(source.utf8))
        var lines: [Int] = []
        var constructions = 0
        var index = 0
        while index < code.count {
            if let end = freshConstructionEnd(in: code, at: index) {
                constructions += 1
                if isPassedStraightIntoACall(code, before: index) {
                    lines.append(lineNumber(of: index, in: code))
                }
                index = end
                continue
            }
            if let end = inferredTokenArgumentEnd(in: code, at: index) {
                constructions += 1
                lines.append(lineNumber(of: index, in: code))
                index = end
                continue
            }
            index += 1
        }
        return (lines, constructions)
    }

    // MARK: Lexing

    private static let space = UInt8(ascii: " ")
    private static let newline = UInt8(ascii: "\n")
    private static let quote = UInt8(ascii: "\"")
    private static let hash = UInt8(ascii: "#")
    private static let slash = UInt8(ascii: "/")
    private static let star = UInt8(ascii: "*")
    private static let backslash = UInt8(ascii: "\\")

    /// The source with every comment and string literal blanked to spaces,
    /// newlines kept, so a site keeps its line and prose cannot trip the
    /// census.
    private static func codeOnly(_ bytes: [UInt8]) -> [UInt8] {
        var out = bytes
        func blank(_ range: Range<Int>) {
            for offset in range where out[offset] != newline { out[offset] = space }
        }
        var index = 0
        while index < bytes.count {
            let byte = bytes[index]
            if byte == slash, index + 1 < bytes.count, bytes[index + 1] == slash {
                var end = index
                while end < bytes.count, bytes[end] != newline { end += 1 }
                blank(index..<end)
                index = end
            } else if byte == slash, index + 1 < bytes.count, bytes[index + 1] == star {
                var depth = 0
                var end = index
                while end < bytes.count {
                    if bytes[end] == slash, end + 1 < bytes.count, bytes[end + 1] == star {
                        depth += 1
                        end += 2
                    } else if bytes[end] == star, end + 1 < bytes.count, bytes[end + 1] == slash {
                        depth -= 1
                        end += 2
                        if depth == 0 { break }
                    } else {
                        end += 1
                    }
                }
                blank(index..<min(end, bytes.count))
                index = end
            } else if byte == quote || (byte == hash && startsRawString(bytes, at: index)) {
                let end = endOfStringLiteral(bytes, at: index)
                blank(index..<end)
                index = end
            } else {
                index += 1
            }
        }
        return out
    }

    private static func startsRawString(_ bytes: [UInt8], at index: Int) -> Bool {
        var cursor = index
        while cursor < bytes.count, bytes[cursor] == hash { cursor += 1 }
        return cursor < bytes.count && bytes[cursor] == quote
    }

    /// One past the literal that starts at `index`: `"…"`, `"""…"""`, and
    /// their raw `#"…"#` forms. Escapes skip the next byte; a raw literal
    /// ends only at a quote followed by as many hashes as it opened with.
    private static func endOfStringLiteral(_ bytes: [UInt8], at index: Int) -> Int {
        var cursor = index
        var hashes = 0
        while cursor < bytes.count, bytes[cursor] == hash {
            hashes += 1
            cursor += 1
        }
        let multiline = cursor + 2 < bytes.count
            && bytes[cursor] == quote && bytes[cursor + 1] == quote && bytes[cursor + 2] == quote
        cursor += multiline ? 3 : 1
        while cursor < bytes.count {
            let byte = bytes[cursor]
            if hashes == 0, byte == backslash {
                cursor += 2
                continue
            }
            if !multiline, byte == newline { return cursor }
            if byte == quote {
                let closes = !multiline
                    || (cursor + 2 < bytes.count
                        && bytes[cursor + 1] == quote && bytes[cursor + 2] == quote)
                if closes {
                    var after = cursor + (multiline ? 3 : 1)
                    var trailing = 0
                    while trailing < hashes, after < bytes.count, bytes[after] == hash {
                        trailing += 1
                        after += 1
                    }
                    if trailing == hashes { return after }
                }
            }
            cursor += 1
        }
        return bytes.count
    }

    // MARK: Matching

    private static func isIdentifierByte(_ byte: UInt8) -> Bool {
        (byte >= UInt8(ascii: "a") && byte <= UInt8(ascii: "z"))
            || (byte >= UInt8(ascii: "A") && byte <= UInt8(ascii: "Z"))
            || (byte >= UInt8(ascii: "0") && byte <= UInt8(ascii: "9"))
            || byte == UInt8(ascii: "_")
    }

    private static func isWhitespace(_ byte: UInt8) -> Bool {
        byte == space || byte == newline || byte == UInt8(ascii: "\t")
            || byte == UInt8(ascii: "\r")
    }

    private static func skipWhitespace(_ code: [UInt8], from index: Int) -> Int {
        var cursor = index
        while cursor < code.count, isWhitespace(code[cursor]) { cursor += 1 }
        return cursor
    }

    /// The index of the last non-whitespace byte before `index`, if any.
    private static func previousSignificant(_ code: [UInt8], before index: Int) -> Int? {
        var cursor = index - 1
        while cursor >= 0, isWhitespace(code[cursor]) { cursor -= 1 }
        return cursor >= 0 ? cursor : nil
    }

    private static let tokenName = Array("CancelToken".utf8)
    private static let initCall = Array(".init".utf8)

    private static func matches(_ code: [UInt8], _ bytes: [UInt8], at index: Int) -> Bool {
        guard index + bytes.count <= code.count else { return false }
        for offset in 0..<bytes.count where code[index + offset] != bytes[offset] {
            return false
        }
        return true
    }

    /// The identifier that ends at `end` (inclusive), and where it starts.
    private static func identifier(_ code: [UInt8], endingAt end: Int) -> (String, Int)? {
        guard end >= 0, isIdentifierByte(code[end]) else { return nil }
        var start = end
        while start > 0, isIdentifierByte(code[start - 1]) { start -= 1 }
        return (String(decoding: code[start...end], as: UTF8.self), start)
    }

    /// One past `CancelToken()` or `CancelToken.init()` starting at `index`.
    private static func freshConstructionEnd(in code: [UInt8], at index: Int) -> Int? {
        guard code[index] == UInt8(ascii: "C"), matches(code, tokenName, at: index),
            index == 0 || !isIdentifierByte(code[index - 1])
        else { return nil }
        var cursor = index + tokenName.count
        guard cursor >= code.count || !isIdentifierByte(code[cursor]) else { return nil }
        cursor = skipWhitespace(code, from: cursor)
        if matches(code, initCall, at: cursor) {
            cursor = skipWhitespace(code, from: cursor + initCall.count)
        }
        guard cursor < code.count, code[cursor] == UInt8(ascii: "(") else { return nil }
        cursor = skipWhitespace(code, from: cursor + 1)
        guard cursor < code.count, code[cursor] == UInt8(ascii: ")") else { return nil }
        return cursor + 1
    }

    /// `cancel: .init()` (any label naming a cancel token): the type is
    /// inferred from the parameter, so it is a fresh token too.
    private static func inferredTokenArgumentEnd(in code: [UInt8], at index: Int) -> Int? {
        guard code[index] == UInt8(ascii: "."), matches(code, initCall, at: index) else {
            return nil
        }
        var cursor = skipWhitespace(code, from: index + initCall.count)
        guard cursor < code.count, code[cursor] == UInt8(ascii: "(") else { return nil }
        cursor = skipWhitespace(code, from: cursor + 1)
        guard cursor < code.count, code[cursor] == UInt8(ascii: ")") else { return nil }
        guard let colon = previousSignificant(code, before: index),
            code[colon] == UInt8(ascii: ":"),
            let label = previousSignificant(code, before: colon),
            let (word, _) = identifier(code, endingAt: label),
            word.lowercased().contains("cancel")
        else { return nil }
        return cursor + 1
    }

    /// True when the construction at `index` is an argument, an element or a
    /// fallback (`(`, `,`, `:`, `[`, `?`), or a parameter's default value
    /// (`label: CancelToken = CancelToken()` inside a parameter list). A
    /// binding, a store on an owner, a `return` or a factory closure is the
    /// operation's own token.
    private static func isPassedStraightIntoACall(_ code: [UInt8], before index: Int) -> Bool {
        guard let previous = previousSignificant(code, before: index) else { return false }
        switch code[previous] {
        case UInt8(ascii: "("), UInt8(ascii: ","), UInt8(ascii: ":"), UInt8(ascii: "["),
            UInt8(ascii: "?"):
            return true
        case UInt8(ascii: "="):
            return isDefaultParameterValue(code, equals: previous)
        default:
            return false
        }
    }

    /// `(… label: CancelToken = ` or `, _ label: CancelToken? = `: the `=`
    /// follows a parameter's type annotation, not a `let`/`var` binding.
    private static func isDefaultParameterValue(_ code: [UInt8], equals: Int) -> Bool {
        guard var cursor = previousSignificant(code, before: equals) else { return false }
        if code[cursor] == UInt8(ascii: "?") {
            guard let next = previousSignificant(code, before: cursor) else { return false }
            cursor = next
        }
        guard let (type, typeStart) = identifier(code, endingAt: cursor), type == "CancelToken",
            let colon = previousSignificant(code, before: typeStart),
            code[colon] == UInt8(ascii: ":"),
            let nameEnd = previousSignificant(code, before: colon),
            let (_, nameStart) = identifier(code, endingAt: nameEnd),
            var before = previousSignificant(code, before: nameStart)
        else { return false }
        // An external label (`with cancel:` / `_ cancel:`) may precede the name.
        if let (label, labelStart) = identifier(code, endingAt: before) {
            guard label != "let", label != "var",
                let beforeLabel = previousSignificant(code, before: labelStart)
            else { return false }
            before = beforeLabel
        }
        return code[before] == UInt8(ascii: "(") || code[before] == UInt8(ascii: ",")
    }

    private static func lineNumber(of index: Int, in code: [UInt8]) -> Int {
        var line = 1
        for byte in code[..<index] where byte == newline { line += 1 }
        return line
    }
}
