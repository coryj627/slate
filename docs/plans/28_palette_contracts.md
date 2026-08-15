# W5-1 — Command palette, the chord table, and the drift tests (#741): contracts

Scope (spec §W5-1): the command palette over core ranking; the **command
registration bridge** that gives the palette something to rank; one
declarative mac-chord → Windows-chord table as the single source of truth
for menu accelerators, palette display, spoken hotkeys (W7-3) and help
docs; and Windows twins of the command-drift tests. Written BEFORE
implementation per `24_red_team_protocol.md` §0. The divergence (PD) and
accepted-risk (PR) registers are deliberate owner-recorded decisions and
are **off-limits for review re-litigation** — a reviewer may report that
an entry is factually wrong, not that the trade-off should be re-made.

Contract numbering is per-wave. "P7" here is unrelated to any other
document's contract 7.

## The finding that shapes this issue

The issue text says "palette over the core registry". **There is no
registry.** `apps/slate-windows/src` contains zero `slate.*` id literals;
`CommandRegistry` and `CommandAction` are generated into the binding
(`SlateUniffi/generated/slate_uniffi.cs:9024`, `:8682`) and referenced only
by test censuses under synthetic `census.*` ids. The app's 125
user-invokable command objects — 121 `ICommand` properties across 11 files
plus 4 static `RoutedCommand`s in `Grids/AccessibleDataGrid.cs:71-85` — are
wired directly to XAML `KeyBinding`s, menu `Command=` bindings, and
code-behind handlers.

So the registration bridge (P2–P4) is a **prerequisite phase**, not a
detail of the overlay. The palette, the registration-forward drift test,
and the chord table all assert against a registry that this issue must
first populate.

## Contracts

**P1 — Core ranks, the host renders.** `palette_sections(commands, query,
recent_ids, sidebar_pinned_order)` owns ranking, match ranges, section
membership, section order, section titles, Recent blending, and Sidebar
catalog placement. The host re-implements none of it. Three specific
prohibitions, each with a live failure mode:

- **Section order is not enum order.** `SECTION_ORDER`
  (`crates/slate-core/src/palette.rs:86-99`) is File, Navigation, View,
  Vault, Editor, **Canvas, Bases, Graph, Sidebar**, Tasks, Settings,
  Plugins; the generated `CommandSection` enum declares the four middle
  ones after Plugins. The host renders the returned array **in the order
  returned** and never sorts by enum value.
- **`PaletteRow.Score` is not a display order.** It exists so a host can
  identify the strongest match overall without re-scoring. It has exactly
  two legitimate consumers: the filter count and a strongest-match
  assertion. Sorting the rendered list by it desynchronises navigation
  from what the user sees.
- **`PaletteSection.Title` is canonical copy**, rendered verbatim. The
  host never maps a `CommandSection` to a heading string.

**P2 — One registry, host-owned, checked at registration.** The app holds
exactly one `CommandRegistry` for its lifetime. `Register` returns `true`
when it **replaced** an existing id — the Rust doc calls silent override of
a `slate.*` id a privilege-escalation footgun and requires the caller to
reject conflicts at the registration site. The bridge therefore treats a
`true` return as a fatal registration conflict (fail fast at startup, not a
log line), and a unit fact proves a duplicate id throws.

**P3 — The declared id catalog is the stability contract.** A C# catalog
of `slate.*` ids — the twin of mac's `SlateCommandID` — is the authored
list; the registry is what the app actually holds. Ids are **byte-identical
to mac's** for every capability that exists on both platforms; a
Windows-only capability takes a new id under the same namespace scheme.
Labels, accessibility hints, and sections are supplied at registration and
match mac's for shared capabilities, because they are what the palette
ranks and displays.

**P4 — The snapshot rule.** The palette calls `Registry.List()` **once, on
open**, and ranks that array for its entire lifetime; the recents id list is
snapshotted at the same moment. Neither refreshes while the palette is
open — a command invoked in this session does not appear under Recent until
the next open. `List()` sorts and clones every command, so a per-keystroke
call is also a performance defect.

**P5 — The overlay is the Quick Open shape, never a `Popup`.** A WPF
`Popup` is a separate HWND whose UIA subtree is a sibling window
(`MainWindow.xaml:2098-2105`). The palette is an in-window overlay built on
the W1-4 Quick Open precedent, which already solves what the palette needs:
three-layer modality (shell roots and menu disabled by `DataTrigger`, plus a
full-window hit-test scrim), `FocusManager.IsFocusScope`,
`TabNavigation="Cycle"`, `AutomationProperties.IsDialog="True"`, and a
shell-shortcut swallow list so chords do not fire underneath the open
overlay. `Visibility` binds through
`RelativeSource={RelativeSource AncestorType=Window}` — binding it on the
same element that overrides `DataContext` resolves against the wrong
context and sticks at `FallbackValue=Collapsed` forever, which shipped
twice before (W4-5, W4-6).

**P6 — One accessible name per row; bolding is presentation only.**
`MatchSpan` carries **UTF-8 byte offsets** into the label
(`palette.rs:49-51`); C# strings index UTF-16. Conversion happens at
exactly one place (PINV-6). The row exposes ONE
`AutomationProperties.Name` — `label`, or `"{label}, {spoken chord}"` when
the command has a chord. The bolded runs, the chord text, and the
unavailability caption are all presentation-only
(`AutomationPresentationTextBlock`, or `AccessibilityView="Raw"`); none
becomes a separate UIA stop. Rows are hosted on elements that **have** a
peer — a bare `Panel` or `Border` gets none and the composed name is
silently dropped (`AutomationLandmark.cs:10-14, 28-31`). An empty
`LabelMatchSpans` on a non-empty query is a hint-only match: render the
label fully unbolded, never bold-everything, never hide the row.

**P7 — The keyboard model.** `displayOrder` is one flat cycle over
`sections.flatMap(rows)`. Section headers are headings, not selection
stops. Down from the last row wraps to the first and Up from the first
wraps to the last; from no selection, Down lands on the first row and Up on
the last. Enter invokes; bare Escape closes; modified arrows pass through
(VoiceOver/NVDA navigation keys must reach the AT). Selection is
**preserved** when the selected id survives a query change, snaps to the
first row only when it vanished or was null, and becomes **null** on zero
matches. WPF's `ListBox` defaults — no wrap, focusable group headers,
reset-to-index-0 on `ItemsSource` change — are all wrong here and must be
overridden. See PD-1 for Home/End/Page keys.

**P8 — Unavailable commands are shown, selectable, and refused at the
gate.** An unavailable command keeps its row, keeps its place in the
selection cycle, and remains arrow-reachable; only activation is blocked.
`IsEnabled="False"` is **forbidden on the row** — it removes the element
from keyboard navigation and from the UIA tree, which is the opposite of
the contract. The reason is surfaced three ways: a visible caption, the
row's `HelpText`, and appended to the selection announcement. One
availability resolver serves the row state, the announcement, and the
Enter gate, so those three cannot disagree, and it is re-evaluated at
invoke time rather than trusted from render time.

**P9 — Invocation ordering.** Exactly: (1) restore focus to the palette's
search field, unconditionally, **before** any availability check; (2) if a
disabled reason applies, announce it and **return without reaching the
registry**; (3) invoke through `CommandRegistry.InvokeById`; (4) **on
success only**, record the recents transition, **then** dismiss. Every
non-success outcome — `ActionFailed`, `UnknownId`, unavailable — leaves the
palette **open** while a High-priority announcement plays. A `finally`-block
dismissal breaks this.

**P10 — Announcements are canonical, and their triggers are the contract.**
Six events, rendered by core, never composed in C#:
`CommandPaletteNeedsVault`, `PaletteCommandSelected`, `PaletteFilterCount`,
`PaletteCommandFailed`, `PaletteCommandNotFound`,
`PaletteCommandUnavailable`. Trigger rules that are easy to get wrong:

- `PaletteFilterCount` fires on **every non-empty** keystroke with **no
  debounce**, and is **suppressed entirely on an empty query** (opening the
  palette announces nothing). It posts at Medium so the AT coalesces;
  raising it to `ImportantMostRecent` produces interrupted mid-word garbage
  at typing speed.
- `PaletteCommandSelected` is **suppressed for the first selection change
  after open** — the initial row is not announced before any user action.
- `PaletteCommandUnavailable` renders the reason **verbatim, with no
  prefix**. The row already displays that sentence; composing
  `"Unavailable: {reason}"` host-side makes the AT say it twice, differently.
- An `ActionFailed` whose message equals the structural-busy reason routes
  to `PaletteCommandUnavailable`, not `PaletteCommandFailed`.

The palette posts through the existing injected `Action<A11yEvent>` —
`AccessibilityNotificationDispatcher` is "one canonical funnel" and its
shared `ActivityId` is what makes successive announcements coalesce. A
second dispatcher instance is a defect.

**P11 — Recents persistence.** Global, device-local, never per-vault:
`%LOCALAPPDATA%\Slate\command-palette-recents.json`, the same v1 byte
format core encodes (pretty JSON array, 2-space indent). The host owns only
the path and the I/O; **core owns every state transition** — the LRU goes
through `PaletteRecentsAdd`, not a hand-rolled C# list operation. Reads are
**bounded**: open once and read `maxFileBytes + 1` bytes rather than
stat-then-read (TOCTOU), where the guard is `>` — a file of exactly 65536
bytes is accepted and 65537 is refused **before** decoding, even when its
JSON is valid. Writes are atomic (temp file + replace); a plain truncating
write can tear and silently decode as empty.

**P12 — The chord table is the single source of truth.** One checked-in
declarative table maps command id → mac chord → Windows chord → Windows
spoken string, and is the **only** place a Windows chord string is
authored. Menu `InputGestureText`, palette display strings, W7-3 spoken
hotkeys, and (in W8-6) the help-doc chord tables all read from it. Its
required coverage is every chord the app actually delivers — which at the
start of this issue was roughly triple what `chords.json` recorded (35
chords on `main`); see PR-3. Two rules the current
data lacks:

- **Modifier mapping is declared, not improvised.** ⌘→Ctrl and ⌥→Alt are
  decision 12. ⌃ has no stated rule and today maps two different ways
  (⌃⌘[ → Ctrl+Alt+[, but ⌃1 → Ctrl+1); the table states the rule it
  follows and marks every deviation as a recorded divergence with a reason.
- **Spoken strings are walked over the Windows chord, not substituted into
  the mac one.** Token order inverts: `⇧⌘O` speaks "Shift Command O" while
  `Ctrl+Shift+O` speaks "Control Shift O". `windowsSpoken` today has zero
  producers and zero consumers — 35 hand-authored literals — so W5-1 ships
  the producer and a fact that every table row's spoken string matches it.

**P13 — The drift tests.** Three Windows twins, all in CI:

- **(a) Registration-forward.** The declared id catalog (P3) and the live
  registry hold the same set, in both directions, and every registered
  command is reachable through the palette. Allow-lists carry a mandatory
  reason and are checked for **staleness in both directions** — mac's
  tests assert that an allow-list entry with no live counterpart is itself
  a failure, which is what makes them non-bypassable, and the twin adopts
  it.
- **(b) Menu-scrape-reverse.** `MainWindow.xaml` is purely static — nothing
  in production code mutates the menu — so the twin parses the XAML with
  `XDocument` and needs no app launch. Every menu item is backed by a
  registry command, and every `InputGestureText` string agrees with the
  chord table. Note that mac gets display and delivery from one
  `keyboardShortcut(...)` construct while Windows separates them, so the
  twin asserts **both** directions and their agreement; three menu leaves
  are not command-backed today and need explicit dispositions (PR-4).
- **(c) Chord-table drift.** Every chord the app delivers appears in the
  table, and every table row corresponds to a live binding. Per the owner's
  2026-08-13 call, the `docs/help/` per-platform chord tables and their
  drift test remain with **#756 (W8-6)**, which owns docs wholesale;
  authoring Windows chord tables for canvas and graph before W6 ships those
  surfaces would document surfaces that do not exist.

**P14 — Host copy is verbatim and inventoried.** Three host-composed
strings, absent from core's vocabulary, transcribed exactly — including the
deliberate asymmetry that the visible no-matches line quotes the query while
its accessible name does not:

- empty registry: "No commands available" / "Open a vault to access the palette."
- no matches: "No matches" / "No command matches \"{query}\". Try fewer letters or a different word."
- accessible name for no matches: "No command matches {query}. Try fewer letters or a different word."

The needs-vault case is **not** an empty state: the palette does not open at
all, and the flag must never be set without a vault or the next vault open
auto-presents an empty palette.

## Invariants

- **PINV-1 — No announcement text is composed in C#.** Every spoken string
  comes from `A11yRender` or from `palette_sections`. New host-composed
  copy requires a recorded `// W0.5-3 residue:` marker and raises the §2.6
  budget, which is an owner decision. **The owner approved a two-string
  increase on 2026-08-13** for the palette's availability copy — "Open a
  vault to use this command." and "This command is not available right
  now." (`SlateCommandRegistrar`). Mac carries richer per-capability
  reasons; matching them needs a capability model W5-1 does not build, and
  a generic reason spoken verbatim beats no reason at all. The increase is
  **pinned, not merely commented**: a fact asserts the palette's whole
  invocation surface produces exactly these two texts and no third, so the
  approved budget cannot drift upward silently the way an unpinned
  convention would.
- **PINV-2 — No ranking, ordering, or title rule exists host-side.**
  `fuzzy_score` and `section_title` are deliberately not exported; any C#
  equivalent is drift by construction.
- **PINV-3 — Exactly one `CommandRegistry` instance and one
  `AccessibilityNotificationDispatcher` for the shell.**
- **PINV-4 — Every registered command is reachable from the palette**, and
  every palette row invokes through `InvokeById` rather than calling an
  `ICommand` directly. The Quick Open precedent shows the failure mode:
  six of its eight commands are dead because the code-behind calls the
  underlying methods directly, bypassing `CanExecute` entirely.
- **PINV-5 — A Windows chord string is authored in exactly one place**
  (the chord table). Menus, palette rows, and spoken hotkeys read it.
- **PINV-6 — Byte→UTF-16 span conversion happens in exactly one helper**,
  with a fact covering a non-ASCII label. This is live, not theoretical:
  28+ shipped labels end in `…` (3 bytes, 1 char), and mac registers one
  palette command per saved query as `"Run query: {name}"` with arbitrary
  user text mid-label.
- **PINV-7 — Command-state refresh enumerates the registration table.**
  The four hand-maintained `RaiseCommandStates` lists are a verified drift
  surface — `ToggleReadingModeCommand` gates on `ActiveTab?.IsMarkdown` and
  is omitted from its list today. Registered commands requery by
  enumeration; a new command cannot be silently omitted.

## Recorded divergences (off-limits for re-litigation)

- **PD-1 — Home / End / Page Up / Page Down navigate the palette list on
  Windows. APPROVED 2026-08-13, and mac converges up.** Mac deliberately
  handles none of them (fn+arrows pass through to its text field). WPF's
  `ListBox` provides all four, they are standard Windows list behavior, and
  §W-C conformance expects them. The owner's call was **not** to keep this
  as a permanent divergence: mac gains the same keys under
  [#1105](https://github.com/coryj627/slate/issues/1105), on the reasoning
  that they are useful in a long list on any platform even though they are
  not a macOS palette convention. **This entry retires when #1105 lands** —
  until then it is a convergence-pending divergence, not an accepted one.
- **PD-2 — The palette chord is Ctrl+Shift+P**, the direct ⇧⌘P map and the
  Windows convention. Verified unbound across every chord surface in the
  app. Non-toggling, matching mac: pressing it while open re-opens rather
  than closes; Escape closes.
- **PD-3 — Recents transition routes through core's `PaletteRecentsAdd`.**
  Mac hand-rolls the same LRU in Swift and does not call it. Behaviourally
  identical today, but core owns the policy, so the Windows host calls it;
  converging mac is a follow-up, not this issue's work.

## Accepted-risk register (off-limits for re-litigation)

- **PR-1 — Ctrl+Alt+{Left,Right,Up,Down} collide with JAWS table-reading
  commands, and with Windows Magnifier.** *(Basis materially changed
  2026-08-15 — the owner reports Magnifier runs roughly half the time on
  their machine and intercepts all four chords to pan the magnified view.
  That is a first-party Microsoft accessibility tool, not vendor
  software: in an accessibility-first app, these chords are unreachable
  for exactly the low-vision users most likely to be running it. The
  record below stands as W5-1's disposition, but the trade-off deserves
  re-deciding on this new information rather than being carried
  forward.)* These pane-focus chords shipped in W1-3 and `w3_spec.md`
  states the project's own rule that chords are vetted against AT binding
  tables precisely because JAWS owns this family. W5-1 **records** the
  conflict in the chord table rather than rebinding, because the issue's
  adjudication clause is explicit that already-shipping chords are recorded,
  not re-bound. Flagged to the owner as the one entry where recording may
  be the wrong answer; a rebind is its own issue.
- **PR-2 — Escape has roughly fifteen claimants** across window preview,
  overlays, editor, grid, and property templates, with precedence living
  implicitly in guard conditions. W5-1 adds the palette to this chain
  deliberately (innermost-first, gated on the palette being open) and
  documents its position; rationalising the whole chain is out of scope.
- **PR-3 — The chord table's coverage debt is inherited, and W5-1 pays it
  down rather than auditing every binding's correctness.** Beyond the 37
  catalogued entries the app also delivers 32 `ReadingNavigator` chords,
  grid Ctrl+F and Ctrl+Alt+S, editor Ctrl+E and Ctrl+Enter, property-row
  Ctrl+Backspace, F2 rename, Ctrl+1–9, and the Quick Open Enter-modifier
  family. Two documentation claims were false when this was written — `w3_spec.md`
  said the reading chords were "registered in chords.json" and both
  `WorkspaceTemplates.xaml` and `w_c_matrix.md` cited chords.json for
  Ctrl+Backspace, while no entry existed. **Both are true as of this
  issue.** The table records what ships; it
  does not re-adjudicate whether each binding was a good choice.
- **PR-4 — Eleven command objects have no invocation path and need
  dispositions, not silent registration.** Six `QuickSwitcherViewModel`
  commands are dead because the code-behind bypasses them; five more
  (`FocusNextPaneCommand`, `FocusPreviousPaneCommand`, `ToggleTagsCommand`,
  `ToggleDualPaneCommand`, `ToggleBooleanCommand`) have no binding at all.
  The registration-forward test forces the question. Disposition rule:
  overlay-scoped interactions are **not** app commands and are not
  registered (they are interactions of an open surface, like arrow keys in
  a list); capabilities with no surface are registered so the palette
  becomes their surface. Each decision is recorded in the catalog with a
  reason.
- **PR-5 — `Ctrl+S`, `Ctrl+Shift+[`, and `Ctrl+Shift+]` have no menu home
  and emit no UIA `AcceleratorKey`.** Registering them surfaces them in the
  palette, which is a real discoverability improvement, but adding menu
  items for them is a menu-design change beyond this issue.

## Resolved before implementation

These four were open when the contracts landed and are now decided; each
becomes a binding contract line rather than an implementation choice.

**P15 — Invocation is dispatcher-affine.** `InvokeById` is a synchronous
FFI call and `ForeignActionAdapter` invokes the foreign action on the
calling thread, so a `CommandAction` invoked from the UI thread runs on the
UI thread with no marshalling. The registry is therefore **only ever
invoked from the dispatcher thread**; the action adapter asserts
`CheckAccess()` and throws otherwise. This is deliberately stricter than the
`enqueueUi` pattern the scan and vault listeners use — those receive events
from Rust-owned background threads, whereas command invocation is always
user-initiated from a UI surface. If a future caller needs background
invocation it marshals to the dispatcher first; the registry does not
acquire an `enqueueUi` seam speculatively.

**P16 — `sidebar_pinned_order` is the action-catalog order, declared by the
bridge.** The question's premise was wrong: `FilesSidebarViewModel._pinned`
and `._recents` hold **file paths**, not command ids, and are unrelated to
this parameter. Core wants the sidebar *action catalog's* id order, which
mac supplies from `SidebarActionCatalog.actions.map(\.id)`. It matters
because `Registry.List()` sorts by `(section, id)`, so passing an empty
list renders the Sidebar section alphabetically rather than in catalog
order. The bridge declares an explicit ordered id list mirroring mac's
catalog order and passes it — data, not policy, exactly as the FFI doc
specifies.

**P17 — One app-lifetime registry, registered once at shell
construction.** Commands are registered a single time; their actions
resolve live state through a provider rather than capturing a workspace
instance, so vault open and close never mutate the registry. This keeps
P2's "a `true` return is a fatal conflict" rule meaningful (re-registering
per vault would make replacement the normal case) and makes the
registration-forward drift test deterministic. Vault-scoped commands refuse
through the availability gate; the palette itself refuses to open without a
vault via `CommandPaletteNeedsVault`, so a registry holding vault-scoped
commands with no vault open is a reachable but harmless state.

**P18 — `PaletteSections` is computed once per query change**, stored on
the view model, and read from that field by rendering, navigation, and the
filter count. Mac re-enters the FFI several times per render through
computed properties; the Windows host does not, and the stored value is
keyed to the open snapshot, so no cache-invalidation question arises
(P4 makes the snapshot immutable for the palette's lifetime).

## Design pass — modal surfaces and key routing (stopping rule 4, 2026-08-13)

`24_red_team_protocol.md` rule 4 fired: three consecutive rounds produced
blockers in one subsystem, so this is written **before** more code.

| Round | Blocker | Where |
|---|---|---|
| 1 | Blanket modifier swallow killed text editing and PD-2 | `Window_PreviewKeyDown` palette branch |
| 2 | AltGr still swallowed; palette/Quick Open exclusion unenforced | same branch |
| 3 | Palette opens on top of the other seven sheets | same branch |

**Why each fix was incomplete.** The branch answers three different
questions in one `if` chain, and every round fixed one of them:

1. *Does the palette want this key?* (navigation, Enter, Escape)
2. *Would this key fire a shell command underneath the overlay?*
   (modality suppression)
3. *Does the focused text field need this key?* (editing passthrough)

Question 2 is not the palette's question at all — it belongs to whatever
modal surface is open. The app has **nine** `IsDialog` overlays declared
as siblings in one `Grid` cell with no `Panel.ZIndex` anywhere, so paint
order is declaration order and "which surface is on top" is implicit in
the XAML's line numbering. Each overlay hand-rolls its own modality:
its own scrim, its own `PreviewKeyDown` on its own element, and — for
Quick Open and the palette only — `IsEnabled` `DataTrigger`s on the
shell roots. **Nothing knows the set of open surfaces**, which is why a
guard written for one of them silently omits the other seven.

**The design: one modal-surface registry, one routing decision.**

- **`ModalSurface`** — an enum or ordered list naming the nine overlays
  and their precedence. Declaration order becomes explicit data instead
  of an accident of XAML line numbers.
- **`MainWindow.OpenModalSurface`** — a single computed property
  returning the topmost open surface, derived from the view-model flags
  that already exist (`QuickSwitcher.IsOpen`, `Palette.IsOpen`,
  `Workspace.AddPropertySheet`, `BulkRenameSheet`, `CitationDetails`,
  `CitationSummary`, `FilesCiting`, dashboard editor,
  `BaseQueryBuilderSheet`). No new state.
- **Opening is gated on it.** A surface may only open when nothing of
  higher precedence is up; the palette chord refuses (or dismisses the
  incumbent, per surface) rather than stacking. This replaces the
  one-off Quick Open dismissal round 2 added.
- **Key routing asks the registry, not the palette.** While a modal
  surface is open the window handler consults that surface's descriptor
  for the three questions above, so suppression policy lives with the
  surface rather than being re-derived in the palette's branch.
- **Text-editing passthrough becomes a property of the descriptor**, not
  a special case: a surface that owns a text field declares it, and the
  allow-list (including the AltGr arm) is applied once, in one place,
  and is unit-testable because it stops being a `private static` on
  `MainWindow`.

**Scope discipline.** Retro-fitting all nine overlays onto the registry
is larger than W5-1 and would rewrite W4-4/W4-5/W4-6 surfaces this issue
does not own. W5-1 therefore ships:

1. the registry and the `OpenModalSurface` predicate,
2. the palette gated on it — which fixes the round-3 blocker for all
   seven sheets, not just Quick Open, and
3. the routing/allow-list extraction, made `internal` so the whole table
   is pinned by unit facts rather than only by a journey.

Converting the other eight surfaces to open through the registry is
filed as follow-up work, with the registry already in place to convert
onto.

## Round record

Per `24_red_team_protocol.md` §Per-round record.

### Round 1 — four dimensions (correctness, accessibility, completeness/drift, lifecycle/re-entrancy)

Heavy convergence: three findings were reported independently by three or
four reviewers, which removed any doubt they were real.

**Fixed — each case where the code contradicted something this branch asserted:**

- **The modal swallow ate text editing and PD-2** (4/4 reviewers). Every
  modified key was marked handled at the Window's `PreviewKeyDown`, the
  tunnel root, and WPF runs a `TextBox`'s `InputBindings` only for
  UNHANDLED events — so Ctrl+A/C/V/X/Z and every Shift-selection key were
  dead inside the palette's own search box, and AltGr (which WPF reports
  as Ctrl+Alt) made several characters untypeable on German, French and
  Polish layouts. The same line ate Ctrl+Shift+P, leaving PD-2
  unreachable. Fixed with a text-editing allow-list; the shell-chord
  deny-list could not serve alone because it predates Ctrl+J,
  Ctrl+Shift+E and Ctrl+R.
- **Focus restore stole focus back from the invoked command** (3/4). A
  command that opens its own surface queues its focus at the same
  dispatcher priority during the invocation, so the palette's restore ran
  last and undid it.
- **Nothing dismissed the palette on a vault transition**, so P14's
  forbidden state was reachable — permanently, if the new vault failed.
- **The license-header CI gate was failing** on a missing SPDX header.
- **The palette's own chord was absent from the chord table**, in the
  issue whose job is to make that table authoritative. The drift test
  scrapes only declarative sources and Ctrl+Shift+P is delivered
  imperatively; now recorded with an allow-list entry.
- **PINV-7 was false as shipped** — the enumerating refresh had zero
  production callers. Now wired.
- **The empty state published nothing to UIA** (its name sat on a bare
  `Panel`, which creates no peer), and **a closed palette left its search
  box and results list in the tree** (neither child carried a
  `Visibility` of its own, so the guarded list type was inert).
- **A throwing `ICommand` escaped the palette's catch chain** as a
  non-`CommandException` and reached the dispatcher.
- Data errors: `slate.file.newNote` and `slate.file.moveTo` recorded
  `mac: null` while mac ships ⌘N and ⇧⌘M; `slate.file.cancelImport`'s
  hint diverged from mac's, which is load-bearing (it tells the user
  completed copies survive).
- Behaviour: double-click on a section header or on empty space invoked
  the selection; the first-selection suppression flag survived an open
  that produced no selection change.

**Test-coverage gap closed:** the production `IsAvailabilityRejection`
had no test at all — the palette's routing facts assert against a fake
seeded with the same constants, so deleting a clause left every test
green.

### Round 2 — verification (re-ran every round-1 mutation)

**The headline result: 7 of 12 round-1 fixes had NO test anywhere.**
Reverting them left the suite byte-identically green, so the claim "each
round-1 fix works" was unfalsifiable for a majority of them — the
recorded W4-8 lesson reproducing almost exactly. Gated: the SPDX header,
the chord-table rows, the `ActionFailed` wrapping, and the chord data.
Ungated: the text-editing allow-list (FlaUI only), the PD-2 re-open, the
focus guard, the vault dismissal, the enumerating refresh, the UIA
hosting, the double-click gate, and the suppression disarm.

**Three new confirmed defects, two of them false claims in this document:**

- **AltGr was still swallowed.** The round-1 record claimed the
  allow-list fixed it; it did not. WPF reports AltGr as `Control|Alt`,
  which fell through to the allow-list's final `return false`, so nine
  ordinary Polish letters and the German `@` and euro sign were dropped.
  Now distinguished by the right Alt key being physically down.
- **PINV-7 was wired to one of four refresh points — and not the one
  holding its own cited example.** The vault-lifecycle refresh fires on
  vault transitions; `ToggleReadingModeCommand` gates on the active tab
  being Markdown and needs a requery on tab switch. The workspace refresh
  now raises a hook the shell answers with the enumerating pass. The
  sidebar and quick-switcher lists remain hand-maintained; that residue is
  real and recorded rather than claimed closed.
- **Nothing enforced the palette/overlay mutual exclusion** the code
  comment asserted. Ctrl+O then Ctrl+Shift+P left two `IsDialog` overlays
  and two hit-test scrims live, with Quick Open's key handler unreachable
  behind the palette branch. Quick Open is now dismissed first.

**A round-1 finding that measurement refuted.** The claim that a closed
palette leaves its search box and results list in the UIA tree did not
hold: a mutation removing both `Visibility` bindings left the journey's
closed-palette assertion green, so the children do leave the tree with
their collapsed ancestor. The bindings are kept as precautionary and
labelled as such in the XAML. This is the recorded pattern — a finding
reasoned from framework behaviour measured in an earlier wave, wrong
here.

**Gates added, each mutation-verified:** a unit fact for the vault
dismissal (reverting it fails on P14's forbidden state), and two journey
legs — PD-2's re-open-with-cleared-query, and the closed-palette
absence of both child surfaces.

**Deliberately ungated, and said so in the source:** the suppression-flag
disarm. Investigating why no test could bite it showed the state is
unreachable — `Open()` re-arms the flag every time and P4 freezes the
snapshot, so a palette that opens with zero rows can never gain one. The
line stays as defensive hygiene; a test that passed either way would be
worse than none.

**Also resolved:** the intermittent 17/18 accessibility result flagged in
round 1 was `GridConformanceTests.MatrixConformanceHolds` failing against
a stale `GridConformanceHost` binary — the recorded trap. Rebuilt; 18/18
twice consecutively.

### Round 3 — verification + fresh eyes, then the design pass

**Verification found 2 of round 2's 3 new gates gate nothing**, and that
my AltGr fix was HALF a fix, live on HEAD: the clause tested
`modifiers == (Control|Alt)` by exact equality, so `Control|Alt|Shift`
never reached it — and that is how the UPPERCASE forms of the nine
Polish letters round 2 cited are typed. Nine lowercase letters fixed,
nine capitals left broken, recorded as closed. Now stripped of Shift
before the comparison, with the shifted rows the test table was missing.

**Fresh eyes found the third consecutive blocker in the same subsystem**
— the palette opened on top of the other seven sheets, because round 2's
guard covered Quick Open alone. That fired stopping rule 4, so the
design pass above was written before any further code.

**A round-1 finding was refuted by measurement, then the refutation was
itself corrected.** The closed-palette children do remain in the UIA RAW
tree; what hides them is WPF dropping invisible elements from the
CONTROL view, not the `Visibility` bindings. So the bindings are
redundant (round 2's conclusion) but not for round 2's reason, and both
comments that claimed otherwise are fixed.

**FlaUI was put in order as its own pass.** Twelve of thirteen journeys
silently degraded to a three-second smoke on a non-interactive runner —
including the palette journey, while P13 claims all three drift tests run
in CI. All thirteen now share one gate that honours
`SLATE_REQUIRE_UI_AUTOMATION`. The Ctrl+Alt+arrow chords are registered
process-wide by AMD, NVIDIA and Parsec on this box, so the suite now runs
the `RegisterHotKey` probe itself and labels the skip instead of
asserting what reads like a product defect. That probe returned FREE
early in the session and TAKEN later, which is exactly why two rounds
disagreed over 17/18 vs 18/18 — both were right when measured. Making
the chord leg skippable then unmasked a second, deterministic failure in
`FluentShell` that the suite had never reached; the palette was ruled out
by deleting `ObservePalette()` and reproducing it byte-identically, and
it is filed as
[#1107](https://github.com/coryj627/slate/issues/1107).

**Residue closed.** The shortcut-slot resolvers returned a fresh adapter
per resolve, making PINV-7 silently false for those nine ids; they are
now cached per sidebar instance. And the AltGr arm's dependency on the
shell-chord deny-list is now a build break rather than a comment —
driven from the chord table, because the first version scraped XAML
`KeyBinding`s and did not bite on Ctrl+Alt+F, which a code-behind
handler delivers.

**PINV-7 remains PARTIAL and is stated that way deliberately.** The
workspace and vault refresh points enumerate; the sidebar and
quick-switcher lists are still hand-maintained. I claimed this invariant
closed twice when it was not, so it is now recorded as residue rather
than as a fix.

**Carried, with reasons.** The §2.6 pin's reflection has escapes a
reviewer enumerated (a `public const`, a `static readonly`, or a literal
in another type). No fact enumerates the app's `ICommand` properties, so
PR-4's "the drift test forces the question" is not yet mechanised. The
menu-scrape twin does not reach context menus. Splitter-arrow and
property-row stepper chords remain unrecorded, and the forward chord
comparison keys on bare strings, so a duplicate string can shield a
missing row — the same under-match class already fixed once in
`CommandDriftTests`. *(That last item was closed in codex round 2 — see
below.)*

### Codex round 1 — needs-attention, two highs

Both highs were the same shape as the internal rounds: **a gate that
does not reach the code it claims to protect.** The design pass's own
modal exclusion had no test, and the palette open path was reachable
around it. Closed with facts over both, mutation-verified.

### Codex round 2 — needs-attention, two highs and two mediums

Every one of the four was the *under-match* class again, which is now
the recorded signature of this issue's defects.

- **The modal mapping was untested, and its comment was false.** It
  claimed compile-time exhaustiveness while carrying a `_` arm. Now a
  pure function over a named `ModalSurfaceState` with no wildcard, so
  CS8509 makes a new surface a build break, plus a fact asserting one
  flag lights exactly one surface.
- **A menu accelerator could name the wrong valid id.** Resolving
  accelerators from the table closed the hand-typed hazard but not the
  identity one. Now each accelerator must name the id backing that same
  item's `Command`; verified by swapping Split Right and Split Down,
  which the old gate passed.
- **All nine shortcut-slot labels bypassed mac parity in silence.** mac
  generates them as `sidebarOpenShortcut(1), "Open Shortcut 1"`, which
  the identifier-comma pattern cannot match, and the loop skipped
  unparsed ids with a bare `continue`. Now parsed, and an unparsed shared
  label must be dispositioned rather than skipped.
- **The scoped chord comparison built both sides from the table.**
  `Assert.Contains(("Up", PropertyRow), declaredByScope)` only restated
  that the table had a row it already had, so deleting the real
  `StepUpCommand` binding or the splitter's `Key.Up` arm left it green.
  The expected side is now scraped from `WorkspaceTemplates.xaml`'s
  `PropertyRowViewModel` templates and `WeightedSplitPanel.Thumb_KeyDown`,
  checked in **both** directions, and extended the same way to the
  Reading and Grid scopes — including the navigator's twelve generated
  heading chords, whose loop bound is read from the source rather than
  hard-coded at six. Both of codex's named mutations now fail, each
  naming the correct scope. A scope with neither a scrape nor a written
  reason is now a test failure, so the exemption list cannot grow
  silently.

**Self-QA on that last fix, before codex saw it.** The first version
exempted `Palette` and `QuickOpen` with written reasons, and the Palette
reason — "an imperative handler whose arms carry selection logic rather
than a scrapable gesture list" — was **not true of the code**.
`HandleCommandPaletteKey` is a flat `switch (e.Key)` under one
`modifiers == ModifierKeys.None` guard. So the exemption would have left
the single surface W5-1 actually adds as the only scope still checked
table-against-table, which is precisely the defect the fix existed to
remove, reintroduced one level up and dressed in a reason. Both scopes
are now scraped: the palette's switch, and Quick Open's three bare `if`
arms plus the modifier switch behind its commit. All fifteen rows agreed
with the table on the first run, and the table was written before the
scrapes existed.

That work also produced a **negative control worth recording**. Quick
Open's modifier switch is duplicated verbatim in a second handler
further down `MainWindow.xaml.cs`. Mutating the copy *inside*
`HandleQuickSwitcherKey` fails the gate; mutating the identical copy
*outside* it leaves the gate green. The scrape is therefore bounded to
the method it names, rather than matching a lookalike elsewhere in the
file — which is the same under-match hazard in its other direction, and
the only place in this issue where it has been positively disproved
rather than argued.

Only `Editor`, `Global` and `None` remain exempt: AvalonEdit's key
handling is a third-party control's, and `Global` has its own
both-direction check against `MainWindow.xaml` plus the imperative
allow-list.
