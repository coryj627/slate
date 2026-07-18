// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// FL3-1 (#658): total-order comparators, per-folder override precedence, and
/// date-bucket grouping for the files sidebar. Everything here drives the pure
/// `SidebarLevelOrganizer` / `SidebarOrganizationSchema` engine with injected
/// clocks, calendars, and resolvers — no wall time, no FFI, no MainActor.
final class SidebarOrganizationTests: XCTestCase {

  // MARK: - Fixtures

  private func file(
    path: String,
    name: String? = nil,
    displayName: String? = nil,
    createdDate: String? = nil,
    createdMs: Int64? = nil,
    mtimeMs: Int64 = 0
  ) -> SidebarOrganizerFile {
    SidebarOrganizerFile(
      path: path,
      name: name ?? (path as NSString).lastPathComponent,
      displayName: displayName,
      createdDate: createdDate,
      createdMs: createdMs,
      mtimeMs: mtimeMs)
  }

  private func gregorian(_ timeZone: String) -> Calendar {
    var calendar = Calendar(identifier: .gregorian)
    calendar.timeZone = TimeZone(identifier: timeZone)!
    return calendar
  }

  private func calendar(
    _ identifier: Calendar.Identifier,
    timeZone: String
  ) -> Calendar {
    var calendar = Calendar(identifier: identifier)
    calendar.timeZone = TimeZone(identifier: timeZone)!
    return calendar
  }

  /// Absolute instant for a Gregorian civil datetime in a zone. Tests build
  /// every boundary instant explicitly; no test reads the wall clock.
  private func instant(
    _ year: Int, _ month: Int, _ day: Int,
    _ hour: Int = 0, _ minute: Int = 0, _ second: Int = 0,
    timeZone: String
  ) -> Date {
    var components = DateComponents()
    components.year = year
    components.month = month
    components.day = day
    components.hour = hour
    components.minute = minute
    components.second = second
    var calendar = Calendar(identifier: .gregorian)
    calendar.timeZone = TimeZone(identifier: timeZone)!
    return calendar.date(from: components)!
  }

  private func milliseconds(_ date: Date) -> Int64 {
    Int64((date.timeIntervalSince1970 * 1_000).rounded())
  }

  private func organize(
    _ files: [SidebarOrganizerFile],
    choice: SidebarOrganizationChoice,
    pins: [String] = [],
    now: Date,
    calendar: Calendar,
    resolver: any SidebarCivilDateResolving = SidebarOrganizationTests.productionResolver
  ) -> SidebarOrganizedLevel {
    SidebarLevelOrganizer.organize(
      files: files,
      choice: choice,
      pinnedPaths: pins,
      now: now,
      calendar: calendar,
      civilDateResolver: resolver)
  }

  private static let productionResolver = ProductionCivilDateResolver()

  private struct ProductionCivilDateResolver: SidebarCivilDateResolving {
    func resolve(_ canonicalDate: String, calendar: Calendar) -> Date? {
      SidebarCivilDateResolver.resolve(canonicalDate, calendar: calendar)
    }
  }

  /// Counts calls and can lie about the resolved instant, proving the sort
  /// consumes the resolver's `Date` instead of reparsing ISO components.
  private final class SpyResolver: SidebarCivilDateResolving {
    var resolved: [String] = []
    var override: [String: Date] = [:]

    func resolve(_ canonicalDate: String, calendar: Calendar) -> Date? {
      resolved.append(canonicalDate)
      if let date = override[canonicalDate] { return date }
      return SidebarCivilDateResolver.resolve(canonicalDate, calendar: calendar)
    }
  }

  private func paths(_ level: SidebarOrganizedLevel) -> [String] {
    level.orderedPaths
  }

  // MARK: - Name sort

  func testNameSortUsesEffectiveLabelCaseInsensitiveWithNFCKeys() {
    // Décomposed "é" (e + combining acute) must sort identically to the
    // precomposed spelling (#459 NFC convention), authored titles beat
    // filename stems, and case differences do not split the order.
    let decomposed = "Cafe\u{0301} plan.md"
    let files = [
      file(path: "notes/zulu.md"),
      file(path: "notes/\(decomposed)"),
      file(path: "notes/café list.md"),
      file(path: "notes/alpha.md", displayName: "Bravo title"),
      file(path: "notes/Apple.md"),
    ]
    let level = organize(
      files, choice: .init(sort: .init(field: .name, direction: .asc), grouping: .none),
      now: instant(2026, 7, 18, timeZone: "UTC"),
      calendar: gregorian("UTC"))
    XCTAssertEqual(
      paths(level),
      [
        "notes/Apple.md",
        "notes/alpha.md",  // effective label "Bravo title"
        "notes/café list.md",  // "l" < "p", case-insensitive primary
        "notes/\(decomposed)",  // "Café plan" — NFC ≡ precomposed
        "notes/zulu.md",
      ])
  }

  func testNameSortDescendingReversesButKeepsTotalOrder() {
    let files = [
      file(path: "a.md"),
      file(path: "b.md"),
      file(path: "c.md"),
    ]
    let ascending = organize(
      files, choice: .init(sort: .init(field: .name, direction: .asc), grouping: .none),
      now: instant(2026, 7, 18, timeZone: "UTC"), calendar: gregorian("UTC"))
    let descending = organize(
      files, choice: .init(sort: .init(field: .name, direction: .desc), grouping: .none),
      now: instant(2026, 7, 18, timeZone: "UTC"), calendar: gregorian("UTC"))
    XCTAssertEqual(paths(ascending), ["a.md", "b.md", "c.md"])
    XCTAssertEqual(paths(descending), ["c.md", "b.md", "a.md"])
  }

  // MARK: - Created sort

  func testCreatedSortPrefersCivilDateOverSimultaneousBirthtime() {
    // Both files carry the same birthtime instant; the authored civil date
    // must win for the file that has one, even when the birthtime would sort
    // it the other way.
    let birthtime = milliseconds(instant(2026, 6, 1, 12, 0, 0, timeZone: "UTC"))
    let files = [
      file(path: "authored.md", createdDate: "2026-07-10", createdMs: birthtime),
      file(path: "birthtime.md", createdMs: birthtime),
    ]
    let level = organize(
      files,
      choice: .init(sort: .init(field: .created, direction: .desc), grouping: .none),
      now: instant(2026, 7, 18, timeZone: "UTC"),
      calendar: gregorian("UTC"))
    XCTAssertEqual(paths(level), ["authored.md", "birthtime.md"])
  }

  func testCreatedSortConsumesResolverDateWithoutReparsing() {
    // The spy resolves "2026-01-01" to an instant far in the future. If the
    // organizer trusted the ISO components instead of the resolver's Date,
    // this file would sort last, not first.
    let spy = SpyResolver()
    spy.override["2026-01-01"] = instant(2030, 1, 1, timeZone: "UTC")
    let files = [
      file(path: "lied.md", createdDate: "2026-01-01"),
      file(path: "real.md", createdDate: "2026-07-01"),
    ]
    let level = organize(
      files,
      choice: .init(sort: .init(field: .created, direction: .desc), grouping: .none),
      now: instant(2026, 7, 18, timeZone: "UTC"),
      calendar: gregorian("UTC"),
      resolver: spy)
    XCTAssertEqual(paths(level), ["lied.md", "real.md"])
    XCTAssertEqual(spy.resolved.sorted(), ["2026-01-01", "2026-07-01"])
  }

  func testCreatedSortNullLastRegardlessOfDirection() {
    let files = [
      file(path: "none.md"),
      file(path: "old.md", createdDate: "2020-01-01"),
      file(path: "new.md", createdDate: "2026-01-01"),
    ]
    let newestFirst = organize(
      files,
      choice: .init(sort: .init(field: .created, direction: .desc), grouping: .none),
      now: instant(2026, 7, 18, timeZone: "UTC"), calendar: gregorian("UTC"))
    let oldestFirst = organize(
      files,
      choice: .init(sort: .init(field: .created, direction: .asc), grouping: .none),
      now: instant(2026, 7, 18, timeZone: "UTC"), calendar: gregorian("UTC"))
    XCTAssertEqual(paths(newestFirst), ["new.md", "old.md", "none.md"])
    XCTAssertEqual(paths(oldestFirst), ["old.md", "new.md", "none.md"])
  }

  func testInvalidCivilDateFallsThroughToBirthtime() {
    let files = [
      file(
        path: "impossible.md",
        createdDate: "2026-02-30",
        createdMs: milliseconds(instant(2026, 1, 1, timeZone: "UTC"))),
      file(path: "valid.md", createdDate: "2026-03-01"),
    ]
    let level = organize(
      files,
      choice: .init(sort: .init(field: .created, direction: .desc), grouping: .none),
      now: instant(2026, 7, 18, timeZone: "UTC"),
      calendar: gregorian("UTC"))
    XCTAssertEqual(paths(level), ["valid.md", "impossible.md"])
  }

  // MARK: - Modified sort

  func testModifiedSortOrdersByInstantWithNameTieBreak() {
    let early = milliseconds(instant(2026, 7, 1, 8, 0, 0, timeZone: "UTC"))
    let late = milliseconds(instant(2026, 7, 2, 8, 0, 0, timeZone: "UTC"))
    let files = [
      file(path: "b-tie.md", mtimeMs: late),
      file(path: "a-tie.md", mtimeMs: late),
      file(path: "old.md", mtimeMs: early),
    ]
    let level = organize(
      files,
      choice: .init(sort: .init(field: .modified, direction: .desc), grouping: .none),
      now: instant(2026, 7, 18, timeZone: "UTC"),
      calendar: gregorian("UTC"))
    XCTAssertEqual(paths(level), ["a-tie.md", "b-tie.md", "old.md"])
  }

  // MARK: - Total order property

  func testComparatorIsATotalOrderAcrossPermutations() {
    // Deterministic total order: every input permutation of a fixture with
    // ties, nulls, case collisions, and diacritics converges to one output.
    let birthtime = milliseconds(instant(2026, 5, 5, timeZone: "UTC"))
    let fixture = [
      file(path: "z/Épsilon.md"),
      file(path: "a/epsilon.md"),
      file(path: "m/no-date.md"),
      file(path: "m/dated.md", createdDate: "2026-04-01"),
      file(path: "m/birth.md", createdMs: birthtime),
      file(path: "dup/name.md", displayName: "Same Title"),
      file(path: "dup2/name.md", displayName: "Same Title"),
    ]
    for choice in [
      SidebarOrganizationChoice(sort: .init(field: .name, direction: .asc), grouping: .none),
      SidebarOrganizationChoice(sort: .init(field: .created, direction: .desc), grouping: .none),
      SidebarOrganizationChoice(sort: .init(field: .modified, direction: .asc), grouping: .none),
    ] {
      var seen: Set<[String]> = []
      var generator = SystemRandomNumberGenerator()
      for _ in 0..<24 {
        let shuffled = fixture.shuffled(using: &generator)
        let level = organize(
          shuffled, choice: choice,
          now: instant(2026, 7, 18, timeZone: "UTC"),
          calendar: gregorian("UTC"))
        seen.insert(paths(level))
      }
      XCTAssertEqual(seen.count, 1, "unstable order for \(choice)")
    }
  }

  // MARK: - Cross-calendar identity

  func testOrderAndDayBucketsIdenticalAcrossSystemCalendars() {
    // The resolved Gregorian local-day value is the sort/group input. An
    // injected Buddhist, Hebrew, or Islamic *system* calendar may relabel
    // presentation, but must never shift the order or the day-window bucket
    // a note lands in.
    let zone = "Asia/Bangkok"
    let now = instant(2026, 7, 18, 10, 0, 0, timeZone: zone)
    let files = [
      file(path: "today.md", createdDate: "2026-07-18"),
      file(path: "yesterday.md", createdDate: "2026-07-17"),
      file(path: "week.md", createdDate: "2026-07-12"),
      file(path: "month.md", createdDate: "2026-06-25"),
    ]
    let choice = SidebarOrganizationChoice(
      sort: .init(field: .created, direction: .desc), grouping: .dateBuckets)

    var results: [[String]] = []
    var windows: [[String]] = []
    for identifier in [Calendar.Identifier.gregorian, .buddhist, .hebrew, .islamicCivil] {
      let level = organize(
        files, choice: choice, now: now,
        calendar: calendar(identifier, timeZone: zone))
      results.append(paths(level))
      // Day-window bucket keys (today/yesterday/7d/30d) are calendar-system
      // independent; month buckets legitimately localize.
      windows.append(
        level.groups.map(\.key).filter { !$0.hasPrefix("month-") && !$0.hasPrefix("year-") })
    }
    XCTAssertTrue(results.allSatisfy { $0 == results[0] }, "order shifted: \(results)")
    XCTAssertTrue(windows.allSatisfy { $0 == windows[0] }, "day windows shifted: \(windows)")
    XCTAssertEqual(windows[0], ["today", "yesterday", "previous7", "previous30"])
  }

  // MARK: - Grouping

  func testGroupingForcesMatchingDateSortDescending() {
    // A name sort combined with date grouping is not offered; normalization
    // resolves it to modified/newest-first before any bucket is computed.
    let normalized = SidebarOrganizationChoice(
      sort: .init(field: .name, direction: .asc), grouping: .dateBuckets
    ).normalized
    XCTAssertEqual(normalized.sort.field, .modified)
    XCTAssertEqual(normalized.sort.direction, .desc)
    XCTAssertEqual(normalized.grouping, .dateBuckets)

    let created = SidebarOrganizationChoice(
      sort: .init(field: .created, direction: .asc), grouping: .dateBuckets
    ).normalized
    XCTAssertEqual(created.sort.field, .created)
    XCTAssertEqual(created.sort.direction, .desc)
  }

  func testBucketsOmitEmptyRangesAndLabelContiguousRuns() {
    let zone = "UTC"
    let now = instant(2026, 7, 18, 12, 0, 0, timeZone: zone)
    let files = [
      file(path: "today.md", mtimeMs: milliseconds(instant(2026, 7, 18, 9, 0, 0, timeZone: zone))),
      // No "yesterday" file: that bucket must not appear.
      file(path: "week.md", mtimeMs: milliseconds(instant(2026, 7, 13, 9, 0, 0, timeZone: zone))),
      file(path: "month.md", mtimeMs: milliseconds(instant(2026, 6, 20, 9, 0, 0, timeZone: zone))),
      file(path: "april.md", mtimeMs: milliseconds(instant(2026, 4, 2, 9, 0, 0, timeZone: zone))),
      file(path: "last-year.md", mtimeMs: milliseconds(instant(2025, 12, 30, 9, 0, 0, timeZone: zone))),
    ]
    let level = organize(
      files,
      choice: .init(sort: .init(field: .modified, direction: .desc), grouping: .dateBuckets),
      now: now,
      calendar: gregorian(zone))
    XCTAssertEqual(
      level.groups.map(\.key),
      ["today", "previous7", "previous30", "month-1-2026-4", "year-1-2025"])
    XCTAssertEqual(level.groups.map(\.firstPath), [
      "today.md", "week.md", "month.md", "april.md", "last-year.md",
    ])
    XCTAssertEqual(level.groups.map(\.fileCount), [1, 1, 1, 1, 1])
    XCTAssertEqual(
      paths(level), ["today.md", "week.md", "month.md", "april.md", "last-year.md"])
  }

  func testBucketBoundariesAtMidnightInPositiveAndNegativeOffsets() {
    for zone in ["Pacific/Kiritimati", "Etc/GMT+12"] {  // UTC+14 and UTC-12
      let now = instant(2026, 7, 18, 0, 30, 0, timeZone: zone)
      let justBeforeMidnight = milliseconds(
        instant(2026, 7, 17, 23, 59, 59, timeZone: zone))
      let justAfterMidnight = milliseconds(
        instant(2026, 7, 18, 0, 0, 1, timeZone: zone))
      let files = [
        file(path: "yesterday.md", mtimeMs: justBeforeMidnight),
        file(path: "today.md", mtimeMs: justAfterMidnight),
      ]
      let level = organize(
        files,
        choice: .init(sort: .init(field: .modified, direction: .desc), grouping: .dateBuckets),
        now: now,
        calendar: gregorian(zone))
      XCTAssertEqual(level.groups.map(\.key), ["today", "yesterday"], zone)
    }
  }

  func testBucketBoundariesAcrossSpringForwardUseRealTimezoneRules() {
    // America/New_York 2026-03-08: 23-hour day. A civil date resolved on
    // either side of the transition must land in the right relative bucket
    // when "now" is a few days later; 86,400-second arithmetic would drift.
    let zone = "America/New_York"
    let now = instant(2026, 3, 12, 12, 0, 0, timeZone: zone)
    let files = [
      file(path: "transition-day.md", createdDate: "2026-03-08"),
      file(path: "before.md", createdDate: "2026-03-07"),
    ]
    let level = organize(
      files,
      choice: .init(sort: .init(field: .created, direction: .desc), grouping: .dateBuckets),
      now: now,
      calendar: gregorian(zone))
    // Both are 4–5 civil days back: inside the previous-7-days window.
    XCTAssertEqual(level.groups.map(\.key), ["previous7"])
    XCTAssertEqual(level.groups[0].fileCount, 2)
  }

  func testYearRolloverSplitsMonthAndYearBuckets() {
    let zone = "UTC"
    let now = instant(2026, 1, 15, 12, 0, 0, timeZone: zone)
    let files = [
      // Same calendar year, older than 30 days is impossible in January —
      // December of the previous year must become a year bucket, not
      // "December 2025" claiming the current year.
      file(path: "december.md", mtimeMs: milliseconds(instant(2025, 12, 1, 9, 0, 0, timeZone: zone))),
      file(path: "november.md", mtimeMs: milliseconds(instant(2025, 11, 1, 9, 0, 0, timeZone: zone))),
    ]
    let level = organize(
      files,
      choice: .init(sort: .init(field: .modified, direction: .desc), grouping: .dateBuckets),
      now: now,
      calendar: gregorian(zone))
    XCTAssertEqual(level.groups.map(\.key), ["year-1-2025"])
    XCTAssertEqual(level.groups[0].fileCount, 2)
  }

  func testUndatedFilesUnderCreatedGroupingFormTrailingNoDateBucket() {
    let zone = "UTC"
    let now = instant(2026, 7, 18, 12, 0, 0, timeZone: zone)
    let files = [
      file(path: "dated.md", createdDate: "2026-07-18"),
      file(path: "undated.md"),
    ]
    let level = organize(
      files,
      choice: .init(sort: .init(field: .created, direction: .desc), grouping: .dateBuckets),
      now: now,
      calendar: gregorian(zone))
    XCTAssertEqual(level.groups.map(\.key), ["today", "nodate"])
    XCTAssertEqual(level.groups.last?.firstPath, "undated.md")
  }

  // MARK: - Pinned files

  func testPinnedFilesPrecedeGroupsInAuthoredOrderWithoutDuplication() {
    let zone = "UTC"
    let now = instant(2026, 7, 18, 12, 0, 0, timeZone: zone)
    let files = [
      file(path: "a.md", mtimeMs: milliseconds(instant(2026, 7, 18, 9, 0, 0, timeZone: zone))),
      file(path: "b.md", mtimeMs: milliseconds(instant(2026, 7, 17, 9, 0, 0, timeZone: zone))),
      file(path: "c.md", mtimeMs: milliseconds(instant(2026, 7, 16, 9, 0, 0, timeZone: zone))),
    ]
    let level = organize(
      files,
      choice: .init(sort: .init(field: .modified, direction: .desc), grouping: .dateBuckets),
      pins: ["c.md", "a.md"],
      now: now,
      calendar: gregorian(zone))
    // Authored pin order, then the remaining files bucketed normally; a
    // pinned file never re-appears inside a date bucket.
    XCTAssertEqual(paths(level), ["c.md", "a.md", "b.md"])
    XCTAssertEqual(level.pinnedCount, 2)
    XCTAssertEqual(level.groups.map(\.key), ["yesterday"])
    XCTAssertEqual(level.groups[0].firstPath, "b.md")
  }

  func testStalePinEntriesAreIgnoredAndReportedForPruning() {
    let files = [file(path: "real.md")]
    let level = organize(
      files,
      choice: .init(sort: .init(field: .name, direction: .asc), grouping: .none),
      pins: ["ghost.md", "real.md"],
      now: instant(2026, 7, 18, timeZone: "UTC"),
      calendar: gregorian("UTC"))
    XCTAssertEqual(paths(level), ["real.md"])
    XCTAssertEqual(level.pinnedCount, 1)
    XCTAssertEqual(level.stalePinnedPaths, ["ghost.md"])
  }

  // MARK: - Override precedence

  func testEffectiveChoicePrecedenceFolderOverVaultOverDefault() {
    var prefs = SidebarOrganizationPrefs()
    XCTAssertEqual(prefs.effectiveChoice(forFolder: "any"), .defaults)

    prefs.vaultChoice = SidebarOrganizationChoice(
      sort: .init(field: .modified, direction: .desc), grouping: .none)
    XCTAssertEqual(
      prefs.effectiveChoice(forFolder: "Projects").sort.field, .modified)

    prefs.folderOverrides["Projects"] = SidebarOrganizationOverride(
      sort: SidebarSortOption(field: .created, direction: .asc), grouping: nil)
    let effective = prefs.effectiveChoice(forFolder: "Projects")
    XCTAssertEqual(effective.sort.field, .created)
    XCTAssertEqual(effective.sort.direction, .asc)
    // Partial override: grouping still falls through to the vault choice.
    XCTAssertEqual(effective.grouping, .none)
    // Sibling folders keep the vault default.
    XCTAssertEqual(prefs.effectiveChoice(forFolder: "Other").sort.field, .modified)
  }

  // MARK: - JSON schema

  func testSchemaDecodeLenientlyRecoversPerKeyAndPreservesUnknownSiblings() {
    let root: [String: Any] = [
      "version": 1,
      "sort": ["field": "created", "direction": "desc"],
      "grouping": "dateBuckets",
      "folderOverrides": [
        "Projects": ["sort": ["field": "name", "direction": "asc"]],
        "Broken": ["sort": ["field": "no-such-field", "direction": 12]],
      ],
      "pins": [
        "Projects": ["Projects/a.md", "Projects/b.md"],
        "Bad": "not-an-array",
      ],
      "future-key": ["untouched": true],
    ]
    let decoded = SidebarOrganizationSchema.decode(root: root)
    XCTAssertEqual(decoded.prefs.vaultChoice.sort.field, .created)
    XCTAssertEqual(decoded.prefs.vaultChoice.grouping, .dateBuckets)
    XCTAssertEqual(decoded.prefs.folderOverrides["Projects"]?.sort?.field, .name)
    // A malformed override entry is dropped, not fatal.
    XCTAssertNil(decoded.prefs.folderOverrides["Broken"])
    XCTAssertEqual(decoded.pins.paths(forFolder: "Projects"), ["Projects/a.md", "Projects/b.md"])
    XCTAssertEqual(decoded.pins.paths(forFolder: "Bad"), [])
  }

  func testSchemaMutatorsPreserveUnknownKeysEverywhere() throws {
    var root: [String: Any] = [
      "version": 1,
      "future-key": ["untouched": true],
      "folderOverrides": [
        "Projects": ["future-inner": "keep"]
      ],
      "pins": ["Elsewhere": ["Elsewhere/x.md"]],
    ]
    SidebarOrganizationSchema.setVaultChoice(
      &root,
      SidebarOrganizationChoice(
        sort: .init(field: .modified, direction: .desc), grouping: .dateBuckets))
    SidebarOrganizationSchema.setFolderOverride(
      &root, folder: "Projects",
      override: SidebarOrganizationOverride(
        sort: SidebarSortOption(field: .created, direction: .asc), grouping: nil))
    SidebarOrganizationSchema.setPins(
      &root, folder: "Projects", paths: ["Projects/pin.md"])

    XCTAssertEqual(root["future-key"] as? [String: Bool], ["untouched": true])
    let overrides = try XCTUnwrap(root["folderOverrides"] as? [String: Any])
    let projects = try XCTUnwrap(overrides["Projects"] as? [String: Any])
    XCTAssertEqual(projects["future-inner"] as? String, "keep")
    let pins = try XCTUnwrap(root["pins"] as? [String: Any])
    XCTAssertEqual(pins["Elsewhere"] as? [String], ["Elsewhere/x.md"])
    XCTAssertEqual(pins["Projects"] as? [String], ["Projects/pin.md"])

    // Round trip: what the mutators wrote is what decode reads back.
    let decoded = SidebarOrganizationSchema.decode(root: root)
    XCTAssertEqual(decoded.prefs.vaultChoice.sort.field, .modified)
    XCTAssertEqual(decoded.prefs.vaultChoice.grouping, .dateBuckets)
    XCTAssertEqual(decoded.prefs.folderOverrides["Projects"]?.sort?.field, .created)
    XCTAssertEqual(decoded.pins.paths(forFolder: "Projects"), ["Projects/pin.md"])
  }

  func testClearFolderSortOverridePreservesGroupingAndUnknownKeys() throws {
    var root: [String: Any] = [
      "folderOverrides": [
        "Mixed": [
          "sort": ["field": "name", "direction": "asc"],
          "grouping": "dateBuckets",
          "future-inner": "keep",
        ],
        "Ours": ["sort": ["field": "created", "direction": "desc"]],
      ]
    ]
    SidebarOrganizationSchema.clearFolderSortOverride(&root, folder: "Mixed")
    SidebarOrganizationSchema.clearFolderSortOverride(&root, folder: "Ours")
    let overrides = try XCTUnwrap(root["folderOverrides"] as? [String: Any])
    // Only the sort key is owned by this clear: the grouping override and
    // unknown inner keys survive; an entry that becomes empty is removed.
    let mixed = try XCTUnwrap(overrides["Mixed"] as? [String: Any])
    XCTAssertEqual(mixed["future-inner"] as? String, "keep")
    XCTAssertEqual(mixed["grouping"] as? String, "dateBuckets")
    XCTAssertNil(mixed["sort"])
    XCTAssertNil(overrides["Ours"])
  }

  func testSortWritesMergeIntoExistingObjectsPreservingUnknownMembers() throws {
    // Round-2 finding 4: an additive unknown member inside a stored sort
    // object must survive vault and folder sort writes.
    var root: [String: Any] = [
      "sort": [
        "field": "name", "direction": "asc", "future-member": true,
      ],
      "folderOverrides": [
        "Projects": [
          "sort": ["field": "created", "direction": "asc", "future-member": 7]
        ]
      ],
    ]
    SidebarOrganizationSchema.setVaultChoice(
      &root,
      SidebarOrganizationChoice(
        sort: SidebarSortOption(field: .modified, direction: .desc),
        grouping: .none))
    SidebarOrganizationSchema.setFolderOverride(
      &root, folder: "Projects",
      override: SidebarOrganizationOverride(
        sort: SidebarSortOption(field: .name, direction: .desc), grouping: nil))

    let sort = try XCTUnwrap(root["sort"] as? [String: Any])
    XCTAssertEqual(sort["field"] as? String, "modified")
    XCTAssertEqual(sort["direction"] as? String, "desc")
    XCTAssertEqual(sort["future-member"] as? Bool, true)
    let overrides = try XCTUnwrap(root["folderOverrides"] as? [String: Any])
    let projects = try XCTUnwrap(overrides["Projects"] as? [String: Any])
    let projectsSort = try XCTUnwrap(projects["sort"] as? [String: Any])
    XCTAssertEqual(projectsSort["field"] as? String, "name")
    XCTAssertEqual(projectsSort["future-member"] as? Int, 7)
  }

  func testPrefsStructuralReplayRekeysAndDropsFolderOverrides() {
    var prefs = SidebarOrganizationPrefs()
    prefs.folderOverrides["Projects"] = SidebarOrganizationOverride(
      sort: SidebarSortOption(field: .modified, direction: .desc), grouping: nil)
    prefs.folderOverrides["Projects/sub"] = SidebarOrganizationOverride(
      sort: nil, grouping: .dateBuckets)
    prefs.folderOverrides["Projectile"] = SidebarOrganizationOverride(
      sort: SidebarSortOption(field: .name, direction: .desc), grouping: nil)

    XCTAssertTrue(prefs.applyFolderRename(from: "Projects", to: "Archive"))
    XCTAssertNotNil(prefs.folderOverrides["Archive"])
    XCTAssertNotNil(prefs.folderOverrides["Archive/sub"])
    XCTAssertNil(prefs.folderOverrides["Projects"])
    // Prefix safety: "Projectile" is untouched.
    XCTAssertNotNil(prefs.folderOverrides["Projectile"])

    XCTAssertTrue(prefs.applyFolderDelete(folders: ["Archive"]))
    XCTAssertNil(prefs.folderOverrides["Archive"])
    XCTAssertNil(prefs.folderOverrides["Archive/sub"])
    XCTAssertFalse(prefs.applyFolderDelete(folders: ["Nothing"]))
  }

  // MARK: - Announcements

  func testSortAnnouncementNamesFieldAndDirection() {
    XCTAssertEqual(
      SidebarOrganizationChoice(
        sort: .init(field: .modified, direction: .desc), grouping: .none
      ).sortAnnouncement,
      "Sorted by modified, newest first.")
    XCTAssertEqual(
      SidebarOrganizationChoice(
        sort: .init(field: .name, direction: .asc), grouping: .none
      ).sortAnnouncement,
      "Sorted by name, A to Z.")
    XCTAssertEqual(
      SidebarOrganizationChoice(
        sort: .init(field: .created, direction: .asc), grouping: .none
      ).sortAnnouncement,
      "Sorted by created, oldest first.")
    // The announcement always describes the effective (normalized) state:
    // grouping forces the matching date sort descending.
    XCTAssertEqual(
      SidebarOrganizationChoice(
        sort: .init(field: .created, direction: .asc), grouping: .dateBuckets
      ).sortAnnouncement,
      "Sorted by created, newest first, grouped by date.")
  }
}
