// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// FL5-2 (#665): pure presentation math for the Tags section — which
/// flattened pre-order entries are visible under a disclosure set, and
/// the exact AX strings. Kept view-free so tests exercise it directly.
enum SidebarTagTreeModel {
  struct Row: Equatable {
    let entry: TagTreeEntry
    let hasChildren: Bool
    let isExpanded: Bool
  }

  /// Visible rows for the flattened pre-order `entries`: an entry shows
  /// iff every ancestor is expanded. `hasChildren` = the next entry is
  /// deeper (pre-order property).
  static func visibleRows(
    entries: [TagTreeEntry], expanded: Set<String>
  ) -> [Row] {
    var rows: [Row] = []
    var ancestors: [TagTreeEntry] = []
    for (index, entry) in entries.enumerated() {
      while let last = ancestors.last, last.depth >= entry.depth {
        ancestors.removeLast()
      }
      let visible = ancestors.allSatisfy { expanded.contains($0.full) }
      let hasChildren =
        index + 1 < entries.count && entries[index + 1].depth > entry.depth
      if visible {
        rows.append(
          Row(
            entry: entry,
            hasChildren: hasChildren,
            isExpanded: expanded.contains(entry.full)))
      }
      ancestors.append(entry)
    }
    return rows
  }

  /// Folder-row AX parity (#420 lesson: state baked into the value, not
  /// custom traits): `"12 notes, collapsed, level 1"`.
  static func accessibilityValue(for row: Row) -> String {
    let notes = row.entry.fileCount == 1 ? "1 note" : "\(row.entry.fileCount) notes"
    var parts = [notes]
    if row.hasChildren {
      parts.append(row.isExpanded ? "expanded" : "collapsed")
    }
    parts.append("level \(row.entry.depth + 1)")
    return parts.joined(separator: ", ")
  }

  /// The header's visual count: distinct REAL tags (direct carriers) —
  /// the same number the core summary announces; synthesized
  /// intermediates are navigation, not tags.
  static func realTagCount(entries: [TagTreeEntry]) -> Int {
    entries.lazy.filter { $0.directCount > 0 }.count
  }
}

/// The collapsible Tags section rendered below the folder tree (final
/// order: Shortcuts, Recents, tree, Tags — spec FL5-2 rule 1). Default
/// collapsed; content fetches on first expand and refreshes on the
/// mutation-announcement funnel while expanded.
struct SidebarTagTreeView: View {
  @EnvironmentObject private var appState: AppState
  /// Per-node disclosure. Session-local view state (only the SECTION's
  /// collapsed state is device-local per spec).
  @State private var expandedTags: Set<String> = []

  var body: some View {
    VStack(alignment: .leading, spacing: 0) {
      header
      if appState.sidebarTagsSectionExpanded {
        content
      }
    }
    .accessibilityElement(children: .contain)
    .accessibilityLabel("Tags")
  }

  private var header: some View {
    Button {
      appState.sidebarTagsSectionExpanded.toggle()
    } label: {
      HStack(spacing: Tokens.Spacing.xs) {
        SlateSymbol.disclosure.decorative
          .rotationEffect(
            .degrees(appState.sidebarTagsSectionExpanded ? 90 : 0))
          .foregroundStyle(Tokens.ColorRole.textSecondary)
        Text("Tags")
          .font(Tokens.Typography.sectionHeader)
          .foregroundStyle(Tokens.ColorRole.textPrimary)
        if let tree = appState.sidebarTagTree {
          Text("\(SidebarTagTreeModel.realTagCount(entries: tree.entries))")
            .font(Tokens.Typography.caption)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        Spacer(minLength: 0)
      }
      .contentShape(Rectangle())
    }
    .buttonStyle(.plain)
    .padding(.horizontal, Tokens.Spacing.sm)
    .padding(.vertical, Tokens.Spacing.xs)
    .accessibilityLabel("Tags")
    .accessibilityValue(headerAccessibilityValue)
    .accessibilityHint("Shows every tag as a tree. Activating a tag filters the file list.")
    .accessibilityAddTraits(.isHeader)
  }

  private var headerAccessibilityValue: String {
    guard appState.sidebarTagsSectionExpanded else { return "collapsed" }
    guard let tree = appState.sidebarTagTree else { return "expanded" }
    return "expanded, \(tree.audioSummary)"
  }

  @ViewBuilder
  private var content: some View {
    if let tree = appState.sidebarTagTree {
      if tree.entries.isEmpty && tree.untaggedCount == 0 {
        Text("No tags yet.")
          .font(Tokens.Typography.caption)
          .foregroundStyle(Tokens.ColorRole.textSecondary)
          .padding(.horizontal, Tokens.Spacing.lg)
          .padding(.vertical, Tokens.Spacing.xs)
      } else {
        let rows = SidebarTagTreeModel.visibleRows(
          entries: tree.entries, expanded: expandedTags)
        ForEach(rows, id: \.entry.full) { row in
          tagRow(row)
        }
        if tree.untaggedCount > 0 {
          untaggedRow(count: tree.untaggedCount)
        }
      }
    } else {
      Text("Loading tags…")
        .font(Tokens.Typography.caption)
        .foregroundStyle(Tokens.ColorRole.textSecondary)
        .padding(.horizontal, Tokens.Spacing.lg)
        .padding(.vertical, Tokens.Spacing.xs)
    }
  }

  private func tagRow(_ row: SidebarTagTreeModel.Row) -> some View {
    HStack(spacing: Tokens.Spacing.xs) {
      Color.clear
        .frame(
          width: CGFloat(row.entry.depth) * Tokens.Spacing.md, height: 0)
        .accessibilityHidden(true)
      if row.hasChildren {
        Button {
          toggle(row.entry.full)
        } label: {
          SlateSymbol.disclosure.decorative
            .rotationEffect(.degrees(row.isExpanded ? 90 : 0))
            .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .buttonStyle(.plain)
        .accessibilityHidden(true)
      } else {
        SlateSymbol.tag.decorative
          .font(Tokens.Typography.caption)
          .foregroundStyle(Tokens.ColorRole.textSecondary)
      }
      Text(row.entry.segment)
        .font(Tokens.Typography.body)
        .foregroundStyle(Tokens.ColorRole.textPrimary)
        .lineLimit(2)
      Spacer(minLength: 0)
      Text("\(row.entry.fileCount)")
        .font(Tokens.Typography.caption)
        .foregroundStyle(Tokens.ColorRole.textSecondary)
        .accessibilityHidden(true)
    }
    .contentShape(Rectangle())
    .onTapGesture {
      appState.sidebarFilterModel.activateTagQuery("#\(row.entry.full)")
    }
    .padding(.horizontal, Tokens.Spacing.sm)
    .padding(.vertical, Tokens.Spacing.xxs)
    .accessibilityElement(children: .ignore)
    .accessibilityLabel(row.entry.segment)
    .accessibilityValue(SidebarTagTreeModel.accessibilityValue(for: row))
    .accessibilityHint(
      row.hasChildren
        ? "Filters the file list to this tag. Actions in the context menu expand it or copy it."
        : "Filters the file list to this tag. Other actions are in the context menu.")
    .accessibilityAddTraits(.isButton)
    .accessibilityAction(named: row.isExpanded ? "Collapse" : "Expand") {
      if row.hasChildren { toggle(row.entry.full) }
    }
    .contextMenu {
      Button {
        appState.sidebarFilterModel.activateTagQuery("#\(row.entry.full)")
      } label: {
        SlateSymbol.search.label("Filter by Tag")
      }
      Button {
        appState.copySidebarTag(row.entry.full)
      } label: {
        SlateSymbol.copyPath.label("Copy Tag")
      }
      Button {
        appState.addSidebarTagShortcutDirect(
          kind: .tag, path: row.entry.full)
      } label: {
        SlateSymbol.pin.label("Add to Shortcuts")
      }
    }
  }

  private func untaggedRow(count: UInt32) -> some View {
    HStack(spacing: Tokens.Spacing.xs) {
      SlateSymbol.tag.decorative
        .font(Tokens.Typography.caption)
        .foregroundStyle(Tokens.ColorRole.textSecondary)
      Text("Untagged")
        .font(Tokens.Typography.body)
        .foregroundStyle(Tokens.ColorRole.textPrimary)
      Spacer(minLength: 0)
      Text("\(count)")
        .font(Tokens.Typography.caption)
        .foregroundStyle(Tokens.ColorRole.textSecondary)
        .accessibilityHidden(true)
    }
    .contentShape(Rectangle())
    .onTapGesture {
      appState.sidebarFilterModel.activateUntaggedScope()
    }
    .padding(.horizontal, Tokens.Spacing.sm)
    .padding(.vertical, Tokens.Spacing.xxs)
    .accessibilityElement(children: .ignore)
    .accessibilityLabel("Untagged")
    .accessibilityValue(count == 1 ? "1 note" : "\(count) notes")
    .accessibilityHint("Shows notes with no tags. Other actions are in the context menu.")
    .accessibilityAddTraits(.isButton)
    .contextMenu {
      Button {
        appState.addSidebarTagShortcutDirect(kind: .untagged, path: "")
      } label: {
        SlateSymbol.pin.label("Add to Shortcuts")
      }
    }
  }

  private func toggle(_ full: String) {
    if expandedTags.contains(full) {
      expandedTags.remove(full)
    } else {
      expandedTags.insert(full)
    }
  }
}
