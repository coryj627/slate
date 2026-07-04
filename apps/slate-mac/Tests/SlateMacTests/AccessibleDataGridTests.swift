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

    /// The AX contract, source-structural (traits are not readable from a
    /// rendered SwiftUI tree): header cells carry `.isHeader`, body cells
    /// announce "Header: value", the summary carries `.isSummaryElement`, and
    /// the container label is the injected parameter (not a hardcoded string).
    func testGridAccessibilityContract() throws {
        let src = try gridSource()
        XCTAssertTrue(
            src.contains(".accessibilityAddTraits(.isHeader)"),
            "header cells must carry the header trait for the VO rotor")
        XCTAssertTrue(
            src.contains(".accessibilityLabel(\"\\(col.header): \\(text)\")"),
            "body cells must announce \"Header: value\"")
        XCTAssertTrue(
            src.contains(".accessibilityAddTraits(.isSummaryElement)"),
            "the summary must be a focusable summary element")
        XCTAssertTrue(
            src.contains(".accessibilityLabel(accessibilityLabel)"),
            "the container label must be the caller-supplied parameter")
    }
}
