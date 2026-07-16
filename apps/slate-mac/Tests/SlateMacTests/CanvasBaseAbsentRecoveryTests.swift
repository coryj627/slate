// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Recovery contracts for file-backed Canvas and Base tabs after an
/// outcome-unknown Trash is resolved. A present file can be temporarily
/// detached while its native handle is replaced; an absent file must land in
/// a truthful unavailable state without losing sheet-owned user input.
@MainActor
final class CanvasBaseAbsentRecoveryTests: XCTestCase {
    private final class NativePreparationGate: @unchecked Sendable {
        private let condition = NSCondition()
        private var entered = false
        private var released = false

        func run<T>(_ work: () -> T) -> T {
            condition.lock()
            entered = true
            condition.broadcast()
            while !released { condition.wait() }
            condition.unlock()
            return work()
        }

        var hasEntered: Bool {
            condition.lock()
            defer { condition.unlock() }
            return entered
        }

        func release() {
            condition.lock()
            released = true
            condition.broadcast()
            condition.unlock()
        }
    }

    private struct Fixture {
        let state: AppState
        let vault: URL
        let session: VaultSession
    }

    private var tempDirs: [URL] = []

    private static var projectRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }

    override func tearDown() {
        for directory in tempDirs {
            try? FileManager.default.removeItem(at: directory)
        }
        tempDirs = []
        super.tearDown()
    }

    private func makeFixture() async throws -> Fixture {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("canvas-base-absent-\(UUID().uuidString)")
        tempDirs.append(root)
        let vault = root.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("folder"),
            withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"),
            withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Recovered"),
            withIntermediateDirectories: true)

        try Data(
            #"{"nodes":[{"id":"a","type":"text","text":"A","x":0,"y":0,"width":100,"height":50}],"edges":[]}"#.utf8
        ).write(to: vault.appendingPathComponent("folder/board.canvas"))
        try Data(
            #"""
            views:
              - type: table
                name: Reading
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
                  - status
            """#.utf8
        ).write(to: vault.appendingPathComponent("folder/Reading.base"))
        try Data("---\nstatus: active\n---\n# Alpha\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Alpha.md"))

        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return Fixture(
            state: state,
            vault: vault,
            session: try XCTUnwrap(state.currentSession))
    }

    private func openCanvas(_ fixture: Fixture) throws -> CanvasDocument {
        fixture.state.openFile("folder/board.canvas", target: .currentTab)
        let document = try XCTUnwrap(
            fixture.state.canvasDocuments["folder/board.canvas"])
        XCTAssertNotNil(document.handle)
        return document
    }

    private func openBase(_ fixture: Fixture) throws -> BaseDocument {
        fixture.state.openFile("folder/Reading.base", target: .currentTab)
        let document = try XCTUnwrap(fixture.state.activeBaseDocument)
        XCTAssertNotNil(document.handle)
        return document
    }

    private func eventually(
        timeout: TimeInterval = 2,
        _ condition: () -> Bool
    ) async -> Bool {
        let deadline = Date(timeIntervalSinceNow: timeout)
        while Date() < deadline {
            if condition() { return true }
            await Task.yield()
        }
        return condition()
    }

    private func findTable(in view: NSView) -> NSTableView? {
        if let table = view as? NSTableView { return table }
        for child in view.subviews {
            if let table = findTable(in: child) { return table }
        }
        return nil
    }

    func testCanvasMutationsStayDisabledUntilDetachedRetargetAttachesAHandle()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let document = try openCanvas(fixture)
        let reservation = document.beginBatchRetarget(to: document.path)
        if let replaced = reservation.replacedHandle {
            fixture.session.closeCanvas(handle: replaced)
        }

        XCTAssertEqual(
            state.activeCanvasMutationDisabledReason,
            "This canvas is reopening. Wait for it to finish before making changes.")
        var actionCount = 0
        XCTAssertFalse(
            state.commitCanvasPromptMutation { actionCount += 1 },
            "a detached ready snapshot must not admit a native mutation")
        XCTAssertEqual(actionCount, 0)

        let generation = try XCTUnwrap(document.claimRetargetPreparation())
        XCTAssertTrue(
            document.applyRetargetPreparation(
                .failed("Injected reopen failure."),
                generation: generation,
                path: document.path))
        XCTAssertEqual(
            state.activeCanvasMutationDisabledReason,
            "This canvas could not be reopened. Choose Retry before making changes.")

        document.load(session: fixture.session)
        XCTAssertNotNil(document.handle)
        XCTAssertNil(state.activeCanvasMutationDisabledReason)
    }

    func testAbsentCanvasKeepsDocumentPromptAndPickerInTruthfulUnavailableState()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let document = try openCanvas(fixture)
        state.presentCanvasPrompt(.newGroup, draft: "Roadmap — e\u{301}")
        let picker = CanvasCardPickerRequest(purpose: .connectTo)
        state.canvasCardPicker = picker

        try FileManager.default.removeItem(
            at: fixture.vault.appendingPathComponent("folder/board.canvas"))
        state.invalidateCanvasDocument(path: "folder/board.canvas")

        XCTAssertTrue(
            state.canvasDocuments["folder/board.canvas"] === document,
            "the mounted tab must retain the invalidated document identity")
        XCTAssertNil(document.handle)
        guard case .failed(let message) = document.state else {
            return XCTFail("an absent Canvas must land in a failed state")
        }
        XCTAssertTrue(message.contains("moved to Trash"))
        XCTAssertEqual(
            state.activeCanvasMutationDisabledReason,
            "This canvas is no longer available. Copy any draft before closing.")

        var promptActionCount = 0
        XCTAssertFalse(
            state.commitCanvasPromptMutation { promptActionCount += 1 })
        var pickerActionCount = 0
        XCTAssertFalse(
            state.commitCanvasCardPickerSelection(in: document) {
                pickerActionCount += 1
            })

        XCTAssertEqual(promptActionCount, 0)
        XCTAssertEqual(pickerActionCount, 0)
        XCTAssertEqual(state.canvasPrompt, .newGroup)
        XCTAssertEqual(state.canvasPromptDraft, "Roadmap — e\u{301}")
        XCTAssertEqual(state.canvasCardPicker, picker)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "This canvas is no longer available. Copy any draft before closing.")
    }

    func testBaseSaveToViewStaysDisabledThroughRetargetFailureUntilHandleReturns()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let document = try openBase(fixture)
        state.basesEditViewFilters()
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        XCTAssertNil(
            state.baseDefinitionEditingDisabledReason(
                for: BaseDocument(
                    source: .savedQuery(id: "saved-query", name: "Saved Query"))),
            "saved-query Edit Filters remains handle-independent")
        let reservation = document.beginBatchRetarget(to: document.source)
        if let replaced = reservation.replacedHandle {
            fixture.session.closeBase(handle: replaced)
        }

        XCTAssertEqual(
            state.baseQueryBuilderSaveToViewDisabledReason,
            "This Base is reopening. Wait for it to finish before making changes.")
        XCTAssertEqual(state.baseQueryBuilderRecoveryActionLabel, "Retry")
        let quickFilterFocusToken = state.baseQuickFilterFocusToken
        state.basesFocusQuickFilter()
        XCTAssertEqual(state.baseQuickFilterFocusToken, quickFilterFocusToken)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "This Base is reopening. Wait for it to finish before making changes.")
        state.basesSelectNextView()
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "This Base is reopening. Wait for it to finish before making changes.")
        state.basesSelectPreviousView()
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "This Base is reopening. Wait for it to finish before making changes.")
        state.basesSortByColumn()
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "This Base is reopening. Wait for it to finish before making changes.")
        state.basesSaveSortToView()
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "This Base is reopening. Wait for it to finish before making changes.")
        state.basesBuilderSaveToView()
        XCTAssertNil(
            document.handle,
            "Save to view must not synchronously bypass a pending retarget")
        XCTAssertTrue(state.activeBaseQueryBuilder === model)

        let generation = try XCTUnwrap(document.claimRetargetPreparation())
        XCTAssertNil(
            state.baseQueryBuilderRecoveryActionLabel,
            "Retry must not remain actionable while preparation is already running")
        document.quickFilterText = "typed filter survives refresh"
        state.basesRefresh()
        XCTAssertNil(
            document.handle,
            "Refresh must not synchronously replace an in-flight reopen")
        XCTAssertTrue(document.hasPendingRetargetPreparation)
        XCTAssertTrue(document.isRetargetPreparationInFlight)
        XCTAssertEqual(document.quickFilterText, "typed filter survives refresh")
        XCTAssertTrue(state.activeBaseQueryBuilder === model)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "This Base is reopening. Wait for it to finish before making changes.")
        XCTAssertTrue(
            document.applyRetargetPreparation(
                .failed("Injected Base reopen failure."),
                generation: generation,
                source: document.source))
        XCTAssertEqual(
            state.baseQueryBuilderSaveToViewDisabledReason,
            "This Base could not be reopened. Choose Retry before making changes.")
        XCTAssertEqual(state.baseQueryBuilderRecoveryActionLabel, "Retry")
        XCTAssertEqual(state.baseRecoveryActionLabel(for: document), "Retry")

        let structuralToken = state.beginStructuralMutation()
        XCTAssertNil(state.retryBaseRecovery(for: document))
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.structuralMutationBusyReason)
        state.endStructuralMutation(structuralToken)

        let retry = try XCTUnwrap(state.retryBaseRecovery(for: document))
        await retry.value
        XCTAssertNotNil(document.handle)
        XCTAssertNil(state.baseQueryBuilderSaveToViewDisabledReason)
    }

    func testAbsentBaseKeepsBuilderDraftAndSaveAsRecoveryRoute()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let document = try openBase(fixture)
        state.basesEditViewFilters()
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        model.perform(.addCondition)
        let draftBefore = model.draft

        try FileManager.default.removeItem(
            at: fixture.vault.appendingPathComponent("folder/Reading.base"))
        state.invalidateBaseDocument(path: "folder/Reading.base")

        XCTAssertTrue(
            state.baseDocuments[document.source.key] === document,
            "the mounted Base tab must keep its invalidated document identity")
        XCTAssertNil(document.handle)
        guard case .failed(let message) = document.state else {
            return XCTFail("an absent Base must land in a failed state")
        }
        XCTAssertTrue(message.contains("moved to Trash"))
        XCTAssertEqual(
            state.baseQueryBuilderSaveToViewDisabledReason,
            "The source Base is no longer available. Keep this draft or save it as a new .base file.")

        state.basesBuilderSaveToView()
        XCTAssertNil(document.handle)
        XCTAssertTrue(state.activeBaseQueryBuilder === model)
        XCTAssertEqual(model.draft, draftBefore)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "The source Base is no longer available. Keep this draft or save it as a new .base file.")

        let recovery = try XCTUnwrap(
            state.basesBuilderSaveAsBase(path: "Recovered/Reading.base"))
        await recovery.value
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault
                    .appendingPathComponent("Recovered/Reading.base").path))
        XCTAssertEqual(model.draft, draftBefore)
    }

    func testMountedBasePendingRetargetStaysAsyncAndPreservesInteractionState()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let document = try openBase(fixture)
        let tabID = try XCTUnwrap(state.workspace.activeTab?.id)
        document.quickFilterText = "typed filter survives retarget"
        let reservation = document.beginBatchRetarget(to: document.source)
        if let replaced = reservation.replacedHandle {
            fixture.session.closeBase(handle: replaced)
        }

        let gate = NativePreparationGate()
        let realPreloader = state.baseRetargetPreloadRunner
        state.baseRetargetPreloadRunner = { session, request, observer in
            gate.run {
                realPreloader(session, request, observer)
            }
        }

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 900, height: 600),
            styleMask: [.titled],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
        defer {
            gate.release()
            window.orderOut(nil)
        }
        let host = NSHostingView(
            rootView: BaseContainerView(document: document, tabID: tabID)
                .environmentObject(state))
        window.contentView = host
        window.makeKeyAndOrderFront(nil)

        guard document.handle == nil,
            document.hasPendingRetargetPreparation,
            document.quickFilterText == "typed filter survives retarget"
        else {
            return XCTFail(
                "mounting must not synchronously load, cancel the reservation, or clear input")
        }
        let preparationEntered = await eventually { gate.hasEntered }
        XCTAssertTrue(
            preparationEntered,
            "mounting a pending Base must schedule its background preparation")
        XCTAssertNil(document.handle)
        XCTAssertEqual(document.quickFilterText, "typed filter survives retarget")
        let tableMounted = await eventually { self.findTable(in: host) != nil }
        XCTAssertTrue(tableMounted)
        let table = try XCTUnwrap(findTable(in: host))
        XCTAssertTrue(
            table.tableColumns.allSatisfy { $0.sortDescriptorPrototype == nil },
            "a detached Base must not expose visually actionable sort headers")
        XCTAssertEqual(
            state.baseDocumentRefreshDisabledReason(for: document),
            AppState.baseDocumentReopeningDisabledReason,
            "Refresh must remain unavailable throughout a pending Base reopen")
        XCTAssertEqual(
            state.activeBaseRefreshDisabledReason,
            AppState.baseDocumentReopeningDisabledReason)

        var copiedQuickFilter: String?
        XCTAssertTrue(
            state.copyBaseQuickFilterDraft(document) { text in
                copiedQuickFilter = text
                return true
            })
        XCTAssertEqual(copiedQuickFilter, "typed filter survives retarget")

        gate.release()
        await state.nativeDocumentRetargetTask?.value
        XCTAssertNotNil(document.handle)
        XCTAssertFalse(document.hasPendingRetargetPreparation)
        XCTAssertEqual(document.quickFilterText, "typed filter survives retarget")
    }

    func testRecoverySheetsExposeRetryDiscardAndFocusReturnContracts() throws {
        let sources = Self.projectRoot.appendingPathComponent(
            "apps/slate-mac/Sources/SlateMac")
        let prompt = try String(
            contentsOf: sources.appendingPathComponent("Canvas/CanvasPromptSheet.swift"),
            encoding: .utf8)
        let picker = try String(
            contentsOf: sources.appendingPathComponent("Canvas/CanvasCardPicker.swift"),
            encoding: .utf8)
        let editor = try String(
            contentsOf: sources.appendingPathComponent("Canvas/CanvasCardEditorSheet.swift"),
            encoding: .utf8)
        let queryBuilder = try String(
            contentsOf: sources.appendingPathComponent("Bases/BaseQueryBuilderSheet.swift"),
            encoding: .utf8)
        let baseContainer = try String(
            contentsOf: sources.appendingPathComponent("Bases/BaseContainerView.swift"),
            encoding: .utf8)
        let canvasContainer = try String(
            contentsOf: sources.appendingPathComponent("Canvas/CanvasContainerView.swift"),
            encoding: .utf8)
        let canvasDocument = try String(
            contentsOf: sources.appendingPathComponent("Canvas/CanvasDocument.swift"),
            encoding: .utf8)
        let baseDocument = try String(
            contentsOf: sources.appendingPathComponent("Bases/BaseDocument.swift"),
            encoding: .utf8)

        XCTAssertTrue(prompt.contains("canvasRecoveryActionLabel"))
        XCTAssertTrue(prompt.contains("retryCanvasRecovery"))
        XCTAssertTrue(prompt.contains("interactiveDismissDisabled"))
        XCTAssertTrue(prompt.contains("confirmationDialog"))
        XCTAssertTrue(prompt.contains("draftDialogFocusReturn"))
        XCTAssertFalse(prompt.contains("appState.canvasPromptDraft = current"))
        XCTAssertGreaterThanOrEqual(
            prompt.components(separatedBy: ".textContentType(.none)").count - 1,
            6,
            "non-address Canvas inputs must opt out of address autofill semantics")

        XCTAssertTrue(picker.contains("canvasRecoveryActionLabel"))
        XCTAssertTrue(picker.contains("retryCanvasRecovery"))
        XCTAssertTrue(picker.contains("interactiveDismissDisabled"))
        XCTAssertTrue(picker.contains("confirmationDialog"))
        XCTAssertTrue(picker.contains("draftDialogFocusReturn"))
        XCTAssertTrue(picker.contains("textSelection(.enabled)"))

        XCTAssertTrue(editor.contains("draftDialogFocusReturn"))
        XCTAssertTrue(editor.contains("onChange(of: pendingDiscard)"))
        XCTAssertTrue(editor.contains("accessibilityFocused"))
        XCTAssertTrue(
            canvasDocument.contains(
                "@Published private var retargetPreparationInFlight"))

        XCTAssertTrue(queryBuilder.contains("baseQueryBuilderRecoveryActionLabel"))
        XCTAssertTrue(queryBuilder.contains("retryBaseQueryBuilderSourceRecovery"))
        XCTAssertTrue(baseContainer.contains("baseDocumentAvailabilityDisabledReason"))
        XCTAssertTrue(baseContainer.contains(".textSelection(.enabled)"))
        XCTAssertTrue(baseContainer.contains("admitBaseDocumentInteraction"))
        XCTAssertTrue(baseContainer.contains("baseRecoveryActionLabel"))
        XCTAssertTrue(baseContainer.contains("retryBaseRecovery"))
        XCTAssertTrue(baseContainer.contains("let recoveryDisabledReason"))
        XCTAssertTrue(baseContainer.contains("baseDefinitionEditingDisabledReason"))
        XCTAssertTrue(baseContainer.contains("appState.basesRefresh()"))
        XCTAssertTrue(
            baseContainer.contains(".disabled(baseRefreshDisabledReason != nil)"))
        XCTAssertTrue(
            baseDocument.contains(
                "@Published private var retargetPreparationInFlight"))

        let quickFilterBannerStart = try XCTUnwrap(
            baseContainer.range(of: "private var quickFilterDraftRecoveryBanner"))
        let quickFilterBannerEnd = try XCTUnwrap(
            baseContainer.range(
                of: "private var content",
                range: quickFilterBannerStart.upperBound..<baseContainer.endIndex))
        let quickFilterBanner = baseContainer[
            quickFilterBannerStart.lowerBound..<quickFilterBannerEnd.lowerBound]
        XCTAssertTrue(quickFilterBanner.contains("document.quickFilterText"))
        XCTAssertTrue(quickFilterBanner.contains(".textSelection(.enabled)"))
        XCTAssertTrue(quickFilterBanner.contains("copyBaseQuickFilterDraft"))

        let bannerStart = try XCTUnwrap(
            canvasContainer.range(of: "private func canvasQuarantineBanner"))
        let bannerEnd = try XCTUnwrap(
            canvasContainer.range(
                of: "private var header",
                range: bannerStart.upperBound..<canvasContainer.endIndex))
        let banner = canvasContainer[bannerStart.lowerBound..<bannerEnd.lowerBound]
        XCTAssertTrue(banner.contains("canvasRecoveryActionLabel"))
        XCTAssertTrue(banner.contains("retryCanvasRecovery"))
        XCTAssertFalse(banner.contains("retryBatchTrashUnknownReconciliation"))
    }
}
