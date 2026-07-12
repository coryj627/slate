// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// U1-6 (#458): workspace-layout persistence — round-trip identity, the
/// forward-compat and corruption degradations, missing-file grace, and the
/// end-to-end restore.
@MainActor
final class WorkspaceStoreTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("workspace-store-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    /// Depth-3 fixture: [A(2 tabs) | V[B, C]] with adjusted weights.
    private func depthThreeModel() -> WorkspaceModel {
        var model = WorkspaceModel()
        model.openTab(.markdown(path: "a.md"))
        model.openTab(.markdown(path: "a2.md"))
        let gB = model.split(model.activeGroupID, axis: .horizontal)!
        model.replaceActiveTabItem(.markdown(path: "b.md"))
        _ = model.split(gB, axis: .vertical)
        model.replaceActiveTabItem(.markdown(path: "c.md"))
        model.setWeight(delta: 0.1, for: model.activeGroupID)
        return model
    }

    // MARK: Round trip

    func testSnapshotModelRoundTripIsIdentity() throws {
        let model = depthThreeModel()
        let snapshot = WorkspaceStore.snapshot(of: model)
        let rebuilt = try XCTUnwrap(WorkspaceStore.model(from: snapshot))
        XCTAssertEqual(rebuilt, model, "model → JSON schema → model is exact")
    }

    func testDiskRoundTrip() throws {
        let store = WorkspaceStore(vaultRoot: tempDir)
        let model = depthThreeModel()
        try store.save(WorkspaceStore.snapshot(of: model))
        let loaded = try XCTUnwrap(store.load())
        XCTAssertEqual(WorkspaceStore.model(from: loaded), model)
    }

    /// U4-1 (#470): the active right-pane leaf persists in the same snapshot
    /// and survives the disk round-trip.
    func testDiskRoundTripCarriesActiveLeaf() throws {
        let store = WorkspaceStore(vaultRoot: tempDir)
        let model = depthThreeModel()
        try store.save(WorkspaceStore.snapshot(of: model, activeLeaf: "bibliography"))
        let loaded = try XCTUnwrap(store.load())
        XCTAssertEqual(loaded.activeLeaf, "bibliography")
        XCTAssertEqual(WorkspaceStore.model(from: loaded), model, "layout unaffected")
    }

    func testCensusPersistRestoreIdentity() {
        struct SplitMix64: RandomNumberGenerator {
            var state: UInt64
            mutating func next() -> UInt64 {
                state &+= 0x9E37_79B9_7F4A_7C15
                var z = state
                z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
                z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
                return z ^ (z >> 31)
            }
        }
        for seed in 0..<300 {
            var rng = SplitMix64(state: UInt64(seed) &* 0xD00D + 3)
            var model = WorkspaceModel()
            model.openTab(.markdown(path: "seed.md"))
            for step in 0..<40 {
                switch rng.next() % 5 {
                case 0:
                    model.openTab(.markdown(path: "n\(step).md"))
                case 1:
                    let axis: SplitBranch.Axis =
                        rng.next() % 2 == 0 ? .horizontal : .vertical
                    model.split(model.activeGroupID, axis: axis)
                case 2:
                    if let tab = model.activeGroup.activeTabID, model.allTabs.count > 1 {
                        model.closeTab(tab)
                    }
                case 3:
                    model.setWeight(delta: 0.05, for: model.activeGroupID)
                default:
                    model.selectNextTab()
                }
            }
            guard !model.isEmpty else { continue }
            let rebuilt = WorkspaceStore.model(from: WorkspaceStore.snapshot(of: model))
            if rebuilt != model {
                XCTFail("seed \(seed): persist/restore diverged")
                return
            }
        }
    }

    // MARK: Expanded-folder persistence (#873)

    /// The expanded-folder PATH set (#873, Codex round 2: paths, not
    /// reusable rowids) rides the snapshot and survives the disk
    /// round-trip, layout untouched.
    func testExpandedDirPathsRoundTripThroughDisk() throws {
        let store = WorkspaceStore(vaultRoot: tempDir)
        let model = depthThreeModel()
        try store.save(
            WorkspaceStore.snapshot(
                of: model, expandedDirPaths: ["notes", "notes/deep", "archive"]))
        let loaded = try XCTUnwrap(store.load())
        XCTAssertEqual(
            WorkspaceStore.expandedDirPaths(from: loaded),
            ["notes", "notes/deep", "archive"],
            "recency order round-trips verbatim — order IS the eviction policy")
        XCTAssertEqual(WorkspaceStore.model(from: loaded), model, "layout unaffected")
    }

    /// Version tolerance: a snapshot without the key decodes unchanged and
    /// reads back empty — never a decode failure, never a phantom expansion.
    /// (This also covers pre-paths snapshots that carried the retired
    /// `expandedDirs` id key: unknown keys drop, expansion starts fresh.)
    func testSnapshotWithoutExpandedDirPathsKeyDecodesAsEmpty() throws {
        let groupID = UUID()
        let json = """
            {"version": 1, "activeGroup": "\(groupID.uuidString)",
             "root": {"kind": "group", "id": "\(groupID.uuidString)",
                      "tabs": []},
             "expandedDirs": [3, 7]}
            """
        let snapshot = try JSONDecoder().decode(
            WorkspaceStore.Snapshot.self, from: Data(json.utf8))
        XCTAssertNil(snapshot.expandedDirPaths)
        XCTAssertEqual(WorkspaceStore.expandedDirPaths(from: snapshot), [])
    }

    /// Sparse-write discipline: nothing expanded ⇒ no key in the file.
    func testEmptyExpandedDirPathsIsNotWritten() throws {
        let snapshot = WorkspaceStore.snapshot(
            of: depthThreeModel(), expandedDirPaths: [])
        XCTAssertNil(snapshot.expandedDirPaths)
        let data = try JSONEncoder().encode(snapshot)
        let raw = try XCTUnwrap(String(data: data, encoding: .utf8))
        XCTAssertFalse(raw.contains("expandedDirPaths"))
    }

    /// Defensive bounds: cap on write; hostile payloads (absolute paths,
    /// traversal, empties, oversized entries) are filtered on read so a
    /// tampered workspace.json can name nothing outside the vault.
    func testExpandedDirPathsCappedOnWriteAndFilteredOnRead() throws {
        let oversized = (1...600).map { "dir-\($0)" }
        let snapshot = WorkspaceStore.snapshot(
            of: depthThreeModel(), expandedDirPaths: oversized)
        XCTAssertEqual(
            snapshot.expandedDirPaths?.count, WorkspaceStore.maxExpandedDirs)

        var hostile = snapshot
        hostile.expandedDirPaths = [
            "", "/etc", "a/../../escape", "ok/child",
            String(repeating: "x", count: 2000),
        ]
        let read = WorkspaceStore.expandedDirPaths(from: hostile)
        XCTAssertEqual(read, ["ok/child"], "only the well-formed relative path survives")
    }

    /// Codex round 3: the cap keeps the NEWEST expansions (recency order,
    /// suffix cap) — entry 501 survives, the oldest evicts; duplicates
    /// collapse to their newest position and can't eat cap slots.
    func testExpandedDirPathsCapKeepsNewestAndDedupes() throws {
        let old = (1...WorkspaceStore.maxExpandedDirs).map { "old-\($0)" }
        let snapshot = WorkspaceStore.snapshot(
            of: depthThreeModel(), expandedDirPaths: old + ["z-newest"])
        let written = try XCTUnwrap(snapshot.expandedDirPaths)
        XCTAssertEqual(written.count, WorkspaceStore.maxExpandedDirs)
        XCTAssertEqual(written.last, "z-newest", "the newest expansion survives entry 501")
        XCTAssertFalse(written.contains("old-1"), "the oldest evicts")

        XCTAssertEqual(
            WorkspaceStore.orderedDedup(["a", "b", "a", "c", "b"]),
            ["a", "c", "b"],
            "last occurrence wins; order otherwise preserved")
    }

    // MARK: Degradations

    func testUnknownVersionYieldsNil() throws {
        let store = WorkspaceStore(vaultRoot: tempDir)
        try FileManager.default.createDirectory(
            at: store.fileURL.deletingLastPathComponent(),
            withIntermediateDirectories: true)
        try Data(#"{"version": 99, "activeGroup": "\#(UUID().uuidString)"}"#.utf8)
            .write(to: store.fileURL)
        XCTAssertNil(store.load())
    }

    func testCorruptFileYieldsNil() throws {
        let store = WorkspaceStore(vaultRoot: tempDir)
        try FileManager.default.createDirectory(
            at: store.fileURL.deletingLastPathComponent(),
            withIntermediateDirectories: true)
        try Data("not json {{{".utf8).write(to: store.fileURL)
        XCTAssertNil(store.load())
    }

    func testOversizeFileYieldsNil() throws {
        let store = WorkspaceStore(vaultRoot: tempDir)
        try FileManager.default.createDirectory(
            at: store.fileURL.deletingLastPathComponent(),
            withIntermediateDirectories: true)
        try Data(repeating: 0x20, count: WorkspaceStore.maxFileBytes + 1)
            .write(to: store.fileURL)
        XCTAssertNil(store.load())
    }

    func testUnknownTabKindDroppedNotFatal() throws {
        // Forward compat (#369 inverted the old canvas-drop assertion):
        // a still-future "graph" tab (Milestone P) round-trips as a DROP,
        // with the group's active pointer repaired — exactly how a T-era
        // snapshot degrades in an older build.
        let groupID = UUID()
        let keepID = UUID()
        let json = """
            {"version": 1, "activeGroup": "\(groupID.uuidString)",
             "root": {"kind": "group", "id": "\(groupID.uuidString)",
                      "activeTab": "\(UUID().uuidString)",
                      "tabs": [
                        {"id": "\(UUID().uuidString)",
                         "item": {"kind": "graph", "path": "vault.graph"}},
                        {"id": "\(keepID.uuidString)",
                         "item": {"kind": "markdown", "path": "kept.md"}}
                      ]}}
            """
        let snapshot = try JSONDecoder().decode(
            WorkspaceStore.Snapshot.self, from: Data(json.utf8))
        let model = try XCTUnwrap(WorkspaceStore.model(from: snapshot))
        XCTAssertEqual(model.allTabs.map(\.item), [.markdown(path: "kept.md")])
        XCTAssertEqual(
            model.activeGroup.activeTabID, TabID(raw: keepID),
            "dangling active pointer repaired to a surviving tab")
        XCTAssertTrue(model.validate().isEmpty)
    }

    /// #369: canvas tabs round-trip through the store — with the
    /// additive per-tab `activeCanvasSurface` field (sparse: outline is
    /// the absent default) — instead of being dropped.
    func testCanvasTabRoundTripsWithActiveSurface() throws {
        var model = WorkspaceModel()
        let canvasTab = model.openTab(.canvas(path: "boards/plan.canvas"))
        _ = model.openTab(.markdown(path: "notes/a.md"))

        let snapshot = WorkspaceStore.snapshot(
            of: model, canvasSurfaces: [canvasTab: .table])
        let data = try JSONEncoder().encode(snapshot)
        let decoded = try JSONDecoder().decode(WorkspaceStore.Snapshot.self, from: data)

        let rebuilt = try XCTUnwrap(WorkspaceStore.model(from: decoded))
        XCTAssertEqual(
            rebuilt.allTabs.map(\.item),
            [.canvas(path: "boards/plan.canvas"), .markdown(path: "notes/a.md")])
        XCTAssertEqual(
            WorkspaceStore.canvasSurfaces(from: decoded), [canvasTab: .table])
        XCTAssertTrue(rebuilt.validate().isEmpty)

        // Sparse rule: an outline-surface tab writes no field at all.
        let outlineSnap = WorkspaceStore.snapshot(of: model)
        let encoded = String(
            decoding: try JSONEncoder().encode(outlineSnap), as: UTF8.self)
        XCTAssertFalse(encoded.contains("activeCanvasSurface"))
    }

    /// N3-1: `.base` tabs are first-class workspace tabs. They persist like
    /// canvases and notes, and older snapshots with unknown future tab kinds
    /// still drop those unknown tabs without dropping bases.
    func testBaseTabRoundTripsThroughWorkspaceStore() throws {
        var model = WorkspaceModel()
        _ = model.openTab(.base(path: "Queries/Reading.base"))
        _ = model.openTab(.markdown(path: "notes/a.md"))

        let snapshot = WorkspaceStore.snapshot(of: model)
        let data = try JSONEncoder().encode(snapshot)
        let decoded = try JSONDecoder().decode(WorkspaceStore.Snapshot.self, from: data)

        let rebuilt = try XCTUnwrap(WorkspaceStore.model(from: decoded))
        XCTAssertEqual(
            rebuilt.allTabs.map(\.item),
            [.base(path: "Queries/Reading.base"), .markdown(path: "notes/a.md")])
        XCTAssertTrue(rebuilt.validate().isEmpty)
    }

    /// N4-3 (#709): saved queries are ephemeral base tabs. They persist by
    /// stable saved-query id and display name, not by a vault-relative path.
    func testSavedQueryTabRoundTripsThroughWorkspaceStore() throws {
        var model = WorkspaceModel()
        _ = model.openTab(.savedQuery(id: "sq-active", name: "Active projects"))
        _ = model.openTab(.markdown(path: "notes/a.md"))

        let snapshot = WorkspaceStore.snapshot(of: model)
        let data = try JSONEncoder().encode(snapshot)
        let decoded = try JSONDecoder().decode(WorkspaceStore.Snapshot.self, from: data)

        let rebuilt = try XCTUnwrap(WorkspaceStore.model(from: decoded))
        XCTAssertEqual(
            rebuilt.allTabs.map(\.item),
            [
                .savedQuery(id: "sq-active", name: "Active projects"),
                .markdown(path: "notes/a.md"),
            ])
        XCTAssertTrue(rebuilt.validate().isEmpty)
    }

    /// N4-4 (#710): dashboards are ephemeral base tabs. They persist by
    /// stable dashboard id and display name, not by a vault-relative path.
    func testDashboardTabRoundTripsThroughWorkspaceStore() throws {
        var model = WorkspaceModel()
        _ = model.openTab(.dashboard(id: "dash-overview", name: "Overview"))
        _ = model.openTab(.markdown(path: "notes/a.md"))

        let snapshot = WorkspaceStore.snapshot(of: model)
        let data = try JSONEncoder().encode(snapshot)
        let decoded = try JSONDecoder().decode(WorkspaceStore.Snapshot.self, from: data)

        let rebuilt = try XCTUnwrap(WorkspaceStore.model(from: decoded))
        XCTAssertEqual(
            rebuilt.allTabs.map(\.item),
            [
                .dashboard(id: "dash-overview", name: "Overview"),
                .markdown(path: "notes/a.md"),
            ])
        XCTAssertTrue(rebuilt.validate().isEmpty)
    }

    // MARK: End-to-end restore

    func testVaultReopenRestoresLayoutAndMissingFileShowsErrorTab() async throws {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for name in ["alpha.md", "beta.md"] {
            try "# \(name)\n".write(
                to: vault.appendingPathComponent(name), atomically: true, encoding: .utf8)
        }
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        state.openFile("beta.md", target: .newSplit(.horizontal))
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.model.groupsInOrder.count, 2)
        let savedLayout = state.workspace.model
        state.closeVault()

        // beta.md vanishes between sessions.
        try FileManager.default.removeItem(at: vault.appendingPathComponent("beta.md"))

        state.openVault(at: vault)
        await state.scanTask?.value
        await state.noteLoadTask?.value
        XCTAssertEqual(
            state.workspace.model.groupsInOrder.count, 2,
            "layout restored: two panes")
        XCTAssertEqual(
            state.workspace.model.allTabs.map(\.item).sorted(by: {
                if case .markdown(let a) = $0, case .markdown(let b) = $1 { return a < b }
                return false
            }),
            savedLayout.allTabs.map(\.item).sorted(by: {
                if case .markdown(let a) = $0, case .markdown(let b) = $1 { return a < b }
                return false
            }),
            "tabs restored incl. the now-missing file")
        // The missing file's tab was the restored active tab → its load
        // failed gracefully into the existing error state, not a crash.
        if state.loadedFilePath == nil {
            XCTAssertNotNil(state.noteLoadError, "missing file → per-tab error state")
        }
        XCTAssertTrue(state.workspace.model.validate().isEmpty)
    }

    func testFreshVaultHasNoLayoutFile() async throws {
        let vault = tempDir.appendingPathComponent("fresh")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents2.json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        XCTAssertTrue(state.workspace.model.isEmpty, "no snapshot → fresh workspace")
    }
}
