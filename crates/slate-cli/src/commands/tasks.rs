// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate tasks <vault-path> [--filter …] [--include-completed]` (M-6, #537).
//!
//! Lists a vault's Markdown tasks, filtered by a due-date window. A thin
//! wrapper over [`VaultSession::tasks_in_vault`] — no business logic
//! lives here beyond translating the `--filter` choice into a
//! [`TaskFilter`] (m_spec §M-6 "No business logic in the CLI layer").
//!
//! **Date math (normative, m_spec §M-6):** windows are UTC calendar
//! days, matching both the storage convention (`due_ms` = midnight UTC
//! of the authored date, `tasks.rs:393-396`) and the Mac Tasks panel
//! (`startOfTodayUtc`), so the CLI returns the same sets the app shows.
//! The window is computed by the pure [`due_window`] fn against an
//! injected `now_epoch_ms`; the command passes `SystemTime::now()`.
//! `TaskFilter.due_to_ms` is **exclusive** (`due_ms < ?`,
//! `tasks_db.rs:135-138`), so `due-today`'s upper bound is
//! `today_utc + 86_400_000` and `overdue`'s bound is `today_utc` itself
//! (a task due *today* is not overdue — same as the app).
//!
//! `data` shape (the `slate.cli.v1` stability contract):
//! ```json
//! { "filter": String,
//!   "tasks": [{ "path": String, "file_name": String, "line": u32,
//!               "text": String, "status_char": String,
//!               "completed": bool, "due": "YYYY-MM-DD"|null,
//!               "priority": i32|null }] }
//! ```
//! `due` = the UTC calendar date of `due_ms` (the date as authored in
//! the note, never timezone-shifted — derived with chrono directly from
//! the millis, no `Local`).

use std::time::{SystemTime, UNIX_EPOCH};

use chrono::{DateTime, Utc};
use clap::ValueEnum;

use slate_core::session::{Page, Paging};
use slate_core::{TaskFilter, TaskWithLocation};

use crate::output::{CommandOutput, tsv_row};
use crate::session::{CliError, OpenedVault, map_vault_error, open_vault};

/// Milliseconds in a UTC calendar day.
const DAY_MS: i64 = 86_400_000;

/// Page size when draining `tasks_in_vault` to exhaustion. The CLI is
/// the one consumer allowed to drain the whole vault (bounded by vault
/// size); a large page keeps the roundtrip count low.
const DRAIN_PAGE: u32 = 1000;

/// The `--filter` choices. `all` (the default) applies no due-date
/// bound; the others map to the UTC-day windows in [`due_window`].
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default, ValueEnum)]
pub enum TaskFilterChoice {
    /// Tasks due on today's UTC calendar day.
    DueToday,
    /// Tasks whose due date is strictly before today (UTC); completed
    /// tasks are always excluded (an overdue-done task is nonsense).
    Overdue,
    /// Tasks due within today plus the next six UTC days.
    ThisWeek,
    /// Every task, no due-date bound. The default.
    #[default]
    All,
}

impl TaskFilterChoice {
    /// The wire string echoed into the json `filter` field and printed
    /// in the human header. Matches the `--filter` value spelling.
    fn slug(self) -> &'static str {
        match self {
            TaskFilterChoice::DueToday => "due-today",
            TaskFilterChoice::Overdue => "overdue",
            TaskFilterChoice::ThisWeek => "this-week",
            TaskFilterChoice::All => "all",
        }
    }
}

/// The half-open `[from, to)` due-date window for a filter, in epoch
/// millis, or `None` on either side for "no bound on this edge".
///
/// **Pure** — takes `now_epoch_ms` explicitly so tests can inject a
/// fixed reference date (the CLI passes the real `SystemTime` clock).
/// `today_utc = (now_epoch_ms / DAY_MS) * DAY_MS` — plain integer math,
/// no `chrono` clock feature (the workspace deliberately builds `chrono`
/// with `default-features = false`, lacking `Utc::now`). Euclidean
/// division floors toward negative infinity so a pre-1970 `now` still
/// lands on the correct UTC midnight (defensive; the CLI never sees
/// one).
///
/// The `to` bound is **exclusive** to match `TaskFilter.due_to_ms`
/// (`due_ms < ?`): `due-today` upper-bounds at `today_utc + DAY_MS`
/// (through end-of-day) and `overdue` upper-bounds at `today_utc` itself
/// (strictly before today).
pub fn due_window(filter: TaskFilterChoice, now_epoch_ms: i64) -> (Option<i64>, Option<i64>) {
    let today_utc = now_epoch_ms.div_euclid(DAY_MS) * DAY_MS;
    match filter {
        TaskFilterChoice::DueToday => (Some(today_utc), Some(today_utc + DAY_MS)),
        TaskFilterChoice::Overdue => (None, Some(today_utc)),
        TaskFilterChoice::ThisWeek => (Some(today_utc), Some(today_utc + 7 * DAY_MS)),
        TaskFilterChoice::All => (None, None),
    }
}

/// Build the [`TaskFilter`] for a run from the filter choice, the
/// injected `now`, and `--include-completed`.
///
/// `--include-completed` clears the default `completed: Some(false)`
/// restriction — **except** for `overdue`, which always excludes
/// completed tasks regardless of the flag (m_spec §M-6).
fn build_filter(
    choice: TaskFilterChoice,
    now_epoch_ms: i64,
    include_completed: bool,
) -> TaskFilter {
    let (due_from_ms, due_to_ms) = due_window(choice, now_epoch_ms);
    let completed = match choice {
        // Overdue pins completed=false unconditionally.
        TaskFilterChoice::Overdue => Some(false),
        _ if include_completed => None,
        _ => Some(false),
    };
    TaskFilter {
        completed,
        due_from_ms,
        due_to_ms,
        priority_at_least: None,
    }
}

/// Current wall-clock time as epoch millis. `SystemTime` is the only
/// clock the CLI touches (workspace `chrono` has no `Utc::now`); a
/// before-epoch clock (pathological) clamps to 0 rather than panicking.
fn now_epoch_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0)
}

/// Format a stored midnight-UTC `due_ms` as its `YYYY-MM-DD` calendar
/// date. Derived straight from the millis in UTC — never timezone-
/// shifted (the workspace `chrono` build has no `Local`), so the printed
/// date is exactly the date the author wrote. `None` maps to json
/// `null`.
fn due_date_string(due_ms: Option<i64>) -> Option<String> {
    let ms = due_ms?;
    let dt: DateTime<Utc> = DateTime::<Utc>::from_timestamp_millis(ms)?;
    Some(dt.format("%Y-%m-%d").to_string())
}

/// Drain a paged task query to exhaustion, checking `cancel` between
/// pages so a Ctrl-C during a large-vault drain aborts to exit 130
/// promptly instead of paging on.
///
/// `fetch` is called with each successive [`Paging`] and returns one
/// page (the `run` path threads `session.tasks_in_vault` through it).
/// `tasks_in_vault` itself takes no cancel token — each page is a bounded
/// SQLite query, not a long scan — so cancellation is honored here in the
/// CLI layer with no new core plumbing (Codex adversarial-review, M-6).
/// The token is checked before each fetch and once more after the last
/// page, so an interrupt during the final fetch still exits 130 rather
/// than committing to a "success" result.
fn drain_tasks<F>(
    cancel: &slate_core::session::CancelToken,
    mut fetch: F,
) -> Result<Vec<TaskWithLocation>, CliError>
where
    F: FnMut(Paging) -> Result<Page<TaskWithLocation>, CliError>,
{
    let mut tasks: Vec<TaskWithLocation> = Vec::new();
    let mut paging = Paging::first(DRAIN_PAGE);
    loop {
        if cancel.is_cancelled() {
            return Err(CliError::Cancelled);
        }
        let page = fetch(paging)?;
        tasks.extend(page.items);
        match page.next_cursor {
            Some(cursor) => paging = Paging::after(cursor, DRAIN_PAGE),
            None => break,
        }
    }
    if cancel.is_cancelled() {
        return Err(CliError::Cancelled);
    }
    Ok(tasks)
}

/// Run `slate tasks`. Opens + scans the vault, drains the filtered task
/// query to exhaustion, and renders the result. Returns the absolute
/// vault path (for the json envelope) plus the rendered output.
pub fn run(
    raw_path: &std::path::Path,
    choice: TaskFilterChoice,
    include_completed: bool,
    cancel: &slate_core::session::CancelToken,
) -> Result<(String, CommandOutput), CliError> {
    let OpenedVault {
        session, abs_path, ..
    } = open_vault(raw_path)?;

    // Build the index before querying — tasks live in the cache the
    // scan populates. Same throttled progress + shared cancel token as
    // `open`.
    session
        .scan_initial_with_progress(cancel, Some(crate::progress::StderrProgress::listener()))
        .map_err(map_vault_error)?;

    let filter = build_filter(choice, now_epoch_ms(), include_completed);

    // Drain every page of the filtered query, checking the shared cancel
    // token between pages (see [`drain_tasks`]).
    let tasks = drain_tasks(cancel, |paging| {
        session
            .tasks_in_vault(filter.clone(), paging)
            .map_err(map_vault_error)
    })?;

    let data = build_data(choice, &tasks);
    let human = render_human(&tasks);
    let tsv = render_tsv(&tasks);

    Ok((
        abs_path,
        CommandOutput {
            data,
            human,
            tsv,
            human_verbatim: false,
        },
    ))
}

/// Assemble the json `data` object (the stability contract).
fn build_data(choice: TaskFilterChoice, tasks: &[TaskWithLocation]) -> serde_json::Value {
    let rows: Vec<serde_json::Value> = tasks
        .iter()
        .map(|t| {
            serde_json::json!({
                "path": t.path,
                "file_name": t.file_name,
                "line": t.task.line,
                "text": t.task.text,
                "status_char": t.task.status_char.to_string(),
                "completed": t.task.completed,
                "due": due_date_string(t.task.due_ms),
                "priority": t.task.priority,
            })
        })
        .collect();
    serde_json::json!({
        "filter": choice.slug(),
        "tasks": rows,
    })
}

/// Human format (m_spec §M-6):
/// `[<status_char>] path:line — text (due YYYY-MM-DD)`, one per line,
/// with the actual status char inside the brackets and the `(due …)`
/// suffix present only when the task carries a due date.
fn render_human(tasks: &[TaskWithLocation]) -> String {
    if tasks.is_empty() {
        return "No tasks.".to_string();
    }
    tasks
        .iter()
        .map(|t| {
            let due = due_date_string(t.task.due_ms)
                .map(|d| format!(" (due {d})"))
                .unwrap_or_default();
            format!(
                "[{status}] {path}:{line} — {text}{due}",
                status = t.task.status_char,
                path = t.path,
                line = t.task.line,
                text = t.task.text,
            )
        })
        .collect::<Vec<_>>()
        .join("\n")
}

/// TSV format (m_spec §M-6): header `path line status due text`, one row
/// per task. `due` is the `YYYY-MM-DD` date or empty; `status` is the
/// bare status char.
fn render_tsv(tasks: &[TaskWithLocation]) -> String {
    let mut rows = vec![tsv_row(["path", "line", "status", "due", "text"])];
    for t in tasks {
        let due = due_date_string(t.task.due_ms).unwrap_or_default();
        rows.push(tsv_row([
            t.path.as_str(),
            &t.task.line.to_string(),
            &t.task.status_char.to_string(),
            &due,
            t.task.text.as_str(),
        ]));
    }
    rows.join("\n")
}

#[cfg(test)]
mod tests {
    use super::*;

    /// A fixed reference instant: 2026-06-15T12:34:56Z. All the pure
    /// window assertions anchor to this so the math is deterministic and
    /// independent of the wall clock.
    const REF_MS: i64 = {
        // 2026-06-15 is 20_619 days after the epoch; noon+ within the
        // day proves the intra-day time is floored away.
        20_619 * DAY_MS + 12 * 3_600_000 + 34 * 60_000 + 56_000
    };
    /// Midnight UTC of 2026-06-15 — the `today_utc` the window math must
    /// floor `REF_MS` down to.
    const TODAY_UTC: i64 = 20_619 * DAY_MS;

    #[test]
    fn today_utc_floors_intraday_time() {
        // due-today's `from` bound is exactly today_utc — proves the
        // integer floor drops the 12:34:56 into midnight.
        let (from, _to) = due_window(TaskFilterChoice::DueToday, REF_MS);
        assert_eq!(from, Some(TODAY_UTC));
    }

    #[test]
    fn due_today_window_is_one_exclusive_day() {
        let (from, to) = due_window(TaskFilterChoice::DueToday, REF_MS);
        assert_eq!(from, Some(TODAY_UTC));
        // Exclusive upper bound: through end-of-day, not into tomorrow.
        assert_eq!(to, Some(TODAY_UTC + DAY_MS));
    }

    #[test]
    fn overdue_upper_bound_is_today_utc_exclusive() {
        // Overdue is everything strictly before today: no lower bound,
        // upper bound = today_utc itself (a task due *today* is excluded).
        let (from, to) = due_window(TaskFilterChoice::Overdue, REF_MS);
        assert_eq!(from, None);
        assert_eq!(to, Some(TODAY_UTC));
    }

    #[test]
    fn this_week_window_is_seven_exclusive_days() {
        let (from, to) = due_window(TaskFilterChoice::ThisWeek, REF_MS);
        assert_eq!(from, Some(TODAY_UTC));
        assert_eq!(to, Some(TODAY_UTC + 7 * DAY_MS));
    }

    #[test]
    fn all_has_no_bounds() {
        assert_eq!(due_window(TaskFilterChoice::All, REF_MS), (None, None));
    }

    #[test]
    fn window_is_stable_across_any_intraday_time() {
        // Every instant within the same UTC day yields the same window.
        let start = TODAY_UTC;
        let end = TODAY_UTC + DAY_MS - 1;
        assert_eq!(
            due_window(TaskFilterChoice::DueToday, start),
            due_window(TaskFilterChoice::DueToday, end),
        );
        // One millisecond into the next day shifts the whole window.
        let (next_from, _) = due_window(TaskFilterChoice::DueToday, TODAY_UTC + DAY_MS);
        assert_eq!(next_from, Some(TODAY_UTC + DAY_MS));
    }

    // --- build_filter completed-axis semantics -----------------------

    #[test]
    fn include_completed_clears_completed_filter_except_overdue() {
        // Default (flag off): completed=false on every filter.
        for choice in [
            TaskFilterChoice::DueToday,
            TaskFilterChoice::ThisWeek,
            TaskFilterChoice::All,
            TaskFilterChoice::Overdue,
        ] {
            assert_eq!(
                build_filter(choice, REF_MS, false).completed,
                Some(false),
                "flag off should exclude completed for {choice:?}"
            );
        }

        // Flag on: cleared for the windowed/all filters…
        for choice in [
            TaskFilterChoice::DueToday,
            TaskFilterChoice::ThisWeek,
            TaskFilterChoice::All,
        ] {
            assert_eq!(
                build_filter(choice, REF_MS, true).completed,
                None,
                "flag on should include completed for {choice:?}"
            );
        }
        // …but overdue still pins completed=false even with the flag on.
        assert_eq!(
            build_filter(TaskFilterChoice::Overdue, REF_MS, true).completed,
            Some(false),
            "overdue must exclude completed regardless of --include-completed"
        );
    }

    #[test]
    fn build_filter_wires_the_due_window_into_the_task_filter() {
        let f = build_filter(TaskFilterChoice::DueToday, REF_MS, false);
        assert_eq!(f.due_from_ms, Some(TODAY_UTC));
        assert_eq!(f.due_to_ms, Some(TODAY_UTC + DAY_MS));
        assert_eq!(f.priority_at_least, None);
    }

    #[test]
    fn due_date_string_renders_authored_utc_date() {
        // Midnight UTC of 2026-06-15 → the authored calendar date, no
        // timezone shift.
        assert_eq!(due_date_string(Some(TODAY_UTC)), Some("2026-06-15".into()));
        // One millisecond before midnight is still the previous day.
        assert_eq!(
            due_date_string(Some(TODAY_UTC - 1)),
            Some("2026-06-14".into())
        );
        assert_eq!(due_date_string(None), None);
    }

    // --- drain cancellation ------------------------------------------

    use slate_core::session::CancelToken;

    /// An empty single-page result (no more pages).
    fn last_page() -> Page<TaskWithLocation> {
        Page {
            items: Vec::new(),
            next_cursor: None,
            total_filtered: 0,
        }
    }

    #[test]
    fn drain_tasks_aborts_before_any_fetch_when_pre_cancelled() {
        // A token cancelled before the drain starts must abort at the
        // first check — the fetch closure is never called.
        let cancel = CancelToken::new();
        cancel.cancel();
        let mut fetched = false;
        let result = drain_tasks(&cancel, |_paging| {
            fetched = true;
            Ok(last_page())
        });
        assert!(matches!(result, Err(CliError::Cancelled)));
        assert!(!fetched, "no page should be fetched after cancellation");
    }

    #[test]
    fn drain_tasks_aborts_between_pages_on_cancellation() {
        // Cancel after the first page is fetched: the loop's next
        // top-of-iteration check must abort to Cancelled rather than
        // fetching page two.
        let cancel = CancelToken::new();
        let mut calls = 0u32;
        let result = drain_tasks(&cancel, |_paging| {
            calls += 1;
            // First page reports more to come; then we cancel.
            cancel.cancel();
            Ok(Page {
                items: Vec::new(),
                next_cursor: Some("next".to_string()),
                total_filtered: 0,
            })
        });
        assert!(matches!(result, Err(CliError::Cancelled)));
        assert_eq!(calls, 1, "must not fetch a second page after cancel");
    }

    #[test]
    fn drain_tasks_completes_when_not_cancelled() {
        // Uninterrupted drain of two pages returns every item.
        let cancel = CancelToken::new();
        let mut calls = 0u32;
        let result = drain_tasks(&cancel, |_paging| {
            calls += 1;
            if calls == 1 {
                Ok(Page {
                    items: Vec::new(),
                    next_cursor: Some("next".to_string()),
                    total_filtered: 0,
                })
            } else {
                Ok(last_page())
            }
        });
        assert!(result.is_ok());
        assert_eq!(calls, 2, "both pages drained");
    }
}
