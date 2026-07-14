# FL2 executable spec — Selection & operations: multi-select, batch ops, menu completeness, templates, Finder drop

Issues: FL2-1 ([#655](https://github.com/coryj627/slate/issues/655)) · FL2-2 ([#656](https://github.com/coryj627/slate/issues/656)) · FL2-3 ([#657](https://github.com/coryj627/slate/issues/657)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). Grouped delivery: FL-03 closes #655 and references the batch part of #656; FL-04 closes #656 and references the template part of #657; FL-05 closes #657.
Program: [00_program.md](../00_program.md) (locked decisions 11, 13–15; DoD §FL-A). U §A–§G apply.

**Execution order: FL-03 residual selection + one logical batch → FL-04 action/template completion; FL-05 bounded multi-import follows FL-03.**

Baseline facts (verified 2026-07-14 at `origin/main` `6aa9fce`):

- Pointer multi-selection has shipped: `FileTreeSidebar` keeps a focused `listSelection`, a `multiSelection` set, a stable anchor/snapshot, range and toggle click handling, top-level batch-target pruning, one count announcement, and batch Move/Trash context actions. Preserve the #643 primary-click behavior. Missing FL work is Command-A, Shift-arrow range extension, Return/Command-Down multi-open confirmation, and multi-item drag payloads.
- Batch Move/Trash currently loop K independent AppState/core operations. They consolidate some UI output but produce K mutation/undo units and cannot provide complete preflight, rollback, or one logical result. FL-03 replaces that implementation with one core request; it does not add a parallel batch path.
- Duplicate already ships through context menu, File menu, palette, and rotor with exclusive-create `<stem> copy`, then `<stem> copy 2`, … naming. Reveal in Finder and vault-relative Copy Path also ship. Structural move/rename undo, pointer drag/drop, drop highlighting, 600 ms spring-loading/re-collapse, and expansion persistence are present and must not regress.
- File-URL drops already distinguish in-vault moves from external imports and can import a Finder file. FL-05 adds the missing all-provider, file-and-directory, bounded/cancellable multi-import pipeline while preserving current feedback and spring-loading.
- Templates already have `list_templates`, `render_template`, and an app-side picker/render/name/caret flow. FL-04 injects only the selected destination folder; it must not fork the template engine.
- Mutations continue through AppState action funnels and `TreeMutation` publication. Exact July 5 line references are obsolete; implementation follows the named seams.

---

## FL2-1 · Residual selection and multi-drag (#655) — closing PR FL-03

1. Extract the shipped focus/set/anchor behavior into `SidebarSelectionModel` without changing pointer semantics. Add Shift-arrow range extension and Command-A over the flattened visible rows; hidden/collapsed rows are not selected.
2. **Open semantics:** modifier/multi selection never auto-opens. Return/Command-Down opens the selected files and requires confirmation at 10 or more items.
3. **Multi-drag:** dragging a selected row carries the whole path-validated visible selection; dragging an unselected row carries only that row. Preserve the selection while dragging.
4. Preserve the existing count announcement and programmatic single-select reveal. A reveal collapses the selection to the revealed path; reused/stale row IDs remain fail-closed.
5. Mixed directory+file selections remain legal; capability gating belongs to the shared action catalog.

Tests: shipped pointer click/range/toggle matrix including #643; Shift-arrow; Command-A with collapsed rows; no-auto-open; 10-item confirmation; multi-drag payload; reveal and stale-ID fail-closed behavior.

- [ ] Extract the shipped selection model; add keyboard parity
- [ ] Add multi-item drag payloads
- [ ] Preserve announcements, #643 behavior, and path validation
- [ ] Tests; a11y 100/100 on tip

## FL2-2a · One logical batch contract (#656 prerequisite) — FL-03 (`Refs #656`)

Move and Trash become one core request per user action:

1. Prune descendants of selected folders, validate session/vault ownership, and complete collision/subtree/permission preflight before the first mutation.
2. Journal progress, apply deterministic operations, attempt rollback on failure, and return one consolidated result. If rollback is incomplete, name every unrecovered path honestly; never claim atomicity the filesystem cannot provide.
3. AppState publishes one refresh, one announcement, one skip/error report, and one undo group. While running, affected actions are disabled with an accessible reason; double submission and stale completion after a vault switch are rejected.
4. Preserve the shipped confirmation threshold for destructive non-empty-folder batches and tab error/retarget semantics.

Tests: top-level pruning; complete preflight; injected mid-batch failure and rollback; rollback-failure honesty; vault switch; double submission; one undo, refresh, and announcement.

- [ ] Core batch request/report + progress journal and rollback
- [ ] AppState single-result funnel + disabled reasons
- [ ] Move/Trash regression and failure-path tests

## FL2-2b · Shared action/menu completion (#656 remainder) — closing PR FL-04

1. Append `.sidebar` to the existing cross-language `CommandSection`, add exhaustive conversion/order tests, and define one `SidebarAction` catalog. Menu bar, palette, concise context menus, toolbar, keyboard, and rotor project that catalog through the same AppState action funnels.
2. Preserve shipped Duplicate exactly: files only, exclusive-create `<stem> copy`, `<stem> copy 2`, … naming, outgoing links unchanged, one announcement, copied row selected. Preserve shipped Reveal in Finder and vault-relative Copy Path.
3. Add resolver-correct **Copy Wikilink**: `[[stem]]` only when unambiguous under the live resolver; otherwise use the vault-relative path without `.md`. Copy Path and Copy Wikilink announce one `"Copied."`.
4. Add live polite warnings for filesystem-invalid and link-breaking characters to rename, new-note, and new-folder flows. Warnings do not replace backend commit validation.
5. Multi-selection exposes only applicable shared actions; unavailable actions remain discoverable with an accessible disabled reason.

Tests: command/action parity and unique IDs; exhaustive `CommandSection.sidebar` mapping; disabled reasons; shipped Duplicate suffix; Reveal/Copy Path regression; both wikilink branches; warning character table.

- [ ] Existing registry `.sidebar` section + shared action catalog
- [ ] Preserve shipped actions; add Copy Wikilink and warnings
- [ ] Tests; a11y 100/100 on tip

## FL2-3a · Template creation (#657 prerequisite) — FL-04 (`Refs #657`)

**New Note from Template…** is available from folder actions and the complete command inventory. Reuse the existing picker/render/name/caret pipeline and inject only the selected destination folder. Empty Templates disables the action with an accessible reason. Test destination, title/prompts, caret, and empty state.

## FL2-3b · Bounded multi-item Finder import (#657 remainder) — closing PR FL-05

1. Consume every file-URL provider exactly once and preserve provider order. Classify in-vault URLs as the shipped undoable move before mutation; external URLs copy into the selected vault folder.
2. Copy files and directories recursively without following symlink cycles or escaping the selected root. Preserve raw bytes and security-scoped access.
3. Use exclusive destination creation with deterministic ` 2`, ` 3`, … collision suffixes, per-entry and total size guards, cancellation, and bounded concurrency.
4. Aggregate partial failures, refresh affected levels once, select successful imports, and announce truthful file/folder counts. Preserve shipped drop highlighting and 600 ms spring-load/re-collapse behavior.

Tests: multiple providers; binary/non-UTF8 file; directory and empty directory; collision race; symlink loop/escape; unreadable/oversized entry; cancel; partial success; vault switch; in-vault move; external-copy semantics; spring-load regression.

- [ ] All-provider bounded import coordinator
- [ ] Recursive safe copy, collision, cancellation, and aggregation
- [ ] Tests; a11y 100/100 on tip
