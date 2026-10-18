// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// #1224 (plan 43 PR 17): the editor's canonical spans reach VoiceOver as
/// accessibility text attributes, the twin of the Windows editor peer's
/// text-range attributes (W7-1 #747, contract 37 E-4/E-9,
/// `EditorSemanticTextRangeTests`).
///
/// Three layers, one source. The pure map (`facets(for:)`, one fact per
/// span kind, lock-step with the frozen E-4 table); the plan the colorizer's
/// off-main pass builds from the same windowed spans it paints; and what an
/// assistive client reads back through the text view's
/// `accessibilityAttributedString(for:)` and its Links rotor. XCTest proves
/// attributes, not speech: the spoken half is the PR's VoiceOver field pass.
@MainActor
final class EditorAccessibilityAttributesTests: XCTestCase {

    private typealias Facets = EditorAccessibilityAttributes.Facets

    private func facets(_ kind: EditorSpanKind) -> Facets {
        EditorAccessibilityAttributes.facets(for: kind)
    }

    // MARK: - The map, one fact per span kind

    func testHeadingCarriesItsLevelAndStyleName() {
        for level in UInt8(1)...6 {
            XCTAssertEqual(
                facets(.heading(level: level)),
                Facets(headingLevel: Int(level), styleName: "Heading \(level)"))
        }
    }

    func testWikilinkIsALinkStyledWikilink() {
        XCTAssertEqual(facets(.wikilink), Facets(styleName: "Wikilink", isLink: true))
    }

    func testLinkIsALinkStyledLink() {
        XCTAssertEqual(facets(.link), Facets(styleName: "Link", isLink: true))
    }

    func testEmbedIsALinkStyledEmbed() {
        XCTAssertEqual(facets(.embed), Facets(styleName: "Embed", isLink: true))
    }

    func testImageIsALinkStyledImage() {
        XCTAssertEqual(facets(.image), Facets(styleName: "Image", isLink: true))
    }

    /// Tags and citations are links (#1431): E-9 projects them as
    /// Hyperlinks on Windows because Ctrl+Enter activates them, and the mac
    /// editor now activates the same six kinds (⌘↩, the palette row, and
    /// VoiceOver's press). PR 17 mapped them as styles while nothing
    /// activated them.
    func testTagIsALinkStyledTag() {
        XCTAssertEqual(facets(.tag), Facets(styleName: "Tag", isLink: true))
    }

    func testCitationIsALinkStyledCitation() {
        XCTAssertEqual(facets(.citation), Facets(styleName: "Citation", isLink: true))
    }

    func testInlineCodeIsStyledCode() {
        XCTAssertEqual(facets(.inlineCode), Facets(styleName: "Code"))
    }

    func testCodeFenceIsStyledCode() {
        XCTAssertEqual(facets(.codeFence), Facets(styleName: "Code"))
    }

    func testCodeTokenIsStyledCode() {
        XCTAssertEqual(facets(.code(token: .keyword)), Facets(styleName: "Code"))
        XCTAssertEqual(facets(.code(token: .string)), Facets(styleName: "Code"))
    }

    func testBlockQuoteIsAQuote() {
        XCTAssertEqual(facets(.blockQuote), Facets(isQuote: true, styleName: "Quote"))
    }

    func testEmphasisIsItalic() {
        XCTAssertEqual(facets(.emphasis), Facets(isItalic: true))
    }

    func testStrongIsBold() {
        XCTAssertEqual(facets(.strong), Facets(isBold: true))
    }

    func testStrikethroughIsStruckThrough() {
        XCTAssertEqual(facets(.strikethrough), Facets(isStrikethrough: true))
    }

    func testCommentIsStyledComment() {
        XCTAssertEqual(facets(.comment), Facets(styleName: "Comment"))
    }

    func testFrontmatterIsStyledFrontmatter() {
        XCTAssertEqual(facets(.frontmatter), Facets(styleName: "Frontmatter"))
    }

    // MARK: - Lock-step with the frozen Windows table (contract 37 E-4)

    /// The style names are AX attribute values, not announcements: the mac
    /// twin of the Windows `StyleName` values, host-side by design (W7 spec
    /// R-1). They stay lock-step with the frozen E-4 table, read from the
    /// contracts document itself, so a change on either side is drift.
    func testStyleNamesMatchTheFrozenWindowsTable() throws {
        let contract = Self.repoRoot.appendingPathComponent("docs/plans/37_editor_peer_contracts.md")
        let lines = try String(contentsOf: contract, encoding: .utf8)
            .split(separator: "\n", omittingEmptySubsequences: false).map(String.init)
        guard let header = lines.firstIndex(of: "| Canonical kind | UIA attributes |") else {
            return XCTFail("contract 37 lost its E-4 table header")
        }
        let rows = lines[(header + 2)...].prefix { $0.hasPrefix("|") }.map { line in
            line.trimmingCharacters(in: CharacterSet(charactersIn: "| "))
                .components(separatedBy: " | ")
        }
        XCTAssertEqual(rows.count, 16, "E-4 is exhaustive over the sixteen canonical kinds")
        let samples: [String: EditorSpanKind] = [
            "Heading": .heading(level: 3), "Wikilink": .wikilink, "Link": .link, "Embed": .embed,
            "Image": .image, "Tag": .tag, "Citation": .citation, "InlineCode": .inlineCode,
            "CodeFence": .codeFence, "Code": .code(token: .identifier), "BlockQuote": .blockQuote,
            "Emphasis": .emphasis, "Strong": .strong, "Strikethrough": .strikethrough,
            "Comment": .comment, "Frontmatter": .frontmatter,
        ]
        for row in rows {
            XCTAssertEqual(row.count, 2, "malformed E-4 row \(row)")
            guard row.count == 2, let kind = samples[row[0]] else {
                XCTFail("E-4 names a kind the mac map does not know: \(row)")
                continue
            }
            let windows = row[1]
            let mac = facets(kind)
            let styleName = windows.range(of: "StyleName = ").map {
                String(windows[$0.upperBound...]).components(separatedBy: ";")[0]
                    .replacingOccurrences(of: "Heading N", with: "Heading 3")
            }
            XCTAssertEqual(mac.styleName, styleName, "\(row[0]): StyleName")
            XCTAssertEqual(
                mac.headingLevel != nil, windows.contains("StyleId = 70000 + level"), "\(row[0]): StyleId heading")
            XCTAssertEqual(mac.isQuote, windows.contains("StyleId = 70014"), "\(row[0]): StyleId quote")
            XCTAssertEqual(mac.isItalic, windows.contains("IsItalic = true"), "\(row[0]): IsItalic")
            XCTAssertEqual(mac.isBold, windows.contains("FontWeight = 700"), "\(row[0]): FontWeight")
            XCTAssertEqual(
                mac.isStrikethrough, windows.contains("StrikethroughStyle = Single"), "\(row[0]): Strikethrough")
        }
    }

    // MARK: - The plan (off-main, from the colorizer's spans)

    /// The most specific span wins per field (E-4), resolved in the plan: a
    /// wikilink inside a heading takes the style name of its own stretch and
    /// keeps the heading level. The text runs are disjoint and in document
    /// order, then the links, so stamping only ever extends a key in order.
    func testThePlanGivesEachStretchItsMostSpecificSpanInDocumentOrder() {
        let plan = EditorAccessibilityAttributes.plan([
            (NSRange(location: 3, length: 4), .wikilink),
            (NSRange(location: 0, length: 12), .heading(level: 2)),
        ])
        XCTAssertEqual(
            plan,
            [
                .init(range: NSRange(location: 0, length: 3), facet: .text(Facets(headingLevel: 2, styleName: "Heading 2"))),
                .init(range: NSRange(location: 3, length: 4), facet: .text(Facets(headingLevel: 2, styleName: "Wikilink"))),
                .init(range: NSRange(location: 7, length: 5), facet: .text(Facets(headingLevel: 2, styleName: "Heading 2"))),
                .init(range: NSRange(location: 3, length: 4), facet: .link(nested: false)),
            ])
    }

    /// Code tokens inside their fence add no run of their own (one "Code"
    /// stretch), touching runs of one meaning are one run, and plain text
    /// between spans carries none.
    func testThePlanMergesTouchingRunsOfOneMeaning() {
        let plan = EditorAccessibilityAttributes.plan([
            (NSRange(location: 0, length: 20), .codeFence),
            (NSRange(location: 4, length: 3), .code(token: .keyword)),
            (NSRange(location: 8, length: 2), .code(token: .number)),
            (NSRange(location: 30, length: 4), .emphasis),
            (NSRange(location: 34, length: 4), .emphasis),
        ])
        XCTAssertEqual(
            plan,
            [
                .init(range: NSRange(location: 0, length: 20), facet: .text(Facets(styleName: "Code"))),
                .init(range: NSRange(location: 30, length: 8), facet: .text(Facets(isItalic: true))),
            ])
    }

    /// An image inside a link is a nested link (its own run, so the outer
    /// link keeps its whole range); a link that crosses another is dropped,
    /// the earlier outer interval kept (E-9's crossing rule).
    func testALinkInsideALinkIsNestedAndACrossingLinkIsDropped() {
        let plan = EditorAccessibilityAttributes.plan([
            (NSRange(location: 0, length: 30), .link),
            (NSRange(location: 1, length: 10), .image),
            (NSRange(location: 25, length: 10), .wikilink),
        ])
        let links = plan.filter {
            if case .link = $0.facet { return true }
            return false
        }
        XCTAssertEqual(
            links,
            [
                .init(range: NSRange(location: 0, length: 30), facet: .link(nested: false)),
                .init(range: NSRange(location: 1, length: 10), facet: .link(nested: true)),
            ])
    }

    /// Cost tracks the window: a windowed pass plans only the spans of the
    /// window it repaints, all inside its applied range.
    func testAWindowedPassPlansOnlyTheWindowsSpans() {
        let doc = (0..<200).map { "Para \($0) with [[Link \($0)]] and `code \($0)`.\n\n" }.joined()
        let buffer = DocumentBuffer(text: doc)
        let dirty = NSRange(location: (doc as NSString).range(of: "Para 100 ").location, length: 1)
        let windowed = NoteEditorView.Coordinator.computeHighlight(buffer: buffer, snapshot: doc, dirty: dirty)
        let whole = NoteEditorView.Coordinator.computeHighlight(buffer: buffer, snapshot: doc, dirty: nil)
        XCTAssertFalse(windowed.isWholeDocument)
        XCTAssertFalse(windowed.semantics.isEmpty)
        for run in windowed.semantics {
            XCTAssertEqual(NSIntersectionRange(run.range, windowed.appliedUTF16), run.range)
        }
        XCTAssertLessThan(windowed.semantics.count * 20, whole.semantics.count)
    }

    // MARK: - What an assistive client reads

    /// The spec's buffer: a heading, a wikilink, a link, an embed and a
    /// quote. The AX string carries AXLink over exactly the three link spans
    /// (each value a link element named by its text) and the custom text
    /// attribute over exactly the heading, quote and link spans, computed
    /// independently from core's spans.
    func testTheAccessibilityStringCarriesSpanSemanticsOverExactlyTheSpanRanges() async throws {
        let text = Self.specBuffer
        let (coordinator, textView) = try await makeEditor(text)
        defer { withExtendedLifetime(coordinator) {} }
        let spans = EditorSpanMapping.utf16Spans(from: editorHighlightSpans(text: text), in: text)
        let ax = try XCTUnwrap(textView.accessibilityAttributedString(for: Self.whole(text)))

        let linkKinds: [EditorSpanKind] = [.wikilink, .link, .embed, .image]
        let expectedLinks = spans.filter { linkKinds.contains($0.kind) }.map(\.range)
        XCTAssertEqual(expectedLinks.count, 3, "fixture: one wikilink, one link, one embed")
        let links = Self.runs(ax, .accessibilityLink)
        XCTAssertEqual(links.map(\.range), expectedLinks)
        for (range, value) in links {
            let element = try XCTUnwrap(value as? EditorLinkAccessibilityElement)
            XCTAssertEqual(element.accessibilityRole(), .link)
            XCTAssertEqual(element.accessibilityLabel(), (text as NSString).substring(with: range))
            XCTAssertTrue(element.accessibilityParent() as AnyObject === textView)
        }

        let custom = Self.runs(ax, .accessibilityCustomText).map { ($0.range, $0.value as? [String]) }
        let expected: [(NSRange, [String])] = spans.compactMap { span in
            switch span.kind {
            case .heading(let level): return (span.range, ["Heading \(level)"])
            case .blockQuote: return (span.range, ["Quote"])
            case .wikilink: return (span.range, ["Wikilink"])
            case .link: return (span.range, ["Link"])
            case .embed: return (span.range, ["Embed"])
            default: return nil
            }
        }.sorted { $0.0.location < $1.0.location }
        XCTAssertEqual(custom.map(\.0), expected.map(\.0), "custom text runs cover exactly the span ranges")
        XCTAssertEqual(custom.map(\.1), expected.map(\.1))
        XCTAssertEqual(Self.runs(ax, .accessibilityStrikethrough).count, 0)
    }

    /// Issue #63's rule: the attributes are added, the native reading is
    /// kept — same text, same native attributes, over any range.
    func testTheAccessibilityStringKeepsTheNativeReading() async throws {
        let text = Self.specBuffer
        let (coordinator, textView) = try await makeEditor(text)
        defer { withExtendedLifetime(coordinator) {} }
        let ns = text as NSString
        for range in [Self.whole(text), ns.lineRange(for: NSRange(location: 0, length: 0)), ns.range(of: "[[Wiki")] {
            let plain = try XCTUnwrap(textView.nativeAccessibilityAttributedStringForTesting(for: range))
            let ax = try XCTUnwrap(textView.accessibilityAttributedString(for: range))
            XCTAssertEqual(ax.string, plain.string)
            plain.enumerateAttributes(in: NSRange(location: 0, length: plain.length)) { attributes, run, _ in
                for (key, value) in attributes {
                    let added = ax.attribute(key, at: run.location, effectiveRange: nil)
                    XCTAssertEqual(added as? NSObject, value as? NSObject, "native \(key.rawValue) kept at \(run)")
                }
            }
        }
    }

    /// The twin of Windows `EveryCanonicalKindHasItsContractAttributeThroughTheNativePeer`,
    /// over the same shared fixture (`editor_semantics.md`, all sixteen kinds).
    func testEveryCanonicalKindHasItsContractAttributeThroughTheTextView() async throws {
        let text = try Self.fixture()
        let (coordinator, textView) = try await makeEditor(text)
        defer { withExtendedLifetime(coordinator) {} }
        let spans = editorHighlightSpans(text: text)
        XCTAssertEqual(Set(spans.map { Self.kindName($0.kind) }).count, 16, "the fixture carries every kind")
        let ax = try XCTUnwrap(textView.accessibilityAttributedString(for: Self.whole(text)))
        let ns = text as NSString
        func at(_ marker: String) -> Int { ns.range(of: marker).location }
        func custom(_ marker: String) -> [String]? {
            ax.attribute(.accessibilityCustomText, at: at(marker), effectiveRange: nil) as? [String]
        }
        func isLink(_ marker: String) -> Bool {
            ax.attribute(.accessibilityLink, at: at(marker), effectiveRange: nil) is EditorLinkAccessibilityElement
        }
        XCTAssertEqual(custom("Heading one"), ["Heading 1"])
        XCTAssertEqual(custom("Heading two"), ["Heading 2"])
        XCTAssertEqual(custom("Quoted heading"), ["Heading 2"], "a heading inside a quote keeps its level")
        XCTAssertEqual(custom("Quoted text"), ["Quote"])
        for marker in [
            "[[Target]]", "![[Target]]", "#project", "[@smith2020]", "[Website]", "![Picture]",
            "[quoted link]",
        ] {
            XCTAssertTrue(isLink(marker), "\(marker) is a link")
        }
        XCTAssertEqual(custom("[[Target]]"), ["Wikilink"])
        XCTAssertEqual(custom("![[Target]]"), ["Embed"])
        XCTAssertEqual(custom("[Website]"), ["Link"])
        XCTAssertEqual(custom("Picture"), ["Image"], "an image is a link of its own kind")
        XCTAssertEqual(custom("[quoted link]"), ["Quote", "Link"], "a link in a quote keeps the quote")
        XCTAssertEqual(custom("#project"), ["Tag"])
        XCTAssertEqual(custom("[@smith2020]"), ["Citation"])
        XCTAssertEqual(custom("inline code"), ["Code"])
        XCTAssertEqual(custom("```rust"), ["Code"])
        XCTAssertEqual(custom("let answer"), ["Code"])
        XCTAssertEqual(custom("Comment text"), ["Comment"])
        XCTAssertEqual(custom("title:"), ["Frontmatter"])
        XCTAssertEqual(ax.attribute(.accessibilityStrikethrough, at: at("Strike"), effectiveRange: nil) as? Bool, true)
        if #available(macOS 26.0, *) {
            XCTAssertEqual(ax.attribute(.accessibilityFontItalicAttribute, at: at("Emphasis"), effectiveRange: nil) as? Bool, true)
            XCTAssertEqual(ax.attribute(.accessibilityFontBoldAttribute, at: at("Strong"), effectiveRange: nil) as? Bool, true)
        }
        XCTAssertNil(custom("Plain text"), "plain text carries nothing")
        XCTAssertFalse(isLink("Plain text"))
    }

    /// Windowed: after edits repainted by windowed passes, the AX string
    /// equals the one a fresh whole-document pass produces (the #379
    /// differential, applied to the semantics).
    func testWindowedPassesKeepTheAccessibilityStringEqualToAWholeDocumentPass() async throws {
        let text = Self.specBuffer
        let (coordinator, textView) = try await makeEditor(text)
        defer { withExtendedLifetime(coordinator) {} }
        let storage = try XCTUnwrap(textView.textStorage)
        let edits: [(String, Int, String)] = [
            ("A paragraph", 0, "Typed [[New]] "),
            ("Tail.", 0, "![[Fresh embed]] "),
            ("![[Embed Target]]", 1, ""),  // the embed becomes a wikilink
            ("## Heading two", 0, "#"),  // level 2 becomes level 3
        ]
        for (marker, length, replacement) in edits {
            let location = (storage.string as NSString).range(of: marker).location
            XCTAssertNotEqual(location, NSNotFound, marker)
            storage.replaceCharacters(in: NSRange(location: location, length: length), with: replacement)
            coordinator.scheduleHighlight(debounced: true)
            let applied = await coordinator.settleHighlight()
            XCTAssertTrue(applied, "windowed pass for \(marker)")
        }
        let final = storage.string
        let windowed = try Self.semantics(textView, final)
        coordinator.scheduleHighlight(debounced: false)
        let applied = await coordinator.settleHighlight()
        XCTAssertTrue(applied)
        XCTAssertEqual(windowed, try Self.semantics(textView, final))
        XCTAssertTrue(windowed.contains("AXLink \((final as NSString).range(of: "[[New]]")) [[New]]"), "\(windowed)")
        XCTAssertTrue(windowed.contains("AXCustom \((final as NSString).range(of: "[[Embed Target]]")) Wikilink"))
    }

    /// VoiceOver reads a word inside a link: the AXLink run covers the read
    /// range and names the whole link — the same element on every read.
    func testAPartialReadInsideALinkNamesTheWholeLink() async throws {
        let text = Self.specBuffer
        let (coordinator, textView) = try await makeEditor(text)
        defer { withExtendedLifetime(coordinator) {} }
        let ns = text as NSString
        let link = ns.range(of: "[[Wiki Target]]")
        let word = ns.range(of: "Target]]")
        let first = try XCTUnwrap(textView.accessibilityAttributedString(for: word))
        let runs = Self.runs(first, .accessibilityLink)
        XCTAssertEqual(runs.map(\.range), [NSRange(location: 0, length: word.length)])
        let element = try XCTUnwrap(runs.first?.value as? EditorLinkAccessibilityElement)
        XCTAssertEqual(element.range, link)
        XCTAssertEqual(element.accessibilityLabel(), "[[Wiki Target]]")
        let again = try XCTUnwrap(textView.accessibilityAttributedString(for: link))
        XCTAssertTrue(again.attribute(.accessibilityLink, at: 0, effectiveRange: nil) as AnyObject === element)
    }

    /// Between a keystroke and the pass it schedules, the semantics shift
    /// with the text (AppKit shifts the store's runs), and the typed text carries
    /// none until the pass repaints it.
    func testAKeystrokeShiftsTheSemanticsBeforeThePassRepaints() async throws {
        let text = Self.specBuffer
        let (coordinator, textView) = try await makeEditor(text)
        defer { withExtendedLifetime(coordinator) {} }
        let storage = try XCTUnwrap(textView.textStorage)
        let before = (text as NSString).range(of: "[[Wiki Target]]")
        storage.replaceCharacters(in: NSRange(location: before.location, length: 0), with: "abc ")
        let shifted = NSRange(location: before.location + 4, length: before.length)
        let ax = try XCTUnwrap(
            textView.accessibilityAttributedString(for: NSRange(location: 0, length: storage.length)))
        XCTAssertTrue(Self.runs(ax, .accessibilityLink).map(\.range).contains(shifted), "\(Self.runs(ax, .accessibilityLink))")
        XCTAssertNil(ax.attribute(.accessibilityLink, at: before.location, effectiveRange: nil), "typed text carries none")
        XCTAssertEqual(
            ax.attribute(.accessibilityCustomText, at: shifted.location, effectiveRange: nil) as? [String], ["Wikilink"])
    }

    /// Typing at either edge of the buffer keeps the store with the text: it grows
    /// with every edit and keeps every earlier run, shifted, so an edit at
    /// the end or the start of a note never strands or misplaces semantics.
    func testTypingAtEitherEdgeKeepsTheStoreWithTheText() async throws {
        let text = Self.specBuffer
        let (coordinator, textView) = try await makeEditor(text)
        defer { withExtendedLifetime(coordinator) {} }
        let storage = try XCTUnwrap(textView.textStorage)
        let store = coordinator.semanticStore
        let link = (text as NSString).range(of: "[[Wiki Target]]")
        storage.replaceCharacters(in: NSRange(location: storage.length, length: 0), with: " more")
        XCTAssertEqual(store.length, storage.length, "an append at the end is tracked")
        XCTAssertEqual(store.run(EditorAccessibilityAttributes.Key.link, at: link.location)?.range, link)
        storage.replaceCharacters(in: NSRange(location: 0, length: 0), with: "x")
        XCTAssertEqual(store.length, storage.length, "an insert at the start is tracked")
        XCTAssertEqual(
            store.run(EditorAccessibilityAttributes.Key.link, at: link.location + 1)?.range,
            NSRange(location: link.location + 1, length: link.length))
    }

    // MARK: - The Links rotor

    func testTheEditorOffersALinksRotor() async throws {
        let (coordinator, textView) = try await makeEditor(Self.specBuffer)
        defer { withExtendedLifetime(coordinator) {} }
        let rotors = textView.accessibilityCustomRotors()
        XCTAssertEqual(rotors.filter { $0.type == .link }.count, 1)
        XCTAssertNotNil(rotors.first { $0.type == .link }?.itemSearchDelegate)
    }

    /// VoiceOver's Links rotor (VO-U) and VO-Command-L walk the links in
    /// document order through the rotor's search: next from the top,
    /// previous from the end, and type-ahead filtering.
    func testTheLinksRotorWalksTheLinksInDocumentOrder() async throws {
        let text = try Self.fixture()
        let (coordinator, textView) = try await makeEditor(text)
        defer { withExtendedLifetime(coordinator) {} }
        let rotor = try XCTUnwrap(textView.accessibilityCustomRotors().first { $0.type == .link })
        let delegate = try XCTUnwrap(rotor.itemSearchDelegate)
        func search(
            _ direction: NSAccessibilityCustomRotor.SearchDirection,
            from current: NSAccessibilityCustomRotor.ItemResult?, filter: String = ""
        ) -> NSAccessibilityCustomRotor.ItemResult? {
            let parameters = NSAccessibilityCustomRotor.SearchParameters()
            parameters.searchDirection = direction
            parameters.currentItem = current
            parameters.filterString = filter
            return delegate.rotor(rotor, resultFor: parameters)
        }
        var labels: [String] = []
        var item = search(.next, from: nil)
        while let found = item, labels.count < 20 {
            XCTAssertTrue(found.targetElement as AnyObject === textView)
            XCTAssertEqual(found.customLabel, (text as NSString).substring(with: found.targetRange))
            labels.append(found.customLabel ?? "")
            item = search(.next, from: found)
        }
        XCTAssertEqual(
            labels,
            [
                "[[Target]]", "![[Target]]", "#project", "[@smith2020]", "[Website](https://example.org)",
                "![Picture](picture.png)", "[quoted link](https://example.org/quote)",
            ])
        XCTAssertEqual(search(.previous, from: nil)?.customLabel, "[quoted link](https://example.org/quote)")
        XCTAssertEqual(search(.next, from: nil, filter: "website")?.customLabel, "[Website](https://example.org)")
        XCTAssertNil(search(.next, from: nil, filter: "no such link"))
    }

    // MARK: - Helpers

    static let specBuffer = """
        ## Heading two

        A paragraph with [[Wiki Target]], a [link](https://example.com) and ![[Embed Target]].

        > A quoted line.

        Tail.
        """

    static let repoRoot = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()  // SlateMacTests
        .deletingLastPathComponent()  // Tests
        .deletingLastPathComponent()  // slate-mac
        .deletingLastPathComponent()  // apps
        .deletingLastPathComponent()

    static func fixture() throws -> String {
        try String(
            contentsOf: repoRoot.appendingPathComponent(
                "crates/slate-core/tests/fixtures/markdown/editor_semantics.md"),
            encoding: .utf8)
    }

    static func whole(_ text: String) -> NSRange { NSRange(location: 0, length: (text as NSString).length) }

    static func kindName(_ kind: EditorSpanKind) -> String {
        switch kind {
        case .heading: return "heading"
        case .code: return "code"
        default: return "\(kind)"
        }
    }

    static func runs(_ string: NSAttributedString, _ key: NSAttributedString.Key) -> [(range: NSRange, value: Any)] {
        var out: [(range: NSRange, value: Any)] = []
        string.enumerateAttribute(key, in: NSRange(location: 0, length: string.length)) { value, range, _ in
            if let value { out.append((range, value)) }
        }
        return out
    }

    /// The semantic runs of the whole AX string, element identity replaced
    /// by each link element's range and label (identity is per pass).
    static func semantics(_ textView: NSTextView, _ text: String) throws -> [String] {
        let ax = try XCTUnwrap(textView.accessibilityAttributedString(for: whole(text)))
        var out: [String] = []
        for (range, value) in runs(ax, .accessibilityLink) {
            let element = value as? EditorLinkAccessibilityElement
            out.append("AXLink \(range) \(element?.accessibilityLabel() ?? "?")")
        }
        for (range, value) in runs(ax, .accessibilityCustomText) {
            out.append("AXCustom \(range) \((value as? [String])?.joined(separator: ",") ?? "?")")
        }
        for (range, _) in runs(ax, .accessibilityStrikethrough) { out.append("AXStrikethrough \(range)") }
        return out
    }

    /// A live editor: the `NSTextView` subclass the app mounts, its
    /// coordinator attached, and one whole-document highlight settled.
    func makeEditor(_ text: String) async throws -> (NoteEditorView.Coordinator, SlateEditorTextView) {
        let textView = SlateEditorTextView(frame: NSRect(x: 0, y: 0, width: 640, height: 480))
        textView.isRichText = false
        textView.string = text
        let binding = Binding<String>(get: { text }, set: { _ in })
        let coordinator = NoteEditorView.Coordinator(text: binding, onSave: {}, previewEmbedAtCursor: nil)
        textView.coordinator = coordinator
        coordinator.attach(textView: textView)
        coordinator.scheduleHighlight(debounced: false)
        let applied = await coordinator.settleHighlight()
        XCTAssertTrue(applied, "the seed pass painted")
        return (coordinator, textView)
    }
}
