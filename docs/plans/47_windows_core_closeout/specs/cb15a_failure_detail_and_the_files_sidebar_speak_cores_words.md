# CB-15a: `FailureDetail`, the shell census, and the Files sidebar speaks core's words

Issue: #1890, referenced only (CB-15b and CB-15c continue it; the PR writes "Refs #1890" and keeps every close, fix or resolve word away from that number, in the PR text and in every commit message). Size M. Lanes: windows (the app job; hosted facts and a census). AT: an NVDA spot check. Wave: C7, slot 1. Depends on: nothing. Parallel with: CB-24 (`NoteHeaderView.cs`, `MainWindow.AppMenu.cs`, `MainWindow.FilesToolbar.cs`, `MainWindow.RailMore.cs`, three gate partial files) and CB-28 (`EditorInteractions.cs`, `diagram.rs`, `Reading/`, `Panels/RightPanePanelsViewModel.cs`, one `lib.rs` doc comment). Decision: CD9, as the owner answered it on 2026-11-16 (below). Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9; product code changes).

**CD9 as answered (§3.1, 2026-11-16).** "The four search and graph error arms are not kept: the host says core's sentences. The census part is as recommended: rule 1 over the whole shell, and rule 2 only if a trial run shows few hits." The arms themselves are CB-15b's (plan §5): this chunk does not touch `SearchOverlayViewModel.cs` or `GraphDocumentViewModel.cs`. What the answer means here: (1) the census's first rule covers every shell source from this PR on; (2) the arms get no standing exception, only the dated rows rule 4 gives every remaining site, owned by CB-15b; (3) the second rule's trial was run (step 0) and its result decides its scope (Call 1).

*2026-11-18, the plan lead's rulings on this spec's calls:* (1) yes, rule 2 stays on Bases: the trial did not show few hits, which is CD9's condition; (2) yes, the tag edit's read moves into this chunk; (3) Find 1 (another connection's commit during a listing fails the tree refresh and the dual pane) is filed, labels bug, rust, windows; for Find 3 (core's developer-text details) the principal first checks whether #1527, CB-15b, CB-15c or CB-46's reach list already covers it and comments there instead of filing if one does (the owner wants as few new issues as possible); (4) the lead passes the census constraint to principal P; (5) Copy link is fixed here with the Mac's sentence, which is parity, not a new product sentence: a #1956 line, its facts red first, its NVDA phrase on base and head.

Written 2026-11-18 from the plan's rows (§2.3's #1890 row, §3 CD9 and §3.1, §4 rules 1 to 4, §5 CB-15a to CB-15c, §6, §9), `reading/05_spoken_failures.md:13-46` and `:205-346`, and #1890 with its comment. Every file and line below was read at `main` 26d353ef. Step 0 ran on a Release build of that commit. A fresh red team read the first draft (0 blockers, 7 medium, 23 low); every finding is folded in, and "Red team record" says how. Each statement that is still by reading says so.

`S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`.

## Goal

1. One shell-wide helper, `FailureDetail.Of(Exception)`, says a caught failure's detail: core's `VaultErrorDetail` for a `VaultException`, the exception's own message for anything else. `BasePhrase.ErrorDetail` forwards to it, so the census allows one reader of an exception's `Message` in the shell.
2. A census, `ShellFailureDetailCensus`, binds every shell source and refuses any read of an exception's `Message` outside `FailureDetail.Of`, identified by symbol. Today's reads outside the Files sidebar stand as dated exceptions owned by CB-15b and CB-15c (rule 4), each row naming its file, type, member and count, and a row that no longer matches fails.
3. Every Files sidebar failure sentence built from a caught failure says core's words, never the binding's field dump (`@message=…`, `@path=…, @reason=…`). That covers the sidebar's nineteen vault sites, its three settings-store sites (unchanged text, now through the helper), and the tag edit's failure text, which the sidebar publishes but the workspace composes.
4. Found on the way and fixed here: Copy link on a file core makes no wikilink for (any non-Markdown file, among others) says why, in the Mac's sentence, where it now says nothing.

## Current state on main

**The dump.** The C# generator's error classes build `Message` from the variant's fields: `VaultException.Io` is `"@message" + "=" + message`, `InvalidPath` is `"@path=…, @reason=…"` (`apps/slate-windows/src/SlateUniffi/generated/slate_uniffi.cs:43027-43075` in a built tree, uniffi 0.32.2 with #1937's generator tag). `CommandException` (`:37437`), `GraphConfigException` (`:39188`) and `HotExitException` (`:40129`) are built the same way; every one derives from `UniffiException : System.Exception` (`:219`). The reading's fact, made at 1ec9bf31 before #1937, still holds.

**Core's renderer.** `vault_error_detail`, `crates/slate-core/src/a11y.rs:3612-3651`, exported as `SlateUniffiMethods.VaultErrorDetail`. Some variants give a whole sentence ("Something named {path} already exists there."), some a bare message with no final period of its own (`Io`, `Db`, `Trash`, `InvalidQuery`, `InvalidArgument`, `TrashConfirmationChanged`, `StructuralMutationIncomplete`), and `InvalidPath` gives "Invalid path {path}: {reason}" with none either. No lead and no core renderer around these sites adds a period after the detail, so no sentence doubles one (`CanvasActionFailed` "{verb} failed: {detail}", `a11y.rs:3265-3267`; the canvas's `NoteCreateFailed` "Could not create {path}: {message}", `:3246-3248`; the graph's "Couldn't create note: {message}", `:4215-4217`).

**The Bases helper.** `BasePhrase.ErrorDetail`, `S/Bases/BaseDocumentViewModel.cs:1823-1827`: `failure is VaultException vault ? SlateUniffiMethods.VaultErrorDetail(vault) : failure.Message`. 27 call sites, in `S/Bases/BaseDocumentViewModel.cs`, `BaseQueryBuilderViewModel.cs`, `DashboardViewModel.cs` and `S/WorkspaceViewModel.Bases.cs`. `T/Censuses/BasesErrorDetailCensus.cs` (1,034 lines) binds the Bases sources through `T/Support/ShellCompilation.cs` and applies two rules (`Violations`, `:601-627`): rule 1, no read of `Message` on a type deriving from `System.Exception` outside the helper (`InTheHelper`, `:629-634`, which matches by syntax: a method named `ErrorDetail` in a class named `BasePhrase`, any namespace, any overload); rule 2, an exception value flows on only as an exception (`AllowedUse`, `:735-791`; `IsConsumer`, `:930-952`, which trusts `VaultErrorDetail` by name and a method only when the census reads its body). Every planted `Message` shape in `TheCensusNamesEveryWayAFailureBecomesText` (`:84-165`) is also named by rule 2, so no planted case proves rule 1 alone. `OnlyTheHelperReadsAnExceptionsMessage` (`:236-259`) plants `BasePhrase.ErrorDetail` as the clean reader.

**The Files sidebar's sites** (`S/`):

| File:line | Catch | Sentence lead | Reaches |
|---|---|---|---|
| `FilesSidebarViewModel.cs:1540` | `VaultException` | "Could not load tags: " | `TagLoadOutcome`, Status; spoken unless the refresh is silent |
| `:1801` | `VaultException` | "Could not create folder {name}: " | `ReportFailure` (Status, `HostComposed` High, `:2866-2873`) |
| `:1881` | `VaultException` | "Could not create note {name}: " | `ReportFailure` |
| `:1967` | `VaultException` | "{name}: " inside `CanvasCreateFailure` | core's `CanvasActionFailed(NewCanvas)` detail, through `ReportFailure(A11yEvent)` (`:1982`) |
| `:2058` | `VaultException` | "Delete failed: " | `ReportFailure` unless shutting down |
| `:2099` | `VaultException` | "Could not create folder note: " | `ReportFailure` |
| `:2131` | `VaultException` | "Could not delete folder note: " | `ReportFailure` |
| `:2160` | `VaultException` | "Could not copy wikilink: " | `ReportFailure` |
| `:2385` | `VaultException` | "Move failed: " | `ReportFailure` |
| `:2453` | `VaultException` | "Trash failed: " | `ReportFailure` unless shutting down |
| `:2626` | `VaultException` | "Could not load folder files: " | `ReportFailure` |
| `:2834`, `:2901`, `:2931` | `IOException`, `UnauthorizedAccessException`, `InvalidOperationException` | "Could not save sidebar organization: ", "… pins: ", "… shortcuts: " | `ReportFailure` or the replay's reporter; ordinary text, stays |
| `FilesSidebarViewModel.FileManagement.cs:1183` | `VaultException` | "Move failed: " | `ReportFailure` |
| `…FileManagement.cs:1213` | `VaultException` | "Could not create folder {path}: " | `ReportFailure` |
| `…FileManagement.cs:1318` | `VaultException` | "Could not duplicate {name}: " | `ReportFailure` |
| `FilesSidebarViewModel.TreeOperations.cs:309` | `VaultException` | "Could not load files: " | `ReportTreeFailure`; the synchronous refresh, which runs only with no dispatcher context; the app always passes one (`S/VaultLifecycleViewModel.cs:262`), so it runs in tests only |
| `…TreeOperations.cs:456` | `VaultException` | "Could not load files: " | `ReportTreeRefreshFailureAsync`; the app's path; spoken unless silent |
| `FilesSidebarViewModel.CanvasNotes.cs:41` | `VaultException` | none: `CanvasNoteCreateResult.Failed(message)` | the canvas's `NoteCreateFailed(path, detail)` (`S/Canvas/CanvasDocumentViewModel.cs:4152-4154`) |
| `FilesSidebarViewModel.SurfaceNotes.cs:42` | `VaultException.DestinationExists` | none: `NoteCreateResult.Exists(message)` | the graph's `GraphBlocked(NoteCreateFailed(reason))` (`S/WorkspaceViewModel.GraphCreate.cs:127-135`) |
| `…SurfaceNotes.cs:46` | `VaultException` | none: `NoteCreateResult.Failed(message)` | the same (`:136-139`) |
| `WorkspaceViewModel.Properties.cs:1179`, in `EditTagAtItsTurn` (`:1153`) | `Exception`, but not the fatal three | the sidebar's "Tag edit failed: " (`FilesSidebarViewModel.cs:1738`) | `failure` handed to `PublishTagEdit`; CB-9b moved the read here (#2001) |

The plan's "21 sites" were the reading's: these nineteen vault reads, plus the rename (`FileManagement.cs:289`, which CB-13 moved onto `VaultErrorDetail`, #1985) and the tag edit (whose read CB-9b moved to the workspace). No site's text is used as data: `TagLoadOutcome.Error` is tested only for null, and the canvas and graph results are only relayed. By reading, none of these calls can raise `SavedButUnindexed` (plan §9's row): core raises it only from `save_text_locked_checked` and `canvas_apply` (`crates/slate-core/src/session.rs:4889`, `:5247-5302`, `:22994`, `:23033`), and the creates report `PublishedUnindexed` instead.

**Copy link.** `CopyWikilinkCommand` is available on any file row (`FilesSidebarViewModel.cs:637`), as the Files toolbar's "Copy link" button (`S/MainWindow.xaml:1358`) and a chord (`S/Commands/SlateCommandRegistrar.cs:694`). Core's `wikilink_for_path` (`crates/slate-core/src/session.rs:7023-7049`) returns no link for a non-Markdown file, a path whose file is gone, or a target its formatter refuses (a case twin). On no link, `CopyWikilink` copies nothing and says nothing (`FilesSidebarViewModel.cs:2142-2156`). The Mac says "Could not create a wikilink for this file." in that case (`apps/slate-mac/Sources/SlateMac/AppState.swift:1863-1864`, `:2508-2511`).

**Tests that pin these sentences.** Whole text: `T/SaveChainAdmissionTests.cs:5325-5326` ("Tag edit failed: the tag edit failed (test seam)", the non-vault arm, unchanged by this chunk); `T/W1HardeningTests.cs:1266` ("Could not load files.", the non-vault arm at `TreeOperations.cs:469-474`, unchanged). Prefixes only: `T/SidebarTagFilterTests.cs:472`, `T/RescanTests.cs:1301`, `:1792`, `T/RescanTests.Round4.cs:215-216`, `T/GraphEndToEndTests.cs:646-647`, `T/SidebarSettingsRetryTests.cs:139`, `:189`, `:315`, `:394`, `T/SidebarWriteJournalTests.cs:265`, and `T/SaveChainAdmissionTests.cs:5489` (no "Tag edit failed" after teardown). No test pins a create, delete, trash, move, duplicate, copy wikilink, folder-note or folder-files sentence.

### Step 0, as run (2026-11-18, main 26d353ef, Release)

**The census trial.** A probe (uncommitted, `T/Censuses/Cb15aStep0Probe.cs`) ran `BasesErrorDetailCensus.Violations` over all 309 shell sources with every one censused, and wrote each violation (the trial's output was kept in the principal's working folder, outside the repository):

- **Rule 1: 96 reads** of an exception's `Message`, in 32 files. 23 are this chunk's (22 in the sidebar's files, one in `WorkspaceViewModel.Properties.cs`). Of the other 73, 26 are in `Canvas/`, `Graph/` and `Search/` (CB-15b: `CanvasDocumentViewModel.cs` 15, `CanvasMutationFunnel.cs` 1, `ConnectionsLeafViewModel.cs` 3, `GraphConfigStore.cs` 3, `GraphDocumentViewModel.cs` 3, `SearchOverlayViewModel.cs` 1) and 47 elsewhere (CB-15c). The reading's reads outside the helper were 90 at 1ec9bf31 (its 68 + 19 + 2 counted `GraphConfigStore.cs:171-172` as one site of two reads); the net change since is +6 (`WorkspaceViewModel.History.cs` +4, `TasksReviewViewModel.cs` +1, `WorkspaceViewModel.Properties.cs` +2, `BulkRenameViewModel.cs` +1, the sidebar -2).
- **Rule 2: 172 hits, of which 96 are rule 1's own reads seen again** (the receiver of each `.Message`). **76 are rule 2's alone,** in 29 files. By reading every one of them, none puts a binding dump into text: 25 read a uniffi exception's typed fields, which is the right way to read them (`io.message`, `failed.message`, `held.@reason`, `exception.path`, `unindexed.newContentHash`); the rest store an exception in a field or a record (`_integrityFailure`, `LastSaveError`, `RecentsRead.Unreadable`, `GraphDiagramRefresh`), hand it to a delegate or a completion (`completion(report, failure, …)`, `fault(exception)`), rethrow or capture it (`ExceptionDispatchInfo`, `Task.FromException`), wrap it as an inner exception (`new IOException("…", exception)`, `AggregateException`), or pass it to `HostLog.Write` and `ThrowIfNull`. Three of the 76 are in the sidebar's files (`FileManagement.cs:545`, `:547`, typed fields; `FilesSidebarViewModel.Trash.cs:155`, `Task.FromException`).
- **The shell does not bind cleanly in the census's compilation:** 3 errors, all `CS0104: 'Path' is an ambiguous reference between 'System.Windows.Shapes.Path' and 'System.IO.Path'`, at `S/LeafIcon.cs:63` and `S/Reading/ReadingMathElement.cs:118`, `:128`. `ShellCompilation.Build` adds a synthetic global-usings tree with `System.IO` and `System.Net.Http` (`T/Support/ShellCompilation.cs:67-72`) beside every `*.g.cs` under `S/obj` (`:77-93`), which already includes the app's own `SlateWindows.GlobalUsings.g.cs` (System, System.Collections.Generic, System.Linq, System.Threading, System.Threading.Tasks) and its `_wpftmp` copy. The two extra names cause the three errors. `ShellPromptOwnerCensus` (`T/Censuses/ShellPromptOwnerCensus.cs:59-79`, `:141-153`) already binds the whole shell with no error from the app's generated usings alone. No census's planted source relies on either name as a global using (`GraphConfigMutationCensusTests` writes its own `using System.IO;`). `BasesErrorDetailCensus` checks only the Bases sources, so nothing noticed.

**What each sidebar site says.** A worker's probe (uncommitted, `T/Cb15aStep0SitesProbe.cs`, 21 facts, passed twice; its outputs were kept outside the repository) drove a real core refusal at all nineteen vault sites (`:2160` through a constructed row) and all three settings sites, and for each called the same core API directly and passed its `VaultException` to `VaultErrorDetail`. Every recorded sentence (the Status, or for `CanvasNotes.cs:41` and `SurfaceNotes.cs:42`, `:46` the result the canvas or graph speaks) contains that exception's `Message` verbatim. Examples (Status and the spoken `HostComposed` High are the same text unless noted):

| Site | Driver | On main | With core's detail |
|---|---|---|---|
| `:1801` create folder | the selected folder `sub` replaced on disk by a file | Could not create folder Untitled Folder: @path=sub/Untitled Folder, @reason="sub" is not a folder, so no folder was created | Could not create folder Untitled Folder: Invalid path sub/Untitled Folder: "sub" is not a folder, so no folder was created |
| `:2099` create folder note | `docs/docs.md` written after the scan | Could not create folder note: @path=docs/docs.md | Could not create folder note: Something named docs/docs.md already exists there. |
| `:2058` delete | the existing `TrashStagedForTesting` seam removes the file after staging | Delete failed: @message=Files changed or Trash confirmation expired. Request deletion again | Delete failed: Files changed or Trash confirmation expired. Request deletion again |
| `FileManagement.cs:1183` move | Move To `sub`, which already holds `a.md` | Move failed: @path=sub/a.md | Move failed: Something named sub/a.md already exists there. |
| `FileManagement.cs:1213` | Move To's new folder `blocker.md`, a note's name | Could not create folder blocker.md: @path=blocker.md | Could not create folder blocker.md: Something named blocker.md already exists there. |
| `FileManagement.cs:1318` duplicate | the file removed on disk | Could not duplicate a.md: @message=The system cannot find the file specified. (os error 2) | Could not duplicate a.md: The system cannot find the file specified. (os error 2) |
| `SurfaceNotes.cs:42` | `TryCreateNote("Idea.md")` over an existing note | (the graph would say) Couldn't create note: @path=Idea.md | Couldn't create note: Something named Idea.md already exists there. |
| `WorkspaceViewModel.Properties.cs:1179` tag edit | tag "#" through the workspace | Tag edit failed: @message="#" is not a valid tag. | Tag edit failed: "#" is not a valid tag. |

The other drivers: `:1540` tag load through `TagTreePageForTests`; `:1881` and `:1967` by the replaced folder; `:2131` by the folder note removed on disk; `:2385` batch move through core's `SLATE_TEST_FAULT_PLANT_MARKERS` (then, with no seam, the interrupted batch's own refusal); `:2453` batch trash through `TrashStagedForTesting`; `:2626`, `TreeOperations.cs:309` and `:456` by a second session committing during the listing (a race, 2 to 4 attempts); `CanvasNotes.cs:41` and `SurfaceNotes.cs:46` by `TryCreateNote` with a path core refuses (`Notes\Card.md`, `.drafts/Idea.md`). `:2160` is reachable only with a row the tree never holds (core refuses only a malformed path, and a tree row's path is always well formed); the probe used a constructed row `Notes\Card.md`. For `:2626` the worker designed, and did not run, a constructed directory row core refuses (`../outside`, selected so its setter calls `LoadDualPane`, `FilesSidebarViewModel.cs:805`).

What step 0 settles:

1. The class is real at every sidebar site on today's binding; the head sentence is the lead plus core's detail.
2. Rule 1 over the whole shell has 96 reads; rule 2 has 76 more, none a dump reaching text. "Few" is a call (Call 1).
3. The census compilation must drop its two extra global usings before a whole-shell bind check can pass.

Found, and sized in "Finds": the tree refresh and the dual pane fail on any other connection's commit during a listing; Copy link says nothing for a file core makes no wikilink for; several of core's details are developer text. Two observations that are not defects: the organization save's failure (`:2834`) is spoken at High and the status line then settles to the count and "Sidebar settings are malformed and are read-only.", which names the standing cause; and a link target written with a backslash (`[[Notes\Card]]`) mints a ghost path core's create refuses, which core's own path rules intend (it now says why in core's words).

## Design

### 1. `S/FailureDetail.cs` (new)

```csharp
/// <summary>#1890: the detail a failure sentence speaks: core's rendering
/// for a vault error, never the binding's field dump ("@message=..."), and
/// any other exception's own message. The only reader of an exception's
/// Message the shell census allows (ShellFailureDetailCensus).</summary>
/// <remarks>Limits: CommandException, GraphConfigException and
/// HotExitException dump their fields too, and core renders none of them;
/// each is read by its typed fields where it is spoken. An AggregateException
/// that holds a vault error embeds that error's dump in its own Message.
/// Neither reaches a caller of this helper on today's code.</remarks>
internal static class FailureDetail
{
    internal static string Of(Exception failure) =>
        failure is VaultException vault ? SlateUniffiMethods.VaultErrorDetail(vault) : failure.Message;
}
```

The other three uniffi error types are read by their typed fields where they are spoken today (`CommandPaletteViewModel.cs:1040-1060`, `HotExitStoreHost.cs:132-151`), except `GraphConfigStore.Reason` (`S/Graph/GraphConfigStore.cs:167-173`), which CB-15b rewrites onto the typed fields. `S/Commands/SlateCommandRegistrar.cs:161-170` wraps any other exception's `Message` in `CommandException.ActionFailed`, which the palette reads by its typed field; that read is one of CB-15c's rows. An aggregate is built at `TasksReviewViewModel.cs:883`, `:891`, behind CB-15c's broad catches. No sidebar site and not the tag edit can receive any of these.

### 2. `BasePhrase.ErrorDetail` forwards

`S/Bases/BaseDocumentViewModel.cs:1826-1827` becomes `=> FailureDetail.Of(failure);`. Its 27 callers do not change. `BasesErrorDetailCensus` changes:

- `InTheHelper` identifies the helper by symbol: the method `SlateWindows.FailureDetail.Of(System.Exception)` from the compilation, the one non-generic static method of that name and signature, compared with `SymbolEqualityComparer`. It no longer names `BasePhrase.ErrorDetail`.
- `IsConsumer` trusts `FailureDetail.Of` by the same symbol, as it trusts `VaultErrorDetail` by name, so `BasePhrase.ErrorDetail`'s hand-off passes rule 2. `FailureDetail.cs` is not added to the censused set, so no other member a later change puts in that file becomes a trusted consumer.
- `OnlyTheHelperReadsAnExceptionsMessage` binds against the real helper: `BasePhrase.ErrorDetail` reading `Message` itself is named; a planted class named `FailureDetail` in another namespace with a method `Of` that reads `Message` is named; a planted `Of(Exception, string)` overload in `SlateWindows.FailureDetail`'s namespace is named.
- Its header and violation text name `FailureDetail.Of` as the reader.

### 3. The sidebar's sites

Each read in the table above becomes `FailureDetail.Of(exception)`: the nineteen vault sites, the three settings sites (same text), and `EditTagAtItsTurn`'s `failure = FailureDetail.Of(exception)` (Call 2), so a vault refusal says core's words and the test seam's `InvalidOperationException` keeps its message, as `SaveChainAdmissionTests.cs:5326` pins. The leads do not change. The rename site keeps its direct `VaultErrorDetail` call.

### 4. Copy link says why it copied nothing (found and fixed here)

`CopyWikilink` (`FilesSidebarViewModel.cs:2142-2156`): when core returns no link, `ReportFailure("Could not create a wikilink for this file.")`, the Mac's sentence for the same case (`AppState.swift:1863-1864`), at High like the sidebar's other failures. Nothing is copied, as today. The sentence is host-composed, as the Mac's is; it joins #1956's catalogue as one line. Small by AGENTS.md (a file the PR already changes), red first (Tests 2), one NVDA phrase (Call 5).

### 5. `T/Censuses/ShellFailureDetailCensus.cs` (new) and the compilation

- `T/Support/ShellCompilation.cs` drops its synthetic global-usings tree and binds with the app's own generated usings, which it already loads from `S/obj` (as `ShellPromptOwnerCensus` does). When no generated `GlobalUsings.g.cs` is found, it fails with the instruction to build the shell first. A fact in `T/ShellCompilationTests.cs` pins that the compilation's global usings are exactly the generated file's.
- `TheShellSourcesBindWithoutErrors`: every shell source binds with no error. Red on main (the three CS0104).
- `NoShellFailureBecomesTextOutsideFailureDetail`: rule 1 over every shell source, through `MessageReads(model, file, isHelper)`, extracted from `BasesErrorDetailCensus.Violations` so both censuses run one implementation. A read passes only inside `FailureDetail.Of` (by symbol) or when an exception row matches it.
  - The row: `Row(string File, string Type, string Member, int Reads, string Owner, string Dated, string Reaches)`. The member is the declaration that holds the read's operation tree, as `SaveChainAdmissionCensus` defines it (`HoldsAnOperationTree`, `T/Censuses/SaveChainAdmissionCensus.cs:783-797`): a method, constructor, operator or conversion; an accessor (named `Property.get` or `.set`); a field, property or parameter initializer (named by its field or property); a local function or lambda counts to its containing member. `Type` is the containing type's name, so `CanvasDocumentViewModel` and `CanvasPhrase` in one file are told apart. `Reads` counts the reads in every overload of that name in that type, as `SaveChainAdmissionCensus.Row` counts calls.
  - A member with more reads than its row, or a read with no row, fails and prints a ready row. A row with fewer reads than it names, or none, fails as stale, so each later PR deletes or lowers the rows it ends.
  - No row may name a Files sidebar file or `EditTagAtItsTurn`.
  - Red on main: the 23 reads this chunk ends have no row.
  - The residual a count key leaves (one read routed and another added in the same member keeps the count) is named in the header, with the other limits: code under an inactive `#if` (the census parses with no preprocessor symbols, `T/Support/CSharpSource.cs:87`), a XAML binding path, and `ToString`, interpolation and `string.Format`, which are rule 2's.
- Rule 2 stays on the Bases sources (Call 1). The census's header records the trial: 76 hits of rule 2's own over the shell, none a dump reaching text, which is not few.
- Planted cases (bound beside the shell, as `BasesErrorDetailCensus.PlantedViolations` does), run through `MessageReads` alone: member access, `?.Message`, a property pattern, an extended pattern (`InnerException.Message`), `GetBaseException().Message`, and `catch (VaultException.Io io) { … io.Message }` are each named; the real helper's read is clean; a planted same-named class elsewhere and an `Of` overload are named; each holder kind (a lambda, a local function, an accessor, a constructor, a field initializer, a second type with the same member name, an overload) counts to the member and type the row key says; a row with no read and a row with fewer reads are named stale; a member with two reads under a one-read row is named; a row naming a sidebar file is refused.

### 6. What this chunk does not change

CB-15b's and CB-15c's sites (dated rows); the four search and graph arms (CB-15b, CD9); any lead; core's wording; the rename site. No `A11yEvent` construction moves (contract 38's ledger does not move); no chord or help line (the parity matrix does not move); no focus call. No Mac source is read by a test, so no census trait is owed.

## Tests (red on main first, then green)

The red run is the new tests on main's product code, plus `S/FailureDetail.cs` staged as `=> failure.Message` so the test project builds; the sites, `BasePhrase.ErrorDetail`, `CopyWikilink` and `ShellCompilation` are main's. Every fact below fails there, on the dump, the silence, the unrowed reads or the bind; then the real body, the sites and the fixes make them pass.

1. `T/FailureDetailTests.cs` (new): `Of` gives core's detail for `Io`, `DestinationExists`, `InvalidPath`, `InvalidQuery`, `TrashConfirmationChanged` and `SavedButUnindexed`, each a literal written in the test (as CB-13's `ARefusedRenameSaysCoresDetail` does, `T/FileManagementTests.cs:80-102`), and no field token (`@\w+=`, as `T/RecoveryAnnouncementTests.cs:231` checks); an `IOException`'s own message; and a `CommandException.UnknownId`, which returns its own `Message` because core renders no command error, named in the fact as the helper's documented limit.
2. `T/FilesSidebarFailureDetailTests.cs` (new), converted from the worker's probe. One fact per site, each asserting the exact Status and the one `HostComposed` High (or the canvas or graph event's text) and no field token. Oracles are literals where core's text is fixed and locale-free (`DestinationExists`, `InvalidPath`, `InvalidQuery`, `InvalidArgument`, `TrashConfirmationChanged`); for `Io` (operating system text) the oracle is the lead plus `VaultErrorDetail` of the direct core call. Sites and drivers:
   - `:1540` (`TagTreePageForTests` throwing `VaultException.InvalidArgument` past `MaxTagTreeRestarts`); `:1801`, `:1881`, `:1967` (the replaced folder); `:2058`, `:2453` (`TrashStagedForTesting`); `:2099`, `:2131`; `:2160` (a constructed row: it drives the real catch with a real core refusal); `:2385` (core's `SLATE_TEST_FAULT_PLANT_MARKERS` under `T/EnvFaultLock.cs`, as `T/FileManagementTests.cs:129` uses it); `:2626` (the constructed directory row `../outside`, run once first to confirm core refuses it; if it does not, the fact is dropped and the PR says `:2626` rests on the census); `FileManagement.cs:1183`, `:1213`, `:1318`; `TreeOperations.cs:456` (the existing tree worker seam throwing a `VaultException.InvalidArgument`).
   - `CanvasNotes.cs:41` and `SurfaceNotes.cs:42`, `:46`: the fact drives the surface where a test already can (`T/GraphEndToEndTests.cs:637-647` drives `SurfaceNotes.cs:42` through `leaf.CreateNoteFromSurface`; extend it to the whole sentence) and otherwise asserts the returned result and the text core renders for the event the surface builds from it, saying which.
   - The tag edit, through the workspace as the lifecycle wires it.
   - Copy link (Design 4): a canvas row and an image row each report "Could not create a wikilink for this file." once at High and copy nothing; a Markdown row copies its link as today. Red on main (nothing said).
   - `TreeOperations.cs:309` has no fact: it is the synchronous path the app never takes (`VaultLifecycleViewModel.cs:262`), no seam reaches it, and adding a product seam for a tests-only path is not worth it. The census covers it, and the PR says so.
3. The census and compilation facts above (Design 5), red on main.
4. Regression, not red: `BasesErrorDetailCensus`, `VaultErrorDetailCensus`, `ShellPromptOwnerCensus`, every Bases fact that asserts no `@message` (`T/BasesBuilderTests.cs:244-248`, `T/BasesDocumentTests.cs:221`, `T/BasesExportTests.cs:204`, `:228`), and the prefix pins listed above.

Mutations (each must turn the named fact red; the PR lists each with its result):

- `FailureDetail.Of` returning `failure.Message` for a vault error: 1, 2;
- one sidebar site left on `exception.Message`: its fact in 2, and the census's main fact;
- `BasePhrase.ErrorDetail` reading `failure.Message` itself again: `NoBasesFailureBecomesTextOutsideCoresDetail`;
- the helper check made to accept any method named `Of` in any class named `FailureDetail`: the planted same-named class;
- the helper check removed (no read is exempt): the census's main fact, on the real helper's own read;
- a stale row left in the table: the census's main fact;
- `ShellCompilation`'s synthetic `System.IO` restored: `TheShellSourcesBindWithoutErrors` and the usings pin;
- `IsConsumer` trusting every method in `FailureDetail.cs`: a planted stringifying member there, named;
- `CopyWikilink` silent on no link: Copy link's facts in 2.

## The NVDA spot check

Under the desktop lock, NVDA with debug logging, base and head Release builds, through `scripts/windows/launch-for-field-pass.ps1 -Root`. By keys (by reading: Space on a tree row checks it, `S/MainWindow.xaml.cs:1825-1829`; the "Batch actions" expander holds "Move to…", the "Tag name" field and Add, `S/MainWindow.xaml:1317-1325`):

- Move To (`FileManagement.cs:1183`): check `a.md` in Files, Batch actions, Move to…, the folder `sub`, which already holds an `a.md`. Base: "Move failed: @path=sub/a.md". Head: "Move failed: Something named sub/a.md already exists there."
- The tag edit (`EditTagAtItsTurn`): check a note, Batch actions, Tag name "#", Add. Base: "Tag edit failed: @message="#" is not a valid tag." Head: "Tag edit failed: "#" is not a valid tag."
- Copy link on a canvas row (the toolbar's Copy link or its chord). Base: nothing. Head: "Could not create a wikilink for this file."

The PR body records each phrase verbatim, base and head. If the worker cannot reach one by keys, it records how far it got, and the PR says which phrase rests on the hosted fact alone.

## Files

Product: new `S/FailureDetail.cs`; `S/Bases/BaseDocumentViewModel.cs` (one line); `S/FilesSidebarViewModel.cs` (the sites and `CopyWikilink`), `S/FilesSidebarViewModel.FileManagement.cs`, `S/FilesSidebarViewModel.TreeOperations.cs`, `S/FilesSidebarViewModel.CanvasNotes.cs`, `S/FilesSidebarViewModel.SurfaceNotes.cs`; `S/WorkspaceViewModel.Properties.cs` (one line, Call 2). Tests: new `T/FailureDetailTests.cs`, `T/FilesSidebarFailureDetailTests.cs`, `T/Censuses/ShellFailureDetailCensus.cs`; `T/Censuses/BasesErrorDetailCensus.cs`; `T/Support/ShellCompilation.cs`; `T/ShellCompilationTests.cs`; `T/GraphEndToEndTests.cs` (one assertion widened). New `.cs` files: `git add`, then `python3 scripts/apply-license-header.py`. `dotnet format --verify-no-changes --include` on every edited C# file. The two step 0 probes are deleted before the first commit.

## Overlap

- **CB-14 (#2008, open)** edits `S/WorkspaceViewModel.cs`, `S/Panels/TasksReviewViewModel.cs` and `S/CreateOutcomes.cs`. The first two hold five of the census's dated rows (`StartAdmittedTaskToggle`, `PerformTaskToggle` with two reads, `LoadFirstPage`, `LoadMore`, `DirectToggleAtItsTurn` with two). #2008's head adds and moves no `Message` read in either file, so with member keys its merge leaves every row as it is; whichever of the two merges second still runs the census on the merged tree.
- **CB-28** (principal P, same wave) edits `S/Panels/RightPanePanelsViewModel.cs`, which holds three dated rows (`LoadLinks`, `LoadOutline`, `LoadTasks`), and `S/EditorInteractions.cs` and `S/Reading/`, which hold rows in `EditorInteractions.cs` and `Reading/ReadingContentViewModel.cs`. CB-28's spec adds no `Message` read. Once this census is on `main`, a new read there, or one moved to another member, fails it (Call 4).
- **Plan §6.** The `FilesSidebarViewModel*.cs` row names CB-15a. The `WorkspaceViewModel*.cs` row does not; the line to add is "CB-15a (one line of `WorkspaceViewModel.Properties.cs`, `EditTagAtItsTurn`), after CB-14" (Call 2). `Bases/BaseDocumentViewModel.cs` is in no §6 row; no C7 chunk edits it. `T/Support/ShellCompilation.cs` is test support every binding census reads.
- **CB-15b and CB-15c** inherit the census: each deletes or lowers its rows as it routes its sites through `FailureDetail.Of`, and CB-15c deletes the row mechanism once the table is empty.

## Docs and registers

- No contract register row names these sentences. `reading/05_spoken_failures.md` is the reading and is not edited.
- #1956 (CD38's catalogue): one comment line for the Copy link sentence (host-composed, as the Mac's is) and for leads that repeat the path an `InvalidPath` detail names ("Could not create folder Untitled Folder: Invalid path sub/Untitled Folder: …").

## Ledgers

- #1954 (Mac work owed): none. No core change; the Mac does not call `vaultErrorDetail` (#1527 is its twin), and Design 4 adopts the Mac's own sentence.
- #1955 (deferred finds): none planned (Finds 1 and 3 are filed by Call 3).

## Finds

1. **A listing fails on any other connection's commit.** `list_directory_page` refuses any page, the first included, when SQLite's `data_version` changed while it read (`crates/slate-core/src/session/directory_page.rs:451-479`, `stale_cursor_error` at `:102-104`). The tree refresh (`TreeOperations.cs:309`, `:456`) and the dual pane (`FilesSidebarViewModel.cs:2626`) read single first pages (`LoadDirectoryLevel`, `FilesSidebarViewModel.cs:1244-1277`, `Paging(null, …)`), Move To's folder walk pages on with cursors (`FilesSidebarViewModel.MoveToLoading.cs:95`), and by reading none of them restarts, where the tag walk restarts up to `MaxTagTreeRestarts` times (`FilesSidebarViewModel.cs:1501-1524`). Measured: a second session committing during the listing failed it in 2 to 4 attempts. The reader hears "Could not load files: …" about a cursor they never held. The Mac lists pages the same way (`apps/slate-mac/Sources/SlateMac/FileTreeSidebar.swift:1856`, `AppState.swift:17626`, `:21990`). Sizing: the right fix is a bounded restart of a refused page, in core for both hosts; core reaches the Mac, so High: file it (Call 3).
2. **Copy link says nothing for a file core makes no wikilink for.** Any non-Markdown file (a canvas, an image, a PDF, a base), a file that left the index, or a case twin (Current state). Sizing: Small (a file the PR already changes; the Mac's existing sentence), fixed here (Design 4, Call 5).
3. **Some of core's details read as developer text:** "directory page cursor is stale; restart from the first page", "an interrupted structural batch requires close-and-reopen recovery", the fault seam's text, and details with no final period. Core composes them, so a change reaches the Mac: High, filed with Find 1 or beside it (Call 3). Leads that repeat a path the detail names are the host's, and go on #1956 as a comment line.
4. **The census compilation adds two global usings the app does not have** (step 0). Small, test support only, and this chunk's census needs it: fixed here (Design 5), with its failing fact first and a "Found and fixed here" line.

## Verification

```
# hosted facts and censuses: red on the tests-only commit, green on the fix
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~FailureDetailTests|FullyQualifiedName~FilesSidebarFailureDetailTests|FullyQualifiedName~Census|FullyQualifiedName~ShellCompilationTests|FullyQualifiedName~GraphEndToEndTests"
# the classes around the sites
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~FileManagementTests|FullyQualifiedName~SidebarTagFilterTests|FullyQualifiedName~RescanTests|FullyQualifiedName~SaveChainAdmissionTests|FullyQualifiedName~Bases|FullyQualifiedName~SidebarSettingsRetryTests|FullyQualifiedName~SidebarWriteJournalTests|FullyQualifiedName~W1HardeningTests"
# the whole unit run under the desktop lock before the push (c7-process step 10)
```

## Acceptance

- Every fact above red on the tests-only commit and green on the fix; every mutation red; the regressions green.
- The NVDA phrases recorded for base and head.
- `gh pr view N --json closingIssuesReferences` is empty. No commit message or PR text puts a close, fix or resolve word before #1890 or any other number.
- PR title: `fix(windows): the files sidebar says core's words for a failure, and a census holds the shell to one reader of an exception's message`. Predicted count: found 4 (Finds 1 to 4), fixed here 2 (Finds 2 and 4), deferred 0, filed 1 or 2 (Call 3).

## Calls for the lead

1. **Rule 2 stays on the Bases sources.** Recommended: yes; 76 hits beyond rule 1's own is not few, and by reading none is a dump reaching text (25 are typed-field reads, the correct way to read a uniffi error). Cost: a future `$"{exception}"` or `exception.ToString()` outside Bases is not caught; rule 1 still catches every `Message` read. Two alternatives: rule 2 shell-wide with named allowances for typed-field reads, stored exceptions, captures, wrappers and delegates (a census redesign of its own size); or rule 2 per family as each sweep lands, starting with the sidebar's three hits (two allowance kinds now: typed-field reads and `Task.FromException`), growing in CB-15b and CB-15c.
2. **The tag edit's read moves to `FailureDetail.Of` in this chunk,** though it now sits in `WorkspaceViewModel.Properties.cs`. Recommended: yes; the sentence is the sidebar's, and leaving it would give a sidebar sentence a CB-15c row. Cost: one line in a `WorkspaceViewModel*.cs` file, which §6's row does not name for CB-15a (the line to add is under Overlap; no other C7 chunk edits that file, and CB-14's open PR does not touch it).
3. **File Find 1 (the stale first-page listing) and Find 3 (core's developer-text details)** as bugs (labels bug, windows, rust), with step 0's measurement, one issue each or one together, rather than add a host retry here. Recommended: file, one issue each. Cost: the tree refresh keeps failing on another session's commit until its own PR; this chunk changes only what that failure says. The alternative for Find 1, the tag walk's bounded restart in `LoadDirectoryLevel`, is Small by files but changes the tree refresh's failure path, which has its own publication rules, and leaves the Mac's twin.
4. **Merge order with CB-28.** Recommended: whichever of CB-15a and CB-28 merges second merges `main` and runs the census; P is told now that new code reads no exception's `Message` and moves none between members in `RightPanePanelsViewModel.cs`, `EditorInteractions.cs` or `Reading/`. Cost: none if CB-28 keeps to its spec; a dated row owned by CB-15c otherwise.
5. **Copy link (Find 2) is fixed here** with the Mac's sentence, "Could not create a wikilink for this file.". Recommended: yes; it is Small by AGENTS.md and the wording is already the Mac's. Cost: one more host-composed sentence (a #1956 line), its facts and one NVDA phrase. The alternative is to make Copy link unavailable on a non-Markdown row, which adds no sentence but leaves the vanished-file and case-twin causes silent, and differs from the Mac.

## Red team record

A fresh Opus red team read the first draft on 2026-11-18 (its report and the first draft were kept outside the repository): 0 blockers, 7 medium, 23 low. All taken, one in part.

- **M1, nineteen vault sites, not twenty (taken):** every count corrected; step 0 drove all nineteen.
- **M2, `:309` and `:2626` drivers (taken):** `:309` is the synchronous path the app never takes and has no seam; it rests on the census, with no product seam added, and the PR says so. `:2626` gets the constructed directory row.
- **M3, Copy link (taken):** the find covers every cause, non-Markdown files first; it is Small and fixed here with the Mac's sentence, not deferred.
- **M4, the helper by symbol (taken):** `InTheHelper` and `IsConsumer` use the symbol; planted same-named class and overload.
- **M5, `FailureDetail.cs` in the censused set (taken):** the file stays out of the set; `IsConsumer` trusts `Of` by symbol; a mutation pins it.
- **M6, rule 1 alone and the row key (taken):** planted cases through `MessageReads` alone; the member defined as `SaveChainAdmissionCensus` defines it, with the type in the key and each holder kind planted; "fewer reads" planted.
- **M7, the compilation already loads the app's usings (taken):** the synthetic tree goes; the census binds with the generated file, as `ShellPromptOwnerCensus` does; the safety of dropping the two names is stated.
- **Lows (taken, 27 in part):** 27 call sites; the reading's 90; "every recorded sentence"; CB-28's real files and CB-14's five rows that its merge leaves as they are; the mutation list; `EnvFaultLock`; the canvas and graph facts drive their surfaces where a test can; literal oracles and the `@\w+=` token; the teardown pin; the count-key residual and the census's other limits; the aggregate limit and the `UnknownId` fact's wording; `ShellCompilationTests` in the filter; `EditTagAtItsTurn` named; `SavedButUnindexed` cannot reach these sites; Find 3's home (filed for core's text, #1956 for the leads); Docs, Verification and Acceptance sections and the helper's wording; line references; the NVDA key path; rule 2 per family offered as Call 1's second alternative, not as its own call (in part); the two unsized observations sized as not defects; the red-run sentence; the §6 line.

## What delivery changed (2026-11-19, PR #2015, merged as cc16f7e3)

The PR merged at f92b1748, which contained main 5b890dd9. It referenced #1890 and did not close it; CB-15b and CB-15c continue that issue. #2012 was filed for the listing that fails when another session commits during it, and for core's developer-text details.

**What the code does that the spec did not say.**

- **The red census named 24 reads, not 23.** The twenty-fourth was `BasePhrase.ErrorDetail`'s own read. Once the census finds the helper by symbol, that read is no longer the exempt one, and the forward to `FailureDetail.Of` ends it.
- **The census compilation reads the test build's own configuration.** It binds with the app's generated usings from `obj/<the tests' configuration>`, excluding the `_wpftmp` copies, as `ShellPromptOwnerCensus` does. The first build also bound stray Debug and `_wpftmp` trees, 15 of them (the diff red team).
- **The row key.** It is the containing type's full name, with its namespace, nesting and arity, and the member as its symbol names it, following `SaveChainAdmissionCensus`. Accessors are named by their property, and an explicit implementation by its qualified name. The spec's "type" was the nearest type's identifier, and Codex round 1 showed that twins collide under it.
- **The ratchet is tighter than the spec's "can only shrink".**
  - A pinned 2026-11-18 baseline holds each key with its count. A remaining row must be in it, with no more reads than its baseline count, so no allowance can move between keys (Codex round 1).
  - The table also fails if it grows past its ceilings (61 rows, 73 reads), or falls below them while the ceilings are not lowered.
  - Every row is dated 2026-11-18, and its owner matches its folder.
  - The header names the residual: a lowered row may rise back to its allowance while another row falls by as much.
- **The planted helper is built from the real file.** `FailureDetail.cs`'s text gets "partial" inserted, so the planted helper cannot drift from the real one (the diff red team).
- **Copy link.**
  - Copy link is now offered only for exactly one Markdown file, as on the Mac. The first merge judge's minor 1 found that a canvas or an image heard the new failure sentence where the Mac offers no action.
  - A row counts as Markdown when core's summary says so (`files.is_markdown`). A row with no summary falls back to core's Markdown extensions.
  - When the command is unavailable, the palette gives its existing generic reason, "This command is not available right now.". The Mac's specific reason, "Select exactly one Markdown file to copy its wikilink.", was not adopted; it goes to the owner.
  - The sentence "Could not create a wikilink for this file." stands only for a Markdown note core makes no link for. Core's cases are:
    - a note still in the index that is gone from disk, is now a folder or a link, or cannot be checked there (stale index rows fail closed);
    - a note that left the index;
    - a case twin;
    - a name a link cannot hold.
- **Copy link, Pin and Unpin are unavailable on group-header and placeholder rows** (the diff red team's major 1). On main, Copy link spoke an `InvalidPath` sentence there, and Pin wrote an empty path into the pins and said "Pinned Today.". So `:2160` was reachable on main from those rows; the spec's step 0 said it was not.
- **The selection re-queries the file verbs on every assignment** (the second merge judge's nit 1). When the selection moved to a header, a placeholder or nothing, the setter returned before it re-queried the verbs. The File actions buttons then kept the last row's enabled state.
- **The tag-edit fact asserts at the admitted edit's completion,** with the saves idle before and after. The first version polled the clock (Codex round 1).
  - `TestWaitCensus` did not name that poll. A delay inside a loop that ends on a condition is a condition wait, which CB-6 allows by design.
  - A probe counts 57 such polls across both test projects. That is the census's design, not a defect, so nothing was filed.

**What was measured that the spec expected otherwise.**

- `:2626` (load folder files) has a deterministic fact through a constructed directory row, `../outside`, which core refuses. The spec allowed it census-only coverage.
- The three `WorkspaceViewModel.cs` reads belong to type `WorkspaceTabViewModel`.
- Copy link sits in the File actions expander, and Windows has no chord for it. The spec put it on the Files toolbar with a chord.
- NVDA, with board.canvas selected after the gate:
  - The File actions button is disabled and not a Tab stop. Object navigation reads it as "Copy link, button, unavailable".
  - The palette says "Selected: Copy Wikilink. Unavailable: This command is not available right now.".
  - The Markdown refusal itself was not reached by keys; a hosted fact covers it.

**Reviews and records.**

- Spec red team: 0 blockers, 7 medium, 23 low.
- Diff red team: 0 blockers, 2 majors, 12 minors. Both majors and seven minors were taken.
- Codex:
  - Round 1 was needs-attention (3 medium). Round 2 approved.
  - On the first judge's fix, delta round 1 was needs-attention: one medium finding, declined with reason, because the Markdown refusal fact is a guard. Delta round 2 approved.
  - On the second judge's fix, Codex approved.
- Merge judges 1, 2 and 3 each said Merge.
- #1956 has the Copy link sentence's line. #1955 has the High-against-Medium priority line.

Count: found 8, fixed here 5, deferred 1 (#1955), filed 1 (#2012, which covers two of the finds).
