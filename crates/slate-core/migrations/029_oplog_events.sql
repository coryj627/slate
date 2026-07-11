-- O-6 (#544): the derived temporal-query index over the op logs.
-- Plain rowid table, NO uniqueness constraints: the append path
-- inserts each event exactly once and the rebuild path recreates the
-- table from scratch — dedup has no producer, and same-millisecond
-- events must NOT be swallowed. Regenerable by design (SQLite is the
-- regenerable index over filesystem truth, 05 §9.2).
CREATE TABLE oplog_events (
    file_id      INTEGER NOT NULL,          -- files.id (rebound by scan reconcile)
    ts_ms        INTEGER NOT NULL,
    event_class  INTEGER NOT NULL,          -- 1=content_change 2=property_set
                                            -- 3=property_remove 4=task_toggle
                                            -- 5=fm_replace (pure markers: no rows)
    property_key TEXT,                      -- classes 2/3 only, else NULL
    deleted_text TEXT                       -- class 1 only: removed spans this save,
                                            -- concatenated, capped 4096 bytes
                                            -- (UTF-8 boundary-safe); NULL when no
                                            -- old content was in hand
);
CREATE INDEX oplog_events_file_ts ON oplog_events (file_id, ts_ms);
CREATE INDEX oplog_events_ts ON oplog_events (ts_ms);
