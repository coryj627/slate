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

    /// #373: keyboard focus for the in-canvas filter field (⌘F).
    @FocusState private var filterFocused: Bool


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
            if modeController.handleEscape() { return .handled }
            if document.filterActive || filterFocused {
                appState.canvasClearFilter()
                filterFocused = false
                return .handled
            }
            return .ignored
        }
        .onChange(of: appState.canvasFilterFocusToken) { _, _ in
            filterFocused = true
        }
        .onChange(of: document.filterText) { _, _ in
            appState.canvasAnnounceFilterCount(doc: document)
        }
        // M2: Return commits the active spatial mode (#521).
        .onKeyPress(.return) {
            guard modeController.active != nil else { return .ignored }
            return modeController.commit() ? .handled : .ignored
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
        .sheet(item: cardEditorBinding) { request in
            CanvasCardEditorSheet(request: request)
                .onDisappear {
                    // Focus returns to the canvas content (t0 M8 /
                    // WCAG 2.4.3) — the card row keeps selection.
                    DispatchQueue.main.async { contentFocused = true }
                }
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

    private var cardEditorBinding: Binding<CanvasCardEditorRequest?> {
        Binding(
            get: { appState.canvasCardEditor },
            set: { appState.canvasCardEditor = $0 }
        )
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

    // MARK: Activation (one semantic across surfaces, t2 §#362)

    /// Per-kind activation: text → the #368 card editor; markdown file
    /// → note tab; media file → default app; link → browser.
    private func activate(nodeId: String, kind: String, title: String) {
        document.lastActivatedNode = nodeId
        switch kind {
        case "text":
            appState.canvasEditCard(nodeId: nodeId)
        case "file", "image":
            let target = document.target(of: nodeId)
            if target.lowercased().hasSuffix(".md") || target.lowercased().hasSuffix(".markdown") {
                // #525: `#heading` subpath cards open to the anchor.
                let subpath = document.scene.nodes
                    .first { $0.nodeId == nodeId }?.subpath?
                    .trimmingCharacters(in: CharacterSet(charactersIn: "#"))
                if let subpath, !subpath.isEmpty {
                    appState.canvasOpenFileAtHeading(path: target, heading: subpath)
                } else {
                    appState.openFile(target, target: .currentTab)
                }
            } else if let vault = appState.currentVaultURL,
                FileManager.default.fileExists(
                    atPath: vault.appendingPathComponent(target).path),
                appState.externalOpener(vault.appendingPathComponent(target))
            {
                appState.canvasAnnouncer.announce(
                    .status("Opened \(title) in its default app."))
            } else {
                appState.canvasAnnouncer.announce(
                    .error(
                        "\(target.isEmpty ? title : target) is missing from the vault. Use Locate File to repoint this card."
                    ))
            }
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

            HStack(spacing: Tokens.Spacing.sm) {
                TextField("Filter cards", text: filterBinding, prompt: Text("Filter cards (⌘F)"))
                    .textFieldStyle(.roundedBorder)
                    .frame(maxWidth: 260)
                    .focused($filterFocused)
                    .accessibilityLabel("Filter cards")
                    .accessibilityHint(
                        "Narrows the outline and table by title, type, group, or target. Escape clears."
                    )
                if document.filterActive {
                    // t0 §3: the result summary is pull-readable.
                    Text("\(document.filteredOutline.count) of \(document.outline.count) cards match")
                        .font(Tokens.Typography.caption)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                    Button("Clear") { appState.canvasClearFilter() }
                        .accessibilityLabel("Clear filter")
                }
            }

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

    private var filterBinding: Binding<String> {
        Binding(
            get: { document.filterText },
            set: { document.filterText = $0 }
        )
    }

    private var surfaceBinding: Binding<CanvasSurface> {
        Binding(
            get: { appState.workspace.canvasSurface(for: tabID) },
            set: { appState.workspace.setCanvasSurface($0, for: tabID) }
        )
    }

    // MARK: Empty / error states

    /// Actionable onboarding (#368: leads with New Card).
    private var emptyOnboarding: some View {
        VStack(spacing: Tokens.Spacing.sm) {
            Spacer()
            Text("Canvas is empty.")
                .font(Tokens.Typography.body)
            Text(
                "Press ⌥⌘N to create your first card. Every other canvas action is in the Command Palette (⌘⇧P)."
            )
            .font(Tokens.Typography.body)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .multilineTextAlignment(.center)
            Spacer()
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(
            "Canvas is empty. Press Option Command N to create your first card. Every other canvas action is in the Command Palette, Command Shift P."
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
