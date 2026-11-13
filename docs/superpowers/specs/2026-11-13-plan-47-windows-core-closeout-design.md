# Plan 47 — Windows and core close-out: requirements brief

Status: draft for the owner's approval, 2026-11-13. This is the brief the
plan is written from, not the plan. Baseline: `main` at 1ec9bf31 (#1926).
Everything marked **Decided** is the owner's answer in the 2026-11-13
interview. Everything marked **My call** is the author's proposal and has
not been confirmed. The owner's approval of this brief confirms the calls
that still stand in it, and the plan then records them as decided, so
its first chunk is not gated on them again.

## 1. Intent

Plans 44, 45 and 46 delivered the Windows and host-neutral backlog, and
each left a tail: defects found next to the work by a red team, a Codex
review or an NVDA pass, and filed because `AGENTS.md` says a defect found
on the way is filed unless it blocks the PR. Plan 44's tail became plan
45, and both, with plan 46, left more.

Plan 47 closes that tail and changes the rule that produces it. When it
is finished, no open Windows or core defect is without an owner, and the
work has filed as few new issues as it honestly can.

## 2. Goals, non-goals, anti-goals

**Goals**

1. Close every open Windows or core defect that no chunk of plans 43–46
   owns and that this Windows machine can deliver and verify (§4, groups
   A and B).
2. Leave nothing unowned: every in-scope issue that is not a defect gets
   an owner decision in the plan, answered before delivery starts (§6).
3. Audit the issues that plans 44–46 do own and that are still open, and
   close the ones that can close on evidence that already exists (§4,
   group C).
4. Replace the filing rule in `AGENTS.md`, for the whole repository and
   for good (§5).
5. Stop a class of defect from coming back one sibling at a time, by
   sweeping each class the reading shows and ending each sweep in a
   census test or a CI gate (§7).

**Non-goals**

- Mac work, or anything that needs a Mac to verify. Plan 43 keeps it.
- Feature milestones E, V, X, XD, PD, EX, PS and CV, and Milestone W's
  own Wave 8 epics (#748, #749, #751–#756).
- Re-planning a chunk that plans 44–46 already own. The group C audit
  closes or corrects; it does not re-deliver.
- New features, including the ones filed as issues in scope (§6 gives
  each a home).

**Anti-goals** (the plan has failed if any of these is true at the end)

- It closes its issues and files a comparable number of new ones.
- A PR grows so large from inline fixes that its review cannot hold it.
- An issue closes on a fix nobody saw fail and then pass.
- A core change alters what a Mac user hears and nothing records that a
  VoiceOver pass is owed.
- A feature arrives under a defect's name.

## 3. Constraints

- `AGENTS.md` as it stands, apart from the one rule §5 replaces: the
  failing test first; fresh worktree on `claude/<slug>`; commit and PR
  title wording; `Closes #N`; contract register rows closed in the same
  PR; no accidental closing keywords; the 7-day dependency cooldown;
  merges one at a time, each after `main` is green on the previous one.
- Plan 44 §4's local loop: bindings regenerated per worktree into a
  per-worktree `CARGO_TARGET_DIR`, `dotnet test --filter`, `dotnet
  format --include` after every C# edit, the four Rust gates in
  PowerShell, field passes through `scripts/windows/launch-for-field-pass.ps1`.
- Lane runs cost money. Pushes are batched and only the touched lanes run.
- No Mac is available to the plan. A core change still has to keep the
  mac lane compiling, with the blind mirror edits plan 44 §4 rule 6 names.
- At most three agents at once, and chunks in one wave share no files
  (**My call**, carried from plans 44 and 45).

## 4. Scope

Counted on 2026-11-13 from the 214 open issues. Group B was found by
matching issue numbers against the text of plans 44–46 and reading each
body's opening; the plan's own reading confirms each one is ownerless
before it is planned.

### Group A — never mentioned in plans 43–46 (30)

| Kind | Issues |
|---|---|
| Windows host | #1610 #1671 #1683 #1708 #1714 #1722 #1724 #1791 #1800 #1830 #1846 #1849 #1860 #1866 #1889 #1890 #1900 |
| Windows tests and CI | #1624 #1625 #1647 #1843 #1903 |
| Core | #1628 #1629 #1631 #1704 #1705 #1718 #1917 |
| Comments only | #1709 (two stale comments in `apps/slate-mac`; the swift lane compiles them) |

### Group B — mentioned in a plan 44–46 spec or scope note, owned by no chunk (29)

| Source | Issues |
|---|---|
| Plan 44's deliveries | #1635 #1660 #1661 #1665 #1668 #1669 #1670 #1673 #1694 #1695 #1699 #1721 #1727 #1730 #1733 |
| Plan 45's deliveries | #1614 #1616 |
| Plan 46's deliveries and its "stay filed" findings | #1775 #1782 #1783 #1788 #1826 #1834 #1835 #1836 #1837 #1838 #1842 #1848 |

**Decided:** A and B are both in. #1733 leaves for plan 43's queue,
because its code is `cfg(unix)` and only a Mac can check its behaviour
(**My call**, stated in the interview and not objected to). That leaves
58 issues this plan owns.

### The list is frozen, with one re-count

**Decided.** The plan delivers the issues open on 2026-11-13. It does not
take in new ones wave by wave. Its final chunk lists every Windows or
core defect filed after that date, whoever filed it, fixes the small and
medium ones by §5's tests, and puts the rest in a short decision table
for the owner. The plan is done when that table is answered. This is
what makes "no open Windows or core defect without an owner" true on a
date, rather than only of the first list.

### Group C — owned by plans 44–46 or their watch lists, still open (audit only)

#1323 #1330 #1370 #1389 #1395 #1398 #1434 #1468 #1469 #1470 #1484 #1493
#1513 #1525 #1526 #1560 #1564 #1565 #1596 #1756 #480 #940 #998, and
Milestone W's #746 #747 #750.

**Decided:** the plan audits these. For each one the audit ends in one of
three results: closed, with a comment naming the merge and the evidence;
left open, with a comment saying exactly what remains and which plan or
person owns it; or corrected, where the issue's text no longer matches
the tree. The audit plans no new work for them.

### Out

- Mac-only: plan 43's queue (about 35 issues), and #1597, #1650, #1845.
- Feature request: #1620.
- The feature milestones and Milestone W epics named in §2.

## 5. The new rule for a defect found on the way

**Decided.** It replaces the `AGENTS.md` line "A defect found on the way
is filed as an issue unless it blocks the PR", repository-wide and
permanently, in the plan's first chunk. Plans 43, 44 and 45 repeat the
old rule in their own rules sections; the first chunk points each at the
new one.

| Size | Test | What happens |
|---|---|---|
| Small | The fix stays in files the PR already changes, plus their tests. It needs no owner decision, no FFI change, and no new or changed spoken sentence that needs a full screen-reader pass | Fixed in the same PR |
| Medium | The fix reaches a few files outside the PR's own, in the same subsystem, with the same three "needs no" conditions | Judgment. Fixed in the same PR, or deferred to the plan's final sweep chunk, so the plan still closes it |
| High | The fix crosses subsystems, or it needs a design, an owner decision, an FFI change or a Mac | Filed as an issue |

- **Decided:** the sizes are checkable tests, not time estimates. A
  reviewer can read a diff and say which size a find was.
- **Decided:** an inline fix lands in the same PR. No issue is filed. The
  PR body lists it under "Found and fixed here", with the failing test
  that was written first. The owner accepted the cost: a larger PR, and a
  revert that takes the extra fix with it.
- **My call:** a medium fix taken inline also lands in the same PR.
- **My call:** "a few files" is three or fewer outside the PR's own, not
  counting tests, and "subsystem" is one top-level folder of
  `apps/slate-windows/src/SlateWindows` or one module of a crate.
- **My call:** when inline finds would more than double the number of
  non-test files a PR changes, the agent stops taking them and defers the
  rest to the sweep chunk. This is the guard against the second anti-goal.
- **My call:** a find made by a review (the red team, Codex) or by an
  NVDA pass is sized by the same table. A defect in the PR's own feature
  is still fixed on its branch, whatever its size, as today.
- **My call:** the same rule in a non-plan PR has no sweep chunk to defer
  to, so outside a plan a medium find that is not taken inline is filed.
- **My call:** a find whose fix changes text or speech a Mac user hears
  is never taken inline. It cannot be small, because it owes a VoiceOver
  pass. Inside this plan it goes to the sweep chunk, closes on Windows
  evidence and adds a line to the ledger (§6). Outside a plan it is filed.
- **Decided:** the plan's close-out records, per chunk, how many finds
  were fixed inline, deferred to the sweep and filed, so the rule can be
  judged on numbers.

## 6. Decisions the plan puts to the owner

**Decided:** nothing is left unowned. The plan carries a decision table,
as plans 44–46 did, and the owner answers it before delivery starts. Each
row ends as: do it here, close as won't-do, or move to a named milestone.
The recommendations below are the author's starting points.

| Issue | What it is | Recommended home |
|---|---|---|
| #1800 | The empty right-pane panel shows a note to developers | Do here; small once the owner picks the copy |
| #1665 | Whether the editor popover joins the modal surface | Owner decides; a "no" closes it, a "yes" is a medium chunk |
| #1610 | The SVG guard weighs the documents that decode slowly | Do here, with #1635 |
| #1668 #1669 | The Files tree keeps a row's element across a sort, a pin and a list selection | Owner decides; medium to large, reader-facing |
| #1718 | Interactive reads wait behind a running scan's database lock | Design first; large, possibly a plan of its own |
| #1842 | A user-chosen accent from colours that clear contrast | Move to Milestone W; a feature |
| #1848 | The note header's compact width rule | Move to Milestone W's design work |

The plan's reading will add rows where a defect's fix has a real choice
in it (a sentence's wording, a budget, a refusal), as XD1–XD8 did.

### Core changes a Mac user hears

**Decided:** these are in. Each is fixed and closed on Windows evidence:
its tests, the math golden where it applies, and an NVDA pass. The plan
opens one issue, "VoiceOver passes owed", with one line per change, and
plan 43's queue clears it. Known today: #1628, #1629, #1660, #1782 and
#1834. Any other core change the reading finds that alters text or speech
a Mac consumes goes on the same ledger.

- **My call:** `AGENTS.md`'s VoiceOver gate does not change. The ledger
  is this plan's owner decision, as XD4 was plan 45's. Each such PR
  writes `not delivered: VoiceOver pass`, with the ledger's number, and
  closes its issue. Outside this plan the gate reads as it does today.
- **My call:** the ledger also carries Mac adoption. Where a fix adds
  something to core or the FFI that the Mac host should consume, the PR
  keeps the mac lane compiling and the adoption is one more ledger line,
  not an issue of its own. The ledger is therefore titled for both: "Mac
  work owed by plan 47".

## 7. Approaches

Three ways to cut the chunks were considered.

1. **By code area**, as plans 44 and 45 did. Predictable sizes and three
   agents in parallel, but it fixes the named instances and leaves the
   next sibling to be found later.
2. **By defect class.** Each chunk finds every instance of a class,
   fixes them and adds a census or gate. This stops the tail, but sweeps
   cross files, so fewer run in parallel and sizes are harder to predict.
3. **Hybrid.** Class sweeps where the reading shows a class; code-area
   chunks for the rest.

**Decided: the hybrid.** The evidence is in the issue bodies: #1889 is
"three more Windows routes" after three earlier fixes of the same shape;
#1890 is "the twin of #1527"; #1708 is "the same class as #1595"; #1783
and #1836 are one defect filed twice; #1671 and #1866 are the class
#1867 fixed once.

### The classes seen so far (the plan's reading confirms or changes them)

| Sweep | Named instances | Ends in |
|---|---|---|
| Routes that write a note or reload its tab without joining the save chain | #1889 | A census of every writer |
| A control disabled or removed while it holds the keys, so NVDA says "unavailable" | #1671 #1866 | A census or hosted fact per surface that disables under focus |
| A failure sentence built from an exception's raw message | #1890 | A census, as #1697 added for Bases |
| An open dropdown's rows with no place in their set | #1783 #1836 | An axe scan with a dropdown open, in the shell gate |
| An embedded reading element that line reading speaks as "grouping" | #1708 | A hosted fact per embedded element kind |
| A test that waits on the clock, or a fixture that tears down under in-flight work | #1624 #1625 #1673 #1788 #1826 #1843 | Condition waits; a diagnosis first where the cause is unknown |

- **My call:** every sibling a sweep finds is that chunk's own work,
  whatever its size. The §5 table does not apply to it, because finding
  siblings is what the sweep is for. The exception is a sibling that
  needs an owner decision or a Mac: the census ships with that instance
  named as a dated exception, and it gets a decision row or a ledger line.
- **My call:** the plan's reading lists each class's instances before
  adoption, so a sweep's size and its decisions are known when the owner
  answers the decision table.

### Code-area chunks, as first sketched

CI and runner images (#1647 #1699 #1903 #1614 #1616); core math speech
(#1628 #1629); diagrams and SVG (#1631 #1635 #1610); core links (#1704
#1705 #1782); palette, Quick Open and search (#1834 #1837); SQLite (#1917
#1900, and #1718 by decision); canvas (#1722 #1721 #1724 #1838); the
palette seal (#1694 #1695, and #1665 by decision); the Files tree (#1670,
and #1668 #1669 by decision); vault lifecycle and other-session changes
(#1730 #1683 #1714); weak volumes (#1727); editor activation and its
sentence (#1661 #1660); the reading view (#1775); shell focus and peers
(#1846 #1849 #1860); silent or stuck status (#1830 #1791 #1800); one
colour (#1835); comments (#1709).

### Order

**Decided:**

1. The `AGENTS.md` rule, the group C audit and the close-outs.
2. Test and CI stability, so later PRs don't pay for flaky reruns.
3. The sweeps: the ones that protect a note's bytes first, then the
   reader-facing ones.
4. The code-area chunks.
5. A final sweep chunk: the deferred medium finds, the re-count of
   defects filed since 2026-11-13 (§4), and the close-out count.

## 8. The deliverable

- **Decided:** the plan document, plus the specs for the first wave.
  Later specs are written when their wave starts, against that day's
  `main`.
- **My call:** "the first wave" means order steps 1 and 2 above. Step 1
  alone is mostly comments and one `AGENTS.md` edit, and specs for it
  alone would leave nothing to build the day the plan is adopted.
- **My call:** the plan is `docs/plans/47_windows_core_closeout_plan.md`
  with specs under `docs/plans/47_windows_core_closeout/specs/`, in plan
  45's format: status and baseline; scope (in, left to other plans, out);
  what the reading found (already fixed on `main`, corrections to the
  issues, where it meets other plans); decisions for the owner; rules;
  the chunks with size, lanes, AT work and decision; the closure matrix;
  delivery order with the shared-file table; verification lanes; watch
  list; how an agent works a chunk; risks.
- **My call:** chunks are numbered CB-0 upward and decisions CD1 upward.
- As before, every in-scope issue is read in full, body and comments, and
  its cited code checked against the baseline before it is planned.

## 9. Review

**Decided: plan 44's rule, with a light lane.**

- Every chunk gets a fresh Claude red team before its PR opens, with
  `docs/plans/24_red_team_protocol.md`'s prompts and stopping rules.
- Codex also reviews the risky chunks. Named so far: the save chain
  (#1889), the palette seal (#1694 #1695), the Files tree (#1668–#1670),
  link healing (#1704) and the scan-lock design (#1718). The plan may add
  to the list after its reading.
- A chunk that only changes CI config, tests or comments goes on its
  tests alone.
- The plan itself gets one red team and one Codex round before the owner
  reads it, with the findings applied. No further whole-set rounds.
- Field passes are NVDA first and Narrator for the reading view, driven
  by an agent through plan 44 §4 rule 4's recipe (**My call**, carried
  over). A PR that changes what a reader hears stays a draft until its
  pass is in the PR body.

## 10. Left to the plan's author

- The final chunk boundaries, sizes and wave slots, after the full reading.
- Which issues are already fixed on `main` and close on a verification run.
- Which issues are duplicates or misdescribed.
- The exact `AGENTS.md` wording for §5.
- The further owner decisions the reading turns up.
- The Codex list beyond the five chunks named in §9.
- Each sweep's census or gate design.
