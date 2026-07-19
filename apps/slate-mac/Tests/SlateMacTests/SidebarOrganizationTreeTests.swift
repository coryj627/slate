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

  func testLevelsDrainEveryPageBeforeOrganization() async {
    // Round-13 finding 1: the page limit is a page SIZE, not a level cap.
    // With the newest and the pinned file on page two, created/modified
    // sorting and the pinned section must still see them.
    let now = instant(2026, 7, 18)
    let pageOne = DirListing(
      dirs: [],
      files: FileSummaryPage(
        items: [
          file("old-a.md", mtime: 1_000),
          file("old-b.md", mtime: 2_000),
        ],
        nextCursor: "page-2", totalFiltered: 4))
    let pageTwo = DirListing(
      dirs: [],
      files: FileSummaryPage(
        items: [
          file("newest.md", mtime: 9_000),
          file("pinned.md", mtime: 500),
        ],
        nextCursor: nil, totalFiltered: 4))
    let vm = FileTreeViewModel()
    var prefs = SidebarOrganizationPrefs()
    prefs.vaultChoice = SidebarOrganizationChoice(
      sort: SidebarSortOption(field: .modified, direction: .desc), grouping: .none)
    var pins = SidebarPins()
    pins.pin("pinned.md", inFolder: "")
    vm.applyOrganization(context(prefs: prefs, pins: pins, now: now))
    vm.bindForTesting(pagedFetcher: { parent, cursor in
      XCTAssertEqual(parent, "")
      switch cursor {
      case nil: return pageOne
      case "page-2": return pageTwo
      default:
        XCTFail("unexpected cursor \(String(describing: cursor))")
        return pageTwo
      }
    })

    await vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey]?.value
    XCTAssertEqual(
      visiblePaths(vm),
      ["pinned.md", "newest.md", "old-b.md", "old-a.md"],
      "page-two files participate in the pinned section and the sort")
    XCTAssertTrue(vm.isPinnedRow(.file(path: "pinned.md")))
    // The drained level is complete, so stale classification works again.
    XCTAssertEqual(vm.stalePins(forFolder: ""), [])
  }

  func testContinuationDrainMergesOffMainAndStaysPartialUntilItLands() async {
    // Round-14 finding 1: the first page publishes synchronously; the
    // remaining pages drain off the main actor and merge in one publish.
    // Until then the level is partial (no stale classification).
    let now = instant(2026, 7, 18)
    let pageOne = DirListing(
      dirs: [],
      files: FileSummaryPage(
        items: [file("old-a.md", mtime: 1_000)],
        nextCursor: "page-2", totalFiltered: 2))
    let pageTwo = DirListing(
      dirs: [],
      files: FileSummaryPage(
        items: [file("newest.md", mtime: 9_000)],
        nextCursor: nil, totalFiltered: 2))
    let vm = FileTreeViewModel()
    var prefs = SidebarOrganizationPrefs()
    prefs.vaultChoice = SidebarOrganizationChoice(
      sort: SidebarSortOption(field: .modified, direction: .desc), grouping: .none)
    var pins = SidebarPins()
    pins.pin("newest.md", inFolder: "")
    var staleReports: [[String]] = []
    vm.onStalePins = { _, stale in staleReports.append(stale) }
    vm.applyOrganization(context(prefs: prefs, pins: pins, now: now))
    vm.bindForTesting(pagedFetcher: { _, cursor in
      cursor == nil ? pageOne : pageTwo
    })

    // Synchronously: first page only, partial, page-two pin unknowable.
    XCTAssertEqual(visiblePaths(vm), ["old-a.md"])
    XCTAssertEqual(vm.stalePins(forFolder: ""), [])

    await vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey]?.value
    XCTAssertEqual(
      visiblePaths(vm), ["newest.md", "old-a.md"],
      "the drained pages merge in one publish with full organization")
    XCTAssertTrue(vm.isPinnedRow(.file(path: "newest.md")))
    XCTAssertTrue(
      staleReports.allSatisfy { $0.isEmpty },
      "no stale classification fired from the partial window")
  }

  func testVaultSwitchMidDrainDropsTheStaleContinuation() async {
    // Ownership: a rebind before the continuation lands must drop it.
    let now = instant(2026, 7, 18)
    let pageOne = DirListing(
      dirs: [],
      files: FileSummaryPage(
        items: [file("first-vault.md", mtime: 1_000)],
        nextCursor: "page-2", totalFiltered: 2))
    let pageTwo = DirListing(
      dirs: [],
      files: FileSummaryPage(
        items: [file("first-vault-late.md", mtime: 9_000)],
        nextCursor: nil, totalFiltered: 2))
    let vm = FileTreeViewModel()
    vm.applyOrganization(context(now: now))
    vm.bindForTesting(pagedFetcher: { _, cursor in
      cursor == nil ? pageOne : pageTwo
    })
    let staleDrain = vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey]

    // Rebind to a different (single-page) vault before the drain lands.
    vm.bindForTesting(fetcher: { _ in
      DirListing(
        dirs: [],
        files: FileSummaryPage(
          items: [self.file("second-vault.md")], nextCursor: nil,
          totalFiltered: 1))
    })
    await staleDrain?.value
    XCTAssertEqual(
      visiblePaths(vm), ["second-vault.md"],
      "the first vault's late continuation never publishes into the new bind")
  }

  func testAuthoritativeInvalidationRetiresAnInFlightDrain() async {
    // Round-15 finding 1: a whole-tree invalidation while a continuation is
    // in flight must retire it — the old drain never publishes over the
    // reloaded content, even in the same session.
    final class PagesBox: @unchecked Sendable {
      private let lock = NSLock()
      private var singlePage = false
      func switchToSinglePage() {
        lock.lock()
        singlePage = true
        lock.unlock()
      }
      var isSinglePage: Bool {
        lock.lock()
        defer { lock.unlock() }
        return singlePage
      }
    }
    let box = PagesBox()
    let now = instant(2026, 7, 18)
    let vm = FileTreeViewModel()
    vm.applyOrganization(context(now: now))
    vm.bindForTesting(pagedFetcher: { [self] _, cursor in
      if box.isSinglePage {
        return DirListing(
          dirs: [],
          files: FileSummaryPage(
            items: [file("fresh.md")], nextCursor: nil, totalFiltered: 1))
      }
      if cursor == nil {
        return DirListing(
          dirs: [],
          files: FileSummaryPage(
            items: [file("stale-a.md")], nextCursor: "page-2",
            totalFiltered: 2))
      }
      return DirListing(
        dirs: [],
        files: FileSummaryPage(
          items: [file("stale-late.md")], nextCursor: nil, totalFiltered: 2))
    })
    let staleDrain = vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey]

    // A rescan-style invalidation reloads the root from the new fixture.
    box.switchToSinglePage()
    vm.authoritativeTreeInvalidation()
    await staleDrain?.value

    XCTAssertEqual(
      visiblePaths(vm), ["fresh.md"],
      "the pre-invalidation continuation must never publish stale pages")
  }

  func testContinuationFailureSurfacesInlineErrorAndRetryRefetches() async {
    // Round-15 finding 2: a page-two failure keeps the first page, surfaces
    // the existing inline error + Retry, and Retry refetches the level.
    final class FailBox: @unchecked Sendable {
      private let lock = NSLock()
      private var failing = true
      func heal() {
        lock.lock()
        failing = false
        lock.unlock()
      }
      var isFailing: Bool {
        lock.lock()
        defer { lock.unlock() }
        return failing
      }
    }
    let box = FailBox()
    let now = instant(2026, 7, 18)
    let vm = FileTreeViewModel()
    vm.applyOrganization(context(now: now))
    vm.bindForTesting(pagedFetcher: { [self] _, cursor in
      if cursor == nil {
        return DirListing(
          dirs: [],
          files: FileSummaryPage(
            items: [file("page-one.md")], nextCursor: "page-2",
            totalFiltered: 2))
      }
      if box.isFailing {
        throw VaultError.Db(message: "page two boom")
      }
      return DirListing(
        dirs: [],
        files: FileSummaryPage(
          items: [file("page-two.md")], nextCursor: nil, totalFiltered: 2))
    })
    await vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey]?.value

    XCTAssertEqual(
      visiblePaths(vm), ["page-one.md"],
      "the published first page survives the continuation failure")
    guard case .failed(let message)? = vm.fetchState[FileTreeViewModel.rootFetchKey]
    else {
      return XCTFail("a failed continuation must surface the inline error")
    }
    XCTAssertTrue(message.contains("page two boom"))

    // Retry (the root error row calls loadRoot) refetches and completes.
    box.heal()
    vm.loadRoot()
    await vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey]?.value
    XCTAssertEqual(visiblePaths(vm), ["page-one.md", "page-two.md"])
    XCTAssertNil(vm.fetchState[FileTreeViewModel.rootFetchKey])
  }

  func testSaveForAnUnlandedLaterPageFileSurvivesTheDrain() async {
    // Round-19 finding 2: a save publishing a newer summary for a page-two
    // file BEFORE its page lands must overlay the stale page snapshot.
    final class GateBox: @unchecked Sendable {
      private let semaphore = DispatchSemaphore(value: 0)
      func wait() { semaphore.wait() }
      func open() { semaphore.signal() }
    }
    let gate = GateBox()
    let now = instant(2026, 7, 18)
    let vm = FileTreeViewModel()
    var prefs = SidebarOrganizationPrefs()
    prefs.vaultChoice = SidebarOrganizationChoice(
      sort: SidebarSortOption(field: .modified, direction: .desc), grouping: .none)
    vm.applyOrganization(context(prefs: prefs, now: now))
    vm.bindForTesting(pagedFetcher: { [self] _, cursor in
      if cursor == nil {
        return DirListing(
          dirs: [],
          files: FileSummaryPage(
            items: [file("first.md", mtime: 5_000)],
            nextCursor: "page-2", totalFiltered: 2))
      }
      // Hold page two until the newer save has been published.
      gate.wait()
      return DirListing(
        dirs: [],
        files: FileSummaryPage(
          items: [file("late.md", mtime: 1_000)],
          nextCursor: nil, totalFiltered: 2))
    })

    // The save lands while page two is still being fetched.
    vm.replaceFileSummaries([
      file("late.md", mtime: 9_000, displayName: "Newest")
    ])
    gate.open()
    await vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey]?.value

    XCTAssertEqual(
      vm.fileSummary(forPath: "late.md")?.displayName, "Newest",
      "the buffered newest summary overlays the stale page snapshot")
    XCTAssertEqual(vm.fileSummary(forPath: "late.md")?.mtimeMs, 9_000)
    XCTAssertEqual(
      visiblePaths(vm), ["late.md", "first.md"],
      "the merged order reflects the buffered mtime")
  }

  func testRevokedDrainsBufferNeverLeaksIntoAReplacementDrain() async {
    // Round-20 finding 2: a summary buffered under a revoked drain's token
    // must not be consumed by the replacement drain, whose refetched pages
    // already reflect (or supersede) that save.
    final class GateBox: @unchecked Sendable {
      private let lock = NSLock()
      private let first = DispatchSemaphore(value: 0)
      private var replaced = false
      func holdFirst() { first.wait() }
      func releaseFirst() { first.signal() }
      func markReplaced() {
        lock.lock()
        replaced = true
        lock.unlock()
      }
      var isReplaced: Bool {
        lock.lock()
        defer { lock.unlock() }
        return replaced
      }
    }
    let box = GateBox()
    let now = instant(2026, 7, 18)
    let vm = FileTreeViewModel()
    var prefs = SidebarOrganizationPrefs()
    prefs.vaultChoice = SidebarOrganizationChoice(
      sort: SidebarSortOption(field: .modified, direction: .desc), grouping: .none)
    vm.applyOrganization(context(prefs: prefs, now: now))
    vm.bindForTesting(pagedFetcher: { [self] _, cursor in
      if cursor == nil {
        return DirListing(
          dirs: [],
          files: FileSummaryPage(
            items: [file("first.md", mtime: 5_000)],
            nextCursor: "page-2", totalFiltered: 2))
      }
      if !box.isReplaced {
        // The FIRST drain's page two blocks until after revocation.
        box.holdFirst()
        return DirListing(
          dirs: [],
          files: FileSummaryPage(
            items: [file("late.md", mtime: 1_000)],
            nextCursor: nil, totalFiltered: 2))
      }
      // The REPLACEMENT drain's page two carries the newest truth.
      return DirListing(
        dirs: [],
        files: FileSummaryPage(
          items: [file("late.md", mtime: 20_000, displayName: "Replacement Truth")],
          nextCursor: nil, totalFiltered: 2))
    })
    let firstDrain = vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey]

    // A save buffers under the FIRST drain's token…
    vm.replaceFileSummaries([
      file("late.md", mtime: 9_000, displayName: "Stale Buffer")
    ])
    // …then the level reloads (fetch-start revocation) with newer pages.
    box.markReplaced()
    vm.loadRoot()
    let replacementDrain = vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey]
    box.releaseFirst()
    await firstDrain?.value
    await replacementDrain?.value

    XCTAssertEqual(
      vm.fileSummary(forPath: "late.md")?.displayName, "Replacement Truth",
      "the replacement drain's page wins; the revoked drain's buffer never leaks")
    XCTAssertEqual(vm.fileSummary(forPath: "late.md")?.mtimeMs, 20_000)
  }

  func testPartialLevelsNeverClassifyPinsAsStale() {
    // Round-5 finding 2: a paginated listing omits real files; a pinned
    // file on a later page must not be offered for pruning.
    let now = instant(2026, 7, 18)
    let partial = DirListing(
      dirs: [],
      files: FileSummaryPage(
        items: [file("visible.md")], nextCursor: "page-2", totalFiltered: 2))
    let spy = FetchSpy(["": partial])
    let vm = FileTreeViewModel()
    var pins = SidebarPins()
    pins.pin("beyond-the-page.md", inFolder: "")
    pins.pin("visible.md", inFolder: "")
    var reported: [(String, [String])] = []
    vm.onStalePins = { folder, stale in reported.append((folder, stale)) }
    vm.applyOrganization(context(pins: pins, now: now))
    vm.bindForTesting(fetcher: spy.fetch)

    XCTAssertEqual(vm.stalePins(forFolder: ""), [])
    XCTAssertTrue(reported.isEmpty, "no prune offer from a partial level")
    // The materialized pinned row still renders pinned.
    XCTAssertTrue(vm.isPinnedRow(.file(path: "visible.md")))

    // A later organization pass over the cached partial level stays silent
    // too.
    var updated = pins
    updated.pin("another.md", inFolder: "")
    vm.applyOrganization(context(pins: updated, now: now))
    XCTAssertEqual(vm.stalePins(forFolder: ""), [])
    XCTAssertTrue(reported.isEmpty)
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

  // MARK: - Select-after-mutate stays path-based

  func testPostMutationFocusResolvesByPathUnderAnyActiveSort() {
    // The U2-6 re-find seam locates rows by stable path, never index. Under
    // a date sort that moves rows around, the path lookup must still resolve
    // the same node (fl3 spec §FL3-1.5's easy regression).
    let now = instant(2026, 7, 18)
    let spy = FetchSpy([
      "": listing(files: [
        file("a.md", mtime: 1_000),
        file("b.md", mtime: 9_000),
        file("c.md", mtime: 5_000),
      ])
    ])
    let vm = FileTreeViewModel()
    var prefs = SidebarOrganizationPrefs()
    prefs.vaultChoice = SidebarOrganizationChoice(
      sort: SidebarSortOption(field: .modified, direction: .desc), grouping: .none)
    vm.applyOrganization(context(prefs: prefs, now: now))
    vm.bindForTesting(fetcher: spy.fetch)
    XCTAssertEqual(visiblePaths(vm), ["b.md", "c.md", "a.md"])

    // The row's identity and lookup are path-stable at every position.
    XCTAssertEqual(vm.focusTarget(forPath: "a.md"), .file(path: "a.md"))
    vm.applyOrganization(context(now: now))  // back to name ascending
    XCTAssertEqual(visiblePaths(vm), ["a.md", "b.md", "c.md"])
    XCTAssertEqual(vm.focusTarget(forPath: "a.md"), .file(path: "a.md"))
  }

  // MARK: - Header rendering statics

  func testHeaderAccessibilityValueUsesSingularAndPluralNoteCounts() {
    let one = SidebarTreeHeaderRow(
      kind: .group, key: "today", label: "Today", fileCount: 1, depth: 0)
    let many = SidebarTreeHeaderRow(
      kind: .pinned, key: "pinned", label: "Pinned", fileCount: 3, depth: 1)
    XCTAssertEqual(FileTreeSidebar.headerAccessibilityValue(for: one), "1 note")
    XCTAssertEqual(FileTreeSidebar.headerAccessibilityValue(for: many), "3 notes")
  }

  func testTreeAccessibilitySummaryMentionsOnlyNonDefaultOrganization() {
    XCTAssertNil(FileTreeSidebar.treeAccessibilitySummary(for: .defaults))
    XCTAssertEqual(
      FileTreeSidebar.treeAccessibilitySummary(
        for: SidebarOrganizationChoice(
          sort: SidebarSortOption(field: .modified, direction: .desc),
          grouping: .none)),
      "Files. Sorted by modified, newest first.")
    XCTAssertEqual(
      FileTreeSidebar.treeAccessibilitySummary(
        for: SidebarOrganizationChoice(
          sort: SidebarSortOption(field: .created, direction: .desc),
          grouping: .dateBuckets)),
      "Files. Sorted by created, newest first, grouped by date.")
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

  // MARK: - FL3-4.1 collapse/expand against the live model (FL-07 review)

  func testCollapseAllPreservingAncestorsAgainstTheLiveTree() {
    let spy = FetchSpy([
      "": listing(dirs: [dir(1, "A"), dir(2, "C")], files: []),
      "A": listing(dirs: [dir(3, "A/B")], files: [file("A/top.md")]),
      "A/B": listing(dirs: [], files: [file("A/B/note.md")]),
      "C": listing(dirs: [], files: [file("C/c.md")]),
    ])
    let vm = FileTreeViewModel()
    vm.bindForTesting(fetcher: spy.fetch)
    for node in vm.rootLevel where node.isDirectory {
      vm.expand(node)
    }
    if let inner = vm.children.values.flatMap({ $0 }).first(where: {
      $0.path == "A/B"
    }) {
      vm.expand(inner)
    }
    XCTAssertTrue(visiblePaths(vm).contains("A/B/note.md"))
    XCTAssertTrue(visiblePaths(vm).contains("C/c.md"))

    vm.collapseAllPreservingAncestors(ofPath: "A/B/note.md")

    let visible = visiblePaths(vm)
    XCTAssertTrue(
      visible.contains("A/B"),
      "the selection's ancestor chain stays expanded")
    XCTAssertTrue(visible.contains("A/B/note.md"))
    XCTAssertFalse(
      visible.contains("C/c.md"),
      "unrelated folders collapse")
  }

  func testExpandLoadedFetchesAtMostOneLevelDeeper() {
    let spy = FetchSpy([
      "": listing(dirs: [dir(1, "A")], files: []),
      "A": listing(dirs: [dir(2, "A/B")], files: []),
      "A/B": listing(dirs: [dir(3, "A/B/C")], files: []),
      "A/B/C": listing(dirs: [], files: [file("A/B/C/deep.md")]),
    ])
    let vm = FileTreeViewModel()
    vm.bindForTesting(fetcher: spy.fetch)
    // Only the root is loaded; expand-loaded expands A (fetching A's
    // level) but must NOT cascade into A/B's own expansion.
    vm.expandLoadedLevels()
    let visible = visiblePaths(vm)
    XCTAssertTrue(visible.contains("A/B"), "one level deeper materializes")
    XCTAssertFalse(
      visible.contains("A/B/C"),
      "newly fetched levels' folders stay unexpanded — no cascade")
  }
}
