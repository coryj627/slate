// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// #871: undo/redo for structural file ops (move + rename, incl. drag-moves).
///
/// The structural domain is a THIRD undo stack routed by file-tree focus,
/// mutually exclusive with the canvas domain (#372/#867). These tests pin:
///  - the inverse actually reverses on disk (move-back / rename-back) and
///    redo re-applies,
///  - the "Undid/Redid …" VoiceOver announcements,
///  - the domain-routing precedence (canvas → structural → responder),
///  - the per-domain menu title + enablement, and
///  - the per-vault clearing on close + direct switch (constraint #871.6).
@MainActor
final class StructuralUndoTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("struct-undo-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    /// Build a real vault + opened AppState (mirrors MutationAnnouncementFocusTests).
    private func makeVault(named: String = "vault", files: [String]) async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent(named)
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for rel in files {
            let url = vault.appendingPathComponent(rel)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try "# \((rel as NSString).lastPathComponent)\n".write(
                to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-\(named).json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    private func exists(_ vault: URL, _ rel: String) -> Bool {
        FileManager.default.fileExists(atPath: vault.appendingPathComponent(rel).path)
    }

    // MARK: - Move undo/redo reverses + re-applies

    func testMoveThenUndoMovesBackToOriginalParent() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])

        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertTrue(exists(vault, "dest/a.md"), "moved into dest")
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertEqual(state.structuralUndoStack.count, 1, "the move recorded one inverse")
        XCTAssertTrue(state.structuralRedoStack.isEmpty)

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertTrue(exists(vault, "a.md"), "undo moved it back to the root")
        XCTAssertFalse(exists(vault, "dest/a.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Undid move of a.md.")
        XCTAssertTrue(state.structuralUndoStack.isEmpty, "the undone entry retired")
        XCTAssertEqual(state.structuralRedoStack.count, 1, "and staged a redo")
    }

    func testMoveUndoThenRedoReappliesTheMove() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(exists(vault, "a.md"))

        state.structuralRedo()
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertTrue(exists(vault, "dest/a.md"), "redo re-applied the move")
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Redid move of a.md.")
        XCTAssertEqual(state.structuralUndoStack.count, 1, "redo re-armed undo")
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
    }

    func testMoveToVaultRootUndoReturnsToOriginalFolder() async throws {
        let (state, vault) = try await makeVault(files: ["sub/a.md"])
        await state.moveEntry(path: "sub/a.md", isDirectory: false, to: "")?.value
        XCTAssertTrue(exists(vault, "a.md"))

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(exists(vault, "sub/a.md"), "undo restores the original folder")
        XCTAssertFalse(exists(vault, "a.md"))
    }

    // MARK: - Rename undo/redo reverses + re-applies

    func testRenameThenUndoRestoresOldName() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])

        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value
        XCTAssertTrue(exists(vault, "alpha.md"))
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertEqual(state.structuralUndoStack.count, 1)

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertTrue(exists(vault, "a.md"), "undo restored the original name")
        XCTAssertFalse(exists(vault, "alpha.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Undid rename to a.md.")
    }

    func testRenameUndoThenRedoReappliesTheRename() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])
        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value
        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value

        state.structuralRedo()
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertTrue(exists(vault, "alpha.md"), "redo re-applied the rename")
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Redid rename to alpha.md.")
    }

    /// A fresh op after an undo clears the redo stack (the standard linear
    /// undo contract — you can't redo across a divergence).
    func testFreshOpClearsRedoStack() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md", "dest/x.md"])
        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value
        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertEqual(state.structuralRedoStack.count, 1, "an undo staged a redo")

        // A brand-new move must wipe the pending redo.
        await state.moveEntry(path: "b.md", isDirectory: false, to: "dest")?.value
        XCTAssertTrue(state.structuralRedoStack.isEmpty, "a fresh op clears redo")
        XCTAssertEqual(state.structuralUndoStack.count, 1)
    }

    /// The drag-move path is `moveEntry` with the default `.record` context —
    /// so a mis-drop is one ⌘Z from recovery, exactly like the menu path.
    func testDragMoveIsUndoable() async throws {
        let (state, vault) = try await makeVault(files: ["note.md", "dest/x.md"])
        // What FileTreeSidebar.handleDrop calls on an intra-tree drop.
        await state.moveEntry(path: "note.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "the drag-move recorded an inverse")

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(exists(vault, "note.md"), "a mis-drop is recoverable with one ⌘Z")
    }

    // MARK: - Empty-stack announcement (canvas-parity affordance)

    func testEmptyUndoStackAnnouncesRatherThanSilentNoOp() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        state.structuralUndo()
        XCTAssertEqual(state.lastMutationAnnouncement, "Nothing to undo.")
        state.structuralRedo()
        XCTAssertEqual(state.lastMutationAnnouncement, "Nothing to redo.")
    }

    // MARK: - Domain routing precedence (published-state only)

    /// Tree focus (and no canvas claiming the chord) → the structural domain.
    func testTreeFocusRoutesToStructuralDomain() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        XCTAssertFalse(state.undoTargetsCanvas, "a markdown vault has no active canvas")
        XCTAssertFalse(
            state.undoTargetsStructural,
            "default focus is the editor — structural must NOT own the chord")

        state.workspace.focusTreeRegion()
        XCTAssertEqual(state.workspace.focusRegion, .tree)
        XCTAssertTrue(state.undoTargetsStructural, "tree focus routes ⌘Z to the file-op stack")
    }

    /// #871 red-team regression: while the inline tree-rename field is open,
    /// `focusRegion` stays `.tree` (the field is a focus-WITHIN descendant of
    /// the List), but ⌘Z must NOT route to the structural stack — it belongs
    /// to the field editor's own text undo. `undoTargetsStructural` therefore
    /// excludes `renamingNode != nil`, mirroring `treeKeyInterceptionActive`'s
    /// `!isRenaming` guard. Without this, a ⌘Z to fix a typo mid-rename would
    /// reverse an unrelated prior move on disk and shadow text undo.
    func testInlineRenameSuppressesStructuralUndoDomain() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        state.workspace.focusTreeRegion()
        XCTAssertTrue(
            state.undoTargetsStructural, "precondition: tree focus, not renaming")

        state.renamingNode = AppState.RenamingNode(path: "a.md", isDirectory: false)
        XCTAssertEqual(
            state.workspace.focusRegion, .tree,
            "the rename field is focus-within: focusRegion stays .tree")
        XCTAssertFalse(
            state.undoTargetsStructural,
            "an open inline rename hands ⌘Z to the field editor, not the file-op stack")

        state.renamingNode = nil
        XCTAssertTrue(
            state.undoTargetsStructural,
            "ending the rename returns the chord to the structural domain")
    }

    /// An active canvas tab wins the chord even with the tree focused —
    /// `undoTargetsStructural` is FALSE whenever `undoTargetsCanvas` is true,
    /// so the two are provably mutually exclusive.
    func testCanvasDomainTakesPrecedenceOverStructural() async throws {
        let vault = tempDir.appendingPathComponent("canvas-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        let fixture = """
            {"nodes":[{"id":"a","type":"text","text":"Alpha","x":0,"y":0,\
            "width":200,"height":100}],"edges":[]}
            """
        try Data(fixture.utf8).write(to: vault.appendingPathComponent("c.canvas"))
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-canvas.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("c.canvas", target: .currentTab)
        XCTAssertTrue(state.undoTargetsCanvas, "canvas surface owns ⌘Z")

        // Even with the tree focused, canvas precedence holds.
        state.workspace.focusTreeRegion()
        XCTAssertTrue(state.undoTargetsCanvas, "canvas still wins")
        XCTAssertFalse(
            state.undoTargetsStructural,
            "mutual exclusivity: structural is false whenever canvas is true")
    }

    /// Editor focus with no canvas → neither special domain, so ⌘Z falls
    /// through to the NSText responder chain (title/enablement prove it).
    func testEditorFocusFallsThroughToResponderChain() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.focusRegion, .editor)
        XCTAssertFalse(state.undoTargetsCanvas)
        XCTAssertFalse(state.undoTargetsStructural, "editor focus is the responder domain")
    }

    // MARK: - Per-domain menu title + enablement

    func testStructuralMenuTitleAndEnablementPerDomain() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        state.workspace.focusTreeRegion()

        // Empty stacks, tree-focused: enabled (canvas-parity affordance),
        // bare-verb titles.
        XCTAssertTrue(state.undoTargetsStructural)
        XCTAssertEqual(state.undoMenuItemTitle, "Undo")
        XCTAssertEqual(state.redoMenuItemTitle, "Redo")
        XCTAssertTrue(state.undoMenuItemEnabled)
        XCTAssertTrue(state.redoMenuItemEnabled)

        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(
            state.undoMenuItemTitle, "Undo Move of a.md",
            "the title names the pending undo op")

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertEqual(
            state.redoMenuItemTitle, "Redo Move of a.md",
            "the undone op's name moves to the redo side")
    }

    /// The pure action-name composer is direction-truthful and shared by the
    /// title and the announcement (so they can't drift).
    func testStructuralUndoActionNamePhrasing() {
        XCTAssertEqual(
            AppState.structuralUndoActionName(
                .move(path: "dest/a.md", isDirectory: false, targetParent: "")),
            "move of a.md")
        XCTAssertEqual(
            AppState.structuralUndoActionName(
                .rename(path: "b.md", isDirectory: false, newName: "alpha.md")),
            "rename to alpha.md")
    }

    // MARK: - Per-vault clearing (constraint #871.6)

    func testStacksClearedOnVaultClose() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)

        state.closeVault()
        XCTAssertTrue(state.structuralUndoStack.isEmpty, "close drops the per-vault undo stack")
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
    }

    // MARK: - Routing wiring (source inspection)

    /// XCTest can't drive the `.commands` menu builder, so the ⌘Z / ⇧⌘Z
    /// THREE-domain routing in SlateMacApp is pinned by source inspection
    /// (the repo's `…ByInspection` pattern). Comments + string literals are
    /// stripped first so the tokens must appear as LIVE code — a removed
    /// structural branch (silent regression to the two-domain routing) fails
    /// here even though the AppState gates above would still pass.
    func testUndoRedoRoutingWiresStructuralDomainByInspection() throws {
        let source = try Self.slateMacAppSource()
        let stripped = SwiftSourceStripping.strippingCommentsAndStrings(source)
        for token in [
            "appState.undoTargetsStructural",
            "appState.structuralUndo()",
            "appState.structuralRedo()",
        ] {
            XCTAssertTrue(
                stripped.contains(token),
                "SlateMacApp's .undoRedo routing must wire \(token) (three-domain precedence)")
        }
        // The canvas branch must still precede structural (mutual-exclusivity
        // precedence): canvasUndo appears before structuralUndo in the source.
        let canvas = try XCTUnwrap(stripped.range(of: "appState.canvasUndo()"))
        let structural = try XCTUnwrap(stripped.range(of: "appState.structuralUndo()"))
        XCTAssertLessThan(
            canvas.lowerBound, structural.lowerBound,
            "canvas must be checked before structural (precedence)")
    }

    private static func slateMacAppSource() throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/SlateMacApp.swift")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        throw XCTSkip("SlateMacApp.swift not found relative to the test file")
    }

    func testStacksClearedOnDirectVaultSwitch() async throws {
        let (state, _) = try await makeVault(named: "A", files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)

        // Direct Open Vault (bypasses closeVault) onto a second vault.
        let vaultB = tempDir.appendingPathComponent("B")
        try FileManager.default.createDirectory(at: vaultB, withIntermediateDirectories: true)
        try "# b\n".write(
            to: vaultB.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value

        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "a direct switch must not carry vault A's inverse into vault B")
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
    }

    // MARK: - Codex round 1 regressions

    /// #871 Codex round 1 (F1): a non-undoable structural mutation
    /// (create/delete/import) is a history BARRIER — it clears the undo/redo
    /// stacks, so a later ⌘Z can't replay an inverse whose path a
    /// create/import may have refilled with a DIFFERENT file.
    func testNonUndoableMutationClearsStructuralHistory() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "move armed an inverse")

        // A create is non-undoable → barrier.
        await state.createFolder(name: "NewFolder", in: "")?.value
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "createFolder is a barrier — the stale move-inverse is dropped")

        // And so is a delete.
        await state.moveEntry(path: "dest/a.md", isDirectory: false, to: "")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "second move re-armed")
        await state.deleteEntry(path: "dest/x.md", isDirectory: false)?.value
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty, "deleteEntry is a barrier too")
    }

    /// #871 Codex round 1 (F2): a structural op in flight when the vault
    /// switches must not leave `isMutatingStructure` stuck true — the openVault
    /// reset releases it, so the new vault's ops are not wedged. (Kicks a move
    /// WITHOUT awaiting, so the flag is true across the synchronous switch.)
    func testVaultSwitchDoesNotWedgeStructuralMutations() async throws {
        let (state, _) = try await makeVault(named: "A", files: ["a.md", "dest/x.md"])

        // In-flight move: the detached FFI work suspends at the await, so the
        // guard flag is TRUE when the next synchronous line runs.
        let inflight = state.moveEntry(path: "a.md", isDirectory: false, to: "dest")
        XCTAssertNotNil(inflight)

        let vaultB = tempDir.appendingPathComponent("B")
        try FileManager.default.createDirectory(at: vaultB, withIntermediateDirectories: true)
        try "# b\n".write(
            to: vaultB.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value

        // Not wedged: a fresh structural op in vault B is admitted.
        let followUp = state.createFolder(name: "Fresh", in: "")
        XCTAssertNotNil(
            followUp,
            "vault switch must release isMutatingStructure — no permanent wedge")
        await followUp?.value
        await inflight?.value  // drain the stale task (its session guard no-ops)
    }

    /// #871 Codex round 1 (F4): an undo/redo-context RENAME that FAILS at the
    /// FFI (a collision the execution-time guard couldn't predict — e.g. a
    /// TOCTOU race between the guard and the FFI) has no inline field to render
    /// into, so it must surface via the general alert (`lastError`), NOT
    /// `structuralRenameError` — a silent failure. Exercised by invoking the
    /// `.undoing`-context rename directly against a guaranteed collision (the
    /// same call `structuralUndo` makes once past the guard).
    func testFailedUndoContextRenameSurfacesGeneralAlertNotInlineError() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.structuralRenameError = nil
        state.lastError = nil

        // b.md already exists → the rename FFI fails with DestinationExists.
        await state.renameEntry(
            path: "a.md", isDirectory: false, to: "b.md",
            undoContext: AppState.StructuralUndoContext.undoing)?.value

        XCTAssertNotNil(
            state.lastError,
            "an undo-context rename failure surfaces the general alert")
        XCTAssertNil(
            state.structuralRenameError,
            "must NOT write the inline-only error a hidden field can't show")
    }

    // MARK: - Codex round 2 regressions

    /// #871 Codex round 2 (F1): a file-creation funnel that BYPASSES
    /// `publishTreeMutation` (here New Canvas) must still clear the structural
    /// undo history — else a stale inverse could target the path the create
    /// just filled, advertising a doomed undo.
    func testBypassingCreateFunnelClearsStructuralHistory() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "move armed an inverse")

        // New Canvas creates a file via createExclusive, NOT publishTreeMutation.
        state.canvasNewCanvasFile()

        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "a bypassing create funnel is a structural-history barrier")
    }

    /// #871 Codex round 2 (F1): the execution-time safety net — if the files
    /// changed under an inverse (an EXTERNAL edit, or a funnel this build
    /// forgot to barrier), replaying it must be REFUSED, the suspect history
    /// dropped, and nothing mutated — not a wrong-file rename.
    func testStaleInverseWithOccupiedDestinationIsRefused() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        await state.renameEntry(path: "a.md", isDirectory: false, to: "b.md")?.value
        state.structuralUndo()  // b.md → a.md; redo stack = "rename a.md → b.md"
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(exists(vault, "a.md"))
        XCTAssertEqual(state.structuralRedoStack.count, 1)

        // EXTERNALLY occupy the redo's destination (bypasses every in-app
        // barrier), so replaying "rename a.md → b.md" would collide/misfire.
        try "# squatter\n".write(
            to: vault.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)

        state.structuralRedo()  // aborts synchronously in the validation guard

        XCTAssertTrue(
            state.structuralRedoStack.isEmpty, "the suspect redo history is dropped")
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Can't redo — the files have changed.")
        XCTAssertTrue(exists(vault, "a.md"), "a.md was NOT renamed onto the squatter")
        XCTAssertEqual(
            try String(
                contentsOf: vault.appendingPathComponent("b.md"), encoding: .utf8),
            "# squatter\n", "the external b.md is untouched")
    }

    /// #871 Codex round 3 (F1a): the execution-time guard must lstat, not
    /// `fileExists` — a DANGLING symlink at the inverse's destination is
    /// reported ABSENT by `fileExists` (it follows the link), which would let
    /// the replay clobber it. The guard must see it as occupied and REFUSE.
    func testUndoRefusesWhenDestinationIsADanglingSymlink() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)  // inverse: dest/a.md → root

        // Externally plant a DANGLING symlink at the inverse's destination slot.
        try FileManager.default.createSymbolicLink(
            at: vault.appendingPathComponent("a.md"),
            withDestinationURL: vault.appendingPathComponent("nowhere.md"))
        XCTAssertFalse(
            FileManager.default.fileExists(atPath: vault.appendingPathComponent("a.md").path),
            "precondition: fileExists follows the dangling link and reports absent")

        state.structuralUndo()  // aborts synchronously in the lstat guard

        XCTAssertTrue(
            state.structuralUndoStack.isEmpty, "suspect history dropped")
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Can't undo — the files have changed.")
        // The symlink survives (lstat sees it) and the move was NOT reversed.
        XCTAssertNotNil(
            try? FileManager.default.attributesOfItem(
                atPath: vault.appendingPathComponent("a.md").path),
            "the dangling symlink at a.md was not clobbered")
        XCTAssertTrue(exists(vault, "dest/a.md"), "the move was not wrongly reversed")
    }

    // MARK: - Post-merge audit: bypassing-create funnels are all barriers

    /// #871 post-merge audit (PR9/#901 Codex round): `recoverDeleted` restores
    /// a trashed file via `session.recoverDeletedFile` — a structural CREATE
    /// that BYPASSES `publishTreeMutation`, so it must clear the structural
    /// undo history itself. Otherwise a stale move/rename inverse armed before
    /// the restore could target the very path the restored file now occupies,
    /// advertising a doomed one-keystroke undo. Behavioral proof: arm an
    /// inverse AFTER the delete, restore, and confirm the stack is barriered
    /// (and the file actually came back, so the barrier assertion is real).
    func testRecoverDeletedIsAStructuralHistoryBarrier() async throws {
        let (state, vault) = try await makeVault(files: ["b.md", "dest/x.md"])
        guard let session = state.currentSession else { return XCTFail("no session") }

        // Create a.md THROUGH the session (journaled, so its remnant is
        // recoverable), then trash it and surface the remnant at scan reconcile
        // — the proven recipe from HistoryPanelTests.
        _ = try session.saveText(
            path: "a.md", contents: "recover me\n", expectedContentHash: nil)
        try session.deleteFile(path: "a.md")
        _ = try session.scanInitial(cancel: CancelToken())
        await state.loadDeletedFiles()
        XCTAssertTrue(
            state.deletedFiles.contains { $0.path == "a.md" && $0.recoverable },
            "precondition: a.md is a recoverable remnant")

        // Arm a structural inverse AFTER the delete, so a stale move-inverse sits
        // on the stack when the restore (a bypassing create) lands.
        await state.moveEntry(path: "b.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "the move armed an inverse")

        await state.recoverDeleted(path: "a.md")

        XCTAssertEqual(
            try session.readText(path: "a.md"), "recover me\n",
            "the success path ran — a.md was restored")
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "recoverDeleted bypasses publishTreeMutation — it must barrier the stale inverse")
        XCTAssertTrue(exists(vault, "dest/b.md"), "the arming move stands (only history cleared)")
    }

    /// #871 barrier-contract census (post-merge audit of PR9/#901). Every
    /// creation funnel that BYPASSES the `publishTreeMutation` choke point must
    /// apply the structural-undo barrier on its SUCCESS path — a stale
    /// move/rename inverse could otherwise replay onto the path the new file
    /// just filled ("Can't undo — the files have changed"). Reads each funnel's
    /// brace-balanced body from source and asserts, per funnel, that the barrier
    /// call is:
    ///   - PRESENT (guards silent removal),
    ///   - AFTER the create/write call (guards the clear being moved before the
    ///     create — where it would drop a legit undo without protecting the new
    ///     path), and
    ///   - on the SUCCESS path — for a linear do/switch success the barrier is
    ///     reached with NO intervening `catch`; for the two save-panel funnels
    ///     (write in a do/catch, barrier gated by a success flag) the `if
    ///     wroteOK` guard sits between the write and the barrier.
    /// A missing funnel is an XCTFail, not a silent skip, so the census can't be
    /// defeated by a rename.
    func testEveryBypassingCreateFunnelBarriersOnItsSuccessPath() {
        struct Funnel {
            let name: String
            let file: String
            let createAnchor: String
            let barrier: String
            /// nil → linear success path (assert NO `catch` between the anchor
            /// and the barrier); non-nil → the success-flag guard that must sit
            /// between the write and the barrier (save-panel funnels).
            let successGuard: String?
            /// Optional: a token the barrier must appear strictly BEFORE — guards
            /// a success-path ORDERING regression (performSave's session-global
            /// barrier must precede the per-note `loadedFilePath` guard, or a
            /// switch-away mid-write skips it).
            var mustPrecede: String? = nil
        }
        let funnels = [
            Funnel(
                name: "recoverDeleted", file: "AppState+History.swift",
                createAnchor: "case .success:",
                barrier: "clearStructuralUndoStacks()", successGuard: nil),
            Funnel(
                name: "canvasConvertToNote", file: "Canvas/AppState+CanvasExtras.swift",
                createAnchor: "session.saveText(",
                barrier: "clearStructuralUndoStacks()", successGuard: nil),
            Funnel(
                name: "exportSavedQuery", file: "Bases/AppState+Bases.swift",
                createAnchor: "exportSavedQueryAsBase(",
                barrier: "barrierStructuralUndoForCreatedVaultPath(", successGuard: nil),
            Funnel(
                name: "basesBuilderSaveAsBase", file: "Bases/AppState+Bases.swift",
                createAnchor: "saveQueryAsBase(",
                barrier: "barrierStructuralUndoForCreatedVaultPath(", successGuard: nil),
            Funnel(
                name: "performSave", file: "AppState.swift",
                createAnchor: "if case .success = outcome {",
                barrier: "barrierStructuralUndoForCreatedVaultPath(", successGuard: nil,
                mustPrecede: "guard loadedFilePath == path"),
            Funnel(
                name: "basesExportToSavePanel", file: "Bases/AppState+Bases.swift",
                createAnchor: "text.write(to: url",
                barrier: "barrierStructuralUndoForExternalWrite(", successGuard: "if wroteOK"),
            Funnel(
                name: "convertDataview", file: "Bases/BaseEmbedView.swift",
                createAnchor: "text.write(to: url",
                barrier: "onWroteSaveDestination(", successGuard: "if wroteOK"),
        ]
        for funnel in funnels {
            guard let source = Self.slateMacSource(funnel.file) else {
                XCTFail(
                    "source \(funnel.file) not found relative to the test file — "
                        + "the #871 barrier census can no longer read the funnel")
                continue
            }
            guard let bodySub = Self.functionBody(funnel.name, in: source) else {
                XCTFail(
                    "func \(funnel.name) not found in \(funnel.file) — the #871 "
                        + "barrier census can no longer locate the funnel")
                continue
            }
            let body = String(bodySub)
            guard let createRange = body.range(of: funnel.createAnchor) else {
                XCTFail(
                    "\(funnel.name): create/write anchor \"\(funnel.createAnchor)\" "
                        + "not found in the funnel body")
                continue
            }
            guard
                let barrierRange = body.range(
                    of: funnel.barrier,
                    range: createRange.upperBound..<body.endIndex)
            else {
                XCTFail(
                    "\(funnel.name): barrier \"\(funnel.barrier)\" missing or not "
                        + "AFTER the create/write — the #871 barrier must be on "
                        + "the success path")
                continue
            }
            let between = body[createRange.upperBound..<barrierRange.lowerBound]
            if let guardToken = funnel.successGuard {
                XCTAssertTrue(
                    between.contains(guardToken),
                    "\(funnel.name): the barrier must be gated by \"\(guardToken)\" "
                        + "(success-only) — not run on the write's failure path")
            } else {
                XCTAssertFalse(
                    between.contains("catch"),
                    "\(funnel.name): a `catch` between the create and the barrier "
                        + "means the barrier isn't on the linear success path")
            }
            if let precedeToken = funnel.mustPrecede {
                guard let precedeRange = body.range(of: precedeToken) else {
                    XCTFail(
                        "\(funnel.name): ordering anchor \"\(precedeToken)\" not "
                            + "found — can't verify the barrier's position")
                    continue
                }
                XCTAssertLessThan(
                    barrierRange.lowerBound, precedeRange.lowerBound,
                    "\(funnel.name): the session-global barrier must appear BEFORE "
                        + "\"\(precedeToken)\" — placing it after that per-note guard "
                        + "lets a switch-away mid-write skip the clear (ordering race)")
            }
        }
    }

    /// #871 over-clearing regression (Codex finding 3). `exportSavedQuery` calls
    /// the UNCONDITIONAL (create-OR-overwrite) `exportSavedQueryAsBase`, so it
    /// must barrier ONLY when a NEW in-vault `.base` is created — exporting OVER
    /// an existing `.base` must leave an unrelated legit move/rename undo intact.
    func testExportSavedQueryBarriersOnlyWhenCreatingANewBasePath() async throws {
        let (state, vault) = try await makeVault(files: ["b.md", "dest/x.md"])
        let session = try XCTUnwrap(state.currentSession)
        let queryID = try session.saveQuery(
            name: "All Files", description: nil,
            queryJson: Self.minimalSavedQueryJSON, sourceSyntax: .builder)

        // NEW path: the export creates a .base that did not exist → barrier.
        await state.moveEntry(path: "b.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "move armed an inverse")
        state.exportSavedQuery(id: queryID, path: "new.base")
        XCTAssertTrue(exists(vault, "new.base"), "the export created the file")
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "exporting to a NEW in-vault path barriers the stale inverse")

        // EXISTING path: the export OVERWRITES new.base → must NOT barrier.
        await state.moveEntry(path: "dest/b.md", isDirectory: false, to: "")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "second move re-armed")
        state.exportSavedQuery(id: queryID, path: "new.base")
        XCTAssertEqual(
            state.structuralUndoStack.count, 1,
            "overwriting an EXISTING .base must NOT drop the legit move undo")
    }

    /// #871 Keep Mine create-from-missing barrier (Codex finding 2). Move an
    /// open note's sibling to arm an inverse, externally delete the note, then
    /// Keep Mine: the save observes the path as MISSING (empty expected hash)
    /// and RECREATES it — a bypassing create that must barrier the stale
    /// inverse. A normal save (non-empty expected hash) is covered by the
    /// census; here we prove the create-from-missing transition end-to-end.
    func testKeepMineRecreatingAMissingNoteIsAStructuralHistoryBarrier() async throws {
        let (state, vault) = try await makeVault(files: ["note.md", "b.md"])

        // Load note.md into the editor.
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "note.md")

        // Arm a structural inverse (rename a sibling) that must survive to Keep
        // Mine — a stale inverse waiting when the recreate lands.
        await state.renameEntry(path: "b.md", isDirectory: false, to: "c.md")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "rename armed an inverse")

        // Externally DELETE the open note, then dirty + save → the save sees the
        // file missing (hash mismatch) and raises a conflict (no barrier yet).
        try FileManager.default.removeItem(at: vault.appendingPathComponent("note.md"))
        state.updateEditorText("# note\n\nmy unsaved edit.\n")
        await state.saveCurrentNote()?.value
        let conflict = try XCTUnwrap(
            state.currentSaveConflict, "a missing file surfaces a save conflict")
        XCTAssertEqual(
            conflict.currentContentHash, "", "the disk hash of a missing file is empty")
        XCTAssertEqual(
            state.structuralUndoStack.count, 1, "the conflict did not touch history")

        // Keep Mine re-saves with the empty (missing) expected hash — a
        // create-from-missing that recreates note.md and must barrier.
        await state.resolveSaveConflictKeepMine()?.value

        XCTAssertTrue(exists(vault, "note.md"), "Keep Mine recreated the note")
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "a create-from-missing save is a structural-history barrier")
    }

    /// #871 the shared save-panel barrier helper's decision matrix (Codex
    /// finding 1). `barrierStructuralUndoForExternalWrite` is what the two
    /// save-panel funnels (Dataview → .base conversion, Export Markdown)
    /// delegate to; NSSavePanel can target anywhere, so it must clear ONLY for a
    /// newly created IN-VAULT path — never for an overwrite, never off-vault.
    func testExternalWriteBarrierRespectsInVaultAndCreateVsOverwrite() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])

        // (1) A NEW in-vault path clears the history.
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        state.barrierStructuralUndoForExternalWrite(
            to: vault.appendingPathComponent("fresh.base"), existedBefore: false)
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty, "a new in-vault path barriers")

        // (2) An OVERWRITE of an existing in-vault path must NOT clear.
        await state.moveEntry(path: "dest/a.md", isDirectory: false, to: "")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        state.barrierStructuralUndoForExternalWrite(
            to: vault.appendingPathComponent("dest/x.md"), existedBefore: true)
        XCTAssertEqual(
            state.structuralUndoStack.count, 1, "overwriting keeps the legit undo")

        // (3) A write OUTSIDE the vault must NOT clear, even for a new path.
        let outside = FileManager.default.temporaryDirectory
            .appendingPathComponent("outside-\(UUID().uuidString).base")
        state.barrierStructuralUndoForExternalWrite(to: outside, existedBefore: false)
        XCTAssertEqual(
            state.structuralUndoStack.count, 1, "off-vault writes never barrier")
    }

    /// #871 Keep-Mine ORDERING race (Codex round 2, finding 1). The
    /// create-from-missing barrier is SESSION-GLOBAL and must fire even when the
    /// user switches to another note WHILE the recreate save is in flight —
    /// i.e. `loadedFilePath != path` at completion. We park the Keep-Mine save
    /// at the post-write gate, switch the loaded note, then release: the barrier
    /// must still clear the stale inverse (it now runs BEFORE the per-note
    /// `loadedFilePath` publication guard, not after it).
    func testKeepMineBarriersEvenWhenTheNoteSwitchesAwayMidWrite() async throws {
        let (state, vault) = try await makeVault(files: ["note.md", "other.md", "b.md"])

        // Load note.md.
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "note.md")

        // Arm a structural inverse that must survive to Keep Mine.
        await state.renameEntry(path: "b.md", isDirectory: false, to: "c.md")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "rename armed an inverse")

        // Externally delete note.md, then save → missing-file conflict (empty
        // hash). No dirtying — a clean save still conflicts and keeps the
        // mid-flight note switch free of dirty-navigation entanglement.
        try FileManager.default.removeItem(at: vault.appendingPathComponent("note.md"))
        await state.saveCurrentNote()?.value
        let conflict = try XCTUnwrap(state.currentSaveConflict)
        XCTAssertEqual(conflict.currentContentHash, "", "missing file → empty hash")

        // Park the Keep-Mine recreate save at the post-write seam so we can
        // switch the loaded note WHILE the write is in flight.
        let entered = expectation(description: "keep-mine reached the post-write gate")
        let (gate, release) = AsyncStream.makeStream(of: Void.self)
        state.basesPostWritePublishGate = {
            entered.fulfill()
            for await _ in gate {}
        }
        let keepMine = state.resolveSaveConflictKeepMine()
        await fulfillment(of: [entered], timeout: 10)
        state.basesPostWritePublishGate = nil

        // Switch to a DIFFERENT note in the SAME vault → loadedFilePath diverges
        // from the path the recreate save is committing.
        state.selectedFilePath = "other.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "other.md")

        // Release the gate → the recreate completes with loadedFilePath !=
        // "note.md". The session-global barrier must STILL have fired.
        release.finish()
        await keepMine?.value

        XCTAssertTrue(exists(vault, "note.md"), "Keep Mine recreated the note on disk")
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "the create-from-missing barrier is SESSION-GLOBAL — it must fire even "
                + "when the note switched away mid-write (the ordering fix)")
    }

    /// #871 the empty-hash guard is REAL (Codex round 2, finding 2b). A NORMAL
    /// save (non-empty expected hash, existing file) must NOT clear a pending
    /// move/rename inverse — the barrier is create-from-missing only. The source
    /// census can't see this condition, so lock it behaviorally: an
    /// unconditional barrier would fail here.
    func testNormalSaveDoesNotClearStructuralHistory() async throws {
        let (state, _) = try await makeVault(files: ["note.md", "b.md"])
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        XCTAssertNotNil(
            state.currentNoteContentHash, "a loaded existing note has a real hash")

        // Arm a structural inverse.
        await state.renameEntry(path: "b.md", isDirectory: false, to: "c.md")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "rename armed an inverse")

        // A normal edit + save of an EXISTING file (non-empty expected hash).
        state.updateEditorText("# note\n\nedited normally.\n")
        await state.saveCurrentNote()?.value
        XCTAssertFalse(state.hasUnsavedChanges, "the normal save committed")

        XCTAssertEqual(
            state.structuralUndoStack.count, 1,
            "a normal save of an existing file must NOT drop the legit move undo")
    }

    /// #871 over-clearing regression for the builder funnel (Codex round 2,
    /// finding 2c), mirroring the exportSavedQuery new-vs-overwrite test.
    /// `basesBuilderSaveAsBase` calls the UNCONDITIONAL `saveQueryAsBase`, so it
    /// must barrier ONLY when a NEW in-vault `.base` is created.
    func testBasesBuilderSaveAsBaseBarriersOnlyWhenCreatingANewBasePath() async throws {
        let (state, vault) = try await makeVault(files: ["b.md", "dest/x.md"])
        state.activeBaseQueryBuilder = BaseQueryBuilderModel()

        // NEW path: saving creates a .base that did not exist → barrier.
        await state.moveEntry(path: "b.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "move armed an inverse")
        state.basesBuilderSaveAsBase(path: "builder.base")
        XCTAssertTrue(exists(vault, "builder.base"), "the builder save created the file")
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "saving to a NEW in-vault path barriers the stale inverse")

        // EXISTING path: saving OVER builder.base → must NOT barrier.
        await state.moveEntry(path: "dest/b.md", isDirectory: false, to: "")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "second move re-armed")
        state.basesBuilderSaveAsBase(path: "builder.base")
        XCTAssertEqual(
            state.structuralUndoStack.count, 1,
            "overwriting an EXISTING .base must NOT drop the legit move undo")
    }

    /// A minimal valid saved-query envelope (mirrors BaseEmbedTests) — a
    /// Files-over-Notes table with one column. Exporting it produces `.base`
    /// text without needing the referenced folder to exist.
    private static let minimalSavedQueryJSON = #"""
        {
          "source": { "Folder": "Notes" },
          "row_source": "Files",
          "filters": null,
          "formulas": [],
          "custom_summaries": [],
          "group_by": null,
          "sort": [],
          "columns": [
            { "id": "file.name", "display_name": null }
          ],
          "summaries": [],
          "limit": null,
          "view": { "Table": { "fallback_from": null } }
        }
        """#

    /// Read a SlateMac source file by its path relative to `Sources/SlateMac`
    /// (mirrors `slateMacAppSource`, parameterized). Returns nil when the file
    /// can't be found/read; the census turns that into an XCTFail (not a skip),
    /// so a moved/renamed source can't silently defeat the barrier guarantee.
    private static func slateMacSource(_ relativePath: String) -> String? {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/\(relativePath)")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try? String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        return nil
    }

    /// The brace-balanced body of `func <name>` within `source`, from its
    /// opening `{` to the matching `}`, or nil when the function or its braces
    /// aren't found (the census turns nil into an XCTFail). Balancing (not
    /// "next func") keeps a neighboring function's body out of the slice.
    private static func functionBody(_ name: String, in source: String) -> Substring? {
        guard let funcRange = source.range(of: "func \(name)"),
            let open = source[funcRange.upperBound...].firstIndex(of: "{")
        else { return nil }
        var depth = 0
        var i = open
        while i < source.endIndex {
            switch source[i] {
            case "{": depth += 1
            case "}":
                depth -= 1
                if depth == 0 { return source[open...i] }
            default: break
            }
            i = source.index(after: i)
        }
        return nil
    }
}
