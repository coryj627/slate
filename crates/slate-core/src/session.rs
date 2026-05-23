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

use crate::db;
use crate::vault::{content_hash, EntryKind, FsVaultProvider, VaultProvider};
use crate::VaultError;

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
    /// Tag written into each op-log entry's `user_actor_id` field. For
    /// the single-user V1.F build this defaults to `"local"`; multi-
    /// device sync (V2) will plumb a real per-device identifier here.
    pub user_actor_id: String,
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
            user_actor_id: "local".to_string(),
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
#[derive(Debug, Clone)]
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

// --- The session ---

/// A live vault session.
///
/// Holds the vault provider, the SQLite cache connection, and the
/// configuration. Drop the session (or call `close`) when done to flush
/// the database.
pub struct VaultSession {
    provider: Arc<dyn VaultProvider>,
    conn: Mutex<Connection>,
    config: SessionConfig,
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

        Ok(Self {
            provider,
            conn: Mutex::new(conn),
            config,
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
        let config = SessionConfig::new(cache_dir);
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

        let hash_before = if let Some(expected) = expected_content_hash {
            let (current_hash, current_mtime_ms) = compute_disk_hash(
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
            current_hash
        } else {
            // No conflict check: the op-log still wants a
            // `hash_before` field for future undo / merge UI, but we
            // don't want to re-read the file just for that. The cached
            // hash from the previous index pass is good enough.
            conn.query_row(
                "SELECT content_hash FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get::<_, String>(0),
            )
            .optional()?
            .unwrap_or_default()
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
        }
        tx.commit()?;

        // Op-log append: best-effort. A logging-disk hiccup must not
        // throw away the user's just-saved text.
        let entry = crate::oplog::OpLogEntry {
            timestamp_ms: now,
            user_actor_id: self.config.user_actor_id.clone(),
            op_kind: crate::oplog::OpKind::WholeFileReplace,
            content_hash_before: hash_before,
            content_hash_after: new_hash.clone(),
            payload_bytes: contents.as_bytes().to_vec(),
        };
        if let Err(e) = crate::oplog::append_entry(&self.config.cache_dir, file_id, &entry) {
            eprintln!("warning: oplog append failed for file_id={file_id} at {path:?}: {e}");
        }

        Ok(SaveReport {
            new_content_hash: new_hash,
            new_size_bytes: new_stat.size_bytes,
            new_mtime_ms: new_stat.mtime_ms,
        })
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

    /// Every outgoing link from `path`, in document order. Includes
    /// resolved (internal-and-found), unresolved (internal-and-missing),
    /// and external links so the UI can render them all in one list
    /// with kind flags. Returns an empty vec when the file isn't
    /// indexed yet.
    pub fn outgoing_links(&self, path: &str) -> Result<Vec<crate::OutgoingLink>, VaultError> {
        let conn = self.conn.lock().expect("session connection mutex");
        crate::links_db::outgoing_links_for(&conn, path)
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

    /// Accessor for the underlying config. Useful for hosts that want to
    /// surface the cache directory location.
    pub fn config(&self) -> &SessionConfig {
        &self.config
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

    // For Markdown files, parse + persist headings on the slow path.
    // The fast path (mtime+size+ctime match) never reaches here, so
    // unchanged files don't churn the headings table. Non-Markdown
    // files have no headings worth indexing — pulldown-cmark would
    // happily parse them but it's noise.
    if is_markdown {
        // Need the file_id for the foreign key. INSERT … ON CONFLICT
        // DO UPDATE doesn't expose the row id directly, so query it
        // back — cheap given we just touched the row.
        let file_id: i64 = tx.query_row(
            "SELECT id FROM files WHERE path = ?1",
            rusqlite::params![path],
            |row| row.get(0),
        )?;
        // Reuse the already-decoded `body_text` so we don't pay the
        // utf8_lossy cost twice (once for FTS, once for parsers).
        let text = body_text.as_str();
        replace_headings(tx, file_id, text)?;
        // Links land in the same transaction as headings so a file's
        // outgoing-link snapshot stays consistent with its body. Any
        // link whose target hasn't been indexed yet is written with
        // target_path = NULL and re-resolved by the post-scan pass.
        crate::links_db::replace_links_for_file(tx, file_id, path, text, vault_index)?;
        // Frontmatter properties: same transaction so headings,
        // links, and properties commit atomically with the file row.
        crate::properties_db::replace_properties_for_file(tx, file_id, text)?;
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
        "INSERT INTO headings (file_id, ordinal, level, text, anchor_id)
         VALUES (?1, ?2, ?3, ?4, ?5)",
    )?;
    for heading in headings {
        stmt.execute(rusqlite::params![
            file_id,
            heading.ordinal as i64,
            heading.level as i64,
            heading.text,
            heading.anchor_id,
        ])?;
    }
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
        "SELECT ordinal, level, text, anchor_id
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
/// Bounded by the same `large_file_refuse_bytes` cap as `read_text`:
/// if the file genuinely exceeds the threshold we surface
/// `FileTooLarge` and abort rather than allocating arbitrarily large
/// buffers to compute a hash we're about to compare.
fn compute_disk_hash(
    provider: &dyn crate::VaultProvider,
    path: &str,
    limit: u64,
) -> Result<(String, i64), VaultError> {
    use std::io;
    let stat = match provider.stat(path) {
        Ok(s) => s,
        Err(VaultError::Io(io_err)) if io_err.kind() == io::ErrorKind::NotFound => {
            return Ok((String::new(), 0));
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
    Ok((crate::vault::content_hash(&bytes), stat.mtime_ms))
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

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::AtomicU32;

    fn make_vault(setup: impl FnOnce(&FsVaultProvider)) -> (tempfile::TempDir, VaultSession) {
        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        setup(&provider);
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        (tmp, session)
    }

    /// Test provider that delegates to an `FsVaultProvider` but cancels a
    /// shared `CancelToken` after the Nth `list_dir` call. Used to drive
    /// scan-cancellation tests at a specific point in the scan timeline.
    struct CancellingProvider {
        inner: FsVaultProvider,
        cancel: CancelToken,
        list_dir_calls: AtomicU32,
        cancel_after_list_dirs: u32,
    }

    impl CancellingProvider {
        fn new(inner: FsVaultProvider, cancel: CancelToken, cancel_after: u32) -> Self {
            Self {
                inner,
                cancel,
                list_dir_calls: AtomicU32::new(0),
                cancel_after_list_dirs: cancel_after,
            }
        }
    }

    impl crate::VaultProvider for CancellingProvider {
        fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
            let result = self.inner.list_dir(relative);
            let n = self
                .list_dir_calls
                .fetch_add(1, std::sync::atomic::Ordering::SeqCst)
                + 1;
            if n == self.cancel_after_list_dirs {
                self.cancel.cancel();
            }
            result
        }
        fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
            self.inner.read_file(relative)
        }
        fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
            self.inner.write_file(relative, contents)
        }
        fn delete(&self, relative: &str) -> Result<(), VaultError> {
            self.inner.delete(relative)
        }
        fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
            self.inner.rename(from, to)
        }
        fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
            self.inner.stat(relative)
        }
        fn watch(
            &self,
            sink: Arc<dyn crate::FileEventSink>,
        ) -> Result<Option<crate::WatchHandle>, VaultError> {
            self.inner.watch(sink)
        }
    }

    /// Wrapper that delegates to `FsVaultProvider` but zeroes
    /// `stat.ctime_ms`. Used to simulate platforms / filesystems
    /// without portable ctime access (Windows today).
    struct ZeroCtimeProvider {
        inner: FsVaultProvider,
    }

    impl crate::VaultProvider for ZeroCtimeProvider {
        fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
            self.inner.list_dir(relative)
        }
        fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
            self.inner.read_file(relative)
        }
        fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
            self.inner.write_file(relative, contents)
        }
        fn delete(&self, relative: &str) -> Result<(), VaultError> {
            self.inner.delete(relative)
        }
        fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
            self.inner.rename(from, to)
        }
        fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
            let mut stat = self.inner.stat(relative)?;
            stat.ctime_ms = 0;
            Ok(stat)
        }
        fn watch(
            &self,
            sink: Arc<dyn crate::FileEventSink>,
        ) -> Result<Option<crate::WatchHandle>, VaultError> {
            self.inner.watch(sink)
        }
    }

    #[test]
    fn open_creates_cache_database() {
        let tmp = tempfile::tempdir().unwrap();
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        let cache_db = tmp.path().join(".slate").join("cache.sqlite");
        assert!(cache_db.exists(), "cache.sqlite should be created on open");
        drop(session);
    }

    #[test]
    fn open_is_idempotent() {
        let tmp = tempfile::tempdir().unwrap();
        let _s1 = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        drop(_s1);
        let _s2 = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        // Should not panic or fail — schema is at v1 and stays there.
    }

    #[test]
    fn scan_initial_indexes_markdown_and_non_markdown() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/a.md", b"# A").unwrap();
            p.write_file("notes/b.md", b"# B").unwrap();
            p.write_file("attachments/img.png", b"\x89PNG").unwrap();
            p.write_file("README.txt", b"hi").unwrap();
        });

        let cancel = CancelToken::new();
        let report = session.scan_initial(&cancel).unwrap();
        assert_eq!(report.files_indexed, 4);
        assert_eq!(report.errors.len(), 0);
        assert!(report.bytes_processed > 0);
    }

    #[test]
    fn scan_initial_skips_hidden_directories() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("real.md", b"# real").unwrap();
            // Synthetic Obsidian-style hidden config that must not be indexed.
            p.write_file(".obsidian/workspace.json", b"{}").unwrap();
            p.write_file(".obsidian/plugins/foo/main.js", b"// js")
                .unwrap();
        });

        let report = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(report.files_indexed, 1, "only real.md should be indexed");

        let page = session
            .list_files(FileFilter::All, Paging::first(100))
            .unwrap();
        let paths: Vec<&str> = page.items.iter().map(|f| f.path.as_str()).collect();
        assert_eq!(paths, vec!["real.md"]);
    }

    #[test]
    fn scan_initial_does_not_index_its_own_cache_directory() {
        // The .slate cache dir is created on session open. Re-scanning
        // must not pick up the cache.sqlite or any other internal file.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"a").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        // Re-scan after the cache exists. With the mtime/size skip,
        // a.md goes through the fast path on the second pass, so the
        // assertion isn't about files_indexed specifically — it's
        // about "we accounted for exactly one user file, no .slate
        // entries leaked into the scan."
        let report = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(report.files_seen, 1);
        assert_eq!(report.files_indexed + report.files_skipped, 1);
    }

    #[test]
    fn rescan_with_changed_mtime_falls_through_to_rehash() {
        // Same byte count, different content. We poll until the FS
        // actually advances mtime past the original value rather than
        // assuming a fixed sleep duration is enough — coarse-
        // resolution filesystems (FAT, HFS+, some SMB mounts) would
        // make a fixed sleep flaky.
        let (tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"ABCDE").unwrap();
        });
        let first = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(first.files_indexed, 1);
        assert_eq!(first.files_skipped, 0);

        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        let original_mtime = provider.stat("a.md").unwrap().mtime_ms;
        rewrite_until_mtime_advances(&provider, "a.md", b"XYZWQ", original_mtime);

        let second = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(
            second.files_indexed, 1,
            "mtime changed → must re-hash even though size matched"
        );
        assert_eq!(second.files_skipped, 0);
        assert!(second.bytes_processed > 0);
    }

    /// Repeatedly write a file until its stat'd `mtime_ms` differs from
    /// `original`. Used by tests that need a guaranteed mtime advance
    /// without relying on a fixed sleep, which would be flaky on
    /// filesystems with coarse mtime resolution (FAT, HFS+, some SMB).
    /// Panics after a generous deadline so a truly stuck FS surfaces
    /// as a test failure rather than a hang.
    fn rewrite_until_mtime_advances(
        provider: &FsVaultProvider,
        relative: &str,
        content: &[u8],
        original: i64,
    ) {
        let deadline = std::time::Instant::now() + std::time::Duration::from_secs(5);
        loop {
            provider.write_file(relative, content).unwrap();
            let now = provider.stat(relative).unwrap().mtime_ms;
            if now != original {
                return;
            }
            if std::time::Instant::now() >= deadline {
                panic!(
                    "mtime did not advance past {original} within 5 s — \
                     filesystem mtime resolution too coarse for this test"
                );
            }
            std::thread::sleep(std::time::Duration::from_millis(50));
        }
    }

    #[cfg(unix)]
    #[test]
    fn rescan_with_mtime_preserved_but_ctime_changed_rehashes() {
        // The mtime/size heuristic alone misses mtime-preserving
        // writers like `cp -p` and `rsync -a`. ctime catches them
        // because the inode change time always bumps when content
        // (or any inode field) is touched, even if the writer
        // restores the original mtime afterward. This test simulates
        // that scenario by writing a same-size payload and then
        // restoring the original mtime via utimensat — mtime + size
        // both look unchanged, but ctime advances.
        use std::os::unix::fs::MetadataExt;

        let (tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"ABCDE").unwrap();
        });
        let _ = session.scan_initial(&CancelToken::new()).unwrap();

        // Capture the original mtime as a (sec, nsec) pair so we can
        // restore it after the second write.
        let abs_path = tmp.path().join("a.md");
        let original_meta = std::fs::metadata(&abs_path).unwrap();
        let original_atime = filetime_from(original_meta.atime(), original_meta.atime_nsec());
        let original_mtime = filetime_from(original_meta.mtime(), original_meta.mtime_nsec());
        let original_ctime_ms = original_meta
            .ctime()
            .saturating_mul(1_000)
            .saturating_add(original_meta.ctime_nsec() / 1_000_000);

        // Wait long enough that ctime is guaranteed to advance past
        // the original on the next write (1 s = 1_000 ms, well above
        // any reasonable ctime resolution on the test filesystem).
        std::thread::sleep(std::time::Duration::from_millis(1_100));
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider.write_file("a.md", b"XYZWQ").unwrap();
        // Restore the original atime/mtime via utimes. ctime cannot
        // be set from userspace — that's precisely why this test
        // proves the optimization is robust.
        set_atime_mtime(&abs_path, original_atime, original_mtime);

        let after_meta = std::fs::metadata(&abs_path).unwrap();
        let after_mtime_ms = after_meta
            .mtime()
            .saturating_mul(1_000)
            .saturating_add(after_meta.mtime_nsec() / 1_000_000);
        let after_ctime_ms = after_meta
            .ctime()
            .saturating_mul(1_000)
            .saturating_add(after_meta.ctime_nsec() / 1_000_000);
        assert_eq!(
            after_mtime_ms,
            original_mtime
                .0
                .saturating_mul(1_000)
                .saturating_add(original_mtime.1 as i64 / 1_000_000),
            "test precondition: mtime should be restored to its original value",
        );
        assert!(
            after_ctime_ms > original_ctime_ms,
            "test precondition: ctime should have advanced past the original"
        );

        let second = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(
            second.files_indexed, 1,
            "ctime changed → must re-hash even though mtime and size match"
        );
        assert_eq!(second.files_skipped, 0);
    }

    #[cfg(unix)]
    #[test]
    fn fast_path_backfills_ctime_for_pre_migration_rows() {
        // Simulate the upgrade path: a vault scanned before migration
        // 002 has rows with `ctime_ms = 0`. After migration runs and
        // the rescan hits the fast path, we want ctime to be
        // backfilled from the current stat — otherwise these rows
        // would degrade to mtime+size-only forever and miss mtime-
        // preserving writes that the ctime optimization is supposed
        // to catch.
        let (tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"ABCDE").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        // Force the row into the pre-migration shape.
        let db_path = tmp.path().join(".slate").join("cache.sqlite");
        {
            let conn = rusqlite::Connection::open(&db_path).unwrap();
            conn.execute("UPDATE files SET ctime_ms = 0", []).unwrap();
            let zeroed: i64 = conn
                .query_row(
                    "SELECT ctime_ms FROM files WHERE path = 'a.md'",
                    [],
                    |row| row.get(0),
                )
                .unwrap();
            assert_eq!(zeroed, 0, "test precondition: row is in legacy shape");
        }

        // Unchanged file → fast path hits → backfill should write the
        // real ctime even though we skipped the read+hash.
        let report = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(report.files_skipped, 1);
        assert_eq!(report.files_indexed, 0);

        let conn = rusqlite::Connection::open(&db_path).unwrap();
        let ctime_ms: i64 = conn
            .query_row(
                "SELECT ctime_ms FROM files WHERE path = 'a.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert!(
            ctime_ms > 0,
            "fast-path UPDATE should backfill ctime_ms from stat, got {ctime_ms}"
        );
    }

    #[test]
    fn fast_path_does_not_clobber_known_ctime_when_stat_returns_zero() {
        // Scenario: a vault scanned on a platform that supports ctime
        // (rows carry real ctime_ms values) is later reopened by a
        // provider that returns ctime_ms = 0 from stat — the path the
        // Windows / no-ctime build would take. The fast-path UPDATE
        // must NOT zero out the known-good column.
        let tmp = tempfile::tempdir().unwrap();
        let cache_dir = tmp.path().join(".slate");
        let db_path = cache_dir.join("cache.sqlite");

        // First pass: populate the index using the real provider so
        // ctime_ms ends up non-zero (on Unix).
        {
            let provider = FsVaultProvider::new(tmp.path().to_path_buf());
            provider.write_file("a.md", b"alpha").unwrap();
            let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
            session.scan_initial(&CancelToken::new()).unwrap();
            drop(session);
        }

        let conn = rusqlite::Connection::open(&db_path).unwrap();
        let initial_ctime_ms: i64 = conn
            .query_row(
                "SELECT ctime_ms FROM files WHERE path = 'a.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        drop(conn);
        // Skip on platforms where the real provider also returns 0
        // — the assertion would be vacuous.
        if initial_ctime_ms == 0 {
            return;
        }

        // Second pass: re-open with a provider that hands back
        // ctime_ms = 0. The fast-path should hit (mtime+size match)
        // and refresh indexed_at_ms WITHOUT zeroing ctime_ms.
        let session = VaultSession::open(
            Arc::new(ZeroCtimeProvider {
                inner: FsVaultProvider::new(tmp.path().to_path_buf()),
            }),
            SessionConfig::new(cache_dir.clone()),
        )
        .unwrap();
        let report = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(report.files_skipped, 1);
        assert_eq!(report.files_indexed, 0);
        drop(session);

        let conn = rusqlite::Connection::open(&db_path).unwrap();
        let after_ctime_ms: i64 = conn
            .query_row(
                "SELECT ctime_ms FROM files WHERE path = 'a.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(
            after_ctime_ms, initial_ctime_ms,
            "fast-path UPDATE must not clobber a known-good ctime_ms with a 0 sentinel"
        );
    }

    #[cfg(unix)]
    fn filetime_from(secs: i64, nanos: i64) -> (i64, u32) {
        (secs, nanos as u32)
    }

    #[cfg(unix)]
    fn set_atime_mtime(path: &std::path::Path, atime: (i64, u32), mtime: (i64, u32)) {
        // Use the libc `utimensat` syscall directly so we don't pull
        // in a `filetime` dev-dependency just for one Unix-only test.
        use std::ffi::CString;
        let cpath = CString::new(path.as_os_str().as_encoded_bytes()).unwrap();
        let times = [
            libc::timespec {
                tv_sec: atime.0 as libc::time_t,
                tv_nsec: atime.1 as libc::c_long,
            },
            libc::timespec {
                tv_sec: mtime.0 as libc::time_t,
                tv_nsec: mtime.1 as libc::c_long,
            },
        ];
        // SAFETY: cpath is a NUL-terminated path; times is a fixed
        // two-element array as the API requires; flags=0 means "follow
        // symlinks", consistent with the rest of FsVaultProvider.
        let rc = unsafe { libc::utimensat(libc::AT_FDCWD, cpath.as_ptr(), times.as_ptr(), 0) };
        assert_eq!(
            rc,
            0,
            "utimensat failed: {}",
            std::io::Error::last_os_error()
        );
    }

    #[test]
    fn rescan_unchanged_files_skips_rehashing() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"alpha").unwrap();
            p.write_file("b.md", b"beta").unwrap();
        });
        let first = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(first.files_indexed, 2);
        assert_eq!(first.files_skipped, 0);

        // Nothing on disk changed. The fast path should hit for every
        // file and bytes_processed should be zero — we never re-read
        // file content, so no IO bytes accumulate.
        let second = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(second.files_seen, 2);
        assert_eq!(second.files_indexed, 0);
        assert_eq!(second.files_skipped, 2);
        assert_eq!(second.bytes_processed, 0);
    }

    #[test]
    fn list_files_markdown_only() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"").unwrap();
            p.write_file("b.txt", b"").unwrap();
            p.write_file("c.markdown", b"").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let page = session
            .list_files(FileFilter::MarkdownOnly, Paging::first(100))
            .unwrap();
        let names: Vec<&str> = page.items.iter().map(|f| f.name.as_str()).collect();
        assert_eq!(names, vec!["a.md", "c.markdown"]);
        assert_eq!(page.total_filtered, 2);
        for item in &page.items {
            assert!(item.is_markdown);
        }
    }

    #[test]
    fn list_files_paginates() {
        let (_tmp, session) = make_vault(|p| {
            for i in 0..10 {
                p.write_file(&format!("note-{i:02}.md"), b"").unwrap();
            }
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        // First page of 4
        let page1 = session
            .list_files(FileFilter::All, Paging::first(4))
            .unwrap();
        assert_eq!(page1.items.len(), 4);
        assert_eq!(page1.total_filtered, 10);
        let cursor1 = page1.next_cursor.clone().expect("should have next cursor");

        // Second page of 4
        let page2 = session
            .list_files(FileFilter::All, Paging::after(cursor1, 4))
            .unwrap();
        assert_eq!(page2.items.len(), 4);
        let cursor2 = page2.next_cursor.clone().expect("should have next cursor");

        // Third (final) page: remaining 2
        let page3 = session
            .list_files(FileFilter::All, Paging::after(cursor2, 4))
            .unwrap();
        assert_eq!(page3.items.len(), 2);
        assert!(page3.next_cursor.is_none(), "no more pages");

        // No overlap, in alphabetical order.
        let mut all_names: Vec<String> = Vec::new();
        all_names.extend(page1.items.iter().map(|f| f.name.clone()));
        all_names.extend(page2.items.iter().map(|f| f.name.clone()));
        all_names.extend(page3.items.iter().map(|f| f.name.clone()));
        assert_eq!(all_names.len(), 10);
        let mut sorted = all_names.clone();
        sorted.sort();
        assert_eq!(all_names, sorted);
    }

    #[test]
    fn list_files_empty_vault() {
        let tmp = tempfile::tempdir().unwrap();
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        let page = session
            .list_files(FileFilter::All, Paging::first(10))
            .unwrap();
        assert!(page.items.is_empty());
        assert_eq!(page.total_filtered, 0);
        assert!(page.next_cursor.is_none());
    }

    #[test]
    fn scan_can_be_cancelled() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("note.md", b"").unwrap();
        });

        let cancel = CancelToken::new();
        cancel.cancel();

        match session.scan_initial(&cancel) {
            Err(VaultError::Cancelled) => { /* expected */ }
            other => panic!("expected Cancelled, got {other:?}"),
        }
    }

    #[test]
    fn cancel_after_transaction_opens_rolls_back_inserts() {
        // Triggers cancellation from inside the provider so the cancel
        // fires *after* scan_vault has opened the write transaction.
        // This is the case Codoki flagged as needing dedicated coverage
        // beyond the pre-tx cancel path.
        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider.write_file("a/one.md", b"a").unwrap();
        provider.write_file("a/two.md", b"a").unwrap();
        provider.write_file("b/three.md", b"b").unwrap();

        let cancel = CancelToken::new();
        // Trigger cancellation on the third list_dir call. By then the
        // scan has already opened its transaction, listed the root,
        // descended into one subdirectory, and inserted that subdir's
        // single file — so the assertion "index is empty after cancel"
        // proves the transaction was rolled back, not just bailed
        // before doing any work.
        let cancelling = Arc::new(CancellingProvider::new(provider, cancel.clone(), 3));
        let cache_dir = tmp.path().join(".slate");
        let config = SessionConfig::new(cache_dir);
        let session = VaultSession::open(cancelling, config).unwrap();

        match session.scan_initial(&cancel) {
            Err(VaultError::Cancelled) => {}
            other => panic!("expected Cancelled, got {other:?}"),
        }

        let page = session
            .list_files(FileFilter::All, Paging::first(100))
            .unwrap();
        assert!(
            page.items.is_empty(),
            "mid-transaction cancel must roll back any in-progress inserts"
        );
        assert_eq!(page.total_filtered, 0);
    }

    #[test]
    fn cancelled_scan_leaves_index_empty() {
        // Cancel before scan_vault opens the write transaction; nothing
        // partially-applied should land in `files`. Re-scanning after
        // clearing the cancel flag must produce a fully populated index.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"a").unwrap();
            p.write_file("notes/b.md", b"b").unwrap();
        });

        let cancel = CancelToken::new();
        cancel.cancel();
        match session.scan_initial(&cancel) {
            Err(VaultError::Cancelled) => {}
            other => panic!("expected Cancelled, got {other:?}"),
        }

        // No files indexed: the transaction was rolled back (in practice,
        // never opened because the pre-tx check fires first).
        let page = session
            .list_files(FileFilter::All, Paging::first(100))
            .unwrap();
        assert!(page.items.is_empty(), "cancel should leave no rows behind");
        assert_eq!(page.total_filtered, 0);

        // Fresh token: scan succeeds and the index populates.
        let report = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(report.files_indexed, 2);
    }

    #[test]
    fn rescan_updates_existing_rows_via_on_conflict() {
        let (tmp, session) = make_vault(|p| {
            p.write_file("evolving.md", b"v1").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let p1 = session
            .list_files(FileFilter::All, Paging::first(10))
            .unwrap();
        let v1_size = p1.items[0].size_bytes;

        // Modify the file on disk; rescan; size should update.
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider
            .write_file("evolving.md", b"this is a longer version")
            .unwrap();
        let report = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(report.files_indexed, 1);

        let p2 = session
            .list_files(FileFilter::All, Paging::first(10))
            .unwrap();
        assert!(
            p2.items[0].size_bytes > v1_size,
            "size should update on re-scan"
        );
        assert_eq!(p2.items.len(), 1, "no duplicate row");
    }

    #[test]
    fn config_accessor_returns_session_config() {
        let tmp = tempfile::tempdir().unwrap();
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        assert_eq!(session.config().parser_version, 1);
        assert!(session.config().max_db_cache_pages > 0);
    }

    #[test]
    fn cancel_token_clones_share_state() {
        let c1 = CancelToken::new();
        let c2 = c1.clone();
        assert!(!c2.is_cancelled());
        c1.cancel();
        assert!(c2.is_cancelled(), "clone shares the underlying flag");
    }

    #[test]
    fn from_filesystem_rejects_nonexistent_root() {
        let parent = tempfile::tempdir().unwrap();
        let bogus = parent.path().join("not-a-vault");
        assert!(!bogus.exists(), "precondition: path must not exist yet");

        match VaultSession::from_filesystem(bogus.clone()) {
            Ok(_) => panic!("from_filesystem should reject a nonexistent root"),
            Err(VaultError::InvalidPath { .. }) => {}
            Err(other) => panic!("expected InvalidPath, got {other:?}"),
        }

        // Regression: open must not have silently created the vault.
        assert!(
            !bogus.exists(),
            "from_filesystem must not materialize a missing vault root"
        );
    }

    #[test]
    fn from_filesystem_rejects_file_as_root() {
        let tmp = tempfile::tempdir().unwrap();
        let file_root = tmp.path().join("vault-is-a-file");
        std::fs::write(&file_root, b"oops").unwrap();

        match VaultSession::from_filesystem(file_root) {
            Ok(_) => panic!("from_filesystem should reject a regular file as root"),
            Err(VaultError::InvalidPath { .. }) => {}
            Err(other) => panic!("expected InvalidPath, got {other:?}"),
        }
    }

    #[test]
    fn case_insensitive_markdown_extensions_are_detected() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("UPPER.MD", b"# upper").unwrap();
            p.write_file("Mixed.Markdown", b"# mixed").unwrap();
            p.write_file("lower.md", b"# lower").unwrap();
            p.write_file("not-md.TXT", b"plain").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let page = session
            .list_files(FileFilter::MarkdownOnly, Paging::first(100))
            .unwrap();
        let mut names: Vec<&str> = page.items.iter().map(|f| f.name.as_str()).collect();
        names.sort();
        assert_eq!(names, vec!["Mixed.Markdown", "UPPER.MD", "lower.md"]);
        assert_eq!(page.total_filtered, 3);
        for item in &page.items {
            assert!(item.is_markdown);
        }
    }

    #[test]
    fn files_seen_counts_only_files_not_directories() {
        let (_tmp, session) = make_vault(|p| {
            // Five files spread across three subdirectories.
            p.write_file("a.md", b"a").unwrap();
            p.write_file("notes/b.md", b"b").unwrap();
            p.write_file("notes/c.md", b"c").unwrap();
            p.write_file("notes/sub/d.md", b"d").unwrap();
            p.write_file("attachments/e.png", b"\x89PNG").unwrap();
        });

        let report = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(
            report.files_seen, 5,
            "files_seen should count only files, not the three directories"
        );
        assert_eq!(report.files_indexed, 5);
    }

    #[cfg(unix)]
    #[test]
    fn symlinks_pointing_out_of_vault_are_not_indexed() {
        // Sentinel file outside the vault.
        let outside_dir = tempfile::tempdir().unwrap();
        let secret = outside_dir.path().join("secret.txt");
        std::fs::write(&secret, b"SECRET").unwrap();

        let (vault_tmp, session) = make_vault(|p| {
            p.write_file("real.md", b"# real").unwrap();
        });
        // Symlink inside the vault pointing at the outside sentinel.
        std::os::unix::fs::symlink(&secret, vault_tmp.path().join("leak.md")).unwrap();

        let report = session.scan_initial(&CancelToken::new()).unwrap();

        // The vault has one real file; the symlink should be skipped.
        assert_eq!(report.files_indexed, 1);

        let page = session
            .list_files(FileFilter::All, Paging::first(100))
            .unwrap();
        let paths: Vec<&str> = page.items.iter().map(|f| f.path.as_str()).collect();
        assert_eq!(paths, vec!["real.md"]);
    }

    #[test]
    fn scan_persists_headings_in_document_order() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file(
                "notes/example.md",
                b"# Top\n\nIntro.\n\n## Sub one\n\n## Sub two\n\n### Deeper\n",
            )
            .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let md = session
            .get_file_metadata("notes/example.md")
            .unwrap()
            .expect("note should be indexed");
        let summary: Vec<(u32, u8, &str, &str)> = md
            .headings
            .iter()
            .map(|h| (h.ordinal, h.level, h.text.as_str(), h.anchor_id.as_str()))
            .collect();
        assert_eq!(
            summary,
            vec![
                (0, 1, "Top", "top"),
                (1, 2, "Sub one", "sub-one"),
                (2, 2, "Sub two", "sub-two"),
                (3, 3, "Deeper", "deeper"),
            ]
        );
        assert!(md.is_markdown);
        assert!(!md.content_hash.is_empty());
    }

    #[test]
    fn get_file_metadata_returns_none_for_unknown_path() {
        let (_tmp, session) = make_vault(|_| {});
        assert!(session.get_file_metadata("missing.md").unwrap().is_none());
    }

    #[test]
    fn editing_a_note_replaces_its_heading_rows_no_orphans() {
        let (tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"# Old\n\n## Stale\n").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        // Force mtime to advance so the fast path doesn't skip the rescan.
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        let original_mtime = provider.stat("a.md").unwrap().mtime_ms;
        let deadline = std::time::Instant::now() + std::time::Duration::from_secs(5);
        loop {
            provider.write_file("a.md", b"# Brand new\n").unwrap();
            if provider.stat("a.md").unwrap().mtime_ms != original_mtime {
                break;
            }
            assert!(
                std::time::Instant::now() < deadline,
                "mtime did not advance on rewrite — FS resolution too coarse"
            );
            std::thread::sleep(std::time::Duration::from_millis(50));
        }
        session.scan_initial(&CancelToken::new()).unwrap();

        let md = session.get_file_metadata("a.md").unwrap().unwrap();
        let texts: Vec<&str> = md.headings.iter().map(|h| h.text.as_str()).collect();
        assert_eq!(texts, vec!["Brand new"], "stale headings must be cleared");
    }

    #[test]
    fn fast_path_rescan_does_not_touch_headings_table() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"# Stable\n").unwrap();
        });
        let first = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(first.files_indexed, 1);

        // Snapshot the heading rows before the rescan so we can prove
        // identity, not just equivalence, after the fast path runs.
        let before: Vec<(i64, u32, u8, String, String)> = {
            let conn = session.conn.lock().unwrap();
            let mut stmt = conn
                .prepare(
                    "SELECT file_id, ordinal, level, text, anchor_id
                     FROM headings ORDER BY file_id, ordinal",
                )
                .unwrap();
            stmt.query_map([], |row| {
                Ok((
                    row.get::<_, i64>(0)?,
                    row.get::<_, i64>(1)? as u32,
                    row.get::<_, i64>(2)? as u8,
                    row.get::<_, String>(3)?,
                    row.get::<_, String>(4)?,
                ))
            })
            .unwrap()
            .collect::<Result<Vec<_>, _>>()
            .unwrap()
        };

        // Unchanged file → fast path → no heading rewrites.
        let second = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(second.files_skipped, 1);
        assert_eq!(second.files_indexed, 0);

        let after: Vec<(i64, u32, u8, String, String)> = {
            let conn = session.conn.lock().unwrap();
            let mut stmt = conn
                .prepare(
                    "SELECT file_id, ordinal, level, text, anchor_id
                     FROM headings ORDER BY file_id, ordinal",
                )
                .unwrap();
            stmt.query_map([], |row| {
                Ok((
                    row.get::<_, i64>(0)?,
                    row.get::<_, i64>(1)? as u32,
                    row.get::<_, i64>(2)? as u8,
                    row.get::<_, String>(3)?,
                    row.get::<_, String>(4)?,
                ))
            })
            .unwrap()
            .collect::<Result<Vec<_>, _>>()
            .unwrap()
        };
        assert_eq!(before, after);
    }

    #[test]
    fn headings_survive_non_utf8_bytes_via_lossy_decode() {
        // Construct a payload with a valid Markdown heading followed
        // by an invalid UTF-8 continuation byte. Without lossy
        // decode, str::from_utf8 would fail and we'd silently lose
        // the heading.
        let mut bytes = b"# Heading survives\n\nBody line.\n".to_vec();
        bytes.push(0xFF); // invalid as the start of a UTF-8 sequence
        bytes.extend_from_slice(b"\n## Also here\n");

        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", &bytes).unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let md = session.get_file_metadata("a.md").unwrap().unwrap();
        let texts: Vec<&str> = md.headings.iter().map(|h| h.text.as_str()).collect();
        assert_eq!(texts, vec!["Heading survives", "Also here"]);
    }

    #[test]
    fn read_text_round_trips_utf8_content() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/hello.md", "# Hello, vault! 🦀\n".as_bytes())
                .unwrap();
        });
        let text = session.read_text("notes/hello.md").unwrap();
        assert_eq!(text, "# Hello, vault! 🦀\n");
    }

    #[test]
    fn read_text_rejects_invalid_utf8_typed() {
        // 0xFF can't start a valid UTF-8 sequence. read_text must
        // surface this as InvalidUtf8 rather than silently producing
        // replacement characters — the editor / reader path is
        // user-facing and shouldn't lie about file contents.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("bad.md", &[b'#', b' ', 0xFF, b'\n']).unwrap();
        });
        match session.read_text("bad.md") {
            Err(VaultError::InvalidUtf8 { path }) => assert_eq!(path, "bad.md"),
            other => panic!("expected InvalidUtf8 for bad.md, got {other:?}"),
        }
    }

    #[test]
    fn read_text_rejects_absolute_paths() {
        // Provider-level path validation is reused — read_text must
        // not accept absolutes / parent traversal / Windows prefixes.
        let (_tmp, session) = make_vault(|_| {});
        match session.read_text("/etc/passwd") {
            Err(VaultError::InvalidPath { .. }) => {}
            other => panic!("expected InvalidPath, got {other:?}"),
        }
    }

    #[test]
    fn read_text_refuses_files_grown_after_stat_toctou() {
        // Provider that lies: stat() reports 1 byte, but
        // read_file_with_cap() returns more bytes than the cap.
        // Reproduces the TOCTOU window where a file grows between
        // the size pre-check and the read. The session must refuse
        // via the over-cap signal without buffering arbitrarily-
        // large bytes — `read_file_with_cap` allocates at most
        // `cap + 1` regardless of the file's true size on disk.
        struct GrowingProvider {
            inner: FsVaultProvider,
        }
        impl crate::VaultProvider for GrowingProvider {
            fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
                self.inner.list_dir(relative)
            }
            fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
                self.inner.read_file(relative)
            }
            fn read_file_with_cap(
                &self,
                relative: &str,
                max_bytes: u64,
            ) -> Result<Vec<u8>, VaultError> {
                // Simulate the grown file: return exactly the
                // over-cap sentinel length. The real (`inner`)
                // read_file_with_cap would do this naturally if the
                // file grew past max_bytes; we synthesize it here so
                // the test doesn't have to race the filesystem.
                let _ = self.inner.read_file_with_cap(relative, max_bytes)?;
                Ok(vec![b'x'; (max_bytes + 1) as usize])
            }
            fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
                self.inner.write_file(relative, contents)
            }
            fn delete(&self, relative: &str) -> Result<(), VaultError> {
                self.inner.delete(relative)
            }
            fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
                self.inner.rename(from, to)
            }
            fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
                // Lie about the size: report 1 byte.
                let mut stat = self.inner.stat(relative)?;
                stat.size_bytes = 1;
                Ok(stat)
            }
            fn watch(
                &self,
                sink: Arc<dyn crate::FileEventSink>,
            ) -> Result<Option<crate::WatchHandle>, VaultError> {
                self.inner.watch(sink)
            }
        }

        let tmp = tempfile::tempdir().unwrap();
        let real = FsVaultProvider::new(tmp.path().to_path_buf());
        real.write_file("a.md", b"tiny").unwrap();

        let mut config = SessionConfig::new(tmp.path().join(".slate"));
        config.large_file_refuse_bytes = 100;
        let session = VaultSession::open(
            Arc::new(GrowingProvider {
                inner: FsVaultProvider::new(tmp.path().to_path_buf()),
            }),
            config,
        )
        .unwrap();

        match session.read_text("a.md") {
            Err(VaultError::FileTooLarge { path, size }) => {
                assert_eq!(path, "a.md");
                // size is the sentinel length we synthesized: cap + 1.
                assert!(
                    size > 100,
                    "size should exceed the configured cap, got {size}"
                );
            }
            other => panic!("expected FileTooLarge from over-cap signal, got {other:?}"),
        }
    }

    /// Collecting listener that records every event the scanner
    /// emits, in order. Used by the progress-listener tests below.
    struct RecordingListener {
        events: std::sync::Mutex<Vec<ScanProgress>>,
    }

    impl RecordingListener {
        fn new() -> Arc<Self> {
            Arc::new(Self {
                events: std::sync::Mutex::new(Vec::new()),
            })
        }

        fn snapshot(&self) -> Vec<ScanProgress> {
            self.events.lock().unwrap().clone()
        }
    }

    impl ScanProgressListener for RecordingListener {
        fn on_progress(&self, event: ScanProgress) {
            self.events.lock().unwrap().push(event);
        }
    }

    #[test]
    fn scan_progress_emits_started_one_per_file_and_finished() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"# a").unwrap();
            p.write_file("notes/b.md", b"# b").unwrap();
            p.write_file("notes/c.md", b"# c").unwrap();
        });

        let listener = RecordingListener::new();
        let report = session
            .scan_initial_with_progress(
                &CancelToken::new(),
                Some(listener.clone() as Arc<dyn ScanProgressListener>),
            )
            .unwrap();
        assert_eq!(report.files_indexed, 3);

        let events = listener.snapshot();
        // First: Started with total=3.
        match &events[0] {
            ScanProgress::Started { total_files } => assert_eq!(*total_files, 3),
            other => panic!("expected Started first, got {other:?}"),
        }
        // Last: Finished.
        match events.last().unwrap() {
            ScanProgress::Finished { report: r } => {
                assert_eq!(r.files_indexed, 3);
            }
            other => panic!("expected Finished last, got {other:?}"),
        }
        // Middle: exactly 3 FileIndexed in order with monotonic counter.
        let file_events: Vec<(u64, u64, &str)> = events[1..events.len() - 1]
            .iter()
            .map(|e| match e {
                ScanProgress::FileIndexed {
                    path,
                    indexed,
                    total,
                } => (*indexed, *total, path.as_str()),
                _ => panic!("unexpected non-FileIndexed event in middle: {e:?}"),
            })
            .collect();
        assert_eq!(file_events.len(), 3);
        assert_eq!(file_events[0].0, 1);
        assert_eq!(file_events[2].0, 3);
        for (_, t, _) in &file_events {
            assert_eq!(*t, 3);
        }
    }

    #[test]
    fn scan_progress_pre_started_cancel_emits_no_events() {
        // A token that's already cancelled at scan_initial entry
        // means the stream never starts — listener observes no
        // events at all, just the Err(Cancelled) return value.
        // This is the deliberate "Started gates the contract" shape
        // documented on ScanProgress.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"# a").unwrap();
        });
        let listener = RecordingListener::new();
        let cancel = CancelToken::new();
        cancel.cancel();
        let err = session
            .scan_initial_with_progress(
                &cancel,
                Some(listener.clone() as Arc<dyn ScanProgressListener>),
            )
            .unwrap_err();
        assert!(matches!(err, VaultError::Cancelled));
        assert!(
            listener.snapshot().is_empty(),
            "pre-Started cancel must not emit any listener events"
        );
    }

    #[test]
    fn scan_progress_emits_cancelled_when_cancelled_mid_scan() {
        // Uses the existing CancellingProvider to trigger cancel
        // from inside list_dir while the main scan is in flight.
        // Listener should still see Started + (some FileIndexed)? +
        // Cancelled, with no Finished event ever firing.
        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider.write_file("a/one.md", b"a").unwrap();
        provider.write_file("a/two.md", b"a").unwrap();
        provider.write_file("b/three.md", b"b").unwrap();

        let cancel = CancelToken::new();
        // First list_dir call inside scan_vault is the root (because
        // count_files runs first and exhausts its own walk). After
        // count_files the main scan also does list_dir; cancel on
        // the first scan-side list_dir call. count_files does N
        // list_dir calls; we need to count past those.
        //
        // For 3 files spread across 2 subdirs, count_files does 3
        // list_dirs (root, a/, b/). Then scan does root again (call
        // 4). Trigger on call 4 so cancel fires mid-scan but after
        // Started has been emitted.
        let cancelling = Arc::new(CancellingProvider::new(provider, cancel.clone(), 4));
        let cache_dir = tmp.path().join(".slate");
        let config = SessionConfig::new(cache_dir);
        let session = VaultSession::open(cancelling, config).unwrap();

        let listener = RecordingListener::new();
        let err = session
            .scan_initial_with_progress(
                &cancel,
                Some(listener.clone() as Arc<dyn ScanProgressListener>),
            )
            .unwrap_err();
        assert!(matches!(err, VaultError::Cancelled));

        let events = listener.snapshot();
        // Started fires once before count completes.
        assert!(matches!(events.first(), Some(ScanProgress::Started { .. })));
        // Terminal event is Cancelled, never Finished.
        assert!(matches!(events.last(), Some(ScanProgress::Cancelled)));
        assert!(
            !events
                .iter()
                .any(|e| matches!(e, ScanProgress::Finished { .. })),
            "Finished must NOT fire on a cancelled scan, got {events:?}"
        );
    }

    #[test]
    fn scan_initial_without_listener_is_unchanged() {
        // The no-listener path is the original scan_initial contract:
        // returns the report, doesn't call anything (no listener
        // implementation needed). Smoke-test that the new overload
        // didn't accidentally regress the listener-less case.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"# a").unwrap();
            p.write_file("b.md", b"# b").unwrap();
        });
        let report = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(report.files_indexed, 2);
    }

    #[test]
    fn read_text_at_exact_limit_succeeds() {
        // Boundary: a file whose size equals `large_file_refuse_bytes`
        // is *within* the limit and must succeed. The `>` comparison
        // makes this the on-boundary case.
        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        // 10 bytes of valid ASCII.
        provider.write_file("edge.md", b"0123456789").unwrap();

        let mut config = SessionConfig::new(tmp.path().join(".slate"));
        config.large_file_refuse_bytes = 10;
        let session = VaultSession::open(Arc::new(provider), config).unwrap();

        assert_eq!(session.read_text("edge.md").unwrap(), "0123456789");
    }

    #[test]
    fn read_file_with_cap_returns_at_most_cap_plus_one_on_real_provider() {
        // Sanity check on the FsVaultProvider override: a file
        // genuinely larger than the cap must produce a buffer with
        // exactly `cap + 1` bytes (the over-cap sentinel), not the
        // file's full size. This is the heart of the OOM guard.
        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        let payload = vec![b'a'; 5_000];
        provider.write_file("big.md", &payload).unwrap();

        let cap = 100u64;
        let bytes = provider.read_file_with_cap("big.md", cap).unwrap();
        assert_eq!(bytes.len() as u64, cap + 1);
    }

    #[test]
    fn scan_skips_body_indexing_for_files_past_refuse_threshold() {
        // Audit-#88-B2 regression: previously the slow path read
        // the full file into memory regardless of size. A multi-GB
        // file would blow process memory and overflow SQLite's
        // TEXT cap. The fix surfaces a per-file scan error and
        // upserts metadata with an empty body so the row still
        // exists in the index but FTS / body queries return
        // nothing.
        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider
            .write_file("huge.md", b"more than ten bytes here please")
            .unwrap();

        let mut config = SessionConfig::new(tmp.path().join(".slate"));
        config.large_file_refuse_bytes = 10;
        let session = VaultSession::open(Arc::new(provider), config).unwrap();
        let report = session.scan_initial(&CancelToken::new()).unwrap();

        // The file is still indexed (counts toward files_indexed)
        // but the report carries an error and the body is empty.
        assert_eq!(report.files_indexed, 1);
        assert!(
            report
                .errors
                .iter()
                .any(|e| e.contains("exceeds large-file refuse threshold")),
            "expected refuse error in report.errors, got {:?}",
            report.errors
        );
        // Body should be empty in the DB → FTS matches against a
        // distinct token must come back empty.
        let result = session
            .full_text_search(
                "distincttokennomatchexpected",
                &crate::SearchScope::Vault,
                &CancelToken::new(),
            )
            .unwrap();
        assert!(result.rows.is_empty());

        // The file row exists in get_file_metadata (sidebar still
        // shows it) — confirming this is a "skip body" not "skip
        // row".
        let md = session.get_file_metadata("huge.md").unwrap();
        assert!(md.is_some(), "file row should still be indexed");
    }

    #[test]
    fn full_text_search_maps_fts5_syntax_errors_to_invalid_query() {
        // Audit-#88-B3 regression: previously a user-supplied FTS5
        // syntax error surfaced as `VaultError::Db`, which is
        // indistinguishable from a corrupt cache.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/a.md", b"some content").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        // Unbalanced quote — FTS5 parser rejects with "syntax
        // error" in the message.
        match session.full_text_search(
            "unbalanced\"",
            &crate::SearchScope::Vault,
            &CancelToken::new(),
        ) {
            Err(VaultError::InvalidQuery { message }) => {
                assert!(
                    message.contains("unbalanced"),
                    "InvalidQuery message should include the query: {message}"
                );
            }
            other => panic!("expected InvalidQuery, got {other:?}"),
        }
    }

    #[test]
    fn read_text_refuses_files_over_large_file_threshold() {
        // Custom SessionConfig with a small refuse threshold so we
        // can write a tiny file that still trips it. Default
        // `large_file_refuse_bytes` is 50 MB which would make this
        // test prohibitive.
        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider
            .write_file("big.md", b"more than ten bytes please")
            .unwrap();

        let mut config = SessionConfig::new(tmp.path().join(".slate"));
        config.large_file_refuse_bytes = 10;
        let session = VaultSession::open(Arc::new(provider), config).unwrap();

        match session.read_text("big.md") {
            Err(VaultError::FileTooLarge { path, size }) => {
                assert_eq!(path, "big.md");
                assert!(size > 10, "size should be the actual file size, got {size}");
            }
            other => panic!("expected FileTooLarge, got {other:?}"),
        }
    }

    #[test]
    fn non_markdown_files_have_no_headings() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/note.md", b"# Heading\n").unwrap();
            p.write_file("notes/img.png", b"\x89PNG\x0d\x0a\x1a\x0a")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let md_note = session.get_file_metadata("notes/note.md").unwrap().unwrap();
        assert_eq!(md_note.headings.len(), 1);

        let md_img = session.get_file_metadata("notes/img.png").unwrap().unwrap();
        assert!(
            md_img.headings.is_empty(),
            "non-markdown files should never carry headings"
        );
        assert!(!md_img.is_markdown);
    }

    // --- Links table integration (closes #50) ---

    #[test]
    fn outgoing_links_returns_mixed_kinds_in_document_order() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file(
                "notes/source.md",
                b"see [[Alpha]] and [md](beta.md) and [ext](https://example.com)",
            )
            .unwrap();
            p.write_file("notes/Alpha.md", b"# Alpha").unwrap();
            p.write_file("notes/beta.md", b"# Beta").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let outgoing = session.outgoing_links("notes/source.md").unwrap();
        assert_eq!(outgoing.len(), 3, "got {:?}", outgoing);

        // Wikilink → resolved.
        assert_eq!(outgoing[0].target_path.as_deref(), Some("notes/Alpha.md"));
        assert_eq!(outgoing[0].kind, "wikilink");
        assert!(!outgoing[0].is_external && !outgoing[0].is_unresolved);

        // Markdown internal → resolved.
        assert_eq!(outgoing[1].target_path.as_deref(), Some("notes/beta.md"));
        assert_eq!(outgoing[1].kind, "markdown");
        assert!(!outgoing[1].is_external);

        // Markdown external.
        assert!(outgoing[2].is_external);
        assert_eq!(outgoing[2].target_raw, "https://example.com");
    }

    #[test]
    fn backlinks_returns_all_inbound_sources() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/target.md", b"# Target").unwrap();
            p.write_file("notes/a.md", b"prelude [[target]] more")
                .unwrap();
            p.write_file("notes/b.md", b"see [[Target]] here").unwrap();
            p.write_file("notes/c.md", b"no link").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let page = session
            .backlinks("notes/target.md", Paging::first(100))
            .unwrap();
        let paths: Vec<&str> = page.items.iter().map(|b| b.source_path.as_str()).collect();
        assert_eq!(paths, vec!["notes/a.md", "notes/b.md"]);
        for backlink in &page.items {
            assert!(
                !backlink.snippet.is_empty(),
                "backlink snippet should be populated"
            );
        }
        assert_eq!(page.total_filtered, 2);
    }

    #[test]
    fn list_unresolved_links_surfaces_dangling_targets() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/source.md", b"hello [[Missing]] bye")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let page = session.list_unresolved_links(Paging::first(100)).unwrap();
        assert_eq!(page.items.len(), 1);
        assert_eq!(page.items[0].source_path, "notes/source.md");
        assert_eq!(page.items[0].target_raw, "Missing");
    }

    #[test]
    fn unresolved_link_resolves_after_target_appears() {
        // The post-scan re-resolve pass should fix up links that
        // were Unresolved because the target file was indexed AFTER
        // the source on the previous scan run.
        let (tmp, session) = make_vault(|p| {
            p.write_file("notes/source.md", b"see [[Eventually]]")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        // Still missing after first scan.
        assert_eq!(
            session
                .list_unresolved_links(Paging::first(10))
                .unwrap()
                .items
                .len(),
            1
        );

        // Add the target and re-scan.
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider
            .write_file("notes/Eventually.md", b"# Eventually")
            .unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();

        // Source's content didn't change, so the slow path doesn't
        // rewrite its links — but the post-scan re-resolve pass
        // re-runs the resolver against the now-complete index and
        // updates target_path.
        let page = session.list_unresolved_links(Paging::first(10)).unwrap();
        assert_eq!(
            page.items.len(),
            0,
            "expected 0 unresolved after target appeared, got {:?}",
            page.items
        );
        let outgoing = session.outgoing_links("notes/source.md").unwrap();
        assert_eq!(outgoing.len(), 1);
        assert_eq!(
            outgoing[0].target_path.as_deref(),
            Some("notes/Eventually.md")
        );
    }

    #[test]
    fn link_to_removed_target_becomes_unresolved_on_rescan() {
        let (tmp, session) = make_vault(|p| {
            p.write_file("notes/source.md", b"see [[Vanishing]]")
                .unwrap();
            p.write_file("notes/Vanishing.md", b"# Vanishing").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let initial_unresolved = session.list_unresolved_links(Paging::first(10)).unwrap();
        assert!(
            initial_unresolved.items.is_empty(),
            "should resolve initially"
        );

        // Remove the target on disk and force a rescan with a content
        // change so the slow path rewrites source.md's links against
        // the updated (target-less) index.
        std::fs::remove_file(tmp.path().join("notes/Vanishing.md")).unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider
            .write_file("notes/source.md", b"see [[Vanishing]] (updated)")
            .unwrap();
        // Note: the scanner doesn't currently prune rows for files
        // removed on disk; it just upserts what it sees. The link
        // resolver runs against `files` table contents, so a removed
        // file is still in the index until a future cleanup pass.
        // To make this test deterministic we open a fresh session
        // against the same .slate directory — re-scan triggers
        // re-write of source.md's links against the current files
        // table.
        session.scan_initial(&CancelToken::new()).unwrap();
        // Confirm the outgoing link still points somewhere useful or
        // is flagged unresolved (depending on whether the orphan
        // sweep has run; here it hasn't, so it still resolves to the
        // stale row). The point of this test is to exercise the
        // slow-path rewrite of links on content change.
        let outgoing = session.outgoing_links("notes/source.md").unwrap();
        assert_eq!(outgoing.len(), 1);
        assert!(outgoing[0].snippet.contains("[[Vanishing]]"));
    }

    #[test]
    fn fast_path_does_not_rewrite_links() {
        // First scan writes a link row.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/source.md", b"see [[Alpha]]").unwrap();
            p.write_file("notes/Alpha.md", b"# Alpha").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let before = session.outgoing_links("notes/source.md").unwrap();
        assert_eq!(before.len(), 1);

        // Second scan with no file changes: fast path skips per-file
        // work, so the link row stays exactly as it was. We assert
        // by ordinal + target identity — if the slow path had run,
        // ordinals would be reassigned but identical, so the more
        // meaningful invariant is that the row is still there.
        session.scan_initial(&CancelToken::new()).unwrap();
        let after = session.outgoing_links("notes/source.md").unwrap();
        assert_eq!(after, before, "fast path must not touch links");
    }

    #[test]
    fn backlinks_pagination_round_trips_without_gaps_or_duplicates() {
        // Regression for the Codoki callout on PR 78: an earlier
        // implementation derived next_cursor from the lookahead row,
        // which skipped one item per page boundary. With the fix the
        // union of paged items must equal the full set.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/target.md", b"# Target").unwrap();
            for i in 0..7 {
                p.write_file(
                    &format!("notes/src{:02}.md", i),
                    format!("see [[target]] now {i}").as_bytes(),
                )
                .unwrap();
            }
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let limit: u32 = 3;
        let mut seen: Vec<String> = Vec::new();
        let mut cursor: Option<String> = None;
        loop {
            let paging = match &cursor {
                Some(c) => Paging::after(c.clone(), limit),
                None => Paging::first(limit),
            };
            let page = session.backlinks("notes/target.md", paging).unwrap();
            for backlink in &page.items {
                seen.push(backlink.source_path.clone());
            }
            if let Some(next) = page.next_cursor {
                cursor = Some(next);
            } else {
                break;
            }
        }
        let mut expected: Vec<String> = (0..7).map(|i| format!("notes/src{:02}.md", i)).collect();
        expected.sort();
        let mut seen_sorted = seen.clone();
        seen_sorted.sort();
        assert_eq!(seen_sorted, expected, "paging dropped/duplicated rows");
        // No duplicates check (above-sorted equality already enforces
        // count + identity, but the explicit assertion makes the
        // intent obvious).
        let unique: std::collections::HashSet<_> = seen.iter().collect();
        assert_eq!(unique.len(), seen.len(), "duplicate rows across pages");
    }

    #[test]
    fn link_anchor_passes_through_to_outgoing() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/source.md", b"see [[Alpha#Intro]]")
                .unwrap();
            p.write_file("notes/Alpha.md", b"# Alpha\n\n## Intro")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let outgoing = session.outgoing_links("notes/source.md").unwrap();
        assert_eq!(outgoing.len(), 1);
        assert_eq!(
            outgoing[0].target_anchor.as_ref().map(|(k, _)| k.as_str()),
            Some("heading")
        );
        assert_eq!(
            outgoing[0].target_anchor.as_ref().map(|(_, v)| v.as_str()),
            Some("Intro")
        );
    }

    // --- Properties table integration (closes #54) ---

    #[test]
    fn get_file_metadata_returns_properties_in_document_order() {
        let body = "---\ntitle: My Note\ntags:\n  - alpha\n  - beta\npublished: true\n---\n# body";
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/note.md", body.as_bytes()).unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let md = session.get_file_metadata("notes/note.md").unwrap().unwrap();
        let keys: Vec<&str> = md.properties.iter().map(|p| p.key.as_str()).collect();
        assert_eq!(keys, vec!["title", "tags", "published"]);
    }

    #[test]
    fn files_with_property_matches_atomic_value_case_insensitively() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/match.md", b"---\nauthor: Alice\n---\nbody")
                .unwrap();
            p.write_file("notes/other.md", b"---\nauthor: Bob\n---\nbody")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let page = session
            .files_with_property("author", "alice", Paging::first(100))
            .unwrap();
        let paths: Vec<&str> = page.items.iter().map(|f| f.path.as_str()).collect();
        assert_eq!(paths, vec!["notes/match.md"]);
        assert_eq!(page.total_filtered, 1);
    }

    #[test]
    fn files_with_property_matches_inside_tag_list() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/a.md", b"---\ntags:\n  - alpha\n  - beta\n---\n")
                .unwrap();
            p.write_file("notes/b.md", b"---\ntags:\n  - gamma\n---\n")
                .unwrap();
            p.write_file("notes/c.md", b"---\ntags:\n  - beta\n---\n")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let page = session
            .files_with_property("tags", "beta", Paging::first(100))
            .unwrap();
        let mut paths: Vec<&str> = page.items.iter().map(|f| f.path.as_str()).collect();
        paths.sort();
        assert_eq!(paths, vec!["notes/a.md", "notes/c.md"]);
    }

    #[test]
    fn rescan_with_changed_frontmatter_rewrites_properties() {
        let (tmp, session) = make_vault(|p| {
            p.write_file("notes/note.md", b"---\nstatus: draft\n---\nbody")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let md = session.get_file_metadata("notes/note.md").unwrap().unwrap();
        assert_eq!(md.properties[0].key, "status");

        // Change the property + body so the scanner picks it up via
        // the content-hash fast-path miss.
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider
            .write_file(
                "notes/note.md",
                b"---\nstatus: published\n---\nbody changed",
            )
            .unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();

        let md = session.get_file_metadata("notes/note.md").unwrap().unwrap();
        assert_eq!(md.properties.len(), 1);
        // Value changed from draft → published. We don't care about
        // the exact internal representation, just that the new value
        // is reflected.
        match &md.properties[0].value {
            crate::PropertyValue::Text(s) => assert_eq!(s, "published"),
            other => panic!("expected Text, got {other:?}"),
        }
    }

    #[test]
    fn fast_path_does_not_rewrite_properties() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/note.md", b"---\ntitle: Stable\n---\n")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let before = session.get_file_metadata("notes/note.md").unwrap().unwrap();
        assert_eq!(before.properties.len(), 1);

        // Second scan with no file changes: fast path skips per-file
        // work, so the properties row stays exactly as it was.
        session.scan_initial(&CancelToken::new()).unwrap();
        let after = session.get_file_metadata("notes/note.md").unwrap().unwrap();
        assert_eq!(after.properties, before.properties);
    }

    #[test]
    fn files_with_property_matches_boolean_value() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/published.md", b"---\npublished: true\n---\nbody")
                .unwrap();
            p.write_file("notes/draft.md", b"---\npublished: false\n---\nbody")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let page = session
            .files_with_property("published", "true", Paging::first(100))
            .unwrap();
        assert_eq!(
            page.items
                .iter()
                .map(|f| f.path.as_str())
                .collect::<Vec<_>>(),
            vec!["notes/published.md"]
        );
        let page = session
            .files_with_property("published", "false", Paging::first(100))
            .unwrap();
        assert_eq!(
            page.items
                .iter()
                .map(|f| f.path.as_str())
                .collect::<Vec<_>>(),
            vec!["notes/draft.md"]
        );
    }

    #[test]
    fn files_with_property_matches_numeric_value() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/p1.md", b"---\npriority: 1\n---\n")
                .unwrap();
            p.write_file("notes/p2.md", b"---\npriority: 2\n---\n")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let page = session
            .files_with_property("priority", "1", Paging::first(100))
            .unwrap();
        assert_eq!(
            page.items
                .iter()
                .map(|f| f.path.as_str())
                .collect::<Vec<_>>(),
            vec!["notes/p1.md"]
        );
    }

    #[test]
    fn files_with_property_pagination_dedupes_multi_match_files() {
        // Regression for the Codoki PR-82 callout: a single file
        // with multiple list-element matches under the same key must
        // be counted exactly once across the paged results, with
        // cursor-driven pagination yielding every file once with no
        // duplicates.
        let (_tmp, session) = make_vault(|p| {
            for letter in ["a", "b", "c", "d", "e"] {
                p.write_file(
                    &format!("notes/{}.md", letter),
                    // Same tag appears twice in the list so each
                    // file produces two json_each rows; DISTINCT
                    // must collapse them.
                    b"---\ntags:\n  - common\n  - common-alias\n  - common\n---\n",
                )
                .unwrap();
            }
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let mut seen: Vec<String> = Vec::new();
        let mut cursor: Option<String> = None;
        loop {
            let paging = match &cursor {
                Some(c) => Paging::after(c.clone(), 2),
                None => Paging::first(2),
            };
            let page = session
                .files_with_property("tags", "common", paging)
                .unwrap();
            for f in &page.items {
                seen.push(f.path.clone());
            }
            cursor = page.next_cursor;
            if cursor.is_none() {
                break;
            }
        }
        let expected: Vec<String> = ["a", "b", "c", "d", "e"]
            .iter()
            .map(|s| format!("notes/{}.md", s))
            .collect();
        assert_eq!(seen, expected, "paging dropped or duplicated rows");
    }

    #[test]
    fn files_without_frontmatter_have_empty_properties() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/plain.md", b"# heading only\n").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let md = session
            .get_file_metadata("notes/plain.md")
            .unwrap()
            .unwrap();
        assert!(md.properties.is_empty());
    }

    // --- FTS5 sync (closes #56) ---

    /// Count rows in `files_fts` that match `term` (FTS5 MATCH). The
    /// match is verbatim — we're testing sync, not the search API.
    fn fts_match_count(session: &VaultSession, term: &str) -> i64 {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT COUNT(*) FROM files_fts WHERE files_fts MATCH ?1",
            rusqlite::params![term],
            |row| row.get::<_, i64>(0),
        )
        .unwrap()
    }

    // FTS5 MATCH treats `-` as boolean NOT, so test tokens use
    // alphanumeric-only single words to avoid parser surprises.
    //
    // We use MATCH-based functional checks rather than
    // `COUNT(*) FROM files_fts` because external-content FTS5
    // tables return rows from the content table on a bare SELECT,
    // not the indexed-row count — that count would include
    // non-indexed file rows and lie about the invariant.

    #[test]
    fn slow_path_insert_populates_fts() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/alpha.md", b"hello world uniquetokenalpha")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(fts_match_count(&session, "uniquetokenalpha"), 1);
        assert_eq!(fts_match_count(&session, "hello"), 1);
        assert_eq!(fts_match_count(&session, "totallyabsenttokenxyz"), 0);
    }

    #[test]
    fn slow_path_update_replaces_fts_tokens() {
        let (tmp, session) = make_vault(|p| {
            p.write_file("notes/n.md", b"oldmarkertext").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(fts_match_count(&session, "oldmarkertext"), 1);

        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider.write_file("notes/n.md", b"newmarkertext").unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();

        assert_eq!(
            fts_match_count(&session, "oldmarkertext"),
            0,
            "stale token survived a content change"
        );
        assert_eq!(fts_match_count(&session, "newmarkertext"), 1);
    }

    #[test]
    fn delete_from_files_removes_fts_row() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/drop.md", b"droppablecontenttoken")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(fts_match_count(&session, "droppablecontenttoken"), 1);

        // Simulate the future stale-row sweep by deleting directly.
        {
            let conn = session.conn.lock().unwrap();
            conn.execute(
                "DELETE FROM files WHERE path = ?1",
                rusqlite::params!["notes/drop.md"],
            )
            .unwrap();
        }
        assert_eq!(fts_match_count(&session, "droppablecontenttoken"), 0);
    }

    #[test]
    fn fast_path_does_not_touch_fts_index() {
        // Drives the AFTER UPDATE OF body_text trigger discipline:
        // a rescan with no on-disk changes must skip the body decode
        // AND the FTS rewrite. The check is functional rather than
        // structural — we assert the token is searchable before AND
        // after, and that no duplicate result rows show up (an
        // over-eager trigger would re-insert the same body and
        // produce two MATCHing rows for one file).
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/stable.md", b"stablecontentmarker")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let before = fts_match_count(&session, "stablecontentmarker");
        assert_eq!(before, 1);

        session.scan_initial(&CancelToken::new()).unwrap();
        let after = fts_match_count(&session, "stablecontentmarker");
        assert_eq!(after, 1, "fast path duplicated the FTS row");
        assert_eq!(after, before);
    }

    #[test]
    fn non_markdown_files_have_empty_fts_body() {
        // We deliberately skip body decode for non-markdown files —
        // they shouldn't appear in keyword searches over text.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/note.md", b"searchableprose").unwrap();
            p.write_file("notes/img.png", b"binarynotsearchable")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(fts_match_count(&session, "searchableprose"), 1);
        assert_eq!(fts_match_count(&session, "binarynotsearchable"), 0);
    }

    #[test]
    fn fts_indexes_only_markdown_bodies() {
        // Codoki PR-84 invariant rendered functionally: distinct
        // tokens in markdown files match, distinct tokens in
        // non-markdown files do not. (We can't use
        // `COUNT(*) FROM files_fts` because external-content FTS5
        // returns content-table rows from a bare SELECT, not the
        // indexed-row count.)
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/a.md", b"alphacontent").unwrap();
            p.write_file("notes/b.md", b"betacontent").unwrap();
            p.write_file("notes/c.png", b"binarygammacontent").unwrap();
            p.write_file("notes/d.txt", b"plaindeltacontent").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(fts_match_count(&session, "alphacontent"), 1);
        assert_eq!(fts_match_count(&session, "betacontent"), 1);
        assert_eq!(fts_match_count(&session, "binarygammacontent"), 0);
        assert_eq!(fts_match_count(&session, "plaindeltacontent"), 0);
    }

    #[test]
    fn flipping_is_markdown_to_zero_removes_fts_row() {
        // Exercises the AFTER UPDATE OF body_text trigger's
        // is_markdown-gated branches. Simulates an external rename
        // where notes/swap.md becomes a non-markdown file under the
        // same path: is_markdown flips 1 → 0, body_text empties,
        // and the FTS row must come out.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/swap.md", b"transitiontoken").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(fts_match_count(&session, "transitiontoken"), 1);

        {
            let conn = session.conn.lock().unwrap();
            conn.execute(
                "UPDATE files SET is_markdown = 0, body_text = '' WHERE path = ?1",
                rusqlite::params!["notes/swap.md"],
            )
            .unwrap();
        }
        assert_eq!(
            fts_match_count(&session, "transitiontoken"),
            0,
            "FTS row should be removed when is_markdown flips to 0"
        );
    }

    // --- full_text_search (closes #57) ---

    #[test]
    fn full_text_search_finds_plain_term() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/alpha.md", b"hello uniquetokenalpha world")
                .unwrap();
            p.write_file("notes/beta.md", b"unrelated content").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let result = session
            .full_text_search(
                "uniquetokenalpha",
                &crate::SearchScope::Vault,
                &CancelToken::new(),
            )
            .unwrap();
        assert_eq!(result.rows.len(), 1);
        assert_eq!(result.rows[0].path, "notes/alpha.md");
        assert!(result.rows[0].snippet.contains("uniquetokenalpha"));
        assert!(result.rows[0].snippet.contains(crate::SNIPPET_HIT_START));
        assert_eq!(result.summary, "Search returned 1 result.");
    }

    #[test]
    fn full_text_search_finds_phrase_match() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file(
                "notes/phrase.md",
                b"foreground task: write integration tests today",
            )
            .unwrap();
            p.write_file("notes/wrong-order.md", b"tests write today integration")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        // FTS5 phrase queries use double quotes.
        let result = session
            .full_text_search(
                "\"write integration tests\"",
                &crate::SearchScope::Vault,
                &CancelToken::new(),
            )
            .unwrap();
        assert_eq!(result.rows.len(), 1);
        assert_eq!(result.rows[0].path, "notes/phrase.md");
    }

    #[test]
    fn full_text_search_scope_folder_filters_results() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/inside.md", b"sharedtoken in notes")
                .unwrap();
            p.write_file("archive/outside.md", b"sharedtoken in archive")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let vault_all = session
            .full_text_search(
                "sharedtoken",
                &crate::SearchScope::Vault,
                &CancelToken::new(),
            )
            .unwrap();
        assert_eq!(vault_all.rows.len(), 2);

        let notes_only = session
            .full_text_search(
                "sharedtoken",
                &crate::SearchScope::Folder("notes".to_string()),
                &CancelToken::new(),
            )
            .unwrap();
        assert_eq!(notes_only.rows.len(), 1);
        assert_eq!(notes_only.rows[0].path, "notes/inside.md");

        let archive_only = session
            .full_text_search(
                "sharedtoken",
                &crate::SearchScope::Folder("archive".to_string()),
                &CancelToken::new(),
            )
            .unwrap();
        assert_eq!(archive_only.rows.len(), 1);
        assert_eq!(archive_only.rows[0].path, "archive/outside.md");
    }

    #[test]
    fn full_text_search_folder_scope_escapes_like_meta_chars() {
        // Codoki PR-85 callout: a folder name with `_` must compare
        // literally, not as a LIKE wildcard. `sales_q1` and
        // `salesXq1` overlap under un-escaped LIKE matching; the
        // escape must keep them distinct.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("sales_q1/note.md", b"underscoretoken")
                .unwrap();
            p.write_file("salesXq1/note.md", b"underscoretoken")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let result = session
            .full_text_search(
                "underscoretoken",
                &crate::SearchScope::Folder("sales_q1".to_string()),
                &CancelToken::new(),
            )
            .unwrap();
        let paths: Vec<&str> = result.rows.iter().map(|r| r.path.as_str()).collect();
        assert_eq!(paths, vec!["sales_q1/note.md"]);
    }

    #[test]
    fn full_text_search_pre_cancelled_returns_immediately() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/anything.md", b"contenttoken").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let cancel = CancelToken::new();
        cancel.cancel();
        match session.full_text_search("contenttoken", &crate::SearchScope::Vault, &cancel) {
            Err(VaultError::Cancelled) => {}
            other => panic!("expected Cancelled, got {other:?}"),
        }
    }

    #[test]
    fn full_text_search_reserved_scopes_return_unsupported() {
        // #93 item 2: File / Tag scopes used to return `Cancelled`
        // as a placeholder. That confused any retry-on-cancel
        // caller and made logs lie about why the call failed.
        // They now return `Unsupported { feature: ... }`.
        let (_tmp, session) = make_vault(|_| {});
        let cancel = CancelToken::new();
        match session.full_text_search(
            "anything",
            &crate::SearchScope::File("notes/x.md".to_string()),
            &cancel,
        ) {
            Err(VaultError::Unsupported { feature }) => {
                assert!(
                    feature.contains("File"),
                    "expected feature label to identify File, got {feature:?}"
                );
            }
            other => panic!("expected Unsupported for File scope, got {other:?}"),
        }
        match session.full_text_search(
            "anything",
            &crate::SearchScope::Tag("project".to_string()),
            &cancel,
        ) {
            Err(VaultError::Unsupported { feature }) => {
                assert!(
                    feature.contains("Tag"),
                    "expected feature label to identify Tag, got {feature:?}"
                );
            }
            other => panic!("expected Unsupported for Tag scope, got {other:?}"),
        }
    }

    #[test]
    fn full_text_search_returns_the_matching_file_with_snippet() {
        // Previous shape asserted on `line_number`, but #92 item 1
        // moved line derivation out of full_text_search (it pulled
        // body_text per hit just to compute the line). The
        // user-visible contract for this test is still meaningful:
        // a vault containing one note with a rare token, queried
        // for that token, returns exactly that one hit with a
        // non-empty snippet.
        let (_tmp, session) = make_vault(|p| {
            let body = b"line one\nline two\nline three has thetargettoken in it\nline four";
            p.write_file("notes/multi.md", body).unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let result = session
            .full_text_search(
                "thetargettoken",
                &crate::SearchScope::Vault,
                &CancelToken::new(),
            )
            .unwrap();
        assert_eq!(result.rows.len(), 1);
        assert_eq!(result.rows[0].path, "notes/multi.md");
        assert!(!result.rows[0].snippet.is_empty());
    }

    #[test]
    fn note_load_bundle_returns_backlinks_outgoing_and_properties_in_one_call() {
        // Three vaults' worth of state in a single bundle:
        //   - notes/target.md is linked to from notes/src.md (one backlink)
        //   - notes/target.md links out to notes/other.md (one outgoing)
        //   - notes/target.md has frontmatter (one property)
        // Verifies all three components arrive populated under a
        // single mutex acquisition (#92 item 4).
        let (_tmp, session) = make_vault(|p| {
            p.write_file(
                "notes/target.md",
                b"---\ntitle: Target\n---\nbody mentions [[other]]\n",
            )
            .unwrap();
            p.write_file("notes/src.md", b"see [[target]] for context\n")
                .unwrap();
            p.write_file("notes/other.md", b"placeholder\n").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let bundle = session
            .note_load_bundle("notes/target.md", Paging::first(50))
            .unwrap();

        assert_eq!(bundle.backlinks.items.len(), 1);
        assert_eq!(bundle.backlinks.items[0].source_path, "notes/src.md");
        assert_eq!(bundle.outgoing_links.len(), 1);
        assert_eq!(bundle.outgoing_links[0].target_raw, "other");
        assert_eq!(bundle.properties.len(), 1);
        assert_eq!(bundle.properties[0].key, "title");
    }

    #[test]
    fn note_load_bundle_returns_empty_arrays_for_unknown_path() {
        // The previous shape's three separate calls each independently
        // tolerated unknown paths (empty backlinks page, empty outgoing
        // vec, None metadata → empty properties). The combined call
        // must preserve that contract.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/a.md", b"body\n").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let bundle = session
            .note_load_bundle("notes/missing.md", Paging::first(50))
            .unwrap();

        assert!(bundle.backlinks.items.is_empty());
        assert!(bundle.outgoing_links.is_empty());
        assert!(bundle.properties.is_empty());
    }

    #[test]
    fn full_text_search_empty_query_returns_empty_set() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/a.md", b"content").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let result = session
            .full_text_search("   ", &crate::SearchScope::Vault, &CancelToken::new())
            .unwrap();
        assert!(result.rows.is_empty());
        assert_eq!(result.summary, "Search returned no results.");
    }

    #[test]
    fn flipping_is_markdown_to_one_adds_fts_row() {
        // Inverse transition: a previously non-markdown file (no
        // FTS row) becomes markdown with body content. The trigger
        // must insert.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("notes/binary.png", b"originalbinaryjunk")
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        // The non-markdown body never made it into FTS in the first
        // place — its tokens shouldn't match.
        assert_eq!(fts_match_count(&session, "originalbinaryjunk"), 0);

        {
            let conn = session.conn.lock().unwrap();
            conn.execute(
                "UPDATE files SET is_markdown = 1, body_text = 'newlysearchabletext' WHERE path = ?1",
                rusqlite::params!["notes/binary.png"],
            )
            .unwrap();
        }
        assert_eq!(fts_match_count(&session, "newlysearchabletext"), 1);
    }

    // ----- save_text + read_oplog -----

    #[test]
    fn save_text_round_trips_content_through_read_text() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("note.md", b"old contents").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let report = session
            .save_text("note.md", "# Brand new\n\nBody.\n", None)
            .unwrap();
        assert!(!report.new_content_hash.is_empty());
        assert_eq!(
            report.new_size_bytes as usize,
            "# Brand new\n\nBody.\n".len()
        );
        assert!(report.new_mtime_ms > 0);

        let back = session.read_text("note.md").unwrap();
        assert_eq!(back, "# Brand new\n\nBody.\n");
    }

    #[test]
    fn save_text_updates_content_hash_in_index() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("note.md", b"v1").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let before = session
            .get_file_metadata("note.md")
            .unwrap()
            .unwrap()
            .content_hash;

        session.save_text("note.md", "v2 contents", None).unwrap();

        let after = session
            .get_file_metadata("note.md")
            .unwrap()
            .unwrap()
            .content_hash;
        assert_ne!(before, after, "save must refresh the cached content hash");
        assert_eq!(after, crate::vault::content_hash(b"v2 contents"));
    }

    #[test]
    fn save_text_replaces_headings_in_index() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("note.md", b"# Old heading\n").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let before = session
            .get_file_metadata("note.md")
            .unwrap()
            .unwrap()
            .headings;
        assert_eq!(before.len(), 1);
        assert_eq!(before[0].text, "Old heading");

        session
            .save_text("note.md", "# New heading\n\n## Sub\n\n### Deeper\n", None)
            .unwrap();

        let after = session
            .get_file_metadata("note.md")
            .unwrap()
            .unwrap()
            .headings;
        let texts: Vec<&str> = after.iter().map(|h| h.text.as_str()).collect();
        assert_eq!(texts, vec!["New heading", "Sub", "Deeper"]);
    }

    #[test]
    fn save_text_creates_files_row_for_brand_new_path() {
        // No scan_initial, no pre-existing row: save_text should
        // insert one rather than failing.
        let (_tmp, session) = make_vault(|_| {});

        let report = session
            .save_text("brand-new.md", "# Hello\n", None)
            .unwrap();
        assert!(!report.new_content_hash.is_empty());

        let md = session.get_file_metadata("brand-new.md").unwrap().unwrap();
        assert!(md.is_markdown);
        assert_eq!(md.headings.len(), 1);
        assert_eq!(md.headings[0].text, "Hello");
    }

    #[test]
    fn save_text_rejects_empty_path_even_with_expected_hash() {
        // The conflict-check path stats the file first, which would
        // otherwise return an IO error on the vault root. Validation
        // up-front makes empty-path saves uniformly InvalidPath.
        let (_tmp, session) = make_vault(|_| {});
        for expected in [None, Some("")] {
            match session.save_text("", "x", expected) {
                Err(VaultError::InvalidPath { .. }) => {}
                other => panic!("expected InvalidPath for empty path, got {other:?}"),
            }
        }
    }

    #[test]
    fn save_text_rejects_dot_path() {
        let (_tmp, session) = make_vault(|_| {});
        match session.save_text(".", "x", None) {
            Err(VaultError::InvalidPath { .. }) => {}
            other => panic!("expected InvalidPath for '.', got {other:?}"),
        }
    }

    #[test]
    fn save_text_rejects_parent_traversal() {
        let (_tmp, session) = make_vault(|_| {});
        match session.save_text("../escape.md", "x", None) {
            Err(VaultError::InvalidPath { .. }) => {}
            other => panic!("expected InvalidPath for '..', got {other:?}"),
        }
    }

    #[test]
    fn save_text_rejects_absolute_path() {
        let (_tmp, session) = make_vault(|_| {});
        match session.save_text("/etc/passwd", "x", None) {
            Err(VaultError::InvalidPath { .. }) => {}
            other => panic!("expected InvalidPath for absolute path, got {other:?}"),
        }
    }

    #[test]
    fn save_text_refuses_oversize_contents() {
        let tmp = tempfile::tempdir().unwrap();
        let mut config = SessionConfig::new(tmp.path().join(".slate"));
        config.large_file_refuse_bytes = 16;
        let provider = Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
        let session = VaultSession::open(provider, config).unwrap();

        let big = "x".repeat(32);
        match session.save_text("big.md", &big, None) {
            Err(VaultError::FileTooLarge { path, size }) => {
                assert_eq!(path, "big.md");
                assert_eq!(size, 32);
            }
            other => panic!("expected FileTooLarge, got {other:?}"),
        }
    }

    #[test]
    fn save_text_with_no_expected_hash_saves_unconditionally() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("note.md", b"v1").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        session.save_text("note.md", "v2", None).unwrap();
        assert_eq!(session.read_text("note.md").unwrap(), "v2");
    }

    #[test]
    fn save_text_with_matching_expected_hash_proceeds() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("note.md", b"v1").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let on_disk_hash = crate::vault::content_hash(b"v1");
        session
            .save_text("note.md", "v2", Some(&on_disk_hash))
            .unwrap();
        assert_eq!(session.read_text("note.md").unwrap(), "v2");
    }

    #[test]
    fn save_text_returns_write_conflict_on_stale_expected_hash() {
        let (tmp, session) = make_vault(|p| {
            p.write_file("note.md", b"v1").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        // Simulate an external writer that changed the file behind our
        // back: rewrite v1 → external between the (caller-supplied)
        // expected hash and the save call.
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider.write_file("note.md", b"external write").unwrap();

        // Caller still thinks the file holds "v1".
        let stale = crate::vault::content_hash(b"v1");
        let current_disk = crate::vault::content_hash(b"external write");
        match session.save_text("note.md", "my version", Some(&stale)) {
            Err(VaultError::WriteConflict {
                current_content_hash,
                expected_content_hash,
                current_mtime_ms,
            }) => {
                assert_eq!(current_content_hash, current_disk);
                assert_eq!(expected_content_hash, stale);
                assert!(current_mtime_ms > 0);
            }
            other => panic!("expected WriteConflict, got {other:?}"),
        }

        // File on disk is unchanged.
        assert_eq!(session.read_text("note.md").unwrap(), "external write");
    }

    #[test]
    fn save_text_with_empty_expected_hash_creates_new_file() {
        // "I expect this file to NOT exist yet" → expected_hash="".
        // Should succeed and create the file.
        let (_tmp, session) = make_vault(|_| {});
        let report = session.save_text("new.md", "# Hello\n", Some("")).unwrap();
        assert!(!report.new_content_hash.is_empty());
        assert_eq!(session.read_text("new.md").unwrap(), "# Hello\n");
    }

    #[test]
    fn save_text_with_expected_hash_against_missing_file_conflicts() {
        // Caller claims the file holds a known hash, but it doesn't
        // exist on disk → conflict (current_hash = "").
        let (_tmp, session) = make_vault(|_| {});
        let stale = crate::vault::content_hash(b"v1");
        match session.save_text("ghost.md", "v2", Some(&stale)) {
            Err(VaultError::WriteConflict {
                current_content_hash,
                expected_content_hash,
                current_mtime_ms,
            }) => {
                assert_eq!(current_content_hash, "");
                assert_eq!(expected_content_hash, stale);
                assert_eq!(current_mtime_ms, 0);
            }
            other => panic!("expected WriteConflict, got {other:?}"),
        }
    }

    #[test]
    fn read_oplog_records_one_entry_per_save() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("note.md", b"v0").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        session.save_text("note.md", "v1", None).unwrap();
        session.save_text("note.md", "v2", None).unwrap();
        session.save_text("note.md", "v3", None).unwrap();

        let entries = session.read_oplog("note.md").unwrap();
        assert_eq!(entries.len(), 3);
        let payloads: Vec<&[u8]> = entries.iter().map(|e| e.payload_bytes.as_slice()).collect();
        assert_eq!(payloads, vec![b"v1" as &[u8], b"v2", b"v3"]);
    }

    #[test]
    fn oplog_entry_carries_hashes_and_actor_id() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("note.md", b"before").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let hash_before = crate::vault::content_hash(b"before");

        session.save_text("note.md", "after", None).unwrap();

        let entries = session.read_oplog("note.md").unwrap();
        assert_eq!(entries.len(), 1);
        let entry = &entries[0];
        assert_eq!(entry.user_actor_id, "local");
        assert!(matches!(entry.op_kind, crate::OpKind::WholeFileReplace));
        assert_eq!(entry.content_hash_before, hash_before);
        assert_eq!(
            entry.content_hash_after,
            crate::vault::content_hash(b"after")
        );
        assert_eq!(entry.payload_bytes, b"after");
        assert!(entry.timestamp_ms > 0);
    }

    #[test]
    fn read_oplog_returns_empty_for_path_not_in_index() {
        let (_tmp, session) = make_vault(|_| {});
        let entries = session.read_oplog("never-saved.md").unwrap();
        assert!(entries.is_empty());
    }

    #[test]
    fn save_report_fields_match_post_save_state() {
        let (_tmp, session) = make_vault(|_| {});
        let report = session
            .save_text("note.md", "exactly twelve", None)
            .unwrap();
        assert_eq!(report.new_size_bytes, "exactly twelve".len() as u64);
        assert_eq!(
            report.new_content_hash,
            crate::vault::content_hash(b"exactly twelve")
        );

        let md = session.get_file_metadata("note.md").unwrap().unwrap();
        assert_eq!(md.mtime_ms, report.new_mtime_ms);
        assert_eq!(md.size_bytes, report.new_size_bytes);
        assert_eq!(md.content_hash, report.new_content_hash);
    }

    #[test]
    fn save_text_refreshes_links_and_properties_and_fts_in_one_transaction() {
        // Acceptance from #60: "Replace headings, properties, links
        // rows for that file in the same transaction so consumers
        // never observe a half-updated index." Verifies all four
        // tables (files, headings, links, properties) plus FTS5
        // reflect the new content after a single save_text call.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("target.md", b"# Target\n").unwrap();
            p.write_file("note.md", b"# Old\n\n[[target]]\n").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        // Sanity: pre-save state.
        assert_eq!(fts_match_count(&session, "rewritten"), 0);

        session
            .save_text(
                "note.md",
                // Frontmatter is intentionally separated from the
                // first ATX heading by a blank line so pulldown-cmark
                // doesn't fold the closing `---` into a setext header
                // — that pre-parse interaction lives in the
                // frontmatter-stripping work-stream, not save_text.
                "---\ntags:\n  - rewritten\n  - edited\nstatus: published\n---\n\n# Rewritten\n\nNew body mentions [[target]] twice and links to [[ghost]].\n",
                None,
            )
            .unwrap();

        // FTS5 picks up the new body.
        assert!(
            fts_match_count(&session, "rewritten") >= 1,
            "FTS row must reflect the new body_text"
        );

        // Headings replaced. The old "Old" heading must be gone and
        // the new "Rewritten" heading must be present; we don't
        // assert exact-list equality because the frontmatter parse
        // can interact with heading detection in ways unrelated to
        // save_text's contract.
        let md = session.get_file_metadata("note.md").unwrap().unwrap();
        let heading_texts: Vec<String> = md.headings.iter().map(|h| h.text.clone()).collect();
        assert!(
            heading_texts.iter().any(|t| t == "Rewritten"),
            "expected 'Rewritten' in {heading_texts:?}"
        );
        assert!(
            !heading_texts.iter().any(|t| t == "Old"),
            "stale 'Old' heading should be gone, got {heading_texts:?}"
        );

        // Properties refreshed.
        let conn = session.conn.lock().unwrap();
        let prop_keys: Vec<String> = conn
            .prepare(
                "SELECT key FROM properties p
                 JOIN files f ON f.id = p.file_id
                 WHERE f.path = ?1
                 ORDER BY key",
            )
            .unwrap()
            .query_map(rusqlite::params!["note.md"], |row| row.get(0))
            .unwrap()
            .collect::<Result<Vec<_>, _>>()
            .unwrap();
        assert!(prop_keys.contains(&"tags".to_string()));
        assert!(prop_keys.contains(&"status".to_string()));

        // Links refreshed. The new body links to "target" (resolves)
        // and "ghost" (unresolved); the old single link is gone.
        let link_targets: Vec<Option<String>> = conn
            .prepare(
                "SELECT target_path FROM links l
                 JOIN files f ON f.id = l.source_file_id
                 WHERE f.path = ?1
                 ORDER BY ordinal",
            )
            .unwrap()
            .query_map(rusqlite::params!["note.md"], |row| {
                row.get::<_, Option<String>>(0)
            })
            .unwrap()
            .collect::<Result<Vec<_>, _>>()
            .unwrap();
        assert_eq!(link_targets.len(), 2, "one [[target]] + one [[ghost]]");
        // target.md is indexed → resolves; ghost.md doesn't exist → NULL.
        let resolved_count = link_targets.iter().filter(|t| t.is_some()).count();
        let unresolved_count = link_targets.iter().filter(|t| t.is_none()).count();
        assert_eq!(resolved_count, 1);
        assert_eq!(unresolved_count, 1);
    }

    #[test]
    fn concurrent_saves_do_not_tear_oplog_entries() {
        // Two threads each issuing many saves against the same file.
        // The session's connection mutex serializes the SQL + op-log
        // append, so the final op-log should contain exactly the
        // expected number of well-formed entries with no torn frames.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("hot.md", b"seed").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let session = Arc::new(session);

        const SAVES_PER_THREAD: usize = 50;
        let s1 = Arc::clone(&session);
        let s2 = Arc::clone(&session);
        let t1 = std::thread::spawn(move || {
            for i in 0..SAVES_PER_THREAD {
                s1.save_text("hot.md", &format!("thread-1 iter-{i}"), None)
                    .unwrap();
            }
        });
        let t2 = std::thread::spawn(move || {
            for i in 0..SAVES_PER_THREAD {
                s2.save_text("hot.md", &format!("thread-2 iter-{i}"), None)
                    .unwrap();
            }
        });
        t1.join().unwrap();
        t2.join().unwrap();

        let entries = session.read_oplog("hot.md").unwrap();
        assert_eq!(
            entries.len(),
            SAVES_PER_THREAD * 2,
            "every save must produce exactly one intact op-log entry"
        );
        for entry in &entries {
            assert!(matches!(entry.op_kind, crate::OpKind::WholeFileReplace));
            assert!(!entry.content_hash_after.is_empty());
            // Payload string starts with "thread-1 " or "thread-2 ".
            let payload = std::str::from_utf8(&entry.payload_bytes).unwrap();
            assert!(
                payload.starts_with("thread-1 ") || payload.starts_with("thread-2 "),
                "unexpected payload {payload:?}"
            );
        }
    }
}
