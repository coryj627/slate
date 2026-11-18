# CB-14: A toggle that landed but was not indexed says it landed

Issues: #1660, #1395. Size M. Lanes: rust (a change under `crates/` runs the Windows lane in full), windows (the app job and its unit facts), swift (the corpus mirror only; the Mac lane proves it compiles). AT: an NVDA spot check on three routes (the PR opens as a draft and is marked ready when the phrases are in its body). Wave: C6, slot 1 of the `a11y.rs` interlock. Decision: CD8, adopted 2026-11-16 as recommended (plan §3, §3.1): fold #1395 in; a new event, "Task completed. Saved, but not indexed yet: database is locked. It will appear after the next scan.", always that event on this error; an FFI addition, with ledger lines for the Mac adoption and the create sentence; the tab's "Task toggled on disk, but the editor no longer matches it" still follows, and the buffer splice behind it is left alone. The owner's record on #1660 (2026-11-16) reads: "The tab's second sentence … still follows, and the code behind it is left alone. … Nothing comes back to the owner." §9: the other sites a `SavedButUnindexed` reaches are listed, and their fixes go to CB-46. The owner's ruling on #1991 (2026-11-18) binds this chunk (see Overlap). Review: a red team, then Codex adversarial-review rounds until it approves.

*2026-11-18, the lead's rulings on this spec's calls:*
- Lead's ruling, Call 1: accepted. Routes a and b are fixed here as CB-14's own feature (Design 8): CD8 assumes the tab's sentence follows on every route, and this makes that true without touching the splice code CD8 left alone. A red fact per route (facts 12 and 13), and both routes in the NVDA spot check (R1 and R1b).
- Lead's ruling, Call 2: Option A. CD8's intent is that the reader hears both sentences and that the reconcile code is left alone; A meets both, measured on every route, while B edits that code. The departure from CD8's literal order goes in the PR body and in contract 38's addendum (Design 11); the lead reports it to the owner.
- Lead's ruling, Call 3: accepted, as CD8 says.
- Lead's ruling, Call 4: accepted, as ONE #1955 comment holding the reach list and core defects A and B, each with its evidence.
- Lead's ruling, Call 5: not as recommended. CD8's adopted sentence reads "Saved, but not indexed yet: database is locked." Core composes the new event's reason from the innermost error message, dropping the "database error: sqlite error: " wrappers, only if the change stays inside the new event's composition in core; if it would reach the error type's Display or other callers, the detail stays verbatim and the PR says so with the measured sentence. It stays inside: Design 2 strips the wrappers in the event's render only; `VaultError`'s Display, `vault_error_detail` and every other caller are unchanged.
- Lead's ruling, Call 6: accepted.
- Lead's ruling, Call 7: noted; #2003, #2004 and #2005 join the owner's unplaced list.

Written 2026-11-18 by the chunk's second principal, from the earlier principal's state file and draft, its worker's trace (re-checked line by line by worker W1 this session; both are kept with the chunk's working notes), `reading/05_spoken_failures.md:156-201`, `reading/09_group_c_audit.md:73-82`, the two issues with their comments, #1954, #1955 and #1991. Every file and line below is cited at eeb99032. `origin/main` moved to 059471b6 (#2002, plan documents only) after the spec was drafted; no code line cited here moved. Unlike the reading, this spec rests on runs: step 0 measured the routes in the unit harness, the real detail a commit failure carries, the two core defects, and what NVDA speaks on main and under five prototypes. "Step 0, as run" holds the results; a statement still by reading says so.

`C/` is `crates/slate-core/src/`; `U/` is `crates/slate-uniffi/src/`; `S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`; `M/` is `apps/slate-mac/`. Step 0's probe outputs and the NVDA run reports are kept with the chunk's working notes, not in the repository.

## Goal

A task toggle whose bytes landed on disk but whose index commit failed (`VaultError::SavedButUnindexed`, "SBU" below) says that the task changed and that the index is behind, in one sentence core renders, on every Windows toggle route that publishes: "Task completed. Saved, but not indexed yet: <detail>. It will appear after the next scan." (or "Task reopened. …"). A reader hears it in full under NVDA. Where the open editor no longer matches the disk, the reader also hears "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing.", and the note's tabs are guarded. (A toggle whose tab is disposed or re-pointed mid-flight on the editor or reading-view route publishes nothing, as on main: `tabPublished` is false, `S/WorkspaceViewModel.cs:1907`, and those routes have no completion.)

Both unindexed caveats close a carried reason before the next sentence: `vault_error_detail(SavedButUnindexed)` and `A11yEvent::CreatePublishedUnindexed` (#1395).

The other places SBU reaches are listed on #1955 for CB-46, with the two core defects step 0 confirmed.

## Current state on main

**Core.** SBU is created only in `save_text_locked_checked` (`C/session.rs:4889`): the test seam `SLATE_TEST_FAULT_AFTER_WRITE` (`:5244-5251`, detail "test fault: injected failure after write, before index commit") and four `map_err` arms after the write. Their details differ by arm: the stat arm (`:5256`) carries an `io::Error`'s text, ending "(os error N)"; the index arm (`:5278`) a `VaultError`'s Display, "database error: sqlite error: …" (`C/lib.rs:218`, `C/db.rs:46`); the intent-clear and commit arms (`:5298`, `:5302`) a bare `rusqlite::Error`'s text, such as "database is locked" or "database or disk is full". None ends in a period. Core documents SBU as a commit (`C/lib.rs:284-295`). `toggle_task_status` propagates it.

`C/a11y.rs`:
- `TaskToggled { completed }` (`:882-884`), Medium (not in the High list `:1946-1983`), renders "Task completed." or "Task reopened." (`:2214-2215`).
- `TaskToggleFailed { detail }` (`:887-889`), High (`:1956`), renders `lead_then_sentence("Task could not be toggled:", detail)`, or "Task could not be toggled." for a blank detail (`:2216-2219`).
- `CreatePublishedUnindexed { name, reason }` (`:927-930`, doc `:917-926` "spliced verbatim", "byte for byte"), Medium, renders a blank reason without the colon (`:2243-2246`) and splices any other reason verbatim (`:2247-2252`).
- `lead_then_sentence` (`:3399-3408`) trims and closes carried text with a period only when it has no terminal `.`, `?`, `!` or `…`.
- `vault_error_detail`'s SBU arm (`:3629-3631`): `format!("Saved, but not indexed yet: {detail} It will appear after the next scan.")`.
- The variant count is pinned at 238 (`:8893-8916`); uniffi's cap is 256.

**The Windows toggle routes.**

| Route | Entry | Completion | SBU sentence site |
|---|---|---|---|
| a. editor Ctrl+Enter (Edit, Activate at Cursor) | `S/EditorInteractions.cs:2055` `_tab.ToggleTask(task, _announce)` | none | the tab's failure arm, `S/WorkspaceViewModel.cs:1932-1940` |
| b. reading view checkbox | `S/Reading/ReadingContentViewModel.cs:449` `_tab.ToggleTask(task, _announce)` | none | the same arm |
| c. Tasks panel through the active tab | `TogglePanelTask`, `:4256-4285` | `TaskToggleCompletion` (`:4143-4226`) | the same arm; then the completion reconciles |
| d. Tasks Review through an open tab | `TryToggleTaskInOpenTab`, `:4094-4134` | `TaskToggleCompletion` | the same arm; then the completion reconciles |
| e. Tasks Review with no tab | `TasksReviewViewModel.DirectToggleAtItsTurn` (`S/Panels/TasksReviewViewModel.cs:735-898`) | its own `PublishDirectToggle` (`:901-985`) | `:946-950` |
| f. c or d whose tab closed mid-flight | the tab does not publish (`:1907`) | `TaskToggleCompletion`, `publishedThroughTab: false` | `:4199-4210` |

On a failure the worker reads the disk back (`ReadBackDiskHashAfterFailure`, `:1741-1756`) and converts the mutation lease into a pending repair (`:1810-1811`). The completion, when the read-back moved, repairs the index at once (`TryRepairNow`, `:4185`) and calls `ReconcileTabsAfterDirectTaskWrite` (`:4188`, `:4235-4245`), which runs `ReconcileAfterExternalTaskWrite` (`:2044-2065`) on every tab of the note: a tab whose saved hash differs is marked `IsExternallyStale` and speaks "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing." as `HostComposed` High (`:2063-2064`), one line per tab. The completion repairs and reconciles before it speaks its own sentence (`:4199-4210`). A null read-back (unreadable disk) skips the reconcile (`:4159-4162`). Routes a and b have no completion, so nothing repairs at once and nothing reconciles. The review's direct route speaks first and then runs `DiskWriteLanded` for a tab that raced open (`:946-958`).

**The dispatcher.** High maps to `AutomationNotificationProcessing.ImportantMostRecent` and Medium to `All` (`S/AccessibilityNotificationDispatcher.cs:258-267`). A High line keeps the shared activity ID; a Medium line gets its own numbered one (`:466-475`).

**The Mac, by reading (W1).** `performToggleCurrentTask` (`M/Sources/SlateMac/AppState.swift:14323-14414`) sends SBU to its catch-all arm (`:14408-14412`), which posts `.taskToggleFailed(detail: humanReadable(error))` only when `announcesOutcome` is set (the editor's activate-at-cursor, `AppState+EditorActivation.swift:219`). Its instance `humanReadable` SBU arm (`:26032-26036`) says "This change was saved but not indexed yet: {detail} It will appear in listings after the next scan; do not recreate it." The static `humanReadableVaultError` (`:14038-14041`) is byte-identical to core's run-on and is used by Bases. The Tasks panel, the note view and the reading view toggle silently on SBU; the review sets `vaultTasksLoadError`. The create caveat is core's event, posted from `AppState+CreateReporting.swift:52-56`, so core's change reaches Mac speech with no Swift change. No Swift `switch` over `A11yEvent` lacks a `default` (W1), so a new variant does not stop the Mac build; the hand-written corpus mirror must list it. No Swift source calls `vaultErrorDetail`.

## Step 0, as run (2026-11-18)

On main's product code at eeb99032, Windows 11 Pro 10.0.26300, NTFS, Release bindings built into a per-worktree cargo target directory. Every probe was temporary and is reverted; the diffs are kept with the chunk's working notes: `probe-tests.diff`, `proto-app.diff`, `proto-swap-app.diff`, `cb14_probe.rs.txt`, `rust-probe-modline.diff`.

**P1, the routes in the unit harness** (`Cb14Step0Probe`, a temporary theory in `T/SaveChainAdmissionTests.cs` on its `Vault` harness, under `EnvFaultLock`, the seam on `a.md`; 7 cases passed, run by filter alone; outputs kept with the chunk's working notes). Spoken, with the harness clock:

- Route a (the tab's `ToggleTask` with no completion, the call routes a and b make): one `TaskToggleFailed` [High] at 7.1 ms (8.3 ms in the second case), "Task could not be toggled: Saved, but not indexed yet: test fault: injected failure after write, before index commit It will appear after the next scan." Right after: disk has `[x]`; the tab is clean and NOT externally stale; its buffer and saved hash are the pre-toggle ones; `CanUndo` is false; a repair is pending. A `RefreshExternalStaleness` (what a `Modified` event runs, `:4435-4451`) leaves it not stale, because the index still holds the old hash. Only after a repair sweep (`Retry`) does the next refresh mark it stale. An edit and save, made right away or after the repair, is refused: "Save blocked. a.md was modified externally. Your edits remain in the editor." [High]; the disk keeps `[x]`.
- Routes c and d: `TaskToggleFailed` [High], then 1.2 to 3.0 ms later `HostComposed` [High] "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing."; `Status` ends as the second sentence; the tab is stale; the repair already ran; the next save is refused as above.
- Route e: one `TaskToggleFailed` [High].

**P2, a real detail** (`cb14_probe_real_sbu_detail`: a TEMP trigger on the session's own connection aborts the index update after the write; `rust-real-sbu-detail.txt`). This reaches the index arm: `SavedButUnindexed { detail: "database error: sqlite error: database is locked" }`, disk moved. Today's sentence: "Task could not be toggled: Saved, but not indexed yet: database error: sqlite error: database is locked It will appear after the next scan." The same with "database or disk is full". CD8's bare "database is locked" is the shape of the intent-clear and commit arms (by reading). The index is in WAL mode and the save opens its transaction IMMEDIATE before the write (`C/session.rs:4902-4918`), so a lock another process holds fails the save before the write (a `Db` error, not SBU).

**P3, core defect A** (`rust-defect-a.txt`). `open_base`, then `base_apply_edit` (RenameView) under the seam: `Err(SavedButUnindexed)`, disk holds "First". A second `base_apply_edit` on the handle without the seam: `Err(WriteConflict { current: <First's hash>, expected: <the original hash> })`, disk unchanged. The handle keeps its pre-write hash (`C/session.rs:20588` returns before `:20589-20597`).

**P4, core defect B** (`rust-defect-b.txt`). Rename `target.md` to `moved.md` (rewrites `cb14linker.md`), then `undo_structural` with the seam on the linker: `Ok(StructuralReport { moved: [moved.md → target.md], rewritten: [], failed: [cb14linker.md: Other("saved but not indexed: … (new hash …)")] })`; the linker on disk reads "See [[target]]." A restore that landed is reported as failed only (`C/session.rs:17723-17735`). Neither app calls `undo_structural` (W1).

**P5, NVDA on main and four priority prototypes** (worker W2, a report per run, NVDA 2026.3.1 with debug logging, the field-pass launcher with `-Root`, `SLATE_TEST_FAULT_AFTER_WRITE=cb14-`, under the desktop lock). The prototype (`proto-app.diff`, C# only) spoke the new sentence as a `HostComposed` in place of `TaskToggleFailed` and took both priorities from `CB14_PROTO`, with CD8's order (the new sentence first). NVDA logs a "Speaking" line for a sentence it then cancels, so W2 also recorded the output device's playback levels and matched each sentence's length and phrase pattern (A, main's sentence, 10.7 s; C, the new sentence, 11.3 s; B, the reconcile sentence, 5.6 s). "Heard" means the whole pattern played; no person listened.

| Route | main | new Medium, reconcile High | both High | new High, reconcile Medium | both Medium |
|---|---|---|---|---|---|
| a. editor Ctrl+Enter | A in full (the "ctrl+enter" key echo cut) | the key echo, then C in full | not run | not run | not run |
| c. Tasks panel through the tab | **B only; A silent** (2 of 2 samples) | **B only; C silent** | **B only; C silent** | C, then B | C, then B |
| e. Tasks Review, no tab | A, then the row's focus line | C, then the row's focus line | not run | not run | not run |

- NVDA received the two lines 30 to 44 ms apart and queued both (no rate-limiter coalescing); the first was lost to the second one's cancel: NVDA cancels speech for an ImportantMostRecent notification. This is #748's designed High etiquette, as CB-9b's spot check found (#1955, 2026-11-18).
- A Medium line waits for the key echo and for speech already playing; a High line cuts them. Focus speech after the toggle queued after the notifications and never cut them.
- After route a's save: "Save blocked. cb14-editor.md was modified externally. Your edits remain in the editor." in full.
- On route e, focus moved from the toggled row (3 of 3) to the review's first row (1 of 3) after the list republished, on main and on the prototype (the base run's NVDA log, kept with the chunk's working notes).

**P6, NVDA on the swap prototype, the create caveat and a plain review toggle** (W2's second pass; `proto-swap-app.diff`, `CB14_PROTO=swap`: the reconcile sentence first at High, unchanged, then the new sentence at Medium; on routes a and b the tab reconciles itself and mirrors its stale state to its peers first). Same method; a report per run (swap-1, swap-split, swap-split-editor, base-create, base-review-plain).

| Run | Route | Raised (log) | Heard in full (levels) |
|---|---|---|---|
| swap-1 | c. Tasks panel | B High, then C Medium 39 ms later | B (+97 ms to +5.67 s), then C (+6.05 s to +17.32 s) |
| swap-1 | a. editor Ctrl+Enter | B High, then C Medium 21 ms later | the key echo cut; B (+18 ms to +5.64 s), then C (+6.02 s to +17.29 s) |
| swap-1 | a, then End, "z", Ctrl+S | "Save blocked. cb14-editor.md was modified externally. Your edits remain in the editor." | in full |
| swap-1 | e. review, no tab | C Medium | C (+7 ms to +11.37 s), then the row's focus line |
| swap-split | c, the note in two split tabs | B High, B High 40 ms later, then C Medium | the first B silent; one B in full, then C in full; both tabs' status lines read B |
| swap-split-editor | a, the note in two split tabs | B High (one only), then C Medium | B, then C, in full; both tabs' status lines read B (the mirror reached the peer) |
| base-create | the sidebar's New Note (palette), `SLATE_TEST_FAULT_AFTER_WRITE=Untitled` | "Created note Untitled.md." Medium, then the caveat as a High host line 221 ms later | the caveat in full; "Created note Untitled.md." silent (filed as #2005) |
| base-review-plain | a plain review toggle on row 3, then row 2, no fault | "Task completed." Medium | "Task completed.", then focus on row 1 of 3 with no row selected, both times (filed as #2004) |

So the swap order (Option A) plays both sentences in full on every route measured, with one tab and with two; the per-tab reconcile lines of a split note still sound as one, as on main.

**What step 0 changes in the design.** CD8 as worded (the new event, then the reconcile sentence at High) leaves the new event unheard on routes c and d under NVDA, as today's failure sentence is unheard there now (P5). Design 7 and Call 2 take that up. Routes a and b never get the reconcile sentence or the stale mark CD8 assumed "still follows" (P1). Design 8 and Call 1 take that up.

## Design

The recommended shape keeps every line of the reconcile code as it is (its words, its High priority, its one line per tab) and changes where the new sentence is spoken: after the reconcile, at Medium (Option A in Call 2). Option B, CD8's order with the reconcile at Medium, is set out in Call 2 with its costs.

1. **Core, one rendering for the landed-unindexed clause** (`C/a11y.rs`). A private helper beside `lead_then_sentence`:

   ```rust
   /// "Saved, but not indexed yet: <detail>. It will appear after the next
   /// scan." The detail is closed as a sentence once (#1660, #1395); a blank
   /// one drops the colon.
   fn saved_but_unindexed(detail: &str) -> String {
       let saved = if detail.trim().is_empty() {
           "Saved, but not indexed yet.".to_owned()
       } else {
           lead_then_sentence("Saved, but not indexed yet:", detail)
       };
       format!("{saved} It will appear after the next scan.")
   }
   ```

   `vault_error_detail`'s SBU arm returns `saved_but_unindexed(detail)`. Every Windows site that speaks or shows that detail (the toggle failure today, and the sites listed for #1955) gains the period; no Windows site composes the clause.

2. **Core, the new event.** `TaskToggledUnindexed { completed: bool, detail: String }` in the Tasks group after `TaskToggleFailed`, with a doc comment naming #1660 and CD8. Render: `format!("{} {}", TaskToggled { completed }.render(), saved_but_unindexed(innermost_error_message(detail)))`: "Task completed. Saved, but not indexed yet: test fault: injected failure after write, before index commit. It will appear after the next scan.", and for P2's detail "Task completed. Saved, but not indexed yet: database is locked. It will appear after the next scan." (Lead's ruling, Call 5). `innermost_error_message` is a private helper beside the new helper: it strips, from the start of the detail and repeatedly, the Display wrappers core puts in front of an inner error, "database error: " (`VaultError::Db`, `C/lib.rs:218`), "sqlite error: " (`DbError::Sqlite`, `C/db.rs:46`) and "io error: " (`VaultError::Io`, `C/lib.rs:215`). Only the event's render calls it: `VaultError`'s Display (which `WriteMayHaveLanded` matches), `vault_error_detail` and every other caller keep the detail verbatim. Priority Medium (it stays out of the High list), as `TaskToggled` and `CreatePublishedUnindexed` are. `detail` is the SBU's own detail field, never `vault_error_detail` of it (that would say "Saved, but not indexed yet" twice).

3. **Core, the create caveat (#1395's create half).** The non-blank arm becomes `format!("{} It will appear in listings after the next scan; do not recreate it.", lead_then_sentence(&format!("{name} was written but not indexed:"), reason))`; the blank arm is unchanged. With the seam: "landed-outcome.md was written but not indexed: invalid argument: test fault: injected failure after write, before index commit. It will appear in listings after the next scan; do not recreate it." A reason that ends in a period renders as today. The variant's doc (`:917-926`) and the arm's comment (`:2247-2248`) stop saying "verbatim" and "byte for byte"; so do the comment at `S/CreateOutcomes.cs:25-27` and the doc of `T/CreateOutcomeTests.TheCaveatIsCoresRenderedCreatePublishedUnindexedEvent`.

4. **Count and corpus.** The count pin moves to 239 with its comment ("plan 47 CB-14 the landed-unindexed toggle, 239"). Corpus witnesses after `TaskToggleFailed`'s (`:4565-4571`): `TaskToggledUnindexed { completed: true, detail: "database error: sqlite error: database is locked" }` (P2, the index arm), `{ completed: false, detail: "database is locked" }` (the commit arm's bare shape), `{ completed: true, detail: "the index is locked." }` (already closed), and `{ completed: true, detail: "" }` (blank). One more `CreatePublishedUnindexed` witness with an unpunctuated reason ("database error: disk I/O error"); the existing row stays (the Swift fact at `M/Tests/SlateMacTests/A11yCorpusCensusTests.swift:718-769` finds it by identity). `corpus_renders_the_shipped_strings` gains the five rows; `tests/fixtures/a11y/corpus.json` is regenerated (`SLATE_REGENERATE_FIXTURES=1 cargo test -p slate-core a11y`).

5. **FFI** (`U/lib.rs`). The variant in the FFI `A11yEvent` (after `:10100-10102`) and its arm in `impl From<A11yEvent>` (after `:12031-12032`). `vault_error_detail` (`:12353`) maps through core unchanged. `./apps/slate-windows/generate-bindings.ps1 -Locked` regenerates the C# side; with no Mac here, `make regenerate-bindings` is not run, and the Mac lane proves the Swift side (AGENTS.md "An FFI change").

6. **Windows, where the new sentence is spoken** (Option A).
   - **The tab's failure arm** (`PublishTaskToggleThroughTab`, `:1932-1940`), on `VaultException.SavedButUnindexed unindexed`: `Status` becomes the event's rendering. `PublishTaskToggleOutcome` (`:1889-1915`) passes whether a completion follows. If one follows (routes c, d), the tab speaks nothing: the completion speaks the event after its reconcile. If none follows (routes a, b): Design 8, then the tab speaks the event.
   - **The completion's failure arm** (`:4199-4210`): on SBU it speaks `TaskToggledUnindexed(!task.Completed, unindexed.detail)` whether or not the tab published, at the arm's existing place, after the repair and the reconcile (`:4185-4189`). Every other error is unchanged (`TaskToggleFailed` only when the tab did not publish).
   - **The review's direct route** (`:946-950`): the event in place of `TaskToggleFailed`, first in the `RunEvery` as today. A tab that opened on the note while the write ran is reconciled after it by `DiskWriteLanded` at High, which by P5's mechanism cuts the new sentence; accepted and named (the race needs a tab to open on that note during the toggle's write), and left as main orders it.
   - Every other error keeps `TaskToggleFailed` at every site.

7. **The order: the reconcile sentence first, the new sentence second.** On routes a to d and f, a reader hears "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing." (High, unchanged) and then "Task completed. Saved, but not indexed yet: …. It will appear after the next scan." (Medium). A Medium raised after a High waits for it (P5; P6 measures this order). `ReconcileAfterExternalTaskWrite` and `ReconcileTabsAfterDirectTaskWrite` are not edited: their words, priority and one-line-per-tab behaviour stay, so a note open in two tabs still raises two High reconcile lines of which NVDA plays the second (as on main), and the splice-time sentences on the success arm (`:1945-1954`, `:1960-1969`, followed on routes c and d by the completion's success-arm reconcile at `:4224`) are heard as on main. This reverses CD8's literal order ("still follows"); Call 2.

8. **Routes a and b reconcile the note's tabs** (Call 1). On SBU with no completion, the tab runs `ReconcileAfterExternalTaskWrite(outcome.PostFailureDiskHash ?? unindexed.newContentHash, announce)` (stale mark against the landed hash, the reconcile sentence, `Status`), then `_documentChanged?.Invoke(this, null)`, the splice-time branches' own mechanism (`:1952`, `:1967`): every other tab on the note mirrors the stale state, the hash it was marked against and the `Status` silently (`MirrorSamePathDocumentState`, `:4398-4433`; `MirrorDocumentStateFrom`, `:887-902`), and, the source being clean, the panels and the review re-read the note (`:4428-4432`), which runs their pending-repair retry first. Then the tab speaks the event. One reconcile sentence per note. Nothing here repairs at once, rebaselines the tab, changes its text, saved hash or file identity, or touches the undo stack (the owner's #1991 ruling). It keys on SBU: in production every failure after the write is SBU (`C/session.rs:5236-5305`); a thrown test seam after the write (`TaskToggleFaultForTests`) keeps today's behaviour. A window remains until the repair runs: a `RefreshExternalStaleness` (a `Modified` event for the note, `:4435-4451`, or a property-write conflict, `S/WorkspaceViewModel.Properties.cs:295`, `:623`) recomputes not-stale against the unrepaired index; core's compare-and-swap still refuses the next toggle or save (P1), so nothing is lost. Named as accepted.

9. **The Mac.** No Swift product change. `M/Tests/SlateMacTests/A11yCorpusCensusTests.swift` lists the new witnesses after `.taskToggleFailed(detail: "")` (`:153`) and the new create witness after `:161`, in corpus order, one per line starting with "." (the Rust tripwire `the_mac_corpus_mirror_lists_every_event_in_order`, `U/lib.rs:15910`, reads it). The Mac lane proves it compiles; no Mac is at hand.

10. **The trigger ledger.** The new key has three Windows construction sites (the tab arm, the completion arm, the review) and no Mac site, so `docs/plans/18_windows_port/a11y_trigger_designations.json` gains a `designated.Mac` entry shaped like `AccentChosen`'s: `"TaskToggledUnindexed": { "reason": "CB-14 (#1660, CD8): the Mac's adoption is owed on #1954; until then performToggleCurrentTask posts taskToggleFailed with its own copy of the save sentence.", "decision": "docs/plans/47_windows_core_closeout_plan.md", "status": "recorded" }`. Then `python scripts/a11y_trigger_ledger.py --write` against the Windows Release build rewrites the ledger rows in `docs/plans/38_notification_dispatcher_contracts.md` (the new row; `TaskToggleFailed` keeps one construction in each of its four functions). The census checks (`T/Censuses/A11yTriggerParityCensus.cs:54-138`, `:316-322`) hold.

11. **Contract 38's addendum** (Lead's ruling, Call 2). A dated section in `docs/plans/38_notification_dispatcher_contracts.md`, before the generated ledger, "Landed-unindexed toggle amendment (plan 47 CB-14, #1660, #1395, CD8, 2026-11-18)": the new event and its Medium priority; the order on every route (the reconcile sentence first, at High and unchanged, then the event), with P5's and P6's measurements; the departure from CD8's literal order ("still follows") and its reason; the review's raced-open tab accepted; the innermost-message composition; and the routes a and b reconcile.

**What a reader hears after, with the seam's detail** (P5's mechanism; P6 measured the swap order on routes a, c and e, with one tab and two).

| Route | main (P5) | after |
|---|---|---|
| a, b | "Task could not be toggled: Saved, but not indexed yet: test fault: injected failure after write, before index commit It will appear after the next scan." | "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing." then "Task completed. Saved, but not indexed yet: test fault: injected failure after write, before index commit. It will appear after the next scan." |
| c, d | only the reconcile sentence (the failure sentence is cut) | the reconcile sentence, then the new sentence |
| c, d with a null read-back | the failure sentence | the new sentence alone (no reconcile runs, `:4159-4162`) |
| e | the failure sentence | the new sentence |
| e, a tab raced open | the failure sentence, cut by the reconcile | the new sentence, cut by the reconcile (accepted) |
| f, another tab open on the note | the failure sentence (High) after the reconcile, which it cuts | the reconcile sentence, then the new sentence |
| f, no other tab | the failure sentence | the new sentence |

**What stays out.**
- The buffer splice on SBU (CD8). The tab keeps the pre-toggle text, is stale, and the next save is refused, as on main.
- Any change to the undo stack or the tab's baseline on any toggle (the owner's #1991 ruling; see Overlap).
- Any edit inside `ReconcileAfterExternalTaskWrite` or `ReconcileTabsAfterDirectTaskWrite` (Option A).
- "It will appear after the next scan" after an immediate repair (Call 3).
- The other sites and converted routes SBU reaches (§9, CB-46), and core defects A and B (Call 4).
- The Mac's adoption of the event, and its two private copies of the save sentence (#1954).
- Trimming "database error: sqlite error: " from a detail (Call 5).

## Tests

A red commit first, then the fix. The red commit carries every fact below, the core and FFI variant (`TaskToggledUnindexed` declared, rendered as main's `TaskToggleFailed` text so the Rust facts are red on behaviour), its corpus rows, the regenerated `corpus.json` and both hand mirrors (C# and Swift), so the mirror tripwires are green and each fact is red for its own reason; the PR body shows each red. No fact adds a sleep or a timed wait; the C# facts wait on conditions with the existing `PumpDispatcherUntil` and `PumpUntil` helpers. Every fact that sets `SLATE_TEST_FAULT_AFTER_WRITE` takes `EnvFaultLock` and uses a note name and trigger no other fact uses (for example `cb14-landed-editor.md` with the trigger `cb14-landed-editor`), because the seam matches any path containing the text and test classes run in parallel.

**Rust, `C/a11y.rs` tests.**
1. `a_landed_unindexed_toggle_says_it_landed`: completed and reopened with the seam's detail, P2's detail, the bare "database is locked", a detail ending in ".", one ending in "(os error 2)", and a blank one ("Task completed. Saved, but not indexed yet. It will appear after the next scan."); priority Medium.
2. `an_unindexed_save_detail_closes_its_reason`: `vault_error_detail(SavedButUnindexed)` for the same details.
3. `the_landed_toggle_and_the_save_detail_share_one_clause`: for every detail above, the event's text after its first sentence equals `vault_error_detail` of an SBU whose detail is the innermost message (equal to the detail itself where it has no wrapper). 3b. `the_landed_toggle_speaks_the_innermost_error_message`: the wrappers are exactly core's Display prefixes: for `VaultError::Db(DbError::Sqlite(<rusqlite "database is locked">))` and `VaultError::Io(io::Error::other("Access is denied. (os error 5)"))`, the event built from `e.to_string()` speaks the inner error's own text; `vault_error_detail` of an SBU with the wrapped detail keeps the wrapper; and a detail whose text merely contains "database error: " later on is not cut.
4. `the_unindexed_create_caveat_closes_its_reason` replaces `the_unindexed_create_caveat_is_the_windows_sentence_byte_for_byte` (`:6908-6930`): the seam's reason renders closed; priority Medium. The last assert of `an_empty_create_reason_leaves_no_dangling_separator` (`:6954-6963`) expects "notes.md was written but not indexed: database error: disk I/O error. It will appear …"; its doc drops "Closing the sentence … is #1395".
5. The count pin (239), `corpus()`, `corpus_renders_the_shipped_strings`, `committed_corpus_artifact_matches_the_vocabulary` (`:8333-8376`), and the two mirror tripwires (`U/lib.rs:15910`, `:16021`).

**C#, edited facts** (each red on main's product code once edited).
6. `TasksPanelTests.ATabToggleSpeaksCoresEvents` (`T/TasksPanelTests.cs:539-618`), its fault half (its note is already `fault-tab-words.md`): exactly one reconcile `HostComposed` (High) and then exactly one `TaskToggledUnindexed`, `Completed` true, rendering exactly the seam sentence; the reconcile's index is lower; no `TaskToggleFailed`; the tab's final `Status` is the reconcile sentence.
7. `TasksPanelTests.RealPreCommitFailuresRepairTheIndexOnTheTabRoute` (`:958-1017`): waits on `TaskToggledUnindexed` whose `Detail` holds "test fault"; the rest holds (the single reconcile sentence `:1005-1007`, the conflict on retry).
8. `TasksPanelTests.ThePanelHonorsTheSharedRepairQuarantine` (`:1019-1082`): waits on `TaskToggledUnindexed`.
9. `TasksReviewTests.AReviewToggleSpeaksCoresEvents` (`T/TasksReviewTests.cs:659-706`): one `TaskToggledUnindexed`, `Completed` true, with the exact seam sentence; no `TaskToggleFailed`.
10. `TasksReviewTests.RealPreCommitFailuresRepairTheIndexBeforeReloading` (`:708-754`): `OfType<TaskToggledUnindexed>()` at `:740`.
11. `CreateOutcomeTests.TheReportingCreateSeparatesTheLandedArmFromARefusal` (`T/CreateOutcomeTests.cs:22-62`): the caveat at `:57` equals the closed sentence exactly, not only its prefix.

**C#, new facts.**
12. `TheEditorsLandedUnindexedToggleReconcilesTheNotesTabsThenSaysItLanded` (`T/SaveChainAdmissionTests.cs`, on its `Peers` harness, two tabs on the note): tab A's `ToggleTask` with no completion under the seam. One reconcile sentence (High), then one `TaskToggledUnindexed` with `Completed` true and the exact text; A and B both `IsExternallyStale` with B silent; both `Status` the reconcile sentence; A's buffer and `SavedContentHash` the pre-toggle ones; `CanUndo` false; an edit and save on A is `NoteSaveConflict` and the disk keeps `[x]`.
13. `ReadingViewTests.ATaskCheckboxThatLandedUnindexedSaysItLanded`: the reading route through `ReadingContentViewModel.ToggleTaskAt` under the seam, tab-only as `TaskCheckboxTogglesThroughTheCoreTaskCommand` (`T/ReadingViewTests.cs:2012-2060`) with its own note name: the reconcile sentence, then the event with `Completed` true and the exact text; the tab stale.
14. `AClosedTabsLandedUnindexedToggleSaysItLandedAfterThePeersReconcile` (`T/SaveChainAdmissionTests.cs`, `Peers`): route f, the panel toggle through tab A, held at `TaskToggleBeforeWriteForTests` while A is closed, under the seam: B's reconcile sentence, then one `TaskToggledUnindexed` from the completion with `Completed` true and the exact text; no `TaskToggleFailed`.
15. `TheNotesSecondTabStillHearsOneSentencePerTabOnThePanelRoute` (`Peers`, route c under the seam): the completion's reconcile lines (one per tab, High, as on main), all before the one `TaskToggledUnindexed`. A guard that Option A leaves the reconcile code's per-tab behaviour alone.
16. The non-SBU guards, named in the PR as proof that only SBU moved: route c, `TasksPanelTests.PostWriteToggleFailuresReconcileInsteadOfAssumingUnchanged` (`:906` on); route f, `TasksPanelTests.AFailedToggleSpeaksCoresDetailWithAClosingPeriod` (`:507-532`); route e, `TasksReviewTests.PostWriteFailuresStillRefreshTheSnapshot` (`:609-651`) and the middle half of `AReviewToggleSpeaksCoresEvents` (`:673-682`); routes a and b, `SaveChainAdmissionTests.ATaskToggleWhoseWorkerThrowsEndsAndFreesTheNotesChain(afterTheWrite: true)` (`:545-580`).
17. `ASuccessfulToggleThroughATabStaysOnItsUndoStack` (`T/SaveChainAdmissionTests.cs`, a guard for the owner's #1991 ruling, green on main and on head): a successful toggle through the tab's `ToggleTask` leaves `CanUndo` true and one Undo restores "- [ ] the task" in the buffer. If an existing fact already pins this (search `CanUndo` and `UndoStack` in `T/`), it is named instead.

**Censuses and mirrors.** `T/Censuses/A11yCorpusCensus.cs` lists the five new witnesses in corpus order. `A11yTriggerParityCensus` holds after the ledger is regenerated. `scripts/check_windows_census_trait.py` is unaffected (no new census-path read). The parity matrix does not move (no command, chord, leaf, tab, help doc or verb).

**Mutations** (each reverted after it is shown; the PR body quotes the facts each turns red).
- M1a: `vault_error_detail`'s SBU arm back to the verbatim splice: 2, 3.
- M1b: the helper back to the verbatim splice: 1, 2, 6, 9, 12, 13, 14.
- M2: the event's render with `completed` inverted: 1; each Windows site passing `task.Completed` instead of `!task.Completed`: 6 (completion arm), 9 (review), 12 and 13 (tab arm), 14 (completion arm, unpublished).
- M3: each site, one at a time, back to `TaskToggleFailed`: the tab arm turns 12 and 13 red; the completion arm 6, 7, 8, 14; the review arm 9, 10.
- M4: the completion speaks the event before its repair and reconcile: 6, 14, 15. The tab arm speaking the event when a completion follows: 6 (two events).
- M5: Design 8's reconcile removed: 12, 13. Its `_documentChanged` call removed: 12 (B not stale).
- M6: the create arm back to the splice: 4, 11.
- M7: the SBU test widened to every error at the tab arm: the route a/b guard in 16 (`afterTheWrite: true` speaks the new event).

## Files this chunk edits

Product: `C/a11y.rs`; `U/lib.rs`; `S/WorkspaceViewModel.cs` (`PublishTaskToggleOutcome`, the tab's failure arm, the completion's failure arm); `S/Panels/TasksReviewViewModel.cs` (`PublishDirectToggle`'s failure line); `S/CreateOutcomes.cs` (a comment only). Generated or data: `tests/fixtures/a11y/corpus.json`; `docs/plans/18_windows_port/a11y_trigger_designations.json`; `docs/plans/38_notification_dispatcher_contracts.md` (the ledger block only). Tests: `T/TasksPanelTests.cs`, `T/TasksReviewTests.cs`, `T/SaveChainAdmissionTests.cs`, `T/ReadingViewTests.cs`, `T/CreateOutcomeTests.cs`, `T/Censuses/A11yCorpusCensus.cs`, `M/Tests/SlateMacTests/A11yCorpusCensusTests.swift`. No new file, so no license-header run. Five non-test product files (one of them a comment).

## Overlap

- **#1991, closed 2026-11-18 by the owner's ruling** (as working as intended): a task toggle made through a tab is the reader's own edit and stays on the tab's undo stack, on every route through a tab (the editor, the reading view, and the Tasks panel or review through an open tab). CB-14 keeps that true. It does not clear the undo stack and does not rebaseline the tab on any toggle: the success arm's splice (`:1972`), its `MarkSavedAfterVerifiedDelta` (`:1978-1983`) and the undo step the splice leaves are untouched. On SBU no splice runs, so the toggle never entered the buffer and there is no undo step to keep or drop (P1, `CanUndo=False`). Design 8 only marks tabs stale and speaks: `ReconcileAfterExternalTaskWrite` (`:2044-2065`) sets `IsExternallyStale` and `_staleAgainstHash`, drops the interaction caches and sets `Status`, and the mirror copies those to the peers; neither changes the text, the saved hash or the undo stack (fact 12 asserts all three unchanged). Fact 17 pins the ruling on the success arm. Plan 47 records the ruling in §3.1 (the issue's comment); until the lead's docs branch lands it, this spec cites the issue.
- Plan §6, `a11y.rs`, the corpus and the variant count: CB-14 holds the first slot (then CB-32, CB-16, CB-20b, CB-26). The next of those rebases on 239 and regenerates `corpus.json` and the three mirrors.
- Plan §6, `U/lib.rs`: CB-14 is the only C6 chunk on it.
- Plan §6, `WorkspaceViewModel*.cs` and `Panels/TasksReviewViewModel.cs`: CB-9a, CB-11 and CB-9b (merged; #2001 is under this branch), then CB-14, then CB-15c and CB-22.
- C6's other chunks: CB-30 (math speech and braille; its spec edits no file above and adds no `a11y.rs` corpus row) and CB-25 (`S/AutomationLandmark.cs`). No shared file.
- CB-15a to CB-15c (#1890) own the "@message=" sweep; this chunk adds no binding-message read. `WriteMayHaveLanded` (`S/WorkspaceViewModel.Properties.cs:1322-1323`) matches core's Display text "saved but not indexed:" (`C/lib.rs:291`), which this chunk does not change (#1955 carries that coupling, 2026-11-18).

## The NVDA spot check

On head, the P5 driver (kept with the chunk's working notes) and fixture (`cb14-editor.md`, `cb14-panel.md`, `cb14-review.md`; `SLATE_TEST_FAULT_AFTER_WRITE=cb14-`; `-Root`), fresh launches, under the desktop lock, with the playback levels. Base phrases are P5's (R1 to R3) and P6's (R4).

- **R1b, reading view** (Lead's ruling, Call 1): the same note in reading mode, the checkbox toggled by keys (Ctrl+Enter or Space on the task, `S/Reading/ReadingNavigator.cs:165-191`): the reconcile sentence, then the new sentence, each in full. Base phrase: one P5-style base run of this route is added to the head pass's base launch.
- **R1, editor Ctrl+Enter** on "- [ ] editor task": the "ctrl+enter" echo, then "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing.", then "Task completed. Saved, but not indexed yet: test fault: injected failure after write, before index commit. It will appear after the next scan.", each in full. Then End, "z", Ctrl+S: "Save blocked. cb14-editor.md was modified externally. Your edits remain in the editor."
- **R2, Tasks panel through the tab** on "panel task": the reconcile sentence, then the new sentence, each in full, then the Done row's focus line. Once more with the note in two split tabs: one reconcile sentence heard, then the new sentence.
- **R3, Tasks Review with no tab** on "review task": the new sentence in full, then the row's focus line.
- **R4, the create caveat** (`SLATE_TEST_FAULT_AFTER_WRITE=Untitled`, New Note from the File menu or the command palette; Ctrl+N is not bound on Windows, `S/Commands/ChordTable.cs:1013-1016`, `:1450-1452`): the caveat now closes its reason, "Untitled.md was written but not indexed: invalid argument: test fault: injected failure after write, before index commit. It will appear in listings after the next scan; do not recreate it.", in full. "Created note Untitled.md." stays silent on head as on base (P6): the sidebar raises the caveat as a High host line after it (`S/FilesSidebarViewModel.cs:1867-1875`, `:2866-2885`), filed as #2005 and not changed here.

The PR body records each line, with the playback-level evidence for "in full". VoiceOver: `not delivered: VoiceOver pass` (no Mac), with the #1954 line.

## #1954 line (Mac work owed), posted before merge

"CB-14, #1660 and #1395, PR #2008. Speech or text. (1) The new core event `TaskToggledUnindexed` ("Task completed. Saved, but not indexed yet: <detail>. It will appear after the next scan.", Medium) crosses the FFI. The Mac does not post it: `performToggleCurrentTask` (`AppState.swift:14408-14412`) still posts `.taskToggleFailed(detail: humanReadable(error))` on the editor route, "Task could not be toggled: This change was saved but not indexed yet: … It will appear in listings after the next scan; do not recreate it.", and the Tasks panel, note view and reading view routes say nothing. On Windows the reconcile sentence ("Task toggled on disk, but the editor no longer matches it. Reopen the note before editing.") comes first and the new sentence second, both heard under NVDA. Owed: the adoption (post `.taskToggledUnindexed(completed:detail:)` with the error's own `detail` on `SavedButUnindexed`, on every route that announces an outcome, ordered after any sentence that would cancel it) and a VoiceOver pass. The trigger ledger names it as a recorded Mac designation. (2) `CreatePublishedUnindexed`, which the Mac posts from `AppState+CreateReporting.swift:52-56`, now closes a reason that has no terminal punctuation, for example "Untitled.md was written but not indexed: database error: disk I/O error. It will appear in listings after the next scan; do not recreate it." Owed: a VoiceOver pass. (3) For #1527: the Mac's two copies of the save sentence (`AppState.swift:14038-14041`, byte-identical to core's old run-on, and `:26032-26036`) still run an unpunctuated detail into the next sentence; core's `vault_error_detail` no longer does. Not run: anything on a Mac."

## #1955 comment (deferred finds), posted before merge as ONE comment (Lead's ruling, Call 4)

1. **The other places `SavedButUnindexed` reaches** (plan §9: the fixes are CB-46's). Each is a write that landed (core's bytes are on disk) reported as a failure. All files under `apps/slate-windows/src/SlateWindows/`. For each: the file, what it does today, and the failing test (the seam set to a unique note name under `EnvFaultLock`).
   1. `WorkspaceViewModel.cs:1305-1306`, `PublishSave` (`:1354`, `:1387-1392`): the editor save speaks "Save blocked. Could not save X: Saved, but not indexed yet: …. It will appear after the next scan. Your edits remain in the editor." and leaves the tab dirty on its old hash, so the next save is a false conflict. Test (`SaveChainAdmissionTests`): `SaveNow` under the seam gives no `NoteSaveBlocked`, `!tab.IsDirty`, `SavedContentHash` equals the disk; a second save without the seam gives no `NoteSaveConflict`.
   2. Property row set and delete (`WorkspaceViewModel.Properties.cs:446`, `:451`, `:549`, `:558`; worker catch `WorkspaceTabViewModel.Properties.cs:319-340`; completion `WorkspaceViewModel.Properties.cs:602-642`, read-back reconcile `:651-663`): "Property edit failed: " with the binding's message (`failure?.Message`) until CB-15; the row's draft is kept. Test (`NotePropertiesTests`): a row edit under the seam announces `PropertyChanged` and no `PropertyEditFailed`.
   3. Add property (`WorkspaceViewModel.Properties.cs:1786`, `:1840`; completion `:273-314`, `MarkAddFailed` `:292`): the same sentence, the sheet stays open, and a retry meets "key exists". Test (`AddPropertyTests`): the sheet is not marked failed and no `PropertyEditFailed`.
   4. Bases cell on an open tab (`WorkspaceViewModel.Bases.cs:2089-2101`): "Base edit failed: Saved, but not indexed yet: …"; the tab keeps a stale saved hash. Test (`BasesCellEditTests`): no `BasesCellEditFailed`, the tab's `SavedContentHash` equals the disk.
   5. Bases cell with no tab (`WorkspaceViewModel.Bases.cs:2161-2194`): the same sentence; no refresh. Test: `BasesCellSaved` is announced.
   6. Bases view export (`WorkspaceViewModel.Bases.cs:950`, `:962-965`): "Base view could not be exported: …". Test (`BasesExportTests`): `BasesViewExported`.
   7. Saved query export (`WorkspaceViewModel.Bases.cs:1758-1766`): "Saved query could not be exported: …"; the queries list is not refreshed. Test: `BasesSavedQueryExported`.
   8. Base sort save (`Bases/BaseDocumentViewModel.cs:599`, `:610-623`): "Base sort could not be saved: …"; with core defect A (line 2), the next edit is a false conflict. Test (`BasesDocumentTests`): `SaveSortToView` under the seam, then again without it: no `BasesSortSaveFailed`, no conflict.
   9. Base builder save (`Bases/BaseDocumentViewModel.cs:759`, `:764-776`): "Base view could not be saved: …"; the builder stays open. Test (`BasesBuilderTests`): `ApplyBuilderEdits` completes true; a second apply does not conflict.
   10. History restore (`WorkspaceViewModel.History.cs:237`, `:248-251`): an alert titled "Restore failed" with the exception's message (`:306-309`); the open tab keeps the pre-restore bytes. Test (`HistoryPanelTests`): no alert, `RestoredVersionFrom` announced, the tab shows the disk.
   11. Bulk rename (`Panels/BulkRenameViewModel.cs:334-336`, `:503`, `:538`): the note's row says "Failed: error: saved but not indexed: <detail> (new hash "<64 hex>")". Test (`SaveChainAdmissionTests`, extending `AWriteWhoseIndexCommitFailedStillBringsTheNotesCleanTabsUpToDisk`, `:5100-5173`): `RenameSummary.Failed == 0`.
   12. Tag edit (core `C/session.rs:9841-9846`): the landed note is left out of core's count ("Tagged 0 files…"). Test: the same extension: "Tagged 1 file with #probe."
   13. Rename and move link rewrites (`FilesSidebarViewModel.FileManagement.cs:68-87`): "Links in 1 note could not be updated." for a rewrite that landed; batch moves speak `BatchMoveSummary` (`FilesSidebarViewModel.cs:2458-2477`), which names no failed rewrite. Test: a red twin of `FileManagementTests.RewriteFailureDetailSurvivesTheSuccessSentenceInStatus` (`:1329-1368`) asserting no "could not be updated" when the linker holds the rewrite.
   Found by CB-14 (PR #2008).
2. **Core defect A** (`crates/slate-core/src/session.rs`, `base_apply_edits`, `:20588`): on `SavedButUnindexed` it returns before updating the handle's `content_hash` and `source` (`:20589-20597`), so the next edit on the handle is a false `WriteConflict` (measured). Failing test: in `crates/slate-core/src/session/tests/bases.rs`, `base_apply_edit` under the seam, then a second edit on the same handle returns `Ok`. Medium by `AGENTS.md` (`session.rs` only); deferred by plan §4 rule 5 (a core change a Mac user gets different behaviour from; the Mac calls `base_apply_edit`). Found by CB-14 (PR #2008).
3. **Core defect B** (`crates/slate-core/src/session.rs`, `undo_structural`, `:17723-17735`): a link-rewrite restore that landed is reported in `failed` only, as `Other`, never in `rewritten`, with no rescan flag, where its siblings classify it through `classify_rewrite_after_save_error` (measured). No app calls it today. Failing test: in `crates/slate-core/src/session/tests/structural.rs`, a rename that rewrites a linker, then `undo_structural` with the seam on the linker: the linker is in `rewritten`. Medium; deferred by plan §4 rule 5. Found by CB-14 (PR #2008).

## Found on the way

- **The CD8 sentence would not be heard on routes c and d** (P5). Fixed here (Designs 6 and 7, Call 2): a defect in the PR's own feature.
- **Routes a and b leave the note's tabs unmarked and silent over the landed write** (P1). Fixed here if Call 1 is ruled as recommended (Design 8).
- **Tasks Review focus jumps to its first row after a toggle republishes the list** (P5, route e; P6, a plain toggle on rows 3 and 2, no fault). Not CB-14's feature. A focus-survives-republish defect (`AGENTS.md`, "Accessibility gates"), High (it changes what NVDA reads after every review toggle): filed as #2004.
- **`TaskToggleConflict` tells a Windows reader to "Resolve in the dialog"** (`C/a11y.rs:2211-2213`); Windows has no resolve dialog (the reason `NoteSaveConflict` exists, `:904-916`). Pre-existing on main through core's compare-and-swap; Design 8 adds a second way to it (the stale guard). Sized High (a changed core sentence, a Windows-specific event, an FFI change): filed as #2003.
- **The sidebar's untitled create raises its unindexed caveat as a High host line after a Medium "Created note …"** (`S/FilesSidebarViewModel.cs:1867-1875`, `:2866-2885`), which cuts the created sentence (P6, base-create: the caveat in full, "Created note Untitled.md." silent), against core's design note that the caveat is spoken after the created sentence and never instead of it (`C/a11y.rs:917-926`). Five sidebar create routes do it. High (it changes what every sidebar create route speaks, across more than three files): filed as #2005. Not CB-14's feature: CB-14 changes the caveat's words, not where or at what priority the sidebar raises it.
- Core defects A and B: #1955 lines 2 and 3.

## Calls for the lead

1. **Routes a and b (the editor's Ctrl+Enter and the reading view).** CD8 assumed the reconcile sentence follows everywhere; on these routes nothing reconciles the tab (P1). With the new event they would say "Task completed" while the editor shows the task open, the tabs are not stale, and only the next save says "modified externally". **Recommended:** Design 8, here, as a defect in the PR's own feature: the tab reconciles itself and mirrors the stale state to its peers when no completion follows (one sentence per note, no repair, no rebaseline, no undo change). Cost: one branch in `WorkspaceViewModel.cs`, facts 12 and 13, and a new occasion for an existing sentence. The owner's record on #1660 says the code behind that sentence is left alone and nothing comes back to the owner; Design 8 does not edit that code, but it calls it on two more routes, and CB-9a's record treated an existing sentence at a new occasion as owing the owner's ear. The lead rules whether this goes to the owner. Alternative: speak the new event alone on these routes and file the gap (cost: an issue; the mismatch ships).
2. **Order and priority** (P5, P6). Whatever comes first is cut when a High follows it. Two shapes make both sentences heard.
   - **Option A (recommended): the reconcile sentence first, unchanged (High); the new sentence second (Medium).** No line of the reconcile code changes, so two tabs and the splice-time sentences sound as on main. Costs: it reverses CD8's literal order ("still follows"); the review's raced-open tab (route e) still cuts the new sentence (rare, accepted). Measured (P6): both sentences in full on routes a, c and e, and with the note in two split tabs (the second tab's reconcile line cuts the first, as on main, so one is heard, then the new sentence).
   - **Option B: CD8's order; the reconcile sentence Medium.** Measured heard on route c with one tab (P5, both-Medium column). Costs: three edits inside the reconcile code the owner's record leaves alone. At Medium, a note in two tabs hears the reconcile sentence twice (one line per tab, `:4240-4243`; no longer cut), so it must speak once per note, CB-9b's precedent (`S/WorkspaceTabViewModel.Properties.cs:251-255`). On routes c and d, a success whose splice found the editor changed now plays the splice-time High sentence and then the completion's Medium reconcile (`:4224`): "…the editor changed…" then "…no longer matches it…", or on the `:1966` branch the same sentence twice, so the completion must stay silent for a tab that has just spoken its own. Route f orders the reconcile before the toggle sentence (`:4185-4210`), so B's ordering holds only on a to d.
   Either shape departs from a word of the owner's record (A from "follows", B from "left alone"). The lead rules which, and whether it goes to the owner with the P5 and P6 tables.
3. **"It will appear after the next scan" after an immediate repair.** On routes c to f the index is repaired at once and the repair succeeds (P1: nothing pending right after), so the caveat is out of date when spoken there; on routes a and b a repair is pending. **Recommended:** keep, as CD8 says ("always that event on this error"). Cost: none.
4. **The reach list and core defects A and B.** **Recommended:** the three #1955 lines above, nothing fixed here: §9 gives the reach list to CB-46, and plan §4 rule 5 keeps the two core defects (each Medium, `C/session.rs` only, a Mac-visible behaviour) out of this chunk. The editor save's state half (line 1.1) would be Small by `AGENTS.md` (a file this PR changes); §9 defers it with the rest. Cost: none here.
5. **The detail.** A failure in the index step reads "database error: sqlite error: database is locked" (P2); the commit step's reads "database is locked" (by reading). Recommended was verbatim; ruled otherwise (the event composes the innermost message, Design 2).
6. **The note for #1527.** **Recommended:** carry it in the #1954 comment (its part (3)), as #1954's body lists it, and post nothing on #1527 itself. Cost: none.
7. **The filed finds** (High, filed by the process rule, no ruling needed; named so the lead knows): #2003 (the Windows toggle refusal names a dialog Windows does not show), #2004 (a Tasks Review toggle moves focus to the first row), #2005 (a sidebar create's High caveat silences the created sentence).

## Verification

- Rust: `cargo fmt --all -- --check`; `cargo clippy --workspace --all-targets --locked -- -D warnings`; `cargo test -p slate-core -p slate-uniffi -p slate-cli --locked --no-fail-fast`, all with a per-worktree `CARGO_TARGET_DIR`.
- Windows: `./apps/slate-windows/generate-bindings.ps1 -Locked`; the Release build; the edited and new facts by `--filter`; `dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <each edited .cs>`; the whole unit run under the desktop lock before the push; the ledger `--write` and `--check`; `python3 scripts/generate-parity-matrix.py --check`; `python scripts/check_windows_census_trait.py`.
- The mutations above, each shown red, then reverted.
- The head NVDA pass (R1 to R4).

## Acceptance

- On every Windows route that publishes, a toggle whose write landed and whose index commit failed speaks `TaskToggledUnindexed`, never `TaskToggleFailed`; every other failure still speaks `TaskToggleFailed`.
- Under NVDA, routes R1 to R3 are heard in full as listed; R4's caveat closes its reason.
- `vault_error_detail(SavedButUnindexed)` and `CreatePublishedUnindexed` close an unpunctuated reason once and drop the colon for a blank one.
- No toggle changes the undo stack or rebaselines a tab beyond what main does (#1991).
- The corpus, its three mirrors, the count pin, the trigger ledger and its designation agree; every lane green.
- The PR body: `Closes #1660`, `Closes #1395`; the facts; the NVDA phrases on base and head; "Found and fixed here"; `not delivered: VoiceOver pass`; the count line. The #1954 and #1955 comments are posted before merge.

## Red team record (2026-11-18)

A fresh Opus red team read the first draft at eeb99032 (its report is kept with the chunk's working notes): 0 blockers, 9 mediums, 12 lows. Each was checked against the code and taken:

- M1 (a note in two tabs would hear a Medium reconcile twice) and M2 (the splice-time sentences are followed on routes c and d by the completion's reconcile, so a Medium reconcile makes them heard twice): the first draft's Design 7 (the reconcile at Medium) is now Option B with these as its costs; the recommended Option A changes no line of the reconcile code, and P6 was added to measure its order with one and two tabs. Fact 15 guards the per-tab behaviour.
- M3 (the order at route f, route e and the review's raced-open success route was misstated): the table and Call 2 are corrected; the claimed benefit on the review's success route is dropped.
- M4 (Design 8 left a second tab clean): Design 8 now mirrors the stale state to the note's other tabs through `_documentChanged`; fact 12 runs on `Peers`.
- M5 (the owner's #1660 record): quoted in the header and in Calls 1 and 2, with the owner question put to the lead.
- M6 (the fault seam matches across parallel classes): every seam fact uses a unique note name and trigger.
- M7 (R4 named an unbound key and expected the wrong priority): R4 uses the menu or palette; P6 measures it on base.
- M8 (M1 and M2 did not bite as claimed): M1 is split in two; facts 12 to 14 assert `Completed` and the exact text.
- M9 (#1955 line 1 lacked the format and misplaced files): one numbered comment with each site's file, behaviour today and failing test written out.
- Lows: the per-arm detail shape (Current state, P2, Call 5, a bare-shape corpus witness); the doc comments and `CreateOutcomes.cs` added to the files; the fact and line names corrected; the non-SBU guards named per route with M7; the null read-back row; the stale-mark window named; the "every route" exception stated; the `TaskToggleConflict` wording find added; main's head and the #1991 citation noted; the red commit named as such; Design 8's SBU key justified; rule 5 cited for defects A and B.

## What delivery changed (2026-11-18, branch claude/cb14-unindexed-toggle-landed)

- **Facts renamed and moved.** Fact 14 (route f) is `TasksPanelTests.AClosedTabsLandedUnindexedToggleSaysItLandedAfterThePeersReconcile`, on `TasksPanelTests`' own workspace with a split. Fact 15 is `TasksPanelTests.TheNotesTabsAreReconciledBeforeTheLandedSentenceOnThePanelRoute`. Fact 17 (the #1991 guard) is new: no existing fact pinned the success arm's undo step.
- **Lead's ruling on Call 5.** The innermost-message composition is `innermost_error_message`, used only in `TaskToggledUnindexed`'s render. Fact 3b is `the_landed_toggle_speaks_the_innermost_error_message`. A real index failure's sentence reads "Task completed. Saved, but not indexed yet: database is locked. It will appear after the next scan."
- **The diff's red team (0 blockers, 3 mediums, 10 lows), taken in 7162a588.**
  - The completion falls back to the error's landed hash when the read-back is unreadable. A test seam, `TaskToggleReadBackFaultForTests`, makes the read-back unreadable.
  - The tab arm's steps and the completion's failure steps each run through `AdmittedPublication.RunEvery`.
  - The non-SBU guard asserts one `TaskToggleFailed`, no landed sentence, no reconcile line and neither tab stale.
  - Doc comments the new callers made untrue are corrected (`ReconcileAfterExternalTaskWrite`, `TaskToggleCompletion`, `MirrorSamePathDocumentState`, the Mac's `AfterWriteFault.swift`).
- **Codex round 1 (one high), taken in 2b79dcd9.** A note renamed or moved between the landed write and the publication was missed by the completion's path-keyed reconcile. The tab that published now reconciles itself and its peers whenever its path moved. Fact: `APanelToggleWhoseNoteIsRenamedBeforeItPublishesStillReconcilesTheRenamedTab`.
- **Codex round 2: approve.** Two accepted-risk candidates go in the PR's register. Each needs SBU plus two independent conditions:
  - a renamed tab's throwing reconcile sentence skips the completion;
  - a renamed source tab closed before publication leaves its peers at the new path to the path-keyed completion.
- **Accepted from Codex round 1.**
  - The tabless review route keeps main's no-reconcile on an unreadable read-back.
  - Two pre-existing fault facts share their trigger names under `EnvFaultLock`.
- **Mutations.**
  - Rust: M1a, M1b, M2, M6 and M8 (the innermost message) all bite.
  - Windows: M2a to M5b all bite. M9 (no landed-hash fallback), M10a and M10b (steps not each attempted) and M11 (a renamed tab left to the completion) were added. M7 was not run as a mutation; the strengthened guard covers it.
- **Finds filed.** #2003, #2004, #2005.
- **Codex round 2's first case, taken in bb726e05.** `PublishTaskToggleOutcome` runs the tab's publication and the completion as separate `RunEvery` steps, so a throwing tab sentence no longer skips the completion. Fact: `ARenamedTabsThrowingReconcileSentenceStillLetsTheCompletionSayItLanded`. Mutation M12 bites.
- **The throwing-sentence facts.** `RunEvery` rethrows the first failure after every step, and that surfaces through the pumped dispatcher. The facts take it once (`PumpPastTheAnnouncersFailure`, bea36aa3).
- **Codex round 3: approve, no findings.**
- **The designation's decision.** It now cites `docs/plans/38_notification_dispatcher_contracts.md` (380eb1fd). The Windows docs-trigger census requires every docs path the lane reads to be on `windows.yml`'s path list, and plan 47 is not.
- **The merge.** Main 4b700172 (CB-25, #2009) was merged in as f08a3656, and the affected classes reran 397/0.
- **CI.** Green at f08a3656: 34 pass, 4 skipping.
- **PR and comments.** PR #2008. #1954 issuecomment-6978966764. #1955 issuecomment-6978967335.
- **The head NVDA pass (bea36aa3).** It confirmed the order on every route, and added the reading view (R1b) on base and head. The reading view's checkbox still shows the pre-toggle state after a landed toggle, on base and on head: it reflects the tab's text, which is not rebaselined.
- **The independent judge (standing in for Codoki): Merge, no blocker or major.** The lead took three minors before merge, the two behaviour changes red first (688b4590), fixed in 9237f8dc; the third is a guard (below).
  - Route e (`PublishDirectToggle`) now raises `DiskWriteLanded` before `TaskToggledUnindexed`, so a tab that opened on the note during the write hears the reconcile sentence and then the landed one; with a null read-back it uses the landed write's `newContentHash`. Facts: `ATablessReviewToggleThatLandedReconcilesATabThatOpenedBeforeItSaysItLanded`, `ATablessReviewToggleThatLandedReconcilesATabThatOpenedWhenItsReadBackFails`. Test seam: `DirectToggleReadBackFaultForTests`. Both round-1 accepted risks on route e are closed.
  - Routes a and b: a non-SBU failure whose read-back moved now says it failed, then reconciles the tab and mirrors its peers, as the panel's completion does. The guard `ATaskToggleWhoseWorkerThrowsEndsAndFreesTheNotesChain(afterTheWrite: true)` now asserts that (it had pinned main's clean tabs over changed disk). Resolved by reconciling, so no #1955 line. Under NVDA the High reconcile line cuts the High failure sentence, as on the panel route on main: an accepted risk in the PR.
  - `AnEditsUndoHistorySurvivesALandedUnindexedToggleOnTheTabAndItsPeer` (undo on the source tab, and on the peer): an edit made and saved before a landed toggle still undoes in both tabs. A guard, green on both commits.
  - Mutations: M2c and M3c re-anchored and still bite; MJ1 (landed before the reconcile), MJ2 (no landed-hash fallback), MJ3a (no editor reconcile), MJ3b (reconcile before the failure sentence), MJ3c (no mirror), MJ4 (the reconcile clears the undo stack) and MJ5 (the stale mirror clears the peer's undo stack) all bite.
  - Codex round 4 on f08a3656..9237f8dc: approve, no material findings.
  - Main 26d353ef (CB-30, #2007) merged in as 4e17c512.
- **The judge's review of that delta: Merge, no blocker or major.** Two items taken, red first (0069ec51), fixed in 5daeea33.
  - Contract 38's amendment gives the tabless review's order (failure sentence, repair, `DiskWriteLanded` reconcile, landed sentence, reload), records the reconcile after a landed failure that is not SBU on the editor, the reading view and a renamed tab, with the accepted NVDA risk, and names the new facts and the undo theory.
  - The tab's generic failure arm uses the landed arm's `completionReconcilesThisTab` (now computed once for both): a tab renamed or moved while a panel or review write ran reconciles itself after a landed non-SBU failure, since the completion reconciles the old path. Fact `APanelToggleThatFailsAfterItsWriteStillReconcilesATabRenamedBeforeItPublishes`, with the seam `TaskToggleBeforePublishForTests` (the worker between its read-back and its publication). A rename before the read-back leaves the read-back unreadable, so nothing knows the disk moved on any route; that stays the outcome-unknown case.
  - The nit on `landedAnyway`'s null `_contentHash` is left: no cheap fact reaches a toggle on a tab before its note loads.
  - Mutations MJ6 and MJ7 added; M11 re-anchored on the shared test.
  - Codex round 5 (local head): approve, no material findings.
- **The judge's third review (5daeea33): Merge, no blocker or major.** Two items taken, red first (fab77d5e), fixed in 79a3e12f.
  - Found on main: a successful panel or review toggle whose tab was renamed before it published took the tab's diverged branch ("the editor changed", no stale mark) while the completion reconciled the old path, so the renamed tab and its peers kept the old hash over the changed disk. The success arm now has its own path-moved branch that reconciles the tab over `report.NewContentHash` and mirrors to peers. Fact `APanelToggleThatSucceedsStillReconcilesATabRenamedBeforeItPublishes` (split peer, text and undo unchanged). Mutations MJ8 and MJ9.
  - Contract 38's wording: when each tabless step runs; the renamed-tab rule narrowed to the panel and the review through an open tab; the successful toggle's renamed tab; two accepted residuals (a tab that opens during a tabless write and is then renamed; a rename before the read-back leaves the outcome unknown, as on main).
  - Codex round 6 (local head): approve, no material findings.
- **The merge.** #2008 merged as 834cbe31 (squash, pinned to 79a3e12f) on 2026-11-18. The fourth judge said Merge. It named a third residual: tab a sends the toggle and is then closed or navigated away, the note is renamed before publication, and a split peer at the new path is missed by the old-path reconcile. That is pre-existing, changes only what the reader is told, and the lead adds it to contract 38 through the C6 docs PR.
