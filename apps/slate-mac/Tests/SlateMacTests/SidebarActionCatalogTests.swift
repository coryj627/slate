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
                "slate.file.rename",
                "slate.file.moveTo",
                "slate.file.duplicate",
                "slate.file.revealInFinder",
                "slate.file.copyPath",
                "slate.sidebar.copyWikilink",
                "slate.file.delete",
            ])
        XCTAssertEqual(
            SidebarActionCatalog.actions.map(\.label),
            [
                "Open", "New Note", "New Folder", "New Note from Template…",
                "Rename…", "Move To…", "Duplicate", "Reveal in Finder",
                "Copy Path", "Copy Wikilink", "Move to Trash",
            ])
        XCTAssertEqual(
            SidebarActionCatalog.actions.map(\.symbol),
            [
                .open, .newNote, .newFolder, .newFromTemplate, .rename, .moveTo,
                .duplicate, .revealInFinder, .copyPath, .copyWikilink, .trash,
            ])
        XCTAssertEqual(
            Set(SidebarActionCatalog.actions.map(\.id)).count,
            SidebarActionCatalog.actions.count)
        XCTAssertTrue(SidebarActionCatalog.actions.allSatisfy { $0.section == .sidebar })
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

        XCTAssertEqual(ids(empty), [
            SlateCommandID.newNote, SlateCommandID.newFolder, SlateCommandID.newFromTemplate,
        ])
        XCTAssertEqual(ids(markdown), SidebarActionCatalog.actions.map(\.id))
        XCTAssertEqual(
            ids(nonMarkdown),
            SidebarActionCatalog.actions.map(\.id).filter {
                $0 != SlateCommandID.sidebarCopyWikilink
            })
        XCTAssertEqual(
            ids(folder),
            [
                SlateCommandID.newNote, SlateCommandID.newFolder,
                SlateCommandID.newFromTemplate, SlateCommandID.renameEntry,
                SlateCommandID.moveTo, SlateCommandID.revealInFinder,
                SlateCommandID.copyPath, SlateCommandID.deleteEntry,
            ])
        XCTAssertEqual(
            ids(files),
            [
                SlateCommandID.sidebarOpen, SlateCommandID.newFromTemplate,
                SlateCommandID.moveTo, SlateCommandID.deleteEntry,
            ])
        XCTAssertEqual(
            ids(mixed),
            [
                SlateCommandID.newFromTemplate, SlateCommandID.moveTo,
                SlateCommandID.deleteEntry,
            ])
        XCTAssertEqual(
            ids(folders),
            [
                SlateCommandID.newFromTemplate, SlateCommandID.moveTo,
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
                SlateCommandID.newFromTemplate, SlateCommandID.renameEntry,
                SlateCommandID.moveTo, SlateCommandID.duplicateEntry,
                SlateCommandID.sidebarCopyWikilink, SlateCommandID.deleteEntry,
            ])
        XCTAssertEqual(
            blocked.map(\.undoBehavior),
            [
                .historyBarrier, .historyBarrier, .historyBarrier,
                .slateUndo, .slateUndo, .historyBarrier, .noChange, .notUndoable,
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

    func testSurfaceProjectionUsesExactMembershipAndExplicitRootTemplateIntent() {
        let markdown = snapshot([item("Note.md")], focusedPath: "Note.md")
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
                SlateCommandID.sidebarCopyWikilink, SlateCommandID.deleteEntry,
            ])
        XCTAssertEqual(
            SidebarActionCatalog.project(surface: .voiceOver, snapshot: markdown).map(\.id),
            [
                SlateCommandID.renameEntry, SlateCommandID.moveTo,
                SlateCommandID.duplicateEntry, SlateCommandID.revealInFinder,
                SlateCommandID.copyPath, SlateCommandID.sidebarCopyWikilink,
                SlateCommandID.deleteEntry,
            ],
            "VoiceOver Open belongs only to the conditional default action")
        XCTAssertEqual(
            SidebarActionCatalog.project(surface: .toolbar, snapshot: markdown).map(\.id),
            [SlateCommandID.newFromTemplate],
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
            XCTAssertEqual(intent?.snapshot.items, [])
            XCTAssertEqual(intent?.snapshot.focusedPath, nil)
            XCTAssertEqual(intent?.snapshot.creationParent, "")
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
                    SlateCommandID.sidebarCopyWikilink, SlateCommandID.deleteEntry,
                ]
            ),
            (
                nonMarkdown,
                [
                    SlateCommandID.sidebarOpen, SlateCommandID.renameEntry,
                    SlateCommandID.moveTo, SlateCommandID.duplicateEntry,
                    SlateCommandID.revealInFinder, SlateCommandID.copyPath,
                    SlateCommandID.deleteEntry,
                ]
            ),
            (
                folder,
                [
                    SlateCommandID.newNote, SlateCommandID.newFolder,
                    SlateCommandID.renameEntry, SlateCommandID.moveTo,
                    SlateCommandID.revealInFinder, SlateCommandID.copyPath,
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
                expected.filter { $0 != SlateCommandID.sidebarOpen },
                "VoiceOver is the same concise matrix minus default-owned Open")
        }
        XCTAssertTrue(contextCases.allSatisfy { !$0.1.contains(SlateCommandID.newFromTemplate) })
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
            XCTAssertEqual(
                projected.map(\.id),
                [SlateCommandID.revealInFinder, SlateCommandID.copyPath],
                "context and VoiceOver omit structural, temporary, and pre-Task-4 template unavailability")
            XCTAssertTrue(projected.allSatisfy { $0.disabledReason == nil })
        }
    }

    func testFL04ATemplateBoundaryHasNoContextualAvailabilityEscapeHatch() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/SlateMac/Sidebar/SidebarActionCatalog.swift"),
            encoding: .utf8)
        XCTAssertFalse(source.contains("contextualTemplateAvailable"))
    }
}
