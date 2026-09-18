# W7-3 AT navigation and spoken-chord contracts (#749)

Scope: [W7 executable spec](18_windows_port/specs/w7_spec.md) §4. This
contract-only commit precedes implementation and review. The first register
is [the AT navigation map](18_windows_port/at_navigation_map.md), authored
from the Mac construction sites and the shipped Windows mechanisms.
Paths below are relative to `apps/slate-windows/src/SlateWindows` unless
otherwise stated. Planned witnesses are identified explicitly.

## Contracts

**N-1 — One spoken derivation.** Speakable chords come from
`WindowsHotkeySpoken.Spoken(row.WindowsChord)` through `ChordTable`.
`Commands/HotkeyChords.cs` owns the word dictionary;
`Commands/ChordTable.cs:WindowsSpokenFor` exposes the result. Quick Open
help, reading navigation help, splitter help and canvas onboarding compose those results.
The planned `Commands/NavigationHelp.cs` fails clearly for a missing or
chordless ID. It does not introduce another chord parser or key dictionary.
The palette's existing `CommandPaletteViewModel.AccessibleName` derivation
and menu `ChordTextExtension` stay authoritative for their surfaces.

**N-2 — Literal audit and key allowlist.** The planned
`Censuses/ChordSpeechAuditCensus` enumerates every authored shell C# and
XAML file. Roslyn reads C# literals, constant concatenations and
interpolated text; XDocument reads attributes and text. Comments and
inactive C# do not count. Modifier phrases and spoken multiword key names
must be composed, not hand-written. Bare Enter, Return, Escape, Tab,
Space, Up, Down, Home, End, Page Up, Page Down, Delete, Backspace and F2
are allowed. The exact word-dictionary entries in `HotkeyChords.cs` are
definition sites, not consumers; no whole-file exemption is permitted.
Mutation witnesses cover ordinary, raw, concatenated, interpolated and
XAML literals, including an added file, and false positives in comments.

**N-3 — Delivered chords have rows.** Both commands and surface
interactions resolve in `ChordTable.Entries`. Quick Open already has
`windows.quickOpen.openCurrentTab`, `openNewTab`, `openSplitRight`,
`openSplitDown`, `moveNext`, `movePrevious`, and `dismiss` rows; add no
duplicate IDs. Its help names both split directions. Existing
`ChordTableTests` and `CommandDriftTests` continue to pin delivery, not
just equal strings. The table is authored C#; `chords.json` is its generated
projection. W7-3 delivery evidence names the new censuses and live journey.

**N-4 — Display and speech are separate columns.** Menus bind native
`AutomationProperties.AcceleratorKey` to their table-derived `InputGestureText`,
including the editor context menu. Palette Names and HelpText use
the spoken column. The planned FlaUI journey
`SpokenChords_MenusPaletteAndOverlays_MatchTheTable` launches the real
shell, reads every declared menu accelerator and palette row, opens Quick
Open/search/palette through real input and compares their HelpText, then
runs axe. Unit facts pin the reading help and canvas event payload.

**N-5 — The map is the source.** The Markdown map has four groups:
native, rotor, action, content. Each row has a Mac affordance and source,
Windows mechanism and source, patterns, automation IDs, chord IDs, status,
executable evidence and a numbered checklist reference. `-` means no
pattern, automation ID or chord claim. Status is exactly `verified`,
`pending-AT` or `designated`. Verified means the named automated fact
supports the mechanism, never that a human heard it. A designation cites
an existing owner decision. WPF control types such as Tree are not falsely
called UIA patterns. Native controls retain their native patterns.

**N-6 — Completeness and truth are checked both ways.** The planned
`AtNavigationMapCensus` enumerates `.accessibilityRotor`, all
`.accessibilityAction` overloads (including default), `.accessibilityActions` builders,
`NSAccessibilityCustomAction`, `.accessibilityCustomContent` and
`AXCustomContent` under all Mac sources. Each construction site has one
row; missing, duplicate and stale sites fail. Source line citations retain
their original lines after comment removal. Native rotors are documented
separately because framework affordances have no custom construction site.
Windows source citations, named patterns, authored automation IDs, chord
IDs, runnable facts and numbered checklist items resolve. Mutation
witnesses remove/add a site and corrupt IDs, patterns, status or evidence.
Static source checks establish the declared route; the cited behavioral
facts establish the behavior, and human results remain in their checklists.

**N-7 — W8 hand-off.** The map header records that #756 renders
per-platform help from this map and `chords.json`, and owns that help's
drift test. This PR does not change `docs/help/`. The W-C Name/HelpText
cells and W5 checklist gain the new composed help and spoken-chord checks.

## Accepted risks, divergences and owner decisions

- **A-1:** No new editor structural-navigation chords. Editor semantics
  are #747's native Text/Hyperlink route; Outline is the heading-jump
  route. Mac's editor semantic gap remains #1224.
- **A-2:** Stock NVDA/JAWS are the acceptance baseline. Optional W-E7
  layers are enhancements, never required for the mapped route. Human
  cells remain Pending until a named run; Narrator is W8 smoke.
- **A-3:** The owner deferred braille validation on 2026-09-18. The map
  still includes shipped Math braille metadata; it claims no live pass.
- **A-4:** Windows dashboard repair uses its editor, including section
  removal/addition, per accepted D-19 in `25_bases_grid_contracts.md`.
  Do not invent an inline Pick replacement action. Builder condition rows
  expose their expression field and Remove button directly; an
  ItemsControl is not a selectable row provider.
- **A-5:** Canvas has a text filter, tree levels/connection rows and
  navigator commands. UIA has no rotor and the shipped filter is not a
  kind-selector menu. Preserve the W6-1 checklist item 4 equivalence.
- **A-6:** The Mac sweep finds 3 rotor, 21 action and 4 content
  constructions: default actions, the three plural action builders, and AppKit `AXCustomContent` count.
  Dynamic action factories are single construction sites with their
  complete menu projection covered by existing behavioral tests.
- **A-7:** `SwiftSource` is comment-aware, not a Swift compiler. Its
  recorded #1108 limitation for code quoted inside strings remains;
  source excerpts and review supplement the inventory. No claim of
  arbitrary Swift semantic parsing is made.
- **A-8:** Use the recommended key allowlist and add concise table-derived
  reading navigator help (§7.5); do not repeat all 32 rows as a speech wall.
  Heading/link/list/table plus previous-direction guidance are the entry
  points; the exhaustive map carries the remaining rows.

- **A-9:** The live .NET 10 WPF journey disproved the spec's assumption that
  `InputGestureText` automatically reaches AcceleratorKey. The native peer
  reads `AutomationProperties.AcceleratorKey`; an explicit self binding
  supplies it without replacing the native peer or theme. Both editor
  context-menu accelerators also consume the table. This corrects the
  assumed mechanism while delivering N-4's required behavior. See
  [WPF peer source](https://source.dot.net/PresentationCore/System/Windows/Automation/Peers/UIElementAutomationPeer.cs.html).

## Review record

Contract/map round 1 reviewed fixed remote `88ec8440` against `541f7ceb`.
Spec found five P2 documentation/coverage defects: three missing plural
action builders, wrong rail control type, unrelated checklist references,
non-exercising Canvas evidence, and an unrelated grid chord ID. All were
corrected before runtime wiring. The map now has 39 rows (11 native,
3 rotor, 21 action and 4 content). The implementation includes mutation witnesses for source inventory,
map claims and chord literals. Standards independently found four overlapping P2 groups
and additionally identified unproven tag expansion and unrelated action
checklist routes. The tag expansion row now says pending-AT; W5 items
8–10 explicitly cover the sidebar, canvas and graph action routes. Reading
link navigation now cites its navigation suite as well as the range walk.
No heuristic-only findings. Reviews use `24_red_team_protocol.md`: exhaustive invariant
pass, reachable-blocker threshold, same-subsystem stopping rules, and
accepted decisions above excluded from re-litigation.

The real spoken-chord journey also exposed two pre-existing Reading list
axe failures on its list fixture: the Name repeated its control type and
items had no set position/size. N-5's native-list route now exposes an entry
count and the current native ListItem membership. A small STA regression
and the unchanged full-window axe scan pin that repair; no waiver was added.
