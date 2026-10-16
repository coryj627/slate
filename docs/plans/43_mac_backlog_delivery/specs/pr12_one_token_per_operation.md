# PR 12 — One CancelToken per operation, and a census that keeps it so

Issue: #1324 (W7-7 PR 7 follow-up). Size M. Lanes: swift, a11y, windows.
Depends on PR 11 (its tokens are counted). Last in wave 2: it sweeps the
sites the other wave-2 PRs touch.

## Goal

No paged or long core call on mac passes a fresh, never-cancelled
`CancelToken()`. Each operation holds one token tied to its task or its
owner (document, view, sheet), cancelled when that ends; synchronous
main-actor listing loops move off the main actor first (locked 05 §4.1);
and a source census makes any new never-cancelled token need a reviewed
reason.

## Current state on main (the ten sites)

- `AppState.swift:9936` (a tree listing, `.asTreeListing()`), `:11506`
  (`reloadImportInventoryOnly`'s `openableDocuments` page loop, already in
  a `Task.detached`), `:16148`.
- `Graph/GraphDiagramView.swift:1352` `settleReduceMotionForTesting`
  (`session.runToConvergence(cancel: CancelToken())`): a test-only helper.
- `Bases/BaseDocument.swift:248`, `:512`; `Bases/BaseEmbedDocument.swift:
  502`, `:958`; `Bases/DashboardDocument.swift:385`.
- `Canvas/AppState+CanvasCreate.swift:147-160` `canvasMediaPaths`: a
  synchronous computed property on the main actor paging `listFiles`
  with a fresh token per page.
- The reference shape: `Bases/AppState+Bases.swift` around `:2174`
  (`withTaskCancellationHandler { await nativeTask.value } onCancel: {
  cancelToken.cancel(); nativeTask.cancel() }`), `basesLoadNotePaths()`
  (fixed by #1309), and the initial scan (`AppState.swift:11538-11555`:
  one token across the scan and every page).
- `BaseQueryBuilderTests` already asserts
  `XCTAssertFalse(source.contains("cancel: CancelToken()"))` for its own
  source: the census pattern to widen.

## Windows reference

Locked 05 §4 ("every method that scans, indexes or queries the vault
accepts a CancelToken") and §4.1 (callers dispatch off the UI thread).
Windows' rescan holds one token for a whole run (`VaultLifecycleViewModel.
Rescan.cs:200-250`).

## Design

1. For each site: identify the owner. Inside a `Task`: one token created
   with the task, cancelled through `withTaskCancellationHandler`.
   Owned by a document or view: one token stored on the owner, cancelled
   in its teardown (`close`, `deinit`, `onDisappear` as appropriate) and
   replaced per operation (a new load cancels the previous token).
2. `canvasMediaPaths` becomes an async operation off the main actor (a
   detached task with its own token, cancelled when the picker closes or
   a newer listing starts); the picker shows its loading state meanwhile
   (never silent).
3. `settleReduceMotionForTesting`: pass a token from the test (preferred)
   or allowlist with the reason "test-only helper".
4. The census: a test that walks every `.swift` under
   `Sources/SlateMac`, fails on `cancel: CancelToken()` outside an
   allowlist of `file:line reason` entries, and fails if an allowlist
   entry no longer matches (so the list cannot rot). The allowlist starts
   empty or with the one test-only helper.

## Changes

- The ten sites; `Canvas/CanvasCardPicker.swift` (or wherever the media
  picker consumes `canvasMediaPaths`) for the async shape; a new
  `CancelTokenCensusTests.swift`.

## Tests

- The census.
- Per owner: `testCancellingTheOwnerCancelsItsListingToken` for one
  document type (BaseDocument) and the canvas media listing; a page loop
  observes the token (inject a seam that cancels mid-loop and assert the
  loop stops without a partial publish).
- `MoveToLoadingCancellationTests` and `BaseQueryBuilderTests` keep
  passing (they pin the pattern).

## Accessibility

The media picker's loading state is announced through its existing
loading row; no new copy.

## Docs and registers

None beyond the PR body (this is a locked-decision conformance sweep).
Mention the census in `apps/slate-mac/README.md`'s FFI section if it
lists `CancelToken` guidance (it does, under "Consuming the FFI").

## Acceptance

- [ ] Zero `cancel: CancelToken()` outside the allowlist; the census is
      green and self-checking.
- [ ] `canvasMediaPaths` is off the main actor and cancellable.
- [ ] Lanes green.

## Risks

Moving `canvasMediaPaths` to async changes the picker's timing; the
picker's existing facts (`CanvasFFITests`, picker tests) must stay green,
and the loading row must appear rather than a frozen sheet.

## Brief

Give every paged or long core call on mac one owned, cancellable token.
Read the ten sites listed above, the reference pattern at
`Bases/AppState+Bases.swift:2160-2200` and `AppState.swift:11538-11555`,
and `BaseQueryBuilderTests`' source assertion. Convert each site to one
token per operation tied to its task or owner; make `canvasMediaPaths`
an async, off-main-actor, cancellable listing with a loading state; pass
a test token into `settleReduceMotionForTesting`. Add the repository-wide
census with a self-checking allowlist. Write the facts first. Done when
the census passes with an empty (or test-only) allowlist and the lanes
are green.
