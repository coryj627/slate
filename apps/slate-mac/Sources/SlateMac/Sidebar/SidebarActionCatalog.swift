// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// One live row captured from the sidebar's visible-order selection.
struct SidebarSelectionItem: Equatable, Hashable {
    let path: String
    let isDirectory: Bool
    let isMarkdown: Bool
}

/// Immutable selection intent published by the tree and owned by `AppState`.
struct SidebarSelectionSnapshot: Equatable {
    let sessionIdentity: ObjectIdentifier
    let items: [SidebarSelectionItem]
    let focusedPath: String?
    let creationParent: String

    static func capture<Identity: Hashable>(
        sessionIdentity: ObjectIdentifier,
        model: SidebarSelectionModel<Identity>,
        visibleRows: [SidebarSelectionModel<Identity>.VisibleRow]
    ) -> Self {
        let selectedRows = model.selectedVisibleRows(in: visibleRows)
        let focusedRow = model.focused.flatMap { focus in
            selectedRows.first(where: { $0.identity == focus })
        }
        let creationParent: String
        if let focusedRow {
            creationParent = focusedRow.isDirectory
                ? focusedRow.path
                : parentPath(of: focusedRow.path)
        } else {
            creationParent = ""
        }
        return Self(
            sessionIdentity: sessionIdentity,
            items: selectedRows.map {
                SidebarSelectionItem(
                    path: $0.path,
                    isDirectory: $0.isDirectory,
                    isMarkdown: $0.isMarkdown)
            },
            focusedPath: focusedRow?.path,
            creationParent: creationParent)
    }

    private static func parentPath(of path: String) -> String {
        guard let separator = path.lastIndex(of: "/") else { return "" }
        return String(path[..<separator])
    }
}

enum SidebarActionCapability: Equatable {
    case oneOrMoreFiles
    case zeroOrOneItem
    case exactlyOneItem
    case oneOrMoreItems
    case exactlyOneFile
    case exactlyOneMarkdownFile
}

enum SidebarActionUndoBehavior: Equatable {
    case noChange
    /// Mutation succeeds but clears prior structural undo/redo history.
    case historyBarrier
    case slateUndo
    case notUndoable
}

enum SidebarActionSurface: Equatable {
    case menuBar
    case commandPalette
    case contextMenu
    case voiceOver
    case toolbar
    case keyboard

    fileprivate var retainsUnavailableActions: Bool {
        switch self {
        case .menuBar, .commandPalette, .toolbar, .keyboard:
            return true
        case .contextMenu, .voiceOver:
            return false
        }
    }
}

struct SidebarActionDefinition: Equatable {
    let id: String
    let label: String
    let symbol: SlateSymbol
    let section: CommandSection
    let capability: SidebarActionCapability
    let accessibilityHint: String
    let blocksDuringStructuralMutation: Bool
    let isDestructive: Bool
    let undoBehavior: SidebarActionUndoBehavior
}

/// A frozen request. AppState revalidates this capture before dispatching it.
struct SidebarActionInvocationIntent: Equatable {
    let actionID: String
    let snapshot: SidebarSelectionSnapshot
}

struct SidebarActionEvaluation: Equatable {
    let definition: SidebarActionDefinition
    let disabledReason: String?
    let intent: SidebarActionInvocationIntent?

    var id: String { definition.id }
    var label: String { definition.label }
    var symbol: SlateSymbol { definition.symbol }
}

/// AppState/catalog-level capture for the shared multi-open funnel. The tree
/// keeps compatibility forwarding helpers, but no longer owns these types.
struct SidebarOpenSelectionBatch: Equatable {
    let paths: [String]
    let focusedPath: String?

    var executionPaths: [String] {
        guard let focusedPath,
            let focusedIndex = paths.firstIndex(of: focusedPath)
        else { return paths }
        var result = paths
        result.remove(at: focusedIndex)
        result.append(focusedPath)
        return result
    }
}

struct SidebarOpenSelectionRequest: Equatable, Identifiable {
    let id: UUID
    let intent: SidebarActionInvocationIntent
    let batch: SidebarOpenSelectionBatch

    /// Shared-dispatcher form: the request retains the exact frozen Open
    /// intent, while its presentation/execution batch is derived from that
    /// same capture so the two cannot drift apart.
    init(
        id: UUID = UUID(),
        intent: SidebarActionInvocationIntent
    ) {
        self.id = id
        self.intent = intent
        self.batch = SidebarOpenSelectionBatch(
            paths: intent.snapshot.items.map(\.path),
            focusedPath: intent.snapshot.focusedPath)
    }

    /// Compatibility capture for the tree's shipped keyboard helper. The
    /// tree already admits files only, so this constructs the same complete
    /// frozen Open intent that the shared dispatcher supplies directly.
    init(
        id: UUID = UUID(),
        sessionIdentity: ObjectIdentifier,
        batch: SidebarOpenSelectionBatch
    ) {
        self.init(
            id: id,
            intent: SidebarActionInvocationIntent(
                actionID: SlateCommandID.sidebarOpen,
                snapshot: SidebarSelectionSnapshot(
                    sessionIdentity: sessionIdentity,
                    items: batch.paths.map { path in
                        SidebarSelectionItem(
                            path: path,
                            isDirectory: false,
                            isMarkdown:
                                (path as NSString).pathExtension
                                .caseInsensitiveCompare("md") == .orderedSame)
                    },
                    focusedPath: batch.focusedPath,
                    creationParent: "")))
    }

    var sessionIdentity: ObjectIdentifier { intent.snapshot.sessionIdentity }
    var paths: [String] { batch.paths }
    var focusedPath: String? { batch.focusedPath }
    var title: String { "Open \(paths.count) Files?" }
    var message: String { "This will open each selected file in a tab." }
}

/// AppState owns both the validation result and the confirmed Open effects.
/// Callers use this typed result only to react to the completed continuation;
/// they never receive paths to execute independently.
enum SidebarOpenConfirmationOutcome: Equatable {
    case opened([String])
    case rejected(String)
    case ignored
}

enum SidebarOpenSelectionDisposition: Equatable {
    case none
    case direct(SidebarOpenSelectionBatch)
    case confirm(SidebarOpenSelectionRequest)
}

enum SidebarPreparedCopy: Equatable {
    case path(String)
    case wikilink(String)
}

/// Typed result prevents Copy actions from being mistaken for completed Void
/// work. Task 3 replaces the temporary prepared-copy adapter with the final
/// pasteboard payload and announcement contract.
enum SidebarActionDispatchResult: Equatable {
    case completed(actionID: String)
    case opened([String])
    case openConfirmation(SidebarOpenSelectionRequest)
    case copyPrepared(SidebarPreparedCopy)
}

enum SidebarActionCatalog {
    typealias InvocationIntent = SidebarActionInvocationIntent

    static let noVaultReason = "Open a vault to use Sidebar actions."

    private static let toolbarActionIDs: Set<String> = [
        SlateCommandID.newFromTemplate
    ]
    private static let keyboardActionIDs: Set<String> = [
        SlateCommandID.sidebarOpen,
        SlateCommandID.newNote,
        SlateCommandID.newFromTemplate,
        SlateCommandID.renameEntry,
        SlateCommandID.moveTo,
        SlateCommandID.deleteEntry,
    ]

    static let actions: [SidebarActionDefinition] = [
        action(
            SlateCommandID.sidebarOpen, "Open", .open, .oneOrMoreFiles,
            "Open the selected files."),
        action(
            SlateCommandID.newNote, "New Note", .newNote, .zeroOrOneItem,
            "Create an untitled note in the selected location, then rename it.",
            mutation: true, undo: .historyBarrier),
        action(
            SlateCommandID.newFolder, "New Folder", .newFolder, .zeroOrOneItem,
            "Create a new folder in the selected location, then rename it.",
            mutation: true, undo: .historyBarrier),
        action(
            SlateCommandID.newFromTemplate, "New Note from Template…", .newFromTemplate,
            .zeroOrOneItem, "Choose a template for a new note.",
            mutation: true, undo: .historyBarrier),
        action(
            SlateCommandID.renameEntry, "Rename…", .rename, .exactlyOneItem,
            "Rename the selected file or folder in place.",
            mutation: true, undo: .slateUndo),
        action(
            SlateCommandID.moveTo, "Move To…", .moveTo, .oneOrMoreItems,
            "Move the selected files or folders to another folder.",
            mutation: true, undo: .slateUndo),
        action(
            SlateCommandID.duplicateEntry, "Duplicate", .duplicate, .exactlyOneFile,
            "Duplicate the selected file as a copy next to it.",
            mutation: true, undo: .historyBarrier),
        action(
            SlateCommandID.revealInFinder, "Reveal in Finder", .revealInFinder,
            .exactlyOneItem, "Show the selected file or folder in Finder."),
        action(
            SlateCommandID.copyPath, "Copy Path", .copyPath, .exactlyOneItem,
            "Copy the selected item's path."),
        action(
            SlateCommandID.sidebarCopyWikilink, "Copy Wikilink", .copyWikilink,
            .exactlyOneMarkdownFile, "Copy a wikilink to the selected Markdown file."),
        action(
            SlateCommandID.deleteEntry, "Move to Trash", .trash, .oneOrMoreItems,
            "Move the selected files or folders to the Trash.",
            mutation: true, destructive: true, undo: .notUndoable),
    ]

    private static func action(
        _ id: String,
        _ label: String,
        _ symbol: SlateSymbol,
        _ capability: SidebarActionCapability,
        _ hint: String,
        mutation: Bool = false,
        destructive: Bool = false,
        undo: SidebarActionUndoBehavior = .noChange
    ) -> SidebarActionDefinition {
        SidebarActionDefinition(
            id: id,
            label: label,
            symbol: symbol,
            section: .sidebar,
            capability: capability,
            accessibilityHint: hint,
            blocksDuringStructuralMutation: mutation,
            isDestructive: destructive,
            undoBehavior: undo)
    }

    static func structurallyApplicableActionIDs(
        for snapshot: SidebarSelectionSnapshot
    ) -> [String] {
        actions.compactMap { definition in
            capabilityDisabledReason(for: definition, snapshot: snapshot) == nil
                ? definition.id : nil
        }
    }

    static func evaluation(
        for id: String,
        snapshot: SidebarSelectionSnapshot?,
        structuralMutationDisabledReason: String? = nil,
        actionDisabledReasons: [String: String] = [:]
    ) -> SidebarActionEvaluation? {
        guard let definition = actions.first(where: { $0.id == id }) else { return nil }
        guard let snapshot else {
            return SidebarActionEvaluation(
                definition: definition,
                disabledReason: noVaultReason,
                intent: nil)
        }

        let disabledReason = capabilityDisabledReason(for: definition, snapshot: snapshot)
            ?? actionDisabledReasons[id]
            ?? (definition.blocksDuringStructuralMutation
                ? structuralMutationDisabledReason : nil)
        return SidebarActionEvaluation(
            definition: definition,
            disabledReason: disabledReason,
            intent: disabledReason == nil
                ? SidebarActionInvocationIntent(actionID: id, snapshot: snapshot)
                : nil)
    }

    static func project(
        surface: SidebarActionSurface,
        snapshot: SidebarSelectionSnapshot?,
        structuralMutationDisabledReason: String? = nil,
        actionDisabledReasons: [String: String] = [:]
    ) -> [SidebarActionEvaluation] {
        let definitions: [SidebarActionDefinition]
        switch surface {
        case .menuBar, .commandPalette:
            definitions = actions
        case .contextMenu, .voiceOver:
            definitions = contextualDefinitions(
                surface: surface, snapshot: snapshot)
        case .toolbar:
            definitions = actions.filter { toolbarActionIDs.contains($0.id) }
        case .keyboard:
            definitions = actions.filter { keyboardActionIDs.contains($0.id) }
        }

        let evaluations: [SidebarActionEvaluation] = definitions.compactMap {
            definition -> SidebarActionEvaluation? in
            guard let projected = evaluation(
                for: definition.id,
                snapshot: snapshot,
                structuralMutationDisabledReason: structuralMutationDisabledReason,
                actionDisabledReasons: actionDisabledReasons)
            else { return nil }

            guard definition.id == SlateCommandID.newFromTemplate,
                projected.disabledReason == nil,
                let snapshot
            else { return projected }

            // FL04-A preserves the shipped explicit-root picker. First admit
            // the live selection above; only then freeze the accepted intent
            // to root so a mixed selection cannot bypass capability checks.
            let rootSnapshot = SidebarSelectionSnapshot(
                sessionIdentity: snapshot.sessionIdentity,
                items: [],
                focusedPath: nil,
                creationParent: "")
            return SidebarActionEvaluation(
                definition: definition,
                disabledReason: nil,
                intent: SidebarActionInvocationIntent(
                    actionID: definition.id,
                    snapshot: rootSnapshot))
        }
        return surface.retainsUnavailableActions
            ? evaluations
            : evaluations.filter { $0.disabledReason == nil }
    }

    /// Contextual surfaces stay target-specific and concise. Capability still
    /// performs the final availability check below; this allowlist owns only
    /// which relevant verbs belong on each HIG surface. Template remains a
    /// deliberate FL04-A omission until it has a contextual destination.
    private static func contextualDefinitions(
        surface: SidebarActionSurface,
        snapshot: SidebarSelectionSnapshot?
    ) -> [SidebarActionDefinition] {
        guard let snapshot, !snapshot.items.isEmpty else { return [] }

        let ids: [String]
        if snapshot.items.count == 1, let item = snapshot.items.first {
            if item.isDirectory {
                ids = [
                    SlateCommandID.newNote,
                    SlateCommandID.newFolder,
                    SlateCommandID.renameEntry,
                    SlateCommandID.moveTo,
                    SlateCommandID.revealInFinder,
                    SlateCommandID.copyPath,
                    SlateCommandID.deleteEntry,
                ]
            } else {
                ids = [
                    SlateCommandID.sidebarOpen,
                    SlateCommandID.renameEntry,
                    SlateCommandID.moveTo,
                    SlateCommandID.duplicateEntry,
                    SlateCommandID.revealInFinder,
                    SlateCommandID.copyPath,
                    SlateCommandID.sidebarCopyWikilink,
                    SlateCommandID.deleteEntry,
                ]
            }
        } else if snapshot.items.allSatisfy({ !$0.isDirectory }) {
            ids = [
                SlateCommandID.sidebarOpen,
                SlateCommandID.moveTo,
                SlateCommandID.deleteEntry,
            ]
        } else {
            ids = [SlateCommandID.moveTo, SlateCommandID.deleteEntry]
        }

        let surfaceIDs = surface == .voiceOver
            ? ids.filter { $0 != SlateCommandID.sidebarOpen }
            : ids
        let idSet = Set(surfaceIDs)
        return actions.filter { idSet.contains($0.id) }
    }

    private static func capabilityDisabledReason(
        for action: SidebarActionDefinition,
        snapshot: SidebarSelectionSnapshot
    ) -> String? {
        let items = snapshot.items
        switch action.capability {
        case .oneOrMoreFiles:
            guard !items.isEmpty else { return "Select one or more files to open." }
            guard items.allSatisfy({ !$0.isDirectory }) else {
                return "Open is available only for files."
            }
        case .zeroOrOneItem:
            guard items.count <= 1 else {
                return "Select no more than one item to choose a creation location."
            }
        case .exactlyOneItem:
            guard items.count == 1 else {
                switch action.id {
                case SlateCommandID.renameEntry:
                    return "Select exactly one file or folder to rename."
                case SlateCommandID.revealInFinder:
                    return "Select exactly one file or folder to reveal."
                default:
                    return "Select exactly one file or folder to copy its path."
                }
            }
        case .oneOrMoreItems:
            guard !items.isEmpty else {
                return action.id == SlateCommandID.moveTo
                    ? "Select one or more files or folders to move."
                    : "Select one or more files or folders to move to the Trash."
            }
        case .exactlyOneFile:
            guard items.count == 1, items[0].isDirectory == false else {
                return "Select exactly one file to duplicate."
            }
        case .exactlyOneMarkdownFile:
            guard items.count == 1, items[0].isDirectory == false, items[0].isMarkdown else {
                return "Select exactly one Markdown file to copy its wikilink."
            }
        }
        return nil
    }
}
