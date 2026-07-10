// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Bases identifiers cross a Rust/filesystem boundary and are byte-exact.
/// Swift `String` equality is canonically equivalent, so it cannot be used for
/// paths, authored property IDs, view names, or saved-query references.
enum BaseExactIdentity {
    static func matches(_ lhs: String, _ rhs: String) -> Bool {
        lhs.utf8.elementsEqual(rhs.utf8)
    }

    static func matches(_ lhs: String?, _ rhs: String?) -> Bool {
        switch (lhs, rhs) {
        case (nil, nil): return true
        case (.some(let lhs), .some(let rhs)): return matches(lhs, rhs)
        default: return false
        }
    }

    static func hash(_ value: String, into hasher: inout Hasher) {
        hasher.combine(Array(value.utf8))
    }

    static func hash(_ value: String?, into hasher: inout Hasher) {
        guard let value else {
            hasher.combine(false)
            return
        }
        hasher.combine(true)
        hash(value, into: &hasher)
    }

    /// Injective ASCII identity suitable for SwiftUI IDs and String-keyed
    /// registries. Encoding UTF-8 bytes avoids Swift String's canonical-
    /// equivalence semantics at the exact-identity boundary.
    static func key(prefix: String, components: [String?]) -> String {
        let encoded = components.map { component in
            guard let component else { return "n" }
            let bytes = component.utf8.map { String(format: "%02x", $0) }.joined()
            return "s\(bytes)"
        }
        return ([prefix] + encoded).joined(separator: "|")
    }

    static func lessThan(_ lhs: String, _ rhs: String) -> Bool {
        lhs.utf8.lexicographicallyPrecedes(rhs.utf8)
    }

    /// Injective ASCII key for registries that are intentionally keyed by
    /// `String` for compatibility with the existing AppState surface.
    static func registryKey(prefix: String, value: String) -> String {
        key(prefix: prefix, components: [value])
    }
}
