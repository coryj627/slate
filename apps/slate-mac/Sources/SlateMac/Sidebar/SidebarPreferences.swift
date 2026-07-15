// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Device-local presentation choices for the Files sidebar.
///
/// These values describe how this Mac presents every vault. Vault-authored
/// overrides live in `.slate/sidebar.json` and deliberately do not share this
/// `UserDefaults` surface.
struct SidebarPreferences: Equatable {
  typealias DateSource = SidebarRowPreferencesSnapshot.DateSource
  typealias DateFormat = SidebarRowPreferencesSnapshot.DateFormat
  typealias Density = SidebarRowPreferencesSnapshot.Density

  enum Keys {
    static let dateSource = "sidebar.dateSource"
    static let dateFormat = "sidebar.dateFormat"
    static let previewLines = "sidebar.previewLines"
    static let showTaskCounts = "sidebar.showTaskCounts"
    static let showWordCount = "sidebar.showWordCount"
    static let density = "sidebar.density"
  }

  var dateSource: DateSource
  var dateFormat: DateFormat
  var previewLines: Int
  var showTaskCounts: Bool
  var showWordCount: Bool
  var density: Density

  init(
    dateSource: DateSource = .modified,
    dateFormat: DateFormat = .relative,
    previewLines: Int = 0,
    showTaskCounts: Bool = true,
    showWordCount: Bool = false,
    density: Density = .standard
  ) {
    self.dateSource = dateSource
    self.dateFormat = dateFormat
    self.previewLines = min(max(previewLines, 0), 3)
    self.showTaskCounts = showTaskCounts
    self.showWordCount = showWordCount
    self.density = density
  }

  /// Immutable, render-ready value passed into the lazy tree. Keeping this
  /// separate from `AppState` means each row remains a cheap value projection
  /// and never performs a global file lookup or observes unrelated app state.
  var rowSnapshot: SidebarRowPreferencesSnapshot {
    SidebarRowPreferencesSnapshot(
      dateSource: dateSource,
      dateFormat: dateFormat,
      previewLines: previewLines,
      showTaskCounts: showTaskCounts,
      showWordCount: showWordCount,
      density: density
    )
  }
}

extension SidebarRowPreferencesSnapshot.DateSource {
  var displayName: String {
    switch self {
    case .modified: return String(localized: "Modified")
    case .created: return String(localized: "Created")
    }
  }
}

extension SidebarRowPreferencesSnapshot.DateFormat {
  var displayName: String {
    switch self {
    case .relative: return String(localized: "Relative")
    case .absolute: return String(localized: "Absolute")
    }
  }
}

extension SidebarRowPreferencesSnapshot.Density {
  var displayName: String {
    switch self {
    case .standard: return String(localized: "Standard")
    case .compact: return String(localized: "Compact")
    }
  }
}
