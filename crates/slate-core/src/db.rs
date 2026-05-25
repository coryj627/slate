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
    conn.pragma_update(None, "journal_mode", "WAL")?;
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

/// Apply all pending migrations and return the final schema version.
///
/// Idempotent: if the database is already current, returns the current
/// version without doing any work.
pub fn migrate(conn: &mut Connection) -> Result<u32, DbError> {
    ensure_version_table(conn)?;
    let current = current_version(conn)?;
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
            apply_migration(conn, target_version, migration)?;
        }
    }

    current_version(conn)
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

fn apply_migration(
    conn: &mut Connection,
    version: u32,
    migration: &Migration,
) -> Result<(), DbError> {
    let tx = conn.transaction()?;
    tx.execute_batch(migration.sql)
        .map_err(|e| DbError::MigrationFailed {
            version,
            description: migration.description,
            message: e.to_string(),
        })?;
    tx.execute(
        "INSERT INTO schema_version (version, applied_at_ms, description) VALUES (?1, ?2, ?3)",
        rusqlite::params![version, now_ms(), migration.description],
    )?;
    tx.commit()?;
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
