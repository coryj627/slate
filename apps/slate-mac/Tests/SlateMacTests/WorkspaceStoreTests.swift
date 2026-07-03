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
        // A future "canvas" tab (Milestone T) round-trips as a DROP, with
        // the group's active pointer repaired.
        let groupID = UUID()
        let keepID = UUID()
        let json = """
            {"version": 1, "activeGroup": "\(groupID.uuidString)",
             "root": {"kind": "group", "id": "\(groupID.uuidString)",
                      "activeTab": "\(UUID().uuidString)",
                      "tabs": [
                        {"id": "\(UUID().uuidString)",
                         "item": {"kind": "canvas", "path": "board.canvas"}},
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
