// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// Searchable folder picker for moving a file or folder (U2-5, #463).
///
/// The keyboard-first, drag-free move path the DoD requires: a filter-as-you-
/// type list of every folder in the vault (plus a "Vault root" target and a
/// "New Folder…" row at the top), arrow navigation, Return commits. Modeled on
/// `CommandPaletteView`'s interaction (local keyDown monitor for ↑/↓ + Esc, a
/// focused search field, `ScrollViewReader` to keep the selection visible).
///
/// Invalid destinations are filtered OUT before the user can pick them: the
/// node's current parent (a no-op move) and — for a folder — its own subtree
/// (which the backend rejects as `InvalidArgument`). So every offered row is a
/// legal move.
struct MoveToFolderSheet: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    private enum Scope {
        case single(AppState.PendingMove)
        case batch(AppState.BatchMove)
    }

    /// Explicit action origin. A captured batch remains a batch even if core's
    /// later projection leaves one top-level item.
    private let scope: Scope
    /// SwiftUI may recreate this value while one sheet remains presented. The
    /// store survives those recreations and keys its one selection scan to the
    /// operation UUID, keeping that work out of both init and `offered`.
    @StateObject private var destinationLegalityStore = DestinationLegalityStore()
    private let operationID: UUID
    private let capturedItems: [AppState.TreeSelection]
    /// The noun phrase the title/hint speak — a single node's name, or "N items".
    private let displayName: String

    /// Single-node move (U2-5) — the sheet drives one `moveEntry`.
    init(move: AppState.PendingMove) {
        let items = [
            AppState.TreeSelection(path: move.path, isDirectory: move.isDirectory)
        ]
        self.scope = .single(move)
        self.operationID = move.id
        self.capturedItems = items
        self.displayName = move.name
    }

    /// Batch move (#852) — the sheet drives `batchMove` over the whole
    /// selection, offering only destinations legal for EVERY item.
    init(batch: AppState.BatchMove) {
        self.scope = .batch(batch)
        self.operationID = batch.id
        self.capturedItems = batch.items
        self.displayName = batch.displayName
    }

    private var isBatch: Bool {
        if case .batch = scope { return true }
        return false
    }

    @State private var query: String = ""
    /// All folder paths in the vault (loaded on appear). "" is not in here — the
    /// "Vault root" row carries it.
    @State private var allFolders: [String] = []
    @State private var isLoading = true
    /// The currently-highlighted destination row.
    @State private var selection: Destination?
    @State private var keyDownMonitor: Any?
    @FocusState private var searchFocused: Bool

    /// One offered destination: a real folder, or the vault root, or the
    /// "create a new folder here" affordance.
    enum Destination: Hashable {
        /// Move into the vault root (path "").
        case root
        /// Move into an existing folder at this vault-relative path.
        case folder(String)
        /// Create a new folder (in the vault root) and move into it.
        case newFolder

        var rowID: String {
            switch self {
            case .root: return "\u{0000}root"
            case .folder(let p): return "folder:\(p)"
            case .newFolder: return "\u{0000}new"
            }
        }
    }

    /// Lifetime-managed, operation-keyed cache. A parent `AppState` update may
    /// recreate `MoveToFolderSheet`, but SwiftUI retains this object for the
    /// presented sheet identity. If the pending operation itself changes, its
    /// UUID admits exactly one new index rather than reusing stale membership.
    final class DestinationLegalityStore: ObservableObject {
        private var cachedOperationID: UUID?
        private var cachedIndex: DestinationLegalityIndex?

        func index(
            for operationID: UUID,
            items: [AppState.TreeSelection]
        ) -> DestinationLegalityIndex {
            var ignored = 0
            return index(
                for: operationID,
                items: items,
                selectedItemVisits: &ignored)
        }

        func index(
            for operationID: UUID,
            items: [AppState.TreeSelection],
            selectedItemVisits: inout Int
        ) -> DestinationLegalityIndex {
            if cachedOperationID == operationID, let cachedIndex {
                return cachedIndex
            }
            let built = DestinationLegalityIndex(
                items, selectedItemVisits: &selectedItemVisits)
            cachedOperationID = operationID
            cachedIndex = built
            return built
        }
    }

    /// Pure selection-derived legality seam. It scans the captured selection
    /// once, indexing selected directories for component-safe ancestor lookup
    /// and reducing the all-items no-op rule to one common-parent value. Every
    /// later candidate is therefore bounded by that candidate's path depth.
    struct DestinationLegalityIndex {
        private let selectedDirectoryPaths: Set<String>
        private let commonParent: String?
        private let selectionIsEmpty: Bool

        init(_ items: [AppState.TreeSelection]) {
            var ignored = 0
            self.init(items, selectedItemVisits: &ignored)
        }

        init(
            _ items: [AppState.TreeSelection],
            selectedItemVisits: inout Int
        ) {
            let firstParent = items.first.map {
                AppState.TreeMutation.parentPath(of: $0.path) ?? ""
            }
            var everyItemSharesFirstParent = !items.isEmpty
            var selectedDirectoryPaths = Set<String>()
            selectedDirectoryPaths.reserveCapacity(items.count)

            for item in items {
                selectedItemVisits += 1
                let parent = AppState.TreeMutation.parentPath(of: item.path) ?? ""
                if parent != firstParent {
                    everyItemSharesFirstParent = false
                }
                if item.isDirectory { selectedDirectoryPaths.insert(item.path) }
            }

            self.selectedDirectoryPaths = selectedDirectoryPaths
            self.commonParent = everyItemSharesFirstParent ? firstParent : nil
            self.selectionIsEmpty = items.isEmpty
        }

        func isLegalTarget(_ folder: String) -> Bool {
            var ignoredSelectionComparisons = 0
            var ignoredComponentVisits = 0
            return isLegalTarget(
                folder,
                selectedItemComparisons: &ignoredSelectionComparisons,
                componentVisits: &ignoredComponentVisits)
        }

        func isLegalTarget(
            _ folder: String,
            selectedItemComparisons: inout Int,
            componentVisits: inout Int
        ) -> Bool {
            _ = selectedItemComparisons
            guard !selectionIsEmpty else { return false }
            if selectedDirectoryCovers(folder, componentVisits: &componentVisits) { return false }
            if let commonParent, commonParent == folder { return false }
            return true
        }

        /// Probe the candidate itself, then each slash-delimited ancestor. A
        /// `Set<String>` retains Swift's canonical-equivalence semantics from
        /// the predicate this replaces while remaining case-sensitive. It also
        /// preserves malformed empty/trailing-slash behavior exactly; real tree
        /// paths are validated, but the pure seam stays total for tests.
        private func selectedDirectoryCovers(
            _ folder: String,
            componentVisits: inout Int
        ) -> Bool {
            var ancestor = folder
            while true {
                componentVisits += 1
                if selectedDirectoryPaths.contains(ancestor) { return true }
                guard let slash = ancestor.lastIndex(of: "/") else { return false }
                ancestor = String(ancestor[..<slash])
            }
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()
            searchField
            if let reason = appState.structuralMutationDisabledReason {
                Divider()
                busyNotice(reason)
            }
            Divider()
            if isLoading {
                loadingState
            } else {
                results
            }
        }
        .frame(minWidth: 480, idealWidth: 520, minHeight: 380, idealHeight: 420)
        .background(Color(nsColor: .controlBackgroundColor))
        .onExitCommand { dismissSheet() }
        .task {
            allFolders = await appState.loadAllFolders()
            isLoading = false
            // Default the highlight to the first offered row so Return works
            // immediately.
            selection = offered.first
        }
        .onAppear {
            searchFocused = true
            installKeyDownMonitor()
        }
        .onDisappear { removeKeyDownMonitor() }
        .onChange(of: query) { _, _ in
            // Keep a valid selection as the filter narrows.
            if let sel = selection, offered.contains(sel) { return }
            selection = offered.first
        }
    }

    // MARK: - Header

    private var header: some View {
        HStack {
            Text("Move \(displayName)")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Spacer()
            Button("Cancel") { dismissSheet() }
                .keyboardShortcut(.cancelAction)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    private var searchField: some View {
        HStack(spacing: 8) {
            SlateSymbol.search.decorative
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
            TextField("Search folders", text: $query)
                .textFieldStyle(.plain)
                .font(.title3)
                .focused($searchFocused)
                .accessibilityLabel("Search folders")
                .accessibilityHint(
                    "Arrow up and down to move selection. Return moves \(displayName) to the selected folder."
                )
                .onSubmit(commitSelected)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    /// The exact shared structural-operation reason, kept visible while the
    /// destination actions are disabled. Cancel remains available.
    private func busyNotice(_ reason: String) -> some View {
        Text(reason)
            .font(.callout)
            .foregroundStyle(Color(nsColor: .secondaryLabelColor))
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.horizontal, 16)
            .padding(.vertical, 8)
            .accessibilityLabel(reason)
    }

    // MARK: - Results

    @ViewBuilder
    private var results: some View {
        let rows = offered
        if rows.isEmpty {
            noMatchesState
        } else {
            ScrollViewReader { proxy in
                List {
                    ForEach(rows, id: \.rowID) { destination in
                        row(destination)
                            .id(destination.rowID)
                    }
                }
                .listStyle(.inset)
                .onChange(of: selection) { _, sel in
                    guard let sel else { return }
                    proxy.scrollTo(sel.rowID, anchor: .center)
                }
            }
        }
    }

    private func row(_ destination: Destination) -> some View {
        let isSelected = destination == selection
        return Button {
            searchFocused = true
            commit(destination)
        } label: {
            HStack(spacing: 10) {
                icon(for: destination)
                Text(label(for: destination))
                    .foregroundStyle(
                        isSelected
                            ? Color(nsColor: .selectedMenuItemTextColor)
                            : Color(nsColor: .labelColor))
                Spacer()
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 10)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(
                isSelected
                    ? Color(nsColor: .selectedContentBackgroundColor)
                    : Color.clear)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .onHover { hovering in
            if hovering { selection = destination }
        }
        .accessibilityLabel(accessibilityLabel(for: destination))
        .accessibilityHint(appState.structuralMutationDisabledReason ?? "")
        .accessibilityIsSelected(isSelected)
        .disabled(appState.structuralMutationDisabledReason != nil)
    }

    @ViewBuilder
    private func icon(for destination: Destination) -> some View {
        switch destination {
        case .newFolder:
            SlateSymbol.newFolder.decorative.foregroundStyle(.secondary)
        case .root, .folder:
            SlateSymbol.folder.decorative.foregroundStyle(.secondary)
        }
    }

    private func label(for destination: Destination) -> String {
        switch destination {
        case .newFolder: return "New Folder…"
        case .root: return "Vault root"
        case .folder(let p): return p
        }
    }

    private func accessibilityLabel(for destination: Destination) -> String {
        switch destination {
        case .newFolder: return "New Folder. Create a folder in the vault root and move here."
        case .root: return "Vault root"
        case .folder(let p): return "Folder \(p)"
        }
    }

    // MARK: - States

    private var loadingState: some View {
        VStack(spacing: 8) {
            Spacer()
            ProgressView()
            Text("Loading folders…")
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
            Spacer()
        }
        .frame(maxWidth: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Loading folders.")
    }

    private var noMatchesState: some View {
        VStack(spacing: 8) {
            Spacer()
            Text("No matching folders")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text("No folder matches \"\(query)\". Choose New Folder… or clear the search.")
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .multilineTextAlignment(.center)
            Spacer()
        }
        .frame(maxWidth: .infinity)
        .padding(.horizontal, 24)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("No folder matches \(query). Choose New Folder, or clear the search.")
    }

    // MARK: - Offered destinations

    /// The filtered, legal destination rows in display order: "New Folder…"
    /// first, then "Vault root" (unless the node is already at root), then every
    /// folder that's a legal target, matching the query.
    private var offered: [Destination] {
        let q = query.trimmingCharacters(in: .whitespaces).lowercased()
        var rows: [Destination] = []
        // "New Folder…" always offered (subject to matching the query on the
        // word "new"/"folder" so it doesn't clutter a specific search).
        if q.isEmpty || "new folder".contains(q) {
            rows.append(.newFolder)
        }
        // Vault root — unless it's a no-op / illegal for the selection (#852:
        // "" is offered only when at least one item can legally move there).
        if isLegalTarget(""), q.isEmpty || "vault root".contains(q) {
            rows.append(.root)
        }
        for folder in allFolders where isLegalTarget(folder) {
            if q.isEmpty || folder.lowercased().contains(q) {
                rows.append(.folder(folder))
            }
        }
        return rows
    }

    /// Whether `folder` is a legal move destination for the selection: not the
    /// self/subtree of ANY selected folder (the backend rejects those), and not
    /// a pure no-op — i.e. NOT the case where every selected item already lives
    /// directly in `folder` (#852). For a single node this reduces exactly to
    /// the U2-5 rule (not its current parent, not its own subtree).
    private func isLegalTarget(_ folder: String) -> Bool {
        destinationLegalityStore.index(
            for: operationID,
            items: capturedItems
        ).isLegalTarget(folder)
    }

    // MARK: - Commit

    private func commitSelected() {
        guard let selection else { return }
        commit(selection)
    }

    private func commit(_ destination: Destination) {
        let admitted: Bool
        switch destination {
        case .root:
            admitted = performMove(to: "")
        case .folder(let target):
            admitted = performMove(to: target)
        case .newFolder:
            // Create a folder at the vault root, then move into it. The create
            // enters inline rename in the tree, but the move should go into the
            // freshly-created folder — so we create with a concrete name here
            // and move into it directly (no rename detour), keeping the picker
            // flow atomic from the user's view.
            let name = "New Folder"
            switch scope {
            case .batch(let batch):
                admitted = appState.commitPendingBatchMoveToNewFolder(
                    id: batch.id, newFolderName: name, in: "")
            case .single(let move):
                admitted = appState.commitPendingMoveToNewFolder(
                    id: move.id, newFolderName: name, in: "")
            }
        }
        if admitted { dismiss() }
    }

    /// Route the move: a batch through `batchMove` (one announcement), a single
    /// node through `moveEntry` — the #852 vs U2-5 fork.
    private func performMove(to target: String) -> Bool {
        switch scope {
        case .batch(let batch):
            return appState.commitPendingBatchMove(id: batch.id, to: target)
        case .single(let move):
            return appState.commitPendingMove(id: move.id, to: target)
        }
    }

    private func dismissSheet() {
        // Clear whichever pending field drove this presentation (#852: the
        // batch sheet reads `pendingBatchMove`, the single reads `pendingMove`).
        // The presentation item is frozen in MainSplitView. If AppState already
        // holds a replacement request, identity-safe cancellation intentionally
        // returns false; this old sheet must still dismiss so onDismiss can
        // promote that preserved replacement instead of becoming an inert modal.
        switch scope {
        case .single(let move):
            appState.cancelPendingMove(id: move.id)
        case .batch(let batch):
            appState.cancelPendingBatchMove(id: batch.id)
        }
        dismiss()
    }

    // MARK: - Keyboard monitor (↑/↓ selection, mirrors CommandPaletteView)

    private func installKeyDownMonitor() {
        guard keyDownMonitor == nil else { return }
        keyDownMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in
            // Don't steal keys from an IME mid-composition.
            if let editor = event.window?.firstResponder as? NSTextView,
                editor.hasMarkedText() {
                return event
            }
            switch event.keyCode {
            case 125:  // down arrow
                guard event.modifierFlags.intersection([.shift, .control, .option, .command]).isEmpty
                else { return event }
                moveSelection(by: 1)
                return nil
            case 126:  // up arrow
                guard event.modifierFlags.intersection([.shift, .control, .option, .command]).isEmpty
                else { return event }
                moveSelection(by: -1)
                return nil
            default:
                return event
            }
        }
    }

    private func removeKeyDownMonitor() {
        if let m = keyDownMonitor {
            NSEvent.removeMonitor(m)
            keyDownMonitor = nil
        }
    }

    private func moveSelection(by delta: Int) {
        let rows = offered
        guard !rows.isEmpty else { return }
        guard let current = selection, let idx = rows.firstIndex(of: current) else {
            selection = rows.first
            return
        }
        let next = (idx + delta + rows.count) % rows.count
        selection = rows[next]
    }
}
