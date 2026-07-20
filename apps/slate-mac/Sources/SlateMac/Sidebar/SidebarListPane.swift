// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// FL7-2 (#669): the COMPLETE list-pane content pipeline. Containers
/// drain to completion (async, cancellable, budget-capped) through the
/// existing core contracts — folder → one `list_dir_children` level or
/// the FL-08 descendant scope, tag → `filter_files(#tag)`, Untagged →
/// its reserved scope — then the FL-06 `SidebarLevelOrganizer` applies
/// pins, sort, and grouping client-side over the COMPLETE set (the
/// same engine, same overrides storage, one more projection).
@MainActor
final class SidebarListPaneModel: ObservableObject {
  struct Dependencies {
    /// FL4-1 scoped/queried listing (descendants + tags).
    var performQuery:
      (
        _ query: String, _ scopeDir: String?, _ scopeTag: String?,
        _ paging: Paging
      ) throws
        -> SidebarFilterPage
    var performUntagged: (_ paging: Paging) throws -> SidebarFilterPage
    /// One-level listing (the non-descendant folder default).
    var listLevel:
      (_ parentPath: String, _ paging: Paging) throws -> DirListing
  }

  enum Row: Equatable, Identifiable {
    case header(key: String, label: String, count: Int)
    case file(FileSummary)

    var id: String {
      switch self {
      case .header(let key, _, _): return "header:\(key)"
      case .file(let summary): return summary.path
      }
    }
  }

  static let pageSize: UInt32 = 500
  /// The FL budget scale: a scope past this drains no further and the
  /// truncation is announced in the header — never silent (§FL rules).
  static let drainCap = 10_000

  @Published private(set) var container: SidebarContainer?
  @Published private(set) var rows: [Row] = []
  @Published private(set) var fileCount = 0
  @Published private(set) var truncated = false
  @Published private(set) var isLoading = false
  @Published private(set) var loadError: String?

  private var dependencies: Dependencies?
  private var organization: (() -> FileTreeViewModel.OrganizationContext)?
  private var deviceDefaults:
    (() -> (previewLines: Int, density: SidebarRowPreferencesSnapshot.Density))?
  private(set) var drainTaskForTesting: Task<Void, Never>?
  private var generation = 0

  func bind(
    _ dependencies: Dependencies,
    organization: @escaping () -> FileTreeViewModel.OrganizationContext,
    deviceDefaults: @escaping () -> (
      previewLines: Int, density: SidebarRowPreferencesSnapshot.Density
    )
  ) {
    self.dependencies = dependencies
    self.organization = organization
    self.deviceDefaults = deviceDefaults
  }

  func resetForVaultClose() {
    generation += 1
    drainTaskForTesting?.cancel()
    drainTaskForTesting = nil
    dependencies = nil
    organization = nil
    deviceDefaults = nil
    container = nil
    rows = []
    fileCount = 0
    truncated = false
    isLoading = false
    loadError = nil
  }

  /// The row-preference snapshot for THIS container's rows: device
  /// defaults with the folder's per-container overrides applied (one
  /// storage, two projections — tree rows read the same values).
  func rowPreferences(
    base: SidebarRowPreferencesSnapshot
  ) -> SidebarRowPreferencesSnapshot {
    guard let organization, case .folder(let path) = container else {
      return base
    }
    let prefs = organization().prefs
    var snapshot = base
    snapshot.previewLines = prefs.effectivePreviewLines(
      forFolder: path, default: base.previewLines)
    snapshot.density = prefs.effectiveDensity(
      forFolder: path, default: base.density)
    return snapshot
  }

  /// Select (or clear) the container: supersedes any pending drain,
  /// then drains the scope to completion as an awaitable main-actor
  /// task — the filter model's pattern — and organizes wholesale (VO
  /// stability — one replacement per settle). The drain yields between
  /// pages (review round), so a superseding `show` cancels it
  /// MID-drain — not just before it starts — and the main actor stays
  /// responsive while large scopes page; any stale result that still
  /// completes is dropped by the generation guard.
  func show(_ container: SidebarContainer?) {
    generation += 1
    let myGeneration = generation
    drainTaskForTesting?.cancel()
    // Review round (high): a CONTAINER SWITCH clears the published
    // content immediately — the old container's rows must never render
    // under the new header while the drain runs. A same-container
    // refresh keeps the wholesale-on-settle replacement (no flicker).
    if container != self.container {
      rows = []
      fileCount = 0
      truncated = false
    }
    self.container = container
    guard let container, let dependencies else {
      rows = []
      fileCount = 0
      truncated = false
      isLoading = false
      loadError = nil
      return
    }
    isLoading = true
    loadError = nil
    let descendants = includesDescendants(for: container)
    drainTaskForTesting = Task { [weak self] in
      let outcome: Result<(files: [FileSummary], truncated: Bool), Error>
      do {
        let drained = try await Self.drain(
          container, descendants: descendants, dependencies: dependencies)
        outcome = .success(drained)
      } catch {
        outcome = .failure(error)
      }
      guard let self, !Task.isCancelled, self.generation == myGeneration
      else { return }
      self.isLoading = false
      switch outcome {
      case .success(let drained):
        self.truncated = drained.truncated
        self.fileCount = drained.files.count
        self.rows = self.organizedRows(
          files: drained.files, container: container)
      case .failure(let error):
        self.rows = []
        self.fileCount = 0
        self.truncated = false
        self.loadError = error.localizedDescription
      }
    }
  }

  func refreshAfterStructuralMutation() {
    guard container != nil else { return }
    show(container)
  }

  /// Ordered file summaries (headers skipped) — selection ranges and
  /// snapshot publication read these.
  var fileSummaries: [FileSummary] {
    rows.compactMap {
      if case .file(let summary) = $0 { return summary }
      return nil
    }
  }

  func summary(at path: String) -> FileSummary? {
    for case .file(let summary) in rows where summary.path == path {
      return summary
    }
    return nil
  }

  func includesDescendants(for container: SidebarContainer) -> Bool {
    guard let organization, case .folder(let path) = container else {
      // Tag containers are inherently descendant-inclusive (rule 2).
      return true
    }
    return organization().prefs.includesDescendants(forFolder: path)
  }

  // MARK: - Pipeline

  private static func drain(
    _ container: SidebarContainer,
    descendants: Bool,
    dependencies: Dependencies
  ) async throws -> (files: [FileSummary], truncated: Bool) {
    var files: [FileSummary] = []
    var cursor: String?
    while files.count < drainCap {
      let paging = Paging(cursor: cursor, limit: pageSize)
      let (page, next): ([FileSummary], String?)
      switch container {
      case .folder(let path) where !descendants:
        let listing = try dependencies.listLevel(path, paging)
        (page, next) = (listing.files.items, listing.files.nextCursor)
      case .folder(let path):
        let result = try dependencies.performQuery("", path, nil, paging)
        (page, next) = (result.files, result.nextCursor)
      case .tag(let full):
        // FL-15 red team (high): the tag scopes via the core
        // parameter — text interpolation would re-tokenize a tag
        // containing whitespace and drain the WRONG file set.
        let result = try dependencies.performQuery("", nil, full, paging)
        (page, next) = (result.files, result.nextCursor)
      case .untagged:
        let result = try dependencies.performUntagged(paging)
        (page, next) = (result.files, result.nextCursor)
      }
      files.append(contentsOf: page)
      guard let nextCursor = next else {
        return (files, false)
      }
      cursor = nextCursor
      // The yield is what makes cancellation land MID-drain: a
      // superseding show() runs during it, and checkCancellation
      // throws before the next page is requested.
      await Task.yield()
      try Task.checkCancellation()
    }
    return (files, true)
  }

  private func organizedRows(
    files: [FileSummary], container: SidebarContainer
  ) -> [Row] {
    guard let organization else {
      return files.map(Row.file)
    }
    let context = organization()
    let scopeFolder: String
    let pinnedPaths: [String]
    switch container {
    case .folder(let path):
      scopeFolder = path
      pinnedPaths = context.pins.paths(forFolder: path)
    case .tag, .untagged:
      scopeFolder = ""
      pinnedPaths = []
    }
    let organizerFiles = files.map {
      SidebarOrganizerFile(
        path: $0.path,
        name: $0.name,
        displayName: $0.displayName,
        createdDate: $0.createdDate,
        createdMs: $0.createdMs,
        mtimeMs: $0.mtimeMs)
    }
    let organized = SidebarLevelOrganizer.organize(
      files: organizerFiles,
      choice: context.prefs.effectiveChoice(forFolder: scopeFolder),
      pinnedPaths: pinnedPaths,
      now: context.now,
      calendar: context.calendar,
      locale: context.locale,
      civilDateResolver: context.civilDateResolver)

    var byPath: [String: FileSummary] = [:]
    byPath.reserveCapacity(files.count)
    for file in files { byPath[file.path] = file }

    var headersBefore: [String: Row] = [:]
    if organized.pinnedCount > 0, let first = organized.orderedPaths.first {
      headersBefore[first] = .header(
        key: "pinned", label: "Pinned", count: organized.pinnedCount)
    }
    for group in organized.groups {
      headersBefore[group.firstPath] = .header(
        key: group.key, label: group.label, count: group.fileCount)
    }

    var rows: [Row] = []
    rows.reserveCapacity(organized.orderedPaths.count + headersBefore.count)
    for path in organized.orderedPaths {
      if let header = headersBefore[path] {
        rows.append(header)
      }
      if let summary = byPath[path] {
        rows.append(.file(summary))
      }
    }
    return rows
  }
}

/// FL7-2 rule 3: the list-pane header's display menu — Sort and
/// Grouping ride the SAME catalog evaluations the tree uses (no second
/// command dialect); folder containers add Include Subfolders and the
/// preview-lines/density overrides with "Use Vault Default" clears.
/// The same component mounts in the tree's folder context menu, which
/// is what makes the §FL-D parity structural rather than aspirational.
struct SidebarListPaneDisplayMenu: View {
  @EnvironmentObject private var appState: AppState
  let container: SidebarContainer?

  var body: some View {
    Menu {
      sortAndGroupSection
      if case .folder(let path) = container {
        Divider()
        SidebarFolderDisplayOverrideItems(folder: path, showsDescendants: true)
      }
    } label: {
      SlateSymbol.sortOrder.decorative
        .foregroundStyle(Tokens.ColorRole.textSecondary)
    }
    .menuStyle(.borderlessButton)
    .menuIndicator(.hidden)
    .fixedSize()
    .accessibilityLabel("Display options")
    .accessibilityHint("Sort, grouping, and per-folder display overrides.")
  }

  /// The catalog sort/group family evaluated against the CURRENT
  /// published snapshot: a folder container targets that folder's
  /// override; empty/tag selections target the vault default —
  /// exactly the FL-06 dispatch semantics.
  @ViewBuilder
  private var sortAndGroupSection: some View {
    // Review round (medium): Sort/Grouping act on the CONTAINER this
    // menu is labeled with — never the ambient row snapshot, which may
    // be a selected file (whose parent would silently take the
    // override) or a batch (which would drop the controls).
    let evaluations = appState.sidebarActionProjection(
        surface: .contextMenu,
        snapshot: appState.sidebarContainerActionSnapshot(for: container))
    let sortIDs = [
      SlateCommandID.sidebarSortNameAsc, SlateCommandID.sidebarSortNameDesc,
      SlateCommandID.sidebarSortCreatedDesc,
      SlateCommandID.sidebarSortCreatedAsc,
      SlateCommandID.sidebarSortModifiedDesc,
      SlateCommandID.sidebarSortModifiedAsc,
      SlateCommandID.sidebarToggleDateGrouping,
      SlateCommandID.sidebarUseVaultDefaultSort,
    ]
    ForEach(
      sortIDs.compactMap { id in evaluations.first { $0.id == id } },
      id: \.id
    ) { evaluation in
      Button {
        guard let intent = evaluation.intent else { return }
        do {
          _ = try appState.dispatchSidebarAction(intent)
        } catch {
          appState.postMutationAnnouncement(error.sidebarActionAnnouncement)
        }
      } label: {
        evaluation.definition.symbol.label(evaluation.definition.label)
      }
    }
  }
}

/// FL7-2 rule 3: the per-folder display-override items — ONE shared
/// component for the list-pane header menu AND the tree's folder
/// context menu (one storage, two projections, one menu).
struct SidebarFolderDisplayOverrideItems: View {
  @EnvironmentObject private var appState: AppState
  let folder: String
  var showsDescendants = false

  var body: some View {
    if showsDescendants {
      Toggle(
        "Include Subfolders",
        isOn: Binding(
          get: {
            appState.sidebarOrganization.prefs.includesDescendants(
              forFolder: folder)
          },
          set: { include in
            appState.setSidebarFolderDescendantsOverride(
              folder: folder, includeDescendants: include)
          }))
    }
    Menu("Preview Lines") {
      ForEach(0...3, id: \.self) { lines in
        Button("\(lines)") {
          appState.setSidebarFolderPreviewLinesOverride(
            folder: folder, lines: lines)
        }
      }
      Divider()
      Button("Use Vault Default") {
        appState.setSidebarFolderPreviewLinesOverride(
          folder: folder, lines: nil)
      }
    }
    Menu("Density") {
      ForEach(
        SidebarRowPreferencesSnapshot.Density.allCases, id: \.rawValue
      ) { density in
        Button(density == .standard ? "Standard" : "Compact") {
          appState.setSidebarFolderDensityOverride(
            folder: folder, density: density)
        }
      }
      Divider()
      Button("Use Vault Default") {
        appState.setSidebarFolderDensityOverride(
          folder: folder, density: nil)
      }
    }
  }
}
