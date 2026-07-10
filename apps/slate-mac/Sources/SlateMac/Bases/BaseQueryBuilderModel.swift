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

struct EditingSavedQuery: Equatable {
    var id: String
    var name: String
    var description: String?
}

struct EditingBaseView: Equatable {
    let source: BaseDocumentSource
    let viewIndex: UInt32

    var previewThisPath: String? { source.filePath }
}

enum BaseQuerySource: Hashable {
    case allNotes
    case folder(String)
    case tag(String)
    case recent(days: Int)
    case linked(fromPath: String)
    case tasks
    case unsupported(label: String, queryJSON: String)

    static func == (lhs: BaseQuerySource, rhs: BaseQuerySource) -> Bool {
        switch (lhs, rhs) {
        case (.allNotes, .allNotes), (.tasks, .tasks):
            return true
        case (.folder(let lhs), .folder(let rhs)),
            (.tag(let lhs), .tag(let rhs)),
            (.linked(let lhs), .linked(let rhs)):
            return BaseExactIdentity.matches(lhs, rhs)
        case (.recent(let lhs), .recent(let rhs)):
            return lhs == rhs
        case (.unsupported(let lhsLabel, let lhsJSON),
            .unsupported(let rhsLabel, let rhsJSON)):
            return BaseExactIdentity.matches(lhsLabel, rhsLabel)
                && BaseExactIdentity.matches(lhsJSON, rhsJSON)
        default:
            return false
        }
    }

    func hash(into hasher: inout Hasher) {
        switch self {
        case .allNotes:
            hasher.combine(0)
        case .folder(let path):
            hasher.combine(1)
            BaseExactIdentity.hash(path, into: &hasher)
        case .tag(let tag):
            hasher.combine(2)
            BaseExactIdentity.hash(tag, into: &hasher)
        case .recent(let days):
            hasher.combine(3)
            hasher.combine(days)
        case .linked(let path):
            hasher.combine(4)
            BaseExactIdentity.hash(path, into: &hasher)
        case .tasks:
            hasher.combine(5)
        case .unsupported(let label, let queryJSON):
            hasher.combine(6)
            BaseExactIdentity.hash(label, into: &hasher)
            BaseExactIdentity.hash(queryJSON, into: &hasher)
        }
    }

    var accessibilityLabel: String {
        switch self {
        case .allNotes: return "All notes"
        case .folder(let path): return "Folder \(path)"
        case .tag(let tag): return "Tag \(tag)"
        case .recent(let days): return "Recently edited, \(days) days"
        case .linked(let path): return "Linked from \(path)"
        case .tasks: return "Tasks"
        case .unsupported(let label, _): return "\(label), read only"
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
        case .unsupported(let label, let queryJSON):
            return BaseQueryJSON.decode(queryJSON) ?? ["Unsupported": label]
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
            return .stmt("file.mtime >= now() - duration(\"\(max(days, 1))d\")")
        case .linked(let path):
            return .stmt("link(\(BaseQueryYAML.quoteString(path))).linksTo(file.file)")
        case .unsupported:
            return nil
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

    static func == (lhs: BaseQueryProperty, rhs: BaseQueryProperty) -> Bool {
        switch (lhs, rhs) {
        case (.note(let lhs), .note(let rhs)),
            (.formula(let lhs), .formula(let rhs)):
            return BaseExactIdentity.matches(lhs, rhs)
        case (.file(let lhs), .file(let rhs)):
            return lhs == rhs
        case (.task(let lhs), .task(let rhs)):
            return lhs == rhs
        default:
            return false
        }
    }

    func hash(into hasher: inout Hasher) {
        switch self {
        case .note(let name):
            hasher.combine(0)
            BaseExactIdentity.hash(name, into: &hasher)
        case .file(let field):
            hasher.combine(1)
            hasher.combine(field)
        case .formula(let name):
            hasher.combine(2)
            BaseExactIdentity.hash(name, into: &hasher)
        case .task(let field):
            hasher.combine(3)
            hasher.combine(field)
        }
    }

    var exactIdentityKey: String {
        switch self {
        case .note(let name):
            return BaseExactIdentity.key(prefix: "note-property", components: [name])
        case .file(let field):
            return BaseExactIdentity.key(prefix: "file-property", components: [field.rawValue])
        case .formula(let name):
            return BaseExactIdentity.key(prefix: "formula-property", components: [name])
        case .task(let field):
            return BaseExactIdentity.key(prefix: "task-property", components: [field.rawValue])
        }
    }

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

/// Stable editor/operator families. Indexed note-property kinds travel beside a
/// picker choice rather than becoming part of `BaseQueryProperty` identity, so
/// a rescan cannot invalidate selections or encoded property references.
enum BaseQueryValueKind: Hashable {
    case text
    case number
    case boolean
    case date
    case datetime
    case list
    case tagList
    case wikilink
    case file
    case object
    case mixedOrUnknown
    case formula

    init(indexedKinds: [String]) {
        guard indexedKinds.count == 1 else {
            self = .mixedOrUnknown
            return
        }
        switch indexedKinds[0] {
        case "text": self = .text
        case "number": self = .number
        case "boolean": self = .boolean
        case "date": self = .date
        case "datetime": self = .datetime
        case "list": self = .list
        case "tag_list": self = .tagList
        case "wikilink": self = .wikilink
        default: self = .mixedOrUnknown
        }
    }

    var defaultValue: BaseQueryValue {
        switch self {
        case .text, .object, .mixedOrUnknown, .formula: return .text("")
        case .number: return .number(0)
        case .boolean: return .bool(false)
        case .date, .datetime: return .absoluteDate("1970-01-01")
        case .list, .tagList: return .tokens([])
        case .wikilink: return .wikilink("")
        case .file: return .file("")
        }
    }
}

enum BaseQueryEditorDescriptor: Hashable {
    case text
    case number
    case toggle
    case tokenList
    case link
    case dateAndRelative

    static func forKind(_ kind: BaseQueryValueKind) -> BaseQueryEditorDescriptor {
        switch kind {
        case .number: return .number
        case .boolean: return .toggle
        case .list, .tagList: return .tokenList
        case .wikilink, .file: return .link
        case .date, .datetime: return .dateAndRelative
        case .text, .object, .mixedOrUnknown, .formula: return .text
        }
    }
}

struct BaseQueryPropertyChoice: Hashable, Identifiable {
    let property: BaseQueryProperty
    let kind: BaseQueryValueKind

    var id: BaseQueryProperty { property }

    init(property: BaseQueryProperty, kind: BaseQueryValueKind) {
        self.property = property
        self.kind = kind
    }

    init(summary: PropertyKeySummary) {
        property = .note(summary.key)
        kind = BaseQueryValueKind(indexedKinds: summary.valueKinds)
    }

    static let fileChoices: [BaseQueryPropertyChoice] = BaseQueryFileField.allCases.map {
        BaseQueryPropertyChoice(property: .file($0), kind: $0.valueKind)
    }

    static let taskChoices: [BaseQueryPropertyChoice] = BaseQueryTaskField.allCases.map {
        BaseQueryPropertyChoice(property: .task($0), kind: $0.valueKind)
    }
}

private extension BaseQueryFileField {
    var valueKind: BaseQueryValueKind {
        switch self {
        case .name, .basename, .path, .folder, .ext: return .text
        case .size, .inDegree, .outDegree: return .number
        case .properties, .tasks: return .object
        case .tags: return .tagList
        case .aliases, .links, .backlinks, .embeds: return .list
        case .file: return .file
        case .ctime, .mtime: return .datetime
        }
    }
}

private extension BaseQueryTaskField {
    var valueKind: BaseQueryValueKind {
        switch self {
        case .text, .status: return .text
        case .completed: return .boolean
        case .due, .scheduled: return .date
        case .priority: return .number
        case .file: return .file
        }
    }
}

extension BaseQueryProperty {
    var staticValueKind: BaseQueryValueKind? {
        switch self {
        case .file(let field): return field.valueKind
        case .task(let field): return field.valueKind
        case .formula: return .formula
        case .note: return nil
        }
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

    static func options(for kind: BaseQueryValueKind) -> [BaseQueryOperator] {
        let equalityAndEmpty: [BaseQueryOperator] = [.equals, .notEquals, .isEmpty]
        switch kind {
        case .text:
            return [.equals, .notEquals, .contains, .startsWith, .endsWith, .isEmpty]
        case .number, .date, .datetime:
            return [
                .equals, .notEquals, .greaterThan, .greaterThanOrEqual,
                .lessThan, .lessThanOrEqual, .isEmpty,
            ]
        case .file:
            return [.equals, .notEquals, .hasTag, .hasLink, .matches, .isEmpty]
        case .boolean, .wikilink, .object, .mixedOrUnknown, .formula:
            return equalityAndEmpty
        case .list, .tagList:
            return [.equals, .notEquals, .contains, .isEmpty]
        }
    }
}

enum BaseQueryValue: Hashable {
    case text(String)
    case number(Double)
    case bool(Bool)
    case absoluteDate(String)
    case relativeDays(Int)
    case tokens([String])
    case wikilink(String)
    case file(String)

    static func == (lhs: BaseQueryValue, rhs: BaseQueryValue) -> Bool {
        switch (lhs, rhs) {
        case (.text(let lhs), .text(let rhs)),
            (.absoluteDate(let lhs), .absoluteDate(let rhs)),
            (.wikilink(let lhs), .wikilink(let rhs)),
            (.file(let lhs), .file(let rhs)):
            return BaseExactIdentity.matches(lhs, rhs)
        case (.number(let lhs), .number(let rhs)):
            return lhs == rhs
        case (.bool(let lhs), .bool(let rhs)):
            return lhs == rhs
        case (.relativeDays(let lhs), .relativeDays(let rhs)):
            return lhs == rhs
        case (.tokens(let lhs), .tokens(let rhs)):
            return lhs.count == rhs.count
                && zip(lhs, rhs).allSatisfy {
                    BaseExactIdentity.matches($0.0, $0.1)
                }
        default:
            return false
        }
    }

    func hash(into hasher: inout Hasher) {
        switch self {
        case .text(let value):
            hasher.combine(0)
            BaseExactIdentity.hash(value, into: &hasher)
        case .number(let value):
            hasher.combine(1)
            hasher.combine(value)
        case .bool(let value):
            hasher.combine(2)
            hasher.combine(value)
        case .absoluteDate(let value):
            hasher.combine(3)
            BaseExactIdentity.hash(value, into: &hasher)
        case .relativeDays(let value):
            hasher.combine(4)
            hasher.combine(value)
        case .tokens(let values):
            hasher.combine(5)
            hasher.combine(values.count)
            values.forEach { BaseExactIdentity.hash($0, into: &hasher) }
        case .wikilink(let value):
            hasher.combine(6)
            BaseExactIdentity.hash(value, into: &hasher)
        case .file(let value):
            hasher.combine(7)
            BaseExactIdentity.hash(value, into: &hasher)
        }
    }

    var accessibilityName: String {
        editingText
    }

    var editingText: String {
        switch self {
        case .text(let text): return text
        case .number(let number): return Self.numberFormatter.string(from: NSNumber(value: number))
            ?? String(number)
        case .bool(let value): return value ? "true" : "false"
        case .absoluteDate(let value): return value
        case .relativeDays(let days): return "\(days) days ago"
        case .tokens(let values): return values.joined(separator: ", ")
        case .wikilink(let target): return target
        case .file(let path): return path
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
        case .absoluteDate:
            return .absoluteDate(text.trimmingCharacters(in: .whitespacesAndNewlines))
        case .relativeDays:
            let digits = text.filter(\.isNumber)
            return Int(digits).map { .relativeDays(max($0, 1)) } ?? self
        case .tokens:
            return .tokens(Self.parseTokens(text))
        case .wikilink:
            return .wikilink(text.trimmingCharacters(in: .whitespacesAndNewlines))
        case .file:
            return .file(text.trimmingCharacters(in: .whitespacesAndNewlines))
        }
    }

    func replacingEditingText(
        _ text: String,
        preferredKind: BaseQueryValueKind?
    ) -> BaseQueryValue {
        guard let preferredKind else {
            return replacingEditingText(text)
        }
        switch preferredKind {
        case .text, .object, .mixedOrUnknown, .formula:
            return .text(text)
        case .number:
            return Self.parseNumber(text).map(BaseQueryValue.number) ?? preferredKind.defaultValue
        case .boolean:
            return Self.parseBool(text).map(BaseQueryValue.bool) ?? preferredKind.defaultValue
        case .date, .datetime:
            return .absoluteDate(text.trimmingCharacters(in: .whitespacesAndNewlines))
        case .list, .tagList:
            return .tokens(Self.parseTokens(text))
        case .wikilink:
            return .wikilink(text.trimmingCharacters(in: .whitespacesAndNewlines))
        case .file:
            return .file(text.trimmingCharacters(in: .whitespacesAndNewlines))
        }
    }

    fileprivate func coerced(preferredKind: BaseQueryValueKind?) -> BaseQueryValue {
        guard let preferredKind else { return self }
        switch (preferredKind, self) {
        case (.number, .number(let number)):
            return number.isFinite ? self : preferredKind.defaultValue
        case (.boolean, .bool):
            return self
        case (.date, .absoluteDate), (.date, .relativeDays),
            (.datetime, .absoluteDate), (.datetime, .relativeDays),
            (.list, .tokens), (.tagList, .tokens), (.wikilink, .wikilink),
            (.file, .file), (.text, .text), (.object, .text),
            (.mixedOrUnknown, _), (.formula, _):
            return self
        case (.number, _):
            return Self.parseNumber(editingText).map(BaseQueryValue.number) ?? preferredKind.defaultValue
        case (.boolean, _):
            return Self.parseBool(editingText).map(BaseQueryValue.bool) ?? preferredKind.defaultValue
        case (.text, _):
            return .text(editingText)
        case (.date, _), (.datetime, _):
            return .absoluteDate(editingText)
        case (.list, _), (.tagList, _):
            return .tokens(Self.parseTokens(editingText))
        case (.wikilink, _):
            return .wikilink(editingText)
        case (.file, _):
            return .file(editingText)
        case (.object, _):
            return .text(editingText)
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
        case .absoluteDate(let value):
            return Self.globalCall(name: "Date", arguments: [.text(value)])
        case .relativeDays(let days):
            return BaseQueryJSON.expr([
                "Binary": [
                    "op": "Sub",
                    "lhs": Self.globalCall(name: "Now", arguments: []),
                    "rhs": Self.globalCall(
                        name: "Duration",
                        arguments: [.text("\(max(days, 1))d")]),
                ]
            ])
        case .tokens(let values):
            return BaseQueryJSON.expr([
                "Lit": ["List": values.map { BaseQueryValue.text($0).expressionJSON }]
            ])
        case .wikilink(let target):
            return Self.globalCall(name: "Link", arguments: [.text(target)])
        case .file(let path):
            return Self.globalCall(name: "File", arguments: [.text(path)])
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
        case .absoluteDate(let value):
            return "date(\(BaseQueryYAML.quoteString(value)))"
        case .relativeDays(let days):
            return "now() - duration(\(BaseQueryYAML.quoteString("\(max(days, 1))d")))"
        case .tokens(let values):
            return "[\(values.map(BaseQueryYAML.quoteString).joined(separator: ", "))]"
        case .wikilink(let target):
            return "link(\(BaseQueryYAML.quoteString(target)))"
        case .file(let path):
            return "file(\(BaseQueryYAML.quoteString(path)))"
        }
    }

    private static func globalCall(name: String, arguments: [BaseQueryValue]) -> [String: Any] {
        BaseQueryJSON.expr([
            "Call": [
                "callee": ["Global": name],
                "args": arguments.map(\.expressionJSON),
            ]
        ])
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

    private static func parseTokens(_ text: String) -> [String] {
        text.split(separator: ",", omittingEmptySubsequences: true)
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
    }
}

struct BaseQueryCondition: Hashable {
    var property: BaseQueryProperty
    var op: BaseQueryOperator
    var value: BaseQueryValue
    fileprivate var preservedFilterJSON: String?

    init(
        property: BaseQueryProperty = .note("status"),
        operator conditionOperator: BaseQueryOperator = .equals,
        value: BaseQueryValue = .text("active"),
        preservedFilterJSON: String? = nil
    ) {
        self.property = property
        self.op = conditionOperator
        self.value = value
        self.preservedFilterJSON = preservedFilterJSON
    }

    var accessibilityPhrase: String {
        if op == .isEmpty {
            return "\(property.accessibilityName) \(op.accessibilityName)"
        }
        return "\(property.accessibilityName) \(op.accessibilityName) \(value.accessibilityName)"
    }

    mutating func replaceValueEditingText(_ text: String) {
        value = value.replacingEditingText(text, preferredKind: preferredValueKind)
        preservedFilterJSON = nil
    }

    mutating func retarget(to choice: BaseQueryPropertyChoice) {
        property = choice.property
        if !BaseQueryOperator.options(for: choice.kind).contains(op) {
            op = .equals
        }
        let operandKind = op == .isEmpty
            ? choice.kind
            : Self.inputKind(for: choice.kind, operator: op)
        value = value.coerced(preferredKind: operandKind)
        preservedFilterJSON = nil
    }

    mutating func setOperator(
        _ newOperator: BaseQueryOperator,
        kind: BaseQueryValueKind? = nil
    ) {
        op = newOperator
        if let kind, newOperator != .isEmpty {
            value = value.coerced(preferredKind: Self.inputKind(for: kind, operator: newOperator))
        }
        preservedFilterJSON = nil
    }

    mutating func setValue(_ newValue: BaseQueryValue) {
        value = newValue
        preservedFilterJSON = nil
    }

    func isCompatible(with kind: BaseQueryValueKind) -> Bool {
        guard BaseQueryOperator.options(for: kind).contains(op) else { return false }
        guard op != .isEmpty else { return true }
        switch Self.inputKind(for: kind, operator: op) {
        case .text:
            if case .text = value { return true }
        case .number:
            if case .number = value { return true }
        case .boolean:
            if case .bool = value { return true }
        case .date, .datetime:
            if case .absoluteDate = value { return true }
            if case .relativeDays = value { return true }
        case .list, .tagList:
            if case .tokens = value { return true }
        case .wikilink:
            if case .wikilink = value { return true }
        case .file:
            if case .file = value { return true }
        case .object:
            if case .text = value { return true }
        case .mixedOrUnknown, .formula:
            return true
        }
        return false
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
        guard let kind = property.staticValueKind else { return nil }
        return Self.inputKind(for: kind, operator: op)
    }

    static func inputKind(
        for kind: BaseQueryValueKind,
        operator conditionOperator: BaseQueryOperator
    ) -> BaseQueryValueKind {
        switch (kind, conditionOperator) {
        case (.file, .hasTag), (.file, .matches),
            (.list, .contains), (.tagList, .contains):
            return .text
        case (.file, .hasLink):
            return .file
        default:
            return kind
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

    fileprivate var semanticFilterNodeJSON: Any {
        switch self {
        case .condition(let condition):
            return BaseQueryJSON.decode(condition.preservedFilterJSON)
                ?? condition.filterNodeJSON
        case .group(let group):
            return [group.combinator.filterNodeName: group.rows.map(\.semanticFilterNodeJSON)]
        case .advanced(_, let filterJSON):
            return BaseQueryJSON.decode(filterJSON) ?? filterNodeJSON
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

enum BaseQueryViewType: Hashable, Identifiable {
    case table
    case list
    case unsupported(label: String, queryJSON: String)

    static func == (lhs: BaseQueryViewType, rhs: BaseQueryViewType) -> Bool {
        switch (lhs, rhs) {
        case (.table, .table), (.list, .list):
            return true
        case (.unsupported(let lhsLabel, let lhsJSON),
            .unsupported(let rhsLabel, let rhsJSON)):
            return BaseExactIdentity.matches(lhsLabel, rhsLabel)
                && BaseExactIdentity.matches(lhsJSON, rhsJSON)
        default:
            return false
        }
    }

    func hash(into hasher: inout Hasher) {
        switch self {
        case .table:
            hasher.combine(0)
        case .list:
            hasher.combine(1)
        case .unsupported(let label, let queryJSON):
            hasher.combine(2)
            BaseExactIdentity.hash(label, into: &hasher)
            BaseExactIdentity.hash(queryJSON, into: &hasher)
        }
    }

    var id: String {
        switch self {
        case .table: return "table"
        case .list: return "list"
        case .unsupported(let label, let queryJSON):
            return BaseExactIdentity.key(
                prefix: "unsupported-builder-view", components: [label, queryJSON])
        }
    }

    var title: String {
        switch self {
        case .table: return "Table"
        case .list: return "List"
        case .unsupported(let label, _): return "\(label) (read only)"
        }
    }

    fileprivate var queryViewJSON: Any {
        switch self {
        case .table: return ["Table": ["fallback_from": NSNull()]]
        case .list: return ["List": ["fallback_from": NSNull()]]
        case .unsupported(_, let queryJSON):
            return BaseQueryJSON.decode(queryJSON)
                ?? ["Table": ["fallback_from": ["Other": "unsupported"]]]
        }
    }

    fileprivate var yamlType: String? {
        switch self {
        case .table: return "table"
        case .list: return "list"
        case .unsupported: return nil
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

    static func == (lhs: BaseQuerySortKey, rhs: BaseQuerySortKey) -> Bool {
        lhs.property == rhs.property
            && BaseExactIdentity.matches(lhs.expressionJSON, rhs.expressionJSON)
            && BaseExactIdentity.matches(lhs.expressionLabel, rhs.expressionLabel)
            && lhs.ascending == rhs.ascending
    }

    func hash(into hasher: inout Hasher) {
        hasher.combine(property)
        BaseExactIdentity.hash(expressionJSON, into: &hasher)
        BaseExactIdentity.hash(expressionLabel, into: &hasher)
        hasher.combine(ascending)
    }

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

    fileprivate func referencesFormula(named name: String) -> Bool {
        if property == .formula(name)
            || BaseExactIdentity.matches(expressionLabel, "formula.\(name)")
        {
            return true
        }
        guard let expr = BaseQueryJSON.decode(expressionJSON) else { return false }
        return Self.jsonValue(expr, containsFormulaReference: name)
    }

    private static func jsonValue(_ value: Any, containsFormulaReference name: String) -> Bool {
        if let object = value as? [String: Any] {
            if let formula = object["Formula"] as? String,
                BaseExactIdentity.matches(formula, name)
            {
                return true
            }
            return object.values.contains { jsonValue($0, containsFormulaReference: name) }
        }
        if let items = value as? [Any] {
            return items.contains { jsonValue($0, containsFormulaReference: name) }
        }
        return false
    }

    fileprivate var slateYAML: String {
        [
            "- expr: \(BaseQueryYAML.scalar(expressionLabel))",
            "  direction: \(ascending ? "asc" : "desc")",
        ].joined(separator: "\n")
    }
}

struct BaseQueryColumn: Hashable {
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

    static func == (lhs: BaseQueryColumn, rhs: BaseQueryColumn) -> Bool {
        BaseExactIdentity.matches(lhs.id, rhs.id)
            && lhs.property == rhs.property
            && BaseExactIdentity.matches(lhs.displayName, rhs.displayName)
    }

    func hash(into hasher: inout Hasher) {
        BaseExactIdentity.hash(id, into: &hasher)
        hasher.combine(property)
        BaseExactIdentity.hash(displayName, into: &hasher)
    }
}

struct BaseQueryFormula: Hashable, Identifiable {
    var name: String
    var expression: String
    var expressionJSON: String

    var id: String {
        BaseExactIdentity.key(prefix: "builder-formula", components: [name])
    }

    static func == (lhs: BaseQueryFormula, rhs: BaseQueryFormula) -> Bool {
        BaseExactIdentity.matches(lhs.name, rhs.name)
            && BaseExactIdentity.matches(lhs.expression, rhs.expression)
            && BaseExactIdentity.matches(lhs.expressionJSON, rhs.expressionJSON)
    }

    func hash(into hasher: inout Hasher) {
        BaseExactIdentity.hash(name, into: &hasher)
        BaseExactIdentity.hash(expression, into: &hasher)
        BaseExactIdentity.hash(expressionJSON, into: &hasher)
    }

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

enum BaseFormulaCompletion {
    /// Pinned Bases v1 global evaluator inventory. `random` is intentionally
    /// absent because Core parses it only to emit the v1 excluded-function error.
    static let names = [
        "average", "date", "duration", "escapeHTML", "file", "html", "icon", "if",
        "image", "link", "list", "max", "min", "now", "number", "object", "string",
        "sum", "today",
    ]

    static func inserting(_ name: String, into expression: String) -> String {
        var start = expression.endIndex
        while start > expression.startIndex {
            let previous = expression.index(before: start)
            let character = expression[previous]
            guard character.isLetter || character.isNumber || character == "_" else { break }
            start = previous
        }
        var result = expression
        result.replaceSubrange(start..<result.endIndex, with: "\(name)()")
        return result
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
    private var editableSource: BaseQuerySource = .allNotes
    var source: BaseQuerySource {
        get { editableSource }
        set {
            if editableSource != newValue {
                opaqueTaskQuerySourceJSON = nil
            }
            editableSource = newValue
        }
    }
    var combinator: BaseQueryCombinator = .all
    var rows: [BaseQueryBuilderRow] = []
    var sortKeys: [BaseQuerySortKey] = []
    private var editableGroupBy: BaseQueryGroupBy?
    var groupBy: BaseQueryGroupBy? {
        get { editableGroupBy }
        set {
            editableGroupBy = newValue
            opaqueGroupByJSON = nil
        }
    }
    var columns: [BaseQueryColumn] = []
    var formulas: [BaseQueryFormula] = []
    var viewType: BaseQueryViewType = .table

    /// Canonical JSON for the complete effective SlateQuery root. Unknown and
    /// non-builder-owned facets stay opaque so opening the builder cannot erase
    /// data introduced by Core or a newer Slate version.
    private var opaqueEffectiveRootJSON: String?

    /// The base-wide filter extracted only from Core's guaranteed
    /// `And([global, local])` composition. The editable fields above remain the
    /// view-local draft while `queryJSON()` recomposes the effective query.
    private var inheritedFilterJSON: String?

    /// `row_source: Tasks` is independent from `source`. Preserve a non-All
    /// query source while the UI exposes Tasks as the row-source choice.
    private var opaqueTaskQuerySourceJSON: String?

    /// A newer or plugin-provided group-by shape that this builder cannot edit.
    /// It survives no-op/filter-only edits and is discarded only when the user
    /// explicitly chooses or clears a supported group.
    private var opaqueGroupByJSON: String?

    init() {}

    init(queryJSON: String) throws {
        let root = try Self.queryRoot(from: queryJSON)
        opaqueEffectiveRootJSON = BaseQueryJSON.canonicalJSONString(root)

        let rowSource = root["row_source"] as? String
        let decodedSource = Self.decodeSource(root["source"])
        if rowSource == "Tasks" {
            editableSource = .tasks
            if decodedSource != .allNotes {
                opaqueTaskQuerySourceJSON = Self.canonicalQuerySourceJSON(root["source"])
            }
        } else {
            editableSource = decodedSource
        }

        let decoded = Self.decodeFilterRows(
            root["filters"],
            allowSourceExtraction: decodedSource == .allNotes && rowSource != "Tasks")
        if source == .allNotes, let canonical = decoded.source {
            source = canonical
        }
        combinator = decoded.combinator
        rows = decoded.rows
        sortKeys = Self.decodeSortKeys(root["sort"])
        editableGroupBy = Self.decodeGroupBy(root["group_by"])
        if editableGroupBy == nil,
            let value = root["group_by"],
            !(value is NSNull)
        {
            opaqueGroupByJSON = BaseQueryJSON.canonicalJSONString(value)
        }
        columns = Self.decodeColumns(root["columns"])
        formulas = Self.decodeFormulas(root["formulas"])
        viewType = Self.decodeViewType(root["view"])
    }

    init(effectiveQueryJSON: String, localQueryJSON: String) throws {
        let effectiveRoot = try Self.queryRoot(from: effectiveQueryJSON)
        let localRoot = try Self.queryRoot(from: localQueryJSON)
        try self.init(queryJSON: localQueryJSON)

        opaqueEffectiveRootJSON = BaseQueryJSON.canonicalJSONString(effectiveRoot)
        if source == .tasks {
            let effectiveSource = Self.decodeSource(effectiveRoot["source"])
            opaqueTaskQuerySourceJSON = effectiveSource == .allNotes
                ? nil
                : Self.canonicalQuerySourceJSON(effectiveRoot["source"])
        }
        inheritedFilterJSON = try Self.inheritedFilterJSON(
            effective: effectiveRoot["filters"],
            local: localRoot["filters"])
    }

    var conditionsListAccessibilityValue: String {
        combinator.accessibilityValue
    }

    func queryJSON() throws -> String {
        var root = (BaseQueryJSON.decode(opaqueEffectiveRootJSON) as? [String: Any]) ?? [:]
        root["source"] = encodedQuerySourceJSON
        root["row_source"] = source.rowSourceJSON
        root["filters"] = effectiveFilterJSON ?? NSNull()
        root["formulas"] = formulas.map(\.queryJSON)
        root["group_by"] = groupBy?.queryJSON
            ?? BaseQueryJSON.decode(opaqueGroupByJSON)
            ?? NSNull()
        root["sort"] = sortKeys.map(\.queryJSON)
        root["columns"] = encodedColumns
        root["view"] = viewType.queryViewJSON

        // Fresh drafts still need the complete current schema. Parsed drafts
        // retain these values verbatim from `opaqueEffectiveRootJSON`.
        if root["custom_summaries"] == nil { root["custom_summaries"] = [] }
        if root["summaries"] == nil { root["summaries"] = [] }
        if root["limit"] == nil { root["limit"] = NSNull() }

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
        let filtersChanged = previous.map {
            source != $0.source
                || !BaseExactIdentity.matches(
                    localFilterSemanticSignature, $0.localFilterSemanticSignature)
        } ?? true
        if filtersChanged {
            if let viewFilterYAML = try encodedViewFilterYAML() {
                edits.append(.setViewFilters(view: view, yaml: viewFilterYAML))
            } else if previous?.hasLocalFilter == true {
                edits.append(.removeViewKey(view: view, key: "filters"))
            }
        }
        if previous?.viewType != viewType || previous == nil {
            guard let yamlType = viewType.yamlType else {
                throw BaseQueryBuilderError.cannotEncodeQuery
            }
            edits.append(.setViewKey(view: view, key: "type", value: yamlType))
        }
        if source == .tasks {
            if previous?.source != .tasks || previous == nil {
                edits.append(.setViewKey(view: view, key: "source", value: "tasks"))
            }
        } else if previous?.source == .tasks {
            edits.append(.removeViewKey(view: view, key: "source"))
        }
        if previous == nil
            || !BaseExactIdentity.matches(previous?.groupBySignature, groupBySignature)
        {
            if let groupBy {
                edits.append(.setViewKey(view: view, key: "groupBy", value: groupBy.yamlFragment))
            } else if opaqueGroupByJSON != nil {
                guard BaseExactIdentity.matches(
                    previous?.opaqueGroupByJSON, opaqueGroupByJSON)
                else {
                    throw BaseQueryBuilderError.cannotEncodeQuery
                }
            } else if previous?.groupBySignature != nil {
                edits.append(.removeViewKey(view: view, key: "groupBy"))
            }
        }
        if previous?.effectiveOrderIdentityKeys != effectiveOrderIdentityKeys || previous == nil {
            edits.append(.setViewKey(view: view, key: "order", value: orderYAMLFragment))
        }
        if let previous {
            let currentFormulaNames = Set(formulas.map(\.id))
            for formula in previous.formulas where !currentFormulaNames.contains(formula.id) {
                edits.append(.removeFormula(name: formula.name))
            }
        }
        let previousFormulaJSON = previous.map { Self.formulaJSONMap(for: $0.formulas) } ?? [:]
        for formula in formulas
        where previous == nil
            || !BaseExactIdentity.matches(
                previousFormulaJSON[formula.id], formula.expressionJSON)
        {
            edits.append(.setFormula(name: formula.name, expression: formula.expression))
        }
        let previousDisplayNames = previous.map { displayNameMap(for: $0.columns) } ?? [:]
        for column in columns {
            let displayName = normalizedDisplayName(column.displayName)
            if previous == nil {
                if let displayName {
                    edits.append(.setDisplayName(property: column.id, displayName: displayName))
                }
            } else if !BaseExactIdentity.matches(
                previousDisplayNames[Self.columnIdentityKey(column.id)], displayName)
            {
                edits.append(.setDisplayName(property: column.id, displayName: displayName))
            }
        }
        if previous?.sortKeys != sortKeys,
            !sortKeys.isEmpty || previous?.sortKeys.isEmpty == false
        {
            edits.append(.setSlateSort(view: view, yaml: slateSortYAML))
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

    private var encodedQuerySourceJSON: Any {
        if source == .tasks,
            let opaqueTaskQuerySourceJSON,
            let preserved = BaseQueryJSON.decode(opaqueTaskQuerySourceJSON)
        {
            return preserved
        }
        return source.querySourceJSON
    }

    private var localFilterSemanticSignature: String? {
        guard !rows.isEmpty else { return nil }
        let node: Any
        if rows.count == 1, combinator == .all {
            node = rows[0].semanticFilterNodeJSON
        } else {
            node = [combinator.filterNodeName: rows.map(\.semanticFilterNodeJSON)]
        }
        return BaseQueryJSON.canonicalJSONString(node)
    }

    private var effectiveFilterJSON: Any? {
        let inherited = BaseQueryJSON.decode(inheritedFilterJSON)
        switch (inherited, filterJSON) {
        case (let inherited?, let local?):
            return ["And": [inherited, local]]
        case (let inherited?, nil):
            return inherited
        case (nil, let local?):
            return local
        case (nil, nil):
            return nil
        }
    }

    private var encodedColumns: [[String: Any]] {
        let selected = columns.isEmpty ? defaultColumnSelections : columns
        return selected.map(\.queryJSON)
    }

    private var effectiveOrderIdentityKeys: [String] {
        (columns.isEmpty ? defaultColumnSelections : columns).map {
            Self.columnIdentityKey($0.id)
        }
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

    private var hasLocalFilter: Bool {
        source.filterYAML != nil || !rows.isEmpty
    }

    private var groupBySignature: String? {
        if let groupBy {
            return BaseQueryJSON.canonicalJSONString(groupBy.queryJSON)
        }
        return opaqueGroupByJSON
    }

    private func encodedFilterYAML() throws -> String? {
        var filters: [BaseQueryFilterYAML] = []
        if let sourceFilter = source.filterYAML {
            filters.append(sourceFilter)
        }
        let rowFilters = rows.compactMap(Self.filterYAML(for:))
        guard rowFilters.count == rows.count else {
            throw BaseQueryBuilderError.cannotEncodeQuery
        }
        filters.append(contentsOf: rowFilters)
        guard !filters.isEmpty else { return nil }
        if filters.count == 1 {
            return filters[0].yamlValue(indent: 0)
        }
        return BaseQueryFilterYAML.all(filters).yamlValue(indent: 0)
    }

    private func encodedViewFilterYAML() throws -> String? {
        guard let filterYAML = try encodedFilterYAML() else { return nil }
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

    private var slateSortYAML: String? {
        guard !sortKeys.isEmpty else { return nil }
        return sortKeys.map(\.slateYAML).joined(separator: "\n")
    }

    private func normalizedDisplayName(_ value: String?) -> String? {
        let trimmed = value?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return trimmed.isEmpty ? nil : trimmed
    }

    private func displayNameMap(for columns: [BaseQueryColumn]) -> [String: String] {
        var names: [String: String] = [:]
        for column in columns {
            names[Self.columnIdentityKey(column.id)] = normalizedDisplayName(column.displayName)
        }
        return names
    }

    private static func columnIdentityKey(_ id: String) -> String {
        BaseExactIdentity.key(prefix: "builder-column", components: [id])
    }

    private static func formulaJSONMap(for formulas: [BaseQueryFormula]) -> [String: String] {
        var map: [String: String] = [:]
        for formula in formulas {
            map[formula.id] = formula.expressionJSON
        }
        return map
    }

    fileprivate static func filterYAML(for row: BaseQueryBuilderRow) -> BaseQueryFilterYAML? {
        switch row {
        case .condition(let condition):
            return .stmt(condition.expressionSource)
        case .group(let group):
            let children = group.rows.compactMap(filterYAML(for:))
            guard !children.isEmpty, children.count == group.rows.count else { return nil }
            switch group.combinator {
            case .all: return .all(children)
            case .any: return .any(children)
            case .none: return .none(children)
            }
        case .advanced(let rawExpression, let filterJSON):
            if let filterJSON {
                return filterYAML(fromPreservedFilterJSON: filterJSON)
            }
            return .stmt(rawExpression)
        }
    }

    private static func filterYAML(
        fromPreservedFilterJSON filterJSON: String
    ) -> BaseQueryFilterYAML? {
        guard let node = BaseQueryJSON.decode(filterJSON) as? [String: Any] else { return nil }
        return filterYAML(fromPreservedNode: node)
    }

    private static func filterYAML(
        fromPreservedNode node: [String: Any]
    ) -> BaseQueryFilterYAML? {
        guard node.count == 1 else { return nil }
        if let expression = node["Stmt"] as? [String: Any],
            let expressionJSON = BaseQueryJSON.canonicalJSONString(expression),
            let source = BaseQueryExpressionSource.render(json: expressionJSON)
        {
            return .stmt(source)
        }
        for (key, combinator) in [
            ("And", BaseQueryCombinator.all),
            ("Or", BaseQueryCombinator.any),
            ("Not", BaseQueryCombinator.none),
        ] {
            guard let nodes = node[key] as? [Any] else { continue }
            let children = nodes.compactMap { child -> BaseQueryFilterYAML? in
                guard let child = child as? [String: Any] else { return nil }
                return filterYAML(fromPreservedNode: child)
            }
            guard !children.isEmpty, children.count == nodes.count else { return nil }
            switch combinator {
            case .all: return .all(children)
            case .any: return .any(children)
            case .none: return .none(children)
            }
        }
        return nil
    }

    private static func decodeSource(_ value: Any?) -> BaseQuerySource {
        if let text = value as? String {
            if text == "All" { return .allNotes }
            return unsupportedSource(value, label: "Unsupported source: \(text)")
        }
        guard let object = value as? [String: Any] else {
            return unsupportedSource(value, label: "Unsupported source")
        }
        if object.count == 1, let folder = object["Folder"] as? String {
            return .folder(folder)
        }
        if object.count == 1, let tag = object["Tag"] as? String {
            return .tag(tag)
        }
        if let recent = object["Recent"] as? [String: Any],
            object.count == 1,
            Set(recent.keys) == ["days"],
            let days = recent["days"] as? Int,
            days > 0
        {
            return .recent(days: days)
        }
        if let linked = object["Linked"] as? [String: Any],
            object.count == 1,
            Set(linked.keys) == ["from_path", "depth"],
            let fromPath = linked["from_path"] as? String,
            let depth = linked["depth"] as? Int,
            depth == 1
        {
            return .linked(fromPath: fromPath)
        }
        let label: String
        if let reason = object["Unsupported"] as? String {
            label = "Unsupported source: \(reason)"
        } else {
            label = "Unsupported source"
        }
        return unsupportedSource(object, label: label)
    }

    private static func unsupportedSource(_ value: Any?, label: String) -> BaseQuerySource {
        let queryJSON = value.flatMap(BaseQueryJSON.canonicalJSONString)
            ?? BaseQueryJSON.canonicalJSONString(["Unsupported": label])
            ?? #"{"Unsupported":"unsupported source"}"#
        return .unsupported(label: label, queryJSON: queryJSON)
    }

    private static func queryRoot(from queryJSON: String) throws -> [String: Any] {
        guard
            let data = queryJSON.data(using: .utf8),
            var root = try JSONSerialization.jsonObject(with: data) as? [String: Any]
        else { throw BaseQueryBuilderError.invalidQueryJSON }
        if root["v"] != nil, let query = root["query"] as? [String: Any] {
            root = query
        }
        return root
    }

    private static func inheritedFilterJSON(effective: Any?, local: Any?) throws -> String? {
        let effectiveJSON = canonicalFilterJSON(effective)
        let localJSON = canonicalFilterJSON(local)

        switch (effectiveJSON, localJSON) {
        case (nil, nil):
            return nil
        case (let effectiveJSON?, nil):
            return effectiveJSON
        case (nil, .some):
            throw BaseQueryBuilderError.invalidQueryJSON
        case (let effectiveJSON?, let localJSON?) where effectiveJSON == localJSON:
            return nil
        case (_, let localJSON?):
            guard let effectiveObject = effective as? [String: Any],
                let nodes = effectiveObject["And"] as? [Any],
                nodes.count == 2,
                canonicalFilterJSON(nodes[1]) == localJSON,
                let inherited = canonicalFilterJSON(nodes[0])
            else { throw BaseQueryBuilderError.invalidQueryJSON }
            return inherited
        }
    }

    private static func canonicalFilterJSON(_ value: Any?) -> String? {
        guard let value, !(value is NSNull) else { return nil }
        return BaseQueryJSON.canonicalJSONString(value)
    }

    private static func canonicalQuerySourceJSON(_ value: Any?) -> String? {
        guard let value, !(value is NSNull) else { return nil }
        return BaseQueryJSON.canonicalJSONString(value)
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
        guard let object = value as? [String: Any],
            let queryJSON = BaseQueryJSON.canonicalJSONString(object)
        else { return .table }
        if let list = object["List"] as? [String: Any],
            list["fallback_from"] == nil || list["fallback_from"] is NSNull
        {
            return .list
        }
        if let table = object["Table"] as? [String: Any] {
            let fallback = table["fallback_from"]
            if fallback == nil || fallback is NSNull {
                return .table
            }
            return .unsupported(
                label: unsupportedViewLabel(fallback),
                queryJSON: queryJSON)
        }
        return .unsupported(label: "Unsupported view", queryJSON: queryJSON)
    }

    private static func unsupportedViewLabel(_ value: Any?) -> String {
        if let name = value as? String { return name }
        if let object = value as? [String: Any],
            let name = object["Other"] as? String
        {
            return name
        }
        return "Unsupported view"
    }

    private static func decodeFilterRows(
        _ value: Any?,
        allowSourceExtraction: Bool = true
    ) -> (source: BaseQuerySource?, combinator: BaseQueryCombinator, rows: [BaseQueryBuilderRow]) {
        guard !(value is NSNull), let value else { return (nil, .all, []) }
        if let object = value as? [String: Any] {
            if let nodes = object["And"] as? [Any] {
                return decodeRows(
                    nodes: nodes,
                    combinator: .all,
                    allowSourceExtraction: allowSourceExtraction,
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
        if let row = decodeRow(
            value,
            allowSourceExtraction: allowSourceExtraction,
            depth: 0)
        {
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
        for (index, node) in nodes.enumerated() {
            guard
                let decoded = decodeRow(
                    node,
                    allowSourceExtraction: allowSourceExtraction && index == 0 && source == nil,
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
            if let source = decodeCanonicalSourceExpression(expr) {
                if allowSourceExtraction {
                    return (source, advancedRow(value, fallback: source.accessibilityLabel))
                }
                if var condition = decodeConditionExpression(expr) {
                    condition.preservedFilterJSON = canonicalJSONString(value)
                    return (nil, .condition(condition))
                }
                return (nil, advancedRow(value, fallback: source.accessibilityLabel))
            }
            if var condition = decodeConditionExpression(expr) {
                condition.preservedFilterJSON = canonicalJSONString(value)
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
            binary["op"] as? String == "Gte",
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
            let value = decodeConditionValue(rhs),
            let conditionOperator = decodeBinaryOperator(op)
        {
            let condition = BaseQueryCondition(
                property: property,
                operator: conditionOperator,
                value: value)
            guard property.staticValueKind.map(condition.isCompatible(with:)) ?? true else {
                return nil
            }
            return condition
        }
        if let call = methodCall(expr),
            let property = decodeProperty(call.receiver),
            let conditionOperator = decodeMethodOperator(call.name),
            let value = decodeMethodConditionValue(
                receiver: property,
                operator: conditionOperator,
                args: call.args)
        {
            let condition = BaseQueryCondition(
                property: property,
                operator: conditionOperator,
                value: value)
            guard property.staticValueKind.map(condition.isCompatible(with:)) ?? true else {
                return nil
            }
            return condition
        }
        return nil
    }

    private static func decodeConditionValue(_ expr: [String: Any]) -> BaseQueryValue? {
        if let literal = decodeLiteral(expr) { return literal }
        if let args = globalCallArgs(expr, name: "Date"),
            args.count == 1,
            let value = stringLiteral(args[0])
        {
            return .absoluteDate(value)
        }
        if let args = globalCallArgs(expr, name: "Link"),
            args.count == 1,
            let value = stringLiteral(args[0])
        {
            return .wikilink(value)
        }
        if let args = globalCallArgs(expr, name: "File"),
            args.count == 1,
            let value = stringLiteral(args[0])
        {
            return .file(value)
        }
        if let days = decodeRelativeDaysExpression(expr) {
            return .relativeDays(days)
        }
        return nil
    }

    private static func decodeRelativeDaysExpression(_ expr: [String: Any]) -> Int? {
        guard let duration = kindPayload("Binary", in: expr),
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
        return days
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
        receiver property: BaseQueryProperty,
        operator conditionOperator: BaseQueryOperator,
        args: [Any]
    ) -> BaseQueryValue? {
        switch conditionOperator {
        case .isEmpty:
            return args.isEmpty ? .text("") : nil
        case .contains, .startsWith, .endsWith, .hasTag, .hasLink, .matches:
            guard args.count == 1,
                let expr = args[0] as? [String: Any],
                let value = decodeConditionValue(expr)
            else { return nil }
            switch conditionOperator {
            case .hasTag, .matches:
                guard property.staticValueKind == .file else { return nil }
                guard case .text = value else { return nil }
            case .hasLink:
                guard property.staticValueKind == .file else { return nil }
                guard case .file = value else { return nil }
            case .contains, .startsWith, .endsWith:
                break
            case .isEmpty, .equals, .notEquals, .greaterThan, .greaterThanOrEqual,
                .lessThan, .lessThanOrEqual:
                return nil
            }
            if let kind = property.staticValueKind {
                let condition = BaseQueryCondition(
                    property: property,
                    operator: conditionOperator,
                    value: value)
                guard condition.isCompatible(with: kind) else { return nil }
            }
            return value
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
        if let items = lit["List"] as? [Any] {
            let values = items.compactMap(stringLiteral)
            guard values.count == items.count else { return nil }
            return .tokens(values)
        }
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
    let editingSavedQuery: EditingSavedQuery?
    let editingBaseView: EditingBaseView?
    private var comparisonBaseline: BaseQueryBuilderDraft

    init(
        draft: BaseQueryBuilderDraft = BaseQueryBuilderDraft(),
        editingSavedQuery: EditingSavedQuery? = nil,
        editingBaseView: EditingBaseView? = nil
    ) {
        self.draft = draft
        self.editingSavedQuery = editingSavedQuery
        self.editingBaseView = editingBaseView
        self.comparisonBaseline = draft
    }

    var previewThisPath: String? { editingBaseView?.previewThisPath }

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
        try draft.baseEditsForView(view, replacing: comparisonBaseline)
    }

    func rebaseAfterSuccessfulSave() {
        comparisonBaseline = draft
    }

    func applyPropertyChoices(_ choices: [BaseQueryPropertyChoice]) {
        var kinds: [BaseQueryProperty: BaseQueryValueKind] = [:]
        for choice in choices {
            kinds[choice.property] = choice.kind
        }
        draft.rows = draft.rows.map { Self.failClosedRow($0, kinds: kinds) }
    }

    func columnIndex(for property: BaseQueryProperty) -> Int? {
        draft.columns.firstIndex {
            BaseExactIdentity.matches($0.id, property.sourceExpression)
        }
    }

    private static func failClosedRow(
        _ row: BaseQueryBuilderRow,
        kinds: [BaseQueryProperty: BaseQueryValueKind]
    ) -> BaseQueryBuilderRow {
        switch row {
        case .condition(let condition):
            let kind = kinds[condition.property]
                ?? condition.property.staticValueKind
                ?? .mixedOrUnknown
            guard !condition.isCompatible(with: kind) else { return row }
            let filterJSON = condition.preservedFilterJSON
                ?? BaseQueryJSON.canonicalJSONString(condition.filterNodeJSON)
            let raw = BaseQueryExpressionSource.render(json: filterJSON)
                ?? condition.expressionSource
            return .advanced(rawExpression: raw, filterJSON: filterJSON)
        case .group(var group):
            group.rows = group.rows.map { failClosedRow($0, kinds: kinds) }
            return .group(group)
        case .advanced:
            return row
        }
    }

    func removeFormula(named name: String) {
        let columnID = "formula.\(name)"
        draft.formulas.removeAll { BaseExactIdentity.matches($0.name, name) }
        draft.columns.removeAll { BaseExactIdentity.matches($0.id, columnID) }
        draft.sortKeys.removeAll { sortKey in
            sortKey.referencesFormula(named: name)
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
        return try? JSONSerialization.jsonObject(with: data, options: [.fragmentsAllowed])
    }

    static func canonicalJSONString(_ value: Any) -> String? {
        guard let data = try? JSONSerialization.data(
            withJSONObject: value,
            options: [.sortedKeys, .fragmentsAllowed])
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
            kind.count == 1,
            let name = kind.keys.first,
            let payload = kind[name]
        else { return nil }
        switch name {
        case "Lit":
            return renderLiteral(payload)
        case "Prop":
            return renderPropertyRef(payload)
        case "Index":
            guard let object = payload as? [String: Any],
                let base = object["base"] as? [String: Any],
                let index = object["index"] as? [String: Any],
                let baseText = render(expr: base),
                let indexText = render(expr: index)
            else { return nil }
            return "\(baseText)[\(indexText)]"
        case "Unary":
            guard let object = payload as? [String: Any],
                let op = object["op"] as? String,
                let rhs = object["rhs"] as? [String: Any],
                let rhsText = render(expr: rhs)
            else { return nil }
            switch op {
            case "Not": return "!\(rhsText)"
            case "Neg": return "-\(rhsText)"
            default: return nil
            }
        case "Binary":
            guard let object = payload as? [String: Any],
                let op = object["op"] as? String,
                let lhs = object["lhs"] as? [String: Any],
                let rhs = object["rhs"] as? [String: Any],
                let lhsText = render(expr: lhs),
                let rhsText = render(expr: rhs),
                let operatorText = sourceBinaryOperator(op)
            else { return nil }
            return "(\(lhsText) \(operatorText) \(rhsText))"
        case "Call":
            return renderCall(payload)
        case "ListExpr":
            return renderListExpression(payload)
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
            object.count == 1,
            let name = object.keys.first,
            let value = object[name]
        else { return nil }
        switch name {
        case "String":
            guard let value = value as? String else { return nil }
            return expressionStringLiteral(value)
        case "Number":
            if let number = value as? NSNumber { return number.stringValue }
            return nil
        case "Bool":
            guard let value = value as? Bool else { return nil }
            return value ? "true" : "false"
        case "List":
            guard let values = value as? [Any] else { return nil }
            let rendered = values.compactMap {
                ($0 as? [String: Any]).flatMap(render(expr:))
            }
            guard rendered.count == values.count else { return nil }
            return "[\(rendered.joined(separator: ", "))]"
        case "Object":
            guard let values = value as? [Any] else { return nil }
            let rendered = values.compactMap { item -> String? in
                guard let pair = item as? [Any],
                    pair.count == 2,
                    let key = pair[0] as? String,
                    let expression = pair[1] as? [String: Any],
                    let expressionText = render(expr: expression)
                else { return nil }
                return "\(expressionStringLiteral(key)): \(expressionText)"
            }
            guard rendered.count == values.count else { return nil }
            return "{\(rendered.joined(separator: ", "))}"
        case "Regex":
            guard let regex = value as? [String: Any],
                Set(regex.keys) == ["pattern", "flags"],
                let pattern = regex["pattern"] as? String,
                let flags = regex["flags"] as? String
            else { return nil }
            return "/\(pattern)/\(flags)"
        default:
            return nil
        }
    }

    private static func renderPropertyRef(_ payload: Any) -> String? {
        if let name = payload as? String {
            switch name {
            case "This": return "this"
            case "ImplicitValue": return "value"
            case "ImplicitIndex": return "index"
            case "ImplicitAcc": return "acc"
            default: return nil
            }
        }
        guard let property = payload as? [String: Any], property.count == 1 else { return nil }
        if let name = property["Note"] as? String {
            return notePropertySource(name)
        }
        if let fieldName = property["File"] as? String,
            let field = BaseQueryFileField(serdeName: fieldName)
        {
            return "file.\(field.sourceName)"
        }
        if let name = property["Formula"] as? String {
            return "formula.\(name)"
        }
        if let fieldName = property["TaskField"] as? String,
            let field = BaseQueryTaskField(serdeName: fieldName)
        {
            return "task.\(field.sourceName)"
        }
        if let name = property["ThisNote"] as? String {
            return "this.\(notePropertySource(name))"
        }
        if let fieldName = property["ThisFile"] as? String,
            let field = BaseQueryFileField(serdeName: fieldName)
        {
            return "this.file.\(field.sourceName)"
        }
        return nil
    }

    private static func renderCall(_ payload: Any) -> String? {
        guard let object = payload as? [String: Any],
            let callee = object["callee"] as? [String: Any],
            let args = object["args"] as? [Any]
        else { return nil }
        let renderedArgs = args.compactMap { ($0 as? [String: Any]).flatMap(render(expr:)) }
        guard renderedArgs.count == args.count else { return nil }
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

    private static func renderListExpression(_ payload: Any) -> String? {
        guard let object = payload as? [String: Any],
            let base = object["base"] as? [String: Any],
            let kind = object["kind"] as? String,
            let body = object["body"] as? [String: Any],
            let baseText = render(expr: base),
            let bodyText = render(expr: body)
        else { return nil }
        let method: String
        switch kind {
        case "Filter": method = "filter"
        case "Map": method = "map"
        case "Reduce": method = "reduce"
        default: return nil
        }
        var arguments = [bodyText]
        if let initial = object["init"], !(initial is NSNull) {
            guard let initial = initial as? [String: Any],
                let initialText = render(expr: initial)
            else { return nil }
            arguments.append(initialText)
        }
        return "\(baseText).\(method)(\(arguments.joined(separator: ", ")))"
    }

    private static func sourceBinaryOperator(_ name: String) -> String? {
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
        default: return nil
        }
    }

    private static func sourceFunctionName(_ name: String) -> String {
        if name == "EscapeHtml" { return "escapeHTML" }
        guard let first = name.first else { return name }
        return first.lowercased() + name.dropFirst()
    }

    private static func notePropertySource(_ name: String) -> String {
        let safe = !name.isEmpty && name.utf8.allSatisfy {
            (48...57).contains($0)
                || (65...90).contains($0)
                || (97...122).contains($0)
                || $0 == 95
                || $0 == 45
        }
        return safe ? name : "note[\(expressionStringLiteral(name))]"
    }

    private static func expressionStringLiteral(_ value: String) -> String {
        BaseQueryJSON.canonicalJSONString(value) ?? BaseQueryYAML.quoteString(value)
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
