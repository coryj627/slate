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

    /// Interim text-card detail (t2 R14): read-only content panel,
    /// shared by outline and table activation. #368 replaces it with
    /// the real editing component.
    @State private var detail: (title: String, text: String)?

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
        // M3 (t0 §2): active mode is inspectable from the container's
        // AX value — never announcement-only (braille rule §3).
        .accessibilityValue(modeController.containerAXValue ?? "")
        // M5: one Esc press consumes one ladder rung (mode → filter →
        // surface). Unconsumed presses bubble to the workspace.
        .onKeyPress(.escape) {
            modeController.handleEscape() ? .handled : .ignored
        }
        // M4: leaving the canvas (tab switch/pane move) auto-cancels
        // any active mode — no mode survives without focus (WCAG 2.1.2).
        .onChange(of: appState.workspace.model.activeGroup.activeTabID) { _, newActive in
            if newActive != tabID {
                modeController.handleFocusDeparture()
            }
        }
        .overlay(alignment: .bottom) {
            if let readback = appState.canvasWhereAmIReadback {
                whereAmIPanel(readback)
            }
        }
        .sheet(isPresented: detailPresented) {
            detailPanel
        }
        .sheet(item: promptBinding) { prompt in
            CanvasPromptSheet(prompt: prompt)
        }
        .sheet(item: cardPickerBinding) { request in
            CanvasCardPicker(
                document: document,
                purpose: request.purpose,
                excluded: Set(appState.canvasMovingSet(in: document))
            ) { picked in
                appState.canvasHandleCardPick(request.purpose, target: picked)
            }
        }
    }

    private var cardPickerBinding: Binding<CanvasCardPickerRequest?> {
        Binding(
            get: { appState.canvasCardPicker },
            set: { appState.canvasCardPicker = $0 }
        )
    }

    private var promptBinding: Binding<CanvasPrompt?> {
        Binding(
            get: { appState.canvasPrompt },
            set: { appState.canvasPrompt = $0 }
        )
    }

    // MARK: Activation (one semantic across surfaces; #368 replaces)

    /// Per-kind activation (t2 §#362): markdown file → note tab; link →
    /// browser; text → interim read-only detail; media honestly deferred.
    private func activate(nodeId: String, kind: String, title: String) {
        document.lastActivatedNode = nodeId
        switch kind {
        case "text":
            guard let session = appState.currentSession, let handle = document.handle,
                let text = try? session.canvasNodeText(handle: handle, nodeId: nodeId)
            else { return }
            detail = (title: title, text: text ?? "")
        case "file":
            let target = document.target(of: nodeId)
            if target.lowercased().hasSuffix(".md") || target.lowercased().hasSuffix(".markdown") {
                appState.openFile(target, target: .currentTab)
            } else {
                appState.canvasAnnouncer.announce(
                    .status("Opening this file kind from the canvas arrives with canvas actions."))
            }
        case "image":
            appState.canvasAnnouncer.announce(
                .status("Opening media from the canvas arrives with canvas actions."))
        case "link":
            let target = document.target(of: nodeId)
            if let url = URL(string: target), appState.externalOpener(url) {
                appState.canvasAnnouncer.announce(.status("Opened \(title) in your browser."))
            } else {
                appState.canvasAnnouncer.announce(.error("The link could not be opened."))
            }
        default:
            break
        }
    }

    private var detailPresented: Binding<Bool> {
        Binding(
            get: { detail != nil },
            set: { if !$0 { detail = nil } }
        )
    }

    /// Interim read-only text panel (t2 R14). Esc closes; focus returns
    /// to the canvas content (WCAG 2.4.3).
    private var detailPanel: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text(detail?.title ?? "")
                .font(Tokens.Typography.body.weight(.semibold))
            ScrollView {
                Text(detail?.text ?? "")
                    .font(Tokens.Typography.body)
                    .textSelection(.enabled)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            HStack {
                Spacer()
                Button("Close") {
                    detail = nil
                    DispatchQueue.main.async { contentFocused = true }
                }
                .keyboardShortcut(.cancelAction)
            }
        }
        .padding(Tokens.Spacing.lg)
        .frame(minWidth: 360, minHeight: 240)
        .accessibilityElement(children: .contain)
        .accessibilityLabel(
            "Card text: \(detail?.title ?? ""). Read-only until canvas editing arrives.")
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

    private var modeController: CanvasModeController {
        appState.canvasModeController(for: document)
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
                    CanvasOutlineView(document: document, tabID: tabID) { row in
                        activate(nodeId: row.nodeId, kind: row.kind, title: row.title)
                    }
                    .accessibilityFocused($contentFocused)
                case .table:
                    CanvasTableView(document: document) { nodeId in
                        if let row = document.outline.first(where: { $0.nodeId == nodeId }) {
                            activate(nodeId: nodeId, kind: row.kind, title: row.title)
                        }
                    }
                    .accessibilityFocused($contentFocused)
                case .visual:
                    CanvasRendererView(document: document)
                        .accessibilityFocused($contentFocused)
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
