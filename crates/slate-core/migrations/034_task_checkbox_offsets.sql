-- W2-3 editor checkbox activation must use the parser-owned action range
-- instead of reconstructing Markdown syntax in a host. Existing task rows
-- are a regenerable cache; force one scanner slow path to populate ranges.
ALTER TABLE tasks ADD COLUMN checkbox_start_byte INTEGER NOT NULL DEFAULT 0;
ALTER TABLE tasks ADD COLUMN checkbox_end_byte INTEGER NOT NULL DEFAULT 0;
UPDATE files SET size_bytes = -1;
