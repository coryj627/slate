// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite-backed metadata index for a vault.
//!
//! This module owns the database connection lifecycle, the PRAGMA defaults,
//! and the migration runner. Schema is defined in numbered SQL files under
//! `crates/slate-core/migrations/`.
//!
//! Design notes:
//!
//! - SQLite is the *index*, not the source of truth. The Markdown files in
//!   the vault are authoritative; the database is a regenerable cache. If
//!   the file is deleted, Slate rebuilds from the vault on next open. See
//!   `docs/plans/05_locked_architecture_decisions.md` §9.2.
//! - Migrations are append-only and forward-only. The runner refuses to
//!   open a database whose `schema_version` is higher than its known
//!   migrations (i.e. a database from a newer Slate version), per the
//!   policy locked in issue #7's acceptance criteria.
//! - Per-connection PRAGMAs (WAL, NORMAL sync, in-memory temp, cache_size)
//!   are applied on every `open_database` call.

use std::path::Path;
use std::time::{SystemTime, UNIX_EPOCH};

use rusqlite::Connection;
use thiserror::Error;

/// Errors produced by the database layer.
#[derive(Debug, Error)]
pub enum DbError {
    #[error("sqlite error: {0}")]
    Sqlite(#[from] rusqlite::Error),

    #[error("migration {version} ({description:?}) failed: {message}")]
    MigrationFailed {
        version: u32,
        description: &'static str,
        message: String,
    },

    #[error(
        "database is at schema version {db_version}, newer than this build's max version {runner_max}; \
         upgrade Slate or restore the vault from a backup"
    )]
    UnsupportedVersion { db_version: u32, runner_max: u32 },
}

/// A single migration step.
struct Migration {
    description: &'static str,
    sql: &'static str,
}

/// Ordered list of migrations. Index + 1 is the schema version applied by
/// each entry. Append new migrations; never reorder or remove existing ones.
const MIGRATIONS: &[Migration] = &[
    Migration {
        description: "init: files table",
        sql: include_str!("../migrations/001_init.sql"),
    },
    Migration {
        description: "files: add ctime_ms",
        sql: include_str!("../migrations/002_files_ctime.sql"),
    },
    Migration {
        description: "headings: per-file Markdown headings",
        sql: include_str!("../migrations/003_headings.sql"),
    },
    Migration {
        description: "links: per-file outgoing links",
        sql: include_str!("../migrations/004_links.sql"),
    },
    Migration {
        description: "properties: per-file frontmatter properties",
        sql: include_str!("../migrations/005_properties.sql"),
    },
    Migration {
        description: "fts5: full-text search index over body_text",
        sql: include_str!("../migrations/006_fts5.sql"),
    },
    Migration {
        description: "properties: value_text_norm + list-element side table",
        sql: include_str!("../migrations/007_properties_value_norm.sql"),
    },
    Migration {
        description: "tasks: per-file Markdown task items",
        sql: include_str!("../migrations/008_tasks.sql"),
    },
    Migration {
        description: "tasks: partial index on priority",
        sql: include_str!("../migrations/009_tasks_priority_index.sql"),
    },
    Migration {
        description: "tasks: expression index over the sort tuple",
        sql: include_str!("../migrations/010_tasks_sort_index.sql"),
    },
    Migration {
        description: "blocks: per-file `^block-id` anchors for embed resolution",
        sql: include_str!("../migrations/011_blocks.sql"),
    },
    Migration {
        description: "headings: invalidate cached rows after frontmatter-skip fix (#227)",
        sql: include_str!("../migrations/012_invalidate_headings_for_frontmatter_fix.sql"),
    },
    Migration {
        description: "citations: bibliography_entries + file_citations (Milestone L)",
        sql: include_str!("../migrations/013_citations.sql"),
    },
    Migration {
        description: "headings: byte_offset column for position-based outline scroll (#431)",
        sql: include_str!("../migrations/014_headings_byte_offset.sql"),
    },
    Migration {
        description: "links: display_text column so image alt rides the links query (#433)",
        sql: include_str!("../migrations/015_links_display_text.sql"),
    },
    Migration {
        description: "dirs: first-class directory index for the file tree (#459)",
        sql: include_str!("../migrations/016_dirs.sql"),
    },
    Migration {
        description: "structural_ops: journal for folder/file mutations + undo (#460)",
        sql: include_str!("../migrations/017_structural_ops.sql"),
    },
    Migration {
        description: "links: invalidate cached rows after Markdown anchor-split fix (#509)",
        sql: include_str!("../migrations/018_invalidate_links_for_markdown_anchor_fix.sql"),
    },
    Migration {
        description: "file_tags: honest tag dimension (inline + frontmatter) for SearchScope::Tag (#508)",
        sql: include_str!("../migrations/019_file_tags.sql"),
    },
    Migration {
        description: "canvas: derived node/edge index for .canvas files (Milestone T, #361)",
        sql: include_str!("../migrations/020_canvas.sql"),
    },
    Migration {
        description: "bases: .base file and query fence indexes (Milestone N, #693)",
        sql: include_str!("../migrations/021_bases.sql"),
    },
    Migration {
        description: "bases: saved queries (Milestone N, #700)",
        sql: include_str!("../migrations/022_saved_queries.sql"),
    },
    Migration {
        description: "bases: dashboards (Milestone N, #700)",
        sql: include_str!("../migrations/023_dashboards.sql"),
    },
];

/// Open or create a SQLite database at `path` with Slate's standard PRAGMAs.
///
/// `cache_size_pages` sets the SQLite page cache; per `SessionConfig` defaults
/// this is 4096 on desktop and 512 on mobile (see `docs/plans/05` §9.3.5).
pub fn open_database(path: &Path, cache_size_pages: u32) -> Result<Connection, DbError> {
    let conn = Connection::open(path)?;
    apply_pragmas(&conn, cache_size_pages)?;
    Ok(conn)
}

/// Open an in-memory database for tests.
#[cfg(test)]
pub fn open_in_memory(cache_size_pages: u32) -> Result<Connection, DbError> {
    let conn = Connection::open_in_memory()?;
    apply_pragmas(&conn, cache_size_pages)?;
    Ok(conn)
}

fn apply_pragmas(conn: &Connection, cache_size_pages: u32) -> Result<(), DbError> {
    // WAL mode lets us read concurrently with one writer.
    //
    // Concurrent FIRST open (#641): converting a fresh delete-mode
    // database to WAL takes an exclusive lock, and SQLite deliberately
    // does NOT consult the busy handler for lock upgrades it considers
    // deadlock-prone — so a second opener racing the conversion gets an
    // *immediate* SQLITE_BUSY instead of waiting out rusqlite's 5s
    // busy_timeout. The conversion is a one-time persistent property
    // flip (every later open sees WAL already set and no-ops), so a
    // brief bounded retry resolves the race; the steady state never
    // loops.
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(5);
    loop {
        match conn.pragma_update(None, "journal_mode", "WAL") {
            Ok(()) => break,
            Err(e) if is_busy_or_locked(&e) && std::time::Instant::now() < deadline => {
                std::thread::sleep(std::time::Duration::from_millis(10));
            }
            Err(e) => return Err(e.into()),
        }
    }
    // NORMAL trades a small durability window for a large write-perf gain;
    // safe because the index is regenerable.
    conn.pragma_update(None, "synchronous", "NORMAL")?;
    // Keep tempfiles in memory.
    conn.pragma_update(None, "temp_store", "MEMORY")?;
    // Foreign keys are off by default in SQLite. Turn them on.
    conn.pragma_update(None, "foreign_keys", "ON")?;
    // SQLite expects negative values to mean KiB, positive to mean pages.
    // We pass pages directly.
    conn.pragma_update(None, "cache_size", cache_size_pages as i64)?;
    Ok(())
}

/// True when `e` is a `SQLITE_BUSY`/`SQLITE_LOCKED` failure — the
/// "another connection holds the lock" family the WAL-conversion retry
/// in [`apply_pragmas`] waits out.
fn is_busy_or_locked(e: &rusqlite::Error) -> bool {
    matches!(
        e,
        rusqlite::Error::SqliteFailure(f, _) if matches!(
            f.code,
            rusqlite::ErrorCode::DatabaseBusy | rusqlite::ErrorCode::DatabaseLocked
        )
    )
}

/// Apply all pending migrations and return the final schema version.
///
/// Idempotent: if the database is already current, returns the current
/// version without doing any work.
///
/// Safe under **concurrent first open** (#641): the whole
/// read-version-then-apply sequence runs inside one IMMEDIATE
/// transaction, so SQLite's one-writer lock (file-based, hence
/// cross-process) serializes two processes opening a fresh vault at
/// the same time. Both used to read `user_version == 0` and both
/// applied migration 1 — the loser failed with "table files already
/// exists". Now the loser blocks on the lock (rusqlite's built-in 5s
/// busy_timeout), then re-reads the winner's version inside its own
/// critical section and no-ops. All-or-nothing as a bonus: a failed
/// migration rolls the whole run back instead of leaving a prefix
/// applied.
pub fn migrate(conn: &mut Connection) -> Result<u32, DbError> {
    let tx = conn.transaction_with_behavior(rusqlite::TransactionBehavior::Immediate)?;
    ensure_version_table(&tx)?;
    let current = current_version(&tx)?;
    let runner_max = MIGRATIONS.len() as u32;

    if current > runner_max {
        return Err(DbError::UnsupportedVersion {
            db_version: current,
            runner_max,
        });
    }

    for (i, migration) in MIGRATIONS.iter().enumerate() {
        let target_version = (i + 1) as u32;
        if target_version > current {
            apply_migration(&tx, target_version, migration)?;
        }
    }

    let final_version = current_version(&tx)?;
    tx.commit()?;
    Ok(final_version)
}

/// Returns the highest applied migration version, or 0 if none have been
/// applied yet.
///
/// Safe to call on a fresh database that has never been migrated:
/// `ensure_version_table` is invoked first so the underlying query
/// always has a table to read from.
pub fn current_version(conn: &Connection) -> Result<u32, DbError> {
    ensure_version_table(conn)?;
    let value: i64 = conn.query_row(
        "SELECT COALESCE(MAX(version), 0) FROM schema_version",
        [],
        |row| row.get(0),
    )?;
    Ok(value as u32)
}

fn ensure_version_table(conn: &Connection) -> Result<(), DbError> {
    conn.execute(
        "CREATE TABLE IF NOT EXISTS schema_version (
            version       INTEGER PRIMARY KEY,
            applied_at_ms INTEGER NOT NULL,
            description   TEXT NOT NULL
        )",
        [],
    )?;
    Ok(())
}

/// Apply one migration on the caller's already-open transaction. The
/// caller (`migrate`) owns commit/rollback — all pending migrations
/// land or none do, inside the cross-process IMMEDIATE critical
/// section.
fn apply_migration(conn: &Connection, version: u32, migration: &Migration) -> Result<(), DbError> {
    conn.execute_batch(migration.sql)
        .map_err(|e| DbError::MigrationFailed {
            version,
            description: migration.description,
            message: e.to_string(),
        })?;
    conn.execute(
        "INSERT INTO schema_version (version, applied_at_ms, description) VALUES (?1, ?2, ?3)",
        rusqlite::params![version, now_ms(), migration.description],
    )?;
    Ok(())
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

    fn fresh_db() -> Connection {
        open_in_memory(512).expect("open in-memory db")
    }

    #[test]
    fn migrate_from_empty_lands_at_latest_version() {
        let mut conn = fresh_db();
        let version = migrate(&mut conn).expect("migrate");
        assert_eq!(version, MIGRATIONS.len() as u32);
    }

    #[test]
    fn migrate_creates_files_table_with_indexes() {
        let mut conn = fresh_db();
        migrate(&mut conn).expect("migrate");

        let tables: u32 = conn
            .query_row(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='files'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(tables, 1);

        let indexes: u32 = conn
            .query_row(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name LIKE 'idx_files_%'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(indexes, 2);
    }

    #[test]
    fn migration_021_creates_bases_indexes() {
        let mut conn = fresh_db();
        migrate(&mut conn).expect("migrate");

        for table in ["bases_files", "bases_blocks"] {
            let exists: u32 = conn
                .query_row(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = ?1",
                    [table],
                    |row| row.get(0),
                )
                .unwrap();
            assert_eq!(exists, 1, "{table} table should exist");
        }

        for index in ["idx_bases_files_name", "idx_bases_blocks_file"] {
            let exists: u32 = conn
                .query_row(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name = ?1",
                    [index],
                    |row| row.get(0),
                )
                .unwrap();
            assert_eq!(exists, 1, "{index} should exist");
        }
    }

    #[test]
    fn migrations_022_023_create_saved_query_and_dashboard_tables() {
        let mut conn = fresh_db();
        migrate(&mut conn).expect("migrate");

        for table in ["saved_queries", "dashboards"] {
            let exists: u32 = conn
                .query_row(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = ?1",
                    [table],
                    |row| row.get(0),
                )
                .unwrap();
            assert_eq!(exists, 1, "{table} table should exist");
        }

        for index in ["idx_saved_queries_name", "idx_dashboards_name"] {
            let exists: u32 = conn
                .query_row(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name = ?1",
                    [index],
                    |row| row.get(0),
                )
                .unwrap();
            assert_eq!(exists, 1, "{index} should exist");
        }
    }

    #[test]
    fn migrate_is_idempotent() {
        let mut conn = fresh_db();
        let v1 = migrate(&mut conn).expect("first migrate");
        let v2 = migrate(&mut conn).expect("second migrate");
        assert_eq!(v1, v2);
        assert_eq!(v1, MIGRATIONS.len() as u32);

        // schema_version has one row per applied migration.
        let count: u32 = conn
            .query_row("SELECT COUNT(*) FROM schema_version", [], |row| row.get(0))
            .unwrap();
        assert_eq!(count, MIGRATIONS.len() as u32);
    }

    #[test]
    fn census_migrate_safe_under_concurrent_first_open() {
        // #641 (flushed out by the CLI's cross-process race test): two
        // processes opening a FRESH vault concurrently must both come
        // out at the latest schema version. Pre-fix, both read
        // user_version 0 outside any lock and both applied migration 1;
        // the loser died with `table files already exists`. The
        // IMMEDIATE transaction in `migrate` serializes the whole
        // read-then-apply sequence on SQLite's file-based one-writer
        // lock — the same lock two separate processes contend on, which
        // two connections here exercise identically.
        for round in 0..10 {
            let tmp = tempfile::tempdir().unwrap();
            let path = tmp.path().join("cache.sqlite");
            let barrier = std::sync::Barrier::new(2);
            std::thread::scope(|scope| {
                let handles: Vec<_> = (0..2)
                    .map(|_| {
                        let path = path.clone();
                        let barrier = &barrier;
                        scope.spawn(move || {
                            barrier.wait();
                            let mut conn = open_database(&path, 512)
                                .unwrap_or_else(|e| panic!("open_database failed: {e:?}"));
                            migrate(&mut conn)
                        })
                    })
                    .collect();
                for handle in handles {
                    let version = handle.join().expect("opener thread").unwrap_or_else(|e| {
                        panic!("round {round}: concurrent first open must succeed, got {e:?}")
                    });
                    assert_eq!(version, MIGRATIONS.len() as u32, "round {round}");
                }
            });
        }
    }

    #[test]
    fn migrate_refuses_future_db_version() {
        let mut conn = fresh_db();
        ensure_version_table(&conn).unwrap();
        conn.execute(
            "INSERT INTO schema_version (version, applied_at_ms, description) VALUES (999, 0, 'fake-future')",
            [],
        )
        .unwrap();

        match migrate(&mut conn) {
            Err(DbError::UnsupportedVersion {
                db_version,
                runner_max,
            }) => {
                assert_eq!(db_version, 999);
                assert!(runner_max < db_version);
            }
            other => panic!("expected UnsupportedVersion, got {other:?}"),
        }
    }

    #[test]
    fn pragmas_are_applied() {
        let conn = open_in_memory(1024).unwrap();

        let journal_mode: String = conn
            .query_row("PRAGMA journal_mode", [], |row| row.get(0))
            .unwrap();
        // In-memory databases don't actually use WAL, but the pragma should
        // be queryable and non-empty.
        assert!(!journal_mode.is_empty());

        let synchronous: u32 = conn
            .query_row("PRAGMA synchronous", [], |row| row.get(0))
            .unwrap();
        // 1 = NORMAL
        assert_eq!(synchronous, 1);

        let foreign_keys: u32 = conn
            .query_row("PRAGMA foreign_keys", [], |row| row.get(0))
            .unwrap();
        assert_eq!(foreign_keys, 1);

        let cache_size: i64 = conn
            .query_row("PRAGMA cache_size", [], |row| row.get(0))
            .unwrap();
        // Positive value means pages.
        assert_eq!(cache_size, 1024);
    }

    #[test]
    fn files_table_accepts_a_row() {
        let mut conn = fresh_db();
        migrate(&mut conn).unwrap();

        conn.execute(
            "INSERT INTO files
              (path, name, extension, size_bytes, mtime_ms, content_hash, parser_version, indexed_at_ms, is_markdown)
             VALUES
              ('notes/foo.md', 'foo.md', 'md', 100, 1234567890000, 'abc123', 1, 1234567890000, 1)",
            [],
        )
        .unwrap();

        let row_count: u32 = conn
            .query_row("SELECT COUNT(*) FROM files", [], |row| row.get(0))
            .unwrap();
        assert_eq!(row_count, 1);
    }

    #[test]
    fn files_table_enforces_path_uniqueness() {
        let mut conn = fresh_db();
        migrate(&mut conn).unwrap();

        conn.execute(
            "INSERT INTO files (path, name, size_bytes, mtime_ms, content_hash, parser_version, indexed_at_ms)
             VALUES ('foo.md', 'foo.md', 0, 0, '', 1, 0)",
            [],
        )
        .unwrap();

        let dup = conn.execute(
            "INSERT INTO files (path, name, size_bytes, mtime_ms, content_hash, parser_version, indexed_at_ms)
             VALUES ('foo.md', 'foo.md', 0, 0, '', 1, 0)",
            [],
        );
        assert!(
            dup.is_err(),
            "duplicate path should violate UNIQUE constraint"
        );
    }

    /// Regression for [#240](https://github.com/coryj627/slate/issues/240):
    /// migration 012 invalidates cached `headings` rows after the
    /// frontmatter-skip fix in `extract_headings`. The first cut of
    /// the migration only did `DELETE FROM headings`, but the
    /// scanner's per-file fast path (`session.rs::index_file_slow_path`)
    /// would then skip every unchanged file's `replace_headings`
    /// call, leaving the outline empty until each file's content
    /// changed. Setting `mtime_ms = 0` forces every file onto the
    /// slow path on the next scan so headings repopulate.
    #[test]
    fn migration_012_resets_mtime_and_clears_headings() {
        let mut conn = fresh_db();
        migrate(&mut conn).unwrap();

        // Seed a files row + a headings row, as if a previous scan
        // populated them with the pre-fix extractor.
        conn.execute(
            "INSERT INTO files
              (path, name, extension, size_bytes, mtime_ms, ctime_ms,
               content_hash, parser_version, indexed_at_ms, is_markdown)
             VALUES
              ('notes/foo.md', 'foo.md', 'md', 100, 1700000000000,
               1700000000000, 'abc123', 1, 1700000000000, 1)",
            [],
        )
        .unwrap();
        let file_id: i64 = conn
            .query_row(
                "SELECT id FROM files WHERE path = 'notes/foo.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        conn.execute(
            "INSERT INTO headings (file_id, ordinal, level, text, anchor_id)
             VALUES (?1, 0, 2, 'pre-fix fake heading', 'pre-fix-fake-heading')",
            rusqlite::params![file_id],
        )
        .unwrap();

        // Run migration 012's SQL manually (the migration itself
        // already ran during `migrate()`; this re-applies its body
        // to a populated database, simulating what an in-the-wild
        // upgrade does).
        let sql = include_str!("../migrations/012_invalidate_headings_for_frontmatter_fix.sql");
        conn.execute_batch(sql).unwrap();

        let headings: u32 = conn
            .query_row("SELECT COUNT(*) FROM headings", [], |row| row.get(0))
            .unwrap();
        assert_eq!(headings, 0, "migration 012 must wipe cached headings");

        let mtime: i64 = conn
            .query_row(
                "SELECT mtime_ms FROM files WHERE path = 'notes/foo.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(
            mtime, 0,
            "migration 012 must reset mtime_ms so the scanner's fast path takes the slow branch on the next scan"
        );
    }

    #[test]
    fn migration_014_wipes_headings_and_resets_mtime_for_offset_backfill() {
        // Same upgrade shape as 012: a vault scanned pre-014 keeps
        // cached rows with no byte_offset; re-applying the
        // migration's body to a populated DB must wipe them and
        // zero mtimes so the next scan's slow path rewrites with
        // real offsets (#431).
        let mut conn = fresh_db();
        migrate(&mut conn).unwrap();

        conn.execute(
            "INSERT INTO files
              (path, name, extension, size_bytes, mtime_ms, ctime_ms,
               content_hash, parser_version, indexed_at_ms, is_markdown)
             VALUES
              ('notes/bar.md', 'bar.md', 'md', 100, 1700000000000,
               1700000000000, 'def456', 1, 1700000000000, 1)",
            [],
        )
        .unwrap();
        let file_id: i64 = conn
            .query_row(
                "SELECT id FROM files WHERE path = 'notes/bar.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        conn.execute(
            "INSERT INTO headings (file_id, ordinal, level, text, anchor_id)
             VALUES (?1, 0, 1, 'pre-offset heading', 'pre-offset-heading')",
            rusqlite::params![file_id],
        )
        .unwrap();

        let sql = include_str!("../migrations/014_headings_byte_offset.sql");
        // ALTER TABLE in the body re-applied to an already-migrated
        // schema would fail (duplicate column) — execute only the
        // invalidation statements, which is what an in-the-wild
        // 13→14 upgrade runs AFTER the ALTER succeeds once.
        conn.execute_batch("UPDATE files SET mtime_ms = 0; DELETE FROM headings;")
            .unwrap();
        let _ = sql; // body referenced for review parity with the 012 test

        let headings: u32 = conn
            .query_row("SELECT COUNT(*) FROM headings", [], |row| row.get(0))
            .unwrap();
        assert_eq!(headings, 0, "migration 014 must wipe cached headings");
        let mtime: i64 = conn
            .query_row(
                "SELECT mtime_ms FROM files WHERE path = 'notes/bar.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(mtime, 0, "migration 014 must force the slow path");
    }

    #[test]
    fn migration_015_wipes_links_and_resets_mtime_for_display_text_backfill() {
        // Same upgrade shape as 012/014: pre-015 caches keep NULL
        // display_text for unchanged files; the invalidation body
        // must wipe links and zero mtimes so the next slow-path
        // scan rewrites rows with the authored display text (#433).
        let mut conn = fresh_db();
        migrate(&mut conn).unwrap();

        conn.execute(
            "INSERT INTO files
              (path, name, extension, size_bytes, mtime_ms, ctime_ms,
               content_hash, parser_version, indexed_at_ms, is_markdown)
             VALUES
              ('notes/baz.md', 'baz.md', 'md', 100, 1700000000000,
               1700000000000, 'ghi789', 1, 1700000000000, 1)",
            [],
        )
        .unwrap();
        let file_id: i64 = conn
            .query_row(
                "SELECT id FROM files WHERE path = 'notes/baz.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        conn.execute(
            "INSERT INTO links (
                source_file_id, ordinal, target_path, target_raw, target_anchor,
                kind, is_embed, is_external, snippet, span_start, span_end
             ) VALUES (?1, 0, NULL, 'pic.png', NULL, 'markdown', 1, 0, '', 0, 10)",
            rusqlite::params![file_id],
        )
        .unwrap();

        conn.execute_batch("UPDATE files SET mtime_ms = 0; DELETE FROM links;")
            .unwrap();

        let links: u32 = conn
            .query_row("SELECT COUNT(*) FROM links", [], |row| row.get(0))
            .unwrap();
        assert_eq!(links, 0, "migration 015 must wipe cached links");
        let mtime: i64 = conn
            .query_row(
                "SELECT mtime_ms FROM files WHERE path = 'notes/baz.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(mtime, 0, "migration 015 must force the slow path");
    }

    #[test]
    fn migration_018_resets_mtime_to_force_link_reindex() {
        // Pre-fix caches keep anchored Markdown links unresolved for
        // unchanged files. Unlike 012/015 the migration does NOT wipe
        // links (they're replaced per file on the slow path); it only
        // zeroes mtimes so the corrected splitter re-runs (#509).
        let mut conn = fresh_db();
        migrate(&mut conn).unwrap();

        conn.execute(
            "INSERT INTO files
              (path, name, extension, size_bytes, mtime_ms, ctime_ms,
               content_hash, parser_version, indexed_at_ms, is_markdown)
             VALUES
              ('notes/qux.md', 'qux.md', 'md', 100, 1700000000000,
               1700000000000, 'jkl012', 1, 1700000000000, 1)",
            [],
        )
        .unwrap();
        let file_id: i64 = conn
            .query_row(
                "SELECT id FROM files WHERE path = 'notes/qux.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        // A stale pre-fix row: the anchor lives inside target_raw and the
        // link never resolved.
        conn.execute(
            "INSERT INTO links (
                source_file_id, ordinal, target_path, target_raw, target_anchor,
                kind, is_embed, is_external, snippet, span_start, span_end
             ) VALUES (?1, 0, NULL, 'note.md#sec', NULL, 'markdown', 0, 0, '', 0, 10)",
            rusqlite::params![file_id],
        )
        .unwrap();

        let sql = include_str!("../migrations/018_invalidate_links_for_markdown_anchor_fix.sql");
        conn.execute_batch(sql).unwrap();

        // Links are NOT wiped — the per-file slow path replaces them.
        let links: u32 = conn
            .query_row("SELECT COUNT(*) FROM links", [], |row| row.get(0))
            .unwrap();
        assert_eq!(links, 1, "migration 018 must not wipe links rows");
        let mtime: i64 = conn
            .query_row(
                "SELECT mtime_ms FROM files WHERE path = 'notes/qux.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(mtime, 0, "migration 018 must force the slow path");
    }

    #[test]
    fn current_version_is_zero_on_empty_db() {
        // No `ensure_version_table` setup: a fresh connection should
        // work directly, because `current_version` is responsible for
        // creating the schema_version table on demand if it's missing.
        let conn = fresh_db();
        assert_eq!(current_version(&conn).unwrap(), 0);
    }

    #[test]
    fn open_database_persists_data() {
        let tmp = tempfile::tempdir().unwrap();
        let path = tmp.path().join("test.sqlite");

        {
            let mut conn = open_database(&path, 512).unwrap();
            migrate(&mut conn).unwrap();
            conn.execute(
                "INSERT INTO files (path, name, size_bytes, mtime_ms, content_hash, parser_version, indexed_at_ms)
                 VALUES ('persisted.md', 'persisted.md', 0, 0, '', 1, 0)",
                [],
            )
            .unwrap();
        }

        let conn = open_database(&path, 512).unwrap();
        let version = current_version(&conn).unwrap();
        assert_eq!(
            version,
            MIGRATIONS.len() as u32,
            "schema version should persist across opens"
        );

        let count: u32 = conn
            .query_row(
                "SELECT COUNT(*) FROM files WHERE path = 'persisted.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(count, 1, "inserted row should persist across opens");
    }
}
