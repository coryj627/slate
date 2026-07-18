// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

/// Pure file-sidebar focus and selection state.
///
/// The identity is deliberately generic: file rows may encode their path in the
/// identity while directory rows may use a stable database identifier. Paths
/// are snapshotted separately so a recycled stable identifier cannot silently
/// select an unrelated row after a tree refresh.
struct SidebarSelectionModel<Identity: Hashable>: Equatable {
    struct VisibleRow: Equatable {
        let identity: Identity
        let path: String
        let isDirectory: Bool
        /// Indexed file metadata captured from the live tree. Directories are
        /// always false. Callers that only exercise selection geometry may
        /// omit it; production tree rows always pass the `FileSummary` value.
        let isMarkdown: Bool

        init(
            identity: Identity,
            path: String,
            isDirectory: Bool,
            isMarkdown: Bool = false
        ) {
            self.identity = identity
            self.path = path
            self.isDirectory = isDirectory
            self.isMarkdown = isDirectory ? false : isMarkdown
        }
    }

    struct KnownMove: Equatable {
        let oldPath: String
        let newPath: String
        let isDirectory: Bool

        init(oldPath: String, newPath: String, isDirectory: Bool = true) {
            self.oldPath = oldPath
            self.newPath = newPath
            self.isDirectory = isDirectory
        }
    }

    struct KnownMoveIndex {
        let entryCount: Int
        private let storage: VaultComponentPrefixIndex<KnownMove>

        init(_ moves: [KnownMove]) {
            entryCount = moves.count
            storage = VaultComponentPrefixIndex(moves.map {
                .init(
                    path: $0.oldPath,
                    includesDescendants: $0.isDirectory,
                    value: $0)
            })
        }

        func remappedPath(
            _ path: String,
            componentVisits: inout Int
        ) -> String? {
            guard let match = storage.longestMatch(
                for: path, componentVisits: &componentVisits)
            else { return nil }
            guard !match.relativeSuffix.isEmpty else {
                return match.entry.value.newPath
            }
            let root = match.entry.value.newPath
            return root.isEmpty
                ? match.relativeSuffix
                : root + "/" + match.relativeSuffix
        }
    }

    struct KnownRemoval: Equatable {
        let path: String
        let isDirectory: Bool
    }

    struct KnownRemovalIndex {
        let entryCount: Int
        private let storage: VaultComponentPrefixIndex<KnownRemoval>

        init(_ removals: [KnownRemoval]) {
            entryCount = removals.count
            storage = VaultComponentPrefixIndex(removals.map {
                .init(
                    path: $0.path,
                    includesDescendants: $0.isDirectory,
                    value: $0)
            })
        }

        func covers(_ path: String, componentVisits: inout Int) -> Bool {
            storage.longestMatch(for: path, componentVisits: &componentVisits) != nil
        }
    }

    enum PointerClick: Equatable {
        case plain
        case toggle
        case range
    }

    enum ArrowDirection: Equatable {
        case up
        case down
    }

    struct Transition: Equatable {
        let handled: Bool
        let changed: Bool
        let focusChanged: Bool
    }

    struct PointerOutcome: Equatable {
        let selection: Set<Identity>
        let anchor: Identity?
        let focus: Identity?
    }

    private(set) var focused: Identity?
    private(set) var selected: Set<Identity>
    private(set) var selectionPathSnapshots: [Identity: String]
    private(set) var rangeAnchor: Identity?
    private(set) var rangeAnchorPathSnapshot: String?
    /// Monotone user-intent generation captured by deferred import landing.
    /// Structural reconciliation/focus deliberately does not advance it.
    private(set) var selectionRevision: UInt64

    init(
        focused: Identity? = nil,
        selected: Set<Identity> = [],
        selectionPathSnapshots: [Identity: String] = [:],
        rangeAnchor: Identity? = nil,
        rangeAnchorPathSnapshot: String? = nil,
        selectionRevision: UInt64 = 0
    ) {
        self.focused = focused
        self.selected = selected
        self.selectionPathSnapshots = selectionPathSnapshots
        self.rangeAnchor = rangeAnchor
        self.rangeAnchorPathSnapshot = rangeAnchorPathSnapshot
        self.selectionRevision = selectionRevision
    }

    /// Fold the identity-only part of a pointer transition. The stateful API
    /// below adds path validation and snapshots around this shipped click matrix.
    static func pointerOutcome(
        order: [Identity],
        current: Set<Identity>,
        anchor: Identity?,
        clicked: Identity,
        click: PointerClick
    ) -> PointerOutcome {
        switch click {
        case .plain:
            return PointerOutcome(selection: [clicked], anchor: clicked, focus: clicked)
        case .toggle:
            var next = current
            if next.contains(clicked) {
                next.remove(clicked)
                return PointerOutcome(
                    selection: next,
                    anchor: clicked,
                    focus: order.last(where: { next.contains($0) }))
            }
            next.insert(clicked)
            return PointerOutcome(selection: next, anchor: clicked, focus: clicked)
        case .range:
            guard
                let anchor,
                let anchorIndex = order.firstIndex(of: anchor),
                let clickedIndex = order.firstIndex(of: clicked)
            else {
                return PointerOutcome(selection: [clicked], anchor: clicked, focus: clicked)
            }
            let lowerBound = min(anchorIndex, clickedIndex)
            let upperBound = max(anchorIndex, clickedIndex)
            return PointerOutcome(
                selection: Set(order[lowerBound...upperBound]),
                anchor: anchor,
                focus: clicked)
        }
    }

    /// Apply one plain, Command-toggle, or Shift-range pointer click.
    @discardableResult
    mutating func applyPointerClick(
        _ click: PointerClick,
        row: VisibleRow,
        visibleRows: [VisibleRow]
    ) -> Transition {
        let previous = self
        let validatedAnchor = pathValidatedAnchor(in: visibleRows)
        let outcome = Self.pointerOutcome(
            order: visibleRows.map(\.identity),
            current: selected,
            anchor: validatedAnchor,
            clicked: row.identity,
            click: click)

        switch click {
        case .plain:
            replaceSelection(with: [row])
            rangeAnchor = row.identity
            rangeAnchorPathSnapshot = row.path
        case .toggle:
            if selected.contains(row.identity) {
                selected.remove(row.identity)
                selectionPathSnapshots.removeValue(forKey: row.identity)
            } else {
                selected.insert(row.identity)
                selectionPathSnapshots[row.identity] = row.path
            }
            rangeAnchor = row.identity
            rangeAnchorPathSnapshot = row.path
        case .range:
            if outcome.anchor == row.identity && validatedAnchor == nil {
                replaceSelection(with: [row])
                rangeAnchor = row.identity
                rangeAnchorPathSnapshot = row.path
            } else if let anchorIndex = visibleRows.firstIndex(where: {
                $0.identity == outcome.anchor && $0.path == rangeAnchorPathSnapshot
            }), let clickedIndex = visibleRows.firstIndex(of: row) {
                let lowerBound = min(anchorIndex, clickedIndex)
                let upperBound = max(anchorIndex, clickedIndex)
                replaceSelection(with: Array(visibleRows[lowerBound...upperBound]))
            } else {
                replaceSelection(with: [row])
                rangeAnchor = row.identity
                rangeAnchorPathSnapshot = row.path
            }
        }
        if click == .toggle && !selected.contains(row.identity) {
            focused = selectedVisibleRows(in: visibleRows).last?.identity
        } else {
            focused = outcome.focus
        }
        return userIntentTransition(from: previous)
    }

    /// Programmatic reveal is an unconditional single-selection collapse. A nil
    /// reveal clears focus, selection, and anchor together.
    @discardableResult
    mutating func reveal(_ row: VisibleRow?) -> Transition {
        let previous = self
        guard let row else {
            clear()
            return transition(from: previous)
        }
        replaceSelection(with: [row])
        focused = row.identity
        rangeAnchor = row.identity
        rangeAnchorPathSnapshot = row.path
        return transition(from: previous)
    }

    /// A List/type-select/navigation reveal originating from explicit user
    /// input. Unlike the programmatic `reveal`, even a same-row intent advances
    /// the generation so deferred import focus cannot overwrite newer agency.
    @discardableResult
    mutating func revealFromUserIntent(_ row: VisibleRow?) -> Transition {
        let result = reveal(row)
        selectionRevision &+= 1
        return result
    }

    /// Record user navigation that originated outside the tree (for example a
    /// search or graph result). The selected row may not have changed yet—or
    /// may already be the same row—so this deliberately advances only the
    /// intent generation. The selected-path mirror remains responsible for the
    /// eventual single-row projection.
    @discardableResult
    mutating func noteExternalNavigationIntent() -> Transition {
        selectionRevision &+= 1
        return Transition(handled: true, changed: true, focusChanged: false)
    }

    /// Programmatic structural focus that preserves a surviving multi-selection
    /// when the target is already one of its path-valid members. Otherwise it
    /// establishes the deterministic single fallback. The view suppresses open.
    @discardableResult
    mutating func focusAfterStructuralMutation(_ row: VisibleRow) -> Transition {
        let previous = self
        if selected.contains(row.identity),
            selectionPathSnapshots[row.identity] == row.path {
            focused = row.identity
        } else {
            replaceSelection(with: [row])
            focused = row.identity
            rangeAnchor = row.identity
            rangeAnchorPathSnapshot = row.path
        }
        return transition(from: previous)
    }

    /// Apply deferred structural focus only when no newer selection/focus
    /// intent has occurred. The comparison and mutation are one value-semantic
    /// operation, so returning to the captured path cannot fool path equality.
    @discardableResult
    mutating func focusAfterStructuralMutation(
        _ row: VisibleRow,
        ifSelectionRevisionIs expectedRevision: UInt64
    ) -> Transition {
        guard selectionRevision == expectedRevision else {
            return Transition(handled: false, changed: false, focusChanged: false)
        }
        return focusAfterStructuralMutation(row)
    }

    /// Atomically install the exact provider-ordered imported-result set and
    /// focus the first imported result. A newer user intent
    /// rejects the complete landing; no partial selection may leak through.
    @discardableResult
    mutating func selectImportedResults(
        _ rows: [VisibleRow],
        ifSelectionRevisionIs expectedRevision: UInt64
    ) -> Transition {
        let previous = self
        guard selectionRevision == expectedRevision else {
            return Transition(handled: false, changed: false, focusChanged: false)
        }
        guard let first = rows.first else {
            return transition(from: previous)
        }
        replaceSelection(with: rows)
        focused = first.identity
        rangeAnchor = first.identity
        rangeAnchorPathSnapshot = first.path
        return transition(from: previous)
    }

    /// Shift-Up / Shift-Down recomputes one inclusive range between a fixed
    /// anchor and the next focus. Recomputing (rather than unioning) naturally
    /// grows, shrinks, and crosses the pivot. A list boundary is consumed while
    /// leaving the value unchanged.
    @discardableResult
    mutating func extendSelection(
        _ direction: ArrowDirection,
        visibleRows: [VisibleRow]
    ) -> Transition {
        let previous = self
        guard
            let focused,
            let focusIndex = visibleRows.firstIndex(where: {
                $0.identity == focused && isSelected($0.identity, currentPath: $0.path)
            })
        else {
            return userIntentTransition(from: previous)
        }

        let nextIndex = direction == .up ? focusIndex - 1 : focusIndex + 1
        guard visibleRows.indices.contains(nextIndex) else {
            return userIntentTransition(from: previous)
        }

        let anchorIndex: Int
        if let validated = pathValidatedAnchor(in: visibleRows),
            let index = visibleRows.firstIndex(where: { $0.identity == validated }) {
            anchorIndex = index
        } else {
            anchorIndex = focusIndex
            rangeAnchor = focused
            rangeAnchorPathSnapshot = visibleRows[focusIndex].path
        }

        let lowerBound = min(anchorIndex, nextIndex)
        let upperBound = max(anchorIndex, nextIndex)
        replaceSelection(with: Array(visibleRows[lowerBound...upperBound]))
        self.focused = visibleRows[nextIndex].identity
        return userIntentTransition(from: previous)
    }

    /// Command-A selects exactly the flattened visible real rows provided by the
    /// caller. A valid visible focus is retained; otherwise the first row is the
    /// deterministic focus and anchor. Empty input clears the complete model.
    @discardableResult
    mutating func selectAll(visibleRows: [VisibleRow]) -> Transition {
        let previous = self
        guard let first = visibleRows.first else {
            clear()
            return userIntentTransition(from: previous)
        }

        let retainedFocus = focused.flatMap { current in
            visibleRows.first(where: {
                $0.identity == current
                    && selectionPathSnapshots[current] == $0.path
                    && selected.contains(current)
            })
        }
        replaceSelection(with: visibleRows)
        let focusRow = retainedFocus ?? first
        focused = focusRow.identity
        rangeAnchor = focusRow.identity
        rangeAnchorPathSnapshot = focusRow.path
        return userIntentTransition(from: previous)
    }

    /// Reconcile an unexplained tree refresh. A confirmed snapshot mismatch is a
    /// reused identity and is dropped; nil resolution is transient and survives.
    /// The independent anchor is validated before selected-row work.
    @discardableResult
    mutating func reconcile(
        visibleRows: [VisibleRow],
        resolveCurrentPath: (Identity) -> String?
    ) -> Transition {
        let previous = self

        if let rangeAnchor,
            !Self.snapshotSurvives(
                snapshot: rangeAnchorPathSnapshot,
                resolved: resolveCurrentPath(rangeAnchor)) {
            self.rangeAnchor = nil
            rangeAnchorPathSnapshot = nil
        }

        var survivors: Set<Identity> = []
        var snapshots: [Identity: String] = [:]
        for identity in selected {
            let snapshot = selectionPathSnapshots[identity]
            let resolved = resolveCurrentPath(identity)
            if let snapshot, let resolved, snapshot != resolved {
                continue
            }
            survivors.insert(identity)
            if let path = snapshot ?? resolved {
                snapshots[identity] = path
            }
        }
        selected = survivors
        selectionPathSnapshots = snapshots

        if let focused, !survivors.contains(focused) {
            self.focused = visibleRows.first(where: {
                survivors.contains($0.identity)
                    && snapshots[$0.identity] == $0.path
            })?.identity
        }
        return transition(from: previous)
    }

    /// Remap known in-app moves before generic reconciliation. The caller owns
    /// identity semantics: path-keyed file identities return a new identity;
    /// stable directory identities return themselves.
    @discardableResult
    mutating func remapKnownMoves(
        _ moves: [KnownMove],
        identityForRemappedPath: (Identity, String) -> Identity
    ) -> Transition {
        var ignored = 0
        return remapKnownMoves(
            using: KnownMoveIndex(moves),
            identityForRemappedPath: identityForRemappedPath,
            componentVisits: &ignored)
    }

    /// Indexed batch form: build once from authoritative standing changes,
    /// then visit only each selected/anchor path's components.
    @discardableResult
    mutating func remapKnownMoves(
        using index: KnownMoveIndex,
        identityForRemappedPath: (Identity, String) -> Identity,
        componentVisits: inout Int
    ) -> Transition {
        let previous = self
        guard index.entryCount > 0 else { return transition(from: previous) }

        var remappedSelection: Set<Identity> = []
        var remappedSnapshots: [Identity: String] = [:]
        var identityMap: [Identity: Identity] = [:]
        for identity in selected {
            let snapshot = selectionPathSnapshots[identity]
            let newPath = snapshot.flatMap {
                index.remappedPath($0, componentVisits: &componentVisits)
            }
            let newIdentity = newPath.map { identityForRemappedPath(identity, $0) } ?? identity
            remappedSelection.insert(newIdentity)
            if let path = newPath ?? snapshot {
                remappedSnapshots[newIdentity] = path
            }
            identityMap[identity] = newIdentity
        }
        selected = remappedSelection
        selectionPathSnapshots = remappedSnapshots
        if let focused {
            self.focused = identityMap[focused] ?? focused
        }

        if let rangeAnchor,
            let snapshot = rangeAnchorPathSnapshot,
            let newPath = index.remappedPath(
                snapshot, componentVisits: &componentVisits) {
            self.rangeAnchor = identityForRemappedPath(rangeAnchor, newPath)
            rangeAnchorPathSnapshot = newPath
        }
        return transition(from: previous)
    }

    /// Apply the exact physical Trash set in one model transition. The live
    /// focused survivor wins over the captured submission path; when focus was
    /// removed, selection falls forward on equal distance, then backward, then
    /// to the deepest visible surviving parent. The fallback is never opened.
    @discardableResult
    mutating func removeKnownItems(
        using index: KnownRemovalIndex,
        preferredFocusPath: String?,
        visibleRows: [VisibleRow],
        componentVisits: inout Int
    ) -> Transition {
        let previous = self
        guard index.entryCount > 0 else { return transition(from: previous) }

        let originalFocusPath = focused.flatMap { identity in
            selectionPathSnapshots[identity]
                ?? visibleRows.first(where: { $0.identity == identity })?.path
        }
        let fallbackOriginPath = originalFocusPath ?? preferredFocusPath
        let focusWasRemoved = fallbackOriginPath.map {
            index.covers($0, componentVisits: &componentVisits)
        } ?? false

        var survivingSelection: Set<Identity> = []
        var survivingSnapshots: [Identity: String] = [:]
        for identity in selected {
            guard let path = selectionPathSnapshots[identity] else { continue }
            if index.covers(path, componentVisits: &componentVisits) { continue }
            survivingSelection.insert(identity)
            survivingSnapshots[identity] = path
        }
        selected = survivingSelection
        selectionPathSnapshots = survivingSnapshots

        if let anchorPath = rangeAnchorPathSnapshot,
            index.covers(anchorPath, componentVisits: &componentVisits) {
            rangeAnchor = nil
            rangeAnchorPathSnapshot = nil
        }

        if !focusWasRemoved,
            let focused,
            visibleRows.contains(where: {
                $0.identity == focused
                    && (survivingSnapshots[focused] ?? $0.path) == $0.path
            }) {
            return transition(from: previous)
        }

        let originIndex = fallbackOriginPath.flatMap { path in
            visibleRows.firstIndex(where: { $0.path == path })
        }
        let survivorRows = visibleRows.enumerated().filter { _, row in
            selected.contains(row.identity)
                && selectionPathSnapshots[row.identity] == row.path
        }
        let target: VisibleRow?
        if let originIndex, !survivorRows.isEmpty {
            target = survivorRows.min { lhs, rhs in
                let leftDistance = abs(lhs.offset - originIndex)
                let rightDistance = abs(rhs.offset - originIndex)
                if leftDistance == rightDistance {
                    return lhs.offset > rhs.offset
                }
                return leftDistance < rightDistance
            }?.element
        } else if !survivorRows.isEmpty {
            target = survivorRows.first?.element
        } else {
            target = Self.removalFallback(
                originPath: fallbackOriginPath,
                originIndex: originIndex,
                visibleRows: visibleRows,
                index: index,
                componentVisits: &componentVisits)
        }

        if let target {
            focused = target.identity
            if selected.isEmpty {
                replaceSelection(with: [target])
                rangeAnchor = target.identity
                rangeAnchorPathSnapshot = target.path
            }
        } else {
            focused = nil
            if selected.isEmpty {
                rangeAnchor = nil
                rangeAnchorPathSnapshot = nil
            }
        }
        return transition(from: previous)
    }

    /// Visible, path-valid selected rows in deterministic flattened order. This
    /// projection is shared by visual state and later multi-open / drag wiring.
    func selectedVisibleRows(in visibleRows: [VisibleRow]) -> [VisibleRow] {
        visibleRows.filter { isSelected($0.identity, currentPath: $0.path) }
    }

    /// Deterministic operation rows with descendants of selected directories
    /// pruned. The visual selection remains untouched.
    func topLevelOperationRows(in visibleRows: [VisibleRow]) -> [VisibleRow] {
        let rows = selectedVisibleRows(in: visibleRows)
        let selectedDirectoryPaths = Set(rows.lazy.filter(\.isDirectory).map(\.path))
        return rows.filter { candidate in
            if !candidate.path.isEmpty && selectedDirectoryPaths.contains("") {
                return false
            }
            var ancestor = candidate.path
            while let slash = ancestor.lastIndex(of: "/") {
                ancestor = String(ancestor[..<slash])
                if selectedDirectoryPaths.contains(ancestor) {
                    return false
                }
            }
            return true
        }
    }

    /// Strict path validation used by row fills, accessibility traits, focus
    /// targets, and command projections.
    func isSelected(_ identity: Identity, currentPath: String) -> Bool {
        selected.contains(identity) && selectionPathSnapshots[identity] == currentPath
    }

    static func snapshotSurvives(snapshot: String?, resolved: String?) -> Bool {
        guard let snapshot, let resolved else { return true }
        return snapshot == resolved
    }

    private mutating func replaceSelection(with rows: [VisibleRow]) {
        selected = Set(rows.map(\.identity))
        selectionPathSnapshots = [:]
        for row in rows {
            selectionPathSnapshots[row.identity] = row.path
        }
    }

    private mutating func clear() {
        focused = nil
        selected = []
        selectionPathSnapshots = [:]
        rangeAnchor = nil
        rangeAnchorPathSnapshot = nil
    }

    private func pathValidatedAnchor(in visibleRows: [VisibleRow]) -> Identity? {
        guard let rangeAnchor, let rangeAnchorPathSnapshot else { return nil }
        return visibleRows.contains(where: {
            $0.identity == rangeAnchor && $0.path == rangeAnchorPathSnapshot
        }) ? rangeAnchor : nil
    }

    private func transition(from previous: Self) -> Transition {
        Transition(
            handled: true,
            changed: previous != self,
            focusChanged: previous.focused != focused)
    }

    /// Every handled user intent supersedes deferred landing, including a
    /// boundary arrow or same-row click whose value state does not change.
    private mutating func userIntentTransition(from previous: Self) -> Transition {
        let result = transition(from: previous)
        selectionRevision &+= 1
        return result
    }

    private static func isDescendant(_ candidate: String, of directory: String) -> Bool {
        if directory.isEmpty { return !candidate.isEmpty }
        return candidate.hasPrefix(directory + "/")
    }

    private static func removalFallback(
        originPath: String?,
        originIndex: Int?,
        visibleRows: [VisibleRow],
        index: KnownRemovalIndex,
        componentVisits: inout Int
    ) -> VisibleRow? {
        if let originIndex {
            if originIndex + 1 < visibleRows.count {
                for row in visibleRows[(originIndex + 1)...] {
                    if !index.covers(row.path, componentVisits: &componentVisits) {
                        return row
                    }
                }
            }
            if originIndex > 0 {
                for row in visibleRows[..<originIndex].reversed() {
                    if !index.covers(row.path, componentVisits: &componentVisits) {
                        return row
                    }
                }
            }
        }
        guard var parent = originPath else { return nil }
        while let slash = parent.lastIndex(of: "/") {
            parent = String(parent[..<slash])
            if let row = visibleRows.first(where: { $0.path == parent }),
                !index.covers(row.path, componentVisits: &componentVisits) {
                return row
            }
        }
        return nil
    }
}
