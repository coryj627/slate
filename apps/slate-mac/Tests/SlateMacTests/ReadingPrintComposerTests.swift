// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// File ▸ Print… (#869): the pure attributed-string composer, the command +
/// menu wiring, and the no-note enablement guard.
///
/// The composer (`ReadingPrintComposer.attributedString`) is a pure static
/// function, so these tests pin the run model directly — bold/larger headings,
/// monospaced code, secondary-ink quotes, list markers, task checkboxes, empty
/// notes, and math/diagram degradation — with no `NSApp` and no print panel.
final class ReadingPrintComposerTests: XCTestCase {

    private final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
        private(set) var posts: [(message: String, priority: AnnouncementPriority)] = []
        func post(_ message: String, priority: AnnouncementPriority) {
            posts.append((message, priority))
        }
    }

    // MARK: - Attributed-string helpers

    /// Attributes on the first run containing `substring` (nil if not found).
    private func attributes(
        of document: NSAttributedString, at substring: String
    ) -> [NSAttributedString.Key: Any]? {
        let ns = document.string as NSString
        let range = ns.range(of: substring)
        guard range.location != NSNotFound else { return nil }
        return document.attributes(at: range.location, effectiveRange: nil)
    }

    private func font(
        of document: NSAttributedString, at substring: String
    ) -> NSFont? {
        attributes(of: document, at: substring)?[.font] as? NSFont
    }

    /// sRGB red component (0…1) of a run's foreground color — a color-space-
    /// robust "is this black vs. gray vs. link-blue" probe.
    private func foregroundRed(
        of document: NSAttributedString, at substring: String
    ) -> CGFloat? {
        guard let color = attributes(of: document, at: substring)?[.foregroundColor]
            as? NSColor,
            let srgb = color.usingColorSpace(.sRGB)
        else { return nil }
        return srgb.redComponent
    }

    // MARK: - Command registration + chord parity

    @MainActor
    func testPrintNoteCommandIsRegisteredInFileWithCommandP() throws {
        let appState = AppState()
        let command = appState.commandRegistry.list().first {
            $0.id == SlateCommandID.printNote
        }
        let print = try XCTUnwrap(command, "printNote must be registered")
        XCTAssertEqual(print.label, "Print…")
        XCTAssertEqual(print.section, .file)
        XCTAssertEqual(
            print.hotkeyHint, "⌘P",
            "the palette row carries the same ⌘P chord as the File ▸ Print… item")
    }

    /// Exactly one registry claimant of ⌘P — no accidental double-binding, and
    /// ⇧⌘P (Command Palette) is untouched.
    @MainActor
    func testExactlyOneCommandClaimsCommandP() {
        let appState = AppState()
        let claimants = appState.commandRegistry.list().filter { $0.hotkeyHint == "⌘P" }
        XCTAssertEqual(claimants.map(\.id), [SlateCommandID.printNote])
    }

    // MARK: - Headings

    func testHeadingRunIsBoldAndLargerThanBody() {
        let document = ReadingPrintComposer.attributedString(
            text: "# Big Heading\n\nplain body text")
        let headingFont = font(of: document, at: "Big Heading")
        let bodyFont = font(of: document, at: "plain body text")
        XCTAssertNotNil(headingFont)
        XCTAssertNotNil(bodyFont)
        XCTAssertTrue(
            headingFont?.fontDescriptor.symbolicTraits.contains(.bold) ?? false,
            "an H1 must print bold")
        XCTAssertGreaterThan(
            headingFont?.pointSize ?? 0, bodyFont?.pointSize ?? .greatestFiniteMagnitude,
            "an H1 must print larger than body text")
        XCTAssertEqual(
            foregroundRed(of: document, at: "plain body text") ?? 1, 0, accuracy: 0.05,
            "body text prints in black ink (print-safe, not the dynamic textColor)")
    }

    // MARK: - Inline emphasis + code

    func testInlineBoldRunIsBoldAndPlainRunIsNot() {
        let document = ReadingPrintComposer.attributedString(
            text: "regularword and **strongword** together")
        XCTAssertEqual(
            font(of: document, at: "regularword")?.fontDescriptor.symbolicTraits
                .contains(.bold), false,
            "a non-emphasized run must not be bold")
        XCTAssertEqual(
            font(of: document, at: "strongword")?.fontDescriptor.symbolicTraits
                .contains(.bold), true,
            "a **strong** run must print bold")
    }

    func testInlineCodeRunIsMonospaced() {
        let document = ReadingPrintComposer.attributedString(
            text: "prose then `codespan` after")
        let codeFont = font(of: document, at: "codespan")
        XCTAssertEqual(
            codeFont?.fontName,
            NSFont.monospacedSystemFont(ofSize: codeFont?.pointSize ?? 10, weight: .regular)
                .fontName,
            "an inline `code` run must print in the monospaced system font")
    }

    // MARK: - Code fences

    func testCodeFenceRunIsMonospacedWithFill() {
        let fixture = """
            ```rust
            let answer = 42
            ```
            """
        let document = ReadingPrintComposer.attributedString(text: fixture)
        let attrs = attributes(of: document, at: "let answer = 42")
        let codeFont = attrs?[.font] as? NSFont
        XCTAssertNotNil(codeFont)
        XCTAssertEqual(
            codeFont?.fontName,
            NSFont.monospacedSystemFont(ofSize: codeFont?.pointSize ?? 10, weight: .regular)
                .fontName,
            "a fenced code block must print monospaced")
        XCTAssertNotNil(
            attrs?[.backgroundColor] as? NSColor,
            "fenced code prints on a subtle fill")
    }

    // MARK: - Block quotes

    func testBlockQuoteRunUsesSecondaryInk() {
        let document = ReadingPrintComposer.attributedString(text: "> a quoted line")
        let red = foregroundRed(of: document, at: "a quoted line")
        XCTAssertNotNil(red)
        // Secondary ink is a mid-gray (~0.30), distinctly not the black body
        // ink and not the link blue.
        XCTAssertGreaterThan(red ?? 0, 0.15, "a quote prints in the secondary gray")
        XCTAssertLessThan(red ?? 1, 0.5, "…but still dark enough to read")
    }

    // MARK: - Lists + tasks

    func testUnorderedAndOrderedMarkersPrint() {
        let document = ReadingPrintComposer.attributedString(
            text: "- first item\n1. second item")
        let string = document.string
        XCTAssertTrue(string.contains("• first item"), "unordered items print a bullet")
        XCTAssertTrue(
            string.contains("1. second item"),
            "ordered items print their authored ordinal verbatim")
    }

    func testTaskItemsPrintCheckboxGlyphsAndStrikeCompleted() {
        let document = ReadingPrintComposer.attributedString(
            text: "- [ ] open task\n- [x] done task")
        let string = document.string
        XCTAssertTrue(string.contains("☐ open task"), "an open task prints an empty box")
        XCTAssertTrue(string.contains("☑ done task"), "a done task prints a checked box")
        let doneAttrs = attributes(of: document, at: "done task")
        XCTAssertEqual(
            doneAttrs?[.strikethroughStyle] as? Int,
            NSUnderlineStyle.single.rawValue,
            "a completed task's text prints struck through")
    }

    // MARK: - Empty note

    func testEmptyNoteProducesEmptyDocument() {
        XCTAssertEqual(
            ReadingPrintComposer.attributedString(text: "").length, 0,
            "an empty note composes to an empty print document")
        XCTAssertEqual(
            ReadingPrintComposer.attributedString(text: "   \n\n  ").length, 0,
            "whitespace-only input yields no blocks")
    }

    // MARK: - Math degradation

    func testMathBlockDegradesToSourceMonospaced() {
        // No pipeline model → the raw `$$…$$` source prints as monospaced text.
        let document = ReadingPrintComposer.attributedString(text: "$$x^2 + y^2$$")
        XCTAssertTrue(
            document.string.contains("x^2 + y^2"),
            "a math block degrades to its printed source")
        let mathFont = font(of: document, at: "x^2 + y^2")
        XCTAssertEqual(
            mathFont?.fontName,
            NSFont.monospacedSystemFont(ofSize: mathFont?.pointSize ?? 10, weight: .regular)
                .fontName,
            "degraded math prints monospaced")
    }

    /// #869 Codex round 1: math degrades from the block's OWN `$$…$$` source
    /// (delimiters stripped) — NOT a byte-offset pipeline-model lookup, whose
    /// whole-file offsets mis-match our body-relative blocks on a frontmatter'd
    /// note. Frontmatter must NOT shift which math source prints.
    func testMathDegradesFromOwnSourceEvenWithFrontmatter() {
        let document = ReadingPrintComposer.attributedString(
            text: "---\ntitle: T\n---\n$$a+b$$\n\n$$c+d$$")
        XCTAssertTrue(document.string.contains("a+b"), "first math block prints its own source")
        XCTAssertTrue(document.string.contains("c+d"), "second prints ITS own source, not the first's")
        XCTAssertFalse(document.string.contains("$$"), "the `$$` delimiters are stripped")
    }

    // MARK: - Diagram degradation

    func testDiagramBlockDegradesToLabeledPlaceholder() {
        let fixture = """
            ```mermaid
            graph TD; A-->B;
            ```
            """
        let document = ReadingPrintComposer.attributedString(text: fixture)
        XCTAssertTrue(
            document.string.contains("[Diagram: mermaid]"),
            "an unrendered diagram prints a labeled placeholder")
        XCTAssertTrue(
            document.string.contains("graph TD"),
            "…alongside its source as a textual stand-in")
    }

    // MARK: - Every-kind smoke render

    /// Composing the every-kind reading fixture never crashes and produces a
    /// non-empty document (degradation paths included).
    func testEveryKindFixtureComposesNonEmpty() {
        let document = ReadingPrintComposer.attributedString(
            text: ReadingViewTests.everyKindFixture,
            citations: [ReadingViewTests.smithCitation])
        XCTAssertGreaterThan(document.length, 0)
        // The citation renders its visual text, not the raw `[@key]`.
        XCTAssertTrue(
            document.string.contains("(Smith, 2020)"),
            "a matched citation prints its rendered visual text")
    }

    // MARK: - No-note enablement guard

    /// With no note open, `printCurrentNote()` is never inert: it announces the
    /// nudge (mirroring the other never-silent guards) and does not crash —
    /// there is no key window under XCTest, so nothing is presented.
    @MainActor
    func testPrintWithNoNoteAnnouncesNudge() throws {
        let announcer = RecordingAnnouncer()
        let appState = AppState(announcer: announcer)
        XCTAssertNil(appState.loadedFilePath)
        appState.printCurrentNote()
        XCTAssertEqual(
            announcer.posts.map(\.message), ["Open a note to print."],
            "⌘P with no note open announces the nudge instead of a dead keystroke")
    }

    /// The same guard holds when the command is fired through the registry
    /// (the palette / chord path), not just the direct call.
    @MainActor
    func testInvokingPrintCommandWithNoNoteIsNoOpAndAnnounces() throws {
        let announcer = RecordingAnnouncer()
        let appState = AppState(announcer: announcer)
        try appState.commandRegistry.invokeById(id: SlateCommandID.printNote)
        XCTAssertEqual(announcer.posts.map(\.message), ["Open a note to print."])
    }

    /// #869 red-team + Codex round 1: with no document window (XCTest /
    /// inactive app) `printTargetWindow` is nil, so `printCurrentNote` no-ops
    /// and never announces a print that never opened a dialog.
    @MainActor
    func testPrintTargetWindowIsNilUnderXCTest() {
        XCTAssertNil(
            ReadingPrintComposer.printTargetWindow(),
            "no NSApp windows under XCTest → nil → the caller no-ops silently")
    }

    // MARK: - Authoritative code interior (#869, retires `fenceInterior`)

    // The code-block interior now comes authoritatively from the Rust parser
    // (`ReadingBlockKind.codeFence.interior`), so the composer prints exactly
    // pulldown-cmark's content — including the lines the retired Swift
    // `fenceInterior` heuristic silently dropped on these pathological inputs.

    /// An UNTERMINATED fence whose only closer candidate is a ```-line ending
    /// in a TAB: pulldown does NOT treat it as a closer, so that ``` line is
    /// authored CONTENT. The old heuristic stripped it as a phantom closer;
    /// the authoritative interior prints it.
    func testUnterminatedFenceWithTabCloserPrintsFullInterior() {
        let document = ReadingPrintComposer.attributedString(text: "```\ncode line\n```\t\n")
        XCTAssertTrue(document.string.contains("code line"))
        XCTAssertTrue(
            document.string.contains("```"),
            "the trailing ``` line is authored content, not a closer — it must print")
    }

    /// An INDENTED code block whose FIRST line is triple-backticks: pulldown
    /// reads the whole thing as indented code and dedents it, so the ``` line
    /// is content — NOT a fence opener. The old heuristic mis-read it as an
    /// opener and dropped the line; the authoritative interior prints it.
    func testIndentedCodeBlockStartingWithBackticksPrintsFullInterior() {
        let document = ReadingPrintComposer.attributedString(
            text: "    ```\n    code\n    more\n")
        XCTAssertTrue(
            document.string.contains("```"),
            "the leading ``` line is authored content, not a fence opener")
        XCTAssertTrue(document.string.contains("code"))
        XCTAssertTrue(document.string.contains("more"))
    }

    // MARK: - Deep nesting indent cap

    /// #869 Codex round 2: a deeply-nested block's indent is CAPPED so its text
    /// never lands past the printable page width (where it would clip).
    func testDeepNestingIndentIsCappedToStayOnPage() {
        let deep = String(repeating: ">", count: 30) + " deeply nested"
        let document = ReadingPrintComposer.attributedString(text: deep)
        let style = attributes(of: document, at: "deeply nested")?[.paragraphStyle]
            as? NSParagraphStyle
        XCTAssertNotNil(style, "the deep quote composed a run")
        XCTAssertLessThanOrEqual(
            style?.headIndent ?? .greatestFiniteMagnitude, 144.0,
            "indent is capped (≤ 8 levels × 18pt) so content stays on the page")
    }

    // MARK: - Menu wiring (source inspection)

    private static var projectRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .deletingLastPathComponent()  // apps
            .deletingLastPathComponent()  // <repo root>
    }

    /// The File ▸ Print… menu item must carry the ⌘P chord AND the published
    /// enablement gate contiguously — SwiftUI menu wiring isn't XCTest-
    /// introspectable, so we pin it by source inspection (the repo's
    /// `ByInspection` technique).
    ///
    /// Two complementary passes:
    ///  1. RAW source, scoped to the brace-balanced window from `Button("Print…")`
    ///     up to the next `Divider()`, asserts the exact string literals and the
    ///     contiguous modifier chain belong to THIS button.
    ///  2. COMMENT/STRING-STRIPPED whole-file source asserts the action +
    ///     modifiers survive as real code (a commented-out chain would vanish).
    func testFilePrintMenuItemWiringByInspection() throws {
        let appFile = Self.projectRoot
            .appendingPathComponent("apps/slate-mac/Sources/SlateMac")
            .appendingPathComponent("SlateMacApp.swift")
        let raw = try String(contentsOf: appFile, encoding: .utf8)

        // Pass 1: contiguous chain in the RAW source (string literals intact).
        let window = try Self.printButtonWindow(raw)
        for fragment in [
            "Button(\"Print…\")",
            "appState.printCurrentNote()",
            ".keyboardShortcut(\"p\", modifiers: [.command])",
            ".disabled(appState.loadedFilePath == nil)",
        ] {
            XCTAssertTrue(
                window.contains(fragment),
                "the File ▸ Print… item must declare `\(fragment)` in its contiguous chain")
        }

        // Pass 2: the code tokens survive comment/string stripping — proof the
        // chain is real code, not a commented-out or string-literal mention.
        XCTAssertFalse(
            raw.contains("\"\"\""),
            "SlateMacApp.swift gained a multiline string literal, which "
                + "SwiftSourceStripping does not model — upgrade the stripper "
                + "before trusting the stripped-source assert below.")
        let stripped = SwiftSourceStripping.strippingCommentsAndStrings(raw)
        XCTAssertTrue(
            stripped.contains("appState.printCurrentNote()"),
            "the print action must be live code, not inside a comment")
        XCTAssertTrue(
            stripped.contains(".disabled(appState.loadedFilePath == nil)"),
            "the published enablement gate must be live code")
    }

    /// Brace-balanced window from `Button("Print…")` up to the following
    /// `Divider()` — the region owning the Print item's contiguous modifier
    /// chain. Operates on the raw source so the `"Print…"` / `"p"` string
    /// literals survive for the exact-match asserts.
    private static func printButtonWindow(_ source: String) throws -> String {
        // #869 Codex round 1: Print MUST live in `CommandGroup(replacing:
        // .printItem)` — replacing SwiftUI's default macOS Print/Page Setup so
        // the File menu doesn't carry two Print items + two ⌘P claimants. The
        // window is that group, bounded by the next `CommandGroup(`.
        guard let start = source.range(of: "CommandGroup(replacing: .printItem)") else {
            throw DriftError.notFound(
                "CommandGroup(replacing: .printItem) not found — Print must REPLACE "
                    + "the default print group so ⌘P isn't double-claimed")
        }
        let end =
            source.range(of: "CommandGroup(", range: start.upperBound..<source.endIndex)?
            .lowerBound ?? source.endIndex
        return String(source[start.lowerBound..<end])
    }

    private enum DriftError: Error { case notFound(String) }
}
