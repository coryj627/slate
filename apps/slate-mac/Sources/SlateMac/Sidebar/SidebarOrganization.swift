// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// The immutable metadata one file contributes to level organization.
/// Production projects it from `FileSummary` on the serial level worker so
/// sorting and bucketing stay pure and testable without FFI or actor hops.
struct SidebarOrganizerFile: Equatable {
  let path: String
  let name: String
  let displayName: String?
  let createdDate: String?
  let createdMs: Int64?
  let mtimeMs: Int64
}

enum SidebarSortField: String, CaseIterable, Equatable {
  case name
  case created
  case modified
}

enum SidebarSortDirection: String, CaseIterable, Equatable {
  case asc
  case desc
}

struct SidebarSortOption: Equatable {
  var field: SidebarSortField
  var direction: SidebarSortDirection

  static let defaults = SidebarSortOption(field: .name, direction: .asc)
}

enum SidebarGroupingOption: String, CaseIterable, Equatable {
  case none
  case dateBuckets
}

/// One folder's effective sort/grouping choice.
struct SidebarOrganizationChoice: Equatable {
  var sort: SidebarSortOption
  var grouping: SidebarGroupingOption

  static let defaults = SidebarOrganizationChoice(
    sort: .defaults, grouping: .none)

  /// Grouping forces the matching date sort descending; mixed name-sort +
  /// grouping combinations are not offered (fl3 spec §FL3-1.3). A name sort
  /// falls back to the modified date, the default date field.
  var normalized: SidebarOrganizationChoice {
    guard grouping == .dateBuckets else { return self }
    let field: SidebarSortField = sort.field == .name ? .modified : sort.field
    return SidebarOrganizationChoice(
      sort: SidebarSortOption(field: field, direction: .desc),
      grouping: .dateBuckets)
  }

  /// The polite announcement posted when organization changes. It always
  /// describes the effective (normalized) state, never an impossible
  /// combination like an ascending grouped sort.
  var sortAnnouncement: String {
    let effective = normalized
    let fieldName: String
    switch effective.sort.field {
    case .name: fieldName = "name"
    case .created: fieldName = "created"
    case .modified: fieldName = "modified"
    }
    let directionName: String
    if effective.sort.field == .name {
      directionName = effective.sort.direction == .asc ? "A to Z" : "Z to A"
    } else {
      directionName =
        effective.sort.direction == .desc ? "newest first" : "oldest first"
    }
    let grouped = effective.grouping == .dateBuckets ? ", grouped by date" : ""
    return "Sorted by \(fieldName), \(directionName)\(grouped)."
  }
}

/// A partial per-folder override; nil fields fall through to the vault choice.
struct SidebarOrganizationOverride: Equatable {
  var sort: SidebarSortOption?
  var grouping: SidebarGroupingOption?
  /// FL7-2 (#669): per-folder display overrides — one storage, two
  /// projections (the dual-pane list header and tree-mode rows read
  /// the same values).
  var previewLines: Int?
  var density: SidebarRowPreferencesSnapshot.Density?
  /// FL7-2 rule 2: folder containers only — the dual-pane list shows
  /// the folder's whole subtree when on. Default off.
  var descendants: Bool?

  var isEmpty: Bool {
    sort == nil && grouping == nil && previewLines == nil && density == nil
      && descendants == nil
  }
}

/// Vault-wide default plus per-folder overrides (decision 5: overrides are UI
/// prefs, not schema). Precedence: folder override > vault choice > defaults.
struct SidebarOrganizationPrefs: Equatable {
  var vaultChoice: SidebarOrganizationChoice = .defaults
  var folderOverrides: [String: SidebarOrganizationOverride] = [:]

  func effectiveChoice(forFolder folder: String) -> SidebarOrganizationChoice {
    guard let override = folderOverrides[folder] else { return vaultChoice }
    return SidebarOrganizationChoice(
      sort: override.sort ?? vaultChoice.sort,
      grouping: override.grouping ?? vaultChoice.grouping)
  }

  /// FL7-2 rule 3: display overrides fall back to the device-local
  /// defaults the caller supplies (density/preview are device prefs;
  /// only the per-folder override lives vault-locally).
  func effectivePreviewLines(forFolder folder: String, default value: Int)
    -> Int
  {
    folderOverrides[folder]?.previewLines ?? value
  }

  func effectiveDensity(
    forFolder folder: String,
    default value: SidebarRowPreferencesSnapshot.Density
  ) -> SidebarRowPreferencesSnapshot.Density {
    folderOverrides[folder]?.density ?? value
  }

  /// FL7-2 rule 2: Include Subfolders, folder containers only.
  func includesDescendants(forFolder folder: String) -> Bool {
    folderOverrides[folder]?.descendants ?? false
  }

  /// A folder rename/move retargets every override keyed at or under the old
  /// folder (path keys, so they follow the folder like pins do).
  @discardableResult
  mutating func applyFolderRename(from oldFolder: String, to newFolder: String) -> Bool {
    let oldPrefix = oldFolder + "/"
    let newPrefix = newFolder + "/"
    var next: [String: SidebarOrganizationOverride] = [:]
    next.reserveCapacity(folderOverrides.count)
    var renamed: [(key: String, value: SidebarOrganizationOverride)] = []
    for (folder, override) in folderOverrides {
      if folder == oldFolder {
        renamed.append((newFolder, override))
      } else if folder.hasPrefix(oldPrefix) {
        renamed.append((newPrefix + folder.dropFirst(oldPrefix.count), override))
      } else {
        next[folder] = override
      }
    }
    guard !renamed.isEmpty else { return false }
    // Renamed source wins over a stale destination entry (round-8 finding 3).
    for entry in renamed {
      next[entry.key] = entry.value
    }
    folderOverrides = next
    return true
  }

  /// Deleted folders drop their own and every descendant override.
  @discardableResult
  mutating func applyFolderDelete(folders deletedFolders: [String]) -> Bool {
    guard !deletedFolders.isEmpty else { return false }
    let prefixes = deletedFolders.map { $0 + "/" }
    let doomed = folderOverrides.keys.filter { key in
      deletedFolders.contains(key) || prefixes.contains { key.hasPrefix($0) }
    }
    guard !doomed.isEmpty else { return false }
    for key in doomed { folderOverrides[key] = nil }
    return true
  }
}

/// The production civil-date resolver behind the shared
/// `SidebarCivilDateResolving` seam. FL-01 owns the parser; organization and
/// row presentation both consume this wrapper instead of reparsing.
struct SidebarProductionCivilDateResolver: SidebarCivilDateResolving {
  func resolve(_ canonicalDate: String, calendar: Calendar) -> Date? {
    SidebarCivilDateResolver.resolve(canonicalDate, calendar: calendar)
  }
}

/// One synthetic, nonselectable section header spliced above a file row:
/// the Pinned section or a date bucket (fl3 spec §FL3-1.4/§FL3-2.2).
struct SidebarTreeHeaderRow: Equatable, Hashable, Sendable {
  enum Kind: Equatable, Hashable, Sendable {
    case pinned
    case group
  }

  let kind: Kind
  /// Stable identity component ("pinned" or the bucket key), so the List row
  /// id survives re-renders without colliding across kinds.
  let key: String
  let label: String
  let fileCount: Int
  let depth: Int
}

/// Per-level organization bookkeeping retained by the view model so a
/// preference change can re-sort cached levels without refetching.
struct SidebarLevelPresentation: Equatable, Sendable {
  var headersBefore: [NodeID: SidebarTreeHeaderRow] = [:]
  var pinnedIDs: Set<NodeID> = []
  var stalePinnedPaths: [String] = []
}

/// The organized presentation of one level's file portion.
struct SidebarOrganizedLevel: Equatable, Sendable {
  struct Group: Equatable, Sendable {
    let key: String
    let label: String
    let firstPath: String
    let fileCount: Int
  }

  let orderedPaths: [String]
  let pinnedCount: Int
  let stalePinnedPaths: [String]
  let groups: [Group]
}

/// Pure sorting/bucketing engine for one level's files (decision 5: backend
/// ships raw fields; the app sorts and groups per level). Keys are built once
/// per file; the civil-date resolver is consulted at most once per file and
/// only when the active field needs a created value.
enum SidebarLevelOrganizer {

  private struct Keyed {
    let file: SidebarOrganizerFile
    /// NFC form of the effective label (#459 convention): an authored title
    /// beats the filename stem, and decomposed/precomposed spellings of the
    /// same name build identical keys.
    let nameKey: String
    /// The created sort instant: the resolver's absolute local-day start for
    /// a valid authored civil date, else the parsed datetime/birthtime
    /// instant. Nil means "no created value at all" (NULL-last).
    let created: Date?
    let modified: Date
  }

  static func organize(
    files: [SidebarOrganizerFile],
    choice: SidebarOrganizationChoice,
    pinnedPaths: [String],
    now: Date,
    calendar: Calendar,
    locale: Locale = .current,
    civilDateResolver: any SidebarCivilDateResolving
  ) -> SidebarOrganizedLevel {
    // The compatibility path is intentionally non-cancelling. Production
    // whole-level work uses `organizeCancellable` so a superseded 50k sort
    // releases the serial worker promptly.
    organizeCancellable(
      files: files,
      choice: choice,
      pinnedPaths: pinnedPaths,
      now: now,
      calendar: calendar,
      locale: locale,
      civilDateResolver: civilDateResolver,
      isCancelled: { false })!
  }

  static func organizeCancellable(
    files: [SidebarOrganizerFile],
    choice: SidebarOrganizationChoice,
    pinnedPaths: [String],
    now: Date,
    calendar: Calendar,
    locale: Locale = .current,
    civilDateResolver: any SidebarCivilDateResolving,
    isCancelled: () -> Bool
  ) -> SidebarOrganizedLevel? {
    let effective = choice.normalized
    let needsCreated =
      effective.sort.field == .created
    var keyed: [Keyed] = []
    keyed.reserveCapacity(files.count)
    for (offset, file) in files.enumerated() {
      if offset.isMultiple(of: 256), isCancelled() { return nil }
      let label =
        file.displayName ?? (file.name as NSString).deletingPathExtension
      var created: Date?
      if needsCreated {
        if let civil = file.createdDate,
          let resolved = civilDateResolver.resolve(civil, calendar: calendar)
        {
          created = resolved
        } else if let createdMs = file.createdMs {
          created = Self.date(fromMilliseconds: createdMs)
        }
      }
      keyed.append(Keyed(
        file: file,
        nameKey: label.precomposedStringWithCanonicalMapping,
        created: created,
        modified: Self.date(fromMilliseconds: file.mtimeMs)))
    }

    var byPath: [String: Keyed] = [:]
    byPath.reserveCapacity(keyed.count)
    for (offset, entry) in keyed.enumerated() {
      if offset.isMultiple(of: 256), isCancelled() { return nil }
      byPath[entry.file.path] = entry
    }

    var pinned: [Keyed] = []
    var stale: [String] = []
    var pinnedSet: Set<String> = []
    for (offset, path) in pinnedPaths.enumerated() {
      if offset.isMultiple(of: 256), isCancelled() { return nil }
      if let entry = byPath[path] {
        // Defensive: a duplicated authored entry pins the row once.
        if pinnedSet.insert(path).inserted { pinned.append(entry) }
      } else {
        stale.append(path)
      }
    }

    var unpinned: [Keyed] = []
    unpinned.reserveCapacity(keyed.count - pinned.count)
    for (offset, entry) in keyed.enumerated() {
      if offset.isMultiple(of: 256), isCancelled() { return nil }
      if !pinnedSet.contains(entry.file.path) { unpinned.append(entry) }
    }
    guard let sorted = stableSort(
      unpinned, by: effective.sort, isCancelled: isCancelled)
    else { return nil }

    var groups: [SidebarOrganizedLevel.Group] = []
    if effective.grouping == .dateBuckets {
      let classifier = BucketClassifier(now: now, calendar: calendar, locale: locale)
      var currentKey: String?
      var currentLabel = ""
      var currentFirst = ""
      var currentCount = 0
      for (offset, entry) in sorted.enumerated() {
        if offset.isMultiple(of: 256), isCancelled() { return nil }
        let date = effective.sort.field == .created ? entry.created : entry.modified
        let bucket = classifier.classify(date)
        if bucket.key != currentKey {
          if let key = currentKey {
            groups.append(
              SidebarOrganizedLevel.Group(
                key: key, label: currentLabel,
                firstPath: currentFirst, fileCount: currentCount))
          }
          currentKey = bucket.key
          currentLabel = bucket.label
          currentFirst = entry.file.path
          currentCount = 0
        }
        currentCount += 1
      }
      if let key = currentKey {
        groups.append(
          SidebarOrganizedLevel.Group(
            key: key, label: currentLabel,
            firstPath: currentFirst, fileCount: currentCount))
      }
    }

    guard !isCancelled() else { return nil }
    var orderedPaths: [String] = []
    orderedPaths.reserveCapacity(pinned.count + sorted.count)
    for entry in pinned { orderedPaths.append(entry.file.path) }
    for (offset, entry) in sorted.enumerated() {
      if offset.isMultiple(of: 256), isCancelled() { return nil }
      orderedPaths.append(entry.file.path)
    }
    return SidebarOrganizedLevel(
      orderedPaths: orderedPaths,
      pinnedCount: pinned.count,
      stalePinnedPaths: stale,
      groups: groups)
  }

  /// Bottom-up stable merge sort with cancellation checks inside every merge
  /// pass. The comparator is a strict total order for real file rows, while
  /// choosing the left entry on equality preserves deterministic duplicate
  /// handling from the old implementation.
  private static func stableSort(
    _ values: [Keyed],
    by sort: SidebarSortOption,
    isCancelled: () -> Bool
  ) -> [Keyed]? {
    guard values.count > 1 else {
      return isCancelled() ? nil : values
    }
    var source = values
    var destination = values
    var width = 1
    var operations = 0
    while width < source.count {
      if isCancelled() { return nil }
      var start = 0
      while start < source.count {
        let middle = min(start + width, source.count)
        let end = min(start + width + width, source.count)
        var left = start
        var right = middle
        var output = start
        while left < middle && right < end {
          operations &+= 1
          if operations.isMultiple(of: 256), isCancelled() { return nil }
          if ordered(source[right], before: source[left], by: sort) {
            destination[output] = source[right]
            right += 1
          } else {
            destination[output] = source[left]
            left += 1
          }
          output += 1
        }
        while left < middle {
          operations &+= 1
          if operations.isMultiple(of: 256), isCancelled() { return nil }
          destination[output] = source[left]
          left += 1
          output += 1
        }
        while right < end {
          operations &+= 1
          if operations.isMultiple(of: 256), isCancelled() { return nil }
          destination[output] = source[right]
          right += 1
          output += 1
        }
        start = end
      }
      swap(&source, &destination)
      if width > source.count / 2 { break }
      width *= 2
    }
    return isCancelled() ? nil : source
  }

  // MARK: - Comparator

  /// Strict total order. The primary field honors direction; NULL created
  /// values always sort last; ties always break name-ascending then path
  /// (binary), regardless of direction (fl3 spec §FL3-1.1).
  private static func ordered(
    _ lhs: Keyed, before rhs: Keyed, by sort: SidebarSortOption
  ) -> Bool {
    switch sort.field {
    case .name:
      switch compareNames(lhs.nameKey, rhs.nameKey) {
      case .orderedAscending: return sort.direction == .asc
      case .orderedDescending: return sort.direction == .desc
      case .orderedSame: return tieBreak(lhs, rhs)
      }
    case .created:
      switch (lhs.created, rhs.created) {
      case (nil, nil):
        return tieBreak(lhs, rhs)
      case (nil, _):
        return false  // NULL-last regardless of direction
      case (_, nil):
        return true
      case let (left?, right?):
        if left == right { return tieBreak(lhs, rhs) }
        return sort.direction == .asc ? left < right : left > right
      }
    case .modified:
      if lhs.file.mtimeMs == rhs.file.mtimeMs { return tieBreak(lhs, rhs) }
      return sort.direction == .asc
        ? lhs.file.mtimeMs < rhs.file.mtimeMs
        : lhs.file.mtimeMs > rhs.file.mtimeMs
    }
  }

  private static func tieBreak(_ lhs: Keyed, _ rhs: Keyed) -> Bool {
    switch compareNames(lhs.nameKey, rhs.nameKey) {
    case .orderedAscending: return true
    case .orderedDescending: return false
    case .orderedSame:
      return lhs.file.path.utf8.lexicographicallyPrecedes(rhs.file.path.utf8)
    }
  }

  /// Case-insensitive, numeric-aware, locale-sensitive Finder-style compare
  /// on the NFC keys (fl3 spec §FL3-1.1).
  private static func compareNames(_ lhs: String, _ rhs: String) -> ComparisonResult {
    lhs.localizedStandardCompare(rhs)
  }

  private static func date(fromMilliseconds value: Int64) -> Date {
    Date(timeIntervalSince1970: TimeInterval(value) / 1_000)
  }

  // MARK: - Date buckets

  /// Classifies an instant into the fl3 §FL3-1.3 bucket set with real
  /// calendar arithmetic (no fixed offsets, no 86,400-second days). The
  /// day-window buckets depend only on the calendar's time zone, so an
  /// injected Buddhist/Hebrew/Islamic system calendar cannot shift which
  /// civil day a note lands in; month/year buckets legitimately render in
  /// the user's calendar (locked decision 4: presentation may localize
  /// calendar rendering).
  private struct BucketClassifier {
    let calendar: Calendar
    let locale: Locale
    let startOfToday: Date
    let nowEra: Int
    let nowYear: Int

    init(now: Date, calendar: Calendar, locale: Locale) {
      self.calendar = calendar
      self.locale = locale
      self.startOfToday = calendar.startOfDay(for: now)
      let components = calendar.dateComponents([.era, .year], from: now)
      self.nowEra = components.era ?? 1
      self.nowYear = components.year ?? 0
    }

    func classify(_ date: Date?) -> (key: String, label: String) {
      guard let date else { return ("nodate", "No Date") }
      let startOfDay = calendar.startOfDay(for: date)
      if startOfDay == startOfToday { return ("today", "Today") }
      let delta =
        calendar.dateComponents([.day], from: startOfDay, to: startOfToday).day ?? 0
      if delta == 1 { return ("yesterday", "Yesterday") }
      if (2...7).contains(delta) { return ("previous7", "Previous 7 Days") }
      if (8...30).contains(delta) { return ("previous30", "Previous 30 Days") }

      let components = calendar.dateComponents([.era, .year, .month], from: date)
      let era = components.era ?? 1
      let year = components.year ?? 0
      if era == nowEra && year == nowYear {
        let label = date.formatted(
          Date.FormatStyle(
            locale: locale, calendar: calendar, timeZone: calendar.timeZone
          )
          .month(.wide).year())
        return ("month-\(era)-\(year)-\(components.month ?? 0)", label)
      }
      let label = date.formatted(
        Date.FormatStyle(
          locale: locale, calendar: calendar, timeZone: calendar.timeZone
        )
        .year())
      return ("year-\(era)-\(year)", label)
    }
  }
}

/// Pinned notes per folder: authored order is pin order, and pins are
/// per-folder context — moving a note to another folder drops its pin
/// (fl3 spec §FL3-2, Navigator semantics).
struct SidebarPins: Equatable {
  private(set) var byFolder: [String: [String]] = [:]

  func paths(forFolder folder: String) -> [String] {
    byFolder[folder] ?? []
  }

  func isPinned(_ path: String, inFolder folder: String) -> Bool {
    byFolder[folder]?.contains(path) ?? false
  }

  var isEmpty: Bool { byFolder.isEmpty }

  mutating func pin(_ path: String, inFolder folder: String) {
    var paths = byFolder[folder] ?? []
    guard !paths.contains(path) else { return }
    paths.append(path)
    byFolder[folder] = paths
  }

  mutating func unpin(_ path: String, inFolder folder: String) {
    guard var paths = byFolder[folder] else { return }
    paths.removeAll { $0 == path }
    byFolder[folder] = paths.isEmpty ? nil : paths
  }

  mutating func unpinAll(inFolder folder: String) {
    byFolder[folder] = nil
  }

  mutating func replacePaths(_ paths: [String], forFolder folder: String) {
    byFolder[folder] = paths.isEmpty ? nil : paths
  }

  /// A file rename or move. A rename inside its folder retargets the pin in
  /// place; a move to another folder drops it.
  @discardableResult
  mutating func applyRename(from oldPath: String, to newPath: String) -> Bool {
    let oldFolder = Self.folder(of: oldPath)
    guard var paths = byFolder[oldFolder],
      let index = paths.firstIndex(of: oldPath)
    else { return false }
    if Self.folder(of: newPath) == oldFolder {
      paths[index] = newPath
      // A rename onto an already-pinned path keeps one authored entry —
      // the earlier position wins (round-8 finding 3, member level).
      var seen: Set<String> = []
      paths = paths.filter { seen.insert($0).inserted }
    } else {
      paths.remove(at: index)
    }
    byFolder[oldFolder] = paths.isEmpty ? nil : paths
    return true
  }

  /// A folder rename/move retargets every pin key and member path under the
  /// old folder, including nested subfolders.
  @discardableResult
  mutating func applyFolderRename(from oldFolder: String, to newFolder: String) -> Bool {
    let oldPrefix = oldFolder + "/"
    let newPrefix = newFolder + "/"
    var next: [String: [String]] = [:]
    next.reserveCapacity(byFolder.count)
    var renamed: [(key: String, paths: [String])] = []
    for (folder, paths) in byFolder {
      let newKey: String
      if folder == oldFolder {
        newKey = newFolder
      } else if folder.hasPrefix(oldPrefix) {
        newKey = newPrefix + folder.dropFirst(oldPrefix.count)
      } else {
        next[folder] = paths
        continue
      }
      renamed.append(
        (
          newKey,
          paths.map { path in
            path.hasPrefix(oldPrefix)
              ? newPrefix + path.dropFirst(oldPrefix.count)
              : path
          }
        ))
    }
    guard !renamed.isEmpty else { return false }
    // Deterministic collision rule (round-8 finding 3): the renamed source
    // subtree is authoritative over any stale destination entry — the
    // filesystem rename proves the destination's previous occupant is gone.
    for entry in renamed {
      next[entry.key] = entry.paths
    }
    byFolder = next
    return true
  }

  /// Deletions drop the affected file pins and every pin under a deleted
  /// folder subtree.
  @discardableResult
  mutating func applyDelete(paths deletedPaths: [String], deletedFolders: [String]) -> Bool {
    var changed = false
    for path in deletedPaths {
      let folder = Self.folder(of: path)
      guard var paths = byFolder[folder], let index = paths.firstIndex(of: path)
      else { continue }
      paths.remove(at: index)
      byFolder[folder] = paths.isEmpty ? nil : paths
      changed = true
    }
    guard !deletedFolders.isEmpty else { return changed }
    let prefixes = deletedFolders.map { $0 + "/" }
    let doomed = byFolder.keys.filter { key in
      deletedFolders.contains(key) || prefixes.contains { key.hasPrefix($0) }
    }
    for key in doomed {
      byFolder[key] = nil
      changed = true
    }
    return changed
  }

  static func folder(of path: String) -> String {
    guard let separator = path.lastIndex(of: "/") else { return "" }
    return String(path[..<separator])
  }
}

/// At-most-one stale-prune rewrite per folder per session (fl3 spec
/// §FL3-2.3). Reset when the vault changes.
struct SidebarPinPruneLedger {
  private var pruned: Set<String> = []

  func shouldPrune(folder: String) -> Bool {
    !pruned.contains(folder)
  }

  mutating func markPruned(folder: String) {
    pruned.insert(folder)
  }

  mutating func reset() {
    pruned.removeAll()
  }
}

/// One structural path transform normalized from a `TreeMutation`: the same
/// value drives the in-memory application, the locked raw disk replay, and —
/// while the preference file is read-only — the deferred journal that Retry
/// replays before republishing (round-4 finding 2).
/// FL3-3 (#660): one vault-local shortcut. Storage keeps `kind` as a raw
/// string — FL3-3 ships `file` and `folder`; `tag`/`untagged` are reserved
/// for FL5-2 and must survive every rewrite untouched and in place.
struct SidebarShortcut: Hashable, Sendable {
  enum Kind: String, Sendable {
    case file
    case folder
    /// FL5-2 (#665): a tag container — `path` carries the normalized
    /// full tag, a namespace disjoint from file paths.
    case tag
    /// FL5-2: the reserved Untagged scope — `path` is empty by
    /// convention and ignored.
    case untagged
  }

  let kind: Kind
  let path: String
}

struct SidebarStructuralTransform: Equatable, Sendable, Identifiable {
  /// Journal identity: pending transforms are acknowledged (removed) only
  /// after their locked replay commits (round-6 finding 1).
  let id = UUID()

  struct Rename: Equatable, Sendable {
    let oldPath: String
    let newPath: String
    /// nil when the mutation cannot say (single rename/move): the pin and
    /// override transforms are disjoint on file vs folder paths, so both
    /// interpretations apply safely.
    let isDirectory: Bool?
  }

  var renames: [Rename] = []
  var deletedFiles: [String] = []
  var deletedFolders: [String] = []

  var isEmpty: Bool {
    renames.isEmpty && deletedFiles.isEmpty && deletedFolders.isEmpty
  }

  @discardableResult
  func apply(to pins: inout SidebarPins) -> Bool {
    var changed = false
    for rename in renames {
      if rename.isDirectory != false {
        changed =
          pins.applyFolderRename(from: rename.oldPath, to: rename.newPath)
          || changed
      }
      if rename.isDirectory != true {
        changed =
          pins.applyRename(from: rename.oldPath, to: rename.newPath) || changed
      }
    }
    changed =
      pins.applyDelete(paths: deletedFiles, deletedFolders: deletedFolders)
      || changed
    return changed
  }

  @discardableResult
  func apply(to prefs: inout SidebarOrganizationPrefs) -> Bool {
    var changed = false
    for rename in renames where rename.isDirectory != false {
      changed =
        prefs.applyFolderRename(from: rename.oldPath, to: rename.newPath)
        || changed
    }
    changed = prefs.applyFolderDelete(folders: deletedFolders) || changed
    return changed
  }

  /// The locked disk replay: pins are replayed as exact operations against
  /// the decoded on-disk state (only changed folders rewritten), and the
  /// folder-override entries are rekeyed/dropped RAW so entries this build
  /// cannot decode still follow their folder.
  func applyRaw(to root: inout [String: Any]) {
    var storedPins = SidebarOrganizationSchema.decode(root: root).pins
    let before = storedPins.byFolder
    apply(to: &storedPins)
    for folder in Set(before.keys).union(storedPins.byFolder.keys)
    where before[folder] != storedPins.byFolder[folder] {
      SidebarOrganizationSchema.setPins(
        &root, folder: folder, paths: storedPins.paths(forFolder: folder))
    }
    for rename in renames where rename.isDirectory != false {
      SidebarOrganizationSchema.renameFolderOverrides(
        &root, from: rename.oldPath, to: rename.newPath)
    }
    SidebarOrganizationSchema.deleteFolderOverrides(
      &root, folders: deletedFolders)
    applyRawShortcuts(to: &root)
  }

  /// FL3-3: retarget/remove file|folder shortcut entries in the RAW
  /// authored array. Reserved kinds keep their bytes and positions;
  /// entries that converge on an existing target drop (source-wins,
  /// like pins).
  private func applyRawShortcuts(to root: inout [String: Any]) {
    guard let raw = root[SidebarOrganizationSchema.shortcutsKey] as? [Any]
    else { return }
    var changed = false
    var seen: Set<String> = []
    var result: [Any] = []
    for entry in raw {
      guard var object = entry as? [String: Any],
        let kindRaw = object["kind"] as? String,
        SidebarShortcut.Kind(rawValue: kindRaw) != nil,
        let path = object["path"] as? String
      else {
        result.append(entry)
        continue
      }
      guard
        let updated = Self.transformedShortcutPath(
          path, kind: kindRaw, renames: renames,
          deletedFiles: deletedFiles, deletedFolders: deletedFolders)
      else {
        changed = true
        continue
      }
      if updated != path { changed = true }
      object["path"] = updated
      if seen.insert("\(kindRaw)\u{0}\(updated)").inserted {
        result.append(object)
      } else {
        changed = true
      }
    }
    if changed {
      root[SidebarOrganizationSchema.shortcutsKey] = result
    }
  }

  /// In-memory counterpart of `applyRawShortcuts` for published state.
  @discardableResult
  func apply(to shortcuts: inout [SidebarShortcut]) -> Bool {
    var changed = false
    var seen: Set<String> = []
    var result: [SidebarShortcut] = []
    for shortcut in shortcuts {
      guard
        let updated = Self.transformedShortcutPath(
          shortcut.path, kind: shortcut.kind.rawValue, renames: renames,
          deletedFiles: deletedFiles, deletedFolders: deletedFolders)
      else {
        changed = true
        continue
      }
      if updated != shortcut.path { changed = true }
      let next = SidebarShortcut(kind: shortcut.kind, path: updated)
      if seen.insert("\(next.kind.rawValue)\u{0}\(next.path)").inserted {
        result.append(next)
      } else {
        changed = true
      }
    }
    if changed { shortcuts = result }
    return changed
  }

  /// One shortcut path through this transform: nil means the target was
  /// deleted. File and folder paths are disjoint namespaces, so exact
  /// matches respect the entry's kind while descendant prefixes apply to
  /// both kinds.
  static func transformedShortcutPath(
    _ path: String, kind: String, renames: [Rename],
    deletedFiles: [String], deletedFolders: [String]
  ) -> String? {
    // FL5-2: tag/untagged shortcuts live in the TAG namespace — a
    // filesystem rename or delete must never rewrite or drop one whose
    // text happens to collide with a file path ("projects/reading" the
    // tag vs projects/ the folder).
    if kind == SidebarShortcut.Kind.tag.rawValue
      || kind == SidebarShortcut.Kind.untagged.rawValue
    {
      return path
    }
    var current = path
    for rename in renames {
      if current == rename.oldPath {
        let kindMatches =
          rename.isDirectory == nil
          || (rename.isDirectory == true && kind == "folder")
          || (rename.isDirectory == false && kind == "file")
        if kindMatches {
          current = rename.newPath
          continue
        }
      }
      let prefix = rename.oldPath + "/"
      if rename.isDirectory != false, current.hasPrefix(prefix) {
        current = rename.newPath + "/" + String(current.dropFirst(prefix.count))
      }
    }
    for folder in deletedFolders {
      if kind == "folder", current == folder { return nil }
      if current.hasPrefix(folder + "/") { return nil }
    }
    if kind == "file", deletedFiles.contains(current) { return nil }
    return current
  }
}

/// Reads and mutates the FL-06 sections of the generic `.slate/sidebar.json`
/// root. Decoding is lenient per key — an unreadable value falls back to its
/// default without discarding siblings — and mutators edit only their own
/// keys so unknown data survives round trips untouched (DoD §FL-E).
enum SidebarOrganizationSchema {
  static let sortKey = "sort"
  static let groupingKey = "grouping"
  /// FL7-2 (#669) per-folder display/scope override keys.
  static let previewLinesKey = "previewLines"
  static let densityKey = "density"
  static let descendantsKey = "descendants"
  static let folderOverridesKey = "folderOverrides"
  static let pinsKey = "pins"
  static let shortcutsKey = "shortcuts"

  /// Top-level shape validation for the FL-06 known sections. A file whose
  /// KNOWN section uses an unrecognized top-level shape (for example `pins`
  /// as an array) likely comes from a newer schema this build cannot merge
  /// into safely — the caller places it into the read-only recovery flow
  /// instead of silently replacing it on the next write (round-5 finding 3).
  /// Per-entry leniency inside a well-shaped section is unchanged.
  /// Round-34: cardinality and length ceilings on vault-authored input.
  /// The store's byte cap bounds what the parse materializes; these bound
  /// what survives decode into published state and the UI. Violations
  /// route into the same read-only recovery flow as malformed JSON —
  /// never silently truncated.
  static let maxPinsPerFolder = 1_000
  static let maxTotalPins = 10_000
  static let maxAuthoredEntries = 10_000
  static let maxAuthoredPathLength = 4_096
  static let maxShortcuts = 200

  static func knownSectionShapesAreValid(root: [String: Any]) -> Bool {
    if let sort = root[sortKey], !(sort is [String: Any]) { return false }
    if let grouping = root[groupingKey], !(grouping is String) { return false }
    if let overrides = root[folderOverridesKey] {
      guard let overrides = overrides as? [String: Any] else { return false }
      guard overrides.count <= maxAuthoredEntries else { return false }
      // Round-7 finding 1: nested mergeability. Every entry must be an
      // object, and its known child fields must have structural types the
      // field-specific mutators can merge into without destroying data.
      for (folder, value) in overrides {
        guard folder.count <= maxAuthoredPathLength else { return false }
        guard let entry = value as? [String: Any] else { return false }
        if let sort = entry[sortKey], !(sort is [String: Any]) { return false }
        if let grouping = entry[groupingKey], !(grouping is String) {
          return false
        }
      }
    }
    if let shortcuts = root[shortcutsKey] {
      guard let entries = shortcuts as? [Any] else { return false }
      guard entries.count <= maxShortcuts else { return false }
      for entry in entries {
        guard let entry = entry as? [String: Any],
          let kind = entry["kind"] as? String,
          let path = entry["path"] as? String
        else { return false }
        // Reserved kinds (tag/untagged, FL5-2) are VALID shapes — kind is
        // an open string here; only its size is bounded.
        guard kind.count <= 64, path.count <= maxAuthoredPathLength else {
          return false
        }
      }
    }
    if let pins = root[pinsKey] {
      guard let pins = pins as? [String: Any] else { return false }
      guard pins.count <= maxAuthoredEntries else { return false }
      var totalPins = 0
      for (folder, value) in pins {
        guard folder.count <= maxAuthoredPathLength else { return false }
        // A pin list that is not purely strings would be truncated by
        // decode and then destroyed by the next exact-op rewrite of that
        // folder — route it into recovery instead.
        guard let paths = value as? [String] else { return false }
        guard paths.count <= maxPinsPerFolder else { return false }
        totalPins += paths.count
        guard totalPins <= maxTotalPins else { return false }
        for path in paths where path.count > maxAuthoredPathLength {
          return false
        }
      }
    }
    return true
  }

  static func decode(
    root: [String: Any]
  ) -> (
    prefs: SidebarOrganizationPrefs, pins: SidebarPins,
    shortcuts: [SidebarShortcut], shortcutRawCount: Int
  ) {
    var prefs = SidebarOrganizationPrefs()
    if let sort = decodeSort(root[sortKey]) {
      prefs.vaultChoice.sort = sort
    }
    if let grouping = decodeGrouping(root[groupingKey]) {
      prefs.vaultChoice.grouping = grouping
    }
    if let overrides = root[folderOverridesKey] as? [String: Any] {
      for (folder, raw) in overrides {
        guard let entry = raw as? [String: Any] else { continue }
        var override = SidebarOrganizationOverride(
          sort: decodeSort(entry[sortKey]),
          grouping: decodeGrouping(entry[groupingKey]))
        if let raw = entry[previewLinesKey] as? Int, (0...3).contains(raw) {
          override.previewLines = raw
        }
        if let raw = entry[densityKey] as? String,
          let density = SidebarRowPreferencesSnapshot.Density(rawValue: raw)
        {
          override.density = density
        }
        if let raw = entry[descendantsKey] as? Bool {
          override.descendants = raw
        }
        if !override.isEmpty {
          prefs.folderOverrides[folder] = override
        }
      }
    }

    var pins = SidebarPins()
    if let rawPins = root[pinsKey] as? [String: Any] {
      for (folder, raw) in rawPins {
        guard let paths = raw as? [String], !paths.isEmpty else { continue }
        // Round-12 finding 1: collapse duplicates at decode (first authored
        // occurrence wins) so no downstream consumer performs per-duplicate
        // work on an adversarially repetitive list.
        var seen: Set<String> = []
        let deduped = paths.filter { seen.insert($0).inserted }
        pins.replacePaths(deduped, forFolder: folder)
      }
    }
    return (
      prefs, pins, decodeShortcuts(root: root),
      (root[shortcutsKey] as? [Any])?.count ?? 0
    )
  }

  /// FL3-3: decode the file|folder shortcuts the UI ships now. Reserved
  /// kinds stay only in the raw array (rewrites preserve them); duplicates
  /// collapse first-occurrence-wins, like pins.
  static func decodeShortcuts(root: [String: Any]) -> [SidebarShortcut] {
    guard let raw = root[shortcutsKey] as? [Any] else { return [] }
    var seen: Set<String> = []
    var result: [SidebarShortcut] = []
    for entry in raw {
      guard let entry = entry as? [String: Any],
        let kindRaw = entry["kind"] as? String,
        let kind = SidebarShortcut.Kind(rawValue: kindRaw),
        let path = entry["path"] as? String
      else { continue }
      if seen.insert("\(kindRaw)\u{0}\(path)").inserted {
        result.append(SidebarShortcut(kind: kind, path: path))
      }
    }
    return result
  }

  private static func shortcutEntryMatches(
    _ entry: Any, kind: String, path: String
  ) -> Bool {
    guard let entry = entry as? [String: Any] else { return false }
    return entry["kind"] as? String == kind && entry["path"] as? String == path
  }

  /// Exact-op shortcut mutations operate on the RAW authored array so
  /// reserved kinds survive positionally and unknown entry keys survive
  /// byte-for-byte.
  static func addShortcut(
    _ root: inout [String: Any], kind: String, path: String
  ) {
    var raw = (root[shortcutsKey] as? [Any]) ?? []
    guard
      !raw.contains(where: { shortcutEntryMatches($0, kind: kind, path: path) })
    else { return }
    raw.append(["kind": kind, "path": path] as [String: Any])
    root[shortcutsKey] = raw
  }

  static func removeShortcut(
    _ root: inout [String: Any], kind: String, path: String
  ) {
    guard var raw = root[shortcutsKey] as? [Any] else { return }
    raw.removeAll { shortcutEntryMatches($0, kind: kind, path: path) }
    root[shortcutsKey] = raw
  }

  /// Move a VISIBLE (file|folder) shortcut one visible slot: the swap
  /// partner is the nearest decodable neighbor, skipping reserved
  /// entries, and the two RAW positions exchange. Edges clamp to no-ops.
  static func moveShortcut(
    _ root: inout [String: Any], kind: String, path: String, delta: Int
  ) {
    guard delta == 1 || delta == -1,
      var raw = root[shortcutsKey] as? [Any],
      let index = raw.firstIndex(where: {
        shortcutEntryMatches($0, kind: kind, path: path)
      })
    else { return }
    var partner = index + delta
    while partner >= 0, partner < raw.count {
      if let entry = raw[partner] as? [String: Any],
        let kindRaw = entry["kind"] as? String,
        SidebarShortcut.Kind(rawValue: kindRaw) != nil,
        entry["path"] is String
      {
        raw.swapAt(index, partner)
        root[shortcutsKey] = raw
        return
      }
      partner += delta
    }
  }

  private static func decodeSort(_ raw: Any?) -> SidebarSortOption? {
    guard let entry = raw as? [String: Any],
      let fieldRaw = entry["field"] as? String,
      let field = SidebarSortField(rawValue: fieldRaw),
      let directionRaw = entry["direction"] as? String,
      let direction = SidebarSortDirection(rawValue: directionRaw)
    else { return nil }
    return SidebarSortOption(field: field, direction: direction)
  }

  private static func decodeGrouping(_ raw: Any?) -> SidebarGroupingOption? {
    guard let value = raw as? String else { return nil }
    return SidebarGroupingOption(rawValue: value)
  }

  /// Merges field/direction into an existing sort object so an additive
  /// unknown member inside `sort` survives every write (DoD §FL-E).
  private static func mergeSort(
    _ sort: SidebarSortOption, into existing: Any?
  ) -> [String: Any] {
    var entry = existing as? [String: Any] ?? [:]
    entry["field"] = sort.field.rawValue
    entry["direction"] = sort.direction.rawValue
    return entry
  }

  /// Field-specific vault mutators: each touches ONLY its own key so an
  /// interleaved writer's change to the other axis — or a raw value this
  /// build cannot decode — is never restored from a stale snapshot.
  static func setVaultSort(_ root: inout [String: Any], _ sort: SidebarSortOption) {
    root[sortKey] = mergeSort(sort, into: root[sortKey])
  }

  static func setVaultGrouping(
    _ root: inout [String: Any], _ grouping: SidebarGroupingOption
  ) {
    root[groupingKey] = grouping.rawValue
  }

  /// Field-specific folder-override mutators, same single-axis rule.
  static func setFolderSort(
    _ root: inout [String: Any], folder: String, _ sort: SidebarSortOption
  ) {
    var overrides = root[folderOverridesKey] as? [String: Any] ?? [:]
    var entry = overrides[folder] as? [String: Any] ?? [:]
    entry[sortKey] = mergeSort(sort, into: entry[sortKey])
    overrides[folder] = entry
    root[folderOverridesKey] = overrides
  }

  static func setFolderGrouping(
    _ root: inout [String: Any], folder: String, _ grouping: SidebarGroupingOption
  ) {
    var overrides = root[folderOverridesKey] as? [String: Any] ?? [:]
    var entry = overrides[folder] as? [String: Any] ?? [:]
    entry[groupingKey] = grouping.rawValue
    overrides[folder] = entry
    root[folderOverridesKey] = overrides
  }

  /// FL7-2 display/scope override mutators — single-axis writes, and
  /// clears drop the key (and the entry once no KNOWN key remains and
  /// no unknown keys were authored — the FL-06 leniency discipline).
  static func setFolderOverrideValue(
    _ root: inout [String: Any], folder: String, key: String, value: Any?
  ) {
    var overrides = root[folderOverridesKey] as? [String: Any] ?? [:]
    var entry = overrides[folder] as? [String: Any] ?? [:]
    if let value {
      entry[key] = value
    } else {
      entry.removeValue(forKey: key)
    }
    if entry.isEmpty {
      overrides.removeValue(forKey: folder)
    } else {
      overrides[folder] = entry
    }
    if overrides.isEmpty {
      root.removeValue(forKey: folderOverridesKey)
    } else {
      root[folderOverridesKey] = overrides
    }
  }

  /// Removes only the folder's grouping override so the folder returns to
  /// vault inheritance (round-6 finding 3); sort and unknown entry keys
  /// survive, and the entry disappears only when empty.
  static func clearFolderGroupingOverride(_ root: inout [String: Any], folder: String) {
    guard var overrides = root[folderOverridesKey] as? [String: Any],
      var entry = overrides[folder] as? [String: Any]
    else { return }
    entry[groupingKey] = nil
    overrides[folder] = entry.isEmpty ? nil : entry
    root[folderOverridesKey] = overrides.isEmpty ? nil : overrides
  }

  /// Removes only the folder's sort override, preserving a grouping override
  /// and any unknown entry keys; the entry disappears only when empty.
  static func clearFolderSortOverride(_ root: inout [String: Any], folder: String) {
    guard var overrides = root[folderOverridesKey] as? [String: Any],
      var entry = overrides[folder] as? [String: Any]
    else { return }
    entry[sortKey] = nil
    overrides[folder] = entry.isEmpty ? nil : entry
    root[folderOverridesKey] = overrides.isEmpty ? nil : overrides
  }


  /// Raw structural replay over `folderOverrides`: rekeys or drops the stored
  /// entry dictionaries themselves so unknown inner keys ride along with the
  /// folder they describe.
  static func renameFolderOverrides(
    _ root: inout [String: Any], from oldFolder: String, to newFolder: String
  ) {
    guard var overrides = root[folderOverridesKey] as? [String: Any] else { return }
    let oldPrefix = oldFolder + "/"
    let newPrefix = newFolder + "/"
    var next: [String: Any] = [:]
    next.reserveCapacity(overrides.count)
    var renamed: [(key: String, entry: Any)] = []
    for (folder, entry) in overrides {
      if folder == oldFolder {
        renamed.append((newFolder, entry))
      } else if folder.hasPrefix(oldPrefix) {
        renamed.append((newPrefix + folder.dropFirst(oldPrefix.count), entry))
      } else {
        next[folder] = entry
      }
    }
    guard !renamed.isEmpty else { return }
    // Renamed source wins over a stale destination entry (round-8 finding 3).
    for item in renamed {
      next[item.key] = item.entry
    }
    root[folderOverridesKey] = next.isEmpty ? nil : next
  }

  static func deleteFolderOverrides(
    _ root: inout [String: Any], folders deletedFolders: [String]
  ) {
    guard !deletedFolders.isEmpty,
      var overrides = root[folderOverridesKey] as? [String: Any]
    else { return }
    let prefixes = deletedFolders.map { $0 + "/" }
    let doomed = overrides.keys.filter { key in
      deletedFolders.contains(key) || prefixes.contains { key.hasPrefix($0) }
    }
    guard !doomed.isEmpty else { return }
    for key in doomed { overrides[key] = nil }
    root[folderOverridesKey] = overrides.isEmpty ? nil : overrides
  }

  static func setPins(_ root: inout [String: Any], folder: String, paths: [String]) {
    var pins = root[pinsKey] as? [String: Any] ?? [:]
    pins[folder] = paths.isEmpty ? nil : paths
    root[pinsKey] = pins.isEmpty ? nil : pins
  }
}
