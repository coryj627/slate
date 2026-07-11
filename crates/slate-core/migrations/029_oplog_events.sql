-- O-6 (#544): the derived temporal-query index over the op logs.
-- Plain rowid table, NO uniqueness constraints: the append path
-- inserts each event exactly once and the rebuild path recreates the
-- table from scratch — dedup has no producer, and same-millisecond
-- events must NOT be swallowed. Regenerable by design (SQLite is the
-- regenerable index over filesystem truth, 05 §9.2).
--
-- The CASCADE mirrors open_marks (028): SQLite recycles rowids, so a
-- note deleted and re-created can be assigned the dead note's id — a
-- surviving event row would falsely attach the dead note's change/
-- deleted-content history to the newcomer (the O-1 recycled-id hazard,
-- closed at this layer too; adversarial review).
CREATE TABLE oplog_events (
    file_id      INTEGER NOT NULL
                     REFERENCES files(id) ON DELETE CASCADE,
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
-- (file_id, ts_ms): the row-eval fallback's per-file EXISTS probes.
CREATE INDEX oplog_events_file_ts ON oplog_events (file_id, ts_ms);
-- (event_class, ts_ms, file_id): COVERS the pushdown membership
-- subqueries — `WHERE event_class = ? AND ts_ms >= ?` yields file_id
-- straight from the index, no rowid table lookups (adversarial
-- round 3: the plain ts_ms index cost a per-row table fetch).
-- deleted_content_matches still fetches rows for deleted_text.
CREATE INDEX oplog_events_class_ts ON oplog_events (event_class, ts_ms, file_id);

-- Staleness marker (adversarial review): set when an append-time event
-- insert fails AFTER its op-log entry landed — the entry is durable but
-- its index rows are missing, and table emptiness alone would never
-- notice. Any row present ⇒ the next scan regenerates oplog_events from
-- the logs and clears the marker in the same transaction.
CREATE TABLE oplog_events_stale (
    marker INTEGER NOT NULL
);
