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

    var body: some View {
        HStack(spacing: 0) {
            modeToggle
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
        .frame(height: 30)
        .background(Tokens.ColorRole.surfaceSecondary)
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
    /// reads "Reading mode, button, Currently editing". Toggling a
    /// non-focused group's strip focuses that group first — one funnel,
    /// the group's own active tab flips. Hidden for empty groups (nothing
    /// to render) — the ⌘⇧E menu item stays the single shortcut owner.
    @ViewBuilder
    private var modeToggle: some View {
        if let activeTabID = group.activeTabID {
            let current = appState.workspace.viewMode(for: activeTabID)
            let target: NoteViewMode = current == .editing ? .reading : .editing
            Button {
                // Non-focused group: focus it FIRST through the identity
                // funnel (activateTab restores that pane's document into
                // the live fields — never mutate mode across a bypassed
                // focus change). Focused group: activateTab is the cheap
                // same-tab path.
                appState.activateTab(activeTabID)
                appState.setViewMode(target, for: activeTabID)
            } label: {
                (target == .reading
                    ? SlateSymbol.readingMode : SlateSymbol.editingMode)
                    .decorative
                    .frame(width: 24, height: 24)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
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

    /// "tab N of M[, edited]" — the VoiceOver value for one tab. Static so
    /// tests pin the exact strings.
    static func accessibilityValue(index: Int, count: Int, isDirty: Bool) -> String {
        "tab \(index + 1) of \(count)" + (isDirty ? ", edited" : "")
    }

    private func isDirty(_ tab: WorkspaceTab) -> Bool {
        if tab.id == group.activeTabID {
            return appState.hasUnsavedChanges
        }
        return appState.workspace.document(for: tab.id)?.hasUnsavedChanges ?? false
    }

    private func title(_ tab: WorkspaceTab) -> String {
        let path = appState.workspace.tabPath(tab)
        let name = (path as NSString).lastPathComponent
        return (name as NSString).deletingPathExtension.isEmpty
            ? name
            : (name as NSString).deletingPathExtension
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
            .buttonStyle(.plain)
            .accessibilityValue(
                Self.accessibilityValue(index: index, count: group.tabs.count, isDirty: dirty)
            )
            .accessibilityAddTraits(active ? [.isSelected] : [])
            .accessibilityHint(
                "Activates this tab. Close, reorder, and split actions are in the context menu.")

            Button {
                appState.requestCloseTab(tab.id)
            } label: {
                SlateSymbol.closeTab.image(label: "Close tab")
                    .font(.caption2.weight(.bold))
                    .frame(width: 24, height: 24)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .opacity(active ? 1 : 0.6)
            .accessibilityValue(Text(title(tab)))
            .help("Close tab")
        }
        .background(
            RoundedRectangle(cornerRadius: 5)
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
            Divider()
            Button("Split Right") {
                appState.selectTab(id: tab.id)
                appState.splitActivePane(axis: .horizontal)
            }
            .disabled(appState.workspace.isAtPaneCapacity)
            Button("Split Down") {
                appState.selectTab(id: tab.id)
                appState.splitActivePane(axis: .vertical)
            }
            .disabled(appState.workspace.isAtPaneCapacity)
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
        .help("All tabs")
    }
}
