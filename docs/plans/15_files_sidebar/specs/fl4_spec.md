# FL4 executable spec — Filter: core query engine + sidebar filter UI

Issues: FL4-1 ([#662](https://github.com/coryj627/slate/issues/662)) · FL4-2 ([#663](https://github.com/coryj627/slate/issues/663)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). One PR per issue.
FL4-1 requires FL0-1 (file_meta); FL4-2 requires FL4-1 + FL1 (row component).
Program: [00_program.md](../00_program.md) (locked decisions 7–8; DoD §FL-A/§FL-C). Backend half is host-independent slate-core.

Baseline facts (verified 2026-07-05):

- FTS5 content search exists (`search_db.rs`: `full_text_search(query, scope, cancel)`, `SearchScope::{Vault, Folder, File, Tag}` :48–71 — Tag = exact OR nested-child; `QueryHit { path, snippet, score }` :96+). The ⌘F overlay consumes it and **keeps owning content search** (locked decision 8) — the sidebar filter is metadata-only and must not grow a snippet pipeline.
- Queryable tables: `files` (name/path/mtime), `file_meta` (created_ms, word_count — FL0-1), `file_tags` (`tag_norm`, normalize = trim/strip-`#`/lowercase, tags_db.rs:124), `tasks` (per-file rows; open = `completed = 0`), `properties`.
- FFI/`FileSummary` enrichment per fl0_spec FL0-2; `Paging` convention per existing `list_files`.
- `audio_summary` convention: pre-rendered VO strings on result-set records (QueryResultSet precedent, search_db.rs:59–88).
- Sidebar states (loading/error/empty) and row components per FL1; announce seam per fl2 baseline.

---

## FL4-1 · Core filter engine: grammar + execution (#662) — PR 1

### Grammar (normative, locked decision 8)

New module `crates/slate-core/src/sidebar_filter.rs`. Query = whitespace-separated terms, all ANDed; `-` prefix negates any term.

| Term | Meaning |
|------|---------|
| `word` | effective-name match: case-insensitive substring of `display_name ?? stem` (diacritic-insensitive via the NFC/case-fold convention already used for ghost keys — reuse, don't re-implement) |
| `#tag` | file has `tag_norm` = `tag` or any nested child (`#a` matches `a/b`) — exactly `SearchScope::Tag` semantics |
| `@today` `@yesterday` `@last7d` `@last30d` | modified within the window (calendar days, caller-supplied "now" — see determinism rule) |
| `@YYYY-MM-DD` | modified on that calendar day |
| `has:task` | at least one open task (`completed = 0`) |
| `ext:pdf` | file extension, case-insensitive |
| `path:research/` | vault-relative path prefix (folder scoping) |

Malformed operator terms (`@notadate`, `has:xyzzy`, bare `#`) are **errors**, not silent name-words: `FilterParseError { term, reason }` — the UI shows which term is wrong (silent fallback teaches users the grammar is broken).

### Execution & FFI

```rust
pub struct SidebarFilterPage { pub files: Vec<FileSummary>, pub total: u64, pub audio_summary: String }
// VaultSession:
fn filter_files(&self, query: String, scope_dir: Option<String>, now_ms: i64, paging: Paging)
    -> Result<SidebarFilterPage, VaultError>   // VaultError::InvalidQuery wraps FilterParseError detail
```

Rules:

1. One SQL statement (joins per term class; `EXISTS` subqueries for tags/tasks), parameterized — **no SQL built from user strings by concatenation**. `scope_dir` composes as an implicit `path:` prefix (dual-pane and tag-scoped reuse).
2. **Determinism:** `now_ms` is a parameter (no wall clock in core — same rule as Graph §P-C); date windows resolve in a fixed UTC-offset handed in by the caller (extend the signature with `tz_offset_min: i32`; the app passes the user's current offset). Result order: effective-name asc (locale-neutral casefold key), tie-break path — total order.
3. Empty query ⇒ `InvalidQuery` (the UI never calls it with nothing; listing is `list_dir_children`'s job).
4. `audio_summary` (normative): `"{n} results."`; scoped: `"{n} results in {folder}."` — grouped decimals; 0 ⇒ `"No results."`
5. Budget: ≤ **50 ms** at 10k (bench `sidebar_filter/{10k}` in `scan_bench.rs` or a sibling bench file; baseline in BENCHMARKS.md). Name-substring over 10k rows is a linear scan — acceptable at budget; if it misses, add a casefolded name column in a follow-up, don't pre-build it speculatively.
6. Grammar/AST/parse errors are `pub` — the CLI adopts this exact grammar later (program: not an FL deliverable).

Tests: parser table-tests (every operator, negation, malformed forms); execution fixtures per term + combinations + negation; permutation of term order ⇒ identical results; window boundaries under fixed `now_ms`/offset; property: `filter(q) ⊆ filter(drop_one_term(q))` for positive-term queries; bench recorded.

- [ ] Parser + typed errors; execution joins; FFI record + method
- [ ] Determinism rules (now/tz parameters); ordering
- [ ] Tests + bench baseline; fmt/clippy; host-independent

## FL4-2 · Sidebar filter UI (#663) — PR 2

1. **Field placement:** a search field pinned above the tree (below FL3-3's sections when present), placeholder `"Filter"`, with a menu button exposing operator snippets (inserts `#`, `@today`, `has:task`, … — discoverability without memorizing the grammar). Focus chord `⌥⌘F` (⌘F stays with the content-search overlay; both registered in the palette).
2. **Active-filter presentation** (locked decision 7): non-empty committed query replaces the tree with a **flat result list** of FL1 rows plus a path subtitle (folder, secondary style; AX value appends `"in <folder>"`). Shortcuts/Recents sections hide while filtering (one list, one focus). List paging follows the existing FileSummaryPage conventions (fetch next page on scroll end).
3. **Lifecycle:** debounce 200 ms after last keystroke; in-flight results replaced wholesale (no incremental mutation — VO stability); Esc in the field clears the query and returns to the tree with prior expansion/selection intact (tree state is never torn down — the filter list is an overlay sibling, not a tree rewrite); Esc in the list moves focus to the field; ↓ from the field enters the list at row 1.
4. **Errors:** `InvalidQuery` renders inline under the field naming the bad term (from `FilterParseError`), AX polite live region; the previous good results stay visible.
5. **Announce:** on settle, `audio_summary` verbatim through the announce seam, deduped on (query, count). Row activation opens the file (all FL2 context-menu verbs work on result rows — they're the same component).
6. **Persistence:** last committed query saved device-local; restored **into the field but not applied** on relaunch (⏎ or edit re-applies) — avoids waking up trapped in a filtered view.
7. `now_ms`/`tz_offset_min` from the app clock at query time; a timer re-evaluates relative windows on day rollover only if a relative term is active.

Tests: debounce/replace-wholesale; Esc state machine (field↔list↔tree); error inline + retained results; announce dedup; restore-not-apply; path-subtitle AX; paging.

- [ ] Field + operator menu + chords/palette
- [ ] Flat-list presentation + tree-state preservation
- [ ] Error/announce/persistence rules
- [ ] Tests; a11y 100/100 on tip; APCA both appearances
