# W3-1 reading view — NVDA field verification record

**Tester:** Cory Joseph · **AT:** NVDA 2026.1.1 · **OS:** Windows 11 Pro build 26200
**Build:** `feat/w3-1-reading-view` — final verified commit `58dd8b2` (passes ran across `23f7f9b` → `58dd8b2` as defects were fixed between rounds)
**Corpus:** the two-note smoke vault (headings ×4 across two levels, resolved + unresolved wikilinks, tag, external link, citation-free, bullet/nested/ordered/task lists, block quote, rust code fence, 2×2 table, thematic break, block embed card)
**Method:** five iterative passes, NVDA Speech Viewer transcripts captured verbatim; each round's defects fixed and re-verified before the next. Diagnostics via `SLATE_UIA_DIAGNOSTICS=1` per-key chord logging.

## Verified working (heard, not inferred)

| capability | evidence |
|---|---|
| Mode toggle `Ctrl+Shift+E` | focus lands in "Reading view document", first line announced |
| Linear caret reading | every line spoken in authored order; links announced inline as links; task checkboxes with state; table entered/exited natively ("table with 2 rows and 2 columns" / "out of table"); code-fence interior spoken |
| **Heading levels in line reading** | "heading level 1 Reading smoke test", "heading level 2 Lists and tasks" — via the `StyleId` text-provider decorator |
| Heading chords `Ctrl+Alt+H` / `+Shift` / `+1..6` | landings "Lists and tasks, level 2 heading." both directions; misses "No next heading." / "No previous heading." / "No next level 1 heading." |
| Link chords `Ctrl+Alt+K` / `+Shift` | all five links, both directions, landings "Target Note, link." etc.; both misses |
| List chords `Ctrl+Alt+U` / `+Shift` (alias) | all four stops (bullet → nested → ordered → task), both directions, both misses |
| Table chords `Ctrl+Alt+T` / `+Shift` | landing "column a, table."; both misses |
| Code chords `Ctrl+Alt+C` / `+Shift` | landing "fn spoken_interior() -> usize { 42 }, code block."; miss |
| Embed chords `Ctrl+Alt+E` / `+Shift` | landing "Embedded note Target Note."; both misses |
| Restore in reading mode | session closed in reading mode restores populated (round 3 fix verified round 4) |
| Alt-menu suppression | no menu-bar focus theft after chords (rounds 4–5) |

## Defects found by these passes, each fixed and re-verified

1. UIA peer bound to a swapped-out document → "Reading view document blank", silent caret (round 1 → persistent-document merge).
2. Programmatic caret moves not echoed by NVDA → silent chord landings (round 2 → `ReadingNavLanded` vocabulary events).
3. Restored reading-mode tabs empty (round 2 → constructor projection).
4. Class `RichTextBox` bindings eating `KeyDown`-phase chords (round 3 → `PreviewKeyDown` dispatch).
5. Alt-release menu activation stealing focus mid-navigation (round 3).
6. Heading levels absent from line reading (round 3–4 → `StyleId` decorator; owner overrode the earlier property-only decision).
7. Consecutive lists fused across ordered-ness; marker glyphs leaking into announcements (round 5 → builder split + marker strip).
8. **`Ctrl+Alt+L` grabbed globally on the test machine** — per-key log proved zero arrivals at the app while `H`/`T`/`C`/`E` arrived; NVDA's key echo proved host receipt. Machine-local interception (suspects: global hotkey registrant, NVDA add-on gesture, Parsec host). Resolved per the G18 precedent: `Ctrl+Alt+U` aliases list navigation; `L` remains bound.
9. App crash on focusing a document hyperlink — W1-era `VisualTreeHelper` ancestor walk meeting its first `ContentElement` (round 5 → logical/visual combined walk, regression-pinned).

## Activation addendum (2026-07-27, later passes)

After the activation slice landed, verified live: **click** activation for
all four kinds (resolved wikilink opened its note; unresolved announced
"is unresolved. Cannot open."; external opened the default browser with
the canonical announcement; tag filtered the file list), and **Enter** /
**Ctrl+Enter** at the caret after the containment fix. Two defects found
and fixed by these passes: Enter dead on a landed link (the caret rests
one symbol before a live link element), and navigation re-titling the tab
while the reading surface kept the old note (ReplaceItem disposed the
projection without rebuilding).

**`NVDA+Enter` behaves per NVDA's own semantics, not as an app defect:**
it acts on the *navigator object*. When that is the link (object
navigation, or review landing on it), it invokes cleanly through the
native Hyperlink peer. When it is the whole document, NVDA synthesizes a
mouse click at the object's location — observed as the caret jumping a
line with no activation. Word in focus mode behaves identically. The
W-E7 browse-mode add-on is the idiomatic closure: browse mode makes
`NVDA+Enter` activate at the browse caret.

## Explicitly NOT covered by this record

- **Citation popover, embed-card expansion** — later W3-1/W3-5 slices.
- **Task checkbox toggling** — delivered after these passes (routes the
  builder-stamped source range through the core task command; dirty and
  stale-snapshot refusals announced; caret preserved across the
  re-projection) but not yet human-AT verified.

## Task-toggle field pass (2026-07-26, checkbox slice) — three defects

1. **Every toggle became "Reading view could not load this note" and the
   caret then died app-wide** (arrows pinned at the first character,
   surviving mode toggles; chords announced synthetically without
   moving). Root cause, reproduced by unit test: WPF's undo preservation
   XamlWriter-serializes content removed by the re-projection merge, and
   the checkbox's stamped `(ulong, ulong)` Tag is a GENERIC type —
   "Cannot serialize a generic type", thrown from `Blocks.Clear()`, then
   thrown again applying the failure notice, leaving the surface
   half-merged. Fixed twice over: `IsUndoEnabled = false` (a read-only
   viewer records no undo) and the Tag is now a plain string codec
   (`ReadingSemantics.EncodeTaskRange`). Clipboard XAML serialization is
   separately pinned green.
2. **Space / NVDA-activation at the caret did nothing** — a caret
   position is not element focus, so the checkbox never saw the key and
   only a real mouse click worked. Space (task lines only) and
   Enter/Ctrl+Enter (shared activation path) now toggle the task on the
   caret's line, mirroring the editor's activate-at-cursor semantics.
3. **A click left keyboard focus on a checkbox the next merge
   destroyed** — focus recovery from a removed element is undefined
   (contributor to the dead caret). The click handler returns focus to
   the document, and the merge reclaims focus from embedded children
   before mutating; caret restore failures degrade to the document
   start instead of aborting the merge.

The confusing do-undo announcement sequence ("Task completed." →
"Task reopened." → "the editor no longer matches it") was downstream of
defect 1: the failed merge never updated the checkbox visuals, so
re-clicks toggled against a stale snapshot. Terminal failures now also
log a message-free stack under `SLATE_UIA_DIAGNOSTICS`.
- **JAWS** (installed, unmeasured) and **Narrator** (smoke scope) — pending per WGA-9.
- Size behaviour beyond unit-level measurement; §W-A `inline_runs` goldens.
