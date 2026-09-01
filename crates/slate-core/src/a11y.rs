// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Canonical accessibility-event vocabulary (W0.5-3, #719).
//!
//! Every screen-reader announcement Slate makes is a typed event here:
//! kind, parameters, priority, and the **message template** all live
//! core-side, so the mac (`postAccessibilityAnnouncement`) and Windows
//! (`RaiseNotificationEvent`, §W-D) hosts speak the same rendered text
//! from the same event. Hosts own *when* an event fires (trigger
//! conditions stay at the interaction sites — WGA-7); this module owns
//! *what it says* and *how urgently*.
//!
//! ## Scope and the `HostComposed` residue
//!
//! The W0.5-3 inventory (148 call expressions across 30 files at
//! execution time) migrates in treatment classes. Literal, templated,
//! and simple-builder announcements are typed variants below — their
//! Swift string originals are deleted. A minority of sites relay text
//! composed by dedicated engines (the GRAPH announcer's verbosity
//! machinery, Bases result summaries, filename advisories) or by
//! availability logic whose copy serves double duty in dialogs/hints;
//! those post [`A11yEvent::HostComposed`] carrying their text verbatim,
//! each call site marked `// W0.5-3 residue:` with the owning engine.
//! A source census (mac: `A11yResidueCensusTests`) pins the marker/site
//! count so residue shrinks deliberately (engine-level vocabularies are
//! follow-on batches) and keeps direct string-primitive calls at zero,
//! so no NEW announcement can bypass the vocabulary.
//!
//! The CANVAS announcer was such an engine and is no longer: W6-1 PR 0a
//! gave it the typed family below and Task 0a-2 moved the mac host onto
//! it, so `CanvasAnnouncer` posts rendered events and its residue site
//! is gone (the mac census drops 30 → 29). The graph announcer is the
//! remaining named engine, and W6-2 does for it what 0a did here. One
//! shared residue site survives that the canvas migration could not
//! delete: the structural-mutation builder the mac reaches through
//! `postMutationAnnouncement`, which serves every authoring surface —
//! the canvas call sites left it, the marker stays.
//!
//! ## Copy rules
//!
//! Templates are the shipped mac strings, moved verbatim — this issue
//! deliberately does not redesign wording or verbosity policy. Plain
//! en-US in V1 (#264 owns localisation). Chord placeholders render
//! per-platform (program decision 12): the few templates that carry a
//! chord take the host's DISPLAY chord string as a parameter
//! (`undo_chord`, `new_card_chord`, `palette_chord` — mac `"⌘Z"`,
//! Windows `"Ctrl+Z"`), because the chord table is host-owned. The
//! §W-D census normalizes those parameters; they are the one recorded
//! platform difference in the corpus. Key NAMES that are spelled
//! identically on both platforms (`Return`, `Escape`) stay literal in
//! the template — they are not chords.

use crate::canvas::CanvasColor;
use crate::canvas::model::EdgeDirection;
use crate::canvas::placement::RelativeDesc;

/// How urgently a host should speak an event. `High` interrupts
/// current speech (assertive); `Medium` queues politely — mirroring the
/// two `NSAccessibilityPriorityLevel`s the mac app uses and the
/// equivalent Windows notification processing levels.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum A11yPriority {
    Medium,
    High,
}

/// One announcement, as data. Rendering ([`A11yEvent::render`]) and
/// priority ([`A11yEvent::priority`]) are canonical; hosts post the
/// rendered pair through their platform notifier verbatim.
/// The structure kind a reading-view navigation command searched for
/// (W3-1, gap_analysis G21). Core owns the SPOKEN NAME of each kind so
/// the host never composes announcement fragments (WGA-7 boundary).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ReadingNavTarget {
    Heading,
    /// A specific level, 1-6 (the `1`..`6` chords).
    HeadingLevel {
        level: u8,
    },
    Link,
    List,
    Table,
    Embed,
    CodeBlock,
    /// W3-2: math blocks (the `M` chord).
    Math,
    /// W3-3: diagram blocks (the `D` chord).
    Diagram,
}

impl ReadingNavTarget {
    fn spoken(&self) -> String {
        use ReadingNavTarget::*;
        match self {
            Heading => "heading".to_owned(),
            HeadingLevel { level } => format!("level {level} heading"),
            Link => "link".to_owned(),
            List => "list".to_owned(),
            Table => "table".to_owned(),
            Embed => "embed".to_owned(),
            CodeBlock => "code block".to_owned(),
            Math => "math".to_owned(),
            Diagram => "diagram".to_owned(),
        }
    }
}

// ---------------------------------------------------------------------------
// Canvas announcement vocabulary (W6-1 PR 0a, #745 — t0 §1 grammar,
// #518). The whole grammar used to live in Swift: a 248-line
// `CanvasAnnouncer` whose twelve cases were eight free-text
// passthroughs, plus ~140 prose call sites composing the sentences at
// the interaction site. It moves here verbatim so both hosts speak one
// canvas. The enums below are the CLOSED parameter sets the templates
// switch on; open-ended payload (titles, labels, group paths, file
// paths, URL hosts, OS error detail, host chord strings, counts and
// dimensions) stays `String`/number, and no variant carries a whole
// sentence.
//
// ## Coalescing class keys (host-side timing, ONE list)
//
// Timing stays with the hosts — a pure render has no clock — but the
// class keys are pinned here so mac and Windows collapse identical
// bursts (t0 §1.5; mac `CanvasAnnouncer.EventClass`, 200 ms
// latest-wins, each class independent):
//
// - **`navigation`** — [`CanvasA11yEvent::CanvasMovedTo`],
//   [`CanvasA11yEvent::CanvasGroupEntered`], [`CanvasA11yEvent::CanvasGroupLeft`],
//   [`CanvasA11yEvent::CanvasConnectionTraversed`],
//   [`CanvasA11yEvent::CanvasMoveRelative`],
//   [`CanvasA11yEvent::CanvasResizeGeometry`].
// - **`filter`** — [`CanvasA11yEvent::CanvasFilterCount`],
//   [`CanvasA11yEvent::CanvasFilterCleared`].
// - Everything else posts immediately, uncoalesced.
// - A canvas event whose [`CanvasA11yEvent::priority`] is
//   [`A11yPriority::High`] FLUSHES both pending classes and DROPS
//   them (never posts them): the error supersedes, and navigation
//   context is re-derivable by moving again.

/// Canvas announcement verbosity (t0 §1.2). Deliberately a PARAMETER
/// on the two families that vary — the moved-to family and the
/// destructive family — rather than module state: core stays pure and
/// each host owns its own persisted, live-switchable preference.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasVerbosity {
    Terse,
    Standard,
    Verbose,
}

/// An overlap transition crossed by a transient move/resize step (t4
/// G20: silent stacking is invisible to a non-visual author).
/// Deliberately NOT its own event: mac appends it as a clause to the
/// geometry line so the coalescer emits one utterance, and two
/// utterances would be a behaviour change (contracts doc 0a-D2).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasOverlapTransition {
    Onset,
    Cleared,
}

/// A canvas mode (t0 §2). Core owns each mode's spoken NAME and its
/// exit instructions; the host supplies only the object acted on.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasMode {
    Move,
    Resize,
    Connect,
}

impl CanvasMode {
    /// The mode's spoken name — also the lead of the M3 inspectable
    /// container value and of the Where-am-I mode clause.
    fn name(&self) -> &'static str {
        match self {
            CanvasMode::Move => "Move mode",
            CanvasMode::Resize => "Resize mode",
            CanvasMode::Connect => "Connect mode",
        }
    }

    /// The bare verb, for the lifecycle sentences that do not say
    /// "mode" (`Move cancelled.`, `Resize ended — …`).
    fn verb(&self) -> &'static str {
        match self {
            CanvasMode::Move => "Move",
            CanvasMode::Resize => "Resize",
            CanvasMode::Connect => "Connect",
        }
    }

    /// The M1 exit instructions. `Return`/`Escape` are KEY NAMES, not
    /// chords — spelled identically on both platforms — so they stay
    /// literal here (unlike the undo hint, which takes the host's
    /// display chord as a parameter).
    fn exits(&self) -> &'static str {
        match self {
            CanvasMode::Move => {
                "Arrows to move, Shift for big steps, Return to place, Escape to cancel."
            }
            CanvasMode::Resize => {
                "Left and Right arrows change width, Up and Down change height, \
                 Return to apply, Escape to cancel."
            }
            CanvasMode::Connect => {
                "Navigate to the target with the usual movements, Return to connect, \
                 Escape to cancel."
            }
        }
    }
}

/// What a mode acts on (t0 §2 M1). Move mode takes the marked set as a
/// rigid unit, so the object is either one titled card or a count.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CanvasModeObject {
    Card { title: String },
    Cards { count: u32 },
}

/// The two modes that hold transient geometry. Connect is absent by
/// design: it has no rects, so `Placed …`/`Resized …` is only ever
/// spoken for these two.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasTransientVerb {
    Move,
    Resize,
}

/// What a cancelled mode put back (t0 §2 M2: Esc restores prior state
/// and says so). `None` is the degenerate path where the document went
/// away before the restore could run.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CanvasModeRestoration {
    /// Cancelled without a restoration statement — the degenerate
    /// path where the document went away before the restore could
    /// run. (Named `Unstated` rather than `None` so the Swift
    /// binding cannot collide with `Optional.none`.)
    Unstated,
    CardsReturned {
        count: u32,
    },
    SizeRestored,
    BackAt {
        title: String,
    },
}

/// The two single-card structural placements that share one template
/// (`⟨Verb⟩ "title" ⟨relative⟩.`).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasPlaceVerb {
    Moved,
    Duplicated,
}

/// Where a card's target was handed off to (t5 #525).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasOpenTarget {
    DefaultApp,
    Browser,
}

/// A resize preset (t4 #521). Core owns the spoken label; the host
/// owns the geometry (Fit to Content is a recorded placeholder
/// formula shared by both hosts — owner decision D-5).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasResizePreset {
    DefaultSize,
    FitToContent,
}

/// The three canvas projections (t2 #369).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasSurfaceKind {
    Outline,
    Table,
    Visual,
}

/// What produced a zoom announcement (#520): a bare zoom step carries
/// no context, while Fit Canvas and Zoom to Selection each prefix
/// their own sentence.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasZoomContext {
    FitCanvas,
    ZoomedToSelection,
}

/// What a destructive confirmation removed (t0 §1.3). Four arms
/// because four different tails ship, not because the verb varies —
/// deleting a group is spoken as *ungrouping* because the cards
/// survive.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CanvasDeleteTarget {
    Card {
        kind_label: String,
        title: String,
    },
    Group {
        label: String,
    },
    Cards {
        count: u32,
    },
    /// The connection's own reference, spoken as the picker row reads
    /// it lower-cased into the sentence (`to "Ideas", labelled
    /// "supports"`). Structural rather than a pre-lowered string:
    /// mac lower-cased the whole row, which mangled the author's card
    /// title and label (contracts doc 0a-D5).
    Connection {
        direction: EdgeDirection,
        other_title: String,
        label: Option<String>,
    },
}

/// The canvas verbs that wrap a backend failure in the shipped
/// `⟨Verb⟩ failed: ⟨detail⟩` sentence. A closed set, not prose: the
/// only open-ended part is the OS/FFI `detail`.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasFailedAction {
    NewCard,
    NewGroup,
    NewCanvas,
    MoveIntoGroup,
    Placement,
    Align,
    Create,
    RemoveFromGroup,
    Duplicate,
    CreateConnectedCard,
    CanvasAction,
    WhereAmI,
}

impl CanvasFailedAction {
    fn verb(&self) -> &'static str {
        match self {
            CanvasFailedAction::NewCard => "New card",
            CanvasFailedAction::NewGroup => "New group",
            CanvasFailedAction::NewCanvas => "New canvas",
            CanvasFailedAction::MoveIntoGroup => "Move",
            CanvasFailedAction::Placement => "Placement",
            CanvasFailedAction::Align => "Align",
            CanvasFailedAction::Create => "Create",
            CanvasFailedAction::RemoveFromGroup => "Remove",
            CanvasFailedAction::Duplicate => "Duplicate",
            CanvasFailedAction::CreateConnectedCard => "Create connected card",
            CanvasFailedAction::CanvasAction => "Canvas action",
            CanvasFailedAction::WhereAmI => "Where am I",
        }
    }
}

/// Why a canvas refuses mutations (the admission ladder). Closed
/// rather than prose because the SAME sentence serves the
/// announcement, every disabled command's reason, and the sheet's
/// read-only copy — three surfaces that drifted while it was a
/// host-side constant.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasMutationRefusal {
    Opening,
    Reopening,
    RetargetFailed,
    Unavailable,
    ReadOnly,
    CardEditorUnavailable,
}

/// Undo or redo — for the Edit-menu title, which is LABEL class, not
/// speech (§2 row G: the title is rendered here so both hosts compose
/// it identically from core's action name).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasHistoryVerb {
    Undo,
    Redo,
}

impl CanvasHistoryVerb {
    /// The menu-title base.
    fn base(&self) -> &'static str {
        match self {
            CanvasHistoryVerb::Undo => "Undo",
            CanvasHistoryVerb::Redo => "Redo",
        }
    }

    /// The spoken past tense (t0 §1.3: undo/redo announce the op name).
    fn past(&self) -> &'static str {
        match self {
            CanvasHistoryVerb::Undo => "Undid",
            CanvasHistoryVerb::Redo => "Redid",
        }
    }
}

/// The polite "why that did nothing" sentences — preconditions,
/// navigation dead ends, and the empty-history notes. One closed set
/// because mac retyped the same fourteen sentences across ten files
/// (`Nothing selected.` alone appears sixteen times) and they are all
/// the same speech act at the same priority.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CanvasStatusNote {
    NothingSelected,
    NoMarks,
    NotAGroup,
    NotATextCard,
    NotAFileCard,
    NoGroups,
    NoNotesInVault,
    NoMediaInVault,
    NoFilesToPointAt,
    OnlyTextCardsConvert,
    NoConnections,
    PickOutsideMovingSet,
    PickDifferentTarget,
    NoChanges,
    /// Where-am-I on a document that never opened cleanly.
    NotReadable,
    /// Where-am-I with no rows — deliberately NOT the onboarding copy,
    /// which advertises the create chord.
    Empty,
    EndOfCanvas,
    StartOfCanvas,
    AtCanvasLevel,
    NoCardsMatchFilter,
    NothingToUndo,
    NothingToRedo,
    GroupIsEmpty {
        label: String,
    },
    NoOutgoingPath {
        title: String,
    },
    NotInAGroup {
        title: String,
    },
    /// Follow-connection found nothing. `ordinal` is `None` when the
    /// card has no connection in that direction at all, and `Some(n)`
    /// when it has some but not an nth.
    NoConnection {
        forward: bool,
        ordinal: Option<u32>,
    },
    /// A structural READ verb — enter group, exit group, trace path,
    /// fit canvas — asked while the canvas is between a physical move
    /// landing and its background reopen. Those verbs answer from
    /// queries that need the native handle, and in that window the
    /// handle is deliberately detached so nothing can save through the
    /// moved-away path, so there is no answer to give.
    ///
    /// **New copy, not a migrated string** (W6-1 0b-2 fix round 2).
    /// The alternative was silence, and t0's never-silent principle
    /// says a keypress that does nothing must say so: the window is
    /// transient but user-reachable, and the canvas commands carry no
    /// enablement predicate (rule R1). Distinct from
    /// [`CanvasMutationRefusal::Reopening`], which is the same window's
    /// WRITE refusal and keeps its "before making changes" tail — this
    /// one is reached by a user who changed nothing and is told when to
    /// try again instead.
    Reopening,
    /// The same read verbs, on a canvas that is still LOADING — a first
    /// open, or a prepared replacement installed over an already-open
    /// tab (`beginPreparedReplacement`). The host's `LoadState` is
    /// `loading`, `ready`, `degraded`, `failed`, `retargetFailed`, and
    /// this covers the first; the window `Reopening` names is a REopen,
    /// so its copy would be false the first time a canvas is opened.
    ///
    /// Same shape as its sibling for the same reason (W6-1 0b-2, codex
    /// round 1): distinct from `CanvasMutationRefusal::Opening`, which
    /// covers this state for WRITES and keeps its "before making
    /// changes" tail.
    Loading,
}

/// The assertive refusals and failures that are not the
/// `⟨Verb⟩ failed: ⟨detail⟩` family (t0 §1.5: errors are assertive).
/// One closed set so a new canvas error cannot inherit the polite
/// default by omission.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CanvasBlockedReason {
    /// An out-of-band mutation (apply, undo, redo, convert) refused
    /// while a move or resize holds transient geometry.
    ModeBusy,
    UndoBlocked,
    RedoBlocked,
    LinkOpenFailed,
    AlignWouldOverlap,
    NotAUrl,
    CardTextUnreadable,
    NotePathMustEndInMd,
    NoFreeSpaceInGroup {
        label: String,
    },
    /// Convert-to-note collision. `on_disk` distinguishes the cheap
    /// snapshot bail from the backend's create-if-absent refusal — two
    /// different sentences, because only the second proves it.
    NotePathExists {
        path: String,
        on_disk: bool,
    },
    NoteReadFailed {
        message: String,
    },
    NoteCreateFailed {
        path: String,
        message: String,
    },
    /// The partial-failure arm: the note landed, the card did not
    /// follow. Naming the created file is the whole point.
    NoteRetargetFailed {
        path: String,
        message: String,
    },
    HeadingNotFound {
        heading: String,
        filename: String,
    },
    ReopenFailed {
        message: String,
    },
}

/// The filter clause Where-am-I discloses (t0 §1.4). One spelling —
/// t0's `⟨matched⟩ of ⟨total⟩ shown` — replaces the two mac shipped
/// (contracts doc 0a-D3).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasFilterState {
    Inactive,
    Active { matched: u32, total: u32 },
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum A11yEvent {
    // --- Regions, panes, tabs, workspace (U4) ---
    /// ⌘⌥← entered the file-tree region.
    FilesRegionFocused,
    /// A right-pane leaf was shown or the leaf region was entered —
    /// entering and switching read identically by design.
    LeafPanelShown {
        title: String,
    },
    /// Focus moved to an editor pane (⌘⌥ arrows / pane cycling).
    EditorPaneFocused {
        ordinal: u32,
        total: u32,
        title: String,
        /// Optional lead-in (e.g. `"Left pane. "`); empty for none.
        prefix: String,
    },
    /// The active tab changed (⌘⇧] / ⌘⇧[ / ⌘1…9).
    TabFocused {
        prefix: String,
        filename: String,
        index: u32,
        count: u32,
    },
    TabClosed {
        closed_title: String,
        successor: Option<String>,
    },
    NoSplitPanesToResize,
    PaneResized {
        percent: u32,
    },
    GraphOpensSinglePane,
    RightPaneShown,
    RightPaneHidden,
    HistoryPanelShown,

    // --- Reopen (⌘⇧T) ---
    ReopenTargetMissing {
        filename: String,
    },
    ReopenedFile {
        filename: String,
    },
    ReopenedNamed {
        name: String,
    },
    ReopenedGraph,

    // --- Vault lifecycle, gates, welcome ---
    VaultOpened {
        vault_title: String,
        /// Suffix notice about sidebar state; empty for none. Composed
        /// by the host today (advisory copy) — refined in the sweep.
        sidebar_notice: String,
    },
    RemovedRecentVault {
        display_name: String,
    },
    /// The welcome screen appeared (no vault open). The recent-vault
    /// count appends its own sentence when nonzero.
    WelcomeShown {
        recent_vault_count: u32,
    },
    CommandPaletteNeedsVault,
    SearchNeedsVault,

    // --- Links, search, embeds, headings, navigation ---
    /// The search panel's result-count summary. This is the §W-D
    /// anchor for `search_db::summary_for`, which renders THROUGH it —
    /// the count is the data, the wording lives here once, and the
    /// spoken and displayed strings cannot drift apart.
    SearchResultsSummary {
        count: u32,
    },
    /// A search that failed, carrying the host's human-readable
    /// reason (the same shape as the other `{ reason }` failures).
    SearchFailed {
        message: String,
    },
    SearchResultOpened {
        filename: String,
        line: u32,
        snippet: String,
    },
    ExternalLinkUnsupported {
        target: String,
    },
    ExternalLinkOpened,
    ExternalLinkFailed {
        target: String,
    },
    LinkUnresolved {
        target: String,
    },
    HelpOpened,
    HelpFailed,
    /// Internal navigation: `kind` is the verb the host chose
    /// ("Opened", "Showing", …) — the observed set is pinned by the
    /// census.
    InternalNavigated {
        kind: String,
        filename: String,
    },
    CitationNotLoaded,
    NoResolvedEmbedAtCursor,
    NoEmbedAtCursor,
    HeadingNotFound,
    HeadingScrollFailed {
        heading: String,
    },
    ScrolledToHeading {
        heading: String,
    },
    ScrolledToLine {
        filename: String,
        line: u32,
    },
    OpenedAtLine {
        filename: String,
        line: u32,
    },
    /// Plain open echo (no line target). Renders the same words as an
    /// `InternalNavigated { kind: "Opened", .. }` but stays Medium: this is
    /// a routine action confirmation, not an interrupt-worthy navigation.
    OpenedFile {
        filename: String,
    },
    ShowingNote {
        display_name: String,
    },

    // --- Tasks ---
    TaskToggleUnsaved {
        filename: String,
    },
    TaskToggleConflict {
        filename: String,
    },
    TasksReviewShown {
        filter_name: String,
    },
    TasksFilterSet {
        filter_name: String,
    },

    // --- Saves ---
    NoteSaved {
        filename: String,
    },
    SaveConflict {
        filename: String,
    },

    // --- History restore (O-3) ---
    RestoredVersionFrom {
        formatted_date: String,
    },
    RestoredFile {
        filename: String,
    },
    RestoredFileAs {
        source_name: String,
        filename: String,
    },

    // --- Print (#869) ---
    PrintNeedsNote,
    PrintDialogOpened {
        name: String,
    },

    // --- Sidebar batch actions + settings lifecycle ---
    /// Preflight start for a multi-item sidebar action. The count is
    /// pre-formatted by the host (locale digit grouping is a
    /// per-platform concern, like chord rendering — decision 12).
    BatchCheckStarted {
        formatted_count: String,
        action_name: String,
    },
    SelectionCopied,
    SidebarSettingsStillDefaults {
        detail: String,
    },
    SidebarSettingsReloadedStaleRefs,
    SidebarSettingsReloaded,

    // --- Vault close ---
    VaultClosed,
    VaultClosedAllSaved,
    VaultClosedChangesDiscarded,

    // --- Properties ---
    PropertiesUpdated,
    PropertyChanged {
        key: String,
        deleted: bool,
    },
    PropertyEditConflict {
        filename: String,
    },
    PropertiesSourceRejected {
        reason: String,
    },
    PropertyEditFailed {
        detail: String,
    },
    PropertiesReloaded,
    PropertiesReloadedBodyChanged,
    /// The note changed again mid-edit; `detail` is the stored error
    /// when one exists, else the canonical fallback renders.
    NoteChangedAgain {
        detail: Option<String>,
    },
    PropertiesReloadFailed {
        reason: String,
    },
    PropertyRetainedCopied,
    PropertyRecoveryUnverified {
        display_name: String,
    },
    PropertyRetainedDiscarded,
    PropertyRetainedReapplyFailed {
        detail: Option<String>,
    },
    PropertyReloadStillFailed {
        reason: String,
    },
    PropertyLoadCurrentFailed {
        reason: String,
    },
    AddPropertySheetShown,
    SourceChangesDiscarded,
    BulkRenameSheetShown,
    RenameReloadFailed {
        detail: Option<String>,
    },
    RenameFailed {
        detail: String,
    },
    /// One-line bulk-rename outcome (also the rename sheet's footer
    /// copy — the mac builder delegates here so the two can't drift).
    /// `applied == false` is the dry-run preview, where `renamed`
    /// carries the will-rename count and `failed` is unused.
    RenameSummary {
        applied: bool,
        renamed: u32,
        skipped: u32,
        failed: u32,
    },
    DuplicateFilesOnly,

    // --- Settings / preference toggles ---
    MathSpeechStyle {
        name: String,
    },
    MathVerbosity {
        name: String,
    },
    MathBrailleCode {
        name: String,
    },
    CodePreambleVerbosity {
        name: String,
    },
    EditorTextSize {
        percent: u32,
    },
    SpellCheckToggled {
        enabled: bool,
    },
    CitationStyleChanged {
        title: String,
    },

    // --- Counts and selection echoes ---
    CitationsCount {
        count: u32,
    },
    OutlineCount {
        count: u32,
    },
    FileListCount {
        count: u32,
    },
    ItemsSelected {
        count: u32,
    },
    NoItemsSelected,
    TreeFolderSelected {
        name: String,
    },
    RowSelected {
        name: String,
    },
    /// Quick-switcher result counts (#963, the #718 core-rendered
    /// follow-up): the opening recents count, the no-match case, and
    /// the per-keystroke match count. Strings moved verbatim from
    /// `QuickSwitcherModel.refreshAnnouncement`.
    SwitcherRecentCount {
        count: u32,
    },
    SwitcherNoMatches {
        query: String,
    },
    SwitcherMatchCount {
        count: u32,
        query: String,
    },
    /// Palette selection echo: the command label, plus its
    /// unavailability reason when the row is disabled (the reason
    /// copy is host availability logic, carried as data).
    PaletteCommandSelected {
        label: String,
        disabled_reason: Option<String>,
    },
    /// Palette filter feedback: how many commands the query matched.
    /// `count == 0` is the no-match phrasing.
    PaletteFilterCount {
        count: u32,
        query: String,
    },
    /// A palette command that ran and failed. `detail == None` is the
    /// defensive branch where the registry gave no message.
    PaletteCommandFailed {
        label: String,
        detail: Option<String>,
    },
    PaletteCommandNotFound {
        id: String,
    },
    /// An availability REJECTION, spoken verbatim: the reason is host
    /// availability copy carried as data (the `PaletteCommandSelected`
    /// precedent) and the row already exposes this exact sentence, so
    /// prefixing it would make VoiceOver say it twice differently.
    PaletteCommandUnavailable {
        reason: String,
    },
    RecentSearchFocused {
        query: String,
    },
    /// Quick-switcher result count. `query == None` is the opening,
    /// recents-first list; a present query is the filtered state.
    QuickSwitcherCount {
        count: u32,
        query: Option<String>,
    },

    // --- Bases ---
    BaseViewMode {
        mode: String,
    },
    BaseViewSwitcher {
        view_count: u32,
    },
    BasesNewQueryBuilder,
    BasesEditingFilters {
        view_name: String,
    },
    BasesFiltersOpenFailed {
        detail: String,
    },
    BasesPreviewFailed {
        detail: String,
    },
    BasesBuilderSaved,
    BasesViewSaveFailed {
        detail: String,
    },
    BasesSavedQueryNameNeeded,
    BasesSavedQueryCreated {
        name: String,
    },
    BasesSavedQueryCreateFailed {
        detail: String,
    },
    BasesSavedQueryUpdated {
        name: String,
    },
    BasesSavedQueryUpdateFailed {
        detail: String,
    },
    BasesViewSelected {
        name: String,
    },
    BasesSortSaveFailed {
        detail: String,
    },
    BaseRefreshed,
    /// The Bases "where am I" readback, composed from PARTS so the
    /// joining and the optional clauses live here rather than in the
    /// host: "Base: X", plus ", view: Y" and ", quick filter: Z" when
    /// those are present.
    BaseWhereAmI {
        base: String,
        view: Option<String>,
        quick_filter: Option<String>,
    },
    /// The results popover. `audio_summary` is core-composed already
    /// (`bases::engine::audio_summary`) and carried as data; the
    /// readback is appended only while a quick filter is active.
    BaseResultsPopover {
        audio_summary: String,
        where_am_i: Option<String>,
    },
    /// Quick-filter result count, "{shown} of {total} result(s)".
    /// Both Bases document types built this string identically and
    /// separately; the counts are the data.
    BaseQuickFilterResult {
        shown: u64,
        total: u64,
    },
    /// Keyboard row reorder (Option+Arrow) in the Bases query builder
    /// and the dashboard editor. ONE host builder composed all three
    /// sentences for three call sites — sort rows, column rows and
    /// dashboard sections — so the row's label and its position are the
    /// data and the wording lives here.
    BaseRowReorderRefused {
        label: String,
    },
    /// The row is already at the end it was asked to move toward.
    BaseRowReorderAtBoundary {
        label: String,
        /// True at the top of the list ("first"), false at the bottom
        /// ("last"). The host does not get a say in the noun.
        at_first: bool,
    },
    /// A completed move. `position` is 1-BASED, exactly as spoken.
    BaseRowReorderMoved {
        label: String,
        moved_up: bool,
        position: u64,
        count: u64,
    },
    /// The query builder's preview readback — one variant per state the
    /// host branched on.
    BaseQueryPreviewIdle,
    BaseQueryPreviewLoading,
    /// `first_result` is present only when the top row carries a
    /// non-blank description; the host decides that (it is a data
    /// question), the joining lives here.
    ///
    /// The join no longer doubles the period. `bases::engine::
    /// audio_summary` always terminates with `.`, and the shipped mac
    /// code appended `". First result: …"` to it, so users heard
    /// "12 notes.. First result: Alpha" for as long as the readback has
    /// existed. #969 moved the string verbatim and pinned the defect in
    /// a golden; this is the deliberate follow-up that fixes it.
    ///
    /// The separator is chosen from the summary rather than assumed:
    /// terminal punctuation gets a single space, anything else gets
    /// ". ", so a caller that passes an unterminated summary still
    /// produces one well-formed sentence.
    BaseQueryPreviewReady {
        audio_summary: String,
        first_result: Option<String>,
    },
    /// Distinct from `BasesPreviewFailed` ("Base preview failed: …"):
    /// this is the BUILDER's shorter sentence, kept verbatim rather
    /// than folded into its neighbour.
    BaseQueryPreviewFailed {
        detail: String,
    },
    /// A transient column sort, and the same sort persisted into the
    /// view. The two sentences differ deliberately — the transient one
    /// carries NO terminal period, the saved one does. Preserved as
    /// shipped; #969 moves strings, it does not redesign copy.
    BaseSortedByColumn {
        column: String,
        ascending: bool,
    },
    BaseSortSavedToView {
        column: String,
        ascending: bool,
    },
    /// The saved-query action family. These all reached AT through the
    /// host's `postBaseActionAnnouncement(String)` funnel; #969 gives
    /// each its own case so the Windows twin renders the same sentence
    /// rather than re-typing it.
    ///
    /// Note the near-neighbours that are deliberately NOT reused:
    /// `BasesSavedQueryNameNeeded` is "…before saving.",
    /// `BasesSavedQueryRenameNameNeeded` is "…before renaming.".
    BasesSavedQueryReferenceMissing {
        reference: String,
    },
    BasesSavedQueryMissing,
    BasesQueriesRefreshFailed {
        detail: String,
    },
    BasesSavedQueryEditing {
        name: String,
    },
    BasesSavedQueryEditFailed {
        detail: String,
    },
    BasesSavedQueryRenameNameNeeded,
    BasesSavedQueryRenamed {
        name: String,
    },
    BasesSavedQueryRenameFailed {
        detail: String,
    },
    BasesSavedQueryDeleted,
    BasesSavedQueryDeleteFailed {
        detail: String,
    },
    BasesSavedQueryExportPathNeeded,
    BasesSavedQueryExported {
        name: String,
    },
    BasesSavedQueryExportFailed {
        detail: String,
    },
    BasesPathOutsideVault,
    /// The dashboard action family — the second group behind the same
    /// `postBaseActionAnnouncement` funnel.
    BasesDashboardNameNeeded,
    BasesDashboardSaved {
        name: String,
    },
    BasesDashboardSaveFailed {
        detail: String,
    },
    BasesDashboardUpdated {
        name: String,
    },
    BasesDashboardUpdateFailed {
        detail: String,
    },
    /// The section moved under the editor's feet — a stale-index
    /// refusal, not a failure with a reason to relay.
    BasesDashboardSectionStale,
    BasesDashboardSectionRemoveFailed {
        detail: String,
    },
    BasesDashboardSectionReplaceFailed {
        detail: String,
    },
    BasesDashboardDeleted,
    BasesDashboardDeleteFailed {
        detail: String,
    },
    BasesDashboardEditFailed {
        detail: String,
    },
    BasesDashboardMissing,
    /// The last group behind the `postBaseActionAnnouncement` funnel:
    /// row actions, clipboard/export, and cell editing. With these the
    /// funnel is deleted and the Bases family is fully converted.
    BasesDockUpdatedForNote,
    BasesLinkCopied {
        name: String,
    },
    BasesBacklinksFor {
        name: String,
    },
    BasesViewCopyNoActiveBase,
    BasesViewCopiedAsMarkdown,
    BasesViewCopyFailed {
        detail: String,
    },
    BasesRowSelectionNeeded,
    BasesNoEditableProperty,
    /// Why a cell refuses editing. `file_metadata` picks the noun; the
    /// host used to choose between two literals.
    BasesCellReadOnly {
        file_metadata: bool,
    },
    BasesCellSaved {
        column: String,
        value: String,
    },
    BasesCellCleared {
        column: String,
    },
    /// Deliberately has NO terminal period — preserved as shipped.
    BasesCellRowNoLongerMatches,
    BasesCellEditFailed {
        detail: String,
    },
    BasesCellEditCanceled,
    BasesViewExported,
    BasesViewExportFailed {
        detail: String,
    },
    BasesDataviewConverted,
    /// Distinct from `DataviewConversionFailed` ("Dataview conversion
    /// failed: …"): this one is the SAVE step of the conversion.
    BasesDataviewConversionSaveFailed {
        detail: String,
    },
    /// The quick-filter scope prompt, dismissed. `verb` is the caller's
    /// action word ("Copy", "Export") and stays data — the prompt is
    /// reused by both.
    BasesQuickFilterChoiceCanceled {
        verb: String,
    },
    /// Cell-edit validation refusals. These reached AT through the same
    /// funnel via `BaseCellEditValidationError.message`.
    BasesCellMustBeFiniteNumber,
    BasesCellMustBeWholeNumber,
    BasesCellMustBeFiniteDecimal,
    BasesCellMustBeBoolean,
    BasesCellMustBeDate,
    /// A visible base whose row membership changed under a background
    /// refresh. `audio_summary` is core-composed already
    /// (`bases::engine::audio_summary`); only the "Updated: " prefix
    /// was host-side.
    BasesRefreshUpdated {
        audio_summary: String,
    },
    DataviewConversionFailed {
        detail: String,
    },

    // --- One-offs ---
    CitationInsertUnavailable,
    CitationWalkThrough,
    CodeCopied,

    /// Text composed by a host-side engine that has not yet been given
    /// its own vocabulary (see module docs). Carries its priority as
    /// data because the composing engines post at differing levels.
    /// Every producing call site is marked `// W0.5-3 residue:`.
    // --- Reading view structural navigation (W3-1, G21) ---
    /// A chorded reading-navigation command found no target in the
    /// requested direction. The LANDING case is deliberately not an
    /// event: moving the caret makes the AT speak the landing line
    /// itself, and a second notification would double-speak.
    ReadingNavNoTarget {
        target: ReadingNavTarget,
        forward: bool,
    },
    /// A chorded reading-navigation command landed. Measured 2026-07-27
    /// (NVDA 2026.1.1): a PROGRAMMATIC caret move produces no speech —
    /// NVDA echoes lines only for keys it recognizes as caret movement —
    /// so the landing must be announced, and core owns the phrasing.
    /// `text` is the landing target's own document text, captured by the
    /// host at the landmark (content, not composition).
    ReadingNavLanded {
        target: ReadingNavTarget,
        text: String,
    },

    // --- Accessible data grid (W4-1, the #969 grid announce family) ---
    /// A grid column sort was applied (keyboard or header click).
    GridSorted {
        column: String,
        ascending: bool,
    },
    /// Vertical navigation landed on a DIFFERENT row: the engine's row
    /// `audio_description` deduplicated against the focused cell label
    /// (spoken alone when it already contains the cell, case-folded;
    /// mac's pre-conversion rule also folded diacritics -- the core
    /// rule is case-only, a recorded refinement both twins now share).
    GridRowMoved {
        description: String,
        focused_cell: String,
    },
    /// Horizontal / within-row navigation: the "Header: value" label.
    GridCellMoved {
        column: String,
        value: String,
    },
    /// A group heading row.
    GridGroup {
        label: String,
        row_count: u32,
        summary: Option<String>,
    },

    // --- Templates (W5-3, #743) ---
    /// The template picker presented with its enumeration result. The
    /// three count arms are mac's `templatePickerOpenAnnouncement`
    /// verbatim (the 0 arm is carried for completeness; mac's empty
    /// present speaks its availability reason instead — contracts doc
    /// T10).
    TemplatePickerOpened {
        count: u32,
    },
    /// Create-from-template succeeded. High priority (mac #421 F-H1:
    /// the created announcement must win over the tab-switch
    /// announcement that immediately follows the open).
    TemplateNoteCreated {
        name: String,
        template: String,
    },

    /// The whole canvas announcement family (W6-1 0a, #745), nested
    /// so one engine costs ONE top-level variant. uniffi caps an enum
    /// at 256 variants and this vocabulary was at 197 before canvas;
    /// a flat family would have spent a fifth of the remaining budget
    /// on one surface and left none for the graph announcer (the other
    /// named residue engine). Nesting per engine is the pattern every
    /// later family copies.
    Canvas {
        event: CanvasA11yEvent,
    },

    HostComposed {
        text: String,
        priority: A11yPriority,
    },
}

/// Everything the canvas may say out loud (t0 §1 grammars), reached
/// through [`A11yEvent::Canvas`]. Its own enum rather than 51 more
/// top-level variants: see that variant's note for why, and the
/// section comment above for the closed parameter sets and the
/// coalescing class keys.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CanvasA11yEvent {
    // --- Canvas: selection and navigation (W6-1 0a, #745; t0 §1.2) ---
    /// Selection landed on a card. The one event whose template is a
    /// verbosity MATRIX (t0 §1.2): terse speaks the bare title for
    /// rapid arrowing, standard adds the card reference and the
    /// n-of-m fix, verbose adds connections, colour, and the mark.
    /// `container` is the innermost group label; `None` speaks the
    /// literal lowercase word `canvas`.
    ///
    /// `color_name` stays a `String` — unlike
    /// [`CanvasA11yEvent::CanvasColorSet`]'s typed `CanvasColor` —
    /// because it is core's OWN `canvas::color_name` output arriving
    /// back through `CanvasOutlineRow.color_name`: the host relays it,
    /// it never derives it. Typing it here would force a host to parse
    /// a spoken name back into a colour, which is the re-derivation the
    /// typed payload exists to prevent (contracts doc 0a-11).
    CanvasMovedTo {
        verbosity: CanvasVerbosity,
        kind_label: String,
        title: String,
        ordinal_n: u32,
        total_m: u32,
        container: Option<String>,
        connection_count: u32,
        color_name: Option<String>,
        marked: bool,
    },
    /// Crossed INTO a group. `count` is the group's own card count —
    /// mac spoke the entered row's SIBLING count, a miscount this
    /// migration fixes (contracts doc 0a-D4).
    CanvasGroupEntered {
        label: String,
        count: u32,
    },
    CanvasGroupLeft {
        label: String,
    },
    /// Followed a connection. The direction phrase comes from the
    /// model's `EdgeDirection` (derived from `fromEnd`/`toEnd`), never
    /// from geometry; `kind_label`/`title` describe the card ARRIVED
    /// AT, so a group or file target is never introduced as a text
    /// card (Codoki #613).
    CanvasConnectionTraversed {
        direction: EdgeDirection,
        kind_label: String,
        title: String,
        label: Option<String>,
    },
    /// The whole traced chain in ONE utterance — mac deliberately does
    /// not narrate hop by hop, and the visited count is the tail of
    /// the same sentence, so a separate end event would double-speak.
    ///
    /// `titles` is the ONLY payload on purpose: the tail count is
    /// `titles.len()`, so the list and the number it claims can never
    /// disagree. Mac spoke `visited.count` while listing the titles it
    /// could resolve to outline rows — two numbers from one walk
    /// (contracts doc CD-13).
    CanvasTracePathEnd {
        titles: Vec<String>,
    },

    // --- Canvas: transient geometry (t4 #521; `navigation` class) ---
    /// Move-mode narration: the nearest-neighbour fix, coalesced so a
    /// held arrow announces the resting position. `descs` is core's
    /// own relative description list (empty = nothing to fix against);
    /// the first phrase is capitalised and the rest join lower-cased.
    CanvasMoveRelative {
        descs: Vec<RelativeDesc>,
        overlap: Option<CanvasOverlapTransition>,
    },
    /// Resize-mode narration. `preset` is `None` for an arrow step and
    /// names the preset when one was applied; both carry the overlap
    /// clause because a preset can land on another card too.
    CanvasResizeGeometry {
        preset: Option<CanvasResizePreset>,
        width: u32,
        height: u32,
        overlap: Option<CanvasOverlapTransition>,
    },
    /// A resize step refused at the minimum card size.
    CanvasResizeClamped,

    // --- Canvas: mode stack (t0 §2, M1-M7) ---
    /// M1 entry: name, object, exits — the three things a mode must
    /// disclose before it swallows the arrow keys.
    CanvasModeEntered {
        mode: CanvasMode,
        object: CanvasModeObject,
    },
    /// M7: a second mode was refused while one is active.
    CanvasModeRejected {
        active_mode: CanvasMode,
    },
    /// M2 commit that changed geometry.
    CanvasModeCommitted {
        verb: CanvasTransientVerb,
        object: CanvasModeObject,
    },
    /// M2 commit that wrote nothing — no ops for move/resize, no
    /// target for connect. The user must hear that rather than a
    /// false confirmation.
    CanvasModeEndedWithoutEffect {
        mode: CanvasMode,
    },
    /// M2 cancel: prior state restored, and the restoration named.
    CanvasModeCancelled {
        mode: CanvasMode,
        restoration: CanvasModeRestoration,
    },

    // --- Canvas: authoring confirmations (t0 §1.3) ---
    /// A card or group was created at a computed placement. One
    /// template for both: mac's group arm hand-rolled a string that
    /// was byte-identical to the builder's group rendering.
    CanvasCreated {
        kind_label: String,
        title: String,
        relative: RelativeDesc,
    },
    /// A `.canvas` FILE was created (the File-section verb), not a
    /// card on one.
    CanvasFileCreated {
        name: String,
    },
    /// The mind-mapping loop's compound verb: create + connect in one
    /// action, so it is one confirmation.
    CanvasConnectedCardCreated {
        relative: RelativeDesc,
        origin_title: String,
    },
    CanvasConnected {
        from_title: String,
        to_title: String,
        label: Option<String>,
    },
    CanvasConnectionUpdated {
        label: Option<String>,
    },
    CanvasMovedIntoGroup {
        label: String,
    },
    /// Names the group left behind — a payload-less variant could not
    /// render the shipped sentence.
    CanvasRemovedFromGroup {
        label: String,
    },
    /// The host hands over the colour it just SET — core's own
    /// [`CanvasColor`], not a name it phrased itself — and
    /// [`canvas::color_name`] does the phrasing here. `None` speaks
    /// the literal `no color` (the clear-colour arm). Typing the
    /// payload is what deletes mac's preset dictionaries: a host
    /// cannot spell `"red"` at this seam even by accident.
    CanvasColorSet {
        title: String,
        color: Option<CanvasColor>,
    },
    CanvasRenamedGroup {
        label: String,
    },
    CanvasCardUpdated {
        title: String,
    },
    /// Locate… repointed a file card at a new vault path.
    CanvasCardRetargeted {
        title: String,
        path: String,
    },
    /// The two single-card structural placements that share one
    /// template (Place Below/Above/Left Of/Right Of…, and Duplicate).
    CanvasCardPlaced {
        verb: CanvasPlaceVerb,
        title: String,
        relative: RelativeDesc,
    },
    CanvasCardAligned {
        title: String,
        target_title: String,
    },
    CanvasConvertedToNote {
        path: String,
    },

    // --- Canvas: destructive confirmations (t0 §1.3) ---
    /// The one destructive family. The undo hint rides at standard+
    /// only (terse users asked for minimum chrome), and `undo_chord`
    /// is the host's DISPLAY chord — the first chord-bearing template
    /// in this vocabulary.
    CanvasDeleted {
        target: CanvasDeleteTarget,
        verbosity: CanvasVerbosity,
        undo_chord: String,
    },

    // --- Canvas: bulk over the marked set (t0 §1.5: one summary) ---
    /// Four typed bulk variants, not one `{verb, count}`: the shipped
    /// tails are a relative description, a colour name, a group label,
    /// and a fixed clause — they do not share a shape. None of them
    /// carries the undo hint (only the destructive family does).
    CanvasBulkMoved {
        count: u32,
        relative: RelativeDesc,
    },
    /// Typed like [`CanvasA11yEvent::CanvasColorSet`] — the bulk verb
    /// hands over the same `CanvasColor` it wrote.
    CanvasBulkColorSet {
        count: u32,
        color: Option<CanvasColor>,
    },
    CanvasGrouped {
        count: u32,
        label: String,
    },
    CanvasBulkDuplicated {
        count: u32,
    },

    // --- Canvas: marks (t4 #524) ---
    CanvasMarkToggled {
        marked: bool,
        title: String,
        count: u32,
    },
    /// Clearing marks with nothing marked speaks the precondition
    /// instead — mac's ternary, kept inside the template.
    CanvasMarksCleared {
        count: u32,
    },

    // --- Canvas: filter (t5 #373; `filter` class) ---
    /// The debounced result count. Carries `matched` only: the
    /// m-of-n form is the static summary LABEL, not speech.
    CanvasFilterCount {
        matched: u32,
    },
    CanvasFilterCleared {
        total: u32,
    },

    // --- Canvas: viewport and surfaces (#520, #369) ---
    CanvasZoom {
        context: Option<CanvasZoomContext>,
        percent: u32,
    },
    CanvasFollowSelectionToggled {
        following: bool,
    },
    /// A viewport verb reached a document with NO addressable canvas
    /// view — no initiating renderer and no last owner (W6-1 §D D7,
    /// obligation ID-7): a restored, never-focused tab receiving Zoom
    /// from the palette. The presentation-address refusal is its own
    /// arm because the load-state mapping admits a Ready document and
    /// has nothing honest to say about panes.
    CanvasViewportNoPane,
    CanvasSurfaceShown {
        surface: CanvasSurfaceKind,
    },

    // --- Canvas: undo and redo (t3 #372) ---
    /// `Undid: ⟨name⟩` / `Redid: ⟨name⟩` — the op name is core's own
    /// `CanvasAction.name`, spoken verbatim.
    CanvasHistoryApplied {
        verb: CanvasHistoryVerb,
        name: String,
    },
    /// LABEL class, not speech: the Edit menu's item title. Only the
    /// leading character is upper-cased — core's action names embed
    /// user-typed card titles that must pass through verbatim.
    CanvasUndoMenuTitle {
        verb: CanvasHistoryVerb,
        name: String,
    },

    // --- Canvas: polite notes and assertive refusals ---
    /// A command that could not run, or a movement with nowhere to go.
    CanvasStatus {
        note: CanvasStatusNote,
    },
    /// The assertive refusals and failures outside the
    /// `⟨Verb⟩ failed:` family.
    CanvasBlocked {
        reason: CanvasBlockedReason,
    },
    /// The `⟨Verb⟩ failed: ⟨detail⟩` family. Twelve verbs ship; the
    /// detail is the OS/FFI message.
    CanvasActionFailed {
        action: CanvasFailedAction,
        detail: String,
    },
    /// t0 §5 save conflict: the file changed under us and nothing was
    /// applied. Its own variant because the whole conflict SURFACE
    /// (Reload / Overwrite / Save a Copy) keys off it.
    CanvasSaveConflict,
    /// A file card whose target is gone. `target` is the vault-relative
    /// path, falling back to the card title when the node carries no
    /// target at all.
    CanvasFileNotFound {
        target: String,
    },
    /// A file or link card handed off to the OS.
    CanvasOpened {
        title: String,
        target: CanvasOpenTarget,
    },

    // --- Canvas: admission (the mutation ladder) ---
    /// The six refusal reasons that used to bypass the canvas funnel
    /// entirely as host-composed text.
    CanvasMutationRefused {
        reason: CanvasMutationRefusal,
    },

    // --- Canvas: load states and Where-am-I (t0 §1.4, §5) ---
    /// t0 §5 tolerant-parse notice, polite. Mac ships this as a
    /// static banner only; t0 requires the announcement, so the
    /// event exists and mac gains the post.
    CanvasLoadedDegraded {
        skipped: u32,
    },
    /// The empty-canvas onboarding region — LABEL grade (region text,
    /// never spoken). Renders the spelled-out AX form, which is the
    /// one a screen reader gets; the glyph form stays a host label.
    CanvasEmptyOnboarding {
        new_card_chord: String,
        palette_chord: String,
    },
    /// t0 §1.4: the pull-based readback, ALWAYS verbose-grade
    /// regardless of the verbosity setting — which is why it takes no
    /// `verbosity` parameter. `color_name` is a relayed `String` for
    /// the same reason as [`CanvasA11yEvent::CanvasMovedTo`]'s: it
    /// arrives from `CanvasWhereAmI.color_name`, already core's.
    CanvasWhereAmI {
        kind_label: String,
        title: String,
        group_path: Vec<String>,
        ordinal_n: u32,
        total_m: u32,
        connection_count: u32,
        in_count: u32,
        out_count: u32,
        color_name: Option<String>,
        marked: bool,
        mode: Option<CanvasMode>,
        filter: CanvasFilterState,
    },
}

impl A11yEvent {
    /// The urgency this event is spoken at — pinned per variant (the
    /// shipped mac priorities, moved verbatim).
    pub fn priority(&self) -> A11yPriority {
        use A11yEvent::*;
        match self {
            CommandPaletteNeedsVault
            | PaletteCommandFailed { .. }
            | PaletteCommandNotFound { .. }
            | PaletteCommandUnavailable { .. }
            | InternalNavigated { .. }
            | TaskToggleUnsaved { .. }
            | PropertiesSourceRejected { .. }
            | PropertyEditFailed { .. }
            | PropertiesReloadedBodyChanged
            | NoteChangedAgain { .. }
            | PropertiesReloadFailed { .. }
            | PropertyRecoveryUnverified { .. }
            | PropertyRetainedReapplyFailed { .. }
            | PropertyReloadStillFailed { .. }
            | PropertyLoadCurrentFailed { .. }
            | AddPropertySheetShown
            | BulkRenameSheetShown
            | RenameReloadFailed { .. }
            | RenameFailed { .. }
            | RestoredVersionFrom { .. }
            | RestoredFile { .. }
            | RestoredFileAs { .. }
            | TemplateNoteCreated { .. } => A11yPriority::High,
            // The canvas family owns its own tiering (t0 §1.5).
            Canvas { event } => event.priority(),
            HostComposed { priority, .. } => *priority,
            _ => A11yPriority::Medium,
        }
    }

    /// The canonical spoken text — the shipped mac strings, verbatim.
    pub fn render(&self) -> String {
        use A11yEvent::*;
        match self {
            FilesRegionFocused => "Files.".to_owned(),
            LeafPanelShown { title } => format!("{title} panel."),
            EditorPaneFocused {
                ordinal,
                total,
                title,
                prefix,
            } => {
                format!("{prefix}Editor pane {ordinal} of {total}, {title}.")
            }
            TabFocused {
                prefix,
                filename,
                index,
                count,
            } => {
                format!("{prefix} {filename}, tab {index} of {count}.")
            }
            TabClosed {
                closed_title,
                successor,
            } => match successor {
                Some(successor) => format!("Closed {closed_title}. {successor} is active."),
                None => format!("Closed {closed_title}."),
            },
            NoSplitPanesToResize => "No split panes to resize.".to_owned(),
            PaneResized { percent } => format!("Pane resized, {percent} percent."),
            GraphOpensSinglePane => {
                "The graph opens in a single pane. Split from a note instead.".to_owned()
            }
            RightPaneShown => "Right pane shown.".to_owned(),
            RightPaneHidden => "Right pane hidden.".to_owned(),
            HistoryPanelShown => "History panel.".to_owned(),

            ReopenTargetMissing { filename } => format!("{filename} no longer exists."),
            ReopenedFile { filename } => format!("Reopened {filename}."),
            ReopenedNamed { name } => format!("Reopened {name}."),
            ReopenedGraph => "Reopened Graph.".to_owned(),

            VaultOpened {
                vault_title,
                sidebar_notice,
            } => format!(
                "Vault {vault_title} opened. Scanning files for the sidebar.{sidebar_notice}"
            ),
            RemovedRecentVault { display_name } => {
                format!("Removed {display_name} from recent vaults.")
            }
            WelcomeShown { recent_vault_count } => {
                let base = "Welcome to Slate. Open Vault button focused. Press Return \
                            or Command-O to choose a folder of Markdown files.";
                if *recent_vault_count == 0 {
                    base.to_owned()
                } else {
                    format!(
                        "{base} {recent_vault_count} recent {} listed below.",
                        plural(*recent_vault_count, "vault", "vaults")
                    )
                }
            }
            CommandPaletteNeedsVault => "Open a vault to use the command palette.".to_owned(),
            SearchNeedsVault => "Open a vault first. Search works inside a vault.".to_owned(),
            SearchResultsSummary { count } => match *count {
                0 => "Search returned no results.".to_owned(),
                1 => "Search returned 1 result.".to_owned(),
                n => format!("Search returned {n} results."),
            },
            SearchFailed { message } => format!("Search error: {message}"),

            SearchResultOpened {
                filename,
                line,
                snippet,
            } => {
                format!("Opened {filename}, line {line}: {snippet}")
            }
            ExternalLinkUnsupported { target } => format!(
                "Cannot open external link {target}. Only web and mail links are supported."
            ),
            ExternalLinkOpened => "Opened external link in default browser.".to_owned(),
            ExternalLinkFailed { target } => format!("Could not open external link {target}."),
            LinkUnresolved { target } => format!("{target} is unresolved. Cannot open."),
            HelpOpened => "Opened Help in your default browser.".to_owned(),
            HelpFailed => "Could not open Help.".to_owned(),
            InternalNavigated { kind, filename } => format!("{kind} {filename}."),
            CitationNotLoaded => "Citation is not loaded yet.".to_owned(),
            NoResolvedEmbedAtCursor => "No resolved embed at cursor.".to_owned(),
            NoEmbedAtCursor => "No embed at cursor.".to_owned(),
            HeadingNotFound => "Could not find that heading.".to_owned(),
            HeadingScrollFailed { heading } => format!("Could not scroll to {heading}."),
            ScrolledToHeading { heading } => format!("Scrolled to {heading}."),
            ScrolledToLine { filename, line } => format!("Scrolled to {filename}, line {line}."),
            OpenedAtLine { filename, line } => format!("Opened {filename}, line {line}."),
            OpenedFile { filename } => format!("Opened {filename}."),
            ShowingNote { display_name } => format!("Showing {display_name}."),

            TaskToggleUnsaved { filename } => format!(
                "Cannot toggle task. The editor has unsaved changes in {filename}. \
                 Save the note first."
            ),
            TaskToggleConflict { filename } => format!(
                "Toggle blocked. {filename} was modified externally. Resolve in the dialog."
            ),
            TasksReviewShown { filter_name } => format!("Tasks review. {filter_name}."),
            TasksFilterSet { filter_name } => format!("Filter set to {filter_name}."),

            NoteSaved { filename } => format!("Saved {filename}."),
            SaveConflict { filename } => {
                format!("Save blocked. {filename} was modified externally. Resolve in the dialog.")
            }

            RestoredVersionFrom { formatted_date } => {
                format!("Restored version from {formatted_date}.")
            }
            RestoredFile { filename } => format!("Restored {filename}."),
            RestoredFileAs {
                source_name,
                filename,
            } => {
                format!("Restored {source_name} as {filename}.")
            }

            PrintNeedsNote => "Open a note to print.".to_owned(),
            PrintDialogOpened { name } => format!("Print dialog opened for {name}."),

            BatchCheckStarted {
                formatted_count,
                action_name,
            } => {
                format!("Checking {formatted_count} selected items before {action_name}.")
            }
            SelectionCopied => "Copied.".to_owned(),
            SidebarSettingsStillDefaults { detail } => {
                format!("Sidebar settings still use defaults. {detail}")
            }
            SidebarSettingsReloadedStaleRefs => {
                "Sidebar settings reloaded. Some pinned notes or sort overrides \
                 may still reference old locations."
                    .to_owned()
            }
            SidebarSettingsReloaded => "Sidebar settings reloaded.".to_owned(),

            VaultClosed => "Vault closed. Returned to the welcome screen.".to_owned(),
            VaultClosedAllSaved => {
                "All changes saved. Vault closed. Returned to the welcome screen.".to_owned()
            }
            VaultClosedChangesDiscarded => {
                "Changes discarded. Vault closed. Returned to the welcome screen.".to_owned()
            }

            PropertiesUpdated => "Properties updated.".to_owned(),
            PropertyChanged { key, deleted } => {
                let action = if *deleted { "deleted" } else { "updated" };
                format!("Property {key} {action}.")
            }
            PropertyEditConflict { filename } => format!(
                "Property edit blocked. {filename} was modified externally. \
                 Resolve in the dialog."
            ),
            PropertiesSourceRejected { reason } => {
                format!("Properties source not applied: {reason}")
            }
            PropertyEditFailed { detail } => format!("Property edit failed: {detail}"),
            PropertiesReloaded => "Properties reloaded.".to_owned(),
            PropertiesReloadedBodyChanged => {
                "Properties reloaded. The note body also changed externally; \
                 saving it will require conflict resolution."
                    .to_owned()
            }
            NoteChangedAgain { detail } => detail
                .clone()
                .unwrap_or_else(|| "The note changed again.".to_owned()),
            PropertiesReloadFailed { reason } => {
                format!("Properties could not be reloaded: {reason}")
            }
            PropertyRetainedCopied => "Retained property update copied.".to_owned(),
            PropertyRecoveryUnverified { display_name } => format!(
                "The saved property update in {display_name} could not be verified. \
                 Reopen the note to copy or resolve the retained update."
            ),
            PropertyRetainedDiscarded => {
                "Using the current saved properties. The retained update was discarded.".to_owned()
            }
            PropertyRetainedReapplyFailed { detail } => detail.clone().unwrap_or_else(|| {
                "The retained property update could not be reapplied.".to_owned()
            }),
            PropertyReloadStillFailed { reason } => {
                format!("Slate still could not reload the saved property update. {reason}")
            }
            PropertyLoadCurrentFailed { reason } => format!(
                "Slate couldn\u{2019}t load the current properties. \
                 The retained update is still available. {reason}"
            ),
            AddPropertySheetShown => "Add property".to_owned(),
            SourceChangesDiscarded => "Source changes discarded.".to_owned(),
            BulkRenameSheetShown => "Bulk rename property".to_owned(),
            RenameReloadFailed { detail } => detail
                .clone()
                .unwrap_or_else(|| "Some open notes could not be reloaded.".to_owned()),
            RenameFailed { detail } => format!("Rename failed: {detail}"),
            RenameSummary {
                applied,
                renamed,
                skipped,
                failed,
            } => {
                if *applied {
                    format!("{renamed} renamed, {skipped} skipped, {failed} failed.")
                } else {
                    format!(
                        "{renamed} {} will be renamed, {skipped} skipped, 0 errors.",
                        plural(*renamed, "file", "files")
                    )
                }
            }
            DuplicateFilesOnly => "Duplicate applies to files only.".to_owned(),

            MathSpeechStyle { name } => format!("Math speech style: {name}."),
            MathVerbosity { name } => format!("Math verbosity: {name}."),
            MathBrailleCode { name } => format!("Math braille code: {name}."),
            CodePreambleVerbosity { name } => format!("Code preamble verbosity: {name}."),
            EditorTextSize { percent } => format!("Editor text size {percent} percent."),
            SpellCheckToggled { enabled } => if *enabled {
                "Check spelling while typing on."
            } else {
                "Check spelling while typing off."
            }
            .to_owned(),
            CitationStyleChanged { title } => format!("Citation style: {title}."),

            CitationsCount { count } => {
                format!(
                    "Citations, {count} {}.",
                    plural(*count, "citation", "citations")
                )
            }
            OutlineCount { count } => {
                format!(
                    "Outline, {count} {}.",
                    plural(*count, "heading", "headings")
                )
            }
            FileListCount { count } => {
                format!("File list, {count} {}", plural(*count, "item", "items"))
            }
            ItemsSelected { count } => {
                format!("{count} {} selected", plural(*count, "item", "items"))
            }
            NoItemsSelected => "No items selected".to_owned(),
            TreeFolderSelected { name } => format!("Selected: {name}, folder"),
            RowSelected { name } => format!("Selected: {name}"),
            SwitcherRecentCount { count } => {
                format!("{count} recent {}", plural(*count, "file", "files"))
            }
            SwitcherNoMatches { query } => format!("No files matching \"{query}\""),
            SwitcherMatchCount { count, query } => format!(
                "{count} {} matching \"{query}\"",
                plural(*count, "file", "files")
            ),
            PaletteCommandSelected {
                label,
                disabled_reason,
            } => match disabled_reason {
                Some(reason) => format!("Selected: {label}. Unavailable: {reason}"),
                None => format!("Selected: {label}"),
            },
            PaletteFilterCount { count, query } => {
                if *count == 0 {
                    format!("No commands match \"{query}\"")
                } else {
                    format!(
                        "{count} {} matching \"{query}\"",
                        plural(*count, "command", "commands")
                    )
                }
            }
            PaletteCommandFailed { label, detail } => match detail {
                Some(detail) => format!("{label} failed: {detail}"),
                None => format!("{label} failed."),
            },
            PaletteCommandNotFound { id } => format!("Command not found: {id}"),
            PaletteCommandUnavailable { reason } => reason.clone(),
            RecentSearchFocused { query } => format!("Recent search: {query}"),
            QuickSwitcherCount { count, query } => match query {
                None => format!("{count} recent {}", plural(*count, "file", "files")),
                Some(query) if *count == 0 => format!("No files matching \"{query}\""),
                Some(query) => format!(
                    "{count} {} matching \"{query}\"",
                    plural(*count, "file", "files")
                ),
            },

            BaseViewMode { mode } => format!("Base view as {mode}."),
            BaseViewSwitcher { view_count } => format!(
                "Base view switcher. {view_count} {}.",
                plural(*view_count, "view", "views")
            ),
            BasesNewQueryBuilder => "New Bases query builder.".to_owned(),
            BasesEditingFilters { view_name } => format!("Editing filters for {view_name}."),
            BasesFiltersOpenFailed { detail } => {
                format!("Base filters could not be opened in the builder: {detail}")
            }
            BasesPreviewFailed { detail } => format!("Base preview failed: {detail}"),
            BasesBuilderSaved => "Saved builder changes to view.".to_owned(),
            BasesViewSaveFailed { detail } => format!("Base view could not be saved: {detail}"),
            BasesSavedQueryNameNeeded => "Enter a saved query name before saving.".to_owned(),
            BasesSavedQueryCreated { name } => format!("Saved query {name}."),
            BasesSavedQueryCreateFailed { detail } => {
                format!("Saved query could not be created: {detail}")
            }
            BasesSavedQueryUpdated { name } => format!("Updated saved query {name}."),
            BasesSavedQueryUpdateFailed { detail } => {
                format!("Saved query could not be updated: {detail}")
            }
            BasesViewSelected { name } => format!("Base view: {name}."),
            BasesSortSaveFailed { detail } => format!("Base sort could not be saved: {detail}"),
            BaseRefreshed => "Base refreshed.".to_owned(),
            BaseWhereAmI {
                base,
                view,
                quick_filter,
            } => {
                let mut parts = vec![format!("Base: {base}")];
                if let Some(view) = view {
                    parts.push(format!("view: {view}"));
                }
                if let Some(quick_filter) = quick_filter {
                    parts.push(format!("quick filter: {quick_filter}"));
                }
                parts.join(", ")
            }
            BaseResultsPopover {
                audio_summary,
                where_am_i,
            } => match where_am_i {
                Some(where_am_i) => format!("{audio_summary} {where_am_i}."),
                None => audio_summary.clone(),
            },
            BaseQuickFilterResult { shown, total } => format!(
                "{shown} of {total} {}",
                crate::sidebar_filter::noun(*total, "result", "results")
            ),
            BaseRowReorderRefused { label } => format!("{label} cannot be moved."),
            BaseRowReorderAtBoundary { label, at_first } => {
                format!(
                    "{label} is already {}.",
                    if *at_first { "first" } else { "last" }
                )
            }
            BaseRowReorderMoved {
                label,
                moved_up,
                position,
                count,
            } => format!(
                "{label} moved {} to position {position} of {count}.",
                if *moved_up { "up" } else { "down" }
            ),
            BaseQueryPreviewIdle => "Preview not loaded.".to_owned(),
            BaseQueryPreviewLoading => "Preview loading.".to_owned(),
            BaseQueryPreviewReady {
                audio_summary,
                first_result,
            } => match first_result {
                Some(first) => {
                    let head = audio_summary.trim_end();
                    let separator = if head.ends_with(['.', '!', '?']) {
                        " "
                    } else {
                        ". "
                    };
                    format!("{head}{separator}First result: {first}")
                }
                None => audio_summary.clone(),
            },
            BaseQueryPreviewFailed { detail } => format!("Preview failed: {detail}"),
            BaseSortedByColumn { column, ascending } => format!(
                "Sorted by {column}, {}",
                if *ascending {
                    "ascending"
                } else {
                    "descending"
                }
            ),
            BaseSortSavedToView { column, ascending } => format!(
                "Saved sort by {column}, {}.",
                if *ascending {
                    "ascending"
                } else {
                    "descending"
                }
            ),
            BasesSavedQueryReferenceMissing { reference } => {
                format!("Saved query {reference} is no longer available.")
            }
            BasesSavedQueryMissing => "Saved query is no longer available.".to_owned(),
            BasesQueriesRefreshFailed { detail } => {
                format!("Queries could not be refreshed: {detail}")
            }
            BasesSavedQueryEditing { name } => format!("Editing {name} in builder."),
            BasesSavedQueryEditFailed { detail } => {
                format!("Saved query could not be edited: {detail}")
            }
            BasesSavedQueryRenameNameNeeded => {
                "Enter a saved query name before renaming.".to_owned()
            }
            BasesSavedQueryRenamed { name } => format!("Renamed saved query to {name}."),
            BasesSavedQueryRenameFailed { detail } => {
                format!("Saved query could not be renamed: {detail}")
            }
            BasesSavedQueryDeleted => "Deleted saved query.".to_owned(),
            BasesSavedQueryDeleteFailed { detail } => {
                format!("Saved query could not be deleted: {detail}")
            }
            BasesSavedQueryExportPathNeeded => "Choose a .base path before exporting.".to_owned(),
            BasesSavedQueryExported { name } => format!("Exported saved query as {name}."),
            BasesSavedQueryExportFailed { detail } => {
                format!("Saved query could not be exported: {detail}")
            }
            BasesPathOutsideVault => "Choose a path inside the vault.".to_owned(),
            BasesDashboardNameNeeded => "Enter a dashboard name before saving.".to_owned(),
            BasesDashboardSaved { name } => format!("Saved dashboard {name}."),
            BasesDashboardSaveFailed { detail } => {
                format!("Dashboard could not be saved: {detail}")
            }
            BasesDashboardUpdated { name } => format!("Updated dashboard {name}."),
            BasesDashboardUpdateFailed { detail } => {
                format!("Dashboard could not be updated: {detail}")
            }
            BasesDashboardSectionStale => {
                "Dashboard section changed; reload and try again.".to_owned()
            }
            BasesDashboardSectionRemoveFailed { detail } => {
                format!("Dashboard section could not be removed: {detail}")
            }
            BasesDashboardSectionReplaceFailed { detail } => {
                format!("Dashboard section could not be replaced: {detail}")
            }
            BasesDashboardDeleted => "Deleted dashboard.".to_owned(),
            BasesDashboardDeleteFailed { detail } => {
                format!("Dashboard could not be deleted: {detail}")
            }
            BasesDashboardEditFailed { detail } => {
                format!("Dashboard could not be edited: {detail}")
            }
            BasesDashboardMissing => "Dashboard is no longer available.".to_owned(),
            BasesDockUpdatedForNote => "Base dock updated for active note.".to_owned(),
            BasesLinkCopied { name } => format!("Copied link to {name}."),
            BasesBacklinksFor { name } => format!("Backlinks for {name}."),
            BasesViewCopyNoActiveBase => {
                "Base view could not be copied: No active base.".to_owned()
            }
            BasesViewCopiedAsMarkdown => "Copied base view as Markdown.".to_owned(),
            BasesViewCopyFailed { detail } => {
                format!("Base view could not be copied: {detail}")
            }
            BasesRowSelectionNeeded => "Select a base row first.".to_owned(),
            BasesNoEditableProperty => {
                "No editable property is available for the selected row.".to_owned()
            }
            BasesCellReadOnly { file_metadata } => if *file_metadata {
                "read-only: file metadata"
            } else {
                "read-only: computed"
            }
            .to_owned(),
            BasesCellSaved { column, value } => format!("Saved. {column}: {value}"),
            BasesCellCleared { column } => format!("Saved. {column}: empty"),
            BasesCellRowNoLongerMatches => "Saved. Row no longer matches this view".to_owned(),
            BasesCellEditFailed { detail } => format!("Base edit failed: {detail}"),
            BasesCellEditCanceled => "Edit canceled.".to_owned(),
            BasesViewExported => "Exported base view.".to_owned(),
            BasesViewExportFailed { detail } => {
                format!("Base view could not be exported: {detail}")
            }
            BasesDataviewConverted => "Converted Dataview block to .base.".to_owned(),
            BasesDataviewConversionSaveFailed { detail } => {
                format!("Dataview conversion could not be saved: {detail}")
            }
            BasesQuickFilterChoiceCanceled { verb } => format!("{verb} canceled."),
            BasesCellMustBeFiniteNumber => "Must be a finite number.".to_owned(),
            BasesCellMustBeWholeNumber => "Must be a whole number.".to_owned(),
            BasesCellMustBeFiniteDecimal => "Must be a finite decimal number.".to_owned(),
            BasesCellMustBeBoolean => "Must be true or false.".to_owned(),
            BasesCellMustBeDate => "Date must be YYYY-MM-DD.".to_owned(),
            BasesRefreshUpdated { audio_summary } => format!("Updated: {audio_summary}"),
            DataviewConversionFailed { detail } => {
                format!("Dataview conversion failed: {detail}")
            }

            CitationInsertUnavailable => {
                "Insert citation lands in V1.x. See Milestone L.".to_owned()
            }
            CitationWalkThrough => {
                "Walk through citations. Switch to the Citations sidebar tab and \
                 arrow through the list."
                    .to_owned()
            }
            CodeCopied => "Code copied.".to_owned(),

            ReadingNavNoTarget { target, forward } => {
                let direction = if *forward { "next" } else { "previous" };
                format!("No {direction} {}.", target.spoken())
            }
            ReadingNavLanded { target, text } => {
                let text = text.trim();
                if text.is_empty() {
                    // Capitalized kind alone — an embed card with no name
                    // still announces as something.
                    let spoken = target.spoken();
                    let mut chars = spoken.chars();
                    match chars.next() {
                        Some(first) => {
                            format!("{}{}.", first.to_uppercase(), chars.as_str())
                        }
                        None => String::new(),
                    }
                } else if matches!(target, ReadingNavTarget::Embed) {
                    // Embed card names already carry their kind
                    // ("Embedded note X"); a suffix would stutter.
                    format!("{text}.")
                } else {
                    format!("{text}, {}.", target.spoken())
                }
            }

            GridSorted { column, ascending } => format!(
                "Sorted by {column}, {}",
                if *ascending {
                    "ascending"
                } else {
                    "descending"
                }
            ),
            GridRowMoved {
                description,
                focused_cell,
            } => {
                let description = description.trim();
                if description.is_empty() {
                    focused_cell.clone()
                } else if contains_field(description, focused_cell) {
                    description.to_owned()
                } else if description.ends_with('.') {
                    format!("{description} {focused_cell}")
                } else {
                    format!("{description}. {focused_cell}")
                }
            }
            GridCellMoved { column, value } => format!("{column}: {value}"),
            GridGroup {
                label,
                row_count,
                summary,
            } => {
                let rows = format!("{row_count} {}", plural(*row_count, "row", "rows"));
                match summary {
                    Some(summary) if !summary.is_empty() => {
                        format!("Group: {label}, {rows}. Summary: {summary}")
                    }
                    _ => format!("Group: {label}, {rows}"),
                }
            }

            TemplatePickerOpened { count } => match *count {
                0 => "Template picker opened. No templates found. \
                      Add a Markdown file to the configured template folder."
                    .to_owned(),
                1 => "Template picker opened. 1 template available.".to_owned(),
                n => format!("Template picker opened. {n} templates available."),
            },
            TemplateNoteCreated { name, template } => {
                format!("Created {name} from {template}.")
            }

            Canvas { event } => event.render(),

            HostComposed { text, .. } => text.clone(),
        }
    }
}

impl CanvasA11yEvent {
    /// The urgency this canvas event is spoken at. Exactly mac's
    /// `.error` case is `High` (t0 §1.5: "navigation = polite;
    /// errors/conflicts = assertive"); every other canvas event rode
    /// `.status`/`.confirmation`/`.mode`/`.bulk` and is `Medium`. The
    /// members are listed explicitly because the catch-all below makes
    /// a forgotten one silently polite.
    pub fn priority(&self) -> A11yPriority {
        use CanvasA11yEvent::*;
        match self {
            CanvasModeRejected { .. }
            | CanvasBlocked { .. }
            | CanvasActionFailed { .. }
            | CanvasSaveConflict
            | CanvasFileNotFound { .. } => A11yPriority::High,
            _ => A11yPriority::Medium,
        }
    }

    /// The canonical spoken text — the shipped mac strings, verbatim.
    pub fn render(&self) -> String {
        use CanvasA11yEvent::*;
        match self {
            CanvasMovedTo {
                verbosity,
                kind_label,
                title,
                ordinal_n,
                total_m,
                container,
                connection_count,
                color_name,
                marked,
            } => match verbosity {
                CanvasVerbosity::Terse => title.clone(),
                _ => {
                    let mut parts = vec![
                        card_ref(kind_label, title),
                        format!(
                            "{ordinal_n} of {total_m} in {}",
                            container.as_deref().unwrap_or("canvas")
                        ),
                    ];
                    if matches!(verbosity, CanvasVerbosity::Verbose) {
                        parts.push(format!(
                            "{connection_count} {}",
                            plural(*connection_count, "connection", "connections")
                        ));
                        if let Some(color_name) = color_name {
                            parts.push(color_name.clone());
                        }
                        if *marked {
                            parts.push("marked".to_owned());
                        }
                    }
                    parts.join(", ")
                }
            },
            CanvasGroupEntered { label, count } => format!(
                "Entering group \"{label}\", {count} {}",
                plural(*count, "card", "cards")
            ),
            CanvasGroupLeft { label } => format!("Leaving group \"{label}\""),
            CanvasConnectionTraversed {
                direction,
                kind_label,
                title,
                label,
            } => {
                let phrase = match direction {
                    EdgeDirection::Outgoing => "Connects to",
                    EdgeDirection::Incoming => "Connected from",
                    EdgeDirection::Bidirectional | EdgeDirection::Undirected => "Linked with",
                };
                let reference = card_ref(kind_label, title);
                match label {
                    Some(label) => format!("{phrase} {reference}, labelled \"{label}\""),
                    None => format!("{phrase} {reference}"),
                }
            }
            CanvasTracePathEnd { titles } => {
                let tail = format!(
                    "End of path — {} {} visited.",
                    titles.len(),
                    plural_len(titles.len(), "card", "cards")
                );
                if titles.is_empty() {
                    // The `Path: …` clause is omitted when it has no
                    // content, like every other optional clause here —
                    // `Path: . End of path — 0 cards visited.` is not a
                    // sentence. Unreachable from mac (the walk always
                    // seeds with the selected card and returns
                    // `NoOutgoingPath` below two), but the arm is total
                    // over the payload (contract 0a-14).
                    tail
                } else {
                    format!("Path: {}. {tail}", titles.join(", then "))
                }
            }

            CanvasMoveRelative { descs, overlap } => {
                let fix = if descs.is_empty() {
                    "Alone on the canvas".to_owned()
                } else {
                    descs
                        .iter()
                        .enumerate()
                        .map(|(index, desc)| {
                            let phrase = relative_phrase(desc);
                            if index == 0 {
                                capitalize_first(&phrase)
                            } else {
                                phrase
                            }
                        })
                        .collect::<Vec<_>>()
                        .join(", ")
                };
                format!("{fix}{}", overlap_clause(overlap))
            }
            CanvasResizeGeometry {
                preset,
                width,
                height,
                overlap,
            } => {
                let size = match preset {
                    None => format!("{width} by {height}"),
                    Some(CanvasResizePreset::DefaultSize) => {
                        format!("Resized to default size: {width} by {height}")
                    }
                    Some(CanvasResizePreset::FitToContent) => {
                        format!("Resized to fit to content: {width} by {height}")
                    }
                };
                format!("{size}{}", overlap_clause(overlap))
            }
            CanvasResizeClamped => "Minimum size.".to_owned(),

            CanvasModeEntered { mode, object } => format!(
                "{} — {}. {}",
                mode.name(),
                mode_object(object),
                mode.exits()
            ),
            CanvasModeRejected { active_mode } => format!(
                "{} is active. Return to commit or Escape to cancel first.",
                active_mode.name()
            ),
            CanvasModeCommitted { verb, object } => match verb {
                CanvasTransientVerb::Move => format!("Placed {}.", mode_object(object)),
                CanvasTransientVerb::Resize => format!("Resized {}.", mode_object(object)),
            },
            CanvasModeEndedWithoutEffect { mode } => match mode {
                // Connect's "no effect" is a different fact: nothing
                // was chosen, rather than nothing changed.
                CanvasMode::Connect => "Connect ended — no target chosen.".to_owned(),
                _ => format!("{} ended — nothing changed.", mode.verb()),
            },
            CanvasModeCancelled { mode, restoration } => {
                let head = format!("{} cancelled", mode.verb());
                match restoration {
                    CanvasModeRestoration::Unstated => format!("{head}."),
                    CanvasModeRestoration::CardsReturned { count } => {
                        format!("{head} — {} returned.", plural(*count, "card", "cards"))
                    }
                    CanvasModeRestoration::SizeRestored => format!("{head} — size restored."),
                    CanvasModeRestoration::BackAt { title } => {
                        format!("{head} — back at \"{title}\".")
                    }
                }
            }

            CanvasCreated {
                kind_label,
                title,
                relative,
            } => format!(
                "Created {} {}",
                lower_first(&card_ref(kind_label, title)),
                relative_phrase(relative)
            ),
            CanvasFileCreated { name } => format!("Created canvas \"{name}\"."),
            CanvasConnectedCardCreated {
                relative,
                origin_title,
            } => format!(
                "Created connected card {} — connected from \"{origin_title}\".",
                relative_phrase(relative)
            ),
            CanvasConnected {
                from_title,
                to_title,
                label,
            } => match label {
                Some(label) => {
                    format!("Connected \"{from_title}\" to \"{to_title}\", labelled \"{label}\".")
                }
                None => format!("Connected \"{from_title}\" to \"{to_title}\"."),
            },
            CanvasConnectionUpdated { label } => match label {
                Some(label) => format!("Connection updated, labelled \"{label}\"."),
                None => "Connection updated.".to_owned(),
            },
            CanvasMovedIntoGroup { label } => format!("Moved into group \"{label}\"."),
            CanvasRemovedFromGroup { label } => format!("Removed from group \"{label}\"."),
            CanvasColorSet { title, color } => {
                format!("Set \"{title}\" to {}.", spoken_color(color))
            }
            CanvasRenamedGroup { label } => format!("Renamed group to \"{label}\"."),
            CanvasCardUpdated { title } => format!("Updated \"{title}\"."),
            CanvasCardRetargeted { title, path } => {
                format!("\"{title}\" now points at {path}.")
            }
            CanvasCardPlaced {
                verb,
                title,
                relative,
            } => format!(
                "{} \"{title}\" {}.",
                match verb {
                    CanvasPlaceVerb::Moved => "Moved",
                    CanvasPlaceVerb::Duplicated => "Duplicated",
                },
                relative_phrase(relative)
            ),
            CanvasCardAligned {
                title,
                target_title,
            } => format!("Aligned \"{title}\" with \"{target_title}\"."),
            CanvasConvertedToNote { path } => {
                format!("Converted to note {path}. The card now points at it.")
            }

            CanvasDeleted {
                target,
                verbosity,
                undo_chord,
            } => {
                let body = match target {
                    CanvasDeleteTarget::Card { kind_label, title } => {
                        format!("Deleted {}", card_ref(kind_label, title))
                    }
                    CanvasDeleteTarget::Group { label } => {
                        format!("Ungrouped {} — cards kept", card_ref("group", label))
                    }
                    CanvasDeleteTarget::Cards { count } => {
                        format!("Deleted {}", counted(*count, "card", "cards"))
                    }
                    CanvasDeleteTarget::Connection {
                        direction,
                        other_title,
                        label,
                    } => {
                        let preposition = match direction {
                            EdgeDirection::Outgoing => "to",
                            EdgeDirection::Incoming => "from",
                            EdgeDirection::Bidirectional | EdgeDirection::Undirected => "with",
                        };
                        match label {
                            Some(label) => format!(
                                "Deleted connection {preposition} \"{other_title}\", \
                                 labelled \"{label}\""
                            ),
                            None => {
                                format!("Deleted connection {preposition} \"{other_title}\"")
                            }
                        }
                    }
                };
                match verbosity {
                    // The undo hint rides at standard+ (t0 §1.3);
                    // terse users asked for minimum chrome.
                    CanvasVerbosity::Terse => body,
                    _ => format!("{body} — {undo_chord} to undo"),
                }
            }

            CanvasBulkMoved { count, relative } => format!(
                "Moved {count} {} {}.",
                plural(*count, "card", "cards"),
                relative_phrase(relative)
            ),
            CanvasBulkColorSet { count, color } => format!(
                "Set {} to {}.",
                counted(*count, "card", "cards"),
                spoken_color(color)
            ),
            CanvasGrouped { count, label } => format!(
                "Grouped {} into \"{label}\".",
                counted(*count, "card", "cards")
            ),
            CanvasBulkDuplicated { count } => format!(
                "Duplicated {count} {} — one undo restores.",
                plural(*count, "card", "cards")
            ),

            CanvasMarkToggled {
                marked,
                title,
                count,
            } => format!(
                "{} \"{title}\". {count} marked.",
                if *marked { "Marked" } else { "Unmarked" }
            ),
            CanvasMarksCleared { count } => {
                if *count == 0 {
                    "No marks.".to_owned()
                } else {
                    format!("Cleared {}.", counted(*count, "mark", "marks"))
                }
            }

            CanvasFilterCount { matched } => {
                format!("{matched} {} match.", plural(*matched, "card", "cards"))
            }
            CanvasFilterCleared { total } => {
                format!("Filter cleared — {}.", counted(*total, "card", "cards"))
            }

            CanvasZoom { context, percent } => match context {
                None => format!("Zoom {percent} percent."),
                Some(CanvasZoomContext::FitCanvas) => {
                    format!("Fit canvas. Zoom {percent} percent.")
                }
                Some(CanvasZoomContext::ZoomedToSelection) => {
                    format!("Zoomed to selection. Zoom {percent} percent.")
                }
            },
            CanvasFollowSelectionToggled { following } => if *following {
                "Viewport follows selection."
            } else {
                "Viewport stays put."
            }
            .to_owned(),
            CanvasViewportNoPane => "No canvas view to act on.".to_owned(),
            CanvasSurfaceShown { surface } => format!(
                "Canvas {} view.",
                match surface {
                    CanvasSurfaceKind::Outline => "outline",
                    CanvasSurfaceKind::Table => "table",
                    CanvasSurfaceKind::Visual => "visual",
                }
            ),

            CanvasHistoryApplied { verb, name } => format!("{}: {name}", verb.past()),
            CanvasUndoMenuTitle { verb, name } => {
                if name.is_empty() {
                    verb.base().to_owned()
                } else {
                    format!("{} {}", verb.base(), capitalize_first(name))
                }
            }

            CanvasStatus { note } => match note {
                CanvasStatusNote::NothingSelected => "Nothing selected.".to_owned(),
                CanvasStatusNote::NoMarks => "No marks.".to_owned(),
                CanvasStatusNote::NotAGroup => "Not a group.".to_owned(),
                CanvasStatusNote::NotATextCard => "Not a text card.".to_owned(),
                CanvasStatusNote::NotAFileCard => "Not a file card.".to_owned(),
                CanvasStatusNote::NoGroups => "This canvas has no groups.".to_owned(),
                CanvasStatusNote::NoNotesInVault => "This vault has no notes yet.".to_owned(),
                CanvasStatusNote::NoMediaInVault => "This vault has no media files.".to_owned(),
                CanvasStatusNote::NoFilesToPointAt => {
                    "This vault has no files to point at.".to_owned()
                }
                CanvasStatusNote::OnlyTextCardsConvert => {
                    "Only text cards convert to notes.".to_owned()
                }
                CanvasStatusNote::NoConnections => {
                    "The selected card has no connections.".to_owned()
                }
                CanvasStatusNote::PickOutsideMovingSet => {
                    "Pick a card outside the moving set.".to_owned()
                }
                CanvasStatusNote::PickDifferentTarget => {
                    "Pick a different card to connect to.".to_owned()
                }
                CanvasStatusNote::NoChanges => "No changes.".to_owned(),
                CanvasStatusNote::NotReadable => "Canvas is not readable.".to_owned(),
                CanvasStatusNote::Empty => "Canvas is empty.".to_owned(),
                CanvasStatusNote::EndOfCanvas => "End of canvas.".to_owned(),
                CanvasStatusNote::StartOfCanvas => "Start of canvas.".to_owned(),
                CanvasStatusNote::AtCanvasLevel => "At canvas level.".to_owned(),
                CanvasStatusNote::NoCardsMatchFilter => "No cards match the filter.".to_owned(),
                CanvasStatusNote::NothingToUndo => "Nothing to undo.".to_owned(),
                CanvasStatusNote::NothingToRedo => "Nothing to redo.".to_owned(),
                CanvasStatusNote::GroupIsEmpty { label } => {
                    format!("Group \"{label}\" is empty.")
                }
                CanvasStatusNote::NoOutgoingPath { title } => {
                    format!("No outgoing path from \"{title}\".")
                }
                CanvasStatusNote::NotInAGroup { title } => {
                    format!("\"{title}\" is not in a group.")
                }
                CanvasStatusNote::NoConnection { forward, ordinal } => {
                    let base = if *forward {
                        "No outgoing connection"
                    } else {
                        "No incoming connection"
                    };
                    match ordinal {
                        Some(ordinal) => format!("{base} {ordinal}."),
                        None => format!("{base}."),
                    }
                }
                CanvasStatusNote::Reopening => {
                    "This canvas is reopening. Try again in a moment.".to_owned()
                }
                CanvasStatusNote::Loading => {
                    "This canvas is loading. Try again in a moment.".to_owned()
                }
            },
            CanvasBlocked { reason } => match reason {
                CanvasBlockedReason::ModeBusy => {
                    "A move or resize is in progress. Return to place it or Escape to \
                     cancel first."
                        .to_owned()
                }
                CanvasBlockedReason::UndoBlocked => {
                    "Undo blocked: the canvas changed on disk. Reload it and try again.".to_owned()
                }
                CanvasBlockedReason::RedoBlocked => {
                    "Redo blocked: the canvas changed on disk. Reload it and try again.".to_owned()
                }
                CanvasBlockedReason::LinkOpenFailed => "The link could not be opened.".to_owned(),
                CanvasBlockedReason::AlignWouldOverlap => {
                    "Aligning would overlap another card — not moved.".to_owned()
                }
                CanvasBlockedReason::NotAUrl => "That doesn't look like a URL.".to_owned(),
                CanvasBlockedReason::CardTextUnreadable => {
                    "The card's text could not be read.".to_owned()
                }
                CanvasBlockedReason::NotePathMustEndInMd => {
                    "The note path must end in .md.".to_owned()
                }
                CanvasBlockedReason::NoFreeSpaceInGroup { label } => {
                    format!("No free space inside \"{label}\".")
                }
                CanvasBlockedReason::NotePathExists { path, on_disk } => {
                    if *on_disk {
                        format!("{path} already exists on disk. Pick another name.")
                    } else {
                        format!("{path} already exists. Pick another name.")
                    }
                }
                CanvasBlockedReason::NoteReadFailed { message } => {
                    format!("Could not read the card text: {message}")
                }
                CanvasBlockedReason::NoteCreateFailed { path, message } => {
                    format!("Could not create {path}: {message}")
                }
                CanvasBlockedReason::NoteRetargetFailed { path, message } => {
                    format!("Created {path}, but could not retarget the card: {message}")
                }
                CanvasBlockedReason::HeadingNotFound { heading, filename } => {
                    format!("Heading {heading} was not found in {filename}.")
                }
                CanvasBlockedReason::ReopenFailed { message } => format!(
                    "Canvas could not be reopened. The previous snapshot is read-only. \
                     {message}"
                ),
            },
            CanvasActionFailed { action, detail } => {
                format!("{} failed: {detail}", action.verb())
            }
            CanvasSaveConflict => {
                "The canvas changed on disk. Reload it to continue — your action was \
                 not applied."
                    .to_owned()
            }
            CanvasFileNotFound { target } => {
                format!("{target} is missing from the vault. Use Locate File to repoint this card.")
            }
            CanvasOpened { title, target } => match target {
                CanvasOpenTarget::DefaultApp => {
                    format!("Opened {title} in its default app.")
                }
                CanvasOpenTarget::Browser => format!("Opened {title} in your browser."),
            },

            CanvasMutationRefused { reason } => match reason {
                CanvasMutationRefusal::Opening => {
                    "This canvas is still opening. Wait for it to finish before making \
                     changes."
                }
                CanvasMutationRefusal::Reopening => {
                    "This canvas is reopening. Wait for it to finish before making changes."
                }
                CanvasMutationRefusal::RetargetFailed => {
                    "This canvas could not be reopened. Choose Retry before making changes."
                }
                CanvasMutationRefusal::Unavailable => {
                    "This canvas is no longer available. Copy any draft before closing."
                }
                CanvasMutationRefusal::ReadOnly => {
                    "This canvas is read-only because it could not be opened safely."
                }
                CanvasMutationRefusal::CardEditorUnavailable => {
                    "This canvas is no longer available. Copy your draft before closing \
                     the editor."
                }
            }
            .to_owned(),

            CanvasLoadedDegraded { skipped } => format!(
                "Canvas loaded. {skipped} unsupported {} are preserved in the file but \
                 not shown.",
                plural(*skipped, "item", "items")
            ),
            CanvasEmptyOnboarding {
                new_card_chord,
                palette_chord,
            } => format!(
                "Canvas is empty. Press {new_card_chord} to create your first card. \
                 Every other canvas action is in the Command Palette, {palette_chord}."
            ),
            CanvasWhereAmI {
                kind_label,
                title,
                group_path,
                ordinal_n,
                total_m,
                connection_count,
                in_count,
                out_count,
                color_name,
                marked,
                mode,
                filter,
            } => {
                let mut parts = vec![card_ref(kind_label, title)];
                parts.push(if group_path.is_empty() {
                    "at canvas level".to_owned()
                } else {
                    format!("in {}", group_path.join(" › "))
                });
                parts.push(format!("{ordinal_n} of {total_m}"));
                parts.push(format!(
                    "{connection_count} {} ({in_count} in, {out_count} out)",
                    plural(*connection_count, "connection", "connections")
                ));
                if let Some(color_name) = color_name {
                    parts.push(color_name.clone());
                }
                if *marked {
                    parts.push("marked".to_owned());
                }
                if let Some(mode) = mode {
                    parts.push(mode.name().to_owned());
                }
                if let CanvasFilterState::Active { matched, total } = filter {
                    parts.push(format!("{matched} of {total} shown"));
                }
                parts.join(", ")
            }
        }
    }
}

/// True when `haystack` already speaks `field` as a COMPLETE row
/// field — case-folded, bounded on each side by the start/end of the
/// description or a field separator (`.`/`,`/`;`, one optional space
/// before). A plain substring hit is NOT dedup: "Status: Open" must
/// not vanish into "Substatus: Opening" (mid-word) or "Status: Open
/// Questions remain" (the row's actual value keeps going) — both
/// adversarial round-1 findings.
fn contains_field(haystack: &str, field: &str) -> bool {
    let haystack = haystack.to_lowercase();
    let field = field.to_lowercase();
    if field.is_empty() {
        return true;
    }
    haystack.match_indices(&field).any(|(begin, matched)| {
        let end = begin + matched.len();
        let before = haystack[..begin].trim_end();
        let after = &haystack[end..];
        (before.is_empty() || before.ends_with(['.', ',', ';']))
            && (after.is_empty() || after.starts_with(['.', ',', ';']))
    })
}

/// en-US count noun (this vocabulary is V1 English; #264 owns l10n).
/// Delegates so the singular-at-exactly-one rule has one definition;
/// the count is interpolated by the caller and stays ungrouped here.
fn plural<'a>(count: u32, one: &'a str, many: &'a str) -> &'a str {
    crate::sidebar_filter::noun(count as u64, one, many)
}

/// The same rule over a COLLECTION LENGTH. Every count payload in this
/// vocabulary is a `u32`, but the arms that speak the size of a `Vec`
/// hold a `usize`; routing them here keeps the singular/plural rule at
/// one definition instead of casting (or hand-branching) at the site.
fn plural_len<'a>(len: usize, one: &'a str, many: &'a str) -> &'a str {
    crate::sidebar_filter::noun(len as u64, one, many)
}

/// The spoken name of a colour a host just wrote, or the literal
/// `no color` for the clear-colour arm. One line, but it is the whole
/// point of the typed `Option<CanvasColor>` payload: the preset table
/// lives in [`canvas::color_name`] and nowhere else, on either host.
fn spoken_color(color: &Option<CanvasColor>) -> String {
    match color {
        Some(color) => crate::canvas::color_name(color),
        None => "no color".to_owned(),
    }
}

/// A grouped count plus its noun (`"3 cards"`, `"1,024 cards"`). The
/// canvas templates that mac built with `CountCopy.counted` route here
/// instead: `CountCopy` deliberately does NOT group thousands, so the
/// two spellings diverge at ≥ 1000 and core's grouping wins (contracts
/// doc 0a-D6).
fn counted(count: u32, one: &str, many: &str) -> String {
    crate::sidebar_filter::count_noun(count as u64, one, many)
}

/// The t0 §1.1 card reference: `Group "label"` for groups,
/// `⟨Kind⟩ card "title"` otherwise. ONE definition — mac spelled it in
/// `CanvasCardRef.phrase`, again (unquoted) in the renderer's peer
/// names, and again (with a hardcoded kind) in the outline's
/// connection rows, and the three drifted.
fn card_ref(kind_label: &str, title: &str) -> String {
    if kind_label == "group" {
        format!("Group \"{title}\"")
    } else {
        format!("{} card \"{title}\"", capitalize_first(kind_label))
    }
}

/// Upper-cases the LEADING character only — canvas payloads embed
/// user-typed titles that must pass through verbatim.
fn capitalize_first(text: &str) -> String {
    let mut chars = text.chars();
    match chars.next() {
        Some(first) => first.to_uppercase().collect::<String>() + chars.as_str(),
        None => String::new(),
    }
}

/// The inverse, for a card reference used mid-sentence
/// (`Created text card "X" …`).
fn lower_first(text: &str) -> String {
    let mut chars = text.chars();
    match chars.next() {
        Some(first) => first.to_lowercase().collect::<String>() + chars.as_str(),
        None => String::new(),
    }
}

/// Core's own placement description, spoken. Lower-case because every
/// shipped template places it mid-sentence; the move-mode narration
/// capitalises its leading phrase itself.
fn relative_phrase(relative: &RelativeDesc) -> String {
    match relative {
        RelativeDesc::Below(anchor) => format!("below \"{anchor}\""),
        RelativeDesc::RightOf(anchor) => format!("right of \"{anchor}\""),
        RelativeDesc::Above(anchor) => format!("above \"{anchor}\""),
        RelativeDesc::LeftOf(anchor) => format!("left of \"{anchor}\""),
        RelativeDesc::AtOrigin => "at the canvas origin".to_owned(),
    }
}

/// The overlap clause a transient geometry line carries (t4 G20). It
/// is a suffix, never a sentence of its own.
fn overlap_clause(overlap: &Option<CanvasOverlapTransition>) -> &'static str {
    match overlap {
        None => "",
        Some(CanvasOverlapTransition::Onset) => ". Overlapping another card",
        Some(CanvasOverlapTransition::Cleared) => ". Clear of overlaps",
    }
}

/// The object clause of a mode announcement (t0 §2 M1).
fn mode_object(object: &CanvasModeObject) -> String {
    match object {
        CanvasModeObject::Card { title } => format!("\"{title}\""),
        CanvasModeObject::Cards { count } => {
            format!("{count} {}", plural(*count, "card", "cards"))
        }
    }
}

/// One representative event per variant (parameterized variants use
/// fixed sample values). This is the seed of the §W-D canonical corpus:
/// the goldens below pin every entry's (priority, text), and the
/// committed corpus artifact is generated from the same list, so the
/// Rust goldens, the fixture, and the Swift census can never drift
/// apart.
pub fn corpus() -> Vec<A11yEvent> {
    use A11yEvent::*;
    let mut events = vec![
        FilesRegionFocused,
        LeafPanelShown {
            title: "Outline".into(),
        },
        EditorPaneFocused {
            ordinal: 2,
            total: 3,
            title: "notes.md".into(),
            prefix: String::new(),
        },
        TabFocused {
            prefix: "Now".into(),
            filename: "notes.md".into(),
            index: 1,
            count: 4,
        },
        TabClosed {
            closed_title: "draft.md".into(),
            successor: Some("notes.md".into()),
        },
        TabClosed {
            closed_title: "draft.md".into(),
            successor: None,
        },
        NoSplitPanesToResize,
        PaneResized { percent: 60 },
        GraphOpensSinglePane,
        RightPaneShown,
        RightPaneHidden,
        HistoryPanelShown,
        ReopenTargetMissing {
            filename: "gone.md".into(),
        },
        ReopenedFile {
            filename: "notes.md".into(),
        },
        ReopenedNamed {
            name: "Open tasks".into(),
        },
        ReopenedGraph,
        VaultOpened {
            vault_title: "Garden".into(),
            sidebar_notice: String::new(),
        },
        RemovedRecentVault {
            display_name: "Garden".into(),
        },
        WelcomeShown {
            recent_vault_count: 0,
        },
        WelcomeShown {
            recent_vault_count: 1,
        },
        WelcomeShown {
            recent_vault_count: 2,
        },
        CommandPaletteNeedsVault,
        SearchNeedsVault,
        SearchResultsSummary { count: 0 },
        SearchResultsSummary { count: 1 },
        SearchResultsSummary { count: 7 },
        SearchFailed {
            message: "the index is unavailable".into(),
        },
        SearchResultOpened {
            filename: "notes.md".into(),
            line: 12,
            snippet: "the quick brown fox".into(),
        },
        ExternalLinkUnsupported {
            target: "ftp://example.com".into(),
        },
        ExternalLinkOpened,
        ExternalLinkFailed {
            target: "https://example.com".into(),
        },
        LinkUnresolved {
            target: "Missing Note".into(),
        },
        HelpOpened,
        HelpFailed,
        InternalNavigated {
            kind: "Opened".into(),
            filename: "notes.md".into(),
        },
        CitationNotLoaded,
        NoResolvedEmbedAtCursor,
        NoEmbedAtCursor,
        HeadingNotFound,
        HeadingScrollFailed {
            heading: "Roadmap".into(),
        },
        ScrolledToHeading {
            heading: "Roadmap".into(),
        },
        ScrolledToLine {
            filename: "notes.md".into(),
            line: 40,
        },
        OpenedAtLine {
            filename: "notes.md".into(),
            line: 40,
        },
        OpenedFile {
            filename: "notes.md".into(),
        },
        ShowingNote {
            display_name: "notes".into(),
        },
        TaskToggleUnsaved {
            filename: "notes.md".into(),
        },
        TaskToggleConflict {
            filename: "notes.md".into(),
        },
        TasksReviewShown {
            filter_name: "Open tasks".into(),
        },
        TasksFilterSet {
            filter_name: "All tasks".into(),
        },
        NoteSaved {
            filename: "notes.md".into(),
        },
        SaveConflict {
            filename: "notes.md".into(),
        },
        RestoredVersionFrom {
            formatted_date: "July 19, 2026 at 9:41 AM".into(),
        },
        RestoredFile {
            filename: "notes.md".into(),
        },
        RestoredFileAs {
            source_name: "notes.md".into(),
            filename: "notes-restored.md".into(),
        },
        PrintNeedsNote,
        PrintDialogOpened {
            name: "notes.md".into(),
        },
        BatchCheckStarted {
            formatted_count: "1,024".into(),
            action_name: "Move".into(),
        },
        SelectionCopied,
        SidebarSettingsStillDefaults {
            detail: "the file is malformed.".into(),
        },
        SidebarSettingsReloadedStaleRefs,
        SidebarSettingsReloaded,
        VaultClosed,
        VaultClosedAllSaved,
        VaultClosedChangesDiscarded,
        PropertiesUpdated,
        PropertyChanged {
            key: "tags".into(),
            deleted: false,
        },
        PropertyChanged {
            key: "tags".into(),
            deleted: true,
        },
        PropertyEditConflict {
            filename: "notes.md".into(),
        },
        PropertiesSourceRejected {
            reason: "the YAML does not parse".into(),
        },
        PropertyEditFailed {
            detail: "io error".into(),
        },
        PropertiesReloaded,
        PropertiesReloadedBodyChanged,
        NoteChangedAgain { detail: None },
        NoteChangedAgain {
            detail: Some("The note changed while saving.".into()),
        },
        PropertiesReloadFailed {
            reason: "io error".into(),
        },
        PropertyRetainedCopied,
        PropertyRecoveryUnverified {
            display_name: "notes".into(),
        },
        PropertyRetainedDiscarded,
        PropertyRetainedReapplyFailed { detail: None },
        PropertyReloadStillFailed {
            reason: "io error".into(),
        },
        PropertyLoadCurrentFailed {
            reason: "io error".into(),
        },
        AddPropertySheetShown,
        SourceChangesDiscarded,
        BulkRenameSheetShown,
        RenameReloadFailed { detail: None },
        RenameFailed {
            detail: "io error".into(),
        },
        RenameSummary {
            applied: true,
            renamed: 3,
            skipped: 1,
            failed: 0,
        },
        RenameSummary {
            applied: false,
            renamed: 1,
            skipped: 0,
            failed: 0,
        },
        RenameSummary {
            applied: false,
            renamed: 3,
            skipped: 2,
            failed: 0,
        },
        DuplicateFilesOnly,
        MathSpeechStyle {
            name: "ClearSpeak".into(),
        },
        MathVerbosity {
            name: "Verbose".into(),
        },
        MathBrailleCode {
            name: "Nemeth".into(),
        },
        CodePreambleVerbosity {
            name: "Concise".into(),
        },
        EditorTextSize { percent: 110 },
        SpellCheckToggled { enabled: true },
        SpellCheckToggled { enabled: false },
        CitationStyleChanged {
            title: "APA".into(),
        },
        CitationsCount { count: 1 },
        CitationsCount { count: 3 },
        OutlineCount { count: 1 },
        OutlineCount { count: 5 },
        FileListCount { count: 1 },
        FileListCount { count: 12 },
        ItemsSelected { count: 4 },
        ItemsSelected { count: 1 },
        NoItemsSelected,
        TreeFolderSelected {
            name: "Archive".into(),
        },
        RowSelected {
            name: "notes".into(),
        },
        SwitcherRecentCount { count: 2 },
        SwitcherRecentCount { count: 1 },
        // Zero recents announces too (shipped behavior): an empty vault's
        // switcher tells the user the recency list is empty rather than
        // staying silent.
        SwitcherRecentCount { count: 0 },
        SwitcherNoMatches {
            query: "zzz".into(),
        },
        SwitcherMatchCount {
            count: 2,
            query: "foo".into(),
        },
        SwitcherMatchCount {
            count: 1,
            query: "foo".into(),
        },
        PaletteCommandSelected {
            label: "Save".into(),
            disabled_reason: None,
        },
        PaletteCommandSelected {
            label: "Save".into(),
            disabled_reason: Some("A structural operation is in progress.".into()),
        },
        PaletteFilterCount {
            count: 0,
            query: "zzz".into(),
        },
        PaletteFilterCount {
            count: 1,
            query: "save".into(),
        },
        PaletteFilterCount {
            count: 4,
            query: "e".into(),
        },
        PaletteCommandFailed {
            label: "Save".into(),
            detail: Some("disk full".into()),
        },
        PaletteCommandFailed {
            label: "Save".into(),
            detail: None,
        },
        PaletteCommandNotFound {
            id: "slate.nope".into(),
        },
        PaletteCommandUnavailable {
            reason: "A structural operation is in progress.".into(),
        },
        RecentSearchFocused {
            query: "fox".into(),
        },
        QuickSwitcherCount {
            count: 2,
            query: None,
        },
        QuickSwitcherCount {
            count: 1,
            query: None,
        },
        QuickSwitcherCount {
            count: 2,
            query: Some("foo".into()),
        },
        QuickSwitcherCount {
            count: 1,
            query: Some("foo".into()),
        },
        QuickSwitcherCount {
            count: 0,
            query: Some("zzz".into()),
        },
        BaseViewMode {
            mode: "cards".into(),
        },
        BaseViewSwitcher { view_count: 1 },
        BaseViewSwitcher { view_count: 2 },
        BasesNewQueryBuilder,
        BasesEditingFilters {
            view_name: "Table".into(),
        },
        BasesFiltersOpenFailed {
            detail: "io error".into(),
        },
        BasesPreviewFailed {
            detail: "bad expression".into(),
        },
        BasesBuilderSaved,
        BasesViewSaveFailed {
            detail: "io error".into(),
        },
        BasesSavedQueryNameNeeded,
        BasesSavedQueryCreated {
            name: "Open tasks".into(),
        },
        BasesSavedQueryCreateFailed {
            detail: "io error".into(),
        },
        BasesSavedQueryUpdated {
            name: "Open tasks".into(),
        },
        BasesSavedQueryUpdateFailed {
            detail: "io error".into(),
        },
        BasesViewSelected {
            name: "Cards".into(),
        },
        BasesSortSaveFailed {
            detail: "io error".into(),
        },
        BaseRefreshed,
        BaseWhereAmI {
            base: "Reading".into(),
            view: None,
            quick_filter: None,
        },
        BaseWhereAmI {
            base: "Reading".into(),
            view: Some("Table".into()),
            quick_filter: None,
        },
        BaseWhereAmI {
            base: "Reading".into(),
            view: Some("Table".into()),
            quick_filter: Some("CAFE".into()),
        },
        BaseResultsPopover {
            audio_summary: "12 results.".into(),
            where_am_i: None,
        },
        BaseResultsPopover {
            audio_summary: "12 results.".into(),
            where_am_i: Some("Base: Reading, quick filter: CAFE".into()),
        },
        BaseQuickFilterResult { shown: 0, total: 0 },
        BaseQuickFilterResult { shown: 1, total: 1 },
        BaseQuickFilterResult { shown: 1, total: 2 },
        BaseRowReorderRefused {
            label: "Sort 1".into(),
        },
        BaseRowReorderAtBoundary {
            label: "Sort 1".into(),
            at_first: true,
        },
        BaseRowReorderAtBoundary {
            label: "Sort 2".into(),
            at_first: false,
        },
        BaseRowReorderMoved {
            label: "Sort 1".into(),
            moved_up: false,
            position: 2,
            count: 3,
        },
        BaseRowReorderMoved {
            label: "Status column".into(),
            moved_up: true,
            position: 1,
            count: 3,
        },
        BaseQueryPreviewIdle,
        BaseQueryPreviewLoading,
        BaseQueryPreviewReady {
            audio_summary: "12 results.".into(),
            first_result: None,
        },
        // The unterminated-summary branch: the separator must supply
        // the period the summary lacks.
        BaseQueryPreviewReady {
            audio_summary: "12 results".into(),
            first_result: Some("Alpha".into()),
        },
        BaseQueryPreviewReady {
            audio_summary: "12 results.".into(),
            first_result: Some("Alpha".into()),
        },
        BaseQueryPreviewFailed {
            detail: "invalid expression".into(),
        },
        BaseSortedByColumn {
            column: "Status".into(),
            ascending: true,
        },
        BaseSortedByColumn {
            column: "Status".into(),
            ascending: false,
        },
        BaseSortSavedToView {
            column: "Status".into(),
            ascending: true,
        },
        BaseSortSavedToView {
            column: "Status".into(),
            ascending: false,
        },
        BasesSavedQueryReferenceMissing {
            reference: "Open tasks".into(),
        },
        BasesSavedQueryMissing,
        BasesQueriesRefreshFailed {
            detail: "io error".into(),
        },
        BasesSavedQueryEditing {
            name: "Open tasks".into(),
        },
        BasesSavedQueryEditFailed {
            detail: "io error".into(),
        },
        BasesSavedQueryRenameNameNeeded,
        BasesSavedQueryRenamed {
            name: "Open tasks".into(),
        },
        BasesSavedQueryRenameFailed {
            detail: "io error".into(),
        },
        BasesSavedQueryDeleted,
        BasesSavedQueryDeleteFailed {
            detail: "io error".into(),
        },
        BasesSavedQueryExportPathNeeded,
        BasesSavedQueryExported {
            name: "Open tasks.base".into(),
        },
        BasesSavedQueryExportFailed {
            detail: "io error".into(),
        },
        BasesPathOutsideVault,
        BasesDashboardNameNeeded,
        BasesDashboardSaved {
            name: "Reading".into(),
        },
        BasesDashboardSaveFailed {
            detail: "io error".into(),
        },
        BasesDashboardUpdated {
            name: "Reading".into(),
        },
        BasesDashboardUpdateFailed {
            detail: "io error".into(),
        },
        BasesDashboardSectionStale,
        BasesDashboardSectionRemoveFailed {
            detail: "io error".into(),
        },
        BasesDashboardSectionReplaceFailed {
            detail: "io error".into(),
        },
        BasesDashboardDeleted,
        BasesDashboardDeleteFailed {
            detail: "io error".into(),
        },
        BasesDashboardEditFailed {
            detail: "io error".into(),
        },
        BasesDashboardMissing,
        BasesDockUpdatedForNote,
        BasesLinkCopied {
            name: "Reading".into(),
        },
        BasesBacklinksFor {
            name: "Reading".into(),
        },
        BasesViewCopyNoActiveBase,
        BasesViewCopiedAsMarkdown,
        BasesViewCopyFailed {
            detail: "io error".into(),
        },
        BasesRowSelectionNeeded,
        BasesNoEditableProperty,
        BasesCellReadOnly {
            file_metadata: true,
        },
        BasesCellReadOnly {
            file_metadata: false,
        },
        BasesCellSaved {
            column: "Status".into(),
            value: "Done".into(),
        },
        BasesCellCleared {
            column: "Status".into(),
        },
        BasesCellRowNoLongerMatches,
        BasesCellEditFailed {
            detail: "io error".into(),
        },
        BasesCellEditCanceled,
        BasesViewExported,
        BasesViewExportFailed {
            detail: "io error".into(),
        },
        BasesDataviewConverted,
        BasesDataviewConversionSaveFailed {
            detail: "io error".into(),
        },
        BasesQuickFilterChoiceCanceled {
            verb: "Export".into(),
        },
        BasesCellMustBeFiniteNumber,
        BasesCellMustBeWholeNumber,
        BasesCellMustBeFiniteDecimal,
        BasesCellMustBeBoolean,
        BasesCellMustBeDate,
        BasesRefreshUpdated {
            audio_summary: "1 note.".into(),
        },
        DataviewConversionFailed {
            detail: "unsupported query".into(),
        },
        CitationInsertUnavailable,
        CitationWalkThrough,
        CodeCopied,
        ReadingNavNoTarget {
            target: ReadingNavTarget::Heading,
            forward: true,
        },
        ReadingNavNoTarget {
            target: ReadingNavTarget::HeadingLevel { level: 2 },
            forward: false,
        },
        ReadingNavNoTarget {
            target: ReadingNavTarget::Link,
            forward: true,
        },
        ReadingNavNoTarget {
            target: ReadingNavTarget::List,
            forward: false,
        },
        ReadingNavNoTarget {
            target: ReadingNavTarget::Table,
            forward: true,
        },
        ReadingNavNoTarget {
            target: ReadingNavTarget::Embed,
            forward: false,
        },
        ReadingNavNoTarget {
            target: ReadingNavTarget::CodeBlock,
            forward: true,
        },
        ReadingNavLanded {
            target: ReadingNavTarget::HeadingLevel { level: 2 },
            text: "Lists and tasks".into(),
        },
        ReadingNavLanded {
            target: ReadingNavTarget::Link,
            text: "Target Note".into(),
        },
        ReadingNavLanded {
            target: ReadingNavTarget::List,
            text: "first bullet".into(),
        },
        ReadingNavLanded {
            target: ReadingNavTarget::Table,
            text: "column a".into(),
        },
        ReadingNavLanded {
            target: ReadingNavTarget::Embed,
            text: "Embedded note Target Note".into(),
        },
        ReadingNavLanded {
            target: ReadingNavTarget::CodeBlock,
            text: "fn spoken_interior() -> usize { 42 }".into(),
        },
        ReadingNavNoTarget {
            target: ReadingNavTarget::Math,
            forward: true,
        },
        ReadingNavLanded {
            target: ReadingNavTarget::Math,
            // The landing text is the MathCAT speech the host captured
            // from the landed block — content, not composition.
            text: "x equals negative b plus or minus the square root of b squared minus 4 a c, over 2 a".into(),
        },
        ReadingNavNoTarget {
            target: ReadingNavTarget::Diagram,
            forward: true,
        },
        ReadingNavLanded {
            target: ReadingNavTarget::Diagram,
            // The landing text is the canonical structured description
            // the host captured from the landed block, trailing period
            // stripped so the vocabulary's own punctuation composes —
            // content, not composition.
            text: "Flowchart with 3 steps".into(),
        },
        ReadingNavLanded {
            target: ReadingNavTarget::Embed,
            text: "".into(),
        },
        GridSorted {
            column: "Status".into(),
            ascending: true,
        },
        GridSorted {
            column: "Due".into(),
            ascending: false,
        },
        GridRowMoved {
            // Dedup hit: the description already carries the focused
            // cell label, so it is spoken alone.
            description: "Ship the plan. Status: Open. Due: Friday".into(),
            focused_cell: "Status: Open".into(),
        },
        GridRowMoved {
            // Dedup miss, no trailing period: ". " joins.
            description: "Ship the plan".into(),
            focused_cell: "Status: Open".into(),
        },
        GridRowMoved {
            // Dedup miss with a trailing period: a space joins.
            description: "Done reviewing.".into(),
            focused_cell: "Status: Open".into(),
        },
        GridRowMoved {
            // Substring near-collision (adversarial round 1): the
            // description does NOT speak the focused field — the hit
            // is inside longer words — so both are spoken.
            description: "Substatus: Opening".into(),
            focused_cell: "Status: Open".into(),
        },
        GridRowMoved {
            // Value continuation (adversarial round 1): the field
            // appears but the row's actual value keeps going, so the
            // focused cell is NOT already conveyed — both spoken.
            description: "Status: Open Questions remain".into(),
            focused_cell: "Status: Open".into(),
        },
        GridCellMoved {
            column: "Status".into(),
            value: "Open".into(),
        },
        GridGroup {
            label: "Open".into(),
            row_count: 1,
            summary: None,
        },
        GridGroup {
            label: "Done".into(),
            row_count: 12,
            summary: Some("Count: 12".into()),
        },
        TemplatePickerOpened { count: 0 },
        TemplatePickerOpened { count: 1 },
        TemplatePickerOpened { count: 7 },
        TemplateNoteCreated {
            name: "Meeting 2026-08-20.md".into(),
            template: "Meeting".into(),
        },
        HostComposed {
            text: "Composed by a host engine.".into(),
            priority: A11yPriority::High,
        },
    ];
    // The canvas family (W6-1 0a, #745) is APPENDED as one block, so
    // every pre-existing index — and therefore both host mirrors —
    // is untouched by it.
    events.extend(canvas_corpus().into_iter().map(|event| Canvas { event }));
    events
}

/// The canvas half of the corpus, in its own list because the family
/// is its own enum ([`A11yEvent::Canvas`]). One representative event
/// per variant, plus one per closed-set ARM whose template differs —
/// `every_canvas_variant_and_arm_is_represented_in_the_corpus` proves
/// the coverage.
fn canvas_corpus() -> Vec<CanvasA11yEvent> {
    use CanvasA11yEvent::*;
    vec![
        CanvasMovedTo {
            verbosity: CanvasVerbosity::Terse,
            kind_label: "text".into(),
            title: "Research".into(),
            ordinal_n: 2,
            total_m: 5,
            container: Some("Q3".into()),
            connection_count: 3,
            color_name: Some("red".into()),
            marked: true,
        },
        CanvasMovedTo {
            verbosity: CanvasVerbosity::Standard,
            kind_label: "text".into(),
            title: "Research".into(),
            ordinal_n: 2,
            total_m: 5,
            container: Some("Q3".into()),
            connection_count: 3,
            color_name: Some("red".into()),
            marked: true,
        },
        CanvasMovedTo {
            verbosity: CanvasVerbosity::Verbose,
            kind_label: "text".into(),
            title: "Research".into(),
            ordinal_n: 2,
            total_m: 5,
            container: Some("Q3".into()),
            connection_count: 3,
            color_name: Some("red".into()),
            marked: true,
        },
        CanvasMovedTo {
            verbosity: CanvasVerbosity::Standard,
            kind_label: "group".into(),
            title: "Q3".into(),
            ordinal_n: 1,
            total_m: 3,
            container: None,
            connection_count: 0,
            color_name: None,
            marked: false,
        },
        CanvasMovedTo {
            verbosity: CanvasVerbosity::Verbose,
            kind_label: "file".into(),
            title: "Notes.md".into(),
            ordinal_n: 1,
            total_m: 1,
            container: None,
            connection_count: 1,
            color_name: None,
            marked: false,
        },
        CanvasGroupEntered {
            label: "Q3".into(),
            count: 4,
        },
        CanvasGroupEntered {
            label: "Solo".into(),
            count: 1,
        },
        CanvasGroupLeft { label: "Q3".into() },
        CanvasViewportNoPane,
        CanvasConnectionTraversed {
            direction: EdgeDirection::Outgoing,
            kind_label: "text".into(),
            title: "Ideas".into(),
            label: Some("supports".into()),
        },
        CanvasConnectionTraversed {
            direction: EdgeDirection::Incoming,
            kind_label: "text".into(),
            title: "Research".into(),
            label: None,
        },
        CanvasConnectionTraversed {
            direction: EdgeDirection::Undirected,
            kind_label: "group".into(),
            title: "Q3".into(),
            label: None,
        },
        CanvasConnectionTraversed {
            direction: EdgeDirection::Bidirectional,
            kind_label: "link".into(),
            title: "example.com".into(),
            label: None,
        },
        CanvasTracePathEnd {
            titles: vec!["Research".into(), "Ideas".into(), "Draft".into()],
        },
        CanvasMoveRelative {
            descs: Vec::new(),
            overlap: None,
        },
        CanvasMoveRelative {
            descs: vec![RelativeDesc::Below("Research".into())],
            overlap: None,
        },
        CanvasMoveRelative {
            descs: vec![
                RelativeDesc::Below("Research".into()),
                RelativeDesc::RightOf("Ideas".into()),
            ],
            overlap: Some(CanvasOverlapTransition::Onset),
        },
        CanvasMoveRelative {
            descs: vec![RelativeDesc::Above("Ideas".into())],
            overlap: Some(CanvasOverlapTransition::Cleared),
        },
        CanvasResizeGeometry {
            preset: None,
            width: 320,
            height: 200,
            overlap: None,
        },
        CanvasResizeGeometry {
            preset: Some(CanvasResizePreset::DefaultSize),
            width: 260,
            height: 140,
            overlap: None,
        },
        CanvasResizeGeometry {
            preset: Some(CanvasResizePreset::FitToContent),
            width: 260,
            height: 88,
            overlap: Some(CanvasOverlapTransition::Onset),
        },
        CanvasResizeClamped,
        CanvasModeEntered {
            mode: CanvasMode::Move,
            object: CanvasModeObject::Card {
                title: "Research".into(),
            },
        },
        CanvasModeEntered {
            mode: CanvasMode::Move,
            object: CanvasModeObject::Cards { count: 3 },
        },
        CanvasModeEntered {
            mode: CanvasMode::Resize,
            object: CanvasModeObject::Card {
                title: "Research".into(),
            },
        },
        CanvasModeEntered {
            mode: CanvasMode::Connect,
            object: CanvasModeObject::Card {
                title: "Research".into(),
            },
        },
        CanvasModeRejected {
            active_mode: CanvasMode::Move,
        },
        CanvasModeCommitted {
            verb: CanvasTransientVerb::Move,
            object: CanvasModeObject::Card {
                title: "Research".into(),
            },
        },
        CanvasModeCommitted {
            verb: CanvasTransientVerb::Move,
            object: CanvasModeObject::Cards { count: 3 },
        },
        CanvasModeCommitted {
            verb: CanvasTransientVerb::Resize,
            object: CanvasModeObject::Card {
                title: "Research".into(),
            },
        },
        CanvasModeEndedWithoutEffect {
            mode: CanvasMode::Move,
        },
        CanvasModeEndedWithoutEffect {
            mode: CanvasMode::Resize,
        },
        CanvasModeEndedWithoutEffect {
            mode: CanvasMode::Connect,
        },
        CanvasModeCancelled {
            mode: CanvasMode::Move,
            restoration: CanvasModeRestoration::Unstated,
        },
        CanvasModeCancelled {
            mode: CanvasMode::Move,
            restoration: CanvasModeRestoration::CardsReturned { count: 1 },
        },
        CanvasModeCancelled {
            mode: CanvasMode::Move,
            restoration: CanvasModeRestoration::CardsReturned { count: 3 },
        },
        CanvasModeCancelled {
            mode: CanvasMode::Resize,
            restoration: CanvasModeRestoration::Unstated,
        },
        CanvasModeCancelled {
            mode: CanvasMode::Resize,
            restoration: CanvasModeRestoration::SizeRestored,
        },
        CanvasModeCancelled {
            mode: CanvasMode::Connect,
            restoration: CanvasModeRestoration::Unstated,
        },
        CanvasModeCancelled {
            mode: CanvasMode::Connect,
            restoration: CanvasModeRestoration::BackAt {
                title: "Research".into(),
            },
        },
        CanvasCreated {
            kind_label: "text".into(),
            title: "New idea".into(),
            relative: RelativeDesc::Below("Research".into()),
        },
        CanvasCreated {
            kind_label: "group".into(),
            title: "Q3".into(),
            relative: RelativeDesc::RightOf("Research".into()),
        },
        CanvasCreated {
            kind_label: "file".into(),
            title: "Notes.md".into(),
            relative: RelativeDesc::Above("Research".into()),
        },
        CanvasCreated {
            kind_label: "link".into(),
            title: "example.com".into(),
            relative: RelativeDesc::LeftOf("Research".into()),
        },
        CanvasCreated {
            kind_label: "text".into(),
            title: "Untitled".into(),
            relative: RelativeDesc::AtOrigin,
        },
        CanvasFileCreated {
            name: "Roadmap".into(),
        },
        CanvasConnectedCardCreated {
            relative: RelativeDesc::Below("Research".into()),
            origin_title: "Research".into(),
        },
        CanvasConnected {
            from_title: "Research".into(),
            to_title: "Ideas".into(),
            label: Some("supports".into()),
        },
        CanvasConnected {
            from_title: "Research".into(),
            to_title: "Ideas".into(),
            label: None,
        },
        CanvasConnectionUpdated {
            label: Some("supports".into()),
        },
        CanvasConnectionUpdated { label: None },
        CanvasMovedIntoGroup { label: "Q3".into() },
        CanvasRemovedFromGroup { label: "Q3".into() },
        CanvasColorSet {
            title: "Research".into(),
            color: Some(CanvasColor::Preset(1)),
        },
        CanvasColorSet {
            title: "Research".into(),
            color: None,
        },
        CanvasRenamedGroup { label: "Q3".into() },
        CanvasCardUpdated {
            title: "Research".into(),
        },
        CanvasCardRetargeted {
            title: "Research".into(),
            path: "notes/research.md".into(),
        },
        CanvasCardPlaced {
            verb: CanvasPlaceVerb::Moved,
            title: "Research".into(),
            relative: RelativeDesc::Below("Ideas".into()),
        },
        CanvasCardPlaced {
            verb: CanvasPlaceVerb::Duplicated,
            title: "Research".into(),
            relative: RelativeDesc::RightOf("Research".into()),
        },
        CanvasCardAligned {
            title: "Research".into(),
            target_title: "Ideas".into(),
        },
        CanvasConvertedToNote {
            path: "notes/research.md".into(),
        },
        CanvasDeleted {
            target: CanvasDeleteTarget::Card {
                kind_label: "text".into(),
                title: "Research".into(),
            },
            verbosity: CanvasVerbosity::Standard,
            undo_chord: "⌘Z".into(),
        },
        CanvasDeleted {
            target: CanvasDeleteTarget::Card {
                kind_label: "text".into(),
                title: "Research".into(),
            },
            verbosity: CanvasVerbosity::Terse,
            undo_chord: "⌘Z".into(),
        },
        CanvasDeleted {
            target: CanvasDeleteTarget::Group { label: "Q3".into() },
            verbosity: CanvasVerbosity::Standard,
            undo_chord: "⌘Z".into(),
        },
        // The chord parameter is the one recorded platform difference
        // in this corpus (§W-D): the Windows display chord renders
        // through the same template.
        CanvasDeleted {
            target: CanvasDeleteTarget::Cards { count: 3 },
            verbosity: CanvasVerbosity::Standard,
            undo_chord: "Ctrl+Z".into(),
        },
        CanvasDeleted {
            target: CanvasDeleteTarget::Cards { count: 1 },
            verbosity: CanvasVerbosity::Verbose,
            undo_chord: "⌘Z".into(),
        },
        CanvasDeleted {
            target: CanvasDeleteTarget::Connection {
                direction: EdgeDirection::Outgoing,
                other_title: "Ideas".into(),
                label: Some("supports".into()),
            },
            verbosity: CanvasVerbosity::Standard,
            undo_chord: "⌘Z".into(),
        },
        CanvasDeleted {
            target: CanvasDeleteTarget::Connection {
                direction: EdgeDirection::Incoming,
                other_title: "Research".into(),
                label: None,
            },
            verbosity: CanvasVerbosity::Terse,
            undo_chord: "⌘Z".into(),
        },
        CanvasDeleted {
            target: CanvasDeleteTarget::Connection {
                direction: EdgeDirection::Undirected,
                other_title: "Q3".into(),
                label: None,
            },
            verbosity: CanvasVerbosity::Standard,
            undo_chord: "⌘Z".into(),
        },
        CanvasBulkMoved {
            count: 3,
            relative: RelativeDesc::Below("Research".into()),
        },
        CanvasBulkColorSet {
            count: 3,
            color: Some(CanvasColor::Preset(5)),
        },
        CanvasBulkColorSet {
            count: 1,
            color: None,
        },
        CanvasGrouped {
            count: 3,
            label: "Q3".into(),
        },
        CanvasBulkDuplicated { count: 2 },
        CanvasMarkToggled {
            marked: true,
            title: "Research".into(),
            count: 2,
        },
        CanvasMarkToggled {
            marked: false,
            title: "Research".into(),
            count: 1,
        },
        CanvasMarksCleared { count: 0 },
        CanvasMarksCleared { count: 3 },
        CanvasFilterCount { matched: 3 },
        CanvasFilterCount { matched: 1 },
        CanvasFilterCleared { total: 40 },
        CanvasFilterCleared { total: 1 },
        CanvasZoom {
            context: None,
            percent: 100,
        },
        CanvasZoom {
            context: Some(CanvasZoomContext::FitCanvas),
            percent: 80,
        },
        CanvasZoom {
            context: Some(CanvasZoomContext::ZoomedToSelection),
            percent: 150,
        },
        CanvasFollowSelectionToggled { following: true },
        CanvasFollowSelectionToggled { following: false },
        CanvasSurfaceShown {
            surface: CanvasSurfaceKind::Outline,
        },
        CanvasSurfaceShown {
            surface: CanvasSurfaceKind::Table,
        },
        CanvasSurfaceShown {
            surface: CanvasSurfaceKind::Visual,
        },
        CanvasHistoryApplied {
            verb: CanvasHistoryVerb::Undo,
            name: "move \"Research\"".into(),
        },
        CanvasHistoryApplied {
            verb: CanvasHistoryVerb::Redo,
            name: "move \"Research\"".into(),
        },
        CanvasUndoMenuTitle {
            verb: CanvasHistoryVerb::Undo,
            name: "delete \"My Card\"".into(),
        },
        CanvasUndoMenuTitle {
            verb: CanvasHistoryVerb::Redo,
            name: String::new(),
        },
        CanvasStatus {
            note: CanvasStatusNote::NothingSelected,
        },
        CanvasStatus {
            note: CanvasStatusNote::NoMarks,
        },
        CanvasStatus {
            note: CanvasStatusNote::NotAGroup,
        },
        CanvasStatus {
            note: CanvasStatusNote::NotATextCard,
        },
        CanvasStatus {
            note: CanvasStatusNote::NotAFileCard,
        },
        CanvasStatus {
            note: CanvasStatusNote::NoGroups,
        },
        CanvasStatus {
            note: CanvasStatusNote::NoNotesInVault,
        },
        CanvasStatus {
            note: CanvasStatusNote::NoMediaInVault,
        },
        CanvasStatus {
            note: CanvasStatusNote::NoFilesToPointAt,
        },
        CanvasStatus {
            note: CanvasStatusNote::OnlyTextCardsConvert,
        },
        CanvasStatus {
            note: CanvasStatusNote::NoConnections,
        },
        CanvasStatus {
            note: CanvasStatusNote::PickOutsideMovingSet,
        },
        CanvasStatus {
            note: CanvasStatusNote::PickDifferentTarget,
        },
        CanvasStatus {
            note: CanvasStatusNote::NoChanges,
        },
        CanvasStatus {
            note: CanvasStatusNote::NotReadable,
        },
        CanvasStatus {
            note: CanvasStatusNote::Empty,
        },
        CanvasStatus {
            note: CanvasStatusNote::EndOfCanvas,
        },
        CanvasStatus {
            note: CanvasStatusNote::StartOfCanvas,
        },
        CanvasStatus {
            note: CanvasStatusNote::AtCanvasLevel,
        },
        CanvasStatus {
            note: CanvasStatusNote::NoCardsMatchFilter,
        },
        CanvasStatus {
            note: CanvasStatusNote::NothingToUndo,
        },
        CanvasStatus {
            note: CanvasStatusNote::NothingToRedo,
        },
        CanvasStatus {
            note: CanvasStatusNote::GroupIsEmpty { label: "Q3".into() },
        },
        CanvasStatus {
            note: CanvasStatusNote::NoOutgoingPath {
                title: "Research".into(),
            },
        },
        CanvasStatus {
            note: CanvasStatusNote::NotInAGroup {
                title: "Research".into(),
            },
        },
        CanvasStatus {
            note: CanvasStatusNote::NoConnection {
                forward: true,
                ordinal: None,
            },
        },
        CanvasStatus {
            note: CanvasStatusNote::NoConnection {
                forward: true,
                ordinal: Some(2),
            },
        },
        CanvasStatus {
            note: CanvasStatusNote::NoConnection {
                forward: false,
                ordinal: None,
            },
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::ModeBusy,
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::UndoBlocked,
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::RedoBlocked,
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::LinkOpenFailed,
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::AlignWouldOverlap,
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::NotAUrl,
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::CardTextUnreadable,
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::NotePathMustEndInMd,
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::NoFreeSpaceInGroup { label: "Q3".into() },
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::NotePathExists {
                path: "notes/research.md".into(),
                on_disk: false,
            },
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::NotePathExists {
                path: "notes/research.md".into(),
                on_disk: true,
            },
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::NoteReadFailed {
                message: "The card text is unavailable.".into(),
            },
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::NoteCreateFailed {
                path: "notes/research.md".into(),
                message: "io error".into(),
            },
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::NoteRetargetFailed {
                path: "notes/research.md".into(),
                message: "io error".into(),
            },
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::HeadingNotFound {
                heading: "Roadmap".into(),
                filename: "notes.md".into(),
            },
        },
        CanvasBlocked {
            reason: CanvasBlockedReason::ReopenFailed {
                message: "The file moved.".into(),
            },
        },
        CanvasActionFailed {
            action: CanvasFailedAction::NewCard,
            detail: "the file is read-only".into(),
        },
        CanvasActionFailed {
            action: CanvasFailedAction::NewGroup,
            detail: "the file is read-only".into(),
        },
        CanvasActionFailed {
            action: CanvasFailedAction::NewCanvas,
            detail: "the file is read-only".into(),
        },
        CanvasActionFailed {
            action: CanvasFailedAction::MoveIntoGroup,
            detail: "the file is read-only".into(),
        },
        CanvasActionFailed {
            action: CanvasFailedAction::Placement,
            detail: "the file is read-only".into(),
        },
        CanvasActionFailed {
            action: CanvasFailedAction::Align,
            detail: "the file is read-only".into(),
        },
        CanvasActionFailed {
            action: CanvasFailedAction::Create,
            detail: "the file is read-only".into(),
        },
        CanvasActionFailed {
            action: CanvasFailedAction::RemoveFromGroup,
            detail: "the file is read-only".into(),
        },
        CanvasActionFailed {
            action: CanvasFailedAction::Duplicate,
            detail: "the file is read-only".into(),
        },
        CanvasActionFailed {
            action: CanvasFailedAction::CreateConnectedCard,
            detail: "the file is read-only".into(),
        },
        CanvasActionFailed {
            action: CanvasFailedAction::CanvasAction,
            detail: "the file is read-only".into(),
        },
        CanvasActionFailed {
            action: CanvasFailedAction::WhereAmI,
            detail: "the file is read-only".into(),
        },
        CanvasSaveConflict,
        CanvasFileNotFound {
            target: "media/diagram.png".into(),
        },
        CanvasOpened {
            title: "Notes.md".into(),
            target: CanvasOpenTarget::DefaultApp,
        },
        CanvasOpened {
            title: "example.com".into(),
            target: CanvasOpenTarget::Browser,
        },
        CanvasMutationRefused {
            reason: CanvasMutationRefusal::Opening,
        },
        CanvasMutationRefused {
            reason: CanvasMutationRefusal::Reopening,
        },
        CanvasMutationRefused {
            reason: CanvasMutationRefusal::RetargetFailed,
        },
        CanvasMutationRefused {
            reason: CanvasMutationRefusal::Unavailable,
        },
        CanvasMutationRefused {
            reason: CanvasMutationRefusal::ReadOnly,
        },
        CanvasMutationRefused {
            reason: CanvasMutationRefusal::CardEditorUnavailable,
        },
        CanvasLoadedDegraded { skipped: 3 },
        CanvasLoadedDegraded { skipped: 1 },
        CanvasEmptyOnboarding {
            new_card_chord: "Option Command N".into(),
            palette_chord: "Command Shift P".into(),
        },
        CanvasWhereAmI {
            kind_label: "text".into(),
            title: "Research".into(),
            group_path: vec!["Quarter".into(), "Q3".into()],
            ordinal_n: 2,
            total_m: 5,
            connection_count: 3,
            in_count: 1,
            out_count: 2,
            color_name: Some("red".into()),
            marked: true,
            mode: Some(CanvasMode::Move),
            filter: CanvasFilterState::Active {
                matched: 3,
                total: 40,
            },
        },
        CanvasWhereAmI {
            kind_label: "text".into(),
            title: "Loose".into(),
            group_path: Vec::new(),
            ordinal_n: 1,
            total_m: 1,
            connection_count: 1,
            in_count: 1,
            out_count: 0,
            color_name: None,
            marked: false,
            mode: None,
            filter: CanvasFilterState::Inactive,
        },
        // --- Cardinality boundary witnesses (contract 0a-14) ---
        // Every arm above that interpolates a count or a collection
        // length is sampled at its PLURAL value; these sample the
        // singular and the empty collection, so an arm that hardcodes
        // "cards" fails the golden instead of shipping "1 cards". They
        // are appended rather than filed beside their siblings so no
        // pre-existing corpus index moves (contract 0a-2), and grouped
        // so the reason they exist is legible in one block.
        CanvasTracePathEnd {
            titles: vec!["Research".into()],
        },
        CanvasTracePathEnd { titles: Vec::new() },
        CanvasBulkMoved {
            count: 1,
            relative: RelativeDesc::Below("Research".into()),
        },
        CanvasBulkDuplicated { count: 1 },
        CanvasModeEntered {
            mode: CanvasMode::Move,
            object: CanvasModeObject::Cards { count: 1 },
        },
        CanvasModeCommitted {
            verb: CanvasTransientVerb::Move,
            object: CanvasModeObject::Cards { count: 1 },
        },
        // These two already rendered correctly (they route through
        // `counted`); the witnesses exist so the claim "every
        // count-speaking arm has a count-one witness" is TRUE rather
        // than nearly true, and so a later edit cannot regress them
        // unseen.
        CanvasGrouped {
            count: 1,
            label: "Q3".into(),
        },
        CanvasMarksCleared { count: 1 },
        // Zero witnesses, for the arms whose zero the HOST can reach
        // (the reachability claim, with its reason per arm, is in
        // `canvas_count_speaking_arms_have_boundary_witnesses_and_agreement`'s
        // `ZERO_REACHABLE`). The bulk verbs are absent because their
        // call sites announce a precondition instead of counting to
        // zero.
        CanvasMovedTo {
            verbosity: CanvasVerbosity::Verbose,
            kind_label: "text".into(),
            title: "Loose".into(),
            ordinal_n: 1,
            total_m: 1,
            container: None,
            connection_count: 0,
            color_name: None,
            marked: false,
        },
        CanvasMarkToggled {
            marked: false,
            title: "Research".into(),
            count: 0,
        },
        CanvasBulkDuplicated { count: 0 },
        CanvasFilterCount { matched: 0 },
        CanvasFilterCleared { total: 0 },
        CanvasWhereAmI {
            kind_label: "text".into(),
            title: "Loose".into(),
            group_path: Vec::new(),
            ordinal_n: 1,
            total_m: 1,
            connection_count: 0,
            in_count: 0,
            out_count: 0,
            color_name: None,
            marked: false,
            mode: None,
            filter: CanvasFilterState::Inactive,
        },
        // --- Read-verb refusal during the reopening window ---
        // Appended, not filed beside its `CanvasStatus` siblings, so no
        // pre-existing corpus index moves (contract 0a-2) — the same
        // discipline the boundary witnesses above follow.
        CanvasStatus {
            note: CanvasStatusNote::Reopening,
        },
        CanvasStatus {
            note: CanvasStatusNote::Loading,
        },
    ]
}

#[cfg(test)]
mod tests {
    use super::*;
    use A11yPriority::{High, Medium};

    /// The full corpus golden: every representative event's exact
    /// (priority, text). THIS TABLE IS THE CONTRACT — a wording change
    /// here is a product decision (and a §W-D parity change), never a
    /// drive-by.
    #[test]
    fn corpus_renders_the_shipped_strings() {
        let expected: Vec<(A11yPriority, &str)> = vec![
            (Medium, "Files."),
            (Medium, "Outline panel."),
            (Medium, "Editor pane 2 of 3, notes.md."),
            (Medium, "Now notes.md, tab 1 of 4."),
            (Medium, "Closed draft.md. notes.md is active."),
            (Medium, "Closed draft.md."),
            (Medium, "No split panes to resize."),
            (Medium, "Pane resized, 60 percent."),
            (
                Medium,
                "The graph opens in a single pane. Split from a note instead.",
            ),
            (Medium, "Right pane shown."),
            (Medium, "Right pane hidden."),
            (Medium, "History panel."),
            (Medium, "gone.md no longer exists."),
            (Medium, "Reopened notes.md."),
            (Medium, "Reopened Open tasks."),
            (Medium, "Reopened Graph."),
            (
                Medium,
                "Vault Garden opened. Scanning files for the sidebar.",
            ),
            (Medium, "Removed Garden from recent vaults."),
            (
                Medium,
                "Welcome to Slate. Open Vault button focused. Press Return or Command-O to choose a folder of Markdown files.",
            ),
            (
                Medium,
                "Welcome to Slate. Open Vault button focused. Press Return or Command-O to choose a folder of Markdown files. 1 recent vault listed below.",
            ),
            (
                Medium,
                "Welcome to Slate. Open Vault button focused. Press Return or Command-O to choose a folder of Markdown files. 2 recent vaults listed below.",
            ),
            (High, "Open a vault to use the command palette."),
            (Medium, "Open a vault first. Search works inside a vault."),
            (Medium, "Search returned no results."),
            (Medium, "Search returned 1 result."),
            (Medium, "Search returned 7 results."),
            (Medium, "Search error: the index is unavailable"),
            (Medium, "Opened notes.md, line 12: the quick brown fox"),
            (
                Medium,
                "Cannot open external link ftp://example.com. Only web and mail links are supported.",
            ),
            (Medium, "Opened external link in default browser."),
            (Medium, "Could not open external link https://example.com."),
            (Medium, "Missing Note is unresolved. Cannot open."),
            (Medium, "Opened Help in your default browser."),
            (Medium, "Could not open Help."),
            (High, "Opened notes.md."),
            (Medium, "Citation is not loaded yet."),
            (Medium, "No resolved embed at cursor."),
            (Medium, "No embed at cursor."),
            (Medium, "Could not find that heading."),
            (Medium, "Could not scroll to Roadmap."),
            (Medium, "Scrolled to Roadmap."),
            (Medium, "Scrolled to notes.md, line 40."),
            (Medium, "Opened notes.md, line 40."),
            (Medium, "Opened notes.md."),
            (Medium, "Showing notes."),
            (
                High,
                "Cannot toggle task. The editor has unsaved changes in notes.md. Save the note first.",
            ),
            (
                Medium,
                "Toggle blocked. notes.md was modified externally. Resolve in the dialog.",
            ),
            (Medium, "Tasks review. Open tasks."),
            (Medium, "Filter set to All tasks."),
            (Medium, "Saved notes.md."),
            (
                Medium,
                "Save blocked. notes.md was modified externally. Resolve in the dialog.",
            ),
            (High, "Restored version from July 19, 2026 at 9:41 AM."),
            (High, "Restored notes.md."),
            (High, "Restored notes.md as notes-restored.md."),
            (Medium, "Open a note to print."),
            (Medium, "Print dialog opened for notes.md."),
            (Medium, "Checking 1,024 selected items before Move."),
            (Medium, "Copied."),
            (
                Medium,
                "Sidebar settings still use defaults. the file is malformed.",
            ),
            (
                Medium,
                "Sidebar settings reloaded. Some pinned notes or sort overrides may still reference old locations.",
            ),
            (Medium, "Sidebar settings reloaded."),
            (Medium, "Vault closed. Returned to the welcome screen."),
            (
                Medium,
                "All changes saved. Vault closed. Returned to the welcome screen.",
            ),
            (
                Medium,
                "Changes discarded. Vault closed. Returned to the welcome screen.",
            ),
            (Medium, "Properties updated."),
            (Medium, "Property tags updated."),
            (Medium, "Property tags deleted."),
            (
                Medium,
                "Property edit blocked. notes.md was modified externally. Resolve in the dialog.",
            ),
            (
                High,
                "Properties source not applied: the YAML does not parse",
            ),
            (High, "Property edit failed: io error"),
            (Medium, "Properties reloaded."),
            (
                High,
                "Properties reloaded. The note body also changed externally; saving it will require conflict resolution.",
            ),
            (High, "The note changed again."),
            (High, "The note changed while saving."),
            (High, "Properties could not be reloaded: io error"),
            (Medium, "Retained property update copied."),
            (
                High,
                "The saved property update in notes could not be verified. Reopen the note to copy or resolve the retained update.",
            ),
            (
                Medium,
                "Using the current saved properties. The retained update was discarded.",
            ),
            (High, "The retained property update could not be reapplied."),
            (
                High,
                "Slate still could not reload the saved property update. io error",
            ),
            (
                High,
                "Slate couldn\u{2019}t load the current properties. The retained update is still available. io error",
            ),
            (High, "Add property"),
            (Medium, "Source changes discarded."),
            (High, "Bulk rename property"),
            (High, "Some open notes could not be reloaded."),
            (High, "Rename failed: io error"),
            (Medium, "3 renamed, 1 skipped, 0 failed."),
            (Medium, "1 file will be renamed, 0 skipped, 0 errors."),
            (Medium, "3 files will be renamed, 2 skipped, 0 errors."),
            (Medium, "Duplicate applies to files only."),
            (Medium, "Math speech style: ClearSpeak."),
            (Medium, "Math verbosity: Verbose."),
            (Medium, "Math braille code: Nemeth."),
            (Medium, "Code preamble verbosity: Concise."),
            (Medium, "Editor text size 110 percent."),
            (Medium, "Check spelling while typing on."),
            (Medium, "Check spelling while typing off."),
            (Medium, "Citation style: APA."),
            (Medium, "Citations, 1 citation."),
            (Medium, "Citations, 3 citations."),
            (Medium, "Outline, 1 heading."),
            (Medium, "Outline, 5 headings."),
            (Medium, "File list, 1 item"),
            (Medium, "File list, 12 items"),
            (Medium, "4 items selected"),
            (Medium, "1 item selected"),
            (Medium, "No items selected"),
            (Medium, "Selected: Archive, folder"),
            (Medium, "Selected: notes"),
            (Medium, "2 recent files"),
            (Medium, "1 recent file"),
            (Medium, "0 recent files"),
            (Medium, "No files matching \"zzz\""),
            (Medium, "2 files matching \"foo\""),
            (Medium, "1 file matching \"foo\""),
            (Medium, "Selected: Save"),
            (
                Medium,
                "Selected: Save. Unavailable: A structural operation is in progress.",
            ),
            (Medium, "No commands match \"zzz\""),
            (Medium, "1 command matching \"save\""),
            (Medium, "4 commands matching \"e\""),
            (High, "Save failed: disk full"),
            (High, "Save failed."),
            (High, "Command not found: slate.nope"),
            (High, "A structural operation is in progress."),
            (Medium, "Recent search: fox"),
            (Medium, "2 recent files"),
            (Medium, "1 recent file"),
            (Medium, "2 files matching \"foo\""),
            (Medium, "1 file matching \"foo\""),
            (Medium, "No files matching \"zzz\""),
            (Medium, "Base view as cards."),
            (Medium, "Base view switcher. 1 view."),
            (Medium, "Base view switcher. 2 views."),
            (Medium, "New Bases query builder."),
            (Medium, "Editing filters for Table."),
            (
                Medium,
                "Base filters could not be opened in the builder: io error",
            ),
            (Medium, "Base preview failed: bad expression"),
            (Medium, "Saved builder changes to view."),
            (Medium, "Base view could not be saved: io error"),
            (Medium, "Enter a saved query name before saving."),
            (Medium, "Saved query Open tasks."),
            (Medium, "Saved query could not be created: io error"),
            (Medium, "Updated saved query Open tasks."),
            (Medium, "Saved query could not be updated: io error"),
            (Medium, "Base view: Cards."),
            (Medium, "Base sort could not be saved: io error"),
            (Medium, "Base refreshed."),
            (Medium, "Base: Reading"),
            (Medium, "Base: Reading, view: Table"),
            (Medium, "Base: Reading, view: Table, quick filter: CAFE"),
            (Medium, "12 results."),
            (Medium, "12 results. Base: Reading, quick filter: CAFE."),
            (Medium, "0 of 0 results"),
            (Medium, "1 of 1 result"),
            (Medium, "1 of 2 results"),
            (Medium, "Sort 1 cannot be moved."),
            (Medium, "Sort 1 is already first."),
            (Medium, "Sort 2 is already last."),
            (Medium, "Sort 1 moved down to position 2 of 3."),
            (Medium, "Status column moved up to position 1 of 3."),
            (Medium, "Preview not loaded."),
            (Medium, "Preview loading."),
            (Medium, "12 results."),
            (Medium, "12 results. First result: Alpha"),
            (Medium, "12 results. First result: Alpha"),
            (Medium, "Preview failed: invalid expression"),
            (Medium, "Sorted by Status, ascending"),
            (Medium, "Sorted by Status, descending"),
            (Medium, "Saved sort by Status, ascending."),
            (Medium, "Saved sort by Status, descending."),
            (Medium, "Saved query Open tasks is no longer available."),
            (Medium, "Saved query is no longer available."),
            (Medium, "Queries could not be refreshed: io error"),
            (Medium, "Editing Open tasks in builder."),
            (Medium, "Saved query could not be edited: io error"),
            (Medium, "Enter a saved query name before renaming."),
            (Medium, "Renamed saved query to Open tasks."),
            (Medium, "Saved query could not be renamed: io error"),
            (Medium, "Deleted saved query."),
            (Medium, "Saved query could not be deleted: io error"),
            (Medium, "Choose a .base path before exporting."),
            (Medium, "Exported saved query as Open tasks.base."),
            (Medium, "Saved query could not be exported: io error"),
            (Medium, "Choose a path inside the vault."),
            (Medium, "Enter a dashboard name before saving."),
            (Medium, "Saved dashboard Reading."),
            (Medium, "Dashboard could not be saved: io error"),
            (Medium, "Updated dashboard Reading."),
            (Medium, "Dashboard could not be updated: io error"),
            (Medium, "Dashboard section changed; reload and try again."),
            (Medium, "Dashboard section could not be removed: io error"),
            (Medium, "Dashboard section could not be replaced: io error"),
            (Medium, "Deleted dashboard."),
            (Medium, "Dashboard could not be deleted: io error"),
            (Medium, "Dashboard could not be edited: io error"),
            (Medium, "Dashboard is no longer available."),
            (Medium, "Base dock updated for active note."),
            (Medium, "Copied link to Reading."),
            (Medium, "Backlinks for Reading."),
            (Medium, "Base view could not be copied: No active base."),
            (Medium, "Copied base view as Markdown."),
            (Medium, "Base view could not be copied: io error"),
            (Medium, "Select a base row first."),
            (
                Medium,
                "No editable property is available for the selected row.",
            ),
            (Medium, "read-only: file metadata"),
            (Medium, "read-only: computed"),
            (Medium, "Saved. Status: Done"),
            (Medium, "Saved. Status: empty"),
            (Medium, "Saved. Row no longer matches this view"),
            (Medium, "Base edit failed: io error"),
            (Medium, "Edit canceled."),
            (Medium, "Exported base view."),
            (Medium, "Base view could not be exported: io error"),
            (Medium, "Converted Dataview block to .base."),
            (Medium, "Dataview conversion could not be saved: io error"),
            (Medium, "Export canceled."),
            (Medium, "Must be a finite number."),
            (Medium, "Must be a whole number."),
            (Medium, "Must be a finite decimal number."),
            (Medium, "Must be true or false."),
            (Medium, "Date must be YYYY-MM-DD."),
            (Medium, "Updated: 1 note."),
            (Medium, "Dataview conversion failed: unsupported query"),
            (Medium, "Insert citation lands in V1.x. See Milestone L."),
            (
                Medium,
                "Walk through citations. Switch to the Citations sidebar tab and arrow through the list.",
            ),
            (Medium, "Code copied."),
            (Medium, "No next heading."),
            (Medium, "No previous level 2 heading."),
            (Medium, "No next link."),
            (Medium, "No previous list."),
            (Medium, "No next table."),
            (Medium, "No previous embed."),
            (Medium, "No next code block."),
            (Medium, "Lists and tasks, level 2 heading."),
            (Medium, "Target Note, link."),
            (Medium, "first bullet, list."),
            (Medium, "column a, table."),
            (Medium, "Embedded note Target Note."),
            (Medium, "fn spoken_interior() -> usize { 42 }, code block."),
            (Medium, "No next math."),
            (
                Medium,
                "x equals negative b plus or minus the square root of b squared \
                 minus 4 a c, over 2 a, math.",
            ),
            (Medium, "No next diagram."),
            (Medium, "Flowchart with 3 steps, diagram."),
            (Medium, "Embed."),
            (Medium, "Sorted by Status, ascending"),
            (Medium, "Sorted by Due, descending"),
            (Medium, "Ship the plan. Status: Open. Due: Friday"),
            (Medium, "Ship the plan. Status: Open"),
            (Medium, "Done reviewing. Status: Open"),
            (Medium, "Substatus: Opening. Status: Open"),
            (Medium, "Status: Open Questions remain. Status: Open"),
            (Medium, "Status: Open"),
            (Medium, "Group: Open, 1 row"),
            (Medium, "Group: Done, 12 rows. Summary: Count: 12"),
            (
                Medium,
                "Template picker opened. No templates found. \
                 Add a Markdown file to the configured template folder.",
            ),
            (Medium, "Template picker opened. 1 template available."),
            (Medium, "Template picker opened. 7 templates available."),
            (High, "Created Meeting 2026-08-20.md from Meeting."),
            (High, "Composed by a host engine."),
            // --- Canvas (W6-1 0a, #745) ---
            (Medium, "Research"),
            (Medium, "Text card \"Research\", 2 of 5 in Q3"),
            (
                Medium,
                "Text card \"Research\", 2 of 5 in Q3, 3 connections, red, marked",
            ),
            (Medium, "Group \"Q3\", 1 of 3 in canvas"),
            (
                Medium,
                "File card \"Notes.md\", 1 of 1 in canvas, 1 connection",
            ),
            (Medium, "Entering group \"Q3\", 4 cards"),
            (Medium, "Entering group \"Solo\", 1 card"),
            (Medium, "Leaving group \"Q3\""),
            (Medium, "No canvas view to act on."),
            (
                Medium,
                "Connects to Text card \"Ideas\", labelled \"supports\"",
            ),
            (Medium, "Connected from Text card \"Research\""),
            (Medium, "Linked with Group \"Q3\""),
            (Medium, "Linked with Link card \"example.com\""),
            (
                Medium,
                "Path: Research, then Ideas, then Draft. End of path — 3 cards visited.",
            ),
            (Medium, "Alone on the canvas"),
            (Medium, "Below \"Research\""),
            (
                Medium,
                "Below \"Research\", right of \"Ideas\". Overlapping another card",
            ),
            (Medium, "Above \"Ideas\". Clear of overlaps"),
            (Medium, "320 by 200"),
            (Medium, "Resized to default size: 260 by 140"),
            (
                Medium,
                "Resized to fit to content: 260 by 88. Overlapping another card",
            ),
            (Medium, "Minimum size."),
            (
                Medium,
                "Move mode — \"Research\". Arrows to move, Shift for big steps, Return to place, Escape to cancel.",
            ),
            (
                Medium,
                "Move mode — 3 cards. Arrows to move, Shift for big steps, Return to place, Escape to cancel.",
            ),
            (
                Medium,
                "Resize mode — \"Research\". Left and Right arrows change width, Up and Down change height, Return to apply, Escape to cancel.",
            ),
            (
                Medium,
                "Connect mode — \"Research\". Navigate to the target with the usual movements, Return to connect, Escape to cancel.",
            ),
            (
                High,
                "Move mode is active. Return to commit or Escape to cancel first.",
            ),
            (Medium, "Placed \"Research\"."),
            (Medium, "Placed 3 cards."),
            (Medium, "Resized \"Research\"."),
            (Medium, "Move ended — nothing changed."),
            (Medium, "Resize ended — nothing changed."),
            (Medium, "Connect ended — no target chosen."),
            (Medium, "Move cancelled."),
            (Medium, "Move cancelled — card returned."),
            (Medium, "Move cancelled — cards returned."),
            (Medium, "Resize cancelled."),
            (Medium, "Resize cancelled — size restored."),
            (Medium, "Connect cancelled."),
            (Medium, "Connect cancelled — back at \"Research\"."),
            (Medium, "Created text card \"New idea\" below \"Research\""),
            (Medium, "Created group \"Q3\" right of \"Research\""),
            (Medium, "Created file card \"Notes.md\" above \"Research\""),
            (
                Medium,
                "Created link card \"example.com\" left of \"Research\"",
            ),
            (
                Medium,
                "Created text card \"Untitled\" at the canvas origin",
            ),
            (Medium, "Created canvas \"Roadmap\"."),
            (
                Medium,
                "Created connected card below \"Research\" — connected from \"Research\".",
            ),
            (
                Medium,
                "Connected \"Research\" to \"Ideas\", labelled \"supports\".",
            ),
            (Medium, "Connected \"Research\" to \"Ideas\"."),
            (Medium, "Connection updated, labelled \"supports\"."),
            (Medium, "Connection updated."),
            (Medium, "Moved into group \"Q3\"."),
            (Medium, "Removed from group \"Q3\"."),
            (Medium, "Set \"Research\" to red."),
            (Medium, "Set \"Research\" to no color."),
            (Medium, "Renamed group to \"Q3\"."),
            (Medium, "Updated \"Research\"."),
            (Medium, "\"Research\" now points at notes/research.md."),
            (Medium, "Moved \"Research\" below \"Ideas\"."),
            (Medium, "Duplicated \"Research\" right of \"Research\"."),
            (Medium, "Aligned \"Research\" with \"Ideas\"."),
            (
                Medium,
                "Converted to note notes/research.md. The card now points at it.",
            ),
            (Medium, "Deleted Text card \"Research\" — ⌘Z to undo"),
            (Medium, "Deleted Text card \"Research\""),
            (Medium, "Ungrouped Group \"Q3\" — cards kept — ⌘Z to undo"),
            (Medium, "Deleted 3 cards — Ctrl+Z to undo"),
            (Medium, "Deleted 1 card — ⌘Z to undo"),
            (
                Medium,
                "Deleted connection to \"Ideas\", labelled \"supports\" — ⌘Z to undo",
            ),
            (Medium, "Deleted connection from \"Research\""),
            (Medium, "Deleted connection with \"Q3\" — ⌘Z to undo"),
            (Medium, "Moved 3 cards below \"Research\"."),
            (Medium, "Set 3 cards to cyan."),
            (Medium, "Set 1 card to no color."),
            (Medium, "Grouped 3 cards into \"Q3\"."),
            (Medium, "Duplicated 2 cards — one undo restores."),
            (Medium, "Marked \"Research\". 2 marked."),
            (Medium, "Unmarked \"Research\". 1 marked."),
            (Medium, "No marks."),
            (Medium, "Cleared 3 marks."),
            (Medium, "3 cards match."),
            (Medium, "1 card match."),
            (Medium, "Filter cleared — 40 cards."),
            (Medium, "Filter cleared — 1 card."),
            (Medium, "Zoom 100 percent."),
            (Medium, "Fit canvas. Zoom 80 percent."),
            (Medium, "Zoomed to selection. Zoom 150 percent."),
            (Medium, "Viewport follows selection."),
            (Medium, "Viewport stays put."),
            (Medium, "Canvas outline view."),
            (Medium, "Canvas table view."),
            (Medium, "Canvas visual view."),
            (Medium, "Undid: move \"Research\""),
            (Medium, "Redid: move \"Research\""),
            (Medium, "Undo Delete \"My Card\""),
            (Medium, "Redo"),
            (Medium, "Nothing selected."),
            (Medium, "No marks."),
            (Medium, "Not a group."),
            (Medium, "Not a text card."),
            (Medium, "Not a file card."),
            (Medium, "This canvas has no groups."),
            (Medium, "This vault has no notes yet."),
            (Medium, "This vault has no media files."),
            (Medium, "This vault has no files to point at."),
            (Medium, "Only text cards convert to notes."),
            (Medium, "The selected card has no connections."),
            (Medium, "Pick a card outside the moving set."),
            (Medium, "Pick a different card to connect to."),
            (Medium, "No changes."),
            (Medium, "Canvas is not readable."),
            (Medium, "Canvas is empty."),
            (Medium, "End of canvas."),
            (Medium, "Start of canvas."),
            (Medium, "At canvas level."),
            (Medium, "No cards match the filter."),
            (Medium, "Nothing to undo."),
            (Medium, "Nothing to redo."),
            (Medium, "Group \"Q3\" is empty."),
            (Medium, "No outgoing path from \"Research\"."),
            (Medium, "\"Research\" is not in a group."),
            (Medium, "No outgoing connection."),
            (Medium, "No outgoing connection 2."),
            (Medium, "No incoming connection."),
            (
                High,
                "A move or resize is in progress. Return to place it or Escape to cancel first.",
            ),
            (
                High,
                "Undo blocked: the canvas changed on disk. Reload it and try again.",
            ),
            (
                High,
                "Redo blocked: the canvas changed on disk. Reload it and try again.",
            ),
            (High, "The link could not be opened."),
            (High, "Aligning would overlap another card — not moved."),
            (High, "That doesn't look like a URL."),
            (High, "The card's text could not be read."),
            (High, "The note path must end in .md."),
            (High, "No free space inside \"Q3\"."),
            (High, "notes/research.md already exists. Pick another name."),
            (
                High,
                "notes/research.md already exists on disk. Pick another name.",
            ),
            (
                High,
                "Could not read the card text: The card text is unavailable.",
            ),
            (High, "Could not create notes/research.md: io error"),
            (
                High,
                "Created notes/research.md, but could not retarget the card: io error",
            ),
            (High, "Heading Roadmap was not found in notes.md."),
            (
                High,
                "Canvas could not be reopened. The previous snapshot is read-only. The file moved.",
            ),
            (High, "New card failed: the file is read-only"),
            (High, "New group failed: the file is read-only"),
            (High, "New canvas failed: the file is read-only"),
            (High, "Move failed: the file is read-only"),
            (High, "Placement failed: the file is read-only"),
            (High, "Align failed: the file is read-only"),
            (High, "Create failed: the file is read-only"),
            (High, "Remove failed: the file is read-only"),
            (High, "Duplicate failed: the file is read-only"),
            (High, "Create connected card failed: the file is read-only"),
            (High, "Canvas action failed: the file is read-only"),
            (High, "Where am I failed: the file is read-only"),
            (
                High,
                "The canvas changed on disk. Reload it to continue — your action was not applied.",
            ),
            (
                High,
                "media/diagram.png is missing from the vault. Use Locate File to repoint this card.",
            ),
            (Medium, "Opened Notes.md in its default app."),
            (Medium, "Opened example.com in your browser."),
            (
                Medium,
                "This canvas is still opening. Wait for it to finish before making changes.",
            ),
            (
                Medium,
                "This canvas is reopening. Wait for it to finish before making changes.",
            ),
            (
                Medium,
                "This canvas could not be reopened. Choose Retry before making changes.",
            ),
            (
                Medium,
                "This canvas is no longer available. Copy any draft before closing.",
            ),
            (
                Medium,
                "This canvas is read-only because it could not be opened safely.",
            ),
            (
                Medium,
                "This canvas is no longer available. Copy your draft before closing the editor.",
            ),
            (
                Medium,
                "Canvas loaded. 3 unsupported items are preserved in the file but not shown.",
            ),
            (
                Medium,
                "Canvas loaded. 1 unsupported item are preserved in the file but not shown.",
            ),
            (
                Medium,
                "Canvas is empty. Press Option Command N to create your first card. Every other canvas action is in the Command Palette, Command Shift P.",
            ),
            (
                Medium,
                "Text card \"Research\", in Quarter › Q3, 2 of 5, 3 connections (1 in, 2 out), red, marked, Move mode, 3 of 40 shown",
            ),
            (
                Medium,
                "Text card \"Loose\", at canvas level, 1 of 1, 1 connection (1 in, 0 out)",
            ),
            // --- Cardinality boundary witnesses (contract 0a-14) ---
            (Medium, "Path: Research. End of path — 1 card visited."),
            (Medium, "End of path — 0 cards visited."),
            (Medium, "Moved 1 card below \"Research\"."),
            (Medium, "Duplicated 1 card — one undo restores."),
            (
                Medium,
                "Move mode — 1 card. Arrows to move, Shift for big steps, Return to place, Escape to cancel.",
            ),
            (Medium, "Placed 1 card."),
            (Medium, "Grouped 1 card into \"Q3\"."),
            (Medium, "Cleared 1 mark."),
            // Zero witnesses (host-reachable zeros only).
            (
                Medium,
                "Text card \"Loose\", 1 of 1 in canvas, 0 connections",
            ),
            (Medium, "Unmarked \"Research\". 0 marked."),
            (Medium, "Duplicated 0 cards — one undo restores."),
            (Medium, "0 cards match."),
            (Medium, "Filter cleared — 0 cards."),
            (
                Medium,
                "Text card \"Loose\", at canvas level, 1 of 1, 0 connections (0 in, 0 out)",
            ),
            // Read-verb refusal during the reopening window.
            (Medium, "This canvas is reopening. Try again in a moment."),
            // …and during a first load or a prepared replacement.
            (Medium, "This canvas is loading. Try again in a moment."),
        ];

        let corpus = corpus();
        assert_eq!(
            corpus.len(),
            expected.len(),
            "corpus and golden table must stay in lockstep",
        );
        for (event, (priority, text)) in corpus.iter().zip(&expected) {
            assert_eq!(event.priority(), *priority, "priority for {event:?}");
            assert_eq!(event.render(), *text, "render for {event:?}");
        }
    }

    /// The committed §W-D corpus artifact (`tests/fixtures/a11y/corpus.json`)
    /// is generated FROM [`corpus()`] and pinned here: every entry is
    /// `{ "event": <Debug>, "priority": "medium"|"high", "text": <render> }`
    /// in corpus order. Regenerate deliberately with
    /// `SLATE_REGENERATE_FIXTURES=1 cargo test -p slate-core a11y` after a
    /// vocabulary change — the regenerating run FAILS by design (so the
    /// variable can never mask drift, even exported in CI); review the diff
    /// as the §W-D delta, then re-run without the variable to prove the pin.
    /// Both hosts consume this same file for their parity censuses —
    /// mac's `A11yCorpusCensusTests` and Windows' `A11yCorpusCensus`
    /// (#1114) — each constructing the mirrored events in corpus order
    /// and rendering them through the FFI; with all three green, both
    /// hosts speak identical announcements for identical events.
    #[test]
    fn committed_corpus_artifact_matches_the_vocabulary() {
        let rendered: Vec<serde_json::Value> = corpus()
            .iter()
            .map(|event| {
                serde_json::json!({
                    "event": format!("{event:?}"),
                    "priority": match event.priority() {
                        A11yPriority::Medium => "medium",
                        A11yPriority::High => "high",
                    },
                    "text": event.render(),
                })
            })
            .collect();
        let mut expected = serde_json::to_string_pretty(&rendered).expect("corpus serializes");
        expected.push('\n');

        let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("../../tests/fixtures/a11y/corpus.json");
        if std::env::var("SLATE_REGENERATE_FIXTURES").as_deref() == Ok("1") {
            std::fs::create_dir_all(path.parent().unwrap()).expect("fixture dir");
            std::fs::write(&path, &expected).expect("write corpus fixture");
            // A regenerating run must never double as a passing pin: the
            // comparison below would trivially succeed against the file just
            // written, so exporting the variable (say, in CI) would silence
            // this test forever. Fail loudly instead.
            panic!(
                "regenerated {path:?} — review the diff as a §W-D change, \
                 then re-run without SLATE_REGENERATE_FIXTURES"
            );
        }
        let committed = std::fs::read_to_string(&path).unwrap_or_else(|_| {
            panic!(
                "missing {path:?} — run SLATE_REGENERATE_FIXTURES=1 \
                 cargo test -p slate-core a11y"
            )
        });
        assert_eq!(
            committed.replace("\r\n", "\n"),
            expected,
            "corpus artifact drifted from the vocabulary — regenerate \
             deliberately and review the diff as a §W-D change",
        );
    }

    /// The Debug head of an event — its variant name.
    fn variant_of(event: &A11yEvent) -> String {
        format!("{event:?}")
            .chars()
            .take_while(char::is_ascii_alphanumeric)
            .collect()
    }

    /// The INNER variant name of a canvas event, or `None` for
    /// everything else. The family nests under one top-level variant
    /// (`Canvas { event: … }`), so the head alone says "canvas" and
    /// nothing more.
    fn canvas_variant_of(event: &A11yEvent) -> Option<String> {
        let debug = format!("{event:?}");
        let at = debug.strip_prefix("Canvas { event: ")?;
        Some(at.chars().take_while(char::is_ascii_alphanumeric).collect())
    }

    /// The variant names declared by an enum in this file. The corpus
    /// is positional and hand-maintained, so "I added the variant and
    /// forgot the corpus entry" is the mistake that actually happens —
    /// and it is invisible until a host names the case. Same parser
    /// shape as slate-uniffi's mirror tripwire.
    fn declared_variants(enum_name: &str) -> std::collections::BTreeSet<String> {
        let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("src/a11y.rs");
        let source = std::fs::read_to_string(&path).expect("a11y source");
        let needle = format!("pub enum {enum_name} {{");
        let decl = source
            .find(&needle)
            .unwrap_or_else(|| panic!("{enum_name} declaration"));
        let open = decl + source[decl..].find('{').expect("opening brace");
        let mut depth = 0usize;
        let mut end = source.len();
        for (offset, ch) in source[open..].char_indices() {
            match ch {
                '{' => depth += 1,
                '}' => {
                    depth -= 1;
                    if depth == 0 {
                        end = open + offset;
                        break;
                    }
                }
                _ => {}
            }
        }
        let mut names = std::collections::BTreeSet::new();
        let mut depth = 0usize;
        for line in source[open + 1..end].lines() {
            let trimmed = line.trim();
            if depth == 0 && !trimmed.starts_with("//") && !trimmed.starts_with('#') {
                // The leading identifier of a variant line. Taking the
                // HEAD rather than the whole trimmed line is what makes
                // the single-line payload form (`Card { title: String },`
                // — how the small parameter enums are written) parse the
                // same as the multi-line one.
                let name: String = trimmed
                    .chars()
                    .take_while(char::is_ascii_alphanumeric)
                    .collect();
                let tail = trimmed[name.len()..].trim_start();
                let delimited = tail.is_empty()
                    || tail.starts_with(',')
                    || tail.starts_with('{')
                    || tail.starts_with('(');
                if !name.is_empty()
                    && name.starts_with(|c: char| c.is_ascii_uppercase())
                    && delimited
                {
                    names.insert(name);
                }
            }
            depth += trimmed.matches('{').count();
            depth -= trimmed.matches('}').count().min(depth);
        }
        names
    }

    /// The nested-enum arm a corpus entry selected, read out of its
    /// Debug string (`… CanvasStatus { note: NotAGroup }` → `NotAGroup`).
    /// `Option`-carried parameters unwrap through `Some(`; a `None`
    /// contributes nothing, because "absent" is not an arm.
    ///
    /// Scoped by (variant, field) rather than by field alone: three
    /// different enums ride a field called `verb`, two ride `reason`,
    /// and two ride `target`, so field-only scoping would let one
    /// enum's coverage vouch for another's.
    fn nested_arms(sites: &[(&str, &str)]) -> std::collections::BTreeSet<String> {
        let mut arms = std::collections::BTreeSet::new();
        for (variant, field) in sites {
            let prefix = format!("{field}: ");
            for event in corpus()
                .iter()
                .filter(|event| canvas_variant_of(event).as_deref() == Some(*variant))
            {
                let debug = format!("{event:?}");
                let Some(at) = debug.find(&prefix) else {
                    continue;
                };
                let rest = &debug[at + prefix.len()..];
                let rest = rest.strip_prefix("Some(").unwrap_or(rest);
                let arm: String = rest
                    .chars()
                    .take_while(char::is_ascii_alphanumeric)
                    .collect();
                if !arm.is_empty() && arm != "None" {
                    arms.insert(arm);
                }
            }
        }
        arms
    }

    /// Every closed nested enum the canvas family carries as a
    /// parameter, with the (variant, field) sites it is reached
    /// through. Eighteen of them — the whole set, not a sample: an arm
    /// added to ANY of these without a corpus entry ships a string no
    /// golden, no artifact entry and no host census covers.
    const CANVAS_NESTED_ENUMS: &[(&str, &[(&str, &str)])] = &[
        (
            "CanvasVerbosity",
            &[
                ("CanvasMovedTo", "verbosity"),
                ("CanvasDeleted", "verbosity"),
            ],
        ),
        (
            "CanvasOverlapTransition",
            &[
                ("CanvasMoveRelative", "overlap"),
                ("CanvasResizeGeometry", "overlap"),
            ],
        ),
        (
            "CanvasMode",
            &[
                ("CanvasModeEntered", "mode"),
                ("CanvasModeRejected", "active_mode"),
                ("CanvasModeEndedWithoutEffect", "mode"),
                ("CanvasModeCancelled", "mode"),
                ("CanvasWhereAmI", "mode"),
            ],
        ),
        (
            "CanvasModeObject",
            &[
                ("CanvasModeEntered", "object"),
                ("CanvasModeCommitted", "object"),
            ],
        ),
        ("CanvasTransientVerb", &[("CanvasModeCommitted", "verb")]),
        (
            "CanvasModeRestoration",
            &[("CanvasModeCancelled", "restoration")],
        ),
        ("CanvasPlaceVerb", &[("CanvasCardPlaced", "verb")]),
        ("CanvasOpenTarget", &[("CanvasOpened", "target")]),
        ("CanvasResizePreset", &[("CanvasResizeGeometry", "preset")]),
        ("CanvasSurfaceKind", &[("CanvasSurfaceShown", "surface")]),
        ("CanvasZoomContext", &[("CanvasZoom", "context")]),
        ("CanvasDeleteTarget", &[("CanvasDeleted", "target")]),
        ("CanvasFailedAction", &[("CanvasActionFailed", "action")]),
        (
            "CanvasMutationRefusal",
            &[("CanvasMutationRefused", "reason")],
        ),
        (
            "CanvasHistoryVerb",
            &[
                ("CanvasHistoryApplied", "verb"),
                ("CanvasUndoMenuTitle", "verb"),
            ],
        ),
        ("CanvasStatusNote", &[("CanvasStatus", "note")]),
        ("CanvasBlockedReason", &[("CanvasBlocked", "reason")]),
        ("CanvasFilterState", &[("CanvasWhereAmI", "filter")]),
    ];

    /// Five-place rule, first place: a canvas variant — or a closed-set
    /// ARM of one — that never reaches [`corpus()`] is pinned by
    /// nothing: not the golden above, not the committed artifact, and
    /// not either host census.
    #[test]
    fn every_canvas_variant_and_arm_is_represented_in_the_corpus() {
        let declared = declared_variants("CanvasA11yEvent");
        assert!(
            declared.len() > 40,
            "parsed only {} canvas variants — the parser broke, not the vocabulary",
            declared.len()
        );
        let represented: std::collections::BTreeSet<String> =
            corpus().iter().filter_map(canvas_variant_of).collect();
        let missing: Vec<&String> = declared.difference(&represented).collect();
        assert!(
            missing.is_empty(),
            "these canvas variants never appear in corpus(), so no golden, no \
             artifact entry, and neither host census covers them: {missing:?}"
        );

        for (enum_name, sites) in CANVAS_NESTED_ENUMS {
            let declared = declared_variants(enum_name);
            assert!(
                !declared.is_empty(),
                "parsed no arms of {enum_name} — the parser broke, not the vocabulary"
            );
            let covered = nested_arms(sites);
            let missing: Vec<&String> = declared.difference(&covered).collect();
            assert!(
                missing.is_empty(),
                "{enum_name} arms with no corpus entry, so their shipped string is \
                 pinned nowhere: {missing:?}"
            );
        }
    }

    /// The nested-enum table above must not silently fall behind the
    /// module: every closed `Canvas*` parameter enum declared here is
    /// listed, so adding one without a coverage site fails HERE rather
    /// than leaving its arms unpinned forever.
    #[test]
    fn every_canvas_parameter_enum_is_listed_for_coverage() {
        let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("src/a11y.rs");
        let source = std::fs::read_to_string(&path).expect("a11y source");
        let declared: std::collections::BTreeSet<String> = source
            .lines()
            .filter_map(|line| line.strip_prefix("pub enum "))
            .filter_map(|rest| rest.split_whitespace().next())
            .filter(|name| name.starts_with("Canvas"))
            // The family enum itself is not a parameter of the family.
            .filter(|name| *name != "CanvasA11yEvent")
            .map(str::to_owned)
            .collect();
        let listed: std::collections::BTreeSet<String> = CANVAS_NESTED_ENUMS
            .iter()
            .map(|(name, _)| (*name).to_owned())
            .collect();
        assert_eq!(
            declared, listed,
            "CANVAS_NESTED_ENUMS is out of step with the declared canvas \
             parameter enums"
        );
        assert_eq!(listed.len(), 18, "the canvas family carries 18 closed sets");
    }

    /// What a canvas event says about CARDINALITY, if anything.
    ///
    /// Exhaustive at EVERY level. The outer variant is matched arm by
    /// arm, and so is every one of the eighteen closed parameter sets
    /// the family carries. The compiler therefore refuses this function
    /// the moment a variant, or an arm of any nested set, is added;
    /// nothing joins the family without its author declaring whether it
    /// speaks a count. That is the property three rounds of
    /// hand-maintained, hand-counted prose could not hold (contract
    /// 0a-14; round record, rule 4).
    ///
    /// The precise rule for `..`: it elides only fields whose types
    /// **cannot gain variants** — `String`, `u32`, `bool`,
    /// `Vec<String>`, and `Option` of those, whose two arms are fixed
    /// by the language. **Every parameter type that CAN gain variants
    /// is explicitly matched, with no exception** — this module's
    /// eighteen closed sets, and the three `core::canvas` sets the
    /// vocabulary reuses (`RelativeDesc`, `EdgeDirection`,
    /// `CanvasColor`), which cannot carry a count either but are
    /// matched through the helpers below rather than trusted.
    ///
    /// "Speaks" means THIS value renders the count, not that the
    /// variant sometimes can. `CanvasMovedTo` is the only arm whose
    /// count is conditional on another field — the connection clause
    /// rides at `Verbose` only — so a terse or standard moved-to speaks
    /// no count and cannot serve as its witness. (Swept: every other
    /// count reaches its template unconditionally. `CanvasMarksCleared`
    /// swaps template at zero, but zero is a count it does speak.)
    #[derive(Debug, Clone, Copy, PartialEq, Eq)]
    enum SpokenCardinality {
        /// A `u32` count reaches the template.
        Count(u32),
        /// A collection LENGTH reaches the template, so the arm has an
        /// empty case as well as a singular one.
        Length(usize),
    }

    /// The three `core::canvas` sets this vocabulary reuses. They name
    /// anchors, directions and colours, so none of them can carry a
    /// count — but each CAN gain a variant, so they are MATCHED rather
    /// than elided, and an arm added to any of them fails to compile
    /// here. That is what lets the record say every variant-bearing
    /// parameter type is matched, with no exception to remember.
    fn relative_speaks_no_count(relative: &RelativeDesc) {
        match relative {
            RelativeDesc::Below(_)
            | RelativeDesc::RightOf(_)
            | RelativeDesc::Above(_)
            | RelativeDesc::LeftOf(_)
            | RelativeDesc::AtOrigin => {}
        }
    }

    fn direction_speaks_no_count(direction: &EdgeDirection) {
        match direction {
            EdgeDirection::Outgoing
            | EdgeDirection::Incoming
            | EdgeDirection::Bidirectional
            | EdgeDirection::Undirected => {}
        }
    }

    fn color_speaks_no_count(color: &Option<CanvasColor>) {
        match color {
            Some(CanvasColor::Preset(_)) | Some(CanvasColor::Hex(_)) | None => {}
        }
    }

    fn spoken_cardinality(event: &CanvasA11yEvent) -> Option<SpokenCardinality> {
        use CanvasA11yEvent::*;
        use SpokenCardinality::{Count, Length};
        match event {
            // --- speaks a count ---------------------------------------
            CanvasMovedTo {
                verbosity,
                connection_count,
                ..
            } => match verbosity {
                CanvasVerbosity::Verbose => Some(Count(*connection_count)),
                // The connection clause is not rendered at all here, so
                // this value speaks no count.
                CanvasVerbosity::Terse | CanvasVerbosity::Standard => None,
            },
            CanvasWhereAmI {
                connection_count,
                mode,
                filter,
                ..
            } => {
                // `mode`/`filter` speak no count, but they are closed
                // sets and so are matched rather than elided.
                match mode {
                    Some(CanvasMode::Move | CanvasMode::Resize | CanvasMode::Connect) | None => {}
                }
                match filter {
                    CanvasFilterState::Inactive | CanvasFilterState::Active { .. } => {}
                }
                Some(Count(*connection_count))
            }
            CanvasGroupEntered { count, .. } => Some(Count(*count)),
            CanvasTracePathEnd { titles } => Some(Length(titles.len())),
            // The mode object is a shared clause, so BOTH arms speak
            // its count and both need their own witness — the corpus is
            // per event, not per clause.
            CanvasModeEntered { mode, object } => {
                match mode {
                    CanvasMode::Move | CanvasMode::Resize | CanvasMode::Connect => {}
                }
                match object {
                    CanvasModeObject::Cards { count } => Some(Count(*count)),
                    CanvasModeObject::Card { .. } => None,
                }
            }
            CanvasModeCommitted { verb, object } => {
                match verb {
                    CanvasTransientVerb::Move | CanvasTransientVerb::Resize => {}
                }
                match object {
                    CanvasModeObject::Cards { count } => Some(Count(*count)),
                    CanvasModeObject::Card { .. } => None,
                }
            }
            CanvasModeCancelled { mode, restoration } => {
                match mode {
                    CanvasMode::Move | CanvasMode::Resize | CanvasMode::Connect => {}
                }
                match restoration {
                    // The count is not interpolated here — only the noun
                    // agrees with it ("card returned" / "cards
                    // returned") — which is exactly the disagreement
                    // this test hunts.
                    CanvasModeRestoration::CardsReturned { count } => Some(Count(*count)),
                    CanvasModeRestoration::Unstated
                    | CanvasModeRestoration::SizeRestored
                    | CanvasModeRestoration::BackAt { .. } => None,
                }
            }
            CanvasDeleted {
                target, verbosity, ..
            } => {
                match verbosity {
                    CanvasVerbosity::Terse
                    | CanvasVerbosity::Standard
                    | CanvasVerbosity::Verbose => {}
                }
                match target {
                    CanvasDeleteTarget::Cards { count } => Some(Count(*count)),
                    CanvasDeleteTarget::Connection { direction, .. } => {
                        direction_speaks_no_count(direction);
                        None
                    }
                    CanvasDeleteTarget::Card { .. } | CanvasDeleteTarget::Group { .. } => None,
                }
            }
            CanvasBulkMoved { count, relative } => {
                relative_speaks_no_count(relative);
                Some(Count(*count))
            }
            CanvasBulkColorSet { count, color } => {
                color_speaks_no_count(color);
                Some(Count(*count))
            }
            CanvasGrouped { count, .. }
            | CanvasBulkDuplicated { count }
            | CanvasMarkToggled { count, .. }
            | CanvasMarksCleared { count } => Some(Count(*count)),
            CanvasFilterCount { matched } => Some(Count(*matched)),
            CanvasFilterCleared { total } => Some(Count(*total)),
            CanvasLoadedDegraded { skipped } => Some(Count(*skipped)),

            // --- speaks no count --------------------------------------
            // Each closed parameter set is still matched arm by arm, so
            // a count added to any of them lands here as a compile
            // error rather than as an unpinned string.
            CanvasMoveRelative { descs, overlap } => {
                // Carries a `Vec` but never says how long it is — it
                // joins the descriptions — so this is a Length-free
                // arm. Its elements are still matched.
                descs.iter().for_each(relative_speaks_no_count);
                match overlap {
                    Some(CanvasOverlapTransition::Onset)
                    | Some(CanvasOverlapTransition::Cleared)
                    | None => None,
                }
            }
            CanvasResizeGeometry {
                preset, overlap, ..
            } => {
                match preset {
                    Some(CanvasResizePreset::DefaultSize)
                    | Some(CanvasResizePreset::FitToContent)
                    | None => {}
                }
                match overlap {
                    Some(CanvasOverlapTransition::Onset)
                    | Some(CanvasOverlapTransition::Cleared)
                    | None => None,
                }
            }
            CanvasModeRejected { active_mode } => match active_mode {
                CanvasMode::Move | CanvasMode::Resize | CanvasMode::Connect => None,
            },
            CanvasModeEndedWithoutEffect { mode } => match mode {
                CanvasMode::Move | CanvasMode::Resize | CanvasMode::Connect => None,
            },
            CanvasCardPlaced { verb, relative, .. } => {
                match verb {
                    CanvasPlaceVerb::Moved | CanvasPlaceVerb::Duplicated => {}
                }
                relative_speaks_no_count(relative);
                None
            }
            CanvasCreated { relative, .. } | CanvasConnectedCardCreated { relative, .. } => {
                relative_speaks_no_count(relative);
                None
            }
            CanvasConnectionTraversed { direction, .. } => {
                direction_speaks_no_count(direction);
                None
            }
            CanvasColorSet { color, .. } => {
                color_speaks_no_count(color);
                None
            }
            CanvasOpened { target, .. } => match target {
                CanvasOpenTarget::DefaultApp | CanvasOpenTarget::Browser => None,
            },
            CanvasSurfaceShown { surface } => match surface {
                CanvasSurfaceKind::Outline
                | CanvasSurfaceKind::Table
                | CanvasSurfaceKind::Visual => None,
            },
            CanvasZoom { context, .. } => match context {
                Some(CanvasZoomContext::FitCanvas)
                | Some(CanvasZoomContext::ZoomedToSelection)
                | None => None,
            },
            CanvasHistoryApplied { verb, .. } | CanvasUndoMenuTitle { verb, .. } => match verb {
                CanvasHistoryVerb::Undo | CanvasHistoryVerb::Redo => None,
            },
            CanvasStatus { note } => match note {
                CanvasStatusNote::NothingSelected
                | CanvasStatusNote::NoMarks
                | CanvasStatusNote::NotAGroup
                | CanvasStatusNote::NotATextCard
                | CanvasStatusNote::NotAFileCard
                | CanvasStatusNote::NoGroups
                | CanvasStatusNote::NoNotesInVault
                | CanvasStatusNote::NoMediaInVault
                | CanvasStatusNote::NoFilesToPointAt
                | CanvasStatusNote::OnlyTextCardsConvert
                | CanvasStatusNote::NoConnections
                | CanvasStatusNote::PickOutsideMovingSet
                | CanvasStatusNote::PickDifferentTarget
                | CanvasStatusNote::NoChanges
                | CanvasStatusNote::NotReadable
                | CanvasStatusNote::Empty
                | CanvasStatusNote::EndOfCanvas
                | CanvasStatusNote::StartOfCanvas
                | CanvasStatusNote::AtCanvasLevel
                | CanvasStatusNote::NoCardsMatchFilter
                | CanvasStatusNote::NothingToUndo
                | CanvasStatusNote::NothingToRedo
                | CanvasStatusNote::GroupIsEmpty { .. }
                | CanvasStatusNote::NoOutgoingPath { .. }
                | CanvasStatusNote::NotInAGroup { .. }
                | CanvasStatusNote::NoConnection { .. }
                | CanvasStatusNote::Reopening
                | CanvasStatusNote::Loading => None,
            },
            CanvasBlocked { reason } => match reason {
                CanvasBlockedReason::ModeBusy
                | CanvasBlockedReason::UndoBlocked
                | CanvasBlockedReason::RedoBlocked
                | CanvasBlockedReason::LinkOpenFailed
                | CanvasBlockedReason::AlignWouldOverlap
                | CanvasBlockedReason::NotAUrl
                | CanvasBlockedReason::CardTextUnreadable
                | CanvasBlockedReason::NotePathMustEndInMd
                | CanvasBlockedReason::NoFreeSpaceInGroup { .. }
                | CanvasBlockedReason::NotePathExists { .. }
                | CanvasBlockedReason::NoteReadFailed { .. }
                | CanvasBlockedReason::NoteCreateFailed { .. }
                | CanvasBlockedReason::NoteRetargetFailed { .. }
                | CanvasBlockedReason::HeadingNotFound { .. }
                | CanvasBlockedReason::ReopenFailed { .. } => None,
            },
            CanvasActionFailed { action, .. } => match action {
                CanvasFailedAction::NewCard
                | CanvasFailedAction::NewGroup
                | CanvasFailedAction::NewCanvas
                | CanvasFailedAction::MoveIntoGroup
                | CanvasFailedAction::Placement
                | CanvasFailedAction::Align
                | CanvasFailedAction::Create
                | CanvasFailedAction::RemoveFromGroup
                | CanvasFailedAction::Duplicate
                | CanvasFailedAction::CreateConnectedCard
                | CanvasFailedAction::CanvasAction
                | CanvasFailedAction::WhereAmI => None,
            },
            CanvasMutationRefused { reason } => match reason {
                CanvasMutationRefusal::Opening
                | CanvasMutationRefusal::Reopening
                | CanvasMutationRefusal::RetargetFailed
                | CanvasMutationRefusal::Unavailable
                | CanvasMutationRefusal::ReadOnly
                | CanvasMutationRefusal::CardEditorUnavailable => None,
            },

            // No closed parameter set at all — plain data only.
            CanvasGroupLeft { .. }
            | CanvasResizeClamped
            | CanvasFileCreated { .. }
            | CanvasConnected { .. }
            | CanvasConnectionUpdated { .. }
            | CanvasMovedIntoGroup { .. }
            | CanvasRemovedFromGroup { .. }
            | CanvasRenamedGroup { .. }
            | CanvasCardUpdated { .. }
            | CanvasCardRetargeted { .. }
            | CanvasCardAligned { .. }
            | CanvasConvertedToNote { .. }
            | CanvasFollowSelectionToggled { .. }
            | CanvasViewportNoPane
            | CanvasSaveConflict
            | CanvasFileNotFound { .. }
            | CanvasEmptyOnboarding { .. } => None,
        }
    }

    /// Contract 0a-14, mechanically.
    ///
    /// Totality used to be pinned by a paragraph that counted arms and
    /// witnesses by hand; three consecutive adversarial rounds found
    /// three different miscounts in it (round record, rule 4), so the
    /// invariant lives here and the paragraph points at this test.
    ///
    /// **(a)** the record — the count-speaking arms, derived from the
    /// exhaustive classifier above and cross-checked against a written
    /// list so the change is legible in a diff.
    /// **(b)** each has a `corpus()` witness at exactly one; an arm
    /// speaking a collection length also has an empty-collection one;
    /// and an arm whose ZERO the host can reach has a zero witness
    /// (that reachability is a claim about mac, so it is declared, not
    /// derived — see `ZERO_REACHABLE`).
    /// **(c)** those boundary renderings carry no plural form, except
    /// the templates CR-3 pins, which are allow-listed as
    /// (arm, string) PAIRS and proved to render from that arm and
    /// nowhere else.
    /// **(d)** a lexical source scan of the canvas render section: no
    /// template may interpolate a count immediately before a hardcoded
    /// plural noun, and no bare plural-noun literal may sit outside a
    /// `plural` / `plural_len` / `counted` call.
    ///
    /// Between them these pin the BOUNDARY, not the whole domain:
    /// agreement is checked at one (and at empty/zero where reachable),
    /// and (d) routes count interpolations through the shared helpers.
    ///
    /// **(d) is a parser now, not a line scan** (contract 0b-16; the
    /// implementation is below in this file). Two powers the 0a-1
    /// version lacked, and this comment used to disclaim: the countable
    /// noun list is DERIVED — it is the set of `one`/`many` arguments
    /// this module's own `plural` / `plural_len` / `counted` call sites
    /// pass, so a noun is guarded by being pluralized somewhere, not by
    /// being typed into the test; and helper provenance is bound to the
    /// LITERAL, not to the line, so a line carrying a real `plural(`
    /// call no longer clears a hardcoded plural sitting beside it.
    /// `\`-continuations are joined the way rustc joins them, with a
    /// committed witness template proving the lexer builds the string
    /// rustc does, and raw strings are refused loudly rather than
    /// mis-lexed.
    ///
    /// It is still a check over THIS crate's source, not a proof:
    /// `ZERO_REACHABLE` stays declared because host reachability is a
    /// property of the mac call sites, and a runtime-assembled string or
    /// a noun pluralized nowhere in the module remain outside its reach.
    /// Those residuals are named in contract 0a-14, narrowed in place
    /// rather than dropped.
    #[test]
    fn canvas_count_speaking_arms_have_boundary_witnesses_and_agreement() {
        // (a) --------------------------------------------------------
        let listed: std::collections::BTreeSet<&str> = [
            "CanvasBulkColorSet",
            "CanvasBulkDuplicated",
            "CanvasBulkMoved",
            "CanvasDeleted",
            "CanvasFilterCleared",
            "CanvasFilterCount",
            "CanvasGroupEntered",
            "CanvasGrouped",
            "CanvasLoadedDegraded",
            "CanvasMarkToggled",
            "CanvasMarksCleared",
            "CanvasModeCancelled",
            "CanvasModeCommitted",
            "CanvasModeEntered",
            "CanvasMovedTo",
            "CanvasTracePathEnd",
            "CanvasWhereAmI",
        ]
        .into_iter()
        .collect();

        let mut speaking: std::collections::BTreeMap<String, Vec<(SpokenCardinality, String)>> =
            std::collections::BTreeMap::new();
        for entry in corpus() {
            let A11yEvent::Canvas { ref event } = entry else {
                continue;
            };
            if let Some(cardinality) = spoken_cardinality(event) {
                let name = canvas_variant_of(&entry).expect("canvas variant name");
                speaking
                    .entry(name)
                    .or_default()
                    .push((cardinality, entry.render()));
            }
        }
        let derived: std::collections::BTreeSet<&str> =
            speaking.keys().map(String::as_str).collect();
        assert_eq!(
            derived, listed,
            "the written list of count-speaking canvas arms is out of step with what \
             `spoken_cardinality` classifies over corpus() — add the arm here, then \
             give it the witnesses below"
        );

        // (b) --------------------------------------------------------
        // Arms whose count the MAC HOST can actually reach at zero.
        // Reachability is a property of the host's call sites, not of
        // this crate, so it cannot be derived here — it is declared,
        // with the reason, and re-checked when a host changes.
        const ZERO_REACHABLE: &[(&str, &str)] = &[
            ("CanvasMovedTo", "a card with no connections, at Verbose"),
            ("CanvasWhereAmI", "a card with no connections"),
            (
                "CanvasMarkToggled",
                "unmarking the last mark leaves zero marked",
            ),
            ("CanvasFilterCount", "a filter that matches nothing"),
            (
                "CanvasFilterCleared",
                "clearing the filter on an empty canvas",
            ),
            (
                "CanvasMarksCleared",
                "clearing with nothing marked (its own template)",
            ),
            (
                "CanvasBulkDuplicated",
                "a STALE selection: create selects the new card, undo \
                 removes it without reconciling `selection.selected`, \
                 and Duplicate's seed passes its non-empty guard while \
                 the collection it announces resolves to nothing",
            ),
            // NOT reachable, and so deliberately absent. The audit that
            // matters is not "is the verb guarded" but "is the guarded
            // collection the ANNOUNCED one" — `CanvasBulkDuplicated`
            // above is exactly the case where it is not. Re-checked
            // per verb: `CanvasDeleted`, `CanvasBulkColorSet` and
            // `CanvasGrouped` announce `canvasMarkedInOrder`, which is
            // already outline-filtered when the guard sees it;
            // `CanvasBulkMoved` and the mode arms announce the same
            // `canvasMovingSet` seed they guard (a stale id there
            // reaches ONE, not zero — CD-15). `CanvasGroupEntered`
            // counts a container holding the row just entered;
            // `CanvasTracePathEnd` seeds with the selected card;
            // `CanvasLoadedDegraded` only posts above zero.
        ];
        for (arm, _) in ZERO_REACHABLE {
            assert!(
                listed.contains(arm),
                "{arm} is declared zero-reachable but is not a count-speaking arm"
            );
        }

        for (arm, samples) in &speaking {
            assert!(
                samples.iter().any(|(c, _)| matches!(
                    c,
                    SpokenCardinality::Count(1) | SpokenCardinality::Length(1)
                )),
                "{arm} speaks a count but corpus() has no witness at ONE, so nothing \
                 pins its singular rendering (contract 0a-14). NOTE: a witness only \
                 counts if the event actually RENDERS the count — a non-Verbose \
                 `CanvasMovedTo` does not"
            );
            if samples
                .iter()
                .any(|(c, _)| matches!(c, SpokenCardinality::Length(_)))
            {
                assert!(
                    samples
                        .iter()
                        .any(|(c, _)| matches!(c, SpokenCardinality::Length(0))),
                    "{arm} speaks a collection LENGTH, so it also needs an \
                     empty-collection witness (contract 0a-14)"
                );
            }
            if let Some((_, why)) = ZERO_REACHABLE.iter().find(|(a, _)| a == arm) {
                assert!(
                    samples
                        .iter()
                        .any(|(c, _)| matches!(c, SpokenCardinality::Count(0))),
                    "{arm} can reach zero on the host ({why}) but corpus() has no \
                     witness at zero (contract 0a-14)"
                );
            }
        }

        // (c) --------------------------------------------------------
        // The shipped English defects CR-3 pins as verbatim, as
        // (arm, rendering) PAIRS. The pairing is enforced below: each
        // string must render from THAT arm and from nowhere else in the
        // corpus, so a second defective template cannot hide behind an
        // existing excuse, and a defect that moves arms fails here.
        const CR3_VERBATIM_DEFECTS: &[(&str, &str)] = &[
            // The plural rule is applied to the noun but the verb is
            // fixed.
            ("CanvasFilterCount", "1 card match."),
            // "item" is singular, "are" is not.
            (
                "CanvasLoadedDegraded",
                "Canvas loaded. 1 unsupported item are preserved in the file but not shown.",
            ),
        ];
        // A plural noun, or a verb agreeing with one, has no business
        // in a rendering whose count is one. The verbs are listed
        // because NEITHER CR-3 defect carries a plural noun — both are
        // correctly singular there — so a noun-only check would excuse
        // them silently.
        const PLURAL_AT_ONE: &[&str] =
            &["cards", "marks", "items", "connections", " are ", " match."];

        for (arm, text) in CR3_VERBATIM_DEFECTS {
            let sources: Vec<String> = corpus()
                .iter()
                .filter(|event| event.render() == *text)
                .filter_map(canvas_variant_of)
                .collect();
            assert_eq!(
                sources,
                vec![(*arm).to_owned()],
                "CR3_VERBATIM_DEFECTS pairs {text:?} with {arm}, but the corpus renders \
                 that exact string from {sources:?} — a carve-out excuses ONE template \
                 on ONE arm, so update the pair (or drop it, if CR-3 was fixed)"
            );
        }

        for (arm, samples) in &speaking {
            for (cardinality, text) in samples {
                if !matches!(
                    cardinality,
                    SpokenCardinality::Count(1) | SpokenCardinality::Length(1)
                ) {
                    continue;
                }
                if CR3_VERBATIM_DEFECTS
                    .iter()
                    .any(|(defect_arm, defect)| defect_arm == arm && defect == text)
                {
                    continue;
                }
                for token in PLURAL_AT_ONE {
                    assert!(
                        !text.contains(token),
                        "{arm} renders {text:?} at a count of one, which carries the \
                         plural form {token:?} — route the noun through \
                         plural()/plural_len()/counted() (contract 0a-14), or, if this \
                         is a deliberately preserved shipped defect, add the \
                         (arm, string) pair to CR3_VERBATIM_DEFECTS with a citation"
                    );
                }
            }
        }

        // (d) --------------------------------------------------------
        // The canvas templates and the helpers they call: from the
        // family's own impl block down to `corpus()`.
        //
        // W6-1 contract 0b-16 replaced the line-scoped scan 0a-1
        // shipped. This walks the module's string literals WITH the
        // call and argument position each one belongs to, so both of
        // that scan's declared artefacts are gone: the countable-noun
        // list is whatever the helper call sites actually pass, and a
        // noun literal is excused only when THAT literal is a helper's
        // noun argument — a line carrying a real `plural(` call no
        // longer vouches for a hardcoded plural sitting beside it.
        let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("src/a11y.rs");
        let source = std::fs::read_to_string(&path).expect("a11y source");
        let begin = source
            .find("impl CanvasA11yEvent {")
            .expect("canvas render impl");
        let end = source[begin..]
            .find("pub fn corpus()")
            .expect("corpus() terminates the render section")
            + begin;

        // Every helper takes `(count, one, many)`, so the noun forms
        // are arguments 1 and 2 of a call by one of these names.
        const PLURAL_HELPERS: &[&str] = &["plural", "plural_len", "counted"];
        const SINGULAR_ARG: usize = 1;
        const PLURAL_ARG: usize = 2;

        // The nouns this vocabulary counts, DERIVED from the calls that
        // count them rather than declared here: a noun enters these
        // sets by being pluralized somewhere in the module.
        let module_literals = string_literals(&source);
        let noun_forms = |position: usize| -> std::collections::BTreeSet<&str> {
            module_literals
                .iter()
                .filter(|lit| PLURAL_HELPERS.contains(&lit.call.as_str()) && lit.arg == position)
                .map(|lit| lit.text.as_str())
                .collect()
        };
        let singulars = noun_forms(SINGULAR_ARG);
        let plurals = noun_forms(PLURAL_ARG);
        assert!(
            !singulars.is_empty() && !plurals.is_empty(),
            "no pluralization call sites were found, so this guard would pass \
             vacuously — the lexer, the helper names or the argument positions are wrong"
        );
        let counted_nouns: std::collections::BTreeSet<&str> =
            singulars.union(&plurals).copied().collect();

        let literals = string_literals(&source[begin..end]);
        assert!(
            !literals.is_empty(),
            "the canvas render section parsed to zero string literals, so this guard \
             would pass vacuously"
        );
        // The lexer's own witness: this template is written across two
        // source lines with a `\`-continuation, and rustc joins it with
        // no indentation. If the lexer ever stops agreeing, the
        // placeholder-then-noun check below is reading strings rustc
        // never builds, and the whole guard is measuring the wrong text.
        assert!(
            literals
                .iter()
                .any(|lit| lit.text.contains("in the file but not shown.")),
            "the lexer did not join a `\\`-continued template the way rustc does"
        );
        // The escape witness, for the same reason: a literal the lexer
        // decodes differently from rustc is a literal this guard cannot
        // see. `card\x73` and `card\u{73}` are both `cards` to rustc,
        // and were `cardx73` / `cardu{73}` here until codex 0b round 1
        // — an evasion any author could reach for by accident. The
        // probes are lexed directly rather than planted in the render
        // section, so proving the decoder does not require shipping an
        // escaped noun in shipped copy.
        //
        // Written with escaped quotes rather than as raw strings
        // BECAUSE the noun derivation above lexes this whole file, and
        // the lexer refuses raw strings by design.
        // The third probe is a continuation across a BLANK line, with
        // the noun split by both escape families — rustc joins it to
        // `{count} cards`, and a decoder that skips only indentation
        // leaves a newline in the middle where the plural should be.
        for probe in [
            "\"{count} card\\x73\"",
            "\"{count} card\\u{73}\"",
            "\"{count} car\\u{64}\\\n\n\\x73\"",
        ] {
            assert_eq!(
                string_literals(probe)
                    .first()
                    .map(|literal| literal.text.as_str()),
                Some("{count} cards"),
                "the lexer must decode {probe} the way rustc does, or an escaped \
                 hardcoded plural is invisible to every check below"
            );
        }
        let line_of = |literal: &SourceLiteral| source[..begin].lines().count() + literal.line - 1;

        let mut offenders: Vec<String> = Vec::new();
        for literal in &literals {
            let routed = PLURAL_HELPERS.contains(&literal.call.as_str())
                && (literal.arg == SINGULAR_ARG || literal.arg == PLURAL_ARG);
            // A literal that IS a counted noun, in either form, is
            // either that call's argument or a hardcoded form smuggled
            // past the template. Provenance is the literal's own call
            // and argument position, never its line.
            if counted_nouns.contains(literal.text.as_str()) && !routed {
                offenders.push(format!(
                    "a11y.rs:{}: the literal {:?} is a counted noun but is argument {} of \
                     `{}`, not a noun argument of plural()/plural_len()/counted()",
                    line_of(literal),
                    literal.text,
                    literal.arg,
                    literal.call
                ));
                continue;
            }
            if routed {
                continue;
            }
            // The defect shape, inside ONE logical format string
            // (`\`-continuations already joined by the lexer): a
            // placeholder, then whitespace, then a hardcoded PLURAL.
            // Only the plural form — a placeholder before a SINGULAR is
            // the shape `⟨Kind⟩ card "title"` legitimately uses, and
            // nothing lexical separates the two (0a-14's residuals).
            for noun in &plurals {
                let mut from = 0usize;
                while let Some(at) = literal.text[from..].find(noun) {
                    let at = from + at;
                    from = at + noun.len();
                    let before = &literal.text[..at];
                    let after = &literal.text[from..];
                    // Whole word only — never `placards`, `Cards`.
                    if before.ends_with(|c: char| c.is_alphanumeric())
                        || after.starts_with(|c: char| c.is_alphanumeric())
                    {
                        continue;
                    }
                    if before.trim_end().ends_with('}') {
                        offenders.push(format!(
                            "a11y.rs:{}: {:?} interpolates a value immediately before the \
                             hardcoded plural {noun:?}",
                            line_of(literal),
                            literal.text
                        ));
                    }
                }
            }
        }
        assert!(
            offenders.is_empty(),
            "a canvas template speaks a noun whose count cannot agree with it — route it \
             through plural()/plural_len()/counted() (contracts 0a-14, 0b-16): {offenders:#?}"
        );
    }

    /// One string literal from Rust source, with the call it is an
    /// argument of — the provenance contract 0b-16 binds to the
    /// literal instead of to its line.
    struct SourceLiteral {
        /// 1-based line within the slice that was lexed.
        line: usize,
        /// The literal's CONTENT: escapes resolved, and Rust's
        /// `\`-before-newline continuation applied, so a template split
        /// across source lines reads as the one logical string it is.
        text: String,
        /// The identifier opening the innermost enclosing bracket
        /// (`format!`, `plural`, …); empty when nothing names it.
        call: String,
        /// 0-based argument position within that call, so the SINGULAR
        /// and PLURAL forms a helper is passed can be told apart.
        arg: usize,
    }

    /// Lex `source` into its string literals. Deliberately small: it
    /// knows strings, escapes, line comments, char literals and bracket
    /// nesting, which is exactly what binding a plural noun to its call
    /// needs — and nothing else, so it cannot quietly grow into a
    /// second Rust front end.
    ///
    /// A raw string literal (the `r`-prefixed form) is REFUSED rather
    /// than mis-lexed: it would read as an ordinary literal and its
    /// escapes would be resolved wrongly. The refusal happens inside
    /// the lexer, where prose in a comment cannot trip it.
    ///
    /// Two shapes it does NOT handle, named rather than implied away:
    ///
    /// - **block comments** (`/* … */`) are not skipped, so a quote
    ///   inside one would open a phantom literal and desynchronise
    ///   everything after it. This module has none;
    /// - **closure pipes** (`|x|`) are not bracket-like, so a comma
    ///   inside a closure's parameter list increments the enclosing
    ///   call's argument counter. That only mis-numbers arguments, and
    ///   only for a call that takes a multi-parameter closure — no
    ///   pluralization helper does.
    ///
    /// Neither can make the guard pass VACUOUSLY: both would corrupt
    /// the derived noun set or the literal list, and the caller asserts
    /// both are non-empty before using them. The failure mode is a loud
    /// wrong answer, not a silent green — which is the property that
    /// makes a hand-rolled lexer acceptable here at all.
    fn string_literals(source: &str) -> Vec<SourceLiteral> {
        let bytes = source.as_bytes();
        let mut out: Vec<SourceLiteral> = Vec::new();
        // One frame per open bracket: the identifier that opened it and
        // the argument position within it. Braces and square brackets
        // get a frame too, so a comma inside a closure or an array
        // cannot be mistaken for the next argument of a call.
        let mut stack: Vec<(String, usize)> = Vec::new();
        let mut ident = String::new();
        let mut line = 1usize;
        let mut index = 0usize;
        while index < bytes.len() {
            match bytes[index] {
                b'\n' => {
                    line += 1;
                    ident.clear();
                    index += 1;
                }
                b'/' if bytes.get(index + 1) == Some(&b'/') => {
                    while index < bytes.len() && bytes[index] != b'\n' {
                        index += 1;
                    }
                    ident.clear();
                }
                b'(' | b'[' | b'{' => {
                    stack.push((std::mem::take(&mut ident), 0));
                    index += 1;
                }
                b')' | b']' | b'}' => {
                    stack.pop();
                    ident.clear();
                    index += 1;
                }
                b',' => {
                    if let Some(frame) = stack.last_mut() {
                        frame.1 += 1;
                    }
                    ident.clear();
                    index += 1;
                }
                b'\'' => {
                    // A char literal (`'x'`, `'\n'`) or a lifetime.
                    // Only the former can hide a quote from the scanner.
                    if bytes.get(index + 1) == Some(&b'\\') {
                        index += 2;
                        while index < bytes.len() && bytes[index] != b'\'' {
                            index += 1;
                        }
                        index += 1;
                    } else if bytes.get(index + 2) == Some(&b'\'') {
                        index += 3;
                    } else {
                        index += 1;
                    }
                    ident.clear();
                }
                b'"' => {
                    assert_ne!(
                        ident, "r",
                        "the literal lexer does not handle raw strings; teach it before \
                         using one in this module"
                    );
                    let start = line;
                    let mut text: Vec<u8> = Vec::new();
                    index += 1;
                    while index < bytes.len() && bytes[index] != b'"' {
                        if bytes[index] == b'\\' {
                            index += 1;
                            match bytes.get(index) {
                                Some(b'n') => text.push(b'\n'),
                                Some(b't') => text.push(b'\t'),
                                Some(b'r') => text.push(b'\r'),
                                Some(b'0') => text.push(0),
                                // String-continuation escape: `\` before
                                // a newline swallows that newline AND
                                // every whitespace character after it —
                                // which is how a template split across
                                // source lines reads as one logical
                                // string.
                                //
                                // The set is rustc's, not "spaces and
                                // tabs": BLANK LINES and carriage
                                // returns are whitespace too, so a
                                // continuation followed by an empty line
                                // joins straight through it. Skipping
                                // only indentation left a stray newline
                                // in the decoded text, which is enough
                                // to hide a hardcoded plural from the
                                // scan below (codex 0b round 2).
                                Some(b'\n' | b'\r') => {
                                    while matches!(
                                        bytes.get(index),
                                        Some(b' ' | b'\t' | b'\n' | b'\r')
                                    ) {
                                        if bytes[index] == b'\n' {
                                            line += 1;
                                        }
                                        index += 1;
                                    }
                                    continue;
                                }
                                // `\xNN` and `\u{…}` are DECODED, not
                                // copied through. Pushing the escape's
                                // letters made `"{count} card\x73"` lex
                                // as `card` + `x73`, so a hardcoded
                                // plural spelled with escapes was
                                // invisible to every downstream check —
                                // a guard-evasion hole, and the one
                                // shape a hand-rolled lexer is most
                                // likely to get wrong.
                                Some(b'x') => {
                                    let hex = std::str::from_utf8(
                                        &bytes[index + 1..(index + 3).min(bytes.len())],
                                    )
                                    .expect("source is UTF-8");
                                    text.push(
                                        u8::from_str_radix(hex, 16)
                                            .expect("`\\x` takes two hex digits"),
                                    );
                                    index += 3;
                                    continue;
                                }
                                Some(b'u') => {
                                    assert_eq!(
                                        bytes.get(index + 1),
                                        Some(&b'{'),
                                        "`\\u` takes a braced scalar"
                                    );
                                    let close = index
                                        + bytes[index..]
                                            .iter()
                                            .position(|byte| *byte == b'}')
                                            .expect("unterminated `\\u{`");
                                    let hex = std::str::from_utf8(&bytes[index + 2..close])
                                        .expect("source is UTF-8");
                                    let scalar = u32::from_str_radix(hex, 16)
                                        .expect("`\\u{}` takes hex digits");
                                    let character = char::from_u32(scalar)
                                        .expect("`\\u{}` takes a scalar value");
                                    let mut buffer = [0u8; 4];
                                    text.extend_from_slice(
                                        character.encode_utf8(&mut buffer).as_bytes(),
                                    );
                                    index = close + 1;
                                    continue;
                                }
                                Some(other) => text.push(*other),
                                None => break,
                            }
                            index += 1;
                            continue;
                        }
                        if bytes[index] == b'\n' {
                            line += 1;
                        }
                        text.push(bytes[index]);
                        index += 1;
                    }
                    index += 1;
                    let (call, arg) = stack.last().cloned().unwrap_or_default();
                    out.push(SourceLiteral {
                        line: start,
                        text: String::from_utf8(text).expect("source is UTF-8"),
                        call,
                        arg,
                    });
                    ident.clear();
                }
                c if c.is_ascii_alphanumeric() || c == b'_' => {
                    ident.push(c as char);
                    index += 1;
                }
                b'!' => {
                    // `format!` / `matches!`: the bang belongs to the
                    // name the following `(` opens.
                    if !ident.is_empty() {
                        ident.push('!');
                    }
                    index += 1;
                }
                // `r#"…"#`: the hash keeps the `r` alive so the quote
                // arm above still sees the raw-string prefix.
                b'#' if ident == "r" => index += 1,
                _ => {
                    ident.clear();
                    index += 1;
                }
            }
        }
        out
    }

    /// The family is reached through exactly one top-level variant, and
    /// no canvas variant leaked back out to the top level (the whole
    /// point of the nesting — uniffi's 256-variant ceiling).
    #[test]
    fn the_canvas_family_occupies_one_top_level_variant() {
        let top_level: std::collections::BTreeSet<String> = declared_variants("A11yEvent")
            .into_iter()
            .filter(|name| name.starts_with("Canvas"))
            .collect();
        assert_eq!(
            top_level,
            ["Canvas".to_owned()]
                .into_iter()
                .collect::<std::collections::BTreeSet<String>>(),
            "the canvas family must nest under A11yEvent::Canvas"
        );
        assert!(
            corpus()
                .iter()
                .filter(|event| variant_of(event) == "Canvas")
                .count()
                > 100,
            "the canvas corpus went missing"
        );
    }

    /// t0 §1.2 and §1.3: the verbosity matrix, level by level, on the
    /// two families whose TEMPLATE varies — moved-to (the navigation
    /// matrix) and the destructive family (the undo hint at
    /// standard+). Full rendered strings, never substrings.
    #[test]
    fn canvas_verbosity_matrix_pins_every_level() {
        let moved_to = |verbosity| CanvasA11yEvent::CanvasMovedTo {
            verbosity,
            kind_label: "text".into(),
            title: "Research".into(),
            ordinal_n: 2,
            total_m: 5,
            container: Some("Q3".into()),
            connection_count: 3,
            color_name: Some("red".into()),
            marked: true,
        };
        let deleted = |verbosity, target| CanvasA11yEvent::CanvasDeleted {
            target,
            verbosity,
            undo_chord: "⌘Z".into(),
        };
        let card = || CanvasDeleteTarget::Card {
            kind_label: "text".into(),
            title: "Research".into(),
        };
        let group = || CanvasDeleteTarget::Group { label: "Q3".into() };
        let cards = || CanvasDeleteTarget::Cards { count: 3 };
        let connection = || CanvasDeleteTarget::Connection {
            direction: EdgeDirection::Outgoing,
            other_title: "Ideas".into(),
            label: Some("supports".into()),
        };

        let expected: Vec<(CanvasA11yEvent, &str)> = vec![
            (moved_to(CanvasVerbosity::Terse), "Research"),
            (
                moved_to(CanvasVerbosity::Standard),
                "Text card \"Research\", 2 of 5 in Q3",
            ),
            (
                moved_to(CanvasVerbosity::Verbose),
                "Text card \"Research\", 2 of 5 in Q3, 3 connections, red, marked",
            ),
            (
                deleted(CanvasVerbosity::Terse, card()),
                "Deleted Text card \"Research\"",
            ),
            (
                deleted(CanvasVerbosity::Standard, card()),
                "Deleted Text card \"Research\" — ⌘Z to undo",
            ),
            (
                deleted(CanvasVerbosity::Verbose, card()),
                "Deleted Text card \"Research\" — ⌘Z to undo",
            ),
            (
                deleted(CanvasVerbosity::Terse, group()),
                "Ungrouped Group \"Q3\" — cards kept",
            ),
            (
                deleted(CanvasVerbosity::Standard, group()),
                "Ungrouped Group \"Q3\" — cards kept — ⌘Z to undo",
            ),
            (
                deleted(CanvasVerbosity::Verbose, group()),
                "Ungrouped Group \"Q3\" — cards kept — ⌘Z to undo",
            ),
            (deleted(CanvasVerbosity::Terse, cards()), "Deleted 3 cards"),
            (
                deleted(CanvasVerbosity::Standard, cards()),
                "Deleted 3 cards — ⌘Z to undo",
            ),
            (
                deleted(CanvasVerbosity::Verbose, cards()),
                "Deleted 3 cards — ⌘Z to undo",
            ),
            (
                deleted(CanvasVerbosity::Terse, connection()),
                "Deleted connection to \"Ideas\", labelled \"supports\"",
            ),
            (
                deleted(CanvasVerbosity::Standard, connection()),
                "Deleted connection to \"Ideas\", labelled \"supports\" — ⌘Z to undo",
            ),
            (
                deleted(CanvasVerbosity::Verbose, connection()),
                "Deleted connection to \"Ideas\", labelled \"supports\" — ⌘Z to undo",
            ),
        ];
        for (event, text) in &expected {
            assert_eq!(event.render(), *text, "verbosity render for {event:?}");
            // The wrapper is a pure relay: the same string comes back
            // through the top-level event a host actually posts.
            assert_eq!(
                A11yEvent::Canvas {
                    event: event.clone()
                }
                .render(),
                *text
            );
        }

        // Everything else is verbosity-INVARIANT, and structurally so:
        // no other variant carries the parameter at all, which is why
        // a host cannot accidentally make one vary.
        let carriers: std::collections::BTreeSet<String> = corpus()
            .iter()
            .filter(|event| format!("{event:?}").contains("verbosity: "))
            .filter_map(canvas_variant_of)
            .collect();
        assert_eq!(
            carriers,
            ["CanvasDeleted".to_owned(), "CanvasMovedTo".to_owned()]
                .into_iter()
                .collect::<std::collections::BTreeSet<String>>(),
            "only the moved-to and destructive families take a verbosity"
        );
    }

    /// t0 §1.4: the ⌃⌘I readback is ALWAYS verbose-grade — it takes no
    /// verbosity parameter, so a terse user still gets the full fix.
    #[test]
    fn canvas_where_am_i_is_always_verbose_grade() {
        assert_eq!(
            CanvasA11yEvent::CanvasWhereAmI {
                kind_label: "text".into(),
                title: "Research".into(),
                group_path: vec!["Quarter".into(), "Q3".into()],
                ordinal_n: 2,
                total_m: 5,
                connection_count: 3,
                in_count: 1,
                out_count: 2,
                color_name: Some("red".into()),
                marked: true,
                mode: Some(CanvasMode::Move),
                filter: CanvasFilterState::Active {
                    matched: 3,
                    total: 40,
                },
            }
            .render(),
            "Text card \"Research\", in Quarter › Q3, 2 of 5, 3 connections (1 in, 2 out), \
             red, marked, Move mode, 3 of 40 shown"
        );
        assert!(
            !corpus()
                .iter()
                .any(
                    |event| canvas_variant_of(event).as_deref() == Some("CanvasWhereAmI")
                        && format!("{event:?}").contains("verbosity")
                ),
            "Where-am-I must not take a verbosity"
        );
    }

    /// t0 §1.5: "navigation = polite; errors/conflicts = assertive."
    /// `CanvasA11yEvent::priority` ends in a catch-all `_ => Medium`,
    /// so a canvas error that is not listed is silently polite. This
    /// pins the High membership BY NAME, both ways: the five families
    /// that carry mac's `.error` case and nothing else. The assertions
    /// run through the TOP-LEVEL event, so the wrapper's delegation is
    /// covered too.
    #[test]
    fn canvas_priorities_pin_the_error_tier() {
        const HIGH: &[&str] = &[
            "CanvasActionFailed",
            "CanvasBlocked",
            "CanvasFileNotFound",
            "CanvasModeRejected",
            "CanvasSaveConflict",
        ];
        let mut high_seen = std::collections::BTreeSet::new();
        for event in corpus() {
            let Some(variant) = canvas_variant_of(&event) else {
                continue;
            };
            let listed = HIGH.contains(&variant.as_str());
            match event.priority() {
                A11yPriority::High => {
                    assert!(
                        listed,
                        "{variant} renders High but is not in the error tier"
                    );
                    high_seen.insert(variant);
                }
                A11yPriority::Medium => assert!(
                    !listed,
                    "{variant} is in the error tier but renders Medium — the \
                     explicit priority() arm is missing it"
                ),
            }
        }
        let expected: std::collections::BTreeSet<String> =
            HIGH.iter().map(|name| (*name).to_owned()).collect();
        assert_eq!(
            high_seen, expected,
            "every error-tier canvas variant must be exercised by the corpus"
        );
    }

    #[test]
    fn multiline_templates_carry_no_stray_whitespace() {
        // The templates written with line-continuation backslashes must
        // render as single-space prose.
        for event in corpus() {
            let text = event.render();
            assert!(!text.contains('\n'), "newline leaked into {event:?}");
            assert!(!text.contains("  "), "double space leaked into {event:?}");
        }
    }
}
