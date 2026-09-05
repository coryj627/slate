-- Copyright (C) 2026 Cory Joseph
-- SPDX-License-Identifier: AGPL-3.0-or-later

-- Migration 037: Unicode fold indexes for the collision gate (#1077
-- contract I5, docs/plans/32 finding 7).
--
-- The gate that refuses a name which would alias an existing one on a
-- case-insensitive volume (APFS and NTFS default to it) is a PORTABILITY
-- guard that runs on every volume. It compared `lower(path)`, and SQLite's
-- lower() folds ASCII only: `Ä.md` beside `ä.md` slipped through while APFS
-- and NTFS treat them as one file. The gate now folds with the registered
-- `slate_tree_sort_key` (NFC + full-Unicode lowercase — the rule the tree
-- already sorts by, persisted by 033); these expression indexes keep it
-- O(log n). Kanji, kana, and width are untouched by that fold.
--
-- Same UDF-as-schema-state and replay discipline as 033: rebuild our owned
-- indexes so a stale definition can neither block the migration nor
-- silently violate the query-plan contract.
DROP INDEX IF EXISTS idx_files_path_fold;
CREATE INDEX idx_files_path_fold ON files(slate_tree_sort_key(path));
DROP INDEX IF EXISTS idx_dirs_path_fold;
CREATE INDEX idx_dirs_path_fold ON dirs(slate_tree_sort_key(path));
