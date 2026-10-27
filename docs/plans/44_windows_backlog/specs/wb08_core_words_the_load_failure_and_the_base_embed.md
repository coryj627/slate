# WB-8 — Core words the failure: a tab that cannot load speaks core's sentence, and Ctrl+E titles a `.base` embed as a base

Issues: Closes #1266, Closes #1288. Size S. Lanes: rust, windows, swift (compile) — all three under either WD23 answer, because `embed_target_kind` / `EmbedTargetKind` is a new FFI export under both and the generated Swift binding must compile it (`swift-tests.yml:23-27` runs on any `crates/slate-core/**` or `crates/slate-uniffi/**` change; plan §7: a binding change adds `swift-tests.yml`); only the `A11yEvent` mirror arms, the corpus rows, the blind mac mirror entry and the mac designation are conditional on WD23's new-event answer. AT: NVDA spot (the two Windows sentences). Wave: W3. Depends on: nothing (WB-8 is alone in its wave on core's `a11y.rs` corpus, §6). Parallel with: WB-7, WB-9. Decision: WD23 (pending; recommended "reuse `FileReopenFailed`"; under the other answer — a new `NoteOpenFailed` — the corpus, the three mirrors (the mac one a blind edit), the artifact and the mac designation join the chunk, as the alternative design below says; the Swift lane is required either way).

## Amended at implementation (2026-10-27)

This section outranks the text below it.

- **WD23 answered as recommended:** reuse `FileReopenFailed`. The owner's approval of the plan 44 run, 2026-10-26, took every pending decision as recommended. No corpus row, mirror, artifact or mac designation changes. The Swift lane still compiles the new `EmbedTargetKind` binding.
- **Main's load failure reads `@path=note0.md`, not `@message=…`.** That is the binding's field token for the invalid-UTF-8 read. The facts therefore pin the detail to core's own `VaultErrorDetail` for the same read failure, and they reject any `@field=` token (`@\w+=`) rather than only `@message=`. Measured: the first draft of `ReopeningAnUnreadableNoteSpeaksItsFailureOnce` passed on main because it checked only for `@message=`.
- **`embed_target_kind` lives in `a11y.rs` and calls `session::classify_path`, now `pub(crate)`.** That is the scanner's own extension rule, so one rule decides what a base is and no copy can drift. Its fact also pins `base`, with no extension, and `folder.base/note.md` as notes.
- **`EmbedTitleRealizedSurfaceTests`' `Resolves` helper learns a base.** A whole-file resolution matches a `Base` row when core classifies its target as a base, and it matches a `Note` row only when core says note. The flipped row needed this for its leaf check.
- **A route speaks no success line after a failed load (red team).** The design's "speaks the failure before that route's own `InternalNavigated`, which is unchanged" was wrong. A High line is `ImportantMostRecent` under the dispatcher's shared activity ID, so the route's High `InternalNavigated` replaced the failure before it was heard. The Medium routes also followed it with a false success ("Opened x.md"). `WorkspaceViewModel.LandedOnAFailedLoad` (the landing tab has a `LoadFailureEvent`) gates every such line:
  - the editor's wikilink and reading-link route (`OpenEditorNavigation`, before the anchor too);
  - the right pane's opener (backlinks, outgoing links, embed sources);
  - the graph and connections rows' `OpenedFile`;
  - the bases row's `OpenedFile`;
  - search's `SearchResultOpened`. When the note was already showing, search speaks the failure itself, because no open ran to speak it.

  New facts: `W2EditorInteractionTests.AWikilinkToAnUnreadableNoteSpeaksOnlyItsFailure` and `RecoveryAnnouncementTests.AConnectionsRowOnAnUnreadableNoteSpeaksItsFailureAndNoSuccess`. The panel, graph, bases and search gates share the one predicate and have no fact of their own.
- **A tab's failure ends with the failure (red team).** The cached `LoadFailure`/`LoadFailureEvent` is cleared in three places:
  - when a save lands the tab's bytes (`AdoptLandedWrite`);
  - on a rename (`RetargetPath`), because the failure named the old file and the status line is already cleared;
  - when a peer's state is mirrored into the tab (`MirrorDocumentStateFrom`), which copies it along with the peer's text and status.

  Facts: `ARenamedFailedTabSpeaksNoStaleFailure` and `AnOpenBesideAHealthyPeerSpeaksNoFailure`. The save path has none: a deleted note's reopened tab cannot save until a rescan updates the index ("Something named note0.md already exists there").
- **A failed load is spoken once per load (CI, the Connections model).** The first push spoke the failure on every explicit open that landed on the tab. So a ghost's tab, opened before its note was created, repeated a stale failure when a later route landed on it (`GhostCreateAlreadyOpen`, 84 cells). `WorkspaceTabViewModel.TakeUnspokenLoadFailure` now hands the event to the first open that lands on the tab, then nothing until `Load` or a mirror runs again. A reopen's own line takes it too, so the reopen still speaks once. Fact: `RecoveryAnnouncementTests.AFailedLoadIsSpokenOnceAcrossOpensOfItsTab`. Removing the flag fails it at the second open.

  `LandedOnAFailedLoad` still keys on the event, not the flag. A later open of a failed tab speaks neither the failure again nor a success line over it.
- **The Connections model learns the open's failure where it is true.** None of these cells is stale: each one's tab is a fresh load that fails.
  - `pic.png` (an attachment) opens as a Markdown tab whose text read fails, in every entrance's re-root (88 cells). The failure comes before the leaf's line, except behind a dirty tab's dialog on a leaf the route showed. There the dialog pumps the frames the leaf's load may speak in, so the two come in either order: a local run heard the line first, and CI's shard 1 heard the failure first. The model takes those two lines unordered.
  - The composed routes that rename or delete the pin, or its folder, inside the open's dialog install the old path, which no longer loads: `RenamePinInDialog`, `RenamePinFolderInDialog`, `DeletePinInDialog`, `DeletePinFolderInDialog`.
  - `BackWithReservedRenamed` installs a path renamed inside Back's open, which also no longer loads.
- **Left as is:** a reopened tab's status line names the file (`note0.md`), while the reopen's spoken line names the tab as its group does (`note0`, OD-9). `Load` cannot know the group.
- **`CtrlEOnABaseEmbedTitlesItAsABase` also reads the popover's card** (`PopoverEmbedRoot.Title`, "Embedded base: Notes"), beside the announcement, the UIA name and the header.

## Goal

A note whose load fails today puts the binding's `VaultException.Message`
("@message=…" with field tokens) into the tab's `Status` and `LoadFailure`,
so a screen-reader user hears a diagnostic dump; and Ctrl+E on
`![[Projects.base]]` titles the popover "Embedded note: Projects.base"
while the reading view's card for the same embed says "Embedded base:
Projects". After this chunk both are core's words: the load failure is
`A11yEvent.FileReopenFailed` rendered with `vault_error_detail` ("Could
not reopen {filename}: {detail}"; WD23 recommended), spoken exactly once
by the command that opened the tab (the load itself records it and never
speaks, so a reopen is not announced twice), `VaultErrorDetailCensus`
guards the load site and the reopen's own catch so a `.Message` read
cannot return, and a core free
function `embed_target_kind` classifies the resolved target so the
popover, its UIA name, the embeds leaf and the announcement all read
"Embedded base: {name}".

## Current state on main

Verified in the worktree; the plan's and `report_a11y.md`'s lines hold.

- **#1266 site:** `WorkspaceViewModel.cs:1844-1848`
  `WorkspaceTabViewModel` load: `catch (VaultException exception) {
  LoadFailure = exception.Message; Status = $"Could not open {Path}: {exception.Message}"; }`.
  The save path already does it right: `:1128-1138` builds
  `NoteSaveBlocked(filename, SlateUniffiMethods.VaultErrorDetail(exception))`
  and sets `Status = A11yRender(blocked).Text`.
- **Who runs `Load` (`WorkspaceViewModel.cs:1828-1849`).** The tab's
  constructor (`:184`, before `InitializeEditorSession`, with `_announce`
  already set at `:157`) and `ReplaceItem` (`:569`). So `Load` runs on
  every explicit open (`TryOpenItem`, `WorkspaceViewModel.Layout.cs:110-131`,
  through `AddTab` `:341-374` or `ReplaceTabItem` `:305-323`), the Files
  selection preview (`ShowSelectionInTransientTab`, `:157-181`, quiet by
  rule R-2: `:76-79`, `WorkspaceViewModel.cs:2632-2639`), the closed-tab
  reopen (`ReopenClosedTab`, `Layout.cs:830-882`: `AddTab` at `:863`), the
  startup restore (`WorkspaceViewModel.Persistence.cs:70`), the
  same-item reloads (`WorkspaceViewModel.cs:2974`,
  `WorkspaceViewModel.History.cs:260`, `WorkspaceViewModel.Templates.cs:276`)
  and the rescan's re-seat and reload (`WorkspaceViewModel.Rescan.cs:430`,
  `:480`; the reload hands in text its worker read, so its `Load` cannot
  fail). Today `Load` announces nothing; only the reopen speaks a load
  failure: `ReopenClosedTab` announces (`:879`) what
  `ReopenFileAnnouncement` (`:884-905`) returns — `FileReopenFailed(filename,
  tab.LoadFailure)` when the tab's load failed (`:902-904`), and, from its
  own `CanonicalPath` catch, `FileReopenFailed(filename, failure.Message)`
  (`:898-901`), a second `.Message` read on the reopen half of #1266.
  `RecoveryAnnouncementTests.ReopeningUnreadableFileReportsFailureWithoutClaimingItIsMissing`
  (`RecoveryAnnouncementTests.cs:215-227`) pins a single "Could not
  reopen" line for an unreadable reopened note. The tab's `Status` is shown
  by its status line (`WorkspaceTemplates.xaml:591-595`); it has a private
  setter (`WorkspaceViewModel.cs:497-501`).
- `Censuses/VaultErrorDetailCensus.cs:56-61` `Sites()` lists three
  guarded sites (`StartSave`, `PublishSave`, `ResolveEmbedPreview`); neither
  the load site nor the reopen's catch is among them.
- Core: `a11y.rs:3401` `vault_error_detail(&VaultError) -> String`; FFI
  `lib.rs:11329`. `FileReopenFailed { filename, detail }`
  (`a11y.rs:677-680`, rendered `:1988-1990` "Could not reopen {filename}:
  {detail}", High at `:1872`) is posted by Windows' closed-tab reopen
  (`WorkspaceViewModel.Layout.cs:898-904`) and is in all three corpus
  mirrors (`a11y.rs:4062`, `A11yCorpusCensus.cs`, the Swift mirror). No
  "first open failed" variant exists.
- `A11yEvent` crosses the FFI through a hand-mirrored enum in
  `lib.rs:9164-9167` (`NoteSaveBlocked`) and a `From` arm at `:11041`;
  the corpus has three mirrors: core `a11y.rs:4340-4344`, Windows
  `Censuses/A11yCorpusCensus.cs:163-164`, mac
  `A11yCorpusCensusTests.swift:156-157` (the `NoteSaveBlocked` rows), and
  the committed artifact `tests/fixtures/a11y/corpus.json`, regenerated by
  `committed_corpus_artifact_matches_the_vocabulary` (`a11y.rs:7809`) under
  `SLATE_REGENERATE_FIXTURES=1` (`:7828-7830`). (Touched only under the
  new-event answer.)
- **#1288 sites:** `EditorInteractions.cs:1499-1507` `ResolveEmbedPreview`
  calls `_session.ResolveEmbedPreview(...)` and hands `preview.Resolution`
  to `BuildEmbedPreview` (`:1655-1689`), whose card data is
  `ResolvedEmbeds.Of(resolution)` (`:1683`); the leaf and nested cards go
  through `BuildEmbedPreviewNode` → `BuildEmbedNode` (`:1741-1800`), whose
  `FullNote` arm titles with `ResolvedEmbedTitle(ResolvedEmbeds.Of(full)!)`
  (`:1774`). `ResolvedEmbeds.Of` (`ResolvedEmbeds.cs:25-34`) maps
  `FullNote → ResolvedEmbed.Note` with no base arm. `:1602-1610` posts
  `EmbedPreviewShown(targetRaw, content.Resolved)` and sets
  `PopoverTitle`/`PopoverAutomationName` from it.
- The reading view decides "base" by host sniffing:
  `ReadingContentViewModel.cs:1071-1078` `fullNote.TargetPath.EndsWith(".base", OrdinalIgnoreCase)`
  → `ProjectBaseEmbed`; `ReadingDocumentBuilder.cs:1198` titles the card with
  `new ResolvedEmbed.Base(baseCard.TargetPath)`.
- Core: `ResolvedEmbed::Base { target_path }` (`a11y.rs:3305-3307`),
  rendered `:3353-3360` "Embedded base: {file stem}"; FFI mirror
  `lib.rs:6015`; `resolved_embed_title` export `lib.rs:11387`. Core's
  extension rule is `classify_path` (`session.rs:12588-12601`, lower-cased
  extension) and `save_query_as_base` refuses a non-`.base` path
  (`:19152-19156`). No free function exposes "is this target a base".
- Mac: `EmbedView.swift:52-63` and `:240-249` switch over
  `EmbedResolution` exhaustively with no default — a new `EmbedResolution`
  variant breaks the mac build (plan §2.2).
- Pins: `EmbedTitleRealizedSurfaceTests.cs:107` row `"base"` has
  `OnPopoverAndLeaf: false` ("a `.base` is a card of its own kind in the
  reading view only"); `W2EditorInteractionTests.cs:269`
  `AResolvedEmbedAnnouncesItsPreviewWhenTheResultLands` pins
  "Embedded note: target.md" (`:277`); `ReadingEmbedTests.cs:1365, 1386` pin
  "Embedded base: Notes" on the reading card;
  `EmbedPreviewTitleCensus.Sinks.cs` enumerates the title sinks.

## Reference

- #1266: contract 38 doctrine (core-rendered text); R-7 of contract 40 (the
  failed-save precedent, `WorkspaceViewModel.cs:1128-1138`); NVDA pass
  finding F6 (2026-09-22) is the same class. WD23 (plan §3, quoted):
  "Reuse `FileReopenFailed`: the sentence is truthful for a first open
  too, and it removes the corpus change, the blind mac mirror edit and the
  Swift lane from WB-8. Add `NoteOpenFailed` only if the owner wants
  'open' and 'reopen' to differ." The quote's "and the Swift lane" is
  wrong and is reported for the plan: reuse removes the event's mirror
  and corpus edits, not the Swift compile of the independent
  `embed_target_kind` export (#1288), which both answers add.
- #1288: mac's `BaseEmbedDocument` rule (`Bases/AppState+Bases.swift:351`):
  a `.base` embed is a base, never a note. Core's `ResolvedEmbed::Base`
  (`a11y.rs:3305`) is the card data both hosts already render from.
- §4 rule 6 of the plan applies only under the new-event answer: a new
  `A11yEvent` adds one entry to `A11yCorpusCensusTests.swift:50`'s mirror
  (blind text edit; mac lane compiles it).

## Design

**#1266, who speaks a load failure (both WD23 answers).** `Load` records
the failure and never speaks; the command that opened the tab speaks it,
once. An `_announce` inside `Load` would speak wherever `Load` runs (the
restore, the same-item reloads and the rescan's re-seat included) and
twice on a reopen: `ReopenClosedTab` builds the tab (`Layout.cs:863`, so
`Load` runs and would speak), then announces `ReopenFileAnnouncement`'s
`FileReopenFailed(filename, tab.LoadFailure)` (`:879`, `:902-904`) — the
same event twice under the recommended answer (and
`ReopeningUnreadableFileReportsFailureWithoutClaimingItIsMissing`'s single
"Could not reopen" line fails), "Could not open …" then "Could not reopen
…" under the other answer. The owners:
- `WorkspaceTabViewModel.Load` (`WorkspaceViewModel.cs:1828-1849`) speaks
  nothing. New `internal A11yEvent? LoadFailureEvent { get; private set; }`
  on `WorkspaceTabViewModel`, cleared with `LoadFailure` at `Load`'s start
  (`:1830`). The catch (`:1844-1848`) becomes
  `string detail = SlateUniffiMethods.VaultErrorDetail(exception); var failed
  = new A11yEvent.FileReopenFailed(System.IO.Path.GetFileName(Path), detail);
  LoadFailure = detail; LoadFailureEvent = failed; Status =
  SlateUniffiMethods.A11yRender(failed).Text;` — no `_announce`.
- The explicit open speaks the tab it lands on. In `TryOpenItem`
  (`Layout.cs:110-131`) the non-selection branch (`:116-119`, today
  `return TryOpenItemCore(…)`) becomes `bool opened = TryOpenItemCore(item, target, requestEditorFocus); if
  (opened && ActiveGroup.ActiveTab is { LoadFailureEvent: { } failed })
  { _announce(failed); } return opened;`. Every `true` return of
  `TryOpenItemCore` leaves the landing tab as `ActiveGroup.ActiveTab` (the
  global graph `:188` via `TryFocusGlobalGraph` `:325-339`, a split's new
  group `:423-424`, an existing tab `:218`, a new tab `:228`/`:241`, the
  in-place replace `:272-276`), so every explicit open — Enter, Ctrl+Enter, the
  splits, links, Back, and the graph, canvas, connections and bases opens
  that reach `OpenPathCore` (`WorkspaceViewModel.cs:2700-2719`) or
  `OpenItem` (`Layout.cs:98-104`) — speaks a failed tab's sentence once,
  including a tab that already shows the note (Enter on a Files row whose
  selection preview failed). The selection branch (`:121-130`) stays quiet
  (R-2: focus stays on the row). Opening a note into the wikilink route
  speaks the failure before that route's own `InternalNavigated`
  (`WorkspaceViewModel.cs:2668-2670`), which is unchanged.
- The reopen speaks the reopen. `ReopenClosedTab` does not go through
  `TryOpenItem`; its one announcement stays `ReopenFileAnnouncement`'s,
  which now reads core's detail through `tab.LoadFailure`, and its own
  `CanonicalPath` catch (`:898-901`) becomes `new
  A11yEvent.FileReopenFailed(filename,
  SlateUniffiMethods.VaultErrorDetail(failure))` (the reopen half of
  #1266; the issue names "load / reopen").
- Silent, as on main: the startup restore, the same-item reloads, the
  rescan's re-seat and reload, and the Files selection preview — the
  sentence is in the tab's status line.

`VaultErrorDetailCensus.Sites()` gains two rows: `{ "WorkspaceViewModel.cs",
"Load", "A11yEvent.FileReopenFailed", "Detail", 1, true }`
(`WorkspaceTabViewModel.Load`, `WorkspaceViewModel.cs:1828`, the file's only
method named `Load`, so `CSharpSource.Method("Load")` is unambiguous) and
`{ "WorkspaceViewModel.Layout.cs", "ReopenFileAnnouncement",
"A11yEvent.FileReopenFailed", "Detail", 1, false }` (its one catch is a
`VaultException` catch; `CSharpSource.Load("WorkspaceViewModel.Layout.cs")`
is the pattern of `CanvasAnnouncerCensus.cs:277`). The census reads
constructions; `LoadFailure` is assigned the same `detail` local the
construction reads, and the runtime fact pins `LoadFailure ==
VaultErrorDetail(e)`. No core change, no corpus change, no mac edit for
#1266.

**#1266, WD23 alternative (new event).** New `A11yEvent::NoteOpenFailed {
name, detail }` beside `NoteSaveBlocked` (`a11y.rs:902`), rendered "Could
not open {name}: {detail}" with `lead_then_sentence`'s period rule and
"Could not open {name}." for a blank detail (the `:2148-2155` shape);
priority High (`:1870` list); corpus rows after `:4340-4344` (`notes.md` /
"invalid UTF-8" and the blank twin); goldens beside `:7010`; FFI mirror
arm (`lib.rs:9164` region) and `From` arm (`:11041` region); Windows mirror
after `A11yCorpusCensus.cs:164`; mac mirror after
`A11yCorpusCensusTests.swift:157` (`.noteOpenFailed(name:detail:)`, blind);
`corpus.json` regenerated; a `recorded` mac-side designation in
`a11y_trigger_designations.json` (the `VaultRescanFinishedUncounted`
pattern at `:204`). The ownership above is unchanged: `Load` builds
`new A11yEvent.NoteOpenFailed(System.IO.Path.GetFileName(Path), detail)`
into `LoadFailureEvent` and `Status` and speaks nothing; `TryOpenItem`
speaks it on an explicit open; the reopen still speaks `FileReopenFailed`
(the owner chose "open" and "reopen" to differ), so a failed reopen says
"Could not reopen …" once and never "Could not open …" — the reopened
tab's status line reads the load's "Could not open {name}: {detail}."
(the load cannot know which command built the tab; the PR body says so).
The census's `Load` row names `A11yEvent.NoteOpenFailed`; the
`ReopenFileAnnouncement` row is unchanged.

**#1288 (both answers).**
- Core: new free fn `pub fn embed_target_kind(target_path: &str) ->
  EmbedTargetKind` with `pub enum EmbedTargetKind { Note, Base }`, in
  `a11y.rs` beside `resolved_embed_title` (`:3340`), using `classify_path`'s
  lower-cased extension rule (move or re-export the rule so the two cannot
  drift; `session.rs:12588` stays the scanner's). `Base` iff the extension
  is `base`. Never an `EmbedResolution` variant.
- FFI: `#[uniffi::export] pub fn embed_target_kind(target_path: String)
  -> EmbedTargetKind` beside `resolved_embed_title` (`lib.rs:11387`); a
  `crosses_the_ffi` fact (the pattern at `:14480`). Both bindings
  regenerate. The generated Swift binding gains the enum
  `EmbedTargetKind` and the free function `embedTargetKind(targetPath:)`
  (UniFFI's Swift names) with no mac consumer and no name clash
  (`git grep -i -e EmbedTargetKind -e embed_target_kind` is empty on
  main), so no mac source changes — but the binding is new Swift code,
  so `swift-tests.yml` must be green under BOTH WD23 answers (it
  triggers on this PR's `crates/` paths, `swift-tests.yml:23-27`).
- `ResolvedEmbeds.Of`: `EmbedResolution.FullNote full when
  SlateUniffiMethods.EmbedTargetKind(full.TargetPath) is EmbedTargetKind.Base
  => new ResolvedEmbed.Base(full.TargetPath)` ahead of the `Note` arm. That
  one change retitles the popover (`:1683`), the embeds leaf and nested
  cards (`:1774`), the UIA name and the `EmbedPreviewShown` announcement
  (`:1602-1610`) — one sink, as `EmbedPreviewTitleCensus` requires.
- `ReadingContentViewModel.cs:1073-1075`: replace `EndsWith(".base")` with
  the core call; behaviour unchanged for the reading card.
- Not changed: the popover's BODY for a `.base` embed (core still returns
  `FullNote` text, the definition's YAML) — the issue asks for the title,
  name and announcement only; "not delivered: a base summary card in the
  Ctrl+E popover (C10 is the reading view's)".

## Tests

Facts fail first.

- Rust `a11y::tests::embed_target_kind_is_base_only_for_the_base_extension`
  — `Projects.base`, `PROJECTS.BASE`, `a/b.base` → Base; `note.md`,
  `base.md`, `x.base.md` → Note. Mutation: compare the raw suffix
  case-sensitively → `PROJECTS.BASE` fails.
- `lib.rs` `embed_target_kind_crosses_the_ffi`.
- `W1WorkspaceModelTests.ATabThatCannotLoadShowsCoresSentenceAndSpeaksNothing`
  (in `W1WorkspaceTests.cs`, whose classes are `W1WorkspacePersistenceTests`,
  `W1WorkspaceModelTests` `:124`, `W1SidebarAndRecentsTests`, … — there is
  no class `W1WorkspaceTests`, so a filter on that name selects nothing) —
  `FixtureVault.Create(1, …)`, `note0.md` overwritten with `[0xff, 0xfe,
  0xff]`, `VaultException e = Assert.ThrowsAny<VaultException>(() =>
  session.ReadText("note0.md"))`, `string detail =
  SlateUniffiMethods.VaultErrorDetail(e)`; a tab built directly (`new
  WorkspaceTabViewModel(session, new WorkspaceTabState(Guid.NewGuid(), new
  WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")), announce:
  announced.Add, startInteractionBackgroundWork: false)`, the
  `EmbedWalkCancellationTests.cs:95-101` shape): `LoadFailure == detail`
  (no `@message=`), `LoadFailureEvent` equals `new
  A11yEvent.FileReopenFailed("note0.md", detail)`, `Status ==
  A11yRender(LoadFailureEvent).Text` ("Could not reopen note0.md: …"), and
  `announced` is empty. Mutations: restore `exception.Message` → the
  `LoadFailure` and `Status` assertions fail; add `_announce(failed)` to
  the catch → `announced` is not empty. (New-event answer: the same fact
  over `NoteOpenFailed("note0.md", detail)`.)
- `RecoveryAnnouncementTests.AnExplicitOpenOfAnUnreadableNoteSpeaksItsFailureOnce`
  — through the workspace (`RecoveryAnnouncementTests.Host`, `:364-394`):
  `note0.md` overwritten with `[0xff, 0xfe, 0xff]`, `Announced` cleared;
  (a) `Workspace.OpenPath("note0.md", fromSelection: true)` (the Files
  selection preview) adds no line whose rendered text starts "Could not ";
  (b) `Workspace.OpenPath("note0.md")` (Enter on the row: the existing
  transient tab is activated) adds exactly one, `Assert.Single(host.Rendered,
  r => r.Text.StartsWith("Could not ", StringComparison.Ordinal))`, High,
  equal to `A11yRender(tab.LoadFailureEvent!)`, typed `FileReopenFailed`
  (new-event answer: `NoteOpenFailed`), its `Detail` free of `@message=`;
  (c) `Workspace.OpenPath("note0.md", WorkspaceOpenTarget.SplitRight)` (a
  new tab, so a new `Load`) adds exactly one more. The "Could not "
  prefix counts the open and the reopen sentence alike, so the fact is
  the same under both answers. Mutations: drop the `_announce` from
  `TryOpenItem` → (b) and (c) find none; speak it from the selection
  branch as well → (a) finds one; `_announce(failed)` in `Load` → (c)
  finds two.
- `RecoveryAnnouncementTests.ReopeningAnUnreadableNoteSpeaksItsFailureOnce`
  — through the workspace: `host.CloseNote()`, `note0.md` overwritten with
  `[0xff, 0xfe, 0xff]`, `Announced` cleared, `ReopenClosedTabCommand`
  executed: exactly one rendered line starts "Could not " (a duplicate of
  either kind — the same event twice under the recommended answer,
  "Could not open …" then "Could not reopen …" under the other — fails
  it), it is `FileReopenFailed` under BOTH answers with `Detail ==
  tab.LoadFailure`, free of `@message=`, High, and no `ReopenedFile` or
  `ReopenTargetMissing` is posted. Mutations: `_announce(failed)` in
  `Load` → two lines; have the reopen route through `TryOpenItem` (or
  speak `tab.LoadFailureEvent` from `AddTab`) → two lines; restore
  `exception.Message` in `Load` → `Detail` carries `@message=`. The
  existing `ReopeningUnreadableFileReportsFailureWithoutClaimingItIsMissing`
  (`:215-227`) stays and passes.
- `VaultErrorDetailCensus.ACaughtFailureIsAnnouncedInCoresWords` — two new
  rows, `Load` and `ReopenFileAnnouncement`; mutations: the `.Message`
  read in the load catch → the `Load` row fails; `failure.Message` back in
  the reopen catch (main's `Layout.cs:900`) → the `ReopenFileAnnouncement`
  row fails (it fails on main first).
- `W2EditorInteractionTests.CtrlEOnABaseEmbedTitlesItAsABase` — fixture
  `![[Notes.base]]`: `PopoverTitle` starts "Embedded base: Notes",
  `PopoverAutomationName` equals `A11yRender(EmbedPreviewShown(…,
  ResolvedEmbed.Base("Notes.base"))).Text`, and the single `EmbedPreviewShown`
  carries `ResolvedEmbed.Base`. Mutation: restore the `Note` arm → fails
  in all three places (the issue's mutation).
- `EmbedTitleRealizedSurfaceTests` row `"base"` flips to
  `OnPopoverAndLeaf: true`; the leaf fact then covers the embeds leaf.
- New-event answer only: Rust
  `a11y::tests::note_open_failed_renders_core_detail_with_a_period`, the
  regenerated artifact fact, `note_open_failed_crosses_the_ffi_as_core_renders_it`,
  and both corpus censuses with the two new rows.

## Docs and registers

- Contract 38 ledger: regenerate (`python -I scripts/a11y_trigger_ledger.py
  --write`); the inventory counts constructions, so under the recommended
  answer the `FileReopenFailed` row gains the `Load` construction beside
  the reopen's two (the `TryOpenItem` announcement speaks a constructed
  event and adds no construction); under the new-event answer a
  `NoteOpenFailed` row appears with the `Load` site and a `recorded`
  mac-side designation, and the `FileReopenFailed` row is unchanged.
- `tests/fixtures/a11y/corpus.json`: regenerated under the new-event
  answer only.
- No `w_c_matrix.md` / `parity_matrix.md` row change.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb08'
function Assert-Ok([string] $What) { if ($LASTEXITCODE -ne 0) { throw "$What exited $LASTEXITCODE" } }
# Rust facts exist (cargo takes ONE positional filter; `-- --list` prints `path: test`):
$core = @(cargo test -p slate-core embed_target_kind -- --list); Assert-Ok 'slate-core --list'
if (-not ($core -match '^a11y::tests::embed_target_kind_is_base_only_for_the_base_extension: test$')) { throw 'embed_target_kind_is_base_only_for_the_base_extension is not discovered' }
$ffi = @(cargo test -p slate-uniffi embed_target_kind -- --list); Assert-Ok 'slate-uniffi --list'
if (-not ($ffi -match 'embed_target_kind_crosses_the_ffi: test$')) { throw 'embed_target_kind_crosses_the_ffi is not discovered' }
cargo test -p slate-core a11y::; Assert-Ok 'slate-core a11y tests'
cargo test -p slate-uniffi crosses_the_ffi; Assert-Ok 'slate-uniffi crosses_the_ffi tests'
# new-event answer only (run these three as well):
# $env:SLATE_REGENERATE_FIXTURES = '1'; cargo test -p slate-core committed_corpus_artifact_matches_the_vocabulary; Assert-Ok 'corpus regeneration'; Remove-Item Env:SLATE_REGENERATE_FIXTURES
# $n = @(cargo test -p slate-core note_open_failed -- --list); Assert-Ok 'slate-core --list'; if (-not ($n -match 'note_open_failed_renders_core_detail_with_a_period: test$')) { throw 'note_open_failed fact missing' }
# $n = @(cargo test -p slate-uniffi note_open_failed -- --list); Assert-Ok 'slate-uniffi --list'; if (-not ($n -match 'note_open_failed_crosses_the_ffi_as_core_renders_it: test$')) { throw 'note_open_failed FFI fact missing' }
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'generate-bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'   # and Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
# W1WorkspaceTests.cs declares no class W1WorkspaceTests: the fact lives in W1WorkspaceModelTests (:124)
$filter = 'FullyQualifiedName~SlateWindows.Tests.W1WorkspaceModelTests|FullyQualifiedName~SlateWindows.Tests.RecoveryAnnouncementTests|FullyQualifiedName~SlateWindows.Tests.W2EditorInteractionTests|FullyQualifiedName~SlateWindows.Tests.EmbedTitleRealizedSurfaceTests|FullyQualifiedName~SlateWindows.Tests.Censuses.VaultErrorDetailCensus|FullyQualifiedName~SlateWindows.Tests.Censuses.A11yCorpusCensus|FullyQualifiedName~SlateWindows.Tests.AccessibilityNotificationDispatcherTests|FullyQualifiedName~SlateWindows.Tests.Censuses.EmbedPreviewTitleCensus|FullyQualifiedName~SlateWindows.Tests.ReadingEmbedTests'
$raw = @(dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String '^\s{4}\S' | ForEach-Object { $_.Line.Trim() }); Assert-Ok 'test discovery'
$names = $raw | ForEach-Object { ($_ -split '\(', 2)[0] }
foreach ($must in 'SlateWindows.Tests.W1WorkspaceModelTests.ATabThatCannotLoadShowsCoresSentenceAndSpeaksNothing', 'SlateWindows.Tests.RecoveryAnnouncementTests.AnExplicitOpenOfAnUnreadableNoteSpeaksItsFailureOnce', 'SlateWindows.Tests.RecoveryAnnouncementTests.ReopeningAnUnreadableNoteSpeaksItsFailureOnce', 'SlateWindows.Tests.RecoveryAnnouncementTests.ReopeningUnreadableFileReportsFailureWithoutClaimingItIsMissing', 'SlateWindows.Tests.W2EditorInteractionTests.CtrlEOnABaseEmbedTitlesItAsABase', 'SlateWindows.Tests.Censuses.VaultErrorDetailCensus.ACaughtFailureIsAnnouncedInCoresWords', 'SlateWindows.Tests.EmbedTitleRealizedSurfaceTests.EveryResolvedKindReadsAsCoresTitleOnEveryRealizedSurface') {
  if ($names -notcontains $must) { throw "the filter does not select $must" }
}
# the census theory lists one row per site; the load row (A11yEvent.NoteOpenFailed under the new-event answer) and the reopen catch's row must be among them:
$loadEvent = 'A11yEvent.FileReopenFailed'   # 'A11yEvent.NoteOpenFailed' under the new-event answer
if (-not ($raw -like "*ACaughtFailureIsAnnouncedInCoresWords(file: `"WorkspaceViewModel.cs`", method: `"Load`", construct: `"$loadEvent`"*")) { throw 'VaultErrorDetailCensus has no Load row' }
if (-not ($raw -like '*ACaughtFailureIsAnnouncedInCoresWords(file: "WorkspaceViewModel.Layout.cs", method: "ReopenFileAnnouncement", construct: "A11yEvent.FileReopenFailed"*')) { throw 'VaultErrorDetailCensus has no ReopenFileAnnouncement row' }
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'dotnet test'
python -I scripts/a11y_trigger_ledger.py --write; Assert-Ok 'ledger --write'
python -I scripts/a11y_trigger_ledger.py --check; Assert-Ok 'ledger --check'
$cs = @(git diff --name-only origin/main -- '*.cs') + @(git ls-files --others --exclude-standard -- '*.cs')   # WorkspaceViewModel.cs, WorkspaceViewModel.Layout.cs, ResolvedEmbeds.cs, Reading/ReadingContentViewModel.cs, Censuses/VaultErrorDetailCensus.cs, W1WorkspaceTests.cs, RecoveryAnnouncementTests.cs, W2EditorInteractionTests.cs, EmbedTitleRealizedSurfaceTests.cs (+ Censuses/A11yCorpusCensus.cs under the new-event answer)
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs; Assert-Ok 'dotnet format'
# the Rust gates of plan §4 rule 3 (CONTRIBUTING.md:76-98; no make here):
cargo fmt --all -- --check; Assert-Ok 'cargo fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'cargo clippy'
cargo test --workspace; Assert-Ok 'cargo test'
cargo bench --workspace --no-run; Assert-Ok 'bench compile'
# after `git add` of any new file; plain `bash` in PowerShell here is WSL's, so name Git Bash:
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh; Assert-Ok 'license headers'
```

CI: `windows.yml`, `rust.yml` and `swift-tests.yml` under BOTH WD23
answers — the generated Swift binding gains `EmbedTargetKind` and
`embedTargetKind(targetPath:)` either way and only the mac lane compiles
it (plan §7: a binding change adds `swift-tests.yml`); under the new-event
answer the same lane also compiles the blind mirror entry. The PR waits for
all three green (§11). AT: NVDA
spot — Enter on a Files row whose note holds invalid UTF-8: one line,
"Could not reopen <name>: <detail>" (new-event answer: "Could not open
<name>: <detail>."), and nothing while arrowing onto the row; close that
tab and press the reopen-closed-tab chord: one line, "Could not reopen
<name>: <detail>" under both answers, never two; Ctrl+E on a `.base`
embed: "Embedded base: <name>". Record all three in the PR body.

## Acceptance

- [ ] WD23's recorded answer named in the PR body; the design branch followed matches it.
- [ ] `embed_target_kind` in core and both bindings; `cargo test -p slate-core`, the four Rust gates of plan §4 rule 3 and the license-header check green.
- [ ] Facts above pass, seen failing first; mutations run.
- [ ] `VaultErrorDetailCensus` rows added (`Load`, `ReopenFileAnnouncement`); `EmbedTitleRealizedSurfaceTests` base row true.
- [ ] `Load` speaks nothing; an explicit open speaks a failed tab's sentence once (`TryOpenItem`); a failed reopen speaks once, `FileReopenFailed`, under either answer — the two `RecoveryAnnouncementTests` journeys pass and each of their mutations kills them.
- [ ] Ledger regenerated (and, new-event answer only: corpus, three mirrors, artifact, mac designation).
- [ ] Windows, Rust and Swift (`swift-tests.yml`) lanes green under either WD23 answer — the Swift lane compiles the new `EmbedTargetKind` binding, and under the new-event answer also the blind mirror entry; format gate clean; every Verification command exits 0 and both discovery checks pass.
- [ ] NVDA spot (the open sentence, the single reopen sentence, "Embedded base: <name>") in the PR body.
- [ ] PR body: `Closes #1266`, `Closes #1288`; "not delivered: a base summary card in the popover body".

## Brief

Read `WorkspaceViewModel.cs:1128-1138` (the right pattern), `:1828-1849`
(`WorkspaceTabViewModel.Load`, the wrong one at `:1844-1848`), `:184` and
`:569` (its two callers), `:2632-2719` (`OpenPath`, the wikilink route,
`OpenPathCore`), `WorkspaceViewModel.Layout.cs:96-131` (`OpenItem`,
`TryOpenItem`), `:157-323` (the selection preview, `TryOpenItemCore`,
`ReplaceTabItem`), `:341-374` (`AddTab`), `:830-905` (`ReopenClosedTab`,
`ReopenFileAnnouncement`: `FileReopenFailed(filename, tab.LoadFailure)` at
`:902-904` and a `failure.Message` read at `:900`),
`RecoveryAnnouncementTests.cs:215-227` and `:364-394` (the existing
reopen fact and `Host`), `VaultErrorDetailCensus.cs:56-61`,
`ResolvedEmbeds.cs:25-34`, `EditorInteractions.cs:1499-1507, 1655-1689,
1741-1800`, `ReadingContentViewModel.cs:1071-1078`, core `a11y.rs:677-680,
1988-1990, 3287-3360`, `lib.rs:11387`, `session.rs:12588-12601`, and plan
§3 row WD23. Pending decision WD23 (recommended: reuse `FileReopenFailed`;
the other answer adds `A11yEvent::NoteOpenFailed { name, detail }` with
corpus rows after `a11y.rs:4344`, mirror entries after
`A11yCorpusCensus.cs:164` and `A11yCorpusCensusTests.swift:157`, FFI arms
at `lib.rs:9164`/`:11041`, a regenerated `tests/fixtures/a11y/corpus.json`,
a `recorded` mac designation in `a11y_trigger_designations.json`, and the
Rust facts `note_open_failed_renders_core_detail_with_a_period` and
`note_open_failed_crosses_the_ffi_as_core_renders_it`; under it `Load`
builds `NoteOpenFailed`, the explicit open speaks it, the reopen still
speaks `FileReopenFailed` once, and the reopened tab's status line reads
the load's "Could not open …" sentence, said in the PR body; the Swift
lane is required under BOTH answers, because the `embed_target_kind`
export below is new Swift binding code either way). Build — who speaks a
load failure (both answers): `Load` records it and never speaks, because
it also runs on the reopen (whose own announcement would then double),
the startup restore, the same-item reloads and the rescan's re-seat. In
`Load`'s catch: `string detail = SlateUniffiMethods.VaultErrorDetail(exception);
var failed = new A11yEvent.FileReopenFailed(System.IO.Path.GetFileName(Path),
detail); LoadFailure = detail; LoadFailureEvent = failed; Status =
SlateUniffiMethods.A11yRender(failed).Text;` with new `internal A11yEvent?
LoadFailureEvent { get; private set; }` cleared beside `LoadFailure` at
`Load`'s start. In `TryOpenItem`'s non-selection branch: `bool opened =
TryOpenItemCore(item, target, requestEditorFocus); if (opened &&
ActiveGroup.ActiveTab is { LoadFailureEvent: { } failed }) {
_announce(failed); } return opened;` (every `true` return leaves the
landing tab active; the selection branch stays quiet under R-2). The
reopen keeps its one announcement, now in core's words through
`tab.LoadFailure`, and its `CanonicalPath` catch builds
`FileReopenFailed(filename, SlateUniffiMethods.VaultErrorDetail(failure))`.
Add two rows to `VaultErrorDetailCensus.Sites()`: `{ "WorkspaceViewModel.cs",
"Load", "A11yEvent.FileReopenFailed", "Detail", 1, true }` (the
`A11yEvent.NoteOpenFailed` construct under the other answer) and `{
"WorkspaceViewModel.Layout.cs", "ReopenFileAnnouncement",
"A11yEvent.FileReopenFailed", "Detail", 1, false }`. In core add
`embed_target_kind(&str) -> EmbedTargetKind { Note, Base }` beside
`resolved_embed_title`, export it beside `lib.rs:11387`
(`#[uniffi::export] pub fn embed_target_kind(target_path: String) ->
EmbedTargetKind`, new), regenerate both bindings (the Swift one gains
`EmbedTargetKind` and `embedTargetKind(targetPath:)` with no mac consumer,
so no mac source changes); add the `FullNote`-is-a-base arm to
`ResolvedEmbeds.Of` guarded by `SlateUniffiMethods.EmbedTargetKind`; swap
the reading view's `EndsWith(".base")` for the core call. Facts first,
seen failing:
`a11y::tests::embed_target_kind_is_base_only_for_the_base_extension`
(`.base` any case → Base, everything else Note);
`embed_target_kind_crosses_the_ffi`;
`W1WorkspaceModelTests.ATabThatCannotLoadShowsCoresSentenceAndSpeaksNothing`
(in `W1WorkspaceTests.cs`, which has no class `W1WorkspaceTests`; a tab
built directly over `note0.md` holding `[0xff, 0xfe, 0xff]`: `LoadFailure`
equals `VaultErrorDetail(e)` for the `VaultException` `session.ReadText`
throws, `LoadFailureEvent` equals `FileReopenFailed("note0.md", detail)`,
`Status` is its rendering, and the tab's announcer heard nothing);
`RecoveryAnnouncementTests.AnExplicitOpenOfAnUnreadableNoteSpeaksItsFailureOnce`
(selection preview: no rendered line starting "Could not "; then
`OpenPath("note0.md")`: exactly one, High, equal to the rendering of
`tab.LoadFailureEvent`, no `@message=`; then a split-right open: exactly
one more);
`RecoveryAnnouncementTests.ReopeningAnUnreadableNoteSpeaksItsFailureOnce`
(close, corrupt, reopen: exactly one rendered line starting "Could not ",
typed `FileReopenFailed` under both answers, `Detail == tab.LoadFailure`,
High, no `ReopenedFile`/`ReopenTargetMissing`; the existing
`ReopeningUnreadableFileReportsFailureWithoutClaimingItIsMissing` stays
green); `VaultErrorDetailCensus.ACaughtFailureIsAnnouncedInCoresWords`
(the new `Load` and `ReopenFileAnnouncement` rows refuse a `.Message`
read; the reopen row fails on main);
`W2EditorInteractionTests.CtrlEOnABaseEmbedTitlesItAsABase` (title, UIA
name and `EmbedPreviewShown` all carry `ResolvedEmbed.Base`);
`EmbedTitleRealizedSurfaceTests` base row → `OnPopoverAndLeaf: true`.
Mutations: `exception.Message` back in `Load`; `failure.Message` back in
the reopen catch; `_announce(failed)` added to `Load` (the tab fact hears
it, the split open and the reopen each speak twice); the `TryOpenItem`
announcement dropped (the explicit opens speak nothing); the selection
branch speaking too; the `Note` arm back; case-sensitive suffix. Local
loop (every native command followed by a `$LASTEXITCODE` check that
throws): `cargo test -p slate-core embed_target_kind -- --list` and
`cargo test -p slate-uniffi embed_target_kind -- --list` must list the
two Rust facts; `cargo test -p slate-core a11y::`; `cargo test -p
slate-uniffi crosses_the_ffi`; bindings regenerated and the Release build;
`dotnet test --list-tests` over the filter `W1WorkspaceModelTests |
RecoveryAnnouncementTests | W2EditorInteractionTests |
EmbedTitleRealizedSurfaceTests | Censuses.VaultErrorDetailCensus |
Censuses.A11yCorpusCensus | AccessibilityNotificationDispatcherTests |
Censuses.EmbedPreviewTitleCensus | ReadingEmbedTests` (each
`FullyQualifiedName~SlateWindows.Tests.…`) must list the six named facts
(the tab fact, the two journeys, the existing reopen fact, the Ctrl+E
fact, the census theory), the embed-title theory and the census's `Load`
and `ReopenFileAnnouncement` rows, else throw; then the run, the ledger
`--write`/`--check`, the format gate over the touched `.cs` files
(`WorkspaceViewModel.cs`, `WorkspaceViewModel.Layout.cs`,
`ResolvedEmbeds.cs`, `Reading/ReadingContentViewModel.cs`,
`Censuses/VaultErrorDetailCensus.cs`, `W1WorkspaceTests.cs`,
`RecoveryAnnouncementTests.cs`, `W2EditorInteractionTests.cs`,
`EmbedTitleRealizedSurfaceTests.cs`, plus `Censuses/A11yCorpusCensus.cs`
under the other answer), the four Rust gates, and
`scripts/check-license-headers.sh` under Git Bash. Registers:
`python -I scripts/a11y_trigger_ledger.py --write` (the `FileReopenFailed`
row gains the `Load` construction; under the other answer a
`NoteOpenFailed` row appears). Not delivered: a base summary card in the
popover body. Done when `windows.yml`, `rust.yml` and `swift-tests.yml`
are all green under either WD23 answer (the Swift lane compiles the new
export; under the new-event answer it also compiles the blind mirror
entry), the four Rust gates, the license-header check and the format gate
are clean, the mutation run is recorded, and the NVDA spot (Enter on an
unreadable note's row: one sentence, none while arrowing; reopening it:
one "Could not reopen …" sentence; "Embedded base: <name>") is in the PR
body with `Closes #1266`, `Closes #1288`. Commit: `fix(core,windows): a
tab's load failure is spoken once in core's words, and a .base embed's
preview title is core's`.

## Discrepancies for the plan author

- `EmbedTitleRealizedSurfaceTests.cs:84-86, 107` documents the popover
  deliberately NOT showing a `.base` as a base ("reading view only;
  contract C10"); the issue and the plan reverse that. The row flips to
  `true` in this chunk.
- Plan §5's WB-8 lane cell says "rust, windows (+ swift mirror entry only
  under WD23's new-event answer)", and plan §2 (`:153`) and the WD23 row
  (`:257`) say reuse removes "the Swift lane": wrong. `embed_target_kind`
  is a new FFI export under both answers, so the Swift compile lane is
  required either way (the WB-26 row's "all three under either design
  answer, since the free-function alternative is a core export too" is
  the same case); only the mirror entry is conditional.
- Plan §5's WB-8 row says core's sentence "replaces `VaultException.Message`
  in the tab's load failure (census row added)" and §9's row says "a tab
  that cannot load speaks `NoteOpenFailed`". Round 7 found that speaking
  inside `Load` doubles a failed reopen (`ReopenClosedTab` builds the tab,
  then announces the reopen's own `FileReopenFailed`), so the spec now
  has `Load` record the failure and the opening command speak it once:
  `TryOpenItem` for an explicit open, `ReopenClosedTab` for a reopen. The
  reopen's own catch read `failure.Message` too, so the census gains two
  rows, not one. §9's `NoteOpenFailed` is the other WD23 answer, not the
  recommended one.
