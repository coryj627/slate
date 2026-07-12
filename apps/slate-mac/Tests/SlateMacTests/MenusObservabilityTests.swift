// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Combine
import XCTest

@testable import SlateMac

/// The menus-observability seams (#868 + #867): the workspace→appState
/// objectWillChange bridge, the state-reflecting title inputs
/// (`activeTabIsReading`, `propertiesSourceShowing`), and the Undo/Redo
/// menu-title machinery (`undoMenuTick` pulse + pure title composers).
///
/// The SwiftUI `.commands` builder itself can't be driven from XCTest
/// (no menu bar in a test runner); these tests pin the OBSERVABLE
/// surface the menu items read, which is where both issues' bugs
/// lived — titles derived from state that never published.
@MainActor
final class MenusObservabilityTests: XCTestCase {

    /// Non-deallocating undo target for closure-based `registerUndo`
    /// (the manager holds it unowned; `self` would tie manager
    /// lifetime to the test case).
    private final class UndoTarget {}

    // MARK: - #868: the workspace → appState bridge

    /// The load-bearing seam: a mutation that publishes ONLY on the
    /// nested WorkspaceState must surface on appState's own
    /// objectWillChange, or `.commands` content reading workspace
    /// state renders stale (the #868 founding defect — a dynamic
    /// Reading Mode title could show the WRONG direction).
    func testWorkspacePublishForwardsIntoAppStateObjectWillChange() {
        let state = AppState()
        var fired = 0
        let subscription = state.objectWillChange.sink { fired += 1 }
        defer { subscription.cancel() }

        // viewModes is @Published on WorkspaceState and touched by no
        // appState property here — the forward is the only path.
        state.workspace.setViewMode(.reading, for: TabID())
        XCTAssertGreaterThan(
            fired, 0,
            "workspace.objectWillChange must forward into appState.objectWillChange"
        )

        // A model mutation (tab open) forwards too — the enablement
        // class (`workspace.activeTab == nil` menu items) rides this.
        let before = fired
        state.workspace.openTab(.markdown(path: "bridge.md"))
        XCTAssertGreaterThan(
            fired, before,
            "workspace model publishes must forward as well"
        )
    }

    // MARK: - #867 red-team: canvas sheets release the undo target

    /// Red-team (BROKEN): a canvas-tab sheet (card editor / prompt /
    /// picker) puts ITS editor in first-responder position — ⌘Z must
    /// drive the sheet's responder chain and the Edit menu must show
    /// its verbs, never the canvas stack's. Both the menu TITLES and
    /// the ⌘Z ROUTING key off `undoTargetsCanvas`, so the gate must
    /// drop while any of the three AppState-driven sheets is up.
    func testCanvasSheetsReleaseUndoTargetToResponderChain() async throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-menus-undo-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        let fixture = """
            {"nodes":[{"id":"a","type":"text","text":"Alpha","x":0,"y":0,\
            "width":200,"height":100}],"edges":[]}
            """
        try Data(fixture.utf8).write(to: vault.appendingPathComponent("c.canvas"))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("c.canvas", target: .currentTab)
        XCTAssertNotNil(state.activeCanvasDocument, "fixture premise")
        XCTAssertTrue(state.undoTargetsCanvas, "canvas surface owns ⌘Z by default")

        state.canvasCardEditor = CanvasCardEditorRequest(
            nodeId: "a", title: "Alpha", initialText: "Alpha")
        XCTAssertFalse(state.undoTargetsCanvas, "card-editor sheet owns undo")
        state.canvasCardEditor = nil

        state.canvasPrompt = .newGroup
        XCTAssertFalse(state.undoTargetsCanvas, "prompt sheet owns undo")
        state.canvasPrompt = nil

        state.canvasCardPicker = CanvasCardPickerRequest(purpose: .placeBelow)
        XCTAssertFalse(state.undoTargetsCanvas, "card-picker sheet owns undo")
        state.canvasCardPicker = nil

        XCTAssertTrue(state.undoTargetsCanvas, "dismissal returns ⌘Z to the canvas")
    }

    /// Red-team (BROKEN, #868 mirror): deleting the active note while
    /// its properties widget is in SOURCE mode must reset the
    /// `propertiesSourceShowing` mirror. The delete arm clears a bespoke
    /// field list (not `clearActiveNoteFields`), NoteContentView then
    /// unmounts the widget so its `.onChange` never fires — a stuck
    /// mirror latches "Hide Properties Source" over a deleted note and
    /// ⇧⌘D silently no-ops.
    func testDeleteResetsPropertiesSourceMirror() async throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-menus-delsrc-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        try Data("---\ntitle: A\n---\nbody\n".utf8)
            .write(to: vault.appendingPathComponent("a.md"))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "a.md", "note mounted")

        // The widget mirrors source mode into AppState.
        state.notePropertiesSourceModeChanged(true)
        XCTAssertTrue(state.propertiesSourceShowing, "source mode showing")

        await state.deleteEntry(path: "a.md", isDirectory: false)?.value

        XCTAssertNotNil(state.noteLoadError, "the tab flipped to the error state")
        XCTAssertFalse(
            state.propertiesSourceShowing,
            "the mirror must reset — no wrong-direction 'Hide' title over a deleted note")
    }

    /// Red-team (Codex, #868 mirror): the SYMMETRIC teardown path — a
    /// same-path reload (a conflict resolver's reload) that FAILS
    /// because the file was externally deleted/corrupted mounts the
    /// error state through `loadCurrentNote`'s failure arm, which also
    /// bypasses `clearActiveNoteFields`. It must reset the mirror too,
    /// or the same wrong-direction "Hide Properties Source" latch
    /// survives on the error tab.
    func testFailedReloadResetsPropertiesSourceMirror() async throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-menus-reloadfail-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        let noteURL = vault.appendingPathComponent("a.md")
        try Data("---\ntitle: A\n---\nbody\n".utf8).write(to: noteURL)
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        state.notePropertiesSourceModeChanged(true)
        XCTAssertTrue(state.propertiesSourceShowing)

        // External deletion between mount and reload, then a same-path
        // reload (the conflict-resolver shape) that now fails.
        try FileManager.default.removeItem(at: noteURL)
        await state.loadCurrentNote(path: "a.md")

        XCTAssertNotNil(state.noteLoadError, "the failed reload mounts the error state")
        XCTAssertFalse(
            state.propertiesSourceShowing,
            "the mirror resets on the failure arm too — no wrong-direction title")
    }

    // MARK: - #868: Enter/Exit Reading Mode input

    func testActiveTabIsReadingTracksActiveTabMode() {
        let state = AppState()
        XCTAssertFalse(
            state.activeTabIsReading,
            "no tab must read as NOT reading (title falls back to Enter Reading Mode)"
        )

        let tab = state.workspace.openTab(.markdown(path: "a.md"))
        XCTAssertFalse(state.activeTabIsReading, "fresh tab defaults to editing")

        state.workspace.setViewMode(.reading, for: tab)
        XCTAssertTrue(state.activeTabIsReading)

        state.workspace.setViewMode(.editing, for: tab)
        XCTAssertFalse(state.activeTabIsReading)
    }

    /// The mode is per-tab (U3-2): a background tab's reading mode
    /// must not leak into the ACTIVE tab's menu title.
    func testActiveTabIsReadingIsScopedToTheActiveTab() {
        let state = AppState()
        let first = state.workspace.openTab(.markdown(path: "a.md"))
        let second = state.workspace.openTab(.markdown(path: "b.md"))

        state.workspace.setViewMode(.reading, for: first)
        XCTAssertEqual(state.workspace.model.activeGroup.activeTabID, second)
        XCTAssertFalse(
            state.activeTabIsReading,
            "background tab's reading mode must not drive the title"
        )

        state.workspace.select(first)
        XCTAssertTrue(state.activeTabIsReading)
    }

    // MARK: - #882: Hide/Show Right Pane title input

    /// The View ▸ Hide/Show Right Pane menu title reads
    /// `isRightPaneVisible`, and `toggleRightPane()` publishes the flip so
    /// the title (and the zero-width detail collapse in MainSplitView)
    /// re-render. Default visible → "Hide Right Pane".
    func testToggleRightPanePublishesVisibilityForTheMenuTitle() {
        let state = AppState()
        XCTAssertTrue(
            state.isRightPaneVisible,
            "default visible → the menu reads 'Hide Right Pane'")

        var fired = 0
        let subscription = state.objectWillChange.sink { fired += 1 }
        defer { subscription.cancel() }

        state.toggleRightPane()
        XCTAssertFalse(state.isRightPaneVisible, "now hidden → menu reads 'Show Right Pane'")
        XCTAssertGreaterThan(fired, 0, "the flip must publish so the title re-renders")

        state.toggleRightPane()
        XCTAssertTrue(state.isRightPaneVisible)
    }

    /// #882 red-team: a leaf-reveal command must UN-HIDE a hidden pane —
    /// setting `activeLeaf` alone was a dead no-op while hidden. Every
    /// reveal routes through `focusLeafRegionRevealingPane()` (or sets
    /// `isRightPaneVisible = true` inline for the dock leaves).
    func testRevealCommandsUnhideTheRightPane() {
        let state = AppState()
        state.isRightPaneVisible = false
        state.focusLeafRegionRevealingPane()
        XCTAssertTrue(state.isRightPaneVisible, "the shared reveal helper un-hides")
        XCTAssertEqual(state.workspace.focusRegion, .leaf, "and focuses the leaf")

        state.isRightPaneVisible = false
        state.showHistoryPanel()
        XCTAssertTrue(state.isRightPaneVisible, "Show History reveals the pane")
        XCTAssertEqual(state.workspace.activeLeaf, .history)
    }

    /// #878 red-team: an external clear of `expandedCitation` (⌘J
    /// jump-to-bib, note-switch, vault close) must reset the row-anchor
    /// discriminator via its `didSet`, so the panel popover and the
    /// detached fallback can never both present on the next click.
    func testExternalExpandedCitationClearResetsRowAnchor() {
        let state = AppState()
        let cit = RenderedCitation(
            raw: "[@k]", visualText: "(k)", speechText: "k",
            bibEntry: nil, styleId: "apa")
        state.expandedCitationRowAnchored = true
        state.expandedCitation = cit
        XCTAssertTrue(state.expandedCitationRowAnchored, "row activation owns it")

        state.expandedCitation = nil  // the ⌘J / note-switch external clear
        XCTAssertFalse(
            state.expandedCitationRowAnchored,
            "clearing the expansion drops the anchor — no double-present latch")
    }

    // MARK: - #868: Show/Hide Properties Source mirror

    /// The AppState surface of the mirror. The view glue is one
    /// `.onChange(of: isSourceMode)` + `.onAppear` pair in
    /// NotePropertiesHeader (every view-local mutation site funnels
    /// through the @State bool, so the onChange wire covers toggle /
    /// commit / note-switch / Discard / Cancel); XCTest can't mount
    /// the SwiftUI view, so the seam itself is pinned here.
    func testPropertiesSourceShowingMirrorSetAndIdempotence() {
        let state = AppState()
        XCTAssertFalse(state.propertiesSourceShowing)

        state.notePropertiesSourceModeChanged(true)
        XCTAssertTrue(state.propertiesSourceShowing)

        // Equality-guarded: the .onAppear resync on an in-sync mount
        // must not publish. Count objectWillChange to prove the no-op.
        var fired = 0
        let subscription = state.objectWillChange.sink { fired += 1 }
        state.notePropertiesSourceModeChanged(true)
        XCTAssertEqual(fired, 0, "same-value mirror writes must not publish")
        subscription.cancel()

        state.notePropertiesSourceModeChanged(false)
        XCTAssertFalse(state.propertiesSourceShowing)
    }

    /// Note transitions reset the mirror through the funnel — the
    /// widget self-hides on `loadedFilePath == nil` without firing its
    /// own onChange, so `clearActiveNoteFields` owns that edge (same
    /// belt as `propertiesSourceError`).
    func testClearActiveNoteFieldsResetsPropertiesSourceShowing() {
        let state = AppState()
        state.notePropertiesSourceModeChanged(true)

        state.clearActiveNoteFields()
        XCTAssertFalse(
            state.propertiesSourceShowing,
            "note transition must reset the Show/Hide Properties Source direction"
        )
    }

    // MARK: - #867: undo tick pulse

    /// A real NSUndoManager's group-close and undo notifications must
    /// bump the published tick (debounced) — that publish is what
    /// re-renders the Edit menu after typing registers undo actions.
    func testUndoManagerNotificationsBumpUndoMenuTick() {
        let state = AppState()
        let target = UndoTarget()
        let manager = UndoManager()
        manager.groupsByEvent = false

        var cancellables = Set<AnyCancellable>()
        let closeBump = expectation(description: "tick bumped after group close")
        closeBump.assertForOverFulfill = false
        state.$undoMenuTick
            .dropFirst()
            .sink { _ in closeBump.fulfill() }
            .store(in: &cancellables)

        manager.beginUndoGrouping()
        manager.registerUndo(withTarget: target) { _ in }
        manager.endUndoGrouping()  // posts NSUndoManagerDidCloseUndoGroup

        wait(for: [closeBump], timeout: 5)
        let afterClose = state.undoMenuTick
        XCTAssertGreaterThan(afterClose, 0)

        let undoBump = expectation(description: "tick bumped after undo")
        undoBump.assertForOverFulfill = false
        state.$undoMenuTick
            .dropFirst()
            .sink { _ in undoBump.fulfill() }
            .store(in: &cancellables)

        manager.undo()  // posts NSUndoManagerDidUndoChange

        wait(for: [undoBump], timeout: 5)
        XCTAssertGreaterThan(
            state.undoMenuTick, afterClose,
            "an executed undo must pulse the menu again (title flips to the redo side)"
        )
    }

    /// The canvas funnel entry: `canvasApply`/`canvasUndo`/`canvasRedo`
    /// call this after mutating the plain-var stacks (never @Published);
    /// it must land on the same debounced tick.
    func testNoteUndoStacksChangedBumpsUndoMenuTick() {
        let state = AppState()
        var cancellables = Set<AnyCancellable>()
        let bumped = expectation(description: "tick bumped from canvas funnel")
        bumped.assertForOverFulfill = false
        state.$undoMenuTick
            .dropFirst()
            .sink { _ in bumped.fulfill() }
            .store(in: &cancellables)

        state.noteUndoStacksChanged()

        wait(for: [bumped], timeout: 5)
        XCTAssertGreaterThan(state.undoMenuTick, 0)
    }

    // MARK: - #867: title composers (pure)

    func testCanvasUndoRedoMenuTitleComposition() {
        // Leading character uppercased; the embedded user-typed card
        // title passes through verbatim (no Title Case mangling).
        XCTAssertEqual(
            AppState.canvasUndoRedoMenuTitle(
                base: "Undo", actionName: "delete \"my Card\""),
            "Undo Delete \"my Card\""
        )
        XCTAssertEqual(
            AppState.canvasUndoRedoMenuTitle(base: "Redo", actionName: "create card"),
            "Redo Create card"
        )
        // Empty stack → bare verb.
        XCTAssertEqual(
            AppState.canvasUndoRedoMenuTitle(base: "Undo", actionName: nil), "Undo")
        XCTAssertEqual(
            AppState.canvasUndoRedoMenuTitle(base: "Redo", actionName: ""), "Redo")
    }

    /// The responder path defers to NSUndoManager's own composed,
    /// localized titles — and reads plain "Undo"/"Redo" (matching the
    /// disabled state) whenever the manager reports nothing to do.
    func testResponderUndoRedoTitlesFollowTheManager() {
        let target = UndoTarget()
        let manager = UndoManager()
        manager.groupsByEvent = false

        XCTAssertEqual(AppState.responderUndoMenuTitle(manager), "Undo")
        XCTAssertEqual(AppState.responderRedoMenuTitle(manager), "Redo")
        XCTAssertEqual(AppState.responderUndoMenuTitle(nil), "Undo")
        XCTAssertEqual(AppState.responderRedoMenuTitle(nil), "Redo")

        manager.beginUndoGrouping()
        Self.registerTypingAction(on: manager, target: target)
        manager.endUndoGrouping()

        XCTAssertEqual(
            AppState.responderUndoMenuTitle(manager),
            manager.undoMenuItemTitle,
            "with an undoable action the manager's own composition wins"
        )
        XCTAssertTrue(
            AppState.responderUndoMenuTitle(manager).contains("Typing"),
            "the action name must ride the title (undo-and-redo.md)"
        )

        manager.undo()
        XCTAssertEqual(
            AppState.responderUndoMenuTitle(manager), "Undo",
            "spent stack reads plain again")
        XCTAssertTrue(
            AppState.responderRedoMenuTitle(manager).contains("Typing"),
            "the undone action's name moves to the redo side"
        )
    }

    /// Symmetric registration (the NSUndoManager contract): whatever
    /// the undo handler registers during `undo()` becomes the REDO
    /// action — including its name. A no-op handler would leave redo
    /// empty, which is why the handler re-registers itself.
    private static func registerTypingAction(on manager: UndoManager, target: UndoTarget) {
        manager.registerUndo(withTarget: target) { [weak manager] t in
            guard let manager else { return }
            registerTypingAction(on: manager, target: t)
        }
        manager.setActionName("Typing")
    }

    /// Under XCTest there is no NSApp/key window, so the responder
    /// probe resolves nil — the instance-level title/enablement must
    /// fall back safely (plain titles, ENABLED: a nil read must never
    /// latch the chord off — see the property doc).
    func testInstanceTitlesFallBackWithoutAKeyWindow() {
        let state = AppState()
        XCTAssertNil(state.responderChainUndoManager)
        XCTAssertEqual(state.undoMenuItemTitle, "Undo")
        XCTAssertEqual(state.redoMenuItemTitle, "Redo")
        XCTAssertTrue(state.undoMenuItemEnabled)
        XCTAssertTrue(state.redoMenuItemEnabled)
    }
}
