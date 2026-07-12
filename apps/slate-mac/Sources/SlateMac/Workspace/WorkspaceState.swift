// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// How a tab presents its note (U3-2, #466): the NSTextView editor or the
/// rendered read-only ReadingView. Raw values are the `workspace.json`
/// per-tab `"mode"` strings; `.editing` is the absent-key default.
enum NoteViewMode: String, Codable {
    case editing
    case reading
}

/// The observable owner of the `WorkspaceModel` (Milestone U1).
///
/// Views never mutate the model directly — every mutation funnels through
/// methods here so invariants and per-tab document lifecycle have a single
/// choke point. `AppState` orchestrates (it owns both this and the active-
/// note fields); this type never reaches into `AppState`.
///
/// Per the U1-2 architecture amendment (u1_spec.md): AppState's single-note
/// fields are the ACTIVE tab's document; `documents` holds the parked state
/// of inactive tabs, keyed by tab. Snapshot/restore moves state between the
/// two on tab switch.
@MainActor
final class WorkspaceState: ObservableObject {

    @Published private(set) var model = WorkspaceModel()

    /// The right-pane leaf currently shown by `RightPaneView` (U4-1, #470).
    /// Window-level view state, not part of the tab model, so it lives here
    /// beside `model` rather than inside it. Persisted through
    /// `workspace.json` (`WorkspaceStore.Snapshot.activeLeaf`); an absent or
    /// unknown stored value restores to `.outline` (see AppState restore).
    @Published var activeLeaf: Leaf = .outline

    /// Parked state for tabs that are not the active tab of the active
    /// group. The ACTIVE tab's state lives in AppState's fields; its entry
    /// here is stale while active and overwritten on park.
    private(set) var documents: [TabID: NoteDocument] = [:]

    /// Per-tab view mode (U3-2, #466). Sparse: only tabs in reading mode
    /// have an entry — absent means `.editing` (today's behavior and the
    /// workspace.json backward-compat rule). Cleared on tab close; persisted
    /// per tab as `"mode"` in the snapshot schema.
    @Published private(set) var viewModes: [TabID: NoteViewMode] = [:]

    func viewMode(for tabID: TabID) -> NoteViewMode {
        viewModes[tabID] ?? .editing
    }

    /// The ACTIVE tab's mode — what `NoteContentView` renders. No active
    /// tab (empty workspace) reads as `.editing` so the editor empty state
    /// keeps its shape.
    var activeViewMode: NoteViewMode {
        guard let id = model.activeGroup.activeTabID else { return .editing }
        return viewMode(for: id)
    }

    func setViewMode(_ mode: NoteViewMode, for tabID: TabID) {
        if mode == .editing {
            viewModes[tabID] = nil  // sparse: editing is the absent default
        } else {
            viewModes[tabID] = mode
        }
    }

    // MARK: Reading scroll park (#856)

    /// One parked reading-scroll position: the topmost visible block
    /// index of a tab's `ReadingView`, plus the note path it belongs
    /// to — restore validates the path so a note switch inside the
    /// same tab never restores another note's offset.
    struct ReadingScrollPark: Equatable {
        let path: String
        let blockIndex: Int
    }

    /// Per-tab reading-scroll park (#856) — the reading-mode sibling
    /// of the U3-2 caret park: editing→reading→editing preserves the
    /// caret; this preserves the reading offset across the reverse
    /// round trip. TRANSIENT by design: never persisted to
    /// workspace.json (a stale block index against a since-edited
    /// file would restore garbage), NOT `@Published` (continuous
    /// scroll reporting must not invalidate views — the caret-park
    /// rule), and cleared exactly where `viewModes` entries die (tab
    /// close, vault reset, session adopt) plus on note switch via the
    /// path check + the replace purge below.
    private(set) var readingScrollParks: [TabID: ReadingScrollPark] = [:]

    /// Park the topmost visible reading block for `tabID`. Continuous
    /// caller (every scroll-position change while reading); cheap
    /// dictionary write, no publish.
    func parkReadingScroll(blockIndex: Int, path: String, for tabID: TabID) {
        readingScrollParks[tabID] = ReadingScrollPark(path: path, blockIndex: blockIndex)
    }

    /// The parked block index for `tabID`, or nil when nothing is
    /// parked or the park belongs to a different note (path changed
    /// under the same tab — the #856 "clear on note switch" rule).
    func parkedReadingScroll(for tabID: TabID, path: String?) -> Int? {
        guard let park = readingScrollParks[tabID], park.path == path else { return nil }
        return park.blockIndex
    }

    /// Per-tab properties-widget expansion (U3-3, #467). Sparse like
    /// `viewModes`: only COLLAPSED tabs have an entry — expanded is the
    /// default and the absent-key state in workspace.json.
    @Published private(set) var propertiesCollapsed: Set<TabID> = []

    func isPropertiesExpanded(for tabID: TabID) -> Bool {
        !propertiesCollapsed.contains(tabID)
    }

    func setPropertiesExpanded(_ expanded: Bool, for tabID: TabID) {
        if expanded {
            propertiesCollapsed.remove(tabID)
        } else {
            propertiesCollapsed.insert(tabID)
        }
    }

    /// Per-tab canvas sub-surface (Milestone T, #369). Sparse like
    /// `viewModes`: only tabs away from the outline (the structured-first
    /// default landing) have an entry; persisted per tab as
    /// `"activeCanvasSurface"` in the snapshot schema.
    @Published private(set) var canvasSurfaces: [TabID: CanvasSurface] = [:]

    // MARK: Closed-tab stack (#863 Reopen Closed Tab)

    /// A closed tab's reopenable snapshot: the item it showed plus the
    /// group it lived in, so ⇧⌘T can land the reopen back in the pane
    /// the tab was closed from (when that pane still exists).
    struct ClosedTabRecord: Equatable {
        let item: EditorItem
        let groupID: GroupID
    }

    /// Bound on `closedTabs` — the reopen stack never grows past this;
    /// the oldest record is evicted first (a 21st close forgets the
    /// 1st, exactly the browser-history convention).
    static let closedTabCapacity = 20

    /// Most-recent-last stack of user-closed tabs (⇧⌘T pops the end).
    /// Per-vault-session by construction: every user close funnels
    /// through `close(_:)` below (which pushes), while vault close /
    /// vault switch go through `reset()` (which clears without
    /// pushing — those tabs were not closed one-by-one by the user).
    /// `@Published` so AppState can mirror the emptiness signal into
    /// its menu-enablement property (`canReopenClosedTab`).
    @Published private(set) var closedTabs: [ClosedTabRecord] = []

    /// Remove reopen records matching `predicate`. Deletion flows call
    /// this AFTER auto-closing an entity's tabs: without the purge,
    /// deleting a saved query/dashboard leaves its just-pushed (and any
    /// older) records reopenable, and ⇧⌘T resurrects the deleted id as
    /// a failure tab announcing "Reopened…" (Codex review, chords PR).
    func purgeClosedTabs(where predicate: (ClosedTabRecord) -> Bool) {
        closedTabs.removeAll(where: predicate)
    }

    /// Pop the most recently closed tab's record, or nil when the
    /// stack is empty. AppState's `reopenClosedTab()` consumes this
    /// and routes the reopen through the standard open funnel.
    func popClosedTab() -> ClosedTabRecord? {
        closedTabs.popLast()
    }

    func canvasSurface(for tabID: TabID) -> CanvasSurface {
        canvasSurfaces[tabID] ?? .outline
    }

    func setCanvasSurface(_ surface: CanvasSurface, for tabID: TabID) {
        if surface == .outline {
            canvasSurfaces[tabID] = nil  // sparse: outline is the absent default
        } else {
            canvasSurfaces[tabID] = surface
        }
    }

    // MARK: Queries

    var activeTab: WorkspaceTab? { model.activeGroup.activeTab }

    var activeTabPath: String? {
        if case .markdown(let path) = activeTab?.item { return path }
        return nil
    }

    func document(for tabID: TabID) -> NoteDocument? { documents[tabID] }

    func tabPath(_ tab: WorkspaceTab) -> String {
        tab.item.path
    }

    func tabTitle(_ tab: WorkspaceTab) -> String {
        tab.item.title
    }

    /// The tab in the ACTIVE group holding `path`, if any (dedup rule
    /// scope) — kind-agnostic: a path opens as exactly one item kind, so
    /// equal paths are one tab (#369 openTab dedup).
    func activeGroupTab(forPath path: String) -> WorkspaceTab? {
        model.activeGroup.tabs.first { $0.item.path == path }
    }

    /// The `.base` tab in the active group whose path has the exact UTF-8
    /// identity used by SQLite. Base paths must not use Swift's canonical
    /// Unicode equivalence because Core can store both byte-distinct names.
    func activeGroupBaseTab(forPath path: String) -> WorkspaceTab? {
        model.activeGroup.tabs.first {
            guard case .base(let candidate) = $0.item else { return false }
            return BaseExactIdentity.matches(candidate, path)
        }
    }

    func activeGroupSavedQueryTab(id: String) -> WorkspaceTab? {
        model.activeGroup.tabs.first {
            if case .savedQuery(let queryID, _) = $0.item {
                return BaseExactIdentity.matches(queryID, id)
            }
            return false
        }
    }

    func activeGroupDashboardTab(id: String) -> WorkspaceTab? {
        model.activeGroup.tabs.first {
            if case .dashboard(let dashboardID, _) = $0.item {
                return BaseExactIdentity.matches(dashboardID, id)
            }
            return false
        }
    }

    /// True when any tab — parked or (per the caller-supplied flag) active —
    /// has unsaved changes. Vault close gates on this aggregate.
    func anyTabDirty(activeTabDirty: Bool) -> Bool {
        if activeTabDirty { return true }
        let activeID = model.activeGroup.activeTabID
        return documents.contains { id, doc in id != activeID && doc.hasUnsavedChanges }
    }

    /// All dirty parked documents (excludes the active tab — the caller owns
    /// its state). Used by vault-close Save All.
    func dirtyParkedDocuments() -> [NoteDocument] {
        let activeID = model.activeGroup.activeTabID
        return documents
            .filter { id, doc in id != activeID && doc.hasUnsavedChanges }
            .map(\.value)
            .sorted { $0.path < $1.path }
    }

    // MARK: Snapshot / restore (the tab-switch core)

    /// Capture the active tab's state from AppState's fields into its parked
    /// document. Call BEFORE the note-load teardown clears the fields.
    ///
    /// `loadedFilePath` must match the active tab's path — the fields being
    /// parked must actually BELONG to that tab. This guard makes an entire
    /// bug class (parking buffer A under tab B after a model mutation)
    /// structurally impossible rather than merely avoided by call order.
    func snapshotActiveTab(
        text: String?, baseline: String?, contentHash: String?,
        hasUnsavedChanges: Bool, saveError: String?, saveConflict: SaveConflict?,
        loadedFilePath: String?,
        fmSource: String = "", bodyByteOffset: Int = 0, bodyLineOffset: Int = 0
    ) {
        guard let tab = activeTab, case .markdown(let path) = tab.item,
            let text, let baseline,
            loadedFilePath == path
        else { return }
        let doc = documents[tab.id] ?? NoteDocument(id: tab.id, path: path)
        doc.text = text
        doc.savedBaselineText = baseline
        doc.contentHash = contentHash
        doc.hasUnsavedChanges = hasUnsavedChanges
        doc.saveError = saveError
        doc.saveConflict = saveConflict
        doc.fmSource = fmSource
        doc.bodyByteOffset = bodyByteOffset
        doc.bodyLineOffset = bodyLineOffset
        doc.hasLoaded = true
        documents[tab.id] = doc
    }

    /// U3-3: a property edit (or U3-4 source commit) rewrote the file's
    /// frontmatter — same-path parked documents must carry the fresh fm,
    /// offsets, and hash, or a later composed save from a duplicated tab
    /// would resurrect stale frontmatter over the new bytes.
    func mirrorFrontmatter(
        path: String, fmSource: String, bodyByteOffset: Int,
        bodyLineOffset: Int, contentHash: String?
    ) {
        for doc in documents.values where doc.path == path {
            doc.fmSource = fmSource
            doc.bodyByteOffset = bodyByteOffset
            doc.bodyLineOffset = bodyLineOffset
            doc.contentHash = contentHash
        }
    }

    /// Mirror a live edit into every same-path parked document so a
    /// duplicated tab renders current bytes (copy-on-write: O(1) per doc).
    /// Dirty state mirrors too — the duplicate IS the same buffer.
    func mirrorEdit(path: String, text: String, hasUnsavedChanges: Bool) {
        for doc in documents.values where doc.path == path {
            doc.text = text
            doc.hasUnsavedChanges = hasUnsavedChanges
        }
    }

    /// Same-path parked documents also share save results (baseline/hash
    /// move together — they are one file).
    func mirrorSaveResult(path: String, baseline: String, contentHash: String?) {
        for doc in documents.values where doc.path == path {
            doc.savedBaselineText = baseline
            doc.text = baseline
            doc.contentHash = contentHash
            doc.hasUnsavedChanges = false
            doc.saveConflict = nil
            doc.saveError = nil
        }
    }

    // MARK: Retarget (U2-5 file rename/move follow)

    /// The single mutation point for an open file whose PATH changed on disk —
    /// rename, move, or an ancestor folder moving (U2-5, #463). Every tab
    /// holding `.markdown(old)` is rewritten to `.markdown(new)` (tab identity,
    /// order, and focus preserved), and every matching parked `NoteDocument` is
    /// rebound to the new path with its buffer/dirty/hash/baseline state carried
    /// over — the FILE moved, the in-memory buffer is still valid, and the
    /// content-hash conflict machinery stays correct because the hash travels
    /// with the CONTENT, not the path.
    ///
    /// Returns the ids of the tabs that were retargeted. AppState uses this to
    /// tell whether the ACTIVE tab (whose document lives in AppState's fields,
    /// not in `documents`) was among them, so it can rebind `loadedFilePath` /
    /// `selectedFilePath` to the new path in the same turn.
    ///
    /// A no-op (`old == new`, or the file isn't open anywhere) returns `[]`.
    @discardableResult
    func retarget(old: String, new: String) -> [TabID] {
        let crossesBaseBoundary = old.lowercased().hasSuffix(".base")
            || new.lowercased().hasSuffix(".base")
        let isSamePath = crossesBaseBoundary
            ? BaseExactIdentity.matches(old, new)
            : old == new
        guard !isSamePath else { return [] }
        var changed = model.retargetItem(
            from: .markdown(path: old), to: .markdown(path: new))
        changed += model.retargetItem(
            from: .canvas(path: old), to: .canvas(path: new))
        changed += model.retargetItem(
            from: .base(path: old), to: .base(path: new))
        // Rebind parked documents: `NoteDocument.path` is a `let`, so a moved
        // file gets a fresh document that inherits the old one's buffer state.
        // Only tabs that were actually retargeted are touched (the ACTIVE tab
        // has no `documents` entry while active — AppState owns its fields).
        for tabID in changed {
            // Rebind reading-scroll parks alongside documents (red-team:
            // a rename while the note was parked in reading mode
            // orphaned the offset — park.path kept the OLD path).
            if let park = readingScrollParks[tabID], park.path == old {
                readingScrollParks[tabID] = ReadingScrollPark(
                    path: new, blockIndex: park.blockIndex)
            }
            guard let old = documents[tabID] else { continue }
            let rebound = NoteDocument(id: tabID, path: new)
            rebound.text = old.text
            rebound.savedBaselineText = old.savedBaselineText
            rebound.contentHash = old.contentHash
            rebound.hasUnsavedChanges = old.hasUnsavedChanges
            rebound.saveError = old.saveError
            rebound.saveConflict = old.saveConflict
            rebound.hasLoaded = old.hasLoaded
            documents[tabID] = rebound
        }
        assert(model.validate().isEmpty, "workspace invariants: \(model.validate())")
        return changed
    }

    @discardableResult
    func retargetSavedQuery(id: String, name: String) -> [TabID] {
        let changed = model.retargetSavedQuery(id: id, name: name)
        assert(model.validate().isEmpty, "workspace invariants: \(model.validate())")
        return changed
    }

    @discardableResult
    func retargetDashboard(id: String, name: String) -> [TabID] {
        let changed = model.retargetDashboard(id: id, name: name)
        assert(model.validate().isEmpty, "workspace invariants: \(model.validate())")
        return changed
    }

    /// Close every tab (across all groups) whose item is `.markdown(path)` —
    /// the workspace half of a DELETE of an open file (U2-5, #463). Returns the
    /// ids of the tabs that were closed, plus whether the ACTIVE tab was among
    /// them (so AppState can flip its live fields to the missing-file error
    /// state). Tabs pointing elsewhere are untouched.
    ///
    /// NOTE: the spec's chosen behavior for delete-while-open is to flip the tab
    /// to the missing-file error STATE, not to close it — so AppState drives
    /// that directly for the active tab (see `deleteEntry`). This helper exists
    /// for the parked-tab arm: a deleted file open in a *background* tab should
    /// surface its error the next time it's activated. We keep those tabs open
    /// (they load lazily and hit `noteLoadError`), so this returns the set for
    /// bookkeeping without mutating — the parked documents are simply dropped so
    /// the next activation re-reads from disk and fails into the error state.
    @discardableResult
    func invalidateParkedDocuments(forPath path: String) -> [TabID] {
        let affected = model.allTabs
            .filter { $0.item == .markdown(path: path) }
            .map(\.id)
        for id in affected { documents[id] = nil }
        return affected
    }

    // MARK: Model mutations (funneled)

    /// U1-4 compatibility: mirror a single selection (replace-in-place /
    /// close). Still the path taken when selection changes WITHOUT a tab
    /// switch (fresh open into the current tab).
    func mirrorSingleSelection(_ path: String?) {
        if let path {
            replaceActiveItem(.markdown(path: path))
        } else if let activeTab = model.activeGroup.activeTabID {
            close(activeTab)
        }
        assert(model.validate().isEmpty, "workspace invariants: \(model.validate())")
    }

    /// Replace the active tab's item (single-click open). Drops the parked
    /// document of the replaced item if no other tab holds that path.
    func replaceActiveItem(_ item: EditorItem) {
        let previous = activeTab
        model.replaceActiveTabItem(item)
        if let previous, previous.id == model.activeGroup.activeTabID,
            previous.item != model.activeGroup.activeTab?.item {
            // Same tab, new item: the old item's parked snapshot (keyed by
            // this tab id) no longer describes the tab's content.
            documents[previous.id] = nil
            // #856: same rule for the reading-scroll park (belt — the
            // path check in `parkedReadingScroll` is the braces).
            readingScrollParks[previous.id] = nil
        }
    }

    @discardableResult
    func openTab(
        _ item: EditorItem, activate: Bool = true, allowDuplicate: Bool = false
    ) -> TabID {
        let id = model.openTab(item, activate: activate, allowDuplicate: allowDuplicate)
        assert(model.validate().isEmpty)
        return id
    }

    func select(_ tabID: TabID) {
        model.selectTab(tabID)
    }

    func selectNext() { model.selectNextTab() }
    func selectPrevious() { model.selectPreviousTab() }
    func select(ordinal: Int) { model.selectTab(ordinal: ordinal) }

    func moveActiveTab(by delta: Int) {
        guard let active = model.activeGroup.activeTabID,
            let idx = model.activeGroup.tabs.firstIndex(where: { $0.id == active })
        else { return }
        model.moveTab(active, toIndex: idx + delta)
    }

    @discardableResult
    func close(_ tabID: TabID) -> WorkspaceModel.CloseOutcome {
        // #863: record the closing tab for Reopen Closed Tab. Captured
        // BEFORE the model mutation — the owning group is unknowable
        // after a collapse. This is the single push point: every user
        // close flow (⌘W, tab-strip button, context menu, the
        // save/discard gate resolutions, Close Pane) funnels through
        // here, while vault close/switch replaces the model wholesale
        // via `reset()` and therefore never pushes.
        if let tab = model.tab(tabID), let owner = model.groupContaining(tabID) {
            closedTabs.append(ClosedTabRecord(item: tab.item, groupID: owner.id))
            if closedTabs.count > Self.closedTabCapacity {
                closedTabs.removeFirst(closedTabs.count - Self.closedTabCapacity)
            }
        }
        let outcome = model.closeTab(tabID)
        documents[tabID] = nil
        viewModes[tabID] = nil
        readingScrollParks[tabID] = nil
        propertiesCollapsed.remove(tabID)
        canvasSurfaces[tabID] = nil
        assert(model.validate().isEmpty)
        return outcome
    }

    /// Vault close / vault switch: drop every tab and parked document. The
    /// model returns to the single-empty-root state (I3). The active leaf
    /// resets to the default (U4-1) so a freshly-opened vault starts on
    /// Outline until its own `workspace.json` restores a remembered leaf.
    func reset() {
        model = WorkspaceModel()
        documents = [:]
        viewModes = [:]
        readingScrollParks = [:]
        propertiesCollapsed = []
        canvasSurfaces = [:]
        // #863: the reopen stack is per-vault-session — tabs from a
        // closed vault must not resurrect inside the next one.
        closedTabs = []
        activeLeaf = .outline
        focusRegion = .editor
        lastFocusedGroup = nil
    }

    /// Session restore (U1-6): adopt a rebuilt model wholesale. The caller
    /// (WorkspaceStore.model(from:)) has already validated; the assert is
    /// the belt-and-suspenders. Parked documents start empty — tabs load
    /// lazily on first activation, missing files surface the existing
    /// per-tab load-error state. `viewModes` restores the per-tab reading
    /// modes captured in the snapshot (U3-2); unknown ids are dropped.
    func adopt(
        _ restored: WorkspaceModel,
        viewModes restoredModes: [TabID: NoteViewMode] = [:],
        propertiesCollapsed restoredCollapsed: Set<TabID> = [],
        canvasSurfaces restoredSurfaces: [TabID: CanvasSurface] = [:]
    ) {
        assert(restored.validate().isEmpty)
        model = restored
        documents = [:]
        // #856: transient by contract — a restored session starts with
        // no reading-scroll parks (never persisted; see the property doc).
        readingScrollParks = [:]
        let knownIDs = Set(restored.allTabs.map(\.id))
        viewModes = restoredModes
            .filter { knownIDs.contains($0.key) && $0.value != .editing }
        propertiesCollapsed = restoredCollapsed.intersection(knownIDs)
        canvasSurfaces = restoredSurfaces
            .filter { knownIDs.contains($0.key) && $0.value != .outline }
        focusRegion = .editor
        lastFocusedGroup = nil
    }

    // MARK: Splits (U1-3)

    /// Split `groupID` along `axis` (duplicate-active-item semantics — the
    /// model's default). Returns the new group, or nil when the model
    /// rejects (empty group / at the 6-pane capacity).
    @discardableResult
    func split(_ groupID: GroupID, axis: SplitBranch.Axis) -> GroupID? {
        let created = model.split(groupID, axis: axis)
        assert(model.validate().isEmpty)
        return created
    }

    var isAtPaneCapacity: Bool {
        model.groupsInOrder.count >= WorkspaceModel.maxGroups
    }

    var hasSplits: Bool {
        model.groupsInOrder.count > 1
    }

    func focusGroup(_ id: GroupID) {
        model.focusGroup(id)
    }

    @discardableResult
    func focusNeighbor(_ direction: WorkspaceModel.Direction) -> GroupID? {
        let target = model.focusNeighbor(direction)
        assert(model.validate().isEmpty)
        return target
    }

    /// Non-mutating spatial-neighbor probe: the interior editor group ⌘⌥`dir`
    /// would move to, WITHOUT changing focus. Used by `resolveFocusRouting`
    /// (and the terminal-region census) so the routing decision is side-effect
    /// free — the mutation happens only when the outcome is applied.
    func peekNeighbor(_ direction: WorkspaceModel.Direction) -> GroupID? {
        var copy = model
        return copy.focusNeighbor(direction)
    }

    /// Divider drag commit (U1-3): weights for the split containing
    /// `groupID`, clamped by the model.
    func setWeights(_ weights: [Double], forSplitContaining groupID: GroupID) {
        model.setWeights(weights, forSplitContaining: groupID)
        assert(model.validate().isEmpty)
    }

    /// Keyboard resize (⌘⌥= / ⌘⌥-).
    func adjustFocusedPaneWeight(by delta: Double) {
        model.setWeight(delta: delta, for: model.activeGroupID)
        assert(model.validate().isEmpty)
    }

    /// The fraction of its split axis the focused pane currently occupies —
    /// for the resize announcement ("Pane resized, N percent").
    var focusedPaneFraction: Double? {
        guard hasSplits else { return nil }
        let rects = model.groupRects()
        guard let rect = rects[model.activeGroupID] else { return nil }
        // Announce the dominant (most-recently-resized) dimension: the one
        // that differs most from an even share.
        return max(rect.width, rect.height) == 1
            ? min(rect.width, rect.height)
            : max(rect.width, rect.height)
    }

    // MARK: - Focus regions (U4-4, #473)

    /// Where window focus lives, at the granularity ⌘⌥arrows route between.
    ///
    /// The workspace is three terminal regions west→east: the file tree
    /// (westernmost), the editor groups (the split tree), and the right-pane
    /// leaf (easternmost). Only `.editor` maps to a `WorkspaceModel` node —
    /// tree and leaf are window chrome, NOT groups, so they live here beside
    /// `activeLeaf` (same rationale) rather than inside the model, whose
    /// invariants I1–I7 assume every focusable node is a `TabGroupNode`. The
    /// 800-seed geometry census in `WorkspaceModel` is untouched: it still
    /// owns editor↔editor moves; this layer only wraps it at the two edges.
    enum FocusRegion: Equatable { case tree, editor, leaf }

    /// The currently-focused region. Starts on `.editor` (the note is the
    /// primary surface); a terminal move parks the editor's group in
    /// `lastFocusedGroup` so the reverse move restores exactly it.
    @Published private(set) var focusRegion: FocusRegion = .editor

    /// The editor group that held focus when we last left `.editor` for a
    /// terminal region — the round-trip anchor (⌘⌥← from the leaf, ⌘⌥→ from
    /// the tree return to THIS group, not merely "the first group"). Survives
    /// the excursion because it lives here, not in the model. Repaired to the
    /// active group if that group is later collapsed (never dangles → I7's
    /// "focus never lost" extends across the region boundary).
    private(set) var lastFocusedGroup: GroupID?

    /// A monotonically-bumped token the terminal-region views observe to pull
    /// keyboard + AX focus to themselves. SwiftUI `@FocusState` /
    /// `@AccessibilityFocusState` are view-local, so AppState can't assign
    /// them directly; it bumps the matching request and the view mirrors it
    /// into its own focus state on `.onChange`. Separate tokens per terminal
    /// so a leaf request doesn't also re-fire the tree.
    @Published private(set) var treeFocusRequest: Int = 0
    @Published private(set) var leafFocusRequest: Int = 0

    /// The decision a ⌘⌥arrow makes, as a pure value (no mutation, no
    /// effects) — so the terminal-region routing is censusable without a
    /// running AppState or a rendered view, exactly as `WorkspaceModel.
    /// focusNeighbor` and `Leaf.railMove` are. `AppState.focusPane` resolves
    /// this, then performs the matching mutation + announcement + focus
    /// signal.
    enum FocusRoutingOutcome: Equatable {
        /// Interior editor move: focus this group (activate its tab, announce
        /// "Editor pane N of M, …").
        case editorGroup(GroupID)
        /// Cross into the tree terminal (announce "Files.").
        case enterTree
        /// Cross into the leaf terminal (announce "<leaf> panel.").
        case enterLeaf
        /// Return to the editor region, landing on this group.
        case returnToEditor(GroupID)
        /// Edge — focus unchanged (the request is a no-op).
        case none
    }

    /// Resolve a ⌘⌥arrow against the current region + model geometry. Pure:
    /// reads `focusRegion`, `lastFocusedGroup`, and `model` (via
    /// `focusNeighbor`, which does NOT mutate when it only probes — see the
    /// caller: the census resolves-then-applies so probing is side-effect
    /// free at decision time). The three-region state machine, verbatim from
    /// u4_spec §U4-4:
    ///
    /// - `.tree` (westernmost): ⌘⌥→ returns to the editor; the other three
    ///   are edges → `.none`.
    /// - `.leaf` (easternmost): ⌘⌥← returns to the editor; ⌘⌥→ is the far
    ///   edge; the rest → `.none`.
    /// - `.editor`: an interior neighbor is `.editorGroup`; off a horizontal
    ///   edge, ⌘⌥← crosses to the tree and ⌘⌥→ to the leaf; vertical edges
    ///   have no terminal (tree/leaf flank E/W only) → `.none`.
    ///
    /// `neighbor` is the interior spatial neighbor in `direction` (the caller
    /// passes `model.focusNeighbor`'s result computed on a copy so this stays
    /// non-mutating) — nil means "no interior neighbor", i.e. an edge.
    func resolveFocusRouting(
        _ direction: WorkspaceModel.Direction, interiorNeighbor neighbor: GroupID?
    ) -> FocusRoutingOutcome {
        switch focusRegion {
        case .tree:
            return direction == .right ? .returnToEditor(resolvedReturnGroup) : .none
        case .leaf:
            return direction == .left ? .returnToEditor(resolvedReturnGroup) : .none
        case .editor:
            if let neighbor { return .editorGroup(neighbor) }
            switch direction {
            case .left: return .enterTree
            case .right: return .enterLeaf
            case .up, .down: return .none
            }
        }
    }

    /// The group a return-to-editor lands on: the parked anchor when it still
    /// exists, else the model's current active group (a collapse ate the
    /// anchor mid-excursion — focus still resolves, never lost).
    var resolvedReturnGroup: GroupID {
        if let anchor = lastFocusedGroup, model.group(anchor) != nil { return anchor }
        return model.activeGroupID
    }

    /// Enter the file tree region (⌘⌥← from the leftmost editor group). Parks
    /// the current editor group as the round-trip anchor and signals the tree
    /// view to take focus.
    func focusTreeRegion() {
        if focusRegion == .editor { lastFocusedGroup = model.activeGroupID }
        focusRegion = .tree
        treeFocusRequest &+= 1
    }

    /// Enter the right-pane leaf region (⌘⌥→ from the rightmost editor group).
    /// Parks the current editor group and signals the leaf view to take focus.
    func focusLeafRegion() {
        if focusRegion == .editor { lastFocusedGroup = model.activeGroupID }
        focusRegion = .leaf
        leafFocusRequest &+= 1
    }

    /// Return to the editor region from a terminal (⌘⌥→ from the tree, ⌘⌥←
    /// from the leaf). Restores `lastFocusedGroup` when it still exists; falls
    /// back to the model's current active group otherwise (a collapse ate the
    /// anchor while we were away). Returns the group focus landed on so the
    /// caller can activate its tab through the identity funnel and announce.
    @discardableResult
    func focusEditorRegion() -> GroupID {
        focusRegion = .editor
        if let anchor = lastFocusedGroup, model.group(anchor) != nil {
            model.focusGroup(anchor)
        }
        lastFocusedGroup = nil
        return model.activeGroupID
    }

    /// Called whenever the editor region is (re-)entered by an ordinary
    /// interior focus move / tab activation, so a subsequent terminal move
    /// anchors on the right group. Keeps `focusRegion` truthful without
    /// forcing every editor-focus path through `focusEditorRegion`.
    func markEditorRegionActive() {
        focusRegion = .editor
        lastFocusedGroup = nil
    }

    // MARK: Passive region mirrors (U4-4 review)

    /// The region must also track focus that arrived WITHOUT a routing
    /// command — native Tab order or a mouse click into a terminal — so the
    /// next ⌘⌥arrow behaves per spec ("from the tree, ⌘⌥→ returns to the
    /// editor" regardless of how focus got to the tree). Views mirror their
    /// real focus state here on `.onChange` (post-update, #448-safe). No
    /// focus request is bumped — focus is already where it is; only the
    /// region bookkeeping catches up. Entering from `.editor` parks the
    /// round-trip anchor exactly like the command path; re-entry while
    /// already in the region never re-parks (idempotent).
    func noteTreeFocusChanged(_ focused: Bool) {
        noteTerminalFocusChanged(.tree, focused: focused)
    }

    func noteLeafFocusChanged(_ focused: Bool) {
        noteTerminalFocusChanged(.leaf, focused: focused)
    }

    private func noteTerminalFocusChanged(_ region: FocusRegion, focused: Bool) {
        if focused {
            if focusRegion == .editor { lastFocusedGroup = model.activeGroupID }
            if focusRegion != region { focusRegion = region }
        } else if focusRegion == region {
            // Only demote the region we own — a tree→leaf Tab interleaves
            // (tree loses focus, leaf gains) and must converge on .leaf.
            focusRegion = .editor
        }
    }
}
