# PR 25 — Sidebar shortcut slots: two slots on one note are two items (D6)

Issue: #1332 (W7-7 PR 3 parity). Size M. Lanes: swift, a11y, windows.
D6 decided 2026-10-16: converge. Parallel with PRs 13, 14, 15, 15b.

## Goal

Under D6 the mac keeps every authored shortcut slot distinct, as Windows
does: one note in two slots is two rows, each with a composed spoken
name, surviving a restart, a rename and a move in order; removing the
second removes the second and says so. The adversarial-repetition bound
becomes a decode cap instead of a collapse.

## Current state on main

- `Sidebar/SidebarOrganization.swift:1051-1060` `decodeShortcuts`
  collapses duplicates first-occurrence-wins ("like pins"); the pins
  decode does the same (`:1034-1039`, round-12 finding 1: no
  per-duplicate downstream work on an adversarially repetitive list).
- `:802-830` `applyRawShortcuts` drops an entry that converges on an
  existing target during a rename or retarget (source-wins).
- `:317-318` the pinned-rows pass pins a duplicated authored row once.
- `SidebarShortcut` (`:706`) is `Hashable` over kind and path (file,
  folder, tag, untagged); slots are positions in the authored array;
  ⌘-number bindings are `sidebarOpenShortcutSlots` (`Sidebar/
  SidebarActionCatalog.swift:246`); the section is labelled "Shortcuts"
  (`Sidebar/SidebarSectionsView.swift:75`).
- These are deliberate FL hardening rules, so this was a recorded
  divergence for the owner to decide (D6: converge), not a defect.

## Windows reference

- `tests/SlateWindows.Tests/SharedNameTests.cs:202-241`
  `OneNoteInTwoShortcutSlotsIsTwoItems`: `AssignShortcut(1)` and `(2)` on
  one note give two distinct items whose hosted names are
  "note.md, file shortcut, note.md, shortcut 1" and "…, shortcut 2";
  removing the selected second leaves the first. `:248`
  `TwoShortcutSlotsOnOneNoteSurviveARestartARenameAndAMove`.
- `FilesSidebarViewModel.cs:378-389` `SidebarShortcutViewModel`:
  `AutomationName` = "{DisplayName}, {KindLabel} shortcut"; the R-4
  shared-name rule appends the slot distinguisher when names collide.
- #1301 AR-58: the removal announcement names the slot.

## Design (D6: converge)

1. **Decode.** `decodeShortcuts` keeps every entry in authored order up
   to a cap (64, the sidebar retained-writes bound); entries past the cap
   are ignored and the count reported on the sidebar status line (never
   silently). Pins keep their collapse (a pin set is a set; out of
   scope).
2. **Rename and retarget.** `applyRawShortcuts` keeps a converging entry:
   after `a.md` → `b.md` with `b.md` already in slot 2, slots 1 and 2 both
   point at `b.md`, in order.
3. **Identity.** Rows are identified by slot index, not by (kind, path):
   `SidebarShortcut` gains a `slot` ordinal (or the row model wraps it),
   so SwiftUI identity, selection and the ⌘-number bindings stay stable
   when two slots share a target.
4. **Spoken names.** A row's accessibility label is "{name}, {kind}
   shortcut"; when two or more rows share a display name, each appends
   ", shortcut N" (the R-4 shared-name rule, same words as Windows).
5. **Removal.** The removal announcement names the slot's composed name
   captured before removal.
6. The pinned-rows pass (`:317`) is unrelated to shortcuts; leave it.

## Changes

- `Sidebar/SidebarOrganization.swift:700-720`, `:795-830`, `:1045-1060`.
- `Sidebar/SidebarSectionsView.swift` (row identity and labels);
  `SidebarActionCatalog.swift` if the slot bindings resolve by path.

## Tests

(`SidebarShortcutsTests`, `SidebarOrganizationTests`, mirroring the C#
names.)
- `testOneNoteInTwoShortcutSlotsIsTwoItems` (names as Windows').
- `testTwoShortcutSlotsOnOneNoteSurviveARestartARenameAndAMove`.
- `testRemovingTheSecondSlotRemovesTheSecondAndNamesIt`.
- `testTheDecodeCapRefusesOverflowWithoutCollapsing` (65 entries: 64
  rows, a status line).
- `testPinsStillCollapseDuplicates` (regression for the untouched rule).

## Accessibility

Two rows with one name become distinguishable by the slot ordinal, the
reason Windows made this call (R-4). The status line for the cap is
host-composed: use the existing sidebar status mechanism and, if it is
an announcement, add the residue marker.

## Docs and registers

`docs/plans/40_nvda_matrix_remediation_contracts.md`: the PR 3 record's
mac note resolved. Plan 41 §6's #1332 line updated.

## Acceptance

- [ ] The five facts pass; the cap replaces the collapse for shortcuts
      only.
- [ ] Contract 40's PR 3 record updated; lanes green.

## Risks

Slot identity touches SwiftUI `ForEach` ids in the sections view; a
path-keyed id would duplicate. The first fact catches it.

## Brief

Converge the mac sidebar's shortcut slots to the Windows model (D6).
Read `Sidebar/SidebarOrganization.swift:700-720`, `:795-830`,
`:1030-1060`, `Sidebar/SidebarSectionsView.swift`, the Windows
`SharedNameTests.cs:198-262` and `FilesSidebarViewModel.cs:378-389`.
Keep every authored slot (cap 64, overflow reported, never collapsed),
keep a converging entry on rename, identify rows by slot, compose
", shortcut N" into the label when names collide, and name the slot on
removal. Leave the pins collapse alone. Write the five twin-named facts
first. Update contract 40's PR 3 record. Done when the facts pass and
the lanes are green.
