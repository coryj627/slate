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
}
