// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation
import SwiftUI
import XCTest

@testable import SlateMac

/// Cross-surface contract for FL04-A's shared Sidebar action catalog.
///
/// Pure catalog assertions pin the exact projection matrix. Source ownership
/// assertions cover SwiftUI surfaces that cannot be mounted reliably in a
/// headless XCTest process, while live registry/model assertions exercise the
/// real command-palette bridge.
@MainActor
final class SidebarActionSurfaceIntegrationTests: XCTestCase {
    private struct ExpectedActionMetadata {
        let id: String
        let label: String
        let hint: String
        let section: CommandSection
        let hotkey: String?
    }

    private static let expectedActions: [ExpectedActionMetadata] = [
        .init(
            id: "slate.sidebar.open", label: "Open",
            hint: "Open the selected files.", section: .sidebar, hotkey: nil),
        .init(
            id: "slate.file.newNote", label: "New Note",
            hint: "Create an untitled note in the selected location, then rename it.",
            section: .sidebar, hotkey: "⌘N"),
        .init(
            id: "slate.file.newFolder", label: "New Folder",
            hint: "Create a new folder in the selected location, then rename it.",
            section: .sidebar, hotkey: nil),
        .init(
            id: "slate.file.newFromTemplate", label: "New Note from Template…",
            hint: "Choose a template for a new note.",
            section: .sidebar, hotkey: "⇧⌘N"),
        .init(
            id: "slate.file.importFilesAndFolders",
            label: "Import Files and Folders…",
            hint: "Choose files and folders. External items are copied into the selected location; items already in this vault are moved.",
            section: .sidebar, hotkey: nil),
        .init(
            id: "slate.file.rename", label: "Rename…",
            hint: "Rename the selected file or folder in place.",
            section: .sidebar, hotkey: "⌥⌘R"),
        .init(
            id: "slate.file.moveTo", label: "Move To…",
            hint: "Move the selected files or folders to another folder.",
            section: .sidebar, hotkey: "⇧⌘M"),
        .init(
            id: "slate.file.duplicate", label: "Duplicate",
            hint: "Duplicate the selected file as a copy next to it.",
            section: .sidebar, hotkey: nil),
        .init(
            id: "slate.file.revealInFinder", label: "Reveal in Finder",
            hint: "Show the selected file or folder in Finder.",
            section: .sidebar, hotkey: nil),
        .init(
            id: "slate.file.copyPath", label: "Copy Path",
            hint: "Copy the selected item's path.",
            section: .sidebar, hotkey: nil),
        .init(
            id: "slate.sidebar.copyWikilink", label: "Copy Wikilink",
            hint: "Copy a wikilink to the selected Markdown file.",
            section: .sidebar, hotkey: nil),
        .init(
            id: "slate.file.delete", label: "Move to Trash",
            hint: "Move the selected files or folders to the Trash.",
            section: .sidebar, hotkey: nil),
    ]

    private let session = NSObject()
    private var root: URL!

    private var catalogIDs: [String] { Self.expectedActions.map(\.id) }

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-sidebar-surface-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func openVault(
        named name: String,
        files: [String] = [],
        folders: [String] = []
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
            externalOpener: { _ in true })
        state.openVault(at: vault)
        let currentSession = try XCTUnwrap(state.currentSession)
        _ = try currentSession.scanInitial(cancel: CancelToken())
        return state
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

    private func snapshot(
        _ items: [SidebarSelectionItem],
        focusedPath: String? = nil,
        creationParent: String = ""
    ) -> SidebarSelectionSnapshot {
        SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(session),
            items: items,
            focusedPath: focusedPath,
            creationParent: creationParent)
    }

    func testRegistryAndPaletteExposeExactCatalogMetadataAndOrder() {
        let state = AppState()
        let registered = state.commandRegistry.list()
        let catalogIDSet = Set(catalogIDs)
        let sidebarCommands = registered.filter { catalogIDSet.contains($0.id) }

        XCTAssertEqual(SidebarActionCatalog.actions.count, Self.expectedActions.count)
        for (definition, expected) in zip(
            SidebarActionCatalog.actions, Self.expectedActions)
        {
            XCTAssertEqual(definition.id, expected.id)
            XCTAssertEqual(definition.label, expected.label)
            XCTAssertEqual(definition.accessibilityHint, expected.hint)
            XCTAssertEqual(definition.section, expected.section)
        }

        XCTAssertEqual(
            sidebarCommands.count,
            Self.expectedActions.count,
            "the registry must expose every catalog action even without a vault")

        let grouped = Dictionary(grouping: sidebarCommands, by: \.id)
        for expected in Self.expectedActions {
            let matches = grouped[expected.id] ?? []
            XCTAssertEqual(
                matches.count, 1,
                "\(expected.id) must be registered exactly once")
            guard let command = matches.first else { continue }
            XCTAssertEqual(command.label, expected.label, "catalog label drift for \(expected.id)")
            XCTAssertEqual(
                command.accessibilityHint,
                expected.hint,
                "catalog hint drift for \(expected.id)")
            XCTAssertEqual(
                command.section,
                expected.section,
                "every catalog command belongs to the Sidebar palette section")
            XCTAssertEqual(
                command.hotkeyHint,
                expected.hotkey,
                "shortcut drift for \(expected.id)")
        }

        let model = CommandPaletteModel()
        model.loadCommands(registered)
        let sidebarSection = model.sections.first { $0.kind == .sidebar }
        XCTAssertEqual(
            sidebarSection?.commands.map(\.id),
            catalogIDs + [SlateCommandID.cancelImport],
            "the Sidebar palette section must preserve catalog order before global lifecycle commands")
    }

    func testMenuAndPaletteKeepFullInventoryWithIdenticalCurrentReasons() {
        let noVaultMenu = SidebarActionCatalog.project(surface: .menuBar, snapshot: nil)
        let noVaultPalette = SidebarActionCatalog.project(
            surface: .commandPalette, snapshot: nil)
        XCTAssertEqual(noVaultMenu, noVaultPalette)
        XCTAssertEqual(noVaultMenu.map(\.id), catalogIDs)
        XCTAssertTrue(noVaultMenu.allSatisfy {
            $0.disabledReason == SidebarActionCatalog.noVaultReason && $0.intent == nil
        })

        let mixed = snapshot(
            [item("Note.md"), item("Folder", directory: true)],
            focusedPath: "Folder",
            creationParent: "Folder")
        let menu = SidebarActionCatalog.project(surface: .menuBar, snapshot: mixed)
        let palette = SidebarActionCatalog.project(surface: .commandPalette, snapshot: mixed)
        XCTAssertEqual(menu, palette, "menu and palette must expose the same current reasons")
        XCTAssertEqual(menu.map(\.id), catalogIDs)
        XCTAssertEqual(
            menu.filter { $0.disabledReason == nil }.map(\.id),
            [SlateCommandID.moveTo, SlateCommandID.deleteEntry],
            "multi-selection rejects every single-destination creation action")

        let expectedDisabledReasons: [String: String] = [
            SlateCommandID.sidebarOpen: "Open is available only for files.",
            SlateCommandID.newNote:
                "Select no more than one item to choose a creation location.",
            SlateCommandID.newFolder:
                "Select no more than one item to choose a creation location.",
            SlateCommandID.newFromTemplate:
                "Select no more than one item to choose a creation location.",
            SlateCommandID.importFilesAndFolders:
                "Select no more than one item to choose an import location.",
            SlateCommandID.renameEntry: "Select exactly one file or folder to rename.",
            SlateCommandID.duplicateEntry: "Select exactly one file to duplicate.",
            SlateCommandID.revealInFinder: "Select exactly one file or folder to reveal.",
            SlateCommandID.copyPath: "Select exactly one file or folder to copy its path.",
            SlateCommandID.sidebarCopyWikilink:
                "Select exactly one Markdown file to copy its wikilink.",
        ]
        XCTAssertEqual(
            Dictionary(uniqueKeysWithValues: menu.compactMap { evaluation in
                evaluation.disabledReason.map { (evaluation.id, $0) }
            }),
            expectedDisabledReasons,
            "the other nine mixed-selection actions expose deterministic capability reasons")
    }

    func testContextAndVoiceOverOmitEveryUnavailableAction() {
        let markdown = snapshot([item("Note.md")], focusedPath: "Note.md")
        let nonMarkdown = snapshot(
            [item("Diagram.canvas", markdown: false)], focusedPath: "Diagram.canvas")
        let mixed = snapshot(
            [item("Note.md"), item("Folder", directory: true)],
            focusedPath: "Folder",
            creationParent: "Folder")
        let busy = "Another file operation is still running."
        let loading = "Wikilinks are still loading."
        let propertyEdit = AppState.propertyEditInProgressReason

        for surface in [SidebarActionSurface.contextMenu, .voiceOver] {
            let contextualOpen = surface == .contextMenu
                ? [SlateCommandID.sidebarOpen] : []
            let idle = SidebarActionCatalog.project(surface: surface, snapshot: markdown)
            let nonMarkdownIdle = SidebarActionCatalog.project(
                surface: surface, snapshot: nonMarkdown)
            let mixedIdle = SidebarActionCatalog.project(surface: surface, snapshot: mixed)
            let structurallyBusy = SidebarActionCatalog.project(
                surface: surface,
                snapshot: markdown,
                structuralMutationDisabledReason: busy)
            let actionLoading = SidebarActionCatalog.project(
                surface: surface,
                snapshot: markdown,
                actionDisabledReasons: [SlateCommandID.sidebarCopyWikilink: loading])
            let propertyEditing = SidebarActionCatalog.project(
                surface: surface,
                snapshot: markdown,
                actionDisabledReasons: [SlateCommandID.sidebarOpen: propertyEdit])

            XCTAssertEqual(
                idle.map(\.id),
                contextualOpen + [
                    SlateCommandID.renameEntry, SlateCommandID.moveTo,
                    SlateCommandID.duplicateEntry, SlateCommandID.revealInFinder,
                    SlateCommandID.copyPath, SlateCommandID.sidebarCopyWikilink,
                    SlateCommandID.deleteEntry,
                ],
                "context remains concise and VoiceOver omits default-owned Open")
            XCTAssertEqual(
                nonMarkdownIdle.map(\.id),
                contextualOpen + [
                    SlateCommandID.renameEntry, SlateCommandID.moveTo,
                    SlateCommandID.duplicateEntry, SlateCommandID.revealInFinder,
                    SlateCommandID.copyPath, SlateCommandID.deleteEntry,
                ],
                "non-Markdown files omit Copy Wikilink")
            XCTAssertEqual(
                mixedIdle.map(\.id),
                [SlateCommandID.moveTo, SlateCommandID.deleteEntry],
                "mixed selections expose only Move To and Move to Trash")
            XCTAssertEqual(
                structurallyBusy.map(\.id),
                contextualOpen + [
                    SlateCommandID.revealInFinder,
                    SlateCommandID.copyPath,
                ],
                "busy contextual surfaces omit every structurally blocked action")
            XCTAssertEqual(
                actionLoading.map(\.id),
                contextualOpen + [
                    SlateCommandID.renameEntry, SlateCommandID.moveTo,
                    SlateCommandID.duplicateEntry, SlateCommandID.revealInFinder,
                    SlateCommandID.copyPath, SlateCommandID.deleteEntry,
                ],
                "action-specific unavailable rows are omitted, not rendered disabled")
            XCTAssertEqual(
                propertyEditing.map(\.id),
                [
                    SlateCommandID.renameEntry, SlateCommandID.moveTo,
                    SlateCommandID.duplicateEntry, SlateCommandID.revealInFinder,
                    SlateCommandID.copyPath, SlateCommandID.sidebarCopyWikilink,
                    SlateCommandID.deleteEntry,
                ],
                "property-edit navigation state omits contextual Open")

            for projection in [
                idle, nonMarkdownIdle, mixedIdle, structurallyBusy,
                actionLoading, propertyEditing,
            ] {
                XCTAssertTrue(
                    projection.allSatisfy {
                        $0.disabledReason == nil && $0.intent != nil
                    },
                    "unavailable contextual actions are omitted; every rendered action is invokable")
            }
        }
    }

    func testInsideAndOutsideRowTargetsAreFrozenWithoutSelectionMutation() {
        let published = snapshot(
            [item("Note.md"), item("Folder", directory: true)],
            focusedPath: "Folder",
            creationParent: "Folder")
        let unchangedPublished = published
        let outsideRow = item("Other/Outside.md")
        let expectedOutside = SidebarSelectionSnapshot(
            sessionIdentity: published.sessionIdentity,
            items: [outsideRow],
            focusedPath: outsideRow.path,
            creationParent: "Other")
        let outsideFolder = item("Other/Folder", directory: true)
        let expectedOutsideFolder = SidebarSelectionSnapshot(
            sessionIdentity: published.sessionIdentity,
            items: [outsideFolder],
            focusedPath: outsideFolder.path,
            creationParent: outsideFolder.path)

        for surface in [SidebarActionSurface.contextMenu, .voiceOver] {
            let contextualOpen = surface == .contextMenu
                ? [SlateCommandID.sidebarOpen] : []
            let inside = FileTreeSidebar.sidebarRowActionProjection(
                surface: surface,
                row: item("Note.md"),
                publishedSnapshot: published,
                structuralMutationDisabledReason: nil,
                actionDisabledReasons: [:])
            XCTAssertEqual(
                inside.targetSnapshot,
                published,
                "\(surface): inside-selection actions retain the exact published snapshot")
            XCTAssertEqual(
                inside.evaluations.map(\.id),
                [SlateCommandID.moveTo, SlateCommandID.deleteEntry])
            XCTAssertTrue(inside.evaluations.allSatisfy {
                $0.disabledReason == nil && $0.intent?.snapshot == published
            })

            let outside = FileTreeSidebar.sidebarRowActionProjection(
                surface: surface,
                row: outsideRow,
                publishedSnapshot: published,
                structuralMutationDisabledReason: nil,
                actionDisabledReasons: [:])
            XCTAssertEqual(
                outside.targetSnapshot,
                expectedOutside,
                "\(surface): outside file targets one explicit row without selection mutation")
            XCTAssertEqual(
                outside.evaluations.map(\.id),
                contextualOpen + [
                    SlateCommandID.renameEntry, SlateCommandID.moveTo,
                    SlateCommandID.duplicateEntry, SlateCommandID.revealInFinder,
                    SlateCommandID.copyPath, SlateCommandID.sidebarCopyWikilink,
                    SlateCommandID.deleteEntry,
                ])
            XCTAssertTrue(outside.evaluations.allSatisfy {
                $0.disabledReason == nil && $0.intent?.snapshot == expectedOutside
            })

            let folder = FileTreeSidebar.sidebarRowActionProjection(
                surface: surface,
                row: outsideFolder,
                publishedSnapshot: published,
                structuralMutationDisabledReason: nil,
                actionDisabledReasons: [:])
            XCTAssertEqual(
                folder.targetSnapshot,
                expectedOutsideFolder,
                "\(surface): outside folder is its own canonical creation parent")
            XCTAssertEqual(
                folder.evaluations.map(\.id),
                [
                    SlateCommandID.newNote, SlateCommandID.newFolder,
                    SlateCommandID.newFromTemplate,
                    SlateCommandID.renameEntry, SlateCommandID.moveTo,
                    SlateCommandID.revealInFinder, SlateCommandID.copyPath,
                    SlateCommandID.deleteEntry,
                ])
            XCTAssertTrue(folder.evaluations.allSatisfy {
                $0.disabledReason == nil && $0.intent?.snapshot == expectedOutsideFolder
            })
        }
        XCTAssertEqual(
            published,
            unchangedPublished,
            "the pure row projection must not publish or mutate tree selection")
    }

    func testTemplateUsesCanonicalRootFolderOrFileParentAndRejectsMultiSelection() throws {
        let root = snapshot([])
        let file = snapshot(
            [item("Folder/Note.md")],
            focusedPath: "Folder/Note.md",
            creationParent: "Folder")
        let folder = snapshot(
            [item("Other", directory: true)],
            focusedPath: "Other",
            creationParent: "Other")
        let mixed = snapshot(
            [item("Folder/Note.md"), item("Other", directory: true)],
            focusedPath: "Other",
            creationParent: "Other")
        for selection in [root, file, folder] {
            for surface in [
                SidebarActionSurface.menuBar, .commandPalette, .toolbar, .keyboard,
            ] {
                let evaluation = try XCTUnwrap(
                    SidebarActionCatalog.project(surface: surface, snapshot: selection)
                        .first { $0.id == SlateCommandID.newFromTemplate })
                XCTAssertNil(evaluation.disabledReason)
                let intent = try XCTUnwrap(evaluation.intent)
                XCTAssertEqual(intent.snapshot, selection)
            }
        }
        for surface in [
            SidebarActionSurface.menuBar, .commandPalette, .toolbar, .keyboard,
        ] {
            let evaluation = try XCTUnwrap(
                SidebarActionCatalog.project(surface: surface, snapshot: mixed)
                    .first { $0.id == SlateCommandID.newFromTemplate })
            XCTAssertEqual(
                evaluation.disabledReason,
                "Select no more than one item to choose a creation location.")
            XCTAssertNil(evaluation.intent)
        }
        for surface in [SidebarActionSurface.contextMenu, .voiceOver] {
            XCTAssertFalse(
                SidebarActionCatalog.project(surface: surface, snapshot: file)
                    .contains { $0.id == SlateCommandID.newFromTemplate })
            XCTAssertFalse(
                SidebarActionCatalog.project(surface: surface, snapshot: mixed)
                    .contains { $0.id == SlateCommandID.newFromTemplate })
            let folderEvaluation = try XCTUnwrap(
                SidebarActionCatalog.project(surface: surface, snapshot: folder)
                    .first { $0.id == SlateCommandID.newFromTemplate })
            XCTAssertEqual(folderEvaluation.intent?.snapshot, folder)
        }
    }

    func testRegistryBackstopUsesTheSameCatalogNoVaultReason() {
        let state = AppState()
        XCTAssertThrowsError(
            try state.commandRegistry.invokeById(id: SlateCommandID.renameEntry),
            "an external registry caller must not bypass current catalog evaluation"
        ) { error in
            guard case CommandError.ActionFailed(message: let message) = error else {
                return XCTFail("expected ActionFailed, got \(error)")
            }
            XCTAssertEqual(message, SidebarActionCatalog.noVaultReason)
        }
    }

    func testSidebarRegistrationInvokesEveryStableIDThroughCaptureSeam() throws {
        let registry = CommandRegistry()
        var capturedIDs: [String] = []
        registerSidebarCommands(
            into: registry,
            invokeID: { id in capturedIDs.append(id) })

        XCTAssertEqual(registry.list().count, Self.expectedActions.count)
        for expected in Self.expectedActions {
            capturedIDs.removeAll()
            try registry.invokeById(id: expected.id)
            XCTAssertEqual(
                capturedIDs,
                [expected.id],
                "registered \(expected.id) must call the stable-ID dispatcher once")
        }
    }

    func testImportCommandUsesOneCatalogDefinitionAcrossFileMenuRegistryAndPalette() throws {
        let id = "slate.file.importFilesAndFolders"
        XCTAssertTrue(SlateCommandID.all.contains(id))
        XCTAssertTrue(SlateCommandID.structuralMutationCommands.contains(id))
        XCTAssertEqual(
            SlateMacApp.SidebarFileMenuActionGroup.creation.actionIDs,
            [
                SlateCommandID.newNote,
                SlateCommandID.newFolder,
                SlateCommandID.newFromTemplate,
                id,
            ])

        let root = snapshot([])
        let menu = try XCTUnwrap(
            SlateMacApp.sidebarFileMenuEvaluations(
                for: .creation,
                from: SidebarActionCatalog.project(
                    surface: .menuBar,
                    snapshot: root)
            ).first { $0.id == id })
        let palette = try XCTUnwrap(
            SidebarActionCatalog.project(
                surface: .commandPalette,
                snapshot: root
            ).first { $0.id == id })
        XCTAssertEqual(menu, palette)

        let registry = CommandRegistry()
        var invoked: [String] = []
        registerSidebarCommands(
            into: registry,
            invokeID: { invoked.append($0) })
        let command = try XCTUnwrap(registry.findById(id: id))
        XCTAssertEqual(command.label, "Import Files and Folders…")
        XCTAssertEqual(command.accessibilityHint, menu.definition.accessibilityHint)
        XCTAssertNil(command.hotkeyHint)
        XCTAssertEqual(command.section, .sidebar)
        try registry.invokeById(id: id)
        XCTAssertEqual(invoked, [id])
    }

    func testSingleFolderSelectionPreservesTemplateDestinationThroughLiveRegistry()
        async throws
    {
        let state = try openVault(
            named: "folder-template",
            files: ["Note.md"],
            folders: ["Folder"])
        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        let owner = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let folder = SidebarSelectionSnapshot(
            sessionIdentity: owner,
            items: [item("Folder", directory: true)],
            focusedPath: "Folder",
            creationParent: "Folder")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(folder))

        var pickerParents: [String] = []
        state.sidebarActionDispatchOverrides.openTemplatePicker = { parent in
            pickerParents.append(parent)
            return true
        }
        XCTAssertNoThrow(
            try state.commandRegistry.invokeById(id: SlateCommandID.newFromTemplate))
        XCTAssertEqual(
            pickerParents, ["Folder"],
            "the shipped Template command must preserve the frozen folder destination")
        XCTAssertNil(state.templatePickerTask)
        XCTAssertFalse(state.isTemplatePickerOpen)
    }

    func testAppStateProjectionAndPaletteResolverMergeEveryCurrentReason() async throws {
        func evaluation(
            _ id: String,
            in projection: [SidebarActionEvaluation]
        ) throws -> SidebarActionEvaluation {
            try XCTUnwrap(projection.first { $0.id == id })
        }

        let noVaultState = AppState()
        let noVaultMenu = noVaultState.sidebarActionProjection(surface: .menuBar)
        let noVaultPalette = noVaultState.sidebarActionProjection(surface: .commandPalette)
        XCTAssertEqual(noVaultMenu, noVaultPalette)
        XCTAssertTrue(noVaultMenu.allSatisfy {
            $0.disabledReason == SidebarActionCatalog.noVaultReason
        })

        let state = try openVault(
            named: "projection-reasons",
            files: ["Note.md"],
            folders: ["Folder"])
        let summary = TemplateSummary(
            path: "Templates/Meeting.md", name: "Meeting", description: nil)
        state.templateListRunner = { _, _ in .success([summary]) }
        await state.refreshTemplateAvailability()?.value
        let owner = ObjectIdentifier(try XCTUnwrap(state.currentSession))
        let markdown = SidebarSelectionSnapshot(
            sessionIdentity: owner,
            items: [item("Note.md")],
            focusedPath: "Note.md",
            creationParent: "")
        let mixed = SidebarSelectionSnapshot(
            sessionIdentity: owner,
            items: [item("Note.md"), item("Folder", directory: true)],
            focusedPath: "Folder",
            creationParent: "Folder")

        XCTAssertTrue(state.publishSidebarSelectionSnapshot(mixed))
        let capability = state.sidebarActionProjection(surface: .menuBar)
        XCTAssertEqual(
            try evaluation(SlateCommandID.duplicateEntry, in: capability).disabledReason,
            "Select exactly one file to duplicate.")
        let template = try evaluation(SlateCommandID.newFromTemplate, in: capability)
        XCTAssertEqual(
            template.disabledReason,
            "Select no more than one item to choose a creation location.")
        XCTAssertNil(template.intent)

        XCTAssertTrue(state.publishSidebarSelectionSnapshot(markdown))
        let busy = "Another file operation is still running."
        state.sidebarActionStructuralDisabledReasonOverride = busy
        let structural = state.sidebarActionProjection(surface: .menuBar)
        XCTAssertEqual(
            try evaluation(SlateCommandID.renameEntry, in: structural).disabledReason,
            busy)
        XCTAssertNil(
            try evaluation(SlateCommandID.copyPath, in: structural).disabledReason,
            "inspection actions remain available during structural mutation")
        XCTAssertEqual(
            try evaluation(
                SlateCommandID.sidebarCopyWikilink, in: structural
            ).disabledReason,
            busy,
            "the synchronous native formatter must not contend with a structural writer")

        state.sidebarActionStructuralDisabledReasonOverride = nil
        let loading = "Wikilinks are still loading."
        state.sidebarActionAvailabilityReasonProvider = { id in
            id == SlateCommandID.sidebarCopyWikilink ? loading : nil
        }
        let actionSpecific = state.sidebarActionProjection(surface: .menuBar)
        XCTAssertEqual(
            try evaluation(
                SlateCommandID.sidebarCopyWikilink, in: actionSpecific
            ).disabledReason,
            loading)
        state.sidebarActionAvailabilityReasonProvider = { _ in nil }

        let propertyTask = state.setProperty(
            path: "Note.md", key: "status", value: .text(value: "busy"))
        XCTAssertNotNil(propertyTask)
        XCTAssertEqual(
            state.propertyEditNavigationDisabledReason,
            AppState.propertyEditInProgressReason)
        let propertyEditingMenu = state.sidebarActionProjection(surface: .menuBar)
        let propertyEditingPalette = state.sidebarActionProjection(surface: .commandPalette)
        XCTAssertEqual(propertyEditingMenu, propertyEditingPalette)
        XCTAssertEqual(
            try evaluation(
                SlateCommandID.sidebarOpen, in: propertyEditingMenu
            ).disabledReason,
            AppState.propertyEditInProgressReason)
        for id in [SlateCommandID.revealInFinder, SlateCommandID.copyPath] {
            XCTAssertNil(
                try evaluation(id, in: propertyEditingMenu).disabledReason,
                "non-native inspection remains available during property edits: \(id)")
        }
        XCTAssertEqual(
            try evaluation(
                SlateCommandID.sidebarCopyWikilink, in: propertyEditingMenu
            ).disabledReason,
            AppState.propertyEditInProgressReason,
            "Copy Wikilink must not synchronously contend on the native session lock")

        let reasonCases: [(String, [SidebarActionEvaluation], String)] = [
            (SlateCommandID.renameEntry, noVaultMenu, SidebarActionCatalog.noVaultReason),
            (
                SlateCommandID.duplicateEntry, capability,
                "Select exactly one file to duplicate."
            ),
            (SlateCommandID.renameEntry, structural, busy),
            (SlateCommandID.sidebarCopyWikilink, actionSpecific, loading),
            (
                SlateCommandID.sidebarOpen, propertyEditingPalette,
                AppState.propertyEditInProgressReason
            ),
        ]
        for (id, projection, expectedReason) in reasonCases {
            let metadata = try XCTUnwrap(Self.expectedActions.first { $0.id == id })
            let command = Command(
                id: metadata.id,
                label: metadata.label,
                accessibilityHint: metadata.hint,
                hotkeyHint: metadata.hotkey,
                section: metadata.section)
            let currentReason = CommandPaletteView.disabledReason(
                for: command,
                sidebarActionProjection: projection,
                structuralMutationDisabledReason: nil)
            XCTAssertEqual(currentReason, expectedReason)
            XCTAssertEqual(
                CommandPaletteView.selectionAnnouncement(
                    for: command, disabledReason: currentReason),
                "Selected: \(metadata.label). Unavailable: \(expectedReason)")
            var invocations = 0
            var announcements: [String] = []
            XCTAssertNil(
                CommandPaletteView.invokeIfAvailable(
                    disabledReason: currentReason,
                    restoreSearchFocus: {},
                    announceUnavailable: { announcements.append($0) },
                    invoke: { invocations += 1; return .success }))
            XCTAssertEqual(invocations, 0)
            XCTAssertEqual(announcements, [expectedReason])
        }

        propertyTask?.cancel()
        await propertyTask?.value
    }

    func testAppStateProjectionCallGraphHasNoRenderIO() throws {
        let appState = try semanticSource("AppState.swift")
        let body = try functionBody(named: "sidebarActionProjection", in: appState)
        for required in [
            "SidebarActionCatalog.project", "sidebarSelectionSnapshot",
            "structuralMutationDisabledReason", "sidebarActionDisabledReasons",
            "propertyEditNavigationDisabledReason", "SlateCommandID.sidebarOpen",
        ] {
            XCTAssertTrue(
                body.contains(required),
                "the current AppState projection must merge \(required)")
        }
        try assertClosedCallExpressions(
            owner: "AppState projection",
            body: body,
            allowedExact: ["SidebarActionCatalog.project"])

        let catalog = try semanticSource("Sidebar/SidebarActionCatalog.swift")
        let tree = try semanticSource("FileTreeSidebar.swift")
        let rowTarget = try functionBody(named: "sidebarRowActionProjection", in: tree)
        let catalogRenderer = try functionBody(named: "sidebarCatalogActions", in: tree)
        try assertClosedCallExpressions(
            owner: "row target projection",
            body: rowTarget,
            allowedExact: [
                "SidebarActionCatalog.project",
                "SidebarActionCatalog.evaluation",
            ],
            allowedTerminal: ["contains", "SidebarSelectionSnapshot", "String"])
        try assertClosedCallExpressions(
            owner: "catalog renderer",
            body: catalogRenderer,
            allowedExact: [
                "appState.dispatchSidebarAction",
                "appState.postMutationAnnouncement",
            ],
            allowedTerminal: ["ForEach", "Button", "label", "first"])
        XCTAssertTrue(
            appState.contains(
                "@Published private(set) var sidebarSelectionSnapshot: SidebarSelectionSnapshot?"),
            "the projection reads a stored snapshot, not an unscanned getter")

        let owners: [(String, Substring)] = [
            ("catalog evaluation", try functionBody(named: "evaluation", in: catalog)),
            ("catalog projection", try functionBody(named: "project", in: catalog)),
            ("AppState projection", body),
            (
                "action-specific reason getter",
                try computedPropertyBody(named: "sidebarActionDisabledReasons", in: appState)
            ),
            (
                "property-edit navigation getter",
                try computedPropertyBody(
                    named: "propertyEditNavigationDisabledReason", in: appState)
            ),
            (
                "structural reason getter",
                try computedPropertyBody(named: "structuralMutationDisabledReason", in: appState)
            ),
            (
                "row selection-item mapper",
                try functionBody(named: "sidebarSelectionItem", in: tree)
            ),
            (
                "row current-reason getter",
                try computedPropertyBody(
                    named: "sidebarRowActionDisabledReasons", in: tree)
            ),
            ("row target projection", rowTarget),
            ("catalog renderer", catalogRenderer),
        ]
        let forbiddenTokens = [
            "FileManager", "VaultSession", "wikilinkForPath", "NSWorkspace",
            "NSPasteboard", "Task", "await", "Data(contentsOf", "String(contentsOf",
            "attributesOfItem", "contentsOfDirectory", "resourceValues",
            "keyboardShortcut",
        ]
        for (owner, ownerBody) in owners {
            for forbidden in forbiddenTokens {
                XCTAssertFalse(
                    ownerBody.contains(forbidden),
                    "\(owner) performs render-time I/O or exposes a shortcut: \(forbidden)")
            }
        }
    }

    func testFileMenuAndPaletteUseSharedProjectionAndFrozenIntentDispatch() throws {
        let menuSource = try semanticSource("SlateMacApp.swift")
        let menu = try blockBody(
            anchoredBy: "CommandGroup(replacing: .newItem)", in: menuSource)
        XCTAssertEqual(
            occurrences(of: "appState.sidebarActionProjection(", in: menu),
            1,
            "File menu has one live ordered projection owner")
        XCTAssertTrue(menu.contains("surface: .menuBar"))
        XCTAssertTrue(menu.contains("let sidebarEvaluations = appState.sidebarActionProjection"))
        for group in ["creation", "open", "management", "inspection", "destructive"] {
            XCTAssertEqual(
                occurrences(
                    of: "sidebarFileMenuActions(.\(group), evaluations: sidebarEvaluations)",
                    in: menu),
                1,
                "File menu renders the \(group) catalog group exactly once")
        }
        let items = try functionBody(named: "sidebarFileMenuActions", in: menuSource)
        let compactItems = String(items).filter { !$0.isWhitespace }
        for binding in [
            "evaluation.definition.label",
            "evaluation.intent",
            "appState.dispatchSidebarAction",
            "appState.postMutationAnnouncement",
            ".disabled(evaluation.disabledReason != nil)",
            ".accessibilityHint(evaluation.disabledReason ?? evaluation.definition.accessibilityHint)",
            ".help(evaluation.disabledReason ?? evaluation.definition.accessibilityHint)",
            ".keyboardShortcut(Self.sidebarMenuKeyboardShortcut(for: evaluation.id))",
        ] {
            XCTAssertTrue(
                compactItems.contains(binding.filter { !$0.isWhitespace }),
                "each ordered File-menu item binds the same evaluation's \(binding)")
        }
        XCTAssertTrue(compactItems.contains("Self.sidebarFileMenuEvaluations("))

        for legacy in [
            "treeSelectedNode", "newNoteCommand", "newFolderCommand",
            "openTemplatePicker", "renameSelectedCommand", "moveSelectedCommand",
            "duplicateSelectedCommand", "revealSelectedInFinderCommand",
            "copySelectedPathCommand", "deleteSelectedCommand",
        ] {
            XCTAssertFalse(
                menu.contains(legacy),
                "the File menu must not retain a second \(legacy) executor")
        }

        let palette = try typeBody(
            named: "CommandPaletteView",
            in: try semanticSource("CommandPaletteView.swift"))
        let paletteBody = try computedPropertyBody(
            named: "body", in: String(palette))
        let speech = try blockBody(
            anchoredBy: ".onChange(of: model.selectedID)", in: String(paletteBody))
        let row = try functionBody(named: "commandRow", in: String(palette))
        let invokeSelected = try functionBody(named: "invokeSelected", in: String(palette))
        let invoke = try functionBody(named: "invoke", in: String(palette))
        XCTAssertTrue(
            invokeSelected.contains("invoke(command)"),
            "Return's selected-command owner must reach the checked invoke owner")
        for (owner, body) in [
            ("selection speech", speech), ("row", row), ("Return", invoke),
        ] {
            XCTAssertEqual(
                occurrences(of: "Self.disabledReason(", in: body),
                1,
                "\(owner) calls the one per-ID current-reason resolver exactly once")
            XCTAssertTrue(body.contains("sidebarActionProjection:"))
            XCTAssertTrue(body.contains("appState.sidebarActionProjection(surface: .commandPalette)"))
        }
    }

    func testFileMenuHasExactHIGGroupsOrderAndNoCatalogDuplicates() throws {
        let source = try sourceRegion("SlateMacApp.swift")
        let command = try pairedBlockBody(
            structuralAnchor: "CommandGroup(replacing: .newItem)", in: source)
        let ordered = normalized(command.literals)
        let tokens = [
            "sidebarFileMenuActions(.creation, evaluations: sidebarEvaluations)",
            "Button(\"Open Vault…\")",
            "Menu(\"Open Recent\")",
            "sidebarFileMenuActions(.open, evaluations: sidebarEvaluations)",
            "Button(\"Quick Open…\")",
            "Button(\"Save\")",
            "sidebarFileMenuActions(.management, evaluations: sidebarEvaluations)",
            "sidebarFileMenuActions(.inspection, evaluations: sidebarEvaluations)",
            "sidebarFileMenuActions(.destructive, evaluations: sidebarEvaluations)",
            "Button(\"Duplicate Tab\")",
        ]
        var cursor = ordered.startIndex
        for token in tokens {
            guard let range = ordered.range(of: token, range: cursor..<ordered.endIndex) else {
                XCTFail("File menu is missing or misorders \(token)")
                return
            }
            cursor = range.upperBound
        }

        let semantic = try semanticSource("SlateMacApp.swift")
        let membership = try computedPropertyBody(named: "actionIDs", in: semantic)
        let expectedGroups = [
            "case .creation: return [ SlateCommandID.newNote, SlateCommandID.newFolder, SlateCommandID.newFromTemplate, SlateCommandID.importFilesAndFolders ]",
            "case .open: return [SlateCommandID.sidebarOpen]",
            "case .management: return [ SlateCommandID.renameEntry, SlateCommandID.moveTo, SlateCommandID.duplicateEntry ]",
            "case .inspection: return [ SlateCommandID.revealInFinder, SlateCommandID.copyPath, SlateCommandID.sidebarCopyWikilink ]",
            "case .destructive: return [SlateCommandID.deleteEntry]",
        ]
        let compactMembership = String(membership)
            .filter { !$0.isWhitespace }
            .replacingOccurrences(of: ",]", with: "]")
        for expected in expectedGroups {
            XCTAssertTrue(
                compactMembership.contains(expected.filter { !$0.isWhitespace }),
                "surface-owned File-menu metadata drifted: \(expected)")
        }
        for id in [
            "SlateCommandID.sidebarOpen", "SlateCommandID.newNote",
            "SlateCommandID.newFolder", "SlateCommandID.newFromTemplate",
            "SlateCommandID.importFilesAndFolders",
            "SlateCommandID.renameEntry", "SlateCommandID.moveTo",
            "SlateCommandID.duplicateEntry", "SlateCommandID.revealInFinder",
            "SlateCommandID.copyPath", "SlateCommandID.sidebarCopyWikilink",
            "SlateCommandID.deleteEntry",
        ] {
            XCTAssertEqual(
                occurrences(of: id, in: membership), 1,
                "each catalog action belongs to exactly one File-menu group: \(id)")
        }

        let projection = SidebarActionCatalog.project(
            surface: .menuBar,
            snapshot: snapshot([item("Note.md")], focusedPath: "Note.md"))
        let grouped = SlateMacApp.SidebarFileMenuActionGroup.allCases.flatMap {
            SlateMacApp.sidebarFileMenuEvaluations(for: $0, from: projection)
        }
        XCTAssertEqual(
            grouped.map(\.id),
            [
                SlateCommandID.newNote, SlateCommandID.newFolder,
                SlateCommandID.newFromTemplate, SlateCommandID.importFilesAndFolders,
                SlateCommandID.sidebarOpen,
                SlateCommandID.renameEntry, SlateCommandID.moveTo,
                SlateCommandID.duplicateEntry, SlateCommandID.revealInFinder,
                SlateCommandID.copyPath, SlateCommandID.sidebarCopyWikilink,
                SlateCommandID.deleteEntry,
            ])
        XCTAssertEqual(grouped.count, SidebarActionCatalog.actions.count)
        XCTAssertEqual(Set(grouped.map(\.id)).count, SidebarActionCatalog.actions.count)
        XCTAssertEqual(
            Dictionary(uniqueKeysWithValues: grouped.map { ($0.id, $0) }),
            Dictionary(uniqueKeysWithValues: projection.map { ($0.id, $0) }),
            "File grouping must preserve every live evaluation without mutation")
    }

    func testRegistrySidebarOwnerRegistersDefinitionsAndDispatchesStableIDs() throws {
        let source = try semanticSource("SlateCommands.swift")
        let core = try functionBody(named: "registerCoreCommands", in: source)
        XCTAssertEqual(
            occurrences(of: "registerSidebarCommands(", in: core),
            1,
            "the live core registry calls the shared Sidebar registrar exactly once")
        let liveCall = try blockBody(
            anchoredBy: "registerSidebarCommands(", in: String(core))
        XCTAssertTrue(liveCall.contains("appState.dispatchSidebarAction(id: id)"))

        let body = try functionBody(named: "registerSidebarCommands", in: source)
        XCTAssertTrue(body.contains("SidebarActionCatalog.actions"))
        XCTAssertTrue(body.contains("definition.id"))
        XCTAssertTrue(body.contains("definition.label"))
        XCTAssertTrue(body.contains("definition.accessibilityHint"))
        XCTAssertTrue(body.contains("section: .sidebar"))
        XCTAssertTrue(body.contains("invokeID(definition.id)"))
        for legacy in [
            "treeSelectedNode", "newNoteCommand", "newFolderCommand",
            "openTemplatePicker", "renameSelectedCommand", "moveSelectedCommand",
            "duplicateSelectedCommand", "revealSelectedInFinderCommand",
            "copySelectedPathCommand", "deleteSelectedCommand",
        ] {
            XCTAssertFalse(body.contains(legacy))
        }
        for idToken in [
            "SlateCommandID.sidebarOpen", "SlateCommandID.newNote",
            "SlateCommandID.newFolder", "SlateCommandID.newFromTemplate",
            "SlateCommandID.importFilesAndFolders",
            "SlateCommandID.renameEntry", "SlateCommandID.moveTo",
            "SlateCommandID.duplicateEntry", "SlateCommandID.revealInFinder",
            "SlateCommandID.copyPath", "SlateCommandID.sidebarCopyWikilink",
            "SlateCommandID.deleteEntry",
        ] {
            XCTAssertFalse(
                core.contains(idToken),
                "old live per-action registration remains in registerCoreCommands: \(idToken)")
        }
    }

    func testContextAndVoiceOverShareOneRowProjectionAndFrozenDispatcher() throws {
        let document = try sourceRegion("FileTreeSidebar.swift")
        let folderRegion = try pairedBlockBody(
            structuralAnchor: "private func folderRow(", in: document)
        let fileRegion = try pairedBlockBody(
            structuralAnchor: "private func fileRow(", in: document)
        let rendererRegion = try pairedBlockBody(
            structuralAnchor: "func sidebarCatalogActions(", in: document)
        let targetRegion = try pairedBlockBody(
            structuralAnchor: "func sidebarRowActionProjection(", in: document)
        let folder = normalized(folderRegion.structural)
        let file = normalized(fileRegion.structural)
        let renderer = normalized(rendererRegion.structural)
        let target = normalized(targetRegion.structural)

        let folderVoiceOverRegion = try pairedBlockBody(
            structuralAnchor: ".accessibilityActions", in: folderRegion)
        let folderContextRegion = try pairedBlockBody(
            structuralAnchor: ".contextMenu {", in: folderRegion)
        let fileVoiceOverRegion = try pairedBlockBody(
            structuralAnchor: ".accessibilityActions", in: fileRegion)
        let fileContextRegion = try pairedBlockBody(
            structuralAnchor: ".contextMenu {", in: fileRegion)
        let folderContext = normalized(folderContextRegion.structural)
        XCTAssertGreaterThanOrEqual(
            occurrences(of: "SlateCommandID.newFromTemplate", in: folderContext),
            2,
            "the live folder New submenu must both detect and render Template")
        let actionOwners: [(String, SourceRegion)] = [
            ("catalog renderer", rendererRegion),
            ("folder VoiceOver", folderVoiceOverRegion),
            ("folder context", folderContextRegion),
            ("file VoiceOver", fileVoiceOverRegion),
            ("file context", fileContextRegion),
        ]
        for (owner, region) in actionOwners {
            for token in [
                "keyboardShortcut", "KeyboardShortcut", "hotkey", "keyEquivalent",
                "shortcut",
            ] {
                XCTAssertFalse(
                    region.structural.contains(token),
                    "\(owner) must not expose contextual shortcut structure: \(token)")
            }
            for token in [
                "⌘", "⌥", "⌃", "⇧", "Command-", "Option-", "Control-", "Shift-",
            ] {
                XCTAssertFalse(
                    region.literals.contains(token),
                    "\(owner) must not expose contextual shortcut wording: \(token)")
            }
        }

        for (surface, region) in [
            ("folder VoiceOver", folderVoiceOverRegion),
            ("folder context", folderContextRegion),
            ("file context", fileContextRegion),
        ] {
            let body = normalized(region.structural)
            XCTAssertEqual(
                occurrences(of: "Self.sidebarRowActionProjection(", in: body),
                1,
                "\(surface) has one shared target projection")
            XCTAssertTrue(
                body.contains(
                    surface.contains("VoiceOver")
                        ? "surface: .voiceOver" : "surface: .contextMenu"))
            XCTAssertTrue(body.contains("row: sidebarSelectionItem(for: node)"))
            XCTAssertTrue(body.contains("sidebarCatalogActions(projection.evaluations)"))
            XCTAssertFalse(body.contains("keyboardShortcut"))
            for forbidden in [
                "fileManagementMenu", "batchManagementMenu", "requestCreateNote",
                "newFolderInContext", "beginRename", "requestPendingMove",
                "requestBatchMove", "requestDuplicateEntry", "requestDeleteEntry",
                "requestBatchDelete", "copyAbsolutePath", "activateFileViewerSelecting",
                "publishSidebarSelectionSnapshot", "mutateSelectionAndPublish",
                "applyPlainSelection", "applyMultiSelectClick", "listSelection =",
                "treeSelectedNode =",
            ] {
                XCTAssertFalse(
                    body.contains(forbidden),
                    "\(surface) must not retain \(forbidden)")
                }
        }
        let fileVoiceOver = normalized(fileVoiceOverRegion.structural)
        XCTAssertEqual(
            occurrences(of: "Self.sidebarRowActionProjection(", in: fileVoiceOver),
            0,
            "the file row must reuse its one retained VoiceOver projection")
        XCTAssertTrue(fileVoiceOver.contains("voiceOverProjection"))
        XCTAssertTrue(fileVoiceOver.contains("sidebarCatalogActions("))
        XCTAssertFalse(fileVoiceOver.contains("SlateCommandID.sidebarOpen"))
        XCTAssertEqual(
            occurrences(of: "Self.sidebarRowActionProjection(", in: file),
            2,
            "the file row owns one VoiceOver capture and one context capture")
        XCTAssertFalse(document.structural.contains("func fileManagementMenu("))
        XCTAssertFalse(document.structural.contains("func batchManagementMenu("))

        XCTAssertTrue(renderer.contains("evaluation.intent"))
        XCTAssertTrue(renderer.contains("appState.dispatchSidebarAction"))
        XCTAssertTrue(renderer.contains("appState.postMutationAnnouncement"))
        XCTAssertFalse(
            renderer.contains("dispatchSidebarAction(id:"),
            "context and VoiceOver dispatch the already-frozen target intent")
        XCTAssertTrue(target.contains("SidebarActionCatalog.project"))
        XCTAssertTrue(target.contains("SidebarActionCatalog.evaluation"))
        XCTAssertTrue(target.contains("SlateCommandID.sidebarOpen"))
        XCTAssertTrue(target.contains("openEvaluation"))
        XCTAssertTrue(target.contains("publishedSnapshot"))
        XCTAssertTrue(target.contains("SidebarSelectionSnapshot"))
        for mutation in [
            "publishSidebarSelectionSnapshot", "mutateSelectionAndPublish",
            "applyPlainSelection", "listSelection =", "treeSelectedNode =",
        ] {
            XCTAssertFalse(
                target.contains(mutation),
                "target projection must not mutate selection: \(mutation)")
        }

        let openModifier = normalized(
            try pairedBlockBody(
                structuralAnchor: "struct FileRowOpenAccessibilityModifier",
                in: document
            ).structural)
        for required in [
            "presentation.exposesButtonTrait",
            "presentation.exposesDefaultAction",
            "let openIntent = presentation.intent",
            ".accessibilityAddTraits(.isButton)",
            ".accessibilityAction(.default)",
            "dispatch(openIntent)",
        ] {
            XCTAssertTrue(
                openModifier.contains(required),
                "conditional file default Open modifier must own \(required)")
        }
        for forbidden in ["activate()", "applyPlainSelection", "openFile"] {
            XCTAssertFalse(openModifier.contains(forbidden))
        }
        XCTAssertTrue(file.contains("FileRowOpenAccessibilityModifier("))
        XCTAssertTrue(file.contains("presentation: openPresentation"))
        XCTAssertTrue(file.contains("appState.dispatchSidebarAction(openIntent)"))
        XCTAssertTrue(file.contains("appState.postMutationAnnouncement"))
        XCTAssertFalse(file.contains(".accessibilityAction(.default)"))
        XCTAssertFalse(file.contains(".accessibilityAddTraits(.isButton)"))
        XCTAssertTrue(file.contains("Self.fileRowOpenAccessibilityPresentation("))

        let folderActivate = try blockBody(anchoredBy: "let activate =", in: String(folder))
        let folderDefault = try blockBody(
            anchoredBy: ".accessibilityAction(.default)", in: String(folder))
        let folderDisclosure = try blockBody(
            anchoredBy: ".accessibilityAction(named:", in: String(folder))
        XCTAssertTrue(folderActivate.contains("tree.toggle(node)"))
        XCTAssertTrue(folderDefault.contains("activate()"))
        XCTAssertTrue(folderDisclosure.contains("tree.toggle(node)"))
        for disclosureOwner in [folderActivate, folderDefault, folderDisclosure] {
            XCTAssertFalse(disclosureOwner.contains("SlateCommandID.sidebarOpen"))
            XCTAssertFalse(disclosureOwner.contains("dispatchSidebarAction"))
        }
    }

    func testWorkspaceOpenButtonsRemainDistinctInsideTheRealFileContextOwner() throws {
        let document = try sourceRegion("FileTreeSidebar.swift")
        let fileRow = try pairedBlockBody(
            structuralAnchor: "private func fileRow(", in: document)
        let context = try pairedBlockBody(
            structuralAnchor: ".contextMenu {", in: fileRow)
        let source = normalized(context.structural)
        let compactSource = source.filter { !$0.isWhitespace }
        let literals = normalized(context.literals)

        XCTAssertEqual(
            occurrences(
                of: "appState.openFile(node.path,target:.newTab)",
                in: compactSource),
            1)
        XCTAssertTrue(
            compactSource.contains(
                "appState.openFile(node.path,target:.newSplit(.horizontal))"))
        XCTAssertTrue(literals.contains("SlateSymbol.newTab.label(\"Open in New Tab\")"))
        XCTAssertTrue(literals.contains("SlateSymbol.splitRight.label(\"Open in Split\")"))

        let availabilityGate = normalized(
            try pairedBlockBody(
                structuralAnchor: "if hasCatalogOpen", in: context
            ).structural)
        let compactAvailabilityGate = availabilityGate.filter { !$0.isWhitespace }
        XCTAssertTrue(
            availabilityGate.contains("Menu"),
            "the retained catalog Open evaluation owns the complete submenu")
        XCTAssertTrue(
            compactAvailabilityGate.contains(
                "appState.openFile(node.path,target:.newTab)"),
            "temporary Open unavailability must omit the direct new-tab path")
        XCTAssertTrue(
            compactAvailabilityGate.contains(
                "appState.openFile(node.path,target:.newSplit(.horizontal))"),
            "temporary Open unavailability must omit the direct split path")
    }

    func testContextMenusUseExactConciseGroupsAndFlatMultiFallback() throws {
        let document = try sourceRegion("FileTreeSidebar.swift")
        let folderRow = try pairedBlockBody(
            structuralAnchor: "private func folderRow(", in: document)
        let fileRow = try pairedBlockBody(
            structuralAnchor: "private func fileRow(", in: document)
        let folderContext = try pairedBlockBody(
            structuralAnchor: ".contextMenu {", in: folderRow)
        let fileContext = try pairedBlockBody(
            structuralAnchor: ".contextMenu {", in: fileRow)
        let folderStructure = normalized(folderContext.structural)
        let fileStructure = normalized(fileContext.structural)
        func compactSyntax(_ source: String) -> String {
            source.filter { !$0.isWhitespace }
                .replacingOccurrences(of: ",]", with: "]")
        }
        let folderLiterals = normalized(folderContext.literals)
        let fileLiterals = normalized(fileContext.literals)

        XCTAssertTrue(folderLiterals.contains("SlateSymbol.newNote.label(\"New\")"))
        XCTAssertTrue(fileLiterals.contains("SlateSymbol.open.label(\"Open\")"))
        XCTAssertTrue(fileLiterals.contains("SlateSymbol.copyPath.label(\"Copy\")"))
        XCTAssertTrue(fileLiterals.contains("SlateSymbol.newTab.label(\"Open in New Tab\")"))
        XCTAssertTrue(fileLiterals.contains("SlateSymbol.splitRight.label(\"Open in Split\")"))

        for (owner, source, groups) in [
            (
                "folder", folderStructure,
                [
                    "SlateCommandID.newNote, SlateCommandID.newFolder, SlateCommandID.newFromTemplate",
                    "SlateCommandID.renameEntry, SlateCommandID.moveTo",
                    "SlateCommandID.revealInFinder, SlateCommandID.copyPath",
                    "SlateCommandID.deleteEntry",
                ]
            ),
            (
                "file", fileStructure,
                [
                    "SlateCommandID.sidebarOpen",
                    "SlateCommandID.renameEntry, SlateCommandID.moveTo, SlateCommandID.duplicateEntry",
                    "SlateCommandID.revealInFinder",
                    "SlateCommandID.copyPath, SlateCommandID.sidebarCopyWikilink",
                    "SlateCommandID.deleteEntry",
                ]
            ),
        ] {
            let compactSource = compactSyntax(source)
            XCTAssertTrue(
                source.contains("projection.targetSnapshot.items.count == 1"),
                "\(owner) groups only a single-row target")
            for ids in groups {
                XCTAssertTrue(
                    compactSource.contains(
                        compactSyntax("actionIDs: [\(ids)]")),
                    "\(owner) contextual group drifted: \(ids)")
            }
            XCTAssertTrue(
                source.contains("sidebarCatalogActions(projection.evaluations)"),
                "\(owner) multi-selection fallback stays flat")
            XCTAssertFalse(source.contains("keyboardShortcut"))
        }
    }

    func testVoiceOverDefaultOpenRetainsUnavailableReasonAndNeverDuplicatesRotorOpen()
        throws
    {
        let mixed = snapshot(
            [item("Note.md"), item("Folder", directory: true)],
            focusedPath: "Folder",
            creationParent: "Folder")
        let capability = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.sidebarOpen, snapshot: mixed))
        XCTAssertEqual(capability.disabledReason, "Open is available only for files.")
        XCTAssertNil(capability.intent)
        XCTAssertFalse(
            SidebarActionCatalog.project(surface: .voiceOver, snapshot: mixed)
                .contains { $0.id == SlateCommandID.sidebarOpen })

        let single = snapshot([item("Note.md")], focusedPath: "Note.md")
        let propertyBlocked = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.sidebarOpen,
                snapshot: single,
                actionDisabledReasons: [
                    SlateCommandID.sidebarOpen: AppState.propertyEditInProgressReason
                ]))
        XCTAssertEqual(
            propertyBlocked.disabledReason,
            AppState.propertyEditInProgressReason)
        XCTAssertNil(propertyBlocked.intent)
        XCTAssertFalse(
            SidebarActionCatalog.project(
                surface: .voiceOver,
                snapshot: single,
                actionDisabledReasons: [
                    SlateCommandID.sidebarOpen: AppState.propertyEditInProgressReason
                ]
            ).contains { $0.id == SlateCommandID.sidebarOpen })

        let availableEvaluation = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.sidebarOpen, snapshot: single))
        let available = FileTreeSidebar.fileRowOpenAccessibilityPresentation(
            openEvaluation: availableEvaluation,
            availableHint: FileTreeSidebar.fileRowAvailableOpenHint(
                targetCount: single.items.count,
                idleGuidance: "Drag to move it.",
                structuralDisabledReason: nil),
            unavailableHint: "Drag to move it. Other actions are in the context menu.")
        XCTAssertEqual(available.intent, availableEvaluation.intent)
        XCTAssertEqual(available.hint, "Opens the note. Drag to move it.")
        XCTAssertTrue(available.exposesButtonTrait)
        XCTAssertTrue(available.exposesDefaultAction)

        let mixedPresentation = FileTreeSidebar.fileRowOpenAccessibilityPresentation(
            openEvaluation: capability,
            availableHint: "Opens the note. Drag to move it.",
            unavailableHint: "Drag to move it. Other actions are in the context menu.")
        XCTAssertNil(mixedPresentation.intent)
        XCTAssertTrue(
            mixedPresentation.hint.hasPrefix("Open is available only for files."))
        XCTAssertTrue(mixedPresentation.hint.contains("Drag to move it."))
        XCTAssertTrue(mixedPresentation.hint.contains("context menu"))
        XCTAssertFalse(mixedPresentation.exposesButtonTrait)
        XCTAssertFalse(mixedPresentation.exposesDefaultAction)
        XCTAssertFalse(mixedPresentation.hint.contains("Opens the note"))

        let propertyPresentation = FileTreeSidebar.fileRowOpenAccessibilityPresentation(
            openEvaluation: propertyBlocked,
            availableHint: "Opens the note. Drag to move it.",
            unavailableHint: "Drag to move it. Other actions are in the context menu.")
        XCTAssertNil(propertyPresentation.intent)
        XCTAssertTrue(
            propertyPresentation.hint.hasPrefix(AppState.propertyEditInProgressReason))
        XCTAssertTrue(propertyPresentation.hint.contains("Drag to move it."))
        XCTAssertTrue(propertyPresentation.hint.contains("context menu"))
        XCTAssertFalse(propertyPresentation.exposesButtonTrait)
        XCTAssertFalse(propertyPresentation.exposesDefaultAction)
        XCTAssertFalse(propertyPresentation.hint.contains("Opens the note"))

        let multi = snapshot(
            [item("A.md"), item("B.md")], focusedPath: "B.md")
        let multiEvaluation = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.sidebarOpen, snapshot: multi))
        let multiPresentation = FileTreeSidebar.fileRowOpenAccessibilityPresentation(
            openEvaluation: multiEvaluation,
            availableHint: FileTreeSidebar.fileRowAvailableOpenHint(
                targetCount: multi.items.count,
                idleGuidance: "Drag to move the selected files.",
                structuralDisabledReason: nil),
            unavailableHint: "Other available actions are in the context menu.")
        XCTAssertEqual(multiPresentation.intent, multiEvaluation.intent)
        XCTAssertEqual(
            multiPresentation.hint,
            "Opens the selected files. Drag to move the selected files.")
        XCTAssertTrue(multiPresentation.exposesButtonTrait)
        XCTAssertTrue(multiPresentation.exposesDefaultAction)
        XCTAssertFalse(multiPresentation.hint.contains("Opens the note"))

        let document = try sourceRegion("FileTreeSidebar.swift")
        let fileRow = try pairedBlockBody(
            structuralAnchor: "private func fileRow(", in: document)
        let structure = normalized(fileRow.structural)
        XCTAssertTrue(structure.contains("let voiceOverProjection"))
        XCTAssertTrue(structure.contains("openEvaluation"))
        XCTAssertTrue(structure.contains("Self.fileRowOpenAccessibilityPresentation("))
        XCTAssertTrue(
            structure.contains("Self.fileRowAvailableOpenHint("),
            "the live file row must derive singular/plural wording from its frozen target")
        XCTAssertTrue(structure.contains("openPresentation.hint"))
        XCTAssertTrue(structure.contains("FileRowOpenAccessibilityModifier("))
        XCTAssertFalse(structure.contains(".accessibilityAddTraits(.isButton)"))
        XCTAssertFalse(structure.contains(".accessibilityAction(.default)"))

        let modifier = normalized(
            try pairedBlockBody(
                structuralAnchor: "struct FileRowOpenAccessibilityModifier",
                in: document
            ).structural)
        XCTAssertTrue(modifier.contains("if presentation.exposesButtonTrait"))
        XCTAssertTrue(modifier.contains("presentation.exposesDefaultAction"))
        XCTAssertTrue(modifier.contains("let openIntent = presentation.intent"))
        XCTAssertTrue(modifier.contains(".accessibilityAddTraits(.isButton)"))
        XCTAssertTrue(modifier.contains(".accessibilityAction(.default)"))
    }

    func testReturnOwnerFallsThroughForFolderOnlyButConsumesFileBearingSelections()
        throws
    {
        let empty = snapshot([])
        let folder = snapshot(
            [item("Folder", directory: true)],
            focusedPath: "Folder",
            creationParent: "Folder")
        let folders = snapshot(
            [item("A", directory: true), item("B", directory: true)],
            focusedPath: "B",
            creationParent: "B")
        let oneFile = snapshot([item("A.md")], focusedPath: "A.md")
        let allFiles = snapshot(
            [item("A.md"), item("B.md")], focusedPath: "B.md")
        let mixed = snapshot(
            [item("A.md"), item("Folder", directory: true)], focusedPath: "Folder")
        for selection in [nil, empty, folder, folders] as [SidebarSelectionSnapshot?] {
            XCTAssertEqual(
                FileTreeSidebar.returnOpenDisposition(for: selection),
                .folderDisclosure)
        }
        for selection in [oneFile, allFiles, mixed] {
            XCTAssertEqual(
                FileTreeSidebar.returnOpenDisposition(for: selection),
                .openSelection)
        }

        let source = try semanticSource("FileTreeSidebar.swift")
        let type = try typeBody(named: "FileTreeSidebar", in: source)
        let treeList = try computedPropertyBody(named: "treeList", in: String(type))
        let owner = try blockBody(
            anchoredBy: ".onKeyPress(keys: [.space, .return], phases: .down)",
            in: String(treeList))
        XCTAssertTrue(owner.contains("Self.returnOpenDisposition("))
        XCTAssertTrue(owner.contains("appState.sidebarSelectionSnapshot"))
        XCTAssertTrue(owner.contains("case .openSelection"))
        XCTAssertTrue(owner.contains("case .folderDisclosure"))
        XCTAssertTrue(owner.contains("handleSelectionKeyAction(action, proxy: proxy)"))
        XCTAssertTrue(owner.contains("tree.toggle(node)"))

        var dispatched: [SidebarActionInvocationIntent] = []
        XCTAssertTrue(
            try FileTreeSidebar.invokeSidebarKeyboardAction(
                id: SlateCommandID.sidebarOpen,
                projection: SidebarActionCatalog.project(
                    surface: .keyboard, snapshot: allFiles),
                dispatch: { dispatched.append($0) }))
        XCTAssertEqual(dispatched.map(\.snapshot), [allFiles])

        XCTAssertThrowsError(
            try FileTreeSidebar.invokeSidebarKeyboardAction(
                id: SlateCommandID.sidebarOpen,
                projection: SidebarActionCatalog.project(
                    surface: .keyboard, snapshot: mixed),
                dispatch: { dispatched.append($0) })
        ) { error in
            guard case let CommandError.ActionFailed(message) = error else {
                return XCTFail("expected ActionFailed, got \(error)")
            }
            XCTAssertEqual(message, "Open is available only for files.")
        }
        XCTAssertEqual(dispatched.count, 1, "mixed Return consumes without dispatch")
    }

    func testToolbarKeepsExactSevenIDsAndRoutesOnlyTemplate() throws {
        let document = try sourceRegion("MainSplitView.swift")
        let toolbar = try pairedBlockBody(
            structuralAnchor: "private var mainToolbar", in: document)
        let expectedIDs = [
            "saveStatus", "save", "search", "template", "tasksReview",
            "citationSummary", "bibliography",
        ]
        let allItems = try regexMatches(#"\bToolbarItem\s*\("#, in: toolbar.structural)
        let idItems = try regexMatches(
            #"\bToolbarItem\s*\(\s*id\s*:\s*\"([^\"]+)\""#,
            in: toolbar.literals)
        XCTAssertEqual(allItems.count, 7, "the persisted toolbar has exactly seven items")
        XCTAssertEqual(
            idItems.count,
            allItems.count,
            "every ToolbarItem must carry a stable customization id")
        XCTAssertTrue(
            try regexMatches(#"\bToolbarItemGroup\s*\("#, in: toolbar.structural).isEmpty,
            "FL04-A adds no grouped or un-ID'd toolbar declarations")
        let actualIDs = idItems.compactMap { match -> String? in
            guard let range = Range(match.range(at: 1), in: toolbar.literals) else {
                return nil
            }
            return String(toolbar.literals[range])
        }
        XCTAssertEqual(actualIDs, expectedIDs)

        let template = try pairedBlockBody(
            literalAnchor: "ToolbarItem(id: \"template\"", in: toolbar)
        let templateBody = normalized(template.structural)
        for required in [
            "let evaluation", "sidebarActionProjection(surface: .toolbar)",
            "SlateCommandID.newFromTemplate", "evaluation.intent",
            "appState.dispatchSidebarAction",
            "appState.postMutationAnnouncement",
            ".disabled(evaluation.disabledReason != nil)",
            ".accessibilityHint(evaluation.disabledReason ?? evaluation.definition.accessibilityHint)",
            ".help(evaluation.disabledReason ?? evaluation.definition.accessibilityHint)",
        ] {
            XCTAssertTrue(
                templateBody.contains(required),
                "Template toolbar item must bind its frozen evaluation: \(required)")
        }
        XCTAssertGreaterThanOrEqual(
            occurrences(of: "evaluation.disabledReason", in: templateBody), 3)
        XCTAssertFalse(
            document.structural.contains("openTemplatePicker("),
            "no toolbar helper may hide the legacy picker executor")
    }

    func testKeyboardOwnersPreserveGatesAndDispatchSharedIDs() throws {
        func assertFrozenIntentDispatch(
            owner: String,
            in body: some StringProtocol
        ) throws {
            let dispatch = try blockBody(
                anchoredBy: "dispatch: { intent in", in: String(body))
            try assertClosedCallExpressions(
                owner: "\(owner) dispatch closure",
                body: dispatch,
                allowedExact: ["appState.dispatchSidebarAction"])
            XCTAssertFalse(
                dispatch.contains("dispatchSidebarAction(id:"),
                "\(owner) must dispatch the already-frozen intent")
        }

        let source = try semanticSource("FileTreeSidebar.swift")
        let type = try typeBody(named: "FileTreeSidebar", in: source)
        let keyboardInvoker = try functionBody(
            named: "invokeSidebarKeyboardAction", in: String(type))
        XCTAssertTrue(keyboardInvoker.contains("projection.first"))
        XCTAssertTrue(keyboardInvoker.contains("$0.id == id"))
        XCTAssertTrue(keyboardInvoker.contains("evaluation.intent"))
        XCTAssertTrue(keyboardInvoker.contains("dispatch(intent)"))
        let treeList = try computedPropertyBody(named: "treeList", in: String(type))
        let commandDown = try blockBody(
            anchoredBy: "TreeOpenSelectedKeyMonitor(", in: String(type))
        XCTAssertTrue(commandDown.contains("_ = requestOpenSelected()"))

        let returnOwner = try blockBody(
            anchoredBy: ".onKeyPress(keys: [.space, .return], phases: .down)",
            in: String(treeList))
        XCTAssertTrue(returnOwner.contains("Self.treeKeyInterceptionActive"))
        XCTAssertTrue(returnOwner.contains("handleSelectionKeyAction(action, proxy: proxy)"))
        let selectionHandler = try functionBody(
            named: "handleSelectionKeyAction", in: String(type))
        XCTAssertTrue(selectionHandler.contains("action == .openSelected"))
        XCTAssertTrue(selectionHandler.contains("requestOpenSelected()"))

        let open = try functionBody(named: "requestOpenSelected", in: source)
        XCTAssertTrue(open.contains("sidebarActionProjection(surface: .keyboard)"))
        XCTAssertTrue(open.contains("SlateCommandID.sidebarOpen"))
        XCTAssertTrue(open.contains("Self.invokeSidebarKeyboardAction("))
        XCTAssertFalse(open.contains("openCapturedPaths"))
        XCTAssertFalse(open.contains("openFile"))
        XCTAssertTrue(open.contains("appState.postMutationAnnouncement"))
        try assertFrozenIntentDispatch(owner: "Open", in: open)

        let delete = try functionBody(named: "requestDeleteFromKeyboard", in: source)
        XCTAssertTrue(delete.contains("sidebarActionProjection(surface: .keyboard)"))
        XCTAssertTrue(delete.contains("SlateCommandID.deleteEntry"))
        XCTAssertTrue(delete.contains("Self.invokeSidebarKeyboardAction("))
        XCTAssertFalse(delete.contains("requestBatchDelete"))
        XCTAssertFalse(delete.contains("requestDeleteEntry"))
        XCTAssertTrue(delete.contains("appState.postMutationAnnouncement"))
        try assertFrozenIntentDispatch(owner: "Delete", in: delete)

        let deleteCommand = try blockBody(
            anchoredBy: ".onDeleteCommand", in: String(treeList))
        XCTAssertTrue(deleteCommand.contains("fileTreeFocused"))
        XCTAssertTrue(deleteCommand.contains("deleteCommandAllowed"))
        XCTAssertTrue(deleteCommand.contains("requestDeleteFromKeyboard()"))

        let f2 = try blockBody(
            anchoredBy: ".onKeyPress(keys: [Self.f2Key])", in: String(treeList))
        XCTAssertTrue(f2.contains("treeKeyInterceptionActive"))
        XCTAssertTrue(f2.contains("typeSelectModifiersAllowed"))
        XCTAssertTrue(f2.contains("sidebarActionProjection(surface: .keyboard)"))
        XCTAssertTrue(f2.contains("SlateCommandID.renameEntry"))
        XCTAssertTrue(f2.contains("Self.invokeSidebarKeyboardAction("))
        XCTAssertFalse(f2.contains("beginRename"))
        XCTAssertTrue(f2.contains("appState.postMutationAnnouncement"))
        try assertFrozenIntentDispatch(owner: "F2", in: f2)

        let deleteKey = try blockBody(
            anchoredBy: ".onKeyPress(keys: [.delete])", in: String(treeList))
        XCTAssertTrue(deleteKey.contains("treeKeyInterceptionActive"))
        XCTAssertTrue(deleteKey.contains("deleteKeyModifiersAllowed"))
        XCTAssertTrue(deleteKey.contains("requestDeleteFromKeyboard"))

        let monitor = try semanticSource("Sidebar/TreeOpenSelectedKey.swift")
        for preservedGate in [
            "applicationIsActive", "currentKeyWindow", "hasMarkedText",
            "event.isARepeat", "fileTreeFocused", "isRenaming", "openSelected()",
        ] {
            XCTAssertTrue(
                monitor.contains(preservedGate),
                "Command-Down must preserve its \(preservedGate) gate")
        }

        let app = try sourceRegion("SlateMacApp.swift")
        let shortcutMap = try pairedBlockBody(
            structuralAnchor: "func sidebarMenuKeyboardShortcut(", in: app)
        let shortcutBody = normalized(shortcutMap.literals)
        for mapping in [
            "case SlateCommandID.newNote: return KeyboardShortcut(\"n\", modifiers: [.command])",
            "case SlateCommandID.newFromTemplate: return KeyboardShortcut(\"n\", modifiers: [.command, .shift])",
            "case SlateCommandID.renameEntry: return KeyboardShortcut(\"r\", modifiers: [.command, .option])",
            "case SlateCommandID.moveTo: return KeyboardShortcut(\"m\", modifiers: [.command, .shift])",
        ] {
            XCTAssertTrue(shortcutBody.contains(mapping), "preserve exact ID chord: \(mapping)")
        }
        XCTAssertEqual(occurrences(of: "case SlateCommandID.", in: shortcutBody), 4)
        XCTAssertTrue(shortcutBody.contains("default: return nil"))
    }

    func testReturnAndCommandDownReachTheSameCapturedSharedOpenIntent() throws {
        let selection = snapshot([item("Note.md")], focusedPath: "Note.md")
        let keyboardProjection = SidebarActionCatalog.project(
            surface: .keyboard, snapshot: selection)
        var captured: [SidebarActionInvocationIntent] = []

        func invokeCapturedOpen() throws {
            XCTAssertTrue(
                try FileTreeSidebar.invokeSidebarKeyboardAction(
                    id: SlateCommandID.sidebarOpen,
                    projection: keyboardProjection,
                    dispatch: { captured.append($0) }))
        }

        XCTAssertEqual(
            FileTreeSidebar.selectionKeyAction(
                key: .return,
                modifiers: [],
                fileTreeFocused: true,
                isRenaming: false),
            .openSelected)
        try invokeCapturedOpen()

        let commandDownModifiers: NSEvent.ModifierFlags = [
            .command, .function, .numericPad,
        ]
        XCTAssertEqual(
            TreeOpenSelectedKey.disposition(
                keyCode: 125,
                modifierFlags: commandDownModifiers,
                isRepeat: false,
                fileTreeFocused: true,
                isRenaming: false),
            .open)
        try invokeCapturedOpen()

        XCTAssertEqual(captured.map(\.actionID), [
            SlateCommandID.sidebarOpen, SlateCommandID.sidebarOpen,
        ])
        XCTAssertTrue(captured.allSatisfy { $0.snapshot == selection })
    }

    func testKeyboardUnavailableActionThrowsExactReasonWithoutDispatch() throws {
        let mixed = snapshot(
            [item("Note.md"), item("Folder", directory: true)],
            focusedPath: "Folder",
            creationParent: "Folder")
        let projection = SidebarActionCatalog.project(
            surface: .keyboard, snapshot: mixed)
        let open = try XCTUnwrap(
            projection.first { $0.id == SlateCommandID.sidebarOpen })
        XCTAssertEqual(open.disabledReason, "Open is available only for files.")
        XCTAssertNil(open.intent)

        var dispatchCount = 0
        XCTAssertThrowsError(
            try FileTreeSidebar.invokeSidebarKeyboardAction(
                id: SlateCommandID.sidebarOpen,
                projection: projection,
                dispatch: { _ in dispatchCount += 1 })
        ) { error in
            guard case CommandError.ActionFailed(message: let message) = error else {
                return XCTFail("expected ActionFailed, got \(error)")
            }
            XCTAssertEqual(message, "Open is available only for files.")
        }
        XCTAssertEqual(dispatchCount, 0)
    }

    // MARK: - Source ownership helpers

    private enum SourceExtractionError: Error, CustomStringConvertible {
        case missing(String)
        case ambiguous(String, Int)
        case unbalanced(String)

        var description: String {
            switch self {
            case .missing(let owner): return "missing semantic source owner: \(owner)"
            case .ambiguous(let owner, let count):
                return "ambiguous semantic source owner: \(owner) matched \(count) times"
            case .unbalanced(let owner): return "unbalanced semantic source owner: \(owner)"
            }
        }
    }

    private struct SourceRegion {
        let structural: String
        let literals: String
    }

    private func rawSource(_ name: String) throws -> String {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(
            contentsOf: packageRoot.appendingPathComponent("Sources/SlateMac/\(name)"),
            encoding: .utf8)
    }

    private func semanticSource(_ name: String) throws -> String {
        SwiftSourceStripping.strippingCommentsAndStrings(try rawSource(name))
            .split(whereSeparator: { $0.isWhitespace })
            .joined(separator: " ")
    }

    private func sourceRegion(_ name: String) throws -> SourceRegion {
        let raw = try rawSource(name)
        let structural = SwiftSourceStripping.strippingCommentsAndStrings(raw)
        let literals = strippingCommentsPreservingStrings(raw)
        guard structural.count == literals.count else {
            throw SourceExtractionError.unbalanced("paired source views for \(name)")
        }
        return SourceRegion(structural: structural, literals: literals)
    }

    private func functionBody(
        named name: String,
        signatureContaining signatureFragment: String? = nil,
        in source: String
    ) throws -> Substring {
        let anchor = "func \(name)("
        let candidates = ranges(of: anchor, in: source).filter { range in
            guard let signatureFragment else { return true }
            let tail = source[range.lowerBound...]
            guard let brace = tail.firstIndex(of: "{") else { return false }
            return tail[..<brace].contains(signatureFragment)
        }
        let start = try uniqueRange(candidates, description: "func \(name)")
        let tail = source[start.lowerBound...]
        guard let openBrace = tail.firstIndex(of: "{") else {
            throw SourceExtractionError.unbalanced("func \(name)")
        }
        return try bracedBody(
            startingAt: openBrace, in: tail, description: "func \(name)")
    }

    private func typeBody(named name: String, in source: String) throws -> Substring {
        let candidates = ranges(of: "struct \(name)", in: source)
        let start = try uniqueRange(candidates, description: "struct \(name)")
        let tail = source[start.lowerBound...]
        guard let openBrace = tail.firstIndex(of: "{") else {
            throw SourceExtractionError.unbalanced("struct \(name)")
        }
        return try bracedBody(
            startingAt: openBrace, in: tail, description: "struct \(name)")
    }

    private func blockBody(
        anchoredBy anchor: String,
        in source: String
    ) throws -> Substring {
        let start = try uniqueRange(ranges(of: anchor, in: source), description: anchor)
        let tail = source[start.lowerBound...]
        guard let openBrace = tail.firstIndex(of: "{") else {
            throw SourceExtractionError.unbalanced(anchor)
        }
        return try bracedBody(startingAt: openBrace, in: tail, description: anchor)
    }

    private func computedPropertyBody(
        named name: String,
        in source: String
    ) throws -> Substring {
        let anchor = "var \(name)"
        let start = try uniqueRange(ranges(of: anchor, in: source), description: anchor)
        let tail = source[start.lowerBound...]
        guard let openBrace = tail.firstIndex(of: "{") else {
            throw SourceExtractionError.unbalanced(anchor)
        }
        return try bracedBody(startingAt: openBrace, in: tail, description: anchor)
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
                if depth == 0 { return source[openBrace...index] }
            default: break
            }
        }
        throw SourceExtractionError.unbalanced(description)
    }

    private func pairedBlockBody(
        structuralAnchor anchor: String,
        in source: SourceRegion
    ) throws -> SourceRegion {
        try pairedBlockBody(anchor: anchor, useLiterals: false, in: source)
    }

    private func pairedBlockBody(
        literalAnchor anchor: String,
        in source: SourceRegion
    ) throws -> SourceRegion {
        try pairedBlockBody(anchor: anchor, useLiterals: true, in: source)
    }

    private func pairedBlockBody(
        anchor: String,
        useLiterals: Bool,
        in source: SourceRegion
    ) throws -> SourceRegion {
        let searchSource = useLiterals ? source.literals : source.structural
        let match = try uniqueRange(
            ranges(of: anchor, in: searchSource), description: anchor)
        let startOffset = searchSource.distance(
            from: searchSource.startIndex, to: match.lowerBound)
        let structuralStart = source.structural.index(
            source.structural.startIndex, offsetBy: startOffset)
        guard let openBrace = source.structural[structuralStart...].firstIndex(of: "{") else {
            throw SourceExtractionError.unbalanced(anchor)
        }
        let structuralTail = source.structural[openBrace...]
        let body = try bracedBody(
            startingAt: openBrace, in: structuralTail, description: anchor)
        let lowerOffset = source.structural.distance(
            from: source.structural.startIndex, to: body.startIndex)
        let upperOffset = source.structural.distance(
            from: source.structural.startIndex, to: body.endIndex)
        return pairedRegion(
            lowerOffset: lowerOffset, upperOffset: upperOffset, in: source)
    }

    private func pairedRegion(
        lowerOffset: Int,
        upperOffset: Int,
        in source: SourceRegion
    ) -> SourceRegion {
        func slice(_ text: String) -> String {
            let lower = text.index(text.startIndex, offsetBy: lowerOffset)
            let upper = text.index(text.startIndex, offsetBy: upperOffset)
            return String(text[lower..<upper])
        }
        return SourceRegion(
            structural: slice(source.structural),
            literals: slice(source.literals))
    }

    private func ranges(of needle: String, in source: String) -> [Range<String.Index>] {
        guard !needle.isEmpty else { return [] }
        var result: [Range<String.Index>] = []
        var cursor = source.startIndex
        while let range = source.range(of: needle, range: cursor..<source.endIndex) {
            result.append(range)
            cursor = range.upperBound
        }
        return result
    }

    private func uniqueRange(
        _ ranges: [Range<String.Index>],
        description: String
    ) throws -> Range<String.Index> {
        guard let only = ranges.first else {
            throw SourceExtractionError.missing(description)
        }
        guard ranges.count == 1 else {
            throw SourceExtractionError.ambiguous(description, ranges.count)
        }
        return only
    }

    private func strippingCommentsPreservingStrings(_ source: String) -> String {
        enum State {
            case code
            case lineComment
            case blockComment(Int)
            case string
        }
        let characters = Array(source)
        var output = ""
        output.reserveCapacity(source.utf8.count)
        var state = State.code
        var index = 0

        func blank(_ character: Character) -> Character {
            character == "\n" || character == "\r" ? character : " "
        }

        while index < characters.count {
            let character = characters[index]
            let next = index + 1 < characters.count ? characters[index + 1] : nil
            switch state {
            case .code:
                if character == "/", next == "/" {
                    output.append("  ")
                    state = .lineComment
                    index += 2
                } else if character == "/", next == "*" {
                    output.append("  ")
                    state = .blockComment(1)
                    index += 2
                } else {
                    output.append(character)
                    if character == "\"" { state = .string }
                    index += 1
                }
            case .lineComment:
                output.append(blank(character))
                if character == "\n" { state = .code }
                index += 1
            case .blockComment(let depth):
                if character == "/", next == "*" {
                    output.append("  ")
                    state = .blockComment(depth + 1)
                    index += 2
                } else if character == "*", next == "/" {
                    output.append("  ")
                    state = depth == 1 ? .code : .blockComment(depth - 1)
                    index += 2
                } else {
                    output.append(blank(character))
                    index += 1
                }
            case .string:
                output.append(character)
                if character == "\\", let next {
                    output.append(next)
                    index += 2
                } else {
                    if character == "\"" { state = .code }
                    index += 1
                }
            }
        }
        return output
    }

    private func normalized(_ source: String) -> String {
        source.split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")
    }

    private func assertClosedCallExpressions(
        owner: String,
        body: some StringProtocol,
        allowedExact: Set<String>,
        allowedTerminal: Set<String> = []
    ) throws {
        let source = String(body)
        let matches = try regexMatches(
            #"(?<![A-Za-z0-9_])([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*\("#,
            in: source)
        let languageForms: Set<String> = [
            "if", "while", "switch", "guard", "for", "catch", "return",
        ]
        let calls = matches.compactMap { match -> String? in
            guard let range = Range(match.range(at: 1), in: source) else { return nil }
            return String(source[range])
        }.filter { !languageForms.contains($0) }
        let unexpected = calls.filter { call in
            guard !allowedExact.contains(call) else { return false }
            guard let terminal = call.split(separator: ".").last.map(String.init) else {
                return true
            }
            return !allowedTerminal.contains(terminal)
        }
        XCTAssertEqual(
            unexpected,
            [],
            "\(owner) introduces an unscanned render-time call expression: \(unexpected)")
        for required in allowedExact {
            XCTAssertEqual(
                calls.filter { $0 == required }.count,
                1,
                "\(owner) must call \(required) exactly once")
        }
    }

    private func regexMatches(
        _ pattern: String,
        in source: String
    ) throws -> [NSTextCheckingResult] {
        let expression = try NSRegularExpression(pattern: pattern)
        return expression.matches(
            in: source,
            range: NSRange(source.startIndex..<source.endIndex, in: source))
    }

    private func occurrences(of needle: String, in haystack: some StringProtocol) -> Int {
        guard !needle.isEmpty else { return 0 }
        var count = 0
        var cursor = haystack.startIndex
        while let range = haystack.range(of: needle, range: cursor..<haystack.endIndex) {
            count += 1
            cursor = range.upperBound
        }
        return count
    }
}
