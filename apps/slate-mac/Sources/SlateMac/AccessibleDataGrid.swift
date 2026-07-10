// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

struct DataGridSortState: Equatable, Hashable {
    let columnIndex: Int
    let ascending: Bool

    init(columnIndex: Int, ascending: Bool) {
        self.columnIndex = columnIndex
        self.ascending = ascending
    }
}

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
        let accessibilityHint: ((Row) -> String?)?

        init(
            _ header: String,
            cell: @escaping (Row) -> String,
            sort: ((Row, Row) -> Bool)? = nil,
            accessibilityHint: ((Row) -> String?)? = nil
        ) {
            self.header = header
            self.cell = cell
            self.sort = sort
            self.accessibilityHint = accessibilityHint
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

    /// A grouped section heading inserted into the table before
    /// `rowStart`. `rowCount` is announced so the heading is useful
    /// without visually scanning the following rows.
    struct Group {
        let label: String
        let rowStart: Int
        let rowCount: Int
        let summary: String?

        init(label: String, rowStart: Int, rowCount: Int, summary: String? = nil) {
            self.label = label
            self.rowStart = rowStart
            self.rowCount = rowCount
            self.summary = summary
        }
    }

    struct CellPosition {
        let rowID: Row.ID
        let columnIndex: Int
    }

    struct EditRequest {
        let rowID: Row.ID
        let columnIndex: Int
        let text: String
    }

    enum EditCommitNavigation: Equatable {
        case stay
        case next
        case previous
    }

    enum CellMove {
        case left
        case right
        case up
        case down
        case home
        case end
        case pageUp
        case pageDown
    }

    let columns: [Column]
    let rows: [Row]
    let summary: String
    let accessibilityLabel: String
    var groups: [Group]
    var selection: Binding<Row.ID?>?
    var cellSelection: Binding<CellPosition?>?
    var sortState: Binding<DataGridSortState?>?
    var cellNavigation: Bool
    var onActivate: ((Row) -> Void)?
    var onEditCell: ((Row, Int) -> Void)?
    var editRequest: Binding<EditRequest?>?
    var onCommitEdit: ((Row, Int, String, EditCommitNavigation) -> Void)?
    var onCancelEdit: (() -> Void)?
    /// Optional engine-authored row context (for example, Bases'
    /// `audioDescription`). It augments vertical moves into a new row only;
    /// native labels and within-row navigation keep "Header: value" speech.
    /// Generic grids keep their existing cell-only speech when this is nil.
    var rowAccessibilityDescription: ((Row) -> String?)?
    var rowActions: [RowAction]
    var focusRequest: Int
    /// Sort (and other grid-owned) announcements route here. Defaults
    /// to the app-wide announcer; #363 injects the #518 coordinator.
    var announce: (String) -> Void

    init(
        columns: [Column],
        rows: [Row],
        summary: String,
        accessibilityLabel: String = "Property rename preview, data grid",
        groups: [Group] = [],
        selection: Binding<Row.ID?>? = nil,
        cellSelection: Binding<CellPosition?>? = nil,
        sortState: Binding<DataGridSortState?>? = nil,
        cellNavigation: Bool = false,
        onActivate: ((Row) -> Void)? = nil,
        onEditCell: ((Row, Int) -> Void)? = nil,
        editRequest: Binding<EditRequest?>? = nil,
        onCommitEdit: ((Row, Int, String, EditCommitNavigation) -> Void)? = nil,
        onCancelEdit: (() -> Void)? = nil,
        rowAccessibilityDescription: ((Row) -> String?)? = nil,
        rowActions: [RowAction] = [],
        focusRequest: Int = 0,
        announce: @escaping (String) -> Void = {
            postAccessibilityAnnouncement($0, priority: .medium)
        }
    ) {
        self.columns = columns
        self.rows = rows
        self.summary = summary
        self.accessibilityLabel = accessibilityLabel
        self.groups = groups
        self.selection = selection
        self.cellSelection = cellSelection
        self.sortState = sortState
        self.cellNavigation = cellNavigation
        self.onActivate = onActivate
        self.onEditCell = onEditCell
        self.editRequest = editRequest
        self.onCommitEdit = onCommitEdit
        self.onCancelEdit = onCancelEdit
        self.rowAccessibilityDescription = rowAccessibilityDescription
        self.rowActions = rowActions
        self.focusRequest = focusRequest
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

extension AccessibleDataGrid.CellPosition: Equatable where Row.ID: Equatable {}

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
        context.coordinator.focusIfRequested()
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
    NSTableViewDataSource, NSTextFieldDelegate, GridKeyHandling
{
    private(set) var grid: AccessibleDataGrid<Row>
    weak var table: NSTableView?

    /// Rows in display order (sorted when a sort is active).
    private(set) var displayRows: [Row] = []
    private enum DisplayEntry {
        case group(AccessibleDataGrid<Row>.Group)
        case row(Row)
    }
    private var displayEntries: [DisplayEntry] = []
    private(set) var activeSort: DataGridSortState?
    private(set) var lastFocusRequest = 0
    private var isSyncingSelectionFromBinding = false

    // Type-ahead state (first-column prefix match, 1s window).
    private var typeAheadBuffer = ""
    private var typeAheadDeadline: Date = .distantPast

    init(grid: AccessibleDataGrid<Row>) {
        self.grid = grid
        super.init()
        self.activeSort = grid.sortState?.wrappedValue
        self.displayRows = grid.rows
        rebuildDisplayEntries()
    }

    func reload(grid: AccessibleDataGrid<Row>) {
        self.grid = grid
        if let sortState = grid.sortState {
            activeSort = sortState.wrappedValue
        }
        invalidateUnavailableSort()
        resortPreservingDescriptor()
        rebuildDisplayEntries()
        reconcileTableColumnsAndLabel()
        syncSortDescriptorsToTable()
        syncHeaderAccessibilityToTable()
        table?.reloadData()
        syncSelectionFromBinding()
        syncEditRequestToTable()
    }

    func focusIfRequested() {
        guard grid.focusRequest != lastFocusRequest else { return }
        lastFocusRequest = grid.focusRequest
        guard grid.focusRequest != 0, let table else { return }
        table.window?.makeFirstResponder(table)
    }

    // MARK: Sorting

    private func invalidateUnavailableSort() {
        guard let activeSort,
            (!grid.columns.indices.contains(activeSort.columnIndex)
                || grid.columns[activeSort.columnIndex].sort == nil)
        else { return }
        self.activeSort = nil
        if grid.sortState?.wrappedValue != nil {
            grid.sortState?.wrappedValue = nil
        }
    }

    private func reconcileTableColumnsAndLabel() {
        guard let table else { return }
        table.setAccessibilityLabel(grid.accessibilityLabel)

        while table.tableColumns.count > grid.columns.count {
            guard let trailingColumn = table.tableColumns.last else { break }
            table.removeTableColumn(trailingColumn)
        }
        while table.tableColumns.count < grid.columns.count {
            let index = table.tableColumns.count
            table.addTableColumn(
                NSTableColumn(identifier: NSUserInterfaceItemIdentifier("col\(index)")))
        }

        for (index, column) in grid.columns.enumerated() {
            let tableColumn = table.tableColumns[index]
            tableColumn.identifier = NSUserInterfaceItemIdentifier("col\(index)")
            tableColumn.title = column.header
            tableColumn.resizingMask = .autoresizingMask
            tableColumn.sortDescriptorPrototype = column.sort == nil
                ? nil
                : NSSortDescriptor(key: "\(index)", ascending: true)
        }
    }

    private func resortPreservingDescriptor() {
        displayRows = grid.rows
        guard let sort = activeSort,
            grid.columns.indices.contains(sort.columnIndex),
            let comparator = grid.columns[sort.columnIndex].sort
        else { return }

        let ordered: (Row, Row) -> Bool = {
            sort.ascending ? comparator($0, $1) : comparator($1, $0)
        }
        guard !grid.groups.isEmpty else {
            displayRows.sort {
                ordered($0, $1)
            }
            return
        }

        for group in grid.groups {
            guard group.rowCount > 1 else { continue }
            let start = min(max(group.rowStart, 0), displayRows.count)
            let end = min(start + group.rowCount, displayRows.count)
            guard start < end else { continue }
            let sortedRows = displayRows[start..<end].sorted(by: ordered)
            displayRows.replaceSubrange(start..<end, with: sortedRows)
        }
    }

    private func rebuildDisplayEntries() {
        guard !grid.groups.isEmpty else {
            displayEntries = displayRows.map { .row($0) }
            return
        }
        let sortedGroups = grid.groups.sorted { lhs, rhs in
            lhs.rowStart == rhs.rowStart ? lhs.label < rhs.label : lhs.rowStart < rhs.rowStart
        }
        var entries: [DisplayEntry] = []
        var nextGroup = 0
        for rowIndex in displayRows.indices {
            while nextGroup < sortedGroups.count,
                sortedGroups[nextGroup].rowStart == rowIndex
            {
                entries.append(.group(sortedGroups[nextGroup]))
                nextGroup += 1
            }
            entries.append(.row(displayRows[rowIndex]))
        }
        while nextGroup < sortedGroups.count {
            entries.append(.group(sortedGroups[nextGroup]))
            nextGroup += 1
        }
        displayEntries = entries
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
                [NSSortDescriptor(key: "\($0.columnIndex)", ascending: $0.ascending)]
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

    private func syncHeaderAccessibilityToTable() {
        guard let table else { return }
        for (columnIndex, tableColumn) in table.tableColumns.enumerated() {
            guard grid.columns.indices.contains(columnIndex) else { continue }
            let column = grid.columns[columnIndex]
            guard column.sort != nil else {
                tableColumn.headerCell.setAccessibilityLabel(column.header)
                continue
            }
            let direction: String
            if activeSort?.columnIndex == columnIndex {
                direction = activeSort?.ascending == true ? "asc" : "desc"
            } else {
                direction = "none"
            }
            tableColumn.headerCell.setAccessibilityLabel(
                "Column: \(column.header), sortable, current sort: \(direction)")
        }
    }

    /// Apply a sort (the unit-testable seam). Returns the announcement
    /// text it routed through the announce hook.
    @discardableResult
    func applySort(column columnIndex: Int, ascending: Bool) -> String? {
        guard grid.columns.indices.contains(columnIndex),
            grid.columns[columnIndex].sort != nil
        else { return nil }
        let sort = DataGridSortState(columnIndex: columnIndex, ascending: ascending)
        activeSort = sort
        grid.sortState?.wrappedValue = sort
        resortPreservingDescriptor()
        syncSortDescriptorsToTable()
        syncHeaderAccessibilityToTable()
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
        MainActor.assumeIsolated { displayEntries.count }
    }

    nonisolated func tableView(
        _ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int
    ) -> NSView? {
        MainActor.assumeIsolated { () -> NSView? in
            guard let tableColumn,
                let columnIndex = Int(tableColumn.identifier.rawValue.dropFirst("col".count)),
                grid.columns.indices.contains(columnIndex),
                displayEntries.indices.contains(row)
            else { return nil }
            if case .group(let group) = displayEntries[row] {
                let text = columnIndex == 0 ? accessibilityLabel(for: group) : ""
                let cell = makeCell(tableView: tableView, tableColumn: tableColumn, text: text)
                cell.textField?.font = NSFont.preferredFont(forTextStyle: .headline)
                cell.textField?.setAccessibilityLabel(text)
                cell.textField?.setAccessibilityElement(false)
                if columnIndex == 0 {
                    configureNativeHeadingAccessibility(cell, label: text)
                } else {
                    cell.setAccessibilityElement(false)
                }
                return cell
            }
            guard case .row(let rowValue) = displayEntries[row] else { return nil }
            let column = grid.columns[columnIndex]
            let text = column.cell(rowValue)
            let editRequest = grid.editRequest?.wrappedValue
            let isEditing =
                editRequest?.rowID == rowValue.id && editRequest?.columnIndex == columnIndex

            let cell = makeCell(
                tableView: tableView,
                tableColumn: tableColumn,
                text: isEditing ? editRequest?.text ?? text : text,
                editable: isEditing)
            // Dynamic Type: the cell inherits the user's body text size
            // (WCAG 1.4.4); the row height follows via rowSizeStyle.
            cell.textField?.font = NSFont.preferredFont(forTextStyle: .body)
            // "Header: value" — the v1 AX contract, now on the AppKit cell.
            cell.textField?.setAccessibilityLabel(
                cellAccessibilityLabel(for: rowValue, columnIndex: columnIndex))
            cell.textField?.setAccessibilityHelp(column.accessibilityHint?(rowValue))
            // Named row actions surface as AX custom actions on every
            // cell (Switch Control / Voice Control reachable).
            if !grid.rowActions.isEmpty {
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

    private func makeCell(
        tableView: NSTableView,
        tableColumn: NSTableColumn,
        text: String,
        editable: Bool = false
    ) -> NSTableCellView {
        let reuseID = tableColumn.identifier
        let cell: NSTableCellView
        if let reused = tableView.makeView(withIdentifier: reuseID, owner: nil)
            as? NSTableCellView
        {
            cell = reused
        } else {
            cell = NSTableCellView()
            cell.identifier = reuseID
        }
        if cell.textField?.isEditable != editable {
            cell.textField?.removeFromSuperview()
            let field = NSTextField(string: "")
            field.isEditable = editable
            field.isSelectable = editable
            field.isBordered = editable
            field.drawsBackground = editable
            field.backgroundColor = editable ? .textBackgroundColor : .clear
            field.lineBreakMode = .byTruncatingTail
            field.translatesAutoresizingMaskIntoConstraints = false
            if editable {
                field.delegate = self
                field.target = self
                field.action = #selector(editingFieldDidReturn(_:))
            }
            cell.addSubview(field)
            cell.textField = field
            NSLayoutConstraint.activate([
                field.leadingAnchor.constraint(equalTo: cell.leadingAnchor, constant: 2),
                field.trailingAnchor.constraint(equalTo: cell.trailingAnchor, constant: -2),
                field.centerYAnchor.constraint(equalTo: cell.centerYAnchor),
            ])
        }
        cell.textField?.stringValue = text
        cell.setAccessibilityElement(true)
        cell.setAccessibilityRole(.cell)
        cell.setAccessibilityLabel(nil)
        cell.textField?.setAccessibilityElement(true)
        cell.textField?.setAccessibilityCustomActions(nil)
        cell.textField?.setAccessibilityHelp(nil)
        return cell
    }

    nonisolated func tableViewSelectionDidChange(_ notification: Notification) {
        MainActor.assumeIsolated {
            guard !isSyncingSelectionFromBinding, let table else { return }
            let row = table.selectedRow
            guard let rowValue = rowValue(atDisplayIndex: row) else {
                grid.cellSelection?.wrappedValue = nil
                grid.selection?.wrappedValue = nil
                return
            }
            if !grid.columns.isEmpty {
                let columnIndex = min(
                    max(grid.cellSelection?.wrappedValue?.columnIndex ?? 0, 0),
                    grid.columns.count - 1)
                grid.cellSelection?.wrappedValue = .init(
                    rowID: rowValue.id,
                    columnIndex: columnIndex)
            }
            grid.selection?.wrappedValue = rowValue.id
        }
    }

    nonisolated func tableView(_ tableView: NSTableView, shouldSelectRow row: Int) -> Bool {
        MainActor.assumeIsolated {
            guard rowValue(atDisplayIndex: row) != nil else {
                if tableView.selectedRow != -1 {
                    mirrorTableSelectionFromBinding {
                        tableView.deselectAll(nil)
                    }
                }
                grid.cellSelection?.wrappedValue = nil
                grid.selection?.wrappedValue = nil
                return false
            }
            return true
        }
    }

    private func syncSelectionFromBinding() {
        guard let table else { return }
        // A cleared binding — or one naming a row that no longer
        // exists — must clear the table too, or the visible selection
        // goes stale against the model.
        guard let wanted = grid.selection?.wrappedValue,
            let index = displayIndex(forRowID: wanted)
        else {
            if table.selectedRow != -1 {
                mirrorTableSelectionFromBinding {
                    table.deselectAll(nil)
                }
            }
            return
        }
        if table.selectedRow != index {
            mirrorTableSelectionFromBinding {
                table.selectRowIndexes([index], byExtendingSelection: false)
                table.scrollRowToVisible(index)
            }
        }
    }

    private func mirrorTableSelectionFromBinding(_ action: () -> Void) {
        isSyncingSelectionFromBinding = true
        defer { isSyncingSelectionFromBinding = false }
        action()
    }

    private func syncEditRequestToTable() {
        guard let table,
            let editRequest = grid.editRequest?.wrappedValue,
            let displayIndex = displayIndex(forRowID: editRequest.rowID),
            grid.columns.indices.contains(editRequest.columnIndex)
        else { return }
        if table.selectedRow != displayIndex {
            table.selectRowIndexes([displayIndex], byExtendingSelection: false)
        }
        table.scrollRowToVisible(displayIndex)
        table.scrollColumnToVisible(editRequest.columnIndex)
        DispatchQueue.main.async { [weak table] in
            guard let table else { return }
            let view = table.view(
                atColumn: editRequest.columnIndex,
                row: displayIndex,
                makeIfNecessary: false) as? NSTableCellView
            guard let field = view?.textField, field.isEditable else { return }
            table.window?.makeFirstResponder(field)
            field.currentEditor()?.selectAll(nil)
        }
    }

    @discardableResult
    func commitEditing(
        text: String,
        navigation: AccessibleDataGrid<Row>.EditCommitNavigation
    ) -> Bool {
        guard let editRequest = grid.editRequest?.wrappedValue,
            let row = displayRows.first(where: { $0.id == editRequest.rowID }),
            grid.columns.indices.contains(editRequest.columnIndex)
        else { return false }
        grid.onCommitEdit?(row, editRequest.columnIndex, text, navigation)
        return true
    }

    @discardableResult
    func cancelEditing() -> Bool {
        guard grid.editRequest?.wrappedValue != nil else { return false }
        grid.editRequest?.wrappedValue = nil
        grid.onCancelEdit?()
        return true
    }

    nonisolated func control(
        _ control: NSControl,
        textView: NSTextView,
        doCommandBy commandSelector: Selector
    ) -> Bool {
        MainActor.assumeIsolated {
            switch commandSelector {
            case #selector(NSResponder.insertNewline(_:)):
                return commitEditing(text: textView.string, navigation: .stay)
            case #selector(NSResponder.insertTab(_:)):
                return commitEditing(text: textView.string, navigation: .next)
            case #selector(NSResponder.insertBacktab(_:)):
                return commitEditing(text: textView.string, navigation: .previous)
            case #selector(NSResponder.cancelOperation(_:)):
                return cancelEditing()
            default:
                return false
            }
        }
    }

    @objc private func editingFieldDidReturn(_ sender: NSTextField) {
        _ = commitEditing(text: sender.stringValue, navigation: .stay)
    }

    // MARK: Keyboard extras (Home/End, type-ahead, Return)

    func handleKeyDown(_ event: NSEvent, in table: NSTableView) -> Bool {
        if grid.cellNavigation {
            switch event.keyCode {
            case 123:
                moveCell(.left, in: table)
                return true
            case 124:
                moveCell(.right, in: table)
                return true
            case 125:
                moveCell(.down, in: table)
                return true
            case 126:
                moveCell(.up, in: table)
                return true
            case 115:
                moveCell(.home, in: table)
                return true
            case 119:
                moveCell(.end, in: table)
                return true
            case 116:
                moveCell(.pageUp, in: table)
                return true
            case 121:
                moveCell(.pageDown, in: table)
                return true
            case 36, 76, 120:  // Return, keypad Enter, F2
                if let handler = grid.onEditCell,
                    let position = grid.cellSelection?.wrappedValue,
                    let row = displayRows.first(where: { $0.id == position.rowID })
                {
                    handler(row, position.columnIndex)
                    return true
                }
            default:
                break
            }
        }
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
                let displayIndex = displayIndex(forRowID: displayRows[index].id) ?? index
                select(index: displayIndex, in: table)
            } else {
                grid.selection?.wrappedValue = displayRows[index].id
            }
        }
    }

    func moveCell(_ move: AccessibleDataGrid<Row>.CellMove, in table: NSTableView?) {
        guard !displayRows.isEmpty, !grid.columns.isEmpty else { return }
        let current = grid.cellSelection?.wrappedValue
        let currentRowIndex = current.flatMap { position in
            displayRows.firstIndex { $0.id == position.rowID }
        } ?? 0
        let currentColumn = min(max(current?.columnIndex ?? 0, 0), grid.columns.count - 1)
        let visibleDataRows = visibleDataRowCount(in: table)

        let rowIndex: Int
        let columnIndex: Int
        switch move {
        case .left:
            rowIndex = currentRowIndex
            columnIndex = max(currentColumn - 1, 0)
        case .right:
            rowIndex = currentRowIndex
            columnIndex = min(currentColumn + 1, grid.columns.count - 1)
        case .up:
            rowIndex = max(currentRowIndex - 1, 0)
            columnIndex = currentColumn
        case .down:
            rowIndex = min(currentRowIndex + 1, displayRows.count - 1)
            columnIndex = currentColumn
        case .home:
            rowIndex = currentRowIndex
            columnIndex = 0
        case .end:
            rowIndex = currentRowIndex
            columnIndex = grid.columns.count - 1
        case .pageUp:
            rowIndex = max(currentRowIndex - visibleDataRows, 0)
            columnIndex = currentColumn
        case .pageDown:
            rowIndex = min(currentRowIndex + visibleDataRows, displayRows.count - 1)
            columnIndex = currentColumn
        }

        let row = displayRows[rowIndex]
        grid.cellSelection?.wrappedValue = .init(rowID: row.id, columnIndex: columnIndex)
        grid.selection?.wrappedValue = row.id
        if let displayIndex = displayIndex(forRowID: row.id), let table {
            table.selectRowIndexes([displayIndex], byExtendingSelection: false)
            table.scrollRowToVisible(displayIndex)
        }
        let movedToDifferentRow = row.id != current?.rowID
        switch move {
        case .up, .down, .pageUp, .pageDown:
            grid.announce(
                movedToDifferentRow
                    ? rowMoveAnnouncement(for: row, columnIndex: columnIndex)
                    : cellAccessibilityLabel(for: row, columnIndex: columnIndex))
        default:
            grid.announce(cellAccessibilityLabel(for: row, columnIndex: columnIndex))
        }
    }

    private func visibleDataRowCount(in table: NSTableView?) -> Int {
        guard let table else { return 1 }
        let visibleRows = table.rows(in: table.visibleRect)
        guard visibleRows.location != NSNotFound else { return 1 }
        let lowerBound = max(visibleRows.location, 0)
        let upperBound = min(visibleRows.location + visibleRows.length, displayEntries.count)
        guard lowerBound < upperBound else { return 1 }
        let count = displayEntries[lowerBound..<upperBound].reduce(into: 0) { result, entry in
            if case .row = entry { result += 1 }
        }
        return max(count, 1)
    }

    func accessibilityLabelForDisplayRow(_ row: Int) -> String? {
        guard displayEntries.indices.contains(row) else { return nil }
        switch displayEntries[row] {
        case .group(let group):
            return accessibilityLabel(for: group)
        case .row(let rowValue):
            guard !grid.columns.isEmpty else { return nil }
            return rowMoveAnnouncement(for: rowValue, columnIndex: 0)
        }
    }

    private func select(index: Int, in table: NSTableView) {
        guard displayEntries.indices.contains(index) else { return }
        table.selectRowIndexes([index], byExtendingSelection: false)
        table.scrollRowToVisible(index)
    }

    private func selectedRow(in table: NSTableView) -> Row? {
        rowValue(atDisplayIndex: table.selectedRow)
    }

    private func rowValue(atDisplayIndex index: Int) -> Row? {
        guard displayEntries.indices.contains(index) else { return nil }
        guard case .row(let row) = displayEntries[index] else { return nil }
        return row
    }

    private func displayIndex(forRowID rowID: Row.ID) -> Int? {
        displayEntries.firstIndex { entry in
            guard case .row(let row) = entry else { return false }
            return row.id == rowID
        }
    }

    private func accessibilityLabel(for group: AccessibleDataGrid<Row>.Group) -> String {
        let rowText = "\(group.rowCount) \(group.rowCount == 1 ? "row" : "rows")"
        if let summary = group.summary, !summary.isEmpty {
            return "Group: \(group.label), \(rowText). Summary: \(summary)"
        }
        return "Group: \(group.label), \(rowText)"
    }

    private func cellAccessibilityLabel(for row: Row, columnIndex: Int) -> String {
        guard grid.columns.indices.contains(columnIndex) else { return "" }
        let column = grid.columns[columnIndex]
        return "\(column.header): \(column.cell(row))"
    }

    private func rowMoveAnnouncement(for row: Row, columnIndex: Int) -> String {
        let focusedCell = cellAccessibilityLabel(for: row, columnIndex: columnIndex)
        guard let rawDescription = grid.rowAccessibilityDescription?(row) else {
            return focusedCell
        }
        let description = rawDescription.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !description.isEmpty else { return focusedCell }
        if description.range(
            of: focusedCell,
            options: [.caseInsensitive, .diacriticInsensitive]
        ) != nil {
            return description
        }
        return description + (description.hasSuffix(".") ? " " : ". ") + focusedCell
    }

    @objc func doubleClicked(_ sender: Any?) {
        guard let table, let handler = grid.onActivate, let row = selectedRow(in: table) else {
            return
        }
        handler(row)
    }
}

@MainActor
func configureNativeHeadingAccessibility(_ view: NSView, label: String) {
    view.setAccessibilityElement(true)
    // AXHeading predates AppKit's typed `.headingRole` constant, so use the
    // underlying role string to preserve heading semantics on our older targets.
    view.setAccessibilityRole(NSAccessibility.Role(rawValue: "AXHeading"))
    view.setAccessibilityLabel(label)
}
