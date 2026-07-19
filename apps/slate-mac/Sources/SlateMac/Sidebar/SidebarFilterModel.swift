// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// FL4-2 (#663): app-side date-window construction. Core owns the
/// grammar and requirements; the APP owns boundary instants, built with
/// an injected `now`, an explicitly Gregorian calendar, and the actual
/// time zone — never a fixed offset, `Calendar.current`, 86 400-second
/// arithmetic, or UTC-midnight encoding (spec rule 3).
enum SidebarFilterWindowBuilder {
  /// One exact half-open `[start, end)` window per canonical requirement
  /// (`@today`, `@yesterday`, `@last7d`, `@last30d`, `@YYYY-MM-DD`).
  /// Returns nil when a literal date cannot resolve — the caller surfaces
  /// it as the term's inline error rather than guessing a boundary.
  static func windows(
    forRequirements requirements: [String],
    now: Date,
    timeZone: TimeZone,
    resolver: SidebarCivilDateResolving
  ) -> [SidebarFilterDateWindow]? {
    var calendar = Calendar(identifier: .gregorian)
    calendar.timeZone = timeZone

    func startOfDay(_ date: Date) -> Date {
      calendar.startOfDay(for: date)
    }
    func addDays(_ days: Int, to date: Date) -> Date? {
      calendar.date(byAdding: .day, value: days, to: date)
    }

    let todayStart = startOfDay(now)
    guard let tomorrowStart = addDays(1, to: todayStart) else { return nil }

    var windows: [SidebarFilterDateWindow] = []
    for requirement in requirements {
      let bounds: (Date, Date)?
      switch requirement {
      case "@today":
        bounds = (todayStart, tomorrowStart)
      case "@yesterday":
        bounds = addDays(-1, to: todayStart).map { ($0, todayStart) }
      case "@last7d":
        bounds = addDays(-6, to: todayStart).map { ($0, tomorrowStart) }
      case "@last30d":
        bounds = addDays(-29, to: todayStart).map { ($0, tomorrowStart) }
      default:
        guard requirement.hasPrefix("@"),
          let dayStart = resolver.resolve(
            String(requirement.dropFirst()), calendar: calendar),
          let dayEnd = addDays(1, to: dayStart)
        else { return nil }
        bounds = (dayStart, dayEnd)
      }
      guard let (start, end) = bounds else { return nil }
      windows.append(
        SidebarFilterDateWindow(
          term: requirement,
          startMs: Int64((start.timeIntervalSince1970 * 1000).rounded()),
          endMs: Int64((end.timeIntervalSince1970 * 1000).rounded())))
    }
    return windows
  }
}

/// FL4-2 (#663): the sidebar filter's state machine. A committed
/// non-empty query overlays the sections/tree with a flat paged result
/// list; the underlying tree state is never torn down. Results replace
/// wholesale (VoiceOver stability), parse errors render inline while the
/// previous good results stay visible, and the last committed query is
/// restored into the field — but never applied — on relaunch.
@MainActor
final class SidebarFilterModel: ObservableObject {
  struct Dependencies {
    var requirements: (String) throws -> [String]
    var perform:
      (_ query: String, _ windows: [SidebarFilterDateWindow], _ paging: Paging)
        throws -> SidebarFilterPage
    /// FL5-2 (#665): the reserved Untagged scope — same page shape,
    /// same presentation, dedicated core call.
    var performUntagged: (_ paging: Paging) throws -> SidebarFilterPage = { _ in
      SidebarFilterPage(files: [], nextCursor: nil, total: 0, audioSummary: "No results.")
    }
    var announce: (String) -> Void
    var now: () -> Date
    var timeZone: () -> TimeZone
    var resolver: SidebarCivilDateResolving
    var defaults: UserDefaults
    /// Debounce interval; tests inject zero.
    var debounceNanoseconds: UInt64 = 200_000_000
  }

  static let persistedQueryKey = "slate.sidebar.filterQuery"
  static let pageLimit: UInt32 = 200

  @Published var fieldText = ""
  @Published private(set) var committedQuery = ""
  @Published private(set) var results: SidebarFilterPage?
  @Published private(set) var inlineError: String?
  /// FL5-2 (#665): true while the reserved Untagged scope owns the
  /// overlay. Not persisted (restore-not-apply covers query TEXT only);
  /// any field edit or Esc exits back to a normal query/tree.
  @Published private(set) var untaggedScope = false

  /// Announce-dedup sentinel for the Untagged scope; NUL cannot appear
  /// in a typed query, so it can never collide with one.
  private static let untaggedAnnounceKey = "\u{0}untagged"

  /// True while a committed non-empty query or the Untagged scope owns
  /// the result surface.
  var isActive: Bool { !committedQuery.isEmpty || untaggedScope }

  private var dependencies: Dependencies?
  private var lastAnnouncement: (query: String, total: UInt64)?
  /// Dedup token for spoken error feedback (review round: the inline
  /// text alone is silent for VoiceOver — rule 4 requires a polite
  /// announcement). Distinct from the results dedup so an error →
  /// same-results → same-error cycle still re-announces the error.
  private var lastErrorAnnouncement: String?
  /// The exact windows the committed page ran with — reused verbatim by
  /// `loadNextPage` so every page of one committed query sees identical
  /// boundaries even across a rollover (rule 7's recompute is explicit).
  private var activeWindows: [SidebarFilterDateWindow] = []
  /// Value token for the latest `fieldText` write the model made
  /// itself. The view forwards every `onChange(of: fieldText)` into
  /// `fieldTextChanged()`; restore/clear writes must not read as
  /// keystrokes or bind() would silently APPLY the restored query. A
  /// value (not a counter) so SwiftUI coalescing several same-body
  /// writes into one delivery can never strand a token that would
  /// swallow a later real keystroke.
  private var pendingProgrammaticFieldText: String?
  private(set) var pendingCommitTaskForTesting: Task<Void, Never>?

  func bind(_ dependencies: Dependencies) {
    self.dependencies = dependencies
    // Restore-not-apply (spec rule 6): the field shows the persisted
    // query; ⏎ or an edit re-applies it. Waking up trapped in a filtered
    // view is the failure mode this exists to prevent.
    let restored =
      dependencies.defaults.string(forKey: Self.persistedQueryKey) ?? ""
    if restored != fieldText {
      pendingProgrammaticFieldText = restored
      fieldText = restored
    }
  }

  /// Field edits debounce 200 ms after the last keystroke; each edit
  /// cancels the in-flight wait. Programmatic writes (restore, Esc
  /// clear) consume their suppression token here and do nothing.
  func fieldTextChanged() {
    if let token = pendingProgrammaticFieldText {
      pendingProgrammaticFieldText = nil
      if token == fieldText { return }
    }
    pendingCommitTaskForTesting?.cancel()
    guard let dependencies else { return }
    let interval = dependencies.debounceNanoseconds
    pendingCommitTaskForTesting = Task { [weak self] in
      if interval > 0 {
        try? await Task.sleep(nanoseconds: interval)
      }
      guard !Task.isCancelled else { return }
      self?.commit()
    }
  }

  /// ⏎ in the field: cancel any pending debounce and commit right now.
  func commitNow() {
    pendingCommitTaskForTesting?.cancel()
    commit()
  }

  /// Operator-menu insertion: append the operator to the field (spaced
  /// as a fresh term) and start the ordinary debounce, exactly like a
  /// keystroke. The menu returns focus to the field; the field's
  /// `onChange` observation stays the single commit trigger.
  func insertOperator(_ text: String) {
    if fieldText.isEmpty || fieldText.hasSuffix(" ") {
      fieldText += text
    } else {
      fieldText += " \(text)"
    }
  }

  /// Commit the field's current text: run it, replace results wholesale,
  /// persist it. Empty text deactivates and returns to the tree.
  /// Persistence records only *committed* states (spec rule 6): a query
  /// that fails to parse never becomes the restored text.
  func commit() {
    guard let dependencies else { return }
    // A text commit (or an emptied field) always exits the Untagged
    // scope: the field is the single source of the overlay's mode.
    untaggedScope = false
    let query = fieldText.trimmingCharacters(in: .whitespaces)
    guard !query.isEmpty else {
      committedQuery = ""
      results = nil
      inlineError = nil
      lastErrorAnnouncement = nil
      dependencies.defaults.set("", forKey: Self.persistedQueryKey)
      return
    }
    runQuery(query, dependencies: dependencies)
  }

  /// FL5-2 rule 3: a tag row (or tag shortcut) drives the overlay with
  /// the query `#<full>` — shown in the field, editable, teaching the
  /// grammar. Identical lifecycle to typing it.
  func activateTagQuery(_ query: String) {
    pendingCommitTaskForTesting?.cancel()
    if fieldText != query {
      pendingProgrammaticFieldText = query
      fieldText = query
    }
    commitNow()
  }

  /// FL5-2 rule 3: the reserved Untagged scope — a dedicated core call,
  /// the SAME flat-list presentation and announce seam. The field
  /// empties (there is no textual grammar for untagged); editing it
  /// exits the scope.
  func activateUntaggedScope() {
    guard let dependencies else { return }
    pendingCommitTaskForTesting?.cancel()
    if !fieldText.isEmpty {
      pendingProgrammaticFieldText = ""
      fieldText = ""
    }
    committedQuery = ""
    do {
      let page = try dependencies.performUntagged(
        Paging(cursor: nil, limit: Self.pageLimit))
      untaggedScope = true
      results = page
      inlineError = nil
      lastErrorAnnouncement = nil
      dependencies.defaults.set("", forKey: Self.persistedQueryKey)
      if lastAnnouncement?.query != Self.untaggedAnnounceKey
        || lastAnnouncement?.total != page.total
      {
        lastAnnouncement = (Self.untaggedAnnounceKey, page.total)
        dependencies.announce(page.audioSummary)
      }
    } catch {
      setInlineError(errorMessage(for: error), dependencies: dependencies)
    }
  }

  /// Fetch the next page for the committed query and extend the list.
  /// The published page is still swapped as one value; the announce
  /// dedup key (query, total) is unchanged by paging, so VoiceOver
  /// hears nothing new.
  func loadNextPage() {
    guard let dependencies, isActive,
      let current = results, let cursor = current.nextCursor
    else { return }
    do {
      let page =
        untaggedScope
        ? try dependencies.performUntagged(
          Paging(cursor: cursor, limit: Self.pageLimit))
        : try dependencies.perform(
          committedQuery,
          activeWindows,
          Paging(cursor: cursor, limit: Self.pageLimit))
      results = SidebarFilterPage(
        files: current.files + page.files,
        nextCursor: page.nextCursor,
        total: page.total,
        audioSummary: page.audioSummary)
    } catch {
      setInlineError(errorMessage(for: error), dependencies: dependencies)
    }
  }

  private func runQuery(_ query: String, dependencies: Dependencies) {
    do {
      let requirements = try dependencies.requirements(query)
      guard
        let windows = SidebarFilterWindowBuilder.windows(
          forRequirements: requirements,
          now: dependencies.now(),
          timeZone: dependencies.timeZone(),
          resolver: dependencies.resolver)
      else {
        setInlineError(
          "That date can't be resolved in this calendar.",
          dependencies: dependencies)
        return
      }
      let page = try dependencies.perform(
        query, windows, Paging(cursor: nil, limit: Self.pageLimit))
      committedQuery = query
      activeWindows = windows
      results = page
      inlineError = nil
      lastErrorAnnouncement = nil
      dependencies.defaults.set(query, forKey: Self.persistedQueryKey)
      if lastAnnouncement?.query != query
        || lastAnnouncement?.total != page.total
      {
        lastAnnouncement = (query, page.total)
        dependencies.announce(page.audioSummary)
      }
    } catch {
      // InvalidQuery renders inline naming the bad term; the previous
      // good results stay visible (spec rule 4).
      setInlineError(errorMessage(for: error), dependencies: dependencies)
    }
  }

  /// Rule 4's polite live region: the inline text is the visual, the
  /// announce seam is the spoken channel (priority .medium = polite).
  /// Deduped per distinct message so a keystroke stream over one broken
  /// term doesn't spam; a NEW error message always speaks.
  private func setInlineError(_ message: String, dependencies: Dependencies) {
    inlineError = message
    if lastErrorAnnouncement != message {
      lastErrorAnnouncement = message
      dependencies.announce(message)
    }
  }

  private func errorMessage(for error: Error) -> String {
    if let vaultError = error as? VaultError,
      case .InvalidQuery(let message) = vaultError
    {
      return message
    }
    return error.localizedDescription
  }

  /// Esc in the field clears the query and returns to the tree with the
  /// prior expansion/selection intact (the tree was never torn down).
  func escapeInField() {
    pendingCommitTaskForTesting?.cancel()
    if !fieldText.isEmpty {
      pendingProgrammaticFieldText = ""
      fieldText = ""
    }
    committedQuery = ""
    untaggedScope = false
    results = nil
    inlineError = nil
    lastErrorAnnouncement = nil
    dependencies?.defaults.set("", forKey: Self.persistedQueryKey)
  }

  /// Vault close: drop results and bindings, keep the device-local
  /// persisted query untouched (it restores into the next bind's field).
  func resetForVaultClose() {
    pendingCommitTaskForTesting?.cancel()
    pendingCommitTaskForTesting = nil
    dependencies = nil
    if !fieldText.isEmpty {
      pendingProgrammaticFieldText = ""
      fieldText = ""
    }
    committedQuery = ""
    untaggedScope = false
    results = nil
    inlineError = nil
    lastAnnouncement = nil
    lastErrorAnnouncement = nil
    activeWindows = []
  }

  /// A structural mutation (rename, move, trash, duplicate) completed
  /// while the overlay is active: the committed query re-runs so the
  /// flat list can't keep showing a renamed or deleted row (review
  /// round). Wholesale replacement; the announce dedup keys on
  /// (query, total), so an unchanged count stays silent.
  func refreshAfterStructuralMutation() {
    guard let dependencies, isActive else { return }
    if untaggedScope {
      if let page = try? dependencies.performUntagged(
        Paging(cursor: nil, limit: Self.pageLimit))
      {
        results = page
      }
      return
    }
    runQuery(committedQuery, dependencies: dependencies)
  }

  /// Local-day rollover / system time-zone change: recompute ONLY when a
  /// date term is active — never cache one offset across a DST change.
  func handleDayRolloverOrTimeZoneChange() {
    guard let dependencies, isActive, !committedQuery.isEmpty else { return }
    let hasDateTerm =
      (try? dependencies.requirements(committedQuery))?.isEmpty == false
    guard hasDateTerm else { return }
    runQuery(committedQuery, dependencies: dependencies)
  }
}
