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
            src.contains("setAccessibilityLabel(\"\\(column.header): \\(text)\")"),
            "body cells must announce \"Header: value\"")
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
        announce: @escaping (String) -> Void = { _ in }
    ) -> AccessibleDataGrid<Row> {
        AccessibleDataGrid(
            columns: [
                .init("Name", cell: { $0.a }, sort: { $0.a < $1.a }),
                .init("Role", cell: { $0.b }),
            ],
            rows: rows,
            summary: "Table: \(rows.count) rows, 2 columns.",
            accessibilityLabel: "Table",
            selection: selection,
            announce: announce)
    }

    private static let people = [
        Row(id: 0, a: "Charlie", b: "Ops"),
        Row(id: 1, a: "Ada", b: "Engineer"),
        Row(id: 2, a: "Bea", b: "Design"),
    ]

    @MainActor
    func testSortComparatorAndAnnouncement() {
        var announced: [String] = []
        let grid = makeGrid(rows: Self.people) { announced.append($0) }
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
}
