// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The tab strip above a tab group's content (U1-2, #454).
///
/// AX contract (u1_spec §U1-2): the strip is a contained, labeled region
/// ("Tabs"); each tab is a Button carrying `.isSelected` when active and an
/// `accessibilityValue` of "tab N of M[, edited]", so VoiceOver reads
/// "notes, tab 2 of 5, edited, selected". The dirty state is never conveyed
/// by the dot alone (WCAG 1.4.1) — the value string carries it.
///
/// Reorder: ⌃⌘←/→ commands are the source of truth (keyboard parity);
/// pointer drag arrives with the same `moveTab` call underneath. Overflow:
/// the strip scrolls horizontally and the trailing "All tabs" menu is the
/// enumerable fallback.
struct TabBarView: View {
    @EnvironmentObject private var appState: AppState
    let group: TabGroupNode
    /// True when this group is the focused pane (U1-3) — or the only pane.
    /// Drives the strip's focus indicator: a 2pt accent top border, paired
    /// with the active tab's bolded title (never color alone).
    var isFocusedGroup: Bool = true

    private var activeTabIsMarkdown: Bool {
        if case .markdown = group.activeTab?.item { return true }
        return false
    }

    /// Tab-strip row height (u5_spec §U5-2 density target: 30pt). A fixed
    /// SHAPE height, not text — the titles inside are `Tokens.Typography`
    /// roles and still scale with Dynamic Type (WCAG 1.4.4); the 24pt hit
    /// targets sit inside this with breathing room.
    private static let stripHeight: CGFloat = 30

    var body: some View {
        HStack(spacing: 0) {
            // The reading/editing toggle is a note-editor affordance;
            // canvas/base tabs switch their own surfaces/views.
            if activeTabIsMarkdown {
                modeToggle
            }
            ScrollViewReader { proxy in
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: Tokens.Spacing.xxs) {
                        ForEach(Array(group.tabs.enumerated()), id: \.element.id) { index, tab in
                            tabItem(tab, index: index)
                                .id(tab.id)
                        }
                    }
                    .padding(.horizontal, Tokens.Spacing.xs)
                }
                .onChange(of: group.activeTabID) { _, newValue in
                    guard let newValue else { return }
                    proxy.scrollTo(newValue)
                }
            }
            Spacer(minLength: 0)
            allTabsMenu
        }
        .frame(height: Self.stripHeight)
        // macOS 26 Liquid Glass on the strip; the exact `surfaceSecondary`
        // token below 26 (U5-1) — appearance-only, no layout change.
        .slateChromeMaterial(fallback: Tokens.ColorRole.surfaceSecondary)
        // Tab-strip glyphs (mode toggle, close, all-tabs) render monochrome so
        // the strip reads as one continuous command band with the toolbar
        // (U5-1, DoD §B rendering-mode consistency).
        .slateSymbolSurface(.tabStrip)
        // Focused-pane indicator (U1-3): the strip of the focused group
        // carries a 2pt accent top border. Never the only cue — the pane's
        // AX label + the active tab's bolded title travel with it.
        .overlay(alignment: .top) {
            if isFocusedGroup {
                Rectangle()
                    .fill(Tokens.ColorRole.accentFill)
                    .frame(height: 2)
                    .accessibilityHidden(true)
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Tabs")
    }

    /// The reading/editing toggle at the strip's leading edge (U3-2, #466).
    /// Shows the TARGET mode's glyph + label ("Reading mode" enters
    /// reading); `accessibilityValue` carries the CURRENT mode so VoiceOver
    /// reads "Reading mode, button, Currently editing". The CURRENT mode is
    /// also visible to sighted users: the control carries an accent wash +
    /// tint while the pane is in reading mode (a toggle that's "on"), so
    /// the state isn't AT-only — without it, a pointer user had to infer
    /// the mode from whether the content looked like raw Markdown.
    /// Toggling a non-focused group's strip focuses that group first — one
    /// funnel, the group's own active tab flips. Hidden for empty groups
    /// (nothing to render) — the ⌘⇧E menu item stays the single shortcut
    /// owner.
    @ViewBuilder
    private var modeToggle: some View {
        if let activeTabID = group.activeTabID {
            let current = appState.workspace.viewMode(for: activeTabID)
            let target: NoteViewMode = current == .editing ? .reading : .editing
            let isReading = current == .reading
            Button {
                // Non-focused group: focus it FIRST through the identity
                // funnel (activateTab restores that pane's document into
                // the live fields — never mutate mode across a bypassed
                // focus change). Focused group: activateTab is the cheap
                // same-tab path.
                appState.selectTab(id: activeTabID)
                appState.setViewMode(target, for: activeTabID)
            } label: {
                (target == .reading
                    ? SlateSymbol.readingMode : SlateSymbol.editingMode)
                    .decorative
                    .foregroundStyle(
                        isReading
                            ? Tokens.ColorRole.accentText
                            : Tokens.ColorRole.textPrimary)
                    // 28pt — the HIG macOS default click target (the
                    // #866 floor; these two strip buttons were missed).
                    .frame(minWidth: 28, minHeight: 28)
                    .contentShape(Rectangle())
                    .background(
                        isReading
                            ? Tokens.ColorRole.accentFill.opacity(0.18)
                            : Color.clear,
                        in: RoundedRectangle(cornerRadius: Tokens.Radius.control))
            }
            .buttonStyle(.interactiveRow())
            .padding(.leading, Tokens.Spacing.xs)
            .accessibilityLabel(
                target == .reading ? "Reading mode" : "Editing mode")
            .accessibilityValue(
                Text(current == .editing ? "Currently editing" : "Currently reading"))
            .accessibilityHint("Switches this pane's note view. Shift-Command-E.")
            .help(
                (target == .reading ? "Reading mode" : "Editing mode")
                    + " (⇧⌘E)")
        }
    }

    /// "tab N of M[, edited][, canvas/base/saved query/dashboard/graph]" — the VoiceOver value for one tab.
    /// Static so tests pin the exact strings. The kind rides in the value
    /// (t0 §3 inspectability), not the label, so the speakable name stays
    /// the filename for Voice Control.
    static func accessibilityValue(
        index: Int, count: Int, isDirty: Bool, isCanvas: Bool = false,
        isBase: Bool = false, isSavedQuery: Bool = false, isDashboard: Bool = false,
        isGraph: Bool = false
    ) -> String {
        "tab \(index + 1) of \(count)" + (isDirty ? ", edited" : "")
            + (isCanvas ? ", canvas" : "")
            + (isBase ? ", base" : "")
            + (isSavedQuery ? ", saved query" : "")
            + (isDashboard ? ", dashboard" : "")
            + (isGraph ? ", graph" : "")
    }

    private func isDirty(_ tab: WorkspaceTab) -> Bool {
        guard case .markdown = tab.item else { return false }
        return appState.noteTabHasUnsavedChanges(tab.id)
    }

    private func title(_ tab: WorkspaceTab) -> String {
        appState.workspace.tabTitle(tab)
    }

    @ViewBuilder
    private func tabItem(_ tab: WorkspaceTab, index: Int) -> some View {
        let active = tab.id == group.activeTabID
        let dirty = isDirty(tab)

        HStack(spacing: Tokens.Spacing.xs) {
            Button {
                appState.selectTab(id: tab.id)
            } label: {
                HStack(spacing: Tokens.Spacing.xs) {
                    if case .canvas = tab.item {
                        // Kind marker (decorative: the AX value carries
                        // "canvas" — see accessibilityValue below).
                        SlateSymbol.canvas.decorative
                            .font(Tokens.Typography.caption)
                            .foregroundStyle(Tokens.ColorRole.textSecondary)
                    }
                    if case .base = tab.item {
                        SlateSymbol.base.decorative
                            .font(Tokens.Typography.caption)
                            .foregroundStyle(Tokens.ColorRole.textSecondary)
                    }
                    if case .savedQuery = tab.item {
                        SlateSymbol.base.decorative
                            .font(Tokens.Typography.caption)
                            .foregroundStyle(Tokens.ColorRole.textSecondary)
                    }
                    if case .dashboard = tab.item {
                        SlateSymbol.base.decorative
                            .font(Tokens.Typography.caption)
                            .foregroundStyle(Tokens.ColorRole.textSecondary)
                    }
                    if case .graph = tab.item {
                        // Kind marker (decorative: the AX value carries
                        // "graph" — see accessibilityValue below).
                        SlateSymbol.graph.decorative
                            .font(Tokens.Typography.caption)
                            .foregroundStyle(Tokens.ColorRole.textSecondary)
                    }
                    Text(title(tab))
                        .font(active ? Tokens.Typography.body.weight(.semibold) : Tokens.Typography.body)
                        .foregroundStyle(
                            active ? Tokens.ColorRole.textPrimary : Tokens.ColorRole.textSecondary)
                    if dirty {
                        // A text glyph, not a fixed-frame shape: it scales
                        // with Dynamic Type and is invisible to target-size
                        // linting (it isn't a target — the value string
                        // carries "edited" for VoiceOver).
                        Text(verbatim: "●")
                            .font(Tokens.Typography.caption)
                            .foregroundStyle(Tokens.ColorRole.accentText)
                            .accessibilityHidden(true)
                    }
                }
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.vertical, Tokens.Spacing.xs)
            }
            // Shared rest/hover/pressed affordance (U5-2). Selection stays the
            // outer `surface` fill below; focus stays the system ring.
            .buttonStyle(.interactiveRow())
            .accessibilityValue(
                Self.accessibilityValue(
                    index: index, count: group.tabs.count, isDirty: dirty,
                    isCanvas: { if case .canvas = tab.item { return true }; return false }(),
                    isBase: { if case .base = tab.item { return true }; return false }(),
                    isSavedQuery: { if case .savedQuery = tab.item { return true }; return false }(),
                    isDashboard: { if case .dashboard = tab.item { return true }; return false }(),
                    isGraph: { if case .graph = tab.item { return true }; return false }())
            )
            .accessibilityAddTraits(active ? [.isSelected] : [])
            .accessibilityHint(
                "Activates this tab. Close, reorder, and split actions are in the context menu.")

            Button {
                appState.requestCloseTab(tab.id)
            } label: {
                SlateSymbol.closeTab.image(label: "Close tab")
                    .font(.caption2.weight(.bold))
                    // 28pt — HIG macOS default click target (#866 floor).
                    .frame(minWidth: 28, minHeight: 28)
                    .contentShape(Rectangle())
            }
            // Close is its own hover/pressed target (U5-2) so it lights up
            // independently of the tab body it sits beside.
            .buttonStyle(.interactiveRow())
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .opacity(active ? 1 : 0.6)
            .accessibilityValue(Text(title(tab)))
            .help("Close tab")
        }
        .background(
            RoundedRectangle(cornerRadius: Tokens.Radius.control)
                .fill(active ? Tokens.ColorRole.surface : Color.clear)
        )
        .contextMenu {
            Button("Close Tab") { appState.requestCloseTab(tab.id) }
            Button("Move Tab Left") {
                appState.selectTab(id: tab.id)
                appState.moveActiveTabLeft()
            }
            Button("Move Tab Right") {
                appState.selectTab(id: tab.id)
                appState.moveActiveTabRight()
            }
            // Context menus HIDE unavailable items rather than dimming
            // them (context-menus.md:35 — the macOS dim exception covers
            // only the Cut/Copy/Paste family). The MENU-BAR Split items
            // keep disabled-not-hidden, which is correct there. The
            // Divider rides INSIDE the gate so capacity never strands an
            // orphan separator (Codex review).
            if !appState.workspace.isAtPaneCapacity {
                Divider()
                Button("Split Right") {
                    appState.selectTab(id: tab.id)
                    appState.splitActivePane(axis: .horizontal)
                }
                Button("Split Down") {
                    appState.selectTab(id: tab.id)
                    appState.splitActivePane(axis: .vertical)
                }
            }
        }
    }

    /// Enumerable fallback for overflowed strips: every tab, selectable.
    private var allTabsMenu: some View {
        Menu {
            ForEach(Array(group.tabs.enumerated()), id: \.element.id) { index, tab in
                Button {
                    appState.selectTab(id: tab.id)
                } label: {
                    let dirtyMark = isDirty(tab) ? " — edited" : ""
                    let activeMark = tab.id == group.activeTabID ? " (active)" : ""
                    Text("\(index + 1). \(title(tab))\(dirtyMark)\(activeMark)")
                }
            }
        } label: {
            SlateSymbol.moreActions.image(label: "All tabs")
        }
        .menuStyle(.borderlessButton)
        .frame(width: 28)
        .padding(.trailing, Tokens.Spacing.xs)
        // Verb-first (offering-help.md).
        .help("Show all open tabs")
    }
}
