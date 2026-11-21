-- Copyright (C) 2026 Cory Joseph
-- SPDX-License-Identifier: AGPL-3.0-or-later

-- Migration 042: the tree key folds the final sigma; rebuild every index
-- that stores it (#1705, plan 47 CB-32, the owner's CD22).
--
-- `slate_tree_sort_key` was NFC + full-Unicode lowercase. Rust's lowercase
-- reads context for one letter: a capital sigma lowercases to the final
-- form U+03C2 at a word's end and to U+03C3 before more letters, so a link
-- target's stem lowered alone and its file's path lowered whole folded
-- apart (`[[ΚΟΣΜΟΣ]]` gave `κοσμος.md`, the file `ΚΟΣΜΟΣ.md` gave
-- `κοσμοσ.md`) and the link never resolved. The key now folds U+03C2 to
-- U+03C3 after lowercasing.
--
-- The function is schema state: these seven indexes store its answers
-- (033's two directory-page indexes, 037's two collision-gate indexes,
-- 039's name index, 041's two dangling-target indexes). Each is rebuilt
-- here from its own text, so every stored key is the new fold's. A stale
-- key does not make the bundled SQLite (3.54.1) report the index corrupt,
-- whatever 041's comment says: writes still succeed, a lookup by the new
-- key misses silently, and `PRAGMA integrity_check` lists the rows
-- missing from the index (measured for CB-32). So the rebuild is what
-- keeps every lookup right. Same replay discipline as 033, 037, 039 and
-- 041: drop our owned index, then create it.
--
-- A link resolved under the old fold keeps its target until its source is
-- indexed again, and a scan re-resolves only dangling rows. Only a target
-- that holds a sigma can resolve differently under the new fold (a target
-- with none folds the same way, and so does every file it can match), so
-- those rows are set dangling here and the open's scan resolves them again
-- at its tail. Until that scan ends, such a link reads as unresolved.
UPDATE links SET target_path = NULL
WHERE is_external = 0 AND target_path IS NOT NULL
  AND (instr(target_raw, 'ς') > 0 OR instr(target_raw, 'σ') > 0 OR instr(target_raw, 'Σ') > 0);

DROP INDEX IF EXISTS idx_dirs_parent_tree;
CREATE INDEX idx_dirs_parent_tree
    ON dirs(parent_path, slate_tree_sort_key(name), path COLLATE BINARY);

DROP INDEX IF EXISTS idx_files_parent_tree;
CREATE INDEX idx_files_parent_tree
    ON files(
        CASE
            WHEN length(path) = length(name) THEN ''
            ELSE substr(path, 1, length(path) - length(name) - 1)
        END,
        slate_tree_sort_key(name),
        path COLLATE BINARY
    );

DROP INDEX IF EXISTS idx_files_path_fold;
CREATE INDEX idx_files_path_fold ON files(slate_tree_sort_key(path));
DROP INDEX IF EXISTS idx_dirs_path_fold;
CREATE INDEX idx_dirs_path_fold ON dirs(slate_tree_sort_key(path));

DROP INDEX IF EXISTS idx_files_name_fold;
CREATE INDEX idx_files_name_fold ON files(slate_tree_sort_key(name));

DROP INDEX IF EXISTS idx_links_target_path_fold;
CREATE INDEX idx_links_target_path_fold ON links(slate_tree_sort_key(slate_link_target_path(target_raw)))
WHERE target_path IS NULL AND is_external = 0;
DROP INDEX IF EXISTS idx_links_target_name_fold;
CREATE INDEX idx_links_target_name_fold ON links(slate_tree_sort_key(slate_link_target_name(target_raw)))
WHERE target_path IS NULL AND is_external = 0;
