# CB-15b: canvas, graph and search failures say core's words, and the search and graph error arms go

Issue: #1890, referenced only (CB-15c closes it; the PR writes "Refs #1890" and keeps every close, fix or resolve word away from that number and every other, in the PR text and in every commit message). Size M. Lanes: windows (the app job; hosted facts and a census). No change under `crates/` and none under `apps/slate-mac/`. AT: an NVDA spot check. Wave: C8, slot 2. Depends on: CB-15a (#2015, cc16f7e3, merged). Parallel with: CB-31 (rust, swift) and CB-29 (diagram.rs, the SVG guard). Decision: CD9, as the owner answered it on 2026-11-16 (below). Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9; product code changes).

**CD9 as answered (§3.1, 2026-11-16).** "The four search and graph error arms are not kept: the host says core's sentences." The plan row: "The four arms go; one sentence (the invalid-query one) changes, on search and on graph. The `Io`, `Db` and `Unsupported` arms already produce core's words (`SearchOverlayViewModel.cs:909-916`, `GraphDocumentViewModel.cs:1509-1516`); only the `InvalidQuery` arm adds a lead of its own, "Search query is invalid:". CB-15b checks that sentence on both surfaces under NVDA. The #1890 census needs no exception for the arms." CB-15b deletes both `HumanReadable` methods and speaks `FailureDetail.Of(failure)`. Step 0 shows the graph's `InvalidQuery` arm cannot be reached by any input (Call 1).

## Lead rulings (2026-11-19)

1. Call 1 accepted: no field-pass seam; a hosted fact through `FetchGateForTests` for the graph's invalid-query arm. NVDA hears the two graph lines real input reaches (the Connections failure and the inspector's read-only notice) and the search's invalid-query sentence, base and head. The PR body and the §12 line record the departure from "both surfaces", with the reason that core's graph build fails only with `Db`.
2. Call 2 accepted: one comment on #2012 with step 0's table; core is not reworded here. The PR body carries a heading "For the owner" with the before and after of the search's invalid-query sentence (main "Search error: Search query is invalid: FTS5 could not parse …", head "Search error: FTS5 could not parse …"); the PR proceeds on CD9 as answered.
3. Call 3 accepted: the FFI crate's `Display` words, Windows-only; the PR body names the two strings.
4. Call 4 accepted: CD9 is the owner's recorded call, so it is appended to both contract records as added lines; no existing text changes.
5. Call 5 accepted: the #1954 line, and "not delivered: VoiceOver pass (no Mac speech changes)" in the PR body.
6. Call 6 accepted: row 11 keeps its real-refusal fact, provided it waits on a condition and never on a fixed delay. If it shows as a flake on a hosted runner, the lead is told before it changes.
7. Call 7 accepted: Find 1 is filed as High (labels bug, windows) and counts as filed.
8. Call 8 accepted: row 7 is census-only, with the reason (the race the lease's lock forbids) in the census's text and in this spec.

Written 2026-11-19 from the plan's rows (§5 CB-15b, §3 and §3.1 CD9, §6, §12's CB-15a row), CB-15a's spec, `reading/05_spoken_failures.md:13-46` and `:205-346`, and #1890 with the owner's comment ("CB-15b checks the changed sentence on both surfaces under NVDA. Nothing comes back to the owner."). Every file and line below was read at `main` 16ad788d (plan 47 records wave C7, #2018), which is also where step 0 ran, on a Release build. A fresh red team read the first draft (0 blockers, 4 major, 15 minor); every finding is folded in, and "Red team record" says how. `S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`. Each statement that rests on reading alone says so.

## Goal

1. Every canvas, graph and search failure sentence built from a caught failure says core's words, never the binding's field dump (`@message=…`, `@path=…`, `@reason=…`, `@version=…`): the census's 22 CB-15b members, 26 reads.
2. The search overlay's and the graph's four hand-written error arms go (`HumanReadable`, two copies); both speak `FailureDetail.Of(failure)`. One sentence changes beyond the dump: an invalid search query loses the lead "Search query is invalid: ".
3. `GraphConfigStore.Reason` reads `GraphConfigException`'s typed fields, in the words the FFI crate's own `Display` gives the two variants (Call 3), so the graph inspector's read-only notice says no `@version=` or `@reason=`.
4. `ShellFailureDetailCensus` loses its 22 CB-15b rows, its ceilings fall to 39 rows and 47 reads (all CB-15c's), and a row in `Canvas/`, `Graph/` or `Search/` is refused, as CB-15a refused a Files sidebar row.

## Current state on main (the drift check)

Every file-and-line claim of the plan's CB-15b row, CD9's row, §12's CB-15a row and the reading's #1890 tables for these folders was re-read at 16ad788d.

- **Held.** The reading's canvas lines (`CanvasDocumentViewModel.cs:964`, `:2570`, `:2715`, `:2856`, `:3154`, `:3216`, `:3787`, `:3858`, `:4007`, `:4137`, `:4241`, `:4891`, `:4992`, `:5503`, `:5512`), `CanvasMutationFunnel.cs:486`, `ConnectionsLeafViewModel.cs:894`, `:899`, `:912`, `GraphDocumentViewModel.cs:1515`, `:1855`, `:1859`, `GraphConfigStore.cs:88`, `:167-173`, `SearchOverlayViewModel.cs:909-916` (the reading's `:915`), `GraphPreferencesViewModel.cs:109`, `CanvasNavigator.cs:1423-1427`, `ConnectionsLeafView.cs:723` and CD9's `GraphDocumentViewModel.cs:1509-1516` all stand at the cited lines. Core's `vault_error_detail` is `crates/slate-core/src/a11y.rs:3668-3705`, exported at `crates/slate-uniffi/src/lib.rs:12363-12380` (Io and Db return their message as it is).
- **Moved.** `SearchFailed`'s template is `a11y.rs:2155`, not the reading's `:872` (contract 29's S6 text still cites `:872`).
- **Corrected by step 0.** The plan's "21 sites" (the reading's 16 canvas sites and 5 graph and search sites) are the census's 22 members and 26 reads: 16 canvas reads; five graph and search reads the reading counted as vault sites (`ConnectionsLeafViewModel.cs:894`, `GraphDocumentViewModel.cs:1515`, `:1855`, `SearchOverlayViewModel.cs:915`, and `GraphConfigStore.Reason`, one site of two reads); and four reads of an ordinary exception's message (`ConnectionsLeafViewModel.cs:899`, `:912`, `GraphDocumentViewModel.cs:1859`, `GraphConfigStore.cs:88`), which route through the helper with no change of text. Of the five, `GraphDocumentViewModel.cs:1515` is the build's fallback: the build fails with a vault error only as `Db` (by reading), which its own arm handles, so the fallback meets only an `InvalidOperationException` or `IOException` (an `ObjectDisposedException` at close among them) on today's code. CD9's "on search and on graph": the graph's `InvalidQuery` arm is unreachable (step 0, site 18).

**The census's CB-15b rows** (`T/Censuses/ShellFailureDetailCensus.cs:92-114`, baseline `:166-187`): `CanvasDocumentViewModel` (13 members, 1 read each), `CanvasPhrase.OpenFailed` and `.RetargetAbsent` (1 each), `CanvasMutationFunnel.Transact` (1), `ConnectionsLeafViewModel.FetchUnder` (3), `GraphConfigStore.Read` (1) and `.Reason` (2), `GraphDocumentViewModel.FetchUnder` (2) and `.HumanReadable` (1), `SearchOverlayViewModel.HumanReadable` (1). Ceilings 61 rows and 73 reads (`:335-337`); the baseline holds 61 and 73 and never changes (`:342-348`).

**What each site reaches** (by reading, then measured in step 0):

| Site | Catch | Sentence |
|---|---|---|
| 13 `CanvasDocumentViewModel` verbs | `VaultException` | core's `CanvasActionFailed(action, detail)`, "{verb} failed: {detail}" (`a11y.rs:3285-3287`), or for Convert Card to Note `CanvasBlocked(NoteReadFailed)`, "Could not read the card text: {detail}" (`:3263-3264`); spoken at High |
| `CanvasPhrase.OpenFailed` | any survivable exception (`CanvasLoadPipeline.cs:408-411`, `CanvasHandleLease.cs:32-36`) | host text "This canvas could not be opened: {detail} ({path})", the failed state's banner |
| `CanvasPhrase.RetargetAbsent` | the same, after a rename retargeted the document | core's `ReopenFailed` sentence with "{from} moved to {to}: {detail}" as its message (`a11y.rs:3275-3278`), the banner |
| `CanvasMutationFunnel.Transact` | `VaultException` held in `refusal`, not `WriteConflict`, not `SavedButUnindexed` (`:459-487`) | `CanvasActionFailed(CanvasAction, detail)`, "Canvas action failed: {detail}" |
| `SearchOverlayViewModel.HumanReadable` | every exception of the search (`:756`, `:781`; `await` unwraps), `Cancelled` dropped first (`:827`) | `SearchFailed`, "Search error: {detail}", the summary and the announcement (`:875-883`) |
| `GraphDocumentViewModel.HumanReadable` | the diagram build's `VaultException`, `InvalidOperationException`, `IOException` (`:984-993`) | `DiagramError`, shown with the accessible name "Graph diagram error: {detail}" (`GraphSurfaceView.cs:1257-1259`); not announced |
| `GraphDocumentViewModel.FetchUnder` | the table load's same three (`:1853-1860`) | `GraphBlocked(LoadFailed)`, "Couldn't load the graph: {detail}" (`:1914-1927`, `a11y.rs:4263-4264`), and the error state |
| `ConnectionsLeafViewModel.FetchUnder` | the same three, plus the test gate's (`:891-913`) | `GraphBlocked(ConnectionsLoadFailed)`, "Couldn't load connections: {detail}" (`:962-981`, `a11y.rs:4266-4267`), and "Connections error: {detail}" (`ConnectionsLeafView.cs:723`) |
| `GraphConfigStore.Read`, `.Reason` | `IOException`, `UnauthorizedAccessException`, `DecoderFallbackException` (`:86-91`); `GraphConfigException` (`:96-101`) | `LoadFailure`, shown as "Graph settings are read-only: {reason}" in the inspector (`GraphInspectorView.cs:375`) |

**Tests that pin these sentences.** None pins one of the 26 with a real core refusal. Unchanged under core's detail: `T/SearchOverlayViewModelTests.cs:385-404` (a `Db` arm, "disk exploded"), `:964-967` and `:1162-1165` (the panel state only); `T/GraphDiagramTests.Lineage.cs:207-219` (`Io("disk gone")` through the `FetchGateForTests` seam); `T/CanvasDocumentTests.cs:1331-1344` (`OpenFailed` and `RetargetAbsent` with an `InvalidOperationException`), `:1561-1577` (contains the two paths), `:1668-1702` (not null); `T/CanvasMutationFunnelTests.cs:57-73` (the event type only); the graph config, inspector and preferences tests (not null, or the text compared with itself); the graph and Connections tests that throw an `InvalidOperationException` ("injected"). `T/CanvasMutationTests.cs:2618-2633` is a source census over four canvas catches (the text "VaultException", "CanvasActionFailed", "return null"); it survives. **One test mirrors the dump:** the Connections model's oracle, `T/ConnectionsLeafTests.Model.cs:1992-2003` (`LineFor`), renders `ConnectionsLoadFailed(failure.Message)`, and by reading its cells reach that catch: the `Missing` presentation roots at `missing.md` (`:169`, `:321-338`; the enum's comment, `:71-74`, "a path core cannot load, its Error core's own"), `lineBefore = LineFor(host, rootBefore, …)` runs for those cells (`:2281-2283`), audible pending cells expect that line (`:452`), and the `Composed` and `ReRoot` partials use the same `LineFor`. So head fails the model classes until the oracle changes. No `.xaml` file sits under `Canvas/`, `Graph/` or `Search/`, so no binding path there reads an exception (the census does not bind XAML).

### Step 0, as run (2026-11-19, main 16ad788d, Release)

Two workers ran uncommitted probes on main: `T/Cb15bStep0CanvasProbe.cs` (16 facts, passed in three runs) in this chunk's worktree, and `T/Cb15bStep0GraphProbe.cs` with `T/Cb15bStep0CensusProbe.cs` (26 facts in two runs, then 27 in two more, every run passed, the outputs identical run to run) in a detached worktree of the same commit. Each site fact drives the site's real catch with a real core refusal, takes the oracle exception from the same core call made directly, asserts main's sentence, and prints the sentence with `FailureDetail.Of` in its place. Two graph-probe facts do not drive a catch: `GraphArmsForEachAdmittedException` and `SearchArmsForTheOtherVariants` render constructed exceptions through a copy of the arms, to compare the arms with core's detail variant by variant. The outputs stay local, in the principal's scratch folder (its `w1-canvas` and `w2-graph` folders), and are not in the repository. No probe opened a window.

**The census trial.** On main, `TheShellSourcesBindWithoutErrors`, `NoShellFailureBecomesTextOutsideFailureDetail` and `TheTableOnlyShrinks` pass (the class, 19 of 19; 311 shell sources bind clean). Rule 1 over `Canvas/`, `Graph/` and `Search/` finds exactly the 22 members and 26 reads. Rule 2 (`BasesErrorDetailCensus.Violations`, every shell tree censused) over the three folders has 46 hits: 26 are rule 1's reads seen again and **20 are rule 2's own**, none an exception reaching text by interpolation, `ToString` or concatenation (by reading): 12 typed-field reads (`CanvasMutationFunnel.cs:313`, `:314`, `:473`, `:474`, `unindexed.newContentHash`; the eight arm reads in the two `HumanReadable` methods), 4 stored exceptions (`GraphDocumentViewModel.cs:1228`, a `GraphDiagramRefresh` only logged at `:1428`; `SearchRecentsStore.cs:164`, `:184`, `:226`, `LastSaveError`, which no shell code renders), 2 captures (`CanvasPresentationEngine.cs:254`, `SearchOverlayViewModel.cs:578`), 1 interface hand-off (`CanvasLoadPipeline.cs:410`, `_source.FailureFor(exception)`, which reaches text only through `OpenFailed` and `RetargetAbsent`, rule 1's rows) and 1 argument guard (`GraphConfigStore.cs:169`). So the three folders hold no failure sentence the census does not see.

**What each site says.** Main is today's sentence; head is the same sentence with `FailureDetail.Of` (and, for `Reason`, the typed fields). "{n}" is the canvas handle's number. "Constructed" means the probe built the state: core's canvas handle closed behind the document's lease (`_session.CloseCanvas(handle)`; the lease still names it, so admission passes and core refuses with `bad_handle`, `session.rs:22410-22414`), or one core apply on the document's own handle around the funnel, so core loses a node the rows still hold (`bad_node`, `:22416-22420`).

| # | Site | Driver | Reach | Main | Head |
|---|---|---|---|---|---|
| 1 | `TryWhereAmI` | bad_node, then Where am I | a race by keys (by reading), else constructed | Where am I failed: @message=canvas node "loose" not found in this canvas | Where am I failed: canvas node "loose" not found in this canvas |
| 2 | `CanvasNewCard` | bad_handle | constructed | New card failed: @message=unknown canvas handle {n} (closed or never opened) | New card failed: unknown canvas handle {n} (closed or never opened) |
| 3 | `CanvasNewGroup` | bad_handle | constructed | New group failed: @message=unknown canvas handle {n} … | New group failed: unknown canvas handle {n} … |
| 4 | `CanvasMoveIntoGroup` | bad_node on the group; also a text card's id (not_a_group) | constructed | Move failed: @message=canvas node "grp" not found in this canvas | Move failed: canvas node "grp" not found in this canvas |
| 5, 6 | `CanvasAddFileCard`, `CanvasAddLinkCard` | bad_handle | constructed | Create failed: @message=unknown canvas handle {n} … | Create failed: unknown canvas handle {n} … |
| 7 | `CanvasRemoveFromGroup` | a thread closes core's handle while the verb runs (reached the catch in every run, attempts 7, 2 and 9) | a race the lease's lock forbids in production | Remove failed: @message=unknown canvas handle {n} … | Remove failed: unknown canvas handle {n} … |
| 8 | `CanvasCreateConnectedCard` | bad_handle | constructed | Create connected card failed: @message=unknown canvas handle {n} … | the same without "@message=" |
| 9 | `CanvasDuplicate` | bad_handle | constructed | Duplicate failed: @message=unknown canvas handle {n} … | the same without "@message=" |
| 10 | `CanvasConvertToNote` | bad_node | constructed | Could not read the card text: @message=canvas node "loose" not found in this canvas | Could not read the card text: canvas node "loose" not found in this canvas |
| 11 | `RequestVaultPick` | the index's WAL read locks held by a second handle; Add Note's file list returns SQLITE_PROTOCOL after about 10 s | an environmental fault only | Create failed: @message=sqlite error: locking protocol | Create failed: sqlite error: locking protocol |
| 12 | `SubmitBulkMarked` | bad_handle, Delete Marked | constructed | Canvas action failed: @message=unknown canvas handle {n} … | the same without "@message=" |
| 13 | `SubmitGroupMarked` | bad_handle | constructed | New group failed: @message=unknown canvas handle {n} … | the same without "@message=" |
| 14 | `CanvasPhrase.OpenFailed` | (a) a `.canvas` with a byte that is not UTF-8; (b) a path with no file; (c) a file another program holds with no sharing | ordinary input | (a) This canvas could not be opened: @path=bad.canvas (bad.canvas); (c) …: @message=The process cannot access the file because it is being used by another process. (os error 32) (board.canvas) | (a) This canvas could not be opened: File at bad.canvas is not valid UTF-8. (bad.canvas); (c) the same without "@message=" |
| 15 | `CanvasPhrase.RetargetAbsent` | (a) case 14a's canvas renamed while open (`WorkspaceViewModel.RetargetPath`, which a sidebar rename calls, `VaultLifecycleViewModel.cs:1675-1676`); (b) a retarget onto nothing | (a) ordinary input; (b) a race | Canvas could not be reopened. The previous snapshot is read-only. bad.canvas moved to renamed.canvas: @path=renamed.canvas | … renamed.canvas: File at renamed.canvas is not valid UTF-8. |
| 16 | `CanvasMutationFunnel.Transact` | (a) another program holds `board.canvas` (read sharing, no delete sharing) during New Card; (b) the file's read-only attribute; (c) bad_node then a card edit | (a), (b) ordinary input; (c) constructed | (a), (b) Canvas action failed: @message=Access is denied. (os error 5); (c) Canvas action failed: @message=canvas action "edit \"Unfiled thought\"" rejected: unknown node "loose" | (a), (b) Canvas action failed: Access is denied. (os error 5); (c) the same without "@message=" |
| 17 | `SearchOverlayViewModel.HumanReadable` | a real vault and `VaultSearchSource`, typed queries | ordinary input | `AND`: Search error: Search query is invalid: FTS5 could not parse "AND": fts5: syntax error near "AND"; `unbalanced"`: Search error: Search query is invalid: FTS5 could not parse "unbalanced\"": unterminated string | Search error: FTS5 could not parse "AND": fts5: syntax error near "AND"; Search error: FTS5 could not parse "unbalanced\"": unterminated string |
| 18 | `GraphDocumentViewModel.HumanReadable` | the `links` table's root page zeroed in `.slate/cache.sqlite`, a session opened with no scan | constructed (the same damage fails the open's own scan first) | Graph diagram error: sqlite error: database disk image is malformed | unchanged (the `Db` arm already said core's words) |
| 19 | `GraphDocumentViewModel.FetchUnder` | the same damaged index | constructed | spoken: Couldn't load the graph: @message=sqlite error: database disk image is malformed; state: Graph error: @message=… | Couldn't load the graph: sqlite error: database disk image is malformed |
| 20 | `ConnectionsLeafViewModel.FetchUnder` | (a) `missing-note.md` opened in a tab; (b) the root note deleted on disk, a rescan, then Show Connections | ordinary input | Couldn't load connections: @path=missing-note.md, @reason=no such note in the graph under the active filter | Couldn't load connections: Invalid path missing-note.md: no such note in the graph under the active filter |
| 21 | `GraphConfigStore.Reason` | `.slate/graph.json` written as `{"version": 999}`, `not json`, `{"version": "x"}`, `[1, 2]` | ordinary input | Graph settings are read-only: graph.json is a newer version; not downgrading (@version=999); …: @reason=invalid JSON: expected ident at line 1 column 2; …: @reason=version "x" is not a number; …: @reason=expected a JSON object at the top level | …: graph.json is a newer version (999); not downgrading; …: graph.json is unparseable: invalid JSON: expected ident at line 1 column 2; …: graph.json is unparseable: version "x" is not a number; …: graph.json is unparseable: expected a JSON object at the top level (the probe printed the bare reason; the lead is Design 3's) |
| 22 | `GraphConfigStore.Read` | the file with a byte that is not UTF-8; a folder at its name; a file over 1 MiB | ordinary input | Graph settings are read-only: graph.json is unreadable: Unable to translate bytes [FF] at index 45 from specified code page to Unicode. (and the other two) | unchanged (ordinary exceptions) |

The other reads (`ConnectionsLeafViewModel.cs:899`, `:912`; `GraphDocumentViewModel.cs:1859`) take an ordinary exception's message: at `:899` and `:1859` only an `ObjectDisposedException` at close, which each receiver rejects (by reading), and at `:912` only the test gate's throw. The search's other variants: `Db` from a typed query (`foo:bar`, `-x`) says "Search error: sqlite error: no such column: foo" on main and head alike (Find 2); `Unsupported` needs File scope, which the shell never sets (`SearchOverlayViewModel.cs:475`, core raises it only there); search raises no `Io` (by reading); `Cancelled` publishes nothing.

**Step 0 settles:**

1. The dump is real at every CB-15b site that a real refusal reaches; head is the same lead with core's detail. Sites 18 and 22 do not change.
2. Only one reachable sentence changes beyond the dump: the search's invalid query (site 17). On the graph, no input reaches the `InvalidQuery` arm: core's graph filter is three booleans (`crates/slate-core/src/graph.rs:233-237`) and `start_layout` (`session.rs:2571-2594`) fails only with `Db` (by reading), which the probe confirmed with the one driver it found (site 18). One more text changes and is never published: a `Cancelled` at the graph table, the diagram build or Connections says "Exception of type 'uniffi.slate_uniffi.VaultException+Cancelled' was thrown." on main and "Operation cancelled." on head (constructed exceptions, w2-graph run 4); by reading those loads' tokens cancel only at close (`_lifetime`) or with the rescan source (`VaultLifecycleViewModel.cs:1347-1360`, disposed after each run, `VaultLifecycleViewModel.Rescan.cs:405-424`), and each receiver rejects a retired document's envelope.
3. Many of core's details at these sites are developer text: a handle number, Rust's debug quoting with a backslash, "unknown node", "sqlite error: …", and "unknown special query: " with nothing after it. That is #2012's second half; each is a data point for it, not reworded here (the lead's brief; Call 2).
4. No site needs a census exception, and the three folders hold no sentence the census does not see.

**Which exceptions each site can receive** (CB-15a's §12 row asks; by reading the try bodies). No CB-15b site can receive a `CommandException`, a `HotExitException` or an `AggregateException` (no `Wait` or `Result` in any try body; the search awaits, which unwraps). A `GraphConfigException` reaches only `GraphConfigStore.Reason`, which reads its typed fields and never calls the helper (Design 3). `OpenFailed`, `RetargetAbsent` and the search's broad catch can receive a `PanicException` or `InternalException` from the binding; its `Message` is the panic's text, not a field dump, and the helper returns it unchanged. The search and the three graph loads can receive an `ObjectDisposedException` during a close; each receiver rejects that envelope or publication. So no CB-15b site can hand the helper an exception whose `Message` is a dump of its fields, except a `VaultException`, which the helper renders.

## Design

### 1. The canvas's 16 reads

Each `e.Message`, `e.Message ?? string.Empty` and `exception.Message` in the 16 rows becomes `FailureDetail.Of(e)` (or `exception`): the 13 verbs in `CanvasDocumentViewModel`, `CanvasPhrase.OpenFailed` and `.RetargetAbsent`, and `CanvasMutationFunnel.Transact`'s `refusal`. The leads, the events, the priorities and the banner do not change. `TryWhereAmI` keeps its `out string detail`. `RequestVaultPick` calls the helper on its worker thread, as it reads `Message` there today (core's renderer is a plain FFI function).

### 2. The search's and the graph's arms go

- `SearchOverlayViewModel.PublishError` speaks `new A11yEvent.SearchFailed(FailureDetail.Of(failure))`; `HumanReadable` (`:903-916`) is deleted with its own summary, which held "the mac humanReadable arms". The comment inside `PublishError` (`:879-881`) keeps contract S6's point (the displayed and spoken lines are one template) and cites the template at `a11y.rs:2155`, not `:872`.
- `GraphDocumentViewModel.BuildDiagram`'s two catches return `FailureDetail.Of(exception)`; `HumanReadable` (`:1507-1516`) is deleted. The comments that say "humanised message" in the edited files say "the failure's detail (FailureDetail.Of)": `GraphDocumentViewModel.cs:741-742` (`DiagramError`), `:941` (`BuildDiagram`), `:908-912` (`GraphDiagramBuild`), and `GraphPublication.cs:89` (`Error`), a one-line comment in a fourth `Graph/` file.
- `GraphDocumentViewModel.FetchUnder` (`:1855`, `:1859`) and `ConnectionsLeafViewModel.FetchUnder` (`:894`, `:899`, `:912`) pass `FailureDetail.Of(exception)`.

What a reader hears changes only where step 0 showed it: the search's invalid query, and every vault error at the three graph loads and the 16 canvas reads, which lose the dump. The `Io`, `Db` and `Unsupported` arms were already core's words (step 0, sites 17 and 18, and constructed exceptions). A `Cancelled` at the three graph loads changes text but is never published (settles 2); at the search it is dropped before publishing (`:827`).

### 3. `GraphConfigStore.Reason` reads the typed fields

```csharp
/// <summary>The host-facing reason for each core refusal, from the typed
/// fields in the words the FFI crate gives the two variants (its
/// GraphConfigError Display, slate-uniffi lib.rs): never the binding's
/// field dump (#1890).</summary>
internal static string Reason(GraphConfigException exception)
{
    ArgumentNullException.ThrowIfNull(exception);
    return exception switch
    {
        GraphConfigException.NewerVersion newer => $"{FileName} is a newer version ({newer.version}); not downgrading",
        GraphConfigException.Unparseable unparseable => $"{FileName} is unparseable: {unparseable.reason}",
        _ => $"{FileName} is unreadable",
    };
}
```

The two texts are the FFI crate's `Display` for `GraphConfigError` (`crates/slate-uniffi/src/lib.rs:5326-5331`, `#[error("graph.json is unparseable: {reason}")]` and `#[error("graph.json is a newer version ({version}); not downgrading")]`), which the C# binding does not use for `Message` (it builds the field dump). They sit beside `Read`'s own "graph.json is unreadable: {detail}" (`:88`), so the three notices share one shape. No host shows the Mac's adapter text: the Mac's load catches every error and only marks the config read-only (`apps/slate-mac/Sources/SlateMac/Graph/AppState+GraphConfig.swift:30-39`), and its write logs the error (`Graph/GraphConfigStore.swift:155-165`); so the notice is Windows-only text either way (Call 3). The last arm is required: CS8509 is an error in the app (`S/SlateWindows.csproj:20`). It cannot run while the binding has two variants; a fact pins the variant set (Tests 2), so a third variant core adds fails that fact before a reader meets the arm, and the arm says the read arm's lead with no dump. `Read`'s ordinary-exception reason (`:88`) routes through the helper with the same text. `GraphConfigException` stays out of `FailureDetail.Of` (its documented limit; the remark in `FailureDetail.cs` gains "GraphConfigStore.Reason reads it by its typed fields").

### 4. The census

- `ShellFailureDetailCensus.Remaining` loses its 22 CB-15b rows; `RowCeiling` falls to 39 and `ReadCeiling` to 47. The baseline does not change: it records 2026-11-18.
- `Problems` refuses a row whose file is under `Canvas/`, `Graph/` or `Search/`, with "CB-15b routed …; they never stand as rows", beside CB-15a's sidebar rule; `TheTableIsHeldToTheReads` plants one such row in each of the three folders, so a rule that forgot one folder fails.
- `OwnerOf`'s CB-15b arm stays: the planted ratchet facts (`:274-326`) give `Canvas/` rows to CB-15b through `TableProblems`, which the new rule in `Problems` does not touch.
- The text that says what a row may not name says the three folders too: the header (`:18-26`), `Remaining`'s summary (`:87-89`), `TheTableIsHeldToTheReads`' summary (`:608-612`) and `Problems`' summary (`:871-875`). The header records the delivery: CB-15b routed its 26 reads on its merge date, 39 rows and 47 reads stand for CB-15c, and the trial's rule 2 hits in the three folders (20, none reaching text).

### 5. What this chunk does not change

Core's wording (Call 2), any lead (Find 3), CB-15c's rows, contract 38's ledger (no `A11yEvent` construction is added, removed or moved: each changes only an argument; the generator is run to confirm), the parity matrix (no chord or help line), any focus call, any Mac source. No test reads a Mac source, so no census trait is owed. No §6 shared file is edited: `ConnectionsLeafView.cs` and `GraphInspectorView.cs` render the new text without a change.

## Tests (red on main first, then green)

The red run is the new and changed tests on main's product code: the sites, the arms and `Reason` are main's. Every fact below fails there, on the dump, the lead or the unrowed reads; then the routing makes them pass.

1. **`T/CanvasFailureDetailTests.cs` (new),** converted from the canvas probe: one fact per row 1 to 16 except row 7, each driving the real catch with the probe's driver, asserting the exact spoken text (or the banner for 14 and 15), its priority, and no field token (`@\w+=`). Oracles: the lead plus `VaultErrorDetail` of the same core call made directly, where core's text holds a handle number or operating-system text; literals where core's text is fixed and locale-free (`InvalidUtf8`, `bad_node`, row 11's "sqlite error: locking protocol").
   - Row 7 (`CanvasRemoveFromGroup`) has no fact (Call 8): its catch wraps only `CanvasPlaceNew`, which refuses only `bad_handle`, and `CanvasParentOf` runs first on the same handle in the same lease hold (`CanvasDocumentViewModel.cs:3755-3789`), so only a close that bypasses the lease's lock mid-hold reaches it. The probe reached it by a background thread that closes core's handle after a swept busy-wait (up to 400 attempts; it hit at attempts 7, 2 and 9 here), which is a race whose hit rate on a loaded runner is unknown. The row's change is the same one line as rows 2 to 9, and the census holds it.
   - Row 11 holds `.slate/cache.sqlite-shm`'s WAL read-lock bytes from a second handle, so Add Note's file list returns SQLITE_PROTOCOL after one SQLite retry loop, about 11 s; the probe's second wait (`Cb15bStep0CanvasProbe.cs:523-531`), which made the oracle exception from a direct call, goes, since the oracle is the literal. The test project has no long-fact convention, so the fact's summary names the wait (Call 6).
2. **`T/GraphSearchFailureDetailTests.cs` (new),** from the graph probe:
   - Search: a real vault and the real source, `AND` and `unbalanced"`, each asserting the summary and the spoken text are "Search error: " plus core's detail with no "Search query is invalid"; red on main. A constructed `InvalidPath` through the overlay's fake source says core's detail (red: the fallback's dump). `Io`, `Db` and `Unsupported` through the fake source say what they said (regression, not red).
   - Graph diagram build: the existing `FetchGateForTests` seam throwing `VaultException.InvalidQuery("bad")` gives `DiagramError` "bad" (red: "Search query is invalid: bad"), and `InvalidPath` gives core's detail (red). The damaged index gives the `Db` text (regression).
   - Graph table: the damaged index; the spoken line and the state text say core's detail (red).
   - Connections: `missing-note.md`; the spoken line and "Connections error: …" say core's detail (red).
   - Graph config: the four `GraphConfigException` files give Design 3's notices (red); the three ordinary failures keep their text (regression). `GraphConfigException`'s nested variant types are exactly `Unparseable` and `NewerVersion` (green on main; it guards Design 3's last arm).
3. **The census** (Design 4): the 22 rows deleted and the ceilings lowered make `NoShellFailureBecomesTextOutsideFailureDetail` red on main (26 unrowed reads). The folder rule lands with its planted rows in the same test commit, so it is proved by its mutations (the rule removed; one folder dropped), not by the red run.
4. **The Connections model's oracle** (`T/ConnectionsLeafTests.Model.cs:2001`) renders `SlateUniffiMethods.VaultErrorDetail(failure)`, core's renderer, not `FailureDetail.Of`, so the oracle does not call the routine under test and the mutation of `FailureDetail.Of` cannot leave the leaf and its oracle agreeing. The model's `Missing` cells reach that catch (Current state). Measured on main with the new oracle: `TheModelOfTermsTwoToNineDerivesEveryRoutesTimelineAcrossEveryState` is red (520 of 12,388 cells diverge, each on "@path=missing.md, @reason=…" against core's "Invalid path missing.md: …"), and the `Composed` and `ReRoot` model facts stay green, since none of their cells speaks that line. The three facts took 24 min 49 s together.
5. **Regression, not red:** `ShellFailureDetailCensus`, `BasesErrorDetailCensus`, `VaultErrorDetailCensus`, `FailureDetailTests`, `SearchOverlayViewModelTests`, `SearchOverlay*`, `GraphDiagramTests*`, `GraphDocumentTests`, `GraphTableTests*`, `ConnectionsLeafTests*`, `ConnectionsLeafViewTests`, `GraphPreferencesTests`, `GraphConfigStoreTests`, `GraphInspector*Tests`, `CanvasDocumentTests`, `CanvasMutationFunnelTests`, `CanvasMutationTests`, `CanvasLoadPipelineTests`, `A11yCorpusCensus`, and `python scripts/a11y_trigger_ledger.py --check` (no drift).

Mutations (each must turn the named fact red; the PR lists each with its result). Each site fact is its own site's mutation: that site left on `Message` turns its fact red. Beyond those, one per family is run and recorded:

- a canvas verb (row 2), `OpenFailed` (row 14), `RetargetAbsent` (row 15) and the funnel (row 16) each left on `Message`: its fact and the census's main fact;
- the search's `InvalidQuery` arm restored: the search facts;
- the graph's `HumanReadable` restored with its fallback: the diagram build's `InvalidQuery` and `InvalidPath` facts;
- the graph table's and Connections' reads left on `Message`: their facts and the Connections model classes;
- `Reason` back to `exception.Message`: the config facts; `NewerVersion` worded the old way: its fact;
- the census's folder rule removed, and the rule with one folder dropped: the planted rows' fact;
- one CB-15b row left in the table: `TheTableOnlyShrinks` (ceilings) or the stale-row check;
- `FailureDetail.Of` returning `failure.Message` for a vault error: the facts in 1 and 2, the Connections model classes and `FailureDetailTests`.

The census alone holds five reads: four whose text does not change (`GraphConfigStore.cs:88`, `ConnectionsLeafViewModel.cs:899` and `:912`, `GraphDocumentViewModel.cs:1859`, ordinary exceptions) and row 7, which has no fact.

## The NVDA spot check

Under the desktop lock, NVDA with debug logging, base (main) and head Release builds, through `scripts/windows/launch-for-field-pass.ps1 -Root`, NVDA's punctuation level recorded. By keys; the routes are by reading and are confirmed before the pass:

1. **Search, the changed sentence.** Ctrl+Shift+F, paste `AND` (pasting avoids searching "A" and "AN" first). Base: "Search error: Search query is invalid: FTS5 could not parse "AND": fts5: syntax error near "AND"". Head: "Search error: FTS5 could not parse "AND": fts5: syntax error near "AND"".
2. **Connections, a graph-side line.** Open `hub.md` with the Connections leaf showing; delete `hub.md` in Explorer; Ctrl+Shift+P, "Show Connections", Enter. Base: "Couldn't load connections: @path=hub.md, @reason=no such note in the graph under the active filter". Head: "Couldn't load connections: Invalid path hub.md: no such note in the graph under the active filter". (Step 0 drove this with a manual rescan; the running app's watcher is by reading.) If the deleted note's tab does not survive and Show Connections roots elsewhere, the pass tries the second route, by reading only: close Slate with `hub.md` open and the leaf showing, delete `hub.md`, relaunch; the restored tab fails to reopen and the mounted leaf speaks the same line. If neither route is heard, the PR records how far the pass got and that the hosted fact alone covers the line.
3. **Canvas, a spoken refusal.** Open a canvas, keys on its outline; `attrib +r` the file; New Card (Ctrl+Alt+T, registered at `Commands/ChordTable.cs:1389-1392`). Base: "Canvas action failed: @message=Access is denied. (os error 5)". Head: "Canvas action failed: Access is denied. (os error 5)".
4. **The graph inspector's notice.** Before the vault opens, write `{"version": 999}` to `.slate\graph.json`; open the graph and its inspector; read the notice by object navigation (it is a text element, never announced). Base: "Graph settings are read-only: graph.json is a newer version; not downgrading (@version=999)". Head: "Graph settings are read-only: graph.json is a newer version (999); not downgrading".

The graph's invalid-query sentence is not in the pass: no input reaches it (step 0, Call 1). The PR body records each phrase verbatim, base and head; a phrase not reached by keys is recorded as not reached, with how far the pass got.

## Files

Product: `S/Canvas/CanvasDocumentViewModel.cs`, `S/Canvas/CanvasMutationFunnel.cs`, `S/Graph/ConnectionsLeafViewModel.cs`, `S/Graph/GraphConfigStore.cs`, `S/Graph/GraphDocumentViewModel.cs`, `S/Search/SearchOverlayViewModel.cs`, one comment line of `S/Graph/GraphPublication.cs`, and one remark line of `S/FailureDetail.cs`. Tests: new `T/CanvasFailureDetailTests.cs` and `T/GraphSearchFailureDetailTests.cs`; `T/Censuses/ShellFailureDetailCensus.cs`; `T/ConnectionsLeafTests.Model.cs` (one line); `T/CanvasMutationTests.cs` (one comment, Find 4). Docs, if Call 4 is ruled yes: `docs/plans/29_search_overlay_contracts.md` and `docs/plans/35_graph_contracts.md`, with amendment lines added and no existing text changed. New `.cs` files: `git add`, then `python3 scripts/apply-license-header.py`. `dotnet format --verify-no-changes --include` on every edited C# file. The three step 0 probes are deleted before the first commit (the canvas probe is in this worktree; the graph probes are in the detached step 0 worktree, removed with it).

## Overlap

- **CB-31 and CB-29 (same wave).** CB-31 is Rust and Swift; CB-29 edits `diagram.rs`, `EditorInteractions.cs` and the SVG guard. No file is shared. One overlap is behavioural: CB-31 changes `open_canvas`'s first-index arm (principal T's step 0), and rows 14 and 15 drive `open_canvas` with literal oracles. The risk is low, because the fixture scans before it opens, so the files are indexed already; and CB-31's change under `crates/` runs the whole Windows lane over these facts, so whichever merges second sees them on the merged tree. `EditorInteractions.cs` holds CB-15c's row `ResolveEmbedPreview`; once this PR's census is on `main`, a read CB-29 added or moved there would fail it, as it would today. Whichever merges second merges `main` and runs the census.
- **CB-15c (C9)** inherits 39 rows and 47 reads, all its own, and deletes the row mechanism once the table is empty.
- **Plan §6.** The five shared files of `:750` (`VaultLifecycleViewModel.cs`, `BulkRenameViewModel.cs`, `BaseSurfaceView.cs`, `ConnectionsLeafView.cs`, `GraphInspectorView.cs`) are not edited. `CanvasDocumentViewModel.cs` is in no §6 row; CB-36 and CB-37 (C13, C15) edit the canvas renderer files, not it.

## Docs and registers

- No contract register row names these sentences as delivered text. Two contract records describe the arms CD9 removes. Contract 29's S6 cites the `SearchFailed` template at `a11y.rs:872` (now `:2155`), and its divergence register has no row for the Mac's lead. Contract 35 says the graph build's failure installs "DiagramError (the humanised message)" in four places: Term G2 (`35_graph_contracts.md:12536-12538`), TGD-2's record (`:13804-13806`) and its note (viii) (`:13937-13938`), and the task loop's record of the `VaultException` arms as a host mapping on both platforms (`:17333-17338`). Call 4.
- #1527: one comment. Windows now says core's detail for search and graph errors (CD9). The Mac's instance `humanReadable` (`apps/slate-mac/Sources/SlateMac/AppState.swift:26004-26025`), which serves the search (`:9900`), the graph table, the diagram and Connections, still adds "Search query is invalid: " to an `InvalidQuery` (`:26018-26019`) and "Invalid argument: " to an `InvalidArgument` (`:26022-26023`); its static `humanReadableVaultError` (`:14019-14023`) passes both bare, as core does. Plan §8 asks for this note.
- #2012: one comment with step 0's developer-text details (Call 2).
- #1956: the canvas's `OpenFailed` joins the existing line from CB-15a's comment (a lead that repeats a path its detail names): "This canvas could not be opened: File at bad.canvas is not valid UTF-8. (bad.canvas)" (Find 3). And one new line: the graph config's notices are host text, and the FFI crate already words both variants in its `Display`, which core could export (`graph_config_error_detail`, the reading's §2 table).

## Ledgers

- #1954 (Mac work owed): one line: the Mac's `humanReadable` arms for search and graph errors adopt core's detail, as Windows does since CB-15b (CD9, with #1527). Nothing on the Mac changes in this PR. The PR body says `not delivered: VoiceOver pass (no Mac speech changes here; the Mac's adoption is #1954's line)` (Call 5).
- #1955 (deferred finds): none planned. Find 1 is filed or deferred by Call 7.

## Corrections and the feature's own changes (not finds)

- **The graph's invalid-query sentence cannot be reached** (step 0). A correction to the plan's "on search and on graph", and a departure from the owner's words (Call 1).
- **The Connections model's oracle mirrors the dump** (`ConnectionsLeafTests.Model.cs:2001`). The feature must change it; Tests 4.

## Finds

1. **A canvas that never opened says "The previous snapshot is read-only." after a rename.** The host shows core's `ReopenFailed` sentence (`a11y.rs:3275-3278`) whenever the document was retargeted (`SessionSource.FailureFor`, `CanvasDocumentViewModel.cs:2354-2359`), though a canvas that failed to open had no snapshot (step 0, site 15a). The fix is in a file this PR changes, so it is Small by files. But it chooses the load state by whether a snapshot was published, which changes contract 34's A3 table ("a retarget's reopen threw → `RetargetAbsent`", `34_canvas_contracts.md:1009-1015`) and the banner a reader meets, and a fix that changes a contract row and a spoken sentence is never small or medium (AGENTS.md): High, so filed (Call 7). The failing test that would show it: open a `.canvas` with a byte that is not UTF-8 (`Failed`), retarget it to a new name, and assert the banner does not say "The previous snapshot is read-only.".
2. **A search with a column filter (`foo:bar`, `-x`) is classified as a database error:** "Search error: sqlite error: no such column: foo" (step 0). Core's query-problem markers (`search_db.rs:403-406`) miss "no such column". Core's, and the Mac's too; a #2012 data point (Call 2).
3. **"This canvas could not be opened: {detail} ({path})" repeats the path and follows a full sentence** ("…is not valid UTF-8. (bad.canvas)"). The lead predates this PR (§W-G row S keeps it host text, and CD9 keeps leads); this PR only removes the dump inside it. A #1956 line, added to CB-15a's.
4. **`CanvasMutationTests.cs:2610-2616` says no session fault is inducible from a real vault** for the four verbs' prepares (`:2621`). Step 0's rows 3 to 6 drove each of those prepares to its catch with a real vault (the canvas handle closed behind the lease). Small, a test comment in a file the PR changes: the comment says the refusal is constructed and points at the new facts; a "Found and fixed here" line.
5. **Core's canvas, search and lock details read as developer text** (step 0, settles 3): a handle number, Rust's debug quoting with a backslash, "unknown node", "sqlite error: …", "unknown special query: " with nothing after it. #2012's second half; a comment there (Call 2).

## Verification

```
# hosted facts and censuses: red on the tests-only commit, green on the fix
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~CanvasFailureDetailTests|FullyQualifiedName~GraphSearchFailureDetailTests|FullyQualifiedName~Census|FullyQualifiedName~FailureDetailTests|FullyQualifiedName~ConnectionsLeafTests.Model"
# the classes around the sites
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~SearchOverlay|FullyQualifiedName~Graph|FullyQualifiedName~Connections|FullyQualifiedName~Canvas"
python scripts/a11y_trigger_ledger.py --check
python -I scripts/graph_reconciliation.py --check   # contract 35's generated index pins each register head's line
# the whole unit run under the desktop lock before the push (c8-process step 10)
```

## Acceptance

- Every fact above red on the tests-only commit and green on the fix; every mutation red; the regressions green; the trigger ledger unchanged (its keys are `Member@Ordinal`, and no construction site is added, removed or reordered).
- The NVDA phrases recorded for base and head.
- `gh pr view N --json closingIssuesReferences` is empty.
- PR title: `fix(windows): canvas, graph and search failures say core's words, and the search and graph error arms go`. Predicted count: found 5, fixed here 1 (Find 4), deferred 0, filed 1 (Find 1, by Call 7); Finds 2 and 5 are #2012 comments and Find 3 a #1956 line.

## Calls for the lead

1. **The graph half of the plan row's NVDA item, a departure from the owner's words.** CD9 as answered says "CB-15b checks that sentence on both surfaces under NVDA", and the owner's #1890 comment says the same. No input reaches the graph's invalid-query sentence (core's graph build fails only with `Db`), and on head the sentence no longer exists: the arm is gone, and a build's `InvalidQuery` would say core's bare detail. Recommended: no field-pass seam; a hosted fact through the existing `FetchGateForTests` seam pins the head text, and the NVDA pass hears two graph-side lines that real input reaches (the Connections failure and the inspector's notice). The PR body, and §12's row, record it as a departure: `not delivered: the graph's invalid-query sentence under NVDA (no input reaches it, and head has no such sentence)`. Cost: the owner's "both surfaces" is met on search only. The alternative, a test-fault seam in the graph build (CB-27's precedent), is a product seam that could only verify a sentence head no longer has, and the diagram's error is a state name, never announced.
2. **Core's developer-text details stay as they are here.** The new search sentence is "Search error: FTS5 could not parse "AND": fts5: syntax error near "AND"", and the canvas's are "… failed: unknown canvas handle 1 (closed or never opened)" and the like. Recommended: no core change in this chunk; one comment on #2012 with step 0's table, including Find 2 (a column filter classified as a database error). Cost: until #2012's fix, a reader who mistypes a search hears "FTS5 could not parse" with no word "invalid". The alternative, a core reword of the search's `InvalidQuery` text now, changes `search_db.rs` and so the Mac's sentence (its arm keeps "Search query is invalid: " before core's message), starts the Rust and Mac lanes, and owes a VoiceOver line.
3. **The graph config's notices use the FFI crate's `Display` words for both variants:** "graph.json is a newer version (999); not downgrading" and "graph.json is unparseable: {reason}". The notice is Windows-only text: the Mac shows no reason anywhere (its load only marks the config read-only, its write logs). Recommended: yes; the words already exist in the crate, the three notices share "graph.json is …" with the read arm's "graph.json is unreadable: …", and no new copy is written. Cost: the new-version sentence's order changes beyond losing "@version=", and the unparseable one gains a lead. Alternatives: the Mac adapter's bare reason for `Unparseable` ("invalid JSON: expected ident at line 1 column 2", which no host shows today and which reads unlike its two siblings); or Windows' old order, "graph.json is a newer version; not downgrading (version 999)", a third wording.
4. **Append CD9 to the two contract records.** Recommended: yes, as amendment lines that add the owner's recorded call and change no existing text: a divergence row in contract 29 ("SD-6: Windows says core's detail for a failed search; the Mac's `humanReadable` adds "Search query is invalid: " (CD9, 2026-11-16)"), and in contract 35 one line beneath each of the four passages ("Amended by CB-15b under CD9 (2026-11-16): the failure is core's `vault_error_detail` through `FailureDetail.Of`; the `HumanReadable` arms are gone"). Cost: two docs files in a code PR (the Windows lane already runs in full). The alternative leaves both records describing code that no longer exists.
5. **The Mac's arms: a #1954 line, no VoiceOver pass owed here.** Recommended as written under Ledgers. Cost: none beyond the line.
6. **Row 11's fact (about 11 s).** Recommended: keep it, with the literal oracle and one SQLite retry loop, its wait named in its summary; it is the only real refusal that reaches `RequestVaultPick`. Cost: about 11 s of the unit run, and a fact that depends on SQLite's WAL lock protocol (a different SQLite build could answer another way). The alternative is census-only, as CB-15a left `TreeOperations.cs:309`.
7. **Find 1 (the never-opened canvas's "previous snapshot" sentence) is filed** (labels bug, windows), with its failing test, not fixed here. Recommended: file, since it changes a contract row and a sentence (AGENTS.md: High). Cost: one new issue; a reader who renames a canvas that never opened keeps hearing about a snapshot that does not exist. The alternative, if the lead reads it as Medium, is a #1955 line for CB-46, which adds no issue.
8. **Row 7 (`CanvasRemoveFromGroup`) is census-only.** Recommended: yes; its catch is reachable only by a close that bypasses the lease's lock mid-hold, which production does not do, and its one-line change matches rows 2 to 9, whose facts pin the helper's use. Cost: no fact proves row 7's sentence. Alternatives: keep the probe's bounded race as a fact (it hit at attempts 2 to 9 here; on a loaded hosted runner the hit rate is unknown, so the lane could go red on a miss); or a test seam between `CanvasParentOf` and `CanvasPlaceNew` (a product seam for a path no reader meets).

## Red team record

A fresh Opus red team read the first draft on 2026-11-19 against the worktree, the step 0 probes and their outputs (its report and the first draft were kept outside the repository): 0 blockers, 4 major, 15 minor. All taken.

- **M1, the Mac-parity premise of Call 3 was false (taken):** the Mac shows no graph config reason to a reader; Design 3 now uses the FFI crate's `Display` words for both variants, Call 3 says the notice is Windows-only text, and the #1956 line names the crate's wording.
- **M2, the Connections model reaches its oracle's catch (taken):** settled by reading (the `Missing` cells); the oracle uses core's renderer, not the helper; the three model classes join the red run.
- **M3, Call 1 departs from the owner's words (taken):** Call 1 says so, and that head has no graph invalid-query sentence to verify; the `not delivered` line and §12's row record it as a departure.
- **M4, row 7's fact was a race (taken):** row 7 is census-only by Call 8, with the race and a product seam as the alternatives.
- **Minors (taken):** the unpublished `Cancelled` text change; one planted row per folder, the census's four texts, and `OwnerOf`'s arm kept; row 11's literal oracle, one retry loop, the named `-shm` file and no claimed convention; Find 4's evidence (rows 3 to 6); Design 2's comment edits (`a11y.rs:2155`, the four "humanised" comments, one in `GraphPublication.cs`); contract 35's four passages and "amendment lines, no text changed"; Find 1's sizing (High, filed, with its failing test); Find 3 joins CB-15a's #1956 line and says why it is not this PR's feature; the count (corrections and the oracle are not finds); the Mac's one instance `humanReadable` and its bare static twin; mutations per family and the five reads the census alone holds; the fallback's reach and the two mirrored-switch facts; CB-31's behavioural overlap; NVDA route 2's fallback and the chord's registration line; CS8509 makes Design 3's last arm compulsory, and no XAML sits in the three folders.

## What delivery changed (2026-11-19, PR #2020, merged as 15cba369)

The PR merged at 43992f2e, which contained `main` 16ad788d. It referenced #1890 and did not close it; CB-15c continues that issue with the census's last 39 rows and 47 reads.

**What the code does that the spec did not say.**

- Contract 35 carries a generated reconciliation index that pins each register head's line, so the three amendment lines moved 40 of its pointers. `scripts/graph_reconciliation.py --write` regenerated them, and the Windows app job's `--check` holds them (the diff red team's blocker). The spec's Verification had not listed that check.
- The amendment lines say "core's `vault_error_detail` for a vault error, and any other exception's own message", where the first draft said core's detail alone (the diff red team's minor 5).
- The graph config's unparseable facts take the parser's reason from core's codec called directly, so a `serde_json` wording change does not fail a Windows fact; the newer-version fact keeps its literal, which guards the word order.
- The damaged-index fact checks two premises before it patches: the closed session left no WAL to replay, and the byte it zeroes is a b-tree page type. The first version read the WAL's length when no WAL existed and failed on that (seen in the first green run, fixed before the commit).
- Row 11's release timer cannot end the test process: both of its unlocks catch a disposed stream.
- The census's header names all five reads it alone holds (four ordinary-exception reads and `CanvasRemoveFromGroup`, with the race the lease's lock forbids), where the first text named one.
- `CanvasFailureDetailTests` has 17 facts: the open failure and the funnel each have two drivers.

**What was measured that the spec expected otherwise.**

- The red run on `main`'s product code: 29 new facts and the census's main fact (30 failures). The folder rule cannot be red on `main`, since it lands with its planted rows in the test commit; its two mutations prove it.
- The Connections model: one of the three facts is red on `main` with the new oracle (`TheModelOfTermsTwoToNine…`, 520 of 12,388 cells); the `Composed` and `ReRoot` facts stay green. The three took 24 min 49 s.
- Green on the fix: the new facts with every census, 1,589 of 1,589; the whole unit run under the desktop lock, 7,873 passed, 2 skipped, 0 failed (1 h 25 min). All 14 mutations turned their named facts red. CI on 43992f2e passed every lane that ran.
- NVDA 2026.3.1 heard every expected phrase, base and head, on all four steps: the search's `AND`, Connections after the root note was deleted on disk, New Card on a read-only canvas, and the inspector's notice by object navigation. The deleted note's tab survived the delete, so the relaunch route was not needed; the Connections leaf's own name also moved from the dump to core's words ("Connections error: Invalid path hub.md: …").
- The graph's invalid-query sentence was not heard (Call 1, as ruled): no input reaches it, and on head it no longer exists. The PR body records the departure.

**Found on the way.** Count: found 7, fixed here 1, deferred 0, filed 3.

- Fixed here: `CanvasMutationTests`' comment that no session fault is inducible from a real vault.
- Filed #2019 (Find 1): a canvas that never opened says "The previous snapshot is read-only." after a rename.
- Filed #2021 (found by the NVDA pass): a launch is silent at random under NVDA. On both builds Slate raises its launch lines a few milliseconds before or after its window's focus event, and NVDA drops any that arrive before it (two `main` launches silent, one CB-15b launch spoken). Five more launches could not take the foreground (Windows' foreground lock, by inference recent desktop input), which the issue's comment records.
- Filed #2022 (found by the merge judge, by reading): a canvas undo or redo that core refuses says nothing (`CanvasMutationFunnel.cs:318-322`).
- Recorded on existing issues: core's developer-text details and a search column filter classified as a database error (#2012, one comment with step 0's table); the canvas open failure's lead that repeats its path, and the graph config's host-composed notices (#1956).

**Reviews and records.**

- Spec red team: 0 blockers, 4 major, 15 minor; all taken. The lead ruled the eight calls as recommended.
- Diff red team: 1 blocker, 5 minors, 1 nit; all taken.
- Codex round 1 on 43992f2e: approve, no material findings.
- Merge judge: Merge, no blocker or major. Its minor 1 (the mutation line) was met by the mutation run, its minor 2 is #2022, and its nit on the fact count was corrected in the PR body.
- Ledger comments: #1954 (the Mac's search and graph `humanReadable` arms adopt core's detail), #1527 (the note plan §8 asks for), #1956 and #2012.
