// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// FL7-2 (#669): the complete dual-pane list pane. Container → content
/// matrix over the REAL core contracts (one-level listing for the folder
/// default, FL-08 scope for descendants, tag query, Untagged), the
/// drain-to-completion machinery (paging, cap, stale-drop, errors) over
/// injected dependencies, the client-side pins/sort/group projection,
/// per-folder display overrides on both surfaces, and the multi-select
/// batch snapshot.
@MainActor
final class SidebarListPaneTests: XCTestCase {
    private var root: URL!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-list-pane-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: root, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    /// `files` maps vault-relative path → markdown body, so individual
    /// fixtures control which files carry tags.
    private func openVault(
        named name: String, files: [String: String]
    ) throws -> AppState {
        let vault = root.appendingPathComponent(name)
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        for (path, body) in files {
            let url = vault.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(),
                withIntermediateDirectories: true)
            try body.write(to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("\(name)-recents.json")),
            externalOpener: { _ in true },
            announcer: AppKitAnnouncementPoster(),
            sidebarSectionDefaults: UserDefaults(
                suiteName: "list-pane-\(name)-\(UUID().uuidString)")!)
        state.openVault(at: vault)
        _ = try XCTUnwrap(state.currentSession).scanInitial(
            cancel: CancelToken())
        return state
    }

    private func shown(
        _ state: AppState, _ container: SidebarContainer
    ) async -> [String] {
        state.sidebarListPaneModel.show(container)
        await state.sidebarListPaneModel.drainTaskForTesting?.value
        return state.sidebarListPaneModel.fileSummaries.map(\.path)
    }

    // MARK: - Container → content matrix (rule 2)

    func testFolderContainerListsImmediateChildrenOnly() async throws {
        let state = try openVault(
            named: "immediate",
            files: [
                "root.md": "# r",
                "P/a.md": "# a",
                "P/Sub/deep.md": "# d",
            ])
        let folderPaths = await shown(state, .folder(path: "P"))
        XCTAssertEqual(
            folderPaths, ["P/a.md"],
            "the folder default is ONE level — the FL-13 lean model's "
                + "silent subtree-wide listing is gone")
        let rootPaths = await shown(state, .folder(path: ""))
        XCTAssertEqual(
            rootPaths, ["root.md"],
            "the vault root is a plain one-level folder container")
    }

    func testDescendantsOverrideExpandsFolderToSubtree() async throws {
        let state = try openVault(
            named: "descend",
            files: ["P/a.md": "# a", "P/Sub/deep.md": "# d"])
        state.setSidebarFolderDescendantsOverride(
            folder: "P", includeDescendants: true)
        let subtree = await shown(state, .folder(path: "P"))
        XCTAssertEqual(
            subtree, ["P/a.md", "P/Sub/deep.md"],
            "the descendants override routes through the FL-08 scope "
                + "(name-ordered, like every scoped listing)")

        state.setSidebarFolderDescendantsOverride(
            folder: "P", includeDescendants: false)
        let immediate = await shown(state, .folder(path: "P"))
        XCTAssertEqual(
            immediate, ["P/a.md"],
            "clearing the override returns to the one-level default")
    }

    func testTagAndUntaggedContainersDrainTheirScopes() async throws {
        let state = try openVault(
            named: "tags",
            files: [
                "P/tagged.md": "# t\n#project\n",
                "Q/also.md": "# q\n#project\n",
                "plain.md": "# plain\n",
            ])
        let tagged = await shown(state, .tag(full: "project"))
        XCTAssertEqual(
            tagged, ["Q/also.md", "P/tagged.md"],
            "tag containers run the vault-wide tag query (name-ordered)")
        let untagged = await shown(state, .untagged)
        XCTAssertEqual(
            untagged, ["plain.md"],
            "the Untagged container runs the FL-11 untagged scope")
    }

    func testEmptyFolderIsAnEmptyStateNotAnError() async throws {
        let state = try openVault(
            named: "empty", files: ["only.md": "# x"])
        _ = await shown(state, .folder(path: ""))
        state.sidebarListPaneModel.show(.folder(path: "Missing"))
        await state.sidebarListPaneModel.drainTaskForTesting?.value
        let model = state.sidebarListPaneModel
        XCTAssertTrue(model.fileSummaries.isEmpty)
        XCTAssertNil(model.loadError)
        XCTAssertFalse(model.isLoading)
        XCTAssertEqual(model.fileCount, 0)
    }

    // MARK: - Drain machinery (injected dependencies)

    private struct StubError: Error, LocalizedError {
        var errorDescription: String? { "stub failed" }
    }

    private func summary(_ path: String) -> FileSummary {
        FileSummary(
            path: path, name: (path as NSString).lastPathComponent,
            mtimeMs: 0, sizeBytes: 0, isMarkdown: true, displayName: nil,
            createdDate: nil, createdMs: nil, wordCount: nil, preview: nil,
            taskTotal: 0, taskOpen: 0)
    }

    /// A bare model whose folder listing serves synthetic pages:
    /// `pages[i]` is served for cursor i (nil cursor = 0). A `nil`
    /// entry in `next` marks the final page.
    private func syntheticModel(
        pages: @escaping (Int) -> (files: [FileSummary], next: String?)
    ) -> SidebarListPaneModel {
        let model = SidebarListPaneModel()
        model.bind(
            SidebarListPaneModel.Dependencies(
                performQuery: { _, _, _, _ in throw StubError() },
                performUntagged: { _ in throw StubError() },
                listLevel: { _, paging in
                    let index = paging.cursor.flatMap(Int.init) ?? 0
                    let page = pages(index)
                    return DirListing(
                        dirs: [],
                        files: FileSummaryPage(
                            items: page.files,
                            nextCursor: page.next,
                            totalFiltered: UInt64(page.files.count)))
                }),
            organization: { FileTreeViewModel.OrganizationContext() },
            deviceDefaults: { (2, .standard) })
        return model
    }

    func testDrainRunsAllPagesToCompletion() async {
        let pageSize = 500
        let model = syntheticModel { index in
            if index == 0 {
                let files = (0..<pageSize).map {
                    self.summary(String(format: "f/%04d.md", $0))
                }
                return (files, "1")
            }
            return ([self.summary("f/last.md")], nil)
        }
        model.show(.folder(path: "f"))
        await model.drainTaskForTesting?.value
        XCTAssertEqual(model.fileCount, pageSize + 1, "both pages drained")
        XCTAssertFalse(model.truncated)
        XCTAssertFalse(model.isLoading)
    }

    func testDrainStopsAtCapAndMarksTruncated() async {
        let model = syntheticModel { index in
            let files = (0..<500).map {
                self.summary(String(format: "f/%d-%03d.md", index, $0))
            }
            return (files, String(index + 1))
        }
        model.show(.folder(path: "f"))
        await model.drainTaskForTesting?.value
        XCTAssertEqual(model.fileCount, 10_000, "the drain cap bounds work")
        XCTAssertTrue(
            model.truncated,
            "capped results are LABELED truncated — never silently "
                + "presented as the complete container")
    }

    func testDrainCapsAnUnalignedFinalPageWithoutOvershoot() async {
        let model = syntheticModel { index in
            if index == 0 {
                let files = (0..<9_900).map {
                    self.summary(String(format: "f/first-%05d.md", $0))
                }
                return (files, "1")
            }
            let files = (0..<500).map {
                self.summary(String(format: "f/final-%03d.md", $0))
            }
            return (files, nil)
        }
        model.show(.folder(path: "f"))
        await model.drainTaskForTesting?.value
        XCTAssertEqual(model.fileCount, 10_000)
        XCTAssertTrue(model.truncated)
        XCTAssertEqual(model.fileSummaries.last?.path, "f/final-099.md")
    }

    func testSupersededShowNeverPublishesItsStaleResult() async {
        let model = syntheticModel { _ in
            ([self.summary("stale/marker.md")], nil)
        }
        model.show(.folder(path: "stale"))
        let staleDrain = model.drainTaskForTesting
        // Supersede before the first drain task gets to run.
        model.show(nil)
        await staleDrain?.value
        XCTAssertTrue(
            model.fileSummaries.isEmpty,
            "the superseded drain's result is dropped by the generation "
                + "guard — a cleared pane can never resurrect old rows")
        XCTAssertNil(model.container)
    }

    func testDependencyFailureSurfacesLoadError() async {
        let model = SidebarListPaneModel()
        model.bind(
            SidebarListPaneModel.Dependencies(
                performQuery: { _, _, _, _ in throw StubError() },
                performUntagged: { _ in throw StubError() },
                listLevel: { _, _ in throw StubError() }),
            organization: { FileTreeViewModel.OrganizationContext() },
            deviceDefaults: { (2, .standard) })
        model.show(.folder(path: "f"))
        await model.drainTaskForTesting?.value
        XCTAssertEqual(model.loadError, "stub failed")
        XCTAssertTrue(model.fileSummaries.isEmpty)
        XCTAssertFalse(model.isLoading)
    }

    func testContainerSwitchClearsRowsBeforeTheDrainSettles() async {
        // Review round (high): stale rows must never render under the
        // new container's header — the switch clears immediately.
        let model = syntheticModel { _ in
            ([self.summary("f/one.md")], nil)
        }
        model.show(.folder(path: "f"))
        await model.drainTaskForTesting?.value
        XCTAssertEqual(model.fileCount, 1)

        model.show(.folder(path: "g"))
        XCTAssertTrue(
            model.fileSummaries.isEmpty,
            "the old container's rows are gone BEFORE the new drain lands")
        XCTAssertEqual(model.fileCount, 0)
        XCTAssertFalse(model.truncated)
        await model.drainTaskForTesting?.value
        XCTAssertEqual(model.fileCount, 1)
    }

    func testSupersessionCancelsAStartedDrainMidFlight() async {
        // Review round (medium): the per-page yield makes cancellation
        // land MID-drain — a superseded endless drain must stop paging,
        // not run to the 10k cap.
        var pagesServed = 0
        let model = syntheticModel { index in
            pagesServed += 1
            let files = (0..<500).map {
                self.summary(String(format: "f/%d-%03d.md", index, $0))
            }
            return (files, String(index + 1))
        }
        model.show(.folder(path: "f"))
        let started = model.drainTaskForTesting
        await Task.yield()
        model.show(nil)
        await started?.value
        XCTAssertLessThan(
            pagesServed, 20,
            "cancellation landed mid-drain, not after the full cap walk")
        XCTAssertTrue(model.fileSummaries.isEmpty)
        XCTAssertNil(model.container)
    }

    func testReconcilePrunesSelectionsToVisibleRows() async throws {
        // Review round (high): after a refresh, selections must be a
        // subset of the visible rows — pruned and republished.
        let state = try openVault(
            named: "reconcile", files: ["P/a.md": "# a", "P/b.md": "# b"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.sidebarSelectedContainer = .folder(path: "P")
        _ = await shown(state, .folder(path: "P"))

        state.sidebarDualPaneMultiSelection = ["P/a.md", "P/ghost.md"]
        state.sidebarDualPaneListSelection = "P/ghost.md"
        state.reconcileDualPaneSelectionWithVisibleRows()
        XCTAssertEqual(state.sidebarDualPaneMultiSelection, ["P/a.md"])
        XCTAssertNil(state.sidebarDualPaneListSelection)
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items.map(\.path), ["P/a.md"],
            "the republished snapshot matches the pruned selection")

        state.sidebarDualPaneMultiSelection = ["P/gone.md"]
        state.reconcileDualPaneSelectionWithVisibleRows()
        XCTAssertTrue(state.sidebarDualPaneMultiSelection.isEmpty)
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items.map(\.path), ["P"],
            "a fully pruned selection falls back to the container target")
    }

    func testIdenticalAnnouncementCopyStillChangesOrganizationState() throws {
        // Review round (medium): the refresh trigger is the VALUE of
        // sidebarOrganization, never announcement copy — two pins with
        // identical "Pinned." text must produce two distinct states.
        let state = try openVault(
            named: "org-token", files: ["P/a.md": "# a", "P/b.md": "# b"])
        func pin(_ path: String) throws {
            _ = state.publishSidebarSelectionSnapshot(
                SidebarSelectionSnapshot(
                    sessionIdentity: ObjectIdentifier(
                        try XCTUnwrap(state.currentSession)),
                    items: [
                        SidebarSelectionItem(
                            path: path, isDirectory: false, isMarkdown: true)
                    ],
                    focusedPath: path, creationParent: "P"))
            _ = try state.dispatchSidebarAction(
                id: SlateCommandID.sidebarPinNote)
        }
        try pin("P/a.md")
        let afterFirst = state.sidebarOrganization
        let firstCopy = state.lastMutationAnnouncement
        try pin("P/b.md")
        XCTAssertEqual(
            state.lastMutationAnnouncement, firstCopy,
            "identical copy is exactly the case the old trigger missed")
        XCTAssertNotEqual(
            state.sidebarOrganization, afterFirst,
            "the Equatable state the view observes changed anyway")
    }

    // MARK: - Organize projection (rule 3)

    func testPinnedNotesLeadWithAHeaderRow() async throws {
        let state = try openVault(
            named: "pins",
            files: ["P/alpha.md": "# a", "P/beta.md": "# b"])
        _ = state.publishSidebarSelectionSnapshot(
            SidebarSelectionSnapshot(
                sessionIdentity: ObjectIdentifier(
                    try XCTUnwrap(state.currentSession)),
                items: [
                    SidebarSelectionItem(
                        path: "P/beta.md", isDirectory: false,
                        isMarkdown: true)
                ],
                focusedPath: "P/beta.md", creationParent: "P"))
        _ = try state.dispatchSidebarAction(
            id: SlateCommandID.sidebarPinNote)

        let pinnedOrder = await shown(state, .folder(path: "P"))
        XCTAssertEqual(
            pinnedOrder, ["P/beta.md", "P/alpha.md"],
            "pinned notes lead, then the sorted remainder")
        guard case .header(let key, let label, let count) =
            state.sidebarListPaneModel.rows.first
        else {
            return XCTFail("the pinned group opens with a header row")
        }
        XCTAssertEqual(key, "pinned")
        XCTAssertEqual(label, "Pinned")
        XCTAssertEqual(count, 1)
    }

    func testDateGroupingEmitsHeaderRows() async throws {
        let state = try openVault(
            named: "grouping",
            files: ["P/one.md": "# 1", "P/two.md": "# 2"])
        _ = state.publishSidebarSelectionSnapshot(
            SidebarSelectionSnapshot(
                sessionIdentity: ObjectIdentifier(
                    try XCTUnwrap(state.currentSession)),
                items: [], focusedPath: nil, creationParent: ""))
        _ = try state.dispatchSidebarAction(
            id: SlateCommandID.sidebarToggleDateGrouping)

        _ = await shown(state, .folder(path: "P"))
        let rows = state.sidebarListPaneModel.rows
        guard case .header(_, let label, let count) = rows.first else {
            return XCTFail("date grouping opens with a bucket header")
        }
        XCTAssertEqual(label, "Today", "fresh mtimes bucket into Today")
        XCTAssertEqual(count, 2)
        XCTAssertEqual(
            rows.count, 3, "one header + two file rows — no per-file headers")
    }

    // MARK: - Per-folder display overrides, both surfaces (rule 3)

    func testRowPreferenceOverridesApplyAndClearOnBothSurfaces() async throws {
        let state = try openVault(
            named: "override-prefs", files: ["P/a.md": "# a"])
        state.setSidebarFolderPreviewLinesOverride(folder: "P", lines: 0)
        state.setSidebarFolderDensityOverride(folder: "P", density: .compact)

        _ = await shown(state, .folder(path: "P"))
        let base = SidebarRowPreferencesSnapshot.defaults
        let effective = state.sidebarListPaneModel.rowPreferences(base: base)
        XCTAssertEqual(effective.previewLines, 0)
        XCTAssertEqual(effective.density, .compact)

        // The tree surface reads the SAME stored values through the
        // prefs accessors — one storage, two projections.
        let prefs = state.sidebarOrganization.prefs
        XCTAssertEqual(
            prefs.effectivePreviewLines(
                forFolder: "P", default: base.previewLines),
            0)
        XCTAssertEqual(
            prefs.effectiveDensity(forFolder: "P", default: base.density),
            .compact)
        XCTAssertEqual(
            prefs.effectivePreviewLines(
                forFolder: "", default: base.previewLines),
            base.previewLines,
            "other folders keep inheriting the device default")

        state.setSidebarFolderPreviewLinesOverride(folder: "P", lines: nil)
        state.setSidebarFolderDensityOverride(folder: "P", density: nil)
        let cleared = state.sidebarListPaneModel.rowPreferences(base: base)
        XCTAssertEqual(cleared, base, "clearing falls back to inheritance")
    }

    func testOverridesPersistIntoSidebarJSONAndReload() async throws {
        let state = try openVault(
            named: "override-persist", files: ["P/a.md": "# a"])
        state.setSidebarFolderPreviewLinesOverride(folder: "P", lines: 3)
        state.setSidebarFolderDescendantsOverride(
            folder: "P", includeDescendants: true)
        await state.sidebarOrganizationPersistTaskForTesting?.value

        let vault = root.appendingPathComponent("override-persist")
        let data = try Data(
            contentsOf: vault.appendingPathComponent(".slate/sidebar.json"))
        let json = try XCTUnwrap(
            JSONSerialization.jsonObject(with: data) as? [String: Any])
        let folders = try XCTUnwrap(
            json[SidebarOrganizationSchema.folderOverridesKey]
                as? [String: Any])
        let entry = try XCTUnwrap(folders["P"] as? [String: Any])
        XCTAssertEqual(
            entry[SidebarOrganizationSchema.previewLinesKey] as? Int, 3)
        XCTAssertEqual(
            entry[SidebarOrganizationSchema.descendantsKey] as? Bool, true)

        let decoded = SidebarOrganizationSchema.decode(root: json)
        XCTAssertEqual(
            decoded.prefs.effectivePreviewLines(forFolder: "P", default: 1), 3)
        XCTAssertTrue(decoded.prefs.includesDescendants(forFolder: "P"))
    }

    func testActiveFilterOrdersBatchSnapshotByResultRows() async throws {
        // Review round (high): filtered rows acquire snapshot
        // ownership — the batch orders by the FILTER page while a
        // committed filter owns the pane.
        let state = try openVault(
            named: "filter-order",
            files: ["P/aa.md": "# aa", "P/ab.md": "# ab", "P/zz.md": "# z"])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.sidebarFilterModel.fieldText = "a"
        state.sidebarFilterModel.commitNow()
        XCTAssertTrue(state.sidebarFilterModel.isActive)
        XCTAssertEqual(
            state.sidebarFilterModel.results?.files.map(\.path),
            ["P/aa.md", "P/ab.md"])

        state.sidebarDualPaneMultiSelection = ["P/ab.md", "P/aa.md"]
        state.publishDualPaneSelectionSnapshot()
        XCTAssertEqual(
            state.sidebarSelectionSnapshot?.items.map(\.path),
            ["P/aa.md", "P/ab.md"],
            "batch order follows the visible FILTER rows")
    }

    // MARK: - Multi-select batch snapshot (rule 5)

    func testMultiSelectionPublishesBatchSnapshotInVisibleOrder()
        async throws
    {
        let state = try openVault(
            named: "multi",
            files: [
                "P/alpha.md": "# a", "P/beta.md": "# b", "P/gamma.md": "# c",
            ])
        state.setSidebarLayoutForInternalTesting(.dualPane)
        state.sidebarSelectedContainer = .folder(path: "P")
        _ = await shown(state, .folder(path: "P"))

        state.sidebarDualPaneMultiSelection = ["P/gamma.md", "P/alpha.md"]
        state.publishDualPaneSelectionSnapshot()
        let snapshot = try XCTUnwrap(state.sidebarSelectionSnapshot)
        XCTAssertEqual(
            snapshot.items.map(\.path), ["P/alpha.md", "P/gamma.md"],
            "batch items ride in VISIBLE row order, not set order")
        XCTAssertTrue(snapshot.items.allSatisfy { $0.isMarkdown })
        XCTAssertEqual(
            snapshot.focusedPath, "P/gamma.md",
            "no anchor row → the last visible selected row is focused")
        XCTAssertEqual(snapshot.creationParent, "P")
    }
}
