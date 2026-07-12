// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// Tests for `PreferencesStore` — the JSON-via-UserDefaults
/// persistence layer for math + code prefs. Tests inject a non-
/// standard UserDefaults via `UserDefaults(suiteName:)` so the
/// system-wide defaults stay untouched.
final class PreferencesStoreTests: XCTestCase {

    private var defaults: UserDefaults!
    private var suiteName: String!

    override func setUp() {
        super.setUp()
        suiteName = "slate.prefs.tests.\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: suiteName)
    }

    override func tearDown() {
        defaults.removePersistentDomain(forName: suiteName)
        defaults = nil
        suiteName = nil
        super.tearDown()
    }

    // MARK: - Math round-trip

    func testMathPrefsRoundTripThroughUserDefaults() {
        let store = PreferencesStore(defaults: defaults)
        var prefs = MathPrefs()
        prefs.speechStyle = .mathSpeak
        prefs.verbosity = .verbose
        prefs.brailleCode = .ueb

        store.saveMathPrefs(prefs)

        let store2 = PreferencesStore(defaults: defaults)
        let loaded = store2.loadMathPrefs()
        XCTAssertEqual(loaded.speechStyle, .mathSpeak)
        XCTAssertEqual(loaded.verbosity, .verbose)
        XCTAssertEqual(loaded.brailleCode, .ueb)
    }

    func testLoadMathPrefsReturnsDefaultsWhenAbsent() {
        let store = PreferencesStore(defaults: defaults)
        let prefs = store.loadMathPrefs()
        XCTAssertEqual(prefs, MathPrefs()) // default-initialized
    }

    /// Corrupt JSON falls back to defaults rather than crashing.
    /// Schema drift / partial writes / future-version blobs all
    /// land in this branch.
    func testInvalidStoredJsonFallsBackToDefaults() {
        defaults.set(Data("not valid json".utf8), forKey: PreferencesStore.mathKey)
        let store = PreferencesStore(defaults: defaults)
        XCTAssertEqual(store.loadMathPrefs(), MathPrefs())
    }

    // MARK: - Code round-trip

    func testCodePrefsRoundTripThroughUserDefaults() {
        let store = PreferencesStore(defaults: defaults)
        var prefs = CodePrefs()
        prefs.verbosity = .preambleAllTokens

        store.saveCodePrefs(prefs)

        let store2 = PreferencesStore(defaults: defaults)
        let loaded = store2.loadCodePrefs()
        XCTAssertEqual(loaded.verbosity, .preambleAllTokens)
    }

    func testLoadCodePrefsReturnsDefaultsWhenAbsent() {
        let store = PreferencesStore(defaults: defaults)
        XCTAssertEqual(store.loadCodePrefs(), CodePrefs())
    }

    // MARK: - Base query pins

    func testBaseQueryPinsRoundTripThroughUserDefaults() {
        let store = PreferencesStore(defaults: defaults)
        let prefs = BaseQueryPrefs(pinnedSavedQueryIDs: ["sq-backlog", "sq-active"])

        store.saveBaseQueryPrefs(prefs)

        let store2 = PreferencesStore(defaults: defaults)
        XCTAssertEqual(store2.loadBaseQueryPrefs(), prefs)
    }

    // MARK: - Compaction-failure alert suppression (#881)

    func testSuppressCompactionFailureAlertDefaultsOffAndRoundTrips() {
        let store = PreferencesStore(defaults: defaults)
        XCTAssertFalse(
            store.loadSuppressCompactionFailureAlert(),
            "the alert shows until the user opts out")

        store.saveSuppressCompactionFailureAlert(true)
        let store2 = PreferencesStore(defaults: defaults)
        XCTAssertTrue(
            store2.loadSuppressCompactionFailureAlert(),
            "the 'Don't Show Again' choice persists across store instances")
    }

    // MARK: - Restore last vault on launch (#872)

    func testRestoreVaultOnLaunchDefaultsOnAndRoundTrips() {
        let store = PreferencesStore(defaults: defaults)
        XCTAssertTrue(
            store.loadRestoreVaultOnLaunch(),
            "default ON — a returning user lands back in their vault")

        store.saveRestoreVaultOnLaunch(false)
        let store2 = PreferencesStore(defaults: defaults)
        XCTAssertFalse(
            store2.loadRestoreVaultOnLaunch(),
            "the opt-out persists across store instances")

        store2.saveRestoreVaultOnLaunch(true)
        XCTAssertTrue(PreferencesStore(defaults: defaults).loadRestoreVaultOnLaunch())
    }

    // MARK: - Persistence tags

    /// Persistence tags are stable strings, independent of the
    /// user-facing `displayName`. Renaming "Short" → "Brief" in
    /// the UI must not migrate stored data — that's a separate
    /// migration story. Pin the wire format here.
    func testMathSpeechStylePersistenceTagsAreStable() throws {
        let clearSpeakEncoded = try JSONEncoder().encode(MathSpeechStyle.clearSpeak)
        XCTAssertEqual(String(data: clearSpeakEncoded, encoding: .utf8), "\"clearSpeak\"")
        let mathSpeakEncoded = try JSONEncoder().encode(MathSpeechStyle.mathSpeak)
        XCTAssertEqual(String(data: mathSpeakEncoded, encoding: .utf8), "\"mathSpeak\"")
    }

    func testMathVerbosityPersistenceTagsAreStable() throws {
        XCTAssertEqual(
            String(data: try JSONEncoder().encode(MathVerbosity.terse), encoding: .utf8),
            "\"terse\""
        )
        XCTAssertEqual(
            String(data: try JSONEncoder().encode(MathVerbosity.medium), encoding: .utf8),
            "\"medium\""
        )
        XCTAssertEqual(
            String(data: try JSONEncoder().encode(MathVerbosity.verbose), encoding: .utf8),
            "\"verbose\""
        )
    }

    func testBrailleCodePersistenceTagsAreStable() throws {
        XCTAssertEqual(
            String(data: try JSONEncoder().encode(BrailleCode.nemeth), encoding: .utf8),
            "\"nemeth\""
        )
        XCTAssertEqual(
            String(data: try JSONEncoder().encode(BrailleCode.ueb), encoding: .utf8),
            "\"ueb\""
        )
    }

    // MARK: - AppState integration

    /// AppState loads persisted prefs on init.
    @MainActor
    func testAppStateLoadsPersistedPrefsOnInit() throws {
        let store = PreferencesStore(defaults: defaults)
        var seeded = MathPrefs()
        seeded.speechStyle = .mathSpeak
        store.saveMathPrefs(seeded)

        let appState = AppState(
            recentsStore: RecentVaultsStore(fileURL: FileManager.default.temporaryDirectory.appendingPathComponent("\(suiteName!).recents.json")),
            externalOpener: { _ in true },
            preferencesStore: store
        )
        XCTAssertEqual(appState.mathPrefs.speechStyle, .mathSpeak)
    }

    /// Changing AppState.mathPrefs persists through the store.
    @MainActor
    func testAppStateMathPrefsChangePersistsToStore() throws {
        let store = PreferencesStore(defaults: defaults)
        let appState = AppState(
            recentsStore: RecentVaultsStore(fileURL: FileManager.default.temporaryDirectory.appendingPathComponent("\(suiteName!).recents.json")),
            externalOpener: { _ in true },
            preferencesStore: store
        )
        appState.mathPrefs.verbosity = .verbose

        // Re-load from a fresh store: the new value persisted.
        let store2 = PreferencesStore(defaults: defaults)
        XCTAssertEqual(store2.loadMathPrefs().verbosity, .verbose)
    }

    @MainActor
    func testAppStateCodePrefsChangePersistsToStore() throws {
        let store = PreferencesStore(defaults: defaults)
        let appState = AppState(
            recentsStore: RecentVaultsStore(fileURL: FileManager.default.temporaryDirectory.appendingPathComponent("\(suiteName!).recents.json")),
            externalOpener: { _ in true },
            preferencesStore: store
        )
        appState.codePrefs.verbosity = .preambleFirstLine

        let store2 = PreferencesStore(defaults: defaults)
        XCTAssertEqual(store2.loadCodePrefs().verbosity, .preambleFirstLine)
    }
}
