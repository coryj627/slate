// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

@MainActor
final class SidebarActionInvocationTests: XCTestCase {
    private var root: URL!
    private enum ProbeError: Error { case formatterFailed }

    private final class LockedValue<Value>: @unchecked Sendable {
        private let lock = NSLock()
        private var storage: Value

        init(_ value: Value) {
            storage = value
        }

        func set(_ value: Value) {
            lock.lock()
            storage = value
            lock.unlock()
        }

        func mutate(_ body: (inout Value) -> Void) {
            lock.lock()
            body(&storage)
            lock.unlock()
        }

        var value: Value {
            lock.lock()
            defer { lock.unlock() }
            return storage
        }
    }

    private final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
        private(set) var posts: [(message: String, priority: AnnouncementPriority)] = []

        func post(_ message: String, priority: AnnouncementPriority) {
            posts.append((message, priority))
        }
    }

    private final class RecordingPasteboard: SidebarPasteboardWriting {
        private(set) var clearCount = 0
        private(set) var values: [String] = []

        func clearContents() {
            clearCount += 1
        }

        func setString(_ value: String) -> Bool {
            values.append(value)
            return true
        }
    }

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-sidebar-invocation-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func openVault(
        named name: String,
        files: [String] = [],
        folders: [String] = [],
        announcer: AnnouncementPosting = AppKitAnnouncementPoster(),
        sidebarPasteboard: SidebarPasteboardWriting = AppKitSidebarPasteboard()
    ) throws -> AppState {
        let vault = root.appendingPathComponent(name)
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for folder in folders {
            try FileManager.default.createDirectory(
                at: vault.appendingPathComponent(folder),
                withIntermediateDirectories: true)
        }
        for path in files {
            let url = vault.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(),
                withIntermediateDirectories: true)
            try "# \((path as NSString).lastPathComponent)".write(
                to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("\(name)-recents.json")),
            externalOpener: { _ in true },
            announcer: announcer,
            sidebarPasteboard: sidebarPasteboard)
        state.openVault(at: vault)
        let session = try XCTUnwrap(state.currentSession)
        _ = try session.scanInitial(cancel: CancelToken())
        return state
    }

    private func snapshot(
        on state: AppState,
        _ items: [SidebarSelectionItem],
        focusedPath: String? = nil,
        creationParent: String = ""
    ) throws -> SidebarSelectionSnapshot {
        SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(try XCTUnwrap(state.currentSession)),
            items: items,
            focusedPath: focusedPath,
            creationParent: creationParent)
    }

    private func item(
        _ path: String,
        directory: Bool = false,
        markdown: Bool = true
    ) -> SidebarSelectionItem {
        SidebarSelectionItem(
            path: path,
            isDirectory: directory,
            isMarkdown: !directory && markdown)
    }

    private func createValidationFiles(
        count: Int,
        in state: AppState,
        parent: String = "A/B"
    ) throws -> [String] {
        let vault = try XCTUnwrap(state.currentVaultURL)
        let parentURL = vault.appendingPathComponent(parent, isDirectory: true)
        try FileManager.default.createDirectory(
            at: parentURL,
            withIntermediateDirectories: true)
        return try (0..<count).map { index in
            let path = "\(parent)/Note-\(index).md"
            guard FileManager.default.createFile(
                atPath: vault.appendingPathComponent(path).path,
                contents: Data())
            else {
                throw CocoaError(.fileWriteUnknown)
            }
            return path
        }
    }

    private func intent(
        _ id: String,
        snapshot: SidebarSelectionSnapshot
    ) throws -> SidebarActionInvocationIntent {
        try XCTUnwrap(
            SidebarActionCatalog.evaluation(for: id, snapshot: snapshot)?.intent)
    }

    private func assertRejected(
        expectedMessage: String? = nil,
        context: String? = nil,
        _ operation: () throws -> SidebarActionDispatchResult,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        XCTAssertThrowsError(try operation(), file: file, line: line) { error in
            guard case CommandError.ActionFailed(message: let actualMessage) = error else {
                XCTFail(
                    "\(context.map { "\($0): " } ?? "")expected CommandError.ActionFailed, got \(error)",
                    file: file,
                    line: line)
                return
            }
            if let expectedMessage {
                XCTAssertEqual(
                    actualMessage,
                    expectedMessage,
                    context ?? "unexpected rejection message",
                    file: file,
                    line: line)
            }
        }
    }

    func test00SingleOpenUsesCurrentTabFromFrozenIntentAfterLiveSelectionChanges() throws {
        let state = try openVault(named: "open-single", files: ["A.md", "B.md"])
        let frozen = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let replacement = try snapshot(on: state, [item("B.md")], focusedPath: "B.md")
        let frozenIntent = try intent(SlateCommandID.sidebarOpen, snapshot: frozen)
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(replacement))

        var preflightBatches: [SidebarOpenSelectionBatch] = []
        var opens: [(path: String, target: AppState.OpenTarget)] = []
        state.sidebarActionDispatchOverrides.openPreflight = {
            preflightBatches.append($0)
            return true
        }
        state.sidebarActionDispatchOverrides.openPath = { path, target in
            opens.append((path, target))
            return true
        }

        XCTAssertEqual(
            try state.dispatchSidebarAction(frozenIntent),
            .opened(["A.md"]))
        XCTAssertEqual(preflightBatches.map(\.paths), [["A.md"]])
        XCTAssertEqual(opens.map(\.path), ["A.md"])
        XCTAssertEqual(
            opens.map(\.target), [.currentTab],
            "a one-file catalog Open is the ordinary current-tab action")
    }

    func test01OpenUnderTenPreflightsThenOpensFocusedLast() throws {
        let names = ["A.md", "B.md", "C.md"]
        let state = try openVault(named: "open-direct", files: names)
        let selection = try snapshot(
            on: state, names.map { item($0) }, focusedPath: "B.md")
        var events: [String] = []
        state.sidebarActionDispatchOverrides.openPreflight = { batch in
            events.append("preflight:\(batch.paths.joined(separator: ",")):\(batch.focusedPath ?? "nil")")
            return true
        }
        state.sidebarActionDispatchOverrides.openPath = { path, target in
            events.append("open:\(path):\(target)")
            return true
        }

        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.sidebarOpen, snapshot: selection)),
            .opened(["A.md", "C.md", "B.md"]))
        XCTAssertEqual(
            events,
            [
                "preflight:A.md,B.md,C.md:B.md",
                "open:A.md:newTab", "open:C.md:newTab", "open:B.md:newTab",
            ],
            "multi-file catalog Open keeps one new-tab target per frozen path")
    }

    func test02TenFileOpenSharesAlertUUIDDoesNotPreopenAndConfirmsFrozenBatch() async throws {
        let names = (1...10).map { "Note-\($0).md" }
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "open-confirm", files: names + ["Other.md"], announcer: announcer)
        let selection = try snapshot(
            on: state, names.map { item($0) }, focusedPath: names[2])
        let openIntent = try intent(SlateCommandID.sidebarOpen, snapshot: selection)
        var preflights = 0
        var opened: [String] = []
        state.sidebarActionDispatchOverrides.openPreflight = { _ in
            preflights += 1
            return true
        }
        state.sidebarActionDispatchOverrides.openPath = { path, target in
            XCTAssertEqual(target, .newTab)
            opened.append(path)
            return true
        }

        let announcementBaseline = announcer.posts.count
        let result = try state.dispatchSidebarAction(openIntent)
        guard case .openConfirmation(let request) = result else {
            return XCTFail("10+ Open must stage the shared alert")
        }
        guard case .open(let active)? = state.activeBatchAlertPresentation else {
            return XCTFail("AppState must own the same alert request")
        }
        XCTAssertEqual(preflights, 1)
        XCTAssertEqual(opened, [], "staging performs no pre-open")
        XCTAssertEqual(
            announcer.posts.count, announcementBaseline,
            "synchronous alert staging never owns an accessibility announcement")
        XCTAssertEqual(active.id, request.id)
        XCTAssertEqual(active, request)
        XCTAssertEqual(request.intent, openIntent)
        XCTAssertNil(
            state.deferredBatchAlertPresentation,
            "the first Open confirmation owns the active slot without duplicating itself")

        let replacement = try snapshot(
            on: state, [item("Other.md")], focusedPath: "Other.md")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(replacement))
        let executionPaths = names.filter { $0 != names[2] } + [names[2]]
        XCTAssertEqual(
            state.confirmOpenSelection(id: request.id),
            .opened(executionPaths),
            "confirmation consumes and opens the frozen batch, not the live replacement selection")
        XCTAssertEqual(preflights, 2, "confirmation rechecks current Open admission once")
        XCTAssertEqual(opened, executionPaths)
        XCTAssertEqual(announcer.posts.count, announcementBaseline)
        XCTAssertNil(state.deferredBatchAlertPresentation)

        for drift in ConfirmedOpenDrift.allCases {
            try await assertConfirmedOpenRejectsPostStagingDrift(drift)
        }
    }

    private enum ConfirmedOpenDrift: String, CaseIterable {
        case missing
        case typeChanged
        case liveSymlink
        case danglingSymlink
        case intermediateSymlink
        case propertyEditNavigation
    }

    private func assertConfirmedOpenRejectsPostStagingDrift(
        _ drift: ConfirmedOpenDrift
    ) async throws {
        let names = (1...10).map { "Folder/Note-\($0).md" }
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "confirm-drift-\(drift.rawValue)",
            files: names + ["Other.md"],
            folders: ["Folder"],
            announcer: announcer)
        let vault = try XCTUnwrap(state.currentVaultURL)
        let selection = try snapshot(
            on: state, names.map { item($0) }, focusedPath: names[2],
            creationParent: "Folder")
        let openIntent = try intent(SlateCommandID.sidebarOpen, snapshot: selection)
        var preflightCalls = 0
        if drift != .propertyEditNavigation {
            state.sidebarActionDispatchOverrides.openPreflight = { _ in
                preflightCalls += 1
                return true
            }
        }
        var opened: [String] = []
        state.sidebarActionDispatchOverrides.openPath = { path, target in
            XCTAssertEqual(target, .newTab)
            opened.append(path)
            return true
        }

        guard case .openConfirmation(let request) =
            try state.dispatchSidebarAction(openIntent)
        else {
            return XCTFail("\(drift.rawValue): staging must succeed before drift")
        }
        XCTAssertEqual(request.intent, openIntent)
        let announcementBaseline = announcer.posts.count
        let preflightBaseline = preflightCalls

        let replacement = try snapshot(
            on: state, [item("Other.md")], focusedPath: "Other.md")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(replacement))

        var propertyTask: Task<Void, Never>?
        let expectedReason: String
        switch drift {
        case .missing:
            try FileManager.default.removeItem(
                at: vault.appendingPathComponent(names.last!))
            expectedReason = AppState.sidebarSelectionChangedReason
        case .typeChanged:
            let url = vault.appendingPathComponent(names.last!)
            try FileManager.default.removeItem(at: url)
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: false)
            expectedReason = AppState.sidebarSelectionChangedReason
        case .liveSymlink:
            let url = vault.appendingPathComponent(names.last!)
            try FileManager.default.removeItem(at: url)
            try FileManager.default.createSymbolicLink(
                at: url,
                withDestinationURL: vault.appendingPathComponent(names[0]))
            expectedReason = AppState.sidebarSelectionChangedReason
        case .danglingSymlink:
            let url = vault.appendingPathComponent(names.last!)
            try FileManager.default.removeItem(at: url)
            try FileManager.default.createSymbolicLink(
                at: url,
                withDestinationURL: vault.appendingPathComponent("MissingTarget.md"))
            expectedReason = AppState.sidebarSelectionChangedReason
        case .intermediateSymlink:
            let folder = vault.appendingPathComponent("Folder")
            let backing = vault.appendingPathComponent("Backing")
            try FileManager.default.moveItem(at: folder, to: backing)
            try FileManager.default.createSymbolicLink(
                at: folder, withDestinationURL: backing)
            expectedReason = AppState.sidebarSelectionChangedReason
        case .propertyEditNavigation:
            propertyTask = state.setProperty(
                path: names[0], key: "status", value: .text(value: "busy"))
            XCTAssertNotNil(propertyTask)
            XCTAssertEqual(
                state.propertyEditNavigationDisabledReason,
                AppState.propertyEditInProgressReason)
            expectedReason = AppState.propertyEditInProgressReason
        }

        let outcome = state.confirmOpenSelection(id: request.id)
        guard case .rejected(let message) = outcome else {
            propertyTask?.cancel()
            await propertyTask?.value
            return XCTFail("\(drift.rawValue): expected typed rejection, got \(outcome)")
        }
        XCTAssertEqual(message, expectedReason)
        XCTAssertEqual(opened, [], "\(drift.rawValue): no path opens after staging drift")
        XCTAssertEqual(
            preflightCalls, preflightBaseline,
            "\(drift.rawValue): item revalidation finishes before Open admission")
        XCTAssertEqual(announcer.posts.count, announcementBaseline + 1)
        XCTAssertEqual(announcer.posts.last?.message, expectedReason)
        XCTAssertEqual(announcer.posts.last?.priority, .medium)
        XCTAssertNil(state.activeBatchAlertPresentation)
        XCTAssertNil(state.deferredBatchAlertPresentation)

        propertyTask?.cancel()
        await propertyTask?.value
    }

    func test03CreationUsesCanonicalCapturedParentMatrixNotLegacyMirror() throws {
        let state = try openVault(
            named: "creation", files: ["Root.md", "Folder/Nested.md"],
            folders: ["Folder", "Elsewhere"])
        let cases: [(SidebarSelectionSnapshot, String)] = [
            (try snapshot(on: state, []), ""),
            (try snapshot(on: state, [item("Root.md")], focusedPath: "Root.md"), ""),
            (try snapshot(
                on: state, [item("Folder/Nested.md")], focusedPath: "Folder/Nested.md",
                creationParent: "Folder"), "Folder"),
            (try snapshot(
                on: state, [item("Folder", directory: true)], focusedPath: "Folder",
                creationParent: "Folder"), "Folder"),
        ]
        var noteParents: [String] = []
        var folderParents: [String] = []
        state.sidebarActionDispatchOverrides.createNote = {
            noteParents.append($0)
            return true
        }
        state.sidebarActionDispatchOverrides.createFolder = {
            folderParents.append($0)
            return true
        }
        for (selection, _) in cases {
            state.treeSelectedNode = .init(path: "Elsewhere", isDirectory: true)
            XCTAssertEqual(
                try state.dispatchSidebarAction(intent(SlateCommandID.newNote, snapshot: selection)),
                .completed(actionID: SlateCommandID.newNote))
            state.treeSelectedNode = .init(path: "Elsewhere", isDirectory: true)
            XCTAssertEqual(
                try state.dispatchSidebarAction(intent(SlateCommandID.newFolder, snapshot: selection)),
                .completed(actionID: SlateCommandID.newFolder))
        }
        XCTAssertEqual(noteParents, cases.map(\.1))
        XCTAssertEqual(folderParents, cases.map(\.1))

        let forged = try snapshot(
            on: state, [item("Folder/Nested.md")], focusedPath: "Folder/Nested.md",
            creationParent: "Elsewhere")
        let acceptedParentCount = noteParents.count + folderParents.count
        assertRejected(context: "forged New Note parent") {
            try state.dispatchSidebarAction(intent(SlateCommandID.newNote, snapshot: forged))
        }
        assertRejected(context: "forged New Folder parent") {
            try state.dispatchSidebarAction(intent(SlateCommandID.newFolder, snapshot: forged))
        }
        XCTAssertEqual(
            noteParents.count + folderParents.count, acceptedParentCount,
            "neither creation funnel sees a forged non-canonical parent")
    }

    func test04TemplateUsesExactCanonicalFrozenCreationParent() async throws {
        let state = try openVault(named: "template", folders: ["Folder"])
        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        XCTAssertEqual(state.templateAvailability, .available)
        var parents: [String] = []
        state.sidebarActionDispatchOverrides.openTemplatePicker = {
            parents.append($0)
            return true
        }
        let rootIntent = try snapshot(on: state, [])
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.newFromTemplate, snapshot: rootIntent)),
            .completed(actionID: SlateCommandID.newFromTemplate))

        let nonRoot = try snapshot(
            on: state, [item("Folder", directory: true)], focusedPath: "Folder",
            creationParent: "Folder")
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.newFromTemplate, snapshot: nonRoot)),
            .completed(actionID: SlateCommandID.newFromTemplate))
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(nonRoot))
        XCTAssertEqual(
            try state.dispatchSidebarAction(id: SlateCommandID.newFromTemplate),
            .completed(actionID: SlateCommandID.newFromTemplate))
        XCTAssertEqual(
            parents, ["", "Folder", "Folder"],
            "root, selected-row, and ID invocations preserve their canonical frozen parent")
    }

    func test04aUnavailableTemplateKeyboardInvocationAnnouncesEachSharedReasonOnceWithoutPresentation()
        async throws
    {
        let announcer = RecordingAnnouncer()
        let state = try openVault(named: "template-keyboard-unavailable", announcer: announcer)
        XCTAssertEqual(state.templateAvailability, .loading)

        XCTAssertTrue(state.invokeSidebarKeyboardAction(SlateCommandID.newFromTemplate))

        await state.templateAvailabilityTask?.value
        XCTAssertEqual(state.templateAvailability, .empty)
        XCTAssertTrue(state.invokeSidebarKeyboardAction(SlateCommandID.newFromTemplate))

        state.templateListRunner = { _, _ in .failure(.Io(message: "no access")) }
        await state.refreshTemplateAvailability()?.value
        XCTAssertTrue(state.invokeSidebarKeyboardAction(SlateCommandID.newFromTemplate))

        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        state.sidebarActionStructuralDisabledReasonOverride =
            AppState.structuralMutationBusyReason
        XCTAssertTrue(state.invokeSidebarKeyboardAction(SlateCommandID.newFromTemplate))

        XCTAssertEqual(
            announcer.posts.map(\.message),
            [
                AppState.templateAvailabilityLoadingReason,
                AppState.templateAvailabilityEmptyReason,
                AppState.templateAvailabilityFailedReason,
                AppState.structuralMutationBusyReason,
            ])
        XCTAssertFalse(state.isTemplatePickerOpen)
        XCTAssertNil(state.templatePickerTask)
        XCTAssertNil(state.templateCreationDestination)
        XCTAssertEqual(state.pendingTemplateFlow, .idle)
        let markdownFiles = try FileManager.default.subpathsOfDirectory(
            atPath: try XCTUnwrap(state.currentVaultURL).path
        ).filter { ($0 as NSString).pathExtension.lowercased() == "md" }
        XCTAssertTrue(markdownFiles.isEmpty)
    }

    func test04a2CachedAvailableRelistEmptyAndFailureStayVisibleAndAnnounceOnce()
        async throws
    {
        let announcer = RecordingAnnouncer()
        let state = try openVault(named: "template-relist-announcements", announcer: announcer)
        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        XCTAssertEqual(state.templateAvailability, .available)

        state.templateListRunner = { _, _ in .success([]) }
        let emptyRelist = state.openTemplatePicker()
        XCTAssertNotNil(emptyRelist)
        await emptyRelist?.value
        await state.templateAvailabilityTask?.value
        XCTAssertEqual(state.templateAvailability, .empty)
        XCTAssertEqual(
            state.templateAnnouncementLastMessage,
            AppState.templateAvailabilityEmptyReason)
        XCTAssertTrue(
            state.isTemplatePickerOpen,
            "the empty result must remain visible with setup guidance")
        XCTAssertEqual(state.templateCreationDestination, "")

        state.cancelTemplateFlow()

        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        state.templateListRunner = { _, _ in .failure(.Io(message: "no access")) }
        let failedRelist = state.openTemplatePicker()
        XCTAssertNotNil(failedRelist)
        await failedRelist?.value
        await state.templateAvailabilityTask?.value
        XCTAssertEqual(state.templateAvailability, .failed)
        XCTAssertEqual(
            state.templateAnnouncementLastMessage,
            AppState.templateAvailabilityFailedReason)
        XCTAssertTrue(
            state.isTemplatePickerOpen,
            "the failed result must remain visible with Retry")
        XCTAssertEqual(state.templateCreationDestination, "")

        XCTAssertEqual(
            announcer.posts.map(\.message),
            [
                AppState.templateAvailabilityEmptyReason,
                AppState.templateAvailabilityFailedReason,
            ],
            "each user-triggered relist terminal state must announce its shared reason once")
    }

    func test04bAvailableTemplateKeyboardInvocationDispatchesFrozenDestinationOnce()
        async throws
    {
        let state = try openVault(
            named: "template-keyboard-available", folders: ["Projects"])
        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        let folder = try snapshot(
            on: state,
            [item("Projects", directory: true)],
            focusedPath: "Projects",
            creationParent: "Projects")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(folder))
        var destinations: [String] = []
        state.sidebarActionDispatchOverrides.openTemplatePicker = {
            destinations.append($0)
            return true
        }

        XCTAssertTrue(state.invokeSidebarKeyboardAction(SlateCommandID.newFromTemplate))

        XCTAssertEqual(destinations, ["Projects"])
    }

    func test04bAvailableTemplateToolbarInvocationDispatchesFrozenDestinationOnce()
        async throws
    {
        let state = try openVault(
            named: "template-toolbar-available", folders: ["Projects", "Other"])
        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        let projects = try snapshot(
            on: state,
            [item("Projects", directory: true)],
            focusedPath: "Projects",
            creationParent: "Projects")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(projects))
        let toolbarEvaluation = try XCTUnwrap(
            state.sidebarActionProjection(surface: .toolbar).first {
                $0.id == SlateCommandID.newFromTemplate
            })
        let frozenIntent = try XCTUnwrap(toolbarEvaluation.intent)

        let other = try snapshot(
            on: state,
            [item("Other", directory: true)],
            focusedPath: "Other",
            creationParent: "Other")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(other))
        var destinations: [String] = []
        state.sidebarActionDispatchOverrides.openTemplatePicker = {
            destinations.append($0)
            return true
        }

        XCTAssertEqual(
            try state.dispatchSidebarAction(frozenIntent),
            .completed(actionID: SlateCommandID.newFromTemplate))
        XCTAssertEqual(destinations, ["Projects"])
    }

    func test04b2TemplateKeyboardCannotRestartOrRetargetAnActiveFlow() async throws {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "template-keyboard-active-flow",
            folders: ["Projects", "Other"],
            announcer: announcer)
        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        let projects = try snapshot(
            on: state,
            [item("Projects", directory: true)],
            focusedPath: "Projects",
            creationParent: "Projects")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(projects))
        XCTAssertTrue(state.invokeSidebarKeyboardAction(SlateCommandID.newFromTemplate))
        XCTAssertEqual(state.templateCreationDestination, "Projects")

        let other = try snapshot(
            on: state,
            [item("Other", directory: true)],
            focusedPath: "Other",
            creationParent: "Other")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(other))
        XCTAssertTrue(state.invokeSidebarKeyboardAction(SlateCommandID.newFromTemplate))

        XCTAssertEqual(state.templateCreationDestination, "Projects")
        XCTAssertEqual(announcer.posts.map(\.message), [AppState.templateFlowBusyReason])
        state.cancelTemplateFlow()
    }

    func test04b3SharedAdmissionRejectsMenuAndFrozenIntentFromAnotherWindow()
        async throws
    {
        let state = try openVault(
            named: "template-shared-modal-admission", folders: ["Projects"])
        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        let projects = try snapshot(
            on: state,
            [item("Projects", directory: true)],
            focusedPath: "Projects",
            creationParent: "Projects")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(projects))

        let host = NSWindow()
        let other = NSWindow()
        state.setTemplateShortcutHostWindow(host)
        state.templateShortcutKeyWindowProvider = { host }
        let admitted = try XCTUnwrap(
            state.sidebarActionProjection(surface: .menuBar).first {
                $0.id == SlateCommandID.newFromTemplate
            })
        let frozenIntent = try XCTUnwrap(admitted.intent)

        var destinations: [String] = []
        state.sidebarActionDispatchOverrides.openTemplatePicker = {
            destinations.append($0)
            return true
        }
        state.templateShortcutKeyWindowProvider = { other }

        let blocked = try XCTUnwrap(
            state.sidebarActionProjection(surface: .menuBar).first {
                $0.id == SlateCommandID.newFromTemplate
            })
        XCTAssertNil(blocked.intent)
        XCTAssertEqual(blocked.disabledReason, AppState.templateOtherWindowReason)
        assertRejected(expectedMessage: AppState.templateOtherWindowReason) {
            try state.dispatchSidebarAction(frozenIntent)
        }
        XCTAssertTrue(
            destinations.isEmpty,
            "a stale enabled File-menu evaluation must not reach the template funnel")

        state.templateShortcutModalWindowProvider = { other }
        let appModal = try XCTUnwrap(
            state.sidebarActionProjection(surface: .menuBar).first {
                $0.id == SlateCommandID.newFromTemplate
            })
        XCTAssertNil(appModal.intent)
        XCTAssertEqual(appModal.disabledReason, AppState.templateDialogBusyReason)
        assertRejected(expectedMessage: AppState.templateDialogBusyReason) {
            try state.dispatchSidebarAction(frozenIntent)
        }
        XCTAssertTrue(destinations.isEmpty)
        state.templateShortcutModalWindowProvider = { nil }

        state.setTemplateShortcutHostWindow(nil)
        let hostless = try XCTUnwrap(
            state.sidebarActionProjection(surface: .menuBar).first {
                $0.id == SlateCommandID.newFromTemplate
            })
        XCTAssertNil(hostless.intent)
        XCTAssertEqual(hostless.disabledReason, AppState.templateOtherWindowReason)
        assertRejected(expectedMessage: AppState.templateOtherWindowReason) {
            try state.dispatchSidebarAction(frozenIntent)
        }
        XCTAssertTrue(
            destinations.isEmpty,
            "Settings must not stage a hidden flow after the main host closes")

        state.templateShortcutKeyWindowProvider = { nil }
        let noKeyWindow = try XCTUnwrap(
            state.sidebarActionProjection(surface: .menuBar).first {
                $0.id == SlateCommandID.newFromTemplate
            })
        XCTAssertNil(noKeyWindow.intent)
        XCTAssertEqual(
            noKeyWindow.disabledReason,
            AppState.templateOtherWindowReason,
            "once a UI host existed, a missing key window must fail closed")
        assertRejected(expectedMessage: AppState.templateOtherWindowReason) {
            try state.dispatchSidebarAction(frozenIntent)
        }
        XCTAssertTrue(destinations.isEmpty)

        state.setTemplateShortcutHostWindow(host)
        state.templateShortcutKeyWindowProvider = { host }
        XCTAssertEqual(
            try state.dispatchSidebarAction(frozenIntent),
            .completed(actionID: SlateCommandID.newFromTemplate))
        XCTAssertEqual(destinations, ["Projects"])
    }

    func test04b4RegisteredCommandPaletteWindowCanLaunchAndDismissesBeforeTemplateFlow()
        async throws
    {
        let state = try openVault(
            named: "template-command-palette-admission", folders: ["Projects"])
        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        let projects = try snapshot(
            on: state,
            [item("Projects", directory: true)],
            focusedPath: "Projects",
            creationParent: "Projects")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(projects))

        let host = NSWindow()
        let palette = NSWindow()
        state.setTemplateShortcutHostWindow(host)
        state.setTemplateShortcutActionLauncherWindow(palette)
        state.templateShortcutKeyWindowProvider = { palette }
        state.isCommandPaletteOpen = true

        let evaluation = try XCTUnwrap(
            state.sidebarActionProjection(surface: .commandPalette).first {
                $0.id == SlateCommandID.newFromTemplate
            })
        let intent = try XCTUnwrap(
            evaluation.intent,
            "the palette's registered sheet must keep its Template action available")
        var destinations: [String] = []
        state.sidebarActionDispatchOverrides.openTemplatePicker = {
            destinations.append($0)
            return true
        }

        XCTAssertEqual(
            try state.dispatchSidebarAction(intent),
            .completed(actionID: SlateCommandID.newFromTemplate))
        XCTAssertFalse(
            state.isCommandPaletteOpen,
            "the action-launcher sheet must dismiss before the template sheet stages")
        XCTAssertEqual(destinations, ["Projects"])

        state.setTemplateShortcutActionLauncherWindow(nil)
        XCTAssertFalse(state.templateShortcutWindowOwnsActionLauncher(palette))
    }

    func test04cTemplateShortcutRemainsVisibleInMenuAndRoutesThroughOneEventMonitor()
        throws
    {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(
            contentsOf: packageRoot.appendingPathComponent("Sources/SlateMac/SlateMacApp.swift"),
            encoding: .utf8)
        let paletteSource = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/SlateMac/CommandPaletteView.swift"),
            encoding: .utf8)
        let shortcutFunction = try XCTUnwrap(
            source.range(of: "private static func sidebarMenuKeyboardShortcut"))
        let renderer = try XCTUnwrap(
            source.range(of: "private func sidebarFileMenuActions"))
        let menuShortcutBody = source[shortcutFunction.lowerBound..<renderer.lowerBound]
        XCTAssertTrue(
            menuShortcutBody.contains("SlateCommandID.newFromTemplate"),
            "the disabled File-menu item must still advertise Shift-Command-N")
        XCTAssertEqual(
            source.components(
                separatedBy: "KeyboardShortcut(\"n\", modifiers: [.command, .shift])"
            ).count - 1,
            1,
            "the File menu must advertise exactly one Shift-Command-N key equivalent")
        XCTAssertTrue(source.contains("SidebarTemplateShortcutMonitor"))
        XCTAssertTrue(source.contains("NSApplication.didBecomeActiveNotification"))
        XCTAssertFalse(
            source.contains(".keyboardShortcut(\n                \"n\", modifiers: [.command, .shift])"),
            "a second hidden SwiftUI shortcut owner would double-dispatch")
        XCTAssertTrue(
            source.contains(
                "appState.invokeSidebarKeyboardAction(SlateCommandID.newFromTemplate)"))
        XCTAssertTrue(source.contains("presentDialogNotice"))
        XCTAssertTrue(source.contains("slate.template-shortcut-dialog-notice"))
        XCTAssertTrue(source.contains("NSTitlebarAccessoryViewController"))
        XCTAssertTrue(source.contains("controller.layoutAttribute = .bottom"))
        XCTAssertFalse(
            source.contains("contentView.addSubview(notice"),
            "persistent feedback must reserve layout instead of covering dialog controls")
        XCTAssertTrue(source.contains("rejectBlockedWindow"))
        XCTAssertTrue(paletteSource.contains("CommandPaletteWindowReader"))
        XCTAssertTrue(
            paletteSource.contains("setTemplateShortcutActionLauncherWindow"))
    }

    func test04dTemplateSheetsExposeVisibleAndSpokenDestinationContext() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let picker = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/SlateMac/TemplatePicker.swift"),
            encoding: .utf8)
        let prompts = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/SlateMac/TemplatePromptSheet.swift"),
            encoding: .utf8)
        XCTAssertTrue(picker.contains("templateCreationDestinationDescription"))
        XCTAssertGreaterThanOrEqual(
            prompts.components(separatedBy: "templateCreationDestinationDescription").count - 1,
            2,
            "both prompt and name steps must preserve destination context")
        XCTAssertTrue(prompts.contains("Name relative to"))
    }

    func test04eTemplateShortcutMonitorOwnsAttachedSheetAndAnnouncesBusyOnce()
        async throws
    {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "template-sheet-shortcut", folders: ["Projects"], announcer: announcer)
        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        state.installTemplateDestinationForTesting("Projects")

        let host = NSWindow()
        let other = NSWindow()
        let sheet = NSWindow()
        host.beginSheet(sheet, completionHandler: nil)
        defer { host.endSheet(sheet) }
        var rejectedWindowCount = 0
        var rejectedReasons: [String] = []
        let common: (
            NSWindow?, NSWindow?, NSWindow?
        ) -> Bool = { eventWindow, keyWindow, modalWindow in
            SidebarTemplateShortcutRouting.route(
                applicationIsActive: true,
                hostWindow: host,
                hostAttachedSheet: sheet,
                modalWindow: modalWindow,
                attachedSheetOwnsTemplateAction: true,
                eventWindow: eventWindow,
                keyWindow: keyWindow,
                hasMarkedText: false,
                isRepeat: false,
                charactersIgnoringModifiers: "n",
                modifierFlags: [.command, .shift],
                rejectBlockedWindow: { context in
                    rejectedWindowCount += 1
                    rejectedReasons.append(context.reason)
                    return true
                },
                invoke: {
                    state.invokeSidebarKeyboardAction(SlateCommandID.newFromTemplate)
                })
        }

        XCTAssertTrue(
            common(other, other, nil),
            "an exact chord in another Slate window must be consumed before menu fallback")
        XCTAssertEqual(rejectedWindowCount, 1)
        XCTAssertEqual(rejectedReasons, [AppState.templateOtherWindowReason])
        XCTAssertTrue(
            common(other, other, other),
            "an app-modal panel must be consumed with dialog recovery")
        XCTAssertEqual(rejectedWindowCount, 2)
        XCTAssertEqual(
            rejectedReasons,
            [AppState.templateOtherWindowReason, AppState.templateDialogBusyReason])
        XCTAssertFalse(
            common(host, other, nil), "a non-key Slate window must receive its event")
        XCTAssertTrue(
            common(sheet, sheet, nil),
            "the host window's attached sheet must route and consume the owned chord")
        XCTAssertEqual(state.templateCreationDestination, "Projects")
        XCTAssertEqual(announcer.posts.map(\.message), [AppState.templateFlowBusyReason])
        XCTAssertEqual(rejectedWindowCount, 2)
        XCTAssertNil(state.templatePickerTask)
        XCTAssertEqual(state.pendingTemplateFlow, .idle)
    }

    func test04e1UnrelatedAttachedSheetConsumesShortcutWithoutStartingTemplateFlow()
        async throws
    {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "template-unrelated-sheet", announcer: announcer)
        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        XCTAssertEqual(state.templateAvailability, .available)

        let host = NSWindow()
        let sheet = NSWindow()
        host.beginSheet(sheet, completionHandler: nil)
        defer { host.endSheet(sheet) }

        let handled = SidebarTemplateShortcutRouting.route(
            applicationIsActive: true,
            hostWindow: host,
            hostAttachedSheet: sheet,
            modalWindow: nil,
            attachedSheetOwnsTemplateAction: false,
            eventWindow: sheet,
            keyWindow: sheet,
            hasMarkedText: false,
            isRepeat: false,
            charactersIgnoringModifiers: "n",
            modifierFlags: [.command, .shift],
            rejectBlockedWindow: { context in
                XCTAssertEqual(context.reason, AppState.templateDialogBusyReason)
                return state.rejectTemplateShortcutForActiveDialog()
            },
            invoke: {
                XCTFail("an unrelated sheet must not dispatch the template action")
                return true
            })

        XCTAssertTrue(handled, "the chord must be consumed before the File menu sees it")
        XCTAssertEqual(
            announcer.posts.map(\.message),
            [AppState.templateDialogBusyReason])
        XCTAssertEqual(
            state.templateShortcutDialogNotice,
            AppState.templateDialogBusyReason)
        XCTAssertNil(state.templateCreationDestination)
        XCTAssertNil(state.templatePickerTask)
        XCTAssertFalse(state.isTemplatePickerOpen)
        XCTAssertEqual(state.pendingTemplateFlow, .idle)
    }

    func test04e2TemplatePickerRendersVisibleFailureAndRetryGuidance() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/SlateMac/TemplatePicker.swift"),
            encoding: .utf8)

        let emptyStart = try XCTUnwrap(
            source.range(of: "private var emptyState: some View"))
        let failedStart = try XCTUnwrap(
            source.range(of: "private var failedState: some View"))
        let retryStart = try XCTUnwrap(
            source.range(of: "private var retryButton: some View"))
        let focusStart = try XCTUnwrap(
            source.range(of: "private func updateFocus"))
        let emptyBody = source[emptyStart.lowerBound..<failedStart.lowerBound]
        let failedBody = source[failedStart.lowerBound..<retryStart.lowerBound]
        let retryBody = source[retryStart.lowerBound..<focusStart.lowerBound]

        XCTAssertTrue(source.contains("templateAvailability == .failed"))
        XCTAssertTrue(source.contains("Couldn’t load templates"))
        for (name, stateBody) in [
            ("empty", emptyBody),
            ("failed", failedBody),
        ] {
            XCTAssertTrue(
                stateBody.contains("retryButton"),
                "the \(name) state must retain the shared Retry control")
            XCTAssertTrue(
                stateBody.contains(".accessibilityElement(children: .contain)"),
                "the \(name) state must expose Retry as a distinct AX child")
            XCTAssertFalse(
                stateBody.contains(".accessibilityElement(children: .combine)"),
                "the \(name) state must not swallow Retry into static guidance")
        }
        XCTAssertTrue(retryBody.contains("Button(\"Try Again\")"))
        XCTAssertTrue(retryBody.contains("appState.retryTemplatePickerLoad()"))
        XCTAssertTrue(retryBody.contains(".focused($focus, equals: .retry)"))
        XCTAssertTrue(
            retryBody.contains(
                ".accessibilityHint(\"Reloads templates for the same destination.\")"))
    }

    func test04e3FolderVoiceOverHintDoesNotPromiseAnOmittedTemplateAction() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/SlateMac/FileTreeSidebar.swift"),
            encoding: .utf8)

        XCTAssertFalse(
            source.contains("template actions are in the context menu"),
            "a folder hint must not promise Template when the shared projection omits it")
    }

    func test05RenameUsesExactCapturedPathKindAndRejectsFalseAdmission() throws {
        let state = try openVault(named: "rename", files: ["A.md"], folders: ["Other"])
        let selection = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        state.treeSelectedNode = .init(path: "Other", isDirectory: true)
        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.renameEntry, snapshot: selection)),
            .completed(actionID: SlateCommandID.renameEntry))
        XCTAssertEqual(state.renamingNode?.path, "A.md")
        XCTAssertEqual(state.renamingNode?.isDirectory, false)
        if let pending = state.renamingNode { XCTAssertTrue(state.cancelPendingRename(id: pending.id)) }

        var captured: AppState.TreeSelection?
        state.sidebarActionDispatchOverrides.rename = {
            captured = $0
            return false
        }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.renameEntry, snapshot: selection))
        }
        XCTAssertEqual(captured, .init(path: "A.md", isDirectory: false))
        XCTAssertNil(state.renamingNode)
    }

    func test06MoveUsesExactSingleOrOrderedBatchFocusAndRejectsFalseAdmission() throws {
        let state = try openVault(
            named: "move", files: ["A.md", "B.md"], folders: ["Folder"])
        let single = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.moveTo, snapshot: single)),
            .completed(actionID: SlateCommandID.moveTo))
        let pendingSingle = try XCTUnwrap(state.pendingMove)
        XCTAssertEqual(pendingSingle.path, "A.md")
        XCTAssertFalse(pendingSingle.isDirectory)
        XCTAssertTrue(state.cancelPendingMove(id: pendingSingle.id))

        let batch = try snapshot(
            on: state, [item("B.md"), item("Folder", directory: true), item("A.md")],
            focusedPath: "Folder")
        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.moveTo, snapshot: batch)),
            .completed(actionID: SlateCommandID.moveTo))
        XCTAssertEqual(state.pendingBatchMove?.items.map(\.path), ["B.md", "Folder", "A.md"])
        XCTAssertEqual(state.pendingBatchMove?.preferredFocusPath, "Folder")
        if let pending = state.pendingBatchMove { XCTAssertTrue(state.cancelPendingBatchMove(id: pending.id)) }

        state.sidebarActionDispatchOverrides.moveSingle = { _ in false }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.moveTo, snapshot: single))
        }
        state.sidebarActionDispatchOverrides.moveSingle = nil
        state.sidebarActionDispatchOverrides.moveBatch = { _, _ in false }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.moveTo, snapshot: batch))
        }
        XCTAssertNil(state.pendingMove)
        XCTAssertNil(state.pendingBatchMove)
    }

    func test07DuplicateUsesExactFileAndNilAdmissionRejects() throws {
        let state = try openVault(named: "duplicate", files: ["Folder/A.md"], folders: ["Folder"])
        let selection = try snapshot(
            on: state, [item("Folder/A.md")], focusedPath: "Folder/A.md",
            creationParent: "Folder")
        var captured: [String] = []
        state.sidebarActionDispatchOverrides.duplicate = {
            captured.append($0)
            return true
        }
        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.duplicateEntry, snapshot: selection)),
            .completed(actionID: SlateCommandID.duplicateEntry))
        state.sidebarActionDispatchOverrides.duplicate = {
            captured.append($0)
            return false
        }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.duplicateEntry, snapshot: selection))
        }
        XCTAssertEqual(captured, ["Folder/A.md", "Folder/A.md"])
    }

    func test08RevealUsesCapturedPathThroughFallibleAdapter() throws {
        let state = try openVault(named: "reveal", files: ["Folder/A.md"], folders: ["Folder"])
        let vaultURL = try XCTUnwrap(state.currentVaultURL)
        let selection = try snapshot(
            on: state, [item("Folder/A.md")], focusedPath: "Folder/A.md",
            creationParent: "Folder")
        var requests: [(URL, String)] = []
        state.sidebarActionDispatchOverrides.reveal = { vaultURL, path in
            requests.append((vaultURL, path))
            return true
        }
        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.revealInFinder, snapshot: selection)),
            .completed(actionID: SlateCommandID.revealInFinder))
        state.sidebarActionDispatchOverrides.reveal = { vaultURL, path in
            requests.append((vaultURL, path))
            return false
        }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.revealInFinder, snapshot: selection))
        }
        XCTAssertEqual(requests.map(\.0), [vaultURL, vaultURL])
        XCTAssertEqual(requests.map(\.1), ["Folder/A.md", "Folder/A.md"])
    }

    func test09CopyPathWritesVaultRelativeValueAndCompletes() throws {
        let announcer = RecordingAnnouncer()
        let pasteboard = RecordingPasteboard()
        let state = try openVault(
            named: "copy-path", files: ["Folder/A.md"], folders: ["Folder"],
            announcer: announcer, sidebarPasteboard: pasteboard)
        let selection = try snapshot(
            on: state, [item("Folder/A.md")], focusedPath: "Folder/A.md",
            creationParent: "Folder")
        let announcementBaseline = announcer.posts.count
        let result = try state.dispatchSidebarAction(
            intent(SlateCommandID.copyPath, snapshot: selection))
        XCTAssertEqual(result, .completed(actionID: SlateCommandID.copyPath))
        XCTAssertFalse(String(describing: result).contains(root.path))
        XCTAssertEqual(pasteboard.clearCount, 1)
        XCTAssertEqual(pasteboard.values, ["Folder/A.md"])
        XCTAssertEqual(
            announcer.posts.count, announcementBaseline + 1)
        XCTAssertEqual(announcer.posts.last?.message, "Copied.")
        guard case .medium = announcer.posts.last?.priority else {
            return XCTFail("successful copy must announce politely")
        }
    }

    func test10WikilinkRenderIsPureAndInvocationFormatsExactlyOnce() async throws {
        let pasteboard = RecordingPasteboard()
        let state = try openVault(
            named: "wikilink", files: ["A.md"], sidebarPasteboard: pasteboard)
        let currentSession = try XCTUnwrap(state.currentSession)
        let selection = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let requests = LockedValue<
            [(sessionIdentity: ObjectIdentifier, path: String)]
        >([])
        state.sidebarActionDispatchOverrides.wikilink = { session, path in
            requests.mutate { $0.append((ObjectIdentifier(session), path)) }
            return "[[Exact A]]"
        }

        for surface in [
            SidebarActionSurface.menuBar, .commandPalette, .contextMenu,
            .voiceOver, .toolbar, .keyboard,
        ] {
            _ = SidebarActionCatalog.project(surface: surface, snapshot: selection)
        }
        XCTAssertEqual(
            requests.value.count, 0,
            "render projection never invokes the formatter")
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.sidebarCopyWikilink, snapshot: selection)),
            .copyPending(actionID: SlateCommandID.sidebarCopyWikilink))
        let copyTask = try XCTUnwrap(
            state.pendingSidebarWikilinkCopyTaskForTesting)
        await copyTask.value

        let capturedRequests = requests.value
        XCTAssertEqual(capturedRequests.count, 1, "invocation formats exactly once")
        XCTAssertEqual(
            capturedRequests.first?.sessionIdentity,
            ObjectIdentifier(currentSession))
        XCTAssertEqual(
            capturedRequests.first?.sessionIdentity,
            selection.sessionIdentity)
        XCTAssertEqual(capturedRequests.first?.path, "A.md")
        XCTAssertEqual(pasteboard.clearCount, 1)
        XCTAssertEqual(pasteboard.values, ["[[Exact A]]"])
    }

    func test11WikilinkNilAndThrowSurfaceOneBackgroundFailureWithoutPreparedCopy()
        async throws
    {
        let announcer = RecordingAnnouncer()
        let pasteboard = RecordingPasteboard()
        let state = try openVault(
            named: "wikilink-failure", files: ["A.md"], announcer: announcer,
            sidebarPasteboard: pasteboard)
        let selection = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let sessionIdentity = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let wikilinkIntent = try intent(
            SlateCommandID.sidebarCopyWikilink, snapshot: selection)
        let requests = LockedValue<[(ObjectIdentifier, String)]>([])
        let announcementBaseline = announcer.posts.count
        state.sidebarActionDispatchOverrides.wikilink = { session, path in
            requests.mutate { $0.append((ObjectIdentifier(session), path)) }
            return nil
        }
        XCTAssertEqual(
            try state.dispatchSidebarAction(wikilinkIntent),
            .copyPending(actionID: SlateCommandID.sidebarCopyWikilink))
        let refusedTask = try XCTUnwrap(
            state.pendingSidebarWikilinkCopyTaskForTesting)
        await refusedTask.value
        XCTAssertEqual(requests.value.count, 1)
        XCTAssertEqual(requests.value.first?.0, sessionIdentity)
        XCTAssertEqual(requests.value.first?.1, "A.md")
        XCTAssertEqual(
            state.sidebarActionBackgroundFailure,
            AppState.sidebarWikilinkFailureReason)
        XCTAssertEqual(
            announcer.posts.dropFirst(announcementBaseline).map(\.message),
            [AppState.sidebarWikilinkFailureReason])

        requests.set([])
        state.sidebarActionDispatchOverrides.wikilink = { session, path in
            requests.mutate { $0.append((ObjectIdentifier(session), path)) }
            throw ProbeError.formatterFailed
        }
        XCTAssertEqual(
            try state.dispatchSidebarAction(wikilinkIntent),
            .copyPending(actionID: SlateCommandID.sidebarCopyWikilink))
        XCTAssertNil(
            state.sidebarActionBackgroundFailure,
            "an admitted retry clears the previous persistent failure")
        let throwingTask = try XCTUnwrap(
            state.pendingSidebarWikilinkCopyTaskForTesting)
        await throwingTask.value
        XCTAssertEqual(requests.value.count, 1)
        XCTAssertEqual(requests.value.first?.0, sessionIdentity)
        XCTAssertEqual(requests.value.first?.1, "A.md")
        XCTAssertEqual(
            state.sidebarActionBackgroundFailure,
            AppState.sidebarWikilinkFailureReason)
        XCTAssertEqual(
            announcer.posts.dropFirst(announcementBaseline).map(\.message),
            [
                AppState.sidebarWikilinkFailureReason,
                AppState.sidebarWikilinkFailureReason,
            ])
        XCTAssertEqual(pasteboard.clearCount, 0)
        XCTAssertEqual(pasteboard.values, [])
    }

    func test11bBusyStructuralWriterRejectsBeforeWikilinkFormatterEntry() throws {
        let state = try openVault(named: "wikilink-busy", files: ["A.md"])
        let selection = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let wikilinkIntent = try intent(
            SlateCommandID.sidebarCopyWikilink, snapshot: selection)
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(selection))
        let busy = "Another file operation is still running."
        let formatterCalls = LockedValue(0)
        state.sidebarActionStructuralDisabledReasonOverride = busy
        state.sidebarActionDispatchOverrides.wikilink = { _, _ in
            formatterCalls.mutate { $0 += 1 }
            return "[[A]]"
        }

        assertRejected(expectedMessage: busy) {
            try state.dispatchSidebarAction(wikilinkIntent)
        }
        XCTAssertEqual(
            formatterCalls.value, 0,
            "busy admission must prevent any synchronous native mutex wait")

        XCTAssertFalse(
            state.sidebarActionProjection(surface: .contextMenu)
                .contains { $0.id == SlateCommandID.sidebarCopyWikilink })
        XCTAssertFalse(
            state.sidebarActionProjection(surface: .voiceOver)
                .contains { $0.id == SlateCommandID.sidebarCopyWikilink })
        for surface in [SidebarActionSurface.menuBar, .commandPalette] {
            let evaluation = try XCTUnwrap(
                state.sidebarActionProjection(surface: surface)
                    .first { $0.id == SlateCommandID.sidebarCopyWikilink })
            XCTAssertEqual(evaluation.disabledReason, busy)
            XCTAssertNil(evaluation.intent)
        }
    }

    func test11cWikilinkFormattingRunsOffMainAndVaultSwitchCancelsStaleCopy()
        async throws
    {
        let announcer = RecordingAnnouncer()
        let pasteboard = RecordingPasteboard()
        let state = try openVault(
            named: "wikilink-off-main", files: ["A.md"], announcer: announcer,
            sidebarPasteboard: pasteboard)
        let selection = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let wikilinkIntent = try intent(
            SlateCommandID.sidebarCopyWikilink, snapshot: selection)
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(selection))
        let formatterEntered = expectation(description: "formatter entered")
        let mainActorProbed = expectation(description: "main actor stayed responsive")
        let releaseFormatter = DispatchSemaphore(value: 0)
        let formatterRanOnMainThread = LockedValue(false)
        state.sidebarActionDispatchOverrides.wikilink = { _, _ in
            formatterRanOnMainThread.set(Thread.isMainThread)
            formatterEntered.fulfill()
            _ = releaseFormatter.wait(timeout: .now() + 10)
            return "[[A]]"
        }
        let announcementBaseline = announcer.posts.count

        XCTAssertEqual(
            try state.dispatchSidebarAction(wikilinkIntent),
            .copyPending(actionID: SlateCommandID.sidebarCopyWikilink))
        XCTAssertTrue(state.isCopyingSidebarWikilink)
        XCTAssertEqual(
            state.sidebarActionBackgroundProgressReason,
            AppState.sidebarWikilinkCopyPendingReason)
        for surface in [SidebarActionSurface.menuBar, .commandPalette] {
            let evaluation = try XCTUnwrap(
                state.sidebarActionProjection(surface: surface)
                    .first { $0.id == SlateCommandID.sidebarCopyWikilink })
            XCTAssertEqual(
                evaluation.disabledReason,
                AppState.sidebarWikilinkCopyPendingReason)
            XCTAssertNil(evaluation.intent)
        }
        for surface in [SidebarActionSurface.contextMenu, .voiceOver] {
            XCTAssertFalse(
                state.sidebarActionProjection(surface: surface)
                    .contains { $0.id == SlateCommandID.sidebarCopyWikilink })
        }
        XCTAssertEqual(
            announcer.posts.count, announcementBaseline,
            "visible non-live progress must not add a second announcement")
        let copyTask = try XCTUnwrap(
            state.pendingSidebarWikilinkCopyTaskForTesting)
        await fulfillment(of: [formatterEntered], timeout: 10)
        Task { @MainActor in mainActorProbed.fulfill() }
        await fulfillment(of: [mainActorProbed], timeout: 2)

        let replacementVault = root.appendingPathComponent("wikilink-replacement")
        try FileManager.default.createDirectory(
            at: replacementVault, withIntermediateDirectories: true)
        state.openVault(at: replacementVault)
        XCTAssertFalse(
            state.isCopyingSidebarWikilink,
            "the replacement vault must not inherit the old pending state")
        XCTAssertNil(state.sidebarActionBackgroundProgressReason)
        for surface in [SidebarActionSurface.menuBar, .commandPalette] {
            let evaluation = try XCTUnwrap(
                state.sidebarActionProjection(surface: surface)
                    .first { $0.id == SlateCommandID.sidebarCopyWikilink })
            XCTAssertNotEqual(
                evaluation.disabledReason,
                AppState.sidebarWikilinkCopyPendingReason)
        }
        releaseFormatter.signal()
        await copyTask.value

        XCTAssertFalse(formatterRanOnMainThread.value)
        XCTAssertFalse(state.isCopyingSidebarWikilink)
        XCTAssertEqual(pasteboard.clearCount, 0)
        XCTAssertEqual(pasteboard.values, [])
        XCTAssertFalse(
            announcer.posts.dropFirst(announcementBaseline)
                .contains { $0.message == "Copied." },
            "a formatter owned by the previous vault cannot mutate or announce")
    }

    func test12TrashUsesExactSingleFileFolderOrOrderedBatchAndRejectsFalse() throws {
        let state = try openVault(
            named: "trash", files: ["A.md", "Folder/Child.md"], folders: ["Folder"])
        let file = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let folder = try snapshot(
            on: state, [item("Folder", directory: true)], focusedPath: "Folder",
            creationParent: "Folder")
        let batch = try snapshot(
            on: state, [item("Folder", directory: true), item("A.md")],
            focusedPath: "A.md")
        let vault = try XCTUnwrap(state.currentVaultURL)
        var singles: [AppState.TreeSelection] = []
        var batches: [([AppState.TreeSelection], String?)] = []
        state.sidebarActionDispatchOverrides.trashSingle = {
            singles.append($0)
            return true
        }
        state.sidebarActionDispatchOverrides.trashBatch = { items, focus in
            batches.append((items, focus))
            return true
        }
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.deleteEntry, snapshot: file)),
            .completed(actionID: SlateCommandID.deleteEntry))
        XCTAssertEqual(
            singles, [.init(path: "A.md", isDirectory: false)],
            "the file case remains observable through the narrow leaf seam")

        state.sidebarActionDispatchOverrides.trashSingle = nil
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.deleteEntry, snapshot: folder)),
            .completed(actionID: SlateCommandID.deleteEntry))
        let pendingFolder = try XCTUnwrap(state.pendingFolderDelete)
        XCTAssertEqual(pendingFolder.path, "Folder")
        XCTAssertEqual(pendingFolder.itemCount, 1)
        XCTAssertEqual(state.pendingFolderDelete?.id, pendingFolder.id)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Folder/Child.md").path),
            "default non-empty-folder admission only stages the owned confirmation")
        XCTAssertFalse(state.cancelPendingFolderDelete(id: UUID()))
        XCTAssertEqual(state.pendingFolderDelete?.id, pendingFolder.id)
        XCTAssertTrue(state.cancelPendingFolderDelete(id: pendingFolder.id))
        XCTAssertNil(state.pendingFolderDelete)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Folder/Child.md").path),
            "cancelling the exact staged UUID preserves the subtree")

        XCTAssertEqual(
            try state.dispatchSidebarAction(intent(SlateCommandID.deleteEntry, snapshot: batch)),
            .completed(actionID: SlateCommandID.deleteEntry))
        XCTAssertEqual(batches.first?.0.map(\.path), ["Folder", "A.md"])
        XCTAssertEqual(batches.first?.1, "A.md")

        state.sidebarActionDispatchOverrides.trashSingle = { _ in false }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.deleteEntry, snapshot: file))
        }
        state.sidebarActionDispatchOverrides.trashBatch = { _, _ in false }
        assertRejected {
            try state.dispatchSidebarAction(intent(SlateCommandID.deleteEntry, snapshot: batch))
        }
    }

    func test13AllItemPreflightRejectsLateInvalidOrStaleBeforeAnyEffect() throws {
        let victims = ["Missing.md", "Type.md", "LiveLink.md", "DeadLink.md", "Stale.md"]
        let state = try openVault(named: "preflight", files: ["A.md"] + victims)
        let vault = try XCTUnwrap(state.currentVaultURL)
        let originalA = try String(
            contentsOf: vault.appendingPathComponent("A.md"), encoding: .utf8)
        var effects = 0
        state.sidebarActionDispatchOverrides.openPreflight = { _ in effects += 1; return true }
        state.sidebarActionDispatchOverrides.openPath = { _, _ in effects += 1; return true }
        state.sidebarActionDispatchOverrides.moveBatch = { _, _ in effects += 1; return true }
        state.sidebarActionDispatchOverrides.trashBatch = { _, _ in effects += 1; return true }

        func capturedIntents(_ victim: String) throws -> [SidebarActionInvocationIntent] {
            let selection = try snapshot(
                on: state, [item("A.md"), item(victim)], focusedPath: victim)
            return try [
                intent(SlateCommandID.sidebarOpen, snapshot: selection),
                intent(SlateCommandID.moveTo, snapshot: selection),
                intent(SlateCommandID.deleteEntry, snapshot: selection),
            ]
        }
        func reject(_ intents: [SidebarActionInvocationIntent], _ label: String) {
            for captured in intents {
                assertRejected(context: "\(label): \(captured.actionID)") {
                    try state.dispatchSidebarAction(captured)
                }
            }
        }

        let missing = try capturedIntents("Missing.md")
        try FileManager.default.removeItem(at: vault.appendingPathComponent("Missing.md"))
        reject(missing, "later missing")

        let typeChanged = try capturedIntents("Type.md")
        let typeURL = vault.appendingPathComponent("Type.md")
        try FileManager.default.removeItem(at: typeURL)
        try FileManager.default.createDirectory(at: typeURL, withIntermediateDirectories: false)
        reject(typeChanged, "later type change")

        let liveLink = try capturedIntents("LiveLink.md")
        let liveURL = vault.appendingPathComponent("LiveLink.md")
        try FileManager.default.removeItem(at: liveURL)
        try FileManager.default.createSymbolicLink(
            at: liveURL, withDestinationURL: vault.appendingPathComponent("A.md"))
        reject(liveLink, "live symlink")

        let deadLink = try capturedIntents("DeadLink.md")
        let deadURL = vault.appendingPathComponent("DeadLink.md")
        let deadTarget = vault.appendingPathComponent("DeadTarget.md")
        try "# target".write(to: deadTarget, atomically: true, encoding: .utf8)
        try FileManager.default.removeItem(at: deadURL)
        try FileManager.default.createSymbolicLink(at: deadURL, withDestinationURL: deadTarget)
        try FileManager.default.removeItem(at: deadTarget)
        reject(deadLink, "dangling symlink")

        let stale = try capturedIntents("Stale.md")
        let replacementVault = root.appendingPathComponent("replacement-vault")
        try FileManager.default.createDirectory(
            at: replacementVault, withIntermediateDirectories: true)
        state.openVault(at: replacementVault)
        reject(stale, "stale session")
        XCTAssertEqual(effects, 0, "validation completes for every item before any funnel effect")
        XCTAssertNil(state.pendingMove)
        XCTAssertNil(state.pendingBatchMove)
        XCTAssertNil(state.pendingFolderDelete)
        XCTAssertNil(state.pendingBatchDelete)
        XCTAssertEqual(
            try String(contentsOf: vault.appendingPathComponent("A.md"), encoding: .utf8),
            originalA,
            "a rejected late item leaves earlier valid captures untouched")
    }

    func test13aTenThousandItemMovePreflightReturnsWithinInteractionBudgetAndRunsOffMain()
        async throws
    {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "responsive-batch",
            files: ["Seed.md"],
            announcer: announcer)
        let paths = try createValidationFiles(count: 10_000, in: state)
        let selection = try snapshot(
            on: state,
            paths.map { item($0) },
            focusedPath: paths.last,
            creationParent: "A/B")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(selection))
        let openIntent = try intent(SlateCommandID.sidebarOpen, snapshot: selection)
        guard case .openConfirmation(let openRequest) =
            try state.dispatchSidebarAction(openIntent)
        else {
            return XCTFail("the competing large Open must be staged first")
        }
        let moveIntent = try intent(SlateCommandID.moveTo, snapshot: selection)
        var effects = 0
        state.sidebarActionDispatchOverrides.moveBatch = { items, focusedPath in
            effects += 1
            XCTAssertEqual(items.count, 10_000)
            XCTAssertEqual(focusedPath, paths.last)
            return true
        }

        let clock = ContinuousClock()
        let started = clock.now
        XCTAssertEqual(
            try state.dispatchSidebarAction(moveIntent),
            .validationPending(actionID: SlateCommandID.moveTo))
        let dispatchDuration = started.duration(to: clock.now)

        XCTAssertLessThan(
            dispatchDuration,
            .milliseconds(100),
            "a 10k selection must hand filesystem work off before the interaction budget")
        XCTAssertEqual(effects, 0, "the full walk finishes before any action funnel effect")
        XCTAssertTrue(state.isValidatingSidebarAction)
        XCTAssertEqual(
            state.structuralMutationDisabledReason,
            AppState.sidebarActionValidationPendingReason)
        XCTAssertEqual(
            state.sidebarActionProjection(surface: .menuBar)
                .first { $0.id == SlateCommandID.sidebarOpen }?.disabledReason,
            AppState.sidebarActionValidationPendingReason)
        for surface in [SidebarActionSurface.contextMenu, .voiceOver] {
            let ids = Set(state.sidebarActionProjection(surface: surface).map(\.id))
            XCTAssertFalse(ids.contains(SlateCommandID.sidebarOpen))
            XCTAssertFalse(ids.contains(SlateCommandID.moveTo))
        }

        let validationTask = try XCTUnwrap(
            state.pendingSidebarActionValidationTaskForTesting)
        XCTAssertEqual(
            state.confirmOpenSelection(id: openRequest.id),
            .rejected(AppState.sidebarActionValidationPendingReason),
            "a second validator must reject without superseding the accepted Move")
        XCTAssertTrue(state.isValidatingSidebarAction)
        XCTAssertEqual(effects, 0)
        await validationTask.value

        XCTAssertEqual(effects, 1)
        XCTAssertFalse(state.isValidatingSidebarAction)
        XCTAssertEqual(state.sidebarActionValidationRanOnMainThreadForTesting, false)
        XCTAssertNil(state.sidebarActionBackgroundFailure)
        XCTAssertEqual(
            announcer.posts.filter { $0.message.hasPrefix("Checking 10,000") }.count,
            1,
            "one accepted validator owns one polite progress announcement")
    }

    func test13aaDeepSelectionBelowItemThresholdStillOffloadsByComponentWork()
        async throws
    {
        let state = try openVault(named: "responsive-deep", files: ["Seed.md"])
        let parent = (0..<100).map { "D\($0)" }.joined(separator: "/")
        let paths = try createValidationFiles(count: 255, in: state, parent: parent)
        let selection = try snapshot(
            on: state,
            paths.map { item($0) },
            focusedPath: paths.last,
            creationParent: parent)
        var effects = 0
        state.sidebarActionDispatchOverrides.moveBatch = { _, _ in
            effects += 1
            return true
        }

        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.moveTo, snapshot: selection)),
            .validationPending(actionID: SlateCommandID.moveTo))
        XCTAssertEqual(effects, 0)
        let validationTask = try XCTUnwrap(
            state.pendingSidebarActionValidationTaskForTesting)
        await validationTask.value
        XCTAssertEqual(effects, 1)
        XCTAssertEqual(state.sidebarActionValidationRanOnMainThreadForTesting, false)
    }

    func test13abVisibleValidationFailurePersistsUntilAnAdmittedRetryBegins()
        async throws
    {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "responsive-retry",
            files: ["Seed.md"],
            announcer: announcer)
        let parent = (0..<100).map { "D\($0)" }.joined(separator: "/")
        let paths = try createValidationFiles(count: 10, in: state, parent: parent)
        let selection = try snapshot(
            on: state,
            paths.map { item($0) },
            focusedPath: paths.last,
            creationParent: parent)
        try FileManager.default.removeItem(
            at: try XCTUnwrap(state.currentVaultURL)
                .appendingPathComponent(paths.last!))
        var effects = 0
        state.sidebarActionDispatchOverrides.moveBatch = { _, _ in
            effects += 1
            return true
        }

        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.moveTo, snapshot: selection)),
            .validationPending(actionID: SlateCommandID.moveTo))
        let failedTask = try XCTUnwrap(
            state.pendingSidebarActionValidationTaskForTesting)
        await failedTask.value

        XCTAssertEqual(effects, 0)
        XCTAssertEqual(
            state.sidebarActionBackgroundFailure,
            AppState.sidebarSelectionChangedReason)
        XCTAssertEqual(announcer.posts.last?.message, AppState.sidebarSelectionChangedReason)

        let valid = try snapshot(on: state, [item("Seed.md")], focusedPath: "Seed.md")
        state.sidebarActionDispatchOverrides.moveSingle = { _ in true }
        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.moveTo, snapshot: valid)),
            .completed(actionID: SlateCommandID.moveTo))
        XCTAssertNil(
            state.sidebarActionBackgroundFailure,
            "an admitted retry clears the persistent warning before its new sheet/funnel")
    }

    func test13acVaultSwitchCancelsDetachedFilesystemValidationWork()
        async throws
    {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "validation-cancellation",
            files: ["Seed.md"],
            announcer: announcer)
        let parent = (0..<100).map { "D\($0)" }.joined(separator: "/")
        let paths = try createValidationFiles(count: 10, in: state, parent: parent)
        let selection = try snapshot(
            on: state,
            paths.map { item($0) },
            focusedPath: paths.last,
            creationParent: parent)
        let probeStarted = expectation(description: "detached validator started")
        let observedCancellation = LockedValue(false)
        let probeCalls = LockedValue(0)
        state.sidebarActionValidationItemProbeForTesting = { index in
            probeCalls.mutate { $0 += 1 }
            guard index == 0 else { return }
            probeStarted.fulfill()
            let deadline = Date().addingTimeInterval(2)
            while !Task.isCancelled, Date() < deadline {
                Thread.sleep(forTimeInterval: 0.001)
            }
            observedCancellation.set(Task.isCancelled)
        }
        var effects = 0
        state.sidebarActionDispatchOverrides.moveBatch = { _, _ in
            effects += 1
            return true
        }

        XCTAssertEqual(
            try state.dispatchSidebarAction(
                intent(SlateCommandID.moveTo, snapshot: selection)),
            .validationPending(actionID: SlateCommandID.moveTo))
        let validationTask = try XCTUnwrap(
            state.pendingSidebarActionValidationTaskForTesting)
        await fulfillment(of: [probeStarted], timeout: 10)

        let replacementVault = root.appendingPathComponent("validation-replacement")
        try FileManager.default.createDirectory(
            at: replacementVault,
            withIntermediateDirectories: true)
        state.openVault(at: replacementVault)
        await validationTask.value

        XCTAssertTrue(
            observedCancellation.value,
            "vault replacement must cancel the detached filesystem walk itself")
        XCTAssertEqual(
            probeCalls.value, 1,
            "cancellation must stop before the validator advances to another item")
        XCTAssertEqual(effects, 0)
        XCTAssertFalse(state.isValidatingSidebarAction)
        XCTAssertNil(state.pendingSidebarActionValidationTaskForTesting)
        XCTAssertNil(state.sidebarActionBackgroundFailure)
        XCTAssertFalse(
            announcer.posts.contains {
                $0.message == AppState.sidebarSelectionChangedReason
            },
            "lifecycle cancellation stays silent")
    }

    func test13bLargeOpenStagesImmediatelyThenValidatesOnceOffMainBeforeOpening()
        async throws
    {
        let state = try openVault(named: "responsive-open", files: ["Seed.md"])
        let paths = try createValidationFiles(count: 512, in: state)
        let selection = try snapshot(
            on: state,
            paths.map { item($0) },
            focusedPath: paths[17],
            creationParent: "A/B")
        let openIntent = try intent(SlateCommandID.sidebarOpen, snapshot: selection)
        var preflights = 0
        var opened: [String] = []
        state.sidebarActionDispatchOverrides.openPreflight = { _ in
            preflights += 1
            return true
        }
        state.sidebarActionDispatchOverrides.openPath = { path, target in
            XCTAssertEqual(target, .newTab)
            opened.append(path)
            return true
        }

        let clock = ContinuousClock()
        let stagingStarted = clock.now
        guard case .openConfirmation(let request) =
            try state.dispatchSidebarAction(openIntent)
        else {
            return XCTFail("10+ Open must stage confirmation without a filesystem walk")
        }
        XCTAssertLessThan(
            stagingStarted.duration(to: clock.now),
            .milliseconds(100))
        XCTAssertEqual(preflights, 1)
        XCTAssertEqual(opened, [])
        XCTAssertNil(state.pendingSidebarActionValidationTaskForTesting)

        let confirmationStarted = clock.now
        XCTAssertEqual(
            state.confirmOpenSelection(id: request.id),
            .validationPending)
        XCTAssertLessThan(
            confirmationStarted.duration(to: clock.now),
            .milliseconds(100))
        XCTAssertEqual(opened, [], "confirmation must not report or perform an early open")
        XCTAssertNil(
            state.activeBatchAlertPresentation,
            "confirmed work owns a background continuation, not a stale alert")

        let validationTask = try XCTUnwrap(
            state.pendingSidebarActionValidationTaskForTesting)
        await validationTask.value

        let expected = paths.filter { $0 != paths[17] } + [paths[17]]
        XCTAssertEqual(opened, expected)
        XCTAssertEqual(preflights, 3, "staging, confirmation, and post-await admission each run once")
        XCTAssertEqual(state.sidebarActionValidationRanOnMainThreadForTesting, false)
        XCTAssertFalse(state.isValidatingSidebarAction)
    }

    func test14StructuralAndActionSpecificReasonsRejectBeforeStaging() throws {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "reasons", files: ["A.md"], announcer: announcer)
        let selection = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let renameIntent = try intent(SlateCommandID.renameEntry, snapshot: selection)
        var funnelCalls = 0
        state.sidebarActionDispatchOverrides.rename = { _ in funnelCalls += 1; return true }

        let busy = "Wait for the current file operation to finish."
        state.sidebarActionStructuralDisabledReasonOverride = busy
        let announcementBaseline = announcer.posts.count
        assertRejected(expectedMessage: busy) {
            try state.dispatchSidebarAction(renameIntent)
        }
        XCTAssertEqual(funnelCalls, 0)
        XCTAssertNil(state.renamingNode)

        state.sidebarActionStructuralDisabledReasonOverride = nil
        let loading = "Sidebar actions are still loading."
        state.sidebarActionAvailabilityReasonProvider = {
            $0 == SlateCommandID.renameEntry ? loading : nil
        }
        assertRejected(expectedMessage: loading) {
            try state.dispatchSidebarAction(renameIntent)
        }
        XCTAssertEqual(funnelCalls, 0)
        XCTAssertNil(state.renamingNode)
        XCTAssertEqual(
            announcer.posts.count, announcementBaseline,
            "synchronous disabled-state rejection does not self-announce")
    }

    func test15EveryFallibleFunnelRejectionNeverReturnsCompleted() throws {
        let announcer = RecordingAnnouncer()
        let state = try openVault(
            named: "fallible", files: ["A.md", "B.md"], folders: ["Folder"],
            announcer: announcer)
        let file = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let batch = try snapshot(
            on: state, [item("A.md"), item("B.md")], focusedPath: "B.md")
        let empty = try snapshot(on: state, [])
        let cases: [(String, String, SidebarSelectionSnapshot, () -> Void)] = [
            ("open", SlateCommandID.sidebarOpen, file, {
                state.sidebarActionDispatchOverrides.openPreflight = { _ in false }
            }),
            ("new note", SlateCommandID.newNote, empty, {
                state.sidebarActionDispatchOverrides.createNote = { _ in false }
            }),
            ("new folder", SlateCommandID.newFolder, empty, {
                state.sidebarActionDispatchOverrides.createFolder = { _ in false }
            }),
            ("template", SlateCommandID.newFromTemplate, empty, {
                state.sidebarActionDispatchOverrides.openTemplatePicker = { _ in false }
            }),
            ("rename", SlateCommandID.renameEntry, file, {
                state.sidebarActionDispatchOverrides.rename = { _ in false }
            }),
            ("single move", SlateCommandID.moveTo, file, {
                state.sidebarActionDispatchOverrides.moveSingle = { _ in false }
            }),
            ("batch move", SlateCommandID.moveTo, batch, {
                state.sidebarActionDispatchOverrides.moveBatch = { _, _ in false }
            }),
            ("duplicate", SlateCommandID.duplicateEntry, file, {
                state.sidebarActionDispatchOverrides.duplicate = { _ in false }
            }),
            ("reveal", SlateCommandID.revealInFinder, file, {
                state.sidebarActionDispatchOverrides.reveal = { _, _ in false }
            }),
            ("single trash", SlateCommandID.deleteEntry, file, {
                state.sidebarActionDispatchOverrides.trashSingle = { _ in false }
            }),
            ("batch trash", SlateCommandID.deleteEntry, batch, {
                state.sidebarActionDispatchOverrides.trashBatch = { _, _ in false }
            }),
        ]
        let announcementBaseline = announcer.posts.count
        for (name, id, selection, configure) in cases {
            state.sidebarActionDispatchOverrides = .init()
            configure()
            assertRejected(context: name) {
                try state.dispatchSidebarAction(intent(id, snapshot: selection))
            }
        }
        XCTAssertEqual(
            announcer.posts.count, announcementBaseline,
            "leaf admission failures stay synchronous and silent")
    }

    func test16IDOverloadCapturesSnapshotOnceAcrossAvailabilityReentrancy() throws {
        let state = try openVault(named: "id-freeze", files: ["A.md", "B.md"])
        let a = try snapshot(on: state, [item("A.md")], focusedPath: "A.md")
        let b = try snapshot(on: state, [item("B.md")], focusedPath: "B.md")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(a))
        var captured: [AppState.TreeSelection] = []
        state.sidebarActionDispatchOverrides.rename = { captured.append($0); return true }
        state.sidebarActionAvailabilityReasonProvider = { id in
            if id == SlateCommandID.renameEntry {
                _ = state.publishSidebarSelectionSnapshot(b)
            }
            return nil
        }

        XCTAssertEqual(
            try state.dispatchSidebarAction(id: SlateCommandID.renameEntry),
            .completed(actionID: SlateCommandID.renameEntry))
        XCTAssertEqual(captured, [.init(path: "A.md", isDirectory: false)])
        XCTAssertEqual(state.sidebarSelectionSnapshot, b)

        state.sidebarActionAvailabilityReasonProvider = { _ in nil }
        XCTAssertEqual(
            try state.dispatchSidebarAction(id: SlateCommandID.renameEntry),
            .completed(actionID: SlateCommandID.renameEntry))
        XCTAssertEqual(
            captured,
            [
                .init(path: "A.md", isDirectory: false),
                .init(path: "B.md", isDirectory: false),
            ],
            "after the reentrant callback is reset, a second invocation captures B")
    }

    func test17CatalogIsNoIOOpenTypesAreSharedAndDefaultsUseRealFunnels() throws {
        let catalog = try appSource("Sidebar/SidebarActionCatalog.swift")
        for forbidden in [
            "FileManager", "VaultSession", "wikilinkForPath", "NSWorkspace",
            "NSPasteboard", "Data(contentsOf", "String(contentsOf", "resourceValues",
            "getResourceValue", "checkResourceIsReachable", "fileExists",
            "attributesOfItem", "contentsOfDirectory",
        ] {
            XCTAssertFalse(catalog.contains(forbidden), "render-time I/O token: \(forbidden)")
        }

        let tree = try appSource("FileTreeSidebar.swift")
        let appState = try appSource("AppState.swift")
        XCTAssertFalse(tree.contains("struct OpenSelectionBatch"))
        XCTAssertFalse(tree.contains("struct OpenSelectionRequest"))
        XCTAssertFalse(tree.contains("enum OpenSelectionDisposition"))
        XCTAssertTrue(tree.contains("typealias OpenSelectionBatch = SidebarOpenSelectionBatch"))
        XCTAssertTrue(tree.contains("typealias OpenSelectionRequest = SidebarOpenSelectionRequest"))
        XCTAssertTrue(tree.contains("typealias OpenSelectionDisposition = SidebarOpenSelectionDisposition"))
        XCTAssertTrue(tree.contains("_ = appState.confirmOpenSelection(id: request.id)"))
        XCTAssertFalse(
            tree.contains("openCapturedPaths(appState.confirmOpenSelection"),
            "the alert must not receive raw paths or run a second Open executor")
        XCTAssertFalse(appState.contains("FileTreeSidebar.OpenSelection"))

        let dispatch = try functionBody(
            named: "dispatchSidebarAction",
            signatureFragment: "SidebarActionInvocationIntent",
            in: appState)
        for forbidden in [
            "postAccessibilityAnnouncement", "announcer.post",
            "activeBatchAlertPresentation =", "deferredBatchAlertPresentation =",
        ] {
            XCTAssertFalse(
                dispatch.contains(forbidden),
                "dispatcher bypasses shared presentation/announcement ownership: \(forbidden)")
        }
        for realFunnel in [
            "openFile", "enqueueOpenSelection", "activateFileViewerSelecting",
            "startSidebarWikilinkCopy",
            "requestCreateNote", "requestCreateFolder", "openTemplatePicker",
            "requestRename", "requestPendingMove", "requestBatchMove",
            "requestDuplicateEntry", "requestDeleteEntry", "requestBatchDelete",
        ] {
            XCTAssertTrue(
                dispatch.contains(realFunnel),
                "default dispatcher path must call the real \(realFunnel) funnel")
        }

        let wikilinkCopy = try functionBody(
            named: "startSidebarWikilinkCopy",
            signatureFragment: "SidebarActionInvocationIntent",
            in: appState)
        for required in [
            "Task.detached", "wikilinkForPath", "validateSidebarActionIntent",
            "completeSidebarCopy",
        ] {
            XCTAssertTrue(
                wikilinkCopy.contains(required),
                "async Wikilink copy must retain \(required)")
        }

        XCTAssertEqual(
            appState.components(separatedBy: "func dispatchSidebarAction(").count - 1,
            2,
            "only the ID capture overload and one frozen-intent executor are allowed")
        let overrides = try typeBody(named: "SidebarActionDispatchOverrides", in: appState)
        for leafSeam in [
            "openPreflight", "openPath", "createNote", "createFolder",
            "openTemplatePicker", "rename", "moveSingle", "moveBatch",
            "duplicate", "reveal", "wikilink", "trashSingle", "trashBatch",
        ] {
            let declaration = try storedPropertyDeclaration(named: leafSeam, in: overrides)
            XCTAssertTrue(
                declaration.contains("?") || declaration.contains("Optional<"),
                "\(leafSeam) must remain an optional leaf seam over the real default")
        }
    }

    private func appSource(_ name: String) throws -> String {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(
            contentsOf: packageRoot.appendingPathComponent("Sources/SlateMac/\(name)"),
            encoding: .utf8)
    }

    private enum SourceExtractionError: Error {
        case missing(String)
        case unbalanced(String)
    }

    private func functionBody(
        named name: String,
        signatureFragment: String,
        in source: String
    ) throws -> Substring {
        let functionPrefix = "func \(name)("
        var searchStart = source.startIndex
        while let start = source.range(
            of: functionPrefix,
            range: searchStart..<source.endIndex)
        {
            let tail = source[start.lowerBound...]
            guard let openBrace = tail.firstIndex(of: "{") else {
                throw SourceExtractionError.unbalanced(name)
            }
            if tail[..<openBrace].contains(signatureFragment) {
                return try bracedBody(
                    startingAt: openBrace,
                    in: tail,
                    description: "\(name)(\(signatureFragment))")
            }
            searchStart = start.upperBound
        }
        throw SourceExtractionError.missing("\(name)(\(signatureFragment))")
    }

    private func typeBody(named name: String, in source: String) throws -> Substring {
        guard let start = source.range(of: "struct \(name)") else {
            throw SourceExtractionError.missing("struct \(name)")
        }
        let tail = source[start.lowerBound...]
        guard let openBrace = tail.firstIndex(of: "{") else {
            throw SourceExtractionError.unbalanced("struct \(name)")
        }
        return try bracedBody(
            startingAt: openBrace, in: tail, description: "struct \(name)")
    }

    private func bracedBody(
        startingAt openBrace: String.Index,
        in source: Substring,
        description: String
    ) throws -> Substring {
        var depth = 0
        for index in source.indices[openBrace...] {
            switch source[index] {
            case "{": depth += 1
            case "}":
                depth -= 1
                if depth == 0 { return source[...index] }
            default: break
            }
        }
        throw SourceExtractionError.unbalanced(description)
    }

    private func storedPropertyDeclaration(
        named name: String,
        in typeBody: Substring
    ) throws -> String {
        let lines = typeBody.split(separator: "\n", omittingEmptySubsequences: false)
        guard let start = lines.firstIndex(where: { $0.contains("var \(name):") }) else {
            throw SourceExtractionError.missing("stored property \(name)")
        }
        var declaration = ""
        for line in lines[start...] {
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            if !declaration.isEmpty,
                trimmed.hasPrefix("var ") || trimmed.hasPrefix("let ")
                    || trimmed.hasPrefix("func ") || trimmed.hasPrefix("init(")
            {
                break
            }
            declaration.append(contentsOf: line)
            declaration.append("\n")
        }
        return declaration
    }
}
