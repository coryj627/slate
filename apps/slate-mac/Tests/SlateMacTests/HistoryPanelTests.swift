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

    /// Adversarial round 1 High: a STALE history load (the user
    /// navigated away while it was in flight) must neither publish its
    /// verdict NOR move the note's baseline. The load is parked
    /// deterministically inside the race window (post-compute,
    /// pre-guard) on the `historyPublishGate` seam; the selection
    /// switch completes fully; then the stale load is released. If it
    /// had marked, the later genuine reopen would report `.unchanged`
    /// and the user's changes would be swallowed.
    func testStaleHistoryLoadNeitherPublishesNorMarks() async throws {
        let announcer = RecordingAnnouncer()
        let (state, _) = try await openVault(announcer: announcer) {
            try self.write("v1\n", to: $0, name: "n.md")
            try self.write("other\n", to: $0, name: "m.md")
        }
        state.setHistoryShowChangesSinceOpen(true)
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }
        _ = try session.saveText(path: "n.md", contents: "v1\n", expectedContentHash: nil)

        // Genuine first open of n.md: computes (.noBaseline) + marks.
        state.selectedFilePath = "n.md"
        await state.noteLoadTask?.value
        await state.historyLoadTask?.value
        XCTAssertEqual(state.sinceOpenChanges, .noBaseline)

        // Edit the note so an honest future reopen must report .diff.
        _ = try session.saveText(
            path: "n.md", contents: "v1\nv2 added\n", expectedContentHash: nil)

        // Park the NEXT load for n.md inside the race window.
        let entered = expectation(description: "stale load parked in the race window")
        let (gateStream, release) = AsyncStream.makeStream(of: Void.self)
        state.historyPublishGate = {
            entered.fulfill()
            for await _ in gateStream {}
        }
        let staleLoad = Task { await state.loadHistoryForCurrentNote(path: "n.md") }
        await fulfillment(of: [entered], timeout: 10)
        state.historyPublishGate = nil

        // Navigate away while the stale load is suspended; m.md's own
        // funnel completes (bumping the seq).
        state.selectedFilePath = "m.md"
        await state.noteLoadTask?.value
        await state.historyLoadTask?.value
        let verdictOnM = state.sinceOpenChanges

        // Release the stale load: it must change NOTHING.
        release.finish()
        await staleLoad.value
        XCTAssertEqual(state.sinceOpenChanges, verdictOnM, "stale publish blocked")

        // The honest reopen of n.md reports .diff — proof the stale
        // load never marked (a stale mark would make this .unchanged).
        state.selectedFilePath = "n.md"
        await state.noteLoadTask?.value
        await state.historyLoadTask?.value
        guard case .diff = state.sinceOpenChanges else {
            return XCTFail(
                "stale load moved the baseline: \(String(describing: state.sinceOpenChanges))"
            )
        }
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

        // Markers: a rename appends a PathChanged marker. It reaches
        // the published list (tests/CLI want the full ledger) and the
        // panel's default filter hides it; the toggle reveals it.
        try session.renameFile(path: "n.md", newName: "renamed.md")
        state.selectedFilePath = "renamed.md"
        await state.noteLoadTask?.value
        await state.historyLoadTask?.value
        let markers = state.historyVersions.filter(\.isMarker)
        XCTAssertEqual(markers.count, 1, "the rename marker is in the ledger")
        let hidden = HistoryPanel.visible(state.historyVersions, showMarkers: false)
        XCTAssertTrue(hidden.allSatisfy { !$0.isMarker }, "hidden by default")
        let shown = HistoryPanel.visible(state.historyVersions, showMarkers: true)
        XCTAssertEqual(shown.count, hidden.count + 1, "toggle reveals it")
    }

    func testShowOlderVersionsPagesThroughTheCursor() async throws {
        let announcer = RecordingAnnouncer()
        let (state, _) = try await openVault(announcer: announcer) {
            try self.write("v0\n", to: $0, name: "n.md")
        }
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }
        var contents = "v0\n"
        var hash: String? = nil
        for i in 1...55 {
            contents += "line \(i)\n"
            hash = try session.saveText(
                path: "n.md", contents: contents, expectedContentHash: hash
            ).newContentHash
        }
        state.selectedFilePath = "n.md"
        await state.noteLoadTask?.value
        await state.historyLoadTask?.value
        XCTAssertEqual(state.historyVersions.count, 50, "first page")
        XCTAssertNotNil(state.historyNextCursor)
        XCTAssertEqual(state.historyTotalFiltered, 55)

        await state.loadOlderVersions()
        XCTAssertEqual(state.historyVersions.count, 55, "second page appended")
        XCTAssertNil(state.historyNextCursor, "no more pages")
        // Position identity stays contiguous across pages.
        XCTAssertEqual(
            state.historyVersions.map(\.positionFromTail),
            (0..<55).map(UInt32.init))
    }

    func testDirtyBufferRestoreRoutesToConflictBeforeTouchingDisk() async throws {
        let announcer = RecordingAnnouncer()
        let (state, _) = try await openVault(announcer: announcer) {
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

        // Dirty the buffer through the editor's real entry point.
        state.updateEditorText("unsaved edits\n")
        XCTAssertTrue(state.hasUnsavedChanges)

        let request = HistoryRestoreRequest(
            path: "n.md", versionHash: r0.newContentHash,
            formattedDate: "test date")
        await state.performRestore(request)

        XCTAssertNotNil(
            state.currentSaveConflict,
            "dirty buffer routes to the conflict flow BEFORE any write")
        XCTAssertEqual(
            state.currentSaveConflict?.attemptedContents, "unsaved edits\n",
            "keep-mine preserves MY buffer, not the disk body")
        XCTAssertEqual(
            try session.readText(path: "n.md"), "original\n",
            "nothing was written")
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

    /// A restore that fails on damaged history must alert and write
    /// NOTHING. (The byte-flip tears an entry frame, so the reader's
    /// clean-prefix rule drops it and restore fails typed — the
    /// HistoryUnavailable-specific mapping is pinned by the Rust
    /// suite end-to-end and by copy inspection below.)
    func testRestoreFailureOnDamagedHistoryAlertsAndWritesNothing() async throws {
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

        XCTAssertNotNil(state.historyAlert, "damage surfaces as an alert")
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

    /// Adversarial round 1 High: the Rust history writer and the Swift
    /// bibliography writer race on ONE prefs.json. Both hold the
    /// `prefs.json.lock` flock across their read-modify-write, so
    /// neither section is ever lost to the other's rename. Without the
    /// lock this test fails within a few rounds.
    func testConcurrentPrefsWritersPreserveBothSections() async throws {
        let announcer = RecordingAnnouncer()
        let (state, dir) = try await openVault(announcer: announcer)
        guard let session = state.currentSession else {
            return XCTFail("no session")
        }
        let store = PrefsJsonStore(vaultRoot: dir)
        try store.writeBibliographyPrefs(
            BibliographyPrefs(
                sources: [], defaultStyle: "style.csl", additionalStyles: []))

        for round in 1...12 {
            let days = UInt32(30 + round)
            async let history: Void = Task.detached {
                try? session.setHistoryPrefs(
                    prefs: HistoryPrefs(retentionDays: days))
            }.value
            async let bibliography: Void = Task.detached {
                try? PrefsJsonStore(vaultRoot: dir)
                    .writeBibliographyPrefs(
                        BibliographyPrefs(
                            sources: [], defaultStyle: "style.csl",
                            additionalStyles: ["round\(round).csl"]))
            }.value
            _ = await (history, bibliography)

            let data = try Data(
                contentsOf: dir.appendingPathComponent(".slate/prefs.json"))
            let root =
                try JSONSerialization.jsonObject(with: data) as? [String: Any]
            XCTAssertNotNil(
                root?["history"], "round \(round): history section lost")
            XCTAssertNotNil(
                root?["bibliography"],
                "round \(round): bibliography section lost")
        }
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

    /// Two-version orientation: the OLDER selection (higher position)
    /// is `from`, the newer is `to`, regardless of toggle order.
    func testCompareEndpointsOrientation() {
        func version(_ position: UInt32, hash: String) -> VersionSummary {
            VersionSummary(
                positionFromTail: position, contentHashAfter: hash,
                timestampMs: 0, opKind: .editBatch, opCount: 1, byteDelta: 0,
                annotations: [], isMarker: false, audioFragment: "f")
        }
        let versions = [
            version(0, hash: "newest"), version(3, hash: "older"),
            version(7, hash: "oldest"),
        ]
        for positions in [[UInt32(3), 7], [UInt32(7), 3]] {
            let endpoints = HistoryPanel.compareEndpoints(
                positions: positions, in: versions)
            XCTAssertEqual(endpoints?.from.contentHashAfter, "oldest")
            XCTAssertEqual(endpoints?.to.contentHashAfter, "older")
        }
        XCTAssertNil(
            HistoryPanel.compareEndpoints(positions: [3], in: versions),
            "exactly two selections required")
        // Duplicate hashes resolve independently by position.
        let twins = [version(0, hash: "same"), version(5, hash: "same")]
        let endpoints = HistoryPanel.compareEndpoints(
            positions: [0, 5], in: twins)
        XCTAssertEqual(endpoints?.from.positionFromTail, 5)
        XCTAssertEqual(endpoints?.to.positionFromTail, 0)
    }

    /// The spec-pinned strings survive refactors (inspection asserts —
    /// the alert copy, the destructive role, and the BaselineCompacted
    /// caption all live in view code XCTest can't execute).
    func testPinnedCopyInspection() throws {
        let source = try historyPanelSource()
        XCTAssertTrue(source.contains(#""Restore version?""#))
        XCTAssertTrue(
            source.contains("This replaces the current content of"),
            "the pinned confirmation copy")
        XCTAssertTrue(
            source.contains("The replaced state remains available in version history."),
            "the pinned confirmation copy, second sentence")
        XCTAssertTrue(
            source.contains(#"Button("Restore", role: .destructive)"#),
            "destructive styling on the confirm action")
        XCTAssertTrue(
            source.contains(#""Earlier changes have been compacted.""#),
            "the BaselineCompacted caption")
        XCTAssertTrue(
            source.contains("case .noBaseline, .unchanged, nil:"),
            "Unchanged/NoBaseline render nothing — the four-state matrix")
        XCTAssertTrue(
            source.contains(
                #""Files deleted before Slate saved them go to the system Trash.""#),
            "the Deleted-segment footer")
        XCTAssertTrue(
            source.contains(#""No recently deleted files.""#)
                && source.contains(#""Select a note to see its history.""#),
            "the two empty states")
        // The HistoryUnavailable-specific restore copy lives in
        // AppState+History.swift (the Rust suite pins the error path
        // itself; this pins the exact user-facing sentence).
        let plumbing = try appStateHistorySource()
        XCTAssertTrue(
            plumbing.contains(
                "This version can't be restored: its history failed an integrity check."
            ), "the pinned integrity copy")
        XCTAssertTrue(
            plumbing.contains("Rename or move it first, then restore."),
            "the pinned DestinationExists copy")
    }

    /// Settings reseed (adversarial round 1 Medium): the History tab
    /// reseeds its retention picker when the vault changes while
    /// Settings stays mounted, and the programmatic reseed must not
    /// round-trip into set_history_prefs (view-local logic — pinned by
    /// inspection).
    func testSettingsReseedsAcrossVaultSwitchByInspection() throws {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        var source = ""
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/SettingsView.swift")
            if FileManager.default.fileExists(atPath: candidate.path) {
                source = (try? String(contentsOf: candidate, encoding: .utf8)) ?? ""
                break
            }
            cursor = cursor.deletingLastPathComponent()
        }
        XCTAssertTrue(
            source.contains(".onChange(of: appState.currentVaultURL)"),
            "the History tab reseeds on vault identity change")
        XCTAssertTrue(
            source.contains("isReseeding = true"),
            "programmatic reseeds are marked...")
        XCTAssertTrue(
            source.contains("if isReseeding {"),
            "...and consumed before persisting")
    }

    private func appStateHistorySource() throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/AppState+History.swift")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        XCTFail("Could not locate AppState+History.swift from \(#filePath)")
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
