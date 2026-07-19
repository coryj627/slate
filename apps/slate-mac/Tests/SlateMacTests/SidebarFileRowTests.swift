// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import SwiftUI
import XCTest

@testable import SlateMac

@MainActor
final class SidebarFileRowTests: XCTestCase {
  private final class ResolverSpy: SidebarCivilDateResolving {
    var calls: [(value: String, calendar: Calendar)] = []
    let result: Date?

    init(result: Date?) {
      self.result = result
    }

    func resolve(_ canonicalDate: String, calendar: Calendar) -> Date? {
      calls.append((canonicalDate, calendar))
      return result
    }
  }

  private final class FormatterSpy: SidebarRowFormatting {
    var relativeDates: [Date] = []
    var absoluteDates: [Date] = []
    var numbers: [UInt32] = []

    func relativeDate(
      _ date: Date,
      relativeTo now: Date,
      locale: Locale,
      calendar: Calendar
    ) -> String {
      relativeDates.append(date)
      return "RELATIVE"
    }

    func absoluteDate(_ date: Date, locale: Locale, calendar: Calendar) -> String {
      absoluteDates.append(date)
      return "ABSOLUTE"
    }

    func decimal(_ value: UInt32, locale: Locale) -> String {
      numbers.append(value)
      return "#\(value)"
    }
  }

  private func summary(
    path: String = "Journal/review-2026-07.md",
    name: String = "review-2026-07.md",
    modifiedMs: Int64 = 1_700_000_000_000,
    displayName: String? = nil,
    createdDate: String? = nil,
    createdMs: Int64? = nil,
    wordCount: UInt32? = nil,
    preview: String? = nil,
    taskTotal: UInt32 = 0,
    taskOpen: UInt32 = 0
  ) -> FileSummary {
    FileSummary(
      path: path,
      name: name,
      mtimeMs: modifiedMs,
      sizeBytes: 0,
      isMarkdown: true,
      displayName: displayName,
      createdDate: createdDate,
      createdMs: createdMs,
      wordCount: wordCount,
      preview: preview,
      taskTotal: taskTotal,
      taskOpen: taskOpen)
  }

  private func preferences(
    dateSource: SidebarRowPreferencesSnapshot.DateSource = .modified,
    dateFormat: SidebarRowPreferencesSnapshot.DateFormat = .relative,
    previewLines: Int = 0,
    showTaskCounts: Bool = true,
    showWordCount: Bool = false,
    density: SidebarRowPreferencesSnapshot.Density = .standard
  ) -> SidebarRowPreferencesSnapshot {
    SidebarRowPreferencesSnapshot(
      dateSource: dateSource,
      dateFormat: dateFormat,
      previewLines: previewLines,
      showTaskCounts: showTaskCounts,
      showWordCount: showWordCount,
      density: density)
  }

  func testDisplayNameUsesAuthoredTitleAndAccessibilityNamesFilenameOnce() {
    let model = SidebarRowModel(
      summary: summary(displayName: "Weekly review"),
      preferences: .defaults,
      formatter: FormatterSpy())

    XCTAssertEqual(model.displayName, "Weekly review")
    XCTAssertEqual(model.filename, "review-2026-07.md")
    XCTAssertEqual(model.accessibilityLabel, "Weekly review — file review-2026-07.md")
    XCTAssertEqual(model.filenameTooltip, "review-2026-07.md")
  }

  func testUntitledDisplayNameUsesFilenameStemWithoutRedundantAXFilename() {
    let model = SidebarRowModel(
      summary: summary(name: "meeting.notes.md"),
      preferences: .defaults,
      formatter: FormatterSpy())

    XCTAssertEqual(model.displayName, "meeting.notes")
    XCTAssertEqual(model.accessibilityLabel, "meeting.notes")
  }

  func testCreatedCivilDateUsesProductionResolverSeamBeforeLocalizedFormatting() throws {
    let resolved = Date(timeIntervalSince1970: 123_456)
    let resolver = ResolverSpy(result: resolved)
    let formatter = FormatterSpy()
    var calendar = Calendar(identifier: .buddhist)
    calendar.timeZone = try XCTUnwrap(TimeZone(identifier: "Pacific/Auckland"))

    let model = SidebarRowModel(
      summary: summary(createdDate: "2024-07-14", createdMs: 999_000),
      preferences: preferences(dateSource: .created, dateFormat: .absolute),
      calendar: calendar,
      civilDateResolver: resolver,
      formatter: formatter)

    XCTAssertEqual(resolver.calls.map(\.value), ["2024-07-14"])
    XCTAssertEqual(resolver.calls.first?.calendar.timeZone, calendar.timeZone)
    XCTAssertEqual(formatter.absoluteDates, [resolved])
    XCTAssertTrue(formatter.relativeDates.isEmpty)
    XCTAssertEqual(model.metadataText, "Created ABSOLUTE")
  }

  func testCreatedCivilDateWinsOverCreatedInstant() {
    let resolved = Date(timeIntervalSince1970: 321)
    let resolver = ResolverSpy(result: resolved)
    let formatter = FormatterSpy()

    _ = SidebarRowModel(
      summary: summary(createdDate: "2024-01-02", createdMs: 999_000),
      preferences: preferences(dateSource: .created),
      civilDateResolver: resolver,
      formatter: formatter)

    XCTAssertEqual(formatter.relativeDates, [resolved])
  }

  func testCreatedInstantIsLocalizedDirectlyWhenThereIsNoCivilDate() {
    let formatter = FormatterSpy()
    let createdMs: Int64 = 1_710_000_123_456

    let model = SidebarRowModel(
      summary: summary(createdMs: createdMs),
      preferences: preferences(dateSource: .created, dateFormat: .absolute),
      formatter: formatter)

    XCTAssertEqual(
      formatter.absoluteDates,
      [Date(timeIntervalSince1970: TimeInterval(createdMs) / 1_000)])
    XCTAssertEqual(model.metadataText, "Created ABSOLUTE")
  }

  func testMissingCreatedValuesTruthfullyFallBackToModifiedLabel() {
    let formatter = FormatterSpy()
    let model = SidebarRowModel(
      summary: summary(),
      preferences: preferences(dateSource: .created),
      formatter: formatter)

    XCTAssertEqual(model.dateLabel, "Modified")
    XCTAssertEqual(model.metadataText, "Modified RELATIVE")
  }

  func testWordCountUsesLocalizedDecimalAndSingularPlural() {
    let pluralFormatter = FormatterSpy()
    let plural = SidebarRowModel(
      summary: summary(wordCount: 1_240),
      preferences: preferences(showWordCount: true),
      formatter: pluralFormatter)
    XCTAssertEqual(plural.metadataText, "Modified RELATIVE · #1240 words")
    XCTAssertEqual(pluralFormatter.numbers, [1_240])

    let singular = SidebarRowModel(
      summary: summary(wordCount: 1),
      preferences: preferences(showWordCount: true),
      formatter: FormatterSpy())
    XCTAssertEqual(singular.metadataText, "Modified RELATIVE · #1 word")
  }

  func testPreviewClampsPreferenceAndComposesOneAXValue() {
    let model = SidebarRowModel(
      summary: summary(preview: "A useful preview", taskTotal: 5, taskOpen: 3),
      preferences: preferences(previewLines: 99),
      formatter: FormatterSpy())

    XCTAssertEqual(model.previewLineLimit, 3)
    XCTAssertEqual(model.previewText, "A useful preview")
    XCTAssertEqual(model.taskBadgeText, "#3/#5")
    XCTAssertEqual(model.taskAccessibilityText, "3 of 5 tasks open")
    XCTAssertEqual(
      model.accessibilityValue,
      "Modified RELATIVE, Preview: A useful preview, 3 of 5 tasks open")
  }

  func testEmptyPreviewHasNoPlaceholder() {
    for preview in [nil, "", "  \n "] {
      let model = SidebarRowModel(
        summary: summary(preview: preview),
        preferences: preferences(previewLines: 2),
        formatter: FormatterSpy())
      XCTAssertNil(model.previewText)
    }
  }

  func testPreviewIsOptInAndLineCountClampsAtBothBounds() {
    let hidden = SidebarRowModel(
      summary: summary(preview: "Available but not requested"),
      preferences: .defaults,
      formatter: FormatterSpy())
    XCTAssertEqual(hidden.previewLineLimit, 0)
    XCTAssertNil(hidden.previewText)
    XCTAssertFalse(hidden.accessibilityValue.contains("Preview:"))

    let negative = SidebarRowModel(
      summary: summary(preview: "Available"),
      preferences: preferences(previewLines: -1),
      formatter: FormatterSpy())
    XCTAssertEqual(negative.previewLineLimit, 0)
    XCTAssertNil(negative.previewText)
  }

  func testTaskBadgeOpenDoneAndHiddenStates() {
    let open = SidebarRowModel(
      summary: summary(taskTotal: 5, taskOpen: 3),
      preferences: .defaults,
      formatter: FormatterSpy())
    XCTAssertEqual(open.taskBadgeText, "#3/#5")
    XCTAssertFalse(open.taskBadgeIsComplete)
    XCTAssertEqual(open.taskAccessibilityText, "3 of 5 tasks open")

    let done = SidebarRowModel(
      summary: summary(taskTotal: 5, taskOpen: 0),
      preferences: .defaults,
      formatter: FormatterSpy())
    XCTAssertEqual(done.taskBadgeText, "#0/#5")
    XCTAssertTrue(done.taskBadgeIsComplete)
    XCTAssertEqual(done.taskAccessibilityText, "all 5 tasks done")

    for (total, shown) in [(UInt32(0), true), (UInt32(5), false)] {
      let hidden = SidebarRowModel(
        summary: summary(taskTotal: total, taskOpen: 0),
        preferences: preferences(showTaskCounts: shown),
        formatter: FormatterSpy())
      XCTAssertNil(hidden.taskBadgeText)
      XCTAssertNil(hidden.taskAccessibilityText)
    }
  }

  func testCompactIsNameOnlyVisuallyButPreservesFullSpokenValue() {
    let model = SidebarRowModel(
      summary: summary(wordCount: 20, preview: "Preview", taskTotal: 2, taskOpen: 1),
      preferences: preferences(
        previewLines: 2,
        showWordCount: true,
        density: .compact),
      formatter: FormatterSpy())

    XCTAssertFalse(model.visuallyShowsMetadata)
    XCTAssertFalse(model.visuallyShowsPreview)
    XCTAssertFalse(model.visuallyShowsTaskBadge)
    XCTAssertEqual(
      model.accessibilityValue,
      "Modified RELATIVE · #20 words, Preview: Preview, 1 of 2 tasks open")
    XCTAssertGreaterThanOrEqual(SidebarFileRow.compactMinimumHeight, 28)
  }

  func testPinnedRowAppendsPinnedToTheSpokenValueAndShowsTheBadge() {
    let pinned = SidebarRowModel(
      summary: summary(taskTotal: 2, taskOpen: 1),
      preferences: .defaults,
      isPinned: true,
      formatter: FormatterSpy())
    // ", pinned" is the last spoken component (fl3 spec §FL3-2.2) and the
    // glyph renders in standard density.
    XCTAssertEqual(
      pinned.accessibilityValue,
      "Modified RELATIVE, 1 of 2 tasks open, pinned")
    XCTAssertTrue(pinned.isPinned)
    XCTAssertTrue(pinned.visuallyShowsPinBadge)

    let unpinned = SidebarRowModel(
      summary: summary(),
      preferences: .defaults,
      formatter: FormatterSpy())
    XCTAssertFalse(unpinned.isPinned)
    XCTAssertFalse(unpinned.visuallyShowsPinBadge)
    XCTAssertFalse(unpinned.accessibilityValue.contains("pinned"))
  }

  func testPinnedCompactRowStaysVisuallyCompactButSpeaksPinned() {
    let model = SidebarRowModel(
      summary: summary(),
      preferences: preferences(density: .compact),
      isPinned: true,
      formatter: FormatterSpy())
    // Compact hides secondary visuals but never reduces spoken information.
    XCTAssertTrue(model.visuallyShowsPinBadge)
    XCTAssertTrue(model.accessibilityValue.hasSuffix(", pinned"))
  }

  func testKeyboardSelectionAnnouncementUsesTheCompleteCompactRowModel() {
    let model = SidebarRowModel(
      summary: summary(
        displayName: "Weekly review",
        wordCount: 20,
        preview: "Preview",
        taskTotal: 2,
        taskOpen: 1),
      preferences: preferences(
        previewLines: 2,
        showWordCount: true,
        density: .compact),
      formatter: FormatterSpy())

    XCTAssertEqual(
      FileTreeSidebar.selectionAnnouncement(for: model),
      "Selected: Weekly review — file review-2026-07.md. "
        + "Modified RELATIVE · #20 words, Preview: Preview, 1 of 2 tasks open.")
  }

  func testProductionFormatterInstancesAreReusedAcrossLargeProjection() {
    SidebarRowFormatterCache.shared.resetForTesting()
    let summaries = (0..<10_000).map { index in
      summary(
        path: "note-\(index).md",
        name: "note-\(index).md",
        wordCount: UInt32(index),
        taskTotal: 2,
        taskOpen: 1)
    }
    let prefs = preferences(showWordCount: true)

    let elapsed = ContinuousClock().measure {
      for value in summaries {
        _ = SidebarRowModel(summary: value, preferences: prefs)
      }
      let absolute = preferences(dateFormat: .absolute, showWordCount: true)
      for value in summaries {
        _ = SidebarRowModel(summary: value, preferences: absolute)
      }
    }

    // One relative-date, one absolute-date, and one decimal formatter for
    // all 20k projections — never one formatter per row.
    XCTAssertLessThanOrEqual(SidebarRowFormatterCache.shared.creationCountForTesting, 3)
    XCTAssertLessThan(elapsed, .seconds(2))
  }

  func testSharedRowRendersInBothAppearances() {
    let model = SidebarRowModel(
      summary: summary(
        displayName: "Weekly review",
        wordCount: 1_240,
        preview: "A useful preview",
        taskTotal: 5,
        taskOpen: 3),
      preferences: preferences(previewLines: 2, showWordCount: true),
      formatter: FormatterSpy())

    PresentationReady.assertRendersInBothAppearances(
      SidebarFileRow(model: model, depth: 1, isSelected: false))
    PresentationReady.assertRendersInBothAppearances(
      SidebarFileRow(
        model: model,
        depth: 1,
        isSelected: true,
        selectionIsActive: true))
    PresentationReady.assertRendersInBothAppearances(
      SidebarFileRow(
        model: model,
        depth: 1,
        isSelected: true,
        selectionIsActive: false))
  }
}
