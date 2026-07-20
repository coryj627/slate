// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// FL7-1 (#668): the internally gated dual-pane container — layout
/// gate round-trip, container-vs-leaf selection matrix, folders-only
/// projection, focus-transfer rules, divider persistence, filter
/// scoping, and tree-mode equivalence (zero extra work).
@MainActor
final class SidebarDualPaneContainerTests: XCTestCase {
    private var root: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-dual-pane-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: root, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
        try super.tearDownWithError()
    }

    private func freshDefaults() -> UserDefaults {
        let suite = "SidebarDualPaneContainerTests.\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defaults.removePersistentDomain(forName: suite)
        return defaults
    }

    private func makeState(defaults: UserDefaults? = nil) -> AppState {
        AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent(
                    "recents-\(UUID().uuidString).json")),
            sidebarSectionDefaults: defaults ?? freshDefaults())
    }

    private func openVault(
        _ state: AppState, named name: String, files: [String]
    ) throws {
        let vault = root.appendingPathComponent(name)
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        for path in files {
            let url = vault.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(),
                withIntermediateDirectories: true)
            try "# \(path)\n#tagged\n".write(
                to: url, atomically: true, encoding: .utf8)
        }
        state.openVault(at: vault)
        _ = try XCTUnwrap(state.currentSession).scanInitial(
            cancel: CancelToken())
    }

    // MARK: - Internal gate

    func testLayoutGateDefaultsToTreeAndRoundTripsDeviceLocally() {
        let defaults = freshDefaults()
        let state = makeState(defaults: defaults)
        XCTAssertEqual(state.sidebarLayout, .tree, "shipped default")

        state.setSidebarLayoutForInternalTesting(.dualPane)
        XCTAssertEqual(state.sidebarLayout, .dualPane)
        XCTAssertEqual(
            defaults.string(forKey: AppState.sidebarLayoutKey), "dualPane")

        // A fresh AppState on the same device store restores the mode.
        let restored = makeState(defaults: defaults)
        XCTAssertEqual(restored.sidebarLayout, .dualPane)

        state.setSidebarLayoutForInternalTesting(.tree)
        XCTAssertEqual(
            defaults.string(forKey: AppState.sidebarLayoutKey), "tree")
    }

    func testLayoutToggleIsPublicAfterListPaneShips() throws {
        // FL7-2 rule 6: FL-13 kept the layout internal; FL-14 ships the
        // complete list pane + equivalence tests, so the exposure gate
        // OPENS — the catalog carries the toggle and dispatch flips the
        // mode with an announcement.
        XCTAssertTrue(
            SidebarActionCatalog.actions.contains {
                $0.id == SlateCommandID.sidebarToggleLayout
            },
            "FL-14 exposes the layout toggle in the catalog")
        XCTAssertTrue(SlateCommandID.all.contains(
            SlateCommandID.sidebarToggleLayout))

        let state = makeState()
        try openVault(state, named: "toggle-cmd", files: ["a.md"])
        XCTAssertEqual(state.sidebarLayout, .tree)
        _ = try state.dispatchSidebarAction(
            id: SlateCommandID.sidebarToggleLayout)
        XCTAssertEqual(state.sidebarLayout, .dualPane)
        XCTAssertEqual(state.lastMutationAnnouncement, "Dual-pane sidebar.")
        _ = try state.dispatchSidebarAction(
            id: SlateCommandID.sidebarToggleLayout)
        XCTAssertEqual(state.sidebarLayout, .tree)
        XCTAssertEqual(state.lastMutationAnnouncement, "Tree sidebar.")
    }

    func testPublicSetterPersistsAndInternalHookForwards() {
        let defaults = freshDefaults()
        let state = makeState(defaults: defaults)
        state.setSidebarLayout(.dualPane)
        XCTAssertEqual(
            defaults.string(forKey: AppState.sidebarLayoutKey), "dualPane")
        state.setSidebarLayoutForInternalTesting(.tree)
        XCTAssertEqual(
            defaults.string(forKey: AppState.sidebarLayoutKey), "tree")
    }

    // MARK: - Container vs leaf selection matrix (rule 3)

    func testScopeShortcutsSelectContainersInDualPaneOnly() throws {
        let state = makeState()
        try openVault(
            state, named: "containers", files: ["P/a.md", "Q/b.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)

        state.activateSidebarShortcut(
            SidebarShortcut(kind: .folder, path: "P"))
        XCTAssertEqual(state.sidebarSelectedContainer, .folder(path: "P"))
        XCTAssertNil(
            state.sidebarRevealRequest,
            "dual-pane container selection does not drive the tree reveal")

        state.activateSidebarShortcut(
            SidebarShortcut(kind: .tag, path: "tagged"))
        XCTAssertEqual(state.sidebarSelectedContainer, .tag(full: "tagged"))
        XCTAssertFalse(
            state.sidebarFilterModel.isActive,
            "dual-pane tag containers do not enter the filter overlay")

        state.activateSidebarShortcut(
            SidebarShortcut(kind: .untagged, path: ""))
        XCTAssertEqual(state.sidebarSelectedContainer, .untagged)

        // Tree mode keeps the shipped handoffs.
        state.setSidebarLayoutForInternalTesting(.tree)
        state.activateSidebarShortcut(
            SidebarShortcut(kind: .tag, path: "tagged"))
        XCTAssertEqual(state.sidebarFilterModel.committedQuery, "#tagged")
        state.sidebarFilterModel.escapeInField()
    }

    func testFileShortcutsAndRecentsStayLeavesInDualPane() async throws {
        let state = makeState()
        try openVault(state, named: "leaves", files: ["P/note.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.sidebarSelectedContainer = .folder(path: "P")

        state.activateSidebarShortcut(
            SidebarShortcut(kind: .file, path: "P/note.md"))
        await state.noteLoadTask?.value
        XCTAssertEqual(
            state.selectedFilePath, "P/note.md",
            "a file shortcut opens through the normal seam")
        XCTAssertEqual(
            state.sidebarSelectedContainer, .folder(path: "P"),
            "leaf activation never retargets the list container")
    }

    func testTagRowActivationIsLayoutAware() throws {
        let state = makeState()
        try openVault(state, named: "tagrows", files: ["a.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.activateSidebarTagScope(full: "projects/reading")
        XCTAssertEqual(
            state.sidebarSelectedContainer, .tag(full: "projects/reading"))
        state.activateSidebarUntaggedScope()
        XCTAssertEqual(state.sidebarSelectedContainer, .untagged)

        state.setSidebarLayoutForInternalTesting(.tree)
        state.activateSidebarTagScope(full: "projects/reading")
        XCTAssertEqual(
            state.sidebarFilterModel.committedQuery, "#projects/reading")
        state.sidebarFilterModel.escapeInField()
        state.activateSidebarUntaggedScope()
        XCTAssertTrue(state.sidebarFilterModel.untaggedScope)
        state.sidebarFilterModel.escapeInField()
    }

    // MARK: - Container mirror (rule 3)

    func testContainingContainerForOpenedFiles() {
        XCTAssertEqual(
            SidebarContainer.containing(filePath: "P/Q/note.md"),
            .folder(path: "P/Q"))
        XCTAssertEqual(
            SidebarContainer.containing(filePath: "top.md"),
            .folder(path: ""))
    }

    // MARK: - Folders-only projection (rule 2)

    func testFoldersOnlyProjectionSuppressesFilesAndFollowsExpansion() throws {
        let state = makeState()
        try openVault(
            state, named: "proj",
            files: ["A/inner/deep.md", "A/x.md", "B/y.md", "top.md"])
        let tree = FileTreeViewModel()
        tree.bind(to: try XCTUnwrap(state.currentSession))
        tree.loadRoot()
        var rows = SidebarDualPaneView.foldersOnlyRows(tree: tree)
        XCTAssertEqual(
            rows.map(\.node.path), ["A", "B"],
            "files never appear; collapsed folders do not recurse")

        if let a = tree.rootLevel.first(where: { $0.path == "A" }) {
            tree.toggle(a)
        }
        rows = SidebarDualPaneView.foldersOnlyRows(tree: tree)
        XCTAssertEqual(
            rows.map(\.node.path), ["A", "A/inner", "B"],
            "expanded folders recurse into child DIRECTORIES only")
    }

    // MARK: - Focus rules (rule 4)

    func testRightArrowTransferMatrix() {
        XCTAssertEqual(
            SidebarDualPaneFocus.rightArrow(
                isContainer: true, hasDisclosure: true, isExpanded: false),
            .disclose,
            "inside-tree disclosure keeps priority")
        XCTAssertEqual(
            SidebarDualPaneFocus.rightArrow(
                isContainer: true, hasDisclosure: true, isExpanded: true),
            .moveToList)
        XCTAssertEqual(
            SidebarDualPaneFocus.rightArrow(
                isContainer: true, hasDisclosure: false, isExpanded: false),
            .moveToList,
            "nothing to disclose hands focus to the list")
        XCTAssertEqual(
            SidebarDualPaneFocus.rightArrow(
                isContainer: false, hasDisclosure: false, isExpanded: false),
            .stay,
            "leaves never enter or retarget the list")
        XCTAssertTrue(SidebarDualPaneFocus.leftArrowReturnsToNavigation())
    }

    // MARK: - Divider persistence (rule 2)

    func testDividerFractionClampsAndRoundTrips() {
        let defaults = freshDefaults()
        XCTAssertEqual(
            SidebarDualPaneDivider.load(from: defaults),
            SidebarDualPaneDivider.defaultFraction,
            "unset loads the default")
        SidebarDualPaneDivider.store(0.05, in: defaults)
        XCTAssertEqual(
            SidebarDualPaneDivider.load(from: defaults),
            SidebarDualPaneDivider.minimumFraction,
            "stores clamp so neither pane can collapse")
        SidebarDualPaneDivider.store(0.63, in: defaults)
        XCTAssertEqual(SidebarDualPaneDivider.load(from: defaults), 0.63)
    }

    // MARK: - Container list contents + filter scoping (rule 5)

    func testContainerListLoadsScopedContentsPerContainer() async throws {
        let state = makeState()
        try openVault(
            state, named: "list",
            files: ["P/one.md", "P/two.md", "Q/three.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        let model = state.sidebarListPaneModel

        model.show(.folder(path: "P"))
        await model.drainTaskForTesting?.value
        XCTAssertEqual(
            model.fileSummaries.map(\.path), ["P/one.md", "P/two.md"],
            "folder containers use the scoped listing")

        model.show(.tag(full: "tagged"))
        await model.drainTaskForTesting?.value
        XCTAssertEqual(
            model.fileSummaries.count, 3, "tag containers use the tag query")

        model.show(nil)
        XCTAssertNil(model.container, "clearing shows the empty state")
        XCTAssertTrue(model.rows.isEmpty)
    }

    func testFilterScopesToTheSelectedContainerInDualPane() throws {
        let state = makeState()
        try openVault(
            state, named: "scope",
            files: ["P/alpha.md", "Q/alpha-two.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.sidebarSelectedContainer = .folder(path: "P")

        state.sidebarFilterModel.fieldText = "alpha"
        state.sidebarFilterModel.commitNow()
        XCTAssertEqual(
            state.sidebarFilterModel.results?.files.map(\.path),
            ["P/alpha.md"],
            "the field scopes to the selected folder container")
        state.sidebarFilterModel.escapeInField()

        // Tree mode: the same query is vault-wide.
        state.setSidebarLayoutForInternalTesting(.tree)
        state.sidebarFilterModel.fieldText = "alpha"
        state.sidebarFilterModel.commitNow()
        XCTAssertEqual(
            state.sidebarFilterModel.results?.files.count, 2)
        state.sidebarFilterModel.escapeInField()
    }

    // MARK: - Review-round regressions

    func testDualPaneSelectionOwnsTheActionSnapshot() throws {
        let state = makeState()
        try openVault(state, named: "snapshot", files: ["P/a.md", "P/b.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)

        state.sidebarSelectedContainer = .folder(path: "P")
        state.publishDualPaneSelectionSnapshot()
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items.map(\.path), ["P"],
            "a selected folder container is a single-folder target")
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items.first?.isDirectory, true)

        state.sidebarListPaneModel.show(.folder(path: "P"))
        state.sidebarDualPaneListSelection = "P/a.md"
        state.publishDualPaneSelectionSnapshot()
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items.map(\.path), ["P/a.md"],
            "a selected list row is a single-file target")
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items.first?.isMarkdown, true)
    }

    func testMirrorSelectsContainingFolderAndListRow() async throws {
        let state = makeState()
        try openVault(state, named: "mirror", files: ["P/note.md", "Q/x.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.sidebarSelectedContainer = .folder(path: "Q")
        state.sidebarListPaneModel.show(.folder(path: "Q"))
        await state.sidebarListPaneModel.drainTaskForTesting?.value

        state.mirrorOpenedFileIntoDualPane("P/note.md")
        await state.sidebarListPaneModel.drainTaskForTesting?.value
        XCTAssertEqual(
            state.sidebarSelectedContainer, .folder(path: "P"),
            "the mirror selects the CONTAINING folder container")
        XCTAssertEqual(state.sidebarDualPaneListSelection, "P/note.md")
        XCTAssertEqual(
            state.sidebarListPaneModel.fileSummaries.map(\.path),
            ["P/note.md"],
            "the list pane follows the mirrored container")
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items.map(\.path), ["P/note.md"])
    }

    func testContainerSwitchClearsPaneSelectionsBeforePublish() throws {
        // Review round (high): switching containers must clear both
        // pane selections at the owner — the follow-up publish targets
        // the NEW container, never rows of the old one.
        let state = makeState()
        try openVault(
            state, named: "switch-clear", files: ["P/a.md", "Q/b.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.sidebarSelectedContainer = .folder(path: "P")
        state.sidebarDualPaneMultiSelection = ["P/a.md"]
        state.sidebarDualPaneListSelection = "P/a.md"

        state.sidebarSelectedContainer = .folder(path: "Q")
        XCTAssertTrue(state.sidebarDualPaneMultiSelection.isEmpty)
        XCTAssertNil(state.sidebarDualPaneListSelection)
        state.publishDualPaneSelectionSnapshot()
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items.map(\.path), ["Q"],
            "the snapshot follows the new container, not the old rows")
    }

    func testLeavingDualPaneReleasesSnapshotOwnership() async throws {
        // Review round (high): a dual-pane batch must not stay the
        // action target after the pane unmounts — leaving the layout
        // empties the snapshot until the tree republishes.
        let state = makeState()
        try openVault(
            state, named: "layout-handoff", files: ["P/a.md", "P/b.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.sidebarSelectedContainer = .folder(path: "P")
        state.sidebarListPaneModel.show(.folder(path: "P"))
        await state.sidebarListPaneModel.drainTaskForTesting?.value
        state.sidebarDualPaneMultiSelection = ["P/a.md", "P/b.md"]
        state.publishDualPaneSelectionSnapshot()
        XCTAssertEqual(state.sidebarSelectionSnapshot?.items.count, 2)

        state.setSidebarLayout(.tree)
        XCTAssertTrue(state.sidebarDualPaneMultiSelection.isEmpty)
        XCTAssertNil(state.sidebarDualPaneListSelection)
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items.isEmpty, true,
            "the unmounted pane's batch cannot remain the action target")
    }

    func testMirrorOwnsThePaneSelectionAsASingleElementSet() async throws {
        let state = makeState()
        try openVault(state, named: "mirror-set", files: ["P/note.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.mirrorOpenedFileIntoDualPane("P/note.md")
        XCTAssertEqual(
            state.sidebarDualPaneMultiSelection, ["P/note.md"],
            "the mirrored row IS the pane selection")
        XCTAssertEqual(state.sidebarDualPaneListSelection, "P/note.md")
    }

    func testSpacedTagContainerScopesExactlyEverywhere() async throws {
        // FL-15 red team (high): a frontmatter tag containing
        // whitespace must scope the list drain AND the composed filter
        // out-of-band — the textual form would split into tag
        // `project` + name term `alpha` and target the wrong files.
        let state = makeState()
        let vault = root.appendingPathComponent("spaced")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        for (rel, body) in [
            ("a/spaced.md", "---\ntags: [\"project alpha\"]\n---\nbody\n"),
            ("a/truncated.md", "#project\n"),
            ("b/alpha name.md", "no tags\n"),
        ] {
            let url = vault.appendingPathComponent(rel)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(),
                withIntermediateDirectories: true)
            try body.write(to: url, atomically: true, encoding: .utf8)
        }
        state.openVault(at: vault)
        _ = try XCTUnwrap(state.currentSession).scanInitial(
            cancel: CancelToken())
        state.setSidebarLayoutForInternalTesting(.dualPane)

        state.sidebarSelectedContainer = .tag(full: "project alpha")
        state.sidebarListPaneModel.show(.tag(full: "project alpha"))
        await state.sidebarListPaneModel.drainTaskForTesting?.value
        XCTAssertEqual(
            state.sidebarListPaneModel.fileSummaries.map(\.path),
            ["a/spaced.md"],
            "the drain scopes the WHOLE tag, not its first token")

        // A committed query composes with the container's scope.
        state.sidebarFilterModel.fieldText = "spaced"
        state.sidebarFilterModel.commitNow()
        XCTAssertEqual(
            state.sidebarFilterModel.results?.files.map(\.path),
            ["a/spaced.md"],
            "the composed filter ANDs the query with the scope")
        state.sidebarFilterModel.fieldText = "alpha"
        state.sidebarFilterModel.commitNow()
        XCTAssertEqual(
            state.sidebarFilterModel.results?.files.map(\.path), [],
            "`alpha` matches no NAME inside the tag scope — the "
                + "name-matching untagged file stays out")
    }

    func testHiddenPreFilterSelectionIsReconciledAway() async throws {
        // FL-15 red team (high): a selection excluded by a committed
        // filter must not remain the published batch target.
        let state = makeState()
        try openVault(
            state, named: "hidden-sel", files: ["P/a.md", "P/b.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.sidebarSelectedContainer = .folder(path: "P")
        state.sidebarListPaneModel.show(.folder(path: "P"))
        await state.sidebarListPaneModel.drainTaskForTesting?.value
        state.sidebarDualPaneMultiSelection = ["P/a.md"]
        state.publishDualPaneSelectionSnapshot()
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items.map(\.path), ["P/a.md"])

        state.sidebarFilterModel.fieldText = "zzz-no-match"
        state.sidebarFilterModel.commitNow()
        XCTAssertTrue(state.sidebarFilterModel.isActive)
        state.reconcileDualPaneSelectionWithVisibleRows()
        XCTAssertTrue(state.sidebarDualPaneMultiSelection.isEmpty)
        XCTAssertNotEqual(
            state.sidebarSelectionSnapshot?.items.map(\.path), ["P/a.md"],
            "the hidden row is no longer the action target")
    }

    func testMutationTokenTicksOnIdenticalAnnouncementCopy() throws {
        let state = makeState()
        try openVault(state, named: "token", files: ["a.md"])
        let before = state.sidebarMutationToken
        state.postMutationAnnouncement("Pinned.")
        state.postMutationAnnouncement("Pinned.")
        XCTAssertEqual(
            state.sidebarMutationToken, before + 2,
            "identical copy still ticks the funnel counter")
    }

    func testDividerDragMathIsAnchorBasedNotCumulative() {
        // Review round: translation is cumulative from gesture start —
        // three callbacks of 10/20/30pt must land at anchor+30pt, not
        // anchor+60pt.
        let anchor = 0.5
        var fraction = anchor
        for translation in [10.0, 20.0, 30.0] {
            fraction = SidebarDualPaneDivider.dragged(
                fromAnchor: anchor, translation: translation, totalHeight: 300)
        }
        XCTAssertEqual(fraction, 0.6, accuracy: 0.0001)
        XCTAssertEqual(
            SidebarDualPaneDivider.dragged(
                fromAnchor: 0.5, translation: -1000, totalHeight: 300),
            SidebarDualPaneDivider.minimumFraction,
            "drag output clamps")
    }

    // MARK: - Vault lifecycle

    func testContainerStateDiesWithTheVaultButLayoutSurvives() async throws {
        let defaults = freshDefaults()
        let state = makeState(defaults: defaults)
        try openVault(state, named: "life", files: ["P/a.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.sidebarSelectedContainer = .folder(path: "P")
        state.sidebarListPaneModel.show(.folder(path: "P"))
        await state.sidebarListPaneModel.drainTaskForTesting?.value
        XCTAssertFalse(state.sidebarListPaneModel.fileSummaries.isEmpty)

        state.closeVault()
        XCTAssertNil(state.sidebarSelectedContainer)
        XCTAssertNil(state.sidebarListPaneModel.container)
        XCTAssertTrue(state.sidebarListPaneModel.rows.isEmpty)
        XCTAssertEqual(
            state.sidebarLayout, .dualPane,
            "the layout gate is device-local and survives the vault")
    }
}
