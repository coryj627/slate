// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

enum BaseQueryBuilderError: LocalizedError {
    case invalidQueryJSON
    case cannotEncodeQuery

    var errorDescription: String? {
        switch self {
        case .invalidQueryJSON:
            return "The query could not be loaded into the builder."
        case .cannotEncodeQuery:
            return "The builder draft could not be encoded as a Slate query."
        }
    }
}

enum BaseQuerySource: Hashable {
    case allNotes
    case folder(String)
    case tag(String)
    case recent(days: Int)
    case linked(fromPath: String)
    case tasks

    var accessibilityLabel: String {
        switch self {
        case .allNotes: return "All notes"
        case .folder(let path): return "Folder \(path)"
        case .tag(let tag): return "Tag \(tag)"
        case .recent(let days): return "Recently edited, \(days) days"
        case .linked(let path): return "Linked from \(path)"
        case .tasks: return "Tasks"
        }
    }

    fileprivate var querySourceJSON: Any {
        switch self {
        case .allNotes, .tasks:
            return "All"
        case .folder(let path):
            return ["Folder": path]
        case .tag(let tag):
            return ["Tag": tag]
        case .recent(let days):
            return ["Recent": ["days": max(days, 1)]]
        case .linked(let path):
            return ["Linked": ["from_path": path, "depth": 1]]
        }
    }

    fileprivate var rowSourceJSON: String {
        self == .tasks ? "Tasks" : "Files"
    }

    fileprivate var filterYAML: BaseQueryFilterYAML? {
        switch self {
        case .allNotes, .tasks:
            return nil
        case .folder(let path):
            return .stmt("file.inFolder(\(BaseQueryYAML.quoteString(path)))")
        case .tag(let tag):
            return .stmt("file.hasTag(\(BaseQueryYAML.quoteString(tag)))")
        case .recent(let days):
            return .stmt("file.mtime > now() - duration(\"\(max(days, 1))d\")")
        case .linked(let path):
            return .stmt("link(\(BaseQueryYAML.quoteString(path))).linksTo(file.file)")
        }
    }
}

enum BaseQueryCombinator: Hashable {
    case all
    case any
    case none

    var filterNodeName: String {
        switch self {
        case .all: return "And"
        case .any: return "Or"
        case .none: return "Not"
        }
    }

    var accessibilityValue: String {
        switch self {
        case .all: return "Combined with AND"
        case .any: return "Combined with OR"
        case .none: return "Combined with NONE"
        }
    }

    var groupLabel: String {
        switch self {
        case .all: return "ALL"
        case .any: return "ANY"
        case .none: return "NONE"
        }
    }
}

enum BaseQueryProperty: Hashable {
    case note(String)
    case file(BaseQueryFileField)
    case formula(String)
    case task(BaseQueryTaskField)

    var accessibilityName: String {
        switch self {
        case .note(let name): return name
        case .file(let field): return "file.\(field.sourceName)"
        case .formula(let name): return "formula.\(name)"
        case .task(let field): return "task.\(field.sourceName)"
        }
    }

    fileprivate var expressionJSON: Any {
        BaseQueryJSON.expr(["Prop": propertyRefJSON])
    }

    fileprivate var propertyRefJSON: Any {
        switch self {
        case .note(let name):
            return ["Note": name]
        case .file(let field):
            return ["File": field.serdeName]
        case .formula(let name):
            return ["Formula": name]
        case .task(let field):
            return ["TaskField": field.serdeName]
        }
    }

    var sourceExpression: String {
        switch self {
        case .note(let name):
            return name
        case .file(let field):
            return "file.\(field.sourceName)"
        case .formula(let name):
            return "formula.\(name)"
        case .task(let field):
            return "task.\(field.sourceName)"
        }
    }

    init?(columnID: String) {
        if let name = columnID.stripPrefix("formula.") {
            self = .formula(name)
        } else if let fieldName = columnID.stripPrefix("file."),
            let field = BaseQueryFileField(rawValue: fieldName)
        {
            self = .file(field)
        } else if let fieldName = columnID.stripPrefix("task."),
            let field = BaseQueryTaskField(rawValue: fieldName)
        {
            self = .task(field)
        } else if !columnID.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            self = .note(columnID)
        } else {
            return nil
        }
    }
}

enum BaseQueryFileField: String, CaseIterable, Hashable {
    case name
    case basename
    case path
    case folder
    case ext
    case size
    case properties
    case tags
    case aliases
    case links
    case backlinks
    case embeds
    case file
    case tasks
    case ctime
    case mtime
    case inDegree
    case outDegree

    var sourceName: String { rawValue }

    var serdeName: String {
        switch self {
        case .inDegree: return "InDegree"
        case .outDegree: return "OutDegree"
        default:
            return rawValue.prefix(1).uppercased() + String(rawValue.dropFirst())
        }
    }
}

enum BaseQueryTaskField: String, CaseIterable, Hashable {
    case text
    case status
    case completed
    case due
    case scheduled
    case priority
    case file

    var sourceName: String { rawValue }

    var serdeName: String {
        rawValue.prefix(1).uppercased() + String(rawValue.dropFirst())
    }
}

enum BaseQueryOperator: Hashable {
    case equals
    case notEquals
    case greaterThan
    case greaterThanOrEqual
    case lessThan
    case lessThanOrEqual
    case contains
    case startsWith
    case endsWith
    case isEmpty
    case hasTag
    case hasLink
    case matches

    var accessibilityName: String {
        switch self {
        case .equals: return "equals"
        case .notEquals: return "does not equal"
        case .greaterThan: return "is greater than"
        case .greaterThanOrEqual: return "is at least"
        case .lessThan: return "is less than"
        case .lessThanOrEqual: return "is at most"
        case .contains: return "contains"
        case .startsWith: return "starts with"
        case .endsWith: return "ends with"
        case .isEmpty: return "is empty"
        case .hasTag: return "has tag"
        case .hasLink: return "has link"
        case .matches: return "matches"
        }
    }

    fileprivate var binaryOpJSON: String? {
        switch self {
        case .equals: return "Eq"
        case .notEquals: return "Ne"
        case .greaterThan: return "Gt"
        case .greaterThanOrEqual: return "Gte"
        case .lessThan: return "Lt"
        case .lessThanOrEqual: return "Lte"
        case .contains, .startsWith, .endsWith, .isEmpty, .hasTag, .hasLink, .matches:
            return nil
        }
    }

    fileprivate var methodJSON: String? {
        switch self {
        case .contains: return "Contains"
        case .startsWith: return "StartsWith"
        case .endsWith: return "EndsWith"
        case .isEmpty: return "IsEmpty"
        case .hasTag: return "HasTag"
        case .hasLink: return "HasLink"
        case .matches: return "Matches"
        case .equals, .notEquals, .greaterThan, .greaterThanOrEqual, .lessThan, .lessThanOrEqual:
            return nil
        }
    }

    fileprivate var sourceBinaryOperator: String? {
        switch self {
        case .equals: return "=="
        case .notEquals: return "!="
        case .greaterThan: return ">"
        case .greaterThanOrEqual: return ">="
        case .lessThan: return "<"
        case .lessThanOrEqual: return "<="
        case .contains, .startsWith, .endsWith, .isEmpty, .hasTag, .hasLink, .matches:
            return nil
        }
    }

    fileprivate var sourceMethodName: String? {
        switch self {
        case .contains: return "contains"
        case .startsWith: return "startsWith"
        case .endsWith: return "endsWith"
        case .isEmpty: return "isEmpty"
        case .hasTag: return "hasTag"
        case .hasLink: return "hasLink"
        case .matches: return "matches"
        case .equals, .notEquals, .greaterThan, .greaterThanOrEqual, .lessThan, .lessThanOrEqual:
            return nil
        }
    }
}

enum BaseQueryValue: Hashable {
    case text(String)
    case number(Double)
    case bool(Bool)

    var accessibilityName: String {
        editingText
    }

    var editingText: String {
        switch self {
        case .text(let text): return text
        case .number(let number): return Self.numberFormatter.string(from: NSNumber(value: number))
            ?? String(number)
        case .bool(let value): return value ? "true" : "false"
        }
    }

    func replacingEditingText(_ text: String) -> BaseQueryValue {
        switch self {
        case .text:
            return .text(text)
        case .number:
            guard let number = Self.parseNumber(text) else { return self }
            return .number(number)
        case .bool:
            switch text.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() {
            case "true", "yes", "1":
                return .bool(true)
            case "false", "no", "0":
                return .bool(false)
            default:
                return self
            }
        }
    }

    fileprivate func replacingEditingText(
        _ text: String,
        preferredKind: BaseQueryValueKind?
    ) -> BaseQueryValue {
        guard let preferredKind else {
            return replacingEditingText(text)
        }
        switch preferredKind {
        case .number:
            return Self.parseNumber(text).map(BaseQueryValue.number) ?? preferredKind.defaultValue
        case .bool:
            return Self.parseBool(text).map(BaseQueryValue.bool) ?? preferredKind.defaultValue
        }
    }

    fileprivate func coerced(preferredKind: BaseQueryValueKind?) -> BaseQueryValue {
        guard let preferredKind else { return self }
        switch (preferredKind, self) {
        case (.number, .number(let number)):
            return number.isFinite ? self : preferredKind.defaultValue
        case (.bool, .bool):
            return self
        case (.number, _):
            return Self.parseNumber(editingText).map(BaseQueryValue.number) ?? preferredKind.defaultValue
        case (.bool, _):
            return Self.parseBool(editingText).map(BaseQueryValue.bool) ?? preferredKind.defaultValue
        }
    }

    fileprivate var expressionJSON: Any {
        switch self {
        case .text(let text):
            return BaseQueryJSON.expr(["Lit": ["String": text]])
        case .number(let number):
            return BaseQueryJSON.expr(["Lit": ["Number": number]])
        case .bool(let value):
            return BaseQueryJSON.expr(["Lit": ["Bool": value]])
        }
    }

    fileprivate var sourceLiteral: String {
        switch self {
        case .text(let text):
            return BaseQueryYAML.quoteString(text)
        case .number(let number):
            return Self.numberFormatter.string(from: NSNumber(value: number)) ?? String(number)
        case .bool(let value):
            return value ? "true" : "false"
        }
    }

    private static let numberFormatter: NumberFormatter = {
        let formatter = NumberFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.numberStyle = .decimal
        formatter.usesGroupingSeparator = false
        formatter.maximumFractionDigits = 16
        formatter.minimumFractionDigits = 0
        return formatter
    }()

    private static func parseNumber(_ text: String) -> Double? {
        let normalized = text.trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: ",", with: "")
        guard let number = Double(normalized), number.isFinite else { return nil }
        return number
    }

    private static func parseBool(_ text: String) -> Bool? {
        switch text.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() {
        case "true", "yes", "1":
            return true
        case "false", "no", "0":
            return false
        default:
            return nil
        }
    }
}

fileprivate enum BaseQueryValueKind {
    case number
    case bool

    var defaultValue: BaseQueryValue {
        switch self {
        case .number: return .number(0)
        case .bool: return .bool(false)
        }
    }
}

struct BaseQueryCondition: Hashable {
    var property: BaseQueryProperty
    var op: BaseQueryOperator
    var value: BaseQueryValue

    init(
        property: BaseQueryProperty = .note("status"),
        operator conditionOperator: BaseQueryOperator = .equals,
        value: BaseQueryValue = .text("active")
    ) {
        self.property = property
        self.op = conditionOperator
        self.value = value
    }

    var accessibilityPhrase: String {
        if op == .isEmpty {
            return "\(property.accessibilityName) \(op.accessibilityName)"
        }
        return "\(property.accessibilityName) \(op.accessibilityName) \(value.accessibilityName)"
    }

    mutating func replaceValueEditingText(_ text: String) {
        value = value.replacingEditingText(text, preferredKind: preferredValueKind)
    }

    fileprivate var filterNodeJSON: Any {
        ["Stmt": expressionJSON]
    }

    private var expressionJSON: Any {
        let encodedValue = value.coerced(preferredKind: preferredValueKind)
        if let binary = op.binaryOpJSON {
            return BaseQueryJSON.expr([
                "Binary": [
                    "op": binary,
                    "lhs": property.expressionJSON,
                    "rhs": encodedValue.expressionJSON,
                ]
            ])
        }
        let method = op.methodJSON ?? "Contains"
        let args: [Any] = method == "IsEmpty" ? [] : [encodedValue.expressionJSON]
        return BaseQueryJSON.expr([
            "Call": [
                "callee": [
                    "Method": [
                        "receiver": property.expressionJSON,
                        "name": method,
                    ]
                ],
                "args": args,
            ]
        ])
    }

    fileprivate var expressionSource: String {
        let encodedValue = value.coerced(preferredKind: preferredValueKind)
        if let symbol = op.sourceBinaryOperator {
            return "\(property.sourceExpression) \(symbol) \(encodedValue.sourceLiteral)"
        }
        let method = op.sourceMethodName ?? "contains"
        if op == .isEmpty {
            return "\(property.sourceExpression).\(method)()"
        }
        return "\(property.sourceExpression).\(method)(\(encodedValue.sourceLiteral))"
    }

    private var preferredValueKind: BaseQueryValueKind? {
        guard op != .isEmpty else { return nil }
        switch property {
        case .file(.size), .file(.inDegree), .file(.outDegree), .task(.priority):
            return .number
        case .task(.completed):
            return .bool
        case .note, .file, .formula, .task:
            return nil
        }
    }
}

enum BaseQueryBuilderRow: Hashable {
    case condition(BaseQueryCondition)
    case group(BaseQueryConditionGroup)
    case advanced(rawExpression: String, filterJSON: String?)

    func accessibilityLabel(index: Int) -> String {
        switch self {
        case .condition(let condition):
            return "Condition \(index + 1): \(condition.accessibilityPhrase)"
        case .group(let group):
            let count = group.rows.count
            return "Group \(index + 1): \(group.combinator.groupLabel) of \(count) \(count == 1 ? "condition" : "conditions")"
        case .advanced(let rawExpression, _):
            return "Advanced condition: \(rawExpression)"
        }
    }

    fileprivate var filterNodeJSON: Any {
        switch self {
        case .condition(let condition):
            return condition.filterNodeJSON
        case .group(let group):
            return group.filterNodeJSON
        case .advanced(let rawExpression, let filterJSON):
            if let node = BaseQueryJSON.decode(filterJSON) {
                return node
            }
            return [
                "Stmt": BaseQueryJSON.expr([
                    "Unsupported": [
                        "raw": rawExpression,
                        "reason": "advanced builder expression",
                    ]
                ])
            ]
        }
    }
}

struct BaseQueryConditionGroup: Hashable {
    var combinator: BaseQueryCombinator = .all
    var rows: [BaseQueryBuilderRow] = []

    fileprivate var filterNodeJSON: Any {
        [combinator.filterNodeName: rows.map(\.filterNodeJSON)]
    }
}

enum BaseQueryViewType: String, CaseIterable, Hashable, Identifiable {
    case table
    case list

    var id: String { rawValue }

    var title: String {
        switch self {
        case .table: return "Table"
        case .list: return "List"
        }
    }

    fileprivate var queryViewJSON: Any {
        switch self {
        case .table: return ["Table": ["fallback_from": NSNull()]]
        case .list: return ["List": ["fallback_from": NSNull()]]
        }
    }
}

struct BaseQueryGroupBy: Hashable {
    var property: BaseQueryProperty
    var ascending: Bool

    fileprivate var queryJSON: Any {
        [
            "property": property.propertyRefJSON,
            "ascending": ascending,
        ]
    }

    fileprivate var yamlFragment: String {
        [
            "groupBy:",
            "  property: \(BaseQueryYAML.scalar(property.sourceExpression))",
            "  direction: \(ascending ? "ASC" : "DESC")",
        ].joined(separator: "\n")
    }
}

struct BaseQuerySortKey: Hashable {
    var property: BaseQueryProperty?
    var expressionJSON: String
    var expressionLabel: String
    var ascending: Bool

    init(property: BaseQueryProperty, ascending: Bool) {
        self.property = property
        self.expressionJSON = BaseQueryJSON.canonicalJSONString(property.expressionJSON) ?? ""
        self.expressionLabel = property.sourceExpression
        self.ascending = ascending
    }

    fileprivate init(expressionJSON: String, expressionLabel: String, ascending: Bool) {
        self.property = nil
        self.expressionJSON = expressionJSON
        self.expressionLabel = expressionLabel
        self.ascending = ascending
    }

    fileprivate var queryJSON: Any {
        [
            "expr": BaseQueryJSON.decode(expressionJSON) ?? BaseQueryJSON.unsupportedExpr(expressionLabel),
            "ascending": ascending,
        ]
    }

    fileprivate var slateYAML: String {
        [
            "    - expr: \(BaseQueryYAML.scalar(expressionLabel))",
            "      direction: \(ascending ? "asc" : "desc")",
        ].joined(separator: "\n")
    }
}

struct BaseQueryColumn: Hashable, Identifiable {
    var id: String
    var property: BaseQueryProperty?
    var displayName: String?

    init(property: BaseQueryProperty, displayName: String?) {
        self.id = property.sourceExpression
        self.property = property
        self.displayName = displayName
    }

    fileprivate init(id: String, displayName: String?) {
        self.id = id
        self.property = BaseQueryProperty(columnID: id)
        self.displayName = displayName
    }

    fileprivate var queryJSON: [String: Any] {
        [
            "id": id,
            "display_name": displayName ?? NSNull(),
        ]
    }
}

struct BaseQueryFormula: Hashable, Identifiable {
    var name: String
    var expression: String
    var expressionJSON: String

    var id: String { name }

    init(name: String, expression: String, expressionJSON: String) throws {
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { throw BaseQueryBuilderError.invalidQueryJSON }
        guard BaseQueryJSON.decode(expressionJSON) != nil else {
            throw BaseQueryBuilderError.invalidQueryJSON
        }
        self.name = trimmed
        self.expression = expression
        self.expressionJSON = expressionJSON
    }

    fileprivate init(name: String, expressionJSON: String) {
        self.name = name
        self.expressionJSON = expressionJSON
        self.expression = BaseQueryExpressionSource.render(json: expressionJSON) ?? expressionJSON
    }

    fileprivate var queryJSON: [Any] {
        [name, BaseQueryJSON.decode(expressionJSON) ?? BaseQueryJSON.unsupportedExpr(expression)]
    }
}

enum BaseQueryPreviewState: Equatable {
    case idle
    case loading
    case ready(BasesResultSet)
    case failed(String)

    var accessibilityAnnouncement: String {
        switch self {
        case .idle:
            return "Preview not loaded."
        case .loading:
            return "Preview loading."
        case .ready(let result):
            var text = result.audioSummary
            if let first = result.rows.first?.audioDescription,
                !first.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            {
                text += ". First result: \(first)"
            }
            return text
        case .failed(let message):
            return "Preview failed: \(message)"
        }
    }
}

struct BaseQueryBuilderDraft: Equatable {
    var source: BaseQuerySource = .allNotes
    var combinator: BaseQueryCombinator = .all
    var rows: [BaseQueryBuilderRow] = []
    var sortKeys: [BaseQuerySortKey] = []
    var groupBy: BaseQueryGroupBy?
    var columns: [BaseQueryColumn] = []
    var formulas: [BaseQueryFormula] = []
    var viewType: BaseQueryViewType = .table

    init() {}

    init(queryJSON: String) throws {
        guard
            let data = queryJSON.data(using: .utf8),
            let root = try JSONSerialization.jsonObject(with: data) as? [String: Any]
        else { throw BaseQueryBuilderError.invalidQueryJSON }

        let rowSource = root["row_source"] as? String
        source = rowSource == "Tasks" ? .tasks : Self.decodeSource(root["source"])

        let decoded = Self.decodeFilterRows(root["filters"])
        if source == .allNotes, let canonical = decoded.source {
            source = canonical
        }
        combinator = decoded.combinator
        rows = decoded.rows
        sortKeys = Self.decodeSortKeys(root["sort"])
        groupBy = Self.decodeGroupBy(root["group_by"])
        columns = Self.decodeColumns(root["columns"])
        formulas = Self.decodeFormulas(root["formulas"])
        viewType = Self.decodeViewType(root["view"])
    }

    var conditionsListAccessibilityValue: String {
        combinator.accessibilityValue
    }

    func queryJSON() throws -> String {
        let root: [String: Any] = [
            "source": source.querySourceJSON,
            "row_source": source.rowSourceJSON,
            "filters": filterJSON ?? NSNull(),
            "formulas": formulas.map(\.queryJSON),
            "custom_summaries": [],
            "group_by": groupBy?.queryJSON ?? NSNull(),
            "sort": sortKeys.map(\.queryJSON),
            "columns": encodedColumns,
            "summaries": [],
            "limit": NSNull(),
            "view": viewType.queryViewJSON,
        ]
        let data = try JSONSerialization.data(withJSONObject: root, options: [.sortedKeys])
        guard let json = String(data: data, encoding: .utf8) else {
            throw BaseQueryBuilderError.cannotEncodeQuery
        }
        return json
    }

    func baseEditsForView(
        _ view: UInt32,
        replacing previous: BaseQueryBuilderDraft? = nil
    ) throws -> [BaseEdit] {
        var edits: [BaseEdit] = []
        if let viewFilterYAML {
            edits.append(.setViewFilters(view: view, yaml: viewFilterYAML))
        } else if previous?.filterYAML != nil {
            edits.append(.removeViewKey(view: view, key: "filters"))
        }
        edits.append(.setViewKey(view: view, key: "type", value: viewType.rawValue))
        if source == .tasks {
            edits.append(.setViewKey(view: view, key: "source", value: "tasks"))
        } else if previous?.source == .tasks {
            edits.append(.removeViewKey(view: view, key: "source"))
        }
        if let groupBy {
            edits.append(.setViewKey(view: view, key: "groupBy", value: groupBy.yamlFragment))
        } else if previous?.groupBy != nil {
            edits.append(.removeViewKey(view: view, key: "groupBy"))
        }
        edits.append(.setViewKey(view: view, key: "order", value: orderYAMLFragment))
        if let previous {
            let currentFormulaNames = Set(formulas.map(\.name))
            for formula in previous.formulas where !currentFormulaNames.contains(formula.name) {
                edits.append(.removeFormula(name: formula.name))
            }
        }
        let previousFormulaJSON = previous.map { Self.formulaJSONMap(for: $0.formulas) } ?? [:]
        for formula in formulas
        where previous == nil || previousFormulaJSON[formula.name] != formula.expressionJSON {
            edits.append(.setFormula(name: formula.name, expression: formula.expression))
        }
        let previousDisplayNames = previous.map { displayNameMap(for: $0.columns) } ?? [:]
        for column in columns {
            let displayName = normalizedDisplayName(column.displayName)
            if previous == nil {
                if let displayName {
                    edits.append(.setDisplayName(property: column.id, displayName: displayName))
                }
            } else if previousDisplayNames[column.id] != displayName {
                edits.append(.setDisplayName(property: column.id, displayName: displayName))
            }
        }
        if !sortKeys.isEmpty || previous?.sortKeys.isEmpty == false {
            edits.append(.setSlateState(view: view, yaml: slateStateYAML))
        }
        return edits
    }

    private var filterJSON: Any? {
        guard !rows.isEmpty else { return nil }
        if rows.count == 1, combinator == .all {
            return rows[0].filterNodeJSON
        }
        return [combinator.filterNodeName: rows.map(\.filterNodeJSON)]
    }

    private var encodedColumns: [[String: Any]] {
        let selected = columns.isEmpty ? defaultColumnSelections : columns
        return selected.map(\.queryJSON)
    }

    private var defaultColumnSelections: [BaseQueryColumn] {
        if source == .tasks {
            return [
                BaseQueryColumn(property: .task(.text), displayName: nil),
                BaseQueryColumn(property: .task(.file), displayName: nil),
            ]
        }
        return [BaseQueryColumn(property: .file(.name), displayName: nil)]
    }

    private var filterYAML: String? {
        var filters: [BaseQueryFilterYAML] = []
        if let sourceFilter = source.filterYAML {
            filters.append(sourceFilter)
        }
        filters.append(contentsOf: rows.compactMap(Self.filterYAML(for:)))
        guard !filters.isEmpty else { return nil }
        if filters.count == 1 {
            return filters[0].yamlValue(indent: 0)
        }
        return BaseQueryFilterYAML.all(filters).yamlValue(indent: 0)
    }

    private var viewFilterYAML: String? {
        guard let filterYAML else { return nil }
        let lines = filterYAML.split(separator: "\n", omittingEmptySubsequences: false)
        guard lines.count > 1 else { return "filters: \(filterYAML)" }
        return (["filters:"] + lines.map { "  \($0)" }).joined(separator: "\n")
    }

    private var orderYAMLFragment: String {
        let selected = columns.isEmpty ? defaultColumnSelections : columns
        var lines = ["order:"]
        for column in selected {
            lines.append("  - \(BaseQueryYAML.scalar(column.id))")
        }
        return lines.joined(separator: "\n")
    }

    private var slateStateYAML: String? {
        guard !sortKeys.isEmpty else { return nil }
        var lines = ["slate:", "  sort:"]
        for sort in sortKeys {
            lines.append(sort.slateYAML)
        }
        return lines.joined(separator: "\n")
    }

    private func normalizedDisplayName(_ value: String?) -> String? {
        let trimmed = value?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return trimmed.isEmpty ? nil : trimmed
    }

    private func displayNameMap(for columns: [BaseQueryColumn]) -> [String: String] {
        var names: [String: String] = [:]
        for column in columns {
            names[column.id] = normalizedDisplayName(column.displayName)
        }
        return names
    }

    private static func formulaJSONMap(for formulas: [BaseQueryFormula]) -> [String: String] {
        var map: [String: String] = [:]
        for formula in formulas {
            map[formula.name] = formula.expressionJSON
        }
        return map
    }

    fileprivate static func filterYAML(for row: BaseQueryBuilderRow) -> BaseQueryFilterYAML? {
        switch row {
        case .condition(let condition):
            return .stmt(condition.expressionSource)
        case .group(let group):
            let children = group.rows.compactMap(filterYAML(for:))
            guard !children.isEmpty else { return nil }
            switch group.combinator {
            case .all: return .all(children)
            case .any: return .any(children)
            case .none: return .none(children)
            }
        case .advanced(let rawExpression, let filterJSON):
            if let source = BaseQueryExpressionSource.render(json: filterJSON) {
                return .stmt(source)
            }
            return .stmt(rawExpression)
        }
    }

    private static func decodeSource(_ value: Any?) -> BaseQuerySource {
        if let text = value as? String, text == "All" {
            return .allNotes
        }
        guard let object = value as? [String: Any] else { return .allNotes }
        if let folder = object["Folder"] as? String { return .folder(folder) }
        if let tag = object["Tag"] as? String { return .tag(tag) }
        if let recent = object["Recent"] as? [String: Any],
            let days = recent["days"] as? Int
        {
            return .recent(days: days)
        }
        if let linked = object["Linked"] as? [String: Any],
            let fromPath = linked["from_path"] as? String
        {
            return .linked(fromPath: fromPath)
        }
        return .allNotes
    }

    private static func decodeSortKeys(_ value: Any?) -> [BaseQuerySortKey] {
        guard let items = value as? [Any] else { return [] }
        return items.compactMap { item in
            guard let object = item as? [String: Any],
                let expr = object["expr"] as? [String: Any],
                let ascending = object["ascending"] as? Bool,
                let expressionJSON = BaseQueryJSON.canonicalJSONString(expr)
            else { return nil }
            if let property = decodeProperty(expr) {
                return BaseQuerySortKey(property: property, ascending: ascending)
            }
            return BaseQuerySortKey(
                expressionJSON: expressionJSON,
                expressionLabel: BaseQueryExpressionSource.render(json: expressionJSON) ?? "advanced sort",
                ascending: ascending)
        }
    }

    private static func decodeGroupBy(_ value: Any?) -> BaseQueryGroupBy? {
        guard !(value is NSNull),
            let object = value as? [String: Any],
            let propertyJSON = object["property"] as? [String: Any],
            let property = decodePropertyRef(propertyJSON),
            let ascending = object["ascending"] as? Bool
        else { return nil }
        return BaseQueryGroupBy(property: property, ascending: ascending)
    }

    private static func decodeColumns(_ value: Any?) -> [BaseQueryColumn] {
        guard let items = value as? [Any] else { return [] }
        return items.compactMap { item in
            guard let object = item as? [String: Any],
                let id = object["id"] as? String
            else { return nil }
            let displayName: String?
            if object["display_name"] is NSNull {
                displayName = nil
            } else {
                displayName = object["display_name"] as? String
            }
            return BaseQueryColumn(id: id, displayName: displayName)
        }
    }

    private static func decodeFormulas(_ value: Any?) -> [BaseQueryFormula] {
        guard let items = value as? [Any] else { return [] }
        return items.compactMap { item in
            guard let pair = item as? [Any],
                pair.count == 2,
                let name = pair[0] as? String,
                let expr = pair[1] as? [String: Any],
                let expressionJSON = BaseQueryJSON.canonicalJSONString(expr)
            else { return nil }
            return BaseQueryFormula(name: name, expressionJSON: expressionJSON)
        }
    }

    private static func decodeViewType(_ value: Any?) -> BaseQueryViewType {
        guard let object = value as? [String: Any] else { return .table }
        if object["List"] != nil { return .list }
        return .table
    }

    private static func decodeFilterRows(
        _ value: Any?
    ) -> (source: BaseQuerySource?, combinator: BaseQueryCombinator, rows: [BaseQueryBuilderRow]) {
        guard !(value is NSNull), let value else { return (nil, .all, []) }
        if let object = value as? [String: Any] {
            if let nodes = object["And"] as? [Any] {
                return decodeRows(
                    nodes: nodes,
                    combinator: .all,
                    allowSourceExtraction: true,
                    depth: 0)
            }
            if let nodes = object["Or"] as? [Any] {
                return decodeRows(
                    nodes: nodes,
                    combinator: .any,
                    allowSourceExtraction: false,
                    depth: 0)
            }
            if let nodes = object["Not"] as? [Any] {
                return decodeRows(
                    nodes: nodes,
                    combinator: .none,
                    allowSourceExtraction: false,
                    depth: 0)
            }
        }
        if let row = decodeRow(value, allowSourceExtraction: true, depth: 0) {
            if let source = row.source {
                return (source, .all, [])
            }
            return (nil, .all, [row.row])
        }
        return (nil, .all, [advancedRow(value, fallback: "unrecognized filter")])
    }

    private static func decodeRows(
        nodes: [Any],
        combinator: BaseQueryCombinator,
        allowSourceExtraction: Bool,
        depth: Int
    ) -> (source: BaseQuerySource?, combinator: BaseQueryCombinator, rows: [BaseQueryBuilderRow]) {
        var source: BaseQuerySource?
        var rows: [BaseQueryBuilderRow] = []
        for node in nodes {
            guard
                let decoded = decodeRow(
                    node,
                    allowSourceExtraction: allowSourceExtraction,
                    depth: depth)
            else {
                rows.append(advancedRow(node, fallback: "unrecognized filter"))
                continue
            }
            if source == nil, let canonical = decoded.source {
                source = canonical
            } else {
                rows.append(decoded.row)
            }
        }
        return (source, combinator, rows)
    }

    private static func decodeRow(
        _ value: Any,
        allowSourceExtraction: Bool,
        depth: Int
    ) -> (source: BaseQuerySource?, row: BaseQueryBuilderRow)? {
        guard let object = value as? [String: Any] else { return nil }
        if let expr = object["Stmt"] as? [String: Any] {
            if allowSourceExtraction, let source = decodeCanonicalSourceExpression(expr) {
                return (source, advancedRow(value, fallback: source.accessibilityLabel))
            }
            if let condition = decodeConditionExpression(expr) {
                return (nil, .condition(condition))
            }
            return (nil, advancedRow(value, fallback: expressionFallback(expr)))
        }
        if let nodes = object["And"] as? [Any] {
            guard depth == 0 else {
                return (nil, advancedRow(value, fallback: "nested ALL group"))
            }
            let decoded = decodeRows(
                nodes: nodes,
                combinator: .all,
                allowSourceExtraction: false,
                depth: depth + 1)
            return (nil, .group(BaseQueryConditionGroup(combinator: .all, rows: decoded.rows)))
        }
        if let nodes = object["Or"] as? [Any] {
            guard depth == 0 else {
                return (nil, advancedRow(value, fallback: "nested ANY group"))
            }
            let decoded = decodeRows(
                nodes: nodes,
                combinator: .any,
                allowSourceExtraction: false,
                depth: depth + 1)
            return (nil, .group(BaseQueryConditionGroup(combinator: .any, rows: decoded.rows)))
        }
        if let nodes = object["Not"] as? [Any] {
            guard depth == 0 else {
                return (nil, advancedRow(value, fallback: "nested NONE group"))
            }
            let decoded = decodeRows(
                nodes: nodes,
                combinator: .none,
                allowSourceExtraction: false,
                depth: depth + 1)
            return (nil, .group(BaseQueryConditionGroup(combinator: .none, rows: decoded.rows)))
        }
        return nil
    }

    private static func decodeCanonicalSourceExpression(_ expr: [String: Any]) -> BaseQuerySource? {
        if let recent = decodeRecentSourceExpression(expr) {
            return recent
        }
        guard let call = methodCall(expr) else { return nil }

        switch call.name {
        case "InFolder":
            guard isProperty(call.receiver, .file(.file)),
                call.args.count == 1,
                let folder = stringLiteral(call.args[0])
            else { return nil }
            return .folder(folder)
        case "HasTag":
            guard isProperty(call.receiver, .file(.file)),
                call.args.count == 1,
                let tag = stringLiteral(call.args[0])
            else { return nil }
            return .tag(tag)
        case "LinksTo":
            guard call.args.count == 1,
                let target = call.args[0] as? [String: Any],
                isProperty(target, .file(.file)),
                let linkArgs = globalCallArgs(call.receiver, name: "Link"),
                linkArgs.count == 1,
                let fromPath = stringLiteral(linkArgs[0])
            else { return nil }
            return .linked(fromPath: fromPath)
        default:
            return nil
        }
    }

    private static func decodeRecentSourceExpression(_ expr: [String: Any]) -> BaseQuerySource? {
        guard let binary = kindPayload("Binary", in: expr),
            binary["op"] as? String == "Gt",
            let lhs = binary["lhs"] as? [String: Any],
            isProperty(lhs, .file(.mtime)),
            let rhs = binary["rhs"] as? [String: Any],
            let duration = kindPayload("Binary", in: rhs),
            duration["op"] as? String == "Sub",
            let now = duration["lhs"] as? [String: Any],
            let nowArgs = globalCallArgs(now, name: "Now"),
            nowArgs.isEmpty,
            let durationCall = duration["rhs"] as? [String: Any],
            let durationArgs = globalCallArgs(durationCall, name: "Duration"),
            durationArgs.count == 1,
            let literal = stringLiteral(durationArgs[0]),
            literal.hasSuffix("d"),
            let days = Int(literal.dropLast()),
            days > 0
        else { return nil }
        return .recent(days: days)
    }

    private static func decodeConditionExpression(_ expr: [String: Any]) -> BaseQueryCondition? {
        if let binary = kindPayload("Binary", in: expr),
            let op = binary["op"] as? String,
            let lhs = binary["lhs"] as? [String: Any],
            let rhs = binary["rhs"] as? [String: Any],
            let property = decodeProperty(lhs),
            let value = decodeLiteral(rhs),
            let conditionOperator = decodeBinaryOperator(op)
        {
            return BaseQueryCondition(
                property: property,
                operator: conditionOperator,
                value: value)
        }
        if let call = methodCall(expr),
            let property = decodeProperty(call.receiver),
            let conditionOperator = decodeMethodOperator(call.name),
            let value = decodeMethodConditionValue(operator: conditionOperator, args: call.args)
        {
            return BaseQueryCondition(
                property: property,
                operator: conditionOperator,
                value: value)
        }
        return nil
    }

    private static func decodeBinaryOperator(_ op: String) -> BaseQueryOperator? {
        switch op {
        case "Eq": return .equals
        case "Ne": return .notEquals
        case "Gt": return .greaterThan
        case "Gte": return .greaterThanOrEqual
        case "Lt": return .lessThan
        case "Lte": return .lessThanOrEqual
        default: return nil
        }
    }

    private static func decodeMethodConditionValue(
        operator conditionOperator: BaseQueryOperator,
        args: [Any]
    ) -> BaseQueryValue? {
        switch conditionOperator {
        case .isEmpty:
            return args.isEmpty ? .text("") : nil
        case .contains, .startsWith, .endsWith, .hasTag, .hasLink, .matches:
            guard args.count == 1,
                let expr = args[0] as? [String: Any]
            else { return nil }
            return decodeLiteral(expr)
        case .equals, .notEquals, .greaterThan, .greaterThanOrEqual, .lessThan, .lessThanOrEqual:
            return nil
        }
    }

    private static func decodeMethodOperator(_ name: String) -> BaseQueryOperator? {
        switch name {
        case "Contains": return .contains
        case "StartsWith": return .startsWith
        case "EndsWith": return .endsWith
        case "IsEmpty": return .isEmpty
        case "HasTag": return .hasTag
        case "HasLink": return .hasLink
        case "Matches": return .matches
        default: return nil
        }
    }

    private static func decodeProperty(_ expr: [String: Any]) -> BaseQueryProperty? {
        guard let prop = kindPayload("Prop", in: expr) else { return nil }
        return decodePropertyRef(prop)
    }

    fileprivate static func decodePropertyRef(_ prop: [String: Any]) -> BaseQueryProperty? {
        if let note = prop["Note"] as? String { return .note(note) }
        if let formula = prop["Formula"] as? String { return .formula(formula) }
        if let file = prop["File"] as? String,
            let field = BaseQueryFileField(serdeName: file)
        {
            return .file(field)
        }
        if let task = prop["TaskField"] as? String,
            let field = BaseQueryTaskField(serdeName: task)
        {
            return .task(field)
        }
        return nil
    }

    private static func decodeLiteral(_ expr: [String: Any]) -> BaseQueryValue? {
        guard let lit = kindPayload("Lit", in: expr) else { return nil }
        if let text = lit["String"] as? String { return .text(text) }
        if let number = lit["Number"] as? Double { return .number(number) }
        if let number = lit["Number"] as? Int { return .number(Double(number)) }
        if let bool = lit["Bool"] as? Bool { return .bool(bool) }
        return nil
    }

    private static func kindPayload(_ name: String, in expr: [String: Any]) -> [String: Any]? {
        guard let kind = expr["kind"] as? [String: Any] else { return nil }
        return kind[name] as? [String: Any]
    }

    private static func methodCall(_ expr: [String: Any]) -> (
        name: String,
        receiver: [String: Any],
        args: [Any]
    )? {
        guard
            let call = kindPayload("Call", in: expr),
            let callee = call["callee"] as? [String: Any],
            let method = callee["Method"] as? [String: Any],
            let name = method["name"] as? String,
            let receiver = method["receiver"] as? [String: Any]
        else { return nil }
        return (name, receiver, call["args"] as? [Any] ?? [])
    }

    private static func globalCallArgs(_ expr: [String: Any], name: String) -> [Any]? {
        guard
            let call = kindPayload("Call", in: expr),
            let callee = call["callee"] as? [String: Any],
            let global = callee["Global"] as? String,
            global == name
        else { return nil }
        return call["args"] as? [Any] ?? []
    }

    private static func isProperty(_ expr: [String: Any], _ property: BaseQueryProperty) -> Bool {
        decodeProperty(expr) == property
    }

    private static func stringLiteral(_ value: Any) -> String? {
        guard let expr = value as? [String: Any],
            case .text(let text) = decodeLiteral(expr)
        else { return nil }
        return text
    }

    private static func expressionFallback(_ expr: [String: Any]) -> String {
        if let unsupported = kindPayload("Unsupported", in: expr),
            let raw = unsupported["raw"] as? String
        {
            return raw
        }
        return "unstructured expression"
    }

    private static func advancedRow(_ value: Any, fallback: String) -> BaseQueryBuilderRow {
        .advanced(rawExpression: canonicalJSONString(value) ?? fallback, filterJSON: canonicalJSONString(value))
    }

    private static func canonicalJSONString(_ value: Any) -> String? {
        guard JSONSerialization.isValidJSONObject(value),
            let data = try? JSONSerialization.data(withJSONObject: value, options: [.sortedKeys])
        else { return nil }
        return String(data: data, encoding: .utf8)
    }
}

@MainActor
final class BaseQueryBuilderModel: ObservableObject {
    @Published var draft: BaseQueryBuilderDraft
    @Published var selectedRowIndex: Int?
    @Published var editingRowIndex: Int?
    @Published var previewState: BaseQueryPreviewState = .idle
    private let initialDraft: BaseQueryBuilderDraft

    init(draft: BaseQueryBuilderDraft = BaseQueryBuilderDraft()) {
        self.draft = draft
        self.initialDraft = draft
    }

    var source: BaseQuerySource {
        get { draft.source }
        set { draft.source = newValue }
    }

    var combinator: BaseQueryCombinator {
        get { draft.combinator }
        set { draft.combinator = newValue }
    }

    var rows: [BaseQueryBuilderRow] {
        get { draft.rows }
        set { draft.rows = newValue }
    }

    var sortKeys: [BaseQuerySortKey] {
        get { draft.sortKeys }
        set { draft.sortKeys = newValue }
    }

    var groupBy: BaseQueryGroupBy? {
        get { draft.groupBy }
        set { draft.groupBy = newValue }
    }

    var columns: [BaseQueryColumn] {
        get { draft.columns }
        set { draft.columns = newValue }
    }

    var formulas: [BaseQueryFormula] {
        get { draft.formulas }
        set { draft.formulas = newValue }
    }

    var viewType: BaseQueryViewType {
        get { draft.viewType }
        set { draft.viewType = newValue }
    }

    var conditionsListAccessibilityValue: String {
        draft.conditionsListAccessibilityValue
    }

    func baseEditsForView(_ view: UInt32) throws -> [BaseEdit] {
        try draft.baseEditsForView(view, replacing: initialDraft)
    }

    func removeFormula(named name: String) {
        let columnID = "formula.\(name)"
        draft.formulas.removeAll { $0.name == name }
        draft.columns.removeAll { $0.id == columnID }
        draft.sortKeys.removeAll { sortKey in
            sortKey.property == .formula(name) || sortKey.expressionLabel == columnID
        }
        if draft.groupBy?.property == .formula(name) {
            draft.groupBy = nil
        }
    }

    func perform(_ command: BaseQueryBuilderCommand) {
        switch command {
        case .addCondition:
            draft.rows.append(.condition(BaseQueryCondition()))
            selectedRowIndex = draft.rows.count - 1
            editingRowIndex = selectedRowIndex
        case .addGroup:
            let group = BaseQueryConditionGroup(
                combinator: .all,
                rows: [.condition(BaseQueryCondition())])
            draft.rows.append(.group(group))
            selectedRowIndex = draft.rows.count - 1
            editingRowIndex = nil
        case .removeCondition(let index):
            guard draft.rows.indices.contains(index) else { return }
            draft.rows.remove(at: index)
            selectedRowIndex = draft.rows.indices.contains(index) ? index : draft.rows.indices.last
            editingRowIndex = nil
        case .editCondition(let index):
            guard draft.rows.indices.contains(index) else { return }
            selectedRowIndex = index
            editingRowIndex = index
        case .setGroupCombinator(let index, let combinator):
            guard draft.rows.indices.contains(index),
                case .group(var group) = draft.rows[index]
            else { return }
            group.combinator = combinator
            draft.rows[index] = .group(group)
        case .addConditionToGroup(let index):
            guard draft.rows.indices.contains(index),
                case .group(var group) = draft.rows[index]
            else { return }
            group.rows.append(.condition(BaseQueryCondition()))
            draft.rows[index] = .group(group)
            selectedRowIndex = index
        case .removeConditionFromGroup(let groupIndex, let conditionIndex):
            guard draft.rows.indices.contains(groupIndex),
                case .group(var group) = draft.rows[groupIndex],
                group.rows.indices.contains(conditionIndex)
            else { return }
            group.rows.remove(at: conditionIndex)
            if group.rows.isEmpty {
                draft.rows.remove(at: groupIndex)
                selectedRowIndex = draft.rows.indices.contains(groupIndex)
                    ? groupIndex
                    : draft.rows.indices.last
            } else {
                draft.rows[groupIndex] = .group(group)
                selectedRowIndex = groupIndex
            }
        case .editAsExpression(let index):
            guard draft.rows.indices.contains(index) else { return }
            let row = draft.rows[index]
            let raw = BaseQueryBuilderDraft.filterYAML(for: row)?.expressionText
                ?? row.accessibilityLabel(index: index)
            draft.rows[index] = .advanced(
                rawExpression: raw,
                filterJSON: BaseQueryJSON.canonicalJSONString(row.filterNodeJSON))
            selectedRowIndex = index
            editingRowIndex = index
        }
    }

    func updateAdvancedExpression(
        index: Int,
        rawExpression: String,
        validation: BaseExpressionValidation?
    ) {
        guard draft.rows.indices.contains(index),
            case .advanced = draft.rows[index]
        else { return }
        let filterJSON: String?
        if let exprJSON = validation?.exprJson,
            let expr = BaseQueryJSON.decode(exprJSON)
        {
            filterJSON = BaseQueryJSON.canonicalJSONString(["Stmt": expr])
        } else {
            filterJSON = nil
        }
        draft.rows[index] = .advanced(rawExpression: rawExpression, filterJSON: filterJSON)
    }
}

enum BaseQueryBuilderCommand: Equatable {
    case addCondition
    case addGroup
    case removeCondition(index: Int)
    case editCondition(index: Int)
    case setGroupCombinator(index: Int, combinator: BaseQueryCombinator)
    case addConditionToGroup(index: Int)
    case removeConditionFromGroup(groupIndex: Int, conditionIndex: Int)
    case editAsExpression(index: Int)
}

private enum BaseQueryJSON {
    static func expr(_ kind: [String: Any]) -> [String: Any] {
        [
            "kind": kind,
            "span": ["start": 0, "end": 0],
        ]
    }

    static func decode(_ json: String?) -> Any? {
        guard let json,
            let data = json.data(using: .utf8)
        else { return nil }
        return try? JSONSerialization.jsonObject(with: data)
    }

    static func canonicalJSONString(_ value: Any) -> String? {
        guard JSONSerialization.isValidJSONObject(value),
            let data = try? JSONSerialization.data(withJSONObject: value, options: [.sortedKeys])
        else { return nil }
        return String(data: data, encoding: .utf8)
    }

    static func unsupportedExpr(_ raw: String) -> [String: Any] {
        expr([
            "Unsupported": [
                "raw": raw,
                "reason": "advanced builder expression",
            ]
        ])
    }
}

private enum BaseQueryFilterYAML: Equatable {
    case stmt(String)
    case all([BaseQueryFilterYAML])
    case any([BaseQueryFilterYAML])
    case none([BaseQueryFilterYAML])

    var expressionText: String? {
        if case .stmt(let expression) = self {
            return expression
        }
        return nil
    }

    func yamlValue(indent: Int) -> String {
        switch self {
        case .stmt(let expression):
            return "\(String(repeating: " ", count: indent))\(BaseQueryYAML.scalar(expression))"
        case .all(let children):
            return collectionYAML(key: "and", children: children, indent: indent)
        case .any(let children):
            return collectionYAML(key: "or", children: children, indent: indent)
        case .none(let children):
            return collectionYAML(key: "not", children: children, indent: indent)
        }
    }

    private func collectionYAML(
        key: String,
        children: [BaseQueryFilterYAML],
        indent: Int
    ) -> String {
        let spaces = String(repeating: " ", count: indent)
        var lines = ["\(spaces)\(key):"]
        for child in children {
            switch child {
            case .stmt(let expression):
                lines.append("\(spaces)  - \(BaseQueryYAML.scalar(expression))")
            case .all, .any, .none:
                lines.append("\(spaces)  -")
                lines.append(child.yamlValue(indent: indent + 4))
            }
        }
        return lines.joined(separator: "\n")
    }
}

private enum BaseQueryYAML {
    static func scalar(_ value: String) -> String {
        let allowed = CharacterSet(
            charactersIn: "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.")
        let lower = value.lowercased()
        if !value.isEmpty,
            value.unicodeScalars.allSatisfy({ allowed.contains($0) }),
            !["true", "false", "null", "~"].contains(lower)
        {
            return value
        }
        return quoteString(value)
    }

    static func quoteString(_ value: String) -> String {
        let escaped = value
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\n", with: "\\n")
            .replacingOccurrences(of: "\r", with: "\\r")
            .replacingOccurrences(of: "\t", with: "\\t")
            .replacingOccurrences(of: "\"", with: "\\\"")
        return "\"\(escaped)\""
    }
}

private enum BaseQueryExpressionSource {
    static func render(json: String?) -> String? {
        guard let object = BaseQueryJSON.decode(json) as? [String: Any] else { return nil }
        return render(expr: object)
    }

    private static func render(expr: [String: Any]) -> String? {
        guard let kind = expr["kind"] as? [String: Any],
            let name = kind.keys.first,
            let payload = kind[name]
        else { return nil }
        switch name {
        case "Lit":
            return renderLiteral(payload)
        case "Prop":
            guard let property = payload as? [String: Any],
                let decoded = BaseQueryBuilderDraft.decodePropertyRef(property)
            else { return nil }
            return decoded.sourceExpression
        case "Binary":
            guard let object = payload as? [String: Any],
                let op = object["op"] as? String,
                let lhs = object["lhs"] as? [String: Any],
                let rhs = object["rhs"] as? [String: Any],
                let lhsText = render(expr: lhs),
                let rhsText = render(expr: rhs)
            else { return nil }
            return "\(lhsText) \(sourceBinaryOperator(op)) \(rhsText)"
        case "Call":
            return renderCall(payload)
        case "Field":
            guard let object = payload as? [String: Any],
                let base = object["base"] as? [String: Any],
                let name = object["name"] as? String,
                let baseText = render(expr: base)
            else { return nil }
            return "\(baseText).\(name)"
        case "Unsupported":
            guard let object = payload as? [String: Any] else { return nil }
            return object["raw"] as? String
        default:
            return nil
        }
    }

    private static func renderLiteral(_ payload: Any) -> String? {
        guard let object = payload as? [String: Any],
            let name = object.keys.first,
            let value = object[name]
        else { return nil }
        switch name {
        case "String":
            return BaseQueryYAML.quoteString(value as? String ?? "")
        case "Number":
            if let number = value as? NSNumber { return number.stringValue }
            return nil
        case "Bool":
            return (value as? Bool) == true ? "true" : "false"
        default:
            return nil
        }
    }

    private static func renderCall(_ payload: Any) -> String? {
        guard let object = payload as? [String: Any],
            let callee = object["callee"] as? [String: Any],
            let args = object["args"] as? [Any]
        else { return nil }
        let renderedArgs = args.compactMap { ($0 as? [String: Any]).flatMap(render(expr:)) }
        if let global = callee["Global"] as? String {
            return "\(sourceFunctionName(global))(\(renderedArgs.joined(separator: ", ")))"
        }
        if let method = callee["Method"] as? [String: Any],
            let receiver = method["receiver"] as? [String: Any],
            let receiverText = render(expr: receiver),
            let name = method["name"] as? String
        {
            return "\(receiverText).\(sourceFunctionName(name))(\(renderedArgs.joined(separator: ", ")))"
        }
        return nil
    }

    private static func sourceBinaryOperator(_ name: String) -> String {
        switch name {
        case "Eq": return "=="
        case "Ne": return "!="
        case "Gt": return ">"
        case "Gte": return ">="
        case "Lt": return "<"
        case "Lte": return "<="
        case "Add": return "+"
        case "Sub": return "-"
        case "Mul": return "*"
        case "Div": return "/"
        case "Mod": return "%"
        case "And": return "&&"
        case "Or": return "||"
        default: return name
        }
    }

    private static func sourceFunctionName(_ name: String) -> String {
        guard let first = name.first else { return name }
        return first.lowercased() + name.dropFirst()
    }
}

private extension String {
    func stripPrefix(_ prefix: String) -> String? {
        guard hasPrefix(prefix) else { return nil }
        return String(dropFirst(prefix.count))
    }
}

private extension BaseQueryFileField {
    init?(serdeName: String) {
        switch serdeName {
        case "InDegree":
            self = .inDegree
        case "OutDegree":
            self = .outDegree
        default:
            let raw = serdeName.prefix(1).lowercased() + String(serdeName.dropFirst())
            self.init(rawValue: raw)
        }
    }
}

private extension BaseQueryTaskField {
    init?(serdeName: String) {
        let raw = serdeName.prefix(1).lowercased() + String(serdeName.dropFirst())
        self.init(rawValue: raw)
    }
}
