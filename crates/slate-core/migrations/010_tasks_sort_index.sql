-- Migration 010: expression index over the tasks_in_vault sort tuple.
--
-- Background. `tasks_in_vault`'s ORDER BY is
--   IFNULL(due_ms, MAX) ASC,
--   IFNULL(priority, MIN) DESC,
--   file_id ASC,
--   ordinal ASC
-- Without an index that matches the leading expressions, SQLite's
-- planner emitted `USE TEMP B-TREE FOR ORDER BY` on every page —
-- materialising every matching row, sorting in a temp btree, then
-- applying LIMIT. First-page bench on 10k tasks was 1.24 ms (fine);
-- worst case scales with the total matching row count, not page
-- size (red-team M2 / issue #145).
--
-- Fix: expression index over the leading two sort keys with the
-- same IFNULL sentinels the query uses. SQLite expression indexes
-- require literal constants (not parameters), so the query in
-- `tasks_db::tasks_in_vault` was updated alongside this migration
-- to bake those sentinels in as literals — see the matching
-- comment block there.
--
-- Sentinel choices:
--   - due_ms (ASC NULLS LAST):  9223372036854775807 (i64::MAX) —
--     NULL rows sort after every real date.
--   - priority (DESC NULLS LAST): -2147483648 (i32::MIN as i64) —
--     NULL rows sort below every real priority in the DESC walk.
--     Picked from the i32 range (not i64::MIN) so the negation
--     trick in the cursor predicate stays inside i64.
--
-- The trailing `file_id, ordinal` are included so the index can
-- also help with the third-tier sort once the join resolves
-- f.path; for tie-breaks within (due, priority), the planner can
-- walk file_id order without a secondary sort.
--
-- f.path itself is in the files table and can't be denormalised
-- into this index without a much bigger schema change, so the
-- final tier (path COLLATE BINARY) may still drive a small partial
-- sort within tie groups. That's acceptable — the dominant cost
-- was the full-table sort, not the per-tie-group cleanup.

CREATE INDEX idx_tasks_sort ON tasks (
    IFNULL(due_ms, 9223372036854775807),
    IFNULL(priority, -2147483648) DESC,
    file_id,
    ordinal
);
