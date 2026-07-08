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
        switch self {
        case .note(let name):
            return BaseQueryJSON.expr(["Prop": ["Note": name]])
        case .file(let field):
            return BaseQueryJSON.expr(["Prop": ["File": field.serdeName]])
        case .formula(let name):
            return BaseQueryJSON.expr(["Prop": ["Formula": name]])
        case .task(let field):
            return BaseQueryJSON.expr(["Prop": ["TaskField": field.serdeName]])
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

struct BaseQueryBuilderDraft: Equatable {
    var source: BaseQuerySource = .allNotes
    var combinator: BaseQueryCombinator = .all
    var rows: [BaseQueryBuilderRow] = []

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
    }

    var conditionsListAccessibilityValue: String {
        combinator.accessibilityValue
    }

    func queryJSON() throws -> String {
        let root: [String: Any] = [
            "source": source.querySourceJSON,
            "row_source": source.rowSourceJSON,
            "filters": filterJSON ?? NSNull(),
            "formulas": [],
            "custom_summaries": [],
            "group_by": NSNull(),
            "sort": [],
            "columns": defaultColumns,
            "summaries": [],
            "limit": NSNull(),
            "view": ["Table": ["fallback_from": NSNull()]],
        ]
        let data = try JSONSerialization.data(withJSONObject: root, options: [.sortedKeys])
        guard let json = String(data: data, encoding: .utf8) else {
            throw BaseQueryBuilderError.cannotEncodeQuery
        }
        return json
    }

    private var filterJSON: Any? {
        guard !rows.isEmpty else { return nil }
        if rows.count == 1, combinator == .all {
            return rows[0].filterNodeJSON
        }
        return [combinator.filterNodeName: rows.map(\.filterNodeJSON)]
    }

    private var defaultColumns: [[String: Any]] {
        if source == .tasks {
            return [
                ["id": "task.text", "display_name": NSNull()],
                ["id": "task.file", "display_name": NSNull()],
            ]
        }
        return [["id": "file.name", "display_name": NSNull()]]
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

    init(draft: BaseQueryBuilderDraft = BaseQueryBuilderDraft()) {
        self.draft = draft
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

    var conditionsListAccessibilityValue: String {
        draft.conditionsListAccessibilityValue
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
        }
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
