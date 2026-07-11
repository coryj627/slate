-- #801: file birth time (epoch ms; 0 = unknown, the ctime convention).
-- `oplog.created_since` lowers onto this column because it is stable
-- against compaction folds and cache rebuilds — the oldest oplog_events
-- row shifts with retention, filesystem birth doesn't. Stamped by the
-- scanner and every save-path upsert from FileStat.birthtime_ms.
ALTER TABLE files ADD COLUMN birthtime_ms INTEGER NOT NULL DEFAULT 0;
CREATE INDEX idx_files_birthtime ON files(birthtime_ms);
