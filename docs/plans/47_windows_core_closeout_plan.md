# 47 — Windows and core close-out: delivery plan

Status: adopted by the owner's delivery instruction of 2026-11-15.
Drafted on 2026-11-13; corrected on 2026-11-14 and 2026-11-15 for twelve
merges that landed around it (§2.6). The owner answered §3 on
2026-11-16; the answers are in §3.1, and nine of them depart from the
recommendation. Waves C0 to C13 are delivered (CB-0 to CB-17b, CB-19
to CB-25 apart from CB-20b, CB-27 to CB-36 apart from CB-35b, CB-42 and
CB-45, merged from 2026-11-16 to 26, with the owner-directed
invalid-search change in wave C8). The waves overlapped: wave C10 ended
on 2026-11-24 with CB-22b, wave C12 the same day with CB-34, wave C11 on
2026-11-25 with CB-16, and wave C13 on 2026-11-26 with CB-35. Three
chunks were added in them: CB-45c and CB-45d, the two phases CB-45's
design cuts after CB-45b, and CB-35b (#2070), cut from CB-35 (§5, §6).
Wave C14 started on 2026-11-26: Principal Q has CB-20b and then CB-38,
and Principal O has CB-39. §12 keeps the delivery record.
Written from the requirements brief the owner approved on 2026-11-13
(`docs/superpowers/specs/2026-11-13-plan-47-windows-core-closeout-design.md`).
Baseline: `main` at c2377210 (#1927). The reading was done at 1ec9bf31
(#1926); the one commit between them adds `ExitWatch` and its tests and
edits `AssemblyInfo.cs`, all in the Windows test project.

Reviewed by a fresh Claude red team (1 high, 17 medium, 20 low) and by
Codex in rounds, at the owner's request. Round one read the plan alone
(1 high, 10 medium, 2 low, "do not adopt"). Round two read the revised
plan with the eight first-wave specs: it confirmed all thirteen fixed
and found 1 medium and 3 low in the plan and 6 medium and 1 low in the
specs ("do not adopt", on the specs). Round three confirmed eight of
those eleven fixed and three partly fixed, each a sentence left behind
elsewhere in the same spec. Round four confirmed those three and ended
"approve with the fixes above": three places where a spec's path for a
decision answered the other way disagreed with its acceptance list.
Every finding of both reviewers was checked and applied. The first-wave specs (CB-0 to CB-7) are in
`47_windows_core_closeout/specs/`; later specs are written when their
wave starts (§10).

Every issue below was read in full, body and comments, and its cited code
checked against the tree. Nothing was built or run for the reading, so a
statement about what a screen reader says is either quoted from a
recorded pass or marked unverified in the reading notes, and "by reading"
below means the code says so and nobody has seen it happen.

## 1. Scope

**In:** 61 issues that were open on 2026-11-13, in six kinds. Two have
since left: #1826 is closed and #1596 is back with plan 45 (§2.6), so 59
remain, all still open on 2026-11-15.

| Kind | Issues |
|---|---|
| Windows tests | #1624 #1625 #1673 #1788 #1826 #1843 |
| CI workflows and runner images | #1647 #1699 #1903 #1614 #1616 #1596 |
| Windows host (`apps/slate-windows`) | #1610 #1635 #1661 #1670 #1671 #1683 #1694 #1695 #1708 #1714 #1721 #1722 #1724 #1730 #1783 #1791 #1800 #1830 #1836 #1837 #1838 #1846 #1849 #1860 #1866 #1889 #1890 #1330 |
| Core (`crates/`), host-neutral or Windows-cfg | #1628 #1629 #1631 #1660 #1395 #1704 #1705 #1727 #1775 #1782 #1834 #1900 #1917 |
| Decisions, not defects (§3) | #1665 #1668 #1669 #1718 #1842 #1848 |
| Close-outs and comments | #1835 #1709 |

Three of these were not in the brief's count of 58 and are in by a
decision row: #1330 (CD13), #1395 (CD8) and #1596 (CD32, since
overtaken). §2.4 says why.

Two more of the six decisions became work in this plan by the owner's
answers of 2026-11-16 (§3.1): #1665 is built here (CD27), and #1718 is
designed in CB-45 and built here (CD34). How each is cut is the plan
lead's call, 2026-11-16, not the owner's answer: the lead cut #1665 as
CB-38b, a chunk of its own, and cut #1718's build into phases, with
CB-45b the first implementation phase and the later ones cut by CB-45's
design.

**Left to another owner:** #1733 goes to plan 43's queue, because its
code is `cfg(unix)` and only a Mac can check its behaviour. §9 lists the
other open issues that plans 43 to 46 or the owner already hold.

**Out:** Mac-only work (plan 43's queue, and #1597, #1650, #1845); the
feature request #1620; feature milestones E, V, X, XD, PD, EX, PS and CV;
Milestone W's Wave 8 (#751 to #756) and its human-AT residuals (#746 to
#750). The brief called #748 and #749 Wave 8 epics, which was wrong: they
are W7-2 and W7-3, delivered and open for their human rows.

**The list is frozen at 2026-11-13.** The last wave (CB-46) re-counts
every Windows or core defect filed after that date, fixes the small and
medium ones, and puts the rest to the owner. The plan is done when that
table is answered.

## 2. What the reading found

### 2.1 Already fixed on `main`

- **#1835** (the checked Dual pane button's text colour). #1856
  (3035525d) removed the button two days after the issue was filed. By
  reading, no other toggled button has the defect. CB-0 closes it with a
  comment.
- **#1788** (a graph force change misses its save window), most likely.
  The gate's own file poll held `graph.json` open without delete sharing,
  so the app's save could not rename over it; #1870 (df67ee39) made the
  write wait out a brief hold, as a fix for #1865. By reading, the
  issue's own theory (a starved timer) does not fit the code. The failing
  run cannot be replayed. CB-7 adds a pin and closes it by CD3; if the
  pin fails, the issue's theory is real and the fix is a product change.

### 2.2 Corrections to the issues

- **#1783 and #1836 are one defect**, filed twice: every combo box's open
  rows report no place in their set. The control #1783 was seen on is
  gone (#1856). There are eleven combo box sites in three row-style
  families. One PR closes both (CB-19).
- **#1624 was not a hang.** The Routes model is one fact, and the blame
  collector's 45-minute timer ended it while it was still advancing on a
  loaded host. The rerun then overwrote the dumps. What stands is
  smaller, and CB-2 delivers it: the model has no per-case watchdog and a
  failed attempt does not keep its evidence.
- **#1826 was fixed while this plan was being written.** #1928
  (662be6b1) took each fact thread's WPF exit listeners off the
  process's exit and closed the issue on 2026-11-13. The cause was as the
  reading found: a slow exit from leaked listeners, not a crash. CB-1
  keeps #1843 only (§2.6).
- **#1843's "done when" cannot be met.** The fact was rewritten the next
  day (#1853), the dump expired after seven days, and the host was not
  idle: its log shows a slot's cache commit and a VM start in the stalled
  minute (CB-1, CD2).
- **#1625 is not a flake.** It is a fixed gap in what the Connections
  model covers. It is a decision (CD30), then a small or a large chunk.
- **#1846 is probably not a defect.** The code moves the keys once, into
  the editor. NVDA names the tab control and the tab because they are the
  editor's ancestors, as it does on every other entry into the editor.
  One NVDA run confirms it (CB-24, CD16).
- **#1671 is not the class #1867 fixed.** No trigger disables the row,
  and the landing already runs in the same turn. Its cause is unknown and
  CB-18 starts with a diagnosis.
- **#1775 is a core gap, not a Windows rendering defect.** Core's reading
  model has no callout, so by reading the Mac shows the `[!note]` marker
  too (CB-26).
- **#1635 is partly fixed.** #1908 moved the decode off the dispatcher.
  What remains is that a timed-out decode is not remembered and a
  projection has no decode deadline; both now delay the refresh worker.
  The remedy reverses a fact #1908 pinned on 2026-11-11 (CD19).
- **#1610 is a hardening, not a defect,** and its four measured documents
  are not in the repository. Its limits need a calibration run (CD20).
- **#1694 is a recorded accepted risk** (contract 28 PR-6), not an
  unknown defect. A full guard means a Slate peer for nearly every
  control in the shell (about 330 XAML sites). It is a decision (CD26).
- **#1721 is a latent risk, not a failure,** and the issue is wrong that
  no axe scan follows a zoom-in: one does. No scan has run in the state
  where an edge's bounds cover the board (CD24).
- **#1665's inventory is stale:** eight admission tables and about 35
  read sites, not seven and a dozen.
- **#1683's emitter is core.** `reindex_path_locked` announces `Deleted`
  even when it removed no index row (CD6).
- **#1900 is explained, so it needs no diagnosis step.** The quarantine
  writes `size_bytes = -1` and the listing decoder rejects it. It is
  reachable in ordinary use (CB-8).
- **#1889's reload guard** is at `WorkspaceViewModel.Rescan.cs:788-789`,
  not `:536`.
- **#1890 is misdescribed both ways.** Two files it names need nothing;
  about twenty sites in files it does not name are missed (§2.3).
- **#1660's run-on sentence is half of #1395.** #1660's is in
  `vault_error_detail` (`a11y.rs:3613-3615`); #1395's other half is the
  `CreatePublishedUnindexed` arm of the announcement renderer
  (`a11y.rs:2243-2252`). One helper and one test serve both (CD8).
- **#1628 is caused upstream.** MathCAT 0.7.7's Nemeth table has a
  literal full stop where a third dot-3 cell belongs for the triple and
  quadruple prime. Slate keeps the primes loose to keep the braille
  right, which is why speech says "times" (CD21).
- **#1629 is wider than its examples:** by reading, `n\log_2 n`,
  `2\max_i a_i`, `x_\max` and every function with a script after an
  ordinary atom fall back to source too.
- **#1834 has a second cause,** by reading. Both hosts keep a surviving
  selection when the query changes, so reordering in core alone does not
  fix it.
- **#1917 is misdescribed twice.** Three of the four temporal probes it
  names are not cached at all. And its cause is not a new binding. By
  CB-33's measurement on SQLite 3.54.1, a statement taken from the
  statement cache was re-prepared on every use, whatever was bound: the
  cache clears a statement's bindings when it goes back, and 3.54
  expires a statement whose plan looked at a bound value. The class is
  38 of 276 product SQL literals, sixteen of which name a path
  (corrected on 2026-11-21; §12, Waves C9 and C10).
- **#1903's tests are `Golden.Tests.ps1`,** not `Install.Tests.ps1`. The
  runner mode defaults to `namespace`, so its "done when" needs a
  `home-only` run on an image rebuilt after CB-43.
- **#1616 has four more stale-comment cases** than it lists.
- **#1842's starting code is on no branch.** `origin/claude/ds11-accent`
  is gone; 9edf4b6b is reachable only through `refs/pull/1841/head`. CB-0
  tags it before the move.

### 2.3 What the sweeps found

The brief's six classes held up, with different sizes than expected.

| Sweep | Named | Found |
|---|---|---|
| Routes that write a note or reload its tab outside the save chain | #1889's three | Five more unjoined routes (the tabless Bases cell write, the tabless Tasks Review toggle, the sidebar's tag edit, link rewrites by a rename or move, a Bases export over a vault file), and an exclusive-create family of eleven call sites. One route has no compare-and-swap at all: exporting a saved query "as a base" writes over whatever path is picked, so choosing an existing note replaces it (`session.rs:19620`; whether the system's replace prompt shows first is unverified). |
| A failure sentence built from an exception's raw message | #1890 | 68 sites: 50 certain, 17 through broad catches, one on a non-vault error. Every uniffi error type dumps its fields, not only `VaultException`. One test pins the dump (`SyncDiagnosticsPanelTests.cs:239`). |
| A control disabled while it holds the keys | #1866 | Fourteen sheet triggers on the menu, and the launcher's rows; neither had been heard under NVDA when this was written. Four sites were fixed by #1867. Six further groups of buttons that go unavailable when the reader activates them are instances by mechanism (CD11). One of them has been heard: #1866's comment of 2026-11-17 heard Bulk Rename's Apply go unavailable, with the keys leaving the sheet. The class is 47 dated rows in CB-17a's census, the fifteen sheet-trigger rows and 32 more, not the reading's "about twenty buttons". Until CB-17a the focus manifest's census did not scrape `Command=`, so 249 bindings could disable through `CanExecute` unseen. |
| An open dropdown's rows with no place in their set | #1783 #1836 | Eleven combo box sites, one defect. |
| An embedded reading element spoken as "grouping" | #1708 | One live member, the diagram, in three states. Math is fixed. A thematic break and an embed image each sit alone on a line with no name; what NVDA says there needs one pass. Tab onto a diagram still reads its whole source as HelpText, which #1591 removed for math; nobody had filed it (CD12). |
| A test that waits on the clock, or a fixture that tears down under in-flight work | six issues | Only #1673 is the class as worded. The unit project has 81 fixed waits that stand as statements of their own (28 sleeps, 13 delays, 40 fixed pumps) and the gate has 30; 20 elapsed-time oracles (19 in the unit project, one in the gate); nine fixtures that release a gate or dispose an event under work still running; and ten gate polls that hold a file the app replaces by rename (the mirror of #1788). Most of the unit project's fixed waits prove that nothing happened, and replacing one needs a product seam, so CB-6 fixes the few it can and pins the rest with reasons. |

Corrected on 2026-11-16 by CB-9a's spec, read at 80925d72: the first
row's eleven are routes, not call sites. A census that binds calls sees
six creator call sites, and one helper (`CreateOutcomes.cs:42`) stands
behind eight of the eleven routes. Two of the six are handled apart
from the class: a tab's own save when it holds no hash is already in
the chain, and a deleted file's recovery is CB-9a's to admit. The
export that row cites is at `session.rs:19886-19895` on that commit.

Corrected on 2026-11-16 by CB-6's census (#1971): the last row's gate
count is 31 fixed waits, not 30. The thirty-first is a sleep in
`FocusSettleWatchTests.cs` (§12).

Corrected on 2026-11-21 by CB-17a's spec and delivery (#2037): the
third row said the six further groups "have never been heard either"
and counted them as "about twenty buttons". #1866's comment of
2026-11-17, made during CB-9b's spot check, heard Bulk Rename's Apply
go unavailable when pressed, with the keys leaving the open sheet. And
CB-17a's census dates 47 rows of the class for CB-17b: the menu's
fourteen sheet triggers, the launcher's one, and 32 more. The row now
says both (§12, Waves C9 and C10).

The reading also found the palette seal's route table has two rows in no
register (drag and drop, and UIA `SetFocus`), and five more search
fields with the shape #1837 names. §9 lists every other find that no
issue names, each with its owner.

One thread runs through the test issues that is not in the brief: **the
evidence did not live long enough to be read.** #1624's dumps were
overwritten, #1843's expired, #1788's app log was never uploaded. §4
rule 6 answers it.

### 2.4 The audit of issues other plans own

The brief expected the audit to close some. **None can close on today's
evidence.** CB-0 posts a status comment on each of the 24 still open
(#998 and #1525 have since closed); eight of those also correct text on
the issue that is no longer true. Three are one
owner action from closing (#1565, #1756, #1564) and three could end on
an owner decision (CD36). §9 has the table.

Two had no owner at all, and one fits this plan better than where it sat:

- **#1330** (Narrator says nothing on Ctrl+A in the reading view). Plan
  44's WB-11 merged with `Refs` and left it open; no chunk owns the rest.
  It is Windows-deliverable. CD13 takes it as CB-21.
- **#1596** (the setup-python and runner pins). When this was written,
  plan 45's XB-6 had never run. It has since started: #1938 delivered its
  runner half, and its PR says the setup-python half will end the issue.
  #1596 is back with plan 45 (§2.6).
- **#1395** (the unindexed caveats' punctuation). Plan 44 parked it with
  plan 43's PR 39. CB-14 must edit the same helper and the same tests for
  #1660. CD8 folds it in.

Plans 44 to 46 have one chunk not yet delivered, WB-30 (blocked on plan
43's PR 34), and plan 45's XB-6 is half delivered. WB-32 merged as #1937
on 2026-11-14. Plan 46 is fully merged. Plan 43 restarted on 2026-11-13
with PR 29 (#1935, #1936, then #1942, which ended #1525); eleven of the
audited issues wait on it.

### 2.5 Where this plan meets other work

- **Plan 43's queue has restarted** (#1935, #1936), and no Mac is
  available to this plan. Every core change here that a Mac user hears,
  sees or gets different behaviour from is closed on Windows evidence and
  listed on one issue, "Mac work owed by plan 47" (§8). Plan 43 clears it.
- **WB-30** will rebase over CB-13 (`vault/`) and takes the next
  `a11y.rs` corpus slot after this plan's.
- **Stale status lines** in plans 45 and 46, the 22 design and plan 44's
  #1484 row are corrected in CB-0's docs PR.

### 2.6 Merged since this plan was written

Read on 2026-11-14 at f0bde81c (#1938). Codex reviewed these
corrections once: two findings, both applied.

| Merge | What it did | What changes here |
|---|---|---|
| #1928 (662be6b1) | Took each fact thread's WPF exit listeners off the process's exit, through WPF's own `StopListening`, and closed #1826 | CB-1 keeps #1843 only and is S. CD1 is overtaken. Its spec is rewritten. |
| #1937 | uniffi 0.32.2 and the C# generator's matching tag; plan 44's WB-32; closed #998 | #998 leaves the watch list and the audit's comments, which are now 25. |
| #1938 | The golden image's runner pin moves to 2.338.2, as plan 45's XB-6b, with `Refs #1596` | Plan 45 is delivering XB-6. #1596 leaves this plan; CD32 keeps only its question about the evergreen installers. |
| #1930 | A CI job, `rust-weak-volumes`, runs the weak-volume facts on FAT32, exFAT and letterless VHDs on every lane run; it skips the case-only rename fact by name until #1727 is fixed | CB-13 needs no elevated session from the owner. It un-skips that fact, so it edits `windows.yml` and `Workflows.Tests.ps1`. The two facts WB-28 left are delivered. |
| #1935, #1936 | Plan 43's PR 29 spec, and its core half (an FFI addition) | Plan 43's queue has restarted. CB-14, CB-16 and CB-20b re-read `a11y.rs` and the variant count when they start. |

Read again on 2026-11-15 at 798cdaae (#1946).

| Merge | What it did | What changes here |
|---|---|---|
| #1941, #1944 | Found and fixed #1939: when the keys open the app menu and the pointer rests where it appears, WPF hovers the row under the pointer and that row takes the keys. The fix is in `MainWindow.AppMenu.cs`, for the app menu only | This is very likely #1849's cause too: the reading's first candidate was a parked pointer. CB-24 still records one NVDA run, then applies #1944's remedy to the note header's menu and checks the other code-built context menus (the Files toolbar's, the rail's More menu) as finds on the way. It is no longer diagnosis-first. |
| #1942 | The Mac half of hot exit; ended #1525 | #1525 leaves §9 and the audit's comments, which are now 24. |
| #1946 | A PR that touches only Mac sources or the documents the censuses read runs the Windows lane's census tier; `AGENTS.md` gained two paragraphs; the parity-matrix check moved from the model job to the docs census job; a guard requires `[Trait("tier", "census")]` on any test class that reads a census-only path | CB-0's `AGENTS.md` line numbers moved. CB-3 pins the parity-matrix check where it now lives, and its ledger check must run in both tiers, because a Mac-only PR is the one that moves ledger rows. A census this plan adds carries the trait if it reads a Mac source or a census-list document. |
| #1950 | The unit tests tolerate a 20-second SVG decode through a new process-wide seam in `EditorInteractions.cs`; ended #1949 | CB-28 builds on that seam. Lines in `ReadingSvgEmbedDecodeTests`, `ReadingDiagramTests` and `W2EditorInteractionRedTeamTests` moved for CB-4, CB-6 and CB-28. |
| #1951 | The gate's theme fact reads the window's own pixels at its first rendered frame; ended #1943 | CB-7's pixel sites in the theme journeys can use the same reader; `ShellAccessibilityTests.Settings.cs` moved. |

#1943, #1945 and #1949 were filed and ended inside those two days, so
CB-46's re-count has nothing to do for them. #1932 and #1933 are Mac
defects and plan 43's.
Also since the baseline:

- **The home pool ran again.** A golden-image acceptance run on
  2026-11-14 used an image rebuilt with #1937 and #1938. The default mode
  is still `namespace`. That rebuild did not carry #1903 or #1614, so
  CB-43 needs another.
- **#1939, filed 2026-11-14,** reported the app menu opening with the keys
  on its last row on home-pool VMs. #1944 fixed and ended it on
  2026-11-15 (second table below).
- **#1934** (Windows consumes core's hot-exit restore plan), filed
  2026-11-13 from plan 43's PR 29 work, is labelled an enhancement but
  also names a Windows defect it would end: the host groups `A.md` and
  `a.md` as one file and can give a displaced tab the group's latest
  text. CB-46's re-count takes it and records what was decided for it.
- `windows.yml`, `Workflows.Tests.ps1`, `AGENTS.md`, `StaThread.cs` and
  its tests, `session.rs`, `a11y.rs`, the SVG decode tests and plans 43
  and 44 all changed, so the line numbers in the first-wave specs were
  re-read on 2026-11-14 and again on 2026-11-15.

**Read a third time on 2026-11-15 at 5b060297 (#1940), as delivery
starts.** Nothing has merged since #1940, which changed only this plan's
documents, and no pull request is open. All 59 in-scope issues are still
open, none with a comment since 2026-11-13, and no Windows or core issue
has been filed since #1949. Five agents re-read the specs for CB-0 to
CB-6 and the reading for CB-8 against the tree; nothing in them has been
delivered elsewhere. What moved:

| Chunk | What the check found | What changes here |
|---|---|---|
| CB-0 | Every line it edits still holds. The #1395 comment's `a11y.rs:3608` is `:3613` since #1936. Two audit drafts (#1370, #940) said no plan 43 PR after 22a had merged, which #1936 and #1942 made false. The new plan 43 row said the queue had not restarted. crates.io now lists `notify` 9.0.0-rc.9 (2026-11-15) and `mathcat` 0.7.8-alpha.1. The deferred-finds issue starts with four lines, not three | The spec and the two drafts are corrected. #1493's comment names the newest listed (rc.9 on 2026-11-15) |
| CB-1, CB-2 | Every cited line holds. `TestHostCrashDumpCensus`'s failure message names the `windows` job in words, so CB-2's theory passes the job name in. A highest-priority heartbeat cannot tell a starved fact thread from a healthy one | CB-1's PR says so in its "not delivered" line; a second beat at normal priority is a find for the red team to size |
| CB-3 | Design 1 cited `windows.yml:981`; the line is `:1024`. Design 6's chain scan would read `check_windows_census_trait.py`'s two chains (a docstring example at `:23` and a join at `:287`) as the folder `docs/plans` and turn the census red | The spec is corrected: a chain with a non-literal segment is skipped whole, with a fact |
| CB-4 | All nine fixtures hold. `ParkedFetch` has eighteen users, not eight. The teardown drain's default 10-second bound is shorter than the 20-second decode #1950 allows, so the reading fixtures' drains need the longer bound | The spec is corrected; `PumpedDispatcher.cs` may join the chunk's files |
| CB-5 | Every cited line holds. The existing Pester fact's consecutive-lines pin breaks under the new skipped-analysis branch and is rewritten, and new pin 2 is scoped to the ten `noScore(` calls | The spec is corrected |
| CB-6 | The 81 sites reproduce. There is a nineteenth unit ceiling the spec missed, `SingleInstanceCoordinatorTests.cs:66` | The spec is corrected; the ceiling goes |
| CB-8 | Every `session.rs` line in the reading moved by 248 to 254 with #1936. Three migrations (026, 034, 038) write the same `size_bytes = -1` on every row, so a cache upgraded across one, holding a file the scan cannot read, fails its folder's listing with no quarantine involved. `get_file_metadata` wraps the -1 to 2^64-1 and the canvas pass purges a quarantined board's rows. Only the quarantine also writes `mtime_ms = -1` (`session.rs:8166`, `:8920`, `:8980`); a row a migration left keeps its real modified time. Once a quarantined row lists, Windows never shows that time as a date: it reads `MtimeMs` only to put the row in a date group, where it lands in the oldest, and to sort it (`FilesSidebarViewModel.cs:1380`, `:1417-1418`). The Mac shows and speaks it: `SidebarFileRow.swift` puts "Modified" and the date in the row's accessibility value, so VoiceOver would say a 1969 or 1970 date. A base with a `file.mtime` column shows that date on both hosts | CB-8's spec is written from this at C2's start: the decoder absorbs the sentinel, and the two other readers stop wrapping it. The date is CD41. It changes what the Mac's row says, so §4 rule 5 puts `not delivered: VoiceOver pass` on CB-8's PR, with a ledger line (§8). Its Windows twin fact goes in `RescanTests.ChangeFeed.cs`, because CB-6 edits `RescanTests.cs` |

The lanes, as read the same day: `WINDOWS_RUNNER_MODE` is `namespace`,
and the home pool reports serving with two slots and nothing running.
The one ruleset requires `pr gate` and an up-to-date branch; no lane is
required. `main` is green at 798cdaae, the newest commit that ran a
lane. The shell gate's three red runs on `main` of 2026-11-14 and
2026-11-15 were the app-menu and theme facts that #1944 and #1951 then
fixed.

From 2026-11-15, by the owner's instruction, every plan 47 PR is
reviewed and merged by the rule below. It replaces the rule 9 this plan
was written with, and §4 rule 9 and the first-wave specs now carry it.

- A light PR changes only documents or comments, or makes a small
  configuration change with no product code and no test logic. It gets
  one red team: a fresh Claude agent with `24_red_team_protocol.md`, the
  diff and the spec. Nothing more.
- Every other PR gets that red team, then Codex adversarial-review
  rounds until it says approve. At 14 rounds without approval an
  independent agent judges the churn. The PR body records both.
- After the push, Codoki must report "Safe to merge" at confidence 3/5
  or higher, or be bypassed on an independent agent's recorded
  judgement, and every check must be green. The merge is
  `gh pr merge N --squash --match-head-commit <sha>`, one at a time,
  with `main` green on each merge commit before the next.
- Up to two principal engineers work a wave, each directing up to three
  workers, and never more than eight agents run at once. This replaces
  §6's "at most three agents at once".

## 3. Decisions for the owner (CD1 to CD41)

**The owner answered this table on 2026-11-16,** before delivery
started, as the brief says. §3.1 holds the answers. Each row has a
recommendation, kept below as it was put to the owner; where §3.1
differs, §3.1 stands. §6's Gate column names the rows each wave's chunks
rely on.

Five rows are answered in two steps. Four are marked "two steps" in the
table: the owner answered the method on 2026-11-16, and the result is
put to them in the chunk's PR. CD27 is the fifth, by its answer: one
question it left open comes back in CB-38b's PR (§3.1). §8 lists the
five second steps. CD11 was put as a two-step row; the owner's answer
leaves it no second step (§3.1).

| Id | Issue | Question | Recommended | Blocks |
|---|---|---|---|---|
| CD1 | #1826 | Overtaken: #1928 closed #1826 on 2026-11-13 (§2.6). | No answer is needed. | — |
| CD2 | #1843 | Close with its cause not proved? Keep a timed-out fact's dump thirty days? | Close: the fact was replaced, the capture expired, and the harness will now say whether the process stalled. Keep seven days; a dump is about 5 GB. | CB-1 |
| CD3 | #1788 | Close as a second sighting of #1865, on #1870's facts and a new pin? | Yes. #1870's fix was seen failing and passing. If the pin fails on `main`, the chunk stops and the product fix comes back to the owner. | CB-7 |
| CD4 | #1616 | When no score is computed, rewrite the bot's comment or delete it? | Rewrite the existing one; never create a "not computed" comment where there was none. | CB-5 |
| CD5 | #1889 | (a) A save asked for while a bulk rename or tag edit applies: wait or refuse? (b) Link rewrites cannot join the chain without the set of notes core will rewrite. (c) The exclusive creates. (d) The saved-query export that overwrites. | (a) Wait, as WD2 answered for the property write. (b) Accept as a dated census exception, and rebaseline clean open tabs from the report. (c) Exempt as a class with one class fact; admit only `RecoverDeletedFile`. (d) Core refuses a path that does not end in `.base`; one ledger line. | CB-9a CB-9b |
| CD6 | #1683 | Fix in core (a repair announces only what it changed) or in the Windows host? | Core. It is the cause, it is smaller, and it fixes both hosts. One ledger line. | CB-12 |
| CD7 | #1727 | A case-only rename on FAT32 or exFAT: refuse, or rename in two steps? | Refuse, and say so: "This drive keeps the capitalization a file was created with, so Note.md was not renamed to note.md." Folders the same. FAT32's file id changes on rename, which counts against two steps. | CB-13 |
| CD8 | #1660 #1395 | Fold #1395 in? What does a landed, unindexed toggle say? What of the tab's second sentence? | Fold it in. New event: "Task completed. Saved, but not indexed yet: database is locked. It will appear after the next scan." Always that event on this error. An FFI addition; ledger lines for Mac adoption and the create sentence. The tab's "Task toggled on disk, but the editor no longer matches it" still follows: splicing the buffer there is code with nineteen recorded review rounds, and is left alone. | CB-14 |
| CD9 | #1890 | Search and graph word four errors themselves, differently from core. The census's second rule forbids things the shell does on purpose. | Keep the four arms and fix only the fallback; note the divergence on #1527. Rule 1 over the whole shell; rule 2 only if a trial run shows few hits. | CB-15a |
| CD10 | #1830 #1791 | The cancel sentence "through core"? Spoken when? What does the Files status line say after a plain refresh? | The existing host sentence now, since core has no import vocabulary and both hosts compose it; one ledger line that the import sentences move to core on both hosts. Spoken at once. The line says what it said before, never a count. | CB-16 |
| CD11 | #1866 | Does a button that goes unavailable because the reader just activated it count as this defect? (Two steps.) | CB-17a's NVDA session presses each, and opens a sheet from the menu and from the launcher first, since neither has been heard. Any that says "unavailable" and moves the reader is fixed by moving the keys first; the rest are put to the owner in CB-17a's PR to accept, dated. | CB-17a CB-17b |
| CD12 | #1708 | (a) Does line reading speak a failed diagram's description? (b) Where does a pending diagram's name sit? (Two steps.) (c) Find and copy over the hidden description. (d) Tab onto a diagram reads its whole source. | (a) No; its text is already in range. (b) A half-day spike recorded on the issue, then the owner's yes. (c) Math's rule: find skips it, copy carries the fenced source. (d) Do it here as CB-20b, the twin of #1591: the source on request by a chord, a core sentence, an FFI addition, a ledger line. | CB-20 CB-20b |
| CD13 | #1330 | Take it into this plan? | Yes, as CB-21, diagnosis first: whether Narrator wants TextPattern2's caret range or another event. If the diagnosis finds no route Narrator honours, close with the record. | CB-21 |
| CD14 | #1661 | Activate at Cursor in reading mode: disabled, or enabled and refusing? | Disabled, with the Preview Embed sibling. The palette then speaks its own unavailable sentence. | CB-22 |
| CD15 | #1800 | The copy for Math, Code and Diagrams, and who owns it. | One line per panel saying where the content is today, for example "Math for this note isn't listed here yet. Open the note in the reading view to read its math." Host-owned, beside the other right-pane empty states, with the exception written down. | CB-23 |
| CD16 | #1846 | NVDA names the tab control and the tab when the keys enter the editor from another region. Acceptable? | Yes. Close on the confirming run. | CB-24 |
| CD17 | #1775 | Core rewrites the marker to a label (no FFI change), or a structured callout over the FFI? | The label: the authored title when there is one, else the type capitalised ("Note"). The structured callout is a feature for Milestone W. One ledger line. | CB-26 |
| CD18 | #1631 | Drop the session-long short-circuit or narrow it? Reword the panic sentence? | Narrow it to an upstream error that names a poisoned lock. Reword "mermaid renderer panicked: Any { .. }" to "Diagram not drawn: the renderer failed on this diagram. The source follows." One ledger line. | CB-27 |
| CD19 | #1635 | How long is a timed-out decode remembered, given #1908 pinned a retry? The per-note deadline and its sentence. | For the life of the process, by a hash of the bytes, 256 entries. 20 seconds. "Image not shown: this note's images used their 20-second decoding budget." | CB-28 |
| CD20 | #1610 | Do it here? The two limits. (Two steps.) The refusal's sentence. | Do it, after CB-28. The owner picks the limits from the calibration table the PR records. Its own sentence: "Image not shown: it would take too long to decode." | CB-29 |
| CD21 | #1628 | How is `f'''(x)` rendered until MathCAT fixes its cell? Does `y''^2` fold? | Speech from the folded tree, braille and the exposed MathML from the loose one; report upstream in the PR. `y''^2` stays as XB-8 pinned it. | CB-30 |
| CD22 | #1705 #1782 | Sigma: selector keys, or a new fold with an index rebuild? Snippets: trim where, and with what mark? | Selector keys (no migration). Trim at read to a word boundary, mark with "…"; stripping raw Markdown from a snippet is a feature and stays out. A ledger line each. | CB-32 |
| CD23 | #1834 | The ranking, the selection rule, and whether the count sentence names the selected row. | Sections in best-match order; an untouched selection follows the first row; no new sentence. Ledger: the Mac's selection rule. | CB-34 |
| CD24 | #1721 | Draw label chips, report the midpoint, or leave the risk? | Report a small rectangle at the path's midpoint, after the covering-state scan is written and seen to fail. Its cost: touch or mouse exploration then finds an edge only near its midpoint. Drawn chips stay with the canvas milestone. | CB-36 |
| CD25 | #1724 | With nothing selected, does an edit seat the first row (today) or leave nothing selected? (Two steps.) | Nothing selected after a mutation; the first-row seat stays for a load. Confirmed with the owner after the chunk's diagnosis. | CB-37 |
| CD26 | #1694 #1695 | Does the seal reach UI Automation pattern calls? What of drag and drop? | Pattern calls and `SetFocus`: no. Close #1694 as won't-do and keep contract 28 PR-6 as a standing exclusion that names both. Drag and drop: CB-38 probes it with touch and stylus, then takes the route or records it. | CB-38 |
| CD27 | #1665 | Does the editor popover join the modal surface? | No. Close; one line in contract 40. | CB-38b |
| CD28 | #1669 | Keep the tree's rows while a list's row is selected, and follow a reader on an unselected row? | Both, here, after CB-39, with a design step first. If one must go, keep the first. | CB-40 |
| CD29 | #1668 | Rebuilt rows on a sort move and on a pin. (Two steps.) | Fix the pin in memory, which keeps the pinned row and every row it does not displace. Probe whether a `Move` keeps WPF's container. If it does, both halves close. If not, the owner accepts the remainder in the PR or the issue stays open. | CB-41 |
| CD30 | #1625 | The Connections model does not see session events. | Scope it out of the model in a comment and pin production with one lifecycle fact. | CB-42 |
| CD31 | #1903 | When does it close? | The PR merges with `Refs`; it closes when the owner posts the home-VM probe, on an image rebuilt after CB-43. If the pool is not staying up, park it and say so. | CB-43 |
| CD32 | #1596 | Overtaken in part: plan 45's XB-6 is delivering #1596 (#1938, §2.6). What is left: the image's two evergreen installers (Build Tools, `dotnet-install.ps1`) and the floating Python patch. | Record all three as accepted, dated: both installers are signature-checked. | CB-43 |
| CD33 | #1614 | How strictly is the runtime held? Does it close with the rebuild still owed? | First print what each pool loads today. Then each job installs the pinned SDK by exact URL and committed hash and does not use `setup-dotnet`; CI's runtime moves back to the one the pinned SDK bundles. It closes at CB-44's merge, with the rebuild on the owner's steps. | CB-44 |
| CD34 | #1718 | Which design, and where is it delivered? | A read-only second connection in WAL mode, phased from Quick Open, as its own plan. Here: the design spec only. No interim. | CB-45 CB-45b |
| CD35 | #1842 #1848 | Build here or move? | Move both to Milestone W. | CB-0 |
| CD36 | #940 #1389 #1323 | Three audited issues that could end on a decision. | Close #940 by hand and let #1513 carry its one remaining fact. #1651 carries #1389's mac half, and plan 43's PR 52 is retired. #1323 stays open for its one mac fact, retitled. CB-0 makes these edits once the row is answered. | CB-0 |
| CD37 | #480 #1493 #1628 | Nobody is named to read the release lists. | The agent that opens each wave of this plan checks them and comments. After the plan ends, the owner's choice. | CB-0 |
| CD38 | — | The reading listed about nine families of sentences the host composes that `AGENTS.md` says core should own (the sidebar's failure leads, the panel error leads, the status-line copy and others). Each move is an FFI addition with Mac adoption. | Not in this plan. CB-0 files one issue for Milestone W that holds the catalogue. | CB-0 |
| CD39 | — | The brief's order is sweeps before code-area chunks. With three slots and shared files, §6 fills free slots with code-area chunks from C2 on, ahead of the later reader-facing sweeps. | Accept §6's order. | §6 |
| CD40 | #1835 | Close on the control's removal, with no fact? There is no toggle left to measure. | Yes. CB-0's comment names #1856 and quotes the search that finds only the menu row. | CB-0 |
| CD41 | #1900 | Added 2026-11-15 (§2.6). Once a quarantined note lists again, its row carries no real modified time: core holds -1. (A row that migration 026, 034 or 038 left keeps its real time and is not in question.) Windows shows no date for it: the row lands in the oldest date group and sorts as the oldest. The Mac's row says "Modified" with a 1969 or 1970 date, in its accessibility value too, so VoiceOver speaks it. A base with a `file.mtime` column shows that date on both hosts. Accept that, or mark the row as unreadable over the FFI? | Accept it here: the row lists with size 0 and the odd date until the file reads again, and the ledger line says so. The Mac's row speech changes, so CB-8's PR writes `not delivered: VoiceOver pass` (§4 rule 5). A marked row (an optional time, or an `unreadable` flag, with a sentence on both hosts) is an FFI change with new speech; CB-8 files it for Milestone W. | CB-8 |

### 3.1 Owner answers

Recorded 2026-11-16. An answer here supersedes the recommendation in
the table above. No row remains pending, apart from the second steps of
CD25, CD27 and CD29 (CD12(b)'s was answered on 2026-11-22, in CB-20);
CD8's order, which CB-14 changed
and which waits for the owner (§12, Wave C6); two points of CD19 that
CB-28's delivery left for the owner (§12, Wave C7); and whether the
#1991 call covers every route through a tab, as that row reads it
(§12, Wave C7). The owner gave the answers in an interview on
2026-11-15 and 2026-11-16, and every decision row is dated 2026-11-16.
The Run process row is dated 2026-11-15, the day of the delivery
instruction. The #1991 row is a later call, dated 2026-11-18, on an
issue that CB-9b's spec work filed. The CD20 (second step) and CD9
(wording) rows are later calls, dated 2026-11-20, in wave C8. The CD11
(CB-17b) row is a later call, dated 2026-11-21, in wave C10. The eight
rows after it are later calls of waves C10 to C13, from 2026-11-21 to
2026-11-26, each put by a chunk's spec or PR. CD1 is overtaken and has
no answer.

Some rows also carry a call the owner did not make. It is marked "the
plan lead's call, 2026-11-16": how the answered work is cut into chunks
and phases, and which chunks open with a design the owner reads. The
owner can change any of them.

| Id | Date | Answer | What it changes |
|---|---|---|---|
| CD9 | 2026-11-16 | **The four search and graph error arms are not kept: the host says core's sentences.** The census part is as recommended: rule 1 over the whole shell, and rule 2 only if a trial run shows few hits. | The four arms go; one sentence (the invalid-query one) changes, on search and on graph. The `Io`, `Db` and `Unsupported` arms already produce core's words (`SearchOverlayViewModel.cs:909-916`, `GraphDocumentViewModel.cs:1509-1516`); only the `InvalidQuery` arm adds a lead of its own, "Search query is invalid:". CB-15b checks that sentence on both surfaces under NVDA. The #1890 census needs no exception for the arms. §8's note for #1527 no longer says Windows keeps four arms worded apart from core. |
| CD10 | 2026-11-16 | **The import sentences move to core now,** as new core events, not as the host's sentence with a ledger line. The cancel is still spoken at once, and the Files status line still says what it said before, never a count. | CB-16 is M to L on the windows, rust and swift lanes, with an FFI addition. It joins §6's `crates/slate-uniffi/src/lib.rs` and `a11y.rs` rows. §8's ledger line becomes the Mac's adoption of the import events, with a VoiceOver pass. |
| CD11 | 2026-11-16 | **Every self-disabling button moves the keys first, whatever the NVDA session hears,** not only the ones heard to fail. | CD11 has no second step. It covers every self-disabling button the `Command=` scrape finds, not only the six groups the reading listed (about twenty buttons). Every one of those changes goes in CB-17b (C10), none in CB-17a. CB-17a keeps the NVDA session, which still runs and is recorded, the keys-first witness, and the `Command=` scrape with today's sites as dated exceptions. CB-17b ends those exceptions, and no dated accepted exception is left for this class. §8's owner step and the closure matrix row for #1866 drop the second answer. |
| CD15 | 2026-11-16 | **Core owns the three empty-panel lines,** not the host. The wording is the recommended pattern, one line per panel. | CB-23 is S to M on the rust, windows and swift lanes, with an FFI addition and a ledger line. It joins §6's uniffi row. |
| CD17 | 2026-11-16 | **A structured callout over the FFI, with foldable callouts, in this plan,** not the label rewrite. A callout carries its type and title, and Obsidian's `+` and `-` fold markers are honoured, so a reader can expand and collapse it. | CB-26 is L, with an FFI change, a full NVDA pass and a Narrator pass. The Mac lane keeps compiling by blind mirror edits, and until plan 43 adopts the callout the Mac renders whatever that edit gives. Mac adoption and a VoiceOver pass go on the ledger. CB-26 joins §6's uniffi and `Reading/` rows. It opens with a short design the owner reads before code; that step is the plan lead's call, 2026-11-16, not the owner's answer. §11 records it as a feature-sized chunk taken on the owner's decision. |
| CD22 | 2026-11-16 | **Sigma: a new fold stored in the index, with an index rebuild,** not selector keys. Snippets are as recommended: trimmed at read to a word boundary and marked with "…". | CB-32 is M to L. It gains a migration and a one-time rebuild of each vault's index on upgrade, and its spec says what a user with a large vault sees while the rebuild runs. Its spec also says which fold it is: a new stored column, which leaves the sort order unchanged, or a change to the sort key (`tree_sort_key`), which may move the Files tree's sort order for Greek names on both hosts (`reading/07_core.md:95`). The ledger lines stay, and #1705's names the sort-order change if there is one. |
| CD24 | 2026-11-16 | **Label chips are drawn now,** not the midpoint rectangle. | CB-36 is M to L: each labelled edge gets a drawn chip that carries its bounds, with layout and overlap rules. Its order is the same everywhere it is stated: the covering-state scan is written first and seen to fail, then a short design the owner reads, then the chip code. The design step, and landing the chips as a second PR of CB-36 so the two small changes (#1838, #1722) do not wait on the design, are the plan lead's call, 2026-11-16. The Mac already draws edge-label chips (#371, `CanvasRendererView.swift`), so a ledger line is added only if the chips change core. |
| CD27 | 2026-11-16 | **The editor popover joins the modal surface,** not "no, close". CD26 is as recommended. | #1665 is no longer closed by decision. The lead cut it as CB-38b, a chunk of its own in the wave after CB-38 (the plan lead's call, 2026-11-16): eight admission tables and about 35 read sites, with an NVDA pass. One question the owner did not answer makes this a two-step row: does an open popover count as modal while focus is elsewhere? CB-38b's spec proposes the rule. Its starting point is the ring's, which counts the popover only while focus is inside it (`MainWindow.ShellRegions.cs:35-36`). The owner confirms the rule in CB-38b's PR. §4 rule 7, the closure matrix and §6 follow. |
| CD34 | 2026-11-16 | **#1718 is built in this plan,** not handed to its own plan: a read-only second connection in WAL mode, phased from Quick Open. No interim. | CB-45 stays the reviewed design, which the owner reads, and moves to C11 so the build can follow it. How the build is cut is the plan lead's call, 2026-11-16: CB-45b is the first implementation phase, and the design cuts the later ones. So is the review of CB-45's PR: it is documents only and still gets the red team and Codex (§4 rule 9). #1718 closes when the last phase merges. §11 records a large core change added by the owner's decision. |
| CD2 to CD8, CD12 to CD14, CD16, CD18 to CD21, CD23, CD25, CD26, CD28 to CD33, CD35 to CD40 | 2026-11-16 | **Adopted as recommended.** The Recommended column stands exactly for CD2, CD3, CD4, CD5 (all four parts), CD6, CD7, CD8, CD12 (all four parts, CB-20b among them), CD13, CD14, CD16, CD18, CD19, CD20, CD21, CD23, CD25 (the target; its second step follows the diagnosis), CD26, CD28 (both, design first), CD29 (the method; its second step follows the probe), CD30, CD31, CD32, CD33, CD35 (with the tag `ds11-accent-contrast-gate` at 9edf4b6b, the repository's first pushed tag), CD36, CD37, CD38, CD39 and CD40. | Each chunk's spec as written for those rows |
| CD41 | 2026-11-16 | **As recommended: the odd date is accepted.** A quarantined note lists with size 0 and no real modified time until the file reads again. A marked row is filed for Milestone W. | CB-8's PR writes `not delivered: VoiceOver pass` with its ledger line, and files the marked row |
| Run process | 2026-11-15 | **How the chunks are delivered.** The owner's delivery instruction, as §2.6 and §4 rule 9 state it: one red team on a light PR; the red team and then Codex rounds until it approves on every other PR, with an independent judge at 14 rounds; Codoki at 3/5 or a recorded bypass, and every check green, before a merge; merges one at a time, pinned to the head; up to two principal engineers a wave, each directing up to three workers, and never more than eight agents at once. | §4 rule 9; §6's first paragraph; the Review line of each first-wave spec |
| #1991 | 2026-11-18 | **A task toggle made through a tab counts as the reader's own edit.** It stays on the tab's undo stack and is undoable. An undo that takes it back is ordinary editor behaviour, and a save then writes what the reader sees. The call is read as covering every route through a tab (the editor, the reading view, and the Tasks panel and Tasks Review through an open tab), since the reader makes the toggle on each. | #1991 is closed as not planned, with no code change. CB-9b's no-undo rule (its Design 0) stays for the writes the reader did not make: property writes, the bulk rename, Bases cell edits, the tag edit and link rewrites. CB-14's landed-but-unindexed path splices no tab, rebaselines none and leaves every undo stack as it was. |
| CD20 (second step) | 2026-11-20 | **One estimated decode time and one limit, 3 s,** in place of the two limits CD20 put. The owner picked 3 s from CB-29's calibration table, after approving the CI-runner probe run; no real plotting export was fetched first. | CB-29's guard refuses an SVG whose estimated decode passes 3,000 ms (`MaxEstimatedDecodeMilliseconds`); a matplotlib-shaped scatter is admitted up to 3,425 points and refused from 3,426. CD20 has no step left. §12's Wave C8 record says how. |
| CD9 (wording) | 2026-11-20 | **"Plain words. Please change it."** The answer to #2020's "For the owner" section: under CD9 the search's invalid-query sentence had become core's developer text. | Core's invalid-query detail is a plain sentence, "Check the quotes, symbols and operators in your search.", after "Search error:" on both hosts and in Bases' `file.matches`, delivered by #2028 (§12). |
| CD11 (CB-17b) | 2026-11-21 | **"Stay, and speak on a press".** Two buttons stay available while they run: Bulk Rename's Preview, and Expand loaded folders, the Files pane's button. A press made during a run speaks a sentence from core and does nothing else. | CB-17b's question departed from CD11's words, which have every self-disabling button move the keys first, and this is the owner's answer to it. The CD11 row above stands for every other button. CB-17b delivers it (§5). |
| CD11 (CB-17b's sentences) | 2026-11-21 | **"Wording is fine".** The answer on the three sentences a press during a run speaks: "The preview is still running.", "The rename is still running." and "Folders are still expanding." | No core sentence fitted, so core gained one event, `A11yEvent::WorkStillRunning`, whose `RunningWork` is `BulkRenamePreview`, `BulkRenameApply` or `ExpandLoadedFolders`, at the palette's unavailable priority, with the uniffi mirror, both bindings and the ledger rows. CB-17b's first PR delivered it (#2041). The Mac's adoption, with a VoiceOver pass, is on #1954. |
| CD34 (CB-45) | 2026-11-22 | **Call 2: "Accept, record it (Recommended)". Call 9: "Speak new selection (Recommended)". Call 10: "Accept (Recommended)".** After reading the design the owner answered the two points its merge left open, each "Confirm (Recommended)": the widened Call 9 branch, and that a default first-row selection is not the reader's. | Call 2: during a rescan a typed Quick Open query speaks the count from before the scan, and the later re-rank is silent; CB-45b appends the AR row to doc 40. Call 9: when a re-rank cannot keep the selected path, it names the new selection with the existing `RowSelected` sentence; the lead widened it so the re-rank after a Slate-owned write keeps a surviving selection too, and the owner confirmed that. Call 10: during a Slate-owned write a typed query speaks the count from before the write and then the corrected count, a sentence in the AR row. CB-45b is unblocked, and changes no FFI (the lead's ruling on CB-45's Call 3). |
| CD12(b) (second step) and CB-20 | 2026-11-22 | **Call 1, where a pending diagram's name sits: "Before the source". Call 4, an embedded image's line, which NVDA read as "blank": "Fix it in CB-20". The caret fix the diff red team's B1 brought: "Accept (Recommended)".** | A pending diagram's "Diagram, rendering" is read before its source. An image's line reads core's existing "Embedded image: …" sentence, with no new copy. After Up or Down onto a line of hidden speech the caret rests just after the element, at full height, and that line does not keep its column. CD12 has no step left. CB-20 delivered all three (#2044). |
| CD15 (CB-23's lines) | 2026-11-23 | **"Add the shortcut" and "Vary by state"; on the nine lines that followed, "Approve as written (Recommended)"; and on the panels themselves, "File one issue (Recommended)".** | Each line names the reading view's shortcut, taken from the chord table, and there are nine: one for each of the three panels in each of three states, a note in the editor, the reading view already open, and no note open. The reading-view lines name no shortcut. In the editor the Math line is "Math for this note isn't listed here yet. Open the note in the reading view (Ctrl+Shift+E) to read its math." The panels that would list a note's blocks are #2048, in Milestone W. CB-23 delivered it (#2049). |
| CB-22b (letters) | 2026-11-23 | **Call 1, the letters table: "Approve the table (Recommended)". Call 2, the Workspace menu: "A' (Recommended)".** | Every changed access key is the table's letter, pinned by `TheMenuLettersAreTheOwners`. The Workspace menu has 28 items whose labels hold 23 letters, so Reopen Closed Tab, Split Down, Grow Pane and Shrink Pane carry no key; their chords and the arrows reach them. The cost the owner approved: a key that two enabled items shared now runs its item at the first press, so Alt+F, C closes the vault at once. CB-22b delivered it (#2055). |
| CD23 (CB-34) | 2026-11-23 | **"Silent follows (Recommended)".** A selection the palette moves is never announced at a publication, and a reader's removed row is replaced and announced once, before the count. | Two things `main` says go quiet: an untouched row that a query removes, and the recovery from no matches; #2058 has their base and head phrases. CB-34 delivered it (#2058). |
| CD10 (CB-16) | 2026-11-24 | **Call 2: "Approve the wording (Recommended)". Call 4: "Off, keys go back (Recommended)".** | A press of Cancel import says "Cancelling import…" at once, and the summary follows when the import settles. The import's Cancel buttons go off at the press and the keys return first, as the trash's Cancel does. That differs from the CD11 (CB-17b) row, which is for the two buttons that start work. CB-16 delivered it (#2061). |
| CD24 (CB-36's chips) | 2026-11-25, and again 2026-11-26 | **On 2026-11-25: the outline, "Outline (Recommended)"; twin chips, "Later covers (Recommended)"; a cut-off label, "Tooltip (Recommended)". On 2026-11-26, asked again after two premises of the design proved false: "Keep later covers (Recommended)", "Keep the outline (Recommended)", "Act on the card (Recommended)".** | A chip has a 1 px outline in `Slate.Canvas.EdgeBrush`. Where chips overlap, the later one covers the earlier one. A cut-off chip shows its full label in a tooltip on hover. A click on a chip acts on the card under it. The two false premises, which the second PR's spec red team found: the Mac's chips cover in Swift dictionary order, not document order (#2069), and the outline's colour equals the card border's in every theme. CB-36's second PR delivered it (#2073); a keyboard route to a cut-off chip's full label waits on the owner's design (#2076). |

Two adopted rows changed in delivery by the lead's rulings, and §12's
Wave C6 record says how. CB-14 speaks CD8's tab sentence before the new
event, not after it, and whether that order stands waits for the
owner's word. CB-30 brailles an authored `‴` or `⁗` after a
carrier as single primes, from a tree of its own, where CD21 has
braille from the loose tree; and its upstream reports are drafted in
its PR, with filing left to the owner.

In wave C7, by the lead's ruling on CB-28's Call 2, the reading view's
diagram stage has CD19's memory and not its 20-second deadline, and
whether it gets one waits for the owner. So does a consequence of the
deadline that CB-28's PR names as its third departure: successful
decodes count against the 20 seconds and are not cached. §12's Wave C7
record says how.

In wave C8 the owner answered CD20's second step and gave CD9's
invalid-query sentence its wording (the two 2026-11-20 rows above).
One question from that wave waits for the owner: an open search
overlay does not announce a second refused search, since its line is
the same and contract 29's S7 announces a summary only when it
changes; the recorded alternative is a sentence for each refused query
(§12's Wave C8 record).

In waves C9 and C10 the owner gave four answers. Three were on Codex
stop questions (§12's record of those waves). The fourth is the CD11
(CB-17b) row above: CB-17b put a question that departed from CD11's
words, and "Stay, and speak on a press" is the owner's answer to it,
for Bulk Rename's Preview and the Files pane's Expand loaded folders.
Three notes on other rows above, each the plan lead's, 2026-11-21:

- CD14's "disabled" means that in reading mode the Editor menu's arrow
  keys skip Activate at Cursor and Preview Embed, since WPF skips a
  disabled item. NVDA's object navigation still reads both, as
  "unavailable", and the positions still count them (CB-22, #2035).
- CD22's fold was delivered as a change to the sort key
  (`tree_sort_key` folds `ς` to `σ`), with migration 042 rebuilding the
  seven indexes that store it. Windows shows no change in the Files
  tree's order, and the Mac's folder order moves for names that meet
  at a `ς` or `σ` (CB-32, #2033; #1954).
- CB-33 has no decision row. Its remedy is the lead's plan call:
  SQLite's query planner stability guarantee on every connection core
  opens. The owner was told, and the call did not wait for an answer.

The questions those waves left for the owner are in §12's record of
Waves C9 and C10: whether "Plain words" extends to the ordinary-input
developer sentences; the frozen graph contracts 0b-3 and 0b-6; the
Editor menu's A and O keys, and a silent Ctrl+click on a checkbox
before a note's first task list loads; the plan call above and the
drafted SQLite report; and the other drivers that CB-17a's census does
not judge.

From CB-17b's sentences to CB-36's chips, waves C10 to C13 brought the
eight later rows above. The owner also answered three questions that
are not decision rows, all on 2026-11-22 and recorded in §12's record of
those waves: CB-17b's NVDA spot check, where the property row's Save
still says "unavailable" ("Accept and file it (Recommended)", filed as
#2040); CB-45's Codex loop ("Run round 4 (Recommended)"); and CB-20's
Codex round 8 ("Small fix, no new round (Recommended)"). Three notes on
rows above, each the plan lead's:

- CD10's "spoken at once" is delivered at High. At Medium, NVDA heard
  "Cancelling import…" about 11 s after a press of the status bar's
  Cancel, behind the status bar's long help, so the lead ruled it High,
  with CB-17b's `WorkStillRunning` as the precedent (2026-11-25, CB-16,
  #2061).
- CB-45b changes no FFI and no `crates/slate-uniffi/src/lib.rs` (the
  lead's ruling on CB-45's Call 3, 2026-11-22), and CB-45's design cuts
  three phases: CB-45b, CB-45c and CB-45d, the last of which ends
  #1718 (§5).
- CB-35 turns letter search off on five lists, Quick Open, search, the
  Files filter, Move To and the canvas pickers. No decision row covers
  it. The lead ruled four of them on 2026-11-25 and put them to the
  owner's eye, and the merge judge added the Files filter's (#2077).

The questions waves C10 to C13 left for the owner are in §12's record of
those waves.

## 4. Rules (every chunk)

Plan 44 §4 and plan 45 §4 apply, except their found-on-the-way rule,
which rule 1 replaces: the failing test first; the Windows recipe
(bindings regenerated per worktree into a per-worktree
`CARGO_TARGET_DIR`, `dotnet test --filter`, `dotnet format --include`
after every C# edit); the four Rust gates in PowerShell for a core
change; field passes through `scripts/windows/launch-for-field-pass.ps1`;
the parity matrix regenerated when a chord or help line changes; the
cooldown; merges one at a time; a workflow change pinned in
`Workflows.Tests.ps1` (or `Golden.Tests.ps1`, or the C# census that
already pins the step) and proved on the lane. Added for this plan:

1. **A defect found on the way is sized by what its fix touches.** CB-0
   writes this into `AGENTS.md` for the whole repository; its spec has
   the exact text.
   - Small: the fix stays in files the PR already changes, plus their
     tests. Fix it in the PR.
   - Medium: the fix reaches three or fewer other non-test files in the
     same subsystem. Fix it in the PR, or defer it (rule 10); outside a
     plan, file it.
   - High: the fix reaches more than three other non-test files, crosses
     subsystems, or needs a design, an owner decision, an FFI change or a
     machine the agent does not have. File it. If it blocks the PR, its
     fix merges first, as its own PR.
   - A subsystem is one top-level folder of an app or one module of a
     crate; the files of one partial class count as one subsystem
     wherever they sit.
   - A fix that needs an owner decision, an FFI change, a machine the
     agent does not have, or a new or changed spoken sentence that needs
     a full screen-reader pass is never small or medium.
2. **"Found and fixed here."** Each fix taken in the PR has its failing
   test first and a line under that heading in the PR body. No issue is
   filed for it. An agent stops taking them when they would more than
   double the non-test files the PR changes; the rest are deferred.
3. **A sweep's siblings are the chunk's own work,** whatever their size.
   The exception is a sibling that needs an owner decision or a Mac: the
   census ships with it named as a dated exception, and it gets a
   decision row or a ledger line. A sweep PR that would pass rule 2's cap
   splits, census first.
4. **A census lands first, with today's instances as dated exceptions.**
   Each later PR of the sweep deletes the exceptions it fixes, so every
   fix is visible in a diff and no new sibling arrives while the sweep is
   in flight.
5. **A core change a Mac user hears, sees or gets different behaviour
   from adds its ledger line in the same PR.** Where it changes speech,
   the PR body writes `not delivered: VoiceOver pass`, with the ledger's
   number, and closes its issue. A find of this kind is never taken
   inline; it is deferred.
6. **A flake's PR records where its evidence lives and for how long.**
   A single home-pool timeout is read against `C:\slate-ci\logs` before
   it is filed as a flake.
7. **An issue closes on a fix that was seen failing and then passing.**
   Where that cannot happen, a decision row says so: CD2, CD3 and CD30
   close without a replay or a red phase; CD16 and CD26 close without a
   fix; CD40 closes #1835 on the removal of its control. CD27 is no
   longer in this list: the owner's answer makes #1665 a fix (§3.1).
8. **Every PR body ends with the count:** found on the way, fixed here,
   deferred, filed.
9. **Review and merge.** From 2026-11-15, by the owner's instruction.
   This replaces the rule the plan was written with: a red team on every
   chunk, Codex on twelve named chunks, and a chunk that only changed CI
   config, tests or comments on its tests alone.
   - A light PR changes only documents or comments, or makes a small
     configuration change with no product code and no test logic. It
     gets one red team: a fresh Claude agent with
     `24_red_team_protocol.md`, the diff and the spec. Nothing more.
   - Every other PR gets that red team, then Codex adversarial-review
     rounds until it says approve. At 14 rounds without approval an
     independent agent judges the churn. The PR body records both.
   - After the push, Codoki must report "Safe to merge" at confidence
     3/5 or higher, or be bypassed on an independent agent's recorded
     judgement, and every check must be green. The merge is
     `gh pr merge N --squash --match-head-commit <sha>`, one at a time,
     with `main` green on each merge commit before the next.
   - One light PR is reviewed as the others are: CB-45's design. It is
     documents only and gets the red team and then Codex, because the
     brief names Codex for the scan-lock design. This is the plan lead's
     call, 2026-11-16.
10. **A deferral is one line on "Plan 47 deferred finds",** an issue CB-0
    opens: the file, what is wrong, and the failing test that would show
    it. CB-46 works that list.

## 5. The chunks

Size: S under a day, M a few days, L a week or more. Lanes: `windows`,
`rust`, `swift` (compile only), `a11y`, `runner` (the Windows and mac
runner tests), `pilot` (the Windows CI pilot). AT: the NVDA or Narrator
work the chunk carries.

| Chunk | What becomes true | Closes | Size | Lanes | AT | Decision |
|---|---|---|---|---|---|---|
| CB-0 | The found-on-the-way rule is in `AGENTS.md`, and plans 43 to 46 point at it; the audited issues carry a status comment; #1835 is closed; #1842 and #1848 are in Milestone W with 9edf4b6b tagged; #1733 points at plan 43; the ledger, the deferred-finds issue and CD38's catalogue issue exist; the stale plan headers are corrected | #1835 (CD40) | S | none | — | CD35 CD36 CD37 CD38 CD40; CD8 CD13 for one comment each |
| CB-1 | A timeout that could not take a stack reports how long a heartbeat thread went without running and how long the collector paused, so the log shows whether a stalled process would explain it | #1843 | S | windows | — | CD2 |
| CB-2 | The model reports a stalled case by name; a slow shard is not ended at 45 minutes; a failed attempt keeps its dumps | #1624 | S | windows, runner | — | — |
| CB-3 | The shell gate's binaries keep seven days; the lane checks contract 38's ledger against its generator and prints the differing rows; the generated-table checks are pinned, and the docs-trigger census sees their spelling | #1647 #1699 | S | windows, runner | — | — |
| CB-4 | No fixture tears down under work it released: #1673 and eight sibling fixtures; `TestWaitCensus` rule 3 | #1673 | M | windows | — | — |
| CB-5 | a11y-check rewrites its comment when no score is computed, and malformed results fail in one sentence | #1616 | S | a11y, runner | — | CD4 |
| CB-6 | The unit project's fixed waits can only shrink: `TestWaitCensus` rules 1 and 2 pin all 81 and the elapsed-time oracles with a reason each; the cold wall-clock budgets go through `WallClockBudget`; the waits that have a condition today become condition waits (at least five) | — (the class) | M | windows | — | — |
| CB-7 | The gate's journeys wait on conditions and read app files with delete sharing; rule 4; the graph settle-and-save pin; the app log joins the gate's uploaded evidence | #1788 | M | windows (gate) | — | CD3 |
| CB-8 | A quarantined note still lists, so an unreadable note no longer fails the Files tree; the two stale mac comments are corrected | #1900 #1709 | S | rust, windows, swift | — | CD41 |
| CB-9a | `SaveChainAdmissionCensus` with dated exceptions; the single-path routes join (the reload guard, `RecoverDeletedFile`, the two tabless writes); the create-class fact | Refs #1889 | M | windows | — | CD5 |
| CB-9b | A bulk rename and a tag edit are admitted over their notes; the saved-query export cannot name a note; two accepted exceptions are left, and the census names both: the link rewrites (CD5 part b) and the Bases export, which is not one of CD5's four parts but the reading's exemption (W11, `reading/03_save_chain_lifecycle.md`); both are put to the owner in CB-46's table. In the census the two are five rows (the link rewrites sit in four members, and the Bases export is the fifth), beside one standing row for navigation's tab replacement, which is not this class (CB-9a's spec, 2026-11-16) | #1889 | M–L | windows, rust, swift | NVDA spot if a refusal is new | CD5 |
| CB-10 | A failed vault switch returns to the welcome view once | #1730 | S | windows | NVDA spot | — |
| CB-11 | A parked citation summary press follows its note's rename | #1714 | S | windows | NVDA spot | — |
| CB-12 | A repair announces only what it changed, so an own rename after another session's does not say the old path is missing | #1683 | S | rust, windows, swift | NVDA spot (two sessions) | CD6 |
| CB-13 | A case-only rename on FAT32 or exFAT is refused and says so; the lane's weak-volume job runs the fact it has skipped by name | #1727 | S | rust, windows, runner | NVDA spot | CD7 |
| CB-14 | A toggle that landed but was not indexed says it landed; the unindexed caveats close their reason; the other leads a `SavedButUnindexed` reaches are listed | #1660 #1395 | M | rust, windows, swift | NVDA spot, three routes | CD8 |
| CB-15a | `FailureDetail` and `ShellFailureDetailCensus`; the Files sidebar's 21 sites | Refs #1890 | M | windows | NVDA spot | CD9 |
| CB-15b | Canvas, graph and search: 21 sites, `GraphConfigStore` among them; the four search and graph error arms go, and one sentence (the invalid-query one) changes, on search and on graph | Refs #1890 | M | windows | NVDA spot, the invalid-query sentence on search and on graph among them | CD9 |
| CB-15c | Panels, reading, workspace, lifecycle and commands; the census covers the whole shell | #1890 | M | windows | NVDA spot | CD9 |
| CB-16 | A cancelled import says so, and the import summary and cancel sentences are core events that Windows speaks; the status bar shows an import running, with a Cancel that moves the keys first; the Files status line never stays on "Loading files…"; the sidebar filter's and the Bases export's cancel paths are checked | #1830 #1791 | M–L | windows, rust, swift | NVDA spot | CD10 |
| CB-17a | One NVDA session hears a sheet opened from the menu and from the launcher, and each self-disabling button; the manifest census gains the keys-first witness and scrapes `Command=`, with today's sites as dated exceptions that CB-17b ends; the session's record is kept whatever it hears; no button is changed here | Refs #1866 | L (M until 2026-11-21: its spec sized it L, and the lead's ruling took L) | windows | NVDA: the session | CD11 |
| CB-17b | Nothing holding the keys goes off as a sheet opens, over all fourteen sheets and the launcher; every self-disabling button the `Command=` scrape finds moves the keys first, heard to fail or not: the reading's six groups and any other the scrape lists, all of them here; no dated exception of this class is left. Scope added on 2026-11-21 from CB-17a's delivery (§12, Waves C9 and C10), in this order. First, the two finds of CB-17a's NVDA session: the keyboard is dead after a cancelled Import… box, until Alt, and the keys rest on the bare window after the launcher's template picker and Import's folder picker close; restore-after-box facts for Import…, Delete folder note and the Queries leaf's Delete…, and the launcher's restore (#1866 comment 7067654509). Then the Clear All Marks button, which is stuck off on the open marks list on `main` because nothing raises its command's `CanExecuteChanged`; CB-17a's red test is its failing fact (#1955 comment 7067701218). Then CB-17a's merge-judge findings, all seven deferred here (#1955 comment 7069935142): a witness that skips at run time counts as running, a witness is not tied to its recorder's root, a dated row could be rewritten as an own-press exemption with the ceiling lowered in the same edit, and four nits. And the 47 dated rows of the focus manifest, which this chunk ends: the menu's fourteen sheet triggers, the launcher's one and 32 more, where the reading counted about twenty buttons. It ships as two PRs (recorded 2026-11-21). PR 1 is the 32 buttons, with Import…'s box, Bulk Rename and Clear All Marks; it is Refs #1866 and takes the dated ceiling from 47 to 15. PR 2 is the fourteen sheets, the launcher and its restore, and the end of the dated kind, and it closes #1866. Its step 0 found that Space or a click on any button in a sheet that stays open sends the keys behind the sheet, because each sheet is a focus scope and `ButtonBase` gives the keys back when it loses capture. By the owner's answer of 2026-11-21 (§3.1), "Stay, and speak on a press", Bulk Rename's Preview and the Files pane's Expand loaded folders stay available while they run, and a press made during a run speaks a sentence from core and does nothing else | #1866 | L | windows (gate) | NVDA spot: three sheets, and one button of each group | CD11 |
| CB-18 | Deleting the last note of a date group lands the keys with no "unavailable"; diagnosis first | #1671 | M | windows (gate) | NVDA spot | — |
| CB-19 | Every open combo box's rows report their place; `ComboBoxCensus`; the gate scans a list open in each family; probe first | #1783 #1836 | S–M | windows (gate) | NVDA spot | — |
| CB-20 | Line reading speaks a diagram's description, rendered and pending; `ReadingEmbeddedElementCensus`; spike first | #1708 | M | windows (gate) | NVDA full, Narrator spot | CD12 |
| CB-20b | A diagram's source is spoken on request, never on Tab | — (found by the sweep) | M | rust, windows, swift | NVDA spot | CD12 |
| CB-21 | Narrator announces the selection on Ctrl+A in the reading view; diagnosis first | #1330 | S–M | windows (gate) | Narrator | CD13 |
| CB-22 | Activate at Cursor and Preview Embed are unavailable in reading mode; a Ctrl+click on prose stays silent while the cache loads | #1661 | S | windows | NVDA spot | CD14 |
| CB-22b | No two items of one menu level share an access key: an access-key sweep ends the sixteen dated rows of `MenuAccessKeyCensus`, with the letters put to the owner as one table before any of them changes; a folder name with an underscore shows whole in the note header's Folders menu, where a folder `my_notes` shows "mynotes" today. Added on 2026-11-21 by the lead's second ruling on CB-22's Call 8 (§12, Waves C9 and C10); it runs after CB-17b, since both edit `MainWindow.xaml` | none (found by CB-22's census; Refs #1955, comment 7066667520) | M (S until 2026-11-23: the lead's rulings fixed the underscore class through text-block headers, the embed cards' among them) | windows | NVDA spot: the changed keys | the owner's letters, as one table in its PR (answered 2026-11-23, §3.1) |
| CB-23 | A right-pane panel with no body says what it will show, in a line core owns | #1800 | M (S–M until 2026-11-23: the lead's ruling on its Call 3 made the line the panel's focus stop) | rust, windows, swift | NVDA spot | CD15 |
| CB-24 | One NVDA session records #1846 and #1849; the note header's menu keeps its first row under a resting pointer, by #1944's remedy | #1849; #1846 by comment | S–M | windows (gate) | NVDA full on the steps | CD16 |
| CB-25 | A `LayoutItemsControl` drops a removed item's raw-view peer | #1860 | S | windows | — | — |
| CB-26 | A callout is an element with its type and title over the FFI, and on Windows a reader can move to it and fold or unfold it; it opens with a short design the owner reads (the plan lead's call, 2026-11-16) | #1775 | L | rust, windows, swift | NVDA full, Narrator | CD17 |
| CB-27 | One panicking diagram no longer disables diagrams for the session; a test-fault seam lets a field pass hear the new sentence | #1631 | S | rust, windows, swift | NVDA spot through the seam (as merged, no known input reaches the panic sentence or the session-long sentences; the seam reaches both) | CD18 |
| CB-28 | A timed-out SVG decode is remembered, and a projection's decodes have a deadline | #1635 | M | windows, rust, swift | NVDA spot | CD19 |
| CB-29 | The SVG guard refuses the documents that decode slowly, from recorded limits | #1610 | M | windows, rust, swift | NVDA spot | CD20 |
| CB-30 | A scripted function after an ordinary atom is spoken as math; three or more primes are spoken as primes | #1629 #1628 | M | rust, swift | NVDA spot, speech and braille | CD21 |
| CB-31 | A file first indexed by a save, a repair or an open heals the dangling links that name it | #1704 | M | rust, swift | — | — |
| CB-32 | A target with a final sigma resolves, in links and in Quick Open's name match, through a fold stored in the index; each vault's index is rebuilt once on upgrade; a link snippet starts and ends on a word. Its spec says whether the fold is a new stored column (sort order unchanged) or a change to the sort key, which may move the Files tree's order for Greek names on both hosts. If the rebuild shows the user anything, its spec names that surface and checks it against CB-15c's files (same wave) before it starts | #1705 #1782 | M–L | rust, windows, swift | NVDA spot | CD22 |
| CB-33 | No cached or hot statement with an equality on `files.path` re-prepares per binding | #1917 | M | rust, swift | — | — |
| CB-34 | A typed command name runs that command | #1834 | M | rust, windows, swift | NVDA full | CD23 |
| CB-35 | Quick Open, search and the five other search fields flush as the palette does; a census holds every search field over a republishing list to it | #1837 | M | windows | NVDA spot, timed | — |
| CB-35b | The search and filter fields whose rows live in a grid or the canvas outline tree lay out their rows in the operation that publishes them: the bibliography search, the Bases quick filter, the canvas filter, the graph filter, the graph inspector's name and group queries, and Bulk Rename's keys. Cut from CB-35 by the lead's ruling of 2026-11-25 (§12, Waves C10 to C13); CB-35's `SearchFieldCensus` holds a row for each member that points at #2070 | #2070 | sized when its spec is written | windows | NVDA spot, timed: the bibliography search and one grid filter | — |
| CB-36 | The board paints in scene order; a first search for an off-window item returns it; each labelled edge has a drawn chip, and the edge's bounds are the chip's. For the chips: the covering-state scan first and seen to fail, then a short design the owner reads, then the chip code. The chips land as a second PR of CB-36, so #1838 and #1722 do not wait on the design (the design step and the two PRs are the plan lead's call, 2026-11-16) | #1838 #1722 #1721 | M–L | windows (gate) | NVDA spot | CD24 |
| CB-37 | A deselect made while an edit installs stands; diagnosis first | #1724 | M | windows | — | CD25 |
| CB-38 | A probe says what touch, stylus and a drop do under the seal, and the seal takes each route that acts; the route table names every route; #1694 is recorded as decided | #1695; #1694 by decision | S–M | windows | — | CD26 |
| CB-38b | The editor popover is part of the modal surface: a member of its own, an arm in each of the eight admission tables, and an answer at each of about 35 read sites. The lead cut it as CB-38b (the plan lead's call, 2026-11-16). Its spec proposes whether an open popover counts as modal while focus is elsewhere, starting from the ring's "only while focus is inside" (`MainWindow.ShellRegions.cs:35-36`), and the owner confirms the rule in the PR. Its spec reads CB-22's (`specs/cb22_activate_at_cursor_and_preview_embed_are_unavailable_in_reading_mode.md`): CB-22 closes an open popover when a note leaves the editor, and once the popover is modal, Ctrl+Shift+E does not run while it is open (the lead's note, 2026-11-21) | #1665 | M–L | windows (gate) | NVDA spot | CD27 (two steps) |
| CB-39 | A Reveal in File Tree into an unlisted folder seats when the folder lists, and the newest intent wins | #1670 | M | windows (gate) | NVDA spot | — |
| CB-40 | A publication keeps the tree's rows while a list's row is selected, and follows a reader on an unselected row; design first | #1669 | M–L | windows (gate) | NVDA full | CD28 |
| CB-41 | A pin keeps the pinned row and every row it does not displace; the rest per the `Move` probe | #1668, if CD29's second step allows | S–M | windows | NVDA spot | CD29 |
| CB-42 | The Connections model says what it does not cover, and one lifecycle fact pins production | #1625 | S | windows | — | CD30 |
| CB-43 | The next golden image arms crash dumps and bakes the pinned SDK; the app job collects the dumps | Refs #1903 #1614 | M | runner, windows | — | CD31 CD32 |
| CB-44 | Every job that sets up .NET runs the runtime the pinned SDK bundles | #1614 | M | windows, runner, pilot | — | CD33 |
| CB-45 | A reviewed design for reads that do not wait behind a scan: a read-only second connection in WAL mode, phased from Quick Open; it cuts the chunks that build it, and the owner reads it before CB-45b starts. Its PR gets the red team and Codex, though it is documents only (the brief names Codex for this design; the plan lead's call, 2026-11-16). Its phasing is what keeps CB-45b inside one reviewable PR | Refs #1718 | S (docs) | none | — | CD34 |
| CB-45b | Quick Open's reads do not wait behind a scan. This is the first implementation phase of CB-45's design (the phasing is the plan lead's call, 2026-11-16); the design cuts the phases after it. By the design (`specs/cb45_reads_do_not_wait_behind_a_scan.md`, #2042): it changes no FFI; it edits `windows.yml` and `Workflows.Tests.ps1` (the weak-volume job's session step and its pin) and `QuickSwitcherViewModel.cs` (Call 9's re-rank selection); it appends one AR row and one addendum after R-9 to doc 40; and its read connection opens through `db.rs`'s one configure function and joins the connections fact | Refs #1718 | M (L until 2026-11-22, by CB-45's design) | rust, windows, swift, runner | NVDA spot, with the design's section 5 sequences | CD34 |
| CB-45c | The Files tree, the directory pages and the tag tree read through CB-45's read connection, so they do not wait behind a scan; so does the Windows `ShowTags` call, which today freezes the window for the rest of a scan (#2039), and `CopyWikilink` does only if `wikilink_for_path` moves; the Canvas vault file picker's listing, deferred here by CB-45, is checked with them. The second phase of CB-45's design, added 2026-11-22 (#2042); it brings its own owner call for what its surfaces speak during a rescan | Refs #1718 | M–L | rust, windows, swift | NVDA spot, and the VoiceOver check for #1718 (§8) | CD34 |
| CB-45d | Search and the sidebar filter read through CB-45's read connection. The third and last phase of CB-45's design, added 2026-11-22 (#2042), with its own owner call for what its surfaces speak during a rescan; it joins the `lib.rs` row only if `filter_files` gains a cancellation token across the FFI | #1718 | M | rust, windows, swift | NVDA spot | CD34 |
| CB-46 | The deferred finds are fixed, in several PRs cut by subsystem; every Windows or core defect filed since 2026-11-13 is fixed or decided; every dated census exception is gone or in the owner's table; the close-out count is recorded | — | M–L | as needed | as needed | the re-count's own table |

### Closure matrix

Only rows where something beyond the chunk's merge must happen.

| Issue | Chunk | Closes or Refs | What else must happen before close |
|---|---|---|---|
| #1788 | CB-7 | closes (CD3) | the pin passes on `main` |
| #1889 | CB-9a, CB-9b | Refs, then closes | CB-9b; the two accepted exceptions (five census rows, CB-9b's row in §5) named in CB-46's table |
| #1890 | CB-15a to CB-15c | Refs, Refs, closes | the census over the whole shell |
| #1866 | CB-17a, CB-17b (two PRs) | Refs, Refs, then closes | CB-17b: every self-disabling button the scrape finds moves the keys first; no accepted exception is left (CD11). Its PR 1 is Refs and its PR 2 closes. Two buttons, Bulk Rename's Preview and the Files pane's Expand loaded folders, stay available while they run, by the owner's answer of 2026-11-21 (§3.1) |
| #1846 | CB-24 | closes by comment (CD16) | the recorded NVDA run |
| #1835 | CB-0 | ended by hand, with a comment (CD40) | done on 2026-11-16 (§12) |
| #1694 | CB-38 | closes by decision (CD26) | the contract line merged |
| #1665 | CB-38b | closes (CD27) | the owner's second answer, in CB-38b's PR: whether an open popover counts as modal while focus is elsewhere |
| #1668 | CB-41 | closes, or stays open (CD29) | the probe's result and the owner's second answer |
| #1330 | CB-21 | closes, or closes with the record (CD13) | the diagnosis |
| every issue §8's ledger lists, except #1718 | their chunks | close | each one's ledger line; #1718 has its own row below and stays open until its last phase |
| #1727 | CB-13 | closes | the `rust-weak-volumes` job green with its skipped fact running |
| #1903 | CB-43 | Refs, then closes | the golden rebuild and the home-VM probe (CD31) |
| #1614 | CB-43, CB-44 | Refs, then closes at CB-44 (CD33) | the rebuild stays on the owner's steps |
| #1718 | CB-45, CB-45b, CB-45c, then CB-45d (the three phases CB-45's design cuts, #2042) | Refs until CB-45d, which closes (CD34) | the owner has read the design (done on 2026-11-22, with its two confirmations, §3.1); CB-45d has merged |
| #940 #1323 #1389 | CB-0 | by CD36 | the owner's answer; CB-0 makes the edits |
| #1842 #1848 | CB-0 | stay open | Milestone W |
| #1733 | CB-0 | stays open | plan 43 |

## 6. Delivery order

Up to two principal engineers work a wave, each directing up to three
workers, and never more than eight agents run at once (from 2026-11-15,
by the owner's instruction; it replaces "at most three agents at once").
Chunks in one wave share no files. The
decision table was answered on 2026-11-16 (§3.1); the Gate column names
the rows each wave relies on. Each chunk is re-checked against `main`
when it starts, and the slotting of waves C3 onward is confirmed when
each wave's specs are written.

| Wave | Slot 1 | Slot 2 | Slot 3 | Gate |
|---|---|---|---|---|
| C0 | CB-0 | — | — | CD8 CD13 CD35 CD36 CD37 CD38 CD40 |
| C1 | CB-1 | CB-2 | CB-4 | CD2 |
| C2 | CB-3, then CB-5 | CB-6 | CB-8 | CD4 CD41 |
| C3 | CB-7 | CB-9a | CB-10 | CD3 CD5 |
| C4 | CB-13 | CB-27 | — | CD7 CD18 |
| C5 | CB-11, then CB-9b | CB-12 | CB-19 | CD5 CD6 |
| C6 | CB-14 | CB-30 | CB-25 | CD8 CD21 |
| C7 | CB-15a | CB-28 | CB-24 | CD9 CD16 CD19 |
| C8 | CB-31 | CB-15b | CB-29 | CD9 CD20 |
| C9 | CB-15c | CB-17a | CB-32 | CD9 CD11 CD22 |
| C10 | CB-17b, then CB-22b | CB-33 | CB-22 | CD11 CD14; CB-22b's letters, in its PR |
| C11 | CB-20 | CB-16 | CB-45 | CD10 CD12 CD34 |
| C12 | CB-23 | CB-34 | CB-42 | CD15 CD23 CD30 |
| C13 | CB-21 | CB-36 | CB-35 | CD13 CD24 |
| C14 | CB-39 | CB-20b, then CB-38 | — | CD12 CD26 |
| C15 | CB-38b | CB-26 | CB-37 | CD17 CD25 CD27 |
| C16 | CB-40 | CB-43 | CB-45b | CD28 CD31 CD32 CD34 |
| C17 | CB-41 | CB-44 | — | CD29 CD33 |
| C18 | CB-18 | — | — | — |
| C19 | CB-46 (several PRs) | | | the re-count's table |

Waves C11 to C19 were re-cut on 2026-11-16 for the owner's answers
(§3.1); C0 to C10 did not move. Seven chunks now change
`crates/slate-uniffi/src/lib.rs` (CB-14, CB-28, CB-16, CB-23, CB-20b,
CB-26, CB-45b), and no wave holds two of them. Seven chunks from C11
on edit the `MainWindow` files, one a wave, and CB-41 sits between CB-40
and CB-18. Twenty waves is one valid slotting of that, not a count any
one chunk forces, and the count is re-cut when each wave's specs are
written. CB-45 is documents only, so it takes
the first free slot from C3 on, which is C11's. CB-45b follows it in
C16, after CB-33 and after the owner has read the design. The phases the
design cuts after CB-45b take free slots from C17 on, under the same
rules.

Corrected on 2026-11-26 for waves C10 to C13: the `lib.rs` list above
is no longer the one in the shared-file row below. CB-45b changes no
FFI (CB-45's Call 3) and leaves it. CB-17b's first PR joined it with one
event, `WorkStillRunning`, and CB-34 with two doc comments, as CB-28 and
CB-32 each did with one. The chunks that edit `lib.rs` are now CB-14,
CB-28, CB-32, CB-17b, CB-23, CB-34 and CB-16 (all merged), then CB-20b
and CB-26, and CB-45d only if `filter_files` gains a token across the
FFI. No wave held two of them.

The table was checked wave by wave on 2026-11-16 against the shared-file
table below: no two chunks of a wave appear together in a row. The two
exceptions are the ones the rows allow. CB-3 and CB-5 share
`Workflows.Tests.ps1` and run in turn in one slot. CB-21 and CB-36 both
add gate journeys, in different partial files. A third is conditional:
if CB-26's design adds a gate journey, it shares C15 with CB-38b's
popover journey, in a different partial file. The check was run again
the same day after CB-17b's group files and the conditional `a11y.rs`
members joined the rows below, with the same result. As before, the
slotting of waves C3 onward is confirmed when each wave's specs are
written.

Waves C4 and C5 were re-cut on 2026-11-17. CB-9b left C4 because it
and CB-13 both edit `TryRenameSelected` in
`FilesSidebarViewModel.FileManagement.cs`. In C5 it shares
`WorkspaceViewModel*.cs` with CB-11 and runs after it in one slot, a
fifth allowed pair. It also stands in the `session.rs` row with CB-12,
in another slot: if CB-9b's spec edits `session.rs`, CB-9b branches only
after CB-12 has merged, and if it does not, the row is corrected. It
did, and branched from CB-12's merge.

Wave C3 was checked again on 2026-11-16, when the specs for CB-9a and
CB-10 were written. CB-7 shares no file with either: it edits the gate
project and `TestWaitCensus.cs`, and its spec says it does not edit
`windows.yml`. CB-9a and CB-10 are a fourth allowed same-wave pair:
each adds one addendum to contract 40, CB-10 after R-5 and CB-9a after
R-9, in different rows of one document. The second to merge rebases and
keeps both. CB-10 moves no `A11yEvent` construction, so contract 38's
ledger is CB-9a's alone in this wave. CB-9a's spec edits neither
`VaultLifecycleViewModel.cs` nor `Panels/BulkRenameViewModel.cs`, and
CB-10's edits `OpenVaultAsync` in the first; the rows below say so.

Wave C10 was changed twice on 2026-11-21. It started early: Principal
V's slot freed when CB-15c merged, so CB-22 and then CB-33 started
while CB-32 and CB-17a were still in wave C9. The files CB-22 shares
were free once CB-15c had merged, and CB-33's code waited for CB-32's
merge, as the `session.rs` row below asks. And the wave gained a chunk,
CB-22b, by the lead's second ruling on CB-22's Call 8 (§12, Waves C9
and C10). CB-22b runs after CB-17b in one slot, a sixth allowed pair,
because both edit `MainWindow.xaml`; it takes the file before CB-16
does in C11. Its letters go to the owner as one table, and its slot is
confirmed when its spec is written.

Wave C11 opened on 2026-11-21, when Principal X's slot freed at CB-33's
merge and while CB-17b was still in wave C10. Principal Z has CB-45,
the design, which is documents only and Refs #1718, so it shares no
file with CB-17b. The wave's other two chunks start after CB-17b
merges: CB-16 waits for it, as "Why this order" says, and CB-20 is
placed after it because both edit the gate
(`ShellAccessibilityTests*.cs`, below).

Waves C10 to C13 ran as follows, from 2026-11-21 to 26 (§12, Waves C10
to C13). Each principal took the next chunk when its slot freed, so the
waves overlapped. CB-17b's first PR edited `crates/slate-uniffi/src/lib.rs`
and `a11y.rs` (one event, `WorkStillRunning`), so it took a slot in both
rows ahead of CB-16. CB-22b ran after CB-17b's second PR, as planned, in
a slot of its own (Principal R). CB-20 started on 2026-11-22, in the
slot CB-45 freed; it shares no file with CB-17b's first PR. Wave C12
started early, on 2026-11-22, in the slot CB-20 freed: Principal Q took
CB-42 and then CB-23, and Principal T took CB-34 in the same slot on
2026-11-23. CB-16, a wave C11 chunk, started on 2026-11-24, after
CB-22b's merge freed `MainWindow.xaml`. Its edit of `lib.rs` waited
for CB-34's merge, since the row allows one editor at a time; the lead
relaxed the order so that it branched and wrote everything else first.
Wave C13 started on 2026-11-25, after CB-16's merge: Principal P took
CB-36's two PRs, and Principal O took CB-21 and then CB-35 in one slot.
Their gate edits are in different partial files, as the row below
allows: CB-36's journey in `ShellAccessibilityTests.CanvasBoard.cs`, and
CB-21's edit, with no new journey, in
`ShellAccessibilityTests.ReadingList.cs`.

Wave C14 started on 2026-11-26 at about 21:10 Eastern, while CB-35 was
still open: Principal Q took CB-20b and then CB-38 in one slot, with
CB-38's branch waiting for CB-35's merge, since CB-35 edits
`MainWindow.xaml` and `28_palette_contracts.md` and CB-38 may edit both;
and Principal O took
CB-39 when CB-35 merged. CB-20b needs no gate journey (the lead's ruling
on its spec, 2026-11-26), and the gate row below names none for it.

Three chunks were added in these waves, and none is placed in a wave
yet. CB-45c and CB-45d take free slots from C17 on, each after the one
before it, as this section's first paragraph says for the phases after
CB-45b. CB-35b (#2070) takes a later free slot; a settle in
`AccessibleDataGrid.Bind` reaches every grid host, the reading view's
tables among them (#2070). CB-45b stays in C16, where it now shares
`windows.yml` and `Workflows.Tests.ps1` with CB-43: the two run in turn
in one slot, or one moves a wave, when C16's specs are written (CB-45's
design, #2042).

Why this order:

- **C0 is a morning,** and every later chunk works under its rule. Its
  `AGENTS.md` PR relies on no row; two of its comments rely on CD8 and
  CD13, its issue edits on CD35 and CD36, and #1835's close on CD40.
- **C1 and C2 make reruns rare** before the larger chunks start paying
  for lane time. #1928 has already removed the most frequent red on
  `main`.
- **C3 to C6 protect a note's bytes and the index:** the save chain, the
  quarantine, the repair's event, the rename that lies.
- **C6 to C16 are reader-facing,** with at most one full NVDA pass in a
  wave (CB-24, CB-17a, CB-20, CB-34, CB-26, CB-40).
- **CB-16 follows CB-17b,** because it adds a Cancel button of the class
  that sweep closes. **CB-18 follows CB-41,** because its fix lands in
  the publication code CB-40 redesigns.
- **CB-22b follows CB-17b,** because its sixteen access keys are in
  `MainWindow.xaml`, which CB-17b holds for the sheets (added
  2026-11-21).
- **CB-45c follows CB-45b, and CB-45d follows CB-45c,** because each
  moves more reads onto the read connection CB-45b opens, and all three
  edit `session.rs`. **CB-35b follows CB-35,** whose census holds a row
  for each of its members (both added 2026-11-26).
- **CB-20b follows CB-20,** and takes the `a11y.rs` corpus slot after
  CB-16's. **CB-37 follows CB-36,** because it edits the canvas peer
  tests CB-36 changes. **CB-38b follows CB-38** by one wave, because
  both edit the palette contract and `MainWindow` files. **CB-45b
  follows CB-45 and CB-33,** because CB-33 edits the same `session.rs`
  statements.
- **C16 and C17 hold the chunks that wait longest on the owner's
  hands:** the golden image and the runtime. The scan-lock design
  (CB-45) moved to C11, so the owner can read it before its first phase
  starts.
- **Code-area chunks fill free slots from C2 on,** ahead of the later
  reader-facing sweeps. This departs from the brief's order (sweeps, then
  code areas) and is CD39.

Sequenced on purpose, because they share files:

| Shared code | Order |
|---|---|
| `AGENTS.md` | CB-0, then CB-3 (one line beside the parity-matrix gotcha) |
| `.github/workflows/windows.yml` | CB-2 (model step), CB-3 (app job), CB-13 (the weak-volume job's skip), #2052 (not a chunk, merged: the app job's time limit from 60 to 80 minutes), CB-43 (the dump steps), CB-45b (the weak-volume job's session step; it runs in turn with CB-43, §6's paragraph on waves C10 to C13), CB-44 (the setup steps) |
| `Workflows.Tests.ps1` | CB-2, CB-3, CB-5, CB-13, CB-43, CB-45b (the weak-volume job's pin), CB-44; each rebases and reruns the suite, never hand-merging |
| `38_notification_dispatcher_contracts.md` (the generated ledger, a lane gate after CB-3) | every chunk that adds or moves an `A11yEvent` construction, CB-9a among them (it moves up to four construction sites); same-wave pairs rebase and rerun `--write`, never hand-merging |
| `TestWaitCensus.cs` | CB-4, CB-6, CB-7 |
| `Censuses/SaveChainAdmissionCensus.cs` | CB-9a creates it; CB-9b deletes its rows. From CB-9a's merge, any chunk that adds a first Windows call to a `VaultSession` method classifies it there |
| `docs/plans/40_nvda_matrix_remediation_contracts.md` (contract 40's R rows) | CB-10 (an addendum after R-5) and CB-9a (an addendum after R-9), same wave, different rows; the second to merge rebases and keeps both. Then CB-11 (its R-8 addendum, #1997), CB-17b (both PRs, merged), CB-34 (merged), and CB-45b (one AR row, for CB-45's Calls 2 and 10, and one addendum after R-9, for its Call 9). Contract 40 is also in the palette row below, for CB-34, CB-38 and CB-38b |
| `WorkspaceSaveCoordinator.cs` | CB-9a (`AdmittedForTests` and `INoteSaveChain`), then CB-9b |
| `Panels/PanelWorkScheduler.cs` | CB-9a (`StartTrackedWork`), then any chunk that adds panel work; no later chunk is named for it yet |
| `Support/StaThread.cs` | CB-1 only; CB-2's stall dump goes through the runtime's own dump path |
| `ShellAccessibilityTests*.cs` (the gate) | CB-7, CB-19, CB-24, CB-17b (merged: its first PR's Import box journey, in a partial file of its own, `ShellAccessibilityTests.ImportBox.cs`), CB-20 (merged: the main partial's named census, `ShellAccessibilityTests.ReadingCaretMouse.cs` and `ShellAccessibilityTests.ReadingDiagramLines.cs`), CB-34 (merged: `ShellAccessibilityTests.Palette.cs`), CB-21 (merged: `ShellAccessibilityTests.ReadingList.cs`, with no new journey), CB-36 (merged, both PRs: `ShellAccessibilityTests.CanvasBoard.cs`), CB-39, CB-38b (the popover journey), CB-26 (only if its design adds a journey), CB-40, CB-18; never two in a wave that edit the same partial file. CB-20b adds no journey (the lead's ruling on its spec, 2026-11-26) |
| `crates/slate-core/src/session.rs` | CB-8, CB-12, CB-9b (branched from CB-12's merge, 6b098c1e), CB-31 (merged; `index_file` now returns `Result<bool, VaultError>`, `true` when it wrote the path's first row, and CB-32 meets that signature), CB-32 (merged: the batch move's preflight and the rename's candidate scan use the fold; the one-time rebuild is migration 042, in `db.rs`, and did not start here), CB-33 (merged), then CB-45b, CB-45c and CB-45d, each after the one before it |
| `crates/slate-core/src/session/directory_page.rs` | CB-45c (CB-45's design, #2042) |
| `crates/slate-core/src/db.rs` | CB-32 (merged: `tree_sort_key`'s fold and migration 042), then CB-33 (merged: every open goes through one function, `configure_connection`, and a census holds that nothing opens a connection another way), then CB-45b (the reader connection, which opens through that function and joins the connections fact) |
| `a11y.rs`, the corpus and the variant count | CB-14, then #2028 (merged: the constant `SEARCH_QUERY_UNREADABLE` and its test; no event or variant), then CB-17b (merged, its first PR: `WorkStillRunning`, with the corpus count at 240), then CB-16 (merged: the import events), then CB-20b (one event, `DiagramSourceSpoken`, by its spec), then CB-26 (only if folding announces); plan 44's WB-30 takes the next slot. CB-32 took no slot: its rebuild speaks nothing. CB-23's lines are in a module of their own, `empty_panels.rs`, and took no slot |
| `crates/slate-uniffi/src/lib.rs` | CB-14 (a new event), CB-28 (one doc comment), CB-32 (one doc comment, on `backlinks`), CB-17b (merged, its first PR: `WorkStillRunning` and `RunningWork`), CB-23 (merged: the nine panel lines, through `empty_panel_line`), CB-34 (merged: two doc comments, on the palette's order), CB-16 (merged: the import events, `ImportPhase` and `import_status_line`), CB-20b (a new event), CB-26 (the callout), CB-45d (only if `filter_files` gains a token); never two in a wave. CB-45b changes no FFI (CB-45's Call 3), so it is not in this row |
| `diagram.rs`, `EditorInteractions.cs` | CB-27, CB-28, CB-29, then CB-15c and CB-22 (both merged; each edited `EditorInteractions.cs` only) |
| `WorkspaceViewModel*.cs`, `Panels/TasksReviewViewModel.cs` | CB-9a, CB-11, CB-9b, CB-14, CB-15a (one line of `WorkspaceViewModel.Properties.cs`, `EditTagAtItsTurn`, the tag edit's failure detail), CB-15c, CB-22, CB-17b (merged, its first PR: `WorkspaceViewModel.Bases.cs`, `WorkspaceViewModel.Properties.cs` and `Canvas/WorkspaceViewModel.Canvas.cs`), CB-23 (merged: `WorkspaceViewModel.cs`, `WorkspaceViewModel.Layout.cs` and a new `WorkspaceViewModel.EmptyPanels.cs`) |
| `FilesSidebarViewModel*.cs` (`FilesSidebarViewModel.Trash.cs` among them) | CB-13 (one line in `TryRenameSelected`'s catch, merged), then CB-9b (merged), CB-15a, CB-17b (merged, its first PR: the Files and Trash self-disabling buttons), CB-16 (merged), CB-35 (merged: the filter field, in `FilesSidebarViewModel.Filter.cs`), CB-39, CB-40, CB-41, CB-18 |
| `VaultLifecycleViewModel.cs`, `Panels/BulkRenameViewModel.cs`, `Bases/BaseSurfaceView.cs`, `Graph/ConnectionsLeafView.cs`, `Graph/GraphInspectorView.cs` | CB-10 (`OpenVaultAsync` in `VaultLifecycleViewModel.cs` only), then CB-9b, CB-15a, CB-15b and CB-15c (each only where its spec names one of them), then CB-17b (merged, its first PR: the self-disabling groups, in all five files); no other C10 chunk edited them. CB-9a's spec edits none of them. CB-35b's members include the Bases quick filter and the graph inspector's queries, in `Bases/BaseSurfaceView.cs` and `Graph/GraphInspectorView.cs` (#2070) |
| `MainWindow.xaml`, `MainWindow*.cs`, `WorkspaceTemplates.xaml` | CB-19, CB-24 (`MainWindow.AppMenu.cs`, onto the shared resting-pointer hold; `MainWindow.FilesToolbar.cs` and `MainWindow.RailMore.cs`, the two menus found on the way), CB-17a (merged; it edited none of them, since under CD11 it changes no product code), CB-22 (merged: two access keys in the Editor menu of `MainWindow.xaml`, "Actual Si_ze" and "Zoom Ou_t"), CB-17b (merged, both PRs: the buttons, then the sheets and the launcher, `WorkspaceTemplates.xaml` among them), CB-22b (merged: the sixteen other shared access keys in `MainWindow.xaml`), CB-23 (merged), CB-16 (merged: the status bar's Cancel, in a new `MainWindow.StatusBarCancel.cs`), CB-35 (merged), CB-38 (`MainWindow.xaml` for each route its probe shows acting, by its drift check of 2026-11-26), CB-38b, CB-40, CB-18 |
| `focus_integrity_manifest.json` | CB-17a, CB-17b (merged, both PRs), CB-23 (merged: the line's row), CB-16 (merged), CB-18, then any chunk that adds a focus site |
| `Reading/` (`ReadingDocumentBuilder`, `ReadingSurface`, `ReadingContentViewModel`) | CB-28, CB-29 (`ReadingDocumentBuilder`, `ReadingContentViewModel` and `ReadingEmbedArtifact`, merged), CB-15c, CB-20 (merged), CB-21 (merged: `ReadingSurface` and `HeadingStyleText`), CB-20b, CB-26 |
| `links_db.rs` | CB-31 (merged: `heal_rows_naming_first_indexed` replaces `heal_after_create`), then CB-32 (merged: a link row's context is trimmed when it is read). CB-33 did not edit it: its remedy is on the connection, not in the statements |
| `CommandPaletteViewModel.cs`, `28_palette_contracts.md`, contract 40 | CB-34 (merged: `CommandPaletteViewModel.cs`, its results presenter, and contracts 28 and 40), CB-35 (merged: a record in contract 28), then CB-38 (the two contracts only: by its drift check of 2026-11-26 it edits neither `CommandPaletteViewModel.cs` nor `ModalSurfaces.cs`, and it needs `MainWindow.xaml`, in the row above), then CB-38b (with `ModalSurfaces.cs` and its tests). Contract 40's R rows are in a row of their own above, for CB-10 and CB-9a |
| `QuickSwitcherViewModel.cs`, `Search/SearchOverlayViewModel.cs`, `FileManagement/MoveToPickerViewModel.cs`, `Canvas/CanvasPromptViewModel.cs`, `Canvas/CanvasVaultFilePickerViewModel.cs` (added 2026-11-26) | CB-17b (merged, its first PR: `QuickSwitcherViewModel.cs`), CB-35 (merged: each lays out its list in the operation that publishes it, through a new `PublishedList.cs`, and `SearchFieldCensus` holds them), then CB-45b (`QuickSwitcherViewModel.cs`, for CB-45's Call 9) |
| `Canvas/CanvasRendererView.cs`, `CanvasRendererPeers.cs`, `CanvasRendererPeerTests.cs` | CB-36 (merged, both PRs; the second also edited `Canvas/CanvasPeerTopology.cs`), then CB-37 |
| `chords.json`, `ChordTable.cs`, the parity matrix | CB-20b; CB-26, if its design gives folding a chord |
| the golden image (`provision-guest.ps1`, `versions.json`, `Golden.Tests.ps1`) | CB-43 only |

## 7. Verification lanes and the local loop

Plan 44 §7 applies. Per kind of chunk, what its PR body quotes:

- **Tests and censuses** (CB-1, CB-4, CB-6, CB-7, CB-42):
  `dotnet test … --filter` on the touched facts, seen failing first; a
  census's planted-case fact. CB-17b needs one full local run on an
  interactive desktop, because the menu facts skip on the hosted runners.
- **Workflows** (CB-2, CB-3, CB-5, CB-13, CB-43, CB-44): `Invoke-Pester` on the
  runner tests, then the PR's own lane runs by run id, and a probe that
  shows the new check red. `windows.yml` does not run on a branch push,
  so its probe is a `workflow_dispatch` on a scratch branch.
- **Core** (CB-8, CB-9b, CB-12 to CB-14, CB-16, CB-20b, CB-23, CB-26 to
  CB-34, CB-45b): the four Rust gates in PowerShell; the Windows twin
  fact; the swift lane's compile.
- **Host** (the rest): the hosted facts, the shell gate where the chunk
  adds a journey, and the NVDA phrases recorded in the PR body.

## 8. Owner steps, the two issues and the watch list

**Owner steps.** Each is something an agent cannot do.

| What | When | Chunk |
|---|---|---|
| Answer §3 | Done on 2026-11-16 (§3.1) | all |
| The second step of CD12(b), CD20, CD25, CD27 and CD29: read the spike, the calibration table, the diagnosis, the proposed popover rule or the probe, and answer | In that chunk's PR; CD20's was answered on 2026-11-20 (3 s, §3.1), and CD12(b)'s on 2026-11-22 ("Before the source", §3.1) | CB-20 CB-29 CB-37 CB-38b CB-41 |
| Read the short designs for CB-26 and CB-36 (the plan lead's call, 2026-11-16, §3.1), CB-40's design step (CD28) and CB-45's design (CD34) | Before that chunk's code; CB-36's after its scan is seen to fail and before the chip code; CB-45's before CB-45b starts. CB-45's was read, and its two points confirmed, on 2026-11-22; CB-36's was answered on 2026-11-25 and again on 2026-11-26 (§3.1) | CB-26 CB-36 CB-40 CB-45 |
| The ruleset `PUT` from #1639, so #1565 can close | Any time; the week's watch is met | (audit) |
| `/design-sync`, so #1756 can close | Any time | (audit) |
| The four Studio steps for #1564 | Any time | (audit) |
| Two Slate sessions on one vault for the field pass, if the launcher cannot start two | Not needed: CB-12's pass started the second window by hand (#1996) | CB-12 |
| One golden rebuild, then a `home-only` run and #1903's probe | After CB-43 merges (and after CB-44, if it changes what the image needs) | CB-43 |
| The VoiceOver passes and Mac adoptions on the ledger | Plan 43's queue | (ledger) |

**"Mac work owed by plan 47"** is one issue, #1954, opened by CB-0. Lines known
today:

- Speech or text: #1628 and #1629 (math speech and braille); #1660 and
  #1395 (the new toggle event's adoption; the create sentence); #1782
  (snippets); #1834 (section order; the selection rule in
  `CommandPaletteModel.swift`, with the owner's "Silent follows",
  whether hover is a reader's move, `filteredCommands`' score order,
  the Swift twins and a VoiceOver pass, by CB-34's #1954 comment
  7148412227); CB-17b's `WorkStillRunning` (the three still-running
  sentences, with a VoiceOver pass); #1631 (the session-long sentence no longer follows a panic;
  the panic sentence); #1775 (the adoption of the structured callout,
  with its fold; until then the Mac shows what the compile-keeping edit
  gives, CD17); CB-20b (the diagram source event); #1830 (the adoption
  of the new import events, in place of the Mac's own summary and cancel
  sentences, CD10); #1900 (an unreadable note lists, and on the Mac its
  row says a 1969 or 1970 modified date until the file reads again,
  CD41); #1889 (the export's refusal: by reading uniffi 0.32.2's Swift
  error template, VoiceOver would hear "Saved query could not be
  exported: " and the error's type spelling, so the Mac's arm needs
  `humanReadableVaultError`; moved here from the line below by CB-9b's
  Call 6, ruled on 2026-11-17 and recorded on #1954 on 2026-11-18).
- Behaviour, with nothing new to hear: #1683 (one event stops arriving);
  #1704 and #1705 (links that used to stay
  unresolved now resolve; #1705's new fold rebuilds each vault's index
  once on upgrade, on the Mac too, CD22, and the Mac's window pauses
  while it runs; CB-32 made the fold by changing the sort key, so the
  Mac's folder order moves for names that meet at a `ς` or `σ`, while
  Windows, which sorts every level again itself, shows no change).
- Checks: the Mac reading view for #1708's gap; a note on #1527 that the
  Mac's two copies of the save sentence carry #1660's run-on; #1800
  (core owns the empty-panel lines, nine of them by the owner's answer
  of 2026-11-23, and the Mac, which has bodies for those panels in
  `ContentBlockPanels.swift`, uses none of them, CD15; no VoiceOver pass
  is owed, by the lead's ruling on CB-23's Call 5); #1718 (the
  VoiceOver check moves from CB-45b to CB-45c, for the tree, and to
  #1652's follow-up, for Quick Open, since the Mac's Quick Open does not
  use the indexed rank; CB-45's design, #2042, and #1954 comment
  7075132728); #1721 (CB-36's chips changed no core, so no line is owed;
  #2073 records the Mac's divergences, and the Mac's chip cover order is
  #2069); the Mac's Move to sheet filter,
  which misses a sigma spelling that the Windows twin finds
  (`MoveToFolderSheet.swift:428-441`, found by CB-32); #1782's VoiceOver
  pass, since the Mac's Backlinks label speaks the trimmed context; and
  whether APFS treats a `ς` and `σ` pair of names as one, which CB-32's
  collision gate assumes by reading.

The issue's body is the live text of these lines. It was brought into
line with this list on 2026-11-16, and #1889's line moved under "Speech
or text" on 2026-11-18. The lines this list gained on 2026-11-26, from
waves C10 to C13, are comments on #1954 and are not yet in its body.

**"Plan 47 deferred finds"** (#1955) is the second issue CB-0 opens (§4 rule 10).
It starts with the finds §9 gives to CB-46.

**Watch list.**

| Issue | Waits for | Check | Action when it lands |
|---|---|---|---|
| #1628 | a MathCAT release with the Nemeth cells corrected | at each wave start (CD37) | drop the split render; one golden diff |
| #480 #1493 | upstream releases | at each wave start (CD37) | as plan 44 §8 |
| #1560 | a transcriber's verdict | the owner | a core PR per item |

## 9. Left to other plans, and found but not filed

CB-0 posts the status comment the audit drafted for each audited issue.
None closes today on evidence. By CD36 as answered (§3.1), #940 is ended
by hand, and #1389 and #1323 stay open for their Mac halves.

| Owner of what remains | Issues |
|---|---|
| Plan 43's queue (restarted 2026-11-13; needs a Mac) | #1323 (one mac fact) #1370 #1398 #1434 #1470 #1469 #1513 #940 (CD36) #1389 and #1526 (through #1651, CD36) #1484 (through #1653) #1733 |
| Plan 44 WB-30, then plan 43 PR 41 | #1468 |
| The owner | #1560 #1564 #1565 #1756 #746 #747 #750; and #748 #749, which the audit did not read |
| The watch list | #480 #1493 |
| Plan 45 XB-6, its second half | #1596 |

Mac follow-ups that plan 44 filed and no plan 43 row names
(#1651 to #1654, #1662, #1679, #1720) are out of this plan. Two audited
issues close only with them, so CB-0 adds one row to plan 43 §11 that
lists them, and corrects #1653's body by comment.

**Found by the reading, with no issue.** Each has an owner here, so the
next reader does not file it.

| Find | Owner |
|---|---|
| Tab onto a diagram reads its whole source | CB-20b (CD12) |
| The saved-query export overwrites any path | CB-9b (CD5) |
| Drag and drop, and UIA `SetFocus`, under the seal | CB-38 (CD26) |
| Five more search fields with #1837's shape | CB-35 (delivered by #2077; the grid and outline fields are CB-35b, #2070) |
| Quick Open and the sidebar filter miss a final-sigma name | CB-32 (delivered by #2033) |
| `SavedButUnindexed` reaches other leads after the #1890 sweep ("… failed: Saved, but not indexed yet") | CB-14 lists the sites; the fixes are deferred to CB-46 |
| The sidebar filter's and the Bases export's cancel paths, unverified members of #1830's class | CB-16 (delivered by #2061: both filter paths fixed; the Bases export's and Copy as Markdown's silent drop filed as #2056) |
| The golden image's evergreen installers; the floating Python patch | CB-43 (CD32) |
| WB-28 PR 1's two facts that needed an elevated session | Delivered by #1930 |
| The canvas and graph trigger ledgers have no `--check`, and `test_canvas_trigger_ledger.py` runs in no workflow | CB-46, on the deferred list |
| The device stores (`preferences.json`, `recent-vaults.json`) do not retry a refused rename, as the vault stores now do | CB-46, on the deferred list |
| Whether blame's mini dumps lack managed frames | CB-46, on the deferred list |
| The gate's facts, other than the ones CB-7 edits, delete their app log on failure; one launch-and-teardown seam over 56 launch sites would keep it | CB-46, on the deferred list |
| Whether the home host's cache commit should yield to a running slot | The owner, with the home pool (CD31) |
| The catalogue of host-composed sentences core should own | Milestone W (CD38). Three of its families are delivered here by the owner's answers of 2026-11-16: the import sentences (CB-16, CD10), the two search and graph error copies (CB-15b, CD9) and the right-pane placeholder (CB-23, CD15) |
| `ReadingSemantics.MarkEmbed(BlockUIContainer)` is never called, so one arm of the landmark walk never runs | CB-20 removes both, as a fix found on the way |
| A script slot filled by a state change (`x^\rm a`) still falls back to source; one filled by a space (`x^\, 2`) renders as TeX reads it, the space as the superscript | CB-30 leaves `x^\rm a` out, pins both in the golden, and puts `x^\rm a` on the deferred list for CB-46's table |

## 10. How an implementing agent works a chunk

Plan 44 §10 applies: a fresh worktree on a `claude/<chunk-slug>` branch
from `main`; the spec's "Current state" lines read before coding; failing
facts first; registers closed in the same PR; the commit and PR title
wording from `AGENTS.md`; the PR body with `Closes #N`, the facts, the
registers, the field-pass phrases, "Found and fixed here", `not
delivered: <reason>` and the count (§4 rule 8).

A chunk whose cause is unknown (CB-17a, CB-18, CB-21, CB-37)
opens with a diagnosis recorded on its issue before any product code. A
chunk that rests on WPF behaviour nobody has checked (CB-19, CB-36,
CB-41) opens with the failing fact or probe that settles it, with
the fallback named. A chunk with a spike (CB-20) or a design step
(CB-26, CB-36, CB-40, CB-45) opens with that, and the owner reads it
first. CB-26's and CB-36's design steps are the plan lead's call,
2026-11-16 (§3.1). CB-36 has one order: the covering-state scan first
and seen to fail, then the design the owner reads, then the chip code.
The chips land as a second PR of CB-36, so #1838 and #1722 do not wait
on the design.

Specs for the first wave, written 2026-11-13 against c2377210. CB-1's
was rewritten on 2026-11-14, and every line number re-read on
2026-11-15 at 798cdaae. CB-0 to CB-6 were read again at 5b060297
(§2.6). CB-7's spec is re-read against `main` by its principal before
any code. CB-8's spec was written on 2026-11-16, when its wave started.
CB-9a's and CB-10's were written the same day at 80925d72, when wave C3
started:

| Chunk | Spec |
|---|---|
| CB-0 | `specs/cb00_the_rule_the_audit_and_the_close_outs.md` |
| CB-1 | `specs/cb01_a_timeout_says_what_its_heartbeat_saw.md` |
| CB-2 | `specs/cb02_the_model_reports_a_stalled_case.md` |
| CB-3 | `specs/cb03_gate_binaries_keep_a_week_and_the_ledger_is_checked.md` |
| CB-4 | `specs/cb04_no_fixture_tears_down_under_released_work.md` |
| CB-5 | `specs/cb05_a11y_check_says_when_no_score_was_computed.md` |
| CB-6 | `specs/cb06_unit_facts_wait_on_conditions.md` |
| CB-7 | `specs/cb07_gate_journeys_wait_on_conditions.md` |
| CB-8 | `specs/cb08_a_quarantined_note_still_lists.md` |
| CB-9a | `specs/cb09a_save_chain_admission_census_and_single_path_routes.md` |
| CB-10 | `specs/cb10_a_failed_vault_switch_returns_to_the_welcome_view_once.md` |
| CB-13 | `specs/cb13_a_case_only_rename_on_fat_is_refused_and_says_so.md` |
| CB-27 | `specs/cb27_one_panicking_diagram_does_not_disable_diagrams.md` |
| CB-9b | `specs/cb09b_bulk_and_compound_routes_join_the_note_save_chain.md` |
| CB-11 | `specs/cb11_a_parked_citation_summary_press_follows_its_notes_rename.md` |
| CB-12 | `specs/cb12_a_repair_announces_only_what_it_changed.md` |
| CB-19 | `specs/cb19_every_open_combo_boxs_rows_report_their_place.md` |
| CB-14 | `specs/cb14_a_toggle_that_landed_but_was_not_indexed_says_it_landed.md` |
| CB-25 | `specs/cb25_a_layout_host_drops_a_removed_items_peer.md` |
| CB-30 | `specs/cb30_a_scripted_function_and_three_primes_are_spoken_as_math.md` |
| CB-15a | `specs/cb15a_failure_detail_and_the_files_sidebar_speak_cores_words.md` |
| CB-24 | `specs/cb24_a_resting_pointer_leaves_the_keys_on_a_menus_first_row.md` |
| CB-28 | `specs/cb28_a_timed_out_svg_decode_is_remembered.md` |
| #2016 (not a chunk) | `specs/fix2016_forward_tab_wraps_from_the_windows_last_stop.md` |
| CB-15b | `specs/cb15b_canvas_graph_and_search_speak_cores_words.md` |
| CB-31 | `specs/cb31_a_first_index_heals_the_links_that_name_it.md` |
| invalid-search plain words (not a chunk: owner direction) | `specs/invalid_search_plain_words.md` |
| CB-29 | `specs/cb29_the_svg_guard_refuses_documents_that_decode_slowly.md` |
| CB-15c | `specs/cb15c_panels_reading_workspace_lifecycle_and_commands_speak_cores_words.md` |
| CB-32 | `specs/cb32_a_final_sigma_target_resolves_and_a_snippet_starts_on_a_word.md` |
| CB-17a | `specs/cb17a_one_nvda_session_hears_the_sheets_and_the_self_disabling_buttons.md` |
| CB-22 | `specs/cb22_activate_at_cursor_and_preview_embed_are_unavailable_in_reading_mode.md` |
| CB-33 | `specs/cb33_no_statement_with_an_equality_on_files_path_reprepares_per_binding.md` |
| CB-17b (both PRs) | `specs/cb17b_nothing_holding_the_keys_goes_off.md` |
| CB-45 (the design) | `specs/cb45_reads_do_not_wait_behind_a_scan.md` |
| CB-20 | `specs/cb20_line_reading_speaks_a_diagrams_description.md` |
| CB-42 | `specs/cb42_the_connections_model_says_what_it_does_not_cover.md` |
| CB-23 | `specs/cb23_a_panel_with_no_body_says_what_it_will_show.md` |
| CB-22b | `specs/cb22b_no_two_items_of_one_menu_level_share_an_access_key.md` |
| CB-34 | `specs/cb34_a_typed_command_name_runs_that_command.md` |
| CB-16 | `specs/cb16_a_cancelled_import_says_so_and_the_files_line_never_stays_loading.md` |
| CB-36 (first PR) | `specs/cb36a_the_board_paints_in_scene_order_and_a_first_search_returns_its_item.md` |
| CB-21 | `specs/cb21_narrator_announces_the_reading_views_selection.md` |
| CB-36 (second PR) | `specs/cb36b_each_labelled_edge_has_a_drawn_chip_and_its_bounds_are_the_chips.md` |
| CB-35 | `specs/cb35_search_fields_flush_as_the_palette_does.md` |

CB-13's and CB-27's specs were written on 2026-11-16 at 7c2d45e0 and
each ends with the corrections its delivery made. Wave C5's specs were
written on 2026-11-17: CB-11's, CB-12's and CB-19's cite `main` at
3ae84200 (CB-12's and CB-19's were checked again at eea6e7a2), and
CB-9b's cites eea6e7a2. Each has a step 0 measurement and a red team
before code, and each ends with what its delivery changed. Wave C6's
specs were written on 2026-11-18 and cite `main` at eeb99032, with the
same step 0, red team and closing record. Wave C7's specs were written
on 2026-11-18 and cite `main` at 26d353ef, with the same three parts.
The spec for #2016, a find taken early in that wave, records its step 0
on `main` at 5b890dd9 and ends with what its delivery changed; its red
teams read the diff. Wave C8's chunk specs were written on 2026-11-19
and cite `main` at 16ad788d, with the same three parts; CB-29's also
holds the calibration table the owner picked the limit from, and its
evidence is committed under `47_windows_core_closeout/calibration/cb29/`.
The spec for the invalid-search change, which the owner directed during
that wave, was written on 2026-11-20 at c4d1cd60, with its own step 0,
red team and closing record. Wave C9's specs were written on
2026-11-21 and cite `main` at 47bcf77e, with the same three parts;
CB-17a's step 0 is the scrape, since its NVDA session is its delivery.
The two wave C10 specs delivered so far were written the same day at
7a34cb6e, and CB-33's was read again at 46f50603, after CB-32 merged;
its record of what delivery changed stands near its top, before its
goal. CB-17b's spec was written on 2026-11-21 at 3c0dbe55 and covers
both its PRs; its second PR added it to the repository. CB-45's design
was written on 2026-11-21 at 02fbd37f and finished on 2026-11-22 at
3f2fd733, and its PR is the design. Wave C11's CB-20 spec was written on
2026-11-22 at df1c1147, and CB-16's on 2026-11-24 at ad04a470. Wave
C12's specs for CB-42 and CB-23 were written on 2026-11-22 and re-read
at 68e4f974, and CB-34's on 2026-11-23 at f363a597; CB-22b's, a wave C10
chunk, was written on 2026-11-23 at 38f6edde. Wave C13's specs for
CB-36's first PR, CB-21 and CB-35 were written on 2026-11-25 at
b19c94ae, and the spec for CB-36's second PR on 2026-11-26, on a branch
that held `main` at 01b293b3. Each of these chunk specs has the same
three parts, a step 0,
its red team's record and what delivery changed, and the copies in the
repository say "the principal's working notes" where the principal's
copy named a local folder. Where these specs name probe outputs or
working notes kept outside the repository, those stay local.

The reading notes behind every chunk are in
`47_windows_core_closeout/reading/`: nine reports, one per code area,
with the sweep tables and the audit's drafted comments. A later wave's
spec starts from its report and re-verifies every file-and-line claim
against that day's `main`.

## 11. Risks

- **The plan is larger than the brief guessed:** 53 PRs before CB-46's,
  and more for the phases CB-45's design cuts, not 20 to 25. The sweeps
  found what they were meant to find, and the owner's answers of
  2026-11-16 added the work the lead cut as CB-38b and CB-45b (the plan
  lead's call, 2026-11-16), made five chunks larger (CB-16, CB-23,
  CB-26, CB-32, CB-36) and put more work in CB-15b and CB-17b.
  Twenty waves under one-at-a-time merges is weeks of lane time. The
  first cut, if one is needed, is CD28 and CD29 (the Files tree
  redesign) and CB-35's five extra fields. Since the count of 53 was
  written, CB-17b shipped as two PRs, CB-22b and CB-35b were added,
  and CB-45's design cut two phases after CB-45b, CB-45c and CB-45d
  (corrected on 2026-11-26). CB-35's five extra fields were delivered
  with it (#2077).
- **The forty decisions were answered before any work** (§3.1; CD1 is
  overtaken, and CD41 was added on 2026-11-15). Nine answers depart from
  the recommendation, and each adds work. Five second steps remain
  (CD12(b), CD20, CD25, CD27, CD29).
- **CB-26 is a feature-sized chunk.** The brief's anti-goal is "a
  feature arrives under a defect's name", and a structured, foldable
  callout over the FFI is a feature that #1775's defect did not need.
  It is here by the owner's decision (CD17), recorded as the owner's
  call. Its design step, which is the plan lead's call, 2026-11-16, its
  full NVDA pass and the blind Mac mirror edit are where it can slip.
- **CB-36's drawn chips are also a feature-sized change,** taken on the
  owner's decision (CD24). They change what a sighted user sees on
  every board, and no chip token exists in `design/windows`. CB-36's
  design names the chip's fill and text tokens, reusing the card tokens
  as the Mac does with `CanvasColorPalette.cardFill`, or it brings a
  design-source change to the owner.
- **CB-45b and the phases after it are a large core change added by the
  owner's decision (CD34).** Building it here is the owner's decision;
  cutting it into phases, with CB-45b the first, is the plan lead's
  call, 2026-11-16. The reading sized the whole of it L and
  kept it out of this plan: it changes the session's connection model,
  with the write-intent protocol and the page-cache guards (#1909,
  #1926) in reach, and it shares `session.rs` with six other chunks.
  The number of phases is not known until the design is read, so the
  PR count and the wave count above are floors. The design, read by the
  owner on 2026-11-22, cuts three: CB-45b (Quick Open), CB-45c (the
  Files tree, the directory pages and the tag tree) and CB-45d (search
  and the sidebar filter, which ends #1718).
- **Seven chunks now change the FFI,** one a wave (§6). A slip in one
  holds the next, and each needs both bindings regenerated and the
  Mac's consumers kept compiling with no Mac to run them. Corrected on
  2026-11-26: CB-45b changes no FFI, and CB-17b's first PR added one
  event, so the chunks with an FFI change, doc comments aside, are six:
  CB-14, CB-17b, CB-23 and CB-16 (merged), then CB-20b and CB-26.
  CB-45d makes seven if `filter_files` gains a token (§6's `lib.rs`
  row).
- **CB-32 rebuilds every vault's index once on upgrade (CD22).** On a
  large vault that is a visible wait at first open. Its spec has to say
  what the user sees and hears while it runs.
- **CB-38b was sized from the reading, not the code.** The owner
  decided that the popover joins the modal surface (CD27); the lead cut
  it as CB-38b. The reading put the full answer at M to L: eight
  admission tables, about 35 read sites and four censuses. It is sized
  again when its spec is written, and one question goes back to the
  owner in its PR (§3.1).
- **Inline fixes can swell a PR.** Rule 2's cap and the red team's read
  are the guard; rule 8's count shows it happening.
- **Three issues close on inference, not replay** (#1788, #1843, and
  #1846 on one confirming run). The owner accepted all three (CD2, CD3,
  CD16). If #1788's pin fails or #1846's run does not confirm it, that
  issue stays open with a watch, and CB-46's table carries it.
- **Core changes with no Mac to listen.** About twenty ledger lines so
  far. The owner's answers of 2026-11-16 grew the two largest: the
  import events (CD10) and the structured callout (CD17). Plan 43's
  queue restarted on 2026-11-13, which is what clears it.
- **CB-17b is the #1867 problem fourteen times.** That change took eight
  Codex rounds for three overlays. A sheet whose first stop is not ready
  in the opening turn needs the fallback, and each is a place for a
  regression in Escape, typing or Invoke.
- **CB-40 is the code that stopped WB-6 after three review rounds.** It
  has a design step and a Codex review for that reason; do not size it
  from the diff.
- **CB-9b's many-path admission can hang Close Vault** if an admitted
  apply is not cancelled before teardown settles. #1888's first two
  review rounds found the same symptom by another cause: a ticket left
  open on a fault (a worker exception outside its catch, then a refusal
  whose sentence throws), so a settle never returned. That half holds
  for every admitted route, CB-9a's among them. The cancel before the
  settle is CB-9b's alone (corrected on 2026-11-16 from #1888's PR
  body).
- **CB-28 reverses a fact pinned two days before the baseline.** If the
  owner prefers the retry, the memory needs an expiry and a clock seam.
- **CB-43 cannot be finished by an agent:** it needs the owner's rebuild,
  and the image was rebuilt on 2026-11-14 without it.
- **CB-44 moves CI's runtime backwards,** below what developers run. That
  is the cooldown working as written; the owner should see it first.
- **The censuses are ratchets with allow-lists.** If an entry is easy to
  add without a reason, it will be added. Each census requires a reason
  and fails on a stale entry, and CB-46 puts every exception left to the
  owner.
- **CB-3's ledger check will catch Mac contributors,** who cannot run
  the generator. The printed diff and the `AGENTS.md` line are the
  mitigation.
- **The first-wave specs were written by eight agents in parallel.**
  Codex's second round read them with the plan and found seven places
  where a spec disagreed with it; expect a spec written later to need
  the same check against the plan before its chunk starts.

## 12. Delivery record

One row per chunk as it merges. Rows are batched into each wave's docs
PR, so a row can trail its merge by a wave. The count is rule 8's, as
the chunk's PR body ended, unless the wave's record names a find added
after the merge. "Owed" is what the merge left for someone else: an
owner's call, a second answer, a rebuild or a ledger line.

| Chunk | PR | Merged | Closed | Found / fixed here / deferred / filed | Owed |
|---|---|---|---|---|---|
| CB-0 | #1957 | 2026-11-16 (97b94ddc) | #1835, #940 | 2 / 1 / 1 / 0 | One line on #1955 (below). The owner's call on the subsystem definition's two gaps (below) |
| CB-4 | #1960 | 2026-11-16 (01b851d4) | #1673 | 4 / 3 / 1 / 0 | One line on #1955: a math element's drawing template is tied to the first thread that touches it. It was delivered by #1965 (merged as 5afe7455; the math template row below) |
| CB-1 | #1961 | 2026-11-16 (fcc7b5f6) | #1843 (CD2) | 7 / 3 / 3 / 1 | Three lines on #1955. Filed #1959 (the licence-header script rewrites line endings). CB-6's sleep rules list `Support/Heartbeat.cs` and the planted sleeps in `StaThreadTests` |
| CB-2 | #1962 | 2026-11-16 (b62d5729) | #1624 | 5 / 2 / 2 / 1 | Filed #1963 (a core property test drew the reserved name `aux`), which CB-8's PR takes. One new line on #1955 (the pilot workflow's model step inherits the stall rule with no dump variables) and one already on its opening list (the blame dump type) |
| CB-8 | #1966 | 2026-11-16 (70e97ba0) | #1900, #1709, #1963 | 1 / 1 / 0 / 0 | Nothing deferred. `not delivered: VoiceOver pass` on #1954: on the Mac a quarantined row says a 1969 or 1970 modified date (CD41). Filed for Milestone W as CD41 said: #1964, a marked row and the other surfaces that read the sentinel |
| math template (not a chunk: CB-4's deferred find, taken early) | #1965 | 2026-11-16 (5afe7455) | none (Refs #1955) | 0 / 0 / 0 / 0 | Nothing. A user cannot hit it: the app builds reading documents on its one UI thread |
| CB-3 | #1968 | 2026-11-16 (7c2b2655) | #1647, #1699 | 5 / 4 / 1 / 0 | One line on #1955: the canvas and graph trigger ledgers have no `--check`, and no workflow runs `test_canvas_trigger_ledger.py`. Not verified on a lane: a census-tier run of the ledger step, and a gate-only rerun after the first day |
| CB-5 | #1970 | 2026-11-16 (80925d72) | #1616 (CD4: rewrite) | 5 / 4 / 1 / 0 | One line on #1955: the computed-score comment prints `grade`, `criterion` and `name` unescaped |
| CB-6 | #1971 | 2026-11-16 (ecf7f551) | none (the class) | 5 / 0 / 5 / 0 | Five lines on #1955: three product seams the remaining waits need, one `HotExitTests` fact whose count cannot see a second claim, and the pinned sites Design 1 may still reach |
| CB-10 | #1972 | 2026-11-16 (48df4543) | #1730 | 2 / 1 / 1 / 0 | One line on #1955. The owner's ear on the failed first open, which is left as built. NVDA phrases are in the PR body |
| CB-27 | #1978 | 2026-11-17 (b127a84a) | #1631 | 3 / 0 / 2 / 1 | Two lines on #1955 (the "unsupported" classification; a sequence diagram's name counts `participant` lines as interactions). Filed #1979. VoiceOver on #1954. NVDA phrases are in the PR body; the diagram key is Ctrl+Alt+D |
| #1975 (not a chunk: found by CB-13's step 0, merged first) | #1982 | 2026-11-17 (c84a08ef) | #1975 | 11 / 1 / 9 / 1 | Seven deferred lines and two possible flakes on #1955. Filed #1981 (a stopped rename's recovery loses the history binding). Nothing is notified for the alias path. The alias-row case is pinned in core only. A Mac check on #1954 |
| #1979 (not a chunk: the math hook race that turned CB-27's lane red) | #1984 | 2026-11-17 (7d948eb3) | #1979 | 0 / 0 / 0 / 0 | Not delivered: the swap-first order under a forced interleaving, a pin on the lock's release, a census that every fact swaps through the helper |
| CB-9a | #1983 | 2026-11-17 (3d56978f) | none (Refs #1889, open for CB-9b) | 11 / 5 / 5 / 1 | CB-9b ends four census rows (`BulkRenameViewModel.Run`, `FilesSidebarViewModel.EditTag`, `ExportSavedQueryAsBase`, the bulk-rename caller of `ReconcileTabsAfterPropertyWrite`). Filed #1980 (a tabless Bases cell edit names a path), pinned by a fact. Five lines on #1955. Not delivered: an NVDA hearing of the raced-tab sentence. The owner's ear on that sentence's new occasion. Delivered by #2001 (eeb99032). |
| CB-7 | #1986 | 2026-11-17 (3ae84200) | #1788 (CD3) | 6 / 0 / 4 / 2 | Filed #1974 (every Windows writer makes the classic replacing rename; the spec's mechanism was wrong) and #1976 (the app menu pointer fact). Four lines on #1955. Codoki was bypassed on an independent judge's verdict: its one finding, that xunit has no `Assert.Fail`, is false. Not delivered: a failing test for #1788's own run. Three local reds never reproduced and never seen on a lane |
| CB-13 | #1985 | 2026-11-17 (0a417b84) | #1727 | 7 / 1 / 4 / 2 | Filed #1975 (merged first, above) and #1977 (an outside edit made just before a rename is dropped as the rename's own echo). Three lines and a possible flake on #1955. A refusal records no echo. Run 43943403456: the read with the renamed handle still open equals the folder's listing in all twelve cases, and the watcher hears no event for either spelling around an ignored rename. Not delivered: an NVDA hearing on a real FAT32 or exFAT volume (an owner step); the read-back on an SMB share on the lane (it passed locally on a loopback share over NTFS); either read after a rename a FAT volume applied; the mixed folder outcome. The Mac's twin on #1954 |
| CB-12 | #1996 | 2026-11-17 (6b098c1e) | #1683 (CD6) | 8 / 1 / 6 / 1 | Six lines on #1955: the Modified sibling, the launcher's second instance, the generation across sessions, the destination's re-read under a stale graph at `graph.rs:841`, the release DLL's reproducibility, and `delete_file`'s staging. Filed #1995 (a field pass started by the launcher without `-Root` runs with no hot-exit store). The Mac's line on #1954. NVDA two-session phrases for base and head are in the PR body. Not delivered: a VoiceOver or Mac run |
| CB-11 | #1997 | 2026-11-18 (6351c82b) | #1714 | 15 / 9 / 1 / 5 | Filed #1988, #1989, #1990, #1993 and #1994 (more shapes added as comments on #1990, #1993 and #1994). A line on #1955: NVDA cuts off the leaf's count under the sheet's sentence when both arrive in one turn. Not delivered: another session's rename read before this window's rescan (#1990); a rename made outside Slate. The read-window exposure (another session replacing the file between the check and the load's read) was declined on the lead's call and recorded on #1990. NVDA ran on 2d45fbea, before the review fixes. No Mac work owed |
| CB-19 | #1998 | 2026-11-18 (69380c4e) | #1783, #1836 | 5 / 2 / 3 / 0 | Three lines on #1955: the stale `ChordTable.cs` sort-combo comment, a possible `OpenRailPanel` flake, and key lag under NVDA during an import on `main`. NVDA phrases for base and head are in the PR body. NVDA speaks no position on the popup copy on either build, so the gate scans and UIA reads are the evidence. Not delivered: a fact for the tracker's attach guard, and a red run for the stricter still-open check |
| CB-9b | #2001 | 2026-11-18 (eeb99032) | #1889 | 21 / 12 / 5 / 4 | Filed #1991 and #1992 (from the spec), #1999 (the reading view's notice, ruled in at three new occasions) and #2000 (a throwing announcement in a posted publication ends the app). Lines on #1955: NVDA's #748 cancel in phrase 4, and a structured per-file failure kind to replace the `SavedButUnindexed` text match; the CB-9a items 1 and 3 clearing comment. Comments on #1866 (Apply moves the keys out of the sheet) and #1995. The undo defect (data loss on `main`) is fixed for every property write, the bulk rename, both Bases cell routes, the tag edit and the link rewrite. Census: four rows ended; five accepted rows (four link-rewrite members and `PublishBasesExport`) left for CB-46's table, beside one standing row (`ReplaceTabItem`, not this class). Not delivered: a VoiceOver pass (a #1954 line, owed the Mac fix); the apply's summary on the application close (NVDA drops it at exit, as on `main`); a fact that drives a vault switch. NVDA ran on ba0c1b64, before the Codex-round fixes, which add no sentence |
| CB-25 | #2009 | 2026-11-18 (4b700172) | #1860 | 0 / 0 / 0 / 0 | Nothing. Fact 2 and the counter-fact ran on CI and passed (run 44314910379). Not delivered: an AT pass (AT none in the plan row) |
| CB-30 | #2007 | 2026-11-18 (26d353ef) | #1629, #1628 (CD21) | 6 / 4 / 1 / 1 | Filed #2006. VoiceOver pass (#1954); upstream drafts await the owner; `x^\rm a` on #1955; Nemeth for a glyph in a superscript or after no carrier waits on MathCAT's cell (§8's #1628 watch row); §9 row corrected here |
| CB-14 | #2008 | 2026-11-18 (834cbe31) | #1660 #1395 (CD8) | 15 / 10 / 2 / 3 | VoiceOver pass and the Mac's adoption of TaskToggledUnindexed (#1954, with the #1527 note); the other SavedButUnindexed sites and core defects A and B (#1955, one comment, for CB-46); #2003, #2004, #2005 filed; the third rename residual goes into contract 38 through the C6 docs PR |
| CB-28 | #2011 | 2026-11-18 (9999cb68) | #1635 (CD19) | 5 / 2 / 3 / 0 | #1954: the Mac embed view decodes SVGs with no time budget (EmbedView.swift:161, by reading; needs a Mac). #1955: a deadline for the diagram stage's decodes; a bounded cache of successful decodes. Owner: departure 3, where slow successes spend the 20 s deadline at every refresh, and whether the diagram stage gets a deadline |
| CB-24 | #2014 | 2026-11-18 (5b890dd9) | #1849; #1846 by comment (CD16) | 4 / 3 / 0 / 1 | Filed #2013: a context menu WPF's service opens from the keys lands no row, and a resting pointer gives the keys to the row under it (in the editor, Shift+F10 3 of 3 and the Apps key 1 of 1). The judge's two accepted minors: the two close-fade hosted facts pass vacuously where menus do not fade, and the gate grew (CI ran it in 25 min 25 s of its 35-minute limit). Not delivered: a run at 100 % scale (CI's DPI and animation setting are unknown; red on main was seen only at 200 % with animations on), touch and pen, a real mouse right-click during a close fade, and Shift+F10 on a row of the open menu. No Mac work owed |
| CB-15a | #2015 | 2026-11-19 (cc16f7e3) | none (Refs #1890, open for CB-15b and CB-15c; CD9) | 8 / 5 / 1 / 1 | Filed #2012: a folder listing fails when another session commits during it (core refuses even a first page and the host never restarts), and some of core's details read as developer text. One line on #1955: the Copy link failure is spoken at High on Windows and at Medium on the Mac. One line on #1956: the Copy link sentence, which is host-composed and is the Mac's, plus leads that repeat the path an InvalidPath detail names. For the owner: whether Windows adopts the Mac's specific unavailable reason, "Select exactly one Markdown file to copy its wikilink."; today the palette gives its generic "This command is not available right now.". CB-15b and CB-15c inherit ShellFailureDetailCensus's 61 dated rows (73 reads) and its baseline. Not delivered: the synchronous tree refresh path has no fact (a path only tests take; the census holds it); the Markdown refusal and the disabled buttons after a header selection were not heard under NVDA; the other three uniffi error types and an AggregateException still dump their fields if they ever reach the helper. No Mac work owed |
| #2016 (not a chunk: found by CB-15a's NVDA run, taken early) | #2017 | 2026-11-19 (57b9fbbb) | #2016 | 5 / 4 / 1 / 0 | One line on #1955 (comment 7001021616): with the palette open, an external activation request leaves the keys on the bare window; main does the same. Intended changes the lead ruled: with a canvas open (seen in the test suite's window only), the logo, the content pane's border and the rail's two ends keep their arrows (the rail's ends by a Contained group); with no vault open, the logo keeps its arrows and the workspace chords; on the welcome view with a recent vault, Shift+Tab from the logo lands on the last recent vault. Not delivered: Narrator and VoiceOver passes; CI runs of the 15 logo facts, which skip on hosted runners under the #1680 guard (the two test classes' local Release run, 43 passed, 0 skipped). No Mac work owed |
| CB-15b | #2020 | 2026-11-19 (15cba369) | none (Refs #1890, open for CB-15c; CD9) | 7 / 1 / 0 / 3 | Filed #2019 (a canvas that never opened says "The previous snapshot is read-only." after a rename), #2021 (a launch is silent at random under NVDA: launch lines raised before the window's focus event are dropped; two main launches silent, one head launch spoken; five more launches could not take the foreground) and #2022 (a canvas undo or redo that core refuses says nothing; found by the merge judge, by reading). One comment on #2012 with step 0's developer-text table, including a search column filter classified as a database error. One #1956 comment: the canvas open failure's lead repeats its path, and the graph config's notices are host text the FFI crate already words. One #1954 line: the Mac's search and graph humanReadable arms should adopt core's detail; the #1527 note is posted. Departure: the graph's invalid-query sentence was not heard under NVDA, because no input reaches it (core's graph build fails only with Db) and head has no such sentence; a hosted fact pins it. Not delivered: VoiceOver pass (no Mac speech changes); a fact for CanvasRemoveFromGroup (census-only, a race the lease's lock forbids). For the owner: under CD9 the search's invalid-query sentence reads as developer text; #2020's "For the owner" has the before and after |
| CB-31 | #2024 | 2026-11-20 (c4d1cd60) | #1704 | 3 / 1 / 1 / 1 | #1954: two lines, by reading (a first-indexing repair's Created now refreshes the Mac's template list, comment 7027208954; handleSidebarFileChange ignores .created, so that row's metadata is not refreshed, comment 7027505455). Filed #2023: an open Windows reading view keeps drawing a link unresolved after core heals it, and refuses to follow it. One comment on #1981 (7025682510): a stopped case-only move's recovery leaves the links at the deleted alias spelling. Ruling 4 taken in the PR: a repair that writes a path's first row announces Created, with doc 21's two rules amended. index_file now returns Result<bool, VaultError> (§6 note for CB-32). Not delivered: a Mac run and VoiceOver (nothing spoken changes, by reading); a whole Windows unit run at the merged head (it ran at 71d4f8ab, 7,838 + 3 passed). No NVDA owed |
| invalid-search plain words (owner, 2026-11-20) | #2028 | 2026-11-20 (925f0a42) | none (Refs #2012, #1527; owner direction, rulings 1-8) | 6 / 2 / 1 / 2 | Fixed here: a damaged index classified as a refused query; a damaged schema's SQLITE_CORRUPT read as a refused query (merge judge). Filed #2026, #2027. #1955 line 7036192406 (Bases interpreter path: damaged index a view error, exit 2). Judge comment 7036024550. Not delivered: VoiceOver pass (#1954 7034192987). For the owner: a second refused search in an open overlay is silent (S7) |
| CB-29 | #2029 | 2026-11-21 (e8956899) | #1610 (CD20) | 1 / 1 / 0 / 0 | Owner answers: one estimated decode time and one limit, 3 s (CD20's second step, 2026-11-20); the CI-runner probe was approved (Namespace EPYC-v4, about 1.13x the desktop); the merge-judge loop was ended after stage 14 (2026-11-20), and stage 14b fixed Codex round 12's stage 14 regression (a comment or escape in an @media header). Register: in the PR body, 14 routes that fall back to main's 5 s budget and CB-28's memory, every one erring toward refusing; on the judge's 7,508 rendered documents, 0 hidden and 1,378 over-counts. Not delivered: no real-world export beyond core's mermaid and the repo's pie (downloading one needs the owner); d:path(), duplicate ids and arcs follow the IL; the 40 ns step cost is measured on two shapes; header placements were probed by sample. #1954 line posted (EmbedView.swift:161 has no cost guard either); no Mac speech changes. NVDA ran on 9c23729f, and later stages add no sentence. Not decided, as briefed: a diagram-stage deadline, and a bounded cache of successful decodes (#1955). Found and fixed: the probe tool now maps the cost sentence to "refused". Judge record comment 7058816967. |
| CB-15c | #2032 | 2026-11-21 (7a34cb6e) | #1890 (CD9) | 12 / 5 / 4 / 1 | Filed #2031 (History compare "no such version" after an outside edit). #1955: Find 2 for CB-46 (landed recovery that failed to index says "Can't restore"; ruling 5 named departure; 7060894084); Restore As leaves keys on its button, "History panel." spoken twice (7062231830); judge's 2 minors + 5 nits (7062804535). #1866: bulk rename Preview drops keys behind its sheet (7062230594; to CB-17a). #2012 data points (7060901161). #1956 Citations lead doubles (7061280062). Contract 38 ledger two rows. Departure (ruling 1): rule 2's text sinks shell-wide, flow rules stay on Bases. For the owner: does "Plain words" extend to the ordinary-input sentences (PR table). Not delivered: rule 2 flow rules shell-wide; fact for 20 census-only reads; RowWriteAfterHeaderWait not driven; VoiceOver (no Mac speech change). No Mac work owed |
| CB-32 | #2033 | 2026-11-21 (46f50603) | #1705 #1782 (CD22) | 13 / 7 / 6 / 0 | VoiceOver pass for the trimmed Backlinks context and the Mac's rebuild pause and folder order (#1954); APFS ς/σ behaviour by reading; ghost key and graph name filter for the owner (#1955) |
| CB-22 | #2035 | 2026-11-21 (f547d8f5) | #1661 (CD14) | 8 / 4 / 3 / 1 | CB-22b ends the sixteen dated rows of MenuAccessKeyCensus and the folder-name underscore in the note header's Folders menu (one #1955 line); #2034 filed (an open on a reading-mode note moves only the hidden caret); the license script's CRLF on Windows (#1955); the owner's eye on Alt+E, A and O now running at once in editing mode, and on a Ctrl+click on a checkbox being silent before a note's first task list loads; three menu facts skip on the hosted runners (#1680 shape) |
| CB-17a | #2037 | 2026-11-21 (3c0dbe55) | none (Refs #1866) (CD11) | 6 / 0 / 6 / 0 | CB-17b ends the 47 dated rows, with first the dead keyboard after a cancelled Import… box and the bare-window restores (#1866 comment), and the stuck-off Clear All Marks (#1955); three NVDA finds on #1955 (Queries leaf misses a base rename, Tab stops silently past a tree row, Shift+Tab skips expanders, unconfirmed); judge's 3 minors + 4 nits on #1955 for CB-17b; MenuRowCommandTests and the MainMenu keys-first witnesses skip on hosted runners (#1680) |
| CB-33 | #2036 | 2026-11-21 (02fbd37f) | #1917 (no decision row; the lead's plan call: the planner stability guarantee on every connection) | 12 / 3 / 5 / 0 | The owner's eye on the plan call and on the drafted SQLite report (posted by no one); five #1955 lines: the scan's fourteen never-cached per-file statements (a cache of 64 with them), unresolved-link pages that cost the whole list (needs an index, so a migration), a bound value against a partial or expression index is unguarded for product statements (remedy: SQLite as the judge end to end), the connection census's skip after a test module closed as "} // mod tests", two cache facts tied to 32 slots; no run goes through the anchored (macOS-only) branch of the open on any CI lane, held by the type and a source census; CB-45b: a reader connection opens through db.rs's one configure function and joins the connections fact |
| CB-45 | #2042 | 2026-11-22 (df1c1147) | #1718 Refs (CD34) | 6 / 0 / 5 / 1 | Owner reads the design and confirms two Call 9 points before CB-45b (the widened branch: the write's re-rank keeps the reader's selection and names a moved one; a default first-row selection is not the reader's); plan edits per the PR's "For the plan" (CB-45b M, runner lane added, no lib.rs edit, beside CB-43 in C16 over windows.yml and Workflows.Tests.ps1; new rows CB-45c and CB-45d with NVDA spot checks and their own owner calls; the #1718 ledger; the VoiceOver check moves to CB-45c and #1652; §11 +2 PRs; the answers to Calls 2, 9 and 10 into §3.1); CB-45b appends the AR row and the R-9 addendum, and adds a #1954 line; #2039 filed and commented; nothing past step 0 was run |
| CB-17b (PR 1) | #2041 | 2026-11-22 (ad40500d) | Refs #1866 (CD11) | 21 / 14 / 6 / 1 | Filed #2040 (Save's "unavailable" under NVDA; the owner accepted it). #1954: the Mac adopts WorkStillRunning with a VoiceOver pass. #1955 comment 7080803604: six deferred finds, the other-drivers line shrunk, m3 closed for 32 rows, m2 kept. PR 2 owes the fourteen sheets, the launcher and its restore, m1 and the end of the dated kind, and closes #1866. Not delivered: VoiceOver (#1954); a physical held Enter under NVDA; "Folders are still expanding." under NVDA; the gate journey and the Space, click and menu facts on hosted runners (#1701, #1680) |
| CB-20 | #2044 | 2026-11-22 (68e4f974) | #1708 (CD12) | 9 / 6 / 2 / 1 | Two #1955 lines: "Diagram, rendering" is spoken text the host composes (m4, comment 7086672178), and the copy makes one XAML round trip per part (N6, comment 7090079433). Filed #2043: after a diagram lands, NVDA's say-current-line reads the line above once. The Ctrl+Alt+D case is comment 7084924764. CB-26 adds its callout row to ReadingEmbeddedElementCensus. The NVDA and Narrator passes ran on b3645827, not on the merged head. The later commits change mouse, restore and copy behaviour, not speech. |
| CB-17b (PR 2) | #2045 | 2026-11-23 (38f6edde) | #1866 (CD11) | 28 / 28 / 0 / 0 | None deferred, no Mac work owed. Found and fixed, main's behaviour: Files Citing presented under Settings took the keys out of Settings; a closing sheet's restore landed the keys behind a sheet still open. The NVDA spot found the template picker's momentary hold, heard as "unavailable" on A1, fixed here. The merge judge found a sheet with no restore leaving the keys beneath until a Background landing, the property restore's missing fact, and a dead control on the window's return landing Settings' Close; all fixed here. Not delivered: a second whole run after 1cdc17af and 608f2a68 (opening-landing changes only; the touched classes and every census ran locally, and the lane ran the whole project); two guards no product route reaches; the menu, Space and click facts on hosted runners (#1680, #1701) |
| CB-42 | #2047 | 2026-11-23 (5e0846f5) | #1625 (CD30) | 1 / 0 / 0 / 1 | none (#2046 filed for the owner) |
| app job time limit (not a chunk: main's app job reached its 60-minute limit on 5e0846f5) | #2052 | 2026-11-23 (ef0c780f) | none | 0 / 0 / 0 / 0 | Nothing. The `windows` job's limit is 80 minutes, under the home lane's 100-minute app cap; the unit run alone takes about 54 to 55 minutes. Main's run on 5e0846f5, rerun after the timeout, was green |
| CB-23 | #2049 | 2026-11-23 (f363a597) | #1800 (CD15) | 3 / 1 / 0 / 2 | #2053, #2054 filed (#2048 filed on the owner's answer) |
| CB-22b | #2055 | 2026-11-24 (ad04a470) | none (Refs #1955, comment 7066667520 line 1) | 6 / 5 / 0 / 2 | #2050 and #2051 filed; the owner's eye on every once-shared key now running on its first press (Alt+F, C closes the vault) and on repeated sequences typing into a note; the hosted runners skip the menu facts (#1680) |
| CB-34 | #2058 | 2026-11-24 (fce6a6e1) | #1834 | 6 / 3 / 2 / 1 | Deferred to #1955 (comment 7148412636): Esc back from a list of choices leaves the caret at the start of the restored query; Escape with the keys on a palette row logs "unavailable" under NVDA (both on main). Filed #2057: in the palette's list the arrows move the selection but not the keys (on main too; needs a speech decision). #1954 line (comment 7148412227): the Mac's selection rule, silent follows in its view, whether hover is a reader's move, filteredCommands' score order, Swift twins, VoiceOver pass. For the owner's eye: the two quiet cases under "Silent follows" (an untouched row a query removes; recovery from no matches), with base and head phrases in the PR; "refresh" and "back" now lead with Bases: Refresh and Connections: Back; a right-button press makes a row the reader's; the list's letter search is off; residuals named in the PR (an arrowed row survives another typed name; an Enter before the latest rank returns runs the rows on screen, which ran the typed command 15 of 15 under NVDA at typist speed; a UIA select on the selected row is not the reader's) |
| CB-16 | #2061 | 2026-11-25 (b19c94ae) | #1830 #1791 (CD10) | 23 / 16 / 2 / 5 | VoiceOver pass and the Mac's adoption (#1954); Narrator cuts a no-press summary under the keys, import and trash (#2063); the trash's Cancel press at Medium (#2062); after the status bar's Cancel is pressed, nothing says where the keys went (the press cuts the status bar's focus line, the trade accepted with B; the Files pane's Cancel import cuts "File actions, grouping, expanded" the same way) |
| CB-36 (first PR) | #2066 | 2026-11-26 (01b293b3) | #1838, #1722 | 7 / 2 / 2 / 3 | Two lines on #1955 (the board's ItemContainer door's lenient answers; a card's SelectionContainer is null). Filed #2064 (NVDA object navigation skips a labelled connection; the control type needs the owner), #2065 (the graph diagram's Selection has the shape the board's had) and #2068 (a held placeholder's Realize pans a hidden board; the judge's minor). not delivered: Narrator spot check. Owner's eye: a next-item walk realizes and pans the board item by item. #1721 waits for CB-36's second PR |
| CB-21 | #2072 | 2026-11-26 (b2ebcbb9) | #1330 (CD13) | 4 / 2 / 0 / 2 (filed #2067, #2071) | not delivered: scan mode rows (Narrator+Space); how Narrator meets the provider's null, which no classic UIA client can show; a Narrator measurement of a text field on main (the text fields are #2067); a bound on the rectangle walk for a table row whose first cell wraps to thousands of lines (the judge's nit 2) |
| CB-36 (second PR) | #2073 | 2026-11-26 (20f7a8ab) | #1721 (CD24) | 10 / 3 / 3 / 4 (filed #2069, #2074, #2075, #2076) | Not delivered: a Narrator pass; NVDA object navigation to a connection (#2064); a keyboard route to a cut-off chip's full label, and whether a seated truncated card's tooltip should outrank the pointer on a chip (#2076, waits on the owner's design). Owner's-eye items: realizing a connection longer than the view pans to its chip, so neither card is in view until Invoke; while a truncated card is seated, a chip's tooltip does not show (#2076). |
| CB-35 | #2077 | 2026-11-26 (998db5dc) | #1837 | 15 / 5 / 10 / 1 (filed #2070) | not delivered: whether a keystroke's setter settles inside a TSF write lock under IME composition, touch-keyboard autocorrect or dictation (on #1955); a stall probe with a working positive control; the vault file picker by ear; Ctrl+O in a fact; the 50,000-folder settle cost. Owner's-eye: letter search off on five lists (Quick Open, search, the Files filter, Move To, the canvas pickers) |

CB-0, beyond its docs PR: it opened #1954 ("Mac work owed by plan 47"),
#1955 ("Plan 47 deferred finds") and #1956 (CD38's catalogue, in
Milestone W); retitled #1323; posted all 24 audit status comments and
those on #1653, #1628, #1733, #1842 and #1848; pushed the tag
`ds11-accent-contrast-gate` at 9edf4b6b; and moved #1842 and #1848 to
Milestone W. Its red team found 0 high, 2 medium and 11 low. One medium
was applied: the Medium line in `AGENTS.md` now reads "defer it to the
plan's list of deferred finds; outside a plan, or in a plan that keeps
no such list, file it". The other is left for the owner: the subsystem
definition does not say where an app's root-folder files belong, or
whether a Swift type's extensions count as a partial class. Its one
deferred find, a line on #1955, was that §9 and the CB-0 spec said
plans 44 and 45 filed the mac follow-ups. Plan 44 alone did, and both
places were corrected on 2026-11-16.

Wave C1, 2026-11-16. Each chunk had a fresh Opus red team and Codex
rounds to an approve: CB-4 two rounds, CB-1 three, CB-2 three. Every
Codex round ran at the configured effort, high, where the protocol's
step 3 asks for xhigh; the findings stand and the rounds are recorded
as run below that tier. Codoki reported Safe to merge at 5/5 on all
three. CB-2's lane probe ran on a scratch branch (run 43829430401): a
planted stall turned both model shards red five minutes into the test
step, the log named the stalled case, and a failed-jobs rerun kept both
attempts' evidence side by side. Not verified on a lane: a run that
reaches the 70-minute blame timer, `cancelled()` after the job's
90-minute limit, and a stall in a family other than composed (the probe
planted it in the composed case body).

CB-8, 2026-11-16. Its spec was written at wave C2's start and, after
the merge, rewritten as a record of what merged. A red team found no
blocker and four notes, all taken. Its third note (the 1970 dates older
migrations leave, and the graph table and sidebar filter surfaces) went
into #1964. Codex approved in one round, run like wave C1's at the
configured effort, high, where the protocol's step 3 asks for xhigh;
the round is recorded as run below that tier, and the PR body says so.
It also took #1963 at the lead's request: the property test's name
generator now leaves out the 22 stems core refuses on Windows.

Wave C2, 2026-11-16. Each chunk had a fresh Opus red team and Codex
rounds to an approve: CB-8 one round, the math template one, CB-3
seven, CB-5 one and CB-6 five. The configured effort was high
throughout, below the protocol's xhigh, as in wave C1: the config file
was not changed during the run. Only #1966's and #1970's bodies state
it. CB-3's rounds
1 to 5 each found one gap in how the docs census reads a script (a
path that climbs with `..`, a backslash separator, letter case, an
f-string, a byte-order mark), and round 1 also found a default shell on
the docs census job that passed the parity fact. Round 6 approved, a
second Opus read came next, and round 7 approved the one commit it
caused. CB-6's rounds 1 to 4 made eleven findings, nine of them
spellings the census did not recognise. Codoki reported Safe to merge
at 5/5 on all five.

CB-6's census: the unit project's fixed waits were 81 on the spec's
base, 80 on its first commit once CB-1 was under it, and are 68 at the
merge, and its elapsed-time ceilings went from
19 to 2. The gate's 31 sites and its one ceiling are pinned `unswept`
for CB-7. §2.3 read 30 gate sites; the thirty-first is a sleep in
`FocusSettleWatchTests.cs`. CB-6 also corrected contract 34: H1's
bullet for the large canvas fact and TH-2 now say the fact asserts warm
medians. The PR H section is not frozen.

CB-3's probe ran on a scratch branch (run 43858767321). The contract 38
step went red 14 seconds after the step started, about two minutes
twenty into the app job, and the row it printed equalled `main`'s. The committed ledger already equalled the
generator's output, so nothing was regenerated.

CB-5's seven probe pushes ran on a throwaway PR, #1969, since closed.
They used most of an hour's Codoki quota, which is ten reviews an hour
for the account, and CB-6's PR met the limit and waited sixteen minutes
for its review. Later probes run on a branch through
`workflow_dispatch`, with no PR open.

Waves C3 and C4, 2026-11-16 and 17. Each chunk had a fresh Opus red
team and Codex rounds to an approve, at effort high: CB-10, CB-27 one
round, #1975 two, #1979 two, CB-13 three, CB-9a seven and CB-7 eight.
Four of CB-9a's seven findings and three of CB-7's eight were code
shapes that a census did not read; both censuses now refuse by default
and admit by name. CB-7's other five were in the tests' own logic.
Codoki reported Safe to merge at 5/5 on every PR but CB-7's. There it
held "Requires changes" on one finding, that xunit has no `Assert.Fail`,
answered once and repeated. An independent judge found it false (the
gate project references `xunit.v3.mtp-off` 4.0.2, whose assert library
has the method; the call stood 44 times on `main` before the merge; the
lane built and ran the file) and the PR merged on that verdict, recorded
in its body.

CB-9b moved from wave C4 to C5, behind CB-13, because both edit
`TryRenameSelected`.

Two specs had a wrong mechanism. CB-7's said a delete-sharing read lets
the app's rename through; a measurement before any product code showed
that every Windows writer makes the classic replacing rename, which is
refused while any handle is open on the target (#1974). CB-13's spec
recorded an echo on a refused rename. That one was built first; Codex's
first round and a probe with the real watcher then showed the echo hides
an outside edit made in the seconds before it, so a refusal records
nothing. From wave C4 on, each spec has a step 0 measurement and its own
red team before code.

Two fixes went in as their own PRs because each reached another module.
#1975 (after a case-only rename the sweep inserted a second row; CB-13's
step 0 found it) merged ahead of CB-13. #1979 (the math panic-hook fact
raced a sibling's first MathCAT initialisation) turned CB-27's lane red
once; CB-27 merged on a rerun of that job, and #1979's own PR merged
after it.

Product defects on `main` that these waves found and filed, none placed
in a wave yet; each waits for the owner's call:

- #1974: a save is refused while another program holds the file open.
- #1977: an outside edit made just before a rename is dropped as the
  rename's own echo and never reaches the index.
- #1980: a Bases cell edit on a note with no tab names a path, not a
  file, and can land on a note created there meanwhile.
- #1981: a stopped rename's recovery loses the history binding and
  names no creation in the change feed.
- #1976: opened with Alt+F, the File menu rests above its own row, and
  the resting-pointer gate fact can miss because of it.

The home Mac runner stopped answering at 00:55 UTC on 2026-11-17. A run
routed to it two minutes earlier stayed queued for ninety minutes; a
cancel and a rerun routed it to Namespace.

Wave C5, 2026-11-17 and 18. The account's Fable limit stopped both
principals mid-spec at 08:40 UTC on 2026-11-17. On the owner's word
that day, two new principals and their workers ran on Opus at xhigh
from the saved specs and worktrees, until the limit resets on
2026-11-21. Each spec had a step 0 measurement and an Opus red team
before code, and each diff a fresh Opus red team. Codex ran at effort
high, below the protocol's xhigh as in the earlier waves, to an
approve: CB-12 two rounds, CB-19 four, CB-9b five and CB-11 eight.
Codoki reported Safe to merge on every PR: at 5/5 on CB-11's, CB-12's
and CB-19's, and at 3/5 on CB-9b's ("Needs work before merge", from 7
of its 23 files), whose points were answered on the PR by reading.

CB-11's Codex rounds 1 to 7 each found a schedule the branch got
wrong, and each fix in the chunk's own feature was seen red first. In
rounds 1 and 4 to 7 the parked press could be answered for a file that
was not its note, where `main` drops it; in rounds 2 and 3 a fix
dropped a press that should have followed its note. Findings that
`main` already has went to #1993 and #1994. The spec's rule, that the
press follows the tab the leaf shows, was wrong where two tabs name one
path. The press now follows the tab it was parked on, and only with
proof that the moved file is that tab's version: the index at the new
path for this window's renames, and, for another session's, the feed
record's hash and the window's present hash for the destination. Round
7's second finding, another session replacing the file between the
check and the load's read, has the same exposure on `main` for any
parked press; the lead declined it as the residual risk of a read by
path, recorded on #1990.

CB-19's first build stamped closed lists too, on the reasoning that the
lists hold 3 to about 10 rows. Its diff red team showed that the
dashboard editor's saved-query picker binds every saved query and stays
bound with the editor collapsed, and a refill (a `Clear()` and one
`Add` for each query) restamped every realized row at each `Add`, so
the writes grew with the square of the rows. The tracker now stamps
only while a list is open, and a fact shows a closed combo box reports
what it reports on `main`.

CB-9b's spec put eight calls to the lead, all ruled as recommended on
2026-11-17. The lead's undo rule (Design 0) made the fix of an undo
that wrote another writer's replaced bytes back part of the chunk.

Defects on `main` that wave C5 found and filed, none placed in a wave
yet; each waits for the owner's call, beside the five above:

- #1988: a citation summary answered inside the unsaved-changes prompt
  stays over the next note.
- #1989: a re-seat that respells the active tab leaves the panels on the
  old spelling.
- #1990: a citation load for a path the index no longer has says no
  citations.
- #1992: a cancelled bulk rename calls the files it never reached
  "failed".
- #1993: a parked citation summary press survives leaving its note
  while another tab's save is writing, and the sheet opens unasked.
- #1994: two notes with identical bytes both follow another session's
  rename of one of them (the change feed plans a move by the shown
  version, so a tab can follow a file that is not its note).
- #1999: the reading view's 2,000-block notice is spoken again whenever
  another writer's bytes land in a reading-mode tab.
- #2000: the Windows app installs no handler for an unhandled exception,
  so a publication whose sentence throws ends the process (by reading).
- #1995 (tooling): a field pass started by the launcher without `-Root`
  runs with no hot-exit store.

#1991 has left that list: the owner's call of 2026-11-18 (§3.1) closed
it as not planned.

Wave C6, 2026-11-18. CB-25 (#2009) merged as 4b700172, CB-30 (#2007)
as 26d353ef and CB-14 (#2008) as 834cbe31, in that order. Each spec had
a step 0 measurement and a fresh Opus red team before code (blockers,
medium and low: CB-25 0, 5 and 13; CB-30 1, 5 and 19; CB-14 0, 9 and
12), and each diff a fresh Opus red team (CB-25 0, 2 and 10; CB-30 1, 3
and 11; CB-14 0, 3 and 10). Codex ran at the configured effort, high,
below the protocol's xhigh as in the earlier waves, to an approve:
CB-25 in one round, CB-14 in two (rounds three to six, on later
commits, approved as well, six rounds in all), and CB-30 in four, then
four more on the delta of its second ruling (below).

Codoki reached its monthly limit on 2026-11-18 (500 of 500 reviews,
reset on 2026-12-01) and reviewed none of the three. From #2007 on,
each product PR had an independent Opus merge judge at effort xhigh in
Codoki's place, and the lead recorded each verdict on its PR as the
bypass §2.6 allows. Every verdict was Merge, with no blocker and no
major. CB-25 had one judge, CB-30 two and CB-14 four, and each later
judge read the delta the one before it caused. On CB-30 the lead took
both of the first judge's minors: the three finds, red first, and a new
deep-nesting fact, red under mutation J. On CB-14 the lead took
findings of the first three judges, each behaviour change red first
(the undo-history fact is a guard, red under mutations MJ4 and MJ5, and
two items were contract 38 wording), and the fourth said Merge
([#2008, comment 6991456136](https://github.com/coryj627/slate/pull/2008#issuecomment-6991456136)).
That fourth judge pointed out that contract 38 listed two accepted
residuals where there are three. The third (a split tab at the renamed
path, missed when the tab that sent the toggle was closed or left the
note) had been named by Codex's round 2 and is among the accepted risks
in #2008's body; this PR adds it to contract 38's landed-unindexed
toggle amendment. Until Codoki's limit
resets on 2026-12-01, a docs PR's fresh Opus red team at effort xhigh
on the diff stands as its independent review in Codoki's place, by the
lead's call, recorded on the PR.

The owner's call on #1991 (§3.1) came during the wave and bound CB-14.
A toggle that succeeds through a tab stays on its undo stack, as on
`main`. CB-14's landed-but-unindexed path splices no tab, rebaselines
none and leaves every undo stack as it was, so an edit made before a
landed toggle still undoes, on its tab and on a peer tab.

CB-14's order departs from CD8's literal text, by which the tab's
"Task toggled on disk, but the editor no longer matches it" still
follows the new sentence. Under NVDA a High line raised after a
sentence cancels that sentence's speech, and in CD8's order the new
sentence was measured silent on the Tasks panel route, as `main`'s
failure sentence was before CB-14. The lead's ruling on CB-14's Call 2
puts the reconcile sentence (High, unchanged) first and the new event
(Medium) after it. It changes neither the splice nor the reconcile
routines CD8 left alone (`ReconcileAfterExternalTaskWrite`,
`ReconcileTabsAfterDirectTaskWrite`) beyond their doc comments; a tab
renamed before a successful toggle publishes now takes a reconcile
branch ahead of the splice (the third judge's find, a defect `main` had
before CB-14). NVDA
played both in full on the Tasks panel, editor and reading-view routes,
and with two tabs on the Tasks panel route. Tasks Review through an
open tab was not heard; its facts pin the order. NVDA ran on bea36aa3,
before the judges' findings. Contract 38's new section records the
order, and whether it stands is a question for the owner (below).

The lead's first rulings on CB-30's spec left two finds on #1955:
`\frac x\sin`, a function as a fraction's argument (Call 5 recommended
deferring it; the ruling took that call's other half, `2\hat\sin^2 x`,
into the chunk), and MathCAT's stray full stop in the Nemeth of an
authored `\trprime` or `\qprime` (Call 4 recommended naming it in the
PR and the upstream draft; the ruling added the #1955 line). The first
merge judge pointed out that `AGENTS.md` sizes a find whose fix stays
in files the PR already changes as small, to be fixed in the PR, and
named three such finds in `math.rs`: those two, and an ungrouped
operator argument that overwrote its base's script position
(`\liminf\limits_{n}^\max x`), which the pulldown-latex draft had only
named. The lead's second ruling took all three into the PR on that
sizing, as CB-30's spec, #2007's body and the lead's comment on #2007
record. The lead's ruling on this record goes further: they are cases
of the scripted function and of the primes that #1629 and #1628 ask
for, so they are the PR's own feature, not finds, and `AGENTS.md`'s
rule that a defect in the PR's own feature is fixed on its branch
governs them. §4 rule 5, which defers a found core change a Mac user
hears, does not apply to them; #2006, outside the feature, stays filed
under it. CB-30 fixed them red first, with a fourth case in its own
feature (`\int_0^\max f` kept the writer's application mark after the
element), and #1954 carries their VoiceOver lines. (#2007's body lists
all four under "Found and fixed here", and its count includes them.)
The judge also noted that the deep-nesting fact could not catch a
recursive walk; a new fact does. #1955 keeps `x^\rm a`,
which §9 names. An authored glyph in a superscript or after no carrier
keeps the stray full stop: #1955's comment of 2026-11-18 took that line
off its list, and it waits on MathCAT's cell, under §8's watch row for
#1628 and in the MathCAT draft.

Where the specs were wrong, each spec's closing section says so:

- CB-14: Design 6 left the Tasks Review with no tab in `main`'s order,
  so a tab that opened on the note during the write heard its reconcile
  line after the new sentence and cut it, and Codex's round 1 accepted
  no reconcile there when the read-back was unreadable; the first judge
  showed both, and the review now reconciles first and uses the landed
  write's hash. Design 8 reconciled the editor and reading-view routes
  on `SavedButUnindexed` only; any failure whose write landed now
  reconciles there too. The spec missed a tab renamed before a
  successful toggle published, which stayed clean over the changed disk
  on `main` too (the third judge's find).
- CB-25: Design 2's "no post without a peer" dropped a reset `main` ran,
  for a peer made after an Add and read before the next layout. The
  existing trigger now posts as on `main`, and only the new one checks
  for a peer. Fact 2's premise checked only the connected-root list; a
  UI Automation client's handlers already in WPF's event map, or a
  WM_GETOBJECT, also connect a new window's root, so the fact skips
  where either holds (on CI it ran and passed). Mutation D turned both
  halves of fact 3 red, not one.
- CB-30: Design 1's candidate rule, a thin space directly before an
  `<mi>`, could not tell an ordinary character from a function name
  (`\&` and `\operatorname{&}` are written alike); function identity now
  comes from the parser. §9 said `x^\, 2` falls back; it renders, and
  the row is corrected above. The spec put an accented function outside
  the family, as falling back for its script; an accent over a function
  is a script element of its own, so `2\hat\sin x`, with no script, fell
  back on the accent alone. The spec held CD21's braille from the loose
  tree right for every long run; for an authored `‴` or `⁗` it keeps
  MathCAT's stray full stop, so the braille tree now spells such a glyph
  after a carrier as single primes. Calls 4 and 5, and the lead's first
  rulings on them, are above.

The reading behind CB-25 was wrong in three places, as CB-25's spec
records. `reading/04_focus_and_peers.md:355-384` named eight hosts;
step 0 found eleven `LayoutItemsControl` sites, and two of them remove
or replace an item (the builder's conditions and the split host). It
asked for the shell gate because closing a pane removes an item from
the split host, but no gate journey closes a split pane, and no gate
step can be red for #1860, since a connected client heals it. It read
#1861's lesson as a host reset inside a peer read; by reading WPF 10,
#1861's crash was an item peer's reset re-entering its container peer's
guarded `GetChildren()`, and step 0 could not make the host's reset
recurse.

CD37: waves C1 to C6 posted no comment on #480, #1493 or #1628; the
last before wave C7 is wave C0's, of 2026-11-16. The lead read the
crates.io lists at wave C7's start, on 2026-11-18, and commented on all
three
([#480](https://github.com/coryj627/slate/issues/480#issuecomment-6987018008),
[#1493](https://github.com/coryj627/slate/issues/1493#issuecomment-6987018424),
[#1628](https://github.com/coryj627/slate/issues/1628#issuecomment-6987018939)).
`citationberg` 0.7.0, `hayagriva` 0.10.1 and `notify` 9.0.0-rc.9 are
unchanged since wave C0. `mathcat` 0.7.8, published on 2026-11-16, is
new; 0.7.7, the version `Cargo.lock` pins, is yanked; and 0.7.8 clears
the cooldown on 2026-11-23. As #2007's body records, 0.7.8 does not
correct the cell, so §8's watch row for #1628 is not met. #1628 closed
with #2007; the watch row stays, for dropping the split render, and the
bump regenerates the golden CB-30 pinned.

Defects on `main` that wave C6 found and filed, none placed in a wave
yet; each waits for the owner's call, beside #1974, #1976, #1977,
#1980, #1981, #1988 to #1990, #1992 to #1995, #1999 and #2000 above:

- #2003 (CB-14): a refused task toggle tells the reader to resolve in a
  dialog Windows does not show (by reading).
- #2004 (CB-14): a task toggled in Tasks Review moves focus to the
  review's first row.
- #2005 (CB-14): a sidebar create whose index commit failed speaks its
  caveat at High, which silences "Created note".
- #2006 (CB-30): a formula whose MathCAT-rejected element holds deeply
  nested content (about 8,000 levels) overflows the MathCAT worker's
  stack and ends the app.

Three more questions wait for the owner. First, whether CB-14's order
stands: the reconcile sentence at High first, then the new event at
Medium, where CD8's literal text has the tab's sentence still follow
the new one. Second, whether to file the MathCAT report on
daisy/MathCAT: the triple and quadruple prime cells of its Nemeth table
hold a literal full stop. #2007's body drafts it, beside a
pulldown-latex draft, and no agent files either. CD21 put the report in
the PR, but filing it is a public post on another project's tracker, so
it waits for the owner's word. Third, whether Codex runs at effort
xhigh, as the protocol's step 3 asks, or stays at the configured high
that every wave so far has used.

Wave C7, 2026-11-18 and 19. CB-28 (#2011) merged as 9999cb68, CB-24
(#2014) as 5b890dd9 and CB-15a (#2015) as cc16f7e3, in that order, and
#2017, the PR for #2016, a find taken early (below), merged as 57b9fbbb
on 2026-11-19. Each chunk's spec had a step 0 measurement and a fresh
Opus red team before code (blockers, medium and low: CB-28 2, 7 and 12;
CB-24 2, 5 and 14; CB-15a 0, 7 and 23). Each chunk's diff had a fresh
Opus red team: CB-28's found 0 blockers, 3 medium and 7 low; CB-24's 0
blockers, 1 major and 12 minor; CB-15a's 0 blockers, 2 major and 12
minor. Two Opus xhigh red teams read #2017's diff: the first found no
blocker or major, and the second one blocker, the menu bar logo's Left
and Right leaving the bar, which was taken. Codex ran at the configured
effort, high, below the protocol's xhigh as in the earlier waves, to an
approve: CB-24 in two rounds; CB-28 in four, with rounds 3 and 4
approving, the fourth after the merge of `main`; CB-15a in two, then in
two more on the first judge's change and one on the second's, each
ending in an approve; and #2017 in five, with rounds 1, 4 and 5
approving. On CB-15a the first of those delta rounds found the Markdown
refusal fact green before the change; the finding was declined with
reason, since that fact guards the case the change must leave
speaking, and a mutation proves it. On #2017, round 2 found the logo's
keeper swallowing Move Tab, taken red first, and round 3 the two
welcome-view changes, which the lead ruled intended (below).

Codoki stayed at its monthly limit (500 of 500, until 2026-12-01) and
reviewed none of the four. As in wave C6, each product PR had an
independent Opus merge judge at effort xhigh in its place, recorded by
the lead on the PR, and every verdict was Merge, with no blocker and no
major. CB-28 had one judge
([#2011, comment 6996634286](https://github.com/coryj627/slate/pull/2011#issuecomment-6996634286)).
Its first minor is CB-28's third departure (below), and the lead added
its proposed follow-up, a bounded cache of successful decodes, to #1955
after the merge. That line is the fifth find and the third deferral in
CB-28's row, which is why the row counts 5 / 2 / 3 / 0 where #2011's
body ended at 4 / 2 / 2 / 0. CB-24 had one judge
([#2014, comment 6998349391](https://github.com/coryj627/slate/pull/2014#issuecomment-6998349391)),
whose two accepted minors are in its row. CB-15a had three
([#2015, comments 6998996233](https://github.com/coryj627/slate/pull/2015#issuecomment-6998996233)
and [6999998061](https://github.com/coryj627/slate/pull/2015#issuecomment-6999998061)).
By the lead's ruling, the first judge's minor 1 was taken into the PR,
red first: the branch spoke the new Copy link failure on a canvas or an
image, where the Mac offers no action, so Copy link is now offered only
for exactly one Markdown file. Its minor 2 is the #1956 line and its
minor 3 the #1955 line in CB-15a's row. The second judge, on that
change, gave three nits, and the lead took two: the stale File actions
states, red first (when the Files selection moved to a date-group
header, a placeholder or nothing, the buttons kept the last row's
enabled state, as on `main`), and a comment's list of core's refusal
cases. The third judge, on that last change, found no blocker, major or
minor. #2017 had one judge
([#2017, comment 7011800264](https://github.com/coryj627/slate/pull/2017#issuecomment-7011800264)),
whose two minors were both taken: a window key binding now counts for
the keepers only when its command is not null and not `NotACommand`,
red first, since with no vault open the arrow bindings have no command
and the logo let Ctrl+Alt+Left and Ctrl+Alt+Shift+Left through; and
#2017's body names the 15 logo facts that CI skips as not delivered.
Its one nit was not taken: the doc comment's rule that a matched press
is handled holds for a plain command, not for a RoutedCommand that sets
ContinueRouting, and all six arrow bindings are RelayCommands.

The lead ruled CB-28's eight calls on 2026-11-18. The reading view's
refresh, the Ctrl+E popover's build and the embeds leaf's batch each
get a 20-second deadline of their own (Call 1(b)). The spec's red team
found, by reading (its blocker 2), that the leaf decodes every embed of
the active note on every activation, whether or not it is showing, and
the base NVDA pass saw the hidden leaf decode all ten. The diagram
stage gets the memory and no deadline, with a line on #1955 (Call
2(a)): a deadline there needs a transient outcome, so that the next
refresh retries the diagram, a sentence of its own, and a change to
WB-20's landing rule. The deadline counts decode time only, where
core's twin counts the wall clock from the note's first render, and a
wait the deadline cut short is not remembered, where core caches a
render its note deadline cut; the PR names both as departures (Call 3).
The unit-test host keeps the process memory off and the default
deadline open, as #1950 did for the per-image budget, and three facts
put the app's configuration back for one fact each (Call 4). The Mac
twin at `EmbedView.swift:161` is one line on #1954, by reading, with no
new issue (Call 5). The `lib.rs` doc comment, which now points at
core's list of forms, landed after #2008 merged (Call 6(a)). The form
is `"note-images"` (Call 7), and the base NVDA pass ran on step 0's
build of `main` (Call 8). The diff red team found a third departure,
which the PR names and the lead puts to the owner: successful decodes
count against the deadline and are not cached, so a note of many SVGs
that each succeed in just under 5 s shows the deadline sentence for the
later ones at every refresh, where `main` showed them all after
N × 5 s.

NVDA 2026.3.1 heard CB-28 on `slow.md`, which holds ten embedded SVGs
of about 90 KB that each take about 7 s to decode here; each step
waited for the leaf's batch before Ctrl+Shift+E. On base the reading
view landed 50.3 s and 50.7 s after Ctrl+Shift+E, and every card said
"Image not shown: decoding took longer than 5 seconds.". On head it
landed in 20.4 s on the first open, where m1 to m8 said that sentence
and m9 and m10 "Image not shown: this note's images used their
20-second decoding budget.", and in 0.1 s on the second, with all ten
remembered. #2011's body has the table. The overlap with CB-15a held:
CB-28 added and moved no read of an exception's `Message` in the three
files where CB-15a's census keeps dated rows (`EditorInteractions.cs`,
`Reading/ReadingContentViewModel.cs` and
`Panels/RightPanePanelsViewModel.cs`), and CB-15a ran the census on a
tree with CB-28 merged.

CB-24's step 0 settled #1849's cause: a resting pointer, not NVDA, a
key or a UI Automation SetFocus. With the pointer away from the menu
the keys stayed on the first row in every open, with and without NVDA;
with it over another row, that row took the keys in 20 of 20 opens.
The lead ruled its four calls as recommended: one shared
`RestingPointerHold`, with the app menu's #1944 remedy moved onto it;
the Files toolbar's Sort and group menu and the rail's More panels
menu, which failed 4 of 4 each on `main`, taken in the PR red first;
the editor's Shift+F10 probed, and the class of context menus that
WPF's ContextMenuService opens filed as one issue, #2013, once the
probe reproduced it; and #1846 ending by comment after the merge
(below). The header buttons' Shift+F10 and Apps-key route, which landed
no row, was taken as a small find, red first. The app menu's
regression fact, now on the shared hold, missed 5 of 10 runs on head,
and 2 of 5 on base in an interleaved control, each miss with #1976's
placement message. Under NVDA 2026.3.1, on #1849's steps with the
pointer resting on the ⋯ menu's last row, base said "Split Right,
Ctrl+backslash, 1 of 3" and then "Bulk Rename Properties…,
Ctrl+Shift+R, 3 of 3" by Invoke and by Enter, and "menu" twice by
Shift+F10. Head said "Split Right, Ctrl+backslash, 1 of 3" and nothing
after, all three ways. #1846's run matched on base and head:
one focus-changed event, on the editor, with NVDA naming the tab
control and the tab as the editor's ancestors, which CD16 accepts.
#1846 was ended by a comment after the merge
([comment 6998921082](https://github.com/coryj627/slate/issues/1846#issuecomment-6998921082)).

CB-15a's step 0 ran the census's trial over all 309 shell sources:
rule 1 found 96 reads of an exception's `Message`, and rule 2 found 76
hits of its own, none, by reading, a binding dump that reaches text.
CD9 put rule 2 over the whole shell only if the trial showed few hits,
so by the lead's ruling it stays on the Bases sources. The lead's
other rulings: the tag edit's read, which CB-9b had moved into
`WorkspaceViewModel.Properties.cs`, joins this chunk; the listing that
fails when another session commits during it is filed as #2012, with
core's developer-text details in the same issue, since the owner wants
as few new issues as possible; and Copy link, silent on `main` when
core makes no wikilink, says the Mac's sentence, "Could not create a
wikilink for this file.". After the first judge's minor 1 the lead
gated Copy link to exactly one Markdown file, as on the Mac. When it is
unavailable the palette gives its existing generic reason, so the PR
adds no unavailable sentence, and whether Windows adopts the Mac's
specific reason is a question for the owner (below).

`ShellFailureDetailCensus` now holds the whole shell to one reader of
an exception's `Message`, `FailureDetail.Of`. The other 73 reads stand
as 61 dated rows under a pinned baseline, which CB-15b and CB-15c
inherit: each deletes its rows and lowers the ratchet as it routes its
sites. Beside its new helper and its own tests, CB-15a edits two
shared files that no §6 row names: one line of
`Bases/BaseDocumentViewModel.cs`, where `BasePhrase.ErrorDetail`
forwards to `FailureDetail.Of`, and the unit tests'
`Support/ShellCompilation.cs`, which every binding census reads and
which now binds with the app's own generated usings for the test
configuration. No §6 row names CB-28's
`Panels/RightPanePanelsViewModel.cs` and `HostLog.cs` or CB-24's
`NoteHeaderView.cs` either. Under NVDA 2026.3.1, Move To onto a folder
that already holds the name said "Move failed: @path=sub/a.md" on base
and "Move failed: Something named sub/a.md already exists there." on
head; the tag edit with "#" said "Tag edit failed: @message="#" is not
a valid tag." on base and "Tag edit failed: "#" is not a valid tag." on
head.
On head, Copy link on a canvas is a disabled button that object
navigation reads as "Copy link, button, unavailable", and the palette
says "Selected: Copy Wikilink. Unavailable: This command is not
available right now.". The Markdown refusal was not reached by keys; a
hosted fact covers it.

One defect on `main` was taken early. An NVDA run during CB-15a's field
pass heard forward Tab stop at the window's last stop, and the lead's
investigation filed #2016: forward Tab never wraps there, because the
collapsed welcome view's `FocusManager.FocusedElement`
(`MainWindow.xaml:1030`) ends WPF's search for the first stop. An
in-process probe found the cause; by reading, the binding moved onto
the welcome view in a8c7b960 (#1010). #2014 is not the cause: 9999cb68,
before it, behaves the same. #2016 was taken early as its own PR, as
#1965 was for CB-4's find. #2017 merged as 57b9fbbb on 2026-11-19, and
its row is in the table above. The binding moves to the Window, a focus
scope, where W0 first put it. The dead end had also ended WPF's arrow
search through the window, so three stops that had kept their arrows
only because of it now keep them themselves: the content pane's border
(the empty editor stop) and the menu bar's logo by a KeyDown keeper,
and the rail's ends by a Contained group. So the binding move alone
was not enough: the fix needed the two keepers, the constructor call
that registers them and the rail's group besides. The return to the
welcome view does not rest on `ViewModel_ReturnedToWelcome` alone: the
landing when the workspace root goes also lands on Open Vault. #2017
edits `MainWindow.xaml`, `MainWindow.xaml.cs` and
`MainWindow.ShellRegions.cs`, after CB-24's merge. Under NVDA 2026.3.1,
Tab from More panels with a vault open said "tab" and nothing else on
base, and "New note, button" on head.

Where the specs were wrong, each spec's closing section says so:

- CB-28: Design point 3 let a remembered image say the pixel pool's
  notice once earlier SVGs had spent the pool. Codex's round 2 showed
  that shortcut skipped the memory and the deadline; a helper now asks
  both first, so that limit is gone. Point 4 charged a timed-out decode
  its wait and any other decode up to its wait's return; the charge is
  now taken once, in the `finally`, after the PNG conversion and a
  failed wait's `Join`, and every timed-out wait costs at least its
  wait (Codex's round 1). Point 6's "the next ones get their turn"
  holds for timeouts only, which is the third departure above. The wait
  seam now takes the milliseconds the decode waits (the diff red team's
  M1); without that, a mutation that passed the budget in place of the
  cut wait survived the suite.
- CB-24: the spec said `Slate.ContextMenuStyle` turns WPF's popup
  animation off. It does not: the style's setter lands on the
  `ContextMenu`, and WPF sets the popup's own animation from
  `SystemParameters.MenuPopupAnimation`, so these menus fade on close
  (a probe saw `Closed` 185 ms after `IsOpen` went false). Three guards
  came from the fade: a late `Closed` never ends the next open's hold,
  and a pointer's open or a right-click during a keyboard open's fade
  gives back that open's hold. The spec expected base's Shift+F10 under
  a resting pointer to say "menu" and then the row under the pointer;
  it said "menu" twice and the keys stayed on the menu, because WPF's
  service places a keyboard open elsewhere. A hold begun at Loaded
  priority did not turn the gate fact red on this desktop, so a hosted
  arm pins the order. On head, Alt+F under a resting pointer said
  File's first row, not "File" first.
- CB-15a: the red census named 24 reads, not 23, because
  `BasePhrase.ErrorDetail`'s own read stops being the exempt one once
  the helper is found by symbol. The row key is the containing type's
  full name and the member as its symbol names it, where the spec's
  nearest type identifier let twins collide (Codex's round 1), and a
  pinned baseline with ceilings of 61 rows and 73 reads holds the
  table, where the spec said only that it could shrink. Step 0 said
  `FilesSidebarViewModel.cs:2160` could not be reached from a tree row.
  On `main` a date-group header or a placeholder row reached it: Copy
  link spoke an `InvalidPath` sentence there, and Pin wrote an empty
  path into the pins and said "Pinned Today.". Copy link, Pin and Unpin
  are now unavailable on those rows (the diff red team's major 1).
  Design 4 gave the Mac's sentence to any file core makes no wikilink
  for; the first judge showed that the Mac offers no action on a canvas
  or an image, so the sentence now stands only for a Markdown note.
  Copy link sits in the File actions expander with no chord, where the
  spec put it on the Files toolbar with one. Load folder files
  (`:2626`) has a deterministic fact through a constructed directory
  row, where the spec allowed census-only coverage.

CD37: wave C7's start read is the one recorded above, after wave C6:
the lead's three comments of 2026-11-18. No other CD37 comment was
posted during the wave. `mathcat` 0.7.8 clears the cooldown on
2026-11-23, and since it does not correct the Nemeth cell, §8's watch
row for #1628 is still not met. Wave C8's start reads the lists again.

Defects on `main` that wave C7 found and filed, none placed in a wave
yet; each waits for the owner's call, beside #1974, #1976, #1977,
#1980, #1981, #1988 to #1990, #1992 to #1995, #1999, #2000 and #2003 to
#2006 above:

- #2012 (CB-15a): a folder listing fails when another session commits
  during it, because core refuses even a first page and the Windows
  host never restarts the listing. The reader hears "Could not load
  files: directory page cursor is stale; restart from the first page".
  The issue also lists core's details that read as developer text.
- #2013 (CB-24): a context menu that WPF's ContextMenuService opens
  from the keys lands no row, and a resting pointer gives the keys to
  the row under it.

Three more questions wait for the owner. First, CB-28's third
departure: a note's successful SVG decodes count against the 20
seconds and are not cached, so slow successes spend the deadline at
every refresh, and the later images say the budget sentence each time.
#1955 carries the follow-up the merge judge proposed, a bounded cache
of successful decodes under the same key. Second, whether the diagram
stage gets a deadline, which needs a transient outcome, a sentence of
its own and a change to WB-20's landing rule. Third, whether Windows
adopts the Mac's specific reason when Copy link is unavailable,
"Select exactly one Markdown file to copy its wikilink.", in place of
the palette's generic "This command is not available right now.";
adopting it adds a spoken sentence on Windows. The three questions
from wave C6 are still open: CB-14's order, the MathCAT report and
Codex's effort. A fourth is open too: whether the owner's #1991 call
covers every route through a tab, as §3.1's row reads it.

#2017 also changes three behaviours, and a reader may notice them. The
lead ruled each one intended, and they are listed here for the owner.
The first was seen in the test suite's window only; wherever the test
window and the running app were both tried, with a note or no file
open, they agreed.

| Where | On `main` | Since #2017 |
|---|---|---|
| A canvas open (seen in the test suite's window, not tried in the running app on `main`): the menu bar's logo with Left or Right, the content pane's border with any arrow, and the rail's ends (Down on More panels, Up on the bare strip) | the arrow leaves the stop | the stop keeps it |
| No vault open: Alt onto the logo, then Left, Ctrl+Alt+Left or Ctrl+Alt+Shift+Left | the keys go into the welcome view (for the two chords, inferred from WPF's source, not run) | the keys stay on the logo |
| The welcome view with a recent vault: Alt onto the logo, then Shift+Tab | Open Vault, past the recent vaults | the last recent vault, the welcome view's true last stop |

Wave C8, 2026-11-19 to 21. CB-15b (#2020) merged as 15cba369 on
2026-11-19, CB-31 (#2024) as c4d1cd60 on 2026-11-20, the invalid-search
change (#2028) as 925f0a42 on 2026-11-20, and CB-29 (#2029) as
e8956899 on 2026-11-21, in that order. Three principals worked the
wave: S took CB-15b; T took CB-31 and then CB-29; and U took the
invalid-search change, which the owner directed on 2026-11-20, during
the wave, in answer to #2020's "For the owner" section (below). It is
not a chunk, and it changed no slot. Each spec had a step 0 measurement
and a fresh Opus red team before code (blockers, major or medium, and
minor or low: CB-15b 0, 4 and 15; CB-31 2, 2 and 8; CB-29 2, 11 and 11;
the invalid-search change 1, conditional on its Call 4, then 3 and 11).
Each diff had a fresh Opus red team: CB-15b's found 1 blocker, 5 minors
and 1 nit; CB-31's 0 blockers, 1 major and 5 minors; CB-29's, at
3e057460, 1 blocker, 3 majors and 6 minors; and the invalid-search
change's 0 blockers, 0 majors, 9 minors and 7 nits. Codex approved
CB-15b in round 1 (43992f2e), CB-31 in round 1, and the invalid-search
change in round 1 (2f2bf36a) and again in round 2, on the merge
judge's delta. CB-29 took 13 rounds (below).

As in waves C6 and C7, each PR had the lead's independent merge judge,
recorded on the PR. CB-15b's judge
([#2020, comment 7023737556](https://github.com/coryj627/slate/pull/2020#issuecomment-7023737556))
said Merge, with no blocker or major: its minor 1, the mutation line,
was met by the mutation run; its minor 2, read from the code, is
#2022; and its nit on the fact count was corrected in the PR body.
CB-31's judge
([#2024, comment 7027494715](https://github.com/coryj627/slate/pull/2024#issuecomment-7027494715))
said Merge and asked only for edits to the PR body: the two Windows
effects a first-indexing repair's Created no longer triggers (the clean
tab's staleness check and the History refresh), `open_base` named
beside `open_canvas` as healing without a file event, as on `main`, and
the §6 note on `index_file`'s return type, which this docs PR carries.
The invalid-search change's judge
([#2028, comment 7036024550](https://github.com/coryj627/slate/pull/2028#issuecomment-7036024550))
said Merge, with no blocker or major. Its minor 1 and nit 4 were taken,
red first (37d28b85, then cf7bdbb8): a cached MATCH statement
re-prepared against a damaged schema fails with SQLITE_CORRUPT and a
message that says "syntax error", which the text-only classifier heard
as the reader's query, so `map_fts5_error` now reads refusal texts on
an SQLITE_ERROR only; and the CLI's search comments say what
`InvalidQuery` now carries. Its #1955 line is comment 7036192406: on
the Bases interpreter path a damaged index is a view error, and
`slate query` exits 2. CB-29's judges are below.

The lead ruled CB-15b's eight calls on 2026-11-19. The graph's
invalid-query arm cannot be reached, since core's graph build fails
only with `Db`, so a hosted fact pins it through `FetchGateForTests`;
NVDA covers the Connections failure, the inspector's read-only notice
and the search's invalid-query sentence; and the departure from "on
search and on graph" is recorded (Call 1). Core's developer-text
details stay, with one comment on #2012 that carries step 0's table,
and under CD9 the search's invalid-query sentence becomes "Search
error: FTS5 could not parse ...", where it was "Search query is
invalid"; the PR body's "For the owner" section gave the before and
after (Call 2). `GraphConfigStore.Reason` uses the FFI crate's Display
words, on Windows only (Call 3). The CD9 amendment lines go into
contracts 29 (under SD-6) and 35 (Call 4). The Mac gets a #1954 line,
and the PR writes `not delivered: VoiceOver pass`, since no Mac speech
changes (Call 5). Row 11 keeps its real-refusal fact, about 11 s long,
which waits on a condition (Call 6). The canvas that never opened and
says "The previous snapshot is read-only." after a rename is filed as
High, #2019, since its fix changes a contract 34 row and a spoken
sentence (Call 7). Row 7, `CanvasRemoveFromGroup`, is census-only: its
catch needs a race the lease's lock forbids (Call 8). Under NVDA
2026.3.1 every expected phrase was heard on base and head. The search's
`AND` said "Search error: Search query is invalid: FTS5 could not parse
"AND": fts5: syntax error near "AND"" on base and "Search error: FTS5
could not parse "AND": fts5: syntax error near "AND"" on head.
Connections, after its root note was deleted on disk, said "Couldn't
load connections: @path=hub.md, @reason=no such note in the graph
under the active filter" on base and "Couldn't load connections:
Invalid path hub.md: no such note in the graph under the active
filter" on head. New Card on a read-only canvas said "Canvas action
failed: @message=Access is denied. (os error 5)" on base and "Canvas
action failed: Access is denied. (os error 5)" on head. The NVDA pass
also found #2021: a launch is silent at random under NVDA, because
launch lines raised before the window's focus event are dropped.
`ShellFailureDetailCensus` now holds 39 dated rows and 47 reads, which
CB-15c inherits.

The lead ruled CB-31's six calls on 2026-11-19. `index_file` returns
`Result<bool, VaultError>`, `true` when it wrote the path's first row,
and CB-32 meets the changed signature (§6's `session.rs` row) (Call 1).
The helper, `heal_rows_naming_first_indexed`, lives in `links_db.rs`
and replaces `heal_after_create` (Call 2). One Windows twin,
`TheSavedQueryExportHealsTheLinksThatNameIt`, covers the saved-query
export, and every other route has a core fact (Call 3). Call 4 departs from the
recommendation: a repair that first-indexes a path announces Created,
not Modified (`session.rs:9318`). It was fixed in the PR, since
`session.rs` was already changed, with doc 21's two recorded rules
amended (the diff red team's major) and a #1954 line for the Mac's
template list. The reading view's link after a heal got one Windows
probe (Call 5); it was red on the branch, so it was filed as #2023 (an
open reading view keeps drawing a healed link unresolved and refuses
to follow it), and the fact is not in the PR. Step 0's table is on
#1704 (comment 7019271663) (Call 6). Outside the diff, the diff red
team found that a stopped case-only move's recovery deletes the alias
row and leaves the link rows that point at it, which is a comment on
#1981 (7025682510). No NVDA run was owed.

The owner directed the invalid-search change on 2026-11-20: "Plain
words. Please change it." The lead's example, which the owner saw, was
"This search can't be read. Check its quotes and operators." Step 0
ran 77 queries on `main` at c4d1cd60: 49 were refused as `InvalidQuery`
(operators, quotes, and everyday text with one symbol, such as
`#project`, `don't` and `e.g.`), 11 failed as `Db` with "no such
column" (`foo:bar`, `well-known`, dates and links), and 17 ran. The
lead ruled the eight calls on 2026-11-20. The sentence, after "Search
error:", is "Check the quotes, symbols and operators in your search.",
which completes every lead and names symbols, the commonest cause
(Call 1). "Search error:" stays (Call 2). The column-filter, hyphen and
NEAR class takes the sentence too, and #2012 stays open (Call 3).
Bases follows on both of its paths (Call 4). A Mac-only throwaway draft
PR, #2025, showed the Swift test red on `main`'s Mac code, with four
assertion failures; it is closed and its branch deleted (Call 5).
Ordinary text with a symbol refused as a search is filed as #2026
(Call 6). A second refused search in an open overlay is not announced,
consistent with contract 29's S7, and is put to the owner (Call 7). A
damaged full-text index classified as a refused query is this change's
own defect and was fixed in the PR, red first (Find 1). Under NVDA
2026.3.1 every expected phrase was heard on base and head, seven
phrases in all; the search's `AND` said "Search error: Check the
quotes, symbols and operators in your search." on head, and the Bases
preview "Preview failed: file.matches: Check the quotes, symbols and
operators in your search.". The pass also found #2027: Add condition in
the Windows Bases builder moves focus to the Quick filter, on `main`
and head. The whole Windows unit run passed 7,883, with 2 skipped and 0
failed, and `cargo test --workspace` passed 3,328. A
`SlateWindows.Tests` window took the foreground at 01:08:54 without
holding the desktop lock. The comments are on #2012 (7034196605), #1954
(7034192987) and #1527 (7034193336). Contract 29 records the owner's
call under SD-6.

The lead ruled CB-29's thirteen calls on 2026-11-19. Calls 2, 3, 5, 6
and 9 to 13 were taken as recommended, Call 13 with all four IL costs
in scope.
Call 7 departs from the recommendation: a measurement on a Namespace CI
runner is taken, in a column of its own. Calls 1 (one limit, not two),
4 (the limit, recommended at 2 s) and 8 (a real plotting export first)
went to the owner. The CI-runner probe's push was first refused by the
session's permission system, so nothing reached GitHub, until the
owner's word (below). The lead then pushed `claude/cb29-runner-probe`
(3fddedd7) and dispatched run 44673476749, on a Namespace EPYC-v4 with
8 cores and 16 GiB: the runner's decodes take a median 1.13 times the
desktop's, and 1.45 times on stylesheets. At 2 s the desktop refuses
25 documents that decode in under 5 s and the runner 20, and the
runner admits one past 5 s, a single 6.3 s outlier run of one compound
stylesheet document. The probe branch is deleted. The owner's table,
from the shipped guard at 9c23729f over 313 documents, reads:

| Limit | Refused, decoding under 5 s (of them, under 1 s) | Admitted, decoding past 5 s |
|---|---|---|
| 1 s | 55 (4) | 0 |
| 2 s | 25 (0) | 0 |
| 3 s | 15 | 1 |
| 5 s | 5 | 2 |
| 10 s | 0 | 20 |

The largest real document decodes in 76.4 ms. The merge judge then
measured a matplotlib-shaped scatter: at N = 3,000 it decodes in 1,752
ms and is estimated at 2,313 ms, so at 2 s a scatter past about 2,800
points would be refused. Once the limit was pinned, the largest scatter
admitted was 1,951 points at 1 s (a 0.67 s decode), 2,785 at 2 s (1.3
s), 3,425 at 3 s (1.9 s), 4,440 at 5 s (3.1 s) and 6,305 at 10 s (5.9
s), and the lead's recommendation moved to 3 s. Under NVDA, on
9c23729f, head said "Image not shown: it would take too long to
decode." on four slow cards, ran no decoder, and the leaf decided in at
most 1.7 s; base burned 27.5 s of CPU on each abandoned decoder.

CB-29's reviews ran longer than any chunk's so far. The diff red team's
blocker was the guard's own time, unbounded on the caller (a `var()`
by declarations shape took 16.7 s and a duplicate-id fan-out 2.7 s),
fixed with a linear guard and a step budget that refuses with the cost
sentence. Its majors were CSS comment and escape tricks, `@keyframes`
read as rules, and an owner's table drawn from an earlier estimator,
now regenerated from the shipped guard and replacing step 0's table on
#1610. Codex ran 13 rounds, with approves at rounds 4, 8, 9, 10, 11
and 13, plus two launches that returned no verdict. Rounds 1 to 3
found 5, 2 and 3 findings of one class, equivalent inputs the guard
priced apart, and after round 3 the lead ruled the class closed by
structure: every character a scan touches is charged, one multiplier
counts every cloned instance (`use`, `marker` and `pattern`), and
rounds stop when the findings fall in a closed class or belong in the
register. Round 4 approved be8e2de7. The first merge judge, on
be8e2de7
([#2029, comment 7035422298](https://github.com/coryj627/slate/pull/2029#issuecomment-7035422298)),
said Merge once the limit is recorded. Its minors asked for the limit
to be pinned by a fact and a boundary pair, and for cost refusals to be
remembered and the guard's time charged to the note's 20-second
deadline; both were done. Its register point found patterns material
(10,000 rects with a 10-child swatch decoded in 33 to 41 s against an
estimate of 0), so a fifth term was added, and clip paths were measured
as not material. Rounds 5 to 7 each found one false refusal or miss in
the pattern code. The lead ruled that fill and stroke follow the
decoder's cascade, that `currentColor` is a colour and clears a
pattern (reversing the lead's first ruling, once the decoder was
measured), that the whole `var()` class closes in stage 9 against a
measured table, and that cases the guard cannot know, such as
combinator rules, keep counting, with a register line. Round 8
approved 7ae94ac1, which carries the owner's 3 s.

A delta judge read the change from 7ae94ac1 on, in four looks. The
first said Merge after fixes, with two majors: a declaration the
decoder drops still won the guard's cascade (6,000 rects estimated at
0 ms decoded in 13.2 s), and copied custom-property scopes allocated
about 2 GB from a 738 KB file. Both were fixed red first in stage 11
(c9f00133), with its minors and one nit, and Codex round 9 approved.
The second look, at c9f00133, found a reference with `inherit` that
kept a hidden pattern and a scope-cap regression, fixed in stage 12
(1f31be9a); Codex round 10 approved. The third look, at 1f31be9a, found
a reference written as `URL(` and escaped paints in a style attribute;
the lead ruled the class closed by positive recognition (a winning
paint clears a lower pattern only where the guard is sure of it), with
a case-insensitive match and one register line, in stage 13
(5ff69475), and Codex round 11 approved. The fourth look, at 5ff69475,
found G1 to G4: the same rule's last declaration, a rule's
applicability, escaped custom definitions, and a stage 13 regression in
which a comment made a whole stylesheet unsure (a real map file was
estimated at 3,492 ms against a 272 ms decode). The lead ruled all four
fixed, at the level of the rule. The owner was asked whether to end
the judge loop after stage 14, since what remained took hostile input,
stayed bounded by `main`'s 5 s budget and matched `main`'s behaviour,
and answered on 2026-11-20: "Stop after stage 14". So CB-29 merged once
Codex approved stage 14's head and CI was green, with no further judge
round, and later hostile spellings go in the register. Stage 14
(9f462970) fixed G1 to G4. Codex round 12 found a stage 14 regression:
a comment at the end of an `@media` header makes the decoder drop the
block, and the guard stayed sure of it (6,000 rects admitted at 6.75
ms decoded in 11.35 s). Stage 14b fixed it, red first, and the guard
is now sure of an `@media` block only where its header holds no
comment and no escape. Codex round 13 approved 137325cf. The judge's
record is
[#2029, comment 7058816967](https://github.com/coryj627/slate/pull/2029#issuecomment-7058816967).
The whole unit run passed 8,350 with 0 failed on 8e68457a, and the
final head only moves a doc comment. On the Mac lane,
`ConnectionsPanelTests.testTheLoadingRowFollowsConnectionsLoading`
failed twice on 9f462970 and passed when the lead reran run
44866794750; `NoteHeader_AMovedPointerHoversAndAStillClickWorksTheRow`
failed once on 5ff69475, with a 1044 by 788 window on a 1024 by 768
desktop, and passed after. Neither is filed.

The owner's answers in wave C8, all in chat on 2026-11-20:

- CB-29's CI-runner probe: "Push and run the probe". The lead pushed
  the probe branch and dispatched run 44673476749 (above).
- CB-29's decode limit, CD20's second step: "3s". One estimated decode
  time and one limit, as built (Call 1);
  `MaxEstimatedDecodeMilliseconds` is 3,000, and the boundary is a
  scatter of 3,425 points admitted and 3,426 refused. No real plotting
  export was fetched (Call 8). §3.1 has the row.
- The invalid-query wording: "Plain words. Please change it." This
  answers CB-15b's question, whether the search's invalid-query
  sentence, which CD9 had made core's developer text, waits for core's
  reword on #2012. #2028 delivered it. §3.1 has the row.
- CB-29's judge loop: "Stop after stage 14" (above).

CD37: wave C8's start read was posted on 2026-11-19 on #480 (comment
7016355486), #1493 (7016355977) and #1628 (7016356445): no change
since wave C7. `mathcat` 0.7.8 clears the cooldown on 2026-11-23, and
§8's watch row for #1628 is still not met. Wave C9's start reads the
lists again.

Shared files. CB-31 changed `index_file`'s return to
`Result<bool, VaultError>`, which CB-32 meets, and joins §6's
`links_db.rs` row; CB-29 joins the `Reading/` row; and #2028 joins the
`a11y.rs` row with one constant and no event. No §6 row names CB-15b's
canvas files (`CanvasDocumentViewModel.cs`, `CanvasMutationFunnel.cs`),
`FailureDetail.cs`, its graph files (`ConnectionsLeafViewModel.cs`,
`GraphConfigStore.cs`, `GraphDocumentViewModel.cs`,
`GraphPublication.cs`), `Search/SearchOverlayViewModel.cs`, or
contracts 29 and 35. #2028 edited `SearchOverlayViewModel.cs`, its
tests' `GraphSearchFailureDetailTests.cs` and contract 29 after
CB-15b's merge, and also core's `search_db.rs`, `bases/engine.rs` and
`lib.rs`, the CLI's `search.rs` and the Mac's `AppState.swift`. No row
names CB-29's `SvgPreviewGuard.cs`, `HostLog.cs` or its probe tool, or
CB-31's doc 21.

Defects on `main` that wave C8 found and filed, none placed in a wave
yet; each waits for the owner's call, beside #1974, #1976, #1977,
#1980, #1981, #1988 to #1990, #1992 to #1995, #1999, #2000, #2003 to
#2006, #2012 and #2013 above:

- #2019 (CB-15b): a canvas that never opened says "The previous
  snapshot is read-only." after a rename. Filed as High, since its fix
  changes contract 34's A3 table and a spoken sentence.
- #2021 (CB-15b's NVDA pass): a launch is silent at random under NVDA,
  because launch lines raised before the window's focus event are
  dropped (two `main` launches silent, one head launch spoken).
- #2022 (CB-15b's merge judge, by reading): a canvas undo or redo that
  core refuses says nothing.
- #2023 (CB-31): an open Windows reading view keeps drawing a link
  unresolved after core heals it, and refuses to follow it.
- #2026 (the invalid-search change): ordinary text with a symbol, such
  as a tag, a contraction or an email address, is refused as a search.
- #2027 (the invalid-search change's NVDA pass): Add condition in the
  Windows Bases builder moves focus to the Quick filter.

Taken in the wave's own PRs, each red first: CB-15b's test comment
that no session fault is inducible from a real vault; CB-31's
first-indexing repair that announced Modified (ruling 4); and the
invalid-search change's damaged index and damaged schema, each
classified as a refused query.

One question from wave C8 waits for the owner: a second refused search
in an open overlay is silent on both hosts, since its line is the same
and contract 29's S7 announces a summary only when it changes; the
recorded alternative is a sentence for each refused query. The
questions from earlier waves are still open: CB-14's order, which
departs from CD8; whether the MathCAT report is filed upstream; whether
Codex runs at effort xhigh or stays at high; whether the owner's #1991
call covers every route through a tab; CB-28's two points, a bounded
cache of successful decodes and a deadline for the diagram stage,
which CB-29 also left undecided; and whether Windows adopts the Mac's
specific reason when Copy link is unavailable.

The wave left six empty worktree folders on the lead's machine:
`cb15b-core-words`, `cb15b-codex`, `cb31-heal-on-first-index`,
`cb29-svg-cost-guard`, `cb29-runner-probe` and
`invalid-search-plain-words`. A Codex broker, a build or test host, or
an agent's working directory holds each one, and the permission system
refused their removal, so they are left for the owner.

Waves C9 and C10, 2026-11-21. Five chunks merged that day, in this
order: CB-15c (#2032) as 7a34cb6e, CB-32 (#2033) as 46f50603, CB-22
(#2035) as f547d8f5, CB-17a (#2037) as 3c0dbe55 and CB-33 (#2036) as
02fbd37f. Wave C9 is CB-15c, CB-32 and CB-17a. Principal V took CB-15c,
and Principal W took CB-32 and then CB-17a, whose worktree started only
once CB-32 was ready. Wave C10 started early, at about 07:50 Eastern:
V's slot freed at CB-15c's merge, so Principal X took CB-22, whose
shared files were free from that merge, and then CB-33, whose code
waited for CB-32's merge, as §6 asks. CB-17b waited for CB-17a.
Principal Y started it when wave C9 was done; it is in progress, and
this record covers only how it is cut and one owner answer (below).
Wave C10 also gained a chunk, CB-22b (below).

The lead's brief for wave C9 had two errors, and Principal W caught both
before any code. It gave CD22 as selector keys, where the owner's answer
is a fold stored in the index, with an index rebuild. And it gave CD11
by the recommendation, where the owner's answer is that CB-17a changes
no button and every change goes in CB-17b. Both had one cause: the lead
read the plan from a stale copy in its own worktree. The plan is read
from `origin/main`.

Each spec had a step 0 measurement and a fresh Opus red team before
code; CB-17a's step 0 is its scrape, since its NVDA session is its
delivery. The spec red teams found (blockers, majors, minors, and nits,
lows or notes): CB-15c 0, 2, 12 and 12; CB-32 1, 4, 9 and 9; CB-17a 0,
2, 10 and 11; CB-22 0, 1, 7 and 4; CB-33 0, 4, 8 and 3. Each diff had a
fresh Opus red team: CB-15c's found 0 blockers, 0 majors, 4 minors and 9
nits; CB-32's 2 majors; CB-17a's 1 major and 6 minors; CB-22's 0
blockers, 0 majors, 2 minors and 4 notes; and CB-33's 0 blockers, 1
major, 1 minor and 11 notes. Codex approved CB-15c in round 1, CB-22 in
round 1 (d62d4757), and CB-33 in round 1 (a04e042d) and again in round 4
(a1744a20). CB-32 took six rounds and CB-17a three, and the owner
answered a stop question in each (below).

As in waves C6 to C8, each PR had the lead's independent merge judge,
recorded on the PR. Each said Merge, with no blocker and no major.
CB-15c's judge
([#2032, comment 7062804324](https://github.com/coryj627/slate/pull/2032#issuecomment-7062804324))
found 2 minors and 5 nits: the text sinks do not name an exception's
`StackTrace` or `TargetSite`, and an erasure to `object` passes both
rules, which the census documents. All seven are deferred on #1955
(comment 7062804535). With the 23 product files put back to 47bcf77e, 62
of 159 facts failed, each on the dump, the escape or the silence it
names. CB-32's judge
([#2033, comment 7065704598](https://github.com/coryj627/slate/pull/2033#issuecomment-7065704598))
found 1 minor and 5 nits. Minor 1 was fixed on the branch, red first
(0cf51ae0): a cut that lands on an alphabetic combining mark, such as an
Indic vowel sign, an Arabic haraka or a Hebrew point, no longer starts
the row on the rest of its word, and each of the three scripts was red
by mutation. Nit 3, stale doc comments, was fixed. Nit 4 was settled by
a probe on SQLite 3.54.1: a stale expression-index key is ignored, so an
UPDATE and a DELETE succeed, a lookup misses and `integrity_check` lists
the row, and the comments stand. Nits 1, 2 and 5 are on #1955 (comment
7065704776). Codex round 6 approved the delta, and the lead read it.
CB-22's judge
([#2035, comment 7067691403](https://github.com/coryj627/slate/pull/2035#issuecomment-7067691403))
found 2 minors and 3 nits. Minor 1 was that nothing the code points to
tracked CB-22b: the census comment now names the #1955 comment and this
plan's CB-22b, and this docs PR adds the chunk. Minor 2 is the menu
facts' skip on the hosted runners (#1680), with no change. Nit 1, a real
checkbox that is silent before the first task list is published, was
added to the ruling 4 departure text for the owner. Nit 2 fixed a
comment and nit 3 renamed two tests (337ea7c6). CB-17a's judge
([#2037, comment 7069934932](https://github.com/coryj627/slate/pull/2037#issuecomment-7069934932))
found 3 minors and 4 nits, all deferred to CB-17b on #1955 (comment
7069935142). The minors: a witness that skips at run time counts as
running (the `MainMenu` rows and `MenuRowCommandTests` skip on the
Namespace pool, #1680); a witness is not tied to its recorder's root;
and a dated row could be rewritten as an own-press exemption with the
ceiling lowered in the same edit. The nits: two misnamed own-press rows,
a welcome-view reason that contradicts itself, a never-runs check that
omits `SkipTestWithoutData`, and focusless names matched by text.
CB-33's judge
([#2036, comment 7070416010](https://github.com/coryj627/slate/pull/2036#issuecomment-7070416010))
found 1 minor and 3 nits. The minor: three texts named one of the five
uses of a bound value that the guarantee gives up, and all three now
name all five (a1744a20). Nit 2 fixed the doc of `Unconfigured`, and
nits 3 and 4 are on #1955.

The lead ruled CB-15c's eleven calls on 2026-11-21. Step 0, at 47bcf77e,
found 47 reads of an exception's `Message`: 26 change what a reader
meets, 2 change text no reader meets and 19 do not change; rule 2's
trial had 76 hits, none reaching text. Ruling 1 departs from the
recommendation: rule 2's text sinks alone go shell-wide, as a new census
mode with 0 hits, and rule 2's flow rules stay on Bases; the principal
was to stop if the cost passed medium, and it did not, since no non-test
file changed for it. The census's rows, baseline, ceilings and seven row
facts go, and `Holder` stays (ruling 2). Core's words are not changed
here: one #2012 comment carries the developer-text details, the PR's
"For the owner" section has the before and after of the sentences a
reader meets by ordinary input, and #2012's listing restart is not taken
(ruling 3). The three `SavedButUnindexed` reads route now (ruling 4).
Find 2, a deleted file's recovery that landed but failed to index and
says "Can't restore", needs a core and FFI change, so it is High; it is
a #1955 line for CB-46, a named departure from "file it" (ruling 5). The
facts avoid pinning the finds' sentences (ruling 6). Find 1 is filed as
#2031 (ruling 7). Find 3, a faulted toggle on the editor route that
ended in silence, is fixed in the PR (ruling 8). `FailureDetail.Of`
handles the four non-vault error kinds by construction (ruling 9). Find
4, Edit Card Text on a card core no longer holds, is fixed in the PR,
and the same route was swept for other uncaught calls and none was found
(ruling 10). The NVDA pass hears five phrases (ruling 11). Under NVDA
2026.3.1 all five were heard on base and head as the spec predicted. The
property edit on a read-only note said "Property edit failed:
@message=Access is denied. (os error 5)" on base and "Property edit
failed: Access is denied. (os error 5)" on head. The bulk rename preview
said "Rename failed: @message=old_key and new_key are identical" on base
and "Rename failed: old_key and new_key are identical" on head. Restore
As to `../escape.md` said "@path=../escape.md, @reason=path segments
must be non-empty and not self- or parent-references" on base and
"Invalid path ../escape.md: path segments must be non-empty and not
self- or parent-references" on head. The PR body has the reading view's
and the Citations notice's phrases. The pass also heard two things on
both builds. The bulk rename's Preview disables itself and drops the
keys behind its sheet, a #1866 instance (comment 7062230594), which went
to Principal W for CB-17a. And Restore As leaves the keys on its button,
and "History panel." is spoken twice (#1955, comment 7062231830). The
whole solution, run under the desktop lock on fdbe955c, had 12 failures,
each a focus or foreground fact that ran while the two test projects ran
side by side; each passed alone under the lock (245 of 245 in their unit
classes, and 5 of 5 gate facts). `ShellFailureDetailCensus` now holds
the whole shell with no rows, so from this merge a new read of an
exception's `Message` anywhere in the shell goes through
`FailureDetail.Of`.

Four claims in CB-15c's spec were wrong, and its closing record says
how. The text sinks cost no non-test file. The unknown-command arm of
`FailureDetail.Of` renders core's `PaletteCommandNotFound` sentence, a
construction site, so contract 38's ledger moved by two rows, not one.
Row 10's header route needed a failed refresh, since deleting the note
does not reach the arm; `RowWriteAfterHeaderWait` was not driven. And
the disposed-session notice cannot reach a reader, so it has no #1955
line.

The lead ruled CB-32's eight calls on 2026-11-21. Step 0, at 47bcf77e,
found that `[[ΚΟΣΜΟΣ]]`, `[[κοσμος]]`, `[[Κοσμος]]`, `![[ΚΟΣΜΟΣ]]` and
`[[Οδος]]` dangle, that Quick Open and the sidebar filter miss such
names, that a rename of `ΟΔΟΣ.md` leaves `[[ΟΔΟΣ]]` unrewritten, and
that snippets are cut in the middle of a word. A rebuild of the seven
indexes took 0.16 s at 10,000 notes, 1.05 s at 50,000, and 2.2 s at
100,000 notes with a million links. NTFS keeps `ς` and `σ` names apart.
There is one fold: `tree_sort_key` folds `ς` to `σ`, and migration 042
rebuilds the seven indexes that store it. The PR body records both
costs: the Mac's folder order moves (#1954), and a `ς` and `σ` pair
already in a vault acts as case variants do, pinned by a fact (ruling
1). Nothing new is on screen. The rebuild runs in `migrate`'s
transaction, a downgrade gets the existing schema refusal, and a fact
shows that the upgraded index resolves without a rescan (ruling 2). Of
the class members found on the way, the batch move's preflight and the
canvas filter are fixed in the PR; the graph's ghost key (frozen
contract 0b-3) and the graph's name filter (frozen contract 0b-6) are
#1955 lines for the owner; and the Mac's Move to sheet filter is a #1954
line (ruling 3). The snippet's read-time limits are accepted (ruling 4).
The "…" is joined to its word, and `unresolved_links` is trimmed too
(ruling 5). Two Windows twins, and the whole unit run (ruling 6). The PR
ends #1705 and #1782, and no feature issue is filed for raw Markdown in
snippets (ruling 7). Step 0's tables went on both issues before code
(ruling 8). Contracts 32 and 34 each took one appended note that cites
CD22.

Codex ran six rounds on CB-32. Rounds 1 and 2 found three things, each
fixed red first: an upgrade left old sigma resolutions cached, so
migration 042 also sets every internal link whose target holds a sigma
dangling and the opening scan resolves it again; a false link place hid
a snippet cut; and a combining mark joined to a link's bracket could be
split. Round 3 found that in a vault with both `οδος.md` and `οδοσ.md`,
a rooted `[[/οδος]]` can be retargeted to the twin when `οδος.md` is
renamed, because the rename planner saw equal-key candidates in
post-rename order. The lead put it to the owner under the 2026-11-20
rule, and the owner answered "Fix it here": the planner's candidate
order no longer depends on post-rename order, for sigma twins and for
case twins. Round 4 found that a rename of a note whose extension is
written `.MD` left its links unrewritten, which is older than this chunk
and was fixed in the PR. Round 5 approved, and round 6 approved the
merge judge's delta. The whole Windows unit project passed 8,357 with 0
failed and 2 skipped at 8da0171e, in 1 hour 18 minutes; after the
core-only fixes of rounds 3 and 4, the twins, the censuses and the
rename tests passed (193 and 114). The case-twin arm ran on a
case-sensitive directory. CI's one red was contract 34's generated
reconciliation tables, which Principal W regenerated with
`canvas_reconciliation.py --write`. Under NVDA 2026.3.1, Quick Open on
`κοσμος` said "No files matching" on base (47bcf77e) and "1 file
matching" on head, and the Backlinks leaf of `ΚΟΣΜΟΣ.md` said "No notes
link here yet." on base and listed two rows on head. CB-32's count is
13 / 7 / 6 / 0, as its §12 row has it. #2033's body says 8 / 5 / 3 / 0
because it was written before the merge judge: after it, the judge's
minor 1 and nit 3 were fixed, and its nits 1, 2 and 5 were deferred.

Five claims in CB-32's spec were wrong. NTFS does not treat `ς` and `σ`
names as one. The fold is in seven indexes, not six. The `MoveMapping`
mutation as first written could not bite. Design 6, the snippet trim,
needed three changes. And migration 042 also had to resolve sigma links
again. Not verified by anyone: a VoiceOver pass, the Mac's rebuild
timing, APFS's treatment of a `ς` and `σ` pair, and cold-cache timings.

The lead ruled CB-17a's eight calls on 2026-11-21, all as recommended.
CB-17a changes no product code, as CD11 was answered (§3.1). Its spec
sized it L where §5 said M, the lead's ruling took L, and this docs PR
changes §5's row to L. One site per `IsEnabled` trigger, so 19
rows become 41; a keys-first fact names its condition's token, and
#1867's six facts name their overlay (Call 1). Menu rows are not command
sites, which `MenuRowCommandTests` shows; its skip on the hosted runners
(#1680) is named under not delivered (Call 2). The "always on" exemption
is checked by Roslyn in full (Call 3). Five close buttons are dated for
CB-17b, and `FindInNoteClose` is keys-first with a fact (Call 4). A
keys-first fact calls the recorder in its own body (Call 5). The NVDA
session's record is committed under
`docs/plans/18_windows_port/reports/` (Call 6). A red team, then Codex
until it approves (Call 7). The census does not judge the other drivers
that can turn a control off under the keys: a refresh's or a rescan's
deselect, an in-place change of item, a find bar's mode switch. The
ruling was one #1955 line naming that class, 25 command sites and 3
`IsEnabled` rows at the spec's count, with the two alternatives put to
the owner: file the class, or date all 28 for CB-17b (Call 8).

CB-17a's manifest holds 115 disable-site rows: 12 keys-first, 56 exempt
and 47 dated for CB-17b, with the baseline and the ceiling both at 47.
The spec had 57 exempt and 46 dated. The diff red team's major made the
difference: Clear All Marks was wrongly exempt as "its own press never
turns it off". A fact showed it red on `main`: on the canvas's open
marks list the button is stuck off, because nothing raises its command's
`CanExecuteChanged`. The row is dated, a #1955 comment (7067701218)
points to CB-17b, no issue is filed, and the lead keeps the red test's
source outside the repository for CB-17b. Ruling 8's #1955 line was not
posted at the merge, though the manifest's `_doc` cites #1955 for the
other drivers, and #2037's "For the owner" section, as merged, names
only Clear All Marks. This docs PR's worker found the gap, and the lead
posted the line
([#1955, comment 7071845546](https://github.com/coryj627/slate/issues/1955#issuecomment-7071845546)).
The merged manifest holds 35 rows under the own-press exemption. The
question is carried under "For the owner" below. The NVDA session ran
from 13:06 to 13:49 on a Release build of `main`'s product, and its
record is
`docs/plans/18_windows_port/reports/cb17a_nvda_session_2026-11-21.md`.
It heard the launcher's sheet trigger go off. It did not hear the menu's
sheet triggers, because the menu closes before the sheet opens, and menu
rows give the keys back before their command runs. It logged
"unavailable" for Delete, Move checked to Recycle Bin, Cancel import,
Folder note, Remove shortcut, the property row's Save, the base's quick
filter, the Connections depth and the graph inspector's filter. Bulk
Rename's Preview and Apply dropped the keys behind the open sheet. The
lead ruled on what the session found outside CD11. A cancelled Import…
box leaves the keyboard dead until Alt, because the window restored
focus while Import… was off, and the keys rest on the bare window after
the launcher's template picker and Import's folder picker close: both
join CB-17b's scope, first, with restore-after-box facts for Import…,
Delete folder note and the Queries leaf's Delete…, and the launcher's
restore (#1866 comment 7067654509). The Queries leaf does not follow an
on-disk rename of a base file; Tab from a Files tree row stops on the
main window with nothing spoken; and Shift+Tab can skip the action
expanders after a UIA expand, which is unconfirmed: all three are #1955
lines (comment 7067654739).

Codex ran three rounds on CB-17a. Round 1 found four mediums, each a way
a later edit to the test infrastructure could get past the census, and
all four were fixed (eaa8d2c7). Round 2 found one medium of the same
kind, with no instance in today's code: a recorder call in a lambda
handed to `new Action` or `new Lazy` that never runs still counted. The
lead put it to the owner under the 2026-11-20 rule, and the owner
answered "One more fix and round": a recorder call in a lambda counts
only when the lambda is handed to a named runner. Round 3 (49c04eff)
found one more: a property-element binding,
`<Button.IsEnabled><Binding/></Button.IsEnabled>`, makes no disable
site, and the shell's XAML has none. The lead asked again, and the owner
answered "Small fix, no new round": an `IsEnabled`, `Visibility` or
`Focusable` property element fails the census as syntax it does not
read, and the Codex loop ended at round 3 with no approving round. The
PR's head, ae3fc2ae, merges `main` at f547d8f5: #2035's
`EditorSurfaceCommandTests` called `CommandPaletteTests.TheKeysAreIn`,
which this branch had moved, and the merge commit points the call at
`KeysFirstRecorder.TheKeysAreIn`. Every census passed 1,556 of 1,556,
the editor surface, palette and recorder classes 226 of 226, and the
whole unit project 8,476 with 0 failed and 2 skipped at 49c04eff.
`MenuRowCommandTests` and the #1867 menu theory skip on the hosted
runners (#1680) and ran in that local run. Principal W handed back
before CB-17a was ready, so the lead finished it: the checks, the judge,
the merge, the cleanup, the §12 line and the spec's closing record. W's
request to stop leftover `SlateWindows.Tests` processes was refused by
the permission system; it was not done, and it was put to the owner.

The lead ruled CB-22's nine calls on 2026-11-21. Step 0, at 7a34cb6e,
found both commands enabled on every route into reading mode and
`CanExecuteChanged` raised 0 times on a mode switch; Activate at Cursor
toggled a task, followed a link and opened a preview from the hidden
caret; and a Ctrl+click on prose could speak three host sentences.
`ToggleViewMode` tells the coordinator (ruling 1). One guard stands at
each public entry: `ActivateAt`, `ActivateSpan` and `PreviewEmbedAt`
(ruling 2). A consumed click keeps its return value, and only the speech
changes (ruling 3). Ruling 4 departs from the recommendation: a note
changed on disk keeps its reload sentence for a pointer, so the silence
covers only the time the cache loads. Leaving the editor calls
`CloseTransientUi()`, which closes an open popover and a pending
preview, fixed in the PR (ruling 5). A pointer on a real link, embed or
citation while a cache loads still speaks, and is named under not
delivered (ruling 6). The menu fact is a hosted unit fact on a shown
shell under the desktop lock, with its #1680 skip (ruling 7). The spec
red team's major was that "_Activate at Cursor" and "_Actual Size" share
the access key A, so Alt+E, A runs Actual Size wherever Activate at
Cursor is disabled. Ruling 8 fixes the class and not the spelling:
"Actual Si_ze", and an access-key census over every menu level in the
shell. A search result or a Tasks row opened on a reading-mode note is
probed, and filed if real (ruling 9); it was real on three routes, and
it is #2034.

The census's survey brought a second ruling on Call 8 the same day. It
found 18 shared keys in the shell's menus, all in `MainWindow.xaml`. By
reading, in 14 of them the key runs the twin's command at once when the
learned item is disabled. CB-22 fixes the Editor menu's two, "Actual
Si_ze" and "Zoom Ou_t". The other sixteen are dated rows in
`MenuAccessKeyCensus`, on one #1955 line (comment 7066667520). They
are not left for CB-46: the lead cut a new
chunk, CB-22b, to run after CB-17b, when `MainWindow.xaml` is free. It
is an access-key sweep that puts the letters to the owner as one table
and ends every dated row. It also takes the underscore in a data header,
which Principal X's probe proved: in the note header's Folders menu a
folder named `my_notes` shows as "mynotes", because the menu item's
header is the folder's name. This docs PR adds CB-22b to §5 and §6.

The lead accepted two later calls on CB-22. With A and O unique in the
Editor menu, in editing mode Alt+E, A runs Activate at Cursor at once
and Alt+E, O opens Bulk Rename Properties at once, where on `main` each
only moved the keys; the lead accepted it, the PR body names all four
key effects with a line for the owner, and NVDA heard A and O on base
and head. And by Principal X's call, after the diff red team's find, a
pointer keeps the loading sentence on a checkbox the published task list
still names, and prose stays silent. The Windows app job passed 8,449
with 0 failed and 32 skipped, and the local whole run 8,479 with 0
failed and 2 skipped. The 51 new cases passed; 41 of the first 48 were
red on `main`, and the other 7 are pins, each seen red by a mutation. Of
25 mutations 24 were killed; the one that stands is the path equality in
the known-checkbox condition, which the PR names. Under NVDA, in reading
mode, the palette's row said "Selected: Activate at Cursor" on base, and
Enter said "Nothing to activate at cursor."; on head the row said
"Selected: Activate at Cursor. Unavailable: This command is not
available right now.", and Enter said "This command is not available
right now." with the palette still open. After the judge, `main` at
46f50603 was merged in and the head became 337ea7c6.

Four claims in CB-22's spec were wrong. The Editor menu's O pair is not
always enabled together, since Zoom Out is disabled at the minimum text
size. On `main`, Alt+E, E in reading mode runs Preview Embed at once.
Design 5's first operand silenced a checkbox the list still names. And
the shell had 18 shared keys, not 2. Not verified: the three menu facts
on the hosted runners, where they skip (#1680) and which were green
locally; 12 of the 16 census "alone" states, which are by reading;
braille; and the pointer half under NVDA.

The lead ruled CB-33's six calls on 2026-11-21. Step 0, at 7a34cb6e on
SQLite 3.54.1 with 10,000 notes, found that #1917's stated cause is
wrong. rusqlite's statement cache clears a statement's bindings when it
goes back, and 3.54 expires a statement whose plan looked at a bound
value, so a cached statement was re-prepared on every use, whatever was
bound. The class is any bare-parameter equality in a statement with a
join or an ORDER BY, and any bound LIMIT: 38 literals, 16 of which name
a path. A bases row cost 192 µs on `main`, 188 with #1917's rewrite, and
about 103 with the class fixed. Step 0 also found a defect:
`oplog.has_change_since` and `oplog.untouched_for` walk the wrong index,
at 93,000 to 100,000 VM steps a row on a log of 20,000 events. Ruling 1
is the lead's plan call, and it departs from the reading's advice, which
was written before the measurement: SQLite's query planner stability
guarantee (QPSG) is on for every connection core opens, in one configure
function in `db.rs`. With it come a census that refuses a bound `GLOB`
and the conditions under which a `LIKE` could use an index prefix, a
re-prepare counter fact over the whole class, and a fact that walks
every connection. The owner was told, and the call did not wait for an
answer. The PR ends #1917 with its cause corrected. The cache's capacity
goes from 16 to 32, with a fact, at a memory cost of 205 to 532 KB a
connection when full (ruling 2). The five temporal probes name their
index, and both wrong-index defects are fixed in the PR (ruling 3). A
report for SQLite is drafted in the PR for the owner, and no agent posts
it: `sqlite3_clear_bindings` ignores the small-integer mask that binding
honours, which is new in 3.54 (ruling 4). The helpers move to
`session/tests/common.rs` (ruling 5). There is no Windows twin (ruling
6). The lead accepted three wordings where a ruling could not be built
as worded: no connection sets `case_sensitive_like` and no index key is
NOCASE, beside a text census for a bound `GLOB`; the facts walk every
connection they can hold, beside a source census that only `db.rs` opens
one; and 6 of the 38 literals are never cached, so they are named with
that reason.

CB-33 changed three product files, `db.rs`, `bases/engine.rs` and
`session.rs`. Of its 21 tests, 13 were red on 46f50603 for their stated
reasons and 8 are green by design, with about 30 mutations. Release
medians, `main` then the branch: a bases view over 10,001 files, 1,006
then 621 ms; DQL tags, 1,300 then 742 and 988 ms; the link panels of
2,000 notes, 342 then 175 ms; a file summary, 427 then 50 ms; and the
two wrong-index views on a log of 20,000 events, 9.7 and 9.1 s then 0.63
and 0.64 s. Statement memory went from 106,592 to 283,320 bytes. The
diff red team's major was that the anchored branch of the open, which
only macOS takes, could be left unconfigured with no lane to see it,
because no CI lane runs core's tests on macOS. It is now held by a type,
since the open returns a private type that only the configure function
turns into a connection, and by a source census. After the judge's
minor, the doc on `configure_connection`, `BENCHMARKS.md` and the plan
call name the five uses of a bound value that the guarantee gives up: an
index prefix search for a bound `LIKE` or `GLOB` pattern; a partial
index or an indexed expression matched through a bound value; STAT4
sample probes; a bound LIMIT's value in the row estimate and the cost of
a sort; and the selectivity guess for a small integer. The judge also
offered a guard for the second, if cheap. It was built as a reader of
SQL text and taken out again, after Codex rounds 2 and 3 found spellings
it missed: a reader of SQL text cannot stand in for SQLite's parser. In
its place are a pin of SQLite's own list of partial indexes with their
SQL text, and a fact on planted conditions. Product statements are named
as unguarded for that use, and the remedy, SQLite as the judge end to
end, is on #1955 (comment 7070892772). Codex round 4 approved that tree,
a1744a20, which merges `main` at 3c0dbe55. The count was 9 / 3 / 2 / 0
at the READY report and 12 / 3 / 5 / 0 at the merge.

Five claims in CB-33's spec were wrong. The issue's cause, which the
spec corrected at step 0. Fifteen statements were rebuilt in the mixed
rescan, not fourteen. The 20.2 s view was a debug build; in release it
is 9.7 s. "The Linux lane takes the anchored branch" is false. And two
mutations did not exist as written and were replaced. Not verified: runs
on macOS or Linux made locally; a local whole Windows unit run, which
the lane ran; and the six literals that are never cached.

The owner's answers in waves C9 and C10, all in chat on 2026-11-21, each
to a Codex stop question the lead put under the 2026-11-20 rule:

- CB-32's Codex round 3, the twin rename: "Fix it here".
- CB-17a's Codex round 2, the lambda that never runs: "One more fix and
  round".
- CB-17a's Codex round 3, the property-element binding: "Small fix, no
  new round".

A fourth answer came the same day, on a question CB-17b put, and it is
not a stop question: "Stay, and speak on a press". Bulk Rename's
Preview and the Files pane's Expand loaded folders stay available
while they run, and a press made during a run speaks a sentence from
core and does nothing else. The question departed from CD11's words,
and this is the owner's answer to it. §3.1 has the row.

CB-17b, in progress, ships as two PRs. PR 1 is the 32 buttons, with
Import…'s box, Bulk Rename and Clear All Marks. It is Refs #1866 and
takes the dated ceiling from 47 to 15. PR 2 is the fourteen sheets, the
launcher and its restore, and the end of the dated kind, and it closes
#1866. CB-17b's step 0 found one thing no earlier reading had: Space or
a click on any button in a sheet that stays open sends the keys behind
the sheet, because each sheet is a focus scope and `ButtonBase` gives
the keys back when it loses capture. §5's row carries all of this, and
CB-17b's own record comes with its merge.

Wave C11 opened on 2026-11-21, when Principal X's slot freed. Principal
Z has CB-45, the design, which is documents only and Refs #1718. CB-16
waits for CB-17b, and CB-20 is placed after CB-17b, because both edit
the gate (§6).

CD37: wave C9's start read was posted on 2026-11-21 on #1493 (comment
7059486945: `notify` 9.0.0 stable was released on 2026-11-21 and clears
the cooldown on 2026-11-28), #480 (comment 7059487234: no change) and
#1628 (comment 7059487458: `mathcat` 0.7.8 clears the cooldown on
2026-11-23, and the pinned 0.7.7 has been yanked). §8's watch row for
#1628 is still not met. Wave C10 opened the same morning, so its read
was the same one. The lists were read again when wave C11 opened, with
no change (`notify` 9.0.0 clears on 2026-11-28 and `mathcat` 0.7.8 on
2026-11-23), so no comments were posted.

Shared files. This docs PR corrects §6's rows for what these chunks did.
CB-32's rebuild is migration 042 in `db.rs` and did not start in
`session.rs`; it took no `a11y.rs` slot, since the rebuild speaks
nothing; and it changed one doc comment in
`crates/slate-uniffi/src/lib.rs`, on `backlinks`. CB-33 joined the
`db.rs` row, where CB-45b's reader connection must open through its
configure function and join the connections fact, and it did not edit
`links_db.rs`. CB-17a edited no `MainWindow` file. CB-22 edited two
attributes of `MainWindow.xaml`, and CB-22b joins that row after CB-17b.
Two chunks met at a test helper that no row names: CB-17a moved
`TheKeysAreIn` into `KeysFirstRecorder` while CB-22 added a caller, and
CB-17a's merge of `main` fixed the call. No §6 row names CB-15c's other
panel files (`HistoryViewModel.cs`, `RightPanePanelsViewModel.cs`,
`BibliographyViewModel.cs`, `CitationsPanelViewModel.cs`,
`NotePropertiesViewModel.cs`, `SyncDiagnosticsViewModel.cs` and
`TaskIndexRepairCoordinator.cs`), `FailureDetail.cs`,
`Canvas/CanvasDocumentViewModel.cs`,
`Commands/SlateCommandRegistrar.cs`, `FindInNoteViewModel.cs`,
`ShellFailureDetailCensus.cs` or `BasesErrorDetailCensus.cs`. No row
names CB-32's `name_fold.rs`, `link_resolver.rs`, `link_rewrite.rs`,
`sidebar_filter.rs`, `canvas/queries.rs`, `graph.rs` (a doc comment),
the parity golden `links.json`, or contracts 32 and 34; it also changed
`grapheme_key` in `palette.rs`, which CB-34 will meet. No row names
CB-17a's census partial, `KeysFirstRecorder.cs` or its session report,
CB-22's `MenuAccessKeyCensus.cs` and `EditorSurfaceCommandTests.cs`, or
CB-33's `bases/engine.rs`, `db/connection_census.rs` and
`BENCHMARKS.md`.

Defects on `main` that these waves found and filed, neither placed in a
wave yet; each waits for the owner's call, beside #1974, #1976, #1977,
#1980, #1981, #1988 to #1990, #1992 to #1995, #1999, #2000, #2003 to
#2006, #2012, #2013, #2019, #2021 to #2023, #2026 and #2027 above:

- #2031 (CB-15c): History's Compare against current says "no such
  version" after the note was changed outside Slate. Its fix diffs
  against the disk's text, which needs a core API or a design, and the
  Mac's twin.
- #2034 (CB-22): a search result, a Tasks panel row or a heading link
  opened on a note in reading mode moves only the hidden editor's caret,
  and the heading route says "Scrolled to X." It needs a design for
  where the reading view lands.

Taken in the waves' own PRs, each red first. CB-15c: Edit Card Text on a
card core no longer holds, which let core's refusal escape uncaught; a
faulted toggle on a route with no completion, which ended silent; and
three wrong comments. CB-32: the batch move's preflight and the canvas
filter, which did not use the fold; a rename of a note whose extension
is in capitals; a rename of one of two equal-key twins; and two docs
that said SQLite reports a stale expression index. CB-22: an open
popover and a pending preview that outlived the switch to reading;
`ActivateSpan` acting in reading mode; and the two shared access keys of
the Editor menu. CB-33: the two temporal probes that read every note's
events for each row; a statement cache too small for a mixed rescan; and
the class beyond `files.path`. CB-17a fixed nothing, as CD11 was
answered.

Corrections this docs PR makes to the plan and its reading notes, each
at its line:

- §2.3's third row said that the self-disabling groups "have never been
  heard either" and counted them as "about twenty buttons". #1866's
  comment of 2026-11-17 heard Bulk Rename's Apply, and the class is 47
  dated rows. The row is edited, with a note under the table.
- §2.2's line on #1917, and `reading/07_core.md`'s section on it, gave
  the cause as a re-prepare on each new binding, and the reading advised
  against QPSG. By measurement a cached statement was re-prepared on
  every use, and the setting is what shipped. The reading's lanes line
  now says that only macOS takes the anchored branch of the open and
  that no lane runs core's tests there. The reading holds no sentence
  that says the Linux lanes cover every core path; that claim was in
  CB-33's spec, whose closing record corrects it.
- `reading/05_spoken_failures.md` named the planned fact
  `ACtrlClickOnPlainProseStaysSilentWhileTheCacheLoads`, which shipped
  as `ACtrlClickStaysSilentWhileTheNotesFirstTaskListLoads`. Its
  "nothing asks for these two commands" was too strong: the registrar
  raises them on tab activation, open, close and split, and never on a
  mode switch.
- §5's CB-17b row gains the scope CB-17a's delivery gave it, and §5's
  CB-38b row says its spec reads CB-22's.
- §5's CB-17a row is sized L, where it said M: its spec sized it L, and
  the lead's ruling took L.

For the owner. These questions from waves C9 and C10 are open:

- CB-15c: does "Plain words" (2026-11-20) extend to the developer
  sentences a reader now meets by ordinary input? #2032's "For the
  owner" table has each before and after, and #2012 holds the wider
  table.
- CB-32: the graph's ghost key and the graph's name filter still
  lowercase with context, so `[[ΑΓΝΩΣΤΟΣ]]` and `[[αγνωστοσ]]` draw two
  ghosts and `ΚΟΣ` misses `κοσμος`. Frozen graph contracts 0b-3 and 0b-6
  pin them, and only the owner can amend those (#1955).
- CB-22: in editing mode Alt+E, A now runs Activate at Cursor at once
  and Alt+E, O opens Bulk Rename Properties at once, so a reader who
  learned "Alt+E, A, Enter" gets a line break in the note from the
  Enter. And a Ctrl+click on a real checkbox is silent before a note's
  first task list loads, where `main` said the loading sentence.
- CB-33: the lead's plan call, the stability guarantee on every
  connection, with the five things it gives up; and the drafted report
  for SQLite, in #2036's body, which no one has posted.
- CB-17a: the other drivers that can turn a control off under the keys,
  which the census does not judge. The class is one #1955 line
  (comment 7071845546), and the owner has three ways to take it: leave
  the line, which the lead recommends; file it as an issue; or date
  all of its rows, so that CB-17b or a later chunk covers them with
  keys-first facts (28 at the spec's count; the merged manifest holds
  35 rows under the own-press exemption).
- CB-22b's letters. They came to the owner as one table, and the owner
  answered on 2026-11-23, "Approve the table (Recommended)" (§3.1;
  corrected on 2026-11-26).

The questions from earlier waves are still open: a second refused search
in an open overlay, which is silent; CB-14's order, which departs from
CD8; whether the MathCAT report is filed upstream; whether Codex runs at
effort xhigh or stays at high; whether the owner's #1991 call covers
every route through a tab; CB-28's two points, a bounded cache of
successful decodes and a deadline for the diagram stage; and whether
Windows adopts the Mac's specific reason when Copy link is unavailable.

These waves left five empty worktree folders on the lead's machine:
`plan-47-cb15c`, `plan-47-cb17a`, `plan-47-cb22`, `plan-47-cb32` and
`plan-47-cb33`. A process holds each one, so they are left for the
owner.

Wave C9 is done: CB-15c (#2032), CB-32 (#2033) and CB-17a (#2037). Wave
C10 started early, when Principal V's slot freed. It has CB-22 (#2035)
and CB-33 (#2036) merged and CB-17b in progress, as two PRs, and CB-22b
follows CB-17b. Wave C11 opened on 2026-11-21, when Principal X's slot
freed: CB-45's design is in progress, and CB-16 and CB-20 wait for
CB-17b.

Waves C10 to C13, 2026-11-21 to 26. Fourteen PRs merged after #2038, in
this order: CB-45 (#2042) as df1c1147, CB-17b's first PR (#2041) as
ad40500d and CB-20 (#2044) as 68e4f974 on 2026-11-22; CB-17b's second
PR (#2045) as 38f6edde, CB-42 (#2047) as 5e0846f5, #2052, which is not a
chunk (below), as ef0c780f, and CB-23 (#2049) as f363a597 on
2026-11-23; CB-22b (#2055) as ad04a470 and CB-34 (#2058) as fce6a6e1 on
2026-11-24; CB-16 (#2061) as b19c94ae on 2026-11-25; and CB-36's first
PR (#2066) as 01b293b3, CB-21 (#2072) as b2ebcbb9, CB-36's second PR
(#2073) as 20f7a8ab and CB-35 (#2077) as 998db5dc on 2026-11-26. The
dates are Eastern, as in the rows above. Wave C10 ended with CB-17b and
CB-22b, wave C11 with CB-45, CB-20 and CB-16, wave C12 with CB-42,
CB-23 and CB-34, and wave C13 with CB-36, CB-21 and CB-35. §6 says how
the waves overlapped and which principal took which slot.

The account's Fable limit stopped the work again on 2026-11-22, until
2026-11-28 at 04:00 Eastern. Principal Y stopped partway through the
step 0 probes for CB-17b's first PR, and Principal Z had handed CB-45
back to wait for rulings. By the owner's precedent of 2026-11-17 (Wave
C5, above), new principals ran on Opus at effort xhigh from the saved
specs and worktrees: Y2 for CB-17b and Z2 for CB-45. Principals U, Q, P
and O are recorded on Opus at xhigh too.

Each chunk spec had a step 0 measurement and a fresh Opus red team
before code, and each product diff a fresh Opus red team. The spec red
teams found (blockers, majors, minors, and lows, nits or notes where
they were counted): CB-17b 1, 10, 12 and 7, and on its second PR's spec
delta and design 0, 5, 4 and 6; CB-45 0, 4 and 27, and on the design
document 0, 3 and 21; CB-20 0, 4, 12 and 5; CB-42 1, 4 and 12; CB-23 1,
3 and 14; CB-22b 0, 5, 10 and 9; CB-34 0, 4, 9 and 2; CB-16 0, 3, 8 and
10; CB-36's first PR 0, 9 and 14, and its second 2, 14 and 16; CB-21 1,
3 and 14; and CB-35 0, 11 and 14. The diff red teams found: CB-17b's
first PR 0 blockers, 0 majors, 2 minors and 6 lows, and its second 0, 1,
2 and 7; CB-20 one blocker (B1, below) and one major; CB-42 0, 2 and 9;
CB-23 1, 2 and 6; CB-22b 0, 2, 3 and 5; CB-34 one major; CB-16 0, 0, 3
and 5; CB-36's first PR 1, 7 and 8, and its second 0, 1 and 10; and
CB-35 0, 3 and 11. CB-21's record does not count its diff red team's
findings. Codex approved CB-42, CB-23 and CB-22b in round 1, CB-17b's
first PR in round 1 and the delta its judge caused in one more round,
CB-17b's second PR and CB-36's first in round 2, CB-36's second in
round 2 and again in round 3, and CB-16, CB-21, CB-34 and CB-35 in
round 3. CB-45 took eight rounds, with approvals in rounds 4 and 8, and
CB-20 nine; in each the owner answered a question about the loop
(below).

Each chunk PR had the lead's independent merge judge, recorded on the
PR.
Codoki is at its monthly limit until 2026-12-01 (Wave C6, above), so
each judge stood in its place, as §2.6 allows. Every verdict but one was
Merge or Merge after fixes, with no blocker; the one Hold was CB-20's
first judge (below). The verdicts: CB-45, Merge, 0 blockers, 0 majors, 6
minors and 6 nits, all folded into the design before the merge (comment
7081502237); CB-17b's first PR, Merge, 0, 0, 1 and 5 (comment
7081935342); CB-20, Hold, 0, 1, 4 and 8, then Merge on the delta, 0, 0,
2 and 5; CB-17b's second PR, Merge, 0, 0, 2 and 1; CB-42, Merge, 0, 0, 2
and 6; CB-23, Merge, 0, 0, 2 and 3; CB-22b, Merge after fixes, 0, 1, 1
and 3, the major older than the chunk and filed on #2050 (comment
7121357425); CB-34, Merge after fixes, 0, 1, 4 and 4, the major being
the owner's "Silent follows" itself (comment 7149589874); CB-16, Merge
after fixes, 0, 0, 3 and 4 (comment 7171264604); CB-36's first PR,
Merge, 0, 0, 1 and 4 (comment 7184421265); CB-21, Merge, 0, 0, 1 and 7
(comment 7202101830); CB-36's second PR, Merge, 0, 0, 1 and 8 (comment
7207422411); and CB-35, Merge, 0, 0, 4 and 4 (comment 7207754287).

The lead ruled CB-17b's seven calls on 2026-11-21. Step 0, at 3c0dbe55
with 27 probe cases, found that Space or a click on any button in a
sheet that stays open sends the keys behind the sheet; that the
launcher's restore fails because the row goes off as the sheet opens;
and that six dated rows go off in a later publication, so the fix is one
seam where the shell's commands raise. CB-17b ships as two PRs (Call 1).
The first PR holds D1, a sheet keeps the keys, and D2b, keys that
return to nothing are landed (Call 2). The keys go where they go today,
only earlier (Call 3). Call 4 went to the owner, "Stay, and speak on a
press" (§3.1). No core sentence fitted the press, so core gained
`WorkStillRunning`, and the owner answered on its three sentences,
"Wording is fine". The census gains a stays-on kind and a frozen table
of the 47 keys (CB-17a's judge's m3), and a skip rule with the menu's
17 pinned (its m1); its m2 stays on #1955 (Call 5). Import's real box
gets hosted facts and NVDA, and a gate journey because the gate already
drives a
system box, the history restore's, through `window.ModalWindows`; the
first PR adds the journey in a partial file of its own (Call 6).

CB-17b's first PR (Y2) ran its whole unit project with 8,661 passed, 2
test-only failures fixed and 2 skipped. Under NVDA, base then head: a
cancelled Import… box now says "Import…, button" and F6 moves on; Delete
and Remove shortcut lose "unavailable"; Bulk Rename's Preview and Apply
pressed by Space keep the keys in the sheet; the preview and rename
sentences were heard; and Clear All Marks says "Cleared 1 mark.".
"Folders are still expanding." was not heard, and a fact holds it. The
property row's Save still says "unavailable": NVDA drops the field's
late focus event when the republish replaces the field within about
100 ms. The owner answered "Accept and file it (Recommended)" on
2026-11-22, and it is #2040. The judge's minor, a held Enter on Import…
that reopened its box on every repeat, was fixed with an Enter-repeat
fence and a class check, with nits 2, 4, 5 and 6; nit 3 went to #1955.
The count was 14 / 8 / 5 / 1 at the READY report and 21 / 14 / 6 / 1 at
the merge. `main` then went red on ad40500d once, in the shell gate's
`ThemedShell_TheMenuBarAndTheOpenTabAreReadable` ("The File row never
showed on the screen", the known hosted flake), and its rerun was
green.

CB-17b's second PR found 55 of its 56 cases red on `main`. Its spec
delta and design widened D9: a remembered element is cleared, the sheet
takes the keys from anywhere outside it, a sheet that opens beneath
another waits until it is on top, and the topmost sheet is landed on
activation or a box's close. The third is a defect on `main`: Files
Citing presented under Settings took the keys out of Settings. The
product code is one landing routine, `LandInOpeningSurface`, for 17
surfaces, with the menu's one keep-on trigger and no launcher trigger;
the census worker froze the 47-key table and ended the dated kind. The
diff red team's major, also on `main`: a closing sheet's restore landed
the keys behind a sheet still open, and it now hands over to the
topmost sheet's landing. The whole unit project ran 8,782 with 4 failed,
where one fact pinned Move To's queued landing and was rewritten; of 23
mutations 20 were red, and the PR names the two guards no product route
reaches and one rule it removed. The NVDA spot check found the template
picker's momentary hold heard as "unavailable" on its first step (A1),
which was fixed red first; on head, Escape from it says "New Note from
Template…, button", where base said nothing and left the keys on the
window. The judge's two minors and its nit were fixed on the branch: a
sheet with no restore left the keys beneath the open sheet until a
Background landing, the property restore's give-way branch had no fact,
and the window's return landed a dead control on Settings' Close.

The lead ruled CB-22b's calls 3 to 10 as recommended on 2026-11-23,
after the owner's two answers (§3.1). The underscore class is fixed with
text-block headers, the embed cards' among them, and
`EmbedPreviewTitleCensus` changes, so the chunk is M. The rest of the
peer-name class is filed as #2050, apart from Jump to source, which is
fixed here; the menu commands that run with nothing said are #2051,
with Forward added; and the census's waiver mechanism is gone. Worker
A's 22 rows were red on `main`, and worker B's facts were red on
`main`, green on the trial and red under mutation. The judge's F1 was a
defect in the chunk's own feature, so it was fixed on the branch and
withdrew the lead's earlier acceptance of the diff red team's minor 5:
the embed card's title and Jump label now paint their presenter's
foreground, by a fact seen red and then green. The judge's N1 narrowed
the inline tripwire, and N2 put the local command for the menu facts in
the PR body, where 1,939 of 1,939 passed. Five claims in the spec were
corrected: the reason `AChangedItemRunsOnItsOwnKey` is red, the edit to
`NoteHeaderTests`, the embed card's Tab stop, which is its Expander
group, the second R of Alt+W, R, R, Enter, which types "r" and makes a
setext heading, and Forward, which is silent too. `main` went red on
ad04a470 once, in the shell gate's
`ReadingDiagram_LineReadingSpeaksTheDescriptionAndThePlaceholder` (the
rendered diagram's line read "B --> C"; 238 of 239 passed), though the
same tree passed on #2055's head; its rerun was green. It was the first
sighting of that journey failing on a hosted runner.

The lead ruled CB-45's eight calls on 2026-11-22. Step 0, at 02fbd37f
with 10,000 notes: a rescan holds the one connection 0.5 to 1.1 s when
nothing changed and 4.5 to 10.7 s when everything did, and every read
waits for it; the cache has been in WAL since 9a82334f; a read-only
second connection answered Quick Open in 39 to 55 ms throughout a held
scan, opened in 0.2 to 1.8 ms and cost about 2.3 MB; and it must close
before the main connection and never keep a snapshot, since one held
snapshot doubled the log from 21.5 to 43.0 MB. The design has three
phases, CB-45b, CB-45c and CB-45d (Call 1). Call 2 went to the owner
(§3.1). CB-45b changes no FFI (Call 3). The fallback's wait becomes
cancellable (Call 4). The read connection opens on first use (Call 5).
The Windows UI-thread freeze is filed, as #2039 (Call 6), with lines on
#1954 and #1955 (Call 7), and the design lands under `specs/` (Call 8).
The design red team's first major became Call 9; its second gave each
phase its own owner call and AR row, and CB-45d an NVDA spot check; its
third binds the read connection's generation into CB-45c's cursors.
Codex's findings in rounds 1 to 3 were all about what a reader hears or
keeps; they widened Call 9 and brought Call 10 to the owner, who
answered "Run round 4 (Recommended)". No round broke a core invariant
of the read connection. The judge's six minors were folded into the design
before the merge, among them the rules for a null, default or
superseded selection and a pre-scan row that fails to load. After the
merge the owner read the design and confirmed its two points, so CB-45b
is unblocked. Nothing past step 0 ran.

The lead ruled CB-20's calls on 2026-11-22. Step 0, at df1c1147 under
NVDA 2026.3.1: a rendered diagram's line said "grouping"; a pending
diagram's last line said "B --> C" and "grouping", and its name was
never spoken; a failed diagram reads its text; a thematic break said
"separator, unavailable"; an embed image's line said "blank"; and after
a late landing NVDA+Up read the line above, on `main`. A spike of three
placements of the hidden "Diagram, rendering" read cleanly in all
three. The owner answered Calls 1 and 4 (§3.1). The gate's named census
is updated in the main partial (ruling 2). A separator is enabled, so
it says "separator" (ruling 3). Find skips the hidden description
(ruling 5); a pending diagram's copy drops the hidden name, and a
rendered one's carries the fence (ruling 6). Math takes the same
hidden-speech mark (ruling 7). The census reflects over
`ReadingBlockKind`, beside a bound site census, and CB-26 adds its
callout row (ruling 8). The stale line under NVDA+Up is filed, as #2043
(ruling 9). The spike's record is on #1708 (ruling 10). After a landing
the caret anchors at the paragraph's start, and three WB-20 facts are
rewritten for it (ruling 11); delivery moved the landed caret to just
after the element, with B1's fix. The diff red team's blocker, B1, was a
caret drawn 0.13 px tall beside a hidden run, on the diagram's landing,
the image's line and the pending name's line, and on math's since
#1595. Its planned fix added caret stops, so the lead held it for one
that added none, and the owner accepted the column trade that came
with it (§3.1).

Under NVDA, base then head: the rendered diagram's line "grouping"
became "Flowchart with 2 steps." and "grouping"; the pending diagram
said "Diagram, rendering" and "grouping" above its source; the image's
line "blank" became the card's name; the rule "separator, unavailable"
became "separator"; and the formula's stops did not change. Narrator
said "Diagram, rendering flowchart LR" on the pending diagram and
"Flowchart with 2 steps. After the diagram." on the rendered one. The
whole unit run at 9568581b had 8,734 passed, 1 failed and 2 skipped: the
focus manifest census flagged an `Inlines.Remove` on a scratch
document, and b3645827 empties the runs in place instead. The first
judge said Hold, for one major: a mouse press to the left of an element
moved the caret past it on the press, so a drag and its copy left the
element out. It is fixed by moving the caret at the release. Three of
its minors were fixed too: a wrap that could trap Up, a restore after a
republish taken as a move from another line, and a copy that
substituted a fence and so dropped rich text and pictures; every
changed copy or drag now keeps them, math included. The fourth,
"Diagram, rendering" as text the host composes, is a #1955 line. Codex
rounds 6 and 7 were fixed red first.
Round 8 found that a caret moved away and back within one dispatcher
turn after a click could be moved again, through in-process UI
Automation only; the owner answered "Small fix, no new round
(Recommended)", and 184328c4 cancels the queued release on any
selection change. A round 9 that had already started approved it, and
nothing was taken from it. The real-mouse gate fact was made
independent of the screen's size after CI's smaller screen failed it.
The delta judge said Merge: N1 and N2 were fixed on the branch, N3 in
part, and N6 is a #1955 line. The NVDA and Narrator passes ran on
b3645827; the later commits change mouse, restore and copy behaviour,
not speech.

The lead ruled CB-42's six calls on 2026-11-23, all as recommended;
Call 3 filed the silent extra reload, as #2046. Step 0's record is on
#1625 (comment 7110957425). CB-42 changes test files only. Its whole
unit run had 8,757 passed, 0 failed and 2 skipped, and the judge's two
minors and six nits were taken in one commit of tests and comments.
#2046 waits for the owner, since its fix reopens CD30's declined option
(c).

#2052 is not a chunk. `main`'s Windows run on 5e0846f5 (run
45291230398) passed every test, and then its app job reached its
60-minute limit during the format gate and was cancelled, since the
unit run alone now takes about 54 to 55 minutes. Its rerun was green.
#2052 raised the `windows` job's limit to 80 minutes, which stays under
the home lane's 100-minute app cap, and its own app job took about 43.5
minutes.

The lead ruled CB-23's calls on 2026-11-23, after the owner's answers
(§3.1): the line is the panel's focus stop, which makes the chunk M
(Call 3); one enum for each dimension and one function (Call 4); no
VoiceOver pass is owed, and #1954 is corrected (Call 5); and the chunk
proceeds in wave C12 (Call 6). Its whole unit run had 8,891 passed, 0
failed and 2 skipped, and 11 mutants were red. Under NVDA on head, F6
from the editor says "Math for this note isn't listed here yet. Open the
note in the reading view (Control Shift E) to read its math." and then
"Math panel."; on base F6 went to the rail, and the line was reachable
only by object navigation. CI failed CB-17b's second PR's fact
`SheetOpeningTests.ASheetWithNoRestoreClosedOverAnotherLandsTheOneLeftOnTop`
twice on hosted runners, with nothing of CB-23's on its path. A probe
run (45292145048) found two races. The fact did not wait for Move To's
listing to publish, and won by less than one 16 ms tick; the fact now
waits, as "Found and fixed" on #2049. And in the product, Move To's
listing, published off the UI thread while the card editor is on top,
clears Move To's sheet, so the reader comes back to nothing; that is
#2053. The judge's two minors and three nits were taken in the same
push, and a new fact found that a leaf switched under the keys while
they rest on a leaf's notice lands them on the rail, older than CB-23;
that is #2054.

The lead ruled CB-34's calls on 2026-11-24. Step 0: on `main` 16 of 189
typed command names lead with another command; with a fixed ranking,
contract 28's P7 still runs the open's row in 282 of 35,721 pairs; and
the follow rule gives 0. The ranking is D2: an exact label, an exact tail,
the score, a label over a hint, the section, the id, with sections
ordered by their best row (Call 1). Call 3 went to the owner, "Silent
follows (Recommended)" (§3.1). There is no Swift change, with a #1954
line (Call 5); both doc comments in `crates/slate-uniffi/src/lib.rs`
are fixed, and CB-16's and CB-45b's code waited for CB-34's merge (Call
6). The others, a gate leg, a census, NVDA measured first at 60 and 100
ms a key, and a row that takes the keys becomes the reader's, were as
recommended. The full NVDA pass, 61 launches on base and head, heard
typed names run their command 15 of 15 at 100 ms, at 60 ms and in a
burst, so Call 9 needed no owner question; a click is named by NVDA's
focus; and the two quiet cases are quiet on head. The diff red team's
major, that Tab into the list made the palette's row the reader's, and
Codex round 1's two mediums were fixed red first. After Codex approved
in round 3 (d50cdc21), one commit turned off the list's letter search
and fixed a stale census row, with no further round, by the lead's
ruling. The whole unit run had 9,024 passed, 1 failed (that stale row)
and 2 skipped. Five claims in the spec were corrected: the census on
`main` fails 33 spellings, not 16 tails; Call 10's split does not
happen; the type-name letter search moved nothing; Close Pane is
unavailable with one pane; and M10's witness was misplaced. The arrows
inside the list move the selection but not the keys, on `main` too, and
need a speech decision: #2057.

The lead ruled CB-16's calls on 2026-11-24. Step 0: a cancel says
nothing; core's write takes no cancellation token (664 ms at 50 MB);
#1791's line sticks on three routes; the sidebar filter sticks on a
close's cancel and on spaces; and the Bases export drops a failure with
nothing said. The owner answered Calls 2 and 4 (§3.1). Calls 1, 3, 5, 6
and 8 were as recommended. Under Call 7 both filter paths and
`TabFocused` are fixed here, a filter re-run after a refused close is a
#1955 line, and the silent drop is filed, as #2056 (Bases export and
Copy as Markdown). The lead relaxed the order so that CB-16 branched and
wrote everything but its `lib.rs` edit while CB-34 held that file. Codex
round 2 found an import that counts a file which landed but was not
indexed as not imported; it is older than the chunk and needs an FFI
change, so it was declined and filed, as #2060, beside #2059 (F6 to the
status bar does not name a running import or trash). After Codex
approved in round 3 the NVDA spot check found that a press of the
status bar's Cancel put the keys back on the status bar, and NVDA read
the status bar's long help before the Medium "Cancelling import…",
about 11 s late. The lead ruled it High (§3.1). On head the sentence
came 82 ms after a press of the status bar's Cancel, against 11.5 s, 86
ms after the Files pane's, against 1.9 s, and 25 ms after Escape. A
second commit raises the summary before the import's flags clear, as
the trash does, which reverses the diff red team's finding 3 on NVDA's
trace; the summary with no press then came 36 and 41 ms late. The
judge's minors were taken: an announcement that throws can no longer
strand the flags, a Narrator spot check ran, and the trash's Cancel
press at Medium is filed, as #2062. The Narrator check found that
Narrator cuts a summary with no press when the work ends with the keys
on its Cancel, for import and trash, measured with a loopback level
meter: #2063. It also heard "Status bar disabled." on the no-press
case. That was the hidden Cancel's `IsEnabled` change, which UI
Automation hands to the nearest shown ancestor; `StatusBarCancelButton`'s
peer keeps its last shown `IsEnabled` while hidden, by a fact seen red
first, and NVDA and Narrator were checked again once each. The lead
read that commit, which no reviewer saw. The residual: an Invoke on the
hidden Cancel through UI Automation passes the peer's check and reaches
a command whose `CanExecute` is false.

The lead ruled CB-36's first PR's eight calls on 2026-11-25. Step 0, at
b19c94ae: the board paints its cards in dictionary order, and a
client's first `ItemContainer` search for an off-window card or edge
returns null, in process and out (WPF's `ProviderFromPeer`); a prototype
`TrySetParentInfo` connection made every probe green, and 197 canvas
facts stayed green. The off-window seat is fixed here (ruling 1), with
hosted facts and a gate journey (ruling 2) and an NVDA spot check on
base and head, Narrator not delivered (ruling 3). The next-item walk
pans the board item by item, accepted and put to the owner's eye
(ruling 7). The whole run had 9,066, with 9,064 passed and 2 skipped,
and no NVDA phrase differed between base and head. The diff red team's
blocker (an item-names census) was fixed, five majors were taken, and
two were declined with evidence. The judge's minor, older than the
chunk, is #2068: a held placeholder's Realize pans a hidden board and
drops the reveal it owed. It was filed after the merge, so the PR's
body says 6 / 2 / 2 / 2 and the row above says 7 / 2 / 2 / 3. The
graph diagram's Selection has the shape the board's had: #2065. And
NVDA's object navigation skips a labelled connection, whose control
type needs the owner: #2064.

For CB-36's chips, the covering-state scan was written first and failed
on `main`: the edge's element, 4,864 by 3,801, covers a board of 544 by
532, and the scan reports
`BoundingRectangleCompletelyObscuresContainer`. The owner read the
design and answered on 2026-11-25 (§3.1). The second PR's spec red team
found that two premises behind those answers were false: the Mac's
chips cover in Swift dictionary order, not document order (#2069), and
the proposed outline's colour equals the card border's in every theme.
A prototype of the bounds alone made the scan clean, and the owner,
asked again on 2026-11-26, kept all three answers and added a click on
a chip that acts on the card under it. The lead ruled the eleven calls
as recommended; Call 4, which pans the board to contain the chip and
supersedes contract 34's D5 sentence by an append, goes to the owner's
eye. The whole run had 9,094, with 9,092 passed and 2 skipped; the
canvas journeys passed 12 of 12, and 30 mutations bite. On
`large_2000.canvas` installing the chips costs 2.8 ms and a point query
0.17 ms more. CI went red once, on contract 34's generated
reconciliation tables, which were regenerated. The judge's minor was
that the append said, unqualified, that a pointer on a cut-off chip
shows its full label, which is false while a truncated card is seated.
The lead ruled a delta of documents only: a qualifying sentence, ID-9's
edge-by-focus cell left open and pointing at #2076, and the tables
regenerated. NVDA speaks a card's title twice, its name and its help
text (#2074), and `CanvasReconciliationCensus` does not check the
divergence table's line numbers (#2075). Four claims in the spec were
wrong: step 0's hover probe, which ran with the card seated; a drawing
visual's identity, since it hands out a new copy at each read; that a
landing seats a card; and one realization fact's premise.

The lead ruled CB-21's calls all as recommended, and its diagnosis was
posted on #1330 before any product code. WPF's
`TextRangeAdaptor.GetChildren` answers null, the reading view's
decorator passes that on, and Narrator abandons its selection speech;
an empty array fixes it ("body selected"). Eleven other routes are
silent. The rectangle walk, 1.8 to 5.5 s over 2,000 items, is clamped to
the view. The fix is route A; #2067 offers the owner an app-wide hook
and a report to dotnet/wpf for the text fields; there is no new
journey; and the Notification route is recorded. Narrator said "a"
only on base and "<text> selected" on head, and NVDA was the same on
both. The whole run had 9,084 passed and 2 skipped. The judge's minor:
the public `GetVisibleRanges` still threw on a one-line right-to-left
view, while `VisibleSpans` recovers, a find the PR made and recorded
nowhere. It was fixed on the branch red first, with nits 1 and 5, and
the count went from 3 / 1 / 0 / 2 to 4 / 2 / 0 / 2. A reading-view
range across a table answers rectangles for its first column only:
#2071.

The lead ruled CB-35's calls on 2026-11-25: twelve members; the grid
and outline fields split off as CB-35b (#2070); each field's setter
settles its list (step 0b); letter search off on four lists, to the
owner's eye; the arrows show the selection; the pickers keep their
selection; visibility flips are in scope; the residual is a #1955 line;
a timed NVDA check of at least 34 runs a surface; and the shared-file
rows this docs PR adds (§6). The NVDA probe heard no stall in 34 runs
on head and in 68 on base for Quick Open and for search, and its
positive control none in 20, so the probe cannot see this kind of
stall, and the PR says so. Codex round 1 found two highs, a refresh of
Quick Open and a tag activation in the Files filter that publish with
no settle, fixed facts first. The judge's M1, a Search arrows fact that
compared the list with itself, was fixed red first; its M3 turned off
the Files filter's letter search too, so five lists; M2, a UI Automation
Select that no longer scrolls Search's list, is a #1955 line; and M4,
the arrows moving the selection and not the focused row on Quick Open
and search, is a comment on #2057. Whether a keystroke's setter settles
inside a TSF write lock under IME composition, touch-keyboard
autocorrect or dictation was not measured, and it is on #1955 and under
not delivered. The changed classes and censuses passed 1,826. On the
final head the shell gate failed twice, on journeys the PR does not
touch: the palette journey's poll met a list row that stopped answering
its Name, and then the reading view's press-beside-an-element journey
found no margin beside the diagram's line. The second rerun was green,
and the two are filed as #2079 and #2080. Five claims in the spec were
wrong, and its closing record says how: a settle when the selection
moved pulled a reader's own scroll back; `Show` for a publication
scrolled Move To's sheet at twice the text size, so publications use
`ShowInside`; the field's own relayout stays pending after a
keystroke; letter search came off five lists, not four; and some
publishing paths were not listed.

The owner's answers in waves C10 to C13 are in §3.1: eight later rows,
and three answers that are not decision rows, each quoted above where
it was put. Two answers concern Codex loops: CB-45's "Run round 4
(Recommended)" and CB-20's "Small fix, no new round (Recommended)".

CD37: the reads at the openings of waves C12 and C13 are not in the
lead's record. Wave C14's read was posted on 2026-11-26 on #480
(comment 7207552000: no change), #1493 (comment 7207552201: `notify`
9.0.0 of 2026-11-21 is still the newest and clears the cooldown on
2026-11-28) and #1628 (comment 7207552479: `mathcat` 0.7.8 is still the
newest, and its cooldown cleared on 2026-11-23). §8's watch row for
#1628 is still not met.

Shared files. This docs PR corrects §6's rows for what these chunks
did. CB-17b's first PR took `a11y.rs` and `crates/slate-uniffi/src/lib.rs`
for `WorkStillRunning`, ahead of CB-16, and edited all five files of the
`VaultLifecycleViewModel.cs` row and `QuickSwitcherViewModel.cs`; its
second PR edited `WorkspaceTemplates.xaml` and twelve `MainWindow` files.
CB-34 edited two doc comments in `lib.rs`. CB-45b leaves the `lib.rs`
row and joins the `windows.yml`, `Workflows.Tests.ps1` and contract 40
rows, beside CB-43 in wave C16. CB-38 needs `MainWindow.xaml` and
neither `CommandPaletteViewModel.cs` nor `ModalSurfaces.cs`. CB-20b needs
no gate journey. A new row names CB-35's five view models, and another
names `session/directory_page.rs` for CB-45c. No row names CB-17b's
`HeldKey.cs`, `PropertiesLanding.cs`, `RegionFocusGuard.cs`,
`SelectorFocus.cs` and `Commands/CommandKeysFirst.cs`; CB-23's four
censuses; CB-22b's `NoteHeaderView.cs`, `EditorEmbedPreview.cs` and
`EmbedPreviewTitleCensus`; CB-34's `palette.rs` and
`CommandPaletteResultsPresenter.cs`; CB-16's `StatusBarCancelButton.cs`;
or CB-36's `TypeRampCensus` and `WcMatrixCanvasEvidenceCensus`. Two
chunks met at a
fact no row names: CB-23's CI met CB-17b's second PR's Move To fact,
above.

Defects on `main` that these waves found and filed, none placed in a
wave yet; each waits for the owner's call, beside the earlier ones
above (#1974 to #2034):

- #2039 (CB-45): three session calls on the Windows UI thread freeze the
  window for the rest of a running scan. CB-45c frees `ShowTags`, and
  `CopyWikilink` if `wikilink_for_path` moves.
- #2040 (CB-17b): NVDA says a property's Save is unavailable when the
  header's republish replaces the field the keys moved to. The owner
  accepted it.
- #2043 (CB-20): after a diagram lands under the caret, NVDA's
  say-current-line reads the line above until the next arrow key.
- #2046 (CB-42): the first task query after a move re-reads the
  destination, moves the graph generation, and every graph surface
  reloads once more, silently.
- #2048 (CB-23, the owner's answer, Milestone W): the Math, Code and
  Diagrams panels list a note's blocks.
- #2050 and #2051 (CB-22b): a name with an underscore loses it in what a
  screen reader hears on buttons, grid headers and menu items; Collapse
  All, Refresh Sync Diagnostics, Clear Recents and Forward run from the
  menu with nothing said.
- #2053 and #2054 (CB-23): Move To closes itself when its folders arrive
  under another sheet, and the reader comes back to nothing; a leaf
  switched under the keys on a leaf's notice lands them on the rail.
- #2056, #2059, #2060, #2062 and #2063 (CB-16): a Bases export or copy
  whose base fails under its dialog is dropped with nothing said; F6 to
  the status bar does not say that an import or a trash is running; an
  import counts a file that landed but was not indexed as not imported;
  the trash's Cancel press is answered at Medium; Narrator cuts an
  import's or a trash's summary when its work ends with the keys on its
  Cancel.
- #2057 (CB-34): in the palette's list the arrows move the selection
  but not the keys; it needs a speech decision.
- #2064, #2065 and #2068 (CB-36's first PR): NVDA's object navigation
  skips a labelled connection; the graph diagram's Selection can hand a
  client an array holding null, and answers while hidden; a held
  placeholder's Realize pans a hidden board.
- #2067 and #2071 (CB-21): Narrator says nothing on Ctrl+A in a text
  field; a reading-view range across a table answers rectangles for its
  first column only.
- #2069, #2074, #2075 and #2076 (CB-36's second PR): the Mac's chips
  cover each other in arbitrary order; NVDA speaks a card's title twice;
  `CanvasReconciliationCensus` does not check the divergence table's
  line numbers; a cut-off chip's full label cannot be read from the
  keyboard, or by hover while a truncated card is seated.
- #2079 and #2080 (CB-35's gate reruns): two shell gate journeys that
  failed once each on a hosted runner, the palette journey's poll and
  the reading view's press beside an element.

#2070 is CB-35b, placed by §5 and §6. Taken in the waves' own PRs, each
red first, as each PR's "Found and fixed here" lists: CB-17b's first PR
14 finds, its second 28 (two of them defects on `main`: Files Citing
under Settings, and a closing sheet's restore behind a sheet still
open), CB-20 six, CB-22b five, CB-23 one, CB-34 three, CB-16 sixteen,
CB-36 two and three, CB-21 two and CB-35 five.

Corrections this docs PR makes to the plan, each at its line:

- §5 gains CB-35b, CB-45c and CB-45d; CB-45b is M and adds the runner
  lane; CB-22b and CB-23 are M.
- §6's `lib.rs` list and §11's FFI count are corrected for CB-45b,
  which changes no FFI, and CB-17b, which added an event; §11's PR count
  and CB-45's risk are brought up to date.
- §6's table records wave C14 as it started, and its shared-file rows
  take CB-35's view models, CB-38's files and CB-20b's absence from the
  gate.
- §8's #1718 VoiceOver check moves to CB-45c and #1652's follow-up, and
  the closure matrix's #1718 row names the three phases.
- The record of Waves C9 and C10 said CB-22b's letters "will come as one
  table in its PR"; the owner answered on 2026-11-23, and the line says
  so.

For the owner. These questions and owner's-eye items from waves C10 to
C13 are open:

- CB-17b: Enter anywhere in the canvas prompt sheet submits the prompt,
  so Enter on a focused Clear All Marks jumps to the active mark instead
  of pressing it (by reading, a #1955 line). Since pressing it clears
  every mark, which Enter does is the owner's to choose.
- CB-42: #2046's fix reopens CD30's declined option (c).
- CB-22b: every key that two items once shared now runs its item at the
  first press, so Alt+F, C closes the vault at once, and a learned
  sequence that repeated a shared key types the rest into a note. The
  owner approved the cost; the line is for the owner's eye.
- CB-34: the two quiet cases under "Silent follows", with base and head
  phrases in #2058; "refresh" and "back" now lead with Bases: Refresh
  and Connections: Back; a right-button press makes a row the reader's;
  the list's letter search is off; and the residuals #2058 names. #2057
  needs a speech decision.
- CB-16: after a press of the status bar's Cancel, nothing says where
  the keys went, since the press cuts the status bar's focus line; the
  Files pane's Cancel import cuts its group's line the same way.
- CB-36: a next-item walk realizes and pans the board item by item;
  realizing a connection longer than the view pans to its chip, so
  neither card is in view until Invoke; while a truncated card is
  seated a chip's tooltip does not show, and a keyboard route to a
  cut-off chip's full label waits on the owner's design (#2076); and the
  control type a connection needs for NVDA's object navigation (#2064).
- CB-21: #2067 offers an app-wide hook for every text field, and a
  report to dotnet/wpf.
- CB-35: letter search is off on five lists.
- Housekeeping: the `needs-triage` label that
  `docs/agents/triage-labels.md` names does not exist in the repository,
  and no agent created it.

The questions from earlier waves, listed in the record of Waves C9 and
C10, are still open, apart from CB-22b's letters.

These waves left empty worktree folders on the lead's machine that a
process held when they were recorded, so they are left for the owner:
`plan-47-cb45`, `plan-47-cb23-empty-panel-lines`,
`plan-47-cb42-connections-scope`, `plan-47-cb22b-access-keys`,
`plan-47-cb34` and `plan-47-cb16-import-events`. From wave C13 the lead's
helper shuts a Codex broker down before a worktree is removed, and
CB-36's, CB-21's and CB-35's folders were removed. During CB-16's
Narrator check a worker turned Narrator off through Settings, after
Ctrl+Win+Enter failed, so the owner's Settings window was left on
Narrator's page; the shortcut is unchanged. The lead's main checkout
holds uncommitted `demo-vault/.obsidian` changes that no agent of these
waves made; they were left alone.

Waves C10 to C13 are done. Wave C14 started on 2026-11-26: Principal Q
has CB-20b, then CB-38, whose branch waited for CB-35's merge; and
Principal O took CB-39 when CB-35 merged. By the lead's ruling on
CB-20b's spec, an unknown-kind Mermaid diagram that is named by its
source, so that Tab and the landing read it, is filed and not fixed
there: #2078.
