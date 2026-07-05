// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// M-3 (#534): sync diagnostics leaf — the panel state matrix, the
/// assertive-announcement gates (through the `AnnouncementPosting`
/// seam), and the Presentation-Ready render/contrast checks
/// (m_spec §M-3; DoD per issue #534).
@MainActor
final class SyncDiagnosticsPanelTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-sync-diag-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    // MARK: - Fixtures

    /// Recording fake for the announcement seam — the global helper
    /// early-returns without `NSApp`, so the seam is what makes these
    /// gates assertable.
    private final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
        private(set) var posts: [(message: String, priority: AnnouncementPriority)] = []
        func post(_ message: String, priority: AnnouncementPriority) {
            posts.append((message, priority))
        }
    }

    private func makeAppState(announcer: AnnouncementPosting) -> AppState {
        AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json")),
            externalOpener: { _ in true },
            announcer: announcer
        )
    }

    /// A vault directory seeded by `plant`, opened through the real
    /// funnel (`openVault` → scan → post-scan sync-diagnostics load).
    private func openVault(
        named name: String,
        announcer: AnnouncementPosting,
        plant: (URL) throws -> Void
    ) async throws -> AppState {
        let state = makeAppState(announcer: announcer)
        try await reopenVault(named: name, in: state, plant: plant)
        return state
    }

    private func reopenVault(
        named name: String,
        in state: AppState,
        plant: (URL) throws -> Void = { _ in }
    ) async throws {
        let vault = tempDir.appendingPathComponent(name)
        try? FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        try plant(vault)
        state.openVault(at: vault)
        await state.scanTask?.value
    }

    private func plantLiveSync(_ vault: URL, dataJSON: String? = nil) throws {
        let plugin = vault.appendingPathComponent(".obsidian/plugins/obsidian-livesync")
        try FileManager.default.createDirectory(
            at: plugin, withIntermediateDirectories: true)
        try Data("{}".utf8).write(to: plugin.appendingPathComponent("manifest.json"))
        if let dataJSON {
            try Data(dataJSON.utf8).write(to: plugin.appendingPathComponent("data.json"))
        }
    }

    private func plantSyncthing(_ vault: URL) throws {
        try Data("".utf8).write(to: vault.appendingPathComponent(".stignore"))
    }

    private func plantGit(_ vault: URL) throws {
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent(".git"), withIntermediateDirectories: true)
    }

    // MARK: - Report loading (the post-scan funnel)

    func testVaultOpenLoadsReportFromPostScanContinuation() async throws {
        let announcer = RecordingAnnouncer()
        let state = try await openVault(named: "git-vault", announcer: announcer) {
            try self.plantGit($0)
        }
        let report = try XCTUnwrap(state.syncReport, "post-scan funnel loads the report")
        XCTAssertTrue(report.supported)
        XCTAssertEqual(report.providers.map(\.kind), [.git])
        XCTAssertEqual(report.providers.first?.displayName, "Git")
        XCTAssertEqual(report.providers.first?.riskLevel, .low)
        XCTAssertEqual(state.liveSyncConfig, .notPresent)
        XCTAssertNil(state.syncDiagnosticsError)
    }

    func testCleanVaultLoadsEmptyReport() async throws {
        let announcer = RecordingAnnouncer()
        let state = try await openVault(named: "clean-vault", announcer: announcer) {
            try Data("# hi".utf8).write(to: $0.appendingPathComponent("note.md"))
        }
        let report = try XCTUnwrap(state.syncReport)
        XCTAssertTrue(report.providers.isEmpty)
        XCTAssertEqual(report.audioSummary, "No sync systems detected.")
    }

    func testLiveSyncVaultLoadsParsedConfig() async throws {
        let announcer = RecordingAnnouncer()
        let state = try await openVault(named: "livesync-vault", announcer: announcer) {
            try self.plantLiveSync(
                $0,
                dataJSON: #"{"couchDB_DBNAME": "notes", "liveSync": true, "encrypt": false}"#)
        }
        let report = try XCTUnwrap(state.syncReport)
        XCTAssertEqual(report.providers.map(\.kind), [.liveSync])
        guard case .parsed(let config) = try XCTUnwrap(state.liveSyncConfig) else {
            return XCTFail("expected parsed LiveSync config, got \(String(describing: state.liveSyncConfig))")
        }
        XCTAssertEqual(config.database, "notes")
        XCTAssertEqual(config.liveSyncEnabled, true)
        XCTAssertEqual(config.endToEndEncryption, false)
        XCTAssertNil(config.serverHost)
    }

    func testVaultSwitchResetsReportBeforeReload() async throws {
        let announcer = RecordingAnnouncer()
        let state = try await openVault(named: "first-vault", announcer: announcer) {
            try self.plantGit($0)
        }
        XCTAssertNotNil(state.syncReport)
        // Immediately after openVault (before the new scan finishes)
        // the previous vault's report must be gone — no stale flash.
        let vault = tempDir.appendingPathComponent("second-vault")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        state.openVault(at: vault)
        XCTAssertNil(state.syncReport, "vault switch clears the previous report")
        XCTAssertNil(state.liveSyncConfig)
        XCTAssertNil(state.syncDiagnosticsError)
        await state.scanTask?.value
        XCTAssertNotNil(state.syncReport, "new vault's funnel reloads")
    }

    // MARK: - Announcement gates (m_spec §M-3: assertive, once per
    // vault, High-risk or multi-sync ONLY)

    func testHighRiskAnnouncesOncePerVaultAtHighPriority() async throws {
        let announcer = RecordingAnnouncer()
        let state = try await openVault(named: "high-risk", announcer: announcer) {
            try self.plantLiveSync($0)  // LiveSync = High risk
        }
        XCTAssertEqual(announcer.posts.count, 1, "exactly one assertive announcement")
        XCTAssertEqual(announcer.posts.first?.priority, .high)
        XCTAssertEqual(
            announcer.posts.first?.message,
            state.syncReport?.audioSummary,
            "the announcement is the report's pre-rendered audio summary")
    }

    func testMultiSyncAnnouncesEvenWithoutHighRisk() async throws {
        let announcer = RecordingAnnouncer()
        _ = try await openVault(named: "multi-sync", announcer: announcer) {
            // Syncthing + Dropbox marker = two Medium providers →
            // multi-sync warning without any High-risk row.
            try self.plantSyncthing($0)
            try Data("".utf8).write(to: $0.appendingPathComponent(".dropbox"))
        }
        XCTAssertEqual(announcer.posts.count, 1)
        XCTAssertEqual(announcer.posts.first?.priority, .high)
        XCTAssertTrue(
            announcer.posts.first?.message.contains("Warning: multiple sync systems") == true)
    }

    func testMediumOnlyReportDoesNotAnnounce() async throws {
        let announcer = RecordingAnnouncer()
        let state = try await openVault(named: "medium-only", announcer: announcer) {
            try self.plantSyncthing($0)  // single Medium provider
        }
        XCTAssertEqual(state.syncReport?.providers.map(\.kind), [.syncthing])
        XCTAssertTrue(announcer.posts.isEmpty, "Medium-only must stay silent")
    }

    func testEmptyReportDoesNotAnnounce() async throws {
        let announcer = RecordingAnnouncer()
        _ = try await openVault(named: "empty-quiet", announcer: announcer) { _ in }
        XCTAssertTrue(announcer.posts.isEmpty)
    }

    func testReopeningSameVaultDoesNotReannounce() async throws {
        let announcer = RecordingAnnouncer()
        let state = try await openVault(named: "reopen-vault", announcer: announcer) {
            try self.plantLiveSync($0)
        }
        XCTAssertEqual(announcer.posts.count, 1)
        // Same vault, reopened: the gate keys on vault identity.
        try await reopenVault(named: "reopen-vault", in: state)
        XCTAssertEqual(announcer.posts.count, 1, "same vault must not re-announce")
    }

    func testDifferentVaultRearmsAnnouncement() async throws {
        let announcer = RecordingAnnouncer()
        let state = try await openVault(named: "vault-a", announcer: announcer) {
            try self.plantLiveSync($0)
        }
        XCTAssertEqual(announcer.posts.count, 1)
        try await reopenVault(named: "vault-b", in: state) {
            try self.plantLiveSync($0)
        }
        XCTAssertEqual(announcer.posts.count, 2, "a different vault re-arms the gate")
    }

    func testManualRefreshDoesNotReannounce() async throws {
        let announcer = RecordingAnnouncer()
        let state = try await openVault(named: "refresh-vault", announcer: announcer) {
            try self.plantLiveSync($0)
        }
        XCTAssertEqual(announcer.posts.count, 1)
        await state.loadSyncDiagnostics()
        XCTAssertEqual(announcer.posts.count, 1, "refresh must not re-announce")
    }

    // MARK: - Panel state matrix (each state renders in both
    // appearances — the Presentation-Ready render smoke — with the
    // state selection logic pinned above by the loading tests)

    /// Render the panel against `state` in both appearances.
    private func assertPanelRenders(
        _ state: AppState, file: StaticString = #filePath, line: UInt = #line
    ) {
        let view = SyncDiagnosticsPanel().environmentObject(state)
        PresentationReady.assertRendersInBothAppearances(view, file: file, line: line)
    }

    func testPanelRendersLoadingState() {
        let announcer = RecordingAnnouncer()
        let state = makeAppState(announcer: announcer)
        // No report, no error → loading.
        XCTAssertNil(state.syncReport)
        assertPanelRenders(state)
    }

    func testPanelRendersEmptyAndPopulatedAndConfigStates() async throws {
        let announcer = RecordingAnnouncer()
        // Empty.
        let empty = try await openVault(named: "panel-empty", announcer: announcer) { _ in }
        assertPanelRenders(empty)

        // Single Low (Git).
        let low = try await openVault(named: "panel-low", announcer: announcer) {
            try self.plantGit($0)
        }
        assertPanelRenders(low)

        // Single High (LiveSync) + parsed config.
        let high = try await openVault(named: "panel-high", announcer: announcer) {
            try self.plantLiveSync(
                $0, dataJSON: #"{"couchDB_DBNAME": "db", "liveSync": true}"#)
        }
        assertPanelRenders(high)

        // Multi-sync (LiveSync High + Syncthing Medium + Git Low) with
        // malformed config — the populated multi-provider state the
        // DoD's appearance-snapshot bullet names.
        let multi = try await openVault(named: "panel-multi", announcer: announcer) {
            try self.plantLiveSync($0, dataJSON: "{not json")
            try self.plantSyncthing($0)
            try self.plantGit($0)
        }
        let report = try XCTUnwrap(multi.syncReport)
        XCTAssertEqual(report.providers.map(\.kind), [.liveSync, .git, .syncthing])
        XCTAssertNotNil(report.multiSyncWarning)
        guard case .malformed = try XCTUnwrap(multi.liveSyncConfig) else {
            return XCTFail("expected malformed config")
        }
        assertPanelRenders(multi)

        // LiveSync detected but data.json absent → NotPresent while
        // the provider row exists ("plugin present; no config found").
        let noConfig = try await openVault(named: "panel-noconfig", announcer: announcer) {
            try self.plantLiveSync($0)
        }
        XCTAssertEqual(noConfig.liveSyncConfig, .notPresent)
        assertPanelRenders(noConfig)
    }

    // MARK: - Badge contrast (§D — measured, both appearances)

    /// The three risk-badge text roles ride APCA-gated tokens; assert
    /// the specific pairings this panel uses clear the floor, measured
    /// in both appearances, and print the measured Lc values (the
    /// DoD's "numbers in the PR" evidence comes from this output).
    func testRiskBadgeTextClearsContrastFloor() {
        let pairings: [(name: String, text: NSColor, surface: NSColor)] = [
            ("high-risk badge on surface", .tokenDestructiveText, .tokenSurface),
            ("medium-risk badge on surface", .tokenWarningText, .tokenSurface),
            ("low-risk badge on surface", .tokenTextSecondary, .tokenSurface),
        ]
        PresentationReady.assertContrastFloor(pairings)
        for pairing in pairings {
            for name in PresentationReady.appearanceNames {
                guard let appearance = NSAppearance(named: name) else { continue }
                let lc = APCAContrast.lc(
                    text: pairing.text, background: pairing.surface, for: appearance)
                print(
                    "APCA \(pairing.name) [\(name.rawValue)]: Lc "
                        + String(format: "%.1f", abs(lc)))
            }
        }
    }

    // MARK: - State precedence matrix (pure — covers the unsupported
    // state a filesystem fixture can't produce)

    private func report(
        providers: [DetectedSyncProvider] = [],
        warning: String? = nil,
        summary: String = "No sync systems detected.",
        supported: Bool = true
    ) -> SyncDetectionReport {
        SyncDetectionReport(
            providers: providers,
            multiSyncWarning: warning,
            audioSummary: summary,
            supported: supported)
    }

    private func gitProvider() -> DetectedSyncProvider {
        DetectedSyncProvider(
            kind: .git,
            displayName: "Git",
            evidencePaths: [".git"],
            riskLevel: .low,
            recommendation: "This vault is a Git working tree.")
    }

    func testPanelStatePrecedenceMatrix() {
        // Unsupported wins over everything, including a queued error.
        XCTAssertEqual(
            SyncDiagnosticsPanel.state(
                report: report(supported: false), error: "boom"),
            .unsupported)
        // Error beats loading/empty/populated (Retry must be reachable
        // even when a stale report is retained).
        XCTAssertEqual(
            SyncDiagnosticsPanel.state(report: nil, error: "boom"), .error("boom"))
        XCTAssertEqual(
            SyncDiagnosticsPanel.state(
                report: report(providers: [gitProvider()]), error: "boom"),
            .error("boom"))
        // No report, no error → loading.
        XCTAssertEqual(SyncDiagnosticsPanel.state(report: nil, error: nil), .loading)
        // Settled report: empty vs populated.
        XCTAssertEqual(SyncDiagnosticsPanel.state(report: report(), error: nil), .empty)
        let populated = report(providers: [gitProvider()], summary: "1 sync system detected: Git.")
        XCTAssertEqual(
            SyncDiagnosticsPanel.state(report: populated, error: nil),
            .populated(populated))
    }

    /// The unsupported state renders (both appearances) from a
    /// hand-built supported=false report — the provider-abstracted
    /// session shape the Rust side pins with
    /// `detect_sync_without_fs_root_reports_unsupported`.
    func testUnsupportedStateRendersEmptyStateCopy() {
        let unsupported = report(
            summary: "Sync detection isn't available for this vault type.",
            supported: false)
        XCTAssertEqual(
            SyncDiagnosticsPanel.state(report: unsupported, error: nil), .unsupported)
        let view = LeafEmptyState(
            message: "Sync detection isn't available for this vault type.")
        PresentationReady.assertRendersInBothAppearances(view)
    }
}
