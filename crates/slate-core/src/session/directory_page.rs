// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Bounded, snapshot-consistent directory-level pages (W1-RT-14).
//!
//! The legacy DirListing contract pages only files and therefore must
//! materialize every sibling directory. This module exposes dirs-first
//! combined pages and a files-only page that seeks past directory prefixes.
//! Opaque keyset cursors are session-, scope-, parent-, and snapshot-bound.
//! Same- or cross-connection mutations fail the whole page instead of
//! silently combining two index states.

use rusqlite::Connection;

use super::{
    CancelToken, DirNodeSummary, FileSummary, Paging, VaultSession, decode_file_summary_query_row,
    file_summary_query_projection, normalize_parent_path, tree_sort_key,
};
use crate::VaultError;

const MAX_DIRECTORY_PAGE_LIMIT: u32 = 10_000;
const MAX_DIRECTORY_CURSOR_BYTES: usize = 256 * 1024;
const SQLITE_PROGRESS_OPS: i32 = 1_000;
pub(super) const FILE_PARENT_EXPRESSION: &str = "CASE WHEN length(path) = length(name) THEN '' \
     ELSE substr(path, 1, length(path) - length(name) - 1) END";

/// One bounded slice of a directory level.
///
/// Combined pages return directories before files. Files-only pages use the
/// same shape with an empty directory vector. A continuation is ephemeral and
/// fails closed if its session, scope, parent, or SQLite index snapshot differs.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DirListingPage {
    /// Directory rows in the bounded slice; always precede files.
    pub dirs: Vec<DirNodeSummary>,
    /// File rows in the bounded slice.
    pub files: Vec<FileSummary>,
    /// Opaque continuation valid only for the producing session/scope/parent
    /// while the SQLite index remains unchanged.
    pub next_cursor: Option<String>,
    /// True exactly when next_cursor is present.
    pub truncated: bool,
    /// Diagnostic snapshot identity; never pass it back as a cursor.
    pub snapshot_id: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
struct DirectorySnapshot {
    data_version: i64,
    total_changes: u64,
}

impl DirectorySnapshot {
    fn capture(conn: &Connection) -> Result<Self, VaultError> {
        let data_version = conn.pragma_query_value(None, "data_version", |row| row.get(0))?;
        Ok(Self {
            data_version,
            total_changes: conn.total_changes(),
        })
    }

    fn id(self, session_nonce: u64) -> String {
        format!(
            "v2:{session_nonce:016x}:{}:{}",
            self.data_version, self.total_changes
        )
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum CursorKind {
    Directory,
    File,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum PageScope {
    Combined,
    FilesOnly,
}

#[derive(Debug, Clone, PartialEq, Eq)]
struct DirectoryPageCursor {
    session_nonce: u64,
    snapshot: DirectorySnapshot,
    scope: PageScope,
    kind: CursorKind,
    parent: String,
    key: String,
    path: String,
}

fn cursor_error(message: &str) -> VaultError {
    VaultError::InvalidArgument {
        message: message.to_string(),
    }
}

fn stale_cursor_error() -> VaultError {
    cursor_error("directory page cursor is stale; restart from the first page")
}

fn encode_cursor(
    session_nonce: u64,
    snapshot: DirectorySnapshot,
    scope: PageScope,
    kind: CursorKind,
    parent: &str,
    key: &str,
    path: &str,
) -> String {
    let scope_kind = match (scope, kind) {
        (PageScope::Combined, CursorKind::Directory) => "cd",
        (PageScope::Combined, CursorKind::File) => "cf",
        (PageScope::FilesOnly, CursorKind::File) => "ff",
        (PageScope::FilesOnly, CursorKind::Directory) => {
            unreachable!("files-only pages cannot emit a directory cursor")
        }
    };
    format!(
        "v2:{session_nonce:016x}:{}:{}:{scope_kind}:{:08x}{:08x}{parent}{key}{path}",
        snapshot.data_version,
        snapshot.total_changes,
        parent.len(),
        key.len(),
    )
}

fn decode_cursor(value: &str) -> Result<DirectoryPageCursor, VaultError> {
    let malformed = || cursor_error("malformed directory page cursor");
    if value.len() > MAX_DIRECTORY_CURSOR_BYTES {
        return Err(cursor_error("directory page cursor is too large"));
    }
    let mut parts = value.splitn(6, ':');
    if parts.next() != Some("v2") {
        return Err(malformed());
    }
    let session_nonce =
        u64::from_str_radix(parts.next().ok_or_else(malformed)?, 16).map_err(|_| malformed())?;
    let data_version = parts
        .next()
        .ok_or_else(malformed)?
        .parse::<i64>()
        .map_err(|_| malformed())?;
    let total_changes = parts
        .next()
        .ok_or_else(malformed)?
        .parse::<u64>()
        .map_err(|_| malformed())?;
    let (scope, kind) = match parts.next() {
        Some("cd") => (PageScope::Combined, CursorKind::Directory),
        Some("cf") => (PageScope::Combined, CursorKind::File),
        Some("ff") => (PageScope::FilesOnly, CursorKind::File),
        _ => return Err(malformed()),
    };
    let payload = parts.next().ok_or_else(malformed)?;
    let bytes = payload.as_bytes();
    if bytes.len() < 16 {
        return Err(malformed());
    }
    let parent_len = usize::from_str_radix(
        std::str::from_utf8(&bytes[..8]).map_err(|_| malformed())?,
        16,
    )
    .map_err(|_| malformed())?;
    let key_len = usize::from_str_radix(
        std::str::from_utf8(&bytes[8..16]).map_err(|_| malformed())?,
        16,
    )
    .map_err(|_| malformed())?;
    let parent_end = 16usize.checked_add(parent_len).ok_or_else(malformed)?;
    let key_end = parent_end.checked_add(key_len).ok_or_else(malformed)?;
    if key_end > bytes.len() {
        return Err(malformed());
    }
    let parent = std::str::from_utf8(&bytes[16..parent_end]).map_err(|_| malformed())?;
    let key = std::str::from_utf8(&bytes[parent_end..key_end]).map_err(|_| malformed())?;
    let path = std::str::from_utf8(&bytes[key_end..]).map_err(|_| malformed())?;
    if path.is_empty() {
        return Err(malformed());
    }
    Ok(DirectoryPageCursor {
        session_nonce,
        snapshot: DirectorySnapshot {
            data_version,
            total_changes,
        },
        scope,
        kind,
        parent: parent.to_string(),
        key: key.to_string(),
        path: path.to_string(),
    })
}

fn check_cancel(cancel: &CancelToken) -> Result<(), VaultError> {
    if cancel.is_cancelled() {
        Err(VaultError::Cancelled)
    } else {
        Ok(())
    }
}

fn with_sqlite_cancellation<T>(
    conn: &Connection,
    cancel: &CancelToken,
    operation: impl FnOnce() -> Result<T, VaultError>,
) -> Result<T, VaultError> {
    let progress_cancel = cancel.clone();
    conn.progress_handler(
        SQLITE_PROGRESS_OPS,
        Some(move || progress_cancel.is_cancelled()),
    )?;
    let result = operation();
    conn.progress_handler(0, None::<fn() -> bool>)?;
    if cancel.is_cancelled() {
        Err(VaultError::Cancelled)
    } else {
        result
    }
}

#[cfg(test)]
#[derive(Clone, Copy, Hash, PartialEq, Eq)]
pub(super) enum DirectoryPageTestPhase {
    BeforeDirectoryQuery,
    AfterDirectoryQuery,
}

#[cfg(test)]
type DirectoryPageTestHook = std::sync::Arc<dyn Fn() + Send + Sync + 'static>;

#[cfg(test)]
fn directory_page_test_hook() -> &'static std::sync::Mutex<
    std::collections::HashMap<(String, DirectoryPageTestPhase), DirectoryPageTestHook>,
> {
    static HOOKS: std::sync::OnceLock<
        std::sync::Mutex<
            std::collections::HashMap<(String, DirectoryPageTestPhase), DirectoryPageTestHook>,
        >,
    > = std::sync::OnceLock::new();
    HOOKS.get_or_init(|| std::sync::Mutex::new(std::collections::HashMap::new()))
}

#[cfg(test)]
pub(super) fn install_directory_page_test_hook(
    parent: String,
    phase: DirectoryPageTestPhase,
    hook: DirectoryPageTestHook,
) {
    let replaced = directory_page_test_hook()
        .lock()
        .unwrap()
        .insert((parent, phase), hook);
    assert!(replaced.is_none(), "duplicate directory-page test hook");
}

#[cfg(test)]
fn run_directory_page_test_hook(parent: &str, phase: DirectoryPageTestPhase) {
    let hook = directory_page_test_hook()
        .lock()
        .unwrap()
        .remove(&(parent.to_owned(), phase));
    if let Some(hook) = hook {
        hook();
    }
}

#[cfg(not(test))]
fn run_before_directory_query_test_hook(_parent: &str) {}

#[cfg(test)]
fn run_before_directory_query_test_hook(parent: &str) {
    run_directory_page_test_hook(parent, DirectoryPageTestPhase::BeforeDirectoryQuery);
}

#[cfg(not(test))]
fn run_after_directory_query_test_hook(_parent: &str) {}

#[cfg(test)]
fn run_after_directory_query_test_hook(parent: &str) {
    run_directory_page_test_hook(parent, DirectoryPageTestPhase::AfterDirectoryQuery);
}

impl VaultSession {
    /// Return one bounded dirs-first page for parent_path.
    ///
    /// The keyset cursor is bound to this session, combined-page scope,
    /// normalized parent, and an unchanged SQLite index snapshot.
    pub fn list_dir_children_page(
        &self,
        parent_path: &str,
        paging: Paging,
        cancel: &CancelToken,
    ) -> Result<DirListingPage, VaultError> {
        self.list_directory_page(parent_path, paging, cancel, PageScope::Combined)
    }

    /// Return one bounded file-only page for a directory level.
    ///
    /// This seeks directly into the file segment, so a directory-heavy level
    /// never has to drain directory pages merely to display its files.
    pub fn list_dir_files_page(
        &self,
        parent_path: &str,
        paging: Paging,
        cancel: &CancelToken,
    ) -> Result<DirListingPage, VaultError> {
        self.list_directory_page(parent_path, paging, cancel, PageScope::FilesOnly)
    }

    fn list_directory_page(
        &self,
        parent_path: &str,
        paging: Paging,
        cancel: &CancelToken,
        scope: PageScope,
    ) -> Result<DirListingPage, VaultError> {
        if paging.limit == 0 || paging.limit > MAX_DIRECTORY_PAGE_LIMIT {
            return Err(cursor_error(&format!(
                "directory page limit must be between 1 and {MAX_DIRECTORY_PAGE_LIMIT}"
            )));
        }
        check_cancel(cancel)?;
        let conn = self.conn.lock().expect("session connection mutex");
        let snapshot = DirectorySnapshot::capture(&conn)?;
        let page = with_sqlite_cancellation(&conn, cancel, || {
            list_dir_children_page_impl(
                &conn,
                parent_path,
                paging,
                cancel,
                snapshot,
                self.directory_cursor_nonce,
                scope,
            )
        })?;
        check_cancel(cancel)?;
        if DirectorySnapshot::capture(&conn)? != snapshot {
            return Err(stale_cursor_error());
        }
        Ok(page)
    }
}

fn list_dir_children_page_impl(
    conn: &Connection,
    parent_path: &str,
    paging: Paging,
    cancel: &CancelToken,
    snapshot: DirectorySnapshot,
    session_nonce: u64,
    scope: PageScope,
) -> Result<DirListingPage, VaultError> {
    let parent = normalize_parent_path(parent_path)?;
    let cursor = paging.cursor.as_deref().map(decode_cursor).transpose()?;
    if let Some(cursor) = cursor.as_ref() {
        if cursor.session_nonce != session_nonce {
            return Err(cursor_error(
                "directory page cursor belongs to a different session",
            ));
        }
        if cursor.scope != scope {
            return Err(cursor_error(
                "directory page cursor belongs to a different listing scope",
            ));
        }
        if cursor.parent != parent {
            return Err(cursor_error(
                "directory page cursor belongs to a different parent",
            ));
        }
        if cursor.snapshot != snapshot {
            return Err(stale_cursor_error());
        }
    }

    let mut directories = Vec::new();
    let mut files = Vec::new();
    let mut remaining = paging.limit as usize;

    if scope == PageScope::Combined
        && !matches!(
            cursor.as_ref().map(|cursor| cursor.kind),
            Some(CursorKind::File)
        )
    {
        let after = cursor
            .as_ref()
            .map(|cursor| (cursor.key.as_str(), cursor.path.as_str()));
        run_before_directory_query_test_hook(&parent);
        append_directory_rows(conn, &parent, after, remaining, &mut directories)?;
        run_after_directory_query_test_hook(&parent);
        check_cancel(cancel)?;

        if directories.len() == remaining {
            let last = directories.last().expect("nonempty full directory page");
            let key = tree_sort_key(&last.name);
            let has_more_directories = has_more_directories(conn, &parent, &key, &last.path)?;
            check_cancel(cancel)?;
            let has_files = if has_more_directories {
                false
            } else {
                has_any_files(conn, &parent)?
            };
            check_cancel(cancel)?;
            let next_cursor = if has_more_directories || has_files {
                Some(encode_cursor(
                    session_nonce,
                    snapshot,
                    scope,
                    CursorKind::Directory,
                    &parent,
                    &key,
                    &last.path,
                ))
            } else {
                None
            };
            return Ok(page_result(
                directories,
                files,
                next_cursor,
                snapshot,
                session_nonce,
            ));
        }
        remaining -= directories.len();
    }

    append_file_rows(conn, &parent, cursor.as_ref(), remaining, &mut files)?;
    check_cancel(cancel)?;
    let next_cursor = if files.len() == remaining {
        let last = files.last().expect("nonempty full file page");
        let key = tree_sort_key(&last.name);
        let has_more = has_more_files(conn, &parent, &key, &last.path)?;
        check_cancel(cancel)?;
        has_more.then(|| {
            encode_cursor(
                session_nonce,
                snapshot,
                scope,
                CursorKind::File,
                &parent,
                &key,
                &last.path,
            )
        })
    } else {
        None
    };
    Ok(page_result(
        directories,
        files,
        next_cursor,
        snapshot,
        session_nonce,
    ))
}

fn page_result(
    dirs: Vec<DirNodeSummary>,
    files: Vec<FileSummary>,
    next_cursor: Option<String>,
    snapshot: DirectorySnapshot,
    session_nonce: u64,
) -> DirListingPage {
    DirListingPage {
        dirs,
        files,
        truncated: next_cursor.is_some(),
        next_cursor,
        snapshot_id: snapshot.id(session_nonce),
    }
}

pub(super) fn directory_query(has_cursor: bool) -> String {
    let continuation = if has_cursor {
        "AND slate_tree_sort_key(d.name) >= ?2
         AND (slate_tree_sort_key(d.name) > ?2
              OR d.path COLLATE BINARY > ?3)"
    } else {
        ""
    };
    format!(
        "SELECT d.id, d.path, d.name,
                (SELECT COUNT(*) FROM dirs child_dir
                 WHERE child_dir.parent_path = d.path),
                (SELECT COUNT(*) FROM files child_file
                 WHERE {FILE_PARENT_EXPRESSION} = d.path),
                EXISTS(
                    SELECT 1 FROM files folder_note
                    WHERE folder_note.path = d.path || '/' || d.name || '.md'
                      AND folder_note.is_markdown = 1
                )
         FROM dirs d
         WHERE d.parent_path = ?1
           {continuation}
         ORDER BY slate_tree_sort_key(d.name), d.path COLLATE BINARY
         LIMIT ?4"
    )
}

fn append_directory_rows(
    conn: &Connection,
    parent: &str,
    after: Option<(&str, &str)>,
    limit: usize,
    directories: &mut Vec<DirNodeSummary>,
) -> Result<(), VaultError> {
    if limit == 0 {
        return Ok(());
    }
    let (after_key, after_path) = after
        .map(|(key, path)| (Some(key), Some(path)))
        .unwrap_or((None, None));
    let query = directory_query(after.is_some());
    let mut stmt = conn.prepare_cached(&query)?;
    let mapped = stmt.query_map(
        rusqlite::params![
            parent,
            after_key,
            after_path,
            i64::try_from(limit).map_err(|_| cursor_error("page limit overflow"))?,
        ],
        |row| {
            Ok(DirNodeSummary {
                id: row.get(0)?,
                path: row.get(1)?,
                name: row.get(2)?,
                child_dir_count: row.get(3)?,
                child_file_count: row.get(4)?,
                has_folder_note: row.get::<_, i64>(5)? != 0,
            })
        },
    )?;
    for row in mapped {
        directories.push(row?);
    }
    Ok(())
}

fn has_more_directories(
    conn: &Connection,
    parent: &str,
    after_key: &str,
    after_path: &str,
) -> Result<bool, VaultError> {
    Ok(conn.query_row(
        "SELECT EXISTS(
             SELECT 1 FROM dirs d
             WHERE d.parent_path = ?1
               AND slate_tree_sort_key(d.name) >= ?2
               AND (slate_tree_sort_key(d.name) > ?2
                    OR d.path COLLATE BINARY > ?3)
             LIMIT 1
         )",
        rusqlite::params![parent, after_key, after_path],
        |row| row.get(0),
    )?)
}

fn has_any_files(conn: &Connection, parent: &str) -> Result<bool, VaultError> {
    Ok(conn.query_row(
        &format!(
            "SELECT EXISTS(SELECT 1 FROM files
             WHERE {FILE_PARENT_EXPRESSION} = ?1 LIMIT 1)"
        ),
        [parent],
        |row| row.get(0),
    )?)
}

pub(super) fn file_query(has_cursor: bool) -> String {
    let continuation = if has_cursor {
        "AND slate_tree_sort_key(name) >= ?2
         AND (slate_tree_sort_key(name) > ?2
              OR path COLLATE BINARY > ?3)"
    } else {
        ""
    };
    let projection = file_summary_query_projection(
        "LEFT JOIN candidates c ON 1 = 1",
        "",
        "ORDER BY slate_tree_sort_key(c.name), c.path COLLATE BINARY",
    );
    format!(
        "WITH candidates AS MATERIALIZED (
             SELECT id, path, name, mtime_ms, size_bytes, is_markdown, birthtime_ms
             FROM files
             WHERE {FILE_PARENT_EXPRESSION} = ?1
               {continuation}
             ORDER BY slate_tree_sort_key(name), path COLLATE BINARY
             LIMIT ?4
         ),
         totals AS (SELECT 0 AS total_filtered)
         {projection}"
    )
}

fn append_file_rows(
    conn: &Connection,
    parent: &str,
    cursor: Option<&DirectoryPageCursor>,
    limit: usize,
    files: &mut Vec<FileSummary>,
) -> Result<(), VaultError> {
    if limit == 0 {
        return Ok(());
    }
    let after = cursor
        .filter(|cursor| cursor.kind == CursorKind::File)
        .map(|cursor| (cursor.key.as_str(), cursor.path.as_str()));
    let (after_key, after_path) = after
        .map(|(key, path)| (Some(key), Some(path)))
        .unwrap_or((None, None));
    let query = file_query(after.is_some());
    let mut stmt = conn.prepare_cached(&query)?;
    let mapped = stmt.query_map(
        rusqlite::params![
            parent,
            after_key,
            after_path,
            i64::try_from(limit).map_err(|_| cursor_error("page limit overflow"))?,
        ],
        decode_file_summary_query_row,
    )?;
    for row in mapped {
        if let (Some(summary), _) = row? {
            files.push(summary);
        }
    }
    Ok(())
}

fn has_more_files(
    conn: &Connection,
    parent: &str,
    after_key: &str,
    after_path: &str,
) -> Result<bool, VaultError> {
    Ok(conn.query_row(
        &format!(
            "SELECT EXISTS(
                 SELECT 1 FROM files
                 WHERE {FILE_PARENT_EXPRESSION} = ?1
                   AND slate_tree_sort_key(name) >= ?2
                   AND (slate_tree_sort_key(name) > ?2
                        OR path COLLATE BINARY > ?3)
                 LIMIT 1
             )"
        ),
        rusqlite::params![parent, after_key, after_path],
        |row| row.get(0),
    )?)
}
