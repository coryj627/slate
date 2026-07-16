// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

@MainActor
final class PreparedFailureRecoveryTests: XCTestCase {
    func testDashboardDegradedViewKeepsGridVisibleWithAccessibleRetry() throws {
        let source = try sourceFile("Sources/SlateMac/Bases/DashboardViews.swift")

        XCTAssertTrue(source.contains("case .degraded(let message):"), source)
        XCTAssertTrue(source.contains("degradedContent(message)"), source)
        XCTAssertTrue(
            source.contains("sectionResult"),
            "the degraded branch must render the retained result instead of replacing it")
        XCTAssertTrue(source.contains("Button(\"Retry\")"), source)
        XCTAssertTrue(
            source.contains("refreshVisibleBasesAfterInAppWrite("),
            "Retry must re-enter the prepared, bounded refresh scheduler")
        XCTAssertTrue(
            source.contains("changedPath: \"\""),
            "dashboard Retry must use an unfiltered refresh so global generation cannot strand peers")
        XCTAssertTrue(
            source.contains("Runs the dashboard refresh again."),
            "Retry needs an explicit accessibility hint")
    }

    func testDashboardPreparedRefreshFailureKeepsSnapshotUntilRetrySucceeds() throws {
        let section = makeDashboardSection()
        let oldResult = dashboardResult(path: "Notes/Old.md", summary: "1 old note.")
        let initial = try XCTUnwrap(section.beginContentRefresh(thisPath: nil))
        guard case .applied = section.applyContentRefresh(
            preparedDashboard(handle: 61, result: oldResult),
            reservation: initial)
        else {
            return XCTFail("the initial prepared result should be applied")
        }

        let failedRefresh = try XCTUnwrap(section.beginContentRefresh(thisPath: nil))
        guard case .failed = section.applyContentRefresh(
            .failed("Section could not be refreshed. Try again."),
            reservation: failedRefresh)
        else {
            return XCTFail("the owned refresh failure should be published")
        }

        XCTAssertEqual(section.state, .degraded("Section could not be refreshed. Try again."))
        XCTAssertEqual(section.result, oldResult, "a transient refresh failure must retain the grid")

        let newResult = dashboardResult(path: "Notes/New.md", summary: "1 new note.")
        let retry = try XCTUnwrap(section.beginContentRefresh(thisPath: nil))
        guard case .applied = section.applyContentRefresh(
            preparedDashboard(handle: 62, result: newResult),
            reservation: retry)
        else {
            return XCTFail("a successful retry should replace the retained snapshot")
        }

        XCTAssertEqual(section.state, .ready)
        XCTAssertEqual(section.result, newResult)
    }

    func testDashboardPreparedRefreshRejectsStaleFailureWithoutHidingSnapshot() throws {
        let section = makeDashboardSection()
        let snapshot = dashboardResult(path: "Notes/Stable.md", summary: "1 stable note.")
        let initial = try XCTUnwrap(section.beginContentRefresh(thisPath: nil))
        _ = section.applyContentRefresh(
            preparedDashboard(handle: 71, result: snapshot),
            reservation: initial)

        let stale = try XCTUnwrap(section.beginContentRefresh(thisPath: nil))
        let current = try XCTUnwrap(section.beginContentRefresh(thisPath: nil))
        guard case .stale = section.applyContentRefresh(
            .failed("A superseded failure must stay inert."),
            reservation: stale)
        else {
            return XCTFail("the superseded failure should be rejected")
        }

        XCTAssertEqual(section.state, .ready)
        XCTAssertEqual(section.result, snapshot)

        let updated = dashboardResult(path: "Notes/Current.md", summary: "1 current note.")
        guard case .applied = section.applyContentRefresh(
            preparedDashboard(handle: 72, result: updated),
            reservation: current)
        else {
            return XCTFail("the current owned completion should still apply")
        }
        XCTAssertEqual(section.state, .ready)
        XCTAssertEqual(section.result, updated)
    }

    func testDashboardFirstPreparedLoadFailureRemainsFullFailureWithoutSnapshot() throws {
        let section = makeDashboardSection()
        let initial = try XCTUnwrap(section.beginContentRefresh(thisPath: nil))

        guard case .failed = section.applyContentRefresh(
            .failed("Section could not be opened."),
            reservation: initial)
        else {
            return XCTFail("the owned first-load failure should be published")
        }

        XCTAssertEqual(section.state, .failed("Section could not be opened."))
        XCTAssertNil(section.result)
    }

    func testCanvasRetargetFailureViewKeepsSnapshotVisibleWithAccessibleRetry() throws {
        let source = try sourceFile("Sources/SlateMac/Canvas/CanvasContainerView.swift")

        XCTAssertTrue(source.contains("retargetFailureSnapshot(message)"), source)
        XCTAssertTrue(source.contains("canvasBody(readOnly: true)"), source)
        XCTAssertTrue(source.contains("Button(\"Retry\")"), source)
        XCTAssertTrue(
            source.contains("scheduleCanvasRetargetPreparationIfNeeded("),
            "Retry must re-enter the existing guarded background preparation scheduler")
        XCTAssertTrue(
            source.contains(".disabled(readOnly)"),
            "the retained snapshot must not expose writable controls without a native handle")
        XCTAssertTrue(source.contains("The previous snapshot is read-only."), source)
        XCTAssertTrue(
            source.contains("announceCanvasRetargetFailure(message)"),
            "the visible state change needs one announcement through the Canvas funnel")
    }

    func testCanvasRetargetFailureKeepsSnapshotReadOnlyAndRetryPreservesInteractionState() {
        let document = makeLoadedCanvas(path: "Boards/Old.canvas", handle: 41)
        document.selection.selected = "card-1"
        document.selection.marked = ["card-1"]
        document.lastActivatedNode = "card-1"
        document.filterText = "alpha"
        document.viewport.scale = 1.75
        document.viewport.offset = CGPoint(x: 28, y: 36)
        document.viewport.followSelection = false
        document.undoStack = [
            (name: "Move Card", inverse: CanvasAction(name: "Move Card", ops: []))
        ]

        let oldOutline = document.outline
        let oldTableRows = document.tableRows
        let oldScene = document.scene
        let reservation = document.beginBatchRetarget(to: "Boards/New.canvas")
        XCTAssertEqual(document.claimRetargetPreparation(), reservation.generation)
        XCTAssertNil(
            document.claimRetargetPreparation(),
            "one retarget generation may schedule only one native open at a time")

        XCTAssertTrue(
            document.applyRetargetPreparation(
                .failed("New could not be reopened. Check that the file is available."),
                generation: reservation.generation,
                path: "Boards/New.canvas"))

        XCTAssertEqual(
            document.state,
            .retargetFailed("New could not be reopened. Check that the file is available."))
        XCTAssertNil(document.handle, "the moved-away native handle must never be restored")
        XCTAssertTrue(document.hasPendingRetargetPreparation)
        XCTAssertEqual(document.outline, oldOutline)
        XCTAssertEqual(document.tableRows, oldTableRows)
        XCTAssertEqual(document.scene, oldScene)
        XCTAssertEqual(document.selection.selected, "card-1")
        XCTAssertEqual(document.selection.marked, ["card-1"])
        XCTAssertEqual(document.lastActivatedNode, "card-1")
        XCTAssertEqual(document.filterText, "alpha")
        XCTAssertEqual(document.viewport.scale, 1.75)
        XCTAssertEqual(document.viewport.offset, CGPoint(x: 28, y: 36))
        XCTAssertFalse(document.viewport.followSelection)
        XCTAssertEqual(document.undoStack.map(\.name), ["Move Card"])

        XCTAssertEqual(document.claimRetargetPreparation(), reservation.generation)
        XCTAssertFalse(
            document.applyRetargetPreparation(
                preparedCanvas(handle: 42),
                generation: reservation.generation,
                path: "Boards/Stale.canvas"),
            "a retry for another exact path must be inert")
        XCTAssertNil(document.handle)
        XCTAssertEqual(
            document.state,
            .retargetFailed("New could not be reopened. Check that the file is available."))

        XCTAssertTrue(
            document.applyRetargetPreparation(
                preparedCanvas(handle: 43),
                generation: reservation.generation,
                path: "Boards/New.canvas"))
        XCTAssertEqual(document.state, .ready)
        XCTAssertEqual(document.handle, 43)
        XCTAssertFalse(document.hasPendingRetargetPreparation)
        XCTAssertEqual(document.selection.selected, "card-1")
        XCTAssertEqual(document.selection.marked, ["card-1"])
        XCTAssertEqual(document.lastActivatedNode, "card-1")
        XCTAssertEqual(document.filterText, "alpha")
        XCTAssertEqual(document.viewport.scale, 1.75)
        XCTAssertEqual(document.viewport.offset, CGPoint(x: 28, y: 36))
        XCTAssertFalse(document.viewport.followSelection)
        XCTAssertEqual(document.undoStack.map(\.name), ["Move Card"])
    }

    func testCanvasRetargetDegradedPreparationPublishesRetryableReadOnlyDetail() {
        let document = makeLoadedCanvas(path: "Boards/Old.canvas", handle: 51)
        let oldOutline = document.outline
        let reservation = document.beginBatchRetarget(to: "Boards/New.canvas")
        XCTAssertEqual(document.claimRetargetPreparation(), reservation.generation)

        XCTAssertTrue(
            document.applyRetargetPreparation(
                .degraded(
                    warnings: [],
                    message: "the moved file is not valid JSON Canvas"),
                generation: reservation.generation,
                path: "Boards/New.canvas"))

        XCTAssertEqual(
            document.state,
            .retargetFailed("New could not be read as a canvas. the moved file is not valid JSON Canvas"))
        XCTAssertNil(document.handle)
        XCTAssertTrue(document.hasPendingRetargetPreparation)
        XCTAssertEqual(document.outline, oldOutline)
        XCTAssertEqual(document.claimRetargetPreparation(), reservation.generation)
    }

    func testCanvasPreparedActivationGuardConsumesExactlyOnceForEveryOutcome() {
        let outcomes: [CanvasPreparedLoad] = [
            preparedCanvas(handle: 81),
            .degraded(warnings: [], message: "Prepared degraded state."),
            .failed("Prepared failure state."),
        ]

        for (index, prepared) in outcomes.enumerated() {
            let document = CanvasDocument(path: "Boards/Prepared-\(index).canvas")
            document.applyPreparedLoad(prepared)

            XCTAssertTrue(
                document.shouldSkipSynchronousActivationLoad(),
                "the first activation must trust the already prepared outcome")
            XCTAssertFalse(
                document.shouldSkipSynchronousActivationLoad(),
                "the prepared activation guard must be consumed exactly once")
        }
    }

    private func makeLoadedCanvas(path: String, handle: UInt64) -> CanvasDocument {
        let document = CanvasDocument(path: path)
        document.applyPreparedLoad(preparedCanvas(handle: handle))
        return document
    }

    private func preparedCanvas(handle: UInt64) -> CanvasPreparedLoad {
        .ready(
            handle: handle,
            warnings: [],
            outline: [
                CanvasOutlineRow(
                    nodeId: "card-1",
                    depth: 0,
                    kind: "text",
                    title: "Alpha",
                    groupPath: [],
                    ordinalN: 1,
                    totalM: 1,
                    connectionCount: 0,
                    colorName: nil)
            ],
            tableRows: [
                CanvasTableRow(
                    nodeId: "card-1",
                    kind: "text",
                    title: "Alpha",
                    groupPath: [],
                    target: "",
                    connectionCount: 0,
                    colorName: nil)
            ],
            scene: CanvasScene(nodes: [], edges: []))
    }

    private func makeDashboardSection() -> DashboardSectionDocument {
        DashboardSectionDocument(
            index: 0,
            status: DashboardSectionStatus(
                savedQueryId: "query-1",
                savedQueryName: "Reading",
                headingOverride: nil,
                viewOverride: nil,
                missing: false))
    }

    private func preparedDashboard(handle: UInt64, result: BasesResultSet) -> BasePreparedLoad {
        .ready(
            handle: handle,
            views: [
                BaseViewSummary(
                    name: "Table",
                    viewType: "table",
                    source: "files",
                    status: .executable,
                    slateStateJson: nil)
            ],
            result: result,
            activeViewIndex: 0,
            appliedQuickFilter: nil)
    }

    private func dashboardResult(path: String, summary: String) -> BasesResultSet {
        BasesResultSet(
            columns: [],
            rows: [
                BasesRow(
                    filePath: path,
                    taskOrdinal: nil,
                    values: [],
                    audioDescription: summary)
            ],
            groups: [],
            summaries: [],
            totalCount: 1,
            shownCount: 1,
            unfilteredShownCount: 1,
            executedAtMs: 0,
            warnings: [],
            viewError: nil,
            audioSummary: summary)
    }

    private func sourceFile(_ relativePath: String) throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        while cursor.path != "/" {
            let candidate = cursor.appendingPathComponent(relativePath)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor.deleteLastPathComponent()
        }
        throw CocoaError(.fileNoSuchFile)
    }
}
