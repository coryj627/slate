// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Combine
import XCTest

@testable import SlateMac

/// #1077 Phase 4, the mac half (#1147): the host rules of canonical file
/// identity (docs/plans/32 contracts I6 to I8). The scenario they pin is the
/// W5-3 codex round-2 stale overwrite: a tab of a deleted `Ghost.md`, a new
/// `ghost.md` (one physical file on APFS), every exact-identity comparison
/// blind to it, and the tab's hashless save burying the new note. I6
/// re-seats the tab after every Created or Renamed publication; I8 makes a
/// hashless save a create that cannot land on an existing file even when no
/// re-seat has run. The first four facts are the twins of Windows'
/// `CanonicalIdentityTests`.
@MainActor
final class CanonicalIdentityTests: XCTestCase {
    private var tempDirs: [URL] = []

    override func tearDown() {
        for dir in tempDirs {
            try? FileManager.default.removeItem(at: dir)
        }
        tempDirs = []
        super.tearDown()
    }

    private func makeVault(_ files: [String: String]) async throws -> (AppState, URL) {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("canonical-identity-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        tempDirs.append(dir)
        for (relative, text) in files {
            let url = dir.appendingPathComponent(relative)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try text.write(to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!))
        state.openVault(at: dir)
        await state.scanTask?.value
        return (state, dir)
    }

    /// The Windows fact's probe: create `Probe.md`, ask for `probe.md`,
    /// delete. The respelled comeback can only arise where the volume
    /// aliases spellings, so the volume is probed, never assumed (core's
    /// probe-not-assume rule for `canonical_path`).
    private func skipUnlessTheVolumeAliasesSpellings() throws {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("canonical-identity-probe-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        try "p".write(
            to: dir.appendingPathComponent("Probe.md"), atomically: true, encoding: .utf8)
        let aliasing = FileManager.default.fileExists(
            atPath: dir.appendingPathComponent("probe.md").path)
        guard aliasing else {
            throw XCTSkip(
                "This volume is case-sensitive: Ghost.md and ghost.md are two files here, so a note cannot come back under another spelling and this fact is vacuous.")
        }
    }

    private func read(_ vault: URL, _ relative: String) throws -> String {
        try String(contentsOf: vault.appendingPathComponent(relative), encoding: .utf8)
    }

    /// Open `path` in the active tab and return that tab.
    private func open(_ state: AppState, _ path: String) async throws -> TabID {
        state.selectedFilePath = path
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, path)
        return try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
    }

    /// Stand in for a volume that stores the note asked for as `asked` under
    /// the name `stored` (core answers every other path). On an aliasing
    /// volume this is core's own answer; on any other it lets a fact that is
    /// not about the volume run there too.
    private func answerStoredSpelling(_ stored: String, for asked: String, on state: AppState) {
        state.canonicalPathProbe = { session, path in
            if BaseExactIdentity.matches(path, asked) { return stored }
            return try session.canonicalPath(path: path)
        }
    }

    /// Every path a re-seat asked about, in order.
    private final class Probes {
        var paths: [String] = []
    }

    /// Core answers, and every question is recorded.
    private func recordProbes(on state: AppState) -> Probes {
        let probes = Probes()
        state.canonicalPathProbe = { session, path in
            probes.paths.append(path)
            return try session.canonicalPath(path: path)
        }
        return probes
    }

    // MARK: - The Windows twins

    /// I6, volume-probed. A tab of a note that is gone (opened, then moved
    /// to Trash, the state a parked tab of a deleted note is in) finds its
    /// note back under another spelling: it takes the stored spelling and,
    /// being clean, reloads, so its next save extends the new note.
    func testAMissingTabReseatsWhenItsFileComesBackUnderAnotherSpelling() async throws {
        try skipUnlessTheVolumeAliasesSpellings()
        let (state, vault) = try await makeVault(["Ghost.md": "ghost\n"])
        let session = try XCTUnwrap(state.currentSession)
        let tab = try await open(state, "Ghost.md")
        await state.deleteEntry(path: "Ghost.md", isDirectory: false)?.value
        XCTAssertNotNil(state.noteLoadError, "the tab is missing from disk")
        XCTAssertNil(state.loadedFilePath)

        // The note is created again under another spelling.
        _ = try session.createExclusive(path: "ghost.md", content: "fresh\n")
        state.reseatMissingTabs()
        await state.noteLoadTask?.value

        XCTAssertEqual(state.workspace.model.tab(tab)?.item, .markdown(path: "ghost.md"))
        XCTAssertEqual(state.selectedFilePath, "ghost.md")
        XCTAssertEqual(state.loadedFilePath, "ghost.md")
        XCTAssertNil(state.noteLoadError, "the tab is no longer missing")
        XCTAssertEqual(state.currentNoteText, "fresh\n")

        // Its next save is a compare-and-swap on the reloaded bytes: it
        // extends the new note instead of burying it.
        state.updateEditorText("fresh\nmore\n")
        await state.saveCurrentNote()?.value
        XCTAssertNil(state.currentSaveConflict)
        XCTAssertNil(state.saveError)
        XCTAssertEqual(try read(vault, "ghost.md"), "fresh\nmore\n")
    }

    /// I6: a DIRTY missing tab keeps its buffer. It is retargeted to the
    /// stored spelling, so its save names the right identity, and never
    /// reloaded over the user's text. Its save is hashless, so I8 makes it
    /// a create onto an occupied path: the conflict, never an overwrite.
    func testADirtyMissingTabIsRetargetedButNeverReloaded() async throws {
        try skipUnlessTheVolumeAliasesSpellings()
        let (state, vault) = try await makeVault(["Ghost.md": "ghost\n"])
        let session = try XCTUnwrap(state.currentSession)
        let tab = try await open(state, "Ghost.md")
        state.updateEditorText("unsaved\n")
        XCTAssertTrue(state.hasUnsavedChanges)
        await state.deleteEntry(path: "Ghost.md", isDirectory: false)?.value
        XCTAssertNotNil(state.noteLoadError, "the tab is missing from disk")
        XCTAssertEqual(state.missingNoteRecoveryDraft(for: "Ghost.md")?.body, "unsaved\n")

        _ = try session.createExclusive(path: "ghost.md", content: "fresh\n")
        state.reseatMissingTabs()
        await state.noteLoadTask?.value

        XCTAssertEqual(state.workspace.model.tab(tab)?.item, .markdown(path: "ghost.md"))
        XCTAssertEqual(state.loadedFilePath, "ghost.md")
        XCTAssertEqual(state.selectedFilePath, "ghost.md")
        XCTAssertEqual(state.currentNoteText, "unsaved\n", "never reloaded over the buffer")
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertNil(state.noteLoadError, "its file is back, so the tab is no longer missing")
        XCTAssertNil(state.missingNoteRecoveryDraft(for: "Ghost.md"))
        XCTAssertNil(state.missingNoteRecoveryDraft(for: "ghost.md"))

        // Its hashless save is a create onto an occupied path (I8): a
        // conflict, never an overwrite of the fresh note.
        await state.saveCurrentNote()?.value
        let conflict = try XCTUnwrap(
            state.currentSaveConflict, "the hashless save conflicts instead of overwriting")
        XCTAssertEqual(conflict.path, "ghost.md")
        XCTAssertEqual(conflict.attemptedContents, "unsaved\n")
        XCTAssertEqual(try read(vault, "ghost.md"), "fresh\n")
        XCTAssertEqual(state.currentNoteText, "unsaved\n")
    }

    /// I8: a tab with no content hash saves as a CREATE. It never lands on
    /// an existing file, even one that appeared outside Slate after the tab
    /// re-seated, and a free path is still created as the tab's first save,
    /// with its frontmatter. Not volume-dependent. On mac a hashless buffer is
    /// a note moved to Trash with unsaved edits whose file came back under
    /// another spelling (Windows' twin opens a path that never loaded, which
    /// mac shows as an error, not an editor), so the stored-spelling seam
    /// answers for the volume and the fact runs on every volume.
    func testAHashlessSaveNeverLandsOnAnExistingFile() async throws {
        let (state, vault) = try await makeVault([
            "Late.md": "---\ntitle: Late\n---\nlate\n", "other.md": "other\n",
        ])
        let session = try XCTUnwrap(state.currentSession)
        _ = try await open(state, "Late.md")
        XCTAssertEqual(state.currentNoteText, "late\n")
        state.updateEditorText("mine\n")
        await state.deleteEntry(path: "Late.md", isDirectory: false)?.value
        XCTAssertNotNil(state.noteLoadError)

        // Someone lands the note outside Slate under another spelling; the
        // tab re-seats to it and keeps its buffer, which has no content hash.
        try "theirs\n".write(
            to: vault.appendingPathComponent("LATE.md"), atomically: true, encoding: .utf8)
        answerStoredSpelling("LATE.md", for: "Late.md", on: state)
        state.reseatMissingTabs()
        XCTAssertEqual(state.loadedFilePath, "LATE.md")
        XCTAssertNil(state.noteLoadError)
        XCTAssertEqual(state.currentNoteText, "mine\n")

        await state.saveCurrentNote()?.value
        let conflict = try XCTUnwrap(
            state.currentSaveConflict, "a hashless save onto an occupied path is a conflict")
        XCTAssertEqual(conflict.path, "LATE.md")
        XCTAssertEqual(conflict.attemptedContents, "mine\n")
        XCTAssertEqual(
            conflict.currentContentHash,
            try session.readNoteParts(path: "LATE.md").contentHash,
            "Keep Mine compares against the bytes the conflict is about")
        XCTAssertEqual(try read(vault, "LATE.md"), "theirs\n")

        // A free path: the ordinary first save of the tab still creates,
        // with its frontmatter, in core's composition. Filling a path is a
        // structural create: the shared refresh indexes the note, the
        // structural undo history is a barrier, and the gate is released.
        state.resolveSaveConflictCancel()
        try FileManager.default.removeItem(at: vault.appendingPathComponent("LATE.md"))
        await state.renameEntry(path: "other.md", isDirectory: false, to: "renamed.md")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        await state.saveCurrentNote()?.value
        XCTAssertNil(state.currentSaveConflict)
        XCTAssertNil(state.saveError)
        XCTAssertEqual(try read(vault, "LATE.md"), "---\ntitle: Late\n---\nmine\n")
        XCTAssertEqual(
            try read(vault, "LATE.md"), composeNote(fmSource: "title: Late\n", body: "mine\n"))
        XCTAssertTrue(
            state.files.contains { $0.path == "LATE.md" }, "the create's refresh indexed it")
        XCTAssertEqual(state.structuralUndoStack.count, 0, "a create is an undo barrier")
        XCTAssertFalse(state.isMutatingStructure)

        // And the tab now carries a hash: its NEXT save is a CAS save.
        state.updateEditorText("mine\nmore\n")
        await state.saveCurrentNote()?.value
        XCTAssertNil(state.currentSaveConflict)
        XCTAssertNil(state.saveError)
        XCTAssertEqual(try read(vault, "LATE.md"), "---\ntitle: Late\n---\nmine\nmore\n")
    }

    private enum Publication: CaseIterable {
        case newNote, duplicate, rename, move, batchMove
    }

    /// I6 wiring: the re-seat runs after the publication of every
    /// structural operation that creates or renames a path. Windows pins its
    /// watcher handler's source; mac's publications are its own, so the fact
    /// runs them: a new note and a duplicate (`.createNote`), a rename, a
    /// move and a batch move. Same spelling, so not volume-dependent: the tab
    /// of a note moved to Trash stays missing after the note comes back
    /// outside Slate until a publication re-seats it, then reloads. New Note
    /// opens its note in that very tab, which supersedes the reload, so for
    /// it the fact shows the re-seat asked core about the missing note.
    func testTheLifecycleReseatsMissingTabsOnCreatedAndRenamedPublications() async throws {
        for publication in Publication.allCases {
            let (state, vault) = try await makeVault([
                "Ghost.md": "ghost\n", "other.md": "other\n", "dest/keep.md": "keep\n",
            ])
            let ghost = try await open(state, "Ghost.md")
            await state.deleteEntry(path: "Ghost.md", isDirectory: false)?.value
            XCTAssertNotNil(state.noteLoadError)

            // The note comes back outside Slate: nothing has re-seated it yet.
            try "back\n".write(
                to: vault.appendingPathComponent("Ghost.md"), atomically: true, encoding: .utf8)
            let probes = recordProbes(on: state)
            XCTAssertNotNil(state.noteLoadError)

            switch publication {
            case .newNote:
                await state.createNote(in: "")?.value
            case .duplicate:
                await state.duplicateEntry(path: "other.md")?.value
            case .rename:
                await state.renameEntry(
                    path: "dest/keep.md", isDirectory: false, to: "kept.md")?.value
            case .move:
                await state.moveEntry(path: "dest/keep.md", isDirectory: false, to: "")?.value
            case .batchMove:
                await state.batchMove(
                    [AppState.TreeSelection(path: "dest/keep.md", isDirectory: false)],
                    to: "", preferredFocusPath: nil)?.value
            }
            await state.noteLoadTask?.value

            XCTAssertTrue(
                probes.paths.contains("Ghost.md"),
                "\(publication) asks core for the missing note's stored spelling")
            if publication == .newNote {
                XCTAssertEqual(state.loadedFilePath, "Untitled.md")
                continue
            }
            XCTAssertEqual(state.workspace.model.activeGroup.activeTabID, ghost, "\(publication)")
            XCTAssertNil(state.noteLoadError, "\(publication) re-seats the missing tab")
            XCTAssertEqual(state.loadedFilePath, "Ghost.md", "\(publication)")
            XCTAssertEqual(state.currentNoteText, "back\n", "\(publication) reloads it")
        }
    }

    // MARK: - Supporting facts

    /// Every publication that creates or renames a path re-seats; one that
    /// only removes paths, adds an empty folder, or carries no truthful
    /// transform does not. Exhaustive over `TreeMutation.Kind`.
    func testOnlyCreatedAndRenamedPublicationsReseatMissingTabs() {
        typealias Kind = AppState.TreeMutation.Kind
        let change = BatchPathChange(oldPath: "a.md", newPath: "b.md", isDirectory: false)
        let reseating: [Kind] = [
            .createNote(path: "a.md"),
            .rename(oldPath: "a.md", newPath: "b.md"),
            .move(oldPath: "a.md", newPath: "d/a.md", oldParent: "", newParent: "d"),
            .batchMove(standing: [change], touched: []),
            .importBatch(materialized: [], standing: [], touched: []),
        ]
        let quiet: [Kind] = [
            .createFolder(path: "d"),
            .delete(path: "a.md", parent: "", wasDirectory: false),
            .batchTrash(trashed: [StructuralBatchItem(path: "a.md", isDirectory: false)]),
            .batchReconcile,
        ]
        for kind in reseating {
            XCTAssertTrue(kind.publishesCreatedOrRenamedPaths, "\(kind)")
        }
        for kind in quiet {
            XCTAssertFalse(kind.publishesCreatedOrRenamedPaths, "\(kind)")
        }
    }

    /// I7: an identity core cannot read is left as it is. The note is back
    /// on disk, so only the failed answer keeps the tab missing: a re-seat
    /// that took the failure for the tab's own spelling would reload it. A
    /// nil answer leaves it missing too, and once the answer is readable the
    /// same sweep re-seats it.
    func testAStoredSpellingThatCannotBeReadLeavesTheTabMissing() async throws {
        let (state, vault) = try await makeVault(["Ghost.md": "ghost\n"])
        _ = try await open(state, "Ghost.md")
        await state.deleteEntry(path: "Ghost.md", isDirectory: false)?.value
        let missing = try XCTUnwrap(state.noteLoadError)
        try "back\n".write(
            to: vault.appendingPathComponent("Ghost.md"), atomically: true, encoding: .utf8)

        let probes = Probes()
        state.canonicalPathProbe = { _, path in
            probes.paths.append(path)
            throw VaultError.Io(message: "the volume did not answer")
        }
        state.reseatMissingTabs()
        await state.noteLoadTask?.value
        XCTAssertEqual(probes.paths, ["Ghost.md"])
        XCTAssertEqual(state.noteLoadError, missing, "fail closed: still missing")
        XCTAssertNil(state.loadedFilePath)
        XCTAssertNil(state.currentNoteText)

        state.canonicalPathProbe = { _, _ in nil }
        state.reseatMissingTabs()
        await state.noteLoadTask?.value
        XCTAssertEqual(state.noteLoadError, missing, "no stored spelling: still missing")
        XCTAssertNil(state.currentNoteText)

        state.canonicalPathProbe = { session, path in try session.canonicalPath(path: path) }
        state.reseatMissingTabs()
        await state.noteLoadTask?.value
        XCTAssertNil(state.noteLoadError)
        XCTAssertEqual(state.currentNoteText, "back\n")
    }

    /// I6 for a parked tab, volume-probed: a background tab of a deleted
    /// note holds no document, so it takes the stored spelling at the
    /// publication and loads that spelling when it is next activated.
    func testAParkedMissingTabTakesTheStoredSpellingAndLoadsItWhenActivated() async throws {
        try skipUnlessTheVolumeAliasesSpellings()
        let (state, _) = try await makeVault(["Ghost.md": "ghost\n", "other.md": "other\n"])
        let session = try XCTUnwrap(state.currentSession)
        let ghost = try await open(state, "Ghost.md")
        state.openFile("other.md", target: .newTab)
        await state.noteLoadTask?.value
        await state.deleteEntry(path: "Ghost.md", isDirectory: false)?.value
        XCTAssertNil(state.workspace.document(for: ghost), "a clean parked tab keeps no buffer")

        _ = try session.createExclusive(path: "ghost.md", content: "fresh\n")
        state.reseatMissingTabs()
        XCTAssertEqual(state.workspace.model.tab(ghost)?.item, .markdown(path: "ghost.md"))
        XCTAssertEqual(state.loadedFilePath, "other.md", "the active tab is untouched")

        state.activateTab(ghost)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "ghost.md")
        XCTAssertEqual(state.currentNoteText, "fresh\n")
        XCTAssertNil(state.noteLoadError)
    }

    /// Under its own spelling, a dirty missing tab keeps its recovery state,
    /// as Windows' `KeepsBufferOnReseat` leaves such a tab missing: there is
    /// no identity to correct, and its save stays refused, so the note that
    /// came back is never overwritten by it.
    func testADirtyMissingTabWhoseNoteCameBackUnderItsOwnSpellingStaysInRecovery()
        async throws
    {
        let (state, vault) = try await makeVault(["Ghost.md": "ghost\n"])
        _ = try await open(state, "Ghost.md")
        state.updateEditorText("unsaved\n")
        await state.deleteEntry(path: "Ghost.md", isDirectory: false)?.value
        let missing = try XCTUnwrap(state.noteLoadError)
        try "back\n".write(
            to: vault.appendingPathComponent("Ghost.md"), atomically: true, encoding: .utf8)

        state.reseatMissingTabs()
        await state.noteLoadTask?.value
        XCTAssertEqual(state.noteLoadError, missing)
        XCTAssertEqual(state.missingNoteRecoveryDraft(for: "Ghost.md")?.body, "unsaved\n")
        XCTAssertEqual(state.currentNoteText, "unsaved\n")

        XCTAssertNil(state.saveCurrentNote(), "its save stays refused")
        XCTAssertEqual(try read(vault, "Ghost.md"), "back\n")
    }

    /// Tabs in two panes on one missing note re-seat in one sweep: core is
    /// asked once for the path, not once per tab, and both tabs take the
    /// stored spelling. The focused tab reloads at once; the other pane's
    /// tab holds no document (the Trash flip dropped its clean one), so it
    /// reads the same spelling, and the same bytes, when it is focused.
    func testTabsInTwoPanesOnOneMissingNoteReseatTogetherOnOneProbe() async throws {
        let (state, vault) = try await makeVault(["Ghost.md": "ghost\n"])
        let left = try await open(state, "Ghost.md")
        state.splitActivePane(axis: .horizontal)
        let right = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        XCTAssertNotEqual(left, right)
        XCTAssertEqual(state.workspace.model.tab(right)?.item, .markdown(path: "Ghost.md"))
        await state.deleteEntry(path: "Ghost.md", isDirectory: false)?.value
        XCTAssertNotNil(state.noteLoadError)
        XCTAssertNil(state.workspace.document(for: left))

        try "back\n".write(
            to: vault.appendingPathComponent("GHOST.md"), atomically: true, encoding: .utf8)
        let probes = Probes()
        state.canonicalPathProbe = { session, path in
            probes.paths.append(path)
            if BaseExactIdentity.matches(path, "Ghost.md") { return "GHOST.md" }
            return try session.canonicalPath(path: path)
        }
        state.reseatMissingTabs()
        await state.noteLoadTask?.value

        XCTAssertEqual(probes.paths, ["Ghost.md"], "one probe for the path, not one per tab")
        XCTAssertEqual(state.workspace.model.tab(left)?.item, .markdown(path: "GHOST.md"))
        XCTAssertEqual(state.workspace.model.tab(right)?.item, .markdown(path: "GHOST.md"))
        XCTAssertEqual(state.workspace.model.activeGroup.activeTabID, right)
        XCTAssertEqual(state.loadedFilePath, "GHOST.md")
        XCTAssertEqual(state.currentNoteText, "back\n")
        XCTAssertNil(state.noteLoadError)

        state.focusPane(.left)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.model.activeGroup.activeTabID, left)
        XCTAssertEqual(state.loadedFilePath, "GHOST.md")
        XCTAssertEqual(state.currentNoteText, "back\n")
        XCTAssertNil(state.noteLoadError)
    }

    /// I8's create is structural: a hashless save that fills a free path
    /// awaits the shared refresh (`structuralBatchRefreshRunner`) exactly
    /// once, with the structural gate held and before the save publishes,
    /// as the missing-file Keep Mine does (`performSave`'s structural
    /// branch). A hashless save that meets an occupant conflicts and runs no
    /// refresh, and either way the gate is released.
    func testAHashlessSaveThatCreatesAwaitsTheSharedRefreshOnce() async throws {
        let (state, vault) = try await makeVault(["Late.md": "late\n"])
        _ = try await open(state, "Late.md")
        state.updateEditorText("mine\n")
        await state.deleteEntry(path: "Late.md", isDirectory: false)?.value
        try "theirs\n".write(
            to: vault.appendingPathComponent("LATE.md"), atomically: true, encoding: .utf8)
        answerStoredSpelling("LATE.md", for: "Late.md", on: state)
        state.reseatMissingTabs()
        XCTAssertEqual(state.loadedFilePath, "LATE.md")

        @MainActor final class Refreshes {
            var gateHeld: [Bool] = []
            var publishedBefore: [Bool] = []
        }
        let refreshes = Refreshes()
        state.structuralBatchRefreshRunner = { state in
            refreshes.gateHeld.append(state.isMutatingStructure)
            refreshes.publishedBefore.append(state.currentNoteContentHash != nil)
            await state.loadFiles()
        }

        // An occupied path: the conflict, and no refresh.
        await state.saveCurrentNote()?.value
        XCTAssertNotNil(state.currentSaveConflict)
        XCTAssertEqual(refreshes.gateHeld.count, 0)
        XCTAssertFalse(state.isMutatingStructure)

        // A free path: one refresh, under the gate, before the save lands.
        state.resolveSaveConflictCancel()
        try FileManager.default.removeItem(at: vault.appendingPathComponent("LATE.md"))
        await state.saveCurrentNote()?.value
        XCTAssertNil(state.currentSaveConflict)
        XCTAssertNil(state.saveError)
        XCTAssertEqual(refreshes.gateHeld, [true], "awaited once, with the gate held")
        XCTAssertEqual(refreshes.publishedBefore, [false], "before the save publishes")
        XCTAssertNotNil(state.currentNoteContentHash)
        XCTAssertTrue(state.files.contains { $0.path == "LATE.md" })
        XCTAssertFalse(state.isMutatingStructure)
    }

    /// I8's landed arm: a hashless save whose create lands but is not
    /// indexed (#1123) is a save. It completes with the reported hash and
    /// speaks core's caveat (`CreatePublishedUnindexed`, PR 9's event), never
    /// a failure or a retry, and the next save is a CAS save on it. The
    /// stored-spelling seam answers for the volume, as in the I8 twin.
    func testAHashlessSaveThatLandsUnindexedCompletesWithCoresCaveat() async throws {
        let trigger = UUID().uuidString
        let asked = "late-\(trigger).md"
        let stored = "LATE-\(trigger).md"
        let (state, vault) = try await makeVault([asked: "late\n"])
        _ = try await open(state, asked)
        state.updateEditorText("mine\n")
        await state.deleteEntry(path: asked, isDirectory: false)?.value
        try "theirs\n".write(
            to: vault.appendingPathComponent(stored), atomically: true, encoding: .utf8)
        answerStoredSpelling(stored, for: asked, on: state)
        state.reseatMissingTabs()
        XCTAssertEqual(state.loadedFilePath, stored)
        XCTAssertNil(state.noteLoadError)
        try FileManager.default.removeItem(at: vault.appendingPathComponent(stored))

        var sentences: [String] = []
        let subscription = state.$lastMutationAnnouncement
            .dropFirst()
            .compactMap { $0 }
            .sink { sentences.append($0) }
        defer { subscription.cancel() }
        await AfterWriteFault.during(trigger) {
            await state.saveCurrentNote()?.value
        }

        XCTAssertEqual(try read(vault, stored), "mine\n")
        XCTAssertNil(state.currentSaveConflict)
        XCTAssertNil(state.saveError)
        XCTAssertFalse(state.hasUnsavedChanges)
        XCTAssertEqual(sentences, [AfterWriteFault.caveat(for: stored)])

        state.updateEditorText("mine\nmore\n")
        await state.saveCurrentNote()?.value
        XCTAssertNil(state.currentSaveConflict)
        XCTAssertNil(state.saveError)
        XCTAssertEqual(try read(vault, stored), "mine\nmore\n")
    }
}
