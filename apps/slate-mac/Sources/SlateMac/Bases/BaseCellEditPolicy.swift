// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

enum BaseCellEditPolicy {
    static func propertyKey(for column: BasesColumn) -> String? {
        if let key = column.id.stripPrefix("note."), !key.isEmpty {
            return key
        }
        guard !column.id.contains(".") else { return nil }
        guard column.role == .metadata || column.role == .primary else { return nil }
        return column.id.isEmpty ? nil : column.id
    }

    static func readOnlyHint(for column: BasesColumn) -> String {
        if column.id.hasPrefix("file.") {
            return "read-only: file metadata"
        }
        return "read-only: computed"
    }

    static func propertyValue(
        from draft: String,
        valueKind: String
    ) -> Result<PropertyValue, BaseCellEditValidationError> {
        let trimmed = draft.trimmingCharacters(in: .whitespacesAndNewlines)
        switch valueKind.lowercased() {
        case "number":
            if let value = Int64(trimmed) {
                return .success(PropertyValue.integer(value: value))
            }
            guard let value = Double(trimmed), value.isFinite else {
                return .failure(.init(message: "Must be a finite number."))
            }
            return .success(PropertyValue.float(value: value))
        case "integer":
            guard let value = Int64(trimmed) else {
                return .failure(.init(message: "Must be a whole number."))
            }
            return .success(PropertyValue.integer(value: value))
        case "float", "decimal":
            guard let value = Double(trimmed), value.isFinite else {
                return .failure(.init(message: "Must be a finite decimal number."))
            }
            return .success(PropertyValue.float(value: value))
        case "boolean", "bool", "checkbox":
            switch trimmed.lowercased() {
            case "true", "yes", "1":
                return .success(PropertyValue.boolean(value: true))
            case "false", "no", "0":
                return .success(PropertyValue.boolean(value: false))
            default:
                return .failure(.init(message: "Must be true or false."))
            }
        case "date":
            guard looksLikeDate(trimmed) else {
                return .failure(.init(message: "Date must be YYYY-MM-DD."))
            }
            return .success(PropertyValue.date(value: trimmed))
        case "datetime":
            return .success(PropertyValue.datetime(value: trimmed))
        case "wikilink", "link":
            return .success(PropertyValue.wikilink(target: wikilinkTarget(from: trimmed)))
        case "list", "tag_list":
            let items = draft
                .split(whereSeparator: { $0 == "," || $0 == "\n" })
                .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
                .filter { !$0.isEmpty }
            if valueKind.lowercased() == "tag_list" {
                return .success(PropertyValue.tagList(tags: items))
            }
            return .success(PropertyValue.list(items: items.map { PropertyValue.text(value: $0) }))
        default:
            return .success(PropertyValue.text(value: draft))
        }
    }

    static func displayValue(_ value: PropertyValue) -> String {
        switch value {
        case .text(let value), .date(let value), .datetime(let value):
            return value
        case .integer(let value):
            return "\(value)"
        case .float(let value):
            return String(format: "%g", value)
        case .boolean(let value):
            return value ? "true" : "false"
        case .wikilink(let target):
            return target
        case .list(let items):
            return items.map(displayValue).joined(separator: ", ")
        case .tagList(let tags):
            return tags.joined(separator: ", ")
        }
    }

    private static func looksLikeDate(_ value: String) -> Bool {
        guard value.count == 10 else { return false }
        let parts = value.split(separator: "-", omittingEmptySubsequences: false)
        return parts.count == 3
            && parts[0].count == 4
            && parts[1].count == 2
            && parts[2].count == 2
            && parts.allSatisfy { $0.allSatisfy(\.isNumber) }
    }

    private static func wikilinkTarget(from value: String) -> String {
        if value.hasPrefix("[["),
            value.hasSuffix("]]")
        {
            return String(value.dropFirst(2).dropLast(2))
        }
        return value
    }
}

struct BaseCellEditValidationError: Error, Equatable {
    let message: String
}

private extension String {
    func stripPrefix(_ prefix: String) -> String? {
        guard hasPrefix(prefix) else { return nil }
        return String(dropFirst(prefix.count))
    }
}
