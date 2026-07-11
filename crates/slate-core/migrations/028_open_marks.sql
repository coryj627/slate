-- O-4 (#542): "changes since last open" baselines. Regenerable by
-- design — a cache rebuild loses marks and the feature degrades to
-- "no baseline yet", which is honest and harmless (plan decision #6).
-- ON DELETE CASCADE cleans a mark when its file row goes (including
-- delete→recreate rowid recycling: the recycled id starts markless).
CREATE TABLE open_marks (
    file_id              INTEGER PRIMARY KEY
                         REFERENCES files(id) ON DELETE CASCADE,
    last_opened_ms       INTEGER NOT NULL,
    content_hash_at_open TEXT NOT NULL
);
