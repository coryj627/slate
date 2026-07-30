// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import CryptoKit
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
        // EVERY fixture file enters the vault (W3-5: attachment and
        // .canvas targets make embed resolution exercisable);
        // artifacts are still generated per-.md only.
        let allFiles = try fm.contentsOfDirectory(atPath: fixturesDir.path)
            .sorted { Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16)) }
        let files = allFiles.filter { $0.hasSuffix(".md") }
        XCTAssertFalse(files.isEmpty, "no fixtures at \(fixturesDir.path)")

        let vaultRoot = fm.temporaryDirectory
            .appendingPathComponent("parity-harness-\(UUID().uuidString)")
        try fm.createDirectory(at: vaultRoot, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: vaultRoot) }
        for f in allFiles {
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
            artifacts[f + ".json"] = Data(
                try fileArtifact(relPath: f, text: text, session: session).utf8)
        }
        artifacts["search.json"] = Data(try searchArtifact(session: session, cancel: cancel).utf8)
        artifacts["links.json"] = Data(try linksArtifact(session: session, relPaths: files).utf8)
        artifacts["editor_scale.json"] = Data(editorScaleArtifact().utf8)
        return artifacts
    }

    // MARK: - Surfaces (mirror SurfaceSerializer.cs)

    /// Per-file artifact: spans + headings + reading blocks + inline runs.
    ///
    /// The `inline_runs` array (#967) is 1:1 with `blocks` and closes the
    /// §W-A gap that block-only serialization left: wikilink / embed /
    /// tag / citation payloads, per-grammar resolution, and accessible
    /// text are now byte-checked across platforms.
    ///
    /// **Citation join, deliberately empty.** The fixture vault ships no
    /// CSL style and no bibliography, so there is nothing deterministic to
    /// render citations against; both twins therefore pass an EMPTY
    /// citation list. Core still emits `citation` runs — the kind comes
    /// from the span classifier, not from the rendered list — with the raw
    /// text as both display and speech, which is deterministic on every
    /// platform. Matched-citation rendering is covered by the core unit
    /// tests instead; committing a style + `.bib` fixture to bring it
    /// under §W-A is a recorded W8-4 candidate.
    private static func fileArtifact(
        relPath: String, text: String, session: VaultSession
    ) throws -> String {
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
        j.raw("]")

        j.raw(",\"inline_runs\":[")
        let inlines = readingInlineSegmentsSource(
            source: text, citations: [], records: try session.outgoingLinks(path: relPath))
        for (i, inline) in inlines.enumerated() {
            if i > 0 { j.raw(",") }
            appendBlockInlines(j, inline)
        }
        j.raw("]")

        // W3-4: the READING-side canonical artifact. The span rows above
        // byte-check the editor highlight surface; these rows byte-check
        // {source, syntax_tokens, semantic_spans} + the core preamble —
        // exactly what both reading renderers consume. Token offsets are
        // block-local. Mirrors SurfaceSerializer.cs — change both together.
        j.raw(",\"code_blocks\":[")
        let codeBlocks = try session.getSyntaxTokens(path: relPath)
        for (i, c) in codeBlocks.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"line\":").num(UInt64(c.line))
                .raw(",\"offset\":").num(UInt64(c.byteOffset))
                .raw(",\"language\":")
            if let language = c.language { j.str(language) } else { j.null() }
            j.raw(",\"preamble\":").str(
                codeBlockPreamble(language: c.language, source: c.source))
                .raw(",\"source\":").str(c.source)
                .raw(",\"tokens\":[")
            for (t, token) in c.tokens.enumerated() {
                if t > 0 { j.raw(",") }
                j.raw("{\"start\":").num(UInt64(token.startByte))
                    .raw(",\"end\":").num(UInt64(token.endByte))
                    .raw(",\"kind\":").str(tokenKindName(token.kind))
                    .raw("}")
            }
            j.raw("],\"semantic_spans\":[")
            for (t, span) in c.semanticSpans.enumerated() {
                if t > 0 { j.raw(",") }
                j.raw("{\"start\":").num(UInt64(span.startByte))
                    .raw(",\"end\":").num(UInt64(span.endByte))
                    .raw(",\"kind\":").str(semanticKindName(span.kind))
                    .raw(",\"name\":").str(span.name)
                    .raw("}")
            }
            j.raw("]}")
        }
        j.raw("]")

        // W3-2: the canonical math artifact — byte-checks MathCAT's
        // speech and braille output cross-platform for the first time.
        // Braille is hex so byte identity is exact. Mirrors
        // SurfaceSerializer.cs — change both together.
        j.raw(",\"math_blocks\":[")
        let mathBlocks = try session.getMathBlocks(path: relPath)
        for (i, m) in mathBlocks.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"line\":").num(UInt64(m.line))
                .raw(",\"offset\":").num(UInt64(m.byteOffset))
                .raw(",\"display\":").str(m.displayStyle == .block ? "block" : "inline")
                .raw(",\"source\":").str(m.source)
                .raw(",\"mathml\":").str(m.mathml)
                .raw(",\"speech\":").str(m.speech)
                .raw(",\"braille_hex\":").str(
                    m.braille.map { String(format: "%02X", $0) }.joined())
                .raw("}")
        }
        j.raw("]")

        // W3-3: the canonical diagram artifact — description + render
        // status byte-checked. SVG BYTES are deliberately excluded
        // (machine-dependent float text-measurement); presence pins
        // that rendering succeeded. Mirrors SurfaceSerializer.cs —
        // change both together.
        j.raw(",\"diagram_blocks\":[")
        let diagramBlocks = try session.getDiagramBlocks(path: relPath)
        for (i, d) in diagramBlocks.enumerated() {
            if i > 0 { j.raw(",") }
            let status: String
            switch d.renderStatus {
            case .ok: status = "ok"
            case .unsupportedDialect(let reason): status = "unsupported:" + reason
            case .renderFailed(let message): status = "failed:" + message
            }
            let svgPresent = !(d.svg ?? Data()).isEmpty
            j.raw("{\"line\":").num(UInt64(d.line))
                .raw(",\"offset\":").num(UInt64(d.byteOffset))
                .raw(",\"dialect\":").str("mermaid")
                .raw(",\"source\":").str(d.source)
                .raw(",\"description\":").str(d.structuredDescription)
                .raw(",\"status\":").str(status)
                .raw(",\"svg_present\":").raw(svgPresent ? "true" : "false")
                .raw("}")
        }
        j.raw("]")

        // W3-5: the canonical embed-resolution artifact — one row per
        // DISTINCT BlockEmbedKey in document order, alt deliberately
        // nil (the section pins CORE resolution). Mirrors
        // SurfaceSerializer.cs — change both together.
        j.raw(",\"embed_resolutions\":[")
        var embedKeys: [String] = []
        var seenEmbedKeys = Set<String>()
        for blockInlines in inlines {
            if let key = blockInlines.blockEmbedKey, !key.isEmpty,
                seenEmbedKeys.insert(key).inserted
            {
                embedKeys.append(key)
            }
        }
        for (i, key) in embedKeys.enumerated() {
            if i > 0 { j.raw(",") }
            let preview = try session.resolveEmbedPreview(
                hostPath: relPath, target: key, alt: nil)
            j.raw("{\"key\":").str(key)
                .raw(",\"truncated\":").raw(preview.truncated ? "true" : "false")
                .raw(",\"resolution\":")
            appendEmbedResolution(j, preview.resolution)
            j.raw("}")
        }
        j.raw("]}")
        return j.output + "\n"
    }

    private static func appendEmbedResolution(
        _ j: CanonicalJson, _ resolution: EmbedResolution
    ) {
        switch resolution {
        case .fullNote(let targetPath, let text, let nested):
            j.raw("{\"kind\":").str("note")
                .raw(",\"target\":").str(targetPath)
                .raw(",\"text\":").str(text)
                .raw(",\"nested\":[")
            appendNestedEmbeds(j, nested)
            j.raw("]}")
        case .section(let targetPath, let heading, let text, let nested):
            j.raw("{\"kind\":").str("section")
                .raw(",\"target\":").str(targetPath)
                .raw(",\"heading\":").str(heading)
                .raw(",\"text\":").str(text)
                .raw(",\"nested\":[")
            appendNestedEmbeds(j, nested)
            j.raw("]}")
        case .block(let targetPath, let blockId, let text):
            j.raw("{\"kind\":").str("block")
                .raw(",\"target\":").str(targetPath)
                .raw(",\"block_id\":").str(blockId)
                .raw(",\"text\":").str(text)
                .raw("}")
        case .image(let targetPath, let bytes, let mime, let alt):
            // SHA-256 over the payload — image bytes are vault
            // CONTENT (unlike machine-dependent diagram SVG), so both
            // twins must round-trip them byte-identically.
            let digest = bytes.isEmpty
                ? ""
                : SHA256.hash(data: bytes).map { String(format: "%02X", $0) }.joined()
            j.raw("{\"kind\":").str("image")
                .raw(",\"target\":").str(targetPath)
                .raw(",\"mime\":").str(mime)
                .raw(",\"image_len\":").num(UInt64(bytes.count))
                .raw(",\"image_sha256\":").str(digest)
                .raw(",\"alt\":").str(alt ?? "")
                .raw("}")
        case .unresolved(let reason):
            let kind: String
            switch reason {
            case .targetNotFound(let target):
                kind = "unresolved:target_not_found:" + target
            case .headingNotFound(let targetPath, let heading):
                kind = "unresolved:heading_not_found:" + targetPath + "#" + heading
            case .blockNotFound(let targetPath, let blockId):
                kind = "unresolved:block_not_found:" + targetPath + "^" + blockId
            case .depthLimitReached:
                kind = "unresolved:depth_limit"
            case .readError(let message):
                kind = "unresolved:read_error:" + message
            }
            j.raw("{\"kind\":").str(kind).raw("}")
        }
    }

    private static func appendNestedEmbeds(
        _ j: CanonicalJson, _ nested: [NestedEmbed]
    ) {
        for (i, child) in nested.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"raw\":").str(child.rawTarget)
                .raw(",\"start\":").num(UInt64(child.byteOffsetInParent))
                .raw(",\"end\":").num(UInt64(child.byteEndInParent))
                .raw(",\"resolution\":")
            appendEmbedResolution(j, child.resolution)
            j.raw("}")
        }
    }

    private static func semanticKindName(_ kind: SemanticKind) -> String {
        switch kind {
        case .function: return "function"
        case .type: return "type"
        case .variable: return "variable"
        }
    }

    private static func appendBlockInlines(
        _ j: CanonicalJson, _ inlines: ReadingBlockInlines
    ) {
        j.raw("{\"embed\":")
        if let key = inlines.blockEmbedKey { j.str(key) } else { j.null() }
        j.raw(",\"marker\":")
        if let marker = inlines.listMarker { j.str(marker) } else { j.null() }
        j.raw(",\"segments\":[")
        for (s, segment) in inlines.segments.enumerated() {
            if s > 0 { j.raw(",") }
            j.raw("{\"content\":").str(segment.content).raw(",\"task\":")
            if let completed = segment.taskCompleted { j.bool(completed) } else { j.null() }
            j.raw(",\"runs\":[")
            for (r, run) in segment.runs.enumerated() {
                if r > 0 { j.raw(",") }
                appendInlineRun(j, run)
            }
            j.raw("]}")
        }
        j.raw("]}")
    }

    private static func appendInlineRun(_ j: CanonicalJson, _ run: ReadingInlineRun) {
        j.raw("{\"start\":").num(UInt64(run.start))
            .raw(",\"end\":").num(UInt64(run.end))
            .raw(",\"styles\":[")
        for (i, style) in run.styles.enumerated() {
            if i > 0 { j.raw(",") }
            j.str(styleName(style))
        }
        j.raw("]")
        // Naming convention (both twins): `kind` is the snake_case
        // discriminator with enum-ish scalars colon-joined (mirroring
        // `list_item:{depth}:{ordered}:{task}`); free-text payloads are
        // separate escaped fields, because a URL or target contains `:`
        // and colon-joining would make the value ambiguous.
        switch run.kind {
        case .text:
            j.raw(",\"kind\":").str("text")
        case .externalLink(let url):
            j.raw(",\"kind\":").str("external_link").raw(",\"url\":").str(url)
        case .wikilink(let target, let baseTarget, let anchor, let grammar, let resolved):
            j.raw(",\"kind\":").str(
                "wikilink:\(grammarName(grammar)):\(resolved ? "resolved" : "unresolved")")
                .raw(",\"target\":").str(target)
                .raw(",\"base_target\":").str(baseTarget)
                .raw(",\"anchor\":")
            if let anchor { j.str(anchorName(anchor)) } else { j.null() }
        case .embed(let key):
            j.raw(",\"kind\":").str("embed").raw(",\"key\":").str(key)
        case .tag(let name):
            j.raw(",\"kind\":").str("tag").raw(",\"name\":").str(name)
        case .citation(let raw, let speech):
            j.raw(",\"kind\":").str("citation")
                .raw(",\"raw\":").str(raw)
                .raw(",\"speech\":").str(speech)
        }
        j.raw(",\"ax\":")
        if let ax = run.axText { j.str(ax) } else { j.null() }
        j.raw("}")
    }

    private static func styleName(_ style: ReadingInlineStyle) -> String {
        switch style {
        case .emphasis: return "emphasis"
        case .strong: return "strong"
        case .strikethrough: return "strikethrough"
        case .inlineCode: return "inline_code"
        }
    }

    private static func grammarName(_ grammar: ReadingWikiGrammar) -> String {
        switch grammar {
        case .wikilink: return "wikilink"
        case .markdownDestination: return "markdown_destination"
        }
    }

    /// `h:<text>` / `b:<text>` — the anchor form both twins emit.
    private static func anchorName(_ anchor: LinkAnchor) -> String {
        (anchor.kind == "block" ? "b:" : "h:") + anchor.text
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
