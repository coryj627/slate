# Milestone N Final-Review Fixes Implementation Plan

**Execution status (2026-07-10):** Tasks 1–9 are complete. The final independent
Rust/Swift/contract pass produced the convergence findings below; every
actionable item was reproduced, repaired, and committed through `dacb2b0`, and
all allowed local automated closure gates pass. Remote CI awaits branch
publication, and human VoiceOver remains intentionally open.

> **Execution rule:** implement every production change through a focused
> failing regression, then run the named focused suite before committing. Use
> `superpowers:subagent-driven-development` for the independent Rust, atomic
> builder, and Swift UI lanes, followed by independent review of the combined
> diff.

**Goal:** Close every actionable finding from the whole-branch Milestone N
review without weakening the N0-N4 contracts or claiming the human VoiceOver
gate has run.

**Architecture:** Keep Core's lossless sequential-reparse serializer as the
single edit authority, expose one atomic session/UniFFI batch around it, and
make Swift surfaces projections of stable query/document identities. Preserve
the pinned public duration value while recovering calendar semantics at the
date-arithmetic expression boundary. Native query execution leaves MainActor;
publication returns through generation-checked MainActor methods.

**Tech stack:** Rust 2024, serde/serde_yaml, rusqlite, UniFFI, Swift 6,
SwiftUI/AppKit, XCTest.

## Global constraints

- A batch validation or serialization failure changes neither vault bytes,
  index state, open handles, cache state, nor Swift model baselines. I/O and
  database failures retain the existing `save_text` transaction contract.
- Unknown `.base` keys and untouched regions remain byte-identical.
- Live and saved DQL have identical row membership, including embeds.
- `Value::Duration(i64)` remains the public/evaluator duration representation.
- Preview query open/execute/close never runs on MainActor and stale results
  never publish.
- Post-write refresh remains guarded by session identity, but changing the
  active note cannot suppress refresh of global Bases consumers.
- Selection follows a stable column identifier, never an array offset.
- Keyboard reorder commands are local to the focused row and do not add a
  global chord.
- Genuine Obsidian captures are copied byte-for-byte; provenance stays in a
  sidecar document.
- `docs/plans/17_bases/at_smoke_checklist.md` remains unchecked until a human
  runs it.
- Do not stage or modify the pre-existing Graphify, `.agents`,
  `.github/skills`, `.graphify_*`, or demo-vault pizza-toppings changes.

---

## Task 1: Restore live/saved DQL and evaluator semantic identity

**Files:**

- Modify: `crates/slate-core/src/bases/dql.rs`
- Modify: `crates/slate-core/src/bases/engine.rs`
- Modify: `crates/slate-core/src/bases/eval.rs`
- Modify: `crates/slate-core/src/bases/expr.rs`
- Modify: `crates/slate-core/src/session.rs`
- Modify: `crates/slate-core/tests/bases_dql.rs`
- Modify: `crates/slate-core/tests/bases_engine.rs`
- Modify: `crates/slate-core/tests/bases_eval.rs`
- Modify: `crates/slate-core/tests/bases_expr.rs`
- Modify: `crates/slate-core/src/session/tests/bases.rs`

### Steps

- [x] Add a DQL regression whose vault contains only an embed from `Hub.md` to
  `Target.md`. Assert live `FROM outgoing([[Hub]])` and the result of
  `dql_as_base` + parse + execute both return `Target.md`.
- [x] Run `cargo test -p slate-core --test bases_dql outgoing -- --nocapture`
  and retain the RED result showing the saved path drops the embed.
- [x] Make `Link.linksTo` membership combine the lookup's links and embeds, as
  `QuerySource::Linked` already does. Keep `file.links` and `file.embeds`
  separately addressable; do not broaden `file.links` as a side effect.
- [x] Add an evaluator regression proving both
  `date("2026-01-31") + "1M 1d"` and
  `date("2026-01-31") + duration("1M 1d")` produce 2026-03-01 while standalone
  `duration("1M 1d")` remains the pinned millisecond `Value::Duration`.
- [x] Run the single evaluator regression and retain the RED 2026-03-03 result.
- [x] At the binary date-arithmetic boundary, recognize a literal
  `duration("...")` call before ordinary argument evaluation, parse its
  calendar months and fixed remainder, and apply months first with end-of-month
  clamping. Leave computed/nonliteral duration values on the existing fixed
  millisecond path.
- [x] Add parser goldens for `"\\r"` and other unlisted escapes. Assert the AST
  contains the two literal characters backslash + `r`, while listed escapes
  keep their pinned behavior.
- [x] Remove carriage-return decoding from the expression string escape table.
- [x] Run:

  ```bash
  cargo test -p slate-core --test bases_dql --test bases_eval --test bases_expr --test bases_engine
  cargo test -p slate-core --lib session::tests::bases
  ```

  Expected: all GREEN, including live/saved row equality and calendar-duration
  equality.
- [x] Commit only the Task 1 files with
  `fix(bases): preserve DQL and duration semantics [N0-05 N1-03]`.

---

## Task 2: Complete root flow edits and transient-sort invalidation

**Files:**

- Modify: `crates/slate-core/src/bases/mod.rs`
- Modify: `crates/slate-core/src/session.rs`
- Modify: `crates/slate-core/tests/bases_serialize.rs`
- Modify: `crates/slate-core/src/session/tests/bases.rs`

### Steps

- [x] Add serializer regressions that independently apply `SetFormula`,
  `SetTopLevelFilters`, and `AddView` to the flow-style root
  `{views: [], plugin: keep}` where the formula/filter keys are absent and the
  views collection is empty. Add a second `AddView` case whose flow root omits
  `views` entirely. Assert valid YAML, parsed values, preserved flow-root syntax,
  and unchanged unrelated entries.
- [x] Run the two regressions and retain the RED `InvalidEdit`/missing-span
  evidence.
- [x] Extend root structural discovery/replacement so a root flow mapping can
  replace the whole mapping while preserving the source's flow style and
  unrelated entries. Reparse after each edit as the existing batch serializer
  requires.
- [x] Add a session regression: apply transient sort to view 0, remove or
  reorder that view, add/open the new view 0, and prove the old sort no longer
  affects it. Cover structural column/order edits whose sort key disappears.
- [x] Run the regression and retain the RED stale-sort result.
- [x] Centralize transient-state invalidation after successful structural edits:
  clear on view add/remove/reorder; clear when the edited view's order/sort
  structure can invalidate the transient key; preserve it for unrelated scalar
  edits.
- [x] Run:

  ```bash
  cargo test -p slate-core --test bases_serialize
  cargo test -p slate-core --lib session::tests::bases
  ```

  Expected: all GREEN and lossless serializer censuses remain GREEN.
- [x] Commit only the Task 2 files with
  `fix(bases): handle root flow edits and transient state [N0-01 N3-04]`.

---

## Task 3: Make Save-to-View one atomic session operation

**Files:**

- Modify: `crates/slate-core/src/session.rs`
- Modify: `crates/slate-core/src/session/tests/bases.rs`
- Modify: `crates/slate-uniffi/src/lib.rs`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/AppState+Bases.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseQueryBuilderModel.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BaseQueryBuilderTests.swift`

### Steps

- [x] Add a Core session regression that opens a `.base`, submits an ordered
  batch whose first edit is valid and second edit fails validation, then asserts:
  exact file bytes unchanged, parsed handle unchanged, query result unchanged,
  and a fresh session sees the original file.
- [x] Add a success regression proving dependent edits (for example add formula
  then reference it in order/filter) serialize once, reindex once, and update
  the open handle to the final parse.
- [x] Run the focused session tests and retain the RED partial-write behavior of
  sequential public calls.
- [x] Add `VaultSession::base_apply_edits(handle, Vec<BaseEdit>)`. Validate and
  serialize the entire batch before `save_text`; after one successful write,
  parse once, replace handle source/warnings once, reset cache once, and apply
  Task 2 transient-state invalidation once. Keep `base_apply_edit` as a
  one-element compatibility wrapper.
- [x] Export `base_apply_edits` through UniFFI as `[BaseEdit]` and keep the
  one-edit API for existing callers.
- [x] Add Swift regressions showing Save-to-View calls the batch API once, a
  failed later edit leaves bytes unchanged, and two consecutive successful
  saves do not replay an already-applied `RemoveFormula`.
- [x] Make `basesBuilderSaveToView` submit one batch. On success, rebase the
  model's comparison baseline to the saved draft before announcing or allowing
  another save; on failure, leave both draft and baseline untouched.
- [x] Rebuild/check UniFFI integration and run:

  ```bash
  cargo test -p slate-core --lib session::tests::bases
  cargo test -p slate-uniffi
  make regenerate-bindings
  cd apps/slate-mac && DYLD_LIBRARY_PATH="../../target/debug" swift test --filter BaseQueryBuilderTests
  ```

  Expected: atomic failure and repeated-save tests GREEN.
- [x] Commit only the Task 3 files with
  `fix(bases): apply builder edits atomically [N4-02]`.

---

## Task 4: Correct the typed builder and move preview work off MainActor

**Files:**

- Modify: `apps/slate-mac/Sources/SlateMac/AppState.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseQueryBuilderModel.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/AppState+Bases.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BaseQueryBuilderTests.swift`

### Steps

- [x] Add a table-driven operator regression for every
  `BaseQueryValueKind`. In particular, text does not advertise `matches`; file
  exposes `hasTag`, `hasLink`, and `matches`; operator values produce executable
  receiver/argument families.
- [x] Add builder -> JSON -> engine -> `.base` -> parse -> engine regressions for
  all three file-special operators. Assert identical rows and that reopening
  reconstructs structured rows instead of advanced chips.
- [x] Run the focused tests and retain the RED operator/decode failures.
- [x] Correct `BaseQueryOperator.options(for:)` and use operator-aware typed
  operand decoding: text values for `hasTag` and file-search `matches`,
  link/file values for `hasLink`, and the existing typed decoder for the
  remaining methods.
- [x] Add a deterministic preview executor seam that records whether native
  `openQuery`, `baseExecute`, and `closeBase` execute on MainActor. Add a
  cancellation/generation test where an older parked preview finishes after a
  newer preview and cannot publish.
- [x] Run the tests and retain the RED MainActor execution evidence.
- [x] Keep debounce/model state on MainActor, but run the synchronous native
  query lifecycle in `Task.detached` (or an equivalent `nonisolated` executor).
  Always close an opened handle; propagate only a value/error back to the
  generation-checked publisher; cancel both the Swift task and native token
  when superseded.
- [x] Run:

  ```bash
  cd apps/slate-mac && DYLD_LIBRARY_PATH="../../target/debug" swift test --filter BaseQueryBuilderTests
  ```

  Expected: operator round trips, off-main execution, cancellation, and stale
  publication coverage all GREEN.
- [x] Commit only the Task 4 files with
  `fix(bases): harden typed builder preview [N4-02]`.

---

## Task 5: Refresh every live Bases consumer after writes and saved-query edits

**Files:**

- Modify: `apps/slate-mac/Sources/SlateMac/AppState.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/AppState+Bases.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseEmbedDocument.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseEmbedView.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/DashboardDocument.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BasesTabRoutingTests.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BaseEmbedTests.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BaseQueryBuilderTests.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BaseQueriesPanelTests.swift`

### Steps

- [x] Extend the existing publish-gate race tests: park a successful note save
  and property edit after the native write, switch active notes within the same
  session, release, and assert open Base tabs, dashboards, docked queries, and
  visible embeds refresh while active-note fields do not publish stale state.
- [x] Run the focused routing/embed tests and retain the RED early-return
  evidence.
- [x] After the session-identity guard and a successful outcome, run the global
  Bases refresh before any `loadedFilePath == path` guard. Keep note-specific
  hash, editor, links, properties, and panel publication behind the active-note
  guard. Do not refresh on write conflict/failure or after vault replacement.
- [x] Give visible `BaseEmbedDocument` instances the same weak/leased registry
  lifecycle as other Base consumers; register on appearance/acquisition and
  release on disappearance/owner teardown. Refresh only documents belonging to
  the current session.
- [x] Add a saved-query update regression with the same query ID simultaneously
  open in a tab, dashboard section, dock, and visible embed. Assert all consumers
  reopen/refresh and show the new AST result.
- [x] Replace the single-key `refreshOpenSavedQueryDocument` path with registry
  traversal by saved-query ID across tabs, dashboard sections, docks, and embed
  documents. Keep owner errors localized rather than discarding the persisted
  saved query.
- [x] Run:

  ```bash
  cd apps/slate-mac && DYLD_LIBRARY_PATH="../../target/debug" swift test \
    --filter BasesTabRoutingTests \
    --filter BaseEmbedTests \
    --filter BaseQueryBuilderTests \
    --filter BaseQueriesPanelTests
  ```

  Expected: same-session navigation, replacement-session suppression, visible
  embed refresh, and multi-consumer saved-query update tests GREEN.
- [x] Commit only the Task 5 files with
  `fix(bases): refresh all live query consumers [N3-07 N4-04]`.

---

## Task 6: Stabilize grid selection and complete keyboard/heading contracts

**Files:**

- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseContainerView.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/BaseQueryBuilderSheet.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/DashboardViews.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/DashboardEditorSheet.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/AppState+Bases.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BasesTabRoutingTests.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BaseQueryBuilderTests.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/BaseQueriesPanelTests.swift`
- Modify: `apps/slate-mac/Tests/SlateMacTests/RightPaneViewTests.swift`

### Steps

- [x] Add a grid regression that selects property B, receives a result whose
  columns reorder B and A, then invokes Return/F2. Assert the editor targets B,
  not the new column at B's former index. Add a disappearing-column case that
  safely clears or clamps selection.
- [x] Store/derive selection using row identity plus the stable column ID. At
  each render boundary, translate the ID to the current grid index; never retain
  an index across result changes.
- [x] Add keyboard-command tests that focus each of: a builder sort row, builder
  included-column row, and dashboard section. Dispatch Option-Up/Down and assert
  exactly one local reorder, correct boundary behavior, retained focus, and an
  accessibility announcement.
- [x] Add focused-row keyboard handlers/commands reusing the existing row move
  actions. Buttons remain available; no drag interaction or global command is
  added. Accept Option-only and pass Control-Option through so VoiceOver Quick
  Nav remains intact.
- [x] Add source/AX structure tests requiring dashboard title H1 and section
  title H2 in addition to `.isHeader`.
- [x] Apply `.accessibilityHeading(.h1)` to the dashboard title and
  `.accessibilityHeading(.h2)` to section headings on every normal, empty, and
  missing-section path.
- [x] Run:

  ```bash
  cd apps/slate-mac && DYLD_LIBRARY_PATH="../../target/debug" swift test \
    --filter BasesTabRoutingTests \
    --filter BaseQueryBuilderTests \
    --filter BaseQueriesPanelTests \
    --filter RightPaneViewTests
  a11y-check apps/slate-mac/Sources/SlateMac
  ```

  Expected: selection follows column identity, keyboard tests GREEN, heading
  hierarchy present, and a11y-check remains 100/100.
- [x] Commit only the Task 6 files with
  `fix(bases): complete grid and dashboard accessibility [N3-02 N4-02 N4-04]`.

---

## Task 7: Satisfy the genuine Obsidian corpus gate and correct the spec

**Files:**

- Modify: `docs/plans/17_bases/specs/n4_spec.md`
- Create: `crates/slate-core/tests/fixtures/bases/obsidian/`
- Create: `crates/slate-core/tests/fixtures/bases/obsidian/PROVENANCE.md`
- Modify: `crates/slate-core/tests/bases_parse.rs`
- Modify: `crates/slate-core/tests/bases_serialize.rs`
- Modify: `docs/plans/17_bases/specs/gap_analysis.md`

### Steps

- [x] Amend N4 rule 1's Recently edited canonical form from `>` to the shipped,
  inclusive `>=`, matching N2 export and the governing inclusive cutoff.
- [x] Create a temporary vault outside the repository, launch the installed
  Obsidian application against it, and use Obsidian's Bases UI to create at
  least two representative `.base` files: one basic table/filter/sort file and
  one formulas/properties/multiple-view file. Do not hand-edit the captures.
- [x] Record the Obsidian version, OS, UTC capture timestamp, exact UI steps,
  source temp-vault relative paths, and SHA-256 for every raw capture in
  `PROVENANCE.md`.
- [x] Copy the raw files byte-for-byte into the fixture directory. Verify each
  committed SHA-256 matches the source capture before removing the temporary
  vault.
- [x] Add parse/open/execute assertions appropriate to the captures and include
  them in the no-edit byte-equality and generated serializer corpus gates. Tests
  must read the raw bytes and never normalize line endings or whitespace.
- [x] Correct the gap-analysis language so genuine captures are a satisfied hard
  gate, not “as available.”
- [x] Run:

  ```bash
  shasum -a 256 crates/slate-core/tests/fixtures/bases/obsidian/*.base
  cargo test -p slate-core --test bases_parse --test bases_serialize
  ```

  Expected: hashes equal `PROVENANCE.md`; all raw-corpus gates GREEN.
- [x] Commit only the Task 7 files with
  `test(bases): add genuine Obsidian corpus [N4-05]`.

---

## Task 8: Re-review, run closure gates, and repair close-out evidence

**Files:**

- Modify: `docs/plans/17_bases/milestone_n_audit_2026-07-09.md`
- Modify: `docs/superpowers/plans/2026-07-09-milestone-n-remediation.md`
- Modify: `docs/superpowers/plans/2026-07-09-milestone-n-final-review-fixes.md`
- Modify other Milestone N close-out/evidence documents only where current
  statements are contradicted by the final review or final verification.

### Steps

- [x] Dispatch three independent read-only reviews of the combined branch:
  Rust semantics/session atomicity, Swift concurrency/consumer refresh, and
  N0-N4 contract/evidence completeness. Require executable counterexamples and
  exact file:line evidence; accept no speculative findings.
- [x] For every actionable finding, add a RED regression, fix it, run its focused
  suite, and request re-review. Repeat until all three reviewers approve with no
  HIGH/MEDIUM findings.
- [x] Run Rust gates:

  ```bash
  cargo fmt --check
  cargo clippy -p slate-core -p slate-cli -p slate-uniffi --all-targets -- -D warnings
  cargo test -p slate-core -p slate-cli -p slate-uniffi
  ```

  Final local result: strict Clippy passed for all three packages. The allowed
  Core rerun passed after filtering only four Finder/trash tests whose isolated
  probes failed in the macOS sandbox with AppleScript connection-invalid /
  Finder `-1728`; CLI and UniFFI targets passed. The audit records the exact
  exclusions and does not count them as product failures or verified passes.

- [x] Run Swift and accessibility gates:

  ```bash
  cd apps/slate-mac && DYLD_LIBRARY_PATH="../../target/debug" swift test
  cd ../.. && a11y-check apps/slate-mac/Sources/SlateMac
  ```

- [x] Re-run the serializer/DQL/engine/session/scanner generated censuses named
  in the original remediation evidence. Performance benchmarks need not be
  repeated unless a changed hot path invalidates the recorded matched p50
  comparison; if it does, record fresh p50 evidence.
- [x] Classify any environmental failure precisely. Do not call a blocked
  Finder/trash or sandbox-only test a product failure, and do not call it
  verified without a successful allowed rerun.
- [x] Update the audit and both plans: remove the premature “all automatable
  findings complete” claim until the gates above pass, list the final-review
  findings and evidence commits, and leave the human VoiceOver checklist as the
  only manual blocker only if that statement is then true.
- [x] Review `git diff --check`, `git status --short`, and the exact staged file
  list. Stage no user-owned/unrelated files.
- [x] Commit the evidence updates with
  `docs(bases): close final Milestone N review gaps`.

## Task 9: Close final convergence findings

**Files:**

- Modify: `crates/slate-core/src/{db.rs,dql_inline_fields_db.rs,properties_db.rs,session.rs,tags_db.rs}`
- Create: `crates/slate-core/migrations/026_reindex_typed_property_lists.sql`
- Modify: `crates/slate-core/src/bases/{dql.rs,engine.rs,eval.rs,mod.rs}`
- Create: `crates/slate-core/src/bases/slate_query_fence.rs`
- Create: `crates/slate-core/tests/bases_slate_query_fence.rs`
- Modify: `crates/slate-core/src/session/tests/bases.rs`
- Modify: `crates/slate-core/tests/{bases_dql.rs,bases_engine.rs}`
- Modify: `crates/slate-uniffi/src/lib.rs`
- Regenerate: `apps/slate-mac/Sources/SlateMac/slate_uniffi.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/AccessibleDataGrid.swift`
- Create: `apps/slate-mac/Sources/SlateMac/Bases/BaseExactIdentity.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/Bases/*.swift`
- Modify: `apps/slate-mac/Sources/SlateMac/{NoteContentView.swift,Reading/ReadingBlockSource.swift,Reading/ReadingView.swift,Workspace/WorkspaceModel.swift,Workspace/WorkspaceState.swift}`
- Modify focused `SlateMacTests` files and contradicted Milestone N evidence/specs.

### Additional convergence findings discovered during execution

- Exact UTF-8 identity must survive builder inventories/diffs, formula and
  column references, saved-query/embed lookup, Base workspace paths, and
  row/cell/sort state. Base-specific identity is byte-exact; existing Markdown
  and Canvas registries retain their native canonical-string behavior.
- `slate-query` routing belongs to Core's full YAML parser, and the Swift fence
  bridge must preserve the complete authored interior, including the boundary
  newline needed by YAML block-scalar chomping.
- Base commands in a split pane route only through the active tab; Base rename
  and retarget operations use exact path identity and cannot activate a
  canonically equivalent sibling.
- Builder previews and dashboard sections must surface engine `view_error`
  states instead of presenting empty success, and a failed atomic dashboard
  update must retain the user's editor draft for correction/retry.
- A `.base` created after the initial scan must receive targeted indexing when
  opened so its own path can satisfy `this` without a whole-vault rescan; the
  scanner/query hot path must avoid repeated statement and query preparation.
- DQL command sorting must remain deterministic even when Dataview comparison
  is not a total comparator (NaN, equal-casual structured durations, or nested
  inconsistent values). Only command ordering receives the total fallback;
  expression comparison semantics stay unchanged.

### Steps

- [x] Add storage + session RED regressions proving scalar/list frontmatter
  wikilinks resolve relative to their owning note and list Date/Datetime/Link
  elements retain type through SQLite.
- [x] Store typed list elements losslessly, force one safe reindex through
  migration 026, and prove old caches cannot retain erased list types.
- [x] Add RED DQL command-sort and duplicate-outlink regressions; route command
  sort through the isolated DQL comparator, total-fallback inconsistent pairs,
  and first-occurrence-deduplicate outgoing page identity.
- [x] Add RED cache-retention coverage; evict obsolete generations and impose a
  small per-handle LRU bound for same-generation query/`this` variants.
- [x] Expose the engine's exact native value sort key through Core/UniFFI;
  engine-backed grids must not locally re-sort, while local-only preview and
  dashboard grids use that exact key after typed primitives.
- [x] Store cell and sort selection by stable column ID on every Base surface,
  clear disappeared identities, preserve active view by name across reload,
  and clear embedded quick filters on view switch/Escape.
- [x] Make embed recovery truthful for file, saved-query, inline, and DQL
  forms; omit dead row-only edit actions and announce unavailable commands.
- [x] Make dashboard view override an executable Default/Table/List renderer
  choice; legacy invalid values fail visibly instead of silently selecting the
  synthetic saved-query view.
- [x] Visibility-gate heavy embedded query execution without hiding the
  reading structure from VoiceOver; cover editor and reading surfaces.
- [x] Add a post-scan `.base`/`this` regression and targeted open-time indexing;
  precompile Base queries and reuse static scanner SQL without changing fresh
  versus replacement transaction semantics.
- [x] Re-run focused RED→GREEN suites, regenerate bindings, independently
  re-review all three lanes, then repeat the complete allowed Core/CLI/UniFFI,
  Swift, a11y, full-census, raw-hash, and final hot-path benchmark gates.
- [x] Update the audit, help, specs, benchmarks, and both implementation plans;
  leave `at_smoke_checklist.md` untouched.

## Final acceptance

- Every final-review counterexample has a focused regression that was observed
  RED before GREEN.
- Atomic batch validation failure leaves exact vault bytes and every in-memory
  projection unchanged.
- Live/saved DQL, calendar duration, and escape semantics match their specs.
- Preview native work is proven off MainActor and stale-safe.
- All live consumers refresh by stable identity; grid editing follows stable
  column identity.
- Option-arrow reorder and H1/H2 dashboard semantics are automated.
- Genuine Obsidian captures have raw hashes and sidecar provenance.
- Independent reviewers approve; all allowed automated gates pass.
- Remote CI remains pending until the branch is published.
- Human VoiceOver status remains honestly open.
