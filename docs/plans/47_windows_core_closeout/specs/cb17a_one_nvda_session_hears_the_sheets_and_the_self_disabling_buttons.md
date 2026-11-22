# CB-17a: One NVDA session hears the sheets and the self-disabling buttons, and the focus manifest holds them to keys-first

Issue: #1866, referenced only (CB-17b closes it; the PR writes "Refs #1866" and keeps every close, fix or resolve word away from that number, in the PR text and in every commit message). Size L (plan §5 says M; this spec re-sizes it: a census of about 600 lines with its planted facts, ten edited or new facts, and a session of about forty steps over four launches). Lanes: windows (the app job; the PR changes `apps/slate-windows/tests/`, so the whole Windows lane runs; no gate journey). AT: NVDA, the session (below). Wave: C9, slot 2. Depends on: nothing. Parallel with: CB-15c (panels, reading, workspace, lifecycle and commands, and `ShellFailureDetailCensus`) and CB-32 (Rust only). Decision: CD11, as the owner answered it on 2026-11-16, and the lead's confirmation of 2026-11-21. Interlock: `focus_integrity_manifest.json` goes CB-17a, CB-17b, CB-16, CB-18 (plan §6). Review: a red team, then Codex adversarial-review rounds until it approves (Call 7).

**CD11 as answered (§3.1, 2026-11-16).** "Every self-disabling button moves the keys first, whatever the NVDA session hears, not only the ones heard to fail." CD11 has no second step and no list goes to the owner. Every button change goes in CB-17b. CB-17a keeps three things: the NVDA session, which still runs and whose record is kept whatever it hears; the keys-first witness; and the `Command=` scrape, with today's sites as dated exceptions that CB-17b ends. After CB-17b no dated exception of this class is left. The lead confirmed on 2026-11-21 that CB-17a changes no product code and no button.

*2026-11-21, Principal W's rulings on the red team's report:* M1 is taken with the exemption renamed `exempt: its own press never turns it off: <why>`, its limit written into the census's `_doc`, Rename and both Move to… buttons dated now, and the class of other drivers put as Call 8. M2 is taken: a keys-first fact on a per-trigger site also names the trigger's condition. Every minor and low is taken as the red team suggested; "Red team record" says how.

Written 2026-11-21 from the plan's rows (§2.3 :183, §3 CD11 and §3.1, §5 :573-574, §6 :755-765, §7 :779, §11 :1040), `reading/04_focus_and_peers.md:110-182` and `:435-540`, #1866 with its two comments, and an earlier worker's drift check and scrape (its scripts and outputs are in the principal's working folder, `cb17a/`, outside the repository; its automatic verdicts were not used). Every file and line below was read at `origin/main` 47bcf77e; the worktree's head cbfbe640 adds two core commits that touch no Windows file. Each statement that rests on reading alone says so.

`S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`; `M` is `docs/plans/18_windows_port/focus_integrity_manifest.json`.

## Goal

1. **One NVDA session** on the branch's Release build opens a sheet from the menu and from the empty pane's launcher, and presses each button the scrape finds that can go off while it holds the keys. Its record is kept whatever it hears. The branch changes no file under `apps/slate-windows/src/`, so its Release build is `main`'s product, and one run is the record for both.
2. **The focus manifest's census** reads every `Command=` on an element that can take the keys, keys each `IsEnabled` trigger on its own, and holds every site that can turn a control off (an `IsEnabled` trigger, binding or write, and every command site) to a keys-first fact, an exemption of a named class, or a dated row owned by CB-17b. `guard:` alone, or a fact that only names the site, no longer witnesses such a site.
3. **The dated rows only shrink.** A pinned baseline and a ceiling hold them; CB-17b lowers the ceiling to zero and removes the dated kind.
4. **One shared recorder,** `KeysFirstRecorder`, is what a keys-first fact calls. #1867's facts move onto it.

## Current state on main

**The menu.** `S/MainWindow.xaml:147` (`MainMenu`); its inline style at `:172-255`. Three overlay triggers set `IsEnabled` False (`:176-184`), fourteen sheet triggers do the same, one per sheet, through `IsNotNullConverter` (`:185-226`: AddProperty `:185`, BulkRename `:188`, CitationDetails `:191`, CitationSummary `:194`, FilesCiting `:197`, DashboardEditor `:200`, BaseQueryBuilder `:203`, TemplatePicker `:206`, TemplateFlow `:209`, MoveTo `:212`, CanvasCardEditor `:215`, CanvasCardPicker `:218`, CanvasPrompt `:221`, Settings `:224`), and #1867's three keep-on triggers set it True while the menu holds the keys under an overlay (`:232-252`). The reading's lines (180-219 and 227-247) moved by 5.

**The launcher.** `Slate.LauncherRowStyle`, `S/WorkspaceTemplates.xaml:215-275`. Its overlay trigger turns a row off only once the keys have left it (`:264-270`); its sheet trigger turns every row off at once (`:271-273`). Its three rows are Quick Open (`:1243-1250`), New Note from Template (`:1251-1258`, `Workspace.NewFromTemplateCommand`, which opens the template picker sheet) and Search Vault (`:1259-1266`). `T/EmptyPaneLauncherTests.cs:187-212`, `UnderASheetEveryActionGoesOffInTheTurnItOpens`, pins today's sheet behaviour; CB-17b rewrites it.

**The welcome and workspace views.** `S/MainWindow.xaml:1029` (`WelcomeRoot`, triggers `:1039-1059`) and `:1131` (`WorkspaceRoot`, triggers `:1144-1164`): three overlay triggers each, each with `IsKeyboardFocusWithin == False`; no sheet trigger (the reading's 1031-1057 and 1136-1162 moved). The welcome view shows only without a vault (`S/VaultLifecycleViewModel.cs:363`); without one `QuickSwitcher` is null (`:496`, set `:1726`, cleared `:1486`) and Search refuses to open (`S/Search/SearchOverlayViewModel.cs:299-305`), so two of the welcome view's three triggers cannot fire (by reading).

**The recorder.** `DisabledWithTheKeysInside(UIElement root, Action open)` is private at `T/CommandPaletteTests.OverlayFocus.cs:396-417`: it records the element holding the keys each time `root` raises `IsEnabledChanged` to False while `TheKeysAreIn(root)` (`:334-336`). Its callers are at `:36`, `:124`, `:154`, `:199`, `:280` and `:380`. `TakeTheMenuKeys` and `SkipWhereTheDesktopRefusesTheMenu` are private beside it (`:350-363`). `T/EmptyPaneLauncherTests.cs:147-180` (`AnActionGoesOffOnlyAfterThePaletteTakesTheKeys`) writes its own copy inline and calls `CommandPaletteTests.TheKeysAreIn` (`:162`).

**The census.** `T/Censuses/FocusIntegrityManifestCensus.cs` (1,014 lines; LF) scrapes eight site kinds (`:18-34`) and judges three witness kinds (`:43-60`, `WitnessJudge.Judge` at `:661-710`): `Class.Method` (a fact in this project whose code names the site, `:698-709`), `guard: A[, B]` (`:678-696`) and `exempt: <reason>` (any reason; a cited `SomethingTests.Method` must exist, `:663-676`). A XAML trigger site is one key per carrier and property (`:465-491`), so every trigger of one style is one site: the menu's 17 triggers are `xaml-trigger MainWindow.xaml MainMenu IsEnabled`, and the launcher style's two are `xaml-trigger WorkspaceTemplates.xaml Style:Slate.LauncherRowStyle IsEnabled`. Writes in a constructor or an object initializer are not sites (`:380-385`, `:524-528`). The census does not read `Command=`. Its class carries `[Trait("tier", "census")]` (`:81`) because it reads `M`, a census-only path (`.github/workflows/windows.yml:214-230`, `docs/plans/18_windows_port/**` at `:226`). `SLATE_FOCUS_MANIFEST_UPDATE=1` rewrites `M` keeping every assignment (`:98-102`, `:965-984`). `Facts()` reads only this project (`:988-1013`), so a gate fact cannot witness. `RegionBoundaryCensus.Resolve` is private (`T/Censuses/RegionBoundaryCensus.cs:310`).

**The manifest today.** 19 `IsEnabled` rows (`M:9`, `:96`, `:105`, `:107`, `:133`, `:141`, `:160`, `:175`, `:177`, `:191`, `:325`, `:336-338`, `:343-345`, `:348-349`): 12 by `guard:`, 2 by a fact that names the site (`:96`, `:338`), 5 by an exemption. Three of those exemptions are untrue or partial: `:325` says the menu is "disabled only while a modal overlay (Quick Open, Search, the palette) opens", but fourteen sheet triggers also turn it off; `:344` and `:345` are true of their views but witness nothing about the turn between.

**How a command turns a button off.** `RelayCommand` (`S/VaultLifecycleViewModel.cs:2677-2692`) raises `CanExecuteChanged` only when its owner calls `RaiseCanExecuteChanged`. `AsyncRelayCommand` (`:2708-2781`) is off while its task runs unless built with `admitWhileExecuting: true` (`:2741`), and raises in the press turn (`:2768-2769`): every such button turns itself off under the keys when pressed. A command can also go off for a moment and come back in one dispatcher operation: Rename calls `RequestSelectionAt` and `Refresh` (`S/FilesSidebarViewModel.FileManagement.cs:278-279`), whose reconcile lets go of the selection before the old row leaves (`DeselectBeforeRemoval`, `S/FilesSidebarViewModel.RescanReconcile.cs:106`, `:128-139`), so every selection verb goes off and then on again with the keys on it.

**Menu rows.** WPF's `MenuItem` raises `PreviewClick`, which `MenuBase` handles by leaving menu mode (the popup closes and the keys go back where they were), and invokes the command a turn later at Render priority, unless `StaysOpenOnClick` is set. That is WPF's reference source as this spec's writer knows it, not read for this spec; no shell row sets `StaysOpenOnClick` (a search of `S/` finds none). So a menu row should never hold the keys when its own command runs. Not measured; Design 6's fact measures it (Call 2).

### Step 0, as run (the scrape, 2026-11-21, main 47bcf77e)

An earlier worker's scraper (Python, over the shell's XAML and C#) and an independent count for this spec (`cb17a/verify_counts.py`, same folder) agree:

- **262 command sites:** 255 `Command=` attributes in XAML (`MainWindow.xaml` 211, `WorkspaceTemplates.xaml` 39, `FindInNoteBar.xaml` 5), of which 249 are `{Binding` (205, 39, 5) and 6 are `{x:Static` (2 key bindings, the Exit row, 3 caption buttons); and 7 code writes of an element's `Command` (`S/Bases/BaseSurfaceView.cs:234`, `S/Graph/GraphVerbosityMenu.cs:42`, `:63`, `S/MainWindow.TitleBar.cs:71`, `S/NoteHeaderView.cs:519-521`).
- **By kind:** 48 input bindings (`KeyBinding`, never a stop: 34 and 14); 135 menu rows (106 `MenuItem` and 16 `CheckMenuItem` in the app menu; in `WorkspaceTemplates.xaml`, the editor's context menu's 3 rows, one a `CheckMenuItem`, `:826-842`, and the tab strip's 5, `:1030-1038`; the ⋯ menu's 3 rows built in code; and 2 graph verbosity writes, one of them a clear to null); 79 controls.
- **The 79 controls:** 5 not focusable (the title bar's Settings and the three caption buttons, `S/MainWindow.xaml:844-872`, `Focusable="False"`, and the code write to `CaptionMaximize`), so **74 sites**: 73 in XAML (one a `Hyperlink`) and `BaseSurfaceView`'s `_failedEdit` (`S/Bases/BaseSurfaceView.cs:226-235`, built in the constructor). Their classification is Design 7.
- The earlier worker counted 82 controls and a 12th self-disabling one, Split Right. `NoteHeaderView.cs:519-521` writes the ⋯ menu's rows (`:125-132`), so those three are menu rows, and Split Right (off once the split reaches the cap) is a menu row.

No hosted probe and no NVDA run was made for this spec: the desktop is shared, and the session is this chunk's own delivery. The session is the measurement of each button ("The NVDA session", below).

### What the drift check changes in the plan

1. **§2.3 :183, "have never been heard either", is wrong for one group.** #1866's comment of 2026-11-17 (during CB-9b's spot check, on `main` eea6e7a2 and on ba0c1b64): Bulk Rename's Apply goes unavailable when pressed, and the keys leave the open sheet for what is behind it ("n000.md, file, not selected, 1 of 300" from the Files tree; "a.md editor, document" from the editor).
2. **"About twenty buttons" is about thirty.** Beyond the fifteen sheet trigger rows, the census rows CB-17b must end are 31: 19 command sites (Design 7, table B) and 12 `IsEnabled` rows (Bulk Rename 2, Walk through 1, the Queries leaf's 3 groups, the property row's Save 2, and the 4 code writes of reading row 17). The reading did not list five close buttons whose command goes off as their surface closes (Call 4), nor Rename and the two Move to… buttons, which a refresh deselects under the keys (Current state); and its row 12 called Retry sidebar settings R only, though its command also goes off (`S/FilesSidebarViewModel.cs:606`, `:914`).
3. **§5's size.** CB-17a is L, not M (header).
4. **§6 :764** names CB-17a in the `MainWindow.xaml, MainWindow*.cs, WorkspaceTemplates.xaml` row. Under CD11's answer CB-17a edits none of them. The wave's docs PR drops it there.
5. The menu and view trigger lines moved (Current state).

## Design

### 1. `T/Support/KeysFirstRecorder.cs` (new)

```csharp
/// <summary>#1866 (CD11): whether a control holding the keys was turned off before
/// they left it. A keys-first fact calls AssertTheKeysLeaveFirst in its own code.</summary>
internal static class KeysFirstRecorder
{
    // Runs act; fails, naming what held the keys, if root turned off while the keys
    // were on it or inside it. Root is the site's element, or a descendant that the
    // site's change turns off with it (a menu item, for the menu).
    internal static void AssertTheKeysLeaveFirst(string what, UIElement root, Action act);

    // The same for a focusable content element (a Hyperlink): the keys are on it.
    internal static void AssertTheKeysLeaveFirst(string what, ContentElement root, Action act);

    // The same record as a list, for a fact that asserts more about it.
    internal static List<string> DisabledWithTheKeysInside(UIElement root, Action act);

    // Moved from CommandPaletteTests (:334-336, :350-363), unchanged.
    internal static bool TheKeysAreIn(UIElement element);
    internal static void TakeTheMenuKeys(UIElement item);
    internal static MenuItem SkipWhereTheDesktopRefusesTheMenu(string what);
}
```

- `DisabledWithTheKeysInside` moves unchanged. It records every `IsEnabledChanged` to False while the keys are inside the root, so a control that goes off and comes back within one dispatcher operation (Rename's refresh, Current state) is recorded: an off with the keys on it is a defect however short.
- `AssertTheKeysLeaveFirst` runs it and asserts the list is empty with `$"{what} went off with the keys on {...}"`.
- Every caller moves to the asserting form: the six callers in `T/CommandPaletteTests.OverlayFocus.cs`, and `EmptyPaneLauncherTests.AnActionGoesOffOnlyAfterThePaletteTakesTheKeys`, whose inline copy goes. `EmptyPaneLauncherTests.cs:162`'s `CommandPaletteTests.TheKeysAreIn` becomes `KeysFirstRecorder.TheKeysAreIn`.
- The recorder's code never names `FocusIntegrityManifestCensus` (comments may): a type that names a census-only reader takes on its trait duty and passes it to every type that names it (`scripts/check_windows_census_trait.py:398-407`), which would put every keys-first fact's class in the census tier.
- Considered and not taken: a recorder that follows the keys instead of a root (any element that goes off while holding them); it changes what #1867's facts assert, and the root form is proven.

### 2. One site per `IsEnabled` trigger

A XAML trigger whose setter sets `IsEnabled` to anything but True is a site of its own, keyed by its condition: `xaml-trigger <file> <carrier> IsEnabled when <condition>`.

- **The condition.** Each binding's path, with a leading `Path=` dropped and prefixed by its `ElementName` (or a property trigger's `SourceName`) when it has one, or a property trigger's property; then `=` and the `Value` as written, or `(element)` when the value is a property element (`<Condition.Value>`); a `MultiDataTrigger`'s or `MultiTrigger`'s conditions joined by ` & `; a second identical trigger on the same carrier gets ` #2`.
- **The carrier.** The style's or template's carrier, as today; a setter with `TargetName` is carried by the named part.
- **Two more sites per style.** A style whose own setter sets `IsEnabled` False is a site `when (style default)`: it turns the control off when a trigger that set True stops applying. A style whose own setter gives `IsEnabled` a markup value (`Value="{Binding …}"`) is a site `when (style binding)`. Neither exists today (a search of `S/` finds no `Property="IsEnabled" Value="{`), so only the first gives rows.
- A trigger that sets True is not a site. `Visibility` triggers keep one key per carrier, so no other row changes.

Examples: `xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.AddPropertySheet=True`; `xaml-trigger WorkspaceTemplates.xaml Style:Slate.LauncherRowStyle IsEnabled when IsModalSurfaceOpen=True & IsKeyboardFocusWithin=False`; `xaml-trigger MainWindow.xaml WrapPanel(QueriesRun) IsEnabled when QueriesSavedList.SelectedItem={x:Null}`; `xaml-trigger WorkspaceTemplates.xaml Button in PropertyRowTemplate#0 IsEnabled when (style default)`. A site's names stay the carrier's, and a keys-first fact also names the condition's token (Design 4). Under this rule the 19 `IsEnabled` rows become 41 (Design 7, table A). Call 1.

### 3. Command sites, and one more state site

Two new kinds:

- **`xaml-command <file> <identity> <command>`**: a XAML element with a `Command` attribute that can itself take the keys. That is a `UIElement` that is focusable by its own declaration or its type's default, as `RegionBoundaryCensus.HoldsAStop` judges one element (`:246-252`, the `focusable` test alone, exposed as `IsFocusable(XElement)`, with `Resolve` exposed beside it; not the method's other clauses, under which a `Focusable="False"` button with bound content would count), or a `Hyperlink`, a focusable content element that `Resolve` cannot type.
  - **Its identity** is its own name when it has one (`x:Name`, automation id, `x:Key`, as `Named` reads them, `:646-649`). An unnamed one is keyed by its nearest named holder and its command, `<holder> <command>`, with ` #2` only when one holder has two unnamed elements bound to the same command. So Delete is `xaml-command MainWindow.xaml SidebarFileActionsExpander DeleteCommand`, and a button inserted in front renames nothing. `<command>` is the binding path's last segment or the `x:Static` member.
  - **Not a site:** an input binding (`KeyBinding`, `MouseBinding`); and a `MenuItem`, or a type `Resolve` finds derived from it (`CheckMenuItem`), that sits inside a `Menu` or a `ContextMenu` and does not stay open on click, by its own `StaysOpenOnClick="True"` or by a style setter of it (Call 2). A `MenuItem` outside any menu is a site.
- **`command <type>.<member> <receiver>`**: a C# write of `Command` on a `ButtonBase` or a `Hyperlink`: an assignment, an object initializer, or `SetValue`, `SetCurrentValue` or `SetBinding` of a `CommandProperty`. Unlike a state write, an initializer or a constructor's write is a site, because the command it sets lives on with the control. A write of `null` is not. A `MenuItem` receiver is not, unless the same type writes `StaysOpenOnClick = true` on it. A receiver that is a named XAML element with `Focusable="False"` (`CaptionMaximize`) is not.

**And one state site.** A `SetBinding` of `UIElement.IsEnabledProperty` (by the element's method or `BindingOperations`) in a constructor or an initializer is a `ui-state` site, unlike a plain write there: a binding lives on and can turn the control off later. None exists today (a search finds only the shell's own attached properties named `IsEnabledProperty`, which the scrape already skips by owner, `:433-440`).

Today this gives 74 command sites: 73 `xaml-command` and one `command` (`BaseSurfaceView`'s `_failedEdit`, keyed with the constructor as its member).

### 4. The witnesses

A **disable site** is an `xaml-trigger`, `xaml-binding` or `ui-state` site whose property is `IsEnabled`, or a command site. A disable site takes only these witnesses:

- **`keys-first: Class.Method`.**
  - The fact exists (a `[Fact]` or `[Theory]`, as today), and its code names the site (today's rule: an identifier or string literal, whole and case-sensitive, not in a comment).
  - On a per-trigger site, its code also names the condition's token: for some condition of the trigger, the last segment of its path that is not `IsOpen`, `IsKeyboardFocusWithin` or `SelectedItem`, or, when none is left, its `ElementName` or `SourceName`. A `(style default)` or `(style binding)` site has none. This is stricter than "a segment" on purpose: `Workspace` alone does not witness the fourteen sheet rows, but `AddPropertySheet` witnesses its own.
  - Its own code holds an invocation of `KeysFirstRecorder.AssertTheKeysLeaveFirst`, qualified so, found in the syntax of the fact's method, inside a lambda of it included (every fact is written `=> RunSta(() => …)`). A `nameof`, a method group, a string, an unqualified method of the same name, or a call inside a helper the fact calls does not count (Call 5).
  - A keys-first witness is accepted on any site, not only a disable site.
- **`exempt: not focusable`.** Checked: the site's element has `Focusable="False"`, or the keyed style that carries it sets `Focusable` False and no user of the style sets it True; for a code site, the receiver is a named XAML element with `Focusable="False"`.
- **`exempt: always on: <Type>.<Property>`** (command sites). Checked by Roslyn over the shell compilation (Call 3):
  - The named type has that property, and the site's command path ends in its name.
  - Every construction that reaches it is `new RelayCommand(…, <lambda whose body is the literal true>)`, or `new AsyncRelayCommand(…, <the same>, admitWhileExecuting: true)`. The constructions read are the property's initializer, its expression body through `??=` and a returned field, and every assignment to it or to that field in its type. A property that returns another command property (`=> _row.CommitCommand`) is followed to it.
  - Every other property of that name in the shell must be shown constant the same way, since the binding's data context cannot be resolved. An interface member is skipped. A record's parameter, or anything the check cannot follow, counts as not constant. So `CancelCommand`, `RetryCommand`, `NextCommand`, `DeleteCommand` and `RemoveCommand`, each also declared on a type whose command reads state or cannot be followed, are refused, and their sites take the next class.
- **`exempt: its own press never turns it off: <why>`.** Free text, at least a clause: what its command or trigger reads, and that its own press does not change it. A cited `SomethingTests.Method` must exist, as today. Its limit is written into the census's `_doc` and header: another driver can still turn such a control off under the keys (a rescan's or a refresh's deselect, an in-place change of item, a find bar's mode switch), and the census does not judge other drivers (Call 8).
- **`exempt: never fires: <why> (<Class.Method>)`.** For a trigger that cannot apply while its carrier shows. The cited fact is required and must exist.
- **`dated: <D>; CB-17b (CD11): <mechanism>`.** `<D>` is the one date the rows are written, pinned in the census as `DatedOn`; the owner token is exactly `CB-17b (CD11)`; the mechanism is non-empty. Accepted only for a key in the baseline (Design 5).

Any other witness on a disable site fails, with a message naming the six. `guard:` and a fact that only names the site still witness every other site as today. The manifest's `_doc` (written by `Write`, `:969-972`) names the new kinds and the press class's limit.

### 5. The dated rows only shrink

As `ShellFailureDetailCensus` does (`T/Censuses/ShellFailureDetailCensus.cs:102`, `:154-160`, `:261-265`, `:333-335`, `:358-410`):

- `DatedBaseline`, a string array in the census, holds the keys dated on `<D>`, 46 by this spec's count (table A 27, table B 19); the implementer's scrape fixes the number. Nothing is ever added to it; a fact pins its length and that its keys are distinct. With holder-and-command identities (Design 3), an inserted button does not rekey it.
- `DatedCeiling` equals the number of `dated:` witnesses in `M`. More fails ("a new site takes keys-first or an exemption, never a dated row"); fewer fails until the ceiling is lowered ("lower DatedCeiling to N, so the rows this PR ended cannot come back").
- A `dated:` witness on a key outside the baseline, with another date, or another owner, fails.
- CB-17b ends every row, lowers the ceiling to zero, and deletes `DatedBaseline`, `DatedCeiling` and the `dated:` kind, so no exception of this class can be written after it (CD11).

### 6. Menu rows let the keys go before their command runs (`T/MenuRowCommandTests.cs`, new)

The proof behind Call 2's exclusion. On the real window (`ShownShell`), with the keys on a row of an open menu through `KeysFirstRecorder.TakeTheMenuKeys` (skipped where the desktop refuses a menu the keys, #1680, so it runs on an interactive desktop only):

- A recording command is put on the row for the fact, and restored in a `finally`. The row is activated by Enter through the input manager, and again through its peer's Invoke.
- At `Execute`, the fact records whether the keys were on the row or on any `MenuItem` of that menu (walking the focused element's logical parents to `MainMenu` for the app menu, or to the button's `ContextMenu` for ⋯; `TheKeysAreIn(MainMenu)` cannot see a row in a submenu's popup, whose visual tree is the popup's), and whether the popup was open.
- Asserted: neither, for the Slate menu and for the ⋯ context menu (`NoteHeaderMore`).
- A control row with `StaysOpenOnClick = true` asserts the opposite (the keys still on the row at `Execute`), so the fact is seen to tell the two apart.

### 7. Today's sites, classified

Classes by reading, from each command's construction and each trigger's condition. Rows marked † were not traced past the press. Two reading groups are dated because CD11 names the reading's groups (rows 15 and 17), whatever drives them, and their mechanism text says so.

**Table A: the `IsEnabled` sites (19 rows today, 41 under Design 2).**

| Today's row | Today's witness | Rows | CB-17a |
|---|---|---|---|
| `Style:Slate.TitleBarButtonStyle` (`M:9`) | exempt | 1 | exempt: not focusable (`S/Themes/Slate.Text.xaml:152-160` sets `Focusable` False) |
| `Style:Slate.LauncherRowStyle` (`M:96`) | fact `EmptyPaneLauncherTests.AModalTakesTheKeysFromAnAction` | 2 | `when IsModalSurfaceOpen=True & IsKeyboardFocusWithin=False`: keys-first, `EmptyPaneLauncherTests.AnActionGoesOffOnlyAfterThePaletteTakesTheKeys` (Design 8). `when IsModalSheetOpen=True`: dated, a sheet opens over the row holding the keys (#1866) |
| `BaseSurfaceView.RenderChrome _quickFilter` (`M:105`) | guard | 1 | dated, CD11 names reading row 17: a reload sets Loading and turns it off under the keys (`S/Bases/BaseSurfaceView.cs:751-752`, `BaseDocumentViewModel.cs:1038`) |
| `… _viewPicker` (`M:107`) | guard | 1 | dated, reading row 17: the same (`BaseSurfaceView.cs:753`) |
| `ConnectionsLeafView.RenderCore _depth` (`M:133`) | guard | 1 | dated, reading row 17: off when the anchor note goes (`S/Graph/ConnectionsLeafView.cs:700`, `:707`) |
| `GraphInspectorView.RenderGate section` (`M:141`) | guard | 1 | dated, reading row 17: off when the graph stops being effective (`S/Graph/GraphInspectorView.cs:367-372`) |
| `AccessibleDataGrid.BuildRowActionsMenu item` (`M:160`) | exempt | 1 | exempt: its own press never turns it off: set as its menu opens, before the keys are in it |
| `BulkRenameApplyButton` (`M:175`) | guard | 1 | dated: its press starts the apply and `CanApply` falls (`S/MainWindow.xaml:3762-3768`); heard 2026-11-17 |
| `BulkRenamePreviewButton` (`M:177`) | guard | 1 | dated: its press runs the preview and `CanPreview` falls (`:3757-3761`); heard under NVDA on 2026-11-21 by CB-15c’s pass: the keys drop onto the note behind the sheet with no key route back (#1866, comment 7062230594) |
| `CitationSummaryWalkButton` (`M:191`) | guard | 1 | dated: `CanWalkThrough` falls as the walk starts (`:3964-3971`) † |
| `MainMenu` (`M:325`) | exempt (untrue) | 17 | the 3 overlay triggers: keys-first, `CommandPaletteTests.TheMenuIsDisabledOnlyAfterTheKeysLeaveIt` (its data names `QuickSwitcher`, `Search` and `Palette`, Design 8; it skips on the hosted runners, `CommandPaletteTests.OverlayFocus.cs:191`, `:350-363`, and runs in the local whole run). The 14 sheet triggers: dated, a sheet opens over the menu holding the keys (#1866) |
| `WrapPanel(QueriesOpenBaseFile)` (`M:336`) | guard | 1 | dated, CD11 names reading row 15: off when the base files list's selection goes under the keys (a leaf refresh; no button of the group clears it) (`S/MainWindow.xaml:2510-2522`) † |
| `WrapPanel(QueriesOpenDashboard)` (`M:337`) | guard | 1 | dated, reading row 15: Delete… removes the selected dashboard, and the group goes off (`:2568-2582`) † |
| `WrapPanel(QueriesRun)` (`M:338`) | fact `RightPaneLeafRegionTests.PinFromItsButtonKeepsTheKeysOnTheButton` | 1 | dated, reading row 15: Delete… removes the selected query, and the group goes off (`:2435-2455`) †; the Pin fact covers Pin only and stays as a regression |
| `TextBox in BuilderConditions#0` (`M:343`) | guard | 1 | exempt: its own press never turns it off: `IsPreserved` is fixed when the row is built (`S/Bases/BaseQueryBuilderViewModel.cs:24-26`, init-only) |
| `WelcomeRoot` (`M:344`) | exempt | 3 | `when Palette.IsOpen=True`: keys-first, `CommandPaletteTests.TheWelcomeViewIsDisabledOnlyAfterTheKeysLeaveIt` (its code names `Palette`). `when QuickSwitcher.IsOpen=True` and `when Search.IsOpen=True`: exempt: never fires: Quick Open and Search cannot open without a vault, and the welcome view shows only without one (`CommandPaletteTests.QuickOpenAndSearchStayShutOverTheWelcomeView`, new, Design 8) |
| `WorkspaceRoot` (`M:345`) | exempt | 3 | keys-first, `CommandPaletteTests.TheWorkspaceIsDisabledOnlyAfterTheKeysLeaveIt` (its data names the three tokens, Design 8) |
| `Button in PropertyRowTemplate#0` (Save, `M:348`) | guard | 2 | dated ×2: `when WriteInFlight=True` (its press starts the write) and `when (style default)` (off again once the draft is clean) (`S/WorkspaceTemplates.xaml:628-646`) |
| `Button in PropertyRowTemplate#2` (Delete, `M:349`) | guard | 1 | exempt: its own press never turns it off: off while the draft is dirty, which only the row's editor makes (`:654-673`) |

Totals: keys-first 8, exempt 6, dated 27.

**Table B: the command sites (74).**

- **Keys-first (4).**
  - `SidebarFilterClear` (`S/MainWindow.xaml:1273-1280`; its Click moves the keys to the filter field before the command runs, `S/MainWindow.xaml.cs:1941-1942`): `SidebarTreeKeysTests.ClearButton_ClearsAnActiveFilterAndKeepsTheKeysInTheField` (Design 8).
  - `EmptyPaneQuickOpen` and `EmptyPaneSearchVault`: `CommandPaletteTests.ALauncherActionThatOpensAnOverlayIsDisabledOnlyAfterTheKeysLeaveIt`.
  - `FindInNoteClose`: `Close` returns the keys to the surface before `IsOpen` goes false (`S/FindInNoteViewModel.cs:360-372`, by reading): `FindInNoteBarTests.CloseLetsTheKeysGoBeforeItGoesOff` (new, Design 8). If that fact is red on main, the row is dated instead and the PR says so.
- **Dated (19).**
  - Busy flag, `AsyncRelayCommand` (4): `SidebarExpandLoaded` (`S/FilesSidebarViewModel.cs:688-694`); Move checked to Recycle Bin, `SidebarBatchActions BatchTrashCommand` (`:676-679`); Delete, `SidebarFileActionsExpander DeleteCommand` (`:630-634`); Import…, `SidebarFileActionsExpander ImportCommand` (`:680-683`).
  - Its press changes what `CanExecute` reads (6): `CancelTrash` (`CanCancelTrash` falls at Stopping, `S/FilesSidebarViewModel.Trash.cs:20-56`; the restore is queued at Input after, `S/MainWindow.Trash.cs:46-64`); `SidebarSettingsRetry` (the notice clears, `:606`, `:914`; its panel also collapses); Folder note, `SidebarFileActionsExpander CreateFolderNoteCommand` (`:635`); Delete folder note, `… DeleteFolderNoteCommand` (`:636`); Remove shortcut, `SidebarShortcutsActions RemoveShortcutCommand` (`SelectedShortcut` goes null, `:670`, `:2280`); `MoveToRetry` (`CanRetry` falls as the load restarts, `S/FileManagement/MoveToPickerViewModel.cs:141`, `:146-148`, `:190-200`; its `Visibility` also collapses).
  - Its press refreshes the tree, which lets go of the selection under the keys and seats it again (3): Rename, `SidebarFileActionsExpander RenameCommand` (`:622-626`; `FileManagement.cs:278-279`, `RescanReconcile.cs:106`, `:128-139`); Move to…, `SidebarFileActionsExpander MoveToCommand`, and the batch Move to…, `SidebarBatchActions MoveToCommand` (`:657-661`; the move refreshes the same way once the sheet closes) †.
  - Off when the work it stops settles (1): Cancel import, `SidebarFileActionsExpander CancelImportCommand` (`:684`).
  - Its surface closes and its command goes off with it, with the keys moved after (5, Call 4): `QuickSwitcherClose` (`IsOpen`, `S/QuickSwitcherViewModel.cs:77`, raised `:582`); `EditorPopoverClose` and `EditorPopoverOpenSource` (`IsPopoverOpen`, `S/EditorInteractions.cs:667-672`, raised `:701-702`, `:761`; `ClosePopover` queues the focus request at Background after `IsPopoverOpen` goes false, `:4153-4174`); `MoveToCancelButton` (`!_retired`, `MoveToPickerViewModel.cs:177-181`, `:273-274`); `CanvasInspectionClose` (`InspectionOnly`, `S/Canvas/WorkspaceViewModel.Canvas.cs:498-501`, raised `:474`).
- **Exempt, always on, checked (18).** `SidebarCollapseAll` (`FilesSidebarViewModel.CollapseAllCommand`, `:687`); `OpenVaultButton` (`VaultLifecycleViewModel.OpenVaultCommand`, `:276`, admits while executing; the interface member in `S/Commands/SlateCommandRegistrar.cs:40` is skipped); the property row's Step up, Step down, the boolean `CheckBox`, Add item, Save and Revert (`PropertyRowViewModel`, `S/Panels/PropertyRowViewModel.Editor.cs:73-78`; `PropertyListItemViewModel`'s `CommitCommand` and `RevertCommand` forward to the row's, `:321-323`); Pick… (`WorkspaceViewModel.PickWikilinkTargetCommand`, `S/WorkspaceViewModel.Properties.cs:101-111`); `PropertiesBulkRenameButton`, `PropertiesAddButton`, `AddPropertyCancel`, `BulkRenameClose` (`WorkspaceViewModel.Properties.cs:83-97`); `EmptyPaneNewFromTemplate` (`WorkspaceViewModel.NewFromTemplateCommand`, `S/WorkspaceViewModel.Templates.cs:66-68`; its row goes off by the launcher style's sheet trigger, table A); `CitationDetailsCloseButton`, `CitationSummaryDismissButton`, `FilesCitingCloseButton` (`S/WorkspaceViewModel.Citations.cs:84-99`); `TemplateNameCreate` (`TemplateFlowViewModel.CreateCommand`, `S/Templates/TemplateFlowViewModel.cs:177-178`).
- **Exempt, its own press never turns it off (33).**
  - Constant, but not checkable because another type declares the same name (8): `TemplatePickerCancelButton`, `TemplatePickerTryAgainButton`, `TemplatePickerFailedTryAgainButton` (`S/Templates/TemplatePickerViewModel.cs:177-181`; `MoveToPickerViewModel` also has `CancelCommand` and `RetryCommand`); `TemplatePromptsCancel`, `TemplatePromptsNext`, `TemplateNameCancel` (`TemplateFlowViewModel.cs:174-181`; `FindInNoteViewModel` also has `NextCommand`); the property row's Delete (`PropertyRowViewModel.Editor.cs:75`; `FilesSidebarViewModel` also has `DeleteCommand`); a list item's Remove (`:302`; `GraphInspectorGroupRow` has a `RemoveCommand` parameter, `S/Graph/GraphInspectorViewModel.cs:13`).
  - The Files pane's other verbs, whose `CanExecute` reads the import and trash flags, the selection, the checks or the name field, which no press of theirs changes (16): `SidebarNewNote`, `SidebarRefresh` †, the batch Add and Remove, and the File actions New note, New folder, Duplicate, Copy path, Reveal in File Explorer, Copy link, Open, New tab, Split, Pin, Unpin and Add shortcut (`S/FilesSidebarViewModel.cs:605-674`).
  - The find bar's Previous, Next, Replace and Replace all (4; `IsOpen` and `CanReplace`, `FindInNoteViewModel.cs:56-59`, `:136-145`; an in-place change of item closes the bar without moving the keys, `:378-391`, `S/WorkspaceViewModel.cs:714`, and a mode switch turns Replace off: other drivers, Call 8).
  - `CanvasPromptClearMarksButton` (1; the sheet stays a marks list, `Canvas/WorkspaceViewModel.Canvas.cs:573-576`).
  - Three that read only their parameter: the welcome view's recent vault button (`VaultLifecycleViewModel.cs:277-280`), a tab's Close (`S/WorkspaceViewModel.cs:2930-2932`) and the citation field's `Hyperlink` (`WorkspaceViewModel.Citations.cs:340-343`).
  - `BaseSurfaceView`'s `_failedEdit` (1; a `ReasonedCommand` over `EditDefinitionUnavailableReason`; opening the builder does not set it †).

Totals: keys-first 4, dated 19, exempt 51 (18 checked, 33 by reason).

Over both tables: 115 disable-site rows; 12 keys-first, 57 exempt, 46 dated.

### 8. Facts edited or added (tests only)

- **#1867's six recorder callers** in `CommandPaletteTests` move onto `AssertTheKeysLeaveFirst`, and their code names their overlay's token: the theories' data becomes `"QuickSwitcher"`, `"Search"` and `"Palette"` (today `"QuickOpen"` for the first), with `OpenTheOverlay`'s switch following. Their checks do not change.
- **`EmptyPaneLauncherTests.AnActionGoesOffOnlyAfterThePaletteTakesTheKeys`** drops its inline recorder for `AssertTheKeysLeaveFirst("Search Vault", search, …)`, and gains the premises `Assert.Same(search.FindResource("Slate.LauncherRowStyle"), search.Style)` (it then names the style's site, as `AModalTakesTheKeysFromAnAction` does, `:129`) and `Assert.True(host.Shell.IsModalSurfaceOpen)` after the open (it then names the trigger's token).
- **`SidebarTreeKeysTests.ClearButton_ClearsAnActiveFilterAndKeepsTheKeysInTheField`** (`:934-955`) wraps the Invoke with its `PumpedDispatcher.Drain()` and `PumpUntilDrained` in `AssertTheKeysLeaveFirst("Clear filter", host.ClearButton, …)`, and gains the premise `Assert.Equal("SidebarFilterClear", AutomationProperties.GetAutomationId(host.ClearButton))`; today its code does not name the site (the id is looked up in its host, `:1416-1418`).
- **`FindInNoteBarTests.CloseLetsTheKeysGoBeforeItGoesOff`** (new): the keys on `FindInNoteClose`, its Invoke under the recorder; the keys end in the editor.
- **`CommandPaletteTests.QuickOpenAndSearchStayShutOverTheWelcomeView`** (new): with the welcome view showing and no vault, Quick Open's command and Search's toggle leave both overlays shut and the welcome view enabled.
- **A local mutation per converted witness,** each reverted and its result listed in the PR: delete the `Focus()` at `S/MainWindow.xaml.cs:1942` (the Clear filter fact goes red); drop the launcher style's `IsKeyboardFocusWithin` condition (`S/WorkspaceTemplates.xaml:267`; the launcher facts go red); drop the menu's keep-on triggers (`S/MainWindow.xaml:232-252`; the menu theory goes red, on this desktop); drop the welcome view's and the workspace's `IsKeyboardFocusWithin` conditions (their facts go red); move `ReturnFocus` after `IsOpen = false` (`S/FindInNoteViewModel.cs:368-371`; the new find bar fact goes red).

### 9. What this chunk does not change

No file under `apps/slate-windows/src/`, no XAML, no `crates/` file, no binding (the mutations above are local and never committed). `UnderASheetEveryActionGoesOffInTheTurnItOpens` stays as it is (it pins the defect; CB-17b rewrites it, reading :143-144). `ModalSurfaceTests` and the gate's partial files are not edited. No `A11yEvent` construction moves (contract 38's ledger does not move); no chord, command or help line (the parity matrix does not move). No shell code is added, so `ShellFailureDetailCensus` sees nothing new. Gate facts still cannot witness (`Facts()` reads this project only).

## Tests

**The red run.** The tests-first commit holds every new and edited fact, `KeysFirstRecorder` (test support), and stubs of the census's new members that keep main's behaviour: the scrape returns no command site and keys triggers per carrier; the judge treats `keys-first:`, `dated:` and the new exemption classes as today's (an unknown fact name, or any reason); `DatedBaseline` is empty and `DatedCeiling` 0. The facts compile and the census's new facts fail on main's behaviour; the main fact fails on 74 unlisted command sites, the stale `MainMenu`, launcher, view and property-row keys, and the guard-witnessed `IsEnabled` rows. Then the census code and the rewritten `M` make them pass. Each planted fact follows the census's existing pattern (`ThePlantedSitesAreCaught`, `:141-258`; `AFactWitnessNamesTheSiteInItsCode`, `:304-342`).

1. `ThePlantedCommandSitesAreCaught`. Asserts the exact key set.
   - Planted XAML: a `Button` with `Command` (a site); the same with `Focusable="False"` (not); a `Hyperlink` (a site); a `CheckBox` (a site); a `MenuItem` in a `Menu` and one in a `ContextMenu` (not); a `local:CheckMenuItem` in a `Menu` (not, through `Resolve`); a `MenuItem` with `StaysOpenOnClick="True"`, and one whose style sets it (sites); a `MenuItem` outside any menu (a site); a `KeyBinding` in `Window.InputBindings` (not); two unnamed buttons in one named holder with different commands, and two with the same command (holder-and-command keys, ` #2`).
   - Planted C#: `_button.Command = cmd` (a site); `_field = new Button { Command = cmd }` in a constructor (a site); `item.Command = null` (not); `menuItem.Command = cmd` (not); `_button.SetValue(ButtonBase.CommandProperty, cmd)` and `BindingOperations.SetBinding(_button, ButtonBase.CommandProperty, …)` (sites); `_button.SetBinding(UIElement.IsEnabledProperty, …)` in a constructor (a `ui-state` site).
2. `TheIsEnabledTriggersAreKeyedOneByOne`: a style with two `DataTrigger`s setting `IsEnabled` False, one with `ElementName` and one with `Path=`; a `MultiDataTrigger`; a property trigger with `SourceName`; a setter with `TargetName`; a condition whose value is a property element; a trigger setting True (not a site); a style default False with a True trigger (`(style default)`); a style setter with `Value="{Binding …}"` (`(style binding)`); two identical triggers (` #2`); and a `Visibility` trigger (still one key per carrier). Asserts the exact keys.
3. `AKeysFirstWitnessNamesTheSiteItsTokenAndCallsTheRecorder`: planted fact sources given to the judge.
   - Holds: names the site and calls `KeysFirstRecorder.AssertTheKeysLeaveFirst` inside its `RunSta` lambda; and, for `MainMenu when Workspace.AddPropertySheet=True`, names `MainMenu` and `AddPropertySheet` and calls it.
   - Named, each: names the site with no call; calls with no name; the call only in a string; `nameof(KeysFirstRecorder.AssertTheKeysLeaveFirst)`; the method passed as a group; an unqualified `AssertTheKeysLeaveFirst` of the fact's own class; the call only inside a helper the fact calls; the list form only; `keys-first: NoSuchTests.Method`; and, for the sheet site, a fact naming `MainMenu` and `Workspace` but not `AddPropertySheet`.
4. `ADisableSiteTakesOnlyKeysFirstAnExemptionOrADate`: for an `IsEnabled` binding, an `IsEnabled` trigger and a command site, `guard: X` and a plain naming fact are refused; `exempt: its own press never turns it off: …` holds; a bare `exempt: …` is refused; `exempt: not focusable` holds on a `Focusable="False"` element and on a keyed style that sets it, and is refused otherwise; `exempt: never fires: …` holds with an existing cited fact and is refused with none or a missing one. For a `Visibility` site, `guard:` still holds.
5. `AnAlwaysOnExemptionReadsTheCommand`: planted types. Holds: a constructor assignment `_ => true`; a lazy `=> _c ??= new RelayCommand(…, _ => true)`; a property returning a field assigned `() => true` with `admitWhileExecuting: true`; a forwarder to a constant property; an interface member of the same name beside a constant one. Refused: `_ => IsOpen`; `AsyncRelayCommand` with `() => true` and no `admitWhileExecuting`; a type with no such property; a site whose command path ends in another name; a constant one beside a same-named property in another type that reads state; a constant one beside a record parameter of the same name.
6. `ADatedWitnessHasItsDateItsOwnerAndAMechanism` and `TheDatedRowsOnlyShrink`, on planted manifests and a planted baseline and ceiling: a well-formed row on a baseline key holds; another date, another owner, an empty mechanism, a key outside the baseline, and a dated row on a site that is not a disable site are each named; more rows than the ceiling, and fewer without lowering it, are each named with the ceiling to set. `TheDatedBaselineHoldsTheRowsOfD` pins the baseline's length and distinct keys.
7. `EveryClassOfKeyTakingSiteIsScraped` (`:125-135`) gains a key of each new kind: `xaml-command MainWindow.xaml SidebarExpandLoaded ExpandLoadedCommand`, `xaml-command MainWindow.xaml SidebarFileActionsExpander DeleteCommand`, `command SlateWindows.Bases.BaseSurfaceView..ctor _failedEdit` (as the implementer's `Add` names the constructor), and `xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.AddPropertySheet=True`.
8. `EverySiteThatCanTakeTheKeysAwayNamesItsWitness` (`:91-118`) on the rewritten `M`.
9. `T/KeysFirstRecorderTests.cs` (new; unit facts of the moved helper, proved by mutations, not red on main): a focused button turned off is named; turned off and on again within one dispatcher operation, it is named; a button turned off while the keys are elsewhere is not; the keys moved off first, then the button turned off, is not; an ancestor turned off with the keys inside the root is named; a focused `Hyperlink` turned off is named through the content overload; `AssertTheKeysLeaveFirst` fails with the holder's name.
10. `T/MenuRowCommandTests.cs` (new, Design 6): the Slate menu and the ⋯ menu, Enter and Invoke, with the `StaysOpenOnClick` control. It skips on a desktop that refuses a menu the keys, and runs in the local whole run on this interactive desktop.
11. The facts of Design 8: the converted ones are regressions on head; the two new ones are green on main (they pin the order or the guard as it is) and are proved by their local mutations.

Regression, not red: every other `CommandPaletteTests` and `EmptyPaneLauncherTests` fact; `RightPaneLeafRegionTests`, `SidebarTreeKeysTests`, `FindInNoteBarTests`, `ModalSurfaceTests`; `--filter "FullyQualifiedName~Census"`.

Mutations of the census and helpers (each must turn the named fact red; the PR lists each with its result):

- the judge accepts `guard:` on an `IsEnabled` site: 4;
- keys-first without the recorder check: 3 (no call); without the naming check: 3 (no name); without the token check: 3 (the sheet site);
- the recorder check by token instead of syntax: 3 (string, `nameof`, method group);
- the recorder check accepting an unqualified name: 3;
- the command scrape skips initializers and constructors, as state writes do: 1, and 8 (`_failedEdit` unlisted);
- `StaysOpenOnClick` ignored, in an attribute or a style: 1; menu ancestry not required: 1; `CheckMenuItem` not resolved: 1;
- `Hyperlink` dropped: 1; ordinal identities kept for unnamed buttons: 1;
- a constructor's `SetBinding` of `IsEnabled` skipped: 1;
- per-trigger keying collapsed to one key per carrier: 2, and 8; `ElementName`, `SourceName`, `Path=` or `TargetName` ignored: 2;
- a dated row accepted for a key outside the baseline: 6; the ceiling check removed: 6;
- always on accepting any lambda: 5; always on without the same-name rule: 5;
- `exempt: not focusable` accepted without the check: 4; `exempt: never fires` accepted without its cited fact: 4;
- the recorder records an `IsEnabled` change to True, ignores where the keys are, or drops an off undone in the same operation: 9;
- the menu-row fact reads the keys before `PreviewClick` (at activation): 10's Slate menu and ⋯ rows go red; it reads them after the dispatcher settles instead of at `Execute`: 10's control row goes red.

## The NVDA session

**Setup.** Under the desktop lock (shared with Principal V); first, no home-pool job running on this desktop (the runner name of in-progress jobs). NVDA 2026.3.1 with debug logging, its log cleared; speech read from the `Speaking` lines of `nvda.log`; keys sent with `SendInput`; a UIA focus-changed recorder running across each step, as CB-24's step 0 did, so the record says where the keys went as well as what was said. The branch's Release build, launched through `scripts/windows/launch-for-field-pass.ps1 -Vault '<fixture copy>' -Seed -Root '<root>'`, each launch on a fresh copy of the fixture and a fresh root made first (#1995).

**The fixture,** built by a script kept with the session's notes and never committed, its folder prefixed `cb17a-`: `n000.md` to `n099.md`; `bulk/` with 200 notes; `a.md` with frontmatter (`title`, `status`, `tags`), two citations and an embed `![[n001]]`; a bibliography and citation config; `folder-with-note/` with its folder note and `folder-without-note/`; `Templates/` with one template; `Queries.base`; `board.canvas`, and a canvas core loads as recovered read-only (`CanvasLoadDisposition.RecoveredReadOnly`, `S/Canvas/CanvasDocumentViewModel.cs:3296`) with a text card, for D6 (the worker makes one, or records D6 as not reached); and, outside the vault, `import-src/` with 2,000 small files. The saved query and the dashboard are database records (`S/WorkspaceViewModel.Bases.cs:240`, `:274`), so they are made in the app before C4 and C5 (the query builder's save from `Queries.base`, and the dashboard editor). B2, B3 and B6 move fixture notes to this desktop's shared Recycle Bin, as CB-9b's pass did; nothing empties it, and the `cb17a-` prefix tells the items apart.

**Launches.** One: A1 to A7, B1, B4, C3, C8 to C11 and D1 to D6, which change nothing a later step reads. Two: the Files writes, B2, B3, B6, B8 to B10 and B12 to B14. Three: B5 (an import) and B7 (a malformed `.slate/sidebar.json` at launch). Four: the sheets and leaves that write, C1, C2 and C4 to C7.

The branch changes no product file, so this one run is the record for `main` and for the head, and the PR says so. NVDA logs a `Speaking` line for a sentence it then cancels (CB-14's finding), so a line in the log is recorded as logged, not as heard to the end. "By reading" below is the expectation; the record is what the log and the focus recorder show, kept whatever it is.

**Steps.**

| # | Site | Setup and keys | By reading |
|---|---|---|---|
| A1 | launcher `when IsModalSheetOpen=True` | empty pane; Tab to New Note from Template…; Enter | the row goes off with the keys on it ("unavailable"), then the picker's first stop a turn later |
| A2 | the same | the same row; NVDA's activate (UIA Invoke) | the same |
| A3 | the same | keys on the Quick Open row; Ctrl+, | the Quick Open row goes off under the keys; Settings' first stop |
| A4 | `MainMenu when SettingsSheet=True` | F10 to "Slate menu"; Ctrl+, | unverified that the chord reaches the window in menu mode (reading :149); if it does, the menu goes off under the keys |
| A5 | `MainMenu when Workspace.TemplatePickerSheet=True` | Alt, Down into the Slate menu's first row; Ctrl+Shift+N | as A4 |
| A6 | menu row (Call 2) | Slate menu, Settings… row, Enter | the menu closes and the keys go back before Settings opens: no "unavailable" |
| A7 | menu row (Call 2) | a note open; its menu's Bulk Rename Properties… row, Enter | as A6 |
| B1 | `SidebarExpandLoaded` | folders loaded; Files toolbar, Expand loaded folders; Space | off while it expands, then on |
| B2 | Delete | `n001.md` selected; File actions; Delete; Space | off at once (busy flag); the row goes |
| B3 | Move checked to Recycle Bin | `n002.md`, `n003.md` checked (Space on each row); Batch actions; Space | off at once |
| B4 | Import… | File actions; Import…; Space; Escape the folder picker | off before the picker opens, on after |
| B5 | Cancel import | Import… with `import-src/`; Tab to Cancel import; Space | off when the import settles; reachable only if the import outlasts the keys' trip (recorded with its duration) |
| B6 | `CancelTrash` | Delete `bulk/`; confirm; reach the status bar's Cancel; Space | off at once (Stopping); the item collapses when the trash ends; reachable only if the trash outlasts the trip |
| B7 | `SidebarSettingsRetry` | `.slate/sidebar.json` malformed at launch; repair it on disk; Tab to Retry; Space | off and collapsed with the notice; the guard lands the keys |
| B8 | Folder note | `folder-without-note` selected; Space on Folder note | off once the folder row says it has one; the note may open |
| B9 | Delete folder note | `folder-with-note` selected; Space; confirm | off once the note is gone |
| B10 | Remove shortcut | Add shortcut on `a.md`; Shortcuts; select it; Remove shortcut; Space | off at once (selection null) |
| B11 | `MoveToRetry` | not reachable by keys: Move To fails to load only on a refused listing page (#2012's race with another session's commit) | recorded as not reached; rests on reading |
| B12 | Rename | `n005.md` selected; name `n005b`; Rename; Space | off and on again within the refresh; whether NVDA says anything is the question |
| B13 | File actions Move to… | `n006.md` selected; Move to…; pick `folder-without-note`; confirm | the keys come back to Move to…; off and on again as the tree refreshes |
| B14 | batch Move to… | `n007.md` checked; Batch actions; Move to…; as B13 | as B13 |
| C1 | `BulkRenamePreviewButton` | Ctrl+Shift+R from `a.md`'s editor; old key `status`, new key `state`; Preview; Space | off while previewing |
| C2 | `BulkRenameApplyButton` | after C1, Shift+Tab to Apply; Space; then again from the Files tree | as heard on 2026-11-17: "unavailable", and the keys leave the sheet |
| C3 | `CitationSummaryWalkButton` | `a.md`; Ctrl+Shift+J; Walk through; Space | off as the walk starts |
| C4 | `WrapPanel(QueriesRun)` | the saved query made first; Queries leaf; it selected; Delete…; Space; confirm | the group goes off with the keys on Delete… |
| C5 | `WrapPanel(QueriesOpenDashboard)` | the dashboard made first; it selected; Delete…; Space; confirm | as C4 |
| C6 | `WrapPanel(QueriesOpenBaseFile)` | `Queries.base` selected; rename it on disk with the keys on Open | off under the keys when the leaf refreshes |
| C7 | property Save, both rows | `a.md`'s properties; change `status`; Tab to Save; Space | off while writing, then off as clean |
| C8 | `_quickFilter` | `Queries.base` open; keys in its quick filter; edit the file on disk | off under the keys while it reloads |
| C9 | `_viewPicker` | the same with the keys on the view picker | as C8 |
| C10 | `_depth` | Connections leaf; keys on the depth control; close the note's tab | off when the anchor goes |
| C11 | inspector section | a graph tab; keys in an inspector section; make the graph not effective (a route by keys is found before the session, else recorded as not reached) | off under the keys |
| D1 | `QuickSwitcherClose` | Ctrl+O; Tab to Close; Space | the overlay closes and its command goes off |
| D2 | `FindInNoteClose` (keys-first by reading) | Ctrl+F in `a.md`; Tab to Close; Space | the keys go to the editor first: no "unavailable" |
| D3 | `EditorPopoverClose` | the caret on `a.md`'s embed; Ctrl+E (Preview Embed, `S/Commands/ChordTable.cs:896-898`); Close; Space | the popover closes; the keys follow at Background |
| D4 | `EditorPopoverOpenSource` | the same popover; Open source; Space | as D3 |
| D5 | `MoveToCancelButton` | Move to… on `a.md`; Tab to Cancel; Space | as D1 |
| D6 | `CanvasInspectionClose` | the recovered canvas; its text card's text (Canvas: Edit Card Text…); Close; Space | as D1 |

For each step the record holds the `Speaking` lines from the key to two seconds after the keys settle, the focus events, and where the keys were at the end; a step not reached says how far it got and why. The table goes into the PR body with the record filled in (Heard, Keys after), and into the session's report (Call 6). Nothing heard changes a row's class in this chunk: listening is not how an exemption is settled. A doubt about an exempt row is settled by a recorder fact (the red team's M1), and one that fails becomes a dated row in this PR before it merges, with the baseline count raised and said.

## Files

Tests: new `T/Support/KeysFirstRecorder.cs`, `T/KeysFirstRecorderTests.cs`, `T/MenuRowCommandTests.cs`; `T/Censuses/FocusIntegrityManifestCensus.cs` and a new partial `T/Censuses/FocusIntegrityManifestCensus.KeysFirst.cs` (the command scrape, the per-trigger keys, the judge's new kinds, the dated ratchet and their planted facts, so the existing file does not double); `T/Censuses/RegionBoundaryCensus.cs` (exposes `IsFocusable` and `Resolve`); `T/CommandPaletteTests.OverlayFocus.cs`, `T/EmptyPaneLauncherTests.cs`, `T/SidebarTreeKeysTests.cs`, `T/FindInNoteBarTests.cs` (Design 8). Data: `M`, rewritten by `SLATE_FOCUS_MANIFEST_UPDATE=1` and then assigned by hand. Docs: the session's report (Call 6). Every edited C# file is LF today (`git ls-files --eol`). New `.cs` files: `git add`, then `python3 scripts/apply-license-header.py`. `dotnet format --verify-no-changes --include` on every edited C# file.

**The census tier.** `FocusIntegrityManifestCensus` already carries `[Trait("tier", "census")]`; its partial file shares the class's attribute. `KeysFirstRecorder`, `KeysFirstRecorderTests`, `MenuRowCommandTests` and the edited fact classes read no census-only path and name no type that reads one (Design 1's last point), so none needs the trait. `python3 scripts/check_windows_census_trait.py` is run before the push.

## Overlap

- **CB-15c** (principal V, same wave) edits product files in panels, reading, workspace, lifecycle and commands, and `ShellFailureDetailCensus`. CB-17a edits none of them, but its census reads two kinds of thing there. From CB-17a's merge, a `Command=` control or an `IsEnabled` write that CB-15c adds or moves to another member needs a keys-first or exempt witness (a dated row is refused for a new key). And the always-on check reads command constructions in `VaultLifecycleViewModel.cs`, `WorkspaceViewModel.Properties.cs`, `WorkspaceViewModel.Citations.cs` and `WorkspaceViewModel.Templates.cs`, so a CB-15c edit to one can break a checked exemption. Whichever merges second merges `main` and runs this census. V is told now.
- **CB-32** is Rust only: no overlap.
- **CB-24** merged (#2014, 5b890dd9); it moved no focus site (its spec, Docs).
- **CB-17b (C10)** inherits the 46 dated rows, the baseline and the ceiling; it ends every row, then deletes the dated kind. Its sheet theory must call `AssertTheKeysLeaveFirst` in each fact's own code (Call 5), name `MainMenu` or the launcher style, and name each trigger's token (for the menu, the sheet property: `AddPropertySheet`, `MoveToSheet`, `SettingsSheet` and so on).
- **CB-22 (C10)** makes two editor context-menu rows unavailable in reading mode: menu rows, not sites, under Call 2's recommendation.
- **CB-16, CB-18, CB-35, CB-39 to CB-41** add or move focus sites later; each witnesses its own under the new rule. CB-16's new Cancel is keys-first from birth.
- **Plan §6.** `M` goes CB-17a first (:765). The `MainWindow` row (:764) names CB-17a, which edits no `MainWindow` file: the wave's docs PR drops it.

## Docs and registers

- No contract register row names #1866 or the keys-first rule (a search of `docs/plans/*_contracts.md` finds none). The manifest's `_doc` text changes (Design 4).
- `reading/04_focus_and_peers.md` is the reading and is not edited. Its corrections (Drift check) go to the wave's docs PR.

## Ledgers

- #1954 (Mac work owed): none. No core change; the Mac's menus and buttons are AppKit's.
- #1955 (deferred finds): one line, if Call 8 is ruled as recommended: other drivers turn controls off under the keys and no census judges them (a refresh's or a rescan's deselect, an in-place change of item, a find bar's mode switch), with the 25 command sites and 3 `IsEnabled` rows of the press class that read state. A further line only if the session hears a defect outside CD11's class.

## Verification

```
# PowerShell, from the worktree; the census and every touched class, red on the tests-first commit, then green
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~FocusIntegrityManifestCensus|FullyQualifiedName~RegionBoundaryCensus|FullyQualifiedName~KeysFirstRecorderTests|FullyQualifiedName~MenuRowCommandTests|FullyQualifiedName~CommandPaletteTests|FullyQualifiedName~EmptyPaneLauncherTests|FullyQualifiedName~SidebarTreeKeysTests|FullyQualifiedName~FindInNoteBarTests|FullyQualifiedName~RightPaneLeafRegionTests|FullyQualifiedName~ModalSurfaceTests"
# rewrite the manifest's keys (never during a full run), then assign each new row by hand
$env:SLATE_FOCUS_MANIFEST_UPDATE = '1'; dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~FocusIntegrityManifestCensus.EverySiteThatCanTakeTheKeysAwayNamesItsWitness"; Remove-Item Env:SLATE_FOCUS_MANIFEST_UPDATE
# every census
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~Census"
python3 scripts/check_windows_census_trait.py
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <each edited .cs>
# the whole unit run under the desktop lock before the push (c9-process step 10); MenuRowCommandTests and the menu theory run there, not on the hosted runners
```

## Acceptance

- Facts 1 to 8 red on the tests-first commit and green on head; 9 to 11 green with their mutations red; every mutation red; the regressions green.
- `M` holds 115 disable-site rows: 12 keys-first, 57 exempt, 46 dated (or the scrape's own numbers, which the PR states with the difference from this spec); `DatedCeiling` equals the dated count.
- The PR says that the three `MainMenu` overlay rows' witness, `TheMenuIsDisabledOnlyAfterTheKeysLeaveIt`, and `MenuRowCommandTests` skip on the hosted runners (#1680), and gives their local whole run's result.
- The session's record in the PR body and the report, every step heard, not reached, or recorded as unreachable with the reason.
- `gh pr view N --json closingIssuesReferences` is empty. No commit message or PR text puts a close, fix or resolve word before #1866, #1955 or any other number.
- PR title: `test(windows): the focus manifest reads command buttons and each enabled trigger, and dates the ones that go off under the keys`. Predicted count: found 1 (the other drivers, Call 8), fixed here 0, deferred 1 (#1955) or 0 if Call 8 is ruled otherwise, filed 0 or 1, plus any defect the session hears outside CD11's class.

## Calls for the lead

1. **One site per `IsEnabled` trigger** (Design 2), with the condition's token required of a keys-first fact (Design 4). Recommended: yes. A fifteenth sheet trigger on the menu, or a new trigger on any style, is then a new unlisted site, where today it joins its carrier's row unseen; CB-17b gets one row per sheet to end; and one fact can no longer witness all seventeen menu rows by naming `MainMenu`. Cost: 19 rows become 41, and the five rows whose keys change (menu, launcher, both views, Save) are assigned again by hand; #1867's six facts and CB-17b's theory must name each trigger's token. The alternative keeps one row per carrier and dates the menu's and the launcher's whole rows, which CB-17b ends in one stroke each.
2. **Menu rows are not command sites,** proved by `MenuRowCommandTests` (Design 6), with a `StaysOpenOnClick` row, and a `MenuItem` outside any menu, kept as sites. Recommended: yes. WPF closes a menu and gives the keys back before a row's command runs, so 135 rows would be 135 exemptions saying so. Cost: the proof skips on the hosted runners (#1680) and runs only in the local whole run; a menu row turned off by some other change while its menu is open is not this census's to see (none is known). The alternative lists all 135 rows, each exempt by that reason, and needs no menu fact.
3. **"Always on" is checked by Roslyn** (Design 4), the exemption naming the type, and refused when another shell type declares the same name with a command that reads state or cannot be followed. Recommended: yes, for the 18 sites it can show; the 8 constant ones it cannot show take the free-text class with the 25 that read state. The census holds every later button, and a reason a reviewer reads is how the reading undercounted. Cost: about 150 to 200 lines of census code with the same-name and forwarder rules, and one planted fact. The trim alternative keeps the check but drops the forwarder rule and refuses any name declared twice (about 80 lines; Save, Revert and the boolean `CheckBox` move to free text, 15 checked); the full alternative is free text for all 51.
4. **The five close buttons whose command goes off as their surface closes, with the keys moved after** (`QuickSwitcherClose`, the editor popover's two, Move To's Cancel, the canvas inspection's Close), are dated for CB-17b. Recommended: yes; each turns itself off with the keys on it by mechanism, and CD11 has no exception; CB-17b may find a surface's restore already lands the keys in the same turn, and then its keys-first fact is green on main and says so. `FindInNoteClose`, which returns the keys first by reading, is keys-first here with a new fact. Cost: CB-17b grows by five buttons the reading did not list. The alternative exempts them as their surface's closing (an R site the manifest already witnesses).
5. **A keys-first fact calls the recorder in its own code;** a call inside a helper is not followed. Recommended: yes; it is checkable by syntax and keeps the census simple. Cost: CB-17b's sheet theory must call `AssertTheKeysLeaveFirst` in its body, not in a shared opener helper. The alternative follows one call into a private method of the same type.
6. **The session's record is committed** as `docs/plans/18_windows_port/reports/cb17a_nvda_session_<date>.md` with its speech excerpt, beside the earlier field passes. Recommended: yes; CB-17b and the owner read it after the PR is closed. Cost: one more file under a census-only path (no census reads it). The alternative keeps it in the PR body only.
7. **Review:** a red team, then Codex rounds until it approves. Recommended: yes; the PR changes no product code, but the census code it adds will judge every later button, and a lax judge is the risk. Cost: the Codex rounds. The alternative is §4 rule 9's single red team for a light PR.
8. **Other drivers.** The class `exempt: its own press never turns it off` does not judge a control turned off under the keys by something other than its own press: a refresh's or a rescan's deselect (the selection verbs), an in-place change of item (the find bar's Previous and Next), a find bar's mode switch (Replace and Replace all). It is the same symptom as #1866, by drivers CD11 does not name, and the census's `_doc` says so. Recommended: one #1955 line naming the class, its 25 command sites and 3 `IsEnabled` rows, and the drivers found (the owner wants few new issues). Cost: the class stays on `main` without a census until a chunk takes it. Alternatives: file it as an issue (labels bug, windows), at the cost of one more issue; or date all 28 now, so CB-17b covers them with keys-first facts, at the cost of 28 more dated rows (74 in all) and about 25 more buttons in CB-17b, most of which no driver turns off in practice.

### Lead rulings (2026-11-21)

All eight calls go as recommended.

1. One site per `IsEnabled` trigger (19 rows become 41); a keys-first fact names the condition's token; #1867's six facts are edited to name their overlay (tests only).
2. Menu rows are not command sites, proved by `MenuRowCommandTests`. The PR's not-delivered list names its hosted skip (#1680) and the local whole run that ran it.
3. The full Roslyn check with the same-name and forwarder rules, plus its planted fact. If the code goes well past 200 lines, take the trim alternative and tell the lead.
4. The five close buttons are dated for CB-17b; `FindInNoteClose` is keys-first with its tests-only fact.
5. The recorder call is in the fact's own body.
6. The session record is committed under `docs/plans/18_windows_port/reports/`.
7. A red team, then Codex until it approves; the owner's stop rule applies.
8. One #1955 line naming the class, its 25 command sites, its 3 `IsEnabled` rows and the drivers found; the PR body's "For the owner" paragraph gives the two alternatives (file an issue, or date all 28 for CB-17b).

The PR body and this spec's delivery section record the drift: plan line 183's "never heard" is wrong (#1866's comment of 2026-11-17 heard Bulk Rename's Apply), and "about twenty" buttons is about thirty. CB-17a's worktree starts only after CB-32 is READY.

## Red team record

A fresh Opus agent at xhigh read the first draft against `origin/main` 47bcf77e: 0 blockers, 2 majors, 10 minors and 11 lows. Principal W ruled on 2026-11-21; every finding is taken.

- **M1, the "never off holding the keys" exemptions were wrong for some and untestable by listening (taken).** Rename, and both Move to… buttons, go off and on again with the keys on them when the refresh lets go of the selection (`FileManagement.cs:278-279`, `RescanReconcile.cs:106`, `:128-139`): they are dated now (table B). The find bar's close for an in-place change of item does not move the keys (`FindInNoteViewModel.cs:378-391`, `WorkspaceViewModel.cs:714`), and a mode switch turns Replace off (`:136-139`). The class is renamed `exempt: its own press never turns it off`, its limit is in the census's `_doc`, and the other drivers are Call 8. Rows 15 and 17 say they are dated because CD11 names the reading's groups. Design 1 says an off undone in the same operation counts, and fact 9 pins it. The session no longer promotes an exemption by listening; a recorder fact does.
- **M2, one fact naming `MainMenu` would have witnessed all seventeen menu rows (taken).** A keys-first fact on a per-trigger site also names the trigger's token (Design 4), refused in fact 3, with a mutation; #1867's six facts name their overlay (Design 8). The menu theory skips on the hosted runners (`:191`, `:350-363`), and the PR says so (Acceptance).
- **Minor 1, fact 10's mutation was backwards (taken).** Reading the keys at activation reddens the Slate menu and ⋯ rows; reading them after the dispatcher settles reddens the control row.
- **Minor 2, `TheKeysAreIn(MainMenu)` cannot see a submenu row (taken).** Design 6 walks logical parents to the menu, and to the ⋯ button's `ContextMenu`. Design 1's root wording is corrected to "a descendant that the site's change turns off with it".
- **Minor 3, ordinal identities broke the ratchet (taken).** Unnamed command controls are keyed by holder and command (Design 3).
- **Minor 4, same-named commands (taken).** "Always on" is refused when another type declares the name with a command that reads state or cannot be followed; eight sites move to the free-text class (Design 4, table B, Call 3).
- **Minor 5, fact 3's cases (taken).** The call inside the fact's lambda holds; `nameof`, a method group and an unqualified look-alike are refused; the comment case is dropped, since the syntax check never sees trivia.
- **Minor 6, menu-row exclusion (taken).** It needs a `Menu` or `ContextMenu` ancestor; a style setter of `StaysOpenOnClick` counts; `CheckMenuItem` is typed through `Resolve`, now exposed; fact 1 plants each.
- **Minor 7, the converted witnesses (taken).** The Clear filter wrap includes its drains; each converted witness has a local product mutation (Design 8).
- **Minor 8, the session's reach (taken).** The saved query and the dashboard are made in the app; the popover opens on an embed with Ctrl+E; D6 names its card (a text card of a recovered read-only canvas); the steps that change the vault run in four launches on fresh copies; the Recycle Bin is shared and nothing empties it.
- **Minor 9, the red run (taken).** The tests-first commit holds census stubs that keep main's behaviour (Tests).
- **Minor 10, size (taken).** L, in the header; Call 3 stays recommended, with its trim alternative.
- **Lows (taken, eleven).**
  - `WorkspaceTemplates.xaml`'s eight menu rows are the editor's three and the tab strip's five (step 0).
  - One name, `IsFocusable`, throughout.
  - The menu helpers move into `KeysFirstRecorder`.
  - The recorder has a content-element overload for a `Hyperlink`.
  - A constructor's `SetBinding` of `IsEnabled` and a style's own `IsEnabled` setter with a markup value are sites (Designs 2 and 3; facts 1 and 2).
  - `TargetName`, `SourceName`, `Path=` and property-element values are keyed (Design 2; fact 2).
  - `FindInNoteClose` is keys-first with a new fact, and `EditorPopoverClose` stays dated (`EditorInteractions.cs:4153-4174`).
  - The welcome view's Quick Open and Search rows are exempt as never firing, with a new fact.
  - The overlay theory's hosted skip is in the PR.
  - The recorder's code names no census type (`check_windows_census_trait.py:398-407`).
  - CB-15c's overlap covers the always-on constructions, and the PR title no longer claims every control.

## What delivery changed

Merged as #2037 = 3c0dbe55 on 2026-11-21. It references #1866 and closes nothing; CB-17b closes #1866. No product code changed.

- **The manifest.** It has 12 keys-first rows, 56 exempt and 47 dated, with the baseline and the ceiling both at 47. The spec's 46 dated rows became 47 because the diff red team showed that Clear All Marks (`CanvasPromptClearMarksButton`) was wrongly exempt as "its own press". A fact shows it red on main: nothing raises its command's CanExecuteChanged, so it stays off on the open marks list. That stuck-off button joins CB-17b's scope, and the red test's source is kept with the lead's working notes, outside the repository, for CB-17b: one fact, `CanvasClearMarksButtonProbeTests.ClearAllMarksIsOnOnceTheMarksListOpens`, which opens a board, marks a card, opens the marks list and asserts that the Clear All Marks button is enabled (#1955 comment 7067701218).
- **The diff red team's other findings,** each fixed red first with mutations:
  - the recorder fails when its root never goes off, and it counts the keys inside popups;
  - the census reads only facts that run, and refuses Skip and Explicit;
  - fragile carrier keys are keyed by holder and command;
  - an unread syntax fails the census, with one code-built setter allowed and its reason given;
  - the main census fact went from 36 s to 16 s.
- **Codex.**
  - Round 1 found four future-edit bypasses, all fixed:
    - the root must hold the keys as the act starts;
    - a recorder call in an uninvoked local function does not count;
    - an owner-qualified `Property="UIElement.IsEnabled"` is read;
    - the allowed code setter is pinned per setter and condition.
  - Round 2 found one future-edit bypass. The owner answered "One more fix and round": a recorder call inside a lambda counts only when the lambda is handed to RunSta, PumpedDispatcher.Run or a Dispatcher's Invoke.
  - Round 3 found one future-edit bypass, `<X.IsEnabled>` property elements. The owner answered "Small fix, no new round": `.IsEnabled`, `.Visibility` and `.Focusable` property elements fail as an unread syntax.
- **Merging main.** #2035 (CB-22) added `EditorSurfaceCommandTests` calling `CommandPaletteTests.TheKeysAreIn`, which this branch had moved to `KeysFirstRecorder`. The merge commit ae3fc2ae points that call at the recorder.
- **The NVDA session** (13:06-13:49, record in `docs/plans/18_windows_port/reports/cb17a_nvda_session_2026-11-21.md`):
  - It heard the launcher's sheet trigger go off.
  - It did not hear the menu's sheet triggers, because the menu closes first.
  - Menu rows give the keys back before their command.
  - It found findings outside CD11, ruled by the lead:
    - A cancelled Import… box leaves the keyboard dead until Alt, and the launcher and Import's folder picker restore focus to the bare window. Both go to CB-17b first (#1866 comment).
    - The Queries leaf does not follow an on-disk base rename; Tab from a Files tree row stops silently on the main window; Shift+Tab can skip the action expanders (unconfirmed). All three go on #1955.
- **The merge judge** said MERGE, with 0 blockers, 0 majors, 3 minors and 4 nits (PR comment 7069934932). All seven are deferred to CB-17b on #1955 (7069935142). The ones CB-17b must weigh:
  - A witness that skips at run time on a desktop that refuses menus counts as running. The MainMenu keys-first rows and MenuRowCommandTests skip on the Namespace pool (#1680).
  - A witness is not tied to its recorder's root.
  - A dated row could be rewritten as an own-press exemption, with the ceiling lowered in the same edit.
- **Drift.** Plan §2.3:183's "never heard" is wrong: #1866's 2026-11-17 comment heard Bulk Rename Apply. "About twenty buttons" is 47 dated rows.
- **Count:** found 6, fixed here 0, deferred 6, filed 0.
