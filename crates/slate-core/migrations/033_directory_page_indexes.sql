-- Copyright (C) 2026 Cory Joseph
-- SPDX-License-Identifier: AGPL-3.0-or-later

-- W1-RT-14: bounded, dirs-first directory pages need keyset order and
-- page-local immediate-child counts. The registered sort function is
-- deterministic and shared with Rust, so these indexes preserve the existing
-- NFC/full-Unicode-lowercase order without materializing the whole level.
-- The UDF semantics are schema state: any future Unicode/casefold change must
-- ship a new migration that REINDEXes both expression indexes before new
-- cursor keys are used.
-- Replays can occur after recovery tooling restores schema-version metadata.
-- Rebuild our owned indexes so a stale definition can neither block the
-- migration nor silently violate the query-plan contract.
DROP INDEX IF EXISTS idx_dirs_parent_tree;
CREATE INDEX idx_dirs_parent_tree
    ON dirs(parent_path, slate_tree_sort_key(name), path COLLATE BINARY);

-- Files predate an explicit parent_path column. This exact expression is the
-- established parent derivation used by list_dir_children; indexing it makes
-- direct-child counts and keyset pages proportional to the requested page.
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
