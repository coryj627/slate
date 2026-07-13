// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation

/// File ▸ Print… (#869): compose ONE `NSAttributedString` for a whole note's
/// rendered reading content and hand it to `NSPrintOperation`.
///
/// **Why not paginate the live SwiftUI `ReadingView`?** That view's math /
/// diagram / embed rows resolve asynchronously and lay out with fragile
/// measured heights — printing it directly would race those and clip mid-row.
/// Instead we re-segment the note through the SAME pure block source the
/// reading view uses (`readingBlocksSource` + `ReadingBlockSource` marker
/// strippers + `ReadingInlineMapper` for inline runs), then style each block
/// into a print-safe attributed string. `NSTextView` + `NSPrintOperation`
/// then own real multi-page pagination AND give selectable text in Preview /
/// the Save-as-PDF panel for free.
///
/// **Split for testability (issue's hard constraint #1).** The composition is
/// a pure static function (`attributedString(...)`) exercised directly by unit
/// tests with no `NSApp`; the `NSPrintOperation` presentation
/// (`runPrintOperation(...)`) is a thin, separate main-actor step that guards
/// `NSApp?.keyWindow` and no-ops under XCTest (no app instance), mirroring the
/// `postAccessibilityAnnouncement` / `responderChainUndoManager` discipline.
///
/// **Print-safe colors, not screen tokens (NSColor.textColor gotcha).** The
/// reading view styles with dynamic `Tokens.ColorRole`s that resolve to the
/// current appearance — white text in Dark Mode, which would print INVISIBLE
/// on white paper. So every run gets an explicit fixed color from
/// `PrintPalette` below rather than a dynamic system color.
enum ReadingPrintComposer {

    // MARK: - Print palette (fixed, appearance-independent)

    /// Fixed colors so the render is identical regardless of the app's Light /
    /// Dark appearance at print time (see the type doc's NSColor.textColor
    /// note). Deliberately NOT `Tokens.ColorRole` (those are dynamic).
    private enum PrintPalette {
        static let primaryText = NSColor.black
        /// Quotes / list markers — dark enough to stay legible in print.
        static let secondaryText = NSColor(white: 0.30, alpha: 1.0)
        /// Links keep an affordance that is NOT color-only (underline below),
        /// matching the reading view's WCAG 1.4.1 treatment.
        static let link = NSColor(srgbRed: 0.0, green: 0.20, blue: 0.55, alpha: 1.0)
        /// Subtle fill behind inline code + fenced code, the print echo of the
        /// reading view's `surfaceSecondary` code background.
        static let codeFill = NSColor(white: 0.94, alpha: 1.0)
    }

    // MARK: - Font sizes (points)

    /// Print body point size. A hair larger than screen body — print reads at
    /// arm's length and the paper has the resolution to spare.
    private static let bodyPointSize: CGFloat = 11.0
    private static let codePointSize: CGFloat = 10.0

    /// Heading ramp by level (H1…H6), a bold size ladder that flattens into
    /// bold-body at the deep levels — the print echo of `ReadingView`'s
    /// `headingFont(_:)` type ramp.
    private static func headingPointSize(_ level: UInt8) -> CGFloat {
        switch level {
        case 1: return 22.0
        case 2: return 18.0
        case 3: return 15.0
        case 4: return 13.0
        case 5: return 12.0
        default: return bodyPointSize
        }
    }

    // MARK: - Public composition (pure, testable)

    /// Build the whole-note print document. Pure and synchronous: everything
    /// arrives by parameter, so tests pin the run model without `NSApp` or a
    /// print panel.
    ///
    /// - Parameters:
    ///   - text: the note BODY (`AppState.currentNoteText`) — the loaded note,
    ///     printed in its rendered reading form regardless of the tab's current
    ///     editing/reading mode.
    ///   - citations: `AppState.currentNoteCitations`, so `[@key]` runs print
    ///     their rendered visual text (matched exactly as the reading view
    ///     matches them); an unmatched citation degrades to its raw source.
    ///   - mathBlocks / diagramBlocks: the pipeline-extracted models, matched
    ///     by byte-offset containment for graceful degradation (math prints its
    ///     source; a diagram prints its structured description when available,
    ///     else a labeled source placeholder). Never blocks on rendering SVGs.
    static func attributedString(
        text: String,
        citations: [RenderedCitation] = []
    ) -> NSAttributedString {
        let blocks = readingBlocksSource(source: text)
        let output = NSMutableAttributedString()

        for block in blocks {
            let fragment = fragment(for: block, citations: citations)
            // One trailing newline between blocks: the block's own paragraph
            // style carries the vertical rhythm (paragraphSpacing), so a single
            // separator is enough and empty fragments (thematic breaks are
            // self-contained) still advance the layout.
            output.append(fragment)
            if output.length > 0,
                !(output.string as NSString).hasSuffix("\n")
            {
                output.append(NSAttributedString(string: "\n"))
            }
        }
        return output
    }

    // MARK: - Per-block dispatch

    private static func fragment(
        for block: ReadingBlock, citations: [RenderedCitation]
    ) -> NSAttributedString {
        switch block.kind {
        case .heading(let level):
            let text = ReadingBlockSource.headingText(block.source)
            return styledInline(
                text,
                baseFont: boldSystemFont(headingPointSize(level)),
                citations: citations,
                paragraphStyle: headingParagraphStyle(level))

        case .paragraph:
            // A block-level `![[…]]` embed degrades to its inline run (the
            // display text as a link) — print never expands embed cards.
            return styledInline(
                block.source, baseFont: bodyFont(), citations: citations,
                paragraphStyle: bodyParagraphStyle())

        case .listItem(let depth, let ordered, let task):
            return listItemFragment(
                block, depth: depth, ordered: ordered, task: task,
                citations: citations)

        case .blockQuote(let depth):
            let content = ReadingBlockSource.quoteContent(block.source, depth: depth)
            // Quotes print in the secondary ink with a hanging indent — the
            // print echo of the reading view's accent-bar + indent.
            return styledInline(
                content, baseFont: bodyFont(),
                citations: citations, color: PrintPalette.secondaryText,
                paragraphStyle: indentedParagraphStyle(level: Int(max(depth, 1))))

        case .codeFence(let language, let interior):
            _ = language  // language label is not printed; the interior is
            // The authoritative code interior from the Rust parser (#869) —
            // fence delimiters excluded, indented blocks dedented, and the
            // pathological CommonMark edge cases (tab-trailing "closer",
            // unterminated fence, indented block whose first line is
            // triple-backticks) resolved by the parser rather than a Swift
            // heuristic that could silently drop authored lines.
            return codeFragment(interior)

        case .mathBlock:
            // Degrade: print the math SOURCE (can't typeset into a print text
            // view). Codex round 1: derive the interior from the block's OWN
            // `$$…$$` slice — NOT a pipeline MathBlock looked up by byte offset.
            // Those model offsets are WHOLE-FILE while our blocks are
            // body-relative (`text` is the frontmatter-stripped body), so a
            // note with frontmatter mis-matched them and could print one math
            // block's source in place of another's (content loss). The block's
            // own source is always the right stand-in.
            return codeFragment(strippedMathDelimiters(block.source))

        case .diagram(let dialect, let interior):
            return diagramFragment(dialect: dialect, interior: interior)

        case .table:
            // The pipe-table source is readable as monospaced text — faithful
            // and selectable, no Swift-side grid parse needed for print.
            return codeFragment(block.source)

        case .thematicBreak:
            return thematicBreakFragment()

        case .html:
            // Never interpreted — monospaced source, matching the reading view.
            return codeFragment(block.source)
        }
    }

    // MARK: - List items

    private static func listItemFragment(
        _ block: ReadingBlock, depth: UInt8, ordered: Bool, task: String?,
        citations: [RenderedCitation]
    ) -> NSAttributedString {
        let stripTask = task != nil
        let parts = ReadingBlockSource.listItemParts(
            block.source, stripTaskBox: stripTask)
        let content = parts?.content ?? block.source

        let marker: String
        if let taskChar = task {
            // Task box → a printed checkbox glyph (no live toggle on paper).
            marker = taskChar.lowercased() == "x" ? "☑ " : "☐ "
        } else if ordered {
            // Ordered markers print their authored ordinal verbatim (the source
            // carries the real number — no renumbering), like the reading view.
            marker = "\(parts?.marker ?? "1.") "
        } else {
            marker = "• "
        }

        let paragraph = indentedParagraphStyle(level: Int(depth) + 1)
        let fragment = NSMutableAttributedString()
        fragment.append(
            NSAttributedString(
                string: marker,
                attributes: [
                    .font: bodyFont(),
                    .foregroundColor: PrintPalette.secondaryText,
                    .paragraphStyle: paragraph,
                ]))
        fragment.append(
            styledInline(
                content, baseFont: bodyFont(), citations: citations,
                paragraphStyle: paragraph,
                strikethrough: task?.lowercased() == "x"))
        return fragment
    }

    // MARK: - Code / diagram / rule fragments

    /// Monospaced source with a subtle fill — shared by code fences, math
    /// degrade, tables, and HTML.
    private static func codeFragment(_ source: String) -> NSAttributedString {
        NSAttributedString(
            string: source,
            attributes: [
                .font: monospacedFont(codePointSize),
                .foregroundColor: PrintPalette.primaryText,
                .backgroundColor: PrintPalette.codeFill,
                .paragraphStyle: bodyParagraphStyle(),
            ])
    }

    private static func diagramFragment(
        dialect: String, interior: String
    ) -> NSAttributedString {
        // Degrade: a diagram can't render into a print text view, so print a
        // labeled placeholder + the raw diagram source. The `interior` is the
        // authoritative fence content from the Rust parser (#869) — no
        // Swift-side fence heuristic, no pipeline DiagramBlock byte-offset
        // lookup (whole-file offsets vs our body-relative blocks mis-match on a
        // frontmatter'd note and would print the wrong diagram's description).
        let source = interior.trimmingCharacters(in: .whitespacesAndNewlines)
        let body = source.isEmpty
            ? "[Diagram: \(dialect)]"
            : "[Diagram: \(dialect)]\n\(source)"
        return codeFragment(body)
    }

    /// Strip the surrounding `$$` display-math delimiters from a math block's
    /// own source (#869 Codex round 1 — the coordinate-safe replacement for the
    /// byte-offset model interior). Whitespace-tolerant; a block with no
    /// delimiters (already interior) passes through trimmed.
    private static func strippedMathDelimiters(_ source: String) -> String {
        var s = source.trimmingCharacters(in: .whitespacesAndNewlines)
        if s.hasPrefix("$$") { s = String(s.dropFirst(2)) }
        if s.hasSuffix("$$") { s = String(s.dropLast(2)) }
        return s.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    /// Thematic break: a printed rule of horizontal-bar glyphs (a `Divider()`
    /// has no attributed-text form) on its own line.
    private static func thematicBreakFragment() -> NSAttributedString {
        NSAttributedString(
            string: String(repeating: "\u{2014}", count: 24),
            attributes: [
                .font: bodyFont(),
                .foregroundColor: PrintPalette.secondaryText,
                .paragraphStyle: bodyParagraphStyle(),
            ])
    }

    // MARK: - Inline styling

    /// Style one source slice's inline content into print-ready runs.
    ///
    /// Fidelity comes from `ReadingInlineMapper.map` — the SAME pipeline the
    /// reading view uses — which turns wikilinks / embeds / tags / citations
    /// into their DISPLAY text (as links) and parses authored bold / italic /
    /// inline-code. We then walk its runs and translate each run's
    /// `inlinePresentationIntent` into real `NSFont` traits over `baseFont`,
    /// plus fixed print colors — so a bold run prints bold, a code run prints
    /// monospaced, and a link run prints underlined in the print link ink
    /// (activation is meaningless on paper, so the URL attribute is dropped and
    /// only the visual affordance survives).
    private static func styledInline(
        _ slice: String,
        baseFont: NSFont,
        citations: [RenderedCitation],
        color: NSColor = PrintPalette.primaryText,
        paragraphStyle: NSParagraphStyle,
        strikethrough: Bool = false
    ) -> NSAttributedString {
        let mapped = ReadingInlineMapper.map(slice: slice, citations: citations)
        let result = NSMutableAttributedString()

        for run in mapped.attributed.runs {
            let text = String(mapped.attributed[run.range].characters)
            guard !text.isEmpty else { continue }

            let intent = run.inlinePresentationIntent
            let isCode = intent?.contains(.code) ?? false
            var font = isCode ? monospacedFont(baseFont.pointSize) : baseFont
            var traits: NSFontDescriptor.SymbolicTraits = []
            if intent?.contains(.stronglyEmphasized) == true { traits.insert(.bold) }
            if intent?.contains(.emphasized) == true { traits.insert(.italic) }
            if !traits.isEmpty { font = applyingTraits(traits, to: font) }

            var attributes: [NSAttributedString.Key: Any] = [
                .font: font,
                .paragraphStyle: paragraphStyle,
            ]
            let intentStrike = intent?.contains(.strikethrough) ?? false
            if strikethrough || intentStrike {
                attributes[.strikethroughStyle] = NSUnderlineStyle.single.rawValue
            }
            if run.link != nil {
                // Underline + link ink, but NO `.link` URL: a printed page
                // can't be clicked, and slate:// scheme URLs would be dead.
                attributes[.foregroundColor] = PrintPalette.link
                attributes[.underlineStyle] = NSUnderlineStyle.single.rawValue
            } else {
                attributes[.foregroundColor] = color
                if isCode { attributes[.backgroundColor] = PrintPalette.codeFill }
            }
            result.append(NSAttributedString(string: text, attributes: attributes))
        }
        return result
    }

    // MARK: - Fonts

    private static func bodyFont() -> NSFont {
        NSFont.systemFont(ofSize: bodyPointSize)
    }

    private static func boldSystemFont(_ size: CGFloat) -> NSFont {
        NSFont.boldSystemFont(ofSize: size)
    }

    private static func monospacedFont(_ size: CGFloat) -> NSFont {
        NSFont.monospacedSystemFont(ofSize: size, weight: .regular)
    }

    /// Apply bold / italic symbolic traits to `font`, degrading to the
    /// original font if the descriptor can't produce the trait combination.
    private static func applyingTraits(
        _ traits: NSFontDescriptor.SymbolicTraits, to font: NSFont
    ) -> NSFont {
        var descriptorTraits = font.fontDescriptor.symbolicTraits
        descriptorTraits.formUnion(traits)
        let descriptor = font.fontDescriptor.withSymbolicTraits(descriptorTraits)
        return NSFont(descriptor: descriptor, size: font.pointSize) ?? font
    }

    // MARK: - Paragraph styles

    private static func bodyParagraphStyle() -> NSParagraphStyle {
        let style = NSMutableParagraphStyle()
        style.paragraphSpacing = 6.0
        style.lineSpacing = 1.0
        return style
    }

    private static func headingParagraphStyle(_ level: UInt8) -> NSParagraphStyle {
        let style = NSMutableParagraphStyle()
        // More air above a heading than below (the "belongs to what follows"
        // rhythm), scaled a little by prominence.
        style.paragraphSpacingBefore = level <= 2 ? 12.0 : 8.0
        style.paragraphSpacing = 4.0
        return style
    }

    /// Hanging indent by nesting `level` (1-based) for list items and quotes.
    private static func indentedParagraphStyle(level: Int) -> NSParagraphStyle {
        let style = NSMutableParagraphStyle()
        // #869 Codex round 2: CAP the indent. An unbounded `18 × level` sent a
        // deeply-nested block (e.g. a depth-35 quote/list) past the printable
        // width — a Letter page is ~612pt before margins — pushing its text
        // outside the container, where it clips from the print / PDF output.
        // 8 levels (144pt) is already very deep; beyond that the indent
        // plateaus so authored content always stays on the page.
        let indent = CGFloat(min(max(level, 1), 8)) * 18.0
        style.firstLineHeadIndent = indent
        style.headIndent = indent
        style.paragraphSpacing = 3.0
        return style
    }

    // MARK: - Byte-offset matching

    /// A pipeline model belongs to a block when its byte offset falls inside
    /// the block's whole-source range (same containment the reading view uses).

    // MARK: - Presentation (thin, main-actor, guarded)

    /// Run the print panel for `document` against the key window. Separate from
    /// the pure builder above so the composition is unit-testable without ever
    /// presenting a panel.
    ///
    /// The stable DOCUMENT window to sheet the print panel onto — NOT a
    /// transient sheet (e.g. the command palette, which dismisses right after
    /// invoking a command) that would orphan the print sheet (#869 Codex round
    /// 1). Climbs out of any sheet chain via `sheetParent` to the owning
    /// window, falling back to `mainWindow`. nil under XCTest (no `NSApp` /
    /// windows) — the same safe-no-op discipline as `responderChainUndoManager`.
    @MainActor
    static func printTargetWindow() -> NSWindow? {
        guard let app = NSApp else { return nil }
        var window = app.keyWindow
        while let parent = window?.sheetParent { window = parent }
        return window ?? app.mainWindow
    }

    /// Present the print panel for `document` as a window-modal sheet on
    /// `window`. Callers defer this a runloop turn (via `DispatchQueue.main`)
    /// so any presenting sheet has dismissed first.
    @MainActor
    static func present(_ document: NSAttributedString, jobName: String, on window: NSWindow) {
        let printInfo = NSPrintInfo.shared.copy() as! NSPrintInfo
        // A fresh NSPrintInfo already defaults to the `.spool` (print)
        // disposition; the panel offers Save-as-PDF from there.
        printInfo.horizontalPagination = .fit
        printInfo.verticalPagination = .automatic
        printInfo.isHorizontallyCentered = false
        printInfo.isVerticallyCentered = false

        let contentWidth =
            printInfo.paperSize.width - printInfo.leftMargin - printInfo.rightMargin
        let contentHeight =
            printInfo.paperSize.height - printInfo.topMargin - printInfo.bottomMargin

        // A text view sized to the printable content WIDTH, with an unbounded
        // container HEIGHT so the layout manager lays out the ENTIRE document
        // (not just one page's worth); `NSPrintOperation`'s automatic vertical
        // pagination then slices that full height across pages.
        let textView = NSTextView(
            frame: NSRect(x: 0, y: 0, width: contentWidth, height: contentHeight))
        textView.minSize = NSSize(width: contentWidth, height: 0)
        textView.maxSize = NSSize(
            width: contentWidth, height: CGFloat.greatestFiniteMagnitude)
        textView.isVerticallyResizable = true
        textView.isHorizontallyResizable = false
        textView.autoresizingMask = [.width]
        textView.textContainerInset = .zero
        textView.textContainer?.containerSize = NSSize(
            width: contentWidth, height: CGFloat.greatestFiniteMagnitude)
        textView.textContainer?.widthTracksTextView = true
        textView.textContainer?.lineFragmentPadding = 0
        // Print on white paper regardless of the app appearance, so fixed-color
        // text (see PrintPalette) always lands on a light background.
        textView.drawsBackground = true
        textView.backgroundColor = .white
        textView.textStorage?.setAttributedString(document)
        // Force a full layout pass so `sizeToFit` grows the view to the whole
        // document height before the print operation measures it.
        if let container = textView.textContainer {
            textView.layoutManager?.ensureLayout(for: container)
        }
        textView.sizeToFit()

        let operation = NSPrintOperation(view: textView, printInfo: printInfo)
        operation.jobTitle = jobName
        operation.showsPrintPanel = true
        operation.showsProgressPanel = true
        // Sheet-modal: presents the panel and returns immediately. We report
        // only that the DIALOG opened (not that a print completed) — the sheet
        // owns the subsequent print / Save-as-PDF / Cancel interaction and its
        // own feedback, so the caller must NOT claim the document printed.
        operation.runModal(
            for: window, delegate: nil, didRun: nil, contextInfo: nil)
    }
}
