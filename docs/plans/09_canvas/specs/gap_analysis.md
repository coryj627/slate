# Canvas gap analysis — 2026-07-03 review

Adversarial coverage review of the original 15 issues (#359–#373) + the interview decisions, run before spec lock. Each gap records its disposition. (Format mirrors `../../08_ui_parity/specs/gap_analysis.md`.) Infra facts verified against the tree: U1 workspace landed with `EditorItem` = `.markdown` only; `AccessibleDataGrid.swift` is a 99-line non-sorting skeleton; `CommandSection` is an FFI enum.

| # | Gap | Disposition |
|---|-----|-------------|
| G1 | No owner for announcement coordinator / verbosity / Where-am-I; five issues would each invent phrasing | **New issue #518**; grammar normative in `t0_interaction_contract.md` §1 |
| G2 | No rate-limiting/coalescing contract (held-arrow nudge floods VO; bulk ops announce N times) | t0 §1.5; #518 implements; #521/#524/#373 consume |
| G3 | No unified mode contract (move/connect): Esc/Return, queryable state, focus-departure, Esc layering | t0 §2 (M1–M7); `CanvasModeController` ships in #364 (t3); #521/#523 consume |
| G4 | Shortcut collisions unowned (workspace claims ⌥⌘-arrows, ⌘1–9, ⌥⌘=/−…; VO Quick Nav eats plain arrows) | Normative allocation table + rules R1–R4 in `00_program.md`; drift test extended (#364) |
| G5 | **Card resize missing entirely** — drag-only resize would fail WCAG 2.5.7 | **New issue #521** (resize mode alongside move mode) |
| G6 | FKA/first-responder discipline in the NSView renderer unspecified | #367 amendment (t3) |
| G7 | Focus visibility at zoom (2.4.7/2.4.11): sub-pixel rings, off-viewport selection | #367 amendment: screen-space overlay indicator + scroll-into-view; contrast in #370 |
| G8 | Reading-order edge cases undefined (partial containment, nested/overlapping groups, coincident/degenerate nodes) | t1 §rules 1–4 normative + census; #360 amendment |
| G9 | "N of M in group" positional context nowhere specified | t0 §1.2; #362/#364 amendments |
| G10 | Edge directionality (`fromEnd`/`toEnd`) not surfaced in labels or traversal | t0 §1.2 direction phrases; #360 model + #362 amendments; #523 direction step |
| G11 | AX-frame invalidation on pan/zoom (stale frames = VO cursor on empty space) | #367 amendment + dedicated test (t3) |
| G12 | Voice Control label uniqueness ("click Untitled" × 30); picker/dictation friendliness | t0 §1.1 ordinal disambiguation; #367 uniqueness test; R4 |
| G13 | Braille: announcements transient, state not inspectable | t0 §3 inspectability rule; enforced across #362/#368/#524/#373 |
| G14 | #371 scope too narrow (chrome/outline/table; large-type × zoom; 1.4.13 hover content) | #371 amendment (t5) |
| G15 | AccessibleDataGrid can't do what #363 promises (no sort/selection/virtualization) | **New issue #519** (v2; shared with Milestone N); #363 re-scoped onto it |
| G16 | `EditorItem.canvas` activation unowned (switches, store round-trip, glyph, dedup, sub-surface persistence) | #369 amendment: first deliverable (t2) |
| G17 | Dirty/autosave policy undecided vs U1 close gates; conflict + partial-parse surfacing non-visual | Decided: write-through on commit, close-gate bypass tested (#369, t2 §4); t0 §5 surfacing (#366) |
| G18 | Undo routing ambiguous (text-card editor vs op-log); undo not announced | #372 amendment: responder-chain seam, named announcements, bulk = one entry (t3) |
| G19 | Group label rename missing; empty state not actionable onboarding | #368 (rename) + #369 (onboarding copy) amendments |
| G20 | Nudge can silently stack cards (invisible to a non-visual author) | Overlap onset/offset announcements, #521 (t4) |
| G21 | `docs/help/` doesn't exist; Help command opens README | **New issue #526**; help-index routing = noted follow-up |
| G22 | No create path (card / group / new canvas file) in the original issue set | #368 amendment (New Card/Group/Canvas) + **new issue #517** (placement engine) |
| G23 | Obsidian-parity authoring gestures had no keyboard equivalents (create-connected, duplicate, convert-to-note, edge-label edit, subpaths) | **New issue #525** |
| G24 | U2-3 link-integrity rewriter likely skips `.canvas` file-node paths → silent rot on note move | Decision in `00_program.md` sequencing gates: extend the rewriter via #366's serializer; tracked on #366, never silent |
| G25 | JSON Canvas has no alt-text field for image/media nodes | Filename/frontmatter derivation is the floor (t0 §1.1); a Slate extension field (round-trip-safe via #359 `unknown`) is an **explicitly deferred decision** |

---

## Round 2 — adequacy review, 2026-07-03 (cold-developer + cross-document audit)

Two independent reviews of the completed doc set (a "could I implement this cold?" pass verified against the live tree, and a mechanical cross-document consistency audit). Dispositions applied in the same PR:

| # | Finding | Disposition |
|---|---------|-------------|
| R1 | Canvas **mutation** FFI was unowned — Wave 4 had no write API to call | #361 now owns `canvas_apply(handle, CanvasAction{name, ops})` + typed `CanvasOp` set + inverse-action return; shape pinned in t1 |
| R2 | "Navigator" was referenced as a fourth view but never defined | Decided: navigator = canvas-wide **command layer** hosted by outline/table/visual, not a view; t2 shared architecture + t3 #364; "Show Navigator" removed |
| R3 | Malformed/unknown entries were parser-skipped but promised "preserved in the file" — silent data loss on first save | `Canvas.skipped: Vec<SkippedEntry>` retained + re-emitted in document order; malformed round-trip fixture (t1) |
| R4 | #372 claimed reuse of `oplog.rs`, whose format can't express named/inversible canvas ops | Two-layer design pinned in t3: `CanvasApply` journal kind (persistence/audit) + in-memory session-scoped undo/redo stack of (action, inverse); undo does not survive restart |
| R5 | Help promised *Add Note/Media/Link Card* and *Locate…* with no owning issue; "link page title" had no mechanism | Creation verbs + Locate… added to #368 (t4); link labels = URL host (no fetch), t0 §1.1 + t5 |
| R6 | Read-API record shapes (`OutlineRow`, `TableRow`, `WhereAmI`, `RelativeDesc`) undefined across the FFI | Pinned in t1 §#361; "columns vs blob" decided (columns); handle-based API (per-file node IDs) |
| R7 | Quick-open/scan is hard-filtered to `.md`; the `.canvas` listing change was unowned | Assigned to #361 (backend), consumed by #369 |
| R8 | Global `CanvasSelection` on AppState breaks with multi-pane | Per-`CanvasDocument` (one per open path, U1 NoteDocument pattern); AppState mirrors the focused pane; marks clear when the last tab closes (t2) |
| R9 | No rigid-set placement API for marked-set move/duplicate | `place_set` added to #517/t1 |
| R10 | Move-mode transient geometry contradicted "no view-local state"; no hypothetical-overlap query | Pipeline exception carved in t4; `canvas_check_overlap` added to #361 |
| R11 | #519 "behind the existing API" impossible; its sort announcements would bypass the #518 funnel | v2 API extension pinned (sort descriptors, selection/activation, injectable announcer); #363 injects the coordinator (t2) |
| R12 | Windowed AX tree would strand VO at the viewport edge on the renderer | Windowing contract in t3: viewport+margin materialization; VO next/prev moves selection → auto-pan → materialize |
| R13 | 2,000-node fixture needed at Wave-1 close but owned by Wave-5 #365 | Fixture (+ generator) committed with #359 (t1); #365 consumes |
| R14 | Wave-2 text-card activation target undefined (editor is Wave 4) | Interim read-only text detail panel specified in t2 §#362; #368 swaps it |
| R15 | "#369 uncomment the reserved case" was fiction; per-tab view state has no schema slot | Reworded to the real seams: `EditorItem` case + Codable discriminator, `FailableTab` whitelist, additive optional `activeCanvasSurface` tab field (t2) |
| R16 | `CommandSection.canvas` landed in Wave 3 (#364) but Wave 2 registers commands | Enum change moved to #369, first Wave-2 PR (program R1, t2, t3) |
| R17 | Six issue bodies carried stale `[Wave N]` tags; #365/#369 bodies had stale wave/code references; "(G6)"/"(G7)" tags pointed at the wrong gap numbers | Issue bodies corrected (wave tags → program waves; G6→G25, G7→§K; stale-ref notes added) |
| R18 | Program inventory listed ⌘N as claimed (it's free — New Note is ⇧⌘N, system `.newItem` replaced) and omitted ⇧⌘W | Inventory corrected; ⌥⌘N rationale updated (⌘N reserved for future New Note) |
| R19 | t4 cited t0 M2 (Esc = cancel) for the text-card editor while defining Esc = commit | t0 **M8** embedded-editor carve-out added; t4/help align on "Esc commits" |
| R20 | Preset color *names* needed by Wave-1 `CardSummary` but pinned in Wave-5 #370 | Names pinned in t1/t0 (red, orange, yellow, green, cyan, purple; hex = "custom color"); #370 verifies contrast/naming only |
| R21 | "Untitled N" ordinal stability domain undefined (feeds Voice Control) | Pinned in t0 §1.1: document order at load, session-stable, moves don't renumber |
| R22 | Reparenting was only a geometric side effect — no voice-friendly command (R1 violation) | *Move into Group…* / *Remove from Group* commands added to #368 (t4) |
| R23 | #368 executes first in Wave 4 but referenced later PRs' mechanics; APCA stated both ">" and "≥"; migration naming; 08 program status page stale | t4 execution-order annotation; standardized **Lc ≥ 75**; `NNN_` migration convention noted; 08 status header updated |

Left open deliberately (implementing dev's judgment, per the review): numeric values of `GRID_STEP`/`DEFAULT_CARD_SIZE`/`DEFAULT_GAP`, the exact coalescing window (~150–250 ms), group-creation default geometry, Where-am-I panel presentation, `Target` column rendering per kind, final chord bindings (drift-tested at registration), and the SwiftUI `undoManager` plumbing for #372's responder seam.

---

Considered and *not* adopted (with reasons): pattern-fills for color-blind users on the renderer (Increase Contrast + color-as-text already satisfy 1.4.1); z-order editing commands (JSON Canvas has no z-index; document-order tiebreak suffices); pop-out canvas windows (excluded by U1 scope); live URL embeds (interview decision 10 — divergence documented, may return as an evaluated enhancement).
