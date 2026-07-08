// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

enum BaseDocumentSource: Hashable {
    case file(path: String)
    case savedQuery(id: String, name: String)

    init?(item: EditorItem) {
        switch item {
        case .base(let path):
            self = .file(path: path)
        case .savedQuery(let id, let name):
            self = .savedQuery(id: id, name: name)
        default:
            return nil
        }
    }

    var key: String {
        switch self {
        case .file(let path):
            return path
        case .savedQuery(let id, _):
            return "saved-query:\(id)"
        }
    }

    var displayName: String {
        switch self {
        case .file(let path):
            let name = (path as NSString).lastPathComponent
            return (name as NSString).deletingPathExtension
        case .savedQuery(_, let name):
            return name
        }
    }

    var selectionKey: String {
        switch self {
        case .file(let path):
            return path
        case .savedQuery(let id, _):
            return "saved-query:\(id)"
        }
    }

    var filePath: String? {
        if case .file(let path) = self { return path }
        return nil
    }
}

/// Per-open-base state (Milestone N, #702): loads the `.base` file over
/// the FFI, owns the handle, and stores the active view result. One
/// `BaseDocument` is shared by every tab/pane showing the same path.
@MainActor
final class BaseDocument: ObservableObject {
    @Published private(set) var source: BaseDocumentSource

    enum LoadState: Equatable {
        case loading
        case ready
        case degraded(String)
        case failed(String)
    }

    @Published private(set) var state: LoadState = .loading
    @Published private(set) var views: [BaseViewSummary] = []
    @Published private(set) var result: BasesResultSet?
    @Published private(set) var activeViewIndex: Int = 0
    @Published var quickFilterText = ""
    @Published var sortState: DataGridSortState?
    @Published private(set) var focusedColumnIndex: Int = 0

    private(set) var handle: UInt64?

    init(path: String) {
        self.source = .file(path: path)
    }

    init(source: BaseDocumentSource) {
        self.source = source
    }

    var activeViewName: String? {
        guard views.indices.contains(activeViewIndex) else { return nil }
        return views[activeViewIndex].name
    }

    var displayName: String {
        source.displayName
    }

    var selectionKey: String {
        source.selectionKey
    }

    var path: String {
        source.filePath ?? source.selectionKey
    }

    var quickFilterActive: Bool {
        !quickFilterText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    func load(session: VaultSession) {
        if let stale = handle {
            session.closeBase(handle: stale)
            handle = nil
        }
        state = .loading
        clearQuickFilterState()
        do {
            let opened: UInt64
            switch source {
            case .file(let path):
                opened = try session.openBase(path: path)
            case .savedQuery(let id, _):
                opened = try session.openSavedQuery(id: id)
            }
            handle = opened
            views = try session.baseViews(handle: opened)
            activeViewIndex = views.isEmpty ? 0 : min(activeViewIndex, views.count - 1)
            sortState = nil
            focusedColumnIndex = 0
            executeActiveView(session: session)
        } catch {
            handle = nil
            views = []
            result = nil
            state = .failed(friendlyMessage(for: error))
        }
    }

    func refresh(session: VaultSession) {
        load(session: session)
    }

    func selectView(index: Int, session: VaultSession) {
        guard views.indices.contains(index) else { return }
        guard activeViewIndex != index else { return }
        activeViewIndex = index
        sortState = nil
        clearQuickFilterState()
        focusedColumnIndex = 0
        executeActiveView(session: session)
    }

    func selectNextView(session: VaultSession) {
        guard !views.isEmpty else { return }
        selectView(index: min(activeViewIndex + 1, views.count - 1), session: session)
    }

    func selectPreviousView(session: VaultSession) {
        guard !views.isEmpty else { return }
        selectView(index: max(activeViewIndex - 1, 0), session: session)
    }

    func executeActiveView(session: VaultSession) {
        guard let handle, views.indices.contains(activeViewIndex) else {
            result = nil
            state = .degraded("No executable base views were found.")
            return
        }
        do {
            let executed = try session.baseExecute(
                handle: handle,
                view: UInt32(activeViewIndex),
                thisPath: nil,
                quickFilter: quickFilterArgument,
                cancel: CancelToken())
            result = executed
            let view = views[activeViewIndex]
            if view.status == .fallback {
                state = .degraded("Using fallback view for \(view.name).")
            } else if view.status == .error {
                state = .degraded("View \(view.name) has errors.")
            } else if let message = executed.viewError, !message.isEmpty {
                state = .degraded(message)
            } else {
                state = .ready
            }
        } catch {
            result = nil
            state = .failed(friendlyMessage(for: error))
        }
    }

    func export(
        format: ExportFormat,
        session: VaultSession,
        includeQuickFilter: Bool = true
    ) throws -> String {
        guard let handle, views.indices.contains(activeViewIndex) else {
            throw BaseDocumentError.noExecutableView
        }
        return try session.baseExport(
            handle: handle,
            view: UInt32(activeViewIndex),
            format: format,
            quickFilter: includeQuickFilter ? quickFilterArgument : nil)
    }

    @discardableResult
    func applyQuickFilter(_ text: String, session: VaultSession) -> String {
        if quickFilterText != text {
            quickFilterText = text
        }
        executeActiveView(session: session)
        return quickFilterResultAnnouncement
    }

    @discardableResult
    func clearQuickFilter(session: VaultSession?) -> String? {
        guard quickFilterActive || !quickFilterText.isEmpty else { return nil }
        clearQuickFilterState()
        if let session {
            executeActiveView(session: session)
            return quickFilterResultAnnouncement
        }
        return nil
    }

    func focusColumn(_ columnIndex: Int) {
        guard columnIndex >= 0 else { return }
        focusedColumnIndex = columnIndex
    }

    @discardableResult
    func sortFocusedColumn() -> String? {
        guard let result, result.columns.indices.contains(focusedColumnIndex) else {
            return nil
        }
        let ascending: Bool
        if sortState?.columnIndex == focusedColumnIndex {
            ascending = !(sortState?.ascending ?? false)
        } else {
            ascending = true
        }
        sortState = DataGridSortState(columnIndex: focusedColumnIndex, ascending: ascending)
        let direction = ascending ? "ascending" : "descending"
        return "Sorted by \(result.columns[focusedColumnIndex].label), \(direction)"
    }

    @discardableResult
    func saveSortToView(session: VaultSession) throws -> String? {
        guard let handle,
            let result,
            let sortState,
            result.columns.indices.contains(sortState.columnIndex)
        else { return nil }
        let column = result.columns[sortState.columnIndex]
        try session.baseApplyEdit(
            handle: handle,
            edit: .setSlateState(
                view: UInt32(activeViewIndex),
                yaml: slateSortStateYAML(columnID: column.id, ascending: sortState.ascending)))
        views = try session.baseViews(handle: handle)
        executeActiveView(session: session)
        let direction = sortState.ascending ? "ascending" : "descending"
        return "Saved sort by \(column.label), \(direction)."
    }

    func close(session: VaultSession) {
        if let handle {
            session.closeBase(handle: handle)
        }
        handle = nil
    }

    func retarget(to newPath: String, session: VaultSession?) {
        if let session {
            close(session: session)
        } else {
            handle = nil
        }
        source = .file(path: newPath)
        if let session {
            load(session: session)
        }
    }

    func retarget(to newSource: BaseDocumentSource, session: VaultSession?) {
        if let session {
            close(session: session)
        } else {
            handle = nil
        }
        source = newSource
        if let session {
            load(session: session)
        }
    }

    func retargetSavedQueryName(_ name: String) {
        guard case .savedQuery(let id, _) = source else { return }
        source = .savedQuery(id: id, name: name)
    }

    private var quickFilterArgument: String? {
        quickFilterActive ? quickFilterText : nil
    }

    var quickFilterResultAnnouncement: String {
        guard let result else { return "0 of 0 results" }
        return "\(result.shownCount) of \(result.totalCount) results"
    }

    var whereAmIReadback: String {
        var parts = ["Base: \(displayName)"]
        if let activeViewName {
            parts.append("view: \(activeViewName)")
        }
        if quickFilterActive {
            parts.append("quick filter: \(quickFilterText)")
        }
        return parts.joined(separator: ", ")
    }

    private func clearQuickFilterState() {
        if !quickFilterText.isEmpty {
            quickFilterText = ""
        }
    }

    private enum BaseDocumentError: LocalizedError {
        case noExecutableView

        var errorDescription: String? {
            switch self {
            case .noExecutableView:
                return "No executable base view."
            }
        }
    }

    private func slateSortStateYAML(columnID: String, ascending: Bool) -> String {
        var lines = ["slate:"]
        if let view = views.indices.contains(activeViewIndex) ? views[activeViewIndex] : nil {
            appendExistingSlateState(
                view.slateStateJson,
                excluding: ["sort"],
                to: &lines,
                indent: 2)
        }
        lines.append("  sort:")
        lines.append("    - property: \(quoteYAMLString(columnID))")
        lines.append("      direction: \(ascending ? "ASC" : "DESC")")
        return lines.joined(separator: "\n")
    }

    private func appendExistingSlateState(
        _ slateStateJson: String?,
        excluding excludedKeys: Set<String>,
        to lines: inout [String],
        indent: Int
    ) {
        guard let slateStateJson,
            let data = slateStateJson.data(using: .utf8),
            let object = try? JSONSerialization.jsonObject(with: data),
            let dictionary = object as? [String: Any]
        else { return }
        for key in dictionary.keys.sorted() where !excludedKeys.contains(key) {
            appendYAML(key: key, value: dictionary[key] as Any, to: &lines, indent: indent)
        }
    }

    private func appendYAML(key: String, value: Any, to lines: inout [String], indent: Int) {
        let spaces = String(repeating: " ", count: indent)
        let keyText = yamlKey(key)
        if let dictionary = value as? [String: Any] {
            lines.append("\(spaces)\(keyText):")
            for childKey in dictionary.keys.sorted() {
                appendYAML(
                    key: childKey,
                    value: dictionary[childKey] as Any,
                    to: &lines,
                    indent: indent + 2)
            }
        } else if let array = value as? [Any] {
            lines.append("\(spaces)\(keyText):")
            for item in array {
                appendYAMLSequenceItem(item, to: &lines, indent: indent + 2)
            }
        } else {
            lines.append("\(spaces)\(keyText): \(yamlScalar(value))")
        }
    }

    private func appendYAMLSequenceItem(_ value: Any, to lines: inout [String], indent: Int) {
        let spaces = String(repeating: " ", count: indent)
        if let dictionary = value as? [String: Any] {
            guard let firstKey = dictionary.keys.sorted().first else {
                lines.append("\(spaces)- {}")
                return
            }
            let firstValue = dictionary[firstKey] as Any
            if firstValue is [String: Any] || firstValue is [Any] {
                lines.append("\(spaces)- \(yamlKey(firstKey)):")
                appendYAMLSequenceValue(firstValue, to: &lines, indent: indent + 4)
            } else {
                lines.append("\(spaces)- \(yamlKey(firstKey)): \(yamlScalar(firstValue))")
            }
            for key in dictionary.keys.sorted().dropFirst() {
                appendYAML(
                    key: key,
                    value: dictionary[key] as Any,
                    to: &lines,
                    indent: indent + 2)
            }
        } else {
            lines.append("\(spaces)- \(yamlScalar(value))")
        }
    }

    private func appendYAMLSequenceValue(_ value: Any, to lines: inout [String], indent: Int) {
        if let dictionary = value as? [String: Any] {
            for key in dictionary.keys.sorted() {
                appendYAML(key: key, value: dictionary[key] as Any, to: &lines, indent: indent)
            }
        } else if let array = value as? [Any] {
            for item in array {
                appendYAMLSequenceItem(item, to: &lines, indent: indent)
            }
        } else {
            lines.append("\(String(repeating: " ", count: indent))\(yamlScalar(value))")
        }
    }

    private func yamlKey(_ key: String) -> String {
        let allowed = CharacterSet(
            charactersIn: "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-")
        return key.unicodeScalars.allSatisfy { allowed.contains($0) } ? key : quoteYAMLString(key)
    }

    private func yamlScalar(_ value: Any) -> String {
        if value is NSNull { return "null" }
        if let text = value as? String { return yamlString(text) }
        if let bool = value as? Bool { return bool ? "true" : "false" }
        if let number = value as? NSNumber {
            if CFGetTypeID(number) == CFBooleanGetTypeID() {
                return number.boolValue ? "true" : "false"
            }
            return number.stringValue
        }
        return quoteYAMLString(String(describing: value))
    }

    private func yamlString(_ value: String) -> String {
        let allowed = CharacterSet(charactersIn: "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-")
        let lower = value.lowercased()
        let reserved = ["true", "false", "null", "~"]
        if !value.isEmpty,
            value.unicodeScalars.allSatisfy({ allowed.contains($0) }),
            !reserved.contains(lower)
        {
            return value
        }
        return quoteYAMLString(value)
    }

    private func quoteYAMLString(_ value: String) -> String {
        let escaped = value
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\n", with: "\\n")
            .replacingOccurrences(of: "\r", with: "\\r")
            .replacingOccurrences(of: "\t", with: "\\t")
            .replacingOccurrences(of: "\"", with: "\\\"")
        return "\"\(escaped)\""
    }

    private func friendlyMessage(for error: Error) -> String {
        if let vaultError = error as? VaultError {
            switch vaultError {
            case .Io:
                return "\(displayName) could not be read — it may have been moved or deleted."
            case .FileTooLarge:
                return "\(displayName) is too large to open."
            case .InvalidUtf8:
                return "\(displayName) is not valid UTF-8 text."
            default:
                break
            }
        }
        return "\(displayName) could not be opened: \(error.localizedDescription)"
    }
}
