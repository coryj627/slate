-- Property list elements previously erased Date, Datetime, and Wikilink
-- variants by storing every string-backed value as an untagged JSON string.
-- The files table is a regenerable cache. Filesystem sizes are nonnegative
-- and the scanner converts them to SQLite's signed integer with a checked
-- conversion, so -1 can never match a real stat. This guarantees one scanner
-- slow path even for files whose real mtime is the Unix epoch.
-- Keep the old property rows until that pass so an interrupted upgrade does
-- not make metadata disappear.
UPDATE files SET size_bytes = -1;
