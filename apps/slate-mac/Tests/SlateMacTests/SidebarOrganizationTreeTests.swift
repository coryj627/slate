// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// FL-06 view-model integration: per-level organization applied at level
/// store time, re-sorts on preference/pin/day changes, header/pin lookups
/// for the rendered rows, and metadata-driven reorder confined to the
/// affected level. Uses the same injected-fetcher seam as
/// `FileTreeSidebarTests`; no FFI, no wall clock.
@MainActor
final class SidebarOrganizationTreeTests: XCTestCase {

  // MARK: - Fixtures

  private func dir(
    _ id: Int64, _ path: String, dirCount: Int = 0, fileCount: Int = 0
  ) -> DirNodeSummary {
    DirNodeSummary(
      id: id, path: path, name: (path as NSString).lastPathComponent,
      childDirCount: UInt32(dirCount), childFileCount: UInt32(fileCount))
  }

  private func file(
    _ path: String,
    mtime: Int64 = 0,
    displayName: String? = nil,
    createdDate: String? = nil,
    createdMs: Int64? = nil,
    wordCount: UInt32? = nil
  ) -> FileSummary {
    FileSummary(
      path: path, name: (path as NSString).lastPathComponent, mtimeMs: mtime,
      sizeBytes: 0, isMarkdown: true, displayName: displayName,
      createdDate: createdDate, createdMs: createdMs, wordCount: wordCount,
      preview: nil, taskTotal: 0, taskOpen: 0)
  }

  private func listing(dirs: [DirNodeSummary] = [], files: [FileSummary] = []) -> DirListing {
    DirListing(
      dirs: dirs,
      files: FileSummaryPage(
        items: files, nextCursor: nil, totalFiltered: UInt64(files.count)))
  }

  private final class FetchSpy {
    var calls: [String] = []
    let table: [String: DirListing]

    init(_ table: [String: DirListing]) {
      self.table = table
    }

    func fetch(_ parentPath: String) throws -> DirListing {
      calls.append(parentPath)
      return table[parentPath]
        ?? DirListing(
          dirs: [], files: FileSummaryPage(items: [], nextCursor: nil, totalFiltered: 0))
    }
  }

  private func utc() -> Calendar {
    var calendar = Calendar(identifier: .gregorian)
    calendar.timeZone = TimeZone(identifier: "UTC")!
    return calendar
  }

  private func instant(
    _ year: Int, _ month: Int, _ day: Int, _ hour: Int = 12
  ) -> Date {
    var components = DateComponents()
    components.year = year
    components.month = month
    components.day = day
    components.hour = hour
    return utc().date(from: components)!
  }

  private func milliseconds(_ date: Date) -> Int64 {
    Int64((date.timeIntervalSince1970 * 1_000).rounded())
  }

  private func context(
    prefs: SidebarOrganizationPrefs = SidebarOrganizationPrefs(),
    pins: SidebarPins = SidebarPins(),
    now: Date
  ) -> FileTreeViewModel.OrganizationContext {
    FileTreeViewModel.OrganizationContext(
      prefs: prefs,
      pins: pins,
      now: now,
      calendar: utc(),
      locale: Locale(identifier: "en_US"))
  }

  private func visiblePaths(_ vm: FileTreeViewModel) -> [String] {
    vm.visibleRows.map(\.path)
  }

  // MARK: - Sorting at store and on preference change

  func testFilesSortedPerVaultChoiceKeepingDirsFirstInBackendOrder() {
    let now = instant(2026, 7, 18)
    let spy = FetchSpy([
      "": listing(
        dirs: [dir(1, "alpha"), dir(2, "beta")],
        files: [
          file("old.md", mtime: 1_000),
          file("new.md", mtime: 9_000),
          file("mid.md", mtime: 5_000),
        ])
    ])
    let vm = FileTreeViewModel()
    var prefs = SidebarOrganizationPrefs()
    prefs.vaultChoice = SidebarOrganizationChoice(
      sort: SidebarSortOption(field: .modified, direction: .desc), grouping: .none)
    vm.applyOrganization(context(prefs: prefs, now: now))
    vm.bindForTesting(fetcher: spy.fetch)

    XCTAssertEqual(
      visiblePaths(vm), ["alpha", "beta", "new.md", "mid.md", "old.md"])

    // Switching back to the default name sort re-sorts the cached level
    // without a refetch.
    vm.applyOrganization(context(now: now))
    XCTAssertEqual(
      visiblePaths(vm), ["alpha", "beta", "mid.md", "new.md", "old.md"])
    XCTAssertEqual(spy.calls, [""])
  }

  func testPerFolderOverrideAppliesToThatLevelOnly() {
    let now = instant(2026, 7, 18)
    let spy = FetchSpy([
      "": listing(
        dirs: [dir(1, "proj", fileCount: 2)],
        files: [file("b.md", mtime: 9_000), file("a.md", mtime: 1_000)]),
      "proj": listing(
        files: [file("proj/x.md", mtime: 1_000), file("proj/y.md", mtime: 9_000)]),
    ])
    let vm = FileTreeViewModel()
    var prefs = SidebarOrganizationPrefs()  // vault default: name asc
    prefs.folderOverrides["proj"] = SidebarOrganizationOverride(
      sort: SidebarSortOption(field: .modified, direction: .desc), grouping: nil)
    vm.applyOrganization(context(prefs: prefs, now: now))
    vm.bindForTesting(fetcher: spy.fetch)
    vm.expand(vm.rootLevel[0])

    // Root keeps name-ascending; proj's fetched level honors its override.
    XCTAssertEqual(
      visiblePaths(vm), ["proj", "proj/y.md", "proj/x.md", "a.md", "b.md"])
  }

  // MARK: - Headers and pins

  func testGroupingProducesHeadersAndPinnedSectionFirst() {
    let now = instant(2026, 7, 18)
    let today = milliseconds(instant(2026, 7, 18, 9))
    let yesterday = milliseconds(instant(2026, 7, 17, 9))
    let spy = FetchSpy([
      "": listing(files: [
        file("a.md", mtime: today),
        file("b.md", mtime: yesterday),
        file("c.md", mtime: yesterday),
      ])
    ])
    let vm = FileTreeViewModel()
    var prefs = SidebarOrganizationPrefs()
    prefs.vaultChoice = SidebarOrganizationChoice(
      sort: SidebarSortOption(field: .modified, direction: .desc),
      grouping: .dateBuckets)
    var pins = SidebarPins()
    pins.pin("c.md", inFolder: "")
    vm.applyOrganization(context(prefs: prefs, pins: pins, now: now))
    vm.bindForTesting(fetcher: spy.fetch)

    XCTAssertEqual(visiblePaths(vm), ["c.md", "a.md", "b.md"])
    XCTAssertTrue(vm.isPinnedRow(.file(path: "c.md")))
    XCTAssertFalse(vm.isPinnedRow(.file(path: "a.md")))

    let pinnedHeader = vm.headerRow(before: .file(path: "c.md"))
    XCTAssertEqual(pinnedHeader?.kind, .pinned)
    XCTAssertEqual(pinnedHeader?.fileCount, 1)
    XCTAssertEqual(pinnedHeader?.depth, 0)

    let todayHeader = vm.headerRow(before: .file(path: "a.md"))
    XCTAssertEqual(todayHeader?.kind, .group)
    XCTAssertEqual(todayHeader?.label, "Today")
    XCTAssertEqual(todayHeader?.fileCount, 1)

    let yesterdayHeader = vm.headerRow(before: .file(path: "b.md"))
    XCTAssertEqual(yesterdayHeader?.kind, .group)
    XCTAssertEqual(yesterdayHeader?.label, "Yesterday")
    XCTAssertEqual(yesterdayHeader?.fileCount, 1)

    // No grouping, no pins → no headers anywhere.
    vm.applyOrganization(context(now: now))
    XCTAssertNil(vm.headerRow(before: .file(path: "a.md")))
    XCTAssertFalse(vm.isPinnedRow(.file(path: "c.md")))
  }

  func testStalePinsSurfacedPerFolderForLazyPrune() {
    let now = instant(2026, 7, 18)
    let spy = FetchSpy([
      "": listing(files: [file("real.md")])
    ])
    let vm = FileTreeViewModel()
    var pins = SidebarPins()
    pins.pin("ghost.md", inFolder: "")
    pins.pin("real.md", inFolder: "")
    vm.applyOrganization(context(pins: pins, now: now))
    vm.bindForTesting(fetcher: spy.fetch)

    XCTAssertEqual(vm.stalePins(forFolder: ""), ["ghost.md"])
    XCTAssertTrue(vm.isPinnedRow(.file(path: "real.md")))
  }

  // MARK: - Metadata-driven reorder

  func testSummaryReplacementReordersOnlyWhenActiveSortKeyChanges() {
    let now = instant(2026, 7, 18)
    let spy = FetchSpy([
      "": listing(files: [
        file("a.md", mtime: 9_000),
        file("b.md", mtime: 5_000),
        file("c.md", mtime: 1_000),
      ])
    ])
    let vm = FileTreeViewModel()
    var prefs = SidebarOrganizationPrefs()
    prefs.vaultChoice = SidebarOrganizationChoice(
      sort: SidebarSortOption(field: .modified, direction: .desc), grouping: .none)
    vm.applyOrganization(context(prefs: prefs, now: now))
    vm.bindForTesting(fetcher: spy.fetch)
    XCTAssertEqual(visiblePaths(vm), ["a.md", "b.md", "c.md"])
    let baseline = vm.levelReorganizeCountForTesting

    // A save bumps c.md to the newest mtime: it must float to the top.
    vm.replaceFileSummaries([file("c.md", mtime: 99_000)])
    XCTAssertEqual(visiblePaths(vm), ["c.md", "a.md", "b.md"])
    XCTAssertEqual(vm.levelReorganizeCountForTesting, baseline + 1)

    // A metadata change that cannot affect the active sort (word count)
    // must not re-sort anything.
    vm.replaceFileSummaries([file("a.md", mtime: 9_000, wordCount: 42)])
    XCTAssertEqual(vm.levelReorganizeCountForTesting, baseline + 1)
    XCTAssertEqual(visiblePaths(vm), ["c.md", "a.md", "b.md"])
  }

  func testDisplayNameChangeReordersUnderNameSort() {
    let now = instant(2026, 7, 18)
    let spy = FetchSpy([
      "": listing(files: [file("alpha.md"), file("zulu.md")])
    ])
    let vm = FileTreeViewModel()
    vm.applyOrganization(context(now: now))  // default name asc
    vm.bindForTesting(fetcher: spy.fetch)
    XCTAssertEqual(visiblePaths(vm), ["alpha.md", "zulu.md"])

    // Authoring a title that sorts first moves the row without a refetch.
    vm.replaceFileSummaries([file("zulu.md", displayName: "AAA first")])
    XCTAssertEqual(visiblePaths(vm), ["zulu.md", "alpha.md"])
    XCTAssertEqual(spy.calls, [""])
  }

  // MARK: - Day rollover

  func testSameDayTickDoesNotReorganizeButRolloverDoes() {
    let now = instant(2026, 7, 18, 9)
    let spy = FetchSpy([
      "": listing(files: [
        file("a.md", mtime: milliseconds(instant(2026, 7, 18, 8)))
      ])
    ])
    let vm = FileTreeViewModel()
    var prefs = SidebarOrganizationPrefs()
    prefs.vaultChoice = SidebarOrganizationChoice(
      sort: SidebarSortOption(field: .modified, direction: .desc),
      grouping: .dateBuckets)
    vm.applyOrganization(context(prefs: prefs, now: now))
    vm.bindForTesting(fetcher: spy.fetch)
    XCTAssertEqual(vm.headerRow(before: .file(path: "a.md"))?.label, "Today")
    let baseline = vm.levelReorganizeCountForTesting

    // A same-day clock tick (the relative-date refresh) is a no-op.
    vm.applyOrganization(context(prefs: prefs, now: instant(2026, 7, 18, 10)))
    XCTAssertEqual(vm.levelReorganizeCountForTesting, baseline)

    // Crossing local midnight re-buckets: the note is now "Yesterday".
    vm.applyOrganization(context(prefs: prefs, now: instant(2026, 7, 19, 0)))
    XCTAssertGreaterThan(vm.levelReorganizeCountForTesting, baseline)
    XCTAssertEqual(vm.headerRow(before: .file(path: "a.md"))?.label, "Yesterday")
  }
}
