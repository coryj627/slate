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

    // Bail before opening a write transaction so a pre-cancelled token
    // doesn't pay SQLite's open-tx cost.
    if cancel.is_cancelled() {
        return Err(VaultError::Cancelled);
    }
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
            // Check inside the inner loop so a cancel request in the
            // middle of a flat directory of many large files doesn't
            // wait for the whole directory to finish hashing.
            if cancel.is_cancelled() {
                return Err(VaultError::Cancelled);
            }

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
                    ) {
                        report.errors.push(format!("{path}: {e}"));
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
            // Always rewrite ctime_ms alongside indexed_at_ms. For
            // post-migration rows this is a no-op assignment (the
            // values already match). For pre-migration-002 rows
            // carrying ctime_ms = 0, it back-fills the column from
            // the current stat — without this, those rows would
            // permanently fall back to mtime+size-only semantics on
            // every future rescan, defeating the ctime optimization.
            tx.execute(
                "UPDATE files SET indexed_at_ms = ?1, ctime_ms = ?2 WHERE path = ?3",
                rusqlite::params![now, stat.ctime_ms, path],
            )?;
            report.files_skipped += 1;
            return Ok(());
        }
    }

    let content = provider.read_file(path)?;
    let hash = content_hash(&content);

    tx.execute(
        "INSERT INTO files
            (path, name, extension, size_bytes, mtime_ms, ctime_ms,
             content_hash, parser_version, indexed_at_ms, is_markdown)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10)
         ON CONFLICT(path) DO UPDATE SET
            name           = excluded.name,
            extension      = excluded.extension,
            size_bytes     = excluded.size_bytes,
            mtime_ms       = excluded.mtime_ms,
            ctime_ms       = excluded.ctime_ms,
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
            stat.ctime_ms,
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
        // Re-scan after the cache exists. With the mtime/size skip,
        // a.md goes through the fast path on the second pass, so the
        // assertion isn't about files_indexed specifically — it's
        // about "we accounted for exactly one user file, no .yana
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
        let db_path = tmp.path().join(".yana").join("cache.sqlite");
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
        let cache_dir = tmp.path().join(".yana");
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
}
