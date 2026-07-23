// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// §W-A differential-harness skeleton, mac twin (w0_spec §W0-3 item 5,
/// #715). Serializes the skeleton's read-side surfaces — editor spans,
/// headings, reading blocks, search, links — over the shared markdown
/// fixture corpus and asserts byte-identity against the committed goldens
/// (`crates/slate-core/tests/fixtures/parity_golden/`). The Windows twin
/// (`apps/slate-windows/tools/ParityHarness/` + its census) asserts the
/// same goldens, so both CIs green proves cross-platform byte-identity
/// transitively; W8-4 replaces this with the direct three-job diff.
///
/// The canonical serialization rules live in the Windows twin's
/// `CanonicalJson.cs` header — change both implementations together,
/// never one. Line endings are inside the corpus deliberately (CRLF and
/// mixed fixtures) and are never normalized (§W-A / decision 9).
final class ParityHarnessTests: XCTestCase {

    private static var repoRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .deletingLastPathComponent()  // apps
            .deletingLastPathComponent()  // repo root
    }

    private static var fixturesDir: URL {
        repoRoot.appendingPathComponent("crates/slate-core/tests/fixtures/markdown")
    }

    private static var goldenDir: URL {
        repoRoot.appendingPathComponent("crates/slate-core/tests/fixtures/parity_golden")
    }

    private static let pinnedSearchQueries = ["fixture", "heading", "parity"]

    func testHarnessArtifactsMatchCommittedGoldensByteForByte() throws {
        let produced = try Self.runHarness()

        let goldenNames = try FileManager.default
            .contentsOfDirectory(atPath: Self.goldenDir.path)
            .filter { $0.hasSuffix(".json") }
            .sorted { Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16)) }
        XCTAssertEqual(
            goldenNames,
            produced.keys.sorted { Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16)) })

        for name in goldenNames {
            let golden = try Data(contentsOf: Self.goldenDir.appendingPathComponent(name))
            XCTAssertEqual(
                produced[name], golden,
                "artifact \(name) differs from golden — the mac and Windows serializations "
                    + "have drifted; fix the divergence (or regenerate goldens deliberately "
                    + "with the Windows harness) before merging")
        }
    }

    func testHarnessIsDeterministicAcrossRuns() throws {
        let a = try Self.runHarness()
        let b = try Self.runHarness()
        XCTAssertEqual(a, b)
    }

    // MARK: - Harness

    /// Returns artifact-name → canonical bytes, mirroring the Windows
    /// harness exactly: fixtures copied into a temp vault (scans write
    /// .slate/ cache), one artifact per fixture plus search + links.
    private static func runHarness() throws -> [String: Data] {
        let fm = FileManager.default
        let files = try fm.contentsOfDirectory(atPath: fixturesDir.path)
            .filter { $0.hasSuffix(".md") }
            .sorted { Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16)) }
        XCTAssertFalse(files.isEmpty, "no fixtures at \(fixturesDir.path)")

        let vaultRoot = fm.temporaryDirectory
            .appendingPathComponent("parity-harness-\(UUID().uuidString)")
        try fm.createDirectory(at: vaultRoot, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: vaultRoot) }
        for f in files {
            try fm.copyItem(
                at: fixturesDir.appendingPathComponent(f),
                to: vaultRoot.appendingPathComponent(f))
        }

        let session = try VaultSession.openFilesystem(rootPath: vaultRoot.path)
        let cancel = CancelToken()
        _ = try session.scanInitial(cancel: cancel)

        var artifacts: [String: Data] = [:]
        for f in files {
            let bytes = try Data(contentsOf: vaultRoot.appendingPathComponent(f))
            let text = String(decoding: bytes, as: UTF8.self)
            artifacts[f + ".json"] = Data(fileArtifact(relPath: f, text: text).utf8)
        }
        artifacts["search.json"] = Data(try searchArtifact(session: session, cancel: cancel).utf8)
        artifacts["links.json"] = Data(try linksArtifact(session: session, relPaths: files).utf8)
        artifacts["editor_scale.json"] = Data(editorScaleArtifact().utf8)
        return artifacts
    }

    // MARK: - Surfaces (mirror SurfaceSerializer.cs)

    private static func fileArtifact(relPath: String, text: String) -> String {
        let j = CanonicalJson()
        j.raw("{\"file\":").str(relPath)

        j.raw(",\"spans\":[")
        appendSpans(j, editorHighlightSpans(text: text))
        j.raw("]")

        j.raw(",\"span_windows\":")
        appendSpanWindows(j, text: text)

        j.raw(",\"headings\":[")
        let headings = extractHeadings(source: text)
        for (i, h) in headings.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"level\":").num(UInt64(h.level))
                .raw(",\"text\":").str(h.text)
                .raw(",\"ordinal\":").num(UInt64(h.ordinal))
                .raw(",\"anchor\":").str(h.anchorId)
                .raw(",\"offset\":").num(UInt64(h.byteOffset))
                .raw("}")
        }
        j.raw("]")

        j.raw(",\"blocks\":[")
        let blocks = readingBlocksSource(source: text)
        for (i, b) in blocks.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"kind\":").str(blockKindName(b.kind))
                .raw(",\"start\":").num(b.byteStart)
                .raw(",\"end\":").num(b.byteEnd)
                .raw(",\"source\":").str(b.source)
                .raw("}")
        }
        j.raw("]}")
        return j.output + "\n"
    }

    private static func editorScaleArtifact() -> String {
        let j = CanonicalJson()
        let sizes = [100 * 1024, 1024 * 1024, 8 * 1024 * 1024]
        j.raw("{\"sizes\":[")
        for (index, size) in sizes.enumerated() {
            if index > 0 { j.raw(",") }
            let text = editorScaleFixture(targetBytes: size)
            j.raw("{\"bytes\":").num(UInt64(size)).raw(",\"span_windows\":")
            appendSpanWindows(j, text: text)
            j.raw("}")
        }
        j.raw("]}")
        return j.output + "\n"
    }

    private static func appendSpanWindows(_ j: CanonicalJson, text: String) {
        let buffer = DocumentBuffer(text: text)
        let length = text.utf16.count
        let anchors = [0, length / 2, length]
        j.raw("[")
        for (index, anchor) in anchors.enumerated() {
            if index > 0 { j.raw(",") }
            let start = max(0, anchor - 32)
            let end = min(length, anchor + 32)
            let ranged = buffer.highlightInRange(
                dirtyStartUtf16: UInt32(start),
                dirtyEndUtf16: UInt32(end))
            j.raw("{\"request_start_utf16\":").num(UInt64(start))
                .raw(",\"request_end_utf16\":").num(UInt64(end))
                .raw(",\"applied_start\":").num(UInt64(ranged.appliedStart))
                .raw(",\"applied_end\":").num(UInt64(ranged.appliedEnd))
                .raw(",\"spans\":[")
            appendSpans(j, ranged.spans)
            j.raw("]}")
        }
        j.raw("]")
    }

    private static func appendSpans(_ j: CanonicalJson, _ spans: [EditorSpan]) {
        for (index, span) in spans.enumerated() {
            if index > 0 { j.raw(",") }
            j.raw("{\"start\":").num(UInt64(span.startByte))
                .raw(",\"end\":").num(UInt64(span.endByte))
                .raw(",\"kind\":").str(spanKindName(span.kind))
                .raw("}")
        }
    }

    private static func editorScaleFixture(targetBytes: Int) -> String {
        let block =
            "## Section\n\nProse with [[Wikilink]] and #tag plus `code` and [@citation].\n\n"
        var text = ""
        text.reserveCapacity(targetBytes + block.utf8.count)
        while text.utf8.count < targetBytes {
            text += block
        }
        return String(text.prefix(targetBytes))
    }

    private static func searchArtifact(session: VaultSession, cancel: CancelToken) throws -> String {
        let j = CanonicalJson()
        j.raw("{\"queries\":[")
        for (q, query) in pinnedSearchQueries.enumerated() {
            if q > 0 { j.raw(",") }
            let rs = try session.fullTextSearch(query: query, scope: .vault, cancel: cancel)
            let rows = rs.rows.sorted { lhs, rhs in
                if lhs.path != rhs.path {
                    return Array(lhs.path.utf16).lexicographicallyPrecedes(Array(rhs.path.utf16))
                }
                return Array(lhs.snippet.utf16).lexicographicallyPrecedes(Array(rhs.snippet.utf16))
            }
            j.raw("{\"query\":").str(query).raw(",\"rows\":[")
            for (i, row) in rows.enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"path\":").str(slash(row.path))
                    .raw(",\"snippet\":").str(row.snippet)
                    .raw(",\"score\":").num(row.score)
                    .raw("}")
            }
            j.raw("]}")
        }
        j.raw("]}")
        return j.output + "\n"
    }

    private static func linksArtifact(session: VaultSession, relPaths: [String]) throws -> String {
        let j = CanonicalJson()
        j.raw("{\"files\":[")
        for (f, rel) in relPaths.enumerated() {
            if f > 0 { j.raw(",") }
            j.raw("{\"file\":").str(slash(rel))

            j.raw(",\"outgoing\":[")
            let outgoing = try session.outgoingLinks(path: rel)
            for (i, o) in outgoing.enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"target\":")
                if let target = o.targetPath {
                    j.str(slash(target))
                } else {
                    j.null()
                }
                j.raw(",\"raw\":").str(o.targetRaw)
                    .raw(",\"kind\":").str(o.kind)
                    .raw(",\"embed\":").bool(o.isEmbed)
                    .raw(",\"external\":").bool(o.isExternal)
                    .raw(",\"unresolved\":").bool(o.isUnresolved)
                    .raw(",\"ordinal\":").num(UInt64(o.ordinal))
                    .raw("}")
            }
            j.raw("]")

            j.raw(",\"backlinks\":[")
            let backlinks = try session.backlinks(path: rel, paging: Paging(cursor: nil, limit: 500)).items
            for (i, b) in backlinks.enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"source\":").str(slash(b.sourcePath))
                    .raw(",\"snippet\":").str(b.snippet)
                    .raw(",\"ordinal\":").num(UInt64(b.ordinal))
                    .raw(",\"kind\":").str(b.kind)
                    .raw(",\"embed\":").bool(b.isEmbed)
                    .raw("}")
            }
            j.raw("]}")
        }
        j.raw("]}")
        return j.output + "\n"
    }

    private static func spanKindName(_ kind: EditorSpanKind) -> String {
        switch kind {
        case .heading(let level): return "heading:\(level)"
        case .emphasis: return "emphasis"
        case .strong: return "strong"
        case .strikethrough: return "strikethrough"
        case .inlineCode: return "inline_code"
        case .codeFence: return "code_fence"
        case .link: return "link"
        case .image: return "image"
        case .blockQuote: return "block_quote"
        case .wikilink: return "wikilink"
        case .embed: return "embed"
        case .tag: return "tag"
        case .citation: return "citation"
        case .comment: return "comment"
        case .frontmatter: return "frontmatter"
        case .code(let token): return "code:\(tokenKindName(token))"
        }
    }

    private static func tokenKindName(_ token: TokenKind) -> String {
        switch token {
        case .keyword: return "keyword"
        case .string: return "string"
        case .number: return "number"
        case .comment: return "comment"
        case .identifier: return "identifier"
        case .type: return "type"
        case .function: return "function"
        case .operator: return "operator"
        case .punctuation: return "punctuation"
        case .other(let label): return "other:\(label)"
        }
    }

    private static func blockKindName(_ kind: ReadingBlockKind) -> String {
        switch kind {
        case .heading(let level): return "heading:\(level)"
        case .paragraph: return "paragraph"
        case .listItem(let depth, let ordered, let task):
            return "list_item:\(depth):\(ordered ? "ordered" : "unordered"):\(task ?? "-")"
        case .blockQuote(let depth): return "block_quote:\(depth)"
        case .codeFence(let language, _): return "code_fence:\(language)"
        case .mathBlock: return "math_block"
        case .diagram(let dialect, _): return "diagram:\(dialect)"
        case .table: return "table"
        case .thematicBreak: return "thematic_break"
        case .html: return "html"
        }
    }

    private static func slash(_ path: String) -> String {
        path.replacingOccurrences(of: "\\", with: "/")
    }
}

/// Canonical JSON writer — the Swift half of the fixed serialization
/// algorithm defined in `apps/slate-windows/tools/ParityHarness/
/// CanonicalJson.cs`. Same escaping table, same `%.6f` doubles, no
/// whitespace; change both together.
private final class CanonicalJson {
    private(set) var output = ""

    @discardableResult
    func raw(_ s: String) -> CanonicalJson {
        output += s
        return self
    }

    @discardableResult
    func str(_ value: String) -> CanonicalJson {
        output += "\""
        for scalar in value.unicodeScalars {
            switch scalar {
            case "\"": output += "\\\""
            case "\\": output += "\\\\"
            case "\n": output += "\\n"
            case "\r": output += "\\r"
            case "\t": output += "\\t"
            default:
                if scalar.value < 0x20 {
                    output += String(format: "\\u%04x", scalar.value)
                } else {
                    output.unicodeScalars.append(scalar)
                }
            }
        }
        output += "\""
        return self
    }

    @discardableResult
    func num(_ value: UInt64) -> CanonicalJson {
        output += String(value)
        return self
    }

    @discardableResult
    func num(_ value: Double) -> CanonicalJson {
        output += String(format: "%.6f", locale: Locale(identifier: "en_US_POSIX"), value)
        return self
    }

    @discardableResult
    func bool(_ value: Bool) -> CanonicalJson {
        output += value ? "true" : "false"
        return self
    }

    @discardableResult
    func null() -> CanonicalJson {
        output += "null"
        return self
    }
}
