// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// FL7-1 (#668): the internally gated dual-pane container — navigation
/// pane (Shortcuts, Recents, folders-only tree, Tags) over the list
/// pane, split by a persisted, AX-adjustable divider. Mounted ONLY
/// when the internal layout gate selects `.dualPane`; tree mode never
/// pays for any of this.
struct SidebarDualPaneView: View {
  @EnvironmentObject private var appState: AppState
  @ObservedObject var filterModel: SidebarFilterModel
  @ObservedObject var tree: FileTreeViewModel
  @ObservedObject var listModel: SidebarListPaneModel
  /// FL7-2 rule 4 action parity: the tree's OWNER builds the row
  /// context menu (the one shared single-file builder + flat multi
  /// fallback) and drag payloads; closures keep those file-private
  /// helpers — and their source-contract anchors — where they live.
  var rowContextMenu: ((FileSummary) -> AnyView)?
  var rowDragProvider: ((FileSummary) -> NSItemProvider?)?
  /// Drag-to-navigation (rule 4): drops on nav folder rows route into
  /// the tree owner's one admission funnel.
  var navDrop: ((_ folderPath: String, _ providers: [NSItemProvider]) -> Bool)?

  @State private var dividerFraction = SidebarDualPaneDivider.load(
    from: .standard)
  /// The gesture's starting fraction — drag math is anchor-based
  /// (review round: translation is cumulative from gesture start).
  @State private var dividerDragAnchor: Double?
  @FocusState private var navigationFocused: Bool
  @FocusState private var listFocused: Bool

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
      appState.publishDualPaneSelectionSnapshot()
      // Review round: a committed filter is container-scoped — a
      // container change re-runs it so results and pagination can
      // never mix scopes or go stale.
      if filterModel.isActive {
        filterModel.refreshAfterStructuralMutation()
      }
    }
    .onChange(of: filterModel.committedQuery) { _, committed in
      // Review round: Untagged has no textual composition — a typed
      // query under it would silently search the whole vault while
      // the nav still showed "Untagged" selected. Clearing the
      // container keeps the UI truthful (visibly unscoped).
      if !committed.isEmpty,
        appState.sidebarSelectedContainer == .untagged
      {
        appState.sidebarSelectedContainer = nil
      }
    }
    .onChange(of: appState.selectedFilePath) { _, path in
      // Rule 3: the editor→sidebar mirror (Recents, file shortcuts,
      // search, quick open — any open).
      if let path {
        appState.mirrorOpenedFileIntoDualPane(path)
      }
    }
    .onChange(of: appState.sidebarDualPaneListFocusRequest) { _, _ in
      // ↓ from the filter field: enter the list at row 1 (rule 4).
      let first =
        filterModel.isActive
        ? filterModel.results?.files.first?.path
        : listModel.fileSummaries.first?.path
      if let first {
        appState.sidebarDualPaneListSelection = first
        appState.sidebarDualPaneMultiSelection = [first]
      }
      navigationFocused = false
      listFocused = true
    }
    .onAppear {
      listModel.show(appState.sidebarSelectedContainer)
      appState.publishDualPaneSelectionSnapshot()
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
    .focusable()
    .focused($navigationFocused)
    .accessibilityElement(children: .contain)
    .accessibilityLabel("Folders")
    .onChange(of: navigationFocused) { _, focused in
      if focused {
        appState.announceSidebarPaneTransition("Folders")
      }
    }
    // Rule 4 (review round: the matrix must RUN, not just be tested):
    // → on the selected container consults the disclosure-priority
    // matrix; leaves and no-selection stay put.
    .onMoveCommand { direction in
      guard direction == .right else { return }
      switch rightArrowOutcomeForSelection() {
      case .disclose:
        if case .folder(let path) = appState.sidebarSelectedContainer,
          let node = folderNode(at: path)
        {
          tree.toggle(node)
        }
      case .moveToList:
        navigationFocused = false
        listFocused = true
      case .stay:
        break
      }
    }
  }

  /// The production consultation of `SidebarDualPaneFocus.rightArrow`
  /// for the current navigation selection.
  func rightArrowOutcomeForSelection() -> SidebarDualPaneFocus.RightArrowOutcome
  {
    switch appState.sidebarSelectedContainer {
    case .folder(let path):
      guard let node = folderNode(at: path) else {
        return SidebarDualPaneFocus.rightArrow(
          isContainer: true, hasDisclosure: false, isExpanded: false)
      }
      let hasChildDirs: Bool
      if case let .directory(dirs, _, _) = node.kind {
        hasChildDirs = dirs > 0
      } else {
        hasChildDirs = false
      }
      return SidebarDualPaneFocus.rightArrow(
        isContainer: true,
        hasDisclosure: hasChildDirs,
        isExpanded: tree.expanded.contains(node.nodeID))
    case .tag, .untagged:
      return SidebarDualPaneFocus.rightArrow(
        isContainer: true, hasDisclosure: false, isExpanded: false)
    case nil:
      return .stay
    }
  }

  private func folderNode(at path: String) -> TreeNode? {
    if let node = tree.rootLevel.first(where: {
      $0.isDirectory && $0.path == path
    }) {
      return node
    }
    for level in tree.children.values {
      if let node = level.first(where: { $0.isDirectory && $0.path == path }) {
        return node
      }
    }
    return nil
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
      .onDrop(
        of: [FileTreeSidebar.nodeUTType, FileTreeSidebar.fileURLUTType],
        isTargeted: nil
      ) { providers in
        navDrop?(row.node.path, providers) ?? false
      }
      .contextMenu {
        // FL7-2 rule 3: the same shared override items as the list
        // header — one component, two surfaces.
        SidebarFolderDisplayOverrideItems(
          folder: row.node.path, showsDescendants: true)
      }
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
            let anchor = dividerDragAnchor ?? dividerFraction
            dividerDragAnchor = anchor
            dividerFraction = SidebarDualPaneDivider.dragged(
              fromAnchor: anchor,
              translation: value.translation.height,
              totalHeight: totalHeight)
          }
          .onEnded { _ in
            dividerDragAnchor = nil
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

  // MARK: - List pane (FL7-2 complete)

  private var listPane: some View {
    VStack(alignment: .leading, spacing: 0) {
      listPaneHeader
      listPaneBody
    }
    .accessibilityElement(children: .contain)
    .accessibilityLabel("Files")
    .onChange(of: listFocused) { _, focused in
      if focused {
        appState.announceSidebarPaneTransition("Files")
      }
    }
    // Review round (high): the selection handler lives on the SHARED
    // pane — both the container list and the filter-results list bind
    // the same selection, so filtered rows acquire snapshot ownership
    // and open exactly like container rows.
    .onChange(of: appState.sidebarDualPaneMultiSelection) { _, selection in
      // Rule 5: single selection keeps the open-on-select behavior;
      // a batch is a TARGET, not an open storm.
      appState.sidebarDualPaneListSelection =
        selection.count == 1 ? selection.first : nil
      appState.publishDualPaneSelectionSnapshot()
      // The already-open guard breaks the mirror echo: the editor→pane
      // mirror selects the opened file's row, which must not dispatch
      // a second open of the same note.
      if selection.count == 1, let path = selection.first,
        appState.selectedFilePath != path
      {
        appState.openFile(path, target: .currentTab)
      }
    }
    // Review round (medium): value-typed event sources, not
    // announcement copy — two mutations announcing identical text
    // ("Pinned.") must both refresh. treeMutation tokens every
    // structural transform; sidebarOrganization changes on every
    // pin/sort/group/override edit.
    .onChange(of: appState.treeMutation) { _, _ in
      listModel.refreshAfterStructuralMutation()
    }
    .onChange(of: appState.sidebarOrganization) { _, _ in
      listModel.refreshAfterStructuralMutation()
    }
    // Review round (high): once a drain settles, selections must be a
    // subset of the visible rows — republished if pruned.
    .onChange(of: listModel.rows) { _, _ in
      appState.reconcileDualPaneSelectionWithVisibleRows()
    }
  }

  /// The pane header: container title + count/truncation + the FL7-2
  /// display-override menu (folder containers get the full override
  /// set; every container gets sort/group through the SAME catalog
  /// evaluations the tree uses — no second command dialect).
  private var listPaneHeader: some View {
    HStack(spacing: Tokens.Spacing.xs) {
      Text("Files")
        .font(Tokens.Typography.sectionHeader)
        .foregroundStyle(Tokens.ColorRole.textSecondary)
        .accessibilityAddTraits(.isHeader)
      if listModel.container != nil {
        Text(headerCountText)
          .font(Tokens.Typography.caption)
          .foregroundStyle(Tokens.ColorRole.textSecondary)
      }
      Spacer(minLength: 0)
      if listModel.container != nil {
        SidebarListPaneDisplayMenu(container: listModel.container)
      }
    }
    .padding(.horizontal, Tokens.Spacing.md)
    .padding(.vertical, Tokens.Spacing.xxs)
  }

  private var headerCountText: String {
    let count = listModel.fileCount
    let base = count == 1 ? "1 file" : "\(count) files"
    return listModel.truncated ? "first \(base)" : base
  }

  @ViewBuilder
  private var listPaneBody: some View {
    if filterModel.isActive {
      // Rule 5: an active filter replaces the LIST pane contents only.
      filterResults
    } else if listModel.isLoading && listModel.rows.isEmpty {
      Text("Loading…")
        .font(Tokens.Typography.caption)
        .foregroundStyle(Tokens.ColorRole.textSecondary)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    } else if !listModel.rows.isEmpty {
      containerRows
    } else if let error = listModel.loadError {
      Text(error)
        .font(Tokens.Typography.caption)
        .foregroundStyle(Tokens.ColorRole.warningText)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    } else if listModel.container != nil {
      emptyContainerState
    } else {
      Text("Select a folder or tag.")
        .font(Tokens.Typography.body)
        .foregroundStyle(Tokens.ColorRole.textSecondary)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
  }

  /// Rule 1: empty container ⇒ quiet empty state + New Note into the
  /// folder container (tags/Untagged have no creation location).
  private var emptyContainerState: some View {
    VStack(spacing: Tokens.Spacing.sm) {
      Text("No files here.")
        .font(Tokens.Typography.body)
        .foregroundStyle(Tokens.ColorRole.textSecondary)
      if case .folder(let path) = listModel.container {
        Button {
          _ = appState.createNote(in: path)
        } label: {
          SlateSymbol.newNote.label("New Note")
        }
        .buttonStyle(.borderless)
        .accessibilityHint("Creates a note in this folder.")
      }
    }
    .frame(maxWidth: .infinity, maxHeight: .infinity)
  }

  private var multiSelectionBinding: Binding<Set<String>> {
    Binding(
      get: { appState.sidebarDualPaneMultiSelection },
      set: { appState.sidebarDualPaneMultiSelection = $0 })
  }

  private var containerRows: some View {
    List(selection: multiSelectionBinding) {
      ForEach(listModel.rows) { row in
        switch row {
        case .header(_, let label, let count):
          Text("\(label) — \(count == 1 ? "1 file" : "\(count) files")")
            .font(Tokens.Typography.caption)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .accessibilityAddTraits(.isHeader)
            .selectionDisabled()
        case .file(let summary):
          fileRow(summary)
        }
      }
    }
    .listStyle(.sidebar)
    .focused($listFocused)
    .onExitCommand {
      listFocused = false
      navigationFocused = true
    }
    .onMoveCommand { direction in
      if direction == .left {
        listFocused = false
        navigationFocused = true
      }
    }
  }

  private var filterResults: some View {
    Group {
      if let page = filterModel.results, !page.files.isEmpty {
        List(selection: multiSelectionBinding) {
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
      } else {
        Text("No results.")
          .font(Tokens.Typography.body)
          .foregroundStyle(Tokens.ColorRole.textSecondary)
          .frame(maxWidth: .infinity, maxHeight: .infinity)
      }
    }
  }

  @ViewBuilder
  private func fileRow(_ summary: FileSummary) -> some View {
    // Review round (medium): the shared menu exposes Rename, so this
    // row must render the SAME visible editor the tree row swaps to —
    // otherwise the rename owner strands invisibly (the FL-09 overlay
    // lesson, dual-pane edition). New Note's inline-rename handoff
    // rides the same mount.
    if let rename = appState.renamingNode, rename.path == summary.path,
      !rename.isDirectory
    {
      RenameField(
        initialName: rename.name,
        isDirectory: false,
        error: appState.structuralRenameError,
        onCommit: { newName in
          appState.commitPendingRename(id: rename.id, to: newName)
        },
        onCancel: {
          appState.cancelPendingRename(id: rename.id)
        })
    } else {
      plainFileRow(summary)
    }
  }

  @ViewBuilder
  private func plainFileRow(_ summary: FileSummary) -> some View {
    let parent = (summary.path as NSString).deletingLastPathComponent
    let base = appState.sidebarPreferences.rowSnapshot
    let row = SidebarFileRow(
      model: SidebarRowModel(
        summary: summary,
        preferences: listModel.rowPreferences(base: base),
        isPinned: false,
        pathSubtitle: listModel.includesDescendants(
          for: listModel.container ?? .untagged)
          ? (parent.isEmpty ? "Vault root" : parent) : nil,
        now: Date()),
      depth: 0,
      isSelected: appState.sidebarDualPaneMultiSelection.contains(
        summary.path),
      selectionIsActive: listFocused)
      .tag(summary.path)
    // #951's checker lesson, dual-pane edition: the complex-gesture
    // hint must ride the OUTERMOST chain holding .onDrag/.contextMenu,
    // not an inner builder expression.
    let hint =
      "Opens the file. Other available actions are in the context menu."
    if let rowDragProvider,
      let provider = rowDragProvider(summary)
    {
      row
        .onDrag { provider }
        .contextMenu { rowContextMenu?(summary) }
        .accessibilityHint(hint)
    } else {
      row
        .contextMenu { rowContextMenu?(summary) }
        .accessibilityHint(hint)
    }
  }
}
