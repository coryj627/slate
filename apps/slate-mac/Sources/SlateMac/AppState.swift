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
    /// #313). Opened by `⌘⇧P` from the `SlateMacApp` menu (only
    /// when a vault is open — see the `.disabled` gate); closed
    /// by `Esc` (cancel-action button inside the palette). The
    /// palette registry, fuzzy filter, sections, and recents
    /// arrive in #314–#316.
    ///
    /// Reset to `false` by `closeVault()` so a vault close with
    /// the palette open doesn't leave the bool stuck (and re-
    /// trigger the sheet on the next vault open).
    @Published var isCommandPaletteOpen: Bool = false

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

    /// Citation the user wants to expand (Cmd+Shift+E / row activation
    /// in `CitationsPanel`). Drives the `CitationPopover` sheet bound
    /// in `MainSplitView`.
    @Published var expandedCitation: RenderedCitation?

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
    /// CitationsPanel). Drives the same `CitationPopover` sheet — we
    /// wrap the entry in a synthetic `RenderedCitation` so the
    /// popover renders without a separate code path.
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
    /// `TasksReviewView` uses this to show a loading placeholder
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
    /// Whether the `TasksReviewView` sheet is currently presented.
    /// Cmd+Shift+T toggles this; the sheet's `onDismiss` also flips
    /// it back to false. Setting this `true` does NOT auto-load —
    /// `openTasksReview()` is the public entry point that kicks
    /// the query.
    @Published var isTasksReviewOpen: Bool = false

    /// True while the search overlay is visible. Cmd+F toggles;
    /// Esc clears.
    @Published var isSearchOpen: Bool = false
    /// Live search query — bound to the overlay's TextField. Every
    /// edit feeds the debouncer; the actual search fires ~150 ms
    /// after the user stops typing.
    @Published var searchQuery: String = ""
    /// Current state of the search overlay's results panel.
    @Published private(set) var searchState: SearchState = .idle
    /// Pre-rendered audio summary for the live region. Mirrors
    /// `searchState`'s results.summary so the SwiftUI .onChange
    /// observer can fire a polite announcement.
    @Published private(set) var searchSummary: String = ""

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
    let cursorByteOffsetRequest = PassthroughSubject<Int, Never>()

    /// One-shot channel for "scroll the content pane to this heading
    /// anchor." `OutlineSidebar` sends; `NoteContentView` subscribes
    /// via `.onReceive`. PassthroughSubject (not @Published) so
    /// repeated clicks on the same heading re-trigger the scroll
    /// without needing a counter.
    let scrollAnchorRequest = PassthroughSubject<String, Never>()

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
    private let externalOpener: (URL) -> Bool
    /// Preferences persistence (math + code panels, #224). Injected
    /// at init so tests can substitute a non-standard `UserDefaults`.
    let preferencesStore: PreferencesStore

    init(
        recentsStore: RecentVaultsStore? = nil,
        externalOpener: @escaping (URL) -> Bool = { NSWorkspace.shared.open($0) },
        preferencesStore: PreferencesStore = PreferencesStore()
    ) {
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
        // Load persisted preferences AFTER the store is set, BEFORE
        // any other init work that might consume them. The didSet
        // observers haven't been installed yet (they only run on
        // assignment after init), so loading here doesn't recurse.
        self.mathPrefs = preferencesStore.loadMathPrefs()
        self.codePrefs = preferencesStore.loadCodePrefs()
        self.recentVaults = self.recentsStore.load()

        // Watch `selectedFilePath` and (re)trigger note loading on
        // every change. Combine's removeDuplicates avoids reloading
        // when the same path is rebound (e.g. by SwiftUI list
        // diffing). The closure runs on the main actor since the
        // class is @MainActor.
        $selectedFilePath
            .removeDuplicates()
            .sink { [weak self] path in
                self?.handleSelectionChange(to: path)
            }
            .store(in: &subscriptions)

        // Search query debouncer. 150 ms matches the acceptance
        // criteria and is short enough that typing feels live but
        // long enough that fast typists don't fire one query per
        // keystroke.
        searchQuerySubject
            .debounce(for: .milliseconds(150), scheduler: DispatchQueue.main)
            .removeDuplicates()
            .sink { [weak self] query in
                self?.runSearch(query: query)
            }
            .store(in: &subscriptions)
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
    func toggleSearchOverlay() {
        if isSearchOpen {
            closeSearchOverlay()
        } else {
            isSearchOpen = true
        }
    }

    /// Close the overlay and cancel any in-flight search. Keep
    /// `searchQuery` so a Cmd+F → Esc → Cmd+F round trip lands
    /// back where the user was.
    func closeSearchOverlay() {
        isSearchOpen = false
        cancelInFlightSearch()
        searchState = .idle
        searchSummary = ""
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
        if trimmed.isEmpty {
            cancelInFlightSearch()
            searchState = .idle
            searchSummary = ""
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
                            scope: .vault,
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
            // query.
            guard let self else { return }
            if Task.isCancelled || self.searchCancelToken !== cancel {
                return
            }
            switch outcome {
            case .success(let rs):
                self.searchState = .results(rows: rs.rows, summary: rs.summary)
                self.searchSummary = rs.summary
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
        // Dirty-state gate (issue #63): switching files while the
        // editor has unsaved changes must not silently drop the
        // user's edits. Park the requested destination in
        // `pendingNavigation` and let the "Save changes?" alert
        // route the actual transition.
        if hasUnsavedChanges, path != loadedFilePath {
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
        noteLoadTask?.cancel()
        noteLoadTask = nil
        linksLoadTask?.cancel()
        linksLoadTask = nil
        tasksLoadTask?.cancel()
        tasksLoadTask = nil
        // Audit #257 M1: orphaned content-pipeline tasks would
        // otherwise keep grabbing the session mutex after the user
        // switched files, serialising the new selection's loads
        // behind their FFI bodies. Cancel them eagerly so rapid
        // file switching doesn't pile up serialized work.
        mathBlocksLoadTask?.cancel()
        mathBlocksLoadTask = nil
        codeBlocksLoadTask?.cancel()
        codeBlocksLoadTask = nil
        diagramBlocksLoadTask?.cancel()
        diagramBlocksLoadTask = nil
        // Red-team M1+M2: cancel pending refresh-after-save tasks
        // too — same conn-mutex contention argument, plus this
        // avoids a race where the orphan refresh's old-prefs
        // result lands AFTER a prefs-driven reload publishes the
        // new-prefs blocks.
        mathBlocksRefreshTask?.cancel()
        mathBlocksRefreshTask = nil
        codeBlocksRefreshTask?.cancel()
        codeBlocksRefreshTask = nil
        diagramBlocksRefreshTask?.cancel()
        diagramBlocksRefreshTask = nil
        currentNoteText = nil
        savedBaselineText = nil
        currentNoteContentHash = nil
        loadedFilePath = nil
        hasUnsavedChanges = false
        currentSaveConflict = nil
        saveError = nil
        currentNoteHeadings = []
        noteLoadError = nil
        linksLoadError = nil
        tasksLoadError = nil
        guard let path else {
            // Full clear when nothing is selected. Safe here because
            // there's no destination note to attribute stale content
            // to — and `closeVault` / explicit deselect callers expect
            // the panels to drop their contents synchronously.
            currentBacklinks = []
            currentOutgoingLinks = []
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
        currentNoteEmbedResolutions = [:]
        // Drop any open embed-preview popover too — its target may
        // not exist in the new file's embed set.
        pendingEmbedPreview = nil
        // Content pipelines: same synchronous-clear reasoning as
        // embeds. The blocks carry rendered MathML / token streams /
        // SVG bytes for the previous file's body — holding them
        // during a transition would render the prior file's content
        // inside panels labelled as the new file's. Audit #203
        // logic, repeated.
        currentNoteMathBlocks = []
        currentNoteCodeBlocks = []
        currentNoteDiagramBlocks = []
        currentNoteCitations = []
        currentNoteCitationRefs = []
        expandedCitation = nil
        noteLoadTask = Task { [weak self] in
            await self?.loadCurrentNote(path: path)
        }
        linksLoadTask = Task { [weak self] in
            await self?.loadCurrentLinks(path: path)
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
        // Capture the query that produced this hit ahead of the
        // await — by the time the load resolves the user may have
        // edited the field, but the line we want to scroll to is
        // the one matching the query that was active when they
        // pressed Return on this row.
        let queryForLineLookup = searchQuery

        // Close the overlay first so focus moves cleanly back to
        // the content area before the file load completes.
        closeSearchOverlay()

        let wasAlreadyOpen = selectedFilePath == hit.path
        if !wasAlreadyOpen {
            selectedFilePath = hit.path
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
            postAccessibilityAnnouncement(
                "Opened \(filename), line \(line): \(cleanSnippet)"
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
        navigate(to: path, kind: "Opened")
    }

    /// Activate a backlink row from the BacklinksPanel — navigates
    /// to the source file that linked here. Backlinks are always
    /// resolved (the query joins on resolved target_path), so this
    /// is the simple `navigate(to:)` path.
    func openBacklink(_ backlink: Backlink) {
        navigate(to: backlink.sourcePath, kind: "Opened backlink to")
    }

    /// Shared post-activation step: update `selectedFilePath` (which
    /// the file-list selection binding + the note-load observer both
    /// pick up) and post an immediate audio confirmation so the user
    /// hears that the click worked before the content load finishes.
    private func navigate(to path: String, kind: String) {
        selectedFilePath = path
        let filename = (path as NSString).lastPathComponent
        postAccessibilityAnnouncement("\(kind) \(filename).")
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
            currentSession = session
            currentVaultURL = url
            lastError = nil
            // Reset file-list state so the previous vault's contents
            // don't briefly flash in the new vault's sidebar.
            files = []
            scanError = nil
            selectedFilePath = nil
            scanProgress = nil
            scanAnnouncementCount = 0
            scanAnnouncementLastMessage = nil
            scanAnnouncementLastFiredAt = .distantPast
            recordOpened(url: url)
            scanTask?.cancel()
            scanTask = Task { [weak self] in
                await self?.loadFiles()
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
                Task { [weak self] in
                    await self?.refreshAvailableCslStyles()
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

    /// Open a recent-vaults entry. If the path no longer exists on
    /// disk, do *not* try to open it (which would either fail with
    /// InvalidPath or, on older bugs, silently materialize a vault) —
    /// instead surface the entry through `missingRecentVault` so the
    /// UI can offer removal.
    func openRecent(_ entry: RecentVault) {
        let url = URL(fileURLWithPath: entry.path)
        var isDir: ObjCBool = false
        let exists = FileManager.default.fileExists(atPath: url.path, isDirectory: &isDir)
        if !exists || !isDir.boolValue {
            missingRecentVault = entry
            return
        }
        openVault(at: url)
    }

    /// Show the directory picker and, if the user chose a folder, open
    /// it as a vault. Centralizes the flow so the WelcomeView button
    /// and the App-level Cmd+O command share the same code path.
    ///
    /// `@MainActor` is redundant given the class-level annotation but
    /// is repeated here for self-documenting clarity: this method
    /// presents an `NSOpenPanel`, which AppKit requires on the main
    /// thread.
    @MainActor
    func pickAndOpenVault() {
        guard let url = VaultPicker.pick() else { return }
        openVault(at: url)
    }

    func closeVault() {
        scanTask?.cancel()
        scanTask = nil
        noteLoadTask?.cancel()
        noteLoadTask = nil
        linksLoadTask?.cancel()
        linksLoadTask = nil
        tasksLoadTask?.cancel()
        tasksLoadTask = nil
        vaultTasksLoadTask?.cancel()
        vaultTasksLoadTask = nil
        taskToggleTask?.cancel()
        taskToggleTask = nil
        // Audit #260: embed-resolution batch keeps grabbing the
        // session mutex post-closeVault if not cancelled. Race-guard
        // catches the stale write but the orphan task delays
        // session-deallocation.
        embedsLoadTask?.cancel()
        embedsLoadTask = nil
        isLoadingEmbeds = false
        closeSearchOverlay()
        searchQuery = ""
        // Reset transient sheet flags so a vault-close mid-palette
        // doesn't leave the bool stuck (next vault open would
        // auto-present the empty palette). #313 belt-and-suspenders
        // with the menu's `.disabled(!isVaultOpen)` gate.
        isCommandPaletteOpen = false
        currentSession = nil
        currentVaultURL = nil
        files = []
        scanError = nil
        selectedFilePath = nil
        currentNoteText = nil
        currentNoteHeadings = []
        noteLoadError = nil
        currentBacklinks = []
        currentOutgoingLinks = []
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
        vaultTasks = []
        vaultTasksLoadError = nil
        // #160 pagination state — reset alongside the rest of the
        // review-surface bookkeeping so reopening on a different
        // vault doesn't carry forward a cursor from the old one.
        vaultTasksNextCursor = nil
        vaultTasksTotalFiltered = 0
        isLoadingMoreVaultTasks = false
        taskReviewFilter = .all
        isTasksReviewOpen = false
        isScanning = false
        isLoadingNote = false
        isLoadingLinks = false
        isLoadingTasks = false
        isLoadingVaultTasks = false
        scanProgress = nil
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
        defer { isScanning = false }

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
                self?.handleScanProgress(event)
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
                            filter: .markdownOnly,
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
            guard !Task.isCancelled else { return }
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
            guard !Task.isCancelled else { return }
            scanError = humanReadable(error)
        } catch {
            guard !Task.isCancelled else { return }
            scanError = error.localizedDescription
        }
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
        isLoadingNote = true

        // Capture (text, headings, contentHash) in one detached
        // task. The hash comes from `get_file_metadata.contentHash`
        // — that's the same hash the scanner cached, so it matches
        // what's on disk right now (modulo external writes between
        // the scan and this load, which `read_text` would have
        // surfaced as an error or stale-content situation). The
        // save-flow uses this hash as `expected_content_hash` so a
        // mid-edit external write is caught as `WriteConflict`.
        let result: Result<(String, [Heading], String?), VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                let text = try session.readText(path: path)
                // get_file_metadata returns nil when the path isn't in
                // the index yet (race: file showed up between scan and
                // selection). Empty headings is the right fallback —
                // the outline pane shows its "no headings" empty state
                // and the user still sees the content. A nil hash is
                // fine too: subsequent saves will pass nil as
                // expected_content_hash, which means "save
                // unconditionally" — the right behavior when we
                // don't have a known baseline.
                let metadata = try session.getFileMetadata(path: path)
                return .success((text, metadata?.headings ?? [], metadata?.contentHash))
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
        guard !Task.isCancelled, selectedFilePath == path else { return }

        switch result {
        case .success(let (text, headings, contentHash)):
            currentNoteText = text
            savedBaselineText = text
            currentNoteContentHash = contentHash
            loadedFilePath = path
            hasUnsavedChanges = false
            currentNoteHeadings = headings
            noteLoadError = nil
        case .failure(let error):
            currentNoteText = nil
            savedBaselineText = nil
            currentNoteContentHash = nil
            loadedFilePath = nil
            hasUnsavedChanges = false
            currentNoteHeadings = []
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
            currentNoteProperties = properties
            linksLoadError = nil
        case .failure(let error):
            currentBacklinks = []
            currentOutgoingLinks = []
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
        let embedTargets: [String] = currentOutgoingLinks
            .filter { $0.isEmbed }
            .map(embedTargetKey)
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
                    do {
                        let resolution = try session.resolveEmbed(
                            hostPath: path,
                            target: target
                        )
                        out[target] = resolution
                    } catch let error as VaultError {
                        // Per-embed failure: synthesize an
                        // Unresolved entry instead of bailing out
                        // of the whole batch (audit #202).
                        out[target] = .unresolved(
                            reason: .readError(message: Self.humanReadableVaultError(error))
                        )
                    } catch {
                        out[target] = .unresolved(
                            reason: .readError(message: error.localizedDescription)
                        )
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
        guard let session = currentSession else { return }
        let sources = newPrefs.sources
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
    func requestEmbedPreview(target: String, sourceLine: Int? = nil) {
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
            let attempted = (try? session.readText(path: path)) ?? ""
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
            let text: String? = await Task.detached(priority: .userInitiated) {
                try? session.readText(path: path)
            }
            .value
            guard let self, let text else { return }
            guard self.loadedFilePath == path else { return }
            self.currentNoteText = text
            self.savedBaselineText = text
            self.hasUnsavedChanges = false
        }
    }

    /// Open the vault-wide Tasks Review surface (Cmd+Shift+T or
    /// toolbar). Kicks off the initial `loadVaultTasks` query and
    /// posts a polite VoiceOver announcement so screen-reader
    /// users know the surface opened.
    func openTasksReview() {
        guard currentSession != nil else { return }
        isTasksReviewOpen = true
        vaultTasksLoadTask = Task { [weak self] in
            await self?.loadVaultTasks()
        }
        postAccessibilityAnnouncement(
            "Tasks review opened. \(taskReviewFilter.displayName).",
            priority: .medium
        )
    }

    /// Close the review surface. Idempotent; safe to call from the
    /// sheet's onDismiss without an existence check.
    func closeTasksReview() {
        isTasksReviewOpen = false
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
        guard let session = currentSession else { return }
        let filter = taskReviewFilter.toFFIFilter()
        let activeFilter = taskReviewFilter
        isLoadingVaultTasks = true
        // #159: clear the spinner on EVERY exit path. The primary
        // stuck case was `closeTasksReview()` mid-load — it cancels
        // the task but doesn't reset the flag, so the cancelled
        // path's early `guard` return leaked it true forever.
        // `defer` runs on MainActor at function exit regardless of
        // which return arm we hit.
        defer { isLoadingVaultTasks = false }

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
        return task
    }

    private func performLoadMoreVaultTasks(
        session: VaultSession,
        filter: TaskFilter,
        cursor: String,
        activeFilter: TaskReviewFilter
    ) async {
        defer { isLoadingMoreVaultTasks = false }
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

    /// Activate a `TasksReviewView` row: switch the file
    /// selection (if needed) and scroll the editor to the task's
    /// line. Closes the review sheet so the user lands on the
    /// editor. Same selection+scroll pattern as the search-overlay
    /// activation flow.
    func openTaskRowInEditor(_ row: TaskWithLocation) {
        let target = row.path
        let line = Int(row.task.line)
        closeTasksReview()

        // If we're already on the file, just scroll.
        if selectedFilePath == target {
            lineScrollRequest.send(line)
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
            self.lineScrollRequest.send(line)
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
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performSave(
                session: session,
                path: path,
                contents: contents,
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
        expectedHash: String?
    ) async {
        // Detached so the SQLite-mutex-holding `save_text` doesn't
        // pin the main actor while disk IO + tree rewrites run.
        let outcome: Result<SaveReport, VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                let report = try session.saveText(
                    path: path,
                    contents: contents,
                    expectedContentHash: expectedHash
                )
                return .success(report)
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        // The user could have switched files (or closed the vault)
        // while we were saving. Drop the result in that case
        // rather than mutating state for a file the user has
        // already moved on from.
        guard loadedFilePath == path else {
            isSaving = false
            return
        }

        switch outcome {
        case .success(let report):
            currentNoteContentHash = report.newContentHash
            savedBaselineText = contents
            hasUnsavedChanges = false
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
            await self?.performSave(
                session: session,
                path: conflict.path,
                contents: conflict.attemptedContents,
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
                }
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        // If the user navigated away mid-edit, drop the result
        // rather than mutating state for a file the user has
        // already moved on from. Same shape as `performSave`.
        guard loadedFilePath == path else {
            isEditingProperty = false
            return
        }

        switch outcome {
        case .success(let report):
            currentNoteContentHash = report.newContentHash
            // Refresh the properties panel so the row updates in
            // place. `loadCurrentLinks` already runs one trip
            // through the SQLite mutex for backlinks + outgoing +
            // properties — reusing it keeps the panel coherent
            // without a second round-trip.
            await loadCurrentLinks(path: path)
            postAccessibilityAnnouncement(
                "Property \(key) \(action == .delete ? "deleted" : "updated").",
                priority: .medium
            )
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
            propertyEditError = humanReadable(error)
            postAccessibilityAnnouncement(
                "Property edit failed: \(propertyEditError ?? "")",
                priority: .high
            )
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
            self.currentNoteHeadings = headings ?? []
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

    private func filename(of path: String) -> String {
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
                _ = try session.saveText(
                    path: relativePath,
                    contents: rendered.body,
                    expectedContentHash: nil
                )
                return .success(rendered)
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        switch outcome {
        case .success(let rendered):
            pendingTemplateFlow = .idle
            templateNoteNameError = nil
            announceTemplate(
                "Created \(filename(of: relativePath)) from \(template.name)."
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
                    self.cursorByteOffsetRequest.send(Int(offset))
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
    private func announceTemplate(_ message: String) {
        templateAnnouncementLastMessage = message
        postAccessibilityAnnouncement(message, priority: .medium)
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

    private func humanReadable(_ error: VaultError) -> String {
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
