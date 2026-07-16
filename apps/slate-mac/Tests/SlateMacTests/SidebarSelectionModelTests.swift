// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

final class SidebarSelectionModelTests: XCTestCase {
    private enum RowID: Hashable {
        case file(String)
        case directory(Int)
    }

    private typealias Model = SidebarSelectionModel<RowID>
    private typealias Row = Model.VisibleRow

    private func file(_ path: String) -> Row {
        Row(identity: .file(path), path: path, isDirectory: false)
    }

    private func directory(_ path: String, id: Int) -> Row {
        Row(identity: .directory(id), path: path, isDirectory: true)
    }

    private func remappedIdentity(_ identity: RowID, _ path: String) -> RowID {
        switch identity {
        case .file:
            return .file(path)
        case .directory:
            return identity
        }
    }

    func testPointerClickMatrixPreservesPlainToggleAndFixedRangeSemantics() {
        let rows = [file("a.md"), file("b.md"), file("c.md"), file("d.md")]
        var model = Model()

        model.applyPointerClick(.plain, row: rows[1], visibleRows: rows)
        XCTAssertEqual(model.selected, [.file("b.md")])
        XCTAssertEqual(model.focused, .file("b.md"))
        XCTAssertEqual(model.rangeAnchor, .file("b.md"))

        model.applyPointerClick(.toggle, row: rows[3], visibleRows: rows)
        XCTAssertEqual(model.selected, [.file("b.md"), .file("d.md")])
        XCTAssertEqual(model.focused, .file("d.md"))
        XCTAssertEqual(model.rangeAnchor, .file("d.md"))

        model.applyPointerClick(.toggle, row: rows[3], visibleRows: rows)
        XCTAssertEqual(model.selected, [.file("b.md")])
        XCTAssertEqual(model.focused, .file("b.md"))
        XCTAssertEqual(
            model.rangeAnchor, .file("d.md"),
            "Command-remove keeps the removed row as the independent range anchor")

        model.applyPointerClick(.range, row: rows[0], visibleRows: rows)
        XCTAssertEqual(model.selected, Set(rows.map(\.identity)))
        XCTAssertEqual(model.focused, .file("a.md"))
        XCTAssertEqual(model.rangeAnchor, .file("d.md"), "Shift does not move the pivot")

        model.applyPointerClick(.range, row: rows[2], visibleRows: rows)
        XCTAssertEqual(model.selected, [.file("c.md"), .file("d.md")])
        XCTAssertEqual(model.rangeAnchor, .file("d.md"), "successive Shift-clicks shrink")
    }

    func testPlainClickCollapsesBatchWhenClickedRowAlreadyHasFocus() {
        let rows = [file("a.md"), file("b.md")]
        var model = Model()
        model.applyPointerClick(.plain, row: rows[1], visibleRows: rows)
        model.applyPointerClick(.toggle, row: rows[0], visibleRows: rows)
        model.applyPointerClick(.toggle, row: rows[1], visibleRows: rows)
        XCTAssertEqual(model.focused, .file("a.md"))
        model.applyPointerClick(.toggle, row: rows[1], visibleRows: rows)
        XCTAssertEqual(model.focused, .file("b.md"))
        XCTAssertEqual(model.selected, Set(rows.map(\.identity)))

        let transition = model.applyPointerClick(.plain, row: rows[1], visibleRows: rows)

        XCTAssertEqual(model.selected, [.file("b.md")], "#643 plain selection always collapses")
        XCTAssertFalse(transition.focusChanged, "the model transition cannot rely on a focus edge")
        XCTAssertTrue(transition.changed, "the same-focus selection collapse is still observable")
    }

    func testShiftArrowGrowsShrinksCrossesAnchorAndHandlesBoundary() {
        let rows = [file("a.md"), file("b.md"), file("c.md"), file("d.md")]
        var model = Model()
        model.applyPointerClick(.plain, row: rows[1], visibleRows: rows)

        XCTAssertTrue(model.extendSelection(.down, visibleRows: rows).changed)
        XCTAssertEqual(model.selected, [.file("b.md"), .file("c.md")])
        XCTAssertEqual(model.focused, .file("c.md"))

        model.extendSelection(.down, visibleRows: rows)
        XCTAssertEqual(model.selected, [.file("b.md"), .file("c.md"), .file("d.md")])

        model.extendSelection(.up, visibleRows: rows)
        XCTAssertEqual(model.selected, [.file("b.md"), .file("c.md")], "range shrinks")
        model.extendSelection(.up, visibleRows: rows)
        XCTAssertEqual(model.selected, [.file("b.md")], "range shrinks to the anchor")
        model.extendSelection(.up, visibleRows: rows)
        XCTAssertEqual(model.selected, [.file("a.md"), .file("b.md")], "range crosses the anchor")
        XCTAssertEqual(model.focused, .file("a.md"))
        XCTAssertEqual(model.rangeAnchor, .file("b.md"), "arrow extension never moves the pivot")

        let boundary = model.extendSelection(.up, visibleRows: rows)
        XCTAssertTrue(boundary.handled)
        XCTAssertFalse(boundary.changed, "a boundary chord is consumed without mutating state")
        XCTAssertEqual(model.selected, [.file("a.md"), .file("b.md")])
    }

    func testCommandAUsesOnlyVisibleRowsAndChoosesFocusDeterministically() {
        let hidden = file("collapsed/hidden.md")
        let oldDirectory = directory("old", id: 7)
        let visible = [file("a.md"), directory("replacement", id: 7), file("c.md")]
        var model = Model()
        model.applyPointerClick(.plain, row: hidden, visibleRows: [hidden])
        model.applyPointerClick(.toggle, row: oldDirectory, visibleRows: [hidden, oldDirectory])
        XCTAssertEqual(model.focused, .directory(7))

        model.selectAll(visibleRows: visible)

        XCTAssertEqual(model.selected, Set(visible.map(\.identity)))
        XCTAssertFalse(model.selected.contains(hidden.identity), "collapsed rows are absent from input")
        XCTAssertEqual(
            model.focused, visible[0].identity,
            "a path-invalid reused focus falls back to the first visible row")
        XCTAssertEqual(model.rangeAnchor, visible[0].identity)

        model.applyPointerClick(.plain, row: visible[2], visibleRows: visible)
        model.selectAll(visibleRows: visible)
        XCTAssertEqual(model.focused, visible[2].identity, "a valid visible focus is retained")

        model.selectAll(visibleRows: [])
        XCTAssertTrue(model.selected.isEmpty)
        XCTAssertNil(model.focused)
        XCTAssertNil(model.rangeAnchor)
        XCTAssertNil(model.rangeAnchorPathSnapshot)
    }

    func testReusedDirectoryIdentityDropsFromEveryPathValidatedSurface() {
        let keep = file("keep.md")
        let oldDirectory = directory("old", id: 5)
        let replacement = directory("new", id: 5)
        var model = Model()
        model.applyPointerClick(.plain, row: keep, visibleRows: [keep, oldDirectory])
        model.applyPointerClick(.toggle, row: oldDirectory, visibleRows: [keep, oldDirectory])
        XCTAssertEqual(model.focused, oldDirectory.identity)
        XCTAssertEqual(model.rangeAnchor, oldDirectory.identity)

        let transition = model.reconcile(visibleRows: [replacement, keep]) { identity in
            switch identity {
            case .directory(5): return "new"
            case .file("keep.md"): return "keep.md"
            default: return nil
            }
        }

        XCTAssertTrue(transition.changed)
        XCTAssertEqual(model.selected, [keep.identity])
        XCTAssertEqual(model.focused, keep.identity)
        XCTAssertNil(model.rangeAnchor)
        XCTAssertFalse(model.isSelected(replacement.identity, currentPath: replacement.path))
        XCTAssertEqual(model.selectedVisibleRows(in: [replacement, keep]), [keep])
        XCTAssertEqual(model.topLevelOperationRows(in: [replacement, keep]), [keep])
    }

    func testCommandRemoveNeverFocusesAPathInvalidRemainingIdentity() {
        let oldDirectory = directory("old", id: 5)
        let replacement = directory("new", id: 5)
        let file = file("file.md")
        var model = Model()
        model.applyPointerClick(.plain, row: oldDirectory, visibleRows: [oldDirectory, file])
        model.applyPointerClick(.toggle, row: file, visibleRows: [oldDirectory, file])

        model.applyPointerClick(.toggle, row: file, visibleRows: [replacement, file])

        XCTAssertEqual(model.selected, [oldDirectory.identity], "reconcile still owns stale removal")
        XCTAssertNil(model.focused, "toggle focus must fail closed instead of targeting replacement")
        XCTAssertTrue(model.selectedVisibleRows(in: [replacement, file]).isEmpty)
    }

    func testTemporarilyUnresolvedRowsAndAnchorSurviveReconciliation() {
        let keep = file("keep.md")
        let pending = directory("pending", id: 9)
        var model = Model()
        model.applyPointerClick(.plain, row: keep, visibleRows: [keep, pending])
        model.applyPointerClick(.toggle, row: pending, visibleRows: [keep, pending])

        let transition = model.reconcile(visibleRows: [keep]) { identity in
            identity == keep.identity ? keep.path : nil
        }

        XCTAssertFalse(transition.changed)
        XCTAssertEqual(model.selected, [keep.identity, pending.identity])
        XCTAssertEqual(model.focused, pending.identity)
        XCTAssertEqual(model.rangeAnchor, pending.identity)
        XCTAssertEqual(model.rangeAnchorPathSnapshot, pending.path)
        XCTAssertEqual(model.selectionPathSnapshots[pending.identity], pending.path)
    }

    func testCommandRemovedAnchorKeepsSnapshotAndReusedIdentityMakesRangeFailClosed() {
        let a = file("a.md")
        let oldDirectory = directory("old", id: 11)
        let replacement = directory("replacement", id: 11)
        let c = file("c.md")
        var model = Model()
        model.applyPointerClick(.plain, row: a, visibleRows: [a, oldDirectory, c])
        model.applyPointerClick(.toggle, row: oldDirectory, visibleRows: [a, oldDirectory, c])
        model.applyPointerClick(.toggle, row: oldDirectory, visibleRows: [a, oldDirectory, c])

        XCTAssertEqual(model.selected, [a.identity])
        XCTAssertEqual(model.rangeAnchor, oldDirectory.identity)
        XCTAssertEqual(
            model.rangeAnchorPathSnapshot, "old",
            "a deselected Command-remove anchor owns a snapshot outside selection")
        XCTAssertNil(model.selectionPathSnapshots[oldDirectory.identity])

        model.applyPointerClick(.range, row: c, visibleRows: [a, replacement, c])

        XCTAssertEqual(model.selected, [c.identity], "the mismatched anchor degrades to plain")
        XCTAssertEqual(model.focused, c.identity)
        XCTAssertEqual(model.rangeAnchor, c.identity)
        XCTAssertEqual(model.rangeAnchorPathSnapshot, c.path)
    }

    func testProgrammaticRevealAlwaysCollapsesAndNilClearsAllState() {
        let a = file("a.md")
        let b = file("b.md")
        var model = Model()
        model.applyPointerClick(.plain, row: b, visibleRows: [a, b])
        model.applyPointerClick(.toggle, row: a, visibleRows: [a, b])
        XCTAssertEqual(model.focused, a.identity)
        XCTAssertEqual(model.selected, [a.identity, b.identity])

        let collapse = model.reveal(a)

        XCTAssertTrue(collapse.changed)
        XCTAssertFalse(collapse.focusChanged, "same-value focus cannot be the collapse trigger")
        XCTAssertEqual(model.selected, [a.identity])
        XCTAssertEqual(model.selectionPathSnapshots, [a.identity: a.path])
        XCTAssertEqual(model.rangeAnchor, a.identity)
        XCTAssertEqual(model.rangeAnchorPathSnapshot, a.path)

        model.reveal(nil)
        XCTAssertTrue(model.selected.isEmpty)
        XCTAssertTrue(model.selectionPathSnapshots.isEmpty)
        XCTAssertNil(model.focused)
        XCTAssertNil(model.rangeAnchor)
        XCTAssertNil(model.rangeAnchorPathSnapshot)
    }

    func testKnownMultiMoveRemapsStableDirectoriesFilesFocusAndDeselectedAnchor() {
        let folder = directory("folder", id: 42)
        let other = file("other.md")
        let descendant = file("folder/anchor.md")
        let before = [folder, descendant, other]
        var model = Model()
        model.applyPointerClick(.plain, row: folder, visibleRows: before)
        model.applyPointerClick(.toggle, row: other, visibleRows: before)
        model.applyPointerClick(.toggle, row: descendant, visibleRows: before)
        model.applyPointerClick(.toggle, row: descendant, visibleRows: before)
        XCTAssertEqual(model.selected, [folder.identity, other.identity])
        XCTAssertEqual(model.focused, other.identity)
        XCTAssertEqual(model.rangeAnchor, descendant.identity)

        model.remapKnownMoves(
            [
                Model.KnownMove(oldPath: "folder", newPath: "dest/folder"),
                Model.KnownMove(oldPath: "other.md", newPath: "dest/other.md"),
            ],
            identityForRemappedPath: remappedIdentity)

        let movedFolder = directory("dest/folder", id: 42)
        let movedOther = file("dest/other.md")
        let movedAnchor = file("dest/folder/anchor.md")
        XCTAssertEqual(model.selected, [movedFolder.identity, movedOther.identity])
        XCTAssertEqual(model.selectionPathSnapshots[movedFolder.identity], movedFolder.path)
        XCTAssertEqual(model.focused, movedOther.identity)
        XCTAssertEqual(model.rangeAnchor, movedAnchor.identity)
        XCTAssertEqual(model.rangeAnchorPathSnapshot, movedAnchor.path)

        let reconcile = model.reconcile(visibleRows: [movedFolder, movedAnchor, movedOther]) {
            identity in
            switch identity {
            case .directory(42): return movedFolder.path
            case let .file(path): return path
            default: return nil
            }
        }
        XCTAssertFalse(reconcile.changed, "known remapping happens before generic reconciliation")
    }

    func testBatchMoveRemapsOnlyStandingWithLongestComponentPrefix() {
        let folder = directory("a", id: 1)
        let nested = file("a/deep/note.md")
        let boundary = file("ab/keep.md")
        let rolledBack = file("rolled/item.md")
        let exactFileDescendant = file("exact.md/child.md")
        let anchor = file("a/deep/anchor.md")
        var model = Model(
            focused: nested.identity,
            selected: [
                folder.identity, nested.identity, boundary.identity,
                rolledBack.identity, exactFileDescendant.identity,
            ],
            selectionPathSnapshots: [
                folder.identity: folder.path,
                nested.identity: nested.path,
                boundary.identity: boundary.path,
                rolledBack.identity: rolledBack.path,
                exactFileDescendant.identity: exactFileDescendant.path,
            ],
            rangeAnchor: anchor.identity,
            rangeAnchorPathSnapshot: anchor.path)
        let index = Model.KnownMoveIndex([
            Model.KnownMove(oldPath: "a", newPath: "dest/a", isDirectory: true),
            Model.KnownMove(
                oldPath: "a/deep", newPath: "special/deep", isDirectory: true),
            Model.KnownMove(
                oldPath: "exact.md", newPath: "dest/exact.md", isDirectory: false),
        ])
        var visits = 0

        model.remapKnownMoves(
            using: index,
            identityForRemappedPath: remappedIdentity,
            componentVisits: &visits)

        XCTAssertEqual(model.selectionPathSnapshots[folder.identity], "dest/a")
        XCTAssertTrue(model.selected.contains(.file("special/deep/note.md")))
        XCTAssertEqual(model.focused, .file("special/deep/note.md"))
        XCTAssertEqual(model.rangeAnchor, .file("special/deep/anchor.md"))
        XCTAssertEqual(model.rangeAnchorPathSnapshot, "special/deep/anchor.md")
        XCTAssertTrue(model.selected.contains(boundary.identity), "a never covers ab")
        XCTAssertTrue(
            model.selected.contains(rolledBack.identity),
            "a rolled-back path absent from the standing index stays unchanged")
        XCTAssertTrue(
            model.selected.contains(exactFileDescendant.identity),
            "an exact-file entry never covers a malformed descendant path")
        XCTAssertLessThan(visits, 30)
    }

    func testBatchMoveIndexWorkIsIndependentOfTenThousandChanges() {
        let changes = (0..<10_000).map {
            Model.KnownMove(
                oldPath: "source-\($0)", newPath: "dest/source-\($0)",
                isDirectory: true)
        }
        let index = Model.KnownMoveIndex(changes)
        let selected = [
            file("source-1/a.md"), file("source-5000/b.md"),
            file("source-9999/deep/c.md"),
        ]
        var model = Model(
            focused: selected[1].identity,
            selected: Set(selected.map(\.identity)),
            selectionPathSnapshots: Dictionary(
                uniqueKeysWithValues: selected.map { ($0.identity, $0.path) }),
            rangeAnchor: selected[2].identity,
            rangeAnchorPathSnapshot: selected[2].path)
        var visits = 0

        model.remapKnownMoves(
            using: index,
            identityForRemappedPath: remappedIdentity,
            componentVisits: &visits)

        XCTAssertEqual(index.entryCount, 10_000)
        XCTAssertEqual(
            model.focused, .file("dest/source-5000/b.md"))
        XCTAssertLessThanOrEqual(
            visits, 11,
            "lookups visit only selected/anchor path components, never all changes")
    }

    func testBatchTrashPreservesUntrashedFocusAndUsesNextBiasedFallbacks() {
        let before = [
            directory("folder", id: 1),
            file("folder/child.md"),
            file("before.md"),
            file("focused.md"),
            file("after.md"),
            file("folderish/keep.md"),
        ]

        do {
            var model = Model(
                focused: before[4].identity,
                selected: [before[3].identity, before[4].identity],
                selectionPathSnapshots: [
                    before[3].identity: before[3].path,
                    before[4].identity: before[4].path,
                ])
            var visits = 0
            model.removeKnownItems(
                using: Model.KnownRemovalIndex([
                    Model.KnownRemoval(path: "focused.md", isDirectory: false)
                ]),
                preferredFocusPath: "focused.md",
                visibleRows: before,
                componentVisits: &visits)

            XCTAssertEqual(
                model.focused, before[4].identity,
                "an untrashed live focus is never stolen by the captured origin")
            XCTAssertEqual(model.selected, [before[4].identity])
        }

        do {
            var model = Model(
                focused: before[3].identity,
                selected: [before[2].identity, before[3].identity, before[4].identity],
                selectionPathSnapshots: [
                    before[2].identity: before[2].path,
                    before[3].identity: before[3].path,
                    before[4].identity: before[4].path,
                ])
            var visits = 0
            model.removeKnownItems(
                using: Model.KnownRemovalIndex([
                    Model.KnownRemoval(path: "focused.md", isDirectory: false)
                ]),
                preferredFocusPath: "focused.md",
                visibleRows: before,
                componentVisits: &visits)
            XCTAssertEqual(
                model.focused, before[4].identity,
                "equal-distance selected survivors prefer the next row")
            XCTAssertEqual(model.selected, [before[2].identity, before[4].identity])
        }

        do {
            var model = Model(
                focused: before[0].identity,
                selected: [before[0].identity, before[1].identity],
                selectionPathSnapshots: [
                    before[0].identity: before[0].path,
                    before[1].identity: before[1].path,
                ])
            var visits = 0
            model.removeKnownItems(
                using: Model.KnownRemovalIndex([
                    Model.KnownRemoval(path: "folder", isDirectory: true)
                ]),
                preferredFocusPath: "folder",
                visibleRows: before,
                componentVisits: &visits)
            XCTAssertFalse(model.selected.contains(before[1].identity))
            XCTAssertEqual(model.focused, before[2].identity, "next survivor wins")
            XCTAssertEqual(model.selected, [before[2].identity])
            XCTAssertFalse(
                model.selected.contains(before[5].identity),
                "fallback does not manufacture unrelated multi-selection")
        }

        do {
            let rows = [directory("parent", id: 8), file("parent/only.md")]
            var model = Model(
                focused: rows[1].identity,
                selected: [rows[1].identity],
                selectionPathSnapshots: [rows[1].identity: rows[1].path])
            var visits = 0
            model.removeKnownItems(
                using: Model.KnownRemovalIndex([
                    Model.KnownRemoval(path: "parent/only.md", isDirectory: false)
                ]),
                preferredFocusPath: rows[1].path,
                visibleRows: rows,
                componentVisits: &visits)
            XCTAssertEqual(model.focused, rows[0].identity, "surviving parent is last fallback")
            XCTAssertEqual(model.selected, [rows[0].identity])
        }

        do {
            let rows = [file("before.md"), file("last.md")]
            var model = Model(
                focused: rows[1].identity,
                selected: [rows[1].identity],
                selectionPathSnapshots: [rows[1].identity: rows[1].path])
            var visits = 0
            model.removeKnownItems(
                using: Model.KnownRemovalIndex([
                    Model.KnownRemoval(path: "last.md", isDirectory: false)
                ]),
                preferredFocusPath: rows[1].path,
                visibleRows: rows,
                componentVisits: &visits)
            XCTAssertEqual(model.focused, rows[0].identity, "previous wins when no next row survives")
            XCTAssertEqual(model.selected, [rows[0].identity])
        }

        do {
            let rows = [directory("folder", id: 20), file("folderish/keep.md")]
            var model = Model(
                focused: rows[1].identity,
                selected: [rows[0].identity, rows[1].identity],
                selectionPathSnapshots: [
                    rows[0].identity: rows[0].path,
                    rows[1].identity: rows[1].path,
                ])
            var visits = 0
            model.removeKnownItems(
                using: Model.KnownRemovalIndex([
                    Model.KnownRemoval(path: "folder", isDirectory: true)
                ]),
                preferredFocusPath: "folder",
                visibleRows: rows,
                componentVisits: &visits)
            XCTAssertEqual(model.focused, rows[1].identity)
            XCTAssertEqual(model.selected, [rows[1].identity])
            XCTAssertEqual(
                model.selectionPathSnapshots[rows[1].identity], "folderish/keep.md",
                "folder removal never covers folderish")
        }
    }

    func testMixedVisualSelectionKeepsEveryRowWhileOperationsPruneDescendants() {
        let folder = directory("folder", id: 1)
        let child = file("folder/child.md")
        let otherFolder = directory("other", id: 2)
        let rows = [folder, child, otherFolder]
        var model = Model()
        model.applyPointerClick(.plain, row: folder, visibleRows: rows)
        model.applyPointerClick(.toggle, row: child, visibleRows: rows)
        model.applyPointerClick(.toggle, row: otherFolder, visibleRows: rows)

        XCTAssertEqual(model.selectedVisibleRows(in: rows), rows)
        XCTAssertEqual(model.topLevelOperationRows(in: rows), [folder, otherFolder])
    }
}
