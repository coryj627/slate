# PR 16 — Reading view: native list semantics for VoiceOver (G20, D5)

Issue: #1048. Size L (spike, research, then implementation). Lanes:
swift, a11y, windows, plus a VoiceOver field pass. Depends on D5's two
steps. Parallel with PR 17 (no shared files).

## Goal

VoiceOver users can navigate the reading view's lists as lists: jump
between items and in and out of nesting, hear position and depth from
structure rather than from a value string, keep the authored ordinal,
and keep task-row checkbox semantics. This is the mac half of the owner
call that gave Windows native `List`/`ListItem` peers (gap G20).

## Current state on main

- `Reading/ReadingView.swift:313-320`: the populated state is an eager,
  flat `ScrollView { VStack }` of one view per block (VoiceOver
  enumerability over laziness; the type doc at `:42-56`).
- `:393-397`: `.listItem(depth, ordered, _)` renders `taskRow` (`:618`) or
  `listItemRow` (`:596`). Both rows are `.accessibilityElement(children:
  .contain)` with `.accessibilityValue("list item, level \(depth + 1)")`
  (`:611`, `:643`); the marker `Text` is `.accessibilityHidden(true)`
  (`:605`) and carries core's authored `listMarker` (#967), so ordinals
  are never renumbered. Rows are siblings in the flat VStack; nesting is
  padding only (`:609`).
- The AX-element precedent in this app: `CanvasRendererView.swift`
  (`CanvasCardAXElement`) and `GraphDiagramView.swift` build
  `NSAccessibilityElement` trees for custom surfaces.

## Windows reference

`apps/slate-windows/src/SlateWindows/Reading/ReadingSurface.cs:1131-1215`:
`ReadingListPeer` (control type List; name "{N} entries"; children: one
`ReadingListItemPeer` per item, and each nested list as a sibling peer
right after its owning item, so depth is enumerable) and
`ReadingListItemPeer` (ListItem; name = the item's text; PositionInSet
and SizeOfSet; not keyboard-focusable, because NVDA's list quick-nav
needs non-focusable items). `w_c_matrix.md:20` records "list Name is its
entry count and list items publish their current PositionInSet/
SizeOfSet". `gap_analysis.md:57` (G20).

## Design

**Step 1, the spike (half a day, a written result in the PR).** Which AX
shape VoiceOver honours for list navigation in a native (non-web) macOS
app. Candidates to try with the Accessibility Inspector and VoiceOver:
- an `NSAccessibilityElement` subtree with role `.list` (subrole content
  list) whose children are group/item elements with the item text as
  their description, nested lists as children of their owning item;
- an outline shape (`.outline` with `.row` children carrying disclosure
  level) which VoiceOver's native item navigation does honour;
- SwiftUI-only means: a custom rotor (`.accessibilityRotor`) listing
  list items so VO-U jumps between them, plus `.accessibilityElement
  (children: .contain)` containers per list, with position in set
  conveyed through the label.
Record for each: what VoiceOver speaks on entering the list, whether
VO-arrows move item to item, whether nesting is enterable, and whether
the ordinal is spoken.

**Step 2, the research (D5).** How other macOS apps expose nested lists
to VoiceOver (TextEdit and Pages through `NSTextView`'s list text
attributes; Notes; a native app with a custom document view). Record the
shapes found and pick the one that gives item-to-item and nesting
navigation with the least custom AX.

**Step 3, implement.** Wrap each top-level list (a run of `.listItem`
blocks) in the chosen container; items keep their text as leaves (every
text leaf stays a distinct `Text` with `.textSelection(.enabled)` per
leaf, the continuous-read rule at `:54-56`); ordered items speak the
authored marker; task rows keep the checkbox as a child control; the
value string may remain as a supplement but is no longer the only
carrier of depth. The eager, flat enumeration rule stands (no laziness).

## Changes

- `Reading/ReadingView.swift` (`:313-320`, `:393-397`, `:596-650`), a new
  `Reading/ReadingListAccessibility.swift` for the container.

## Tests

- `ReadingViewTests`: the list container exists per top-level list; items
  are children in document order; a nested list is reachable under its
  owning item; ordered items carry the authored marker; task rows keep
  the checkbox; the value string, if kept, still says the level.
- Re-run the §W-A reading-view structure goldens (both twins serialize
  them); update the mac serializer if the shape adds elements.
- `a11y-check` 100.

## Accessibility (the proof)

A VoiceOver field pass with `scripts/vo.sh` on a fixture note with a
three-level nested list, an ordered list starting at 3, and a task list:
record what VO speaks entering the list, moving item to item, entering
and leaving a nested level, and on the task checkbox. The PR is not
mergeable without this record.

## Docs and registers

`docs/plans/18_windows_port/specs/gap_analysis.md:57` G20 closed on mac;
`docs/plans/18_windows_port/w_c_matrix.md:20` reading-view row updated
with the mac shape; `docs/runbooks/voiceover-feature-test.md` §B gains
the list-navigation steps.

## Acceptance

- [ ] Spike and research results are written in the PR.
- [ ] Items and nesting are navigable with VoiceOver (recorded phrases).
- [ ] Authored ordinals and task checkboxes unchanged.
- [ ] Facts and goldens pass; registers updated; lanes green.

## Risks

SwiftUI's accessibility API has no list role on macOS; the shape may
require an AppKit `NSViewRepresentable` container and a hand-built AX
tree, which must stay in sync with the eager VStack. Keep the spike
honest: if no shape gives true list navigation, record the platform
limit in G20 and ship the best partial (rotor plus structure) rather than
claiming parity.

## Brief

Give the mac reading view real list semantics for VoiceOver. Read
`Reading/ReadingView.swift:40-62`, `:310-330`, `:386-400`, `:596-650`,
the Windows `ReadingSurface.cs:1125-1215`, `w_c_matrix.md:20` and
`gap_analysis.md:57`. Run the spike over the three candidate shapes and
write the result; then research how TextEdit, Pages and Notes expose
nested lists to VoiceOver and write that; then implement the chosen
shape with the authored ordinal and task checkbox preserved and the
eager, flat text leaves untouched. Add the structural facts, re-run the
§W-A goldens, do the VoiceOver field pass and record the phrases. Close
G20 and update the matrix row and runbook. Done when the phrases prove
item-to-item and nested navigation (or the platform limit is recorded
with evidence) and the lanes are green.
