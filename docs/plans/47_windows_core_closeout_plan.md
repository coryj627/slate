# 47 — Windows and core close-out: delivery plan

Status: draft for the owner's review, 2026-11-13. Not adopted; nothing in
this plan has started. Written from the requirements brief the owner
approved the same day
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

**In:** 61 open issues, in six kinds.

| Kind | Issues |
|---|---|
| Windows tests | #1624 #1625 #1673 #1788 #1826 #1843 |
| CI workflows and runner images | #1647 #1699 #1903 #1614 #1616 #1596 |
| Windows host (`apps/slate-windows`) | #1610 #1635 #1661 #1670 #1671 #1683 #1694 #1695 #1708 #1714 #1721 #1722 #1724 #1730 #1783 #1791 #1800 #1830 #1836 #1837 #1838 #1846 #1849 #1860 #1866 #1889 #1890 #1330 |
| Core (`crates/`), host-neutral or Windows-cfg | #1628 #1629 #1631 #1660 #1395 #1704 #1705 #1727 #1775 #1782 #1834 #1900 #1917 |
| Decisions, not defects (§3) | #1665 #1668 #1669 #1718 #1842 #1848 |
| Close-outs and comments | #1835 #1709 |

Three of these were not in the brief's count of 58 and are in by a
decision row: #1330 (CD13), #1395 (CD8) and #1596 (CD32). §2.4 says why.

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
- **#1826's cause is known and its fix is not written.** It is a slow
  process exit from WPF exit listeners that each fact's thread leaks, not
  a crash at thread exit. The local fail-fast it was filed for has not
  been reproduced on current `main` (CB-1, CD1).
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
  `vault_error_detail` (`a11y.rs:3608-3610`); #1395's other half is the
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
- **#1903's tests are `Golden.Tests.ps1`,** not `Install.Tests.ps1`, and
  the home pool is switched off today (`WINDOWS_RUNNER_MODE` is
  `namespace`), so its "done when" needs the owner to bring it back.
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
| A test that waits on the clock, or a fixture that tears down under in-flight work | six issues | Only #1673 is the class as worded. The unit project has 81 fixed waits that stand as statements of their own (28 sleeps, 13 delays, 40 fixed pumps) and the gate has 29; 19 elapsed-time oracles; nine fixtures that release a gate or dispose an event under work still running; and ten gate polls that hold a file the app replaces by rename (the mirror of #1788). Most of the unit project's fixed waits prove that nothing happened, and replacing one needs a product seam, so CB-6 fixes the few it can and pins the rest with reasons. |

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
evidence.** CB-0 posts a status comment on each of the 26; ten of those
also correct text on the issue that is no longer true. Three are one
owner action from closing (#1565, #1756, #1564) and three could end on
an owner decision (CD36). §9 has the table.

Two had no owner at all, and one fits this plan better than where it sat:

- **#1330** (Narrator says nothing on Ctrl+A in the reading view). Plan
  44's WB-11 merged with `Refs` and left it open; no chunk owns the rest.
  It is Windows-deliverable. CD13 takes it as CB-21.
- **#1596** (the setup-python and runner pins). Plan 45's XB-6 never ran,
  though it has been unblocked since 2026-10-29. CD32 takes it in CB-43.
- **#1395** (the unindexed caveats' punctuation). Plan 44 parked it with
  plan 43's PR 39. CB-14 must edit the same helper and the same tests for
  #1660. CD8 folds it in.

Plans 44 to 46 have two chunks not yet delivered: WB-30 (blocked on plan
43's PR 34) and WB-32 (a spike never run). Plan 46 is fully merged. Plan
43 has merged nothing since 22a; twelve of the audited issues wait on it.

### 2.5 Where this plan meets other work

- **Plan 43's queue is paused.** Every core change here that a Mac user
  hears, sees or gets different behaviour from is closed on Windows
  evidence and listed on one issue, "Mac work owed by plan 47" (§8). Plan
  43 clears it.
- **WB-30** will rebase over CB-13 (`vault/`) and takes the next
  `a11y.rs` corpus slot after this plan's.
- **Stale status lines** in plans 45 and 46, the 22 design and plan 44's
  #1484 row are corrected in CB-0's docs PR.

## 3. Decisions for the owner (CD1 to CD40)

**The owner answers this table before delivery starts,** as the brief
says. Each row has a recommendation. §6's Gate column then names the
rows each wave's chunks rely on, so a changed answer shows which wave it
moves.

Five rows are answered in two steps, marked "two steps": the owner
answers the method now, and the result is put to them in the chunk's PR
(§8 lists those second steps).

| Id | Issue | Question | Recommended | Blocks |
|---|---|---|---|---|
| CD1 | #1826 | Close when the original local fail-fast was never reproduced? | The slow-exit half is proved by the exit log before and after; the PR writes `Refs`, and the issue closes by hand once twenty app-job runs on `main` exit promptly. The fail-fast half: twenty runs on an interactive desktop with text-services teardown off, then twenty with it on. A crash with it off and none with it on is the proof; if neither set crashes, close with `not delivered: the local fail-fast was not reproduced`. The dump arming from #1892 and #1902 stays on. | CB-1 |
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
| CD27 | #1665 | Does the editor popover join the modal surface? | No. Close; one line in contract 40. | CB-38 |
| CD28 | #1669 | Keep the tree's rows while a list's row is selected, and follow a reader on an unselected row? | Both, here, after CB-39, with a design step first. If one must go, keep the first. | CB-40 |
| CD29 | #1668 | Rebuilt rows on a sort move and on a pin. (Two steps.) | Fix the pin in memory, which keeps the pinned row and every row it does not displace. Probe whether a `Move` keeps WPF's container. If it does, both halves close. If not, the owner accepts the remainder in the PR or the issue stays open. | CB-41 |
| CD30 | #1625 | The Connections model does not see session events. | Scope it out of the model in a comment and pin production with one lifecycle fact. | CB-42 |
| CD31 | #1903 | When does it close? | The PR merges with `Refs`; it closes when the owner posts the home-VM probe. If the pool is not coming back, park it and say so. | CB-43 |
| CD32 | #1596 | Take plan 45's XB-6 here? What of the image's two evergreen installers (Build Tools, `dotnet-install.ps1`) and the floating Python patch? | Yes, in CB-43, so one golden rebuild covers all three. The installers and the Python patch are recorded as accepted, dated: both installers are signature-checked. | CB-43 |
| CD33 | #1614 | How strictly is the runtime held? Does it close with the rebuild still owed? | First print what each pool loads today. Then each job installs the pinned SDK by exact URL and committed hash and does not use `setup-dotnet`; CI's runtime moves back to the one the pinned SDK bundles. It closes at CB-44's merge, with the rebuild on the owner's steps. | CB-44 |
| CD34 | #1718 | Which design, and where is it delivered? | A read-only second connection in WAL mode, phased from Quick Open, as its own plan. Here: the design spec only. No interim. | CB-45 |
| CD35 | #1842 #1848 | Build here or move? | Move both to Milestone W. | CB-0 |
| CD36 | #940 #1389 #1323 | Three audited issues that could end on a decision. | Close #940 by hand and let #1513 carry its one remaining fact. #1651 carries #1389's mac half, and plan 43's PR 52 is retired. #1323 stays open for its one mac fact, retitled. CB-0 makes these edits once the row is answered. | CB-0 |
| CD37 | #480 #998 #1493 #1628 | Nobody is named to read the release lists. | The agent that opens each wave of this plan checks them and comments. After the plan ends, the owner's choice. | CB-0 |
| CD38 | — | The reading listed about nine families of sentences the host composes that `AGENTS.md` says core should own (the sidebar's failure leads, the panel error leads, the status-line copy and others). Each move is an FFI addition with Mac adoption. | Not in this plan. CB-0 files one issue for Milestone W that holds the catalogue. | CB-0 |
| CD39 | — | The brief's order is sweeps before code-area chunks. With three slots and shared files, §6 fills free slots with code-area chunks from C2 on, ahead of the later reader-facing sweeps. | Accept §6's order. | §6 |
| CD40 | #1835 | Close on the control's removal, with no fact? There is no toggle left to measure. | Yes. CB-0's comment names #1856 and quotes the search that finds only the menu row. | CB-0 |

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
   Where that cannot happen, a decision row says so: CD1, CD2, CD3 and
   CD30 close without a replay or a red phase; CD16, CD26 and CD27 close
   without a fix; CD40 closes #1835 on the removal of its control.
8. **Every PR body ends with the count:** found on the way, fixed here,
   deferred, filed.
9. **Review.** A fresh Claude red team on every chunk before its PR
   opens, with `24_red_team_protocol.md`. Codex also reviews CB-9a,
   CB-9b, CB-12, CB-17a, CB-17b, CB-31, CB-34, CB-38, CB-39, CB-40,
   CB-41 and CB-45's design. A chunk that only changes CI config, tests
   or comments goes on its tests alone; CB-1 is the exception, because
   it reaches into WPF's private members.
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
| CB-0 | The found-on-the-way rule is in `AGENTS.md`, and plans 43 to 46 point at it; the audited issues carry a status comment; #1835 is closed; #1842 and #1848 are in Milestone W with 9edf4b6b tagged; #1733 points at plan 43; the ledger, the deferred-finds issue and CD38's catalogue issue exist; the stale plan headers are corrected | #1835 (CD40) | S | none | — | CD35 CD36 CD37 CD38 CD40; CD8 CD13 CD32 for one comment each |
| CB-1 | A fact's thread leaves no WPF exit listener, so the test process exits promptly; a timeout that could not take a stack says whether the process stalled | Refs #1826, then by hand; #1843 | M | windows | — | CD1 CD2 |
| CB-2 | The model reports a stalled case by name; a slow shard is not ended at 45 minutes; a failed attempt keeps its dumps | #1624 | S | windows, runner | — | — |
| CB-3 | The shell gate's binaries keep seven days; the lane checks contract 38's ledger against its generator and prints the differing rows; the generated-table checks are pinned, and the docs-trigger census sees their spelling | #1647 #1699 | S | windows, runner | — | — |
| CB-4 | No fixture tears down under work it released: #1673 and eight sibling fixtures; `TestWaitCensus` rule 3 | #1673 | M | windows | — | — |
| CB-5 | a11y-check rewrites its comment when no score is computed, and malformed results fail in one sentence | #1616 | S | a11y, runner | — | CD4 |
| CB-6 | The unit project's fixed waits can only shrink: `TestWaitCensus` rules 1 and 2 pin all 81 and the elapsed-time oracles with a reason each; the cold wall-clock budgets go through `WallClockBudget`; the waits that have a condition today become condition waits (at least five) | — (the class) | M | windows | — | — |
| CB-7 | The gate's journeys wait on conditions and read app files with delete sharing; rule 4; the graph settle-and-save pin; the app log joins the gate's uploaded evidence | #1788 | M | windows (gate) | — | CD3 |
| CB-8 | A quarantined note still lists, so an unreadable note no longer fails the Files tree; the two stale mac comments are corrected | #1900 #1709 | S | rust, windows, swift | — | — |
| CB-9a | `SaveChainAdmissionCensus` with dated exceptions; the single-path routes join (the reload guard, `RecoverDeletedFile`, the two tabless writes); the create-class fact | Refs #1889 | M | windows | — | CD5 |
| CB-9b | A bulk rename and a tag edit are admitted over their notes; the saved-query export cannot name a note; the only exceptions left are the two CD5 accepts (link rewrites, the Bases export) | #1889 | M–L | windows, rust, swift | NVDA spot if a refusal is new | CD5 |
| CB-10 | A failed vault switch returns to the welcome view once | #1730 | S | windows | NVDA spot | — |
| CB-11 | A parked citation summary press follows its note's rename | #1714 | S | windows | NVDA spot | — |
| CB-12 | A repair announces only what it changed, so an own rename after another session's does not say the old path is missing | #1683 | S | rust, windows, swift | NVDA spot (two sessions) | CD6 |
| CB-13 | A case-only rename on FAT32 or exFAT is refused and says so; the two facts WB-28 left for an elevated session run in the same one | #1727 | S | rust | NVDA spot | CD7 |
| CB-14 | A toggle that landed but was not indexed says it landed; the unindexed caveats close their reason; the other leads a `SavedButUnindexed` reaches are listed | #1660 #1395 | M | rust, windows, swift | NVDA spot, three routes | CD8 |
| CB-15a | `FailureDetail` and `ShellFailureDetailCensus`; the Files sidebar's 21 sites | Refs #1890 | M | windows | NVDA spot | CD9 |
| CB-15b | Canvas, graph and search: 21 sites, `GraphConfigStore` among them | Refs #1890 | M | windows | NVDA spot | CD9 |
| CB-15c | Panels, reading, workspace, lifecycle and commands; the census covers the whole shell | #1890 | M | windows | NVDA spot | CD9 |
| CB-16 | A cancelled import says so and the status bar shows one running, with a Cancel that moves the keys first; the Files status line never stays on "Loading files…"; the sidebar filter's and the Bases export's cancel paths are checked | #1830 #1791 | S–M | windows | NVDA spot | CD10 |
| CB-17a | One NVDA session hears a sheet opened from the menu and from the launcher, and each self-disabling button; the manifest census gains the keys-first witness and scrapes `Command=`, with today's sites as dated exceptions | Refs #1866 | M | windows | NVDA: the session | CD11 |
| CB-17b | Nothing holding the keys goes off as a sheet opens, over all fourteen sheets and the launcher | #1866 | L | windows (gate) | NVDA spot, three sheets | CD11 |
| CB-18 | Deleting the last note of a date group lands the keys with no "unavailable"; diagnosis first | #1671 | M | windows (gate) | NVDA spot | — |
| CB-19 | Every open combo box's rows report their place; `ComboBoxCensus`; the gate scans a list open in each family; probe first | #1783 #1836 | S–M | windows (gate) | NVDA spot | — |
| CB-20 | Line reading speaks a diagram's description, rendered and pending; `ReadingEmbeddedElementCensus`; spike first | #1708 | M | windows (gate) | NVDA full, Narrator spot | CD12 |
| CB-20b | A diagram's source is spoken on request, never on Tab | — (found by the sweep) | M | rust, windows, swift | NVDA spot | CD12 |
| CB-21 | Narrator announces the selection on Ctrl+A in the reading view; diagnosis first | #1330 | S–M | windows (gate) | Narrator | CD13 |
| CB-22 | Activate at Cursor and Preview Embed are unavailable in reading mode; a Ctrl+click on prose stays silent while the cache loads | #1661 | S | windows | NVDA spot | CD14 |
| CB-23 | A right-pane panel with no body says what it will show | #1800 | S | windows | NVDA spot | CD15 |
| CB-24 | One NVDA session records #1846 and traces #1849; the note header's menu keeps its first row | #1849; #1846 by comment | S–M | windows (gate) | NVDA full on the steps | CD16 |
| CB-25 | A `LayoutItemsControl` drops a removed item's raw-view peer | #1860 | S | windows | — | — |
| CB-26 | A callout reads as its label, not its marker, on both hosts | #1775 | M | rust, windows, swift | NVDA and Narrator spot | CD17 |
| CB-27 | One panicking diagram no longer disables diagrams for the session; a test-fault seam lets a field pass hear the new sentence | #1631 | S | rust, windows, swift | NVDA spot through the seam (no known input reaches it) | CD18 |
| CB-28 | A timed-out SVG decode is remembered, and a projection's decodes have a deadline | #1635 | M | windows, rust, swift | NVDA spot | CD19 |
| CB-29 | The SVG guard refuses the documents that decode slowly, from recorded limits | #1610 | M | windows, rust, swift | NVDA spot | CD20 |
| CB-30 | A scripted function after an ordinary atom is spoken as math; three or more primes are spoken as primes | #1629 #1628 | M | rust, swift | NVDA spot, speech and braille | CD21 |
| CB-31 | A file first indexed by a save, a repair or an open heals the dangling links that name it | #1704 | M | rust, swift | — | — |
| CB-32 | A target with a final sigma resolves, in links and in Quick Open's name match; a link snippet starts and ends on a word | #1705 #1782 | M | rust, windows, swift | NVDA spot | CD22 |
| CB-33 | No cached or hot statement with an equality on `files.path` re-prepares per binding | #1917 | M | rust, swift | — | — |
| CB-34 | A typed command name runs that command | #1834 | M | rust, windows, swift | NVDA full | CD23 |
| CB-35 | Quick Open, search and the five other search fields flush as the palette does; a census holds every search field over a republishing list to it | #1837 | M | windows | NVDA spot, timed | — |
| CB-36 | The board paints in scene order; a first search for an off-window item returns it; an edge's bounds are its midpoint; probe first | #1838 #1722 #1721 | S–M | windows (gate) | — | CD24 |
| CB-37 | A deselect made while an edit installs stands; diagnosis first | #1724 | M | windows | — | CD25 |
| CB-38 | A probe says what touch, stylus and a drop do under the seal, and the seal takes each route that acts; the route table names every route; #1694 and #1665 are recorded as decided | #1695; #1694 #1665 by decision | S–M | windows | — | CD26 CD27 |
| CB-39 | A Reveal in File Tree into an unlisted folder seats when the folder lists, and the newest intent wins | #1670 | M | windows (gate) | NVDA spot | — |
| CB-40 | A publication keeps the tree's rows while a list's row is selected, and follows a reader on an unselected row; design first | #1669 | M–L | windows (gate) | NVDA full | CD28 |
| CB-41 | A pin keeps the pinned row and every row it does not displace; the rest per the `Move` probe | #1668, if CD29's second step allows | S–M | windows | NVDA spot | CD29 |
| CB-42 | The Connections model says what it does not cover, and one lifecycle fact pins production | #1625 | S | windows | — | CD30 |
| CB-43 | The next golden image arms crash dumps, bakes the pinned SDK and carries the cooled runner pin; the app job collects the dumps; the eleven `setup-python` lines move to their cooled release | Refs #1903 #1614; #1596 | M | runner, windows | — | CD31 CD32 |
| CB-44 | Every job that sets up .NET runs the runtime the pinned SDK bundles | #1614 | M | windows, runner, pilot | — | CD33 |
| CB-45 | A reviewed design for reads that do not wait behind a scan, handed to its own plan | Refs #1718 | S (docs) | none | — | CD34 |
| CB-46 | The deferred finds are fixed, in several PRs cut by subsystem; every Windows or core defect filed since 2026-11-13 is fixed or decided; every dated census exception is gone or in the owner's table; the close-out count is recorded | — | M–L | as needed | as needed | the re-count's own table |

### Closure matrix

Only rows where something beyond the chunk's merge must happen.

| Issue | Chunk | Closes or Refs | What else must happen before close |
|---|---|---|---|
| #1826 | CB-1 | Refs, then by hand (CD1) | twenty prompt app-job exits on `main`; the off-and-on desktop runs |
| #1843 | CB-1 | closes (CD2) | — |
| #1788 | CB-7 | closes (CD3) | the pin passes on `main` |
| #1889 | CB-9a, CB-9b | Refs, then closes | CB-9b; the two accepted exceptions named in CB-46's table |
| #1890 | CB-15a to CB-15c | Refs, Refs, closes | the census over the whole shell |
| #1866 | CB-17a, CB-17b | Refs, then closes | CD11's second step |
| #1846 | CB-24 | closes by comment (CD16) | the recorded NVDA run |
| #1835 | CB-0 | closes by comment (CD40) | — |
| #1694 #1665 | CB-38 | close by decision (CD26, CD27) | the contract lines merged |
| #1668 | CB-41 | closes, or stays open (CD29) | the probe's result and the owner's second answer |
| #1330 | CB-21 | closes, or closes with the record (CD13) | the diagnosis |
| every issue §8's ledger lists | their chunks | close | each one's ledger line |
| #1727 | CB-13 | closes | the owner's elevated VHD session for the evidence |
| #1903 | CB-43 | Refs, then closes | the golden rebuild and the home-VM probe (CD31) |
| #1614 | CB-43, CB-44 | Refs, then closes at CB-44 (CD33) | the rebuild stays on the owner's steps |
| #1596 | CB-43 | closes | each version's release date past the cooldown |
| #1718 | CB-45 | Refs | its own plan |
| #940 #1323 #1389 | CB-0 | by CD36 | the owner's answer; CB-0 makes the edits |
| #1842 #1848 | CB-0 | stay open | Milestone W |
| #1733 | CB-0 | stays open | plan 43 |

## 6. Delivery order

At most three agents at once. Chunks in one wave share no files. The
decision table is answered before C0; the Gate column names the rows
each wave relies on. Each chunk is re-checked against `main` when it
starts, and the slotting of waves C3 onward is confirmed when each
wave's specs are written.

| Wave | Slot 1 | Slot 2 | Slot 3 | Gate |
|---|---|---|---|---|
| C0 | CB-0 | — | — | CD8 CD13 CD32 CD35 CD36 CD37 CD38 CD40 |
| C1 | CB-1 | CB-2 | CB-4 | CD1 CD2 |
| C2 | CB-3, then CB-5 | CB-6 | CB-8 | CD4 |
| C3 | CB-7 | CB-9a | CB-10 | CD3 CD5 |
| C4 | CB-9b | CB-13 | CB-27 | CD5 CD7 CD18 |
| C5 | CB-11 | CB-12 | CB-19 | CD6 |
| C6 | CB-14 | CB-30 | CB-25 | CD8 CD21 |
| C7 | CB-15a | CB-28 | CB-24 | CD9 CD16 CD19 |
| C8 | CB-31 | CB-15b | CB-29 | CD9 CD20 |
| C9 | CB-15c | CB-17a | CB-32 | CD9 CD11 CD22 |
| C10 | CB-17b | CB-33 | CB-22 | CD11 CD14 |
| C11 | CB-20 | CB-16 | CB-26 | CD10 CD12 CD17 |
| C12 | CB-20b | CB-23 | CB-34 | CD12 CD15 CD23 |
| C13 | CB-21 | CB-36 | CB-35 | CD13 CD24 |
| C14 | CB-39 | CB-38 | CB-37 | CD25 CD26 CD27 |
| C15 | CB-40 | CB-42 | CB-43 | CD28 CD30 CD31 CD32 |
| C16 | CB-41 | CB-44 | CB-45 | CD29 CD33 CD34 |
| C17 | CB-18 | — | — | — |
| C18 | CB-46 (several PRs) | | | the re-count's table |

Why this order:

- **C0 is a morning,** and every later chunk works under its rule. Its
  `AGENTS.md` PR relies on no row; three of its comments rely on CD8,
  CD13 and CD32, its issue edits on CD35 and CD36, and #1835's close on
  CD40.
- **C1 and C2 make reruns rare** before the larger chunks start paying
  for lane time. CB-1 removes the most frequent red on `main`.
- **C3 to C6 protect a note's bytes and the index:** the save chain, the
  quarantine, the repair's event, the rename that lies.
- **C6 to C15 are reader-facing,** with at most one full NVDA pass in a
  wave (CB-24, CB-17a, CB-20, CB-34, CB-40).
- **CB-16 follows CB-17b,** because it adds a Cancel button of the class
  that sweep closes. **CB-18 follows CB-41,** because its fix lands in
  the publication code CB-40 redesigns.
- **C15 and C16 hold the chunks that wait longest on the owner's
  hands:** the golden image, the runtime, the scan-lock design.
- **Code-area chunks fill free slots from C2 on,** ahead of the later
  reader-facing sweeps. This departs from the brief's order (sweeps, then
  code areas) and is CD39.

Sequenced on purpose, because they share files:

| Shared code | Order |
|---|---|
| `AGENTS.md` | CB-0, then CB-3 (one line beside the parity-matrix gotcha) |
| `.github/workflows/windows.yml` | CB-2 (model step), CB-3 (app job), CB-43 (the dump steps), CB-44 (the setup steps) |
| `Workflows.Tests.ps1` | CB-2, CB-3, CB-5, CB-43, CB-44; each rebases and reruns the suite, never hand-merging |
| `38_notification_dispatcher_contracts.md` (the generated ledger, a lane gate after CB-3) | every chunk that adds or moves an `A11yEvent` construction; same-wave pairs rebase and rerun `--write`, never hand-merging |
| `TestWaitCensus.cs` | CB-4, CB-6, CB-7 |
| `Support/StaThread.cs` | CB-1 only; CB-2's stall dump goes through the runtime's own dump path |
| `ShellAccessibilityTests*.cs` (the gate) | CB-7, CB-19, CB-24, CB-17b, CB-20, CB-21, CB-36, CB-39, CB-40, CB-18; never two in a wave that edit the same partial file |
| `crates/slate-core/src/session.rs` | CB-8, CB-9b, CB-12, CB-31, CB-33 |
| `a11y.rs`, the corpus and the variant count | CB-14, then CB-20b; plan 44's WB-30 takes the next slot |
| `crates/slate-uniffi/src/lib.rs` | CB-14 (a new event), CB-28 (one doc comment), CB-20b (a new event); never two in a wave |
| `diagram.rs`, `EditorInteractions.cs` | CB-27, CB-28, CB-29, then CB-15c and CB-22 |
| `WorkspaceViewModel*.cs`, `Panels/TasksReviewViewModel.cs` | CB-9a, CB-9b, CB-11, CB-14, CB-15c, CB-22 |
| `FilesSidebarViewModel*.cs` | CB-9b, CB-15a, CB-16, CB-35 (the filter field), CB-39, CB-40, CB-41, CB-18 |
| `MainWindow.xaml`, `MainWindow*.cs`, `WorkspaceTemplates.xaml` | CB-19, CB-17a, CB-17b, CB-16, CB-23, CB-35, CB-38, CB-40, CB-18 |
| `focus_integrity_manifest.json` | CB-17a, CB-17b, CB-16, CB-18, then any chunk that adds a focus site |
| `Reading/` (`ReadingDocumentBuilder`, `ReadingSurface`, `ReadingContentViewModel`) | CB-28, CB-15c, CB-20, CB-20b, CB-21 |
| `links_db.rs` | CB-32, then CB-33 |
| `CommandPaletteViewModel.cs`, `28_palette_contracts.md` | CB-34, then CB-38 |
| `chords.json`, `ChordTable.cs`, the parity matrix | CB-20b |
| the golden image (`provision-guest.ps1`, `versions.json`, `Golden.Tests.ps1`) | CB-43 only |

## 7. Verification lanes and the local loop

Plan 44 §7 applies. Per kind of chunk, what its PR body quotes:

- **Tests and censuses** (CB-1, CB-4, CB-6, CB-7, CB-42):
  `dotnet test … --filter` on the touched facts, seen failing first; a
  census's planted-case fact. CB-1 and CB-17b each need one full local
  run on an interactive desktop: CB-1 because the foreground regression
  it could cause is invisible to CI, CB-17b because the menu facts skip
  on the hosted runners.
- **Workflows** (CB-2, CB-3, CB-5, CB-43, CB-44): `Invoke-Pester` on the
  runner tests, then the PR's own lane runs by run id, and a probe that
  shows the new check red. `windows.yml` does not run on a branch push,
  so its probe is a `workflow_dispatch` on a scratch branch.
- **Core** (CB-8, CB-9b, CB-12 to CB-14, CB-20b, CB-26 to CB-34): the
  four Rust gates in PowerShell; the Windows twin fact; the swift lane's
  compile.
- **Host** (the rest): the hosted facts, the shell gate where the chunk
  adds a journey, and the NVDA phrases recorded in the PR body.

## 8. Owner steps, the two issues and the watch list

**Owner steps.** Each is something an agent cannot do.

| What | When | Chunk |
|---|---|---|
| Answer §3 | Before C0 | all |
| The second step of CD11, CD12(b), CD20, CD25 and CD29: read the session record, the spike, the calibration table, the diagnosis or the probe, and answer | In that chunk's PR | CB-17a CB-20 CB-29 CB-37 CB-41 |
| The ruleset `PUT` from #1639, so #1565 can close | Any time; the week's watch is met | (audit) |
| `/design-sync`, so #1756 can close | Any time | (audit) |
| The four Studio steps for #1564 | Any time | (audit) |
| An elevated session with the FAT32 and exFAT VHDs | When CB-13's PR is up | CB-13 |
| Two Slate sessions on one vault for the field pass, if the launcher cannot start two | When CB-12's PR is up | CB-12 |
| One golden rebuild, then a `home-only` run and #1903's probe | After CB-43 merges (and after CB-44, if it changes what the image needs) | CB-43 |
| The VoiceOver passes and Mac adoptions on the ledger | Plan 43's queue | (ledger) |

**"Mac work owed by plan 47"** is one issue, opened by CB-0. Lines known
today:

- Speech or text: #1628 and #1629 (math speech and braille); #1660 and
  #1395 (the new toggle event's adoption; the create sentence); #1782
  (snippets); #1834 (section order; the selection rule in
  `CommandPaletteModel.swift`); #1631 (the session-long sentence goes;
  the panic sentence); #1775 (a callout's first line); CB-20b (the
  diagram source event); the import sentences' move to core (CD10).
- Behaviour, with nothing new to hear: #1683 (one event stops arriving);
  #1889 (the export's refusal); #1704 and #1705 (links that used to stay
  unresolved now resolve); #1900 (an unreadable note lists).
- Checks: the Mac reading view for #1708's gap; a note on #1527 that the
  Mac's two copies of the save sentence carry #1660's run-on, and that
  Windows keeps four search and graph error arms worded apart from core.

**"Plan 47 deferred finds"** is the second issue CB-0 opens (§4 rule 10).
It starts with the finds §9 gives to CB-46.

**Watch list.**

| Issue | Waits for | Check | Action when it lands |
|---|---|---|---|
| #1628 | a MathCAT release with the Nemeth cells corrected | at each wave start (CD37) | drop the split render; one golden diff |
| #480 #998 #1493 | upstream releases | at each wave start (CD37) | as plan 44 §8 |
| #1560 | a transcriber's verdict | the owner | a core PR per item |

## 9. Left to other plans, and found but not filed

CB-0 posts the status comment the audit drafted for each audited issue.
None closes today on evidence; CD36 may end three.

| Owner of what remains | Issues |
|---|---|
| Plan 43's queue (paused; needs a Mac) | #1323 (one mac fact) #1370 #1398 #1434 #1470 #1469 #1513 #1525 #940 (CD36) #1389 and #1526 (through #1651, CD36) #1484 (through #1653) #1733 |
| Plan 44 WB-30, then plan 43 PR 41 | #1468 |
| The owner | #1560 #1564 #1565 #1756 #746 #747 #750; and #748 #749, which the audit did not read |
| The watch list | #480 #998 (WB-32's spike was never run) #1493 |

Mac follow-ups that plans 44 and 45 filed and no plan 43 row names
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
| WB-28 PR 1's two facts that need an elevated session | CB-13's session |
| The canvas and graph trigger ledgers have no `--check`, and `test_canvas_trigger_ledger.py` runs in no workflow | CB-46, on the deferred list |
| The device stores (`preferences.json`, `recent-vaults.json`) do not retry a refused rename, as the vault stores now do | CB-46, on the deferred list |
| Whether blame's mini dumps lack managed frames | CB-46, on the deferred list |
| The gate's facts, other than the ones CB-7 edits, delete their app log on failure; one launch-and-teardown seam over 56 launch sites would keep it | CB-46, on the deferred list |
| Whether the home host's cache commit should yield to a running slot | The owner, with the home pool (CD31) |
| The catalogue of host-composed sentences core should own | Milestone W (CD38) |
| `ReadingSemantics.MarkEmbed(BlockUIContainer)` is never called, so one arm of the landmark walk never runs | CB-20 removes both, as a fix found on the way |
| A script slot filled by a state change or a space (`x^\rm a`, `x^\, 2`) still falls back to source | CB-30 leaves it out and names it in its PR; it goes on the deferred list for CB-46's table |

## 10. How an implementing agent works a chunk

Plan 44 §10 applies: a fresh worktree on a `claude/<chunk-slug>` branch
from `main`; the spec's "Current state" lines read before coding; failing
facts first; registers closed in the same PR; the commit and PR title
wording from `AGENTS.md`; the PR body with `Closes #N`, the facts, the
registers, the field-pass phrases, "Found and fixed here", `not
delivered: <reason>` and the count (§4 rule 8).

A chunk whose cause is unknown (CB-17a, CB-18, CB-21, CB-24, CB-37)
opens with a diagnosis recorded on its issue before any product code. A
chunk that rests on WPF behaviour nobody has checked (CB-1, CB-19,
CB-36, CB-41) opens with the failing fact or probe that settles it, with
the fallback named. A chunk with a spike (CB-20) or a design step
(CB-40, CB-45) opens with that, and the owner reads it first.

Specs for the first wave, written 2026-11-13 against c2377210:

| Chunk | Spec |
|---|---|
| CB-0 | `specs/cb00_the_rule_the_audit_and_the_close_outs.md` |
| CB-1 | `specs/cb01_a_facts_thread_leaves_no_exit_listener.md` |
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

- **The plan is larger than the brief guessed:** 51 PRs before CB-46's,
  not 20 to 25, because the sweeps found what they were meant to find.
  Nineteen waves under one-at-a-time merges is weeks of lane time. The
  first cut, if one is needed, is CD28 and CD29 (the Files tree
  redesign) and CB-35's five extra fields.
- **Forty decisions come before any work.** Each has a
  recommendation, and the brief asks for them up front. If that is too
  much to answer at once, the smallest safe change is to answer C0 to
  C2's rows first (CD1, CD2, CD4, CD8, CD13, CD32, CD35 to CD40) and the
  rest before C3;
  that is the owner's call, not the plan's.
- **Inline fixes can swell a PR.** Rule 2's cap and the red team's read
  are the guard; rule 8's count shows it happening.
- **Four issues close on inference, not replay** (#1788, #1843, half of
  #1826, and #1846 on one confirming run). If the owner says no to a
  row, that issue stays open with a watch, and CB-46's table carries it.
- **CB-1's fix rests on WPF internals nobody has checked.** If a
  listener cannot be run and unhooked on its own thread, the fallback
  drops the listeners at the last test, which keeps the process exit
  prompt and leaves the leak.
- **Core changes with no Mac to listen.** About fifteen ledger lines so
  far. The ledger only helps if plan 43's queue restarts.
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
- **CB-43 cannot be finished by an agent,** and the home pool is off.
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
