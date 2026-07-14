# FL4 executable spec — Filter: core query engine + sidebar filter UI

Issues: FL4-1 ([#662](https://github.com/coryj627/slate/issues/662)) · FL4-2 ([#663](https://github.com/coryj627/slate/issues/663)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). Grouped delivery: FL-08 closes #662 after FL-01; FL-09 closes #663 after FL-02 and FL-08.
Program: [00_program.md](../00_program.md) (locked decisions 7–8; DoD §FL-A/§FL-C). Backend half is host-independent slate-core.

Baseline facts (verified 2026-07-14 at `origin/main` `6aa9fce`):

- FTS5 content search exists through `search_db::full_text_search` and `SearchScope`; the ⌘F overlay keeps owning content search. Exact July 5 line references are obsolete, and the sidebar filter must not grow a second content-snippet pipeline.
- Queryable tables: `files` (name/path/mtime/`birthtime_ms`), `file_meta` (word count/preview from FL-01), `file_tags`, `tasks`, and `properties`. Effective created time follows FL0's frontmatter-over-`files.birthtime_ms` rule.
- FFI/`FileSummary` enrichment per fl0_spec FL0-2; `Paging` convention per existing `list_files`.
- `audio_summary` convention: pre-rendered VoiceOver strings on result-set records follow the existing `QueryResultSet` precedent.
- Sidebar states (loading/error/empty) and row components per FL1; announce seam per fl2 baseline.

---

## FL4-1 · Core filter and scoped-listing engine (#662) — closing PR FL-08

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
fn filter_files(&self, query: String, scope_dir: Option<String>, now_ms: i64,
    tz_offset_min: i32, paging: Paging)
    -> Result<SidebarFilterPage, VaultError>   // VaultError::InvalidQuery wraps FilterParseError detail
```

Rules:

1. One SQL statement (joins per term class; `EXISTS` subqueries for tags/tasks), parameterized — **no SQL built from user strings by concatenation**. `scope_dir` composes as an implicit `path:` prefix (dual-pane and tag-scoped reuse).
2. **Determinism:** `now_ms` is a parameter (no wall clock in core — same rule as Graph §P-C); date windows resolve in a fixed UTC-offset handed in by the caller (extend the signature with `tz_offset_min: i32`; the app passes the user's current offset). Result order: effective-name asc (locale-neutral casefold key), tie-break path — total order.
3. **Scoped listing mode lands now:** an empty query is valid only when `scope_dir` is present, normalized, and vault-contained. It returns the same deterministic paged row model for that scope. Unscoped empty query remains `InvalidQuery`; traversal/escape scopes fail before SQL. FL7 consumes this contract without reopening the filter API.
4. `audio_summary` (normative): `"{n} results."`; scoped: `"{n} results in {folder}."` — grouped decimals; 0 ⇒ `"No results."`
5. Budget: ≤ **50 ms** at 10k (bench `sidebar_filter/{10k}` in `scan_bench.rs` or a sibling bench file; baseline in BENCHMARKS.md). Name-substring over 10k rows is a linear scan — acceptable at budget; if it misses, add a casefolded name column in a follow-up, don't pre-build it speculatively.
6. Grammar/AST/parse errors are `pub` — the CLI adopts this exact grammar later (program: not an FL deliverable).

Tests: parser table-tests (every operator, negation, malformed forms); execution fixtures per term + combinations + negation; SQL-injection strings; permutation of term order ⇒ identical results; window boundaries under fixed `now_ms`/offset; scoped empty listing and pagination; scope normalization/traversal rejection; property: `filter(q) ⊆ filter(drop_one_term(q))` for positive-term queries; bench recorded.

- [ ] Parser + typed errors; execution joins; scoped listing; FFI record + method
- [ ] Determinism rules (now/tz parameters); ordering
- [ ] Tests + bench baseline; fmt/clippy; host-independent

## FL4-2 · Top-pinned sidebar filter UI (#663) — closing PR FL-09

1. **Field placement:** the persistent search field is the **topmost sidebar control**, above Shortcuts, Recents, the tree, and Tags. Placeholder `"Filter"`; an operator menu inserts `#`, `@today`, `has:task`, … for discoverability. Focus chord `⌥⌘F` (⌘F stays with content search); both use the existing command registry.
2. **Active-filter presentation** (locked decision 7): a non-empty committed query overlays the sections/tree with a flat paged list of shared FL1 rows plus path subtitle. Underlying expansion and selection state remains intact. Shortcuts/Recents/tree/Tags are hidden while the list is active, leaving one result list and one focus model.
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
