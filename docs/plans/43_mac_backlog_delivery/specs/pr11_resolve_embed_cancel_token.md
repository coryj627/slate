# PR 11 — `resolve_embed` takes a CancelToken; mac's embed walks stop when their note goes away

Issue: #1287 (#1279 follow-up; locked 05 §4; AR-24). Size M. Lanes:
rust, swift, a11y, windows (the FFI changes). Depends on nothing. PR 12
follows it. Parallel with PRs 8, 9, 10.

## Goal

The last embed resolver that walks with an internal token becomes
cancellable, and the two mac production callers hold one token per batch
or request, cancelled when the task is cancelled or the selection
changes, so a walk whose result will be discarded stops at its next node
instead of running on under the session mutex.

## Current state on main

- Core `crates/slate-core/src/session.rs:6182` `resolve_embed` calls
  `resolve_embed_at_depth(…, &CancelToken::new())` (`:6189`);
  `resolve_embed_preview` (`:6201`), `resolve_embed_preview_pooled`
  (`:6230`) and `resolve_embed_reading_card` (`:6261`) already take the
  caller's token (#1279).
- FFI `lib.rs:2051` `resolve_embed(host_path, target, alt)`; the
  preview sibling's signature and doc comment (`:2066-2075`) show the
  shape.
- Mac callers: `AppState.swift:12062-12075` the batch in
  `loadCurrentNoteEmbedResolutions` (a `Task.detached` looping over
  `embedTargets`, per-embed failures synthesized as `.unresolved`), and
  `:12142-12160` `requestReadingEmbedResolution` (one detached call; the
  stale guard at `:12158` drops the write when the selection moved, but
  the walk has already run to its end).
- No Swift test calls `resolveEmbed(`.
- Windows has no `resolve_embed` callers (confirm with a grep); its lane
  must still regenerate and compile.

## Windows reference

#1279's shape for the three preview resolvers: the host cancels the
request's token when the preview is closed, superseded, invalidated or
disposed, and the walk returns `VaultError::Cancelled` at its next node.
Contract 40 AR-24 (`:67`).

## Design

1. **Core and FFI.** `resolve_embed(&self, host_path, target, alt,
   cancel: &CancelToken)` in core; `cancel: Arc<CancelToken>` over FFI
   (the parameter last, like the siblings). Update core's callers
   (`slate-cli` if any) and the Rust tests. Check `crates/slate-uniffi`
   for a bindings contract version constant and bump it if the policy
   requires for a signature change.
2. **Mac batch.** One `CancelToken` per batch, created before the
   detached task; the awaiting task cancels it through
   `withTaskCancellationHandler`; a selection change cancels the previous
   batch's token explicitly (keep the token on the state so the next
   load can cancel it). A `.cancelled` `VaultError` from any embed drops
   that result and ends the batch without merging (never an
   `.unresolved(.readError)` row).
3. **Mac single request.** Same shape, one token per request, cancelled
   on selection change; `.cancelled` is a dropped result.
4. Regenerate both bindings; `swift-tests` and `windows.yml` must be
   green together.

## Changes

- `crates/slate-core/src/session.rs:6182-6190`; `crates/slate-uniffi/src/
  lib.rs:2051-2063`; any CLI caller.
- `AppState.swift:12040-12170`.

## Tests

- Rust: `resolve_embed` with a pre-cancelled token returns `Cancelled`
  before reading the target; an uncancelled token resolves as before.
- Swift (`EmbedViewTests` or a new `EmbedResolutionCancellationTests`):
  `testASelectionChangeCancelsTheInFlightBatchToken` (inject a slow
  resolver seam or a large embed set; assert the token is cancelled and
  no unresolved rows appear), `testACancelledRequestIsDroppedNotUnresolved`,
  `testEmbedResolutionsStillMergeAfterANormalBatch` (regression).
- PR 12's source census will count these tokens as owned; name the
  owner in a comment at each site.

## Accessibility

No copy change. A cancelled walk no longer produces a spurious
"unresolved" card that VoiceOver would read.

## Docs and registers

`docs/plans/40_nvda_matrix_remediation_contracts.md` AR-24 (`:67`)
closed.

## Acceptance

- [ ] Signature changed in core and FFI; both bindings regenerate; both
      native lanes green.
- [ ] Both mac callers hold one token per operation and cancel it.
- [ ] Facts pass; AR-24 closed.

## Risks

An FFI signature change: never ship a dylib with mismatched bindings;
run `make regenerate-bindings` locally and let `windows.yml` prove the
C# side.

## Brief

Make `resolve_embed` cancellable end to end. Read `session.rs:6182-6265`,
`lib.rs:2040-2075`, `AppState.swift:12040-12170` and contract 40 AR-24.
Add the `CancelToken` parameter in core and over FFI (last parameter,
the preview siblings' shape), regenerate both bindings, and give each
mac caller one token per batch or request, cancelled through
`withTaskCancellationHandler` and on selection change, treating
`.cancelled` as a dropped result. Write the Rust and Swift facts first.
Close AR-24. Done when `rust`, `swift-tests`, `a11y-check` and `windows`
are green on the same commit.
