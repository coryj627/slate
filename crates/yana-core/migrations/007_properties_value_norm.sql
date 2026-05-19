-- Migration 007: faster property lookups.
--
-- Background. `files_with_property` was wrapping `value_text` in
-- `lower(IFNULL(json_extract(value_text, '$'), value_text))` inside
-- the WHERE clause to do case-insensitive matching on the unwrapped
-- atomic value. That expression defeats `idx_properties_key_value`
-- on the value side — the composite index degrades to a single-
-- column lookup on `key`, scanning every row with that key. The
-- same query also `LEFT JOIN`s `json_each(value_text)` for list-
-- element search, which fires on every matched property row even
-- when `value_kind` is atomic. (#92 item 3.)
--
-- Two-pronged fix:
--
--   1. `value_text_norm` column on `properties`. Stores the pre-
--      lowercased, JSON-unwrapped form for atomic kinds; empty
--      for list / tag_list. Backed by a partial index covering
--      only atomic rows so list rows don't bloat it.
--
--   2. `properties_list_values` side table. One row per element of
--      every list / tag_list property, pre-lowercased. Indexed on
--      `(key, value_norm)` so list-element lookups hit a direct
--      btree probe instead of `LEFT JOIN json_each`.
--
-- The previous `idx_properties_key_value` index is now dead weight
-- (atomic queries hit the partial index; list queries hit the side
-- table) so we drop it.

ALTER TABLE properties ADD COLUMN value_text_norm TEXT NOT NULL DEFAULT '';

-- Backfill atomic rows. Boolean rows need a carve-out: their
-- stored `value_text` is already `true` / `false` (the JSON
-- literal, lowercase), but `json_extract` coerces JSON booleans to
-- SQLite integers 1 / 0, so the generic `json_extract → lower`
-- path would land "1" / "0" in `value_text_norm` and silently
-- break boolean lookups against rows that existed before the
-- migration. The CASE keeps booleans on their raw text and runs
-- the JSON-unwrap path for every other atomic kind.
UPDATE properties
SET value_text_norm = CASE value_kind
    WHEN 'boolean' THEN value_text
    ELSE lower(IFNULL(json_extract(value_text, '$'), value_text))
END
WHERE value_kind NOT IN ('list', 'tag_list');

-- Partial composite index for atomic lookups. List / tag_list
-- properties stay out of this index — they're searched via
-- `properties_list_values`.
CREATE INDEX idx_properties_key_norm ON properties(key, value_text_norm)
    WHERE value_kind NOT IN ('list', 'tag_list');

-- Side table: one row per element of a list / tag_list property.
-- No primary key (rebuilt on every replace_properties_for_file),
-- just the lookup index.
CREATE TABLE properties_list_values (
    file_id      INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    key          TEXT NOT NULL,
    value_norm   TEXT NOT NULL
);

CREATE INDEX idx_properties_list_lookup ON properties_list_values(key, value_norm);
CREATE INDEX idx_properties_list_file ON properties_list_values(file_id);

-- Backfill list elements from existing JSON arrays. `json_each`
-- yields one row per element of `value_text`; we lowercase each
-- one for the index lookup. Numeric elements coerce to text via
-- SQLite's standard coercion.
INSERT INTO properties_list_values (file_id, key, value_norm)
SELECT p.file_id, p.key, lower(elem.value)
FROM properties p, json_each(p.value_text) elem
WHERE p.value_kind IN ('list', 'tag_list')
  AND elem.value IS NOT NULL;

DROP INDEX idx_properties_key_value;
