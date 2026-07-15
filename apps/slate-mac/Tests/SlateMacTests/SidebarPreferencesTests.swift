// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

final class SidebarPreferencesTests: XCTestCase {
  private var defaults: UserDefaults!
  private var suiteName: String!

  override func setUp() {
    super.setUp()
    suiteName = "slate.sidebar-prefs.tests.\(UUID().uuidString)"
    defaults = UserDefaults(suiteName: suiteName)
  }

  override func tearDown() {
    defaults.removePersistentDomain(forName: suiteName)
    defaults = nil
    suiteName = nil
    super.tearDown()
  }

  func testDefaultsPreserveTheShippedTwoLineRow() {
    XCTAssertEqual(
      SidebarPreferences(),
      SidebarPreferences(
        dateSource: .modified,
        dateFormat: .relative,
        previewLines: 0,
        showTaskCounts: true,
        showWordCount: false,
        density: .standard
      ))
  }

  func testStableUserDefaultsKeysMatchTheExecutableSpec() {
    XCTAssertEqual(SidebarPreferences.Keys.dateSource, "sidebar.dateSource")
    XCTAssertEqual(SidebarPreferences.Keys.dateFormat, "sidebar.dateFormat")
    XCTAssertEqual(SidebarPreferences.Keys.previewLines, "sidebar.previewLines")
    XCTAssertEqual(SidebarPreferences.Keys.showTaskCounts, "sidebar.showTaskCounts")
    XCTAssertEqual(SidebarPreferences.Keys.showWordCount, "sidebar.showWordCount")
    XCTAssertEqual(SidebarPreferences.Keys.density, "sidebar.density")
  }

  func testTypedPreferencesRoundTripThroughUserDefaults() {
    let store = PreferencesStore(defaults: defaults)
    let expected = SidebarPreferences(
      dateSource: .created,
      dateFormat: .absolute,
      previewLines: 3,
      showTaskCounts: false,
      showWordCount: true,
      density: .compact
    )

    store.saveSidebarPreferences(expected)

    XCTAssertEqual(PreferencesStore(defaults: defaults).loadSidebarPreferences(), expected)
  }

  func testAbsentAndInvalidValuesFallBackWhileNumericPreviewClamps() {
    let store = PreferencesStore(defaults: defaults)
    XCTAssertEqual(store.loadSidebarPreferences(), SidebarPreferences())

    defaults.set("future", forKey: SidebarPreferences.Keys.dateSource)
    defaults.set("future", forKey: SidebarPreferences.Keys.dateFormat)
    defaults.set("future", forKey: SidebarPreferences.Keys.density)
    defaults.set(99, forKey: SidebarPreferences.Keys.previewLines)
    defaults.set("not a bool", forKey: SidebarPreferences.Keys.showTaskCounts)
    defaults.set("not a bool", forKey: SidebarPreferences.Keys.showWordCount)

    var expected = SidebarPreferences()
    expected.previewLines = 3
    XCTAssertEqual(store.loadSidebarPreferences(), expected)

    defaults.set(-10, forKey: SidebarPreferences.Keys.previewLines)
    XCTAssertEqual(store.loadSidebarPreferences().previewLines, 0)
  }

  func testPrimitiveTypeDriftDoesNotChangeSidebarPreferences() {
    let store = PreferencesStore(defaults: defaults)
    defaults.set(true, forKey: SidebarPreferences.Keys.previewLines)
    defaults.set(0, forKey: SidebarPreferences.Keys.showTaskCounts)
    defaults.set(1, forKey: SidebarPreferences.Keys.showWordCount)

    XCTAssertEqual(
      store.loadSidebarPreferences(),
      SidebarPreferences(),
      "CFBoolean and numeric values must not bridge into a different preference type")
  }

  @MainActor
  func testAppStateLoadsAndPersistsPublishedSidebarPreferences() {
    let store = PreferencesStore(defaults: defaults)
    var seeded = SidebarPreferences()
    seeded.dateSource = .created
    seeded.previewLines = 2
    store.saveSidebarPreferences(seeded)

    let recentsURL = FileManager.default.temporaryDirectory
      .appendingPathComponent("\(suiteName!).recents.json")
    let appState = AppState(
      recentsStore: RecentVaultsStore(fileURL: recentsURL),
      externalOpener: { _ in true },
      preferencesStore: store
    )
    XCTAssertEqual(appState.sidebarPreferences, seeded)

    appState.sidebarPreferences.showWordCount = true
    XCTAssertTrue(
      PreferencesStore(defaults: defaults).loadSidebarPreferences().showWordCount)
  }

  func testSettingsViewContainsOneNativeSidebarPaneWithEveryControl() throws {
    let source = try settingsSource()
    XCTAssertTrue(source.contains("struct SidebarSettingsTab: View"))
    XCTAssertEqual(source.components(separatedBy: "SidebarSettingsTab()").count - 1, 1)
    for label in [
      "Date source", "Date format", "Preview lines", "Show task counts",
      "Show word count", "Density",
    ] {
      XCTAssertTrue(source.contains("\"\(label)\""), "missing Settings control: \(label)")
    }
    XCTAssertTrue(source.contains(".formStyle(.grouped)"))
  }

  private func settingsSource() throws -> String {
    var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
    for _ in 0..<8 {
      let candidate = cursor.appendingPathComponent("Sources/SlateMac/SettingsView.swift")
      if FileManager.default.fileExists(atPath: candidate.path) {
        return try String(contentsOf: candidate, encoding: .utf8)
      }
      cursor = cursor.deletingLastPathComponent()
    }
    throw XCTSkip("SettingsView.swift not found relative to the test file")
  }
}
