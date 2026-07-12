# U2 executable spec — File tree + full file management

Issues: #459 (U2-1) · #460 (U2-2) · #461 (U2-3) · #462 (U2-4) · #463 (U2-5) · #464 (U2-6).
Milestone: GH 25. Parallel with U1 (disjoint surfaces: slate-core + left sidebar).
One PR per issue. Program DoD applies throughout.

**Execution order matches issue order.** U2-1 → U2-2 → U2-3 are backend PRs
(slate-core + uniffi); U2-4 → U2-5 → U2-6 are Mac UI PRs consuming them.

Baseline facts this spec relies on (verified in gap_analysis.md): no file rename/move/
delete API exists (G1); no incremental rescan (G2); op-log is per-file only (G3);
directories are not indexed (G10); resolver semantics create four rewrite subtleties
(G11). Path-safety + atomic-write + trash primitives DO exist at the provider layer
(`vault/fs.rs`) and are reused, never bypassed.

---

## U2-1 · Backend: directory-tree API (#459) — PR 1

### Schema (new migration, next number in `crates/slate-core/migrations/`)

```sql
CREATE TABLE dirs (
    id INTEGER PRIMARY KEY,
    path TEXT NOT NULL UNIQUE,          -- vault-relative, forward slashes, no trailing /
    parent_path TEXT NOT NULL,          -- "" for root children
    name TEXT NOT NULL
);
CREATE INDEX idx_dirs_parent ON dirs(parent_path);
```

`parent_path` (not `parent_id`) keeps prefix-updates on move single-pass and mirrors how
`files.path` already works; referential integrity is enforced by the census, and rows are
regenerable from a rescan (SQLite-is-index-not-source-of-truth is a locked decision).

### Scan integration

`scan_initial_with_progress` already walks directories (session.rs stack traversal).
Add: upsert a `dirs` row for every directory encountered (skip dot-prefixed — same rule
as files); after the walk, delete `dirs` rows not seen this scan (mirrors stale-file
cleanup). Empty directories on disk therefore get rows (they appear in `list_dir`).

### API (session.rs + uniffi mirror)

```rust
pub struct DirChild { … }                    // one of:
pub struct DirNodeSummary { pub id: i64, pub path: String, pub name: String,
                            pub child_dir_count: u32, pub child_file_count: u32 }
pub fn list_dir_children(&self, parent_path: &str, paging: Paging)
    -> Result<DirListing, VaultError>
// DirListing { dirs: Vec<DirNodeSummary>, files: Page<FileSummary> }
```

- `parent_path = ""` lists the root level. Sort: dirs first, then files, each
  case-insensitive alphabetical (matches the sidebar's existing sort).
- Lazy per-level: one call per expanded folder; nothing recursive. `child_*_count`s let
  the UI announce "N items" without fetching children (single aggregate query per level
  using the `idx_dirs_parent` index + a files `parent_path` computed column? **No** —
  files have no parent column; counts come from `WHERE path GLOB parent || '/*' AND path
  NOT GLOB parent || '/*/*'`-style range queries. GLOB special characters in vault paths
  (`[`, `*`, `?` are legal in filenames) break GLOB — use the lexicographic range trick:
  `path > 'parent/' AND path < 'parent0'` (`'0'` = `'/' + 1`) with a `NOT LIKE`
  depth filter using ESCAPE, or simpler and correct: SELECT the level's rows and count in
  Rust. **Normative: range-scan + Rust-side count** — correctness over cleverness; the
  per-level row count is small by construction.)
- Stable ids: `dirs.id` / `files.id` (rescans upsert by path, ids persist — the fast-path
  refresh already guarantees this for files; the census asserts it for dirs).

### Tests / census

- Unit: root listing, nested listing, unicode names (NFC/NFD both appear and sort
  deterministically — sort key = `name.to_lowercase()` on the NFC normalization),
  dot-dir exclusion, empty dir inclusion, paging.
- `census_dir_tree_matches_filesystem` — generate random vaults (depth ≤ 6, width ≤ 12,
  unicode + spaces + bracket/star/question-mark names mixed in), scan, walk
  `list_dir_children` recursively, compare against a direct filesystem walk: identical
  sets, identical counts. 500 random vaults + the exhaustive small shapes (every tree
  shape with ≤ 4 dirs).
- `census_dir_ids_stable_across_rescans` — scan, record ids, touch/add/remove unrelated
  files, rescan ×3: surviving paths keep ids.
- Bench guard: `list_dir_children` on the 10k-file fixture root < 10ms (assert in test
  with generous 10× headroom; criterion entry added in U5-4's bench pass).

## U2-2 · Backend: directory + file mutations (#460) — PR 2

### API surface (session-level; uniffi-mirrored 1:1)

```rust
pub fn create_folder(&self, path: &str) -> Result<(), VaultError>
pub fn rename_folder(&self, path: &str, new_name: &str) -> Result<StructuralReport, VaultError>
pub fn move_folder(&self, path: &str, new_parent: &str) -> Result<StructuralReport, VaultError>
pub fn delete_folder(&self, path: &str) -> Result<(), VaultError>          // trash, recursive
pub fn rename_file(&self, path: &str, new_name: &str) -> Result<StructuralReport, VaultError>
pub fn move_file(&self, path: &str, new_parent: &str) -> Result<StructuralReport, VaultError>
pub fn delete_file(&self, path: &str) -> Result<(), VaultError>            // trash
pub fn undo_structural(&self, op_id: i64) -> Result<StructuralReport, VaultError>
```

`StructuralReport { op_id: i64, moved: Vec<(String, String)>, rewritten: Vec<RewriteOutcome>,
failed: Vec<RewriteFailure> }` — the `rewritten/failed` halves are produced by U2-3's
rewriter (empty until that PR; the type ships here so the API is stable).

### Semantics (normative)

- **Path safety:** every input goes through `resolve_relative` + `resolve_for_mutation`
  (rejects absolute, `..`, vault root). `new_name` must be a single component (no `/`),
  non-empty, not dot-prefixed, and not a reserved name (`.slate` at root).
- **Collisions:** destination existing (case-insensitive compare — APFS default) →
  new error variant `VaultError::DestinationExists { path }`. Moving a folder into its
  own subtree → `VaultError::InvalidArgument`. No overwrite path exists, period
  (program DoD §F "never overwrite in place").
- **Filesystem op:** `provider.rename(from, to)` (`fs::rename`, atomic on-volume,
  creates parent dirs). Deletes go to **trash** via the provider (already implemented).
- **Index update, same transaction:** files under the moved prefix get
  `UPDATE files SET path = :new || substr(path, len(:old)+1) WHERE <range-scan>`,
  preserving `id` (op-logs stay attached, G2/G3); ditto `dirs` incl. `parent_path`/`name`
  recompute; FTS + links/backlinks tables reference `files.id`/`source_file_id` and
  survive untouched except `links.target_path` which U2-3 owns.
- **Op-log / journal:** new table in the cache DB:

```sql
CREATE TABLE structural_ops (
    id INTEGER PRIMARY KEY,
    timestamp_ms INTEGER NOT NULL,
    kind TEXT NOT NULL,        -- create_folder|rename_folder|move_folder|delete_folder|rename_file|move_file|delete_file
    payload TEXT NOT NULL      -- JSON: {from, to, moved: [[old,new],…], rewrites: [{path, hash_before, hash_after},…]}
);
```

  `undo_structural(op_id)`: only the **latest** op is undoable (`op_id` must be MAX(id)
  — undoing out of order re-introduces the multi-file consistency problem; error
  `InvalidArgument` otherwise). Undo = inverse rename/move via the same machinery
  (collision-checked — if the original path has been re-occupied, fail cleanly) +
  restore every `rewrites[].path` to `hash_before` content **via the per-file op-log**
  (`reconstruct` at that hash; each restore goes through `save_text` with
  `expected_content_hash = hash_after` so an external edit since the op surfaces as
  `WriteConflict` in the report, not silent clobber). `create_folder` undo = delete if
  still empty, else per-file failure report. Deletes are **not** undoable via this API in
  U2 (trash holds the bytes; a restore-from-trash API is a recorded follow-up) — the
  journal still records them for auditability.
- **External-change safety:** the fs rename is attempted first; if the source vanished or
  target appeared between check and rename (TOCTOU), the underlying `io::Error` maps to
  the existing error paths — the DB transaction only commits after the fs op succeeds.
  Concurrent in-session mutations are serialized by the session's existing lock
  discipline.

### Tests / census

- Unit per op: happy path, each rejection (collision incl. case-only collision, subtree
  move, dot names, root), trash delete leaves no index rows (files CASCADE via
  `source_file_id` FK; assert links/tasks/blocks rows gone), empty-folder create/undo.
- `census_structural_mutations_path_integrity` — random vaults; random valid+invalid
  mutation sequences (500 seeds × 200 ops); after every op assert: DB paths ≡ filesystem
  walk (set-equal), `files.id` stable for surviving paths, no orphan `dirs` rows, every
  rejection left state byte-identical (snapshot compare).
- `census_structural_undo_round_trip` — random op then `undo_structural`: filesystem +
  DB state byte/row-identical to the pre-op snapshot (content via hashes, structure via
  walks). Exhaustive small-N: every op kind × every 2-op sequence on the 3-file fixture,
  undo the tail op.
- Concurrency: mutation with a stale-session copy of the tree racing an external `touch`
  (mtime bump) — op still correct; external **content** edit to a file inside a moved
  folder — move succeeds (moves don't read content), hash-dependent undo then reports
  the conflict.

  *Amendment (2026-07-12, #860):* a folder WITH children now stages a confirmation alert before the trash ("Delete folder …?" — Move to Trash destructive / Cancel; Escape deletes nothing; AX focus returns to the tree on both buttons). Files and empty folders keep the direct Finder-parity path; a cached zero child-count re-probes before skipping the prompt. Announcement + focus contracts unchanged after confirm.

## U2-3 · Backend: link integrity on move/rename (#461) — PR 3 ⚠ RED-TEAM

The correctness lynchpin. Invariant (**referential stability**): for every link L that
resolved to file F before the mutation, L resolves to F after; links that were unresolved
may only change by *becoming resolved to the file that arrived at their target name*; no
other byte of any file changes.

### Algorithm (inside the U2-2 mutation, same logical transaction, before `StructuralReport` returns)

Let M = {old → new} for every file whose path changes (single file, or all files under a
moved/renamed folder).

1. **Collect candidates.**
   a. *Inbound:* `SELECT` links rows whose `target_path ∈ dom(M)` (resolved links to
      moved files) — group by source file.
   b. *Outbound:* links rows whose source file ∈ M (their context moved).
   c. *Healable:* unresolved links whose `target_raw` basename matches any `basename(new)`
      (they may now resolve — handled by re-resolution in step 5, no text edit).
2. **Recompute resolution** for each candidate link against the **post-move** index
   state (paths updated per U2-2), using the production resolver — never a reimplementation.
3. **Decide per link** (kinds from links.rs; alias/anchor/embed prefix preserved byte-exact):
   - Resolves to the same file id → **no text edit**. If its `target_path` column is
     stale (file moved), update the column only.
   - Would dangle or resolve to a *different* file id → **rewrite the target text** to
     the minimal form pinning the original target, via **verified candidate forms** —
     each candidate is checked through the production resolver, never assumed:
     * wikilink/embed → extensionless vault path → path with extension → vault-rooted
       `/`-prefixed forms (the exact form for ROOT-LEVEL files, whose bare path is a
       basename to the resolver — census-found); alias (`|…`), anchor (`#…`/`^…`),
       embed `!` all carried over unchanged (target-segment splice, never a reform).
     * markdown link → the **vault path** (with extension), angle-bracket-wrapped when
       the authored form was wrapped OR the pin contains whitespace.
       **AMENDED during part-1 implementation (PR #497):** the original "recomputed
       relative path + %20 encoding" instruction assumed CommonMark source-relative
       semantics; Slate's resolver is vault-rooted/basename and does **not**
       percent-decode — relative recomputation is churn and `%20` would dangle. A
       side effect of the same finding: the resolver's leading-`/` handling was
       basename-fallback for root files (contradicting its own docs); fixed to
       rooted-exact in the same PR.
4. **Apply text edits** per affected file: descending-span-order splice (the
   rename-property pattern), then `save_text(path, new_contents,
   expected_content_hash = hash-at-collection-time)`. A `WriteConflict` (external edit
   raced us) records a `RewriteFailure { path, kind: WriteConflict }` and **skips that
   file** — the move stands, the report is honest (mirrors
   `rename_property_across_vault`'s per-file failure discipline). Each save lands in that
   file's op-log normally (this is what makes undo byte-identical).
5. **Re-index:** `replace_links_for_file` for every rewritten file;
   `re_resolve_unresolved_links(tx)` vault-wide (heals case c).
6. **Journal:** the `rewrites` list (path, hash_before, hash_after) goes into the
   `structural_ops` payload (U2-2's undo consumes it).

### Tests / RED-TEAM census (worktree red-team pass before push — program norm)

- Unit matrix (fixtures, exact-byte assertions): basename wikilink survives target move
  untouched; folder-qualified wikilink rewritten; alias/heading-anchor/block-anchor/embed
  each preserved through rewrite; markdown relative link from moved source recomputed;
  markdown link to moved target recomputed; tie-break flip (two `note.md` candidates,
  source moves nearer the other) pinned by qualification; unresolved link heals when
  target arrives; link inside code fence NOT rewritten (extract_links already excludes —
  fixture proves end-to-end); self-link within a moved file; link in YAML frontmatter is
  not a link (existing rule, fixture guards regression).
- `census_link_graph_referential_stability` — generate random vaults (30–120 files, dirs
  depth ≤ 5) with a random link graph mixing all five link forms + 10% unresolved + 10%
  ambiguous basenames; run 300-op random move/rename sequences; after **every** op,
  for the pre-op resolution map: every previously-resolved (link → file id) pair still
  holds; unrelated files byte-identical (hash compare of the whole vault minus expected
  rewrites); links/backlinks tables ≡ freshly-recomputed from source (the
  index-vs-recompute discipline from #404's censuses).
- `census_move_undo_restores_bytes` — after each random op + undo: every file in the
  vault byte-identical to pre-op (this is the plan's round-trip requirement; it
  subsumes "link text restored").
- Exhaustive small-N: 3 files/2 dirs, all link forms, ALL single moves and all 2-move
  sequences.
- Perf guard: rewrite cost on a 10k-file vault where 100 files link to a moved folder of
  50 files: < 500ms end-to-end (test-asserted at 10× headroom; criterion entry in U5-4).
  Loop until clean per the adversarial-census methodology (memory: random + exhaustive,
  repeat after every fix).

## U2-4 · Mac UI: `FileTreeSidebar` (#462) — PR 4

Replaces the flat list inside `FileListSidebar` (file renamed to `FileTreeSidebar.swift`
in this PR; the panel stack below it moves in U4, not here).

- **Data:** `FileTreeViewModel` (per-vault): `rootLevel: [TreeNode]` +
  `children: [NodeID: [TreeNode]]` cache + `expanded: Set<NodeID>`;
  `NodeID = .dir(i64) | .file(i64)`. Fetch via `list_dir_children` on first expand;
  refresh a level after any mutation event (AppState posts a `treeInvalidation(parent:)`
  after every U2-5 command and after rescans). Collapse retains the cached level (cheap)
  but re-fetches on next expand if invalidated.
- **View:** `List(selection:)` bound to the local-selection pattern (`listSelection` +
  `.onChange` mirror — the #448-derived discipline stays). Rows are a recursive
  `OutlineGroup`-equivalent built by flattening `expanded` state into a row array
  (SwiftUI `List` + `children:` requires eager data; we flatten visible rows only —
  laziness = only expanded levels are materialized, satisfying the 10k budget).
  Indentation via `Tokens.Spacing.md` × depth, disclosure chevron
  (`SlateSymbol` — chevron comes from the DisclosureGroup control itself; folder glyph
  `.folder`/`.folderOpen` per expanded state, `decorative` since the row label names it).
- **AX contract per row:** file rows keep today's label (name) + selection announcement
  (#418 pattern, unchanged — the `fileListFocused` gate + pendingNavigation suppression
  both survive). Folder rows: label = name, value = "expanded/collapsed, N items"
  (counts from `DirNodeSummary`), traits include `.isButton`-free plain row +
  `accessibilityAction(named: "Expand"/"Collapse")`; disclosure toggles with
  →/← arrows (List gives this natively on macOS when rows disclose — verify; if the
  flattened-rows approach loses it, implement `onMoveCommand` handling: → expands / ←
  collapses-or-moves-to-parent) and Space/Return selects. **Depth is conveyed**: AX value
  appends "level N" (VoiceOver outline-level parity; SwiftUI has no outline-level API on
  macOS custom rows).
- Empty/loading/error states per level: expanding a folder shows an inline "Loading…"
  row (spinner + text) then children or an inline error row with Retry button.
- Selection→open flows through `appState.openFile(path, target: .currentTab)` if U1-4/5
  have merged, else `selectedFilePath` (U2-4 is buildable against either; the seam is one
  call site — note in PR which applies).
- Sort: dirs-then-files per level (already sorted by the API; the view trusts it).

Tests: tree AX properties (label/value builders unit-tested), expand/collapse state
machine, 10k-file fixture renders root in < 1s in tests and only fetches expanded levels
(spy on the fetch), keyboard expand/collapse mapping, appearance snapshots
(PresentationReady), a11y-check 100.

## U2-5 · Mac UI: file-management commands (#463) — PR 5

- **Commands + shortcuts** (all in `CommandSection` `.file`, palette-mirrored, drift
  test updated): `slate.file.newFolder` (context menu + palette; no global shortcut),
  `slate.file.newNote` (⌘N — creates "Untitled.md" in the selected folder / root,
  opens it, selects title for rename), `slate.file.rename` (**Return** on a focused tree
  row? Return conflicts with selection-activate; **F2**? not Mac-idiomatic; resolved:
  **Enter is open**, rename via context menu + palette + ⌘⌥R shortcut), `slate.file.moveTo`
  (⌘⇧M), `slate.file.delete` (⌘⌫ when tree focused; context menu "Move to Trash").
- **Rename UX:** inline TextField swap in the row (Esc cancels, Return commits), initial
  text = current name with extension excluded from the initial selection for files; a
  malformed/colliding name surfaces the error inline below the field (specific message
  from the error variant), field keeps focus (no silent failure).
- **Move target picker** (`MoveToFolderSheet.swift`): a sheet with a searchable folder
  list (flattened tree paths, filter-as-you-type, arrow navigation — the
  CommandPaletteView interaction pattern), "New Folder…" row at top, Return commits,
  fully labeled; this is the drag-free path the DoD requires.
- **Drag & drop** (enhancement, same code path): tree rows are drag sources (file/folder)
  and folders+root are drop targets; drop calls exactly `move_file`/`move_folder` — the
  commands are the source of truth; a modifier-free drop onto the note editor is out of
  scope (recorded non-goal).
- Every mutation routes through AppState wrappers (`appState.createFolder(…)` etc.) that
  call uniffi, surface `StructuralReport.failed` in a **specific** alert listing skipped
  files (never silent), post the U2-6 announcement, and fire `treeInvalidation`.
- Open-tab retargeting: if a renamed/moved file is open in any tab, the tab's
  `NoteDocument.path`… is immutable (u1_spec) — resolved: `WorkspaceState.retarget(old:
  new:)` swaps the tab's `EditorItem` + rebinds the document's path field internally
  (single mutation point, document text/dirty state preserved — the FILE moved, the
  buffer is still valid; content-hash conflict machinery keeps saves correct since hash
  travels with content, not path). If the file was deleted: tab flips to the U1-6
  error-state pane.

  *Amendment (2026-07-12, #850):* F2 now BEGINS INLINE RENAME — supplementing, not replacing, the context menu / palette / ⌘⌥R routes (the rejection above concerned Return-as-rename). Exact shipped key semantics: file SELECTION opens immediately (arrows/click/type-select); Space/Return toggle a selected FOLDER's disclosure; Return on a file adds no action. Type-select (printable-prefix jump among visible rows, repeated-character cycling, ~1s reset, Shift/Caps-Lock tolerated, Space excluded) landed in the same change, with scroll-to-reveal on landing.

Tests: each command end-to-end against a temp vault (create/rename/move/delete file+
folder), collision + invalid-name error surfacing, keyboard-only walkthrough test
(open palette → New Folder → name it → move a file into it — scripted through the
command registry), retargeting (open tab follows rename; delete → error tab), a11y-check.

## U2-6 · Mac UI: announcements + focus preservation (#464) — PR 6

- Announcement strings (exact, via `postAccessibilityAnnouncement`, priority .medium):
  "Created folder <name>." · "Renamed <old> to <new>." · "Moved <name> to <folder>."
  ("to vault root" for root) · "Moved <name> to Trash." · plus failure forms "Could not
  <verb> <name>: <specific reason>." Rewrite side-effects append ", updated links in N
  notes." when `StructuralReport.rewritten` is non-empty (count = distinct files).
- Focus rules after mutation (tree keeps focus; selection moves): create-folder →
  select the new folder row; rename → keep the (renamed) row selected; move → selection
  follows the moved node to its new location (auto-expand the destination ancestor chain);
  delete → select the next sibling, else previous, else parent. Never window-root.
- Implementation: `FileTreeViewModel.postMutationFocus(_: StructuralReport)` computes the
  target NodeID; the view scrolls it into view (`ScrollViewReader`) and re-anchors
  `listSelection`; VoiceOver focus follows list selection (verified in the runbook pass).
- Tests: unit-test the focus-target computation for every mutation × edge position
  (first/last/only child); announcement strings asserted verbatim; integration: delete
  the last file in a folder → selection lands on the folder, no dead focus.

---

## SlateSymbol additions (with the PR that first renders them)

| Role | v7 | fallback | PR |
|---|---|---|---|
| `.newFolder` | `folder.badge.plus` | `folder.badge.plus` | U2-5 |
| `.newNote` | `square.and.pencil` | `square.and.pencil` | U2-5 |
| `.moveTo` | `arrow.turn.down.right` | `arrow.turn.down.right` | U2-5 |
| `.rename` | `pencil` | `pencil` | U2-5 |
| `.trash` | `trash` | `trash` | U2-5 |

(`.folder`/`.folderOpen` exist from U0.)

## Follow-ups filed during U2 (tracked, not silently dropped)

- Restore-from-trash API + UI (undo for deletes) — file as `enhancement` when U2-2 lands.
- Edit ▸ Undo menu integration for `undo_structural` — file with U2-2.
- File-watcher (provider.watch) so external moves reconcile live — pre-existing gap,
  reference it in the U2-2 PR description.
