// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Integration tests for `slate tasks` + `slate render-template`
//! (M-6, #537).
//!
//! Drives the built binary via `assert_cmd` against `tempfile` fixture
//! vaults, covering the m_spec §M-6 test list:
//! - tasks fixture with due dates planted **relative to today's UTC
//!   day** (the binary can't take an injected clock — the fixed-
//!   reference window math is unit-tested in `commands::tasks::tests`);
//! - filters × formats matrix; `--include-completed`; the
//!   overdue-excludes-completed invariant pinned;
//! - a two-prompt template: supplying one → stderr warning names the
//!   other, exit 0, the marker stays literal in stdout; `--strict` →
//!   exit 1 with no stdout; both supplied → clean render; `--format tsv`
//!   → exit 2; json carries `unfilled_prompts`.
//!
//! SIGINT mid-run inherits the M-4 handler installed in `main` before
//! any session open — the `tasks` path opens + scans exactly like
//! `open`, so the M-4 `sigint_during_open_*` test already exercises the
//! shared handler (asserted by code structure, per the spec's "no
//! per-command work needed").

use std::fs;
use std::time::{SystemTime, UNIX_EPOCH};

use assert_cmd::Command;
use predicates::prelude::*;
use serde_json::Value;
use tempfile::TempDir;

/// Milliseconds in a UTC calendar day (mirrors the CLI's `DAY_MS`).
const DAY_MS: i64 = 86_400_000;

/// The binary under test.
fn slate() -> Command {
    Command::cargo_bin("slate").expect("slate binary builds")
}

/// Today's UTC midnight in epoch millis, computed the same way the CLI
/// does (integer floor of `SystemTime::now`), so fixtures land in the
/// window the binary will compute at the same instant.
fn today_utc_ms() -> i64 {
    let now = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .expect("clock after epoch")
        .as_millis() as i64;
    (now / DAY_MS) * DAY_MS
}

/// Format an epoch-millis midnight as `YYYY-MM-DD` for authoring a task
/// line. Uses a tiny civil-date conversion so the test doesn't need
/// chrono's (absent) clock feature.
fn ymd(epoch_ms: i64) -> String {
    // Days since 1970-01-01 (all our fixtures are >= 0).
    let days = epoch_ms.div_euclid(DAY_MS);
    let (y, m, d) = civil_from_days(days);
    format!("{y:04}-{m:02}-{d:02}")
}

/// Howard Hinnant's `civil_from_days` — epoch-day → (year, month, day),
/// proleptic Gregorian. Public-domain algorithm; avoids a chrono clock
/// dependency in the test.
fn civil_from_days(z: i64) -> (i64, u32, u32) {
    let z = z + 719_468;
    let era = if z >= 0 { z } else { z - 146_096 } / 146_097;
    let doe = z - era * 146_097; // [0, 146096]
    let yoe = (doe - doe / 1460 + doe / 36524 - doe / 146_096) / 365; // [0, 399]
    let y = yoe + era * 400;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100); // [0, 365]
    let mp = (5 * doy + 2) / 153; // [0, 11]
    let d = (doy - (153 * mp + 2) / 5 + 1) as u32; // [1, 31]
    let m = (if mp < 10 { mp + 3 } else { mp - 9 }) as u32; // [1, 12]
    let y = if m <= 2 { y + 1 } else { y };
    (y, m, d)
}

/// Seed a vault whose tasks span the due-date windows relative to today:
/// one overdue (yesterday), one due today, one due in three days
/// (this-week but not today), one due next week (outside this-week), one
/// with no due date, plus a completed task due today (for the
/// include-completed / overdue-excludes-completed assertions).
fn seed_tasks_vault() -> TempDir {
    let dir = TempDir::new().expect("tempdir");
    let root = dir.path();
    let today = today_utc_ms();
    let yesterday = ymd(today - DAY_MS);
    let today_s = ymd(today);
    let in_three = ymd(today + 3 * DAY_MS);
    let next_week = ymd(today + 8 * DAY_MS);

    let body = format!(
        "# Tasks\n\n\
         - [ ] overdue task 📅 {yesterday}\n\
         - [ ] today task 📅 {today_s}\n\
         - [ ] soon task 📅 {in_three}\n\
         - [ ] later task 📅 {next_week}\n\
         - [ ] no due task\n\
         - [x] done today task 📅 {today_s}\n"
    );
    fs::write(root.join("tasks.md"), body).unwrap();
    dir
}

/// Run `slate tasks` with the given flags and return the parsed json
/// `data`. Asserts success + envelope invariants along the way.
fn tasks_json(vault: &std::path::Path, extra: &[&str]) -> Value {
    let mut cmd = slate();
    cmd.arg("tasks").arg(vault);
    for a in extra {
        cmd.arg(a);
    }
    cmd.arg("--format").arg("json");
    let out = cmd.assert().success().get_output().stdout.clone();
    let text = std::str::from_utf8(&out).expect("utf8 stdout");
    let v: Value = serde_json::from_str(text).expect("stdout is valid JSON");
    assert_eq!(v["schema"], "slate.cli.v1");
    assert_eq!(v["command"], "tasks");
    v["data"].clone()
}

/// Collect the `text` field of every task row in a `data` payload.
fn task_texts(data: &Value) -> Vec<String> {
    data["tasks"]
        .as_array()
        .unwrap()
        .iter()
        .map(|t| t["text"].as_str().unwrap().to_string())
        .collect()
}

// --- `tasks` filters --------------------------------------------------

#[test]
fn tasks_all_lists_every_open_task_by_default() {
    let vault = seed_tasks_vault();
    let data = tasks_json(vault.path(), &["--filter", "all"]);
    assert_eq!(data["filter"], "all");
    let texts = task_texts(&data);
    // Default excludes completed: five open tasks, no "done today task".
    assert!(texts.contains(&"overdue task".to_string()));
    assert!(texts.contains(&"today task".to_string()));
    assert!(texts.contains(&"soon task".to_string()));
    assert!(texts.contains(&"later task".to_string()));
    assert!(texts.contains(&"no due task".to_string()));
    assert!(
        !texts.contains(&"done today task".to_string()),
        "completed excluded by default: {texts:?}"
    );
}

#[test]
fn tasks_all_is_the_default_filter() {
    let vault = seed_tasks_vault();
    // No --filter at all → same as --filter all.
    let data = tasks_json(vault.path(), &[]);
    assert_eq!(data["filter"], "all");
}

#[test]
fn tasks_due_today_window_is_exactly_today() {
    let vault = seed_tasks_vault();
    let data = tasks_json(vault.path(), &["--filter", "due-today"]);
    assert_eq!(data["filter"], "due-today");
    let texts = task_texts(&data);
    assert_eq!(
        texts,
        vec!["today task".to_string()],
        "only the task due today (open) is in the due-today window"
    );
    // The due field is the authored UTC date.
    let due = data["tasks"][0]["due"].as_str().unwrap();
    assert_eq!(due, ymd(today_utc_ms()));
}

#[test]
fn tasks_overdue_excludes_today_and_future() {
    let vault = seed_tasks_vault();
    let data = tasks_json(vault.path(), &["--filter", "overdue"]);
    let texts = task_texts(&data);
    // Yesterday is overdue; today is NOT (exclusive bound).
    assert_eq!(texts, vec!["overdue task".to_string()]);
}

#[test]
fn tasks_this_week_spans_today_through_six_days_out() {
    let vault = seed_tasks_vault();
    let data = tasks_json(vault.path(), &["--filter", "this-week"]);
    let mut texts = task_texts(&data);
    texts.sort();
    // Today + three-days-out are in; next-week (8 days) and overdue are
    // not; no-due has no due date so it's out of a windowed filter.
    assert_eq!(
        texts,
        vec!["soon task".to_string(), "today task".to_string()]
    );
}

// --- `--include-completed` -------------------------------------------

#[test]
fn tasks_include_completed_adds_done_tasks() {
    let vault = seed_tasks_vault();
    // due-today WITHOUT the flag: only the open task.
    let without = tasks_json(vault.path(), &["--filter", "due-today"]);
    assert_eq!(task_texts(&without), vec!["today task".to_string()]);

    // due-today WITH the flag: the completed task due today appears too.
    let with = tasks_json(
        vault.path(),
        &["--filter", "due-today", "--include-completed"],
    );
    let mut texts = task_texts(&with);
    texts.sort();
    assert_eq!(
        texts,
        vec!["done today task".to_string(), "today task".to_string()]
    );
}

#[test]
fn tasks_overdue_excludes_completed_even_with_flag() {
    let vault = seed_tasks_vault();
    // Author an overdue completed task to prove it's suppressed.
    let yesterday = ymd(today_utc_ms() - DAY_MS);
    fs::write(
        vault.path().join("more.md"),
        format!("- [x] overdue done 📅 {yesterday}\n"),
    )
    .unwrap();

    let data = tasks_json(
        vault.path(),
        &["--filter", "overdue", "--include-completed"],
    );
    let texts = task_texts(&data);
    // Even with --include-completed, overdue pins completed=false: the
    // open overdue task is present, the completed one is not.
    assert!(texts.contains(&"overdue task".to_string()));
    assert!(
        !texts.contains(&"overdue done".to_string()),
        "overdue must exclude completed regardless of the flag: {texts:?}"
    );
}

// --- formats round-trip ----------------------------------------------

#[test]
fn tasks_human_format_renders_status_path_and_due() {
    let vault = seed_tasks_vault();
    let today = ymd(today_utc_ms());
    slate()
        .arg("tasks")
        .arg(vault.path())
        .arg("--filter")
        .arg("due-today")
        .assert()
        .success()
        // `[ ] tasks.md:LINE — today task (due YYYY-MM-DD)`
        .stdout(predicate::str::contains("[ ] tasks.md:"))
        .stdout(predicate::str::contains("— today task"))
        .stdout(predicate::str::contains(format!("(due {today})")));
}

#[test]
fn tasks_tsv_format_has_header_and_columns() {
    let vault = seed_tasks_vault();
    let today = ymd(today_utc_ms());
    slate()
        .arg("tasks")
        .arg(vault.path())
        .arg("--filter")
        .arg("due-today")
        .arg("--format")
        .arg("tsv")
        .assert()
        .success()
        .stdout(predicate::str::contains("path\tline\tstatus\tdue\ttext"))
        .stdout(predicate::str::contains(format!("\t{today}\ttoday task")));
}

#[test]
fn tasks_json_rows_carry_the_contract_fields() {
    let vault = seed_tasks_vault();
    let data = tasks_json(vault.path(), &["--filter", "due-today"]);
    let row = &data["tasks"][0];
    assert!(row["path"].as_str().unwrap().ends_with("tasks.md"));
    assert!(row["file_name"].as_str().unwrap().ends_with("tasks.md"));
    assert!(row["line"].is_u64());
    assert_eq!(row["text"], "today task");
    assert_eq!(row["status_char"], " ");
    assert_eq!(row["completed"], false);
    assert_eq!(row["due"], ymd(today_utc_ms()));
    // No priority marker on this task → null.
    assert!(row["priority"].is_null());
}

#[test]
fn tasks_no_due_date_renders_null_due() {
    let vault = seed_tasks_vault();
    let data = tasks_json(vault.path(), &["--filter", "all"]);
    let no_due = data["tasks"]
        .as_array()
        .unwrap()
        .iter()
        .find(|t| t["text"] == "no due task")
        .expect("no-due task present under `all`");
    assert!(no_due["due"].is_null(), "no due date → null");
}

// --- `render-template` -----------------------------------------------

/// Seed a vault with a template carrying two distinct prompts plus a
/// couple of always-substituted variables.
fn seed_template_vault() -> TempDir {
    let dir = TempDir::new().expect("tempdir");
    let root = dir.path();
    let templates = root.join("Templates");
    fs::create_dir_all(&templates).unwrap();
    fs::write(
        templates.join("Meeting.md"),
        "# {{title}} in {{vault}}\n\nTopic: {{prompt:Topic}}\nOwner: {{prompt:Owner}}\n",
    )
    .unwrap();
    dir
}

#[test]
fn render_template_warns_on_the_unfilled_prompt_and_exits_zero() {
    let vault = seed_template_vault();
    // Supply Topic (slug `topic`) but not Owner.
    slate()
        .arg("render-template")
        .arg(vault.path())
        .arg("Templates/Meeting.md")
        .arg("--prompt")
        .arg("topic=Q1 planning")
        .assert()
        .success() // exit 0 — the gap is visible in the output
        // Warning names the *missing* prompt's label, not the filled one.
        .stderr(predicate::str::contains(
            "slate: warning: unfilled prompt 'Owner'",
        ))
        .stderr(predicate::str::contains("'Topic'").not())
        // The filled prompt is substituted; the unfilled one stays literal.
        .stdout(predicate::str::contains("Topic: Q1 planning"))
        .stdout(predicate::str::contains("Owner: {{prompt:Owner}}"));
}

#[test]
fn render_template_strict_exits_one_with_no_stdout() {
    let vault = seed_template_vault();
    let out = slate()
        .arg("render-template")
        .arg(vault.path())
        .arg("Templates/Meeting.md")
        .arg("--prompt")
        .arg("topic=Q1 planning")
        .arg("--strict")
        .assert()
        .code(1)
        .stderr(predicate::str::contains(
            "slate: warning: unfilled prompt 'Owner'",
        ))
        .get_output()
        .clone();
    assert!(
        out.stdout.is_empty(),
        "--strict must emit no stdout before failing: {:?}",
        String::from_utf8_lossy(&out.stdout)
    );
}

#[test]
fn render_template_both_prompts_supplied_renders_cleanly() {
    let vault = seed_template_vault();
    slate()
        .arg("render-template")
        .arg(vault.path())
        .arg("Templates/Meeting.md")
        .arg("--prompt")
        .arg("topic=Q1 planning")
        .arg("--prompt")
        .arg("owner=Alice")
        .assert()
        .success()
        .stdout(predicate::str::contains("Topic: Q1 planning"))
        .stdout(predicate::str::contains("Owner: Alice"))
        // No literal markers survive a fully-filled render.
        .stdout(predicate::str::contains("{{prompt").not())
        // No warnings when nothing is unfilled.
        .stderr(predicate::str::contains("unfilled prompt").not());
}

#[test]
fn render_template_json_carries_body_and_unfilled_prompts() {
    let vault = seed_template_vault();
    let out = slate()
        .arg("render-template")
        .arg(vault.path())
        .arg("Templates/Meeting.md")
        .arg("--prompt")
        .arg("topic=Q1 planning")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let text = std::str::from_utf8(&out).unwrap();
    let v: Value = serde_json::from_str(text).expect("valid JSON");
    assert_eq!(v["command"], "render-template");
    let data = &v["data"];
    assert!(
        data["body"]
            .as_str()
            .unwrap()
            .contains("Topic: Q1 planning")
    );
    assert!(
        data["body"]
            .as_str()
            .unwrap()
            .contains("Owner: {{prompt:Owner}}")
    );
    // cursor_byte_offset is null (no {{cursor}} in this template).
    assert!(data["cursor_byte_offset"].is_null());
    // unfilled_prompts names the missing label.
    let unfilled = data["unfilled_prompts"].as_array().unwrap();
    assert_eq!(unfilled.len(), 1);
    assert_eq!(unfilled[0], "Owner");
}

#[test]
fn render_template_title_flag_overrides_stem() {
    let vault = seed_template_vault();
    slate()
        .arg("render-template")
        .arg(vault.path())
        .arg("Templates/Meeting.md")
        .arg("--prompt")
        .arg("topic=x")
        .arg("--prompt")
        .arg("owner=y")
        .arg("--title")
        .arg("Standup")
        .assert()
        .success()
        // {{title}} → the --title value, not the "Meeting" stem.
        .stdout(predicate::str::contains("# Standup in"));
}

#[test]
fn render_template_default_title_is_the_template_stem() {
    let vault = seed_template_vault();
    slate()
        .arg("render-template")
        .arg(vault.path())
        .arg("Templates/Meeting.md")
        .arg("--prompt")
        .arg("topic=x")
        .arg("--prompt")
        .arg("owner=y")
        .assert()
        .success()
        // No --title → {{title}} defaults to the file stem "Meeting".
        .stdout(predicate::str::contains("# Meeting in"));
}

#[test]
fn render_template_tsv_is_rejected_with_exit_two() {
    let vault = seed_template_vault();
    slate()
        .arg("render-template")
        .arg(vault.path())
        .arg("Templates/Meeting.md")
        .arg("--format")
        .arg("tsv")
        .assert()
        .code(2)
        .stderr(predicate::str::contains(
            "tsv not supported for render-template",
        ));
}

#[test]
fn render_template_prompt_value_may_contain_equals() {
    let vault = TempDir::new().unwrap();
    let templates = vault.path().join("Templates");
    fs::create_dir_all(&templates).unwrap();
    fs::write(templates.join("Expr.md"), "E: {{prompt:Expr}}\n").unwrap();
    slate()
        .arg("render-template")
        .arg(vault.path())
        .arg("Templates/Expr.md")
        .arg("--prompt")
        .arg("expr=a=b=c")
        .assert()
        .success()
        // Split at the FIRST '=', so the value keeps its own '='.
        .stdout(predicate::str::contains("E: a=b=c"));
}

#[test]
fn render_template_prompt_without_equals_is_usage_error() {
    let vault = seed_template_vault();
    // A --prompt with no '=' is a usage error (exit 2, clap-level).
    slate()
        .arg("render-template")
        .arg(vault.path())
        .arg("Templates/Meeting.md")
        .arg("--prompt")
        .arg("noequals")
        .assert()
        .code(2);
}

#[test]
fn render_template_missing_template_exits_one() {
    let vault = seed_template_vault();
    // Missing template → exit 1 with the session's error text (surfaced
    // verbatim through the `slate: ` prefix). We assert the exit code
    // and the prefix; the exact wording is the session's, not ours.
    slate()
        .arg("render-template")
        .arg(vault.path())
        .arg("Templates/DoesNotExist.md")
        .assert()
        .code(1)
        .stderr(predicate::str::starts_with("slate: "));
}

/// `{{vault}}` must render the vault's real directory name even when the
/// vault is passed as `.` from inside it — the context's `vault_name`
/// comes from the canonical opened path, not the raw argument (Codex
/// adversarial-review, M-6 finding 2).
#[test]
fn render_template_vault_variable_uses_root_name_not_the_dot_argument() {
    // Nest the actual vault under a `TempDir` with a fixed, distinctive
    // (non-dot-prefixed) directory name, so the `.` argument resolves to
    // a name we control and the "not the literal '.'" check is
    // unambiguous (a bare `TempDir` name can itself start with `.tmp`).
    let parent = TempDir::new().unwrap();
    let root = parent.path().join("MyNotesVault");
    let templates = root.join("Templates");
    fs::create_dir_all(&templates).unwrap();
    fs::write(templates.join("V.md"), "vault={{vault}}\n").unwrap();

    slate()
        // Run from *inside* the vault and pass `.` as the vault path.
        .current_dir(&root)
        .arg("render-template")
        .arg(".")
        .arg("Templates/V.md")
        .assert()
        .success()
        // {{vault}} → the real directory basename, never the literal ".".
        .stdout(predicate::str::contains("vault=MyNotesVault"))
        .stdout(predicate::str::contains("vault=.").not());
}
