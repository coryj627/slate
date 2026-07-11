// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Vault session: opens a vault, manages its SQLite metadata index,
//! and exposes file enumeration to callers.
//!
//! This is the first slice of the `VaultSession` API from
//! `docs/plans/05_locked_architecture_decisions.md` §4.2. Milestone A
//! ships `open`, `close`, `scan_initial`, and `list_files`. Subsequent
//! milestones expand the surface with metadata queries, search, edit
//! paths, and the op log.
//!
//! Concurrency model: the session owns a single SQLite connection
//! guarded by a `Mutex`. The vault is single-writer (one user, one
//! process), so a mutex is sufficient and avoids a connection-pool
//! dependency. Reads acquire the lock briefly; long-running operations
//! like `scan_initial` hold the lock for their duration and are
//! cancellable via the supplied `CancelToken`.
//!
//! Storage layout (per `docs/plans/05` §7.6): the metadata cache lives
//! at `<cache_dir>/cache.sqlite`. When called via `from_filesystem`,
//! `cache_dir` defaults to `<vault_root>/.slate`. Callers can override
//! for tests or sandbox layouts.

use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::{Arc, Mutex};
use std::time::{SystemTime, UNIX_EPOCH};

use rusqlite::{Connection, OptionalExtension};

use crate::VaultError;
use crate::db;
use crate::vault::{EntryKind, FsVaultProvider, VaultProvider, content_hash};

// --- Configuration ---

/// Configuration for a `VaultSession`.
///
/// All fields are populated even when their feature isn't yet used; this
/// keeps the type stable across milestones. Callers can build via
/// `SessionConfig::new(cache_dir)` and tweak as needed.
#[derive(Debug, Clone)]
pub struct SessionConfig {
    /// Directory where the SQLite cache and other per-vault Slate state lives.
    /// Created on `VaultSession::open` if it does not already exist.
    pub cache_dir: PathBuf,
    /// SQLite page cache cap. Desktop default 4096 (~32 MB); mobile 512.
    pub max_db_cache_pages: u32,
    /// Indexing parallelism. 0 means "auto" (use `num_cpus()` on desktop, 2 on mobile).
    /// Not yet honored by the (sequential) Milestone A scanner.
    pub parse_workers: u32,
    /// Used as a cache-key for parser output. Bump when the parser changes
    /// in a way that invalidates previously-parsed metadata.
    pub parser_version: u32,
    /// LRU size for tree-sitter parse trees (V1.x feature; reserved).
    pub tree_sitter_cache_size: u32,
    /// Op-log compaction trigger by entry count (V1.F feature; reserved).
    pub oplog_compaction_threshold_entries: u32,
    /// Op-log compaction trigger by bytes (V1.F feature; reserved).
    pub oplog_compaction_threshold_bytes: u32,
    /// Op-log retention window (V1.F feature; reserved).
    pub oplog_retention_days: u32,
    /// File-size warn / confirm / refuse thresholds (V1.F feature; reserved).
    pub large_file_warn_bytes: u64,
    pub large_file_confirm_bytes: u64,
    pub large_file_refuse_bytes: u64,
    /// Refuse threshold for `read_attachment`. Image previews larger
    /// than this surface `VaultError::FileTooLarge` rather than
    /// allocating the buffer. Default 50 MiB (same as
    /// `large_file_refuse_bytes`); split into its own field so
    /// future tuning can raise the attachment ceiling independently
    /// of the editor's text ceiling.
    pub large_attachment_refuse_bytes: u64,
    /// Tag written into each op-log entry's `user_actor_id` field. For
    /// the single-user V1.F build this defaults to `"local"`; multi-
    /// device sync (V2) will plumb a real per-device identifier here.
    pub user_actor_id: String,
    /// Vault-relative directory the templates picker enumerates.
    ///
    /// `None` means the vault has no templates and `list_templates`
    /// returns `Ok(Vec::new())` without touching the filesystem. The
    /// Obsidian convention is `Templates/`; [`VaultSession::from_filesystem`]
    /// auto-detects it on session open. Callers can override with any
    /// vault-relative path (e.g. `"Notes/Templates"`) to honor a
    /// non-conventional vault layout.
    pub templates_dir: Option<String>,

    /// Per-user math rendering preferences. Consumed by
    /// `get_math_blocks` to drive MathCAT's speech style, verbosity,
    /// and braille code. Cache invalidates when this changes
    /// (see `MathPrefs::fingerprint`).
    pub math_prefs: crate::math::MathPrefs,

    /// Per-vault citations configuration loaded from
    /// `.slate/prefs.json`. Drives `set_bibliography_sources` +
    /// the renderer's default style. An empty `CitationsPrefs`
    /// means "no bibliography configured" (the common case until
    /// a tester opts in).
    pub citations_prefs: crate::citations::prefs::CitationsPrefs,
}

impl SessionConfig {
    /// Build a config with desktop-default values for the given cache directory.
    pub fn new(cache_dir: PathBuf) -> Self {
        Self {
            cache_dir,
            max_db_cache_pages: 4096,
            parse_workers: 0,
            parser_version: 1,
            tree_sitter_cache_size: 32,
            oplog_compaction_threshold_entries: 10_000,
            oplog_compaction_threshold_bytes: 5 * 1024 * 1024,
            oplog_retention_days: 90,
            large_file_warn_bytes: 5 * 1024 * 1024,
            large_file_confirm_bytes: 10 * 1024 * 1024,
            large_file_refuse_bytes: 50 * 1024 * 1024,
            large_attachment_refuse_bytes: 50 * 1024 * 1024,
            user_actor_id: "local".to_string(),
            templates_dir: None,
            math_prefs: crate::math::MathPrefs::default(),
            citations_prefs: crate::citations::prefs::CitationsPrefs::default(),
        }
    }

    /// Stricter defaults for mobile platforms (smaller caches, fewer workers).
    pub fn new_mobile(cache_dir: PathBuf) -> Self {
        let mut c = Self::new(cache_dir);
        c.max_db_cache_pages = 512;
        c.parse_workers = 2;
        c.tree_sitter_cache_size = 8;
        c
    }
}

// --- Filter ---

/// Filter applied to `list_files` (and, later, to other vault queries).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FileFilter {
    /// Every indexed file.
    All,
    /// Only files whose extension marks them as Markdown.
    MarkdownOnly,
    /// Markdown notes plus `.canvas` files — the openable-document set
    /// quick open and the file tree present once Milestone T lands
    /// (#361 backend change, consumed by #369).
    MarkdownAndCanvas,
    /// Markdown notes plus `.canvas` and `.base` files — the quick-open
    /// openable set once Milestone N adds Bases tabs (#702). Kept additive so
    /// existing canvas-only callers do not silently widen.
    OpenableDocuments,
}

// --- Summary type ---

/// Light-weight per-file row returned by `list_files`. The full per-file
/// metadata (`FileMetadata`) is hydrated on demand via `get_file_metadata`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FileSummary {
    pub path: String,
    pub name: String,
    pub mtime_ms: i64,
    pub size_bytes: u64,
    pub is_markdown: bool,
}

// --- Directory tree types (#459, U2-1) ---

/// One child directory row returned by [`VaultSession::list_dir_children`].
///
/// `child_dir_count` / `child_file_count` are the immediate (non-
/// recursive) child counts, so the UI can announce "N items" for a
/// collapsed folder without a second fetch. Counts exclude dot-prefixed
/// entries (which are never indexed) and count only the level directly
/// under this directory.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DirNodeSummary {
    /// Stable across rescans (upserted by `path`); the tree uses it as a
    /// node id.
    pub id: i64,
    /// Vault-relative, forward slashes, no trailing `/`.
    pub path: String,
    /// Final path component.
    pub name: String,
    pub child_dir_count: u32,
    pub child_file_count: u32,
}

/// One level of the file tree: the child directories of a parent, then
/// its child files. Returned by [`VaultSession::list_dir_children`].
///
/// `dirs` is the full (unpaged) list of child directories, sorted
/// case-insensitively; `files` is a [`Page`] of the child files in the
/// same order. The UI renders dirs first, then files (the sidebar's
/// existing convention). No `PartialEq` derive is needed by callers, but
/// both halves derive it so tests can assert on the whole listing.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DirListing {
    pub dirs: Vec<DirNodeSummary>,
    pub files: Page<FileSummary>,
}

// --- Metadata type ---

/// Full per-file metadata: the summary fields plus parsed structure
/// (headings + frontmatter properties; links join the type as a
/// later milestone if needed).
///
/// Manual `PartialEq` (rather than derived) because
/// `crate::PropertyValue::Float` contains an `f64`, and `f64`
/// doesn't implement `Eq`. PartialEq is enough for the test paths
/// that consume FileMetadata.
#[derive(Debug, Clone, PartialEq)]
pub struct FileMetadata {
    pub path: String,
    pub name: String,
    pub mtime_ms: i64,
    pub size_bytes: u64,
    pub is_markdown: bool,
    pub content_hash: String,
    pub headings: Vec<crate::Heading>,
    pub properties: Vec<crate::Property>,
}

/// Bundle returned by [`VaultSession::note_load_bundle`]: every
/// datum the UI needs to render a note's side panels in one trip
/// through the connection mutex (#92 item 4).
///
/// No `PartialEq` derive — `Vec<Property>` contains
/// `PropertyValue::Float(f64)` which isn't `Eq`-able; the existing
/// individual-component types already carry the appropriate
/// PartialEq impls for test paths that need them.
#[derive(Debug, Clone)]
pub struct NoteLoadBundle {
    pub backlinks: Page<crate::Backlink>,
    pub outgoing_links: Vec<crate::OutgoingLink>,
    pub properties: Vec<crate::Property>,
}

// --- Note parts bundle ---

/// Everything the U3 tab-open path needs to render a note as body-only
/// text plus a frontmatter widget, in one read + one hash (#469, U3-5).
///
/// `fm_source` and `body` are [`crate::split_note`]'s output — the
/// frontmatter source (bytes between the `---` delimiters, `""` when
/// absent) and the Markdown body. `content_hash` is over the **whole
/// file** (the same value `save_text`/`save_composed` conflict-check
/// against — the editor holds the body but the hash chain is whole-file,
/// so a composed save can detect an external edit to either half).
/// `mtime_ms` is the file's last-modified time for the caller's
/// change-tracking, matching [`SaveReport::new_mtime_ms`].
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct NotePartsBundle {
    pub fm_source: String,
    pub body: String,
    pub content_hash: String,
    pub mtime_ms: i64,
    /// Where the body starts in the whole file, in UTF-8 bytes — exactly
    /// `whole.len() - body.len()` at the split point (U3-3, #467). The
    /// host rebases whole-file offsets (headings, cursor parks) into its
    /// body-only buffer with THIS, never by re-deriving the compose
    /// prefix (two composers diverge — the U3-5 law).
    pub body_byte_offset: u64,
    /// Newlines before the body start — the whole-file → body LINE delta
    /// (backlink jumps, task rows speak 1-based file lines).
    pub body_line_offset: u32,
}

// --- Save report ---

/// Result of a successful `save_text`. Carries the post-save state the
/// editor needs to track unsaved-changes and feed back into the next
/// conflict-detected save.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SaveReport {
    pub new_content_hash: String,
    pub new_size_bytes: u64,
    pub new_mtime_ms: i64,
}

// --- Rename report ---

/// Outcome of a `rename_property_across_vault` call (dry-run or apply).
///
/// `affected`, `skipped`, and `failed` partition the candidate files:
///   - `affected` carries the per-file diff (the same shape for dry-run
///     and apply; the `applied` flag distinguishes them).
///   - `skipped` carries files we deliberately didn't touch
///     (`NoSuchKey` for files that don't carry the old key any more,
///     `KeyCollision` for files that already have both old and new
///     keys — we don't silently overwrite the new one).
///   - `failed` carries per-file errors encountered during apply
///     (typically `WriteConflict` from an external mid-rename
///     modification). Apply continues on these.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RenameReport {
    pub affected: Vec<RenameAffected>,
    pub skipped: Vec<RenameSkipped>,
    pub failed: Vec<RenameFailed>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RenameAffected {
    pub path: String,
    /// Excerpt from the source-of-truth frontmatter showing the
    /// old key line plus one context line on each side.
    pub before_excerpt: String,
    /// Same excerpt computed from the post-edit source.
    pub after_excerpt: String,
    /// `false` for dry-run results, `true` when the per-file save
    /// succeeded.
    pub applied: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RenameSkipped {
    pub path: String,
    pub reason: RenameSkipReason,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum RenameSkipReason {
    NoSuchKey,
    KeyCollision,
    /// The rename would cross the `tags` key boundary with a list-
    /// shaped value, and the reader's `tags`-keyname special-casing
    /// would silently flip the type discriminator between `List` and
    /// `TagList`. Audit #180: refuse rather than mutate the disk
    /// byte form. UI can offer a manual-edit fallback.
    TagsKeyTypeDrift,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RenameFailed {
    pub path: String,
    pub kind: RenameFailureKind,
    pub message: String,
}

/// Coarse classification of a per-file rename failure. The full error
/// text is in `RenameFailed::message`; this enum lets the UI route to
/// specific recovery flows (e.g. surface the conflict dialog) without
/// pattern-matching on display strings.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum RenameFailureKind {
    WriteConflict,
    MalformedFrontmatter,
    Cancelled,
    Other,
}

// --- Paging ---

/// Caller-supplied paging request. Use `Paging::first(n)` for the first
/// page and `Paging::after(cursor, n)` for the next, feeding the cursor
/// returned in the previous `Page`.
#[derive(Debug, Clone)]
pub struct Paging {
    pub cursor: Option<String>,
    pub limit: u32,
}

impl Paging {
    pub fn first(limit: u32) -> Self {
        Self {
            cursor: None,
            limit,
        }
    }

    pub fn after(cursor: String, limit: u32) -> Self {
        Self {
            cursor: Some(cursor),
            limit,
        }
    }
}

/// A page of results plus the cursor that fetches the next page.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Page<T> {
    pub items: Vec<T>,
    /// `None` when this page is the last one matching the filter.
    pub next_cursor: Option<String>,
    /// Total rows matching the filter (across all pages). Useful for UI
    /// progress and "showing X of Y" labels.
    pub total_filtered: u64,
}

// --- Cancellation ---

/// Cooperative cancellation token. Long-running operations check this
/// periodically and return `VaultError::Cancelled` when set.
#[derive(Debug, Clone, Default)]
pub struct CancelToken {
    cancelled: Arc<AtomicBool>,
}

impl CancelToken {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn cancel(&self) {
        self.cancelled.store(true, Ordering::SeqCst);
    }

    pub fn is_cancelled(&self) -> bool {
        self.cancelled.load(Ordering::SeqCst)
    }
}

// --- Scan report ---

/// Summary of a scan operation.
#[derive(Debug, Default, Clone)]
pub struct ScanReport {
    pub files_seen: u64,
    /// Files whose content the scanner actually read + hashed this pass.
    pub files_indexed: u64,
    /// Files the scanner could short-circuit because their on-disk
    /// `(mtime_ms, size_bytes)` matched the cached row exactly. We
    /// refresh `indexed_at_ms` for these but don't re-read or re-hash.
    pub files_skipped: u64,
    pub bytes_processed: u64,
    /// Per-file errors that did not abort the scan. The scanner keeps
    /// going on individual-file failures so one unreadable file does not
    /// blank the index.
    pub errors: Vec<String>,
}

// --- Scan progress events ---

/// Listener for incremental scan progress. Implemented by the host UI
/// (or any caller that wants to react to scan events).
///
/// Callbacks are invoked from the thread running `scan_initial`,
/// which holds the session's SQLite connection lock for the duration
/// of the scan. Implementations must be cheap and non-blocking; UI
/// hosts should marshal back to their main thread asynchronously
/// rather than block inside `on_progress`.
pub trait ScanProgressListener: Send + Sync {
    fn on_progress(&self, event: ScanProgress);
}

/// Session-level error events (O-2 #540) — the minimal delivery of the
/// `05` §4.4 `VaultEventListener` sketch: one method, one code for
/// now, both growable additively. Invoked from the background
/// compaction worker's thread; implementations must be cheap and
/// non-blocking and marshal to their UI thread themselves (the
/// `ScanProgressListener` contract).
pub trait VaultEventListener: Send + Sync {
    fn on_error(&self, code: EventErrorCode, path: String, message: String);

    /// #802: a Slate-originated file mutation committed (write on disk
    /// AND index row in one transaction). Dispatch is strictly
    /// post-commit: at callback time the new state is visible to any
    /// external reader of the index (Codoki on #846). Default no-op —
    /// O-2 registrations stay valid unchanged. The filesystem watcher is a
    /// stub (`provider.watch` yields nothing), so external edits
    /// surface at the next scan, never here: this event covers the
    /// app's own write paths. Do not call session APIs synchronously
    /// from the callback — it may arrive with session locks held (the
    /// `ScanProgressListener` discipline); marshal first.
    fn on_file_change(&self, _event: FileChangeEvent) {}

    /// #802: coarse index lifecycle. `files_seen` is nonzero only for
    /// [`IndexPhase::ScanFinished`]. Default no-op; same
    /// no-synchronous-reentry rule as [`Self::on_file_change`].
    fn on_index_phase(&self, _phase: IndexPhase, _files_seen: u64) {}
}

/// One Slate-originated file mutation (#802).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FileChangeEvent {
    pub kind: FileChangeKind,
    /// Vault-relative. For [`FileChangeKind::Renamed`], the NEW path.
    pub path: String,
    /// [`FileChangeKind::Renamed`] only: the path moved away from.
    pub previous_path: Option<String>,
}

/// What happened to the file. Additive-only — hosts must tolerate
/// unknown kinds (the [`EventErrorCode`] convention).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FileChangeKind {
    Created,
    Modified,
    Deleted,
    Renamed,
}

/// Index lifecycle phases (#802). Additive-only.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum IndexPhase {
    ScanStarted,
    ReconcileStarted,
    ReconcileFinished,
    ScanFinished,
}

/// What went wrong. Additive-only — hosts must tolerate unknown codes.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum EventErrorCode {
    /// A background op-log compaction failed; the named file's history
    /// may grow unbounded until a later run succeeds (§9.3.3's
    /// "failure is a user-visible hard error").
    CompactionFailed,
}

/// One unit of background compaction work (O-2 #540).
enum CompactionJob {
    Compact {
        file_id: i64,
        log_name: String,
        path: String,
    },
    Shutdown,
}

/// Incremental events emitted during a scan.
///
/// Once `Started` fires, the listener is guaranteed exactly one
/// terminal event:
/// - `Finished { report }` on success.
/// - `Cancelled` if cancellation fires after `Started`.
/// - `Failed { message }` on any non-cancellation error after
///   `Started` (typically a SQLite commit failure).
///
/// Errors that happen *before* `Started` (e.g. a pre-cancel, or the
/// initial `conn.transaction()` failing) return through the caller's
/// `Result` without emitting any listener event, since the stream
/// hadn't actually started.
#[derive(Debug, Clone)]
pub enum ScanProgress {
    /// Scan is about to begin. `total_files` is the count of file-
    /// typed entries the scanner enumerated in a pre-pass. The
    /// filesystem can change between this pre-pass and the main
    /// scan, so the value is best-effort.
    Started { total_files: u64 },
    /// One file has been visited (either re-hashed via the slow path
    /// or short-circuited via the fast path). `indexed` is the
    /// running count; `total` is the same `total_files` from the
    /// initial `Started` event.
    FileIndexed {
        path: String,
        indexed: u64,
        total: u64,
    },
    /// Scan completed normally. `report` is the same value the
    /// caller will see returned from `scan_initial`.
    Finished { report: ScanReport },
    /// Scan was cancelled after `Started`. Caller's `scan_initial`
    /// returns `Err(VaultError::Cancelled)`.
    Cancelled,
    /// Scan aborted after `Started` for a non-cancellation reason —
    /// almost always a SQLite commit failure. `message` is a
    /// human-readable summary; the same underlying error is also
    /// returned through the caller's `Result`.
    Failed { message: String },
}

/// FFI-friendly subset of `CslStyle`: just the picker-display fields.
/// Distinct from `citations::render::CslStyle` because that type
/// holds a parsed `IndependentStyle` from hayagriva which doesn't
/// cross the uniffi boundary.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CslStyleInfo {
    pub id: String,
    pub path: String,
    pub title: String,
}

// --- The session ---

/// A live vault session.
///
/// Holds the vault provider, the SQLite cache connection, and the
/// configuration. Drop the session (or call `close`) when done to flush
/// the database.
/// In-memory per-file op-log append state for this session (#378). Lets
/// `save_text_locked` choose snapshot-vs-`EditBatch` in O(1) without
/// re-reading the log: `last_hash_after` is the whole-doc hash after the
/// last entry we wrote (the alignment check against disk — if it doesn't
/// match, an external edit happened and we re-snapshot), and
/// `bytes_since_snapshot` drives the snapshot cadence. A cache miss (first
/// save of a file this session) forces a fresh snapshot, so we never read
/// or trust a pre-existing on-disk log (migration / corrupt-tail / legacy
/// all collapse to "first save this session snapshots").
///
/// Keyed by `files.id`, which is stable across renames (the same row) and
/// is never reused while the file exists. Were a future code path to
/// `DELETE` a file row, SQLite could reuse its rowid, and a brand-new file
/// landing on it would inherit a stale `last_hash_after` — but that simply
/// fails the alignment check and forces a (harmless) re-snapshot, never
/// corruption. The map is per-session and not pruned on delete; a
/// long-lived session holds one small entry per file ever saved.
#[derive(Debug, Clone)]
struct OplogAppendState {
    last_hash_after: String,
    bytes_since_snapshot: u64,
    /// O-2 (#540): the last compaction attempt could not shrink this
    /// log (single oversized entry). Cleared by the next append; while
    /// set, non-append paths (the on-open sweep) skip re-enqueueing —
    /// no livelock on permanently-oversized logs.
    compaction_futile: bool,
    /// Appends this session (O-2, adversarial review): a sound lower
    /// bound on the log's entry count — once it exceeds
    /// `oplog_compaction_threshold_entries`, the log definitely does
    /// too, so the entry-count trigger fires without ever walking the
    /// log on the save path. Cross-session accumulation is covered by
    /// the on-open sweep's size-based estimate.
    appends_this_session: u64,
}

/// Estimated per-entry on-disk framing overhead (body header + two hex
/// hashes + actor id + the length/checksum fields) used only to make the
/// snapshot-cadence accounting (`bytes_since_snapshot`) track real log
/// growth rather than payload bytes alone. Deliberately generous.
const OPLOG_ENTRY_FRAMING_OVERHEAD_ESTIMATE: u64 = 256;

pub struct VaultSession {
    provider: Arc<dyn VaultProvider>,
    conn: Mutex<Connection>,
    config: SessionConfig,
    /// Per-file op-log append state (#378). See [`OplogAppendState`].
    /// `Arc` so the compaction worker can mark futility (O-2).
    oplog_state: Arc<Mutex<std::collections::HashMap<i64, OplogAppendState>>>,
    /// Runtime-mutable math preferences. Audit #259: changing
    /// `config.math_prefs` at runtime isn't possible because
    /// `config` is owned by the session. UI surfaces (Settings
    /// panel #224) drive this lock; `get_math_blocks` reads
    /// through it on every call so a preference change takes
    /// effect immediately.
    math_prefs: Mutex<crate::math::MathPrefs>,
    /// Citations index — lazy view over `bibliography_entries`.
    /// `set_bibliography_sources` bumps the version so the renderer's
    /// cache invalidates implicitly (see `RenderCache`).
    bib_index: Mutex<Arc<crate::citations::bibliography::BibIndex>>,
    /// Lazy-loaded CSL styles, keyed by `style_id`. Populated on
    /// first `render_citation` call against an unfamiliar id.
    csl_styles: Mutex<std::collections::HashMap<String, Arc<crate::citations::render::CslStyle>>>,
    /// Per-process render cache. Keyed on
    /// `(reference, style_id, bib_index_version)` so bibliography
    /// reload invalidates implicitly.
    render_cache: crate::citations::render::RenderCache,
    /// Open canvas documents, keyed by opaque handle (Milestone T,
    /// #361). Node ids are unique per file, not vault-wide, so every
    /// canvas API call routes through a handle. One entry per
    /// `open_canvas` call; the UI's one-`CanvasDocument`-per-path
    /// registry (t2) sits above this.
    canvases: Mutex<std::collections::HashMap<u64, OpenCanvasState>>,
    /// Monotonic handle source — never reused within a session, so a
    /// stale handle after `close_canvas` fails loudly instead of
    /// aliasing a newer document.
    next_canvas_handle: AtomicU64,
    /// Open Bases documents and ephemeral queries, keyed by opaque handle.
    /// The handle shape mirrors canvases: row identities are scoped to the
    /// opened base/query, and stale handles fail loudly.
    bases: Mutex<std::collections::HashMap<u64, OpenBaseState>>,
    /// Monotonic Bases handle source, never reused within a session.
    next_base_handle: AtomicU64,
    /// Coarse session-local generation for Bases query caches. Bumped after
    /// index-changing writes so cache lookups stay O(1) at large vault sizes.
    bases_generation: AtomicU64,
    /// Monotonic salt for new op-log name stems (O-1 #539): stems derive
    /// from `blake3(path ‖ now_ms ‖ salt)`, so even two allocations for
    /// the same path in the same millisecond differ.
    next_oplog_stem_salt: AtomicU64,
    /// Deleted-file remnant logs discovered by the last scan reconcile
    /// (O-1 #539). O-3's deleted-file recovery reads this; O-2's
    /// retention sweep ages entries out on disk.
    /// `(generation, remnants)` under ONE mutex: the generation bumps
    /// with every mutation (reconcile refresh, recovery), and paging
    /// snapshots both atomically — a cursor can never pair a stale
    /// snapshot with a newer generation (adversarial review, O-3).
    remnant_logs: Mutex<(u64, Vec<RemnantLog>)>,
    /// True when this open created the cache database (fresh vault or
    /// rebuild). Gates the reconcile's content salvage — see `open`.
    cache_created_this_open: bool,
    /// Registered session-event listeners (O-2 #540), keyed by an
    /// opaque registration token. Shared with the compaction worker.
    event_listeners: Arc<Mutex<std::collections::HashMap<u64, Arc<dyn VaultEventListener>>>>,
    next_listener_token: AtomicU64,
    /// Runtime-mutable retention window (days). Initialized from
    /// `SessionConfig::oplog_retention_days`; O-5's settings surface
    /// writes it live and the next compaction/sweep reads it.
    retention_days: Arc<std::sync::atomic::AtomicU32>,
    /// Background compaction queue (O-2 #540): send side + the
    /// single-flight dedup set shared with the worker.
    compaction_tx: std::sync::mpsc::Sender<CompactionJob>,
    compaction_queued: Arc<Mutex<std::collections::HashSet<String>>>,
    /// Logs whose compaction was requested while a run was already in
    /// flight (adversarial review): the worker queues exactly one
    /// follow-up per dirty log after the in-flight run completes.
    compaction_dirty: Arc<Mutex<std::collections::HashSet<String>>>,
    compaction_shutdown: Arc<std::sync::atomic::AtomicBool>,
    compaction_join: Mutex<Option<std::thread::JoinHandle<()>>>,
}

/// One annotation on a version row (O-3 #541): `kind` is the stable
/// tag name; `display` is the UI chip copy.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct OpAnnotationSummary {
    pub kind: String,
    pub display: String,
}

/// One row of a file's version history (O-3 #541).
///
/// Row identity for lists/selection is `position_from_tail` —
/// `content_hash_after` is NOT unique per row (an A→B→A history
/// repeats hashes); the hash is the CONTENT identity used for
/// `version_content` / `restore_version`, where any occurrence of the
/// same hash reconstructs the same bytes (enforced by verification).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct VersionSummary {
    /// 0 = newest.
    pub position_from_tail: u32,
    pub content_hash_after: String,
    pub timestamp_ms: i64,
    /// The inner kind for `Annotated` entries.
    pub op_kind: crate::oplog::OpKind,
    /// Decoded batch len; 0 for pure markers and canvas records; 1 for
    /// snapshots.
    pub op_count: u32,
    /// `len(after) − len(before)`; a snapshot with no prior = its len.
    pub byte_delta: i64,
    pub annotations: Vec<OpAnnotationSummary>,
    /// `hash_before == hash_after` (anchors, PathChanged markers,
    /// canvas records that changed nothing).
    pub is_marker: bool,
    /// e.g. "12 operations, 340 bytes added" — the Swift side prepends
    /// the date.
    pub audio_fragment: String,
}

/// The changes-since-last-open verdict (O-4 #542).
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ChangesSinceOpen {
    /// No mark recorded yet (first open, or a cache rebuild wiped
    /// `open_marks` — honest degradation, plan decision #6).
    NoBaseline,
    /// The mark's hash equals the current content hash.
    Unchanged,
    /// The structured diff from the mark's version to the log tail.
    Diff(crate::structured_diff::StructuredDiff),
    /// The mark's version is no longer reconstructible (compacted
    /// past, or the log was rebound) — earlier changes exist but
    /// can't be described.
    BaselineCompacted,
}

/// One recoverable (or at least known) deleted file (O-3 #541).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DeletedFileEntry {
    /// Effective path (last `PathChanged` / header creation path).
    pub path: String,
    /// Structural-journal `DeleteFile` timestamp when available;
    /// `None` after a cache rebuild wiped the journal.
    pub deleted_at_ms: Option<i64>,
    /// The tail reconstructs and passes integrity verification.
    pub recoverable: bool,
    /// Reconstructed tail length when recoverable.
    pub size_bytes: Option<u64>,
}

/// A deleted file's surviving op log, discovered by the scan reconcile
/// (O-1 #539): an on-disk log bound to no live `files` row, whose
/// effective vault path is known from its header path record or its
/// last `PathChanged` annotation. This is the reconcile's output
/// interface to O-3's `list_deleted_files` / `recover_deleted_file`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RemnantLog {
    /// Log name stem (`<stem>.oplog` under `<cache_dir>/oplog/`).
    pub stem: String,
    /// The file's vault-relative path as last recorded in the log.
    pub effective_path: String,
    /// `content_hash_after` of the log's final entry.
    pub tail_hash: String,
    /// Timestamp of the log's final entry.
    pub tail_timestamp_ms: i64,
}

/// Per-open-canvas state: the tolerant parse (the write surface
/// mutates it), the derived model (serves navigation queries), and the
/// content hash the next save conflict-checks against.
struct OpenCanvasState {
    path: String,
    file_id: i64,
    canvas: crate::canvas::Canvas,
    model: crate::canvas::model::CanvasModel,
    content_hash: String,
    degraded: bool,
}

enum OpenBaseSource {
    Base(crate::bases::BaseFile),
    Query(crate::bases::SlateQuery),
}

struct OpenBaseState {
    path: Option<String>,
    /// Whole-file hash observed at open/after the last successful edit.
    /// File-backed handles use it as the CAS expectation for the next save;
    /// inline/query handles have no persistence identity and keep `None`.
    content_hash: Option<String>,
    source: OpenBaseSource,
    /// Execution queries compiled once at open/edit time. Building a view
    /// query parses every expression in the YAML model, so repeating it on
    /// cache hits would make the session cache pay parser cost unnecessarily.
    queries: Vec<crate::bases::SlateQuery>,
    warnings: Vec<String>,
    default_this_path: Option<String>,
    cache: crate::bases::engine::BasesQueryCache,
    transient_sort: Option<(u32, crate::bases::SortKey)>,
}

/// Open the compaction worker's own SQLite connection (O close-out):
/// the session's connection isn't shared, and the worker must couple
/// every successful rewrite to the derived `oplog_events` index —
/// otherwise temporal queries keep matching history the fold just
/// discarded (milestone red team High: the retention picker,
/// recoverable log contents, and temporal queries must agree). WAL +
/// busy_timeout make a second connection safe; the schema already
/// exists (no migrations here).
fn open_worker_connection(cache_dir: &std::path::Path) -> Result<Connection, rusqlite::Error> {
    let conn = Connection::open(cache_dir.join("cache.sqlite"))?;
    conn.busy_timeout(std::time::Duration::from_secs(5))?;
    conn.pragma_update(None, "foreign_keys", "ON")?;
    Ok(conn)
}

/// Regenerate ONE file's `oplog_events` rows from its (just-rewritten)
/// log and clear the worker's own staleness marker, in one
/// transaction. Rows for other files are untouched, as are OTHER
/// markers (a pending save/adoption obligation must survive).
fn regen_events_after_compaction(
    conn: &mut Connection,
    cache_dir: &std::path::Path,
    file_id: i64,
    log_name: &str,
    marker_rowid: Option<i64>,
    events_cutoff_ms: i64,
) -> Result<(), VaultError> {
    // Hold the LOG's exclusive lock across read + transaction
    // (milestone re-review High): without it, a save can append entry
    // N+1 and commit its event row between our read and our
    // DELETE+reinsert — erasing that row forever with every marker
    // already cleared. Under the lock a concurrent save blocks at its
    // own append (milliseconds) and its event insert lands strictly
    // after this commit. Same lock-then-verify-inode protocol as the
    // appenders and the compactor; a vanished log aborts (the marker
    // stays, the scan heals).
    let log_path = crate::oplog::oplog_path_for_name(cache_dir, log_name);
    let _log_lock = crate::oplog::open_locked_verified(
        &log_path,
        std::fs::OpenOptions::new().read(true).write(true),
    )
    .map_err(VaultError::Io)?
    .ok_or_else(|| VaultError::Trash {
        message: "log vanished before event regeneration".into(),
    })?;
    let entries = crate::oplog::read_oplog(cache_dir, log_name).map_err(VaultError::Io)?;
    let events = crate::oplog_events::derive_events_for_log(&entries);
    let tx = conn.transaction()?;
    tx.execute(
        "DELETE FROM oplog_events WHERE file_id = ?1",
        rusqlite::params![file_id],
    )?;
    insert_oplog_events(&tx, file_id, &events, events_cutoff_ms)?;
    if let Some(rowid) = marker_rowid {
        tx.execute(
            "DELETE FROM oplog_events_stale WHERE rowid = ?1",
            rusqlite::params![rowid],
        )?;
    }
    tx.commit()?;
    Ok(())
}

/// The background compaction worker (O-2 #540). Owns nothing but its
/// channel: config snapshots, the listener registry, the append-state
/// map, and the dedup set all arrive as `Arc`s. Checks the shutdown
/// flag between files — a mid-file compaction completes (sub-second by
/// the §9.3.3 perf gate) rather than tearing.
#[allow(clippy::too_many_arguments)] // cohesive worker context
fn compaction_worker_loop(
    rx: std::sync::mpsc::Receiver<CompactionJob>,
    tx: std::sync::mpsc::Sender<CompactionJob>,
    cache_dir: PathBuf,
    threshold_bytes: u64,
    threshold_entries: u64,
    retention_days: Arc<std::sync::atomic::AtomicU32>,
    listeners: Arc<Mutex<std::collections::HashMap<u64, Arc<dyn VaultEventListener>>>>,
    oplog_state: Arc<Mutex<std::collections::HashMap<i64, OplogAppendState>>>,
    queued: Arc<Mutex<std::collections::HashSet<String>>>,
    dirty: Arc<Mutex<std::collections::HashSet<String>>>,
    shutdown: Arc<std::sync::atomic::AtomicBool>,
) {
    // Lazily opened on the first pass that needs it; owned by the
    // worker thread for its lifetime.
    let mut worker_conn: Option<Connection> = None;
    while let Ok(job) = rx.recv() {
        let CompactionJob::Compact {
            file_id,
            log_name,
            path,
        } = job
        else {
            break; // Shutdown
        };
        if shutdown.load(std::sync::atomic::Ordering::SeqCst) {
            break;
        }
        // Single-flight THROUGH completion (adversarial review): the
        // log stays claimed in `queued` for the whole drain below, so
        // triggers landing at ANY point — first run, follow-up runs,
        // or the release window — either find the claim and flip the
        // dirty bit, or arrive after release and queue normally. The
        // release itself holds BOTH locks (queued → dirty, the same
        // order enqueue_compaction uses), so a trigger can never slip
        // between "observe clean" and "drop the claim" and be erased.
        let mut passes = 0u32;
        loop {
            let limits = crate::oplog_compaction::CompactionLimits {
                threshold_bytes,
                threshold_entries,
                retention_days: retention_days.load(std::sync::atomic::Ordering::SeqCst),
            };
            // Durable staleness marker BEFORE the rewrite (milestone
            // red team, the mark-before-append discipline applied to
            // compaction): a rewrite discards folded history, and the
            // derived `oplog_events` rows for it must die too. Marker
            // first ⇒ a crash between the rename and the regeneration
            // below leaves a repair trigger for the next scan. A
            // marker-write failure degrades to the pre-marker crash
            // window; the fold itself still runs — filesystem truth
            // outranks index freshness.
            if worker_conn.is_none() {
                worker_conn = open_worker_connection(&cache_dir)
                    .map_err(|e| {
                        log::warn!("compaction worker db connection failed");
                        log::debug!("compaction worker db failure detail: {e}");
                    })
                    .ok();
            }
            let marker_rowid: Option<i64> = worker_conn.as_ref().and_then(|conn| {
                match conn.execute("INSERT INTO oplog_events_stale (marker) VALUES (1)", []) {
                    Ok(_) => Some(conn.last_insert_rowid()),
                    Err(e) => {
                        log::warn!("compaction staleness marker write failed");
                        log::debug!("compaction staleness marker failure detail: {e}");
                        None
                    }
                }
            });
            let clear_marker_only = |worker_conn: &mut Option<Connection>| {
                if let (Some(conn), Some(rowid)) = (worker_conn.as_ref(), marker_rowid) {
                    let _ = conn.execute(
                        "DELETE FROM oplog_events_stale WHERE rowid = ?1",
                        rusqlite::params![rowid],
                    );
                }
            };
            // Fail CLOSED without the marker (milestone re-review
            // High): a rewrite whose crash window has no durable
            // trigger would leave stale index rows undetectable
            // forever. Skip the pass — the claim releases normally
            // below and the next trigger (a save's threshold check or
            // the on-open sweep) retries with a working database.
            let outcome = if marker_rowid.is_none() {
                log::warn!(
                    "oplog compaction skipped for file_id={file_id}: staleness marker unavailable"
                );
                Ok(crate::oplog_compaction::CompactionOutcome::Missing)
            } else {
                crate::oplog_compaction::compact_log(
                    &cache_dir,
                    &log_name,
                    &path,
                    &limits,
                    now_ms(),
                )
            };
            match outcome {
                Ok(crate::oplog_compaction::CompactionOutcome::Rewritten { .. }) => {
                    // Tail hash unchanged by construction; the session
                    // append state still chains and its cadence
                    // counter self-corrects at the next snapshot.
                    //
                    // Couple the derived index to the rewrite: the
                    // folded-away entries' event rows must not keep
                    // matching temporal queries (milestone red team
                    // High). One transaction per file; failure leaves
                    // the marker standing and the next scan heals.
                    let regenerated = match worker_conn.as_mut() {
                        Some(conn) => regen_events_after_compaction(
                            conn,
                            &cache_dir,
                            file_id,
                            &log_name,
                            marker_rowid,
                            retention_cutoff_ms(limits.retention_days),
                        ),
                        None => Err(VaultError::Trash {
                            message: "no worker db connection".into(),
                        }),
                    };
                    if let Err(e) = regenerated {
                        log::warn!(
                            "oplog_events regeneration after compaction failed for file_id={file_id}"
                        );
                        log::debug!("post-compaction regeneration failure detail: {e:?}");
                        // Marker (if written) stays: the scan rebuild
                        // is the repair path.
                    }
                }
                Ok(crate::oplog_compaction::CompactionOutcome::Futile)
                | Ok(crate::oplog_compaction::CompactionOutcome::AlreadyCompact) => {
                    // No rewrite: the index rows are still accurate;
                    // this pass's marker is moot.
                    clear_marker_only(&mut worker_conn);
                    if let Some(state) = oplog_state
                        .lock()
                        .expect("oplog state mutex")
                        .get_mut(&file_id)
                    {
                        state.compaction_futile = true;
                    }
                }
                Ok(crate::oplog_compaction::CompactionOutcome::Missing) => {
                    clear_marker_only(&mut worker_conn);
                }
                Err(e) => {
                    // §9.3.3: failure is a user-visible hard error — the
                    // exact copy below is the O-2/O-5 contract. The
                    // listener message may carry the vault path (it IS
                    // user-facing UI copy); the log warn stays path-free
                    // per the privacy rule.
                    let message = format!(
                        "Slate couldn't compact the edit history for {path}: {e}. \
                         History for this file may grow unbounded."
                    );
                    let snapshot: Vec<Arc<dyn VaultEventListener>> = listeners
                        .lock()
                        .expect("event listener mutex")
                        .values()
                        .cloned()
                        .collect();
                    for listener in snapshot {
                        listener.on_error(
                            EventErrorCode::CompactionFailed,
                            path.clone(),
                            message.clone(),
                        );
                    }
                    log::warn!("oplog compaction failed for {log_name}: {}", e.kind());
                    log::debug!("oplog compaction failure for {path:?}: {e}");
                    // compact_log publishes atomically (tmp+rename): a
                    // reported failure means the log was NOT rewritten
                    // and the index rows are still accurate.
                    clear_marker_only(&mut worker_conn);
                }
            }

            // Atomic release-or-continue: with BOTH locks held, either
            // consume a dirty bit (another pass is owed) or drop the
            // claim (fully clean — new triggers queue normally).
            let rerun = {
                let mut queued = queued.lock().expect("compaction queue mutex");
                let mut dirty = dirty.lock().expect("compaction dirty mutex");
                if dirty.remove(&log_name) {
                    true // claim stays held for the next pass
                } else {
                    queued.remove(&log_name);
                    false
                }
            };
            if !rerun {
                break;
            }
            passes += 1;
            if passes >= 3 || shutdown.load(std::sync::atomic::Ordering::SeqCst) {
                // Fairness / shutdown: keep the claim and requeue
                // through the channel so other logs get a turn (a
                // hot saver must not pin the worker to one log). On
                // shutdown the queued job is simply abandoned — the
                // next open's sweep rediscovers the oversized log.
                let _ = tx.send(CompactionJob::Compact {
                    file_id,
                    log_name: log_name.clone(),
                    path: path.clone(),
                });
                break;
            }
        }
    }
}

impl Drop for VaultSession {
    fn drop(&mut self) {
        // Stop the compaction worker: flag first (checked between
        // files), then a wake-up message, then join — a mid-file
        // compaction completes, everything queued behind it is
        // abandoned (re-discovered by the next open's sweep).
        self.compaction_shutdown
            .store(true, std::sync::atomic::Ordering::SeqCst);
        let _ = self.compaction_tx.send(CompactionJob::Shutdown);
        if let Some(handle) = self
            .compaction_join
            .lock()
            .expect("compaction join mutex")
            .take()
        {
            let _ = handle.join();
        }
    }
}

impl VaultSession {
    fn bump_bases_generation(&self) {
        self.bases_generation
            .fetch_add(1, std::sync::atomic::Ordering::SeqCst);
    }

    fn bases_generation(&self) -> u64 {
        self.bases_generation
            .load(std::sync::atomic::Ordering::SeqCst)
    }

    /// Open or create a vault session against the given provider.
    ///
    /// Creates `config.cache_dir` if missing, opens/creates the SQLite
    /// database under it, and applies all pending schema migrations.
    pub fn open(
        provider: Arc<dyn VaultProvider>,
        config: SessionConfig,
    ) -> Result<Self, VaultError> {
        std::fs::create_dir_all(&config.cache_dir)?;
        let db_path = config.cache_dir.join("cache.sqlite");

        let mut conn = db::open_database(&db_path, config.max_db_cache_pages)?;
        // Whether this open CREATED the cache (schema version 0 before
        // migrating) — i.e. a brand-new vault or a deleted/rebuilt
        // cache.sqlite. The oplog reconcile's content salvage is gated
        // on this: on an intact cache an unbound live file is a file
        // that was never saved through Slate, and attaching any
        // existing log to it by content coincidence (two notes from
        // one template!) would be exactly the cross-attach O-1 exists
        // to prevent. Only a rebuild legitimately orphans bindings.
        let cache_created_this_open = db::current_version(&conn)? == 0;
        db::migrate(&mut conn)?;

        let math_prefs = Mutex::new(config.math_prefs);

        // Build the initial BibIndex from whatever's already in the
        // bibliography_entries table — empty after a fresh
        // installation, populated after a re-open if the previous
        // session ran `set_bibliography_sources`.
        let initial_entries = crate::citations_db::list_bibliography_entries(&conn)?;
        let bib_index = Arc::new(crate::citations::bibliography::BibIndex::build(
            initial_entries,
            1,
        ));

        // Background compaction worker (O-2 #540): one thread, jobs
        // deduped per log (single-flight), idle when the queue is
        // empty, joined on session close. It takes only per-log file
        // locks — never the connection mutex — so the save path can
        // block on a log lock while the worker runs without any
        // ordering cycle.
        let oplog_state: Arc<Mutex<std::collections::HashMap<i64, OplogAppendState>>> =
            Arc::new(Mutex::new(std::collections::HashMap::new()));
        let event_listeners: Arc<
            Mutex<std::collections::HashMap<u64, Arc<dyn VaultEventListener>>>,
        > = Arc::new(Mutex::new(std::collections::HashMap::new()));
        let retention_days = Arc::new(std::sync::atomic::AtomicU32::new(
            config.oplog_retention_days,
        ));
        let compaction_queued: Arc<Mutex<std::collections::HashSet<String>>> =
            Arc::new(Mutex::new(std::collections::HashSet::new()));
        let compaction_dirty: Arc<Mutex<std::collections::HashSet<String>>> =
            Arc::new(Mutex::new(std::collections::HashSet::new()));
        let compaction_shutdown = Arc::new(std::sync::atomic::AtomicBool::new(false));
        let (compaction_tx, compaction_rx) = std::sync::mpsc::channel::<CompactionJob>();
        let worker_tx = compaction_tx.clone();
        let compaction_join = {
            let cache_dir = config.cache_dir.clone();
            let threshold_bytes = config.oplog_compaction_threshold_bytes as u64;
            let threshold_entries = config.oplog_compaction_threshold_entries as u64;
            let retention = Arc::clone(&retention_days);
            let listeners = Arc::clone(&event_listeners);
            let state = Arc::clone(&oplog_state);
            let queued = Arc::clone(&compaction_queued);
            let dirty = Arc::clone(&compaction_dirty);
            let shutdown = Arc::clone(&compaction_shutdown);
            std::thread::Builder::new()
                .name("slate-oplog-compactor".into())
                .spawn(move || {
                    compaction_worker_loop(
                        compaction_rx,
                        worker_tx,
                        cache_dir,
                        threshold_bytes,
                        threshold_entries,
                        retention,
                        listeners,
                        state,
                        queued,
                        dirty,
                        shutdown,
                    );
                })
                .expect("spawn compaction worker")
        };

        Ok(Self {
            provider,
            conn: Mutex::new(conn),
            config,
            oplog_state,
            next_oplog_stem_salt: AtomicU64::new(0),
            remnant_logs: Mutex::new((0, Vec::new())),
            cache_created_this_open,
            event_listeners,
            next_listener_token: AtomicU64::new(1),
            retention_days,
            compaction_tx,
            compaction_queued,
            compaction_dirty,
            compaction_shutdown,
            compaction_join: Mutex::new(Some(compaction_join)),
            math_prefs,
            bib_index: Mutex::new(bib_index),
            csl_styles: Mutex::new(std::collections::HashMap::new()),
            render_cache: crate::citations::render::RenderCache::default(),
            canvases: Mutex::new(std::collections::HashMap::new()),
            next_canvas_handle: AtomicU64::new(1),
            bases: Mutex::new(std::collections::HashMap::new()),
            next_base_handle: AtomicU64::new(1),
            bases_generation: AtomicU64::new(1),
        })
    }

    /// Convenience: open a vault rooted at `root` using `FsVaultProvider`.
    /// Cache lives at `<root>/.slate` per the locked storage layout.
    ///
    /// The vault root must already exist as a directory. A typo'd path
    /// would otherwise `create_dir_all` its way into existence under
    /// `open`, silently materializing a fresh empty vault on disk.
    pub fn from_filesystem(root: PathBuf) -> Result<Self, VaultError> {
        // The desktop/host default is the single-user `"local"` actor
        // (`SessionConfig::new`). The op-log attribution plumbing lives
        // in `from_filesystem_with_actor`; this preserves the historic
        // signature every host + the uniffi wrapper already call.
        Self::from_filesystem_with_actor(root, "local")
    }

    /// Same as [`from_filesystem`](Self::from_filesystem) but stamps a
    /// caller-chosen `user_actor_id` into every op-log entry this
    /// session appends.
    ///
    /// A vault can be held open by more than one process at once — the
    /// Slate app and the `slate` CLI, say. Both go through `save_text`'s
    /// `expected_content_hash` compare-and-swap, so neither can silently
    /// clobber the other's writes; the op-log's `user_actor_id` is the
    /// *honest label* of which writer produced an entry. The desktop app
    /// keeps `"local"`; the CLI passes `"cli"` (see
    /// `crates/slate-cli/src/session.rs`), so a second writer's entries
    /// are attributable in the history without inventing any new write
    /// plumbing. Everything else (templates auto-detect, citations prefs,
    /// provider construction) is identical to `from_filesystem`.
    pub fn from_filesystem_with_actor(
        root: PathBuf,
        user_actor_id: &str,
    ) -> Result<Self, VaultError> {
        if !root.is_dir() {
            return Err(VaultError::InvalidPath {
                path: root.display().to_string(),
                reason: "vault root does not exist or is not a directory".into(),
            });
        }
        let cache_dir = root.join(".slate");
        let mut config = SessionConfig::new(cache_dir);
        config.user_actor_id = user_actor_id.to_string();
        // Obsidian-convention auto-detect. Callers wanting a different
        // layout can mutate `templates_dir` and call `open` directly.
        if root.join("Templates").is_dir() {
            config.templates_dir = Some("Templates".to_string());
        }
        // Vault-shipped config (#411): a root `slate.json` may pin the
        // templates directory explicitly — an explicit declaration
        // beats the convention auto-detect above. Only honored when
        // the named directory actually exists, so a stale config
        // can't silently point templates at nothing.
        let root_cfg = crate::vault_config::read_root_vault_config(&root);
        if let Some(dir) = root_cfg.templates_directory.as_ref().filter(|d| {
            // Red-team L1: only honor clean vault-relative paths.
            // `root.join` would happily accept `..` segments and
            // absolute paths (join replaces on absolute), and those
            // later hard-error in `list_templates` instead of
            // falling back — config must degrade, not break the
            // template picker.
            let p = std::path::Path::new(d.as_str());
            p.is_relative()
                && p.components()
                    .all(|c| matches!(c, std::path::Component::Normal(_)))
                && root.join(d.as_str()).is_dir()
        }) {
            config.templates_dir = Some(dir.clone());
        }
        // Citations prefs across both config surfaces (#411):
        // `.slate/prefs.json` wins where it speaks; the vault-shipped
        // root `slate.json` fills in otherwise. Missing files are
        // fine (default-empty prefs); a malformed file surfaces as
        // `VaultError::PrefsUnreadable`.
        config.citations_prefs = crate::citations::prefs::read_citations_prefs(&root)?;
        // History prefs (O-5 #543): the `.slate/prefs.json` `history`
        // section overrides the default retention window; absence
        // keeps the default. Malformed prefs fail the open loudly —
        // the citations policy, applied consistently.
        if let Some(prefs) =
            crate::history_prefs::read_history_prefs(&root.join(".slate").join("prefs.json"))?
        {
            config.oplog_retention_days = prefs.retention_days;
        }
        let provider = Arc::new(FsVaultProvider::new(root));
        Self::open(provider, config)
    }

    /// Close the session and flush the database. Equivalent to dropping
    /// the session, but explicit for callers that want a `Result`.
    pub fn close(self) -> Result<(), VaultError> {
        drop(self);
        Ok(())
    }

    /// Walk the vault, indexing every file into the metadata cache.
    ///
    /// Hidden files and directories (anything whose name starts with `.`)
    /// are skipped — most importantly `.slate` itself (our cache) and
    /// `.obsidian` (Obsidian-compatible vaults).
    ///
    /// On individual-file errors the scanner records the error in the
    /// report and continues. A cancellation request aborts the scan
    /// mid-transaction; nothing partially-applied lands in the database.
    pub fn scan_initial(&self, cancel: &CancelToken) -> Result<ScanReport, VaultError> {
        self.scan_initial_with_progress(cancel, None)
    }

    /// Same as `scan_initial` but with optional progress events.
    ///
    /// Pass `Some(listener)` and the scanner will emit a `Started`
    /// event with the pre-scan file count, one `FileIndexed` event
    /// per file (both fast and slow path), and exactly one terminal
    /// `Finished` or `Cancelled` event. The listener is always
    /// called from the scanner's thread; UI hosts should marshal
    /// back to their main actor asynchronously inside `on_progress`
    /// rather than block (the session's SQLite lock is held for the
    /// scan's duration).
    pub fn scan_initial_with_progress(
        &self,
        cancel: &CancelToken,
        listener: Option<Arc<dyn ScanProgressListener>>,
    ) -> Result<ScanReport, VaultError> {
        // #802: coarse lifecycle events bracket the scan. Started
        // fires before the session lock is taken; Finished after it
        // drops; the reconcile pair fires under the lock (the
        // ScanProgressListener precedent — listeners marshal, never
        // reenter synchronously).
        self.notify_index_phase(IndexPhase::ScanStarted, 0);
        let mut conn = self.conn.lock().expect("session connection mutex");
        // O-6 (#544): observe a parser bump BEFORE the scan runs — the
        // scan stamps every row with the new version, erasing the
        // evidence — and convert it straight into the DURABLE
        // staleness marker (adversarial round 3): an in-memory flag
        // would be consumed by this scan's rebuild attempt, so one
        // busy-failed or crashed rebuild would lose the obligation
        // with the evidence already erased. Query failure
        // conservatively reads as "bumped"; marker-write failure falls
        // back to forcing THIS scan's rebuild in memory (better one
        // volatile attempt than none).
        let parser_bumped: bool = conn
            .query_row(
                "SELECT EXISTS(SELECT 1 FROM files WHERE parser_version != ?1)",
                [self.config.parser_version],
                |row| row.get(0),
            )
            .unwrap_or(true);
        let mut force_rebuild = false;
        if parser_bumped
            && let Err(e) = conn.execute("INSERT INTO oplog_events_stale (marker) VALUES (1)", [])
        {
            log::warn!("oplog_events parser-bump marker write failed");
            log::debug!("oplog_events parser-bump marker failure detail: {e}");
            force_rebuild = true;
        }
        let report = scan_vault(
            self.provider.as_ref(),
            &mut conn,
            self.config.parser_version,
            self.config.large_file_refuse_bytes,
            cancel,
            listener.as_deref(),
        )?;
        self.bump_bases_generation();
        // Re-attach op logs to live files and surface deleted-file
        // remnants (O-1 #539). Best-effort: a reconcile failure
        // degrades history features, never the scan itself.
        self.notify_index_phase(IndexPhase::ReconcileStarted, 0);
        self.reconcile_oplogs(&mut conn);
        self.notify_index_phase(IndexPhase::ReconcileFinished, 0);
        // O-6 (#544): regenerate the derived temporal-events index if
        // it's stale. Runs after the reconcile so bindings are fresh.
        // Every trigger rides the durable marker — append-window
        // crashes, adoptions (written inside the reconcile
        // transaction), and parser bumps (above) — so a failed rebuild
        // keeps its obligation for the next scan.
        self.rebuild_oplog_events_if_stale(&mut conn, force_rebuild);
        // #831: age out event rows past the retention window — the
        // producers' shared rule applied as a pruner, so long-lived
        // active files stop accumulating rows their next compaction
        // would fold anyway. Best-effort, like the rebuild above: a
        // failed DELETE leaves extra rows, never wrong ones.
        //
        // Deliberately unindexed (Codoki on #844): a bare ts_ms index
        // would tax every save's insert for a once-per-open DELETE
        // whose table this very rule keeps bounded — the full scan is
        // O(retained rows), single-digit ms at a 90-day heavy-use
        // bound, and the one large pass (first open on a pre-#831
        // vault) is a one-time upgrade cost.
        let events_cutoff = retention_cutoff_ms(self.retention_days());
        match conn.execute(
            "DELETE FROM oplog_events WHERE ts_ms <= ?1",
            rusqlite::params![events_cutoff],
        ) {
            // Deleted-count observability (Codoki on #844): steady
            // state prunes a trickle; a large count marks the one-time
            // legacy cleanup and would flag a pathological producer.
            Ok(aged_out) if aged_out > 0 => {
                log::debug!("oplog_events age-out removed {aged_out} rows");
            }
            Ok(_) => {}
            Err(e) => {
                log::warn!("oplog_events age-out failed");
                log::debug!("oplog_events age-out failure detail: {e}");
            }
        }
        // On-open compaction sweep (O-2 #540): enqueue any bound log
        // whose file size exceeds the byte threshold — stat only, no
        // log reads.
        self.sweep_oversized_logs(&conn);
        drop(conn);
        self.notify_index_phase(IndexPhase::ScanFinished, report.files_seen);
        Ok(report)
    }

    /// O-2 (#540): stat every bound log and queue the oversized ones
    /// for background compaction. Best-effort; runs after the scan
    /// reconcile so bindings are fresh.
    fn sweep_oversized_logs(&self, conn: &Connection) {
        let rows: Vec<(i64, String, String)> = match conn
            .prepare("SELECT id, oplog_name, path FROM files WHERE oplog_name IS NOT NULL")
            .and_then(|mut stmt| {
                stmt.query_map([], |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)))
                    .and_then(std::iter::Iterator::collect)
            }) {
            Ok(rows) => rows,
            Err(e) => {
                log::warn!("oplog sweep query failed");
                log::debug!("oplog sweep query failure: {e}");
                return;
            }
        };
        let threshold = self.config.oplog_compaction_threshold_bytes as u64;
        // Conservative cross-session entry-count trigger (adversarial
        // review): a log cannot hold more entries than its size divided
        // by a minimum realistic frame (~64 B with real hashes), so any
        // file smaller than threshold_entries × 64 provably satisfies
        // the entry threshold and is skipped without IO. Bigger files
        // get ONE background read per open; a fine count reports Futile
        // and the flag stops repeats within the session.
        let min_avg_entry_bytes: u64 = 64;
        let entry_estimate_floor = (self.config.oplog_compaction_threshold_entries as u64)
            .saturating_mul(min_avg_entry_bytes);
        for (file_id, log_name, path) in rows {
            let log_path = crate::oplog::oplog_path_for_name(&self.config.cache_dir, &log_name);
            if let Ok(meta) = std::fs::metadata(&log_path)
                && (meta.len() > threshold || meta.len() > entry_estimate_floor)
            {
                // Respect an in-session futility verdict (a giant
                // single-entry log doesn't shrink until it grows).
                let futile = self
                    .oplog_state
                    .lock()
                    .expect("oplog state mutex")
                    .get(&file_id)
                    .is_some_and(|s| s.compaction_futile);
                if !futile {
                    self.enqueue_compaction(file_id, &log_name, &path);
                }
            }
        }
    }

    /// O-6 (#544): regenerate the derived `oplog_events` table from
    /// the bound logs when it is empty (fresh cache — migration 029
    /// landing, or a cache rebuild) or the durable staleness marker is
    /// set — the one channel every trigger rides: append-window
    /// crashes/failures, reconcile adoptions, and parser bumps
    /// (adversarial round 3: volatile triggers lose their obligation
    /// to one failed rebuild). `force` is the in-memory fallback for a
    /// failed marker write. One transaction end to end, markers
    /// cleared inside it: a crash mid-rebuild rolls back to the marked
    /// state and the next open retries. Best-effort — failure degrades
    /// the temporal operators to stale results, never the scan.
    /// Unbound remnant logs contribute nothing: events only exist for
    /// live `files` rows, which is all the operators can ever filter.
    fn rebuild_oplog_events_if_stale(&self, conn: &mut Connection, force: bool) {
        if let Err(e) = self.rebuild_oplog_events_inner(conn, force) {
            log::warn!("oplog_events rebuild failed");
            log::debug!("oplog_events rebuild failure detail: {e:?}");
        }
    }

    fn rebuild_oplog_events_inner(
        &self,
        conn: &mut Connection,
        force: bool,
    ) -> Result<(), VaultError> {
        let empty: bool =
            conn.query_row("SELECT NOT EXISTS(SELECT 1 FROM oplog_events)", [], |row| {
                row.get(0)
            })?;
        let stale: bool = conn.query_row(
            "SELECT EXISTS(SELECT 1 FROM oplog_events_stale)",
            [],
            |row| row.get(0),
        )?;
        if !empty && !force && !stale {
            return Ok(());
        }
        let cutoff_ms = retention_cutoff_ms(self.retention_days());
        let tx = conn.transaction()?;
        tx.execute("DELETE FROM oplog_events", [])?;
        tx.execute("DELETE FROM oplog_events_stale", [])?;
        let bound: Vec<(i64, String)> = tx
            .prepare("SELECT id, oplog_name FROM files WHERE oplog_name IS NOT NULL")?
            .query_map([], |row| Ok((row.get(0)?, row.get(1)?)))?
            .collect::<Result<_, _>>()?;
        for (file_id, log_name) in bound {
            // A missing/torn log yields its readable prefix or nothing;
            // either way the file simply has fewer (or no) events.
            let entries = match crate::oplog::read_oplog(&self.config.cache_dir, &log_name) {
                Ok(entries) => entries,
                Err(_) => continue,
            };
            let events = crate::oplog_events::derive_events_for_log(&entries);
            insert_oplog_events(&tx, file_id, &events, cutoff_ms)?;
        }
        tx.commit()?;
        Ok(())
    }

    /// Scan-time op-log reconcile (O-1 #539): bind every on-disk log to
    /// its live file, salvage what a cache rebuild orphaned, and
    /// collect deleted-file remnants for O-3.
    ///
    /// Steps (o_spec §O-1, adjusted for the migration-027 stamping —
    /// legacy `<id>.oplog` bindings were written into `files.oplog_name`
    /// at upgrade time, so no id-based adoption ever happens here):
    ///
    /// 1. Fast path: every `*.oplog` stem matching some live
    ///    `files.oplog_name` is bound. With an intact cache this covers
    ///    everything and the reconcile does **no** log IO.
    /// 2. Remaining unbound logs are forward-walked once each: header
    ///    path, last `PathChanged`, tail hash/timestamp, and the
    ///    `hash_after` set. Effective path = last `PathChanged.to`,
    ///    else the header's `created_path`, else unknown (bare v1 log
    ///    after a rebuild).
    /// 3. Adoption: effective path names a live file with
    ///    `oplog_name IS NULL` → claim; no path match → content
    ///    salvage, but ONLY when this open created the cache (rebuild
    ///    — the case that legitimately orphans bindings): exactly ONE
    ///    unbound live file whose current `content_hash` appears in
    ///    the log's `hash_after` set → claim; zero or several → no
    ///    adoption. On an intact cache, content coincidence must
    ///    never re-bind a dead note's log (the template-twin hazard).
    /// 4. Unbound + effective path known + that path is not a live
    ///    file → **deleted-file remnant** ([`RemnantLog`]).
    /// 5. Anything conflicted (two logs claiming one file, a claimed
    ///    path already bound) or pathless → quarantine: left on disk,
    ///    warned about, invisible to features. Never guess.
    ///
    fn reconcile_oplogs(&self, conn: &mut Connection) {
        match self.reconcile_oplogs_inner(conn) {
            Ok(remnants) => {
                let mut guard = self.remnant_logs.lock().expect("remnant logs mutex");
                guard.0 += 1;
                guard.1 = remnants;
            }
            Err(e) => {
                // Leave the previous remnant set in place — stale data
                // beats a silently emptied Deleted list. The warn stays
                // path-free (lib.rs privacy rule); detail rides debug.
                log::warn!("oplog reconcile failed");
                log::debug!("oplog reconcile failure detail: {e:?}");
            }
        }
    }

    fn reconcile_oplogs_inner(&self, conn: &mut Connection) -> Result<Vec<RemnantLog>, VaultError> {
        let dir = crate::oplog::oplog_dir(&self.config.cache_dir);
        let read_dir = match std::fs::read_dir(&dir) {
            Ok(rd) => rd,
            Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Ok(Vec::new()),
            Err(e) => return Err(VaultError::Io(e)),
        };
        let mut stems: Vec<String> = Vec::new();
        for entry in read_dir {
            let Ok(entry) = entry else { continue };
            let name = entry.file_name();
            let Some(name) = name.to_str() else { continue };
            if let Some(stem) = name.strip_suffix(".oplog") {
                stems.push(stem.to_string());
            }
        }
        if stems.is_empty() {
            return Ok(Vec::new());
        }
        stems.sort(); // deterministic adoption/quarantine decisions

        // One-writer discipline from the start (#787): binding updates
        // below write `files.oplog_name`.
        let tx = conn.transaction_with_behavior(rusqlite::TransactionBehavior::Immediate)?;

        struct LiveFile {
            id: i64,
            path: String,
            content_hash: String,
            oplog_name: Option<String>,
        }
        let live: Vec<LiveFile> = tx
            .prepare("SELECT id, path, content_hash, oplog_name FROM files")?
            .query_map([], |row| {
                Ok(LiveFile {
                    id: row.get(0)?,
                    path: row.get(1)?,
                    content_hash: row.get(2)?,
                    oplog_name: row.get(3)?,
                })
            })?
            .collect::<Result<Vec<_>, _>>()?;

        let bound: std::collections::HashSet<&str> = live
            .iter()
            .filter_map(|f| f.oplog_name.as_deref())
            .collect();
        let by_path: std::collections::HashMap<&str, &LiveFile> =
            live.iter().map(|f| (f.path.as_str(), f)).collect();

        // (stem, file_id) adoption claims, resolved after the walk so
        // conflicts are visible before any binding is written.
        let mut claims: Vec<(String, i64)> = Vec::new();
        // Remnant candidates keyed by stem; pruned if their claim won.
        let mut remnants: Vec<RemnantLog> = Vec::new();

        for stem in &stems {
            if bound.contains(stem.as_str()) {
                continue; // fast path — already bound, no log IO
            }
            let (header, entries) =
                match crate::oplog::read_oplog_with_header(&self.config.cache_dir, stem) {
                    Ok(pair) => pair,
                    Err(e) => {
                        log::warn!("oplog reconcile: unreadable log {stem}: {}", e.kind());
                        log::debug!("oplog reconcile: unreadable log {stem}: {e}");
                        continue; // quarantine
                    }
                };

            // Effective path: last PathChanged wins over the creation
            // record. Undecodable annotated payloads are skipped —
            // best-effort metadata, never a walk-stopper here.
            let mut effective_path: Option<String> = header.created_path.clone();
            for entry in &entries {
                if entry.op_kind == crate::oplog::OpKind::Annotated
                    && let Ok((_, _, anns)) = crate::oplog::decode_annotated(&entry.payload_bytes)
                {
                    for ann in anns {
                        if let crate::oplog::OpAnnotation::PathChanged { to, .. } = ann {
                            effective_path = Some(to);
                        }
                    }
                }
            }

            // Adoption by path.
            if let Some(eff) = effective_path.as_deref()
                && let Some(file) = by_path.get(eff)
            {
                if file.oplog_name.is_none() {
                    claims.push((stem.clone(), file.id));
                } else {
                    // A live file at this path already has its own log
                    // — e.g. a hand-copied log file. Never guess; but
                    // DO age it out on the same retention rule as
                    // remnants (O-2: quarantined logs are eventually
                    // reclaimed too).
                    log::warn!(
                        "oplog reconcile: log {stem} claims an already-bound file; quarantined"
                    );
                    log::debug!("oplog reconcile: {stem} claimed path {eff:?}");
                    let cutoff = retention_cutoff_ms(self.retention_days());
                    // Newest-timestamp rule — same backwards-clock
                    // conservatism as the remnant branch below.
                    if !entries.is_empty()
                        && entries
                            .iter()
                            .map(|e| e.timestamp_ms)
                            .max()
                            .is_some_and(|ts| ts <= cutoff)
                    {
                        let log_path =
                            crate::oplog::oplog_path_for_name(&self.config.cache_dir, stem);
                        if let Err(e) = std::fs::remove_file(&log_path) {
                            log::warn!("oplog reclamation failed for {stem}: {}", e.kind());
                            log::debug!("oplog reclamation failure detail: {e}");
                        }
                    }
                }
                continue;
            }

            // Content salvage — REBUILD-ONLY (red-team hardening over
            // the spec text): on an intact cache an unbound live file
            // was never saved through Slate, so a content coincidence
            // (two notes created from one template) must not attach a
            // dead note's history to it. Only a rebuild legitimately
            // orphans bindings; there, exactly one unbound live file
            // whose current hash this log once produced is safe to
            // re-bind.
            if self.cache_created_this_open {
                let hash_set: std::collections::HashSet<&str> = entries
                    .iter()
                    .map(|e| e.content_hash_after.as_str())
                    .collect();
                let mut matches = live.iter().filter(|f| {
                    f.oplog_name.is_none() && hash_set.contains(f.content_hash.as_str())
                });
                if let (Some(only), None) = (matches.next(), matches.next()) {
                    claims.push((stem.clone(), only.id));
                    continue;
                }
            }

            // Remnant (deleted file) or quarantine — with the O-2
            // retention sweep applied first: an unbound log whose tail
            // is older than the retention window is DELETED (`05` §7.5
            // "old ops are discarded", applied to deleted files). This
            // both bounds `.slate/oplog` disk and is the mechanism by
            // which the deleted-files list ages out. Within retention,
            // remnants are never rewritten — full fidelity for
            // recovery. Unreadable logs are left in place (no
            // timestamp to judge by; conservative).
            // Age = the NEWEST wall-clock timestamp anywhere in the
            // log, not the tail's (adversarial-review High): clocks
            // step backwards, so a fresh deletion can carry a
            // stale-looking tail — deleting on the tail alone would
            // permanently destroy the only recovery copy. Requiring
            // EVERY entry to be past the cutoff is the conservative
            // reading; a log written entirely under a regressed clock
            // can still age out early, which is the best a wall clock
            // can do (documented limit).
            let cutoff = retention_cutoff_ms(self.retention_days());
            let newest_ts = entries.iter().map(|e| e.timestamp_ms).max();
            match (effective_path, entries.last()) {
                (_, Some(_)) if newest_ts.is_some_and(|ts| ts <= cutoff) => {
                    let log_path = crate::oplog::oplog_path_for_name(&self.config.cache_dir, stem);
                    if let Err(e) = std::fs::remove_file(&log_path) {
                        log::warn!("oplog reclamation failed for {stem}: {}", e.kind());
                        log::debug!("oplog reclamation failure detail: {e}");
                    }
                }
                (Some(eff), Some(tail)) => remnants.push(RemnantLog {
                    stem: stem.clone(),
                    effective_path: eff,
                    tail_hash: tail.content_hash_after.clone(),
                    tail_timestamp_ms: tail.timestamp_ms,
                }),
                _ => {
                    log::warn!(
                        "oplog reconcile: log {stem} has no usable path record; quarantined"
                    );
                }
            }
        }

        // Resolve claims: a file claimed by more than one log gets
        // none of them (quarantine both — never guess).
        let mut claims_per_file: std::collections::HashMap<i64, u32> =
            std::collections::HashMap::new();
        for (_, file_id) in &claims {
            *claims_per_file.entry(*file_id).or_insert(0) += 1;
        }
        let mut adopted_any = false;
        for (stem, file_id) in &claims {
            if claims_per_file[file_id] > 1 {
                log::warn!("oplog reconcile: log {stem} conflicts with another claim; quarantined");
                continue;
            }
            let newly_bound = tx.execute(
                "UPDATE files SET oplog_name = ?1 WHERE id = ?2 AND oplog_name IS NULL",
                rusqlite::params![stem, file_id],
            )?;
            adopted_any |= newly_bound > 0;
        }
        if adopted_any {
            // A fresh binding attaches history the events table has
            // never seen — set the O-6 staleness marker IN THIS
            // transaction (#544, adversarial round 3): an in-memory
            // flag alone dies with a crash or a busy-failed rebuild
            // between this commit and the rebuild's, leaving the
            // adopted history permanently invisible. Durable marker ⇒
            // the rebuild fires now or at any later scan.
            tx.execute("INSERT INTO oplog_events_stale (marker) VALUES (1)", [])?;
        }
        tx.commit()?;
        Ok(remnants)
    }

    /// Fetch full per-file metadata, including parsed headings.
    ///
    /// Returns `Ok(None)` if the path isn't in the index yet — call
    /// `scan_initial` first, or pass a path the scanner has visited.
    pub fn get_file_metadata(&self, path: &str) -> Result<Option<FileMetadata>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        get_file_metadata_impl(&conn, path)
    }

    /// Read a vault file's contents as UTF-8 text.
    ///
    /// - Refuses to read files larger than
    ///   `SessionConfig::large_file_refuse_bytes` and returns
    ///   `VaultError::FileTooLarge { path, size }` without ever
    ///   touching the file's bytes.
    /// - Returns `VaultError::InvalidUtf8 { path }` if the contents
    ///   aren't valid UTF-8. Unlike the scanner's heading parse —
    ///   which uses lossy decode to preserve as much structure as
    ///   possible — the editor / reader path needs to be honest
    ///   about decode failures so we don't silently substitute
    ///   replacement characters into a file the user is about to
    ///   look at.
    /// - Path validation (no absolute, no `..`, no Windows prefix)
    ///   is inherited from `FsVaultProvider::read_file`.
    pub fn read_text(&self, path: &str) -> Result<String, VaultError> {
        let limit = self.config.large_file_refuse_bytes;
        let stat = self.provider.stat(path)?;
        if stat.size_bytes > limit {
            return Err(VaultError::FileTooLarge {
                path: path.to_string(),
                size: stat.size_bytes,
            });
        }
        // `read_file_with_cap` allocates at most `limit + 1` bytes
        // even if the file grows between stat and read — that's the
        // TOCTOU-safe ceiling. If the returned buffer hits the +1
        // sentinel, the file exceeded the threshold; we report
        // FileTooLarge but the actual size on disk is unknown to us
        // here (we deliberately stopped reading), so we surface
        // `limit + 1` as a lower bound rather than lying.
        let bytes = self.provider.read_file_with_cap(path, limit)?;
        if (bytes.len() as u64) > limit {
            return Err(VaultError::FileTooLarge {
                path: path.to_string(),
                size: bytes.len() as u64,
            });
        }
        String::from_utf8(bytes).map_err(|_| VaultError::InvalidUtf8 {
            path: path.to_string(),
        })
    }

    /// Read a binary attachment (image, PDF, etc.) from the vault.
    ///
    /// Same size-cap pattern as `read_text` but bounded by
    /// `SessionConfig::large_attachment_refuse_bytes` (default
    /// 50 MiB). Returns the bytes alongside an inferred MIME type:
    /// extension first, falling back to magic-byte sniffing for
    /// the common image formats, then `application/octet-stream`.
    ///
    /// Used by `resolve_embed` for image embeds and exposed
    /// directly for future "open original" / "copy attachment"
    /// affordances.
    pub fn read_attachment(
        &self,
        path: &str,
    ) -> Result<crate::embeds::AttachmentBytes, VaultError> {
        let limit = self.config.large_attachment_refuse_bytes;
        let stat = self.provider.stat(path)?;
        if stat.size_bytes > limit {
            return Err(VaultError::FileTooLarge {
                path: path.to_string(),
                size: stat.size_bytes,
            });
        }
        let bytes = self.provider.read_file_with_cap(path, limit)?;
        if (bytes.len() as u64) > limit {
            return Err(VaultError::FileTooLarge {
                path: path.to_string(),
                size: bytes.len() as u64,
            });
        }
        let mime = crate::embeds::infer_mime(path, &bytes);
        Ok(crate::embeds::AttachmentBytes { bytes, mime })
    }

    /// Save UTF-8 text to a vault-relative path, refresh the index,
    /// and append a `WholeFileReplace` entry to that file's op-log.
    ///
    /// `expected_content_hash`:
    /// - `None` → save unconditionally. Used by callers that don't
    ///   track the on-disk hash (scripted writers).
    /// - `Some(h)` → before writing, stat + hash the file currently
    ///   on disk. If it doesn't match `h`, return
    ///   `VaultError::WriteConflict` and leave the file untouched.
    ///   This is the path the Mac editor uses to detect external
    ///   changes between read and save, and the `slate write` CLI verb
    ///   rides the same discipline (#641).
    ///
    /// The compare-and-swap is atomic **across processes**, not just
    /// within a session: the rehash-through-rename window runs inside an
    /// IMMEDIATE transaction on the shared `.slate/cache.sqlite`, whose
    /// one-writer lock is file-based. Two processes racing the same
    /// expected hash serialize on that lock; exactly one wins and the
    /// other observes the winner's bytes and gets `WriteConflict`
    /// (see `save_text_locked` and the concurrency tests in
    /// `session/tests/save.rs`).
    ///
    /// On success the index reflects the new state in one atomic
    /// transaction (`files` row + `headings` + `links` + `properties`
    /// rows replaced together; FTS5 rebuilt by the migration-006
    /// trigger on the `body_text` update) so consumers never observe
    /// a half-updated index. The op-log append happens after the
    /// SQLite commit and is best-effort: a failed append logs a
    /// warning but does not fail the save — the user's text is on
    /// disk and indexed either way.
    pub fn save_text(
        &self,
        path: &str,
        contents: &str,
        expected_content_hash: Option<&str>,
    ) -> Result<SaveReport, VaultError> {
        validate_save_path(path)?;

        let new_size = contents.len() as u64;
        if new_size > self.config.large_file_refuse_bytes {
            return Err(VaultError::FileTooLarge {
                path: path.to_string(),
                size: new_size,
            });
        }

        // Hold the connection lock for the entire save. This both
        // serializes the SQLite work and gives the op-log append a
        // process-wide critical section, so two concurrent saves to
        // the same file_id can never tear a frame.
        let mut conn = self.conn.lock().expect("session connection mutex");
        self.save_text_locked(&mut conn, path, contents, expected_content_hash, &[])
    }

    /// Body of `save_text` minus the path validation, size check, and
    /// mutex acquisition. Callers must hold the session connection
    /// mutex (via `conn`) for the duration of this call.
    ///
    /// Exists so `toggle_task_status` can hold the mutex across its
    /// read+parse+rewrite+save sequence — passing a stale-pre-read
    /// payload to a non-locked `save_text` was the lost-update race
    /// fixed in #135.
    ///
    /// `annotations` is the save's semantic intent (O-1 #539): callers
    /// that know what the edit *means* — `set_property`,
    /// `delete_property`, `set_frontmatter_source`,
    /// `toggle_task_status` — pass it here and the op-log entry wraps
    /// it; plain text saves pass `&[]`. Kept off the public `save_text`
    /// signature: hosts never supply intent directly.
    fn save_text_locked(
        &self,
        conn: &mut Connection,
        path: &str,
        contents: &str,
        expected_content_hash: Option<&str>,
        annotations: &[crate::oplog::OpAnnotation],
    ) -> Result<SaveReport, VaultError> {
        // Cross-process critical section (#641 adversarial review).
        //
        // The expected-hash compare-and-swap below re-reads and re-hashes
        // the file on disk; it is only sound if no OTHER writer can slip
        // a rename in between that rehash and our own atomic write. The
        // session's connection mutex serializes writers within one
        // process, but the Slate app and the `slate` CLI are separate
        // processes sharing this vault. The shared `.slate/cache.sqlite`
        // is the cross-process rendezvous: opening the index transaction
        // IMMEDIATE acquires SQLite's one-writer lock (file-based, so it
        // excludes other processes too) BEFORE the rehash and holds it
        // through the rename + index commit. Two racing writers therefore
        // serialize here — the loser blocks (rusqlite's built-in 5s
        // busy_timeout), then re-hashes and sees the winner's bytes →
        // `WriteConflict`, never a silent clobber. A post-timeout
        // SQLITE_BUSY surfaces as `Db` (the CLI maps it to its "cache is
        // busy" copy).
        let tx = conn.transaction_with_behavior(rusqlite::TransactionBehavior::Immediate)?;

        // `old_contents` is `Some(text)` only on the Some(expected_hash)
        // path with UTF-8-decodable disk content — the diff-on-save base
        // (#378). The conflict-check read already happens, so capturing
        // those bytes costs no extra I/O. The None path (CLI/scripted)
        // and any non-UTF-8 file leave it `None`, which routes the op-log
        // append to a `WholeFileReplace` snapshot.
        let (hash_before, old_contents): (String, Option<String>) =
            if let Some(expected) = expected_content_hash {
                let (old_bytes, current_hash, current_mtime_ms) = read_disk_contents_and_hash(
                    self.provider.as_ref(),
                    path,
                    self.config.large_file_refuse_bytes,
                )?;
                if current_hash != expected {
                    return Err(VaultError::WriteConflict {
                        current_content_hash: current_hash,
                        expected_content_hash: expected.to_string(),
                        current_mtime_ms,
                    });
                }
                (current_hash, String::from_utf8(old_bytes).ok())
            } else {
                // No conflict check: the cached index hash is good enough
                // for the entry's `hash_before`, and we don't re-read disk
                // just to diff — the None path logs a `WholeFileReplace`.
                let cached = tx
                    .query_row(
                        "SELECT content_hash FROM files WHERE path = ?1",
                        rusqlite::params![path],
                        |row| row.get::<_, String>(0),
                    )
                    .optional()?
                    .unwrap_or_default();
                (cached, None)
            };

        // Created-vs-Modified for the #802 event, read inside the
        // save's own transaction so a racing writer can't flip it.
        let existed: bool = tx
            .query_row(
                "SELECT EXISTS(SELECT 1 FROM files WHERE path = ?1)",
                rusqlite::params![path],
                |row| row.get(0),
            )
            .unwrap_or(false);

        // Atomic write happens before the index update so that a
        // subsequent SQLite failure leaves the file on disk in a
        // consistent state. Worst case: the file is newer than the
        // index, and the next scan picks it up via mtime/size/ctime.
        // (The write happens INSIDE the IMMEDIATE transaction so the
        // check-to-rename window above stays exclusive; a failed commit
        // still leaves a fully-written file, never a partial one —
        // `atomic_write` is temp-file + rename.)
        self.provider.write_file(path, contents.as_bytes())?;

        let new_stat = self.provider.stat(path)?;
        let new_hash = crate::vault::content_hash(contents.as_bytes());

        let now = now_ms();
        let file_id = self.index_saved_file(&tx, path, contents, &new_stat, &new_hash, now)?;
        // Resolve (allocating on first save) the file's op-log name
        // inside the index transaction, so the binding column commits
        // atomically with the save it serves (O-1 #539). Best-effort:
        // `None` skips the append below exactly like an append failure.
        let oplog_name = self.ensure_oplog_name(&tx, file_id, path);
        tx.commit()?;
        self.bump_bases_generation();

        // Op-log append: best-effort (#378). A logging-disk hiccup must
        // not throw away the user's just-saved text, so all of the
        // diff/encode/append work below swallows its errors.
        if let Some(log_name) = oplog_name.as_deref() {
            self.append_save_to_oplog(
                conn,
                file_id,
                log_name,
                path,
                &hash_before,
                &new_hash,
                contents,
                old_contents.as_deref(),
                now,
                annotations,
            );
        }

        // #802: the ONE file-change emission seat for text writes —
        // plain saves, property/task/frontmatter edits, canvas
        // serialization, structural link rewrites, and restores all
        // commit through this seam, so no mutator can silently bypass
        // the event stream. A same-content save still committed a
        // write (the identity-axiom anchor), so Modified is honest.
        // Delivered with the session lock held — the documented
        // contract: listeners marshal, never reenter synchronously.
        self.notify_file_change(
            if existed {
                FileChangeKind::Modified
            } else {
                FileChangeKind::Created
            },
            path,
            None,
        );

        Ok(SaveReport {
            new_content_hash: new_hash,
            new_size_bytes: new_stat.size_bytes,
            new_mtime_ms: new_stat.mtime_ms,
        })
    }

    /// Resolve — allocating on first use — the op-log name stem bound
    /// to `file_id` through `files.oplog_name` (O-1 #539). The column
    /// is the ONLY binding: log paths are never derived from
    /// `files.id`, whose rowids SQLite recycles after a delete (the
    /// hazard documented above [`OplogAppendState`] — a recycled id
    /// must never inherit a dead note's history). Legacy `<id>.oplog`
    /// bindings were stamped into the column once, by migration 027,
    /// at upgrade time — the one moment ids are provably trustworthy
    /// (a rebuilt cache has no rows to stamp).
    ///
    /// Allocation derives `<32 lowercase hex>` stems from
    /// `blake3(path ‖ now_ms ‖ salt)`, re-deriving with a bumped salt
    /// until [`crate::oplog::try_create_log`] wins the creation race
    /// (serialized by the oplog directory lock). Stems are unique
    /// forever and mean nothing.
    ///
    /// Best-effort like every op-log write: `None` means "no log for
    /// this save" and the caller skips the append. The warn carries
    /// the file id only; the path rides debug (lib.rs privacy rule).
    fn ensure_oplog_name(&self, conn: &Connection, file_id: i64, path: &str) -> Option<String> {
        match conn
            .query_row(
                "SELECT oplog_name FROM files WHERE id = ?1",
                rusqlite::params![file_id],
                |row| row.get::<_, Option<String>>(0),
            )
            .optional()
        {
            Ok(Some(Some(name))) => return Some(name),
            Ok(Some(None)) => {} // row exists, no binding yet — allocate below
            Ok(None) => {
                log::warn!("oplog name lookup found no row for file_id={file_id}");
                log::debug!("oplog name lookup miss was for path {path:?}");
                return None;
            }
            Err(e) => {
                log::warn!("oplog name lookup failed for file_id={file_id}: db error");
                log::debug!("oplog name lookup failure for path {path:?}: {e}");
                return None;
            }
        }

        // Collisions are astronomically unlikely (128-bit stems salted
        // by time and counter), so a handful of retries is already
        // paranoid; a persistent failure is an IO problem, not a
        // collision storm.
        for _ in 0..8 {
            let salt = self
                .next_oplog_stem_salt
                .fetch_add(1, std::sync::atomic::Ordering::Relaxed);
            let mut stem_input = Vec::with_capacity(path.len() + 16);
            stem_input.extend_from_slice(path.as_bytes());
            stem_input.extend_from_slice(&now_ms().to_le_bytes());
            stem_input.extend_from_slice(&salt.to_le_bytes());
            // First 16 bytes of the blake3 = first 32 lowercase hex chars.
            let stem = crate::vault::content_hash(&stem_input)[..32].to_string();
            match crate::oplog::try_create_log(&self.config.cache_dir, &stem, path) {
                Ok(true) => {
                    // Guarded UPDATE: never overwrite a binding that
                    // appeared since the lookup (a racing writer in
                    // another process). Zero rows updated → return the
                    // winner's binding; our freshly created log stays
                    // behind as an orphan the reconcile re-binds or
                    // reclaims. The partial UNIQUE index on oplog_name
                    // (migration 027) backstops double-binding at the
                    // schema level.
                    match conn.execute(
                        "UPDATE files SET oplog_name = ?1 WHERE id = ?2 AND oplog_name IS NULL",
                        rusqlite::params![stem, file_id],
                    ) {
                        Ok(0) => {
                            // A racing writer bound the file first —
                            // return the winner's binding. Lookup
                            // failures are logged, not swallowed
                            // (Codoki, PR #790): a DB error here means
                            // no binding is known, so the append is
                            // skipped exactly like an append failure.
                            return match conn
                                .query_row(
                                    "SELECT oplog_name FROM files WHERE id = ?1",
                                    rusqlite::params![file_id],
                                    |row| row.get::<_, Option<String>>(0),
                                )
                                .optional()
                            {
                                Ok(binding) => binding.flatten(),
                                Err(e) => {
                                    log::warn!(
                                        "oplog raced-bind lookup failed for file_id={file_id}: db error"
                                    );
                                    log::debug!(
                                        "oplog raced-bind lookup failure for path {path:?}: {e}"
                                    );
                                    None
                                }
                            };
                        }
                        Ok(_) => return Some(stem),
                        Err(e) => {
                            // The created log file stays behind as an
                            // orphan; the scan reconcile re-binds it
                            // by its header path.
                            log::warn!("oplog name bind failed for file_id={file_id}: db error");
                            log::debug!("oplog name bind failure for path {path:?}: {e}");
                            return None;
                        }
                    }
                }
                Ok(false) => continue,
                Err(e) => {
                    log::warn!(
                        "oplog name allocation failed for file_id={file_id}: {}",
                        e.kind()
                    );
                    log::debug!("oplog name allocation failure for path {path:?}: {e}");
                    return None;
                }
            }
        }
        log::warn!("oplog name allocation exhausted retries for file_id={file_id}");
        log::debug!("oplog name allocation retries exhausted for path {path:?}");
        None
    }

    /// The deleted-file remnant logs from the most recent scan
    /// reconcile (O-1 #539). Refreshed by every `scan_initial`.
    pub fn remnant_logs(&self) -> Vec<RemnantLog> {
        self.remnant_logs
            .lock()
            .expect("remnant logs mutex")
            .1
            .clone()
    }

    /// Atomic `(generation, remnants)` snapshot — the paging path must
    /// never pair a stale set with a newer generation.
    fn remnant_snapshot(&self) -> (u64, Vec<RemnantLog>) {
        let guard = self.remnant_logs.lock().expect("remnant logs mutex");
        (guard.0, guard.1.clone())
    }

    /// Register a session-event listener (O-2 #540). Returns an opaque
    /// token for [`unregister_event_listener`](Self::unregister_event_listener).
    /// Listeners are invoked from background threads and must be cheap
    /// and non-blocking.
    pub fn register_event_listener(&self, listener: Arc<dyn VaultEventListener>) -> u64 {
        let token = self
            .next_listener_token
            .fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        self.event_listeners
            .lock()
            .expect("event listener mutex")
            .insert(token, listener);
        token
    }

    /// Remove a previously registered session-event listener. Unknown
    /// tokens are a no-op (idempotent).
    pub fn unregister_event_listener(&self, token: u64) {
        self.event_listeners
            .lock()
            .expect("event listener mutex")
            .remove(&token);
    }

    /// #802: one snapshot of the registered listeners — the shared
    /// fan-out seat for every event family (Codoki on #846: one
    /// helper, no drift). Snapshotting first (the compaction-dispatch
    /// discipline) means listener code never runs under the registry
    /// mutex.
    fn listener_snapshot(&self) -> Vec<Arc<dyn VaultEventListener>> {
        self.event_listeners
            .lock()
            .expect("event listener mutex")
            .values()
            .cloned()
            .collect()
    }

    /// #802: fan a file-change event out to every listener.
    fn notify_file_change(&self, kind: FileChangeKind, path: &str, previous_path: Option<&str>) {
        let snapshot = self.listener_snapshot();
        if snapshot.is_empty() {
            return;
        }
        let event = FileChangeEvent {
            kind,
            path: path.to_string(),
            previous_path: previous_path.map(str::to_string),
        };
        for listener in snapshot {
            listener.on_file_change(event.clone());
        }
    }

    /// #802: one Renamed event per file a structural move touched —
    /// `report.moved` carries (from, to) for the file itself or every
    /// file under a moved folder.
    fn notify_moved(&self, report: &crate::structural::StructuralReport) {
        for (from, to) in &report.moved {
            self.notify_file_change(FileChangeKind::Renamed, to, Some(from));
        }
    }

    /// #802: fan an index-phase event out (same snapshot seat).
    fn notify_index_phase(&self, phase: IndexPhase, files_seen: u64) {
        for listener in self.listener_snapshot() {
            listener.on_index_phase(phase, files_seen);
        }
    }

    /// The live retention window in days (runtime-mutable; O-5's
    /// settings surface writes it and the next compaction reads it).
    pub fn retention_days(&self) -> u32 {
        self.retention_days
            .load(std::sync::atomic::Ordering::SeqCst)
    }

    /// Set the retention window (days). Applies to the compaction
    /// worker and the next scan's remnant reclamation immediately.
    pub fn set_retention_days(&self, days: u32) {
        self.retention_days
            .store(days, std::sync::atomic::Ordering::SeqCst);
    }

    /// The live history prefs (O-5 #543) — read from the same atomic
    /// the compaction worker consumes, so it always reflects what the
    /// session is actually enforcing.
    pub fn history_prefs(&self) -> crate::history_prefs::HistoryPrefs {
        crate::history_prefs::HistoryPrefs {
            retention_days: self.retention_days(),
        }
    }

    /// Persist AND apply history prefs (O-5 #543). Writes the
    /// `history` section of `.slate/prefs.json` FIRST — every other
    /// top-level key preserved, atomic replace, and an unparseable
    /// existing file fails the call untouched — then applies the
    /// retention window live to the compaction worker. Order matters:
    /// a persist failure leaves the running session on its previous
    /// setting, never a live-but-unsaved divergence that would
    /// silently revert at next open.
    pub fn set_history_prefs(
        &self,
        prefs: crate::history_prefs::HistoryPrefs,
    ) -> Result<(), VaultError> {
        if prefs.retention_days == 0 {
            return Err(VaultError::InvalidArgument {
                message: "history retention_days must be at least 1".into(),
            });
        }
        crate::history_prefs::write_history_prefs(
            &self.config.cache_dir.join("prefs.json"),
            &prefs,
        )?;
        self.set_retention_days(prefs.retention_days);
        Ok(())
    }

    /// Queue a background compaction for one log (single-flight: a log
    /// already queued is not queued again). Best-effort — a send onto
    /// a shut-down worker is silently dropped.
    fn enqueue_compaction(&self, file_id: i64, log_name: &str, path: &str) {
        {
            let mut queued = self
                .compaction_queued
                .lock()
                .expect("compaction queue mutex");
            if !queued.insert(log_name.to_string()) {
                // Queued or in flight: mark dirty so the worker runs
                // exactly one follow-up after the current run.
                self.compaction_dirty
                    .lock()
                    .expect("compaction dirty mutex")
                    .insert(log_name.to_string());
                return;
            }
        }
        let _ = self.compaction_tx.send(CompactionJob::Compact {
            file_id,
            log_name: log_name.to_string(),
            path: path.to_string(),
        });
    }

    /// The save path's index refresh: upsert the `files` row and
    /// rebuild every derived index for one file, inside the caller's
    /// transaction (extracted so `create_exclusive` shares the exact
    /// machinery — O-3 #541). Returns the file id.
    fn index_saved_file(
        &self,
        tx: &rusqlite::Transaction<'_>,
        path: &str,
        contents: &str,
        new_stat: &crate::FileStat,
        new_hash: &str,
        now: i64,
    ) -> Result<i64, VaultError> {
        let (name, extension, is_markdown) = classify_path(path);
        let is_base = extension.as_deref() == Some("base");

        // Body text for FTS5: only markdown gets indexed; everything
        // else stores "" so the trigger on `body_text` makes the
        // file_fts row consistent (or absent, via is_markdown gating
        // in the migration-006 triggers).
        let body_text: &str = if is_markdown { contents } else { "" };

        tx.execute(
            "INSERT INTO files
                (path, name, extension, size_bytes, mtime_ms, ctime_ms, birthtime_ms,
                 content_hash, parser_version, indexed_at_ms, is_markdown, body_text)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?12, ?7, ?8, ?9, ?10, ?11)
             ON CONFLICT(path) DO UPDATE SET
                name           = excluded.name,
                extension      = excluded.extension,
                size_bytes     = excluded.size_bytes,
                mtime_ms       = excluded.mtime_ms,
                ctime_ms       = excluded.ctime_ms,
                birthtime_ms   = CASE WHEN excluded.birthtime_ms != 0
                                      THEN excluded.birthtime_ms
                                      ELSE files.birthtime_ms END,
                content_hash   = excluded.content_hash,
                parser_version = excluded.parser_version,
                indexed_at_ms  = excluded.indexed_at_ms,
                is_markdown    = excluded.is_markdown,
                body_text      = excluded.body_text",
            rusqlite::params![
                path,
                name,
                extension,
                new_stat.size_bytes as i64,
                new_stat.mtime_ms,
                new_stat.ctime_ms,
                new_hash,
                self.config.parser_version,
                now,
                is_markdown as i64,
                body_text,
                new_stat.birthtime_ms,
            ],
        )?;
        let file_id: i64 = tx.query_row(
            "SELECT id FROM files WHERE path = ?1",
            rusqlite::params![path],
            |row| row.get(0),
        )?;
        if is_markdown {
            // Build a fresh path index from SQLite so link
            // resolution sees every currently-known file (including
            // any rows the upsert above just added). This is one
            // O(N) query per save — N is the indexed-file count,
            // which is bounded by the vault size. Acceptable for
            // V1.F; can become an incrementally-maintained snapshot
            // if save_text ever shows up in profiles.
            let vault_index = crate::InMemoryVaultIndex::new(
                tx.prepare("SELECT path FROM files")?
                    .query_map([], |row| row.get::<_, String>(0))?
                    .collect::<Result<Vec<_>, _>>()?,
            );
            replace_headings(tx, file_id, contents)?;
            crate::links_db::replace_links_for_file(tx, file_id, path, contents, &vault_index)?;
            crate::properties_db::replace_properties_for_file(tx, file_id, contents)?;
            crate::dql_inline_fields_db::replace_dql_inline_fields_for_file(tx, file_id, contents)?;
            crate::tags_db::replace_tags_for_file(tx, file_id, contents)?;
            crate::tasks_db::replace_tasks_for_file(tx, file_id, contents)?;
            crate::blocks_db::replace_blocks_for_file(tx, file_id, contents)?;
            crate::citations_db::replace_citations_for_file(tx, file_id, contents)?;
            crate::bases_db::replace_base_blocks_for_file(tx, file_id, contents)?;
        } else if is_base {
            crate::bases_db::replace_base_file_for_file(
                tx,
                file_id,
                &name,
                contents,
                self.config.parser_version,
                now,
            )?;
        } else if classify_path(path).1.as_deref() == Some("canvas") {
            // Keep the canvas index coherent even when a `.canvas`
            // file is written through the generic text-save path
            // (the canvas-native save lands with #366/#372).
            crate::canvas_db::replace_canvas_for_file(
                tx,
                file_id,
                contents,
                &DbTitleSource { conn: tx },
            )?;
        }
        Ok(file_id)
    }

    /// Append this save to the file's op log, choosing between a
    /// `WholeFileReplace` **snapshot** and a fine-grained `EditBatch`
    /// using the in-memory [`OplogAppendState`] (#378) — no log read.
    ///
    /// Snapshot when: cold cache (first save of this file this session),
    /// or the cache's `last_hash_after` doesn't match `hash_before` (an
    /// external edit slipped in — re-anchor), or `old_contents` isn't
    /// available (None path / non-UTF-8), or the diff would be as large
    /// as the file (near-total rewrite / a giant single line — keeps the
    /// entry well under `MAX_PLAUSIBLE_BODY_LEN`), or the bytes since the
    /// last snapshot would exceed the cadence threshold. An identical
    /// save (empty diff) writes nothing — **even when it carries
    /// annotations** (a no-op semantic edit changed no bytes; there is
    /// no version to describe). Otherwise: one `EditBatch`.
    ///
    /// Non-empty `annotations` wrap whatever inner kind the decision
    /// logic picked in **one** kind-4 `Annotated` entry (O-1 #539) — a
    /// cadence-forced snapshot that also set a property is one entry
    /// wrapping a snapshot payload; intent preserved, atomicity
    /// preserved, never an entry pair.
    ///
    /// Best-effort throughout: any error logs a warning and returns,
    /// leaving the (already durable) file save untouched.
    #[allow(clippy::too_many_arguments)]
    fn append_save_to_oplog(
        &self,
        conn: &Connection,
        file_id: i64,
        log_name: &str,
        path: &str,
        hash_before: &str,
        new_hash: &str,
        new_contents: &str,
        old_contents: Option<&str>,
        now: i64,
        annotations: &[crate::oplog::OpAnnotation],
    ) {
        let mut state = self.oplog_state.lock().expect("oplog state mutex");
        let cached = state.get(&file_id).cloned();

        // The save's edit ops when this function computes them — kept
        // for O-6 event derivation even when the kind decision lands
        // on a snapshot. The spec pins `deleted_text` NULL to "no old
        // content in hand" ONLY: the cadence-snapshot arm samples
        // because the diff informed its decision, and the cold/
        // misaligned snapshot arm samples too whenever the conflict
        // check put the old bytes in hand (adversarial round 3 — the
        // first save after an app restart is the COMMON divergence,
        // and leaving it unsampled made deletions across restarts
        // unsearchable, a wider gap than the documented one).
        let mut ops_in_hand: Option<Vec<crate::oplog::EditOp>> = None;

        // (inner_kind, inner_payload, resets_snapshot_cadence).
        // `None` ⇒ write nothing.
        let decision: Option<(crate::oplog::OpKind, Vec<u8>, bool)> = match (old_contents, &cached)
        {
            (Some(old), Some(c)) if c.last_hash_after == hash_before => {
                let ops = crate::diff::diff_to_ops(old, new_contents);
                if ops.is_empty() {
                    None // identical content — don't grow the log
                } else {
                    let payload = crate::oplog::encode_edit_batch(&ops);
                    ops_in_hand = Some(ops);
                    // Count the per-entry framing overhead (body header +
                    // two hex hashes + actor id + length fields + checksum)
                    // alongside the payload so the cadence tracks on-disk
                    // growth, not just payload bytes (red-team).
                    let projected = c.bytes_since_snapshot
                        + payload.len() as u64
                        + OPLOG_ENTRY_FRAMING_OVERHEAD_ESTIMATE;
                    // Snapshot if the batch isn't smaller than just storing
                    // the file, or the cadence cap is hit. The size check
                    // also caps a batch's payload below the file size, so a
                    // pathological giant-line edit can't approach
                    // MAX_PLAUSIBLE_BODY_LEN.
                    if payload.len() as u64 >= new_contents.len() as u64
                        || projected > self.config.oplog_compaction_threshold_bytes as u64
                    {
                        (
                            crate::oplog::OpKind::WholeFileReplace,
                            new_contents.as_bytes().to_vec(),
                            true,
                        )
                            .into()
                    } else {
                        Some((crate::oplog::OpKind::EditBatch, payload, false))
                    }
                }
            }
            // Cold cache, misaligned, None path, or non-UTF-8 old → snapshot.
            _ => {
                // Old bytes in hand (conflict-checked save on a cold or
                // misaligned session cache): compute the diff for event
                // sampling only — the ENTRY is still a snapshot, so the
                // log format and replay are untouched. An identical
                // save still writes the re-anchoring snapshot here (the
                // pre-O-6 behavior); its empty ops sample as "".
                if let Some(old) = old_contents {
                    ops_in_hand = Some(crate::diff::diff_to_ops(old, new_contents));
                }
                Some((
                    crate::oplog::OpKind::WholeFileReplace,
                    new_contents.as_bytes().to_vec(),
                    true,
                ))
            }
        };

        let Some((inner_kind, inner_payload, resets_cadence)) = decision else {
            return; // nothing to write (identical save)
        };

        // Wrap intent (O-1 #539): one atomic entry either way.
        let (op_kind, payload_bytes) = if annotations.is_empty() {
            (inner_kind, inner_payload)
        } else {
            (
                crate::oplog::OpKind::Annotated,
                crate::oplog::encode_annotated(inner_kind, &inner_payload, annotations),
            )
        };
        let bytes_since_snapshot = if resets_cadence {
            0
        } else {
            cached.as_ref().map_or(0, |c| c.bytes_since_snapshot)
                + payload_bytes.len() as u64
                + OPLOG_ENTRY_FRAMING_OVERHEAD_ESTIMATE
        };
        let entry = crate::oplog::OpLogEntry {
            timestamp_ms: now,
            user_actor_id: self.config.user_actor_id.clone(),
            op_kind,
            content_hash_before: hash_before.to_string(),
            content_hash_after: new_hash.to_string(),
            payload_bytes,
        };

        // O-6 (#544): durable staleness marker BEFORE the filesystem
        // append (adversarial round 2 — mark-before-append). The log
        // entry becomes durable at `append_entry`; a crash before the
        // event rows commit would otherwise leave the index silently
        // missing them with nothing to ever trigger repair (the
        // error-path marker only covers REPORTED failures). Marker
        // first ⇒ a crash anywhere in the window leaves the marker
        // behind and the next scan rebuilds from the log. Cleared —
        // this row only, earlier failures' markers must survive — in
        // the same transaction as the event rows below. If the marker
        // itself can't be written, proceed anyway: the log is the
        // source of truth and history beats index freshness.
        //
        // The marker commit is forced to disk (round 3): under
        // WAL+synchronous=NORMAL an autocommit isn't fsynced at
        // commit, while `append_entry` sync_data's the log — a power
        // cut (not just a process kill) could otherwise keep the
        // entry and lose the marker. FULL for exactly this commit
        // fsyncs its WAL frame, restoring "marker durable before
        // entry durable". Per-connection pragmas; the caller holds
        // the connection mutex, so nothing else sees the toggle.
        let stale_marker_rowid: Option<i64> = {
            // Checked transitions (round 4): a silent enter-FULL
            // failure would commit the marker unfsynced while this
            // comment claims power-cut ordering; a silent restore
            // failure would leave the connection fsync-heavy forever.
            // Enter failure degrades the guarantee to process-crash-
            // only — logged, marker still written. Restore failure
            // retries once, then warns: durability is unaffected,
            // later writes just pay FULL until restart.
            if let Err(e) = conn.pragma_update(None, "synchronous", "FULL") {
                log::warn!(
                    "oplog_events marker fsync mode unavailable; power-cut window not covered"
                );
                log::debug!("synchronous=FULL pragma failure detail: {e}");
            }
            let inserted = conn.execute("INSERT INTO oplog_events_stale (marker) VALUES (1)", []);
            if conn.pragma_update(None, "synchronous", "NORMAL").is_err()
                && let Err(e) = conn.pragma_update(None, "synchronous", "NORMAL")
            {
                log::warn!("connection stuck in synchronous=FULL; writes stay durable but slower");
                log::debug!("synchronous=NORMAL restore failure detail: {e}");
            }
            match inserted {
                Ok(_) => Some(conn.last_insert_rowid()),
                Err(e) => {
                    log::warn!("oplog_events staleness marker write failed");
                    log::debug!("oplog_events staleness marker failure detail: {e}");
                    None
                }
            }
        };

        let post_append_len =
            match crate::oplog::append_entry(&self.config.cache_dir, log_name, path, &entry) {
                Ok(len) => len,
                Err(e) => {
                    // Non-fatal: a missing op-log entry only degrades undo to a
                    // per-file conflict report, never corruption. Route through the
                    // facade (#507). warn carries only the file id and the error
                    // *kind* — never the full error Display, which for a torn/short
                    // existing log embeds the cache path (`oplog {path:?}: …`). The
                    // path-bearing detail rides the debug line instead, so it stays
                    // out of shipped host logs (see lib.rs privacy rule).
                    log::warn!("oplog append failed for file_id={file_id}: {}", e.kind());
                    log::debug!("oplog append failure for path {path:?}: {e}");
                    // The append never happened: log and index agree, so
                    // this save's marker is moot. Best-effort removal — a
                    // leftover only costs one harmless rebuild.
                    if let Some(rowid) = stale_marker_rowid {
                        let _ = conn.execute(
                            "DELETE FROM oplog_events_stale WHERE rowid = ?1",
                            rusqlite::params![rowid],
                        );
                    }
                    return; // leave the cache untouched so the next save re-snapshots
                }
            };
        let appends_this_session = cached.as_ref().map_or(1, |c| c.appends_this_session + 1);
        state.insert(
            file_id,
            OplogAppendState {
                last_hash_after: new_hash.to_string(),
                bytes_since_snapshot,
                compaction_futile: false, // an append clears futility
                appends_this_session,
            },
        );
        drop(state);

        // O-6 (#544): populate the derived `oplog_events` index from the
        // entry just appended. One transaction per entry — an entry's
        // rows land all-or-nothing, never a partial set (adversarial
        // review) — clearing this save's pre-append staleness marker
        // with them: commit ⇒ rows in AND marker gone; any failure or
        // crash ⇒ rows out AND marker present ⇒ the next scan
        // regenerates the table from the logs. The entry is in the
        // log either way, so nothing is lost, only deferred.
        let events =
            crate::oplog_events::derive_events(&entry, ops_in_hand.as_deref(), old_contents);
        let inserted = conn.unchecked_transaction().and_then(|tx| {
            insert_oplog_events(
                &tx,
                file_id,
                &events,
                retention_cutoff_ms(self.retention_days()),
            )?;
            if let Some(rowid) = stale_marker_rowid {
                tx.execute(
                    "DELETE FROM oplog_events_stale WHERE rowid = ?1",
                    rusqlite::params![rowid],
                )?;
            }
            tx.commit()
        });
        if let Err(e) = inserted {
            // Rows rolled back; the marker stays behind and the next
            // scan rebuilds.
            log::warn!("oplog_events insert failed for file_id={file_id}");
            log::debug!("oplog_events insert failure detail: {e}");
            // Re-arm the marker unconditionally (round 3). Two ways
            // the pre-append marker can be missing here: it never
            // landed (the marker write and this transaction share ONE
            // cause — a long-lived writer holding the cross-process
            // lock past the busy timeout — so "two independent
            // failures" was false comfort), or a concurrent process's
            // scan rebuild deleted it after reading the log but before
            // this entry's append landed. A redundant marker when it
            // DID survive is harmless — the rebuild clears the whole
            // table. Failing this too degrades to a stale index until
            // the next full rebuild.
            if let Err(e) = conn.execute("INSERT INTO oplog_events_stale (marker) VALUES (1)", []) {
                log::warn!("oplog_events staleness marker retry failed");
                log::debug!("oplog_events staleness marker retry failure detail: {e}");
            }
        }

        // O-2 (#540): the compaction trigger check is pure arithmetic —
        // the returned post-append length against the byte threshold,
        // plus this session's append count against the entry threshold
        // (a sound lower bound on the log's entries). No log walk ever
        // runs on the save path; compaction itself makes the count
        // exact when it reads the log.
        if post_append_len > self.config.oplog_compaction_threshold_bytes as u64
            || appends_this_session > self.config.oplog_compaction_threshold_entries as u64
        {
            self.enqueue_compaction(file_id, log_name, path);
        }
    }

    /// Read every well-formed op-log entry recorded for `path`.
    ///
    /// Returns `Ok(Vec::new())` if the path isn't indexed yet or has
    /// never been saved through `save_text`. A torn trailing entry
    /// (e.g. crash mid-append) is silently dropped; the returned
    /// vector is the well-formed prefix.
    pub fn read_oplog(&self, path: &str) -> Result<Vec<crate::oplog::OpLogEntry>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        let log_name: Option<Option<String>> = conn
            .query_row(
                "SELECT oplog_name FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get(0),
            )
            .optional()?;
        drop(conn);
        let Some(Some(log_name)) = log_name else {
            // Unindexed path, or an indexed file that has never been
            // saved through Slate (no binding, no log).
            return Ok(Vec::new());
        };
        crate::oplog::read_oplog(&self.config.cache_dir, &log_name).map_err(VaultError::Io)
    }

    /// Page through `path`'s version history, newest first (O-3 #541).
    ///
    /// Markers are included — UI filters them, tests/CLI want the full
    /// ledger. The opaque cursor embeds the log's header GENERATION: a
    /// compaction between pages bumps it (O-2) and the next page
    /// request fails with a typed `InvalidArgument("history changed,
    /// restart paging")` — the UI reloads page one. (The tail hash
    /// alone cannot detect compaction: the fold preserves the tail by
    /// construction.)
    pub fn list_versions(
        &self,
        path: &str,
        paging: Paging,
    ) -> Result<Page<VersionSummary>, VaultError> {
        let log_name = self.bound_log_name(path)?;
        let Some(log_name) = log_name else {
            return Ok(Page {
                items: Vec::new(),
                next_cursor: None,
                total_filtered: 0,
            });
        };
        let (header, entries) =
            crate::oplog::read_oplog_with_header(&self.config.cache_dir, &log_name)
                .map_err(VaultError::Io)?;

        let start_position: usize = match paging.cursor.as_deref() {
            None => 0,
            Some(cursor) => {
                let parsed = cursor.strip_prefix("v1:").and_then(|rest| {
                    let (generation, position) = rest.split_once(':')?;
                    Some((
                        generation.parse::<u32>().ok()?,
                        position.parse::<usize>().ok()?,
                    ))
                });
                let Some((cursor_generation, position)) = parsed else {
                    return Err(VaultError::InvalidArgument {
                        message: "malformed version-history cursor".into(),
                    });
                };
                if cursor_generation != header.generation {
                    return Err(VaultError::InvalidArgument {
                        message: "history changed, restart paging".into(),
                    });
                }
                position
            }
        };

        let summaries = version_summaries(&entries);
        let total = summaries.len() as u64;
        let limit = (paging.limit as usize).max(1);
        let page: Vec<VersionSummary> = summaries
            .into_iter()
            .skip(start_position)
            .take(limit)
            .collect();
        let next_position = start_position + page.len();
        let next_cursor = (next_position < total as usize)
            .then(|| format!("v1:{}:{next_position}", header.generation));
        Ok(Page {
            items: page,
            next_cursor,
            total_filtered: total,
        })
    }

    /// The exact bytes of the version whose `content_hash_after` is
    /// `version_hash` — **integrity-verified** (O-3 #541): the
    /// reconstruction is re-hashed and refused with
    /// [`VaultError::HistoryUnavailable`] on mismatch. Wrong bytes are
    /// never served. Same-hash duplicates (A→B→A) are safe precisely
    /// because of this check: any occurrence that passes reconstructs
    /// the same bytes.
    pub fn version_content(&self, path: &str, version_hash: &str) -> Result<String, VaultError> {
        let log_name = self
            .bound_log_name(path)?
            .ok_or_else(|| VaultError::InvalidArgument {
                message: format!("no such version: {path:?} has no history"),
            })?;
        let entries =
            crate::oplog::read_oplog(&self.config.cache_dir, &log_name).map_err(VaultError::Io)?;
        if !entries.iter().any(|e| e.content_hash_after == version_hash) {
            return Err(VaultError::InvalidArgument {
                message: "no such version".into(),
            });
        }
        let content =
            crate::oplog::reconstruct_at_hash(&entries, version_hash).ok_or_else(|| {
                VaultError::HistoryUnavailable {
                    path: path.to_string(),
                    reason: format!("version {version_hash} failed to reconstruct"),
                }
            })?;
        if crate::vault::content_hash(content.as_bytes()) != version_hash {
            return Err(VaultError::HistoryUnavailable {
                path: path.to_string(),
                reason: format!("version {version_hash} failed integrity verification"),
            });
        }
        Ok(content)
    }

    /// Restore `path` to the (verified) version `version_hash` through
    /// the **standard save machinery** — atomic write, `WriteConflict`
    /// on `expected_content_hash` mismatch, index refresh, its own
    /// op-log entry. History is never rewritten: an undo of a restore
    /// is just another restore (plan decision #5).
    pub fn restore_version(
        &self,
        path: &str,
        version_hash: &str,
        expected_content_hash: Option<&str>,
    ) -> Result<SaveReport, VaultError> {
        let content = self.version_content(path, version_hash)?;
        self.save_text(path, &content, expected_content_hash)
    }

    /// Structured diff between two (verified) versions of `path`
    /// (O-4 #542). Both hashes resolve through
    /// [`version_content`](Self::version_content), so wrong bytes are
    /// never diffed.
    pub fn diff_versions(
        &self,
        path: &str,
        from_hash: &str,
        to_hash: &str,
    ) -> Result<crate::structured_diff::StructuredDiff, VaultError> {
        let from = self.version_content(path, from_hash)?;
        let to = self.version_content(path, to_hash)?;
        Ok(crate::structured_diff::structured_diff(
            path, from_hash, to_hash, &from, &to,
        ))
    }

    /// What changed since the last recorded open (O-4 #542).
    ///
    /// **Ordering contract (pinned)**: hosts compute THIS first, then
    /// call [`mark_opened`](Self::mark_opened) — marking first would
    /// always report `Unchanged`.
    pub fn changes_since_last_open(&self, path: &str) -> Result<ChangesSinceOpen, VaultError> {
        // Snapshot consistency (adversarial review): every verdict is a
        // pure function of ONE atomic observation — the (current_hash,
        // mark) pair read under a single connection-mutex hold. In
        // particular `Unchanged` involves no further reads, so it is
        // exactly the truth at its observation instant: returning it
        // is a valid linearization even if a save lands right after
        // (any API racing a live writer has that property). The
        // original defect was different — a verdict assembled from an
        // index read at t1 and log reads at t2, coherent with neither.
        // The bounded revalidation loop below adds FRESHNESS on top of
        // that correctness: it retries when the pair moved during the
        // (Diff-path) log work, and on persistent churn returns the
        // verdict of the newest atomic observation.
        let read_state = |session: &Self| -> Result<Option<(String, Option<String>)>, VaultError> {
            let conn = session.conn.lock().expect("session connection mutex");
            let row: Option<(i64, String)> = conn
                .query_row(
                    "SELECT id, content_hash FROM files WHERE path = ?1",
                    rusqlite::params![path],
                    |row| Ok((row.get(0)?, row.get(1)?)),
                )
                .optional()?;
            let Some((file_id, current_hash)) = row else {
                return Ok(None);
            };
            let mark_hash: Option<String> = conn
                .query_row(
                    "SELECT content_hash_at_open FROM open_marks WHERE file_id = ?1",
                    rusqlite::params![file_id],
                    |row| row.get(0),
                )
                .optional()?;
            Ok(Some((current_hash, mark_hash)))
        };

        let mut observed = read_state(self)?.ok_or_else(|| VaultError::InvalidPath {
            path: path.to_string(),
            reason: "no such file in the index".into(),
        })?;
        for _ in 0..3 {
            let (current_hash, mark_hash) = observed.clone();
            let verdict = self.changes_verdict(path, &current_hash, mark_hash.as_deref())?;
            let reread = read_state(self)?.ok_or_else(|| VaultError::InvalidPath {
                path: path.to_string(),
                reason: "file disappeared from the index".into(),
            })?;
            if reread == observed {
                return Ok(verdict);
            }
            observed = reread;
        }
        // Third strike under churn: compute once more from the final
        // observation and return it.
        let (current_hash, mark_hash) = observed;
        self.changes_verdict(path, &current_hash, mark_hash.as_deref())
    }

    /// One verdict computation over an observed `(current_hash, mark)`
    /// pair — a pure function of that single atomic observation
    /// (adversarial review round 3): the Diff target is the OBSERVED
    /// `current_hash`, never a separately read log tail, so no verdict
    /// can mix index state from one instant with log state from
    /// another. Both sides resolve through
    /// [`version_content`](Self::version_content) (membership +
    /// integrity verification); either side missing from the log —
    /// compacted past the mark, a rebound log, or a save whose
    /// best-effort append failed — degrades to `BaselineCompacted`
    /// ("changes happened but this span can't be described").
    fn changes_verdict(
        &self,
        path: &str,
        current_hash: &str,
        mark_hash: Option<&str>,
    ) -> Result<ChangesSinceOpen, VaultError> {
        let Some(mark_hash) = mark_hash else {
            return Ok(ChangesSinceOpen::NoBaseline);
        };
        if mark_hash == current_hash {
            return Ok(ChangesSinceOpen::Unchanged);
        }
        let from = match self.version_content(path, mark_hash) {
            Ok(content) => content,
            Err(VaultError::InvalidArgument { .. })
            | Err(VaultError::HistoryUnavailable { .. }) => {
                return Ok(ChangesSinceOpen::BaselineCompacted);
            }
            Err(other) => return Err(other),
        };
        let to = match self.version_content(path, current_hash) {
            Ok(content) => content,
            Err(VaultError::InvalidArgument { .. })
            | Err(VaultError::HistoryUnavailable { .. }) => {
                return Ok(ChangesSinceOpen::BaselineCompacted);
            }
            Err(other) => return Err(other),
        };
        Ok(ChangesSinceOpen::Diff(
            crate::structured_diff::structured_diff(path, mark_hash, current_hash, &from, &to),
        ))
    }

    /// Record "opened now, at this content" (O-4 #542) — upsert into
    /// `open_marks`. Call AFTER
    /// [`changes_since_last_open`](Self::changes_since_last_open) (the
    /// pinned funnel order).
    pub fn mark_opened(&self, path: &str) -> Result<(), VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        let row: Option<(i64, String)> = conn
            .query_row(
                "SELECT id, content_hash FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| Ok((row.get(0)?, row.get(1)?)),
            )
            .optional()?;
        let Some((file_id, current_hash)) = row else {
            return Err(VaultError::InvalidPath {
                path: path.to_string(),
                reason: "no such file in the index".into(),
            });
        };
        conn.execute(
            "INSERT INTO open_marks (file_id, last_opened_ms, content_hash_at_open)
             VALUES (?1, ?2, ?3)
             ON CONFLICT(file_id) DO UPDATE SET
                last_opened_ms = excluded.last_opened_ms,
                content_hash_at_open = excluded.content_hash_at_open",
            rusqlite::params![file_id, now_ms(), current_hash],
        )?;
        Ok(())
    }

    /// The recoverable deleted files (O-3 #541): the scan reconcile's
    /// remnant set joined with structural-journal `DeleteFile` rows for
    /// timestamps. One row per path — delete → recreate → delete keeps
    /// the NEWEST remnant (older ones age out via O-2's reclamation).
    /// Ordered by `deleted_at_ms` desc, unknown timestamps last.
    /// Out-of-retention remnants don't appear because the sweep has
    /// already deleted their logs — no retention filter here.
    ///
    /// Honesty rule: files deleted having never been saved through
    /// Slate have no log and are simply absent (the UI carries the
    /// system-Trash footnote).
    pub fn list_deleted_files(&self, paging: Paging) -> Result<Page<DeletedFileEntry>, VaultError> {
        let (generation, remnants) = self.remnant_snapshot();
        // Newest remnant per path.
        let mut newest: std::collections::HashMap<String, RemnantLog> =
            std::collections::HashMap::new();
        for remnant in remnants {
            match newest.get(&remnant.effective_path) {
                Some(existing) if existing.tail_timestamp_ms >= remnant.tail_timestamp_ms => {}
                _ => {
                    newest.insert(remnant.effective_path.clone(), remnant);
                }
            }
        }

        // Journal join: stem → newest DeleteFile timestamp.
        let mut deleted_at_by_stem: std::collections::HashMap<String, i64> =
            std::collections::HashMap::new();
        {
            let conn = self.conn.lock().expect("session connection mutex");
            let mut stmt = conn.prepare(
                "SELECT timestamp_ms, payload FROM structural_ops
                 WHERE kind = 'delete_file' ORDER BY id ASC",
            )?;
            let rows = stmt.query_map([], |row| {
                Ok((row.get::<_, i64>(0)?, row.get::<_, String>(1)?))
            })?;
            for row in rows {
                let (ts, payload) = row?;
                if let Some(parsed) = crate::structural::StructuralOpPayload::from_json(&payload)
                    && let Some(stem) = parsed.oplog_name
                {
                    deleted_at_by_stem.insert(stem, ts);
                }
            }
        }

        let mut entries: Vec<DeletedFileEntry> = newest
            .into_values()
            .map(|remnant| {
                // Recoverability = the tail reconstructs AND verifies.
                let reconstructed = crate::oplog::read_oplog(&self.config.cache_dir, &remnant.stem)
                    .ok()
                    .and_then(|entries| crate::oplog::reconstruct_at_tail(&entries).ok())
                    .filter(|content| {
                        crate::vault::content_hash(content.as_bytes()) == remnant.tail_hash
                    });
                DeletedFileEntry {
                    path: remnant.effective_path,
                    deleted_at_ms: deleted_at_by_stem.get(&remnant.stem).copied(),
                    recoverable: reconstructed.is_some(),
                    size_bytes: reconstructed.map(|c| c.len() as u64),
                }
            })
            .collect();
        entries.sort_by(|a, b| match (b.deleted_at_ms, a.deleted_at_ms) {
            (Some(x), Some(y)) => x.cmp(&y).then_with(|| a.path.cmp(&b.path)),
            (Some(_), None) => std::cmp::Ordering::Greater,
            (None, Some(_)) => std::cmp::Ordering::Less,
            (None, None) => a.path.cmp(&b.path),
        });

        let total = entries.len() as u64;
        let start: usize = match paging.cursor.as_deref() {
            None => 0,
            Some(cursor) => {
                let parsed = cursor.strip_prefix("v1:").and_then(|rest| {
                    let (cursor_generation, offset) = rest.split_once(':')?;
                    Some((
                        cursor_generation.parse::<u64>().ok()?,
                        offset.parse::<usize>().ok()?,
                    ))
                });
                let Some((cursor_generation, offset)) = parsed else {
                    return Err(VaultError::InvalidArgument {
                        message: "malformed deleted-files cursor".into(),
                    });
                };
                if cursor_generation != generation {
                    return Err(VaultError::InvalidArgument {
                        message: "deleted files changed, restart paging".into(),
                    });
                }
                offset
            }
        };
        let limit = (paging.limit as usize).max(1);
        let page: Vec<DeletedFileEntry> = entries.into_iter().skip(start).take(limit).collect();
        let next = start + page.len();
        let next_cursor = (next < total as usize).then(|| format!("v1:{generation}:{next}"));
        Ok(Page {
            items: page,
            next_cursor,
            total_filtered: total,
        })
    }

    /// Recover a deleted file: reconstruct the newest matching
    /// remnant's tail (integrity-verified), create the file
    /// **exclusively** (never clobbering an occupant), and re-bind the
    /// remnant log so the recovered file KEEPS its pre-deletion
    /// history — the recovery save appends onto it (O-3 #541).
    pub fn recover_deleted_file(&self, path: &str) -> Result<SaveReport, VaultError> {
        let remnant = self
            .remnant_logs()
            .into_iter()
            .filter(|r| r.effective_path == path)
            .max_by_key(|r| r.tail_timestamp_ms)
            .ok_or_else(|| VaultError::InvalidArgument {
                message: format!("no deleted-file history for {path:?}"),
            })?;
        self.recover_remnant_to(remnant, path)
    }

    /// `recover_deleted_file` to a CALLER-CHOSEN destination (#795
    /// "Restore As…"): the occupied-destination case's escape hatch.
    /// The remnant is looked up by its original deleted path; the
    /// content lands at `destination` via the same no-clobber
    /// machinery, and the remnant log re-binds to the new path (its
    /// history — including the pre-delete versions — follows the
    /// file, immediately queryable through the O-5 repopulation).
    pub fn recover_deleted_file_as(
        &self,
        path: &str,
        destination: &str,
    ) -> Result<SaveReport, VaultError> {
        let remnant = self
            .remnant_logs()
            .into_iter()
            .filter(|r| r.effective_path == path)
            .max_by_key(|r| r.tail_timestamp_ms)
            .ok_or_else(|| VaultError::InvalidArgument {
                message: format!("no deleted-file history for {path:?}"),
            })?;
        self.recover_remnant_to(remnant, destination)
    }

    /// Shared tail of the two recovery entry points: reconstruct +
    /// verify the remnant's content, publish it exclusively at
    /// `destination` with the log re-bound, and drop the remnant from
    /// the Deleted list.
    fn recover_remnant_to(
        &self,
        remnant: RemnantLog,
        destination: &str,
    ) -> Result<SaveReport, VaultError> {
        let entries = crate::oplog::read_oplog(&self.config.cache_dir, &remnant.stem)
            .map_err(VaultError::Io)?;
        let content = crate::oplog::reconstruct_at_tail(&entries).map_err(|e| {
            VaultError::HistoryUnavailable {
                path: destination.to_string(),
                reason: format!("deleted-file history failed to reconstruct: {e}"),
            }
        })?;
        if crate::vault::content_hash(content.as_bytes()) != remnant.tail_hash {
            return Err(VaultError::HistoryUnavailable {
                path: destination.to_string(),
                reason: "deleted-file history failed integrity verification".into(),
            });
        }
        let report = self.create_exclusive_binding(destination, &content, Some(&remnant.stem))?;
        // The remnant is a remnant no more (one lock hold: retain +
        // generation bump are atomic together).
        {
            let mut guard = self.remnant_logs.lock().expect("remnant logs mutex");
            guard.0 += 1;
            guard.1.retain(|r| r.stem != remnant.stem);
        }
        self.notify_file_change(FileChangeKind::Created, destination, None);
        Ok(report)
    }

    /// Create-if-absent write (O-3 #541): `save_text` silently
    /// overwrites, so it cannot be used for recovery. Path exists on
    /// disk or in the index (case-insensitively, the structural-move
    /// convention) → [`VaultError::DestinationExists`]; else the
    /// standard atomic-write + index + op-log machinery.
    pub fn create_exclusive(&self, path: &str, content: &str) -> Result<SaveReport, VaultError> {
        let report = self.create_exclusive_binding(path, content, None)?;
        self.notify_file_change(FileChangeKind::Created, path, None);
        Ok(report)
    }

    /// `create_exclusive` with an optional pre-existing op-log binding
    /// (deleted-file recovery re-attaches the remnant's stem so
    /// history continues across the delete/recover pair).
    ///
    /// One IMMEDIATE transaction end to end (adversarial review): the
    /// cross-process SQLite write lock is held BEFORE the existence
    /// checks, so a racing Slate/CLI writer serializes behind it; the
    /// disk publish itself uses the provider's no-replace primitive
    /// (`write_file_if_absent` — hard-link on the filesystem
    /// provider), so even a non-Slate writer in the residual window
    /// cannot be clobbered. Any failure before commit rolls the whole
    /// row (including a recovery binding) back — no phantom index
    /// entries blocking retries. A post-write index failure leaves the
    /// created file on disk (never delete user bytes on an index
    /// error); the next scan indexes it.
    fn create_exclusive_binding(
        &self,
        path: &str,
        content: &str,
        bind_log: Option<&str>,
    ) -> Result<SaveReport, VaultError> {
        validate_save_path(path)?;
        if content.len() as u64 > self.config.large_file_refuse_bytes {
            return Err(VaultError::FileTooLarge {
                path: path.to_string(),
                size: content.len() as u64,
            });
        }
        let mut conn = self.conn.lock().expect("session connection mutex");
        let tx = conn.transaction_with_behavior(rusqlite::TransactionBehavior::Immediate)?;

        // Existence gates INSIDE the cross-process critical section:
        // the index (case-insensitive — the APFS-aware structural
        // convention) and the disk.
        if let Some(existing) = index_entry_case_insensitive(&tx, path)? {
            return Err(VaultError::DestinationExists { path: existing });
        }
        if self.provider.stat(path).is_ok() {
            return Err(VaultError::DestinationExists {
                path: path.to_string(),
            });
        }

        // No-replace publish: the point of no return for user bytes.
        self.provider
            .write_file_if_absent(path, content.as_bytes())?;

        let new_stat = self.provider.stat(path)?;
        let new_hash = crate::vault::content_hash(content.as_bytes());
        let now = now_ms();
        let file_id = self.index_saved_file(&tx, path, content, &new_stat, &new_hash, now)?;
        if let Some(stem) = bind_log {
            // Recovery re-binding, atomic with the row itself; the
            // partial UNIQUE index backstops double-binding.
            tx.execute(
                "UPDATE files SET oplog_name = ?1 WHERE id = ?2 AND oplog_name IS NULL",
                rusqlite::params![stem, file_id],
            )?;
            // O-6 (#544): the re-bound log carries the file's
            // pre-delete history, but its event rows died with the old
            // `files` row (the CASCADE). Repopulate inside the binding
            // transaction — before the post-commit append adds the new
            // save's own row — so the recovered history is immediately
            // visible to the temporal operators, not deferred to the
            // next cache rebuild. Best-effort: a failure sets the
            // staleness marker and the next scan's rebuild heals.
            let repopulated = crate::oplog::read_oplog(&self.config.cache_dir, stem)
                .map_err(VaultError::Io)
                .and_then(|entries| {
                    let events = crate::oplog_events::derive_events_for_log(&entries);
                    insert_oplog_events(
                        &tx,
                        file_id,
                        &events,
                        retention_cutoff_ms(self.retention_days()),
                    )
                    .map_err(VaultError::from)
                });
            if let Err(e) = repopulated {
                log::warn!("oplog_events recovery repopulation failed for file_id={file_id}");
                log::debug!("oplog_events recovery repopulation failure detail: {e:?}");
                if let Err(e) = tx.execute("INSERT INTO oplog_events_stale (marker) VALUES (1)", [])
                {
                    log::warn!("oplog_events staleness marker write failed");
                    log::debug!("oplog_events staleness marker failure detail: {e}");
                }
            }
        }
        let oplog_name = self.ensure_oplog_name(&tx, file_id, path);
        tx.commit()?;
        self.bump_bases_generation();

        if let Some(log_name) = oplog_name.as_deref() {
            self.append_save_to_oplog(
                &conn,
                file_id,
                log_name,
                path,
                "",
                &new_hash,
                content,
                None,
                now,
                &[],
            );
        }
        Ok(SaveReport {
            new_content_hash: new_hash,
            new_size_bytes: new_stat.size_bytes,
            new_mtime_ms: new_stat.mtime_ms,
        })
    }

    /// The `files.oplog_name` binding for `path`; `Ok(None)` covers
    /// both "unindexed" and "indexed but never saved through Slate".
    fn bound_log_name(&self, path: &str) -> Result<Option<String>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        Ok(conn
            .query_row(
                "SELECT oplog_name FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get::<_, Option<String>>(0),
            )
            .optional()?
            .flatten())
    }

    /// Page through the indexed files.
    pub fn list_files(
        &self,
        filter: FileFilter,
        paging: Paging,
    ) -> Result<Page<FileSummary>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        list_files_impl(&conn, filter, paging)
    }

    /// Return the distinct normalized tag inventory from the tag index.
    pub fn list_tags(&self) -> Result<Vec<String>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        let mut stmt = conn.prepare("SELECT DISTINCT tag_norm FROM file_tags ORDER BY tag_norm")?;
        let rows = stmt.query_map([], |row| row.get::<_, String>(0))?;
        rows.collect::<Result<Vec<_>, _>>()
            .map_err(VaultError::from)
    }

    /// List one level of the file tree: the child directories of
    /// `parent_path`, then a page of its child files (#459, U2-1).
    ///
    /// `parent_path = ""` lists the root level. The path is validated
    /// (no absolute, no `..`, no platform prefix) the same way the
    /// provider validates mutation paths; an invalid path surfaces
    /// `VaultError::InvalidPath` without touching the index.
    ///
    /// Ordering: directories first, then files, each sorted
    /// case-insensitively on the NFC form of the name (a decomposed and
    /// precomposed spelling of the same name sort adjacently and
    /// deterministically). `dirs` is the complete child-directory list;
    /// `files` is paged via the supplied [`Paging`]. Each
    /// `DirNodeSummary` carries the immediate child dir/file counts so
    /// the UI can announce a collapsed folder without a second fetch.
    ///
    /// Lazy per level: nothing recursive happens, so one call materializes
    /// exactly one expanded folder.
    pub fn list_dir_children(
        &self,
        parent_path: &str,
        paging: Paging,
    ) -> Result<DirListing, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        list_dir_children_impl(&conn, parent_path, paging)
    }

    /// Every outgoing link from `path`, in document order. Includes
    /// resolved (internal-and-found), unresolved (internal-and-missing),
    /// and external links so the UI can render them all in one list
    /// with kind flags. Returns an empty vec when the file isn't
    /// indexed yet.
    pub fn outgoing_links(&self, path: &str) -> Result<Vec<crate::OutgoingLink>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::links_db::outgoing_links_for(&conn, path)
    }

    /// Resolve one `![[target]]` embed reference (host file + raw
    /// target string, anchor still embedded in the target) into an
    /// `EmbedResolution` the UI can render.
    ///
    /// Image targets short-circuit through `read_attachment`. Note
    /// targets go through the same `link_resolver` path as outgoing
    /// links; the optional `#heading` / `^block` suffix narrows the
    /// resolution to a section or block. Nested embeds inside a
    /// `FullNote` or `Section` resolution are pre-resolved up to
    /// `MAX_EMBED_DEPTH` (3) so the UI doesn't have to track
    /// recursion itself.
    pub fn resolve_embed(
        &self,
        host_path: &str,
        target: &str,
        alt: Option<String>,
    ) -> Result<crate::EmbedResolution, VaultError> {
        self.resolve_embed_at_depth(host_path, target, 0, alt)
    }

    fn resolve_embed_at_depth(
        &self,
        host_path: &str,
        target: &str,
        depth: u32,
        alt: Option<String>,
    ) -> Result<crate::EmbedResolution, VaultError> {
        use crate::embeds::{EmbedAnchor, parse_embed_target};

        if depth >= crate::MAX_EMBED_DEPTH {
            return Ok(crate::EmbedResolution::Unresolved {
                reason: crate::EmbedUnresolvedReason::DepthLimitReached,
            });
        }

        let (note_name, anchor) = parse_embed_target(target);

        // Image targets: looks_like_image fires on the raw target's
        // extension before we hit the link_resolver. Thread the
        // host path through so relative-folder resolution stays
        // symmetric with the note-target branch (Codoki PR #190).
        if crate::embeds::looks_like_image(note_name) {
            return self.resolve_image_embed(host_path, target, note_name, alt);
        }

        // Note target: snapshot the file index, run link_resolver,
        // then read the resolved file off-mutex.
        let target_path = {
            let conn = self.conn.lock().expect("session connection mutex");
            let paths: Vec<String> = conn
                .prepare("SELECT path FROM files")?
                .query_map([], |row| row.get::<_, String>(0))?
                .collect::<Result<Vec<_>, _>>()?;
            let vault_index = crate::InMemoryVaultIndex::new(paths);
            match crate::resolve_link(note_name, None, host_path, &vault_index) {
                crate::ResolvedLink::Resolved { target_path, .. } => target_path,
                _ => {
                    return Ok(crate::EmbedResolution::Unresolved {
                        reason: crate::EmbedUnresolvedReason::TargetNotFound {
                            target: target.to_string(),
                        },
                    });
                }
            }
        };

        let text = match self.read_text(&target_path) {
            Ok(t) => t,
            Err(VaultError::Io(e)) if e.kind() == std::io::ErrorKind::NotFound => {
                // The index pointed at a path the filesystem no
                // longer has — surface as TargetNotFound so the UI
                // doesn't show a stack-trace-shaped error.
                return Ok(crate::EmbedResolution::Unresolved {
                    reason: crate::EmbedUnresolvedReason::TargetNotFound {
                        target: target.to_string(),
                    },
                });
            }
            Err(e) => {
                return Ok(crate::EmbedResolution::Unresolved {
                    reason: crate::EmbedUnresolvedReason::ReadError {
                        message: e.to_string(),
                    },
                });
            }
        };

        match anchor {
            None => self.resolve_full_note_embed(target_path, text, depth),
            Some(EmbedAnchor::Heading(h)) => {
                self.resolve_section_embed(target_path, text, h, depth)
            }
            Some(EmbedAnchor::Block(id)) => self.resolve_block_embed(target_path, text, id),
        }
    }

    fn resolve_image_embed(
        &self,
        host_path: &str,
        raw_target: &str,
        note_name: &str,
        alt: Option<String>,
    ) -> Result<crate::EmbedResolution, VaultError> {
        // Same path-resolution strategy as note targets: snapshot
        // the file index and run the link_resolver. `looks_like_image`
        // guaranteed the extension is one the resolver recognises;
        // basename / folder matching does the rest. `host_path`
        // threads through so any future folder-relative resolution
        // in `link_resolver` lights up automatically.
        let target_path = {
            let conn = self.conn.lock().expect("session connection mutex");
            let paths: Vec<String> = conn
                .prepare("SELECT path FROM files")?
                .query_map([], |row| row.get::<_, String>(0))?
                .collect::<Result<Vec<_>, _>>()?;
            let vault_index = crate::InMemoryVaultIndex::new(paths);
            match crate::resolve_link(note_name, None, host_path, &vault_index) {
                crate::ResolvedLink::Resolved { target_path, .. } => target_path,
                _ => {
                    return Ok(crate::EmbedResolution::Unresolved {
                        reason: crate::EmbedUnresolvedReason::TargetNotFound {
                            target: raw_target.to_string(),
                        },
                    });
                }
            }
        };
        // #433: the alt arrives as an argument — threaded from the
        // link's persisted display_text (top level: the Swift caller
        // passes its OutgoingLink's displayText; nested:
        // resolve_nested_embeds passes the freshly-parsed link's
        // display_text per occurrence). #419's interim re-read +
        // re-parse of the host per image is gone, and nested alt is
        // now per-occurrence by construction.
        match self.read_attachment(&target_path) {
            Ok(att) => Ok(crate::EmbedResolution::Image {
                target_path,
                bytes: att.bytes,
                mime: att.mime,
                alt,
            }),
            Err(VaultError::Io(e)) if e.kind() == std::io::ErrorKind::NotFound => {
                Ok(crate::EmbedResolution::Unresolved {
                    reason: crate::EmbedUnresolvedReason::TargetNotFound {
                        target: raw_target.to_string(),
                    },
                })
            }
            Err(e) => Ok(crate::EmbedResolution::Unresolved {
                reason: crate::EmbedUnresolvedReason::ReadError {
                    message: e.to_string(),
                },
            }),
        }
    }

    fn resolve_full_note_embed(
        &self,
        target_path: String,
        text: String,
        depth: u32,
    ) -> Result<crate::EmbedResolution, VaultError> {
        let body = crate::embeds::strip_frontmatter_for_embed(&text).to_string();
        let nested = self.resolve_nested_embeds(&target_path, &body, depth)?;
        Ok(crate::EmbedResolution::FullNote {
            target_path,
            text: body,
            nested,
        })
    }

    fn resolve_section_embed(
        &self,
        target_path: String,
        text: String,
        heading_name: &str,
        depth: u32,
    ) -> Result<crate::EmbedResolution, VaultError> {
        match crate::embeds::extract_section(&text, heading_name) {
            Some((matched_heading, section_text)) => {
                let nested = self.resolve_nested_embeds(&target_path, &section_text, depth)?;
                Ok(crate::EmbedResolution::Section {
                    target_path,
                    heading: matched_heading,
                    text: section_text,
                    nested,
                })
            }
            None => Ok(crate::EmbedResolution::Unresolved {
                reason: crate::EmbedUnresolvedReason::HeadingNotFound {
                    target_path,
                    heading: heading_name.to_string(),
                },
            }),
        }
    }

    fn resolve_block_embed(
        &self,
        target_path: String,
        text: String,
        block_id: &str,
    ) -> Result<crate::EmbedResolution, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        let file_id: Option<i64> = conn
            .query_row(
                "SELECT id FROM files WHERE path = ?1",
                rusqlite::params![target_path],
                |row| row.get(0),
            )
            .optional()?;
        let Some(file_id) = file_id else {
            return Ok(crate::EmbedResolution::Unresolved {
                reason: crate::EmbedUnresolvedReason::BlockNotFound {
                    target_path,
                    block_id: block_id.to_string(),
                },
            });
        };
        let resolved = crate::blocks_db::resolve_block(&conn, file_id, block_id)?;
        drop(conn);
        let Some(b) = resolved else {
            return Ok(crate::EmbedResolution::Unresolved {
                reason: crate::EmbedUnresolvedReason::BlockNotFound {
                    target_path,
                    block_id: block_id.to_string(),
                },
            });
        };
        let block_text = text
            .get(b.byte_start as usize..b.byte_end as usize)
            .unwrap_or("")
            .to_string();
        Ok(crate::EmbedResolution::Block {
            target_path,
            block_id: block_id.to_string(),
            text: block_text,
        })
    }

    /// Walk `text` for `![[…]]` / `![…](…)` embed references and
    /// recursively resolve each one at `depth + 1`. Used by the
    /// `FullNote` and `Section` resolution paths to pre-bake the
    /// nested tree so the UI never recurses.
    fn resolve_nested_embeds(
        &self,
        host_path: &str,
        text: &str,
        depth: u32,
    ) -> Result<Vec<crate::NestedEmbed>, VaultError> {
        let next_depth = depth + 1;
        let mut out: Vec<crate::NestedEmbed> = Vec::new();
        for link in crate::extract_links(text) {
            if !link.is_embed {
                continue;
            }
            // Reconstruct the embed target string (target + optional
            // anchor) so the recursive call parses identically to
            // the top-level entry point.
            let target_with_anchor = match link.anchor.as_ref() {
                Some(crate::LinkAnchor::Heading(h)) => format!("{}#{h}", link.target_raw),
                Some(crate::LinkAnchor::Block(b)) => format!("{}^{b}", link.target_raw),
                None => link.target_raw.clone(),
            };
            let resolution = self.resolve_embed_at_depth(
                host_path,
                &target_with_anchor,
                next_depth,
                link.display_text.clone(),
            )?;
            out.push(crate::NestedEmbed {
                raw_target: target_with_anchor,
                byte_offset_in_parent: link.span_start as u32,
                resolution,
            });
        }
        Ok(out)
    }

    /// Paged inbound links to `path`. Excludes external links by
    /// construction (their `target_path` is NULL). Snippets are
    /// served from the cached `links.snippet` column — no disk
    /// re-reads.
    pub fn backlinks(
        &self,
        path: &str,
        paging: Paging,
    ) -> Result<Page<crate::Backlink>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::links_db::backlinks_for(&conn, path, paging)
    }

    /// Combined fetch of every datum the UI needs to render a note's
    /// side panels: backlinks, outgoing links, and properties — under
    /// a single mutex acquisition.
    ///
    /// Same total work as calling `backlinks`, `outgoing_links`, and
    /// `get_file_metadata` in sequence, but the lock is held for one
    /// contiguous slice instead of three races. The scanner holds
    /// the lock for the full duration of a slow-path transaction
    /// (see `Session::scan_initial`), so on a 10k-file initial scan
    /// every selection-change in the UI previously stalled three
    /// times waiting for the scanner transaction to commit. Now it
    /// stalls once. (#92 item 4.)
    pub fn note_load_bundle(
        &self,
        path: &str,
        backlinks_paging: Paging,
    ) -> Result<NoteLoadBundle, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        let backlinks = crate::links_db::backlinks_for(&conn, path, backlinks_paging)?;
        let outgoing_links = crate::links_db::outgoing_links_for(&conn, path)?;
        let properties = match get_file_metadata_impl(&conn, path)? {
            Some(meta) => meta.properties,
            None => Vec::new(),
        };
        Ok(NoteLoadBundle {
            backlinks,
            outgoing_links,
            properties,
        })
    }

    /// Paged audit of every unresolved internal link in the vault.
    /// Useful for "broken links" panels and pre-commit lint flows.
    pub fn list_unresolved_links(
        &self,
        paging: Paging,
    ) -> Result<Page<crate::UnresolvedLink>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::links_db::unresolved_links(&conn, paging)
    }

    /// Paged list of files whose frontmatter contains property `key`
    /// with a value matching `value` (case-insensitive). For list /
    /// tag_list properties, each element is searched independently —
    /// matching any element counts as a hit on the file.
    pub fn files_with_property(
        &self,
        key: &str,
        value: &str,
        paging: Paging,
    ) -> Result<Page<FileSummary>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::properties_db::files_with_property(&conn, key, value, paging)
    }

    /// Every distinct frontmatter property key in the vault, key-sorted,
    /// each with the count of files that carry it (m_spec §M-5). Powers
    /// `slate properties` (no `--key`) and the app's future property
    /// browser. `file_count` counts distinct files, so a key repeated on
    /// one file (a dotted key that recurs) is counted once.
    pub fn list_property_keys(&self) -> Result<Vec<crate::PropertyKeySummary>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::properties_db::list_property_keys(&conn)
    }

    /// Paged list of files carrying property `key` with **any** value
    /// (m_spec §M-5) — the key-only companion to `files_with_property`.
    /// Path-ordered and cursor-paged identically, so the CLI can drain
    /// to exhaustion by feeding `next_cursor` back until it is `None`.
    pub fn files_with_property_key(
        &self,
        key: &str,
        paging: Paging,
    ) -> Result<Page<FileSummary>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::properties_db::files_with_property_key(&conn, key, paging)
    }

    /// Every task parsed from `path`, in document order. Returns an
    /// empty vec when the file isn't indexed yet or has no tasks.
    pub fn tasks_for_file(&self, path: &str) -> Result<Vec<crate::TaskItem>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::tasks_db::tasks_for_file(&conn, path)
    }

    /// Replace one character — the `[X]` status — on a single task
    /// line, atomically. Routes through `save_text` so the same
    /// content-hash conflict detection, atomic temp+rename write,
    /// index refresh, and op-log append all fire as for a normal
    /// editor save.
    ///
    /// `ordinal` is the 0-based task-in-document index returned by
    /// `tasks_for_file` / `tasks_in_vault`. Stable across saves for
    /// a given parser version: the same source text yields the same
    /// ordinals, so the caller's stored ordinal stays valid for as
    /// long as the file hasn't been externally edited in a way that
    /// shifts the task layout.
    ///
    /// Errors:
    ///   - `InvalidArgument` when `ordinal` is out of range or the
    ///     task line can't be located (parser drift; the file is
    ///     left untouched).
    ///   - `WriteConflict` (via `save_text`) when
    ///     `expected_content_hash = Some(h)` and the file's current
    ///     hash on disk doesn't match.
    pub fn toggle_task_status(
        &self,
        path: &str,
        ordinal: u32,
        new_status_char: char,
        expected_content_hash: Option<&str>,
    ) -> Result<SaveReport, VaultError> {
        validate_save_path(path)?;

        // Acquire the session mutex BEFORE reading the file so a
        // concurrent `save_text` on the same path can't slip a write
        // in between our read and our save. The previous shape (read
        // outside the lock, save inside) lost the editor's write
        // every time the saves were close together — see the
        // `toggle_task_status_does_not_lose_concurrent_save`
        // regression test for the race the red team reproduced
        // (#135).
        //
        // `read_text` doesn't touch the connection, so it's safe to
        // call from inside this critical section — only the
        // `save_text` write path and the index queries care about
        // the mutex.
        let mut conn = self.conn.lock().expect("session connection mutex");

        let contents = self.read_text(path)?;
        let tasks = crate::extract_tasks(&contents);
        let task = tasks.iter().find(|t| t.ordinal == ordinal).ok_or_else(|| {
            VaultError::InvalidArgument {
                message: format!("no task at ordinal {ordinal} in {path:?}"),
            }
        })?;

        let bracket_idx = find_status_char_byte_offset(&contents, task.byte_offset as usize)
            .ok_or_else(|| VaultError::InvalidArgument {
                message: format!(
                    "could not locate `[X]` brackets at ordinal {ordinal} in {path:?}"
                ),
            })?;

        // Replace exactly the status character — preserve indentation,
        // bullet, post-bracket spacing, task text, and metadata
        // markers byte-for-byte.
        let mut new_contents = String::with_capacity(contents.len() + 4);
        new_contents.push_str(&contents[..bracket_idx]);
        new_contents.push(new_status_char);
        let old_char = contents[bracket_idx..]
            .chars()
            .next()
            .expect("status char position was just confirmed in find_status_char_byte_offset");
        new_contents.push_str(&contents[bracket_idx + old_char.len_utf8()..]);

        // Size check mirrors the one in `save_text` — both call
        // sites must enforce the refuse threshold.
        if new_contents.len() as u64 > self.config.large_file_refuse_bytes {
            return Err(VaultError::FileTooLarge {
                path: path.to_string(),
                size: new_contents.len() as u64,
            });
        }

        self.save_text_locked(
            &mut conn,
            path,
            &new_contents,
            expected_content_hash,
            &[crate::oplog::OpAnnotation::ToggleTask {
                ordinal,
                new_status: new_status_char,
            }],
        )
    }

    /// Insert or replace a single YAML frontmatter property and flush
    /// through the same `save_text` pipeline as the editor (atomic
    /// write, op-log entry, full reindex).
    ///
    /// Existing keys keep their position in the frontmatter block; a
    /// brand-new key appends at the end. The body of the note (after
    /// the closing `---`) is byte-identical to its pre-edit state.
    ///
    /// `WriteConflict` fires when the on-disk content hash no longer
    /// matches `expected_content_hash` — the same shape the editor's
    /// save uses, so the UI can reuse the conflict dialog.
    ///
    /// Returns `MalformedFrontmatter` rather than overwriting a YAML
    /// block that doesn't parse: the user's source is still
    /// authoritative and we don't try to merge into broken YAML.
    pub fn set_property(
        &self,
        path: &str,
        key: &str,
        value: crate::PropertyValue,
        expected_content_hash: Option<&str>,
    ) -> Result<SaveReport, VaultError> {
        validate_save_path(path)?;

        // Acquire the mutex before the read so a concurrent `save_text`
        // can't slip between our read and write — same shape as
        // `toggle_task_status` (#135).
        let mut conn = self.conn.lock().expect("session connection mutex");

        let contents = self.read_text(path)?;
        let new_contents = crate::frontmatter::set_property_in_source(&contents, key, &value)
            .map_err(|e| frontmatter_edit_error_to_vault_error(e, path))?;

        if new_contents.len() as u64 > self.config.large_file_refuse_bytes {
            return Err(VaultError::FileTooLarge {
                path: path.to_string(),
                size: new_contents.len() as u64,
            });
        }

        let value_json = crate::properties_db::property_value_to_json(&value).to_string();
        self.save_text_locked(
            &mut conn,
            path,
            &new_contents,
            expected_content_hash,
            &[crate::oplog::OpAnnotation::SetProperty {
                key: key.to_string(),
                value_json,
            }],
        )
    }

    /// Remove a single YAML frontmatter property.
    ///
    /// When the deletion empties the frontmatter, the entire `---`
    /// shell is removed — no empty `---\n---\n` left behind.
    ///
    /// When the key isn't present (or the file has no frontmatter at
    /// all), the call short-circuits: no write, no op-log entry. The
    /// `expected_content_hash` is still validated against the on-disk
    /// hash so callers don't silently mask a `WriteConflict` they'd
    /// have caught with a real edit.
    ///
    /// Returns `MalformedFrontmatter` on unparseable YAML for the same
    /// reason `set_property` does — we don't try to merge into broken
    /// YAML.
    pub fn delete_property(
        &self,
        path: &str,
        key: &str,
        expected_content_hash: Option<&str>,
    ) -> Result<SaveReport, VaultError> {
        validate_save_path(path)?;

        let mut conn = self.conn.lock().expect("session connection mutex");

        let contents = self.read_text(path)?;
        let edit = crate::frontmatter::delete_property_in_source(&contents, key)
            .map_err(|e| frontmatter_edit_error_to_vault_error(e, path))?;

        let new_contents = match edit {
            crate::frontmatter::FrontmatterEdit::Changed(s) => s,
            crate::frontmatter::FrontmatterEdit::Unchanged => {
                // Audit #174: the previous short-circuit did a second
                // disk read via `compute_disk_hash` to populate the
                // SaveReport, which could race against the `read_text`
                // we already did and produce a SaveReport whose hash
                // didn't match the bytes we actually observed. Hash
                // the bytes we read instead — the SaveReport then
                // describes the state we acted on, not a possibly-
                // different state on disk.
                let current_hash = crate::vault::content_hash(contents.as_bytes());
                if let Some(expected) = expected_content_hash
                    && current_hash != expected
                {
                    // mtime is best-effort metadata for the caller's
                    // change tracking; if the file's been deleted
                    // out from under us between read and now, fall
                    // back to 0 rather than failing the whole call
                    // on a missing-file stat.
                    let current_mtime_ms =
                        self.provider.stat(path).map(|s| s.mtime_ms).unwrap_or(0);
                    return Err(VaultError::WriteConflict {
                        current_content_hash: current_hash,
                        expected_content_hash: expected.to_string(),
                        current_mtime_ms,
                    });
                }
                let stat = self.provider.stat(path)?;
                return Ok(SaveReport {
                    new_content_hash: current_hash,
                    new_size_bytes: contents.len() as u64,
                    new_mtime_ms: stat.mtime_ms,
                });
            }
        };

        if new_contents.len() as u64 > self.config.large_file_refuse_bytes {
            return Err(VaultError::FileTooLarge {
                path: path.to_string(),
                size: new_contents.len() as u64,
            });
        }

        self.save_text_locked(
            &mut conn,
            path,
            &new_contents,
            expected_content_hash,
            &[crate::oplog::OpAnnotation::RemoveProperty {
                key: key.to_string(),
            }],
        )
    }

    /// Read a note split into `{ fm_source, body }` plus the whole-file
    /// content hash and mtime — the U3 tab-open call (#469, U3-5).
    ///
    /// One read, one hash. The body-only editor buffer is populated from
    /// `body`; the properties widget from `fm_source`; the hash chain
    /// stays whole-file so a later [`save_composed`](Self::save_composed)
    /// can conflict-detect an external edit to *either* half.
    ///
    /// Uses [`read_text`](Self::read_text) for the read, so the same
    /// size-cap, `InvalidUtf8`, and path-validation guarantees apply. The
    /// split reuses `body_after_frontmatter`'s boundary via
    /// [`crate::split_note`] — there is no second delimiter parser.
    pub fn read_note_parts(&self, path: &str) -> Result<NotePartsBundle, VaultError> {
        // `read_text` doesn't touch the connection mutex, so no lock is
        // needed here — this is a pure read, like `read_text` itself.
        let contents = self.read_text(path)?;
        let content_hash = crate::vault::content_hash(contents.as_bytes());
        let mtime_ms = self.provider.stat(path)?.mtime_ms;
        let parts = crate::split_note(&contents);
        // Byte-exact by construction: the body is a suffix of `contents`
        // (split_note never rewrites bytes), so the prefix length is the
        // difference — no compose-rule arithmetic anywhere.
        let body_byte_offset = contents.len() - parts.body.len();
        let body_line_offset = contents[..body_byte_offset]
            .bytes()
            .filter(|b| *b == b'\n')
            .count() as u32;
        Ok(NotePartsBundle {
            fm_source: parts.fm_source,
            body: parts.body,
            content_hash,
            mtime_ms,
            body_byte_offset: body_byte_offset as u64,
            body_line_offset,
        })
    }

    /// Compose `fm_source ⊕ body` and save through the **existing**
    /// `save_text` machinery — conflict detection, atomic write, index
    /// refresh, op-log append (#469, U3-5). No second write path.
    ///
    /// This is the body-only editor's save: the widget hands the current
    /// frontmatter source, the editor hands the body, and
    /// [`crate::compose_note`] joins them into the whole-file bytes
    /// `save_text` writes. `expected_content_hash` is the whole-file hash
    /// from [`read_note_parts`](Self::read_note_parts) or the previous
    /// [`SaveReport`], so an external edit to either the frontmatter or
    /// the body since the read is caught as `WriteConflict`.
    ///
    /// The composed form is canonical (`---\n{fm}\n---\n{body}`, or
    /// `body` alone when `fm_source` is empty). A note whose frontmatter
    /// was authored with a non-canonical delimiter shape (trailing
    /// whitespace, CRLF delimiters, a leading BOM) is normalized on the
    /// first composed save — the byte-exact pass-through reconstruction
    /// lives in [`crate::NoteParts::compose`], which the host uses when
    /// it needs to preserve the authored shape without a round trip.
    pub fn save_composed(
        &self,
        path: &str,
        fm_source: &str,
        body: &str,
        expected_content_hash: Option<String>,
    ) -> Result<SaveReport, VaultError> {
        validate_save_path(path)?;
        let composed = crate::compose_note(fm_source, body);
        // Size check mirrors `save_text` — enforce the refuse threshold at
        // this call site too, before we take the connection lock.
        if composed.len() as u64 > self.config.large_file_refuse_bytes {
            return Err(VaultError::FileTooLarge {
                path: path.to_string(),
                size: composed.len() as u64,
            });
        }
        self.save_text(path, &composed, expected_content_hash.as_deref())
    }

    /// Replace a note's frontmatter source wholesale — the U3-4
    /// show-source YAML commit path (#469, plumbing for #468).
    ///
    /// `fm_source` is validated first: it must be empty or parse as a
    /// YAML mapping ([`crate::validate_frontmatter_source`]), else
    /// `MalformedFrontmatter` is returned with a line/column message and
    /// **nothing is written** (the UI keeps the user's draft). On success
    /// the note's **current body is read fresh** and recomposed with the
    /// new frontmatter, then saved through `save_text` — so an in-flight
    /// body edit isn't clobbered and the whole-file hash chain is
    /// preserved.
    ///
    /// Unlike [`set_property`](Self::set_property), the frontmatter is
    /// stored **verbatim** (no round trip through the YAML emitter): the
    /// user's authored comments, anchors, and formatting survive, because
    /// this is the source-of-truth-editing surface, not a per-key mutation.
    pub fn set_frontmatter_source(
        &self,
        path: &str,
        fm_source: &str,
        expected_content_hash: Option<String>,
    ) -> Result<SaveReport, VaultError> {
        validate_save_path(path)?;

        // Validate BEFORE any read/write so a malformed draft is a pure,
        // non-destructive error — the file is never touched on failure.
        crate::validate_frontmatter_source(fm_source)
            .map_err(|e| frontmatter_edit_error_to_vault_error(e, path))?;

        // Acquire the mutex before the read so a concurrent `save_text`
        // can't slip between our body read and our compose+save — same
        // shape as `set_property` / `toggle_task_status` (#135).
        let mut conn = self.conn.lock().expect("session connection mutex");

        // Read the CURRENT body so a live body edit isn't dropped, then
        // recompose with the validated frontmatter. `read_text` doesn't
        // touch the connection, so it's safe inside this critical section.
        let contents = self.read_text(path)?;
        let body = crate::split_note(&contents).body;
        let composed = crate::compose_note(fm_source, &body);

        if composed.len() as u64 > self.config.large_file_refuse_bytes {
            return Err(VaultError::FileTooLarge {
                path: path.to_string(),
                size: composed.len() as u64,
            });
        }

        self.save_text_locked(
            &mut conn,
            path,
            &composed,
            expected_content_hash.as_deref(),
            &[crate::oplog::OpAnnotation::FrontmatterReplace],
        )
    }

    /// Rename a YAML frontmatter property across every file in the
    /// vault that currently carries `old_key`.
    ///
    /// Two modes:
    ///   - `dry_run = true` returns the per-file diff without writing.
    ///     Useful for the bulk-rename preview UI.
    ///   - `dry_run = false` iterates per-file: read → in-memory edit →
    ///     atomic `save_text` carrying the fresh on-disk hash as
    ///     `expected_content_hash`. Per-file `WriteConflict` from an
    ///     external mid-rename modification becomes a `RenameFailed`
    ///     entry; the rest of the vault still processes.
    ///
    /// Files that no longer carry `old_key` between the SQL scan and
    /// the read land in `skipped` as `NoSuchKey`. Files that already
    /// have both `old_key` and `new_key` land in `skipped` as
    /// `KeyCollision` — we don't silently overwrite the existing
    /// `new_key` value.
    ///
    /// Cancellation: the loop checks `cancel` between files. Already-
    /// saved files stay saved; remaining files end up in `failed` with
    /// `RenameFailureKind::Cancelled` so the caller can render a
    /// partial-progress report.
    pub fn rename_property_across_vault(
        &self,
        old_key: &str,
        new_key: &str,
        dry_run: bool,
        cancel: &CancelToken,
    ) -> Result<RenameReport, VaultError> {
        if old_key.is_empty() || new_key.is_empty() {
            return Err(VaultError::InvalidArgument {
                message: "rename requires non-empty old_key and new_key".to_string(),
            });
        }
        if old_key == new_key {
            return Err(VaultError::InvalidArgument {
                message: "old_key and new_key are identical".to_string(),
            });
        }
        // Audit #179: dotted keys aren't symmetric between the read
        // path (which produces them by flattening nested mappings)
        // and the write path (which would create a duplicate top-
        // level key). Refuse at the boundary.
        if old_key.contains('.') || new_key.contains('.') {
            return Err(VaultError::InvalidArgument {
                message: format!(
                    "rename refuses dotted keys ({old_key:?} → {new_key:?}); \
                     the read path's dotted-key flattening isn't symmetric \
                     with the writer"
                ),
            });
        }

        // Snapshot the candidate set up front; release the connection
        // mutex before iterating so per-file `save_text` calls can
        // acquire it independently. The snapshot can drift between
        // here and the per-file open — that drift is handled by the
        // `NoSuchKey` skip path.
        let candidates: Vec<String> = {
            let conn = self.conn.lock().expect("session connection mutex");
            let mut stmt = conn.prepare(
                "SELECT DISTINCT files.path
                 FROM files
                 JOIN properties p ON p.file_id = files.id
                 WHERE p.key = ?1
                 ORDER BY files.path COLLATE BINARY ASC",
            )?;

            stmt.query_map(rusqlite::params![old_key], |row| row.get::<_, String>(0))?
                .collect::<Result<Vec<_>, _>>()?
        };

        let mut report = RenameReport {
            affected: Vec::new(),
            skipped: Vec::new(),
            failed: Vec::new(),
        };

        for path in candidates {
            if cancel.is_cancelled() {
                report.failed.push(RenameFailed {
                    path,
                    kind: RenameFailureKind::Cancelled,
                    message: "rename cancelled before this file was processed".to_string(),
                });
                continue;
            }

            let source = match self.read_text(&path) {
                Ok(s) => s,
                Err(VaultError::Io(ref io_err))
                    if io_err.kind() == std::io::ErrorKind::NotFound =>
                {
                    // File disappeared between snapshot and open — treat
                    // as a no-op rather than a hard failure.
                    report.skipped.push(RenameSkipped {
                        path,
                        reason: RenameSkipReason::NoSuchKey,
                    });
                    continue;
                }
                Err(e) => {
                    report.failed.push(RenameFailed {
                        path,
                        kind: classify_rename_failure(&e),
                        message: e.to_string(),
                    });
                    continue;
                }
            };

            let (props, _) = crate::frontmatter::extract_frontmatter(&source);
            let Some(old_value) = props
                .iter()
                .find(|p| p.key == old_key)
                .map(|p| p.value.clone())
            else {
                report.skipped.push(RenameSkipped {
                    path,
                    reason: RenameSkipReason::NoSuchKey,
                });
                continue;
            };

            if props.iter().any(|p| p.key == new_key) {
                report.skipped.push(RenameSkipped {
                    path,
                    reason: RenameSkipReason::KeyCollision,
                });
                continue;
            }

            // Audit #180B: crossing the `tags` key boundary with a
            // list-shaped value drifts the type discriminator on
            // round-trip (reader's `tags`-keyname classifier flips
            // `List ↔ TagList`). Refuse rather than silently mutate
            // the on-disk value form.
            if crosses_tags_boundary(old_key, new_key, &old_value) {
                report.skipped.push(RenameSkipped {
                    path,
                    reason: RenameSkipReason::TagsKeyTypeDrift,
                });
                continue;
            }

            // In-memory edit: set new_key to the old value, then drop
            // old_key. Both helpers reject malformed frontmatter, so the
            // first call effectively gates the second.
            let after_source =
                match crate::frontmatter::set_property_in_source(&source, new_key, &old_value)
                    .and_then(|with_new| {
                        match crate::frontmatter::delete_property_in_source(&with_new, old_key)? {
                            crate::frontmatter::FrontmatterEdit::Changed(s) => Ok(s),
                            // The new key landed, the old key was
                            // already gone before delete ran — that
                            // shouldn't happen since we just observed
                            // it in `props`. Treat as a successful
                            // edit (the new key is in place).
                            crate::frontmatter::FrontmatterEdit::Unchanged => Ok(with_new),
                        }
                    }) {
                    Ok(s) => s,
                    Err(e) => {
                        let (kind, message) = match e {
                            crate::frontmatter::FrontmatterEditError::MalformedFrontmatter(
                                reason,
                            ) => (RenameFailureKind::MalformedFrontmatter, reason),
                            crate::frontmatter::FrontmatterEditError::InvalidPropertyValue {
                                reason,
                            } => (RenameFailureKind::Other, reason),
                        };
                        report.failed.push(RenameFailed {
                            path,
                            kind,
                            message,
                        });
                        continue;
                    }
                };

            let before_excerpt = excerpt_around_key(&source, old_key);
            let after_excerpt = excerpt_around_key(&after_source, new_key);

            if dry_run {
                report.affected.push(RenameAffected {
                    path,
                    before_excerpt,
                    after_excerpt,
                    applied: false,
                });
                continue;
            }

            // Apply: pin the expected hash to what we just read so a
            // mid-rename external write surfaces as WriteConflict for
            // this one file.
            let expected_hash = crate::vault::content_hash(source.as_bytes());
            match self.save_text(&path, &after_source, Some(&expected_hash)) {
                Ok(_) => report.affected.push(RenameAffected {
                    path,
                    before_excerpt,
                    after_excerpt,
                    applied: true,
                }),
                Err(e) => {
                    report.failed.push(RenameFailed {
                        path,
                        kind: classify_rename_failure(&e),
                        message: e.to_string(),
                    });
                }
            }
        }

        Ok(report)
    }

    /// Paged vault-wide task query. Results are ordered
    /// `(due_ms ASC NULLS LAST, priority DESC NULLS LAST,
    /// file path, ordinal ASC)` — overdue/today/soon first, then
    /// prioritised, then alphabetically.
    pub fn tasks_in_vault(
        &self,
        filter: crate::TaskFilter,
        paging: Paging,
    ) -> Result<Page<crate::TaskWithLocation>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::tasks_db::tasks_in_vault(&conn, filter, paging)
    }

    /// Run a full-text search across the vault. `scope` lets callers
    /// narrow to a folder or tag; `cancel` cooperates the same way it
    /// does for `scan_initial` — the result-collection loop checks the
    /// token between rows. The reserved `File` scope returns
    /// `VaultError::Unsupported`; `Tag` is live (see
    /// `search_db::SearchScope::Tag`).
    pub fn full_text_search(
        &self,
        query: &str,
        scope: &crate::SearchScope,
        cancel: &CancelToken,
    ) -> Result<crate::QueryResultSet, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::search_db::full_text_search(&conn, query, scope, cancel)
    }

    /// Enumerate templates under [`SessionConfig::templates_dir`].
    ///
    /// Returns `Ok(Vec::new())` — never an error — for any of:
    ///
    /// - `templates_dir` is `None` (the vault has no templates folder
    ///   configured, e.g. a freshly-opened vault that never had a
    ///   `Templates/` directory).
    /// - `templates_dir` points at a path that no longer exists or
    ///   isn't a directory (vault changed under us between opens).
    ///
    /// Only `.md` files are returned. `.DS_Store`, `.gitkeep`, README
    /// images, etc. are silently filtered out. Results are sorted by
    /// `name` (case-insensitive) so the picker UI doesn't need its
    /// own sort.
    ///
    /// One disk read per template (to extract the description). For
    /// the V1 tester scope (`Templates/` folders contain a handful of
    /// files), this is fine; if vaults ever ship hundreds of templates
    /// we'd want a description column in the index.
    pub fn list_templates(&self) -> Result<Vec<crate::TemplateSummary>, VaultError> {
        let Some(dir) = self.config.templates_dir.as_deref() else {
            return Ok(Vec::new());
        };

        let entries = match self.provider.list_dir(dir) {
            Ok(entries) => entries,
            // Treat "directory disappeared since `from_filesystem` saw
            // it" as "no templates" rather than an error — the picker
            // UI is meant to be benign on a vault with no templates.
            // Anything that isn't a NotFound (permission denied, IO
            // error, etc.) is real and propagates.
            Err(VaultError::Io(io_err)) if io_err.kind() == std::io::ErrorKind::NotFound => {
                return Ok(Vec::new());
            }
            Err(e) => return Err(e),
        };

        let limit = self.config.large_file_refuse_bytes;
        let mut out: Vec<crate::TemplateSummary> = Vec::new();
        for entry in entries {
            if !matches!(
                entry.kind,
                crate::EntryKind::File | crate::EntryKind::Symlink
            ) {
                continue;
            }
            if !entry.name.to_ascii_lowercase().ends_with(".md") {
                continue;
            }
            let rel = format!("{dir}/{name}", name = entry.name);
            // `read_in_vault_with_cap` does the canonical-path
            // verify AND the read in one atomic step, so there's
            // no TOCTOU window where a symlink could be swapped
            // between the check and the open (#132, Codoki PR #153
            // follow-up). Three failure modes all end the same way
            // here: silently drop the entry rather than blanking
            // the picker over one bad template:
            //   - `InvalidPath` — canonical target escapes the
            //     vault root (e.g. `Templates/Pwn.md → /etc/passwd`).
            //   - `Io(NotFound)` — broken symlink or template was
            //     deleted between `list_dir` and the read.
            //   - any other `Io` — permission denied etc.
            let bytes = match self.provider.read_in_vault_with_cap(&rel, limit) {
                Ok(b) => b,
                Err(VaultError::InvalidPath { .. }) => continue,
                Err(VaultError::Io(_)) => continue,
                Err(e) => return Err(e),
            };
            if (bytes.len() as u64) > limit {
                // Skip oversized files rather than aborting the picker.
                continue;
            }
            let source = match String::from_utf8(bytes) {
                Ok(s) => s,
                Err(_) => continue, // non-UTF-8 templates aren't useful here
            };
            let name = std::path::Path::new(&entry.name)
                .file_stem()
                .map(|s| s.to_string_lossy().into_owned())
                .unwrap_or_else(|| entry.name.clone());
            let description = crate::templates::description_from_source(&source);
            out.push(crate::TemplateSummary {
                path: rel,
                name,
                description,
            });
        }

        out.sort_by(|a, b| {
            a.name
                .to_lowercase()
                .cmp(&b.name.to_lowercase())
                .then_with(|| a.name.cmp(&b.name))
        });
        Ok(out)
    }

    /// Render the template at `template_path` against `context`.
    ///
    /// `template_path` is vault-relative; reads use the same size cap
    /// as `read_text`, so a runaway template can't OOM the picker.
    /// The template source itself never crosses the FFI boundary — the
    /// host only sees the [`RenderedTemplate`] body and the cursor
    /// offset, which keeps the FFI surface narrow.
    pub fn render_template(
        &self,
        template_path: &str,
        context: crate::TemplateContext,
    ) -> Result<crate::RenderedTemplate, VaultError> {
        let source = self.read_template_source(template_path)?;
        Ok(crate::render_template_source(&source, &context))
    }

    /// Render `template_path` **and** report its prompt metadata from a
    /// **single** read of the source.
    ///
    /// Identical resolution / cap / error semantics to [`render_template`]
    /// (both go through [`Self::read_template_source`]), but returns the
    /// [`TemplateMetadata`](crate::TemplateMetadata) extracted from the
    /// *same bytes* that were rendered, so a caller deciding on unfilled
    /// prompts (e.g. the `slate render-template --strict` CLI path) can
    /// never observe a body and a prompt set from two different snapshots
    /// of a concurrently-edited / sync-managed template (Codex
    /// adversarial-review, M-6). The returned `body` and metadata are
    /// coherent by construction.
    pub fn render_template_with_metadata(
        &self,
        template_path: &str,
        context: crate::TemplateContext,
    ) -> Result<(crate::TemplateMetadata, crate::RenderedTemplate), VaultError> {
        let source = self.read_template_source(template_path)?;
        let metadata = crate::extract_template_metadata(&source);
        let rendered = crate::render_template_source(&source, &context);
        Ok((metadata, rendered))
    }

    /// Read a template's UTF-8 source under the vault, once, with the
    /// scope + size-cap discipline both template entry points share.
    ///
    /// Uses the atomic verify-and-read (`read_in_vault_with_cap`) instead
    /// of `read_text` so a symlink can't be swapped between the scope
    /// check and the open (#132, Codoki PR #153 follow-up), and so a
    /// template symlinking OUT of the vault is refused with `InvalidPath`
    /// rather than silently read. The provider's `resolve` step rejects
    /// `..` / absolute paths textually before the canonicalize. Errors
    /// surface as-is so an explicit render attempt gets a real
    /// "refused for safety" / "template not found" message.
    fn read_template_source(&self, template_path: &str) -> Result<String, VaultError> {
        let limit = self.config.large_file_refuse_bytes;
        let bytes = self.provider.read_in_vault_with_cap(template_path, limit)?;
        if (bytes.len() as u64) > limit {
            return Err(VaultError::FileTooLarge {
                path: template_path.to_string(),
                size: bytes.len() as u64,
            });
        }
        String::from_utf8(bytes).map_err(|_| VaultError::InvalidUtf8 {
            path: template_path.to_string(),
        })
    }

    // --- Milestone K content pipelines (#217 / #218 / #219) -------

    /// Extract every math block in `path` and render each via the
    /// session's `math_prefs`. Returns `Vec::new()` for files with
    /// no math.
    ///
    /// Reads the file fresh on each call (no cache yet — the LRU
    /// cache is a follow-up; render time on a typical note is
    /// dominated by MathCAT's per-block work which is bounded). The
    /// session's `math_prefs` field is consulted on every call so a
    /// settings change is observed immediately.
    pub fn get_math_blocks(&self, path: &str) -> Result<Vec<crate::math::MathBlock>, VaultError> {
        let source = self.read_text(path)?;
        let raws = crate::math::extract_math_blocks(&source);
        // Audit #259: read through the runtime-mutable Mutex so a
        // Settings-panel pref change reflects on the next call.
        // MathPrefs is Copy + small; the lock is held only long
        // enough to clone the value, no contention with renderers.
        let prefs = *self.math_prefs.lock().expect("math_prefs mutex poisoned");
        Ok(raws
            .iter()
            .map(|raw| crate::math::render_math(raw, prefs))
            .collect())
    }

    /// Swap the session's math preferences at runtime. Settings
    /// panel UIs call this when the user changes a Picker so
    /// subsequent `get_math_blocks` calls render with the new
    /// preferences immediately. Audit #259.
    pub fn set_math_prefs(&self, prefs: crate::math::MathPrefs) -> Result<(), VaultError> {
        *self.math_prefs.lock().expect("math_prefs mutex poisoned") = prefs;
        Ok(())
    }

    /// Extract every code block in `path` and highlight each via the
    /// matching tree-sitter grammar. Unknown languages fall back to a
    /// single `Other` token covering the source — never panics.
    pub fn get_syntax_tokens(&self, path: &str) -> Result<Vec<crate::code::CodeBlock>, VaultError> {
        let source = self.read_text(path)?;
        let raws = crate::code::extract_code_blocks(&source);
        Ok(raws.iter().map(crate::code::highlight_code).collect())
    }

    /// Extract every Mermaid diagram block in `path` and render each
    /// to SVG plus structured description. Failures surface as typed
    /// `DiagramRenderStatus::RenderFailed` with the source preserved
    /// so AT users still hear the raw text.
    pub fn get_diagram_blocks(
        &self,
        path: &str,
    ) -> Result<Vec<crate::diagram::DiagramBlock>, VaultError> {
        let source = self.read_text(path)?;
        let raws = crate::diagram::extract_diagram_blocks(&source);
        Ok(raws.iter().map(crate::diagram::render_diagram).collect())
    }

    /// Ordered whole-document block segmentation for the reading view
    /// (U3-1, #465). Reads the note fresh, then delegates to the pure
    /// [`crate::reading::reading_blocks_source`] — the reading view
    /// renders each returned block (existing Math/Code/Diagram views for
    /// specialized kinds, the inline pipeline for paragraph-family kinds).
    /// Frontmatter is excluded from the walk but block offsets are rebased
    /// onto the whole source so an editor can map a block back to a caret.
    ///
    /// U3-2 renders the *live* editor buffer directly via
    /// [`crate::reading::reading_blocks_source`] (exposed on the uniffi
    /// surface as `reading_blocks_source`); this path is the disk read
    /// for the initial open.
    pub fn reading_blocks(
        &self,
        path: &str,
    ) -> Result<Vec<crate::reading::ReadingBlock>, VaultError> {
        let source = self.read_text(path)?;
        Ok(crate::reading::reading_blocks_source(&source))
    }

    /// Accessor for the underlying config. Useful for hosts that want to
    /// surface the cache directory location.
    pub fn config(&self) -> &SessionConfig {
        &self.config
    }

    // ================================================================
    // Citations + bibliography (Milestone L, #278)
    // ================================================================

    /// Replace the active bibliography sources, reload all entries
    /// from disk, write them into `bibliography_entries`, and bump
    /// the in-memory `BibIndex` version so the renderer's cache
    /// invalidates implicitly.
    ///
    /// Sources are merged with first-source-wins on key collisions.
    /// Loader warnings are returned to the caller via the warnings
    /// vec (the UI uses them to flag malformed entries inline next
    /// to their source).
    pub fn set_bibliography_sources(
        &self,
        sources: Vec<crate::citations::bibliography::BibliographySource>,
    ) -> Result<Vec<crate::citations::bibliography::BibLoadWarning>, VaultError> {
        let vault_root = self.vault_root_for_bibliography()?;
        let mut all_warnings: Vec<crate::citations::bibliography::BibLoadWarning> = Vec::new();
        let mut per_source: Vec<(String, Vec<crate::citations::bibliography::BibEntry>)> =
            Vec::with_capacity(sources.len());
        for src in &sources {
            let result = crate::citations::bibliography::load_source(src, &vault_root)?;
            all_warnings.extend(result.warnings);
            per_source.push((src.path.clone(), result.entries));
        }
        let (merged, _collisions) = crate::citations::bibliography::merge_sources(&per_source);

        // Source-path lookup for the DB rewrite. The merge already
        // recorded a winner per key; we walk per_source in the same
        // order to find which source owns each entry.
        let source_for_key: std::collections::HashMap<String, String> = per_source
            .iter()
            .flat_map(|(path, entries)| entries.iter().map(move |e| (e.key.clone(), path.clone())))
            .fold(std::collections::HashMap::new(), |mut acc, (k, v)| {
                acc.entry(k).or_insert(v);
                acc
            });
        let lookup = move |key: &str| -> String {
            source_for_key
                .get(key)
                .cloned()
                .unwrap_or_else(|| "(unknown)".to_string())
        };

        let mut conn = self.conn.lock().expect("session connection mutex");
        crate::citations_db::replace_bibliography_entries(&mut conn, &merged, &lookup, now_ms())?;
        drop(conn);

        // Bump the in-memory index so the renderer's cache
        // invalidates on the next render call (the key includes
        // version).
        let mut idx = self.bib_index.lock().expect("bib_index mutex");
        let new_version = idx.version().saturating_add(1);
        *idx = Arc::new(crate::citations::bibliography::BibIndex::build(
            merged,
            new_version,
        ));
        Ok(all_warnings)
    }

    /// Render `reference` against the style identified by `style_id`.
    /// Loads the style lazily (`style_id` matches the CSL file's
    /// basename without `.csl`) and caches the resulting render keyed
    /// on `(reference, style_id, bib_index_version)`.
    ///
    /// Returns `VaultError::CslStyleUnreadable` if no path in the
    /// configured `default_style + additional_styles` matches
    /// `style_id` (or the file fails to load).
    pub fn render_citation(
        &self,
        reference: &crate::citations::CitationReference,
        style_id: &str,
    ) -> Result<crate::citations::render::RenderedCitation, VaultError> {
        let style = self.style_by_id(style_id)?;
        let bib = self.bib_index.lock().expect("bib_index mutex").clone();
        Ok(self.render_cache.render(reference, &bib, &style))
    }

    /// Resolve a `style_id` against the configured styles. Lazy-loads
    /// from disk on first miss; subsequent calls hit the cache.
    fn style_by_id(
        &self,
        style_id: &str,
    ) -> Result<Arc<crate::citations::render::CslStyle>, VaultError> {
        {
            let cache = self.csl_styles.lock().expect("csl_styles mutex");
            if let Some(s) = cache.get(style_id) {
                return Ok(s.clone());
            }
        }
        let vault_root = self.vault_root_for_bibliography()?;
        let prefs = &self.config.citations_prefs;
        let candidate_paths: Vec<String> = prefs
            .default_style
            .iter()
            .chain(prefs.additional_styles.iter())
            .cloned()
            .collect();
        for rel in candidate_paths {
            let p = std::path::Path::new(&rel);
            let resolved = if p.is_absolute() {
                p.to_path_buf()
            } else {
                vault_root.join(p)
            };
            let file_id = resolved
                .file_stem()
                .and_then(|s| s.to_str())
                .unwrap_or_default();
            if file_id == style_id {
                let style = Arc::new(crate::citations::render::load_style(&resolved)?);
                let mut cache = self.csl_styles.lock().expect("csl_styles mutex");
                cache.insert(style_id.to_string(), style.clone());
                return Ok(style);
            }
        }
        Err(VaultError::CslStyleUnreadable {
            path: style_id.to_string(),
            reason: "no configured CSL style matches this id".to_string(),
        })
    }

    /// Lightweight metadata for every CSL style configured in
    /// `.slate/prefs.json`. The full `CslStyle` value (including the
    /// parsed XML) lives in the session's lazy cache; this method
    /// returns only `(id, title, path)` so the FFI surface stays
    /// POD-shaped.
    pub fn list_csl_styles(&self) -> Result<Vec<CslStyleInfo>, VaultError> {
        let vault_root = self.vault_root_for_bibliography()?;
        let prefs = &self.config.citations_prefs;
        let mut out = Vec::new();
        for rel in prefs
            .default_style
            .iter()
            .chain(prefs.additional_styles.iter())
        {
            let p = std::path::Path::new(rel);
            let resolved = if p.is_absolute() {
                p.to_path_buf()
            } else {
                vault_root.join(p)
            };
            let id = resolved
                .file_stem()
                .and_then(|s| s.to_str())
                .unwrap_or_default()
                .to_string();
            // Try to load to get the title; on failure fall back to
            // the id so the picker still shows the entry.
            let title = match crate::citations::render::load_style(&resolved) {
                Ok(s) => s.title.clone(),
                Err(_) => id.clone(),
            };
            out.push(CslStyleInfo {
                id,
                path: resolved.display().to_string(),
                title,
            });
        }
        Ok(out)
    }

    /// Filesystem root of the vault, when the session has one.
    ///
    /// `.slate` is always at `<root>/.slate` (locked storage layout),
    /// so the cache dir's parent is the canonical derivation — the ONE
    /// rooted-ness convention shared by every filesystem-facing
    /// feature (bibliography, sync detection), so a session is never
    /// "rooted" for one and "unsupported" for another. `None` when
    /// `cache_dir` has no parent or an empty one (a bare relative
    /// path) — provider-abstracted/in-memory setups, where resolving
    /// vault paths against the process CWD would be meaningless.
    fn fs_root(&self) -> Option<std::path::PathBuf> {
        self.config
            .cache_dir
            .parent()
            .filter(|p| !p.as_os_str().is_empty())
            .map(std::path::Path::to_path_buf)
    }

    /// Resolve the vault root for bibliography path resolution.
    fn vault_root_for_bibliography(&self) -> Result<std::path::PathBuf, VaultError> {
        self.fs_root().ok_or_else(|| VaultError::Unsupported {
            feature: "bibliography on non-filesystem vault".to_string(),
        })
    }

    /// Detect external sync systems managing this vault (M-1, #532).
    ///
    /// Filesystem-probe based — sync markers are dot-prefixed and the
    /// scanner skips hidden entries, so the index can never see them.
    /// Sessions without a filesystem root return a report with
    /// `supported == false` rather than an error: the host renders
    /// from the flag. Synchronous and cheap (bounded exact-path
    /// probes); callers dispatch off-main like any FFI call.
    pub fn detect_sync(&self) -> Result<crate::sync_detect::SyncDetectionReport, VaultError> {
        match self.fs_root() {
            Some(root) => Ok(crate::sync_detect::detect_sync_providers(&root)),
            None => Ok(crate::sync_detect::SyncDetectionReport::unsupported()),
        }
    }

    /// Read the LiveSync plugin's config, credential-free (M-2, #533).
    ///
    /// Same `fs_root` rule as [`Self::detect_sync`]: a session without
    /// a filesystem root reads no config — `Ok(NotPresent)`, not an
    /// error. Read/parse failures are data (`Malformed { reason }`);
    /// the six allow-listed fields are the only values ever read out
    /// of the JSON.
    pub fn livesync_config(&self) -> Result<crate::sync_detect::LiveSyncConfigStatus, VaultError> {
        match self.fs_root() {
            Some(root) => Ok(crate::sync_detect::read_livesync_config(&root)),
            None => Ok(crate::sync_detect::LiveSyncConfigStatus::NotPresent),
        }
    }

    /// Every bibliography entry currently indexed, ordered by year
    /// desc then title asc.
    pub fn get_bibliography_entries(
        &self,
    ) -> Result<Vec<crate::citations::bibliography::BibEntry>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::citations_db::list_bibliography_entries(&conn)
    }

    /// Look up one bibliography entry by citation key.
    pub fn get_bibliography_entry(
        &self,
        key: &str,
    ) -> Result<Option<crate::citations::bibliography::BibEntry>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::citations_db::get_bibliography_entry(&conn, key)
    }

    /// Case-insensitive substring search on `title` + `authors_json`.
    pub fn search_bibliography(
        &self,
        query: &str,
    ) -> Result<Vec<crate::citations::bibliography::BibEntry>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::citations_db::search_bibliography(&conn, query)
    }

    /// Every file in the vault that contains at least one citation
    /// of `citation_key`. Ordered by path.
    pub fn list_files_citing(
        &self,
        citation_key: &str,
    ) -> Result<Vec<crate::citations_db::FileCiting>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::citations_db::list_files_citing(&conn, citation_key)
    }

    /// Every `(file_path, citation_key)` pair where the cited key
    /// has no matching `bibliography_entries` row.
    pub fn list_unresolved_citations(&self) -> Result<Vec<(String, String)>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::citations_db::list_unresolved_citations(&conn)
    }

    /// Every citation reference indexed for `path`, in document
    /// order. Empty vec when the file isn't indexed yet.
    pub fn list_citations_in_file(
        &self,
        path: &str,
    ) -> Result<Vec<crate::citations::CitationReference>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::citations_db::list_citations_in_file(&conn, path)
    }
}

// --- Internal: scan ---

fn scan_vault(
    provider: &dyn VaultProvider,
    conn: &mut Connection,
    parser_version: u32,
    large_file_refuse_bytes: u64,
    cancel: &CancelToken,
    listener: Option<&dyn ScanProgressListener>,
) -> Result<ScanReport, VaultError> {
    /// Fires `Cancelled` to the listener and returns Err. Only
    /// valid AFTER `Started` has been emitted — see the contract on
    /// `ScanProgress`. Pre-Started cancellation paths return Err
    /// directly without touching the listener.
    macro_rules! bail_cancelled_after_started {
        () => {{
            if let Some(l) = listener {
                l.on_progress(ScanProgress::Cancelled);
            }
            return Err(VaultError::Cancelled);
        }};
    }

    // Pre-Started cancel check: no listener calls yet because the
    // stream hasn't started. Same for a Cancel from count_files
    // below.
    if cancel.is_cancelled() {
        return Err(VaultError::Cancelled);
    }

    // Pre-pass: count the files we'll visit so the listener can show
    // "indexed N of M" progress from the very first FileIndexed
    // event. The FS may change between this count and the main scan
    // — that's allowed by the contract (the spec calls out best-
    // effort totals).
    let total_files = count_files(provider, cancel)?;

    let mut report = ScanReport::default();
    let now = now_ms();
    // Open an IMMEDIATE transaction *before* emitting Started so a
    // tx-open failure can't leave listeners stuck waiting for a terminal
    // event. Taking SQLite's one-writer lock before the initial index
    // snapshot also serializes simultaneous cold scans: a second process
    // cannot snapshot an empty cache and then lose the deferred lock
    // upgrade while indexing, returning a misleading partial scan.
    let tx = conn.transaction_with_behavior(rusqlite::TransactionBehavior::Immediate)?;

    // Snapshot vault-relative paths for link resolution. Built once
    // up-front so per-file scanning doesn't re-query SQLite for every
    // markdown file. Mid-scan resolutions against this snapshot are
    // best-effort — links to files first seen in this scan run start
    // out Unresolved and get fixed up by the post-scan re-resolve
    // pass below. That trade-off keeps per-file work O(scanner) not
    // O(scanner * vault).
    let vault_index = crate::InMemoryVaultIndex::new(
        tx.prepare("SELECT path FROM files")?
            .query_map([], |row| row.get::<_, String>(0))?
            .collect::<Result<Vec<_>, _>>()?,
    );
    if let Some(l) = listener {
        l.on_progress(ScanProgress::Started { total_files });
    }
    let mut indexed_count: u64 = 0;

    // Every non-dot directory the walk visits, so we can prune `dirs`
    // rows for directories that vanished from disk after the walk
    // (mirrors the intent behind stale-file cleanup, scoped to the
    // directory index this PR owns). The root ("") is never a `dirs`
    // row — `list_dir_children("")` lists root children — so it is not
    // tracked here.
    let mut seen_dirs: std::collections::HashSet<String> = std::collections::HashSet::new();

    // Every file the walk visits (indexed OR errored — an unreadable
    // file still exists on disk), so files deleted out-of-band since
    // the last scan can be pruned from the index (#641: a stale row
    // otherwise makes `write`/`read` treat a deleted note as existing —
    // `write` reports a misleading conflict and `--create` can't
    // recreate it).
    let mut seen_files: std::collections::HashSet<String> = std::collections::HashSet::new();
    // A failed directory listing hides its whole subtree from the walk;
    // pruning on a partial view would evict live rows wholesale. Track
    // completeness and skip the file prune on any listing error (the
    // rows heal on the next clean scan).
    let mut walk_complete = true;

    let mut stack: Vec<String> = vec![String::new()];
    while let Some(dir) = stack.pop() {
        if cancel.is_cancelled() {
            bail_cancelled_after_started!();
        }

        let entries = match provider.list_dir(&dir) {
            Ok(e) => e,
            Err(e) => {
                report.errors.push(format!("list_dir {dir:?}: {e}"));
                walk_complete = false;
                continue;
            }
        };

        for entry in entries {
            // Check inside the inner loop so a cancel request in the
            // middle of a flat directory of many large files doesn't
            // wait for the whole directory to finish hashing.
            if cancel.is_cancelled() {
                bail_cancelled_after_started!();
            }

            // Skip hidden files/directories. `.slate` (our cache) and
            // `.obsidian` (Obsidian-compatible vault config) must not be
            // indexed; the general "starts with dot" rule covers them
            // and matches typical vault expectations.
            if entry.name.starts_with('.') {
                continue;
            }

            let path = if dir.is_empty() {
                entry.name.clone()
            } else {
                format!("{}/{}", dir, entry.name)
            };

            match entry.kind {
                EntryKind::Directory => {
                    if let Err(e) = upsert_dir(&tx, &path, &dir, &entry.name) {
                        report.errors.push(format!("upsert dir {path:?}: {e}"));
                    }
                    seen_dirs.insert(path.clone());
                    stack.push(path);
                }
                EntryKind::File => {
                    report.files_seen += 1;
                    seen_files.insert(path.clone());
                    if let Err(e) = index_file(
                        &tx,
                        provider,
                        &path,
                        &entry.name,
                        parser_version,
                        now,
                        &mut report,
                        &vault_index,
                        large_file_refuse_bytes,
                    ) {
                        // NotFound here means the file vanished between
                        // the directory listing and the stat/read — a
                        // concurrent deleter (#641 codex round 5).
                        // Un-see it so the post-walk prune drops any
                        // stale row THIS scan; the disk truth is
                        // "gone". Every other per-file error
                        // (permissions, oversize, invalid UTF-8) keeps
                        // the row: the file still exists.
                        if matches!(
                            &e,
                            VaultError::Io(io_err)
                                if io_err.kind() == std::io::ErrorKind::NotFound
                        ) {
                            seen_files.remove(&path);
                        }
                        report.errors.push(format!("{path}: {e}"));
                    }
                    indexed_count += 1;
                    if let Some(l) = listener {
                        l.on_progress(ScanProgress::FileIndexed {
                            path: path.clone(),
                            indexed: indexed_count,
                            total: total_files,
                        });
                    }
                }
                EntryKind::Symlink => {
                    // Symlinks are skipped entirely. Following them risks
                    // hashing out-of-vault data (a link pointing at
                    // /etc/passwd would otherwise land in the index) and
                    // the engine has no way to enforce that the target
                    // stays under the vault root without canonicalize
                    // logic in the provider. Cycle/escape handling is a
                    // future-issue concern; for now, don't index links.
                }
            }
        }
    }

    // Prune `dirs` rows for directories no longer on disk. The walk
    // upserts every directory it sees into `seen_dirs`; anything left
    // in the table that we didn't see this pass was deleted (or renamed
    // away) since the last scan, so drop it. Runs inside the same
    // transaction so it commits atomically with the upserts above.
    if let Err(e) = prune_unseen_dirs(&tx, &seen_dirs) {
        report.errors.push(format!("prune stale dirs: {e}"));
    }

    // Prune `files` rows for files no longer on disk (#641, codex
    // adversarial round 4): without this, a note deleted out-of-band
    // stays "indexed" forever and existence checks lie — `slate write`
    // reports a misleading conflict instead of "no such note", and
    // `--create` anchors to the stale hash and can't recreate the note.
    // Deletion follows the shipped `delete_file` discipline exactly:
    // one `DELETE FROM files` per row — child tables cascade
    // (`ON DELETE CASCADE`), FTS is maintained by the migration-006
    // DELETE trigger. Skipped when any directory listing failed
    // (`walk_complete`): a partial walk must not evict live rows.
    if walk_complete && let Err(e) = prune_unseen_files(&tx, &seen_files) {
        report.errors.push(format!("prune stale files: {e}"));
    }

    // Re-resolve links that were Unresolved purely because their
    // target file hadn't been inserted yet at the time the source
    // was indexed (first-scan ordering problem). Reads & updates
    // happen inside the same transaction so they commit atomically
    // with the rest of the scan; a cancel beforehand short-circuits
    // through `bail_cancelled_after_started!` and skips this step.
    if let Err(e) = crate::links_db::re_resolve_unresolved_links(&tx) {
        report
            .errors
            .push(format!("re-resolve unresolved links: {e}"));
    }

    // Canvas index pass (#361). Runs after the walk for the same
    // first-scan-ordering reason as link re-resolution: a canvas
    // card's display title can depend on ANOTHER file's frontmatter
    // `title`, which is only guaranteed indexed once the walk is
    // done. Canvases are re-derived every scan rather than
    // fast-path-skipped because their titles depend on other files —
    // a note rename/retitle must reflect in canvas rows even when the
    // .canvas bytes didn't change. Vaults hold few canvases and one
    // derivation is milliseconds at the 2,000-node budget, so this
    // stays O(canvases), not O(vault).
    if let Err(e) = reindex_all_canvases(&tx, provider, large_file_refuse_bytes) {
        report.errors.push(format!("canvas index: {e}"));
    }

    // Commit can still fail (disk full, file corruption). If it
    // does, the listener has already seen `Started` and N
    // `FileIndexed`s — fire `Failed` before propagating the error
    // so the stream's terminal-event contract holds.
    if let Err(e) = tx.commit() {
        let message = e.to_string();
        if let Some(l) = listener {
            l.on_progress(ScanProgress::Failed {
                message: message.clone(),
            });
        }
        return Err(VaultError::from(e));
    }
    if let Some(l) = listener {
        l.on_progress(ScanProgress::Finished {
            report: report.clone(),
        });
    }
    Ok(report)
}

/// Pre-scan pass: walk the vault and count file-typed entries.
///
/// Mirrors `scan_vault`'s traversal rules — dot-prefixed entries are
/// skipped (so `.slate/`, `.obsidian/` don't inflate the total) and
/// symlinks are not counted since the main scan won't index them.
/// Honors the cancel token so a user who clicks Cancel before the
/// scanner starts doesn't pay the count cost either.
fn count_files(provider: &dyn VaultProvider, cancel: &CancelToken) -> Result<u64, VaultError> {
    let mut total: u64 = 0;
    let mut stack: Vec<String> = vec![String::new()];
    while let Some(dir) = stack.pop() {
        if cancel.is_cancelled() {
            return Err(VaultError::Cancelled);
        }
        let entries = match provider.list_dir(&dir) {
            Ok(e) => e,
            // Match scan_vault's tolerant behavior — a per-directory
            // error during counting shouldn't blow up the scan.
            Err(_) => continue,
        };
        for entry in entries {
            if entry.name.starts_with('.') {
                continue;
            }
            let path = if dir.is_empty() {
                entry.name.clone()
            } else {
                format!("{}/{}", dir, entry.name)
            };
            match entry.kind {
                EntryKind::Directory => stack.push(path),
                EntryKind::File => total += 1,
                EntryKind::Symlink => {}
            }
        }
    }
    Ok(total)
}

/// Upsert one directory row keyed by its vault-relative `path`.
///
/// `parent` is the directory the walk was listing (`""` for a root-
/// level directory) and `name` is the final path component. Keyed on
/// the `path UNIQUE` constraint so a rescan re-uses the same row —
/// `dirs.id` is therefore stable across rescans (the census asserts
/// it), matching the stable-`files.id` guarantee. Only `parent_path`
/// and `name` are refreshed on conflict; a plain re-scan leaves them
/// unchanged (the path is the conflict key, and parent/name are a pure
/// function of it), while a future in-place path rewrite (U2-2) can
/// reuse this to keep the row.
fn upsert_dir(
    tx: &rusqlite::Transaction,
    path: &str,
    parent: &str,
    name: &str,
) -> Result<(), VaultError> {
    tx.execute(
        "INSERT INTO dirs (path, parent_path, name)
         VALUES (?1, ?2, ?3)
         ON CONFLICT(path) DO UPDATE SET
            parent_path = excluded.parent_path,
            name        = excluded.name",
        rusqlite::params![path, parent, name],
    )?;
    Ok(())
}

/// Delete `dirs` rows whose `path` was not visited this scan.
///
/// The scan walks every non-dot directory and records each in
/// `seen_dirs`; whatever remains in the table afterward is a directory
/// that was removed or renamed on disk since the previous scan. We load
/// the current paths and delete the set difference by id in one pass, so
/// the work is O(rows) with no per-row query round-trip.
/// Delete `files` rows whose paths the walk did not see this pass —
/// files removed from disk out-of-band since the last scan. Same shape
/// as [`prune_unseen_dirs`]; the per-row `DELETE FROM files` matches
/// `delete_file`'s discipline (child tables cascade, FTS trigger
/// fires). Only called after a complete walk (#641).
fn prune_unseen_files(
    tx: &rusqlite::Transaction,
    seen_files: &std::collections::HashSet<String>,
) -> Result<(), VaultError> {
    let stale: Vec<i64> = {
        let mut stmt = tx.prepare("SELECT id, path FROM files")?;
        let rows = stmt.query_map([], |row| {
            Ok((row.get::<_, i64>(0)?, row.get::<_, String>(1)?))
        })?;
        let mut stale = Vec::new();
        for row in rows {
            let (id, path) = row?;
            if !seen_files.contains(&path) {
                stale.push(id);
            }
        }
        stale
    };
    for id in stale {
        tx.execute("DELETE FROM files WHERE id = ?1", rusqlite::params![id])?;
    }
    Ok(())
}

fn prune_unseen_dirs(
    tx: &rusqlite::Transaction,
    seen_dirs: &std::collections::HashSet<String>,
) -> Result<(), VaultError> {
    let stale: Vec<i64> = {
        let mut stmt = tx.prepare("SELECT id, path FROM dirs")?;
        let rows = stmt.query_map([], |row| {
            Ok((row.get::<_, i64>(0)?, row.get::<_, String>(1)?))
        })?;
        let mut stale = Vec::new();
        for row in rows {
            let (id, path) = row?;
            if !seen_dirs.contains(&path) {
                stale.push(id);
            }
        }
        stale
    };
    for id in stale {
        tx.execute("DELETE FROM dirs WHERE id = ?1", rusqlite::params![id])?;
    }
    Ok(())
}

#[allow(clippy::too_many_arguments)] // shared scanner state; bundling adds friction
/// # FTS5 trigger invariant (#93 item 3)
///
/// The `files_fts` external-content index is maintained by triggers
/// in migration 006 that fire on `INSERT`, `UPDATE OF body_text`,
/// and `DELETE` of `files`. The `UPDATE` form is deliberately
/// scoped to `body_text` — gating on the column keeps the fast
/// path (`UPDATE files SET indexed_at_ms = ?, ctime_ms = ?`) from
/// re-tokenizing every unchanged file on every scan tick.
///
/// **Invariant.** Any UPDATE that toggles `is_markdown` MUST also
/// update `body_text`, otherwise the FTS row goes stale silently
/// (md → non leaves the old row; non → md leaves none). Today
/// every code path that reclassifies a file also re-reads its
/// body, so the invariant holds — but a future extension
/// reclassification, rename-without-reread, or migration backfill
/// would break it. If you add such a path, also update `body_text`
/// in the same statement (set to `""` for non-markdown).
fn index_file(
    tx: &rusqlite::Transaction,
    provider: &dyn VaultProvider,
    path: &str,
    name: &str,
    parser_version: u32,
    now: i64,
    report: &mut ScanReport,
    vault_index: &crate::InMemoryVaultIndex,
    large_file_refuse_bytes: u64,
) -> Result<(), VaultError> {
    let stat = provider.stat(path)?;
    // Case-fold the extension so macOS (APFS) and Windows (NTFS) files
    // saved as `README.MD` or `Notes.Markdown` are still recognized as
    // markdown by `is_markdown` (and therefore by FileFilter::MarkdownOnly).
    let extension = std::path::Path::new(name)
        .extension()
        .and_then(|s| s.to_str())
        .map(|s| s.to_ascii_lowercase());
    let is_markdown = matches!(
        extension.as_deref(),
        Some("md") | Some("markdown") | Some("mdown") | Some("mkd")
    );
    let is_base = extension.as_deref() == Some("base");

    // Fast path: if the indexed row's (mtime_ms, size_bytes, ctime_ms)
    // already match what we just stat'd, assume content is unchanged
    // and skip the blake3 hash. ctime catches the case where mtime is
    // preserved by the writer (`cp -p`, `rsync -a`, snapshot restore)
    // — mtime alone would miss those. Pre-migration-002 rows store
    // ctime_ms = 0, and platforms without portable ctime (Windows)
    // hand us stat.ctime_ms = 0; in either case the ctime check is
    // skipped and the fast path keeps its mtime+size semantics. We
    // still refresh `indexed_at_ms` so a future stale-row sweep can
    // tell "the scanner has visited this" from "this row is orphaned."
    let existing: Option<(i64, i64, i64)> = tx
        .prepare_cached("SELECT mtime_ms, size_bytes, ctime_ms FROM files WHERE path = ?1")?
        .query_row(rusqlite::params![path], |row| {
            Ok((
                row.get::<_, i64>(0)?,
                row.get::<_, i64>(1)?,
                row.get::<_, i64>(2)?,
            ))
        })
        .optional()?;
    let replacing_existing_file = existing.is_some();
    if let Some((db_mtime_ms, db_size_bytes, db_ctime_ms)) = existing {
        let mtime_size_match = i64::try_from(stat.size_bytes).is_ok_and(|stat_size_bytes| {
            db_mtime_ms == stat.mtime_ms && db_size_bytes == stat_size_bytes
        });
        // ctime is an additional axis when both sides have it: it
        // catches mtime-preserving writes (`cp -p`, `rsync -a`) that
        // the mtime+size pair alone can't see. When either side is 0
        // — pre-migration-002 rows, or a platform without portable
        // ctime — fall back to mtime+size only so the fast path still
        // works.
        let ctime_match = stat.ctime_ms == 0 || db_ctime_ms == 0 || db_ctime_ms == stat.ctime_ms;
        if mtime_size_match && ctime_match {
            // Update indexed_at_ms unconditionally; update ctime_ms
            // only when the current stat actually carries one. The
            // CASE guards against two regressions:
            //   1. Pre-migration rows have ctime_ms = 0. Without the
            //      back-fill, they'd keep degrading to mtime+size-
            //      only semantics forever and miss mtime-preserving
            //      writes.
            //   2. Switching a vault between platforms (Linux with
            //      ctime → Windows without, or a future runtime that
            //      drops ctime support) must not clobber a known-good
            //      ctime_ms with a 0 sentinel from the current stat.
            //   3. (#801) birthtime follows the same two rules:
            //      migration-030 rows carry 0 until this back-fill
            //      copies the filesystem birth in — WITHOUT it,
            //      `created_since` silently omits every unchanged
            //      pre-upgrade file forever (adversarial review) —
            //      and a 0 sentinel from a platform that stops
            //      reporting birth must not clobber a known value.
            tx.execute(
                "UPDATE files SET
                    indexed_at_ms = ?1,
                    ctime_ms = CASE WHEN ?2 != 0 THEN ?2 ELSE ctime_ms END,
                    birthtime_ms = CASE WHEN ?4 != 0 THEN ?4 ELSE birthtime_ms END
                 WHERE path = ?3",
                rusqlite::params![now, stat.ctime_ms, path, stat.birthtime_ms],
            )?;
            report.files_skipped += 1;
            return Ok(());
        }
    }

    // Refuse to read files past the configured size threshold. The
    // editor's `read_text` path already enforces this; the scanner
    // used to read whatever was on disk into memory, then double it
    // via `from_utf8_lossy(&content).into_owned()`. A multi-GB file
    // would blow process memory and overflow SQLite's TEXT cap
    // when stored as `body_text`. Skip the body indexing for such
    // files and record a per-file error so the user notices via
    // `ScanReport.errors`; the row is still upserted with metadata
    // (size, mtime, hash of an empty body) so subsequent scans
    // don't re-trip the threshold on every pass.
    if stat.size_bytes > large_file_refuse_bytes {
        let message = format!(
            "{path}: {size} bytes exceeds large-file refuse threshold ({limit}); skipping body index",
            path = path,
            size = stat.size_bytes,
            limit = large_file_refuse_bytes,
        );
        report.errors.push(message);
        // Upsert metadata + empty body so the file appears in the
        // index (sidebar lists it, but search won't find anything
        // inside it). hash is computed against an empty body so
        // the row's hash matches the persisted body_text — the
        // fast path won't be tricked into thinking the body is
        // current the next time.
        let empty_hash = content_hash(b"");
        tx.execute(
            "INSERT INTO files
                (path, name, extension, size_bytes, mtime_ms, ctime_ms, birthtime_ms,
                 content_hash, parser_version, indexed_at_ms, is_markdown, body_text)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?11, ?7, ?8, ?9, ?10, '')
             ON CONFLICT(path) DO UPDATE SET
                name           = excluded.name,
                extension      = excluded.extension,
                size_bytes     = excluded.size_bytes,
                mtime_ms       = excluded.mtime_ms,
                ctime_ms       = excluded.ctime_ms,
                birthtime_ms   = CASE WHEN excluded.birthtime_ms != 0
                                      THEN excluded.birthtime_ms
                                      ELSE files.birthtime_ms END,
                content_hash   = excluded.content_hash,
                parser_version = excluded.parser_version,
                indexed_at_ms  = excluded.indexed_at_ms,
                is_markdown    = excluded.is_markdown,
                body_text      = excluded.body_text",
            rusqlite::params![
                path,
                name,
                extension,
                stat.size_bytes as i64,
                stat.mtime_ms,
                stat.ctime_ms,
                empty_hash,
                parser_version,
                now,
                is_markdown as i64,
                stat.birthtime_ms,
            ],
        )?;
        // A file that grew past the refuse threshold may have been
        // indexed earlier with full derivatives. Drop those rows so
        // backlinks / outgoing links / frontmatter properties /
        // headings panels don't continue surfacing data pointing
        // into a body we no longer index.
        let file_id: i64 = tx.query_row(
            "SELECT id FROM files WHERE path = ?1",
            rusqlite::params![path],
            |row| row.get(0),
        )?;
        purge_markdown_derivatives(tx, file_id, path, vault_index)?;
        purge_canvas_rows(tx, file_id)?;
        crate::bases_db::delete_base_file_for_file(tx, file_id)?;
        report.files_indexed += 1;
        return Ok(());
    }

    let content = provider.read_file(path)?;
    let hash = content_hash(&content);

    // Cache the markdown body in `files.body_text` so the FTS5
    // external-content trigger can index it. Non-markdown files
    // get the empty string — we don't index binary blobs, and
    // gating the body decode behind `is_markdown` keeps the cost
    // off the path for image / PDF / etc. files.
    //
    // Lossy decode matches the headings / links / properties path:
    // a single stray non-UTF-8 byte shouldn't wipe the file's
    // entire FTS row.
    let body_text: String = if is_markdown {
        String::from_utf8_lossy(&content).into_owned()
    } else {
        String::new()
    };

    tx.prepare_cached(
        "INSERT INTO files
            (path, name, extension, size_bytes, mtime_ms, ctime_ms, birthtime_ms,
             content_hash, parser_version, indexed_at_ms, is_markdown, body_text)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?12, ?7, ?8, ?9, ?10, ?11)
         ON CONFLICT(path) DO UPDATE SET
            name           = excluded.name,
            extension      = excluded.extension,
            size_bytes     = excluded.size_bytes,
            mtime_ms       = excluded.mtime_ms,
            ctime_ms       = excluded.ctime_ms,
            birthtime_ms   = CASE WHEN excluded.birthtime_ms != 0
                                  THEN excluded.birthtime_ms
                                  ELSE files.birthtime_ms END,
            content_hash   = excluded.content_hash,
            parser_version = excluded.parser_version,
            indexed_at_ms  = excluded.indexed_at_ms,
            is_markdown    = excluded.is_markdown,
            body_text      = excluded.body_text",
    )?
    .execute(rusqlite::params![
        path,
        name,
        extension,
        stat.size_bytes as i64,
        stat.mtime_ms,
        stat.ctime_ms,
        hash,
        parser_version,
        now,
        is_markdown as i64,
        body_text,
        stat.birthtime_ms,
    ])?;

    // For Markdown files, parse + persist headings, links, and
    // frontmatter properties in the same transaction as the files
    // upsert above. The fast path (mtime+size+ctime match) never
    // reaches here, so unchanged files don't churn the derivative
    // tables. Non-Markdown files have no headings / outgoing links /
    // frontmatter properties worth indexing.
    if is_markdown {
        // Need the file_id for the foreign keys. INSERT … ON CONFLICT
        // DO UPDATE doesn't expose the row id directly, so query it
        // back — cheap given we just touched the row.
        let file_id: i64 = tx
            .prepare_cached("SELECT id FROM files WHERE path = ?1")?
            .query_row(rusqlite::params![path], |row| row.get(0))?;
        // Reuse the already-decoded `body_text` so we don't pay the
        // utf8_lossy cost twice (once for FTS, once for parsers).
        index_markdown_derivatives(
            tx,
            file_id,
            path,
            body_text.as_str(),
            vault_index,
            replacing_existing_file,
        )?;
    } else if is_base {
        let file_id: i64 = tx
            .prepare_cached("SELECT id FROM files WHERE path = ?1")?
            .query_row(rusqlite::params![path], |row| row.get(0))?;
        let source = String::from_utf8_lossy(&content);
        crate::bases_db::replace_base_file_for_file(
            tx,
            file_id,
            name,
            source.as_ref(),
            parser_version,
            now,
        )?;
    }

    report.files_indexed += 1;
    report.bytes_processed += stat.size_bytes;
    Ok(())
}

/// Atomically replace all `headings` rows for `file_id` with the
/// headings extracted from `markdown_source`.
///
/// Called on the scanner's slow path only — the fast path never
/// touches the headings table, so unchanged files don't churn it.
fn replace_headings(
    tx: &rusqlite::Transaction,
    file_id: i64,
    markdown_source: &str,
) -> Result<(), VaultError> {
    tx.execute(
        "DELETE FROM headings WHERE file_id = ?1",
        rusqlite::params![file_id],
    )?;
    let headings = crate::extract_headings(markdown_source);
    if headings.is_empty() {
        return Ok(());
    }
    let mut stmt = tx.prepare_cached(
        "INSERT INTO headings (file_id, ordinal, level, text, anchor_id, byte_offset)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
    )?;
    for heading in headings {
        stmt.execute(rusqlite::params![
            file_id,
            heading.ordinal as i64,
            heading.level as i64,
            heading.text,
            heading.anchor_id,
            heading.byte_offset as i64,
        ])?;
    }
    Ok(())
}

/// Run the markdown-only indexing pipeline for one file: headings →
/// outgoing links → frontmatter properties. Each step is a
/// DELETE-then-INSERT for the file's rows in its table, so calling
/// this re-indexes from scratch even if previous rows existed.
fn index_markdown_derivatives(
    tx: &rusqlite::Transaction,
    file_id: i64,
    path: &str,
    body_text: &str,
    vault_index: &crate::InMemoryVaultIndex,
    replacing_existing_file: bool,
) -> Result<(), VaultError> {
    replace_headings(tx, file_id, body_text)?;
    crate::links_db::replace_links_for_file(tx, file_id, path, body_text, vault_index)?;
    crate::properties_db::replace_properties_for_file(tx, file_id, body_text)?;
    if replacing_existing_file {
        crate::dql_inline_fields_db::replace_dql_inline_fields_for_file(tx, file_id, body_text)?;
        crate::tags_db::replace_tags_for_file(tx, file_id, body_text)?;
    } else {
        crate::dql_inline_fields_db::insert_dql_inline_fields_for_new_file(tx, file_id, body_text)?;
        crate::tags_db::insert_tags_for_new_file(tx, file_id, body_text)?;
    }
    crate::tasks_db::replace_tasks_for_file(tx, file_id, body_text)?;
    crate::blocks_db::replace_blocks_for_file(tx, file_id, body_text)?;
    crate::citations_db::replace_citations_for_file(tx, file_id, body_text)?;
    crate::bases_db::replace_base_blocks_for_file(tx, file_id, body_text)?;
    Ok(())
}

/// Drop any cached headings, links, and properties rows for
/// `file_id`. Used when a file transitions into the large-file
/// branch (no body indexing) so previously-indexed derivatives from
/// when the same file was under the threshold don't linger as stale
/// rows in the sidebar / backlinks panel / properties query.
///
/// Each underlying `replace_*` function DELETEs first and skips its
/// INSERT loop when the source body is empty, so passing `""` is
/// the canonical "purge but leave the files row" idiom.
/// Given the start byte offset of a task's line in `source`, return
/// the byte offset of the status character (the `X` between `[` and
/// `]`). Mirrors the prefix-matching shape of
/// `crate::tasks::parse_task_line`: skip leading whitespace, bullet,
/// post-bullet whitespace, then `[`. Returns `None` only when the
/// line doesn't actually match the task shape — which shouldn't
/// happen if the caller derived `line_start` from a parsed
/// `TaskItem`, but the option keeps `toggle_task_status` honest
/// against an unexpected parser drift.
fn find_status_char_byte_offset(source: &str, line_start: usize) -> Option<usize> {
    if line_start > source.len() {
        return None;
    }
    let tail = &source[line_start..];
    let line_end = tail.find('\n').unwrap_or(tail.len());
    let line = &tail[..line_end];
    let bytes = line.as_bytes();
    let mut i = 0;
    while i < bytes.len() && (bytes[i] == b' ' || bytes[i] == b'\t') {
        i += 1;
    }
    if i >= bytes.len() || !matches!(bytes[i], b'-' | b'*' | b'+') {
        return None;
    }
    i += 1;
    while i < bytes.len() && (bytes[i] == b' ' || bytes[i] == b'\t') {
        i += 1;
    }
    if i >= bytes.len() || bytes[i] != b'[' {
        return None;
    }
    i += 1;
    if i >= bytes.len() {
        return None;
    }
    Some(line_start + i)
}

fn purge_markdown_derivatives(
    tx: &rusqlite::Transaction,
    file_id: i64,
    path: &str,
    vault_index: &crate::InMemoryVaultIndex,
) -> Result<(), VaultError> {
    replace_headings(tx, file_id, "")?;
    crate::links_db::replace_links_for_file(tx, file_id, path, "", vault_index)?;
    crate::properties_db::replace_properties_for_file(tx, file_id, "")?;
    crate::dql_inline_fields_db::mark_dql_inline_fields_incomplete_for_file(tx, file_id)?;
    crate::tags_db::replace_tags_for_file(tx, file_id, "")?;
    crate::tasks_db::replace_tasks_for_file(tx, file_id, "")?;
    crate::blocks_db::replace_blocks_for_file(tx, file_id, "")?;
    crate::citations_db::replace_citations_for_file(tx, file_id, "")?;
    crate::bases_db::replace_base_blocks_for_file(tx, file_id, "")?;
    Ok(())
}

// --- Internal: get_file_metadata ---

fn get_file_metadata_impl(
    conn: &Connection,
    path: &str,
) -> Result<Option<FileMetadata>, VaultError> {
    let summary: Option<(i64, String, String, i64, i64, i64, String)> = conn
        .query_row(
            "SELECT id, path, name, mtime_ms, size_bytes, is_markdown, content_hash
             FROM files WHERE path = ?1",
            rusqlite::params![path],
            |row| {
                Ok((
                    row.get::<_, i64>(0)?,
                    row.get::<_, String>(1)?,
                    row.get::<_, String>(2)?,
                    row.get::<_, i64>(3)?,
                    row.get::<_, i64>(4)?,
                    row.get::<_, i64>(5)?,
                    row.get::<_, String>(6)?,
                ))
            },
        )
        .optional()?;
    let Some((file_id, path, name, mtime_ms, size_bytes, is_markdown, content_hash)) = summary
    else {
        return Ok(None);
    };

    // prepare_cached: get_file_metadata is called on every note
    // selection in the UI, and the headings SELECT is the same
    // statement each time. Caching avoids the prepare-step overhead.
    let mut stmt = conn.prepare_cached(
        "SELECT ordinal, level, text, anchor_id, byte_offset
         FROM headings WHERE file_id = ?1
         ORDER BY ordinal ASC",
    )?;
    let headings: Result<Vec<crate::Heading>, rusqlite::Error> = stmt
        .query_map(rusqlite::params![file_id], |row| {
            Ok(crate::Heading {
                ordinal: row.get::<_, i64>(0)? as u32,
                level: row.get::<_, i64>(1)? as u8,
                text: row.get::<_, String>(2)?,
                anchor_id: row.get::<_, String>(3)?,
                byte_offset: row.get::<_, i64>(4)? as u32,
            })
        })?
        .collect();
    let headings = headings?;

    // Frontmatter properties are loaded after headings so a stale or
    // empty properties table (e.g. pre-migration-005 rows) doesn't
    // hold up the metadata read; same-transaction guarantees on the
    // write path mean both arrays always reflect the same parse.
    let properties = crate::properties_db::properties_for_file(conn, file_id)?;

    Ok(Some(FileMetadata {
        path,
        name,
        mtime_ms,
        size_bytes: size_bytes as u64,
        is_markdown: is_markdown != 0,
        content_hash,
        headings,
        properties,
    }))
}

// --- Internal: list_files ---

fn list_files_impl(
    conn: &Connection,
    filter: FileFilter,
    paging: Paging,
) -> Result<Page<FileSummary>, VaultError> {
    let where_clause = match filter {
        FileFilter::All => "1=1",
        FileFilter::MarkdownOnly => "is_markdown = 1",
        FileFilter::MarkdownAndCanvas => "(is_markdown = 1 OR extension = 'canvas')",
        FileFilter::OpenableDocuments => {
            "(is_markdown = 1 OR extension = 'canvas' OR extension = 'base')"
        }
    };

    let total: i64 = conn.query_row(
        &format!("SELECT COUNT(*) FROM files WHERE {where_clause}"),
        [],
        |row| row.get(0),
    )?;

    // Fetch limit + 1: the extra row tells us whether there's a next page.
    let fetch_n = paging.limit as i64 + 1;

    let mut items: Vec<FileSummary> = Vec::new();
    let row_to_summary = |row: &rusqlite::Row<'_>| -> rusqlite::Result<FileSummary> {
        Ok(FileSummary {
            path: row.get(0)?,
            name: row.get(1)?,
            mtime_ms: row.get(2)?,
            size_bytes: row.get::<_, i64>(3)? as u64,
            is_markdown: row.get::<_, i64>(4)? != 0,
        })
    };

    if let Some(cursor) = &paging.cursor {
        let query = format!(
            "SELECT path, name, mtime_ms, size_bytes, is_markdown FROM files
             WHERE {where_clause} AND path > ?1
             ORDER BY path ASC
             LIMIT ?2"
        );
        let mut stmt = conn.prepare(&query)?;
        let rows = stmt.query_map(rusqlite::params![cursor, fetch_n], row_to_summary)?;
        for r in rows {
            items.push(r?);
        }
    } else {
        let query = format!(
            "SELECT path, name, mtime_ms, size_bytes, is_markdown FROM files
             WHERE {where_clause}
             ORDER BY path ASC
             LIMIT ?1"
        );
        let mut stmt = conn.prepare(&query)?;
        let rows = stmt.query_map(rusqlite::params![fetch_n], row_to_summary)?;
        for r in rows {
            items.push(r?);
        }
    }

    let next_cursor = if items.len() > paging.limit as usize {
        items.pop();
        items.last().map(|f| f.path.clone())
    } else {
        None
    };

    Ok(Page {
        items,
        next_cursor,
        total_filtered: total as u64,
    })
}

// --- Internal: list_dir_children (#459, U2-1) ---

/// Case-insensitive sort key for a tree node name.
///
/// The spec pins the key to `name.to_lowercase()` on the **NFC**
/// normalization: a decomposed (NFD) and precomposed (NFC) spelling of
/// the same name normalize to the same key, so they sort adjacently and
/// deterministically rather than by raw code-unit order (where the
/// combining-mark form sorts after the base letter of the *next* name).
/// `to_lowercase` is the full Unicode lowercasing, matching the
/// resolver's case-insensitive discipline.
fn tree_sort_key(name: &str) -> String {
    use unicode_normalization::UnicodeNormalization;
    name.nfc().collect::<String>().to_lowercase()
}

/// Normalize + validate a `parent_path` for [`list_dir_children_impl`].
///
/// Returns the canonical vault-relative form (forward slashes, no
/// trailing `/`, `.` components stripped) or `VaultError::InvalidPath`.
/// `""` (root) is accepted and returned as `""`. Rejection rules mirror
/// the provider's `resolve_relative`: no absolute paths, no `..`, no
/// platform prefix. The canonical form must match the slash form the
/// scanner stores in `dirs.path` / `files.path`, since the child queries
/// key off it exactly.
fn normalize_parent_path(parent_path: &str) -> Result<String, VaultError> {
    use std::path::{Component, Path};
    let mut normalized = String::new();
    for component in Path::new(parent_path).components() {
        match component {
            Component::Normal(s) => {
                let seg = s.to_string_lossy();
                if !normalized.is_empty() {
                    normalized.push('/');
                }
                normalized.push_str(&seg);
            }
            Component::CurDir => {}
            Component::ParentDir => {
                return Err(VaultError::InvalidPath {
                    path: parent_path.to_string(),
                    reason: "parent-directory references (..) are not allowed".into(),
                });
            }
            Component::RootDir | Component::Prefix(_) => {
                return Err(VaultError::InvalidPath {
                    path: parent_path.to_string(),
                    reason: "absolute paths and platform prefixes are not allowed".into(),
                });
            }
        }
    }
    Ok(normalized)
}

/// Inclusive-lower / exclusive-upper bounds for a range-scan of every
/// descendant path under `parent` (`""` = whole vault).
///
/// For a non-root `parent` the subtree is every path in
/// `('parent/', 'parent' + '0')`: `'/'` is `0x2F` and the byte after it
/// is `'0'` (`0x30`), so `'parent0'` is the least string greater than
/// every `'parent/...'` string. We deliberately avoid `GLOB`/`LIKE` —
/// `[`, `*`, `?`, `_`, `%` are all legal in vault filenames and would be
/// interpreted as wildcards, silently corrupting the scan (gap_analysis
/// G10 / spec §U2-1). Returns `None` for the root, whose "subtree" is the
/// full table (no bounds).
fn subtree_bounds(parent: &str) -> Option<(String, String)> {
    if parent.is_empty() {
        None
    } else {
        Some((format!("{parent}/"), format!("{parent}0")))
    }
}

/// The immediate child segment of `path` relative to `parent`, or `None`
/// if `path` is a deeper descendant (has more than one segment below
/// `parent`) or is not under `parent` at all.
///
/// `parent = ""` treats the whole path as relative; a top-level entry
/// `"notes"` yields `Some("notes")`, a nested `"notes/a.md"` yields
/// `None`. For `parent = "notes"`, `"notes/a.md"` yields `Some("a.md")`
/// and `"notes/sub/a.md"` yields `None`.
fn immediate_child_segment<'a>(parent: &str, path: &'a str) -> Option<&'a str> {
    let rest = if parent.is_empty() {
        path
    } else {
        path.strip_prefix(parent)?.strip_prefix('/')?
    };
    if rest.is_empty() || rest.contains('/') {
        None
    } else {
        Some(rest)
    }
}

fn list_dir_children_impl(
    conn: &Connection,
    parent_path: &str,
    paging: Paging,
) -> Result<DirListing, VaultError> {
    let parent = normalize_parent_path(parent_path)?;

    // --- Child directories, with their immediate child counts. ---
    //
    // Range-scan the `dirs` subtree once (root = whole table), then
    // group in Rust: immediate children are the depth-1 rows; each such
    // child's `child_dir_count` is the number of descendant dirs whose
    // `parent_path` equals that child's path. Range-scan + Rust-side
    // count is the spec's normative choice — correctness over a
    // GLOB/LIKE query that filenames can break.
    let mut child_dirs: Vec<(i64, String, String)> = Vec::new(); // (id, path, name)
    let mut dir_child_dir_count: std::collections::HashMap<String, u32> =
        std::collections::HashMap::new();
    {
        let scan_dir_row = |row: &rusqlite::Row<'_>| -> rusqlite::Result<(i64, String, String)> {
            Ok((row.get(0)?, row.get(1)?, row.get(2)?))
        };
        let rows: Vec<(i64, String, String)> = match subtree_bounds(&parent) {
            Some((lo, hi)) => {
                let mut stmt = conn.prepare(
                    "SELECT id, path, parent_path FROM dirs WHERE path > ?1 AND path < ?2",
                )?;
                stmt.query_map(rusqlite::params![lo, hi], scan_dir_row)?
                    .collect::<Result<Vec<_>, _>>()?
            }
            None => {
                let mut stmt = conn.prepare("SELECT id, path, parent_path FROM dirs")?;
                stmt.query_map([], scan_dir_row)?
                    .collect::<Result<Vec<_>, _>>()?
            }
        };
        for (id, path, row_parent) in rows {
            // Tally toward the containing dir's child-dir count.
            *dir_child_dir_count.entry(row_parent).or_insert(0) += 1;
            if let Some(name) = immediate_child_segment(&parent, &path) {
                child_dirs.push((id, path.clone(), name.to_string()));
            }
        }
    }

    // --- Files: immediate child files of `parent`, plus each child
    //     directory's immediate child-file count. ---
    let mut listing_files: Vec<FileSummary> = Vec::new();
    let mut dir_child_file_count: std::collections::HashMap<String, u32> =
        std::collections::HashMap::new();
    {
        let scan_file_row = |row: &rusqlite::Row<'_>| -> rusqlite::Result<FileSummary> {
            Ok(FileSummary {
                path: row.get(0)?,
                name: row.get(1)?,
                mtime_ms: row.get(2)?,
                size_bytes: row.get::<_, i64>(3)? as u64,
                is_markdown: row.get::<_, i64>(4)? != 0,
            })
        };
        let rows: Vec<FileSummary> = match subtree_bounds(&parent) {
            Some((lo, hi)) => {
                let mut stmt = conn.prepare(
                    "SELECT path, name, mtime_ms, size_bytes, is_markdown FROM files
                     WHERE path > ?1 AND path < ?2",
                )?;
                stmt.query_map(rusqlite::params![lo, hi], scan_file_row)?
                    .collect::<Result<Vec<_>, _>>()?
            }
            None => {
                let mut stmt = conn
                    .prepare("SELECT path, name, mtime_ms, size_bytes, is_markdown FROM files")?;
                stmt.query_map([], scan_file_row)?
                    .collect::<Result<Vec<_>, _>>()?
            }
        };
        for f in rows {
            // A file's containing directory is its path minus the final
            // component. Tally it toward that dir's child-file count so a
            // collapsed child folder can announce its file count.
            let file_parent = match f.path.rfind('/') {
                Some(i) => &f.path[..i],
                None => "",
            };
            *dir_child_file_count
                .entry(file_parent.to_string())
                .or_insert(0) += 1;
            if immediate_child_segment(&parent, &f.path).is_some() {
                listing_files.push(f);
            }
        }
    }

    // Assemble + sort the child directories (dirs-first ordering is by
    // construction — they're a separate list from files).
    let mut dirs: Vec<DirNodeSummary> = child_dirs
        .into_iter()
        .map(|(id, path, name)| DirNodeSummary {
            child_dir_count: dir_child_dir_count.get(&path).copied().unwrap_or(0),
            child_file_count: dir_child_file_count.get(&path).copied().unwrap_or(0),
            id,
            path,
            name,
        })
        .collect();
    // Case-insensitive NFC sort; the path is a deterministic tiebreak so
    // two names with the same fold key (distinct only by case/normal
    // form) still order stably across runs. `sort_by_cached_key` computes
    // each NFC key once (not per comparison).
    dirs.sort_by_cached_key(|d| (tree_sort_key(&d.name), d.path.clone()));

    // Sort the files the same way, then page by numeric offset — the
    // level is materialized in memory (case-insensitive order doesn't
    // survive a SQL `path >` cursor), so the cursor is the count already
    // returned.
    listing_files.sort_by_cached_key(|f| (tree_sort_key(&f.name), f.path.clone()));
    let total_files = listing_files.len() as u64;
    let offset: usize = match &paging.cursor {
        Some(c) => c.parse().map_err(|_| VaultError::InvalidArgument {
            message: format!("invalid directory paging cursor {c:?}"),
        })?,
        None => 0,
    };
    let end = offset
        .saturating_add(paging.limit as usize)
        .min(listing_files.len());
    let start = offset.min(listing_files.len());
    let items: Vec<FileSummary> = listing_files[start..end].to_vec();
    let next_cursor = if end < listing_files.len() {
        Some(end.to_string())
    } else {
        None
    };

    Ok(DirListing {
        dirs,
        files: Page {
            items,
            next_cursor,
            total_filtered: total_files,
        },
    })
}

fn now_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0)
}

// --- Internal: save_text helpers ---

/// Reject `save_text` paths that can't refer to a real vault file —
/// empties, lone `.`, absolutes, `..`, and dot-prefixed components.
/// The provider's `resolve_for_mutation` enforces the traversal rules
/// at write time too, but rejecting up-front means a
/// `save_text("", b"x", Some(hash))` call fails as `InvalidPath`
/// rather than tripping a read-side IO error during the conflict
/// check.
fn validate_save_path(path: &str) -> Result<(), VaultError> {
    use std::path::{Component, Path};
    let p = Path::new(path);
    if p.is_absolute() {
        return Err(VaultError::InvalidPath {
            path: path.to_string(),
            reason: "absolute paths are not allowed; vault-relative only".into(),
        });
    }
    let mut has_normal = false;
    for component in p.components() {
        match component {
            Component::Normal(name) => {
                has_normal = true;
                // Dot-prefixed components are invisible to the scanner
                // (it skips them wholesale — most importantly `.slate`
                // and `.obsidian`, the internal/tool state namespaces).
                // A content save landing there would write a file Slate
                // can never index, and worse: files like
                // `.slate/prefs.json` are re-read as internal state on
                // the next open, so `slate write --create` must not be
                // able to plant one (#641, codex adversarial round 3).
                // Same rule structural mutations already enforce via
                // `validate_leaf_component` ("dot-prefixed names are
                // reserved"). No internal caller saves hidden paths
                // through this funnel — the app writes prefs via its
                // own PrefsJsonStore, not save_text.
                if name.to_string_lossy().starts_with('.') {
                    return Err(VaultError::InvalidPath {
                        path: path.to_string(),
                        reason: "dot-prefixed path components are reserved for Slate/tool \
                                 state and are never indexed"
                            .into(),
                    });
                }
            }
            Component::CurDir => {}
            Component::ParentDir => {
                return Err(VaultError::InvalidPath {
                    path: path.to_string(),
                    reason: "parent-directory references (..) are not allowed".into(),
                });
            }
            Component::RootDir | Component::Prefix(_) => {
                return Err(VaultError::InvalidPath {
                    path: path.to_string(),
                    reason: "absolute paths and platform prefixes are not allowed".into(),
                });
            }
        }
    }
    if !has_normal {
        return Err(VaultError::InvalidPath {
            path: path.to_string(),
            reason: "save requires a non-empty vault-relative path".into(),
        });
    }
    Ok(())
}

/// Stat + hash a file currently on disk. Used by `save_text`'s
/// conflict-detection path. A missing file returns `("", 0)` so the
/// caller can compare against an empty `expected_content_hash` (the
/// "save as new file" idiom).
///
/// The hash itself comes from [`crate::vault::content_hash`] (blake3,
/// lowercase hex, 64 chars). The returned tuple is `(hash, mtime_ms)`
/// — save uses both: the hash to detect "someone else wrote the same
/// path", the mtime to update the `files` row when the body matches.
///
/// Bounded by the same `large_file_refuse_bytes` cap as `read_text`:
/// if the file genuinely exceeds the threshold we surface
/// `FileTooLarge` and abort rather than allocating arbitrarily large
/// buffers to compute a hash we're about to compare.
/// True when the rename would push a list-shaped value across the
/// `tags` key boundary. The reader's `tags`-keyname special-case
/// classifies a list of strings as `TagList`; under any other key
/// the same list classifies as `List([Text, …])`. A rename that
/// crosses the boundary therefore flips the discriminator on
/// round-trip without changing the disk-byte form in the way the
/// user would expect. We refuse and surface the case as a
/// `TagsKeyTypeDrift` skip so the UI can offer a manual-edit
/// fallback (audit #180B).
fn crosses_tags_boundary(old_key: &str, new_key: &str, value: &crate::PropertyValue) -> bool {
    let is_list = matches!(
        value,
        crate::PropertyValue::List(_) | crate::PropertyValue::TagList(_)
    );
    if !is_list {
        return false;
    }
    (old_key == "tags") ^ (new_key == "tags")
}

/// Map a `FrontmatterEditError` to the `VaultError` shape the FFI
/// surface expects. Keeps all three callers (`set_property`,
/// `delete_property`, the rename helper) in sync — adding a new
/// `FrontmatterEditError` variant will fail to compile here, not at
/// each scattered match site.
fn frontmatter_edit_error_to_vault_error(
    err: crate::frontmatter::FrontmatterEditError,
    path: &str,
) -> VaultError {
    match err {
        crate::frontmatter::FrontmatterEditError::MalformedFrontmatter(reason) => {
            VaultError::MalformedFrontmatter {
                path: path.to_string(),
                reason,
            }
        }
        crate::frontmatter::FrontmatterEditError::InvalidPropertyValue { reason } => {
            VaultError::InvalidArgument {
                message: format!("invalid property value for {path:?}: {reason}"),
            }
        }
    }
}

fn classify_rename_failure(err: &VaultError) -> RenameFailureKind {
    match err {
        VaultError::WriteConflict { .. } => RenameFailureKind::WriteConflict,
        VaultError::MalformedFrontmatter { .. } => RenameFailureKind::MalformedFrontmatter,
        VaultError::Cancelled => RenameFailureKind::Cancelled,
        _ => RenameFailureKind::Other,
    }
}

/// Pull the YAML frontmatter line containing `key` plus one neighbour
/// line on each side, for the bulk-rename preview UI. Returns an empty
/// string when the file has no frontmatter or the key isn't present —
/// the caller decides how to render the absence.
///
/// Match rule (audit #178): the line must start `key:` at column 0
/// (no leading whitespace) so we don't false-positive on:
///   - block-scalar continuation lines that happen to start with
///     `key:` after indentation,
///   - nested-mapping keys (those are dotted in the read path; their
///     literal-on-disk form is indented and shouldn't match a flat
///     top-level rename).
///
/// The match also tolerates yaml-rust2's emitter quoting the key
/// (`"key":` / `'key':`) — it does that for scalars that look like
/// YAML 1.1 booleans (`y`, `n`, `on`, `off`, etc.) or that are
/// otherwise ambiguous.
fn excerpt_around_key(source: &str, key: &str) -> String {
    let Some(range) = crate::frontmatter::frontmatter_range(source) else {
        return String::new();
    };
    let body = &source[range];
    let lines: Vec<&str> = body.lines().collect();
    let bare = format!("{key}:");
    let dquoted = format!("\"{key}\":");
    let squoted = format!("'{key}':");
    let key_indexed = lines.iter().enumerate().find_map(|(i, line)| {
        if line.starts_with(&bare) || line.starts_with(&dquoted) || line.starts_with(&squoted) {
            Some(i)
        } else {
            None
        }
    });
    let Some(idx) = key_indexed else {
        return String::new();
    };
    let start = idx.saturating_sub(1);
    let end = (idx + 2).min(lines.len());
    lines[start..end].join("\n")
}

/// Read the on-disk file and return `(raw_bytes, content_hash, mtime_ms)`.
///
/// The hash is computed over the **raw bytes** (never decoded), so the
/// conflict-check path keeps working for any file including non-UTF-8 —
/// the bytes are returned so the diff-on-save path can attempt a UTF-8
/// decode and fall back to a snapshot if it isn't text (#378). A missing
/// file yields `(empty, "", 0)` — the first-save / NotFound case.
fn read_disk_contents_and_hash(
    provider: &dyn crate::VaultProvider,
    path: &str,
    limit: u64,
) -> Result<(Vec<u8>, String, i64), VaultError> {
    use std::io;
    let stat = match provider.stat(path) {
        Ok(s) => s,
        Err(VaultError::Io(io_err)) if io_err.kind() == io::ErrorKind::NotFound => {
            return Ok((Vec::new(), String::new(), 0));
        }
        Err(e) => return Err(e),
    };
    if stat.size_bytes > limit {
        return Err(VaultError::FileTooLarge {
            path: path.to_string(),
            size: stat.size_bytes,
        });
    }
    let bytes = provider.read_file_with_cap(path, limit)?;
    if (bytes.len() as u64) > limit {
        return Err(VaultError::FileTooLarge {
            path: path.to_string(),
            size: bytes.len() as u64,
        });
    }
    let hash = crate::vault::content_hash(&bytes);
    Ok((bytes, hash, stat.mtime_ms))
}

#[allow(clippy::too_many_arguments)]
fn refresh_text_derived_indexes_after_reclassification(
    tx: &rusqlite::Transaction,
    provider: &dyn VaultProvider,
    file_id: i64,
    path: &str,
    name: &str,
    extension: Option<&str>,
    is_markdown: bool,
    parser_version: u32,
    large_file_refuse_bytes: u64,
) -> Result<(), VaultError> {
    let vault_index = crate::InMemoryVaultIndex::new(
        tx.prepare("SELECT path FROM files")?
            .query_map([], |row| row.get::<_, String>(0))?
            .collect::<Result<Vec<_>, _>>()?,
    );
    let is_base = extension == Some("base");
    let write_body = |tx: &rusqlite::Transaction, body_text: &str| -> Result<(), VaultError> {
        tx.execute(
            "UPDATE files SET body_text = ?1, is_markdown = ?2 WHERE id = ?3",
            rusqlite::params![body_text, is_markdown as i64, file_id],
        )?;
        Ok(())
    };

    if !is_markdown && !is_base {
        write_body(tx, "")?;
        purge_markdown_derivatives(tx, file_id, path, &vault_index)?;
        crate::bases_db::delete_base_file_for_file(tx, file_id)?;
        return Ok(());
    }

    let stat = provider.stat(path)?;
    if stat.size_bytes > large_file_refuse_bytes {
        write_body(tx, "")?;
        purge_markdown_derivatives(tx, file_id, path, &vault_index)?;
        crate::bases_db::delete_base_file_for_file(tx, file_id)?;
        return Ok(());
    }

    let content = provider.read_file_with_cap(path, large_file_refuse_bytes)?;
    if content.len() as u64 > large_file_refuse_bytes {
        write_body(tx, "")?;
        purge_markdown_derivatives(tx, file_id, path, &vault_index)?;
        crate::bases_db::delete_base_file_for_file(tx, file_id)?;
        return Ok(());
    }

    let source = String::from_utf8_lossy(&content);
    if is_markdown {
        write_body(tx, source.as_ref())?;
        index_markdown_derivatives(tx, file_id, path, source.as_ref(), &vault_index, true)?;
        crate::bases_db::delete_base_file_for_file(tx, file_id)?;
    } else {
        write_body(tx, "")?;
        purge_markdown_derivatives(tx, file_id, path, &vault_index)?;
        crate::bases_db::replace_base_file_for_file(
            tx,
            file_id,
            name,
            source.as_ref(),
            parser_version,
            now_ms(),
        )?;
    }

    Ok(())
}

/// Pull `(name, extension, is_markdown)` from a vault-relative path
/// using the same rules as the scanner. Kept in one place so save
/// and scan can't drift on what counts as a Markdown file.
fn classify_path(path: &str) -> (String, Option<String>, bool) {
    let name = std::path::Path::new(path)
        .file_name()
        .map(|n| n.to_string_lossy().into_owned())
        .unwrap_or_else(|| path.to_string());
    let extension = std::path::Path::new(&name)
        .extension()
        .and_then(|s| s.to_str())
        .map(|s| s.to_ascii_lowercase());
    let is_markdown = matches!(
        extension.as_deref(),
        Some("md") | Some("markdown") | Some("mdown") | Some("mkd")
    );
    (name, extension, is_markdown)
}

// ---------------------------------------------------------------------------
// Structural mutations (U2-2, #460): folder/file create / rename / move /
// delete + the journal-backed undo. See `docs/plans/08_ui_parity/specs/
// u2_spec.md` §U2-2 for the normative semantics and `crate::structural` for
// the shared types.
//
// Concurrency: every mutation holds the connection mutex for its whole body
// (single-writer discipline, same as `set_property`). Ordering within a
// mutation: validate → collision-check → FILESYSTEM op → one SQLite
// transaction (index updates + journal append). The tx only runs after the
// fs op succeeded; if the tx fails, the fs op is reverted best-effort and
// the error surfaces (never a silently split state).

impl VaultSession {
    /// Create an empty folder. Journaled; undo deletes it if still empty.
    pub fn create_folder(
        &self,
        path: &str,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        validate_save_path(path)?;
        validate_leaf_component(leaf_name(path))?;
        let conn = self.conn.lock().expect("session connection mutex");
        if let Some(existing) = index_entry_case_insensitive(&conn, path)? {
            return Err(VaultError::DestinationExists { path: existing });
        }
        self.provider.create_dir(path)?;
        self.with_structural_tx(conn, |tx| {
            upsert_dir_row(tx, path)?;
            journal_append(
                tx,
                crate::structural::StructuralOpKind::CreateFolder,
                &crate::structural::StructuralOpPayload {
                    from: path.to_string(),
                    to: path.to_string(),
                    ..Default::default()
                },
            )
        })
        .map(|op_id| crate::structural::StructuralReport {
            op_id,
            moved: Vec::new(),
            rewritten: Vec::new(),
            failed: Vec::new(),
        })
    }

    /// Rename a folder in place (same parent, new final component).
    pub fn rename_folder(
        &self,
        path: &str,
        new_name: &str,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        let new_path = sibling_path(path, new_name)?;
        let report = self.structural_move_folder(
            path,
            &new_path,
            crate::structural::StructuralOpKind::RenameFolder,
            true,
        )?;
        self.notify_moved(&report);
        Ok(report)
    }

    /// Move a folder under a new parent ("" = vault root).
    pub fn move_folder(
        &self,
        path: &str,
        new_parent: &str,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        let new_path = child_path(new_parent, leaf_name(path))?;
        let report = self.structural_move_folder(
            path,
            &new_path,
            crate::structural::StructuralOpKind::MoveFolder,
            true,
        )?;
        self.notify_moved(&report);
        Ok(report)
    }

    /// Rename a file in place.
    pub fn rename_file(
        &self,
        path: &str,
        new_name: &str,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        let new_path = sibling_path(path, new_name)?;
        let report = self.structural_move_file(
            path,
            &new_path,
            crate::structural::StructuralOpKind::RenameFile,
            true,
        )?;
        self.notify_moved(&report);
        Ok(report)
    }

    /// Move a file under a new parent ("" = vault root).
    pub fn move_file(
        &self,
        path: &str,
        new_parent: &str,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        let new_path = child_path(new_parent, leaf_name(path))?;
        let report = self.structural_move_file(
            path,
            &new_path,
            crate::structural::StructuralOpKind::MoveFile,
            true,
        )?;
        self.notify_moved(&report);
        Ok(report)
    }

    /// Move a file to the system trash. Journaled for auditability; NOT
    /// undoable via `undo_structural` (the bytes are in the trash).
    pub fn delete_file(&self, path: &str) -> Result<(), VaultError> {
        validate_save_path(path)?;
        let conn = self.conn.lock().expect("session connection mutex");
        // Capture the op-log binding before the row goes: the journal
        // row is then the durable stem↔path association for a deleted
        // file (O-1 #539 — the `.oplog` itself is deliberately left in
        // place; O-3's recovery joins remnant logs with these rows,
        // O-2's retention sweep ages them out).
        let oplog_name: Option<String> = conn
            .query_row(
                "SELECT oplog_name FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get(0),
            )
            .optional()?
            .flatten();
        self.provider.delete(path)?;
        self.with_structural_tx(conn, |tx| {
            tx.execute("DELETE FROM files WHERE path = ?1", rusqlite::params![path])?;
            journal_append(
                tx,
                crate::structural::StructuralOpKind::DeleteFile,
                &crate::structural::StructuralOpPayload {
                    from: path.to_string(),
                    to: path.to_string(),
                    oplog_name,
                    ..Default::default()
                },
            )
        })?;
        self.bump_bases_generation();
        self.notify_file_change(FileChangeKind::Deleted, path, None);
        Ok(())
    }

    /// Move a folder (recursively) to the system trash. Journaled; not
    /// undoable via `undo_structural`.
    pub fn delete_folder(&self, path: &str) -> Result<(), VaultError> {
        validate_save_path(path)?;
        let conn = self.conn.lock().expect("session connection mutex");
        // #802: the range delete below erases the paths — capture them
        // first so each file's Deleted event can fire after commit.
        // In-memory and O(folder size) by design (Codoki on #846): a
        // folder delete already walks its subtree on disk, and events
        // are per-file by contract; revisit with streamed emission
        // only if a real vault shows this hot.
        let deleted_files: Vec<String> = {
            let (lo, hi) = subtree_bounds(path).expect("non-root folder path");
            let mut stmt = conn.prepare("SELECT path FROM files WHERE path >= ?1 AND path < ?2")?;
            let rows = stmt.query_map(rusqlite::params![lo, hi], |row| row.get(0))?;
            rows.collect::<Result<_, _>>()?
        };
        self.provider.delete(path)?;
        self.with_structural_tx(conn, |tx| {
            let (lo, hi) = subtree_bounds(path).expect("non-root folder path");
            tx.execute(
                "DELETE FROM files WHERE path >= ?1 AND path < ?2",
                rusqlite::params![lo, hi],
            )?;
            tx.execute(
                "DELETE FROM dirs WHERE path = ?1 OR (path >= ?2 AND path < ?3)",
                rusqlite::params![path, lo, hi],
            )?;
            journal_append(
                tx,
                crate::structural::StructuralOpKind::DeleteFolder,
                &crate::structural::StructuralOpPayload {
                    from: path.to_string(),
                    to: path.to_string(),
                    ..Default::default()
                },
            )
        })?;
        self.bump_bases_generation();
        for file in &deleted_files {
            self.notify_file_change(FileChangeKind::Deleted, file, None);
        }
        Ok(())
    }

    /// Undo the LATEST structural op (op_id must be MAX(id) — out-of-order
    /// undo would re-introduce the multi-file consistency problem the
    /// journal exists to avoid). The undo itself is journaled as the
    /// inverse op, so the ledger stays append-only and undoing an undo is
    /// a redo. Rewritten files (U2-3) are restored byte-exactly via their
    /// recorded pre-op hashes, conflict-guarded per file.
    pub fn undo_structural(
        &self,
        op_id: i64,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        use crate::structural::{StructuralOpKind, StructuralOpPayload};
        let (kind, payload) = {
            let conn = self.conn.lock().expect("session connection mutex");
            let (max_id, kind_str, payload_json): (i64, String, String) = conn
                .query_row(
                    "SELECT id, kind, payload FROM structural_ops ORDER BY id DESC LIMIT 1",
                    [],
                    |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
                )
                .map_err(|_| VaultError::InvalidArgument {
                    message: "no structural operations to undo".into(),
                })?;
            if max_id != op_id {
                return Err(VaultError::InvalidArgument {
                    message: format!(
                        "only the latest structural op is undoable (latest is {max_id}, got {op_id})"
                    ),
                });
            }
            let kind =
                StructuralOpKind::parse(&kind_str).ok_or_else(|| VaultError::InvalidArgument {
                    message: format!("unknown structural op kind {kind_str:?}"),
                })?;
            if !kind.undoable() {
                return Err(VaultError::InvalidArgument {
                    message: format!(
                        "{} is not undoable (the content is in the system trash)",
                        kind.as_str()
                    ),
                });
            }
            let payload = StructuralOpPayload::from_json(&payload_json).ok_or_else(|| {
                VaultError::InvalidArgument {
                    message: "corrupt structural journal payload".into(),
                }
            })?;
            (kind, payload)
            // Mutex drops here: the inverse op below re-acquires it through
            // the public mutation, which owns full validation + journaling.
        };

        let mut report = match kind {
            StructuralOpKind::CreateFolder => {
                // Inverse: remove the folder iff still empty.
                let conn = self.conn.lock().expect("session connection mutex");
                let (lo, hi) = subtree_bounds(&payload.from).expect("non-root folder path");
                let occupied: i64 = conn.query_row(
                    "SELECT (SELECT COUNT(*) FROM files WHERE path >= ?1 AND path < ?2)
                          + (SELECT COUNT(*) FROM dirs  WHERE path >= ?1 AND path < ?2)",
                    rusqlite::params![lo, hi],
                    |row| row.get(0),
                )?;
                if occupied > 0 {
                    return Err(VaultError::InvalidArgument {
                        message: format!(
                            "cannot undo create_folder: {:?} is no longer empty",
                            payload.from
                        ),
                    });
                }
                self.provider.delete(&payload.from)?;
                self.with_structural_tx(conn, |tx| {
                    tx.execute(
                        "DELETE FROM dirs WHERE path = ?1",
                        rusqlite::params![payload.from],
                    )?;
                    journal_append(
                        tx,
                        StructuralOpKind::DeleteFolder,
                        &StructuralOpPayload {
                            from: payload.from.clone(),
                            to: payload.from.clone(),
                            ..Default::default()
                        },
                    )
                })
                .map(|new_op| crate::structural::StructuralReport {
                    op_id: new_op,
                    moved: Vec::new(),
                    rewritten: Vec::new(),
                    failed: Vec::new(),
                })?
            }
            StructuralOpKind::RenameFolder | StructuralOpKind::MoveFolder => {
                self.structural_move_folder(&payload.to, &payload.from, kind, false)?
            }
            StructuralOpKind::RenameFile | StructuralOpKind::MoveFile => {
                self.structural_move_file(&payload.to, &payload.from, kind, false)?
            }
            StructuralOpKind::DeleteFile | StructuralOpKind::DeleteFolder => {
                unreachable!("undoable() filtered deletes above")
            }
        };

        // Restore U2-3 rewrites byte-exactly: each rewritten file goes back
        // to its pre-op bytes via the per-file op-log, guarded by the
        // recorded post-op hash so an external edit since the op surfaces
        // as a per-file WriteConflict in the report, never a clobber.
        //
        // Rewrites are journaled under POST-move paths; the inverse move
        // above already relocated moved files back, so map each restore
        // target through the reverse mapping (census-found: rewritten
        // MOVED sources otherwise fail their restore at a stale path).
        let reverse: std::collections::HashMap<&str, &str> = payload
            .moved
            .iter()
            .map(|(old, new)| (new.as_str(), old.as_str()))
            .collect();
        for rewrite in &payload.rewrites {
            let restore_path = reverse
                .get(rewrite.path.as_str())
                .copied()
                .unwrap_or(rewrite.path.as_str());
            match self.restore_file_to_hash(restore_path, &rewrite.hash_before, &rewrite.hash_after)
            {
                Ok(()) => report.rewritten.push(rewrite.clone()),
                Err(VaultError::WriteConflict { .. }) => {
                    report.failed.push(crate::structural::RewriteFailure {
                        path: rewrite.path.clone(),
                        kind: crate::structural::RewriteFailureKind::WriteConflict,
                    })
                }
                Err(other) => report.failed.push(crate::structural::RewriteFailure {
                    path: rewrite.path.clone(),
                    kind: crate::structural::RewriteFailureKind::Other(other.to_string()),
                }),
            }
        }
        // #802: the inverse moves above bypass the public wrappers —
        // emit their per-file Renamed events here. (Rewrite restores
        // ride `save_text_locked`, the shared seam, and emit
        // themselves.)
        self.notify_moved(&report);
        Ok(report)
    }

    // ----- internals -----

    fn structural_move_folder(
        &self,
        from: &str,
        to: &str,
        kind: crate::structural::StructuralOpKind,
        plan_rewrites: bool,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        validate_save_path(from)?;
        validate_save_path(to)?;
        if to == from {
            return Err(VaultError::InvalidArgument {
                message: "destination equals source".into(),
            });
        }
        if to.starts_with(&format!("{from}/")) {
            return Err(VaultError::InvalidArgument {
                message: "cannot move a folder into its own subtree".into(),
            });
        }
        let conn = self.conn.lock().expect("session connection mutex");
        let dir_exists: bool = conn
            .query_row(
                "SELECT 1 FROM dirs WHERE path = ?1",
                rusqlite::params![from],
                |_| Ok(true),
            )
            .unwrap_or(false);
        if !dir_exists {
            return Err(VaultError::InvalidPath {
                path: from.to_string(),
                reason: "no such folder in the index".into(),
            });
        }
        if let Some(existing) = index_entry_case_insensitive(&conn, to)? {
            return Err(VaultError::DestinationExists { path: existing });
        }

        // Collect the per-file mapping BEFORE mutating anything.
        let (lo, hi) = subtree_bounds(from).expect("non-root folder path");
        let moved: Vec<(String, String)> = {
            let mut stmt = conn
                .prepare("SELECT path FROM files WHERE path >= ?1 AND path < ?2 ORDER BY path")?;
            let rows = stmt.query_map(rusqlite::params![lo, hi], |row| row.get::<_, String>(0))?;
            let mut out = Vec::new();
            for row in rows {
                let old = row?;
                let new = format!("{to}{}", &old[from.len()..]);
                out.push((old, new));
            }
            out
        };

        self.provider.rename(from, to)?;
        self.finish_structural_move(conn, kind, from, to, moved, plan_rewrites, |tx| {
            rename_prefix_in_index(tx, from, to)
        })
    }

    fn structural_move_file(
        &self,
        from: &str,
        to: &str,
        kind: crate::structural::StructuralOpKind,
        plan_rewrites: bool,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        validate_save_path(from)?;
        validate_save_path(to)?;
        if to == from {
            return Err(VaultError::InvalidArgument {
                message: "destination equals source".into(),
            });
        }
        let conn = self.conn.lock().expect("session connection mutex");
        let file_exists: bool = conn
            .query_row(
                "SELECT 1 FROM files WHERE path = ?1",
                rusqlite::params![from],
                |_| Ok(true),
            )
            .unwrap_or(false);
        if !file_exists {
            return Err(VaultError::InvalidPath {
                path: from.to_string(),
                reason: "no such file in the index".into(),
            });
        }
        if let Some(existing) = index_entry_case_insensitive(&conn, to)? {
            return Err(VaultError::DestinationExists { path: existing });
        }

        let (_, old_extension, old_is_markdown) = classify_path(from);
        let old_is_base = old_extension.as_deref() == Some("base");
        let provider = Arc::clone(&self.provider);
        let parser_version = self.config.parser_version;
        let large_file_refuse_bytes = self.config.large_file_refuse_bytes;
        self.provider.rename(from, to)?;
        let moved = vec![(from.to_string(), to.to_string())];
        self.finish_structural_move(conn, kind, from, to, moved, plan_rewrites, move |tx| {
            let (name, extension, is_markdown) = classify_path(to);
            let is_base = extension.as_deref() == Some("base");
            if old_is_base || is_base || old_is_markdown != is_markdown {
                tx.execute(
                    "UPDATE files SET path = ?1, name = ?2, extension = ?3
                     WHERE path = ?4",
                    rusqlite::params![to, name, extension, from],
                )?;
                let file_id: i64 = tx.query_row(
                    "SELECT id FROM files WHERE path = ?1",
                    rusqlite::params![to],
                    |row| row.get(0),
                )?;
                refresh_text_derived_indexes_after_reclassification(
                    tx,
                    provider.as_ref(),
                    file_id,
                    to,
                    &name,
                    extension.as_deref(),
                    is_markdown,
                    parser_version,
                    large_file_refuse_bytes,
                )?;
            } else {
                tx.execute(
                    "UPDATE files SET path = ?1, name = ?2, extension = ?3, is_markdown = ?4
                     WHERE path = ?5",
                    rusqlite::params![to, name, extension, is_markdown as i64, from],
                )?;
            }
            Ok(())
        })
    }

    /// Shared tail of every move/rename (U2-2 index update + U2-3 link
    /// integrity, #461): with the fs op already done and the connection
    /// lock held —
    ///
    ///   tx1: `update_index` (path renames) + inbound `links.target_path`
    ///        column updates + unresolved-link re-resolution (arrivals
    ///        heal) — the atomic "move" as far as the index is concerned;
    ///   then: per-file link-text rewrites through the standard save path
    ///        (each file atomic + op-logged; failures reported per file,
    ///        never aborting the move — the rename-property discipline);
    ///   tx2: journal append carrying the applied rewrites, so
    ///        `undo_structural` can restore them byte-exactly.
    ///
    /// tx1 failure reverts the fs op best-effort (no silently split state).
    #[allow(clippy::too_many_arguments)] // cohesive move-tail; a param
    // struct would only relocate the argument list
    fn finish_structural_move(
        &self,
        mut conn: std::sync::MutexGuard<'_, Connection>,
        kind: crate::structural::StructuralOpKind,
        from: &str,
        to: &str,
        moved: Vec<(String, String)>,
        plan_rewrites: bool,
        update_index: impl FnOnce(&rusqlite::Transaction) -> Result<(), VaultError>,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        let tx1 = (|| -> Result<(), VaultError> {
            let tx = conn.transaction()?;
            update_index(&tx)?;
            {
                let mut stmt =
                    tx.prepare("UPDATE links SET target_path = ?1 WHERE target_path = ?2")?;
                for (old, new) in &moved {
                    stmt.execute(rusqlite::params![new, old])?;
                }
            }
            crate::links_db::re_resolve_unresolved_links(&tx)?;
            tx.commit()?;
            Ok(())
        })();
        if let Err(e) = tx1 {
            let _ = self.provider.rename(to, from);
            return Err(e);
        }

        // Re-path each moved file's history (O-1 #539): a pure
        // `PathChanged` marker per file, appended before any rewrite
        // entries so a log tells its story in order — "renamed, then
        // its links were rewritten". The oplog_name binding column is
        // untouched by moves (stems are path-independent); the marker
        // exists so a log orphaned by a LATER cache rebuild or delete
        // still knows its final vault path.
        for (old, new) in &moved {
            self.append_path_changed_marker(&conn, old, new);
        }

        // Undo inverts the move and restores the FORWARD rewrites from the
        // journal — it must never plan NEW rewrites (the reverse pass would
        // both break byte-identity and invalidate the hash-guarded
        // restores; census-found, seed 0).
        let (rewritten, failed) = if plan_rewrites {
            self.apply_link_rewrites(&mut conn, &moved)
        } else {
            (Vec::new(), Vec::new())
        };

        let op_id = {
            let tx = conn.transaction()?;
            let id = journal_append(
                &tx,
                kind,
                &crate::structural::StructuralOpPayload {
                    from: from.to_string(),
                    to: to.to_string(),
                    moved: moved.clone(),
                    rewrites: rewritten.clone(),
                    oplog_name: None,
                },
            )?;
            tx.commit()?;
            id
        };
        self.bump_bases_generation();
        Ok(crate::structural::StructuralReport {
            op_id,
            moved,
            rewritten,
            failed,
        })
    }

    /// Append a pure `PathChanged` marker to a just-moved file's log
    /// (O-1 #539): kind-4 wrapping an **empty batch**, both hashes set
    /// to the log's current **tail hash**.
    ///
    /// Marker hash rule (normative — protects version identity): the
    /// marker's `hash_before == hash_after` MUST be the last entry's
    /// `hash_after` — NEVER the index's/disk's current hash, which can
    /// differ after an external edit and would introduce a `hash_after`
    /// whose prefix reconstruction is not that hash's content. Skipped
    /// when the file has no log or the log is empty: there is no
    /// history to re-path.
    ///
    /// The tail read + marker append are ONE atomic op-log operation
    /// under the per-log file lock
    /// ([`crate::oplog::append_path_changed_marker`]) — a
    /// session-cached tail or an unlocked read could race a concurrent
    /// writer (another process: the `slate` CLI) and stamp a stale
    /// hash, breaking the identity axiom (adversarial-review finding).
    /// The in-memory append state is left untouched: the tail hash is
    /// unchanged by construction, so the next save still chains (the
    /// ~100 marker bytes are deliberately not counted into the
    /// snapshot cadence — noise next to the 256-byte framing
    /// estimate). Best-effort like every op-log write.
    fn append_path_changed_marker(&self, conn: &Connection, from: &str, to: &str) {
        let row: Option<(i64, Option<String>)> = conn
            .query_row(
                "SELECT id, oplog_name FROM files WHERE path = ?1",
                rusqlite::params![to],
                |row| Ok((row.get(0)?, row.get(1)?)),
            )
            .optional()
            .ok()
            .flatten();
        let Some((file_id, Some(log_name))) = row else {
            return; // unindexed, or never saved through Slate — no log
        };

        if let Err(e) = crate::oplog::append_path_changed_marker(
            &self.config.cache_dir,
            &log_name,
            from,
            to,
            &self.config.user_actor_id,
            now_ms(),
        ) {
            log::warn!(
                "oplog path marker append failed for file_id={file_id}: {}",
                e.kind()
            );
            log::debug!("oplog path marker append failure for path {to:?}: {e}");
        }
    }

    /// U2-3 (#461): plan and apply the link-text rewrites a move demands.
    /// Referential stability — every link that resolved to file F before
    /// still resolves to F, byte-minimal edits only; the planner
    /// (`crate::link_rewrite`) decides, this method applies.
    fn apply_link_rewrites(
        &self,
        conn: &mut std::sync::MutexGuard<'_, Connection>,
        moved: &[(String, String)],
    ) -> (
        Vec<crate::structural::RewriteOutcome>,
        Vec<crate::structural::RewriteFailure>,
    ) {
        use crate::structural::{RewriteFailure, RewriteFailureKind, RewriteOutcome};
        let mapping = crate::link_rewrite::MoveMapping::new(moved.iter().cloned());
        if mapping.is_empty() {
            return (Vec::new(), Vec::new());
        }

        // This path is best-effort by design (the move already stands): a
        // DB error here must never panic the process (Codoki #503). It
        // surfaces LOUDLY instead — an empty rewrite set plus a sweep-level
        // failure entry, so the report can't read as "nothing needed
        // rewriting".
        let (index, candidates) = match collect_rewrite_candidates(conn, moved) {
            Ok(pair) => pair,
            Err(e) => {
                return (
                    Vec::new(),
                    vec![RewriteFailure {
                        path: "*".to_string(),
                        kind: RewriteFailureKind::Other(format!(
                            "link-rewrite candidate sweep failed: {e}"
                        )),
                    }],
                );
            }
        };

        let mut rewritten = Vec::new();
        let mut failed = Vec::new();
        for path in candidates {
            let (text, hash_before) = match self.read_text(&path) {
                Ok(text) => {
                    let hash = crate::vault::content_hash(text.as_bytes());
                    (text, hash)
                }
                Err(e) => {
                    failed.push(RewriteFailure {
                        path,
                        kind: RewriteFailureKind::Other(e.to_string()),
                    });
                    continue;
                }
            };
            let edits =
                crate::link_rewrite::plan_rewrites_for_source(&path, &text, &mapping, &index);
            if edits.is_empty() {
                continue;
            }
            // Anchor the PRE-rewrite bytes in the op-log before rewriting:
            // scan-indexed files have no log entries yet, so without this
            // anchor `undo_structural`'s reconstruct-at-hash cannot reach
            // the pre-op state (census-found, seed 0). hash_before ==
            // hash_after marks a pure anchor; the state map is updated so
            // the following save appends its EditBatch against it.
            self.anchor_oplog_snapshot(conn, &path, &text, &hash_before);
            let new_text = crate::link_rewrite::apply_edits(&text, &edits);
            match self.save_text_locked(conn, &path, &new_text, Some(&hash_before), &[]) {
                Ok(report) => rewritten.push(RewriteOutcome {
                    path,
                    hash_before,
                    hash_after: report.new_content_hash,
                }),
                Err(VaultError::WriteConflict { .. }) => failed.push(RewriteFailure {
                    path,
                    kind: RewriteFailureKind::WriteConflict,
                }),
                Err(e) => failed.push(RewriteFailure {
                    path,
                    kind: RewriteFailureKind::Other(e.to_string()),
                }),
            }
        }

        // `.canvas` participation (Milestone T #366, program gate G24):
        // file cards reference notes by vault path, so a note move must
        // rewrite those references or canvases silently rot. Same
        // discipline as the markdown loop above: best-effort, every
        // outcome or failure lands in the structural report — never
        // silent. Serialization is per-field (#366), so only the
        // changed `file` values are touched on disk.
        self.rewrite_canvas_references(conn, &mapping, &mut rewritten, &mut failed);

        (rewritten, failed)
    }

    /// Rewrite `file`-card paths in every canvas that references a
    /// moved file. Degraded canvases are skipped (unwritable by the
    /// #359 contract); references inside skipped/unmodelable entries
    /// can't be rewritten and stay exactly as the user wrote them.
    fn rewrite_canvas_references(
        &self,
        conn: &mut std::sync::MutexGuard<'_, Connection>,
        mapping: &crate::link_rewrite::MoveMapping,
        rewritten: &mut Vec<crate::structural::RewriteOutcome>,
        failed: &mut Vec<crate::structural::RewriteFailure>,
    ) {
        use crate::structural::{RewriteFailure, RewriteFailureKind, RewriteOutcome};

        let canvas_paths: Vec<String> = {
            let result = conn
                .prepare("SELECT path FROM files WHERE extension = 'canvas' ORDER BY path")
                .and_then(|mut stmt| {
                    stmt.query_map([], |row| row.get::<_, String>(0))
                        .and_then(std::iter::Iterator::collect)
                });
            match result {
                Ok(paths) => paths,
                Err(e) => {
                    failed.push(RewriteFailure {
                        path: "*.canvas".to_string(),
                        kind: RewriteFailureKind::Other(format!(
                            "canvas rewrite sweep failed: {e}"
                        )),
                    });
                    return;
                }
            }
        };

        for path in canvas_paths {
            let (text, hash_before) = match self.read_text(&path) {
                Ok(text) => {
                    let hash = crate::vault::content_hash(text.as_bytes());
                    (text, hash)
                }
                Err(e) => {
                    failed.push(RewriteFailure {
                        path,
                        kind: RewriteFailureKind::Other(e.to_string()),
                    });
                    continue;
                }
            };
            let (mut canvas, warnings) = crate::canvas::parse(&text);
            if crate::canvas::is_load_degraded(&warnings) {
                continue; // unwritable; nothing modelable to rewrite
            }
            let mut changed = false;
            for node in &mut canvas.nodes {
                if let crate::canvas::NodeKind::File { file, .. } = &mut node.kind {
                    let new_path = mapping.new_path_of(file).to_string();
                    if new_path != *file {
                        *file = new_path;
                        changed = true;
                    }
                }
            }
            if !changed {
                continue;
            }
            self.anchor_oplog_snapshot(conn, &path, &text, &hash_before);
            let new_text = crate::canvas::serialize::serialize(&canvas);
            match self.save_text_locked(conn, &path, &new_text, Some(&hash_before), &[]) {
                Ok(report) => rewritten.push(RewriteOutcome {
                    path,
                    hash_before,
                    hash_after: report.new_content_hash,
                }),
                Err(VaultError::WriteConflict { .. }) => failed.push(RewriteFailure {
                    path,
                    kind: RewriteFailureKind::WriteConflict,
                }),
                Err(e) => failed.push(RewriteFailure {
                    path,
                    kind: RewriteFailureKind::Other(e.to_string()),
                }),
            }
        }
    }

    /// Append a pure WholeFileReplace anchor of `contents` (current disk
    /// state, hash `content_hash`) to `path`'s op-log, and align the append
    /// state so the next save chains onto it. Failures are logged, not
    /// fatal: a missing anchor degrades an eventual undo of THIS file to a
    /// per-file conflict report, never corruption.
    fn anchor_oplog_snapshot(
        &self,
        conn: &std::sync::MutexGuard<'_, Connection>,
        path: &str,
        contents: &str,
        content_hash: &str,
    ) {
        let file_id: Option<i64> = conn
            .query_row(
                "SELECT id FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get(0),
            )
            .ok();
        let Some(file_id) = file_id else { return };
        let Some(log_name) = self.ensure_oplog_name(conn, file_id, path) else {
            return; // ensure_oplog_name already warned
        };
        let entry = crate::oplog::OpLogEntry {
            timestamp_ms: now_ms(),
            user_actor_id: self.config.user_actor_id.clone(),
            op_kind: crate::oplog::OpKind::WholeFileReplace,
            content_hash_before: content_hash.to_string(),
            content_hash_after: content_hash.to_string(),
            payload_bytes: contents.as_bytes().to_vec(),
        };
        if let Err(e) = crate::oplog::append_entry(&self.config.cache_dir, &log_name, path, &entry)
        {
            // Non-fatal: a missing anchor only degrades a structural-move's
            // undo to a per-file conflict report, never corruption. Route
            // through the facade (#507). warn carries only the file id and
            // the error *kind* — never the full Display, which for a
            // torn/short existing log embeds the cache path. The path-bearing
            // detail rides the debug line (see the lib.rs privacy rule).
            log::warn!("oplog anchor failed for file_id={file_id}: {}", e.kind());
            log::debug!("oplog anchor failure for path {path:?}: {e}");
            return;
        }
        let mut state = self.oplog_state.lock().expect("oplog state mutex");
        state.insert(
            file_id,
            OplogAppendState {
                last_hash_after: content_hash.to_string(),
                bytes_since_snapshot: 0,
                compaction_futile: false,
                appends_this_session: 1,
            },
        );
    }

    /// Run `body` inside one transaction on the already-locked connection.
    fn with_structural_tx<T>(
        &self,
        mut conn: std::sync::MutexGuard<'_, Connection>,
        body: impl FnOnce(&rusqlite::Transaction) -> Result<T, VaultError>,
    ) -> Result<T, VaultError> {
        let tx = conn.transaction()?;
        let out = body(&tx)?;
        tx.commit()?;
        Ok(out)
    }

    /// Restore `path` to the exact bytes whose hash is `hash_before`, via
    /// the per-file op-log, conflict-guarded on `expected_current`.
    fn restore_file_to_hash(
        &self,
        path: &str,
        hash_before: &str,
        expected_current: &str,
    ) -> Result<(), VaultError> {
        let mut conn = self.conn.lock().expect("session connection mutex");
        // Inline binding lookup (NOT the public read_oplog — it takes the
        // mutex this method already holds).
        let log_name: Option<Option<String>> = conn
            .query_row(
                "SELECT oplog_name FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get(0),
            )
            .optional()?;
        let Some(log_name) = log_name else {
            return Err(VaultError::InvalidPath {
                path: path.to_string(),
                reason: "no such file in the index".into(),
            });
        };
        let entries = match log_name {
            Some(name) => {
                crate::oplog::read_oplog(&self.config.cache_dir, &name).map_err(VaultError::Io)?
            }
            None => Vec::new(),
        };
        let contents =
            crate::oplog::reconstruct_at_hash(&entries, hash_before).ok_or_else(|| {
                VaultError::InvalidArgument {
                    message: format!("op-log for {path:?} has no state with hash {hash_before}"),
                }
            })?;
        self.save_text_locked(&mut conn, path, &contents, Some(expected_current), &[])
            .map(|_| ())
    }
}

/// Post-move index + candidate set for the link-rewrite pass (U2-3).
/// Candidates: any source with an internal link whose `target_raw` STEM
/// matches a moved file's stem (census-found over-approximation — a move
/// can flip bystander basename links by proximity; over-inclusion costs a
/// plan, never an edit) ∪ moved markdown files that carry links.
fn collect_rewrite_candidates(
    conn: &Connection,
    moved: &[(String, String)],
) -> Result<
    (
        crate::InMemoryVaultIndex,
        std::collections::BTreeSet<String>,
    ),
    VaultError,
> {
    let all_paths: Vec<String> = {
        let mut stmt = conn.prepare("SELECT path FROM files")?;
        let rows = stmt.query_map([], |row| row.get::<_, String>(0))?;
        rows.collect::<Result<Vec<_>, _>>()?
    };
    let stem_of = |s: &str| -> String {
        let name = s.rsplit('/').next().unwrap_or(s);
        let mut stem = name;
        for ext in [".md", ".markdown", ".mdown", ".mkd"] {
            if let Some(candidate) = stem.strip_suffix(ext) {
                stem = candidate;
                break;
            }
        }
        stem.to_lowercase()
    };
    let moved_stems: std::collections::HashSet<String> =
        moved.iter().map(|(old, _)| stem_of(old)).collect();
    let mut candidates = std::collections::BTreeSet::new();
    {
        let mut stmt = conn.prepare(
            "SELECT DISTINCT f.path, l.target_raw FROM links l
             JOIN files f ON f.id = l.source_file_id
             WHERE l.is_external = 0",
        )?;
        let rows = stmt.query_map([], |row| {
            Ok((row.get::<_, String>(0)?, row.get::<_, String>(1)?))
        })?;
        for row in rows {
            let (source, target_raw) = row?;
            if moved_stems.contains(&stem_of(&target_raw)) {
                candidates.insert(source);
            }
        }
    }
    {
        let mut stmt = conn.prepare(
            "SELECT f.path FROM files f
             WHERE f.path = ?1 AND f.is_markdown = 1
               AND EXISTS (SELECT 1 FROM links l WHERE l.source_file_id = f.id)",
        )?;
        for (_, new) in moved {
            let rows = stmt.query_map(rusqlite::params![new], |row| row.get::<_, String>(0))?;
            for row in rows {
                candidates.insert(row?);
            }
        }
    }
    Ok((crate::InMemoryVaultIndex::new(all_paths), candidates))
}

/// Final path component (files or folders; input is a validated relative
/// path, so the component is non-empty).
fn leaf_name(path: &str) -> &str {
    path.rsplit('/').next().unwrap_or(path)
}

/// The path of `path`'s sibling named `new_name` (rename-in-place).
fn sibling_path(path: &str, new_name: &str) -> Result<String, VaultError> {
    validate_leaf_component(new_name)?;
    Ok(match path.rfind('/') {
        Some(idx) => format!("{}/{new_name}", &path[..idx]),
        None => new_name.to_string(),
    })
}

/// The path of `leaf` placed under `parent` ("" = vault root).
fn child_path(parent: &str, leaf: &str) -> Result<String, VaultError> {
    if !parent.is_empty() {
        validate_save_path(parent)?;
    }
    validate_leaf_component(leaf)?;
    Ok(if parent.is_empty() {
        leaf.to_string()
    } else {
        format!("{parent}/{leaf}")
    })
}

/// Single non-empty component, no separators, not dot-prefixed, and not
/// the reserved `.slate` (redundant with dot-prefix, kept for the explicit
/// error the spec names).
fn validate_leaf_component(name: &str) -> Result<(), VaultError> {
    if name.is_empty() {
        return Err(VaultError::InvalidArgument {
            message: "name must not be empty".into(),
        });
    }
    if name.contains('/') || name.contains('\\') {
        return Err(VaultError::InvalidArgument {
            message: "name must be a single path component".into(),
        });
    }
    if name.starts_with('.') {
        return Err(VaultError::InvalidArgument {
            message: "dot-prefixed names are reserved".into(),
        });
    }
    Ok(())
}

/// Case-insensitive existence check across BOTH index tables (APFS default
/// is case-insensitive; a differing-case collision would shadow on disk).
/// Returns the existing entry's exact path for the error message.
fn index_entry_case_insensitive(
    conn: &Connection,
    path: &str,
) -> Result<Option<String>, VaultError> {
    let lowered = path.to_lowercase();
    let hit: Option<String> = conn
        .query_row(
            "SELECT path FROM files WHERE lower(path) = ?1
             UNION ALL
             SELECT path FROM dirs WHERE lower(path) = ?1
             LIMIT 1",
            rusqlite::params![lowered],
            |row| row.get(0),
        )
        .ok();
    Ok(hit)
}

/// Rename every index row under `from` (files + dirs + the dir row itself)
/// to live under `to`, preserving ids. Range-scan + per-row UPDATE — the
/// U2-1 discipline (GLOB/LIKE are wildcard-unsafe against legal filenames).
fn rename_prefix_in_index(
    tx: &rusqlite::Transaction,
    from: &str,
    to: &str,
) -> Result<(), VaultError> {
    let (lo, hi) = subtree_bounds(from).expect("non-root folder path");

    let file_rows: Vec<(i64, String)> = {
        let mut stmt = tx.prepare("SELECT id, path FROM files WHERE path >= ?1 AND path < ?2")?;
        let rows = stmt.query_map(rusqlite::params![lo, hi], |row| {
            Ok((row.get::<_, i64>(0)?, row.get::<_, String>(1)?))
        })?;
        rows.collect::<Result<_, _>>()?
    };
    for (id, old_path) in file_rows {
        let new_path = format!("{to}{}", &old_path[from.len()..]);
        let (name, extension, is_markdown) = classify_path(&new_path);
        tx.execute(
            "UPDATE files SET path = ?1, name = ?2, extension = ?3, is_markdown = ?4
             WHERE id = ?5",
            rusqlite::params![new_path, name, extension, is_markdown as i64, id],
        )?;
    }

    let dir_rows: Vec<(i64, String)> = {
        let mut stmt =
            tx.prepare("SELECT id, path FROM dirs WHERE path = ?1 OR (path >= ?2 AND path < ?3)")?;
        let rows = stmt.query_map(rusqlite::params![from, lo, hi], |row| {
            Ok((row.get::<_, i64>(0)?, row.get::<_, String>(1)?))
        })?;
        rows.collect::<Result<_, _>>()?
    };
    for (id, old_path) in dir_rows {
        let new_path = if old_path == from {
            to.to_string()
        } else {
            format!("{to}{}", &old_path[from.len()..])
        };
        let name = leaf_name(&new_path).to_string();
        let parent = match new_path.rfind('/') {
            Some(idx) => new_path[..idx].to_string(),
            None => String::new(),
        };
        tx.execute(
            "UPDATE dirs SET path = ?1, parent_path = ?2, name = ?3 WHERE id = ?4",
            rusqlite::params![new_path, parent, name, id],
        )?;
    }
    Ok(())
}

/// Insert (or keep) a `dirs` row for `path`, plus any missing ancestor
/// rows (`create_dir` creates parents; the index must agree).
fn upsert_dir_row(tx: &rusqlite::Transaction, path: &str) -> Result<(), VaultError> {
    let mut ancestors: Vec<&str> = Vec::new();
    let mut end = path.len();
    loop {
        ancestors.push(&path[..end]);
        match path[..end].rfind('/') {
            Some(idx) => end = idx,
            None => break,
        }
    }
    for dir in ancestors.into_iter().rev() {
        let name = leaf_name(dir).to_string();
        let parent = match dir.rfind('/') {
            Some(idx) => dir[..idx].to_string(),
            None => String::new(),
        };
        tx.execute(
            "INSERT INTO dirs (path, parent_path, name) VALUES (?1, ?2, ?3)
             ON CONFLICT(path) DO NOTHING",
            rusqlite::params![dir, parent, name],
        )?;
    }
    Ok(())
}

/// Append one journal row; returns its id.
/// Build newest-first version rows from an in-order entry list
/// (O-3 #541). Pure — separately testable; display-only decode
/// failures degrade to zero-op rows rather than erroring (the
/// integrity-critical paths do their own verification).
fn version_summaries(entries: &[crate::oplog::OpLogEntry]) -> Vec<VersionSummary> {
    fn annotation_summary(ann: &crate::oplog::OpAnnotation) -> OpAnnotationSummary {
        use crate::oplog::OpAnnotation;
        match ann {
            OpAnnotation::SetProperty { key, .. } => OpAnnotationSummary {
                kind: "SetProperty".into(),
                display: format!("Set property '{key}'"),
            },
            OpAnnotation::RemoveProperty { key } => OpAnnotationSummary {
                kind: "RemoveProperty".into(),
                display: format!("Removed property '{key}'"),
            },
            OpAnnotation::ToggleTask { new_status, .. } => OpAnnotationSummary {
                kind: "ToggleTask".into(),
                display: match new_status {
                    'x' | 'X' => "Completed task".to_string(),
                    ' ' => "Reopened task".to_string(),
                    other => format!("Changed task status to '{other}'"),
                },
            },
            OpAnnotation::FrontmatterReplace => OpAnnotationSummary {
                kind: "FrontmatterReplace".into(),
                display: "Edited properties as source".into(),
            },
            OpAnnotation::PathChanged { from, to } => OpAnnotationSummary {
                kind: "PathChanged".into(),
                display: format!("Renamed from {from} to {to}"),
            },
        }
    }
    fn size_phrase(delta: i64) -> String {
        match delta.cmp(&0) {
            std::cmp::Ordering::Greater => format!("{delta} bytes added"),
            std::cmp::Ordering::Less => format!("{} bytes removed", -delta),
            std::cmp::Ordering::Equal => "no size change".to_string(),
        }
    }

    let mut rows: Vec<VersionSummary> = Vec::with_capacity(entries.len());
    // Forward length tracking: batch deltas are computable from the
    // ops alone; snapshots reset the running length.
    let mut prev_len: Option<i64> = None;
    // #797: the previous in-order entry's (before, after) pair, for
    // folding a canvas action's semantic record into its byte row.
    let mut prev_pair: Option<(String, String)> = None;
    for (idx, entry) in entries.iter().enumerate() {
        use crate::oplog::OpKind;
        let (inner_kind, inner_payload, annotations) = match entry.op_kind {
            OpKind::Annotated => match crate::oplog::decode_annotated(&entry.payload_bytes) {
                Ok((kind, payload, anns)) => (
                    kind,
                    Some(payload),
                    anns.iter().map(annotation_summary).collect(),
                ),
                // Display-only degradation for an undecodable wrapper.
                Err(_) => (OpKind::Annotated, None, Vec::new()),
            },
            kind => (kind, Some(entry.payload_bytes.clone()), Vec::new()),
        };

        // #797: a committed canvas action journals TWO entries for ONE
        // transition — the byte-level save the seam wrote, then the
        // semantic CanvasApply record beside it (T #372). One
        // transition = one version row: fold the semantic record into
        // its byte row as an annotation carrying the action name. A
        // standalone record (its byte entry compacted away) keeps
        // today's own-row rendering.
        // Fold eligibility (Codoki on #865, both rounds): the target
        // must be an actual BYTE-level row — WholeFileReplace or
        // EditBatch by allowlist, so an undecodable Annotated row or a
        // standalone CanvasApply row can never absorb — non-marker (a
        // no-op action's record must not vanish into a default-hidden
        // anchor row), and not already annotated (the T protocol
        // appends exactly one record per action; a second match is an
        // anomaly that should stay a visible row).
        let fold_target = |row: &VersionSummary| {
            matches!(row.op_kind, OpKind::WholeFileReplace | OpKind::EditBatch)
                && !row.is_marker
                && !row.annotations.iter().any(|a| a.kind == "CanvasAction")
        };
        if inner_kind == OpKind::CanvasApply
            && prev_pair.as_ref()
                == Some(&(
                    entry.content_hash_before.clone(),
                    entry.content_hash_after.clone(),
                ))
            && let Some(last) = rows.last_mut()
            && fold_target(last)
        {
            let name = inner_payload
                .as_deref()
                .and_then(|p| serde_json::from_slice::<serde_json::Value>(p).ok())
                .and_then(|v| v.get("name").and_then(|n| n.as_str()).map(str::to_string))
                .unwrap_or_else(|| "canvas action".to_string());
            last.annotations.push(OpAnnotationSummary {
                kind: "CanvasAction".into(),
                display: format!("Canvas: {name}"),
            });
            prev_pair = Some((
                entry.content_hash_before.clone(),
                entry.content_hash_after.clone(),
            ));
            continue;
        }

        let (op_count, byte_delta) = match inner_kind {
            OpKind::WholeFileReplace => {
                let new_len = inner_payload.as_ref().map_or(0, |p| p.len() as i64);
                let delta = new_len - prev_len.unwrap_or(0);
                prev_len = Some(new_len);
                (1u32, delta)
            }
            OpKind::EditBatch => {
                let ops = inner_payload
                    .as_deref()
                    .and_then(|p| crate::oplog::decode_edit_batch(p).ok())
                    .unwrap_or_default();
                let delta: i64 = ops
                    .iter()
                    .map(|op| match op {
                        crate::oplog::EditOp::Insert { text, .. } => text.len() as i64,
                        crate::oplog::EditOp::Delete { start, end } => -((end - start) as i64),
                        crate::oplog::EditOp::Replace { start, end, text } => {
                            text.len() as i64 - (end - start) as i64
                        }
                    })
                    .sum();
                if let Some(len) = prev_len.as_mut() {
                    *len += delta;
                }
                (ops.len() as u32, delta)
            }
            // Semantic records change no text.
            OpKind::CanvasApply | OpKind::Annotated => (0u32, 0i64),
        };
        let is_marker = entry.content_hash_before == entry.content_hash_after;
        let audio_fragment = match inner_kind {
            OpKind::WholeFileReplace if is_marker => "anchor snapshot".to_string(),
            OpKind::WholeFileReplace => {
                format!("snapshot, {} bytes", prev_len.unwrap_or(0))
            }
            OpKind::EditBatch if op_count == 0 => "marker".to_string(),
            OpKind::EditBatch => format!(
                "{op_count} operation{}, {}",
                if op_count == 1 { "" } else { "s" },
                size_phrase(byte_delta)
            ),
            OpKind::CanvasApply => "canvas action".to_string(),
            OpKind::Annotated => "unreadable entry".to_string(),
        };
        prev_pair = Some((
            entry.content_hash_before.clone(),
            entry.content_hash_after.clone(),
        ));
        rows.push(VersionSummary {
            position_from_tail: (entries.len() - 1 - idx) as u32,
            content_hash_after: entry.content_hash_after.clone(),
            timestamp_ms: entry.timestamp_ms,
            op_kind: inner_kind,
            op_count,
            byte_delta,
            annotations,
            is_marker,
            audio_fragment,
        });
    }
    rows.reverse(); // newest first
    rows
}

fn journal_append(
    tx: &rusqlite::Transaction,
    kind: crate::structural::StructuralOpKind,
    payload: &crate::structural::StructuralOpPayload,
) -> Result<i64, VaultError> {
    tx.execute(
        "INSERT INTO structural_ops (timestamp_ms, kind, payload) VALUES (?1, ?2, ?3)",
        rusqlite::params![now_ms(), kind.as_str(), payload.to_json()],
    )?;
    Ok(tx.last_insert_rowid())
}

// ---------------------------------------------------------------------------
// Bases API (Milestone N, #699). Handle-based, matching the canvas family.

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BaseFileSummary {
    pub path: String,
    pub name: String,
    pub view_count: u32,
    pub warning_count: u32,
    pub degraded: bool,
    pub indexed_at_ms: i64,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BaseViewSummary {
    pub name: String,
    pub view_type: String,
    pub source: String,
    pub status: BaseViewStatus,
    /// JSON-encoded `slate.*` view state from the `.base` file. `None` means
    /// the view has no Slate state; callers should not treat an empty string as
    /// an equivalent sentinel.
    pub slate_state_json: Option<String>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BaseViewStatus {
    Executable,
    Fallback,
    Error,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ColumnRole {
    Primary,
    Identifier,
    Metadata,
    Metric,
    Action,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ExportFormat {
    Csv,
    Markdown,
}

#[derive(Debug, Clone, PartialEq)]
pub struct BasesColumn {
    pub id: String,
    pub label: String,
    pub value_kind: String,
    pub role: ColumnRole,
}

#[derive(Debug, Clone, PartialEq)]
pub struct BasesValue {
    pub raw_kind: String,
    pub sort_key: String,
    pub display: String,
    pub text: Option<String>,
    pub number: Option<f64>,
    pub bool_value: Option<bool>,
    pub date_epoch_ms: Option<i64>,
    pub date_has_time: bool,
    pub link_target: Option<String>,
    pub link_display: Option<String>,
    pub list: Vec<String>,
    pub error: Option<String>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct BasesRow {
    pub file_path: String,
    pub task_ordinal: Option<u64>,
    pub values: Vec<BasesValue>,
    pub audio_description: String,
}

#[derive(Debug, Clone, PartialEq)]
pub struct BasesGroup {
    pub label: String,
    pub row_start: u64,
    pub row_count: u64,
    pub summaries: Vec<BasesSummaryCell>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct BasesSummaryCell {
    pub column_id: String,
    pub summary: String,
    pub value: BasesValue,
}

#[derive(Debug, Clone, PartialEq)]
pub struct BasesResultSet {
    pub columns: Vec<BasesColumn>,
    pub rows: Vec<BasesRow>,
    pub groups: Vec<BasesGroup>,
    pub summaries: Vec<BasesSummaryCell>,
    pub total_count: u64,
    pub shown_count: u64,
    pub unfiltered_shown_count: u64,
    pub executed_at_ms: i64,
    pub warnings: Vec<String>,
    pub view_error: Option<String>,
    pub audio_summary: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SavedQuerySourceSyntax {
    Builder,
    Base,
    Dql,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SavedQuerySummary {
    pub id: String,
    pub name: String,
    pub description: Option<String>,
    pub source_syntax: SavedQuerySourceSyntax,
    pub created_at_ms: i64,
    pub modified_at_ms: i64,
    pub warning: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SavedQuery {
    pub id: String,
    pub name: String,
    pub description: Option<String>,
    pub query_json: String,
    pub source_syntax: SavedQuerySourceSyntax,
    pub created_at_ms: i64,
    pub modified_at_ms: i64,
    pub warning: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
pub struct DashboardSection {
    pub saved_query_id: String,
    pub heading_override: Option<String>,
    pub view_override: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DashboardSectionStatus {
    pub saved_query_id: String,
    pub saved_query_name: Option<String>,
    pub heading_override: Option<String>,
    pub view_override: Option<String>,
    pub missing: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DashboardSummary {
    pub id: String,
    pub name: String,
    pub section_count: u32,
    pub created_at_ms: i64,
    pub modified_at_ms: i64,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Dashboard {
    pub id: String,
    pub name: String,
    pub sections: Vec<DashboardSectionStatus>,
    pub created_at_ms: i64,
    pub modified_at_ms: i64,
}

fn bad_base_handle(handle: u64) -> VaultError {
    VaultError::InvalidArgument {
        message: format!("unknown base handle {handle} (closed or never opened)"),
    }
}

fn base_query_for_view(
    state: &OpenBaseState,
    view: u32,
) -> Result<&crate::bases::SlateQuery, VaultError> {
    match &state.source {
        OpenBaseSource::Base(_) => {
            state
                .queries
                .get(view as usize)
                .ok_or_else(|| VaultError::InvalidArgument {
                    message: format!("base view {view} is out of range"),
                })
        }
        OpenBaseSource::Query(query) => {
            if view != 0 {
                return Err(VaultError::InvalidArgument {
                    message: format!("query handle has one view; got {view}"),
                });
            }
            Ok(state.queries.first().unwrap_or(query))
        }
    }
}

fn compiled_base_queries(base: &crate::bases::BaseFile) -> Vec<crate::bases::SlateQuery> {
    (0..base.views.len())
        .map(|view| crate::bases::view_query(base, view))
        .collect()
}

fn base_edits_invalidate_transient_sort(
    transient_sort: Option<&(u32, crate::bases::SortKey)>,
    edits: &[crate::bases::BaseEdit],
) -> bool {
    let Some((sort_view, _)) = transient_sort else {
        return false;
    };
    edits.iter().any(|edit| match edit {
        // View indices are positional. Collection edits can change which view
        // a saved index identifies, so the contract clears rather than remaps.
        crate::bases::BaseEdit::AddView { .. } | crate::bases::BaseEdit::RemoveView { .. } => true,
        crate::bases::BaseEdit::SetViewKey { view, key, .. } => {
            *view as u32 == *sort_view && matches!(key.as_str(), "order" | "source" | "type")
        }
        crate::bases::BaseEdit::RemoveViewKey { view, key } => {
            *view as u32 == *sort_view && matches!(key.as_str(), "order" | "slate" | "source")
        }
        crate::bases::BaseEdit::SetSlateState { view, .. } => *view as u32 == *sort_view,
        crate::bases::BaseEdit::SetSlateSort { view, .. } => *view as u32 == *sort_view,
        // A removed formula can be the transient column itself or a dependency
        // of that column. Clearing conservatively avoids retaining a key that
        // becomes meaningful again after a later formula edit.
        crate::bases::BaseEdit::RemoveFormula { .. } => true,
        crate::bases::BaseEdit::RenameView { .. }
        | crate::bases::BaseEdit::SetViewFilters { .. }
        | crate::bases::BaseEdit::SetTopLevelFilters { .. }
        | crate::bases::BaseEdit::SetFormula { .. }
        | crate::bases::BaseEdit::SetDisplayName { .. }
        | crate::bases::BaseEdit::SetSummaryAssignment { .. } => false,
    })
}

impl VaultSession {
    pub fn bases_list(&self) -> Result<Vec<BaseFileSummary>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        let mut stmt = conn.prepare_cached(
            "SELECT f.path, bf.name, bf.warning_count, bf.indexed_at_ms, bf.parsed_query_json
             FROM bases_files bf
             JOIN files f ON f.id = bf.file_id
             ORDER BY f.path",
        )?;
        let rows = stmt.query_map([], |row| {
            let parsed: String = row.get(4)?;
            let json = serde_json::from_str::<serde_json::Value>(&parsed).unwrap_or_default();
            let view_count = json
                .get("views")
                .and_then(serde_json::Value::as_array)
                .map(|views| views.len() as u32)
                .unwrap_or(0);
            Ok(BaseFileSummary {
                path: row.get(0)?,
                name: row.get(1)?,
                warning_count: row.get::<_, i64>(2)? as u32,
                indexed_at_ms: row.get(3)?,
                view_count,
                degraded: json
                    .get("degraded")
                    .and_then(serde_json::Value::as_bool)
                    .unwrap_or(false),
            })
        })?;
        Ok(rows.collect::<Result<Vec<_>, _>>()?)
    }

    /// Heal the narrow watcher race where a readable `.base` is opened after
    /// the latest vault scan but before its filesystem event is indexed.
    ///
    /// `base_execute` resolves `this` through the `files` table. A file-backed
    /// handle therefore needs its own path represented there before the handle
    /// records that path as its default context. Existing rows stay on the
    /// scanner fast path; only a genuinely missing row pays the targeted read
    /// and Bases-index update below.
    fn ensure_open_base_indexed(&self, path: &str) -> Result<(), VaultError> {
        let mut conn = self.conn.lock().expect("session connection mutex");
        let indexed = conn
            .query_row(
                "SELECT 1 FROM files WHERE path = ?1",
                rusqlite::params![path],
                |_| Ok(()),
            )
            .optional()?
            .is_some();
        if indexed {
            return Ok(());
        }

        let tx = conn.transaction()?;
        let mut indexed_paths = tx
            .prepare("SELECT path FROM files")?
            .query_map([], |row| row.get::<_, String>(0))?
            .collect::<Result<Vec<_>, _>>()?;
        indexed_paths.push(path.to_string());
        let vault_index = crate::InMemoryVaultIndex::new(indexed_paths);
        let mut report = ScanReport::default();
        let (name, _, _) = classify_path(path);
        index_file(
            &tx,
            self.provider.as_ref(),
            path,
            &name,
            self.config.parser_version,
            now_ms(),
            &mut report,
            &vault_index,
            self.config.large_file_refuse_bytes,
        )?;
        tx.commit()?;
        self.bump_bases_generation();
        Ok(())
    }

    pub fn open_base(&self, path: &str) -> Result<u64, VaultError> {
        let source = self.read_text(path)?;
        self.ensure_open_base_indexed(path)?;
        let (base, warnings) = crate::bases::parse_base(&source);
        let queries = compiled_base_queries(&base);
        let handle = self
            .next_base_handle
            .fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        self.bases.lock().expect("base registry mutex").insert(
            handle,
            OpenBaseState {
                path: Some(path.to_string()),
                content_hash: Some(content_hash(source.as_bytes())),
                source: OpenBaseSource::Base(base),
                queries,
                warnings: warnings.into_iter().map(|w| w.message).collect(),
                default_this_path: Some(path.to_string()),
                cache: crate::bases::engine::BasesQueryCache::default(),
                transient_sort: None,
            },
        );
        Ok(handle)
    }

    pub fn open_base_inline(
        &self,
        source: &str,
        this_path: Option<String>,
    ) -> Result<u64, VaultError> {
        let (base, warnings) = crate::bases::parse_base(source);
        let queries = compiled_base_queries(&base);
        let handle = self
            .next_base_handle
            .fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        self.bases.lock().expect("base registry mutex").insert(
            handle,
            OpenBaseState {
                path: None,
                content_hash: None,
                source: OpenBaseSource::Base(base),
                queries,
                warnings: warnings.into_iter().map(|w| w.message).collect(),
                default_this_path: this_path,
                cache: crate::bases::engine::BasesQueryCache::default(),
                transient_sort: None,
            },
        );
        Ok(handle)
    }

    pub fn open_query(
        &self,
        query_json: &str,
        this_path: Option<String>,
    ) -> Result<u64, VaultError> {
        let query =
            serde_json::from_str::<crate::bases::SlateQuery>(query_json).map_err(|err| {
                VaultError::InvalidArgument {
                    message: format!("invalid SlateQuery JSON: {err}"),
                }
            })?;
        let handle = self
            .next_base_handle
            .fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        self.bases.lock().expect("base registry mutex").insert(
            handle,
            OpenBaseState {
                path: None,
                content_hash: None,
                queries: vec![query.clone()],
                source: OpenBaseSource::Query(query),
                warnings: Vec::new(),
                default_this_path: this_path,
                cache: crate::bases::engine::BasesQueryCache::default(),
                transient_sort: None,
            },
        );
        Ok(handle)
    }

    pub fn open_saved_query(&self, id: &str) -> Result<u64, VaultError> {
        let query = {
            let conn = self.conn.lock().expect("session connection mutex");
            let saved = load_saved_query(&conn, id)?;
            parse_saved_query_envelope(&saved.query_json)?
        };
        let handle = self
            .next_base_handle
            .fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        self.bases.lock().expect("base registry mutex").insert(
            handle,
            OpenBaseState {
                path: None,
                content_hash: None,
                queries: vec![query.clone()],
                source: OpenBaseSource::Query(query),
                warnings: Vec::new(),
                default_this_path: None,
                cache: crate::bases::engine::BasesQueryCache::default(),
                transient_sort: None,
            },
        );
        Ok(handle)
    }

    pub fn save_query(
        &self,
        name: &str,
        description: Option<&str>,
        query_json: &str,
        source_syntax: SavedQuerySourceSyntax,
    ) -> Result<String, VaultError> {
        validate_saved_name("saved query", name)?;
        let envelope = normalize_saved_query_envelope_for_save(query_json)?;
        let now = now_ms();
        let mut conn = self.conn.lock().expect("session connection mutex");
        let tx = conn.transaction()?;
        ensure_name_available(&tx, NameTable::SavedQueries, name, None)?;
        let id = sqlite_uuid(&tx)?;
        tx.execute(
            "INSERT INTO saved_queries
             (id, name, description, query_json, source_syntax, created_at_ms, modified_at_ms)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)",
            rusqlite::params![
                id,
                name,
                description,
                envelope,
                source_syntax.to_i64(),
                now,
                now
            ],
        )?;
        tx.commit()?;
        Ok(id)
    }

    pub fn list_saved_queries(&self) -> Result<Vec<SavedQuerySummary>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        let mut stmt = conn.prepare_cached(
            "SELECT id, name, description, query_json, source_syntax, created_at_ms, modified_at_ms
             FROM saved_queries
             ORDER BY name COLLATE NOCASE, name",
        )?;
        let rows = stmt.query_map([], saved_query_summary_from_row)?;
        Ok(rows.collect::<Result<Vec<_>, _>>()?)
    }

    pub fn get_saved_query(&self, id: &str) -> Result<SavedQuery, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        load_saved_query(&conn, id)
    }

    pub fn rename_saved_query(&self, id: &str, name: &str) -> Result<(), VaultError> {
        validate_saved_name("saved query", name)?;
        let now = now_ms();
        let mut conn = self.conn.lock().expect("session connection mutex");
        let tx = conn.transaction()?;
        ensure_name_available(&tx, NameTable::SavedQueries, name, Some(id))?;
        let changed = tx.execute(
            "UPDATE saved_queries SET name = ?1, modified_at_ms = ?2 WHERE id = ?3",
            rusqlite::params![name, now, id],
        )?;
        ensure_changed(changed, "saved query", id)?;
        tx.commit()?;
        Ok(())
    }

    pub fn update_saved_query(
        &self,
        id: &str,
        description: Option<&str>,
        query_json: &str,
        source_syntax: SavedQuerySourceSyntax,
    ) -> Result<(), VaultError> {
        let envelope = normalize_saved_query_envelope_for_save(query_json)?;
        let now = now_ms();
        let conn = self.conn.lock().expect("session connection mutex");
        let changed = conn.execute(
            "UPDATE saved_queries
             SET description = ?1, query_json = ?2, source_syntax = ?3, modified_at_ms = ?4
             WHERE id = ?5",
            rusqlite::params![description, envelope, source_syntax.to_i64(), now, id],
        )?;
        ensure_changed(changed, "saved query", id)
    }

    pub fn delete_saved_query(&self, id: &str) -> Result<(), VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        let changed = conn.execute("DELETE FROM saved_queries WHERE id = ?1", [id])?;
        ensure_changed(changed, "saved query", id)
    }

    pub fn export_saved_query_as_base(&self, id: &str, path: &str) -> Result<(), VaultError> {
        let query = {
            let conn = self.conn.lock().expect("session connection mutex");
            let saved = load_saved_query(&conn, id)?;
            parse_saved_query_envelope(&saved.query_json)?
        };
        let text = query_as_base_text(&query)?;
        self.save_text(path, &text, None)?;
        Ok(())
    }

    pub fn save_dashboard(
        &self,
        name: &str,
        sections: Vec<DashboardSection>,
    ) -> Result<String, VaultError> {
        validate_saved_name("dashboard", name)?;
        let sections_json = dashboard_sections_json(&sections)?;
        let now = now_ms();
        let mut conn = self.conn.lock().expect("session connection mutex");
        let tx = conn.transaction()?;
        ensure_name_available(&tx, NameTable::Dashboards, name, None)?;
        let id = sqlite_uuid(&tx)?;
        tx.execute(
            "INSERT INTO dashboards
             (id, name, sections_json, created_at_ms, modified_at_ms)
             VALUES (?1, ?2, ?3, ?4, ?5)",
            rusqlite::params![id, name, sections_json, now, now],
        )?;
        tx.commit()?;
        Ok(id)
    }

    pub fn list_dashboards(&self) -> Result<Vec<DashboardSummary>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        let mut stmt = conn.prepare_cached(
            "SELECT id, name, sections_json, created_at_ms, modified_at_ms
             FROM dashboards
             ORDER BY name COLLATE NOCASE, name",
        )?;
        let mut rows = stmt.query([])?;
        let mut dashboards = Vec::new();
        while let Some(row) = rows.next()? {
            let sections_json: String = row.get(2)?;
            let section_count = parse_dashboard_sections(&sections_json)?.len() as u32;
            dashboards.push(DashboardSummary {
                id: row.get(0)?,
                name: row.get(1)?,
                section_count,
                created_at_ms: row.get(3)?,
                modified_at_ms: row.get(4)?,
            });
        }
        Ok(dashboards)
    }

    pub fn get_dashboard(&self, id: &str) -> Result<Dashboard, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        load_dashboard(&conn, id)
    }

    /// Update a dashboard's user-editable fields as one durable operation.
    ///
    /// Name and section writes deliberately remain separate statements inside
    /// one transaction: any failure while persisting the sections rolls the
    /// preceding name write back instead of exposing a partially saved editor
    /// state.
    pub fn update_dashboard(
        &self,
        id: &str,
        name: &str,
        sections: Vec<DashboardSection>,
    ) -> Result<(), VaultError> {
        validate_saved_name("dashboard", name)?;
        let sections_json = dashboard_sections_json(&sections)?;
        let now = now_ms();
        let mut conn = self.conn.lock().expect("session connection mutex");
        let tx = conn.transaction()?;
        ensure_name_available(&tx, NameTable::Dashboards, name, Some(id))?;
        let changed = tx.execute(
            "UPDATE dashboards SET name = ?1 WHERE id = ?2",
            rusqlite::params![name, id],
        )?;
        ensure_changed(changed, "dashboard", id)?;
        let changed = tx.execute(
            "UPDATE dashboards SET sections_json = ?1, modified_at_ms = ?2 WHERE id = ?3",
            rusqlite::params![sections_json, now, id],
        )?;
        ensure_changed(changed, "dashboard", id)?;
        tx.commit()?;
        Ok(())
    }

    pub fn rename_dashboard(&self, id: &str, name: &str) -> Result<(), VaultError> {
        validate_saved_name("dashboard", name)?;
        let now = now_ms();
        let mut conn = self.conn.lock().expect("session connection mutex");
        let tx = conn.transaction()?;
        ensure_name_available(&tx, NameTable::Dashboards, name, Some(id))?;
        let changed = tx.execute(
            "UPDATE dashboards SET name = ?1, modified_at_ms = ?2 WHERE id = ?3",
            rusqlite::params![name, now, id],
        )?;
        ensure_changed(changed, "dashboard", id)?;
        tx.commit()?;
        Ok(())
    }

    pub fn update_dashboard_sections(
        &self,
        id: &str,
        sections: Vec<DashboardSection>,
    ) -> Result<(), VaultError> {
        let sections_json = dashboard_sections_json(&sections)?;
        let now = now_ms();
        let conn = self.conn.lock().expect("session connection mutex");
        let changed = conn.execute(
            "UPDATE dashboards SET sections_json = ?1, modified_at_ms = ?2 WHERE id = ?3",
            rusqlite::params![sections_json, now, id],
        )?;
        ensure_changed(changed, "dashboard", id)
    }

    pub fn delete_dashboard(&self, id: &str) -> Result<(), VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        let changed = conn.execute("DELETE FROM dashboards WHERE id = ?1", [id])?;
        ensure_changed(changed, "dashboard", id)
    }

    pub fn open_dql(&self, source: &str, this_path: Option<String>) -> Result<u64, VaultError> {
        let (query, warnings) = crate::bases::dql::parse_dql(source);
        let handle = self
            .next_base_handle
            .fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        self.bases.lock().expect("base registry mutex").insert(
            handle,
            OpenBaseState {
                path: None,
                content_hash: None,
                queries: vec![query.clone()],
                source: OpenBaseSource::Query(query),
                warnings: warnings.into_iter().map(|w| w.message).collect(),
                default_this_path: this_path,
                cache: crate::bases::engine::BasesQueryCache::default(),
                transient_sort: None,
            },
        );
        Ok(handle)
    }

    pub fn save_query_as_base(&self, query_json: &str, path: &str) -> Result<(), VaultError> {
        let query = parse_query_json(query_json)?;
        let text = query_as_base_text(&query)?;
        self.save_text(path, &text, None)?;
        Ok(())
    }

    pub fn dql_as_base(&self, source: &str) -> Result<String, VaultError> {
        let (query, warnings) = crate::bases::dql::parse_dql(source);
        if !warnings.is_empty() {
            return Err(VaultError::InvalidArgument {
                message: format!(
                    "DQL conversion is lossy: {}",
                    warnings
                        .iter()
                        .map(|warning| warning.message.as_str())
                        .collect::<Vec<_>>()
                        .join("; ")
                ),
            });
        }
        query_as_base_text(&query)
    }

    pub fn close_base(&self, handle: u64) {
        self.bases
            .lock()
            .expect("base registry mutex")
            .remove(&handle);
    }

    pub fn base_views(&self, handle: u64) -> Result<Vec<BaseViewSummary>, VaultError> {
        let bases = self.bases.lock().expect("base registry mutex");
        let state = bases.get(&handle).ok_or_else(|| bad_base_handle(handle))?;
        match &state.source {
            OpenBaseSource::Base(base) => Ok(base.views.iter().map(base_view_summary).collect()),
            OpenBaseSource::Query(query) => Ok(vec![query_view_summary(query)]),
        }
    }

    pub fn base_view_query_json(&self, handle: u64, view: u32) -> Result<String, VaultError> {
        self.base_query_json_for_view(handle, view, crate::bases::view_query)
    }

    pub fn base_view_edit_query_json(&self, handle: u64, view: u32) -> Result<String, VaultError> {
        self.base_query_json_for_view(handle, view, crate::bases::view_edit_query)
    }

    pub fn base_set_transient_sort(
        &self,
        handle: u64,
        view: u32,
        column_id: Option<String>,
        ascending: bool,
    ) -> Result<(), VaultError> {
        let mut bases = self.bases.lock().expect("base registry mutex");
        let state = bases
            .get_mut(&handle)
            .ok_or_else(|| bad_base_handle(handle))?;
        let query = base_query_for_view(state, view)?;
        let Some(column_id) = column_id else {
            state.transient_sort = None;
            return Ok(());
        };
        if !query.columns.iter().any(|column| column.id == column_id) {
            return Err(VaultError::InvalidArgument {
                message: format!("base column {column_id:?} is not displayed in view {view}"),
            });
        }
        state.transient_sort = Some((
            view,
            crate::bases::engine::sort_key_for_column_id(&column_id, ascending),
        ));
        Ok(())
    }

    fn base_query_json_for_view<F>(
        &self,
        handle: u64,
        view: u32,
        query_from_base: F,
    ) -> Result<String, VaultError>
    where
        F: FnOnce(&crate::bases::BaseFile, usize) -> crate::bases::SlateQuery,
    {
        let bases = self.bases.lock().expect("base registry mutex");
        let state = bases.get(&handle).ok_or_else(|| bad_base_handle(handle))?;
        let query = match &state.source {
            OpenBaseSource::Base(base) => {
                if base.views.get(view as usize).is_none() {
                    return Err(VaultError::InvalidArgument {
                        message: format!("base view {view} is out of range"),
                    });
                }
                query_from_base(base, view as usize)
            }
            OpenBaseSource::Query(query) => {
                if view != 0 {
                    return Err(VaultError::InvalidArgument {
                        message: format!("query handle has one view; got {view}"),
                    });
                }
                query.clone()
            }
        };
        serde_json::to_string(&query).map_err(|err| VaultError::InvalidArgument {
            message: format!("could not encode SlateQuery JSON: {err}"),
        })
    }

    pub fn base_execute(
        &self,
        handle: u64,
        view: u32,
        this_path: Option<String>,
        quick_filter: Option<String>,
        cancel: &CancelToken,
    ) -> Result<BasesResultSet, VaultError> {
        self.base_execute_at(handle, view, this_path, quick_filter, now_ms(), cancel)
    }

    fn base_execute_at(
        &self,
        handle: u64,
        view: u32,
        this_path: Option<String>,
        quick_filter: Option<String>,
        execution_now_ms: i64,
        cancel: &CancelToken,
    ) -> Result<BasesResultSet, VaultError> {
        let bases = self.bases.lock().expect("base registry mutex");
        let state = bases.get(&handle).ok_or_else(|| bad_base_handle(handle))?;
        let compiled_query = base_query_for_view(state, view)?;
        let transient_query;
        let query = if let Some((sort_view, sort)) = &state.transient_sort
            && *sort_view == view
        {
            transient_query = {
                let mut query = compiled_query.clone();
                query.sort = vec![sort.clone()];
                query
            };
            &transient_query
        } else {
            compiled_query
        };
        let default_this_path = state.default_this_path.clone();
        let warnings = state.warnings.clone();
        let quick_filter = quick_filter
            .as_deref()
            .map(str::trim)
            .filter(|filter| !filter.is_empty());
        let conn = self.conn.lock().expect("session connection mutex");
        let ctx = crate::bases::engine::EngineCtx {
            now_ms: execution_now_ms,
            generation: self.bases_generation(),
            this_path: this_path.or(default_this_path),
            cache: Some(&state.cache),
            quick_filter,
            ..crate::bases::engine::EngineCtx::default()
        };
        let engine_result = crate::bases::engine::execute(query, &conn, &ctx, cancel)?;
        Ok(bases_result_from_engine(engine_result, warnings))
    }

    pub fn base_export(
        &self,
        handle: u64,
        view: u32,
        format: ExportFormat,
        quick_filter: Option<String>,
    ) -> Result<String, VaultError> {
        let result = self.base_execute(handle, view, None, quick_filter, &CancelToken::new())?;
        Ok(export_bases_result(&result, format))
    }

    pub fn base_apply_edit(
        &self,
        handle: u64,
        edit: crate::bases::BaseEdit,
    ) -> Result<(), VaultError> {
        self.base_apply_edits(handle, vec![edit])
    }

    /// Apply an ordered batch to one open `.base`, validating and serializing
    /// the complete batch before the single persistence operation.
    pub fn base_apply_edits(
        &self,
        handle: u64,
        edits: Vec<crate::bases::BaseEdit>,
    ) -> Result<(), VaultError> {
        let mut bases = self.bases.lock().expect("base registry mutex");
        let state = bases
            .get_mut(&handle)
            .ok_or_else(|| bad_base_handle(handle))?;
        let path = state
            .path
            .clone()
            .ok_or_else(|| VaultError::InvalidArgument {
                message: "ephemeral query handles cannot be edited as .base files".to_string(),
            })?;
        let OpenBaseSource::Base(base) = &state.source else {
            return Err(VaultError::InvalidArgument {
                message: "ephemeral query handles cannot be edited as .base files".to_string(),
            });
        };
        if edits.is_empty() {
            return Ok(());
        }
        let clear_transient_sort =
            base_edits_invalidate_transient_sort(state.transient_sort.as_ref(), &edits);
        let new_text = crate::bases::serialize_base(base, &edits).map_err(|err| {
            VaultError::InvalidArgument {
                message: format!("base edit rejected: {err}"),
            }
        })?;
        let expected_hash =
            state
                .content_hash
                .as_deref()
                .ok_or_else(|| VaultError::InvalidArgument {
                    message: "file-backed .base handle is missing its content hash".to_string(),
                })?;
        let report = self.save_text(&path, &new_text, Some(expected_hash))?;
        let (base, warnings) = crate::bases::parse_base(&new_text);
        state.queries = compiled_base_queries(&base);
        state.source = OpenBaseSource::Base(base);
        state.content_hash = Some(report.new_content_hash);
        state.warnings = warnings.into_iter().map(|w| w.message).collect();
        state.cache = crate::bases::engine::BasesQueryCache::default();
        if clear_transient_sort {
            state.transient_sort = None;
        }
        Ok(())
    }
}

fn base_view_summary(view: &crate::bases::ViewDef) -> BaseViewSummary {
    BaseViewSummary {
        name: view.name.clone(),
        view_type: view_type_name(&view.view_type).to_string(),
        source: row_source_name(&view.source).to_string(),
        slate_state_json: view
            .slate_state
            .as_ref()
            .and_then(|state| serde_json::to_string(state).ok()),
        status: match view.view_type {
            crate::bases::ViewType::Table | crate::bases::ViewType::List => {
                BaseViewStatus::Executable
            }
            crate::bases::ViewType::Cards
            | crate::bases::ViewType::Map
            | crate::bases::ViewType::Other(_) => BaseViewStatus::Fallback,
        },
    }
}

fn query_view_summary(query: &crate::bases::SlateQuery) -> BaseViewSummary {
    BaseViewSummary {
        name: "Query".to_string(),
        view_type: match query.view {
            crate::bases::ViewSpec::Table { .. } => "table".to_string(),
            crate::bases::ViewSpec::List { .. } => "list".to_string(),
        },
        source: row_source_name(&query.row_source).to_string(),
        status: BaseViewStatus::Executable,
        slate_state_json: None,
    }
}

fn view_type_name(view_type: &crate::bases::ViewType) -> &str {
    match view_type {
        crate::bases::ViewType::Table => "table",
        crate::bases::ViewType::List => "list",
        crate::bases::ViewType::Cards => "cards",
        crate::bases::ViewType::Map => "map",
        crate::bases::ViewType::Other(other) => other.as_str(),
    }
}

fn row_source_name(source: &crate::bases::RowSource) -> &'static str {
    match source {
        crate::bases::RowSource::Files => "files",
        crate::bases::RowSource::Tasks => "tasks",
    }
}

fn parse_query_json(query_json: &str) -> Result<crate::bases::SlateQuery, VaultError> {
    serde_json::from_str::<crate::bases::SlateQuery>(query_json).map_err(|err| {
        VaultError::InvalidArgument {
            message: format!("invalid SlateQuery JSON: {err}"),
        }
    })
}

impl SavedQuerySourceSyntax {
    fn to_i64(self) -> i64 {
        match self {
            SavedQuerySourceSyntax::Builder => 0,
            SavedQuerySourceSyntax::Base => 1,
            SavedQuerySourceSyntax::Dql => 2,
        }
    }

    fn from_i64(value: i64) -> Result<Self, VaultError> {
        match value {
            0 => Ok(SavedQuerySourceSyntax::Builder),
            1 => Ok(SavedQuerySourceSyntax::Base),
            2 => Ok(SavedQuerySourceSyntax::Dql),
            other => Err(VaultError::InvalidArgument {
                message: format!("unknown saved-query source_syntax {other}"),
            }),
        }
    }
}

fn validate_saved_name(kind: &str, name: &str) -> Result<(), VaultError> {
    if name.trim().is_empty() {
        return Err(VaultError::InvalidArgument {
            message: format!("{kind} name cannot be empty"),
        });
    }
    Ok(())
}

fn ensure_changed(changed: usize, kind: &str, id: &str) -> Result<(), VaultError> {
    if changed == 0 {
        return Err(VaultError::InvalidArgument {
            message: format!("unknown {kind} id {id:?}"),
        });
    }
    Ok(())
}

#[derive(Debug, Clone, Copy)]
enum NameTable {
    SavedQueries,
    Dashboards,
}

impl NameTable {
    fn sql_name(self) -> &'static str {
        match self {
            NameTable::SavedQueries => "saved_queries",
            NameTable::Dashboards => "dashboards",
        }
    }

    fn kind(self) -> &'static str {
        match self {
            NameTable::SavedQueries => "saved query",
            NameTable::Dashboards => "dashboard",
        }
    }
}

fn ensure_name_available(
    conn: &Connection,
    table: NameTable,
    name: &str,
    except_id: Option<&str>,
) -> Result<(), VaultError> {
    let table_name = table.sql_name();
    let kind = table.kind();
    let exists: Option<i64> = match except_id {
        Some(id) => conn
            .query_row(
                &format!("SELECT 1 FROM {table_name} WHERE name = ?1 AND id <> ?2 LIMIT 1"),
                rusqlite::params![name, id],
                |row| row.get(0),
            )
            .optional()?,
        None => conn
            .query_row(
                &format!("SELECT 1 FROM {table_name} WHERE name = ?1 LIMIT 1"),
                [name],
                |row| row.get(0),
            )
            .optional()?,
    };
    if exists.is_some() {
        return Err(VaultError::InvalidArgument {
            message: format!("{kind} name {name:?} already exists"),
        });
    }
    Ok(())
}

fn sqlite_uuid(conn: &Connection) -> Result<String, VaultError> {
    Ok(conn.query_row(
        "SELECT lower(
            hex(randomblob(4)) || '-' ||
            hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-' ||
            substr('89ab', 1 + (abs(random()) % 4), 1) || substr(hex(randomblob(2)), 2) || '-' ||
            hex(randomblob(6))
        )",
        [],
        |row| row.get(0),
    )?)
}

fn normalize_saved_query_envelope_for_save(query_json: &str) -> Result<String, VaultError> {
    let value = serde_json::from_str::<serde_json::Value>(query_json).map_err(|err| {
        VaultError::InvalidArgument {
            message: format!("invalid SlateQuery JSON: {err}"),
        }
    })?;
    let query = if value.get("v").is_some() {
        parse_saved_query_envelope(query_json)?
    } else {
        serde_json::from_value::<crate::bases::SlateQuery>(value).map_err(|err| {
            VaultError::InvalidArgument {
                message: format!("invalid SlateQuery JSON: {err}"),
            }
        })?
    };
    Ok(serde_json::json!({ "v": 1, "query": query }).to_string())
}

fn parse_saved_query_envelope(query_json: &str) -> Result<crate::bases::SlateQuery, VaultError> {
    let value = serde_json::from_str::<serde_json::Value>(query_json).map_err(|err| {
        VaultError::InvalidArgument {
            message: format!("invalid saved query envelope: {err}"),
        }
    })?;
    let version = value
        .get("v")
        .and_then(serde_json::Value::as_u64)
        .ok_or_else(|| VaultError::InvalidArgument {
            message: "invalid saved query envelope: missing numeric version".to_string(),
        })?;
    if version != 1 {
        return Err(VaultError::InvalidArgument {
            message: format!("unsupported query_json envelope version {version}"),
        });
    }
    let query = value
        .get("query")
        .ok_or_else(|| VaultError::InvalidArgument {
            message: "invalid saved query envelope: missing query".to_string(),
        })?
        .clone();
    serde_json::from_value::<crate::bases::SlateQuery>(query).map_err(|err| {
        VaultError::InvalidArgument {
            message: format!("invalid saved query envelope query: {err}"),
        }
    })
}

fn saved_query_envelope_warning(query_json: &str) -> Option<String> {
    parse_saved_query_envelope(query_json)
        .err()
        .map(|err| match err {
            VaultError::InvalidArgument { message } => message,
            other => other.to_string(),
        })
}

fn saved_query_summary_from_row(row: &rusqlite::Row<'_>) -> rusqlite::Result<SavedQuerySummary> {
    let query_json: String = row.get(3)?;
    let source_syntax_raw: i64 = row.get(4)?;
    let source_syntax = SavedQuerySourceSyntax::from_i64(source_syntax_raw).map_err(|err| {
        rusqlite::Error::FromSqlConversionFailure(4, rusqlite::types::Type::Integer, Box::new(err))
    })?;
    Ok(SavedQuerySummary {
        id: row.get(0)?,
        name: row.get(1)?,
        description: row.get(2)?,
        warning: saved_query_envelope_warning(&query_json),
        source_syntax,
        created_at_ms: row.get(5)?,
        modified_at_ms: row.get(6)?,
    })
}

fn saved_query_from_row(row: &rusqlite::Row<'_>) -> rusqlite::Result<SavedQuery> {
    let query_json: String = row.get(3)?;
    let source_syntax_raw: i64 = row.get(4)?;
    let source_syntax = SavedQuerySourceSyntax::from_i64(source_syntax_raw).map_err(|err| {
        rusqlite::Error::FromSqlConversionFailure(4, rusqlite::types::Type::Integer, Box::new(err))
    })?;
    Ok(SavedQuery {
        id: row.get(0)?,
        name: row.get(1)?,
        description: row.get(2)?,
        warning: saved_query_envelope_warning(&query_json),
        query_json,
        source_syntax,
        created_at_ms: row.get(5)?,
        modified_at_ms: row.get(6)?,
    })
}

fn load_saved_query(conn: &Connection, id: &str) -> Result<SavedQuery, VaultError> {
    conn.query_row(
        "SELECT id, name, description, query_json, source_syntax, created_at_ms, modified_at_ms
         FROM saved_queries
         WHERE id = ?1",
        [id],
        saved_query_from_row,
    )
    .optional()?
    .ok_or_else(|| VaultError::InvalidArgument {
        message: format!("unknown saved query id {id:?}"),
    })
}

fn dashboard_sections_json(sections: &[DashboardSection]) -> Result<String, VaultError> {
    serde_json::to_string(sections).map_err(|err| VaultError::InvalidArgument {
        message: format!("invalid dashboard sections: {err}"),
    })
}

fn parse_dashboard_sections(sections_json: &str) -> Result<Vec<DashboardSection>, VaultError> {
    serde_json::from_str::<Vec<DashboardSection>>(sections_json).map_err(|err| {
        VaultError::InvalidArgument {
            message: format!("invalid dashboard sections: {err}"),
        }
    })
}

fn load_dashboard(conn: &Connection, id: &str) -> Result<Dashboard, VaultError> {
    let (id, name, sections_json, created_at_ms, modified_at_ms): (
        String,
        String,
        String,
        i64,
        i64,
    ) = conn
        .query_row(
            "SELECT id, name, sections_json, created_at_ms, modified_at_ms
             FROM dashboards
             WHERE id = ?1",
            [id],
            |row| {
                Ok((
                    row.get(0)?,
                    row.get(1)?,
                    row.get(2)?,
                    row.get(3)?,
                    row.get(4)?,
                ))
            },
        )
        .optional()?
        .ok_or_else(|| VaultError::InvalidArgument {
            message: format!("unknown dashboard id {id:?}"),
        })?;
    let sections = parse_dashboard_sections(&sections_json)?;
    let mut resolved = Vec::with_capacity(sections.len());
    let mut saved_name_stmt =
        conn.prepare_cached("SELECT name FROM saved_queries WHERE id = ?1")?;
    for section in sections {
        let saved_query_name = saved_name_stmt
            .query_row([section.saved_query_id.as_str()], |row| {
                row.get::<_, String>(0)
            })
            .optional()?;
        resolved.push(DashboardSectionStatus {
            missing: saved_query_name.is_none(),
            saved_query_name,
            saved_query_id: section.saved_query_id,
            heading_override: section.heading_override,
            view_override: section.view_override,
        });
    }
    Ok(Dashboard {
        id,
        name,
        sections: resolved,
        created_at_ms,
        modified_at_ms,
    })
}

#[derive(Debug, Clone)]
enum FilterYaml {
    Stmt(String),
    And(Vec<FilterYaml>),
    Or(Vec<FilterYaml>),
    Not(Vec<FilterYaml>),
}

fn query_as_base_text(query: &crate::bases::SlateQuery) -> Result<String, VaultError> {
    validate_query_as_base(query)?;
    let mut out = String::new();
    if let Some(filter) = query_filter_yaml(query)? {
        push_filter_yaml(&mut out, "filters", &filter, 0);
    }
    if !query.formulas.is_empty() {
        out.push_str("formulas:\n");
        for (name, expr) in &query.formulas {
            let expression = expr_to_source(expr)?;
            out.push_str(&format!(
                "  {}: {}\n",
                yaml_key(name),
                expr_string_literal(&expression)
            ));
        }
    }
    let display_names = query
        .columns
        .iter()
        .filter_map(|column| {
            column
                .display_name
                .as_ref()
                .map(|name| (column.id.as_str(), name.as_str()))
        })
        .collect::<Vec<_>>();
    if !display_names.is_empty() {
        out.push_str("properties:\n");
        for (id, name) in display_names {
            out.push_str(&format!("  {}:\n", yaml_key(id)));
            out.push_str(&format!("    displayName: {}\n", yaml_scalar(name)));
        }
    }
    if !query.custom_summaries.is_empty() {
        out.push_str("summaries:\n");
        for (name, expr) in &query.custom_summaries {
            let expression = expr_to_source(expr)?;
            out.push_str(&format!(
                "  {}: {}\n",
                yaml_key(name),
                expr_string_literal(&expression)
            ));
        }
    }
    out.push_str("views:\n");
    out.push_str("  - type: ");
    out.push_str(match query.view {
        crate::bases::ViewSpec::Table { .. } => "table",
        crate::bases::ViewSpec::List { .. } => "list",
    });
    out.push('\n');
    out.push_str("    name: Query\n");
    if let Some(limit) = query.limit {
        out.push_str(&format!("    limit: {limit}\n"));
    }
    if let Some(group_by) = &query.group_by {
        out.push_str("    groupBy:\n");
        out.push_str(&format!(
            "      property: {}\n",
            yaml_scalar(&property_ref_to_source(&group_by.property)?)
        ));
        out.push_str(&format!(
            "      direction: {}\n",
            if group_by.ascending { "ASC" } else { "DESC" }
        ));
    }
    if !query.columns.is_empty() {
        out.push_str("    order:\n");
        for column in &query.columns {
            out.push_str(&format!("      - {}\n", yaml_scalar(&column.id)));
        }
    }
    if !query.summaries.is_empty() {
        out.push_str("    summaries:\n");
        for (column, summary) in &query.summaries {
            let summary_name = match summary {
                crate::bases::SummaryRef::Builtin(name) => name,
                crate::bases::SummaryRef::Custom(name) => name,
            };
            out.push_str(&format!(
                "      {}: {}\n",
                yaml_key(column),
                yaml_scalar(summary_name)
            ));
        }
    }
    if query.row_source == crate::bases::RowSource::Tasks {
        out.push_str("    source: tasks\n");
    }
    if !query.sort.is_empty() {
        out.push_str("    slate:\n");
        out.push_str("      sort:\n");
        for sort in &query.sort {
            out.push_str(&format!(
                "        - expr: {}\n",
                yaml_scalar(&expr_to_source(&sort.expr)?)
            ));
            out.push_str(&format!(
                "          direction: {}\n",
                if sort.ascending { "asc" } else { "desc" }
            ));
        }
    }
    Ok(out)
}

fn validate_query_as_base(query: &crate::bases::SlateQuery) -> Result<(), VaultError> {
    match &query.view {
        crate::bases::ViewSpec::Table {
            fallback_from: Some(view),
        }
        | crate::bases::ViewSpec::List {
            fallback_from: Some(view),
        } => {
            return Err(VaultError::InvalidArgument {
                message: format!("cannot save fallback {view:?} view as canonical .base text"),
            });
        }
        crate::bases::ViewSpec::Table {
            fallback_from: None,
        }
        | crate::bases::ViewSpec::List {
            fallback_from: None,
        } => {}
    }

    if let crate::bases::QuerySource::Unsupported(reason) = &query.source {
        return Err(VaultError::InvalidArgument {
            message: format!("cannot save unsupported source as a .base file: {reason}"),
        });
    }
    if let crate::bases::QuerySource::Linked { depth, .. } = &query.source
        && *depth != 1
    {
        return Err(VaultError::InvalidArgument {
            message: format!("cannot save linked source depth {depth} as a .base file"),
        });
    }

    if let Some(filter) = &query.filters {
        validate_filter_as_base(filter, "filter")?;
    }
    for (name, expr) in &query.formulas {
        validate_expr_as_base(expr, &format!("formula {name:?}"))?;
    }
    for (name, expr) in &query.custom_summaries {
        validate_expr_as_base(expr, &format!("summary {name:?}"))?;
    }
    for sort in &query.sort {
        validate_expr_as_base(&sort.expr, "sort expression")?;
    }
    for column in &query.columns {
        validate_column_as_base(&column.id)?;
    }
    for (_, summary) in &query.summaries {
        validate_summary_as_base(summary, &query.custom_summaries)?;
    }
    Ok(())
}

fn validate_filter_as_base(
    filter: &crate::bases::FilterNode,
    context: &str,
) -> Result<(), VaultError> {
    match filter {
        crate::bases::FilterNode::Stmt(expr) => validate_expr_as_base(expr, context),
        crate::bases::FilterNode::And(nodes)
        | crate::bases::FilterNode::Or(nodes)
        | crate::bases::FilterNode::Not(nodes) => {
            for node in nodes {
                validate_filter_as_base(node, context)?;
            }
            Ok(())
        }
    }
}

fn validate_column_as_base(id: &str) -> Result<(), VaultError> {
    if id.starts_with("formula.") {
        return Ok(());
    }
    if let Ok(expr) = crate::bases::expr::parse_expr(id) {
        validate_expr_as_base(&expr, &format!("column {id:?}"))?;
    }
    Ok(())
}

fn validate_summary_as_base(
    summary: &crate::bases::SummaryRef,
    custom_summaries: &[(String, crate::bases::expr::Expr)],
) -> Result<(), VaultError> {
    match summary {
        crate::bases::SummaryRef::Builtin(name) => {
            let normalized = name.to_ascii_lowercase();
            if matches!(
                normalized.as_str(),
                "count"
                    | "filled"
                    | "empty"
                    | "unique"
                    | "min"
                    | "max"
                    | "sum"
                    | "average"
                    | "earliest"
                    | "latest"
                    | "checked"
                    | "unchecked"
            ) {
                Ok(())
            } else {
                Err(VaultError::InvalidArgument {
                    message: format!("cannot save unsupported summary {name:?} as a .base file"),
                })
            }
        }
        crate::bases::SummaryRef::Custom(name) => {
            if custom_summaries
                .iter()
                .any(|(summary_name, _)| summary_name == name)
            {
                Ok(())
            } else {
                Err(VaultError::InvalidArgument {
                    message: format!("cannot save unknown custom summary {name:?}"),
                })
            }
        }
    }
}

fn validate_expr_as_base(expr: &crate::bases::expr::Expr, context: &str) -> Result<(), VaultError> {
    use crate::bases::expr::{Callee, ExprKind, GlobalFn, Lit};

    match &expr.kind {
        ExprKind::Lit(lit) => match lit {
            Lit::List(values) => {
                for value in values {
                    validate_expr_as_base(value, context)?;
                }
                Ok(())
            }
            Lit::Object(values) => {
                for (_, value) in values {
                    validate_expr_as_base(value, context)?;
                }
                Ok(())
            }
            Lit::String(_) | Lit::Number(_) | Lit::Bool(_) | Lit::Regex { .. } => Ok(()),
        },
        ExprKind::Prop(_) => Ok(()),
        ExprKind::Index { base, index } => {
            validate_expr_as_base(base, context)?;
            validate_expr_as_base(index, context)
        }
        ExprKind::Field { base, .. } | ExprKind::Unary { rhs: base, .. } => {
            validate_expr_as_base(base, context)
        }
        ExprKind::Binary { lhs, rhs, .. } => {
            validate_expr_as_base(lhs, context)?;
            validate_expr_as_base(rhs, context)
        }
        ExprKind::Call { callee, args } => {
            match callee {
                Callee::Global(GlobalFn::Random) => {
                    return Err(VaultError::InvalidArgument {
                        message: format!(
                            "cannot save {context}: random() is excluded from Bases v1"
                        ),
                    });
                }
                Callee::Global(_) => {}
                Callee::Method { receiver, .. } => validate_expr_as_base(receiver, context)?,
            }
            for arg in args {
                validate_expr_as_base(arg, context)?;
            }
            Ok(())
        }
        ExprKind::ListExpr {
            base, body, init, ..
        } => {
            validate_expr_as_base(base, context)?;
            validate_expr_as_base(body, context)?;
            if let Some(init) = init {
                validate_expr_as_base(init, context)?;
            }
            Ok(())
        }
        ExprKind::Unsupported { reason, .. } => Err(VaultError::InvalidArgument {
            message: format!("cannot save {context}: unsupported expression: {reason}"),
        }),
    }
}

fn query_filter_yaml(query: &crate::bases::SlateQuery) -> Result<Option<FilterYaml>, VaultError> {
    let source = query_source_filter(&query.source)?;
    let filter = query.filters.as_ref().map(filter_to_yaml).transpose()?;
    Ok(match (source, filter) {
        (Some(source), Some(filter)) => Some(FilterYaml::And(vec![source, filter])),
        (Some(source), None) => Some(source),
        (None, Some(filter)) => Some(filter),
        (None, None) => None,
    })
}

fn query_source_filter(
    source: &crate::bases::QuerySource,
) -> Result<Option<FilterYaml>, VaultError> {
    use crate::bases::QuerySource;
    Ok(match source {
        QuerySource::All => None,
        QuerySource::Folder(folder) => Some(FilterYaml::Stmt(format!(
            "file.inFolder({})",
            expr_string_literal(folder)
        ))),
        QuerySource::Tag(tag) => Some(FilterYaml::Stmt(format!(
            "file.hasTag({})",
            expr_string_literal(tag)
        ))),
        QuerySource::Recent { days } => Some(FilterYaml::Stmt(format!(
            "file.mtime >= now() - duration({})",
            expr_string_literal(&format!("{days}d"))
        ))),
        QuerySource::Linked { from_path, depth } if *depth == 1 => Some(FilterYaml::Stmt(format!(
            "link({}).linksTo(file.file)",
            expr_string_literal(from_path)
        ))),
        QuerySource::Linked { depth, .. } => {
            return Err(VaultError::InvalidArgument {
                message: format!("cannot save linked source depth {depth} as a .base file"),
            });
        }
        QuerySource::Unsupported(reason) => {
            return Err(VaultError::InvalidArgument {
                message: format!("cannot save unsupported source as a .base file: {reason}"),
            });
        }
    })
}

fn filter_to_yaml(filter: &crate::bases::FilterNode) -> Result<FilterYaml, VaultError> {
    use crate::bases::FilterNode;
    Ok(match filter {
        FilterNode::Stmt(expr) => FilterYaml::Stmt(expr_to_source(expr)?),
        FilterNode::And(nodes) => FilterYaml::And(
            nodes
                .iter()
                .map(filter_to_yaml)
                .collect::<Result<Vec<_>, _>>()?,
        ),
        FilterNode::Or(nodes) => FilterYaml::Or(
            nodes
                .iter()
                .map(filter_to_yaml)
                .collect::<Result<Vec<_>, _>>()?,
        ),
        FilterNode::Not(nodes) => FilterYaml::Not(
            nodes
                .iter()
                .map(filter_to_yaml)
                .collect::<Result<Vec<_>, _>>()?,
        ),
    })
}

fn push_filter_yaml(out: &mut String, key: &str, filter: &FilterYaml, indent: usize) {
    let pad = " ".repeat(indent);
    match filter {
        FilterYaml::Stmt(expr) => {
            out.push_str(&format!("{pad}{key}: {}\n", yaml_scalar(expr)));
        }
        FilterYaml::And(nodes) => push_filter_list_yaml(out, key, "and", nodes, indent),
        FilterYaml::Or(nodes) => push_filter_list_yaml(out, key, "or", nodes, indent),
        FilterYaml::Not(nodes) => push_filter_list_yaml(out, key, "not", nodes, indent),
    }
}

fn push_filter_list_yaml(
    out: &mut String,
    key: &str,
    kind: &str,
    nodes: &[FilterYaml],
    indent: usize,
) {
    let pad = " ".repeat(indent);
    out.push_str(&format!("{pad}{key}:\n"));
    out.push_str(&format!("{pad}  {kind}:\n"));
    for node in nodes {
        push_filter_list_item_yaml(out, node, indent + 4);
    }
}

fn push_filter_list_item_yaml(out: &mut String, node: &FilterYaml, indent: usize) {
    let pad = " ".repeat(indent);
    match node {
        FilterYaml::Stmt(expr) => {
            out.push_str(&format!("{pad}- {}\n", yaml_scalar(expr)));
        }
        FilterYaml::And(children) => push_nested_filter_list_yaml(out, "and", children, indent),
        FilterYaml::Or(children) => push_nested_filter_list_yaml(out, "or", children, indent),
        FilterYaml::Not(children) => push_nested_filter_list_yaml(out, "not", children, indent),
    }
}

fn push_nested_filter_list_yaml(out: &mut String, kind: &str, nodes: &[FilterYaml], indent: usize) {
    let pad = " ".repeat(indent);
    out.push_str(&format!("{pad}- {kind}:\n"));
    for node in nodes {
        push_filter_list_item_yaml(out, node, indent + 4);
    }
}

fn expr_to_source(expr: &crate::bases::expr::Expr) -> Result<String, VaultError> {
    use crate::bases::expr::{Callee, ExprKind, UnaryOp};
    Ok(match &expr.kind {
        ExprKind::Lit(lit) => lit_to_source(lit)?,
        ExprKind::Prop(property) => property_ref_to_source(property)?,
        ExprKind::Index { base, index } => {
            format!("{}[{}]", expr_to_source(base)?, expr_to_source(index)?)
        }
        ExprKind::Field { base, name } => format!("{}.{}", expr_to_source(base)?, name),
        ExprKind::Unary { op, rhs } => match op {
            UnaryOp::Not => format!("!{}", expr_to_source(rhs)?),
            UnaryOp::Neg => format!("-{}", expr_to_source(rhs)?),
        },
        ExprKind::Binary { op, lhs, rhs } => format!(
            "({} {} {})",
            expr_to_source(lhs)?,
            binary_op_source(*op),
            expr_to_source(rhs)?
        ),
        ExprKind::Call { callee, args } => {
            let args = args
                .iter()
                .map(expr_to_source)
                .collect::<Result<Vec<_>, _>>()?
                .join(", ");
            match callee {
                Callee::Global(global) => format!("{}({args})", global_fn_source(*global)),
                Callee::Method { receiver, name } => {
                    format!(
                        "{}.{}({args})",
                        expr_to_source(receiver)?,
                        method_source(*name)
                    )
                }
            }
        }
        ExprKind::ListExpr {
            base,
            kind,
            body,
            init,
        } => {
            let method = match kind {
                crate::bases::expr::ListExprKind::Filter => "filter",
                crate::bases::expr::ListExprKind::Map => "map",
                crate::bases::expr::ListExprKind::Reduce => "reduce",
            };
            let mut args = vec![expr_to_source(body)?];
            if let Some(init) = init {
                args.push(expr_to_source(init)?);
            }
            format!("{}.{}({})", expr_to_source(base)?, method, args.join(", "))
        }
        ExprKind::Unsupported { reason, .. } => {
            return Err(VaultError::InvalidArgument {
                message: format!("cannot save unsupported expression as a .base file: {reason}"),
            });
        }
    })
}

fn lit_to_source(lit: &crate::bases::expr::Lit) -> Result<String, VaultError> {
    use crate::bases::expr::Lit;
    Ok(match lit {
        Lit::String(value) => expr_string_literal(value),
        Lit::Number(value) => value.to_string(),
        Lit::Bool(value) => value.to_string(),
        Lit::List(values) => format!(
            "[{}]",
            values
                .iter()
                .map(expr_to_source)
                .collect::<Result<Vec<_>, _>>()?
                .join(", ")
        ),
        Lit::Object(values) => format!(
            "{{{}}}",
            values
                .iter()
                .map(|(key, value)| Ok(format!(
                    "{}: {}",
                    expr_string_literal(key),
                    expr_to_source(value)?
                )))
                .collect::<Result<Vec<_>, VaultError>>()?
                .join(", ")
        ),
        Lit::Regex { pattern, flags } => format!("/{pattern}/{flags}"),
    })
}

fn property_ref_to_source(
    property: &crate::bases::expr::PropertyRef,
) -> Result<String, VaultError> {
    use crate::bases::expr::PropertyRef;
    Ok(match property {
        PropertyRef::Note(name) => note_property_source(name),
        PropertyRef::File(field) => format!("file.{}", file_field_source(*field)),
        PropertyRef::Formula(name) => namespaced_property_source("formula", name),
        PropertyRef::This => "this".to_string(),
        PropertyRef::ThisNote(name) => this_note_property_source(name),
        PropertyRef::ThisFile(field) => format!("this.file.{}", file_field_source(*field)),
        PropertyRef::TaskField(field) => format!("task.{}", task_field_source(*field)),
        PropertyRef::ImplicitValue => "value".to_string(),
        PropertyRef::ImplicitIndex => "index".to_string(),
        PropertyRef::ImplicitAcc => "acc".to_string(),
    })
}

fn note_property_source(name: &str) -> String {
    if lexer_safe_property_identifier(name) {
        name.to_string()
    } else {
        format!("note[{}]", expr_string_literal(name))
    }
}

fn namespaced_property_source(namespace: &str, name: &str) -> String {
    if lexer_safe_property_identifier(name) {
        format!("{namespace}.{name}")
    } else {
        format!("{namespace}[{}]", expr_string_literal(name))
    }
}

fn this_note_property_source(name: &str) -> String {
    if lexer_identifier(name) && !matches!(name, "true" | "false" | "file") {
        format!("this.{name}")
    } else {
        format!("this[{}]", expr_string_literal(name))
    }
}

fn lexer_safe_property_identifier(name: &str) -> bool {
    lexer_identifier(name)
        && !matches!(
            name,
            "true"
                | "false"
                | "note"
                | "formula"
                | "file"
                | "this"
                | "task"
                | "value"
                | "index"
                | "acc"
        )
}

fn lexer_identifier(name: &str) -> bool {
    let mut bytes = name.bytes();
    let Some(first) = bytes.next() else {
        return false;
    };
    (first.is_ascii_alphabetic() || first == b'_')
        && bytes.all(|byte| byte.is_ascii_alphanumeric() || byte == b'_')
}

fn binary_op_source(op: crate::bases::expr::BinaryOp) -> &'static str {
    use crate::bases::expr::BinaryOp;
    match op {
        BinaryOp::Add => "+",
        BinaryOp::Sub => "-",
        BinaryOp::Mul => "*",
        BinaryOp::Div => "/",
        BinaryOp::Mod => "%",
        BinaryOp::Eq => "==",
        BinaryOp::Ne => "!=",
        BinaryOp::Gt => ">",
        BinaryOp::Lt => "<",
        BinaryOp::Gte => ">=",
        BinaryOp::Lte => "<=",
        BinaryOp::And => "&&",
        BinaryOp::Or => "||",
    }
}

fn global_fn_source(function: crate::bases::expr::GlobalFn) -> &'static str {
    use crate::bases::expr::GlobalFn;
    match function {
        GlobalFn::Date => "date",
        GlobalFn::Duration => "duration",
        GlobalFn::EscapeHtml => "escapeHTML",
        GlobalFn::File => "file",
        GlobalFn::Html => "html",
        GlobalFn::Icon => "icon",
        GlobalFn::If => "if",
        GlobalFn::Image => "image",
        GlobalFn::Link => "link",
        GlobalFn::List => "list",
        GlobalFn::Max => "max",
        GlobalFn::Min => "min",
        GlobalFn::Now => "now",
        GlobalFn::Number => "number",
        GlobalFn::Object => "object",
        GlobalFn::Random => "random",
        GlobalFn::String => "string",
        GlobalFn::Sum => "sum",
        GlobalFn::Average => "average",
        GlobalFn::Today => "today",
    }
}

fn method_source(method: crate::bases::expr::MethodName) -> &'static str {
    use crate::bases::expr::MethodName;
    match method {
        MethodName::IsTruthy => "isTruthy",
        MethodName::IsType => "isType",
        MethodName::ToString => "toString",
        MethodName::Date => "date",
        MethodName::Format => "format",
        MethodName::Time => "time",
        MethodName::Relative => "relative",
        MethodName::IsEmpty => "isEmpty",
        MethodName::Contains => "contains",
        MethodName::ContainsAll => "containsAll",
        MethodName::ContainsAny => "containsAny",
        MethodName::StartsWith => "startsWith",
        MethodName::EndsWith => "endsWith",
        MethodName::Lower => "lower",
        MethodName::Title => "title",
        MethodName::Trim => "trim",
        MethodName::Reverse => "reverse",
        MethodName::Repeat => "repeat",
        MethodName::Slice => "slice",
        MethodName::Split => "split",
        MethodName::Replace => "replace",
        MethodName::Abs => "abs",
        MethodName::Ceil => "ceil",
        MethodName::Floor => "floor",
        MethodName::Trunc => "trunc",
        MethodName::Round => "round",
        MethodName::ToFixed => "toFixed",
        MethodName::Join => "join",
        MethodName::Flat => "flat",
        MethodName::Sort => "sort",
        MethodName::Unique => "unique",
        MethodName::AsFile => "asFile",
        MethodName::LinksTo => "linksTo",
        MethodName::AsLink => "asLink",
        MethodName::HasLink => "hasLink",
        MethodName::HasProperty => "hasProperty",
        MethodName::HasTag => "hasTag",
        MethodName::InFolder => "inFolder",
        MethodName::Keys => "keys",
        MethodName::Values => "values",
        MethodName::Matches => "matches",
        MethodName::OplogHasChangeSince => "has_change_since",
        MethodName::OplogHasPropertyChange => "has_property_change",
        MethodName::OplogDeletedContentMatches => "deleted_content_matches",
        MethodName::OplogDeletedContentMatchesRegex => "deleted_content_matches_regex",
        // Bare names: `expr_to_source` prints `receiver.method(args)`
        // and the receiver already serializes as `oplog` — a prefixed
        // name here would emit `oplog.oplog.created_since(...)`, which
        // no longer re-parses as the operator (regression: #800 PR).
        MethodName::OplogCreatedSince => "created_since",
        MethodName::OplogUntouchedFor => "untouched_for",
    }
}

fn file_field_source(field: crate::bases::expr::FileField) -> &'static str {
    use crate::bases::expr::FileField;
    match field {
        FileField::Name => "name",
        FileField::Basename => "basename",
        FileField::Path => "path",
        FileField::Folder => "folder",
        FileField::Ext => "ext",
        FileField::Size => "size",
        FileField::Properties => "properties",
        FileField::Tags => "tags",
        FileField::Aliases => "aliases",
        FileField::Links => "links",
        FileField::Backlinks => "backlinks",
        FileField::Embeds => "embeds",
        FileField::File => "file",
        FileField::Tasks => "tasks",
        FileField::Ctime => "ctime",
        FileField::Mtime => "mtime",
        FileField::InDegree => "inDegree",
        FileField::OutDegree => "outDegree",
    }
}

fn task_field_source(field: crate::bases::expr::TaskField) -> &'static str {
    use crate::bases::expr::TaskField;
    match field {
        TaskField::Text => "text",
        TaskField::Status => "status",
        TaskField::Completed => "completed",
        TaskField::Due => "due",
        TaskField::Scheduled => "scheduled",
        TaskField::Priority => "priority",
        TaskField::File => "file",
    }
}

fn yaml_key(value: &str) -> String {
    if yaml_plain_safe(value) {
        value.to_string()
    } else {
        expr_string_literal(value)
    }
}

fn yaml_scalar(value: &str) -> String {
    if yaml_plain_safe(value) && !matches!(value, "true" | "false" | "null") {
        value.to_string()
    } else {
        expr_string_literal(value)
    }
}

fn yaml_plain_safe(value: &str) -> bool {
    !value.is_empty()
        && value
            .bytes()
            .all(|b| b.is_ascii_alphanumeric() || matches!(b, b'_' | b'-' | b'.' | b'/' | b' '))
        && !value.starts_with(['-', '?', ':', '!', '@', '&', '*', '#'])
        && !value.ends_with(' ')
}

fn expr_string_literal(value: &str) -> String {
    serde_json::to_string(value).unwrap_or_else(|_| "\"\"".to_string())
}

fn bases_result_from_engine(
    result: crate::bases::engine::BasesResultSet,
    mut open_warnings: Vec<String>,
) -> BasesResultSet {
    open_warnings.extend(result.warnings);
    let columns = result
        .columns
        .iter()
        .enumerate()
        .map(|(index, column)| BasesColumn {
            id: column.id.clone(),
            label: column
                .display_name
                .clone()
                .unwrap_or_else(|| column.id.clone()),
            value_kind: column_value_kind(index, &result.rows),
            role: column_role(index, &column.id),
        })
        .collect();
    BasesResultSet {
        columns,
        rows: result.rows.into_iter().map(bases_row_from_engine).collect(),
        groups: result.groups.into_iter().map(group_from_engine).collect(),
        summaries: result
            .summaries
            .into_iter()
            .map(summary_from_engine)
            .collect(),
        total_count: result.total_count as u64,
        shown_count: result.shown_count as u64,
        unfiltered_shown_count: result.unfiltered_shown_count as u64,
        executed_at_ms: result.executed_at_ms,
        warnings: open_warnings,
        view_error: result.error.map(|e| {
            if e.row_path.is_empty() {
                e.construct
            } else {
                format!("{} ({})", e.construct, e.row_path)
            }
        }),
        audio_summary: result.audio_summary,
    }
}

fn bases_row_from_engine(row: crate::bases::engine::BasesRow) -> BasesRow {
    BasesRow {
        file_path: row.path,
        task_ordinal: row.task_ordinal,
        values: row.cells.into_iter().map(bases_value_from_cell).collect(),
        audio_description: row.audio_description,
    }
}

fn group_from_engine(group: crate::bases::engine::ResultGroup) -> BasesGroup {
    BasesGroup {
        label: group.label,
        row_start: group.rows.start as u64,
        row_count: group.rows.len() as u64,
        summaries: group
            .summaries
            .into_iter()
            .map(summary_from_engine)
            .collect(),
    }
}

fn summary_from_engine(summary: crate::bases::engine::BasesSummaryCell) -> BasesSummaryCell {
    BasesSummaryCell {
        column_id: summary.column_id,
        summary: summary.summary,
        value: bases_value_from_cell(summary.value),
    }
}

fn column_value_kind(index: usize, rows: &[crate::bases::engine::BasesRow]) -> String {
    rows.iter()
        .filter_map(|row| row.cells.get(index))
        .find_map(|cell| match cell {
            crate::bases::engine::CellValue::Value(crate::bases::eval::Value::Null)
            | crate::bases::engine::CellValue::Error(_) => None,
            crate::bases::engine::CellValue::Value(value) => Some(value_kind(value).to_string()),
        })
        .unwrap_or_else(|| "null".to_string())
}

fn column_role(index: usize, id: &str) -> ColumnRole {
    if index == 0 {
        ColumnRole::Primary
    } else if matches!(id, "file.path" | "file.name" | "file.file" | "task.file") {
        ColumnRole::Identifier
    } else if id.starts_with("task.priority") || id.starts_with("formula.") {
        ColumnRole::Metric
    } else {
        ColumnRole::Metadata
    }
}

fn bases_value_from_cell(cell: crate::bases::engine::CellValue) -> BasesValue {
    match cell {
        crate::bases::engine::CellValue::Value(value) => bases_value_from_value(value),
        crate::bases::engine::CellValue::Error(error) => BasesValue {
            raw_kind: "error".to_string(),
            sort_key: format!("fe:{error}"),
            display: format!("Error: {error}"),
            text: None,
            number: None,
            bool_value: None,
            date_epoch_ms: None,
            date_has_time: false,
            link_target: None,
            link_display: None,
            list: Vec::new(),
            error: Some(error),
        },
    }
}

fn bases_value_from_value(value: crate::bases::eval::Value) -> BasesValue {
    use crate::bases::eval::Value;
    let display = crate::bases::engine::value_display(&value);
    let sort_key = crate::bases::engine::value_sort_key(&value);
    match value {
        Value::Null => base_value("null", sort_key, display),
        Value::Bool(value) => BasesValue {
            raw_kind: "bool".to_string(),
            sort_key,
            display,
            text: None,
            number: None,
            bool_value: Some(value),
            date_epoch_ms: None,
            date_has_time: false,
            link_target: None,
            link_display: None,
            list: Vec::new(),
            error: None,
        },
        Value::Number(value) => BasesValue {
            raw_kind: "number".to_string(),
            sort_key,
            display,
            text: None,
            number: Some(value),
            bool_value: None,
            date_epoch_ms: None,
            date_has_time: false,
            link_target: None,
            link_display: None,
            list: Vec::new(),
            error: None,
        },
        Value::Text(text) => BasesValue {
            raw_kind: "text".to_string(),
            sort_key,
            display,
            text: Some(text),
            number: None,
            bool_value: None,
            date_epoch_ms: None,
            date_has_time: false,
            link_target: None,
            link_display: None,
            list: Vec::new(),
            error: None,
        },
        Value::Date(value) => BasesValue {
            raw_kind: "date".to_string(),
            sort_key,
            display,
            text: None,
            number: None,
            bool_value: None,
            date_epoch_ms: Some(value.epoch_ms),
            date_has_time: value.has_time,
            link_target: None,
            link_display: None,
            list: Vec::new(),
            error: None,
        },
        Value::DqlDate(value) => BasesValue {
            raw_kind: "date".to_string(),
            sort_key,
            display,
            text: None,
            number: None,
            bool_value: None,
            date_epoch_ms: Some(value.epoch_ms),
            date_has_time: value.has_time,
            link_target: None,
            link_display: None,
            list: Vec::new(),
            error: None,
        },
        Value::Duration(_) | Value::DqlDuration(_) => base_value("duration", sort_key, display),
        Value::List(values) => BasesValue {
            raw_kind: "list".to_string(),
            sort_key,
            display,
            text: None,
            number: None,
            bool_value: None,
            date_epoch_ms: None,
            date_has_time: false,
            link_target: None,
            link_display: None,
            list: values
                .iter()
                .map(crate::bases::engine::value_display)
                .collect(),
            error: None,
        },
        Value::Object(_) => base_value("object", sort_key, display),
        Value::Link(link) => BasesValue {
            raw_kind: "link".to_string(),
            sort_key,
            display,
            text: None,
            number: None,
            bool_value: None,
            date_epoch_ms: None,
            date_has_time: false,
            link_target: Some(link.target),
            link_display: link.display,
            list: Vec::new(),
            error: None,
        },
        Value::File(file) => BasesValue {
            raw_kind: "file".to_string(),
            sort_key,
            display,
            text: Some(file.path.clone()),
            number: None,
            bool_value: None,
            date_epoch_ms: None,
            date_has_time: false,
            link_target: Some(file.path),
            link_display: None,
            list: Vec::new(),
            error: None,
        },
        Value::Regex(_, _) => base_value("regex", sort_key, display),
    }
}

fn base_value(raw_kind: &str, sort_key: String, display: String) -> BasesValue {
    BasesValue {
        raw_kind: raw_kind.to_string(),
        sort_key,
        display,
        text: None,
        number: None,
        bool_value: None,
        date_epoch_ms: None,
        date_has_time: false,
        link_target: None,
        link_display: None,
        list: Vec::new(),
        error: None,
    }
}

fn value_kind(value: &crate::bases::eval::Value) -> &'static str {
    match value {
        crate::bases::eval::Value::Null => "null",
        crate::bases::eval::Value::Bool(_) => "bool",
        crate::bases::eval::Value::Number(_) => "number",
        crate::bases::eval::Value::Text(_) => "text",
        crate::bases::eval::Value::Date(_) | crate::bases::eval::Value::DqlDate(_) => "date",
        crate::bases::eval::Value::Duration(_) | crate::bases::eval::Value::DqlDuration(_) => {
            "duration"
        }
        crate::bases::eval::Value::List(_) => "list",
        crate::bases::eval::Value::Object(_) => "object",
        crate::bases::eval::Value::Link(_) => "link",
        crate::bases::eval::Value::File(_) => "file",
        crate::bases::eval::Value::Regex(_, _) => "regex",
    }
}

pub fn export_bases_result(result: &BasesResultSet, format: ExportFormat) -> String {
    match format {
        ExportFormat::Csv => bases_export_csv(result),
        ExportFormat::Markdown => bases_export_markdown(result),
    }
}

fn bases_export_csv(result: &BasesResultSet) -> String {
    let mut out = String::new();
    out.push_str(
        &result
            .columns
            .iter()
            .map(|column| csv_field(&column.label))
            .collect::<Vec<_>>()
            .join(","),
    );
    out.push_str("\r\n");
    for row in &result.rows {
        out.push_str(
            &row.values
                .iter()
                .map(|value| csv_field(&value.display))
                .collect::<Vec<_>>()
                .join(","),
        );
        out.push_str("\r\n");
    }
    out
}

fn csv_field(value: &str) -> String {
    if value.contains([',', '"', '\r', '\n']) {
        format!("\"{}\"", value.replace('"', "\"\""))
    } else {
        value.to_string()
    }
}

fn bases_export_markdown(result: &BasesResultSet) -> String {
    let header = result
        .columns
        .iter()
        .map(|column| markdown_cell(&column.label))
        .collect::<Vec<_>>()
        .join(" | ");
    let separator = result
        .columns
        .iter()
        .map(|_| "---")
        .collect::<Vec<_>>()
        .join(" | ");
    let mut out = format!("| {header} |\n| {separator} |\n");
    for row in &result.rows {
        let cells = row
            .values
            .iter()
            .map(|value| markdown_cell(&value.display))
            .collect::<Vec<_>>()
            .join(" | ");
        out.push_str(&format!("| {cells} |\n"));
    }
    out
}

fn markdown_cell(value: &str) -> String {
    value.replace('|', "\\|").replace('\n', "<br>")
}

// ---------------------------------------------------------------------------
// Canvas API (Milestone T, #361). Handle-based: node ids are unique per
// file, not vault-wide. Read shapes pinned in
// docs/plans/09_canvas/specs/t1_spec.md §#361; the mutation surface
// (`canvas_apply`) lands as the second #361 slice after the #366
// serializer, per the t1 execution order.

/// One outline row (depth-first flattening of the canvas model).
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasOutlineRow {
    pub node_id: String,
    pub depth: u32,
    /// Announcement type word: "text" | "file" | "image" | "link" | "group".
    pub kind: String,
    pub title: String,
    pub group_path: Vec<String>,
    /// 1-based position among siblings ("n of m in ⟨group‖canvas⟩").
    pub ordinal_n: u32,
    pub total_m: u32,
    pub connection_count: u32,
    pub color_name: Option<String>,
}

/// One table row (flat, sortable view).
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasTableRow {
    pub node_id: String,
    pub kind: String,
    pub title: String,
    pub group_path: Vec<String>,
    /// File path (file/image cards), URL (link cards), "" otherwise.
    pub target: String,
    pub connection_count: u32,
    pub color_name: Option<String>,
}

/// One adjacency entry for a node, with the raw directional data the
/// announcement layer (#518) phrases from.
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasNeighbor {
    pub edge_id: String,
    pub other_node: String,
    pub other_title: String,
    pub direction: crate::canvas::model::EdgeDirection,
    /// Attachment side on the queried node, if pinned.
    pub self_side: Option<crate::canvas::Side>,
    pub label: Option<String>,
    /// True when the queried node is the edge's `fromNode`.
    pub self_is_from: bool,
}

/// The ⌃⌘I "Where am I?" context readback (t0 §1.4). Mark state is
/// UI-owned and merged UI-side.
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasWhereAmI {
    pub node_id: String,
    pub title: String,
    pub kind: String,
    pub group_path: Vec<String>,
    pub ordinal_n: u32,
    pub total_m: u32,
    pub connection_count: u32,
    pub in_count: u32,
    pub out_count: u32,
    pub color_name: Option<String>,
}

/// One node's render geometry (visual renderer, #367). Raw JSON
/// Canvas coordinates; the renderer owns view transforms.
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasSceneNode {
    pub node_id: String,
    pub kind: String,
    pub title: String,
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
    /// Raw color ("1".."6" or hex), if set.
    pub color: Option<String>,
    pub color_name: Option<String>,
    /// File-card `#heading` subpath, verbatim (t5 #525: open-to-anchor
    /// + faithful duplication). `None` for non-file nodes.
    pub subpath: Option<String>,
}

/// One connection's render data (visual renderer, #367).
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasSceneEdge {
    pub edge_id: String,
    pub from_node: String,
    pub from_side: Option<crate::canvas::Side>,
    pub to_node: String,
    pub to_side: Option<crate::canvas::Side>,
    pub from_arrow: bool,
    pub to_arrow: bool,
    pub label: Option<String>,
    pub color: Option<String>,
}

/// One load warning, pre-classified so the UI can phrase t0 §5
/// ("Canvas loaded. N unsupported items are preserved…") without
/// string-matching.
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasLoadWarning {
    pub kind: CanvasLoadWarningKind,
    pub detail: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CanvasLoadWarningKind {
    /// Whole file unusable — canvas is empty and read-only.
    ParseFailed,
    /// An entry is preserved in the file but not shown.
    SkippedEntry,
    /// A connection references a missing node.
    DanglingEdge,
    /// An optional field was unusable and read as absent.
    IgnoredValue,
}

/// Result of `open_canvas`.
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasOpenInfo {
    pub handle: u64,
    pub node_count: u32,
    pub edge_count: u32,
    /// True when the file could not be loaded as a canvas at all; the
    /// document must be treated as read-only (t0 §5 error state).
    pub degraded: bool,
    pub warnings: Vec<CanvasLoadWarning>,
}

/// Geometry argument for placement / overlap queries.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct CanvasRectArg {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

/// A computed placement for one new card.
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasPlacement {
    pub x: f64,
    pub y: f64,
    pub relative: crate::canvas::placement::RelativeDesc,
}

/// A computed rigid-set placement (pairwise offsets preserved).
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasSetPlacement {
    pub origins: Vec<(f64, f64)>,
    pub relative: crate::canvas::placement::RelativeDesc,
}

/// Result of `canvas_apply`: the post-write content hash (the next
/// apply conflict-checks against it) and the inverse action for the
/// session-scoped undo stack (#372).
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasApplyResult {
    pub new_content_hash: String,
    pub inverse: crate::canvas::apply::CanvasAction,
}

/// `FileTitleSource` over the live index: a file card referencing a
/// note titled via frontmatter (`title:`) displays that title on every
/// canvas surface (t0 §1.1 — never a raw path).
struct DbTitleSource<'a> {
    conn: &'a rusqlite::Connection,
}

impl crate::canvas::model::FileTitleSource for DbTitleSource<'_> {
    fn title_for(&self, vault_path: &str) -> Option<String> {
        self.conn
            .query_row(
                "SELECT p.value_text FROM properties p
                 JOIN files f ON f.id = p.file_id
                 WHERE f.path = ?1 AND p.key = 'title' AND p.value_kind = 'text'
                 LIMIT 1",
                rusqlite::params![vault_path],
                |row| row.get::<_, String>(0),
            )
            .optional()
            .ok()
            .flatten()
            // `value_text` stores the JSON encoding of the property
            // value (a text property is a JSON string) — decode it;
            // fall back to the raw column for defensive robustness.
            .map(|raw| serde_json::from_str::<String>(&raw).unwrap_or(raw))
            .filter(|t| !t.trim().is_empty())
    }
}

/// Drop a file's canvas index rows (large-file refuse, file deleted).
/// Insert derived `oplog_events` rows for one file (O-6 #544). Works
/// on a bare connection (append path, autocommit) or inside the
/// rebuild transaction — `Transaction` derefs to `Connection`.
/// #831: the single retention-cutoff rule. Every consumer of the
/// retention window derives its boundary from THIS function — the
/// event-row producers (the [`insert_oplog_events`] filter), the
/// scan-time age-out, and the reconcile's log reclamation — so the
/// table a rebuild regenerates always converges with what
/// append-plus-age-out left behind (rebuild ≡ append). The boundary
/// convention matches the log-level fold
/// (`oplog_compaction::fold_boundary`): `ts <= cutoff` is out,
/// `ts > cutoff` is retained.
fn retention_cutoff_ms(retention_days: u32) -> i64 {
    now_ms() - i64::from(retention_days) * 24 * 60 * 60 * 1000
}

/// Insert derived event rows, applying the shared retention rule
/// (#831): rows at or before `cutoff_ms` are not written. Append-time
/// events are always fresh (the filter is a no-op there); full-log
/// rederivations — rebuild, post-compaction regen, deleted-file
/// recovery — drop the beyond-window slice a compaction would fold,
/// whether or not that compaction has run yet.
fn insert_oplog_events(
    conn: &Connection,
    file_id: i64,
    events: &[crate::oplog_events::DerivedEvent],
    cutoff_ms: i64,
) -> Result<(), rusqlite::Error> {
    let mut stmt = conn.prepare_cached(
        "INSERT INTO oplog_events (file_id, ts_ms, event_class, property_key, deleted_text)
         VALUES (?1, ?2, ?3, ?4, ?5)",
    )?;
    for event in events {
        if event.ts_ms <= cutoff_ms {
            continue;
        }
        stmt.execute(rusqlite::params![
            file_id,
            event.ts_ms,
            event.event_class,
            event.property_key,
            event.deleted_text,
        ])?;
    }
    Ok(())
}

fn purge_canvas_rows(tx: &rusqlite::Transaction, file_id: i64) -> Result<(), VaultError> {
    tx.execute(
        "DELETE FROM canvas_nodes WHERE file_id = ?1",
        rusqlite::params![file_id],
    )?;
    tx.execute(
        "DELETE FROM canvas_edges WHERE file_id = ?1",
        rusqlite::params![file_id],
    )?;
    Ok(())
}

/// Post-scan canvas pass: (re)derive index rows for every `.canvas`
/// file in the vault. See the call site in `scan_vault` for why this
/// runs after the walk and unconditionally.
fn reindex_all_canvases(
    tx: &rusqlite::Transaction,
    provider: &dyn VaultProvider,
    large_file_refuse_bytes: u64,
) -> Result<(), VaultError> {
    let canvases: Vec<(i64, String, i64)> = {
        let mut stmt = tx.prepare(
            "SELECT id, path, size_bytes FROM files WHERE extension = 'canvas' ORDER BY path",
        )?;
        let rows = stmt.query_map([], |row| {
            Ok((
                row.get::<_, i64>(0)?,
                row.get::<_, String>(1)?,
                row.get::<_, i64>(2)?,
            ))
        })?;
        rows.collect::<Result<Vec<_>, _>>()?
    };
    for (file_id, path, size_bytes) in canvases {
        if size_bytes as u64 > large_file_refuse_bytes {
            purge_canvas_rows(tx, file_id)?;
            continue;
        }
        let bytes = match provider.read_file_with_cap(&path, large_file_refuse_bytes) {
            Ok(b) => b,
            // File vanished between walk and pass, or unreadable:
            // leave rows for the next scan's delete-pruning rather
            // than failing the whole scan over one canvas.
            Err(_) => continue,
        };
        let source = String::from_utf8_lossy(&bytes);
        crate::canvas_db::replace_canvas_for_file(
            tx,
            file_id,
            &source,
            &DbTitleSource { conn: tx },
        )?;
    }
    Ok(())
}

fn bad_handle(handle: u64) -> VaultError {
    VaultError::InvalidArgument {
        message: format!("unknown canvas handle {handle} (closed or never opened)"),
    }
}

fn bad_node(node_id: &str) -> VaultError {
    VaultError::InvalidArgument {
        message: format!("canvas node {node_id:?} not found in this canvas"),
    }
}

impl VaultSession {
    /// Open a `.canvas` file: tolerant parse, model derivation, index
    /// refresh (one transaction), and a session-scoped handle for all
    /// further canvas calls. Warnings surface per t0 §5; a `degraded`
    /// canvas is read-only.
    pub fn open_canvas(&self, path: &str) -> Result<CanvasOpenInfo, VaultError> {
        let text = self.read_text(path)?;
        let mut conn = self.conn.lock().expect("session connection mutex");
        let tx = conn.transaction()?;

        // Ensure a files row exists (open-before-first-scan works).
        let existing: Option<i64> = tx
            .query_row(
                "SELECT id FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get(0),
            )
            .optional()?;
        let file_id = match existing {
            Some(id) => id,
            None => {
                let stat = self.provider.stat(path)?;
                let (name, extension, is_markdown) = classify_path(path);
                tx.execute(
                    "INSERT INTO files
                        (path, name, extension, size_bytes, mtime_ms, ctime_ms, birthtime_ms,
                         content_hash, parser_version, indexed_at_ms, is_markdown, body_text)
                     VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?11, ?7, ?8, ?9, ?10, '')",
                    rusqlite::params![
                        path,
                        name,
                        extension,
                        stat.size_bytes as i64,
                        stat.mtime_ms,
                        stat.ctime_ms,
                        content_hash(text.as_bytes()),
                        self.config.parser_version,
                        now_ms(),
                        is_markdown as i64,
                        stat.birthtime_ms,
                    ],
                )?;
                tx.query_row(
                    "SELECT id FROM files WHERE path = ?1",
                    rusqlite::params![path],
                    |row| row.get(0),
                )?
            }
        };

        let (parsed, warnings, model) = crate::canvas_db::replace_canvas_for_file(
            &tx,
            file_id,
            &text,
            &DbTitleSource { conn: &tx },
        )?;
        tx.commit()?;
        drop(conn);

        let degraded = crate::canvas::is_load_degraded(&warnings);
        let info_warnings = warnings.iter().map(load_warning).collect();
        let info = CanvasOpenInfo {
            handle: self
                .next_canvas_handle
                .fetch_add(1, std::sync::atomic::Ordering::SeqCst),
            node_count: parsed.nodes.len() as u32,
            edge_count: parsed.edges.len() as u32,
            degraded,
            warnings: info_warnings,
        };
        self.canvases.lock().expect("canvas registry mutex").insert(
            info.handle,
            OpenCanvasState {
                path: path.to_string(),
                file_id,
                canvas: parsed,
                model,
                content_hash: content_hash(text.as_bytes()),
                degraded,
            },
        );
        Ok(info)
    }

    /// Release a canvas handle. Idempotent: closing an unknown handle
    /// is a no-op (close paths race with teardown in the UI).
    pub fn close_canvas(&self, handle: u64) {
        self.canvases
            .lock()
            .expect("canvas registry mutex")
            .remove(&handle);
    }

    /// Depth-first outline rows, one per node, in reading order — a
    /// single indexed query against the derived columns (§K).
    pub fn canvas_outline(&self, handle: u64) -> Result<Vec<CanvasOutlineRow>, VaultError> {
        let file_id = self.canvas_file_id(handle)?;
        let conn = self.conn.lock().expect("session connection mutex");
        let mut stmt = conn.prepare_cached(
            "SELECT node_id, depth, kind, title, group_path, ordinal_n, total_m,
                    conn_count, color_name
             FROM canvas_nodes WHERE file_id = ?1 ORDER BY order_idx",
        )?;
        let rows = stmt.query_map(rusqlite::params![file_id], |row| {
            Ok(CanvasOutlineRow {
                node_id: row.get(0)?,
                depth: row.get::<_, i64>(1)? as u32,
                kind: row.get(2)?,
                title: row.get(3)?,
                group_path: parse_group_path(&row.get::<_, String>(4)?),
                ordinal_n: row.get::<_, i64>(5)? as u32,
                total_m: row.get::<_, i64>(6)? as u32,
                connection_count: row.get::<_, i64>(7)? as u32,
                color_name: row.get(8)?,
            })
        })?;
        Ok(rows.collect::<Result<Vec<_>, _>>()?)
    }

    /// Flat table rows in reading order; the table view sorts client-side
    /// per column (#519 v2 comparators).
    pub fn canvas_table_rows(&self, handle: u64) -> Result<Vec<CanvasTableRow>, VaultError> {
        let file_id = self.canvas_file_id(handle)?;
        let conn = self.conn.lock().expect("session connection mutex");
        let mut stmt = conn.prepare_cached(
            "SELECT node_id, kind, title, group_path, target, conn_count, color_name
             FROM canvas_nodes WHERE file_id = ?1 ORDER BY order_idx",
        )?;
        let rows = stmt.query_map(rusqlite::params![file_id], |row| {
            Ok(CanvasTableRow {
                node_id: row.get(0)?,
                kind: row.get(1)?,
                title: row.get(2)?,
                group_path: parse_group_path(&row.get::<_, String>(3)?),
                target: row.get(4)?,
                connection_count: row.get::<_, i64>(5)? as u32,
                color_name: row.get(6)?,
            })
        })?;
        Ok(rows.collect::<Result<Vec<_>, _>>()?)
    }

    /// A node's connections in document order, with directional data
    /// for phrasing (dangling edges never appear — model contract).
    pub fn canvas_neighbors(
        &self,
        handle: u64,
        node_id: &str,
    ) -> Result<Vec<CanvasNeighbor>, VaultError> {
        let canvases = self.canvases.lock().expect("canvas registry mutex");
        let state = canvases.get(&handle).ok_or_else(|| bad_handle(handle))?;
        let id = crate::canvas::NodeId(node_id.to_string());
        let neighbors = state
            .model
            .adjacency
            .get(&id)
            .ok_or_else(|| bad_node(node_id))?;
        Ok(neighbors
            .iter()
            .map(|n| CanvasNeighbor {
                edge_id: n.edge.0.clone(),
                other_node: n.other.0.clone(),
                other_title: state
                    .model
                    .summaries
                    .get(&n.other)
                    .map(|s| s.display_title.clone())
                    .unwrap_or_default(),
                direction: n.direction,
                self_side: n.self_side,
                label: n.label.clone(),
                self_is_from: n.self_is_from,
            })
            .collect())
    }

    /// The ⌃⌘I readback context for one node (t0 §1.4).
    pub fn canvas_where_am_i(
        &self,
        handle: u64,
        node_id: &str,
    ) -> Result<CanvasWhereAmI, VaultError> {
        let canvases = self.canvases.lock().expect("canvas registry mutex");
        let state = canvases.get(&handle).ok_or_else(|| bad_handle(handle))?;
        let id = crate::canvas::NodeId(node_id.to_string());
        let s = state
            .model
            .summaries
            .get(&id)
            .ok_or_else(|| bad_node(node_id))?;
        Ok(CanvasWhereAmI {
            node_id: node_id.to_string(),
            title: s.display_title.clone(),
            kind: s.kind_label.to_string(),
            group_path: s.group_path.clone(),
            ordinal_n: s.position_in_container as u32,
            total_m: s.container_size as u32,
            connection_count: s.connection_count as u32,
            in_count: s.in_count as u32,
            out_count: s.out_count as u32,
            color_name: s.color_name.clone(),
        })
    }

    /// Non-overlapping, grid-aligned position for a new card (#517).
    /// `exclude` removes nodes from collision checks (re-placing an
    /// existing card, #522).
    pub fn canvas_place_new(
        &self,
        handle: u64,
        anchor: Option<String>,
        width: f64,
        height: f64,
        direction_hint: Option<crate::canvas::placement::PlaceDirection>,
        exclude: Vec<String>,
    ) -> Result<CanvasPlacement, VaultError> {
        let canvases = self.canvases.lock().expect("canvas registry mutex");
        let state = canvases.get(&handle).ok_or_else(|| bad_handle(handle))?;
        let anchor_id = anchor.map(crate::canvas::NodeId);
        let exclude: Vec<crate::canvas::NodeId> =
            exclude.into_iter().map(crate::canvas::NodeId).collect();
        let p = crate::canvas::placement::place_new(
            &state.model,
            anchor_id.as_ref(),
            (width, height),
            direction_hint,
            &exclude,
        );
        Ok(CanvasPlacement {
            x: p.x,
            y: p.y,
            relative: p.relative,
        })
    }

    /// Rigid-set placement (#517 `place_set`): origins for each box,
    /// pairwise offsets preserved exactly.
    pub fn canvas_place_set(
        &self,
        handle: u64,
        anchor: Option<String>,
        boxes: Vec<CanvasRectArg>,
        direction_hint: Option<crate::canvas::placement::PlaceDirection>,
        exclude: Vec<String>,
    ) -> Result<CanvasSetPlacement, VaultError> {
        let canvases = self.canvases.lock().expect("canvas registry mutex");
        let state = canvases.get(&handle).ok_or_else(|| bad_handle(handle))?;
        let anchor_id = anchor.map(crate::canvas::NodeId);
        let exclude: Vec<crate::canvas::NodeId> =
            exclude.into_iter().map(crate::canvas::NodeId).collect();
        let rects: Vec<crate::canvas::model::Rect> = boxes
            .iter()
            .map(|b| crate::canvas::model::Rect::new(b.x, b.y, b.width, b.height))
            .collect();
        let sp = crate::canvas::placement::place_set(
            &state.model,
            anchor_id.as_ref(),
            &rects,
            direction_hint,
            &exclude,
        );
        Ok(CanvasSetPlacement {
            origins: sp.origins,
            relative: sp.relative,
        })
    }

    /// Node ids whose rects overlap `rect` (positive-area, cards only)
    /// — the move/resize-mode transient overlap warning query (#521).
    pub fn canvas_check_overlap(
        &self,
        handle: u64,
        rect: CanvasRectArg,
        exclude: Vec<String>,
    ) -> Result<Vec<String>, VaultError> {
        let canvases = self.canvases.lock().expect("canvas registry mutex");
        let state = canvases.get(&handle).ok_or_else(|| bad_handle(handle))?;
        let exclude: Vec<crate::canvas::NodeId> =
            exclude.into_iter().map(crate::canvas::NodeId).collect();
        Ok(state
            .model
            .spatial
            .overlapping(
                crate::canvas::model::Rect::new(rect.x, rect.y, rect.width, rect.height),
                &exclude,
                false,
            )
            .into_iter()
            .map(|n| n.0)
            .collect())
    }

    /// Apply one committed user action to an open canvas: mutate the
    /// typed model (atomic per action), serialize (#366), conflict-
    /// checked atomic write, reindex, refresh the handle's model — and
    /// return the inverse action for the undo stack (#372 consumes).
    ///
    /// One `canvas_apply` = one write = one undo step; bulk marked-set
    /// operations batch their ops into one action (t1 pipeline).
    pub fn canvas_apply(
        &self,
        handle: u64,
        action: crate::canvas::apply::CanvasAction,
    ) -> Result<CanvasApplyResult, VaultError> {
        let mut canvases = self.canvases.lock().expect("canvas registry mutex");
        let state = canvases
            .get_mut(&handle)
            .ok_or_else(|| bad_handle(handle))?;
        if state.degraded {
            return Err(VaultError::InvalidArgument {
                message: "canvas failed to load and is read-only (t0 §5); \
                          fix the file on disk and reopen"
                    .to_string(),
            });
        }

        // Mutate a working copy; `apply` guarantees all-or-nothing.
        let mut working = state.canvas.clone();
        let inverse = crate::canvas::apply::apply(&mut working, &action).map_err(|e| {
            VaultError::InvalidArgument {
                message: format!("canvas action {:?} rejected: {e}", action.name),
            }
        })?;

        // Serialize + conflict-checked atomic write + reindex (the
        // save path's canvas branch re-derives the DB rows in the same
        // transaction). Conflict = external writer changed the file
        // since open/last apply → typed WriteConflict for t0 §5.
        let new_text = crate::canvas::serialize::serialize(&working);
        let mut conn = self.conn.lock().expect("session connection mutex");
        let report = self.save_text_locked(
            &mut conn,
            &state.path,
            &new_text,
            Some(&state.content_hash),
            &[],
        )?;

        // Refresh the handle: new parse-equivalent state + model.
        let tx = conn.transaction()?;
        let model = crate::canvas::model::derive_with(&working, &DbTitleSource { conn: &tx });
        drop(tx);
        // The save above allocated/resolved the binding; read it before
        // releasing the connection so the semantic entry lands in the
        // same log (O-1: names come from the column, never `files.id`).
        let log_name: Option<String> = conn
            .query_row(
                "SELECT oplog_name FROM files WHERE id = ?1",
                rusqlite::params![state.file_id],
                |row| row.get(0),
            )
            .optional()
            .ok()
            .flatten()
            .flatten();
        state.canvas = working;
        state.model = model;
        let hash_before = state.content_hash.clone();
        state.content_hash = report.new_content_hash.clone();

        // Semantic journal entry (#372): named action + inverse beside
        // the byte-level text entry the save just wrote. Best-effort,
        // same discipline as append_save_to_oplog — a logging hiccup
        // must never fail the user's committed action.
        //
        // Appended while the session connection lock is STILL HELD
        // (#797, codex): the version-list fold pairs this record with
        // its byte entry by adjacency, and an in-process save slipping
        // between the two appends would split them. Cross-process
        // writers can still interleave — then the record renders as a
        // standalone "canvas action" row (the pre-fold behavior for
        // that one action; pinned by test), never a mis-fold.
        let payload = serde_json::json!({
            "name": action.name,
            "action": crate::canvas::apply::action_to_json(&action),
            "inverse": crate::canvas::apply::action_to_json(&inverse),
        });
        let entry = crate::oplog::OpLogEntry {
            timestamp_ms: now_ms(),
            user_actor_id: self.config.user_actor_id.clone(),
            op_kind: crate::oplog::OpKind::CanvasApply,
            content_hash_before: hash_before,
            content_hash_after: report.new_content_hash.clone(),
            payload_bytes: payload.to_string().into_bytes(),
        };
        let append_result = match log_name.as_deref() {
            Some(name) => {
                crate::oplog::append_entry(&self.config.cache_dir, name, &state.path, &entry)
                    .map(|_| ())
            }
            // Save append was skipped (no binding — allocation failed);
            // treat the journal entry the same way, loudly.
            None => Err(std::io::Error::other("no op-log binding for file")),
        };
        if let Err(e) = append_result {
            // Non-fatal, same discipline as the save/anchor append sites
            // (#507): the committed canvas write already succeeded; only the
            // semantic journal entry (undo/audit metadata) is missing. Route
            // through the facade instead of swallowing so a host can see the
            // observability gap — warn carries the file id + error *kind*
            // only, the path rides the debug line (see lib.rs privacy rule).
            log::warn!(
                "canvas oplog journal append failed for file_id={}: {}",
                state.file_id,
                e.kind()
            );
            log::debug!(
                "canvas oplog journal append failure for path {:?}: {e}",
                state.path
            );
        }
        drop(conn);

        // #802: the Modified event fired inside `save_text_locked`
        // above — the canvas serialization commits through the same
        // seam as every text write, so no extra emission here.
        Ok(CanvasApplyResult {
            new_content_hash: report.new_content_hash,
            inverse,
        })
    }

    /// The full render scene (nodes with geometry, in document order —
    /// the renderer's z-order tiebreak — plus edges). One call per
    /// open/mutation; the renderer windows its own materialization.
    pub fn canvas_scene(
        &self,
        handle: u64,
    ) -> Result<(Vec<CanvasSceneNode>, Vec<CanvasSceneEdge>), VaultError> {
        let canvases = self.canvases.lock().expect("canvas registry mutex");
        let state = canvases.get(&handle).ok_or_else(|| bad_handle(handle))?;
        let nodes = state
            .canvas
            .nodes
            .iter()
            .map(|node| {
                let summary = &state.model.summaries[&node.id];
                CanvasSceneNode {
                    node_id: node.id.0.clone(),
                    kind: summary.kind_label.to_string(),
                    title: summary.display_title.clone(),
                    x: node.x,
                    y: node.y,
                    width: node.width,
                    height: node.height,
                    color: node.color.as_ref().map(|c| match c {
                        crate::canvas::CanvasColor::Preset(p) => p.to_string(),
                        crate::canvas::CanvasColor::Hex(h) => h.clone(),
                    }),
                    color_name: node.color.as_ref().map(crate::canvas::color_name),
                    subpath: match &node.kind {
                        crate::canvas::NodeKind::File { subpath, .. } => subpath.clone(),
                        _ => None,
                    },
                }
            })
            .collect();
        let edges = state
            .canvas
            .edges
            .iter()
            .map(|edge| CanvasSceneEdge {
                edge_id: edge.id.0.clone(),
                from_node: edge.from.0.0.clone(),
                from_side: edge.from.1,
                to_node: edge.to.0.0.clone(),
                to_side: edge.to.1,
                from_arrow: edge.from_end == crate::canvas::EndStyle::Arrow,
                to_arrow: edge.to_end == crate::canvas::EndStyle::Arrow,
                label: edge.label.clone(),
                color: edge.color.as_ref().map(|c| match c {
                    crate::canvas::CanvasColor::Preset(p) => p.to_string(),
                    crate::canvas::CanvasColor::Hex(h) => h.clone(),
                }),
            })
            .collect();
        Ok((nodes, edges))
    }

    /// A text card's markdown content (interim read-only detail panel,
    /// t2 §#362; the real editor replaces it in Wave 4). `None` for
    /// non-text cards.
    pub fn canvas_node_text(
        &self,
        handle: u64,
        node_id: &str,
    ) -> Result<Option<String>, VaultError> {
        let canvases = self.canvases.lock().expect("canvas registry mutex");
        let state = canvases.get(&handle).ok_or_else(|| bad_handle(handle))?;
        let node = state
            .canvas
            .nodes
            .iter()
            .find(|n| n.id.0 == node_id)
            .ok_or_else(|| bad_node(node_id))?;
        Ok(match &node.kind {
            crate::canvas::NodeKind::Text { text } => Some(text.clone()),
            _ => None,
        })
    }

    fn canvas_file_id(&self, handle: u64) -> Result<i64, VaultError> {
        self.canvases
            .lock()
            .expect("canvas registry mutex")
            .get(&handle)
            .map(|s| s.file_id)
            .ok_or_else(|| bad_handle(handle))
    }
}

fn parse_group_path(json: &str) -> Vec<String> {
    serde_json::from_str(json).unwrap_or_default()
}

fn load_warning(w: &crate::canvas::CanvasWarning) -> CanvasLoadWarning {
    use crate::canvas::CanvasWarning as W;
    let (kind, detail) = match w {
        W::ParseFailed { reason } => (CanvasLoadWarningKind::ParseFailed, reason.clone()),
        W::MalformedNode { index, reason } => (
            CanvasLoadWarningKind::SkippedEntry,
            format!("node {index}: {reason}"),
        ),
        W::MalformedEdge { index, reason } => (
            CanvasLoadWarningKind::SkippedEntry,
            format!("connection {index}: {reason}"),
        ),
        W::UnknownNodeType { index, node_type } => (
            CanvasLoadWarningKind::SkippedEntry,
            format!("node {index}: unsupported type {node_type:?}"),
        ),
        W::DuplicateId { index, id, .. } => (
            CanvasLoadWarningKind::SkippedEntry,
            format!("entry {index}: duplicate id {id:?}"),
        ),
        W::DanglingEdge {
            edge_id,
            missing_node,
        } => (
            CanvasLoadWarningKind::DanglingEdge,
            format!(
                "connection {:?} references missing node {missing_node:?}",
                edge_id.0
            ),
        ),
        W::IgnoredValue {
            index, key, detail, ..
        } => (
            CanvasLoadWarningKind::IgnoredValue,
            format!("entry {index}, {key:?}: {detail}"),
        ),
    };
    CanvasLoadWarning { kind, detail }
}

#[cfg(test)]
mod tests {
    //! Tests for `VaultSession`. The 169-test suite that used to live
    //! inline here is split across `src/session/tests/*.rs` so each
    //! subsystem owns a navigable file. See #272.
    //!
    //! Each sub-module is included via `#[path]` so the tests retain
    //! private access to `VaultSession` internals (e.g. `session.conn`).

    use super::*;
    use chrono::NaiveDate;

    #[path = "common.rs"]
    mod common;

    #[path = "scan.rs"]
    mod scan;

    #[path = "files.rs"]
    mod files;

    #[path = "dir_tree.rs"]
    mod dir_tree;

    #[path = "structural.rs"]
    mod structural;

    #[path = "link_integrity.rs"]
    mod link_integrity;

    #[path = "search.rs"]
    mod search;

    #[path = "links_embeds.rs"]
    mod links_embeds;

    #[path = "properties.rs"]
    mod properties;

    #[path = "save.rs"]
    mod save;

    #[path = "oplog_identity.rs"]
    mod oplog_identity;

    #[path = "oplog_compaction_session.rs"]
    mod oplog_compaction_session;

    #[path = "oplog_history.rs"]
    mod oplog_history;

    #[path = "oplog_since_open.rs"]
    mod oplog_since_open;

    #[path = "oplog_logging.rs"]
    mod oplog_logging;

    #[path = "oplog_temporal.rs"]
    mod oplog_temporal;

    #[path = "composed.rs"]
    mod composed;

    #[path = "templates.rs"]
    mod templates;

    #[path = "tasks.rs"]
    mod tasks;

    #[path = "misc.rs"]
    mod misc;

    #[path = "events.rs"]
    mod events;

    #[path = "sync.rs"]
    mod sync;

    #[path = "citations.rs"]
    mod citations;

    #[path = "reading.rs"]
    mod reading;

    #[path = "canvas.rs"]
    mod canvas;

    #[path = "bases.rs"]
    mod bases;
}
