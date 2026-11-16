# 47 — Windows and core close-out: delivery plan

Status: adopted by the owner's delivery instruction of 2026-11-15.
Drafted on 2026-11-13; corrected on 2026-11-14 and 2026-11-15 for twelve
merges that landed around it (§2.6). The owner answered §3 on
2026-11-16; the answers are in §3.1, and nine of them depart from the
recommendation. Waves C0 and C1 are delivered (CB-0, CB-1, CB-2 and CB-4, all merged
on 2026-11-16), and wave C2 is under way. §12 keeps the delivery
record. Written from
the requirements brief the owner approved on 2026-11-13
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
- **#1917 is partly misdescribed:** three of the four temporal probes it
  names are not cached at all.
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
| A control disabled while it holds the keys | #1866 | Fourteen sheet triggers on the menu, and the launcher's rows; neither has been heard under NVDA yet. Four sites were fixed by #1867. Six further groups (about twenty buttons that go unavailable when the reader activates them) are instances by mechanism and have never been heard either (CD11). The focus manifest's census does not scrape `Command=`, so 249 bindings can disable through `CanExecute` unseen. |
| An open dropdown's rows with no place in their set | #1783 #1836 | Eleven combo box sites, one defect. |
| An embedded reading element spoken as "grouping" | #1708 | One live member, the diagram, in three states. Math is fixed. A thematic break and an embed image each sit alone on a line with no name; what NVDA says there needs one pass. Tab onto a diagram still reads its whole source as HelpText, which #1591 removed for math; nobody had filed it (CD12). |
| A test that waits on the clock, or a fixture that tears down under in-flight work | six issues | Only #1673 is the class as worded. The unit project has 81 fixed waits that stand as statements of their own (28 sleeps, 13 delays, 40 fixed pumps) and the gate has 30; 20 elapsed-time oracles (19 in the unit project, one in the gate); nine fixtures that release a gate or dispose an event under work still running; and ten gate polls that hold a file the app replaces by rename (the mirror of #1788). Most of the unit project's fixed waits prove that nothing happened, and replacing one needs a product seam, so CB-6 fixes the few it can and pins the rest with reasons. |

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
CD12(b), CD20, CD25, CD27 and CD29. The owner gave the answers in an
interview on 2026-11-15 and 2026-11-16, and every decision row is dated
2026-11-16. The Run process row is dated 2026-11-15, the day of the
delivery instruction. CD1 is overtaken and has no answer.

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
| CB-9b | A bulk rename and a tag edit are admitted over their notes; the saved-query export cannot name a note; two accepted exceptions are left, and the census names both: the link rewrites (CD5 part b) and the Bases export, which is not one of CD5's four parts but the reading's exemption (W11, `reading/03_save_chain_lifecycle.md`); both are put to the owner in CB-46's table | #1889 | M–L | windows, rust, swift | NVDA spot if a refusal is new | CD5 |
| CB-10 | A failed vault switch returns to the welcome view once | #1730 | S | windows | NVDA spot | — |
| CB-11 | A parked citation summary press follows its note's rename | #1714 | S | windows | NVDA spot | — |
| CB-12 | A repair announces only what it changed, so an own rename after another session's does not say the old path is missing | #1683 | S | rust, windows, swift | NVDA spot (two sessions) | CD6 |
| CB-13 | A case-only rename on FAT32 or exFAT is refused and says so; the lane's weak-volume job runs the fact it has skipped by name | #1727 | S | rust, windows, runner | NVDA spot | CD7 |
| CB-14 | A toggle that landed but was not indexed says it landed; the unindexed caveats close their reason; the other leads a `SavedButUnindexed` reaches are listed | #1660 #1395 | M | rust, windows, swift | NVDA spot, three routes | CD8 |
| CB-15a | `FailureDetail` and `ShellFailureDetailCensus`; the Files sidebar's 21 sites | Refs #1890 | M | windows | NVDA spot | CD9 |
| CB-15b | Canvas, graph and search: 21 sites, `GraphConfigStore` among them; the four search and graph error arms go, and one sentence (the invalid-query one) changes, on search and on graph | Refs #1890 | M | windows | NVDA spot, the invalid-query sentence on search and on graph among them | CD9 |
| CB-15c | Panels, reading, workspace, lifecycle and commands; the census covers the whole shell | #1890 | M | windows | NVDA spot | CD9 |
| CB-16 | A cancelled import says so, and the import summary and cancel sentences are core events that Windows speaks; the status bar shows an import running, with a Cancel that moves the keys first; the Files status line never stays on "Loading files…"; the sidebar filter's and the Bases export's cancel paths are checked | #1830 #1791 | M–L | windows, rust, swift | NVDA spot | CD10 |
| CB-17a | One NVDA session hears a sheet opened from the menu and from the launcher, and each self-disabling button; the manifest census gains the keys-first witness and scrapes `Command=`, with today's sites as dated exceptions that CB-17b ends; the session's record is kept whatever it hears; no button is changed here | Refs #1866 | M | windows | NVDA: the session | CD11 |
| CB-17b | Nothing holding the keys goes off as a sheet opens, over all fourteen sheets and the launcher; every self-disabling button the `Command=` scrape finds moves the keys first, heard to fail or not: the reading's six groups (about twenty) and any other the scrape lists, all of them here; no dated exception of this class is left | #1866 | L | windows (gate) | NVDA spot: three sheets, and one button of each group | CD11 |
| CB-18 | Deleting the last note of a date group lands the keys with no "unavailable"; diagnosis first | #1671 | M | windows (gate) | NVDA spot | — |
| CB-19 | Every open combo box's rows report their place; `ComboBoxCensus`; the gate scans a list open in each family; probe first | #1783 #1836 | S–M | windows (gate) | NVDA spot | — |
| CB-20 | Line reading speaks a diagram's description, rendered and pending; `ReadingEmbeddedElementCensus`; spike first | #1708 | M | windows (gate) | NVDA full, Narrator spot | CD12 |
| CB-20b | A diagram's source is spoken on request, never on Tab | — (found by the sweep) | M | rust, windows, swift | NVDA spot | CD12 |
| CB-21 | Narrator announces the selection on Ctrl+A in the reading view; diagnosis first | #1330 | S–M | windows (gate) | Narrator | CD13 |
| CB-22 | Activate at Cursor and Preview Embed are unavailable in reading mode; a Ctrl+click on prose stays silent while the cache loads | #1661 | S | windows | NVDA spot | CD14 |
| CB-23 | A right-pane panel with no body says what it will show, in a line core owns | #1800 | S–M | rust, windows, swift | NVDA spot | CD15 |
| CB-24 | One NVDA session records #1846 and #1849; the note header's menu keeps its first row under a resting pointer, by #1944's remedy | #1849; #1846 by comment | S–M | windows (gate) | NVDA full on the steps | CD16 |
| CB-25 | A `LayoutItemsControl` drops a removed item's raw-view peer | #1860 | S | windows | — | — |
| CB-26 | A callout is an element with its type and title over the FFI, and on Windows a reader can move to it and fold or unfold it; it opens with a short design the owner reads (the plan lead's call, 2026-11-16) | #1775 | L | rust, windows, swift | NVDA full, Narrator | CD17 |
| CB-27 | One panicking diagram no longer disables diagrams for the session; a test-fault seam lets a field pass hear the new sentence | #1631 | S | rust, windows, swift | NVDA spot through the seam (no known input reaches it) | CD18 |
| CB-28 | A timed-out SVG decode is remembered, and a projection's decodes have a deadline | #1635 | M | windows, rust, swift | NVDA spot | CD19 |
| CB-29 | The SVG guard refuses the documents that decode slowly, from recorded limits | #1610 | M | windows, rust, swift | NVDA spot | CD20 |
| CB-30 | A scripted function after an ordinary atom is spoken as math; three or more primes are spoken as primes | #1629 #1628 | M | rust, swift | NVDA spot, speech and braille | CD21 |
| CB-31 | A file first indexed by a save, a repair or an open heals the dangling links that name it | #1704 | M | rust, swift | — | — |
| CB-32 | A target with a final sigma resolves, in links and in Quick Open's name match, through a fold stored in the index; each vault's index is rebuilt once on upgrade; a link snippet starts and ends on a word. Its spec says whether the fold is a new stored column (sort order unchanged) or a change to the sort key, which may move the Files tree's order for Greek names on both hosts. If the rebuild shows the user anything, its spec names that surface and checks it against CB-15c's files (same wave) before it starts | #1705 #1782 | M–L | rust, windows, swift | NVDA spot | CD22 |
| CB-33 | No cached or hot statement with an equality on `files.path` re-prepares per binding | #1917 | M | rust, swift | — | — |
| CB-34 | A typed command name runs that command | #1834 | M | rust, windows, swift | NVDA full | CD23 |
| CB-35 | Quick Open, search and the five other search fields flush as the palette does; a census holds every search field over a republishing list to it | #1837 | M | windows | NVDA spot, timed | — |
| CB-36 | The board paints in scene order; a first search for an off-window item returns it; each labelled edge has a drawn chip, and the edge's bounds are the chip's. For the chips: the covering-state scan first and seen to fail, then a short design the owner reads, then the chip code. The chips land as a second PR of CB-36, so #1838 and #1722 do not wait on the design (the design step and the two PRs are the plan lead's call, 2026-11-16) | #1838 #1722 #1721 | M–L | windows (gate) | NVDA spot | CD24 |
| CB-37 | A deselect made while an edit installs stands; diagnosis first | #1724 | M | windows | — | CD25 |
| CB-38 | A probe says what touch, stylus and a drop do under the seal, and the seal takes each route that acts; the route table names every route; #1694 is recorded as decided | #1695; #1694 by decision | S–M | windows | — | CD26 |
| CB-38b | The editor popover is part of the modal surface: a member of its own, an arm in each of the eight admission tables, and an answer at each of about 35 read sites. The lead cut it as CB-38b (the plan lead's call, 2026-11-16). Its spec proposes whether an open popover counts as modal while focus is elsewhere, starting from the ring's "only while focus is inside" (`MainWindow.ShellRegions.cs:35-36`), and the owner confirms the rule in the PR | #1665 | M–L | windows (gate) | NVDA spot | CD27 (two steps) |
| CB-39 | A Reveal in File Tree into an unlisted folder seats when the folder lists, and the newest intent wins | #1670 | M | windows (gate) | NVDA spot | — |
| CB-40 | A publication keeps the tree's rows while a list's row is selected, and follows a reader on an unselected row; design first | #1669 | M–L | windows (gate) | NVDA full | CD28 |
| CB-41 | A pin keeps the pinned row and every row it does not displace; the rest per the `Move` probe | #1668, if CD29's second step allows | S–M | windows | NVDA spot | CD29 |
| CB-42 | The Connections model says what it does not cover, and one lifecycle fact pins production | #1625 | S | windows | — | CD30 |
| CB-43 | The next golden image arms crash dumps and bakes the pinned SDK; the app job collects the dumps | Refs #1903 #1614 | M | runner, windows | — | CD31 CD32 |
| CB-44 | Every job that sets up .NET runs the runtime the pinned SDK bundles | #1614 | M | windows, runner, pilot | — | CD33 |
| CB-45 | A reviewed design for reads that do not wait behind a scan: a read-only second connection in WAL mode, phased from Quick Open; it cuts the chunks that build it, and the owner reads it before CB-45b starts. Its PR gets the red team and Codex, though it is documents only (the brief names Codex for this design; the plan lead's call, 2026-11-16). Its phasing is what keeps CB-45b inside one reviewable PR | Refs #1718 | S (docs) | none | — | CD34 |
| CB-45b | Quick Open's reads do not wait behind a scan. This is the first implementation phase of CB-45's design (the phasing is the plan lead's call, 2026-11-16); the design cuts the phases after it | Refs #1718 | L | rust, windows, swift | NVDA spot | CD34 |
| CB-46 | The deferred finds are fixed, in several PRs cut by subsystem; every Windows or core defect filed since 2026-11-13 is fixed or decided; every dated census exception is gone or in the owner's table; the close-out count is recorded | — | M–L | as needed | as needed | the re-count's own table |

### Closure matrix

Only rows where something beyond the chunk's merge must happen.

| Issue | Chunk | Closes or Refs | What else must happen before close |
|---|---|---|---|
| #1788 | CB-7 | closes (CD3) | the pin passes on `main` |
| #1889 | CB-9a, CB-9b | Refs, then closes | CB-9b; the two accepted exceptions named in CB-46's table |
| #1890 | CB-15a to CB-15c | Refs, Refs, closes | the census over the whole shell |
| #1866 | CB-17a, CB-17b | Refs, then closes | CB-17b: every self-disabling button the scrape finds moves the keys first; no accepted exception is left (CD11) |
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
| #1718 | CB-45, CB-45b, then the phases CB-45's design cuts | Refs until the last phase, which closes (CD34) | the owner has read the design; the design's last phase has merged |
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
| C4 | CB-9b | CB-13 | CB-27 | CD5 CD7 CD18 |
| C5 | CB-11 | CB-12 | CB-19 | CD6 |
| C6 | CB-14 | CB-30 | CB-25 | CD8 CD21 |
| C7 | CB-15a | CB-28 | CB-24 | CD9 CD16 CD19 |
| C8 | CB-31 | CB-15b | CB-29 | CD9 CD20 |
| C9 | CB-15c | CB-17a | CB-32 | CD9 CD11 CD22 |
| C10 | CB-17b | CB-33 | CB-22 | CD11 CD14 |
| C11 | CB-20 | CB-16 | CB-45 | CD10 CD12 CD34 |
| C12 | CB-23 | CB-34 | CB-42 | CD15 CD23 CD30 |
| C13 | CB-21 | CB-36 | CB-35 | CD13 CD24 |
| C14 | CB-39 | CB-38 | CB-20b | CD12 CD26 |
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
| `.github/workflows/windows.yml` | CB-2 (model step), CB-3 (app job), CB-13 (the weak-volume job's skip), CB-43 (the dump steps), CB-44 (the setup steps) |
| `Workflows.Tests.ps1` | CB-2, CB-3, CB-5, CB-13, CB-43, CB-44; each rebases and reruns the suite, never hand-merging |
| `38_notification_dispatcher_contracts.md` (the generated ledger, a lane gate after CB-3) | every chunk that adds or moves an `A11yEvent` construction; same-wave pairs rebase and rerun `--write`, never hand-merging |
| `TestWaitCensus.cs` | CB-4, CB-6, CB-7 |
| `Support/StaThread.cs` | CB-1 only; CB-2's stall dump goes through the runtime's own dump path |
| `ShellAccessibilityTests*.cs` (the gate) | CB-7, CB-19, CB-24, CB-17b, CB-20, CB-21, CB-36, CB-39, CB-38b (the popover journey), CB-26 (only if its design adds a journey), CB-40, CB-18; never two in a wave that edit the same partial file |
| `crates/slate-core/src/session.rs` | CB-8, CB-9b, CB-12, CB-31, CB-32 (if its one-time rebuild starts there), CB-33, then CB-45b and the phases after it |
| `crates/slate-core/src/db.rs` | CB-32 (the fold's migration), then CB-45b (the reader connection) |
| `a11y.rs`, the corpus and the variant count | CB-14, then CB-32 (only if the rebuild speaks), then CB-16 (the import events), then CB-20b, then CB-26 (only if folding announces); plan 44's WB-30 takes the next slot |
| `crates/slate-uniffi/src/lib.rs` | CB-14 (a new event), CB-28 (one doc comment), CB-16 (the import events), CB-23 (the three panel lines), CB-20b (a new event), CB-26 (the callout), CB-45b (the first phase of the scan-lock design); never two in a wave |
| `diagram.rs`, `EditorInteractions.cs` | CB-27, CB-28, CB-29, then CB-15c and CB-22 |
| `WorkspaceViewModel*.cs`, `Panels/TasksReviewViewModel.cs` | CB-9a, CB-9b, CB-11, CB-14, CB-15c, CB-22 |
| `FilesSidebarViewModel*.cs` (`FilesSidebarViewModel.Trash.cs` among them) | CB-9b, CB-15a, CB-17b (the Files and Trash self-disabling buttons), CB-16, CB-35 (the filter field), CB-39, CB-40, CB-41, CB-18 |
| `VaultLifecycleViewModel.cs`, `Panels/BulkRenameViewModel.cs`, `Bases/BaseSurfaceView.cs`, `Graph/ConnectionsLeafView.cs`, `Graph/GraphInspectorView.cs` | CB-9a, CB-9b, CB-15a, CB-15b and CB-15c (each only where its spec names one of them), then CB-17b (the self-disabling groups, `reading/04_focus_and_peers.md:485-493`); no other C10 chunk edits them |
| `MainWindow.xaml`, `MainWindow*.cs`, `WorkspaceTemplates.xaml` | CB-19, CB-17a, CB-17b (the sheets, and the property-row and launcher buttons), CB-16, CB-23, CB-35, CB-38, CB-38b, CB-40, CB-18 |
| `focus_integrity_manifest.json` | CB-17a, CB-17b, CB-16, CB-18, then any chunk that adds a focus site |
| `Reading/` (`ReadingDocumentBuilder`, `ReadingSurface`, `ReadingContentViewModel`) | CB-28, CB-15c, CB-20, CB-21, CB-20b, CB-26 |
| `links_db.rs` | CB-32, then CB-33 |
| `CommandPaletteViewModel.cs`, `28_palette_contracts.md`, contract 40 | CB-34, then CB-38, then CB-38b (with `ModalSurfaces.cs` and its tests) |
| `Canvas/CanvasRendererView.cs`, `CanvasRendererPeers.cs`, `CanvasRendererPeerTests.cs` | CB-36, then CB-37 |
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
| The second step of CD12(b), CD20, CD25, CD27 and CD29: read the spike, the calibration table, the diagnosis, the proposed popover rule or the probe, and answer | In that chunk's PR | CB-20 CB-29 CB-37 CB-38b CB-41 |
| Read the short designs for CB-26 and CB-36 (the plan lead's call, 2026-11-16, §3.1), CB-40's design step (CD28) and CB-45's design (CD34) | Before that chunk's code; CB-36's after its scan is seen to fail and before the chip code; CB-45's before CB-45b starts | CB-26 CB-36 CB-40 CB-45 |
| The ruleset `PUT` from #1639, so #1565 can close | Any time; the week's watch is met | (audit) |
| `/design-sync`, so #1756 can close | Any time | (audit) |
| The four Studio steps for #1564 | Any time | (audit) |
| Two Slate sessions on one vault for the field pass, if the launcher cannot start two | When CB-12's PR is up | CB-12 |
| One golden rebuild, then a `home-only` run and #1903's probe | After CB-43 merges (and after CB-44, if it changes what the image needs) | CB-43 |
| The VoiceOver passes and Mac adoptions on the ledger | Plan 43's queue | (ledger) |

**"Mac work owed by plan 47"** is one issue, #1954, opened by CB-0. Lines known
today:

- Speech or text: #1628 and #1629 (math speech and braille); #1660 and
  #1395 (the new toggle event's adoption; the create sentence); #1782
  (snippets); #1834 (section order; the selection rule in
  `CommandPaletteModel.swift`); #1631 (the session-long sentence goes;
  the panic sentence); #1775 (the adoption of the structured callout,
  with its fold; until then the Mac shows what the compile-keeping edit
  gives, CD17); CB-20b (the diagram source event); #1830 (the adoption
  of the new import events, in place of the Mac's own summary and cancel
  sentences, CD10); #1900 (an unreadable note lists, and on the Mac its
  row says a 1969 or 1970 modified date until the file reads again,
  CD41).
- Behaviour, with nothing new to hear: #1683 (one event stops arriving);
  #1889 (the export's refusal); #1704 and #1705 (links that used to stay
  unresolved now resolve; #1705's new fold rebuilds each vault's index
  once on upgrade, on the Mac too, CD22; if CB-32 makes the fold by
  changing the sort key, the line also names the Files tree's changed
  sort order for Greek names, on both hosts).
- Checks: the Mac reading view for #1708's gap; a note on #1527 that the
  Mac's two copies of the save sentence carry #1660's run-on; #1800
  (core owns the three empty-panel lines, and the Mac, which has bodies
  for those panels in `ContentBlockPanels.swift`, uses none of them,
  CD15); #1718 (VoiceOver on Quick Open during a rescan, once CB-45b
  lands, CD34); #1721 (only if the chips change core: the Mac already
  draws edge-label chips, #371, CD24).

The issue's body is the live text of these lines. It was brought into
line with this list on 2026-11-16.

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
| Five more search fields with #1837's shape | CB-35 |
| Quick Open and the sidebar filter miss a final-sigma name | CB-32 |
| `SavedButUnindexed` reaches other leads after the #1890 sweep ("… failed: Saved, but not indexed yet") | CB-14 lists the sites; the fixes are deferred to CB-46 |
| The sidebar filter's and the Bases export's cancel paths, unverified members of #1830's class | CB-16 |
| The golden image's evergreen installers; the floating Python patch | CB-43 (CD32) |
| WB-28 PR 1's two facts that needed an elevated session | Delivered by #1930 |
| The canvas and graph trigger ledgers have no `--check`, and `test_canvas_trigger_ledger.py` runs in no workflow | CB-46, on the deferred list |
| The device stores (`preferences.json`, `recent-vaults.json`) do not retry a refused rename, as the vault stores now do | CB-46, on the deferred list |
| Whether blame's mini dumps lack managed frames | CB-46, on the deferred list |
| The gate's facts, other than the ones CB-7 edits, delete their app log on failure; one launch-and-teardown seam over 56 launch sites would keep it | CB-46, on the deferred list |
| Whether the home host's cache commit should yield to a running slot | The owner, with the home pool (CD31) |
| The catalogue of host-composed sentences core should own | Milestone W (CD38). Three of its families are delivered here by the owner's answers of 2026-11-16: the import sentences (CB-16, CD10), the two search and graph error copies (CB-15b, CD9) and the right-pane placeholder (CB-23, CD15) |
| `ReadingSemantics.MarkEmbed(BlockUIContainer)` is never called, so one arm of the landmark walk never runs | CB-20 removes both, as a fix found on the way |
| A script slot filled by a state change or a space (`x^\rm a`, `x^\, 2`) still falls back to source | CB-30 leaves it out and names it in its PR; it goes on the deferred list for CB-46's table |

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
(§2.6), and CB-7 is re-read when wave C3 starts:

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
  redesign) and CB-35's five extra fields.
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
  PR count and the wave count above are floors.
- **Seven chunks now change the FFI,** one a wave (§6). A slip in one
  holds the next, and each needs both bindings regenerated and the
  Mac's consumers kept compiling with no Mac to run them.
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
  review rounds were exactly this.
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
the chunk's PR body ended. "Owed" is what the merge left for someone
else: an owner's call, a second answer, a rebuild or a ledger line.

| Chunk | PR | Merged | Closed | Found / fixed here / deferred / filed | Owed |
|---|---|---|---|---|---|
| CB-0 | #1957 | 2026-11-16 (97b94ddc) | #1835, #940 | 2 / 1 / 1 / 0 | The owner's call on the subsystem definition's two gaps (below). Nothing else |
| CB-4 | #1960 | 2026-11-16 (01b851d4) | #1673 | 4 / 3 / 1 / 0 | The deferred find (a math element's drawing template is tied to the first thread that touches it) is taken at once as #1965, because it can redden the unit lane |
| CB-1 | #1961 | 2026-11-16 (fcc7b5f6) | #1843 (CD2) | 7 / 3 / 3 / 1 | Filed #1959 (the licence-header script rewrites line endings). CB-6's sleep rules list `Support/Heartbeat.cs` and the planted sleeps in `StaThreadTests` |
| CB-2 | #1962 | 2026-11-16 (b62d5729) | #1624 | 5 / 2 / 2 / 1 | Filed #1963 (a core property test drew the reserved name `aux`), which CB-8's PR takes. Deferred: the pilot workflow's model step inherits the stall rule with no dump variables |
| CB-8 | #1966 | 2026-11-16 (70e97ba0) | #1900, #1709, #1963 | 1 / 1 / 0 / 0 | `not delivered: VoiceOver pass` on #1954: on the Mac a quarantined row says a 1969 or 1970 modified date (CD41). Filed for Milestone W as CD41 said: #1964, a marked row and the other surfaces that read the sentinel |

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
rounds to an approve: CB-4 two rounds, CB-1 three, CB-2 three. Codoki
reported Safe to merge at 5/5 on all three. CB-2's lane probe ran on a
scratch branch (run 43829430401): a planted stall turned both model
shards red five minutes into the test step, the log named the stalled
case, and a failed-jobs rerun kept both attempts' evidence side by
side. Not verified on a lane: a run that reaches the 70-minute blame
timer, and the routes and reroot families under a planted stall.

CB-8, 2026-11-16. Its spec was written at wave C2's start and corrected
from its delivery. A red team found no blocker and four notes, all
taken. Codex approved in one round, run at effort high where the
protocol asks for xhigh; the PR body records that. It also took #1963
at the lead's request: the property test's name generator now leaves
out the 22 stems core refuses.
