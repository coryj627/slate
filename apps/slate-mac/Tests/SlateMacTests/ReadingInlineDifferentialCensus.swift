// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Accessibility
import Foundation
import SwiftUI
import XCTest

@testable import SlateMac

/// The old-vs-new runtime differential census `w3_inline_runs_spec.md` §9
/// item 3 calls for (#967).
///
/// #1042 shipped the canonical pipeline with a deltas ledger built by
/// reading the retired implementation line by line. A ledger built by
/// inspection can only contain the differences its author thought to look
/// for. This census exists to find the ones nobody predicted: it drives
/// the FROZEN retired pipeline (`LegacyReadingInlinePipeline.swift`) and
/// the shipped one over the same inputs and compares what a reader and a
/// screen reader actually receive.
///
/// ## What is asserted, and what is deliberately not
///
/// #967 moved TWO things at once, and they carry different obligations:
///
/// 1. **Slate's own affordances** — which bytes are a wikilink / embed /
///    tag / citation, what each activates with, whether it resolves, what
///    VoiceOver announces. This is what the migration was *for*, and it
///    must not have changed. The **affordance ledger** is asserted on
///    every document, and any difference must map to a ledger row.
/// 2. **Generic CommonMark inline parsing** — `AttributedString(markdown:)`
///    was replaced by pulldown-cmark. That substitution is intentional and
///    the two parsers genuinely disagree (emphasis flanking, entities,
///    escapes, hard breaks, raw HTML). Asserting character equality across
///    that boundary would force an allow-list broad enough to swallow real
///    findings, which is worse than not asserting it: the honest owner of
///    pulldown's conformance is the core CommonMark suite, not this file.
///    So text differences on documents containing generic markdown are
///    **reported**, not asserted.
///
/// The gap that leaves is closed from the other side: documents built
/// only from plain words and Slate constructs — where the two parsers
/// have nothing to disagree about — assert the **full attribute
/// projection**, character for character.
///
/// ## Reading a failure
///
/// Each test collects every unclassified difference, deduplicates by
/// shape, and fails ONCE with a bounded catalogue. That is deliberate: an
/// `XCTFail` per delta buries the signal, and this test can only run on
/// macOS CI, so one run has to be worth a whole round trip. Set
/// `SLATE_DIFFERENTIAL_REPORT=1` to also print the classified deltas and
/// the per-row counts.
final class ReadingInlineDifferentialCensus: XCTestCase {

    // MARK: - What a reader and a screen reader actually receive

    /// The observable projection of one rendered block: the text, and the
    /// attributes covering each UTF-16 offset.
    ///
    /// Per-offset rather than per-run on purpose — the run BOUNDARIES
    /// legitimately differ (Foundation coalesces adjacent equal-attribute
    /// runs; core emits its own partition), while what lands on any given
    /// character must not.
    private struct Projection: Equatable {
        var text: String
        var links: [String?]
        var colors: [String?]
        /// The whole line style, not merely "is underlined" — both sides
        /// construct it identically, so a pattern or colour change shows
        /// up rather than passing as still-present.
        var underlines: [String?]
        var intents: [UInt?]
        var axText: [String?]

        static let empty = Projection(
            text: "", links: [], colors: [], underlines: [], intents: [], axText: [])
    }

    /// One activatable range as the user meets it: what it reads as, what
    /// it opens, whether it is styled unresolved, what VoiceOver says.
    private struct Affordance: Equatable {
        var display: String
        var url: String
        var color: String?
        var axText: String?
    }

    private static func project(_ attributed: AttributedString) -> Projection {
        let text = String(attributed.characters)
        let count = text.utf16.count
        var projection = Projection(
            text: text,
            links: [String?](repeating: nil, count: count),
            colors: [String?](repeating: nil, count: count),
            underlines: [String?](repeating: nil, count: count),
            intents: [UInt?](repeating: nil, count: count),
            axText: [String?](repeating: nil, count: count))

        // Runs arrive in order and partition the string, so accumulating
        // each run's UTF-16 length walks the offsets without re-measuring
        // a prefix per run. Attribute keys are spelled explicitly: the
        // dynamic-member forms are ambiguous between the SwiftUI and
        // AppKit scopes, which both sides import.
        var offset = 0
        for run in attributed.runs {
            let body = String(attributed[run.range].characters)
            let length = body.utf16.count
            guard length > 0 else { continue }
            let url = run[AttributeScopes.FoundationAttributes.LinkAttribute.self]?
                .absoluteString
            // Hoisted: `Color` equality per character would dominate the
            // sweep's cost for no extra coverage.
            let color = colorName(
                run[AttributeScopes.SwiftUIAttributes.ForegroundColorAttribute.self])
            let underline = run[
                AttributeScopes.SwiftUIAttributes.UnderlineStyleAttribute.self
            ].map { String(describing: $0) }
            let intent = run[
                AttributeScopes.FoundationAttributes.InlinePresentationIntentAttribute.self
            ]?.rawValue
            let ax = run[
                AttributeScopes.AccessibilityAttributes.TextCustomAttribute.self]?
                .joined(separator: "|")

            for index in offset..<min(offset + length, count) {
                projection.links[index] = url
                projection.colors[index] = color
                projection.underlines[index] = underline
                projection.intents[index] = intent
                projection.axText[index] = ax
            }
            offset += length
        }
        return projection
    }

    /// Name the two roles the pipeline is allowed to use, so a failure
    /// reads as `warning` vs `accent` rather than as two opaque `Color`
    /// descriptions.
    private static func colorName(_ color: Color?) -> String? {
        guard let color else { return nil }
        if color == Tokens.ColorRole.accentText { return "accent" }
        if color == Tokens.ColorRole.warningText { return "warning" }
        return "other(\(color))"
    }

    /// Collapse a projection to its activatable ranges. Adjacent offsets
    /// sharing a link, colour and AX string are one affordance — the same
    /// grouping on both sides, so a merge can never mask a difference in
    /// what a user can act on.
    private static func affordances(_ projection: Projection) -> [Affordance] {
        let utf16 = Array(projection.text.utf16)
        var result: [Affordance] = []
        var index = 0
        while index < utf16.count {
            guard let url = projection.links[index] else {
                index += 1
                continue
            }
            let color = projection.colors[index]
            let ax = projection.axText[index]
            var end = index
            while end < utf16.count, projection.links[end] == url,
                projection.colors[end] == color, projection.axText[end] == ax
            {
                end += 1
            }
            result.append(
                Affordance(
                    display: String(decoding: utf16[index..<end], as: UTF16.self),
                    url: url, color: color, axText: ax))
            index = end
        }
        return result
    }

    // MARK: - The two pipelines

    /// The retired pipeline's rendering of one block — nil for a block it
    /// never inline-mapped.
    ///
    /// The slice derivation is copied from `ReadingView` at 02f9153, not
    /// approximated: heading text stripped of its chrome, list content
    /// after the marker (and after the checkbox for tasks), quote content
    /// stripped to `depth`, paragraph verbatim. Getting this wrong would
    /// make the census compare two different inputs and then report the
    /// difference as a behavior change.
    private static func legacyProjection(
        block: ReadingBlock, citations: [RenderedCitation],
        recordSets: LegacyRouter.LinkRecordSets
    ) -> Projection? {
        let slice: String
        switch block.kind {
        case .heading:
            slice = LegacyBlockSource.headingText(block.source)
        case .paragraph:
            // A paragraph that IS one embed rendered a CARD, never a text
            // leaf — the retired view returned before reaching the mapper.
            guard LegacyInlineMapper.blockEmbedTarget(inSlice: block.source) == nil
            else { return nil }
            slice = block.source
        case .listItem(_, _, let task):
            let parts = LegacyBlockSource.listItemParts(
                block.source, stripTaskBox: task != nil)
            slice = parts?.content ?? block.source
        case .blockQuote(let depth):
            slice = LegacyBlockSource.quoteContent(block.source, depth: depth)
        default:
            return nil
        }
        return project(
            LegacyInlineMapper.map(
                slice: slice, citations: citations, recordSets: recordSets
            ).attributed)
    }

    private static func shippedProjection(_ inline: ReadingBlockInlines) -> Projection? {
        // Same exclusion as above: core reports the block embed key, and
        // the shipped view expands the card instead of applying segments.
        guard inline.blockEmbedKey == nil, let segment = inline.segments.first
        else { return nil }
        return project(ReadingInlineMapper.attributed(segment))
    }

    // MARK: - The deltas ledger, as an allow-list

    /// Rows from #1042's deltas ledger. A difference matching one is
    /// expected and counted; anything else is a finding.
    ///
    /// Each row is a NARROW predicate over the block that produced the
    /// difference. Deliberately narrow: a row broad enough to be safe is
    /// a row that hides the next regression.
    private enum Delta: String, CaseIterable {
        /// Core composes the embed cache key as anchor-cut target +
        /// anchor marker + anchor text (`reading_embed_key`); the retired
        /// mapper used the authored interior verbatim, so `![[Note#^blk]]`
        /// and `![[ Note # Sec ]]` composed keys the resolution
        /// dictionary could not match.
        ///
        /// Scoped to interiors that composition actually normalizes. A
        /// bare `![[Note]]` whose key differs is NOT this row — the two
        /// compositions are identical there, so a difference would be a
        /// regression.
        case embedKeyComposition

        /// `[m](note^block)`: the retired router percent-encoded the
        /// destination into the routing URL, so a `^` path could never
        /// match its own record. Core carries the destination verbatim
        /// under the markdown-grammar scheme.
        case markdownCaretPath

        /// A Slate token swallowed by a code span or raw HTML: the
        /// retired pipeline spliced markdown into the slice and the
        /// tighter construct captured the splice, leaking
        /// `[label](slate-wiki://…)` into the rendered text. Core never
        /// splices, so the authored bytes render.
        case spliceLeak

        /// `[[]]` / `[[ ]]`: an empty interior produced an activatable
        /// run with an empty target in the retired mapper.
        case emptyWikilinkInterior

        func explains(source: String, old: Projection, new: Projection) -> Bool {
            switch self {
            case .embedKeyComposition:
                guard let interior = embedInterior(of: source) else { return false }
                return interior.contains("#") || interior.contains("^")
                    || interior.contains("|")
                    || interior != interior.trimmingCharacters(in: .whitespaces)
            case .markdownCaretPath:
                // The `^` must be in the DESTINATION. A `^` anywhere in
                // the block would also match a wikilink's block anchor,
                // which this row does not cover.
                return markdownDestinations(of: source).contains { $0.contains("^") }
            case .spliceLeak:
                return [
                    "slate-wiki://", "slate-wikimd://", "slate-embed://",
                    "slate-tag://", "slate-cite://",
                ].contains { old.text.contains($0) && !new.text.contains($0) }
            case .emptyWikilinkInterior:
                return source.contains("[[]]") || source.contains("[[ ]]")
            }
        }

        /// The authored bytes between `![[` and the next `]]`, or nil if
        /// the block holds no embed. A string scan is fine HERE — this is
        /// a test classifying a difference after the fact, not the
        /// pipeline deciding what an embed is.
        private func embedInterior(of source: String) -> String? {
            guard let open = source.range(of: "![["),
                let close = source.range(
                    of: "]]", range: open.upperBound..<source.endIndex)
            else { return nil }
            return String(source[open.upperBound..<close.lowerBound])
        }

        /// Every `](…)` destination in the block. Same caveat as above: a
        /// crude scan, used only to classify a difference that already
        /// happened.
        private func markdownDestinations(of source: String) -> [String] {
            var destinations: [String] = []
            var cursor = source.startIndex
            while let open = source.range(
                of: "](", range: cursor..<source.endIndex)
            {
                guard
                    let close = source.range(
                        of: ")", range: open.upperBound..<source.endIndex)
                else { break }
                destinations.append(
                    String(source[open.upperBound..<close.lowerBound]))
                cursor = close.upperBound
            }
            return destinations
        }
    }

    // MARK: - Collecting findings

    /// Findings are deduplicated by SHAPE — which dimensions differed,
    /// plus which constructs the block contains — so a randomized sweep
    /// reports a handful of distinct problems rather than thousands of
    /// instances of one.
    private struct Catalogue {
        var counts: [Delta: Int] = [:]
        var findings: [String: String] = [:]
        var findingCount = 0

        /// Blocks where BOTH pipelines produced a projection, and the
        /// total characters those projections covered.
        ///
        /// Without these a green run is ambiguous: "the two pipelines
        /// agree" and "the census compared nothing" look identical. The
        /// tests assert a floor, so a change that silently stops
        /// exercising a pipeline fails instead of passing quietly.
        var comparedBlocks = 0
        var comparedCharacters = 0

        mutating func compared(characters: Int) {
            comparedBlocks += 1
            comparedCharacters += characters
        }

        mutating func classified(_ delta: Delta) {
            counts[delta, default: 0] += 1
        }

        mutating func unclassified(shape: String, detail: String) {
            findingCount += 1
            if findings[shape] == nil { findings[shape] = detail }
        }
    }

    /// Which constructs a block contains — the second half of the dedup
    /// key, and usually enough on its own to name the cause.
    private static func shape(of source: String, dimensions: [String]) -> String {
        let markers: [(String, String)] = [
            ("![[", "embed"), ("[[", "wiki"), ("](", "mdlink"), ("[@", "cite"),
            ("#", "hash"), ("`", "code"), ("~~", "strike"), ("**", "strong"),
            ("*", "em"), ("_", "underscore"), ("\\", "escape"), ("&", "entity"),
            ("<", "html"), ("\u{FFFC}", "u+fffc"),
        ]
        var present: [String] = []
        for (needle, name) in markers where source.contains(needle) {
            present.append(name)
        }
        return "[\(dimensions.joined(separator: ","))] \(present.joined(separator: "+"))"
    }

    private static func dimensions(old: Projection, new: Projection) -> [String] {
        var differing: [String] = []
        if old.text != new.text { differing.append("text") }
        if old.links != new.links { differing.append("link") }
        if old.colors != new.colors { differing.append("color") }
        if old.underlines != new.underlines { differing.append("underline") }
        if old.intents != new.intents { differing.append("intent") }
        if old.axText != new.axText { differing.append("ax") }
        return differing
    }

    private static func detail(
        source: String, old: Projection, new: Projection
    ) -> String {
        """
          source:   \(source.debugDescription)
          old text: \(old.text.debugDescription)
          new text: \(new.text.debugDescription)
          old affordances: \(affordances(old))
          new affordances: \(affordances(new))
        """
    }

    // MARK: - The comparison

    /// - Parameter strictText: assert the full projection (quiet
    ///   documents), or only the affordance ledger (documents containing
    ///   generic markdown, where the two inline parsers legitimately
    ///   disagree).
    private static func compare(
        source: String, citations: [RenderedCitation], records: [OutgoingLink],
        strictText: Bool, into catalogue: inout Catalogue
    ) {
        let blocks = readingBlocksSource(source: source)
        let inlines = readingInlineSegmentsSource(
            source: source, citations: citations, records: records)
        let recordSets = LegacyRouter.LinkRecordSets(records: records)
        let report =
            ProcessInfo.processInfo.environment["SLATE_DIFFERENTIAL_REPORT"] == "1"

        for (index, block) in blocks.enumerated() {
            guard index < inlines.count else { continue }
            let inline = inlines[index]

            // Block-embed detection first: it is a DECISION about how the
            // block renders, so a disagreement here means the two
            // pipelines put the user in front of different UI, whatever
            // the inline attributes say.
            //
            // A disagreement here is reported ONCE. When the two sides
            // disagree about whether the block IS an embed, exactly one
            // of them inline-maps it, and the `mapped` check below would
            // otherwise report the same fact a second time.
            var embedDecisionReported = false
            if case .paragraph = block.kind {
                let legacyKey = LegacyInlineMapper.blockEmbedTarget(inSlice: block.source)
                if legacyKey != inline.blockEmbedKey {
                    embedDecisionReported = true
                    if Delta.embedKeyComposition.explains(
                        source: block.source, old: .empty, new: .empty)
                    {
                        catalogue.classified(.embedKeyComposition)
                        if report {
                            print("embedKeyComposition: \(block.source.debugDescription)")
                        }
                    } else {
                        catalogue.unclassified(
                            shape: shape(of: block.source, dimensions: ["embedkey"]),
                            detail: """
                                  source:  \(block.source.debugDescription)
                                  old key: \(String(describing: legacyKey))
                                  new key: \(String(describing: inline.blockEmbedKey))
                                """)
                    }
                }
            }

            let old = legacyProjection(
                block: block, citations: citations, recordSets: recordSets)
            let new = shippedProjection(inline)

            // One side inline-mapped the block and the other did not —
            // always a finding: it means they disagree about whether this
            // block is text at all.
            guard let old, let new else {
                if (old == nil) != (new == nil), !embedDecisionReported {
                    catalogue.unclassified(
                        shape: shape(of: block.source, dimensions: ["mapped"]),
                        detail: """
                              source: \(block.source.debugDescription)
                              inline-mapped by old: \(old != nil), by new: \(new != nil)
                            """)
                }
                continue
            }

            catalogue.compared(characters: old.text.utf16.count)

            let differing =
                strictText
                ? dimensions(old: old, new: new)
                : (affordances(old) == affordances(new) ? [] : ["affordance"])
            guard !differing.isEmpty else { continue }

            if let delta = Delta.allCases.first(where: {
                $0.explains(source: block.source, old: old, new: new)
            }) {
                catalogue.classified(delta)
                if report {
                    print("\(delta.rawValue): \(block.source.debugDescription)")
                }
                continue
            }
            catalogue.unclassified(
                shape: shape(of: block.source, dimensions: differing),
                detail: detail(source: block.source, old: old, new: new))
        }
    }

    /// Fail once, with a bounded catalogue. `maximumShapes` keeps a
    /// pathological sweep from producing a CI log nobody reads.
    private func assertNoUnclassified(
        _ catalogue: Catalogue, label: String, minimumBlocks: Int,
        maximumShapes: Int = 25, line: UInt = #line
    ) {
        if ProcessInfo.processInfo.environment["SLATE_DIFFERENTIAL_REPORT"] == "1" {
            print("--- differential census (\(label)) ---")
            print(
                "  compared: \(catalogue.comparedBlocks) block(s), "
                    + "\(catalogue.comparedCharacters) character(s)")
            for delta in Delta.allCases {
                print("  \(delta.rawValue): \(catalogue.counts[delta] ?? 0)")
            }
            print(
                "  unclassified: \(catalogue.findingCount) in "
                    + "\(catalogue.findings.count) shape(s)")
        }

        // Coverage floor FIRST: "the pipelines agree" and "nothing was
        // compared" both produce an empty findings set, and only this
        // tells them apart.
        XCTAssertGreaterThan(
            catalogue.comparedBlocks, minimumBlocks,
            "[\(label)] census compared \(catalogue.comparedBlocks) blocks — too few to "
                + "mean anything. Either the corpus stopped producing inline blocks or one "
                + "pipeline stopped returning projections.",
            line: line)
        XCTAssertGreaterThan(
            catalogue.comparedCharacters, catalogue.comparedBlocks,
            "[\(label)] compared blocks are essentially empty", line: line)

        guard !catalogue.findings.isEmpty else { return }

        let shapes = catalogue.findings.keys.sorted()
        let shown = shapes.prefix(maximumShapes)
        var message = """
            \(catalogue.findingCount) unclassified difference(s) between the retired \
            and shipped inline pipelines, in \(catalogue.findings.count) distinct \
            shape(s) [\(label)].

            Every difference must map to a `Delta` row from #1042's deltas ledger. \
            If a shape below is an INTENDED change, add the row with its reason; if \
            it is not, it is a regression the migration introduced.


            """
        for key in shown {
            message += "\(key)\n\(catalogue.findings[key] ?? "")\n\n"
        }
        if shapes.count > shown.count {
            message += "… and \(shapes.count - shown.count) more shape(s) not shown.\n"
        }
        XCTFail(message, line: line)
    }

    // MARK: - Censuses

    /// The shared §W-A markdown corpus — the same fixtures both parity
    /// twins serialize, so a delta here is a delta the goldens carry.
    /// Hand-authored prose, so the loud rules apply.
    func testCorpusAffordancesAreUnchanged() throws {
        var catalogue = Catalogue()
        let names = try FileManager.default
            .contentsOfDirectory(atPath: Self.fixturesDir.path)
            .filter { $0.hasSuffix(".md") }
            .sorted()
        XCTAssertFalse(names.isEmpty, "no fixtures at \(Self.fixturesDir.path)")

        for name in names {
            let text = try String(
                contentsOf: Self.fixturesDir.appendingPathComponent(name),
                encoding: .utf8)
            Self.compare(
                source: text, citations: Self.citations, records: Self.records,
                strictText: false, into: &catalogue)
        }
        assertNoUnclassified(catalogue, label: "corpus", minimumBlocks: 20)
    }

    /// Randomized documents built ONLY from plain words and Slate
    /// constructs: the two inline parsers have nothing to disagree about,
    /// so the full projection must match character for character.
    func testQuietDocumentsRenderIdentically() {
        var catalogue = Catalogue()
        for seed in 0..<Self.documentCount {
            var rng = SplitMix64(seed: UInt64(seed))
            Self.compare(
                source: Self.document(&rng, pool: Self.quietFragments),
                citations: Self.citations, records: Self.records,
                strictText: true, into: &catalogue)
        }
        assertNoUnclassified(catalogue, label: "quiet", minimumBlocks: 1_000)
    }

    /// Randomized documents mixing Slate constructs with generic
    /// CommonMark. Text may legitimately differ (pulldown-cmark replaced
    /// `AttributedString(markdown:)`); the affordance ledger may not.
    func testLoudDocumentsKeepTheirAffordances() {
        var catalogue = Catalogue()
        for seed in 0..<Self.documentCount {
            var rng = SplitMix64(seed: UInt64(seed) &+ 1_000_000)
            Self.compare(
                source: Self.document(&rng, pool: Self.loudFragments),
                citations: Self.citations, records: Self.records,
                strictText: false, into: &catalogue)
        }
        assertNoUnclassified(catalogue, label: "loud", minimumBlocks: 1_000)
    }

    /// The same quiet sweep with NO records: every wikilink classifies
    /// unresolved on both sides, which is the state a note renders in
    /// before its link index has caught up — the window #849 is about.
    func testUnresolvedTreatmentIsUnchangedWithoutRecords() {
        var catalogue = Catalogue()
        for seed in 0..<Self.documentCount {
            var rng = SplitMix64(seed: UInt64(seed) &+ 2_000_000)
            Self.compare(
                source: Self.document(&rng, pool: Self.quietFragments),
                citations: Self.citations, records: [], strictText: true,
                into: &catalogue)
        }
        assertNoUnclassified(catalogue, label: "no records", minimumBlocks: 1_000)
    }

    // MARK: - Proving the census can fail

    /// Every dimension the census compares must be able to REPORT a
    /// difference. A census whose comparator is inert passes for the same
    /// reason a correct one does, and nothing else in this file
    /// distinguishes the two.
    ///
    /// Built from real pipeline output, then perturbed one dimension at a
    /// time — so this also pins that the fixture actually exercises each
    /// dimension (an all-plain-text fixture would make the perturbations
    /// vacuous, and the `XCTAssertTrue`s below catch that).
    func testComparatorReportsEveryDimensionItCompares() {
        let source = "A **bold** [[gone]] link and [@smith2020].\n"
        let inlines = readingInlineSegmentsSource(
            source: source, citations: Self.citations, records: Self.records)
        guard let segment = inlines.first?.segments.first else {
            return XCTFail("fixture produced no inline segment")
        }
        let base = Self.project(ReadingInlineMapper.attributed(segment))

        // The fixture must actually carry each attribute, or perturbing
        // it proves nothing.
        XCTAssertFalse(base.text.isEmpty)
        XCTAssertTrue(base.links.contains { $0 != nil }, "fixture has no activatable run")
        XCTAssertTrue(base.colors.contains { $0 != nil }, "fixture has no coloured run")
        XCTAssertTrue(base.underlines.contains { $0 != nil }, "fixture has no underline")
        XCTAssertTrue(base.intents.contains { $0 != nil }, "fixture has no styled run")
        XCTAssertTrue(base.axText.contains { $0 != nil }, "fixture has no AX text")
        XCTAssertFalse(Self.affordances(base).isEmpty, "fixture has no affordances")

        // Identical input: no difference, and no affordance difference.
        XCTAssertEqual(Self.dimensions(old: base, new: base), [])
        XCTAssertEqual(Self.affordances(base), Self.affordances(base))

        // One perturbation per dimension; each must be reported, and only
        // reporting the RIGHT dimension proves the comparison is wired to
        // the attribute it claims.
        var perturbed = base
        perturbed.text += "!"
        XCTAssertEqual(Self.dimensions(old: base, new: perturbed), ["text"])

        perturbed = base
        perturbed.links = base.links.map { $0.map { _ in "slate-wiki://elsewhere" } }
        XCTAssertEqual(Self.dimensions(old: base, new: perturbed), ["link"])
        XCTAssertNotEqual(
            Self.affordances(base), Self.affordances(perturbed),
            "a rerouted link must change the affordance ledger")

        perturbed = base
        perturbed.colors = base.colors.map { $0.map { _ in "accent" } }
        XCTAssertEqual(Self.dimensions(old: base, new: perturbed), ["color"])

        perturbed = base
        perturbed.underlines = base.underlines.map { _ in nil }
        XCTAssertEqual(Self.dimensions(old: base, new: perturbed), ["underline"])

        perturbed = base
        perturbed.intents = base.intents.map { _ in nil }
        XCTAssertEqual(Self.dimensions(old: base, new: perturbed), ["intent"])

        perturbed = base
        perturbed.axText = base.axText.map { $0.map { _ in "something else" } }
        XCTAssertEqual(Self.dimensions(old: base, new: perturbed), ["ax"])
        XCTAssertNotEqual(
            Self.affordances(base), Self.affordances(perturbed),
            "a changed announcement must change the affordance ledger")
    }

    /// Every ledger row must fire on the input it was written for.
    ///
    /// A row that never matches is not harmless: it reads as "this
    /// difference is known and accepted" while proving nothing, and it
    /// widens the allow-list for free. If one of these fails, the two
    /// pipelines AGREE on that input and the row should be deleted rather
    /// than kept as decoration.
    func testEveryLedgerRowFiresOnItsOwnInput() {
        let inputs: [(Delta, String, [OutgoingLink])] = [
            // Anchored interior: core composes `Note^blk`, the retired
            // mapper used the authored `Note#^blk` verbatim.
            (.embedKeyComposition, "![[Note#^blk]]\n", []),
            // `^` in a markdown destination: the retired router
            // percent-encoded it, so the record could never match.
            (.markdownCaretPath, "See [m](note^block) here.\n", Self.records),
            // A token inside a code span: the retired pipeline's splice
            // leaked into the rendered text.
            (.spliceLeak, "Literal `[[basic]]` span.\n", Self.records),
            (.emptyWikilinkInterior, "An [[]] empty target.\n", Self.records),
        ]

        for (delta, source, records) in inputs {
            var catalogue = Catalogue()
            Self.compare(
                source: source, citations: Self.citations, records: records,
                strictText: true, into: &catalogue)
            XCTAssertGreaterThanOrEqual(
                catalogue.counts[delta] ?? 0, 1,
                """
                ledger row `\(delta.rawValue)` did not fire on its own input \
                \(source.debugDescription).
                Either the two pipelines now agree here — in which case delete the row, \
                it is proving nothing — or the row's predicate no longer matches the \
                difference it was written for.
                classified: \(catalogue.counts)
                unclassified: \(catalogue.findings.keys.sorted())
                """)
            XCTAssertTrue(
                catalogue.findings.isEmpty,
                "unexpected unclassified difference for \(delta.rawValue): "
                    + "\(catalogue.findings)")
        }
    }

    // MARK: - Inputs

    private static var documentCount: Int {
        ProcessInfo.processInfo.environment["SLATE_CENSUS_FULL"] == "1" ? 20_000 : 2_000
    }

    private static let citations = [
        RenderedCitation(
            raw: "[@smith2020]", visualText: "(Smith, 2020)",
            speechText: "Smith, two thousand twenty.", bibEntry: nil,
            styleId: "census")
    ]

    private static let records = [
        ReadingInlineDifferentialCensus.record("basic", kind: "wikilink", unresolved: false),
        ReadingInlineDifferentialCensus.record("gone", kind: "wikilink", unresolved: true),
        ReadingInlineDifferentialCensus.record("note.md", kind: "markdown", unresolved: false),
        ReadingInlineDifferentialCensus.record(
            "note^block", kind: "markdown", unresolved: false),
    ]

    private static func record(
        _ targetRaw: String, kind: String, unresolved: Bool
    ) -> OutgoingLink {
        OutgoingLink(
            targetPath: unresolved ? nil : "\(targetRaw).md", targetRaw: targetRaw,
            targetAnchor: nil, kind: kind, isEmbed: false, isExternal: false,
            isUnresolved: unresolved, snippet: "", ordinal: 0, spanStart: 0,
            spanEnd: 0, displayText: nil)
    }

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

    /// Plain words and Slate constructs only — nothing CommonMark's
    /// inline grammar claims.
    private static let quietFragments: [String] = [
        "plain words", "a vault note", "見出し", "café", "🎉",
        "[[basic]]", "[[gone]]", "[[missing]]", "[[basic|alias]]", "[[a|b|c]]",
        "[[basic#Section Two]]", "[[basic^blk]]", "[[basic#^blk]]", "[[ basic ]]",
        "![[basic]]", "![[basic#Section Two]]", "![[basic^blk]]", "![[basic|shown]]",
        "#tag", "#project/alpha", "[@smith2020]", "[@missing]",
    ]

    /// Everything above, plus the constructs the two inline parsers own —
    /// and the shapes the #1042 review rounds surfaced: authored U+FFFC,
    /// tokens beside delimiters, tokens inside code spans.
    private static let loudFragments: [String] =
        ReadingInlineDifferentialCensus.quietFragments + [
            "**strong**", "*emphasis*", "_under_", "~~struck~~", "`code span`",
            "`[[basic]]`", "`#tag`", "<span>html</span>", "&amp;", "\\*escaped\\*",
            "[label](note.md)", "[label](note.md#sec)", "[m](note^block)",
            "[x](https://example.com)", "[x](mailto:a@b.c)", "[x](javascript:alert)",
            "[x](file:///etc/passwd)", "[x](//host/path)", "[x](#intro)",
            "![alt](attachments/pic.png)", "[[]]", "[[ ]]", "[[a*b]]",
            "\u{FFFC}", "a\u{FFFC}b", "\u{FFFC}[[basic]]",
        ]

    private static func document(_ rng: inout SplitMix64, pool: [String]) -> String {
        var document = ""
        for _ in 0..<(1 + rng.below(5)) {
            var line = ""
            for piece in 0..<(1 + rng.below(4)) {
                if piece > 0 { line += " " }
                line += pool[rng.below(pool.count)]
            }
            switch rng.below(6) {
            case 0: document += "#\(String(repeating: "#", count: rng.below(3))) \(line)"
            case 1: document += "- \(line)"
            case 2: document += "- [\(rng.below(2) == 0 ? "x" : " ")] \(line)"
            case 3: document += "> \(line)"
            case 4: document += "\(1 + rng.below(20)). \(line)"
            default: document += line
            }
            // CRLF in a quarter of blocks: the line ending reached the
            // inline probe as two characters and cost #1042 a round.
            document += rng.below(4) == 0 ? "\r\n\r\n" : "\n\n"
        }
        return document
    }

    /// splitmix64 — the repo's census PRNG, seeded `0..<N` so a failing
    /// seed replays exactly and a short run is a strict prefix of a long
    /// one.
    private struct SplitMix64 {
        private var state: UInt64
        init(seed: UInt64) { state = seed }
        mutating func next() -> UInt64 {
            state = state &+ 0x9E37_79B9_7F4A_7C15
            var z = state
            z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
            z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
            return z ^ (z >> 31)
        }
        mutating func below(_ n: Int) -> Int { Int(next() % UInt64(n)) }
    }
}
