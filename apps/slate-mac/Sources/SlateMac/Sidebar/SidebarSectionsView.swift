// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// FL3-3 (#660): the Shortcuts and Recents sections rendered above the
/// folder tree. Each is an AX-labeled group with a rotor-navigable
/// header; collapsed state is device-local; empty sections keep a quiet
/// placeholder row so the affordance stays discoverable (especially for
/// VoiceOver users). Folder shortcuts activate as navigation containers;
/// file shortcuts and every Recent are leaves that open through the
/// normal file-open seam.
struct SidebarSectionsView: View {
    @EnvironmentObject private var appState: AppState
    @AppStorage("slate.sidebar.shortcutsCollapsed")
    private var shortcutsCollapsed = false
    @AppStorage("slate.sidebar.recentsCollapsed")
    private var recentsCollapsed = false

    let activateShortcut: (SidebarShortcut) -> Void
    let openRecent: (String) -> Void

    private func displayName(for path: String) -> String {
        if let summary = appState.files.first(where: { $0.path == path }),
            let display = summary.displayName, !display.isEmpty
        {
            return display
        }
        let stem = (path as NSString).lastPathComponent
        return (stem as NSString).deletingPathExtension.isEmpty
            ? stem : (stem as NSString).deletingPathExtension
    }

    /// A folder shortcut shows its path subtitle when another visible
    /// shortcut shares the same trailing name (FL3-3.5).
    private func folderSubtitle(for shortcut: SidebarShortcut) -> String? {
        guard shortcut.kind == .folder else { return nil }
        let name = (shortcut.path as NSString).lastPathComponent
        let twins = appState.sidebarOrganization.shortcuts.filter {
            ($0.path as NSString).lastPathComponent == name
        }
        return twins.count > 1 ? shortcut.path : nil
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            shortcutsSection
            recentsSection
        }
    }

    /// FL3-3.1: each section is its own AX-labeled group landmark, so
    /// VoiceOver reports "Shortcuts, group" / "Recents, group" context.
    private var shortcutsSection: some View {
        VStack(alignment: .leading, spacing: 0) {
            sectionHeader(
                title: "Shortcuts",
                collapsed: $shortcutsCollapsed,
                accessibilityIdentifier: "sidebar.shortcuts.header")
            if !shortcutsCollapsed {
                let shortcuts = appState.sidebarOrganization.shortcuts
                if shortcuts.isEmpty {
                    placeholderRow(
                        "No shortcuts — right-click a file to add one")
                } else {
                    ForEach(Array(shortcuts.enumerated()), id: \.element) {
                        index, shortcut in
                        shortcutRow(shortcut, slot: index + 1)
                    }
                }
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Shortcuts")
    }

    private var recentsSection: some View {
        VStack(alignment: .leading, spacing: 0) {
            sectionHeader(
                title: "Recents",
                collapsed: $recentsCollapsed,
                accessibilityIdentifier: "sidebar.recents.header")
            if !recentsCollapsed {
                let recents = appState.sidebarRecentsForDisplay
                if recents.isEmpty {
                    placeholderRow("No recent files yet")
                } else {
                    ForEach(recents, id: \.self) { path in
                        recentRow(path)
                    }
                }
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Recents")
    }

    private func sectionHeader(
        title: String,
        collapsed: Binding<Bool>,
        accessibilityIdentifier: String
    ) -> some View {
        Button {
            collapsed.wrappedValue.toggle()
        } label: {
            HStack(spacing: Tokens.Spacing.xs) {
                SlateSymbol.disclosure.decorative
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .rotationEffect(
                        .degrees(collapsed.wrappedValue ? 0 : 90))
                Text(title)
                    .font(Tokens.Typography.sectionHeader)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                Spacer(minLength: 0)
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .padding(.horizontal, Tokens.Spacing.md)
        .padding(.vertical, Tokens.Spacing.xs)
        .accessibilityIdentifier(accessibilityIdentifier)
        .accessibilityLabel(title)
        .accessibilityAddTraits(.isHeader)
        .accessibilityHint(
            collapsed.wrappedValue
                ? "Expands the \(title) section."
                : "Collapses the \(title) section.")
    }

    private func placeholderRow(_ text: String) -> some View {
        Text(text)
            .font(Tokens.Typography.caption)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .padding(.horizontal, Tokens.Spacing.lg)
            .padding(.vertical, Tokens.Spacing.xs)
            .frame(maxWidth: .infinity, alignment: .leading)
            .accessibilityLabel(text)
    }

    /// FL5-2 review round: tag/untagged shortcuts are CONTAINERS with
    /// their own presentation — a blank "file" row for an empty-path
    /// untagged entry would be wrong visually and in the AX tree.
    /// Static so tests pin the exact strings per kind.
    static func shortcutPresentation(
        for shortcut: SidebarShortcut
    ) -> (label: String, value: String, hint: String, symbol: SlateSymbol) {
        switch shortcut.kind {
        case .folder:
            let name = (shortcut.path as NSString).lastPathComponent
            return (
                name, "folder shortcut",
                "Reveals and selects this folder in the file tree.",
                .folder)
        case .file:
            let name =
                ((shortcut.path as NSString).lastPathComponent as NSString)
                .deletingPathExtension
            return (name, "file shortcut", "Opens this note.", .newNote)
        case .tag:
            return (
                "#\(shortcut.path)", "tag shortcut",
                "Filters the file list to this tag.", .tag)
        case .untagged:
            return (
                "Untagged", "untagged shortcut",
                "Shows notes with no tags.", .tag)
        }
    }

    private func shortcutRow(_ shortcut: SidebarShortcut, slot: Int) -> some View {
        let presentation = Self.shortcutPresentation(for: shortcut)
        return Button {
            activateShortcut(shortcut)
        } label: {
            HStack(spacing: Tokens.Spacing.sm) {
                presentation.symbol.decorative
                VStack(alignment: .leading, spacing: 0) {
                    Text(presentation.label)
                        .font(Tokens.Typography.body)
                        .lineLimit(2)
                    if let subtitle = folderSubtitle(for: shortcut) {
                        Text(subtitle)
                            .font(Tokens.Typography.caption)
                            .foregroundStyle(Tokens.ColorRole.textSecondary)
                            .lineLimit(2)
                    }
                }
                Spacer(minLength: 0)
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .padding(.horizontal, Tokens.Spacing.lg)
        .padding(.vertical, Tokens.Spacing.xs)
        .accessibilityLabel(presentation.label)
        .accessibilityValue(presentation.value)
        .accessibilityHint(presentation.hint)
        .contextMenu {
            Button("Move Up") {
                appState.moveSidebarShortcut(shortcut, delta: -1)
            }
            .disabled(slot == 1)
            Button("Move Down") {
                appState.moveSidebarShortcut(shortcut, delta: 1)
            }
            .disabled(slot == appState.sidebarOrganization.shortcuts.count)
            Divider()
            Button("Remove from Shortcuts") {
                try? appState.removeSidebarShortcutDirect(shortcut)
            }
        }
    }

    private func recentRow(_ path: String) -> some View {
        Button {
            openRecent(path)
        } label: {
            HStack(spacing: Tokens.Spacing.sm) {
                SlateSymbol.history.decorative
                Text(displayName(for: path))
                    .font(Tokens.Typography.body)
                    .lineLimit(2)
                Spacer(minLength: 0)
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .padding(.horizontal, Tokens.Spacing.lg)
        .padding(.vertical, Tokens.Spacing.xs)
        .accessibilityLabel(displayName(for: path))
        .accessibilityValue("recent file")
        .accessibilityHint("Opens this note.")
        .contextMenu {
            Button("Clear Recents") {
                _ = try? appState.dispatchSidebarAction(
                    id: SlateCommandID.sidebarClearRecents)
            }
        }
    }
}

/// FL3-3.2: local key monitor for the ⌃1–⌃9 shortcut chords, active only
/// while the file tree has key focus (mirrors TreeOpenSelectedKeyMonitor).
struct SidebarShortcutChordMonitor: NSViewRepresentable {
    let enabled: Bool
    let isRenaming: Bool
    let activateSlot: (Int) -> Void

    final class Coordinator {
        var monitor: Any?
        var enabled = false
        var isRenaming = false
        var activateSlot: (Int) -> Void = { _ in }
    }

    func makeCoordinator() -> Coordinator { Coordinator() }

    func makeNSView(context: Context) -> NSView {
        let view = NSView(frame: .zero)
        context.coordinator.monitor = NSEvent.addLocalMonitorForEvents(
            matching: .keyDown
        ) { [weak coordinator = context.coordinator] event in
            guard let coordinator, coordinator.enabled,
                !coordinator.isRenaming,
                event.modifierFlags.intersection(
                    [.command, .option, .control, .shift]) == .control,
                let characters = event.charactersIgnoringModifiers,
                characters.count == 1,
                let digit = Int(characters), (1...9).contains(digit)
            else { return event }
            coordinator.activateSlot(digit)
            return nil
        }
        return view
    }

    func updateNSView(_ nsView: NSView, context: Context) {
        context.coordinator.enabled = enabled
        context.coordinator.isRenaming = isRenaming
        context.coordinator.activateSlot = activateSlot
    }

    static func dismantleNSView(_ nsView: NSView, coordinator: Coordinator) {
        if let monitor = coordinator.monitor {
            NSEvent.removeMonitor(monitor)
        }
        coordinator.monitor = nil
    }
}
