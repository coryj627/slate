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
use std::sync::atomic::{AtomicBool, Ordering};
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
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum FileFilter {
    /// Every indexed file.
    All,
    /// Only files whose extension marks them as Markdown.
    MarkdownOnly,
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
    oplog_state: Mutex<std::collections::HashMap<i64, OplogAppendState>>,
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
}

impl VaultSession {
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

        Ok(Self {
            provider,
            conn: Mutex::new(conn),
            config,
            oplog_state: Mutex::new(std::collections::HashMap::new()),
            math_prefs,
            bib_index: Mutex::new(bib_index),
            csl_styles: Mutex::new(std::collections::HashMap::new()),
            render_cache: crate::citations::render::RenderCache::default(),
        })
    }

    /// Convenience: open a vault rooted at `root` using `FsVaultProvider`.
    /// Cache lives at `<root>/.slate` per the locked storage layout.
    ///
    /// The vault root must already exist as a directory. A typo'd path
    /// would otherwise `create_dir_all` its way into existence under
    /// `open`, silently materializing a fresh empty vault on disk.
    pub fn from_filesystem(root: PathBuf) -> Result<Self, VaultError> {
        if !root.is_dir() {
            return Err(VaultError::InvalidPath {
                path: root.display().to_string(),
                reason: "vault root does not exist or is not a directory".into(),
            });
        }
        let cache_dir = root.join(".slate");
        let mut config = SessionConfig::new(cache_dir);
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
        let mut conn = self.conn.lock().expect("session connection mutex");
        scan_vault(
            self.provider.as_ref(),
            &mut conn,
            self.config.parser_version,
            self.config.large_file_refuse_bytes,
            cancel,
            listener.as_deref(),
        )
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
    ///   track the on-disk hash (CLI, scripted writers).
    /// - `Some(h)` → before writing, stat + hash the file currently
    ///   on disk. If it doesn't match `h`, return
    ///   `VaultError::WriteConflict` and leave the file untouched.
    ///   This is the path the Mac editor uses to detect external
    ///   changes between read and save.
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
        self.save_text_locked(&mut conn, path, contents, expected_content_hash)
    }

    /// Body of `save_text` minus the path validation, size check, and
    /// mutex acquisition. Callers must hold the session connection
    /// mutex (via `conn`) for the duration of this call.
    ///
    /// Exists so `toggle_task_status` can hold the mutex across its
    /// read+parse+rewrite+save sequence — passing a stale-pre-read
    /// payload to a non-locked `save_text` was the lost-update race
    /// fixed in #135.
    fn save_text_locked(
        &self,
        conn: &mut Connection,
        path: &str,
        contents: &str,
        expected_content_hash: Option<&str>,
    ) -> Result<SaveReport, VaultError> {
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
                let cached = conn
                    .query_row(
                        "SELECT content_hash FROM files WHERE path = ?1",
                        rusqlite::params![path],
                        |row| row.get::<_, String>(0),
                    )
                    .optional()?
                    .unwrap_or_default();
                (cached, None)
            };

        // Atomic write happens before the index update so that a
        // subsequent SQLite failure leaves the file on disk in a
        // consistent state. Worst case: the file is newer than the
        // index, and the next scan picks it up via mtime/size/ctime.
        self.provider.write_file(path, contents.as_bytes())?;

        let new_stat = self.provider.stat(path)?;
        let new_hash = crate::vault::content_hash(contents.as_bytes());

        let now = now_ms();
        let (name, extension, is_markdown) = classify_path(path);

        // Body text for FTS5: only markdown gets indexed; everything
        // else stores "" so the trigger on `body_text` makes the
        // file_fts row consistent (or absent, via is_markdown gating
        // in the migration-006 triggers).
        let body_text: &str = if is_markdown { contents } else { "" };

        let tx = conn.transaction()?;
        tx.execute(
            "INSERT INTO files
                (path, name, extension, size_bytes, mtime_ms, ctime_ms,
                 content_hash, parser_version, indexed_at_ms, is_markdown, body_text)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11)
             ON CONFLICT(path) DO UPDATE SET
                name           = excluded.name,
                extension      = excluded.extension,
                size_bytes     = excluded.size_bytes,
                mtime_ms       = excluded.mtime_ms,
                ctime_ms       = excluded.ctime_ms,
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
            replace_headings(&tx, file_id, contents)?;
            crate::links_db::replace_links_for_file(&tx, file_id, path, contents, &vault_index)?;
            crate::properties_db::replace_properties_for_file(&tx, file_id, contents)?;
            crate::tasks_db::replace_tasks_for_file(&tx, file_id, contents)?;
            crate::blocks_db::replace_blocks_for_file(&tx, file_id, contents)?;
            crate::citations_db::replace_citations_for_file(&tx, file_id, contents)?;
        }
        tx.commit()?;

        // Op-log append: best-effort (#378). A logging-disk hiccup must
        // not throw away the user's just-saved text, so all of the
        // diff/encode/append work below swallows its errors.
        self.append_save_to_oplog(
            file_id,
            path,
            &hash_before,
            &new_hash,
            contents,
            old_contents.as_deref(),
            now,
        );

        Ok(SaveReport {
            new_content_hash: new_hash,
            new_size_bytes: new_stat.size_bytes,
            new_mtime_ms: new_stat.mtime_ms,
        })
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
    /// save (empty diff) writes nothing. Otherwise: one `EditBatch`.
    ///
    /// Best-effort throughout: any error logs a warning and returns,
    /// leaving the (already durable) file save untouched.
    #[allow(clippy::too_many_arguments)]
    fn append_save_to_oplog(
        &self,
        file_id: i64,
        path: &str,
        hash_before: &str,
        new_hash: &str,
        new_contents: &str,
        old_contents: Option<&str>,
        now: i64,
    ) {
        let mut state = self.oplog_state.lock().expect("oplog state mutex");
        let cached = state.get(&file_id).cloned();

        let snapshot = || crate::oplog::OpLogEntry {
            timestamp_ms: now,
            user_actor_id: self.config.user_actor_id.clone(),
            op_kind: crate::oplog::OpKind::WholeFileReplace,
            content_hash_before: hash_before.to_string(),
            content_hash_after: new_hash.to_string(),
            payload_bytes: new_contents.as_bytes().to_vec(),
        };

        // (entry, bytes_since_snapshot_after). `None` ⇒ write nothing.
        let decision: Option<(crate::oplog::OpLogEntry, u64)> = match (old_contents, &cached) {
            (Some(old), Some(c)) if c.last_hash_after == hash_before => {
                let ops = crate::diff::diff_to_ops(old, new_contents);
                if ops.is_empty() {
                    None // identical content — don't grow the log
                } else {
                    let payload = crate::oplog::encode_edit_batch(&ops);
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
                        Some((snapshot(), 0))
                    } else {
                        let entry = crate::oplog::OpLogEntry {
                            timestamp_ms: now,
                            user_actor_id: self.config.user_actor_id.clone(),
                            op_kind: crate::oplog::OpKind::EditBatch,
                            content_hash_before: hash_before.to_string(),
                            content_hash_after: new_hash.to_string(),
                            payload_bytes: payload,
                        };
                        Some((entry, projected))
                    }
                }
            }
            // Cold cache, misaligned, None path, or non-UTF-8 old → snapshot.
            _ => Some((snapshot(), 0)),
        };

        let Some((entry, bytes_since_snapshot)) = decision else {
            return; // nothing to write (identical save)
        };

        if let Err(e) = crate::oplog::append_entry(&self.config.cache_dir, file_id, &entry) {
            eprintln!("warning: oplog append failed for file_id={file_id} at {path:?}: {e}");
            return; // leave the cache untouched so the next save re-snapshots
        }
        state.insert(
            file_id,
            OplogAppendState {
                last_hash_after: new_hash.to_string(),
                bytes_since_snapshot,
            },
        );
    }

    /// Read every well-formed op-log entry recorded for `path`.
    ///
    /// Returns `Ok(Vec::new())` if the path isn't indexed yet or has
    /// never been saved through `save_text`. A torn trailing entry
    /// (e.g. crash mid-append) is silently dropped; the returned
    /// vector is the well-formed prefix.
    pub fn read_oplog(&self, path: &str) -> Result<Vec<crate::oplog::OpLogEntry>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        let file_id: Option<i64> = conn
            .query_row(
                "SELECT id FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get(0),
            )
            .optional()?;
        drop(conn);
        let Some(file_id) = file_id else {
            return Ok(Vec::new());
        };
        crate::oplog::read_oplog(&self.config.cache_dir, file_id).map_err(VaultError::Io)
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

        self.save_text_locked(&mut conn, path, &new_contents, expected_content_hash)
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

        self.save_text_locked(&mut conn, path, &new_contents, expected_content_hash)
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

        self.save_text_locked(&mut conn, path, &new_contents, expected_content_hash)
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
        Ok(NotePartsBundle {
            fm_source: parts.fm_source,
            body: parts.body,
            content_hash,
            mtime_ms,
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

        self.save_text_locked(&mut conn, path, &composed, expected_content_hash.as_deref())
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
    /// narrow to a folder; `cancel` cooperates the same way it does
    /// for `scan_initial` — the result-collection loop checks the
    /// token between rows. Reserved scopes (`File`, `Tag`) return
    /// `VaultError::Cancelled` for now.
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
        // Use the atomic verify-and-read instead of `read_text` so a
        // symlink can't be swapped between the scope check and the
        // open (#132, Codoki PR #153 follow-up). The provider's
        // `resolve` step inside this call rejects `..` / absolute
        // paths textually before the canonicalize, matching what
        // `read_text` did. Errors surface as-is: the picker drops
        // bad entries silently, but explicit render attempts get a
        // real error so the host can show "refused for safety" or
        // "template not found" cleanly.
        let limit = self.config.large_file_refuse_bytes;
        let bytes = self.provider.read_in_vault_with_cap(template_path, limit)?;
        if (bytes.len() as u64) > limit {
            return Err(VaultError::FileTooLarge {
                path: template_path.to_string(),
                size: bytes.len() as u64,
            });
        }
        let source = String::from_utf8(bytes).map_err(|_| VaultError::InvalidUtf8 {
            path: template_path.to_string(),
        })?;
        Ok(crate::render_template_source(&source, &context))
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

    /// Resolve the vault root for bibliography path resolution.
    /// `.slate` is always at `<root>/.slate`, so the cache dir's
    /// parent is the canonical lookup. Errors only if `cache_dir`
    /// itself has no parent — defensive for in-memory test setups.
    fn vault_root_for_bibliography(&self) -> Result<std::path::PathBuf, VaultError> {
        self.config
            .cache_dir
            .parent()
            .map(std::path::Path::to_path_buf)
            .ok_or_else(|| VaultError::Unsupported {
                feature: "bibliography on non-filesystem vault".to_string(),
            })
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
    // Open the transaction *before* emitting Started so a tx-open
    // failure can't leave listeners stuck waiting for a terminal
    // event. If conn.transaction() fails here, the listener has
    // observed nothing and the error flows through the Result.
    let tx = conn.transaction()?;

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

    let mut stack: Vec<String> = vec![String::new()];
    while let Some(dir) = stack.pop() {
        if cancel.is_cancelled() {
            bail_cancelled_after_started!();
        }

        let entries = match provider.list_dir(&dir) {
            Ok(e) => e,
            Err(e) => {
                report.errors.push(format!("list_dir {dir:?}: {e}"));
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
        .query_row(
            "SELECT mtime_ms, size_bytes, ctime_ms FROM files WHERE path = ?1",
            rusqlite::params![path],
            |row| {
                Ok((
                    row.get::<_, i64>(0)?,
                    row.get::<_, i64>(1)?,
                    row.get::<_, i64>(2)?,
                ))
            },
        )
        .optional()?;
    if let Some((db_mtime_ms, db_size_bytes, db_ctime_ms)) = existing {
        let mtime_size_match =
            db_mtime_ms == stat.mtime_ms && db_size_bytes == stat.size_bytes as i64;
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
            tx.execute(
                "UPDATE files SET
                    indexed_at_ms = ?1,
                    ctime_ms = CASE WHEN ?2 != 0 THEN ?2 ELSE ctime_ms END
                 WHERE path = ?3",
                rusqlite::params![now, stat.ctime_ms, path],
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
                (path, name, extension, size_bytes, mtime_ms, ctime_ms,
                 content_hash, parser_version, indexed_at_ms, is_markdown, body_text)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, '')
             ON CONFLICT(path) DO UPDATE SET
                name           = excluded.name,
                extension      = excluded.extension,
                size_bytes     = excluded.size_bytes,
                mtime_ms       = excluded.mtime_ms,
                ctime_ms       = excluded.ctime_ms,
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

    tx.execute(
        "INSERT INTO files
            (path, name, extension, size_bytes, mtime_ms, ctime_ms,
             content_hash, parser_version, indexed_at_ms, is_markdown, body_text)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11)
         ON CONFLICT(path) DO UPDATE SET
            name           = excluded.name,
            extension      = excluded.extension,
            size_bytes     = excluded.size_bytes,
            mtime_ms       = excluded.mtime_ms,
            ctime_ms       = excluded.ctime_ms,
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
            hash,
            parser_version,
            now,
            is_markdown as i64,
            body_text,
        ],
    )?;

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
        let file_id: i64 = tx.query_row(
            "SELECT id FROM files WHERE path = ?1",
            rusqlite::params![path],
            |row| row.get(0),
        )?;
        // Reuse the already-decoded `body_text` so we don't pay the
        // utf8_lossy cost twice (once for FTS, once for parsers).
        index_markdown_derivatives(tx, file_id, path, body_text.as_str(), vault_index)?;
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
) -> Result<(), VaultError> {
    replace_headings(tx, file_id, body_text)?;
    crate::links_db::replace_links_for_file(tx, file_id, path, body_text, vault_index)?;
    crate::properties_db::replace_properties_for_file(tx, file_id, body_text)?;
    crate::tasks_db::replace_tasks_for_file(tx, file_id, body_text)?;
    crate::blocks_db::replace_blocks_for_file(tx, file_id, body_text)?;
    crate::citations_db::replace_citations_for_file(tx, file_id, body_text)?;
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
    crate::tasks_db::replace_tasks_for_file(tx, file_id, "")?;
    crate::blocks_db::replace_blocks_for_file(tx, file_id, "")?;
    crate::citations_db::replace_citations_for_file(tx, file_id, "")?;
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
/// empties, lone `.`, absolutes, `..`. The provider's
/// `resolve_for_mutation` enforces the same rules at write time, but
/// rejecting up-front means a `save_text("", b"x", Some(hash))` call
/// fails as `InvalidPath` rather than tripping a read-side IO error
/// during the conflict check.
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
            Component::Normal(_) => has_normal = true,
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
        self.structural_move_folder(
            path,
            &new_path,
            crate::structural::StructuralOpKind::RenameFolder,
        )
    }

    /// Move a folder under a new parent ("" = vault root).
    pub fn move_folder(
        &self,
        path: &str,
        new_parent: &str,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        let new_path = child_path(new_parent, leaf_name(path))?;
        self.structural_move_folder(
            path,
            &new_path,
            crate::structural::StructuralOpKind::MoveFolder,
        )
    }

    /// Rename a file in place.
    pub fn rename_file(
        &self,
        path: &str,
        new_name: &str,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        let new_path = sibling_path(path, new_name)?;
        self.structural_move_file(
            path,
            &new_path,
            crate::structural::StructuralOpKind::RenameFile,
        )
    }

    /// Move a file under a new parent ("" = vault root).
    pub fn move_file(
        &self,
        path: &str,
        new_parent: &str,
    ) -> Result<crate::structural::StructuralReport, VaultError> {
        let new_path = child_path(new_parent, leaf_name(path))?;
        self.structural_move_file(
            path,
            &new_path,
            crate::structural::StructuralOpKind::MoveFile,
        )
    }

    /// Move a file to the system trash. Journaled for auditability; NOT
    /// undoable via `undo_structural` (the bytes are in the trash).
    pub fn delete_file(&self, path: &str) -> Result<(), VaultError> {
        validate_save_path(path)?;
        let conn = self.conn.lock().expect("session connection mutex");
        self.provider.delete(path)?;
        self.with_structural_tx(conn, |tx| {
            tx.execute("DELETE FROM files WHERE path = ?1", rusqlite::params![path])?;
            journal_append(
                tx,
                crate::structural::StructuralOpKind::DeleteFile,
                &crate::structural::StructuralOpPayload {
                    from: path.to_string(),
                    to: path.to_string(),
                    ..Default::default()
                },
            )
        })?;
        Ok(())
    }

    /// Move a folder (recursively) to the system trash. Journaled; not
    /// undoable via `undo_structural`.
    pub fn delete_folder(&self, path: &str) -> Result<(), VaultError> {
        validate_save_path(path)?;
        let conn = self.conn.lock().expect("session connection mutex");
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
                self.structural_move_folder(&payload.to, &payload.from, kind)?
            }
            StructuralOpKind::RenameFile | StructuralOpKind::MoveFile => {
                self.structural_move_file(&payload.to, &payload.from, kind)?
            }
            StructuralOpKind::DeleteFile | StructuralOpKind::DeleteFolder => {
                unreachable!("undoable() filtered deletes above")
            }
        };

        // Restore U2-3 rewrites byte-exactly: each rewritten file goes back
        // to its pre-op bytes via the per-file op-log, guarded by the
        // recorded post-op hash so an external edit since the op surfaces
        // as a per-file WriteConflict in the report, never a clobber.
        for rewrite in &payload.rewrites {
            match self.restore_file_to_hash(
                &rewrite.path,
                &rewrite.hash_before,
                &rewrite.hash_after,
            ) {
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
        Ok(report)
    }

    // ----- internals -----

    fn structural_move_folder(
        &self,
        from: &str,
        to: &str,
        kind: crate::structural::StructuralOpKind,
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
        let tx_result = self.with_structural_tx(conn, |tx| {
            rename_prefix_in_index(tx, from, to)?;
            journal_append(
                tx,
                kind,
                &crate::structural::StructuralOpPayload {
                    from: from.to_string(),
                    to: to.to_string(),
                    moved: moved.clone(),
                    ..Default::default()
                },
            )
        });
        let op_id = match tx_result {
            Ok(id) => id,
            Err(e) => {
                // Best-effort fs revert so state can't silently split.
                let _ = self.provider.rename(to, from);
                return Err(e);
            }
        };
        Ok(crate::structural::StructuralReport {
            op_id,
            moved,
            rewritten: Vec::new(),
            failed: Vec::new(),
        })
    }

    fn structural_move_file(
        &self,
        from: &str,
        to: &str,
        kind: crate::structural::StructuralOpKind,
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

        self.provider.rename(from, to)?;
        let (name, extension, is_markdown) = classify_path(to);
        let tx_result = self.with_structural_tx(conn, |tx| {
            tx.execute(
                "UPDATE files SET path = ?1, name = ?2, extension = ?3, is_markdown = ?4
                 WHERE path = ?5",
                rusqlite::params![to, name, extension, is_markdown as i64, from],
            )?;
            journal_append(
                tx,
                kind,
                &crate::structural::StructuralOpPayload {
                    from: from.to_string(),
                    to: to.to_string(),
                    moved: vec![(from.to_string(), to.to_string())],
                    ..Default::default()
                },
            )
        });
        let op_id = match tx_result {
            Ok(id) => id,
            Err(e) => {
                let _ = self.provider.rename(to, from);
                return Err(e);
            }
        };
        Ok(crate::structural::StructuralReport {
            op_id,
            moved: vec![(from.to_string(), to.to_string())],
            rewritten: Vec::new(),
            failed: Vec::new(),
        })
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
        // Inline id lookup (NOT the public read_oplog — it takes the mutex
        // this method already holds).
        let file_id: Option<i64> = conn
            .query_row(
                "SELECT id FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get(0),
            )
            .optional()?;
        let Some(file_id) = file_id else {
            return Err(VaultError::InvalidPath {
                path: path.to_string(),
                reason: "no such file in the index".into(),
            });
        };
        let entries =
            crate::oplog::read_oplog(&self.config.cache_dir, file_id).map_err(VaultError::Io)?;
        let contents =
            crate::oplog::reconstruct_at_hash(&entries, hash_before).ok_or_else(|| {
                VaultError::InvalidArgument {
                    message: format!("op-log for {path:?} has no state with hash {hash_before}"),
                }
            })?;
        self.save_text_locked(&mut conn, path, &contents, Some(expected_current))
            .map(|_| ())
    }
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

    #[path = "search.rs"]
    mod search;

    #[path = "links_embeds.rs"]
    mod links_embeds;

    #[path = "properties.rs"]
    mod properties;

    #[path = "save.rs"]
    mod save;

    #[path = "composed.rs"]
    mod composed;

    #[path = "templates.rs"]
    mod templates;

    #[path = "tasks.rs"]
    mod tasks;

    #[path = "misc.rs"]
    mod misc;

    #[path = "citations.rs"]
    mod citations;

    #[path = "reading.rs"]
    mod reading;
}
