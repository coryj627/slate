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

- **Citation popover, embed-card expansion, task toggles** — later W3-1/W3-5 slices.
- **JAWS** (installed, unmeasured) and **Narrator** (smoke scope) — pending per WGA-9.
- Size behaviour beyond unit-level measurement; §W-A `inline_runs` goldens.
