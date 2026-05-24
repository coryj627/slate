-- Migration 009: partial index on tasks.priority.
--
-- Background. `tasks_in_vault(filter)` with `priority_at_least`
-- set produced a `SCAN tasks` plan because migration 008 indexed
-- only `completed` and `due_ms`. Red-team review of PR #134
-- (issue #139) called this out as a Medium scalability concern:
-- a "show me high-priority tasks" query on a 100k-task vault
-- would linear-scan the whole table on every page.
--
-- Partial index keeps the index small. NULL is the dominant
-- value for casual users (most tasks carry no priority emoji);
-- excluding NULLs means the index has at most one row per
-- prioritised task, which is the only population the filter
-- actually queries (`WHERE priority IS NOT NULL AND priority
-- >= ?`). Same partial-index shape as
-- `idx_properties_key_norm` from migration 007.

CREATE INDEX idx_tasks_priority ON tasks(priority) WHERE priority IS NOT NULL;
