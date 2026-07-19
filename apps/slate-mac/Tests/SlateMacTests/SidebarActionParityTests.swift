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
            try publish(
                state, items, focusedPath: items.first?.path,
                creationParent: "P")
            state.setSidebarLayout(.tree)
            let tree = state.sidebarActionProjection(surface: .contextMenu)
            state.setSidebarLayout(.dualPane)
            let dual = state.sidebarActionProjection(surface: .contextMenu)
            XCTAssertEqual(
                tree, dual,
                "the projection is selection-driven; the layout gate must "
                    + "never add, remove, or re-reason an action "
                    + "(items: \(items.map(\.path)))")
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

        // A selected folder container (the list-pane header's context).
        // "Use Vault Default Sort" only projects once the folder HAS a
        // sort override to clear (FL-06 availability), so set one first.
        try publish(
            state,
            [SidebarSelectionItem(
                path: "P", isDirectory: true, isMarkdown: false)],
            focusedPath: "P", creationParent: "P")
        _ = try state.dispatchSidebarAction(
            id: SlateCommandID.sidebarSortNameDesc)
        for layout in [SidebarLayoutMode.tree, .dualPane] {
            state.setSidebarLayout(layout)
            let projected = Set(
                state.sidebarActionProjection(surface: .contextMenu)
                    .map(\.id))
            for id in displayMenuSortIDs {
                XCTAssertTrue(
                    projected.contains(id),
                    "\(id) missing from the \(layout) folder projection — "
                        + "the display menu would drift from the tree")
            }
        }
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
