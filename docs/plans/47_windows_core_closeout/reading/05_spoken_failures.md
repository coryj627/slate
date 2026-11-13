# Reading 05 — what the Windows app says when something fails, stalls or is cancelled

Cluster: #1890 (sweep), #1830, #1791, #1800, #1661, #1660 (and #1395 as its twin).
Read at: worktree `<worktree>`, `main` at 1ec9bf31.
Method: every cited file opened in the worktree; `git log --grep` per issue; read-only `gh issue view` for #1527, #1697, #1395. No build and no test was run, so every statement about runtime behaviour is from reading the code. Anything I did not check in the code is marked **unverified**.

One fact the whole cluster rests on, checked in the generated binding (`C:\dev\slate\apps\slate-windows\src\SlateUniffi\generated\slate_uniffi.cs`; the worktree has no generated binding, so this is the main checkout's copy, 45 commits behind, generator output): every uniffi error class builds its `Message` as a field dump. `VaultException.Io` is `"@message=" + message` (line 41718-41726), `SavedButUnindexed` is `"@newContentHash=…, @detail=…"` (41966-41980), and the same holds for `CommandException`, `GraphConfigException` (`"@reason=…"`, `"@version=…"`) and `HotExitException`. So the class is "any uniffi exception's `Message`", not only `VaultException`'s. Core has a renderer for one of them only: `vault_error_detail` (`crates/slate-core/src/a11y.rs:3591`, exported at `crates/slate-uniffi/src/lib.rs:12214`).

No commit on `main` mentions #1890, #1830, #1791, #1800 or #1395. #1660 and #1661 are mentioned only by the merge that filed them (1de2725c, #1663, WB-3).

---

## #1890 — every failure that speaks a caught VaultError uses core's error detail

- **Verdict:** stands. Partly misdescribed in two directions:
  - Two files it names need no fix. `EditorInteractions.cs` already passes `VaultErrorDetail` in its `VaultException` catch (`:1575-1582`) and its other catch (`:1584-1592`) carries a non-vault exception's own message by design; `VaultErrorDetailCensus` pins both (`Sites()` row `ResolveEmbedPreview`). `FindInNoteViewModel.cs:706` catches AvalonEdit's `SearchPatternException`, whose message is ordinary text.
  - It under-counts. It misses about 20 sites in files it does not name (the restore dialogs, the property-edit announcements, the right-pane panels, the reading view's base embed, the template flow, sync diagnostics, the canvas funnel), and it misses the same defect on a non-vault uniffi exception (`GraphConfigException`, `Graph/GraphConfigStore.cs:171-172`).
- **Current state:** 111 reads of a member named `Message` in `apps/slate-windows/src` (generated binding excluded). Sorted by reading each one:
  - 68 sites put, or can put, a binding field dump into text a person hears or reads (50 in a `catch (VaultException …)` or on a `VaultException`-typed value; 17 in a broad catch or an `Exception`-typed parameter that a vault error reaches; 1 on `GraphConfigException`). Full table in the cluster section.
  - 19 read a non-uniffi exception's ordinary message (I/O, argument, AvalonEdit). Correct as text; they stay.
  - 20 are model properties that happen to be named `Message` (records, core structs). Not exceptions.
  - 3 are clean: the helper itself (`Bases/BaseDocumentViewModel.cs:1827`), an `out` value both callers discard (`Panels/TaskIndexRepairCoordinator.cs:190`), and a value replaced by `VaultErrorDetail` before it is spoken (`Panels/TasksReviewViewModel.cs:737`, see `:779-782`).
  - A grep for an exception rendered by `ToString()`, interpolation or concatenation found none. That grep goes by variable name and is not exhaustive: **unverified** until the bound census runs.
  - One test pins the defect: `SyncDiagnosticsPanelTests.cs:239` asserts `panel.LoadError == thrown.Message` for a `VaultException.Io`, which is `"@message=probe exploded"`. The other tests pin only prefixes (`FileManagementTests.cs:70` "Rename failed:", `SidebarTagFilterTests.cs:472`, `RescanTests.cs:1301`, `:1776`).
- **Cause:** the sites predate `vault_error_detail` (W7-7) or were written beside it without it; #1891 fixed and censused only `Bases/` and `WorkspaceViewModel.Bases.cs`.
- **Fix shape:**
  1. One shell-wide helper, `FailureDetail.Of(Exception)` (new file, e.g. `apps/slate-windows/src/SlateWindows/FailureDetail.cs`): `VaultException` → `SlateUniffiMethods.VaultErrorDetail`, anything else → its own `Message`. `BasePhrase.ErrorDetail` (`Bases/BaseDocumentViewModel.cs:1826-1827`) forwards to it or is replaced by it.
  2. Each of the 68 sites reads the helper instead of `Message`. The 19 non-vault sites and the 3 clean ones route through it too, so the census has one exempt reader.
  3. `GraphConfigStore.Reason` reads the typed fields (`Unparseable.reason`, `NewerVersion.version`) instead of `Message`. No FFI change. (An FFI `graph_config_error_detail` would be the core-owned alternative; flagged below.)
  4. `GraphDocumentViewModel.HumanReadable` (`:1509-1516`) and `SearchOverlayViewModel.HumanReadable` (`:909-916`): replace the `_ => failure.Message` fallback with the helper. Keeping or dropping their four hand-written arms is an owner decision (below).
  5. The census (design in the cluster section).
- **Failing test first:**
  - `Censuses/ShellFailureDetailCensus.cs`, `NoShellFailureBecomesTextOutsideCoresDetail`: the #1697 rules over the whole shell compilation. Fails today naming every site.
  - One hosted fact per family, as the issue asks, each driving a real refusal and asserting the sentence has core's words and no `@`:
    - Files sidebar: `FileManagementTests`, `ARefusedRenameSpeaksCoresDetail` (rename onto an existing name; expects "Rename failed: Something named b.md already exists there.").
    - Canvas: `CanvasDocumentTests`, `AFailedCanvasActionSpeaksCoresDetail` (a `CanvasActionFailed` whose detail is core's).
    - Panels: `SyncDiagnosticsPanelTests.AFailedConfigReadFailsThePairAndKeepsTheRenderedReport` changes its `:239` assert to `"probe exploded"`; `HistoryPanelTests`, `AFailedVersionLoadShowsCoresDetail`.
    - Dialog: `WorkspaceViewModel.History` restore, `ARefusedRestoreAlertsWithCoresDetail`.
- **Size:** L as one piece (about 27 source files). Three PRs of S to M each (cut below).
- **Lanes:** windows.
- **Screen-reader work:** NVDA spot, one failure per family (files sidebar, canvas, a panel, the restore dialog).
- **Changes text or speech a Mac user would hear:** no. `vault_error_detail` is not changed, and nothing in `apps/slate-mac` calls `vaultErrorDetail` today (grep: no hits; Mac speaks its own `humanReadableVaultError`, #1527 is open).
- **Owner decision needed:**
  1. *Search and graph error wording.* The two `HumanReadable` copies word `InvalidQuery` as "Search query is invalid: {message}"; core's `vault_error_detail` returns the bare message. Moving them onto core changes what Windows says ("Search error: Search query is invalid: X" becomes "Search error: X"). Recommended: keep the four arms in this plan, fix only the fallback, and record the divergence on #1527, because Mac's instance `humanReadable` (`AppState.swift:25735-25765`) words these arms differently from core too.
  2. *Scope of the census's second rule* (see census design). Recommended: rule 1 over the whole shell now; rule 2 over the whole shell only after a trial run shows its hit count.
- **Shares files with:** #1830 (`FilesSidebarViewModel.Import.cs` is not in the sweep, but the sweep edits `FilesSidebarViewModel.cs`, which holds `ReportFailure` and the command wiring #1830 may touch); #1791 (`FilesSidebarViewModel.TreeOperations.cs:309`, `:456`); #1660 (`WorkspaceViewModel.cs`, `Panels/TasksReviewViewModel.cs`); #1661 (`EditorInteractions.cs:1592`, only if that read is routed through the helper).

---

## #1830 — cancelling an import says nothing, and no surface shows one running

- **Verdict:** stands. Line numbers drifted by one.
- **Current state** (`apps/slate-windows/src/SlateWindows/FilesSidebarViewModel.Import.cs`):
  - `ImportStatus` at `:41` ("Import in progress" / "Import is idle"). Bound in no XAML and read by no code (grep over `src` and `tests`).
  - `CancelImport` at `:43-74`: cancels and disposes the token, announces nothing.
  - `ImportAsync`'s `catch (OperationCanceledException) { }` at `:151-153`.
  - A completed import reports at `:146-148` (status line plus `HostComposed`, Medium).
  - Not in the issue: the sentence for a cancelled import already exists and cannot be reached. `ImportSources` catches the cancellation and returns `ImportResult(…, Cancelled: true, …)` (`:312-319`), and `ImportSummary` appends " Import cancelled; completed copies remain." (`:522-525`). But `ImportAsync` then runs `cancellationToken.ThrowIfCancellationRequested()` at `:140`, before it reads the result, so the summary is thrown away and `Refresh()` at `:149` is skipped too.
  - `IsImporting` goes true at `:97`, before the file picker opens (`:103`), so "running" includes the picker. `tests/SlateWindows.Tests/PendingImport.cs:6-12` documents that a cancelled import still reports running until the picker returns.
  - The status bar shows a running trash at `MainWindow.xaml:944-949` (`FileSidebar.ShowTrashProgress`, `TrashStatus`, a Cancel button). The claim stands.
  - `StatusBarAndPaneTogglesTests.cs:591` records the gap by name: `["CancelImportCommand"] = "no surface shows an import, pane or not: #1830"`.
  - Routes to cancel: File menu (`MainWindow.xaml:328-329`), the sidebar's button (`MainWindow.xaml:1372`), the palette (`SlateCommandRegistrar.cs:678`), Escape (`MainWindow.xaml.cs:1444-1454`), and shutdown (`FilesSidebarViewModel.SessionWork.cs:55`, `VaultLifecycleViewModel.cs:2145-2147`).
  - Core has no import event (no "import" in `a11y.rs`). Mac composes its own summary in the host with the same clause (`AppState.swift:17465-17476`) and has a `SidebarImportProgressStrip` (**unverified** beyond the grep).
- **Cause:** the rethrow at `:140` discards the worker's cancelled result; the outer catch is empty; `ImportStatus` was never bound.
- **Fix shape:**
  - `ImportAsync`: when the worker returned a result, report it whether or not the token is cancelled (the existing summary, with its "Import cancelled; completed copies remain." clause), and refresh the tree. In the outer cancel catch (cancelled before any work), say a plain cancelled sentence. Say nothing when `SessionShutdownStarted`.
  - A status-bar item beside the trash one, shown while a worker is running (not while the picker is open), with the status text and a Cancel button. `ImportStatus` is bound there or deleted.
  - Files: `FilesSidebarViewModel.Import.cs`, `MainWindow.xaml` (status bar, about `:944`), `MainWindow.xaml.cs` if the Cancel button needs the trash item's focus handlers, `StatusBarAndPaneTogglesTests.cs:591`.
- **Failing test first:** `ImportEscapePrecedenceTests` (or `W1HardeningTests`, which has the import facts at `:879-913`): `CancellingARunningImportSaysSoAndKeepsWhatWasCopied` (cancel mid-worker; expects one announcement carrying the cancelled clause and the copied count, and a tree refresh). `CommandPaletteTests`: `CancelImportFromThePaletteSpeaksTheCancellation`. `StatusBarAndPaneTogglesTests`: the `CancelImportCommand` row changes from the #1830 note to "the status bar's progress", with a fact that the item is visible while a worker runs.
- **Size:** S to M (M with the status-bar item and its focus rules).
- **Lanes:** windows.
- **Screen-reader work:** NVDA spot (the cancel sentence; the status-bar item's name and its Cancel button).
- **Changes text or speech a Mac user would hear:** no, if the sentence stays host-composed as the completion sentence is today. Yes (an FFI addition, with Mac adoption) if the owner wants import events in core.
- **Owner decision needed:** the issue says "through core". Core has no import vocabulary, and both hosts compose the summary themselves. Question: add core import events now (FFI addition, Mac adoption, VoiceOver pass owed), or speak the existing host sentence and record the move on the ledger? Recommended: the existing host sentence now, one ledger line "import summary and cancel sentences move to core, both hosts". Second, smaller question: when the cancel lands during the picker, speak at once or when the picker closes? Recommended: at once, since nothing was imported and the picker may stay open.
- **Shares files with:** #1791 (same partial class, `Status`); #1890 (`FilesSidebarViewModel.cs`); #1800 (`MainWindow.xaml`, a different region).

---

## #1791 — the Files status line stays on "Loading files…"

- **Verdict:** stands, and it is wider than described. Not only creates: every file-change event ends in a plain refresh.
- **Current state:**
  - `Refresh` writes `Status = "Loading files…"` at `FilesSidebarViewModel.TreeOperations.cs:342`, on the asynchronous path only (the synchronous path at `:274-333` never writes it).
  - `ApplyTreeRefresh` replaces it in three arms only, `:701-713` (truncated root; `reportCount` or a settings notice; a restored overflow path), then calls `ReassertStatusAfterPublication()` at `:727`.
  - `Refresh(bool reportCount = false)` at `:214`.
  - `ReassertStatusAfterPublication` is at `FilesSidebarViewModel.FileManagement.cs:139-153`; it restores `_statusToReassert` once, then clears it.
  - The issue's callers "`FilesSidebarViewModel.cs:709` and `:733`" are now `:738` (`SortMode` setter) and `:762` (`GroupByDate` setter).
  - The cause the issue did not see: `VaultLifecycleViewModel.HandleFileChange` (`:1153`) queues a debounced plain `FileSidebar?.Refresh()` 150 ms after every change event (`:1187-1200`), Slate's own writes included. So New Canvas runs like this: `CreateCanvas` refreshes and holds its sentence (`FilesSidebarViewModel.cs:1926-1931`); the first publication restores it and clears the hold; the change feed's refresh then writes "Loading files…" and its publication has no arm to replace it. The order of the two publications is **unverified** (not run), but either order ends stuck once any later plain refresh publishes.
  - Other plain-refresh callers: `VaultLifecycleViewModel.cs:1172`, `:1860`, `:2307`; 22 more inside the sidebar.
  - A refresh cancelled with no successor (`CancelTreeRefresh`, `TreeOperations.cs:733`; a refused admission, `:407-410`) also leaves the line.
  - `SidebarTagFilterTests.cs:339` pins the count for `reportCount: true` (now at `:335-339`); nothing covers the plain path. Confirmed.
  - `Status` is not announced here; it is a `TextBlock` in the sidebar (`MainWindow.xaml:1401-1405`).
- **Cause:** a transient status is written at the start of a refresh and has no terminal write on the common path.
- **Fix shape:** in `FilesSidebarViewModel.TreeOperations.cs`: when `Refresh` writes the loading text, remember what the line said (unless it already said the loading text); in `ApplyTreeRefresh`, a final `else` restores it when the line still shows the loading text; the cancel and refused-admission paths restore it too. Alternative: write "Loading files…" only when the tree is empty. One file plus its test.
- **Failing test first:** `SidebarTagFilterTests` (beside `:339`) or `FileManagementTests`: `APlainRefreshPutsBackTheStatusItReplaced` (set a status through a mutation, let it publish, call `Refresh()`, publish; expects the earlier status, never "Loading files…"). A second fact for a sort change.
- **Size:** S.
- **Lanes:** windows.
- **Screen-reader work:** none (the line is not announced; a reader who navigates to it reads a truer line).
- **Changes text or speech a Mac user would hear:** no.
- **Owner decision needed:** what the line says after a plain refresh: what it said before, or the count. Recommended: what it said before. The line is where a mutation's failure detail stays inspectable (`FileManagement.cs:158-162`), and a count would erase that on every file change.
- **Shares files with:** #1890 (`TreeOperations.cs:309`, `:456`), #1830 (same class).

---

## #1800 — a right-pane panel with no surface says what it is for

- **Verdict:** stands.
- **Current state:**
  - The placeholder is `RightPaneDockedPlaceholder`, `MainWindow.xaml:2745-2805`. Its text, `:2802`: "This panel is docked and ready for its feature surface." Above it, a `PanelToolbarView` titled with the leaf's title (`:2800`).
  - It collapses for 14 of the 17 leaves in `WorkspaceViewModel.Leaves` (`WorkspaceViewModel.cs:2886-2907`). The three that reach it: `math` ("Math"), `code` ("Code"), `diagrams` ("Diagrams").
  - Those three are `pending` in the parity matrix's leaf inventory (`docs/plans/18_windows_port/parity_matrix.md:238-240`, against #729, #731, #730). Mac has bodies for all three (`ContentBlockPanels.swift:73`, `:134`, `:202`), with empty states such as "Select a note to see its math." and "This note has no math blocks." (`:87`, `:92`).
  - Two censuses pin the sentence by its text: `Censuses/RightPaneLeafBodyCensus.cs:25-26` and `Censuses/ConnectionsLeafCensus.cs:763`. Both must change with the copy.
  - Whether NVDA reads the placeholder without object navigation is **unverified**. It is a plain `TextBlock`, so it is in the UIA tree.
- **Cause:** a development placeholder that outlived 14 leaves; the three leaves left are features nobody has built on Windows.
- **Fix shape:** replace the line in `MainWindow.xaml:2802` (one sentence, or one per leaf through a trigger or a bound property), and update the two censuses. If the owner chooses to hide the leaves instead: `WorkspaceViewModel.Leaves`, the strip and More-panels counts (DS-16 fixes "the other twelve", `WorkspaceViewModel.cs:2921`), any persisted `ActiveLeaf` id, and the parity matrix all move; that is M and reader-facing.
- **Failing test first:** `RightPaneLeafBodyCensus`: a new fact `ALeafWithNoBodySaysWhatItWillShow` (for each leaf with no body, the pane's text names the leaf's subject and does not contain "feature surface"); the `PlaceholderSentence` constant changes in the same commit.
- **Size:** S once the copy is chosen.
- **Lanes:** windows.
- **Screen-reader work:** NVDA spot (open Math from More panels; read the pane).
- **Changes text or speech a Mac user would hear:** no.
- **Owner decision needed:** the copy, and who owns it.
  - Candidate A, one line per panel, saying where the content is today: "Math for this note isn't listed here yet. Open the note in the reading view to read its math." (and "Code blocks…", "Diagrams…").
  - Candidate B, one short line for all three: "This panel isn't available on Windows yet."
  - Candidate C, no copy: do not offer Math, Code and Diagrams until they have bodies.
  - Recommended: A. It says what the panel is for and what to do next, which is what the issue asks, and it costs no layout change.
  - Ownership: `AGENTS.md`'s rule, read strictly, puts it in core, because a screen reader can reach it. That would be an FFI addition for a sentence only Windows shows and that disappears when the leaves are built. Every other right-pane empty state on Windows is a host string copied from Mac (`RightPanePanelsViewModel.cs:296-300`, `TasksReviewViewModel.cs:188-192`), under the "static UI copy is a label concern" note at `Bases/BaseDocumentViewModel.cs:1779-1781`. Recommended: host-owned, beside those, with the exception written down.
- **Shares files with:** #1830 (`MainWindow.xaml`, different region).

---

## #1661 — Activate at Cursor acts on the hidden caret in reading mode; a Ctrl+click can speak while the cache loads

- **Verdict:** stands, both halves. Line numbers drifted. One sibling found.
- **Current state** (`apps/slate-windows/src/SlateWindows/EditorInteractions.cs`):
  - `ActivateAtCaretCommand` is built at `:661-663`; its CanExecute is `_ => _tab.IsMarkdown` (the issue said about `:619`).
  - Sibling: `PreviewEmbedCommand`, `:664-666`, has the same CanExecute and the same hidden-caret behaviour in reading mode.
  - Both are bound in the Editor menu (`MainWindow.xaml:545-552`) and resolved for the palette (`SlateCommandRegistrar.cs:505-510`).
  - The tab already knows: `IsReadingMode` and `IsEditorVisible` at `WorkspaceViewModel.cs:427-428`.
  - `RelayCommand` raises `CanExecuteChanged` only when asked (`VaultLifecycleViewModel.cs:2646-2661`), and nothing asks for these two commands. So a new predicate alone would leave the menu item stale after a mode switch. The mode changes at `ToggleViewMode` (`WorkspaceViewModel.cs:456-490`) and on in-place navigation (`:775-796`).
  - Mac: `activateAtCursor` announces nothing-to-activate when no editor is mounted (`AppState+EditorActivation.swift:16-22`). Confirmed.
  - `TryToggleTaskAt` is at `:2018-2054`; `EnsureArtifactCacheReady(announceWhenUnavailable: true)` at `:2031`, whatever the origin (the issue said about `:1989`). The sentence, `:2888-2890`: "Editor interactions are still loading; try again." (a `HostComposed`). The math check above it already passes `origin is Keyboard` (`:874-878`).
  - "Every other pointer refusal stays silent" is slightly too strong: a pointer on a real link or task in a dirty note does speak (`:851-872`, `:2026-2030`, `:2050`). On plain prose the only speaker is `:2031`. The fix is the same.
  - `W2EditorInteractionTests.ACtrlClickOnPlainProseStaysSilent` (`:1641-1664`) refreshes the cache first (`:1652`), so it never exercises the stale-cache case.
- **Cause:** the commands were gated on the document kind, not on the surface shown; `TryToggleTaskAt` did not carry the origin into the cache check.
- **Fix shape:** in `EditorInteractions.cs`: both commands' CanExecute becomes `_tab.IsEditorVisible`; their Execute refuses when the editor is hidden (a guard, in case a caller skips CanExecute); `:2031` passes `origin is EditorInteractionOrigin.Keyboard`. In `WorkspaceViewModel.cs`: the two mode-change sites raise the commands' `CanExecuteChanged` (or the coordinator listens for `IsEditorVisible`). The palette then speaks core's `PaletteCommandUnavailable` through `DisabledReason` (`SlateCommandRegistrar.cs:319-343`).
- **Failing test first:** `W2EditorInteractionTests`: `ActivateAtCursorAndPreviewEmbedAreUnavailableInReadingMode` (after `ToggleViewMode`: both `CanExecute` false, `CanExecuteChanged` raised, `SlateCommandRegistrar.DisabledReason` non-null, executing announces nothing and toggles nothing); `ACtrlClickOnPlainProseStaysSilentWhileTheCacheLoads` (no cache refresh; pointer origin: no announcement; keyboard origin: the loading sentence still speaks).
- **Size:** S.
- **Lanes:** windows.
- **Screen-reader work:** NVDA spot (the Editor menu in reading mode; the palette row's refusal).
- **Changes text or speech a Mac user would hear:** no.
- **Owner decision needed:** disabled, or enabled and refusing with "Nothing to activate at cursor." as Mac does. Recommended: disabled. A dimmed menu item and the palette's own unavailable sentence tell the reader why before they act; Mac's refusal exists because its command has no enabled state to lower. In reading mode Ctrl+Enter itself is unaffected (the reading surface handles it, `Reading/ReadingNavigator.cs:139`).
- **Shares files with:** #1890 (`EditorInteractions.cs`), #1660 and #1890 (`WorkspaceViewModel.cs`).

---

## #1660 — a task toggle that landed but was not indexed is announced as a failure, in a run-on sentence

- **Verdict:** stands, both problems. Its second problem is half of #1395 (the save template); #1395's other half (the create template) is the same defect in the next match arm. One core change settles the punctuation for both issues. The contradiction (#1660's first problem) is separate and needs more than punctuation.
- **Current state:**
  - Core, `crates/slate-core/src/a11y.rs`:
    - `TaskToggleFailed { detail }` declared at `:887-889`; High priority (`:1956`).
    - Rendered at `:2216-2219`: blank detail → `"Task could not be toggled."`; otherwise `lead_then_sentence("Task could not be toggled:", detail)`.
    - `lead_then_sentence` at `:3399-3408`: adds a period only when the text does not end in `.`, `?`, `!` or `…`.
    - `vault_error_detail` at `:3591`; the arm at `:3608-3610` (the issue said about `:3430`): `format!("Saved, but not indexed yet: {detail} It will appear after the next scan.")`.
    - The create twin, `:2243-2252`: blank reason → `"{name} was written but not indexed. It will appear in listings after the next scan; do not recreate it."`; otherwise `"{name} was written but not indexed: {reason} It will appear in listings after the next scan; do not recreate it."`.
    - The detail's sources (`crates/slate-core/src/session.rs:4986-5043`): the test fault's literal, or `e.to_string()` of a stat or rusqlite error. None ends in a period.
  - What Windows says today: "Task could not be toggled: Saved, but not indexed yet: test fault: injected failure after write, before index commit It will appear after the next scan." As the issue quotes.
  - Windows sites, all in `apps/slate-windows/src/SlateWindows`:
    - `WorkspaceTabViewModel.PublishTaskToggleThroughTab`, `WorkspaceViewModel.cs:1907`; the announcement at `:1927-1929`.
    - `WorkspaceViewModel.TaskToggleCompletion`, `:4084`; the announcement at `:4148-4149`.
    - `TasksReviewViewModel.ToggleTask`, `Panels/TasksReviewViewModel.cs:680`; the announcement at `:779-782`.
    - The second sentence the issue mentions: the completion sees the disk hash moved, repairs the index (`:4126`), and calls `ReconcileTabsAfterDirectTaskWrite` (`:4129`), which reaches `ReconcileAfterExternalTaskWrite` (`:2034-2055`) and speaks "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing." (`:2053-2054`, a `HostComposed`, High).
    - The canvas already treats the same error as a commit (`Canvas/CanvasMutationFunnel.cs:303`, `:465`).
  - Mac: `toggleCurrentTask` posts `.taskToggleFailed(detail: humanReadable(error))` from its generic failure arm (`AppState.swift:14242-14245`), only on the editor's activate-at-cursor route (`announcesOutcome`, `:14116-14121`). The detail is Mac's own copy, `AppState.swift:25761-25765`: "This change was saved but not indexed yet: \(detail) It will appear in listings after the next scan; do not recreate it." So Mac has the same contradiction and its own run-on, in different words. Mac's second copy of the save sentence is at `AppState.swift:13872-13875`.
  - The two Windows facts the issue names pin only the prefix "Task could not be toggled: Saved, but not indexed yet: ", a closing period, and no "@message=" (`TasksPanelTests.cs:614-617`, `TasksReviewTests.cs:681-684`). The closing period they check is the one after "scan", so they cannot see the run-on. Confirmed.
- **Cause:** (1) core has no way to say "the toggle landed, the index is behind", so hosts send the error through the failure event; (2) two templates splice a carried reason and start a new sentence without closing the first.
- **Fix shape:**
  - Core, `a11y.rs`:
    - `vault_error_detail(SavedButUnindexed)`: blank detail → "Saved, but not indexed yet. It will appear after the next scan."; otherwise `lead_then_sentence("Saved, but not indexed yet:", detail)` plus " It will appear after the next scan." Example: "Saved, but not indexed yet: database is locked. It will appear after the next scan."
    - If #1395 is folded in: `CreatePublishedUnindexed`'s non-blank arm the same way. Example: "Untitled.md was written but not indexed: database error: disk I/O error. It will appear in listings after the next scan; do not recreate it."
    - A new event for the landed toggle, `TaskToggledUnindexed { completed, detail }`. Proposed sentence, built from the two sentences core already has: "Task completed. Saved, but not indexed yet: database is locked. It will appear after the next scan." ("Task reopened. …" for the other state; blank detail drops the colon.) Priority: Medium, as `CreatePublishedUnindexed` is; `TaskToggled`'s own priority is **unverified**.
  - FFI, `crates/slate-uniffi/src/lib.rs`: the new variant in the FFI enum (near `:9958-9963`) and its mapping (near `:11892-11893`). `A11yEvent` has 238 variants against uniffi's cap of 256 (`a11y.rs:8848-8868`); the count pin moves to 239.
  - Windows: at the three announcement sites, `VaultException.SavedButUnindexed` speaks the new event; everything else keeps `TaskToggleFailed`. The repair and read-back logic stays. Whether the tab also splices its buffer on this arm (so the "no longer matches" sentence does not follow) is a second step: `PerformTaskToggle` computes `updatedText` before the write (`:1764-1767`) and the error carries the new hash, so it is possible, but that code has 19 recorded adversarial rounds; I would spec it separately or leave it.
  - Mac (compile only): one line in `A11yCorpusCensusTests.swift` (the hand list near `:150-161`), the blind mirror edit plan 44 §4 rule 6 names. Posting the new event from `toggleCurrentTask` is Mac adoption, for the ledger.
- **Failing test first:**
  - Rust, `a11y.rs` tests: `a_landed_unindexed_toggle_says_it_landed` (the new event's render and priority); `an_unindexed_save_detail_closes_its_reason` (the `vault_error_detail` arm with "database is locked" and with a blank detail). If #1395 is folded: `the_unindexed_create_caveat_is_the_windows_sentence_byte_for_byte` (`:6866-6887`) and the last assert of `an_empty_create_reason_leaves_no_dangling_separator` (`:6913-6921`) pin the run-on bytes today and are rewritten first.
  - C#: `TasksPanelTests.ATabToggleSpeaksCoresEvents` and `TasksReviewTests.AReviewToggleSpeaksCoresEvents` change their fault-seam half to expect the new event, its exact rendering with the seam's detail closed by a period, and no `TaskToggleFailed`. A third fact for the tab-less route (`TaskToggleCompletion`).
  - Corpus: `tests/fixtures/a11y/corpus.json` gains rows for the new event; the three existing rows (`:505`, `:510`, `:550`) do not change, because their witnesses end in a period. Add one unpunctuated witness per template, or the corpus still cannot show the run-on (#1395's own observation).
- **Size:** M.
- **Lanes:** rust, windows, swift (compile only).
- **Screen-reader work:** NVDA spot through `SLATE_TEST_FAULT_AFTER_WRITE` on all three routes (editor Ctrl+Enter, Tasks panel, Tasks Review).
- **Changes text or speech a Mac user would hear:** yes, in two of three parts.
  - The new event: an FFI addition. Mac hears nothing new until it adopts it; until then it keeps saying "Task could not be toggled: This change was saved but not indexed yet: …". Ledger: adoption plus VoiceOver pass.
  - `vault_error_detail(SavedButUnindexed)`: no Mac consumer today (Mac has its own two copies). It becomes Mac speech when #1527 lands. Ledger: a note on #1527 that Mac's copies at `AppState.swift:13875` and `:25765` carry the same run-on.
  - `CreatePublishedUnindexed` (only if #1395 is folded): yes, today. Mac posts it (`AppState+CreateReporting.swift:51`). A reason with no closing period gains one. Ledger: VoiceOver pass.
  - Tests that pin these strings. Rust: `a11y.rs:4544-4551` and `:4573-4576` (corpus list), `:6760-6764` (detail witness, unchanged), `:6866-6921` (the two create facts), `:6976` (`carried_text_closes_its_sentence_exactly_once`), `:8848-8868` (variant count); `crates/slate-uniffi/src/lib.rs:15457`, `:15505`. C#: `TasksPanelTests.cs:529`, `:615`; `TasksReviewTests.cs:618`, `:662`, `:682`; `CreateOutcomeTests.cs:57`, `:77`, `:91`; `FileManagementTests.cs:1790`; `Censuses/A11yCorpusCensus.cs:157-160`. Swift: `A11yCorpusCensusTests.swift:150-153`, `:161`, `:721-748`; `AfterWriteFault.swift:27`; `EditorActivationTests.swift:277-313` (renders through core, no literal). `docs/plans/38_notification_dispatcher_contracts.md` names `TaskToggleFailed`; whether a trigger-ledger census needs a row for the new event is **unverified**.
- **Owner decision needed:**
  1. *Fold #1395 in?* #1395 is in group C (audit only) and plan 43 lists it as PR 39 (`docs/plans/44_windows_backlog_plan.md:51`). #1660 must edit the save template anyway; the create template is the adjacent arm, the same helper and the same test. Recommended: yes, one PR closes #1660 and #1395, with two ledger lines. Leaving the create half behind means two PRs rewriting the same test.
  2. *The landed toggle's words.* The proposed sentence above, or a variant. Recommended as proposed: it reuses the two sentences already pinned.
  3. *The caveat after an immediate repair.* Windows repairs the index at once on this error (`WorkspaceViewModel.cs:4126`, `TasksReviewViewModel.cs:797`), so "It will appear after the next scan" is often already out of date when spoken. Options: always speak the new event; or speak plain `TaskToggled` when the repair succeeded and the new event only when it did not (needs the announcement moved after the repair on two routes). Recommended: always the new event in this plan (small and never silent about a real fault); the refinement only if the owner wants it.
- **Shares files with:** #1890 (`WorkspaceViewModel.cs`, `Panels/TasksReviewViewModel.cs`); #1661 (`WorkspaceViewModel.cs`); any other core chunk that adds an `A11yEvent` (the count pin at `a11y.rs:8867`, `corpus.json`, the three corpus censuses) — #1628, #1629, #1782, #1834 if they add events.

---

## Cluster section

### 1. The sweep

Count: **68 sites** where a uniffi exception's field dump reaches, or can reach, a sentence a person hears or reads. All are `.Message` reads. Paths are under `apps/slate-windows/src/SlateWindows/`. "Helper" means the proposed `FailureDetail.Of`, which is `VaultErrorDetail` for a vault error.

**A. Caught as `VaultException`, or a `VaultException`-typed value (50). The dump is certain.**

| file:line | surface | caught | today | should use | notes |
|---|---|---|---|---|---|
| `FilesSidebarViewModel.cs:1540` | Files status line (tag load) | `VaultException` | "Could not load tags: @message=…" shown; spoken unless the refresh is silent | helper | lead is host text |
| `FilesSidebarViewModel.cs:1722` | status + High announcement | `VaultException` | "Tag edit failed: …" | helper | `ReportFailure` (`:2837-2845`) |
| `:1779`, `:1859` | same | `VaultException` | "Could not create folder/note {name}: …" | helper | |
| `:1945` | canvas event `CanvasActionFailed(NewCanvas)` | `VaultException` | "{name}: @…" as the detail | helper | core event, host detail |
| `:2036`, `:2424` | status + announcement | `VaultException` | "Delete failed: …", "Trash failed: …" | helper | |
| `:2077`, `:2109`, `:2138` | same | `VaultException` | "Could not create folder note: …", "Could not delete folder note: …", "Could not copy wikilink: …" | helper | |
| `:2356`, `:2597` | same | `VaultException` | "Move failed: …", "Could not load folder files: …" | helper | |
| `FilesSidebarViewModel.FileManagement.cs:275` | same | `VaultException` | "Rename failed: …" | helper | core has `RenameFailed` with these words (`a11y.rs:2342`) |
| `…FileManagement.cs:1164`, `:1194`, `:1299` | same | `VaultException` | "Move failed: …", "Could not create folder {path}: …", "Could not duplicate {name}: …" | helper | |
| `FilesSidebarViewModel.TreeOperations.cs:309`, `:456` | status; spoken unless silent | `VaultException` | "Could not load files: …" | helper | shares the file with #1791 |
| `FilesSidebarViewModel.CanvasNotes.cs:41` | canvas `NoteCreateFailed` (`CanvasDocumentViewModel.cs:4152-4154`) | `VaultException` | dump as the reason | helper | |
| `FilesSidebarViewModel.SurfaceNotes.cs:42`, `:46` | graph `NoteCreateFailed` (`WorkspaceViewModel.GraphCreate.cs:133-138`) | `DestinationExists`, `VaultException` | "@path=…" as the reason | helper | |
| `Canvas/CanvasDocumentViewModel.cs:964` | `CanvasActionFailed(WhereAmI)` (`CanvasNavigator.cs:1423-1427`) | `VaultException` | dump as the detail | helper | |
| `…CanvasDocumentViewModel.cs:2570`, `:2715`, `:2856`, `:3154`, `:3216`, `:3787`, `:3858`, `:4007`, `:4891`, `:4992` | `CanvasActionFailed` (NewCard, NewGroup, MoveIntoGroup, Create ×2, RemoveFromGroup, CreateConnectedCard, Duplicate, CanvasAction, NewGroup) | `VaultException` | dump as the detail | helper | |
| `…CanvasDocumentViewModel.cs:4137` | `CanvasBlocked(NoteReadFailed)` | `VaultException` | dump as the message | helper | |
| `…CanvasDocumentViewModel.cs:4241` | `CanvasActionFailed` (vault file picker) | `VaultException` | dump as the detail | helper | |
| `Canvas/CanvasMutationFunnel.cs:486` | `CanvasActionFailed(CanvasAction)` | `VaultException` (held in `refusal`) | dump as the detail | helper | |
| `Graph/ConnectionsLeafViewModel.cs:894` | Connections leaf error state (`ConnectionsLeafView.cs:723`) | `VaultException` | dump in the error text; whether spoken is **unverified** | helper | |
| `Graph/GraphDocumentViewModel.cs:1855` | graph load failure | `VaultException` | dump in the failure text; spoken or not **unverified** | helper | |
| `Panels/HistoryViewModel.cs:573`, `:694`, `:960`, `:1085` | History leaf: load error, next-page error, inline diff error, deleted-files error | `VaultException` | dump shown in the leaf | helper | |
| `Panels/SyncDiagnosticsViewModel.cs:207` | Sync leaf error (`SyncDiagnosticsSurfaceView.cs:245`) | `VaultException` | dump shown | helper | `SyncDiagnosticsPanelTests.cs:239` pins the dump |
| `Reading/ReadingContentViewModel.cs:1208` | reading view, base embed card (`ReadingDocumentBuilder.cs:1403-1406`) | `VaultException` | "This base could not be executed: @…" | helper | |
| `WorkspaceViewModel.History.cs:245` | "Restore failed" message box (`:303`) | `VaultException` | dump as the dialog's body | helper | |
| `WorkspaceViewModel.History.cs:472`, `:537`, `:602` | "Can't restore" message box (`:488`, `:553`, `:618`) | `VaultException` | dump as the body | helper | |
| `WorkspaceViewModel.Properties.cs:297`, `:619` | `PropertyEditFailed` announcement | `VaultException?` parameter | "Property edit failed: @…" | helper | spoken, High |
| `WorkspaceViewModel.Templates.cs:199` | template sheet, inline failure | `VaultException` | dump shown inline; the comment says "Core's message, relayed verbatim", which it is not | helper | |

**B. A broad catch, or an `Exception`-typed parameter, that a vault error reaches (17). The dump appears when the failure is a vault error.**

| file:line | surface | caught | today | should use | notes |
|---|---|---|---|---|---|
| `Panels/BibliographyViewModel.cs:573`, `:603` | Bibliography leaf error rows | `Exception` (filtered) | message shown | helper | |
| `Panels/BulkRenameViewModel.cs:252` | sheet "Error: …" (`:302`) and `RenameFailed` announcement (`:304`) | `Exception` | message spoken and shown | helper | |
| `Panels/CitationsPanelViewModel.cs:214` | Citations leaf error and its spoken name (`:97-98`) | `Exception` | message | helper | |
| `Panels/NotePropertiesViewModel.cs:243` | Properties load error | `Exception` | message; spoken or not **unverified** | helper | |
| `Panels/RightPanePanelsViewModel.cs:479`, `:812`, `:915` | links, outline and tasks leaves ("Could not load tasks: …", `:299`; links `:330-354`) | `Exception` | message shown | helper | |
| `Panels/TaskIndexRepairCoordinator.cs:246` | "index needs repair" notice (`RightPanePanelsViewModel.cs:902`, `TasksReviewViewModel.cs:361`, `:573`) | `Exception` | message shown | helper | |
| `Panels/TasksReviewViewModel.cs:399`, `:610` | Tasks Review "Couldn’t load tasks. …" (`:190`) | `Exception` | message shown | helper | |
| `Canvas/CanvasDocumentViewModel.cs:5503` | canvas failed state: "This canvas could not be opened: {message} ({path})" | `Exception` parameter (`:2354-2359`) | message shown | helper | |
| `…CanvasDocumentViewModel.cs:5512` | canvas `ReopenFailed`: "{from} moved to {to}: {message}" | `Exception` parameter | message | helper | |
| `WorkspaceViewModel.Citations.cs:221` | bibliography seed notices | `Exception` | message; where the notices surface is **unverified** | helper | |
| `Commands/SlateCommandRegistrar.cs:170` | palette `PaletteCommandFailed` ("{label} failed: …") | `Exception` not `CommandException` | message spoken | helper | only when a command lets a vault error escape |
| `Graph/GraphDocumentViewModel.cs:1515` | graph diagram failure | `Exception` parameter; four vault arms, then `failure.Message` | dump for any other vault variant | helper for the fallback | owner decision 1 under #1890 |
| `Search/SearchOverlayViewModel.cs:915` | `SearchFailed` ("Search error: …") | same shape | same | same | same |

**D. A non-vault uniffi exception (1 site, 2 reads).**

| file:line | surface | caught | today | should use | notes |
|---|---|---|---|---|---|
| `Graph/GraphConfigStore.cs:171-172` | graph preferences load failure (`GraphPreferencesViewModel.cs:109`) | `GraphConfigException` | "graph.json is a newer version; not downgrading (@version=3)", or "@reason=…" alone | the typed fields | core has no renderer for this error |

**C. Ordinary messages that stay (19), listed so the census can route them through the helper:** `FilesSidebarViewModel.cs:2805`, `:2872`, `:2902` (settings store I/O); `EditorInteractions.cs:1592`; `FindInNoteViewModel.cs:706`; `Graph/ConnectionsLeafViewModel.cs:899`, `:912`; `Graph/GraphDocumentViewModel.cs:1859`; `Graph/GraphConfigStore.cs:88`; `Reading/MathMlUiaProperty.cs:90` (a diagnostic string; whether a person sees it is **unverified**); `VaultLifecycleViewModel.cs:587`, `:793`, `:1224`, `:1263`; `WorkspaceViewModel.History.cs:255`; and four that wrap a non-vault message in `VaultException.InvalidArgument` for core to render later (`WorkspaceViewModel.cs:1658`, `:1804`, `:1830`; `WorkspaceTabViewModel.Properties.cs:220`).

Already correct, for contrast: `HotExitStoreHost.cs:145-151` reads `HotExitException`'s typed `reason`; `VaultLifecycleViewModel.cs:770-779` uses `VaultErrorDetail`; the sixteen existing `VaultErrorDetail` callers.

### 2. Sentences the host composes that the rule says core should own

The sweep above fixes only the *detail*. The *leads* around it are host text. None of these moves is needed to close #1890; each is listed with whether moving it is an FFI addition (which also needs Mac adoption).

| Sentences | Where | Moving it to core |
|---|---|---|
| The Files sidebar's failure leads ("Move failed:", "Delete failed:", "Trash failed:", "Tag edit failed:", "Could not create note {x}:", and about twelve more), all `HostComposed`, marked "W0.5-3 residue" (`FilesSidebarViewModel.cs:2843`) | A-table rows for `FilesSidebarViewModel*.cs` | **FFI addition** (new events, or one `SidebarActionFailed {action, detail}` family like `CanvasActionFailed`). "Rename failed:" alone could use the existing `RenameFailed` with no FFI change; its fit (it is the bulk property rename's event) is **unverified**. |
| Import summary, "Import in progress", "Import is idle" | `FilesSidebarViewModel.Import.cs:41`, `:503-533` | **FFI addition.** Mac composes its own too. |
| "Loading files…", "Filtering files…", "{N} top-level items." | `TreeOperations.cs:342`, `:707`; `Filter.cs:254` | **FFI addition**; status-line copy, not announced. |
| The two `HumanReadable` copies of Mac's error arms | `GraphDocumentViewModel.cs:1509-1516`, `SearchOverlayViewModel.cs:909-916` | No FFI change (`vault_error_detail` exists) but it changes Windows wording; owner decision 1 under #1890. |
| `GraphConfigStore.Reason` | `Graph/GraphConfigStore.cs:167-173` | **FFI addition** (`graph_config_error_detail`) if core is to word it; reading the typed fields is the no-FFI fix. |
| Panel and dialog error leads ("Could not load tasks:", "Couldn’t load tasks.", "Could not load links:", "Error:", "This base could not be executed:", "This canvas could not be opened:", "Can't restore", "Restore failed", "Could not open vault:", "Unexpected vault error:", "Could not save recent vaults:", "Could not save pins:") | B-table and C-list files | **FFI addition** each; several are Mac's strings copied verbatim, as static label copy. |
| "Editor interactions are still loading; try again.", "Citations are still loading; try again.", "Save {file} before using this interaction." | `EditorInteractions.cs:2888-2890`, `:2919-2921`, `:3053-3056` | **FFI addition** (three events). Next to #1661. |
| "Task toggled on disk, but the editor changed / no longer matches it. Reopen the note before editing." | `WorkspaceViewModel.cs:1941`, `:1956`, `:2053` | **FFI addition.** Next to #1660. |
| The right-pane placeholder | `MainWindow.xaml:2802` | **FFI addition** if core-owned; see #1800. |

### 3. How #1891's census works, and what widening takes

`tests/SlateWindows.Tests/Censuses/BasesErrorDetailCensus.cs` (1,034 lines) does not scrape text. It binds the shell's sources with Roslyn (`Support/ShellCompilation.cs`, which loads every `.cs` under the shell root against the build's recorded references) and applies two rules to the Bases trees (`BasesTrees()`, `:42-48`: `WorkspaceViewModel.Bases.cs` and `Bases/`):

1. No member outside `BasePhrase.ErrorDetail` reads `Message` on a type that derives from `System.Exception` (member access, `?.`, property and extended patterns). A record's or model's `Message` is not flagged, because the rule asks the binder for the property's owner (`:611-617`).
2. An expression whose type is an exception, or carries one, may only flow on as an exception: into a local or parameter, a `throw`, a type or null test, core's renderer, a `TaskCompletionSource`, or a non-generic, non-virtual method whose body is itself inside the censused trees. `ToString()`, interpolation, concatenation, a field or property, an `object` slot, a generic helper, an interface call and a delegate handed elsewhere are all named (`AllowedUse`, `:735-791`; `IsConsumer`, `:930-952`).

It fails closed (the censused sources must bind without errors, `:52-67`) and carries about 80 planted cases from 19 Codex rounds. The older `VaultErrorDetailCensus.cs` is a different thing: a syntax check over five named sites.

Widening is not just removing an allowlist:

- **Rule 1 widens by changing one filter** (`BasesTrees()` to all of `ShellCompilation.Sources`) and one hard-coded name (`InTheHelper`, `:629-634`, names `BasePhrase.ErrorDetail`). It then fails on exactly the 68 + 19 + 2 exception reads above until they route through the helper. No new scan is needed.
- **Rule 2 needs a trial run before it can go shell-wide.** With every shell tree censused, any shell method becomes a trusted consumer and its body is checked, which is right (`HostLog.Write`, called with an exception about 98 times, reads only `GetType().Name`, `HostLog.cs:161-172`, and passes). But the rule also forbids storing an exception in a field or property and handing one to a delegate. The shell does both on purpose: records that carry a failure across threads (`TaskToggleOutcome`, `GraphDiagramRefresh(…, Exception? Failure)` at `Graph/GraphDocumentViewModel.cs:917`, `FileRecentsStore.Unreadable(Exception Error)` at `FileRecentsStore.cs:451`), completion callbacks typed `Action<SaveReport?, VaultException?, …>` (`WorkspaceViewModel.cs:1586`, `:4084`), `ticket.Fail(…)` (`WorkspaceSaveCoordinator.cs:176`). How many hits that makes is unknown until it is run; I could not run it.

Recommended design: a sibling `ShellFailureDetailCensus` that calls the existing `BasesErrorDetailCensus.Violations` (already `internal static`, `:601`) with the helper's identity as a parameter. Rule 1 over the whole shell from the first PR, with a scope list that grows per family so each PR lands green. Rule 2 run once as a spike at the start of the chunk; if its hits are few, adopt it shell-wide with named allowances (a stored exception is allowed when its holder is a private record with no `ToString` use, a delegate when its parameter is exception-typed); if many, keep rule 2 on Bases and say so in the census header. `BasesErrorDetailCensus` and its planted cases stay as the teeth for both.

### 4. Is "an operation's end is never reported" one class?

Partly. #1830 and #1791 are two different faults that share a class file and the `Status` line:

- #1830 is a terminal outcome thrown away (a cancel swallowed after the worker had already produced the sentence).
- #1791 is a transient status with no terminal write on the common path.

Other long-running operations, checked for the same gap:

| Operation | End and cancel reported? | Where |
|---|---|---|
| Import | cancel: **no** (#1830) | `FilesSidebarViewModel.Import.cs:140`, `:151-153` |
| Tree refresh | end: **no** on a plain refresh (#1791); cancel with no successor: **no** | `FilesSidebarViewModel.TreeOperations.cs:342`, `:701-713`, `:733` |
| Sidebar filter | "Filtering files…" at `FilesSidebarViewModel.Filter.cs:254`; replaced at `:524`/`:531`; its cancel catches are empty (`:391`, `:452`). Every canceller I found writes its own status, so probably fine: **unverified** |
| Batch trash | yes: "Cancelled. No items were moved to the Recycle Bin." and the status-bar item | `FilesSidebarViewModel.cs:2414-2421`; `MainWindow.xaml:944-949` |
| Rescan | yes: `VaultRescanFinished`, the uncounted form, or "incomplete"; a cancel only happens on close or vault switch and is rightly silent | `VaultLifecycleViewModel.Rescan.cs:469-486`, `:534-539` |
| Initial scan | yes: "Scan cancelled." / "Scan failed: …" | `VaultLifecycleViewModel.cs:1101-1110` |
| Bulk property rename | yes: `RenameSummary` or `RenameFailed`; a close mid-run waits for the terminal publish | `Panels/BulkRenameViewModel.cs:203-212`, `:299-330` |
| Template apply | synchronous; failure presented inline | `WorkspaceViewModel.Templates.cs:188-200` |
| Bases export | failure events exist (`BasesViewExportFailed`); the cancel path was not traced: **unverified** |
| Move To's folder list | cancel is silent by design (the sheet is closing) | `FilesSidebarViewModel.MoveToLoading.cs:138` |

So the class has two confirmed members and one candidate. It does not need a census; a hosted fact per member is enough. They belong in one chunk because they share `FilesSidebarViewModel` partials and the status line's hold-and-reassert rules.

### 5. Recommended cut into PRs

1. **Core: the landed toggle and the unindexed caveats** (#1660, and #1395 if the owner folds it). `a11y.rs`, `slate-uniffi/src/lib.rs`, `corpus.json`, the three announcement sites, the three corpus censuses, one Swift mirror line. Lanes: rust, windows, swift compile. First, because it changes `WorkspaceViewModel.cs` and `TasksReviewViewModel.cs` semantically and the sweep then only touches them mechanically. Two or three ledger lines.
2. **Sweep A: helper, census, Files sidebar** (#1890, part 1). `FailureDetail.cs`, `ShellFailureDetailCensus.cs` scoped to the sidebar files, 21 sidebar sites plus the 3 ordinary ones, the files-sidebar hosted fact. Includes the rule 2 spike.
3. **Silent or stuck status** (#1830, #1791). After sweep A, same files. `Import.cs`, `TreeOperations.cs`, the status-bar item.
4. **Sweep B: canvas, graph, search** (#1890, part 2). 16 canvas sites, 5 graph and search, `GraphConfigStore.Reason`; census scope grows.
5. **Sweep C: panels, reading, workspace, lifecycle, commands** (#1890, part 3, closes it). The rest; the census scope becomes the whole shell and the scope list is deleted.
6. **Activation** (#1661). Small, `EditorInteractions.cs` and two lines of `WorkspaceViewModel.cs`. Any time after PR 1; not in the same wave as sweep C if sweep C routes `EditorInteractions.cs:1592` through the helper.
7. **The placeholder** (#1800). After the owner picks the copy; not in the same wave as PR 3 (`MainWindow.xaml`).

Sweeps B and C share no files with each other and could run in one wave.

### 6. Risks

- **The census's second rule may not fit the whole shell.** Decide by a trial run before promising it. If it is dropped to rule 1 only, a future `$"{exception}"` outside Bases is not caught.
- **A site's text gets longer or changes shape.** `VaultErrorDetail` returns whole sentences for some variants ("Something named b.md already exists there."), and several leads then read "X failed: Something named … there." with no further closing. That is today's Bases behaviour and is fine; but a lead that adds its own period after the detail would double it. None of the 68 does (checked by reading the format strings), except that `CanvasPhrase.OpenFailed` appends " ({path})" after the detail (`CanvasDocumentViewModel.cs:5503`), which reads oddly after a full sentence.
- **`SavedButUnindexed` reaches other leads.** After the sweep, a landed-but-unindexed error at any swept site says "… failed: Saved, but not indexed yet: …". That is #1660's contradiction on other surfaces (restore, property edit, canvas note create). The sweep should not try to fix it; the plan should name it as a known remainder, or the #1660 PR should grep for `SavedButUnindexed` arms at those sites. I did not trace which swept calls can return it: **unverified**.
- **The toggle code is heavily defended.** `WorkspaceViewModel.cs:1575-2060` and `:4084-4170` carry 19 recorded adversarial rounds. PR 1 should change which event is announced and nothing else, unless the buffer splice is specced on its own.
- **A new `A11yEvent` collides with other core chunks** on the variant-count pin, `corpus.json` and the three corpus lists. Sequence core PRs that add events; do not run two in one wave.
- **Mac divergence grows quietly.** Mac has two private copies of the save sentence and no adoption of `vault_error_detail`. Every core wording change here must add its ledger line, or #1527 inherits surprises.
- **#1791's fix can resurrect a stale line.** Restoring "what it said before" must not bring back a sentence an intervening event has replaced; the existing hold-and-reassert rules (`FileManagement.cs:128-153`) were written after exactly that mistake (its comment cites Codex rounds). Write the fact for "a status written while the refresh was in flight wins".
