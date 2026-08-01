// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// `AccessibleDataGrid` is a shared component (BulkRenameSheet + the reading
/// view's table render, #510). These pin its AX contract — header cells are
/// headers, cells announce "Header: value", the summary is a focusable
/// region, and the container label is caller-supplied — since the reading
/// view is its first content consumer.
final class AccessibleDataGridTests: XCTestCase {

    override func setUp() {
        super.setUp()
        // `NSMenu.performActionForItem(at:)` dispatches through
        // `NSApp.sendAction` — even for items with an explicit target,
        // like `ClosureMenuItem` — and is a SILENT no-op in a process
        // where nothing has initialized `NSApplication.shared` yet. The
        // menu-firing tests below only passed in full-suite serial order
        // because an earlier test class happened to touch NSApp (#935);
        // in isolation (`--filter`) or under `swift test --parallel`
        // (fresh worker process per class) the fire never dispatched.
        // Establish the production precondition ourselves: the real app
        // always has NSApp before any menu can be opened.
        MainActor.assumeIsolated {
            _ = NSApplication.shared
        }
    }

    private final class VisibleRowsTableView: NSTableView {
        var reportedVisibleRows = NSRange(location: 0, length: 0)

        override func rows(in rect: NSRect) -> NSRange {
            reportedVisibleRows
        }
    }

    private final class ReusingTableView: NSTableView {
        var nextReusableView: NSView?

        override func makeView(withIdentifier identifier: NSUserInterfaceItemIdentifier, owner: Any?)
            -> NSView?
        {
            defer { nextReusableView = nil }
            return nextReusableView
        }
    }

    private struct Row: Identifiable {
        let id: Int
        let a: String
        let b: String
    }

    @MainActor
    private func sampleGrid(label: String = "Property rename preview, data grid")
        -> AccessibleDataGrid<Row>
    {
        AccessibleDataGrid(
            columns: [
                .init("Name") { $0.a },
                .init("Role") { $0.b },
            ],
            rows: [Row(id: 0, a: "Ada", b: "Engineer")],
            summary: "Table: 1 row, 2 columns.",
            accessibilityLabel: label)
    }

    @MainActor
    func testGridRendersInBothAppearances() {
        PresentationReady.assertRendersInBothAppearances(sampleGrid())
        PresentationReady.assertRendersInBothAppearances(sampleGrid(label: "Table"))
    }

    private static var projectRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .deletingLastPathComponent()  // apps
            .deletingLastPathComponent()  // <repo root>
    }

    private func gridSource() throws -> String {
        let url = Self.projectRoot
            .appendingPathComponent("apps/slate-mac/Sources/SlateMac")
            .appendingPathComponent("AccessibleDataGrid.swift")
        return try String(contentsOf: url, encoding: .utf8)
    }

    /// The AX contract, source-structural. v2 (#519) is NSTableView-
    /// backed: header semantics + NSAccessibilitySortDirection come from
    /// the native NSTableColumn headers, body cells announce
    /// "Header: value" via the AppKit label, the summary carries
    /// `.isSummaryElement`, and the container label is the injected
    /// parameter (not a hardcoded string).
    func testGridAccessibilityContract() throws {
        let src = try gridSource()
        XCTAssertTrue(
            src.contains("NSTableColumn("),
            "v2 must use native NSTableColumn headers (AX header role + sort direction)")
        XCTAssertTrue(
            src.contains("cellAccessibilityLabel(for: rowValue, columnIndex: columnIndex)"),
            "native body cells must keep the pinned cell-only speech composer")
        XCTAssertTrue(
            src.contains(".accessibilityAddTraits(.isSummaryElement)"),
            "the summary must be a focusable summary element")
        XCTAssertTrue(
            src.contains(".accessibilityLabel(accessibilityLabel)"),
            "the container label must be the caller-supplied parameter")
        XCTAssertTrue(
            src.contains("setAccessibilityCustomActions"),
            "row actions must surface as AX custom actions")
    }

    // MARK: v2 behavior (#519) — coordinator seams, no window needed.

    private func makeGrid(
        rows: [Row],
        selection: Binding<Int?>? = nil,
        cellSelection: Binding<AccessibleDataGrid<Row>.CellPosition?>? = nil,
        sortState: Binding<DataGridSortState?>? = nil,
        editRequest: Binding<AccessibleDataGrid<Row>.EditRequest?>? = nil,
        onActivate: ((Row) -> Void)? = nil,
        onEditCell: ((Row, Int) -> Void)? = nil,
        onCommitEdit: ((Row, Int, String, AccessibleDataGrid<Row>.EditCommitNavigation) -> Void)? =
            nil,
        onCancelEdit: (() -> Void)? = nil,
        cellNavigation: Bool = false,
        sortsRowsLocally: Bool = true,
        groups: [AccessibleDataGrid<Row>.Group] = [],
        rowAccessibilityDescription: ((Row) -> String?)? = nil,
        announce: @escaping (String) -> Void = { _ in }
    ) -> AccessibleDataGrid<Row> {
        // Tests assert the rendered strings; the grid now posts
        // canonical events, so the helper renders through the same
        // core path production speech takes.
        AccessibleDataGrid(
            columns: [
                .init("Name", cell: { $0.a }, sort: { $0.a < $1.a }),
                .init("Role", cell: { $0.b }, accessibilityHint: { _ in "read-only: computed" }),
            ],
            rows: rows,
            summary: "Table: \(rows.count) rows, 2 columns.",
            accessibilityLabel: "Table",
            groups: groups,
            selection: selection,
            cellSelection: cellSelection,
            sortState: sortState,
            cellNavigation: cellNavigation,
            sortsRowsLocally: sortsRowsLocally,
            onActivate: onActivate,
            onEditCell: onEditCell,
            editRequest: editRequest,
            onCommitEdit: onCommitEdit,
            onCancelEdit: onCancelEdit,
            rowAccessibilityDescription: rowAccessibilityDescription,
            announce: { announce(a11yRender(event: $0).text) })
    }

    private static let people = [
        Row(id: 0, a: "Charlie", b: "Ops"),
        Row(id: 1, a: "Ada", b: "Engineer"),
        Row(id: 2, a: "Bea", b: "Design"),
    ]

    @MainActor
    func testSortComparatorAndAnnouncement() {
        var announced: [String] = []
        let grid = makeGrid(rows: Self.people, announce: { announced.append($0) })
        let coordinator = GridCoordinator(grid: grid)

        XCTAssertEqual(coordinator.applySort(column: 0, ascending: true),
            "Sorted by Name, ascending")
        XCTAssertEqual(coordinator.displayRows.map(\.a), ["Ada", "Bea", "Charlie"])

        XCTAssertEqual(coordinator.applySort(column: 0, ascending: false),
            "Sorted by Name, descending")
        XCTAssertEqual(coordinator.displayRows.map(\.a), ["Charlie", "Bea", "Ada"])

        // Unsortable column: no-op, no announcement.
        XCTAssertNil(coordinator.applySort(column: 1, ascending: true))
        XCTAssertEqual(announced.count, 2)
    }

    /// Round 6: the table CONSUMES `displayEntries`, and the sort
    /// announcement must never outrun the rendered/AX order — for a
    /// caller with NO sortState binding (CanvasTableView's shape), the
    /// entries rebuild happens inside `applySort` or nowhere.
    @MainActor
    func testSortWithoutBindingReordersTheConsumedTableOrder() throws {
        let grid = AccessibleDataGrid<Row>(
            columns: [
                .init("Name", cell: { $0.a }, sort: { $0.a < $1.a }),
                .init("Role", cell: { $0.b }),
            ],
            rows: Self.people,
            summary: "",
            accessibilityLabel: "People",
            groups: [.init(label: "All", rowStart: 0, rowCount: 3)])
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator

        XCTAssertEqual(
            coordinator.applySort(column: 0, ascending: true),
            "Sorted by Name, ascending")

        // Group heading first, then the rows in the ANNOUNCED order —
        // read through the data-source path the table itself uses.
        XCTAssertEqual(coordinator.numberOfRows(in: table), 4)
        let consumed = (1..<4).compactMap { rowIndex in
            (coordinator.tableView(
                table, viewFor: table.tableColumns[0], row: rowIndex)
                as? NSTableCellView)?.textField?.stringValue
        }
        XCTAssertEqual(consumed, ["Ada", "Bea", "Charlie"])
    }

    /// Round 7: NSTableView selection is INDEX-based while the binding
    /// holds the row ID — a sort that reorders rows must re-derive the
    /// selected index from identity, or activation and destructive row
    /// actions target a DIFFERENT row (the Canvas shape: selection
    /// binding, no sortState, Delete among the actions).
    @MainActor
    func testSortWithLiveTableKeepsSelectionOnTheSameRowIdentity() {
        var selected: Int? = 0  // Charlie — display index 0 pre-sort.
        let binding = Binding<Int?>(
            get: { selected },
            set: { selected = $0 })
        let grid = makeGrid(rows: Self.people, selection: binding)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)
        XCTAssertEqual(table.selectedRow, 0, "Charlie selected pre-sort")

        XCTAssertEqual(
            coordinator.applySort(column: 0, ascending: true),
            "Sorted by Name, ascending")

        // Ascending renders Ada, Bea, Charlie — the selected IDENTITY
        // is unchanged and the table's index must follow it.
        XCTAssertEqual(selected, 0, "the binding identity is unchanged")
        XCTAssertEqual(
            table.selectedRow, 2,
            "the table selection follows the row identity, not the old index")
    }

    /// Round 8: a grid with NO selection binding keeps NSTableView's
    /// native selection — the sort must carry that identity to its new
    /// index, not deselect it (the round-7 sync read an absent binding
    /// exactly like a nil one and cleared the table).
    @MainActor
    func testSortWithoutBindingKeepsNativeSelectionOnTheSameRow() {
        let grid = makeGrid(rows: Self.people)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)

        table.selectRowIndexes([0], byExtendingSelection: false)
        XCTAssertEqual(table.selectedRow, 0, "Charlie natively selected pre-sort")

        XCTAssertEqual(
            coordinator.applySort(column: 0, ascending: true),
            "Sorted by Name, ascending")
        XCTAssertEqual(
            table.selectedRow, 2,
            "native selection follows Charlie to the new index, never deselects")
    }

    /// Round 9: a sortState write schedules a SwiftUI update, whose
    /// reload(grid:) pass must preserve unbound native selection
    /// exactly as the immediate sort path does — the binding-only
    /// sync deselected it right after applySort had restored it.
    @MainActor
    func testLifecycleReloadKeepsNativeSelectionForSortStateBoundGrids() {
        var sortState: DataGridSortState?
        let binding = Binding<DataGridSortState?>(
            get: { sortState },
            set: { sortState = $0 })
        let grid = makeGrid(rows: Self.people, sortState: binding)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)

        table.selectRowIndexes([0], byExtendingSelection: false)  // Charlie
        XCTAssertEqual(
            coordinator.applySort(column: 0, ascending: true),
            "Sorted by Name, ascending")
        XCTAssertEqual(table.selectedRow, 2, "sort carries the native selection")

        // The SwiftUI update the sortState write scheduled.
        coordinator.reload(grid: makeGrid(rows: Self.people, sortState: binding))
        XCTAssertEqual(
            table.selectedRow, 2,
            "the lifecycle reload must not deselect the unbound grid")
    }

    /// Round 10: when the natively selected row is REMOVED by a
    /// reload, its old numeric index may still be valid — index-based
    /// selection would silently retarget whatever row now occupies it,
    /// and a destructive action would take that row. Removal must
    /// deselect.
    @MainActor
    func testUnboundReloadDeselectsWhenTheSelectedRowIsRemoved() {
        let grid = makeGrid(rows: Self.people)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)

        table.selectRowIndexes([1], byExtendingSelection: false)  // Ada
        let remaining = [Self.people[0], Self.people[2]]  // Charlie, Bea
        coordinator.reload(grid: makeGrid(rows: remaining))
        XCTAssertEqual(
            table.selectedRow, -1,
            "a removed selected row must deselect, never retarget the old index")
    }

    /// Round 11: the removal deselect runs under the sync guard, which
    /// skips the selection-changed handler's normal cellSelection
    /// cleanup — the binding must be cleared explicitly, or cell
    /// navigation stays anchored to an identity absent from the
    /// display.
    @MainActor
    func testUnboundReloadClearsCellSelectionWhenItsRowIsRemoved() {
        var position: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 1, columnIndex: 1)  // Ada's Role cell
        let binding = Binding<AccessibleDataGrid<Row>.CellPosition?>(
            get: { position },
            set: { position = $0 })
        let grid = makeGrid(rows: Self.people, cellSelection: binding)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)
        table.selectRowIndexes([1], byExtendingSelection: false)  // Ada

        let remaining = [Self.people[0], Self.people[2]]  // Charlie, Bea
        coordinator.reload(grid: makeGrid(rows: remaining, cellSelection: binding))

        XCTAssertEqual(table.selectedRow, -1, "the removed row deselects")
        XCTAssertNil(
            position,
            "the cell-selection binding must not keep referencing the removed row")
    }

    /// Round 12: an in-flight edit request carries its own row ID —
    /// removal must clear it (firing onCancelEdit so the owner resets
    /// its draft), or the identity reappearing later would re-render
    /// and focus the stale edit, allowing an unintended commit.
    @MainActor
    func testReloadClearsAnEditRequestWhoseRowWasRemoved() {
        var editRequest: AccessibleDataGrid<Row>.EditRequest? =
            .init(rowID: 1, columnIndex: 1, text: "draft")  // Ada's Role
        var cancels = 0
        func makeEditGrid(rows: [Row]) -> AccessibleDataGrid<Row> {
            AccessibleDataGrid<Row>(
                columns: [.init("Name") { $0.a }, .init("Role") { $0.b }],
                rows: rows,
                summary: "",
                accessibilityLabel: "People",
                editRequest: Binding(
                    get: { editRequest },
                    set: { editRequest = $0 }),
                onCancelEdit: { cancels += 1 })
        }
        let coordinator = GridCoordinator(grid: makeEditGrid(rows: Self.people))
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: makeEditGrid(rows: Self.people))
        XCTAssertNotNil(editRequest, "the draft survives while its row exists")

        let remaining = [Self.people[0], Self.people[2]]  // Charlie, Bea
        coordinator.reload(grid: makeEditGrid(rows: remaining))

        XCTAssertNil(
            editRequest,
            "a draft for a removed row must be cleared, never re-focusable")
        XCTAssertEqual(cancels, 1, "the owner is told its edit was canceled")
    }

    /// Round 13: a BOUND identity whose row is removed must clear the
    /// OWNER's bindings too — the sync helper only deselects the table
    /// under its guard, and a shared selection (the Canvas shape,
    /// where the same binding feeds the global Delete command) would
    /// keep a filtered-out card as a hidden destructive target.
    @MainActor
    func testBoundReloadClearsRowAndCellSelectionWhenTheirRowIsRemoved() {
        var selected: Int? = 1  // Ada
        var position: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 1, columnIndex: 1)
        let selectionBinding = Binding<Int?>(
            get: { selected }, set: { selected = $0 })
        let cellBinding = Binding<AccessibleDataGrid<Row>.CellPosition?>(
            get: { position }, set: { position = $0 })
        let grid = makeGrid(
            rows: Self.people,
            selection: selectionBinding,
            cellSelection: cellBinding)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)
        XCTAssertEqual(table.selectedRow, 1, "Ada selected via the binding")

        let remaining = [Self.people[0], Self.people[2]]  // Charlie, Bea
        coordinator.reload(grid: makeGrid(
            rows: remaining,
            selection: selectionBinding,
            cellSelection: cellBinding))

        XCTAssertEqual(table.selectedRow, -1, "the table deselects")
        XCTAssertNil(
            selected,
            "the shared row selection must not retain a hidden destructive target")
        XCTAssertNil(
            position,
            "the cell selection must not retain a hidden target")
    }

    /// Round 14: the cell selection is validated INDEPENDENTLY of row
    /// selection — a surviving native selection must not carry a cell
    /// binding anchored to a DIFFERENT, removed row past the
    /// reconciliation.
    @MainActor
    func testSurvivingNativeSelectionStillClearsARemovedCellSelection() {
        var position: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 1, columnIndex: 1)  // Ada's Role cell
        var cellWrites = 0
        let cellBinding = Binding<AccessibleDataGrid<Row>.CellPosition?>(
            get: { position },
            set: {
                cellWrites += 1
                position = $0
            })
        let grid = makeGrid(rows: Self.people, cellSelection: cellBinding)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)
        table.selectRowIndexes([0], byExtendingSelection: false)  // Charlie
        // Native selection tracking rewrote the cell binding to
        // Charlie — the divergence is an OWNER-side write (external
        // state change) pointing the cell at Ada afterward.
        position = .init(rowID: 1, columnIndex: 1)
        let writesBeforeReload = cellWrites

        let remaining = [Self.people[0], Self.people[2]]  // Charlie, Bea
        coordinator.reload(grid: makeGrid(rows: remaining, cellSelection: cellBinding))

        XCTAssertEqual(
            table.selectedRow, 0,
            "the surviving native selection stays on Charlie")
        XCTAssertNil(
            position,
            "the cell binding must not keep referencing the removed row")
        XCTAssertEqual(
            cellWrites, writesBeforeReload + 1,
            "the cell owner is written exactly once by the reconciliation")
    }

    @MainActor
    func testExternallySortedGridDoesNotReapplyLocalComparatorAfterSortBinding() {
        var sortState: DataGridSortState?
        let binding = Binding<DataGridSortState?>(
            get: { sortState },
            set: { sortState = $0 })
        let engineOrder = Self.people
        let grid = makeGrid(
            rows: engineOrder,
            sortState: binding,
            sortsRowsLocally: false)
        let coordinator = GridCoordinator(grid: grid)

        XCTAssertEqual(
            coordinator.applySort(column: 0, ascending: true),
            "Sorted by Name, ascending")
        XCTAssertEqual(sortState, DataGridSortState(columnIndex: 0, ascending: true))
        XCTAssertEqual(
            coordinator.displayRows.map(\.a),
            engineOrder.map(\.a),
            "an engine-backed grid must wait for the externally reordered rows instead of "
                + "re-sorting the stale result with a Swift display-value comparator")
    }

    /// Round 15: the owner is AUTHORITATIVE for engine-backed sorts —
    /// a rejecting setter (admission change, backend failure) must
    /// produce NO success announcement, no header flip, and an
    /// unchanged order; the owner's own error path narrates.
    @MainActor
    func testRejectedExternalSortAnnouncesNothingAndKeepsOrder() {
        var announced: [String] = []
        var rejectedWrites = 0
        let rejecting = Binding<DataGridSortState?>(
            get: { nil },
            set: { _ in rejectedWrites += 1 })  // the owner refuses
        let grid = makeGrid(
            rows: Self.people,
            sortState: rejecting,
            sortsRowsLocally: false,
            announce: { announced.append($0) })
        let coordinator = GridCoordinator(grid: grid)

        XCTAssertNil(coordinator.applySort(column: 0, ascending: true))
        XCTAssertEqual(rejectedWrites, 1, "the request reached the owner")
        XCTAssertTrue(
            announced.isEmpty,
            "a rejected sort must not announce success")
        XCTAssertEqual(
            coordinator.displayRows.map(\.a),
            Self.people.map(\.a),
            "the order is unchanged")
    }

    @MainActor
    func testSortSurvivesReload() {
        let grid = makeGrid(rows: Self.people)
        let coordinator = GridCoordinator(grid: grid)
        coordinator.applySort(column: 0, ascending: true)
        // New data arrives (SwiftUI update): sort order is preserved.
        coordinator.reload(grid: makeGrid(rows: Self.people + [Row(id: 3, a: "Abe", b: "QA")]))
        XCTAssertEqual(coordinator.displayRows.map(\.a), ["Abe", "Ada", "Bea", "Charlie"])
    }

    @MainActor
    func testSortStateBindingBridgesHeaderAndCommandSorts() {
        var sortState: DataGridSortState?
        let binding = Binding<DataGridSortState?>(
            get: { sortState }, set: { sortState = $0 })
        let grid = makeGrid(rows: Self.people, sortState: binding)
        let coordinator = GridCoordinator(grid: grid)

        coordinator.applySort(column: 0, ascending: true)
        XCTAssertEqual(sortState, DataGridSortState(columnIndex: 0, ascending: true))

        sortState = DataGridSortState(columnIndex: 0, ascending: false)
        coordinator.reload(grid: makeGrid(rows: Self.people, sortState: binding))
        XCTAssertEqual(coordinator.displayRows.map(\.a), ["Charlie", "Bea", "Ada"])
    }

    @MainActor
    func testTypeAheadSelectsByFirstColumnPrefix() {
        var selected: Int?
        let binding = Binding<Int?>(get: { selected }, set: { selected = $0 })
        let grid = makeGrid(rows: Self.people, selection: binding)
        let coordinator = GridCoordinator(grid: grid)

        coordinator.typeAhead("b", in: nil)
        XCTAssertEqual(selected, 2, "prefix 'b' → Bea")
        // Accumulating within the window narrows the match.
        coordinator.typeAhead("e", in: nil)
        XCTAssertEqual(selected, 2, "prefix 'be' still Bea")
    }

    /// Codoki #611: the header's sort indicator must track `activeSort`
    /// — programmatic sorts and reloads previously left
    /// NSTableView.sortDescriptors stale — and syncing it must not
    /// re-announce the sort through the delegate callback.
    @MainActor
    func testSortDescriptorsTrackActiveSortWithoutReAnnouncing() {
        var announced: [String] = []
        let grid = makeGrid(rows: Self.people, announce: { announced.append($0) })
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table

        coordinator.applySort(column: 0, ascending: false)
        XCTAssertEqual(table.sortDescriptors.first?.key, "0")
        XCTAssertEqual(table.sortDescriptors.first?.ascending, false)
        XCTAssertEqual(announced, ["Sorted by Name, descending"], "sync must not re-announce")

        // Reload keeps the indicator in step too.
        table.sortDescriptors = []
        coordinator.reload(grid: makeGrid(rows: Self.people))
        XCTAssertEqual(table.sortDescriptors.first?.key, "0")
        XCTAssertEqual(announced.count, 1)
    }

    /// Codoki #611: a cleared or dangling selection binding deselects
    /// the table instead of leaving a stale visible selection.
    @MainActor
    func testClearedOrDanglingSelectionDeselectsTable() {
        var selected: Int? = 1
        let binding = Binding<Int?>(get: { selected }, set: { selected = $0 })
        let grid = makeGrid(rows: Self.people, selection: binding)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table

        coordinator.reload(grid: makeGrid(rows: Self.people, selection: binding))
        XCTAssertEqual(table.selectedRow, 1, "binding drives the initial selection")

        selected = nil
        coordinator.reload(grid: makeGrid(rows: Self.people, selection: binding))
        XCTAssertEqual(table.selectedRow, -1, "cleared binding deselects")

        selected = 99  // no such row
        coordinator.reload(grid: makeGrid(rows: Self.people, selection: binding))
        XCTAssertEqual(table.selectedRow, -1, "dangling id deselects")
    }

    /// A SwiftUI update can ask the AppKit table to mirror an already-current
    /// selection binding. That sync must not write the same value back through
    /// the binding, because callers may publish selection state and trigger a
    /// layout/update feedback loop while `updateNSView` is still on the stack.
    @MainActor
    func testBindingDrivenSelectionSyncDoesNotWriteBackDuringReload() {
        var selected: Int? = 1
        var bindingWrites = 0
        let binding = Binding<Int?>(
            get: { selected },
            set: {
                bindingWrites += 1
                selected = $0
            })
        let grid = makeGrid(rows: Self.people, selection: binding)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table

        coordinator.reload(grid: makeGrid(rows: Self.people, selection: binding))

        XCTAssertEqual(table.selectedRow, 1, "binding still drives the table selection")
        XCTAssertEqual(bindingWrites, 0, "syncing from binding must not write back to it")
    }

    @MainActor
    func testVirtualizedDataSourceAtScaleBudget() {
        // §K: 2,000 rows through the data source — row count is O(1),
        // cell building is on-demand only.
        let rows = (0..<2000).map { Row(id: $0, a: "Row \($0)", b: "value") }
        let grid = makeGrid(rows: rows)
        let coordinator = GridCoordinator(grid: grid)
        XCTAssertEqual(coordinator.numberOfRows(in: NSTableView()), 2000)
        XCTAssertEqual(coordinator.displayRows.count, 2000)
    }

    /// N3-1: Bases table mode uses cell navigation, not only row navigation.
    /// The coordinator seam keeps this test window-free while pinning the
    /// arrow/Home/End matrix that `GridTableView.keyDown` routes through.
    @MainActor
    func testCellNavigationMovesByColumnAndRow() {
        var selectedCell: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 0, columnIndex: 0)
        let binding = Binding<AccessibleDataGrid<Row>.CellPosition?>(
            get: { selectedCell }, set: { selectedCell = $0 })
        let grid = makeGrid(rows: Self.people, cellSelection: binding, cellNavigation: true)
        let coordinator = GridCoordinator(grid: grid)

        coordinator.moveCell(.right, in: nil)
        XCTAssertEqual(selectedCell, .init(rowID: 0, columnIndex: 1))

        coordinator.moveCell(.down, in: nil)
        XCTAssertEqual(selectedCell, .init(rowID: 1, columnIndex: 1))

        coordinator.moveCell(.home, in: nil)
        XCTAssertEqual(selectedCell, .init(rowID: 1, columnIndex: 0))

        coordinator.moveCell(.end, in: nil)
        XCTAssertEqual(selectedCell, .init(rowID: 1, columnIndex: 1))
    }

    @MainActor
    func testCellNavigationKeepsGenericCallerAnnouncementUnchanged() {
        var selectedCell: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 0, columnIndex: 1)
        var announced: [String] = []
        let grid = makeGrid(
            rows: Self.people,
            cellSelection: Binding(get: { selectedCell }, set: { selectedCell = $0 }),
            cellNavigation: true,
            announce: { announced.append($0) })
        let coordinator = GridCoordinator(grid: grid)

        coordinator.moveCell(.down, in: nil)

        XCTAssertEqual(selectedCell, .init(rowID: 1, columnIndex: 1))
        XCTAssertEqual(announced, ["Role: Engineer"])
    }

    @MainActor
    func testVerticalNavigationUsesCompleteRowAudioWithoutRepeatingFocusedCell() {
        var selectedCell: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 0, columnIndex: 1)
        var announced: [String] = []
        let grid = makeGrid(
            rows: Self.people,
            cellSelection: Binding(get: { selectedCell }, set: { selectedCell = $0 }),
            cellNavigation: true,
            rowAccessibilityDescription: { row in
                "\(row.a). Role: \(row.b)"
            },
            announce: { announced.append($0) })
        let coordinator = GridCoordinator(grid: grid)

        coordinator.moveCell(.down, in: nil)

        XCTAssertEqual(selectedCell, .init(rowID: 1, columnIndex: 1))
        XCTAssertEqual(
            announced,
            ["Ada. Role: Engineer"],
            "engine row audio already containing the focused cell must not repeat it")
    }

    @MainActor
    func testVerticalNavigationComposesPartialRowContextWithFocusedCell() {
        var selectedCell: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 0, columnIndex: 1)
        var announced: [String] = []
        let grid = makeGrid(
            rows: Self.people,
            cellSelection: Binding(get: { selectedCell }, set: { selectedCell = $0 }),
            cellNavigation: true,
            rowAccessibilityDescription: { "\($0.a) row" },
            announce: { announced.append($0) })
        let coordinator = GridCoordinator(grid: grid)

        coordinator.moveCell(.down, in: nil)

        XCTAssertEqual(announced, ["Ada row. Role: Engineer"])
    }

    @MainActor
    func testHorizontalHomeAndEndNavigationKeepPinnedCellOnlyGrammar() {
        var selectedCell: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 1, columnIndex: 1)
        var announced: [String] = []
        let grid = makeGrid(
            rows: Self.people,
            cellSelection: Binding(get: { selectedCell }, set: { selectedCell = $0 }),
            cellNavigation: true,
            rowAccessibilityDescription: { row in
                "\(row.a). Role: \(row.b)"
            },
            announce: { announced.append($0) })
        let coordinator = GridCoordinator(grid: grid)

        coordinator.moveCell(.left, in: nil)
        coordinator.moveCell(.end, in: nil)
        coordinator.moveCell(.home, in: nil)

        XCTAssertEqual(selectedCell, .init(rowID: 1, columnIndex: 0))
        XCTAssertEqual(announced, ["Name: Ada", "Role: Engineer", "Name: Ada"])
    }

    @MainActor
    func testNativeCellLabelsStayCellOnlyWhenRowAudioIsAvailable() {
        let grid = makeGrid(
            rows: Self.people,
            rowAccessibilityDescription: { row in
                "\(row.a). Role: \(row.b)"
            })
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))

        let roleCell = coordinator.tableView(
            table,
            viewFor: table.tableColumns[1],
            row: 1) as? NSTableCellView

        XCTAssertEqual(roleCell?.textField?.accessibilityLabel(), "Role: Engineer")
    }

    @MainActor
    func testGroupedPageNavigationUsesCompleteRowAudioAndSkipsHeadingRows() {
        let rows = (0..<12).map { Row(id: $0, a: "Row \($0)", b: "value") }
        var selectedCell: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 4, columnIndex: 1)
        var announced: [String] = []
        let grid = makeGrid(
            rows: rows,
            cellSelection: Binding(get: { selectedCell }, set: { selectedCell = $0 }),
            cellNavigation: true,
            groups: [
                .init(label: "All", rowStart: 0, rowCount: rows.count)
            ],
            rowAccessibilityDescription: { "\($0.a). Role: \($0.b)" },
            announce: { announced.append($0) })
        let coordinator = GridCoordinator(grid: grid)
        let table = VisibleRowsTableView()
        table.reportedVisibleRows = NSRange(location: 0, length: 4)

        coordinator.moveCell(.pageDown, in: table)
        coordinator.moveCell(.pageUp, in: table)

        XCTAssertEqual(selectedCell, .init(rowID: 4, columnIndex: 1))
        XCTAssertEqual(announced, ["Row 7. Role: value", "Row 4. Role: value"])
        XCTAssertEqual(
            coordinator.accessibilityLabelForDisplayRow(0),
            "Group: All, 12 rows",
            "group headings must keep their own list semantics")
    }

    @MainActor
    func testNativeRowSelectionRetargetsCellBeforeReturnAndRejectsGroupRows() {
        var selectedRow: Int?
        var selectedCell: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 0, columnIndex: 1)
        var edited: [(row: Int, column: Int)] = []
        let grid = makeGrid(
            rows: Self.people,
            selection: Binding(get: { selectedRow }, set: { selectedRow = $0 }),
            cellSelection: Binding(get: { selectedCell }, set: { selectedCell = $0 }),
            onEditCell: { edited.append(($0.id, $1)) },
            cellNavigation: true)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table

        table.selectRowIndexes([1], byExtendingSelection: false)
        coordinator.tableViewSelectionDidChange(
            Notification(name: NSTableView.selectionDidChangeNotification, object: table))

        XCTAssertEqual(selectedRow, 1)
        XCTAssertEqual(selectedCell, .init(rowID: 1, columnIndex: 1))
        XCTAssertTrue(coordinator.handleKeyDown(Self.returnKeyEvent(), in: table))
        XCTAssertEqual(edited.map(\.row), [1], "Return must edit the newly selected native row")
        XCTAssertEqual(edited.map(\.column), [1], "native row changes preserve the current column")

        table.selectRowIndexes([2], byExtendingSelection: false)
        coordinator.tableViewSelectionDidChange(
            Notification(name: NSTableView.selectionDidChangeNotification, object: table))
        XCTAssertTrue(coordinator.handleKeyDown(Self.f2KeyEvent(), in: table))
        XCTAssertEqual(edited.map(\.row), [1, 2], "F2 must edit the newly selected native row")

        let grouped = makeGrid(
            rows: Self.people,
            selection: Binding(get: { selectedRow }, set: { selectedRow = $0 }),
            cellSelection: Binding(get: { selectedCell }, set: { selectedCell = $0 }),
            onEditCell: { edited.append(($0.id, $1)) },
            cellNavigation: true,
            groups: [.init(label: "Everyone", rowStart: 0, rowCount: 3)])
        coordinator.reload(grid: grouped)
        table.selectRowIndexes([0], byExtendingSelection: false)
        coordinator.tableViewSelectionDidChange(
            Notification(name: NSTableView.selectionDidChangeNotification, object: table))

        XCTAssertNil(selectedRow)
        XCTAssertNil(selectedCell)
        XCTAssertFalse(
            coordinator.handleKeyDown(Self.returnKeyEvent(), in: table),
            "group headings must not dispatch cell edits")
        XCTAssertEqual(edited.map(\.row), [1, 2])
    }

    @MainActor
    func testRejectedGroupRowClearsStaleCellBeforeReturnOrF2() {
        var selectedRow: Int? = 0
        var selectedCell: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 0, columnIndex: 1)
        var edited: [(row: Int, column: Int)] = []
        let grouped = makeGrid(
            rows: Self.people,
            selection: Binding(get: { selectedRow }, set: { selectedRow = $0 }),
            cellSelection: Binding(get: { selectedCell }, set: { selectedCell = $0 }),
            onEditCell: { edited.append(($0.id, $1)) },
            cellNavigation: true,
            groups: [.init(label: "Everyone", rowStart: 0, rowCount: 3)])
        let coordinator = GridCoordinator(grid: grouped)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grouped)

        XCTAssertEqual(table.selectedRow, 1, "the selected data row follows the group heading")
        XCTAssertFalse(coordinator.tableView(table, shouldSelectRow: 0))
        XCTAssertNil(
            selectedCell,
            "a rejected heading click must disarm the prior cell without a selection-change event")
        XCTAssertFalse(coordinator.handleKeyDown(Self.returnKeyEvent(), in: table))
        XCTAssertFalse(coordinator.handleKeyDown(Self.f2KeyEvent(), in: table))
        XCTAssertTrue(edited.isEmpty, "Return/F2 must not edit the previously selected data row")
    }

    @MainActor
    func testRejectedGroupRowDisarmsReturnEditAndActivation() {
        var selectedRow: Int? = 0
        var selectedCell: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 0, columnIndex: 1)
        var edited: [Int] = []
        var activated: [Int] = []
        let grouped = makeGrid(
            rows: Self.people,
            selection: Binding(get: { selectedRow }, set: { selectedRow = $0 }),
            cellSelection: Binding(get: { selectedCell }, set: { selectedCell = $0 }),
            onActivate: { activated.append($0.id) },
            onEditCell: { row, _ in edited.append(row.id) },
            cellNavigation: true,
            groups: [.init(label: "Everyone", rowStart: 0, rowCount: 3)])
        let coordinator = GridCoordinator(grid: grouped)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grouped)

        XCTAssertEqual(table.selectedRow, 1, "the selected data row follows the group heading")
        XCTAssertFalse(coordinator.tableView(table, shouldSelectRow: 0))
        XCTAssertNil(selectedCell)
        XCTAssertNil(selectedRow)
        XCTAssertEqual(table.selectedRow, -1)

        XCTAssertFalse(coordinator.handleKeyDown(Self.returnKeyEvent(), in: table))
        XCTAssertTrue(edited.isEmpty, "Return must not edit the prior data row")
        XCTAssertTrue(activated.isEmpty, "Return must not activate the prior data row")
    }

    @MainActor
    func testPageDownMovesOneVisibleDataViewportSkippingGroupRows() {
        let rows = (0..<12).map { Row(id: $0, a: "Row \($0)", b: "value") }
        var selectedCell: AccessibleDataGrid<Row>.CellPosition? =
            .init(rowID: 4, columnIndex: 1)
        let grid = makeGrid(
            rows: rows,
            cellSelection: Binding(get: { selectedCell }, set: { selectedCell = $0 }),
            cellNavigation: true,
            groups: [.init(label: "All", rowStart: 0, rowCount: rows.count)])
        let coordinator = GridCoordinator(grid: grid)
        let table = VisibleRowsTableView()
        table.reportedVisibleRows = NSRange(location: 0, length: 4)

        coordinator.moveCell(.pageDown, in: table)
        XCTAssertEqual(
            selectedCell,
            .init(rowID: 7, columnIndex: 1),
            "one heading plus three visible data rows means a three-row page")

        coordinator.moveCell(.pageUp, in: table)
        XCTAssertEqual(selectedCell, .init(rowID: 4, columnIndex: 1))
    }

    @MainActor
    func testSortableHeaderPublishesPinnedGrammarAndRefreshesSortDirection() {
        let grid = makeGrid(rows: Self.people)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        for (index, title) in ["Name", "Role"].enumerated() {
            let column = NSTableColumn(identifier: .init("col\(index)"))
            column.title = title
            if index == 0 {
                column.sortDescriptorPrototype = NSSortDescriptor(key: "0", ascending: true)
            }
            table.addTableColumn(column)
        }
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table

        coordinator.reload(grid: grid)
        XCTAssertEqual(
            table.tableColumns[0].headerCell.accessibilityLabel(),
            "Column: Name, sortable, current sort: none")

        coordinator.applySort(column: 0, ascending: true)
        XCTAssertEqual(
            table.tableColumns[0].headerCell.accessibilityLabel(),
            "Column: Name, sortable, current sort: asc")

        coordinator.applySort(column: 0, ascending: false)
        XCTAssertEqual(
            table.tableColumns[0].headerCell.accessibilityLabel(),
            "Column: Name, sortable, current sort: desc")
    }

    @MainActor
    func testReloadReconcilesColumnsSortStateAndNativeAudioLabel() {
        var sortState: DataGridSortState? = .init(columnIndex: 1, ascending: true)
        let sortBinding = Binding<DataGridSortState?>(
            get: { sortState }, set: { sortState = $0 })
        let sortableColumns: [AccessibleDataGrid<Row>.Column] = [
            .init("Name", cell: { $0.a }, sort: { $0.a < $1.a }),
            .init("Role", cell: { $0.b }, sort: { $0.b < $1.b }),
        ]
        let initialGrid = AccessibleDataGrid(
            columns: sortableColumns,
            rows: Self.people,
            summary: "Table: 3 rows, 2 columns.",
            accessibilityLabel: "2 notes.",
            sortState: sortBinding)
        let coordinator = GridCoordinator(grid: initialGrid)
        let table = NSTableView()
        table.setAccessibilityLabel(initialGrid.accessibilityLabel)
        for (index, column) in sortableColumns.enumerated() {
            let tableColumn = NSTableColumn(identifier: .init("col\(index)"))
            tableColumn.title = column.header
            tableColumn.sortDescriptorPrototype = NSSortDescriptor(
                key: "\(index)", ascending: true)
            table.addTableColumn(tableColumn)
        }
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: initialGrid)

        XCTAssertEqual(table.tableColumns.count, 2)
        XCTAssertEqual(
            table.tableColumns[1].headerCell.accessibilityLabel(),
            "Column: Role, sortable, current sort: asc")

        let updatedGrid = AccessibleDataGrid(
            columns: [AccessibleDataGrid<Row>.Column("Title", cell: { $0.a })],
            rows: Self.people,
            summary: "Table: 3 rows, 1 column.",
            accessibilityLabel: "1 note.",
            sortState: sortBinding)
        coordinator.reload(grid: updatedGrid)

        XCTAssertEqual(table.tableColumns.count, 1)
        XCTAssertEqual(table.tableColumns[0].identifier.rawValue, "col0")
        XCTAssertEqual(table.tableColumns[0].title, "Title")
        XCTAssertNil(table.tableColumns[0].sortDescriptorPrototype)
        XCTAssertEqual(table.tableColumns[0].headerCell.accessibilityLabel(), "Title")
        XCTAssertEqual(table.accessibilityLabel(), "1 note.")
        XCTAssertTrue(table.sortDescriptors.isEmpty)
        XCTAssertNil(coordinator.activeSort)
        XCTAssertNil(sortState, "an invalidated native sort must also clear the bound sort state")

        coordinator.reload(grid: initialGrid)
        XCTAssertEqual(table.tableColumns.count, 2, "reload must add newly restored columns")
        XCTAssertEqual(table.tableColumns.map(\.title), ["Name", "Role"])
        XCTAssertNotNil(table.tableColumns[0].sortDescriptorPrototype)
        XCTAssertNotNil(table.tableColumns[1].sortDescriptorPrototype)
        XCTAssertEqual(
            table.tableColumns[1].headerCell.accessibilityLabel(),
            "Column: Role, sortable, current sort: none")
        XCTAssertEqual(table.accessibilityLabel(), "2 notes.")
    }

    @MainActor
    func testCellEditingCommitAndCancelHooks() {
        var editRequest: AccessibleDataGrid<Row>.EditRequest? =
            .init(rowID: 0, columnIndex: 0, text: "Charlie")
        let binding = Binding<AccessibleDataGrid<Row>.EditRequest?>(
            get: { editRequest }, set: { editRequest = $0 })
        var commits: [(Int, Int, String, AccessibleDataGrid<Row>.EditCommitNavigation)] = []
        var canceled = false
        let grid = makeGrid(
            rows: Self.people,
            editRequest: binding,
            onCommitEdit: { row, columnIndex, text, navigation in
                commits.append((row.id, columnIndex, text, navigation))
            },
            onCancelEdit: { canceled = true })
        let coordinator = GridCoordinator(grid: grid)

        XCTAssertTrue(coordinator.commitEditing(text: "Edited", navigation: .next))
        XCTAssertEqual(commits.count, 1)
        XCTAssertEqual(commits.first?.0, 0)
        XCTAssertEqual(commits.first?.1, 0)
        XCTAssertEqual(commits.first?.2, "Edited")
        XCTAssertEqual(commits.first?.3, .next)

        XCTAssertTrue(coordinator.cancelEditing())
        XCTAssertNil(editRequest)
        XCTAssertTrue(canceled)
    }

    @MainActor
    func testCellsExposeOptionalAccessibilityHints() {
        let grid = makeGrid(rows: Self.people)
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        table.delegate = coordinator
        table.dataSource = coordinator

        let roleCell = coordinator.tableView(
            table,
            viewFor: table.tableColumns[1],
            row: 0) as? NSTableCellView

        XCTAssertEqual(roleCell?.textField?.accessibilityLabel(), "Role: Ops")
        XCTAssertEqual(roleCell?.textField?.accessibilityHelp(), "read-only: computed")
    }

    @MainActor
    func testReusedCellClearsCustomActionsWhenTheyBecomeUnavailable() throws {
        var busy = false
        let row = Row(id: 0, a: "Ghost", b: "Unresolved")
        let grid = AccessibleDataGrid<Row>(
            columns: [.init("Name") { $0.a }],
            rows: [row],
            summary: "",
            accessibilityLabel: "Graph",
            rowActions: [
                .init("Create note", isEnabled: { _ in !busy }) { _ in }
            ])
        let coordinator = GridCoordinator(grid: grid)
        let table = ReusingTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.delegate = coordinator
        table.dataSource = coordinator

        let idleCell = try XCTUnwrap(
            coordinator.tableView(table, viewFor: table.tableColumns[0], row: 0)
                as? NSTableCellView)
        let idleField = try XCTUnwrap(idleCell.textField)
        XCTAssertEqual(
            idleField.accessibilityCustomActions()?.map(\.name),
            ["Create note"])

        busy = true
        XCTAssertEqual(
            idleField.accessibilityCustomActions()?.map(\.name),
            ["Create note"],
            "changing the model alone cannot mutate the materialized AX element")
        table.nextReusableView = idleCell
        let busyCell = try XCTUnwrap(
            coordinator.tableView(table, viewFor: table.tableColumns[0], row: 0)
                as? NSTableCellView)

        XCTAssertTrue(busyCell === idleCell, "the regression requires a reused native cell")
        XCTAssertTrue(busyCell.textField === idleField, "the native text field must be reused too")
        XCTAssertEqual(
            busyCell.textField?.accessibilityCustomActions()?.map(\.name) ?? [],
            [],
            "busy reuse must not retain the idle Create note action")
    }

    func testCellConfigurationExplicitlyAssignsEmptyCustomActionSets() throws {
        let source = try gridSource()
        XCTAssertFalse(
            source.contains("if !rowActions.isEmpty"),
            "cell reuse must explicitly clear AX actions instead of relying on incidental AppKit resets")
    }

    @MainActor
    func testGroupedSectionsAddAddressableHeadingRows() {
        let grid = makeGrid(
            rows: Self.people,
            groups: [
                .init(label: "Team A", rowStart: 0, rowCount: 2),
                .init(label: "Team B", rowStart: 2, rowCount: 1),
            ])
        let coordinator = GridCoordinator(grid: grid)
        XCTAssertEqual(coordinator.numberOfRows(in: NSTableView()), 5)
        XCTAssertEqual(
            coordinator.accessibilityLabelForDisplayRow(0),
            "Group: Team A, 2 rows")
        XCTAssertEqual(
            coordinator.accessibilityLabelForDisplayRow(3),
            "Group: Team B, 1 row")

        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.addTableColumn(NSTableColumn(identifier: .init("col1")))
        let groupCell = coordinator.tableView(
            table,
            viewFor: table.tableColumns[0],
            row: 0)
        XCTAssertEqual(
            groupCell?.accessibilityRole(),
            NSAccessibility.Role(rawValue: "AXHeading"))
        let trailingGroupCell = coordinator.tableView(
            table,
            viewFor: table.tableColumns[1],
            row: 0)
        XCTAssertEqual(trailingGroupCell?.isAccessibilityElement(), false)
    }

    @MainActor
    func testGroupedSortPreservesSectionRanges() {
        let rows = [
            Row(id: 0, a: "Delta", b: "Team A"),
            Row(id: 1, a: "Alpha", b: "Team A"),
            Row(id: 2, a: "Charlie", b: "Team B"),
            Row(id: 3, a: "Bravo", b: "Team B"),
        ]
        let grid = makeGrid(
            rows: rows,
            groups: [
                .init(label: "Team A", rowStart: 0, rowCount: 2),
                .init(label: "Team B", rowStart: 2, rowCount: 2),
            ])
        let coordinator = GridCoordinator(grid: grid)

        coordinator.applySort(column: 0, ascending: true)

        XCTAssertEqual(coordinator.displayRows.map(\.a), [
            "Alpha", "Delta", "Bravo", "Charlie",
        ])
        XCTAssertEqual(
            coordinator.accessibilityLabelForDisplayRow(0),
            "Group: Team A, 2 rows")
        XCTAssertEqual(
            coordinator.accessibilityLabelForDisplayRow(3),
            "Group: Team B, 2 rows")
    }

    // MARK: Alternate activation + row context menu (round 2 finding 4)

    @MainActor
    func testCommandReturnTakesModifiedActivation() {
        var activated: [Int] = []
        var modified: [Int] = []
        let grid = AccessibleDataGrid<Row>(
            columns: [.init("Name") { $0.a }],
            rows: Self.people,
            summary: "",
            accessibilityLabel: "Table",
            onActivate: { activated.append($0.id) },
            onActivateModified: { modified.append($0.id) })
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)
        table.selectRowIndexes([1], byExtendingSelection: false)

        XCTAssertTrue(coordinator.handleKeyDown(Self.returnKeyEvent(), in: table))
        XCTAssertEqual(activated, [1], "plain Return activates in place")
        XCTAssertEqual(modified, [])

        XCTAssertTrue(coordinator.handleKeyDown(Self.commandReturnKeyEvent(), in: table))
        XCTAssertEqual(modified, [1], "⌘Return takes the alternate action")
        XCTAssertEqual(activated, [1], "⌘Return must not also fire plain activation")
    }

    @MainActor
    func testCommandReturnFallsBackToActivateWithoutModifiedHandler() {
        var activated: [Int] = []
        let grid = AccessibleDataGrid<Row>(
            columns: [.init("Name") { $0.a }],
            rows: Self.people,
            summary: "",
            accessibilityLabel: "Table",
            onActivate: { activated.append($0.id) })
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)
        table.selectRowIndexes([2], byExtendingSelection: false)
        // No modified handler → ⌘Return behaves like Return (unchanged
        // for grids that don't distinguish).
        XCTAssertTrue(coordinator.handleKeyDown(Self.commandReturnKeyEvent(), in: table))
        XCTAssertEqual(activated, [2])
    }

    @MainActor
    func testRowContextMenuFiltersDisabledActionsAndFiresOnSelectedRow() {
        var opened: [Int] = []
        let grid = AccessibleDataGrid<Row>(
            columns: [.init("Name") { $0.a }],
            rows: Self.people,  // [Charlie, Ada, Bea]
            summary: "",
            accessibilityLabel: "Table",
            showsRowContextMenu: true,
            rowActions: [
                .init("Open") { opened.append($0.id) },
                .init("Only Ada", isEnabled: { $0.a == "Ada" }) { _ in },
            ])
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)

        // Charlie (row 0) sees only the unconditional action.
        XCTAssertEqual(coordinator.rowMenu(at: 0, in: table)?.items.map(\.title), ["Open"])
        // Ada (row 1) sees both.
        XCTAssertEqual(
            coordinator.rowMenu(at: 1, in: table)?.items.map(\.title),
            ["Open", "Only Ada"])

        // Firing an item runs the action against the SELECTED row.
        table.selectRowIndexes([1], byExtendingSelection: false)
        let adaMenu = try! XCTUnwrap(coordinator.rowMenu(at: 1, in: table))
        adaMenu.performActionForItem(at: 0)
        XCTAssertEqual(opened, [1], "the menu action targets Ada, the selected row")
    }

    @MainActor
    func testRowContextMenuRetainsDisabledRelevantActionWithExactReason() throws {
        let reason = AppState.structuralMutationBusyReason
        var invocations = 0
        let grid = AccessibleDataGrid<Row>(
            columns: [.init("Name") { $0.a }],
            rows: Self.people,
            summary: "",
            accessibilityLabel: "Table",
            showsRowContextMenu: true,
            rowActions: [
                .init(
                    "Create note",
                    isVisible: { _ in true },
                    isEnabled: { _ in false },
                    disabledReason: { _ in reason }
                ) { _ in
                    invocations += 1
                }
            ])
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)

        let item = try XCTUnwrap(
            coordinator.rowMenu(at: 0, in: table)?.items.first)
        XCTAssertEqual(item.title, "Create note")
        XCTAssertFalse(item.isEnabled)
        XCTAssertEqual(item.toolTip, reason)
        XCTAssertEqual(item.accessibilityHelp(), reason)

        item.menu?.performActionForItem(at: 0)
        XCTAssertEqual(invocations, 0)
    }

    @MainActor
    func testRowContextMenuOptOutReturnsNil() {
        // Default (showsRowContextMenu == false): no contextual menu even
        // with row actions present (Bases/Canvas keep AX-only actions).
        let grid = AccessibleDataGrid<Row>(
            columns: [.init("Name") { $0.a }],
            rows: Self.people,
            summary: "",
            accessibilityLabel: "Table",
            rowActions: [.init("Open") { _ in }])
        let coordinator = GridCoordinator(grid: grid)
        let table = NSTableView()
        table.addTableColumn(NSTableColumn(identifier: .init("col0")))
        table.delegate = coordinator
        table.dataSource = coordinator
        coordinator.table = table
        coordinator.reload(grid: grid)
        XCTAssertNil(coordinator.rowMenu(at: 0, in: table))
    }

    private static func returnKeyEvent() -> NSEvent {
        NSEvent.keyEvent(
            with: .keyDown,
            location: .zero,
            modifierFlags: [],
            timestamp: 0,
            windowNumber: 0,
            context: nil,
            characters: "\r",
            charactersIgnoringModifiers: "\r",
            isARepeat: false,
            keyCode: 36)!
    }

    private static func f2KeyEvent() -> NSEvent {
        NSEvent.keyEvent(
            with: .keyDown,
            location: .zero,
            modifierFlags: [.function],
            timestamp: 0,
            windowNumber: 0,
            context: nil,
            characters: "",
            charactersIgnoringModifiers: "",
            isARepeat: false,
            keyCode: 120)!
    }

    private static func commandReturnKeyEvent() -> NSEvent {
        NSEvent.keyEvent(
            with: .keyDown,
            location: .zero,
            modifierFlags: [.command],
            timestamp: 0,
            windowNumber: 0,
            context: nil,
            characters: "\r",
            charactersIgnoringModifiers: "\r",
            isARepeat: false,
            keyCode: 36)!
    }
}
