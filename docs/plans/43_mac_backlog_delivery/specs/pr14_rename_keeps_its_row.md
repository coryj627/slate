# PR 14 — A renamed file keeps its row (the fact #785 lacks)

Issue: #785. Size S. Lanes: swift, a11y, windows. Depends on nothing.
Parallel with PRs 13, 15, 15b, 25.

## Goal

Pin, on mac, what the Windows rebuild proved: renaming from the file
list leaves the row present under its new name, selected, with no stale
old-path row, and respecting the active filter. If any criterion fails,
fix it; the issue closes either way.

## Current state on main

- `AppState.swift:16807` `renameEntry(path:isDirectory:to:undoContext:
  renameOwnerID:)`: admits the mutation, runs the structural rename on a
  worker, then `applyRetargets` and `publishTreeMutation(.rename(oldPath:
  newPath:), …)` (`:16841-16845`), with the folder-note fix-up for
  directories.
- The sidebar reconciles from `appState.treeMutation`
  (`FileTreeSidebar.swift:5688` `.onChange(of: appState.treeMutation?.
  token)`, `:6265` the consumer).
- Existing facts: `FileTreeSidebarTests.swift:1246`
  `testRemapExpansionFollowsRenameIncludingDescendants`, `:1421`
  `testRenameOrderingKeepsFolderExpandedThroughInvalidateReloadRemap`,
  `:1461` `testStableIDRenameUpdatesPathLedgerDespiteUnchangedIDSet`. None
  asserts the renamed row's presence, selection, or the absence of a
  stale row.

## Windows reference

`FileManagementTests.APublicationDuringInlineRenameKeepsTheTypedName`
ends by asserting `SelectedNode?.Path == "journey.md"` after the commit;
the journey `FileManagement_CreateRenameDuplicateMoveDelete_IsClean`
selects the renamed row from the live tree afterwards (triage comment on
#785, 2026-08-22).

## Design

One view-model-level fact over `FileTreeViewModel` with a fetcher that
returns the post-rename listing, driving the mutation consumer with a
`.rename` publication: the row for the new path is present, is the
selected row, no row for the old path remains, and a filter that matched
the old name and matches the new one keeps the row (and one that no
longer matches hides it). If the consumer fails any assertion, fix it in
this PR (expected small: the selection follow or the stale-row removal)
and retitle the issue `mac:` in the PR body; otherwise close #785.

## Tests

`FileTreeSidebarTests.testARenamedRowStaysPresentSelectedAndUnduplicated`
(and `testARenamedRowRespectsTheActiveFilter`).

## Docs and registers

None (no register row). Comment #785 with the mac verdict.

## Acceptance

- [ ] The fact exists and passes (with a fix if needed).
- [ ] #785 closed, or retitled with the failing criterion named.

## Brief

Confirm or fix #785 on mac with one fact. Read `AppState.swift:
16807-16870`, `FileTreeSidebar.swift:5680-5700` and `:6260-6280`, and the
existing rename facts at `FileTreeSidebarTests.swift:1246`, `:1421`,
`:1461`. Add a view-model fact that renames from the tree and asserts
the renamed row is present under its new name, selected, with no stale
old-path row, and that the active filter is respected. Fix the consumer
if a criterion fails. Comment #785 with the verdict. Done when the fact
passes and the lane is green.
