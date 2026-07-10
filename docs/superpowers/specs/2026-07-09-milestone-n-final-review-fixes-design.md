# Milestone N Final Review Fixes — Design

**Status:** Approved in conversation on 2026-07-09 (`please resume`).

## Goal

Close every actionable finding from the whole-branch Milestone N review without
weakening the N0–N4 contracts, while keeping the human VoiceOver checklist as a
separate operational gate.

## Chosen approach

Use targeted, contract-preserving repairs within the existing Rust session/FFI
and Swift document architecture. This avoids a risky query-state rewrite while
fixing the actual cross-surface ownership failures. A broader observable-query
registry was considered but rejected for this milestone because it would expand
the regression surface; spec exemptions were rejected because they would hide
verified defects.

## Architecture

### 1. Atomic Base edits

- Add a session/UniFFI batch edit API that accepts ordered `Vec<BaseEdit>`.
- Reuse Core's sequential-reparse serializer internally, but write, reindex,
  replace the open handle, and invalidate transient state exactly once.
- Swift Save-to-View sends one batch and rebases the builder's comparison draft
  after success. A validation or serialization failure occurs before the write
  and leaves disk, index, handles, and model baseline unchanged; persistence
  failures retain the existing `save_text` transaction contract.
- Structural view/order/column edits clear or safely remap transient sort state.

### 2. Rust semantic fidelity

- DQL `outgoing()` and its saved `.base` form use the same link membership,
  including embeds.
- Preserve the pinned `Value::Duration(i64)` representation. Date arithmetic
  recognizes literal `duration("...")` expressions before duration collapse and
  applies months calendar-first, then fixed units; standalone durations remain
  millisecond values.
- Restore the pinned escape grammar: unlisted escapes such as `\r` preserve the
  backslash and character.
- Root-level flow mappings are edited as flow mappings, including empty/missing
  formulas, filters, and views.

### 3. Builder correctness and responsiveness

- Advertise operators only on executable receiver/value families. File values
  expose `hasTag`, `hasLink`, and `matches`; text no longer exposes a silently
  null `matches` path. Operand decoding uses the operator-appropriate typed
  value decoder so save/reopen remains structured.
- Preview debounce stays on MainActor, but native open/execute/close work runs
  off MainActor and publishes back only if the generation is current. A newer
  preview can cancel a running older query.
- Successful saves rebase the model and cannot replay stale removals.

### 4. Refresh and selection ownership

- After any successful same-session note/property write, global Bases refresh
  runs before active-note-only publication guards.
- The refresh registry includes tabs, dashboards, docked saved queries, and
  visible editor/reading embeds.
- Saved-query updates reopen every consumer referencing the saved-query ID.
- Cell selection is preserved by stable column ID across result/column changes;
  if the ID disappears, selection is safely cleared or moved to a valid cell.

### 5. Keyboard and accessibility contracts

- Focused sort rows, included-column rows, and dashboard sections support
  Option-Up/Down reorder commands in addition to buttons.
- Dashboard title and section headings expose explicit H1/H2 semantics.
- Tests exercise command routing and accessibility structure, not only model
  mutation helpers.

### 6. Evidence and governing specs

- Correct the N4 Recent source operator to inclusive `>=`.
- Create raw, genuine Obsidian-app-written `.base` captures in a temporary vault
  using the installed Obsidian application. Copy bytes unchanged into the
  fixture corpus and record app version, capture steps, timestamp, and SHA-256
  in a sidecar provenance document.
- Keep the manual VoiceOver checklist unchecked until a human performs it.

## Error handling and concurrency

- Batch validation and serialization failures are atomic and return the first
  named error before persistence begins.
- Detached preview work never touches MainActor state directly; cancellation and
  generation checks guard publication and handle cleanup.
- Global refresh remains session-identity guarded so a completed write from an
  abandoned vault cannot publish into a replacement session.
- Consumer reopen failures surface through existing error states without
  discarding the persisted saved query.

## Verification

Every finding receives a failing regression first, then focused GREEN coverage.
The final gate includes serializer/DQL/evaluator/engine/session suites, Swift
builder/routing/grid/dashboard suites, full Rust/CLI and Swift suites,
`cargo fmt`, Clippy, `a11y-check`, the generated default/full censuses, fixture
byte hashes/provenance, and independent Rust/Swift/contract re-review.

## Self-review

- No TODO/TBD placeholders.
- The batch API, model rebase, and refresh ownership agree across Rust/FFI/Swift.
- The duration design preserves the pinned public value representation.
- The Obsidian fixture remains byte-raw; provenance is sidecar-only.
- Manual AT is explicitly outside automated completion.
