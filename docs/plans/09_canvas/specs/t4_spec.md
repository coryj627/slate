# T4 executable spec — Authoring (Wave 4)

Issues: #368 (actions umbrella) · #521 (move & resize modes) · #522 (card picker + placement commands) · #523 (connect flow) · #524 (mark-then-act).
Every issue satisfies 08 DoD §A–§G + 09 deltas §H–§L + the [t0 contract](t0_interaction_contract.md). One PR per issue.

**Gate:** Wave 3 complete **and U3's editor seam stable** (text-card editing pins to the post-U3 editing component — program sequencing gates).

**Execution order: #368 → #522 → #521 → #523 → #524.** #368 lands first with the verbs that stand alone (create/open/edit/rename/delete/color; the pickers it needs ship inside it); its **Move into Group…** command and marked-set behaviors activate as #522/#524 land in the same wave — the issue's test list annotates which assertions defer to those PRs. "Lands in edit mode" requires the U3 editing component on day one of the wave (the wave gate exists for exactly this reason).

---

## Mutation pipeline (all five issues)

Every action follows one path: action → `canvas_apply(handle, action)` (#361's write surface — the UI builds `CanvasOp`s, Rust mutates) → #366 serialize → atomic write → journal entry + undo-stack push (#372) → reindex → UI refresh from the model → #518 confirmation announcement. **No view-local canvas state**, with one carved exception: **mode-transient geometry** — during move/resize mode (#521) the UI holds the hypothetical position/size (querying `canvas_check_overlap` per step for warnings) and commits it as a single `canvas_apply` on Return; Esc discards it without any backend call. Bulk actions over the marked set batch into one `CanvasAction` (one write, one undo, one announcement).

## #368 — Canvas actions (umbrella)

Owns the core verbs; the specialized flows live in #521/#522/#523/#524 and this issue routes to them. Scope here:

- **Create**: New Card (⌥⌘N; text card via #517 placement, announced, lands in edit mode), New Group (prompts label), and **New Canvas** (file-level; registers in `CommandSection.file`, uses the U2-2 create API, appears in file tree/quick open — closes the "can't start from empty" gap).
- **Create, all card kinds** (the help doc and Obsidian parity both require these; previously unowned): **Add Note to Canvas…** (file card via the quick-open-style note picker), **Add Media…** (vault media picker; same component), **Add Link Card** (prompts for/paste a URL). All place via #517 and announce. **Locate…** on a missing-target file card (t0 §5) reuses the same note picker to repoint the card.
- **Open** card (per-kind activation as in #362; replaces the Wave-2 interim read-only text detail); **edit text-card content** in the real note editor (decision 7): inline panel/sheet hosting the post-U3 editing component; **Esc commits** + restores focus to the card row (t0 **M8** embedded-editor carve-out — not M2 cancel semantics); `allowsUndo` inside the editor, one `canvas_apply` on commit (#372 responder seam).
- **Rename group label** (prompt; announced) — group labels are the skeleton of the reading order.
- **Delete** card/connection/group (group delete offers Ungroup-instead; destructive confirmations carry the undo hint per t0 §1.3).
- **Reparent, both ways**: geometric containment updates the tree as a side effect of any move (t1 rules, announced "Moved into group 'Q3'"), **and** first-class commands exist per R1 — **Move into Group…** (group picker; #517 places inside the target's bounds) and **Remove from Group** (places just outside). Voice-friendly; no drag or nudge required.
- **Set color** (named presets + hex; announced with the color *name*; #370 surfaces it).
- Every action: palette + menu + **context-menu on the card row/element** (t0 M6 visible-control rule); non-visual reachability asserted (action available with the renderer hidden).

**Tests:** per action — model mutation, round-trip, announcement, non-visual reachability; editor commit/Esc/focus-restore; new-canvas end-to-end.

## #522 — Card picker + structural placement commands

Per the issue body. Spec pins: the picker is one reusable component (`CanvasCardPicker`) with the command-palette interaction model; proximity sort uses model geometry (distance from the anchor card's center); rows read "⟨type⟩ card '⟨title⟩', in ⟨group⟩". Placement commands compute via #517 (never UI math) and announce the `RelativeDesc`. Marked-set placement moves the set as a rigid unit.
**Tests:** filter, ordering, VO rows, focus return; per-command geometry + announcement; rigid-unit invariants (pairwise offsets preserved).

## #521 — Move & resize modes

Per the issue body, on the Wave-3 `CanvasModeController` (t0 §2 M1–M7). Spec pins: nudge = `GRID_STEP` / ⇧ `GRID_STEP_LARGE` (t1 constants via FFI); the UI holds the transient geometry (pipeline exception above) and queries `canvas_check_overlap` per step; announcements coalesced and **relative** ("Below 'Research', right of 'Ideas'"), overlap onset/offset flagged; commit = one `canvas_apply` capturing start→end (not per-nudge entries); cancel restores exact prior geometry with no backend call. Resize: arrows = width (←→) / height (↑↓); presets Fit to Content / Default Size; minimum size enforced with announcement. Marked-set move: rigid unit.
**Tests:** geometry census (random sequences round-trip clean); step/⇧-step; overlap onset/offset strings; commit/cancel; single op-log entry; mode-contract conformance (M1–M7 suite reused).

## #523 — Connect flow

Per the issue body. Spec pins: auto-side = nearest edges by geometry at confirm time; direction step maps to `fromEnd`/`toEnd` (default: one-way arrow to target, matching Obsidian); connect-mode candidate stepping reuses navigator movements verbatim (no second traversal grammar); existing-connection edit (sides/direction/label) and delete live on the connection rows (#362) and the palette.
**Tests:** auto-side fixtures; direction round-trips; mode traversal + confirm/cancel; picker end-to-end; announcement strings; non-visual reachability.

## #524 — Mark-then-act

Per the issue body. Spec pins: `CanvasSelection.marked` is the store (Wave-2 slot; per-`CanvasDocument`, shared across panes showing the same canvas); mark toggle ⌃⌘M works on whichever surface has focus; marks list is a focusable panel (leaf/popover) with per-row Unmark + Jump and a **Clear All Marks** command; bulk actions = Group / Move (rigid, via `place_set`) / Delete / Set Color, each one `CanvasAction` + one summary announcement; marks clear when the last tab for the path closes; selection-vs-marks semantics: arrows move selection and never mutate marks.
**Tests:** cross-surface mark propagation + AX values (t0 §3); each bulk action correct + single-undo restores all N; marks list navigation + focus return; clear-on-close.

---

## Acceptance (wave close)

**The milestone's core promise:** a blind or keyboard-only user starts from an empty vault, creates a canvas, authors cards (including connected cards), arranges them (nudge + structural placement), connects and labels them, groups a marked set, edits text cards in the real editor, and undoes any of it — entirely without the visual surface, with every step announced per t0 and inspectable per t0 §3. Voice Control and Switch Control paths per the t0 §4 matrix. a11y-check 100/100.
