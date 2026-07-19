// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

final class SidebarActionCatalogTests: XCTestCase {
    private let session = NSObject()

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

    func testCatalogHasExactStableOrderLabelsSymbolsAndSidebarSection() {
        XCTAssertEqual(
            SidebarActionCatalog.actions.map(\.id),
            [
                "slate.sidebar.open",
                "slate.file.newNote",
                "slate.file.newFolder",
                "slate.file.newFromTemplate",
                "slate.file.importFilesAndFolders",
                "slate.file.rename",
                "slate.file.moveTo",
                "slate.file.duplicate",
                "slate.file.revealInFinder",
                "slate.file.copyPath",
                "slate.sidebar.copyWikilink",
                "slate.sidebar.pinNote",
                "slate.sidebar.unpinNote",
                "slate.sidebar.unpinAllInFolder",
                "slate.sidebar.sortNameAsc",
                "slate.sidebar.sortNameDesc",
                "slate.sidebar.sortCreatedDesc",
                "slate.sidebar.sortCreatedAsc",
                "slate.sidebar.sortModifiedDesc",
                "slate.sidebar.sortModifiedAsc",
                "slate.sidebar.toggleDateGrouping",
                "slate.sidebar.useVaultDefaultSort",
                "slate.sidebar.addShortcut",
                "slate.sidebar.removeShortcut",
                "slate.sidebar.clearRecents",
                "slate.sidebar.collapseAll",
                "slate.sidebar.expandLoaded",
                "slate.sidebar.historyBack",
                "slate.sidebar.historyForward",
                "slate.sidebar.openShortcut1",
                "slate.sidebar.openShortcut2",
                "slate.sidebar.openShortcut3",
                "slate.sidebar.openShortcut4",
                "slate.sidebar.openShortcut5",
                "slate.sidebar.openShortcut6",
                "slate.sidebar.openShortcut7",
                "slate.sidebar.openShortcut8",
                "slate.sidebar.openShortcut9",
                "slate.sidebar.focusFilter",
                "slate.sidebar.addTag",
                "slate.sidebar.removeTag",
                "slate.file.delete",
            ])
        XCTAssertEqual(
            SidebarActionCatalog.actions.map(\.label),
            [
                "Open", "New Note", "New Folder", "New Note from Template…",
                "Import Files and Folders…",
                "Rename…", "Move To…", "Duplicate", "Reveal in Finder",
                "Copy Path", "Copy Wikilink",
                "Pin to Top of Folder", "Unpin", "Unpin All in Folder",
                "Sort by Name (A to Z)", "Sort by Name (Z to A)",
                "Sort by Created (Newest First)", "Sort by Created (Oldest First)",
                "Sort by Modified (Newest First)", "Sort by Modified (Oldest First)",
                "Group by Date", "Use Vault Default Sort",
                "Add to Shortcuts", "Remove from Shortcuts", "Clear Recents",
                "Collapse All Folders", "Expand Loaded Folders",
                "Back in Sidebar History", "Forward in Sidebar History",
                "Open Shortcut 1", "Open Shortcut 2", "Open Shortcut 3",
                "Open Shortcut 4", "Open Shortcut 5", "Open Shortcut 6",
                "Open Shortcut 7", "Open Shortcut 8", "Open Shortcut 9",
                "Focus Sidebar Filter",
                "Add Tag…", "Remove Tag…",
                "Move to Trash",
            ])
        XCTAssertEqual(
            SidebarActionCatalog.actions.map(\.symbol),
            [
                .open, .newNote, .newFolder, .newFromTemplate,
                .importFilesAndFolders, .rename, .moveTo,
                .duplicate, .revealInFinder, .copyPath, .copyWikilink,
                .pin, .unpin, .unpin,
                .sortOrder, .sortOrder, .sortOrder, .sortOrder, .sortOrder,
                .sortOrder, .dateGrouping, .sortOrder,
                .pin, .unpin, .unpin,
                .sortOrder, .sortOrder, .sortOrder, .sortOrder,
                .pin, .pin, .pin, .pin, .pin, .pin, .pin, .pin, .pin,
                .search,
                .pin, .unpin,
                .trash,
            ])
        XCTAssertEqual(
            Set(SidebarActionCatalog.actions.map(\.id)).count,
            SidebarActionCatalog.actions.count)
        XCTAssertTrue(SidebarActionCatalog.actions.allSatisfy { $0.section == .sidebar })
    }

    func testImportFilesAndFoldersIsAFileCreationAction() throws {
        let definition = try XCTUnwrap(
            SidebarActionCatalog.actions.first {
                $0.id == "slate.file.importFilesAndFolders"
            })

        XCTAssertEqual(definition.label, "Import Files and Folders…")
        XCTAssertEqual(definition.section, .sidebar)
        XCTAssertEqual(definition.capability, .zeroOrOneItem)
        XCTAssertTrue(definition.blocksDuringStructuralMutation)
        XCTAssertEqual(definition.undoBehavior, .runtimeDetermined)
        XCTAssertFalse(definition.isDestructive)
        XCTAssertEqual(
            definition.accessibilityHint,
            "Choose files and folders. External items are copied into the selected location; items already in this vault are moved.")
        XCTAssertFalse(definition.accessibilityHint.localizedCaseInsensitiveContains("undo"))
        XCTAssertFalse(definition.accessibilityHint.localizedCaseInsensitiveContains("rollback"))
    }

    func testImportFilesAndFoldersAvailabilityAndSurfaceParityAreDeterministic() throws {
        let id = "slate.file.importFilesAndFolders"
        let busy = "Another file operation is still running."
        let noSelection = snapshot([])
        let oneFile = snapshot(
            [item("Folder/Note.md")],
            focusedPath: "Folder/Note.md",
            creationParent: "Folder")
        let oneFolder = snapshot(
            [item("Folder", directory: true)],
            focusedPath: "Folder",
            creationParent: "Folder")
        let many = snapshot(
            [item("A.md"), item("B.md")],
            focusedPath: "B.md")

        let noVault = try XCTUnwrap(
            SidebarActionCatalog.evaluation(for: id, snapshot: nil))
        XCTAssertEqual(noVault.disabledReason, SidebarActionCatalog.noVaultReason)
        XCTAssertNil(noVault.intent)

        for available in [noSelection, oneFile, oneFolder] {
            let evaluation = try XCTUnwrap(
                SidebarActionCatalog.evaluation(for: id, snapshot: available))
            XCTAssertNil(evaluation.disabledReason)
            XCTAssertEqual(evaluation.intent?.snapshot, available)
        }

        let ambiguous = try XCTUnwrap(
            SidebarActionCatalog.evaluation(for: id, snapshot: many))
        XCTAssertEqual(
            ambiguous.disabledReason,
            "Select no more than one item to choose an import location.")
        XCTAssertNil(ambiguous.intent)

        let blocked = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: id,
                snapshot: oneFolder,
                structuralMutationDisabledReason: busy))
        XCTAssertEqual(blocked.disabledReason, busy)
        XCTAssertNil(blocked.intent)

        let menu = try XCTUnwrap(
            SidebarActionCatalog.project(
                surface: .menuBar,
                snapshot: many,
                structuralMutationDisabledReason: busy
            ).first { $0.id == id })
        let palette = try XCTUnwrap(
            SidebarActionCatalog.project(
                surface: .commandPalette,
                snapshot: many,
                structuralMutationDisabledReason: busy
            ).first { $0.id == id })
        XCTAssertEqual(menu, palette)
        XCTAssertEqual(menu.label, "Import Files and Folders…")
    }

    func testCapabilityMatrixIsExactForSelectionShapes() {
        let ids = SidebarActionCatalog.structurallyApplicableActionIDs
        let empty = snapshot([])
        let markdown = snapshot([item("Note.md")], focusedPath: "Note.md")
        let nonMarkdown = snapshot(
            [item("Diagram.canvas", markdown: false)], focusedPath: "Diagram.canvas")
        let folder = snapshot(
            [item("Folder", directory: true)], focusedPath: "Folder", creationParent: "Folder")
        let files = snapshot(
            [item("A.md"), item("B.md")], focusedPath: "B.md")
        let mixed = snapshot(
            [item("A.md"), item("Folder", directory: true)], focusedPath: "Folder")
        let folders = snapshot(
            [item("A", directory: true), item("B", directory: true)], focusedPath: "B")

        let organizationLocationIDs: [String] = [
            SlateCommandID.sidebarSortNameAsc, SlateCommandID.sidebarSortNameDesc,
            SlateCommandID.sidebarSortCreatedDesc, SlateCommandID.sidebarSortCreatedAsc,
            SlateCommandID.sidebarSortModifiedDesc, SlateCommandID.sidebarSortModifiedAsc,
            SlateCommandID.sidebarToggleDateGrouping,
            SlateCommandID.sidebarUseVaultDefaultSort,
        ]
        let navigationLocationIDs: [String] =
            [
                SlateCommandID.sidebarClearRecents,
                SlateCommandID.sidebarCollapseAll,
                SlateCommandID.sidebarExpandLoaded,
                SlateCommandID.sidebarHistoryBack,
                SlateCommandID.sidebarHistoryForward,
            ] + SlateCommandID.sidebarOpenShortcutSlots
            + [SlateCommandID.sidebarFocusFilter]
        XCTAssertEqual(
            ids(empty),
            [
                SlateCommandID.newNote, SlateCommandID.newFolder,
                SlateCommandID.newFromTemplate, SlateCommandID.importFilesAndFolders,
            ] + organizationLocationIDs + navigationLocationIDs)
        // A single file takes every verb except the folder-only Unpin All.
        XCTAssertEqual(
            ids(markdown),
            SidebarActionCatalog.actions.map(\.id).filter {
                $0 != SlateCommandID.sidebarUnpinAll
            })
        XCTAssertEqual(
            ids(nonMarkdown),
            SidebarActionCatalog.actions.map(\.id).filter {
                $0 != SlateCommandID.sidebarCopyWikilink
                    && $0 != SlateCommandID.sidebarUnpinAll
            })
        XCTAssertEqual(
            ids(folder),
            [
                SlateCommandID.newNote, SlateCommandID.newFolder,
                SlateCommandID.newFromTemplate, SlateCommandID.importFilesAndFolders,
                SlateCommandID.renameEntry,
                SlateCommandID.moveTo, SlateCommandID.revealInFinder,
                SlateCommandID.copyPath, SlateCommandID.sidebarUnpinAll,
            ] + organizationLocationIDs + [
                SlateCommandID.sidebarAddShortcut,
                SlateCommandID.sidebarRemoveShortcut,
            ] + navigationLocationIDs + [SlateCommandID.deleteEntry])
        XCTAssertEqual(
            ids(files),
            [
                SlateCommandID.sidebarOpen, SlateCommandID.moveTo,
            ] + navigationLocationIDs + [
                SlateCommandID.sidebarAddTag,
                SlateCommandID.sidebarRemoveTag,
                SlateCommandID.deleteEntry,
            ])
        // Mixed selections exclude the tag editors: `.oneOrMoreFiles`
        // is ALL-files (the Open convention) — the menu shows the
        // files-only reason instead of running a batch guaranteed to
        // skip its folders.
        XCTAssertEqual(
            ids(mixed),
            [
                SlateCommandID.moveTo
            ] + navigationLocationIDs + [
                SlateCommandID.deleteEntry,
            ])
        XCTAssertEqual(
            ids(folders),
            [
                SlateCommandID.moveTo
            ] + navigationLocationIDs + [
                SlateCommandID.deleteEntry,
            ])
    }

    func testDisabledReasonPrecedenceIsVaultThenCapabilityThenTemporaryState() throws {
        let busy = "Another file operation is still running."
        let noVault = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.duplicateEntry,
                snapshot: nil,
                structuralMutationDisabledReason: busy))
        XCTAssertEqual(noVault.disabledReason, SidebarActionCatalog.noVaultReason)

        let folder = snapshot([item("Folder", directory: true)], focusedPath: "Folder")
        let wrongCapability = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.duplicateEntry,
                snapshot: folder,
                structuralMutationDisabledReason: busy))
        XCTAssertEqual(wrongCapability.disabledReason, "Select exactly one file to duplicate.")

        let file = snapshot([item("Note.md")], focusedPath: "Note.md")
        let temporarilyBlocked = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.duplicateEntry,
                snapshot: file,
                structuralMutationDisabledReason: busy))
        XCTAssertEqual(temporarilyBlocked.disabledReason, busy)
        XCTAssertNil(temporarilyBlocked.intent)

        let inspection = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.copyPath,
                snapshot: file,
                structuralMutationDisabledReason: busy))
        XCTAssertNil(inspection.disabledReason, "inspection remains available during a mutation")
        XCTAssertEqual(inspection.intent?.snapshot, file)

        let contendedInspection = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.sidebarCopyWikilink,
                snapshot: file,
                structuralMutationDisabledReason: busy))
        XCTAssertEqual(
            contendedInspection.disabledReason,
            busy,
            "Copy Wikilink must not synchronously wait on the native writer lock")
        XCTAssertNil(contendedInspection.intent)
    }

    func testEveryMutationHasHonestUndoAndHistoryMetadata() throws {
        let blocked = SidebarActionCatalog.actions.filter(\.blocksDuringStructuralMutation)
        XCTAssertEqual(
            blocked.map(\.id),
            [
                SlateCommandID.newNote, SlateCommandID.newFolder,
                SlateCommandID.newFromTemplate, SlateCommandID.importFilesAndFolders,
                SlateCommandID.renameEntry,
                SlateCommandID.moveTo, SlateCommandID.duplicateEntry,
                SlateCommandID.sidebarCopyWikilink, SlateCommandID.deleteEntry,
            ])
        XCTAssertEqual(
            blocked.map(\.undoBehavior),
            [
                .historyBarrier, .historyBarrier, .historyBarrier,
                .runtimeDetermined, .slateUndo, .slateUndo, .historyBarrier,
                .noChange, .notUndoable,
            ],
            "blocking during native writes does not make Copy Wikilink a mutation")
        XCTAssertEqual(blocked.filter(\.isDestructive).map(\.id), [SlateCommandID.deleteEntry])

        let trash = try XCTUnwrap(
            SidebarActionCatalog.actions.first { $0.id == SlateCommandID.deleteEntry })
        XCTAssertTrue(trash.isDestructive)
        XCTAssertEqual(trash.undoBehavior, .notUndoable)
        XCTAssertFalse(trash.label.localizedCaseInsensitiveContains("undo"))
        XCTAssertFalse(trash.accessibilityHint.localizedCaseInsensitiveContains("undo"))
        XCTAssertFalse(trash.accessibilityHint.localizedCaseInsensitiveContains("rollback"))
    }

    func testSurfaceProjectionUsesExactMembershipAndFrozenTemplateDestination() {
        let markdown = snapshot(
            [item("Folder/Note.md")],
            focusedPath: "Folder/Note.md",
            creationParent: "Folder")
        for surface in [SidebarActionSurface.menuBar, .commandPalette] {
            XCTAssertEqual(
                SidebarActionCatalog.project(surface: surface, snapshot: markdown).map(\.id),
                SidebarActionCatalog.actions.map(\.id))
        }
        XCTAssertEqual(
            SidebarActionCatalog.project(surface: .contextMenu, snapshot: markdown).map(\.id),
            [
                SlateCommandID.sidebarOpen, SlateCommandID.renameEntry,
                SlateCommandID.moveTo, SlateCommandID.duplicateEntry,
                SlateCommandID.revealInFinder, SlateCommandID.copyPath,
                SlateCommandID.sidebarCopyWikilink,
                SlateCommandID.sidebarPinNote, SlateCommandID.sidebarUnpinNote,
                SlateCommandID.sidebarAddShortcut,
                SlateCommandID.sidebarRemoveShortcut,
                SlateCommandID.deleteEntry,
            ])
        XCTAssertEqual(
            SidebarActionCatalog.project(surface: .voiceOver, snapshot: markdown).map(\.id),
            [
                SlateCommandID.renameEntry, SlateCommandID.moveTo,
                SlateCommandID.duplicateEntry, SlateCommandID.revealInFinder,
                SlateCommandID.copyPath, SlateCommandID.sidebarCopyWikilink,
                SlateCommandID.sidebarPinNote, SlateCommandID.sidebarUnpinNote,
                SlateCommandID.sidebarAddShortcut,
                SlateCommandID.sidebarRemoveShortcut,
                SlateCommandID.deleteEntry,
            ],
            "VoiceOver Open belongs only to the conditional default action")
        XCTAssertEqual(
            SidebarActionCatalog.project(surface: .toolbar, snapshot: markdown).map(\.id),
            [
                SlateCommandID.newFromTemplate,
                SlateCommandID.sidebarCollapseAll,
                SlateCommandID.sidebarExpandLoaded,
            ],
            "FL04-A adds no toolbar items")
        XCTAssertEqual(
            SidebarActionCatalog.project(surface: .keyboard, snapshot: markdown).map(\.id),
            [
                SlateCommandID.sidebarOpen, SlateCommandID.newNote,
                SlateCommandID.newFromTemplate, SlateCommandID.renameEntry,
                SlateCommandID.moveTo, SlateCommandID.deleteEntry,
            ],
            "copy actions gain neither toolbar items nor keyboard shortcuts")

        for surface in [
            SidebarActionSurface.menuBar, .commandPalette, .toolbar, .keyboard,
        ] {
            let intent = SidebarActionCatalog.project(surface: surface, snapshot: markdown)
                .first { $0.id == SlateCommandID.newFromTemplate }?.intent
            XCTAssertEqual(intent?.snapshot, markdown)
        }
    }

    func testContextualMembershipMatrixIsExactAndConcise() {
        let markdown = snapshot([item("Note.md")], focusedPath: "Note.md")
        let nonMarkdown = snapshot(
            [item("Diagram.canvas", markdown: false)], focusedPath: "Diagram.canvas")
        let folder = snapshot(
            [item("Folder", directory: true)],
            focusedPath: "Folder",
            creationParent: "Folder")
        let files = snapshot(
            [item("A.md"), item("B.md")], focusedPath: "B.md")
        let mixed = snapshot(
            [item("A.md"), item("Folder", directory: true)], focusedPath: "Folder")
        let folders = snapshot(
            [item("A", directory: true), item("B", directory: true)], focusedPath: "B")

        let contextCases: [(SidebarSelectionSnapshot, [String])] = [
            (
                markdown,
                [
                    SlateCommandID.sidebarOpen, SlateCommandID.renameEntry,
                    SlateCommandID.moveTo, SlateCommandID.duplicateEntry,
                    SlateCommandID.revealInFinder, SlateCommandID.copyPath,
                    SlateCommandID.sidebarCopyWikilink,
                    SlateCommandID.sidebarPinNote, SlateCommandID.sidebarUnpinNote,
                    SlateCommandID.sidebarAddShortcut,
                    SlateCommandID.sidebarRemoveShortcut,
                    SlateCommandID.deleteEntry,
                ]
            ),
            (
                nonMarkdown,
                [
                    SlateCommandID.sidebarOpen, SlateCommandID.renameEntry,
                    SlateCommandID.moveTo, SlateCommandID.duplicateEntry,
                    SlateCommandID.revealInFinder, SlateCommandID.copyPath,
                    SlateCommandID.sidebarPinNote, SlateCommandID.sidebarUnpinNote,
                    SlateCommandID.sidebarAddShortcut,
                    SlateCommandID.sidebarRemoveShortcut,
                    SlateCommandID.deleteEntry,
                ]
            ),
            (
                folder,
                [
                    SlateCommandID.newNote, SlateCommandID.newFolder,
                    SlateCommandID.newFromTemplate, SlateCommandID.renameEntry,
                    SlateCommandID.moveTo,
                    SlateCommandID.revealInFinder, SlateCommandID.copyPath,
                    SlateCommandID.sidebarUnpinAll,
                    SlateCommandID.sidebarSortNameAsc,
                    SlateCommandID.sidebarSortNameDesc,
                    SlateCommandID.sidebarSortCreatedDesc,
                    SlateCommandID.sidebarSortCreatedAsc,
                    SlateCommandID.sidebarSortModifiedDesc,
                    SlateCommandID.sidebarSortModifiedAsc,
                    SlateCommandID.sidebarToggleDateGrouping,
                    SlateCommandID.sidebarUseVaultDefaultSort,
                    SlateCommandID.sidebarAddShortcut,
                    SlateCommandID.sidebarRemoveShortcut,
                    SlateCommandID.deleteEntry,
                ]
            ),
            (
                files,
                [
                    SlateCommandID.sidebarOpen, SlateCommandID.moveTo,
                    SlateCommandID.deleteEntry,
                ]
            ),
            (mixed, [SlateCommandID.moveTo, SlateCommandID.deleteEntry]),
            (folders, [SlateCommandID.moveTo, SlateCommandID.deleteEntry]),
        ]
        for (selection, expected) in contextCases {
            XCTAssertEqual(
                SidebarActionCatalog.project(
                    surface: .contextMenu, snapshot: selection
                ).map(\.id),
                expected)
            XCTAssertEqual(
                SidebarActionCatalog.project(
                    surface: .voiceOver, snapshot: selection
                ).map(\.id),
                expected.filter {
                    $0 != SlateCommandID.sidebarOpen
                        && $0 != SlateCommandID.sidebarSortNameAsc
                        && $0 != SlateCommandID.sidebarSortNameDesc
                        && $0 != SlateCommandID.sidebarSortCreatedDesc
                        && $0 != SlateCommandID.sidebarSortCreatedAsc
                        && $0 != SlateCommandID.sidebarSortModifiedDesc
                        && $0 != SlateCommandID.sidebarSortModifiedAsc
                        && $0 != SlateCommandID.sidebarToggleDateGrouping
                        && $0 != SlateCommandID.sidebarUseVaultDefaultSort
                },
                "VoiceOver is the concise matrix minus default-owned Open and the sort radio set")
        }
        XCTAssertEqual(
            contextCases.filter { $0.1.contains(SlateCommandID.newFromTemplate) }.count,
            1,
            "Template is concise and contextual: a single folder row only")
    }

    func testUnavailableProjectionKeepsFullInventoryOnlyForMenuAndPalette() {
        let folder = snapshot([item("Folder", directory: true)], focusedPath: "Folder")
        let busy = "Another file operation is still running."

        for surface in [SidebarActionSurface.menuBar, .commandPalette] {
            let projected = SidebarActionCatalog.project(
                surface: surface,
                snapshot: folder,
                structuralMutationDisabledReason: busy)
            XCTAssertEqual(projected.map(\.id), SidebarActionCatalog.actions.map(\.id))
            XCTAssertEqual(
                projected.first { $0.id == SlateCommandID.duplicateEntry }?.disabledReason,
                "Select exactly one file to duplicate.")
            XCTAssertEqual(
                projected.first { $0.id == SlateCommandID.renameEntry }?.disabledReason,
                busy)
        }

        for surface in [SidebarActionSurface.contextMenu, .voiceOver] {
            let projected = SidebarActionCatalog.project(
                surface: surface,
                snapshot: folder,
                structuralMutationDisabledReason: busy)
            let expected: [String]
            if surface == .contextMenu {
                expected = [
                    SlateCommandID.revealInFinder, SlateCommandID.copyPath,
                    SlateCommandID.sidebarUnpinAll,
                    SlateCommandID.sidebarSortNameAsc,
                    SlateCommandID.sidebarSortNameDesc,
                    SlateCommandID.sidebarSortCreatedDesc,
                    SlateCommandID.sidebarSortCreatedAsc,
                    SlateCommandID.sidebarSortModifiedDesc,
                    SlateCommandID.sidebarSortModifiedAsc,
                    SlateCommandID.sidebarToggleDateGrouping,
                    SlateCommandID.sidebarUseVaultDefaultSort,
                    SlateCommandID.sidebarAddShortcut,
                    SlateCommandID.sidebarRemoveShortcut,
                ]
            } else {
                expected = [
                    SlateCommandID.revealInFinder, SlateCommandID.copyPath,
                    SlateCommandID.sidebarUnpinAll,
                    SlateCommandID.sidebarAddShortcut,
                    SlateCommandID.sidebarRemoveShortcut,
                ]
            }
            XCTAssertEqual(
                projected.map(\.id),
                expected,
                "context and VoiceOver omit structural and temporary unavailability; preference edits never block on the structural gate")
            XCTAssertTrue(projected.allSatisfy { $0.disabledReason == nil })
        }
    }

    func testTemplateCapabilityRejectsAmbiguousMultiSelection() throws {
        let template = try XCTUnwrap(
            SidebarActionCatalog.actions.first {
                $0.id == SlateCommandID.newFromTemplate
            })
        XCTAssertEqual(template.capability, .zeroOrOneItem)

        let selection = snapshot(
            [item("A.md"), item("Folder", directory: true)],
            focusedPath: "Folder",
            creationParent: "Folder")
        let evaluation = try XCTUnwrap(
            SidebarActionCatalog.evaluation(
                for: SlateCommandID.newFromTemplate,
                snapshot: selection))
        XCTAssertEqual(
            evaluation.disabledReason,
            "Select no more than one item to choose a creation location.")
        XCTAssertNil(evaluation.intent)
    }
}
