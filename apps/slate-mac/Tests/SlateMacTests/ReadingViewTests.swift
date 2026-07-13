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

    // MARK: - Inline mapper: runs + labels + URLs

    func testMapperWikilinkWithAlias() {
        let mapped = ReadingInlineMapper.map(slice: "See [[Note One|the note]] now.")
        XCTAssertEqual(
            mapped.runs,
            [
                ReadingInlineMapper.MappedRun(
                    kind: .wiki,
                    display: "the note",
                    target: "Note One",
                    url: URL(string: "slate-wiki://Note%20One")!,
                    axLabel: "the note")
            ])
        let rendered = String(mapped.attributed.characters)
        XCTAssertTrue(rendered.contains("the note"))
        XCTAssertFalse(rendered.contains("[["), "wikilink chrome must not render")
        XCTAssertEqual(
            mapped.attributed.runs.compactMap(\.link),
            [URL(string: "slate-wiki://Note%20One")!])
    }

    func testMapperWikilinkWithoutAliasKeepsTargetWithAnchor() {
        let mapped = ReadingInlineMapper.map(slice: "[[Note#Section]]")
        XCTAssertEqual(mapped.runs.count, 1)
        XCTAssertEqual(mapped.runs[0].kind, .wiki)
        XCTAssertEqual(mapped.runs[0].display, "Note#Section")
        XCTAssertEqual(mapped.runs[0].target, "Note#Section")
        XCTAssertEqual(
            mapped.runs[0].url, URL(string: "slate-wiki://Note%23Section")!)
    }

    func testMapperTag() {
        let mapped = ReadingInlineMapper.map(slice: "Hello #alpha world")
        XCTAssertEqual(
            mapped.runs,
            [
                ReadingInlineMapper.MappedRun(
                    kind: .tag,
                    display: "#alpha",
                    target: "alpha",
                    url: URL(string: "slate-tag://alpha")!,
                    axLabel: "#alpha")
            ])
    }

    /// Citation runs: visible text is the RENDERED form, the AX label is the
    /// SPEECH text (Milestone L — same `speechText` source CitationsPanel
    /// uses), carried per-range via `accessibilityTextCustom`.
    func testMapperCitationWithMatchCarriesSpeechText() {
        let mapped = ReadingInlineMapper.map(
            slice: "As shown [@smith2020].",
            citations: [Self.smithCitation])
        XCTAssertEqual(mapped.runs.count, 1)
        XCTAssertEqual(mapped.runs[0].kind, .citation)
        XCTAssertEqual(mapped.runs[0].display, "(Smith, 2020)")
        XCTAssertEqual(mapped.runs[0].target, "[@smith2020]")
        XCTAssertEqual(mapped.runs[0].axLabel, "Smith, two thousand twenty.")
        XCTAssertEqual(
            mapped.runs[0].url,
            URL(string: "slate-cite://%5B%40smith2020%5D")!)

        let rendered = String(mapped.attributed.characters)
        XCTAssertTrue(rendered.contains("(Smith, 2020)"))
        XCTAssertFalse(rendered.contains("[@smith2020]"))

        var speech: [String]?
        for run in mapped.attributed.runs where run.link != nil {
            speech = mapped.attributed[run.range][
                AttributeScopes.AccessibilityAttributes.TextCustomAttribute.self]
        }
        XCTAssertEqual(
            speech, ["Smith, two thousand twenty."],
            "citation run must carry its speech text for AT")
    }

    func testMapperCitationWithoutMatchDegradesToRaw() {
        let mapped = ReadingInlineMapper.map(slice: "As shown [@ghost1999].")
        XCTAssertEqual(mapped.runs.count, 1)
        XCTAssertEqual(mapped.runs[0].display, "[@ghost1999]")
        XCTAssertEqual(mapped.runs[0].axLabel, "[@ghost1999]")
    }

    /// Embed alt-text contract: alias, else target NAME — never empty.
    func testMapperEmbedDisplayNames() {
        let aliased = ReadingInlineMapper.map(slice: "![[img.png|photo]]")
        XCTAssertEqual(aliased.runs.count, 1)
        XCTAssertEqual(aliased.runs[0].kind, .embed)
        XCTAssertEqual(aliased.runs[0].display, "photo")

        let named = ReadingInlineMapper.map(slice: "![[folder/pic one.png]]")
        XCTAssertEqual(named.runs.count, 1)
        XCTAssertEqual(named.runs[0].display, "pic one.png")
        XCTAssertEqual(named.runs[0].target, "folder/pic one.png")
        XCTAssertFalse(named.runs[0].axLabel.isEmpty, "image embeds need a non-empty AX label")
    }

    /// External markdown links are NOT remapped — they keep their real URL
    /// (activation passes through to the system) but still get link styling.
    func testMapperLeavesExternalLinksToFoundation() {
        let mapped = ReadingInlineMapper.map(slice: "Visit [site](https://example.com).")
        XCTAssertTrue(mapped.runs.isEmpty)
        XCTAssertEqual(
            mapped.attributed.runs.compactMap(\.link),
            [URL(string: "https://example.com")!])
    }

    /// Every link run — slate or external — carries accent + underline (the
    /// affordance is not conveyed by color alone).
    func testMapperStylesLinkRuns() {
        let mapped = ReadingInlineMapper.map(slice: "See [[Note]] and [x](https://x.com).")
        var styledRuns = 0
        for run in mapped.attributed.runs where run.link != nil {
            styledRuns += 1
            let color = mapped.attributed[run.range][
                AttributeScopes.SwiftUIAttributes.ForegroundColorAttribute.self]
            let underline = mapped.attributed[run.range][
                AttributeScopes.SwiftUIAttributes.UnderlineStyleAttribute.self]
            XCTAssertEqual(color, Tokens.ColorRole.accentText)
            XCTAssertNotNil(underline)
        }
        XCTAssertEqual(styledRuns, 2)
    }

    // MARK: - Unresolved wikilink styling (#849)

    /// Foreground color of the FIRST link run whose activation URL routes
    /// to the given wiki target (base form).
    private func linkRunColor(
        in mapped: ReadingInlineMapper.Mapped, target: String
    ) -> Color? {
        for run in mapped.attributed.runs {
            guard let link = run.link,
                case .wiki(let t, _) = ReadingLinkRouter.disposition(for: link),
                ReadingLinkRouter.baseTarget(of: t) == target
            else { continue }
            return mapped.attributed[run.range][
                AttributeScopes.SwiftUIAttributes.ForegroundColorAttribute.self]
        }
        return nil
    }

    /// With membership, a dangling wikilink renders in warningText (the
    /// editor's U5-3 treatment) while resolved siblings keep accent — and
    /// the underline stays on BOTH (the affordance is never color-only).
    func testMapperStylesUnresolvedWikilinkWithWarningText() {
        let mapped = ReadingInlineMapper.map(
            slice: "See [[Missing]] and [[There]].",
            unresolvedTargets: ["Missing"])
        XCTAssertEqual(
            linkRunColor(in: mapped, target: "Missing"),
            Tokens.ColorRole.warningText)
        XCTAssertEqual(
            linkRunColor(in: mapped, target: "There"),
            Tokens.ColorRole.accentText)
        for run in mapped.attributed.runs where run.link != nil {
            XCTAssertNotNil(
                mapped.attributed[run.range][
                    AttributeScopes.SwiftUIAttributes.UnderlineStyleAttribute.self],
                "underline marks activatable on resolved AND unresolved runs")
        }
    }

    /// Without membership nothing changes — the empty default is exactly
    /// the pre-#849 rendering.
    func testMapperWithoutMembershipKeepsAccentStyling() {
        let mapped = ReadingInlineMapper.map(slice: "See [[Missing]].")
        XCTAssertEqual(
            linkRunColor(in: mapped, target: "Missing"),
            Tokens.ColorRole.accentText)
    }

    /// Key normalization: the run's target carries the anchor
    /// (`Note#Section`); the membership key is the router's resolution key
    /// — the anchor-STRIPPED base form `links.rs` stores as `targetRaw` —
    /// so styling and activation agree about the same run.
    func testUnresolvedMembershipUsesRouterBaseTargetKey() {
        let mapped = ReadingInlineMapper.map(
            slice: "[[Missing#Section]]",
            unresolvedTargets: ["Missing"])
        XCTAssertEqual(
            linkRunColor(in: mapped, target: "Missing"),
            Tokens.ColorRole.warningText,
            "anchor forms resolve through the base-target key, like the router")
    }

    /// Case sensitivity: the router's record match is case-SENSITIVE
    /// (`targetRaw ==`), so membership must be too — a different-cased
    /// entry must NOT mark the run, or styling would disagree with
    /// activation.
    /// Codex rounds 1+2: the match keys are EXACT per grammar. Wiki
    /// grammar cuts at `#` (an earlier `^` stays in the base), else the
    /// first `^` (legacy block ref); markdown grammar never cuts at `^`.
    /// The verbatim defense closes each list.
    func testCandidateKeysAreExactPerGrammar() {
        XCTAssertEqual(
            ReadingLinkRouter.candidateKeys(for: "note^draft#sec", grammar: .wikilink),
            ["note^draft", "note^draft#sec"],
            "wiki grammar cuts at # even with an earlier ^")
        XCTAssertEqual(
            ReadingLinkRouter.candidateKeys(for: "note^block", grammar: .wikilink),
            ["note", "note^block"],
            "legacy block ref cuts FIRST — no markdown-form shadow record")
        XCTAssertEqual(
            ReadingLinkRouter.candidateKeys(for: "note#^block", grammar: .wikilink),
            ["note", "note#^block"],
            "canonical block ref cuts at the #")
        XCTAssertEqual(
            ReadingLinkRouter.candidateKeys(
                for: "note^draft.md", grammar: .markdownDestination),
            ["note^draft.md"],
            "a bare ^ is a legal markdown path character — never cut")
        XCTAssertEqual(
            ReadingLinkRouter.candidateKeys(
                for: "note^draft.md#sec", grammar: .markdownDestination),
            ["note^draft.md", "note^draft.md#sec"])
        XCTAssertEqual(
            ReadingLinkRouter.candidateKeys(for: "note", grammar: .wikilink),
            ["note"])
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
    }

    /// End-to-end `#`-outranks-`^` for wiki runs: `[[note^draft#sec]]`
    /// must match a record keyed `note^draft` (links.rs cuts at the #;
    /// the old first-marker cut searched for `note` and missed).
    func testWikiHashOutranksCaretEndToEnd() {
        let mapped = ReadingInlineMapper.map(
            slice: "[[note^draft#sec]]",
            unresolvedTargets: ["note^draft"])
        XCTAssertEqual(
            linkRunColor(in: mapped, target: "note^draft"),
            Tokens.ColorRole.warningText,
            "the record key is the hash-cut base — the ^ stays in it")
    }

    /// A `^` in a markdown destination: Foundation's URL bridging
    /// percent-encodes the authored bytes inside the native markdown
    /// parse, BEFORE the mapper sees the link — so resolving `^`-paths
    /// authored as markdown links is a platform limitation, not a
    /// grammar bug. What MUST hold is agreement: activation through
    /// `candidateKeys` finds no record for the mangled form and
    /// announces unresolved, so styling (known set supplied) must
    /// warn — never resolved accent. Pinned so a bridging change in a
    /// future SDK is noticed.
    func testMarkdownCaretPathStylingAgreesWithActivation() {
        var sets = ReadingLinkRouter.LinkRecordSets()
        sets.knownMarkdown = ["note^draft.md"]
        let mapped = ReadingInlineMapper.map(
            slice: "See [t](note^draft.md#sec).", recordSets: sets)
        var sawWikiRun = false
        for run in mapped.attributed.runs {
            guard let link = run.link,
                case .wiki = ReadingLinkRouter.disposition(for: link)
            else { continue }
            sawWikiRun = true
            XCTAssertEqual(
                mapped.attributed[run.range][
                    AttributeScopes.SwiftUIAttributes.ForegroundColorAttribute.self],
                Tokens.ColorRole.warningText,
                "whatever form the bridging produced, never a resolved accent")
        }
        XCTAssertTrue(
            sawWikiRun,
            "the rewritten markdown run must survive with a wiki route")
    }

    /// Codex review: a run with NO saved record activates as
    /// "unresolved. Cannot open." (live-buffer text the index hasn't
    /// seen, or a query that hasn't landed) — accent styling would lie.
    /// With the known set supplied, missing-record runs style
    /// unresolved; recorded runs follow their record.
    func testMissingRecordStylesUnresolvedWhenRecordSetsSupplied() {
        let missing = ReadingInlineMapper.map(
            slice: "[[Ghost]]",
            recordSets: ReadingLinkRouter.LinkRecordSets())
        XCTAssertEqual(
            linkRunColor(in: missing, target: "Ghost"),
            Tokens.ColorRole.warningText,
            "no record → activation announces unresolved → styling agrees")

        var known = ReadingLinkRouter.LinkRecordSets()
        known.knownWikilink = ["Ghost"]
        let resolved = ReadingInlineMapper.map(
            slice: "[[Ghost]]", recordSets: known)
        XCTAssertEqual(
            linkRunColor(in: resolved, target: "Ghost"),
            Tokens.ColorRole.accentText)

        var dangling = known
        dangling.unresolvedWikilink = ["Ghost"]
        let recorded = ReadingInlineMapper.map(
            slice: "[[Ghost]]", recordSets: dangling)
        XCTAssertEqual(
            linkRunColor(in: recorded, target: "Ghost"),
            Tokens.ColorRole.warningText)
    }

    /// Codex round 3: the sets are KIND-partitioned — a saved markdown
    /// record must never vouch for an unsaved wikilink spelling the
    /// same characters (activation applies the same rule through
    /// `recordKindMatches`).
    func testStylingNeverClassifiesAcrossGrammars() {
        var sets = ReadingLinkRouter.LinkRecordSets()
        sets.knownMarkdown = ["note^block"]
        let mapped = ReadingInlineMapper.map(
            slice: "[[note^block]]", recordSets: sets)
        XCTAssertEqual(
            linkRunColor(in: mapped, target: "note"),
            Tokens.ColorRole.warningText,
            "the markdown record is the OTHER grammar's — record-less here")
    }

    /// And the record-sets builder itself partitions by `kind`,
    /// skipping embeds and externals.
    func testLinkRecordSetsPartitionByKind() {
        let records = [
            OutgoingLink(
                targetPath: "n.md", targetRaw: "n", targetAnchor: nil,
                kind: "wikilink", isEmbed: false, isExternal: false,
                isUnresolved: false, snippet: "", ordinal: 0,
                displayText: nil),
            OutgoingLink(
                targetPath: nil, targetRaw: "m.md", targetAnchor: nil,
                kind: "markdown", isEmbed: false, isExternal: false,
                isUnresolved: true, snippet: "", ordinal: 1,
                displayText: nil),
            OutgoingLink(
                targetPath: nil, targetRaw: "e", targetAnchor: nil,
                kind: "wikilink", isEmbed: true, isExternal: false,
                isUnresolved: false, snippet: "", ordinal: 2,
                displayText: nil),
            OutgoingLink(
                targetPath: nil, targetRaw: "https://x", targetAnchor: nil,
                kind: "markdown", isEmbed: false, isExternal: true,
                isUnresolved: false, snippet: "", ordinal: 3,
                displayText: nil),
        ]
        let sets = ReadingLinkRouter.LinkRecordSets(records: records)
        XCTAssertEqual(sets.knownWikilink, ["n"])
        XCTAssertEqual(sets.unresolvedWikilink, [])
        XCTAssertEqual(sets.knownMarkdown, ["m.md"])
        XCTAssertEqual(sets.unresolvedMarkdown, ["m.md"])
    }

    /// Red-team probe (confirmed): whitespace-padded and anchored
    /// targets must trim to the router's key — `[[ Missing ]]` and
    /// `[[Missing #Section]]` are the same unresolved target.
    func testUnresolvedMembershipTrimsWhitespaceLikeLinksRs() {
        let padded = ReadingInlineMapper.map(
            slice: "[[ Missing ]]",
            unresolvedTargets: ["Missing"])
        XCTAssertEqual(
            linkRunColor(in: padded, target: "Missing"),
            Tokens.ColorRole.warningText,
            "padded target trims to the unresolved key")
        let anchored = ReadingInlineMapper.map(
            slice: "[[Missing #Section]]",
            unresolvedTargets: ["Missing"])
        XCTAssertEqual(
            linkRunColor(in: anchored, target: "Missing"),
            Tokens.ColorRole.warningText,
            "space-before-anchor trims to the base target")
    }

    func testUnresolvedMembershipIsCaseSensitiveLikeTheRouter() {
        let mapped = ReadingInlineMapper.map(
            slice: "[[Missing]]",
            unresolvedTargets: ["missing"])
        XCTAssertEqual(
            linkRunColor(in: mapped, target: "Missing"),
            Tokens.ColorRole.accentText,
            "case-mismatched membership must not style the run unresolved")
    }

    /// Internal markdown links rewritten onto the wiki scheme take the
    /// same unresolved treatment — they activate through the same router
    /// branch.
    func testUnresolvedStylingCoversRewrittenMarkdownLinks() {
        let mapped = ReadingInlineMapper.map(
            slice: "See [t](gone.md).",
            unresolvedTargets: ["gone.md"])
        XCTAssertEqual(
            linkRunColor(in: mapped, target: "gone.md"),
            Tokens.ColorRole.warningText)
    }

    /// The unresolved state is announced, not color-only: the run carries
    /// the AX custom-text suffix.
    func testUnresolvedRunCarriesAccessibilitySuffix() {
        let mapped = ReadingInlineMapper.map(
            slice: "[[Missing]]",
            unresolvedTargets: ["Missing"])
        var found = false
        for run in mapped.attributed.runs where run.link != nil {
            if mapped.attributed[run.range][
                AttributeScopes.AccessibilityAttributes.TextCustomAttribute.self]
                == ["Unresolved link"]
            {
                found = true
            }
        }
        XCTAssertTrue(found, "unresolved runs carry the AX custom-text suffix")
    }

    /// Internal markdown destinations (scheme-less; Slate semantics:
    /// vault-rooted/basename, stored literally) are rewritten onto the wiki
    /// scheme so they activate — never styled-then-dead.
    func testMapperRewritesInternalMarkdownLinksToWikiScheme() {
        let mapped = ReadingInlineMapper.map(slice: "See [t](note.md).")
        let links = mapped.attributed.runs.compactMap(\.link)
        XCTAssertEqual(links.count, 1)
        XCTAssertEqual(links[0].scheme, ReadingLinkRouter.wikiMarkdownScheme)
        XCTAssertEqual(
            ReadingLinkRouter.disposition(for: links[0]),
            .wiki("note.md", .markdownDestination))
    }

    /// The authored destination travels VERBATIM — fragments kept (markdown
    /// `targetRaw` stores them), percent-escapes NOT decoded (Slate never
    /// decodes markdown destinations).
    func testMapperRewriteKeepsFragmentAndPercentEscapesLiteral() {
        let anchored = ReadingInlineMapper.map(slice: "[t](note.md#sec)")
        XCTAssertEqual(
            anchored.attributed.runs.compactMap(\.link).compactMap {
                ReadingLinkRouter.disposition(for: $0)
            },
            [.wiki("note.md#sec", .markdownDestination)])
        let escaped = ReadingInlineMapper.map(slice: "[t](my%20note.md)")
        XCTAssertEqual(
            escaped.attributed.runs.compactMap(\.link).compactMap {
                ReadingLinkRouter.disposition(for: $0)
            },
            [.wiki("my%20note.md", .markdownDestination)])
    }

    /// Non-activatable destinations lose the link attribute entirely: no
    /// dead affordance visually or to VoiceOver, and nothing ever reaches
    /// LaunchServices. Fragment-only and protocol-relative references mirror
    /// `links.rs::looks_external` (not internal notes → not rewritten).
    func testMapperStripsLinkAffordanceFromNonActivatableDestinations() {
        for slice in [
            "[t](javascript:alert(1))",
            "[t](file:///etc/passwd)",
            "[t](ftp://host/x)",
            "[t](#intro)",
            "[t](//host/x)",
        ] {
            let mapped = ReadingInlineMapper.map(slice: slice)
            XCTAssertEqual(
                mapped.attributed.runs.compactMap(\.link), [],
                "\(slice) must not render as an activatable link")
            XCTAssertTrue(
                String(mapped.attributed.characters).contains("t"),
                "\(slice) keeps its display text")
        }
    }

    /// Slate tokens inside markdown-link syntax stay literal — the link is
    /// the construct there. `#intro` in the label (or a fragment-only
    /// destination) must not be spliced into a tag link, which would corrupt
    /// the destination the native markdown parse consumes.
    func testMapperDoesNotSpliceSlateTokensInsideMarkdownLinks() {
        let mapped = ReadingInlineMapper.map(
            slice: "see [about #intro](note.md) here")
        XCTAssertTrue(
            mapped.runs.isEmpty,
            "no Slate-token runs may be minted inside a markdown link")
        let links = mapped.attributed.runs.compactMap(\.link)
        XCTAssertEqual(links.count, 1)
        XCTAssertEqual(
            ReadingLinkRouter.disposition(for: links[0]),
            .wiki("note.md", .markdownDestination))
        XCTAssertTrue(
            String(mapped.attributed.characters).contains("about #intro"),
            "label text renders verbatim")
    }

    func testMapperPreservesDocumentOrderAcrossKinds() {
        let mapped = ReadingInlineMapper.map(slice: "A [[x]] then #t end.")
        XCTAssertEqual(mapped.runs.map(\.kind), [.wiki, .tag])
    }

    /// The Rust span classifier is the single syntax authority: a wikilink
    /// inside inline code is code, not a link.
    func testMapperRespectsInlineCodeSuppression() {
        let mapped = ReadingInlineMapper.map(slice: "`[[not a link]]` and [[real]]")
        XCTAssertEqual(mapped.runs.count, 1)
        XCTAssertEqual(mapped.runs[0].target, "real")
        let rendered = String(mapped.attributed.characters)
        XCTAssertTrue(
            rendered.contains("[[not a link]]"),
            "code-span content must stay literal")
    }

    /// Markdown-meaningful characters in display text can't break out of the
    /// link label.
    func testMapperEscapesDisplayText() {
        XCTAssertEqual(
            ReadingInlineMapper.escapeMarkdownLabel("a*b]c[d"),
            "a\\*b\\]c\\[d")
        // An alias containing `]` survives end-to-end.
        let mapped = ReadingInlineMapper.map(slice: "[[a|x]y]]")
        XCTAssertEqual(mapped.runs.count, 1)
        XCTAssertEqual(mapped.runs[0].display, "x]y")
        XCTAssertTrue(String(mapped.attributed.characters).contains("x]y"))
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

    func testBaseTargetStripsAnchors() {
        XCTAssertEqual(ReadingLinkRouter.baseTarget(of: "Note#Sec"), "Note")
        XCTAssertEqual(ReadingLinkRouter.baseTarget(of: "Note^blk"), "Note")
        XCTAssertEqual(ReadingLinkRouter.baseTarget(of: "Plain"), "Plain")
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

    // MARK: - Block-source helpers

    func testHeadingTextStripping() {
        XCTAssertEqual(ReadingBlockSource.headingText("## Title"), "Title")
        XCTAssertEqual(ReadingBlockSource.headingText("# Title ##"), "Title")
        XCTAssertEqual(ReadingBlockSource.headingText("Title\n====="), "Title")
        XCTAssertEqual(ReadingBlockSource.headingText("Title\n---"), "Title")
    }

    func testListItemParts() {
        let bullet = ReadingBlockSource.listItemParts("- foo")
        XCTAssertEqual(bullet?.marker, "-")
        XCTAssertEqual(bullet?.content, "foo")
        XCTAssertNil(bullet?.taskChar)

        let nested = ReadingBlockSource.listItemParts("  - bar")
        XCTAssertEqual(nested?.content, "bar")

        let ordered = ReadingBlockSource.listItemParts("12. twelve")
        XCTAssertEqual(ordered?.marker, "12.")
        XCTAssertEqual(ordered?.content, "twelve")

        let paren = ReadingBlockSource.listItemParts("3) three")
        XCTAssertEqual(paren?.marker, "3)")

        let task = ReadingBlockSource.listItemParts(
            "- [x] done thing", stripTaskBox: true)
        XCTAssertEqual(task?.taskChar, "x")
        XCTAssertEqual(task?.content, "done thing")

        let multi = ReadingBlockSource.listItemParts("- a\n  continued")
        XCTAssertEqual(multi?.content, "a\n  continued")

        XCTAssertNil(ReadingBlockSource.listItemParts("not a list"))
    }

    /// Taskhood belongs to the Rust classifier — the splitter must NOT strip
    /// a boxy-looking prefix from PLAIN list items (Codoki, #514). The two
    /// reachable shapes: ordered items (never tasks in the Rust grammar) and
    /// a box with no following space.
    func testListItemPartsKeepsBracketTextOnPlainItems() {
        let ordered = ReadingBlockSource.listItemParts("1. [v] Visible")
        XCTAssertEqual(ordered?.content, "[v] Visible")
        XCTAssertNil(ordered?.taskChar)

        let noSpace = ReadingBlockSource.listItemParts("- [v]x")
        XCTAssertEqual(noSpace?.content, "[v]x")
        XCTAssertNil(noSpace?.taskChar)

        // Default (no flag) never strips, even for the canonical task shape.
        let unflagged = ReadingBlockSource.listItemParts("- [x] done")
        XCTAssertEqual(unflagged?.content, "[x] done")
        XCTAssertNil(unflagged?.taskChar)
    }

    func testQuoteContentStripping() {
        XCTAssertEqual(ReadingBlockSource.quoteContent("> quoted", depth: 1), "quoted")
        XCTAssertEqual(ReadingBlockSource.quoteContent("> > deep", depth: 2), "deep")
        XCTAssertEqual(
            ReadingBlockSource.quoteContent("> a\n> b", depth: 1), "a\nb")
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
        let changed = cache.parsed(for: "# Other")
        XCTAssertEqual(changed.blocks.count, 1)
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
            ReadingInlineMapper.blockEmbedTarget(inSlice: "![[Note]]"), "Note")
        // Leading/trailing whitespace around the sole embed is still
        // block-level. (A ≥4-space / tab indent is NOT tested here: that is a
        // CommonMark indented-code block, which the Rust classifier correctly
        // declines to treat as an embed — and `readingBlocksSource` never hands
        // a paragraph slice such an indent anyway.)
        XCTAssertEqual(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "  ![[Note]]  "), "Note")
        XCTAssertEqual(
            ReadingInlineMapper.blockEmbedTarget(inSlice: " ![[Note]] \n"), "Note")
        // Heading anchor stays attached to the target (cache-key form).
        XCTAssertEqual(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "![[Note#Section A]]"),
            "Note#Section A")
        // Block anchor likewise (`^id`).
        XCTAssertEqual(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "![[Note^blk]]"),
            "Note^blk")
        // Alias does NOT change the routing target/cache key.
        XCTAssertEqual(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "![[Note|shown]]"), "Note")
    }

    /// The detected target equals the key `AppState.embedTargetKey` composes
    /// for the same reference — the ONE-cache-key invariant, pinned so the
    /// reading card and the panel can never drift onto different dict entries.
    func testBlockEmbedTargetMatchesAppStateCacheKey() {
        // Plain target.
        XCTAssertEqual(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "![[folder/Note]]"),
            appStateEmbedKey(targetRaw: "folder/Note", anchorKind: nil, anchorText: nil))
        // Heading anchor.
        XCTAssertEqual(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "![[Note#Sec]]"),
            appStateEmbedKey(targetRaw: "Note", anchorKind: "heading", anchorText: "Sec"))
        // Block anchor.
        XCTAssertEqual(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "![[Note^b1]]"),
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
            ReadingInlineMapper.blockEmbedTarget(inSlice: "see ![[Note]] here"))
        XCTAssertNil(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "![[Note]] trailing"))
        XCTAssertNil(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "leading ![[Note]]"))
        // Two embeds in one paragraph.
        XCTAssertNil(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "![[One]] ![[Two]]"))
        // A wikilink (not an embed) is not a block-level embed.
        XCTAssertNil(ReadingInlineMapper.blockEmbedTarget(inSlice: "[[Note]]"))
        // Plain prose.
        XCTAssertNil(ReadingInlineMapper.blockEmbedTarget(inSlice: "just text"))
        // Empty embed body doesn't parse to a target.
        XCTAssertNil(ReadingInlineMapper.blockEmbedTarget(inSlice: "![[]]"))
    }

    /// An embed INSIDE inline code is suppressed by the shared `mappableSpans`
    /// (rendered-as-code stays literal), so it never counts as a block embed —
    /// the no-second-classifier suppression is pinned here.
    func testBlockEmbedDetectionSuppressedInsideInlineCode() {
        XCTAssertNil(ReadingInlineMapper.blockEmbedTarget(inSlice: "`![[Note]]`"))
    }

    /// SCOPE (pinned, #511): only WIKILINK embeds expand in place. A markdown
    /// IMAGE embed classifies as `.image`, not `.embed`, so it is NOT detected
    /// as a block-level embed and keeps its current inline behavior. In-place
    /// markdown-image rendering is a noted follow-up, not this PR.
    func testMarkdownImageEmbedIsNotBlockLevelEmbed() {
        XCTAssertNil(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "![alt](picture.png)"))
    }

    // MARK: - Block-level embed render state machine (#511)

    /// RESOLVED path: a present dict entry renders through `EmbedView` (the one
    /// path for both real resolutions and `.unresolved`), with jump-to-source
    /// wired and depth 0 — structural check against the renderer source.
    func testBlockEmbedRendersEmbedViewWhenResolved() throws {
        let text = try strippedReadingViewSource()
        XCTAssertTrue(
            text.contains("blockEmbedTarget(inSlice:"),
            "the paragraph case must detect block-level embeds via the span authority")
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
            raw.contains("inlineLeaf(fallbackSlice)"),
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
    /// through the embed scheme — unchanged from today. Asserted at the mapper:
    /// the surrounding text keeps the embed as ONE `.embed` run.
    func testMidParagraphEmbedStaysInlineRun() {
        let mapped = ReadingInlineMapper.map(slice: "before ![[Note]] after")
        XCTAssertEqual(mapped.runs.count, 1)
        XCTAssertEqual(mapped.runs[0].kind, .embed)
        XCTAssertEqual(mapped.runs[0].target, "Note")
        XCTAssertEqual(mapped.runs[0].url.scheme, ReadingLinkRouter.embedScheme)
        // And it is NOT a block-level embed, so the paragraph case renders it
        // via the inline leaf (mid-paragraph navigate behavior preserved).
        XCTAssertNil(
            ReadingInlineMapper.blockEmbedTarget(inSlice: "before ![[Note]] after"))
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
