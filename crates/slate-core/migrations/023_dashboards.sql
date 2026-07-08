-- Migration 023: Bases dashboards (Milestone N, #700).
--
-- Dashboards own only ordered references to saved queries. Deleting a
-- dashboard never deletes a saved query, and deleting a saved query leaves
-- a visible dangling section for the UI to label.

CREATE TABLE dashboards (
    id             TEXT    PRIMARY KEY,
    name           TEXT    NOT NULL UNIQUE,
    sections_json  TEXT    NOT NULL,
    created_at_ms  INTEGER NOT NULL,
    modified_at_ms INTEGER NOT NULL
);

CREATE INDEX idx_dashboards_name ON dashboards(name);
