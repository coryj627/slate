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

### Implementation round 1: map validation design reset

The first implementation review (`ce7b7f5a`) found that the plural-builder
correction introduced an unshipped sidebar context-menu route. Combined
with the earlier map blockers, this triggers the protocol's design stop.
The source check also pooled unrelated controls and IDs, allowing an
existing but unrelated pattern or ID to satisfy a row. The next repair
uses this narrower model, specified before changing the census:

1. A Windows source is one or more explicit anchors, separated by `; `:
   `file.xaml#id:AutomationId` selects that element and its descendants;
   `file.cs#class:TypeName` selects exactly that type declaration. A route
   spanning host and child/peer types names each scope explicitly.
2. IDs must be authored inside the selected scopes. Patterns must be
   declared by their custom peers or the native controls (including their
   generated item peers) inside those scopes. Unrelated siblings in the
   same file and IDs elsewhere in the repository cannot satisfy a claim.
   This proves scoped declarations, not arbitrary behavioral equivalence;
   the row's named behavioral facts and human checklist retain that role.
3. Mutation witnesses substitute wrong-but-existing IDs and patterns,
   alongside missing tokens. Mac action discovery includes trailing-closure
   defaults. Multiple constructions of the same group on one line fail
   explicitly, requiring separate source lines/citations rather than
   collapsing multiplicity into set membership.
4. Sidebar plural action builders map to selected-target File actions
   buttons (`SidebarFileActions`), with a matching human route. Mermaid
   refers to the diagram checklist item 4. All remaining rows are reviewed
   against the selected scopes before another review round.
5. The literal audit also joins authored inline XAML text within its text
   owner, so splitting a spoken chord across adjacent Runs cannot hide it.

These changes amend N-2/N-6's witnesses, not the accepted owner decisions.

The scoped repair resolves the four Spec findings and five overlapping
Standards groups from implementation round 1. The source sweep now sees
trailing-closure defaults and rejects repeated constructions on one line;
the XAML audit joins inline Runs. The map and File actions checklist name
the shipped buttons, and Mermaid points to item 4. Wrong-but-existing
ID/pattern mutations pass only when the census correctly rejects them.
Seven map-census facts pass after rebuilding; the affected suite's other
126 cases (reading, chord table/delivery, help, Swift and speech audit)
passed before the final traversal correction. The full real-desktop axe
suite and the next independent fixed-head review remain in progress.

### Implementation round 2: complete factory routes before another repair

The revised File actions checklist accidentally promised folder duplication;
that route must instead observe the canonical files-only refusal. Review
also found that a factory's projection spans several controls. This
repair-created map finding triggers another design stop before map edits.

The two sidebar builders project 14 folder and 13 file VoiceOver actions
from `Sidebar/SidebarActionCatalog.swift:contextualDefinitions`, after its
VoiceOver exclusions. Their Windows routes are distributed: File actions
contains the common file/folder operations (its Open button also opens an
existing folder note); Batch actions contains Add/Remove Tag; Files menu
contains Remove Shortcut and Unpin All; File's named New-from-Template item
contains template creation. The map must name every participating scope
and describe those exceptional routes explicitly. The file/folder action
sets are reviewed against that catalog, not inferred from the builder line.
For the otherwise unnamed Files menu, a `#menu:_Files` anchor selects the
unique MenuItem by exact authored Header, with the same subtree semantics
as `#id:`. No application control or behavior changes are needed.

Separately, the speech audit must handle explicit Inlines and Text property
elements. Codoki's source-parser feedback is addressed with semantic Roslyn
symbols for native PatternInterface fields (qualified, aliased and static
forms), AutomationProperties calls and constructed control types; focused
positive and lookalike-negative facts replace spelling assumptions.

The full factory trace additionally pins the distinction between selected
and checked targets. Add/Remove Tag and batch Move/Delete use checked rows;
single File actions use the selected node. Remove Shortcut first selects
an entry in the Shortcuts list, then uses that expander's Remove button.
Both factory rows therefore also scope SidebarBatchActions,
SidebarShortcutsActions and the checked-row template. `#key:` selects an
exact XAML resource by x:Key, preserving its subtree for declaration checks.
The templates, file-management and W1 sidebar behavioral witnesses cover
these distinct routes; the human checklist tests them separately.

Local full desktop verification passed all 52 tests with the mandatory UIA
gate enabled, zero failures and zero skips (8m29s), including the new
spoken-chord journey and all four new axe scans. Runtime code is unchanged
by the subsequent census and map corrections.

Implementation round 2 found the explicit-property XAML gap, the incomplete
sidebar factory routes and the incorrect folder-duplicate expectation.
Those are corrected according to the second design amendment. The final
local affected run passes 140 tests, including all map/speech mutations,
qualified/aliased/static enum binding, source lookalike rejection, reading
and menu facts, and the added sidebar/template witnesses. The complete
desktop suite's 52 passing tests cover the unchanged runtime revision.

### Implementation round 3: source identity and scalar-text model

Standards reported no findings at `176d619d`; Spec found two census-only
boundary gaps. Three successive speech-audit findings trigger the protocol
stop. Before further code changes, freeze this model:

- A scalar XAML text value is either literal text (attribute, direct text,
  explicit Text property, or typed String) or a markup expression. Every
  markup expression is the same unknown text token for this audit,
  regardless of attribute versus property-element spelling. A literal
  modifier beside that unknown token is still a forbidden hand-composed
  chord. Inline collections recursively concatenate these scalar values;
  explicit collection properties do not change the result. Mutation cases
  must cross scalar representation with implicit/explicit collection form.
- Native pattern inference retains the resolved framework type's namespace
  and assembly identity, including inherited native types. A lookalike
  short name contributes nothing. XAML native controls must have the WPF
  presentation namespace; custom controls need their explicit C# scope.
- An AutomationId getter is evidence only when Roslyn proves its override
  chain reaches WPF AutomationPeer.GetAutomationIdCore. An ordinary method
  with that spelling is not a UIA declaration. SetAutomationId likewise
  resolves to the native AutomationProperties method. Positive inherited
  cases and negative control/peer/getter lookalikes pin these boundaries.

These are declaration witnesses, not a substitute for the row's behavioral
facts. The map routes and runtime behavior remain as independently verified.

The round-3 declaration and scalar-text repairs pass all 35 map/speech
census cases. The scalar mutation fact crosses five modifier spellings,
seven key spellings (including property Binding/MultiBinding and typed
String), and three collection forms: 105 combinations. Native control,
peer-name and getter lookalikes are rejected; inherited native controls
and real AutomationPeer overrides are accepted. No runtime code changed.
