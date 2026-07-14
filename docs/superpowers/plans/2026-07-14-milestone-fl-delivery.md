# Milestone FL Complete Delivery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:subagent-driven-development` or `superpowers:executing-plans` to
> implement this plan task-by-task. Use `superpowers:test-driven-development`
> for every behavior change and `superpowers:verification-before-completion`
> before every completion claim.

**Goal:** Deliver every open issue in GitHub Milestone FL as a reliable,
performant, accessible macOS files-sidebar navigator, while preserving the FL2
behavior that has already shipped on `main` and closing every issue through one
clearly identified PR.

**Architecture:** Rust remains authoritative for indexed metadata, filtering,
tag queries, batch content edits, and structural mutations. Swift owns
presentation and device-local view state. A small shared sidebar presentation
layer supplies the same rows, actions, selection behavior, and accessibility
semantics to tree, filter, tag, recent, shortcut, and dual-pane surfaces.
Vault-local authored configuration is stored in `.slate/sidebar.json`; ephemeral
view state stays device-local. Every async result is session/generation gated.

**Tech Stack:** Rust 2024, rusqlite, proptest, Criterion, UniFFI, Swift 6,
SwiftUI/AppKit, XCTest, UserDefaults, GitHub Actions.

## Evidence Snapshot (2026-07-14)

- Governing program: `docs/plans/15_files_sidebar/00_program.md` plus
  `specs/fl0_spec.md` through `specs/fl7_spec.md`.
- GitHub Milestone 31, **Milestone FL — Files sidebar navigator**, contains 21
  open issues (#650–#670), no closed issues, and no issue comments. The local
  program and issue bodies still describe the July 5 clean-slate baseline.
- Implementation baseline is `origin/main` at `6aa9fce`. FL2 is not actually a
  clean slate: main already has pointer multi-selection, batch move/trash,
  Duplicate, Reveal in Finder, Copy Path, Finder file imports, drop highlighting,
  600 ms spring-loading, expansion persistence, and structural undo.
- The newest schema migration is `030_files_birthtime.sql`; `files.birthtime_ms`
  already supplies the filesystem-created timestamp. FL0 must use migration 031
  and must not add a second created-time column.
- `CommandRegistry`, `SlateCommandID`, and `CommandSection` already exist and are
  shared by the menu bar, command palette, and sidebar utility surfaces. The old
  program statement that no registry exists is stale.
- `SettingsView` already exists. FL adds a Sidebar settings tab/section; it does
  not add a second Settings scene.
- `FileRecentsStore` currently writes `.slate/file-recents.json` with 50 entries.
  FL's locked persistence boundary says recents are device-local, so that store
  must migrate rather than coexist with a second sidebar-only recents store.
- The checked-in Graphify snapshot is advisory: 12,472 nodes, 25,585 edges, and
  334 communities. It identifies `FileTreeSidebar.swift` (3,176 lines),
  `AppState.swift`, the command registry, session/FFI, `SettingsView`, and
  `FileRecentsStore` as the primary coupling seams. The report also claims a
  zero-file corpus and its files are locally modified, so use topology only;
  never treat its counts as a correctness gate or modify/stage it in FL work.
- The workspace Apple HIG skill currently contains its manifest only; its
  required `routing-index.md` and `distilled/` corpus are absent. HIG acceptance
  below therefore cites the official Apple guidance directly.

## Contract Reconciliation

These decisions replace stale implementation assumptions without changing the
locked user-facing scope:

1. **Migration 031, no duplicate birthtime.** `file_meta` stores only
   `word_count`, `char_count`, and `preview`; `created_ms` is resolved from the
   existing `files.birthtime_ms`, with frontmatter `created` taking precedence.
2. **One command system.** Append `.sidebar` to the existing cross-language
   `CommandSection` enum and register every FL command through the current
   registry. Menu bar, palette, context menu, toolbar, keyboard, and VoiceOver
   rotor actions call the same AppState action funnels.
3. **FL2 is residual work.** Keep the shipped `<stem> copy` Duplicate naming,
   spring-loading, pointer multi-selection, and current undo behavior unless a
   later PR deliberately upgrades the batch contract. Do not implement parallel
   versions.
4. **One logical batch operation.** Replace the current K independent Swift
   operations with one core request, complete preflight, one consolidated result,
   one refresh/announcement, and one undo group. Because multiple filesystem
   mutations cannot be crash-atomic, the core must journal progress, attempt
   rollback on failure, and report any rollback failure honestly.
5. **One recents history.** Move `FileRecentsStore` to device-local persistence,
   retain its 50-entry depth for Quick Switcher, and show the first 10 eligible
   entries in the sidebar. Migrate the legacy vault-local file once; do not keep
   two diverging histories.
6. **Search is always at the top.** The Filter field sits above Shortcuts,
   Recents, the tree, and Tags. This intentionally corrects FL4's older
   below-sections placement.
7. **Listing mode lands with filtering.** FL4's engine accepts an empty query
   only when a validated scope is present. FL7 consumes that contract instead of
   reopening the filter API late in the milestone.
8. **Folder-note rename is one core operation.** Swift must not sequence a folder
   rename and note rename. The core owns the compound preflight, link rewrite,
   mutation, rollback/error report, and tab-retarget data.
9. **No second Markdown interpretation stack.** FL0 reuses the existing
   frontmatter range and Markdown/source-span machinery wherever available. Any
   preview-specific stripping is isolated in `file_meta_db` and protected by
   fixtures/censuses so it cannot affect the editor parser.

## Architecture and Usability Rules

- Keep `FileTreeSidebar` as an assembly surface. New logic belongs under
  `apps/slate-mac/Sources/SlateMac/Sidebar/` in focused files such as
  `SidebarFileRow.swift`, `SidebarSelectionModel.swift`,
  `SidebarActionCatalog.swift`, `SidebarPreferences.swift`,
  `SidebarFilterView.swift`, `SidebarSectionsView.swift`, and
  `SidebarDualPaneView.swift`.
- Introduce one immutable `SidebarRowModel` and one `SidebarAction` catalog.
  Tree, filter, tag, recent, shortcut, and list-pane rows project through them.
  This is the compile-time seam for FL7's single-tree-equivalence test.
- `AppState` remains the user-action owner. Views emit intent and render state;
  they do not call FFI directly or independently mutate preference files.
- `SidebarVaultPrefsStore` follows `PrefsJsonStore`: bounded reads, exclusive
  cross-writer lock, unknown-key preservation, deterministic JSON, atomic rename,
  corruption fallback plus a visible notice, and a single serialized owner.
- Every async fetch captures the current `VaultSession` and a monotonically
  increasing request generation. Publish only when both still match. Cancellation
  is checked before fetch, after await, and before state publication.
- List ordering is total and deterministic. Locale-aware presentation sort keys
  are built once per row, never by allocating formatters or querying SQLite in a
  Swift row body.
- All new symbols go through `SlateSymbol`; colors/type/spacing go through
  `Tokens`. New text pairings are measured with the existing
  `PresentationReady`/`APCAContrast` harness in Aqua and Dark Aqua at
  `|Lc| > 75`.
- Follow Apple's macOS sidebar, outline, search, focus/selection, menu, and drag
  conventions: leading navigation, succinct section labels, disclosure separate
  from activation, persistent search above a navigable list, active/inactive
  selection distinction, full keyboard/menu parity, copy semantics for external
  drops, multi-item drag, visible drop feedback, and spring-loading.
- Context menus stay concise and selection-relevant. The menu bar and command
  palette remain the complete command inventory.
- Human-facing UAT covers keyboard-only use, VoiceOver, Full Keyboard Access,
  light/dark, Increase Contrast, Reduce Motion, narrow/wide sidebars, empty/error/
  loading states, 10k-note vaults, and external filesystem changes.

## PR Train and Issue Ownership

Only the named **closing PR** uses `Closes #…`; prerequisite PRs use `Refs #…` so
an issue cannot close while residual acceptance criteria remain.

| PR | Scope | Closes | References without closing | Depends on |
|---|---|---|---|---|
| FL-00 | Contract rebaseline | — | all Milestone FL issues | none |
| FL-01 | Metadata pipeline, FFI, censuses | #650 #651 #652 | — | FL-00 |
| FL-02 | Rich rows, settings, preference stores | #653 #654 | — | FL-01 |
| FL-03 | Selection and one-logical-batch contract | #655 | batch part of #656 | FL-00 |
| FL-04 | Action/menu/template completeness | #656 | template part of #657 | FL-03 |
| FL-05 | Multi-item Finder import pipeline | #657 | — | FL-03 |
| FL-06 | Sort, grouping, and pins | #658 #659 | — | FL-02 |
| FL-07 | Shortcuts, recents, and navigation | #660 #661 | — | FL-06 |
| FL-08 | Filter/listing engine | #662 | — | FL-01 |
| FL-09 | Filter UI and shared result list | #663 | — | FL-02 FL-08 |
| FL-10 | Tag tree and batch tag core | #664 | core part of #666 | FL-08 |
| FL-11 | Tags navigation and batch tag UI | #665 #666 | — | FL-03 FL-07 FL-09 FL-10 |
| FL-12 | Folder notes | #667 | — | FL-01 FL-04 |
| FL-13 | Dual-pane container behind internal gate | #668 | — | FL-07 FL-09 FL-11 |
| FL-14 | Dual-pane list, overrides, public setting | #669 | — | FL-06 FL-13 |
| FL-15 | Close-out docs, UAT, performance evidence | #670 | — | all prior PRs |

Publication is a serial merge train rebased on the latest `origin/main`. Work on
disjoint backend branches may be prepared concurrently, but no dependent PR is
published as a stacked PR; each starts from the merged predecessor so CI and
Codoki review the actual merge result.

## Standard Gate for Every PR

Each task below inherits this gate in addition to its targeted tests:

1. Start from a clean branch named `codex/fl-XX-short-name` at current
   `origin/main`; record the exact base SHA in the PR.
2. Add failing unit/integration tests first, run them to prove the intended
   failure, implement the smallest coherent change, and rerun targeted tests.
3. Run `make ci` for any Rust/FFI change. For Swift changes run
   `./scripts/build-mac-app.sh --skip-a11y-check` followed by
   `(cd apps/slate-mac && DYLD_LIBRARY_PATH="$PWD/../../target/debug" swift test)`.
   Run `./scripts/build-mac-app.sh` when the local accessibility checker is
   available. New `.rs`/`.swift` files must pass the SPDX header check.
4. Run the PR's HIG/UAT matrix and record evidence, including the tightest new
   APCA pairings in both appearances. Never mark a human VoiceOver pass unless a
   human actually performed it.
5. Ask an **independent, read-only red-team agent** to review the exact base..tip
   diff against the governing issue/spec, usability, reliability, performance,
   security, and Apple HIG. The reviewer returns actionable findings or
   `APPROVED`; it does not edit the branch.
6. Address every finding, rerun affected and full gates, and return the new diff
   to an independent reviewer. Repeat until the current tip is `APPROVED`.
7. Only then push and open the ready-for-review PR. The PR body includes issue
   ownership, state-machine/rollback notes, automated evidence, manual evidence,
   known limitations, and the red-team approval.
8. Assign an independent monitor agent. At 90-second intervals it checks all
   required GitHub checks plus new reviews, review threads, issue comments, and
   Codoki output. Any failure/comment returns control for diagnosis and fixes.
9. Every fix after publication repeats targeted/full verification and independent
   red-team review before push. Reply to comments with evidence; do not dismiss or
   resolve a thread merely because code changed.
10. Merge only when no required check is pending/failing, all review findings and
    threads are resolved, the current tip is independently approved, and Codoki
    explicitly states the PR is safe to merge. If Codoki is unavailable or quota
    limited, wait; this plan does not inherit the older UI-program exception.
11. Squash-merge, confirm issue closure/milestone state, update local `main`, and
    use that merged SHA as the next PR's base.

## FL-00 — Rebaseline the Governing Contracts

**Files:**

- Modify `docs/plans/15_files_sidebar/00_program.md`
- Modify `docs/plans/15_files_sidebar/specs/fl0_spec.md` through `fl7_spec.md`
- Modify this plan only if red-team review finds an execution ambiguity

**Work:**

- [ ] Replace stale schema, FFI line numbers, Settings, command-registry, and
      FL2 clean-slate assumptions with the evidence snapshot above.
- [ ] Record the grouped-PR exception requested for this delivery and the single
      closing-PR ownership table.
- [ ] Amend FL0 to migration 031 and reuse `files.birthtime_ms`.
- [ ] Amend FL2 to preserve shipped Duplicate/spring-load behavior and enumerate
      only missing keyboard, multi-drag, wikilink, warning, template, and import
      work.
- [ ] Amend FL3 recents to one device-local store with 50 retained/10 displayed.
- [ ] Amend FL4 search placement and scoped empty-query listing mode.
- [ ] Amend FL7 so the public setting remains hidden until its list pane lands.
- [ ] Post a concise rebaseline comment to each GitHub issue after the PR merges;
      do not rewrite historical issue text in a way that hides original scope.

**Verification:** links resolve; issue-to-PR table covers #650–#670 exactly once;
`make ci`; docs link check if available; independent contract red team.

**Commit:** `docs(fl): rebaseline files sidebar delivery contracts`

## FL-01 — Derived Metadata, FFI, Census, and Performance Gate

**Issues:** Close #650, #651, #652.

**Files:**

- Create `crates/slate-core/migrations/031_file_meta.sql`
- Create `crates/slate-core/src/file_meta_db.rs`
- Create `crates/slate-core/src/session/tests/file_meta.rs`
- Modify `crates/slate-core/src/db.rs`, `session.rs`, and session test module list
- Modify `crates/slate-uniffi/src/lib.rs`
- Modify `crates/slate-core/benches/scan_bench.rs` and `BENCHMARKS.md`
- Add/modify Swift FFI smoke tests; generated bindings remain untracked

**Work:**

- [ ] Add `file_meta(file_id PK/FK, word_count, char_count, preview)` and force
      one established slow-path replay for existing vaults.
- [ ] Derive body counts and normalized preview at the scan slow path and save
      path in the same transaction; delete cascades. Preserve O(changed-file).
- [ ] Extend `FileSummary` additively with effective title, resolved created
      timestamp, word count, preview, and task aggregates. Both listing APIs use
      one SQL statement with no per-row queries.
- [ ] Resolve frontmatter date/datetime deterministically and test the chosen
      calendar/UTC display boundary explicitly so a date cannot shift a day.
- [ ] Verify the additive JSON/CLI contract and regenerate bindings locally.
- [ ] Add rescan-parity and random-walk censuses plus 1k/10k/50k scan, 10k root
      listing, and save-path benchmarks.

**Targeted verification:**

- `cargo test -p slate-core file_meta -- --nocapture`
- `cargo test -p slate-uniffi file_summary -- --nocapture`
- `SLATE_CENSUS_FULL=1 cargo test -p slate-core census_file_meta --release -- --nocapture`
- `make bench BENCH_ARGS='list_dir_children_meta/10000'`
- Scan overhead <= 5%, root metadata listing <= 10 ms, and save remains
  O(changed-file), with machine/baseline context recorded.

**Commit:** `feat(sidebar): add derived file metadata read model`

## FL-02 — Shared Rich Rows, Settings, and Preference Ownership

**Issues:** Close #653 and #654.

**Files:**

- Create `apps/slate-mac/Sources/SlateMac/Sidebar/SidebarFileRow.swift`
- Create `apps/slate-mac/Sources/SlateMac/Sidebar/SidebarPreferences.swift`
- Create `apps/slate-mac/Sources/SlateMac/Sidebar/SidebarVaultPrefsStore.swift`
- Modify `FileTreeSidebar.swift`, `SettingsView.swift`, and `AppState.swift`
- Add `SidebarFileRowTests.swift`, `SidebarPreferencesTests.swift`, and
  `SidebarVaultPrefsStoreTests.swift`

**Work:**

- [ ] Extract one row model/view for effective title, truthful created/modified
      labels, preview clamp, task badge, localized word count, standard/compact
      density, filename tooltip, rename filename, and one coherent AX utterance.
- [ ] Cache formatters and precompute row strings outside `body`.
- [ ] Add the Sidebar settings surface to the existing Settings view using
      device-local typed preferences.
- [ ] Add the vault-local store with lock/read-modify-write/unknown-key/atomic
      semantics and AppState ownership; corruption yields defaults plus notice.
- [ ] Keep compact mode visually compact without reducing spoken information.

**Targeted verification:** titled/untitled/date-fallback rows; no date
mislabeling; rename uses filename; preview/badge/compact AX; corrupted/oversized/
unknown-key prefs; formatter-allocation regression; APCA both appearances.

**Commit:** `feat(sidebar): add rich shared rows and preferences`

## FL-03 — Complete Selection and One Logical Batch Operation

**Issues:** Close #655; reference #656.

**Files:**

- Create `apps/slate-mac/Sources/SlateMac/Sidebar/SidebarSelectionModel.swift`
- Create `crates/slate-core/src/structural_batch.rs`
- Modify `session.rs`, `slate-uniffi/src/lib.rs`, `AppState.swift`,
  `FileTreeSidebar.swift`, and `MoveToFolderSheet.swift`
- Extend `FileTreeMultiSelectTests.swift`, `FileTreeDragDropTests.swift`, and core
  structural-operation tests

**Work:**

- [ ] Preserve shipped click semantics while adding Command-A, Shift-arrow range
      extension, stable anchor behavior, Return/Command-Down open-selected, and a
      >=10-item confirmation.
- [ ] Build multi-item drag providers from the current visible selection and
      preserve the selection while dragging.
- [ ] Add one core batch request for move and trash with top-level selection
      pruning, complete preflight, progress journal, deterministic results,
      rollback attempt, one undo group, and session/vault ownership guards.
- [ ] Publish one refresh, announcement, and consolidated error/skip report.
- [ ] Keep actions disabled with an accessible reason while a batch is running;
      prevent double submission and stale completion after vault switch.

**Targeted verification:** pointer/keyboard range matrix, hidden/collapsed rows,
multi-drag payload, open confirmation, collision/subtree preflight, injected
mid-batch failure and rollback, rollback-failure honesty, vault switch, one undo,
one announcement, #643 click regression.

**Commit:** `feat(sidebar): complete selection and batch operations`

## FL-04 — Action Catalog, Menus, Wikilinks, Warnings, and Templates

**Issues:** Close #656; reference #657.

**Files:**

- Create `apps/slate-mac/Sources/SlateMac/Sidebar/SidebarActionCatalog.swift`
- Modify `SlateCommands.swift`, `CommandPaletteModel.swift`, `SlateMacApp.swift`,
  `FileTreeSidebar.swift`, `AppState.swift`, and template picker flow files
- Modify `crates/slate-core/src/commands.rs` and `slate-uniffi/src/lib.rs`
- Extend command registry, file management, sidebar, and template tests

**Work:**

- [ ] Append `.sidebar` cross-language command section and update exhaustive
      conversion/order tests.
- [ ] Define one action catalog with enablement/reason rules. Project it into
      menu bar, palette, concise context menus, toolbar, keyboard, and rotor.
- [ ] Preserve shipped Duplicate/Reveal behavior; make Copy Path vault-relative;
      add resolver-correct Copy Wikilink and one `Copied.` announcement.
- [ ] Add live, polite filename warnings for filesystem-invalid and link-breaking
      characters to rename/new-note/new-folder flows without replacing backend
      commit validation.
- [ ] Reuse the existing template picker/render/name/caret pipeline, injecting
      only the selected destination folder. Empty Templates disables the action
      with an accessible reason.

**Targeted verification:** command/action parity and unique IDs; disabled-not-
hidden rules; ambiguous/unambiguous wikilinks; warning character table; shipped
Duplicate suffix; template destination/title/prompts/caret; empty Templates.

**Commit:** `feat(sidebar): complete actions and template creation`

## FL-05 — Bounded Multi-Item Finder Import

**Issues:** Close #657.

**Files:**

- Create `apps/slate-mac/Sources/SlateMac/Sidebar/SidebarImportCoordinator.swift`
- Modify `FileTreeSidebar.swift` and `AppState.swift`
- Extend `FileTreeDragDropTests.swift` and add `SidebarImportCoordinatorTests.swift`

**Work:**

- [ ] Consume every file-URL provider exactly once; distinguish in-vault moves
      from external copies before mutation.
- [ ] Copy files and directories recursively without following symlink cycles or
      escaping the selected root. Preserve raw bytes and security-scoped access.
- [ ] Use deterministic ` 2`, ` 3`, … collision suffixes, exclusive destination
      creation, per-file/total size guards, cancellation, and bounded concurrency.
- [ ] Aggregate partial failures, refresh affected levels once, select successful
      imports, and announce truthful file/folder counts.
- [ ] Preserve current highlight and 600 ms spring-load/re-collapse behavior.

**Targeted verification:** multi-provider order, binary/non-UTF8, directory tree,
empty directory, collision race, symlink loop/escape, unreadable/oversized entry,
cancel, partial success, vault switch, in-vault move, external copy semantics.

**Commit:** `feat(sidebar): import Finder drops as bounded batches`

## FL-06 — Sorting, Date Groups, and Pins

**Issues:** Close #658 and #659.

**Files:**

- Create `Sidebar/SidebarOrganization.swift` and `Sidebar/SidebarSectionsView.swift`
- Modify `SidebarVaultPrefsStore.swift`, `SidebarFileRow.swift`,
  `FileTreeSidebar.swift`, `AppState.swift`, and commands
- Add `SidebarOrganizationTests.swift` and `SidebarPinsTests.swift`

**Work:**

- [ ] Add total-order name/created/modified sorting with direction, null-last
      rules, stable tie-breaks, and per-folder override precedence.
- [ ] Add injected-clock date buckets as nonselectable AX headers. Grouping
      forces its compatible date sort and never mixes folder/file ordering.
- [ ] Add one nonduplicated Pinned section, pin commands, authored order, and
      mutation-driven rename/move/delete integrity with bounded stale pruning.
- [ ] Re-find selections by stable path after sort/group mutations; never by row
      index. Announce nondefault organization changes.

**Targeted verification:** comparator properties/permutations, locale/diacritic,
null dates, midnight/month/year/timezone boundaries, override precedence,
selection after mutation, pin integrity/prune idempotence, header/row AX.

**Commit:** `feat(sidebar): add organization and pinned notes`

## FL-07 — Shortcuts, One Recents Store, and Navigation History

**Issues:** Close #660 and #661.

**Files:**

- Modify `FileRecentsStore.swift`, `AppState.swift`, `FileTreeSidebar.swift`,
  `SidebarSectionsView.swift`, `SidebarVaultPrefsStore.swift`, and commands
- Extend `FileRecentsStoreTests.swift` and add
  `SidebarShortcutsTests.swift`/`SidebarNavigationTests.swift`

**Work:**

- [ ] Move recents to bounded UserDefaults data keyed by vault identity; migrate
      legacy `.slate/file-recents.json` once after a successful durable write.
      Quick Switcher retains 50; sidebar displays first 10 excluding current.
- [ ] Add discoverable Shortcuts and Recents AX groups above the tree, including
      accessible empty rows, reorder parity, clear recents, and mutation repair.
- [ ] Add focus-scoped shortcut chords without stealing tab chords.
- [ ] Add bounded Collapse All/Expand Loaded and per-window back/forward selection
      history with stable reveal, invalid-entry skipping, and focus-scoped chords.

**Targeted verification:** migration idempotence/corruption/oversize; two-window
recents writes; cap/dedupe/current exclusion; shortcut mutation/reorder/chords;
collapse ancestor preservation; fetch bound; history dedupe/delete/vault switch;
AX headings and announcements.

**Commit:** `feat(sidebar): add shortcuts recents and navigation`

## FL-08 — Deterministic Filter and Scoped Listing Engine

**Issues:** Close #662.

**Files:**

- Create `crates/slate-core/src/sidebar_filter.rs`
- Modify `session.rs`, `slate-uniffi/src/lib.rs`, session tests,
  `scan_bench.rs`, and `BENCHMARKS.md`

**Work:**

- [ ] Implement the locked typed grammar, explicit parse errors, negation, name,
      tag, date, task, extension, and path terms using parameterized SQL only.
- [ ] Accept caller-supplied now/timezone; use deterministic total ordering and
      one statement/page with no N+1 work.
- [ ] Add `scope_dir` listing mode: empty query is valid only with a normalized,
      vault-contained scope. Unscoped empty query remains invalid.
- [ ] Return normative count/audio summary and expose parse detail through FFI.

**Targeted verification:** parser table, injection strings, combinations/
negations, term permutation, timezone/calendar boundaries, pagination stability,
scope normalization/traversal rejection, positive-term subset property, 10k
benchmark <= 50 ms.

**Commit:** `feat(sidebar): add deterministic filter engine`

## FL-09 — Top-Pinned Filter UI and Shared Flat Results

**Issues:** Close #663.

**Files:**

- Create `Sidebar/SidebarFilterModel.swift` and `Sidebar/SidebarFilterView.swift`
- Modify `FileTreeSidebar.swift`, `SidebarFileRow.swift`, action catalog, commands,
  and AppState
- Add `SidebarFilterModelTests.swift` and `SidebarFilterViewTests.swift`

**Work:**

- [ ] Put the filter field at the top of the sidebar with operator insertion,
      Option-Command-F, 200 ms debounce, generation cancellation, and day-rollover
      refresh only for relative terms.
- [ ] Overlay a paged flat list without destroying tree expansion/selection;
      reuse the shared row and action catalog. Include path subtitles.
- [ ] Keep previous good results on parse failure, name the bad term in a polite
      live region, and implement the exact Escape/down-arrow focus state machine.
- [ ] Restore the last query into the field without auto-applying it at launch.

**Targeted verification:** debounce/stale response/vault switch, Esc states,
keyboard entry, retained results/error AX, page append and stable selection,
restore-not-apply, relative rollover, action parity, APCA both appearances.

**Commit:** `feat(sidebar): add accessible top-pinned filtering`

## FL-10 — Tag Tree and Batch Tag Core APIs

**Issues:** Close #664; reference #666.

**Files:**

- Create `crates/slate-core/src/tag_tree.rs`
- Modify tag/property serialization modules, `session.rs`, `slate-uniffi/src/lib.rs`,
  session tests, censuses, benchmarks, and `BENCHMARKS.md`

**Work:**

- [ ] Build deterministic nested tag trees from one query with distinct direct/
      descendant counts, intermediate nodes, untagged count, and honest summary.
- [ ] Add add/remove-tag batch APIs that validate once, conflict-check each file,
      edit frontmatter through the existing serializer, ride normal save/index
      refresh, leave inline tags untouched, and report changed/skipped/remainder.
- [ ] Keep core summaries locale-neutral; UI localizes visual count formatting.
- [ ] Extend metadata/tag parity censuses with tag mutations.

**Targeted verification:** deep/intermediate/permuted tag trees, distinct counts,
untagged, add idempotence, frontmatter preservation/creation, normalized removal,
inline remainder, conflict skip, vault switch, 10k tag tree <= 25 ms.

**Commit:** `feat(sidebar): add tag tree and batch tag APIs`

## FL-11 — Tags Navigation and Batch Tag UI

**Issues:** Close #665 and #666.

**Files:**

- Create `Sidebar/SidebarTagTreeView.swift` and `Sidebar/SidebarTagEditor.swift`
- Modify `SidebarSectionsView.swift`, `SidebarFilterView.swift`,
  `SidebarActionCatalog.swift`, `SidebarVaultPrefsStore.swift`, and AppState
- Add `SidebarTagTreeViewTests.swift` and `SidebarTagEditorTests.swift`

**Work:**

- [ ] Add a lazy, default-collapsed Tags section after the tree with disclosure,
      counts, Untagged, empty state, AX levels, and scoped invalidation refresh.
- [ ] Activate tags through the shared flat result list; populate/edit the filter
      query and support tag shortcuts without a second query path.
- [ ] Add Add Tag/Remove Tag selection actions with autocomplete/free entry,
      selection counts, one result announcement, consolidated skips, and honest
      inline-remainder messaging.
- [ ] Keep tag rename/delete out of scope and document the deferral.

**Targeted verification:** lazy fetch/invalidation, disclosure AX, tag/untagged
activation, shortcut round-trip, add/remove menus for mixed selections, skip and
inline reports, empty/error states, action parity, APCA.

**Commit:** `feat(sidebar): add tag navigation and batch editing`

## FL-12 — Atomic Folder Notes

**Issues:** Close #667.

**Files:**

- Modify `session.rs`, structural operation/link rewrite code, and
  `slate-uniffi/src/lib.rs`
- Modify `FileTreeSidebar.swift`, `SidebarFileRow.swift`, action catalog, and AppState
- Add core folder-note tests and `SidebarFolderNoteTests.swift`

**Work:**

- [ ] Add `has_folder_note` to `DirNodeSummary` via the listing query and hide the
      represented child note row without falsifying child counts.
- [ ] Add create/open/delete actions and distinct label-activation vs disclosure
      behavior. Mirror active editor selection to the folder row.
- [ ] Implement folder+folder-note rename as one core compound operation with
      complete collision/link-rewrite preflight, rollback, one mutation report,
      backlink repair, and tab retargeting.
- [ ] Add a tokenized note-badge overlay and truthful AX value.

**Targeted verification:** exact stem/case/nonmarkdown detection, lazy listing,
hidden row/count honesty, click/Return/chevron behavior, create/delete, collision,
backlinks, injected second-step failure and rollback, tab retarget, external
rename-in/out refresh, badge APCA and AX.

**Commit:** `feat(sidebar): add atomic folder notes`

## FL-13 — Dual-Pane Container Behind an Internal Gate

**Issues:** Close #668.

**Files:**

- Create `Sidebar/SidebarDualPaneView.swift` and `Sidebar/SidebarContainerModel.swift`
- Modify `FileTreeSidebar.swift`, preferences, filter view, tag/section views,
  AppState, and commands
- Add `SidebarDualPaneContainerTests.swift`

**Work:**

- [ ] Add the top/bottom navigation/list container, persisted divider fraction,
      AX-labeled pane groups/divider, and folders-only navigation projection.
- [ ] Define container selection for folder/tag/untagged/shortcut and editor
      mirror state. Filter remains topmost and replaces list contents only.
- [ ] Implement Tab/arrow focus transfer without stealing disclosure keys; mode
      switches preserve tree/filter/selection state and avoid redundant fetches.
- [ ] Keep the user-facing layout toggle hidden/internal until FL-14 supplies the
      complete list pane; default tree mode remains byte-for-byte behaviorally
      equivalent.

**Targeted verification:** internal toggle round trip, folders-only projection,
container matrix, focus escape priority, VO pane announcements, divider storage,
filter scoping, editor mirror, state preservation, zero extra work in tree mode.

**Commit:** `feat(sidebar): add gated dual-pane container`

## FL-14 — Dual-Pane List, Overrides, and Public Setting

**Issues:** Close #669.

**Files:**

- Create `Sidebar/SidebarListPane.swift`
- Modify dual-pane view/model, shared row/action/selection models,
  organization/filter/preferences, Settings, and commands
- Add `SidebarListPaneTests.swift` and `SidebarActionParityTests.swift`

**Work:**

- [ ] Render pinned/grouped/sorted shared rows for folder, descendant, tag,
      untagged, and shortcut containers with paging and path subtitles.
- [ ] Add Include Subfolders and per-container sort/group/preview/density
      overrides in the one vault-local store; single-tree reads the same values.
- [ ] Share multi-selection, drag-to-navigation, keyboard, context, menu, palette,
      rotor, and batch actions. Add compile-time/exhaustive action parity tests.
- [ ] Expose the tree/dual-pane setting only after all equivalence tests pass.
- [ ] Virtualize/page large scopes and cancel stale listings on container/vault
      changes.

**Targeted verification:** full container/content matrix, paging, empty states,
descendant scope, override clear/precedence/two-surface consistency, multi-select
and drag, tree/list action parity, mode switching, 10k filter/list budget, APCA,
VoiceOver/focus walk.

**Commit:** `feat(sidebar): complete dual-pane navigation`

## FL-15 — Milestone Close-Out, Runbook, and Final Audit

**Issues:** Close #670 only after every prior issue is closed.

**Files:**

- Modify `docs/plans/15_files_sidebar/00_program.md`
- Create `docs/plans/15_files_sidebar/runbook.md`
- Create `docs/plans/15_files_sidebar/at_smoke_checklist.md`
- Update `BENCHMARKS.md` and affected user/help documentation

**Work:**

- [ ] Re-audit every locked decision and DoD item against merged code, not issue
      labels or green tests alone.
- [ ] Record exact commands, census seeds/scales, machine context, p50 benchmark
      samples, APCA values, and failure/recovery procedures.
- [ ] Document filter grammar, sort/group/pin/shortcut/tag/folder-note/dual-pane
      behavior, persistence boundaries, import copy semantics, and all chords.
- [ ] Run single-tree vs dual-pane action/setting parity audit and a 10k-vault
      interaction/performance pass.
- [ ] Execute the human AT checklist if a human is available. Otherwise leave it
      explicitly open; do not claim VoiceOver PASS from automated evidence.
- [ ] Confirm GitHub Milestone FL has #650–#670 closed, no unresolved Codoki or
      review threads, and no deferred in-scope acceptance criterion.

**Final verification:** `make ci`; full Swift build/tests; Accessibility Check
100/100; all full censuses in release; recorded benchmarks within FL budgets;
fresh independent whole-milestone red team; published PR monitored every 90
seconds until all checks are green and Codoki says safe to merge.

**Commit:** `docs(fl): close files sidebar milestone`

## Completion Criteria

Milestone FL is complete only when:

- #650–#670 are closed by the mapped merged PRs.
- Every PR tip passed its targeted gates, repository CI, independent red team,
  and explicit Codoki safe-to-merge review before merge.
- No feature exists only in a context menu; menu bar/palette/keyboard/rotor parity
  is verified through the shared action catalog.
- Metadata scan/save remains O(changed-file), filter <= 50 ms at 10k, tag tree
  <= 25 ms at 10k, root metadata listing <= 10 ms, and scan regression <= 5%.
- `.slate/sidebar.json` survives corruption/concurrent writers/unknown keys; all
  device-local state remains outside the vault except the one-time legacy recents
  migration source.
- Tree mode can perform every dual-pane action, and turning dual pane off returns
  the existing experience without lost state.
- Automated accessibility is 100/100 and every new text pairing clears the
  project APCA floor in both appearances; human AT status is reported honestly.
- The Graphify artifacts and the pre-existing Milestone W branch changes remain
  untouched.
