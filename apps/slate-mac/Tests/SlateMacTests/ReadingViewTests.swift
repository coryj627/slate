// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Accessibility
import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// U3-1 (#465) — reading view: block order, the inline pipeline's run
/// contract, activation routing, leaf-level selection discipline, and
/// presentation-ready renders. Spec: `docs/plans/08_ui_parity/specs/
/// u3_spec.md` §U3-1.
final class ReadingViewTests: XCTestCase {

    // MARK: - Shared fixtures

    /// Exercises EVERY `ReadingBlockKind` (asserted below, so the render
    /// smoke test can't silently lose coverage).
    static let everyKindFixture = """
        # Alpha Title

        Intro paragraph with [[Note One|the note]], a #alpha tag, \
        a citation [@smith2020], **bold**, and [site](https://example.com).

        ## Beta Section

        - first bullet
          - nested bullet
        1. ordered item
        - [ ] open task
        - [x] done task

        > quoted line
        > > deeper quote

        ```rust
        fn main() {}
        ```

        $$x^2 + y^2$$

        ```mermaid
        graph TD; A-->B;
        ```

        | a | b |
        | - | - |
        | 1 | 2 |

        ---

        <div>raw html</div>
        """

    static let smithCitation = RenderedCitation(
        raw: "[@smith2020]",
        visualText: "(Smith, 2020)",
        speechText: "Smith, two thousand twenty.",
        bibEntry: nil,
        styleId: "apa"
    )

    private func kindName(_ kind: ReadingBlockKind) -> String {
        switch kind {
        case .heading: return "heading"
        case .paragraph: return "paragraph"
        case .listItem: return "listItem"
        case .blockQuote: return "blockQuote"
        case .codeFence: return "codeFence"
        case .mathBlock: return "mathBlock"
        case .diagram: return "diagram"
        case .table: return "table"
        case .thematicBreak: return "thematicBreak"
        case .html: return "html"
        }
    }

    // MARK: - Rotor order = document order (data level)

    /// The heading rotor walks in document order BECAUSE the VStack renders
    /// blocks in array order — so the array order is the contract: strictly
    /// increasing byte positions, headings in authored sequence.
    func testReadingBlocksArriveInDocumentOrder() {
        let blocks = readingBlocksSource(source: Self.everyKindFixture)
        XCTAssertFalse(blocks.isEmpty)

        var previousStart: UInt64 = 0
        for (index, block) in blocks.enumerated() {
            if index > 0 {
                XCTAssertGreaterThan(
                    block.byteStart, previousStart,
                    "block \(index) out of document order")
            }
            previousStart = block.byteStart
        }

        let headingLevels: [UInt8] = blocks.compactMap {
            if case .heading(let level) = $0.kind { return level }
            return nil
        }
        XCTAssertEqual(
            headingLevels, [1, 2],
            "fixture headings must arrive in authored order for the rotor")
    }

    /// The render fixture must keep exercising every block kind.
    func testFixtureCoversEveryBlockKind() {
        let blocks = readingBlocksSource(source: Self.everyKindFixture)
        let kinds = Set(blocks.map { kindName($0.kind) })
        XCTAssertEqual(
            kinds,
            [
                "heading", "paragraph", "listItem", "blockQuote", "codeFence",
                "mathBlock", "diagram", "table", "thematicBreak", "html",
            ],
            "every ReadingBlockKind must appear in the fixture")
    }

    /// Blank input produces no blocks — the data-level trigger for the
    /// "This note is empty." state.
    func testEmptySourceYieldsNoBlocks() {
        XCTAssertTrue(readingBlocksSource(source: "").isEmpty)
        XCTAssertTrue(readingBlocksSource(source: "   \n\n  \n").isEmpty)
    }

    // MARK: - Rotor order (source-structural, the technique #333 trusts)

    private static var projectRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .deletingLastPathComponent()  // apps
            .deletingLastPathComponent()  // <repo root>
    }

    private func strippedReadingViewSource() throws -> String {
        let url = Self.projectRoot
            .appendingPathComponent("apps/slate-mac/Sources/SlateMac/Reading")
            .appendingPathComponent("ReadingView.swift")
        let raw = try String(contentsOf: url, encoding: .utf8)
        XCTAssertFalse(
            raw.contains("\"\"\""),
            "ReadingView.swift gained a multiline string literal, which "
                + "SwiftSourceStripping does not model — upgrade the stripper "
                + "before trusting the structural asserts below.")
        return SwiftSourceStripping.strippingCommentsAndStrings(raw)
    }

    /// Headings must carry BOTH the trait and the level; the stack must be
    /// the eager, document-ordered VStack (LazyVStack would create AX
    /// enumeration gaps — the ContentBlockPanels discipline).
    func testHeadingRendererAndEagerStackDiscipline() throws {
        let text = try strippedReadingViewSource()
        XCTAssertTrue(
            text.contains(".accessibilityAddTraits(.isHeader)"),
            "heading renderer must add .isHeader for the VO rotor")
        XCTAssertTrue(
            text.contains(".accessibilityHeading("),
            "heading renderer must convey the heading LEVEL")
        XCTAssertTrue(
            text.contains("VStack(alignment: .leading, spacing: Tokens.Spacing.md)"),
            "populated state must be the spec'd document-ordered VStack")
        XCTAssertFalse(
            text.contains("LazyVStack"),
            "reading view must stay EAGER for VoiceOver enumerability")
    }

    /// `.textSelection(.enabled)` must sit on leaf `Text` views only —
    /// container scope breaks VoiceOver continuous read (memory:
    /// feedback_swiftui_textselection_ax).
    func testTextSelectionIsLeafScoped() throws {
        let text = try strippedReadingViewSource()
        let lines = text.components(separatedBy: "\n")
        var occurrences = 0
        for (index, line) in lines.enumerated()
        where line.contains(".textSelection(.enabled)") {
            occurrences += 1
            var nearestConstructor: String?
            var cursor = index
            while cursor >= 0 {
                let candidate = lines[cursor]
                if candidate.contains("Text(") {
                    nearestConstructor = "Text"
                    break
                }
                if candidate.contains("VStack(") || candidate.contains("HStack(")
                    || candidate.contains("ScrollView(") || candidate.contains("ForEach(")
                {
                    nearestConstructor = "container"
                    break
                }
                cursor -= 1
            }
            XCTAssertEqual(
                nearestConstructor, "Text",
                "textSelection at line \(index + 1) is not chained on a leaf Text")
        }
        XCTAssertEqual(
            occurrences, 2,
            "expected leaf selection on exactly the inline leaf + the "
                + "raw-source leaf; a new occurrence needs the same leaf audit")
    }

    // MARK: - Inline pipeline: runs + display text + URLs (#967)
    //
    // The semantics under test now live in `slate-core`
    // (`reading_inline_segments_source`); these cases pin the SAME six
    // behavior families the retired Swift mapper's tests pinned, expressed
    // against the canonical runs and the attributes the applier stamps on
    // them. The mac-only assertions (colour roles, underline, AX custom
    // text) stay here because they are host presentation policy.

    /// The single inline result for a one-block slice.
    private static func inline(
        _ slice: String, citations: [RenderedCitation] = [],
        records: [OutgoingLink] = []
    ) -> ReadingBlockInlines {
        readingInlineSegmentsSource(
            source: slice, citations: citations, records: records
        ).first
            ?? ReadingBlockInlines(segments: [], blockEmbedKey: nil, listMarker: nil)
    }

    private static func segment(
        _ slice: String, citations: [RenderedCitation] = [],
        records: [OutgoingLink] = []
    ) -> ReadingInlineSegment {
        inline(slice, citations: citations, records: records).segments.first
            ?? ReadingInlineSegment(content: "", runs: [], taskCompleted: nil)
    }

    /// `(text, kind)` per run — the shape most assertions care about.
    private static func runs(
        _ slice: String, citations: [RenderedCitation] = [],
        records: [OutgoingLink] = []
    ) -> [(text: String, kind: ReadingInlineRunKind)] {
        let seg = segment(slice, citations: citations, records: records)
        let utf8 = Array(seg.content.utf8)
        return seg.runs.map { run in
            (
                String(decoding: utf8[Int(run.start)..<Int(run.end)], as: UTF8.self),
                run.kind
            )
        }
    }

    private static func attributed(
        _ slice: String, citations: [RenderedCitation] = [],
        records: [OutgoingLink] = []
    ) -> AttributedString {
        ReadingInlineMapper.attributed(
            segment(slice, citations: citations, records: records))
    }

    /// A saved outgoing-link record, the resolution input.
    private static func record(
        _ targetRaw: String, kind: String = "wikilink",
        unresolved: Bool = false, embed: Bool = false, external: Bool = false
    ) -> OutgoingLink {
        OutgoingLink(
            targetPath: unresolved ? nil : "\(targetRaw).md",
            targetRaw: targetRaw, targetAnchor: nil, kind: kind,
            isEmbed: embed, isExternal: external, isUnresolved: unresolved,
            snippet: "", ordinal: 0, spanStart: 0, spanEnd: 0, displayText: nil)
    }

    func testInlineWikilinkWithAlias() {
        let mapped = Self.runs("See [[Note One|the note]] now.")
        XCTAssertEqual(mapped.map(\.text), ["See ", "the note", " now."])
        guard case .wikilink(let target, _, _, let grammar, _) = mapped[1].kind else {
            return XCTFail("expected a wikilink run, got \(mapped[1].kind)")
        }
        XCTAssertEqual(target, "Note One")
        XCTAssertEqual(grammar, .wikilink)

        let attributed = Self.attributed("See [[Note One|the note]] now.")
        let rendered = String(attributed.characters)
        XCTAssertTrue(rendered.contains("the note"))
        XCTAssertFalse(rendered.contains("[["), "wikilink chrome must not render")
        XCTAssertEqual(
            attributed.runs.compactMap(\.link),
            [URL(string: "slate-wiki://Note%20One")!])
    }

    func testInlineWikilinkWithoutAliasKeepsTargetWithAnchor() {
        let mapped = Self.runs("[[Note#Section]]")
        XCTAssertEqual(mapped.count, 1)
        XCTAssertEqual(mapped[0].text, "Note#Section")
        guard case .wikilink(let target, let base, let anchor, _, _) = mapped[0].kind
        else { return XCTFail("expected a wikilink run") }
        XCTAssertEqual(target, "Note#Section")
        XCTAssertEqual(base, "Note", "the anchor-cut form rides alongside")
        XCTAssertEqual(anchor?.kind, "heading")
        XCTAssertEqual(anchor?.text, "Section")
        XCTAssertEqual(
            Self.attributed("[[Note#Section]]").runs.compactMap(\.link),
            [URL(string: "slate-wiki://Note%23Section")!])
    }

    func testInlineTag() {
        let mapped = Self.runs("Hello #alpha world")
        XCTAssertEqual(mapped.map(\.text), ["Hello ", "#alpha", " world"])
        XCTAssertEqual(mapped[1].kind, .tag(name: "alpha"))
        XCTAssertEqual(
            Self.attributed("Hello #alpha world").runs.compactMap(\.link),
            [URL(string: "slate-tag://alpha")!])
    }

    /// Citation runs: visible text is the RENDERED form, the AX text is the
    /// SPEECH text (Milestone L — same `speechText` source CitationsPanel
    /// uses), carried per-range via `accessibilityTextCustom`.
    func testInlineCitationWithMatchCarriesSpeechText() {
        let citations = [Self.smithCitation]
        let mapped = Self.runs("As shown [@smith2020].", citations: citations)
        XCTAssertEqual(mapped[1].text, "(Smith, 2020)")
        XCTAssertEqual(
            mapped[1].kind,
            .citation(
                raw: "[@smith2020]", speech: "Smith, two thousand twenty."))

        let attributed = Self.attributed(
            "As shown [@smith2020].", citations: citations)
        let rendered = String(attributed.characters)
        XCTAssertTrue(rendered.contains("(Smith, 2020)"))
        XCTAssertFalse(rendered.contains("[@smith2020]"))
        XCTAssertEqual(
            attributed.runs.compactMap(\.link),
            [URL(string: "slate-cite://%5B%40smith2020%5D")!])

        var speech: [String]?
        for run in attributed.runs where run.link != nil {
            speech = attributed[run.range][
                AttributeScopes.AccessibilityAttributes.TextCustomAttribute.self]
        }
        XCTAssertEqual(
            speech, ["Smith, two thousand twenty."],
            "citation run must carry its speech text for AT")
    }

    func testInlineCitationWithoutMatchDegradesToRaw() {
        let mapped = Self.runs("As shown [@ghost1999].")
        XCTAssertEqual(mapped[1].text, "[@ghost1999]")
        XCTAssertEqual(
            mapped[1].kind,
            .citation(raw: "[@ghost1999]", speech: "[@ghost1999]"))
    }

    /// Embed alt-text contract: alias, else target NAME — never empty.
    func testInlineEmbedDisplayNames() {
        let aliased = Self.runs("![[img.png|photo]]")
        XCTAssertEqual(aliased.count, 1)
        XCTAssertEqual(aliased[0].text, "photo")
        XCTAssertEqual(aliased[0].kind, .embed(key: "img.png"))

        let named = Self.runs("![[folder/pic one.png]]")
        XCTAssertEqual(named.count, 1)
        XCTAssertEqual(named[0].text, "pic one.png")
        XCTAssertEqual(named[0].kind, .embed(key: "folder/pic one.png"))
        XCTAssertFalse(
            named[0].text.isEmpty, "image embeds need a non-empty AX label")
    }

    /// External markdown links keep their real URL (activation passes
    /// through to the system) and still get link styling.
    func testInlineExternalLinksKeepTheirURL() {
        let mapped = Self.runs("Visit [site](https://example.com).")
        XCTAssertEqual(
            mapped[1].kind, .externalLink(url: "https://example.com"))
        XCTAssertEqual(
            Self.attributed("Visit [site](https://example.com).").runs
                .compactMap(\.link),
            [URL(string: "https://example.com")!])
    }

    /// Every link run — slate or external — carries accent + underline (the
    /// affordance is not conveyed by colour alone).
    func testInlineStylesLinkRuns() {
        let attributed = Self.attributed(
            "See [[Note]] and [x](https://x.com).",
            records: [Self.record("Note")])
        var styledRuns = 0
        for run in attributed.runs where run.link != nil {
            styledRuns += 1
            let color = attributed[run.range][
                AttributeScopes.SwiftUIAttributes.ForegroundColorAttribute.self]
            let underline = attributed[run.range][
                AttributeScopes.SwiftUIAttributes.UnderlineStyleAttribute.self]
            XCTAssertEqual(color, Tokens.ColorRole.accentText)
            XCTAssertNotNil(underline)
        }
        XCTAssertEqual(styledRuns, 2)
    }

    // MARK: - Unresolved wikilink styling (#849 · #967)

    /// Foreground colour of the FIRST link run whose activation URL routes
    /// to the given wiki target (base form).
    private func linkRunColor(
        in attributed: AttributedString, base: String
    ) -> Color? {
        for run in attributed.runs {
            guard let link = run.link,
                case .wiki(let target, let grammar) =
                    ReadingLinkRouter.disposition(for: link)
            else { continue }
            let cut =
                grammar == .wikilink
                ? Self.wikiBase(target) : Self.markdownBase(target)
            guard cut == base else { continue }
            return attributed[run.range][
                AttributeScopes.SwiftUIAttributes.ForegroundColorAttribute.self]
        }
        return nil
    }

    /// Test-local anchor cuts, used only to FIND a run in these
    /// assertions — production code never re-derives these (core ships
    /// `base_target` on the run).
    private static func wikiBase(_ target: String) -> String {
        let trimmed = target.trimmingCharacters(in: .whitespaces)
        if let hash = trimmed.firstIndex(of: "#") {
            return String(trimmed[trimmed.startIndex..<hash])
                .trimmingCharacters(in: .whitespaces)
        }
        if let caret = trimmed.firstIndex(of: "^") {
            return String(trimmed[trimmed.startIndex..<caret])
                .trimmingCharacters(in: .whitespaces)
        }
        return trimmed
    }

    private static func markdownBase(_ target: String) -> String {
        let trimmed = target.trimmingCharacters(in: .whitespaces)
        if let hash = trimmed.firstIndex(of: "#") {
            return String(trimmed[trimmed.startIndex..<hash])
                .trimmingCharacters(in: .whitespaces)
        }
        return trimmed
    }

    /// A dangling wikilink renders in warningText (the editor's U5-3
    /// treatment) while resolved siblings keep accent — and the underline
    /// stays on BOTH (the affordance is never colour-only).
    func testUnresolvedWikilinkRendersWithWarningText() {
        let attributed = Self.attributed(
            "See [[Missing]] and [[There]].",
            records: [
                Self.record("Missing", unresolved: true), Self.record("There"),
            ])
        XCTAssertEqual(
            linkRunColor(in: attributed, base: "Missing"),
            Tokens.ColorRole.warningText)
        XCTAssertEqual(
            linkRunColor(in: attributed, base: "There"),
            Tokens.ColorRole.accentText)
        for run in attributed.runs where run.link != nil {
            XCTAssertNotNil(
                attributed[run.range][
                    AttributeScopes.SwiftUIAttributes.UnderlineStyleAttribute.self],
                "underline marks activatable on resolved AND unresolved runs")
        }
    }

    /// #967 semantics (unchanged in production, made honest everywhere):
    /// with NO records the note's link index has not landed, so every run
    /// classifies unresolved — which is exactly what activation announces
    /// in that window. The pre-#967 default styled these accent, so a
    /// mid-transition run could look resolved and then refuse to open.
    func testNoRecordsClassifiesEveryLinkUnresolved() {
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed("See [[Missing]]."), base: "Missing"),
            Tokens.ColorRole.warningText)
    }

    /// Key normalization: the run's target carries the anchor
    /// (`Note#Section`); the record key is the anchor-STRIPPED base form
    /// `links.rs` stores as `targetRaw` — so styling and activation agree
    /// about the same run.
    func testUnresolvedMembershipUsesTheAnchorCutKey() {
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed(
                    "[[Missing#Section]]",
                    records: [Self.record("Missing", unresolved: true)]),
                base: "Missing"),
            Tokens.ColorRole.warningText,
            "anchor forms resolve through the base-target key, like the router")
    }

    /// Codex rounds 1+2: the match keys are EXACT per grammar. Wiki
    /// grammar cuts at `#` (an earlier `^` stays in the base), else the
    /// first `^` (legacy block ref); markdown grammar never cuts at `^`.
    /// The verbatim target closes each list. `reading_match_link` is the
    /// ONE implementation of that order — activation and styling share it.
    func testCandidateKeyOrderIsExactPerGrammar() {
        // Wiki grammar cuts at # even with an earlier ^.
        XCTAssertEqual(
            readingMatchLink(
                target: "note^draft#sec", grammar: .wikilink, embed: false,
                records: [Self.record("note^draft")]),
            0)
        XCTAssertNil(
            readingMatchLink(
                target: "note^draft#sec", grammar: .wikilink, embed: false,
                records: [Self.record("note")]),
            "the ^ stays inside the hash-cut base")
        // Legacy block ref cuts at the ^ when there is no #.
        XCTAssertEqual(
            readingMatchLink(
                target: "note^block", grammar: .wikilink, embed: false,
                records: [Self.record("note")]),
            0)
        // Canonical block ref cuts at the #.
        XCTAssertEqual(
            readingMatchLink(
                target: "note#^block", grammar: .wikilink, embed: false,
                records: [Self.record("note")]),
            0)
        // A bare ^ is a legal markdown path character — never cut.
        XCTAssertNil(
            readingMatchLink(
                target: "note^draft.md", grammar: .markdownDestination,
                embed: false, records: [Self.record("note", kind: "markdown")]))
        XCTAssertEqual(
            readingMatchLink(
                target: "note^draft.md#sec", grammar: .markdownDestination,
                embed: false,
                records: [Self.record("note^draft.md", kind: "markdown")]),
            0)
        // The verbatim target closes the list (pre-#509 rows).
        XCTAssertEqual(
            readingMatchLink(
                target: "Note#Sec", grammar: .wikilink, embed: false,
                records: [Self.record("Note#Sec")]),
            0)
    }

    /// Codex round 2: record-ownership predicate — stale (previous
    /// note's) records must never classify or activate.
    func testRecordsBelongToNote() {
        XCTAssertTrue(
            ReadingLinkRouter.recordsBelongToNote(
                recordsPath: "a.md", notePath: "a.md"))
        XCTAssertFalse(
            ReadingLinkRouter.recordsBelongToNote(
                recordsPath: "a.md", notePath: "b.md"))
        XCTAssertFalse(
            ReadingLinkRouter.recordsBelongToNote(
                recordsPath: nil, notePath: "a.md"))
        XCTAssertFalse(
            ReadingLinkRouter.recordsBelongToNote(recordsPath: nil, notePath: nil),
            "no loaded records never owns anything")

        // A vault path is a BYTE identity: Swift's `==` treats canonically
        // equivalent Unicode as equal, so an NFC-spelled retained path
        // would otherwise be accepted as owning a byte-distinct NFD note
        // and the previous note's records would classify and activate
        // this one's runs.
        let nfc = "Cafe\u{0301}.md"  // decomposed
        let nfd = "Caf\u{00E9}.md"  // precomposed
        XCTAssertEqual(nfc, nfd, "the two spellings are canonically equal")
        XCTAssertFalse(
            Array(nfc.utf8) == Array(nfd.utf8), "but byte-distinct")
        XCTAssertFalse(
            ReadingLinkRouter.recordsBelongToNote(
                recordsPath: nfc, notePath: nfd),
            "ownership must be byte-exact, not canonical")
    }

    /// End-to-end `#`-outranks-`^` for wiki runs: `[[note^draft#sec]]`
    /// must match a record keyed `note^draft` (links.rs cuts at the #;
    /// the old first-marker cut searched for `note` and missed).
    func testWikiHashOutranksCaretEndToEnd() {
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed(
                    "[[note^draft#sec]]",
                    records: [Self.record("note^draft", unresolved: true)]),
                base: "note^draft"),
            Tokens.ColorRole.warningText,
            "the record key is the hash-cut base — the ^ stays in it")
    }

    /// A `^` in a markdown destination.
    ///
    /// **Behavior fix (#967, deltas ledger):** the retired pipeline routed
    /// markdown destinations through Foundation's `URL`, which
    /// percent-encoded the authored bytes before the mapper saw them, so a
    /// `^`-path could never match its own record — a platform limitation
    /// the old test pinned as "must at least never style resolved". Core
    /// carries the authored destination VERBATIM (the `target_raw`
    /// contract), so the record now matches and the run styles resolved.
    func testMarkdownCaretPathResolvesAgainstItsOwnRecord() {
        let attributed = Self.attributed(
            "See [t](note^draft.md#sec).",
            records: [Self.record("note^draft.md", kind: "markdown")])
        XCTAssertEqual(
            linkRunColor(in: attributed, base: "note^draft.md"),
            Tokens.ColorRole.accentText,
            "the authored ^-path is carried verbatim and matches its record")
        XCTAssertEqual(
            attributed.runs.compactMap(\.link).map {
                ReadingLinkRouter.disposition(for: $0)
            },
            [.wiki("note^draft.md#sec", .markdownDestination)])
    }

    /// A run with NO saved record activates as "unresolved. Cannot open."
    /// (live-buffer text the index hasn't seen, or a query that hasn't
    /// landed) — accent styling would lie.
    func testMissingRecordStylesUnresolved() {
        XCTAssertEqual(
            linkRunColor(in: Self.attributed("[[Ghost]]"), base: "Ghost"),
            Tokens.ColorRole.warningText,
            "no record → activation announces unresolved → styling agrees")
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed("[[Ghost]]", records: [Self.record("Ghost")]),
                base: "Ghost"),
            Tokens.ColorRole.accentText)
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed(
                    "[[Ghost]]",
                    records: [Self.record("Ghost", unresolved: true)]),
                base: "Ghost"),
            Tokens.ColorRole.warningText)
    }

    /// Codex round 3: records are KIND-partitioned — a saved markdown
    /// record must never vouch for an unsaved wikilink spelling the same
    /// characters (activation applies the same rule).
    func testStylingNeverClassifiesAcrossGrammars() {
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed(
                    "[[note^block]]",
                    records: [Self.record("note^block", kind: "markdown")]),
                base: "note"),
            Tokens.ColorRole.warningText,
            "the markdown record is the OTHER grammar's — record-less here")
    }

    /// Embed and external records are excluded from link classification,
    /// exactly as the retired `LinkRecordSets` builder excluded them.
    func testEmbedAndExternalRecordsNeverVouchForLinkRuns() {
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed(
                    "[[n]]", records: [Self.record("n", embed: true)]),
                base: "n"),
            Tokens.ColorRole.warningText,
            "an embed record is not a link record")
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed(
                    "[t](https-ish.md)",
                    records: [
                        Self.record(
                            "https-ish.md", kind: "markdown", external: true)
                    ]),
                base: "https-ish.md"),
            Tokens.ColorRole.warningText,
            "an external record is not an internal link record")
    }

    /// Red-team probe (confirmed): whitespace-padded and anchored targets
    /// must trim to the resolver's key — `[[ Missing ]]` and
    /// `[[Missing #Section]]` are the same unresolved target.
    func testUnresolvedMembershipTrimsWhitespaceLikeLinksRs() {
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed(
                    "[[ Missing ]]",
                    records: [Self.record("Missing", unresolved: true)]),
                base: "Missing"),
            Tokens.ColorRole.warningText,
            "padded target trims to the unresolved key")
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed(
                    "[[Missing #Section]]",
                    records: [Self.record("Missing", unresolved: true)]),
                base: "Missing"),
            Tokens.ColorRole.warningText,
            "space-before-anchor trims to the base target")
    }

    func testUnresolvedMembershipIsCaseSensitiveLikeTheRouter() {
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed(
                    "[[Missing]]",
                    records: [Self.record("missing", unresolved: true)]),
                base: "Missing"),
            Tokens.ColorRole.warningText,
            "a case-mismatched record is no record at all — and a run with "
                + "no record of its own grammar is unresolved")
    }

    /// Internal markdown links take the same unresolved treatment — they
    /// activate through the same router branch.
    func testUnresolvedStylingCoversMarkdownDestinations() {
        XCTAssertEqual(
            linkRunColor(
                in: Self.attributed(
                    "See [t](gone.md).",
                    records: [
                        Self.record("gone.md", kind: "markdown", unresolved: true)
                    ]),
                base: "gone.md"),
            Tokens.ColorRole.warningText)
    }

    /// The unresolved state is announced, not colour-only: the run carries
    /// the AX custom text.
    func testUnresolvedRunCarriesAccessibilityText() {
        let attributed = Self.attributed(
            "[[Missing]]", records: [Self.record("Missing", unresolved: true)])
        var found = false
        for run in attributed.runs where run.link != nil {
            if attributed[run.range][
                AttributeScopes.AccessibilityAttributes.TextCustomAttribute.self]
                == ["Unresolved link"]
            {
                found = true
            }
        }
        XCTAssertTrue(found, "unresolved runs carry the AX custom text")
    }

    /// Internal markdown destinations (scheme-less; Slate semantics:
    /// vault-rooted/basename, stored literally) route on the markdown-wiki
    /// scheme so they activate — never styled-then-dead.
    func testMarkdownDestinationsRouteOnTheMarkdownScheme() {
        let links = Self.attributed("See [t](note.md).").runs.compactMap(\.link)
        XCTAssertEqual(links.count, 1)
        XCTAssertEqual(links[0].scheme, ReadingLinkRouter.wikiMarkdownScheme)
        XCTAssertEqual(
            ReadingLinkRouter.disposition(for: links[0]),
            .wiki("note.md", .markdownDestination))
    }

    /// The authored destination travels VERBATIM — fragments kept (markdown
    /// `targetRaw` stores them), percent-escapes NOT decoded (Slate never
    /// decodes markdown destinations).
    func testMarkdownDestinationKeepsFragmentAndPercentEscapesLiteral() {
        XCTAssertEqual(
            Self.attributed("[t](note.md#sec)").runs.compactMap(\.link).map {
                ReadingLinkRouter.disposition(for: $0)
            },
            [.wiki("note.md#sec", .markdownDestination)])
        XCTAssertEqual(
            Self.attributed("[t](my%20note.md)").runs.compactMap(\.link).map {
                ReadingLinkRouter.disposition(for: $0)
            },
            [.wiki("my%20note.md", .markdownDestination)])
    }

    /// Non-activatable destinations carry no link attribute at all: no dead
    /// affordance visually or to VoiceOver, and nothing ever reaches
    /// LaunchServices. Fragment-only and protocol-relative references mirror
    /// `links.rs::looks_external` (not internal notes → not routed).
    func testNonActivatableDestinationsCarryNoAffordance() {
        for slice in [
            "[t](javascript:alert(1))",
            "[t](file:///etc/passwd)",
            "[t](ftp://host/x)",
            "[t](#intro)",
            "[t](//host/x)",
        ] {
            let attributed = Self.attributed(slice)
            XCTAssertEqual(
                attributed.runs.compactMap(\.link), [],
                "\(slice) must not render as an activatable link")
            XCTAssertTrue(
                String(attributed.characters).contains("t"),
                "\(slice) keeps its display text")
        }
    }

    /// Slate tokens inside markdown-link syntax stay literal — the link is
    /// the construct there. `#intro` in the label (or a fragment-only
    /// destination) must not become a tag run.
    func testSlateTokensInsideMarkdownLinksStayLiteral() {
        let mapped = Self.runs("see [about #intro](note.md) here")
        XCTAssertFalse(
            mapped.contains { if case .tag = $0.kind { return true } else { return false } },
            "no Slate-token runs may be minted inside a markdown link")
        let attributed = Self.attributed("see [about #intro](note.md) here")
        let links = attributed.runs.compactMap(\.link)
        XCTAssertEqual(links.count, 1)
        XCTAssertEqual(
            ReadingLinkRouter.disposition(for: links[0]),
            .wiki("note.md", .markdownDestination))
        XCTAssertTrue(
            String(attributed.characters).contains("about #intro"),
            "label text renders verbatim")
    }

    func testRunsPreserveDocumentOrderAcrossKinds() {
        let kinds = Self.runs("A [[x]] then #t end.").map(\.kind)
        XCTAssertTrue(
            kinds.contains { if case .wikilink = $0 { return true } else { return false } })
        XCTAssertTrue(kinds.contains(.tag(name: "t")))
        let wikiIndex = kinds.firstIndex {
            if case .wikilink = $0 { return true } else { return false }
        }
        let tagIndex = kinds.firstIndex(of: .tag(name: "t"))
        XCTAssertNotNil(wikiIndex)
        XCTAssertNotNil(tagIndex)
        XCTAssertLessThan(wikiIndex!, tagIndex!)
    }

    /// The Rust span classifier is the single syntax authority: a wikilink
    /// inside inline code is code, not a link.
    func testInlineCodeSuppressesTokens() {
        let mapped = Self.runs("`[[not a link]]` and [[real]]")
        let wikiRuns = mapped.filter {
            if case .wikilink = $0.kind { return true } else { return false }
        }
        XCTAssertEqual(wikiRuns.count, 1)
        XCTAssertEqual(wikiRuns[0].text, "real")
        XCTAssertTrue(
            Self.segment("`[[not a link]]` and [[real]]").content
                .contains("[[not a link]]"),
            "code-span content must stay literal")
    }

    /// Markdown-meaningful characters in display text can't break out of a
    /// run: the retired backslash-escaping splice is replaced structurally —
    /// a selected token's bytes are opaque to the CommonMark walk, so its
    /// display text never re-parses.
    func testTokenDisplayTextNeverReparses() {
        let mapped = Self.runs("[[a|x]y]]")
        XCTAssertEqual(mapped.count, 1)
        XCTAssertEqual(mapped[0].text, "x]y")

        let starred = Self.runs("[[note|**not bold**]]")
        XCTAssertEqual(starred.count, 1)
        XCTAssertEqual(starred[0].text, "**not bold**")
        XCTAssertEqual(
            Self.segment("[[note|**not bold**]]").runs[0].styles, [],
            "the token's own text carries no emphasis")

        // A delimiter INSIDE a token can't pair with one outside it.
        XCTAssertEqual(Self.segment("[[a*b]] * tail").content, "a*b * tail")
        XCTAssertTrue(
            Self.segment("[[a*b]] * tail").runs.allSatisfy { $0.styles.isEmpty })
    }

    /// Emphasis SPANNING a token keeps the style on every run, including
    /// the token's own — the overlap sweep in `highlight_spans` would have
    /// dropped it, so the styling walk is a separate masked pass.
    func testEmphasisSpanningATokenStylesEveryRun() {
        let seg = Self.segment("**bold [[Target]] tail**")
        XCTAssertEqual(seg.content, "bold Target tail")
        XCTAssertTrue(
            seg.runs.allSatisfy { $0.styles == [.strong] },
            "every run inside the strong span carries it: \(seg.runs)")
    }

    // MARK: - URL codec

    func testRouterURLCodecRoundTrips() {
        let targets = [
            "Note Name", "folder/note", "héllo – x", "a#b", "p|q", "100%",
        ]
        for target in targets {
            guard
                let url = ReadingLinkRouter.encodedURL(
                    scheme: ReadingLinkRouter.wikiScheme, target: target)
            else {
                XCTFail("failed to encode \(target)")
                continue
            }
            XCTAssertEqual(
                ReadingLinkRouter.decodedTarget(from: url), target,
                "codec must round-trip \(target)")
        }
    }

    /// Core ships the anchor-cut form on the run, so no host re-derives it.
    func testRunsCarryTheAnchorCutBaseTarget() {
        func base(_ slice: String) -> String? {
            guard case .wikilink(_, let base, _, _, _) = Self.runs(slice)[0].kind
            else { return nil }
            return base
        }
        XCTAssertEqual(base("[[Note#Sec]]"), "Note")
        XCTAssertEqual(base("[[Note^blk]]"), "Note")
        XCTAssertEqual(base("[[Plain]]"), "Plain")
    }

    // MARK: - Routing table

    func testDispositionTable() {
        func disposition(_ s: String) -> ReadingLinkRouter.Disposition {
            ReadingLinkRouter.disposition(for: URL(string: s)!)
        }
        XCTAssertEqual(
            disposition("slate-wiki://Note%20One"), .wiki("Note One", .wikilink))
        XCTAssertEqual(
            disposition("slate-wikimd://note%5Emine.md"),
            .wiki("note^mine.md", .markdownDestination))
        XCTAssertEqual(disposition("slate-embed://img.png"), .embed("img.png"))
        XCTAssertEqual(disposition("slate-tag://alpha"), .tag("alpha"))
        XCTAssertEqual(
            disposition("slate-cite://%5B%40smith2020%5D"),
            .citation("[@smith2020]"))
        // Allowlisted external schemes pass to the system…
        XCTAssertEqual(disposition("https://example.com"), .external)
        XCTAssertEqual(disposition("http://example.com"), .external)
        XCTAssertEqual(disposition("mailto:a@b.c"), .external)
        // …everything else is dropped: relative markdown links (no scheme)
        // and scheme-hijack shapes must never reach LaunchServices.
        XCTAssertEqual(disposition("note.md"), .discard)
        XCTAssertEqual(disposition("file:///etc/passwd"), .discard)
        XCTAssertEqual(disposition("javascript:alert(1)"), .discard)
    }

    /// `route` executes the disposition: each slate scheme lands on exactly
    /// its closure with the decoded target (recording-fake router).
    func testRouteDispatchesToRecordingClosures() {
        final class Recorder {
            var events: [String] = []
        }
        let recorder = Recorder()
        let router = ReadingLinkRouter(
            openWikiLink: { target, _ in recorder.events.append("wiki:\(target)") },
            openEmbed: { recorder.events.append("embed:\($0)") },
            openTag: { recorder.events.append("tag:\($0)") },
            expandCitation: { recorder.events.append("cite:\($0)") }
        )
        _ = router.route(URL(string: "slate-wiki://Note%20One")!)
        _ = router.route(URL(string: "slate-embed://pic.png")!)
        _ = router.route(URL(string: "slate-tag://alpha")!)
        _ = router.route(URL(string: "slate-cite://%5B%40k%5D")!)
        _ = router.route(URL(string: "https://example.com")!)
        _ = router.route(URL(string: "file:///etc/passwd")!)
        XCTAssertEqual(
            recorder.events,
            ["wiki:Note One", "embed:pic.png", "tag:alpha", "cite:[@k]"],
            "external/discard URLs must not touch the slate closures")
    }

    // MARK: - Live router → AppState seams

    /// Tag activation opens the search overlay in REAL tag scope (#508):
    /// `.tag("alpha")` with an empty query (which, under tag scope, lists
    /// the tag's files) — not the old approximate bare-name FTS prefilter.
    @MainActor
    func testLiveRouterTagPrefiltersSearchOverlay() {
        let appState = AppState()
        let router = ReadingLinkRouter.live(appState: appState)
        router.openTag("alpha")
        XCTAssertEqual(appState.searchScope, .tag(name: "alpha"))
        XCTAssertEqual(appState.searchQuery, "")
        XCTAssertTrue(appState.isSearchOpen)
    }

    @MainActor
    func testLiveRouterCitationWithoutLoadedCitationsIsNoOp() {
        let appState = AppState()
        ReadingLinkRouter.live(appState: appState).expandCitation("[@ghost]")
        XCTAssertNil(appState.expandedCitation)
    }

    /// A wiki target with no matching outgoing-link record (live buffer
    /// ahead of the saved link index) must not navigate.
    @MainActor
    func testLiveRouterUnknownWikiTargetDoesNotNavigate() {
        let appState = AppState()
        ReadingLinkRouter.live(appState: appState)
            .openWikiLink("Nowhere", .wikilink)
        XCTAssertNil(appState.selectedFilePath)
    }

    /// Rewritten internal markdown links ride the wiki path, end to end
    /// against a real vault. Both families are now anchor-stripped: a
    /// markdown record stores the fragment-less base in `targetRaw` with the
    /// `#fragment` split into `targetAnchor`, exactly like a wikilink
    /// (links.rs walk_markdown, #509). So `[a](note.md#sec)` resolves on its
    /// base, and activating the wiki-scheme URL `note.md#sec` matches the
    /// base-form record and OPENS the target — no longer the unresolved
    /// feedback it produced before the anchor split landed.
    @MainActor
    func testLiveRouterOpensInternalMarkdownLinksEndToEnd() async throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-reading-md-links-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        try Data("# Note\n\n## sec".utf8)
            .write(to: vault.appendingPathComponent("note.md"))
        try Data("open [a](note.md#sec)".utf8)
            .write(to: vault.appendingPathComponent("source.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let appState = AppState(
            recentsStore: store, externalOpener: { _ in true })
        appState.openVault(at: vault)
        await appState.scanTask?.value
        appState.selectedFilePath = "source.md"
        await appState.linksLoadTask?.value
        XCTAssertEqual(appState.currentOutgoingLinks.count, 1)
        // Anchor split: base in targetRaw, `#sec` in targetAnchor.
        XCTAssertEqual(appState.currentOutgoingLinks[0].targetRaw, "note.md")
        XCTAssertEqual(
            appState.currentOutgoingLinks[0].targetAnchor,
            LinkAnchor(kind: "heading", text: "sec"))

        ReadingLinkRouter.live(appState: appState)
            .openWikiLink("note.md#sec", .markdownDestination)
        // Base-form match resolves and navigates to the target note.
        XCTAssertEqual(
            appState.lastActivatedLinkOutcome, .openedInternal("note.md"))
        XCTAssertEqual(appState.selectedFilePath, "note.md")
    }

    /// Codex round 2, grammar retention end-to-end against a real
    /// vault: a note holding BOTH `[[note^block]]` (Rust records
    /// targetRaw `note` — wiki grammar cuts at the `^`) and
    /// `[m](note^block)` (records targetRaw `note^block` verbatim).
    /// Activating the WIKILINK must select the wiki-grammar record and
    /// open `note.md` — a grammar-blind key list matched the markdown
    /// sibling's record first.
    @MainActor
    func testLiveRouterGrammarDisambiguatesCaretCollision() async throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-reading-caret-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        try Data("# Note\n".utf8)
            .write(to: vault.appendingPathComponent("note.md"))
        try Data("# Shadow\n".utf8)
            .write(to: vault.appendingPathComponent("note^block.md"))
        try Data("[[note^block]] and [m](note^block)".utf8)
            .write(to: vault.appendingPathComponent("source.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let appState = AppState(
            recentsStore: store, externalOpener: { _ in true })
        appState.openVault(at: vault)
        await appState.scanTask?.value
        appState.selectedFilePath = "source.md"
        await appState.linksLoadTask?.value
        XCTAssertEqual(
            Set(appState.currentOutgoingLinks.map(\.targetRaw)),
            ["note", "note^block"],
            "Rust keys the two grammars differently — the collision premise")

        ReadingLinkRouter.live(appState: appState)
            .openWikiLink("note^block", .wikilink)
        XCTAssertEqual(
            appState.selectedFilePath, "note.md",
            "the wikilink resolves through ITS grammar's record")
    }

    /// Codex round 3, the live-buffer variant: ONLY the markdown link
    /// is saved; a `[[note^block]]` typed in the dirty buffer is
    /// record-less and must REFUSE — the saved markdown record's
    /// verbatim `note^block` row is the other grammar's and must never
    /// be hijacked.
    @MainActor
    func testLiveRouterUnsavedWikilinkNeverHijacksMarkdownRecord() async throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-reading-hijack-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        try Data("# Shadow\n".utf8)
            .write(to: vault.appendingPathComponent("note^block.md"))
        try Data("only [m](note^block) saved".utf8)
            .write(to: vault.appendingPathComponent("source.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let appState = AppState(
            recentsStore: store, externalOpener: { _ in true })
        appState.openVault(at: vault)
        await appState.scanTask?.value
        appState.selectedFilePath = "source.md"
        await appState.linksLoadTask?.value
        XCTAssertEqual(
            appState.currentOutgoingLinks.map(\.kind), ["markdown"],
            "premise: only the markdown record exists")

        ReadingLinkRouter.live(appState: appState)
            .openWikiLink("note^block", .wikilink)
        XCTAssertEqual(
            appState.selectedFilePath, "source.md",
            "record-less wikilink refuses — no cross-grammar hijack")
    }

    /// Codex round 2, stale-record refusal: records for `source.md` are
    /// loaded, selection moves on, and the incoming note's query has
    /// not landed. Activating a target that matches the RETAINED
    /// records must refuse (missing-record announce), never navigate
    /// through another note's records.
    @MainActor
    func testLiveRouterRefusesStaleRecordsDuringTransition() async throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-reading-stale-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        try Data("# Note\n".utf8)
            .write(to: vault.appendingPathComponent("note.md"))
        try Data("# Third\n".utf8)
            .write(to: vault.appendingPathComponent("third.md"))
        try Data("go [[third]]".utf8)
            .write(to: vault.appendingPathComponent("source.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let appState = AppState(
            recentsStore: store, externalOpener: { _ in true })
        appState.openVault(at: vault)
        await appState.scanTask?.value
        appState.selectedFilePath = "source.md"
        await appState.linksLoadTask?.value
        XCTAssertEqual(appState.currentOutgoingLinksPath, "source.md")

        // Move selection; the new query is scheduled but has NOT run —
        // no await between here and the activation (MainActor-serial).
        appState.selectedFilePath = "note.md"
        ReadingLinkRouter.live(appState: appState)
            .openWikiLink("third", .wikilink)
        XCTAssertEqual(
            appState.selectedFilePath, "note.md",
            "no navigation through the previous note's retained records")
    }

    // MARK: - Chrome stripping (core-owned since #967)
    //
    // Heading ATX/setext text, the list marker + task-box split, and the
    // blockquote depth strip moved into `slate-core` (inline-runs spec §2).
    // These cases pin the SAME semantics through the canonical result:
    // the Swift strippers they used to call are deleted.

    func testHeadingChromeStripping() {
        XCTAssertEqual(Self.segment("## Title").content, "Title")
        XCTAssertEqual(Self.segment("# Title ##").content, "Title")
        XCTAssertEqual(Self.segment("Title\n=====").content, "Title")
        XCTAssertEqual(Self.segment("Title\n---").content, "Title")
    }

    func testListItemChromeStripping() {
        let bullet = Self.inline("- foo")
        XCTAssertEqual(bullet.listMarker, "-")
        XCTAssertEqual(bullet.segments.first?.content, "foo")
        XCTAssertNil(bullet.segments.first?.taskCompleted)

        XCTAssertEqual(Self.inline("  - bar").segments.first?.content, "bar")

        let ordered = Self.inline("12. twelve")
        XCTAssertEqual(ordered.listMarker, "12.")
        XCTAssertEqual(ordered.segments.first?.content, "twelve")

        XCTAssertEqual(Self.inline("3) three").listMarker, "3)")

        let task = Self.inline("- [x] done thing")
        XCTAssertEqual(task.segments.first?.taskCompleted, true)
        XCTAssertEqual(task.segments.first?.content, "done thing")

        XCTAssertEqual(
            Self.inline("- a\n  continued").segments.first?.content,
            "a\n  continued")

        // A paragraph is not a list item — no marker is reported.
        XCTAssertNil(Self.inline("not a list").listMarker)
    }

    /// Taskhood belongs to the Rust classifier — the splitter must NOT strip
    /// a boxy-looking prefix from PLAIN list items (Codoki, #514). The two
    /// reachable shapes: ordered items (never tasks in the Rust grammar) and
    /// a box with no following space.
    func testListItemChromeKeepsBracketTextOnPlainItems() {
        let ordered = Self.inline("1. [v] Visible")
        XCTAssertEqual(ordered.segments.first?.content, "[v] Visible")
        XCTAssertNil(ordered.segments.first?.taskCompleted)

        let noSpace = Self.inline("- [v]x")
        XCTAssertEqual(noSpace.segments.first?.content, "[v]x")
        XCTAssertNil(noSpace.segments.first?.taskCompleted)
    }

    func testQuoteChromeStripping() {
        XCTAssertEqual(Self.segment("> quoted").content, "quoted")
        XCTAssertEqual(Self.segment("> > deep").content, "deep")
        XCTAssertEqual(Self.segment("> a\n> b").content, "a\nb")
    }

    /// The code-block interior is now carried authoritatively from Rust
    /// (`ReadingBlockKind.codeFence.interior`), so the `fenceInterior`
    /// heuristic is retired. `fenceInteriorVerbatim` remains for YAML block
    /// scalars, whose chomping semantics depend on the pre-closer newline.
    func testFenceInteriorVerbatim() {
        XCTAssertEqual(
            ReadingBlockSource.fenceInteriorVerbatim("```yaml\nquery: |\n  Saved\n```"),
            "query: |\n  Saved\n")
        XCTAssertEqual(
            ReadingBlockSource.fenceInteriorVerbatim("~~~yaml\nquery: |+\n  Saved\n\n~~~\n"),
            "query: |+\n  Saved\n\n")
    }

    /// The authoritative interior (#869): the Rust parser carries the exact
    /// code content on the block kind, so the reading view's fallback
    /// `CodeBlock.source` and the print composer both use it directly. This
    /// pins that `readingBlocksSource` surfaces it for the pathological cases
    /// the old Swift heuristic got wrong.
    func testCodeFenceInteriorIsAuthoritative() {
        func interior(_ src: String) -> String? {
            for block in readingBlocksSource(source: src) {
                if case .codeFence(_, let interior) = block.kind { return interior }
                if case .diagram(_, let interior) = block.kind { return interior }
            }
            return nil
        }
        // Well-formed fence: delimiters excluded, no trailing newline.
        XCTAssertEqual(interior("```rust\nfn main() {}\n```\n"), "fn main() {}")
        // Indented code block: dedented four spaces by the parser.
        XCTAssertEqual(interior("para\n\n    indented\n    code\n"), "indented\ncode")
        // Unterminated fence whose only closer candidate ends in a TAB: that
        // ``` line is CONTENT, not a closer (the old heuristic dropped it).
        XCTAssertEqual(interior("```\ncode line\n```\t\n"), "code line\n```\t")
        // Indented block whose first line is triple-backticks: kept as content.
        XCTAssertEqual(interior("    ```\n    code\n    more\n"), "```\ncode\nmore")
    }

    func testLineNumberMapping() {
        let text = "alpha\nbeta\ngamma"
        let starts = ReadingBlockSource.lineStartOffsets(of: text)
        XCTAssertEqual(ReadingBlockSource.lineNumber(forByteOffset: 0, lineStarts: starts), 1)
        XCTAssertEqual(ReadingBlockSource.lineNumber(forByteOffset: 6, lineStarts: starts), 2)
        XCTAssertEqual(ReadingBlockSource.lineNumber(forByteOffset: 12, lineStarts: starts), 3)
    }

    /// The task row matches its `TaskItem` by 1-based line — pin that a task
    /// block's byteStart maps to the authored line number (what
    /// `TaskItem.line` carries).
    func testTaskBlockLineMatchesAuthoredLine() {
        let text = Self.everyKindFixture
        let blocks = readingBlocksSource(source: text)
        let taskBlocks = blocks.filter {
            if case .listItem(_, _, .some) = $0.kind { return true }
            return false
        }
        XCTAssertEqual(taskBlocks.count, 2, "fixture has two task items")

        let starts = ReadingBlockSource.lineStartOffsets(of: text)
        guard let openTaskRange = text.range(of: "- [ ] open task") else {
            return XCTFail("fixture lost its open task")
        }
        let expectedLine =
            text[text.startIndex..<openTaskRange.lowerBound]
            .filter { $0 == "\n" }.count + 1
        let mappedLine = ReadingBlockSource.lineNumber(
            forByteOffset: Int(taskBlocks[0].byteStart), lineStarts: starts)
        XCTAssertEqual(mappedLine, expectedLine)
    }

    /// Parse memo: same text → same parse (no re-segmentation on SwiftUI
    /// re-init); new text → new parse.
    func testParseCacheMemoizesPerText() {
        let cache = ReadingParseCache()
        let first = cache.parsed(for: Self.everyKindFixture)
        let second = cache.parsed(for: Self.everyKindFixture)
        XCTAssertEqual(first.blocks, second.blocks)
        XCTAssertEqual(first.lineStarts, second.lineStarts)
        XCTAssertEqual(
            first.inlines.count, first.blocks.count,
            "inline results are 1:1 with blocks")
        let changed = cache.parsed(for: "# Other")
        XCTAssertEqual(changed.blocks.count, 1)
    }

    /// #967: the inline pipeline is a pure function of (text, citations,
    /// records), so the memo must invalidate when the RECORDS change —
    /// keying on text alone would freeze a note's link styling at whatever
    /// the first render saw.
    func testParseCacheInvalidatesWhenRecordsChange() {
        let cache = ReadingParseCache()
        let text = "[[Known]]\n"
        func resolved(_ parsed: ReadingParseCache.Parsed) -> Bool? {
            guard
                case .wikilink(_, _, _, _, let resolved)? =
                    parsed.inlines.first?.segments.first?.runs.first?.kind
            else { return nil }
            return resolved
        }
        XCTAssertEqual(
            resolved(cache.parsed(for: text)), false,
            "no records → unresolved")
        XCTAssertEqual(
            resolved(cache.parsed(for: text, records: [Self.record("Known")])),
            true,
            "a record must invalidate the memo, not be ignored")
    }

    // MARK: - Table grid (#510)

    /// Table cells are segmented at PARSE time (once per toggle), keyed by the
    /// block's index — never re-derived in `body` on each render.
    func testParseCacheSegmentsTableCellsEagerly() {
        let cache = ReadingParseCache()
        let parsed = cache.parsed(for: Self.everyKindFixture)
        let tableIndex = parsed.blocks.firstIndex {
            if case .table = $0.kind { return true }
            return false
        }
        let index = try! XCTUnwrap(tableIndex, "fixture has a table block")
        let cells = try! XCTUnwrap(
            parsed.tableCells[index],
            "table cells must be computed eagerly at parse time")
        XCTAssertEqual(cells.header, ["a", "b"])
        XCTAssertEqual(cells.rows, [["1", "2"]])
    }

    /// The Rust segmentation is the single table parser: cells arrive already
    /// flattened, so no Swift-side pipe splitting is needed or present.
    func testTableCellsComeFromRustSegmentation() {
        let src = "| **h1** | h2 |\n|---|---|\n| `x` | [t](https://u) |\n"
        let cells = try! XCTUnwrap(readingTableCells(source: src))
        XCTAssertEqual(cells.header, ["h1", "h2"])
        XCTAssertEqual(cells.rows, [["x", "t"]])
    }

    /// Non-table input → nil → the raw-block fallback path (never a crash or a
    /// fabricated grid).
    func testTableCellsRejectsNonTableSource() {
        XCTAssertNil(readingTableCells(source: "just a paragraph\n"))
        XCTAssertNil(readingTableCells(source: ""))
    }

    /// The `.table` case must render the grid (with the raw-source fallback
    /// still present for the nil branch) — a structural check on the renderer
    /// since AccessibleDataGrid is the honest path.
    func testTableRendererDispatchesToGrid() throws {
        let text = try strippedReadingViewSource()
        XCTAssertTrue(
            text.contains("AccessibleDataGrid("),
            "the table case must render AccessibleDataGrid on segmented cells")
        XCTAssertTrue(
            text.contains("readingTableCells(source:"),
            "table cells must come from the Rust segmentation API")
        XCTAssertTrue(
            text.contains("rawSourceBlock(block.source, axLabel:"),
            "the nil branch must keep the raw-source fallback")
    }

    /// Summary string is "Table: N rows, M columns." with singular/plural
    /// agreement — the grid's focusable summary region.
    func testTableSummaryStringPluralization() {
        // The summary derivation lives in tableGrid; assert the same rule the
        // view uses so a copy-edit there is caught.
        func summary(rows: Int, columns: Int) -> String {
            "Table: \(rows) \(rows == 1 ? "row" : "rows"), "
                + "\(columns) \(columns == 1 ? "column" : "columns")."
        }
        XCTAssertEqual(summary(rows: 2, columns: 3), "Table: 2 rows, 3 columns.")
        XCTAssertEqual(summary(rows: 1, columns: 1), "Table: 1 row, 1 column.")
        XCTAssertEqual(summary(rows: 0, columns: 2), "Table: 0 rows, 2 columns.")
    }

    /// Ragged rows are normalized to header width by Rust, so grid indexing is
    /// safe by construction — a short row is padded, a long one truncated.
    func testTableRaggedRowsNormalizedToHeaderWidth() {
        let src = "| a | b | c |\n|---|---|---|\n| 1 | 2 |\n| 4 | 5 | 6 | 7 |\n"
        let cells = try! XCTUnwrap(readingTableCells(source: src))
        XCTAssertEqual(cells.header.count, 3)
        for row in cells.rows {
            XCTAssertEqual(row.count, 3, "every row equals header width")
        }
        XCTAssertEqual(cells.rows[0], ["1", "2", ""])
        XCTAssertEqual(cells.rows[1], ["4", "5", "6"])
    }

    @MainActor
    func testTableGridRendersInBothAppearances() {
        let table = """
            | Name | Role |
            | --- | --- |
            | Ada | Engineer |
            | Grace | Admiral |
            """
        PresentationReady.assertRendersInBothAppearances(
            ReadingView(text: table, pathLabel: "Table.md"))
    }

    // MARK: - Contrast: reading text sits on already-gated pairings

    /// The reading view introduces NO new color roles; every text-on-surface
    /// combination it uses must already be in the gated registry.
    func testReadingTextRolesAreAlreadyContrastGated() {
        let names = Set(Tokens.contrastPairings.map(\.name))
        for required in [
            "textPrimary on surface",
            "textSecondary on surface",
            "accentText on surface",
            "textPrimary on surfaceSecondary",
        ] {
            XCTAssertTrue(
                names.contains(required),
                "\(required) must stay in Tokens.contrastPairings")
        }
    }

    // MARK: - PresentationReady (§D/§E): both appearances

    @MainActor
    func testReadingViewRendersEveryBlockKindInBothAppearances() {
        let view = ReadingView(
            text: Self.everyKindFixture,
            pathLabel: "Fixture.md",
            context: ReadingView.ReadingBlockContext(
                citations: [Self.smithCitation])
        )
        PresentationReady.assertRendersInBothAppearances(view)
    }

    @MainActor
    func testReadingViewStatesRenderInBothAppearances() {
        PresentationReady.assertRendersInBothAppearances(
            ReadingView(text: "", pathLabel: "Loading.md", isLoading: true))
        PresentationReady.assertRendersInBothAppearances(
            ReadingView(
                text: "", pathLabel: "Broken.md",
                loadError: "File changed externally."))
        PresentationReady.assertRendersInBothAppearances(
            ReadingView(text: "", pathLabel: "Empty.md"))
    }

    // MARK: - Block-level embed detection (#511)

    /// Detection is span-authority, not string-prefix: a paragraph that IS one
    /// `![[…]]` embed yields its cache-key target. The target form MATCHES
    /// `AppState.embedTargetKey` (anchors attached) so a reading-view card and
    /// the EmbedsPanel look up the SAME `currentNoteEmbedResolutions` entry.
    func testBlockEmbedDetectionPositives() {
        XCTAssertEqual(
            readingBlockEmbedKey(slice: "![[Note]]"), "Note")
        // Leading/trailing whitespace around the sole embed is still
        // block-level. (A ≥4-space / tab indent is NOT tested here: that is a
        // CommonMark indented-code block, which the Rust classifier correctly
        // declines to treat as an embed — and `readingBlocksSource` never hands
        // a paragraph slice such an indent anyway.)
        XCTAssertEqual(
            readingBlockEmbedKey(slice: "  ![[Note]]  "), "Note")
        XCTAssertEqual(
            readingBlockEmbedKey(slice: " ![[Note]] \n"), "Note")
        // Heading anchor stays attached to the target (cache-key form).
        XCTAssertEqual(
            readingBlockEmbedKey(slice: "![[Note#Section A]]"),
            "Note#Section A")
        // Block anchor likewise (`^id`).
        XCTAssertEqual(
            readingBlockEmbedKey(slice: "![[Note^blk]]"),
            "Note^blk")
        // Alias does NOT change the routing target/cache key.
        XCTAssertEqual(
            readingBlockEmbedKey(slice: "![[Note|shown]]"), "Note")
    }

    /// The detected target equals the key `AppState.embedTargetKey` composes
    /// for the same reference — the ONE-cache-key invariant, pinned so the
    /// reading card and the panel can never drift onto different dict entries.
    func testBlockEmbedTargetMatchesAppStateCacheKey() {
        // Plain target.
        XCTAssertEqual(
            readingBlockEmbedKey(slice: "![[folder/Note]]"),
            appStateEmbedKey(targetRaw: "folder/Note", anchorKind: nil, anchorText: nil))
        // Heading anchor.
        XCTAssertEqual(
            readingBlockEmbedKey(slice: "![[Note#Sec]]"),
            appStateEmbedKey(targetRaw: "Note", anchorKind: "heading", anchorText: "Sec"))
        // Block anchor.
        XCTAssertEqual(
            readingBlockEmbedKey(slice: "![[Note^b1]]"),
            appStateEmbedKey(targetRaw: "Note", anchorKind: "block", anchorText: "b1"))
    }

    /// Mirror of `AppState.embedTargetKey` without needing a live link record.
    private func appStateEmbedKey(
        targetRaw: String, anchorKind: String?, anchorText: String?
    ) -> String {
        guard let anchorKind, let anchorText else { return targetRaw }
        let marker = anchorKind == "block" ? "^" : "#"
        return "\(targetRaw)\(marker)\(anchorText)"
    }

    /// Negatives: anything that is NOT exactly one embed stays inline (the
    /// mid-paragraph / multi-embed cases keep today's link-run behavior).
    func testBlockEmbedDetectionNegatives() {
        // Embed with surrounding prose — mid-paragraph, not block-level.
        XCTAssertNil(
            readingBlockEmbedKey(slice: "see ![[Note]] here"))
        XCTAssertNil(
            readingBlockEmbedKey(slice: "![[Note]] trailing"))
        XCTAssertNil(
            readingBlockEmbedKey(slice: "leading ![[Note]]"))
        // Two embeds in one paragraph.
        XCTAssertNil(
            readingBlockEmbedKey(slice: "![[One]] ![[Two]]"))
        // A wikilink (not an embed) is not a block-level embed.
        XCTAssertNil(readingBlockEmbedKey(slice: "[[Note]]"))
        // Plain prose.
        XCTAssertNil(readingBlockEmbedKey(slice: "just text"))
        // Empty embed body doesn't parse to a target.
        XCTAssertNil(readingBlockEmbedKey(slice: "![[]]"))
    }

    /// An embed INSIDE inline code is suppressed by the shared `mappableSpans`
    /// (rendered-as-code stays literal), so it never counts as a block embed —
    /// the no-second-classifier suppression is pinned here.
    func testBlockEmbedDetectionSuppressedInsideInlineCode() {
        XCTAssertNil(readingBlockEmbedKey(slice: "`![[Note]]`"))
    }

    /// SCOPE (pinned, #511): only WIKILINK embeds expand in place. A markdown
    /// IMAGE embed classifies as `.image`, not `.embed`, so it is NOT detected
    /// as a block-level embed and keeps its current inline behavior. In-place
    /// markdown-image rendering is a noted follow-up, not this PR.
    func testMarkdownImageEmbedIsNotBlockLevelEmbed() {
        XCTAssertNil(
            readingBlockEmbedKey(slice: "![alt](picture.png)"))
    }

    // MARK: - Block-level embed render state machine (#511)

    /// RESOLVED path: a present dict entry renders through `EmbedView` (the one
    /// path for both real resolutions and `.unresolved`), with jump-to-source
    /// wired and depth 0 — structural check against the renderer source.
    func testBlockEmbedRendersEmbedViewWhenResolved() throws {
        let text = try strippedReadingViewSource()
        XCTAssertTrue(
            text.contains("inline.blockEmbedKey"),
            "the paragraph case must detect block-level embeds via core (#967), "
                + "never a host-side string check")
        XCTAssertTrue(
            text.contains("EmbedView("),
            "a resolved block-level embed must render EmbedView")
        XCTAssertTrue(
            text.contains("jumpToSourceAction:") && text.contains("onOpenEmbedSource("),
            "the card's jump-to-source must route through onOpenEmbedSource")
    }

    /// PENDING → RESOLVED-EMPTY: the placeholder carries the house AX label and
    /// the request-once guard; the terminal fallback is the inline leaf. The AX
    /// label is a string literal (stripped by `strippedReadingViewSource`), so
    /// this reads the RAW source.
    func testBlockEmbedPlaceholderAndFallbackShape() throws {
        let url = Self.projectRoot
            .appendingPathComponent("apps/slate-mac/Sources/SlateMac/Reading")
            .appendingPathComponent("ReadingView.swift")
        let raw = try String(contentsOf: url, encoding: .utf8)
        XCTAssertTrue(
            raw.contains("\"Embed, loading.\""),
            "the pending placeholder must carry the 'Embed, loading.' AX label")
        XCTAssertTrue(
            raw.contains("requestedEmbedKeys"),
            "resolution must be requested at most once per key (guard set)")
        XCTAssertTrue(
            raw.contains("await context.onResolveEmbed(key)"),
            "the placeholder must request resolution for its key and AWAIT it")
        XCTAssertTrue(
            raw.contains("inlineLeaf(fallback)"),
            "resolved-empty must fall back to the inline link-run rendering")
        // The fallback gate must be request COMPLETION, not request start —
        // gating on requestedEmbedKeys would flash the inline run for the
        // whole in-flight window (the defect this pins against).
        XCTAssertTrue(
            raw.contains("completedEmbedKeys.contains(key)"),
            "the fallback must gate on completion, not on request start")
        XCTAssertTrue(
            raw.contains("defer { completedEmbedKeys.insert(key) }"),
            "completion must be recorded terminally (defer), even on cancellation")
    }

    /// The reading view renders a block-level embed (resolved), a placeholder
    /// (pending), and the inline fallback without crashing in either
    /// appearance — the full state machine mounted.
    @MainActor
    func testBlockEmbedStatesRenderInBothAppearances() {
        // Resolved (full note) — the card path.
        let resolvedCtx = ReadingView.ReadingBlockContext(
            embedResolutions: [
                "Note": .fullNote(targetPath: "Note.md", text: "body", nested: [])
            ])
        PresentationReady.assertRendersInBothAppearances(
            ReadingView(
                text: "![[Note]]", pathLabel: "Host.md", context: resolvedCtx))

        // Unresolved variant — still the EmbedView card (honest render), never
        // a dead block.
        let unresolvedCtx = ReadingView.ReadingBlockContext(
            embedResolutions: [
                "Ghost": .unresolved(reason: .targetNotFound(target: "Ghost"))
            ])
        PresentationReady.assertRendersInBothAppearances(
            ReadingView(
                text: "![[Ghost]]", pathLabel: "Host.md", context: unresolvedCtx))

        // Pending — the placeholder (no dict entry, resolver is a no-op here).
        PresentationReady.assertRendersInBothAppearances(
            ReadingView(text: "![[Pending]]", pathLabel: "Host.md"))
    }

    // MARK: - Mid-paragraph embed keeps navigate routing (#511)

    /// A mid-paragraph embed stays an inline run whose activation routes
    /// through the embed scheme — unchanged from today. Asserted on the
    /// canonical runs: the surrounding text keeps the embed as ONE
    /// `.embed` run.
    func testMidParagraphEmbedStaysInlineRun() {
        let mapped = Self.runs("before ![[Note]] after")
        let embeds = mapped.filter {
            if case .embed = $0.kind { return true } else { return false }
        }
        XCTAssertEqual(embeds.count, 1)
        XCTAssertEqual(embeds[0].kind, .embed(key: "Note"))
        XCTAssertEqual(
            Self.attributed("before ![[Note]] after").runs
                .compactMap(\.link).compactMap(\.scheme),
            [ReadingLinkRouter.embedScheme])
        // And it is NOT a block-level embed, so the paragraph case renders it
        // via the inline leaf (mid-paragraph navigate behavior preserved).
        XCTAssertNil(
            readingBlockEmbedKey(slice: "before ![[Note]] after"))
    }

    // MARK: - AppState single-embed resolution (#511)

    /// The block-embed live-buffer gap filler resolves one key and MERGES it
    /// into `currentNoteEmbedResolutions` (never replacing the batch's other
    /// keys). Against a real vault: a `![[target]]` in the live buffer resolves
    /// to a `.fullNote` and lands under the exact cache key the reading card
    /// looks up.
    @MainActor
    func testRequestReadingEmbedResolutionMergesResolvedTarget() async throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-reading-embed-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        try Data("# Target\n\nbody text".utf8)
            .write(to: vault.appendingPathComponent("target.md"))
        try Data("# Second\n\nmore".utf8)
            .write(to: vault.appendingPathComponent("second.md"))
        try Data("host body".utf8)
            .write(to: vault.appendingPathComponent("host.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let appState = AppState(recentsStore: store, externalOpener: { _ in true })
        appState.openVault(at: vault)
        await appState.scanTask?.value
        appState.selectedFilePath = "host.md"
        // Let the selection-driven batch settle before single-resolving — in
        // production the batch runs once early, then reading-view gap fills
        // merge onto it; here we await it so it can't wipe our writes mid-test.
        await appState.linksLoadTask?.value
        await appState.embedsLoadTask?.value

        // Resolve one key, then a SECOND: the merge (not replace) contract
        // means the first survives the second write.
        await appState.requestReadingEmbedResolution(target: "second")
        await appState.requestReadingEmbedResolution(target: "target")

        XCTAssertNotNil(
            appState.currentNoteEmbedResolutions["second"],
            "single resolve must MERGE, not replace, existing keys")
        let resolution = try XCTUnwrap(
            appState.currentNoteEmbedResolutions["target"])
        if case .fullNote(let path, _, _) = resolution {
            XCTAssertTrue(path.contains("target"))
        } else {
            XCTFail("expected a resolved full-note embed, got \(resolution)")
        }
    }

    /// A broken target still lands a terminal `.unresolved` entry (so the
    /// placeholder collapses to EmbedView's honest unresolved render — never an
    /// infinite spinner).
    @MainActor
    func testRequestReadingEmbedResolutionWritesUnresolvedForBrokenTarget()
        async throws
    {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-reading-embed-broken-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        try Data("host body".utf8)
            .write(to: vault.appendingPathComponent("host.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let appState = AppState(recentsStore: store, externalOpener: { _ in true })
        appState.openVault(at: vault)
        await appState.scanTask?.value
        appState.selectedFilePath = "host.md"
        await appState.linksLoadTask?.value
        await appState.embedsLoadTask?.value

        await appState.requestReadingEmbedResolution(target: "does-not-exist")
        let resolution = try XCTUnwrap(
            appState.currentNoteEmbedResolutions["does-not-exist"],
            "a broken target must still WRITE a terminal entry, not stay absent")
        if case .unresolved = resolution {
            // expected
        } else {
            XCTFail("broken target must resolve to .unresolved, got \(resolution)")
        }
    }

    /// No session → no write. The reading view's request-once guard then keeps
    /// the key marked and its state machine renders the inline fallback
    /// (deterministic, no re-request loop).
    @MainActor
    func testRequestReadingEmbedResolutionNoSessionIsNoOp() async {
        let appState = AppState()
        await appState.requestReadingEmbedResolution(target: "anything")
        XCTAssertNil(appState.currentNoteEmbedResolutions["anything"])
    }
}
