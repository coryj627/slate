// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

enum BaseRendererMode: String, Equatable, Hashable {
    case table
    case list

    static func resolved(view: BaseViewSummary?, override: BaseRendererMode?) -> BaseRendererMode {
        if let override { return override }
        return view?.viewType == "list" ? .list : .table
    }
}

enum BaseResultContentState: Equatable {
    case empty
    case rowOnly
    case tabular

    init(result: BasesResultSet) {
        if result.rows.isEmpty {
            self = .empty
        } else if result.columns.isEmpty {
            self = .rowOnly
        } else {
            self = .tabular
        }
    }
}

struct BaseListOptions: Equatable, Hashable {
    enum Marker: Equatable, Hashable {
        case bullet
        case number
        case none
    }

    enum SecondaryProperties: Equatable, Hashable {
        case inline
        case indented
    }

    var marker: Marker = .bullet
    var secondaryProperties: SecondaryProperties = .inline
    var separator = ", "

    init(slateStateJson: String?) {
        guard let slateStateJson,
            let data = slateStateJson.data(using: .utf8),
            let decoded = try? JSONDecoder().decode(SlateState.self, from: data),
            let list = decoded.list
        else { return }
        marker = Marker(rawValue: list.marker ?? list.markers) ?? marker
        secondaryProperties =
            SecondaryProperties(rawValue: list.secondaryProperties ?? list.details)
            ?? secondaryProperties
        if let customSeparator = list.separator, !customSeparator.isEmpty {
            separator = customSeparator
        }
    }

    private struct SlateState: Decodable {
        let list: ListState?
    }

    private struct ListState: Decodable {
        let marker: String?
        let markers: String?
        let secondaryProperties: String?
        let details: String?
        let separator: String?
    }
}

private extension BaseListOptions.Marker {
    init?(rawValue: String?) {
        switch rawValue?.lowercased() {
        case "bullet", "bullets":
            self = .bullet
        case "number", "numbers":
            self = .number
        case "none", "no-marker", "no-markers":
            self = .none
        default:
            return nil
        }
    }
}

private extension BaseListOptions.SecondaryProperties {
    init?(rawValue: String?) {
        switch rawValue?.lowercased() {
        case "inline", "separator", "separators", "joined":
            self = .inline
        case "indented", "indent":
            self = .indented
        default:
            return nil
        }
    }
}

struct BaseListProjection: Equatable {
    let items: [BaseListItem]
    let sections: [BaseListSection]
    let summary: String
    let audioSummary: String

    init(result: BasesResultSet, options: BaseListOptions, isQuickFiltered: Bool = false) {
        items = result.rows.enumerated().map { rowIndex, row in
            BaseListItem(
                row: row,
                ordinal: rowIndex,
                columns: result.columns,
                options: options)
        }
        sections = result.groups.map {
            BaseListSection(
                label: $0.label,
                rowStart: Int($0.rowStart),
                rowCount: Int($0.rowCount),
                summary: BaseSummaryFormatter.summaryText(
                    summaries: $0.summaries, columns: result.columns))
        }
        summary = BaseSummaryFormatter.summaryText(result, isQuickFiltered: isQuickFiltered)
        audioSummary = result.audioSummary
    }
}

struct BaseListDisplayModel: Equatable {
    struct Row: Equatable, Hashable {
        enum Kind: Equatable, Hashable {
            case section
            case item
            case detail
        }

        let kind: Kind
        let section: BaseListSection?
        let item: BaseListItem?
        let detail: BaseListItem.Detail?

        static func section(_ section: BaseListSection) -> Self {
            Row(kind: .section, section: section, item: nil, detail: nil)
        }

        static func item(_ item: BaseListItem) -> Self {
            Row(kind: .item, section: nil, item: item, detail: nil)
        }

        static func detail(_ detail: BaseListItem.Detail, item: BaseListItem) -> Self {
            Row(kind: .detail, section: nil, item: item, detail: detail)
        }
    }

    let rows: [Row]

    init(projection: BaseListProjection) {
        var displayRows: [Row] = []
        let sectionsByStart = Dictionary(grouping: projection.sections, by: \.rowStart)
        for (index, item) in projection.items.enumerated() {
            for section in sectionsByStart[index] ?? [] {
                displayRows.append(.section(section))
            }
            displayRows.append(.item(item))
            if item.options.secondaryProperties == .indented {
                displayRows.append(contentsOf: item.details.map { .detail($0, item: item) })
            }
        }
        rows = displayRows
    }

    var firstItemIndex: Int? {
        rows.firstIndex { $0.kind == .item }
    }

    var lastItemIndex: Int? {
        rows.lastIndex { $0.kind == .item }
    }

    func isSelectable(at index: Int) -> Bool {
        guard rows.indices.contains(index) else { return false }
        return rows[index].kind != .section
    }

    func selectionID(at index: Int) -> String? {
        guard rows.indices.contains(index) else { return nil }
        return rows[index].item?.id
    }

    func activationItem(at index: Int) -> BaseListItem? {
        guard rows.indices.contains(index) else { return nil }
        return rows[index].item
    }

    func accessibilityLabel(at index: Int) -> String? {
        guard rows.indices.contains(index) else { return nil }
        let row = rows[index]
        switch row.kind {
        case .section:
            guard let section = row.section else { return nil }
            return sectionAccessibilityLabel(section)
        case .item:
            return row.item?.accessibilityLabel
        case .detail:
            return row.detail?.text
        }
    }

    func rowHeight(at index: Int, fallback: CGFloat) -> CGFloat {
        guard rows.indices.contains(index) else { return fallback }
        switch rows[index].kind {
        case .section, .item, .detail:
            return 28
        }
    }

    private func sectionAccessibilityLabel(_ section: BaseListSection) -> String {
        let rows = "\(CountCopy.counted(section.rowCount, "row", "rows"))"
        if let summary = section.summary, !summary.isEmpty {
            return "Group: \(section.label), \(rows). Summary: \(summary)"
        }
        return "Group: \(section.label), \(rows)"
    }
}

struct BaseListSection: Equatable, Hashable {
    let label: String
    let rowStart: Int
    let rowCount: Int
    let summary: String?
}

struct BaseListItem: Identifiable, Equatable, Hashable {
    struct Detail: Equatable, Hashable {
        let label: String
        let value: String

        var text: String { "\(label): \(value)" }
    }

    let row: BasesRow
    let ordinal: Int
    let details: [Detail]
    let options: BaseListOptions

    var id: String {
        BaseGridRow.id(for: row)
    }

    var filePath: String { row.filePath }

    var primaryText: String {
        guard let first = row.values.first?.display, !first.isEmpty else {
            return (row.filePath as NSString).lastPathComponent
        }
        return first
    }

    var inlineDetailText: String {
        details.map(\.text).joined(separator: options.separator)
    }

    var accessibilityLabel: String {
        row.audioDescription
    }

    init(row: BasesRow, ordinal: Int, columns: [BasesColumn], options: BaseListOptions) {
        self.row = row
        self.ordinal = ordinal
        self.options = options
        details = row.values.dropFirst().enumerated().map { offset, value in
            let columnIndex = offset + 1
            let label = columns.indices.contains(columnIndex)
                ? columns[columnIndex].label
                : "Column \(columnIndex + 1)"
            let display = value.display.isEmpty ? "empty" : value.display
            return Detail(label: label, value: display)
        }
    }
}

struct BaseListView: View {
    let projection: BaseListProjection
    @Binding var selection: String?
    var focusRequest: Int = 0
    var onActivate: (BaseListItem) -> Void
    var rowActions: [BaseListRowAction] = []

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            BaseOutlineList(
                projection: projection,
                selection: $selection,
                focusRequest: focusRequest,
                onActivate: onActivate,
                rowActions: rowActions)
                .frame(minHeight: 200)
                .accessibilityLabel(projection.audioSummary)
            Divider()
            Text(projection.summary)
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.vertical, Tokens.Spacing.xxs)
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityLabel("Summary: \(projection.summary)")
                .accessibilityAddTraits(.isSummaryElement)
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel(projection.audioSummary)
    }
}

struct BaseListRowAction {
    let name: String
    let action: (BaseListItem) -> Void

    init(_ name: String, action: @escaping (BaseListItem) -> Void) {
        self.name = name
        self.action = action
    }
}

private struct BaseOutlineList: NSViewRepresentable {
    let projection: BaseListProjection
    @Binding var selection: String?
    var focusRequest: Int
    var onActivate: (BaseListItem) -> Void
    var rowActions: [BaseListRowAction]

    func makeCoordinator() -> BaseListCoordinator {
        BaseListCoordinator(list: self)
    }

    func makeNSView(context: Context) -> NSScrollView {
        let outline = BaseOutlineView()
        outline.listKeyHandler = context.coordinator
        outline.style = .inset
        outline.headerView = nil
        outline.allowsMultipleSelection = false
        outline.rowSizeStyle = .default
        outline.delegate = context.coordinator
        outline.dataSource = context.coordinator
        outline.target = context.coordinator
        outline.doubleAction = #selector(BaseListCoordinator.doubleClicked(_:))
        outline.setAccessibilityLabel(projection.audioSummary)

        let column = NSTableColumn(identifier: .init("base-list"))
        column.resizingMask = .autoresizingMask
        outline.addTableColumn(column)
        outline.outlineTableColumn = column

        let scroll = NSScrollView()
        scroll.documentView = outline
        scroll.hasVerticalScroller = true
        scroll.drawsBackground = false
        context.coordinator.outline = outline
        return scroll
    }

    func updateNSView(_ scroll: NSScrollView, context: Context) {
        context.coordinator.reload(list: self)
        context.coordinator.focusIfRequested()
    }
}

private final class BaseOutlineView: NSOutlineView {
    weak var listKeyHandler: BaseListCoordinator?

    override func keyDown(with event: NSEvent) {
        if listKeyHandler?.handleKeyDown(event, in: self) == true { return }
        super.keyDown(with: event)
    }
}

@MainActor
private final class BaseListCoordinator: NSObject, NSOutlineViewDataSource, NSOutlineViewDelegate {
    private(set) var list: BaseOutlineList
    private var displayModel: BaseListDisplayModel
    private var lastFocusRequest = 0
    weak var outline: NSOutlineView?

    init(list: BaseOutlineList) {
        self.list = list
        displayModel = BaseListDisplayModel(projection: list.projection)
        super.init()
    }

    func reload(list: BaseOutlineList) {
        self.list = list
        displayModel = BaseListDisplayModel(projection: list.projection)
        outline?.setAccessibilityLabel(list.projection.audioSummary)
        outline?.reloadData()
        syncSelectionFromBinding()
    }

    func focusIfRequested() {
        guard list.focusRequest != lastFocusRequest else { return }
        lastFocusRequest = list.focusRequest
        guard list.focusRequest != 0, let outline else { return }
        outline.window?.makeFirstResponder(outline)
    }

    nonisolated func outlineView(
        _ outlineView: NSOutlineView, numberOfChildrenOfItem item: Any?
    ) -> Int {
        MainActor.assumeIsolated { item == nil ? displayModel.rows.count : 0 }
    }

    nonisolated func outlineView(
        _ outlineView: NSOutlineView, child index: Int, ofItem item: Any?
    ) -> Any {
        MainActor.assumeIsolated { displayModel.rows[index] }
    }

    nonisolated func outlineView(_ outlineView: NSOutlineView, isItemExpandable item: Any) -> Bool {
        false
    }

    nonisolated func outlineView(
        _ outlineView: NSOutlineView, shouldSelectItem item: Any
    ) -> Bool {
        MainActor.assumeIsolated {
            guard let row = item as? BaseListDisplayModel.Row,
                let index = displayModel.rows.firstIndex(of: row)
            else { return false }
            return displayModel.isSelectable(at: index)
        }
    }

    nonisolated func outlineViewSelectionDidChange(_ notification: Notification) {
        MainActor.assumeIsolated {
            guard let outline else { return }
            list.selection = selectedItem(in: outline)?.id
        }
    }

    nonisolated func outlineView(
        _ outlineView: NSOutlineView,
        viewFor tableColumn: NSTableColumn?,
        item: Any
    ) -> NSView? {
        MainActor.assumeIsolated {
            guard let row = item as? BaseListDisplayModel.Row else { return nil }
            switch row.kind {
            case .section:
                guard let section = row.section else { return nil }
                return makeSectionCell(outlineView: outlineView, section: section)
            case .item:
                guard let item = row.item else { return nil }
                return makeItemCell(outlineView: outlineView, item: item)
            case .detail:
                guard let detail = row.detail, let item = row.item else { return nil }
                return makeDetailCell(outlineView: outlineView, detail: detail, item: item)
            }
        }
    }

    nonisolated func outlineView(_ outlineView: NSOutlineView, heightOfRowByItem item: Any) -> CGFloat {
        MainActor.assumeIsolated {
            guard let row = item as? BaseListDisplayModel.Row,
                let index = displayModel.rows.firstIndex(of: row)
            else { return outlineView.rowHeight }
            return displayModel.rowHeight(at: index, fallback: outlineView.rowHeight)
        }
    }

    private func makeSectionCell(
        outlineView: NSOutlineView, section: BaseListSection
    ) -> NSTableCellView {
        let cell = reusableCell(outlineView: outlineView, identifier: "section")
        let text = sectionAccessibilityLabel(section)
        cell.textField?.stringValue = text
        cell.textField?.font = NSFont.preferredFont(forTextStyle: .headline)
        cell.textField?.setAccessibilityLabel(text)
        cell.textField?.setAccessibilityCustomActions(nil)
        configureNativeHeadingAccessibility(cell, label: text)
        cell.textField?.setAccessibilityElement(false)
        return cell
    }

    private func makeDetailCell(
        outlineView: NSOutlineView,
        detail: BaseListItem.Detail,
        item: BaseListItem
    ) -> NSTableCellView {
        let cell = reusableCell(outlineView: outlineView, identifier: "detail")
        cell.textField?.stringValue = "    \(detail.text)"
        cell.textField?.font = NSFont.preferredFont(forTextStyle: .caption1)
        cell.textField?.textColor = .secondaryLabelColor
        cell.textField?.setAccessibilityLabel(detail.text)
        installActions(on: cell, item: item)
        return cell
    }

    private func makeItemCell(outlineView: NSOutlineView, item: BaseListItem) -> NSView {
        let reuseID = NSUserInterfaceItemIdentifier("item")
        let cell =
            outlineView.makeView(withIdentifier: reuseID, owner: nil) as? BaseListItemCell
            ?? BaseListItemCell()
        cell.identifier = reuseID
        cell.configure(item: item, ordinal: item.ordinal + 1)
        installActions(on: cell, item: item)
        return cell
    }

    private func installActions(on cell: NSView, item: BaseListItem) {
        if !list.rowActions.isEmpty {
            cell.setAccessibilityCustomActions(
                list.rowActions.map { action in
                    NSAccessibilityCustomAction(name: action.name) {
                        MainActor.assumeIsolated { action.action(item) }
                        return true
                    }
                })
        }
        if list.rowActions.isEmpty {
            cell.setAccessibilityCustomActions(nil)
        }
    }

    private func reusableCell(
        outlineView: NSOutlineView, identifier: String
    ) -> NSTableCellView {
        let reuseID = NSUserInterfaceItemIdentifier(identifier)
        if let reused = outlineView.makeView(withIdentifier: reuseID, owner: nil)
            as? NSTableCellView
        {
            return reused
        }
        let cell = NSTableCellView()
        cell.identifier = reuseID
        let field = NSTextField(labelWithString: "")
        field.lineBreakMode = .byTruncatingTail
        field.translatesAutoresizingMaskIntoConstraints = false
        cell.addSubview(field)
        cell.textField = field
        NSLayoutConstraint.activate([
            field.leadingAnchor.constraint(equalTo: cell.leadingAnchor, constant: 4),
            field.trailingAnchor.constraint(equalTo: cell.trailingAnchor, constant: -4),
            field.centerYAnchor.constraint(equalTo: cell.centerYAnchor),
        ])
        return cell
    }

    func handleKeyDown(_ event: NSEvent, in outline: NSOutlineView) -> Bool {
        switch event.keyCode {
        case 115:
            if let index = displayModel.firstItemIndex {
                select(index: index, in: outline)
            }
            return true
        case 119:
            if let index = displayModel.lastItemIndex {
                select(index: index, in: outline)
            }
            return true
        case 36, 76:
            if let item = selectedItem(in: outline) {
                list.onActivate(item)
                return true
            }
            return false
        default:
            return false
        }
    }

    private func select(index: Int, in outline: NSOutlineView) {
        guard displayModel.rows.indices.contains(index) else { return }
        outline.selectRowIndexes([index], byExtendingSelection: false)
        outline.scrollRowToVisible(index)
    }

    private func syncSelectionFromBinding() {
        guard let outline else { return }
        guard let wanted = list.selection,
            let index = displayModel.rows.firstIndex(where: {
                $0.kind == .item && $0.item?.id == wanted
            })
        else {
            if outline.selectedRow != -1 { outline.deselectAll(nil) }
            return
        }
        if outline.selectedRow != index {
            outline.selectRowIndexes([index], byExtendingSelection: false)
            outline.scrollRowToVisible(index)
        }
    }

    private func selectedItem(in outline: NSOutlineView) -> BaseListItem? {
        displayModel.activationItem(at: outline.selectedRow)
    }

    private func sectionAccessibilityLabel(_ section: BaseListSection) -> String {
        let rows = "\(CountCopy.counted(section.rowCount, "row", "rows"))"
        if let summary = section.summary, !summary.isEmpty {
            return "Group: \(section.label), \(rows). Summary: \(summary)"
        }
        return "Group: \(section.label), \(rows)"
    }

    @objc func doubleClicked(_ sender: Any?) {
        guard let outline, let item = selectedItem(in: outline) else { return }
        list.onActivate(item)
    }

}

private final class BaseListItemCell: NSTableCellView {
    private let stack = NSStackView()
    private let primaryField = NSTextField(labelWithString: "")
    private let detailField = NSTextField(labelWithString: "")

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        setup()
    }

    required init?(coder: NSCoder) {
        super.init(coder: coder)
        setup()
    }

    private func setup() {
        stack.orientation = .vertical
        stack.spacing = 2
        stack.translatesAutoresizingMaskIntoConstraints = false
        primaryField.font = NSFont.preferredFont(forTextStyle: .body)
        primaryField.lineBreakMode = .byTruncatingTail
        detailField.font = NSFont.preferredFont(forTextStyle: .caption1)
        detailField.textColor = .secondaryLabelColor
        detailField.lineBreakMode = .byTruncatingTail
        stack.addArrangedSubview(primaryField)
        stack.addArrangedSubview(detailField)
        addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 4),
            stack.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -4),
            stack.centerYAnchor.constraint(equalTo: centerYAnchor),
        ])
    }

    func configure(item: BaseListItem, ordinal: Int) {
        primaryField.stringValue = primaryText(item: item, ordinal: ordinal)
        switch item.options.secondaryProperties {
        case .inline:
            detailField.isHidden = true
            detailField.stringValue = ""
        case .indented:
            detailField.isHidden = true
            detailField.stringValue = ""
        }
        setAccessibilityLabel(item.accessibilityLabel)
    }

    private func primaryText(item: BaseListItem, ordinal: Int) -> String {
        let marker: String
        switch item.options.marker {
        case .bullet:
            marker = "• "
        case .number:
            marker = "\(ordinal). "
        case .none:
            marker = ""
        }
        if item.options.secondaryProperties == .inline,
            !item.inlineDetailText.isEmpty
        {
            return "\(marker)\(item.primaryText)\(item.options.separator)\(item.inlineDetailText)"
        }
        return "\(marker)\(item.primaryText)"
    }
}
