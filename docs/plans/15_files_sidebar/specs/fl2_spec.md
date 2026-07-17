# FL2 executable spec — Selection & operations: multi-select, batch ops, menu completeness, templates, Finder drop

Issues: FL2-1 ([#655](https://github.com/coryj627/slate/issues/655)) · FL2-2 ([#656](https://github.com/coryj627/slate/issues/656)) · FL2-3 ([#657](https://github.com/coryj627/slate/issues/657)). Milestone: [GH 31](https://github.com/coryj627/slate/milestone/31). Grouped delivery: FL-03 closes #655 and references the batch part of #656; FL-04-A delivers Tasks 1–3 and references #656 without closing it; FL-04-B starts from refreshed `main` after FL-04-A merges, closes #656, and references only the template prerequisite of #657; FL-05 depends on FL-04-B and closes #657.
Program: [00_program.md](../00_program.md) (locked decisions 11, 13–15; DoD §FL-A). U §A–§G apply.

**Execution order: FL-03 residual selection + one logical batch → FL-04-A action/catalog completion → FL-04-B template lifecycle/integration → FL-05 bounded multi-import.** FL-04-B starts only after FL-04-A merges. FL-05 depends on FL-04-B because FL-04-B owns the template acceptance criteria for #657 and only FL-05 closes the issue.

Baseline facts (verified 2026-07-14 at `origin/main` `6aa9fce`):

- Pointer multi-selection has shipped: `FileTreeSidebar` keeps a focused `listSelection`, a `multiSelection` set, a stable anchor/snapshot, range and toggle click handling, top-level batch-target pruning, one count announcement, and batch Move/Trash context actions. Preserve the #643 primary-click behavior. Missing FL work is Command-A, Shift-arrow range extension, Return/Command-Down multi-open confirmation, and multi-item drag payloads.
- Batch Move/Trash currently loop K independent AppState/core operations. Move produces K structural undo entries. Trash calls `delete_file`/`delete_folder`, which return no restore receipt, send bytes to the system Trash, and are explicitly rejected by `undo_structural`; it is non-undoable inside Slate. FL-03 replaces the Swift loop with one operation-specific core request and result, not a parallel batch path.
- Duplicate already ships through context menu, File menu, palette, and rotor with exclusive-create `<stem> copy`, then `<stem> copy 2`, … naming. Reveal in Finder and vault-relative Copy Path also ship. Structural move/rename undo, non-undoable system Trash, pointer drag/drop, drop highlighting, 600 ms spring-loading/re-collapse, and expansion persistence are present and must not regress.
- File-URL drops already distinguish in-vault moves from external imports and can import a Finder file. FL-05 adds the missing all-provider, file-and-directory, bounded/cancellable multi-import pipeline while preserving current feedback and spring-loading.
- Templates already have `list_templates`, `render_template`, and an app-side picker/render/name/caret flow. FL-04-A preserves the shipped root-only command intent without owning template availability or execution. FL-04-B injects only the selected destination folder; it must not fork the template engine.
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

Move and Trash each become one logical core request per user action, with a shared preflight/result envelope but different recovery semantics:

1. Prune descendants of selected folders, validate session/vault ownership, and complete collision/subtree/permission preflight before the first mutation.
2. **Move:** journal progress, apply deterministic operations, attempt rollback on runtime failure, and name every unrecovered path if rollback is incomplete. A successful batch Move records one structural undo group.
3. **Trash:** apply the existing system-Trash primitive in deterministic order after complete preflight. It remains non-undoable in Slate and does not attempt rollback because the primitive returns no restore receipt. If a runtime failure occurs after earlier items reached Trash, the consolidated result names every trashed and untrashed/failed path; never describe the result as rolled back or atomic.
4. AppState publishes one refresh, one announcement, and one consolidated skip/error report. It registers one undo group only for Move and none for Trash. While running, affected actions that are otherwise relevant to the current surface are disabled with an accessible reason; double submission and stale completion after a vault switch are rejected.
5. Preserve the shipped confirmation threshold for destructive non-empty-folder batches and tab error/retarget semantics. Trash labels, confirmations, announcements, menus, and palette help may say system Trash but must never promise Slate undo, ⌘Z, or rollback.

Tests: top-level pruning; complete preflight for both operations; Move mid-batch failure/rollback and rollback-failure honesty; one Move undo group; Trash success with no undo entry; injected Trash failure after one success with exact trashed/untrashed paths and no rollback claim; vault switch; double submission; one refresh/announcement/result; UI-copy scan proving no Trash undo promise.

- [ ] Core batch requests/report + Move progress journal/rollback/one undo
- [ ] Non-undoable Trash partial-result contract with no restore/rollback fiction
- [ ] AppState single-result funnel + disabled reasons
- [ ] Move/Trash regression and failure-path tests

## FL2-2b · Shared action/menu completion (#656 prerequisite) — FL-04-A (`Refs #656`)

1. Append `.sidebar` to the existing cross-language `CommandSection`, add exhaustive conversion/order tests, and define one `SidebarAction` catalog. The catalog owns verb identity, capability, enablement, disabled reason, and the shared AppState action funnel; surfaces own projection. Menu bar and palette retain a stable full command inventory and disable each unavailable command with one deterministic accessible reason. Context menus and the VoiceOver rotor omit every unavailable verb and stay concise, including actions temporarily unavailable because they are loading, busy, or failed. Toolbar and keyboard paths invoke the same applicable catalog entries. The Move-to-Trash action metadata is explicitly non-undoable and no projected hint/copy may advertise ⌘Z or app rollback.
2. Preserve shipped Duplicate exactly: files only, exclusive-create `<stem> copy`, `<stem> copy 2`, … naming, outgoing links unchanged, one announcement, copied row selected. Preserve shipped Reveal in Finder and vault-relative Copy Path.
3. Add resolver-correct **Copy Wikilink**: `[[stem]]` only when unambiguous under the live resolver; otherwise use the vault-relative path without `.md`. Copy Path and Copy Wikilink announce one `"Copied."`.
4. Add live polite warnings for filesystem-invalid and link-breaking characters to rename, new-note, and new-folder flows. Warnings do not replace backend commit validation.
5. Multi-selection uses the catalog's same capability evaluation on every surface. Every unavailable action (for example, Duplicate on a mixed file/folder selection, or any action temporarily blocked by loading, busy, or failed state) is omitted from context menus and the rotor but remains in the menu bar/palette's full inventory as disabled with the same deterministic accessible reason. Temporary unavailability never creates a second verb or action path.
6. Preserve the shipped root-only **New Note from Template…** intent for menu bar, palette, toolbar, and keyboard by freezing an empty/root selection snapshot. Omit Template from context menus and VoiceOver in FL-04-A. Template availability, frozen folder destinations, picker/render/name/caret ownership, and cancellation belong exclusively to FL-04-B.

Tests: command/action parity and unique IDs; exhaustive `CommandSection.sidebar` mapping; surface projection matrix (context/rotor omission of structural and temporary unavailability, concise ordering, menu bar/palette stable disabled inventory with identical deterministic reasons); root-only Template projection for menu/palette/toolbar/keyboard with context/VoiceOver omission; shipped Duplicate suffix; Reveal/Copy Path regression; both wikilink branches; warning character table. Review the interaction against Apple's [Menus](https://developer.apple.com/design/human-interface-guidelines/menus), [Drag and drop](https://developer.apple.com/design/human-interface-guidelines/drag-and-drop), and [Focus and selection](https://developer.apple.com/design/human-interface-guidelines/focus-and-selection) guidance.

- [ ] Existing registry `.sidebar` section + shared action catalog
- [ ] Preserve shipped actions; add Copy Wikilink and warnings
- [ ] Tests; a11y 100/100 on tip

## FL2-3a · Template creation (#657 prerequisite) — FL-04-B (`Closes #656`; `Refs #657`)

Start from refreshed `main` only after FL-04-A merges. **New Note from Template…** is available from folder actions and the complete command inventory. Reuse the existing picker/render/name/caret pipeline and inject only the selected destination folder. Empty, loading, busy, and failed Templates are temporary availability states: folder context and VoiceOver omit the unavailable action, while the menu bar, palette, and toolbar retain it disabled with the same deterministic accessible reason. The keyboard path uses the same evaluation: an available invocation preserves the frozen folder destination; an unavailable invocation opens no picker, performs no mutation, and announces that reason once. Test destination, title/prompts, caret, cancellation/stale-session behavior, and context, VoiceOver, menu bar, palette, toolbar, and keyboard projections across available and every temporary availability state.

## FL2-3b · Bounded multi-item Finder import (#657 remainder) — closing PR FL-05 after FL-04-B

1. Consume every file-URL provider exactly once and preserve provider order. Classify in-vault URLs as the shipped undoable move before mutation; external URLs copy into the selected vault folder.
2. Copy files and directories recursively without following symlink cycles or escaping the selected root. Preserve raw bytes and security-scoped access.
3. Use exclusive destination creation with deterministic ` 2`, ` 3`, … collision suffixes, per-entry and total size guards, cancellation, and bounded concurrency.
4. Aggregate partial failures, refresh affected levels once, select successful imports, and announce truthful file/folder counts. Preserve shipped drop highlighting and 600 ms spring-load/re-collapse behavior.

Tests: multiple providers; binary/non-UTF8 file; directory and empty directory; collision race; symlink loop/escape; unreadable/oversized entry; cancel; partial success; vault switch; in-vault move; external-copy semantics; spring-load regression.

- [ ] All-provider bounded import coordinator
- [ ] Recursive safe copy, collision, cancellation, and aggregation
- [ ] Tests; a11y 100/100 on tip
