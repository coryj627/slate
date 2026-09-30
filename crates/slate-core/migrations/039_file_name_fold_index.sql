-- Copyright (C) 2026 Cory Joseph
-- SPDX-License-Identifier: AGPL-3.0-or-later

-- Migration 039: a Unicode fold index on file names for the embed
-- resolver (#1279; locked decision 05 §9.3.1).
--
-- An embed preview resolved its target by materializing every path in
-- the files table — once per resolution, and again for each of up to 128
-- nested embeds. The resolver now fetches only the rows its target could
-- match: a qualified target by `idx_files_path_fold` (037), a basename by
-- this index over the same registered fold (`slate_tree_sort_key`: NFC +
-- full-Unicode lowercase), so each lookup is O(log n).
--
-- Same UDF-as-schema-state and replay discipline as 033 and 037: rebuild
-- the owned index so a stale definition can neither block the migration
-- nor silently violate the query-plan contract.
DROP INDEX IF EXISTS idx_files_name_fold;
CREATE INDEX idx_files_name_fold ON files(slate_tree_sort_key(name));
