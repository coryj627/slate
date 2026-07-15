// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import CoreFoundation
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
    private static let sidebarIntegerEncodings: Set<String> = [
        "c", "C", "s", "S", "i", "I", "l", "L", "q", "Q",
    ]

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

    // MARK: - Editor text zoom (#848)

    /// Bare-double key (like the history bool above): the in-app
    /// editor text zoom factor, multiplied onto the body-text-style
    /// base size by `Tokens.Typography.monospacedBodyNSFont(scale:)`.
    /// 1.0 = no zoom. App-level (UserDefaults), NOT the CLI-shared
    /// vault `prefs.json` — zoom is a per-machine display preference,
    /// not vault content.
    static let editorTextScaleKey = "slate.prefs.editorTextScale"

    func loadEditorTextScale() -> Double {
        guard
            let value = defaults.object(forKey: Self.editorTextScaleKey) as? Double,
            value.isFinite, value > 0
        else { return 1.0 }
        // Clamp a hand-edited / corrupt default into a sane band; the
        // zoom ladder's nearest-rung snap normalizes further on the
        // first ⌘=/⌘− press.
        return min(max(value, 0.5), 3.0)
    }

    func saveEditorTextScale(_ scale: Double) {
        defaults.set(scale, forKey: Self.editorTextScaleKey)
    }

    // MARK: - Editor spell check (#855)

    /// Bare-bool key (the history-toggle pattern above): live spell
    /// checking (`NSTextView.isContinuousSpellCheckingEnabled`) in the
    /// note editor. Default OFF — Markdown source is full of tokens a
    /// spell checker red-squiggles (fences, wikilinks, frontmatter
    /// keys), so prose writers opt in explicitly via Edit ▸ Check
    /// Spelling While Typing. App-level (UserDefaults), not vault
    /// content — a per-machine writing preference like the zoom above.
    static let editorSpellCheckKey = "slate.prefs.editorSpellCheck"

    func loadEditorSpellCheck() -> Bool {
        defaults.bool(forKey: Self.editorSpellCheckKey)
    }

    func saveEditorSpellCheck(_ enabled: Bool) {
        defaults.set(enabled, forKey: Self.editorSpellCheckKey)
    }

    // MARK: - Compaction-failure alert suppression (#881)

    /// Bare-bool key (the editorSpellCheck pattern above): the
    /// alerts.md:36 "Don't Show Again" opt-out for the "History
    /// Compaction Failed" alert. Default OFF — the alert shows until the
    /// user suppresses it. App-level (UserDefaults), NOT the CLI-shared
    /// vault `prefs.json`: whether an alert interrupts is a per-machine
    /// UI preference, not vault content. When suppressed, the failure
    /// still reaches a polite AX announcement (o_spec §O-2 "never
    /// silent" — see `AppState.handleVaultEvent`).
    static let suppressCompactionFailureAlertKey =
        "slate.prefs.suppressCompactionFailureAlert"

    func loadSuppressCompactionFailureAlert() -> Bool {
        defaults.bool(forKey: Self.suppressCompactionFailureAlertKey)
    }

    func saveSuppressCompactionFailureAlert(_ suppressed: Bool) {
        defaults.set(suppressed, forKey: Self.suppressCompactionFailureAlertKey)
    }

    // MARK: - Restore last vault on launch (#872)

    /// Bare-bool key (the editorSpellCheck / compaction-suppress pattern
    /// above): reopen the most-recent vault automatically on launch
    /// (launching.md — "Restore previous state on restart … avoid making
    /// people retrace steps"; Obsidian / VS Code reopen the last
    /// workspace). Default **ON** — the whole point of #872 is that a
    /// returning user lands back in their vault. Holding ⌥ at launch is
    /// the transient escape hatch; this toggle is the persistent one.
    /// App-level (UserDefaults), NOT the CLI-shared vault `prefs.json`:
    /// which vault a machine reopens is a per-machine launch preference,
    /// not vault content.
    ///
    /// `defaults.bool(forKey:)` returns `false` for an absent key, which
    /// would invert the ON default, so read through `object(forKey:)` and
    /// fall back to `true` when unset (the `editorTextScale` object-read
    /// pattern above).
    static let restoreVaultOnLaunchKey = "slate.prefs.restoreVaultOnLaunch"

    func loadRestoreVaultOnLaunch() -> Bool {
        (defaults.object(forKey: Self.restoreVaultOnLaunchKey) as? Bool) ?? true
    }

    func saveRestoreVaultOnLaunch(_ enabled: Bool) {
        defaults.set(enabled, forKey: Self.restoreVaultOnLaunchKey)
    }

    // MARK: - Files sidebar presentation (Milestone FL, #653/#654)

    /// Load typed, device-local sidebar presentation preferences. Raw enum
    /// values are stable persistence tags; unknown future values fall back to
    /// today's defaults. The numeric preview count is clamped so a hand-edited
    /// defaults domain can never create an unbounded row.
    func loadSidebarPreferences() -> SidebarPreferences {
        let defaultsValue = SidebarPreferences()
        let dateSource = (defaults.object(forKey: SidebarPreferences.Keys.dateSource) as? String)
            .flatMap(SidebarPreferences.DateSource.init(rawValue:))
            ?? defaultsValue.dateSource
        let dateFormat = (defaults.object(forKey: SidebarPreferences.Keys.dateFormat) as? String)
            .flatMap(SidebarPreferences.DateFormat.init(rawValue:))
            ?? defaultsValue.dateFormat
        let density = (defaults.object(forKey: SidebarPreferences.Keys.density) as? String)
            .flatMap(SidebarPreferences.Density.init(rawValue:))
            ?? defaultsValue.density
        let previewLines = strictSidebarInteger(forKey: SidebarPreferences.Keys.previewLines)
            .map { min(max($0, 0), 3) }
            ?? defaultsValue.previewLines
        let showTaskCounts =
            strictSidebarBool(forKey: SidebarPreferences.Keys.showTaskCounts)
            ?? defaultsValue.showTaskCounts
        let showWordCount =
            strictSidebarBool(forKey: SidebarPreferences.Keys.showWordCount)
            ?? defaultsValue.showWordCount

        return SidebarPreferences(
            dateSource: dateSource,
            dateFormat: dateFormat,
            previewLines: previewLines,
            showTaskCounts: showTaskCounts,
            showWordCount: showWordCount,
            density: density
        )
    }

    func saveSidebarPreferences(_ preferences: SidebarPreferences) {
        defaults.set(
            preferences.dateSource.rawValue,
            forKey: SidebarPreferences.Keys.dateSource)
        defaults.set(
            preferences.dateFormat.rawValue,
            forKey: SidebarPreferences.Keys.dateFormat)
        defaults.set(
            min(max(preferences.previewLines, 0), 3),
            forKey: SidebarPreferences.Keys.previewLines)
        defaults.set(
            preferences.showTaskCounts,
            forKey: SidebarPreferences.Keys.showTaskCounts)
        defaults.set(
            preferences.showWordCount,
            forKey: SidebarPreferences.Keys.showWordCount)
        defaults.set(
            preferences.density.rawValue,
            forKey: SidebarPreferences.Keys.density)
    }

    /// UserDefaults bridges both integers and booleans through `NSNumber`, so
    /// Swift conditional casts alone are permissive enough to turn `true` into
    /// one preview line. Require an integer Objective-C encoding and explicitly
    /// exclude the singleton CFBoolean type before accepting a value.
    private func strictSidebarInteger(forKey key: String) -> Int? {
        guard
            let number = defaults.object(forKey: key) as? NSNumber,
            CFGetTypeID(number) != CFBooleanGetTypeID()
        else { return nil }

        guard Self.sidebarIntegerEncodings.contains(String(cString: number.objCType)) else {
            return nil
        }
        return Int(number.stringValue)
    }

    /// Accept only a persisted CFBoolean. Numeric zero/one values are not
    /// silently reinterpreted as user choices when a defaults domain drifts.
    private func strictSidebarBool(forKey key: String) -> Bool? {
        guard
            let number = defaults.object(forKey: key) as? NSNumber,
            CFGetTypeID(number) == CFBooleanGetTypeID()
        else { return nil }
        return number.boolValue
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
