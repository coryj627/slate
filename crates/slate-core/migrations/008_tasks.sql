-- Migration 008: per-file Markdown task items.
--
-- Powers the Tasks panel and vault-wide task review in Milestone G.
-- Schema follows `docs/plans/05_locked_architecture_decisions.md` §4.5.
-- One row per `- [ ]` / `- [x]` / `- [/]` / `- [-]` (etc.) list item
-- discovered in a markdown file, written in document order with a
-- 0-based `ordinal` so the UI and `toggle_task_status` can target
-- individual tasks stably across saves.
--
-- Columns:
--   file_id      — owning file (cascades on delete).
--   ordinal      — 0-based document order within the file. Stable
--                  across saves for a given parser version, so the
--                  Mac UI can refer to "task N" without holding line
--                  numbers that shift under edits.
--   text         — raw task text (everything after `- [X] `) with
--                  the trailing emoji-metadata block stripped. See
--                  `slate_core::tasks` module docs for the parsing
--                  rules.
--   status_char  — raw character between the brackets (`' '`, `'x'`,
--                  `'X'`, `'/'`, `'-'`, etc.). Stored as a 1-char
--                  TEXT so project-specific status sets (Tasks
--                  plugin lets users define their own) survive a
--                  round-trip even if we don't interpret them.
--   completed    — derived boolean (1 when status_char ∈ {'x','X'}).
--                  Indexed so the most common filter ("show me
--                  open tasks") is a direct btree probe.
--   due_ms       — UTC midnight of a 📅 `YYYY-MM-DD` marker, if any.
--   scheduled_ms — UTC midnight of a ⏳ `YYYY-MM-DD` marker, if any.
--   priority     — integer per Tasks-plugin emoji set:
--                    ⏫ →  2 (highest)
--                    🔼 →  1 (high)
--                    (absent) → NULL
--                    🔽 → -1 (low)
--                    ⏬ → -2 (lowest)
--                  Stored signed so `priority_at_least` filters can
--                  do a single `>=` comparison.
--   recurrence   — raw 🔁 marker payload (e.g. `every week`). Storage
--                  only; execution is a V1.x follow-up so the field
--                  survives untouched.
--   line         — 1-based line number where the task is anchored,
--                  for the "jump to task in editor" affordance.
--   byte_offset  — byte offset of the start of the task's line in
--                  the source, mirroring the headings table so the
--                  editor can place the cursor without rescanning.
--
-- Rows are managed exclusively by the scanner's slow path; the fast
-- path (mtime+size+ctime match) never touches this table — same as
-- headings / links / properties.

CREATE TABLE tasks (
    file_id      INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    ordinal      INTEGER NOT NULL,
    text         TEXT NOT NULL,
    status_char  TEXT NOT NULL,
    completed    INTEGER NOT NULL,
    due_ms       INTEGER,
    scheduled_ms INTEGER,
    priority     INTEGER,
    recurrence   TEXT,
    line         INTEGER NOT NULL,
    byte_offset  INTEGER NOT NULL,
    PRIMARY KEY (file_id, ordinal)
);

CREATE INDEX idx_tasks_completed ON tasks(completed);
CREATE INDEX idx_tasks_due ON tasks(due_ms);
