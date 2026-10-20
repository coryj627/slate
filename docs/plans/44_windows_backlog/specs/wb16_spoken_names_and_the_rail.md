# WB-16 — Every announcement names the item as its list does: the five accepted R-4 risks fixed, the recent-vault distinguisher is the shortest differing ancestor, and the rail stops writing and reloading on every arrow

Issues: #1301 (AR-54 to AR-58) #1311 #1305. Size M. Lanes: windows. AT: NVDA spot (the recent-vault sentence; a saved-query, dashboard and shortcut announcement; a held-arrow sweep of the rail). Wave: W6, slot 1. Depends on: WB-6 and WB-12 merged (both edit `FilesSidebarViewModel*.cs`; §6 orders WB-6, then WB-12, then WB-16). Parallel with: WB-17, WB-18. Decision: none (OD-9's rule is the spec).

## Goal

Contract 40 OD-9's rule — every announcement that names a sibling item takes its name from the same composed-name authority as that item's UIA Name — holds for the five kinds codex found it broken for, each with a failing fact first and a mutation; two recent vaults called "Notes" are told apart by "in Documents" and "in Dropbox", not by two full paths; and arrowing through the right-pane rail makes at most one workspace write and reloads History and Queries once, when the arrows settle, while the focused row is still the shown leaf (S4) and AR-59's one utterance is unchanged.

## Current state on main

All paths under `apps/slate-windows/src/SlateWindows/` unless noted. Line anchors are current; the issue's are older where they differ.

- **AR-54.** `MainWindow.Citations.cs:483` `rowKey: static row => ((UnresolvedRowViewModel)row).Path` in `BindBibliographyUnresolved`; the grid census pins the delegate text at `tests/SlateWindows.Tests/Censuses/ItemContainerNameCensus.cs:870` (`"((UnresolvedRowViewModel)row).Key | ((UnresolvedRowViewModel)row).Path"`), `rowKey` read at `:924-927`.
- **AR-55.** `WorkspaceViewModel.Layout.cs:864-879` `ReopenClosedTab`'s tail: the event is built (`:866-870`, file tabs through `ReopenFileAnnouncement`) and `tab.InvalidatePath()` runs *after* it (`:874-878`). `ReopenFileAnnouncement` (`:884-905`) composes `group.SpokenNameOf(tab)` at `:888` *before* the `CanonicalPath` missing check at `:893`.
- **AR-56.** `Bases/BaseQueryBuilderViewModel.cs:741` `BasesSavedQueryCreated(trimmed)`, `:766-767` `BasesSavedQueryUpdated(Context.SavedQueryName ?? "")`; `WorkspaceViewModel.Bases.cs:1369` `BasesSavedQueryRenamed(trimmed)`. The authority is `SavedQuerySpokenName(id, name)` at `WorkspaceViewModel.Bases.cs:196`. `tests/…/Censuses/AnnouncementNameCensus.cs:33-41` `RawNames` lists `SavedQuerySummary.Name` and `SavedQuery.Name` only — a `string` argument is invisible to it; `Authorities` `:46-53`.
- **AR-57.** `WorkspaceViewModel.Bases.cs:889` `BasesDashboardUpdated(name)`, `:895` `BasesDashboardSaved(name)`; the list is `Dashboards` (`:1212`, refreshed `:1305-1310`), its UIA names composed in XAML (`MainWindow.xaml:2229`: `SiblingNames.NamePath="Name" Noun="dashboard"`, no distinguisher). `DashboardSummary` is absent from `RawNames`.
- **AR-58.** `FilesSidebarViewModel.cs:2052` `RemoveShortcut`; `:2060-2064` removes, then `ReportResult($"Removed {shortcut.DisplayName} from shortcuts.")`. `SidebarShortcutViewModel` (`:466-477`) has `DisplayName`, `KindLabel`, `AutomationName = "{DisplayName}, {KindLabel} shortcut"`; the list's composed names ("…, shortcut 2") come from XAML (`MainWindow.xaml:1102`: `NamePath="AutomationName" DistinguisherPath="Path" Noun="shortcut"`). `tests/SlateWindows.Tests/SharedNameTests.cs:202` `OneNoteInTwoShortcutSlotsIsTwoItems`.
- **#1311.** `RecentVaultsStore.cs:44-45` `RecentVault.SpokenName` = `SiblingNames.SpokenAmong([.. all], vault, other => other.DisplayName, other => other.Path, "vault")` — the distinguisher is the full path. `SiblingNames.cs:248` `SpokenAmong<T>(siblings, item, name, distinguisher, noun)`; `:187-230` `Compose(names, distinguishers, noun, states, prefix)`. The welcome buttons and `VaultOpened` (`VaultLifecycleViewModel.cs:519`, through `RecentVaultSpokenName` `:895-899`) share the authority (OD-9).
- **#1305.** The `ActiveLeaf` setter is `WorkspaceViewModel.cs:2498-2546` (the issue said ~1957): `LeafPanelShown` unless arrowing (`:2505-2508`), `RefreshBaseQueries()` for `queries` (`:2525-2528`), `History.Reload()` for `history` (`:2534-2537`), `Persist()` (`:2544`) → `WorkspaceViewModel.Persistence.cs:188` → `WorkspacePersistence.cs:210` `store.WriteAtomically` → `AnchoredVaultStore.cs:169` (`:186` `RandomAccess.FlushToDisk`). `Panels/HistoryViewModel.cs:456` `Reload()` is synchronous. `IsChoosingLeafByArrow` `:2496`.
- Contract 40 rows: AR-54..58 `docs/plans/40_nvda_matrix_remediation_contracts.md:102-106`; OD-9 `:116`; AR-59 and #1305's residue inside OD-11 `:117`; the round-9 record `:708`.

## Reference

Contract 40 OD-9 (`:116`), round 8's rule: "every announcement that names a sibling item takes its name from the SAME composed-name authority as that item's UIA Name, one per item kind … held by `AnnouncementNameCensus`". AR-59 (`:117`): "the active leaf itself stays synchronous"; S4: the focused rail row is the shown leaf. #1311's issue text is the spec for the distinguisher; mac's recents naming "should follow once the Windows form is settled" (no twin today).

## Design

- **AR-54.** `rowKey: static row => ((UnresolvedRowViewModel)row).Path + "\u001F" + ((UnresolvedRowViewModel)row).Key`; update the pin at `ItemContainerNameCensus.cs:870`.
- **AR-55.** In `ReopenClosedTab`, decide "missing" first: a `bool missing = IsMissing(tab)` helper carrying the `CanonicalPath` check and its `VaultException` arm; if missing, `tab.InvalidatePath()` *then* `ReopenFileAnnouncement` composes `SpokenNameOf(tab)` over the post-state strip; `:874-878` goes.
- **AR-56.** After the registry write and a `RefreshBaseQueries()` (or the prospective composition when the list is not yet refreshed), announce `SavedQuerySpokenName(id, trimmed)`; create returns the new id from `_session` (check `CreateSavedQuery`'s return; if it returns none, compose prospectively over `SavedQueries` plus the new name). `AnnouncementNameCensus` gains an *event-constructor* rule: the first argument of `new A11yEvent.BasesSavedQueryCreated/Updated/Renamed(...)`, `BasesDashboardSaved/Updated(...)` must be an authority call or a local assigned from one (the existing local/pattern-variable tracing, `:116` round-9 record).
- **AR-57.** **New** `internal string DashboardSpokenName(string id, string name)` beside `:196`, = `SiblingNames.SpokenAmong(Dashboards, item-by-id, d => d.Name, _ => null, "dashboard")` (bare when the list lacks it); announce after `Dashboards` refreshes. `RawNames` gains `("uniffi.slate_uniffi.DashboardSummary", "Name", "WorkspaceViewModel.DashboardSpokenName")`; `Authorities` gains the method.
- **AR-58.** **New** `internal string ShortcutSpokenName(SidebarShortcutViewModel shortcut)` = `SiblingNames.SpokenAmong(Shortcuts, shortcut, s => s.AutomationName, s => s.Path, "shortcut")` — the same inputs XAML `:1102` composes from; `RemoveShortcut` captures it before `Shortcuts.Remove` and reports it. `RawNames` gains `("SlateWindows.SidebarShortcutViewModel", "DisplayName", "FilesSidebarViewModel.ShortcutSpokenName")` and the same for `AutomationName`.
- **#1311.** **New** `SiblingNames.ShortestDistinguishingAncestors(IReadOnlyList<string?> paths) : string?[]`: for each path whose *name* collides (by `SpeechKey`, the caller's comparer), compare parent segments from the leaf upward across the colliding set; the first segment where the item differs from every other collider yields `"in {segment}"`; when one segment does not separate it, join the needed segments from that depth up (`"in Work\\Notes"`); fall back to the full path only when every segment matches (same path differing only in case or root). `RecentVault.SpokenName` computes the array once over `all` and passes `other => ancestors[indexOf(other)]`. `SpokenAmong`'s signature is unchanged. Grid-row and tab distinguishers (vault-relative paths) are reviewed and left as they are; the PR body records why (they are already the shortest unique form inside a vault).
- **#1305.** `Persist()` at `:2544` becomes `SchedulePersist()`: a `DispatcherTimer`-free coalescer on `PanelWorkScheduler`'s owner context at `DispatcherPriority.ApplicationIdle` (one pending write; `Dispose`/`PrepareForApplicationClose` flush synchronously). `RefreshBaseQueries()` and `History.Reload()` move into `OnLeafSettled(value)`, posted at `ApplicationIdle` and run only if `ActiveLeaf` is still `value`; the commit, `LeafPanelShown`, `TasksReview.EnsureLoaded`, `Bibliography.EnsureLoaded` and `OnLeafShown` stay synchronous (out of scope per the issue). Item 4 (the Connections B-D11 summary after the row name) is reviewed against AR-59 and recorded, not changed.
- Deliberately not changed: `SiblingNames.Compose`, `SpokenAmong`, XAML `NamePath`/`Noun` attributes, `History.Reload`'s body, the rail's selection-follows-focus.

## Tests

- `BibliographyTests.ASortThenARepublishKeepsTheReaderOnItsUnresolvedCitation` — two unresolved keys in one file; sort by key, republish; the reader's row is the same citation. Mutation: revert the key to `Path`.
- `WorkspaceLayoutTests.AMissingFileReopenNamesTheTabAsTheStripReadsItAfterMarking` — same-title tabs; `ReopenTargetMissing.Name` equals the strip's post-state name. Mutation: compose before `InvalidatePath`.
- `BasesQueriesTests.SavedQueryCreateUpdateAndRenameSpeakTheListsComposedName` (hosted): "draft" beside a new, updated, renamed "DRAFT."; each announcement equals the list's UIA name. Mutation: announce `trimmed`.
- `BasesQueriesTests.DashboardSaveAndUpdateSpeakTheListsComposedName` (hosted, read-alike "draft" / "DRAFT."). Mutation: announce `name`.
- `SharedNameTests.OneNoteInTwoShortcutSlotsIsTwoItems` extended: removing slot 2 reports "…, shortcut 2". Mutation: report `DisplayName`.
- `AnnouncementNameCensus.EveryAnnouncementNamesASiblingItemThroughItsAuthority` — now fails on `main` for the five sites (the first failing fact); the new constructor rule has a planted case.
- `RecentVaultNamingTests.TwoVaultsCalledNotesUnderDifferentParentsSpeakInParent` — "Notes, in Documents" / "Notes, in Dropbox"; `…SpeakTheFirstDifferingSegmentDeeperInTheTree`; `…TheWelcomeButtonsAndVaultOpenedMatch`; `…AUniqueNameStaysBare`; `SiblingNamesTests.ShortestDistinguishingAncestorsFallsBackToTheFullPathOnlyWhenNothingShorterSeparates`. Mutation: return the full path.
- `RightPaneRailTests.AHeldArrowSweepMakesAtMostOneWorkspaceWriteAndNoneInsideAKeyPress` (count `WorkspacePersistence.Save` through a store seam; assert zero during the key presses, one after idle). Mutation: restore `Persist()` at `:2544`.
- `RightPaneRailTests.PassingThroughHistoryKeepsAnOpenInlineDiffAndItsLoadedPages`; `…SettlingOnHistoryOrQueriesRefreshesThatLeafExactlyOnce`. Mutation: call `History.Reload()` synchronously in the setter.
- No FlaUI journey changes; the shell journeys' runtime item-name census (`EveryShellJourneyRunsTheItemNameCensus`) covers the new names.

## Docs and registers

- `docs/plans/40_nvda_matrix_remediation_contracts.md`: append "**Fixed in #1301's PR (plan 44 WB-16):** …" to AR-54..58 (`:102-106`), to AR-59's residue sentence (`:117`), and a note under OD-9 that the recent-vault distinguisher is the shortest differing ancestor (#1311); never rewrite the records.
- `AnnouncementNameCensus` table rows (above) are the register for the two new kinds.
- No `w_c_matrix.md`, `parity_matrix.md`, designations or ledger changes (no new `A11yEvent`); the mac follow-up for recents naming is a one-line comment on #1311, not a new issue (mac has no colliding-recents form yet).

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb16'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~AnnouncementNameCensus|FullyQualifiedName~ItemContainerNameCensus|FullyQualifiedName~BibliographyTests|FullyQualifiedName~WorkspaceLayoutTests|FullyQualifiedName~BasesQueriesTests|FullyQualifiedName~SharedNameTests|FullyQualifiedName~RecentVaultNamingTests|FullyQualifiedName~SiblingNamesTests|FullyQualifiedName~RightPaneRailTests"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <every touched .cs>
```

CI lanes: `windows.yml`. AT pass (NVDA spot, launched with `scripts/windows/launch-for-field-pass.ps1 -Seed` and a second seeded recent "Notes" under another parent): open the colliding vault — expect "Vault Notes, in Documents opened."; save a dashboard "DRAFT." beside "draft" — expect "Dashboard DRAFT., dashboard 2 saved." (the list's exact composed name, whatever `SiblingNames` yields); remove shortcut slot 2 — expect "Removed …, shortcut 2 from shortcuts."; hold Down through the rail — one utterance per row, no stutter; the PR stays a draft until the phrases are in its body.

## Acceptance

- [ ] Eleven facts above fail first (the census fails on `main` for the five sites) and each mutation kills its fact.
- [ ] `AnnouncementNameCensus` holds the two new kinds and the constructor rule.
- [ ] Contract 40 AR-54..59 appended; OD-9 note added.
- [ ] NVDA spot phrases recorded in the PR body.
- [ ] `Closes #1301`, `Closes #1311`, `Closes #1305`.
- [ ] Not delivered: #1305 item 4 if the review finds it inside AR-59's rule (say so with the fact that shows it).

## Brief

Read `MainWindow.Citations.cs:478-488`, `WorkspaceViewModel.Layout.cs:860-906`, `Bases/BaseQueryBuilderViewModel.cs:736-770`, `WorkspaceViewModel.Bases.cs:192-200`, `:885-898`, `:1212`, `:1305-1310`, `:1364-1374`, `FilesSidebarViewModel.cs:466-477`, `:2052-2066`, `RecentVaultsStore.cs:38-46`, `SiblingNames.cs:180-262`, `WorkspaceViewModel.cs:2496-2546`, `WorkspacePersistence.cs:205-215`, `AnnouncementNameCensus.cs:30-60`, `ItemContainerNameCensus.cs:860-930`, `MainWindow.xaml:1102`, `:2229`, and contract 40 `:102-117`. Make the census fail for the five sites first, then fix each as the Design says with its fact and mutation; add `ShortestDistinguishingAncestors` and the four recent-vault facts; coalesce the rail's persist and defer the two leaf loads to a settle; append the contract rows; run the NVDA spot. Done when every fact passes, the mutations kill, the phrases are recorded and the lane is green. Commit: `fix(windows): every sibling announcement speaks its list's composed name, a colliding recent vault is told apart by its nearest differing ancestor, and the rail writes and reloads once the arrows settle`.
