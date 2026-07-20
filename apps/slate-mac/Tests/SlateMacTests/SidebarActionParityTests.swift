// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// §FL-D (#669): dual-pane action parity is STRUCTURAL — the list pane
/// mounts the same shared components (single-file context-menu groups,
/// display menu, folder override items) the tree uses, and the catalog
/// projection is selection-driven, never layout-driven. These tests pin
/// the drift-proof enumeration: every verb the list pane surfaces is a
/// catalog action the tree's contextual projection also carries, and
/// flipping the layout changes no evaluation.
@MainActor
final class SidebarActionParityTests: XCTestCase {
    private var root: URL!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-parity-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: root, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func openVault(
        named name: String, files: [String]
    ) throws -> AppState {
        let vault = root.appendingPathComponent(name)
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        for path in files {
            let url = vault.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(),
                withIntermediateDirectories: true)
            try "# \(path)\n".write(to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("\(name)-recents.json")),
            externalOpener: { _ in true },
            announcer: AppKitAnnouncementPoster(),
            sidebarSectionDefaults: UserDefaults(
                suiteName: "parity-\(name)-\(UUID().uuidString)")!)
        state.openVault(at: vault)
        _ = try XCTUnwrap(state.currentSession).scanInitial(
            cancel: CancelToken())
        return state
    }

    private func publish(
        _ state: AppState, _ items: [SidebarSelectionItem],
        focusedPath: String? = nil, creationParent: String = ""
    ) throws {
        _ = state.publishSidebarSelectionSnapshot(
            SidebarSelectionSnapshot(
                sessionIdentity: ObjectIdentifier(
                    try XCTUnwrap(state.currentSession)),
                items: items, focusedPath: focusedPath,
                creationParent: creationParent))
    }

    /// The display menu's Sort/Grouping verbs, exactly as the view
    /// enumerates them. Duplicated here on purpose: if the view ever
    /// grows a verb this list misses, the projection-membership test
    /// below is the enumeration reviewers check against §FL-D.
    private let displayMenuSortIDs = [
        SlateCommandID.sidebarSortNameAsc, SlateCommandID.sidebarSortNameDesc,
        SlateCommandID.sidebarSortCreatedDesc,
        SlateCommandID.sidebarSortCreatedAsc,
        SlateCommandID.sidebarSortModifiedDesc,
        SlateCommandID.sidebarSortModifiedAsc,
        SlateCommandID.sidebarToggleDateGrouping,
        SlateCommandID.sidebarUseVaultDefaultSort,
    ]

    // MARK: - Layout never changes the projection

    func testContextMenuProjectionIsLayoutInvariant() throws {
        let state = try openVault(
            named: "invariant", files: ["P/a.md", "P/b.md"])

        for items in [
            [SidebarSelectionItem(
                path: "P/a.md", isDirectory: false, isMarkdown: true)],
            [SidebarSelectionItem(
                path: "P", isDirectory: true, isMarkdown: false)],
            [
                SidebarSelectionItem(
                    path: "P/a.md", isDirectory: false, isMarkdown: true),
                SidebarSelectionItem(
                    path: "P/b.md", isDirectory: false, isMarkdown: true),
            ],
        ] {
            let snapshot = SidebarSelectionSnapshot(
                sessionIdentity: ObjectIdentifier(
                    try XCTUnwrap(state.currentSession)),
                items: items, focusedPath: items.first?.path,
                creationParent: "P")
            state.setSidebarLayout(.tree)
            let tree = state.sidebarActionProjection(
                surface: .contextMenu, snapshot: snapshot)
            state.setSidebarLayout(.dualPane)
            let dual = state.sidebarActionProjection(
                surface: .contextMenu, snapshot: snapshot)
            XCTAssertEqual(
                tree, dual,
                "projecting a GIVEN snapshot is layout-independent "
                    + "(items: \(items.map(\.path))); the transition's "
                    + "ownership handoff is covered separately")
        }
    }

    // MARK: - List-pane verbs ⊆ tree contextual projection (§FL-D)

    func testDisplayMenuVerbsAreCatalogBackedAndProjectForFolders() throws {
        let state = try openVault(named: "menu", files: ["P/a.md"])
        let catalogIDs = Set(SidebarActionCatalog.actions.map(\.id))
        for id in displayMenuSortIDs {
            XCTAssertTrue(
                catalogIDs.contains(id),
                "\(id) must be a catalog action — no second command dialect")
        }

        // The menu projects against its CONTAINER snapshot (review
        // round). "Use Vault Default Sort" only projects once the
        // folder HAS a sort override to clear (FL-06 availability), so
        // set one through the same container-scoped intent first.
        let containerSnapshot = try XCTUnwrap(
            state.sidebarContainerActionSnapshot(for: .folder(path: "P")))
        let sortDesc = try XCTUnwrap(
            state.sidebarActionProjection(
                surface: .contextMenu, snapshot: containerSnapshot
            ).first { $0.id == SlateCommandID.sidebarSortNameDesc })
        _ = try state.dispatchSidebarAction(try XCTUnwrap(sortDesc.intent))
        for layout in [SidebarLayoutMode.tree, .dualPane] {
            state.setSidebarLayout(layout)
            let projected = Set(
                state.sidebarActionProjection(
                    surface: .contextMenu, snapshot: containerSnapshot
                ).map(\.id))
            for id in displayMenuSortIDs {
                XCTAssertTrue(
                    projected.contains(id),
                    "\(id) missing from the \(layout) folder projection — "
                        + "the display menu would drift from the tree")
            }
        }
    }

    func testDisplayMenuTargetsTheContainerNotTheAmbientRow() throws {
        // Review round (medium): with a file row owning the ambient
        // snapshot, the display menu's Sort must override the shown
        // CONTAINER's folder — never the ambient row's parent.
        let state = try openVault(
            named: "container-target", files: ["P/a.md", "Q/b.md"])
        try publish(
            state,
            [SidebarSelectionItem(
                path: "Q/b.md", isDirectory: false, isMarkdown: true)],
            focusedPath: "Q/b.md", creationParent: "Q")

        let snapshot = try XCTUnwrap(
            state.sidebarContainerActionSnapshot(for: .folder(path: "P")))
        let evaluations = state.sidebarActionProjection(
            surface: .contextMenu, snapshot: snapshot)
        let sort = try XCTUnwrap(
            evaluations.first {
                $0.id == SlateCommandID.sidebarSortNameDesc
            })
        _ = try state.dispatchSidebarAction(try XCTUnwrap(sort.intent))
        XCTAssertNotNil(
            state.sidebarOrganization.prefs.folderOverrides["P"]?.sort,
            "the override lands on the container the menu is labeled with")
        XCTAssertNil(
            state.sidebarOrganization.prefs.folderOverrides["Q"]?.sort,
            "the ambient row's folder is untouched")

        // Tag/Untagged containers organize at the vault level.
        let vaultScope = try XCTUnwrap(
            state.sidebarContainerActionSnapshot(for: .untagged))
        XCTAssertTrue(vaultScope.items.isEmpty)
    }

    func testDualPaneSourceMountsRenameEditorAndSharedSelectionHandler()
        throws
    {
        // Review round (mediums): Rename must not be a dead verb — the
        // pane renders the SAME RenameField the tree uses — and the
        // selection handler must sit on the shared pane so filter
        // results acquire ownership too. Value-typed refresh triggers
        // replace announcement copy.
        let source = try Self.dualPaneSource()
        XCTAssertTrue(
            source.contains("RenameField("),
            "dual-pane rows render the shared rename editor")
        XCTAssertTrue(source.contains("appState.commitPendingRename"))
        XCTAssertEqual(
            source.components(
                separatedBy: "onChange(of: appState.sidebarDualPaneMultiSelection)"
            ).count - 1,
            1,
            "ONE shared selection handler covers container and filter lists")
        XCTAssertFalse(
            source.contains("onChange(of: appState.lastMutationAnnouncement)"),
            "refresh triggers are value-typed, never announcement copy")
        XCTAssertTrue(
            source.contains("onChange(of: appState.sidebarMutationToken)"),
            "the FL-15 token funnel covers structural, organization, "
                + "and content mutations alike")
    }

    func testLayoutIndependentSurfacesAreHoistedAboveTheBranch() throws {
        // FL-15 red team (mediums): the tag-editor sheet and the
        // mutation-refresh funnel must attach ABOVE the tree/dual-pane
        // branch — attached to the tree mount they go dead the moment
        // dual-pane mounts.
        let tree = try Self.source("FileTreeSidebar.swift")
        let sheetIndex = try XCTUnwrap(
            tree.range(of: "get: { appState.sidebarTagEditorRequest }")
        ).lowerBound
        let navTitleIndex = try XCTUnwrap(
            tree.range(of: ".navigationTitle(\"Files\")")).lowerBound
        XCTAssertLessThan(
            navTitleIndex, sheetIndex,
            "the sheet mounts on the shared body chain, after "
                + "navigationTitle — not inside a layout branch")
        XCTAssertEqual(
            tree.components(separatedBy: "sidebarTagEditorRequest }")
                .count - 1,
            1, "exactly one presenter")
        XCTAssertFalse(
            tree.contains("onChange(of: appState.lastMutationAnnouncement)"),
            "refresh funnels ride the token, never announcement copy")
        XCTAssertTrue(
            tree.contains("onChange(of: appState.sidebarMutationToken)"))

        let dual = try Self.dualPaneSource()
        XCTAssertEqual(
            dual.components(separatedBy: "RenameField(").count - 1, 2,
            "both file AND folder rows render the shared rename editor")
        XCTAssertTrue(
            dual.contains("onChange(of: appState.sidebarMutationToken)"))
        XCTAssertTrue(
            dual.contains("reconcileDualPaneSelectionWithVisibleRows"),
            "filter-surface changes reconcile the selection")
        XCTAssertTrue(
            dual.contains(
                "onChange(of: filterModel.results.map { $0.files.map(\\.path) })"
            ),
            "the observed value preserves nil-vs-empty — a first "
                + "zero-result page must still fire reconciliation")
        XCTAssertFalse(
            dual.contains(".map(\\.path) } ?? []"),
            "collapsing nil results into [] regressed the zero-result case")
        XCTAssertTrue(
            dual.contains("onChange(of: appState.treeMutation?.token)"),
            "the shared VM refetches from this surface while the tree "
                + "list's consumer is unmounted")
        XCTAssertTrue(dual.contains("tree.authoritativeTreeInvalidation()"))
    }

    private static func source(_ name: String) throws -> String {
        var cursor = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/\(name)")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        throw XCTSkip("\(name) not found")
    }

    private static func dualPaneSource() throws -> String {
        var cursor = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/Sidebar/SidebarDualPaneView.swift")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        throw XCTSkip("SidebarDualPaneView.swift not found")
    }

    func testLayoutToggleProjectsOnEverySelectionShape() throws {
        // The toggle is .anySelection: enabled with no vault selection,
        // a file, a folder, or a batch — parity with the palette.
        let state = try openVault(named: "toggle", files: ["P/a.md"])
        for items in [
            [],
            [SidebarSelectionItem(
                path: "P/a.md", isDirectory: false, isMarkdown: true)],
            [SidebarSelectionItem(
                path: "P", isDirectory: true, isMarkdown: false)],
        ] {
            try publish(state, items)
            let evaluation = state.sidebarActionProjection(
                surface: .commandPalette
            ).first { $0.id == SlateCommandID.sidebarToggleLayout }
            XCTAssertNotNil(
                evaluation,
                "toggle absent for items \(items.map(\.path))")
            XCTAssertNil(evaluation?.disabledReason)
        }
    }
}
