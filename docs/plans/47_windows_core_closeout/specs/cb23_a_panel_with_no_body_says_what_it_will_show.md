# CB-23: A right-pane panel with no body says what it will show, in a line core owns

Issue: #1800. Size S to M (M with Call 3's recommendation). Lanes: rust (a change under `crates/` runs every core lane), windows (the whole Windows lane, which a change under `crates/` starts), swift (the Mac lane's `swift test --parallel` and its CLI smoke run on the regenerated Swift bindings; no Swift source changes). AT: an NVDA spot check (plan §5), phrases on base and head. Wave: C12, slot 1. Depends on: nothing. Parallel with: CB-42 (this principal's other chunk; no shared file) and CB-34. Decision: CD15, answered by the owner on 2026-11-16 (plan §3.1): "Core owns the three empty-panel lines, not the host. The wording is the recommended pattern, one line per panel." Interlocks: plan §6's `crates/slate-uniffi/src/lib.rs` row ("never two in a wave"; the lead's brief takes it for this chunk because CB-16 has not started) and its `MainWindow.xaml` row, which orders CB-17b, CB-22b and CB-16 before CB-23, one a wave (plan `:721-723`, `:837`; CB-17b PR 2 is in flight as #2045 and CB-22b and CB-16 have not started: Call 6). Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9).

Written 2026-11-22 from the plan's rows (§3 CD15, §3.1, §5, §6, §7, §8's #1954 lines), `reading/05_spoken_failures.md:105-130` (read at origin/main 68e4f974), the issue and the owner's comment on it, #1954's and #1956's #1800 lines, and a step 0 run. Every file and line below was re-read at 68e4f974. Step 0 ran on 68e4f974 in this chunk's worktree with its own `CARGO_TARGET_DIR` and a Release build; its scripts, transcripts and NVDA log slices are kept with this chunk's working notes, not committed (`leaves.py`, `nvda\pass.ps1`, `nvda\run.ps1`, `nvda\transcript-base.txt`, `nvda\nvda-base.log`).

`C/` below is `crates/slate-core/src/`; `U/` is `crates/slate-uniffi/src/`; `S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`.

## The owner's answers and the lead's rulings (2026-11-23)

These supersede the calls below where they differ.

- **Call 1, the owner:** "Add the shortcut". Each line names the reading-view shortcut, for example "... Open the note in the reading view (Ctrl+Shift+E) to read its math." Core owns the words; the shortcut text comes from wherever the shell's shortcut names already come from, so the line cannot drift from the real binding.
- **Call 2, the owner:** "Vary by state". One line per panel for each of three states: a note in its editor, the note already in the reading view, and no note open. The owner sees every line before the tests-first commit.
- **The nine lines, the owner:** "Approve as written (Recommended)". All nine stand exactly as sent on 2026-11-23; the reading-view lines name no shortcut.
- **Call 7, the owner:** "File one issue (Recommended)". One issue for building the three Windows panels, linked from the PR: filed as #2048 (Milestone W).
- **Call 3, the lead:** (c). The line becomes the panel's focus stop; the size moves to M.
- **Call 4, the lead:** yes (one enum per dimension and one function).
- **Call 5, the lead:** yes. No VoiceOver pass is owed; #1954's #1800 line is corrected and the delivery recorded.
- **Call 6, the lead:** proceed in C12; whichever of the PRs lands second merges `main` in. #2045 merged first (38f6edde), and this branch starts from it.

**What they change in the design.**

- Core: `empty_panel_line(panel: EmptyPanel, context: EmptyPanelContext, reading_view_chord: &str) -> String`, with `EmptyPanelContext { NoNote, NoteInEditor, NoteInReadingView }`. The chord is the host's, handed in as program decision 12 hands in every host chord (`C/a11y.rs:50-56`); a blank one is left out. The reading view's own line names no shortcut, since there the shortcut leaves the view the line points to (the alternative wording that names it was put to the owner with the nine lines).
- The shortcut's source: `ChordTable.WindowsChordFor(ChordTable.Ids.ToggleViewMode)` for the line as shown, and `ChordTable.WindowsSpokenFor(...)` (contract P6's spoken form, "Control Shift E") for its accessible name. The same row is the note header's switch tooltip (`S/NoteHeaderView.cs:446-452`), and `ChordTableTests` holds the table to the window's real key bindings (`T/ChordTableTests.cs:402-461`), so a rebinding moves the line with it.
- Windows: `WorkspaceViewModel.ActiveLeafEmptyLine` and `ActiveLeafEmptyLineName` in a new partial file, `S/WorkspaceViewModel.EmptyPanels.cs`, with `PanelsWithoutBody` and the context from the active tab (`IsMarkdown`, `IsReadingMode`). They are raised in `ActiveLeaf`'s setter, in `FollowActiveTitle` (every sync of the active tab), and from the active tab's own `IsReadingMode` change (the handler, renamed `ActiveTab_PropertyChanged`, `S/WorkspaceViewModel.Layout.cs`).
- The tests carry the nine lines; the landing facts join `T/RightPaneNoticeLandingTests.cs` as a theory over the three panels and three states, with a fact that the line follows the note and its view.

**Files this chunk edits.** Core: a new `C/empty_panels.rs` and two lines of `C/lib.rs`. FFI: `U/lib.rs` (one enum, its `From`, one exported function), beside `sidebar_sort_order_label` (`:402-406`, the function at `:404`). Windows: `S/WorkspaceViewModel.cs` (a static map beside `Leaves`, `:3014-3035`, and `ActiveLeafEmptyLine` beside `MorePanelsName`, `:3074`, raised in `ActiveLeaf`'s setter beside it, `:3238-3240`) and `S/MainWindow.xaml` (the placeholder's line, `:2822-2825`). With Call 3's recommendation also: the placeholder's root (`:2766-2767`, `DirectionalNavigation="Contained"`), `S/MainWindow.ShellRegions.cs` (`VisibleLeafBody`'s exclusion, `:831-840`, and the landing loop, `:921-926`). Tests: `T/Censuses/RightPaneLeafBodyCensus.cs`, `T/Censuses/ConnectionsLeafCensus.cs` (`:764-769`, the anchor and its window), `T/RightPaneStripTests.cs` (one hosted fact); with Call 3 also `T/Censuses/RegionBoundaryCensus.cs` (`:158`), `T/Censuses/RegionGuardCensus.cs` (`:80`) and one landing fact. The generated bindings are regenerated and stay git-ignored. No Swift source. No `A11yEvent` is constructed or moved (contract 38's ledger does not move). No chord, help line, leaf, command or settings tab (the parity matrix does not move: the three leaves stay `pending`, `docs/plans/18_windows_port/parity_matrix.md:238-240`). No new `x:Name` or `AutomationId` (the placeholder keeps its `x:Name`; the line is found as its one `TextBlock`).

**Overlap.**

- PR #2045 (CB-17b PR 2, in flight, head 1a3f667c, a merge of `main` on 2026-11-23) edits `S/MainWindow.xaml` at its hunks `@@ -224,32` and `@@ -1133,7`, and `S/MainWindow.ShellRegions.cs` at `:273-305`, with `S/WorkspaceTemplates.xaml`, the sheets, other `MainWindow*.cs` files and the focus manifest. This chunk edits `S/MainWindow.xaml:2766-2825`, about 1,600 lines below #2045's last hunk there, and, under Call 3, `S/MainWindow.ShellRegions.cs:831-840` and `:921-926`. By reading the two merge without a conflict; whichever merges second merges `main` in first and reruns its lanes (Call 6).
- CB-42 edits only Connections model test files and `T/RescanTests.cs`.
- CB-16 (the import events) also edits `U/lib.rs` and `MainWindow*.cs` and has not started.
- CB-34 edits the command palette's core file (`reading/07_core.md:135-145`).

## Goal

A reader who opens Math, Code or Diagrams in the right pane meets a line that says what the panel will show and where that content is today, in core's words: one line per panel. The developer note "This panel is docked and ready for its feature surface." is gone. Every other panel is unchanged. Under Call 3's recommendation the line is the panel's stop, as every other empty panel's notice is, so a screen reader user meets it on entering the pane. The Mac, which has bodies for all three panels, shows none of the lines and changes nothing.

## Current state on main (68e4f974)

- **The placeholder.** `RightPaneDockedPlaceholder` (`S/MainWindow.xaml:2766-2826`) is a `StackPanel` in column 0 of the leaf host: a `PanelToolbarView` titled with the open leaf's title (`:2821`, a level 2 heading, no buttons) and a `TextBlock` with the sentence (`:2822-2825`). Its style collapses it for 14 leaf ids (`:2768-2819`).
- **Who finds it by its sentence.** `RightPaneLeafBodyCensus` (`:25-26`; the lookup `:36-39`; the fact `:28-81`) and `ConnectionsLeafCensus.TheLeafBodyIsGatedOnItsIdAndThePlaceholderRetiresForIt` (`T/Censuses/ConnectionsLeafCensus.cs:754-775`, the placeholder block `:764-769`, which reads the 4,000 characters before the sentence for the placeholder's `connections` trigger). By reference: `S/MainWindow.ShellRegions.cs:840` (`VisibleLeafBody` excludes it, so `RightPaneHasContentStop`, `:58-62`, the F6 ring, `:218`, and `:1282` pass it over) and `:923` (the landing loop excludes it), `T/Censuses/RegionBoundaryCensus.cs:158`, `T/Censuses/RegionGuardCensus.cs:80`, `docs/plans/18_windows_port/w_c_automation_ids.json:1984` (its `x:Name`).
- **Every other empty panel's notice is its stop.** "An empty leaf's stop is its notice ... the no-note, failure and empty sentences are final and take the keys" (`S/Panels/RightPanePanelsViewModel.cs:359-364`), bound as `Focusable="{Binding Panels.*NoticeIsAStop}"` with the `PanelEmptyMessage` style (`S/MainWindow.xaml:1665-1669`, and `:1721`, `:1807`, `:1853`, `:1929`); contract 40's AR-6 (`docs/plans/40_nvda_matrix_remediation_contracts.md:142`).
- **The pattern core already has.** A label only Windows shows, owned by core and called by no Swift: `more_panels_name` (`C/more_panels.rs`, `U/lib.rs:372-376`, read by `S/WorkspaceViewModel.cs:3074`); and DS-17's enum, `From` and `&'static str` label, `sidebar_sort_order_label` (`C/sidebar_sort.rs`, `U/lib.rs:378-406`).
- **The Mac** has bodies for Math, Code and Diagrams (`apps/slate-mac/Sources/SlateMac/ContentBlockPanels.swift:73`, `:134`, `:202`) with its own empty states (`:88`, `:93`, `:153`, `:208`), and its own leaf ids `math`, `code` and `diagrams` (`apps/slate-mac/Sources/SlateMac/Workspace/RightPaneView.swift:22-33`).
- **Nothing open tracks the three Windows panels.** The parity matrix lists them `pending` against #729, #730 and #731, which are closed: they delivered the reading view's math, diagrams and code, not the panels (Call 7).

### Step 0, as run (2026-11-22)

- **The placeholder's reach,** from the XAML and `WorkspaceViewModel.Leaves` (`leaves.py`): 17 leaves, 14 collapse triggers; it shows for `math`, `code` and `diagrams` only, all three in More panels (the strip is the first five leaves, `S/WorkspaceViewModel.cs:3040-3043`).
- **NVDA on base** (NVDA 2026.3.1, `--debug-logging`, the laptop layout; base is main's Release build copied aside; under the desktop lock, 23:29 to 23:30; `nvda\transcript-base.txt`). `source.md` open in the editor; F6 lands in the Outline leaf ("Outline, list", "Level 1 heading: Source, not selected, 1 of 1", "Outline panel."); Tab: "Right pane panels, list", "Outline, 1 of 5"; Tab: "More panels, button"; Enter: "Embeds, not checked, 1 of 12"; Down to Math; Enter:
  - heard: "checked", then **"More panels, Math open, button"**; the keys stay on More panels. Nothing of the panel is spoken.
  - The right pane's control view, on screen: Text "Math" (the toolbar's title), **Text "This panel is docked and ready for its feature surface."**, then the rail (the "Right pane panels" list of five and the button).
  - NVDA's object navigation from the button: previous object "Right pane panels, list", previous **"This panel is docked and ready for its feature surface."**, previous "Math", previous "Filter, grouping" (out of the pane); next object back the same way.
  - Code and Diagrams: the same, with "More panels, Code open" and "More panels, Diagrams open", the titles "Code" and "Diagrams", and the same sentence.
- So a focus-mode reader hears the pick and never the line: F6 passes the pane's content over (it has no stop), and the line is two objects back from More panels. It is reached only by object navigation or NVDA's screen review.

## Design

1. **Core.** `C/empty_panels.rs` (with the AGPL header the license script adds), a private module re-exported from `C/lib.rs`:

   ```rust
   //! The line a right-pane panel shows while a host has no body for it (plan 47 CB-23, #1800, CD15).
   //! Windows offers Math, Code and Diagrams before it has built them, and a reader meets this line
   //! under the panel's title. The words are core's, as every accessible sentence is; the Mac has a
   //! body for each panel and shows none of them.

   /// A right-pane panel a host offers before it has a body for it.
   #[derive(Debug, Clone, Copy, PartialEq, Eq)]
   pub enum EmptyPanel { Math, Code, Diagrams }

   /// What the panel will show and where that is today: one line per panel.
   pub fn empty_panel_line(panel: EmptyPanel) -> &'static str { /* Call 1's three lines */ }
   ```
2. **The FFI** (`U/lib.rs`, after `sidebar_sort_button_name`): `#[derive(Debug, Clone, Copy, PartialEq, Eq, uniffi::Enum)] pub enum EmptyPanel { Math, Code, Diagrams }`, its `From<EmptyPanel> for core::EmptyPanel`, and `#[uniffi::export] pub fn empty_panel_line(panel: EmptyPanel) -> String { core::empty_panel_line(panel.into()).to_owned() }`, each with a doc comment naming CB-23, as DS-17's are. C# sees `SlateUniffiMethods.EmptyPanelLine(EmptyPanel.Math)`; Swift sees `emptyPanelLine(panel:)` and calls it nowhere. No name collides (searched: the repository and the generated C#).
3. **Windows: the words.** The host owns which of its leaves has no body; core owns the words.
   - `WorkspaceViewModel.PanelsWithoutBody`: a static read-only map, written as a literal of ids (`math`, `code`, `diagrams`) to `EmptyPanel`, beside `Leaves`.
   - `WorkspaceViewModel.ActiveLeafEmptyLine`: `PanelsWithoutBody.TryGetValue(_activeLeaf.Id, out EmptyPanel panel) ? SlateUniffiMethods.EmptyPanelLine(panel) : null`, raised in `ActiveLeaf`'s setter beside `MorePanelsName` (`:3238-3240`), as DS-16's name is. (The first draft put it on the `WorkspaceLeafOption` record; that would make the record's printed form call into the native library and tie the record to the workspace's statics.)
   - `S/MainWindow.xaml:2823`: `Text="{Binding ActiveLeafEmptyLine}"` in place of the sentence.
4. **Windows: the reach** (Call 3's recommendation). The line takes the `PanelEmptyMessage` style and `Focusable="True"` (it shows only while a panel without a body is open, and it is final); the placeholder's `StackPanel` gains `KeyboardNavigation.DirectionalNavigation="Contained"`, as every leaf body root has; `VisibleLeafBody` (`S/MainWindow.ShellRegions.cs:831-840`) stops excluding it, so `RightPaneHasContentStop`, the F6 ring and the leaf host's landing find its stop; the landing loop (`:921-926`) includes it; `RegionBoundaryCensus` and `RegionGuardCensus` drop their exclusions. A pick from More panels still returns the keys to More panels (DS-16). Whether the focus manifest or any other census registers the new stop is checked in implementation and reported.
5. **The two censuses** find the placeholder by its `x:Name`. `ConnectionsLeafCensus`'s window changes with its anchor: it reads from the `x:Name` forward to the placeholder's `</StackPanel.Style>` (the 4,000 characters before the `x:Name` hold no `connections` trigger; the red team ran the arithmetic).

## Tests (red on main first, where a behaviour is new)

- **Core** (`C/empty_panels.rs`): `each_panel_without_a_body_has_the_owners_line`: the three exact lines (Call 1). Red on main by a build failure (the function does not exist), then red by assertion against a stub returning `""`.
- **Windows, hosted** (`T/RightPaneStripTests.cs`, `APanelWithNoBodySaysWhatItWillShowInCoresWords`): a shown shell; for each of `math`, `code` and `diagrams`, `ActiveLeaf` set and settled: the placeholder is visible, its one `TextBlock`'s text and its automation peer's name are the panel's exact line (Call 1's literal) and, with the FFI, `SlateUniffiMethods.EmptyPanelLine` of the panel; for `outline`, a leaf with a body, the placeholder is collapsed. Red on main by assertion (the old sentence), compiled against main.
- **Windows, landing** (Call 3): `T/RightPaneNoticeLandingTests.cs` (W7-7 PR 4, contract 40's R-5: "an empty right-pane leaf's stop is its notice") gains the three panels as cases of `AnEmptyLeafLandsOnItsNoticeAndKeepsEveryArrow`, with one state ("no body"): the landing puts the keys on the line, the line's name is core's sentence, and every arrow from it stays in the leaf. A hosted fact adds that F6's next pane from the editor lands on the line, and that a pick from More panels still leaves the keys on More panels. Red on main (no stop; F6 lands on the rail).
- **Windows, census** (`T/Censuses/RightPaneLeafBodyCensus.cs`):
  - `EveryLeafWithABodyRetiresThePlaceholder` finds the placeholder by `x:Name="RightPaneDockedPlaceholder"` (a refactor; its rule is unchanged).
  - New, `ThePlaceholderSaysCoresLineForEveryLeafItShowsFor`: the placeholder's one `TextBlock` binds `Text` to `ActiveLeafEmptyLine` and to nothing else; the leaves the placeholder shows for (the leaves less its collapse triggers) are exactly the keys of `WorkspaceViewModel.PanelsWithoutBody`, read by reflection so the fact compiles on main; and no shell source (`.cs` or `.xaml` under `S/`, skipping `bin/` and `obj/` as `ConnectionsLeafCensus.cs:28-40` does) spells "feature surface" or any of core's three lines. Red on main.
  - `ConnectionsLeafCensus` (`:764-769`): the anchor and its window move to the `x:Name` (a refactor; its assertion is unchanged).
- **Mutations**, each red, each reverted: the binding points at `ActiveLeaf.Title`; `diagrams` leaves the map; `outline` joins the map; core swaps two arms; the XAML carries a literal line; the property is not raised in `ActiveLeaf`'s setter; (Call 3) the line is not focusable; `VisibleLeafBody` excludes the placeholder again.
- **Runs:** the Rust gates as plan 44 §4 rule 3 names them (`cargo fmt --all -- --check`, `cargo clippy --all-targets --workspace -- -D warnings`, the whole `cargo test -p slate-core`, `cargo bench --workspace --no-run`, and `scripts/check-license-headers.sh` through Git Bash after `git add` and `python3 scripts/apply-license-header.py` on the new `.rs` file); `cargo test -p slate-uniffi`; the bindings regenerated (`./apps/slate-windows/generate-bindings.ps1 -Locked`); `dotnet build`; the touched classes under the lock; `dotnet format --verify-no-changes --include` on each C# file after every edit; and the whole Windows unit run under the lock before the push. The Mac lane builds and tests the regenerated Swift bindings.

## The NVDA spot check

On base and head, the same script (`nvda\pass.ps1`): `source.md` (a heading, prose, a `$$` block, a Python fence and a Mermaid fence) open in the editor; F6 into the right pane and Tab to More panels; Enter, Down to the panel, Enter; then NVDA's object navigation, four objects back and four forward; for Math, Code and Diagrams. Under Call 3 the head pass adds, for Math: F6 from the editor into the right pane, and Shift+Tab from the strip.

Expected on head, to be heard and not assumed: the pick unchanged ("checked", "More panels, Math open, button"); object navigation back from the button: "Right pane panels, list", then core's line for the panel in place of the old sentence, then the title; for each of the three. Under Call 3: F6 into the pane with Math open speaks core's Math line where base spoke the rail; Shift+Tab from the strip speaks it too. The PR records both transcripts' lines.

## #1954, #1955 and #1956

- #1954 (Mac work owed): its "Checks" list holds #1800's line, which says the lines show "while they have nothing to list". They show whenever the panel is open on Windows, since Windows has no body for them. Nothing the Mac shows or speaks changes, so no VoiceOver pass is owed (Call 5); a comment on #1954 corrects the line and records that CB-23 delivered it, so a Mac adopter does not put `emptyPanelLine` in place of "This note has no math blocks.".
- #1955 (deferred finds): none so far.
- #1956 (CD38's catalogue of host-composed sentences): its row "The right-pane placeholder ... FFI addition if core-owned; see #1800" and its note that CB-23 delivers it. A comment marks the row delivered.

## PR body

`Closes #1800`; the tests added; no contract register row (none names #1800); the NVDA phrases on base and head; the FFI addition and its consumers (Windows reads it; the Mac compiles it and calls it nowhere); `not delivered: the Swift bindings regenerated locally (no Mac here; the Mac lane builds and tests them)`; the #1954, #1956 and #1955 notes; "Found and fixed here" with any finds; the count line. No close, fix or resolve word before #1954, #1955, #1956 or any number but #1800. `gh pr view N --json closingIssuesReferences` shows #1800 alone.

## Calls for the lead

1. **The three lines, exactly** (new spoken copy; the owner sees it before the tests-first commit):
   - Math: "Math for this note isn't listed here yet. Open the note in the reading view to read its math." (the owner's example, word for word)
   - Code: "Code blocks for this note aren't listed here yet. Open the note in the reading view to read its code blocks."
   - Diagrams: "Diagrams for this note aren't listed here yet. Open the note in the reading view to read its diagrams."

   Recommended as written: the owner's pattern, one line per panel; "Code blocks" because the panel lists a note's code blocks (the Mac's empty state says "its code blocks"). "Reading view" is the reading surface's name (`S/Reading/ReadingSurface.cs:70`) and the owner's example's term; the app also says "Reading" (the note header's choice, `S/NoteHeaderView.cs:105`; "Reading view" is its compact tooltip, `:448`) and "Reading Mode" (the menu, `S/MainWindow.xaml:558`; the palette, `S/Commands/ChordTable.cs:855-856`). No chord, because the owner's example has none. Cost: a reader is not told the key. Alternative: the line takes the host's display chord as a parameter, as core's templates may under program decision 12 (`C/a11y.rs:50-56`), for example "Open the note in the reading view (Ctrl+Shift+E) to read its math." (`S/Commands/ChordTable.cs:854-856`). Cost: words the owner has not read, and a chord parameter on the FFI.
2. **The line when no note is open, or when the note is already in the reading view.** Recommended: the same line in every state, as "one line per panel" says. Cost: with no note open (no tab, or a canvas, base or graph tab), "this note" and "the note" name nothing; with the note already in the reading view, the line asks for what is already so. Alternative: a wording that holds in every state, for example "Math isn't listed in this panel yet. To read a note's math, open the note in the reading view." Cost: it departs from the owner's example, so the owner reads it first.
3. **How a screen reader reaches the line.** Measured on base: the pick speaks the button's name and nothing of the panel; F6 passes the panel over; the line is reached only by object navigation or screen review. Recommended: (c) the line is the panel's stop, as every other empty panel's final notice is (`S/Panels/RightPanePanelsViewModel.cs:359-364`; contract 40's AR-6), so F6 into the pane and Shift+Tab from the strip land on it and NVDA speaks it (Design 4). Cost: `S/MainWindow.ShellRegions.cs` and two region censuses join the chunk (about M); `ShellRegions.cs` is also in #2045 (different hunks); a new focus stop is heard, so the NVDA spot covers F6 and Shift+Tab; the pick itself still speaks only the button's name. Alternatives: (a) keep today's reach, so the chunk changes the words and not where they are heard. Cost: a focus-mode NVDA reader never meets the line, which the issue's own condition for core ownership ("if a screen reader can reach it") and the Goal would then have to say. (b) The line as More panels' description (`AutomationProperties.HelpText`) while such a panel is open, so NVDA speaks it after the button's name on the pick and on every return to the button. Cost: it changes what the rail speaks on every visit and adds a second route to the same words.
4. **The FFI's shape.** Recommended: one enum and one function, `empty_panel_line(EmptyPanel)`, DS-17's shape. Cost: the host keeps a three-entry map from its leaf ids to the enum (which panel has no body is a host fact), and the census holds that map to the XAML. Alternatives: a function keyed by the leaf id string, returning an optional line (the ids are shared with the Mac's `Leaf` enum, but which panel lacks a body differs by host, so core would carry a Windows-only fact); three functions (more surface for the same three strings).
5. **#1954.** Recommended: no new #1954 line and no `not delivered: VoiceOver pass`, because the Mac shows and speaks none of the lines (plan §4 rule 5 asks for the pass where Mac speech changes); the PR names #1954's existing #1800 check line, and a comment on #1954 corrects its "while they have nothing to list" and records the delivery. Cost: none. Alternative, the brief's words: a #1954 line for a VoiceOver pass. Cost: a pass owed for speech the Mac never produces.
6. **The `MainWindow.xaml` row of plan §6.** It orders CB-17b, CB-22b and CB-16 before CB-23, one a wave. Recommended: proceed in C12 as the lead's brief says, the hunks being far apart (and, under Call 3, `ShellRegions.cs` too); whichever of #2045 and CB-23 merges second merges `main` in and reruns its lanes; CB-22b and CB-16 branch after CB-23 merges, or merge it in. Cost: one merge of `main` into whichever lands second, and the row's order recorded as changed by the lead. Alternative: wait for #2045 to merge before CB-23's branch starts. Cost: CB-23 waits on CB-17b PR 2's review.
7. **The three Windows panels have no open issue.** #729, #730 and #731 are closed (they delivered the reading view's math, diagrams and code), yet the line's "yet" promises the panels. Recommended: file one issue for Milestone W, "windows: the Math, Code and Diagrams panels list a note's blocks", that the parity matrix's three `pending` rows can point at later. Cost: one issue. Alternative: a #1955 line. Cost: a feature, not a defect, on the deferred-finds list.

## Red team record

One Opus red team at xhigh on the first draft, 2026-11-22 (1 blocker, 3 majors, 14 minors). All taken:

- B1: moving `ConnectionsLeafCensus`'s anchor to the `x:Name` with its window unchanged would turn it red (the window reads the 4,000 characters before the anchor). The window moves with the anchor (Design 5).
- M1: Call 3 left the Goal false for a focus-mode reader and missed the repository's own rule for an empty panel's notice. Option (c) is added and recommended; the plan §4 rule 1 citation is dropped (rule 1 sizes found defects, not the chunk's own feature).
- M2: Call 1's reason for no chord was false (core templates can take a host chord, `C/a11y.rs:50-56`). The real reason is given, with the chord as the alternative.
- M3: the Rust gates now follow plan 44 §4 rule 3, with the license header step for the new `.rs` file.
- Minors: line numbers corrected; the closed parity-matrix issues (Call 7); the mixed terms for the reading view; DS-17 as the closer precedent and Call 4's cost corrected; the line moved from the record to a workspace property, as `MorePanelsName` is; the refactor facts no longer claimed red; the hosted fact finds the one `TextBlock`, and the phrase scan skips `bin/` and `obj/`; #2045's head and plan §6's `MainWindow.xaml` order (Call 6); #1956 named; #1954's line corrected by a comment; the PR body items; the lanes; the NVDA expectations' order and wording; Call 2's reading-view state.

A second Opus red team at xhigh on the diff at 76394cd0, 2026-11-23 (1 blocker, 2 majors, 6 minors). All taken in 672c9cf6 except as noted:

- B1: `FocusIntegrityManifestCensus` failed: the placeholder's Visibility trigger became a site once its line was a stop, and the empty-notice style's guard list did not name the placeholder. The manifest gains the row, witnessed by `RightPaneHideLandingTests.ALeafSwitchedUnderTheKeysLandsThemInTheShownLeaf` with a Math case, and the guard list names it.
- M1: the active tab's own `IsReadingMode` raise was pinned by nothing (the mutant survived); the following fact now toggles the tab outside a workspace mutation, the create-from-template route, and the mutant is red.
- M2: the F6 ring's route and the switch under the keys had no fact; `RightPaneStripTests.APanelWithNoBodyIsInTheRingAndAPickStillReturnsToMore` and the hide-landing case pin them, and a mutant that drops the placeholder from `RightPaneHasContentStop` alone is red.
- Minors: the region census's summary and the chord note corrected; the PR body items; the reflection read kept (it made the fact compile on main for its red phase); no AutomationId on the line, named in the PR as a choice; NVDA's name-change speech on a view toggle with the keys on the line recorded as accepted.

## What delivery changed

- The owner's answers of 2026-11-23 replaced Call 1's and Call 2's recommendations: each line names the reading view's shortcut, and the line varies by state (nine lines, approved as written).
- The lead's Call 3 ruling (c) made the line the panel's stop; Design 4 delivered it, and the diff red team added the focus manifest row the stop requires.
- The line is a workspace property with a spoken twin (`ActiveLeafEmptyLineName`), not a record property, so the accessible name carries "Control Shift E".
- The hosted fact moved from `RightPaneStripTests` to a theory in `RightPaneNoticeLandingTests` beside the other notices' landing facts; the ring fact went to `RightPaneStripTests`.
- Two facts were added after the product: the focus-survival fact (AGENTS.md's republish rule) and the ring fact. Neither was red on main; mutants show each bites.
- The core facts were seen red only as a build failure on main (the module did not exist) and by the `core-swaps-words` mutant; no stub run was made.
- CI found a race in #2045's Move To facts.
  - `ASheetWithNoRestoreClosedOverAnotherLandsTheOneLeftOnTop` failed twice on Namespace. The lead approved an instrumented probe lane, run 45292145048. It showed the cause: Move To's folder listing published under the card editor and closed Move To. The cause was not CB-23's placeholder.
  - The lead ruled a test-only fix on this PR (169ecf17): the helper waits for the listing. The product race is filed as #2053 (bug, windows), with the recommendation that the publish applies beneath.
  - Main 5e0846f5 was merged in (8b9465b2).
- The merge judge (MERGE, 2 minors, 3 nits) was taken in 7a698adc, 248d6daf and 3c537976. It added the Shift+Tab, leaf-switch and not-a-note facts, the line's automation id `PanelPlaceholderNotice`, the chord comment's correction and a word census that skips comments.
  - The leaf-switch fact found #2054, a notice keeper that lands on the rail for any leaf. The fact starts on a row.
  - Main ef0c780f (#2052, the app job's 80-minute limit) was merged in (ff099dfa).
