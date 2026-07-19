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
//! composed by dedicated engines (the canvas/graph announcers' verbosity
//! machinery, Bases result summaries, filename advisories) or by
//! availability logic whose copy serves double duty in dialogs/hints;
//! those post [`A11yEvent::HostComposed`] carrying their text verbatim,
//! each call site marked `// W0.5-3 residue:` with the owning engine.
//! The parity census counts the residue so it shrinks deliberately
//! (engine-level vocabularies are follow-on batches), and no NEW
//! announcement may bypass the vocabulary.
//!
//! ## Copy rules
//!
//! Templates are the shipped mac strings, moved verbatim — this issue
//! deliberately does not redesign wording or verbosity policy. Plain
//! en-US in V1 (#264 owns localisation). Chord placeholders, when a
//! template ever needs one, render per-platform (program decision 12);
//! no current template carries a chord.

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
    CommandPaletteNeedsVault,
    SearchNeedsVault,

    // --- Links, search, embeds, headings, navigation ---
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
    RecentSearchFocused {
        query: String,
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
    HostComposed {
        text: String,
        priority: A11yPriority,
    },
}

impl A11yEvent {
    /// The urgency this event is spoken at — pinned per variant (the
    /// shipped mac priorities, moved verbatim).
    pub fn priority(&self) -> A11yPriority {
        use A11yEvent::*;
        match self {
            CommandPaletteNeedsVault
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
            | RenameFailed { .. } => A11yPriority::High,
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
            CommandPaletteNeedsVault => "Open a vault to use the command palette.".to_owned(),
            SearchNeedsVault => "Open a vault first. Search works inside a vault.".to_owned(),

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
            ItemsSelected { count } => format!("{count} items selected"),
            NoItemsSelected => "No items selected".to_owned(),
            TreeFolderSelected { name } => format!("Selected: {name}, folder"),
            RowSelected { name } => format!("Selected: {name}"),
            RecentSearchFocused { query } => format!("Recent search: {query}"),

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

            HostComposed { text, .. } => text.clone(),
        }
    }
}

/// en-US count noun (this vocabulary is V1 English; #264 owns l10n).
fn plural<'a>(count: u32, one: &'a str, many: &'a str) -> &'a str {
    if count == 1 { one } else { many }
}

/// One representative event per variant (parameterized variants use
/// fixed sample values). This is the seed of the §W-D canonical corpus:
/// the goldens below pin every entry's (priority, text), and the
/// committed corpus artifact is generated from the same list, so the
/// Rust goldens, the fixture, and the Swift census can never drift
/// apart.
pub fn corpus() -> Vec<A11yEvent> {
    use A11yEvent::*;
    vec![
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
        CommandPaletteNeedsVault,
        SearchNeedsVault,
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
        NoItemsSelected,
        TreeFolderSelected {
            name: "Archive".into(),
        },
        RowSelected {
            name: "notes".into(),
        },
        RecentSearchFocused {
            query: "fox".into(),
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
        DataviewConversionFailed {
            detail: "unsupported query".into(),
        },
        CitationInsertUnavailable,
        CitationWalkThrough,
        CodeCopied,
        HostComposed {
            text: "Composed by a host engine.".into(),
            priority: A11yPriority::High,
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
            (High, "Open a vault to use the command palette."),
            (Medium, "Open a vault first. Search works inside a vault."),
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
            (Medium, "No items selected"),
            (Medium, "Selected: Archive, folder"),
            (Medium, "Selected: notes"),
            (Medium, "Recent search: fox"),
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
            (Medium, "Dataview conversion failed: unsupported query"),
            (Medium, "Insert citation lands in V1.x. See Milestone L."),
            (
                Medium,
                "Walk through citations. Switch to the Citations sidebar tab and arrow through the list.",
            ),
            (Medium, "Code copied."),
            (High, "Composed by a host engine."),
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
