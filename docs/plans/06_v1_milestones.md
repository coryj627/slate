# 06 — V1 Milestone Decomposition (months 0–3: read+edit Mac alpha)

**Status:** Drafted 2026-05-17 immediately after the bootstrap week landed. Locks the implementation sequence for the months-0–3 window from `05_locked_architecture_decisions.md` §3.2.

**Strategic goal of this phase:** at the end of 12–16 weeks, the 4 committed AT-user testers have a Mac app they can use against their own existing Obsidian vaults, with their own screen readers, to do all four primary read-and-write workflows — **find a note, read its content, follow its links, and edit it**. The phase ships six progressively-richer builds (one per ~2-week milestone), not one big drop at the end.

**Why this sequence:**

- Each milestone ends in a tester-shippable build, not an internal-only checkpoint. With a real cohort of 4 testers, the unit of work is "what can I put in front of them next?" — not abstract feature buckets.
- The six milestones together exercise every layer that V1's full vision depends on (vault access, parsing, indexing, link graph, properties, search, write path, op log) without yet touching the V1.x content pipelines (math, mermaid, citations, code semantic spans) or V2 surfaces (sync, conflict resolution UI, plugins).
- Honest pacing line: **12 weeks is optimistic, 14–16 weeks is realistic.** Don't promise testers an exact end date; promise the next build.

---

## At a glance

| Milestone | Weeks | Tester build can… | New code surfaces |
|---|---|---|---|
| **A — Vault + file list** | 1–2 | Open vault folder; see all `.md` files in a sidebar; navigate with VoiceOver. | `FsVaultProvider`, SQLite `files` table + migrations, vault picker, sidebar, recent-vaults persistence |
| **B — Read + heading nav** | 3–4 | Select a file; read content with VoiceOver; jump heading-to-heading. | Headings persisted to SQLite, content view, outline panel, heading rotor |
| **C — Backlinks + outgoing links** | 5–6 | See what links to this note; see what this links to; jump between. | Wikilink + Markdown link parsing, link resolution, `links` table, backlinks/outgoing panels |
| **D — Frontmatter properties** | 7–8 | See this note's YAML frontmatter as a structured properties panel. | YAML frontmatter parsing, type inference, `properties` table, properties panel |
| **E — Full-text search** | 9–10 | Search across the vault by content; navigate results accessibly. | FTS5 setup, search API, search UI, results list |
| **F — Editing** | 11–12 | Edit a note, save, see changes persist; conflict detection on save. | `NSTextView` wrapper, write path, op log v0, reindex-on-save |

---

## Per-milestone detail

### Milestone A — "Open my vault, see my files" (weeks 1–2)

**Goal:** An AT user can launch the app, point it at a folder of Markdown files, and navigate the file list with their screen reader.

**User-facing capability**

- App launches to a welcome screen with "Open Vault…" and a list of recent vaults.
- Directory picker (`NSOpenPanel`, directory mode).
- Sidebar populates with every `.md` file in the chosen folder tree, in a flat list ordered by relative path.
- Last-opened vault persists across launches; selecting from recent vaults skips the picker.
- VoiceOver reads the file list correctly; Tab cycles between major regions (toolbar, sidebar, content area placeholder).

**Rust work**

- `FsVaultProvider::new(root)` — synchronous scan, recursive enumeration, content hashing with `blake3`.
- `VaultSession::open(provider, config)` — opens or creates the per-vault SQLite database under `.yana/cache.sqlite`.
- `VaultSession::list_files(filter, paging) -> Page<FileSummary>` — paged file listing.
- `VaultSession::close()` — flushes and closes.
- SQLite migration infrastructure (`schema_version` table, ordered migration functions, idempotent).
- Schema migration v1 creates the `files` table per `docs/plans/05` §4.5.

**Swift work**

- `WelcomeView` with "Open Vault…" button (Cmd+O) and recent-vaults list.
- Recent vaults persisted to `~/Library/Application Support/YANA/recent-vaults.json` (max 8 entries).
- `MainSplitView` skeleton with sidebar + content placeholder.
- `FileListSidebar` — accessible flat list of `FileSummary` rows, each with `accessibilityLabel = "\(name), modified \(date)"`.
- Replace the smoke-test `ContentView` with the new welcome → split-view flow.

**Schema migrations**

- `001_init.sql` — `schema_version`, `files`.

**Tests**

- Unit (Rust): `FsVaultProvider` with synthetic directories; ignores non-Markdown; respects symlinks per design.
- Unit (Rust): migration v0→v1 idempotency.
- Unit (Rust): paged `list_files` returns expected counts and ordering.
- Integration (Rust + Swift): manual — open a real Obsidian vault, see every file.
- Benchmark: open a 5k-note synthetic vault; record time-to-first-listing and total scan time.

**Accessibility checkpoints**

- VoiceOver announces "File list, N items" when entering the sidebar.
- Each row reads as `"<filename>, modified <relative date>"`.
- Tab cycles through major regions in predictable order; Shift+Tab reverses.
- Cmd+O reopens the picker; Esc cancels without changing state.
- Focus on first file in list after vault opens (announced via live region).

**Tester feedback questions**

- Does the app find every file in your vault?
- What file order would you expect by default (alphabetical, modified, custom)?
- How does VoiceOver navigate the list — anything that feels wrong?
- Recent-vaults list — useful as is, or do you want pinning / removal?

**Definition of done**

- Build script ships a runnable binary.
- Two testers exercise the build against their own vaults and report any blocking issues fixed.
- Performance benchmark recorded as the V1 baseline.

---

### Milestone B — "Read a note, navigate its headings" (weeks 3–4)

**Goal:** Tester can select a file and read its content with VoiceOver, using heading-level navigation.

**User-facing capability**

- Click (or arrow + Return) a file in the sidebar → its content displays in the main pane, read-only.
- Outline panel (collapsible) lists the file's headings; clicking a heading scrolls to it.
- VoiceOver heading rotor works across the content.
- Switching files preserves selection state in the sidebar.

**Rust work**

- Extend the scanner to persist headings to a `headings` table during indexing.
- `VaultSession::get_file_metadata(path)` returns headings (and other metadata as available).
- Reparse trigger when `content_hash` changes between scan and re-scan.
- Background indexing on vault open: scan progress reported via `VaultEventListener::on_index_progress`.
- API: `VaultSession::read_text(path)` for the content pane.

**Swift work**

- `NoteContentView` — read-only display of Markdown source for now (no rendered preview yet; raw source is more accessible while we don't have content pipelines).
- `OutlineSidebar` — accessible list of headings with level and text; click to scroll content to that heading.
- Heading anchor scrolling via `ScrollViewReader` + `id` per heading region.
- Progress indicator for first-open indexing, with accessible live-region announcements.

**Schema migrations**

- `002_headings.sql` — `headings` table per `docs/plans/05` §4.5.

**Tests**

- Unit (Rust): heading extraction persists to SQLite correctly across heading levels.
- Unit (Rust): outline retrieval returns rows in document order.
- Unit (Rust): reparse on `content_hash` change.
- Integration: open a note with multiple heading levels, verify outline matches.

**Accessibility checkpoints**

- VoiceOver heading rotor cycles H1–H6 in order.
- Outline list reads as `"Outline, N headings"`; each entry as `"Level N heading: <text>"`.
- Switching files focuses the content area's heading 1 (or first paragraph) and announces it.
- Indexing-in-progress announced as a polite live-region update, not assertive.

**Tester feedback questions**

- Does heading navigation match your expectations from Obsidian / other tools?
- Is the outline panel pulling its weight, or is the rotor enough?
- Anything VoiceOver gets wrong when scrolling through the content?

**Definition of done**

- Heading navigation works on real tester vaults.
- Indexing progress doesn't lock the UI on large vaults.
- Performance benchmark for index build vs. Milestone A baseline recorded.

---

### Milestone C — "See what links to this note" (weeks 5–6)

**Goal:** Tester can navigate the link graph — see backlinks for the current note, see outgoing links, jump between linked notes.

**User-facing capability**

- "Backlinks" panel (collapsible sidebar section) lists notes that link to this one, with a short context snippet per backlink.
- "Outgoing links" panel lists notes this one links to, with status (resolved / unresolved).
- Click (or keyboard-activate) a link → opens that note in the content pane.
- Unresolved links are visually and audibly flagged ("unresolved" suffix in label).

**Rust work**

- Wikilink parser: `[[target]]`, `[[target|display]]`, `[[target#heading]]`, `[[target^block]]`.
- Markdown link parser: `[text](relative/path.md)`, `[text](https://...)`.
- Embed parser (for completeness): `![[target]]` — recorded but not rendered yet (embed rendering is V1.x).
- Link resolution against the vault file index (case-insensitive match, folder-aware).
- `links` table per `docs/plans/05` §4.5.
- API: `backlinks(path, paging)`, `outgoing_links(path)`, `list_unresolved_links()`.

**Swift work**

- `BacklinksPanel` + `OutgoingLinksPanel` (collapsible sidebar sections under the file list).
- Click handler that swaps content pane to the link target.
- Visual+audio treatment for unresolved links.

**Schema migrations**

- `003_links.sql` — `links` table.

**Tests**

- Unit (Rust): wikilink parsing edge cases (display text, subpath, block ref, escaped characters).
- Unit (Rust): Markdown link parsing including relative paths and URLs.
- Unit (Rust): link resolution against synthetic vault (matching by basename, folder-qualified, case-insensitive).
- Unit (Rust): `backlinks` query returns expected rows.
- Integration: open a vault with cross-links, verify backlinks panel matches.

**Accessibility checkpoints**

- Backlinks panel reads as "Backlinks, N entries"; each backlink as `"Backlink from <source note>, context: <snippet>"`.
- Outgoing panel reads as "Outgoing links, N entries"; each as `"Link to <target>"` or `"Unresolved link: <target>"`.
- Keyboard cycles through links predictably; Return opens the target.
- Focus moves to the newly-opened note's first heading on activation.

**Tester feedback questions**

- Are the backlinks accurate against your vault?
- Anything about how the panels are announced that's wrong?
- Useful to see context snippet or just the source note title?
- Unresolved-link treatment — too noisy, too quiet, just right?

**Definition of done**

- Backlinks match expected results on tester vaults.
- Link parsing covers Obsidian's common syntax variants.

---

### Milestone D — "See this note's frontmatter properties" (weeks 7–8)

**Goal:** Tester can read a note's YAML frontmatter as a structured, accessible properties list.

**User-facing capability**

- Properties panel (collapsible) shows each frontmatter key/value pair.
- Type-aware display: dates as dates, lists as itemized lists, links as wikilink references.
- Read-only in this milestone; editing comes in V1.x.

**Rust work**

- YAML frontmatter parser (existing crate; the `serde_yaml` family or `yaml-rust2`).
- Type inference: text, number, boolean, date, datetime, list, wikilink reference, tag list.
- `properties` table per `docs/plans/05` §4.5.
- API: properties included in `FileMetadata`; query `files_with_property(key, value)` for later use.

**Swift work**

- `PropertiesPanel` (collapsible sidebar section, below outline).
- Per-property display: key + value, with type-specific formatting.
- All read-only; no editors yet.

**Schema migrations**

- `004_properties.sql` — `properties` table.

**Tests**

- Unit (Rust): YAML frontmatter parsing edge cases (unicode keys, list values, nested objects → flatten or reject?, mixed types).
- Unit (Rust): type inference correctness.
- Integration: open a note with rich frontmatter, verify panel content.

**Accessibility checkpoints**

- Properties panel reads as "Properties, N items".
- Each property reads as `"Property <key>: <value>"`, with type cue (`"Property tags, list of 3: foo, bar, baz"`).
- Keyboard cycles through properties; no editing affordance yet.

**Tester feedback questions**

- Do your frontmatter properties display correctly?
- Anything missing from the inferred type set?
- How does VoiceOver flow through the property list?

**Definition of done**

- Property display matches real frontmatter on tester vaults.
- Type inference handles the common Obsidian frontmatter patterns.

---

### Milestone E — "Search my vault" (weeks 9–10)

**Goal:** Tester can search across the entire vault by content and navigate results accessibly.

**User-facing capability**

- Cmd+F (or `/` keybinding TBD) opens a search bar.
- Typing produces incremental results (debounced).
- Results list with file path, line number, and snippet for each match.
- Arrow keys / Tab navigate results; Return opens the result at the matching line.

**Rust work**

- FTS5 virtual table populated alongside the metadata index.
- `full_text_search(query, scope, cancel)` returning `QueryResultSet` (using the result shape from `docs/plans/05` §8.4 — gets us partway toward Bases too).
- Snippet generation (FTS5 has a `snippet()` function we can use).
- Audio-summary string pre-computed: `"Search returned N results"`.

**Swift work**

- Search bar (overlay or toolbar; spike with overlay first since it's more keyboard-friendly).
- Results list with each row accessible as `"<file>, line <N>: <snippet>"`.
- Open-result handler: load the file, scroll to the matching line.

**Schema migrations**

- `005_fts5.sql` — `files_fts` virtual table; trigger to keep it in sync with `files` content.

**Tests**

- Unit (Rust): FTS5 indexing populates correctly on file changes.
- Unit (Rust): search returns expected rows with snippets.
- Unit (Rust): query cancellation works (test with a large synthetic vault).
- Integration: search across a real vault, verify results.

**Accessibility checkpoints**

- Search bar announces focus on open.
- Results count announced when results arrive (`"N results"`).
- Each result reads as `"<file>, line <N>: <snippet around match>"`.
- No focus trap inside the search overlay — Esc closes; Tab cycles to results.

**Tester feedback questions**

- Are results accurate and ranked usefully?
- Is keyboard navigation through results smooth?
- What search options are missing — case sensitivity, regex, scope to folder, scope to tag?

**Definition of done**

- Search works on real tester vaults at the V1 release-gate performance target (10k vault < 100ms).
- The result data shape matches the Bases / query engine `QueryResultSet` from `docs/plans/05` §8 — so V1.x's Bases work doesn't need to retrofit the type.

---

### Milestone F — "Edit a note" (weeks 11–12)

**Goal:** Tester can edit a note's content, save, and have those changes persist correctly. Conflict detection prevents silent overwrites.

**User-facing capability**

- `NSTextView`-backed editor for note content; type to modify.
- Cmd+S saves (autosave on focus loss as a stretch).
- Save announces "Saved" via a live region.
- If the file on disk changed since YANA opened it, the save attempt produces a typed conflict warning rather than silently overwriting.
- Heading navigation, outline panel, properties panel still work in edit mode.
- Backlinks panel reflects the edited content on next save (after reindex).

**Rust work**

- Write path through `FsVaultProvider::write_file` (atomic — temp file + rename).
- Content-hash recomputation on save; reparse + reindex metadata.
- **First op log version**: append-only binary log at `.yana/oplog/<file>.oplog` recording each save as a coarse-grained operation (full text replacement). Per-keystroke op log waits until V1.x once we know the format under real use.
- File-mtime-changed-before-save check returns a typed `VaultError::WriteConflict { current_hash, expected_hash }`.

**Swift work**

- `NoteEditorView` wrapping `NSTextView` via `NSViewRepresentable` (per `docs/plans/05` §5.2).
- Save handling (Cmd+S + autosave on focus loss).
- Conflict UI: when `WriteConflict` is returned, display an accessible "File changed externally" dialog with "Keep mine" / "Reload from disk" / "Cancel" — first taste of the V2 accessible conflict resolution surface, but only at file-level granularity.
- Unsaved-changes indicator (visible + announced).

**Schema additions**

- `.yana/oplog/` directory created on first save.
- No new SQLite tables (op log lives in files, not SQLite).

**Tests**

- Unit (Rust): write path is atomic (no torn writes on simulated failure).
- Unit (Rust): conflict detection fires when file changed externally.
- Unit (Rust): op log entries serialize and round-trip correctly.
- Integration: edit a note, save, close, reopen — content persists.
- Integration: edit, modify externally, save → conflict raised.

**Accessibility checkpoints**

- `NSTextView`'s native VoiceOver behavior works for typical note sizes (verify on real tester documents).
- Save announces "Saved" via `AXAccessibilityAnnouncementNotification`.
- Unsaved-state announced when closing the file or quitting.
- Conflict dialog is fully keyboard-accessible; default action is "Cancel" so accidental Enter doesn't lose data.

**Tester feedback questions**

- Can you edit comfortably with your screen reader?
- Does the read-vs-edit experience feel like one flow or two?
- Anything about save/load behavior that's wrong?
- Conflict dialog — too aggressive, missing options, accessible enough?

**Definition of done**

- Round-trip edit+save works correctly on tester vaults.
- Op log files written and parseable (foundation for V2 conflict resolution).
- No data-loss incidents in tester usage.

---

## Cross-cutting concerns

- **Op log infrastructure starts in Milestone F** at coarse granularity (one entry per save). Fine-grained per-edit operations come in V1.x. Compaction policy from `docs/plans/05` §7.5 is implemented but rarely triggered at this scale.
- **Performance benchmarks** start in Milestone A. A `benches/` directory in `yana-core` runs against synthetic vaults at 1k / 10k / 50k notes at each milestone. By end of F, the V1-release-gate targets from `docs/plans/05` §9.5 must be measurable on the benchmark suite (not the actual release gate yet — that's months 3+).
- **Mobile-friendly API discipline.** Even though only Mac ships in this phase, the Rust API stays paged + opaque-handle-based + cooperatively-cancellable per the locked decisions. No "load whole vault" shortcuts.
- **No sync writer.** All milestones produce an app that's read+edit on local files only. Sync detection (warn if `.obsidian/plugins/obsidian-livesync/` is present, or iCloud Drive markers) ships separately and not in this phase.
- **Tester compensation.** If testers spend real time on builds, they get paid per project principle (`feedback_oss_a11y_contribution.md`). Build it into the budget thinking now, not at first invoice.
- **Vault file safety.** Every file write goes through the atomic temp+rename pattern. Never overwrite directly. Conflict detection in Milestone F is the first safety net; full snapshot history (the "history" workstream from `docs/plans/03`) is V1.x.

---

## Explicitly NOT in months 0–3

- Math, Mermaid, code-block visual rendering (V1.x — see `docs/plans/05` §6.2, §6.3, §6.4).
- Citations and bibliography (V1.x — see §6.5).
- Bases / query builder / saved queries (V1.x — see §8).
- Tier 1 config-based plugins beyond saved-vault paths (V1.x — see §10).
- Sync writer (V2).
- Accessible structured conflict resolution UI beyond file-level (V2 — see §7.3).
- iOS, Windows, Android UI (later platforms — see §3).
- Themes, dark mode polish, command palette as a first-class surface.

---

## Risk register for this phase

| Risk | Likely impact | Mitigation |
|---|---|---|
| Tester feedback invalidates a UI choice mid-phase | Refactor cost on a 2-week slice | 2-week build cadence catches it; each milestone is a feedback opportunity, not a blocking review |
| `NSTextView` accessibility surprises in Milestone F | Could blow up F's timeline | Prototype the `NSViewRepresentable` wrapper in week 10 alongside Milestone E, not from scratch in week 11 |
| File watcher reliability on macOS | Stale UI when external tools modify the vault | Tested in Milestone A; refresh-on-foreground fallback per `docs/plans/05` §9.3 |
| Tree-sitter parse-tree memory at vault scale | OOM on large vaults | Tree-sitter doesn't enter the picture until V1.x (semantic spans). Until then, the parse trees are pulldown-cmark which is lighter |
| SQLite write performance on large initial vault open | Slow first open | Benchmark Milestone A; `parse_workers` in `SessionConfig` already lets us parallelize |
| Tester goes silent | Hard to validate without feedback | Ask why; don't assume; the cohort is small enough to actually check in |
| Op log file format choice in F locks us in too early | Hard to migrate later | Start with a versioned header byte and a length-prefixed record format; explicit version field makes future migration possible |
| Solo developer burnout over 16 weeks | Phase slips, momentum dies | Ship at end of each milestone, no exceptions. Don't compound milestones |

---

## Issue tracking strategy

- **One GitHub Milestone per A–F.** GitHub's milestone feature is fine for solo work and stays inside the repo. Title format: `Milestone A — Vault + file list`.
- **One issue per concrete task.** Labels: `backend`, `swift-ui`, `schema`, `a11y`, `test`, `benchmark`, `tester-feedback`. Cross-cutting labels: `blocked`, `for-tester-review`.
- **Project board** (single board for the whole phase) with columns: `Todo / In progress / Blocked / For tester / Done`. Skip more elaborate tooling.
- **Tester-feedback issues** are filed by the developer based on tester responses, with the tester credited. Public unless the tester wants otherwise.

---

## References

- `docs/plans/05_locked_architecture_decisions.md` — locked stack, API surfaces, schema. This phase implements §4 (API), §6 (content pipelines, partially), §7 (data model, partially), §9 (performance constraints in full).
- `docs/plans/01_detailed_roadmap.md` — phase model. This document is the concrete decomposition of "Phase 1: Accessible vault MVP" in `01` §3.
- `docs/plans/03_phase_1_plan.md` — earlier Phase 1 plan from the Flutter era. Most of this is superseded by `05` and this doc; some workstream sequencing ideas still apply.
- `.claude/projects/-Users-coryj-Dev-yana/memory/project_testers.md` — context on the tester cohort (4 committed AT users, compensated per project principle).

---

**Status: locked at the level of sequence and shape. Per-milestone implementation issues will be filed in GitHub before each milestone begins.**
