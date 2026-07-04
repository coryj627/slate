// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Canvas tab content (Milestone T, #369): hosts the surface switcher
/// and routes to the active sub-surface (outline / table / visual),
/// plus the empty, warning, and error states (t0 §5).
///
/// Landing is the **outline** — structured-first (t2 decision). The
/// full accessible outline (rotors, live regions) is #362; the table
/// is #363 on the AccessibleDataGrid v2 (#519); the visual renderer is
/// #367. Until each lands, this container shows an honest, accessible
/// interim for that surface — never a blank pane.
struct CanvasContainerView: View {
    @EnvironmentObject var appState: AppState
    @ObservedObject var document: CanvasDocument
    let tabID: TabID

    /// Focus lands on the canvas content when a canvas opens
    /// (WCAG 2.4.3 — the tab press deposits the user somewhere real).
    @AccessibilityFocusState private var contentFocused: Bool

    var body: some View {
        Group {
            switch document.state {
            case .loading:
                ProgressView("Opening \(document.displayName)…")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            case .failed(let message):
                stateMessage(message)
            case .degraded(let detail):
                degradedState(detail)
            case .ready:
                readyBody
            }
        }
        .onAppear {
            // Focus the canvas content once the surface exists. The
            // async hop lets SwiftUI mount the destination first.
            DispatchQueue.main.async { contentFocused = true }
        }
        .overlay(alignment: .bottom) {
            if let readback = appState.canvasWhereAmIReadback {
                whereAmIPanel(readback)
            }
        }
    }

    /// t0 §1.4: the transient, focusable Where-am-I panel — the
    /// pull-based counterpart to the announcement, so braille users
    /// read the same string at leisure.
    private func whereAmIPanel(_ text: String) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text("Where am I?")
                .font(Tokens.Typography.body.weight(.semibold))
            Text(text)
                .font(Tokens.Typography.body)
                .textSelection(.enabled)
            Button("Close") {
                appState.canvasWhereAmIReadback = nil
            }
            .keyboardShortcut(.cancelAction)
        }
        .padding(Tokens.Spacing.md)
        .background(
            RoundedRectangle(cornerRadius: Tokens.Radius.control)
                .fill(Tokens.ColorRole.surface)
                .shadow(radius: 4)
        )
        .padding(Tokens.Spacing.md)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Where am I? \(text)")
    }

    private var surface: CanvasSurface {
        appState.workspace.canvasSurface(for: tabID)
    }

    // MARK: Ready

    @ViewBuilder private var readyBody: some View {
        VStack(spacing: 0) {
            header
            if document.outline.isEmpty {
                emptyOnboarding
            } else {
                switch surface {
                case .outline:
                    CanvasOutlineView(document: document, tabID: tabID)
                        .accessibilityFocused($contentFocused)
                case .table: interimPlaceholder(
                    "Canvas table view is under construction.",
                    detail: "The sortable table surface arrives with the canvas table milestone work. Use Show Outline (Command Palette) meanwhile.")
                case .visual: interimPlaceholder(
                    "Canvas visual view is under construction.",
                    detail: "The visual renderer arrives with the canvas renderer milestone work. The outline and table carry everything it will show.")
                }
            }
        }
    }

    /// Surface switcher + t0 §5 warning banner. The switcher mirrors
    /// the palette commands (Show Outline / Table / Visual) — a chord
    /// or click is a convenience, the palette is always a path (R1).
    private var header: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
            Picker("Canvas view", selection: surfaceBinding) {
                ForEach(CanvasSurface.allCases, id: \.self) { s in
                    Text(s.title).tag(s)
                }
            }
            .pickerStyle(.segmented)
            .fixedSize()
            .accessibilityHint("Switches between the canvas outline, table, and visual views.")

            if document.preservedItemCount > 0 {
                // Pull-readable, not announcement-only (t0 §3): a
                // focusable banner; #362's outline footer adds the
                // per-item detail rows.
                Text(
                    "Canvas loaded. \(document.preservedItemCount) unsupported item\(document.preservedItemCount == 1 ? "" : "s") are preserved in the file but not shown."
                )
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .accessibilityAddTraits(.isStaticText)
            }
        }
        .padding(.horizontal, Tokens.Spacing.md)
        .padding(.vertical, Tokens.Spacing.xs)
    }

    private var surfaceBinding: Binding<CanvasSurface> {
        Binding(
            get: { appState.workspace.canvasSurface(for: tabID) },
            set: { appState.workspace.setCanvasSurface($0, for: tabID) }
        )
    }

    // MARK: Empty / error states

    /// Actionable onboarding (t2 §369.5 Wave-2 copy — #368 updates it
    /// to lead with New Card once that command ships).
    private var emptyOnboarding: some View {
        VStack(spacing: Tokens.Spacing.sm) {
            Spacer()
            Text("Canvas is empty.")
                .font(Tokens.Typography.body)
            Text("Open the Command Palette (⌘⇧P) — every canvas action is there.")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .multilineTextAlignment(.center)
            Spacer()
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(
            "Canvas is empty. Open the Command Palette, Command Shift P — every canvas action is there."
        )
        .accessibilityFocused($contentFocused)
    }

    /// t0 §5: a degraded load is read-only and says so — never a blank
    /// window, never a save path that could blank the file.
    private func degradedState(_ detail: String) -> some View {
        stateMessage(
            "\(document.displayName) could not be read as a canvas. The file is untouched and stays read-only until it is fixed. (\(detail))"
        )
    }

    private func stateMessage(_ message: String) -> some View {
        VStack(spacing: Tokens.Spacing.sm) {
            Spacer()
            Text(message)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .multilineTextAlignment(.center)
            Spacer()
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(message)
        .accessibilityFocused($contentFocused)
    }

    private func interimPlaceholder(_ message: String, detail: String) -> some View {
        VStack(spacing: Tokens.Spacing.sm) {
            Spacer()
            Text(message)
                .font(Tokens.Typography.body)
            Text(detail)
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .multilineTextAlignment(.center)
            Spacer()
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(message) \(detail)")
        .accessibilityFocused($contentFocused)
    }
}
