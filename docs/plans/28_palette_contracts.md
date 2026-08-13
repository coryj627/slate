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
required coverage is every chord the app actually delivers — which today is
roughly triple what `chords.json` records; see PR-3. Two rules the current
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
  budget, which is an owner decision.
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
  Windows.** Mac deliberately handles none of them (fn+arrows pass through
  to its text field). WPF's `ListBox` provides all four, they are standard
  Windows list behavior, and §W-C conformance expects them. Suppressing
  them to match mac would cost work to produce a worse Windows surface.
  Recorded per decision 12 (platform convention governs input). **The owner
  may veto this at PR.**
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
  commands.** These pane-focus chords shipped in W1-3 and `w3_spec.md`
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
  family. Two documentation claims are already false — `w3_spec.md` says the
  reading chords are "registered in chords.json" and both
  `WorkspaceTemplates.xaml` and `w_c_matrix.md` cite chords.json for
  Ctrl+Backspace; neither entry exists. The table records what ships; it
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

## Open questions to close during implementation

1. **`CommandAction` thread affinity.** `InvokeById` is a synchronous FFI
   call into a `Send + Sync` registry; the C# action runs on the calling
   thread. There is no precedent for a uniffi callback that must run on the
   dispatcher — the closest analogues (`UiProgressListener`,
   `UiVaultEventListener`) both take an explicit `enqueueUi` delegate. The
   bridge needs the same, and the choice must be a contract line before the
   red team, not after.
2. **`sidebar_pinned_order` source.** `FilesSidebarViewModel` maintains
   pinned and recent collections but exposes neither as an ordered
   `string[]`.
3. **Registry lifetime owner** — `App`, `MainWindow`, or
   `VaultLifecycleViewModel`. `CommandPaletteNeedsVault` implies some
   commands are vault-scoped while the registry may not be.
4. **Whether the palette memoizes `PaletteSections`** per
   `(snapshot, query, recents)`. Mac calls the FFI several times per render
   through computed properties. Memoization must not change results.

## Round record

Adversarial findings and their resolutions append here, per
`24_red_team_protocol.md` §Per-round record.
