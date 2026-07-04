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

Considered and *not* adopted (with reasons): pattern-fills for color-blind users on the renderer (Increase Contrast + color-as-text already satisfy 1.4.1); z-order editing commands (JSON Canvas has no z-index; document-order tiebreak suffices); pop-out canvas windows (excluded by U1 scope); live URL embeds (interview decision 10 — divergence documented, may return as an evaluated enhancement).
