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
        XCTAssertEqual(disposition("slate-wiki://Note%20One"), .wiki("Note One"))
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
            openWikiLink: { recorder.events.append("wiki:\($0)") },
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

    /// Tag activation prefilters the vault-wide search overlay (approximate
    /// scope — `SearchScope::Tag` is Unsupported in the backend; documented
    /// in the router).
    @MainActor
    func testLiveRouterTagPrefiltersSearchOverlay() {
        let appState = AppState()
        let router = ReadingLinkRouter.live(appState: appState)
        router.openTag("alpha")
        XCTAssertEqual(appState.searchQuery, "alpha")
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
        ReadingLinkRouter.live(appState: appState).openWikiLink("Nowhere")
        XCTAssertNil(appState.selectedFilePath)
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

        let task = ReadingBlockSource.listItemParts("- [x] done thing")
        XCTAssertEqual(task?.taskChar, "x")
        XCTAssertEqual(task?.content, "done thing")

        let multi = ReadingBlockSource.listItemParts("- a\n  continued")
        XCTAssertEqual(multi?.content, "a\n  continued")

        XCTAssertNil(ReadingBlockSource.listItemParts("not a list"))
    }

    func testQuoteContentStripping() {
        XCTAssertEqual(ReadingBlockSource.quoteContent("> quoted", depth: 1), "quoted")
        XCTAssertEqual(ReadingBlockSource.quoteContent("> > deep", depth: 2), "deep")
        XCTAssertEqual(
            ReadingBlockSource.quoteContent("> a\n> b", depth: 1), "a\nb")
    }

    func testFenceInterior() {
        XCTAssertEqual(
            ReadingBlockSource.fenceInterior("```rust\nfn main() {}\n```"),
            "fn main() {}")
        XCTAssertEqual(
            ReadingBlockSource.fenceInterior("    indented\n    code"),
            "indented\ncode")
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
}
