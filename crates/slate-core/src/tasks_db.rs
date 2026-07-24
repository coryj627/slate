// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite storage + query surface for Markdown tasks.
//!
//! Schema lives in `migrations/008_tasks.sql`. Rows are written
//! exclusively by the scanner's slow path (and by `save_text`); the
//! query side serves the Mac Tasks panel + vault-wide review view.

use rusqlite::{Connection, Transaction, params};

use crate::VaultError;
use crate::session::{Page, Paging};
use crate::tasks::{TaskItem, extract_tasks};

/// One task plus the file it lives in, for the vault-wide review
/// view. The path + file_name are joined in so the UI doesn't pay a
/// second SQLite roundtrip per row.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TaskWithLocation {
    pub task: TaskItem,
    pub path: String,
    pub file_name: String,
}

/// Filter shape for `tasks_in_vault`. `None` fields mean "no
/// restriction on this axis." Bounds are inclusive/exclusive per
/// `due_from_ms` / `due_to_ms` naming.
#[derive(Debug, Default, Clone)]
pub struct TaskFilter {
    pub completed: Option<bool>,
    pub due_from_ms: Option<i64>,
    pub due_to_ms: Option<i64>,
    pub priority_at_least: Option<i32>,
}

/// Atomically replace every task row for `file_id` with the tasks
/// parsed from `markdown_source`. Called inside the scanner's slow-
/// path transaction (and `save_text`'s) so the tasks table stays
/// lock-step with headings / links / properties.
pub(crate) fn replace_tasks_for_file(
    tx: &Transaction,
    file_id: i64,
    markdown_source: &str,
) -> Result<(), VaultError> {
    tx.execute("DELETE FROM tasks WHERE file_id = ?1", params![file_id])?;
    let tasks = extract_tasks(markdown_source);
    if tasks.is_empty() {
        return Ok(());
    }
    let mut stmt = tx.prepare_cached(
        "INSERT INTO tasks (
            file_id, ordinal, text, status_char, completed,
            due_ms, scheduled_ms, priority, recurrence, line, byte_offset,
            checkbox_start_byte, checkbox_end_byte
        ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13)",
    )?;
    for task in tasks {
        stmt.execute(params![
            file_id,
            task.ordinal as i64,
            task.text,
            task.status_char.to_string(),
            task.completed as i64,
            task.due_ms,
            task.scheduled_ms,
            task.priority,
            task.recurrence,
            task.line as i64,
            task.byte_offset as i64,
            task.checkbox_start_byte as i64,
            task.checkbox_end_byte as i64,
        ])?;
    }
    Ok(())
}

/// Fetch every task in a file in document order. Empty result if
/// the path isn't indexed yet — same shape as `outgoing_links`.
pub(crate) fn tasks_for_file(conn: &Connection, path: &str) -> Result<Vec<TaskItem>, VaultError> {
    let file_id: Option<i64> = conn
        .query_row(
            "SELECT id FROM files WHERE path = ?1",
            params![path],
            |row| row.get(0),
        )
        .ok();
    let Some(file_id) = file_id else {
        return Ok(Vec::new());
    };
    let mut stmt = conn.prepare_cached(
        "SELECT ordinal, text, status_char, completed,
                due_ms, scheduled_ms, priority, recurrence, line, byte_offset,
                checkbox_start_byte, checkbox_end_byte
         FROM tasks WHERE file_id = ?1
         ORDER BY ordinal ASC",
    )?;
    let rows = stmt.query_map(params![file_id], row_to_task)?;
    let mut out = Vec::new();
    for r in rows {
        out.push(r?);
    }
    Ok(out)
}

/// Vault-wide paged task query. Order:
/// `(due_ms ASC NULLS LAST, priority DESC NULLS LAST, file path, ordinal)`
/// so overdue/today/soon surface first, then prioritised, then
/// alphabetically for stable per-file scanning.
pub(crate) fn tasks_in_vault(
    conn: &Connection,
    filter: TaskFilter,
    paging: Paging,
) -> Result<Page<TaskWithLocation>, VaultError> {
    let limit = paging.limit.clamp(1, 1000);

    // Cursor format encodes the full sort tuple so paging is
    // exact even when many rows share a due date:
    //   "<due_ms_or_X>|<priority_or_X>|<path>|<ordinal>"
    // X marks NULL so it sorts after every populated value (we
    // explicitly invert the comparison server-side so NULL → infinity
    // for due/priority).
    let cursor = paging.cursor.as_deref().and_then(parse_cursor);

    // Build the WHERE clause incrementally so we only pay for the
    // filters the caller actually supplied. Indexed columns
    // (completed, due_ms) are pulled into the clause first so SQLite's
    // optimiser can pick the right index.
    let mut where_parts: Vec<String> = Vec::new();
    let mut params_dyn: Vec<Box<dyn rusqlite::ToSql>> = Vec::new();
    if let Some(c) = filter.completed {
        where_parts.push("t.completed = ?".to_string());
        params_dyn.push(Box::new(c as i64));
    }
    if let Some(from) = filter.due_from_ms {
        where_parts.push("t.due_ms >= ?".to_string());
        params_dyn.push(Box::new(from));
    }
    if let Some(to) = filter.due_to_ms {
        where_parts.push("t.due_ms < ?".to_string());
        params_dyn.push(Box::new(to));
    }
    if let Some(p) = filter.priority_at_least {
        where_parts.push("t.priority IS NOT NULL AND t.priority >= ?".to_string());
        params_dyn.push(Box::new(p));
    }
    if let Some((due_key, prio_key, path_key, ord_key)) = cursor.clone() {
        // Re-express the multi-column ordering as a single tuple
        // comparison. SQLite supports the `(a, b, ...) > (?, ?, ...)`
        // shape but only for plain ascending; our ordering mixes
        // ascending and descending with explicit NULL placement, so
        // we encode each axis as a sortable surrogate via IFNULL.
        //
        // The IFNULL sentinels MUST be literals (not parameters)
        // to match the literal expressions in `idx_tasks_sort`
        // (migration 010). Otherwise the planner falls back to a
        // temp-btree sort.
        //
        // Sentinel choices:
        //   - due_ms (ASC NULLS LAST):  i64::MAX  — never negated.
        //   - priority (DESC NULLS LAST): i32::MIN as i64 so the
        //     negation trick in the cursor predicate stays inside
        //     i64 range (`-i64::MIN` overflows; `-(i32::MIN as i64)`
        //     is `i32::MAX + 1`, safe).
        //
        // `COLLATE BINARY` on f.path matches the ORDER BY collation
        // so the cursor advance is byte-precise even if SQLite's
        // default collation for the column changes in a future
        // migration (mirrors the defence in `files_with_property`).
        where_parts.push(
            "(IFNULL(t.due_ms, 9223372036854775807), \
              -IFNULL(t.priority, -2147483648), \
              f.path COLLATE BINARY, t.ordinal) > (?, ?, ? COLLATE BINARY, ?)"
                .to_string(),
        );
        params_dyn.push(Box::new(due_key));
        params_dyn.push(Box::new(-prio_key));
        params_dyn.push(Box::new(path_key));
        params_dyn.push(Box::new(ord_key));
    }

    let where_clause = if where_parts.is_empty() {
        String::new()
    } else {
        format!("WHERE {}", where_parts.join(" AND "))
    };

    // The COUNT(*) subquery uses the SAME `t` / `f` aliases as the
    // outer query so its inner `WHERE t.completed = ?` etc. resolve
    // to the inner tasks / files rows — not the outer ones. With
    // distinct aliases (`t2`/`f2`) the subquery's WHERE would silently
    // become correlated to the outer row, producing wildly wrong
    // totals when any filter is active (Codoki PR 134, High).
    // SQL scoping shadows the outer aliases inside the subquery so
    // this is unambiguous; the `count_where` snippet binds against
    // these inner aliases.
    // The IFNULL sentinels in the ORDER BY are LITERAL constants
    // rather than `?` parameters so the expression-index from
    // migration 010 (`idx_tasks_sort`) can satisfy the sort
    // directly — SQLite's planner matches expression indexes only
    // against literal expressions, not parameter-bound ones (#145).
    // The literals MUST stay in lock-step with the index definition;
    // changing one without the other silently regresses the plan
    // back to `USE TEMP B-TREE FOR ORDER BY`.
    //
    // Same `t`/`f` aliases as the outer query inside the COUNT(*)
    // subquery — `tasks t` shadows the outer alias, so the inner
    // WHERE binds against the inner rows, not the outer row
    // (Codoki PR #134 High regression fix).
    const DUE_NULL_SENTINEL: i64 = i64::MAX;
    const PRIORITY_NULL_SENTINEL: i64 = i32::MIN as i64;
    let sql = format!(
        "SELECT f.path, f.name,
                t.ordinal, t.text, t.status_char, t.completed,
                t.due_ms, t.scheduled_ms, t.priority, t.recurrence,
                t.line, t.byte_offset, t.checkbox_start_byte, t.checkbox_end_byte,
                (SELECT COUNT(*) FROM tasks t JOIN files f ON f.id = t.file_id {count_where}) AS total_filtered
         FROM tasks t
         JOIN files f ON f.id = t.file_id
         {where_clause}
         ORDER BY IFNULL(t.due_ms, {due_sentinel}) ASC,
                  IFNULL(t.priority, {prio_sentinel}) DESC,
                  f.path COLLATE BINARY ASC,
                  t.ordinal ASC
         LIMIT ?",
        count_where = build_count_where(&filter),
        where_clause = where_clause,
        due_sentinel = DUE_NULL_SENTINEL,
        prio_sentinel = PRIORITY_NULL_SENTINEL,
    );

    // Build the count-where bindings (same shape as the main WHERE
    // minus the cursor) so the COUNT(*) subquery sees the same
    // filters and returns a stable total across pages.
    let mut count_params: Vec<Box<dyn rusqlite::ToSql>> = Vec::new();
    if let Some(c) = filter.completed {
        count_params.push(Box::new(c as i64));
    }
    if let Some(from) = filter.due_from_ms {
        count_params.push(Box::new(from));
    }
    if let Some(to) = filter.due_to_ms {
        count_params.push(Box::new(to));
    }
    if let Some(p) = filter.priority_at_least {
        count_params.push(Box::new(p));
    }

    // SQL parameters must be supplied in textual order. The COUNT(*)
    // subquery is positioned BEFORE the outer WHERE in the SQL we
    // built, so its filter parameters bind first. Then the outer
    // filter parameters. Then `limit + 1` (paging "+1 trick" for
    // has_more detection). The IFNULL sort sentinels are now SQL
    // literals (see the migration-010 commentary above), so they
    // don't appear in the parameter list.
    let mut bound: Vec<Box<dyn rusqlite::ToSql>> = Vec::new();
    bound.extend(count_params);
    bound.extend(params_dyn);
    bound.push(Box::new((limit as i64) + 1));

    let mut stmt = conn.prepare_cached(&sql)?;
    let bound_refs: Vec<&dyn rusqlite::ToSql> = bound.iter().map(|b| b.as_ref()).collect();
    let mut total_filtered: u64 = 0;
    let rows: Vec<TaskWithLocation> = stmt
        .query_map(rusqlite::params_from_iter(bound_refs.iter()), |row| {
            let path: String = row.get(0)?;
            let name: String = row.get(1)?;
            let item = TaskItem {
                ordinal: row.get::<_, i64>(2)? as u32,
                text: row.get(3)?,
                status_char: row.get::<_, String>(4)?.chars().next().unwrap_or(' '),
                completed: row.get::<_, i64>(5)? != 0,
                due_ms: row.get(6)?,
                scheduled_ms: row.get(7)?,
                priority: row.get::<_, Option<i64>>(8)?.map(|p| p as i32),
                recurrence: row.get(9)?,
                line: row.get::<_, i64>(10)? as u32,
                byte_offset: row.get::<_, i64>(11)? as u32,
                checkbox_start_byte: row.get::<_, i64>(12)? as u32,
                checkbox_end_byte: row.get::<_, i64>(13)? as u32,
            };
            let count: i64 = row.get(14)?;
            total_filtered = count as u64;
            Ok(TaskWithLocation {
                task: item,
                path,
                file_name: name,
            })
        })?
        .collect::<Result<Vec<_>, _>>()?;

    // If the paged query returned zero rows (cursor lands past the
    // last match), the COUNT(*) subquery's row was never observed.
    // Re-fetch the total via a dedicated count query.
    if rows.is_empty() {
        let count_sql = format!(
            "SELECT COUNT(*) FROM tasks t JOIN files f ON f.id = t.file_id {}",
            build_count_where(&filter),
        );
        let mut count_stmt = conn.prepare_cached(&count_sql)?;
        let mut count_args: Vec<Box<dyn rusqlite::ToSql>> = Vec::new();
        if let Some(c) = filter.completed {
            count_args.push(Box::new(c as i64));
        }
        if let Some(from) = filter.due_from_ms {
            count_args.push(Box::new(from));
        }
        if let Some(to) = filter.due_to_ms {
            count_args.push(Box::new(to));
        }
        if let Some(p) = filter.priority_at_least {
            count_args.push(Box::new(p));
        }
        let count_refs: Vec<&dyn rusqlite::ToSql> = count_args.iter().map(|b| b.as_ref()).collect();
        total_filtered = count_stmt
            .query_row(rusqlite::params_from_iter(count_refs.iter()), |row| {
                row.get::<_, i64>(0)
            })? as u64;
    }

    let has_more = rows.len() > limit as usize;
    let items: Vec<TaskWithLocation> = rows.into_iter().take(limit as usize).collect();
    let next_cursor = if has_more {
        items.last().map(|row| {
            format_cursor(
                row.task.due_ms,
                row.task.priority,
                &row.path,
                row.task.ordinal,
            )
        })
    } else {
        None
    };
    Ok(Page {
        items,
        next_cursor,
        total_filtered,
    })
}

fn build_count_where(filter: &TaskFilter) -> String {
    let mut parts: Vec<&'static str> = Vec::new();
    if filter.completed.is_some() {
        parts.push("t.completed = ?");
    }
    if filter.due_from_ms.is_some() {
        parts.push("t.due_ms >= ?");
    }
    if filter.due_to_ms.is_some() {
        parts.push("t.due_ms < ?");
    }
    if filter.priority_at_least.is_some() {
        parts.push("t.priority IS NOT NULL AND t.priority >= ?");
    }
    if parts.is_empty() {
        String::new()
    } else {
        format!("WHERE {}", parts.join(" AND "))
    }
}

fn row_to_task(row: &rusqlite::Row) -> rusqlite::Result<TaskItem> {
    Ok(TaskItem {
        ordinal: row.get::<_, i64>(0)? as u32,
        text: row.get(1)?,
        status_char: row.get::<_, String>(2)?.chars().next().unwrap_or(' '),
        completed: row.get::<_, i64>(3)? != 0,
        due_ms: row.get(4)?,
        scheduled_ms: row.get(5)?,
        priority: row.get::<_, Option<i64>>(6)?.map(|p| p as i32),
        recurrence: row.get(7)?,
        line: row.get::<_, i64>(8)? as u32,
        byte_offset: row.get::<_, i64>(9)? as u32,
        checkbox_start_byte: row.get::<_, i64>(10)? as u32,
        checkbox_end_byte: row.get::<_, i64>(11)? as u32,
    })
}

/// Encode the full sort tuple into a single cursor string so a
/// paged caller can pick up exactly where the previous page
/// stopped. NULL due/priority sort last; the encoding uses sentinel
/// strings ("X") rather than a numeric placeholder so the server
/// can route them through `IFNULL` to the same i64 sentinels used
/// in the ORDER BY.
fn format_cursor(due_ms: Option<i64>, priority: Option<i32>, path: &str, ordinal: u32) -> String {
    let d = due_ms
        .map(|x| x.to_string())
        .unwrap_or_else(|| "X".to_string());
    let p = priority
        .map(|x| x.to_string())
        .unwrap_or_else(|| "X".to_string());
    format!("{d}|{p}|{path}|{ordinal}")
}

fn parse_cursor(s: &str) -> Option<(i64, i64, String, i64)> {
    // The path component may legally contain `|` (e.g. a filename
    // with a literal pipe). Parse from the left for `due` and
    // `priority`, then from the right for `ordinal`, so the
    // remainder lands in `path` verbatim.
    let (due_part, rest) = s.split_once('|')?;
    let (prio_part, rest) = rest.split_once('|')?;
    let (path_part, ord_part) = rest.rsplit_once('|')?;
    let due = if due_part == "X" {
        i64::MAX
    } else {
        due_part.parse().ok()?
    };
    // Matches the server-side IFNULL sentinel: NULL priorities
    // collapse to `i32::MIN as i64` so the negation in the cursor
    // predicate stays inside i64 range.
    let prio = if prio_part == "X" {
        i32::MIN as i64
    } else {
        prio_part.parse::<i32>().ok()? as i64
    };
    let ord: i64 = ord_part.parse().ok()?;
    Some((due, prio, path_part.to_string(), ord))
}
