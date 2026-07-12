// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Right-pane leaves surfacing the Milestone K content pipelines —
/// math, code, and Mermaid diagrams — for the currently-selected
/// note ([#410](https://github.com/coryj627/slate/issues/410)).
///
/// PR #257 landed the data path (`currentNoteMathBlocks` /
/// `currentNoteCodeBlocks` / `currentNoteDiagramBlocks` load,
/// refresh after saves, race-guarded) but deferred the user-facing
/// surface, so MathCAT speech, code preambles, and structured
/// diagram descriptions were unreachable in the app: VoiceOver read
/// raw `$$…$$` and raw fences in the note pane. These leaves wire
/// the three existing views (`MathView` / `CodeBlockView` /
/// `MermaidView` — previously instantiated only by Settings
/// previews) into the right-pane leaf host, which is the established
/// accessible pattern: the NSTextView editor stays untouched (true
/// inline rendering rides NSTextAttachment, deferred V1.x), and AT
/// users reach the accessible representations from the leaf rail.
///
/// Each panel follows the `EmbedsPanel` idiom, adapted for the leaf
/// host (U4-2, #471): a labeled leaf empty state when no note is
/// selected, and a distinct labeled empty state when the note has
/// neither blocks of that kind nor a load error (a leaf must never be
/// a blank rectangle — DoD §A, superseding the stack-era self-hiding).
/// When there IS something to show — a load in flight, a load error,
/// or one or more blocks — the leaf renders a non-collapsible header
/// row (count + heading trait) over a loading row / error row / one
/// row per block with a "Jump to source" affordance that scrolls the
/// editor to the block's line.
///
/// Deliberate decisions (red-team #410):
/// - Rows are an EAGER `VStack`, not `LazyVStack`: VoiceOver must be
///   able to enumerate every row in the AX tree; laziness creates
///   enumeration gaps for offscreen rows — the wrong trade for this
///   project. The cost is materializing every block view when a
///   panel expands; realistic notes stay well under the cliff, and
///   the panel ScrollView is height-capped.
/// - The Math panel lists INLINE math spans as well as display
///   blocks: inline `$x$` speech is exactly what a blind reader
///   cannot get from the raw buffer, so verbosity is accepted over
///   omission. Sub-grouping can land later if tester feedback asks.

// MARK: - Shared row chrome

/// Caption + jump-to-source footer under each block row. The jump
/// routes through `lineScrollRequest`, the same path outline-row
/// activation uses, so focus/scroll behavior stays consistent.
private struct BlockRowFooter: View {
    @EnvironmentObject private var appState: AppState
    let line: UInt32
    let kindLabel: String

    var body: some View {
        Button {
            appState.lineScrollRequest.send(Int(line))
        } label: {
            Text(verbatim: "Jump to source — line \(line)")
                .font(Tokens.Typography.caption)
        }
        .buttonStyle(.link)
        // WCAG 2.5.3 label-in-name: the visible text must be a
        // contiguous prefix of the accessible name so Voice Control
        // "click Jump to source" matches (red-team LOW-1).
        .accessibilityLabel("Jump to source — line \(line). \(kindLabel).")
    }
}

// MARK: - Math

struct MathBlocksPanel: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        // Red-team HIGH-1 + Codoki #428, carried into the leaf host: the
        // populated `LeafSection` (which owns the loading + error rows) must
        // render when a load FAILED with no stale blocks (otherwise the error
        // row is dead code and "pipeline crashed" is indistinguishable from
        // "no math here" for a blind user) AND while a load is in flight
        // (otherwise the loading row is unreachable on initial load). The
        // no-blocks empty state is reached only once a load has settled with
        // zero blocks and no error. Loads are local SQLite reads, so the
        // no-blocks flash is millisecond-scale.
        Group {
            if appState.selectedFilePath == nil {
                LeafEmptyState(message: "Select a note to see its math.")
            } else if appState.currentNoteMathBlocks.isEmpty
                && appState.mathBlocksLoadError == nil
                && !appState.isLoadingMathBlocks
            {
                LeafEmptyState(message: "This note has no math blocks.")
            } else {
                LeafSection { header } content: { content }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Math")
    }

    private var header: some View {
        let count = appState.currentNoteMathBlocks.count
        let suffix = count == 1 ? "entry" : "entries"
        return Text(verbatim: "Math, \(count) \(suffix)")
            .font(Tokens.Typography.sectionHeader)
            .accessibilityAddTraits(.isHeader)
    }

    @ViewBuilder
    private var content: some View {
        if appState.isLoadingMathBlocks {
            ContentBlocksLoadingRow(message: "Loading math blocks…")
        } else if let err = appState.mathBlocksLoadError {
            ContentBlocksErrorRow(message: "Could not load math blocks: \(err)")
        } else {
            VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                ForEach(
                    Array(appState.currentNoteMathBlocks.enumerated()), id: \.offset
                ) { _, block in
                    VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                        MathView(block: block)
                        BlockRowFooter(line: block.line, kindLabel: "Math block")
                    }
                    .padding(.vertical, Tokens.Spacing.xxs)
                }
            }
        }
    }
}

// MARK: - Code

struct CodeBlocksPanel: View {
    @EnvironmentObject private var appState: AppState

    /// The code pipeline ingests EVERY fence, including ```mermaid
    /// (the diagram pipeline consumes the same fences). Listing
    /// mermaid blocks in both panels would make VoiceOver users sit
    /// through the same content twice, so the Code panel shows only
    /// non-diagram fences. Static for the unit test.
    static func panelBlocks(_ blocks: [CodeBlock]) -> [CodeBlock] {
        blocks.filter { ($0.language ?? "").lowercased() != "mermaid" }
    }

    private var panelBlocks: [CodeBlock] {
        Self.panelBlocks(appState.currentNoteCodeBlocks)
    }

    var body: some View {
        Group {
            if appState.selectedFilePath == nil {
                LeafEmptyState(message: "Select a note to see its code blocks.")
            } else if panelBlocks.isEmpty
                && appState.codeBlocksLoadError == nil
                && !appState.isLoadingCodeBlocks
            {
                LeafEmptyState(message: "This note has no code blocks.")
            } else {
                LeafSection { header } content: { content }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Code")
    }

    private var header: some View {
        let count = panelBlocks.count
        let suffix = count == 1 ? "entry" : "entries"
        return Text(verbatim: "Code blocks, \(count) \(suffix)")
            .font(Tokens.Typography.sectionHeader)
            .accessibilityAddTraits(.isHeader)
    }

    @ViewBuilder
    private var content: some View {
        if appState.isLoadingCodeBlocks {
            ContentBlocksLoadingRow(message: "Loading code blocks…")
        } else if let err = appState.codeBlocksLoadError {
            ContentBlocksErrorRow(message: "Could not load code blocks: \(err)")
        } else {
            VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                ForEach(
                    Array(panelBlocks.enumerated()), id: \.offset
                ) { _, block in
                    VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                        // #848: the code-blocks panel rides the editing
                        // surface's zoom (reading-mode code blocks stay
                        // on the system Text Size — see CodeBlockView).
                        CodeBlockView(block: block, textScale: appState.editorTextScale)
                        BlockRowFooter(line: block.line, kindLabel: "Code block")
                    }
                    .padding(.vertical, Tokens.Spacing.xxs)
                }
            }
        }
    }
}

// MARK: - Diagrams

struct DiagramsPanel: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Group {
            if appState.selectedFilePath == nil {
                LeafEmptyState(message: "Select a note to see its diagrams.")
            } else if appState.currentNoteDiagramBlocks.isEmpty
                && appState.diagramBlocksLoadError == nil
                && !appState.isLoadingDiagramBlocks
            {
                LeafEmptyState(message: "This note has no diagrams.")
            } else {
                LeafSection { header } content: { content }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Diagrams")
    }

    private var header: some View {
        let count = appState.currentNoteDiagramBlocks.count
        let suffix = count == 1 ? "entry" : "entries"
        return Text(verbatim: "Diagrams, \(count) \(suffix)")
            .font(Tokens.Typography.sectionHeader)
            .accessibilityAddTraits(.isHeader)
    }

    @ViewBuilder
    private var content: some View {
        if appState.isLoadingDiagramBlocks {
            ContentBlocksLoadingRow(message: "Loading diagrams…")
        } else if let err = appState.diagramBlocksLoadError {
            ContentBlocksErrorRow(message: "Could not load diagrams: \(err)")
        } else {
            VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                ForEach(
                    Array(appState.currentNoteDiagramBlocks.enumerated()), id: \.offset
                ) { _, block in
                    VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                        MermaidView(block: block)
                        BlockRowFooter(line: block.line, kindLabel: "Diagram")
                    }
                    .padding(.vertical, Tokens.Spacing.xxs)
                }
            }
        }
    }
}

// MARK: - Shared state rows

private struct ContentBlocksLoadingRow: View {
    let message: String

    var body: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            ProgressView()
                .controlSize(.small)
            Text(verbatim: message)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .padding(.vertical, Tokens.Spacing.xs)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(message)
    }
}

private struct ContentBlocksErrorRow: View {
    let message: String

    var body: some View {
        HStack(alignment: .top, spacing: Tokens.Spacing.xs) {
            SlateSymbol.warning.decorative
                .foregroundStyle(Tokens.ColorRole.warningText)
            Text(verbatim: message)
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
        }
        .padding(.vertical, Tokens.Spacing.xs)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(message)
    }
}
