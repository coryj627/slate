-- Copyright (C) 2026 Cory Joseph
-- SPDX-License-Identifier: AGPL-3.0-or-later

-- Migration 041: folded target indexes over dangling links, so a create
-- heals only the rows that could name it (#1426; locked decision 05 §4 and
-- §9.3.1).
--
-- A create re-resolved every dangling link in the vault against a snapshot
-- of every files row. It now reads only the dangling rows whose authored
-- target could name the created path: an exact (rooted or qualified)
-- target by its path, a bare name by its name, each folded by the
-- registered `slate_tree_sort_key` (NFC + full-Unicode lowercase). The two
-- target-key functions share the resolver's one classification
-- (`link_resolver::selector_target`) and return NULL for the other mode, so
-- a qualified `missing/foo` is in the path index only and never answers a
-- name lookup for `foo`. Partial: only dangling, internal rows are indexed.
--
-- Same UDF-as-schema-state and replay discipline as 033, 037 and 039:
-- rebuild the owned indexes, and a change to the classification needs a
-- migration that rebuilds them again.
DROP INDEX IF EXISTS idx_links_target_path_fold;
CREATE INDEX idx_links_target_path_fold ON links(slate_tree_sort_key(slate_link_target_path(target_raw)))
WHERE target_path IS NULL AND is_external = 0;
DROP INDEX IF EXISTS idx_links_target_name_fold;
CREATE INDEX idx_links_target_name_fold ON links(slate_tree_sort_key(slate_link_target_name(target_raw)))
WHERE target_path IS NULL AND is_external = 0;
