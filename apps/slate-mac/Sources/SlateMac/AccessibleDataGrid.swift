// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Skeleton accessible data grid per `docs/plans/05_locked_architecture_decisions.md` §8.7.
///
/// Renders a 2D table of rows × columns where each cell announces
/// `"<column header>: <value>"` and each column header announces
/// `"Column: <name>"`. Keyboard navigation is the SwiftUI grid
/// default (arrow keys move focus between cells).
///
/// This is the V1.x baseline — Milestone N (Bases) will share the
/// same component, so the API is generic over `Row` and accepts a
/// column declaration list to keep both callers honest. A more
/// sophisticated `NSTableView`-backed implementation can swap in
/// later without changing the call sites.
///
/// `summary` renders below the grid as a separately-focusable
/// region so screen-reader users can navigate to it after the row
/// data without first re-traversing the table.
struct AccessibleDataGrid<Row: Identifiable>: View {
    let columns: [Column]
    let rows: [Row]
    let summary: String

    /// One column declaration. `header` is the visible label and
    /// the AX-announced name; `cell` returns the per-row text.
    struct Column {
        let header: String
        let cell: (Row) -> String

        init(_ header: String, cell: @escaping (Row) -> String) {
            self.header = header
            self.cell = cell
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            headerRow
            Divider()
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 0) {
                    ForEach(rows) { row in
                        bodyRow(row)
                        Divider().opacity(0.4)
                    }
                }
            }
            .frame(minHeight: 200, maxHeight: 400)
            Divider()
            summaryRow
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Property rename preview, data grid")
    }

    private var headerRow: some View {
        HStack(spacing: 8) {
            ForEach(Array(columns.enumerated()), id: \.offset) { _, col in
                Text(col.header)
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .accessibilityLabel("Column: \(col.header)")
                    .accessibilityAddTraits(.isHeader)
            }
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 4)
    }

    private func bodyRow(_ row: Row) -> some View {
        HStack(spacing: 8) {
            ForEach(Array(columns.enumerated()), id: \.offset) { _, col in
                let text = col.cell(row)
                Text(text)
                    .font(.callout)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .lineLimit(3)
                    .accessibilityLabel("\(col.header): \(text)")
            }
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 3)
    }

    private var summaryRow: some View {
        Text(summary)
            .font(.caption)
            .foregroundStyle(.secondary)
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .frame(maxWidth: .infinity, alignment: .leading)
            .accessibilityLabel("Summary: \(summary)")
            .accessibilityAddTraits(.isSummaryElement)
    }
}
