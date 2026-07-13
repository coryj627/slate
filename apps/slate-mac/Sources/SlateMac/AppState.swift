// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import Foundation
import SwiftUI

/// Search-overlay state machine. Lives on `AppState`, drives the
/// `SearchOverlay` view's mode (idle/searching/results/error) and
/// the VoiceOver announcements that go with each transition.
enum SearchState: Equatable {
    /// No query in flight and no results to show. The initial state
    /// when the overlay first opens.
    case idle
    /// A query is in flight. UI shows a "Searching…" placeholder
    /// (announced through the polite live region).
    case searching
    /// Latest search returned `rows` with a pre-rendered audio
    /// summary string (`"Search returned N results"`).
    case results(rows: [QueryHit], summary: String)
    /// SQLite or FFI error — surfaced through the same panel slot
    /// as results so the user notices.
    case error(String)
}

/// Outcome of a single link-activation call. Mirrors the branches in
/// `AppState.openLink(_:)` so tests can assert routing without
/// observing AppKit side effects.
enum LinkActivationOutcome: Equatable {
    case openedInternal(String)
    case unresolved(String)
    case openedExternal(String)
    case externalOpenFailed(String)
}

/// User-facing filter for the vault-wide Tasks Review view (#114).
/// Maps to the FFI `TaskFilter` via `toFFIFilter` — the UI side
/// carries the human-named cases (`.all`, `.dueToday`, …) and the
/// adapter converts them into the date-window + completed-flag
/// shape the SQLite query expects.
///
/// **Timezone note.** The backend parser stores `📅 2026-06-01` as
/// midnight UTC of that calendar date. Our `.dueToday` / `.overdue`
/// / `.thisWeek` windows compare against UTC midnight too, so the
/// filter boundary moves with UTC, not with the user's local
/// timezone. A user in PST will see "today" tasks change at
/// 5 PM local (UTC midnight). That's the cost of the parser's
/// timezone-naive shape and is documented as a V1 limitation; a
/// follow-up could thread a `TimeZone` through the parser and
/// filter together.
enum TaskReviewFilter: Hashable, CaseIterable, Identifiable {
    /// Every task in the vault, completed or open. The literal
    /// "show me everything" view.
    case all
    /// Open tasks with a due date that falls on today (UTC).
    case dueToday
    /// Open tasks whose due date is in the past (UTC) — `due_ms <
    /// startOfTodayUtc`. Tasks with no due date are excluded.
    case overdue
    /// Open tasks due in the next 7 days (UTC), inclusive of today,
    /// exclusive of the 8th day. Matches the `[from, to)` shape of
    /// the FFI's `dueFromMs` / `dueToMs` pair.
    case thisWeek

    var id: Self { self }

    /// Picker chip label. Plain English, no abbreviations — chips
    /// are read by VoiceOver verbatim.
    var displayName: String {
        switch self {
        case .all: return "All"
        case .dueToday: return "Due today"
        case .overdue: return "Overdue"
        case .thisWeek: return "This week"
        }
    }

    /// Resolve to the FFI `TaskFilter` against a reference "now"
    /// (parameterised so tests can pin the clock without mocking
    /// `Date()`). UTC throughout — see the timezone note above.
    func toFFIFilter(now: Date = Date()) -> TaskFilter {
        let cal = Self.utcCalendar
        let startOfTodayUtc = cal.startOfDay(for: now)
        switch self {
        case .all:
            return TaskFilter(
                completed: nil,
                dueFromMs: nil,
                dueToMs: nil,
                priorityAtLeast: nil
            )
        case .dueToday:
            let startOfTomorrow = cal.date(byAdding: .day, value: 1, to: startOfTodayUtc)!
            return TaskFilter(
                completed: false,
                dueFromMs: Int64(startOfTodayUtc.timeIntervalSince1970 * 1000),
                dueToMs: Int64(startOfTomorrow.timeIntervalSince1970 * 1000),
                priorityAtLeast: nil
            )
        case .overdue:
            return TaskFilter(
                completed: false,
                // Unix epoch start as the lower bound — any due
                // date qualifies as long as it's before today.
                dueFromMs: 0,
                dueToMs: Int64(startOfTodayUtc.timeIntervalSince1970 * 1000),
                priorityAtLeast: nil
            )
        case .thisWeek:
            let endOfWindow = cal.date(byAdding: .day, value: 7, to: startOfTodayUtc)!
            return TaskFilter(
                completed: false,
                dueFromMs: Int64(startOfTodayUtc.timeIntervalSince1970 * 1000),
                dueToMs: Int64(endOfWindow.timeIntervalSince1970 * 1000),
                priorityAtLeast: nil
            )
        }
    }

    /// UTC `Calendar` for `startOfDay` etc. so the filter's
    /// behaviour is locale-independent. Built once per call site
    /// rather than statically because `Calendar` isn't `Sendable`
    /// across actors in Swift 6 strict-concurrency mode.
    private static var utcCalendar: Calendar {
        var cal = Calendar(identifier: .gregorian)
        cal.timeZone = TimeZone(identifier: "UTC")!
        return cal
    }
}

/// Per-incident snapshot for the conflict-resolution alert.
///
/// Populated by `saveCurrentNote` when the backend returns
/// `WriteConflict`. Carries everything the resolution actions need:
/// the bytes the user tried to save (so "Keep mine" can re-issue the
/// save), the on-disk hash the alert reports, and the original
/// `expectedContentHash` (kept for telemetry — testers can confirm
/// the conflict was caught because the file moved, not because the
/// editor's hash tracking was wrong).
struct SaveConflict: Equatable {
    let path: String
    let attemptedContents: String
    let currentContentHash: String
    let expectedContentHash: String
    let currentMtimeMs: Int64
}

/// Per-incident snapshot for the editor's embed-preview popover
/// (#188). Carries enough state for the popover to render the
/// resolved embed without re-querying the backend — the resolution
/// has already been cached by `loadCurrentNoteEmbedResolutions`.
///
/// `sourceLine` is the 1-based line number of the embed's source
/// `![[…]]` reference in the editor buffer. Surfaced in the
/// popover header so users have textual spatial bearing without
/// AppKit geometry plumbing (audit #209). Nil when the caller
/// can't compute it (synthetic callers in tests).
struct EmbedPreview: Equatable {
    let target: String
    let resolution: EmbedResolution
    let sourceLine: Int?
}

/// What edit the user was trying to make when a property-edit
/// `WriteConflict` fired. Carries enough state for the resolve
/// helpers to re-issue the original edit verbatim.
enum PropertyEditAction: Equatable {
    case set(PropertyValue)
    case delete
    /// U3-4 (#468): replace the WHOLE frontmatter source (the widget's
    /// show-source Apply). Rides the same edit/conflict machinery as the
    /// per-key actions — one conflict alert flow, one resolution surface.
    case setSource(String)
}

/// Per-incident snapshot for the property-edit conflict alert.
/// Modeled on `SaveConflict` (whole-file save) but scoped to a
/// single key edit so "Keep mine" re-issues the property action
/// rather than the whole-file save.
struct PropertyEditConflict: Equatable {
    let path: String
    let key: String
    let action: PropertyEditAction
    let currentContentHash: String
    let expectedContentHash: String
    let currentMtimeMs: Int64
}

/// Destination the user asked to navigate to while the editor was
/// dirty. Held in `AppState.pendingNavigation` until the user
/// responds to the "Save changes?" alert.
enum PendingNavigation: Equatable {
    case closeVault
    case selectFile(String?)
}

/// State machine driving the create-from-template flow (Milestone H).
///
/// `idle` is the resting state. `needsPrompts` is set after
/// `selectTemplate` against a template that declared at least one
/// `{{prompt:Label}}` marker — the prompt sheet renders one
/// `TextField` per entry, in declaration order, and `Submit`
/// transitions to `needsName(...)` carrying the user's responses.
/// `needsName` is set for prompt-less templates (skipping straight
/// from picker → name sheet) and for any template after its prompts
/// resolve. Submitting the name sheet returns the flow to `idle`
/// via `submitTemplateNoteName`; any sheet's Cancel button calls
/// `cancelTemplateFlow` to do the same.
enum PendingTemplateFlow: Equatable {
    case idle
    case needsPrompts(TemplateSummary, [TemplatePrompt])
    case needsName(TemplateSummary, [String: String])
}

enum BaseQueryBuilderPreviewExecutionPhase: Sendable, Equatable {
    case opened
    case executed
    case closed
}

/// Nil-default preview lifecycle observation surface. The recorded thread bit
/// is captured immediately before each synchronous native call; tests can also
/// suspend the observer after `opened` to hold a real handle across generations.
struct BaseQueryBuilderPreviewExecutionEvent: Sendable, Equatable {
    let phase: BaseQueryBuilderPreviewExecutionPhase
    let generation: Int
    let handle: UInt64
    let ranOnMainThread: Bool
}

enum BaseQueryBuilderPreviewExecutionOutcome: Sendable {
    case success(BasesResultSet)
    case failure(String)
    case cancelled
}

/// Top-level app state.
///
/// Owns the currently-open `VaultSession` (or none, on the welcome
/// screen) and the most-recent error surfaced from opening one. The
/// session is held until `closeVault()` is called or another vault is
/// opened. uniffi gives us back a reference-counted `VaultSession`, so
/// storing it on the main-thread state object is enough — the Rust
/// side keeps the SQLite connection alive as long as we hold a
/// reference.
@MainActor
final class AppState: ObservableObject {
    @Published private(set) var currentSession: VaultSession?
    @Published private(set) var currentVaultURL: URL?
    @Published private(set) var recentVaults: [RecentVault] = []
    @Published var lastError: String?
    /// Set when an attempt to open a recent-vaults entry fails because
    /// the path no longer exists. WelcomeView reads this to drive the
    /// "missing vault, remove from recent?" confirmation alert.
    @Published var missingRecentVault: RecentVault?
    /// Markdown files in the currently-open vault, sorted by relative
    /// path (case-insensitive). Populated by `loadFiles()` after the
    /// scanner finishes; empty while no vault is open or while the
    /// initial scan is still running.
    @Published private(set) var files: [FileSummary] = []
    /// True while the initial scan + file load is in progress for the
    /// current vault. Sidebar uses this to show a progress indicator.
    @Published private(set) var isScanning: Bool = false
    /// Latest progress event from the scanner. Updated unconditionally
    /// for every `Started`/`FileIndexed`/`Finished` so the sidebar's
    /// progress bar can stay current; cleared back to `nil` on
    /// terminal events (Finished/Cancelled/Failed) so the bar hides.
    @Published private(set) var scanProgress: ScanProgress?
    /// Surfaced when scanning or listing fails. Independent of
    /// `lastError` (which guards the open path).
    @Published var scanError: String?
    /// Path of the file currently selected in the sidebar, if any.
    /// `NoteContentView` reads the corresponding bytes via
    /// `currentNoteText`; AppState watches this property and kicks
    /// off the load whenever it changes.
    @Published var selectedFilePath: String?

    // MARK: File-management command state (U2-5, #463)

    /// The most recent structural mutation the tree sidebar must react to
    /// (create/rename/move/delete). `FileTreeSidebar` observes this and drives
    /// its view-owned `FileTreeViewModel` — `treeInvalidation` for the affected
    /// levels, then `postMutationFocus` (U2-6) to move the selection. AppState
    /// owns the mutation funnel but not the tree VM (which is a `@StateObject`
    /// in the view, per U2-4), so this published seam is how the two connect —
    /// the same shape `selectedFilePath` uses to drive the tree.
    ///
    /// A monotonically-bumped `token` guarantees `.onChange` fires even when two
    /// mutations produce an equal event payload (e.g. two creates into root).
    @Published private(set) var treeMutation: TreeMutation?

    /// Surfaced when a structural mutation's link-rewrite pass left one or more
    /// files un-rewritten (`StructuralReport.failed`): the move/rename itself
    /// stood, but some notes' links to it couldn't be updated (an external edit
    /// raced us, malformed frontmatter, …). Drives a SPECIFIC alert listing the
    /// skipped files — never a silent drop (spec §U2-5 "surface … in a specific
    /// alert listing skipped files").
    @Published var structuralFailureReport: StructuralFailureReport?

    /// The tab currently in inline-rename mode in the tree, if any (U2-5). The
    /// tree row swaps its label for a `TextField` while this matches the row's
    /// node. Nil = no rename in progress. Set by the rename command, cleared on
    /// commit/cancel.
    @Published var renamingNode: RenamingNode?

    /// Drives the Move-to-folder sheet (U2-5). Non-nil = the sheet is up,
    /// carrying the node being moved. Cleared on commit/cancel.
    @Published var pendingMove: PendingMove?

    /// Drives the BATCH Move-to-folder sheet (#852): a multi-selection's items
    /// awaiting a destination. The same `MoveToFolderSheet` renders it (a batch
    /// initializer); commit routes every item through `batchMove`. Separate from
    /// `pendingMove` so the single-node path (U2-5) is untouched. Cleared on
    /// commit/cancel.
    @Published var pendingBatchMove: BatchMove?

    /// The tree's currently-selected node (file OR folder), mirrored from
    /// `FileTreeSidebar` so the file-management COMMANDS (which run from the
    /// palette / menu with no row context) know what to act on. A file
    /// selection also lives in `selectedFilePath`; folders have no other home,
    /// so this is the single source the commands read. Nil = nothing selected
    /// (commands that need a target no-op or fall back to the vault root).
    @Published var treeSelectedNode: TreeSelection?

    /// A tree selection the file-management commands act on (U2-5).
    struct TreeSelection: Equatable {
        let path: String
        let isDirectory: Bool
    }

    /// The node currently being dragged in the tree (U2-5 drag & drop), recorded
    /// when a drag starts so the drop handler knows whether it's a directory
    /// (the drag payload carries only the path). Transient; cleared on drop.
    @Published var dragSourceNode: TreeSelection?

    /// The folder a new note/folder should be created in, given the current
    /// tree selection: the selected folder itself, a selected file's parent
    /// folder, or the vault root ("") when nothing is selected. Command-facing.
    var creationParentPath: String {
        guard let node = treeSelectedNode else { return "" }
        if node.isDirectory { return node.path }
        return TreeMutation.parentPath(of: node.path) ?? ""
    }

    /// The workspace shell (Milestone U1): split tree → tab groups → tabs.
    /// AppState's single-note fields hold the ACTIVE tab's document; parked
    /// tabs live in `workspace.documents` (U1-2 snapshot/restore — see the
    /// architecture amendment in u1_spec.md).
    let workspace = WorkspaceState()

    /// A tab-close request blocked on that tab's unsaved changes (U1-2).
    /// Drives the Save / Discard / Cancel alert in `MainSplitView`; the
    /// resolve trio below routes the outcome.
    @Published var pendingTabClose: TabID?

    /// Set when the pending-close alert chose "Save": the close completes
    /// when the in-flight save lands cleanly, and aborts on conflict/error
    /// (the tab must stay open while the user resolves).
    var pendingTabCloseAfterSave: TabID?

    /// Open canvas documents keyed by vault-relative path (Milestone T,
    /// #369) — one per path (t2), shared by every pane/tab showing that
    /// canvas. Not @Published: views observe each document directly.
    var canvasDocuments: [String: CanvasDocument] = [:]

    /// Open base documents keyed by vault-relative path (Milestone N,
    /// #702) or saved-query source — one per source, shared by every
    /// pane/tab showing that base.
    /// Not @Published: views observe each document directly.
    var baseDocuments: [String: BaseDocument] = [:]

    /// Right-pane Queries leaf snapshot (#709): saved queries, `.base`
    /// files, dashboards, and app-level saved-query pins.
    @Published var baseQueries = BaseQueriesState()
    var savedQueryCommandIDs: Set<String> = []

    /// Dashboard tab documents (#710), keyed by dashboard id.
    var dashboardDocuments: [String: DashboardDocument] = [:]

    /// Right-pane docked Bases surface (#710): target plus the currently
    /// followed active note context.
    @Published var basesDock = BasesDockState()
    var basesDockDocument: BaseDocument?
    var basesDockDashboardDocument: DashboardDocument?
    var basesDockRefreshTask: Task<Void, Never>?

    /// Open embedded Bases keyed by source + host note (#706). Reusing the
    /// handle keeps duplicate embeds on one native cache while each rendered
    /// embed owns independent quick-filter/sort/view UI state.
    var baseEmbedHandles: [BaseEmbedCacheKey: BaseEmbedHandle] = [:]

    /// Per-tab Bases renderer override (#703). This is transient UI
    /// preference, deliberately outside `.base` persistence.
    @Published var baseRendererOverrides: [TabID: BaseRendererMode] = [:]

    /// #704: bumped to move keyboard focus into the active Bases quick
    /// filter field. The filter text itself lives on `BaseDocument` and is
    /// never persisted to the `.base` file.
    @Published var baseQuickFilterFocusToken = 0

    /// Last Bases action announcement, exposed for XCTest because the global
    /// accessibility announcer is a no-op without a running NSApp.
    @Published var lastBaseActionAnnouncement: String?

    /// Deduplicated membership announcements produced by the most recent
    /// successful in-app write refresh. This is also the deterministic XCTest
    /// surface for the no-spam gate; ordinary Bases action announcements stay
    /// in `lastBaseActionAnnouncement`.
    @Published var lastBaseRefreshAnnouncements: [String] = []

    /// N3-07 race-test seam — always nil in production. Tests park a completed
    /// write immediately before the session/active-note publish guards, switch
    /// notes or vaults, then release it to prove global Bases refresh ownership.
    var basesPostWritePublishGate: (() async -> Void)?

    /// Currently selected Bases row/column for registry commands whose
    /// invocation originates outside the grid's AppKit row-action callback.
    @Published var activeBaseSelectionPath: String?
    @Published var activeBaseSelectedRow: BasesRow?
    @Published var activeBaseSelectedColumn: BasesColumn?
    @Published var baseEditPropertyRequestToken = 0

    /// The one canvas announcement funnel (#518, DoD §H). Every canvas
    /// surface phrases through it; verbosity persists via
    /// `PreferencesStore` (`setCanvasVerbosity`).
    lazy var canvasAnnouncer = CanvasAnnouncer(
        verbosity: preferencesStore.loadCanvasPrefs().verbosity)

    /// The one graph announcement funnel (Milestone P; mirrors
    /// `canvasAnnouncer`). Connections leaf, graph table, and diagram
    /// surfaces phrase through it — no graph code posts directly
    /// (enforced by `GraphAnnouncerTests`). Verbosity persistence
    /// moves into `.slate/graph.json` with P2-4; `.standard` until then.
    lazy var graphAnnouncer = GraphAnnouncer()

    // MARK: Connections leaf (Milestone P, P1-1 #554)

    /// Local-graph depth for the Connections leaf (1…3). Persisted
    /// per-vault; migrates into `.slate/graph.json` with P2-4.
    @Published var connectionsDepth: Int = 1
    /// The note the Connections leaf is rooted on. `nil` = follow
    /// `selectedFilePath`; non-nil after a "Show connections" re-root
    /// (breadcrumb back-stack in `connectionsBackStack`).
    @Published var connectionsRootPath: String?
    /// The neighborhood payload (structure + metrics + pre-rendered
    /// `audioSummary`) for the current root+depth.
    @Published var connectionsNeighborhood: GraphNeighborhood?
    /// Depth-1 snippet source (spec §P1-1: rows show snippets from the
    /// existing `Backlink`/`OutgoingLink` data at depth 1).
    @Published var connectionsBundle: NoteLoadBundle?
    /// The path the currently-published neighborhood/bundle describes.
    /// The panel renders rows only when this matches the effective
    /// path, so a note switch never shows B's header over A's rows
    /// (review round 1 finding 9).
    @Published var connectionsLoadedPath: String?
    @Published var connectionsLoading: Bool = false
    @Published var connectionsError: String?
    /// Last graph generation the leaf refreshed against — the P0-3
    /// refresh discriminator (only reload when it moves).
    var connectionsSeenGraphGeneration: UInt64 = 0
    /// Newest-wins guard (the O-5 seq pattern).
    var connectionsLoadSeq: UInt64 = 0
    /// Re-root breadcrumb: `⌘[` pops back one step. Each entry records
    /// BOTH the prior root mode (`root`: a path, or `nil` = "was
    /// following the selection") AND the note that was in view
    /// (`effective`), so back restores the exact prior view —
    /// including the originally-selected note when returning to
    /// follow-mode (review round 3 finding 1).
    var connectionsBackStack: [(root: String?, effective: String)] = []
    /// Race-test seam (post-compute, pre-guard); nil in production.
    var connectionsPublishGate: (() async -> Void)?

    /// The note the Connections leaf currently describes: an explicit
    /// re-root wins, else the selected note.
    var connectionsEffectivePath: String? {
        connectionsRootPath ?? selectedFilePath
    }

    // MARK: Graph tab, Table mode (Milestone P, P1-2 #555)

    /// The whole-graph snapshot backing the Graph tab's Table mode,
    /// fetched once per generation and sorted/filtered client-side.
    @Published var graphTableSnapshot: GraphSnapshot?
    /// Backend filter (a snapshot re-fetch on change): Attachments /
    /// Unresolved (= ghosts) / Orphans-only toggles — exactly
    /// `GraphFilter` semantics (spec §P1-2). Defaults: attachments off,
    /// ghosts on.
    @Published var graphTableFilter = GraphFilter(
        includeAttachments: false, includeGhosts: true, orphansOnly: false)
    /// Client-side label substring filter (case/diacritic-insensitive),
    /// applied to the fetched rows without a re-fetch.
    @Published var graphTableTextFilter: String = ""
    /// Client-side KIND filter (P1-3 #556): non-nil shows only rows of
    /// that kind — the "unresolved links" preset sets `.ghost` (which
    /// `GraphFilter` can't express, having no "notes off" flag). Set only
    /// by presets; cleared by any manual filter-bar toggle so it never
    /// becomes hidden state the user can't see or undo.
    @Published var graphTableKindFilter: GraphNodeKind?
    /// A preset (orphans / unresolved / most-linked) awaiting its
    /// post-load announcement — set by `openGraphPreset`, consumed once
    /// the fresh snapshot publishes so the count/hub is spoken from real
    /// data, not the stale pre-fetch snapshot (P1-3 #556).
    var graphTablePendingPreset: GraphPreset?
    @Published var graphTableLoading: Bool = false
    @Published var graphTableError: String?
    var graphTableLoadSeq: UInt64 = 0
    var graphTableSeenGraphGeneration: UInt64 = 0
    /// Race-test seam (post-compute, pre-guard); nil in production.
    var graphTablePublishGate: (() async -> Void)?
    /// True when the active tab is the graph tab (gates the
    /// generation-driven table refresh's announcements).
    var graphTabActive: Bool {
        workspace.activeTab?.item == .graph
    }

    /// True when a `.graph` tab is the active tab of ANY split group —
    /// i.e. currently rendered somewhere, not merely open in a
    /// background position. Gates the generation-driven refresh so it
    /// stops once every graph tab is closed (a stale `graphTableSnapshot`
    /// is never cleared on close, so keying on it alone leaked a
    /// forever-refresh with no on-screen consumer — review round 1
    /// finding 10) while still keeping a graph visible in a non-focused
    /// pane current.
    var anyGraphTabVisible: Bool {
        workspace.model.groupsInOrder.contains { $0.activeTab?.item == .graph }
    }

    // MARK: Graph tab, Diagram mode (Milestone P, P2-3 #559)

    /// The live visual-diagram model — nil until the Graph tab enters
    /// Diagram mode, torn down on graph-tab close / vault change. Owns the
    /// running `LayoutSession` and the id→metadata join for accessible
    /// labels and actions.
    @Published var graphDiagramModel: GraphDiagramModel?
    /// Monotonic build sequence: an in-flight diagram build is stale once
    /// this advances (tab close, vault change, rebuild).
    var graphDiagramBuildSeq: UInt64 = 0
    /// Serializes generation-refreshes: each waits for the prior to adopt
    /// before probing, so two file-change-driven refreshes can't race
    /// (the second would otherwise get `nil` from an already-advanced
    /// layout while the first is rejected, stranding the model on the old
    /// generation). Adoption additionally requires a strictly newer
    /// generation (monotonic).
    var graphDiagramRefreshTask: Task<Void, Never>?
    @Published var graphDiagramLoading = false
    @Published var graphDiagramError: String?

    /// Persisted graph-tab config (P2-4 #560): the LIVE holder for the
    /// inspector's Groups / Display / Forces; filters, mode, and depth
    /// are synced from their existing live state at the load/save
    /// boundaries. Written to `.slate/graph.json` (debounced) via
    /// `graphConfigSaveTask`.
    @Published var graphConfig = GraphConfig.default
    /// Pending debounced saves keyed BY VAULT — genuine per-vault
    /// bookkeeping (P2-4 review finding 3). A same-vault edit cancels only
    /// that vault's pending task (coalescing); a save for a DIFFERENT vault
    /// keeps its own entry and runs to completion, so a fast vault switch
    /// can't drop vault A's final write and no stale same-vault task lingers.
    var graphConfigSaveTasks: [URL: Task<Void, Never>] = [:]
    /// A per-vault monotonic token so a completing save clears its dict
    /// entry ONLY if a newer same-vault save hasn't already replaced it
    /// (avoids a mid-write task dropping its successor's entry).
    var graphConfigSaveGen: [URL: Int] = [:]
    /// The vault `graphConfig` was last loaded for — so the config loads
    /// ONCE per vault (a per-activate reload would revert a debounced,
    /// not-yet-written filter/force edit when the user tab-switches).
    var graphConfigVaultURL: URL?
    /// False when this vault's `graph.json` is unreadable / unparseable /
    /// a NEWER version — the file is then treated as read-only so a save
    /// can never clobber whatever a newer Slate (or a human) put there
    /// (P2-4 review finding 2). Reset true on each successful load.
    var graphConfigWritable = true
    /// Set when a forces edit re-heats the layout; the renderer announces a
    /// single "settled" state once the layout converges, then clears it
    /// (P2-4 review finding 8 — the settled-state announcement).
    var graphForcesSettlePending = false

    /// The ⌃⌘I "Where am I?" readback (t0 §1.4): non-nil presents the
    /// focusable transient panel in the canvas container; Esc/Close
    /// dismisses (panel-local, not a t0 M5 ladder rung).
    @Published var canvasWhereAmIReadback: String?

    /// Per-canvas mode controllers (t0 §2, #364) keyed by path —
    /// created lazily; #521/#523 enter modes through them.
    var canvasModeControllers: [String: CanvasModeController] = [:]

    /// The pending canvas input prompt (#368): non-nil presents the
    /// matching sheet in the canvas container (M6 visible controls).
    @Published var canvasPrompt: CanvasPrompt?

    /// The pending card-picker request (#522/#523): non-nil presents
    /// the reusable proximity-sorted picker in the container.
    @Published var canvasCardPicker: CanvasCardPickerRequest?

    /// #368: the open text-card edit session (sheet; Esc commits — M8).
    @Published var canvasCardEditor: CanvasCardEditorRequest?

    /// #373: bumped to move keyboard focus into the canvas filter
    /// field (⌘F while a canvas has focus).
    @Published var canvasFilterFocusToken = 0

    /// Transient move/resize geometry (#521, the t4 pipeline
    /// exception): held UI-side while a spatial mode is active,
    /// committed as ONE canvas_apply on Return, dropped on Esc.
    var canvasTransient: CanvasTransientState?

    /// Re-entrancy latch: `activateTab` runs the tab funnel itself and then
    /// mirrors `selectedFilePath` for the sidebar highlight; the
    /// `$selectedFilePath` sink must not run the selection funnel again on
    /// that assignment. Synchronous sink ⇒ a plain flag suffices.
    var isActivatingTab = false

    /// The single tab-switch funnel (U1-2): snapshot the outgoing tab,
    /// select `id`, restore its parked buffer (or disk-load on first
    /// activation), re-fire the collection loads, and mirror the sidebar
    /// selection. Identity-keyed, so two tabs holding the SAME path switch
    /// correctly (a path-keyed funnel cannot distinguish them).
    func activateTab(_ id: TabID) {
        guard let tab = workspace.model.tab(id) else { return }
        clearBaseQuickFilterIfLeavingActiveTab(for: id)
        if case .base(let path) = tab.item {
            activateBaseTab(id, path: path)
            return
        }
        if case .savedQuery(let savedQueryID, let name) = tab.item {
            activateSavedQueryTab(id, savedQueryID: savedQueryID, name: name)
            return
        }
        if case .dashboard(let dashboardID, let name) = tab.item {
            activateDashboardTab(id, dashboardID: dashboardID, name: name)
            return
        }
        if case .canvas(let path) = tab.item {
            activateCanvasTab(id, path: path)
            return
        }
        if case .graph = tab.item {
            activateGraphTab(id)
            return
        }
        guard case .markdown(let path) = tab.item else { return }
        if id == workspace.model.activeGroup.activeTabID, loadedFilePath == path {
            return
        }
        // U4-4 (#473): any tab activation lands keyboard focus in the editor
        // content, so the focus region is now `.editor` — a subsequent
        // ⌘⌥arrow anchors on this group, not a stale terminal region left
        // over from before the switch. Clears `lastFocusedGroup`; the terminal
        // return-path already restored+cleared it before reaching here.
        workspace.markEditorRegionActive()
        // Codoki #492 (High): a save-then-close scope must not outlive its
        // tab context. If the user switches away while that save is in
        // flight, the success branch correctly skips the close (active-tab
        // guard) — but leaving the marker set would close the tab on a
        // LATER unrelated save. Clear it on any switch away.
        if let pending = pendingTabCloseAfterSave, pending != id {
            pendingTabCloseAfterSave = nil
        }
        isActivatingTab = true
        defer { isActivatingTab = false }
        workspace.snapshotActiveTab(
            text: currentNoteText, baseline: savedBaselineText,
            contentHash: currentNoteContentHash,
            hasUnsavedChanges: hasUnsavedChanges,
            saveError: saveError, saveConflict: currentSaveConflict,
            loadedFilePath: loadedFilePath,
            fmSource: currentNoteFMSource,
            bodyByteOffset: bodyByteOffset, bodyLineOffset: bodyLineOffset)
        cancelNoteScopedWork()
        clearActiveNoteFields()
        workspace.select(id)
        clearTransitionSensitiveCollections()
        restoreParkedOrLoadFromDisk(path: path)
        // Sidebar highlight mirrors the active tab. The sink is latched;
        // `removeDuplicates` additionally swallows same-path switches.
        if selectedFilePath != path {
            selectedFilePath = path
        }
        fireCollectionLoads(path: path)
        scheduleBasesDockFollowActiveRefresh()
    }

    // MARK: - Tab lifecycle (U1-2, #454)

    /// ⌘T — Duplicate Tab (#863 returned the chord to the tab family;
    /// Quick Open moved to ⌘O, Obsidian's actual default). Duplicates
    /// the active tab's item — Slate's "new tab" verb, since a tab
    /// always hosts an item (u1_spec §U1-2); with no active tab it is
    /// a no-op.
    func newTab() {
        guard let item = workspace.activeTab?.item else { return }
        // The graph is a workspace-global singleton: Duplicate Tab must
        // not spawn a second graph (round 2 finding 6). It's already the
        // active tab, so this is a no-op beyond the spoken confirmation.
        if case .graph = item {
            graphAnnouncer.announce(.status("The graph is already open."))
            return
        }
        // Snapshot the outgoing buffer, then open the duplicate. The
        // selection sink does NOT fire (same path selected), so park and
        // restore in place.
        workspace.snapshotActiveTab(
            text: currentNoteText, baseline: savedBaselineText,
            contentHash: currentNoteContentHash,
            hasUnsavedChanges: hasUnsavedChanges,
            saveError: saveError, saveConflict: currentSaveConflict,
            loadedFilePath: loadedFilePath,
            fmSource: currentNoteFMSource,
            bodyByteOffset: bodyByteOffset, bodyLineOffset: bodyLineOffset)
        workspace.openTab(item, allowDuplicate: true)
        announceActiveTab(prefix: "Opened new tab.")
    }

    /// ⌘W and the tab-strip close buttons. Gates on the tab's dirty state —
    /// active tab reads the live fields; parked tabs read their document.
    func requestCloseTab(_ id: TabID? = nil) {
        guard let target = id ?? workspace.model.activeGroup.activeTabID else { return }
        // Canvas/base tabs are never dirty in the note-buffer sense:
        // canvas mutations write through, and the first Bases surface is
        // read-only (#702), so the close gate is bypassed by design.
        if case .canvas = workspace.model.tab(target)?.item {
            performCloseTab(target)
            return
        }
        if case .base = workspace.model.tab(target)?.item {
            performCloseTab(target)
            return
        }
        if case .savedQuery = workspace.model.tab(target)?.item {
            performCloseTab(target)
            return
        }
        if case .dashboard = workspace.model.tab(target)?.item {
            performCloseTab(target)
            return
        }
        // The Graph tab has no editable buffer, so it is never dirty —
        // closing it must never present the note save gate.
        if case .graph = workspace.model.tab(target)?.item {
            performCloseTab(target)
            return
        }
        let isActive = target == workspace.model.activeGroup.activeTabID
        let dirty =
            isActive
            ? hasUnsavedChanges
            : (workspace.document(for: target)?.hasUnsavedChanges ?? false)
        if dirty {
            pendingTabClose = target
        } else {
            performCloseTab(target)
        }
    }

    /// Alert "Save": activate the tab if parked (snapshot/restore through
    /// the identity funnel), then save through the standard path; the close
    /// completes in `performSave`'s success branch.
    func resolveTabCloseSave() {
        guard let target = pendingTabClose else { return }
        pendingTabClose = nil
        if target != workspace.model.activeGroup.activeTabID {
            activateTab(target)
        }
        pendingTabCloseAfterSave = target
        saveCurrentNote()
    }

    /// Alert "Discard": drop the tab's buffer and close it.
    func resolveTabCloseDiscard() {
        guard let target = pendingTabClose else { return }
        pendingTabClose = nil
        if target == workspace.model.activeGroup.activeTabID {
            // Neutralize the dirty state so the close path's teardown
            // doesn't trip the navigation gate.
            hasUnsavedChanges = false
        }
        performCloseTab(target)
    }

    func resolveTabCloseCancel() {
        pendingTabClose = nil
    }

    /// The close itself: mutate the model, then activate the surviving
    /// focused tab through the identity funnel (restores its buffer even
    /// when it shares a path with the closed tab).
    func performCloseTab(_ id: TabID) {
        let closingActive = id == workspace.model.activeGroup.activeTabID
        let closedTitle = workspace.model.tab(id).map { filename(of: workspace.tabPath($0)) }
        if closingActive {
            // The buffer being closed must not leak into the successor's
            // snapshot: tear the active fields down BEFORE the model close
            // (afterwards the model's active tab is already the successor,
            // and a snapshot would park the closed buffer under its id).
            // A dirty buffer only reaches here through the Discard
            // resolution or clean state — requestCloseTab gates the rest.
            cancelNoteScopedWork()
            clearActiveNoteFields()
        }
        editorCaretReturn[id] = nil
        let closedItem = workspace.model.tab(id)?.item
        let outcome = workspace.close(id)
        releaseCanvasDocumentIfUnreferenced(closedItem)
        releaseBaseDocumentIfUnreferenced(closedItem)
        releaseDashboardDocumentIfUnreferenced(closedItem)
        releaseGraphStateIfUnreferenced(closedItem)
        if closingActive {
            if let successor = outcome.focusedTab {
                activateTab(successor)
            } else if selectedFilePath != nil {
                selectedFilePath = nil
            }
        }
        if let closedTitle {
            let successor = workspace.activeTab.map {
                filename(of: workspace.tabPath($0))
            }
            postAccessibilityAnnouncement(
                successor.map { "Closed \(closedTitle). \($0) is active." }
                    ?? "Closed \(closedTitle).",
                priority: .medium)
        }
    }

    // MARK: - Reopen Closed Tab (#863)

    /// Menu-enablement mirror of `workspace.closedTabs.isEmpty` (#863).
    /// The File ▸ Reopen Closed Tab item reads THIS property. Predates
    /// the #868 workspace→appState objectWillChange bridge (which alone
    /// would now re-render the menu on stack changes); kept because the
    /// mirror is the tested, named Bool surface the menu reads — no
    /// reason to re-derive emptiness at the call site. Wired in `init`
    /// from `workspace.$closedTabs`.
    @Published private(set) var canReopenClosedTab = false

    /// ⇧⌘T — Reopen Closed Tab (#863, the macOS/Obsidian convention).
    /// Pops the per-vault-session closed-tab stack and reopens the
    /// record through the STANDARD open funnel (`openFile` /
    /// `openSavedQuery` / `openDashboard` with `.newTab`), so the U1-2
    /// dedup rule applies unchanged: an item already open in the target
    /// group activates the existing tab and the popped record is simply
    /// consumed. A file that no longer exists on disk is announced and
    /// skipped, and the next record is tried (Reopen never resurrects a
    /// dead document). Pane placement: the reopen lands in the group
    /// the tab was closed from when that pane still exists, else the
    /// active group.
    /// The on-disk path for file-backed tab items; nil for id-backed
    /// kinds (saved query / dashboard), whose loaders own missing-state.
    static func fileBackedPath(of item: EditorItem) -> String? {
        switch item {
        case .markdown(let p), .canvas(let p), .base(let p): return p
        case .savedQuery, .dashboard, .graph: return nil
        }
    }

    func reopenClosedTab() {
        guard isVaultOpen else { return }
        while let record = workspace.popClosedTab() {
            // Dead-record skip runs BEFORE any pane-focus movement: a
            // pure skip must be a focus/park no-op. The first draft
            // parked + focused first, so skipping a deleted file left
            // the previous pane's buffer rendered under the destination
            // tab's title (red-team probe, executable repro).
            if case let fileBacked = record.item,
                let path = Self.fileBackedPath(of: fileBacked),
                let vaultURL = currentVaultURL,
                !FileManager.default.fileExists(
                    atPath: vaultURL.appendingPathComponent(path).path)
            {
                postAccessibilityAnnouncement(
                    "\(filename(of: path)) no longer exists.",
                    priority: .medium)
                continue
            }
            if workspace.model.group(record.groupID) != nil,
                workspace.model.activeGroupID != record.groupID {
                // Park the outgoing buffer BEFORE moving group focus —
                // after `focusGroup` the model's active tab is the
                // destination pane's, and the snapshot guard would
                // (correctly) refuse to park the old fields under it.
                parkOutgoingNoteBuffer()
                workspace.focusGroup(record.groupID)
            }
            switch record.item {
            case .markdown(let path), .canvas(let path), .base(let path):
                openFile(path, target: .newTab)
                postAccessibilityAnnouncement(
                    "Reopened \(filename(of: path)).", priority: .medium)
            case .savedQuery(let id, let name):
                openSavedQuery(id: id, name: name, target: .newTab)
                postAccessibilityAnnouncement(
                    "Reopened \(name).", priority: .medium)
            case .dashboard(let id, let name):
                openDashboard(id: id, name: name, target: .newTab)
                postAccessibilityAnnouncement(
                    "Reopened \(name).", priority: .medium)
            case .graph:
                openGraphTab()
                postAccessibilityAnnouncement("Reopened Graph.", priority: .medium)
            }
            return
        }
    }

    /// ⌘⇧] / ⌘⇧[ / ⌘1…⌘9. The model is a value type: compute the target on
    /// a COPY (zero logic duplication), then run the identity funnel.
    func selectNextTab() { activateComputedTab { $0.selectNextTab() } }
    func selectPreviousTab() { activateComputedTab { $0.selectPreviousTab() } }
    func selectTab(ordinal: Int) { activateComputedTab { $0.selectTab(ordinal: ordinal) } }
    func selectTab(id: TabID) { activateTab(id) }

    /// ⌃⌘← / ⌃⌘→ keyboard reorder (the non-drag equivalent to dragging).
    func moveActiveTabLeft() {
        workspace.moveActiveTab(by: -1)
        announceActiveTab(prefix: "Moved tab left.")
    }
    func moveActiveTabRight() {
        workspace.moveActiveTab(by: 1)
        announceActiveTab(prefix: "Moved tab right.")
    }

    private func activateComputedTab(_ compute: (inout WorkspaceModel) -> Void) {
        var preview = workspace.model
        compute(&preview)
        guard let target = preview.activeGroup.activeTabID,
            target != workspace.model.activeGroup.activeTabID
        else { return }
        activateTab(target)
    }

    // MARK: - Frontmatter parts (U3-3, #467/#469)

    /// The exact bytes between the note's frontmatter delimiters ("" when
    /// the note has none) — `read_note_parts.fm_source`. The editor buffer
    /// (`currentNoteText`) is the BODY; every composed save reassembles
    /// through the ONE Rust composer (`compose_note` inside
    /// `save_composed`), never a Swift mirror. Refreshed by load, property
    /// edits, and the U3-4 source editor.
    @Published private(set) var currentNoteFMSource: String = ""

    /// Whole-file → body deltas, straight from `read_note_parts` (computed
    /// Rust-side as `whole − body` at the split point — byte-exact by
    /// construction, NEVER re-derived from any compose rule in Swift: two
    /// composers diverge, the U3-5 law). Backend offsets (headings, task
    /// lines, backlink lines, template cursors) are whole-file; the body
    /// buffer is not — these are the one conversion authority.
    private(set) var bodyByteOffset: Int = 0
    private(set) var bodyLineOffset: Int = 0

    /// Whole-file heading offsets → body space (clamped at 0 — the scanner
    /// only emits body headings, so clamping is defensive, not semantic).
    static func rebasedToBody(_ headings: [Heading], prefixBytes: Int) -> [Heading] {
        guard prefixBytes > 0 else { return headings }
        return headings.map { h in
            var rebased = h
            rebased.byteOffset = UInt32(max(0, Int(h.byteOffset) - prefixBytes))
            return rebased
        }
    }

    /// Whole-file 1-based line → body 1-based line (floor 1) and back.
    func bodyLine(fromFileLine line: Int) -> Int {
        max(1, line - bodyLineOffset)
    }

    func fileLine(fromBodyLine line: Int) -> Int {
        line + bodyLineOffset
    }

    /// Whole-file byte offset → body byte offset (floor 0).
    func bodyByte(fromFileByte byte: Int) -> Int {
        max(0, byte - bodyByteOffset)
    }

    /// Refresh fmSource + offsets (+hash) after an operation rewrote the
    /// file's frontmatter out-of-buffer (property edit, U3-4 source
    /// commit). One `read_note_parts` call; the BODY buffer is never
    /// touched — an fm-only rewrite leaves the on-disk body identical to
    /// `savedBaselineText`, and a dirty buffer keeps its edits. Same-path
    /// parked duplicates mirror the fresh fm so their eventual composed
    /// saves can't resurrect stale frontmatter.
    private func refreshNoteParts(session: VaultSession, path: String) async {
        let parts: NotePartsBundle? = await Task.detached(priority: .userInitiated) {
            try? session.readNoteParts(path: path)
        }.value
        guard let parts, currentSession === session, loadedFilePath == path else { return }
        currentNoteFMSource = parts.fmSource
        bodyByteOffset = Int(parts.bodyByteOffset)
        bodyLineOffset = Int(parts.bodyLineOffset)
        currentNoteContentHash = parts.contentHash
        workspace.mirrorFrontmatter(
            path: path, fmSource: parts.fmSource,
            bodyByteOffset: Int(parts.bodyByteOffset),
            bodyLineOffset: Int(parts.bodyLineOffset),
            contentHash: parts.contentHash)
    }

    // MARK: - View mode (U3-2, #466)

    /// Live caret location in RAW UTF-16, reported continuously by the
    /// editor coordinator (`onCaretUTF16Change`) — a plain Int handoff.
    /// NEVER `@Published` (selection churn is per-keystroke and must not
    /// invalidate views), and never converted here: the UTF-8 conversion
    /// runs ONCE at switch-to-reading time — a per-keystroke rope build
    /// over the whole document is the O(n) class #404 eliminated.
    private var liveEditorCaretUTF16: Int = 0

    /// Caret (UTF-8 byte offset) to restore per tab when its editor
    /// remounts after reading mode (U3-2 spec: "caret preserved from last
    /// editing session of this tab, else {0,0}"). Sparse; cleared on
    /// delivery and on tab close.
    private var editorCaretReturn: [TabID: Int] = [:]

    func noteEditorCaretDidMove(toUTF16 location: Int) {
        liveEditorCaretUTF16 = location
    }

    /// The ACTIVE tab's view mode — what `NoteContentView` renders.
    var activeViewMode: NoteViewMode { workspace.activeViewMode }

    /// #868 menu-title seam: TRUE when the active tab shows reading
    /// mode. A computed read of workspace state — no mirror needed,
    /// because the init-time workspace→appState objectWillChange
    /// bridge re-renders the menu on every mode flip. No tab reads
    /// as false ("Enter Reading Mode", matching `activeViewMode`'s
    /// `.editing` default for an empty workspace).
    var activeTabIsReading: Bool { activeViewMode == .reading }

    /// ⌘⇧E / toolbar toggle: flip the active tab between editing and
    /// reading. No-ops without a renderable note (no tab, load error, or
    /// nothing loaded) — the button and command are enabled in the same
    /// states, but the palette can always invoke.
    func toggleViewMode() {
        guard let tabID = workspace.model.activeGroup.activeTabID,
            loadedFilePath != nil, noteLoadError == nil
        else { return }
        let target: NoteViewMode =
            workspace.viewMode(for: tabID) == .editing ? .reading : .editing
        setViewMode(target, for: tabID)
    }

    /// Mode-flip funnel (toggle button, ⌘⇧E, ReadingView's empty-state
    /// "Switch to Editing"). Captures/restores the editor caret for the
    /// ACTIVE tab, announces, and persists the layout (mode is per-tab
    /// state in workspace.json).
    func setViewMode(_ target: NoteViewMode, for tabID: TabID? = nil) {
        guard let tabID = tabID ?? workspace.model.activeGroup.activeTabID
        else { return }
        guard workspace.viewMode(for: tabID) != target else { return }
        let isActiveTab = tabID == workspace.model.activeGroup.activeTabID
        if isActiveTab, target == .reading {
            // Park the caret before the editor unmounts (converted to a
            // byte offset HERE, once, against the live buffer — human
            // cadence, not keystroke cadence). The buffer stays in
            // AppState's fields — reading renders from it.
            editorCaretReturn[tabID] = EditorTextConversions.byteOffsetForUTF16Location(
                liveEditorCaretUTF16, in: currentNoteText ?? "")
        }
        workspace.setViewMode(target, for: tabID)
        if isActiveTab, target == .editing {
            // Remount: the coordinator's one-shot handler parks the caret,
            // scrolls it visible, and makes the editor first responder
            // (the #421 F-H1 discipline) — focus lands in the new surface.
            cursorByteOffsetRequest.send(
                editorCaretReturn.removeValue(forKey: tabID) ?? 0)
        }
        saveWorkspaceLayout()
        if isActiveTab {
            let announcement = target == .reading ? "Reading mode." : "Editing mode."
            lastViewModeAnnouncement = announcement
            postAccessibilityAnnouncement(announcement)
        }
    }

    /// Last mode-flip announcement (U3-2). Test seam — the announcement
    /// helper is a no-op under XCTest (no NSApp), so the exact string must
    /// be observable here (same pattern as `lastMutationAnnouncement`).
    private(set) var lastViewModeAnnouncement: String?

    // MARK: - Undo/Redo menu observability (#867)

    /// Re-render pulse for the Edit ▸ Undo/Redo titles. The state those
    /// titles derive from lives OUTSIDE appState's published surface —
    /// responder-chain `NSUndoManager`s and `CanvasDocument`'s session
    /// stacks — so this tick republishes "the undo world changed" into
    /// the object the `.commands` builder observes. Bumped (debounced)
    /// by the NSUndoManager-notification pipeline wired in `init` and
    /// by the canvas mutation funnel (`noteUndoStacksChanged`). The
    /// VALUE is meaningless; only the publish matters — the titles
    /// below are computed live at menu render.
    @Published private(set) var undoMenuTick = 0

    /// Feed for the init pipeline above — merged with the NSUndoManager
    /// notifications ahead of the shared debounce.
    private let undoMenuSubject = PassthroughSubject<Void, Never>()

    /// Canvas funnel entry (#867): the canvas undo/redo stacks mutated
    /// (`canvasApply` / `canvasUndo` / `canvasRedo`) — request an
    /// undo-menu re-render pulse. Coalesced by the init pipeline's
    /// debounce, so callers fire per-mutation without menu churn.
    func noteUndoStacksChanged() {
        undoMenuSubject.send()
    }

    /// The undo manager the responder chain would hand ⌘Z to: the key
    /// window's first responder's (NSTextView supplies its own), else
    /// the key window's on-demand manager. Nil under XCTest (no NSApp —
    /// same guard discipline as `postAccessibilityAnnouncement`) or
    /// with no key window at render time.
    var responderChainUndoManager: UndoManager? {
        guard let window = NSApp?.keyWindow else { return nil }
        return window.firstResponder?.undoManager ?? window.undoManager
    }

    /// Edit ▸ Undo title (#867): a canvas-focused tab uses the canvas
    /// stack's recorded verb (the t3 layer-2 names — "create card",
    /// "delete \"X\""); everywhere else NSUndoManager composes its own
    /// localized full title ("Undo Typing") — descriptive labels per
    /// undo-and-redo.md, the-menu-bar.md ("Undo | Append action name").
    var undoMenuItemTitle: String {
        if undoTargetsCanvas {
            return Self.canvasUndoRedoMenuTitle(
                base: "Undo", actionName: activeCanvasDocument?.undoStack.last?.name)
        }
        // #871: the structural (file-op) domain — same composer as canvas,
        // the action name describing the pending op read from the stack top
        // ("Undo Move of Notes.md", "Undo Rename to Draft.md"). An empty
        // stack composes to the bare "Undo".
        if undoTargetsStructural {
            return Self.canvasUndoRedoMenuTitle(
                base: "Undo",
                actionName: structuralUndoStack.last.map(Self.structuralUndoActionName))
        }
        return Self.responderUndoMenuTitle(responderChainUndoManager)
    }

    /// ⇧⌘Z symmetric to `undoMenuItemTitle`.
    var redoMenuItemTitle: String {
        if undoTargetsCanvas {
            return Self.canvasUndoRedoMenuTitle(
                base: "Redo", actionName: activeCanvasDocument?.redoStack.last?.name)
        }
        if undoTargetsStructural {
            return Self.canvasUndoRedoMenuTitle(
                base: "Redo",
                actionName: structuralRedoStack.last.map(Self.structuralUndoActionName))
        }
        return Self.responderRedoMenuTitle(responderChainUndoManager)
    }

    /// Enablement (#867): the responder path mirrors the system Edit
    /// menu — plain, disabled "Undo" when the resolved manager
    /// positively reports `canUndo == false`. An UNRESOLVABLE manager
    /// (no key window at render time — e.g. the app inactive) stays
    /// ENABLED with the plain title: ⌘Z then no-ops harmlessly through
    /// `sendAction`, which beats a stale-disabled item deadening the
    /// chord (first-responder moves don't publish, so a nil read must
    /// never latch the item off). The canvas path stays ALWAYS enabled:
    /// empty-stack ⌘Z announces "Nothing to undo." (t0 §1.3) — a
    /// deliberate VoiceOver affordance a disabled item would silence.
    var undoMenuItemEnabled: Bool {
        if undoTargetsCanvas { return true }
        // #871: the structural domain stays ALWAYS enabled while the tree
        // owns the chord — an empty-stack ⌘Z announces "Nothing to undo."
        // (`structuralUndo()`), the same deliberate VoiceOver affordance the
        // canvas path keeps that a disabled item would silence.
        if undoTargetsStructural { return true }
        guard let manager = responderChainUndoManager else { return true }
        return manager.canUndo
    }

    /// ⇧⌘Z symmetric to `undoMenuItemEnabled`.
    var redoMenuItemEnabled: Bool {
        if undoTargetsCanvas { return true }
        if undoTargetsStructural { return true }
        guard let manager = responderChainUndoManager else { return true }
        return manager.canRedo
    }

    /// Pure title composer (#867), extracted for direct testing:
    /// "Undo" + a canvas action name — "delete \"My Card\"" becomes
    /// "Undo Delete \"My Card\"". Only the LEADING character is
    /// uppercased: the t3 action names embed user-typed card titles
    /// that must pass through verbatim, so full Title Case is off the
    /// table. Nil/empty (empty stack) falls back to the bare verb.
    static func canvasUndoRedoMenuTitle(base: String, actionName: String?) -> String {
        guard let name = actionName, !name.isEmpty else { return base }
        return "\(base) \(name.prefix(1).uppercased())\(name.dropFirst())"
    }

    /// Pure title composer (#867) for the responder path: NSUndoManager
    /// composes and localizes the full title itself ("Undo Typing" via
    /// `undoMenuItemTitle`). With nothing to undo the item reads plain
    /// "Undo", matching its disabled state (system behavior).
    static func responderUndoMenuTitle(_ manager: UndoManager?) -> String {
        guard let manager, manager.canUndo else { return "Undo" }
        return manager.undoMenuItemTitle
    }

    /// Redo twin of `responderUndoMenuTitle`.
    static func responderRedoMenuTitle(_ manager: UndoManager?) -> String {
        guard let manager, manager.canRedo else { return "Redo" }
        return manager.redoMenuItemTitle
    }

    /// The THIRD undo domain (#871): ⌘Z drives the structural (file move /
    /// rename) undo stack when the FILE TREE owns focus.
    ///
    /// Precedence — MUST match the SlateMacApp `.undoRedo` routing and the
    /// title/enablement getters above: canvas FIRST (#372/#867
    /// route-by-active-tab, byte-for-byte unchanged), then structural, then
    /// the responder chain. This property is explicitly FALSE whenever
    /// `undoTargetsCanvas` is true, so the two domains are PROVABLY mutually
    /// exclusive and the render-time title can never bake a different domain
    /// than the press-time action resolves (the documented #867 desync).
    ///
    /// Gated on PUBLISHED state ONLY — `workspace.focusRegion`, which the
    /// #868 `workspace.objectWillChange → appState.objectWillChange` bridge
    /// forwards so `.commands` re-renders when focus crosses into/out of the
    /// tree. NEVER an `NSApp.keyWindow.firstResponder` read for the routing
    /// decision (constraint #871.2): that value is unpublished, so a title
    /// computed from it at render time would desync from the action.
    var undoTargetsStructural: Bool {
        guard !undoTargetsCanvas else { return false }
        // #871 red-team: EXCLUDE the inline tree-rename field. `RenameField`
        // is a TextField descendant of the List that carries
        // `.focused($fileTreeFocused)`, which has focus-WITHIN semantics — so
        // `workspace.focusRegion` stays `.tree` while the user is typing a new
        // name. Without this guard, ⌘Z to fix a typo mid-rename would route to
        // `structuralUndo()` — reversing an UNRELATED prior move/rename on disk
        // AND shadowing the field editor's own text undo (a regression: the
        // pre-#871 `else` branch forwarded `undo:` to that editor). Mirrors the
        // `!isRenaming` guard `treeKeyInterceptionActive` already uses for the
        // List's key interceptors. `renamingNode` is @Published, so the Edit
        // menu re-renders (and the routing flips back) the moment a rename
        // begins or ends — the published-state-only rule is preserved.
        return workspace.focusRegion == .tree && renamingNode == nil
    }

    // MARK: - Workspace persistence (U1-6, #458)

    /// Debounced layout save: any model mutation schedules a write 500ms
    /// out (coalescing bursts); vault close and app termination save
    /// immediately. Set up from init.
    func wireWorkspacePersistence() {
        workspace.$model
            .dropFirst()
            .debounce(for: .milliseconds(500), scheduler: DispatchQueue.main)
            .sink { [weak self] _ in
                self?.saveWorkspaceLayout()
            }
            .store(in: &subscriptions)
        // U4-1 (#470): the active right-pane leaf persists too. Same debounced
        // path as `$model` — a leaf switch coalesces with any concurrent layout
        // churn into one write.
        workspace.$activeLeaf
            .dropFirst()
            .debounce(for: .milliseconds(500), scheduler: DispatchQueue.main)
            .sink { [weak self] _ in
                self?.saveWorkspaceLayout()
            }
            .store(in: &subscriptions)
        // #873: the file tree's expanded-folder set persists too. Same
        // debounced path — an expand/collapse burst coalesces into one
        // write, and any concurrent layout churn shares it.
        $treeExpandedDirPaths
            .dropFirst()
            .removeDuplicates()
            .debounce(for: .milliseconds(500), scheduler: DispatchQueue.main)
            .sink { [weak self] _ in
                self?.saveWorkspaceLayout()
            }
            .store(in: &subscriptions)
        NotificationCenter.default.addObserver(
            forName: NSApplication.willTerminateNotification, object: nil,
            queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated {
                self?.saveWorkspaceLayout()
            }
        }
    }

    /// The file tree's expanded-folder PATHS in recency order (#873):
    /// the sidebar mirrors its view-model's path ledger here on every
    /// expansion change AND explicitly after mutation reconciliation
    /// (id-stable renames change no id set — Codex round 6), a
    /// post-update mutation point (#448 discipline);
    /// `wireWorkspacePersistence` saves it on the shared debounced
    /// path, and `restoreWorkspaceLayout` refills it so the sidebar's
    /// `bind(to:)` rehydrates instead of resetting to [].
    @Published var treeExpandedDirPaths: [String] = []

    func saveWorkspaceLayout() {
        guard let vaultURL = currentVaultURL else { return }
        let store = WorkspaceStore(vaultRoot: vaultURL)
        do {
            try store.save(
                WorkspaceStore.snapshot(
                    of: workspace.model, activeLeaf: workspace.activeLeaf.rawValue,
                    viewModes: workspace.viewModes,
                    propertiesCollapsed: workspace.propertiesCollapsed,
                    canvasSurfaces: workspace.canvasSurfaces,
                    expandedDirPaths: treeExpandedDirPaths))
        } catch {
            // Layout persistence must never interrupt the user; the next
            // clean save wins.
            fputs("Slate: workspace.json save failed: \(error)\n", stderr)
        }
    }

    func restoreWorkspaceLayout() {
        guard let vaultURL = currentVaultURL else { return }
        let store = WorkspaceStore(vaultRoot: vaultURL)
        guard let snapshot = store.load() else { return }
        // Restore the active leaf independently of the tab model: an unknown or
        // absent token (or a leaf not yet registered) falls back to `.outline`
        // (Leaf.init(persisted:)). Assigning before the empty-model guard means
        // the remembered panel survives even a vault reopened with no tabs.
        workspace.activeLeaf = Leaf(persisted: snapshot.activeLeaf)
        // #873: likewise the expanded-folder set — assigned before the model
        // guard so expansion survives a vault reopened with no restorable
        // tabs. The sidebar's bind(to:) consumes this when it rehydrates.
        treeExpandedDirPaths = WorkspaceStore.expandedDirPaths(from: snapshot)
        guard let restored = WorkspaceStore.model(from: snapshot),
            !restored.isEmpty
        else { return }
        workspace.adopt(
            restored, viewModes: WorkspaceStore.viewModes(from: snapshot),
            propertiesCollapsed: WorkspaceStore.propertiesCollapsed(from: snapshot),
            canvasSurfaces: WorkspaceStore.canvasSurfaces(from: snapshot))
        if let tab = workspace.model.activeGroup.activeTabID {
            activateTab(tab)
        }
    }

    // MARK: - Open-in targets (U1-5, #457)

    /// Where a navigation opens its document.
    enum OpenTarget: Equatable {
        /// Replace the active tab's item (the pre-tabs behavior; the dirty
        /// gate applies exactly as before).
        case currentTab
        /// A new tab in the focused group (⌘-click); reuses an existing tab
        /// for the same path in that group rather than duplicating.
        case newTab
        /// A new split pane showing the document (falls back to `.newTab`
        /// at the 6-pane capacity, announced by the split path).
        case newSplit(SplitBranch.Axis)
    }

    /// The single navigation entry point (U1-5). Every open path — sidebar
    /// row, backlink/outgoing-link/embed activation, search result, palette
    /// command — routes here with an explicit target.
    func openFile(_ path: String, target: OpenTarget) {
        // Record the open into the vault's file-recents (#495) at this
        // single funnel — every user-visible open routes here, launch
        // restores (activateTab) do not. Recorded once per call, before
        // the target branch, so all three targets bump recency equally.
        recordFileOpen(path: path)
        // Milestone T (#369): .canvas paths take the canvas arm of the
        // funnel — the note loader must never read a canvas as text.
        if path.lowercased().hasSuffix(".canvas") {
            openCanvasFile(path, target: target)
            return
        }
        // Milestone N (#702): .base paths take the Bases arm of the
        // funnel — the note loader must never read a base as text.
        if path.lowercased().hasSuffix(".base") {
            openBaseFile(path, target: target)
            return
        }
        clearActiveBaseQuickFilter()
        switch target {
        case .currentTab:
            if selectedFilePath != path {
                selectedFilePath = path
            }
        case .newTab:
            if let existing = workspace.activeGroupTab(forPath: path) {
                activateTab(existing.id)
                return
            }
            // Park the outgoing buffer while the fields still describe it,
            // then create + activate the tab; `activateTab` loads the new
            // path (its snapshot guard blocks a double-park: the fields no
            // longer match the new active tab).
            workspace.snapshotActiveTab(
                text: currentNoteText, baseline: savedBaselineText,
                contentHash: currentNoteContentHash,
                hasUnsavedChanges: hasUnsavedChanges,
                saveError: saveError, saveConflict: currentSaveConflict,
                loadedFilePath: loadedFilePath,
                fmSource: currentNoteFMSource,
                bodyByteOffset: bodyByteOffset, bodyLineOffset: bodyLineOffset)
            let id = workspace.openTab(.markdown(path: path))
            activateTab(id)
        case .newSplit(let axis):
            let paneCount = workspace.model.groupsInOrder.count
            splitActivePane(axis: axis)
            if workspace.model.groupsInOrder.count == paneCount {
                // Split rejected (capacity / empty) — the document still
                // deserves to open somewhere visible.
                openFile(path, target: .newTab)
                return
            }
            // The new pane holds a duplicate of the previous document;
            // retarget it (same funnel as a sidebar click in that pane).
            if selectedFilePath != path {
                selectedFilePath = path
            }
        }
    }

    /// Pointer sugar: ⌘-click on any navigation affordance opens in a new
    /// tab. Reads the CURRENT AppKit event, so it must be called
    /// synchronously from the click's action.
    func openTargetFromCurrentEvent() -> OpenTarget {
        if NSApp?.currentEvent?.modifierFlags.contains(.command) == true {
            return .newTab
        }
        return .currentTab
    }

    // MARK: - Split panes (U1-3, #455)

    /// ⌘\ / ⌘⌥\. Duplicate-active-item split; focus lands in the new pane
    /// (the duplicate shares the active document's file, so the active
    /// fields remain valid — no funnel hop needed; the model split already
    /// moved group focus).
    func splitActivePane(axis: SplitBranch.Axis) {
        // A split duplicates the active item into the new pane; for the
        // graph that would violate the workspace-global singleton (round
        // 2 finding 6). Refuse and say why — the graph opens in one pane;
        // to see it beside a note, split from the note instead. (An
        // `openFile(.newSplit)` for a real file falls back to a new tab
        // via its own capacity check.)
        if case .graph = workspace.activeTab?.item {
            postAccessibilityAnnouncement(
                "The graph opens in a single pane. Split from a note instead.",
                priority: .medium)
            return
        }
        // Park the outgoing pane's buffer FIRST: after the split the
        // original tab is unfocused and renders from its parked document —
        // without this it would show the never-visited placeholder.
        workspace.snapshotActiveTab(
            text: currentNoteText, baseline: savedBaselineText,
            contentHash: currentNoteContentHash,
            hasUnsavedChanges: hasUnsavedChanges,
            saveError: saveError, saveConflict: currentSaveConflict,
            loadedFilePath: loadedFilePath,
            fmSource: currentNoteFMSource,
            bodyByteOffset: bodyByteOffset, bodyLineOffset: bodyLineOffset)
        guard let newGroup = workspace.split(workspace.model.activeGroupID, axis: axis)
        else {
            let reason =
                workspace.isAtPaneCapacity
                ? "Pane limit reached — a seventh pane could not stay visible."
                : "Nothing to split — open a note first."
            postAccessibilityAnnouncement(reason, priority: .medium)
            return
        }
        // A split focuses the new EDITOR pane — if focus had been parked in a
        // terminal region (⌘\ while in the tree/leaf), re-seat the region so a
        // subsequent ⌘⌥arrow anchors on this new group (U4-4, #473).
        workspace.markEditorRegionActive()
        announcePaneFocus(newGroup, prefix: "Split. ")
    }

    /// ⌘⌥arrows. Spatial focus move across the three terminal regions
    /// (U4-4, #473): the file tree (westernmost), the editor split groups,
    /// and the right-pane leaf (easternmost). Interior editor↔editor moves
    /// are the model's geometry (`focusNeighbor`), unchanged; this method
    /// wraps that at the two horizontal edges so ⌘⌥← off the leftmost group
    /// enters the tree and ⌘⌥→ off the rightmost group enters the leaf, with
    /// the reverse move returning to the exact group left behind
    /// (`lastFocusedGroup`). Focus is never lost — I7 extended across the
    /// region boundary.
    func focusPane(_ direction: WorkspaceModel.Direction) {
        // Resolve the decision purely (the same resolver the terminal-region
        // census drives), then apply its one effect. The interior neighbor is
        // probed non-mutating so `.none`/terminal outcomes leave the model
        // untouched.
        let neighbor = workspace.peekNeighbor(direction)
        switch workspace.resolveFocusRouting(direction, interiorNeighbor: neighbor) {
        case .editorGroup(let target):
            workspace.focusGroup(target)
            enterEditorGroup(target)
        case .returnToEditor:
            let landed = workspace.focusEditorRegion()
            enterEditorGroup(landed)
        case .enterTree:
            workspace.focusTreeRegion()
            postAccessibilityAnnouncement(
                Self.filesRegionAnnouncement, priority: .medium)
        case .enterLeaf:
            focusLeafRegionRevealingPane()
            postAccessibilityAnnouncement(
                Self.leafRegionAnnouncement(workspace.activeLeaf), priority: .medium)
        case .none:
            return
        }
    }

    /// Focus an editor group after a move landed on it: activate its tab
    /// through the identity funnel (document swap) and announce. Shared by the
    /// interior move and the return-from-terminal paths so both keep the same
    /// funnel + announcement discipline (U1-3 invariant — never bypass
    /// `activateTab`).
    private func enterEditorGroup(_ groupID: GroupID) {
        if let tab = workspace.model.group(groupID)?.activeTabID {
            activateTab(tab)
        }
        announcePaneFocus(groupID)
    }

    /// ⌘⌥= / ⌘⌥-.
    func growFocusedPane() { adjustFocusedPane(by: WorkspaceModel.resizeStep) }
    func shrinkFocusedPane() { adjustFocusedPane(by: -WorkspaceModel.resizeStep) }

    private func adjustFocusedPane(by delta: Double) {
        guard workspace.hasSplits else {
            postAccessibilityAnnouncement("No split panes to resize.", priority: .medium)
            return
        }
        workspace.adjustFocusedPaneWeight(by: delta)
        if let fraction = workspace.focusedPaneFraction {
            postAccessibilityAnnouncement(
                "Pane resized, \(Int((fraction * 100).rounded())) percent.",
                priority: .medium)
        }
    }

    /// Palette-only "Close Pane": close the focused pane's tabs one at a
    /// time through the standard close gates. A dirty tab halts the sweep
    /// with its Save/Discard/Cancel alert; re-invoking after resolution
    /// continues. Closing the last tab collapses the pane (the model rule),
    /// which is also the shortcut-free keyboard path.
    func closeActivePane() {
        guard workspace.hasSplits else { return }
        let group = workspace.model.activeGroup
        for tab in group.tabs.reversed() {
            let dirty =
                tab.id == group.activeTabID
                ? hasUnsavedChanges
                : (workspace.document(for: tab.id)?.hasUnsavedChanges ?? false)
            if dirty {
                pendingTabClose = tab.id
                return
            }
            performCloseTab(tab.id)
            if workspace.model.group(group.id) == nil { return }
        }
    }

    private func announcePaneFocus(_ groupID: GroupID, prefix: String = "") {
        guard let group = workspace.model.group(groupID),
            let ordinal = workspace.model.ordinal(of: groupID)
        else { return }
        let total = workspace.model.groupsInOrder.count
        let title = group.activeTab.map { filename(of: workspace.tabPath($0)) } ?? "empty"
        postAccessibilityAnnouncement(
            Self.editorPaneAnnouncement(ordinal: ordinal, total: total, title: title, prefix: prefix),
            priority: .medium)
    }

    // MARK: - Focus-routing announcement strings (U4-4, #473) — pure & testable
    //
    // Verbatim per u4_spec §U4-4. Factored out as pure builders so the
    // announcement tests assert the exact string a VoiceOver user hears
    // (the free `postAccessibilityAnnouncement` has no test spy — the string
    // is the contract). The "Editor pane N of M, <title>." form is REUSED
    // from U1-3 unchanged.

    /// "Editor pane N of M, <title>." (U1-3 format, reused). `prefix` carries
    /// the "Split. " lead on a fresh split; empty for a plain focus move.
    static func editorPaneAnnouncement(
        ordinal: Int, total: Int, title: String, prefix: String = ""
    ) -> String {
        "\(prefix)Editor pane \(ordinal) of \(total), \(title)."
    }

    /// "<leaf title> panel." — spoken when ⌘⌥→ enters the leaf region.
    /// Matches the leaf-switch phrasing (`RightPaneView.activate`) so entering
    /// the leaf and switching leaves read identically.
    static func leafRegionAnnouncement(_ leaf: Leaf) -> String {
        "\(leaf.title) panel."
    }

    /// "Files." — spoken when ⌘⌥← enters the file-tree region.
    static let filesRegionAnnouncement = "Files."

    private func announceActiveTab(prefix: String) {
        guard let tab = workspace.activeTab else { return }
        let group = workspace.model.activeGroup
        let index = (group.tabs.firstIndex { $0.id == tab.id }).map { $0 + 1 } ?? 0
        postAccessibilityAnnouncement(
            "\(prefix) \(filename(of: workspace.tabPath(tab))), tab \(index) of \(group.tabs.count).",
            priority: .medium)
    }

    /// UTF-8 text of the currently-selected note. Nil while no note
    /// is selected or while loading is in flight. Writable so the
    /// editor's two-way `Binding<String>` can update the buffer
    /// directly via `updateEditorText(_:)`.
    @Published var currentNoteText: String?
    /// The last-saved (or freshly-loaded) version of the current
    /// note. `hasUnsavedChanges` is derived from
    /// `currentNoteText != savedBaselineText`. Stored separately so
    /// the editor can compare without losing the live buffer.
    @Published private(set) var savedBaselineText: String?
    /// blake3 hex digest of the file's content at the moment we
    /// loaded it (or last saved it). Used as
    /// `expectedContentHash` in `saveCurrentNote` so an external
    /// writer is caught via `WriteConflict`.
    @Published private(set) var currentNoteContentHash: String?
    /// Path of the note whose text+hash we last successfully loaded.
    /// Distinct from `selectedFilePath`: the latter reflects the
    /// user's UI intent (and the file-list selection), while
    /// `loadedFilePath` is what's actually in `currentNoteText`. The
    /// two diverge while a dirty save-changes prompt is open.
    @Published private(set) var loadedFilePath: String?
    /// True when the editor buffer differs from the on-disk
    /// baseline. Drives the toolbar indicator, Cmd+S enablement,
    /// and the save-or-discard prompts triggered by navigation
    /// while dirty.
    @Published private(set) var hasUnsavedChanges: Bool = false
    /// Populated when `saveCurrentNote` returns `WriteConflict`.
    /// Drives the "Keep mine / Reload from disk / Cancel" alert in
    /// `MainSplitView`.
    @Published var currentSaveConflict: SaveConflict?
    /// Set when the user requests navigation (close-vault, switch
    /// file) while `hasUnsavedChanges == true`. Drives the
    /// "Save changes?" prompt. Nil otherwise.
    @Published var pendingNavigation: PendingNavigation?
    /// Surfaced when `saveCurrentNote` fails with anything other
    /// than `WriteConflict` (which goes through `currentSaveConflict`
    /// instead). Independent of `noteLoadError` so a load alert
    /// doesn't shadow a save alert.
    @Published var saveError: String?
    /// True while a save is in flight. Disables Cmd+S to keep the
    /// user from queuing overlapping saves.
    @Published private(set) var isSaving: Bool = false
    /// Handle on the in-flight save task. Exposed (internal) so
    /// tests can `await state.saveTask?.value`.
    private(set) var saveTask: Task<Void, Never>?
    /// Parsed Markdown headings of the currently-selected note, in
    /// document order. Empty while no note is selected (or when the
    /// note has no `#` headings).
    @Published private(set) var currentNoteHeadings: [Heading] = []
    /// True while the selected note's content is being read from disk.
    @Published private(set) var isLoadingNote: Bool = false
    /// Surfaced when reading the selected note fails. Independent of
    /// `lastError` (open path) and `scanError` (indexing path) so the
    /// UI alerts don't cross-fire.
    @Published var noteLoadError: String?

    /// Last outcome from `openLink` / `openBacklink`. Exposed for
    /// tests so they can verify activation routing without observing
    /// AppKit side effects (NSWorkspace open, accessibility
    /// announcements). UI doesn't read this.
    private(set) var lastActivatedLinkOutcome: LinkActivationOutcome?

    /// Last line scrolled to via `openSearchResult`. Same role as
    /// `lastActivatedLinkOutcome` — verifiable in tests without
    /// observing AppKit side effects.
    private(set) var lastActivatedSearchResultLine: Int?
    private(set) var lastActivatedSearchResultPath: String?

    /// Frontmatter properties of the currently-selected note, in
    /// document order. Empty while no note is selected, while the
    /// fetch is still in flight, or when the note has no frontmatter.
    /// Loaded by `loadCurrentLinks(path:)` (which already runs a
    /// `get_file_metadata` under the SQLite mutex) so we don't pay
    /// for two trips through the lock per selection.
    @Published private(set) var currentNoteProperties: [Property] = []

    /// Conflict from `setProperty` / `deleteProperty`. Drives the
    /// "Property edit blocked" alert in `MainSplitView`. Modeled on
    /// `currentSaveConflict` but scoped to a single key edit rather
    /// than a whole-file save so the resolve actions can re-issue
    /// the original property edit verbatim.
    @Published var currentPropertyEditConflict: PropertyEditConflict?

    /// Surfaced when `setProperty` / `deleteProperty` fails with
    /// something other than `WriteConflict` — invalid value, malformed
    /// frontmatter, etc. Separate from `saveError` so a property
    /// failure can't shadow an editor save alert and vice versa.
    @Published var propertyEditError: String?

    /// True while a property set / delete / rename is in flight.
    /// Used by the row editors + sheets to disable commit buttons.
    @Published private(set) var isEditingProperty: Bool = false

    /// Handle on the in-flight property edit task. Exposed (internal)
    /// so tests can `await state.propertyEditTask?.value`.
    private(set) var propertyEditTask: Task<Void, Never>?

    /// True when the Add-Property sheet is presented. Driven by
    /// `PropertiesPanel`'s header button.
    @Published var isAddPropertySheetOpen: Bool = false

    /// True when the Bulk-Rename sheet is presented. Driven by
    /// `PropertiesPanel`'s header button + Cmd+Shift+R shortcut.
    @Published var isBulkRenameSheetOpen: Bool = false

    /// True when the command palette is presented (Milestone Q
    /// #313). Opened by `⌘⇧P` from the `SlateMacApp` menu, routed
    /// through `requestCommandPalette()` which only flips this when a
    /// vault is open (the menu item stays enabled on the welcome screen
    /// so the chord gives feedback instead of being a silent no-op).
    /// Closed by `Esc` (intercepted by the palette's keyDown monitor)
    /// or the cancel-action button. The palette registry, fuzzy filter,
    /// sections, and recents arrive in #314–#316.
    ///
    /// Reset to `false` by `closeVault()` so a vault close with
    /// the palette open doesn't leave the bool stuck (and re-
    /// trigger the sheet on the next vault open).
    @Published var isCommandPaletteOpen: Bool = false

    /// Active Bases query-builder draft (Milestone N4-1, #707).
    /// Non-nil presents `BaseQueryBuilderSheet`; the draft is in-memory
    /// only until N4-2 adds preview/save.
    @Published var activeBaseQueryBuilder: BaseQueryBuilderModel? {
        didSet {
            guard let previous = oldValue else { return }
            if let current = activeBaseQueryBuilder, previous === current { return }
            baseQueryBuilderPreviewGeneration += 1
            baseQueryBuilderPreviewTask?.cancel()
            baseQueryBuilderPreviewTask = nil
            baseQueryBuilderPreviewCancelToken?.cancel()
            baseQueryBuilderPreviewCancelToken = nil
        }
    }
    var baseQueryBuilderPreviewTask: Task<Void, Never>?
    var baseQueryBuilderPreviewCancelToken: CancelToken?
    /// Monotonic freshness identity for builder preview publication. Every
    /// schedule and builder/vault teardown invalidates all earlier work.
    var baseQueryBuilderPreviewGeneration = 0
    /// N4-02 deterministic execution seam. Always nil in production; tests
    /// record native-call executor context and can park a real opened handle.
    var baseQueryBuilderPreviewExecutionObserver:
        (@Sendable (BaseQueryBuilderPreviewExecutionEvent) async -> Void)?

    /// Drives the quick switcher sheet (U1-5 follow-up #495). ⌘O opens
    /// it (via `openQuickSwitcher()`, vault-gated; chord moved ⌘T→⌘O by
    /// #863); Esc / opening a file closes it. Reset in `closeVault()`
    /// for the same stuck-bool reason as `isCommandPaletteOpen` —
    /// enforced by `CloseVaultSheetParityTests`.
    @Published var isQuickSwitcherOpen: Bool = false

    /// Latest `RenameReport` from `previewPropertyRename` (dry-run)
    /// or `applyPropertyRename` (applied). The bulk-rename sheet
    /// renders this in its accessible data grid.
    @Published private(set) var pendingRenameReport: RenameReport?

    /// True while a rename preview / apply call is in flight.
    @Published private(set) var isRenameInFlight: Bool = false

    /// Surfaced when `previewPropertyRename` / `applyPropertyRename`
    /// fails outright (not the per-file `RenameFailed` rows — those
    /// land in `pendingRenameReport.failed`).
    @Published var renameError: String?

    /// Handle on the in-flight rename task. Exposed for tests + so
    /// the sheet's Esc handler can cancel via the token.
    private(set) var renameTask: Task<Void, Never>?

    /// Cancellation token for the in-flight rename. Recreated per
    /// call so a stale `cancel()` from a previous sheet open doesn't
    /// kill a fresh request.
    private var renameCancelToken: CancelToken?

    /// Inbound links to the currently-selected note. Updated whenever
    /// `selectedFilePath` changes. Empty while no note is selected or
    /// while the query is still in flight.
    @Published private(set) var currentBacklinks: [Backlink] = []
    /// Outgoing links from the currently-selected note — resolved,
    /// unresolved, and external in document order. Same lifecycle as
    /// `currentBacklinks`.
    @Published private(set) var currentOutgoingLinks: [OutgoingLink] = []
    /// Which note `currentOutgoingLinks` belongs to (Codex round 2).
    /// The array is intentionally RETAINED across note transitions
    /// (#90 panel anti-flicker), so the reading surface needs this
    /// ownership marker to refuse classifying/activating against a
    /// previous note's records mid-transition. Stamped only by the
    /// query landing (both arms), cleared with the array.
    @Published private(set) var currentOutgoingLinksPath: String?
    /// True while a backlinks/outgoing fetch is in flight. The panels
    /// use this to decide whether to show a `ProgressView`.
    @Published private(set) var isLoadingLinks: Bool = false
    /// Surfaced when the link queries fail (rare — would mean SQLite
    /// itself errored). Independent of `noteLoadError` so the panels'
    /// alert state doesn't cross-fire with the content pane's.
    @Published var linksLoadError: String?

    // MARK: Embeds (#187 — Milestone J UI)

    /// Resolved `EmbedResolution` for each `![[…]]` reference in the
    /// currently-loaded note, keyed by the raw target string
    /// (including any `#heading` / `^block` suffix). Populated by
    /// `loadCurrentNoteEmbedResolutions(path:)` after a selection
    /// change (chained on the back of `linksLoadTask`).
    ///
    /// Refresh shape: the cache is cleared on note-to-note
    /// transitions and re-populated when the next chained load
    /// resolves — same lifecycle as `currentBacklinks` /
    /// `currentOutgoingLinks` (and like them, NOT refreshed inline
    /// post-save; a save triggers a selection re-resolve via the
    /// scanner). Audit #205 corrected an earlier doc claim of
    /// "refreshed after a save" that didn't reflect the code path.
    ///
    /// Empty while no note is selected, while the resolutions are
    /// loading, or when the note has no embeds.
    @Published private(set) var currentNoteEmbedResolutions: [String: EmbedResolution] = [:]
    /// True while the embed-resolution batch is in flight.
    @Published private(set) var isLoadingEmbeds: Bool = false
    /// Surfaced when one of the per-embed resolves throws. Per-embed
    /// failures land in the resolution itself as
    /// `EmbedResolution::Unresolved` — this string only fires when
    /// the whole batch can't run (e.g. SQLite mutex held by the
    /// scanner errored). Independent of `linksLoadError`.
    @Published var embedsLoadError: String?
    /// Handle on the in-flight embed-resolution task so tests can
    /// `await state.embedsLoadTask?.value` for a settled state.
    private(set) var embedsLoadTask: Task<Void, Never>?

    /// Discriminated state for the editor's Cmd+E embed-preview
    /// popover. The editor's keyDown handler computes the embed
    /// at cursor and calls `requestEmbedPreview(target:)`; the
    /// popover dismisses via `dismissEmbedPreview()`. Nil when
    /// no popover is active.
    @Published var pendingEmbedPreview: EmbedPreview?

    // MARK: Content pipelines (#223 — Milestone K)

    /// Math blocks parsed + rendered (LaTeX → MathML → MathCAT
    /// speech + braille) for the currently-loaded note. Loaded by
    /// `loadCurrentNoteMathBlocks(path:)` after a selection change,
    /// chained in parallel off `linksLoadTask` alongside code +
    /// diagram. Cleared on selection change.
    @Published private(set) var currentNoteMathBlocks: [MathBlock] = []
    @Published private(set) var isLoadingMathBlocks: Bool = false
    @Published var mathBlocksLoadError: String?
    private(set) var mathBlocksLoadTask: Task<Void, Never>?

    /// Code blocks parsed + highlighted (tree-sitter) for the
    /// currently-loaded note. Same lifecycle as `currentNoteMathBlocks`.
    @Published private(set) var currentNoteCodeBlocks: [CodeBlock] = []
    @Published private(set) var isLoadingCodeBlocks: Bool = false
    @Published var codeBlocksLoadError: String?
    private(set) var codeBlocksLoadTask: Task<Void, Never>?

    /// Mermaid diagram blocks parsed + rendered (SVG + structured
    /// description) for the currently-loaded note. Same lifecycle
    /// as `currentNoteMathBlocks`.
    @Published private(set) var currentNoteDiagramBlocks: [DiagramBlock] = []
    @Published private(set) var isLoadingDiagramBlocks: Bool = false
    @Published var diagramBlocksLoadError: String?
    private(set) var diagramBlocksLoadTask: Task<Void, Never>?

    /// Citations rendered for the currently-loaded note. Each entry
    /// is a `RenderedCitation` carrying both `visual_text` (sighted)
    /// and `speech_text` (AT) — the renderer (#277) built both from
    /// structured data so the AT form never spells punctuation.
    /// Same lifecycle as `currentNoteMathBlocks`: cleared on
    /// selection change, repopulated by `loadCurrentNoteCitations`.
    @Published private(set) var currentNoteCitations: [RenderedCitation] = []

    /// Parallel array of the structured `CitationReference`s the
    /// rendered list came from. The `RenderedCitation` FFI shape
    /// doesn't surface the per-site `citations` array, so any
    /// caller that needs all keys at a multi-citation site (the
    /// summary sheet's unique-source count, the rotor's positional
    /// index, etc.) reads from here instead.
    @Published private(set) var currentNoteCitationRefs: [CitationReference] = []
    @Published private(set) var isLoadingCitations: Bool = false
    @Published var citationsLoadError: String?
    private(set) var citationsLoadTask: Task<Void, Never>?

    /// The style id that `loadCurrentNoteCitations` renders against.
    /// Derived from `.slate/prefs.json`'s `default_style` (or empty
    /// if no bibliography is configured). The style-switching command
    /// in #281 mutates this; the `didSet` re-triggers a render.
    @Published var activeStyleId: String = "" {
        didSet {
            guard activeStyleId != oldValue, let path = selectedFilePath else { return }
            citationsLoadTask?.cancel()
            citationsLoadTask = Task { [weak self] in
                await self?.loadCurrentNoteCitations(path: path)
            }
        }
    }

    /// Citation the user wants to expand (row activation in
    /// `CitationsPanel`, or an inline citation click in Reading mode).
    /// A `CitationsPanel` row presents the `CitationPopover` anchored to
    /// itself (#878); Reading mode has no SwiftUI anchor for an inline
    /// glyph, so it falls back to the detached presentation in
    /// `MainSplitView` gated on `expandedCitationRowAnchored` below.
    @Published var expandedCitation: RenderedCitation? {
        didSet {
            // #878 red-team: many paths clear this EXTERNALLY (⌘J jump-to-
            // bib, note-switch `clearTransitionSensitiveCollections`, vault
            // close) without going through `CitationsPanel.dismissExpansion`,
            // which is the only site that would reset the anchor. A stale
            // `true` here would let the panel row popover AND the detached
            // fallback both present on the next Reading-mode click. Reset the
            // discriminator whenever the expansion clears so the two gates
            // stay mutually exclusive.
            if expandedCitation == nil { expandedCitationRowAnchored = false }
        }
    }

    /// Discriminates the two `expandedCitation` triggers (#878). A
    /// `CitationsPanel` row sets this `true` so it owns the anchored
    /// `.popover` and `MainSplitView`'s detached fallback stays closed;
    /// Reading mode leaves it `false` so the fallback presents (an inline
    /// text glyph has no anchor a popover can point at). Rides
    /// `expandedCitation`'s publish — the two always change together, and
    /// its `didSet` above resets this on any external clear.
    var expandedCitationRowAnchored: Bool = false

    // MARK: - Bibliography (Milestone L, #280)

    /// Full merged bibliography for the open vault. Populated by
    /// `loadBibliographyEntries` after `setBibliographySources` runs
    /// and on session open. Empty when no bibliography is configured.
    @Published private(set) var bibliographyEntries: [BibEntry] = []
    @Published private(set) var isLoadingBibliography: Bool = false
    @Published var bibliographyLoadError: String?

    /// Vault-wide unresolved citation list: `(file_path, key)` pairs
    /// whose key has no matching `bibliography_entries` row. Bound
    /// by the BibliographyPanel's Unresolved segment.
    @Published private(set) var unresolvedCitations: [UnresolvedCitation] = []

    /// Search query bound to the BibliographyPanel's search field.
    /// The panel observes this and filters the entries list — kept on
    /// AppState (not local @State) so the panel survives sidebar tab
    /// switches without losing the query.
    @Published var bibliographySearchText: String = ""

    /// Bibliography entry the user wants to expand from the
    /// BibliographyPanel (vs. the per-note `expandedCitation` set by
    /// CitationsPanel). Presents the `CitationPopover` anchored to the
    /// triggering entry row (#878) — we wrap the entry in a synthetic
    /// `RenderedCitation` so the popover renders without a separate code
    /// path. Purely BibliographyPanel-driven (no Reading-mode path), so
    /// there is no detached fallback.
    @Published var expandedBibEntry: BibEntry?

    /// Vault-relative paths of every file that cites the
    /// currently-being-inspected key (right-click → Show files citing).
    /// Empty until the user requests this; cleared on tab switch /
    /// vault close.
    @Published var filesCitingResult: [String]?

    /// Bibliography prefs in memory — the Settings panel's binding
    /// surface (#281). On vault open we read the disk copy via
    /// `loadBibliographyPrefsFromDisk`; on every mutation the
    /// caller writes back via `applyBibliographyPrefs` (which both
    /// persists to disk AND calls `setBibliographySources` so the
    /// session re-loads).
    @Published var bibliographyPrefs: BibliographyPrefs = .empty

    /// CSL styles configured in `prefs.json` — populated by
    /// `refreshAvailableCslStyles`. Bound by the Settings tab's
    /// style picker and by the View → Citation Style menu in #282.
    @Published var availableCslStyles: [CslStyleInfo] = []

    /// Settings UI surface — when non-nil, an inline error is shown
    /// (e.g. "library.bib not found").
    @Published var bibliographySettingsError: String?

    /// Drives the Citation Summary sheet (Cmd+Shift+J / #282). `true`
    /// while the sheet is presented; the sheet body reads from
    /// `currentNoteCitations` to compute counts.
    @Published var isCitationSummaryOpen: Bool = false

    /// Set by the Jump-to-Bibliography command (#282 / Cmd+J) from a
    /// focused citation. When non-nil, the BibliographyPanel switches
    /// to the Entries segment and filters to this key. Cleared after
    /// the panel handles it.
    @Published var pendingBibliographyKeyFocus: String?

    /// Per-user math rendering preferences. UI panels bind to this;
    /// the `didSet` re-triggers the math-block load for the current
    /// path so settings changes propagate to the rendered output.
    ///
    /// **Note**: `MathPrefs` is a Swift-side mirror of the Rust
    /// `slate_core::math::MathPrefs` — the FFI doesn't surface the
    /// struct directly (only its enum components, which the FFI
    /// mirrors as `MathSpeechStyle`, `MathVerbosity`, `BrailleCode`).
    /// The session's `get_math_blocks` reads prefs from
    /// `SessionConfig.math_prefs` captured at session-open time;
    /// hot-swapping requires a session-side setter that the
    /// Settings PR (#224) lands. For #223 the UI state path is
    /// wired but the rendered output still uses session defaults
    /// until #224 is in.
    @Published var mathPrefs: MathPrefs = MathPrefs() {
        didSet {
            // Audit #257 L1: SwiftUI bindings (a Picker writing
            // back through `$mathPrefs.speechStyle`) can re-fire
            // the setter with the same value on view rebuilds.
            // Skip the loader spin-up if nothing actually changed.
            guard oldValue != mathPrefs else { return }
            preferencesStore.saveMathPrefs(mathPrefs)
            // Audit #259: push the new prefs into the open session
            // so subsequent `get_math_blocks` calls render with
            // them. Without this the Picker update was UI-only —
            // the backend kept the prefs captured at session-open
            // time. `setMathPrefs` throws VaultError; the only
            // realistic failure is a poisoned mutex, which is
            // unrecoverable — log and continue, the next loader
            // call will still use the cached prefs.
            if let session = currentSession {
                do {
                    try session.setMathPrefs(prefs: mathPrefs)
                } catch {
                    fputs(
                        "Slate: session.setMathPrefs failed: \(error)\n",
                        stderr
                    )
                }
            }
            // Audit #261 H1 (WCAG 4.1.3): when the user changes a
            // Picker in Settings, SwiftUI silently re-renders the
            // live preview's accessibilityLabel — VoiceOver won't
            // re-announce it unless focus is on that element. Post
            // an AT announcement describing what changed so users
            // hear confirmation regardless of focus location.
            announceMathPrefsDiff(from: oldValue, to: mathPrefs)
            guard let path = selectedFilePath else { return }
            mathBlocksLoadTask?.cancel()
            // Red-team M2: also cancel an in-flight
            // refresh-after-save task whose payload would otherwise
            // land AFTER the prefs-driven reload and clobber the
            // new-prefs result with the old-prefs blocks.
            mathBlocksRefreshTask?.cancel()
            mathBlocksRefreshTask = nil
            mathBlocksLoadTask = Task { [weak self] in
                await self?.loadCurrentNoteMathBlocks(path: path)
            }
        }
    }

    /// Compose a brief diff announcement for `mathPrefs` changes.
    /// Only one of the three fields can change per Picker selection
    /// (UI surface uses three separate Pickers), so the announcement
    /// reads like "Speech style: MathSpeak" — short, focused,
    /// announces what the user just did. (Audit #261 H1.)
    private func announceMathPrefsDiff(from old: MathPrefs, to new: MathPrefs) {
        if old.speechStyle != new.speechStyle {
            postAccessibilityAnnouncement(
                "Math speech style: \(new.speechStyle.displayName).",
                priority: .medium
            )
        } else if old.verbosity != new.verbosity {
            postAccessibilityAnnouncement(
                "Math verbosity: \(new.verbosity.displayName).",
                priority: .medium
            )
        } else if old.brailleCode != new.brailleCode {
            postAccessibilityAnnouncement(
                "Math braille code: \(new.brailleCode.displayName).",
                priority: .medium
            )
        }
    }

    /// Code pipeline preferences. Controls the AT preamble's
    /// verbosity on rendered code blocks. Settings panel #224
    /// drives this. The CodeBlockView UI binding to this is V1.x
    /// follow-up — today the view uses the same default
    /// (`preambleOnly`) the type carries.
    @Published var codePrefs: CodePrefs = CodePrefs() {
        didSet {
            guard oldValue != codePrefs else { return }
            preferencesStore.saveCodePrefs(codePrefs)
            if oldValue.verbosity != codePrefs.verbosity {
                postAccessibilityAnnouncement(
                    "Code preamble verbosity: \(codePrefs.verbosity.displayName).",
                    priority: .medium
                )
            }
        }
    }

    // MARK: Editor text zoom (#848)

    /// The discrete zoom ladder for in-app editor text zoom. 1.0 is
    /// the body-text-style base size (which itself tracks the system
    /// Text Size — the two COMPOSE: zoom multiplies on top, it never
    /// replaces the accessibility floor). Discrete rungs, not a free
    /// multiplier, so ⌘=/⌘− feel stepped like every macOS zoom and
    /// the announced percentages are stable round numbers.
    static let editorTextScaleSteps: [Double] = [0.9, 1.0, 1.1, 1.25, 1.4, 1.6]

    /// The persisted in-app editor text zoom factor (#848), applied by
    /// the monospaced EDITOR/CODE surfaces via
    /// `Tokens.Typography.monospacedBodyNSFont(scale:)` — the NSTextView
    /// note editor, the code-blocks panel, and the properties-source
    /// YAML editor. Deliberately NOT the reading surface: reading-mode
    /// prose is Dynamic-Type-backed and tracks the system Text Size;
    /// zooming it too would double-scale (see the Zoom In menu item's
    /// boundary note in `SlateMacApp`). `@Published` so the hosting
    /// views re-render (and their `updateNSView` re-derives the font)
    /// on change. Loaded from `PreferencesStore` in `init`; every
    /// mutation routes through `setEditorTextScale`.
    @Published private(set) var editorTextScale: Double = 1.0

    /// ⌘= with no canvas tab active (focus-routed in `SlateMacApp`).
    func editorZoomIn() { stepEditorTextScale(+1) }

    /// ⌘− with no canvas tab active.
    func editorZoomOut() { stepEditorTextScale(-1) }

    /// ⌘0 with no canvas tab active — reset to the base size.
    func editorActualSize() { setEditorTextScale(1.0) }

    /// Nearest ladder rung to an arbitrary (possibly hand-edited)
    /// stored value. Applied at LOAD so the runtime value is always
    /// on-ladder; static + pure for the boundary tests.
    static func nearestEditorTextRung(to value: Double) -> Double {
        editorTextScaleSteps.min {
            abs($0 - value) < abs($1 - value)
        } ?? 1.0
    }

    private func stepEditorTextScale(_ delta: Int) {
        let steps = Self.editorTextScaleSteps
        // DIRECTIONAL rung selection, not nearest-then-offset: from an
        // off-ladder value, nearest-snap could move AGAINST the pressed
        // direction (Codex counterexample: stored 0.5 + Zoom Out
        // snapped up to 0.9). Zoom In → first rung strictly above;
        // Zoom Out → last rung strictly below; pinned at the ends
        // (announcement still fires — never-silent policy).
        let target: Double
        if delta > 0 {
            target = steps.first(where: { $0 > editorTextScale + 0.0001 })
                ?? steps[steps.count - 1]
        } else {
            target = steps.last(where: { $0 < editorTextScale - 0.0001 })
                ?? steps[0]
        }
        setEditorTextScale(target)
    }

    private func setEditorTextScale(_ scale: Double) {
        if editorTextScale != scale {
            editorTextScale = scale
            preferencesStore.saveEditorTextScale(scale)
        }
        // Announce even when pinned at a ladder end (or already at
        // 100%): the keypress must never be silent feedback-wise —
        // the user hears the (unchanged) size instead of nothing.
        postAccessibilityAnnouncement(
            "Editor text size \(Int((editorTextScale * 100).rounded())) percent.",
            priority: .medium)
    }

    // MARK: Editor spell check (#855)

    /// Opt-in live spell checking for the note editor (#855). Default
    /// OFF (see `PreferencesStore.editorSpellCheckKey` — Markdown
    /// source squiggles everywhere). `@Published` so the Edit-menu
    /// checkmark re-renders and `NoteContentView`'s host re-runs
    /// `NoteEditorView.updateNSView` with the new value (the
    /// `editorTextScale` live-application pattern). Loaded from
    /// `PreferencesStore` in `init`; every mutation routes through
    /// `toggleEditorSpellCheck`.
    @Published private(set) var editorSpellCheckEnabled: Bool = false

    /// Edit ▸ Check Spelling While Typing / the palette command.
    /// Persists + announces — the toggle is never silent feedback-wise
    /// (the `setEditorTextScale` policy).
    func toggleEditorSpellCheck() {
        editorSpellCheckEnabled.toggle()
        preferencesStore.saveEditorSpellCheck(editorSpellCheckEnabled)
        postAccessibilityAnnouncement(
            editorSpellCheckEnabled
                ? "Check spelling while typing on."
                : "Check spelling while typing off.",
            priority: .medium)
    }

    // MARK: Restore last vault on launch (#872)

    /// Whether a cold launch auto-reopens the most-recent vault
    /// (launching.md: "Restore previous state on restart … avoid making
    /// people retrace steps"). Mirrors
    /// `PreferencesStore.restoreVaultOnLaunchKey`; default **ON**. The
    /// General settings toggle is the discoverable escape hatch; holding
    /// ⌥ at launch is the transient one. Loaded from `PreferencesStore`
    /// in `init`; every mutation routes through `setRestoreVaultOnLaunch`.
    @Published private(set) var restoreVaultOnLaunch: Bool = true

    /// Settings ▸ General toggle handler — persists the host pref and
    /// mirrors it into the published property the toggle reads. Takes
    /// effect at the NEXT launch (the decision is made once, early).
    func setRestoreVaultOnLaunch(_ enabled: Bool) {
        restoreVaultOnLaunch = enabled
        preferencesStore.saveRestoreVaultOnLaunch(enabled)
    }

    // MARK: Tasks (#113 + #114 — Milestone G UI)

    /// Tasks parsed from the currently-selected note, in document
    /// order. Loaded by `loadCurrentNoteTasks(path:)` after a
    /// selection change and refreshed by `refreshTasksAfterSave` so
    /// the panel reflects toggle results and edit-save round-trips.
    @Published private(set) var currentNoteTasks: [TaskItem] = []
    /// True while the per-note tasks query is in flight. The
    /// `TasksPanel` uses this for its `ProgressView` placeholder.
    @Published private(set) var isLoadingTasks: Bool = false
    /// Surfaced when the per-note tasks query fails. Independent of
    /// `linksLoadError` so the two panels' error states don't
    /// cross-fire.
    @Published var tasksLoadError: String?

    /// Result rows of the most recent vault-wide tasks query.
    /// Populated by `loadVaultTasks()` whenever the review surface
    /// is open AND the filter changes (or the user explicitly
    /// re-loads). `loadMoreVaultTasks()` *appends* to this array
    /// rather than replacing it, so it grows as the user pages
    /// through the result set.
    @Published private(set) var vaultTasks: [TaskWithLocation] = []
    /// True while `loadVaultTasks()` is in flight (i.e. the
    /// initial query for the current filter). The
    /// `TasksReviewPanel` uses this to show a loading placeholder
    /// when there are no rows yet. Held separately from
    /// `isLoadingMoreVaultTasks` so the "Load more" affordance
    /// doesn't replace the empty-state spinner mid-flow.
    @Published private(set) var isLoadingVaultTasks: Bool = false
    /// True while `loadMoreVaultTasks()` is in flight (#160). The
    /// review surface uses this to disable the "Load more" button
    /// and surface a small progress indicator next to it.
    @Published private(set) var isLoadingMoreVaultTasks: Bool = false
    /// Opaque cursor for the next page of `vaultTasks`, returned
    /// by `tasks_in_vault` when the result set extends beyond the
    /// current page. `nil` means the page is the last one — the
    /// "Load more" button hides when this is nil.
    @Published private(set) var vaultTasksNextCursor: String?
    /// Total count of tasks matching the active filter regardless
    /// of pagination. Computed by `tasks_in_vault` in the same
    /// query, so reading it costs the same as fetching the page.
    /// The review surface shows "Showing N of M tasks" against
    /// this when N < M.
    @Published private(set) var vaultTasksTotalFiltered: UInt64 = 0
    /// Surfaced when the vault-wide query fails.
    @Published var vaultTasksLoadError: String?
    /// Active filter for the review surface. Mutating this kicks
    /// off a re-query via `applyTaskReviewFilter(_:)` — direct
    /// writes from the UI go through the setter rather than the
    /// published binding so we can pair them with the load task.
    @Published private(set) var taskReviewFilter: TaskReviewFilter = .all
    // #879: Tasks Review is no longer a modal sheet — it's the `Leaf.tasksReview`
    // right-pane leaf. Its "is showing" state IS `workspace.activeLeaf ==
    // .tasksReview` (with the pane visible), so the old `isTasksReviewOpen`
    // sheet bool is gone. `openTasksReview()` reveals the leaf + kicks the
    // query; there is no sheet bool to reset on vault close.

    /// Right-pane (detail column) visibility (#882). `NavigationSplitView`'s
    /// `columnVisibility` can only hide the sidebar/content columns, never the
    /// DETAIL column, so `MainSplitView` collapses the right pane to zero width
    /// when this is `false` (split-views.md:44 — allow pane hiding, provide a
    /// menu command + keyboard shortcut to reveal). Default visible; session-
    /// scoped (a fresh launch shows the pane). Flipped by View ▸ Hide/Show
    /// Right Pane (⌥⌘I) and the `slate.view.toggleRightPane` palette command.
    @Published var isRightPaneVisible: Bool = true

    /// True while the search overlay is visible. ⇧⌘F toggles it
    /// (since #874; bare ⌘F is now find-in-note); Esc clears.
    @Published var isSearchOpen: Bool = false
    /// Live search query — bound to the overlay's TextField. Every
    /// edit feeds the debouncer; the actual search fires ~150 ms
    /// after the user stops typing.
    @Published var searchQuery: String = ""
    /// Active search scope. `.vault` is the ⇧⌘F default (since #874);
    /// the reading view's tag activation sets `.tag(name:)` (#508) so
    /// the query runs against the `file_tags` dimension. Reset to
    /// `.vault` on overlay close / vault close (in
    /// `closeSearchOverlay()`) — a sticky invisible tag filter would
    /// silently corrupt the next vault search. `private(set)`: callers mutate via `setSearchScope`
    /// / `clearSearchScope` so scope changes always re-arm the search.
    @Published private(set) var searchScope: SearchScope = .vault
    /// Current state of the search overlay's results panel.
    @Published private(set) var searchState: SearchState = .idle
    /// Pre-rendered audio summary for the live region. Mirrors
    /// `searchState`'s results.summary so the SwiftUI .onChange
    /// observer can fire a polite announcement.
    @Published private(set) var searchSummary: String = ""

    /// The (trimmed) query that produced the currently-displayed
    /// `.results` rows — captured when the search resolves, NOT read from
    /// the live `searchQuery`. #876 Codex round 1: the 150 ms debounce
    /// leaves the PREVIOUS query's rows on screen while the field already
    /// holds a newer string; activating one of those rows must record
    /// (and line-anchor against) the query that actually produced it, or
    /// the recent list gets a query that reruns to different results. Nil
    /// whenever the panel is not showing results (idle).
    private(set) var lastResultsQuery: String?

    // MARK: Template flow (Milestone H)

    /// Snapshot of every template under the vault's templates folder
    /// (default `Templates/`) — populated by `openTemplatePicker` and
    /// re-fetched on every reopen so changes the user made on disk
    /// since last invocation show up.
    @Published private(set) var availableTemplates: [TemplateSummary] = []
    /// `true` while the `TemplatePicker` sheet should be presented.
    /// SwiftUI binding target; the picker sets this back to `false`
    /// from its close button + Esc keybinding.
    @Published var isTemplatePickerOpen: Bool = false
    /// State machine for the create-from-template flow. `.idle` when
    /// no flow is active; `.needsPrompts` while the prompt sheet is
    /// up; `.needsName` while the new-note name sheet is up. Driven
    /// by `selectTemplate` / `submitTemplatePrompts` /
    /// `submitTemplateNoteName` / `cancelTemplateFlow`.
    @Published var pendingTemplateFlow: PendingTemplateFlow = .idle
    /// Inline validation error surfaced by the new-note name sheet
    /// (empty / `.` / `..` / absolute paths, render/save failures).
    /// `nil` when the field is in a valid state.
    @Published var templateNoteNameError: String?

    /// One-shot channel for "park the editor cursor at byte offset N
    /// of the freshly-loaded note." Used by the create-from-template
    /// flow to honor a rendered template's `{{cursor}}` marker.
    /// Indexed in UTF-8 bytes — `NoteEditorView`'s coordinator
    /// converts to UTF-16 before talking to `NSTextView`.
    /// #421 (F-H1): a CurrentValueSubject, NOT a PassthroughSubject —
    /// the create-from-template flow sends the `{{cursor}}` offset
    /// right after the note load resolves, which can be a runloop
    /// tick BEFORE SwiftUI materializes the editor and subscribes.
    /// A passthrough drops that event silently and the caret lands
    /// at end-of-text (the VO test's finding); the current-value
    /// replay delivers it on subscribe. `nil` = no pending park;
    /// NoteContentView compactMaps it away and clears the value on
    /// delivery so re-attaching the editor later doesn't re-park.
    let cursorByteOffsetRequest = CurrentValueSubject<Int?, Never>(nil)
    // NOTE: delivery implies first-responder transfer to the editor
    // (placeCursorAtByteOffset) — a future sender inherits that
    // focus-stealing semantic; scope it deliberately.

    /// Clear any pending `{{cursor}}` park. Called on delivery (via
    /// NoteContentView's handleEvents) and on selection change so a
    /// stale offset can never park the caret in a different note.
    func clearPendingCursorByteOffset() {
        if cursorByteOffsetRequest.value != nil {
            cursorByteOffsetRequest.send(nil)
        }
    }

    /// One-shot channel for "scroll the content pane to this heading
    /// anchor." `OutlineSidebar` sends; `NoteContentView` subscribes
    /// via `.onReceive`. PassthroughSubject (not @Published) so
    /// repeated clicks on the same heading re-trigger the scroll
    /// without needing a counter.
    let scrollAnchorRequest = PassthroughSubject<String, Never>()

    /// #874 red-team: one-shot channel for "reveal the note find bar."
    /// `showFindInNote` sends this in editing mode; `NoteEditorView`'s
    /// `.onReceive` makes its `NSTextView` first responder AND opens the
    /// find bar — so ⌘F works regardless of which surface holds focus
    /// (opening a note from the tree leaves the TREE first responder, so
    /// a bare `performTextFinderAction:` up the current chain would miss
    /// the editor).
    let findInNoteRequest = PassthroughSubject<Void, Never>()

    /// One-shot channel for "scroll to line N." Search-result
    /// activation (#59) sends a 1-based line number; the content
    /// pane's `.onReceive` resolves to the `line-<N>` anchor.
    let lineScrollRequest = PassthroughSubject<Int, Never>()

    /// Live wire for the search-text debouncer. Every keystroke
    /// pushes the latest query string; the Combine pipeline waits
    /// 150 ms of inactivity before kicking off
    /// `runSearch(query:)`. Cancellation of any in-flight search
    /// happens at the head of each run so we don't pile up
    /// background queries.
    private let searchQuerySubject = PassthroughSubject<String, Never>()
    /// Handle on the in-flight search task. Exposed (internal) so
    /// tests can `await state.searchTask?.value` to deterministically
    /// observe the post-search state.
    private(set) var searchTask: Task<Void, Never>?
    private var searchCancelToken: CancelToken?

    /// Handle on the in-flight scan + list task kicked off by
    /// `openVault`. Exposed (internal, not Published) so tests can
    /// `await state.scanTask?.value` to deterministically observe the
    /// post-scan state.
    private(set) var scanTask: Task<Void, Never>?

    /// Handle on the in-flight note-load task. Same shape as
    /// `scanTask` so tests can await deterministically.
    private(set) var noteLoadTask: Task<Void, Never>?

    /// Handle on the in-flight links-fetch task. Held separately from
    /// `noteLoadTask` so the panels can stay responsive while the
    /// note content loads, and so tests can await each path
    /// independently.
    private(set) var linksLoadTask: Task<Void, Never>?

    /// Handle on the in-flight per-note tasks fetch. Same shape as
    /// `linksLoadTask`; held separately so the `TasksPanel` can
    /// render its loading state on a different timeline from the
    /// links/properties panels.
    private(set) var tasksLoadTask: Task<Void, Never>?

    /// Handle on the in-flight vault-wide tasks query. Driven by
    /// `loadVaultTasks()` (called from `openTasksReview` and
    /// `applyTaskReviewFilter`). Tests await this to deterministically
    /// observe filter-switch results.
    private(set) var vaultTasksLoadTask: Task<Void, Never>?

    /// Handle on the in-flight "Load more" page (#879 Codex red-team). The
    /// review is now a persistent leaf, so a fresh first-page load (⌘R, a
    /// reveal, a filter change, a vault switch) can race an in-flight
    /// "Load more" — an untracked page task would append its old cursor
    /// page AFTER the fresh page one. Storing the handle lets the fresh-
    /// load paths cancel it; `performLoadMoreVaultTasks`'s `Task.isCancelled`
    /// guard then drops the stale append.
    private(set) var vaultTasksLoadMoreTask: Task<Void, Never>?

    /// Monotonic epoch for the vault-tasks first-page query (#879 Codex
    /// red-team). A CANCELLED/superseded `loadVaultTasks` runs its
    /// unconditional `defer` clear late — without ownership it would wipe a
    /// NEWER load's `isLoadingVaultTasks` (and drop a stale publish onto the
    /// new vault). Each load captures the epoch it bumped; its defer +
    /// publish only fire while it's still the current one.
    private var vaultTasksLoadGeneration: Int = 0

    /// Handle on the most recent per-task toggle. Toggles serialise
    /// through the FFI's session mutex anyway, but holding the
    /// handle here lets tests await the result and surfaces the
    /// in-flight state if the UI ever wants to show a spinner on
    /// the toggled row.
    private(set) var taskToggleTask: Task<Void, Never>?

    /// Handle on the most recent `refreshTasksAfterSave` call.
    /// The refresh re-runs `tasksForFile` off the main actor to
    /// update `currentNoteTasks` after a toggle or save lands.
    /// Production code doesn't await this (the user sees the row
    /// flip when the refresh's @Published write fires); tests
    /// await it instead of using time-based settle waits (#161).
    private(set) var tasksRefreshTask: Task<Void, Never>?

    /// Handles on the most recent post-save content-pipeline
    /// refresh calls. Audit #258 wired three refresh-after-save
    /// helpers (math / code / diagram) so editing a fenced block
    /// updates the displayed cache. Red-team M1: stash the handles
    /// so `closeVault` and the selection-change clear path can
    /// cancel them — otherwise a save-then-immediately-close (or
    /// save-then-switch-files) window leaves orphan tasks running
    /// the off-actor FFI calls.
    private(set) var mathBlocksRefreshTask: Task<Void, Never>?
    private(set) var codeBlocksRefreshTask: Task<Void, Never>?
    private(set) var diagramBlocksRefreshTask: Task<Void, Never>?

    /// Handle on the most recent `reloadEditorBufferAfterToggle`
    /// call. The reload re-reads disk into `currentNoteText` +
    /// `savedBaselineText` so the editor's view of the file
    /// matches the toggled-on-disk state. Same lifecycle shape
    /// as `tasksRefreshTask` — production fire-and-forget,
    /// tests await deterministically (#161).
    private(set) var editorReloadTask: Task<Void, Never>?

    /// Handle on the inner Task spawned by `openTaskRowInEditor`
    /// when activating a row that points to a different file.
    /// The Task waits for the new file's load to settle, then
    /// emits `lineScrollRequest`. Held so tests can await the
    /// scroll emission (#161).
    private(set) var taskRowActivationTask: Task<Void, Never>?

    /// Handle on the most recent template-flow task — opens the
    /// picker (`openTemplatePicker`), reads source for prompt
    /// extraction (`selectTemplate`), or runs the render+save
    /// (`submitTemplateNoteName`). Exposed so tests can await
    /// deterministically on each step of the flow.
    private(set) var templatePickerTask: Task<Void, Never>?
    private(set) var templateSelectionTask: Task<Void, Never>?
    private(set) var templateCreateTask: Task<Void, Never>?

    /// Total announcements fired since the most recent vault was
    /// opened. Internal so the test target can verify the rate-guard
    /// keeps things <= 3/s; the UI never reads it.
    private(set) var scanAnnouncementCount: Int = 0
    /// Most recent message passed to `postAccessibilityAnnouncement`.
    /// Same role as `scanAnnouncementCount` — for tests only.
    private(set) var scanAnnouncementLastMessage: String?

    /// Times `loadBibliographyEntries` has run its fetch since the current
    /// vault opened. Internal, for tests only (the UI never reads it): the
    /// U4-1 leaf-retention test asserts a Bibliography leaf kept mounted
    /// across leaf switches does NOT re-fetch — the load-fire spy that proves
    /// the mounted-ZStack retention actually holds. Same role/convention as
    /// `scanAnnouncementCount`.
    private(set) var bibliographyLoadCount: Int = 0

    /// Most recent message passed through the template-flow
    /// announcement helper (`announceTemplate`). Same role as
    /// `scanAnnouncementLastMessage` — exposed so tests can verify
    /// the polite live region fired without coupling to AppKit's
    /// `NSAccessibility.post` (which is a no-op under the XCTest
    /// runner anyway).
    private(set) var templateAnnouncementLastMessage: String?

    /// Time source for rate-limiting scan announcements. Injectable so
    /// tests can advance simulated time without sleeping.
    var scanClock: () -> Date = { Date() }
    /// Minimum gap between throttled scan announcements. The
    /// acceptance criteria say "no more than ~3 per second" so 350 ms
    /// gives us 2–3 announcements per real-world second with a little
    /// headroom for the synchronous overhead of posting through
    /// AppKit's accessibility bus.
    private let scanAnnouncementMinInterval: TimeInterval = 0.350
    private var scanAnnouncementLastFiredAt: Date = .distantPast

    private var subscriptions: Set<AnyCancellable> = []
    private let recentsStore: RecentVaultsStore
    /// Hand-off for external URLs (http/https/mailto links the user
    /// clicked in a note's outgoing-links panel). Production wires
    /// this to `NSWorkspace.shared.open`; tests inject a no-op so
    /// `XCTest` runs don't actually spawn the user's default browser
    /// every time the suite touches an external-link code path.
    let externalOpener: (URL) -> Bool
    /// Preferences persistence (math + code panels, #224). Injected
    /// at init so tests can substitute a non-standard `UserDefaults`.
    let preferencesStore: PreferencesStore

    /// Command palette registry (Milestone Q #314). Populated in
    /// `init` by `registerCoreCommands` so every menu item /
    /// keyboard shortcut surfaced in `MainSplitView`, `SlateMacApp`,
    /// and `PropertiesPanel` has a matching `Command` the palette
    /// can list and invoke. The id catalogue lives in
    /// `SlateCommands.swift` (`SlateCommandID.all`).
    let commandRegistry: CommandRegistry = CommandRegistry()

    /// Persistent store for the palette's Recent section (#316).
    /// File-backed JSON; in-memory mirror is `commandPaletteRecents`
    /// below. Failures during persistence are non-fatal — recents
    /// is convenience, not critical state.
    private let commandPaletteRecentsStore: CommandPaletteRecentsStore

    /// In-memory snapshot of the recents list, in most-recent-first
    /// order. Refreshed from `commandPaletteRecentsStore` on init
    /// and on every successful command invocation via
    /// `recordCommandInvocation(id:)`.
    @Published private(set) var commandPaletteRecents: [String] = []

    /// In-memory, most-recent-first list of vault-relative file paths
    /// the quick switcher (#495) orders its empty-query list by and
    /// tie-breaks fuzzy matches with. Loaded from the vault's
    /// `FileRecentsStore` on vault open, refreshed on every user-visible
    /// file open via `recordFileOpen(path:)`. Empty while no vault is
    /// open. Unlike `commandPaletteRecents` (a single global store), the
    /// backing store is per-vault, so this is repopulated from
    /// `fileRecentsStore` each time a vault opens.
    @Published private(set) var fileRecents: [String] = []

    /// The current vault's file-recents store, or nil with no vault
    /// open. A computed value rather than a stored field because the
    /// path is vault-relative (`<vault>/.slate/file-recents.json`) and
    /// changes with every vault switch.
    private var fileRecentsStore: FileRecentsStore? {
        currentVaultURL.map { FileRecentsStore(vaultRoot: $0) }
    }

    /// In-memory, most-recent-first list of vault search queries the
    /// overlay's idle state offers as re-runnable "Recent Searches"
    /// (#876; searching.md:37). Loaded from the vault's
    /// `SearchRecentsStore` on vault open, refreshed on every committed
    /// search via `recordSearchRecent(_:)`, wiped by `clearSearchRecents()`
    /// (the privacy affordance, searching.md:38). Empty while no vault is
    /// open — per-vault, like `fileRecents`, since queries are vault-
    /// content-specific.
    @Published private(set) var searchRecents: [String] = []

    /// The current vault's search-recents store, or nil with no vault
    /// open — the `fileRecentsStore` shape (vault-relative path, so a
    /// computed value, repopulated on every vault switch).
    private var searchRecentsStore: SearchRecentsStore? {
        currentVaultURL.map { SearchRecentsStore(vaultRoot: $0) }
    }

    /// Accessibility-announcement seam (M-3, #534; shared with O-5).
    /// The default wraps the global `postAccessibilityAnnouncement`;
    /// tests inject a recording fake to assert the announce gates.
    let announcer: AnnouncementPosting

    init(
        recentsStore: RecentVaultsStore? = nil,
        externalOpener: @escaping (URL) -> Bool = { NSWorkspace.shared.open($0) },
        preferencesStore: PreferencesStore = PreferencesStore(),
        commandPaletteRecentsStore: CommandPaletteRecentsStore? = nil,
        announcer: AnnouncementPosting = AppKitAnnouncementPoster()
    ) {
        self.announcer = announcer
        // Fall back to an in-memory-only store (writes go to a temp
        // path that's discarded on exit) if the standard Application
        // Support location can't be set up. Better degraded than crash
        // on launch.
        if let store = recentsStore {
            self.recentsStore = store
        } else if let store = try? RecentVaultsStore() {
            self.recentsStore = store
        } else {
            let fallback = FileManager.default.temporaryDirectory
                .appendingPathComponent("slate-recent-vaults-fallback.json")
            self.recentsStore = RecentVaultsStore(fileURL: fallback)
        }
        self.externalOpener = externalOpener
        self.preferencesStore = preferencesStore
        // History leaf (O-5): the since-open toggle is a host pref —
        // UI + mark writes only, no core behavior.
        self.historyShowChangesSinceOpen =
            preferencesStore.loadHistoryShowChangesSinceOpen()

        // Command palette recents store — same degraded-fallback
        // pattern as RecentVaultsStore above. Failures here never
        // crash the app; the palette just renders no Recent section.
        if let store = commandPaletteRecentsStore {
            self.commandPaletteRecentsStore = store
        } else if let store = try? CommandPaletteRecentsStore() {
            self.commandPaletteRecentsStore = store
        } else {
            let fallback = FileManager.default.temporaryDirectory
                .appendingPathComponent("slate-command-palette-recents-fallback.json")
            self.commandPaletteRecentsStore = CommandPaletteRecentsStore(fileURL: fallback)
        }
        self.commandPaletteRecents = self.commandPaletteRecentsStore.load()
        // Load persisted preferences AFTER the store is set, BEFORE
        // any other init work that might consume them. The didSet
        // observers haven't been installed yet (they only run on
        // assignment after init), so loading here doesn't recurse.
        self.mathPrefs = preferencesStore.loadMathPrefs()
        self.codePrefs = preferencesStore.loadCodePrefs()
        // #848: persisted editor text zoom — applied on top of the
        // body-text-style base size in `monospacedBodyNSFont(scale:)`.
        self.editorTextScale = Self.nearestEditorTextRung(to: preferencesStore.loadEditorTextScale())
        // #855: persisted spell-check opt-in (default OFF).
        self.editorSpellCheckEnabled = preferencesStore.loadEditorSpellCheck()
        // #872: persisted "reopen last vault at launch" opt-out (default
        // ON). Read here so the Settings ▸ General toggle and the launch
        // decision both see the persisted value; the launch restore
        // itself is triggered by the root view's `.task`, never by init
        // (so constructing an AppState in tests never opens a vault).
        self.restoreVaultOnLaunch = preferencesStore.loadRestoreVaultOnLaunch()
        // #881: persisted "Don't Show Again" opt-out for the compaction-
        // failure alert (default OFF).
        self.compactionAlertSuppressed =
            preferencesStore.loadSuppressCompactionFailureAlert()
        self.baseQueries = BaseQueriesState(
            pinnedSavedQueryIDs: preferencesStore.loadBaseQueryPrefs().pinnedSavedQueryIDs)
        self.recentVaults = self.recentsStore.load()

        // #868: bridge the nested WorkspaceState's change signal into
        // this object's own. `workspace` is a `let` sub-ObservableObject,
        // and SwiftUI's `.commands` menu builder observes only `appState`
        // — so menu content that reads workspace state (the Duplicate
        // Tab / Close Tab `activeTab == nil` enablement, the #868
        // state-reflecting Reading Mode title) never re-evaluated when
        // ONLY the workspace published (the nested-ObservableObject
        // gap). Forwarding objectWillChange closes that seam for every
        // current and future workspace-reading menu item in one wire.
        //
        // #448 (publish-in-view-update) analysis: this forward is a
        // WILL-change relay — synchronous, same call stack — so it
        // publishes in exactly the transaction context of the source
        // publish and adds no hazard beyond those the workspace's own
        // publishes already carry. WorkspaceState's @Published
        // mutations run in action contexts (every mutation funnels
        // through WorkspaceState methods called from AppState command /
        // click handlers — the U1 funnel discipline) or in post-update
        // `.onChange` mirrors (the U4-4 passive region mirrors; the
        // properties-header expansion Binding setter fires on user
        // interaction) — none run inside a view-update transaction,
        // which is the #448 class. If a future workspace writer
        // violates that, the fix belongs at that call site (the
        // FileTreeSidebar local-@State + .onChange pattern), not here.
        workspace.objectWillChange
            .sink { [weak self] _ in self?.objectWillChange.send() }
            .store(in: &subscriptions)

        // #863: mirror the closed-tab stack's emptiness into the
        // published menu-enablement signal. Subscribing to the nested
        // WorkspaceState publisher (rather than updating at each call
        // site) covers every mutation path — close pushes, reopen
        // pops, and the vault close/switch reset — with one wire.
        workspace.$closedTabs
            .map { !$0.isEmpty }
            .removeDuplicates()
            .sink { [weak self] hasRecords in
                self?.canReopenClosedTab = hasRecords
            }
            .store(in: &subscriptions)

        // #867: menu-title observability for Undo/Redo. The state the
        // Edit ▸ Undo/Redo titles derive from lives OUTSIDE appState's
        // published surface — NSUndoManager instances down the
        // responder chain, and CanvasDocument's session stacks — so
        // nothing re-rendered the menu when an undo stack changed.
        // Observe the undo-manager lifecycle notifications with
        // object: nil (ANY manager: the note editor's, the YAML
        // source editor's, a sheet field editor's) plus a window-key
        // bump (key-window changes swap which manager the responder
        // path reads, without any undo notification), and merge the
        // canvas mutation funnel (`noteUndoStacksChanged`). Debounce-
        // lite: the notifications fire per undo GROUP — NSTextView
        // coalesces typing into per-burst groups, so the raw cadence
        // is human-scale already; 50 ms on the main queue folds a
        // group's open/close pair into one tick.
        //
        // #448: the notifications post from undo registration / undo
        // execution (event handling, never view updates), and the
        // debounce hop makes the eventual publish async on the main
        // queue — post-update by construction.
        let undoEventNames: [Notification.Name] = [
            .NSUndoManagerDidOpenUndoGroup,
            .NSUndoManagerDidCloseUndoGroup,
            .NSUndoManagerDidUndoChange,
            .NSUndoManagerDidRedoChange,
            NSWindow.didBecomeKeyNotification,
        ]
        var undoEventPublishers: [AnyPublisher<Void, Never>] = undoEventNames.map {
            NotificationCenter.default.publisher(for: $0, object: nil)
                .map { _ in () }
                .eraseToAnyPublisher()
        }
        undoEventPublishers.append(undoMenuSubject.eraseToAnyPublisher())
        Publishers.MergeMany(undoEventPublishers)
            .debounce(for: .milliseconds(50), scheduler: DispatchQueue.main)
            .sink { [weak self] in self?.undoMenuTick &+= 1 }
            .store(in: &subscriptions)

        // Watch `selectedFilePath` and (re)trigger note loading on
        // every change. Combine's removeDuplicates avoids reloading
        // when the same path is rebound (e.g. by SwiftUI list
        // diffing). The closure runs on the main actor since the
        // class is @MainActor.
        // The note-load cascade below (handleSelectionChange → ~15
        // `@Published` writes) must NOT run inside a SwiftUI update
        // transaction, or it trips a flood of "Publishing changes from
        // within view updates is not allowed, this will cause undefined
        // behavior" — and, per that warning, real UB: the note loaded
        // only intermittently (~50/50). Confirmed via lldb: frame #21
        // ObjectLocation.set(_:transaction:) → selectedFilePath.setter →
        // this sink → handleSelectionChange. Two transaction-context
        // writers are neutralised so the sink can stay synchronous:
        //
        // 1. The file list. It no longer binds `List(selection:)` to this
        //    property directly (which wrote it mid-transaction); it holds
        //    a local `@State` selection and assigns `selectedFilePath`
        //    from `.onChange` — a post-update, safe mutation point. See
        //    FileTreeSidebar.
        // 2. Init. `.dropFirst()` skips the value Combine replays to a new
        //    subscriber (the initial `nil`), which would otherwise fire
        //    handleSelectionChange(nil) inside the StateObject's creation
        //    pass — the same UB on already-empty state. Safe to drop:
        //    there's nothing to load or clear at init.
        //
        // The sink stays SYNCHRONOUS (no `.receive(on:)`): the direct,
        // programmatic writers — search-open, template-create, the
        // dirty-gate rollback, and the XCTest suite — depend on
        // `handleSelectionChange` (hence `noteLoadTask`) running
        // synchronously on assignment.
        $selectedFilePath
            .removeDuplicates()
            .dropFirst()
            .sink { [weak self] path in
                self?.handleSelectionChange(to: path)
            }
            .store(in: &subscriptions)

        // Search query debouncer. 150 ms matches the acceptance
        // criteria and is short enough that typing feels live but
        // long enough that fast typists don't fire one query per
        // keystroke.
        // #422 red-team F1: NO removeDuplicates here. Its dedup
        // memory is pipeline-lifetime, so the overlay's
        // reopen-with-retained-query re-arm emitted the same string
        // and was silently swallowed — the re-arm was dead code.
        // Announcement dedup lives at the view (searchSummary
        // removeDuplicates); re-running an identical 150ms-debounced
        // query is cheap.
        searchQuerySubject
            .debounce(for: .milliseconds(150), scheduler: DispatchQueue.main)
            .sink { [weak self] query in
                self?.runSearch(query: query)
            }
            .store(in: &subscriptions)

        // Populate the command palette registry from the menu /
        // toolbar surfaces (Milestone Q #314). Closures weak-capture
        // `self` to break the appState → registry → action → appState
        // cycle.
        registerCoreCommands(into: commandRegistry, appState: self)
        wireWorkspacePersistence()
    }

    /// Entry point for the "Show Command Palette…" menu command and
    /// its `⌘⇧P` shortcut. Opens the palette when a vault is open;
    /// otherwise posts an accessibility announcement instead of doing
    /// nothing — pressing `⌘⇧P` on the welcome screen used to be a
    /// silent no-op (the menu item was `.disabled`), which is a dead
    /// end for keyboard / VoiceOver users. Found via live debugging.
    ///
    /// The palette is a vault-scoped surface: its sheet is only
    /// mounted by `MainSplitView` (when a vault is open). This method
    /// must NEVER set `isCommandPaletteOpen = true` while no vault is
    /// open — with no sheet mounted the flipped bool would do nothing
    /// now and then auto-present the palette the instant a vault
    /// opened (the #313 / #328 stuck-bool hazard). Branching here lets
    /// the View-menu command stay ENABLED on the welcome screen (so
    /// the keypress gives feedback) while preserving that invariant.
    func requestCommandPalette() {
        guard isVaultOpen else {
            postAccessibilityAnnouncement(
                "Open a vault to use the command palette.",
                priority: .high
            )
            return
        }
        isCommandPaletteOpen = true
    }

    /// Record that a command was invoked through the palette so the
    /// Recent section (#316) can surface it on next open.
    ///
    /// Updates the in-memory mirror unconditionally so the user
    /// sees the recent during this session even if persistence
    /// fails; THEN tries to persist. Disk failure → log + carry on
    /// (recents is convenience, not critical state). The in-memory
    /// session view stays internally consistent; the next session
    /// might silently "forget" the entry if the disk write didn't
    /// land, which is acceptable.
    ///
    /// Concurrency: `AppState` is `@MainActor`-isolated so this
    /// method (and the `@Published commandPaletteRecents` write
    /// inside it) is reachable only from the main actor. Background-
    /// thread callers must `await` or hop to main — the compiler
    /// enforces this at the call site.
    func recordCommandInvocation(id: String) {
        // LRU update on the in-memory mirror — same shape the
        // store's `add` produces, so session view matches what
        // disk would have if the write succeeds.
        var updated = commandPaletteRecents
        updated.removeAll { $0 == id }
        updated.insert(id, at: 0)
        if updated.count > CommandPaletteRecentsStore.maxEntries {
            updated = Array(updated.prefix(CommandPaletteRecentsStore.maxEntries))
        }
        commandPaletteRecents = updated

        // Persist. Atomic write; failure leaves the on-disk file
        // unchanged and the session view ahead of disk.
        do {
            try commandPaletteRecentsStore.save(updated)
        } catch {
            NSLog("Failed to persist command palette recent '\(id)': \(error)")
        }
    }

    /// Entry point for the ⌘O "Quick Open…" command (#495; chord moved
    /// ⌘T→⌘O by #863 — Obsidian's actual quick-switcher default, and
    /// the HIG-truer File ▸ Open). Opens the quick switcher when a
    /// vault is open. With NO vault it falls through to the vault
    /// picker (#863's welcome-screen nicety): the switcher sheet is
    /// only mounted by `MainSplitView`, so the bool must never flip
    /// here (the #313/#328 stuck-bool hazard, same reasoning as
    /// `requestCommandPalette()`) — but where the palette announces
    /// "open a vault first", ⌘O goes one better and OPENS the picker,
    /// so File ▸ Quick Open… is never a dead chord on the welcome
    /// screen. Cost, accepted in the decision record: the "Quick
    /// Open…" label is mildly inaccurate there.
    func openQuickSwitcher() {
        guard isVaultOpen else {
            pickAndOpenVault()
            return
        }
        isQuickSwitcherOpen = true
    }

    /// Record a user-visible file open into the vault's file-recents so
    /// the quick switcher (#495) surfaces it first next time. Called
    /// from ONE choke point — `openFile(_:target:)`, the U1-5 single
    /// navigation entry point every user open (sidebar, links, search,
    /// palette, quick switcher) routes through. Launch-time workspace
    /// restores use `activateTab` directly, NOT `openFile`, so they
    /// never land here — the restored layout doesn't churn recency.
    ///
    /// In-memory-first, persist-second, same as `recordCommandInvocation`:
    /// the session view stays consistent even if the per-vault write
    /// fails (recents is convenience, not critical state). No-op with no
    /// vault (the store is nil).
    func recordFileOpen(path: String) {
        guard let store = fileRecentsStore else { return }
        var updated = fileRecents
        updated.removeAll { $0 == path }
        updated.insert(path, at: 0)
        if updated.count > FileRecentsStore.maxEntries {
            updated = Array(updated.prefix(FileRecentsStore.maxEntries))
        }
        fileRecents = updated
        do {
            try store.save(updated)
        } catch {
            NSLog("Failed to persist file recent '\(path)': \(error)")
        }
    }

    /// Record a committed vault search into the recents so the overlay's
    /// idle state can offer it again (#876; searching.md:37). Called from
    /// ONE choke point — `openSearchResult(_:)`, i.e. when the user
    /// activates a result — the unambiguous "this query was useful"
    /// signal, NOT per-keystroke (recording every debounced prefix would
    /// bury the list in noise). Mirrors `recordFileOpen`'s open-choke-point
    /// discipline: in-memory-first, persist-second, non-fatal on write
    /// failure. Blank / whitespace-only queries are never recorded. No-op
    /// with no vault (the store is nil).
    func recordSearchRecent(_ query: String) {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, let store = searchRecentsStore else { return }
        // Red-team: route the LRU through the store's tested `add`
        // (move-to-front + dedup + cap + atomic persist) rather than
        // re-implementing it here — a single source of truth.
        do {
            searchRecents = try store.add(trimmed)
        } catch {
            NSLog("Failed to persist search recent '\(trimmed)': \(error)")
        }
    }

    /// Forget every remembered search (the idle-state "Clear" affordance,
    /// searching.md:38 privacy note). PERSIST-first, wipe-memory-on-success
    /// — mirroring `recordSearchRecent` so the in-memory list never
    /// diverges from disk. Red-team: clearing memory first and only
    /// NSLog-ing a `clear()` failure would show an empty list while disk
    /// still held the queries, and the very next `add()` (which rebuilds
    /// from `load()`) — or simply reopening the vault — resurrected them,
    /// silently defeating the privacy affordance. On a write failure the
    /// list stays visible (honest: it was NOT forgotten). No-op with no
    /// vault (the store is nil; the in-memory list is already empty).
    func clearSearchRecents() {
        guard let store = searchRecentsStore else {
            searchRecents = []
            return
        }
        do {
            try store.clear()
            searchRecents = []
        } catch {
            NSLog("Failed to clear search recents (list kept): \(error)")
        }
    }

    /// Re-run a remembered query from the overlay's idle state (#876):
    /// drop it into the field and push it through the debouncer, so the
    /// same `searchQuery` → results path a keystroke takes fires.
    func runRecentSearch(_ query: String) {
        searchQuery = query
        bumpSearchQuery()
    }

    /// Push the current `searchQuery` through the debouncer. Called
    /// from the SwiftUI TextField's `.onChange` so the UI doesn't
    /// have to know about the subject.
    func bumpSearchQuery() {
        searchQuerySubject.send(searchQuery)
    }

    /// Toggle the search overlay open / closed. When opening, the
    /// query is preserved so re-opening with a previous query lands
    /// the user back at the same results. When closing, we cancel
    /// the in-flight query so the worker doesn't keep churning
    /// after the user has moved on.
    /// Menu-driven entry point (#422): same behavior as the toolbar
    /// toggle, but guarded for the welcome screen — the menu item is
    /// reachable with no vault open, where the overlay has no host.
    /// Mirrors `requestCommandPalette()`'s guard + feedback pattern.
    func requestSearchOverlay() {
        guard isVaultOpen else {
            postAccessibilityAnnouncement(
                "Open a vault first. Search works inside a vault."
            )
            return
        }
        toggleSearchOverlay()
    }

    /// Menu-owned Find routing (Edit ▸ Find ▸ Find…, ⌘F). Editor-region
    /// Base/Canvas surfaces get their local filter fields; everything
    /// else routes to `showFindInNote()` — which reveals the note
    /// editor's find bar in editing mode (focusing the editor first, so
    /// it works from the tree / right pane too) and falls back to vault
    /// search in reading mode / with no note open (red-team: ⌘F must
    /// never be inert).
    ///
    /// #874 (Cory-confirmed 2026-07-12): ⌘F is now **find-in-note**, not
    /// vault search (which moved to ⇧⌘F). searching.md:29 — "macOS:
    /// support Find-in-window/page for locating content in open
    /// documents." This reverses the #422 vault-first ⌘F; the Base/Canvas
    /// filter routing is unchanged (their ⌘F already focused a local
    /// filter), only the fall-through — once vault search — now shows the
    /// editor find bar.
    func requestFindInFocusedSurface() {
        if activeBaseDocument != nil, workspace.focusRegion == .editor {
            basesFocusQuickFilter()
        } else if activeCanvasDocument != nil, workspace.focusRegion == .editor {
            canvasFocusFilter()
        } else {
            showFindInNote()
        }
    }

    /// #874: reveal the note editor's find bar (the HIG Find-in-window
    /// path, searching.md:29). ⌘F must NEVER be a dead keystroke
    /// (red-team):
    ///
    /// - **Editing mode, note open** — send `findInNoteRequest`;
    ///   `NoteEditorView` makes its `NSTextView` first responder and
    ///   opens the find bar. Going through the request (not a bare
    ///   `performTextFinderAction:` up the CURRENT chain) is what makes
    ///   ⌘F work when the tree / right pane holds focus — the common
    ///   case right after opening a note from the sidebar.
    /// - **Reading mode, non-note tab (Graph / Canvas / Base / dashboard),
    ///   or no tab open** — there is no mounted `NoteEditorView` subscribed
    ///   to `findInNoteRequest` (reading mode's `ReadingView` is pure
    ///   SwiftUI; the other surfaces mount their own container view). Fall
    ///   back through `requestSearchOverlay()` (the SAME vault-guarded
    ///   entry the ⇧⌘F menu item uses) so ⌘F still does something,
    ///   preserving the #422 never-inert guarantee: with a vault open it
    ///   reveals the search overlay; with none open it announces "Open a
    ///   vault first" rather than mounting a hostless overlay on the
    ///   welcome screen. (A reading-mode in-page find is a follow-up —
    ///   filed separately.)
    ///
    /// The mounted-editor test is `isNoteEditorMounted` — true ONLY when
    /// `NoteContentView.editorSurface` (which carries the
    /// find-bar-subscribing `NoteEditorView`) is actually on screen.
    /// Red-team: gating on `selectedFilePath` was wrong —
    /// `activateGraphTab`/`activateBaseTab`/`activateCanvasTab` leave it
    /// non-nil (a stale note path, or the base/canvas file's own path), so
    /// ⌘F would publish into zero subscribers and become a dead keystroke
    /// on those tabs. Codex round 1: gating on `activeTabPath` +
    /// `.editing` alone was ALSO insufficient — a `.markdown` tab whose
    /// note is still loading, or PERMANENTLY failed to load (deleted /
    /// unreadable / invalid-UTF-8 / oversized → `noteLoadError`), shows
    /// the loading/error state, NOT the editor, so `findInNoteRequest`
    /// again has zero subscribers. `isNoteEditorMounted` mirrors
    /// `NoteContentView.body`'s exact editor-mount conditions.
    func showFindInNote() {
        guard isNoteEditorMounted else {
            requestSearchOverlay()
            return
        }
        findInNoteRequest.send()
    }

    /// True exactly when `NoteContentView.editorSurface` — the only
    /// `findInNoteRequest` subscriber — is mounted and on screen, so a
    /// find request will actually reach an `NSTextView`. Mirrors
    /// `NoteContentView.body`: an active `.markdown` tab
    /// (`workspace.activeTabPath != nil`, so `NoteContentView` is the
    /// mounted container rather than Graph/Canvas/Base), in `.editing`
    /// mode (reading mode's `ReadingView` has no find bar), with the note
    /// LOADED — no `noteLoadError`, not `isLoadingNote`, and
    /// `currentNoteText` populated (the `contentState` arm). Any other
    /// combination routes ⌘F to the vault-search fallback so it is never
    /// inert (#422).
    var isNoteEditorMounted: Bool {
        workspace.activeTabPath != nil
            && activeViewMode == .editing
            && noteLoadError == nil
            && !isLoadingNote
            && currentNoteText != nil
    }

    func toggleSearchOverlay() {
        if isSearchOpen {
            closeSearchOverlay()
        } else {
            isSearchOpen = true
        }
    }

    /// Toggle the right pane's visibility (#882 — View ▸ Hide/Show Right Pane
    /// / ⌥⌘I / the palette). Announces the new state so the change is never
    /// silent for a VoiceOver user (the toggle-feedback policy shared with
    /// `toggleEditorSpellCheck` / `setEditorTextScale`).
    func toggleRightPane() {
        isRightPaneVisible.toggle()
        // #882 note (Codex red-team): hiding the pane while keyboard focus
        // is INSIDE it drops focus to the window (recover with Tab, or
        // ⌥⌘I to bring the pane back). A programmatic move of the real
        // first responder to the editor is intentionally NOT attempted
        // here: this app has no editor-focus request seam — even the
        // spec'd ⌘⌥← "return to editor" only updates region bookkeeping,
        // never `makeFirstResponder`, and the editor surface is an
        // NSTextView that owns first responder natively. A model-only
        // "rescue" would be a no-op that misrepresents where focus is;
        // adding a real editor-focus anchor risks fighting the NSTextView
        // and needs on-device VoiceOver verification — deferred to the
        // region-focus-containment work, tracked separately.
        postAccessibilityAnnouncement(
            isRightPaneVisible ? "Right pane shown." : "Right pane hidden.",
            priority: .medium)
    }

    /// Reveal the right pane and focus the leaf region — the single entry
    /// point every leaf-reveal command routes through (#882 red-team: a
    /// reveal command that only set `activeLeaf` was a dead no-op while the
    /// pane was hidden). Sets visibility BEFORE focusing so the leaf the
    /// caller just selected is actually on screen.
    func focusLeafRegionRevealingPane() {
        isRightPaneVisible = true
        workspace.focusLeafRegion()
    }

    /// Close the overlay and cancel any in-flight search. Keep
    /// `searchQuery` so a Cmd+F → Esc → Cmd+F round trip lands
    /// back where the user was — but RESET `searchScope` to `.vault`:
    /// a tag scope set by reading-view activation is a transient,
    /// invisible filter, and leaving it armed would silently scope the
    /// next ⌘F search to a tag the user can't see. `closeVault()`
    /// routes through here too, so this covers vault close as well.
    func closeSearchOverlay() {
        isSearchOpen = false
        cancelInFlightSearch()
        searchScope = .vault
        searchState = .idle
        searchSummary = ""
        // No rows are displayed once the panel is idle, so the
        // producing-query snapshot must go too (#876 Codex round 2) —
        // otherwise a stale `lastResultsQuery` could survive into the
        // next vault. This is the single teardown choke point (Esc,
        // closeVault, and the openVault direct-switch reset all route
        // here).
        lastResultsQuery = nil
    }

    /// Enter tag scope and re-arm the search. Called by the reading
    /// view's tag activation (#508): sets `.tag(name:)` and pushes the
    /// (typically empty) query through the debouncer, which under tag
    /// scope lists the tag's files rather than idling.
    func setSearchScope(_ scope: SearchScope) {
        searchScope = scope
        bumpSearchQuery()
    }

    /// Drop back to vault scope (the dismissible chip's clear button)
    /// and re-arm with the current query so the results refresh
    /// immediately under the wider scope.
    func clearSearchScope() {
        searchScope = .vault
        bumpSearchQuery()
    }

    /// Cancel any currently-running search task. Safe to call when
    /// nothing's in flight.
    private func cancelInFlightSearch() {
        searchCancelToken?.cancel()
        searchCancelToken = nil
        searchTask?.cancel()
        searchTask = nil
    }

    /// Kick off a fresh search. Called from the debouncer; callers
    /// shouldn't invoke directly.
    private func runSearch(query: String) {
        let trimmed = query.trimmingCharacters(in: .whitespaces)
        // Empty query → idle, EXCEPT under `.tag` scope: there an empty
        // query is meaningful (list every file with the tag), so it must
        // reach the FFI call instead of short-circuiting. `.vault` /
        // `.folder` keep their empty → idle behavior. Match on the scope
        // cases so a future scope can opt in explicitly.
        let scopeListsOnEmpty: Bool = {
            if case .tag = searchScope { return true }
            return false
        }()
        if trimmed.isEmpty && !scopeListsOnEmpty {
            cancelInFlightSearch()
            searchState = .idle
            searchSummary = ""
            lastResultsQuery = nil
            return
        }
        guard let session = currentSession else {
            // No vault open → surface a benign placeholder rather
            // than an error toast.
            searchState = .idle
            return
        }
        // Tear down the previous query before starting a new one.
        cancelInFlightSearch()
        searchState = .searching
        let cancel = CancelToken()
        searchCancelToken = cancel
        // Snapshot the scope for the detached task — reading it off
        // `self` across the actor hop would race a concurrent scope
        // change (e.g. the user clearing the chip mid-flight).
        let scope = searchScope
        // Explicit `@MainActor` on the Task body so the
        // post-await @Published writes are guaranteed to run on
        // the main thread (Codoki PR 86 callout). Without this the
        // awaiting Task could resume on whatever cooperative-pool
        // thread the inner detached task ran on, and SwiftUI
        // throws a runtime warning when @Published changes off-main.
        searchTask = Task { @MainActor [weak self] in
            let outcome: Result<QueryResultSet, VaultError> =
                await Task.detached(priority: .userInitiated) {
                    do {
                        let rs = try session.fullTextSearch(
                            query: trimmed,
                            scope: scope,
                            cancel: cancel
                        )
                        return .success(rs)
                    } catch let error as VaultError {
                        return .failure(error)
                    } catch {
                        return .failure(.Io(message: error.localizedDescription))
                    }
                }
                .value

            // Drop late results: another query may have taken over
            // while this one was in flight; tossing the result keeps
            // the overlay's panel coherent with the latest typed
            // query. The `currentSession !== session` arm is the
            // cross-vault guard (#876 Codex round 2): a direct vault
            // switch that resolves after this search was dispatched must
            // NOT publish vault A's rows into vault B's overlay (the same
            // identity guard the note-load and history paths use). Belt
            // and suspenders — the openVault teardown also cancels this
            // search — but robust if a switch races the await.
            guard let self else { return }
            if Task.isCancelled || self.searchCancelToken !== cancel
                || self.currentSession !== session
            {
                return
            }
            switch outcome {
            case .success(let rs):
                self.searchState = .results(rows: rs.rows, summary: rs.summary)
                self.searchSummary = rs.summary
                // #876 Codex round 1: remember WHICH query produced these
                // rows, so activating one records/anchors that query — not
                // a newer one the user may have typed during the debounce.
                self.lastResultsQuery = trimmed
            case .failure(let error):
                if case .Cancelled = error {
                    // Cancellation is a normal user action — keep
                    // whatever the panel was showing before.
                    return
                }
                let message = self.humanReadable(error)
                self.searchState = .error(message)
                self.searchSummary = "Search error: \(message)"
            }
        }
    }

    /// Internal hook from the selectedFilePath subscription. Cancels
    /// any in-flight note load and kicks off a fresh one — or clears
    /// content if `path` is nil.
    private func handleSelectionChange(to path: String?) {
        // Same file re-selected → no-op. This guard matters
        // because the dirty-state rollback below writes
        // `selectedFilePath = loadedFilePath` to re-highlight the
        // unsaved file in the sidebar, which re-triggers this
        // subscription with `path == loadedFilePath`. Without the
        // guard, the rollback would clear and reload the file the
        // user is still editing, blowing away the dirty buffer
        // we're trying to preserve.
        if let loaded = loadedFilePath, path == loaded {
            return
        }
        // U1-2 (#454): re-entrancy guard — `activateTab` drives the funnel
        // itself and updates `selectedFilePath` as a mirror; the sink must
        // not run the funnel a second time on that assignment.
        if isActivatingTab { return }
        // Milestone T/N: non-markdown selections reaching the sink (e.g.
        // a direct selectedFilePath write) reroute to their document arms —
        // never the note loader.
        if let path, path.lowercased().hasSuffix(".canvas") {
            openCanvasFile(path, target: .currentTab)
            return
        }
        if let path, path.lowercased().hasSuffix(".base") {
            openBaseFile(path, target: .currentTab)
            return
        }
        // U1-2: a selection naming a path that is already open as ANOTHER
        // tab in the active group is a tab switch, not a navigation —
        // route through the tab funnel (snapshot/restore, no dirty gate:
        // each tab keeps its own dirty buffer; that is the point of tabs).
        if let path,
            let incomingTab = workspace.activeGroupTab(forPath: path),
            incomingTab.id != workspace.model.activeGroup.activeTabID {
            activateTab(incomingTab.id)
            return
        }
        // Dirty-state gate (issue #63): switching files while the
        // editor has unsaved changes must not silently drop the
        // user's edits. Park the requested destination in
        // `pendingNavigation` and let the "Save changes?" alert
        // route the actual transition.
        //
        // U1-5 refinement: when the dirty buffer's file is ALSO open in
        // another tab, replacing THIS tab's item cannot lose the edits —
        // the sibling tab holds the same buffer (parked snapshots are
        // mirrored on every keystroke). No prompt in that case.
        let dirtyBufferSurvivesElsewhere =
            loadedFilePath.map { loaded in
                workspace.model.allTabs.filter { $0.item == .markdown(path: loaded) }.count > 1
            } ?? false
        if hasUnsavedChanges, path != loadedFilePath, !dirtyBufferSurvivesElsewhere {
            pendingNavigation = .selectFile(path)
            // Roll the selection back so the file list re-highlights
            // the dirty file while the alert is up. The async hop is
            // required because we're inside the `$selectedFilePath`
            // willSet/sink chain: a synchronous write here would be
            // overwritten by the outer assignment once the willSet
            // returns. Dispatching to the next main-loop tick lets
            // the outer write finish, then our rollback takes
            // effect; the same-file guard at the top of this method
            // short-circuits the re-entry triggered by the rollback.
            if let loaded = loadedFilePath {
                DispatchQueue.main.async { [weak self] in
                    guard let self else { return }
                    if self.selectedFilePath != loaded {
                        self.selectedFilePath = loaded
                    }
                }
            }
            return
        }
        // U1-2: park the outgoing tab's buffer BEFORE the teardown below
        // clears the fields. No-ops when nothing is loaded (guards inside).
        workspace.snapshotActiveTab(
            text: currentNoteText, baseline: savedBaselineText,
            contentHash: currentNoteContentHash,
            hasUnsavedChanges: hasUnsavedChanges,
            saveError: saveError, saveConflict: currentSaveConflict,
            loadedFilePath: loadedFilePath,
            fmSource: currentNoteFMSource,
            bodyByteOffset: bodyByteOffset, bodyLineOffset: bodyLineOffset)
        // Same save-then-close scope rule as activateTab (Codoki #492):
        // replacing the active tab's item in place also ends the scope —
        // the pending close would otherwise target a different document.
        pendingTabCloseAfterSave = nil
        cancelNoteScopedWork()
        clearActiveNoteFields()
        // Workspace model update (U1-4 mirror; tab switches took the
        // `activateTab` early-return above). Below the dirty gate on
        // purpose: a parked navigation rolled back by the alert must never
        // surface in the workspace. Replaces the active tab's item, or
        // closes it on deselect.
        let replacedItem = workspace.activeTab?.item
        workspace.mirrorSingleSelection(path)
        releaseCanvasDocumentIfUnreferenced(replacedItem)
        releaseBaseDocumentIfUnreferenced(replacedItem)
        releaseDashboardDocumentIfUnreferenced(replacedItem)
        guard let path else {
            // Full clear when nothing is selected. Safe here because
            // there's no destination note to attribute stale content
            // to — and `closeVault` / explicit deselect callers expect
            // the panels to drop their contents synchronously.
            currentBacklinks = []
            currentOutgoingLinks = []
            currentOutgoingLinksPath = nil
            currentNoteProperties = []
            currentNoteEmbedResolutions = [:]
            pendingEmbedPreview = nil
            currentNoteTasks = []
            currentNoteMathBlocks = []
            currentNoteCodeBlocks = []
            currentNoteDiagramBlocks = []
            currentNoteCitations = []
        currentNoteCitationRefs = []
            expandedCitation = nil
            isLoadingNote = false
            isLoadingLinks = false
            isLoadingEmbeds = false
            isLoadingTasks = false
            isLoadingMathBlocks = false
            isLoadingCodeBlocks = false
            isLoadingDiagramBlocks = false
            return
        }
        // Note-to-note transitions: leave `currentBacklinks`,
        // `currentOutgoingLinks`, and `currentNoteProperties` holding
        // the previous selection's values until the new load resolves.
        // The previous shape cleared them synchronously, which made
        // the PropertiesPanel render `EmptyView` for the duration of
        // the load — a visible flicker for sighted users and a
        // disappear/reappear of the "Properties, N items" rotor item
        // for VoiceOver on every selection change (#90). The
        // race-cancel guard in `loadCurrentLinks` (selectedFilePath ==
        // path) ensures the newer task's writes win, so the user only
        // sees stale content for the duration of the IO — typically
        // a few milliseconds.
        //
        // Embeds are the exception (audit #203). Each cached
        // resolution carries the embedded note's full body text;
        // holding the prior file's resolutions during a transition
        // would briefly render the prior file's *content* inside
        // panels labelled as belonging to the new file. The window
        // is sub-second but uniquely confusing for embeds because
        // the stale data is whole notes, not one-liners. Clear
        // synchronously so the panel falls back to "not yet
        // resolved" placeholders until the new resolutions land.
        clearTransitionSensitiveCollections()
        restoreParkedOrLoadFromDisk(path: path)
        fireCollectionLoads(path: path)
        scheduleBasesDockFollowActiveRefresh()
    }

    /// Cancel every note-scoped in-flight task (loads + after-save
    /// refreshes). Shared by the selection funnel and `activateTab`.
    /// Comment history: #421 cursor park; audit #257 M1 orphaned pipelines;
    /// red-team M1+M2 refresh-task cancellation.
    func cancelNoteScopedWork() {
        noteLoadTask?.cancel()
        noteLoadTask = nil
        // #421: a pending {{cursor}} park belongs to the note that
        // requested it — never let it ride into a different note.
        clearPendingCursorByteOffset()
        linksLoadTask?.cancel()
        linksLoadTask = nil
        // Codex round 6: the embeds + citations legs were missing from
        // this list. Active-note DELETION retains `selectedFilePath`
        // for the missing-file tab, so an uncancelled in-flight leg
        // still passes its `selectedFilePath == path` guard and could
        // publish resolutions/citations for the deleted note beside
        // the error tab. (The embeds entry guard's isCancelled check
        // is what this cancellation arms.)
        embedsLoadTask?.cancel()
        embedsLoadTask = nil
        citationsLoadTask?.cancel()
        citationsLoadTask = nil
        tasksLoadTask?.cancel()
        tasksLoadTask = nil
        // Audit #257 M1: orphaned content-pipeline tasks would otherwise
        // keep grabbing the session mutex after the user switched files.
        mathBlocksLoadTask?.cancel()
        mathBlocksLoadTask = nil
        codeBlocksLoadTask?.cancel()
        codeBlocksLoadTask = nil
        diagramBlocksLoadTask?.cancel()
        diagramBlocksLoadTask = nil
        // Red-team M1+M2: cancel pending refresh-after-save tasks too.
        mathBlocksRefreshTask?.cancel()
        mathBlocksRefreshTask = nil
        codeBlocksRefreshTask?.cancel()
        codeBlocksRefreshTask = nil
        diagramBlocksRefreshTask?.cancel()
        diagramBlocksRefreshTask = nil
    }

    /// Clear the active-note fields for a transition. Shared by the
    /// selection funnel and `activateTab`.
    func clearActiveNoteFields() {
        currentNoteText = nil
        savedBaselineText = nil
        currentNoteContentHash = nil
        currentNoteFMSource = ""
        bodyByteOffset = 0
        bodyLineOffset = 0
        propertiesSourceError = nil
        // #868: the menu-title mirror resets with the note — the widget
        // self-hides on `loadedFilePath == nil` WITHOUT firing its
        // `.onChange(of: isSourceMode)` (a removed view's onChange never
        // fires), so the transition funnel owns this edge.
        propertiesSourceShowing = false
        loadedFilePath = nil
        hasUnsavedChanges = false
        currentSaveConflict = nil
        saveError = nil
        currentNoteHeadings = []
        noteLoadError = nil
        linksLoadError = nil
        tasksLoadError = nil
    }

    /// Audit #203: resolved note embeds + content pipelines clear synchronously
    /// on transition (their cached payloads belong to the previous file).
    /// Rendered Base embeds use weak visibility leases because sibling panes
    /// can remain live; links/properties intentionally hold stale values until
    /// the new load lands (#90 anti-flicker discipline).
    func clearTransitionSensitiveCollections() {
        currentNoteEmbedResolutions = [:]
        releaseUnleasedBaseEmbedDocuments()
        // Drop any open embed-preview popover too — its target may
        // not exist in the new file's embed set.
        pendingEmbedPreview = nil
        currentNoteMathBlocks = []
        currentNoteCodeBlocks = []
        currentNoteDiagramBlocks = []
        currentNoteCitations = []
        currentNoteCitationRefs = []
        expandedCitation = nil
    }

    /// U1-2 restore path: a previously-activated tab restores its parked
    /// buffer instead of re-reading disk; headings refresh through the
    /// after-save path (index read, no file read). A parked stale
    /// `contentHash` is safe by construction — the next save's conflict
    /// detection compares it against disk, exactly as for a long-open note.
    private func restoreParkedOrLoadFromDisk(path: String) {
        if let activeTabID = workspace.model.activeGroup.activeTabID,
            let parked = workspace.document(for: activeTabID),
            parked.hasLoaded, parked.path == path {
            currentNoteText = parked.text
            savedBaselineText = parked.savedBaselineText
            currentNoteContentHash = parked.contentHash
            currentNoteFMSource = parked.fmSource
            bodyByteOffset = parked.bodyByteOffset
            bodyLineOffset = parked.bodyLineOffset
            loadedFilePath = path
            hasUnsavedChanges = parked.hasUnsavedChanges
            saveError = parked.saveError
            currentSaveConflict = parked.saveConflict
            isLoadingNote = false
            noteLoadError = nil
            if let session = currentSession {
                refreshHeadingsAfterSave(session: session, path: path)
            }
        } else {
            noteLoadTask = Task { [weak self] in
                await self?.loadCurrentNote(path: path)
            }
        }
    }

    /// The per-note collection fan-out (links → embeds chain, tasks, math,
    /// code, diagrams, citations). Shared by the selection funnel and
    /// `activateTab`; every loader carries its own race guard.
    private func fireCollectionLoads(path: String) {
        // Codex round 5: cancel the previous fan-out before replacing
        // it. The per-loader race guards drop stale RESULTS, but a
        // dropped chain still had side effects — the old links leg
        // chained a fresh embeds task even when its own result was
        // rejected, and that late orphan could flip isLoadingEmbeds
        // AFTER the newer chain had already finished, stranding the
        // flag with no newer task left to clear it. Cancellation (plus
        // the chain gate and the embeds entry guard below) closes the
        // set-after-newer-finished window. `scheduleHistoryLoad`
        // serializes itself and is left alone.
        linksLoadTask?.cancel()
        embedsLoadTask?.cancel()
        tasksLoadTask?.cancel()
        mathBlocksLoadTask?.cancel()
        codeBlocksLoadTask?.cancel()
        diagramBlocksLoadTask?.cancel()
        citationsLoadTask?.cancel()
        linksLoadTask = Task { [weak self] in
            await self?.loadCurrentLinks(path: path)
            // Codex round 5: a cancelled links leg must not chain an
            // embeds leg — its result was dropped, and the chain it
            // would spawn is exactly the orphan described above.
            guard !Task.isCancelled else { return }
            // Chain the embed-resolution load on after links — we
            // need the outgoing-links query result (specifically
            // `is_embed = true` rows) before we know what to resolve.
            // Assign to `embedsLoadTask` so tests + future
            // cancellation paths have a handle on this leg
            // independently of the parent links load (audit #204
            // caught the missing assignment).
            let embedsTask: Task<Void, Never> = Task { [weak self] in
                await self?.loadCurrentNoteEmbedResolutions(path: path)
            }
            await MainActor.run { [weak self] in
                self?.embedsLoadTask = embedsTask
            }
            await embedsTask.value
        }
        tasksLoadTask = Task { [weak self] in
            await self?.loadCurrentNoteTasks(path: path)
        }
        // Content pipelines (#223): math + code + diagram fan out
        // in parallel after the selection lands. They don't depend
        // on links or each other; running concurrently keeps the
        // post-selection latency down. Each loader carries its own
        // race-guard (selectedFilePath == path) so a fast switch
        // can't write stale state.
        mathBlocksLoadTask = Task { [weak self] in
            await self?.loadCurrentNoteMathBlocks(path: path)
        }
        codeBlocksLoadTask = Task { [weak self] in
            await self?.loadCurrentNoteCodeBlocks(path: path)
        }
        diagramBlocksLoadTask = Task { [weak self] in
            await self?.loadCurrentNoteDiagramBlocks(path: path)
        }
        // History leaf (O-5, #543): version list + the pref-gated
        // since-open funnel (compute-then-mark), strictly serialized
        // behind any in-flight load (round 2 — see
        // scheduleHistoryLoad).
        scheduleHistoryLoad(path: path)
        // Milestone L (#279): citations for the current note. Same
        // fan-out shape — race-guarded by selectedFilePath == path
        // inside the loader.
        citationsLoadTask = Task { [weak self] in
            await self?.loadCurrentNoteCitations(path: path)
        }
    }

    /// Ask the content pane to scroll to the given heading anchor.
    /// Sent by `OutlineSidebar` rows; `NoteContentView` subscribes via
    /// `onReceive(scrollAnchorRequest)`.
    func requestScrollToHeading(anchor: String) {
        scrollAnchorRequest.send(anchor)
    }

    /// Activate a search result row from `SearchOverlay`.
    ///
    /// 1. Set `selectedFilePath` (which kicks off the regular note-
    ///    load observer).
    /// 2. Close the overlay so the content area gets focus.
    /// 3. Await the in-flight note-load so the per-line anchors are
    ///    in the rendered tree before we ask `ScrollViewReader` to
    ///    target one.
    /// 4. Send the line-scroll request.
    /// 5. Post a polite announcement with filename + line +
    ///    snippet — matches the acceptance criteria's
    ///    `"Opened <filename>, line N: <snippet>"`.
    ///
    /// If the path is the SAME file already open, we skip the
    /// selection assignment (the load observer wouldn't re-trigger
    /// the load anyway because of `.removeDuplicates()`) but still
    /// scroll and announce.
    func openSearchResult(_ hit: QueryHit) {
        let cleanSnippet = hit.snippet
            .replacingOccurrences(of: "\u{2}", with: "")
            .replacingOccurrences(of: "\u{3}", with: "")
        let filename = (hit.path as NSString).lastPathComponent
        // The query that PRODUCED the displayed rows, captured at result
        // time (`lastResultsQuery`) — NOT the live field. #876 Codex
        // round 1: the 150 ms debounce leaves the previous query's rows
        // visible while `searchQuery` already holds a newer string; the
        // row the user activated belongs to `lastResultsQuery`, so both
        // the line-scroll anchor AND the recorded recent must use it (the
        // `?? searchQuery` is a defensive fallback — row activation always
        // implies a `.results` panel, which sets `lastResultsQuery`).
        let queryForRow = lastResultsQuery ?? searchQuery
        let queryForLineLookup = queryForRow

        // #876: activating a result is the commit signal that a query
        // was useful — remember it before `closeSearchOverlay()` (which
        // preserves `searchQuery`, so order is not load-bearing) so the
        // idle overlay can offer it next time. Record the PRODUCING query
        // (`queryForRow`), not the live field (Codex round 1).
        recordSearchRecent(queryForRow)

        // Close the overlay first so focus moves cleanly back to
        // the content area before the file load completes.
        closeSearchOverlay()

        // U1-5: honor a live ⌘ modifier (row ⌘-click or ⌘Return through
        // the overlay's key monitor — both leave NSApp.currentEvent set).
        let target = openTargetFromCurrentEvent()
        let wasAlreadyOpen = selectedFilePath == hit.path && target == .currentTab
        if !wasAlreadyOpen {
            openFile(hit.path, target: target)
        }
        // Snapshot the in-flight load up front so the Task closure
        // doesn't have to reach back through `self?.` before its
        // strong-unwrap guard — that pre-guard `self?` access was
        // tripping Codoki's weak-self lint on PR 98 even though the
        // post-await `guard let self` correctly shadow-unwraps. The
        // Task reference outlives `self` cleanly if AppState
        // dealloc's during scheduling; we just await whatever load
        // was pending.
        let pendingLoad = noteLoadTask

        Task { @MainActor [weak self] in
            // Wait for any in-flight note load to finish so the
            // per-line anchors exist in the rendered tree before
            // we ask `ScrollViewReader.scrollTo` to target one.
            // For the same-file case the snapshot is nil and this
            // await is skipped.
            if let pendingLoad {
                await pendingLoad.value
            }
            guard let self else { return }
            // A subsequent selection change (the user moved to a
            // different file while we were waiting) cancels this
            // scroll — sending into the subject would land on the
            // wrong file's anchors.
            guard self.selectedFilePath == hit.path else { return }
            // Derive the line UI-side from the loaded body. Up
            // through PR 94 this came back on the QueryHit, but
            // computing it Rust-side meant pulling `body_text`
            // through SQLite for every hit (#92 item 1). The body
            // is loaded anyway by the time we get here, so we
            // tokenize the original query and scan for the first
            // match — same heuristic the Rust side used.
            let body = self.currentNoteText ?? ""
            let line = firstTokenLineNumber(in: body, query: queryForLineLookup)
            self.lineScrollRequest.send(line)
            // U3-3: the buffer (and thus `line`) is body-space; humans and
            // on-disk tooling count whole-file lines — announce THOSE.
            postAccessibilityAnnouncement(
                "Opened \(filename), line \(self.fileLine(fromBodyLine: line)): \(cleanSnippet)"
            )
            self.lastActivatedSearchResultLine = line
            self.lastActivatedSearchResultPath = hit.path
        }
    }

    /// Activate an outgoing-link row from the OutgoingLinksPanel.
    ///
    /// - Resolved internal: navigate to the target and announce the
    ///   filename. (The subsequent "Showing <filename>" announcement
    ///   from `NoteContentView.onAppear` rounds out the audio
    ///   feedback once the content has actually loaded.)
    /// - Unresolved internal: don't navigate; announce that we
    ///   couldn't open it so a screen-reader user doesn't think the
    ///   click was a no-op.
    /// - External: hand off to NSWorkspace; announce that the browser
    ///   was invoked.
    ///
    /// The branch chosen is also reflected in `lastActivatedLinkOutcome`
    /// so tests can verify behaviour without observing UIKit/AppKit
    /// side effects.
    func openLink(_ link: OutgoingLink) {
        if link.isExternal {
            // Allowlist the schemes we hand to LaunchServices. The
            // link parser flags `file:`, `javascript:`, and custom
            // schemes as external too, but blindly passing them to
            // NSWorkspace.open would let a typo in a markdown link
            // hand control of the user's machine to whatever app
            // happens to be registered for that scheme. http/https
            // (web pages) and mailto (compose new email) are the
            // schemes a notes app's "external link" feature is
            // expected to handle.
            guard let url = URL(string: link.targetRaw),
                let scheme = url.scheme?.lowercased(),
                ["http", "https", "mailto"].contains(scheme)
            else {
                postAccessibilityAnnouncement(
                    "Cannot open external link \(link.targetRaw). "
                        + "Only web and mail links are supported."
                )
                lastActivatedLinkOutcome = .externalOpenFailed(link.targetRaw)
                return
            }
            if externalOpener(url) {
                postAccessibilityAnnouncement(
                    "Opened external link in default browser."
                )
                lastActivatedLinkOutcome = .openedExternal(link.targetRaw)
            } else {
                postAccessibilityAnnouncement(
                    "Could not open external link \(link.targetRaw)."
                )
                lastActivatedLinkOutcome = .externalOpenFailed(link.targetRaw)
            }
            return
        }
        if link.isUnresolved {
            postAccessibilityAnnouncement(
                "\(link.targetRaw) is unresolved. Cannot open."
            )
            lastActivatedLinkOutcome = .unresolved(link.targetRaw)
            return
        }
        guard let path = link.targetPath else {
            // Defensive: a non-external, non-unresolved row should
            // always carry a target_path. Treat the impossible case
            // as unresolved so the user gets feedback instead of
            // silence.
            postAccessibilityAnnouncement(
                "\(link.targetRaw) is unresolved. Cannot open."
            )
            lastActivatedLinkOutcome = .unresolved(link.targetRaw)
            return
        }
        navigate(to: path, kind: "Opened", target: openTargetFromCurrentEvent())
    }

    /// The repository README URL the Help utility opens (gap G13). Small,
    /// honest, replaceable: Help has no dedicated in-app surface, so the
    /// utility bar's "Help" button hands this URL to the same external-open
    /// path (`externalOpener`, → `NSWorkspace` in production) that outgoing
    /// links use. `static` so a test can assert the exact URL a recording
    /// `externalOpener` received without reaching into a running view.
    static let helpURL = URL(string: "https://github.com/coryj627/slate#readme")!

    /// Open the repository README in the user's default browser (U4-3, #472;
    /// gap G13). Routed through `externalOpener` — the same injected hand-off
    /// the outgoing-links panel uses — so tests spy on it with a recording
    /// closure instead of spawning a browser, and the announcement mirrors the
    /// external-link path so a screen-reader user hears the hand-off. Surfaced
    /// both here (the `SidebarUtilityBar` "Help" button) and in the command
    /// registry as `slate.help.open` — one implementation, two entry points.
    func openHelp() {
        if externalOpener(Self.helpURL) {
            postAccessibilityAnnouncement(
                "Opened Help in your default browser."
            )
            lastActivatedLinkOutcome = .openedExternal(Self.helpURL.absoluteString)
        } else {
            postAccessibilityAnnouncement(
                "Could not open Help."
            )
            lastActivatedLinkOutcome = .externalOpenFailed(Self.helpURL.absoluteString)
        }
    }

    /// Activate a backlink row from the BacklinksPanel — navigates
    /// to the source file that linked here. Backlinks are always
    /// resolved (the query joins on resolved target_path), so this
    /// is the simple `navigate(to:)` path.
    func openBacklink(_ backlink: Backlink) {
        navigate(
            to: backlink.sourcePath, kind: "Opened backlink to",
            target: openTargetFromCurrentEvent())
    }

    /// Shared post-activation step: update `selectedFilePath` (which
    /// the file-list selection binding + the note-load observer both
    /// pick up) and post an immediate audio confirmation so the user
    /// hears that the click worked before the content load finishes.
    private func navigate(
        to path: String, kind: String, target: OpenTarget = .currentTab
    ) {
        openFile(path, target: target)
        let filename = (path as NSString).lastPathComponent
        // #424 (F-C1): .high — the selection change this just
        // triggered posts its own announcements ("Showing <file>.",
        // "Outline, N headings.") which superseded this medium one
        // before VO spoke it; the VO test heard ONLY the indirect
        // outline cue. High priority makes the activation
        // confirmation win; the outline count then follows with the
        // structure info.
        postAccessibilityAnnouncement("\(kind) \(filename).", priority: .high)
        lastActivatedLinkOutcome = .openedInternal(path)
    }

    var isVaultOpen: Bool { currentSession != nil }

    func openVault(at url: URL) {
        do {
            let session = try VaultSession.openFilesystem(rootPath: url.path)
            // Audit #259: push the persisted math prefs into the
            // fresh session so a user who set ClearSpeak → MathSpeak
            // in a prior run gets MathSpeak from the very first
            // `get_math_blocks` call. The Rust-side session opens
            // with defaults; without this the prefs would only take
            // effect after the first Picker interaction.
            do {
                try session.setMathPrefs(prefs: mathPrefs)
            } catch {
                fputs(
                    "Slate: initial session.setMathPrefs failed: \(error)\n",
                    stderr
                )
            }
            // History leaf (O-5, #543): register the compaction-error
            // channel before anything can compact. The adapter hops to
            // the main actor; the once-per-path gate lives there.
            registerVaultEventListener(on: session)
            // Drop document handles while `currentSession` still names the
            // previous vault. Calling close on the freshly-opened session
            // would pair old native handles with the wrong registry.
            releaseAllCanvasDocuments()
            releaseAllBaseDocuments()
            releaseAllBaseEmbedDocuments()
            releaseAllDashboardDocuments()
            // A directly reopened vault replaces the operation lifecycle too.
            // Cancel/reset before installing the new session so an old task
            // resuming later cannot own the new vault's saving/editing flags.
            saveTask?.cancel()
            saveTask = nil
            isSaving = false
            propertyEditTask?.cancel()
            propertyEditTask = nil
            isEditingProperty = false
            // #876 Codex round 2: Open Vault / Open Recent reach here
            // WITHOUT `closeVault`, so vault A's live search must be torn
            // down too — else the still-open overlay shows A's results
            // while `currentVaultURL`/`searchRecentsStore` now name B, and
            // activating a stale A row would persist A's query into B's
            // per-vault recents and try to open A's path inside B. Mirror
            // closeVault's teardown (closeSearchOverlay cancels the
            // in-flight search and resets searchState + lastResultsQuery;
            // clear the retained query), and do it while `currentSession`
            // still names A so the cancellation targets A's search. The
            // per-vault `searchRecents` load below then reflects B.
            closeSearchOverlay()
            searchQuery = ""
            currentSession = session
            currentVaultURL = url
            lastError = nil
            // Load this vault's file-recents (#495) now that the store's
            // vault path resolves. Per-vault, so it's repopulated on
            // every vault switch; a missing / malformed file loads empty.
            fileRecents = fileRecentsStore?.load() ?? []
            // #876: likewise this vault's recent search queries — per-
            // vault, so the overlay's idle "Recent Searches" reflects the
            // vault the user is actually in.
            searchRecents = searchRecentsStore?.load() ?? []
            // Reset file-list state so the previous vault's contents
            // don't briefly flash in the new vault's sidebar.
            files = []
            scanError = nil
            // M-3 (#534): likewise the previous vault's sync report —
            // nil puts the diagnostics panel back into its loading
            // state until this vault's post-scan load publishes.
            // #638: and the previous vault's marker watcher, so a late
            // debounced event can't fire a refresh under the new vault
            // (the load-side session guard would catch it anyway —
            // this just stops the noise at the source).
            stopSyncMarkerWatcher()
            syncReport = nil
            liveSyncConfig = nil
            syncDiagnosticsError = nil
            // Connections leaf (P1-1 #554): clear any prior vault's root
            // / back-stack / payload before the new vault populates
            // (review round 1 finding 3).
            resetConnectionsState()
            // Graph tab table (P1-2 #555): same cross-vault isolation.
            resetGraphTableState()
            // Graph tab diagram (P2-3 #559): tear down the layout session too.
            resetGraphDiagramState()
            // Graph config (P2-4 #560): EAGERLY load the new vault's
            // graph.json now — `currentVaultURL` already names it (set
            // above). Loading here (not lazily on first Graph-tab open)
            // means `graphConfig`/`connectionsDepth` always match the
            // current vault, so a Connections-depth edit BEFORE the Graph
            // tab is ever opened can't persist vault A's stale aggregate
            // into vault B (review finding 1). A pending debounced save
            // still targets the OLD vault via its captured root and is NOT
            // cancelled here, so no edit is lost (review finding 3).
            loadGraphConfig()
            // #871: the structural (file-op) undo/redo stacks are per-vault —
            // a direct Open Vault / Open Recent reaches here WITHOUT
            // `closeVault`, so an inverse move/rename staged against vault A
            // must not survive into vault B (it would move/rename the wrong
            // file). Same reasoning as the fileRecents/searchRecents/graph
            // resets in this block.
            clearStructuralUndoStacks()
            // #871 Codex round 2: INVALIDATE + release the structural-mutation
            // guard. A move/rename/import in flight when the vault switches
            // would otherwise leave `isMutatingStructure` stuck true (its
            // completion returns early on the `currentSession === session`
            // mismatch, before the release) — wedging every later structural op
            // in the new vault. Clearing the flag un-wedges; BUMPING the
            // ownership token (via `cancelStructuralMutationOwnership`) makes a
            // stale completion that already passed its session guard and then
            // suspended in `loadFiles` a no-op, so it can't clear a NEWER
            // vault's op flag on resume.
            cancelStructuralMutationOwnership()
            // #879 Codex red-team: the vault-wide Tasks Review surface must
            // die with the previous vault too — direct Open Vault routes
            // here without `closeVault`, so a stale review would survive and
            // its reload guards would reject re-querying the new vault.
            resetVaultTasksReviewState()
            // U1-2: tabs belong to a vault; a fresh open starts clean (and
            // must reset BEFORE the selection clear so the funnel doesn't
            // park the previous vault's buffer).
            workspace.reset()
            pendingTabClose = nil
            pendingTabCloseAfterSave = nil
            selectedFilePath = nil
            // Codex review (corpus PR): the tree-selection mirror must
            // die with the vault — Reveal in Finder / Copy Path read it,
            // and a stale vault-A node would resolve against vault B's
            // root (cross-vault path leak).
            treeSelectedNode = nil
            // #873: vault A's expansion ids must not leak into vault B —
            // reset before restoreWorkspaceLayout refills from B's snapshot.
            treeExpandedDirPaths = []
            // #860: a staged folder-delete confirmation belongs to the old
            // vault's tree; drop it so the alert can't fire cross-vault.
            pendingFolderDelete = nil
            // #852: the batch move sheet / batch delete confirmation are tied
            // to the old vault's selection — drop them for the same reason.
            pendingBatchMove = nil
            pendingBatchDelete = nil
            scanProgress = nil
            scanAnnouncementCount = 0
            scanAnnouncementLastMessage = nil
            scanAnnouncementLastFiredAt = .distantPast
            bibliographyLoadCount = 0
            recordOpened(url: url)
            // U1-6 (#458): restore the persisted workspace layout. Tabs
            // load lazily; a missing file surfaces the existing per-tab
            // load-error state rather than being dropped.
            restoreWorkspaceLayout()
            refreshBaseQueries()
            scanTask?.cancel()
            scanTask = Task { [weak self] in
                await self?.loadFiles()
                guard !Task.isCancelled else { return }
                self?.refreshBaseQueries()
                // M-3 (#534): sync diagnostics, once per vault open —
                // the post-scan continuation. The probes don't touch
                // the index, so this runs even when the scan itself
                // errored; a vault switch cancels this task and the
                // new vault's funnel runs its own load.
                guard !Task.isCancelled else { return }
                // #638: arm the live marker watch BEFORE the initial
                // probe and AWAIT readiness — `start()` returns only
                // once the fds are open. Arm-then-probe means a marker
                // that appears after arming emits an event (debounced
                // refresh) while anything already present is caught by
                // the probe itself, with no setup-race window
                // (adversarial re-review). A vault switch cancels this
                // task; the awaited arm suspends without blocking main.
                await self?.startSyncMarkerWatcher()
                guard !Task.isCancelled else { return }
                await self?.loadSyncDiagnostics()
            }
            // Milestone L #281: read `.slate/prefs.json` so the
            // Settings panel + style picker reflect the persisted
            // configuration. If the file has a configured
            // bibliography, push it through `setBibliographySources`
            // so the session's `bibliography_entries` matches what
            // the user last saved.
            loadBibliographyPrefsFromDisk()
            let persistedPrefs = bibliographyPrefs
            if !persistedPrefs.sources.isEmpty {
                Task { [weak self] in
                    await self?.applyBibliographyPrefs(persistedPrefs)
                }
            } else {
                // #411: no app-written sources. The session's
                // EFFECTIVE config may carry vault-shipped sources
                // from the root slate.json — the Rust side already
                // applied the precedence rules (an explicit-empty
                // prefs.json bibliography masks the vault config, a
                // silent one falls through to it). Adopt without
                // persisting: prefs.json stays untouched until the
                // user edits in Settings, so the vault file remains
                // the live source of truth.
                Task { [weak self] in
                    await self?.adoptSessionCitationsConfig()
                }
            }
        } catch let error as VaultError {
            currentSession = nil
            currentVaultURL = nil
            lastError = humanReadable(error)
        } catch {
            currentSession = nil
            currentVaultURL = nil
            lastError = error.localizedDescription
        }
    }

    /// The recent vault the user asked to switch to while the current vault
    /// was still open (U4-3, #472). Held from `switchToRecent(_:)` until the
    /// close committed (`closeVault()`'s tail opens it) or the dirty-gate
    /// prompt was cancelled (the cancel resolvers clear it → no switch).
    /// `private(set)` so tests can assert the gate parked / cleared it.
    private(set) var pendingVaultSwitchTarget: RecentVault?

    /// Switch to a recent vault (U4-3 vault switcher): close the current vault
    /// **through the same dirty gate** `closeVaultFromUserAction` uses, then
    /// open the target. Cancelling the "Save changes?" prompt cancels the whole
    /// switch — nothing closes, the target is dropped, the current vault stays.
    ///
    /// The open is deferred to `closeVault()`'s tail (which fires once the close
    /// commits from any gate branch — clean, single-dirty save/discard, or
    /// multi-tab Save All / Discard All), so we never open over an unsaved
    /// buffer. We drive the same gate primitives as `closeVaultFromUserAction`
    /// (attemptCloseVault / pendingVaultClose) but WITHOUT its "returned to the
    /// welcome screen" announcement, which would be false for a switch — the
    /// subsequent `openVault` posts its own "Vault … opened" announcement.
    func switchToRecent(_ entry: RecentVault) {
        // Already the open vault → no-op (the menu also disables the current
        // vault's row; this guards the programmatic path).
        guard entry.path != currentVaultURL?.path else { return }
        pendingVaultSwitchTarget = entry
        let parkedDirty = workspace.dirtyParkedDocuments()
        if parkedDirty.isEmpty {
            if hasUnsavedChanges {
                // Single dirty document → "Save changes?" (pendingNavigation
                // = .closeVault). On Save/Discard the resolver calls
                // closeVault(), whose tail opens the target; on Cancel the
                // resolver clears the target (no switch).
                attemptCloseVault()
            } else {
                // Clean → close now; the tail opens the target.
                closeVault()
            }
        } else {
            // Multiple dirty tabs → Save All / Discard All / Cancel prompt.
            pendingVaultClose = parkedDirty.count + (hasUnsavedChanges ? 1 : 0)
        }
    }

    /// Open a recent-vaults entry. If the path no longer exists on
    /// disk, do *not* try to open it (which would either fail with
    /// InvalidPath or, on older bugs, silently materialize a vault) —
    /// instead surface the entry through `missingRecentVault` so the
    /// UI can offer removal.
    func openRecent(_ entry: RecentVault) {
        // #872 Codex red-team: opening a recent IS the user's launch
        // intent — claim the launch-restore attempt first. This matters
        // even for a MISSING entry (which only sets `missingRecentVault`
        // and opens nothing): without the claim, a WindowGroup launch
        // `.task` firing afterward would restore the leading VALID recent
        // and override the user's "I tried this stale one" intent. The
        // launch path already claimed before routing here; this covers a
        // manual recent-list click.
        hasAttemptedLaunchRestore = true
        guard Self.vaultDirectoryExists(entry) else {
            missingRecentVault = entry
            return
        }
        openVault(at: URL(fileURLWithPath: entry.path))
    }

    /// Whether a directory exists at the entry's path — the existence
    /// rule `openRecent` enforces, factored out (#872) so the launch
    /// decision can consult the SAME rule without opening anything.
    /// Static + side-effect-free so tests drive the launch matrix
    /// deterministically by injecting a closure instead of the disk.
    static func vaultDirectoryExists(_ entry: RecentVault) -> Bool {
        var isDir: ObjCBool = false
        let exists = FileManager.default.fileExists(
            atPath: entry.path, isDirectory: &isDir)
        return exists && isDir.boolValue
    }

    /// Show the directory picker and, if the user chose a folder, open
    /// it as a vault. Centralizes the flow so the WelcomeView button
    /// and the App-level Shift-Command-O command share the same code path.
    ///
    /// `@MainActor` is redundant given the class-level annotation but
    /// is repeated here for self-documenting clarity: this method
    /// presents an `NSOpenPanel`, which AppKit requires on the main
    /// thread.
    @MainActor
    func pickAndOpenVault() {
        // #872 Codex red-team: a manual open IS the user's launch intent —
        // claim the launch-restore attempt BEFORE presenting the picker.
        // The production picker runs a nested `runModal()` event loop that
        // can execute the WindowGroup launch `.task` while the panel is
        // still up (and `isVaultOpen` is still false); without this claim,
        // that task would auto-restore a recent vault BEHIND the open
        // panel — cancelling then strands it open, choosing opens two.
        hasAttemptedLaunchRestore = true
        guard let url = vaultPicker() else { return }
        openVault(at: url)
    }

    /// Directory-picker seam. Production runs the real `NSOpenPanel`
    /// wrapper; tests substitute a canned URL (or nil = cancel) so
    /// flows that fall through to the picker — the welcome screen's
    /// ⌘O quick-open fallthrough (#863) — never present a modal panel
    /// under XCTest. Same injectable-var convention as `scanClock`.
    var vaultPicker: () -> URL? = { VaultPicker.pick() }

    // MARK: - Launch restore (#872)

    /// Where a cold launch should land. HIG launching.md: "Restore
    /// previous state on restart … avoid making people retrace steps";
    /// Obsidian / VS Code reopen the last workspace, the parity
    /// expectation for a vault app.
    enum LaunchDestination: Equatable {
        /// Auto-reopen this vault — it is present on disk.
        case restore(RecentVault)
        /// The most-recent vault is gone (folder moved / drive
        /// unplugged). Land on Welcome AND surface the EXISTING
        /// not-found flow so the user can prune the stale entry — the
        /// caller routes this through `openRecent`, which sets
        /// `missingRecentVault` and never opens a session.
        case notFound(RecentVault)
        /// Show the picker / Welcome with no auto-restore: the ⌥ escape
        /// hatch is held, the "Reopen last vault" setting is off, or
        /// there is no recent vault to restore.
        case welcome
    }

    /// Pure launch decision (#872) — no side effects, no I/O, so the
    /// whole matrix is unit-testable without an event queue or the
    /// filesystem. `optionHeld` is the ⌥-at-launch escape hatch;
    /// `restoreEnabled` is the Settings ▸ General toggle; `vaultExists`
    /// decides restore vs. the not-found flow for the most-recent entry.
    static func launchDestination(
        recents: [RecentVault],
        optionHeld: Bool,
        vaultExists: (RecentVault) -> Bool,
        restoreEnabled: Bool = true
    ) -> LaunchDestination {
        // ⌥ held, toggle off, or nothing to restore → a clean Welcome.
        guard restoreEnabled, !optionHeld, let mostRecent = recents.first else {
            return .welcome
        }
        return vaultExists(mostRecent) ? .restore(mostRecent) : .notFound(mostRecent)
    }

    /// True once `restoreMostRecentVaultOnLaunch()` has run, so the
    /// launch hook is idempotent no matter how many times the root
    /// view's `.task` re-fires across its lifetime.
    private(set) var hasAttemptedLaunchRestore = false

    /// Launch-time entry point (#872), called once from the root view's
    /// `.task`. Reopens the most-recent vault unless the ⌥ escape hatch
    /// is held or Settings ▸ General ▸ "Reopen last vault at launch" is
    /// off; a stale most-recent falls through the EXISTING not-found
    /// flow (`openRecent`). Idempotent, and a no-op once a vault is
    /// already open, so an early manual open can't be clobbered.
    ///
    /// `optionHeld` / `vaultExists` are injected — production reads the
    /// live modifier flags (evaluated at call time, so ⌥ held *during*
    /// launch is caught) and the filesystem — so the routing is
    /// unit-testable.
    func restoreMostRecentVaultOnLaunch(
        optionHeld: Bool = NSEvent.modifierFlags.contains(.option),
        vaultExists: (RecentVault) -> Bool = AppState.vaultDirectoryExists
    ) {
        guard !hasAttemptedLaunchRestore else { return }
        hasAttemptedLaunchRestore = true
        guard !isVaultOpen else { return }
        switch Self.launchDestination(
            recents: recentVaults,
            optionHeld: optionHeld,
            vaultExists: vaultExists,
            restoreEnabled: restoreVaultOnLaunch
        ) {
        case .restore(let entry), .notFound(let entry):
            // Both route through `openRecent`: it re-checks existence
            // (TOCTOU-safe) and either opens the vault or sets
            // `missingRecentVault` for the Welcome-screen not-found
            // alert — the "reuse the existing missing-vault handling"
            // requirement, no new error path.
            openRecent(entry)
        case .welcome:
            break
        }
    }

    /// True when a cold launch is *about* to auto-restore an existing
    /// vault (#872). WelcomeView reads this in `onAppear` to suppress
    /// its "Open Vault focused" announcement during the one-frame flash
    /// before the vault opens — the not-found and no-restore paths DO
    /// land on Welcome, so those keep the announcement. Mirrors the
    /// `restoreMostRecentVaultOnLaunch` decision (same inputs).
    var willRestoreVaultOnLaunch: Bool {
        guard !hasAttemptedLaunchRestore, !isVaultOpen else { return false }
        switch Self.launchDestination(
            recents: recentVaults,
            optionHeld: NSEvent.modifierFlags.contains(.option),
            vaultExists: AppState.vaultDirectoryExists,
            restoreEnabled: restoreVaultOnLaunch
        ) {
        case .restore: return true
        case .notFound, .welcome: return false
        }
    }

    /// User-action wrapper around `closeVault()` / `attemptCloseVault()`
    /// that mirrors what the MainSplitView "Close Vault" toolbar
    /// button does, including the post-close VoiceOver announcement.
    /// One source of truth for both the toolbar button (#296) and
    /// the command palette's `slate.vault.close` registration (#314).
    ///
    /// - Dirty editor → routes through `attemptCloseVault`; the
    ///   eventual close announces from `applyPendingNavigation`.
    /// - Clean editor → closes immediately and posts the
    ///   announcement inline here.
    func closeVaultFromUserAction() {
        // U1-2: the dirty check aggregates over every tab. With no parked
        // dirty documents the flow is exactly the pre-tabs behavior (the
        // active document routes through the existing single-file alert).
        let parkedDirty = workspace.dirtyParkedDocuments()
        if parkedDirty.isEmpty {
            if hasUnsavedChanges {
                attemptCloseVault()
            } else {
                closeVault()
                postAccessibilityAnnouncement(
                    "Vault closed. Returned to the welcome screen."
                )
            }
        } else {
            pendingVaultClose = parkedDirty.count + (hasUnsavedChanges ? 1 : 0)
        }
    }

    /// Multi-tab vault-close prompt (U1-2): the number of dirty tabs, nil
    /// when no prompt is up. Drives the Save All / Discard All / Cancel
    /// alert in `MainSplitView`.
    @Published var pendingVaultClose: Int?

    /// Test hook for the sequential Save All chain.
    private(set) var vaultCloseSaveAllTask: Task<Void, Never>?

    /// "Save All": save every dirty tab through the STANDARD save path
    /// (activate → saveCurrentNote → await), sequentially. Aborts on the
    /// first conflict/error, leaving the user on that tab with the standard
    /// resolution dialog up; the vault stays open. Closes on full success.
    func resolveVaultCloseSaveAll() {
        pendingVaultClose = nil
        vaultCloseSaveAllTask = Task { [weak self] in
            guard let self else { return }
            while true {
                if self.hasUnsavedChanges {
                    self.saveCurrentNote()
                    await self.saveTask?.value
                    if self.currentSaveConflict != nil || self.saveError != nil {
                        // U4-3: an aborted Save All also aborts an in-flight
                        // vault switch — the vault stays open, so a stale
                        // parked target would make a LATER plain Close Vault
                        // surprise-open the other vault (the same leak class
                        // as the U1-2 save-then-close scope).
                        self.pendingVaultSwitchTarget = nil
                        return  // standard dialog owns recovery; vault stays open
                    }
                }
                guard let next = self.workspace.dirtyParkedDocuments().first else { break }
                self.activateTab(next.id)
            }
            self.closeVault()
            postAccessibilityAnnouncement(
                "All changes saved. Vault closed. Returned to the welcome screen."
            )
        }
    }

    /// "Discard All": close without saving any tab. The per-tab buffers are
    /// dropped by `closeVault`'s workspace reset.
    func resolveVaultCloseDiscardAll() {
        pendingVaultClose = nil
        hasUnsavedChanges = false
        closeVault()
        postAccessibilityAnnouncement(
            "Changes discarded. Vault closed. Returned to the welcome screen."
        )
    }

    func resolveVaultCloseCancel() {
        pendingVaultClose = nil
        // U4-3: same as the single-dirty cancel — a cancelled multi-tab close
        // cancels the switch and drops the parked target.
        pendingVaultSwitchTarget = nil
    }

    func closeVault() {
        // U1-6: persist the layout FIRST — the teardown below clears
        // `currentVaultURL`, and the save is a no-op once it's gone.
        saveWorkspaceLayout()
        baseQueryBuilderPreviewGeneration += 1
        scanTask?.cancel()
        scanTask = nil
        noteLoadTask?.cancel()
        noteLoadTask = nil
        linksLoadTask?.cancel()
        linksLoadTask = nil
        tasksLoadTask?.cancel()
        tasksLoadTask = nil
        // #879: Tasks Review is a leaf now; its query teardown is the single
        // `closeTasksReview()` helper (cancels the in-flight page load).
        closeTasksReview()
        taskToggleTask?.cancel()
        taskToggleTask = nil
        // Audit #260: embed-resolution batch keeps grabbing the
        // session mutex post-closeVault if not cancelled. Race-guard
        // catches the stale write but the orphan task delays
        // session-deallocation.
        embedsLoadTask?.cancel()
        embedsLoadTask = nil
        isLoadingEmbeds = false
        baseQueryBuilderPreviewTask?.cancel()
        baseQueryBuilderPreviewTask = nil
        baseQueryBuilderPreviewCancelToken?.cancel()
        baseQueryBuilderPreviewCancelToken = nil
        closeSearchOverlay()
        searchQuery = ""
        // Reset transient sheet flags so a vault-close mid-palette
        // doesn't leave the bool stuck (next vault open would
        // auto-present the empty palette). #313 belt-and-suspenders
        // with the `requestCommandPalette()` open-guard.
        isCommandPaletteOpen = false
        activeBaseQueryBuilder = nil
        resetBaseQueriesForClosedVault()
        // Quick switcher (#495): same stuck-bool reset as the palette,
        // enforced by CloseVaultSheetParityTests. Also drop the in-memory
        // file-recents — it's per-vault, so the next vault reloads its own.
        isQuickSwitcherOpen = false
        fileRecents = []
        // #876: drop the in-memory recent searches — per-vault, so the
        // next vault reloads its own (mirrors `fileRecents` above).
        searchRecents = []
        // #871: the structural (file-op) undo/redo stacks are per-vault;
        // drop them so a stale inverse can't reverse against the next vault.
        clearStructuralUndoStacks()
        // #871 Codex round 2: invalidate + release the structural-mutation
        // guard on vault close too (see the openVault reset for the full
        // token rationale).
        cancelStructuralMutationOwnership()
        // #328 sheet-flag parity audit. Each `@Published var
        // is*Open` driving a `.sheet` binding must reset here for
        // the same reason as `isCommandPaletteOpen`: a vault close
        // while the sheet is presented would otherwise leave the
        // bool stuck `true`, and the next vault open would re-
        // present an empty / stale sheet against the new vault's
        // state. `isCitationSummaryOpen` is reset further down in
        // this method (search for `= false`); `isTasksReviewOpen` is
        // gone (#879 — Tasks Review is a leaf, not a sheet),
        // and `isSearchOpen` via the `closeSearchOverlay()` call
        // above. The full set is enforced by
        // `CloseVaultSheetParityTests` — a structural drift test
        // that scrapes every `@Published var is*Open` and asserts
        // each is reset here, so a newly-added sheet bool that
        // misses this method fails CI.
        isAddPropertySheetOpen = false
        // Bulk-rename: clear the sheet bool plus the in-flight
        // bookkeeping. A rename task in flight against the old
        // vault would race the close — the Rust call holds the
        // SQLite mutex and won't observe the Swift `Task.cancel`
        // signal until it next checks the `CancelToken`. We
        // cancel both: the token tells the Rust side to break
        // out of its row-by-row loop at the next check, and the
        // Task.cancel makes the Swift-side continuation observe
        // `Task.isCancelled` on resume (guarded by `performRename`
        // — #328 red-team P1).
        isBulkRenameSheetOpen = false
        renameCancelToken?.cancel()
        renameCancelToken = nil
        renameTask?.cancel()
        renameTask = nil
        pendingRenameReport = nil
        isRenameInFlight = false
        renameError = nil
        // Property-edit state lives on the same lifecycle as the
        // Add-Property sheet. `performPropertyEdit` already self-
        // guards via `loadedFilePath == path` (closeVault zeroes
        // `selectedFilePath`/`loadedFilePath` below, so the late
        // write is suppressed), but the published flags + alert
        // binding would still leak across the close. The alert
        // matters most: `currentPropertyEditConflict` drives a
        // `.alert(...)` in MainSplitView; without the reset the
        // conflict dialog survives onto the welcome screen.
        propertyEditTask?.cancel()
        propertyEditTask = nil
        isEditingProperty = false
        propertyEditError = nil
        currentPropertyEditConflict = nil
        // Template picker: cancel any in-flight picker / select /
        // create task so they can't write back into the old
        // session's state, then drop the flow + listed templates
        // so a re-open against a new vault doesn't show the prior
        // vault's templates or a half-completed flow.
        isTemplatePickerOpen = false
        templatePickerTask?.cancel()
        templatePickerTask = nil
        templateSelectionTask?.cancel()
        templateSelectionTask = nil
        templateCreateTask?.cancel()
        templateCreateTask = nil
        pendingTemplateFlow = .idle
        templateNoteNameError = nil
        availableTemplates = []
        // Release native document handles before clearing `currentSession`;
        // the release helpers need the session that created those handles.
        releaseAllCanvasDocuments()
        releaseAllBaseDocuments()
        releaseAllBaseEmbedDocuments()
        releaseAllDashboardDocuments()
        // History leaf (O-5): the event-listener unregister also needs
        // the live session.
        unregisterVaultEventListener()
        currentSession = nil
        currentVaultURL = nil
        files = []
        scanError = nil
        stopSyncMarkerWatcher()  // #638: no vault, no watch
        syncReport = nil
        liveSyncConfig = nil
        syncDiagnosticsError = nil
        // History leaf (O-5): reset every history surface (the event
        // listener was unregistered above, while the session lived).
        compactionAlertedPaths = []
        compactionFailure = nil
        resetHistoryState()
        // Connections leaf (P1-1 #554): a root/back-stack path from the
        // closing vault must never load or open against the next one
        // (review round 1 finding 3).
        resetConnectionsState()
        // Graph tab table (P1-2 #555): same cross-vault isolation.
        resetGraphTableState()
        // Graph tab diagram (P2-3 #559): tear down the layout session too.
        resetGraphDiagramState()
        // Graph config (P2-4 #560): no vault now, so drop the in-memory
        // config to defaults and un-stamp the vault marker. A pending
        // debounced save still targets the CLOSING vault via its captured
        // root and completes normally (not cancelled), so its final edit
        // isn't lost (review finding 3).
        graphConfig = .default
        graphConfigVaultURL = nil
        graphConfigWritable = true
        // U1-2: drop every tab + parked document BEFORE clearing the
        // selection — the selection funnel's snapshot would otherwise park
        // the about-to-be-discarded buffer, and mirrorSingleSelection would
        // close only the ACTIVE tab, leaving siblings pointing into a
        // closed vault.
        workspace.reset()
        pendingTabClose = nil
        pendingTabCloseAfterSave = nil
        selectedFilePath = nil
        treeSelectedNode = nil
        treeExpandedDirPaths = []  // #873: expansion dies with the vault
        pendingFolderDelete = nil  // #860: staged confirmation dies with it
        pendingBatchMove = nil  // #852: batch sheet dies with the vault
        pendingBatchDelete = nil  // #852: batch confirmation dies with the vault
        currentNoteText = nil
        currentNoteHeadings = []
        noteLoadError = nil
        currentBacklinks = []
        currentOutgoingLinks = []
        currentOutgoingLinksPath = nil
        currentNoteProperties = []
        currentNoteEmbedResolutions = [:]
        pendingEmbedPreview = nil
        currentNoteMathBlocks = []
        currentNoteCodeBlocks = []
        currentNoteDiagramBlocks = []
        mathBlocksLoadError = nil
        codeBlocksLoadError = nil
        diagramBlocksLoadError = nil
        isLoadingMathBlocks = false
        isLoadingCodeBlocks = false
        isLoadingDiagramBlocks = false
        mathBlocksLoadTask?.cancel()
        mathBlocksLoadTask = nil
        codeBlocksLoadTask?.cancel()
        codeBlocksLoadTask = nil
        diagramBlocksLoadTask?.cancel()
        diagramBlocksLoadTask = nil
        // Red-team M1: cancel any in-flight refresh-after-save
        // tasks so a save-then-immediately-close window doesn't
        // leave them grabbing the conn-mutex post-close.
        mathBlocksRefreshTask?.cancel()
        mathBlocksRefreshTask = nil
        codeBlocksRefreshTask?.cancel()
        codeBlocksRefreshTask = nil
        diagramBlocksRefreshTask?.cancel()
        diagramBlocksRefreshTask = nil
        // Milestone L #279/#280: drop citation + bibliography state
        // on vault close so a re-open starts fresh.
        citationsLoadTask?.cancel()
        citationsLoadTask = nil
        currentNoteCitations = []
        currentNoteCitationRefs = []
        citationsLoadError = nil
        isLoadingCitations = false
        expandedCitation = nil
        bibliographyEntries = []
        bibliographyLoadError = nil
        isLoadingBibliography = false
        unresolvedCitations = []
        bibliographySearchText = ""
        expandedBibEntry = nil
        filesCitingResult = nil
        bibliographyPrefs = .empty
        availableCslStyles = []
        bibliographySettingsError = nil
        isCitationSummaryOpen = false
        pendingBibliographyKeyFocus = nil
        activeStyleId = ""
        embedsLoadError = nil
        linksLoadError = nil
        currentNoteTasks = []
        tasksLoadError = nil
        // #160 / #879: clear the whole review surface (rows, error,
        // pagination cursor, total, filter) and cancel both query legs so
        // reopening on a different vault can't carry state forward. Shared
        // with `openVault` (Codex red-team — direct Open Vault bypasses
        // this close path).
        resetVaultTasksReviewState()
        isScanning = false
        isLoadingNote = false
        isLoadingLinks = false
        isLoadingTasks = false
        isLoadingVaultTasks = false
        scanProgress = nil
        // U4-3 vault switch: if this close was the front half of a switch,
        // open the parked target now that teardown is complete. Capture-then-
        // clear so the open (which resets state again) can't re-enter this
        // arm. `openRecent` re-checks the target exists on disk and posts its
        // own "Vault … opened" announcement. Cancelled switches never reach
        // here (the cancel resolvers cleared the target), so a plain Close
        // Vault after a cancelled switch can't accidentally reopen a vault.
        if let target = pendingVaultSwitchTarget {
            pendingVaultSwitchTarget = nil
            openRecent(target)
        }
    }

    /// Drop a recent-vaults entry by path. Used by the welcome screen
    /// when the user confirms removal of a missing vault.
    func removeRecent(path: String) {
        do {
            recentVaults = try recentsStore.remove(path: path)
        } catch {
            // Recents persistence isn't critical to app function — the
            // in-memory list is what the UI reads. Log so the failure
            // isn't completely silent during dev, but don't surface to
            // the user; they're already mid-flow on removing an entry.
            fputs(
                "Slate: failed to persist recent-vaults removal: \(error)\n",
                stderr
            )
        }
    }

    /// Empty the recent-vaults list. Backs the File ▸ Open Recent ▸
    /// Clear Menu item (the macOS-standard tail of an Open Recent
    /// submenu). Same fire-and-log persistence contract as
    /// `removeRecent(path:)` — the in-memory list is what the UI reads.
    func clearRecentVaults() {
        for entry in recentVaults {
            removeRecent(path: entry.path)
        }
    }

    /// Run the initial scan against the current session, then page
    /// through `listFiles` to build the sidebar's in-memory list.
    /// Called automatically after `openVault` succeeds; can be called
    /// again later (e.g. a refresh action) once we have one.
    ///
    /// Idempotent: the Rust scanner upserts on path so re-running on
    /// an already-indexed vault is fine.
    ///
    /// Honors `Task.isCancelled`: closing the vault or opening a
    /// different one cancels the wrapping task, which (a) signals the
    /// in-flight `scan_initial` via the `CancelToken` so the Rust side
    /// bails at the next per-entry cancel check, and (b) suppresses
    /// the post-scan publish so a late completion can't repopulate
    /// `files` after the user has already moved on.
    func loadFiles() async {
        guard let session = currentSession else { return }
        isScanning = true
        scanError = nil
        // #871 Codex round 3: clear the spinner only if THIS scan's vault is
        // still current (or the vault was closed) — a scan whose vault was
        // switched out from under it must not stomp the NEW vault's in-flight
        // `isScanning`. (closeVault also resets it, covering the closed case.)
        defer {
            if currentSession === session || currentSession == nil {
                isScanning = false
            }
        }

        let cancel = CancelToken()
        // Adapter bridges scanner-thread `onProgress` callbacks back
        // to the main actor where AppState can publish them. Holding a
        // strong reference here is enough to keep the FFI handle live
        // for the duration of the scan; uniffi releases it when the
        // last Swift reference goes away.
        // The Task closure needs its own explicit `[weak self]` so
        // strict-concurrency mode (Swift 6 / sendability checks) can
        // verify the capture is sendable across the @Sendable boundary
        // — without it, the implicit re-capture of `self?` from the
        // outer closure trips a "reference to captured var 'self' in
        // concurrently-executing code" diagnostic on the CI toolchain.
        let adapter = ScanProgressAdapter { [weak self] event in
            Task { @MainActor [weak self] in
                // #871 Codex round 3: drop a late progress event whose vault is
                // no longer current — it must not publish vault A's scan
                // progress into vault B after a switch.
                guard let self, self.currentSession === session else { return }
                self.handleScanProgress(event)
            }
        }

        do {
            let loaded: [FileSummary] = try await withTaskCancellationHandler {
                try Task.checkCancellation()
                // Scan + list both go through SQLite under a Mutex, so
                // dispatching off the main actor keeps the UI responsive
                // on multi-thousand-file vaults.
                return try await Task.detached(priority: .userInitiated) {
                    _ = try session.scanInitialWithProgress(
                        cancel: cancel,
                        listener: adapter
                    )
                    var all: [FileSummary] = []
                    var cursor: String? = nil
                    repeat {
                        let page = try session.listFiles(
                            // Milestone N (#702): quick open lists the
                            // openable-document set — notes, canvases, bases.
                            filter: .openableDocuments,
                            paging: Paging(cursor: cursor, limit: 1_000)
                        )
                        all.append(contentsOf: page.items)
                        cursor = page.nextCursor
                    } while cursor != nil
                    return all
                }.value
            } onCancel: {
                // Bridge structured-concurrency cancellation across to
                // the CancelToken the Rust scanner is polling.
                cancel.cancel()
            }

            // If we were cancelled mid-flight (e.g. closeVault fired
            // between the detached task starting and finishing), don't
            // overwrite the freshly-cleared state with stale results.
            // #871 Codex round 2: also re-check the SESSION is still current —
            // a direct vault switch (Open Vault / Open Recent) may not cancel
            // this task, and publishing vault A's file list after `currentSession`
            // has become vault B would show A's files under B.
            guard !Task.isCancelled, currentSession === session else { return }
            files = loaded.sorted { lhs, rhs in
                lhs.path.localizedCaseInsensitiveCompare(rhs.path) == .orderedAscending
            }
        } catch is CancellationError {
            // Cancellation isn't an error condition the user needs to
            // see; the new vault flow will start its own scan.
        } catch let error as VaultError {
            // Cancelled scans surface from Rust as VaultError.Cancelled
            // — also intentionally non-user-visible.
            if case .Cancelled = error { return }
            // #871 Codex round 3: a stale scan's error must not surface under
            // the new vault.
            guard !Task.isCancelled, currentSession === session else { return }
            scanError = humanReadable(error)
        } catch {
            guard !Task.isCancelled, currentSession === session else { return }
            scanError = error.localizedDescription
        }
    }

    // MARK: - History leaf (Milestone O-5, #543)

    /// Loaded version-history pages for the selected note, newest
    /// first ("Show older versions" appends). Reset on selection
    /// change and vault close.
    @Published var historyVersions: [VersionSummary] = []
    /// Opaque next-page cursor; `nil` = no more pages.
    @Published var historyNextCursor: String?
    /// Total non-marker versions (the section header count).
    @Published var historyTotalFiltered: UInt64 = 0
    @Published var historyLoadError: String?
    /// True from `scheduleHistoryLoad` until the NEWEST load publishes
    /// (or the panel resets on vault close). Gates the HistoryPanel's
    /// first-page spinner: the list starts EMPTY after a vault open
    /// and the load is async, so without this flag the panel shows the
    /// misleading "No versions yet" copy for the whole load window
    /// (the Tasks/Citations panels' loading idiom, applied here).
    /// Note-to-note switches LATCH the previous rows until the new
    /// publish (no flash — deliberate), so the spinner only fills
    /// genuinely-empty windows. Stale loads guard-return without
    /// touching the flag — the newest publish (first-page load OR
    /// `loadOlderVersions`, which also bumps the seq) or the reset
    /// clears it.
    @Published var isHistoryLoading = false
    /// Since-last-open verdict for the selected note. Populated ONLY
    /// when the pref is on — the compute-then-mark funnel order is
    /// pinned by o_spec §O-4/§O-5 (g3): compute first, then mark;
    /// mark-first would silently report Unchanged forever.
    @Published var sinceOpenChanges: ChangesSinceOpen?
    /// Deleted-file remnants (the "Deleted" segment).
    @Published var deletedFiles: [DeletedFileEntry] = []
    @Published var deletedLoadError: String?
    /// Pending "Restore As…" destination prompt (#795): raised when a
    /// deleted-file recovery hits an occupied destination, or from a
    /// version row's Restore As… action (materialize a copy).
    @Published var historyRestoreAsPrompt: RestoreAsPrompt?
    /// Pending restore confirmation (drives the alert).
    @Published var historyRestoreRequest: HistoryRestoreRequest?
    /// History-specific error alert (integrity failure, recover
    /// errors) — never routed through the generic save-error path.
    @Published var historyAlert: HistoryAlert?
    /// Compaction-failure alert payload (the O-2 VaultEventListener
    /// channel's Mac half).
    @Published var compactionFailure: CompactionFailure?
    /// Once-per-(path, session) gate for compaction-failure alerts —
    /// repeated failures on one file don't re-alert (the announcement-
    /// gate pattern). Reset with the session.
    var compactionAlertedPaths: Set<String> = []
    /// The alerts.md:36 "Don't Show Again" opt-out for the compaction-
    /// failure alert (#881). When true, `handleVaultEvent` routes the
    /// failure to a polite AX announcement instead of the app-modal alert
    /// — never silent (o_spec §O-2). Loaded from `PreferencesStore` in
    /// `init`; set only via `suppressCompactionFailureAlert()` (which
    /// lives in the AppState+History extension, so this can't be
    /// `private(set)` — the discipline is by convention).
    @Published var compactionAlertSuppressed: Bool = false
    /// uniffi listener registration token, unregistered on close.
    var vaultEventListenerToken: UInt64?
    /// The adapter's lifetime is ours: uniffi keeps a foreign handle,
    /// but the strong reference lives here.
    var vaultEventAdapter: VaultEventAdapter?
    /// Latest-wins sequencing for history loads (the sync-diagnostics
    /// seq pattern, #638).
    var historyLoadSeq: UInt64 = 0
    /// Bumps after a successful restore so the panel moves focus to
    /// the new head row (WCAG 2.4.3 — the old row's position shifted;
    /// "return focus" is defined as the new head).
    @Published var historyFocusHeadToken: UInt64 = 0
    /// Mirrors `PreferencesStore` key
    /// `slate.prefs.historyShowChangesSinceOpen`. Drives the UI
    /// section and the mark writes only — no core behavior.
    @Published var historyShowChangesSinceOpen: Bool = false
    /// Task handle for the per-note history load (cancelled with the
    /// other note-scoped work).
    var historyLoadTask: Task<Void, Never>?
    /// O-5 race-test seam — ALWAYS nil in production (the M-3
    /// `syncDiagnosticsPublishGate` pattern). Awaited between the
    /// detached compute and the main-actor guards so tests can park a
    /// load inside the window and prove a stale resume neither
    /// publishes NOR marks the baseline.
    var historyPublishGate: (() async -> Void)?

    // MARK: - Sync diagnostics (Milestone M-3, #534)

    /// Latest sync-detection report for the open vault. `nil` while
    /// the first load is in flight — the panel's loading state.
    @Published private(set) var syncReport: SyncDetectionReport?
    /// LiveSync plugin config status, loaded alongside the report.
    @Published private(set) var liveSyncConfig: LiveSyncConfigStatus?
    /// Specific failure message when either FFI call failed; the
    /// panel renders it with a Retry button.
    @Published private(set) var syncDiagnosticsError: String?

    /// Vault path the assertive sync announcement last fired for —
    /// the announce-once gate (the `announcedFilePath` pattern from
    /// CitationsPanel, vault-scoped). Reopening the same vault stays
    /// silent; a different vault re-arms. Deliberately NOT cleared on
    /// close: the risk story hasn't changed just because the vault
    /// was closed and reopened mid-session.
    private var syncAnnouncedVaultPath: String?

    /// M-3 (#534) race-test seam — ALWAYS nil in production. When set,
    /// `loadSyncDiagnostics` awaits it after the detached probe
    /// returns and BEFORE the publish guard, so the stale-refresh
    /// regression test can deterministically park a load inside the
    /// race window (a `Task.yield()`-based interleaving is scheduler
    /// behavior, not a guarantee — codex adversarial round 2). A nil
    /// hook is a single optional check on the load path.
    var syncDiagnosticsPublishGate: (() async -> Void)?

    /// Live marker re-detection (#638): a bounded directory watch on
    /// the detector's in-vault probe locations, debounced into
    /// `refreshSyncDiagnostics()`. Started from the post-scan
    /// continuation, torn down on vault close/switch. The
    /// announce-once gate above is what makes "newly risky
    /// mid-session" (git init, LiveSync install) announce exactly
    /// once without any watcher-side state.
    private var syncMarkerWatcher: SyncMarkerWatcher?

    /// Watcher debounce — injectable so tests don't wait
    /// production-scale quiet periods (`internal` for @testable).
    var syncMarkerWatcherDebounce: TimeInterval = 2.5

    /// Monotonic sequence for in-flight sync-diagnostics loads (#638
    /// adversarial re-review). The `currentSession === session` guard
    /// stops a stale load from publishing under a DIFFERENT vault, but
    /// within one session the initial post-scan load and a
    /// watcher-triggered refresh can overlap — and their detached
    /// probes can resume out of order. Each load captures the sequence
    /// value it bumped this to; only the load whose captured value is
    /// still the latest may publish, so an older clean probe can't
    /// clobber a newer marker-positive report. Reset on vault
    /// switch/close is unnecessary (monotonic + the session guard).
    private var syncDiagnosticsLoadSeq: Int = 0

    /// Arm the marker watcher for the current vault (#638). Called
    /// BEFORE the initial diagnostics probe in the post-scan funnel and
    /// awaited, so the watch is provably live before the probe reads —
    /// arm-then-probe closes the setup-race window (adversarial
    /// re-review). Idempotent per vault (re-arming stops the previous
    /// watcher).
    func startSyncMarkerWatcher() async {
        syncMarkerWatcher?.stop()
        syncMarkerWatcher = nil
        guard let vaultURL = currentVaultURL else { return }
        let watcher = SyncMarkerWatcher(
            root: vaultURL,
            debounceInterval: syncMarkerWatcherDebounce
        ) { [weak self] in
            // The watcher guarantees delivery on the MAIN queue, which
            // is the MainActor's executor — so assume isolation and run
            // the exact funnel the Refresh command uses directly. (Do
            // NOT wrap in an unstructured `Task { @MainActor … }`: that
            // spawns a stray task per callback whose task-local
            // allocations can outlive the caller's scope and corrupt
            // the concurrency allocator under rapid open/close churn.
            // `refreshSyncDiagnostics()` already owns its own Task.)
            // The session-identity guard in loadSyncDiagnostics makes a
            // late event after a vault switch harmless.
            MainActor.assumeIsolated {
                self?.refreshSyncDiagnostics()
            }
        }
        // Await arming: `start()` returns only once the fds are open, so
        // by the time the caller runs the initial probe the watch is
        // provably live (arm-then-probe, no setup-race window — #638
        // adversarial re-review). The await suspends without blocking
        // the main thread (no `queue.sync`).
        syncMarkerWatcher = watcher
        await watcher.start()
    }

    /// Tear down the marker watcher (vault close/switch).
    func stopSyncMarkerWatcher() {
        syncMarkerWatcher?.stop()
        syncMarkerWatcher = nil
    }

    /// Re-run detection + config read. Wired to the panel's Refresh
    /// button, the `slate.diagnostics.refreshSync` command, and its
    /// View-menu item.
    func refreshSyncDiagnostics() {
        Task { [weak self] in
            await self?.loadSyncDiagnostics()
        }
    }

    /// Load `detect_sync` + `livesync_config` off the main actor and
    /// publish (M-3, #534). Both are bounded filesystem probes — cheap,
    /// but they leave the main actor like every other FFI call. Errors
    /// land in `syncDiagnosticsError` for the panel's error state.
    func loadSyncDiagnostics() async {
        guard let session = currentSession else { return }
        // Claim the latest slot for this session (#638 adversarial
        // re-review). Bumped synchronously on the actor before the
        // detached probe; only the load still holding the top value may
        // publish, so an older overlapping same-vault load can't
        // clobber a newer one after resuming out of order.
        syncDiagnosticsLoadSeq += 1
        let seq = syncDiagnosticsLoadSeq
        let result: Result<(SyncDetectionReport, LiveSyncConfigStatus), VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    let report = try session.detectSync()
                    let config = try session.livesyncConfig()
                    return .success((report, config))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }.value
        // Race-test seam: parks the load inside the race window
        // (post-probe, pre-guard). Nil in production — see the
        // property doc.
        if let gate = syncDiagnosticsPublishGate { await gate() }
        // A vault switch mid-load: don't publish a stale report over
        // the new vault's freshly-reset state. `Task.isCancelled` only
        // covers the scanTask funnel — `refreshSyncDiagnostics()` runs
        // in its own Task that a vault switch never cancels, so a
        // refresh started under vault A resuming after a switch to B
        // would otherwise publish A's report AND fire A's assertive
        // announcement under B's gate (poisoning B's announce-once).
        // The session-identity recheck is the #328 red-team P1 pattern
        // every other post-await publish in this file uses. The seq
        // recheck adds same-session ordering: a newer load has since
        // bumped the counter, so this stale resume must not publish.
        guard !Task.isCancelled, currentSession === session,
            seq == syncDiagnosticsLoadSeq
        else { return }
        switch result {
        case .success(let (report, config)):
            syncReport = report
            liveSyncConfig = config
            syncDiagnosticsError = nil
            announceSyncFindingsIfNeeded(report)
        case .failure(let error):
            syncDiagnosticsError = humanReadable(error)
        }
    }

    /// Assertive announcement for risky sync states (m_spec §M-3):
    /// iff the report carries a multi-sync warning OR any High-risk
    /// provider, post the pre-rendered `audioSummary` at `.high`
    /// priority — at most once per vault. Low/Medium-only and empty
    /// reports stay silent: the leaf is discoverable, not shouty.
    private func announceSyncFindingsIfNeeded(_ report: SyncDetectionReport) {
        guard let vaultPath = currentVaultURL?.path else { return }
        guard syncAnnouncedVaultPath != vaultPath else { return }
        let hasHighRisk = report.providers.contains { $0.riskLevel == .high }
        guard report.multiSyncWarning != nil || hasHighRisk else { return }
        syncAnnouncedVaultPath = vaultPath
        announcer.post(report.audioSummary, priority: .high)
    }

    /// Read the selected note's bytes + indexed headings off the main
    /// actor and publish both. Surfaces InvalidUtf8 / FileTooLarge / IO
    /// via `noteLoadError`. Honors task cancellation so a fast click-
    /// through doesn't leave a stale string in `currentNoteText` or a
    /// stale outline.
    ///
    /// Both calls go in the same detached task: `read_text` and
    /// `get_file_metadata` each take the same SQLite mutex on the Rust
    /// side, so serializing them at the call site keeps the lock-
    /// contention picture predictable and ensures the text + outline
    /// we publish are from the same observation.
    func loadCurrentNote(path: String) async {
        guard let session = currentSession else { return }
        // Codex round 7: a task cancelled BEFORE its body ran must not
        // set the spinner it will never clear (this loader clears
        // explicitly at landing, not via defer — audit #201's race).
        guard !Task.isCancelled else { return }
        isLoadingNote = true

        // Capture (text, headings, contentHash) in one detached
        // task. The hash comes from `get_file_metadata.contentHash`
        // — that's the same hash the scanner cached, so it matches
        // what's on disk right now (modulo external writes between
        // the scan and this load, which `read_text` would have
        // surfaced as an error or stale-content situation). The
        // save-flow uses this hash as `expected_content_hash` so a
        // mid-edit external write is caught as `WriteConflict`.
        let result: Result<(NotePartsBundle, [Heading]), VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                // U3-3 (#467/#469): the buffer is the BODY. One read, one
                // hash — `read_note_parts` splits at the canonical scanner
                // boundary and its hash covers the WHOLE file, so the
                // composed-save conflict chain stays intact.
                let parts = try session.readNoteParts(path: path)
                // get_file_metadata returns nil when the path isn't in
                // the index yet (race: file showed up between scan and
                // selection). Empty headings is the right fallback —
                // the outline pane shows its "no headings" empty state
                // and the user still sees the content.
                let metadata = try session.getFileMetadata(path: path)
                return .success((parts, metadata?.headings ?? []))
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        // Don't touch `isLoadingNote` if the user has already moved
        // on. Same reasoning as `loadCurrentLinks`: the newer task
        // already set the flag, and clearing it here would flicker
        // the loading state off briefly.
        guard !Task.isCancelled, currentSession === session, selectedFilePath == path else { return }

        switch result {
        case .success(let (parts, headings)):
            currentNoteText = parts.body
            savedBaselineText = parts.body
            currentNoteContentHash = parts.contentHash
            currentNoteFMSource = parts.fmSource
            bodyByteOffset = Int(parts.bodyByteOffset)
            bodyLineOffset = Int(parts.bodyLineOffset)
            loadedFilePath = path
            hasUnsavedChanges = false
            // The editor + reading view live in BODY space; backend
            // offsets are whole-file. Rebase once, here — every consumer
            // downstream (outline anchors, rotor, scroll routing) is then
            // body-relative for free.
            currentNoteHeadings = Self.rebasedToBody(
                headings, prefixBytes: Int(parts.bodyByteOffset))
            noteLoadError = nil
        case .failure(let error):
            currentNoteText = nil
            savedBaselineText = nil
            currentNoteContentHash = nil
            currentNoteFMSource = ""
            bodyByteOffset = 0
            bodyLineOffset = 0
            loadedFilePath = nil
            hasUnsavedChanges = false
            currentNoteHeadings = []
            // #868 red-team (Codex): a load FAILURE for the mounted path
            // (a same-path conflict reload whose file was externally
            // deleted/corrupted) tears the note down here without going
            // through `clearActiveNoteFields`, and NoteContentView
            // unmounts the properties widget before its `.onChange` can
            // reset the mirror — the same wrong-direction "Hide
            // Properties Source" latch the delete arm had. Reset both
            // mirror fields on the error mount.
            propertiesSourceShowing = false
            propertiesSourceError = nil
            noteLoadError = humanReadable(error)
        }
        isLoadingNote = false
    }

    /// Load the inbound (backlinks) and outgoing-links lists for the
    /// currently-selected note off the main actor. Both queries hit
    /// the same SQLite mutex so we run them in one detached task to
    /// keep the lock-contention picture predictable.
    ///
    /// The backlinks query is bounded with a generous page size
    /// (200) because the sidebar panel doesn't yet paginate — that
    /// lands when we wire link activation in #C5. Vaults with more
    /// than 200 inbound links to a single note are vanishingly rare
    /// in V1 territory and will get a "+ more" affordance later.
    func loadCurrentLinks(path: String) async {
        guard let session = currentSession else { return }
        // Codex round 7: same late-body guard as `loadCurrentNote` —
        // explicit-clear loaders must never set a flag under a
        // cancellation they'll drop at landing.
        guard !Task.isCancelled else { return }
        isLoadingLinks = true

        // Pull links + properties under a single mutex acquisition.
        // Previously this called `backlinks`, `outgoingLinks`, and
        // `getFileMetadata` in sequence — three independent lock
        // grabs that each raced the scanner's transaction-long lock
        // hold (#92 item 4). The new `noteLoadBundle` API holds the
        // mutex for one contiguous slice while it runs all three
        // queries.
        let result: Result<([Backlink], [OutgoingLink], [Property]), VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    let bundle = try session.noteLoadBundle(
                        path: path,
                        backlinksPaging: Paging(cursor: nil, limit: 200)
                    )
                    return .success((bundle.backlinks.items, bundle.outgoingLinks, bundle.properties))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }
            .value

        // Don't touch `isLoadingLinks` if the user has already moved
        // on: a newer task is in flight and has already re-set the
        // flag to `true`, so clearing it here would flicker the
        // spinner off mid-load. The newer task owns the flag's
        // lifecycle from this point on.
        guard !Task.isCancelled, selectedFilePath == path else { return }

        switch result {
        case .success(let (backlinks, outgoing, properties)):
            currentBacklinks = backlinks
            currentOutgoingLinks = outgoing
            currentOutgoingLinksPath = path
            currentNoteProperties = properties
            linksLoadError = nil
        case .failure(let error):
            currentBacklinks = []
            currentOutgoingLinks = []
            // Failure is still an ANSWER for this note (empty records
            // + error surface) — stamp ownership so reading classifies
            // against it rather than a previous note's records.
            currentOutgoingLinksPath = path
            currentNoteProperties = []
            linksLoadError = humanReadable(error)
        }
        isLoadingLinks = false
    }

    // MARK: - Embeds (#187)

    /// Resolve every `is_embed = true` outgoing link in
    /// `currentOutgoingLinks` against the backend and publish the
    /// result map as `currentNoteEmbedResolutions`. Called after
    /// `loadCurrentLinks` so the link list is already populated
    /// when we fan out per-embed.
    ///
    /// Each resolution is a separate FFI call; we batch them off
    /// the main actor to avoid pinning it while the SQLite mutex
    /// is held repeatedly.
    ///
    /// Failure handling (audit #202): per-embed failures don't
    /// discard the rest of the batch. An FFI throw on one embed
    /// becomes a synthesized `EmbedResolution.unresolved(reason:
    /// .readError(message:))` for that key and the loop keeps
    /// going. `embedsLoadError` is reserved for whole-batch
    /// problems (no session, etc.) — per-embed errors land in the
    /// resolution itself where the UI's `EmbedView` already
    /// renders them with the right shape.
    func loadCurrentNoteEmbedResolutions(path: String) async {
        guard let session = currentSession else { return }
        // Snapshot the embed targets from the just-loaded links.
        // We pass these into the detached task so a selection
        // change can't race the snapshot against a partial refresh.
        //
        // Each entry carries the composed cache key plus any alias
        // spellings the same embed can be looked up under. Block
        // anchors have two authored forms — the canonical Obsidian
        // `target#^id` and the legacy bare `target^id` (#413) — and
        // the editor's Cmd+E path looks up the AUTHORED text between
        // `![[` and `]]`, so the resolution must be reachable under
        // both. The composed key uses the bare form (the FFI
        // round-trip contract); the `#^` form is registered as an
        // alias of the same resolution.
        let embedTargets: [(key: String, aliases: [String], alt: String?)] =
            currentOutgoingLinks
            .filter { $0.isEmbed }
            .map { link in
                let key = embedTargetKey(link)
                // #433: the link's authored display text — for image
                // embeds, the alt — rides along so the resolver no
                // longer re-reads the host to recover it.
                if let anchor = link.targetAnchor, anchor.kind == "block" {
                    return (key, ["\(link.targetRaw)#^\(anchor.text)"], link.displayText)
                }
                return (key, [], link.displayText)
            }
        // Codex round 5, entry guard: an orphaned chain task (created
        // by a links leg whose result was already dropped) must not
        // clear the NEW note's resolutions through the empty-targets
        // path, nor set the loading flag after the newer chain has
        // finished. Mirrors the landing guard below.
        guard !Task.isCancelled, selectedFilePath == path else { return }
        if embedTargets.isEmpty {
            currentNoteEmbedResolutions = [:]
            embedsLoadError = nil
            isLoadingEmbeds = false
            return
        }
        isLoadingEmbeds = true

        let resolutions: [String: EmbedResolution] =
            await Task.detached(priority: .userInitiated) {
                var out: [String: EmbedResolution] = [:]
                for target in embedTargets {
                    let resolution: EmbedResolution
                    do {
                        resolution = try session.resolveEmbed(
                            hostPath: path,
                            target: target.key,
                            alt: target.alt
                        )
                    } catch let error as VaultError {
                        // Per-embed failure: synthesize an
                        // Unresolved entry instead of bailing out
                        // of the whole batch (audit #202).
                        resolution = .unresolved(
                            reason: .readError(message: Self.humanReadableVaultError(error))
                        )
                    } catch {
                        resolution = .unresolved(
                            reason: .readError(message: error.localizedDescription)
                        )
                    }
                    // Same-src duplicates: the cache is keyed on
                    // target, so multiple occurrences overwrite in
                    // ordinal order and share the LAST one's
                    // resolution (incl. its alt). Per-occurrence alt
                    // exists at the resolution layer (#433) — nested
                    // embeds get it structurally; surfacing it at
                    // top level needs per-occurrence cache keys, a
                    // UI-contract change deferred with rationale on
                    // the issue.
                    out[target.key] = resolution
                    for alias in target.aliases {
                        out[alias] = resolution
                    }
                }
                return out
            }
            .value

        // Audit #201: clearing the spinner via `defer` made an old
        // cancelled task's late-firing deferred clear race the new
        // task's `isLoadingEmbeds = true`, briefly exposing the
        // panel to "not yet resolved" placeholders. Set explicitly
        // at the end, gated on the selection-change check below —
        // same pattern `loadCurrentLinks` uses.
        guard !Task.isCancelled, selectedFilePath == path else { return }

        currentNoteEmbedResolutions = resolutions
        embedsLoadError = nil
        isLoadingEmbeds = false
    }

    /// Resolve ONE embed cache key against the current note and merge it into
    /// `currentNoteEmbedResolutions` (#511, block-level reading embeds).
    ///
    /// The batch resolver (`loadCurrentNoteEmbedResolutions`) only sees embeds
    /// in the SAVED-state `currentOutgoingLinks`; reading mode renders the
    /// LIVE buffer, so a just-typed `![[…]]` can be visible with no dict
    /// entry. The reading view's block-embed placeholder calls this once per
    /// missing key to fill that gap. The `target` string is already the
    /// cache-key form (`ReadingInlineMapper.blockEmbedTarget`, ==
    /// `embedTargetKey`), so it is BOTH the resolver input and the dict key —
    /// no re-derivation.
    ///
    /// Terminal by design: whatever the resolver returns (including a
    /// synthesized `.unresolved` on a broken target or FFI failure) is written
    /// under `target`, so the placeholder collapses to a real `EmbedView`
    /// render and can't spin forever. When there is no session — the one path
    /// that writes nothing — the reading view's own request-once guard keeps
    /// the key marked, and its state machine falls back to the inline link-run
    /// (deterministic, no re-request).
    func requestReadingEmbedResolution(target: String) async {
        guard let session = currentSession, let path = selectedFilePath else { return }
        // Already landed (a racing batch, or a duplicate request the view's
        // guard didn't cover across a re-init): don't re-resolve.
        if currentNoteEmbedResolutions[target] != nil { return }

        let resolution: EmbedResolution =
            await Task.detached(priority: .userInitiated) {
                do {
                    return try session.resolveEmbed(
                        hostPath: path, target: target, alt: nil)
                } catch let error as VaultError {
                    return .unresolved(
                        reason: .readError(message: Self.humanReadableVaultError(error)))
                } catch {
                    return .unresolved(
                        reason: .readError(message: error.localizedDescription))
                }
            }
            .value

        // Drop the write if the user navigated away mid-resolve — same
        // stale-guard the batch path uses.
        guard selectedFilePath == path else { return }
        // Merge (never replace): the batch may have populated other keys while
        // this single resolve was in flight.
        currentNoteEmbedResolutions[target] = resolution
    }

    // MARK: - Content pipelines (#223)

    /// Load math blocks for `path` via the math pipeline and publish
    /// to `currentNoteMathBlocks`. Cancellable, race-guarded, drops
    /// stale writes when the user has moved on. Same shape as
    /// `loadCurrentNoteEmbedResolutions`.
    func loadCurrentNoteMathBlocks(path: String) async {
        guard let session = currentSession else { return }
        isLoadingMathBlocks = true
        // Audit #257 M2: clear the spinner on EVERY exit path,
        // including cancellation / selection-moved-away. Without
        // `defer`, the late-arriving race-guard short-circuit
        // would leave `isLoadingMathBlocks = true` forever — a
        // WCAG 4.1.3 "stuck busy state" that VoiceOver would
        // announce and never withdraw.
        defer { isLoadingMathBlocks = false }
        let result: Result<[MathBlock], VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    return .success(try session.getMathBlocks(path: path))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }
            .value
        guard !Task.isCancelled, selectedFilePath == path else { return }
        switch result {
        case .success(let blocks):
            currentNoteMathBlocks = blocks
            mathBlocksLoadError = nil
        case .failure(let err):
            currentNoteMathBlocks = []
            mathBlocksLoadError = humanReadable(err)
        }
    }

    /// Load code blocks (syntax-highlighted) for `path` and publish
    /// to `currentNoteCodeBlocks`. Same shape as
    /// `loadCurrentNoteMathBlocks`.
    func loadCurrentNoteCodeBlocks(path: String) async {
        guard let session = currentSession else { return }
        isLoadingCodeBlocks = true
        defer { isLoadingCodeBlocks = false }  // audit #257 M2
        let result: Result<[CodeBlock], VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    return .success(try session.getSyntaxTokens(path: path))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }
            .value
        guard !Task.isCancelled, selectedFilePath == path else { return }
        switch result {
        case .success(let blocks):
            currentNoteCodeBlocks = blocks
            codeBlocksLoadError = nil
        case .failure(let err):
            currentNoteCodeBlocks = []
            codeBlocksLoadError = humanReadable(err)
        }
    }

    /// Load citation references for `path`, render each against the
    /// active CSL style, and publish to `currentNoteCitations`. Same
    /// shape as `loadCurrentNoteMathBlocks` — cancellable, race-
    /// guarded, drops stale writes when the user has moved on.
    ///
    /// When no bibliography is configured (`activeStyleId.isEmpty`)
    /// we publish an empty list rather than calling render with a
    /// missing style — the panel will then show the "no bibliography
    /// configured" empty state. Citations without a matching
    /// bibliography entry still render through the renderer's
    /// unresolved path ("Unresolved citation: <key>") so screen-
    /// reader users hear what's missing.
    func loadCurrentNoteCitations(path: String) async {
        guard let session = currentSession else { return }
        isLoadingCitations = true
        defer { isLoadingCitations = false }

        // Style id source: `activeStyleId` is empty until the Settings
        // panel (#281) wires `.slate/prefs.json`'s `default_style`
        // through. An empty id means "render not possible yet" — we
        // still pull the structural citation refs so the panel can
        // show keys + line numbers, but the visual + speech forms
        // come back empty from the renderer.
        let styleId = activeStyleId

        let result: Result<([CitationReference], [RenderedCitation]), VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    let refs = try session.listCitationsInFile(path: path)
                    guard !styleId.isEmpty else {
                        // No style yet — synthesize a placeholder
                        // RenderedCitation per ref using the key as
                        // the speech form. The panel announces these
                        // as "Citation: <key>" so AT users can still
                        // hear where citations are.
                        return .success(
                            (refs, refs.map { placeholderRendered(for: $0) })
                        )
                    }
                    var rendered: [RenderedCitation] = []
                    rendered.reserveCapacity(refs.count)
                    for ref in refs {
                        rendered.append(
                            try session.renderCitation(
                                reference: ref,
                                styleId: styleId
                            )
                        )
                    }
                    return .success((refs, rendered))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }
            .value
        guard !Task.isCancelled, selectedFilePath == path else { return }
        switch result {
        case .success(let (refs, renders)):
            currentNoteCitationRefs = refs
            currentNoteCitations = renders
            citationsLoadError = nil
        case .failure(let err):
            currentNoteCitations = []
            currentNoteCitationRefs = []
            citationsLoadError = humanReadable(err)
        }
    }

    // MARK: - Bibliography prefs (#281)

    /// Read `<vault>/.slate/prefs.json` and publish its bibliography
    /// section. Called after a successful `openVault`. Parse failures
    /// surface as `bibliographyLoadError` so the user sees what went
    /// wrong rather than a silent no-bibliography state.
    func loadBibliographyPrefsFromDisk() {
        guard let vault = currentVaultURL else {
            bibliographyPrefs = .empty
            return
        }
        let store = PrefsJsonStore(vaultRoot: vault)
        do {
            let prefs = try store.readBibliographyPrefs()
            bibliographyPrefs = prefs
            bibliographySettingsError = nil
            // Seed activeStyleId from the persisted default (basename
            // without `.csl`). The didSet on activeStyleId triggers
            // a citation re-render for the current note.
            if let defaultPath = prefs.defaultStyle, !defaultPath.isEmpty {
                activeStyleId =
                    (defaultPath as NSString).lastPathComponent
                    .replacingOccurrences(of: ".csl", with: "")
            } else {
                activeStyleId = ""
            }
        } catch {
            bibliographyPrefs = .empty
            bibliographySettingsError = error.localizedDescription
        }
    }

    /// Adopt the session's effective citations config (#411): the
    /// merged `.slate/prefs.json` ⊕ vault-root `slate.json` view the
    /// Rust core resolved at open. Publishes it to the Settings
    /// surface and pushes sources into the bibliography index
    /// WITHOUT writing prefs.json — vault-shipped config must not be
    /// frozen into the app-written file by the mere act of opening
    /// the vault (red-team C1 on #411 found the previous gap: the
    /// merged config was passive and the demo vault still showed
    /// zero resolved citations).
    func adoptSessionCitationsConfig() async {
        guard let session = currentSession else { return }
        let effective = session.citationsPrefs()
        guard !effective.sources.isEmpty else {
            await refreshAvailableCslStyles()
            return
        }
        bibliographyPrefs = BibliographyPrefs(
            sources: effective.sources,
            defaultStyle: effective.defaultStyle,
            additionalStyles: effective.additionalStyles
        )
        if let defaultPath = effective.defaultStyle, !defaultPath.isEmpty {
            activeStyleId =
                (defaultPath as NSString).lastPathComponent
                .replacingOccurrences(of: ".csl", with: "")
        }
        await pushBibliographySources(effective.sources)
    }

    /// Persist `newPrefs` to disk AND push the new sources through
    /// the session (so the in-memory `bibliography_entries` table
    /// matches what the user just configured). Called by every
    /// Settings panel mutation — Add/Remove source, change default
    /// style, etc.
    func applyBibliographyPrefs(_ newPrefs: BibliographyPrefs) async {
        guard let vault = currentVaultURL else { return }
        let store = PrefsJsonStore(vaultRoot: vault)
        do {
            try store.writeBibliographyPrefs(newPrefs)
        } catch {
            bibliographySettingsError = error.localizedDescription
            return
        }
        bibliographyPrefs = newPrefs
        bibliographySettingsError = nil

        // Push sources to the session. Warnings (malformed entries,
        // missing files) flow back via `bibliographyLoadError` for
        // surfacing in the Settings UI.
        await pushBibliographySources(newPrefs.sources)
    }

    /// Push `sources` into the session's bibliography index and
    /// refresh every downstream surface (entries, styles, current
    /// note's citation renders). Shared by the Settings mutation
    /// path (`applyBibliographyPrefs`) and the vault-config adopt
    /// path (`adoptSessionCitationsConfig`) — the only difference
    /// between those is whether prefs.json gets written first.
    private func pushBibliographySources(_ sources: [BibliographySource]) async {
        guard let session = currentSession else { return }
        let result: Result<[BibLoadWarning], VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    return .success(try session.setBibliographySources(sources: sources))
                } catch let err as VaultError {
                    return .failure(err)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }
            .value
        guard !Task.isCancelled else { return }
        switch result {
        case .success(let warnings):
            if !warnings.isEmpty {
                bibliographySettingsError = warnings
                    .map { "\($0.sourcePath): \($0.message)" }
                    .joined(separator: "\n")
            }
            await loadBibliographyEntries()
            await refreshAvailableCslStyles()
            // Re-render the current note's citations against the
            // (possibly new) default style.
            if let path = selectedFilePath {
                citationsLoadTask?.cancel()
                citationsLoadTask = Task { [weak self] in
                    await self?.loadCurrentNoteCitations(path: path)
                }
            }
        case .failure(let err):
            bibliographySettingsError = humanReadable(err)
        }
    }

    /// Jump-to-Bibliography (#282). Pull the first citation key out
    /// of the currently-expanded citation's `raw` text, set the
    /// search field + filter target, and close the popover so the
    /// sidebar surface is reachable. The BibliographyPanel observes
    /// `pendingBibliographyKeyFocus` and switches to its Entries
    /// segment with the key as the query.
    func jumpToBibliographyFromExpandedCitation() {
        guard let citation = expandedCitation else { return }
        let key = extractCitationKey(from: citation.raw)
        guard !key.isEmpty else { return }
        bibliographySearchText = key
        pendingBibliographyKeyFocus = key
        expandedCitation = nil
        let resolved = bibliographyEntries.contains(where: { $0.key == key })
        let message =
            resolved
            ? "Jumped to bibliography entry: \(key)."
            : "Searching bibliography for: \(key)."
        postAccessibilityAnnouncement(message, priority: .medium)
    }

    /// Update the active CSL style id (driven by Settings panel's
    /// style picker and by the View → Citation Style menu in #282).
    /// Announces the change via VoiceOver and re-renders the current
    /// note's citations. Idempotent — no announcement when `newId`
    /// equals the current `activeStyleId`.
    func switchActiveStyle(to newId: String) {
        guard newId != activeStyleId else { return }
        activeStyleId = newId  // didSet triggers re-render
        let title =
            availableCslStyles.first(where: { $0.id == newId })?.title ?? newId
        postAccessibilityAnnouncement(
            "Citation style: \(title).",
            priority: .medium
        )
    }

    /// Refresh `availableCslStyles` from the session — picks up
    /// changes the user just made to prefs.json (e.g. adding an
    /// additional style).
    func refreshAvailableCslStyles() async {
        guard let session = currentSession else {
            availableCslStyles = []
            return
        }
        let result: Result<[CslStyleInfo], VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    return .success(try session.listCslStyles())
                } catch let err as VaultError {
                    return .failure(err)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }
            .value
        guard !Task.isCancelled else { return }
        switch result {
        case .success(let styles):
            availableCslStyles = styles
        case .failure:
            availableCslStyles = []
        }
    }

    // MARK: - Bibliography loaders (#280)

    /// Load the merged bibliography from the session's DB into
    /// `bibliographyEntries`. Cancel-safe; idempotent. Call after
    /// `setBibliographySources` or whenever the panel is opened to
    /// re-read the index. Auto-refreshes the `unresolvedCitations`
    /// list afterwards since the two views move together.
    func loadBibliographyEntries() async {
        guard let session = currentSession else {
            bibliographyEntries = []
            unresolvedCitations = []
            return
        }
        // Load-fire spy (tests only): counts real fetches so the U4-1
        // leaf-retention test can prove a mounted Bibliography leaf doesn't
        // re-fetch on leaf switch.
        bibliographyLoadCount += 1
        isLoadingBibliography = true
        defer { isLoadingBibliography = false }

        let result: Result<([BibEntry], [UnresolvedCitation]), VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    let entries = try session.getBibliographyEntries()
                    let unresolved = try session.listUnresolvedCitations()
                    return .success((entries, unresolved))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }
            .value
        guard !Task.isCancelled else { return }
        switch result {
        case .success(let (entries, unresolved)):
            bibliographyEntries = entries
            unresolvedCitations = unresolved
            bibliographyLoadError = nil
        case .failure(let err):
            bibliographyEntries = []
            unresolvedCitations = []
            bibliographyLoadError = humanReadable(err)
        }
    }

    /// Request the set of vault files that cite `key`. Populates
    /// `filesCitingResult` for the BibliographyPanel's
    /// "Show files citing this entry" sheet.
    func requestFilesCiting(key: String) async {
        guard let session = currentSession else { return }
        let result: Result<[String], VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    return .success(try session.listFilesCiting(citationKey: key))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }
            .value
        guard !Task.isCancelled else { return }
        switch result {
        case .success(let files):
            filesCitingResult = files
        case .failure:
            filesCitingResult = []
        }
    }

    /// Filtered view of `bibliographyEntries` against
    /// `bibliographySearchText`. Empty query returns everything.
    /// Substring match on title + author family + key (case-
    /// insensitive). Defined as a method (not computed property)
    /// because SwiftUI re-evaluates filtered lists every render
    /// pass and the closure form keeps the call-site explicit.
    func filteredBibliographyEntries() -> [BibEntry] {
        filterBibliographyEntries(
            bibliographyEntries,
            query: bibliographySearchText
        )
    }

    /// Load Mermaid diagram blocks (rendered SVG + structured
    /// description) for `path` and publish to
    /// `currentNoteDiagramBlocks`. Same shape as
    /// `loadCurrentNoteMathBlocks`.
    func loadCurrentNoteDiagramBlocks(path: String) async {
        guard let session = currentSession else { return }
        isLoadingDiagramBlocks = true
        defer { isLoadingDiagramBlocks = false }  // audit #257 M2
        let result: Result<[DiagramBlock], VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    return .success(try session.getDiagramBlocks(path: path))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }
            .value
        guard !Task.isCancelled, selectedFilePath == path else { return }
        switch result {
        case .success(let blocks):
            currentNoteDiagramBlocks = blocks
            diagramBlocksLoadError = nil
        case .failure(let err):
            currentNoteDiagramBlocks = []
            diagramBlocksLoadError = humanReadable(err)
        }
    }

    /// Off-actor mirror of `humanReadable(_:)` — used inside the
    /// detached resolver loop where `self` isn't available. Keeps
    /// the per-embed `ReadError` message shape consistent with
    /// what the main-actor error surface produces.
    private nonisolated static func humanReadableVaultError(_ error: VaultError) -> String {
        switch error {
        case .Io(let m), .Db(let m), .Trash(let m), .InvalidQuery(let m),
            .InvalidArgument(let m):
            return m
        case .InvalidPath(let path, let reason):
            return "Invalid path \(path): \(reason)"
        case .Cancelled:
            return "Operation cancelled."
        case .InvalidUtf8(let path):
            return "File at \(path) is not valid UTF-8."
        case .FileTooLarge(let path, let size):
            return "File at \(path) is \(size) bytes — larger than this build's refuse threshold."
        case .Unsupported(let feature):
            return "\(feature) is not implemented yet."
        case .WriteConflict:
            return "File changed externally."
        case .MalformedFrontmatter(let path, let reason):
            return "Frontmatter at \(path) is malformed: \(reason)."
        case .BibSourceUnreadable(let path, let reason):
            return "Bibliography source \(path) couldn't be opened: \(reason)."
        case .CslStyleUnreadable(let path, let reason):
            return "Citation style \(path) couldn't be loaded: \(reason)."
        case .PrefsUnreadable(let path, let reason):
            return "Preferences file \(path) couldn't be loaded: \(reason)."
        case .DestinationExists(let path):
            return "Something named \(path) already exists there."
        case .HistoryUnavailable(let path, _):
            return "History for \(path) is unavailable: it failed an integrity check."
        }
    }

    /// Navigate to the target of an embed's "Jump to source" action.
    /// Wraps the private `navigate(to:kind:)` step so the embed
    /// panel + future inline editor activations share one entry
    /// point + announcement shape.
    func openEmbedTarget(_ path: String) {
        navigate(to: path, kind: "Opened embed source")
    }

    /// Pop the editor's Cmd+E embed-preview popover for `target`.
    /// `target` is the cache key form (target + optional `#heading`
    /// / `^block` suffix); we look up the already-resolved entry
    /// in `currentNoteEmbedResolutions` rather than re-resolving.
    /// No-op when the embed isn't in the cache (e.g. the user
    /// pressed Cmd+E before the resolutions finished loading).
    ///
    /// Audit #211: previously this also posted a "preview opened"
    /// AT announcement which competed with the popover's own
    /// `.accessibilityLabel` + the EmbedView's disclosure label,
    /// firing three near-identical sentences back to back. Dropped
    /// the action confirmation — the popover's label is the
    /// canonical "what you're looking at" message.
    ///
    /// Audit #212: rapid double Cmd+E with the same target used to
    /// re-fire side effects without state changes. Short-circuit
    /// when the same target is already pending so the popover
    /// behaves like an idempotent open.
    func requestEmbedPreview(target: String, sourceLine bodySourceLine: Int? = nil) {
        // U3-3: the editor reports body lines; the popover's spatial cue
        // ("source line N") should match what the user sees in the file.
        let sourceLine = bodySourceLine.map { fileLine(fromBodyLine: $0) }
        // Idempotency: re-firing Cmd+E on the exact same embed
        // occurrence (same target AND same source line) is a
        // no-op. A second Cmd+E on a different occurrence of
        // the same target — `![[foo]]` appearing twice on
        // different lines — still updates the popover so the
        // header's line number reflects where the user actually
        // is (Codoki PR #206).
        if let existing = pendingEmbedPreview,
            existing.target == target,
            existing.sourceLine == sourceLine
        {
            return
        }
        guard let resolution = currentNoteEmbedResolutions[target] else {
            postAccessibilityAnnouncement(
                "No resolved embed at cursor.",
                priority: .medium
            )
            return
        }
        pendingEmbedPreview = EmbedPreview(
            target: target,
            resolution: resolution,
            sourceLine: sourceLine
        )
    }

    /// Dismiss the embed-preview popover. Called by the popover's
    /// SwiftUI binding when the user clicks outside, presses Esc,
    /// or activates "Jump to source" (which closes the popover
    /// and navigates).
    func dismissEmbedPreview() {
        pendingEmbedPreview = nil
    }

    /// Compose the lookup key for an embed in `currentNoteEmbedResolutions`.
    /// Mirrors how the backend's `resolve_embed` parses the raw
    /// target (target_raw + optional anchor suffix); the map is
    /// keyed on this composite so the panel can look up "the
    /// resolution for this exact `![[…]]` reference."
    ///
    /// `LinkAnchor.kind` is one of `"heading"` / `"block"` — we map
    /// that back to the `#` / `^` marker so the reconstructed
    /// target matches what the user wrote between `![[` and `]]`.
    func embedTargetKey(_ link: OutgoingLink) -> String {
        if let anchor = link.targetAnchor {
            let marker = anchor.kind == "block" ? "^" : "#"
            return "\(link.targetRaw)\(marker)\(anchor.text)"
        }
        return link.targetRaw
    }

    // MARK: - Tasks (per-note + vault-wide)

    /// Load the per-note tasks for `path` off the main actor and
    /// publish them as `currentNoteTasks`. Same lifecycle shape as
    /// `loadCurrentLinks`: cancellable, dropped on the floor if the
    /// user has moved on by the time the query returns.
    ///
    /// `tasksForFile` returns an empty Vec when the path isn't
    /// indexed yet (race between selection and scan) — we surface
    /// that as an empty `currentNoteTasks` rather than an error,
    /// matching how the panels treat "no rows" elsewhere.
    func loadCurrentNoteTasks(path: String) async {
        guard let session = currentSession else { return }
        isLoadingTasks = true
        // #159: clear the spinner on EVERY exit path, including
        // cancellation and selection-moved-away. The previous
        // shape only cleared on success/failure, so a cancellation
        // without a replacement (e.g. selection cleared mid-load)
        // could leave the spinner stuck. `defer` runs on MainActor
        // when the function returns from any path, including the
        // early `guard` below.
        defer { isLoadingTasks = false }

        let result: Result<[TaskItem], VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                let tasks = try session.tasksForFile(path: path)
                return .success(tasks)
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }
        .value

        guard !Task.isCancelled, selectedFilePath == path else { return }

        switch result {
        case .success(let tasks):
            currentNoteTasks = tasks
            tasksLoadError = nil
        case .failure(let error):
            currentNoteTasks = []
            tasksLoadError = humanReadable(error)
        }
    }

    /// Refresh `currentNoteTasks` after a successful save so the
    /// panel reflects edits to task lines without waiting for the
    /// next selection change. Mirrors `refreshHeadingsAfterSave`'s
    /// shape: off-actor query, post-await re-grab of `self`, drop
    /// the result if the user has navigated away.
    ///
    /// Returns the in-flight Task so callers can stash it on
    /// `tasksRefreshTask` (and tests can await it deterministically
    /// instead of leaning on `Task.sleep` settle waits — #161).
    @discardableResult
    private func refreshTasksAfterSave(
        session: VaultSession,
        path: String
    ) -> Task<Void, Never> {
        Task { [weak self] in
            let tasks: [TaskItem]? = await Task.detached(priority: .userInitiated) {
                try? session.tasksForFile(path: path)
            }
            .value
            guard let self else { return }
            guard self.loadedFilePath == path else { return }
            self.currentNoteTasks = tasks ?? []
        }
    }

    // MARK: - Content pipeline refresh-after-save (audit #258)
    //
    // The math / code / diagram pipelines didn't refresh after a
    // `save_text`, so editing a fenced block would leave the
    // cached arrays stale until the user navigated away. Mirrors
    // `refreshTasksAfterSave`'s shape: off-actor FFI call,
    // post-await re-grab of `self`, drop the result if the user
    // has navigated away.

    @discardableResult
    private func refreshMathBlocksAfterSave(
        session: VaultSession,
        path: String
    ) -> Task<Void, Never> {
        Task { [weak self] in
            let blocks: [MathBlock]? = await Task.detached(priority: .userInitiated) {
                try? session.getMathBlocks(path: path)
            }
            .value
            guard let self else { return }
            guard self.loadedFilePath == path else { return }
            self.currentNoteMathBlocks = blocks ?? []
        }
    }

    @discardableResult
    private func refreshCodeBlocksAfterSave(
        session: VaultSession,
        path: String
    ) -> Task<Void, Never> {
        Task { [weak self] in
            let blocks: [CodeBlock]? = await Task.detached(priority: .userInitiated) {
                try? session.getSyntaxTokens(path: path)
            }
            .value
            guard let self else { return }
            guard self.loadedFilePath == path else { return }
            self.currentNoteCodeBlocks = blocks ?? []
        }
    }

    @discardableResult
    private func refreshDiagramBlocksAfterSave(
        session: VaultSession,
        path: String
    ) -> Task<Void, Never> {
        Task { [weak self] in
            let blocks: [DiagramBlock]? = await Task.detached(priority: .userInitiated) {
                try? session.getDiagramBlocks(path: path)
            }
            .value
            guard let self else { return }
            guard self.loadedFilePath == path else { return }
            self.currentNoteDiagramBlocks = blocks ?? []
        }
    }

    /// Flip the status character on a single task in the currently-
    /// loaded note. Calls `toggleTaskStatus` with the cached
    /// `currentNoteContentHash` so a mid-edit external write
    /// surfaces as a `WriteConflict` and routes through the same
    /// resolution UI the editor uses (#64). Refreshes
    /// `currentNoteTasks` on success so the panel mirrors the new
    /// state.
    ///
    /// The status toggle is `' '` ↔ `'x'`. Tasks already in a
    /// non-standard state (e.g. `[/]` in-progress, `[-]`
    /// cancelled) are normalised to one of those two by the
    /// completion check — the panel doesn't expose a way to
    /// author custom status chars (#113 scope).
    @discardableResult
    func toggleCurrentTask(_ task: TaskItem) -> Task<Void, Never>? {
        guard let session = currentSession else { return nil }
        guard let path = loadedFilePath else { return nil }
        // #158: The editor buffer is the user's authority. The
        // toggle's post-save reload (`reloadEditorBufferAfterToggle`)
        // unconditionally overwrites `currentNoteText` from disk —
        // and the FFI's WriteConflict check doesn't catch this case
        // because `currentNoteContentHash` tracks the *disk* hash,
        // not the buffer hash (the buffer can drift dirty while the
        // disk hash stays valid). So a toggle while the editor has
        // unsaved changes would silently drop those edits. Block
        // here, prompt the user to save first.
        guard !hasUnsavedChanges else {
            postAccessibilityAnnouncement(
                "Cannot toggle task. The editor has unsaved changes in \(filename(of: path)). Save the note first.",
                priority: .high
            )
            return nil
        }
        let newChar = task.completed ? " " : "x"
        let expected = currentNoteContentHash
        let toggle: Task<Void, Never> = Task { [weak self] in
            await self?.performToggleCurrentTask(
                session: session,
                path: path,
                ordinal: task.ordinal,
                newChar: newChar,
                expectedHash: expected
            )
            return
        }
        taskToggleTask = toggle
        return toggle
    }

    private func performToggleCurrentTask(
        session: VaultSession,
        path: String,
        ordinal: UInt32,
        newChar: String,
        expectedHash: String?
    ) async {
        let outcome: Result<SaveReport, VaultError> = await Task.detached(
            priority: .userInitiated
        ) {
            do {
                let report = try session.toggleTaskStatus(
                    path: path,
                    ordinal: ordinal,
                    newStatusChar: newChar,
                    expectedContentHash: expectedHash
                )
                return .success(report)
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }
        .value

        // User could have switched files between toggle issue and
        // toggle completion. Drop the result rather than mutating
        // state attributed to a file that's no longer the active
        // selection.
        guard loadedFilePath == path else { return }

        switch outcome {
        case .success(let report):
            // The toggle path is a `save_text` under the hood, so
            // the on-disk file changed and the cached content
            // hash needs updating to match.
            currentNoteContentHash = report.newContentHash
            // The `toggleCurrentTask` guard (#158) blocks toggles
            // while `hasUnsavedChanges == true`, so we only reach
            // this branch with the buffer already matching disk
            // except for the toggled char. Re-reading from disk
            // back into `currentNoteText` + `savedBaselineText` is
            // therefore safe — no buffer edits to lose.
            //
            // Track the in-flight refresh + reload tasks and await
            // them before this toggle's `taskToggleTask` resolves
            // (#161). Production users never observe the difference
            // — `taskToggleTask` isn't subscribed by any UI — but
            // tests can `await state.taskToggleTask?.value` and
            // know the full settle has landed without sleeping.
            let refresh = refreshTasksAfterSave(session: session, path: path)
            tasksRefreshTask = refresh
            // Also refresh the editor's view of the file so the
            // toggled character appears in the rendered text.
            // Mirrors `loadCurrentNote`'s read-text shape but
            // doesn't redo the heading/property work.
            let reload = reloadEditorBufferAfterToggle(session: session, path: path)
            editorReloadTask = reload
            await refresh.value
            await reload.value
        case .failure(.WriteConflict(let currentHash, let expected, let currentMtimeMs)):
            // Same surface as the editor save's WriteConflict
            // path. The attemptedContents is the toggled file
            // contents — we don't actually have it here because
            // the FFI did the read + mutate internally, so re-
            // read from disk before stashing.
            // U3-3: SaveConflict.attemptedContents is BODY space now (the
            // keep-mine path composes it with the current fmSource).
            let attempted = ((try? session.readNoteParts(path: path))?.body) ?? ""
            currentSaveConflict = SaveConflict(
                path: path,
                attemptedContents: attempted,
                currentContentHash: currentHash,
                expectedContentHash: expected,
                currentMtimeMs: currentMtimeMs
            )
            postAccessibilityAnnouncement(
                "Toggle blocked. \(filename(of: path)) was modified externally. Resolve in the dialog.",
                priority: .medium
            )
        case .failure(let error):
            tasksLoadError = humanReadable(error)
        }
    }

    /// Re-read the on-disk file into the editor's buffer after a
    /// toggle. Toggle goes through `save_text` so the disk content
    /// changed; the editor buffer and `savedBaselineText` need to
    /// follow. Off-actor read so the SQLite-backed code path
    /// doesn't pin the main actor.
    ///
    /// Returns the in-flight Task so the caller can stash it on
    /// `editorReloadTask` (#161 — tests await this handle instead
    /// of using `Task.sleep` to wait for the buffer to settle).
    @discardableResult
    private func reloadEditorBufferAfterToggle(
        session: VaultSession,
        path: String
    ) -> Task<Void, Never> {
        Task { [weak self] in
            // U3-3: the buffer is the BODY — reload through the same split
            // the load path uses so fm + offsets stay coherent with it.
            let parts: NotePartsBundle? = await Task.detached(priority: .userInitiated) {
                try? session.readNoteParts(path: path)
            }
            .value
            guard let self, let parts else { return }
            guard self.loadedFilePath == path else { return }
            self.currentNoteText = parts.body
            self.savedBaselineText = parts.body
            self.currentNoteFMSource = parts.fmSource
            self.bodyByteOffset = Int(parts.bodyByteOffset)
            self.bodyLineOffset = Int(parts.bodyLineOffset)
            self.hasUnsavedChanges = false
        }
    }

    /// Reveal the vault-wide Tasks Review leaf (View ▸ Show Tasks Review / ⌘R
    /// / toolbar). #879: this used to present a modal sheet; it now selects the
    /// `Leaf.tasksReview` right-pane leaf, un-hides a hidden pane (the #882
    /// leaf-reveal invariant — routes through `focusLeafRegionRevealingPane()`
    /// like `showHistoryPanel` / `showConnectionsPanel`), kicks a fresh
    /// `loadVaultTasks` query, and posts a polite VoiceOver announcement. The
    /// no-session guard is preserved (the menu item + toolbar button also
    /// `.disabled` without a session).
    func openTasksReview() {
        guard currentSession != nil else { return }
        workspace.activeLeaf = .tasksReview
        // ⌘R / menu / toolbar forces a FRESH load. Cancel any in-flight
        // query first (#879 red-team: a rapid double ⌘R otherwise leaks
        // the prior task; an in-flight "Load more" would append a stale
        // page after the fresh first page).
        vaultTasksLoadTask?.cancel()
        vaultTasksLoadTask = Task { [weak self] in
            await self?.loadVaultTasks()
        }
        focusLeafRegionRevealingPane()  // #882: un-hide the pane on reveal
        postAccessibilityAnnouncement(
            "Tasks review. \(taskReviewFilter.displayName).",
            priority: .medium
        )
    }

    /// #879 red-team: Tasks Review is now a first-class rail leaf, so it's
    /// revealed WITHOUT `openTasksReview` — a rail click, a keyboard
    /// activate, or a layout restore that sets `activeLeaf = .tasksReview`.
    /// Those paths never kicked the vault query, so the panel showed a
    /// false "No tasks" empty state on a vault that has tasks. This
    /// idempotent kicker (called when the leaf becomes active) loads only
    /// when a load is both needed and possible.
    ///
    /// The review is a SNAPSHOT: it loads on its FIRST reveal per vault
    /// (rail / keyboard / restore / ⌘R) and re-queries on a filter change.
    /// A vault SWITCH resets it (`resetVaultTasksReviewState`) so the next
    /// reveal re-queries the new vault (Codex red-team: the empty/no-error
    /// guards would otherwise reject a reload and show the previous vault's
    /// rows). To refresh its rows against the CURRENT vault thereafter,
    /// press ⌘R (`openTasksReview` forces a fresh load) or switch the
    /// filter — re-revealing an already-loaded review keeps its loaded
    /// pages (this guard's `vaultTasks.isEmpty` makes it a no-op). It does
    /// NOT live-subscribe to editor edits: an auto-refresh-on-save was
    /// removed because it reset paging on every unrelated prose save and
    /// re-queried a hidden pane (Codex red-team).
    func ensureVaultTasksLoaded() {
        guard currentSession != nil,
            vaultTasks.isEmpty,
            !isLoadingVaultTasks,
            vaultTasksLoadError == nil
        else { return }
        vaultTasksLoadTask?.cancel()
        vaultTasksLoadMoreTask?.cancel()
        vaultTasksLoadTask = Task { [weak self] in
            await self?.loadVaultTasks()
        }
    }

    /// Clear the vault-wide Tasks Review surface. Called on vault CLOSE and
    /// on vault SWITCH (`openVault`) — Codex red-team: direct Open Vault
    /// bypasses `closeVault`, so without a reset here the review's
    /// empty/no-error guards reject a reload and the new vault shows the
    /// previous vault's rows (or its error) indefinitely. Cancels both
    /// query legs so a late page can't append across the switch.
    func resetVaultTasksReviewState() {
        // Supersede any in-flight load so its late defer/publish are inert.
        vaultTasksLoadGeneration += 1
        vaultTasksLoadTask?.cancel()
        vaultTasksLoadTask = nil
        vaultTasksLoadMoreTask?.cancel()
        vaultTasksLoadMoreTask = nil
        vaultTasks = []
        vaultTasksLoadError = nil
        vaultTasksNextCursor = nil
        vaultTasksTotalFiltered = 0
        isLoadingMoreVaultTasks = false
        isLoadingVaultTasks = false
        taskReviewFilter = .all
    }

    /// Tear down the vault-wide tasks query. #879: the review is now a non-modal
    /// leaf, so there's no sheet to "close" — activating a task row keeps the
    /// leaf beside the editor. This cancels the in-flight load (the
    /// `loadVaultTasks` `defer` then clears the spinner) and is the single
    /// teardown the vault-reset path routes through. Idempotent.
    func closeTasksReview() {
        vaultTasksLoadTask?.cancel()
        vaultTasksLoadTask = nil
    }

    /// Switch the active filter and re-query. Public setter so
    /// the UI's `Picker` / chips can call this rather than
    /// writing the `@Published` directly — pairs the state change
    /// with the load.
    func applyTaskReviewFilter(_ filter: TaskReviewFilter) {
        taskReviewFilter = filter
        vaultTasksLoadTask?.cancel()
        vaultTasksLoadTask = Task { [weak self] in
            await self?.loadVaultTasks()
        }
        postAccessibilityAnnouncement(
            "Filter set to \(filter.displayName).",
            priority: .medium
        )
    }

    /// Page size for the vault-wide tasks query. Matches the
    /// bench scenario `tasks_in_vault_first_page` so production
    /// behaviour is what perf is measured against. Lifted to a
    /// named constant (#160) so the inline `200` literal isn't
    /// duplicated between `loadVaultTasks` and
    /// `loadMoreVaultTasks`.
    static let vaultTasksPageSize: UInt32 = 200

    /// Run `tasksInVault` against the current filter and publish
    /// the first page as `vaultTasks`. Mirrors `loadFiles`' shape:
    /// off-actor query, cancellation-aware, no error surfaced
    /// when cancelled mid-flight. Also captures the cursor +
    /// total filtered count returned by the FFI so the review
    /// surface can show pagination affordances (#160).
    func loadVaultTasks() async {
        // #879 Codex red-team (round 3): bail BEFORE touching any shared
        // state if this task was already superseded. Cooperative
        // cancellation does NOT stop an unstarted task body from running,
        // and actor jobs aren't FIFO — so a cancelled predecessor could
        // otherwise bump the generation AFTER its replacement, fail its own
        // publish (cancelled) while superseding the replacement (stale
        // epoch), and strand the review empty.
        guard !Task.isCancelled else { return }
        guard let session = currentSession else { return }
        let filter = taskReviewFilter.toFFIFilter()
        let activeFilter = taskReviewFilter
        // #879 Codex red-team: a fresh first page invalidates any in-flight
        // "Load more" — cancel it HERE (the one funnel every first-page
        // caller reaches, incl. the review-row toggle's direct
        // `await loadVaultTasks()`) and clear its flag, so a stale page can
        // neither append after page one nor leak `isLoadingMoreVaultTasks`.
        vaultTasksLoadMoreTask?.cancel()
        isLoadingMoreVaultTasks = false
        // Generation ownership (#879 Codex red-team): only the LATEST load
        // owns `isLoadingVaultTasks` and the publish below.
        vaultTasksLoadGeneration += 1
        let generation = vaultTasksLoadGeneration
        isLoadingVaultTasks = true
        // #159: clear the spinner on EVERY exit path — but only while THIS
        // load is still current, so a superseded load's late `defer`
        // can't wipe a newer one's spinner (Codex red-team).
        defer {
            if generation == vaultTasksLoadGeneration { isLoadingVaultTasks = false }
        }

        let pageSize = Self.vaultTasksPageSize
        let result: Result<TaskWithLocationPage, VaultError> = await Task.detached(
            priority: .userInitiated
        ) {
            do {
                let page = try session.tasksInVault(
                    filter: filter,
                    paging: Paging(cursor: nil, limit: pageSize)
                )
                return .success(page)
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }
        .value

        // If the user changed the filter while we were loading, a
        // newer task is already in flight; drop this result. Also
        // drop if the underlying VaultSession was swapped (close +
        // reopen on a different vault) — the captured `session`
        // would no longer match the live one, and applying its
        // rows to `vaultTasks` would pollute the new vault's
        // surface with the old vault's tasks (#164 Codoki).
        guard !Task.isCancelled,
              generation == vaultTasksLoadGeneration,
              taskReviewFilter == activeFilter,
              currentSession === session
        else { return }

        switch result {
        case .success(let page):
            vaultTasks = page.items
            vaultTasksNextCursor = page.nextCursor
            vaultTasksTotalFiltered = page.totalFiltered
            vaultTasksLoadError = nil
        case .failure(let error):
            vaultTasks = []
            vaultTasksNextCursor = nil
            vaultTasksTotalFiltered = 0
            vaultTasksLoadError = humanReadable(error)
        }
    }

    /// Append the next page of `vaultTasks` using the cursor
    /// returned by the previous `loadVaultTasks` /
    /// `loadMoreVaultTasks` call. No-op when there's no cursor
    /// (the result set has been fully loaded) or when a load is
    /// already in flight. Returns the in-flight Task so tests
    /// can await it deterministically.
    @discardableResult
    func loadMoreVaultTasks() -> Task<Void, Never>? {
        guard let session = currentSession else { return nil }
        guard let cursor = vaultTasksNextCursor else { return nil }
        // Don't stack concurrent "Load more" requests — the button
        // is disabled while one is in flight, but a programmatic
        // caller could still spam this. Re-entrancy here would
        // double-page and append duplicates.
        guard !isLoadingMoreVaultTasks else { return nil }
        let filter = taskReviewFilter.toFFIFilter()
        let activeFilter = taskReviewFilter
        isLoadingMoreVaultTasks = true
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performLoadMoreVaultTasks(
                session: session,
                filter: filter,
                cursor: cursor,
                activeFilter: activeFilter
            )
        }
        // #879 Codex red-team: track the handle so a fresh first-page load
        // can cancel this page — its `Task.isCancelled` guard then drops a
        // stale append that would otherwise land after the fresh page one.
        vaultTasksLoadMoreTask = task
        return task
    }

    private func performLoadMoreVaultTasks(
        session: VaultSession,
        filter: TaskFilter,
        cursor: String,
        activeFilter: TaskReviewFilter
    ) async {
        // #879 Codex red-team: a SUPERSEDED (cancelled) page must not clear
        // the flag a newer "Load more" owns — that would defeat the
        // reentrancy guard and permit a duplicate page request. A fresh
        // first-page load already cleared the flag when it cancelled us.
        defer { if !Task.isCancelled { isLoadingMoreVaultTasks = false } }
        let pageSize = Self.vaultTasksPageSize
        let result: Result<TaskWithLocationPage, VaultError> = await Task.detached(
            priority: .userInitiated
        ) {
            do {
                let page = try session.tasksInVault(
                    filter: filter,
                    paging: Paging(cursor: cursor, limit: pageSize)
                )
                return .success(page)
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }
        .value

        // If the user changed the filter while the page was in
        // flight, the appended rows would not match the visible
        // filter — drop the result and let the new filter's
        // initial load take over. Same session-identity guard as
        // `loadVaultTasks` to defend against cross-vault races
        // (#164 Codoki).
        guard !Task.isCancelled,
              taskReviewFilter == activeFilter,
              currentSession === session
        else { return }

        switch result {
        case .success(let page):
            vaultTasks.append(contentsOf: page.items)
            vaultTasksNextCursor = page.nextCursor
            vaultTasksTotalFiltered = page.totalFiltered
            vaultTasksLoadError = nil
        case .failure(let error):
            // Don't clear `vaultTasks` on a failed "Load more" —
            // the user's already-visible rows are still valid; we
            // just surface the error so they know the next page
            // didn't land.
            vaultTasksLoadError = humanReadable(error)
        }
    }

    /// Toggle a task from the vault-wide review view. Routes
    /// through `toggleTaskStatus` with `expectedContentHash: nil`
    /// — the review surface doesn't track per-row hashes, and the
    /// user's explicit "I want to toggle this" intent overrides
    /// the conflict-detection that the editor save flow relies on.
    /// Re-loads vault tasks on completion so the row reflects its
    /// new state.
    ///
    /// If the toggled task belongs to the currently-loaded note,
    /// `refreshTasksAfterSave` also fires so the per-note panel
    /// stays in sync.
    @discardableResult
    func toggleVaultTask(_ row: TaskWithLocation) -> Task<Void, Never>? {
        guard let session = currentSession else { return nil }
        // #158: Toggling the currently-loaded file's task would
        // clobber any unsaved buffer edits on the post-save
        // reload — see the comment in `toggleCurrentTask` for the
        // full reasoning. Toggles against *other* files in the
        // review are safe because there's no live editor buffer
        // to lose, so we only block the loaded-file case.
        if row.path == loadedFilePath && hasUnsavedChanges {
            postAccessibilityAnnouncement(
                "Cannot toggle task. The editor has unsaved changes in \(filename(of: row.path)). Save the note first.",
                priority: .high
            )
            return nil
        }
        let newChar = row.task.completed ? " " : "x"
        let path = row.path
        let ordinal = row.task.ordinal
        let toggle: Task<Void, Never> = Task { [weak self] in
            await self?.performToggleVaultTask(
                session: session,
                path: path,
                ordinal: ordinal,
                newChar: newChar
            )
            return
        }
        taskToggleTask = toggle
        return toggle
    }

    private func performToggleVaultTask(
        session: VaultSession,
        path: String,
        ordinal: UInt32,
        newChar: String
    ) async {
        let outcome: Result<SaveReport, VaultError> = await Task.detached(
            priority: .userInitiated
        ) {
            do {
                let report = try session.toggleTaskStatus(
                    path: path,
                    ordinal: ordinal,
                    newStatusChar: newChar,
                    expectedContentHash: nil
                )
                return .success(report)
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }
        .value

        switch outcome {
        case .success:
            // Re-query the vault tasks so the row's `completed`
            // flips visually. Cheaper than mutating the array in
            // place: filter-window inclusion may have changed
            // (e.g. completing an overdue task removes it from
            // the `.overdue` filter result).
            await loadVaultTasks()
            // Per-note panel stays in sync if the toggle hit the
            // currently-loaded file. Track + await the refresh and
            // reload so this toggle's `taskToggleTask` resolves
            // only after the full settle lands (#161 — lets tests
            // skip the `Task.sleep(50ms)` pattern).
            if loadedFilePath == path {
                let refresh = refreshTasksAfterSave(session: session, path: path)
                tasksRefreshTask = refresh
                let reload = reloadEditorBufferAfterToggle(session: session, path: path)
                editorReloadTask = reload
                await refresh.value
                await reload.value
            }
        case .failure(let error):
            vaultTasksLoadError = humanReadable(error)
        }
    }

    /// Activate a `TasksReviewPanel` row: switch the file
    /// selection (if needed) and scroll the editor to the task's
    /// line. #879: the review is a non-modal leaf now, so this NO
    /// LONGER tears the review down — the leaf stays beside the
    /// editor so the user can click through several tasks in a row
    /// (the whole point of taking it out of the blocking sheet). The
    /// in-flight load is left running so the leaf keeps populating.
    /// Same selection+scroll pattern as the search-overlay
    /// activation flow.
    func openTaskRowInEditor(_ row: TaskWithLocation) {
        let target = row.path
        let line = Int(row.task.line)

        // If we're already on the file, just scroll.
        if selectedFilePath == target {
            // U3-3: task lines are whole-file; the editor buffer is body.
            lineScrollRequest.send(bodyLine(fromFileLine: line))
            postAccessibilityAnnouncement(
                "Scrolled to \(filename(of: target)), line \(line).",
                priority: .medium
            )
            return
        }

        // Otherwise switch selection + scroll once the new file's
        // load resolves so the per-line anchors exist in the
        // rendered tree before `ScrollViewReader.scrollTo` runs.
        selectedFilePath = target
        let pendingLoad = noteLoadTask
        // Track the inner Task so tests can await the deferred
        // scroll emission (#161). Production: nothing depends on
        // its completion, so this is purely a test-determinism
        // aid.
        taskRowActivationTask = Task { @MainActor [weak self] in
            if let pendingLoad {
                await pendingLoad.value
            }
            guard let self else { return }
            guard self.selectedFilePath == target else { return }
            // Offsets are current here — the awaited load set them.
            self.lineScrollRequest.send(self.bodyLine(fromFileLine: line))
            postAccessibilityAnnouncement(
                "Opened \(filename(of: target)), line \(line).",
                priority: .medium
            )
        }
    }

    // MARK: - Save flow

    /// Editor's two-way binding writes new buffer contents through
    /// this method. Keeps `currentNoteText` as the live buffer and
    /// recomputes `hasUnsavedChanges` against `savedBaselineText` so
    /// the dirty indicator and the dirty-gate stay in sync.
    ///
    /// Calling this with the same string the editor already holds
    /// is a no-op (the equality check below) — SwiftUI sometimes
    /// re-applies bindings during view updates, and we don't want
    /// that to spuriously flip the dirty flag.
    func updateEditorText(_ newText: String) {
        if currentNoteText == newText { return }
        currentNoteText = newText
        hasUnsavedChanges = (newText != (savedBaselineText ?? ""))
        // U1-2: a duplicated tab (same path in another tab) is the same
        // buffer — mirror the edit into same-path parked documents so an
        // unfocused pane never renders stale bytes. Copy-on-write assign.
        if let path = loadedFilePath {
            workspace.mirrorEdit(
                path: path, text: newText, hasUnsavedChanges: hasUnsavedChanges)
        }
    }

    /// SwiftUI `Binding<String>` for the editor view. Wraps the
    /// `currentNoteText` getter and routes writes through
    /// `updateEditorText` so the dirty-state bookkeeping happens
    /// exactly once per buffer change, regardless of how many
    /// times SwiftUI re-applies the binding during a render pass.
    func noteTextBinding() -> Binding<String> {
        Binding(
            get: { self.currentNoteText ?? "" },
            set: { self.updateEditorText($0) }
        )
    }

    /// Save the current editor buffer back to the file under
    /// `loadedFilePath`, refresh the cached hash + headings, and
    /// announce success or surface a conflict. Cmd+S calls this.
    ///
    /// Re-entrancy: a save already in flight is a no-op so the
    /// user can't queue overlapping `save_text` calls by spamming
    /// Cmd+S. The Rust side's session mutex would serialize them
    /// anyway, but this also keeps the UI flag (`isSaving`)
    /// coherent.
    ///
    /// Returns through `saveTask` so tests can `await` to
    /// deterministically observe the post-save state.
    @discardableResult
    func saveCurrentNote() -> Task<Void, Never>? {
        guard !isSaving else { return nil }
        guard let session = currentSession,
            let path = loadedFilePath,
            let contents = currentNoteText
        else { return nil }
        isSaving = true
        saveError = nil
        let expected = currentNoteContentHash
        let fmSource = currentNoteFMSource
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performSave(
                session: session,
                path: path,
                contents: contents,
                fmSource: fmSource,
                expectedHash: expected
            )
            return
        }
        saveTask = task
        return task
    }

    /// Inner save body. Split out so it can be reused by
    /// `resolveSaveConflictKeepMine` (which re-saves with the
    /// current on-disk hash so the user's bytes win).
    private func performSave(
        session: VaultSession,
        path: String,
        contents: String,
        fmSource: String,
        expectedHash: String?
    ) async {
        // Detached so the SQLite-mutex-holding save doesn't pin the main
        // actor while disk IO + tree rewrites run. U3-3 (#469): `contents`
        // is the BODY; `save_composed` reassembles fm ⊕ body through the
        // one Rust composer and then runs the existing save_text machinery
        // verbatim (conflict detection, atomic write, index, op-log).
        let outcome: Result<SaveReport, VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                let report = try session.saveComposed(
                    path: path,
                    fmSource: fmSource,
                    body: contents,
                    expectedContentHash: expectedHash
                )
                return .success(report)
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        if let gate = basesPostWritePublishGate { await gate() }

        // The user could have switched files (or closed the vault)
        // while we were saving. Drop the result in that case
        // rather than mutating state for a file the user has
        // already moved on from.
        guard currentSession === session else { return }
        if case .success = outcome {
            // Bases are session-global consumers. A same-session note switch
            // must not suppress their refresh after this write committed.
            refreshVisibleBasesAfterInAppWrite(session: session, changedPath: path)
        }
        guard loadedFilePath == path else {
            isSaving = false
            return
        }

        switch outcome {
        case .success(let report):
            currentNoteContentHash = report.newContentHash
            savedBaselineText = contents
            hasUnsavedChanges = false
            // U1-2: same-path parked documents share the file — a save
            // updates their baseline/hash too (one file, one truth).
            workspace.mirrorSaveResult(
                path: path, baseline: contents,
                contentHash: report.newContentHash)
            // U1-2: a close-tab request that chose "Save" completes its
            // close once the save lands cleanly.
            if let pending = pendingTabCloseAfterSave,
                workspace.model.activeGroup.activeTabID == pending {
                pendingTabCloseAfterSave = nil
                performCloseTab(pending)
            }
            // Refresh headings so the outline matches the just-
            // saved buffer. Same shape as `loadCurrentNote` —
            // a metadata fetch, no need to re-read text. The
            // links/properties panels still get refreshed by the
            // existing `loadCurrentLinks` path when the user next
            // switches selection; refreshing them here would
            // double-spin the SQLite mutex for marginal benefit.
            refreshHeadingsAfterSave(session: session, path: path)
            tasksRefreshTask = refreshTasksAfterSave(session: session, path: path)
            // Audit #258: refresh the content-pipeline caches so an
            // edit that adds / removes / changes a math / code /
            // diagram block reflects in the panels without waiting
            // for the next selection-change. The race-guard inside
            // each refresher (loadedFilePath == path) drops the
            // result if the user has moved on, but red-team M1
            // flagged that orphan tasks still grab the conn-mutex
            // on close-during-save / switch-during-save — so we
            // stash the handles on the AppState and cancel them
            // from `closeVault` + the selection-change clear path.
            mathBlocksRefreshTask?.cancel()
            mathBlocksRefreshTask = refreshMathBlocksAfterSave(
                session: session, path: path
            )
            codeBlocksRefreshTask?.cancel()
            codeBlocksRefreshTask = refreshCodeBlocksAfterSave(
                session: session, path: path
            )
            diagramBlocksRefreshTask?.cancel()
            diagramBlocksRefreshTask = refreshDiagramBlocksAfterSave(
                session: session, path: path
            )
            postAccessibilityAnnouncement(
                "Saved \(filename(of: path)).",
                priority: .medium
            )
        case .failure(.WriteConflict(let currentHash, let expected, let currentMtimeMs)):
            // U1-2: a save-then-close chain aborts on conflict — the tab
            // must stay open while the user resolves the dialog.
            pendingTabCloseAfterSave = nil
            currentSaveConflict = SaveConflict(
                path: path,
                attemptedContents: contents,
                currentContentHash: currentHash,
                expectedContentHash: expected,
                currentMtimeMs: currentMtimeMs
            )
            // Polite announcement: surface the conflict state
            // without yanking focus away from whatever the user
            // is currently doing in the editor. The alert itself
            // is modal and will steal focus when SwiftUI presents
            // it.
            postAccessibilityAnnouncement(
                "Save blocked. \(filename(of: path)) was modified externally. Resolve in the dialog.",
                priority: .medium
            )
        case .failure(let error):
            pendingTabCloseAfterSave = nil
            saveError = humanReadable(error)
        }
        isSaving = false
    }

    /// Re-run the save with `expected_content_hash` set to the
    /// hash we just observed on disk. The user explicitly chose to
    /// overwrite the external version; resetting `expected` to
    /// `current` removes the conflict guard so the second save
    /// goes through.
    @discardableResult
    func resolveSaveConflictKeepMine() -> Task<Void, Never>? {
        guard let conflict = currentSaveConflict,
            let session = currentSession,
            loadedFilePath == conflict.path
        else {
            currentSaveConflict = nil
            return nil
        }
        // Clear the conflict so the alert dismisses immediately
        // — the in-flight task takes over from here.
        currentSaveConflict = nil
        isSaving = true
        let task: Task<Void, Never> = Task { [weak self] in
            // Keep-mine composes MY fm ⊕ MY body over the external write —
            // the same "my whole state wins" semantics the whole-file
            // keep-mine had before the body flip.
            await self?.performSave(
                session: session,
                path: conflict.path,
                contents: conflict.attemptedContents,
                fmSource: self?.currentNoteFMSource ?? "",
                expectedHash: conflict.currentContentHash
            )
            return
        }
        saveTask = task
        return task
    }

    /// Discard the in-editor buffer for the conflicted file and
    /// reload the current on-disk version. Equivalent to "let the
    /// external write win." Clears the conflict either way so the
    /// alert can dismiss.
    @discardableResult
    func resolveSaveConflictReloadFromDisk() -> Task<Void, Never>? {
        guard let conflict = currentSaveConflict else { return nil }
        currentSaveConflict = nil
        hasUnsavedChanges = false
        // Same path the conflict came from — kick `loadCurrentNote`
        // to refresh text + hash + headings together. If the user
        // has since navigated away, the load's `selectedFilePath
        // == path` guard will drop the result.
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.loadCurrentNote(path: conflict.path)
            return
        }
        noteLoadTask = task
        return task
    }

    /// Dismiss the conflict alert without writing or reloading.
    /// The buffer stays as the user left it; `hasUnsavedChanges`
    /// stays true so the indicator and dirty-gate remain active.
    func resolveSaveConflictCancel() {
        currentSaveConflict = nil
    }

    // MARK: - File management (U2-5, #463)

    /// A structural mutation the tree must react to. Carries the *primary*
    /// post-mutation path (the created/renamed/moved node's new path; nil for a
    /// delete) plus the parent path(s) whose levels changed, so
    /// `FileTreeViewModel` can invalidate exactly the touched levels and then
    /// compute the post-mutation focus target (U2-6). `token` makes the seam
    /// edge-trigger even when two mutations are otherwise equal.
    struct TreeMutation: Equatable {
        enum Kind: Equatable {
            /// A folder was created at `path` (parent = its parent level).
            case createFolder(path: String)
            /// A note was created at `path` (parent = its parent level).
            case createNote(path: String)
            /// `oldPath` → `newPath`, same parent level.
            case rename(oldPath: String, newPath: String)
            /// `oldPath` → `newPath`; source + destination parent levels both
            /// change (`newParent` is "" for the vault root).
            case move(oldPath: String, newPath: String, oldParent: String, newParent: String)
            /// `path` was deleted; `parent` is the level it left ("" = root).
            case delete(path: String, parent: String, wasDirectory: Bool)
        }

        let token: Int
        let kind: Kind
        /// The op's rewritten-file count (distinct files whose links were
        /// updated) — U2-6's ", updated links in N notes." suffix reads this.
        let rewrittenCount: Int

        /// The parent-level paths this mutation dirtied (nil = the vault root
        /// level). The tree invalidates each. A move dirties two; everything
        /// else dirties one.
        var affectedParents: [String?] {
            switch kind {
            case let .createFolder(path), let .createNote(path):
                return [Self.parentPath(of: path)]
            case let .rename(oldPath, _):
                return [Self.parentPath(of: oldPath)]
            case let .move(_, _, oldParent, newParent):
                return [Self.normalizedParent(oldParent), Self.normalizedParent(newParent)]
            case let .delete(path, _, _):
                return [Self.parentPath(of: path)]
            }
        }

        /// The parent path of a vault-relative path, or nil if it's a root-level
        /// entry (whose parent is the vault root).
        static func parentPath(of path: String) -> String? {
            guard let slash = path.lastIndex(of: "/") else { return nil }
            return String(path[path.startIndex..<slash])
        }

        /// "" (the root sentinel used by the mutation API) maps to nil (the
        /// tree's root level); any other parent stays as-is.
        static func normalizedParent(_ parent: String) -> String? {
            parent.isEmpty ? nil : parent
        }
    }

    /// A link-rewrite partial failure surfaced to the user (spec §U2-5). The
    /// mutation succeeded; `skipped` lists the notes whose links to it could not
    /// be updated. `verb`/`name` build the alert title.
    struct StructuralFailureReport: Identifiable, Equatable {
        let id = UUID()
        let verb: String
        let name: String
        let skipped: [String]
    }

    /// A tree node in inline-rename mode (U2-5).
    struct RenamingNode: Equatable {
        let path: String
        let isDirectory: Bool
        /// The current final component (the field's initial text).
        var name: String { (path as NSString).lastPathComponent }
    }

    /// The node whose Move-to-folder sheet is open (U2-5).
    struct PendingMove: Equatable, Identifiable {
        let path: String
        let isDirectory: Bool
        var id: String { path }
        var name: String { (path as NSString).lastPathComponent }
    }

    /// A multi-selection awaiting a Move-to-folder destination (#852). `items`
    /// is the deduplicated top-level selection (a folder and something inside
    /// it collapse to just the folder — see `topLevelSelection`). `id` is
    /// content-stable so `.sheet(item:)`-style presentation is well-behaved.
    struct BatchMove: Equatable, Identifiable {
        let items: [TreeSelection]
        var id: String { items.map(\.path).joined(separator: "\n") }
        /// The sheet's title/hint noun phrase — "3 items".
        var displayName: String {
            "\(items.count) \(items.count == 1 ? "item" : "items")"
        }
    }

    /// True while any structural mutation FFI call is in flight — serializes the
    /// commands (the session lock does too, but this stops the UI from firing a
    /// second mutation before the first's tree refresh lands).
    private var isMutatingStructure = false

    /// Ownership token for the in-flight structural mutation (#871 Codex
    /// round 2). Every structural op captures the token `beginStructuralMutation`
    /// hands it, and its completion only releases `isMutatingStructure` if the
    /// token still matches (`endStructuralMutation`). This closes a cross-vault
    /// race the plain flag couldn't: an op that passes its `currentSession ===
    /// session` guard and THEN suspends in `await loadFiles()` would, on resume,
    /// unconditionally clear the flag — even though the user switched vaults
    /// during the suspension and a NEW op already claimed it. A vault
    /// open/close bumps the token, so any such stale completion is a no-op.
    private var structuralMutationToken = 0

    /// Claim the structural-mutation guard for a new op and return its
    /// ownership token. Callers have already checked `!isMutatingStructure`.
    private func beginStructuralMutation() -> Int {
        structuralMutationToken &+= 1
        isMutatingStructure = true
        return structuralMutationToken
    }

    /// Release the structural-mutation guard IFF `token` still owns it — a
    /// stale task whose vault was switched out from under it (its token was
    /// bumped by open/close, or a newer op) must NOT clear a newer op's flag.
    private func endStructuralMutation(_ token: Int) {
        guard structuralMutationToken == token else { return }
        isMutatingStructure = false
    }

    /// Invalidate any in-flight structural mutation's ownership and release the
    /// guard — called from the vault open (direct-switch reset) and close
    /// paths. Bumping the token means a stale completion's `endStructuralMutation`
    /// no longer matches, so it can't clear a newer vault's op; clearing the
    /// flag itself un-wedges the new vault (the stale task's own session guard
    /// returns before its release, so nothing else clears it).
    private func cancelStructuralMutationOwnership() {
        structuralMutationToken &+= 1
        isMutatingStructure = false
    }

    /// Monotone token backing `TreeMutation.token`.
    private var treeMutationCounter = 0

    /// The most recent structural-mutation task kicked off by a COMMAND entry
    /// point (`newNoteCommand`, `newFolderCommand`, `deleteSelectedCommand`, …).
    /// Command actions run through the registry and can't return their Task, so
    /// this handle lets tests `await` the mutation deterministically — the same
    /// role `scanTask`/`noteLoadTask` play for their flows.
    private(set) var pendingStructuralTaskForTesting: Task<Void, Never>?

    /// Publish a tree mutation to the sidebar seam, bumping the edge-trigger
    /// token. Also fires the U2-6 announcement (`postMutationAnnouncement`,
    /// filled in that PR; a no-op stub until then).
    private func publishTreeMutation(_ kind: TreeMutation.Kind, rewrittenCount: Int) {
        treeMutationCounter += 1
        treeMutation = TreeMutation(
            token: treeMutationCounter, kind: kind, rewrittenCount: rewrittenCount)
        // #871 Codex round 1: every structural mutation that is NOT a recorded
        // move/rename is a structural-history BARRIER — it clears the undo/redo
        // stacks. A create / duplicate / import / delete can FREE or REFILL a
        // path an existing inverse names (e.g. move a.md→dest, delete
        // dest/a.md, import a different a.md→dest), so replaying that inverse
        // afterward would move/rename the WRONG replacement file. move/rename
        // are the only undoable ops; they push their own inverse immediately
        // after this call, so they must NOT clear. This is the single choke
        // point every structural op routes through on success.
        switch kind {
        case .move, .rename:
            break
        case .createFolder, .createNote, .delete:
            clearStructuralUndoStacks()
        }
    }

    /// Turn a `StructuralReport.failed` list into the user-facing skipped-files
    /// alert, if non-empty. Never silent (spec §U2-5).
    private func surfaceStructuralFailures(
        _ report: StructuralReport, verb: String, name: String
    ) {
        guard !report.failed.isEmpty else { return }
        structuralFailureReport = StructuralFailureReport(
            verb: verb, name: name,
            skipped: report.failed.map(\.path).sorted())
    }

    /// The count of DISTINCT files whose links a report rewrote — the number
    /// U2-6's ", updated links in N notes." suffix announces.
    static func distinctRewrittenCount(_ report: StructuralReport) -> Int {
        Set(report.rewritten.map(\.path)).count
    }

    /// The last message routed through `postMutationAnnouncement`. Tests assert
    /// the VERBATIM string here (spec §U2-6); `postAccessibilityAnnouncement` is
    /// a no-op under the XCTest runner — there's no `NSApp` — so the string must
    /// be observable without a live announcement.
    @Published private(set) var lastMutationAnnouncement: String?

    /// Announce a completed (or failed) structural mutation to VoiceOver
    /// (spec §U2-6). The wrappers build the verbatim sentence; this seam routes
    /// it through the existing `postAccessibilityAnnouncement` helper at
    /// `.medium` priority (the politeness floor that survives — see the palette
    /// / search precedent) and records it for the verbatim-string tests.
    func postMutationAnnouncement(_ message: String) {
        lastMutationAnnouncement = message
        postAccessibilityAnnouncement(message, priority: .medium)
    }

    // MARK: Create

    /// Create a new folder named `name` inside `parent` ("" = vault root). On
    /// success: refreshes the affected tree level + selects the new row (U2-6),
    /// announces, and — never silently — surfaces any link-rewrite failures
    /// (a create has none, but the discipline is uniform). A collision /
    /// invalid name surfaces through `lastError` (the tree's create affordance
    /// has no inline field; the palette/menu path reports via the alert).
    ///
    /// `onResult` (#852, Codex finding 2): the create's ACTUAL outcome — `true`
    /// only on `.success`, `false` on failure OR a mid-flight vault switch. The
    /// "New Folder… then move" flows gate the dependent move SOLELY on this (plus
    /// session identity), never on folder existence — an empty pre-existing "New
    /// Folder" would make the create fail with DestinationExists yet still be on
    /// disk, so an existence check would wrongly move the selection into it.
    @discardableResult
    func createFolder(
        name: String, in parent: String, onResult: ((Bool) -> Void)? = nil
    ) -> Task<Void, Never>? {
        guard !isMutatingStructure, let session = currentSession else {
            onResult?(false)
            return nil
        }
        let path = Self.joinVaultPath(parent, name)
        let token = beginStructuralMutation()
        let task = Task { [weak self] in
            let outcome: Result<StructuralReport, VaultError> = await Task.detached(
                priority: .userInitiated
            ) {
                do { return .success(try session.createFolder(path: path)) }
                catch let e as VaultError { return .failure(e) }
                catch { return .failure(.Io(message: error.localizedDescription)) }
            }.value
            guard let self, self.currentSession === session else {
                onResult?(false)
                return
            }
            switch outcome {
            case .success(let report):
                self.publishTreeMutation(
                    .createFolder(path: path),
                    rewrittenCount: Self.distinctRewrittenCount(report))
                self.postMutationAnnouncement(
                    "Created folder \((path as NSString).lastPathComponent).")
                onResult?(true)
            case .failure(let error):
                self.lastError = self.humanReadable(error)
                self.announceMutationFailure(
                    verb: "create folder",
                    name: (path as NSString).lastPathComponent, error: error)
                onResult?(false)
            }
            self.endStructuralMutation(token)
        }
        return task
    }

    /// Create a new untitled note inside `parent` ("" = vault root), open it in
    /// the current tab, and put the tree row into inline-rename mode with the
    /// title selected (spec §U2-5: "creates 'Untitled.md' … opens it, selects
    /// title for rename"). Collisions auto-suffix ("Untitled 2.md", …) so ⌘N is
    /// never a dead end.
    @discardableResult
    func createNote(in parent: String) -> Task<Void, Never>? {
        guard !isMutatingStructure, let session = currentSession else { return nil }
        let token = beginStructuralMutation()
        // Compute a non-colliding name against the known file set. The backend
        // still guards the collision (DestinationExists) — this just spares the
        // user a failure on the common "several untitled notes" flow.
        let name = uniqueUntitledName(in: parent)
        let path = Self.joinVaultPath(parent, name)
        let task = Task { [weak self] in
            let outcome: Result<StructuralReport, VaultError> = await Task.detached(
                priority: .userInitiated
            ) {
                do {
                    // A new note is an empty file. create_exclusive (O-3) is
                    // the no-clobber primitive: a name race against a file
                    // the index hasn't seen yet (external create between
                    // scan and click) surfaces DestinationExists instead of
                    // truncating it — save_text would silently overwrite
                    // (#796).
                    _ = try session.createExclusive(path: path, content: "")
                    return .success(
                        StructuralReport(opId: 0, moved: [], rewritten: [], failed: []))
                } catch let e as VaultError { return .failure(e) }
                catch { return .failure(.Io(message: error.localizedDescription)) }
            }.value
            guard let self, self.currentSession === session else { return }
            switch outcome {
            case .success:
                self.publishTreeMutation(.createNote(path: path), rewrittenCount: 0)
                self.postMutationAnnouncement(
                    "Created note \((path as NSString).lastPathComponent).")
                // Refresh the flat file list so the row exists, then open it.
                await self.loadFiles()
                self.openFile(path, target: .currentTab)
                // Drop the user straight into renaming the fresh note's title.
                self.renamingNode = RenamingNode(path: path, isDirectory: false)
            case .failure(let error):
                self.lastError = self.humanReadable(error)
                self.announceMutationFailure(
                    verb: "create note",
                    name: (path as NSString).lastPathComponent, error: error)
            }
            self.endStructuralMutation(token)
        }
        return task
    }

    // MARK: Duplicate (#853)

    /// Duplicate the FILE at `path` to a collision-safe sibling
    /// ("name copy.md", "name copy 2.md", …). Folders are out of scope
    /// (#853 notes the file-only cut).
    ///
    /// Backend-safe by construction: the copy is written with
    /// `create_exclusive` (the O-3 no-clobber primitive, #793/#796 context),
    /// so a name race against a file the index hasn't seen yet surfaces
    /// `DestinationExists` — the loop then advances to the next candidate
    /// instead of truncating anything. On success: refreshes the tree level +
    /// moves selection to the copy (the U2-6 funnel, via a `.createNote`
    /// mutation — a duplicate IS a created note), and announces
    /// "Duplicated <src> as <copy>.".
    @discardableResult
    func duplicateEntry(path: String) -> Task<Void, Never>? {
        guard !isMutatingStructure, let session = currentSession else { return nil }
        let token = beginStructuralMutation()
        let sourceName = (path as NSString).lastPathComponent
        let parent = TreeMutation.parentPath(of: path) ?? ""
        let task = Task { [weak self] in
            let outcome: Result<String, VaultError> = await Task.detached(
                priority: .userInitiated
            ) {
                do {
                    let content = try session.readText(path: path)
                    // Seed the collision set from the live level listing
                    // (files AND folders — a folder named "a copy" blocks
                    // that name too). The index can lag disk; the exclusive-
                    // create below is the authoritative guard.
                    var existing = Set<String>()
                    if let listing = try? session.listDirChildren(
                        parentPath: parent,
                        paging: Paging(cursor: nil, limit: FileTreeViewModel.levelPageLimit))
                    {
                        for dir in listing.dirs { existing.insert(dir.name.lowercased()) }
                        for file in listing.files.items { existing.insert(file.name.lowercased()) }
                    }
                    // Exclusive-create loop: on DestinationExists (an
                    // un-indexed on-disk sibling), record the candidate as
                    // taken and re-derive. Bounded so a pathological
                    // directory can't spin forever.
                    for _ in 0..<200 {
                        let candidate = Self.duplicateName(
                            for: sourceName, existingLowercasedNames: existing)
                        let candidatePath = Self.joinVaultPath(parent, candidate)
                        do {
                            _ = try session.createExclusive(
                                path: candidatePath, content: content)
                            return .success(candidatePath)
                        } catch VaultError.DestinationExists {
                            existing.insert(candidate.lowercased())
                        }
                    }
                    return .failure(
                        .Io(message: "could not find a free name after 200 attempts"))
                } catch let e as VaultError { return .failure(e) }
                catch { return .failure(.Io(message: error.localizedDescription)) }
            }.value
            guard let self, self.currentSession === session else { return }
            switch outcome {
            case .success(let copyPath):
                let copyName = (copyPath as NSString).lastPathComponent
                self.publishTreeMutation(.createNote(path: copyPath), rewrittenCount: 0)
                self.postMutationAnnouncement("Duplicated \(sourceName) as \(copyName).")
                await self.loadFiles()
            case .failure(let error):
                self.lastError = self.humanReadable(error)
                self.announceMutationFailure(
                    verb: "duplicate", name: sourceName, error: error)
            }
            self.endStructuralMutation(token)
        }
        return task
    }

    /// Pure: the next collision-safe duplicate name for `name` given the
    /// (lowercased) sibling names already taken. Finder-parity naming:
    /// "a.md" → "a copy.md" → "a copy 2.md" → "a copy 3.md" — a source that
    /// already ends in " copy" / " copy N" re-uses its base rather than
    /// stacking ("a copy.md" duplicates to "a copy 2.md", not
    /// "a copy copy.md"). Static + nonisolated (pure string work) so the
    /// naming rule is regression-locked without a vault AND callable from
    /// the detached mutation task (the `humanReadableVaultError` pattern).
    nonisolated static func duplicateName(
        for name: String, existingLowercasedNames: Set<String>
    ) -> String {
        let ns = name as NSString
        let ext = ns.pathExtension
        var stem = ns.deletingPathExtension
        // Strip an existing " copy" / " copy N" suffix from the stem.
        if stem.hasSuffix(" copy") {
            stem = String(stem.dropLast(" copy".count))
        } else if let range = stem.range(of: #" copy \d+$"#, options: .regularExpression) {
            stem = String(stem[stem.startIndex..<range.lowerBound])
        }
        func candidate(_ n: Int?) -> String {
            let base = n.map { "\(stem) copy \($0)" } ?? "\(stem) copy"
            return ext.isEmpty ? base : "\(base).\(ext)"
        }
        if !existingLowercasedNames.contains(candidate(nil).lowercased()) {
            return candidate(nil)
        }
        var n = 2
        while existingLowercasedNames.contains(candidate(n).lowercased()) { n += 1 }
        return candidate(n)
    }

    // MARK: Rename

    /// Rename the file or folder at `path` to `newName` (a single path
    /// component). On success: retargets any open tab holding the file (or a
    /// descendant of the folder) so it follows the move, refreshes the tree
    /// level + keeps the renamed row selected (U2-6), announces (incl. the
    /// links-updated suffix), and surfaces any per-file rewrite failures. A
    /// collision / invalid name is returned via `structuralRenameError` so the
    /// inline field can show it and keep focus (spec §U2-5).
    ///
    /// `undoContext` (#871): `.record` (the default every user-facing caller
    /// takes — inline rename, the command, undo/redo of a MOVE never lands
    /// here) pushes the inverse rename onto the structural undo stack; the
    /// `.undoing`/`.redoing` contexts are how `structuralUndo/Redo` re-enter
    /// this same FFI path to reverse a rename while feeding the opposite stack.
    @discardableResult
    func renameEntry(
        path: String, isDirectory: Bool, to newName: String,
        undoContext: StructuralUndoContext = .record
    ) -> Task<Void, Never>? {
        guard !isMutatingStructure, let session = currentSession else { return nil }
        let token = beginStructuralMutation()
        structuralRenameError = nil
        let task = Task { [weak self] in
            let outcome: Result<StructuralReport, VaultError> = await Task.detached(
                priority: .userInitiated
            ) {
                do {
                    let report =
                        isDirectory
                        ? try session.renameFolder(path: path, newName: newName)
                        : try session.renameFile(path: path, newName: newName)
                    return .success(report)
                } catch let e as VaultError { return .failure(e) }
                catch { return .failure(.Io(message: error.localizedDescription)) }
            }.value
            guard let self, self.currentSession === session else { return }
            switch outcome {
            case .success(let report):
                let newPath = Self.siblingPath(of: path, newName: newName)
                self.applyRetargets(report, movedFrom: path, movedTo: newPath, isDirectory: isDirectory)
                self.renamingNode = nil
                self.publishTreeMutation(
                    .rename(oldPath: path, newPath: newPath),
                    rewrittenCount: Self.distinctRewrittenCount(report))
                if undoContext == .record {
                    self.postMutationAnnouncement(
                        self.mutationSentence(
                            "Renamed \((path as NSString).lastPathComponent) to \(newName).",
                            report: report))
                } else {
                    // #871: an undo/redo of a rename announces "Undid/Redid
                    // rename to <target>." instead of the fresh-op sentence.
                    self.postMutationAnnouncement(
                        self.structuralUndoRedoAnnouncement(
                            executed: .rename(path: path, isDirectory: isDirectory, newName: newName),
                            context: undoContext))
                }
                // #871: record the inverse (rename `newPath` back to the OLD
                // name) on the correct stack for the given context.
                self.recordStructuralUndo(
                    inverse: .rename(
                        path: newPath, isDirectory: isDirectory,
                        newName: (path as NSString).lastPathComponent),
                    context: undoContext)
                self.surfaceStructuralFailures(
                    report, verb: "rename", name: (path as NSString).lastPathComponent)
                await self.loadFiles()
            case .failure(let error):
                if undoContext == .record {
                    // Inline rename: keep the field open + focused with a
                    // specific message it renders below itself.
                    self.structuralRenameError = self.humanReadable(error)
                } else {
                    // #871 Codex round 1: an undo/redo rename has NO inline
                    // field (structural undo routes only when
                    // `renamingNode == nil`), so a failure — the target was
                    // externally deleted, or the restored name now collides —
                    // must surface through the general alert path a failed
                    // MOVE takes, not `structuralRenameError` (which no
                    // visible field would render → a silent failure).
                    self.structuralFailureReport = StructuralFailureReport(
                        verb: "rename", name: (path as NSString).lastPathComponent,
                        skipped: [])
                    self.lastError = self.humanReadable(error)
                }
                self.announceMutationFailure(
                    verb: "rename",
                    name: (path as NSString).lastPathComponent, error: error)
            }
            self.endStructuralMutation(token)
        }
        return task
    }

    /// Surfaced when `renameEntry` fails (collision / invalid name). The inline
    /// rename field renders this below itself and keeps focus (never silent).
    @Published var structuralRenameError: String?

    // MARK: Move

    /// Move the file or folder at `path` under `newParent` ("" = vault root).
    /// Same success handling as rename (retarget open tabs, refresh both
    /// affected levels, announce, surface failures). A collision / invalid
    /// destination (incl. moving a folder into its own subtree) surfaces via the
    /// skipped-files alert path's sibling — the move error alert.
    ///
    /// `undoContext` (#871): `.record` (the default — drag-drops, the Move
    /// sheet, the `createFolderThenMove` inner move) pushes the inverse move
    /// onto the structural undo stack; `.undoing`/`.redoing` are how
    /// `structuralUndo/Redo` re-enter this same FFI path to move an entry back
    /// while feeding the opposite stack.
    ///
    /// `announce` (#852): the per-item VoiceOver sentence. A BATCH move
    /// (`batchMove`) passes `false` so the individual "Moved <name> to <folder>."
    /// sentences don't chatter — the batch posts ONE summary ("Moved 3 items to
    /// Archive.") after every item's `moveEntry` (and its per-item link rewrite +
    /// structural-undo push) has completed. Single-surface callers leave it true.
    @discardableResult
    func moveEntry(
        path: String, isDirectory: Bool, to newParent: String,
        undoContext: StructuralUndoContext = .record,
        announce: Bool = true,
        onResult: ((Bool) -> Void)? = nil
    ) -> Task<Void, Never>? {
        guard !isMutatingStructure, let session = currentSession else { return nil }
        let token = beginStructuralMutation()
        let task = Task { [weak self] in
            let outcome: Result<StructuralReport, VaultError> = await Task.detached(
                priority: .userInitiated
            ) {
                do {
                    let report =
                        isDirectory
                        ? try session.moveFolder(path: path, newParent: newParent)
                        : try session.moveFile(path: path, newParent: newParent)
                    return .success(report)
                } catch let e as VaultError { return .failure(e) }
                catch { return .failure(.Io(message: error.localizedDescription)) }
            }.value
            guard let self, self.currentSession === session else { return }
            switch outcome {
            case .success(let report):
                let newPath = Self.joinVaultPath(newParent, (path as NSString).lastPathComponent)
                let oldParent = TreeMutation.parentPath(of: path) ?? ""
                self.applyRetargets(report, movedFrom: path, movedTo: newPath, isDirectory: isDirectory)
                self.pendingMove = nil
                self.publishTreeMutation(
                    .move(
                        oldPath: path, newPath: newPath,
                        oldParent: oldParent, newParent: newParent),
                    rewrittenCount: Self.distinctRewrittenCount(report))
                if announce {
                    if undoContext == .record {
                        self.postMutationAnnouncement(
                            self.mutationSentence(
                                "Moved \((path as NSString).lastPathComponent) to "
                                    + "\(newParent.isEmpty ? "vault root" : (newParent as NSString).lastPathComponent).",
                                report: report))
                    } else {
                        // #871: an undo/redo of a move announces "Undid/Redid move
                        // of <name>." instead of the fresh-op sentence.
                        self.postMutationAnnouncement(
                            self.structuralUndoRedoAnnouncement(
                                executed: .move(
                                    path: path, isDirectory: isDirectory, targetParent: newParent),
                                context: undoContext))
                    }
                }
                // #871: record the inverse (move `newPath` back to `oldParent`)
                // on the correct stack for the given context.
                self.recordStructuralUndo(
                    inverse: .move(
                        path: newPath, isDirectory: isDirectory, targetParent: oldParent),
                    context: undoContext)
                self.surfaceStructuralFailures(
                    report, verb: "move", name: (path as NSString).lastPathComponent)
                await self.loadFiles()
                onResult?(true)
            case .failure(let error):
                // A move has no inline field — report through the alert path.
                self.structuralFailureReport = StructuralFailureReport(
                    verb: "move", name: (path as NSString).lastPathComponent,
                    skipped: [])
                self.lastError = self.humanReadable(error)
                // #852 red-team: the per-item VoiceOver failure is suppressed
                // under a batch (`announce == false`) so the batch owns ONE
                // summary; the alert (`lastError`) still surfaces the reason.
                if announce {
                    self.announceMutationFailure(
                        verb: "move",
                        name: (path as NSString).lastPathComponent, error: error)
                }
                onResult?(false)
            }
            self.endStructuralMutation(token)
        }
        return task
    }

    // MARK: - Batch move / delete (#852)

    /// Prune a multi-selection down to its TOP-LEVEL entries: drop any item that
    /// lives inside another SELECTED folder. Trashing/moving both a folder AND a
    /// file within it would leave the second op acting on a path the first
    /// already relocated (an error or a wrong-target op) — Finder likewise only
    /// operates on the outermost items. Pure + static so the dedup is regression-
    /// locked (the `moveOutcome` pattern); order-preserving so the batch acts in
    /// the caller's visible-row order.
    static func topLevelSelection(_ items: [TreeSelection]) -> [TreeSelection] {
        items.filter { item in
            !items.contains { other in
                other.path != item.path && other.isDirectory
                    && pathIsWithin(item.path, path: other.path, isDirectory: true)
            }
        }
    }

    /// Pure (#852): the ONE summary sentence a batch move announces once every
    /// item's `moveEntry` (announce:false) has landed. `count` is how many items
    /// actually moved (no-ops skipped). Static so the phrasing is regression-
    /// locked (the `withLinksSuffix` pattern).
    static func batchMoveAnnouncement(count: Int, destination newParent: String) -> String {
        let where_ = newParent.isEmpty ? "vault root" : (newParent as NSString).lastPathComponent
        return "Moved \(count) \(count == 1 ? "item" : "items") to \(where_)."
    }

    /// Pure (#852): the ONE summary sentence a batch delete announces.
    static func batchDeleteAnnouncement(count: Int) -> String {
        "Moved \(count) \(count == 1 ? "item" : "items") to Trash."
    }

    /// Move every item of a multi-selection under `newParent` ("" = vault root),
    /// then announce ONCE (#852). Each item routes through the existing per-item
    /// `moveEntry` funnel — so per-item link rewrite AND the #871 per-item
    /// structural-undo inverse both happen exactly as a single move would (a
    /// K-item batch is K ⌘Z to fully undo; the structural stack is per-op and we
    /// deliberately don't coalesce). `moveEntry` serializes on
    /// `isMutatingStructure`, so the items are awaited SEQUENTIALLY — a second
    /// `moveEntry` fired before the first's tree refresh lands would be rejected.
    /// No-op items (already directly in `newParent`) and backend-illegal ones
    /// (a folder into its own subtree) are skipped so the count is truthful.
    @discardableResult
    func batchMove(_ items: [TreeSelection], to newParent: String) -> Task<Void, Never> {
        let targets = Self.topLevelSelection(items)
        let task = Task { @MainActor [weak self] in
            guard let self, let session = self.currentSession else { return }
            var moved = 0
            for item in targets {
                // #852 red-team: a mid-batch DIRECT vault switch (Open Recent /
                // Open Vault fires at a per-item await suspension) makes the
                // remaining old-vault paths meaningless against the new vault —
                // abort rather than move/announce against the wrong vault.
                guard self.currentSession === session else { break }
                let currentParent = TreeMutation.parentPath(of: item.path) ?? ""
                if currentParent == newParent { continue }  // no-op: already here
                if item.isDirectory,
                    Self.pathIsWithin(newParent, path: item.path, isDirectory: true) {
                    continue  // folder into its own subtree — backend rejects
                }
                var succeeded = false
                if let t = self.moveEntry(
                    path: item.path, isDirectory: item.isDirectory, to: newParent,
                    announce: false, onResult: { succeeded = $0 }) {
                    await t.value
                    // #852 red-team: count only ACTUAL successes — moveEntry
                    // returns a non-nil task even when the op then FAILS (e.g. a
                    // name collision at the destination), so incrementing on
                    // mere completion over-reported "Moved N items".
                    if succeeded { moved += 1 }
                }
            }
            // #852 (Codex finding 5): the post-loop writes must NOT touch a
            // vault that was switched in mid-batch. A direct switch A→B during a
            // per-item await suspension would otherwise clear B's freshly-opened
            // batch-move sheet (`pendingBatchMove = nil`) and announce A's
            // partial result under B. Recheck ownership before either write.
            guard self.currentSession === session else { return }
            self.pendingBatchMove = nil
            if moved > 0 {
                self.postMutationAnnouncement(
                    Self.batchMoveAnnouncement(count: moved, destination: newParent))
            }
        }
        pendingStructuralTaskForTesting = task
        return task
    }

    // MARK: - Structural undo/redo (#871)

    /// A reversible structural op — enough to invert a move or a rename via the
    /// SAME `moveEntry`/`renameEntry` FFI path the forward op used (constraint
    /// #871: reverse the op, don't invent a parallel primitive). `path` is the
    /// entry's CURRENT (post-forward-op) location; executing the case puts it
    /// back.
    enum StructuralUndoOp: Equatable {
        /// Reverse a move: move `path` under `targetParent` ("" = vault root).
        case move(path: String, isDirectory: Bool, targetParent: String)
        /// Reverse a rename: rename `path` to `newName` (a single component).
        case rename(path: String, isDirectory: Bool, newName: String)
    }

    /// Which structural-undo stack a completed move/rename feeds. Threaded
    /// through `moveEntry`/`renameEntry` so undo/redo re-use the exact forward
    /// path (constraint #871) without re-recording themselves onto the wrong
    /// stack. Equatable so the wrappers can branch the fresh-op announcement.
    enum StructuralUndoContext: Equatable {
        /// A fresh user op: push its inverse to the UNDO stack, clear REDO.
        case record
        /// This call IS an undo executing an inverse: retire the undone entry
        /// from the UNDO stack and stage its re-application on REDO.
        case undoing
        /// This call IS a redo: retire the redone entry from REDO and push its
        /// inverse back onto UNDO (REDO is NOT cleared).
        case redoing
    }

    /// Per-vault inverse-op stacks. Plain vars (not `@Published`): the Edit ▸
    /// Undo/Redo menu re-renders off the debounced `undoMenuTick` pulse
    /// (`noteUndoStacksChanged`), exactly like the canvas stacks that live on
    /// `CanvasDocument`. `private(set)` so tests can read depth/contents.
    ///
    /// Cross-vault safety (constraint #871.6): cleared on vault close AND on the
    /// direct-switch reset in `openVault` — an inverse resolved against the
    /// wrong vault would move/rename the wrong file.
    private(set) var structuralUndoStack: [StructuralUndoOp] = []
    private(set) var structuralRedoStack: [StructuralUndoOp] = []

    /// The menu action-name / announcement phrase for a structural op,
    /// describing it AS EXECUTED (direction-truthful): a move reads
    /// "move of <name>" (the item name is stable across the round-trip); a
    /// rename reads "rename to <target>" (the name the executed op produces).
    /// Reused by BOTH the "Undo/Redo …" menu title (the PENDING op, read from
    /// the stack top) and the "Undid/Redid …" announcement (the EXECUTED op) so
    /// the two phrasings can never drift. Static + pure → directly testable.
    static func structuralUndoActionName(_ op: StructuralUndoOp) -> String {
        switch op {
        case .move(let path, _, _):
            return "move of \((path as NSString).lastPathComponent)"
        case .rename(_, _, let newName):
            return "rename to \(newName)"
        }
    }

    /// The VoiceOver sentence for a completed undo/redo of a structural op
    /// (#871), e.g. "Undid move of Notes.md." / "Redid rename to Draft.md." —
    /// composed from the EXECUTED op so the announced name matches what landed
    /// on disk. `.record` never reaches here (the wrappers post the fresh-op
    /// sentence for it).
    private func structuralUndoRedoAnnouncement(
        executed: StructuralUndoOp, context: StructuralUndoContext
    ) -> String {
        let verb = (context == .undoing) ? "Undid" : "Redid"
        return "\(verb) \(Self.structuralUndoActionName(executed))."
    }

    /// Route a completed move/rename's inverse onto the correct stack for its
    /// `context` and pulse the undo menu. On the main actor and (for
    /// undo/redo) called from the SAME success handler that just executed the
    /// top-of-stack inverse under the `isMutatingStructure` guard, so the LIFO
    /// `removeLast()` retires exactly the entry we reversed — nothing can
    /// interleave between the peek in `structuralUndo/Redo` and this call.
    private func recordStructuralUndo(
        inverse: StructuralUndoOp, context: StructuralUndoContext
    ) {
        switch context {
        case .record:
            structuralUndoStack.append(inverse)
            structuralRedoStack.removeAll()
        case .undoing:
            if !structuralUndoStack.isEmpty { structuralUndoStack.removeLast() }
            structuralRedoStack.append(inverse)
        case .redoing:
            if !structuralRedoStack.isEmpty { structuralRedoStack.removeLast() }
            structuralUndoStack.append(inverse)
        }
        // #867/#871 debounced menu-title/enablement re-render pulse.
        noteUndoStacksChanged()
    }

    /// Whether a stack-top inverse is still SAFE to replay against the current
    /// filesystem (#871 Codex round 2). The `publishTreeMutation` barrier
    /// clears the stacks on every in-app non-move/rename mutation, but a
    /// file-creation FUNNEL that bypasses it (ghost note, new canvas, Restore
    /// As, template) — or an EXTERNAL change — can free the inverse's SOURCE or
    /// occupy its DESTINATION. Replaying then fails, or (worst case) touches a
    /// replacement file. This execution-time guard is the funnel-independent
    /// safety net: the source must still exist and the destination slot must be
    /// free, or the whole (now-suspect) history is dropped rather than replayed.
    private func structuralInverseIsExecutable(_ inverse: StructuralUndoOp) -> Bool {
        guard let vault = currentVaultURL else { return false }
        // lstat semantics (#871 Codex round 3): `attributesOfItem` does NOT
        // follow symlinks, so a DANGLING symlink at the destination — which
        // `fileExists` reports as absent — is correctly seen as an occupied
        // entry the replay must not clobber. Matches the backend no-clobber
        // check in `FilesystemProvider.rename`.
        func exists(_ rel: String) -> Bool {
            (try? FileManager.default.attributesOfItem(
                atPath: vault.appendingPathComponent(rel).path)) != nil
        }
        switch inverse {
        case .move(let path, _, let targetParent):
            let dest = Self.joinVaultPath(targetParent, (path as NSString).lastPathComponent)
            return exists(path) && !exists(dest)
        case .rename(let path, _, let newName):
            return exists(path) && !exists(Self.siblingPath(of: path, newName: newName))
        }
    }

    /// ⌘Z routed to the structural domain (`undoTargetsStructural`): reverse
    /// the most recent move/rename via the SAME forward FFI wrapper, then stage
    /// its re-application on the redo stack. An empty stack ANNOUNCES rather
    /// than no-oping silently — the canvas-parity VoiceOver affordance
    /// (`undoMenuItemEnabled` keeps the item enabled to preserve it).
    func structuralUndo() {
        guard let inverse = structuralUndoStack.last else {
            postMutationAnnouncement("Nothing to undo.")
            return
        }
        guard structuralInverseIsExecutable(inverse) else {
            // The files changed under the inverse (a bypassing create funnel or
            // an external edit); drop the suspect history rather than replay it.
            clearStructuralUndoStacks()
            postMutationAnnouncement("Can't undo — the files have changed.")
            return
        }
        pendingStructuralTaskForTesting = executeStructuralInverse(inverse, context: .undoing)
    }

    /// ⇧⌘Z twin of `structuralUndo`.
    func structuralRedo() {
        guard let inverse = structuralRedoStack.last else {
            postMutationAnnouncement("Nothing to redo.")
            return
        }
        guard structuralInverseIsExecutable(inverse) else {
            clearStructuralUndoStacks()
            postMutationAnnouncement("Can't redo — the files have changed.")
            return
        }
        pendingStructuralTaskForTesting = executeStructuralInverse(inverse, context: .redoing)
    }

    /// Dispatch a stack-top inverse back through the forward wrappers with the
    /// given context (so the success handler feeds the OPPOSITE stack). Returns
    /// the wrapper's task so `structuralUndo/Redo` can park it in
    /// `pendingStructuralTaskForTesting` for deterministic test awaits (the
    /// same handle role the command funnels use).
    ///
    /// Constraint #871.5: the wrappers early-return under `isMutatingStructure`
    /// — a second ⌘Z while an op is still in flight is simply dropped (the
    /// entry stays put because the success handler that moves it never runs),
    /// never a deadlock and never a lost entry. Cross-vault (constraint
    /// #871.6): the wrappers' own `currentSession === session` guard after the
    /// FFI await bails without touching the stacks if the vault switched, and
    /// the stacks were already cleared by that switch.
    @discardableResult
    private func executeStructuralInverse(
        _ inverse: StructuralUndoOp, context: StructuralUndoContext
    ) -> Task<Void, Never>? {
        switch inverse {
        case .move(let path, let isDirectory, let targetParent):
            return moveEntry(
                path: path, isDirectory: isDirectory, to: targetParent, undoContext: context)
        case .rename(let path, let isDirectory, let newName):
            return renameEntry(
                path: path, isDirectory: isDirectory, to: newName, undoContext: context)
        }
    }

    /// Drop the per-vault structural undo/redo stacks (constraint #871.6),
    /// pulsing the menu only when something actually changed. Called from
    /// `closeVault` and the `openVault` direct-switch reset block.
    func clearStructuralUndoStacks() {
        guard !structuralUndoStack.isEmpty || !structuralRedoStack.isEmpty else { return }
        structuralUndoStack = []
        structuralRedoStack = []
        noteUndoStacksChanged()
    }

    // MARK: - Import (#870)

    /// Import an EXTERNAL file (dropped from Finder / another app) into
    /// `destinationFolder` ("" = vault root) as a copy. Reuses the SAME
    /// no-clobber collision surface as `moveEntry` (constraint #870): the
    /// `create_exclusive` primitive throws `DestinationExists` on a name
    /// collision, which surfaces through the identical `lastError` +
    /// `announceMutationFailure` path a colliding move takes.
    ///
    /// Text-only: the vault has no bytes-import FFI, so a non-UTF-8 (binary)
    /// file surfaces a clear failure rather than being copied corrupt — see the
    /// PR's scope note. An import is a CREATE (like `createNote`/`duplicateEntry`
    /// — not undoable; delete is the Trash-recoverable inverse), so it records
    /// no structural-undo entry.
    @discardableResult
    func importEntry(externalURL: URL, into destinationFolder: String) -> Task<Void, Never>? {
        guard !isMutatingStructure, let session = currentSession else { return nil }
        let token = beginStructuralMutation()
        let name = externalURL.lastPathComponent
        let destPath = Self.joinVaultPath(destinationFolder, name)
        let task = Task { [weak self] in
            let outcome: Result<Void, VaultError> = await Task.detached(
                priority: .userInitiated
            ) {
                // A dropped file URL may be security-scoped (sandboxed builds);
                // the accessor is a harmless no-op otherwise (returns false, we
                // still read). Balanced with a `defer`.
                let scoped = externalURL.startAccessingSecurityScopedResource()
                defer { if scoped { externalURL.stopAccessingSecurityScopedResource() } }
                do {
                    let content = try String(contentsOf: externalURL, encoding: .utf8)
                    _ = try session.createExclusive(path: destPath, content: content)
                    return .success(())
                } catch let e as VaultError { return .failure(e) }
                catch { return .failure(.Io(message: error.localizedDescription)) }
            }.value
            guard let self, self.currentSession === session else { return }
            switch outcome {
            case .success:
                // A fresh file at the destination — the same `.createNote`
                // mutation `duplicateEntry` publishes (a copy IS a created note).
                self.publishTreeMutation(.createNote(path: destPath), rewrittenCount: 0)
                self.postMutationAnnouncement("Imported \(name).")
                await self.loadFiles()
            case .failure(let error):
                self.lastError = self.humanReadable(error)
                self.announceMutationFailure(verb: "import", name: name, error: error)
            }
            self.endStructuralMutation(token)
        }
        return task
    }

    /// The action a file-URL drop (#870) resolves to. Pure decision so the
    /// import-vs-move branch is testable without a live drag.
    enum StructuralDropAction: Equatable {
        /// The dropped URL is INSIDE the current vault → move (existing
        /// behavior), routed through `moveEntry` (undoable).
        case move(path: String, isDirectory: Bool, to: String)
        /// The dropped URL is EXTERNAL → import a copy via `importEntry`.
        case importFile(url: URL, into: String)
        /// No-op (already in the destination, or a folder onto its own subtree).
        case none
    }

    /// Pure (#870): classify a file-URL drop. A URL under the current vault is
    /// a MOVE (identical to an intra-tree drag — and thus undoable); an
    /// external URL is an IMPORT. Rejects the no-ops the private-type move path
    /// also rejects early (already in the destination; a folder onto its own
    /// subtree) so the two drop routes behave the same.
    /// Whether `url` points at a directory (#870, Codoki: extracted as a
    /// testable seam with UNAMBIGUOUS optional handling). `try?` wraps ONLY the
    /// throwing `resourceValues` call — yielding `URLResourceValues?` — then
    /// `?.isDirectory` reads the (itself-optional) key and `?? false` lands a
    /// plain `Bool`. The prior `(try? resourceValues(...).isDirectory) ?? false`
    /// nested the property access inside `try?`, relying on Swift's flattening
    /// and reading as a `Bool??` a reviewer (and a linter) can misjudge. An
    /// unreadable value falls back to false (treat as a file) — the safe
    /// default for the import path.
    static func urlIsDirectory(_ url: URL) -> Bool {
        (try? url.resourceValues(forKeys: [.isDirectoryKey]))?.isDirectory ?? false
    }

    static func fileURLDropAction(
        url: URL, vaultURL: URL?, destinationFolder: String, isDirectory: Bool
    ) -> StructuralDropAction {
        if let rel = vaultRelativePath(of: url, vaultURL: vaultURL) {
            let currentParent = TreeMutation.parentPath(of: rel) ?? ""
            if currentParent == destinationFolder { return .none }
            if isDirectory, pathIsWithin(destinationFolder, path: rel, isDirectory: true) {
                return .none
            }
            return .move(path: rel, isDirectory: isDirectory, to: destinationFolder)
        }
        // #870 Codex round 3: dragging the CURRENT VAULT ROOT onto its own tree
        // is a no-op, NOT an external import. `vaultRelativePath` returns nil
        // for BOTH the root and a truly-external URL, so distinguish them here
        // before the import branch — otherwise Slate would try to text-import
        // the vault directory and surface a spurious failure.
        if isVaultRoot(url, vaultURL: vaultURL) { return .none }
        return .importFile(url: url, into: destinationFolder)
    }

    /// Whether `url` IS the current vault root (#870 Codex round 3). Fully
    /// resolves symlinks on both sides (the root is a container, so — unlike a
    /// dropped item — its own final component is safe to resolve) and honors
    /// the volume's case sensitivity, so a vault opened through a symlinked
    /// spelling still matches.
    private static func isVaultRoot(_ url: URL, vaultURL: URL?) -> Bool {
        guard let vaultURL else { return false }
        let vaultPath = vaultURL.resolvingSymlinksInPath().standardizedFileURL.path
        let droppedPath = url.resolvingSymlinksInPath().standardizedFileURL.path
        return pathComponentsEqual(
            droppedPath, vaultPath, caseInsensitive: volumeIsCaseInsensitive(vaultURL))
    }

    /// The vault-relative path of `url` if it lives inside `vaultURL`, else
    /// nil (external). The vault root itself maps to nil (it isn't a movable
    /// entry).
    ///
    /// #870 Codex round 1: containment is FILESYSTEM-AWARE, not a raw lexical
    /// compare. Both paths are resolved through symlinks and standardized
    /// (so a symlinked vault root, or a dropped URL that traverses a symlink
    /// or `..`, still matches), and the prefix match honors the volume's
    /// case sensitivity (defaulting to case-INSENSITIVE, the macOS APFS
    /// default). Without this, the same in-vault file reached via a symlink
    /// or a case-differing path was misclassified as external and IMPORTED
    /// as a duplicate instead of performing the required undoable move. The
    /// returned relative path keeps the resolved file's real component casing
    /// (what the backend expects).
    static func vaultRelativePath(of url: URL, vaultURL: URL?) -> String? {
        guard let vaultURL else { return nil }
        let vaultPath = vaultURL.resolvingSymlinksInPath().standardizedFileURL.path
        // #870 Codex round 2: resolve symlinks in the CONTAINER only, then
        // re-attach the dropped item's own final component UNRESOLVED. If the
        // whole URL were resolved, an EXTERNAL symlink file (e.g.
        // ~/Downloads/link.md → /vault/a.md) would dereference to its in-vault
        // target and be classified as a move of the real note — breaking the
        // external link — instead of importing the dropped entry. Resolving the
        // ancestor still canonicalizes a symlinked vault root / parent dir.
        let filePath =
            url.deletingLastPathComponent().resolvingSymlinksInPath()
            .appendingPathComponent(url.lastPathComponent).standardizedFileURL.path
        let caseInsensitive = volumeIsCaseInsensitive(vaultURL)
        if pathComponentsEqual(filePath, vaultPath, caseInsensitive: caseInsensitive) {
            return nil  // the vault root itself
        }
        let prefix = vaultPath.hasSuffix("/") ? vaultPath : vaultPath + "/"
        guard filePath.count > prefix.count else { return nil }
        let head = String(filePath.prefix(prefix.count))
        guard pathComponentsEqual(head, prefix, caseInsensitive: caseInsensitive) else {
            return nil
        }
        return String(filePath.dropFirst(prefix.count))
    }

    /// Whether `url`'s volume treats names case-INSENSITIVELY (the macOS APFS
    /// default). Defaults to `true` when the volume flag can't be read (#870).
    private static func volumeIsCaseInsensitive(_ url: URL) -> Bool {
        let values = try? url.resourceValues(forKeys: [.volumeSupportsCaseSensitiveNamesKey])
        if let sensitive = values?.volumeSupportsCaseSensitiveNames { return !sensitive }
        return true
    }

    private static func pathComponentsEqual(
        _ a: String, _ b: String, caseInsensitive: Bool
    ) -> Bool {
        caseInsensitive ? (a.compare(b, options: .caseInsensitive) == .orderedSame) : (a == b)
    }

    // MARK: Delete

    /// A non-empty folder delete awaiting user confirmation (#860). Set by
    /// `requestDeleteEntry` when the target is a folder with children; the
    /// MainSplitView alert consumes it (Move to Trash confirms, Cancel
    /// drops). Files and empty folders never stage — they keep the
    /// no-confirm Finder-parity path (a file trash is recoverable; a whole
    /// subtree moving on one chord is the heavier loss-of-context event).
    struct PendingFolderDelete: Equatable, Identifiable {
        let path: String
        /// Immediate (non-recursive) child count — the "its N items" the
        /// alert message speaks. Advisory: the delete itself takes the whole
        /// subtree regardless.
        let itemCount: Int
        var id: String { path }
        var name: String { (path as NSString).lastPathComponent }
    }

    @Published var pendingFolderDelete: PendingFolderDelete?

    /// The single delete entry point every surface routes through (#860):
    /// tree ⌘⌫ / rotor / context menu, and the menu/palette command. A
    /// folder with children stages `pendingFolderDelete` (confirmation
    /// alert) instead of deleting; everything else falls straight through to
    /// `deleteEntry`. `knownChildCount` lets the tree pass its node's cached
    /// immediate count; callers without one (the selection-scoped command)
    /// leave it nil and a shallow FileManager enumerate fills in.
    func requestDeleteEntry(path: String, isDirectory: Bool, knownChildCount: Int? = nil) {
        guard isVaultOpen else { return }
        if isDirectory {
            // A cached count of ZERO is treated as unknown: the tree's
            // itemCount can be stale (folder filled externally since the
            // fetch), and a stale zero would BYPASS the confirmation —
            // trashing a non-empty folder unprompted. Only the zero case
            // re-probes; a stale positive merely over-confirms (harmless).
            let count = knownChildCount.flatMap { $0 > 0 ? $0 : nil }
                ?? shallowChildCount(ofFolder: path)
            if count > 0 {
                pendingFolderDelete = PendingFolderDelete(path: path, itemCount: count)
                return
            }
        }
        pendingStructuralTaskForTesting = deleteEntry(path: path, isDirectory: isDirectory)
    }

    /// Alert "Move to Trash" — run the staged folder delete through the
    /// normal funnel (announcement + tree focus move ride along unchanged).
    func confirmPendingFolderDelete() {
        guard let pending = pendingFolderDelete else { return }
        pendingFolderDelete = nil
        pendingStructuralTaskForTesting = deleteEntry(path: pending.path, isDirectory: true)
    }

    /// Alert "Cancel" — nothing is deleted.
    func cancelPendingFolderDelete() {
        pendingFolderDelete = nil
    }

    // MARK: - Batch delete (#852)

    /// A multi-selection awaiting delete confirmation (#852). Staged by
    /// `requestBatchDelete` only when the batch contains at least one non-empty
    /// folder (the #860 heavier-loss event); an all-files / empty-folder batch
    /// trashes straight through, Finder-parity. The MainSplitView alert consumes
    /// it.
    struct BatchDelete: Equatable, Identifiable {
        /// The deduplicated top-level items to trash.
        let items: [TreeSelection]
        /// How many non-empty folders the batch includes — drives the alert
        /// message ("including N folders with contents").
        let nonEmptyFolderCount: Int
        var id: String { items.map(\.path).joined(separator: "\n") }
        var itemCount: Int { items.count }
    }

    @Published var pendingBatchDelete: BatchDelete?

    /// The batch delete entry point (#852): trashes a whole multi-selection with
    /// ONE summary announcement. Mirrors `requestDeleteEntry`'s #860 gate at the
    /// batch level — if ANY selected item is a non-empty folder, stage
    /// `pendingBatchDelete` (confirmation) rather than trashing unprompted; an
    /// all-files / empty-folder batch falls straight through to `batchDelete`.
    /// The selection is deduplicated to its top-level items first so a folder +
    /// something inside it don't double-delete.
    func requestBatchDelete(_ items: [TreeSelection]) {
        guard isVaultOpen else { return }
        let targets = Self.topLevelSelection(items)
        guard !targets.isEmpty else { return }
        // A single item routes through the single funnel so it gets the exact
        // #860 single-folder confirmation copy (and its own announcement).
        if targets.count == 1, let only = targets.first {
            requestDeleteEntry(
                path: only.path, isDirectory: only.isDirectory)
            return
        }
        let nonEmptyFolders = targets.filter {
            $0.isDirectory && shallowChildCount(ofFolder: $0.path) > 0
        }
        if !nonEmptyFolders.isEmpty {
            pendingBatchDelete = BatchDelete(
                items: targets, nonEmptyFolderCount: nonEmptyFolders.count)
            return
        }
        pendingStructuralTaskForTesting = batchDelete(targets)
    }

    /// Alert "Move to Trash" — run the staged batch delete through the funnel.
    func confirmPendingBatchDelete() {
        guard let pending = pendingBatchDelete else { return }
        pendingBatchDelete = nil
        pendingStructuralTaskForTesting = batchDelete(pending.items)
    }

    /// Alert "Cancel" — nothing is deleted.
    func cancelPendingBatchDelete() {
        pendingBatchDelete = nil
    }

    /// Trash every item, then announce ONCE (#852). Each routes through the
    /// per-item `deleteEntry` funnel (announce:false) — same tab-error-flip and
    /// tree refresh a single delete does — awaited sequentially because
    /// `deleteEntry` serializes on `isMutatingStructure`. `items` is expected
    /// already deduplicated (`topLevelSelection`), so no item is nested in
    /// another and no delete acts on an already-trashed path.
    @discardableResult
    func batchDelete(_ items: [TreeSelection]) -> Task<Void, Never> {
        let targets = Self.topLevelSelection(items)
        let task = Task { @MainActor [weak self] in
            guard let self, let session = self.currentSession else { return }
            var deleted = 0
            for item in targets {
                // #852 red-team: abort the rest if the vault switched mid-batch
                // (the remaining old-vault paths are meaningless against a new
                // vault).
                guard self.currentSession === session else { break }
                var succeeded = false
                if let t = self.deleteEntry(
                    path: item.path, isDirectory: item.isDirectory, announce: false,
                    onResult: { succeeded = $0 }) {
                    await t.value
                    // #852 red-team: count only ACTUAL successes (deleteEntry
                    // returns a non-nil task even when it fails), so the summary
                    // doesn't claim a failed trash as trashed.
                    if succeeded { deleted += 1 }
                }
            }
            // #852 (Codex finding 5): don't announce the OLD vault's partial
            // result under a vault switched in mid-batch — recheck ownership.
            guard self.currentSession === session else { return }
            if deleted > 0 {
                self.postMutationAnnouncement(Self.batchDeleteAnnouncement(count: deleted))
            }
        }
        pendingStructuralTaskForTesting = task
        return task
    }

    /// Immediate child count of a vault folder via a shallow FileManager
    /// enumerate (hidden entries COUNT — a folder holding only `.env`
    /// must confirm; only Finder-noise `.DS_Store` is ignored — and
    /// Hidden entries count (only `.DS_Store` is ignored); an unreadable
    /// directory reads as NON-empty (fail-closed → the confirmation), so
    /// unknown never bypasses the prompt.
    private func shallowChildCount(ofFolder path: String) -> Int {
        guard let url = currentVaultURL?.appendingPathComponent(path) else { return 1 }
        // Hidden entries COUNT (Codex P1: a folder holding only `.env` or
        // `.git` must still confirm — those are exactly the deletions that
        // hurt); only the Finder-noise `.DS_Store` is ignored. Enumeration
        // failure fails CLOSED (treated as non-empty → confirmation), not
        // open — the alert is the safe side of "unknown".
        guard let contents = try? FileManager.default.contentsOfDirectory(
            at: url, includingPropertiesForKeys: nil)
        else { return 1 }
        return contents.filter { $0.lastPathComponent != ".DS_Store" }.count
    }

    /// Send the file or folder at `path` to the system trash. Any open tab
    /// holding the file (or a descendant of the folder) flips to the missing-
    /// file error state (spec §U2-5). Refreshes the parent level + moves the
    /// selection to the next sibling / prev / parent (U2-6), and announces.
    ///
    /// `announce` (#852): mirrors `moveEntry` — a BATCH delete (`batchDelete`)
    /// passes `false` so the per-item "Moved <name> to Trash." doesn't chatter;
    /// the batch posts ONE summary after every item has been trashed.
    @discardableResult
    func deleteEntry(
        path: String, isDirectory: Bool, announce: Bool = true,
        onResult: ((Bool) -> Void)? = nil
    ) -> Task<Void, Never>? {
        guard !isMutatingStructure, let session = currentSession else { return nil }
        let token = beginStructuralMutation()
        let task = Task { [weak self] in
            let outcome: Result<Void, VaultError> = await Task.detached(
                priority: .userInitiated
            ) {
                do {
                    if isDirectory { try session.deleteFolder(path: path) }
                    else { try session.deleteFile(path: path) }
                    return .success(())
                } catch let e as VaultError { return .failure(e) }
                catch { return .failure(.Io(message: error.localizedDescription)) }
            }.value
            guard let self, self.currentSession === session else { return }
            switch outcome {
            case .success:
                // Codex round 8: active-tab ownership is decided AT
                // COMPLETION, against the LIVE selection — the old
                // call-time `loadedFilePath` capture was stale in both
                // directions. Select-then-delete before the load
                // lands: captured false, the cleanup was skipped and
                // the doomed read published the deleted note.
                // Delete-then-switch-away: captured true, the
                // completion clobbered the NEW note's state with the
                // deleted-note error. Round 9: the compare is the
                // ACTIVE MARKDOWN TAB's own path — `selectedFilePath`
                // is a transient mirror during dirty navigation (it
                // briefly holds the requested destination while the
                // tab stays put and the rollback is async), so the
                // workspace tab identity is the durable truth.
                // Markdown-gated keeps the old semantics for
                // base/canvas actives (their documents are handled by
                // the invalidate loop below).
                let activeWasDeleted: Bool
                if case .markdown(let activePath) = self.workspace.activeTab?.item {
                    activeWasDeleted = Self.pathIsWithin(
                        activePath, path: path, isDirectory: isDirectory)
                } else {
                    activeWasDeleted = false
                }
                // Flip open tabs to the error state. The ACTIVE tab's document
                // lives in AppState's fields; parked tabs drop so re-activation
                // re-reads from disk and fails into `noteLoadError`.
                self.dropOpenTabsForDeletedPath(path, isDirectory: isDirectory, activeWasDeleted: activeWasDeleted)
                let parent = TreeMutation.parentPath(of: path) ?? ""
                self.publishTreeMutation(
                    .delete(path: path, parent: parent, wasDirectory: isDirectory),
                    rewrittenCount: 0)
                if announce {
                    self.postMutationAnnouncement(
                        "Moved \((path as NSString).lastPathComponent) to Trash.")
                }
                await self.loadFiles()
                onResult?(true)
            case .failure(let error):
                self.lastError = self.humanReadable(error)
                // #852 red-team: batch (`announce == false`) suppresses the
                // per-item VoiceOver failure; the batch announces one summary
                // and the alert (`lastError`) still surfaces the reason.
                if announce {
                    self.announceMutationFailure(
                        verb: "delete",
                        name: (path as NSString).lastPathComponent, error: error)
                }
                onResult?(false)
            }
            self.endStructuralMutation(token)
        }
        return task
    }

    /// Announce a failed mutation (spec §U2-6 failure form). "Could not <verb>
    /// <name>: <specific reason>." Routes through the same medium-priority seam
    /// as the success announcements.
    private func announceMutationFailure(verb: String, name: String, error: VaultError) {
        postMutationAnnouncement("Could not \(verb) \(name): \(humanReadable(error))")
    }

    /// Create a folder (auto-suffixed to avoid a collision) inside `parent`,
    /// then move `movePath` into it — the "New Folder…" row of the Move sheet.
    /// Chains the two mutations so the picker flow is atomic from the user's
    /// view. On a create failure the move is skipped and the error surfaces.
    @discardableResult
    func createFolderThenMove(
        newFolderName: String, in parent: String, movePath: String, isDirectory: Bool
    ) -> Task<Void, Never>? {
        guard let session = currentSession else { return nil }
        // Suffix the name against the known set so the create doesn't collide.
        let suffixed = uniqueName(
            base: newFolderName, ext: nil, siblingsIn: parent)
        let newFolderPath = Self.joinVaultPath(parent, suffixed)
        return Task { [weak self] in
            guard let self else { return }
            // #852 (Codex finding 1): guard the CREATE itself against a vault
            // switch — if B was opened before this task ran, `createFolder`
            // would capture B's session and create/announce the folder in the
            // WRONG vault. Abort before creating anything.
            guard self.currentSession === session else { return }
            // #852 (Codex finding 2): gate the move SOLELY on the create's actual
            // success result (true only on .success) + same session — never
            // folder existence (an empty pre-existing "New Folder" would false-
            // pass an existence check while the create actually FAILED).
            var created = false
            await self.createFolder(name: suffixed, in: parent, onResult: { created = $0 })?.value
            guard created, self.currentSession === session else { return }
            await self.moveEntry(path: movePath, isDirectory: isDirectory, to: newFolderPath)?.value
        }
    }

    /// #852: the batch analog of `createFolderThenMove` — create a fresh folder,
    /// then `batchMove` the whole selection into it (one summary announcement).
    /// The Move sheet's "New Folder…" row for a multi-selection.
    @discardableResult
    func createFolderThenBatchMove(
        newFolderName: String, in parent: String, items: [TreeSelection]
    ) -> Task<Void, Never>? {
        guard let session = currentSession else { return nil }
        let suffixed = uniqueName(base: newFolderName, ext: nil, siblingsIn: parent)
        let newFolderPath = Self.joinVaultPath(parent, suffixed)
        let task = Task { [weak self] in
            guard let self else { return }
            // #852 (Codex finding 1): don't CREATE in a vault switched in before
            // this task ran.
            guard self.currentSession === session else { return }
            // #852 (Codex finding 2): gate the batch move on the create's actual
            // success result + same session, never folder existence.
            var created = false
            await self.createFolder(name: suffixed, in: parent, onResult: { created = $0 })?.value
            guard created, self.currentSession === session else { return }
            await self.batchMove(items, to: newFolderPath).value
        }
        pendingStructuralTaskForTesting = task
        return task
    }

    /// Generic non-colliding-name helper: `base`(+`.ext`), then `base N`(+ext).
    /// `siblingsIn` scopes the collision check to one parent level. Used by
    /// note/folder creation + the move-new-folder flow so the auto-naming rule
    /// is single-source.
    ///
    /// #852 (Codex finding 2): the sibling set is drawn from an AUTHORITATIVE
    /// on-disk directory listing (files AND folders, including EMPTY folders),
    /// not just `files` — which holds only openable DOCUMENTS. An existing empty
    /// "New Folder" is invisible to `files`, so the old heuristic would pick
    /// "New Folder" again and the create would FAIL with DestinationExists. The
    /// known-file set is unioned in as a fallback (a not-yet-flushed index / the
    /// no-vault path).
    private func uniqueName(base: String, ext: String?, siblingsIn parent: String) -> String {
        var siblings: Set<String> = []
        if let vault = currentVaultURL {
            let dir = parent.isEmpty ? vault : vault.appendingPathComponent(parent)
            if let entries = try? FileManager.default.contentsOfDirectory(
                at: dir, includingPropertiesForKeys: nil) {
                for entry in entries { siblings.insert(entry.lastPathComponent.lowercased()) }
            }
        }
        let prefix = parent.isEmpty ? "" : parent + "/"
        for file in files where file.path.hasPrefix(prefix) {
            let rest = String(file.path.dropFirst(prefix.count))
            if let slash = rest.firstIndex(of: "/") {
                siblings.insert(String(rest[rest.startIndex..<slash]).lowercased())
            } else {
                siblings.insert(rest.lowercased())
            }
        }
        func candidate(_ n: Int?) -> String {
            let stem = n.map { "\(base) \($0)" } ?? base
            return ext.map { "\(stem).\($0)" } ?? stem
        }
        if !siblings.contains(candidate(nil).lowercased()) { return candidate(nil) }
        var n = 2
        while siblings.contains(candidate(n).lowercased()) { n += 1 }
        return candidate(n)
    }

    // MARK: Retarget / error-flip helpers

    /// Retarget open tabs after a rename/move: the moved node itself, and — for
    /// a folder — every open descendant path (they moved with the folder). Uses
    /// the report's `moved` list (authoritative: it's exactly the files whose
    /// path changed) so multi-file folder moves retarget precisely.
    private func applyRetargets(
        _ report: StructuralReport, movedFrom: String, movedTo: String, isDirectory: Bool
    ) {
        if isDirectory {
            // Folder: retarget each moved file by its own old→new mapping.
            for m in report.moved {
                let changed = workspace.retarget(old: m.oldPath, new: m.newPath)
                rekeyBaseDocumentIfRetargeted(changed, oldPath: m.oldPath, newPath: m.newPath)
                rebindActiveIfRetargeted(changed, to: m.newPath)
            }
            // The report's `moved` only lists FILES; if the active buffer is a
            // file under the folder it's covered above. Nothing else to do —
            // folders don't have buffers.
        } else {
            let changed = workspace.retarget(old: movedFrom, new: movedTo)
            rekeyBaseDocumentIfRetargeted(changed, oldPath: movedFrom, newPath: movedTo)
            rebindActiveIfRetargeted(changed, to: movedTo)
        }
    }

    /// If the ACTIVE tab was among the retargeted set, rebind AppState's live
    /// fields to the new path (the active document lives here, not in
    /// `workspace.documents`, so `WorkspaceState.retarget` couldn't touch it).
    private func rebindActiveIfRetargeted(_ changed: [TabID], to newPath: String) {
        guard let activeID = workspace.model.activeGroup.activeTabID,
            changed.contains(activeID)
        else { return }
        // Codex round 3: the link-record OWNERSHIP marker follows the
        // rename with the live fields. `loadedFilePath` is updated
        // first, so the selection sink takes its same-file early
        // return and NO new link query restamps the marker — left
        // behind, every reading link classifies unresolved and
        // refuses activation until an unrelated reload.
        let ownershipLanded = currentOutgoingLinksPath == selectedFilePath
        if ownershipLanded {
            currentOutgoingLinksPath = newPath
        }
        if case .markdown = workspace.activeTab?.item {
            loadedFilePath = newPath
        } else {
            loadedFilePath = nil
        }
        let selectionMatches: Bool
        if case .base = workspace.activeTab?.item {
            selectionMatches = BaseExactIdentity.matches(selectedFilePath, newPath)
        } else {
            selectionMatches = selectedFilePath == newPath
        }
        if !selectionMatches {
            selectedFilePath = newPath
        }
        // Codex rounds 4+5: the fan-out legs race the rename
        // INDEPENDENTLY — the links marker landing says nothing about
        // math/tasks/code/diagrams/citations, and any leg that lands
        // late is dropped by its guard with its isLoading flag left
        // for a "newer task" that otherwise never comes. Re-fire the
        // whole fan-out for the new path unconditionally:
        // fireCollectionLoads cancels the previous legs, every loader
        // carries its own race guard, and a rename is rare enough
        // that the extra query burst is irrelevant. (The marker
        // retarget above still matters: it keeps reading
        // classification correct in the window before the refired
        // links leg lands.)
        if case .markdown = workspace.activeTab?.item {
            fireCollectionLoads(path: newPath)
        }
    }

    /// After a successful delete, flip open tabs pointing at the removed path
    /// to the missing-file error state. The active tab: clear its buffer + set
    /// `noteLoadError` (NoteContentView then renders its error pane). Parked
    /// tabs: drop their document so re-activation re-reads and fails into the
    /// same error state.
    private func dropOpenTabsForDeletedPath(
        _ path: String, isDirectory: Bool, activeWasDeleted: Bool
    ) {
        // Parked tabs whose path is within the deleted subtree.
        for tab in workspace.model.allTabs {
            let tabPath = tab.item.path
            guard Self.pathIsWithin(tabPath, path: path, isDirectory: isDirectory) else {
                continue
            }
            switch tab.item {
            case .markdown:
                _ = workspace.invalidateParkedDocuments(forPath: tabPath)
            case .canvas:
                invalidateCanvasDocument(path: tabPath)
            case .base:
                invalidateBaseDocument(path: tabPath)
            case .savedQuery:
                continue
            case .dashboard:
                continue
            case .graph:
                // Synthetic path; never within a deleted real subtree.
                continue
            }
        }
        if activeWasDeleted {
            // `loadedFilePath` may be nil when the doomed load never
            // landed (round 8: select-then-delete) — fall back to the
            // selection the ownership check was made against.
            let deletedName = (loadedFilePath ?? selectedFilePath)
                .map { ($0 as NSString).lastPathComponent }
            cancelNoteScopedWork()
            // Codex round 7: note/links/embeds clear their spinners
            // explicitly at landing (audit #201 — a deferred clear
            // raced the next task's set), relying on "a newer task
            // will clear" after a dropped landing. Deletion is the one
            // path with NO newer task — clear the trio here. (The
            // other legs self-clear via defer, audit #257 M2.)
            isLoadingNote = false
            isLoadingLinks = false
            isLoadingEmbeds = false
            currentNoteText = nil
            savedBaselineText = nil
            currentNoteContentHash = nil
            hasUnsavedChanges = false
            currentNoteHeadings = []
            // #868 red-team: this delete arm clears a BESPOKE field
            // list (not `clearActiveNoteFields`), so it must also reset
            // the properties-source mirror + inline error the funnel
            // resets there. NoteContentView flips to the error pane and
            // UNMOUNTS NotePropertiesHeader, so the widget's
            // `.onChange(of: isSourceMode)` never fires — leaving
            // `propertiesSourceShowing` stuck true would latch the
            // View ▸ "Hide Properties Source" title wrong-direction
            // over a note that no longer exists (the exact "worse than
            // static" failure #868 calls out), and ⇧⌘D would no-op
            // against the `loadedFilePath != nil` guard.
            propertiesSourceShowing = false
            propertiesSourceError = nil
            loadedFilePath = nil
            noteLoadError =
                "\(deletedName ?? "This note") was moved to Trash and is no longer available."
        }
    }

    // MARK: Path helpers

    /// Join a parent path ("" = root) and a final component into a vault path.
    /// `nonisolated`: pure string work, used from the detached duplicate task.
    nonisolated static func joinVaultPath(_ parent: String, _ name: String) -> String {
        parent.isEmpty ? name : "\(parent)/\(name)"
    }

    /// The sibling path of `path` with its final component replaced by `newName`.
    static func siblingPath(of path: String, newName: String) -> String {
        let parent = TreeMutation.parentPath(of: path) ?? ""
        return joinVaultPath(parent, newName)
    }

    /// Whether `candidate` is `path` itself, or (when `path` is a folder) a
    /// descendant of it. Used to decide which open tabs a delete/rename affects.
    static func pathIsWithin(_ candidate: String, path: String, isDirectory: Bool) -> Bool {
        if candidate == path { return true }
        guard isDirectory else { return false }
        return candidate.hasPrefix(path + "/")
    }

    /// Build a non-colliding "Untitled.md" / "Untitled N.md" for a fresh note in
    /// `parent`, checking the known file set (case-insensitively). The backend
    /// re-checks; this just avoids the common repeat-⌘N collision.
    private func uniqueUntitledName(in parent: String) -> String {
        let existing = Set(
            files.map { ($0.path as NSString).lastPathComponent.lowercased() })
        if !existing.contains("untitled.md") { return "Untitled.md" }
        var n = 2
        while existing.contains("untitled \(n).md") { n += 1 }
        return "Untitled \(n).md"
    }

    /// Append U2-6's ", updated links in N notes." suffix to a mutation
    /// sentence when the report rewrote links; otherwise return the base.
    /// (U2-6 fills the announcement *routing*; the suffix logic is shared here
    /// so rename/move build one sentence.)
    private func mutationSentence(_ base: String, report: StructuralReport) -> String {
        Self.withLinksSuffix(base, rewrittenCount: Self.distinctRewrittenCount(report))
    }

    /// Pure: append ", updated links in N notes." to `base` when `rewrittenCount
    /// > 0` (spec §U2-6 verbatim suffix), else return `base` unchanged. Static so
    /// the exact phrasing is regression-locked without a live rewrite — which the
    /// current base branch never produces (U2-3 part 2, the rewriter's session
    /// integration, isn't wired here; `StructuralReport.rewritten` is always
    /// empty until it lands, so this suffix is dormant end-to-end).
    static func withLinksSuffix(_ base: String, rewrittenCount: Int) -> String {
        guard rewrittenCount > 0 else { return base }
        // "Renamed a.md to b.md" (drop trailing period) + suffix.
        let trimmed = base.hasSuffix(".") ? String(base.dropLast()) : base
        return "\(trimmed), updated links in \(rewrittenCount) "
            + "\(rewrittenCount == 1 ? "note" : "notes")."
    }

    // MARK: Command entry points (palette + menu → tree selection)

    /// ⌘N — create a new note in the selection's folder (or root) and open it
    /// for renaming. The command-surface funnel for `createNote(in:)`.
    func newNoteCommand() {
        guard isVaultOpen else { return }
        pendingStructuralTaskForTesting = createNote(in: creationParentPath)
    }

    /// Context-menu / palette "New Folder…". Opens an inline-named folder in the
    /// selection's parent. Uses a placeholder name the user then renames — the
    /// tree has no create-time field, so we create "Untitled Folder" and drop
    /// straight into rename, mirroring the new-note flow.
    func newFolderCommand() {
        guard isVaultOpen else { return }
        newFolderInContext(parent: creationParentPath)
    }

    /// Create a new folder in an EXPLICIT parent (the context-menu path, which
    /// targets the right-clicked folder rather than the current selection), then
    /// drop into inline rename of the new folder.
    func newFolderInContext(parent: String) {
        guard isVaultOpen else { return }
        let name = uniqueUntitledFolderName(in: parent)
        let path = Self.joinVaultPath(parent, name)
        let task = createFolder(name: name, in: parent)
        // After the create lands, drop into inline rename of the new folder.
        pendingStructuralTaskForTesting = Task { [weak self] in
            await task?.value
            guard let self, self.currentSession != nil else { return }
            // Only enter rename if the create actually succeeded (no error).
            if self.lastError == nil {
                self.renamingNode = RenamingNode(path: path, isDirectory: true)
            }
        }
    }

    /// ⌘⌥R — rename the selected node. Enters inline-rename mode (the tree row
    /// swaps to a field); the actual FFI call fires on commit via `renameEntry`.
    func renameSelectedCommand() {
        guard isVaultOpen, let node = treeSelectedNode else { return }
        structuralRenameError = nil
        renamingNode = RenamingNode(path: node.path, isDirectory: node.isDirectory)
    }

    /// ⌘⇧M — open the Move-to-folder sheet for the selected node.
    func moveSelectedCommand() {
        guard isVaultOpen, let node = treeSelectedNode else { return }
        pendingMove = PendingMove(path: node.path, isDirectory: node.isDirectory)
    }

    /// File ▸ Reveal in Finder / palette — jump to the selected node on
    /// disk. Context menus keep their own per-clicked-node variant; this
    /// one follows the tree SELECTION like every other file command
    /// (context-menus.md redundancy rule: every context action needs a
    /// primary-UI home).
    func revealSelectedInFinderCommand() {
        guard isVaultOpen, let node = treeSelectedNode,
            let url = currentVaultURL?.appendingPathComponent(node.path)
        else { return }
        NSWorkspace.shared.activateFileViewerSelecting([url])
    }

    /// File ▸ Copy Path / palette — copy the selected node's absolute
    /// filesystem path (Finder's Copy-as-Pathname affordance).
    func copySelectedPathCommand() {
        guard let node = treeSelectedNode else { return }
        copyAbsolutePath(vaultRelative: node.path)
    }

    /// Shared Copy Path implementation — the menu/palette command above
    /// and the tree's context menu both land here so the pasteboard
    /// write and the AT announcement can never diverge between surfaces
    /// (red-team F7 on the corpus pass).
    func copyAbsolutePath(vaultRelative path: String) {
        guard isVaultOpen,
            let url = currentVaultURL?.appendingPathComponent(path)
        else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(url.path, forType: .string)
        let name = (path as NSString).lastPathComponent
        postAccessibilityAnnouncement("Copied path of \(name).")
    }

    /// ⌘⌫ (tree-focused) / context-menu "Move to Trash" — delete the selected
    /// node. Routes through `requestDeleteEntry` so a non-empty folder
    /// stages the #860 confirmation; files and empty folders delete
    /// immediately as before. No cached child count here (the selection
    /// mirror carries only path + kind) — the FileManager fallback counts.
    func deleteSelectedCommand() {
        guard isVaultOpen, let node = treeSelectedNode else { return }
        requestDeleteEntry(path: node.path, isDirectory: node.isDirectory)
    }

    /// File ▸ Duplicate / palette / context menu — duplicate the selected
    /// FILE next to itself (#853). Folder selections no-op (folders are out
    /// of #853's scope); the menu items are disabled for them, and the
    /// palette row falls through here harmlessly.
    func duplicateSelectedCommand() {
        guard isVaultOpen, let node = treeSelectedNode else { return }
        guard !node.isDirectory else {
            // The palette row can't gray out per-selection like the menu
            // items do — a silent no-op there reads as breakage, so the
            // folder case announces its scope (red-team).
            postAccessibilityAnnouncement(
                "Duplicate applies to files only.", priority: .medium)
            return
        }
        pendingStructuralTaskForTesting = duplicateEntry(path: node.path)
    }

    /// Every folder path in the vault (vault-relative, sorted case-insensitive,
    /// dirs-first order from the API), for the Move-to-folder picker. Walks
    /// `list_dir_children` recursively off the main actor. The vault root is NOT
    /// included as a path here — the picker adds a "Vault root" row (path "")
    /// explicitly. Empty folders ARE included (they're real move targets).
    func loadAllFolders() async -> [String] {
        guard let session = currentSession else { return [] }
        return await Task.detached(priority: .userInitiated) {
            var out: [String] = []
            var queue: [String] = [""]
            // Bound the walk defensively against a pathological vault so the
            // picker can't hang; realistic vaults are far under this.
            var visited = 0
            let cap = 50_000
            while !queue.isEmpty, visited < cap {
                let parent = queue.removeFirst()
                visited += 1
                guard
                    let listing = try? session.listDirChildren(
                        parentPath: parent,
                        paging: Paging(cursor: nil, limit: 5000))
                else { continue }
                for dir in listing.dirs {
                    out.append(dir.path)
                    queue.append(dir.path)
                }
            }
            return out
        }.value
    }

    /// A non-colliding "Untitled Folder" / "Untitled Folder N" for `parent`.
    private func uniqueUntitledFolderName(in parent: String) -> String {
        // Folders aren't in `files`; derive existing sibling folder names from
        // the file set's path prefixes under `parent`.
        let prefix = parent.isEmpty ? "" : parent + "/"
        let siblingDirs: Set<String> = Set(
            files.compactMap { file -> String? in
                guard file.path.hasPrefix(prefix) else { return nil }
                let rest = String(file.path.dropFirst(prefix.count))
                guard let slash = rest.firstIndex(of: "/") else { return nil }
                return String(rest[rest.startIndex..<slash]).lowercased()
            })
        if !siblingDirs.contains("untitled folder") { return "Untitled Folder" }
        var n = 2
        while siblingDirs.contains("untitled folder \(n)") { n += 1 }
        return "Untitled Folder \(n)"
    }

    // MARK: - Property edits (Milestone I)

    /// Insert or replace a frontmatter property on `path`. Routes
    /// through the same `save_text` pipeline as the editor save, so
    /// `WriteConflict` is detected the same way and the F-precedent
    /// conflict dialog is reused (scoped to the property edit via
    /// `currentPropertyEditConflict`).
    ///
    /// On success: refreshes `currentNoteProperties` +
    /// `currentNoteContentHash` so the panel reflects the new state
    /// and a subsequent edit doesn't carry a stale hash.
    @discardableResult
    func setProperty(path: String, key: String, value: PropertyValue) -> Task<Void, Never>? {
        guard !isEditingProperty else { return nil }
        guard let session = currentSession else { return nil }
        isEditingProperty = true
        propertyEditError = nil
        let expected = currentNoteContentHash
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performPropertyEdit(
                session: session,
                path: path,
                key: key,
                action: .set(value),
                expectedHash: expected
            )
            return
        }
        propertyEditTask = task
        return task
    }

    /// U3-4 (#468): commit the show-source editor's YAML draft — the
    /// whole frontmatter block replaced through `set_frontmatter_source`
    /// (YAML validated Rust-side FIRST; malformed input writes nothing).
    /// Rides the property-edit machinery (`.setSource` action): same
    /// in-flight guard, same WriteConflict alert + resolutions, and the
    /// stage-A success refresh (fmSource + offsets + hash + rows).
    @discardableResult
    func applyPropertiesSource(_ draft: String) -> Task<Void, Never>? {
        guard !isEditingProperty else { return nil }
        guard let session = currentSession, let path = loadedFilePath else { return nil }
        isEditingProperty = true
        propertyEditError = nil
        propertiesSourceError = nil
        let expected = currentNoteContentHash
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performPropertyEdit(
                session: session,
                path: path,
                key: "",
                action: .setSource(draft),
                expectedHash: expected
            )
            return
        }
        propertyEditTask = task
        return task
    }

    /// ⌘⇧D / palette: ask the widget to flip fields ⇄ source. View state
    /// (draft, mode) lives in the widget; AppState can only signal — the
    /// U4-4 request-token pattern. No-ops without a renderable note.
    @Published private(set) var propertiesSourceToggleRequest: Int = 0

    func togglePropertiesSourceCommand() {
        guard loadedFilePath != nil, noteLoadError == nil else { return }
        propertiesSourceToggleRequest &+= 1
    }

    /// #868: published mirror of the properties widget's view-local
    /// source-mode `@State`. The DRAFT must stay in the view (#448:
    /// uncommitted user input never rides a `@Published`), but the
    /// View ▸ Show/Hide Properties Source title needs the direction —
    /// so the widget mirrors JUST the bool through
    /// `notePropertiesSourceModeChanged` from a post-update
    /// `.onChange(of: isSourceMode)` (plus an `.onAppear` resync for
    /// mount gaps). `clearActiveNoteFields` also resets it on note
    /// transitions: the widget self-hides without firing its onChange
    /// when the note vanishes (same belt as `propertiesSourceError`).
    @Published private(set) var propertiesSourceShowing = false

    /// The widget's single-writer seam for the mirror above — the view
    /// remains the owner of the real state; AppState only reflects it.
    /// The equality guard makes the `.onAppear` resync publish-free on
    /// the common (already-in-sync) mount path.
    func notePropertiesSourceModeChanged(_ showing: Bool) {
        guard propertiesSourceShowing != showing else { return }
        propertiesSourceShowing = showing
    }

    /// Inline error for the show-source editor (U3-4): the Rust
    /// MalformedFrontmatter line/column message, or nil. Cleared on every
    /// apply attempt, on success, on source-mode exit (the widget's
    /// Cancel/Discard call `clearPropertiesSourceError`), and on note
    /// transitions (`clearActiveNoteFields`) — a stale message must never
    /// greet the next source session (Codoki #530). The draft is NEVER
    /// touched by the error path (non-destructive, DoD §F).
    @Published private(set) var propertiesSourceError: String?

    /// The widget's exit paths reset the inline error without widening
    /// the setter (state mutations stay funneled through AppState).
    func clearPropertiesSourceError() {
        propertiesSourceError = nil
    }

    /// Bumped on every successful `.setSource` commit — the widget flips
    /// back to the fields view on this edge (the row list re-read from
    /// disk state is the round-trip guarantee: no client-side YAML parse).
    @Published private(set) var propertiesSourceCommitted: Int = 0

    /// Remove a frontmatter property. Symmetric to `setProperty`:
    /// same conflict path, same refresh on success.
    @discardableResult
    func deleteProperty(path: String, key: String) -> Task<Void, Never>? {
        guard !isEditingProperty else { return nil }
        guard let session = currentSession else { return nil }
        isEditingProperty = true
        propertyEditError = nil
        let expected = currentNoteContentHash
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performPropertyEdit(
                session: session,
                path: path,
                key: key,
                action: .delete,
                expectedHash: expected
            )
            return
        }
        propertyEditTask = task
        return task
    }

    /// Shared body for `setProperty` / `deleteProperty`. Detaches
    /// the SQLite-mutex-holding FFI call off the main actor, then
    /// routes the outcome to one of three paths (success / conflict
    /// / error) on the main actor.
    private func performPropertyEdit(
        session: VaultSession,
        path: String,
        key: String,
        action: PropertyEditAction,
        expectedHash: String?
    ) async {
        let outcome: Result<SaveReport, VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                switch action {
                case .set(let value):
                    let report = try session.setProperty(
                        path: path,
                        key: key,
                        value: value,
                        expectedContentHash: expectedHash
                    )
                    return .success(report)
                case .delete:
                    let report = try session.deleteProperty(
                        path: path,
                        key: key,
                        expectedContentHash: expectedHash
                    )
                    return .success(report)
                case .setSource(let fmSource):
                    // Validates YAML first (MalformedFrontmatter, nothing
                    // written), then read-current-body + compose + save —
                    // all Rust-side (set_frontmatter_source, #469).
                    let report = try session.setFrontmatterSource(
                        path: path,
                        fmSource: fmSource,
                        expectedContentHash: expectedHash
                    )
                    return .success(report)
                }
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        if let gate = basesPostWritePublishGate { await gate() }

        // If the user navigated away mid-edit, drop the result
        // rather than mutating state for a file the user has
        // already moved on from. Same shape as `performSave`.
        guard currentSession === session else { return }
        if case .success = outcome {
            // The native property write is already indexed. Refresh global
            // Bases consumers before guarding active-note-only publication.
            refreshVisibleBasesAfterInAppWrite(session: session, changedPath: path)
        }
        guard loadedFilePath == path else {
            isEditingProperty = false
            return
        }

        switch outcome {
        case .success(let report):
            currentNoteContentHash = report.newContentHash
            // U3-3 (#469 handoff): the edit rewrote the file's frontmatter —
            // refresh fmSource + offsets from ONE read so the next composed
            // save can't resurrect stale fm. The body on disk is unchanged
            // by an fm-only edit, so the buffer + baseline stay untouched
            // (property edits are allowed while the body is dirty).
            await refreshNoteParts(session: session, path: path)
            guard currentSession === session else { return }
            guard loadedFilePath == path else {
                isEditingProperty = false
                return
            }
            // Refresh the properties panel so the row updates in
            // place. `loadCurrentLinks` already runs one trip
            // through the SQLite mutex for backlinks + outgoing +
            // properties — reusing it keeps the panel coherent
            // without a second round-trip.
            await loadCurrentLinks(path: path)
            guard currentSession === session else { return }
            guard loadedFilePath == path else {
                isEditingProperty = false
                return
            }
            if case .setSource = action {
                propertiesSourceError = nil
                propertiesSourceCommitted &+= 1
                postAccessibilityAnnouncement("Properties updated.", priority: .medium)
            } else {
                postAccessibilityAnnouncement(
                    "Property \(key) \(action == .delete ? "deleted" : "updated").",
                    priority: .medium
                )
            }
        case .failure(.WriteConflict(let currentHash, let expected, let currentMtimeMs)):
            currentPropertyEditConflict = PropertyEditConflict(
                path: path,
                key: key,
                action: action,
                currentContentHash: currentHash,
                expectedContentHash: expected,
                currentMtimeMs: currentMtimeMs
            )
            postAccessibilityAnnouncement(
                "Property edit blocked. \(filename(of: path)) was modified externally. Resolve in the dialog.",
                priority: .medium
            )
        case .failure(let error):
            if case .setSource = action,
                case .MalformedFrontmatter(_, let reason) = error
            {
                // U3-4: inline, specific, non-destructive — the draft stays
                // in the editor, focus stays put, nothing was written.
                propertiesSourceError = reason
                postAccessibilityAnnouncement(
                    "Properties source not applied: \(reason)",
                    priority: .high
                )
            } else {
                propertyEditError = humanReadable(error)
                postAccessibilityAnnouncement(
                    "Property edit failed: \(propertyEditError ?? "")",
                    priority: .high
                )
            }
        }
        isEditingProperty = false
    }

    /// Re-issue the property edit with the new on-disk hash as
    /// `expectedHash` — the user explicitly chose to overwrite the
    /// external change.
    @discardableResult
    func resolvePropertyEditConflictKeepMine() -> Task<Void, Never>? {
        guard let conflict = currentPropertyEditConflict,
            let session = currentSession,
            loadedFilePath == conflict.path
        else {
            currentPropertyEditConflict = nil
            return nil
        }
        currentPropertyEditConflict = nil
        isEditingProperty = true
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performPropertyEdit(
                session: session,
                path: conflict.path,
                key: conflict.key,
                action: conflict.action,
                expectedHash: conflict.currentContentHash
            )
            return
        }
        propertyEditTask = task
        return task
    }

    /// Drop the property edit attempt and reload the file from
    /// disk. The user explicitly chose to let the external write
    /// win for this property.
    @discardableResult
    func resolvePropertyEditConflictReloadFromDisk() -> Task<Void, Never>? {
        guard let conflict = currentPropertyEditConflict else { return nil }
        currentPropertyEditConflict = nil
        // Same path the conflict came from — refresh text + hash +
        // properties so the panel mirrors disk.
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.loadCurrentNote(path: conflict.path)
            return
        }
        noteLoadTask = task
        return task
    }

    /// Dismiss the property-edit conflict alert without acting on
    /// it. The panel stays as-is; the row's local "in-flight edit"
    /// state is cleared by the resolver (the row reads
    /// `currentPropertyEditConflict` going to `nil` as the cue to
    /// reset its commit state).
    func resolvePropertyEditConflictCancel() {
        currentPropertyEditConflict = nil
    }

    // MARK: - Bulk rename (#170)

    /// Dry-run a vault-wide property rename, populating
    /// `pendingRenameReport` with the per-file diff for the
    /// bulk-rename sheet's preview grid. No writes.
    @discardableResult
    func previewPropertyRename(oldKey: String, newKey: String) -> Task<Void, Never>? {
        runRename(oldKey: oldKey, newKey: newKey, dryRun: true)
    }

    /// Apply a vault-wide property rename. Each affected file is
    /// saved with its fresh on-disk hash as `expected_content_hash`
    /// so an external mid-rename modification surfaces as a per-file
    /// `RenameFailed` rather than aborting the whole run.
    @discardableResult
    func applyPropertyRename(oldKey: String, newKey: String) -> Task<Void, Never>? {
        runRename(oldKey: oldKey, newKey: newKey, dryRun: false)
    }

    /// Cancel an in-flight preview or apply via the existing
    /// cancellation token. The sheet's Esc handler calls this.
    func cancelPendingRename() {
        renameCancelToken?.cancel()
    }

    private func runRename(oldKey: String, newKey: String, dryRun: Bool) -> Task<Void, Never>? {
        guard !isRenameInFlight else { return nil }
        guard let session = currentSession else { return nil }
        renameError = nil
        isRenameInFlight = true
        let cancel = CancelToken()
        renameCancelToken = cancel
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performRename(
                session: session,
                oldKey: oldKey,
                newKey: newKey,
                dryRun: dryRun,
                cancel: cancel
            )
            return
        }
        renameTask = task
        return task
    }

    private func performRename(
        session: VaultSession,
        oldKey: String,
        newKey: String,
        dryRun: Bool,
        cancel: CancelToken
    ) async {
        let outcome: Result<RenameReport, VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                let report = try session.renamePropertyAcrossVault(
                    oldKey: oldKey,
                    newKey: newKey,
                    dryRun: dryRun,
                    cancel: cancel
                )
                return .success(report)
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        // #328 red-team P1: closeVault zeros out the rename-related
        // state and cancels the token, but the Rust call may have
        // resolved between the cancel signal arriving and this
        // continuation resuming on the main actor. Without the
        // guard, we'd write `pendingRenameReport`/`renameError`
        // back over the just-zeroed values and post an a11y
        // announcement against the welcome screen. Mirrors the
        // `loadedFilePath == path` guard in `performPropertyEdit`.
        guard !Task.isCancelled, currentSession === session else {
            isRenameInFlight = false
            return
        }

        switch outcome {
        case .success(let report):
            pendingRenameReport = report
            // Apply may have touched the currently-loaded note's
            // properties (when the renamed key was in it). Refresh
            // links + properties so the panel doesn't show stale
            // rows for the renamed key.
            if !dryRun, let path = loadedFilePath {
                await loadCurrentLinks(path: path)
                // Refresh the disk-tracked hash so subsequent edits
                // don't trip a stale WriteConflict. `getFileMetadata`
                // returns `FileMetadata?` and `try?` flattens the
                // throw, so we get a double-Optional that we unwrap
                // before reading the hash.
                let metadata: FileMetadata?? = await Task.detached(priority: .userInitiated) {
                    try? session.getFileMetadata(path: path)
                }.value
                if let outer = metadata, let inner = outer {
                    currentNoteContentHash = inner.contentHash
                }
            }
            let summary = renameSummary(report: report, applied: !dryRun)
            postAccessibilityAnnouncement(summary, priority: .medium)
        case .failure(let error):
            renameError = humanReadable(error)
            postAccessibilityAnnouncement(
                "Rename failed: \(renameError ?? "")",
                priority: .high
            )
        }
        isRenameInFlight = false
        renameCancelToken = nil
    }

    /// Render a one-line summary of a `RenameReport` for the
    /// accessibility announcement + the sheet's footer.
    private func renameSummary(report: RenameReport, applied: Bool) -> String {
        if applied {
            let renamed = report.affected.filter { $0.applied }.count
            let skipped = report.skipped.count
            let failed = report.failed.count
            return "\(renamed) renamed, \(skipped) skipped, \(failed) failed."
        } else {
            let will = report.affected.count
            let skipped = report.skipped.count
            return "\(will) \(will == 1 ? "file" : "files") will be renamed, \(skipped) skipped, 0 errors."
        }
    }

    /// "Save changes?" prompt: Save → run the save, then continue
    /// with the pending navigation if the save succeeds. A
    /// `WriteConflict` short-circuits the navigation so the user
    /// gets the conflict alert in place of the navigation step.
    @discardableResult
    func resolvePendingNavigationSave() -> Task<Void, Never>? {
        guard let pending = pendingNavigation else { return nil }
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.saveAndPerformNavigation(pending)
            return
        }
        return task
    }

    private func saveAndPerformNavigation(_ pending: PendingNavigation) async {
        await saveCurrentNote()?.value
        // Save can complete in one of three states; only proceed
        // with the navigation when the save genuinely succeeded
        // (hasUnsavedChanges cleared, no conflict, no error).
        guard !hasUnsavedChanges,
            currentSaveConflict == nil,
            saveError == nil
        else {
            return
        }
        applyPendingNavigation(pending)
    }

    /// "Save changes?" prompt: Discard → drop the dirty flag and
    /// continue with the pending navigation.
    func resolvePendingNavigationDiscard() {
        guard let pending = pendingNavigation else { return }
        hasUnsavedChanges = false
        // Restore the editor buffer to the baseline so a subsequent
        // load doesn't have a stale dirty buffer hanging around in
        // memory (the load will overwrite anyway, but matching
        // baseline keeps the dirty flag honest in the interim).
        currentNoteText = savedBaselineText
        applyPendingNavigation(pending)
    }

    /// "Save changes?" prompt: Cancel → clear the pending
    /// navigation without saving. The dirty buffer stays.
    func resolvePendingNavigationCancel() {
        pendingNavigation = nil
        // U4-3: cancelling the gate cancels an in-flight vault switch — drop
        // the parked target so no close/open follows and a later plain Close
        // Vault can't inherit it.
        pendingVaultSwitchTarget = nil
    }

    /// Common tail for the Save / Discard branches: clear the
    /// pending state and actually perform the requested
    /// navigation.
    private func applyPendingNavigation(_ pending: PendingNavigation) {
        pendingNavigation = nil
        switch pending {
        case .closeVault:
            closeVault()
        case .selectFile(let path):
            // Setting selectedFilePath re-triggers the
            // handleSelectionChange subscription. At this point
            // `hasUnsavedChanges` is false (Save cleared it; Discard
            // cleared it), so the dirty gate falls through and the
            // load proceeds normally.
            selectedFilePath = path
        }
    }

    /// Refresh headings for the just-saved note without
    /// re-reading its text. `save_text` already updates the
    /// `headings` table inside its transaction, so a single
    /// metadata fetch is enough. Failures are non-fatal: the
    /// outline shows the old list until the next reload.
    private func refreshHeadingsAfterSave(session: VaultSession, path: String) {
        Task { [weak self] in
            // Off-actor metadata fetch — getFileMetadata grabs the
            // SQLite mutex, so we don't want to pin the main actor
            // for its duration. The post-await re-grab of `self`
            // keeps the Sendable check happy under the Swift 6
            // language mode.
            let headings: [Heading]? = await Task.detached(priority: .userInitiated) {
                (try? session.getFileMetadata(path: path))?.headings
            }.value
            guard let self else { return }
            guard self.loadedFilePath == path else { return }
            // U3-3: metadata offsets are whole-file; the buffer is body.
            self.currentNoteHeadings = Self.rebasedToBody(
                headings ?? [], prefixBytes: self.bodyByteOffset)
        }
    }

    /// Closes the vault when the editor is clean; routes through
    /// the "Save changes?" alert when it's dirty. The toolbar
    /// "Close Vault" button calls this instead of `closeVault()`
    /// directly so the dirty path can't be bypassed.
    func attemptCloseVault() {
        if hasUnsavedChanges {
            pendingNavigation = .closeVault
        } else {
            closeVault()
        }
    }

    func filename(of path: String) -> String {
        (path as NSString).lastPathComponent
    }

    // MARK: - Scan progress

    /// Main-actor entry point for scanner events. Called by
    /// `ScanProgressAdapter` after marshaling from the scanner thread.
    /// `internal` so XCTest can drive synthetic event streams against
    /// it without spinning up a real scan.
    func handleScanProgress(_ event: ScanProgress) {
        scanProgress = event
        switch event {
        case .started(let totalFiles):
            // Forced announcement: the user wants to know a scan
            // started even if they just opened the vault and we
            // haven't accumulated 350 ms of cooldown yet.
            announceScan(
                message: "Scanning vault. "
                    + "\(totalFiles) \(totalFiles == 1 ? "file" : "files") to index.",
                force: true
            )
        case .fileIndexed(_, let indexed, let total):
            // Rate-limited: at most ~3 per second per the acceptance
            // criteria, so VoiceOver flow stays polite even on a
            // 50k-file vault.
            announceScan(
                message: "Indexed \(indexed) of \(total) files.",
                force: false
            )
        case .finished(let report):
            announceScan(
                message: "Scan complete. "
                    + "\(report.filesIndexed) "
                    + (report.filesIndexed == 1 ? "file" : "files")
                    + " indexed.",
                force: true
            )
            // Clear so the progress bar hides; loadFiles' post-scan
            // populate runs next and updates `files`.
            scanProgress = nil
        case .cancelled, .failed:
            // No "finished" announcement. Failed is surfaced via
            // `scanError` (the existing path); cancelled is silent
            // because closeVault / next-vault flow is already
            // visible.
            scanProgress = nil
        }
    }

    /// Post a VoiceOver announcement subject to the rate guard. When
    /// `force` is true the announcement always fires (used for
    /// Started/Finished). Otherwise it only fires if the clock has
    /// advanced past the configured min-interval since the last fire.
    private func announceScan(message: String, force: Bool) {
        let now = scanClock()
        if !force,
            now.timeIntervalSince(scanAnnouncementLastFiredAt) < scanAnnouncementMinInterval
        {
            return
        }
        scanAnnouncementLastFiredAt = now
        scanAnnouncementCount += 1
        scanAnnouncementLastMessage = message
        postAccessibilityAnnouncement(message)
    }

    // MARK: - Templates (Milestone H)

    /// Re-fetch templates and open the picker sheet. Always
    /// succeeds: an empty `Templates/` folder, a non-existent one,
    /// or a `listTemplates` failure all surface as "no templates"
    /// rather than an error path — the picker is meant to be benign
    /// on a vault with no templates configured.
    ///
    /// Announces the result count through the polite live region
    /// so VoiceOver users know whether the picker has content
    /// before they start arrow-navigating. The empty-state
    /// announcement explicitly tells the user where to put their
    /// templates.
    @discardableResult
    func openTemplatePicker() -> Task<Void, Never> {
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performOpenTemplatePicker()
        }
        templatePickerTask = task
        return task
    }

    private func performOpenTemplatePicker() async {
        guard let session = currentSession else {
            availableTemplates = []
            isTemplatePickerOpen = true
            announceTemplate(
                "Template picker opened. No vault loaded; cannot list templates."
            )
            return
        }
        let summaries: [TemplateSummary] = await Task.detached(
            priority: .userInitiated
        ) {
            (try? session.listTemplates()) ?? []
        }.value
        // #328 red-team P1: vault may have been closed mid-list.
        // Without the guard we'd re-present the picker against the
        // welcome screen and announce "Template picker opened."
        // even though closeVault just zeroed everything.
        guard !Task.isCancelled, currentSession === session else { return }
        availableTemplates = summaries
        isTemplatePickerOpen = true
        announceTemplate(templatePickerOpenAnnouncement(summaries.count))
    }

    /// Compose the open-picker announcement. Pulled out so the
    /// empty-state copy stays in sync with the non-empty case and
    /// so unit tests can hit it without standing up a real vault.
    private func templatePickerOpenAnnouncement(_ count: Int) -> String {
        switch count {
        case 0:
            let vaultLabel = currentVaultURL?.lastPathComponent ?? "this vault"
            return "Template picker opened. No templates found. "
                + "Create one in \(vaultLabel)/Templates/."
        case 1:
            return "Template picker opened. 1 template available."
        default:
            return "Template picker opened. \(count) templates available."
        }
    }

    /// User chose a template row in the picker. Pulls the source off
    /// disk to extract prompt metadata; routes to the prompt sheet
    /// if there are prompts, otherwise straight to the name sheet.
    /// On read failure (template deleted between list and select)
    /// cancels the flow rather than blocking on an error sheet.
    @discardableResult
    func selectTemplate(_ summary: TemplateSummary) -> Task<Void, Never> {
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performSelectTemplate(summary)
        }
        templateSelectionTask = task
        return task
    }

    private func performSelectTemplate(_ summary: TemplateSummary) async {
        guard let session = currentSession else {
            cancelTemplateFlow()
            return
        }
        let sourceResult: String? = await Task.detached(
            priority: .userInitiated
        ) {
            try? session.readText(path: summary.path)
        }.value
        // #328 red-team P1: vault close mid-read would otherwise
        // resume here, run `cancelTemplateFlow()`/`pendingTemplateFlow
        // = .needsName(...)` against the dead session, and re-present
        // the template flow sheet on the welcome screen.
        guard !Task.isCancelled, currentSession === session else { return }
        guard let source = sourceResult else {
            cancelTemplateFlow()
            return
        }
        let metadata = extractTemplateMetadata(source: source)
        isTemplatePickerOpen = false
        if metadata.prompts.isEmpty {
            pendingTemplateFlow = .needsName(summary, [:])
        } else {
            pendingTemplateFlow = .needsPrompts(summary, metadata.prompts)
        }
    }

    /// Hand-off from the prompt sheet's Submit button. Stuffs the
    /// user's responses into the flow and advances to the name
    /// sheet. No-op when the flow isn't currently waiting on prompts
    /// (e.g. a late callback from a sheet that already dismissed).
    func submitTemplatePrompts(_ values: [String: String]) {
        guard case .needsPrompts(let template, _) = pendingTemplateFlow else {
            return
        }
        pendingTemplateFlow = .needsName(template, values)
    }

    /// Final step. Renders the template, atomically writes the new
    /// note, selects it in the editor, and parks the cursor at the
    /// template's `{{cursor}}` byte offset (if any).
    ///
    /// Path validation rejects empty / `.` / `..` / absolute paths
    /// up front so the user sees the error inline instead of as a
    /// late `save_text` failure. Cancel from any earlier sheet
    /// leaves no file on disk; this method only fires on the user's
    /// explicit Submit.
    @discardableResult
    func submitTemplateNoteName(_ relativePath: String) -> Task<Void, Never>? {
        guard case .needsName(let template, let promptValues) = pendingTemplateFlow
        else { return nil }
        guard let session = currentSession,
            let vaultURL = currentVaultURL
        else {
            cancelTemplateFlow()
            return nil
        }

        let trimmed = relativePath.trimmingCharacters(in: .whitespacesAndNewlines)
        if let problem = validateTemplateNoteName(trimmed) {
            templateNoteNameError = problem
            return nil
        }

        // Append `.md` if the user omitted it. The picker's "default
        // name" already includes it, but a paste from elsewhere
        // might not — adding it here keeps every created template
        // note Markdown-shaped. `pathExtension` is more robust than
        // a bare `hasSuffix(".md")`: it correctly handles multi-dot
        // names like `archive.tar.MD` (extension is `MD`, lowercased
        // matches `md`, so we leave it alone instead of producing
        // `archive.tar.MD.md` — Codoki PR #154 suggestion). Case-
        // insensitive comparison also preserves the user's casing
        // for any-case `.md` / `.MD` / `.Md` (#133).
        let normalized = (trimmed as NSString).pathExtension.lowercased() == "md"
            ? trimmed : "\(trimmed).md"
        // `title` for the rendered template is the file stem of the
        // new note, not the template's own name. Matches how
        // `{{title}}` is documented to behave in §8.2.
        let titleStem = ((normalized as NSString).lastPathComponent as NSString)
            .deletingPathExtension
        let context = TemplateContext(
            nowMs: Int64(Date().timeIntervalSince1970 * 1000),
            title: titleStem,
            vaultName: vaultURL.lastPathComponent,
            promptValues: promptValues
        )

        templateNoteNameError = nil
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performCreateNoteFromTemplate(
                session: session,
                template: template,
                context: context,
                relativePath: normalized
            )
        }
        templateCreateTask = task
        return task
    }

    private func performCreateNoteFromTemplate(
        session: VaultSession,
        template: TemplateSummary,
        context: TemplateContext,
        relativePath: String
    ) async {
        // Render + save off the main actor so the SQLite mutex and
        // file write don't pin the UI thread. Match the
        // `performSave` shape so the threading story stays uniform
        // across editor-save and template-create paths.
        let outcome: Result<RenderedTemplate, VaultError> = await Task.detached(
            priority: .userInitiated
        ) {
            do {
                let rendered = try session.renderTemplate(
                    templatePath: template.path,
                    context: context
                )
                // Same no-clobber discipline as createNote (#796): the
                // template flow races external creates identically.
                _ = try session.createExclusive(
                    path: relativePath,
                    content: rendered.body
                )
                return .success(rendered)
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        // #328 red-team P1: vault close mid-render-and-save would
        // otherwise resume here, write `selectedFilePath` to the
        // dead session, and announce "Created X from Y." on the
        // welcome screen. The file was already saved to disk
        // (the detached block ran to completion); the late state
        // mutation is the only thing we suppress here.
        guard !Task.isCancelled, currentSession === session else { return }

        switch outcome {
        case .success(let rendered):
            // #871 Codex round 2: template creation writes a file outside the
            // `publishTreeMutation` barrier — clear the structural undo history
            // so a stale inverse can't target the created path.
            clearStructuralUndoStacks()
            pendingTemplateFlow = .idle
            templateNoteNameError = nil
            // #421 (F-H1): .high, not .medium — the create
            // announcement is immediately followed by
            // selectedFilePath changing, whose "Showing <file>."
            // announcement supersedes an in-flight medium one before
            // VO finishes it. High makes the completed user action
            // win; the picker-open/cancel messages stay .medium
            // (red-team scoping note).
            announceTemplate(
                "Created \(filename(of: relativePath)) from \(template.name).",
                priority: .high
            )
            // Open the new file. SwiftUI propagates the binding
            // change → `handleSelectionChange` kicks off a fresh
            // note load. After the load resolves, we send the
            // cursor request so `NoteEditorView`'s coordinator can
            // park the caret at `{{cursor}}`.
            selectedFilePath = relativePath
            // Refresh the file list so the new note appears in
            // the sidebar without waiting for the next external
            // event. Same shape `save_text` doesn't do because
            // it only changes existing files; create-from-template
            // adds a new row.
            await loadFiles()
            if let offset = rendered.cursorByteOffset {
                let pendingLoad = noteLoadTask
                Task { @MainActor [weak self] in
                    if let pendingLoad {
                        await pendingLoad.value
                    }
                    guard let self,
                        self.selectedFilePath == relativePath
                    else { return }
                    // U3-3: {{cursor}} offsets are whole-rendered-file;
                    // the load above set the body offsets for this note.
                    self.cursorByteOffsetRequest.send(
                        self.bodyByte(fromFileByte: Int(offset)))
                    // Focus follows content (F-H1): the caret park
                    // alone leaves keyboard focus on the window; the
                    // coordinator makes the editor first responder
                    // when it handles this request.
                }
            }
        case .failure(let error):
            templateNoteNameError = humanReadable(error)
        }
    }

    /// Reset the create-from-template flow back to idle. Called by
    /// every sheet's Cancel button and by the failure paths above.
    /// Idempotent — safe to call from any state.
    func cancelTemplateFlow() {
        pendingTemplateFlow = .idle
        isTemplatePickerOpen = false
        templateNoteNameError = nil
    }

    /// Reject the new-note name early so the user sees a useful
    /// inline error rather than a `save_text` `InvalidPath` failure
    /// that arrives after the render finishes. Mirrors the rules in
    /// `validate_save_path` in `slate-core`.
    private func validateTemplateNoteName(_ candidate: String) -> String? {
        if candidate.isEmpty {
            return "Note name cannot be empty."
        }
        if candidate == "." || candidate == ".." {
            return "Note name cannot be `.` or `..`."
        }
        if candidate.hasPrefix("/") {
            return "Note name must be vault-relative, not absolute."
        }
        // Any segment equal to `..` is a path traversal. Block both
        // `../foo.md` and `foo/../bar.md`.
        for segment in candidate.split(separator: "/", omittingEmptySubsequences: false) {
            if segment == ".." {
                return "Note name cannot contain `..` segments."
            }
        }
        return nil
    }

    /// Convenience: the new-note name field's default value for a
    /// freshly-selected template. Templates whose name starts with
    /// the **word** `Daily` get a date suffix so the user can
    /// confirm-Enter on the daily-note flow without typing;
    /// everything else pre-fills with the template's own name.
    ///
    /// The date is joined with a **space** rather than a dash so
    /// multi-word names read naturally: `Daily Standup` becomes
    /// `Daily Standup 2026-05-23.md`, not the awkward
    /// `Daily Standup-2026-05-23.md` (#133). A bare `Daily` template
    /// becomes `Daily 2026-05-23.md`, which matches the spacing most
    /// users would type by hand.
    ///
    /// "Word" prefix means `daily` is either the whole name or is
    /// followed by a non-alphanumeric character — so `Daily`,
    /// `Daily Standup`, `Daily-notes`, and `Daily.md` all qualify,
    /// but `Dailyness` and `Daily123` don't (Codoki PR #154
    /// suggestion).
    func defaultNewNoteName(for template: TemplateSummary) -> String {
        if Self.isDailyTemplateName(template.name) {
            let formatter = DateFormatter()
            formatter.locale = Locale(identifier: "en_US_POSIX")
            formatter.timeZone = TimeZone(secondsFromGMT: 0)
            formatter.dateFormat = "yyyy-MM-dd"
            return "\(template.name) \(formatter.string(from: Date())).md"
        }
        return "\(template.name).md"
    }

    /// `true` iff `name` begins with the standalone word `daily`
    /// (case-insensitive). Catches `Daily`, `Daily Standup`,
    /// `Daily-notes`; rejects `Dailyness`, `Daily123`. Pulled out
    /// + made static so tests can drive it directly without
    /// constructing a `TemplateSummary`.
    static func isDailyTemplateName(_ name: String) -> Bool {
        let lower = name.lowercased()
        guard lower.hasPrefix("daily") else { return false }
        let afterIdx = lower.index(lower.startIndex, offsetBy: "daily".count)
        if afterIdx == lower.endIndex { return true }
        let next = lower[afterIdx]
        // Anything that isn't a continuation of the word `daily` is
        // a word boundary: whitespace, dashes, periods, slashes are
        // all valid daily-template separators.
        return !next.isLetter && !next.isNumber
    }

    /// Post a polite announcement through AppKit's accessibility
    /// bus and capture it for the test target. Same shape as
    /// `announceScan` but without the per-second rate guard —
    /// template events are user-driven and never rapid-fire.
    private func announceTemplate(
        _ message: String,
        priority: NSAccessibilityPriorityLevel = .medium
    ) {
        templateAnnouncementLastMessage = message
        postAccessibilityAnnouncement(message, priority: priority)
    }

    // MARK: - Private

    private func recordOpened(url: URL) {
        let entry = RecentVault(url: url)
        do {
            recentVaults = try recentsStore.add(entry)
        } catch {
            // Same rationale as removeRecent: don't block the open flow
            // on a recents-list write failure.
            fputs(
                "Slate: failed to persist recent-vaults add: \(error)\n",
                stderr
            )
        }
    }

    func humanReadable(_ error: VaultError) -> String {
        switch error {
        case .Io(let message), .Db(let message), .Trash(let message):
            return message
        case .InvalidPath(let path, let reason):
            return "Invalid path \(path): \(reason)"
        case .Cancelled:
            return "Operation cancelled."
        case .InvalidUtf8(let path):
            return "File at \(path) is not valid UTF-8."
        case .FileTooLarge(let path, let size):
            return "File at \(path) is \(size) bytes — larger than this build's refuse threshold."
        case .InvalidQuery(let message):
            return "Search query is invalid: \(message)"
        case .Unsupported(let feature):
            return "\(feature) is not implemented yet."
        case .InvalidArgument(let message):
            return "Invalid argument: \(message)"
        case .WriteConflict:
            // The editor's save-flow handles this case directly with
            // a "Keep mine / Reload from disk" affordance (issue #64);
            // surfacing it through the generic humanReadable path is
            // a last-resort fallback for non-editor callers.
            return "This file was modified by another writer since you opened it. Reload to see the latest version."
        case .MalformedFrontmatter(let path, let reason):
            return
                "Frontmatter at \(path) is malformed: \(reason). Fix the YAML in this note before editing properties."
        case .BibSourceUnreadable(let path, let reason):
            return "Bibliography source \(path) couldn't be opened: \(reason)."
        case .CslStyleUnreadable(let path, let reason):
            return "Citation style \(path) couldn't be loaded: \(reason)."
        case .PrefsUnreadable(let path, let reason):
            return "Preferences file \(path) couldn't be loaded: \(reason)."
        case .DestinationExists(let path):
            return "Something named \(path) already exists there. Choose a different name."
        case .HistoryUnavailable(let path, _):
            // O-3 (#541): a version operation failed integrity
            // verification and refused rather than serving wrong
            // bytes. The reason string is diagnostic detail; the
            // user-facing message keeps the O-5 alert's framing.
            return "History for \(path) is unavailable: it failed an integrity check."
        }
    }
}

/// 1-based line number of the first occurrence (in `body`) of any
/// alphanumeric token from `query`. Falls back to 1 when no token
/// can be found — FTS5 may have matched through stemming, so the
/// raw tokens needn't appear literally in the note.
///
/// Lives at file scope (not on AppState) so it has no implicit
/// MainActor isolation — pure string crunching, the caller can
/// invoke it from any actor context.
///
/// Mirrors the Rust-side heuristic that lived in
/// `search_db::find_first_token_line` before #92 item 1 moved
/// line derivation out of `full_text_search`. Lowercases the body
/// Pull the first citation key from a raw source-form string. Handles
/// `[@key]`, `[@key, p. 23]`, `[-@key]`, `@key`, etc. Returns `""` when
/// no `@`-anchored key is found. Mirrors the helper in
/// `CitationPopover.extractKey` — pulled out here so the AppState
/// Jump-to-Bibliography action can call it without coupling to the
/// view layer.
func extractCitationKey(from raw: String) -> String {
    guard let atIdx = raw.firstIndex(of: "@") else { return "" }
    let after = raw.index(after: atIdx)
    let tail = raw[after...]
    let key = tail.prefix { ch in
        ch.isLetter || ch.isNumber || ch == "_" || ch == "-" || ch == ":"
            || ch == "." || ch == "+"
    }
    return String(key).trimmingCharacters(in: CharacterSet(charactersIn: "."))
}

/// Pure filter used by `BibliographyPanel`. Empty query returns
/// `entries` unchanged. Otherwise case-insensitive substring match
/// against title, key, author family, and author given names. Split
/// out as a free function so tests can exercise the logic without
/// AppState's `private(set)` constraints.
func filterBibliographyEntries(_ entries: [BibEntry], query: String) -> [BibEntry] {
    let q = query
        .trimmingCharacters(in: .whitespacesAndNewlines)
        .lowercased()
    guard !q.isEmpty else { return entries }
    return entries.filter { entry in
        if entry.title.lowercased().contains(q) { return true }
        if entry.key.lowercased().contains(q) { return true }
        for author in entry.authors {
            if author.family.lowercased().contains(q) { return true }
            if let given = author.given,
                given.lowercased().contains(q)
            {
                return true
            }
        }
        return false
    }
}

/// Synthesize a `RenderedCitation` for a `CitationReference` when no
/// CSL style is configured yet. The panel will announce these via
/// the citation key — better than rendering nothing, since AT users
/// at least learn where citations live in the document. Once #281
/// wires a default style, the real renderer takes over.
func placeholderRendered(for ref: CitationReference) -> RenderedCitation {
    let speech: String
    if ref.citations.count == 1 {
        let item = ref.citations[0]
        switch item.mode {
        case .inText:
            speech = item.key
        default:
            speech = "Citation: \(item.key)"
        }
    } else {
        let keys = ref.citations.map { $0.key }.joined(separator: ", ")
        speech = "Citation: \(keys)"
    }
    return RenderedCitation(
        raw: ref.raw,
        visualText: ref.raw,
        speechText: speech,
        bibEntry: nil,
        styleId: ""
    )
}

/// once and counts newlines in the lowercased prefix — avoids a
/// cross-string slice that would panic on non-boundary indices
/// when Unicode lowercasing changes byte length (`İ` → `i` + U+0307
/// is 2→3 bytes).
func firstTokenLineNumber(in body: String, query: String) -> Int {
    let bodyLower = body.lowercased()
    // Strip FTS5 column-filter prefixes before tokenizing. Today the
    // only indexed column is `body_text`; a user typing
    // `body_text:foo` means "find `foo` inside body_text", so the
    // `body_text:` part shouldn't seed tokens for the line scan. If
    // more columns ever land in the FTS5 schema, add their names
    // here.
    let preprocessed = query.lowercased()
        .replacingOccurrences(of: "body_text:", with: " ")
    // FTS5 keywords that would otherwise sneak through the split and
    // pollute the line lookup if they happen to appear as bare words
    // in prose (#93 item 5). Pure-numeric tokens are also dropped:
    // numbers appearing inside composite FTS5 constructs
    // (`NEAR(a b, 5)`, `LIMIT 10`) aren't semantically meaningful to
    // a body-line scan.
    let fts5Keywords: Set<String> = ["and", "or", "not", "near"]
    let tokens = preprocessed
        .split { !$0.isLetter && !$0.isNumber }
        .map(String.init)
        .filter { tok in
            !tok.isEmpty
                && !fts5Keywords.contains(tok)
                && !tok.allSatisfy(\.isNumber)
        }
    var earliest: String.Index? = nil
    for tok in tokens {
        if let range = bodyLower.range(of: tok) {
            switch earliest {
            case .none:
                earliest = range.lowerBound
            case .some(let prev):
                earliest = min(prev, range.lowerBound)
            }
        }
    }
    guard let earliest else { return 1 }
    // Count newlines in the prefix using UTF-8 view so we don't
    // pay for an O(n) String.distance over the prefix.
    let prefix = bodyLower[..<earliest]
    return prefix.utf8.reduce(1) { acc, byte in
        byte == 0x0A ? acc + 1 : acc
    }
}
