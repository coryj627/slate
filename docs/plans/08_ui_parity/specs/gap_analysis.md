# U1–U5 gap analysis: plan vs. current state (2026-07-03)

Written before spec authoring, per the program's required-reading step. Sources: the six
plan docs in `docs/plans/08_ui_parity/`, the refreshed knowledge graph (`graphify-out/`,
2026-07-03, post-U0), and three deep code surveys (center column, sidebar + panels, Rust
core). Each gap below records the discrepancy and the **resolution baked into the specs**,
so no executor has to re-litigate them.

## G1 — "Preserve the existing per-file open/rename/delete" describes a UI that does not exist

`u2_file_tree.md` (U2-4) says the tree must "preserve selection + the existing per-file
open/rename/delete." **Reality:** file rows in `FileListSidebar.swift` have no context
menu; there is no per-file rename or delete anywhere in the UI, no `VaultSession` API for
file rename/move/delete, and the only rename machinery is *property* rename
(`rename_property_across_vault`). `VaultProvider` has `rename(from,to)` and `delete`
(trash) primitives at the filesystem layer only (`vault/provider.rs:14-107`).

**Resolution:** U2-2's mutation surface explicitly includes **file-level**
`rename_file` / `move_file` / `delete_file` session APIs alongside the folder ops, and
U2-5 adds the UI for both. The word "preserve" in the plan applies to per-file **open**
(selection → note load) only. Scope grows by ~1 session API family; it was already
implied by U2-3 ("when a file or folder moves").

## G2 — No incremental rescan; folder mutations must not trigger full rescans

The plan requires large-vault discipline (10k files) and instant interactions, but the
core has **no incremental rescan** — `scan_initial` is full-vault, and `provider.watch`
is stubbed (returns `Ok(None)`). A folder move that rescanned the vault would blow the
perf budget.

**Resolution:** every U2 mutation updates the SQLite index **surgically inside the same
transaction** (path-prefix UPDATE on `files` preserving `id`, ditto the new `dirs` table,
scoped link re-resolution), never a rescan. Stable `files.id` across renames also keeps
per-file op-logs attached (op-logs are keyed by `files.id`, `oplog.rs`).

## G3 — Op-log has no structural (multi-file) operations

The op-log journal (`YOLG`, `oplog.rs`) knows `WholeFileReplace` and `EditBatch`, keyed
per file. U2-2 requires each directory mutation to be "an op-log entry (reversible)", and
U2-3's census requires "move then undo restores byte-identical link text". A folder move
is a multi-file structural op plus N content rewrites — nothing in the journal can carry
it today.

**Resolution:** a new **structural journal** (SQLite table `structural_ops` in the cache
DB, not a new binary format) records each mutation `{op_id, kind, payload JSON:
from/to + per-file rewrite list with pre/post content hashes, timestamp}`. Content
rewrites go through the existing `save_text` machinery so per-file op-logs stay the
byte-exact history; `undo_structural(op_id)` applies the inverse move and restores
rewritten files to their pre-op bytes (guarded by conflict detection). Undo **UI** (Edit ▸
Undo integration) is out of U2 scope — filed as a follow-up issue at implementation time.

## G4 — `AppState` is single-note everywhere; the tab model needs per-tab documents

`AppState.swift` (4,684 lines) holds exactly one `{selectedFilePath, loadedFilePath,
currentNoteText, savedBaselineText, currentNoteContentHash, hasUnsavedChanges,
currentSaveConflict}` plus one set of per-note collections (headings, links, tasks,
blocks, citations). Every panel binds to those. U1 tabs/splits need N of these.

**Resolution (the central U1 architecture decision):** introduce `NoteDocument` — an
observable object owning one open markdown document's full state (text, baseline, hash,
dirty, save/conflict, per-note collections + load tasks). `AppState` keeps vault-level
state and exposes `activeDocument` (the focused pane's active tab's document). The
existing single-note `@Published` fields migrate INTO `NoteDocument`; panels/toolbar
rebind to `activeDocument`. Migration is sequenced to keep the suite green at every PR
(see u1_spec.md §Sequencing — U1-4 lands *before* U1-2/U1-3 for exactly this reason).
Inactive tabs retain their `NoteDocument` (text buffer in memory) but not a live
`NSTextView`; only visible panes mount editors.

## G5 — U3's "body-only source buffer" inverts today's whole-file editor contract

Today `read_text` → whole file into the editor; `DocumentBuffer` mirrors the whole file
(with `fm_end` tracking); saves write the whole editor string. U3-3/U3-5 move frontmatter
ownership to the properties widget, so the editor buffer must hold **body only** and
saves must **compose** `frontmatter ⊕ body`.

**Resolution:** new session APIs `read_note_parts(path) → {fm_source, body,
content_hash, …}` and `save_composed(path, fm_source, body, expected_hash)`, with the
byte-exact composition rules (empty-frontmatter elision, delimiter normalization,
trailing-newline preservation) implemented in Rust next to `frontmatter.rs` and gated by
a round-trip census (arbitrary fm ⊕ body sequences → byte-identical expected files).
Per-key property edits keep today's immediate-save semantics (conflict-safe, announced);
they update the tab document's hash + fmSource on success so the body buffer's next save
can't false-conflict. `DocumentBuffer` then mirrors body-only text (fm_end = 0 path), so
the #404 keystroke budget is untouched.

## G6 — Reading view needs a block-segmentation API that doesn't exist

U3-1 says "compose the existing pipelines… into inline content". The core exposes
headings, links, tasks, math/code/diagram blocks, citations, embeds per file — but no
**ordered whole-document block segmentation** (paragraphs, lists, quotes interleaved with
those specialized blocks). `EditorSpanKind` (editor_spans.rs:49) covers inline+block
syntax spans for highlighting; `blocks.rs` covers block *anchors* only.

**Resolution:** new session API `reading_blocks(path)` returning ordered top-level blocks
`{kind, byte_range, meta}` derived from the same pulldown-cmark walk the structure layer
already does. Swift renders each block: existing `MathView`/`CodeBlockView`/
`MermaidView`/`EmbedView`/task rows for specialized blocks; `AttributedString(markdown:)`
+ wikilink/tag pre-processing for paragraph-level inline content. Eager `VStack` (the
ContentBlockPanels discipline) for VoiceOver enumerability; virtualization of very large
notes is a recorded perf follow-up, measured in U5-4 before deciding.

## G7 — U3 and U4 both dismantle the left-sidebar panel stack; ownership must be split

`FileListSidebar` hosts 8 panels below the file list; `MainSplitView`'s detail column
hosts 3 more behind a segmented picker. U3-3 removes Properties from the sidebar; U4-2
retires the rest.

**Resolution:** U3 removes **only** `PropertiesPanel` from the stack (it becomes the
in-note widget). U4 ports the remaining 7 stack panels + the 3 detail-column tabs into
the leaf rail (10 leaves total: Outline, Backlinks, Outgoing links, Embeds, Math, Code,
Diagrams, Tasks, Citations, Bibliography), then deletes the stack and the segmented
picker. The mounted-ZStack retention pattern (opacity + allowsHitTesting +
accessibilityHidden — `MainSplitView.swift:118-131`) is the leaf container's mechanism,
unchanged; today all 8 stack panels are permanently mounted anyway, so cost does not
regress.

## G8 — Keyboard shortcut collisions resolved now, not at implementation time

Taken today: ⌘S, ⌘F, ⌘O, ⌘J, ⌘E (embed preview, editor-local), ⌘, ⌘⇧N, ⌘⇧R, ⌘⇧T, ⌘⇧J,
⌘⇧P. The specs assign: **⌘T** new tab, **⌘W** close tab (replaces window-close inside a
vault window; window close remains ⌘⇧W via the File menu), **⌘⇧[ / ⌘⇧]** prev/next tab,
**⌘1…⌘9** select tab N (9 = last), **⌘\\** split right, **⌘⌥\\** split down,
**⌘⌥←→↑↓** move focus between panes (and to the right pane, U4-4), **⌘⌥+ / ⌘⌥-**
grow/shrink focused pane, **⌘⇧E** Reading↔Editing toggle (⌘E stays embed-preview;
Obsidian's ⌘E documented as a deliberate divergence — our ⌘E shipped first),
**⌘⇧D** show-source YAML toggle within the properties widget. All verified free.

## G9 — Workspace persistence has a home

No per-vault UI-state store exists. `.slate/` already holds `cache.sqlite`, `tmp/`,
`oplog/`, `prefs.json` (session.rs:21-24, PrefsJsonStore.swift:30). **Resolution:**
workspace layout persists to `.slate/workspace.json` (versioned schema, atomic write via
the PrefsJsonStore pattern, bounded read, unknown-version → fresh default, missing files
→ per-tab error state). U1-6 stops being "stretch": it is in scope because presentation-
ready demands relaunch continuity.

## G10 — Directories are not first-class in the index

Only files exist in the DB; empty folders (creatable in U2) would be invisible to a
files-derived tree. **Resolution:** new `dirs` table `(id INTEGER PK, path TEXT UNIQUE
NOT NULL, parent_id INTEGER)` maintained by scan (directories are already walked —
session.rs:2334-2366) and by U2 mutations transactionally. Tree node ids are stable
(`dirs.id`/`files.id`), satisfying U2-1's "stable node ids across rescans".

## G11 — Link-rewrite correctness is subtler than "rewrite links to moved files"

The resolver (link_resolver.rs) resolves basename wikilinks case-insensitively with a
**directory-distance tie-break**. Consequences the plan text doesn't spell out:
1. Basename wikilinks to a moved file usually still resolve — rewriting them would churn
   text needlessly (violates "no unrelated link is touched").
2. Moving a **source** file can silently re-target its own basename wikilinks when the
   tie-break winner changes with the new location.
3. Relative markdown links break when their source moves.
4. A move can make previously-unresolved links resolve (new file arrives at the name).

**Resolution (normative rewrite algorithm, censused):** invariant is *referential
stability* — after any move/rename, every link that resolved to file F before still
resolves to F, byte-minimal edits only. Mechanics in u2_spec.md §U2-3: recompute
resolution for (a) all links whose `target_path` is in the moved set, (b) all links whose
source is in the moved set; where resolution would change or dangle, rewrite the link
text to the minimal form that pins the original target (folder-qualified wikilink /
recomputed relative path), preserving alias, anchor, embed prefix, and surrounding bytes
exactly; then `re_resolve_unresolved_links` to let dangling links heal. Census: random +
exhaustive move/rename sequences over generated link graphs asserting the invariant and
round-trip byte-identity via structural undo.

## G12 — SlateSymbol vocabulary is missing the U2–U5 roles

`SlateSymbol` has 23 roles including forward-looking `.newTab/.closeTab/.splitRight/
.readingMode/.editingMode/.folder/.folderOpen`. Missing for U2–U5: `.splitDown`,
`.newFolder`, `.moveTo`, `.rename`, `.trash`, `.outline`, `.backlinks`,
`.outgoingLinks`, `.embed`, `.diagram`, `.tasksLeaf`, `.properties`, `.showSource`,
`.settings`, `.help`, `.vaultSwitch`, `.collapseAll`. Each spec lists the roles it adds
(with v7/fallback pairs) so the vocabulary lands with the milestone that renders it.

## G13 — "Help" utility has no existing surface

U4-3 says Settings/Help/Vault-switcher "reuse existing flows", but no Help surface exists
beyond the default menu. **Resolution:** Help = open the repository README/docs URL via
`NSWorkspace` (the same external-open path links use), registered as
`slate.help.open` in the command registry. Small, honest, replaceable.

## Non-gaps (verified assumptions)

- The graphify graph is fresh (2026-07-03, includes U0 deliverables); its structure
  matches the surveys. Known noise from the 2026-05-22 memory (`ok` token communities)
  persists but doesn't affect U planning.
- U0 deliverables are exactly as the plans describe: `SlateSymbol` (private
  `systemName`, labeled builders, source-lint test), `Tokens` (spacing/type/color +
  `contrastPairings` registry), `PresentationReady` (3 assertions + honest coverage
  boundary), macOS 15 floor, a11y-check floor at 100.
- The command registry pattern (SlateCommands.swift `registerCoreCommands`) is the right
  seam for every new action; the drift test enforces catalogue completeness.
- The mounted-ZStack retention pattern is load-bearing and documented in code; reusing it
  for leaves (U4-1) is correct, not cargo-culting.
- `.slate/`-prefixed and dot-prefixed entries are already excluded from scans; the tree
  API inherits that rule (G10's `dirs` table only contains scanned dirs).

## Execution-order deltas vs. the plan docs

- **U1 internal order:** U1-1 → U1-4 → U1-2 → U1-3 → U1-5 → U1-6 (migration before tab
  UI, so every PR keeps the suite green; rationale in G4 and u1_spec.md).
- **U1-6 promoted from stretch to in-scope** (G9).
- Everything else follows the program's `U0 → (U1 ∥ U2) → (U3 ∥ U4) → U5`.
