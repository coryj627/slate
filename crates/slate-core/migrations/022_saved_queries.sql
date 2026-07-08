-- Migration 022: Saved Bases queries (Milestone N, #700).
--
-- Saved queries are durable user-authored records, not derived scanner
-- rows. `query_json` stores the versioned envelope `{ "v": 1, "query": ... }`
-- so future Slate builds can keep unknown versions visible but inert.

CREATE TABLE saved_queries (
    id             TEXT    PRIMARY KEY,
    name           TEXT    NOT NULL UNIQUE,
    description    TEXT,
    query_json     TEXT    NOT NULL,
    source_syntax  INTEGER NOT NULL CHECK (source_syntax IN (0, 1, 2)),
    created_at_ms  INTEGER NOT NULL,
    modified_at_ms INTEGER NOT NULL
);

CREATE INDEX idx_saved_queries_name ON saved_queries(name);
