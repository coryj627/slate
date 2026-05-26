// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — task scan, save_text rewrites, toggle_task_status, tasks_in_vault filters and paging.
//!
//! Extracted from `crates/slate-core/src/session.rs` as part of #272.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;

type TaskSnapshotRow = (u32, char, bool, String, Option<i64>, Option<i32>);

/// One row of `EXPLAIN QUERY PLAN` output, parsed into its column
/// shape rather than collapsed into one substring-matched blob.
/// Used by the two planner-shape tests below — audit #284 flagged
/// the original concatenate-then-substring pattern as brittle to
/// SQLite phrasing tweaks; this structural form keeps the index-
/// name check (which IS stable across SQLite versions) but gives
/// per-row failure messages.
#[derive(Debug, Clone)]
struct PlanRow {
    id: i64,
    parent: i64,
    detail: String,
}

/// Run `EXPLAIN QUERY PLAN <sql>` and collect the per-step rows.
/// The `notused` column (column 2 in SQLite ≥ 3.24) is skipped;
/// `detail` (column 3) is what carries "USING INDEX <name>" and
/// "SCAN <table>" markers the tests assert against.
fn explain_plan_rows(
    conn: &rusqlite::Connection,
    sql: &str,
    params: &[&dyn rusqlite::ToSql],
) -> Vec<PlanRow> {
    let explain = format!("EXPLAIN QUERY PLAN {sql}");
    conn.prepare(&explain)
        .unwrap()
        .query_map(params, |row| {
            Ok(PlanRow {
                id: row.get::<_, i64>(0)?,
                parent: row.get::<_, i64>(1)?,
                detail: row.get::<_, String>(3)?,
            })
        })
        .unwrap()
        .collect::<Result<Vec<_>, _>>()
        .unwrap()
}

/// Render a plan as a multi-line string for assertion failure
/// messages. Each row shows its tree id + parent so the optimizer's
/// step hierarchy is visible — easier to debug a regression than
/// the joined-detail blob the old tests printed.
fn format_plan(rows: &[PlanRow]) -> String {
    rows.iter()
        .map(|row| format!("  [id={}, parent={}] {}", row.id, row.parent, row.detail))
        .collect::<Vec<_>>()
        .join("\n")
}

fn tasks_snapshot(session: &VaultSession, path: &str) -> Vec<TaskSnapshotRow> {
    session
        .tasks_for_file(path)
        .unwrap()
        .into_iter()
        .map(|t| {
            (
                t.ordinal,
                t.status_char,
                t.completed,
                t.text,
                t.due_ms,
                t.priority,
            )
        })
        .collect()
}

#[test]
fn scan_populates_tasks_table_from_markdown_body() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "notes/todos.md",
            b"# To do\n\n- [ ] open\n- [x] done\n- [/] doing\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let tasks = session.tasks_for_file("notes/todos.md").unwrap();
    assert_eq!(tasks.len(), 3);
    assert_eq!(tasks[0].status_char, ' ');
    assert_eq!(tasks[1].status_char, 'x');
    assert!(tasks[1].completed);
    assert_eq!(tasks[2].status_char, '/');
    assert!(!tasks[2].completed);
}

#[test]
fn save_text_rewrites_tasks_table_on_edit() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/n.md", b"- [ ] a\n- [ ] b\n- [ ] c\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(session.tasks_for_file("notes/n.md").unwrap().len(), 3);

    // Toggle b → done and drop c entirely.
    session
        .save_text("notes/n.md", "- [ ] a\n- [x] b\n", None)
        .unwrap();
    let after = session.tasks_for_file("notes/n.md").unwrap();
    assert_eq!(after.len(), 2);
    assert_eq!(after[0].status_char, ' ');
    assert_eq!(after[1].status_char, 'x');
    assert_eq!(after[1].text, "b");
}

#[test]
fn fast_path_rescan_does_not_touch_tasks_table() {
    // Mirror of `fast_path_does_not_rewrite_links` — an unchanged
    // file must not churn the tasks rows. We capture a snapshot,
    // rescan, snapshot again, and compare for byte-level identity.
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "notes/t.md",
            "- [ ] alpha 📅 2026-06-01 ⏫\n- [x] beta\n".as_bytes(),
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = tasks_snapshot(&session, "notes/t.md");
    assert_eq!(before.len(), 2);

    session.scan_initial(&CancelToken::new()).unwrap();
    let after = tasks_snapshot(&session, "notes/t.md");
    assert_eq!(after, before, "fast path must not touch tasks");
}

#[test]
fn scan_picks_up_added_and_removed_tasks_on_content_change() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/n.md", b"- [ ] one\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(session.tasks_for_file("notes/n.md").unwrap().len(), 1);

    // Add two tasks; rescan picks them up.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("notes/n.md", b"- [ ] one\n- [ ] two\n- [ ] three\n")
        .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(session.tasks_for_file("notes/n.md").unwrap().len(), 3);

    // Remove all tasks; rescan drops the rows.
    provider.write_file("notes/n.md", b"plain text\n").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(session.tasks_for_file("notes/n.md").unwrap().is_empty());
}

#[test]
fn tasks_in_vault_empty_filter_returns_every_task_in_sort_order() {
    // Sort order is (due ASC NULLS LAST, priority DESC NULLS LAST,
    // path ASC, ordinal ASC). Build a vault that exercises every
    // axis so the ordering is unambiguous.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", "- [ ] no metadata\n- [ ] high pri 🔼\n".as_bytes())
            .unwrap();
        p.write_file(
            "b.md",
            "- [ ] due tomorrow 📅 2026-05-24 🔽\n- [ ] due tomorrow no pri 📅 2026-05-24\n"
                .as_bytes(),
        )
        .unwrap();
        p.write_file("c.md", "- [ ] due today 📅 2026-05-23\n".as_bytes())
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let page = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(100))
        .unwrap();
    let order: Vec<&str> = page.items.iter().map(|r| r.task.text.as_str()).collect();
    assert_eq!(
        order,
        vec![
            // Due today first.
            "due today",
            // Both due tomorrow — priority DESC NULLS LAST means
            // a populated priority (-1 here) outranks NULL even
            // when -1 is the lowest "real" priority.
            "due tomorrow",
            "due tomorrow no pri",
            // No due date — sort last; high-pri before no-pri.
            "high pri",
            "no metadata",
        ]
    );
    assert_eq!(page.total_filtered, 5);
    assert!(page.next_cursor.is_none());
}

#[test]
fn tasks_in_vault_completed_filter_excludes_done_tasks() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"- [ ] open\n- [x] done\n- [/] doing\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let page = session
        .tasks_in_vault(
            crate::TaskFilter {
                completed: Some(false),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    let texts: Vec<&str> = page.items.iter().map(|r| r.task.text.as_str()).collect();
    // Only the unchecked + in-progress tasks survive (done is
    // status_char `x` → completed=true).
    assert_eq!(texts, vec!["open", "doing"]);
    // total_filtered must match the filtered row count — not the
    // global count. Regression for the COUNT(*) subquery alias
    // bug (Codoki PR 134, High): with `t2`/`f2` aliases the
    // subquery's `WHERE t.completed = ?` resolved to the outer
    // `t`, turning the count into a correlated boolean and
    // returning the wrong total under any filter.
    assert_eq!(page.total_filtered, 2);
}

#[test]
fn tasks_in_vault_total_filtered_under_filters_matches_actual_count() {
    // Direct regression for the COUNT(*) subquery alias bug.
    // Build a vault where the global count and the
    // per-filter count differ on every filter axis, so an alias
    // slip on any one of them shows up here.
    let (_tmp, session) = make_vault(|p| {
        // 5 total tasks: 3 open / 2 done, 2 due in window / 3 out,
        // 1 highest priority / 1 high / 3 with no priority.
        p.write_file(
            "a.md",
            "- [ ] in window high pri 📅 2026-06-01 🔼\n\
             - [x] in window done 📅 2026-06-02\n\
             - [ ] out of window 📅 2026-07-01\n\
             - [ ] no due\n\
             - [x] no due done ⏫\n"
                .as_bytes(),
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let only_open = session
        .tasks_in_vault(
            crate::TaskFilter {
                completed: Some(false),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    assert_eq!(only_open.items.len(), 3);
    assert_eq!(only_open.total_filtered, 3);

    let only_done = session
        .tasks_in_vault(
            crate::TaskFilter {
                completed: Some(true),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    assert_eq!(only_done.items.len(), 2);
    assert_eq!(only_done.total_filtered, 2);

    let from = NaiveDate::from_ymd_opt(2026, 6, 1)
        .unwrap()
        .and_hms_opt(0, 0, 0)
        .unwrap()
        .and_utc()
        .timestamp_millis();
    let to = NaiveDate::from_ymd_opt(2026, 6, 30)
        .unwrap()
        .and_hms_opt(0, 0, 0)
        .unwrap()
        .and_utc()
        .timestamp_millis();
    let june_due = session
        .tasks_in_vault(
            crate::TaskFilter {
                due_from_ms: Some(from),
                due_to_ms: Some(to),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    assert_eq!(june_due.items.len(), 2);
    assert_eq!(june_due.total_filtered, 2);

    let high_or_better = session
        .tasks_in_vault(
            crate::TaskFilter {
                priority_at_least: Some(1),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    assert_eq!(high_or_better.items.len(), 2);
    assert_eq!(high_or_better.total_filtered, 2);
}

#[test]
fn tasks_in_vault_due_window_inclusive_lower_exclusive_upper() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "a.md",
            "- [ ] just before 📅 2026-05-22\n- [ ] from 📅 2026-05-23\n- [ ] to 📅 2026-05-24\n- [ ] after 📅 2026-05-25\n"
                .as_bytes(),
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let from = NaiveDate::from_ymd_opt(2026, 5, 23)
        .unwrap()
        .and_hms_opt(0, 0, 0)
        .unwrap()
        .and_utc()
        .timestamp_millis();
    let to = NaiveDate::from_ymd_opt(2026, 5, 24)
        .unwrap()
        .and_hms_opt(0, 0, 0)
        .unwrap()
        .and_utc()
        .timestamp_millis();
    let page = session
        .tasks_in_vault(
            crate::TaskFilter {
                due_from_ms: Some(from),
                due_to_ms: Some(to),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    let texts: Vec<&str> = page.items.iter().map(|r| r.task.text.as_str()).collect();
    // [from, to) → from-date matches, to-date excluded.
    assert_eq!(texts, vec!["from"]);
}

#[test]
fn tasks_in_vault_paging_round_trips() {
    let (_tmp, session) = make_vault(|p| {
        let mut body = String::new();
        for i in 0..7 {
            body.push_str(&format!("- [ ] task {i:02}\n"));
        }
        p.write_file("a.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let limit = 3;
    let mut cursor: Option<String> = None;
    let mut seen: Vec<String> = Vec::new();
    loop {
        let paging = match &cursor {
            Some(c) => Paging::after(c.clone(), limit),
            None => Paging::first(limit),
        };
        let page = session
            .tasks_in_vault(crate::TaskFilter::default(), paging)
            .unwrap();
        assert_eq!(page.total_filtered, 7);
        for row in &page.items {
            seen.push(row.task.text.clone());
        }
        if let Some(c) = page.next_cursor {
            cursor = Some(c);
        } else {
            break;
        }
    }
    let expected: Vec<String> = (0..7).map(|i| format!("task {i:02}")).collect();
    assert_eq!(seen, expected);
}

#[test]
fn priority_at_least_filter_uses_idx_tasks_priority_not_table_scan() {
    // Regression for #139 (red-team M1). Before migration 009,
    // `tasks_in_vault` with `priority_at_least = Some(_)`
    // produced an `EXPLAIN QUERY PLAN` of `SCAN tasks` —
    // sequential table scan on every page. After the partial
    // index lands, the planner switches to `SEARCH … USING
    // INDEX idx_tasks_priority`.
    //
    // Populating ~500 prioritised rows + ANALYZE so the planner
    // has real cardinality estimates instead of the empty-table
    // defaults that would mask the plan choice.
    let (_tmp, session) = make_vault(|p| {
        let mut body = String::with_capacity(20_000);
        for i in 0..500 {
            let marker = match i % 4 {
                0 => "⏫",
                1 => "🔼",
                2 => "🔽",
                _ => "⏬",
            };
            body.push_str(&format!("- [ ] task {i} {marker}\n"));
        }
        // Plus some no-priority tasks so the partial index
        // actually skips rows (proving its value).
        for i in 0..200 {
            body.push_str(&format!("- [ ] no-pri {i}\n"));
        }
        p.write_file("bulk.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let conn = session.conn.lock().unwrap();
    conn.execute("ANALYZE", []).unwrap();

    // Replicates the WHERE + ORDER BY shape `tasks_db::tasks_in_vault`
    // produces for a `priority_at_least` filter. The planner
    // picks the index based on WHERE + cardinality; LIMIT is
    // included for parity with the real path.
    let plan = explain_plan_rows(
        &conn,
        "SELECT f.path, t.ordinal
         FROM tasks t
         JOIN files f ON f.id = t.file_id
         WHERE t.priority IS NOT NULL AND t.priority >= ?
         ORDER BY IFNULL(t.due_ms, ?) ASC,
                  IFNULL(t.priority, ?) DESC,
                  f.path COLLATE BINARY ASC,
                  t.ordinal ASC
         LIMIT ?",
        rusqlite::params![1i64, i64::MAX, i32::MIN as i64, 200i64],
    );

    // Audit #284: parse rows from `EXPLAIN QUERY PLAN` structurally
    // (one detail string per planner step) rather than concatenating
    // them all into one blob and substring-matching. The index name
    // is the most stable part of the planner's output across SQLite
    // versions — phrasing around it ("USING INDEX" → "USING COVERING
    // INDEX") can drift but the name itself doesn't. Companion
    // tests verify the index exists in `sqlite_master` independently
    // (`idx_tasks_priority_exists_after_migration_009`).
    assert!(
        plan.iter()
            .any(|row| row.detail.contains("idx_tasks_priority")),
        "priority filter should use idx_tasks_priority; plan:\n{}",
        format_plan(&plan),
    );
    // Defensive check: regardless of how SQLite phrases the
    // optimizer output in future versions, no line should
    // start with a bare full-table SCAN of `t`. The plan may
    // include "USE TEMP B-TREE FOR ORDER BY" — that's the
    // sort step, separately tracked as red-team M2.
    for row in &plan {
        let trimmed = row.detail.trim_start();
        assert!(
            !(trimmed.starts_with("SCAN t ") || trimmed == "SCAN t"),
            "expected no full SCAN of tasks; row {}: {:?}\nfull plan:\n{}",
            row.id,
            row.detail,
            format_plan(&plan),
        );
    }
}

#[test]
fn idx_tasks_priority_exists_after_migration_009() {
    // Belt-and-braces: even if a future planner change masks the
    // EXPLAIN-based test above, the index itself must remain in
    // `sqlite_master`. Migration 009 is append-only / forward-
    // only per the project's migration policy.
    let (_tmp, session) = make_vault(|_| {});
    let conn = session.conn.lock().unwrap();
    let exists: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM sqlite_master
             WHERE type = 'index' AND name = 'idx_tasks_priority'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(
        exists, 1,
        "idx_tasks_priority must exist after migration 009"
    );
}

#[test]
fn idx_tasks_sort_exists_after_migration_010() {
    // Companion to `idx_tasks_priority_exists_after_migration_009`:
    // the expression index must remain in sqlite_master so future
    // planner changes can't silently mask a missing index.
    let (_tmp, session) = make_vault(|_| {});
    let conn = session.conn.lock().unwrap();
    let exists: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM sqlite_master
             WHERE type = 'index' AND name = 'idx_tasks_sort'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(exists, 1, "idx_tasks_sort must exist after migration 010");
}

#[test]
fn unfiltered_tasks_in_vault_uses_idx_tasks_sort_not_full_temp_btree() {
    // Regression for #145 (red-team M2). Before migration 010 the
    // unfiltered `tasks_in_vault` plan ended in
    // `USE TEMP B-TREE FOR ORDER BY` — materialise every row,
    // sort in a temp btree, apply LIMIT. With the expression
    // index, the planner walks the index in (due, priority)
    // order and only needs `USE TEMP B-TREE FOR RIGHT PART OF
    // ORDER BY` to sort within (due, priority) tie-groups by
    // path. That residual sort is small and bounded by tie-group
    // size, not total matching rows.
    //
    // Populate ~500 prioritised tasks + ANALYZE so the planner
    // has real stats; otherwise the empty-table defaults can
    // mask the plan choice.
    let (_tmp, session) = make_vault(|p| {
        let mut body = String::with_capacity(20_000);
        for i in 0..500 {
            let marker = match i % 4 {
                0 => "⏫",
                1 => "🔼",
                2 => "🔽",
                _ => "⏬",
            };
            body.push_str(&format!(
                "- [ ] task {i} {marker} 📅 2026-06-{:02}\n",
                (i % 28) + 1
            ));
        }
        p.write_file("bulk.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let conn = session.conn.lock().unwrap();
    conn.execute("ANALYZE", []).unwrap();

    // Replicates the ORDER BY shape `tasks_db::tasks_in_vault`
    // produces, including the literal IFNULL sentinels that
    // match the expression-index definition.
    let plan = explain_plan_rows(
        &conn,
        "SELECT f.path, t.ordinal
         FROM tasks t JOIN files f ON f.id = t.file_id
         ORDER BY IFNULL(t.due_ms, 9223372036854775807) ASC,
                  IFNULL(t.priority, -2147483648) DESC,
                  f.path COLLATE BINARY ASC,
                  t.ordinal ASC
         LIMIT ?",
        rusqlite::params![200i64],
    );

    // Audit #284: same structural-parse approach as
    // `priority_at_least_filter_uses_idx_tasks_priority_not_table_scan`.
    // Companion `idx_tasks_sort_exists_after_migration_010` test
    // verifies the index's existence independent of EXPLAIN output.
    assert!(
        plan.iter().any(|row| row.detail.contains("idx_tasks_sort")),
        "ORDER BY should use idx_tasks_sort; plan:\n{}",
        format_plan(&plan),
    );
    // The FULL temp-btree variant (no qualifier) means the
    // entire ORDER BY had to be sorted from scratch — that's
    // the bug. The "RIGHT PART OF ORDER BY" variant is
    // acceptable: the index satisfies the leading tiers and
    // only the trailing path tiebreak sorts in a temp btree
    // within tie-groups.
    for row in &plan {
        let trimmed = row.detail.trim_start();
        assert!(
            trimmed != "USE TEMP B-TREE FOR ORDER BY",
            "expected idx-driven sort, got full temp btree; plan:\n{}",
            format_plan(&plan),
        );
    }
}

#[test]
fn tasks_in_vault_priority_at_least_filter() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "a.md",
            "- [ ] no pri\n- [ ] low 🔽\n- [ ] high 🔼\n- [ ] highest ⏫\n".as_bytes(),
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let page = session
        .tasks_in_vault(
            crate::TaskFilter {
                priority_at_least: Some(1),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    let texts: Vec<&str> = page.items.iter().map(|r| r.task.text.as_str()).collect();
    assert_eq!(texts, vec!["highest", "high"]);
}

#[test]
fn toggle_task_status_changes_only_the_status_character() {
    let body = "- [ ] task one\n- [ ] task two 📅 2026-06-01 ⏫\n";
    let (tmp, session) = make_vault(|p| {
        p.write_file("n.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session
        .toggle_task_status("n.md", 1, 'x', None)
        .expect("toggle succeeds");

    // Re-read the file from disk to confirm everything outside
    // the bracket is preserved byte-for-byte.
    let on_disk = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
    assert_eq!(on_disk, "- [ ] task one\n- [x] task two 📅 2026-06-01 ⏫\n");

    // Index reflects the new state, including completed=true.
    let tasks = session.tasks_for_file("n.md").unwrap();
    assert_eq!(tasks[1].status_char, 'x');
    assert!(tasks[1].completed);
    // Metadata still on the task.
    assert!(tasks[1].due_ms.is_some());
    assert_eq!(tasks[1].priority, Some(2));
}

#[test]
fn toggle_task_status_preserves_indentation_for_nested_tasks() {
    let body = "- [ ] parent\n  - [ ] child\n    - [ ] grandchild\n";
    let (tmp, session) = make_vault(|p| {
        p.write_file("n.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session
        .toggle_task_status("n.md", 2, 'x', None)
        .expect("toggle grandchild");
    let on_disk = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
    assert_eq!(
        on_disk,
        "- [ ] parent\n  - [ ] child\n    - [x] grandchild\n"
    );
}

#[test]
fn toggle_task_status_supports_custom_status_chars() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"- [ ] thing\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session
        .toggle_task_status("n.md", 0, '/', None)
        .expect("toggle to in-progress");
    let on_disk = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
    assert_eq!(on_disk, "- [/] thing\n");
}

#[test]
fn toggle_task_status_returns_invalid_argument_for_out_of_range_ordinal() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"- [ ] only one task\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let result = session.toggle_task_status("n.md", 99, 'x', None);
    match result {
        Err(VaultError::InvalidArgument { message }) => {
            assert!(message.contains("ordinal 99"), "got: {message}");
        }
        other => panic!("expected InvalidArgument, got {other:?}"),
    }
    // File untouched.
    let on_disk = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
    assert_eq!(on_disk, "- [ ] only one task\n");
}

#[test]
fn toggle_task_status_returns_write_conflict_when_hash_stale() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"- [ ] thing\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    // Pretend the editor read at a state that no longer exists.
    let stale_hash = crate::content_hash(b"something else entirely\n");
    let result = session.toggle_task_status("n.md", 0, 'x', Some(&stale_hash));
    match result {
        Err(VaultError::WriteConflict { .. }) => {}
        other => panic!("expected WriteConflict, got {other:?}"),
    }
    // File untouched.
    let on_disk = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
    assert_eq!(on_disk, "- [ ] thing\n");
}

#[test]
fn toggle_task_status_appends_oplog_entry() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"- [ ] thing\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(session.read_oplog("n.md").unwrap().is_empty());

    session.toggle_task_status("n.md", 0, 'x', None).unwrap();

    let entries = session.read_oplog("n.md").unwrap();
    assert_eq!(
        entries.len(),
        1,
        "toggle should append exactly one op-log entry"
    );
    assert_eq!(entries[0].op_kind, crate::OpKind::WholeFileReplace);
}

#[test]
fn toggle_task_status_serializes_with_concurrent_save() {
    // Concurrent toggle + editor save on the same file must
    // serialize through the session mutex so the op-log records
    // exactly two entries in well-defined order. The actual
    // outcome (which save lands first) is racy; we assert
    // invariants that hold either way.
    use std::sync::Arc as StdArc;
    use std::thread;

    let (_tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"- [ ] thing\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let session = StdArc::new(session);

    let s1 = StdArc::clone(&session);
    let s2 = StdArc::clone(&session);
    let t1 = thread::spawn(move || s1.toggle_task_status("n.md", 0, 'x', None));
    let t2 = thread::spawn(move || s2.save_text("n.md", "- [ ] thing edited\n", None));
    t1.join().unwrap().expect("toggle ok");
    t2.join().unwrap().expect("save ok");

    let entries = session.read_oplog("n.md").unwrap();
    assert_eq!(entries.len(), 2, "both saves should land an op-log entry");
    // Hashes should chain — second entry's content_hash_before
    // equals first's content_hash_after.
    assert_eq!(
        entries[1].content_hash_before, entries[0].content_hash_after,
        "op-log entries should chain through the mutex"
    );
}

#[test]
fn toggle_task_status_does_not_lose_concurrent_save() {
    // Regression for #135 (red-team finding C1). The previous
    // shape of `toggle_task_status` read the file outside the
    // session mutex, parsed, then called `save_text` which
    // acquired the mutex inside its own body. A concurrent
    // `save_text(..., None)` between toggle's read and toggle's
    // save would land first; toggle then wrote a rebuilt version
    // of the PRE-save contents, silently overwriting the editor's
    // edit.
    //
    // The earlier `toggle_task_status_serializes_with_concurrent_save`
    // test caught hash-chain ordering through the op-log but did
    // not assert the *on-disk content* of both ops survived —
    // so the bug passed CI for an entire PR cycle. This test
    // closes that gap: after both ops complete the file must
    // contain `appended` (the save's distinctive payload),
    // regardless of which op landed first.
    //
    // Run many trials with a barrier to maximize race likelihood —
    // the red-team probe saw 50/50 corruption under exactly this
    // shape.
    use std::sync::{Arc as StdArc, Barrier};
    use std::thread;

    for trial in 0..20 {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("n.md", b"- [ ] thing\n").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let session = StdArc::new(session);
        let barrier = StdArc::new(Barrier::new(2));

        let s1 = StdArc::clone(&session);
        let b1 = StdArc::clone(&barrier);
        let s2 = StdArc::clone(&session);
        let b2 = StdArc::clone(&barrier);

        let t1 = thread::spawn(move || {
            b1.wait();
            s1.toggle_task_status("n.md", 0, 'x', None)
        });
        let t2 = thread::spawn(move || {
            b2.wait();
            s2.save_text("n.md", "- [ ] thing\nappended\n", None)
        });
        t1.join().unwrap().expect("toggle ok");
        t2.join().unwrap().expect("save ok");

        let on_disk = session.read_text("n.md").unwrap();
        // Acceptable final states (both ops survived):
        //   - save-then-toggle: "- [x] thing\nappended\n"
        //   - toggle-then-save: "- [ ] thing\nappended\n"
        // Unacceptable (lost-update bug):
        //   - "- [x] thing\n"  (toggle's stale read clobbered save)
        assert!(
            on_disk.contains("appended"),
            "trial {trial}: save's `appended` line was lost — final on-disk: {on_disk:?}",
        );
    }
}

#[test]
fn tasks_table_purged_when_file_exceeds_large_file_threshold() {
    // A file that grows past the large-file refuse threshold gets
    // its derivative rows purged. The tasks table must follow the
    // same discipline as headings / links / properties, otherwise
    // stale task rows would keep showing up in the panel for a
    // file the scanner no longer indexes.
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("notes/n.md", b"- [ ] task\n- [ ] other\n")
        .unwrap();

    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    // Tiny threshold so the second write trips it.
    config.large_file_refuse_bytes = 50;
    let session = VaultSession::open(
        Arc::new(FsVaultProvider::new(tmp.path().to_path_buf())),
        config,
    )
    .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(!session.tasks_for_file("notes/n.md").unwrap().is_empty());

    // Grow the file past the refuse threshold.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("notes/n.md", vec![b'a'; 200].as_slice())
        .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        session.tasks_for_file("notes/n.md").unwrap().is_empty(),
        "large-file purge must drop task rows"
    );
}
