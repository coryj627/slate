# CB-16: A cancelled import says so, its sentences are core's, and the Files line never stays on "Loading files…"

Issues: #1830 and #1791 (closes both). Size M to L. Lanes: rust (a change under `crates/` runs the Windows lane in full), windows (the app job and its unit facts), swift (the corpus mirror only; the Mac lane proves it compiles). AT: an NVDA spot check (the PR opens as a draft and is marked ready when the phrases are in its body). Wave: C11. Decision: CD10 as the owner answered it on 2026-11-16 (§3.1): "The import sentences move to core now, as new core events, not as the host's sentence with a ledger line. The cancel is still spoken at once, and the Files status line still says what it said before, never a count." Interlocks (plan §6): `a11y.rs`, the corpus and the variant count (CB-16 is next, after CB-14 and #2028); `crates/slate-uniffi/src/lib.rs` (CB-16 branches only after CB-34 merges, by the lead's ordering rule); `FilesSidebarViewModel*.cs`; `MainWindow.xaml`, `MainWindow*.cs`; `focus_integrity_manifest.json`. CB-16 follows CB-17b (#2041, #2045, both merged): its new Cancel is a button of the class that sweep closed, so it meets CB-17b's keys-first rules and census. Review: an Opus xhigh red team on the spec (done; "Red team record") and on the diff, then Codex until it approves, then the lead's merge judge.

Written 2026-11-24 by Principal U2 at `origin/main` ad04a470 from: the plan's rows (§3 CD10, §3.1 CD10, §5 CB-16, §6, §7, §8's #1830 ledger line, §9's unverified row, §11); `reading/05_spoken_failures.md` (#1830 at :50-75, #1791 at :79-101, cluster §2 and §4); the two issues with their owner comments; CB-17b's spec and its delivery sections; one worker's trace of the sidebar filter and the Bases export (by reading, kept with the principal's working notes, not committed); and this spec's own probe runs (step 0). A statement that rests on reading alone says so.

`C/` is `crates/slate-core/src/`; `U/` is `crates/slate-uniffi/src/`; `S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`; `M/` is `apps/slate-mac/`; `MF` is `docs/plans/18_windows_port/focus_integrity_manifest.json`; `D` is `docs/plans/18_windows_port/a11y_trigger_designations.json`.

## Goal

1. A cancelled import says so on Windows, at once when the cancel is asked for and again with what it copied when it settles.
2. The import's sentences are core's: its summary, its cancel sentence and the status texts that say an import is running. Windows speaks them; the Mac compiles unchanged and its adoption is a #1954 line.
3. The status bar shows an import running, with a Cancel that moves the keys first.
4. The Files status line never stays on "Loading files…" or "Filtering files…", and a plain refresh never writes a count: after a refresh or a filter run that ends with nothing of its own to say, the line says what it said before, unless that was a line the tree itself wrote, which is stale by then.
5. The two unverified members of #1830's class (§9), the sidebar filter's and the Bases export's cancel paths, are checked by a run, and what the run finds is fixed or filed (Call 7).

## Step 0, as run (2026-11-24, main ad04a470, Release, this desktop, under the lock)

Fourteen probe cases in `Cb16Step0ProbeTests`, a throwaway class in a worktree cut from `origin/main` with its own cargo target; never committed. The source (`Cb16Step0ProbeTests.cs.txt`) and the log (`step0_results.log`) are in the principal's working notes. No probe asserts anything about the product. Runs: `dotnet test … --filter FullyQualifiedName~Cb16Step0ProbeTests`, 13 passed in 9 s; then P1e alone, 1 passed.

| # | Probe | Result on main |
|---|---|---|
| P1a | An import of a 600-file folder, cancelled from a second thread once 3 files have landed (production worker, `Task.Run`; a bare sidebar, no lifecycle) | The import ended 3 ms after the cancel. 3 of 600 files are in the vault. **Nothing was said**, and the Files line did not change ("1 top-level items."). The sidebar refreshed nothing. |
| P1e | P1a through the lifecycle (`RescanTests.Harness`), with its change feed | 5 of 600 copied; nothing said; the line unchanged. The copied folder was not in the tree when the import ended, and **was in it 800 ms later**: the feed heard core's writes and refreshed. So in the app the tree does show what a cancelled import copied; only the sentence is missing. |
| P1b | The cancel lands after the pick and before the worker runs | Nothing said; the line unchanged. |
| P1c | An import of 2 files that completes | "Copied 2 files to the vault root." is said (`HostComposed`, Medium). The Files line shows it for 2 ms, then **"Loading files…", and stays there** after the tree published with the new files. |
| P1d | A cancel 30 ms into a 50 MB file, then a 200 MB file, each followed by a folder of 20 files | 50 MB: the cancel came while core was writing the file; the import ended **664 ms after the cancel**, the file landed, the folder did not, nothing was said. 200 MB: the cancel came during the read, which honours the token; the import ended in the same millisecond, nothing landed, nothing was said. Core's write (`CreateExclusiveBytes`) takes no token. A file at the 256 MB limit, cancelled during its write, would end about 3.4 s after the cancel by the same rate (an estimate, not run). |
| P2a | A plain `Refresh()` after the open's counted one (the asynchronous tree, as the app runs it) | "1 top-level items." becomes **"Loading files…" and stays**. |
| P2b | A Slate write the change feed hears (`CreateInCore`, the lifecycle's real listener, the 150 ms debounce) | The same: **"Loading files…" stays**. |
| P2c | New Canvas from the sidebar, then the feed (the issue's repro) | "Loading files…" (+57 ms), "Created canvas "Untitled Canvas"." (+61 ms), then the feed's refresh: **"Loading files…" (+327 ms), and it stays**. |
| P2d | A sort change, then a grouping change | Not stuck: each writes its sentence after its `Refresh()` ("Sorted by name, Z to A.", "Sorted by modified, newest first, grouped by date."). The issue's guess that these two callers stick is wrong. |
| P2e | A refresh cancelled with no successor (`CancelTreeRefresh`, the close's path) | **"Loading files…" stays**. |
| P3a | A user filter ("note") cancelled by the close's admission (`PrepareForApplicationClose`) | The close is refused and the vault line says "File filter cancellation requested. Close the vault again after the current query finishes." The Files line **stays on "Filtering files…"**, and the query that line names never runs. |
| P3b | The field set to a single space while a filter run waits | The filter is off (`IsFilterActive` false) and nothing is said; the line **stays on "Filtering files…"**. |
| P4 | A Bases export whose `.base` is deleted and reloaded while its save dialog is up | The base goes `Failed` during the dialog; the export file is not written and **nothing is said**. |

In passing, P2c heard " Untitled Canvas, tab 1 of 1." with a leading space: Windows sends core's `TabFocused` with an empty prefix on a tab activation (`S/WorkspaceViewModel.Layout.cs:124-128`) and with "Tab bar. " on another route (`:1167-1168`), and core renders `"{prefix} {filename}, tab …"` (`C/a11y.rs:2056-2062`), so one route has a leading space and the other a double space. Speech is unaffected (a screen reader collapses the spaces, by reading); the text is wrong. Found on the way (Call 7).

**What step 0 changes.** "At once" has a measured cost: core's write cannot be interrupted, so the summary of a cancel during a large file's write comes seconds after the press (P1d, Call 2). A completed import loses its sentence from the Files line at once (P1c, which is #1791 inside the import). #1791 has three routes, not one: a plain publication, a cancel with no successor, and the change feed after any Slate write (P2a, P2b, P2e). The two §9 members are defective (P3a, P3b, P4). The tree's missing copies after a cancel (P1a) are not a defect in the app: the feed shows them (P1e).

## Drift check (plan and reading against ad04a470)

1. **CB-17b changed the import.** The reading's `IsImporting` (set before the picker, so "running" included the picker, `:97`, `:103`) is now two flags: `IsImporting`, the admission, set at the press (`S/FilesSidebarViewModel.Import.cs:126`), and `IsImportRunning`, set once the pick returned sources (`:160`). Cancel import is off until then (`S/FilesSidebarViewModel.cs:688`), and the pick is a modal box and dialog (`S/MainWindow.xaml.cs:467-500`), so a reader cannot cancel during the picker. The reading's second CD10 question ("a cancel that lands during the picker, spoken at once or when the picker closes") has no user route left; the close and shutdown paths (`S/VaultLifecycleViewModel.cs:2176-2183`, `S/FilesSidebarViewModel.SessionWork.cs:55`) are the only cancels during a pick. "At once" now means at the press during the copy.
2. **Line numbers in the import.** `ImportStatus` `:41` is `:65`; `CancelImport` `:43-74` is `:67-98`; the rethrow `:140` is `:171` (and a second, after the pick, at `:147`); the empty catch `:151-153` is `:182-184`; the completed report `:146-148` is `:177-180`; `ImportSources`' cancelled result `:312-319` is `:343-351`; `ImportSummary` is `:535-571`, its cancelled clause `:554-557`. The status bar's trash item `MainWindow.xaml:944-949` is `:933-950`. The routes to cancel: File menu `:328-329` is `S/MainWindow.xaml:316-318`; the sidebar's button `:1372` is `:1362`; Escape `:1444-1454` is `S/MainWindow.xaml.cs:1440-1452`; the palette `S/Commands/SlateCommandRegistrar.cs:678` holds.
3. **#1791's lines.** `Refresh` `:214`, and its write `:342`, hold. `ApplyTreeRefresh`'s arms `:701-713` are `:707-719`; its reassert `:727` is `:733`. `ReassertStatusAfterPublication` is `S/FilesSidebarViewModel.FileManagement.cs:153-167` (hold `:150-151`). `HandleFileChange` is `S/VaultLifecycleViewModel.cs:1177`, its debounce `:1210-1234`; the other plain callers `:1172`, `:1860`, `:2307` are `:1197`, `:1891`, `:2338`. `CancelTreeRefresh` is `S/FilesSidebarViewModel.TreeOperations.cs:739-753`; the refused admission `:406-409`. The Files line is a `TextBlock` at `S/MainWindow.xaml:1392`. `SidebarTagFilterTests.cs:339` holds.
4. **The sort and grouping callers** (the issue's `:709` and `:733`, now `S/FilesSidebarViewModel.cs:749-795`) do not stick (P2d).
5. **A plain refresh can already write a count.** `ApplyTreeRefresh` writes "N top-level items." followed by the settings notice whenever a notice is up, counted refresh or not (`S/FilesSidebarViewModel.TreeOperations.cs:711-714`); the notice is set once, from a read-only settings reason (`S/FilesSidebarViewModel.cs:596-599`). Neither the issue nor the reading names it; CD10's "never a count" does (W8).
6. **The reading's filter row** ("every canceller writes its own status, so probably fine") is wrong for two paths (P3a, P3b). Its Bases row ("the cancel path was not traced") finds no cancel path at all: the export takes no token and shows no transient line. What P4 shows is a silent drop of a different kind: the "not ready" check runs before the scope prompt and the save dialog (`S/WorkspaceViewModel.Bases.cs:813-819`), and `ExportText` refuses a base that went `Failed` or `Loading` meanwhile with `deliver(null)`, which its callers read as "already announced" (`S/Bases/BaseDocumentViewModel.cs:854-857`; the lost handle, `:872-879`, is the same). Copy as Markdown shares it through its scope prompt (`S/WorkspaceViewModel.Bases.cs:571-588`). The Mac composes before its panel, so it has no such gap (by reading, `M/Sources/SlateMac/Bases/AppState+Bases.swift:3464-3467`).
7. **The Mac.** Its summary is `importBatchAnnouncement` (`M/Sources/SlateMac/AppState.swift:17652-17699`, the reading's `:17465-17476` moved), posted as `HostComposed` Medium (`:17235-17242`, `:18008-18021`). It has three clauses Windows lacks ("moved N items", "found N destination items during reconciliation", "N import warnings need attention"), capitalizes the first clause's verb when no copy precedes it ("Moved", "Found", `:17676`, `:17680`), adds the cancelled clause when a cancellation was requested even if the copy finished (`:17194-17197`), and prints counts without grouping (`CountCopy.counted`, `M/Sources/SlateMac/CountCopy.swift:38-42`). Windows groups its counts with the current culture's `N0` (`S/FilesSidebarViewModel.Import.cs:540-561`), so its own text at 1,000 and up already varies by locale, and has a clause the Mac lacks (the 10,000-item limit). Below 1,000 the two hosts' shared clauses are the same words. The Mac's strip says "Importing…", "Finishing import…", "Moving items…" or "Cancelling import…" (`M/Sources/SlateMac/Sidebar/SidebarImportProgressStrip.swift:95-109`), and a cancel speaks nothing at the press: it sets the strip's phase (`M/Sources/SlateMac/Sidebar/SidebarImportBatchOwner.swift:134-145`). `FileTreeDragDropTests.swift:1225` pins "Copied 2 files to the vault root.".
8. **The trigger ledger's residue register** (`D`, `residue.Windows`) lists the import's one `HostComposed` construction (`FilesSidebarViewModel.Import.cs#FilesSidebarViewModel.ImportAsync@1`). Removing it is a residue drift the generator refuses until the row goes (`scripts/a11y_trigger_ledger.py:82-84`).
9. **No Windows fact pins the import's sentence** (a search of `T/` for its words finds none); `StatusBarAndPaneTogglesTests.cs:591` still records the gap by name, in a dictionary whose values `EverySidebarCommandSaysWhetherItsResultIsInThePane` does not assert (`:605-606`).
10. **The contract registers** (`docs/plans/*_contracts.md`) hold no row for #1830 or #1791 (a search of each); nothing to close there.

## Design

### Core (`C/a11y.rs`)

**C1. The summary, one event for both hosts.** `A11yEvent::ImportFinished`, in the Files group, Medium (it stays out of the High list):

```rust
/// Plan 47 CB-16 (#1830, CD10): an import's outcome. One sentence for both
/// hosts' importers: Windows' picker import and the Mac's Finder drop, whose
/// moved and reconciled items and warnings Windows never has, and whose entry
/// limit the Mac reports per item.
ImportFinished {
    copied_files: u32,
    copied_folders: u32,
    moved_items: u32,
    reconciled_items: u32,
    /// The vault-relative folder the import went into; "" for the vault root.
    destination: String,
    cancelled: bool,
    /// Items not imported; not said when `cancelled`, as both hosts do today.
    not_imported: u32,
    warnings: u32,
    /// The entry limit the walk stopped at, when it did.
    stopped_at_entry_limit: Option<u32>,
},
```

It renders both hosts' sentences as they are today, with core's grouped counts (`counted`, `C/a11y.rs:3793`, the `1,032` convention):

- The clauses, in order, joined by " and ": "Copied {files} and {folders}" (either part alone when the other is zero); "moved {N items}", "Moved" when it is the first clause; "found {N} destination item(s) during reconciliation", "Found" when it is the first clause. "No items were imported" when all three are zero.
- " to {location}.", where the location is "the vault root" for an empty destination, else its last segment (trailing slashes ignored).
- Then " Import cancelled; completed copies remain." when `cancelled`, else " {N} item was / items were not imported." when `not_imported` is above zero.
- Then " {N} import warning needs / warnings need attention." when `warnings` is above zero.
- Then " Import stopped at the {N}-item safety limit; remaining entries were not enumerated." when the limit was hit.

Examples: "Copied 2 files to the vault root." (P1c); "Copied 3 files and 1 folder to Inbox. Import cancelled; completed copies remain." (P1a's shape); "No items were imported to the vault root. Import cancelled; completed copies remain." (P1b, and the Mac's early-cancel sentence today); "Moved 2 items to Notes." and "Copied 1 file and moved 2 items to Notes. 1 import warning needs attention." (Mac shapes). Below 1,000 every count renders as each host renders it today; at 1,000 and up the Mac's counts gain a comma and Windows' always take the comma, whatever the culture (Call 6).

**C2. The cancel, spoken at once.** `A11yEvent::ImportCancelling`, no fields, Medium (the trash's stopping line is Medium, `S/FilesSidebarViewModel.cs:2925-2934`). It renders `import_status_line(ImportPhase::Cancelling)`: "Cancelling import…", the Mac strip's own words for this moment. The words are new speech on Windows (Call 2).

**C3. The status texts.** `pub enum ImportPhase { Importing, Finishing, Moving, Cancelling }` and `pub fn import_status_line(phase: ImportPhase) -> String`: "Importing…", "Finishing import…", "Moving items…", "Cancelling import…", the Mac strip's four titles (choosing "Finishing import…" from its `hasRemainingProviders` stays the Mac's rule). Windows uses Importing and Cancelling. They replace Windows' unbound `ImportStatus` ("Import in progress", "Import is idle", `S/FilesSidebarViewModel.Import.cs:65`), which the reading's catalogue counts among the import sentences (§2's table). Call 1.

**C4. `TabFocused` joins its prefix with one space** (Call 7): `"{filename}, tab …"` for an empty prefix, `"{prefix} {filename}, tab …"` with the prefix trimmed otherwise. Corpus witnesses for "" and "Tab bar. ". No other render changes.

**C5. Count and corpus.** The count pin moves 240 to 242 (`C/a11y.rs:9318-9343`), with its comment line. Corpus witnesses for `ImportFinished`: P1c's sentence; P1a's (files, a folder, a named destination, cancelled); P1b's (all zero, cancelled); not imported with no cancel; the limit; moved only; found only; copied, moved, found and warnings together; counts of 1,000 and up; and the singulars. One for `ImportCancelling`. `corpus.json` regenerated (`SLATE_REGENERATE_FIXTURES=1 cargo test -p slate-core a11y`), and both hand mirrors (`T/Censuses/A11yCorpusCensus.cs`, `M/Tests/SlateMacTests/A11yCorpusCensusTests.swift`) in corpus order.

### FFI (`U/lib.rs`)

**F1.** The two variants in the FFI `A11yEvent` and their arms in `impl From<A11yEvent>`; `ImportPhase` as a 1:1 mirror with its `From`, as `RunningWork` is (`U/lib.rs:9979-9995`); `import_status_line` exported beside `empty_panel_line` (`U/lib.rs:459-465`); a mirror test that each `ImportPhase` member crosses and renders core's line, as `WorkStillRunning`'s does (`U/lib.rs:15967-15995`). The C# bindings are regenerated with `generate-bindings.ps1 -Locked` and this worktree's own cargo target. With no Mac here, the Mac lane proves the Swift side. No Swift `switch` over `A11yEvent` lacks a `default` (CB-14's and CB-17b's searches, and the red team's), so the Mac compiles unchanged.

### Windows: the import (`S/FilesSidebarViewModel.Import.cs`, `S/FilesSidebarViewModel.cs`)

**W1. Every end of an import that ran is reported.** In `ImportAsync`:

- When the worker returned a result, cancelled or not, the import reports it: the rethrow between the result and the report (`:171`) goes. The report is `ReportResult`'s discipline with core's event: `Refresh()` first, then `Status` = the event's rendering, `HoldStatusForPendingPublication()`, and the event announced. Its `cancelled` is the result's own: a cancel that lands after the copy finished reports a full copy with no cancel clause (accepted; the Mac adds the clause there, risk register).
- When the import was cancelled before the worker produced a result (the worker never ran, P1b, or `RunImportWorkerAsync` threw the cancellation), the same report with every count zero, `cancelled: true` and the destination the import had chosen, and no `Refresh()` (nothing can have landed).
- After the pick (`:147`): an empty pick returns silently first, as today; only a pick that returned sources and was cancelled meanwhile reports the zero, cancelled sentence. (Its only cancellers are the close and shutdown: drift 1.)
- When `SessionShutdownStarted`, nothing is said: the session is closing.
- The two failure sentences ("Could not choose import sources.", "Could not import the selected items.", `:140-143`, `:187-190`) stay as they are, through `ReportFailure` (Call 1).

**W2. The cancel is spoken at the press.** `CancelImportCommand` runs `RequestImportCancellation()`: when a cancellation is outstanding, it cancels as today, sets the import's phase to Cancelling, and announces `ImportCancelling`. A second press finds none and says nothing. `CancelImport()` (shutdown, `S/FilesSidebarViewModel.SessionWork.cs:55`; the close's admission, `S/VaultLifecycleViewModel.cs:2178`) cancels and sets the phase without speaking: the close says its own refusal line ("Import cancellation requested. Close the vault again after completed copies finish reconciling.", unchanged), and the summary follows when the import settles. Every user route goes through the command: the File menu, the palette (`SlateCommandRegistrar.cs:678`), the sidebar's button, the new status-bar button and Escape (`S/MainWindow.xaml.cs:1449`, which executes the command whatever its `CanExecute`; a press while cancelling finds no outstanding cancellation and says nothing).

**W3. The phase.** The import's phase becomes Importing in the same step that sets `IsImportRunning` true (`:160`), Cancelling at W2's cancel, and is cleared in `finally` beside `IsImportRunning = false` (`:209`), so each import starts at Importing whatever the last one ended in, as the trash resets its phase in `BeginTrash` (`S/FilesSidebarViewModel.Trash.cs:40-47`).

**W4. Cancel import goes off at the press.** Its `CanExecute` becomes "running and not cancelling", as the trash's Cancel does (`CanCancelTrash`, `S/FilesSidebarViewModel.Trash.cs:22`). `CommandKeysFirst` moves the keys first from any button bound to it (CB-17b D2). The sidebar's Cancel import now goes off at the press rather than when the import settles; its keys still go to "File actions, grouping, expanded". CB-17b's witness `CancelImportMovesTheKeysToItsGroupFirstWhenTheImportSettles` stays green by reading (it asserts across the press and the settle) but its name and summary become false: it is renamed `CancelImportMovesTheKeysToItsGroupFirstWhenPressed`, with `MF:287`. Call 4.

**W5. The status texts.** `ImportStatus` is deleted. `ImportStatusLine` is core's `import_status_line` for the phase, raised when the phase or `IsImportRunning` changes.

### Windows: the status bar (`S/MainWindow.xaml`, `S/MainWindow.Trash.cs` and a new `S/MainWindow.StatusBarCancel.cs`, `S/MainWindow.ShellRegions.cs`)

**W6. An item beside the trash's.** A `StatusBarItem` in a new `Auto` column after the trash's (`S/MainWindow.xaml:898-905`, `:933-950`; every later `Grid.Column` moves up by one), shown while `IsImportRunning` (not during the pick), with a `TextBlock` bound to `ImportStatusLine` (AutomationId `ImportProgressStatus`) and a `Button` bound to `CancelImportCommand` (Content "Cancel", AutomationId `CancelImport`, Name "Cancel import", the command's palette label in sentence case, and `HelpText` bound to `ImportStatusLine`, so a reader who tabs to it hears what is running: NVDA reports a description after the role by default, by reading). Bound through `FileSidebar.` with `FallbackValue=Collapsed`, as the trash's item is, so no vault means no item. It shows with the Files pane hidden, which is what #1830 asks (DS-7). The bar stays 24 pixels high with the item shown, as with the trash's.

**W7. Its Cancel's keys.** The trash Cancel's two pieces serve both buttons through one helper that keeps its state for each button: the stranded landing that puts the keys back where they came to Cancel from, else the editor (`LandWhereTrashCancelCameFrom`, `S/MainWindow.Trash.cs:71-79`, registered at `S/MainWindow.ShellRegions.cs:929-931`), and the restore after an off that `CommandKeysFirst` could not prevent (`TrashCancel_AvailabilityChanged`, `:81-99`). The new button is registered with `RegionFocusGuard.SetStrandedLanding` the same way. When an import ends with the keys on the button and no press, `IsImportRunning`'s change hides the item before the command's raise turns the button off (`S/FilesSidebarViewModel.Import.cs:55-61`): the stranded landing takes the keys as the button leaves the tree.

**W8. The manifest** (`MF`) gains `xaml-command MainWindow.xaml CancelImport CancelImportCommand` with a `keys-first:` witness (fact 14) and `xaml-binding MainWindow.xaml StatusBarItem(ImportProgressStatus) Visibility` as `guard: ShellStatusBarBorder`, as the trash's item is (`MF:236`); `MF:287`'s witness is renamed (W4).

### Windows: the Files line (`S/FilesSidebarViewModel.TreeOperations.cs`, `.FileManagement.cs`, `.Filter.cs`)

**W9. A transient line puts back what it replaced, unless the tree wrote it.** One pair of helpers beside `HoldStatusForPendingPublication`:

- `ShowTransientStatus(text)` remembers the line it replaces, and whether the tree wrote that line, then writes the text. If the line already shows a transient text, it keeps the memory it has: a second refresh in a row puts back the line from before the first.
- `EndTransientStatus(text)` acts only while the line still shows that text: a line written while the work ran wins, which is the reading's risk (§6). It puts back the remembered line, or, when the tree wrote that line (a publication arm's count, overflow or notice line, or a tree failure's line, `S/FilesSidebarViewModel.TreeOperations.cs:707-727`, `:357-367`), leaves the line empty: that line is stale once the tree has moved, and putting back an old count is the count CD10 forbids. An end that does not match leaves the memory alone; only a put-back clears it. Call 3.
- Where it is used. `Refresh`'s asynchronous path shows "Loading files…" through it (`:342`); it ends at the end of `ApplyTreeRefresh`, after its arms and the reassert (`:707-733`), on `CancelTreeRefresh` when the cancelled refresh has no successor (`:739-753`), and on a refused admission (`:406-409`, posted to the tree's context); the failure paths write their own line. `ScheduleFilter`'s user run shows "Filtering files…" through it (`S/FilesSidebarViewModel.Filter.cs:252-255`); it ends when `CancelFilter()` runs while the line still shows that text (the close's path, P3a, whether the run it cancels is the user's or a publication's automatic one that superseded it), and when a field that trims to nothing ends the filter (P3b, `:196-205`): the line goes back, and nothing is spoken, since a field that starts with a space is typing (the setter's own rule, `:58-62`). A superseded run needs nothing: its successor writes.
- **The settings notice arm** writes the notice alone on a refresh that does not count, and the count with it only when the refresh counts (`:711-714`), so no plain refresh writes a count (drift 5).
- A filter the close cancelled is not run again (the red team's M2): XD1 (contract 38, `:739-746`) re-issues only work a close skipped for a later reason, and a re-run would keep a slow query's close refused. The field then shows a query whose results are the previous run's until the next edit or publication; that goes on #1955.

### The Bases export: filed, not fixed here (Call 7)

P4's drop is not a cancel path (the export takes no token) and its fix is in `Bases/` and `WorkspaceViewModel.Bases.cs`, another subsystem: AGENTS.md classes it High, filed. The issue carries P4 as its repro, the Copy as Markdown twin, the fix shape (each refusal in `ExportText` reports through a callback run inside its posted continuation after the `IsShutDown` check, `S/Bases/BaseDocumentViewModel.cs:885-890`, and each caller speaks core's existing "the base is not ready" failure), and the small wording find beside it (Copy as Markdown's compose failure says "could not be exported", `:893`).

### Docs and registers

- `D`: `designated.Mac` gains `ImportFinished` and `ImportCancelling`, each with `reason` ("Plan 47 CB-16 (#1830, CD10): the Mac's adoption is owed on #1954; until then importBatchAnnouncement composes the summary and a cancel speaks nothing at the press."), `decision` (`docs/plans/38_notification_dispatcher_contracts.md`) and `status` (`recorded`); `residue.Windows` loses `ImportAsync@1`.
- Contract 38 (`docs/plans/38_notification_dispatcher_contracts.md`): the ledger regenerated (`python scripts/a11y_trigger_ledger.py --write`, against this worktree's Release build), and a short dated amendment before it, "Import events (plan 47 CB-16, #1830, CD10, 2026-11-16)", recording the owner's call: the two events, their Medium priority, the order (the cancel at the press, the summary when the import settles), the silent shutdown and the close's own refusal line.
- `StatusBarAndPaneTogglesTests.cs:591`: the `CancelImportCommand` row becomes "the status bar's progress" (a record, not a fact: drift 9).
- No chord, command, leaf, tab, help line or CLI verb (the parity matrix does not move). No census-path read (no census trait). No new failure text reading an exception (`ShellFailureDetailCensus` sees nothing new).

## What a reader hears after (by design; the NVDA spot measures it)

| Route | main (step 0) | after |
|---|---|---|
| An import that completes | "Copied 2 files to the vault root."; the Files line then says "Loading files…" | the same sentence, from core; the line keeps it |
| Cancel import pressed during a copy (any route) | nothing | "Cancelling import…" at once; then, as the import settles, "Copied 3 files and 1 folder to the vault root. Import cancelled; completed copies remain." |
| The same, during a large file's write | nothing, for as long as the write runs (664 ms at 50 MB) | "Cancelling import…" at once; the summary when the write ends |
| A close during an import | the close's refusal line; nothing when the import ends | the refusal line; the summary when the import ends |
| New Canvas, then the Files line read | "Loading files…" | "Created canvas "Untitled Canvas"." |
| A filter, then a space in the field | nothing; the line "Filtering files…" | nothing; the line what it said before the filter ran |

## Tests

A tests-first commit carries every fact below, the core and FFI variants and function declared but rendering a placeholder (so each Rust fact is red on its words, not on a missing name), the corpus rows, the regenerated `corpus.json` and both hand mirrors (so the mirror tripwires are green and each fact is red for its own reason). The PR body shows each red. No fact adds a sleep: they wait on conditions with the existing pump helpers.

**Red first, on main or on the placeholder:** 1 to 3, 5 to 9, 11 to 14, 16 to 21, 24, 25. **Guards, green on main, each with the mutation that turns it red:** 4 (the count pin and the corpus: a variant removed), 10 (the silence half; its summary half is red with fact 6's), 15 (no assertion reads the row: a record), 22 (a line written while the refresh ran: `ReportFailure` holds it on main; the "still shows the text" check removed), 23 (`SidebarTagFilterTests.cs:339`, the counted refresh still counts).

**Rust (`C/a11y.rs`).**
1. `an_import_summary_is_each_hosts_sentence`: a table of Windows cases (P1c, P1a's shape, P1b's, not imported, the limit) equal to Windows' `ImportSummary` today, and Mac cases (moved only, found only, copied with moved, found and warnings, the cancelled early return) equal to `importBatchAnnouncement` today, byte for byte below 1,000; the grouped forms at 1,000 and up; Medium.
2. `the_import_status_lines_and_the_cancel_sentence`: each phase's line; `ImportCancelling` renders the Cancelling line; Medium.
3. `an_import_destination_names_its_last_folder`: "", "Inbox", "a/b", "a/b/".
4. The count pin (242), `corpus()`, `corpus_renders_the_shipped_strings`, `committed_corpus_artifact_matches_the_vocabulary`, and the two mirror tripwires.
5. `a_tab_focus_joins_its_prefix_with_one_space`: "", "Now", "Tab bar. ".

**Rust (`U/lib.rs`).** 6. `each_import_phase_crosses_and_renders_cores_line`.

**C#, the import** (headless sidebar on a pumped STA thread with the dispatcher's context, so the tree is asynchronous as in the app; the shapes of P1a to P1c; a new `T/ImportOutcomeTests.cs`).
7. `ACancelledImportSaysWhatItCopied`: P1a's route through `CancelImportCommand`; `ImportCancelling` said before the import ends; then exactly one `ImportFinished` with `Cancelled` true and `CopiedFiles` equal to the files on disk; after the publication the Files line equals the summary's rendering.
8. `ACancelBeforeTheCopySaysNothingWasImported`: P1b; one `ImportCancelling`, then one `ImportFinished` with every count zero and `Cancelled` true; an empty pick, by contrast, says nothing.
9. `ACompletedImportsSentenceStaysOnTheFilesLine`: P1c; one `ImportFinished`, no `HostComposed`; the line equals its rendering after the publication.
10. `AShutdownOrACloseCancelsWithoutTheCancelSentence`: `BeginSessionShutdownAndCaptureWork` says nothing; the close's admission says its refusal line and no `ImportCancelling`, then the summary when the import settles.
11. `TheCancelIsSpokenOnceAtThePressAndCancelGoesOff`: a held worker; the command's press says `ImportCancelling` once at once, `ImportStatusLine` is the Cancelling line, the command is off; a second press (Escape's route, which executes regardless) says nothing more.
12. `ASecondImportAfterACancelledOneStartsAtImporting`: after a cancelled import settles, a second import's line is "Importing…" and Cancel import is on.
13. `CommandPaletteTests.CancelImportFromThePaletteSpeaksTheCancellation` (#1830's "a fact covers cancelling from the palette"): through the registrar's invoke path, in a partial file of its own.

**C#, the status bar and the keys** (`ShownShell`, under the lock).
14. `TheStatusBarsImportCancelLandsTheKeysWhereTheyCameFromFirst` (`MF`'s witness): the keys from the filter field to the button; `KeysFirstRecorder.AssertTheKeysLeaveFirst` over the press and the settle; the keys back on the field. A twin for keys that came from nothing (the editor), as the trash's has.
15. `StatusBarAndPaneTogglesTests`: the edited row.
16. `AnImportShowsOnTheStatusBarWithItsCancel`: hidden during the pick; shown with "Importing…" and a button named "Cancel import" whose help text is the line, while the copy runs, with the Files pane hidden too; hidden after.
17. `AnImportThatEndsUnderTheKeysLandsThemFirst`: the keys on the status bar's Cancel, the import completes with no press.
18. `TheBarStaysTwentyFourWhileAnImportRuns`, the twin of `TheBarStaysTwentyFourWhileATrashRuns` (`StatusBarAndPaneTogglesTests.cs:123-136`).

**C#, the Files line** (`RescanTests.Harness.WithAsyncTree`, the shapes of P2a to P2e, P3a and P3b; a new `T/FilesStatusLineTests.cs`).
19. `APlainRefreshPutsBackAMutationsLine` (P2c's route: New Canvas through the sidebar and the real listener; the line ends on "Created canvas "Untitled Canvas".").
20. `ARefreshNeverPutsBackALineTheTreeWrote`: after the open's count, a file added and the feed's refresh: the line is empty, not the old count; and after a failed refresh then a successful one, not "Could not load files.".
21. `ACancelledRefreshPutsBackTheLine` (P2e), and `TwoRefreshesInARowPutBackTheLineBeforeTheFirst`.
22. `ALineWrittenWhileTheRefreshRanWins`: a failure detail written between the request and the publication stays.
23. `SidebarTagFilterTests.cs:339` stays green.
24. `APlainRefreshWithASettingsNoticeWritesNoCount`: the notice alone.
25. `AFilterTheCloseCancelledPutsBackItsLine` (P3a, and with an automatic run superseding the user's), and `AFieldOfSpacesEndsTheFilterLineSilently` (P3b).

**Censuses.** `FocusIntegrityManifestCensus` holds with the new and renamed `MF` rows. `A11yTriggerParityCensus` holds after `--write`. `A11yCorpusCensus` holds.

**Mutations** (each turns its fact red; the PR lists each): the rethrow at `:171` restored (7); the zero-count report removed (8); the empty-pick check moved after the cancel check (8); the hold removed from the import's report (9); the shutdown guard removed (10); the press sentence moved to the settle (11); `CanExecute` back to running only (11, 14); the phase reset removed (12); the stranded landing not registered (14, 17); `EndTransientStatus` dropped from `ApplyTreeRefresh` (19), from `CancelTreeRefresh` (21); the tree-wrote check removed (20); the "still shows the text" check removed (22); the first refresh's memory overwritten by the second (21); the notice arm counting again (24); `CancelFilter`'s end limited to user runs (25); the empty-query end removed (25); each `ImportFinished` clause's order, capitalization or grouping changed (1); `TabFocused`'s trim removed (5).

**Regression:** every import, rescan, sidebar, filter and keys-first fact, every census, the whole unit run under the lock before the push (35 to 45 min), and the gate.

## Files

- **Core and FFI:** `C/a11y.rs`; `U/lib.rs`; `tests/fixtures/a11y/corpus.json`.
- **Windows product:** `S/FilesSidebarViewModel.Import.cs`, `.cs`, `.TreeOperations.cs`, `.FileManagement.cs`, `.Filter.cs`; `S/MainWindow.xaml`; `S/MainWindow.Trash.cs` and a new `S/MainWindow.StatusBarCancel.cs`; `S/MainWindow.ShellRegions.cs`.
- **Tests:** a new `T/ImportOutcomeTests.cs` (7 to 12), a new `CommandPaletteTests` partial file (13), `T/FilesPaneKeysFirstTests.cs` (14, 17, and the renamed witness), `T/StatusBarAndPaneTogglesTests.cs` (15, 16, 18), a new `T/FilesStatusLineTests.cs` (19 to 25), `T/Censuses/A11yCorpusCensus.cs`; `M/Tests/SlateMacTests/A11yCorpusCensusTests.swift` (the mirror only).
- **Data and docs:** `MF`; `D`; contract 38 (the amendment and the regenerated ledger).
- New `.cs` files: `git add`, then the license header script. `dotnet format --verify-no-changes --include` after every C# edit.
- About 12 non-test files. None outside the §6 rows CB-16 is named in.

## Overlap

- **CB-34** (Principal T, running): `palette.rs`, two doc comments in `U/lib.rs`, `CommandPaletteViewModel.cs`, its presenter, contracts 28 and 40. CB-16 shares `U/lib.rs` only, and branches after CB-34 merges (the lead's rule). Fact 13 goes in a `CommandPaletteTests` partial file of its own.
- **CB-20b** takes the `a11y.rs` corpus slot after CB-16, and **CB-35** (the filter field), **CB-39 to CB-41** and **CB-18** follow in `FilesSidebarViewModel*.cs`. W9's transient helper is what CB-40's redesign of the publication meets; facts 19 to 25 say what it must keep.
- **Plan 43's queue** (the Mac): #1954's line below.

## NVDA spot check (base main, head the PR; recorded in the PR body)

Expected on head; the order of a focus line and an announcement made in the same turn is the run's to show. The launcher takes `-Root` (#1995).

| Step | Base (main), expected by reading and step 0 | Head, expected |
|---|---|---|
| Import two files to the vault root (File, Import Files and Folders…, Yes) | "Copied 2 files to the vault root." | the same |
| The same, then the Files line read (NVDA's review of the line) | "Loading files…" | "Copied 2 files to the vault root." |
| A folder of about 3,000 files imported; F6 to the status bar, Tab to the item's Cancel | no item | "Cancel import, button", and the description "Importing…" |
| Its Cancel pressed | no item | "Cancelling import…"; the line of the element the keys came from; "Copied N files and 1 folder to the vault root. Import cancelled; completed copies remain." |
| Escape during the same import | nothing | "Cancelling import…", then the summary |
| The palette's Cancel Import during the same import | nothing | the same |
| New Canvas from the palette, then the Files line read | "Loading files…" | "Created canvas "Untitled Canvas"." |

## Ledgers

- **#1954 (Mac work owed):** one line, as §8 has it, made exact: the Mac adopts `ImportFinished` in place of `importBatchAnnouncement` (the same words below 1,000; a count of 1,000 or more gains a comma), posts `ImportCancelling` at `requestCancellation` (new speech, where today the strip only changes its title) and takes its strip titles from `import_status_line`; `not delivered: VoiceOver pass` (Call 5).
- **#1955 (deferred finds):** the import's two failure sentences and the close's import refusal line stay host text with their families (the Files sidebar's failure leads and the close's four refusal lines), for Milestone W's catalogue (CD38); after a close that cancelled a filter is refused, the field shows a query whose results are the previous run's until the next edit or publication (M2).
- **Filed:** the Bases export and Copy as Markdown drop (P4), as above.

## Accepted-risk register

- Core's write takes no token: a cancel during a large file's write is answered at once by "Cancelling import…", and the summary waits for the write (P1d).
- A cancel that lands after the copy finished reports a full copy with no cancel clause (the Mac adds the clause).
- Escape is taken by the import for its whole admission, the cancel's drain included, as on main (`S/MainWindow.xaml.cs:1440-1452`).
- On the Mac's adoption, a count of 1,000 or more gains a comma; VoiceOver is expected to read the same number (not verified).
- A Windows count of 1,000 or more takes core's comma on every culture, where `N0` followed the culture.

## Acceptance

- The red-first facts red on the tests-first commit and green on head; every guard's mutation red; every mutation listed red.
- `gh pr view N --json closingIssuesReferences` names #1830 and #1791 and nothing else.
- The PR body: the NVDA phrases on base and head; the #1954 line; `not delivered: VoiceOver pass`; the local whole run; the filed Bases issue; the accepted-risk register; the last line's count (found, fixed here, deferred, filed).
- Title: `fix(windows): a cancelled import says so in core's words, the status bar shows it running, and the files line never stays on loading`.

## Calls for the lead

1. **What "the import sentences" covers.** Recommended: the summary (C1), the cancel sentence (C2) and the status texts (C3), which are the import rows of the reading's catalogue (§2: the summary, "Import in progress", "Import is idle"). The import's two failure sentences go through `ReportFailure` with the Files sidebar's other failure leads, and the close's import refusal is one of the close's four refusal lines; each stays with its family, on #1955 for Milestone W (CD38). Cost: two variants (240 to 242 of uniffi's 256), one enum and one function over the FFI. The wider reading (the failures too) adds a third variant, `ImportFailed { stage }`, that only Windows constructs.
2. **"Spoken at once": a sentence at the press, and its words, which are the owner's.** Recommended: "Cancelling import…" at the press (C2, W2), then the summary when the import settles. Step 0: a cancel settles 3 ms after it lands between files, but core's write takes no token, so a cancel during a large file's write settles when the write ends (664 ms at 50 MB; about 3.4 s at the 256 MB limit, estimated). The summary alone would meet "at once" only between files. Cost: two sentences for one cancel, as the trash speaks today ("Stopping…", then "Stopped. Moved …"). "Cancelling import…" is new speech whose words this spec chose (the Mac strip's own title for this moment, so both hosts would show and say one string); CD10 settled that the cancel is spoken at once, not its words. Recommended: the lead puts the words to the owner before the tests-first commit, since the corpus, the facts and the NVDA spot pin them.
3. **The Files line: put back what it replaced, except a line the tree wrote** (W9). Recommended: one transient-line helper for "Loading files…" and "Filtering files…", ending on every terminal path, putting the line back only while it still shows the transient text, and leaving the line empty when the remembered line was the tree's own count, overflow, notice or failure, which is stale by then; and the settings notice arm stops writing a count on a plain refresh. This reads CD10's "what it said before, never a count" as: a plain refresh keeps a mutation's line (the reading's reason, a failure detail that stays inspectable) and never writes or brings back a count. Cost: two helpers and seven call sites in the sidebar's partial files; after a feed refresh the line can be empty where main shows "Loading files…" forever. The other reading, a fresh count when the line was the tree's own count, is a count after a plain refresh and so the owner's to choose. The alternative mechanism, writing "Loading files…" only on an empty tree, still sticks on an empty vault and does not cover the filter.
4. **The status bar's Cancel goes off at the press, keys first, to where they came from** (W4, W7). Recommended, as the trash's Cancel does: there is nothing left to press, and CB-17b's landing for that button is the one a reader of the status bar already meets. Cost: the sidebar's Cancel import also goes off at the press now, not at the settle, and CB-17b's witness is renamed. The alternative, the owner's CD11 form for Preview and Expand ("Stay, and speak on a press"), keeps the keys on the button and repeats "Cancelling import…" on each press; it fits a button whose press starts work, less one whose work is to stop.
5. **The Mac half.** Recommended: no Swift product change; the corpus mirror only; one #1954 line and `not delivered: VoiceOver pass`. The smaller honest shape this spec found is in the event's design, not the delivery: `ImportFinished` and `import_status_line` cover every clause and title the Mac has today, so the Mac's adoption needs no second FFI change, and its summary keeps its words below 1,000. Adopting the summary on the Mac now (one call site, `presentImportBatchOutcome`, and the builder deleted) would change no words below 1,000, but it is Swift written without a Mac, run only on the Namespace lane, and the cancel sentence would still be owed with a VoiceOver pass. Cost of the recommendation: the Mac keeps its own summary until plan 43 adopts the event.
6. **One event for both hosts' summaries, with core's grouped counts** (C1). Recommended: one variant with the Mac's three clauses and Windows' limit, rather than a Windows-shaped event that the Mac's adoption would widen through a second FFI change. Cost: three fields Windows always passes as zero, and one (the limit) the Mac never passes; a count of 1,000 or more takes core's comma, on the Mac's adoption and on Windows under every culture.
7. **The finds.** Recommended:
   - The two filter paths: fixed here (W9's filter half). Small: the sidebar's partial files this PR changes.
   - The re-run of a filter the close cancelled: not taken (the red team's M2: it contradicts XD1 and can keep a slow query's close refused); the stale results go on #1955.
   - The Bases export and copy that drop silently (P4): filed. It is not a cancel path, and its fix is in another subsystem (`Bases/`, `WorkspaceViewModel.Bases.cs`), which AGENTS.md classes High. The alternative, taking it as this chunk's own check, adds two files outside the §6 rows CB-16 is named in.
   - `TabFocused`'s leading and double space: fixed here (C4). Small: `C/a11y.rs`, which this PR changes; no spoken word moves.
8. **One PR, two workers.** Recommended: one PR (the events, their Windows speaker and the line fix share the sidebar's partial files and one NVDA spot). After CB-34 merges and the branch is cut: one Opus xhigh worker for the core and FFI half (C1 to C5, F1, the mirrors, the bindings), one for the Files line (W9, facts 19 to 25), which needs no new FFI; the principal writes the import, the status bar and the keys (W1 to W8) once the bindings land. Cost: two workers live at once, as the team rule allows.

## Lead rulings and owner calls (2026-11-24)

1. As recommended: the summary, the cancel sentence and the two status texts are in. The failure lines and the close's refusal stay with their families, with a #1955 line.
2. **Owner, 2026-11-24**, verbatim: "Approve the wording (Recommended)". "Cancelling import…" is said at once on the press, then the summary when the import settles.
3. As recommended, because it follows CD10: the line puts back what it replaced, except a line the tree wrote; a stale count, overflow, notice or failure leaves the line empty; the settings-notice arm stops writing a count on a plain refresh.
4. **Owner, 2026-11-24**, verbatim: "Off, keys go back (Recommended)". Both the status bar's Cancel and the sidebar's Cancel import go off at the press, and the keys first return to where they came from, as the trash's Cancel does. CB-17b's witness at `MF:287` is renamed. This is an owner call of its own, distinct from CD11's "Stay, and speak on a press", which covers buttons whose press starts work.
5. As recommended: no Swift product change; the corpus mirror; a #1954 line; `not delivered: VoiceOver pass`.
6. As recommended: one superset `ImportFinished` with core's grouped counts. The PR body names the comma at 1,000 and above.
7. Finds: both filter paths fixed here, failing first; the filter re-run after a refused close not taken, with a #1955 line; the Bases export and Copy as Markdown drop filed now (bug, windows), carrying the red team's two lows; `TabFocused`'s spaces fixed here; no further fix taken once fixes would more than double the PR's non-test files.
8. As recommended: one PR; at most two Opus xhigh workers at once.

Ordering (relaxed by the lead): the worktree is cut from `origin/main` now and tests-first work starts on everything but `U/lib.rs`, which waits until the lead says CB-34 has merged; then main is merged in and the FFI edit added. Main's run on ad04a470 failed one gate fact (`ReadingDiagram_LineReadingSpeaksTheDescriptionAndThePlaceholder`), which the lead is rerunning as a likely flake: reported, not chased, if met.

## Delivery log (branch `claude/plan-47-cb16-import-events`, cut from ad04a470 on 2026-11-24)

- **Filed:** #2056 (the Bases export and Copy as Markdown silent drop; bug, windows), with P4 as its repro and the red team's two lows.
- **Core half (Worker A, Opus xhigh):** 9517b2f4 (tests first: 6 a11y facts red on their words, none a compile error) and d708c055 (the renders; 54 a11y facts green). `ImportPhase` and `import_status_line` are reached as `slate_core::a11y::…`, as `RunningWork` is. Mutations: each clause's capitalization, the grouping (all counts, and the limit alone), the else-if, the slash trim, the TabFocused trim, the limit's place and the `ImportCancelling` witness each turn a fact red; `ImportCancelling` as a literal with the same words is caught by no behavioural test (only a source scrape could). Gates: fmt, clippy -D warnings, the whole `cargo test -p slate-core` (2,751 passed). `cargo test -p slate-uniffi`: 107 passed, 2 failed, both the expected tripwires until the FFI lands (the C# mirror's count, and the FFI mirror's missing variants). Where the hosts differ below 1,000: nowhere for any input either produces. No C# fact pins a `TabFocused` rendering.
- **Files line (Worker B, Opus xhigh):** def5fbf8 (tests first: 8 red, the 2 guards green) and b305199c (W9: `ShowTransientStatus`, `EndTransientStatus`, a `_statusWrittenByTree` mark set only through `WriteTreeStatus` and cleared by every other write; the notice arm; the filter's two ends). Every mutation W9 lists turned its fact red. Regression filter (Sidebar, FileManagement, FilesSidebar, Rescan, W1 hardening, W1 child expansion, Files pane, Files region, the tag filter, the announcement etiquette, right-pane hide, the status bar toggles, the censuses): 2,264 passed, 2 failed, both the C# corpus mirror's count, which waits for the FFI. Two notes, by reading, neither changed: a refused admission's end has no fact (it is unreachable in production: shutdown's `CancelTreeRefresh` ends the line first), and a rescan whose own token is cancelled leaves "Loading files…" until the next refresh (only teardown cancels that token, and teardown's `CancelTreeRefresh` ends the line).
- **The import, the status bar and the keys (the principal):** written on the branch and held until the lead's word that CB-34 had merged (fce6a6e1); main merged in (37f9a543, no rebase), the FFI patch applied cleanly.
  - c2b51369, tests first: the FFI, the C# corpus mirror and facts 7 to 18, on a scaffold that names `IsImportCancelling`, `ImportStatusLine` and the copy hook with main's behaviour. 12 red, each for its own reason (no summary, no cancel sentence, "Import in progress" where core's line was due, no status bar item); the renamed CB-17b witness and the corpus census green. `cargo test -p slate-uniffi`: 110 passed.
  - 04df8e8c: `ACancelDuringThePickReportsOnlyAPickThatChoseSources`, red on the scaffold (a pick that chose sources after a close's cancel said nothing).
  - d9a6db1f, the fix (W1 to W8). Green: 1,693 facts (the import, the palette, the Files pane keys, the status bar, the Files line and every census). Two census finds on the way: the W-C automation id inventory needed the two new ids, and the test-wait census refused a `Task.Delay` in a worker (now waits on its token).
  - The diff red team (Opus xhigh, at d9a6db1f): no blocker or major; 3 minors, 5 lows. e2dcafc6 takes all three minors (a tag tree's failure and an expansion's overflow line are the tree's lines; the summary is said after the keys leave a Cancel that goes with the import) and lows 4, 5 and 7 as facts; low 6 is filed as #2059; low 8 goes on #1955. A held line keeps its tree mark through its reassertion (the weakness Worker B named).
  - Mutations (one batch, under the lock): all 15 turn their facts red (the log is `mutations.log` in the principal's notes).
  - Changes from the design: the summary is announced in `ImportAsync`'s `finally`, after the flags clear (red team finding 3); expansion and dual-pane overflow lines are the tree's own (finding 2), where the spec had them as not the tree's.
  - Codex round 1 (at e2dcafc6): two mediums, both taken in d4105c8e, failing first (a folder expansion's failure and the dual pane's failure came back after a refresh; the status bar's item hid while its Cancel still held the keys).
  - Codex round 2 (at d4105c8e): three findings. Two taken in b0a8c62a, failing first (the status bar's Cancel could land the keys on the pane's Cancel as both went off; a held read failure lost its tree mark). One declined with the lead's agreement and filed as #2060: an import whose copy landed but whose index write failed counts the copy as not imported. It is on main, and its fix needs an FFI change.
  - Codex round 3 (at b0a8c62a), given the decline and its reason: approve.
  - Mutations M16 to M20 (the round 1 and 2 fixes) run under the lock: all red. All 20 turn their facts red.
  - The whole Windows unit project at b0a8c62a under the lock: 9,056 passed, 0 failed, 2 skipped (environmental), 78 minutes.
  - Filed #2059 (F6 to the status bar does not say that an import or a trash is running).
  - NVDA spot 1 (base fce6a6e1, head b0a8c62a): head matched every expected phrase. After a press of the status bar's Cancel, "Cancelling import…" (Medium) queued behind the focus line of where the keys landed (the status bar, with its long description) and was heard about 11 s after the press.
  - The lead's ruling of 2026-11-25 ("take B"): `ImportCancelling` is High, as `WorkStillRunning` is, so the press is answered at once and the focus line it moved is cut. 21d2d902, failing first (the priority fact red at Medium). A post-approval commit: no Codex round; the merge judge reads it.
  - NVDA spot 2 (21d2d902's code): "Cancelling import…" reaches the synth 82 ms after a press of the status bar's Cancel, 25 ms after Escape and 86 ms after the Files pane's Cancel; the focus line the press moved is cut in full. An import that ended with no press and the keys on the status bar's Cancel had its summary heard 11.40 s after the raise, behind the status bar's focus line: the keys moved first (finding 3's order). NVDA never removed a notification on an in-window focus change; only stale focus speech, marked Cancellable, goes.
  - 2473ef42: the summary is raised before the flags clear, as the trash's is (finding 3 reversed). The fact went red on the old order; the import, keys, status bar, Files line, palette, dispatcher and census classes passed 1,977 of 1,977. A post-approval commit: the merge judge reads it. The lead kept it (2026-11-25): a defect in this PR's own feature.
  - NVDA spot 3 (2473ef42's code): with no press and the keys on the status bar's Cancel, the summary reached the synth 36 ms after its raise, then the status bar's region line in full; from the Files pane's Cancel import, 41 ms, then "File actions, grouping, expanded" in full; the status bar press unchanged ("Cancelling import…" 86 ms after the key, the region line cut). Narrator not run; the trash's end not run under NVDA.
  - PR #2061 opened at 2473ef42 (closingIssuesReferences: #1791, #1830). Comments posted on #1954 (the Mac's adoption) and #1955 (the deferred finds, with the Files pane's Cancel import lacking an AutomationId, the lead's item 3).
  - The merge judge (Opus xhigh, at 2473ef42): MERGE AFTER FIXES. No blocker or major; minors m1 to m3 and nits n1 to n4. Taken as the lead ruled, in d4d2bdb6, each failing first:
    - m1: the flag clears are in a nested finally after the summary's announcement. The fact was red on 2473ef42.
    - n4: only a running import's cancel is said. The #1272 Escape-during-the-pick cancel is kept and is silent now. The fact was red on both picks.
    - n2: the unreachable reset as the next import starts copying is removed. Removing the end's clear turns the second-import fact red.
    - n3: the status bar fact is renamed.
    - n1: the FFI doc comment is corrected.
    - Verification: 2,017 Windows facts passed (the affected classes, the censuses, and the Escape-during-an-import classes); a11y 54 and uniffi 110 passed; fmt, `dotnet format` and the ledger are clean. m3 (the trash's press at Medium) is filed as #2062.
    - m2: a Narrator spot check on d4d2bdb6.
  - The Narrator spot (d4d2bdb6), measured with a WASAPI loopback level meter against calibrated durations:
    - Narrator cuts the summary when the import ends with no press under the keys, from either Cancel (filed #2063, with the trash). The press case is heard in full.
    - It also said "Status bar disabled." The lead ruled that a defect in this PR's feature.
  - The trace, from an in-process UIA client:
    - "IsEnabled True->False" was reported on the status bar, whose own IsEnabled never changed. The event is the hidden Cancel's: WPF raises a peer's IsEnabled change at Background priority, after the item hid, and UIA hands a hidden element's event to its nearest shown ancestor. The trash does the same.
    - Landing first and hiding first cannot reach it.
    - An in-process client fact hung on its second case, a managed client's event thread after a window closes, so the fact pins the peer's answer instead.
  - f4ab7c67: `StatusBarCancelButton`, whose peer keeps the enabled state it last showed while hidden. The fact was red on both cases. 2,115 passed after a stuck left Alt, left by the Narrator driver, was released and its 31 key facts rerun.
  - Re-check on f4ab7c67 (one take each):
    - Narrator: "Status bar disabled." is gone. The meter shows only "Status bar," after the end (0.72 s); the summary is still cut (#2063).
    - NVDA: the summary is heard 35 ms after its raise, with the region line after it in full. The log has no IsEnabled event, and no property event at all, in that run.
  - **Spec claims that were wrong:** P1a's tree claim (P1e corrected it before the build); expansion and dual-pane overflow lines are the tree's, not the line's; the summary is announced after the flags clear, not inside the report; the cancel sentence at Medium is not "at once" when the press moves the keys; finding 3's premise, that a focus change raised after the summary would cut it short, is false for NVDA (the order it chose made the summary wait instead).

## Red team record

A fresh Opus xhigh agent read the first draft against `origin/main` ad04a470, by reading (docs/plans/24_red_team_protocol.md): no blocker, 3 majors, 8 minors, 10 lows. It confirmed the drift lines it spot-checked, that C1 reproduces both hosts' sentences below 1,000 apart from one rule, the ledger mechanics, the zero-count route, Escape's bypass of `CanExecute`, `CommandKeysFirst`'s reach, and the absence of Swift switches that a new variant breaks. Every finding is taken but two lows, which go with the filed Bases issue.

- **M1, W8 put back lines the tree wrote (taken).** An old count or "Could not load files." came back after the tree moved. The helper now leaves the line empty when the remembered line is the tree's own (W9), with fact 20; Call 3 says which reading of CD10 this is. Folding it found the settings notice arm writing a count on every plain refresh (drift 5), now in W9 with fact 24.
- **M2, the filter re-run contradicted XD1 (taken).** Dropped; the stale results go on #1955.
- **M3, nothing reset the import phase (taken).** W3, fact 12, its mutation.
- **m4 (taken).** The memory's lifetime is stated; `CancelFilter` ends the line whenever it still shows the filter text, an automatic run's included.
- **m5 (taken).** Red-first facts and guards are listed apart, each guard with its mutation.
- **m6 (taken, and run).** P1a's bare sidebar could not show the feed. P1e ran P1a through the lifecycle: the feed shows the copies 800 ms later. The tree claim is gone from the goal, the table and fact 7.
- **m7 (taken).** The Cancel's help text carries the line; the NVDA row says what Tab hears.
- **m8 (taken).** Call 2 names the words as the owner's and drops the alternative that fails "at once".
- **m9 (taken).** The Bases drop is not a cancel path and crosses subsystems: filed (Call 7).
- **m10 (taken).** `TabFocused` trims its prefix (C4), with both witnesses.
- **m11 (taken).** The first-clause capitalization is stated, with moved-only and found-only witnesses; Call 6 names Windows' culture and counts three fields.
- **Lows (taken, eight).** The empty pick first (W1); the cancel after the copy (risk register); the whitespace end is silent; the status bar's height fact, column renumbering, hide-before-off order and per-button state (W6, W7, fact 18); CB-17b's witness renamed with `MF:287`; the Mac Bases path; `D`'s fields; the risk register and the count line. **Not taken:** the W9 refusal-callback timing and Copy's wording go with the filed Bases issue rather than this PR.
- **Not verified by the red team or this spec:** NVDA's reading of the button's description; VoiceOver's reading of a grouped count; the census's naming of the two new `MF` rows (by analogy with the trash's).

## What delivery changed

Merged as #2061 = b19c94ae (2026-11-25; 34 checks passed, 4 skipped; one shell-gate rerun for the known `ReadingDiagram_LineReadingSpeaksTheDescriptionAndThePlaceholder` flake). Against this spec as written:

- **`ImportCancelling` is High, not Medium.** At Medium, NVDA heard it 11.5 s after a press of the status bar's Cancel, queued behind the focus line the press moved the keys to (the status bar's region help). The lead ruled it High, the tier of `WorkStillRunning` (21d2d902). The trade is CB-17b's: that focus line is cut, so after a press nothing says where the keys went.
- **The summary is said before the flags clear.** The diff red team's finding 3 (the keys first, then the summary) rested on a premise NVDA disproves: an in-window focus change drops only stale focus speech, never a notification. Its order made NVDA hear a no-press summary 11.4 s late. 2473ef42 reverses it, as the trash's order is. Narrator cuts that summary either way it is ordered, and the cut stays filed as #2063 with the trash.
- **The clears sit in a nested finally**, so an import ends even when its summary's announcement throws (merge judge m1).
- **Escape during the pick cancels silently.** #1272's route is kept, but only a running import's cancel says "Cancelling import…" (merge judge n4).
- **One cancel reset, the end's.** The start's reset was unreachable (merge judge n2).
- **Both status bar Cancels are `StatusBarCancelButton`.** A hidden Cancel's IsEnabled change was handed by UI Automation to the status bar the keys were on, and Narrator said "Status bar disabled." Its peer now keeps the answer it last gave while shown (f4ab7c67). This fixes the trash's Cancel too.
- **Expansion and dual-pane overflow lines are the tree's own,** and a held read failure keeps its tree mark (red team and Codex).
- **Filed:** #2056, #2059, #2060, #2062, #2063. **Deferred to #1955:** the filter re-run after a refused close; a field of spaces after a finished filter; also noted there, the Files pane's Cancel import having no AutomationId.
- **Count:** found 23, fixed here 16, deferred 2, filed 5.
- **Not verified:** a VoiceOver pass (#1954); Narrator beyond the three no-press and press cases; the trash's end under NVDA or Narrator (its fix is pinned by the unit fact only); the comma at 1,000 and above under VoiceOver.
