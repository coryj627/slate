// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Persistence layer for `MathPrefs` + `CodePrefs` (and any future
/// user preference structs).
///
/// **Why explicit JSON over `@AppStorage`** (#224 acceptance):
/// `@AppStorage` works for `RawRepresentable` values and primitives,
/// but the FFI enum types (`MathSpeechStyle`, `MathVerbosity`,
/// `BrailleCode`) aren't `RawRepresentable<String>` by default and
/// uniffi can't add the conformance without regenerating. JSON +
/// `Codable` works uniformly, fails gracefully on schema drift
/// (decode-error → defaults), and is testable in isolation.
///
/// All keys namespaced under `slate.prefs.` so a UserDefaults dump
/// is identifiable.
final class PreferencesStore {
    static let mathKey = "slate.prefs.math"
    static let codeKey = "slate.prefs.code"
    static let canvasKey = "slate.prefs.canvas"
    static let baseQueriesKey = "slate.prefs.baseQueries"

    private let defaults: UserDefaults

    /// Inject `UserDefaults` for tests; production uses `.standard`.
    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    // MARK: - Math

    func loadMathPrefs() -> MathPrefs {
        decode(MathPrefs.self, key: Self.mathKey) ?? MathPrefs()
    }

    func saveMathPrefs(_ prefs: MathPrefs) {
        encode(prefs, key: Self.mathKey)
    }

    // MARK: - Code

    func loadCodePrefs() -> CodePrefs {
        decode(CodePrefs.self, key: Self.codeKey) ?? CodePrefs()
    }

    func saveCodePrefs(_ prefs: CodePrefs) {
        encode(prefs, key: Self.codeKey)
    }

    // MARK: - Canvas (Milestone T, #518)

    func loadCanvasPrefs() -> CanvasPrefs {
        decode(CanvasPrefs.self, key: Self.canvasKey) ?? CanvasPrefs()
    }

    func saveCanvasPrefs(_ prefs: CanvasPrefs) {
        encode(prefs, key: Self.canvasKey)
    }

    // MARK: - Base Queries (Milestone N, #709)

    func loadBaseQueryPrefs() -> BaseQueryPrefs {
        decode(BaseQueryPrefs.self, key: Self.baseQueriesKey) ?? BaseQueryPrefs()
    }

    func saveBaseQueryPrefs(_ prefs: BaseQueryPrefs) {
        encode(prefs, key: Self.baseQueriesKey)
    }

    // MARK: - History (Milestone O-5, #543)

    /// Bare-bool key (spec-pinned name): the "Show changes since last
    /// open" toggle. Default OFF — the section (and its `mark_opened`
    /// writes) are opt-in.
    static let historyShowChangesSinceOpenKey =
        "slate.prefs.historyShowChangesSinceOpen"

    func loadHistoryShowChangesSinceOpen() -> Bool {
        defaults.bool(forKey: Self.historyShowChangesSinceOpenKey)
    }

    func saveHistoryShowChangesSinceOpen(_ enabled: Bool) {
        defaults.set(enabled, forKey: Self.historyShowChangesSinceOpenKey)
    }

    // MARK: - Internals

    private func decode<T: Codable>(_ type: T.Type, key: String) -> T? {
        guard let data = defaults.data(forKey: key) else {
            return nil
        }
        // Invalid JSON or schema drift falls back to nil; the caller
        // substitutes defaults. We deliberately swallow the error
        // rather than log — a corrupt preferences blob from a
        // previous app version shouldn't surface as a user-visible
        // error message; defaults are the right "graceful
        // recovery" behaviour. Audit-issue territory if we ever
        // ship telemetry: emit a one-time signal that prefs
        // schema drift happened, so we know to update the
        // migration story.
        return try? JSONDecoder().decode(type, from: data)
    }

    private func encode<T: Codable>(_ value: T, key: String) {
        if let data = try? JSONEncoder().encode(value) {
            defaults.set(data, forKey: key)
        }
    }
}

struct BaseQueryPrefs: Codable, Equatable {
    var pinnedSavedQueryIDs: [String] = []
}
