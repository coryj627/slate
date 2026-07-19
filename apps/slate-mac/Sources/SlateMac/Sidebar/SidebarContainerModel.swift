// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// FL7-1 (#668): the dual-pane layout's value model — which navigation
/// rows are CONTAINERS (drive the list pane) versus LEAVES (open
/// directly), plus the pure focus-transfer rules. View-free so every
/// contract is unit-tested.
enum SidebarLayoutMode: String {
  case tree
  case dualPane
}

/// One list-pane scope. Folder/tag/Untagged rows — and shortcuts
/// targeting those scopes — select a container; file shortcuts and
/// Recents are leaves and never retarget the list.
enum SidebarContainer: Equatable, Hashable {
  case folder(path: String)
  case tag(full: String)
  case untagged

  /// The container a shortcut targets, or nil for LEAF kinds (files).
  static func forShortcut(_ shortcut: SidebarShortcut) -> SidebarContainer? {
    switch shortcut.kind {
    case .folder: return .folder(path: shortcut.path)
    case .tag: return .tag(full: shortcut.path)
    case .untagged: return .untagged
    case .file: return nil
    }
  }

  /// The container that represents an opened file for the editor →
  /// nav-pane mirror: its containing folder ("" = vault root).
  static func containing(filePath: String) -> SidebarContainer {
    guard let separator = filePath.lastIndex(of: "/") else {
      return .folder(path: "")
    }
    return .folder(path: String(filePath[..<separator]))
  }
}

/// Pure focus-walk decisions for the navigation pane (spec rule 4).
enum SidebarDualPaneFocus {
  enum RightArrowOutcome: Equatable {
    /// Inside-tree disclosure keeps priority: expand the collapsed
    /// container first.
    case disclose
    /// A container that is already expanded — or has nothing to
    /// disclose — hands focus to the list pane (Navigator convention).
    case moveToList
    /// Leaves never enter or retarget the list.
    case stay
  }

  static func rightArrow(
    isContainer: Bool, hasDisclosure: Bool, isExpanded: Bool
  ) -> RightArrowOutcome {
    guard isContainer else { return .stay }
    if hasDisclosure && !isExpanded { return .disclose }
    return .moveToList
  }

  /// ← in the list pane always returns to the selected container in
  /// the navigation pane; expressed as a helper for symmetry/testing.
  static func leftArrowReturnsToNavigation() -> Bool { true }
}

/// Divider persistence (spec rule 2): device-local fraction with a
/// clamped honest range so neither pane can collapse into nothing.
enum SidebarDualPaneDivider {
  static let defaultsKey = "slate.sidebar.dualPane.dividerFraction"
  static let minimumFraction = 0.2
  static let maximumFraction = 0.8
  static let defaultFraction = 0.55

  static func clamp(_ fraction: Double) -> Double {
    min(max(fraction, minimumFraction), maximumFraction)
  }

  static func load(from defaults: UserDefaults) -> Double {
    let stored = defaults.double(forKey: defaultsKey)
    guard stored > 0 else { return defaultFraction }
    return clamp(stored)
  }

  static func store(_ fraction: Double, in defaults: UserDefaults) {
    defaults.set(clamp(fraction), forKey: defaultsKey)
  }

  /// Anchor-based drag math (review round: `DragGesture` translation
  /// is CUMULATIVE from gesture start — adding it to the live fraction
  /// compounds every callback). The fraction is always derived from
  /// the gesture's STARTING fraction plus the current translation.
  static func dragged(
    fromAnchor anchor: Double, translation: Double, totalHeight: Double
  ) -> Double {
    guard totalHeight > 0 else { return clamp(anchor) }
    return clamp(anchor + translation / totalHeight)
  }
}
