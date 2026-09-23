// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The rescan's retained delta ledger (W7-7 PR 7, #1252; contract R-9 in
//! `docs/plans/40_nvda_matrix_remediation_contracts.md`).
//!
//! Windows has no filesystem watcher (OD-1), so a rescan of the open
//! session is the only reconciliation a file created, changed or deleted
//! outside Slate ever gets. The scan itself only updates the index; the
//! host still has to re-seat missing tabs, reload clean ones and refresh
//! Quick Open. It learns WHAT changed from this ledger, never from the
//! file-change event channel, which stays Slate-owned writes only (locked
//! decision `05_locked_architecture_decisions.md:338-342`).
//!
//! ## What a generation is
//!
//! Every rescan's delta is an immutable, addressable **generation**: one
//! row per path whose committed content hash differs from what the index
//! held before the scan — `(kind, path, prior_hash, new_hash)`, a `None`
//! hash meaning "absent". "Modified" is hash-authoritative: a slow-path
//! re-read of unchanged bytes (`files_indexed`) is not a change. There is
//! no rename correlation (AR-8): an external rename is a removal plus a
//! creation, and equal hashes retarget nothing.
//!
//! Rows are stored — and therefore paged — **removal-first** (every
//! removal before any creation or modification), so a host consumes a
//! generation in one bounded pass in page order and a case-only rename's
//! removal has marked its tab missing before the creation re-seats it.
//!
//! ## The ledger: at most one Pending and one Applied generation
//!
//! - **Pending** — the host still owes some page's effects. It owns a
//!   cursor (the first row whose effects are not yet applied); a failed
//!   page leaves the cursor where it was, and the retry resumes there
//!   (page application is idempotent). A new scan is refused while a
//!   Pending generation exists.
//! - **Applied** — every page's effects are applied; the rows are kept
//!   only so the spoken outcome can be reduced. When a Pending generation
//!   becomes Applied it is immediately COALESCED into the one retained
//!   Applied generation by per-path composition (prior hash from the older
//!   row, new hash from the newer; a path whose composed prior equals its
//!   composed new is dropped — its effects were applied, only its speech
//!   nets to nothing), in one transaction, so at most one Applied
//!   generation ever exists and its rows are bounded by the paths touched
//!   since the last release.
//! - **Released** — [`release`] reduces the Applied generation into the
//!   spoken counts and deletes it, atomically. It refuses while a Pending
//!   generation exists, so a retry always speaks the recovered and the new
//!   effects together.
//!
//! Effects are never netted; only speech is.
//!
//! ## Where the ledger lives
//!
//! In TEMP tables on the session's one connection: session-side SQLite,
//! bounded, and written in the SAME transaction as the scan's index
//! mutations (TEMP participates in the connection's transactions), so a
//! fault at the commit boundary leaves both or neither. A TEMP table dies
//! with its connection, so a generation orphaned by a crash or exit can
//! never reach another session; the initial-open scan also discards
//! whatever this session retained before it scans ([`discard_all`]). A
//! persisted table would have to be shared through the cache database
//! with every other session on the same vault (the CLI opens its own),
//! where one process's initial-open discard would silently drop another
//! live process's unreconciled tail.

use std::collections::HashMap;

use rusqlite::{Connection, OptionalExtension, params};

use crate::VaultError;
use crate::db;
use crate::session::Paging;

/// The largest page a host may request. Pages are read on the host's UI
/// thread, so a page is kept small enough to apply in one turn.
pub const MAX_SCAN_DELTA_PAGE_LIMIT: u32 = 1000;

/// What one delta row did to its path, derived from its hashes: absent
/// before and present after is `Created`, the reverse is `Removed`, and
/// present both times with a different hash is `Modified`. There is
/// deliberately no `Renamed` (AR-8).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ScanDeltaKind {
    Removed,
    Created,
    Modified,
}

impl ScanDeltaKind {
    fn code(self) -> i64 {
        match self {
            Self::Removed => 0,
            Self::Created => 1,
            Self::Modified => 2,
        }
    }

    fn from_code(code: i64) -> Result<Self, VaultError> {
        match code {
            0 => Ok(Self::Removed),
            1 => Ok(Self::Created),
            2 => Ok(Self::Modified),
            other => Err(invalid(&format!("unknown scan delta kind {other}"))),
        }
    }

    /// The kind a `(prior, new)` pair describes, or `None` when the pair
    /// is not a change at all.
    fn of(prior: Option<&str>, new: Option<&str>) -> Option<Self> {
        match (prior, new) {
            (None, Some(_)) => Some(Self::Created),
            (Some(_), None) => Some(Self::Removed),
            (Some(before), Some(after)) if before != after => Some(Self::Modified),
            _ => None,
        }
    }
}

/// One entry of a delta page: what happened to one vault-relative path.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ScanDeltaEntry {
    pub kind: ScanDeltaKind,
    pub path: String,
}

/// A bounded page of the Pending generation (the `ListDirChildrenPage`
/// shape: entries plus an opaque continuation, `None` on the last page).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ScanDeltaPage {
    pub generation: u64,
    pub entries: Vec<ScanDeltaEntry>,
    pub next_cursor: Option<String>,
}

/// The Pending generation: its id, the cursor its effects have reached
/// (`None` = nothing applied yet), and how many rows it holds.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ScanDeltaPending {
    pub generation: u64,
    pub cursor: Option<String>,
    pub rows: u64,
}

/// The one retained Applied generation and its row count.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ScanDeltaApplied {
    pub generation: u64,
    pub rows: u64,
}

/// The whole ledger — bounded by construction: at most one of each.
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct ScanDeltaLedger {
    pub pending: Option<ScanDeltaPending>,
    pub applied: Option<ScanDeltaApplied>,
}

/// The spoken outcome of a release: created plus modified paths, and
/// removed paths, net per path since the previous release.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct ScanDeltaOutcome {
    pub changed: u64,
    pub removed: u64,
}

/// One delta row as the scan computes it, before it is stored.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct DeltaRow {
    pub(crate) kind: ScanDeltaKind,
    pub(crate) path: String,
    pub(crate) prior_hash: Option<String>,
    pub(crate) new_hash: Option<String>,
}

const STATE_PENDING: i64 = 0;
const STATE_APPLIED: i64 = 1;
const CURSOR_PREFIX: &str = "sd1";

fn invalid(message: &str) -> VaultError {
    VaultError::InvalidArgument {
        message: message.to_string(),
    }
}

/// Create the ledger's TEMP tables on the session connection. Idempotent;
/// called once when the session opens.
pub(crate) fn ensure_tables(conn: &Connection) -> Result<(), VaultError> {
    conn.execute_batch(
        "CREATE TEMP TABLE IF NOT EXISTS scan_delta_generation (
             generation INTEGER PRIMARY KEY,
             state      INTEGER NOT NULL,
             cursor     INTEGER NOT NULL DEFAULT 0
         );
         CREATE TEMP TABLE IF NOT EXISTS scan_delta_row (
             generation INTEGER NOT NULL,
             seq        INTEGER NOT NULL,
             kind       INTEGER NOT NULL,
             path       TEXT NOT NULL,
             prior_hash TEXT,
             new_hash   TEXT,
             PRIMARY KEY (generation, seq)
         );
         CREATE UNIQUE INDEX IF NOT EXISTS temp.scan_delta_row_path
             ON scan_delta_row (generation, path);",
    )?;
    Ok(())
}

/// The initial-open path's cleanup: drop every retained generation of
/// this session before the scan that rebuilds the index from disk. No
/// workspace state survives an open to reconcile, so nothing retained
/// may be applied or spoken against the rebuilt index.
pub(crate) fn discard_all(conn: &Connection) -> Result<(), VaultError> {
    conn.execute_batch(
        "DELETE FROM temp.scan_delta_row;
         DELETE FROM temp.scan_delta_generation;",
    )?;
    Ok(())
}

/// The hash-authoritative delta between the index as it stood before a
/// scan (`prior`: path → content hash) and as it stands now, inside the
/// scan's transaction. Returned removal-first, each group in path order.
pub(crate) fn diff_against_index(
    conn: &Connection,
    mut prior: HashMap<String, String>,
) -> Result<Vec<DeltaRow>, VaultError> {
    let mut changed: Vec<DeltaRow> = Vec::new();
    {
        let mut stmt = conn.prepare("SELECT path, content_hash FROM files")?;
        let rows = stmt.query_map([], |row| {
            Ok((row.get::<_, String>(0)?, row.get::<_, String>(1)?))
        })?;
        for row in rows {
            let (path, hash) = row?;
            match prior.remove(&path) {
                None => changed.push(DeltaRow {
                    kind: ScanDeltaKind::Created,
                    path,
                    prior_hash: None,
                    new_hash: Some(hash),
                }),
                Some(before) if before != hash => changed.push(DeltaRow {
                    kind: ScanDeltaKind::Modified,
                    path,
                    prior_hash: Some(before),
                    new_hash: Some(hash),
                }),
                Some(_) => {}
            }
        }
    }
    let mut removed: Vec<DeltaRow> = prior
        .into_iter()
        .map(|(path, before)| DeltaRow {
            kind: ScanDeltaKind::Removed,
            path,
            prior_hash: Some(before),
            new_hash: None,
        })
        .collect();
    removed.sort_by(|a, b| a.path.cmp(&b.path));
    changed.sort_by(|a, b| a.path.cmp(&b.path));
    removed.extend(changed);
    Ok(removed)
}

/// Store `rows` as a new Pending generation. Runs inside the scan's own
/// transaction, so the generation commits with the index mutations or
/// not at all. `rows` must already be removal-first
/// ([`diff_against_index`]); their order becomes the paging order.
pub(crate) fn insert_pending(
    conn: &Connection,
    generation: u64,
    rows: &[DeltaRow],
) -> Result<(), VaultError> {
    let generation = to_sql_generation(generation)?;
    conn.execute(
        "INSERT INTO temp.scan_delta_generation (generation, state, cursor) VALUES (?1, ?2, 0)",
        params![generation, STATE_PENDING],
    )?;
    let mut insert = conn.prepare(
        "INSERT INTO temp.scan_delta_row (generation, seq, kind, path, prior_hash, new_hash)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
    )?;
    for (seq, row) in rows.iter().enumerate() {
        let seq = i64::try_from(seq).map_err(|_| invalid("scan delta row out of range"))?;
        insert.execute(params![
            generation,
            seq,
            row.kind.code(),
            row.path,
            row.prior_hash,
            row.new_hash,
        ])?;
    }
    Ok(())
}

fn to_sql_generation(generation: u64) -> Result<i64, VaultError> {
    i64::try_from(generation).map_err(|_| invalid("scan delta generation out of range"))
}

fn from_sql_generation(generation: i64) -> Result<u64, VaultError> {
    u64::try_from(generation).map_err(|_| invalid("scan delta generation out of range"))
}

fn encode_cursor(session_nonce: u64, generation: u64, seq: i64) -> String {
    format!("{CURSOR_PREFIX}:{session_nonce:x}:{generation}:{seq}")
}

/// Decode a cursor this session issued for `generation`. A cursor from
/// another session, another generation or of another shape fails closed.
fn decode_cursor(cursor: &str, session_nonce: u64, generation: u64) -> Result<i64, VaultError> {
    let mut parts = cursor.split(':');
    let prefix = parts.next();
    let nonce = parts.next().and_then(|n| u64::from_str_radix(n, 16).ok());
    let owner = parts.next().and_then(|g| g.parse::<u64>().ok());
    let seq = parts.next().and_then(|s| s.parse::<i64>().ok());
    match (prefix, nonce, owner, seq, parts.next()) {
        (Some(CURSOR_PREFIX), Some(n), Some(g), Some(s), None)
            if n == session_nonce && g == generation && s >= 0 =>
        {
            Ok(s)
        }
        _ => Err(invalid(
            "scan delta cursor does not belong to this generation; resume from its pending cursor",
        )),
    }
}

/// The Pending generation's id and stored cursor, if one exists.
fn pending_row(conn: &Connection) -> Result<Option<(u64, i64)>, VaultError> {
    let row = conn
        .query_row(
            "SELECT generation, cursor FROM temp.scan_delta_generation WHERE state = ?1",
            params![STATE_PENDING],
            |row| Ok((row.get::<_, i64>(0)?, row.get::<_, i64>(1)?)),
        )
        .optional()?;
    match row {
        Some((generation, cursor)) => Ok(Some((from_sql_generation(generation)?, cursor))),
        None => Ok(None),
    }
}

fn applied_generation(conn: &Connection) -> Result<Option<u64>, VaultError> {
    let row = conn
        .query_row(
            "SELECT generation FROM temp.scan_delta_generation WHERE state = ?1",
            params![STATE_APPLIED],
            |row| row.get::<_, i64>(0),
        )
        .optional()?;
    row.map(from_sql_generation).transpose()
}

fn row_count(conn: &Connection, generation: u64) -> Result<u64, VaultError> {
    let count: i64 = conn.query_row(
        "SELECT COUNT(*) FROM temp.scan_delta_row WHERE generation = ?1",
        params![to_sql_generation(generation)?],
        |row| row.get(0),
    )?;
    Ok(u64::try_from(count).unwrap_or_default())
}

/// True when a Pending generation exists — a new scan must not start.
pub(crate) fn has_pending(conn: &Connection) -> Result<bool, VaultError> {
    Ok(pending_row(conn)?.is_some())
}

/// The Pending generation and the cursor its effects have reached.
pub(crate) fn pending(
    conn: &Connection,
    session_nonce: u64,
) -> Result<Option<ScanDeltaPending>, VaultError> {
    let Some((generation, cursor)) = pending_row(conn)? else {
        return Ok(None);
    };
    Ok(Some(ScanDeltaPending {
        generation,
        cursor: (cursor > 0).then(|| encode_cursor(session_nonce, generation, cursor)),
        rows: row_count(conn, generation)?,
    }))
}

/// Both halves of the ledger, for diagnostics and the invariant facts.
pub(crate) fn ledger(conn: &Connection, session_nonce: u64) -> Result<ScanDeltaLedger, VaultError> {
    let applied = match applied_generation(conn)? {
        Some(generation) => Some(ScanDeltaApplied {
            generation,
            rows: row_count(conn, generation)?,
        }),
        None => None,
    };
    Ok(ScanDeltaLedger {
        pending: pending(conn, session_nonce)?,
        applied,
    })
}

/// One bounded page of the Pending generation, in stored (removal-first)
/// order. Reading never moves the stored cursor: only [`page_applied`]
/// does, after the page's effects.
pub(crate) fn page(
    conn: &Connection,
    session_nonce: u64,
    generation: u64,
    paging: &Paging,
) -> Result<ScanDeltaPage, VaultError> {
    if paging.limit == 0 || paging.limit > MAX_SCAN_DELTA_PAGE_LIMIT {
        return Err(invalid(&format!(
            "scan delta page limit must be between 1 and {MAX_SCAN_DELTA_PAGE_LIMIT}"
        )));
    }
    match pending_row(conn)? {
        Some((pending, _)) if pending == generation => {}
        _ => {
            return Err(invalid(&format!(
                "scan delta generation {generation} is not pending"
            )));
        }
    }
    let start = match paging.cursor.as_deref() {
        None => 0,
        Some(cursor) => decode_cursor(cursor, session_nonce, generation)?,
    };
    let fetched: Vec<(i64, i64, String)> = {
        let mut stmt = conn.prepare(
            "SELECT seq, kind, path FROM temp.scan_delta_row
             WHERE generation = ?1 AND seq >= ?2
             ORDER BY seq
             LIMIT ?3",
        )?;
        stmt.query_map(
            params![
                to_sql_generation(generation)?,
                start,
                i64::from(paging.limit) + 1
            ],
            |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
        )?
        .collect::<Result<Vec<_>, _>>()?
    };
    let limit = paging.limit as usize;
    let next_cursor = fetched
        .get(limit)
        .map(|(seq, _, _)| encode_cursor(session_nonce, generation, *seq));
    let entries = fetched
        .into_iter()
        .take(limit)
        .map(|(_, kind, path)| {
            Ok(ScanDeltaEntry {
                kind: ScanDeltaKind::from_code(kind)?,
                path,
            })
        })
        .collect::<Result<Vec<_>, VaultError>>()?;
    Ok(ScanDeltaPage {
        generation,
        entries,
        next_cursor,
    })
}

/// The host applied the effects of every entry before `next_cursor`.
/// `Some` advances the Pending generation's cursor (monotonically — a
/// repeated or older report is a no-op, the idempotent-retry case);
/// `None` means the LAST page's effects landed: the generation becomes
/// Applied and is coalesced into the retained Applied generation, in one
/// transaction.
pub(crate) fn page_applied(
    conn: &Connection,
    session_nonce: u64,
    generation: u64,
    next_cursor: Option<&str>,
) -> Result<(), VaultError> {
    let tx = db::begin_fenced(conn)?;
    let current = match pending_row(&tx)? {
        Some((pending, cursor)) if pending == generation => cursor,
        _ => {
            return Err(invalid(&format!(
                "scan delta generation {generation} is not pending"
            )));
        }
    };
    match next_cursor {
        Some(cursor) => {
            let seq = decode_cursor(cursor, session_nonce, generation)?;
            if seq > current {
                tx.execute(
                    "UPDATE temp.scan_delta_generation SET cursor = ?1 WHERE generation = ?2",
                    params![seq, to_sql_generation(generation)?],
                )?;
            }
        }
        None => coalesce_into_applied(&tx, generation)?,
    }
    tx.commit()?;
    Ok(())
}

#[cfg(test)]
thread_local! {
    /// Test seam: fail the next Pending → Applied transition AFTER its
    /// applied mark and BEFORE its coalesce, so the facts can prove the
    /// two commit together (one transaction) or not at all.
    pub(crate) static FAULT_BETWEEN_MARK_AND_COALESCE: std::cell::Cell<bool> =
        const { std::cell::Cell::new(false) };
}

/// Pending → Applied: the applied mark, then the coalesce into the one
/// retained Applied generation (see the module doc) — both inside the
/// caller's fenced transaction, so a fault between them leaves the
/// generation Pending at its last stored cursor and the Applied
/// generation untouched, and the host's retry finishes it idempotently.
fn coalesce_into_applied(conn: &Connection, generation: u64) -> Result<(), VaultError> {
    let sql_generation = to_sql_generation(generation)?;
    // The applied mark.
    conn.execute(
        "UPDATE temp.scan_delta_generation SET state = ?1, cursor = 0 WHERE generation = ?2",
        params![STATE_APPLIED, sql_generation],
    )?;
    #[cfg(test)]
    if FAULT_BETWEEN_MARK_AND_COALESCE.with(|fault| fault.replace(false)) {
        return Err(invalid(
            "injected fault between the applied mark and the coalesce",
        ));
    }
    // The coalesce, into the OTHER Applied generation when one exists.
    let applied: Option<i64> = conn
        .query_row(
            "SELECT generation FROM temp.scan_delta_generation
             WHERE state = ?1 AND generation != ?2",
            params![STATE_APPLIED, sql_generation],
            |row| row.get(0),
        )
        .optional()?;
    let Some(sql_applied) = applied else {
        return Ok(());
    };
    let newer: Vec<(String, Option<String>, Option<String>)> = {
        let mut stmt = conn.prepare(
            "SELECT path, prior_hash, new_hash FROM temp.scan_delta_row
             WHERE generation = ?1 ORDER BY seq",
        )?;
        stmt.query_map(params![sql_generation], |row| {
            Ok((row.get(0)?, row.get(1)?, row.get(2)?))
        })?
        .collect::<Result<Vec<_>, _>>()?
    };
    let mut next_seq: i64 = conn.query_row(
        "SELECT COALESCE(MAX(seq) + 1, 0) FROM temp.scan_delta_row WHERE generation = ?1",
        params![sql_applied],
        |row| row.get(0),
    )?;
    for (path, newer_prior, newer_new) in newer {
        let older: Option<(i64, Option<String>)> = conn
            .query_row(
                "SELECT seq, prior_hash FROM temp.scan_delta_row
                 WHERE generation = ?1 AND path = ?2",
                params![sql_applied, path],
                |row| Ok((row.get(0)?, row.get(1)?)),
            )
            .optional()?;
        match older {
            // Prior from the older row, new from the newer one.
            Some((seq, older_prior)) => {
                match ScanDeltaKind::of(older_prior.as_deref(), newer_new.as_deref()) {
                    Some(kind) => {
                        conn.execute(
                            "UPDATE temp.scan_delta_row SET kind = ?1, new_hash = ?2
                             WHERE generation = ?3 AND seq = ?4",
                            params![kind.code(), newer_new, sql_applied, seq],
                        )?;
                    }
                    None => {
                        // Back where the last release left it: the effects
                        // were applied, the speech nets to nothing.
                        conn.execute(
                            "DELETE FROM temp.scan_delta_row WHERE generation = ?1 AND seq = ?2",
                            params![sql_applied, seq],
                        )?;
                    }
                }
            }
            None => {
                if let Some(kind) = ScanDeltaKind::of(newer_prior.as_deref(), newer_new.as_deref())
                {
                    conn.execute(
                        "INSERT INTO temp.scan_delta_row
                             (generation, seq, kind, path, prior_hash, new_hash)
                         VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
                        params![
                            sql_applied,
                            next_seq,
                            kind.code(),
                            path,
                            newer_prior,
                            newer_new
                        ],
                    )?;
                    next_seq += 1;
                }
            }
        }
    }
    conn.execute(
        "DELETE FROM temp.scan_delta_row WHERE generation = ?1",
        params![sql_generation],
    )?;
    conn.execute(
        "DELETE FROM temp.scan_delta_generation WHERE generation = ?1",
        params![sql_generation],
    )?;
    Ok(())
}

/// Reduce the one Applied generation into the spoken counts and release
/// it, in one transaction. Refused while a Pending generation exists:
/// releasing then would split a retry's outcome across two sentences.
pub(crate) fn release(conn: &Connection) -> Result<ScanDeltaOutcome, VaultError> {
    let tx = db::begin_fenced(conn)?;
    if pending_row(&tx)?.is_some() {
        return Err(invalid(
            "a pending scan delta generation must be applied before the ledger is released",
        ));
    }
    let Some(applied) = applied_generation(&tx)? else {
        return Ok(ScanDeltaOutcome::default());
    };
    let sql_applied = to_sql_generation(applied)?;
    let (changed, removed): (i64, i64) = tx.query_row(
        "SELECT
             COALESCE(SUM(CASE WHEN kind IN (?2, ?3) THEN 1 ELSE 0 END), 0),
             COALESCE(SUM(CASE WHEN kind = ?4 THEN 1 ELSE 0 END), 0)
         FROM temp.scan_delta_row WHERE generation = ?1",
        params![
            sql_applied,
            ScanDeltaKind::Created.code(),
            ScanDeltaKind::Modified.code(),
            ScanDeltaKind::Removed.code()
        ],
        |row| Ok((row.get(0)?, row.get(1)?)),
    )?;
    tx.execute(
        "DELETE FROM temp.scan_delta_row WHERE generation = ?1",
        params![sql_applied],
    )?;
    tx.execute(
        "DELETE FROM temp.scan_delta_generation WHERE generation = ?1",
        params![sql_applied],
    )?;
    tx.commit()?;
    Ok(ScanDeltaOutcome {
        changed: u64::try_from(changed).unwrap_or_default(),
        removed: u64::try_from(removed).unwrap_or_default(),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn prior(pairs: &[(&str, &str)]) -> HashMap<String, String> {
        pairs
            .iter()
            .map(|(path, hash)| ((*path).to_string(), (*hash).to_string()))
            .collect()
    }

    #[test]
    fn a_pair_names_its_kind_and_an_unchanged_pair_names_none() {
        assert_eq!(
            ScanDeltaKind::of(None, Some("h")),
            Some(ScanDeltaKind::Created)
        );
        assert_eq!(
            ScanDeltaKind::of(Some("h"), None),
            Some(ScanDeltaKind::Removed)
        );
        assert_eq!(
            ScanDeltaKind::of(Some("a"), Some("b")),
            Some(ScanDeltaKind::Modified)
        );
        assert_eq!(ScanDeltaKind::of(Some("h"), Some("h")), None);
        assert_eq!(ScanDeltaKind::of(None, None), None);
    }

    #[test]
    fn cursors_are_bound_to_their_session_and_generation() {
        let cursor = encode_cursor(7, 3, 12);
        assert_eq!(decode_cursor(&cursor, 7, 3).unwrap(), 12);
        assert!(decode_cursor(&cursor, 8, 3).is_err(), "another session");
        assert!(decode_cursor(&cursor, 7, 4).is_err(), "another generation");
        assert!(decode_cursor("sd1:7:3", 7, 3).is_err(), "truncated");
        assert!(decode_cursor("sd1:7:3:12:extra", 7, 3).is_err(), "trailing");
        assert!(decode_cursor("sd1:7:3:-1", 7, 3).is_err(), "negative");
    }

    #[test]
    fn the_diff_is_removal_first_and_hash_authoritative() {
        let mut conn = crate::db::open_in_memory(512).unwrap();
        crate::db::migrate(&mut conn).unwrap();
        conn.execute_batch(
            "INSERT INTO files (path, name, size_bytes, mtime_ms, content_hash, parser_version, indexed_at_ms)
             VALUES ('a.md', 'a.md', 1, 1, 'h1', 1, 1),
                    ('b.md', 'b.md', 1, 1, 'same', 1, 1),
                    ('c.md', 'c.md', 1, 1, 'new', 1, 1);",
        )
        .unwrap();
        // Before: a.md at h0 (modified), b.md unchanged, z.md gone and
        // c.md absent (created). The removal sorts AFTER both others by
        // path and still comes first.
        let rows = diff_against_index(
            &conn,
            prior(&[("a.md", "h0"), ("b.md", "same"), ("z.md", "hz")]),
        )
        .unwrap();
        assert_eq!(
            rows,
            vec![
                DeltaRow {
                    kind: ScanDeltaKind::Removed,
                    path: "z.md".into(),
                    prior_hash: Some("hz".into()),
                    new_hash: None,
                },
                DeltaRow {
                    kind: ScanDeltaKind::Modified,
                    path: "a.md".into(),
                    prior_hash: Some("h0".into()),
                    new_hash: Some("h1".into()),
                },
                DeltaRow {
                    kind: ScanDeltaKind::Created,
                    path: "c.md".into(),
                    prior_hash: None,
                    new_hash: Some("new".into()),
                },
            ]
        );
    }
}
