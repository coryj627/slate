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

    /// The canvas corpus lives in a DIFFERENT fixture directory from the
    /// markdown one, and gets its own temp vault below (W6-1 §W-A): a
    /// `.canvas` dropped into the markdown vault would change what
    /// `search`, `links`, `tasks` and `properties` see and move goldens
    /// this section has no business touching.
    private static var canvasFixturesDir: URL {
        repoRoot.appendingPathComponent("crates/slate-core/tests/fixtures/canvas")
    }

    private static var goldenDir: URL {
        repoRoot.appendingPathComponent("crates/slate-core/tests/fixtures/parity_golden")
    }

    private static let pinnedSearchQueries = ["fixture", "heading", "parity"]

    // The three pinned canvas input lists below are DUPLICATED from the
    // Windows twin (apps/slate-windows/tools/ParityHarness/
    // SurfaceSerializer.cs, which names this file beside them), the same
    // way `pinnedSearchQueries` is: the twins cannot share a declaration
    // across languages, and the committed golden arbitrates — two lists
    // that drift produce different artifacts and one census fails on the
    // golden the other passes.

    /// Filter queries the canvas artifact pins. The set is chosen for
    /// what each proves, not for coverage: the EMPTY query (which
    /// matches everything — the easiest rule to get backwards), a plain
    /// ASCII needle, the same needle mixed-case and whitespace-padded
    /// (case folding and trimming), a group-path element, the `kind`
    /// type word (typing "group" selects every group — shipped mac
    /// behaviour), and one that matches nothing.
    private static let pinnedCanvasFilterQueries = [
        "",
        "card",
        "ReSeArCh",
        "  research  ",
        "Q3",
        "group",
        "zzz-nothing",
    ]

    /// Rects the canvas artifact describes relatively. The last one
    /// coincides exactly with a card in `sample.canvas`, which is the
    /// zero-delta case whose axis rule is pinned in contract 0b-7.
    private static let pinnedCanvasRelativeRects = [
        CanvasRect(x: 0.0, y: 0.0, width: 240.0, height: 140.0),
        CanvasRect(x: 300.0, y: 150.0, width: 240.0, height: 140.0),
        CanvasRect(x: -1000.0, y: -1000.0, width: 10.0, height: 10.0),
        CanvasRect(x: 640.0, y: 180.0, width: 240.0, height: 140.0),
    ]

    /// Canvas fixtures deliberately kept OUT of the artifact.
    /// `large_2000.canvas` is the §K performance fixture; at 2,000 nodes
    /// its per-node rows would commit a golden no reviewer reads. Its
    /// coverage is the in-crate census, which runs the same queries over
    /// it.
    private static let canvasArtifactExclusions = ["large_2000.canvas"]

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

        // W4-5: seed the bibliography from the vault's own citation
        // config BEFORE serializing anything — citation rendering, the
        // per-file `citations` sections, and the vault artifact all read
        // the loaded entries. Mirrors Program.cs exactly.
        let bibWarnings = try session.setBibliographySources(
            sources: session.citationsPrefs().sources)

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
        artifacts["tasks.json"] = Data(try tasksArtifact(session: session).utf8)
        artifacts["properties.json"] = Data(try propertiesArtifact(session: session).utf8)
        artifacts["bibliography.json"] = Data(
            try bibliographyArtifact(session: session, loadWarnings: bibWarnings).utf8)
        // W6-1 PR 0b (§W-A `canvas_queries`): its own corpus, its own
        // vault, so nothing above it moves. Mirrors Program.cs exactly.
        artifacts["canvas_queries.json"] = Data(try canvasQueriesArtifact().utf8)
        // W6-1 PR A (§W-A `canvas_read`, contract A20): the READ side of
        // the same corpus, from its own temp vault. Mirrors Program.cs.
        artifacts["canvas_read.json"] = Data(try canvasReadArtifact().utf8)
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
    /// **Citation join, now real (W4-5 #737).** The corpus ships
    /// `library.bib` + `ieee.csl` + a `slate.json` naming both, and the
    /// harness seeds the bibliography before serializing, so citation runs
    /// carry genuinely RENDERED display and speech text instead of the raw
    /// fallback. A render failure is fatal to the harness rather than
    /// falling back — a silent fallback would make the golden meaningless.
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

        // W4-5: the rendered citations for THIS file, reused by both the
        // inline-run join below and the `citations` section further down.
        let references = try session.listCitationsInFile(path: relPath)
        let styleId = try citationStyleId(session: session)
        let rendered = try references.map {
            try session.renderCitation(reference: $0, styleId: styleId)
        }

        j.raw(",\"inline_runs\":[")
        let inlines = readingInlineSegmentsSource(
            source: text, citations: rendered,
            records: try session.outgoingLinks(path: relPath))
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
        j.raw("]")

        // W4-3: the canonical task-row artifact — byte-checks the
        // exact TaskItem surface both task panels consume.
        j.raw(",\"tasks\":[")
        let tasks = try session.tasksForFile(path: relPath)
        for (i, task) in tasks.enumerated() {
            if i > 0 { j.raw(",") }
            appendTaskItem(j, task)
        }
        j.raw("]")

        // W4-4: the canonical property-row artifact — mirrors
        // SurfaceSerializer.cs. value_json is emitted exactly as the
        // FFI hands it, never re-parsed or re-serialized.
        j.raw(",\"properties\":[")
        let properties = try session.getFileMetadata(path: relPath)?.properties ?? []
        for (i, property) in properties.enumerated() {
            if i > 0 { j.raw(",") }
            appendProperty(j, property)
        }
        j.raw("]")

        // W4-5: the per-file citation surface — parsed reference, its
        // cited items, and the RENDERED output both platforms must agree
        // on byte for byte. Mirrors SurfaceSerializer.cs.
        j.raw(",\"citations\":[")
        for (i, reference) in references.enumerated() {
            if i > 0 { j.raw(",") }
            appendCitation(j, reference, rendered[i])
        }
        j.raw("]}")
        return j.output + "\n"
    }

    private static func appendProperty(_ j: CanonicalJson, _ p: Property) {
        j.raw("{\"key\":").str(p.key)
            .raw(",\"kind\":").str(p.kind)
            .raw(",\"value_json\":").str(p.valueJson)
            .raw("}")
    }

    /// W4-4: the vault-wide property-key artifact — mirrors
    /// SurfaceSerializer.cs (list_property_keys is key-sorted and
    /// unpaged).
    private static func propertiesArtifact(session: VaultSession) throws -> String {
        let j = CanonicalJson()
        let keys = try session.listPropertyKeys()
        j.raw("{\"keys\":[")
        for (i, k) in keys.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"key\":").str(k.key)
                .raw(",\"file_count\":").num(k.fileCount)
                .raw(",\"kinds\":[")
            for (v, kind) in k.valueKinds.enumerated() {
                if v > 0 { j.raw(",") }
                j.str(kind)
            }
            j.raw("]}")
        }
        j.raw("]}")
        return j.output + "\n"
    }

    private static func appendTaskItem(_ j: CanonicalJson, _ t: TaskItem) {
        j.raw("{\"ordinal\":").num(UInt64(t.ordinal))
            .raw(",\"text\":").str(t.text)
            .raw(",\"status\":").str(t.statusChar)
            .raw(",\"completed\":").bool(t.completed)
            .raw(",\"due_ms\":")
        if let due = t.dueMs { j.num(due) } else { j.null() }
        j.raw(",\"scheduled_ms\":")
        if let scheduled = t.scheduledMs { j.num(scheduled) } else { j.null() }
        j.raw(",\"priority\":")
        if let priority = t.priority { j.num(Int64(priority)) } else { j.null() }
        j.raw(",\"recurrence\":")
        if let recurrence = t.recurrence { j.str(recurrence) } else { j.null() }
        j.raw(",\"line\":").num(UInt64(t.line))
            .raw(",\"offset\":").num(UInt64(t.byteOffset))
            .raw(",\"checkbox_start\":").num(UInt64(t.checkboxStartByte))
            .raw(",\"checkbox_end\":").num(UInt64(t.checkboxEndByte))
            .raw("}")
    }

    /// W4-3: the vault-wide task-review artifact — pins
    /// tasks_in_vault's DEFAULT-filter ordering contract plus the
    /// joined location fields. The default filter carries no due
    /// windows, so the artifact is wall-clock-free.
    private static func tasksArtifact(session: VaultSession) throws -> String {
        let j = CanonicalJson()
        let page = try session.tasksInVault(
            filter: TaskFilter(
                completed: nil, dueFromMs: nil, dueToMs: nil,
                priorityAtLeast: nil),
            paging: Paging(cursor: nil, limit: 1000))
        j.raw("{\"total\":").num(page.totalFiltered)
            .raw(",\"rows\":[")
        for (i, row) in page.items.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"path\":").str(row.path)
                .raw(",\"file\":").str(row.fileName)
                .raw(",\"task\":")
            appendTaskItem(j, row.task)
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

    // W5-2 (#742, contract S13): `summary` pins core's `summary_for`
    // render — the string the overlay displays verbatim and the
    // announcement template renders — byte-identical across both twins.
    // Same key, same position as the C# twin (SurfaceSerializer.cs),
    // same commit.
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
            j.raw("{\"query\":").str(query)
                .raw(",\"summary\":").str(rs.summary)
                .raw(",\"rows\":[")
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

    /// The style id the harness renders with: the configured default
    /// style's file stem (core matches ids, not paths). Mirrors
    /// SurfaceSerializer.CitationStyleId.
    private static func citationStyleId(session: VaultSession) throws -> String {
        guard let defaultStyle = session.citationsPrefs().defaultStyle,
              !defaultStyle.isEmpty
        else { return "" }
        return (defaultStyle as NSString).deletingPathExtension
    }

    private static func appendCitation(
        _ j: CanonicalJson, _ reference: CitationReference, _ rendered: RenderedCitation
    ) {
        j.raw("{\"raw\":").str(reference.raw)
            .raw(",\"line\":").num(UInt64(reference.line))
            .raw(",\"offset\":").num(UInt64(reference.byteOffset))
            .raw(",\"items\":[")
        for (i, item) in reference.citations.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"key\":").str(item.key)
                .raw(",\"mode\":").str(citationModeToken(item.mode))
                .raw(",\"locator\":")
            if let locator = item.locator {
                j.raw("{\"label\":").str(locator.label)
                    .raw(",\"value\":").str(locator.value)
                    .raw("}")
            } else {
                _ = j.null()
            }
            j.raw(",\"prefix\":")
            appendOptionalString(j, item.prefix)
            j.raw(",\"suffix\":")
            appendOptionalString(j, item.suffix)
            j.raw("}")
        }
        j.raw("]")
            .raw(",\"rendered\":{\"visual\":").str(rendered.visualText)
            .raw(",\"speech\":").str(rendered.speechText)
            .raw(",\"style_id\":").str(rendered.styleId)
            .raw(",\"bib_key\":")
        appendOptionalString(j, rendered.bibEntry?.key)
        j.raw("}}")
    }

    private static func appendOptionalString(_ j: CanonicalJson, _ value: String?) {
        if let value {
            _ = j.str(value)
        } else {
            _ = j.null()
        }
    }

    /// Stable wire tokens for the citation mode — the enum's Swift
    /// spelling is a binding detail, the token is the contract.
    private static func citationModeToken(_ mode: CitationMode) -> String {
        switch mode {
        case .bracketed: return "bracketed"
        case .inText: return "in_text"
        case .suppressAuthor: return "suppress_author"
        }
    }

    private static func bibFormatToken(_ format: BibFormat) -> String {
        switch format {
        case .bibTeX: return "bibtex"
        case .bibLaTeX: return "biblatex"
        case .cslJson: return "csl_json"
        }
    }

    /// W4-5: the vault-wide citation artifact — mirrors
    /// SurfaceSerializer.BibliographyArtifact. `raw_csl_json` is excluded
    /// (serde field ordering is a serializer contract, not a citation
    /// one); `abstract_text` rides as `abstract_present` because the DB
    /// read path hardcodes it absent while the in-memory path fills it.
    /// Style PATHS are excluded as machine-dependent; ids and titles are
    /// not. Entry and unresolved ORDER is core's, emitted as given.
    private static func bibliographyArtifact(
        session: VaultSession, loadWarnings: [BibLoadWarning]
    ) throws -> String {
        let j = CanonicalJson()
        let prefs = session.citationsPrefs()

        j.raw("{\"prefs\":{\"sources\":[")
        for (i, source) in prefs.sources.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"path\":").str(slash(source.path))
                .raw(",\"format\":").str(bibFormatToken(source.format))
                .raw(",\"watch\":").bool(source.watch)
                .raw("}")
        }
        j.raw("],\"default_style\":")
        appendOptionalString(j, prefs.defaultStyle)
        j.raw(",\"additional_styles\":[")
        for (i, style) in prefs.additionalStyles.enumerated() {
            if i > 0 { j.raw(",") }
            _ = j.str(style)
        }
        j.raw("]}")

        j.raw(",\"load_warnings\":[")
        for (i, warning) in loadWarnings.enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"source\":").str(slash(warning.sourcePath))
                .raw(",\"message\":").str(warning.message)
                .raw("}")
        }
        j.raw("]")

        j.raw(",\"styles\":[")
        for (i, style) in (try session.listCslStyles()).enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"id\":").str(style.id)
                .raw(",\"title\":").str(style.title)
                .raw("}")
        }
        j.raw("]")

        j.raw(",\"entries\":[")
        for (i, e) in (try session.getBibliographyEntries()).enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"key\":").str(e.key)
                .raw(",\"item_type\":").str(e.itemType)
                .raw(",\"title\":").str(e.title)
                .raw(",\"authors\":[")
            for (a, author) in e.authors.enumerated() {
                if a > 0 { j.raw(",") }
                j.raw("{\"family\":").str(author.family)
                    .raw(",\"given\":")
                appendOptionalString(j, author.given)
                j.raw("}")
            }
            j.raw("],\"year\":")
            if let year = e.year {
                _ = j.num(Int64(year))
            } else {
                _ = j.null()
            }
            j.raw(",\"journal\":")
            appendOptionalString(j, e.journal)
            j.raw(",\"doi\":")
            appendOptionalString(j, e.doi)
            j.raw(",\"url\":")
            appendOptionalString(j, e.url)
            j.raw(",\"publisher\":")
            appendOptionalString(j, e.publisher)
            j.raw(",\"abstract_present\":").bool(e.abstractText != nil)
                .raw("}")
        }
        j.raw("]")

        j.raw(",\"unresolved\":[")
        for (i, u) in (try session.listUnresolvedCitations()).enumerated() {
            if i > 0 { j.raw(",") }
            j.raw("{\"path\":").str(slash(u.path))
                .raw(",\"key\":").str(u.key)
                .raw("}")
        }
        j.raw("]}")
        return j.output + "\n"
    }

    // MARK: - Canvas structural queries (W6-1 PR 0b, §W-A)

    /// Vault-level artifact: the W6-1 PR 0b structural queries over
    /// every canvas fixture — bounds, then per node in reading order
    /// its speakable name, parent, children and traced path, then the
    /// pinned filter results and relative descriptions.
    ///
    /// Twin of `SurfaceSerializer.CanvasQueriesArtifact` plus the vault
    /// setup `Program.CanvasQueries` does; both are mirrored here
    /// because this test is the whole mac-side harness (there is no mac
    /// CLI).
    private static func canvasQueriesArtifact() throws -> String {
        let fm = FileManager.default
        let canvasFiles = try fm.contentsOfDirectory(atPath: canvasFixturesDir.path)
            .filter { $0.hasSuffix(".canvas") && !canvasArtifactExclusions.contains($0) }
            .sorted { Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16)) }
        XCTAssertFalse(
            canvasFiles.isEmpty, "no .canvas fixtures at \(canvasFixturesDir.path)")

        let vaultRoot = fm.temporaryDirectory
            .appendingPathComponent("parity-canvas-\(UUID().uuidString)")
        try fm.createDirectory(at: vaultRoot, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: vaultRoot) }
        for f in canvasFiles {
            try fm.copyItem(
                at: canvasFixturesDir.appendingPathComponent(f),
                to: vaultRoot.appendingPathComponent(f))
        }
        let session = try VaultSession.openFilesystem(rootPath: vaultRoot.path)
        let cancel = CancelToken()
        _ = try session.scanInitial(cancel: cancel)

        let j = CanonicalJson()
        j.raw("{\"canvases\":[")
        for (f, rel) in canvasFiles.enumerated() {
            if f > 0 { j.raw(",") }
            let info = try session.openCanvas(path: rel)
            defer { session.closeCanvas(handle: info.handle) }

            j.raw("{\"file\":").str(slash(rel))
                .raw(",\"degraded\":").bool(info.degraded)
                .raw(",\"bounds\":")
            if let bounds = try session.canvasBounds(handle: info.handle) {
                appendCanvasRect(j, bounds)
            } else {
                _ = j.null()
            }

            j.raw(",\"nodes\":[")
            for (i, row) in (try session.canvasOutline(handle: info.handle)).enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"node_id\":").str(row.nodeId)
                    .raw(",\"speakable_name\":").str(row.speakableName)
                    .raw(",\"parent\":")
                let parent = try session.canvasParentOf(
                    handle: info.handle, nodeId: row.nodeId)
                appendOptionalString(j, parent)

                j.raw(",\"children\":[")
                let children = try session.canvasChildrenOf(
                    handle: info.handle, groupId: row.nodeId)
                for (c, child) in children.enumerated() {
                    if c > 0 { j.raw(",") }
                    j.str(child)
                }
                j.raw("]")

                j.raw(",\"trace\":[")
                let hops = try session.canvasTracePath(handle: info.handle, nodeId: row.nodeId)
                for (t, hop) in hops.enumerated() {
                    if t > 0 { j.raw(",") }
                    j.raw("{\"edge_id\":").str(hop.edgeId)
                        .raw(",\"node_id\":").str(hop.nodeId)
                        .raw(",\"title\":").str(hop.title)
                        .raw(",\"label\":")
                    appendOptionalString(j, hop.label)
                    j.raw("}")
                }
                j.raw("]}")
            }
            j.raw("]")

            j.raw(",\"filters\":[")
            for (q, query) in pinnedCanvasFilterQueries.enumerated() {
                if q > 0 { j.raw(",") }
                j.raw("{\"query\":").str(query)
                    .raw(",\"matched\":[")
                let matched = try session.canvasFilter(handle: info.handle, query: query)
                for (m, nodeId) in matched.enumerated() {
                    if m > 0 { j.raw(",") }
                    j.str(nodeId)
                }
                j.raw("]}")
            }
            j.raw("]")

            j.raw(",\"relative\":[")
            for (r, rect) in pinnedCanvasRelativeRects.enumerated() {
                if r > 0 { j.raw(",") }
                j.raw("{\"rect\":")
                appendCanvasRect(j, rect)
                j.raw(",\"descs\":[")
                let descs = try session.canvasDescribeRelative(
                    handle: info.handle, rect: rect, exclude: [])
                for (d, desc) in descs.enumerated() {
                    if d > 0 { j.raw(",") }
                    j.raw("{\"kind\":").str(relativeDescName(desc))
                        .raw(",\"anchor\":").str(relativeDescAnchor(desc))
                        .raw("}")
                }
                j.raw("]}")
            }
            j.raw("]}")
        }
        j.raw("]}")
        return j.output + "\n"
    }

    // MARK: - W6-1 PR A: the canvas_read section (contract A20)

    /// The READ side of the canvas surface — the Swift twin of
    /// `SurfaceSerializer.CanvasReadArtifact`. Same corpus, same
    /// exclusions and its own temp vault, exactly as `canvas_queries`
    /// (0b-15): the open info with its warnings, then the three
    /// projections PR A/B/D consume, then per node in reading order the
    /// two per-node reads the outline and the navigator make.
    ///
    /// Every emission below mirrors the C# one key for key and in the
    /// same order; the committed golden arbitrates, and a drift in
    /// either direction fails `testHarnessArtifactsMatchCommittedGoldensByteForByte`
    /// on whichever lane runs second.
    private static func canvasReadArtifact() throws -> String {
        let fm = FileManager.default
        let canvasFiles = try fm.contentsOfDirectory(atPath: canvasFixturesDir.path)
            .filter { $0.hasSuffix(".canvas") && !canvasArtifactExclusions.contains($0) }
            .sorted { Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16)) }
        XCTAssertFalse(
            canvasFiles.isEmpty, "no .canvas fixtures at \(canvasFixturesDir.path)")

        let vaultRoot = fm.temporaryDirectory
            .appendingPathComponent("parity-canvas-read-\(UUID().uuidString)")
        try fm.createDirectory(at: vaultRoot, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: vaultRoot) }
        for f in canvasFiles {
            try fm.copyItem(
                at: canvasFixturesDir.appendingPathComponent(f),
                to: vaultRoot.appendingPathComponent(f))
        }
        let session = try VaultSession.openFilesystem(rootPath: vaultRoot.path)
        let cancel = CancelToken()
        _ = try session.scanInitial(cancel: cancel)

        let j = CanonicalJson()
        j.raw("{\"canvases\":[")
        for (f, rel) in canvasFiles.enumerated() {
            if f > 0 { j.raw(",") }
            let info = try session.openCanvas(path: rel)
            defer { session.closeCanvas(handle: info.handle) }

            j.raw("{\"file\":").str(slash(rel))
                .raw(",\"node_count\":").num(UInt64(info.nodeCount))
                .raw(",\"edge_count\":").num(UInt64(info.edgeCount))
                .raw(",\"degraded\":").bool(info.degraded)
                .raw(",\"warnings\":[")
            for (w, warning) in info.warnings.enumerated() {
                if w > 0 { j.raw(",") }
                j.raw("{\"kind\":").str(loadWarningKindName(warning.kind))
                    .raw(",\"detail\":").str(warning.detail)
                    .raw("}")
            }
            j.raw("]")

            // A degraded open has nothing worth reading: core returns an
            // empty canvas and the host releases the handle at once
            // (contract A3, CD-28). The sections stay present and EMPTY
            // rather than absent, so the artifact's shape never varies
            // with the fixture.
            var outline: [CanvasOutlineRow] = []
            var tableRows: [CanvasTableRow] = []
            var scene = CanvasScene(nodes: [], edges: [])
            if !info.degraded {
                outline = try session.canvasOutline(handle: info.handle)
                tableRows = try session.canvasTableRows(handle: info.handle)
                scene = try session.canvasScene(handle: info.handle)
            }

            j.raw(",\"outline\":[")
            for (i, row) in outline.enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"node_id\":").str(row.nodeId)
                    .raw(",\"depth\":").num(UInt64(row.depth))
                    .raw(",\"kind\":").str(row.kind)
                    .raw(",\"title\":").str(row.title)
                    .raw(",\"speakable_name\":").str(row.speakableName)
                    .raw(",\"group_path\":")
                appendStrings(j, row.groupPath)
                j.raw(",\"ordinal_n\":").num(UInt64(row.ordinalN))
                    .raw(",\"total_m\":").num(UInt64(row.totalM))
                    .raw(",\"connection_count\":").num(UInt64(row.connectionCount))
                    .raw(",\"color_name\":")
                appendOptionalString(j, row.colorName)
                j.raw("}")
            }
            j.raw("]")

            j.raw(",\"table\":[")
            for (i, row) in tableRows.enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"node_id\":").str(row.nodeId)
                    .raw(",\"kind\":").str(row.kind)
                    .raw(",\"title\":").str(row.title)
                    .raw(",\"speakable_name\":").str(row.speakableName)
                    .raw(",\"group_path\":")
                appendStrings(j, row.groupPath)
                j.raw(",\"target\":").str(row.target)
                    .raw(",\"connection_count\":").num(UInt64(row.connectionCount))
                    .raw(",\"color_name\":")
                appendOptionalString(j, row.colorName)
                j.raw("}")
            }
            j.raw("]")

            j.raw(",\"scene\":{\"nodes\":[")
            for (i, node) in scene.nodes.enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"node_id\":").str(node.nodeId)
                    .raw(",\"kind\":").str(node.kind)
                    .raw(",\"title\":").str(node.title)
                    .raw(",\"speakable_name\":").str(node.speakableName)
                    .raw(",\"x\":").num(node.x)
                    .raw(",\"y\":").num(node.y)
                    .raw(",\"width\":").num(node.width)
                    .raw(",\"height\":").num(node.height)
                    .raw(",\"color\":")
                appendOptionalString(j, node.color)
                j.raw(",\"color_name\":")
                appendOptionalString(j, node.colorName)
                j.raw(",\"subpath\":")
                appendOptionalString(j, node.subpath)
                j.raw("}")
            }
            j.raw("],\"edges\":[")
            for (i, edge) in scene.edges.enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"edge_id\":").str(edge.edgeId)
                    .raw(",\"from_node\":").str(edge.fromNode)
                    .raw(",\"from_side\":")
                appendSide(j, edge.fromSide)
                j.raw(",\"to_node\":").str(edge.toNode)
                    .raw(",\"to_side\":")
                appendSide(j, edge.toSide)
                j.raw(",\"from_arrow\":").bool(edge.fromArrow)
                    .raw(",\"to_arrow\":").bool(edge.toArrow)
                    .raw(",\"label\":")
                appendOptionalString(j, edge.label)
                j.raw(",\"color\":")
                appendOptionalString(j, edge.color)
                j.raw("}")
            }
            j.raw("]}")

            j.raw(",\"nodes\":[")
            for (i, row) in outline.enumerated() {
                if i > 0 { j.raw(",") }
                j.raw("{\"node_id\":").str(row.nodeId)
                    .raw(",\"where_am_i\":")
                appendWhereAmI(j, session, info.handle, row.nodeId)
                j.raw(",\"neighbors\":[")
                let neighbors = try session.canvasNeighbors(
                    handle: info.handle, nodeId: row.nodeId)
                for (n, neighbor) in neighbors.enumerated() {
                    if n > 0 { j.raw(",") }
                    j.raw("{\"edge_id\":").str(neighbor.edgeId)
                        .raw(",\"other_node\":").str(neighbor.otherNode)
                        .raw(",\"other_title\":").str(neighbor.otherTitle)
                        .raw(",\"direction\":").str(edgeDirectionName(neighbor.direction))
                        .raw(",\"self_side\":")
                    appendSide(j, neighbor.selfSide)
                    j.raw(",\"label\":")
                    appendOptionalString(j, neighbor.label)
                    j.raw(",\"self_is_from\":").bool(neighbor.selfIsFrom)
                        .raw("}")
                }
                j.raw("]}")
            }
            j.raw("]}")
        }
        j.raw("]}")
        return j.output + "\n"
    }

    /// Where-am-I is MODEL-backed, so under the 0b-6 skew it refuses for
    /// an id the SQLite-served outline rows still name. The artifact has
    /// to be able to express that shape, so a refusal serializes as
    /// `null` rather than aborting the run (contracts A16/A20) — the C#
    /// side's `catch (VaultException)` arm.
    private static func appendWhereAmI(
        _ j: CanonicalJson, _ session: VaultSession, _ handle: UInt64, _ nodeId: String
    ) {
        guard let context = try? session.canvasWhereAmI(handle: handle, nodeId: nodeId) else {
            _ = j.null()
            return
        }
        j.raw("{\"title\":").str(context.title)
            .raw(",\"speakable_name\":").str(context.speakableName)
            .raw(",\"kind\":").str(context.kind)
            .raw(",\"group_path\":")
        appendStrings(j, context.groupPath)
        j.raw(",\"ordinal_n\":").num(UInt64(context.ordinalN))
            .raw(",\"total_m\":").num(UInt64(context.totalM))
            .raw(",\"connection_count\":").num(UInt64(context.connectionCount))
            .raw(",\"in_count\":").num(UInt64(context.inCount))
            .raw(",\"out_count\":").num(UInt64(context.outCount))
            .raw(",\"color_name\":")
        appendOptionalString(j, context.colorName)
        j.raw("}")
    }

    private static func appendStrings(_ j: CanonicalJson, _ values: [String]) {
        j.raw("[")
        for (i, value) in values.enumerated() {
            if i > 0 { j.raw(",") }
            j.str(value)
        }
        j.raw("]")
    }

    private static func appendSide(_ j: CanonicalJson, _ side: CanvasSide?) {
        if let side {
            _ = j.str(sideName(side))
        } else {
            _ = j.null()
        }
    }

    private static func sideName(_ side: CanvasSide) -> String {
        switch side {
        case .top: return "top"
        case .right: return "right"
        case .bottom: return "bottom"
        case .left: return "left"
        }
    }

    private static func edgeDirectionName(_ direction: CanvasEdgeDirection) -> String {
        switch direction {
        case .outgoing: return "outgoing"
        case .incoming: return "incoming"
        case .bidirectional: return "bidirectional"
        case .undirected: return "undirected"
        }
    }

    private static func loadWarningKindName(_ kind: CanvasLoadWarningKind) -> String {
        switch kind {
        case .parseFailed: return "parse_failed"
        case .skippedEntry: return "skipped_entry"
        case .danglingEdge: return "dangling_edge"
        case .ignoredValue: return "ignored_value"
        }
    }

    private static func relativeDescName(_ desc: CanvasRelativeDesc) -> String {
        switch desc {
        case .below: return "below"
        case .rightOf: return "right_of"
        case .above: return "above"
        case .leftOf: return "left_of"
        case .atOrigin: return "at_origin"
        }
    }

    private static func relativeDescAnchor(_ desc: CanvasRelativeDesc) -> String {
        switch desc {
        case .below(let anchorTitle): return anchorTitle
        case .rightOf(let anchorTitle): return anchorTitle
        case .above(let anchorTitle): return anchorTitle
        case .leftOf(let anchorTitle): return anchorTitle
        case .atOrigin: return ""
        }
    }

    private static func appendCanvasRect(_ j: CanonicalJson, _ rect: CanvasRect) {
        j.raw("{\"x\":").num(rect.x)
            .raw(",\"y\":").num(rect.y)
            .raw(",\"width\":").num(rect.width)
            .raw(",\"height\":").num(rect.height)
            .raw("}")
    }

    private static func slash(_ path: String) -> String {
        path.replacingOccurrences(of: "\\", with: "/")
    }

    /// W6-1 §H TH-3 (H3): the read half of §W-A cannot skip a canvas
    /// fixture in silence on this lane either — every
    /// `fixtures/canvas/*.canvas` not in `canvasArtifactExclusions` is an
    /// entry of BOTH committed canvas artifacts, and every exclusion names
    /// a fixture that exists. The Windows twin is
    /// `ParityHarnessCensus.EveryCanvasFixtureIsReadOrExcluded`.
    func testEveryCanvasFixtureIsReadOrExcluded() throws {
        let fixtures = try FileManager.default
            .contentsOfDirectory(atPath: Self.canvasFixturesDir.path)
            .filter { $0.hasSuffix(".canvas") }
            .sorted { Array($0.utf16).lexicographicallyPrecedes(Array($1.utf16)) }
        XCTAssertFalse(fixtures.isEmpty)
        for excluded in Self.canvasArtifactExclusions {
            XCTAssertTrue(fixtures.contains(excluded), "the exclusion \(excluded) names no fixture")
        }
        for artifact in ["canvas_read.json", "canvas_queries.json"] {
            let data = try Data(contentsOf: Self.goldenDir.appendingPathComponent(artifact))
            guard let root = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                let canvases = root["canvases"] as? [[String: Any]]
            else {
                XCTFail("\(artifact) has no canvases array")
                return
            }
            let listed = Set(canvases.compactMap { $0["file"] as? String })
            for fixture in fixtures {
                let excluded = Self.canvasArtifactExclusions.contains(fixture)
                XCTAssertEqual(
                    listed.contains(fixture), !excluded,
                    excluded
                        ? "\(artifact) lists the excluded fixture \(fixture)"
                        : "\(artifact) skips the fixture \(fixture) in silence")
            }
        }
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

    /// Signed twin of `num(UInt64)` — the C# side's `Num(long)`
    /// (invariant decimal). Task due/scheduled epoch millis and the
    /// signed priority scale need it (W4-3).
    @discardableResult
    func num(_ value: Int64) -> CanonicalJson {
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
