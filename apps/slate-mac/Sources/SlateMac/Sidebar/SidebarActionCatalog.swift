// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// One live row captured from the sidebar's visible-order selection.
struct SidebarSelectionItem: Equatable, Hashable, Sendable {
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
    let selectionRevision: UInt64

    init(
        sessionIdentity: ObjectIdentifier,
        items: [SidebarSelectionItem],
        focusedPath: String?,
        creationParent: String,
        selectionRevision: UInt64 = 0
    ) {
        self.sessionIdentity = sessionIdentity
        self.items = items
        self.focusedPath = focusedPath
        self.creationParent = creationParent
        self.selectionRevision = selectionRevision
    }

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
            creationParent: creationParent,
            selectionRevision: model.selectionRevision)
    }

    private static func parentPath(of path: String) -> String {
        guard let separator = path.lastIndex(of: "/") else { return "" }
        return String(path[..<separator])
    }
}

enum SidebarActionCapability: Equatable {
    case oneOrMoreFiles
    case zeroOrOneItem
    /// FL-07: selection-independent — available with any selection shape
    /// whenever a vault is open (collapse/expand, history, recents,
    /// positional shortcut activation).
    case anySelection
    case exactlyOneItem
    case oneOrMoreItems
    case exactlyOneFile
    case exactlyOneFolder
    case exactlyOneMarkdownFile
}

enum SidebarActionUndoBehavior: Equatable {
    case noChange
    /// Mutation succeeds but clears prior structural undo/redo history.
    case historyBarrier
    case slateUndo
    case notUndoable
    /// The aggregate import outcome selects the truthful runtime policy:
    /// pure moves are undoable, verified or outcome-unknown external work
    /// clears history, and a clean no-op preserves history.
    case runtimeDetermined
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
    /// A large confirmed batch is being validated away from the main actor.
    case validationPending
    case rejected(String)
    case ignored
}

enum SidebarOpenSelectionDisposition: Equatable {
    case none
    case direct(SidebarOpenSelectionBatch)
    case confirm(SidebarOpenSelectionRequest)
}

/// Typed result keeps staged Open confirmation distinct from actions that have
/// completed all of their effects inside AppState.
enum SidebarActionDispatchResult: Equatable {
    case completed(actionID: String)
    /// Copy Wikilink is formatting away from the main actor.
    case copyPending(actionID: String)
    /// Complete filesystem admission is running away from the main actor.
    case validationPending(actionID: String)
    case opened([String])
    case openConfirmation(SidebarOpenSelectionRequest)
}

enum SidebarActionCatalog {
    /// FL-07: the nine positional palette commands are chord mirrors; the
    /// Shortcuts section rows are the accessible path, so VoiceOver's
    /// concise surfaces skip the numbered variants.
    static let voiceOverExcludedOpenShortcutSlots: Set<String> =
        Set(SlateCommandID.sidebarOpenShortcutSlots)

    typealias InvocationIntent = SidebarActionInvocationIntent

    static let noVaultReason = "Open a vault to use Sidebar actions."

    private static let toolbarActionIDs: Set<String> = [
        SlateCommandID.newFromTemplate,
        // FL3-4.1: collapse/expand's required toolbar surface.
        SlateCommandID.sidebarCollapseAll,
        SlateCommandID.sidebarExpandLoaded,
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
            blocksDuringStructuralMutation: true, undo: .historyBarrier),
        action(
            SlateCommandID.newFolder, "New Folder", .newFolder, .zeroOrOneItem,
            "Create a new folder in the selected location, then rename it.",
            blocksDuringStructuralMutation: true, undo: .historyBarrier),
        action(
            SlateCommandID.newFromTemplate, "New Note from Template…", .newFromTemplate,
            .zeroOrOneItem, "Choose a template for a new note.",
            blocksDuringStructuralMutation: true, undo: .historyBarrier),
        action(
            SlateCommandID.importFilesAndFolders, "Import Files and Folders…",
            .importFilesAndFolders, .zeroOrOneItem,
            "Choose files and folders. External items are copied into the selected location; items already in this vault are moved.",
            blocksDuringStructuralMutation: true, undo: .runtimeDetermined),
        action(
            SlateCommandID.renameEntry, "Rename…", .rename, .exactlyOneItem,
            "Rename the selected file or folder in place.",
            blocksDuringStructuralMutation: true, undo: .slateUndo),
        action(
            SlateCommandID.moveTo, "Move To…", .moveTo, .oneOrMoreItems,
            "Move the selected files or folders to another folder.",
            blocksDuringStructuralMutation: true, undo: .slateUndo),
        action(
            SlateCommandID.duplicateEntry, "Duplicate", .duplicate, .exactlyOneFile,
            "Duplicate the selected file as a copy next to it.",
            blocksDuringStructuralMutation: true, undo: .historyBarrier),
        action(
            SlateCommandID.revealInFinder, "Reveal in Finder", .revealInFinder,
            .exactlyOneItem, "Show the selected file or folder in Finder."),
        action(
            SlateCommandID.copyPath, "Copy Path", .copyPath, .exactlyOneItem,
            "Copy the selected item's path."),
        action(
            SlateCommandID.sidebarCopyWikilink, "Copy Wikilink", .copyWikilink,
            .exactlyOneMarkdownFile, "Copy a wikilink to the selected Markdown file.",
            blocksDuringStructuralMutation: true),
        // FL-06 organization (#658/#659). Preference edits, not vault content
        // mutations: they never block on the structural gate and none is
        // undoable — no label or hint may promise ⌘Z.
        action(
            SlateCommandID.sidebarPinNote, "Pin to Top of Folder", .pin,
            .exactlyOneFile,
            "Pin the selected note to the top of its folder."),
        action(
            SlateCommandID.sidebarUnpinNote, "Unpin", .unpin, .exactlyOneFile,
            "Remove the selected note from its folder's pinned section."),
        action(
            SlateCommandID.sidebarUnpinAll, "Unpin All in Folder", .unpin,
            .exactlyOneFolder,
            "Remove every pinned note from the selected folder."),
        action(
            SlateCommandID.sidebarSortNameAsc, "Sort by Name (A to Z)",
            .sortOrder, .zeroOrOneItem,
            "Sort the selected location's notes by name, A to Z."),
        action(
            SlateCommandID.sidebarSortNameDesc, "Sort by Name (Z to A)",
            .sortOrder, .zeroOrOneItem,
            "Sort the selected location's notes by name, Z to A."),
        action(
            SlateCommandID.sidebarSortCreatedDesc, "Sort by Created (Newest First)",
            .sortOrder, .zeroOrOneItem,
            "Sort the selected location's notes by created date, newest first."),
        action(
            SlateCommandID.sidebarSortCreatedAsc, "Sort by Created (Oldest First)",
            .sortOrder, .zeroOrOneItem,
            "Sort the selected location's notes by created date, oldest first."),
        action(
            SlateCommandID.sidebarSortModifiedDesc, "Sort by Modified (Newest First)",
            .sortOrder, .zeroOrOneItem,
            "Sort the selected location's notes by modified date, newest first."),
        action(
            SlateCommandID.sidebarSortModifiedAsc, "Sort by Modified (Oldest First)",
            .sortOrder, .zeroOrOneItem,
            "Sort the selected location's notes by modified date, oldest first."),
        action(
            SlateCommandID.sidebarToggleDateGrouping, "Group by Date",
            .dateGrouping, .zeroOrOneItem,
            "Group the selected location's notes into date sections."),
        action(
            SlateCommandID.sidebarUseVaultDefaultSort, "Use Vault Default Sort",
            .sortOrder, .zeroOrOneItem,
            "Remove the selected folder's sort override."),
        // FL-07 shortcuts/recents/navigation (#660/#661). Preference and
        // view-state edits: never structural, never undoable.
        action(
            SlateCommandID.sidebarAddShortcut, "Add to Shortcuts", .pin,
            .exactlyOneItem,
            "Add the selected file or folder to the Shortcuts section."),
        action(
            SlateCommandID.sidebarRemoveShortcut, "Remove from Shortcuts",
            .unpin, .exactlyOneItem,
            "Remove the selected file or folder from the Shortcuts section."),
        action(
            SlateCommandID.sidebarClearRecents, "Clear Recents", .unpin,
            .anySelection,
            "Clear the shared recent-files history for this vault."),
        action(
            SlateCommandID.sidebarCollapseAll, "Collapse All Folders",
            .sortOrder, .anySelection,
            "Collapse every folder except the current selection's ancestors."),
        action(
            SlateCommandID.sidebarExpandLoaded, "Expand Loaded Folders",
            .sortOrder, .anySelection,
            "Expand already-loaded folders, fetching at most one level deeper."),
        action(
            SlateCommandID.sidebarHistoryBack, "Back in Sidebar History",
            .sortOrder, .anySelection,
            "Select the previous sidebar selection from this window's history."),
        action(
            SlateCommandID.sidebarHistoryForward, "Forward in Sidebar History",
            .sortOrder, .anySelection,
            "Select the next sidebar selection from this window's history."),
        action(
            SlateCommandID.sidebarOpenShortcut(1), "Open Shortcut 1",
            .pin, .anySelection,
            "Activate shortcut 1 in the Shortcuts section."),
        action(
            SlateCommandID.sidebarOpenShortcut(2), "Open Shortcut 2",
            .pin, .anySelection,
            "Activate shortcut 2 in the Shortcuts section."),
        action(
            SlateCommandID.sidebarOpenShortcut(3), "Open Shortcut 3",
            .pin, .anySelection,
            "Activate shortcut 3 in the Shortcuts section."),
        action(
            SlateCommandID.sidebarOpenShortcut(4), "Open Shortcut 4",
            .pin, .anySelection,
            "Activate shortcut 4 in the Shortcuts section."),
        action(
            SlateCommandID.sidebarOpenShortcut(5), "Open Shortcut 5",
            .pin, .anySelection,
            "Activate shortcut 5 in the Shortcuts section."),
        action(
            SlateCommandID.sidebarOpenShortcut(6), "Open Shortcut 6",
            .pin, .anySelection,
            "Activate shortcut 6 in the Shortcuts section."),
        action(
            SlateCommandID.sidebarOpenShortcut(7), "Open Shortcut 7",
            .pin, .anySelection,
            "Activate shortcut 7 in the Shortcuts section."),
        action(
            SlateCommandID.sidebarOpenShortcut(8), "Open Shortcut 8",
            .pin, .anySelection,
            "Activate shortcut 8 in the Shortcuts section."),
        action(
            SlateCommandID.sidebarOpenShortcut(9), "Open Shortcut 9",
            .pin, .anySelection,
            "Activate shortcut 9 in the Shortcuts section."),
        // FL-09 filter UI (#663). View-state only, like the FL-07
        // navigation family.
        action(
            SlateCommandID.sidebarFocusFilter, "Focus Sidebar Filter",
            .search, .anySelection,
            "Move focus to the sidebar filter field."),
        // FL5-3b batch tag editors (#666): selection actions opening
        // the tag editor; per-file refusals ride the core report.
        action(
            SlateCommandID.sidebarAddTag, "Add Tag…", .pin,
            .oneOrMoreFiles,
            "Add a tag to the selected files' frontmatter."),
        action(
            SlateCommandID.sidebarRemoveTag, "Remove Tag…", .unpin,
            .oneOrMoreFiles,
            "Remove a tag from the selected files' frontmatter."),
        // FL6-1 folder notes (#667). Presence is validated at
        // activation (the catalog is pure); refusals announce once.
        action(
            SlateCommandID.createFolderNote, "Create Folder Note",
            .newNote, .exactlyOneFolder,
            "Create and open this folder's note.",
            blocksDuringStructuralMutation: true,
            undo: .historyBarrier),
        action(
            SlateCommandID.openFolderNote, "Open Folder Note",
            .open, .exactlyOneFolder,
            "Open this folder's note."),
        action(
            SlateCommandID.deleteFolderNote, "Delete Folder Note",
            .trash, .exactlyOneFolder,
            "Move this folder's note to the Trash.",
            blocksDuringStructuralMutation: true,
            destructive: true, undo: .notUndoable),
        action(
            SlateCommandID.deleteEntry, "Move to Trash", .trash, .oneOrMoreItems,
            "Move the selected files or folders to the Trash.",
            blocksDuringStructuralMutation: true,
            destructive: true, undo: .notUndoable),
    ]

    private static func action(
        _ id: String,
        _ label: String,
        _ symbol: SlateSymbol,
        _ capability: SidebarActionCapability,
        _ hint: String,
        blocksDuringStructuralMutation: Bool = false,
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
            blocksDuringStructuralMutation: blocksDuringStructuralMutation,
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
            definition in
            evaluation(
                for: definition.id,
                snapshot: snapshot,
                structuralMutationDisabledReason: structuralMutationDisabledReason,
                actionDisabledReasons: actionDisabledReasons)
        }
        return surface.retainsUnavailableActions
            ? evaluations
            : evaluations.filter { $0.disabledReason == nil }
    }

    /// Contextual surfaces stay target-specific and concise. Capability still
    /// performs the final availability check below; this allowlist owns only
    /// which relevant verbs belong on each HIG surface. Template is contextual
    /// only for a single selected folder, whose path is its exact destination.
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
                    SlateCommandID.newFromTemplate,
                    SlateCommandID.renameEntry,
                    SlateCommandID.moveTo,
                    SlateCommandID.sidebarSortNameAsc,
                    SlateCommandID.sidebarSortNameDesc,
                    SlateCommandID.sidebarSortCreatedDesc,
                    SlateCommandID.sidebarSortCreatedAsc,
                    SlateCommandID.sidebarSortModifiedDesc,
                    SlateCommandID.sidebarSortModifiedAsc,
                    SlateCommandID.sidebarToggleDateGrouping,
                    SlateCommandID.sidebarUseVaultDefaultSort,
                    SlateCommandID.sidebarUnpinAll,
                    SlateCommandID.sidebarAddShortcut,
                    SlateCommandID.sidebarRemoveShortcut,
                    SlateCommandID.createFolderNote,
                    SlateCommandID.openFolderNote,
                    SlateCommandID.deleteFolderNote,
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
                    SlateCommandID.sidebarPinNote,
                    SlateCommandID.sidebarAddShortcut,
                    SlateCommandID.sidebarRemoveShortcut,
                    SlateCommandID.sidebarUnpinNote,
                    SlateCommandID.sidebarAddTag,
                    SlateCommandID.sidebarRemoveTag,
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
                SlateCommandID.sidebarAddTag,
                SlateCommandID.sidebarRemoveTag,
                SlateCommandID.deleteEntry,
            ]
        } else {
            ids = [SlateCommandID.moveTo, SlateCommandID.deleteEntry]
        }

        // The VoiceOver rotor stays concise and selection-relevant: Open is
        // the row's activation action, and the sort/group radio set lives in
        // the context menu, menu bar, palette, and toolbar instead.
        let voiceOverExcluded: Set<String> = SidebarActionCatalog
            .voiceOverExcludedOpenShortcutSlots.union([
            SlateCommandID.sidebarOpen,
            SlateCommandID.sidebarSortNameAsc,
            SlateCommandID.sidebarSortNameDesc,
            SlateCommandID.sidebarSortCreatedDesc,
            SlateCommandID.sidebarSortCreatedAsc,
            SlateCommandID.sidebarSortModifiedDesc,
            SlateCommandID.sidebarSortModifiedAsc,
            SlateCommandID.sidebarToggleDateGrouping,
            SlateCommandID.sidebarUseVaultDefaultSort,
        ])
        let surfaceIDs = surface == .voiceOver
            ? ids.filter { !voiceOverExcluded.contains($0) }
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
                if action.id == SlateCommandID.importFilesAndFolders {
                    return "Select no more than one item to choose an import location."
                }
                if SlateCommandID.sidebarOrganizationCommands.contains(action.id) {
                    return "Select a single location to change how it's sorted."
                }
                return "Select no more than one item to choose a creation location."
            }
        case .anySelection:
            break
        case .exactlyOneItem:
            guard items.count == 1 else {
                switch action.id {
                case SlateCommandID.renameEntry:
                    return "Select exactly one file or folder to rename."
                case SlateCommandID.revealInFinder:
                    return "Select exactly one file or folder to reveal."
                case SlateCommandID.sidebarAddShortcut,
                    SlateCommandID.sidebarRemoveShortcut:
                    return "Select exactly one file or folder to change its "
                        + "Shortcuts membership."
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
                switch action.id {
                case SlateCommandID.sidebarPinNote:
                    return "Select exactly one note to pin."
                case SlateCommandID.sidebarUnpinNote:
                    return "Select exactly one note to unpin."
                default:
                    return "Select exactly one file to duplicate."
                }
            }
        case .exactlyOneFolder:
            guard items.count == 1, items[0].isDirectory else {
                switch action.id {
                case SlateCommandID.createFolderNote,
                    SlateCommandID.openFolderNote,
                    SlateCommandID.deleteFolderNote:
                    return "Select exactly one folder to manage its folder note."
                default:
                    return "Select exactly one folder to unpin its notes."
                }
            }
        case .exactlyOneMarkdownFile:
            guard items.count == 1, items[0].isDirectory == false, items[0].isMarkdown else {
                return "Select exactly one Markdown file to copy its wikilink."
            }
        }
        return nil
    }
}
