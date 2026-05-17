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
//! `cache_dir` defaults to `<vault_root>/.yana`. Callers can override
//! for tests or sandbox layouts.

use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex};
use std::time::{SystemTime, UNIX_EPOCH};

use rusqlite::Connection;

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
    /// Directory where the SQLite cache and other per-vault YANA state lives.
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
/// metadata (`FileMetadata`) is hydrated on demand by future milestones.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FileSummary {
    pub path: String,
    pub name: String,
    pub mtime_ms: i64,
    pub size_bytes: u64,
    pub is_markdown: bool,
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
    pub files_indexed: u64,
    pub bytes_processed: u64,
    /// Per-file errors that did not abort the scan. The scanner keeps
    /// going on individual-file failures so one unreadable file does not
    /// blank the index.
    pub errors: Vec<String>,
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
    /// Cache lives at `<root>/.yana` per the locked storage layout.
    pub fn from_filesystem(root: PathBuf) -> Result<Self, VaultError> {
        let cache_dir = root.join(".yana");
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
    /// are skipped — most importantly `.yana` itself (our cache) and
    /// `.obsidian` (Obsidian-compatible vaults).
    ///
    /// On individual-file errors the scanner records the error in the
    /// report and continues. A cancellation request aborts the scan
    /// mid-transaction; nothing partially-applied lands in the database.
    pub fn scan_initial(&self, cancel: &CancelToken) -> Result<ScanReport, VaultError> {
        let mut conn = self.conn.lock().expect("session connection mutex");
        scan_vault(
            self.provider.as_ref(),
            &mut conn,
            self.config.parser_version,
            cancel,
        )
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
    cancel: &CancelToken,
) -> Result<ScanReport, VaultError> {
    let mut report = ScanReport::default();
    let now = now_ms();

    let tx = conn.transaction()?;

    let mut stack: Vec<String> = vec![String::new()];
    while let Some(dir) = stack.pop() {
        if cancel.is_cancelled() {
            return Err(VaultError::Cancelled);
        }

        let entries = match provider.list_dir(&dir) {
            Ok(e) => e,
            Err(e) => {
                report.errors.push(format!("list_dir {dir:?}: {e}"));
                continue;
            }
        };

        for entry in entries {
            // Skip hidden files/directories. `.yana` (our cache) and
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

            report.files_seen += 1;

            match entry.kind {
                EntryKind::Directory => {
                    stack.push(path);
                }
                EntryKind::File | EntryKind::Symlink => {
                    if let Err(e) = index_file(
                        &tx,
                        provider,
                        &path,
                        &entry.name,
                        parser_version,
                        now,
                        &mut report,
                    ) {
                        report.errors.push(format!("{path}: {e}"));
                    }
                }
            }
        }
    }

    tx.commit()?;
    Ok(report)
}

fn index_file(
    tx: &rusqlite::Transaction,
    provider: &dyn VaultProvider,
    path: &str,
    name: &str,
    parser_version: u32,
    now: i64,
    report: &mut ScanReport,
) -> Result<(), VaultError> {
    let stat = provider.stat(path)?;
    let extension = std::path::Path::new(name)
        .extension()
        .and_then(|s| s.to_str())
        .map(str::to_string);
    let is_markdown = matches!(
        extension.as_deref(),
        Some("md") | Some("markdown") | Some("mdown") | Some("mkd")
    );

    let content = provider.read_file(path)?;
    let hash = content_hash(&content);

    tx.execute(
        "INSERT INTO files
            (path, name, extension, size_bytes, mtime_ms, content_hash,
             parser_version, indexed_at_ms, is_markdown)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9)
         ON CONFLICT(path) DO UPDATE SET
            name           = excluded.name,
            extension      = excluded.extension,
            size_bytes     = excluded.size_bytes,
            mtime_ms       = excluded.mtime_ms,
            content_hash   = excluded.content_hash,
            parser_version = excluded.parser_version,
            indexed_at_ms  = excluded.indexed_at_ms,
            is_markdown    = excluded.is_markdown",
        rusqlite::params![
            path,
            name,
            extension,
            stat.size_bytes as i64,
            stat.mtime_ms,
            hash,
            parser_version,
            now,
            is_markdown as i64,
        ],
    )?;

    report.files_indexed += 1;
    report.bytes_processed += stat.size_bytes;
    Ok(())
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

#[cfg(test)]
mod tests {
    use super::*;

    fn make_vault(setup: impl FnOnce(&FsVaultProvider)) -> (tempfile::TempDir, VaultSession) {
        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        setup(&provider);
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        (tmp, session)
    }

    #[test]
    fn open_creates_cache_database() {
        let tmp = tempfile::tempdir().unwrap();
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        let cache_db = tmp.path().join(".yana").join("cache.sqlite");
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
        // The .yana cache dir is created on session open. Re-scanning
        // must not pick up the cache.sqlite or any other internal file.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"a").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        // Re-scan after the cache exists.
        let report = session.scan_initial(&CancelToken::new()).unwrap();
        assert_eq!(report.files_indexed, 1);
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
}
