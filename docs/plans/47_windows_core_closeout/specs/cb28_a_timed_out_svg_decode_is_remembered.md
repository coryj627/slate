# CB-28: A timed-out SVG decode is remembered, and a projection's decodes have a deadline

Issue: #1635. Size M. Lanes: windows (the app job; a change under `crates/` runs the lane in full), rust (one sentence form and its fact), swift (compile and the lane's suite; no Mac here). AT: an NVDA spot check in the reading view, base and head. Wave: C7, slot 2. Depends on: #2008 (CB-14) merging first, because both edit `crates/slate-uniffi/src/lib.rs` (plan §6); this chunk branches from `main` after that merge (Call 6). Parallel with: CB-15a (the Files sidebar's failure detail and `ShellFailureDetailCensus`) and CB-24 (the note header's menu, the gate). Decision: CD19, answered 2026-11-16 as recommended (plan §3.1): a timed-out decode is remembered for the life of the process, keyed by a hash of the bytes, 256 entries; a projection's decodes share a 20-second deadline; its sentence is "Image not shown: this note's images used their 20-second decoding budget." This reverses the retry #1908 pinned (plan §11). Interlock: `diagram.rs` and `EditorInteractions.cs` go CB-27 (merged), CB-28, CB-29, then CB-15c and CB-22 (plan §6). Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9).

Written 2026-11-18 from `reading/06_reading_view_diagrams.md:85-117`, the issue and its owner comment, PR #1908's body, and a step 0 run. Every `path:line` below was re-read at 26d353ef (`main` on 2026-11-18, after #2007). Step 0 ran at 26d353ef in this chunk's worktree, detached at `origin/main`, with a Cargo target directory of its own, bindings regenerated with `-Locked`, a Release build (0 errors), and probe facts in an untracked file that is deleted before the branch is cut. The probe source and its raw output are kept with the chunk's working notes, and the numbers are in the step 0 comment on #1635. A fresh Opus red team read the first draft against the tree; its findings are folded in (Red team record). Statements still by reading say so.

`S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`; `D` is `crates/slate-core/src/diagram.rs`; `RCVM` is `S/Reading/ReadingContentViewModel.cs`; `RDB` is `S/Reading/ReadingDocumentBuilder.cs`; `EI` is `S/EditorInteractions.cs`; `RPPVM` is `S/Panels/RightPanePanelsViewModel.cs`.

## Goal

An SVG whose decode ran out its 5-second budget is not decoded again in this process: once that first timeout has landed, a repeat is refused at once, before a slot or a thread, with the sentence it got the first time. A note's SVG decodes, in the reading view, the embeds leaf and the Ctrl+E popover (Call 1), share 20 seconds: each waits for its own budget or what is left of the 20, whichever is shorter, and once the 20 seconds are used the rest say "Image not shown: this note's images used their 20-second decoding budget." without entering the decoder. A decode refused as busy is still retried by the next refresh.

## Drift check (the reading against 26d353ef)

The reading was written at 1ec9bf31. What moved, and what still holds:

| Reading's claim | At 26d353ef |
|---|---|
| `DecodeSvgImage` at `EI:3306-3434` | `EI:3314-3444`. #1950 added the process-wide default seam `SvgDecodeBudgetDefaultMillisecondsForTests` (`EI:3267-3273`), read at `EI:3408-3410`; the unit-test host sets it to 20 s as it loads (`T/Support/UnhurriedSvgDecodes.cs:34-37`) |
| The busy refusal at `EI:3314-3320` | `EI:3321-3328` |
| The stale comment at `EI:3392-3399` ("the reading view decodes on the dispatcher") | `EI:3400-3407`, still stale. A second stale copy, not in the reading: `apps/slate-windows/tools/SvgDecodeProbe/Program.cs:16` ("The reading view decodes diagrams on the dispatcher thread, an STA") |
| The per-thread budget seam at `EI:3257-3265`, read at `:3400` | Holds at `:3257-3265`; read at `:3408-3410` |
| `FetchEmbedResolutions` decodes at `RCVM:1110-1146`; serial at `:1043`, `:1126` | `RCVM:1103-1146`; the loop `:1043`, the pool check `:1116-1119`, the decode `:1126-1143`. Holds |
| The digest after the fetch, `RCVM:958-968`; the decode field `:2363-2370` | Holds. The reserved pixels are a second decode field, `:2374-2376` |
| Caps `:883` (128 embeds), `:888` (16 MiB), `RDB:1297` (16 MP) | Holds |
| Diagrams: non-transient `Failed` at `RDB:1160-1166`, landed at `RCVM:2099-2103` | Holds |
| `ARefreshWhoseSvgDecodeNowSucceedsShowsTheImage` at `T/ReadingSvgEmbedDecodeTests.cs:85` | `:88` (doc comment `:83-86`) |
| `D:263-283` forms; `"note"` at `:266-269` | Holds |
| `lib.rs:8035` doc comment | `crates/slate-uniffi/src/lib.rs:8072-8079`. It lists four forms and omits `"image-busy"`, which core has had since #1581 |
| `render_time_exceeded_message_has_its_four_forms` at `D:1855` | `D:2459-2492`; it already pins six cases of five forms |
| The editor's Ctrl+E preview and the embeds leaf decode on workers (`EI:1538-1566`, `RPPVM:725`) | Holds, by two routes. The popover's worker (`EI:1449`, always `Task.Run`) calls `BuildEmbedPreview` (`EI:1566`, `:1714-1748`), which calls `BuildEmbedNode` directly (`:1722-1726`) with a per-card pixel budget. `BuildEmbedPreviewNode` (`EI:1800-1803`) has two callers, both the leaf's (`RPPVM:725`, `:1326`). Both routes reach `BuildImageNode` (`EI:1883-1909`) through `BuildEmbedNode`'s recursion, so a card's nested images decode one after another (step 0, P5) |
| (not in the reading) | The embeds leaf resolves and decodes every embed of the active note on every activation, whether or not the leaf is showing: `SyncPanels` (`S/WorkspaceViewModel.cs:2990-2996`) calls `Panels.NoteChanged`, which runs `LoadLinks`, `ResolveEmbeds` and `ResolveEmbedBatch` (`RPPVM:394-436`, `:532-558`, `:581-729`) on a pool worker. So opening a note with SVG embeds starts two decode streams of the same bytes, the leaf's and the reading view's, which share the process's slots (`EI:3229`) |
| No production path waits on an SVG decode on the dispatcher | Holds by reading: the two production `ReadingDocumentBuilder.Build` callers (`RCVM:1702`, `:1854`) pass no diagram artifacts (`RCVM:1694`), and `AppendEmbedImage` places a worker's decode (`RDB:1787-1797`); its dispatcher branch serves only builds with no fetch behind them, which only tests make |
| The Mac has no consumer of `render_time_exceeded_message` | Holds: no `renderTimeExceededMessage` under `apps/slate-mac/Sources` |

Since the reading, #1978 (CB-27) changed `D` (the panic and poisoned-lock rule, `D:516-545`), not the sentence function; #2008 (CB-14, open, about to merge) adds ten lines to `lib.rs` away from `:8072`.

## Step 0, as run on main (26d353ef), 2026-11-18

Probe facts in `T/Cb28Step0Probe.cs` (untracked). P1, P2 and P5 use the existing seams with each fact's own `SvgDecodeSlots`, a per-thread budget of 300 ms and a real Win32 wait. P0, P3 and P6 decode a real document: a `<marker>` of 1,500 one-pixel rects replayed at every vertex of a polyline, the shape #1908's field pass used.

- **P0, the real document's full decode time** (no budget): 1,500 × 1,000 vertices (84,649 bytes) 4,058 ms; × 2,000 (90,429 bytes) 6,792 ms; × 4,000 (101,879 bytes) 13,972 ms. Roughly linear in the vertices. The guard (`SvgPreviewGuard`) passes all three. Measured again in P6 half an hour later: × 2,000 took 8,712 ms and × 4,000 15,966 ms, so this desktop varies by about a quarter from run to run.
- **P1, one slow SVG, three refreshes** (body parked, 300 ms budget): open 457 ms, body called 1; refresh 1: 310 ms, body called 2, abandoned 2, not rebuilt, "took longer"; refresh 2: 2 ms, body still 2, **rebuilt**, the card now says "Image not shown: earlier images are still decoding past their 5-second budget."; refresh 3: 0 ms, busy sentence, not rebuilt. So every refresh decodes the same bytes again until the cap refuses them, and the card's sentence flips when it does. This is the reading's "No memory" paragraph, seen.
- **P3, the same with the real document at the app's own 5 s budget** (1,500 × 4,000, 101,879 bytes): open 5,336 ms; refresh 1: 5,114 ms (decoded again), abandoned 2; refresh 2: 4 ms, rebuilt, the busy sentence; refresh 3: 1 ms, busy. The two abandoned decoders ran until 27,890 ms after the open began.
- **P2, six distinct SVGs that each end 50 ms after a 300 ms budget**: one projection took 1,867 ms (about six budgets), six "took longer" sentences, no busy sentence; the abandoned count was at most 1 when a decode started, so the cap never engaged. At the app's 5 s budget that is 30 s for six images and, by arithmetic (not measured), up to 128 × 5 s for the 128 embeds a note may resolve. This is the reading's "No per-projection deadline" paragraph, seen.
- **P5, the card composition** (the leaf's entry, `BuildEmbedPreviewNode`, over a note card holding six distinct nested SVGs, the same body): 1,873 ms, body called 6. The popover's `BuildEmbedPreview` runs the same `BuildEmbedNode` recursion, so it has P2's shape too (by reading).
- **P6, where the marker's time goes**: the pixel reservation, which `RenderSvgToPng` makes after the parse and the picture and before the raster, ran at 7,996 ms of an 8,712 ms decode (× 2,000) and at 14,885 ms of 15,966 ms (× 4,000), each for 640,000 bytes. So for this document a decode abandoned at 5 s has not reserved, and the gate refuses its late reservation (`EI:3344-3348`).
- **P4, the memory key's cost**: SHA-256 of 166,912 bytes, median 0.067 ms; of 8 MiB (the decode's size ceiling, `EI:3141`), median 3.325 ms, max 3.595 ms (21 runs each). Hashing every SVG before its decode is affordable on the workers that decode.

## Current state on main (26d353ef), what the design changes

- **The decode** (`EI:3314-3444`). A refusal at the cap returns the busy sentence (`:3321-3328`). Otherwise a thread decodes (`:3358-3399`) and the caller waits `SvgDecodeBudgetMillisecondsForTests ?? SvgDecodeBudgetDefaultMillisecondsForTests ?? 5000` ms (`:3408-3412`). On `WaitTimeout` it counts an abandoned decode, and if `TryAbandon` wins (`:3418`) it logs `SvgDecodeAbandoned` with `bytes=…, budgetMs=…` and returns `SvgDecodeTimeExceededMessage` ("Image not shown: decoding took longer than 5 seconds.", `:3237-3239`); if the decode finished first it is read as an ordinary end (`:3426-3428`). Nothing is kept.
- **The callers.** The reading refresh's worker (`RCVM:1126-1143`), the diagram stage's worker through `ResolveDiagramOutcome` (`RDB:1154-1166`), the popover's worker through `BuildEmbedPreview` (`EI:1714-1748`), and the leaf's batch through `BuildEmbedPreviewNode` (`RPPVM:725`, one batch on one worker, `:581-729`).
- **Seams.** Per caller thread: `SvgDecodeBudgetMillisecondsForTests`, `SvgDecodeBodyForTests`, `SvgDecodeSlotsForTests`, `SvgDecodeWaitForTests` (`EI:3257-3312`). Process-wide: `SvgDecodeBudgetDefaultMillisecondsForTests` (#1950). The suite runs serially (`T/AssemblyInfo.cs`, `ParallelMode.None`). Every existing fact that forces a timeout sets its own `SvgDecodeSlots` (`W2EditorInteractionRedTeamTests.cs:538-557` through `WithSvgDecodeSeams`; `ReadingSvgEmbedDecodeTests.cs:97`; `ReadingEmbedTests.cs:357-358`; `ReadingDiagramTests.cs:495-496`).
- **The sentence forms** (`D:255-283`): `"diagram"`, `"note"`, `"busy"`, `"image"`, `"image-busy"`, and the diagram form for anything else. Windows reads them through `SlateUniffiMethods.RenderTimeExceededMessage` (`EI:3237-3247`, `RDB:1185-1187`).

## Design

1. **The memory.** A small class in `EI`, `SvgDecodeTimeoutMemory`: a `HashSet<string>` and a `Queue<string>` of the same keys in order of arrival, under one lock, holding at most `MaxRememberedSvgDecodeTimeouts = 256`; remembering a 257th drops the oldest; remembering a key already held changes nothing. The key is the upper-case hex of the bytes' SHA-256 (`Convert.ToHexString(SHA256.HashData(bytes))`, P4): a string, so the set compares by value. It lives on `SvgDecodeSlots`, beside `Abandoned` and `Live`, so the process's instance is the app's memory for the life of the process and a fact's own `SvgDecodeSlots` brings its own empty one. Only the caller writes it, after `TryAbandon` wins, never the decoder thread. Nothing clears it: closing a vault does not, and restarting Slate does. CD19's cost: a decode that timed out only because the machine was busy stays refused until its bytes change or the app restarts. The likeliest such decode is the first of a session, a cold Skia parse (#1949 saw a tiny SVG pass 5 s on a loaded job VM).

2. **What is remembered.** Only a decode abandoned at its own full budget: `WaitTimeout`, `TryAbandon` won, and the wait was not shortened by a deadline (point 4). Never a busy refusal, a wait that failed (`:3430-3434`), a decode that finished as its budget ended (`:3426-3428`), an ordinary failure (null with no reason), a decode the pixel pool refused, or a wait cut by the deadline.

3. **The order of checks in `DecodeSvgImage`.** First the memory: a key it holds returns `SvgDecodeTimeExceededMessage` at once, logs a new `HostDiagnosticEvent.SvgDecodeRefusedRemembered` with `bytes=…`, takes no slot, starts no thread and charges no deadline. Then the deadline (point 4). Then the cap, as today; core's order is the same, deadline then cap (`D:893-907`). The memory comes first so a remembered image says what it said the first time, whichever images were decoded before it in that call's projection. Two limits on that:
   - In the reading view the projection's pixel-pool check (`RCVM:1116-1119`) runs before the decode is asked for, so on a refresh where earlier SVGs have spent the pool a remembered image says the pool notice instead. Both sentences are true; the design does not reorder the pool check.
   - A remembered refusal reserves no pixels. If the timed-out decoder had reserved its pixels before its budget ended, the first refresh after it has a different digest (`RCVM:2374-2376`) and rebuilds once, with the same sentences; every refresh after that is a memo hit. P6 shows the field document reserves at about 92% of its decode, so a decode abandoned at 5 s has reserved nothing and even the first refresh is a memo hit. Fact 8 pins both shapes.

4. **The deadline.** A second small class in `EI`, `SvgDecodeDeadline`, created once per projection with `SvgProjectionDecodeDeadlineSeconds = 20` (or a seam, point 7). It holds the time left. `DecodeImage` gains an overload with an `SvgDecodeDeadline?` parameter; the existing overloads pass null (no deadline). In `DecodeSvgImage`, after the memory:
   - **Spent** (less than 1 ms left): return `SvgProjectionDecodeDeadlineMessage` (point 5), log a new `HostDiagnosticEvent.SvgDecodeRefusedPastDeadline`, no slot, no thread.
   - **Otherwise** the wait is `min(budget, left)`. A timed-out wait that was the full budget is remembered and says the "image" sentence. One that was shorter (cut) is not remembered, leaves the deadline spent, and says the projection's sentence, because "decoding took longer than 5 seconds" would be untrue of it.
   - **The charge.** A decode whose wait timed out and whose `TryAbandon` won is charged the wait it was given: that is what a Win32 timeout costs, and it makes the facts deterministic with the wait seam. Every other decode that started a thread (it ended, it ended as the budget ended, its wait failed, or it threw) is charged the time from its thread's start to the wait's return, in a `finally`, so a decode that throws is still charged. A refusal (remembered, past the deadline, busy) is charged nothing.
   - The deadline counts decode time only, not the core calls and base queries between decodes, so the sentence is literally true when it is said. This departs from core's twin, which is wall-clock from the note's first render (Call 3).
   - Not shared across threads: a projection's decodes run on one worker. The class is not locked.

5. **The sentence.** A sixth form of `render_time_exceeded_message` in `D`: `"note-images"` → `format!("Image not shown: this note's images used their {seconds}-second decoding budget.")`, called with 20. The function's doc comment (`D:255-262`) names it. `crates/slate-uniffi/src/lib.rs:8072-8075`'s doc comment is rewritten to say that `what` selects one of core's forms and to point at `core::diagram::render_time_exceeded_message` for the list, so CB-29's form needs no `lib.rs` edit (Call 6). There is no signature change. uniffi 0.32.2 folds a function's docstring into its contract checksum, so the generated C# and Swift change in that function's doc text and its checksum; both lanes regenerate the bindings, and a developer regenerates as for any FFI change. On Windows, `SvgProjectionDecodeDeadlineMessage => RenderTimeExceededMessage("note-images", SvgProjectionDecodeDeadlineSeconds)`, read once, beside `EI:3237-3247`.

6. **Where a deadline is made.** Never inside a recursion; each caller makes one for its whole projection.
   - **The reading view:** `FetchEmbedResolutions` makes one before its loop (`RCVM:1038-1042`) and passes it to the decode at `RCVM:1126`. A note's SVG cards then cost at most 20 s of waiting per refresh: four images that each run out their 5 s, after which the rest say the projection's sentence. On the next refresh the four are refused at once by the memory and charge nothing, and the next ones get their turn.
   - **The popover** (Call 1): `BuildEmbedPreview` makes one beside its per-card pixel budget (`EI:1718-1726`) and passes it to `BuildEmbedNode`. An optional deadline is threaded through `BuildEmbedNode`, `BuildEmbedParts` and `BuildImageNode` to the decode. `BuildEmbedPreview` gets an internal test entry, so a fact can call it on the test thread (the popover's own resolve always runs on `Task.Run`, `EI:1449`, where the per-thread seams do not reach).
   - **The embeds leaf** (Call 1): `BuildEmbedPreviewNode` takes an optional deadline (null means none, as today); `ResolveEmbedBatch` makes one per batch (`RPPVM:581-729`) and passes it at `:725`. The leaf runs on every activation (drift table), so this bounds a cost every open of such a note pays today, shown or not.
   - **The diagram stage** passes none (Call 2). Its decodes still get the memory.

7. **Seams, and the unit-test host.** Beside #1950's: a per-thread `SvgProjectionDecodeDeadlineMillisecondsForTests` and a process-wide `SvgProjectionDecodeDeadlineDefaultMillisecondsForTests`, read when a deadline is made; and a process-wide `SvgDecodeProcessMemoryOffForTests`, which makes the process's own `SvgDecodeSlots` neither answer from nor add to its memory (a fact's own slots always remember). `UnhurriedSvgDecodes.LetEveryRealDecodeFinish` sets the memory off and the default deadline to `int.MaxValue`, for #1949's reason: on a loaded job VM a real decode can pass even the suite's 20 s tolerance, and with the memory on, the one 8×8 test SVG that five test files share would then be refused in every later fact; with the app's 20 s deadline, two slow real decodes in one fact would cut the second. Facts 14 and 15 put the app's own configuration back for one fact each (Call 4). The shell gate runs the real app with the memory on; each journey launches its own app process (plan §9 counts 56 launch sites), so a cold-start timeout there lasts one journey, which already lands a failed diagram for the life of its view today (by reading; the gate's journeys were not run for this).

8. **Comments corrected.** `EI:3400-3407`: no production caller waits on the dispatcher since #1634; the plain Win32 wait stays because a caller may still be an STA. `S/Reading/ReadingEmbedArtifact.cs:34-48`: `FailureReason` may also be the remembered refusal or the projection's sentence. Two found on the way, each a "Found and fixed here" line with no fact (comments): `tools/SvgDecodeProbe/Program.cs:16` (the reading view decodes on its workers), and `lib.rs`'s omission of `"image-busy"` (point 5 replaces the list with a pointer). `RDB` is not edited: its comment at `RDB:1158-1159` stays true.

9. **Not changed.** The 5 s per-image budget, the cap of two abandoned and four live decoders, the 8 MiB and 1,120 px caps, the pixel pools, the guard (CB-29's), raster decodes, successful decodes (a slow success is still decoded again at each refresh: the issue does not ask for a cache of successes, reading line 93), the diagram stage's landing rules, the busy refusal (still transient and retried), and two first decodes of the same bytes at once (the leaf's and the reading view's on a first open both start a decoder; the memory helps once the first timeout lands).

## Tests

**Red first, against an inert surface.** Most new facts name members that do not exist on `main` (the two log events, the deadline type and overload, the memory class, the seams), so on `main` they fail to compile, which proves nothing. The branch's first commit adds those members inert: the enum members, the seams read nowhere, the deadline class with its arithmetic and the overload that accepts it and ignores it, the memory class consulted nowhere, and the popover's test entry. The facts are seen red on that commit, "main plus the inert surface", for the reasons given below, then the behaviour lands. Facts 1, 8 and 11 also compile and fail on `main` itself.

Every fact asserts each card's exact sentence and that no card says the busy sentence where the target says otherwise. No fact uses `Thread.Sleep` or a fixed delay (`TestWaitCensus` rules 1 and 2); the deadline facts drive time through the wait seam, which returns `WaitTimeout` at once, and point 4's nominal charge. Bodies that park are released, and each fact waits for its slots' `Live` to return to 0 with `SpinWait.SpinUntil`, a condition wait.

**Core, `D`, `mod tests`:**

1. `render_time_exceeded_message_has_its_four_forms` is renamed `render_time_exceeded_message_has_its_forms` and gains `render_time_exceeded_message("note-images", 20)` == "Image not shown: this note's images used their 20-second decoding budget." Red today: `"note-images"` falls to the diagram form. Mutation: a changed literal.

**Windows, `T/W2EditorInteractionRedTeamTests.cs`** (each through `WithSvgDecodeSeams` with its own slots):

2. `SvgPreview_ARepeatOfATimedOutDecodeIsRefusedWithoutADecoderThread`: a parked body, the wait seam returns `WaitTimeout`. The first decode returns the "image" sentence, body called once, `Live` 1. The second decode of the same bytes: the body is still called once (asserted first, so the red names it), the same sentence, `Live` still 1, `Abandoned` still 1, and `SvgDecodeRefusedRemembered` is logged. Red on the inert surface: the body is called twice. Mutations: skip the lookup; skip the remember.
3. `SvgPreview_ABusyRefusalIsNotRemembered`: `slots.Abandoned = 2` (the cap) → the busy sentence; set it to 0; the same bytes decode for real and return an image. Green on the inert surface; it pins point 2. Mutation: remember on the busy branch.
4. `SvgPreview_OnlyATimeoutAtTheFullBudgetIsRemembered`: in turn, an ordinary failure (`<unclosed`), a wait that fails (`0xFFFFFFFF`), a decode that finishes as its budget ends (the shape of `:481-510`), and a wait cut by a deadline with 50 ms left under a 100 ms budget; after each, the same bytes reach the body again. Red on the inert surface for the cut arm only (it is remembered nowhere yet, but says the "image" sentence where the target says the projection's). Mutation: remember on each branch in turn.
5. `SvgPreview_TheTimeoutMemoryKeepsTheNewest256`: on `SvgDecodeTimeoutMemory` directly, remember 257 distinct keys: the first is gone, the second and the 257th are held, and remembering a held key does not evict. Red on the inert surface only if the class is a stub that holds nothing; otherwise named as having no red run. Mutation: capacity 255; drop the newest.
6. `SvgPreview_ALeafCardsImagesShareOneDecodeDeadline`: `BuildEmbedPreviewNode` with an explicit deadline over a note card with three nested distinct SVGs, a 100 ms per-image budget, a 150 ms deadline, a parked body, the wait seam returning `WaitTimeout`. The first node says the "image" sentence; the second and third the projection's sentence; the body ran twice (the second's wait was cut to 50 ms). Then release, wait for `Live == 0`, and decode the second SVG again: its body runs (it was not remembered); the first SVG's does not. Red on the inert surface: the second says "took longer" and the third is refused at the cap with the busy sentence (two parked bodies hold the cap); the body counts already match, so they are not the red. Mutation: drop the deadline from `BuildImageNode`.
6b. `SvgPreview_APopoverCardsImagesShareOneDecodeDeadline`: the same card through `BuildEmbedPreview`'s test entry, with the deadline seam set on the test thread (`SvgProjectionDecodeDeadlineMillisecondsForTests = 150`) and no deadline passed in: the same three sentences. Red on the inert surface as fact 6. Mutation: no deadline made in `BuildEmbedPreview`.
7. `SvgPreview_RefusesANewDecodeWhileTwoAreAbandoned` (existing, `:254-297`) decodes one SVG three times and a fourth time after release. With the memory, the second would be refused as remembered. It is rewritten to four distinct SVGs (a fill each) and keeps its assertions; the PR names it among the facts the new rule changed.

**Windows, `T/ReadingSvgEmbedDecodeTests.cs`:**

8. `ASecondRefreshNeverReDecodesAnSvgThatRanPastItsBudget` replaces `ARefreshWhoseSvgDecodeNowSucceedsShowsTheImage` (`:83-139`), which is deleted in the same commit. Own slots, the wait seam returning `WaitTimeout`. Two arms:
   - A parked body that never reserves (P6's shape): open says the "image" sentence, body once. `Refresh()` twice: body still once, the sentence unchanged, and `reading.Document` is the same object after each (memo hits, no flip).
   - A body that reserves 256 bytes and then parks (the shape of `T/W2EditorInteractionRedTeamTests.cs:443-449`): open says the "image" sentence. Refresh 1: body still once, the sentence unchanged (the document may rebuild once, point 3). Refresh 2: the same `reading.Document` object.
   Red on `main`: the body is called again, and the next refresh flips to the busy sentence (P1). Mutation: skip the lookup.
9. `ARefreshAfterABusyRefusalShowsTheImage` (what #1908's fact pinned that still holds): own slots with `Abandoned = 2`; open shows the busy sentence; set `Abandoned = 0` and a body that returns the class's `TinyPng` (`:341-343`) without calling `reserve`, so the two digests differ only in the decode field; `Refresh()` shows the image and rebuilds. Green today; it keeps the decode field pinned. Mutation: drop the decode field from the digest (`RCVM:2363-2370`).
10. `SvgsPastTheProjectionsDecodeDeadlineNeverEnterTheDecoder`: three distinct SVGs, a 100 ms per-image budget, a 150 ms deadline (per-thread seam), a parked body counting calls per file, the wait seam returning `WaitTimeout`. Open: `a.svg` says "took longer", `b.svg` and `c.svg` the projection's sentence; the body ran for `a` and `b` only. Release, wait for `Live == 0`, re-park. `Refresh()`: `a` is refused by the memory (no new call), `b` runs its full budget and says "took longer", `c`'s wait is cut and says the projection's sentence; the document was rebuilt. Red on the inert surface: `b` says "took longer" and `c` the busy sentence (the cap; the body counts already match). Mutations: no deadline passed; the deadline not charged; remembered refusals charged; a cut wait remembered.

**Windows, `T/ReadingDiagramTests.cs`:**

11. `ADiagramWhoseDecodeRanPastItsBudgetIsNotDecodedAgain`: the shape of `ADiagramWhoseSvgDecodeOutlivesTheBudgetShowsCoresReason` (`:464-533`), building twice from the same artifact: the second build shows the same reason and the body was called once. Red on `main`. Mutation: skip the lookup. It shows the memory serves the diagram path; the stage takes no deadline (Call 2).

**Windows, the embeds leaf** (Call 1), in a class with a small fixture (not `RightPanePanelsBudgetTests`, whose constructor writes about 21 MiB of payloads, `:19-96`); a new file needs `git add` and then `python3 scripts/apply-license-header.py`:

12. `TheEmbedsLeafsImagesShareOneDecodeDeadline`: a note embedding three distinct SVGs, the panels built with `synchronousForTests: true`, so `NoteChanged` → `LoadLinks` → `ResolveEmbeds` → `ResolveEmbedBatch` run inline (`PanelWorkScheduler.cs:157-161`, `:328`) and the thread's seams reach the batch; fact 6's seams. Rows one to three say "took longer", the projection's sentence, the projection's sentence; the body ran twice. Red on the inert surface: the second row says "took longer" and the third the busy sentence. Mutation: a deadline per row instead of per batch.

**The app's own configuration** (the protocol's "test the mode users run", `24_red_team_protocol.md:72-75`); both safe under the serial run:

13. `UnhurriedSvgDecodesKeepsTheProcessMemoryOffAndTheDeadlineOpen`, beside `UnhurriedSvgDecodes`: after the module initializer, `SvgDecodeProcessMemoryOffForTests` is true and the default deadline is `int.MaxValue`; and a deadline made with both deadline seams cleared (in a `try`/`finally`) has 20,000 ms, which pins the app's 20 seconds. Mutations: drop either initializer line; `SvgProjectionDecodeDeadlineSeconds = 21`.
14. `TheProcessMemoryRemembersATimeout`: `SvgDecodeProcessMemoryOffForTests = false` in a `try`/`finally`, `SvgDecodeSlotsForTests` left null, an SVG no other fact uses (a unique comment in it), a parked body and the wait seam: decoded twice on the test thread, the body runs once. Release, and wait for the process slots' `Live` to return to its value before the fact. Mutations: attach the memory only to slots a fact makes; invert the off seam.
15. `ABackgroundRefreshReadsTheProcessDeadline`: a reading view in background mode (`startInteractionBackgroundWork: true`, landing waited as at `T/ReadingSvgEmbedDecodeTests.cs:41-58`) with `SvgProjectionDecodeDeadlineDefaultMillisecondsForTests = 0` in a `try`/`finally`: the card says the projection's sentence, with no body seam. Mutation: the worker reads only the per-thread seam.

**What else runs.** `ReadingSvgEmbedDecodeTests`, `ReadingEmbedTests`, `ReadingDiagramTests`, `W2EditorInteractionRedTeamTests`, the leaf fact's class, `RightPanePanelsBudgetTests`, `RightPanePanelsTests`, `SvgPreviewGuardTests`, `SvgDecodeOutOfProcessTests`, the censuses that read these files, then the whole unit run under the desktop lock before the push. Rust: the four gates. Every other fact that uses the decode seams or the shared 8×8 SVG was read by the red team and holds under the design.

## Files

- `crates/slate-core/src/diagram.rs`: the form, its doc line, fact 1.
- `crates/slate-uniffi/src/lib.rs`: one doc comment (`:8072-8075`). No signature change.
- `S/EditorInteractions.cs`: the memory, the deadline, the sentence, the `DecodeImage` overload, the deadline through `BuildEmbedPreview`, `BuildEmbedPreviewNode`, `BuildEmbedNode`, `BuildEmbedParts` and `BuildImageNode`, the popover's test entry, the stale comment.
- `S/HostLog.cs`: two `HostDiagnosticEvent` members (the privacy fact, `T/W1HardeningTests.cs:27-52`, iterates every member and needs no edit).
- `S/Reading/ReadingContentViewModel.cs`: the deadline in `FetchEmbedResolutions`.
- `S/Reading/ReadingEmbedArtifact.cs`: a doc comment.
- `S/Panels/RightPanePanelsViewModel.cs`: the batch's deadline (Call 1).
- `apps/slate-windows/tools/SvgDecodeProbe/Program.cs`: a comment (a test-only tool).
- Tests: `T/W2EditorInteractionRedTeamTests.cs`, `T/ReadingSvgEmbedDecodeTests.cs`, `T/ReadingDiagramTests.cs`, `T/Support/UnhurriedSvgDecodes.cs`, the leaf fact's class, and a new file for facts 13 and 14 if they do not sit in an existing class.

Not edited: `RDB`, `a11y.rs` and the corpus (no `A11yEvent`), contract 38's ledger (no construction moves; `--check` is run), `session.rs`, any manifest or lockfile, `windows.yml`, `TestWaitCensus.cs`, any Mac source, the parity matrix, help documents.

## Overlap

- **This wave.** CB-15a and CB-24 edit none of these files. CB-15a's `ShellFailureDetailCensus` runs over the whole shell (`reading/05_spoken_failures.md:300`), and its exception list will name sites in three files this chunk edits (`RCVM:1208`, `RPPVM:479`, `:812`, `:915`, `EI:1592`). If that list is keyed by line, the second of the two to merge moves those lines; this chunk adds no new site (nothing here turns an exception into text). CB-15a's spec is not in the repository yet, so this was not checked against it.
- **`lib.rs`.** After #2008 (plan §6), unless Call 6(b).
- **Next waves.** CB-29 (#1610) follows in C8 and edits `EI` (the guard's refusal out of `RenderSvgToPng`), `D` (its own form) and `W2EditorInteractionRedTeamTests.cs`. A guard refusal is not a timeout and is never remembered; CB-29's spec should say where it sits in point 3's order. With point 5's pointer, CB-29 needs no `lib.rs` edit. CB-15c and CB-22 follow in `EI` and `Reading/`.

## The NVDA spot check

Base is this worktree's step 0 Release build, copied aside before the branch is built and launched with `-Exe`; head is the PR's Release build. Each launch goes through `scripts/windows/launch-for-field-pass.ps1` with `-Root` (#1995) and `-Vault`, under the desktop lock, with NVDA's version recorded and the app's log directory (`SLATE_LOG_DIR`, `launch-for-field-pass.ps1:120-121`) kept with the run. Before the pass: no home-pool job running on this desktop (by the runner name of in-progress jobs), and P0 run again at 1,500 × 2,000 to confirm a decode of about 7 to 9 s (past 5 s, short of the 10 s at which the cap would engage within one stream).

The vault: `slow.md` holding "Before.", ten embeds `![[m1.svg]]` to `![[m10.svg]]`, and "After."; each `mN.svg` is the marker at 1,500 × 2,000 with its own fill, so the byte strings differ. The leaf decodes the same ten on every activation (drift table), so each step waits for the leaf's batch to settle before reading mode: its decode lines in the log stop, then two seconds more for the last decoder's tail. At most two decoders run at once.

Timing: NVDA names the reading view as focus arrives, over the "Loading reading view…" placeholder (`S/Reading/ReadingSurface.cs:347-353`), on base and head alike, so the landing is timed as the moment Down from the placeholder first reads the first card, and from the log's last `SvgDecode*` line. Each card is heard as its header (its title, the file name when there is no alt, then "Jump to source") and its body line; the exact header wording is recorded as heard.

1. Launch, Ctrl+O, type "slow", Enter. Wait for the leaf to settle. Ctrl+Shift+E, then Down through the cards.
   - Base, predicted: the leaf about 50 s; then the reading view about 50 s; all ten "Image not shown: decoding took longer than 5 seconds."
   - Head, predicted: the leaf about 20 s (m1 to m4 time out, m5 to m10 meet the deadline); then the reading view about 20 s: m1 to m4 are remembered, m5 to m8 run out their budget, and m9 and m10 meet the deadline. Heard: m1 to m8 "Image not shown: decoding took longer than 5 seconds."; m9 and m10 "Image not shown: this note's images used their 20-second decoding budget."
2. Ctrl+W, Ctrl+O, "slow", Enter, wait for the leaf, Ctrl+Shift+E. Base: the same as step 1. Head: the leaf decodes m9 and m10 (about 10 s); the reading view lands in under a second with all ten "took longer". This is the issue's "a second refresh that lands without the 5 s wait" (reading line 111).
3. Head only: open the Embeds leaf (its rows were built in step 1's launch) and record what rows five to ten said before step 2's batch, from step 1's log, and what they say now. Optional, if the run allows.

Nothing new is announced when the view lands; the reader hears the sentences in range. The PR records what NVDA says, the times from its log, and each step's wait for the leaf.

**Base, as run (2026-11-18, NVDA 2026.3.1; PR #2011's body has the table).** Two opens of `slow.md` on `main`'s build. Each time the hidden leaf decoded all ten in 58 to 59 s, then the reading view landed 50.3 s and 50.7 s after Ctrl+Shift+E. Each card said "Embedded image: mN.svg, link, Jump to source" and then "Image not shown: decoding took longer than 5 seconds.", ten of ten, with no busy sentence. One correction to medium 7: on base, focus waits on the tab while the projection decodes ("slow, tab, selected, 1 of 1"), as #1908 recorded, and NVDA says "Reading view, document, … Before." at the landing. So the landing is audible. The script also times it from the reading view's text by UIA. Step 3 above is not needed: on head the leaf's sentences are read from the host log and its rows.

## Docs and registers

- **Contract 38's ledger:** not regenerated; `python -I scripts/a11y_trigger_ledger.py --check` is run.
- **No contracts document records these sentences** (a search of `docs/plans/*_contracts.md` for "decoding took longer" finds nothing). `BENCHMARKS.md` records the diagram budget only and is not changed.
- **#1954 (Mac work owed):** no line for core's change. The Mac has no consumer of `render_time_exceeded_message`, and core's change is a form only Windows asks for, so no Mac user hears, sees or gets anything different (rule 5 does not apply). The checksum change is absorbed by the Swift lane's regeneration. One line by the lead's ruling on Call 5, marked "by reading": the Mac twin of this chunk's budget, `apps/slate-mac/Sources/SlateMac/EmbedView.swift:161` builds `NSImage(data:)` from an embedded image's bytes in the view's body with no time budget, so a slow SVG embed would hold the main thread for its whole decode; the Windows remedy is #1581's per-image budget and this chunk's memory and deadline.
- **#1955 (deferred finds):** one line under Call 2's recommendation: `RCVM`'s diagram stage (`:2086-2104`) decodes a note's diagram SVGs one after another with no deadline; N slow-decoding diagrams land N × 5 s late. The failing test: a stage over three artifacts whose decodes each run out a 100 ms budget, expecting the third never to reach the decoder.
- **#1635:** closes on this PR. Step 0's numbers go in a comment on it before code.
- **Found on the way, predicted:** found 4 (the probe's stale comment, `lib.rs`'s missing `"image-busy"`, the diagram stage's decodes, the Mac gap of Call 5), fixed here 2 (the two comments), deferred 1 (the diagram stage), filed 1 (the Mac gap, if Call 5 is taken). The leaf and popover deadlines are the chunk's own feature under Call 1(b), not finds.
- **Plan 47 §12:** the delivery row, in the lead's docs PR.

## Calls for the lead

1. **Which projections share a deadline.** CD19 says "a projection's decodes"; the reading names only the reading view's fetch. The leaf decodes every embed of the active note on every activation, shown or not, and the popover builds through the same recursion (P5, drift table).
   - (a) The reading view only. Smallest; leaves the leaf's hidden N × 5 s on every open and the popover's N × 5 s on first build.
   - **(b) Recommended:** the reading view, the popover's build and the leaf's batch, each with its own deadline. Cost: one more product file (`RPPVM`, about three lines), an optional parameter threaded through four builders in `EI` and a test entry for `BuildEmbedPreview`, facts 6, 6b and 12, and the NVDA pass's ten-image vault. It is this PR's own feature, so AGENTS.md's sizing does not send it elsewhere. One wording point for the lead: in the popover over `![[gallery.md]]`, or a leaf row of a nested note card, "this note's images" are the embedded note's while the deadline is the build's. The owner chose the sentence for a reading projection; the recommendation is that it reads true enough in both places (the images are those of the note the card shows), with no owner line.
2. **The diagram stage.** Its decodes get the memory (fact 11), but no deadline.
   - **(a) Recommended:** no deadline there; one #1955 line. A stage's late decode delays only a diagram's landing (stage 1 publishes without diagrams), core already bounds the render at 20 s a note, and a deadline there needs a transient outcome so the next refresh retries it, which changes WB-20's landing rule and wants its own sentence ("Diagram could not be rendered" over "this note's images…" reads wrong).
   - (b) Take it here: `RCVM`'s stage, `ResolveDiagramOutcome` in `RDB`, a transient outcome, a sentence for the owner, two facts. Cost: about a day and one more file.
3. **Two departures from core's twin, and a cut wait.** Recommended as designed, each named in the PR body:
   - The deadline counts decode time only, where core's is wall clock from the note's first render (`D:332`). The sentence says "decoding budget", and between the host's decodes there are core calls and base queries that are not decoding; counting them would make the sentence untrue. Cost: none.
   - A wait cut by the deadline is not remembered and says the projection's sentence. Core caches a render cut by its note deadline (a `Bounded` result is not transient, `D:304-306`, `:891-901`), but core's cache is per note and drops on the note's next change, while the host's memory is by bytes for the life of the process; remembering a cut wait would refuse an image for good after a few milliseconds of decoding. Cost: fact 4's cut arm.
4. **The unit-test host.** Recommended: the process memory off and the default deadline open in `UnhurriedSvgDecodes`, as #1950 did for the per-image budget (point 7), with facts 14 and 15 putting the app's configuration back for one fact each and fact 13 pinning the 20 s. Cost: two seams and three facts. Without the switch, one slow-VM timeout of the shared 8×8 test SVG would fail every later fact that decodes it, where today it fails one.
5. **A Mac gap found by reading.** `EmbedView.swift:161` builds `NSImage(data:)` from an embedded image's bytes inside the view's body, with no time budget, so a slow SVG embed would hold the Mac's main thread for its whole decode (by reading only; no Mac here). It is not this plan's change and not owed by it. Recommended: file one `mac` issue naming the class and #1581 and #1635 as the Windows remedy. Cost: one issue. Alternatives: a "Checks" line on #1954, or nothing.
6. **`lib.rs`.** The plan gives CB-28 one doc comment there, and the edit changes that function's uniffi checksum in both bindings (M5).
   - **(a) Recommended:** rewrite it to point at core's function for the list of forms, after #2008 merges. It fixes today's omission of `"image-busy"` and spares CB-29 a `lib.rs` edit. Cost: the wait on #2008, and a checksum change that the lanes' regeneration absorbs.
   - (b) Leave `lib.rs` alone and name the form only in `D`. No wait on #2008 and no checksum change; the FFI doc comment stays wrong (two forms missing), for a later `lib.rs` chunk to fix.
7. **The form's name** is `"note-images"`, after `"note"` (a note's diagrams). Recommended as written; it is the host's string and core's, in one place each.
8. **The base NVDA pass** needs this worktree's main build, so it runs before the branch is cut: about 15 minutes under the desktop lock, at most two decoder threads at once. Recommended: run it while the calls are with the lead, if the lock is free and CB-24's pass is not waiting on it. Cost: the lock for that time.

### Lead's rulings (2026-11-18)

1. (b): the reading view, the Ctrl+E popover's build and the embeds leaf's batch share the 20 s deadline. The wording in the popover or a nested card needs no owner line.
2. (a): the diagram stage gets the memory without a deadline, and one #1955 line. The lead puts the deadline there to the owner in the wave report.
3. Both departures as designed (decode time only; a cut wait is not remembered), each named in the PR body.
4. Accepted: the unit host turns the memory off and leaves the deadline open, and facts 13 to 15 put the app's configuration back.
5. The Mac gap at `EmbedView.swift:161` goes on #1954 as one line, marked "by reading", as the Mac twin of this chunk's budget; no new issue. The predicted count becomes found 4, fixed here 2, deferred 2 (the #1955 line and the #1954 line), filed 0.
6. (a), after #2008 merges: the doc comment points at core's list of forms. The lead says when #2008 has merged.
7. `"note-images"` accepted.
8. The base NVDA pass runs now if the desktop lock is free, holding it only for the pass. The step 0 comment goes on #1635 now; no branch and no product code before the lead's word on #2008.

The overlap with CB-15a's census: if it is keyed by file and line, whichever of the two merges second moves those rows. The lead tells Q.

## Verification

```powershell
$env:CARGO_TARGET_DIR = '<this worktree's own target directory>'
cargo test -p slate-core --lib diagram::tests::render_time_exceeded_message_has_its_forms
cargo fmt --all -- --check
cargo clippy --all-targets --workspace -- -D warnings
cargo test -p slate-core
cargo bench --workspace --no-run
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh
python -I scripts/a11y_trigger_ledger.py --check
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReadingSvgEmbedDecodeTests|FullyQualifiedName~W2EditorInteractionRedTeamTests|FullyQualifiedName~ReadingDiagramTests|FullyQualifiedName~ReadingEmbedTests|FullyQualifiedName~RightPanePanelsBudgetTests|FullyQualifiedName~RightPanePanelsTests|FullyQualifiedName~SvgPreviewGuardTests|FullyQualifiedName~SvgDecodeOutOfProcessTests|FullyQualifiedName~UnhurriedSvgDecodes"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <each edited .cs file>
python3 scripts/generate-parity-matrix.py --check
python3 scripts/check_windows_census_trait.py
```

The leaf fact's class joins the filter. A new `.cs` file is `git add`ed and then `python3 scripts/apply-license-header.py` is run. Then the whole unit run under the desktop lock. The PR's lanes: rust, windows in full, swift, a11y-check.

## Acceptance

- [ ] Step 0's numbers are in a comment on #1635 and in the PR body.
- [ ] Facts 1, 8 and 11 were seen red on `main`; facts 2, 4 (cut arm), 6, 6b, 10 and 12 on "main plus the inert surface", each for the reason given; 3, 5, 9, 13, 14 and 15 are named with their mutations; every mutation listed bites.
- [ ] A remembered image says exactly "Image not shown: decoding took longer than 5 seconds."; the projection's sentence is exactly "Image not shown: this note's images used their 20-second decoding budget."; a refresh over a remembered image rebuilds at most once.
- [ ] Only a timeout at the full per-image budget is remembered; the busy refusal is still retried by the next refresh.
- [ ] The memory holds at most 256 keys and drops the oldest.
- [ ] `lib.rs` changes in one doc comment; the bindings change in that doc text and that function's checksum, and nowhere else.
- [ ] The NVDA phrases and landing times for base and head are in the body, with NVDA's version and each step's wait for the leaf.
- [ ] `Closes #1635`, and `gh pr view N --json closingIssuesReferences` shows only #1635; "close", "fix" and "resolve" stay away from #1954, #1955, #1908, #1610, #1581, #1634, #1580, #1949, #1950 and #2008.
- [ ] The body has the red team, the Codex rounds and verdict, the two departures of Call 3, "Found and fixed here" (the two comments), the `not delivered` lines, and the count.

## Red team record

A fresh Opus agent at xhigh read the first draft against the tree at 26d353ef, the step 0 output and the probe source: 2 blockers, 7 mediums, 12 lows. It found CD19 met, the memory's placement and the order of checks sound, no race beyond the two first decodes of one byte string, and only the two existing facts the draft named broken by the design. Every finding is taken:

- Blocker 1: the popover never reaches `BuildEmbedPreviewNode`. The popover's deadline is now made in `BuildEmbedPreview`, threaded through the recursion, with a test entry and fact 6b; the drift row and P5's sentence are corrected.
- Blocker 2: the leaf decodes every embed on every activation, shown or not, so the draft's NVDA steps raced it. The drift table says so, Call 1 counts it, and the NVDA pass now waits for the leaf and uses ten images.
- Medium 1: a remembered refusal reserves no pixels, so the first refresh after a reserving timeout rebuilds once. Point 3 says so, fact 8 has both arms, and P6 (run for this) shows the field document does not reserve before 5 s.
- Medium 2: fact 9's mutation did not bite through the reserved pixels; its refresh body now returns a PNG without reserving.
- Medium 3: on `main`, two parked bodies hold the cap, so the third decode says the busy sentence; the red reasons of facts 6, 10 and 12 are corrected, and each fact asserts every sentence.
- Medium 4: most facts do not compile on `main`; they are seen red on an inert-surface commit.
- Medium 5: the `lib.rs` doc edit changes the function's uniffi checksum; point 5, Call 6 and the acceptance say so, and the comment points at core's list instead of copying it.
- Medium 6: the app's own configuration was never exercised; facts 14 and 15.
- Medium 7: "Reading view, document" is spoken over the placeholder; the landing is timed by the first card and the log.
- Lows: the leaf fact uses `synchronousForTests: true` in a small-fixture class with no fallback (L1); fact 6 checks what was remembered (L2); Call 3 names both departures from core (L3); the key is a hex string (L4); the charge cases are specified (L5); point 3 no longer claims the pool cannot intervene (L6); Call 1 carries the wording point (L7); the NVDA logistics are named (L8); the two comments are "Found and fixed here" and new files get the licence header (L9); the keyword list is complete (L10); fact 13 pins 20 s (L11); point 1 and point 7 name the cold start and the gate (L12).

## Brief

Work in a fresh worktree on `claude/cb28-svg-decode-budget`, cut from `origin/main` after #2008 merges, with a `CARGO_TARGET_DIR` of its own. Copy step 0's Release build aside for the base NVDA pass and delete the probe file before cutting the branch. Read this spec's drift table and design, then `EI:1714-1910`, `EI:3141-3483`, `RCVM:996-1151`, `RPPVM:581-729` and `D:255-289`. Post step 0's numbers on #1635. Commit the inert surface first; write facts 2, 4, 6, 6b, 10 and 12 and see them red on it, and facts 1, 8 and 11 red on `main`'s behaviour; then build the memory, the deadline, the sentence and the seams, make them green, add 3, 5, 9, 13, 14 and 15, rewrite fact 7, and run every mutation. Run the gates, the classes and the whole unit run under the lock; regenerate the bindings into this worktree's target. Then the NVDA pass on head. Commit: `fix(windows): a timed-out svg decode is not decoded again, and a note's svg decodes share a 20-second budget`.

## What delivery changed (2026-11-18)

The branch is `claude/cb28-svg-decode-budget`. Where this spec and the merge disagree, the merge stands, and the lines above are left as written.

- **The fact names and the order of commits.** Commit 4a2b5691 adds the inert surface, bd593b8b the facts (all red on it for the spec's reasons) and ce264927 the behaviour. The review rounds added 824f16d1, 7ec285ea and 909c0259. Then came the merge of `main` with #2008 (fc6680d5) and the `lib.rs` doc comment (89ae906d), after #2008 merged as 834cbe31.
- **A shared test helper**, `Support/ParkedSvgDecodes.cs`, is not in the file list. Its wait seam returns `WaitTimeout` only once the served decode's body has entered. The embeds-leaf fact sits in a new class, `EmbedsLeafSvgDecodeDeadlineTests`. Facts 13 and 14 sit in `UnhurriedSvgDecodesTests`, and fact 15 sits in `ReadingSvgEmbedDecodeTests`.
- **The wait seam** (`SvgDecodeWaitForTests`) now takes the milliseconds the decode waits, and the seam and the real call share one variable. This came from the diff red team's M1. Without it, passing the budget in place of the cut wait survived the suite.
- **Codex round 1.** A decode whose pixels the caller's pool refused is not remembered, even when the caller then wins `TryAbandon`; it still says the timeout sentence. The deadline's charge is taken once, in the `finally`, after the PNG conversion and a failed wait's `Join`. Every `WAIT_TIMEOUT` costs at least its wait, so the diff red team's L2 closed too. Codex's second schedule was declined: a decode that signals completion just after its wait timed out did run past its full budget.
- **Codex round 2.** In the reading view, an SVG met after the pixel pool is spent first asks a helper, `SvgDecodeRefusalBeforeDecoding`, which holds the memory and the deadline checks. So it says the remembered or the deadline sentence and not the pool notice. Design point 3's first limit is therefore gone.
- **Design point 6's "the next ones get their turn"** holds for timeouts only. Decodes that succeed are charged and not cached, so a note of many SVGs that each succeed in just under 5 s shows the deadline sentence for the later ones at every refresh. The PR names this as its third departure.
- **The NVDA pass** ran as described here, with ten images and a wait for the leaf, on base and on head; both are in PR #2011's body. Step 3 was not needed. Head landed in 20.4 s on the first open and 0.1 s on the second, against 50.3 s and 50.7 s on base.
- **Mutations:** 39, all biting. **Codex:** 4 rounds. Rounds 1 and 2 were needs-attention; rounds 3 and 4 approved.
- **The count:** found 5, fixed here 2, deferred 3, filed 0. The PR body said 4 / 2 / 2 / 0. The merge judge's finding 1 (a bounded cache of successful decodes, so slow successes stop spending the deadline at every refresh) was added to #1955 by the lead after the PR, which makes the fifth find and the third deferral.
- **The merge.** PR #2011 merged on 2026-11-18 as 9999cb68, pinned to 7d9a2a93, after one independent merge judge said Merge with no blocker or major finding. CI on the head: 29 checks passed and 8 skipped. The whole unit project before the PR: 7,734 passed, 2 skipped, 0 failed.
