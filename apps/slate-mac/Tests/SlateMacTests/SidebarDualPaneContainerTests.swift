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

    func testNoUserFacingToggleExistsWhileGated() {
        // Rule 1: no palette/menu command may expose the layout until
        // FL-14. The catalog is the registry of user-facing sidebar
        // commands — assert nothing mentions the layout.
        XCTAssertFalse(
            SidebarActionCatalog.actions.contains {
                $0.id.localizedCaseInsensitiveContains("layout")
                    || $0.id.localizedCaseInsensitiveContains("dualPane")
                    || $0.label.localizedCaseInsensitiveContains("dual")
            },
            "the dual-pane toggle stays internal in FL-13")
        XCTAssertFalse(
            SlateCommandID.all.contains {
                $0.localizedCaseInsensitiveContains("layout")
                    || $0.localizedCaseInsensitiveContains("dualPane")
            })
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

    func testContainerListLoadsScopedContentsPerContainer() throws {
        let state = makeState()
        try openVault(
            state, named: "list",
            files: ["P/one.md", "P/two.md", "Q/three.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        let model = state.sidebarContainerListModel

        model.show(.folder(path: "P"))
        XCTAssertEqual(
            model.page?.files.map(\.path), ["P/one.md", "P/two.md"],
            "folder containers use the scoped listing")

        model.show(.tag(full: "tagged"))
        XCTAssertEqual(
            model.page?.files.count, 3, "tag containers use the tag query")

        model.show(nil)
        XCTAssertNil(model.page, "clearing shows the empty state")
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

    // MARK: - Vault lifecycle

    func testContainerStateDiesWithTheVaultButLayoutSurvives() throws {
        let defaults = freshDefaults()
        let state = makeState(defaults: defaults)
        try openVault(state, named: "life", files: ["P/a.md"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.sidebarSelectedContainer = .folder(path: "P")
        state.sidebarContainerListModel.show(.folder(path: "P"))
        XCTAssertNotNil(state.sidebarContainerListModel.page)

        state.closeVault()
        XCTAssertNil(state.sidebarSelectedContainer)
        XCTAssertNil(state.sidebarContainerListModel.page)
        XCTAssertEqual(
            state.sidebarLayout, .dualPane,
            "the layout gate is device-local and survives the vault")
    }
}
