// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation
import SwiftUI

/// The device-local presentation choices a file row needs. Keeping this as a
/// value makes row projection deterministic and prevents a visible-row pass
/// from consulting `AppState.files` or `UserDefaults`.
struct SidebarRowPreferencesSnapshot: Equatable {
  enum DateSource: String, CaseIterable, Codable, Equatable {
    case modified
    case created
  }

  enum DateFormat: String, CaseIterable, Codable, Equatable {
    case relative
    case absolute
  }

  enum Density: String, CaseIterable, Codable, Equatable {
    case standard
    case compact
  }

  var dateSource: DateSource
  var dateFormat: DateFormat
  var previewLines: Int
  var showTaskCounts: Bool
  var showWordCount: Bool
  var density: Density

  static let defaults = SidebarRowPreferencesSnapshot(
    dateSource: .modified,
    dateFormat: .relative,
    previewLines: 0,
    showTaskCounts: true,
    showWordCount: false,
    density: .standard)
}

/// Injectable wrapper around FL-01's one canonical civil-date parser. Tests
/// use this seam to prove presentation formats the resolver's absolute `Date`
/// rather than parsing authored Gregorian components a second time.
protocol SidebarCivilDateResolving {
  func resolve(_ canonicalDate: String, calendar: Calendar) -> Date?
}

private struct ProductionSidebarCivilDateResolver: SidebarCivilDateResolving {
  func resolve(_ canonicalDate: String, calendar: Calendar) -> Date? {
    SidebarCivilDateResolver.resolve(canonicalDate, calendar: calendar)
  }
}

/// Formatting seam for deterministic row-model tests. Production uses the
/// bounded cache below; no formatter is allocated by an individual row.
protocol SidebarRowFormatting: AnyObject {
  func relativeDate(
    _ date: Date,
    relativeTo now: Date,
    locale: Locale,
    calendar: Calendar
  ) -> String
  func absoluteDate(_ date: Date, locale: Locale, calendar: Calendar) -> String
  func decimal(_ value: UInt32, locale: Locale) -> String
}

/// Shared, keyed formatter cache. Locale/calendar/time-zone are part of the
/// key and each formatter is configured once, so changing a system calendar
/// cannot race a visible-row render or reinterpret FL-01's resolved `Date`.
final class SidebarRowFormatterCache: SidebarRowFormatting {
  static let shared = SidebarRowFormatterCache()

  private struct DateKey: Hashable {
    let locale: String
    let calendar: String
    let timeZone: String
  }

  private let lock = NSLock()
  private var relativeFormatters: [DateKey: RelativeDateTimeFormatter] = [:]
  private var absoluteFormatters: [DateKey: DateFormatter] = [:]
  private var numberFormatters: [String: NumberFormatter] = [:]
  private(set) var creationCountForTesting = 0

  private init() {}

  func relativeDate(
    _ date: Date,
    relativeTo now: Date,
    locale: Locale,
    calendar: Calendar
  ) -> String {
    lock.lock()
    defer { lock.unlock() }
    let key = dateKey(locale: locale, calendar: calendar)
    let formatter: RelativeDateTimeFormatter
    if let cached = relativeFormatters[key] {
      formatter = cached
    } else {
      let created = RelativeDateTimeFormatter()
      created.locale = locale
      created.calendar = calendar
      created.unitsStyle = .full
      relativeFormatters[key] = created
      creationCountForTesting += 1
      formatter = created
    }
    return formatter.localizedString(for: date, relativeTo: now)
  }

  func absoluteDate(_ date: Date, locale: Locale, calendar: Calendar) -> String {
    lock.lock()
    defer { lock.unlock() }
    let key = dateKey(locale: locale, calendar: calendar)
    let formatter: DateFormatter
    if let cached = absoluteFormatters[key] {
      formatter = cached
    } else {
      let created = DateFormatter()
      created.locale = locale
      created.calendar = calendar
      created.timeZone = calendar.timeZone
      created.dateStyle = .medium
      created.timeStyle = .none
      absoluteFormatters[key] = created
      creationCountForTesting += 1
      formatter = created
    }
    return formatter.string(from: date)
  }

  func decimal(_ value: UInt32, locale: Locale) -> String {
    lock.lock()
    defer { lock.unlock() }
    let key = locale.identifier
    let formatter: NumberFormatter
    if let cached = numberFormatters[key] {
      formatter = cached
    } else {
      let created = NumberFormatter()
      created.locale = locale
      created.numberStyle = .decimal
      created.maximumFractionDigits = 0
      numberFormatters[key] = created
      creationCountForTesting += 1
      formatter = created
    }
    return formatter.string(from: NSNumber(value: value)) ?? String(value)
  }

  func resetForTesting() {
    lock.lock()
    defer { lock.unlock() }
    relativeFormatters.removeAll(keepingCapacity: false)
    absoluteFormatters.removeAll(keepingCapacity: false)
    numberFormatters.removeAll(keepingCapacity: false)
    creationCountForTesting = 0
  }

  private func dateKey(locale: Locale, calendar: Calendar) -> DateKey {
    DateKey(
      locale: locale.identifier,
      calendar: String(describing: calendar.identifier),
      timeZone: calendar.timeZone.identifier)
  }
}

/// Immutable, fully formatted presentation for one `FileSummary`. All string
/// work happens here, outside `SidebarFileRow.body`, and is O(1) per summary.
struct SidebarRowModel {
  let displayName: String
  let filename: String
  let filenameTooltip: String
  let accessibilityLabel: String
  let dateLabel: String
  let dateText: String
  let metadataText: String
  let previewText: String?
  let previewLineLimit: Int
  let taskBadgeText: String?
  let taskAccessibilityText: String?
  let taskBadgeIsComplete: Bool
  let accessibilityValue: String
  let visuallyShowsMetadata: Bool
  let visuallyShowsPreview: Bool
  let visuallyShowsTaskBadge: Bool
  let density: SidebarRowPreferencesSnapshot.Density

  init(
    summary: FileSummary,
    preferences: SidebarRowPreferencesSnapshot,
    now: Date = Date(),
    locale: Locale = .current,
    calendar: Calendar = .current,
    civilDateResolver: any SidebarCivilDateResolving = ProductionSidebarCivilDateResolver(),
    formatter: any SidebarRowFormatting = SidebarRowFormatterCache.shared
  ) {
    filename = summary.name
    filenameTooltip = summary.name
    if let authoredTitle = summary.displayName {
      displayName = Self.displayName(for: summary)
      accessibilityLabel = "\(authoredTitle) — file \(summary.name)"
    } else {
      displayName = Self.displayName(for: summary)
      accessibilityLabel = displayName
    }

    let selectedDate: Date
    switch preferences.dateSource {
    case .modified:
      dateLabel = "Modified"
      selectedDate = Self.date(fromMilliseconds: summary.mtimeMs)
    case .created:
      if let civilDate = summary.createdDate,
        let resolved = civilDateResolver.resolve(civilDate, calendar: calendar)
      {
        dateLabel = "Created"
        selectedDate = resolved
      } else if let createdMs = summary.createdMs {
        dateLabel = "Created"
        selectedDate = Self.date(fromMilliseconds: createdMs)
      } else {
        // Never call a modified timestamp "Created". This is also the
        // corruption-safe fallback if an impossible invalid civil date
        // crosses the FFI boundary without a datetime alternative.
        dateLabel = "Modified"
        selectedDate = Self.date(fromMilliseconds: summary.mtimeMs)
      }
    }

    switch preferences.dateFormat {
    case .relative:
      dateText = formatter.relativeDate(
        selectedDate,
        relativeTo: now,
        locale: locale,
        calendar: calendar)
    case .absolute:
      dateText = formatter.absoluteDate(
        selectedDate,
        locale: locale,
        calendar: calendar)
    }

    var metadata = "\(dateLabel) \(dateText)"
    if preferences.showWordCount, let wordCount = summary.wordCount {
      let count = formatter.decimal(wordCount, locale: locale)
      metadata += wordCount == 1 ? " · \(count) word" : " · \(count) words"
    }
    metadataText = metadata

    previewLineLimit = min(max(preferences.previewLines, 0), 3)
    if previewLineLimit > 0,
      let preview = summary.preview,
      !preview.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    {
      previewText = preview
    } else {
      previewText = nil
    }

    if preferences.showTaskCounts, summary.taskTotal > 0 {
      let open = min(summary.taskOpen, summary.taskTotal)
      taskBadgeText =
        "\(formatter.decimal(open, locale: locale))/\(formatter.decimal(summary.taskTotal, locale: locale))"
      taskBadgeIsComplete = open == 0
      if open == 0 {
        let noun = summary.taskTotal == 1 ? "task" : "tasks"
        taskAccessibilityText = "all \(summary.taskTotal) \(noun) done"
      } else {
        let noun = summary.taskTotal == 1 ? "task" : "tasks"
        taskAccessibilityText = "\(open) of \(summary.taskTotal) \(noun) open"
      }
    } else {
      taskBadgeText = nil
      taskAccessibilityText = nil
      taskBadgeIsComplete = false
    }

    var spokenParts = [metadata]
    if let previewText {
      spokenParts.append("Preview: \(previewText)")
    }
    if let taskAccessibilityText {
      spokenParts.append(taskAccessibilityText)
    }
    accessibilityValue = spokenParts.joined(separator: ", ")

    density = preferences.density
    let standard = preferences.density == .standard
    visuallyShowsMetadata = standard
    visuallyShowsPreview = standard && previewText != nil
    visuallyShowsTaskBadge = standard && taskBadgeText != nil
  }

  private static func date(fromMilliseconds value: Int64) -> Date {
    Date(timeIntervalSince1970: TimeInterval(value) / 1_000)
  }

  /// The primary label shared by rendering and type-select. A title authored
  /// in frontmatter is what the person sees and therefore what typing should
  /// match; otherwise use the filename stem.
  static func displayName(for summary: FileSummary) -> String {
    summary.displayName ?? (summary.name as NSString).deletingPathExtension
  }
}

/// System selection colors for a native macOS sidebar list. The foreground
/// must match the carrier the List actually paints, including the inactive
/// window state; project token selection colors are for custom washes and do
/// not describe AppKit's native focused-row highlight.
enum SidebarSelectionColors {
  static func text(active: Bool) -> NSColor {
    active ? .selectedMenuItemTextColor : .controlTextColor
  }

  static func background(active: Bool) -> NSColor {
    active ? .selectedContentBackgroundColor : .unemphasizedSelectedContentBackgroundColor
  }

  static func dropIndicator(selected: Bool, active: Bool) -> NSColor {
    selected ? text(active: active) : .tokenAccentText
  }
}

/// Content-only native List cell. Selection, activation, context menus, drag,
/// and rename remain owned by `FileTreeSidebar`, so rich presentation cannot
/// fork the browser's established interaction semantics.
struct SidebarFileRow: View {
  let model: SidebarRowModel
  let depth: Int
  let isSelected: Bool
  let selectionIsActive: Bool

  init(
    model: SidebarRowModel,
    depth: Int,
    isSelected: Bool,
    selectionIsActive: Bool = true
  ) {
    self.model = model
    self.depth = depth
    self.isSelected = isSelected
    self.selectionIsActive = selectionIsActive
  }

  /// HIG compact rows still need a comfortable pointer target. Expressed in
  /// the shared spacing scale (24 + 4), not a one-off layout literal.
  static let compactMinimumHeight = Tokens.Spacing.xl + Tokens.Spacing.xs

  private var primaryText: Color {
    isSelected
      ? Color(nsColor: SidebarSelectionColors.text(active: selectionIsActive))
      : Tokens.ColorRole.textPrimary
  }

  private var secondaryText: Color {
    isSelected
      ? Color(nsColor: SidebarSelectionColors.text(active: selectionIsActive))
      : Tokens.ColorRole.textSecondary
  }

  var body: some View {
    HStack(alignment: .top, spacing: Tokens.Spacing.xs) {
      Color.clear
        .frame(width: FileTreeSidebar.indentWidth(for: depth), height: 0)
        .accessibilityHidden(true)
      VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
        HStack(alignment: .firstTextBaseline, spacing: Tokens.Spacing.sm) {
          Text(model.displayName)
            .font(Tokens.Typography.body)
            .foregroundStyle(primaryText)
            .lineLimit(2)
          Spacer(minLength: 0)
          if model.visuallyShowsTaskBadge, let badge = model.taskBadgeText {
            HStack(spacing: Tokens.Spacing.xxs) {
              (model.taskBadgeIsComplete ? SlateSymbol.taskComplete : SlateSymbol.tasksLeaf)
                .decorative
              Text(badge)
            }
            .font(Tokens.Typography.caption)
            // HIG: don't use Light/Thin at the 10 pt caption floor. The
            // checkmark-square plus 0/N conveys completion explicitly; the
            // secondary role supplies a still-readable visual de-emphasis.
            .fontWeight(.regular)
            .foregroundStyle(model.taskBadgeIsComplete ? secondaryText : primaryText)
            .accessibilityHidden(true)
          }
        }
        if model.visuallyShowsMetadata {
          Text(model.metadataText)
            .font(Tokens.Typography.caption)
            .foregroundStyle(secondaryText)
        }
        if model.visuallyShowsPreview, let preview = model.previewText {
          Text(preview)
            .font(Tokens.Typography.caption)
            .foregroundStyle(secondaryText)
            .lineLimit(model.previewLineLimit)
            .truncationMode(.tail)
            .textSelection(.enabled)
        }
      }
      .frame(maxWidth: .infinity, alignment: .leading)
    }
    .frame(
      minHeight: model.density == .compact ? Self.compactMinimumHeight : nil,
      alignment: .leading
    )
    .accessibilityElement(children: .combine)
    .accessibilityLabel(model.accessibilityLabel)
    .accessibilityValue(model.accessibilityValue)
    .help(model.filenameTooltip)
  }
}
