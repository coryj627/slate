// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// FL7-1 (#668): the list-pane content model for the selected
/// container. Deliberately lean — FL7-2 (FL-14) delivers the complete
/// list pane (sort/grouping overrides, full parity); this model gives
/// the gated container its honest paged contents through the SAME core
/// contracts the filter overlay uses (scoped listing, tag query,
/// untagged scope — no second query path).
@MainActor
final class SidebarContainerListModel: ObservableObject {
  struct Dependencies {
    /// Scoped/queried listing — the FL4-1 contract.
    var performQuery:
      (_ query: String, _ scopeDir: String?, _ paging: Paging) throws
        -> SidebarFilterPage
    var performUntagged: (_ paging: Paging) throws -> SidebarFilterPage
  }

  static let pageLimit: UInt32 = 200

  @Published private(set) var container: SidebarContainer?
  @Published private(set) var page: SidebarFilterPage?
  @Published private(set) var loadError: String?

  private var dependencies: Dependencies?

  func bind(_ dependencies: Dependencies) {
    self.dependencies = dependencies
  }

  func resetForVaultClose() {
    dependencies = nil
    container = nil
    page = nil
    loadError = nil
  }

  /// Wholesale replacement per container selection (VO stability, the
  /// FL4-2 discipline). nil clears to the empty state.
  func show(_ container: SidebarContainer?) {
    self.container = container
    guard let container, let dependencies else {
      page = nil
      loadError = nil
      return
    }
    do {
      page = try Self.load(
        container, dependencies: dependencies,
        paging: Paging(cursor: nil, limit: Self.pageLimit))
      loadError = nil
    } catch {
      page = nil
      loadError = error.localizedDescription
    }
  }

  func loadNextPage() {
    guard let container, let dependencies,
      let current = page, let cursor = current.nextCursor
    else { return }
    do {
      let next = try Self.load(
        container, dependencies: dependencies,
        paging: Paging(cursor: cursor, limit: Self.pageLimit))
      page = SidebarFilterPage(
        files: current.files + next.files,
        nextCursor: next.nextCursor,
        total: next.total,
        audioSummary: next.audioSummary)
    } catch {
      loadError = error.localizedDescription
    }
  }

  /// Structural mutations refresh the visible container (rename/trash
  /// must not leave stale rows), same funnel as the filter overlay.
  func refreshAfterStructuralMutation() {
    guard container != nil else { return }
    show(container)
  }

  private static func load(
    _ container: SidebarContainer, dependencies: Dependencies, paging: Paging
  ) throws -> SidebarFilterPage {
    switch container {
    case .folder(let path):
      return try dependencies.performQuery("", path, paging)
    case .tag(let full):
      return try dependencies.performQuery("#\(full)", nil, paging)
    case .untagged:
      return try dependencies.performUntagged(paging)
    }
  }
}

/// FL7-1 (#668): the internally gated dual-pane container — navigation
/// pane (Shortcuts, Recents, folders-only tree, Tags) over the list
/// pane, split by a persisted, AX-adjustable divider. Mounted ONLY
/// when the internal layout gate selects `.dualPane`; tree mode never
/// pays for any of this.
struct SidebarDualPaneView: View {
  @EnvironmentObject private var appState: AppState
  @ObservedObject var filterModel: SidebarFilterModel
  @ObservedObject var tree: FileTreeViewModel
  @ObservedObject var listModel: SidebarContainerListModel

  @State private var dividerFraction = SidebarDualPaneDivider.load(
    from: .standard)
  @FocusState private var navigationFocused: Bool
  @FocusState private var listFocused: Bool
  @State private var listSelection: String?

  private static let minimumPaneHeight: CGFloat = 120

  var body: some View {
    GeometryReader { geometry in
      let total = max(geometry.size.height, 1)
      let navHeight = min(
        max(total * dividerFraction, Self.minimumPaneHeight),
        total - Self.minimumPaneHeight)
      VStack(spacing: 0) {
        navigationPane
          .frame(height: navHeight)
        paneDivider(totalHeight: total)
        listPane
          .frame(maxHeight: .infinity)
      }
    }
    .onChange(of: appState.sidebarSelectedContainer) { _, container in
      listModel.show(container)
    }
    .onAppear {
      listModel.show(appState.sidebarSelectedContainer)
    }
  }

  // MARK: - Navigation pane

  private var navigationPane: some View {
    ScrollView {
      VStack(alignment: .leading, spacing: 0) {
        Text("Folders")
          .font(Tokens.Typography.sectionHeader)
          .foregroundStyle(Tokens.ColorRole.textSecondary)
          .padding(.horizontal, Tokens.Spacing.md)
          .padding(.vertical, Tokens.Spacing.xxs)
          .accessibilityAddTraits(.isHeader)
        SidebarSectionsView(
          activateShortcut: { appState.activateSidebarShortcut($0) },
          openRecent: { appState.openFile($0, target: .currentTab) })
        folderRows
        SidebarTagTreeView()
      }
    }
    .focused($navigationFocused)
    .accessibilityElement(children: .contain)
    .accessibilityLabel("Folders")
    .onChange(of: navigationFocused) { _, focused in
      if focused {
        appState.announceSidebarPaneTransition("Folders")
      }
    }
  }

  /// FL7-1 rule 2: the folders-only projection over the SAME tree view
  /// model — file rows suppressed by presentation, lazy fetch and
  /// expansion state untouched.
  private var folderRows: some View {
    let rows = Self.foldersOnlyRows(tree: tree)
    return ForEach(rows, id: \.node.nodeID) { row in
      folderRow(row)
    }
  }

  struct FolderRow: Equatable {
    let node: TreeNode
    let isExpanded: Bool
  }

  /// Pure folders-only flattening of the materialized tree: expanded
  /// directories recurse; files never appear.
  static func foldersOnlyRows(tree: FileTreeViewModel) -> [FolderRow] {
    var rows: [FolderRow] = []
    func walk(_ level: [TreeNode]) {
      for node in level where node.isDirectory {
        let expanded = tree.expanded.contains(node.nodeID)
        rows.append(FolderRow(node: node, isExpanded: expanded))
        if expanded, let children = tree.children[node.nodeID] {
          walk(children)
        }
      }
    }
    walk(tree.rootLevel)
    return rows
  }

  private func folderRow(_ row: FolderRow) -> some View {
    let container = SidebarContainer.folder(path: row.node.path)
    let isSelected = appState.sidebarSelectedContainer == container
    return SidebarFolderRowContent(
      node: row.node,
      isExpanded: row.isExpanded,
      isSelected: isSelected,
      selectionIsActive: navigationFocused,
      onChevronTap: {
        tree.toggle(row.node)
      })
      .contentShape(Rectangle())
      .onTapGesture {
        // Rule 3: a folder row is a CONTAINER — selecting it drives
        // the list pane; disclosure stays on the chevron.
        appState.sidebarSelectedContainer = container
      }
      .accessibilityElement(children: .ignore)
      .accessibilityLabel(row.node.name)
      .accessibilityValue(
        FileTreeSidebar.folderAccessibilityValue(
          for: row.node, expanded: row.isExpanded))
      .accessibilityHint("Shows this folder's files in the list pane.")
      .accessibilityAddTraits(isSelected ? [.isButton, .isSelected] : .isButton)
  }

  // MARK: - Divider

  private func paneDivider(totalHeight: CGFloat) -> some View {
    Rectangle()
      .fill(Tokens.ColorRole.separator)
      .frame(height: 1)
      .frame(maxWidth: .infinity)
      .padding(.vertical, Tokens.Spacing.xxs)
      .contentShape(Rectangle().inset(by: -Tokens.Spacing.xs))
      .gesture(
        DragGesture(minimumDistance: 1)
          .onChanged { value in
            let proposed =
              dividerFraction + value.translation.height / totalHeight
            dividerFraction = SidebarDualPaneDivider.clamp(proposed)
          }
          .onEnded { _ in
            SidebarDualPaneDivider.store(dividerFraction, in: .standard)
          })
      .accessibilityElement()
      .accessibilityLabel("Pane divider")
      .accessibilityValue(
        "\(Int((dividerFraction * 100).rounded())) percent folders")
      .accessibilityHint("Adjusts how much height the folders pane takes.")
      .accessibilityAdjustableAction { direction in
        let step = 0.05
        let next =
          direction == .increment
          ? dividerFraction + step : dividerFraction - step
        dividerFraction = SidebarDualPaneDivider.clamp(next)
        SidebarDualPaneDivider.store(dividerFraction, in: .standard)
      }
  }

  // MARK: - List pane

  private var listPane: some View {
    Group {
      if filterModel.isActive {
        // Rule 5: an active filter replaces the LIST pane contents
        // only; the navigation pane stays navigable.
        filterResults
      } else if let page = listModel.page, !page.files.isEmpty {
        containerRows(page)
      } else if let error = listModel.loadError {
        Text(error)
          .font(Tokens.Typography.caption)
          .foregroundStyle(Tokens.ColorRole.warningText)
          .frame(maxWidth: .infinity, maxHeight: .infinity)
      } else if listModel.container != nil {
        Text("No files here.")
          .font(Tokens.Typography.body)
          .foregroundStyle(Tokens.ColorRole.textSecondary)
          .frame(maxWidth: .infinity, maxHeight: .infinity)
      } else {
        Text("Select a folder or tag.")
          .font(Tokens.Typography.body)
          .foregroundStyle(Tokens.ColorRole.textSecondary)
          .frame(maxWidth: .infinity, maxHeight: .infinity)
      }
    }
    .accessibilityElement(children: .contain)
    .accessibilityLabel("Files")
    .onChange(of: listFocused) { _, focused in
      if focused {
        appState.announceSidebarPaneTransition("Files")
      }
    }
    .onChange(of: appState.lastMutationAnnouncement) { _, _ in
      listModel.refreshAfterStructuralMutation()
    }
  }

  private func containerRows(_ page: SidebarFilterPage) -> some View {
    List(selection: $listSelection) {
      ForEach(page.files, id: \.path) { summary in
        fileRow(summary)
      }
      if page.nextCursor != nil {
        Button {
          listModel.loadNextPage()
        } label: {
          Text("Load More Results")
            .font(Tokens.Typography.body)
            .foregroundStyle(Tokens.ColorRole.accentText)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .buttonStyle(.borderless)
        .selectionDisabled()
      }
    }
    .listStyle(.sidebar)
    .focused($listFocused)
    .onExitCommand {
      // Rule 4: ← / Esc in the list returns to the navigation pane.
      listFocused = false
      navigationFocused = true
    }
    .onMoveCommand { direction in
      if direction == .left {
        listFocused = false
        navigationFocused = true
      }
    }
    .onChange(of: listSelection) { _, selected in
      guard let selected else { return }
      appState.openFile(selected, target: .currentTab)
    }
  }

  private var filterResults: some View {
    Group {
      if let page = filterModel.results, !page.files.isEmpty {
        List(selection: $listSelection) {
          ForEach(page.files, id: \.path) { summary in
            fileRow(summary)
          }
          if page.nextCursor != nil {
            Button {
              filterModel.loadNextPage()
            } label: {
              Text("Load More Results")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.accentText)
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .buttonStyle(.borderless)
            .selectionDisabled()
          }
        }
        .listStyle(.sidebar)
        .focused($listFocused)
        .onExitCommand {
          listFocused = false
          navigationFocused = true
        }
        .onChange(of: listSelection) { _, selected in
          guard let selected else { return }
          appState.openFile(selected, target: .currentTab)
        }
      } else {
        Text("No results.")
          .font(Tokens.Typography.body)
          .foregroundStyle(Tokens.ColorRole.textSecondary)
          .frame(maxWidth: .infinity, maxHeight: .infinity)
      }
    }
  }

  private func fileRow(_ summary: FileSummary) -> some View {
    let parent = (summary.path as NSString).deletingLastPathComponent
    return SidebarFileRow(
      model: SidebarRowModel(
        summary: summary,
        preferences: appState.sidebarPreferences.rowSnapshot,
        isPinned: false,
        pathSubtitle: parent.isEmpty ? "Vault root" : parent,
        now: Date()),
      depth: 0,
      isSelected: listSelection == summary.path,
      selectionIsActive: listFocused)
      .tag(summary.path)
      .accessibilityHint("Opens the file.")
  }
}
