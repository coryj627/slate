-- Derived note metadata used by the files sidebar. The vault files remain the
-- source of truth; every row is replaced by the scanner/save pipeline.
CREATE TABLE file_meta (
    file_id     INTEGER PRIMARY KEY REFERENCES files(id) ON DELETE CASCADE,
    word_count  INTEGER NOT NULL,
    char_count  INTEGER NOT NULL,
    preview     TEXT NOT NULL
);

-- Populate the new derived table through the established one-time slow-path
-- replay. This intentionally leaves files.birthtime_ms untouched.
UPDATE files SET mtime_ms = 0;
