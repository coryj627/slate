# CB-0 — The found-on-the-way rule is in `AGENTS.md`, the 26 audited issues say where they stand, #1835 is closed, and plan 47's hand-offs are on GitHub

Issues: #1835 (closed by hand, with a comment); comments on the 26 audited issues and on #1653, #1733, #1842, #1848; three new issues (the ledger, the deferred finds, CD38's catalogue). Size S. Lanes: none (the docs PR touches no lane's path list, so each lane's `lane paths` job and `pr gate` run and every other job skips). AT: —. Wave: C0. Depends on: plan 47's documents being on `main`, and the owner's answers to plan §3. Parallel with: nothing (C0 has one slot). Decision: the `AGENTS.md` PR waits on no row. CD35, CD36, CD37 and CD38 gate the issue edits and the catalogue issue; CD40 gates #1835's close; CD8, CD13 and CD32 each gate one comment. All are answered before C0 (plan 47 §3). Interlock: CB-3 adds one line to `AGENTS.md` beside the parity-matrix gotcha, after this chunk's PR has merged.

## Goal

Every later chunk works under one written rule for a defect found on the way. Each issue another plan or the owner holds carries a dated comment saying what is done, what remains and who owns it. The one issue already fixed is closed. The two design follow-ups sit in Milestone W with their starting code kept. Plan 43's queue can see what plan 47 leaves it.

## Current state on main

Read at c2377210 and on GitHub on 2026-11-13.

- **The rule.** `AGENTS.md:116-118`: "One PR closes its issues and nothing else. A defect found on the way is filed as an issue unless it blocks the PR. A defect in the PR's own feature is fixed on its branch." The "PR body:" bullet is `:112-115`. No test or workflow reads `AGENTS.md`.
- **Repeats of the old rule.** `43_mac_backlog_delivery/00_program.md:148-149` (§4 rule 8); `44_windows_backlog_plan.md:307-308`; `45_windows_followups_plan.md:138`; `46_windows_design_system_plan.md:185` (the plan does not name this one). `00_program.md:317-320` and `90_reassessment_2026-10-19.md:196` record what the rule did and stay.
- **None is a frozen record.** Only `*_contracts.md` is append-only (`AGENTS.md:119-121`), and later PRs edited each plan in place (#1655, #1732, #1853). Plan 43 marks a later fact with a dated italic note (`00_program.md:404`, `:555`). This chunk does the same: it appends, and rewrites only a status sentence that is now false.
- **Stale text.** `45_windows_followups_plan.md:5-6` "Not adopted; nothing in this plan has started." (decided 2026-10-24, `:123`; fifteen of sixteen chunks merged, by the audit). `46_windows_design_system_plan.md:5` "Later waves wait for the owner." and `:147-148` "Plan 44's WB-29 and WB-30 are still open". `43_mac_backlog_delivery/22_descriptor_relative_session_design.md:6-7` says 22W is not delivered. `44_windows_backlog_plan.md:434` waits for a measurement that "confirms the remedy"; #1909's body says the remedy was dropped on the owner's call.
- **#1835.** Open, no comments. #1856 (3035525d) removed the toggle; `SidebarDualPaneToggle` is a menu row (`apps/slate-windows/src/SlateWindows/MainWindow.FilesToolbar.cs:89`).
- **#1842, #1848.** Open, no milestone, no comments. Milestone W is number 22, `Milestone W — Windows Version`.
- **9edf4b6b.** `origin` no longer has `claude/ds11-accent` (`git ls-remote origin`; a local `origin/claude/ds11-accent` is a stale tracking ref). The commit is an ancestor of 6740106c, the head of merged PR #1841, so `refs/pull/1841/head` reaches it. The repository has no tags, and no workflow or ruleset acts on one.
- **#1733 and the mac follow-ups** (#1651 #1652 #1653 #1654 #1662 #1679 #1720): open, no comments, and `00_program.md` names none. #1733's code is unchanged (`vault/fs.rs:314`).
- **The 26.** All open. No issue titled "Mac work owed by plan 47" exists.

## Reference

Plan 45's XB-0 (close by comment: name the fixing merge and the anchor). `reading/09_group_c_audit.md` (the drafted comments). `reading/04_focus_and_peers.md:387-429` (#1835). `AGENTS.md` Gotchas: keep close, fix and resolve away from issue numbers in the PR body.

## Design

One docs PR (items 1 to 3 and the row in item 7), then GitHub work (items 4 to 9). Item 8's issue is opened first, because the PR cites its number. Write each comment to a file and post it with `gh issue comment N --body-file <file>`. `<date>` is the day the step runs or the decision was answered.

1. **`AGENTS.md`.** Replace `:112-118` with:

   ```markdown
   - PR body: `Closes #N` for each issue it closes, the tests added, the contract
     register rows closed, VoiceOver phrases where a pass was required, each
     version it adds or moves with its release date, a line under "Found and
     fixed here" for each defect fixed on the way, `not delivered: <reason>`
     for anything not done or not verified, and a last line with the count:
     found on the way, fixed here, deferred, filed.
   - One PR closes its issues. A defect found on the way is sized by what its
     fix touches:
     - Small: the fix stays in files the PR already changes, plus their tests.
       Fix it in the PR.
     - Medium: the fix reaches three or fewer other non-test files in the same
       subsystem (one top-level folder of an app, or one module of a crate;
       the files of one partial class count as one subsystem wherever they
       sit). Fix it in the PR, or defer it to the plan's sweep chunk; outside
       a plan, file it.
     - High: the fix reaches more than three other non-test files, crosses
       subsystems, or needs a design, an owner decision, an FFI change or a
       machine the agent does not have. File it. If it blocks the PR, its fix
       merges first, as its own PR.

     A fix that needs an owner decision, an FFI change, a machine the agent
     does not have, or a new or changed spoken sentence that needs a full
     screen-reader pass is never small or medium.
   - Each fix taken in the PR has its failing test first and a line under
     "Found and fixed here" in the PR body. Stop taking them when they would
     more than double the non-test files the PR changes.
   - A defect in the PR's own feature is fixed on its branch, whatever its size.
   ```

   This is plan 47 §4 rule 1's text. Four things in it go beyond what the owner saw in the brief, and the owner reads them when adopting the plan: (a) High includes "reaches more than three other non-test files", because a four-file fix in one subsystem was in no class; (b) "a Mac" is "a machine the agent does not have", because `AGENTS.md` is also read on the Mac, where "needs a Mac" excludes nothing; (c) the blocking case the old rule covered is kept as one sentence; (d) a partial class's files count as one subsystem, because the Windows app's root folder holds 162 files in no subfolder, most of them the `MainWindow`, `WorkspaceViewModel`, `FilesSidebarViewModel` and `VaultLifecycle` partials. The count line is written on every PR, zeros included.

2. **Pointers.** One appended line each. The old sentences stay.
   - `43_mac_backlog_delivery/00_program.md`, after `:149`, indented as part of rule 8:
     > *`<date>`:* `AGENTS.md` ("Git and PRs") now sizes a defect found on the way by what its fix touches, and replaces this rule (plan 47 §4, CB-0).
   - `44_windows_backlog_plan.md`, a new paragraph at the end of §4, above `:366` (`## 5. The chunks`):
     > *`<date>`:* "one PR closes its issues and nothing else" above is replaced by `AGENTS.md`'s rule for a defect found on the way (plan 47 §4, CB-0).
   - `45_windows_followups_plan.md` and `46_windows_design_system_plan.md`: the same paragraph, above `:155` and above `:227`.

3. **Stale text.** Each is a status sentence that is now false; replace it in place.
   - `45_windows_followups_plan.md:5-6`: "Not adopted; nothing in this plan has started." becomes "Decided 2026-10-24 (§3). Fifteen of its sixteen chunks have merged; XB-6 (#1596) never ran and is plan 47's CB-43." Under CD32 "no", end it at "never ran."
   - `46_windows_design_system_plan.md:5`: "Later waves wait for the owner." becomes "All five waves have merged (#1821 to #1858); what is left is the owner's `/design-sync` (#1756)."
   - `46_windows_design_system_plan.md:147-148`: becomes "- **Plan 44's WB-29** merged as #1873 (b8ca1c20). **WB-30** has not started and doesn't touch shell chrome."
   - `22_descriptor_relative_session_design.md:6-7`: "PR 22b, the mac host, and PR 22W, the Windows body under Milestone W, are not delivered." becomes "PR 22W, the Windows body, is delivered as plan 44's WB-28 (#1729 fb131216, #1731 ed172e07). PR 22b, the mac host, is not delivered."
   - `44_windows_backlog_plan.md:434`: cells 2 and 3 become "WB-31 (the Windows bench; the remedy was dropped on the owner's call, #1909)" and "by hand after a macOS run of `benches/two_sessions_bench.rs` on `main` (the mac follow-up #1653), or the owner withdraws the macOS observation (WD21)". Rows `:374` and `:408` describe the chunk as planned and stay.

4. **#1835.** By CD40 this closes on its control's removal, with no fact (plan 47 §4 rule 7 names it). The witness goes in the PR-less close-out report: `git grep -n "SidebarDualPaneToggle" apps/slate-windows/src` shows the menu row at `MainWindow.FilesToolbar.cs:89` and no toggle button. Under CD40 "no", post the comment without closing. `gh issue close 1835 --reason completed --comment <text>`:
   > Already fixed on `main`, by removal. #1856 (3035525d) replaced the Dual pane toggle button with a checkable menu row, "Enable dual-pane files sidebar" (`MainWindow.FilesToolbar.cs:89`), and removed the Tags toggle beside it, so no checked button with a text child is left to colour. Plan 47's reading looked for the same shape in the rest of the shell: the two status-bar pane toggles and the templated chevron have glyph content, every radio button has string content or inherits its segment's colour, and nothing uses an accent-filled button style. No fact was added, because there is no such toggle to measure. Not checked by eye: a selected row's text in a contrast theme.

5. **The status comments.** "Draft at" is the line in `reading/09_group_c_audit.md`. LO is leave open; +C means the comment also corrects text on the issue. Before posting, run `gh issue view N --json state,comments`; if the issue has closed or a newer comment already says it, skip it and say so. Keep the stamp "`main` 1ec9bf31 (plan 47 audit, 2026-11-13)": that is when it was read.

   | Issue | Result | Draft at | What this spec changes |
   |---|---|---|---|
   | #1323 | LO+C | :37 | last sentence, by CD36 (5c) |
   | #1330 | LO | :47 | written in full below (CD13) |
   | #1389 | LO+C | :71 | last two sentences, by CD36 (5c) |
   | #1395 | LO | :82 | written in full below (CD8) |
   | #1493 | LO | :162 | CD37 fills "[who and when]"; item 9's reading replaces "was not re-read" |
   | #480 | LO | :284 | as #1493 |
   | #998 | LO+C | :311 | as #1493 |
   | #1513 | LO+C | :176 | under CD36 "yes", one added sentence (5c) |
   | #1560 | LO | :216 | the brackets go; the sentence stays and names CB-30 |
   | #1596 | LO+C | :258 | written in full below (CD32) |
   | #940 | LO+C | :299 | last sentence, by CD36 (5c) |
   | #1370 #1398 #1434 #1468 #1469 #1470 #1484 | LO | :57 :92 :104 :114 :124 :134 :149 | nothing |
   | #1564 #1565 #746 #747 | LO | :229 :246 :321 :331 | nothing |
   | #1525 #1526 #1756 #750 | LO+C | :193 :204 :273 :342 | nothing |

   **5a. The three plan 47 takes.** The owner answers plan 47 §3 before C0, so each row is answered when CB-0 starts and each comment is posted with the rest. Under "no", post the audit's draft with its bracket replaced by "Plan 47 does not take it (CDn, `<date>`)."

   #1330 (CD13 "yes"):
   > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): unchanged since the 2026-10-28 comment. Plan 44 WB-11 (#1688, 18c6815b) merged with `Refs` and left this open: the UIA side is correct and pinned by `ReadingSelectAll_RaisesTextSelectionChangedAndReturnsTheNote`, and Narrator still says nothing on Ctrl+A. No chunk of plans 44 to 46 owned what remains, so plan 47 takes it as chunk CB-21 (owner decision CD13, `<date>`; `docs/plans/47_windows_core_closeout_plan.md` §5). Its first step is the one WB-11's record names: whether Narrator needs TextPattern2's caret range or a different event.

   #1395 (CD8 "yes"):
   > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): not started; the text above and the 2026-10-16 narrowing are still accurate. `CreatePublishedUnindexed` with a non-empty reason (`a11y.rs:2249`) and `vault_error_detail(SavedButUnindexed)` (`a11y.rs:3608`) still splice the reason with no closing period. This was parked with plan 43 PR 39. Plan 47 now takes it in chunk CB-14, with #1660: the two sentences are rendered by separate functions (the create announcement and `vault_error_detail`), and both take the same punctuation helper, each with its own facts (owner decision CD8, `<date>`); PR 39 keeps #1394. The change is delivered and checked on Windows. The VoiceOver pass both sentences are owed is listed on #`<ledger>`, "Mac work owed by plan 47", which plan 43's queue clears.

   #1596 (CD32 "yes"):
   > Correction and status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): not delivered, and both cooldowns ended on 2026-10-29. There are now eleven `actions/setup-python` pins at v7.0.0 in six files, not nine in five: `windows.yml:688` and `:787` were added after this was filed. `ci/windows-runner/golden/versions.json` still names runner 2.338.0. Plan 45's XB-6 never ran, so plan 47 takes this into chunk CB-43 with #1614 and #1903, and one golden rebuild covers all three (owner decision CD32, `<date>`). That PR re-reads both release lists when it opens and takes the newest release past the cooldown.

   **5b. #1653** (not one of the 26): post the audit's draft at `:151` unchanged.

   **5c. CD36.** CD36 is answered before C0, like every row. Under "no", or for a part the owner declines, post the audit's draft unchanged.
   - #940, "yes": the last sentence becomes "The owner has ended this by hand (plan 47 CD36, `<date>`): the race it reports is shut on all three platforms (22a, 22W), and 22b's one remaining fact, `a_renamed_open_vault_keeps_working_and_can_show_its_new_path`, is carried by #1513." Then `gh issue close 940 --reason completed`. #1513's comment gains: "It also carries 22b's mac host fact, `a_renamed_open_vault_keeps_working_and_can_show_its_new_path`, since #940 was ended by hand (CD36)."
   - #1389, "yes": from "Two routes now exist" to the end becomes "The owner chose the route for it (plan 47 CD36, `<date>`): #1651 makes the modes real on the mac by consuming core's function, and plan 43 PR 52, which would have removed them, is retired. This issue stays open until #1651's PR merges, alongside #1526."
   - #1323, "yes": the last sentence becomes "This issue stays open for that fact alone and is retitled for it (plan 47 CD36, `<date>`)." Then `gh issue edit 1323 --title "mac tests: LeafContextMatrixTests.testSwitchPaneFocusRebindsLeaves is the one flake left from the W7-7 list"`. The owner's answer confirms the title.

6. **CD35: #1842 and #1848 go to Milestone W.** First keep 9edf4b6b. Recommended: an annotated tag on `origin`. It keeps the whole tree with its tests and survives a mirror or a move off GitHub, which a `refs/pull` ref does not; pasting `SlateAccent.cs` and `SlateAccentTests.cs` would put about 40 KB in a comment. It would be the repository's first tag. If the owner does not want tags, paste `TextPairs`, `MarkPairs`, `FailedChecks`, the overlay checks and the two facts into the #1842 comment instead.

   ```powershell
   git fetch origin refs/pull/1841/head
   git merge-base --is-ancestor 9edf4b6b8960cf536aef5c4257901a675f2ebea8 FETCH_HEAD   # exit 0
   git tag -a ds11-accent-contrast-gate 9edf4b6b8960cf536aef5c4257901a675f2ebea8 -m "The accent contrast gate as DS-11 first built it. Kept for #1842."
   git push origin refs/tags/ds11-accent-contrast-gate
   git ls-remote origin 'refs/tags/ds11-accent-contrast-gate^{}'   # 9edf4b6b8960…
   gh issue edit 1842 --milestone "Milestone W — Windows Version"
   gh issue edit 1848 --milestone "Milestone W — Windows Version"
   ```

   If the dash in the title does not survive the shell, use `gh api -X PATCH repos/coryj627/slate/issues/1842 -F milestone=22` (and `1848`).

   #1842:
   > Moved to Milestone W (plan 47 CD35, `<date>`): this is a feature that needs a design from the owner and the Claude Design project, so plan 47 does not build it. The branch named under "Where to start" is gone from `origin`. The commit is kept by the tag `ds11-accent-contrast-gate` (9edf4b6b8960cf536aef5c4257901a675f2ebea8); it is also an ancestor of PR #1841's head. Read the gate with `git show ds11-accent-contrast-gate:apps/slate-windows/src/SlateWindows/SlateAccent.cs`.

   #1848:
   > Moved to Milestone W (plan 47 CD35, `<date>`): DSD18 kept the compact bar and left its width rule as a later refinement, which is a design call, not a defect. Plan 47 does not change it.

7. **#1733.** Comment (after the docs PR merges):
   > Plan 47 audit, 2026-11-13: this belongs to plan 43's queue, and plan 47 does not take it. The reads it names are `cfg(unix)` code (`vault/fs.rs:314` still resolves by the opened path), so only a Mac or Linux run can show its regression test fail and then pass; plan 47 is delivered on Windows, where WB-28 (#1729, #1731) already reads beneath the held root. It is the same code as #1513 (plan 43's 22c), with `file_identity` and the hot-exit classification added to the test. Plan 43 §11.4 now names it beside 22c. That queue is paused, so nothing merges for this until the owner restarts it.

   The row goes at the end of the PR table in `00_program.md` §11.4, after `:470` (row 53). That table is the queue, and its rows already carry pointers to other plans.

   ```markdown
   | — | *<date>, plan 47 CB-0:* open issues no row above names. #1733 is the Unix half of 22c's reads and goes with 22c. The mac follow-ups plans 44 and 45 filed: #1651 (#1526 and #1389 wait on it), #1652, #1653 (#1484 waits on it), #1654, #1662, #1679, #1720. Plan 47's core changes that a Mac user hears are listed on #<ledger> | #1733 #1651 #1652 #1653 #1654 #1662 #1679 #1720 | — | — | not slotted; the owner places them when the queue restarts |
   ```

   Add to its Title cell, each only under its "yes": "PR 52 is retired and 22b's fact goes with #1513 (plan 47 CD36)."; "#1395 is plan 47's CB-14, and PR 39 keeps #1394 (CD8)."

8. **The ledger issue.** Open it before the docs PR: `gh issue create --title "Mac work owed by plan 47" --label swift-ui --label a11y --body-file <file>`. Write its number into the three places marked `<ledger>` and into plan 47 §8 (one clause, in the docs PR).
   > Plan 47 (`docs/plans/47_windows_core_closeout_plan.md`) is delivered on a Windows machine while plan 43's mac queue is paused. Some of its core changes alter what a Mac user hears or sees. Each merges on Windows evidence, with `not delivered: VoiceOver pass` in its PR body and one line here. This issue is the list of what the Mac is then owed.
   >
   > **Who clears it.** Plan 43's queue, on the owner's Mac. A line is cleared by a comment naming the VoiceOver pass, with its phrases, or the mac PR that adopted the change. This issue ends when every line is cleared.
   >
   > **How a line is added.** The plan 47 PR that makes the change adds one comment here before it merges: the chunk, the issue, the PR, what a Mac user hears or sees differently, and what is owed (a VoiceOver pass, a mac adoption, or both). Nothing else is discussed here.
   >
   > **Lines expected** (plan 47 §8, 2026-11-13; none is owed until its PR merges):
   > - #1628, #1629 (CB-30): math speech and braille for a scripted function and for three or more primes.
   > - #1660, #1395 (CB-14): the new toggle event's adoption on the mac; the create sentence.
   > - #1782 (CB-32): a link snippet starts and ends on a word.
   > - #1834 (CB-34): the palette's section order; the selection rule in `CommandPaletteModel.swift`.
   > - #1631 (CB-27): the session-long diagram sentence goes; the panic sentence is reworded.
   > - #1775 (CB-26): a callout's first line reads as its label.
   > - #1683 (CB-12): one `Deleted` event stops arriving.
   > - #1889 (CB-9b): the saved-query export's refusal.
   > - CB-20b: the diagram source event.
   > - #1830 (CB-16): the import summary and cancel sentences move to core on both hosts (CD10).
   > - Behaviour, with nothing new to hear: #1704 (CB-31) and #1705 (CB-32), links that used to stay unresolved now resolve; #1900 (CB-8), an unreadable note lists.
   > - #1708 (CB-20): a check of the Mac reading view for the same gap.
   > - A note on #1527: the Mac's two copies of the save sentence carry #1660's run-on, and Windows keeps four search and graph error arms worded apart from core (CD9).

   Drop a line whose decision row was answered the other way. The rule for later chunks is plan 47 §4 rule 5: the comment is added in the same PR as the change, and the PR body cites this issue's number.

   **The deferred-finds issue** (plan 47 §4 rule 10). Open it with the ledger: `gh issue create --title "Plan 47 deferred finds" --label windows --body-file <file>`. Its body says what it is (the one list of defects a plan 47 PR found and did not fix, each as one comment: the file, what is wrong, the failing test that would show it, and the PR that found it), who clears it (CB-46), and the lines it starts with, from plan 47 §9's table: the canvas and graph trigger ledgers have no `--check` and `test_canvas_trigger_ledger.py` runs in no workflow; the device stores do not retry a refused rename; whether blame's mini dumps lack managed frames. Write its number into plan 47 §8 in the docs PR.

   **CD38's catalogue issue**, only under CD38 "as recommended": `gh issue create --title "core: the sentences the Windows host still composes" --label windows --label a11y --milestone "Milestone W — Windows Version" --body-file <file>`, with the table from `reading/05_spoken_failures.md` ("Sentences the host composes that the rule says core should own"), and one line that each move is an FFI addition with Mac adoption.

9. **CD37: the release lists.** The agent that opens a wave reads four lists before its first PR. CB-0 does the first reading.

   | Issue | Read | It has landed when |
   |---|---|---|
   | #1493 | crates.io, `notify` versions | 9.0.0 is listed; the bump waits until it is 7 days old |
   | #480 | crates.io, `citationberg` and `hayagriva` versions | a `citationberg` release carries the quick-xml fix of commit 06a591e2, and a `hayagriva` release depends on it |
   | #998 | `gh api repos/NordSecurity/uniffi-bindgen-cs/tags --jq '.[].name'`, and the state of its #176 | a tag names `v0.32` |
   | #1628 | crates.io, `mathcat` versions | a release newer than 0.7.7 exists; whether its Nemeth cells are corrected is then one golden run |

   It comments on the issue itself, and only when the list has moved since the last such comment: "Read `<date>` (plan 47 wave Cn): newest is `<version>`, released `<date>`." With no change, the wave's first PR body carries one line, "release lists read `<date>`: no change". This first reading goes into the three status comments of item 5 in place of "was not re-read", and "[who and when]" becomes "the agent that opens each wave of plan 47, at the wave's start (CD37)"; #1628 gets a short comment of its own. A release that has landed is reported to the owner, not acted on. If the owner names another reader, that name replaces the phrase.

10. **Deliberately not done.**
    - No product code, test or workflow change, and no version bump for anything on the watch list.
    - None of the 26 is closed, except #940 under CD36 "yes". No title, body or label is edited except #1323's title under CD36 "yes". #940 gets no milestone.
    - The owner's steps stay the owner's: the ruleset `PUT` (#1565), `/design-sync` (#1756), the four Studio steps (#1564). `AGENTS.md`'s two #1565 caveats in the merge steps stay until the ruleset requires the lanes.
    - The old rule sentences in plans 43 to 46 are not deleted, and the two records of what the old rule did are not touched.
    - No issue is filed for anything the sweeps found, apart from the three in item 8; each find has an owner in plan 47 §9. #748 and #749 were not audited and get no comment.
    - CB-3's `AGENTS.md` line, WB-32's spike and any reordering of plan 43's queue are not here.

## Tests

None. Nothing here has behaviour to pin. After the edit, `git grep -n "and nothing else" -- AGENTS.md` prints nothing.

## Docs and registers

- `AGENTS.md`; `docs/plans/43_mac_backlog_delivery/00_program.md` (two additions); `22_descriptor_relative_session_design.md`; `44_windows_backlog_plan.md`; `45_windows_followups_plan.md`; `46_windows_design_system_plan.md`; `47_windows_core_closeout_plan.md` §8 (the ledger's number).
- No contract register row changes. No parity matrix change.

## Verification

```powershell
git diff --stat main...HEAD                       # the seven files above and no other
git grep -n "and nothing else" -- AGENTS.md       # no output
gh pr view <N> --json closingIssuesReferences     # []
gh pr checks <N>                                  # pr gate passes; each lane's "lane paths" passes; every other job skips
gh issue view 1835 --json state                   # CLOSED
gh issue view 1842 --json milestone ; gh issue view 1848 --json milestone   # Milestone W — Windows Version
git ls-remote origin 'refs/tags/ds11-accent-contrast-gate^{}'
gh issue list --search '"Mac work owed by plan 47" in:title' --json number
```

CI lanes: none does its work. `windows.yml` names the plan documents its censuses read (`:151-164`), and none of the seven files is among them; no other lane's path list matches a file here. The push to `main` matches no lane's paths either, so `gh run list --commit <merge-sha>` may be empty. AT: none.

## Acceptance

- [ ] `AGENTS.md` carries item 1's text; plans 43 to 46 each carry their pointer line.
- [ ] The five stale sentences are corrected.
- [ ] #1835 is closed with item 4's comment; under CD40 "no", it has the comment and stays open.
- [ ] Each of the 26 has its comment, or is named under `not delivered` with the decision row it waits for. #1653 has its correction.
- [ ] The tag resolves to 9edf4b6b on `origin`, or, if the owner does not want tags, #1842 carries the pasted code item 6 names; #1842 and #1848 are in Milestone W, each with its comment.
- [ ] #1733 has its comment, and plan 43 §11.4 has the row.
- [ ] The ledger issue exists; plan 47 §8 and plan 43's row cite its number.
- [ ] The deferred-finds issue exists with its three starting lines, and plan 47 §8 cites its number. Under CD38 as recommended, the catalogue issue exists in Milestone W.
- [ ] The four release lists were read, and the result is on #1493, #480, #998 and #1628.
- [ ] The docs PR's `closingIssuesReferences` is empty, and its body ends with the count.

## Brief

Work on `claude/cb0-rule-audit-close-outs` from `main`, once the owner has answered plan 47 §3. (1) Open the ledger (`gh issue create --title "Mac work owed by plan 47" --label swift-ui --label a11y`) and the deferred-finds issue (`--title "Plan 47 deferred finds" --label windows`), each with item 8's body, and under CD38 as recommended the catalogue issue in Milestone W; keep the numbers. (2) One docs PR, titled `docs: agents.md sizes a defect found on the way by what its fix touches, and plans 43 to 46 point to it`: replace `AGENTS.md:112-118` with item 1's block; append item 2's dated line to `43_mac_backlog_delivery/00_program.md` after `:149` and as the last paragraph of §4 in plans 44, 45 and 46 (above `:366`, `:155`, `:227`); make item 3's five replacements (`45…plan.md:5-6`, `46…plan.md:5` and `:147-148`, `22_descriptor_relative_session_design.md:6-7`, `44…plan.md:434`); add item 7's row after `00_program.md:470`; write the ledger's and the deferred-finds issue's numbers into plan 47 §8. Re-read each line number first. The body names no issue after a closing word, lists the files, and ends "found on the way: 0, fixed here: 0, deferred: 0, filed: 0". Check `gh pr view N --json closingIssuesReferences` is `[]`; merge when `pr gate` passes and the lanes skip. (3) Close #1835 with item 4's comment (CD40); under CD40 "no", post the comment and leave it open. (4) Read the four release lists (item 9). (5) Post the 26 comments from `reading/09_group_c_audit.md` at the lines in item 5's table, with the changes the table names: the three full texts in 5a as CD13, CD8 and CD32 were answered, CD36's endings from 5c, CD37's reader and the reading's result in #1493, #480 and #998, and #1560 without its brackets. Post #1653's correction (`:151`). Check each issue's state and last comment before posting. Under CD36 "yes", close #940 and retitle #1323. (6) Tag 9edf4b6b and push the tag (or, if the owner does not want tags, paste the code into #1842 as item 6 gives it), move #1842 and #1848 to Milestone W, and post their two comments (item 6). (7) Post #1733's comment. Do nothing in item 10. Report each step as the command and its result.
