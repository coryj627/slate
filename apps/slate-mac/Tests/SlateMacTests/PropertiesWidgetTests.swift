// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// U3-3 (#467): the in-note properties widget's per-tab expansion state,
/// its workspace.json persistence, and the mount contract (pinned above
/// BOTH mode surfaces; the sidebar hosts no properties surface).
@MainActor
final class PropertiesWidgetTests: XCTestCase {

    // MARK: - Per-tab expansion state (the viewModes pattern)

    func testExpansionDefaultsExpandedAndStoresSparsely() {
        let ws = WorkspaceState()
        let tab = ws.openTab(.markdown(path: "a.md"))
        XCTAssertTrue(ws.isPropertiesExpanded(for: tab), "expanded is the default")
        XCTAssertTrue(ws.propertiesCollapsed.isEmpty)

        ws.setPropertiesExpanded(false, for: tab)
        XCTAssertFalse(ws.isPropertiesExpanded(for: tab))
        XCTAssertEqual(ws.propertiesCollapsed, [tab])

        ws.setPropertiesExpanded(true, for: tab)
        XCTAssertTrue(ws.propertiesCollapsed.isEmpty, "expanded entries are never stored")
    }

    func testCloseResetAndAdoptCleanUpCollapseEntries() {
        let ws = WorkspaceState()
        let a = ws.openTab(.markdown(path: "a.md"))
        let b = ws.openTab(.markdown(path: "b.md"))
        ws.setPropertiesExpanded(false, for: a)
        ws.setPropertiesExpanded(false, for: b)

        _ = ws.close(a)
        XCTAssertEqual(ws.propertiesCollapsed, [b], "closed tab's entry dropped")

        ws.reset()
        XCTAssertTrue(ws.propertiesCollapsed.isEmpty)

        var restored = WorkspaceModel()
        let tab = restored.openTab(.markdown(path: "x.md"))
        ws.adopt(restored, propertiesCollapsed: [tab, TabID()])
        XCTAssertEqual(
            ws.propertiesCollapsed, [tab],
            "adopt keeps known ids only")
    }

    // MARK: - Store schema

    func testSnapshotRoundTripsCollapsedSet() {
        var model = WorkspaceModel()
        let a = model.openTab(.markdown(path: "a.md"))
        let b = model.openTab(.markdown(path: "b.md"))
        _ = b  // b stays expanded — must not be written.

        let snapshot = WorkspaceStore.snapshot(of: model, propertiesCollapsed: [a])
        XCTAssertEqual(WorkspaceStore.propertiesCollapsed(from: snapshot), [a])
    }

    func testSnapshotWithoutPropsKeyRestoresExpanded() throws {
        let tabID = UUID()
        let groupID = UUID()
        let json = """
            {
              "version": 1,
              "activeGroup": "\(groupID.uuidString)",
              "root": {
                "kind": "group",
                "id": "\(groupID.uuidString)",
                "activeTab": "\(tabID.uuidString)",
                "tabs": [
                  { "id": "\(tabID.uuidString)",
                    "item": { "kind": "markdown", "path": "a.md" } }
                ]
              }
            }
            """
        let snapshot = try JSONDecoder().decode(
            WorkspaceStore.Snapshot.self, from: Data(json.utf8))
        XCTAssertTrue(WorkspaceStore.propertiesCollapsed(from: snapshot).isEmpty)
    }

    // MARK: - Mount contract (structural)

    /// The widget is pinned ABOVE the mode branch — one mount serving both
    /// modes, outside the editor scroll view. Source-order lint in the
    /// LeafPortTests style: the mount must appear before the mode switch.
    func testWidgetMountsAboveBothModeSurfaces() throws {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // package root
            .appendingPathComponent("Sources/SlateMac/NoteContentView.swift")
        let source = try String(contentsOf: url, encoding: .utf8)
        guard let mount = source.range(of: "NotePropertiesHeader(workspace:"),
            let modeBranch = source.range(of: "workspace.activeViewMode == .reading")
        else {
            return XCTFail("expected the widget mount and the mode branch in NoteContentView")
        }
        XCTAssertLessThan(
            mount.lowerBound, modeBranch.lowerBound,
            "properties render before (above) the mode surface — both modes share the one mount")
    }
}
