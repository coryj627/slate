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
        /// #849: UNRESOLVED outgoing wikilink `targetRaw` values for this
        /// note (from `appState.currentOutgoingLinks`, the same records
        /// `OutgoingLinksPanel.isUnresolved` reads). Threaded as a pure
        /// param into `ReadingInlineMapper` so dangling `[[links]]` render
        /// in `warningText` before activation.
        var unresolvedLinkTargets: Set<String> = []
        /// Codex rounds 2–3 (#849): the note's saved link records,
        /// kind-partitioned. `nil` = no classification available
        /// (test/legacy callers — runs style resolved). Non-nil, a
        /// target with NO same-grammar record styles unresolved,
        /// because that is exactly what activation announces for it.
        var linkRecordSets: ReadingLinkRouter.LinkRecordSets? = nil
        /// Mirrors `AppState.hasUnsavedChanges` — task toggles are disabled
        /// while true (same rule + explanation as `TasksPanel`: the toggle's
        /// post-save reload would overwrite the dirty buffer).
        var isDocumentDirty: Bool = false
        /// Exact path-capability reason when task mutation is unavailable for
        /// a quarantined note. Nil leaves the existing dirty-buffer rule.
        var taskMutationDisabledReason: String? = nil
        var onToggleTask: (TaskItem) -> Void = { _ in }
        /// U3-3 (#467): `TaskItem.line` is a whole-FILE 1-based line; the
        /// rendered `text` is the BODY, so row matching adds this delta
        /// (AppState.bodyLineOffset). 0 when the note has no frontmatter —
        /// and 0 was correct for the pre-flip whole-file text too.
        var taskLineOffset: Int = 0

        // MARK: Block-level embeds (#511)

        /// Resolved embeds for THIS note, keyed by cache-key form (the exact
        /// `AppState.currentNoteEmbedResolutions` dict). A block that is one
        /// `![[…]]` embed looks its target up here to render an `EmbedView`
        /// card; a missing key means "not resolved yet OR unresolvable in the
        /// live buffer" — the render state machine (see `embedBlock`) handles
        /// both from this one dict.
        var embedResolutions: [String: EmbedResolution] = [:]
        /// Request async resolution for a cache key the dict lacks. Wired to
        /// `AppState.requestReadingEmbedResolution`; the view calls it AT MOST
        /// once per key (a `@State` guard set), so a placeholder that stays
        /// unresolved can't loop the resolver. `async` so the view can await
        /// COMPLETION and only then declare the key resolved-empty — the
        /// pending placeholder holds for exactly the in-flight window rather
        /// than collapsing to the fallback one frame after the request fires.
        var onResolveEmbed: (String) async -> Void = { _ in }
        /// Jump-to-source for a block-level embed card. Wired to
        /// `AppState.openEmbedTarget` — the SAME routing the EmbedsPanel rows
        /// and the Cmd+E popover use.
        var onOpenEmbedSource: (String) -> Void = { _ in }
        /// Bases embeds (#706) render through the Bases handle API, not the
        /// note/image embed resolver. `thisPath` is the host note path.
        var baseEmbedSession: VaultSession?
        var baseEmbedThisPath: String?
        var onOpenBaseEmbedInTab: (BaseEmbedOpenDestination) -> Void = { _ in }
        var baseEmbedHandleProvider: @MainActor (BaseEmbedRequest, String?) -> BaseEmbedHandle =
            { request, thisPath in BaseEmbedHandle(request: request, thisPath: thisPath) }
        /// #871: a reading-mode Dataview → .base conversion writes through an
        /// NSSavePanel just like the editor path; report (url, existedBefore)
        /// so AppState can barrier the structural undo history for a newly
        /// created in-vault path.
        var onWroteBaseSaveDestination: (URL, Bool) -> Void = { _, _ in }

        init(
            mathBlocks: [MathBlock] = [],
            codeBlocks: [CodeBlock] = [],
            diagramBlocks: [DiagramBlock] = [],
            citations: [RenderedCitation] = [],
            tasks: [TaskItem] = [],
            unresolvedLinkTargets: Set<String> = [],
            linkRecordSets: ReadingLinkRouter.LinkRecordSets? = nil,
            isDocumentDirty: Bool = false,
            taskMutationDisabledReason: String? = nil,
            onToggleTask: @escaping (TaskItem) -> Void = { _ in },
            taskLineOffset: Int = 0,
            embedResolutions: [String: EmbedResolution] = [:],
            onResolveEmbed: @escaping (String) async -> Void = { _ in },
            onOpenEmbedSource: @escaping (String) -> Void = { _ in },
            baseEmbedSession: VaultSession? = nil,
            baseEmbedThisPath: String? = nil,
            onOpenBaseEmbedInTab: @escaping (BaseEmbedOpenDestination) -> Void = { _ in },
            baseEmbedHandleProvider: @escaping @MainActor (BaseEmbedRequest, String?) -> BaseEmbedHandle =
                { request, thisPath in BaseEmbedHandle(request: request, thisPath: thisPath) },
            onWroteBaseSaveDestination: @escaping (URL, Bool) -> Void = { _, _ in }
        ) {
            self.mathBlocks = mathBlocks
            self.codeBlocks = codeBlocks
            self.diagramBlocks = diagramBlocks
            self.citations = citations
            self.tasks = tasks
            self.unresolvedLinkTargets = unresolvedLinkTargets
            self.linkRecordSets = linkRecordSets
            self.isDocumentDirty = isDocumentDirty
            self.taskMutationDisabledReason = taskMutationDisabledReason
            self.onToggleTask = onToggleTask
            self.taskLineOffset = taskLineOffset
            self.embedResolutions = embedResolutions
            self.onResolveEmbed = onResolveEmbed
            self.onOpenEmbedSource = onOpenEmbedSource
            self.baseEmbedSession = baseEmbedSession
            self.baseEmbedThisPath = baseEmbedThisPath
            self.onOpenBaseEmbedInTab = onOpenBaseEmbedInTab
            self.baseEmbedHandleProvider = baseEmbedHandleProvider
            self.onWroteBaseSaveDestination = onWroteBaseSaveDestination
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
    /// #856: block index to restore on mount (the per-tab park in
    /// `WorkspaceState.readingScrollParks`). Nil = no park → mount at
    /// the top, exactly today's behavior. The restore is a plain state
    /// assignment — never wrapped in `withAnimation` — so it lands
    /// instantly (Reduce Motion honored by construction).
    let initialScrollBlockIndex: Int?
    /// #856: continuous topmost-visible-block report, fed by
    /// `scrollPosition(id:)`. The host parks it (plain dictionary
    /// write, no publish) for the next reading remount of this tab.
    let onScrollBlockChange: ((Int) -> Void)?

    /// Reference-typed memo so the (synchronous, pure) block parse survives
    /// SwiftUI re-initializations of this struct. `@State` keeps the box
    /// stable for the lifetime of the mounted view.
    @State private var parseCache = ReadingParseCache()

    /// Cache keys for which `onResolveEmbed` has already fired (#511). A
    /// block-level embed placeholder requests resolution exactly once per key;
    /// this guards the re-request loop a bare `.task`/`.onAppear` would create
    /// when the resolution legitimately never lands (unsaved buffer, broken
    /// target). Keys are never removed: a re-request would be redundant work,
    /// and the resolution — once it arrives — flows in through `context`.
    @State private var requestedEmbedKeys: Set<String> = []

    /// Cache keys whose `onResolveEmbed` call has RETURNED (#511). Split from
    /// `requestedEmbedKeys` so the state machine can tell "in flight" (show
    /// the placeholder) from "completed without landing" (fall back to the
    /// inline run). Marked in a `defer` so a task cancelled at unmount still
    /// records terminally — though @State is discarded with the view then, so
    /// the distinction only matters if SwiftUI ever cancels a mounted row's
    /// task, where a stuck spinner would be strictly worse than an early
    /// fallback.
    @State private var completedEmbedKeys: Set<String> = []

    /// #856: the block index `scrollPosition(id:)` is currently
    /// anchored on. Seeded from `initialScrollBlockIndex` on mount
    /// (restore), updated by the scroll view as the user scrolls,
    /// reported outward via `onScrollBlockChange`.
    @State private var scrolledBlockIndex: Int?

    init(
        text: String,
        pathLabel: String,
        isLoading: Bool = false,
        loadError: String? = nil,
        onRetry: @escaping () -> Void = {},
        onSwitchToEditing: @escaping () -> Void = {},
        router: ReadingLinkRouter = .inert,
        context: ReadingBlockContext = ReadingBlockContext(),
        initialScrollBlockIndex: Int? = nil,
        onScrollBlockChange: ((Int) -> Void)? = nil
    ) {
        self.text = text
        self.pathLabel = pathLabel
        self.isLoading = isLoading
        self.loadError = loadError
        self.onRetry = onRetry
        self.onSwitchToEditing = onSwitchToEditing
        self.router = router
        self.context = context
        self.initialScrollBlockIndex = initialScrollBlockIndex
        self.onScrollBlockChange = onScrollBlockChange
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
                ForEach(Array(parsed.blocks.enumerated()), id: \.offset) { index, block in
                    blockView(
                        block, index: index,
                        lineStarts: parsed.lineStarts,
                        tableCells: parsed.tableCells[index])
                }
            }
            // #856: the ForEach's positional ids (the block indices)
            // become scroll targets, so `scrollPosition(id:)` below
            // tracks the topmost visible block continuously.
            .scrollTargetLayout()
            .padding(Tokens.Spacing.lg)
            // Measure cap (WCAG 1.4.8 / typographic 45–90ch band): the
            // column tops out at `readingMeasure` and centers in wider
            // windows — prose no longer runs 150+ characters per line
            // full-width. Below the cap nothing changes (leading
            // alignment inside the column is preserved). Code/table
            // blocks already scroll horizontally within their own
            // containers, so they lose nothing.
            .frame(maxWidth: Tokens.Layout.readingMeasure, alignment: .leading)
            .frame(maxWidth: .infinity)
        }
        // #856: continuous topmost-block tracking + park reporting.
        // `.top` anchor = "the block at the top edge", matching the
        // caret-park mental model (where was I reading?).
        .scrollPosition(id: $scrolledBlockIndex, anchor: .top)
        .onChange(of: scrolledBlockIndex) { _, newValue in
            guard let newValue else { return }
            onScrollBlockChange?(newValue)
        }
        .onAppear {
            // Restore the parked offset on remount — WITHOUT animation
            // (plain assignment, never `withAnimation`): the toggle
            // must land instantly for everyone, which also honors
            // Reduce Motion for free. Clamped defensively: the park is
            // path-validated by the host, but a live-buffer edit can
            // still shrink the block count between parks.
            guard let initial = initialScrollBlockIndex, initial > 0 else { return }
            scrolledBlockIndex = min(initial, max(parsed.blocks.count - 1, 0))
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
    private func blockView(
        _ block: ReadingBlock, index: Int, lineStarts: [Int],
        tableCells: ReadingTableCells?
    ) -> some View {
        switch block.kind {
        case .heading(let level):
            headingView(block, level: level)
        case .paragraph:
            // A paragraph that IS one `![[…]]` embed (block-level) expands to
            // an EmbedView card; anything else — prose, or an embed WITH
            // surrounding text (mid-paragraph) — stays the inline text leaf,
            // where the embed run keeps today's link-run navigate behavior (a
            // SwiftUI `Text` can't host a card mid-run). Detection is the Rust
            // span authority, not a string check (see `blockEmbedTarget`).
            if let embedKey = ReadingInlineMapper.blockEmbedTarget(inSlice: block.source) {
                if let request = BaseEmbedRequest.wikilinkTarget(embedKey) {
                    baseEmbedBlock(request, identity: index)
                } else {
                    embedBlock(key: embedKey, fallbackSlice: block.source)
                }
            } else {
                inlineLeaf(block.source)
            }
        case .listItem(let depth, let ordered, let task):
            if let taskChar = task {
                taskRow(block, depth: depth, taskChar: taskChar, lineStarts: lineStarts)
            } else {
                listItemRow(block, depth: depth, ordered: ordered)
            }
        case .blockQuote(let depth):
            quoteRow(block, depth: depth)
        case .codeFence(let language, let interior):
            if let request = BaseEmbedRequest.codeFence(language: language, source: block.source) {
                baseEmbedBlock(request, identity: index)
            } else {
                // Existing view, reused: visual highlight + the "Code block,
                // <language>, N lines" preamble + copy affordance.
                CodeBlockView(
                    block: codeModel(
                        block, language: language, interior: interior,
                        lineStarts: lineStarts))
            }
        case .mathBlock:
            // Existing view, reused: MathCAT speech as the AX label.
            MathView(block: mathModel(block, lineStarts: lineStarts))
        case .diagram(let dialect, _):
            // The unmatched-diagram fallback renders the raw fenced `source`
            // (delimiters and all) as labeled monospace — it never derived an
            // interior, so the authoritative `interior` isn't needed here.
            diagramView(block, dialect: dialect)
        case .table:
            // Cells come from the Rust segmentation API (#510) — the honest
            // grid, no Swift-side pipe parser (no-second-classifier). On nil
            // (Rust didn't recognize the slice as a table), fall back to the
            // raw monospace source, labeled "Table." exactly as before.
            if let cells = tableCells {
                tableGrid(cells)
            } else {
                rawSourceBlock(block.source, axLabel: "Table.")
            }
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
        let mapped = ReadingInlineMapper.map(
            slice: slice, citations: context.citations,
            unresolvedTargets: context.unresolvedLinkTargets,
            recordSets: context.linkRecordSets)
        return Text(mapped.attributed)
            .font(font)
            .foregroundStyle(Tokens.ColorRole.textPrimary)
            .strikethrough(strikethrough, color: Tokens.ColorRole.textSecondary)
            .textSelection(.enabled)
            .frame(maxWidth: .infinity, alignment: .leading)
    }

    /// Block-level embed card (#511) + its render state machine. `key` is the
    /// cache-key form (`ReadingInlineMapper.blockEmbedTarget`, == the
    /// `AppState.embedTargetKey` the resolutions dict is keyed on).
    ///
    /// States, driven by the dict entry plus TWO per-key guard sets
    /// (`requestedEmbedKeys` = asked, `completedEmbedKeys` = the ask
    /// RETURNED — split so "in flight" and "came back empty" are distinct):
    ///
    ///  1. RESOLVED — the dict has an entry → `EmbedView` renders it. This is
    ///     the ONE path for BOTH a real resolution (full note / section /
    ///     block / image) AND `.unresolved` (broken target): EmbedView owns
    ///     the honest "Unresolved embed: …" render + AX for the latter, so a
    ///     resolved-but-broken target is never a dead block and never the
    ///     inline fallback.
    ///  2. PENDING — no entry, request not yet returned → placeholder row
    ///     ("Embed, loading"); the request fires exactly once
    ///     (`requestedEmbedKeys`) and the placeholder holds for the whole
    ///     in-flight window (gating the fallback on `requestedEmbedKeys`
    ///     alone would collapse to state 3 one frame after the request
    ///     fired, flashing the inline run while the resolver was still
    ///     running).
    ///  3. RESOLVED-EMPTY — no entry, and the request RETURNED without
    ///     landing this key (no session — the one path the resolver writes
    ///     nothing). Deterministic terminal fallback: render the INLINE
    ///     link-run, so activation still routes through the router's embed
    ///     branch (announces "unresolved" when the target can't open) —
    ///     never an infinite spinner.
    @ViewBuilder
    private func embedBlock(key: String, fallbackSlice: String) -> some View {
        if let resolution = context.embedResolutions[key] {
            EmbedView(
                resolution: resolution,
                jumpToSourceAction: { target in context.onOpenEmbedSource(target) },
                depth: 0
            )
            .frame(maxWidth: .infinity, alignment: .leading)
        } else if completedEmbedKeys.contains(key) {
            // Resolved-empty terminal: the request completed, the key never
            // landed — fall back to the inline run so the embed is still
            // reachable + activation announces unresolved. Never a dead block.
            inlineLeaf(fallbackSlice)
        } else {
            embedPlaceholder(key: key)
        }
    }

    /// Pending-resolution placeholder for a block-level embed. Requests the
    /// resolution exactly once (`requestedEmbedKeys` guard) and records the
    /// request's RETURN (`completedEmbedKeys`) so `embedBlock` only falls
    /// back once the resolver has actually had its say. Labeled per the
    /// house loading-row phrasing so VoiceOver announces the wait rather than
    /// landing on an unlabeled spinner.
    private func embedPlaceholder(key: String) -> some View {
        HStack(spacing: Tokens.Spacing.sm) {
            ProgressView()
                .controlSize(.small)
            Text("Loading embed…")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .padding(.vertical, Tokens.Spacing.xs)
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Embed, loading.")
        .task {
            // Request-once: the guard prevents a re-request loop when the
            // resolution legitimately never arrives (unsaved buffer / broken
            // target) — a struct re-init re-runs `.task`, but the @State
            // guard set survives it, so no fresh request spawns per render.
            guard !requestedEmbedKeys.contains(key) else { return }
            requestedEmbedKeys.insert(key)
            // Terminal even on cancellation: an early fallback beats a
            // spinner no resolve is coming for.
            defer { completedEmbedKeys.insert(key) }
            await context.onResolveEmbed(key)
        }
    }

    private func baseEmbedBlock(_ request: BaseEmbedRequest, identity: Int) -> some View {
        VisibilityGatedBaseEmbed(
            request: request,
            session: context.baseEmbedSession,
            thisPath: context.baseEmbedThisPath,
            sharedHandle: context.baseEmbedHandleProvider(request, context.baseEmbedThisPath),
            onOpenInTab: { destination in
                context.onOpenBaseEmbedInTab(destination)
            },
            onWroteSaveDestination: context.onWroteBaseSaveDestination)
            .id(
                BaseExactIdentity.key(
                    prefix: "reading-base-embed",
                    components: [
                        String(identity), request.cacheKey, context.baseEmbedThisPath,
                    ]))
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
            let dirtyReason =
                "Save the note first. Toggle is disabled while the editor has unsaved changes."
            let blockedReason = context.taskMutationDisabledReason
                ?? (context.isDocumentDirty ? dirtyReason : nil)
            Button {
                context.onToggleTask(item)
            } label: {
                (completed ? SlateSymbol.taskComplete : SlateSymbol.taskIncomplete)
                    .decorative
                    .imageScale(.large)
            }
            .buttonStyle(.plain)
            .disabled(blockedReason != nil)
            .accessibilityLabel(completed ? "Mark incomplete" : "Mark complete")
            .accessibilityHint(
                blockedReason ?? "Toggles the task between open and done."
            )
            .accessibilityIsSelected(completed)
            .help(
                blockedReason ?? (completed ? "Mark incomplete" : "Mark complete")
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

    /// One body row of a rendered table. `id` is the row's index (rows are
    /// value-identical when their cells repeat, so a positional id keeps
    /// `ForEach` stable); `cells` has the same width as the header — Rust
    /// normalizes ragged rows, so `cell(_:)` never indexes out of range.
    private struct TableRow: Identifiable {
        let id: Int
        let cells: [String]

        /// Belt-and-braces bounds guard: returns "" past the row's width even
        /// though Rust guarantees width == header count.
        func cell(_ column: Int) -> String {
            column >= 0 && column < cells.count ? cells[column] : ""
        }
    }

    /// Honest table render (#510): [`AccessibleDataGrid`] backed by the Rust
    /// cell segmentation — header cells carry `.isHeader`, each body cell
    /// announces "Header: value", and the summary is a focusable region. An
    /// empty header cell stays honest (no "Column N" fabrication); the grid's
    /// per-cell labels use it verbatim.
    private func tableGrid(_ cells: ReadingTableCells) -> some View {
        let columns = cells.header.enumerated().map { columnIndex, header in
            AccessibleDataGrid<TableRow>.Column(header) { row in
                row.cell(columnIndex)
            }
        }
        let rows = cells.rows.enumerated().map { rowIndex, cellValues in
            TableRow(id: rowIndex, cells: cellValues)
        }
        let columnCount = cells.header.count
        let summary =
            "Table: \(CountCopy.counted(rows.count, "row", "rows")), "
            + "\(CountCopy.counted(columnCount, "column", "columns"))."
        return AccessibleDataGrid(
            columns: columns,
            rows: rows,
            summary: summary,
            accessibilityLabel: "Table")
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
        _ block: ReadingBlock, language: String, interior: String, lineStarts: [Int]
    ) -> CodeBlock {
        if let matched = context.codeBlocks.first(where: {
            contains(block, byteOffset: $0.byteOffset)
        }) {
            // Matched: keep the pipeline's highlight tokens / semantic spans —
            // the authoritative interior is only the plain-source fallback.
            return matched
        }
        // Fallback: the authoritative code interior from the Rust parser (#869)
        // — fence delimiters excluded, indented blocks dedented, CommonMark
        // edge cases resolved — without highlight tokens. CodeBlockView still
        // renders monospace source with the correct spoken preamble.
        return CodeBlock(
            source: interior,
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
        /// Segmented cells for each `.table` block, keyed by its index in
        /// `blocks`. Computed EAGERLY here (once per parse), never in `body`
        /// — the "one parse per toggle" budget forbids per-render FFI. Tables
        /// are rare, so eager alongside the block walk is bounded and simplest.
        /// A missing key (Rust returned nil) means the renderer falls back to
        /// the raw-source block.
        var tableCells: [Int: ReadingTableCells]
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
        // Eager, once-per-parse table segmentation (#510): the FFI runs here,
        // not in `body`. Cells come from the SAME Rust parse the block walk
        // used (no second, Swift-side pipe parser — the no-second-classifier
        // invariant). A nil result leaves the key absent → raw-block fallback.
        var tableCells: [Int: ReadingTableCells] = [:]
        for (index, block) in blocks.enumerated() {
            guard case .table = block.kind else { continue }
            if let cells = readingTableCells(source: block.source) {
                tableCells[index] = cells
            }
        }
        let parsed = Parsed(
            blocks: blocks,
            lineStarts: ReadingBlockSource.lineStartOffsets(of: text),
            tableCells: tableCells)
        cachedText = text
        cached = parsed
        return parsed
    }
}
