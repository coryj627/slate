// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// O-5 (#543) — the History leaf's AppState plumbing and the panel's
/// pure helpers, following the M-3 pattern: a REAL `VaultSession` on a
/// temp vault through the real funnel, with the announcement seam
/// faked (`RecordingAnnouncer`).
@MainActor
final class HistoryPanelTests: XCTestCase {
    final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
        var posts: [(message: String, priority: AnnouncementPriority)] = []
        func post(_ message: String, priority: AnnouncementPriority) {
            posts.append((message, priority))
        }
    }

    private var tempDirs: [URL] = []

    override func tearDown() {
        for dir in tempDirs {
            try? FileManager.default.removeItem(at: dir)
        }
        tempDirs = []
        super.tearDown()
    }

    private func makeAppState(announcer: AnnouncementPosting) -> AppState {
        AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!),
            announcer: announcer
        )
    }

    /// Seed a temp vault, open it, and wait for the initial scan.
    private func openVault(
        announcer: AnnouncementPosting,
        plant: (URL) throws -> Void = { _ in }
    ) async throws -> (AppState, URL) {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("history-tests-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: dir, withIntermediateDirectories: true)
        tempDirs.append(dir)
        try plant(dir)
        let state = makeAppState(announcer: announcer)
        state.openVault(at: dir)
        await state.scanTask?.value
        return (state, dir)
    }

    private func write(_ text: String, to dir: URL, name: String) throws {
        try text.write(
            to: dir.appendingPathComponent(name), atomically: true,
            encoding: .utf8)
    }

    // MARK: - Funnel ordering (g3)

    /// Pref ON: `changes_since_last_open` runs BEFORE `mark_opened` on
    /// note open. The order is observable with a real session: after
    /// an edit, a reopen must report `.diff` — the inverted order
    /// would mark first and report `.unchanged` (the lie the Rust side
    /// pins from below).
    func testSinceOpenFunnelComputesBeforeMarking() async throws {
        let announcer = RecordingAnnouncer()
        let (state, _) = try await openVault(announcer: announcer) {
            try self.write("v1\n", to: $0, name: "n.md")
        }
        state.setHistoryShowChangesSinceOpen(true)
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }
        _ = try session.saveText(path: "n.md", contents: "v1\n", expectedContentHash: nil)

        // Select through the real funnel — the FIRST open. With the
        // pref on it computes (no baseline yet) and then marks.
        state.selectedFilePath = "n.md"
        await state.noteLoadTask?.value
        await state.historyLoadTask?.value
        XCTAssertEqual(state.sinceOpenChanges, .noBaseline)

        // Reopen without edits: Unchanged (the funnel's mark landed).
        await state.loadHistoryForCurrentNote(path: "n.md")
        XCTAssertEqual(state.sinceOpenChanges, .unchanged)

        // Edit, then reopen: MUST be .diff — mark-first would report
        // .unchanged and never surface the change.
        _ = try session.saveText(
            path: "n.md", contents: "v1\nv2 added\n", expectedContentHash: nil)
        await state.loadHistoryForCurrentNote(path: "n.md")
        guard case .diff(let diff) = state.sinceOpenChanges else {
            return XCTFail(
                "expected .diff, got \(String(describing: state.sinceOpenChanges))"
            )
        }
        XCTAssertFalse(diff.operations.isEmpty)

        // And the very next open is Unchanged again (mark moved).
        await state.loadHistoryForCurrentNote(path: "n.md")
        XCTAssertEqual(state.sinceOpenChanges, .unchanged)
    }

    /// Pref OFF: neither call happens — no verdict AND no baseline
    /// write. Observable: turning the pref on later reports
    /// `.noBaseline` (a mark written while off would report
    /// `.unchanged`/`.diff`).
    func testSinceOpenPrefOffMakesNoCallsAtAll() async throws {
        let announcer = RecordingAnnouncer()
        let (state, _) = try await openVault(announcer: announcer) {
            try self.write("v1\n", to: $0, name: "n.md")
        }
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }
        _ = try session.saveText(path: "n.md", contents: "v1\n", expectedContentHash: nil)

        state.selectedFilePath = "n.md"
        await state.noteLoadTask?.value
        await state.historyLoadTask?.value

        XCTAssertFalse(state.historyShowChangesSinceOpen, "default off")
        await state.loadHistoryForCurrentNote(path: "n.md")
        XCTAssertNil(state.sinceOpenChanges, "no verdict published while off")

        state.setHistoryShowChangesSinceOpen(true)
        await state.loadHistoryForCurrentNote(path: "n.md")
        XCTAssertEqual(
            state.sinceOpenChanges, .noBaseline,
            "a mark written while the pref was off would have reported Unchanged"
        )
    }

    // MARK: - Version list

    func testVersionListLoadsFiltersAndPages() async throws {
        let announcer = RecordingAnnouncer()
        let (state, _) = try await openVault(announcer: announcer) {
            try self.write("v0\n", to: $0, name: "n.md")
        }
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }
        var contents = "v0\n"
        var hash: String? = nil
        for i in 1...3 {
            contents += "line \(i)\n"
            hash = try session.saveText(
                path: "n.md", contents: contents, expectedContentHash: hash
            ).newContentHash
        }
        state.selectedFilePath = "n.md"
        await state.noteLoadTask?.value
        await state.historyLoadTask?.value
        await state.loadHistoryForCurrentNote(path: "n.md")
        XCTAssertEqual(state.historyVersions.count, 3)
        XCTAssertEqual(state.historyTotalFiltered, 3)
        XCTAssertNil(state.historyLoadError)
        // Newest first; position identity.
        XCTAssertEqual(state.historyVersions.first?.positionFromTail, 0)
        XCTAssertFalse(state.historyVersions[0].audioFragment.isEmpty)
    }

    // MARK: - Restore flow

    func testRestoreRevertsContentAnnouncesAndBumpsFocusToken() async throws {
        let announcer = RecordingAnnouncer()
        let (state, _) = try await openVault(announcer: announcer) {
            try self.write("original\n", to: $0, name: "n.md")
        }
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }
        let r0 = try session.saveText(
            path: "n.md", contents: "original\n", expectedContentHash: nil)
        _ = try session.saveText(
            path: "n.md", contents: "changed\n",
            expectedContentHash: r0.newContentHash)

        // Select the note through the real funnel so the restore's
        // hash handoff uses the loaded document hash.
        state.selectedFilePath = "n.md"
        await state.noteLoadTask?.value
        await state.historyLoadTask?.value
        let focusBefore = state.historyFocusHeadToken
        let request = HistoryRestoreRequest(
            path: "n.md", versionHash: r0.newContentHash,
            formattedDate: "test date")
        await state.performRestore(request)

        XCTAssertNil(state.historyAlert)
        XCTAssertEqual(try session.readText(path: "n.md"), "original\n")
        XCTAssertTrue(
            announcer.posts.contains {
                $0.message == "Restored version from test date."
                    && $0.priority == .high
            },
            "posts: \(announcer.posts)"
        )
        XCTAssertEqual(state.historyFocusHeadToken, focusBefore + 1)
        // The restored state is itself a new version at the head.
        XCTAssertEqual(state.historyVersions.first?.positionFromTail, 0)
    }

    func testRestoreConflictRoutesToTheSaveConflictFlow() async throws {
        let announcer = RecordingAnnouncer()
        let (state, dir) = try await openVault(announcer: announcer) {
            try self.write("original\n", to: $0, name: "n.md")
        }
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }
        let r0 = try session.saveText(
            path: "n.md", contents: "original\n", expectedContentHash: nil)

        state.selectedFilePath = "n.md"
        await state.noteLoadTask?.value
        await state.historyLoadTask?.value

        // External change AFTER the document loaded: the restore's
        // compare-and-swap must fail and route to the conflict alert.
        try write("external overwrite\n", to: dir, name: "n.md")

        let request = HistoryRestoreRequest(
            path: "n.md", versionHash: r0.newContentHash,
            formattedDate: "test date")
        await state.performRestore(request)

        XCTAssertNotNil(state.currentSaveConflict, "WriteConflict routes to the standard flow")
        XCTAssertNil(state.historyAlert)
        XCTAssertEqual(
            try session.readText(path: "n.md"), "external overwrite\n",
            "nothing was written")
    }

    func testRestoreIntegrityFailureGetsTheSpecificAlert() async throws {
        let announcer = RecordingAnnouncer()
        let (state, dir) = try await openVault(announcer: announcer) {
            try self.write("original\n", to: $0, name: "n.md")
        }
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }
        let r0 = try session.saveText(
            path: "n.md", contents: "original\n", expectedContentHash: nil)
        _ = try session.saveText(
            path: "n.md", contents: "changed\n",
            expectedContentHash: r0.newContentHash)

        state.selectedFilePath = "n.md"
        await state.noteLoadTask?.value
        await state.historyLoadTask?.value

        // Corrupt the op log's entry bytes so reconstruction can't
        // verify — restore must alert and write NOTHING.
        let oplogDir = dir.appendingPathComponent(".slate/oplog")
        let logs = try FileManager.default.contentsOfDirectory(
            at: oplogDir, includingPropertiesForKeys: nil)
        XCTAssertFalse(logs.isEmpty)
        for log in logs where log.pathExtension == "oplog" {
            var data = try Data(contentsOf: log)
            // Flip bytes mid-file (inside the first entry's payload).
            let index = min(64, data.count - 1)
            data[index] ^= 0xFF
            data[index + 1] ^= 0xFF
            try data.write(to: log)
        }

        let request = HistoryRestoreRequest(
            path: "n.md", versionHash: r0.newContentHash,
            formattedDate: "test date")
        await state.performRestore(request)

        XCTAssertNotNil(state.historyAlert)
        XCTAssertEqual(
            try session.readText(path: "n.md"), "changed\n", "nothing written")
    }

    // MARK: - Deleted segment

    func testDeletedListRecoveryAndCollisionAlert() async throws {
        let announcer = RecordingAnnouncer()
        let (state, _) = try await openVault(announcer: announcer)
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }
        _ = try session.saveText(
            path: "gone.md", contents: "recover me\n", expectedContentHash: nil)
        try session.deleteFile(path: "gone.md")
        // Remnants surface at the scan reconcile.
        _ = try session.scanInitial(cancel: CancelToken())

        await state.loadDeletedFiles()
        XCTAssertEqual(state.deletedFiles.map(\.path), ["gone.md"])
        XCTAssertTrue(state.deletedFiles[0].recoverable)

        await state.recoverDeleted(path: "gone.md")
        XCTAssertTrue(
            announcer.posts.contains {
                $0.message == "Restored gone.md." && $0.priority == .high
            },
            "posts: \(announcer.posts)")
        XCTAssertEqual(try session.readText(path: "gone.md"), "recover me\n")
        XCTAssertTrue(state.deletedFiles.isEmpty, "list refreshed")

        // Collision: delete again, recreate at the path, then recover.
        try session.deleteFile(path: "gone.md")
        _ = try session.scanInitial(cancel: CancelToken())
        _ = try session.saveText(
            path: "gone.md", contents: "squatter\n", expectedContentHash: nil)
        await state.recoverDeleted(path: "gone.md")
        XCTAssertEqual(
            state.historyAlert?.message,
            "A file already exists at gone.md. Rename or move it first, then restore."
        )
    }

    // MARK: - Compaction-error channel

    func testCompactionFailureAlertsOncePerPath() async throws {
        let announcer = RecordingAnnouncer()
        let (state, _) = try await openVault(announcer: announcer)

        state.handleVaultEvent(
            code: .compactionFailed, path: "a.md", message: "exact core copy")
        XCTAssertEqual(state.compactionFailure?.message, "exact core copy")

        state.compactionFailure = nil
        state.handleVaultEvent(
            code: .compactionFailed, path: "a.md", message: "exact core copy")
        XCTAssertNil(state.compactionFailure, "one alert per (path, session)")

        state.handleVaultEvent(
            code: .compactionFailed, path: "b.md", message: "other file")
        XCTAssertEqual(
            state.compactionFailure?.path, "b.md", "a new path re-alerts")
    }

    // MARK: - Settings round-trip

    func testRetentionRoundTripsThroughPrefsJson() async throws {
        let announcer = RecordingAnnouncer()
        let (state, dir) = try await openVault(announcer: announcer)

        XCTAssertEqual(state.currentHistoryRetentionDays(), 90, "default")
        await state.applyHistoryRetention(days: 180)
        XCTAssertNil(state.historyAlert)
        XCTAssertEqual(state.currentHistoryRetentionDays(), 180, "live-applied")

        let prefs = try Data(
            contentsOf: dir.appendingPathComponent(".slate/prefs.json"))
        let root = try JSONSerialization.jsonObject(with: prefs) as? [String: Any]
        let history = root?["history"] as? [String: Any]
        XCTAssertEqual(history?["retention_days"] as? Int, 180, "persisted")
    }

    // MARK: - Pure panel helpers

    func testCompareSelectionModelTransitions() {
        typealias P = HistoryPanel
        // 0 → 1 → 2 selections accumulate.
        var sel = P.compareSelection(afterToggling: 5, on: true, current: [])
        XCTAssertEqual(sel, [5])
        sel = P.compareSelection(afterToggling: 2, on: true, current: sel)
        XCTAssertEqual(sel, [5, 2])
        // Third selection replaces the OLDER (higher position = 5).
        sel = P.compareSelection(afterToggling: 0, on: true, current: sel)
        XCTAssertEqual(sel, [2, 0])
        // Deselect just removes.
        sel = P.compareSelection(afterToggling: 2, on: false, current: sel)
        XCTAssertEqual(sel, [0])
        // Duplicate-hash rows are independent: identity is position,
        // so toggling one position never touches another.
        sel = P.compareSelection(afterToggling: 7, on: true, current: [3])
        XCTAssertEqual(sel, [3, 7])
        // Re-selecting an already-selected position is a no-op.
        sel = P.compareSelection(afterToggling: 7, on: true, current: [3, 7])
        XCTAssertEqual(sel, [3, 7])
    }

    /// The diff icon mapping is total and lands in the right family —
    /// added/removed/edited tinting never falls through to a generic
    /// glyph for the content classes.
    func testDiffOpClassSymbolFamilies() {
        XCTAssertEqual(DiffOperationList.symbol(for: .headingAdded), .diffAdded)
        XCTAssertEqual(DiffOperationList.symbol(for: .paragraphAdded), .diffAdded)
        XCTAssertEqual(DiffOperationList.symbol(for: .listItemAdded), .diffAdded)
        XCTAssertEqual(DiffOperationList.symbol(for: .headingRemoved), .diffRemoved)
        XCTAssertEqual(DiffOperationList.symbol(for: .paragraphRemoved), .diffRemoved)
        XCTAssertEqual(DiffOperationList.symbol(for: .listItemRemoved), .diffRemoved)
        XCTAssertEqual(DiffOperationList.symbol(for: .headingEdited), .diffEdited)
        XCTAssertEqual(DiffOperationList.symbol(for: .paragraphEdited), .diffEdited)
        XCTAssertEqual(DiffOperationList.symbol(for: .listItemEdited), .diffEdited)
        XCTAssertEqual(DiffOperationList.symbol(for: .propertySet), .addProperty)
        XCTAssertEqual(DiffOperationList.symbol(for: .propertyRemoved), .addProperty)
        XCTAssertEqual(DiffOperationList.symbol(for: .taskStatusChanged), .tasksLeaf)
        XCTAssertEqual(DiffOperationList.symbol(for: .codeBlockEdited), .code)
        XCTAssertEqual(DiffOperationList.symbol(for: .mathBlockEdited), .math)
        XCTAssertEqual(DiffOperationList.symbol(for: .diagramEdited), .diagram)
        XCTAssertEqual(DiffOperationList.symbol(for: .tableEdited), .diffEdited)
        XCTAssertEqual(DiffOperationList.symbol(for: .other), .diffEdited)
    }

    // MARK: - PresentationReady (§D contrast + §E render, both appearances)

    /// The panel's text roles clear the project APCA floor, measured
    /// in both appearances — secondary text and annotation-chip text
    /// on their actual backgrounds. Lc values are printed (the DoD's
    /// "numbers in the PR" evidence).
    func testHistoryTextRolesClearContrastFloor() {
        let pairings: [(name: String, text: NSColor, surface: NSColor)] = [
            ("history secondary text on surface", .tokenTextSecondary, .tokenSurface),
            (
                "annotation chip text on chip fill", .tokenTextSecondary,
                .tokenSurfaceSecondary
            ),
        ]
        PresentationReady.assertContrastFloor(pairings)
        for pairing in pairings {
            for name in PresentationReady.appearanceNames {
                guard let appearance = NSAppearance(named: name) else { continue }
                let lc = APCAContrast.lc(
                    text: pairing.text, background: pairing.surface, for: appearance)
                print("APCA \(pairing.name) [\(name.rawValue)]: Lc \(lc)")
            }
        }
    }

    /// The panel renders to a finite, non-empty size in both
    /// appearances (per-appearance crash / failed-render smoke).
    func testHistoryPanelRendersInBothAppearances() {
        let state = makeAppState(announcer: RecordingAnnouncer())
        let view = HistoryPanel().environmentObject(state)
        PresentationReady.assertRendersInBothAppearances(view)
    }

    /// §7.3 inspection assert: the diff renderer is a single-column
    /// operation list — one plain AX element per operation — and no
    /// side-by-side textual diff exists anywhere in the panel source.
    func testDiffRenderingIsOperationListNeverSideBySide() throws {
        let source = try historyPanelSource()
        XCTAssertTrue(
            source.contains(".accessibilityElement(children: .ignore)"),
            "operation rows must be plain AX elements")
        XCTAssertTrue(
            source.contains("NEVER a side-by-side"),
            "the §7.3 contract comment must survive refactors")
        XCTAssertFalse(
            source.lowercased().contains("side-by-side diff view"),
            "no side-by-side rendering construct")
        // The renderer walks operations in order — a single ForEach
        // over `diff.operations`, not paired columns.
        XCTAssertTrue(source.contains("ForEach(Array(diff.operations.enumerated())"))
    }

    private func historyPanelSource() throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/HistoryPanel.swift")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        XCTFail("Could not locate HistoryPanel.swift from \(#filePath)")
        return ""
    }

    func testDateAndSizeFormattingHelpers() {
        let ms: Int64 = 1_783_650_000_000
        let date = HistoryPanel.formattedDate(ms: ms)
        XCTAssertFalse(date.isEmpty)
        // Absolute date first — the formatted string must not be a
        // bare relative phrase.
        XCTAssertFalse(date.lowercased().contains("ago"))
        let relative = HistoryPanel.relativeDate(
            ms: ms - 3_600_000,
            now: Date(timeIntervalSince1970: Double(ms) / 1000))
        XCTAssertTrue(relative.contains("hour"), "relative: \(relative)")
        XCTAssertEqual(HistoryPanel.formattedSize(bytes: 0), "Zero KB")
    }
}
