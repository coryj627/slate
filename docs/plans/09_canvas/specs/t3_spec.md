# T3 executable spec — Navigation + visual surface (Wave 3)

Issues: #364 (keyboard navigator + palette commands) · #367 (visual renderer) · #520 (viewport commands) · #372 (op-log undo).
Every issue satisfies 08 DoD §A–§G + 09 deltas §H–§L + the [t0 contract](t0_interaction_contract.md). One PR per issue.

**Execution order: #364 → #372 → #367 → #520.**

---

## #364 — Keyboard navigator + palette commands

As issued (next/prev card in reading order, enter/exit group, follow connection forward/back, jump to connected card N, trace path) plus the contract work:

- **The navigator is a command layer, not a fourth view** (t2 shared-architecture decision): its commands are hosted by every canvas surface and operate on `CanvasSelection`. Arrow bindings scope per surface (outline rows already consume ↑/↓ as list navigation — the navigator's ←/→ connection-following works there too; on the visual surface all four arrows are navigator moves outside of modes).
- **`CommandSection.canvas`** already exists (landed with #369 in Wave 2); this issue registers the navigator command set into it. The deferred `.workspace` section case may ride along with #369's enum change if trivial.
- All navigator movements are `CommandSection.canvas` commands with t0-conformant announcements via #518 (destination phrasing, N-of-M, direction phrases). Plain-arrow bindings follow program rule R2 (canvas-surface focus only; palette equivalents always; VO Quick Nav caveat documented in each command's help).
- **Mode-stack plumbing** (t0 §2) ships here as shared infrastructure (`CanvasModeController`): entry/exit/announce/queryable-value/auto-cancel-on-focus-departure/Esc-ladder — consumed by #521/#523 in Wave 4. M1–M7 tests land now against a test mode.
- Chords per the program allocation table; the existing chord↔surface **drift test** extends to the canvas section.
- "Trace path from selected card": walks the outgoing chain (cycle-safe), announcing each hop; any dead-end announced ("End of path — 4 cards visited").

**Tests:** each movement on fixture canvases (dead-ends, group boundaries, multi-edge, cycles); drift test; mode-controller M1–M7; announcement strings.

## #372 — Op-log + undo

**Design decision (this was open; it is now closed).** The existing `oplog.rs` journal is a text-save format (`WholeFileReplace` / `EditBatch` of text ops, keyed on `files.id`) with no op names, no inverses, no redo — canvas undo cannot literally reuse it. The architecture is two-layer:

1. **Persistence/audit layer:** each committed `canvas_apply` (#361) appends a new journal kind, `CanvasApply { name, action, inverse }`, to the existing op-log (append-only, additive format change; old readers skip unknown kinds). This is the durable record and enables document-level revert, mirroring the note-save journal's role.
2. **Live undo/redo layer:** an in-memory, per-`CanvasDocument`, **session-scoped** named stack of `(action, inverse)` pairs returned by `canvas_apply`. Undo applies the inverse (through the same `canvas_apply` pipeline, flagged so it doesn't re-push), redo re-applies. **Undo does not survive app restart** — same contract as the note editor's `NSUndoManager`; the journal remains for audit/revert.

Plus, as issued:
- **Responder-chain routing:** ⌘Z inside an inline text-card editor (Wave 4) drives that editor's `NSUndoManager`; ⌘Z on canvas surfaces drives the canvas stack. First-responder-based seam, tested. (The SwiftUI-side `undoManager` plumbing for outline/table focus is implementation-level — the routing *behavior* is the contract.)
- Every committed action = **one** stack entry + one journal entry (bulk marked-set ops included — single undo; #524 relies on this).
- Undo/redo announce the action name (t0 §1.3): "Undid: move 'Research'"; redo symmetric.
- Undo validates against the current content hash; stale undo after external change → conflict surface per t0 §5, never a blind overwrite.

**Tests:** journal entry + stack entry per action; undo restores exact prior canvas (apply→invert→serialize equality); sequences incl. redo; responder routing; conflict-undo safety; restart clears the stack but not the journal.

## #367 — Visual renderer (read-only)

As issued (NSView + CALayer, pan/zoom, all node kinds + labelled edges, `CanvasSelection` sync, Reduce Motion) plus the decision-3 and gap work:

- **Per-card AX elements:** an `NSAccessibilityElement` per visible card and edge — label from t0 §1.1 (same strings as the outline), `accessibilityFrame` in screen coordinates, actions (activate, and Wave-4 actions as they land). **Frame invalidation on every pan/zoom/resize** — a test zooms then asserts queried frames match new geometry (stale frames are the classic failure: VO cursor on empty space, Voice Control numbers floating).
- **Windowing contract (§K without stranding VO):** elements materialize for the viewport plus a one-viewport margin. VO element navigation past the loaded edge is never a dead end: VO next/prev on the renderer moves `CanvasSelection` in reading order, selection change auto-pans (always — 2.4.11 scroll-into-view), and the pan materializes the next window. Test: with a 2,000-node fixture, VO-next from the last visible card reaches the next card in reading order.
- **Voice Control label uniqueness:** speakable names disambiguate duplicates ("Untitled 3" — t0 §1.1); no two elements on the surface share a speakable name (test).
- **FKA/first-responder:** `acceptsFirstResponder`; predictable key-loop position (after the surface switcher); visible focus indication for view-focus and card-selection distinctly.
- **Focus visibility (WCAG 2.4.7/2.4.11):** the selection indicator is drawn in a **screen-space overlay layer** with a minimum screen-space thickness (never scaled sub-pixel at low zoom); keyboard selection **always** scrolls the card into view regardless of the follow-selection toggle; indicator contrast vs card fill and canvas background is APCA-measured in both appearances (with #370).
- **Hit-testing/z:** overlapping cards resolve to the topmost by document order (consistent with t1 tiebreak).

**Tests:** selection sync both directions; AX-frame invalidation; label uniqueness; renders all node kinds on the fixture; Reduce Motion; focus-ring metrics; scroll-into-view.

## #520 — Viewport commands

As issued (#520 body): zoom in/out (⌘= / ⌘-), actual size (⌘0), fit (⇧1), zoom to selection (⇧2), follow-selection toggle (default ON, silent auto-pan per t0 §1.5); Reduce Motion = instant transform; zoom level announced on command and inspectable in the renderer's AX value.
**Tests:** transforms per command; follow-selection pans from every surface; silence of auto-pan; Reduce Motion; drift test rows.

---

## Acceptance (wave close)

A keyboard/voice user traverses every card and connection without the renderer; a VoiceOver user can also do it *on* the renderer, whose elements track pan/zoom correctly; a low-vision keyboard user controls the viewport fully; every mutation-to-date is undoable with named announcements. a11y-check 100/100; benchmarks (renderer pan/zoom at 2,000 nodes) recorded.
