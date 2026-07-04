// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// Accessible data grid v2 (Milestone T #519; shared with Milestone N
/// Bases and the reading view's table render).
///
/// v1 was a SwiftUI skeleton; v2 is **NSTableView-backed** — the AppKit
/// table is the only mac table with a complete, battle-tested AX story
/// (native "row N of M" positional context, header semantics,
/// `NSAccessibilitySortDirection`, full-keyboard row model, row-reuse
/// virtualization at the 2,000-row §K budget).
///
/// The v1 API is a strict subset: existing call sites (BulkRenameSheet,
/// reading-view tables) compile unchanged via defaults. v2 adds, all
/// optional:
///
/// - per-column **sort comparators** → click-to-sort with an
///   ascending/descending toggle, announced through the injectable
///   `announce` hook (#363 injects the #518 canvas funnel — the grid
///   itself stays canvas-agnostic, DoD §H);
/// - a **selection binding** (`Row.ID?`) so callers share selection
///   with other surfaces;
/// - **`onActivate`** (Return / double-click) and named **row actions**
///   (AX custom actions — Switch Control / Voice Control never depend
///   on a keyboard-only path);
/// - keyboard extras beyond the NSTableView defaults: **Home/End** and
///   **type-ahead** on the first column.
///
/// The summary line renders below the table as a separately-focusable
/// region (unchanged v1 contract).
struct AccessibleDataGrid<Row: Identifiable>: View {
    /// One column declaration. `header` is the visible label and the
    /// AX-announced name; `cell` returns the per-row text; `sort` (v2,
    /// optional) makes the column click-sortable.
    struct Column {
        let header: String
        let cell: (Row) -> String
        let sort: ((Row, Row) -> Bool)?

        init(
            _ header: String,
            cell: @escaping (Row) -> String,
            sort: ((Row, Row) -> Bool)? = nil
        ) {
            self.header = header
            self.cell = cell
            self.sort = sort
        }
    }

    /// A named per-row action (AX custom action on every cell).
    struct RowAction {
        let name: String
        let action: (Row) -> Void

        init(_ name: String, action: @escaping (Row) -> Void) {
            self.name = name
            self.action = action
        }
    }

    let columns: [Column]
    let rows: [Row]
    let summary: String
    let accessibilityLabel: String
    var selection: Binding<Row.ID?>?
    var onActivate: ((Row) -> Void)?
    var rowActions: [RowAction]
    /// Sort (and other grid-owned) announcements route here. Defaults
    /// to the app-wide announcer; #363 injects the #518 coordinator.
    var announce: (String) -> Void

    init(
        columns: [Column],
        rows: [Row],
        summary: String,
        accessibilityLabel: String = "Property rename preview, data grid",
        selection: Binding<Row.ID?>? = nil,
        onActivate: ((Row) -> Void)? = nil,
        rowActions: [RowAction] = [],
        announce: @escaping (String) -> Void = {
            postAccessibilityAnnouncement($0, priority: .medium)
        }
    ) {
        self.columns = columns
        self.rows = rows
        self.summary = summary
        self.accessibilityLabel = accessibilityLabel
        self.selection = selection
        self.onActivate = onActivate
        self.rowActions = rowActions
        self.announce = announce
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            GridTable(grid: self)
                .frame(minHeight: 200)
                .accessibilityLabel(accessibilityLabel)
            Divider()
            Text(summary)
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.vertical, Tokens.Spacing.xxs)
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityLabel("Summary: \(summary)")
                .accessibilityAddTraits(.isSummaryElement)
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel(accessibilityLabel)
    }
}

// MARK: - NSTableView backing

private struct GridTable<Row: Identifiable>: NSViewRepresentable {
    let grid: AccessibleDataGrid<Row>

    func makeCoordinator() -> GridCoordinator<Row> {
        GridCoordinator(grid: grid)
    }

    func makeNSView(context: Context) -> NSScrollView {
        let table = GridTableView()
        table.gridKeyHandler = context.coordinator
        table.style = .inset
        table.usesAlternatingRowBackgroundColors = true
        table.allowsMultipleSelection = false
        table.allowsColumnReordering = false
        table.rowSizeStyle = .default
        table.delegate = context.coordinator
        table.dataSource = context.coordinator
        table.target = context.coordinator
        table.doubleAction = #selector(GridCoordinator<Row>.doubleClicked(_:))
        table.setAccessibilityLabel(grid.accessibilityLabel)

        for (index, column) in grid.columns.enumerated() {
            let tableColumn = NSTableColumn(
                identifier: NSUserInterfaceItemIdentifier("col\(index)"))
            tableColumn.title = column.header
            tableColumn.resizingMask = .autoresizingMask
            if column.sort != nil {
                // The descriptor's key is our column index; the
                // coordinator interprets it (comparison happens through
                // the typed comparator, never KVC).
                tableColumn.sortDescriptorPrototype = NSSortDescriptor(
                    key: "\(index)", ascending: true)
            }
            table.addTableColumn(tableColumn)
        }

        let scroll = NSScrollView()
        scroll.documentView = table
        scroll.hasVerticalScroller = true
        scroll.drawsBackground = false
        context.coordinator.table = table
        context.coordinator.reload(grid: grid)
        return scroll
    }

    func updateNSView(_ scroll: NSScrollView, context: Context) {
        context.coordinator.reload(grid: grid)
    }
}

/// NSTableView subclass adding Home/End + type-ahead + Return
/// activation on top of the native arrow-key row model.
final class GridTableView: NSTableView {
    var gridKeyHandler: GridKeyHandling?

    override func keyDown(with event: NSEvent) {
        if gridKeyHandler?.handleKeyDown(event, in: self) == true {
            return
        }
        super.keyDown(with: event)
    }
}

@MainActor
protocol GridKeyHandling: AnyObject {
    func handleKeyDown(_ event: NSEvent, in table: NSTableView) -> Bool
}

@MainActor
final class GridCoordinator<Row: Identifiable>: NSObject, NSTableViewDelegate,
    NSTableViewDataSource, GridKeyHandling
{
    private(set) var grid: AccessibleDataGrid<Row>
    weak var table: NSTableView?

    /// Rows in display order (sorted when a sort is active).
    private(set) var displayRows: [Row] = []
    private(set) var activeSort: (column: Int, ascending: Bool)?

    // Type-ahead state (first-column prefix match, 1s window).
    private var typeAheadBuffer = ""
    private var typeAheadDeadline: Date = .distantPast

    init(grid: AccessibleDataGrid<Row>) {
        self.grid = grid
        super.init()
        self.displayRows = grid.rows
    }

    func reload(grid: AccessibleDataGrid<Row>) {
        self.grid = grid
        resortPreservingDescriptor()
        syncSortDescriptorsToTable()
        table?.reloadData()
        syncSelectionFromBinding()
    }

    // MARK: Sorting

    private func resortPreservingDescriptor() {
        displayRows = grid.rows
        if let sort = activeSort, let comparator = grid.columns[sort.column].sort {
            displayRows.sort {
                sort.ascending ? comparator($0, $1) : comparator($1, $0)
            }
        }
    }

    /// Keep the header's sort indicator matching `activeSort` (it
    /// drifts when a sort is applied programmatically or after a
    /// reload). Guarded so the descriptor-change delegate callback
    /// doesn't re-announce the sort.
    private var isSyncingSortDescriptors = false

    private func syncSortDescriptorsToTable() {
        guard let table else { return }
        let wanted =
            activeSort.map {
                [NSSortDescriptor(key: "\($0.column)", ascending: $0.ascending)]
            } ?? []
        let current = table.sortDescriptors
        let matches =
            current.count == wanted.count
            && zip(current, wanted).allSatisfy {
                $0.key == $1.key && $0.ascending == $1.ascending
            }
        guard !matches else { return }
        isSyncingSortDescriptors = true
        table.sortDescriptors = wanted
        isSyncingSortDescriptors = false
    }

    /// Apply a sort (the unit-testable seam). Returns the announcement
    /// text it routed through the announce hook.
    @discardableResult
    func applySort(column columnIndex: Int, ascending: Bool) -> String? {
        guard grid.columns.indices.contains(columnIndex),
            grid.columns[columnIndex].sort != nil
        else { return nil }
        activeSort = (columnIndex, ascending)
        resortPreservingDescriptor()
        syncSortDescriptorsToTable()
        table?.reloadData()
        let text =
            "Sorted by \(grid.columns[columnIndex].header), "
            + (ascending ? "ascending" : "descending")
        grid.announce(text)
        return text
    }

    nonisolated func tableView(
        _ tableView: NSTableView, sortDescriptorsDidChange oldDescriptors: [NSSortDescriptor]
    ) {
        MainActor.assumeIsolated {
            guard !isSyncingSortDescriptors,
                let descriptor = tableView.sortDescriptors.first,
                let columnIndex = descriptor.key.flatMap(Int.init)
            else { return }
            applySort(column: columnIndex, ascending: descriptor.ascending)
        }
    }

    // MARK: Data source / delegate

    nonisolated func numberOfRows(in tableView: NSTableView) -> Int {
        MainActor.assumeIsolated { displayRows.count }
    }

    nonisolated func tableView(
        _ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int
    ) -> NSView? {
        MainActor.assumeIsolated {
            guard let tableColumn,
                let columnIndex = Int(tableColumn.identifier.rawValue.dropFirst("col".count)),
                grid.columns.indices.contains(columnIndex),
                displayRows.indices.contains(row)
            else { return nil }
            let column = grid.columns[columnIndex]
            let text = column.cell(displayRows[row])

            let reuseID = tableColumn.identifier
            let cell: NSTableCellView
            if let reused = tableView.makeView(withIdentifier: reuseID, owner: nil)
                as? NSTableCellView
            {
                cell = reused
            } else {
                cell = NSTableCellView()
                cell.identifier = reuseID
                let field = NSTextField(labelWithString: "")
                field.lineBreakMode = .byTruncatingTail
                field.translatesAutoresizingMaskIntoConstraints = false
                cell.addSubview(field)
                cell.textField = field
                NSLayoutConstraint.activate([
                    field.leadingAnchor.constraint(equalTo: cell.leadingAnchor, constant: 2),
                    field.trailingAnchor.constraint(equalTo: cell.trailingAnchor, constant: -2),
                    field.centerYAnchor.constraint(equalTo: cell.centerYAnchor),
                ])
            }
            cell.textField?.stringValue = text
            // Dynamic Type: the cell inherits the user's body text size
            // (WCAG 1.4.4); the row height follows via rowSizeStyle.
            cell.textField?.font = NSFont.preferredFont(forTextStyle: .body)
            // "Header: value" — the v1 AX contract, now on the AppKit cell.
            cell.textField?.setAccessibilityLabel("\(column.header): \(text)")
            // Named row actions surface as AX custom actions on every
            // cell (Switch Control / Voice Control reachable).
            if !grid.rowActions.isEmpty {
                let rowValue = displayRows[row]
                cell.textField?.setAccessibilityCustomActions(
                    grid.rowActions.map { rowAction in
                        NSAccessibilityCustomAction(name: rowAction.name) {
                            MainActor.assumeIsolated { rowAction.action(rowValue) }
                            return true
                        }
                    })
            }
            return cell
        }
    }

    nonisolated func tableViewSelectionDidChange(_ notification: Notification) {
        MainActor.assumeIsolated {
            guard let table else { return }
            let row = table.selectedRow
            grid.selection?.wrappedValue =
                displayRows.indices.contains(row) ? displayRows[row].id : nil
        }
    }

    private func syncSelectionFromBinding() {
        guard let table else { return }
        // A cleared binding — or one naming a row that no longer
        // exists — must clear the table too, or the visible selection
        // goes stale against the model.
        guard let wanted = grid.selection?.wrappedValue,
            let index = displayRows.firstIndex(where: { $0.id == wanted })
        else {
            if table.selectedRow != -1 { table.deselectAll(nil) }
            return
        }
        if table.selectedRow != index {
            table.selectRowIndexes([index], byExtendingSelection: false)
            table.scrollRowToVisible(index)
        }
    }

    // MARK: Keyboard extras (Home/End, type-ahead, Return)

    func handleKeyDown(_ event: NSEvent, in table: NSTableView) -> Bool {
        switch event.keyCode {
        case 115:  // Home
            select(index: 0, in: table)
            return true
        case 119:  // End
            select(index: displayRows.count - 1, in: table)
            return true
        case 36, 76:  // Return / keypad Enter
            if let handler = grid.onActivate, let row = selectedRow(in: table) {
                handler(row)
                return true
            }
            return false
        default:
            guard let characters = event.charactersIgnoringModifiers,
                !characters.isEmpty,
                !event.modifierFlags.contains(.command),
                !event.modifierFlags.contains(.control),
                !event.modifierFlags.contains(.option),
                characters.rangeOfCharacter(from: .alphanumerics) != nil
            else { return false }
            typeAhead(characters, in: table)
            return true
        }
    }

    /// First-column prefix match, case-insensitive, 1-second window —
    /// the unit-testable seam.
    func typeAhead(_ characters: String, in table: NSTableView?) {
        let now = Date()
        if now > typeAheadDeadline { typeAheadBuffer = "" }
        typeAheadDeadline = now.addingTimeInterval(1.0)
        typeAheadBuffer += characters.lowercased()
        guard let firstColumn = grid.columns.first else { return }
        if let index = displayRows.firstIndex(where: {
            firstColumn.cell($0).lowercased().hasPrefix(typeAheadBuffer)
        }) {
            if let table {
                select(index: index, in: table)
            } else {
                grid.selection?.wrappedValue = displayRows[index].id
            }
        }
    }

    private func select(index: Int, in table: NSTableView) {
        guard displayRows.indices.contains(index) else { return }
        table.selectRowIndexes([index], byExtendingSelection: false)
        table.scrollRowToVisible(index)
    }

    private func selectedRow(in table: NSTableView) -> Row? {
        let index = table.selectedRow
        return displayRows.indices.contains(index) ? displayRows[index] : nil
    }

    @objc func doubleClicked(_ sender: Any?) {
        guard let table, let handler = grid.onActivate, let row = selectedRow(in: table) else {
            return
        }
        handler(row)
    }
}
