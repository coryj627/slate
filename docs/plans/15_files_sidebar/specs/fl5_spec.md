# FL5 executable spec — Tag tree: queries, sidebar section, batch tag operations

Issues: FL5-1 ([#664](https://github.com/coryj627/slate/issues/664)) · FL5-2 ([#665](https://github.com/coryj627/slate/issues/665)) · FL5-3 ([#666](https://github.com/coryj627/slate/issues/666)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). Grouped delivery: FL-10 closes #664 and references the core part of #666 after FL-08; FL-11 closes #665 and #666 after FL-03, FL-07, FL-09, and FL-10.
Program: [00_program.md](../00_program.md) (locked decision 9; DoD §FL-A/§FL-B). Tag-scope semantics per #564–#567 are settled — **do not re-litigate**.

Baseline facts (verified 2026-07-14 at `origin/main` `6aa9fce`):

- `file_tags` (migration `019_file_tags.sql`) remains the indexed union of suppression-aware inline tags and frontmatter `TagList`; `tags_db::replace_tags_for_file` and `normalize_tag` are the authoritative seams. Nested `a`→`a/b` semantics remain settled. Exact July 5 line references are obsolete.
- Frontmatter `tags` arrive as `PropertyValue::TagList` (frontmatter.rs); properties rows carry the original list JSON (`value_kind='tag_list'`).
- Save path is the derived-data refresh seam (fl0 baseline); editing a note's frontmatter through core save updates `file_tags` in the same transaction.
- FL-09 supplies the shared flat result list and `filter_files` `#tag` term; FL-07 shortcut storage reserves `kind: tag|untagged` container targets.

---

## FL5-1 · Core tag-tree queries (#664) — closing PR FL-10

```rust
pub struct TagTreeNode {
    pub segment: String,      // display segment, e.g. "reading"
    pub full: String,         // normalized full tag, e.g. "projects/reading"
    pub file_count: u32,      // files with this tag OR any descendant (nested semantics)
    pub direct_count: u32,    // files with exactly this tag
    pub children: Vec<TagTreeNode>,
}
pub struct TagTree { pub roots: Vec<TagTreeNode>, pub untagged_count: u32, pub audio_summary: String }
// VaultSession:
fn tag_tree(&self) -> Result<TagTree, VaultError>
```

Rules:

1. Built from `file_tags` in one query + in-memory `/`-split assembly. Intermediate segments materialize as nodes even when no file carries them exactly (`a/b/c` alone still yields `a` and `a/b`, `direct_count = 0`).
2. Counts are **distinct files**; `file_count` uses the settled nested-prefix semantics (`tag_norm = t OR LIKE t || '/%'`). `untagged_count` = markdown files with zero `file_tags` rows.
3. Order: children alphabetical by segment (casefold; already lowercase by normalization). Deterministic.
4. Display case: `tag_norm` is lowercase by design (#564–#567); nodes display the normalized form. **No** original-case recovery in v1 (would require a new column; note as deferred).
5. `audio_summary`: `"{n} tags, {u} untagged notes."` (grouped decimals; omit second clause when `u = 0`).
6. Budget: ≤ 25 ms at 10k (bench `tag_tree/{10k}`); no caching in v1 — rebuilt per call, the sidebar refreshes it on the existing scoped-invalidation events, not per keystroke.

Tests: fixture with nested/deep/sibling tags + intermediate-only segments; count semantics (nested vs direct, distinct-file dedup); untagged; permutation invariance; bench.

- [ ] `tag_tree()` + records + summary; fmt/clippy; host-independent
- [ ] Tests + bench baseline

## FL5-2 · Tags section in the sidebar (#665) — closing PR FL-11

1. A collapsible **Tags** section rendered below the folder tree (final order: Shortcuts, Recents, folder tree, Tags), header with total count, AX group + header per FL3-3 conventions; collapsed state device-local, default **collapsed** (zero cost until opened; `tag_tree()` fetches on first expand, refreshes on tree-invalidation events while expanded).
2. Rows: disclosure per nested level (reuse the folder-row disclosure interaction + AX patterns wholesale — expanded/collapsed, "level N"); label = segment; count badge = `file_count`, AX `"projects, 12 notes, collapsed, level 1"`. An **Untagged** leaf row renders last when `untagged_count > 0`.
3. **Activation** (click/Return): shows the FL4-2 flat result list with query `#<full>` (Untagged: a reserved scope executed via a dedicated core call — `filter_files` gains `scope_untagged: bool` or the UI calls a sibling method; pick at implementation, either way the list presentation and announce are FL4-2's). The filter field shows the produced query — editable, teaching the grammar.
4. Context menu: **Copy Tag** (`#full`), **Add to Shortcuts** (FL3-3 `kind: tag`; Untagged uses `kind: untagged`). These shortcuts are dual-pane containers whose activation drives the list with the tag/Untagged query; in single-tree mode they reuse the FL4 flat-list handoff. **Filter by Tag** uses the same handoff for VO rotor discoverability. Tag rename/delete are **out of FL scope** (they rewrite note bodies at vault scale; deferred, noted in program close-out list).
5. Empty state: section shows one quiet row "No tags yet."

Tests: section lifecycle (lazy fetch, invalidation refresh); disclosure AX parity with folders; activation query handoff incl. Untagged; tag and Untagged shortcut round-trip as containers; empty state.

- [ ] Section + rows + disclosure AX; lazy fetch/refresh
- [ ] Activation → FL4-2 list; Untagged path; context menu
- [ ] Tests; a11y 100/100 on tip; APCA both appearances

## FL5-3a · Batch tag core APIs (#666 prerequisite) — FL-10 (`Refs #666`)

Core API (new, in the session; app never regex-edits note bodies — locked decision 9):

```rust
pub struct TagEditReport { pub changed: u32, pub skipped: Vec<SkippedFile>, pub inline_remainder: u32, pub audio_summary: String }
// VaultSession:
fn add_tag_to_files(&self, paths: Vec<String>, tag: String) -> Result<TagEditReport, VaultError>
fn remove_tag_from_files(&self, paths: Vec<String>, tag: String) -> Result<TagEditReport, VaultError>
```

Rules:

1. `tag` validated + normalized via `normalize_tag`; empty/invalid ⇒ `InvalidQuery`-class error before touching files.
2. **Add**: ensure frontmatter exists (create a minimal block when absent), append to the `tags:` list iff not already present under normalization; preserve the rest of the frontmatter byte-for-byte where possible (edit via the parsed property list + serializer used by the in-note properties widget (U3) — locate and reuse; if none is exposed in core, this issue extracts one, flagged in the PR).
3. **Remove**: remove matching entries (normalized compare) from the frontmatter `tags:` list only. Inline body `#tag` occurrences are **counted, not edited** (`inline_remainder` = files still carrying the tag inline) — honest partial semantics surfaced to the user, body munging deferred.
4. Each file edit rides the normal core save path (one transaction per file: content write + derived-data refresh incl. `file_tags`), the whole batch in one API call with one report. Files whose on-disk hash changed underneath (conflict convention) are skipped into `skipped`, not overwritten.
5. `audio_summary`: add `"Tagged {n} files with #{tag}."`; remove `"Removed #{tag} from {n} files."` + `" {m} still have it inline."` when `inline_remainder > 0`.
Tests: add idempotence + frontmatter-creation fixture; remove leaves inline + `inline_remainder` count; normalization collisions (`#Reading` vs `reading`); conflict-skip; report strings verbatim; metadata/tag census extension.

- [ ] Core add/remove + report + serializer reuse
- [ ] Core tests; extend the FL0-3 walk with tag-edit operations

## FL5-3b · Batch tag UI (#666 remainder) — closing PR FL-11

Context/menu actions on a single or multi-file selection provide **Add Tag…** (autocomplete from `tag_tree`, free entry allowed) and **Remove Tag…** (selection tags with counts). They use the shared action catalog and FL-03 batch-selection rules, announce the core `audio_summary` once, consolidate skips, report inline remainders honestly, and refresh the tag tree/list through existing invalidation.

Tests: mixed-selection menu state and counts; autocomplete/free entry; add/remove result announcement; consolidated skips; inline-remainder messaging; action parity.

- [ ] Shared action-catalog menu/editor flows
- [ ] UI tests; a11y 100/100 on tip; APCA both appearances
