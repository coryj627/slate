-- Migration 013: bibliography entries + per-file citation index.
--
-- Powers Milestone L (`docs/plans/05_locked_architecture_decisions.md`
-- §6.5). Two tables:
--
-- `bibliography_entries` holds the merged bibliography loaded from
-- the `.bib` / `.json` sources configured in `.slate/prefs.json`.
-- One row per citation key; `source_path` records which source the
-- entry was loaded from (after `merge_sources`'s first-source-wins
-- resolution).
--
-- `file_citations` holds the per-file citation index. One row per
-- `CitedItem` (so a `[@a; @b]` site produces two rows). The scanner
-- writes these via the standard DELETE-by-file_id + bulk INSERT
-- pattern used by `headings` / `links` / `tasks`.
--
-- `mode` column encodes `CitationMode` as `0=Bracketed`,
-- `1=InText`, `2=SuppressAuthor` — same trick as `links.kind`.
--
-- Bibliography itself is not append-only: a bibliography reload
-- (driven by the file-watch debouncer from #276) replaces the
-- entire `bibliography_entries` table in one transaction. The
-- per-file rows in `file_citations` are independent of bibliography
-- state, so they don't need to move when entries change.

CREATE TABLE bibliography_entries (
  key             TEXT PRIMARY KEY,
  item_type       TEXT NOT NULL,
  title           TEXT,
  authors_json    TEXT,
  year            INTEGER,
  journal         TEXT,
  doi             TEXT,
  url             TEXT,
  publisher       TEXT,
  raw_csl_json    TEXT NOT NULL,
  source_path     TEXT NOT NULL,
  last_updated_ms INTEGER NOT NULL
);
CREATE INDEX idx_bib_year  ON bibliography_entries(year);
CREATE INDEX idx_bib_title ON bibliography_entries(title);

CREATE TABLE file_citations (
  file_id       INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
  citation_key  TEXT NOT NULL,
  locator_label TEXT,
  locator_text  TEXT,
  mode          INTEGER NOT NULL,
  line          INTEGER NOT NULL,
  byte_offset   INTEGER NOT NULL
);
CREATE INDEX idx_file_citations_file ON file_citations(file_id);
CREATE INDEX idx_file_citations_key  ON file_citations(citation_key);
