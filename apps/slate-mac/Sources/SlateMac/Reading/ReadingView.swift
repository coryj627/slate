// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Rendered, navigable, read-only view of one note (U3-1, #465 — spec
/// `docs/plans/08_ui_parity/specs/u3_spec.md` §U3-1, gap G6).
///
/// **Mounting (the U3-2 seam).** This PR builds + tests the view but mounts
/// it NOWHERE in the shipping tree — the reading/editing toggle is U3-2
/// (#466), which renders `ReadingView` vs the editor from `NoteContentView`
/// per the tab's mode. Everything the view needs arrives through `init`, so
/// U3-2's mount is one expression:
///
/// ```swift
/// ReadingView(
///     text: appState.currentNoteText ?? "",
///     pathLabel: displayName,
///     isLoading: appState.isLoadingNote,
///     loadError: appState.noteLoadError,
///     onRetry: { appState.selectedFilePath = appState.selectedFilePath },
///     onSwitchToEditing: { /* U3-2 mode flip */ },
///     router: .live(appState: appState),
///     context: ReadingBlockContext(
///         mathBlocks: appState.currentNoteMathBlocks,
///         codeBlocks: appState.currentNoteCodeBlocks,
///         diagramBlocks: appState.currentNoteDiagramBlocks,
///         citations: appState.currentNoteCitations,
///         tasks: appState.currentNoteTasks,
///         isDocumentDirty: appState.hasUnsavedChanges,
///         onToggleTask: { appState.toggleCurrentTask($0) })
/// )
/// ```
///
/// **Live buffer.** Blocks come from `readingBlocksSource(source:)` — the
/// pure segmentation entry point — so the view renders whatever text it is
/// GIVEN (U3-2 feeds the live document; unsaved edits are visible). The
/// parse is memoized per text value (`ReadingParseCache`), so SwiftUI
/// re-initializing the struct does not re-parse; only a text change does —
/// the spec's "one parse per toggle" budget.
///
/// **Eager, flat, document-ordered.** The populated state is a `ScrollView {
/// VStack }` of one view per block — EAGER on purpose (ContentBlockPanels
/// discipline: VoiceOver must be able to enumerate every block; laziness
/// creates AX-tree gaps for offscreen rows). Because the VStack is document-
/// ordered and every heading carries `.isHeader` +
/// `.accessibilityHeading(.h(level))`, the VO heading rotor walks in document
/// order for free. Perf boundary: the eager tree materializes every block —
/// notes past `perfNoteBlockThreshold` (2,000) blocks log a perf note;
/// virtualization is the recorded follow-up, measured in U5-4 before
/// deciding.
///
/// **Continuous read.** Every text leaf is a distinct `Text` in the one flat
/// VStack, and `.textSelection(.enabled)` is applied per leaf `Text`, never
/// on a container (memory: container-scoped selection breaks VoiceOver
/// continuous read).
struct ReadingView: View {

    /// Specialized-block models + task machinery from the owning document.
    /// The reading view MATCHES its segmented blocks against these
    /// pipeline-extracted models (by byte-offset containment) rather than
    /// re-running any pipeline: MathCAT speech, syntax tokens, and rendered
    /// SVGs only exist in the Rust-produced models.
    struct ReadingBlockContext {
        var mathBlocks: [MathBlock] = []
        var codeBlocks: [CodeBlock] = []
        var diagramBlocks: [DiagramBlock] = []
        var citations: [RenderedCitation] = []
        var tasks: [TaskItem] = []
        /// Mirrors `AppState.hasUnsavedChanges` — task toggles are disabled
        /// while true (same rule + explanation as `TasksPanel`: the toggle's
        /// post-save reload would overwrite the dirty buffer).
        var isDocumentDirty: Bool = false
        var onToggleTask: (TaskItem) -> Void = { _ in }
        /// U3-3 (#467): `TaskItem.line` is a whole-FILE 1-based line; the
        /// rendered `text` is the BODY, so row matching adds this delta
        /// (AppState.bodyLineOffset). 0 when the note has no frontmatter —
        /// and 0 was correct for the pre-flip whole-file text too.
        var taskLineOffset: Int = 0

        init(
            mathBlocks: [MathBlock] = [],
            codeBlocks: [CodeBlock] = [],
            diagramBlocks: [DiagramBlock] = [],
            citations: [RenderedCitation] = [],
            tasks: [TaskItem] = [],
            isDocumentDirty: Bool = false,
            onToggleTask: @escaping (TaskItem) -> Void = { _ in },
            taskLineOffset: Int = 0
        ) {
            self.mathBlocks = mathBlocks
            self.codeBlocks = codeBlocks
            self.diagramBlocks = diagramBlocks
            self.citations = citations
            self.tasks = tasks
            self.isDocumentDirty = isDocumentDirty
            self.onToggleTask = onToggleTask
            self.taskLineOffset = taskLineOffset
        }
    }

    let text: String
    let pathLabel: String
    let isLoading: Bool
    let loadError: String?
    let onRetry: () -> Void
    let onSwitchToEditing: () -> Void
    let router: ReadingLinkRouter
    let context: ReadingBlockContext

    /// Reference-typed memo so the (synchronous, pure) block parse survives
    /// SwiftUI re-initializations of this struct. `@State` keeps the box
    /// stable for the lifetime of the mounted view.
    @State private var parseCache = ReadingParseCache()

    init(
        text: String,
        pathLabel: String,
        isLoading: Bool = false,
        loadError: String? = nil,
        onRetry: @escaping () -> Void = {},
        onSwitchToEditing: @escaping () -> Void = {},
        router: ReadingLinkRouter = .inert,
        context: ReadingBlockContext = ReadingBlockContext()
    ) {
        self.text = text
        self.pathLabel = pathLabel
        self.isLoading = isLoading
        self.loadError = loadError
        self.onRetry = onRetry
        self.onSwitchToEditing = onSwitchToEditing
        self.router = router
        self.context = context
    }

    var body: some View {
        Group {
            // Same precedence as NoteContentView: a load error outranks the
            // spinner; both outrank content decisions.
            if let error = loadError {
                errorState(error)
            } else if isLoading {
                loadingState
            } else {
                let parsed = parseCache.parsed(for: text)
                if parsed.blocks.isEmpty {
                    emptyState
                } else {
                    populated(parsed)
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: - States

    /// Spinner ROW (spec): reading mode keeps the top-leading text rhythm
    /// rather than centering a modal-feeling spinner.
    private var loadingState: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            ProgressView()
                .controlSize(.small)
            Text("Loading \(pathLabel)…")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Loading \(pathLabel).")
    }

    private func errorState(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text("Could not load note")
                .font(Tokens.Typography.sectionHeader)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .accessibilityAddTraits(.isHeader)
            // The specific failure, not a generic apology (DoD §F).
            Text(message)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Button("Retry") { onRetry() }
                .accessibilityHint("Tries loading the note again.")
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private var emptyState: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text("This note is empty.")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            // Stub by contract: U3-1 ships no mode toggle, so the closure is
            // a no-op default until U3-2 wires the real mode flip.
            Button("Switch to Editing") { onSwitchToEditing() }
                .accessibilityHint("Switches this tab to editing mode.")
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    // MARK: - Populated

    private func populated(_ parsed: ReadingParseCache.Parsed) -> some View {
        ScrollView {
            // EAGER VStack — see the type doc (VoiceOver enumerability over
            // laziness; >2k-block perf note logged by the cache;
            // virtualization is the recorded U5-4-gated follow-up).
            VStack(alignment: .leading, spacing: Tokens.Spacing.md) {
                ForEach(Array(parsed.blocks.enumerated()), id: \.offset) { _, block in
                    blockView(block, lineStarts: parsed.lineStarts)
                }
            }
            .padding(Tokens.Spacing.lg)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .background(Tokens.ColorRole.surface)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Reading view for \(pathLabel).")
        // Every inline link in every leaf routes through the one router —
        // slate schemes to their closures, http(s)/mailto to the system.
        .environment(\.openURL, OpenURLAction { url in router.route(url) })
    }

    // MARK: - Block dispatch

    @ViewBuilder
    private func blockView(_ block: ReadingBlock, lineStarts: [Int]) -> some View {
        switch block.kind {
        case .heading(let level):
            headingView(block, level: level)
        case .paragraph:
            inlineLeaf(block.source)
        case .listItem(let depth, let ordered, let task):
            if let taskChar = task {
                taskRow(block, depth: depth, taskChar: taskChar, lineStarts: lineStarts)
            } else {
                listItemRow(block, depth: depth, ordered: ordered)
            }
        case .blockQuote(let depth):
            quoteRow(block, depth: depth)
        case .codeFence(let language):
            // Existing view, reused: visual highlight + the "Code block,
            // <language>, N lines" preamble + copy affordance.
            CodeBlockView(block: codeModel(block, language: language, lineStarts: lineStarts))
        case .mathBlock:
            // Existing view, reused: MathCAT speech as the AX label.
            MathView(block: mathModel(block, lineStarts: lineStarts))
        case .diagram(let dialect):
            diagramView(block, dialect: dialect)
        case .table:
            // Raw-block fallback per spec. The evaluated alternative —
            // `AccessibleDataGrid` — requires structured columns/rows, so an
            // honest mapping needs a Rust-side cell segmentation API (a
            // second table parser in Swift would violate the no-second-
            // classifier rule). Recorded as the follow-up; the AX label
            // still announces "table".
            rawSourceBlock(block.source, axLabel: "Table.")
        case .thematicBreak:
            // Decorative: the visual rule carries no content (spec: hidden
            // from AX so VO continuous read flows past it).
            Divider()
                .accessibilityHidden(true)
        case .html:
            // Never interpreted — monospace source, labeled (spec).
            rawSourceBlock(block.source, axLabel: "HTML block.")
        }
    }

    // MARK: - Leaf renderers

    /// One inline-pipeline text leaf. `.textSelection(.enabled)` is applied
    /// HERE, at the leaf `Text`, and only here (plus the raw-source leaf) —
    /// container-scoped selection breaks VoiceOver continuous read.
    private func inlineLeaf(
        _ slice: String, font: Font = Tokens.Typography.body,
        strikethrough: Bool = false
    ) -> some View {
        let mapped = ReadingInlineMapper.map(slice: slice, citations: context.citations)
        return Text(mapped.attributed)
            .font(font)
            .foregroundStyle(Tokens.ColorRole.textPrimary)
            .strikethrough(strikethrough, color: Tokens.ColorRole.textSecondary)
            .textSelection(.enabled)
            .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func headingView(_ block: ReadingBlock, level: UInt8) -> some View {
        inlineLeaf(ReadingBlockSource.headingText(block.source), font: headingFont(level))
            .accessibilityAddTraits(.isHeader)
            .accessibilityHeading(axHeadingLevel(level))
    }

    /// Tokens type ramp by heading level. H1–H3 map to the named roles;
    /// H4–H6 differentiate by weight on the smaller Dynamic-Type roles
    /// (weight, never a fixed size — WCAG 1.4.4 scaling is preserved).
    private func headingFont(_ level: UInt8) -> Font {
        switch level {
        case 1: return Tokens.Typography.largeTitle
        case 2: return Tokens.Typography.title
        case 3: return Tokens.Typography.sectionHeader
        case 4: return Tokens.Typography.body.weight(.semibold)
        case 5: return Tokens.Typography.callout.weight(.semibold)
        default: return Tokens.Typography.caption.weight(.semibold)
        }
    }

    private func axHeadingLevel(_ level: UInt8) -> AccessibilityHeadingLevel {
        switch level {
        case 1: return .h1
        case 2: return .h2
        case 3: return .h3
        case 4: return .h4
        case 5: return .h5
        default: return .h6
        }
    }

    /// Non-task list item: authored ordered markers verbatim (the source
    /// carries the real ordinal — no renumbering), `•` for unordered.
    /// Depth is visual indent + the AX VALUE "list item, level N" (VoiceOver
    /// reads linearly; nesting is value-conveyed, not view-nested — spec).
    private func listItemRow(
        _ block: ReadingBlock, depth: UInt8, ordered: Bool
    ) -> some View {
        let parts = ReadingBlockSource.listItemParts(block.source)
        let marker = ordered ? (parts?.marker ?? "1.") : "•"
        let content = parts?.content ?? block.source
        return HStack(alignment: .firstTextBaseline, spacing: Tokens.Spacing.sm) {
            Text(verbatim: marker)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .accessibilityHidden(true)  // the value carries "list item"
            inlineLeaf(content)
        }
        .padding(.leading, CGFloat(depth) * Tokens.Spacing.lg)
        .accessibilityElement(children: .contain)
        .accessibilityValue("list item, level \(Int(depth) + 1)")
    }

    /// Task list item: checkbox row reusing TasksPanel's toggle semantics —
    /// `onToggleTask` routes to `AppState.toggleCurrentTask` (U3-2 wiring),
    /// disabled while the document is dirty with the panel's exact
    /// explanation. The `TaskItem` is matched by 1-based source line (both
    /// sides derive from the same whole-source text).
    private func taskRow(
        _ block: ReadingBlock, depth: UInt8, taskChar: String, lineStarts: [Int]
    ) -> some View {
        // stripTaskBox: the KIND said task (Rust classifier) — the checkbox
        // control replaces the authored `[c]` box.
        let parts = ReadingBlockSource.listItemParts(
            block.source, stripTaskBox: true)
        let line = ReadingBlockSource.lineNumber(
            forByteOffset: Int(block.byteStart), lineStarts: lineStarts)
        // Body line + delta == the record's whole-file line (U3-3).
        let item = context.tasks.first {
            Int($0.line) == line + context.taskLineOffset
        }
        let completed = item?.completed ?? (taskChar.lowercased() == "x")
        return HStack(alignment: .firstTextBaseline, spacing: Tokens.Spacing.sm) {
            taskCheckbox(item: item, completed: completed)
            inlineLeaf(parts?.content ?? block.source, strikethrough: completed)
        }
        .padding(.leading, CGFloat(depth) * Tokens.Spacing.lg)
        .accessibilityElement(children: .contain)
        .accessibilityValue("list item, level \(Int(depth) + 1)")
    }

    @ViewBuilder
    private func taskCheckbox(item: TaskItem?, completed: Bool) -> some View {
        if let item {
            // #158 rule, verbatim from TasksPanel: block the toggle while the
            // buffer is dirty — the toggle's post-save reload would overwrite
            // unsaved edits; disabling gives a visible "save first" signal
            // and routes VoiceOver away from a button that would no-op.
            let blocked = context.isDocumentDirty
            Button {
                context.onToggleTask(item)
            } label: {
                (completed ? SlateSymbol.taskComplete : SlateSymbol.taskIncomplete)
                    .decorative
                    .imageScale(.large)
            }
            .buttonStyle(.plain)
            .disabled(blocked)
            .accessibilityLabel(completed ? "Mark incomplete" : "Mark complete")
            .accessibilityHint(
                blocked
                    ? "Save the note first. Toggle is disabled while the editor has unsaved changes."
                    : "Toggles the task between open and done."
            )
            .accessibilityIsSelected(completed)
            .help(
                blocked
                    ? "Save the note first. Toggle is disabled while the editor has unsaved changes."
                    : (completed ? "Mark incomplete" : "Mark complete")
            )
        } else {
            // No TaskItem to route the toggle through (tasks not provided,
            // or the live buffer drifted from the saved task list): render
            // the STATE, not a dead button.
            (completed ? SlateSymbol.taskComplete : SlateSymbol.taskIncomplete)
                .image(label: completed ? "Completed task" : "Open task")
                .imageScale(.large)
        }
    }

    /// Quote leaf: accent bar + indent per depth; content has its `>`
    /// markers stripped. AX value parallels the list-item contract.
    private func quoteRow(_ block: ReadingBlock, depth: UInt8) -> some View {
        HStack(alignment: .top, spacing: Tokens.Spacing.sm) {
            // Decorative rule, redundant with the value + indent (1.4.11
            // exempt, same rationale as Tokens.ColorRole.separator).
            RoundedRectangle(cornerRadius: 1)
                .fill(Tokens.ColorRole.separator)
                .frame(width: 3)
                .accessibilityHidden(true)
            inlineLeaf(ReadingBlockSource.quoteContent(block.source, depth: depth))
        }
        .padding(.leading, CGFloat(max(Int(depth) - 1, 0)) * Tokens.Spacing.lg)
        .accessibilityElement(children: .contain)
        .accessibilityValue("block quote, level \(Int(depth))")
    }

    @ViewBuilder
    private func diagramView(_ block: ReadingBlock, dialect: String) -> some View {
        if let matched = context.diagramBlocks.first(where: {
            contains(block, byteOffset: $0.byteOffset)
        }) {
            // Existing view, reused: backend structured description as the
            // AX label, rendered SVG when available.
            MermaidView(block: matched)
        } else {
            // No pipeline model to hand MermaidView (fabricating a "render
            // failed" status would misinform AT users). Raw source, labeled.
            rawSourceBlock(block.source, axLabel: "Diagram, \(dialect).")
        }
    }

    /// Raw-block fallback (tables, HTML, unmatched diagrams): monospace
    /// source on the secondary surface (a gated contrast pairing), leaf-level
    /// selection, horizontal scroll for wide content with the CodeBlockView
    /// hint pattern so AT users know.
    private func rawSourceBlock(_ source: String, axLabel: String) -> some View {
        ScrollView(.horizontal, showsIndicators: true) {
            Text(verbatim: source)
                .font(Tokens.Typography.code)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .textSelection(.enabled)
                .padding(Tokens.Spacing.sm)
        }
        .background(
            RoundedRectangle(cornerRadius: 4)
                .fill(Tokens.ColorRole.surfaceSecondary)
        )
        .accessibilityElement(children: .contain)
        .accessibilityLabel(axLabel)
        .accessibilityHint("Wide content scrolls horizontally.")
    }

    // MARK: - Specialized-model matching

    /// A pipeline model belongs to a segmented block when its byte offset
    /// falls inside the block's whole-source range. Offsets agree because
    /// both sides index the same document text; when the live buffer has
    /// drifted from the saved state the pipelines saw, the match misses and
    /// the renderer degrades gracefully (fallback models below).
    private func contains(_ block: ReadingBlock, byteOffset: UInt32) -> Bool {
        let offset = UInt64(byteOffset)
        return offset >= block.byteStart && offset < block.byteEnd
    }

    private func codeModel(
        _ block: ReadingBlock, language: String, lineStarts: [Int]
    ) -> CodeBlock {
        if let matched = context.codeBlocks.first(where: {
            contains(block, byteOffset: $0.byteOffset)
        }) {
            return matched
        }
        // Fallback: interior without highlight tokens — CodeBlockView still
        // renders monospace source with the correct spoken preamble.
        return CodeBlock(
            source: ReadingBlockSource.fenceInterior(block.source),
            language: language.isEmpty ? nil : language,
            tokens: [],
            semanticSpans: [],
            line: UInt32(
                ReadingBlockSource.lineNumber(
                    forByteOffset: Int(block.byteStart), lineStarts: lineStarts)),
            byteOffset: UInt32(clamping: block.byteStart)
        )
    }

    private func mathModel(_ block: ReadingBlock, lineStarts: [Int]) -> MathBlock {
        if let matched = context.mathBlocks.first(where: {
            $0.displayStyle == .block && contains(block, byteOffset: $0.byteOffset)
        }) {
            return matched
        }
        // Fallback: LaTeXSwiftUI still renders the source visually; the
        // empty speech degrades to MathView's "Math expression." label.
        return MathBlock(
            source: block.source.trimmingCharacters(in: .whitespacesAndNewlines),
            displayStyle: .block,
            mathml: "",
            speech: "",
            braille: Data(),
            line: UInt32(
                ReadingBlockSource.lineNumber(
                    forByteOffset: Int(block.byteStart), lineStarts: lineStarts)),
            byteOffset: UInt32(clamping: block.byteStart)
        )
    }
}

// MARK: - Parse memo

/// Memoizes `readingBlocksSource(source:)` + the line-start index per text
/// value, so a SwiftUI struct re-init doesn't re-parse (only a text CHANGE
/// does — "one parse per toggle"). Reference type held in `@State`.
final class ReadingParseCache {
    struct Parsed {
        var blocks: [ReadingBlock]
        var lineStarts: [Int]
    }

    /// Documented perf boundary (spec §U3-1): the eager VStack materializes
    /// every block; beyond this count a perf note is logged and reading-view
    /// virtualization (the recorded follow-up) is measured in U5-4.
    static let perfNoteBlockThreshold = 2_000

    private var cachedText: String?
    private var cached: Parsed?

    func parsed(for text: String) -> Parsed {
        if let cached, cachedText == text {
            return cached
        }
        let blocks = readingBlocksSource(source: text)
        if blocks.count > Self.perfNoteBlockThreshold {
            NSLog(
                "ReadingView perf note: %d blocks exceeds the eager-render "
                    + "boundary (%d); virtualization follow-up is measured in U5-4.",
                blocks.count, Self.perfNoteBlockThreshold)
        }
        let parsed = Parsed(
            blocks: blocks,
            lineStarts: ReadingBlockSource.lineStartOffsets(of: text))
        cachedText = text
        cached = parsed
        return parsed
    }
}
