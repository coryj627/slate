# 09 — Group C audit (plan 47): the open issues plans 43–46 and Milestone W already own

Audited 2026-11-13 against the worktree `<worktree>` at `main` 1ec9bf31 (#1926). Read-only: nothing was edited, built, tested, commented on, labelled or closed.

## How to read this

- **Sources.** Each issue file in `scratchpad\issues\` was read in full, body and comments, and checked against the live issue with `gh issue view` (state and comment counts match the files). Merges come from `git log` in the worktree and `gh pr view N --json mergedAt,mergeCommit,closingIssuesReferences,body`. Tree claims were checked with `grep` at 1ec9bf31.
- **Results.** CLOSE, LEAVE OPEN, CORRECT. I add CORRECT only when something written on the issue (title, body or a comment) is now false or names the wrong owner or next step. A delivered half the issue never mentioned is recorded in the status comment but is not counted as a correction.
- **Unverified** is marked in line. Three things are unverified throughout: (1) no upstream release list was read (notify, uniffi-bindgen-cs, citationberg, MathCAT, setup-python, the Actions runner), as instructed; (2) nothing the owner did outside the repository can be seen (a `/design-sync` run, a home-host deployment, a human AT pass not yet written down); (3) "plan 43 has merged nothing since 22a" rests on `git log`: no commit after 2026-10-19 (#1518, #1520) delivers a plan 43 PR (26 onward, 22b or 22c), and every later commit under `apps/slate-mac` is a Windows-queue PR's mirror edit.
- **Drafted comments** are for the plan's first chunk to post. Square brackets mark a choice the plan's author has to fill in. Title edits, milestone moves and body edits are the owner's; no comment below assumes one was made.
- If a drafted comment is reused in a PR body, keep "close", "fix" and "resolve" away from issue numbers (`AGENTS.md`, Gotchas).

## Headline

- **CLOSE: 0. LEAVE OPEN: 16. LEAVE OPEN + CORRECT: 10.** No Group C issue can close on evidence that exists today.
- Three are one owner action from closing: #1565 (one ruleset `PUT`), #1756 (`/design-sync`), #1564 (four owner steps).
- Three could close on an owner decision, without new code: #940, #1323, #1389 (see "Owner decisions that would turn a LEAVE OPEN into a CLOSE").
- Two have no owner for what remains: #1330 (its chunk merged without it) and #1596 (its chunk, XB-6, was never delivered).

---

## Per issue

### #1323 — Tests: intermittent timing and clipboard flakes (W7-7 wave)

- **Result:** LEAVE OPEN + CORRECT
- **Owner:** plan 44 WB-7 for the Windows items (delivered); plan 43 PR 28 for the mac fact; plan 43 PR 37 for the Rust fact (delivered another way). Closure matrix (plan 44 §5): closes by hand "only after plan 43 PRs 28 and 37 have landed", then a retitle (RD17).
- **Evidence:**
  - #1674, eb65cdee, 2026-10-27 (WB-7). `Refs #1323`; closes #1294 #1306 #1321. The disposition of every Windows item is the issue's comment of 2026-10-27: graph probe facts fixed (six facts), `TenThousandRows…` already fixed by c247b865 and re-verified, `LargeGraphOpens…` and `RecomputeScalesLinearly…` tagged `kind=benchmark`, the code-copy fact on the clipboard seam, `ReleaseProjection` fixed, `OpenCanvasFromTree` fixed, the palette close-prompt fact fixed.
  - #1877, e48ba189, 2026-11-10 (closes #1611): `FilesTree_ArrowsKeepFocusEnterOpens`, the occurrence in the issue's comment of 2026-10-22, now counts reads, not time.
  - #1880, 4ae810d5, 2026-11-10: `Refs #1323 (the reading scaling guards only)`. `many_unique_citations_stay_subquadratic` and its siblings take confirmation rounds. Its body: "not delivered: operation-count guards", which is what RD17 planned for PR 37.
  - Mac fact: `apps/slate-mac/Tests/SlateMacTests/LeafContextMatrixTests.swift:94-116` is unchanged (`git log --since=2026-10-10` on the file is empty); it still asserts right after `awaitLeafLoads`.
- **What remains:** one item, the mac fact `LeafContextMatrixTests.testSwitchPaneFocusRebindsLeaves`. Owner: plan 43 PR 28 (needs a Mac). The retitle RD17 planned ("the Windows flakes list") no longer fits: the Windows list is empty.
- **What is wrong on the issue:** the comment of 2026-11-10 ("The other tests listed here are still open") is false for the Windows items; the comments of 2026-10-19 and 2026-10-27 say the Rust fact is PR 37's, and #1880 has handled it.
- **Confidence:** medium. The dispositions are the PRs' own claims; I did not run the facts. "One item remains" is my reading of the issue's eleven comments against those PRs.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). Every Windows item on this list is delivered: the eleven in #1674 (eb65cdee, plan 44 WB-7; its table is the comment of 2026-10-27), and the `FilesTree_ArrowsKeepFocusEnterOpens` settle window from the 2026-10-22 comment in #1877 (e48ba189). The Rust guards (`many_unique_citations_stay_subquadratic` and its siblings) were handled by #1880 (4ae810d5) with confirmation rounds, not by the operation counts planned for plan 43 PR 37, which #1880 records as not delivered. So the 2026-11-10 comment is out of date where it says the other tests are still open: one item remains, the mac fact `LeafContextMatrixTests.testSwitchPaneFocusRebindsLeaves`, which is plan 43 PR 28's and has not merged. This issue stays open for that fact alone, and RD17's retitle as the Windows list no longer fits, because the Windows list is empty.

### #1330 — Windows: Narrator says nothing when Ctrl+A selects the reading view

- **Result:** LEAVE OPEN
- **Owner:** on paper plan 44 WB-11 (its row and spec say `Closes #1330`). WB-11 merged without it. **Nobody owns the remainder.**
- **Evidence:** #1688, 18c6815b, 2026-10-28 (WB-11): `Refs #1330. It stays open: Narrator is still silent on Ctrl+A`; `closingIssuesReferences` = #1329, #1422. It serves one Text provider per peer (`ReadingSurface.cs:1686`) and adds the FlaUI journey `ReadingSelectAll_RaisesTextSelectionChangedAndReturnsTheNote` (`ShellAccessibilityTests.ReadingList.cs:124`). Record: `docs/plans/18_windows_port/reports/wb11_reading_list_at_pass_2026-10-28.md:43-50`. No later commit names #1330.
- **What remains:** the defect itself. The UIA side is correct; Narrator still announces nothing. Next step per the WB-11 record: test whether Narrator wants TextPattern2's caret range (`GetCaretRange`) or another event. Windows-deliverable (Narrator on this machine).
- **Confidence:** high that it is open and unowned; the defect was last heard on 2026-10-28 and not re-run here.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): unchanged since the 2026-10-28 comment. Plan 44 WB-11 (#1688, 18c6815b) merged with `Refs` and left this open: the UIA side is correct and pinned by `ReadingSelectAll_RaisesTextSelectionChangedAndReturnsTheNote`, and Narrator still says nothing on Ctrl+A. No chunk of plans 44–46 owns what remains. [Plan 47: name the owner here — a chunk of this plan, a decision row, or Milestone W.] The next step is the one WB-11's record names: whether Narrator needs TextPattern2's caret range or a different event.

### #1370 — both hosts: the file-card hint says "Opens the note in this tab." on audio, video and non-media cards

- **Result:** LEAVE OPEN
- **Owner:** plan 43 PR 32 (RD7: core-owned hint, both hosts in one PR, VoiceOver pass). Plan 44 §1 lists it as out and names only the Windows files it will touch. Not owned by plans 44–46.
- **Evidence:** no commit or PR delivers it (`git log --grep "#1370"` is empty; PR search finds only plan docs, #1373 and #1691). Tree: `CanvasPhrase.ActivationHint` at `Canvas/CanvasDocumentViewModel.cs:5490-5494` still returns "Opens the note in this tab." for `file`; mac `Canvas/CanvasOutlineView.swift:490` the same; core's `CanvasOutlineRow` (`session.rs:21461`) still has `kind` and no target.
- **What remains:** all of it. Owner: plan 43 PR 32; needs a Mac.
- **Confidence:** high.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): not started; the text above is still accurate. Both hosts still pick the hint by kind (`CanvasPhrase.ActivationHint`, `CanvasDocumentViewModel.cs:5490`; mac `CanvasOutlineView.swift:490`), and core's `CanvasOutlineRow` carries no target. This stays with plan 43 PR 32 (RD7: core-owned, both hosts in one PR, with a VoiceOver pass). No plan 43 PR after 22a has merged, and plan 47 does not take this one.

### #1389 — Code preamble verbosity setting changes nothing (mac and Windows)

- **Result:** LEAVE OPEN + CORRECT
- **Owner:** plan 44 WB-21 for the Windows half (delivered); plan 43 PR 52 for the mac half. Closure matrix: closes in "whichever of WB-21 and plan 43 PR 52 merges last".
- **Evidence:**
  - #1710, 0621532c, 2026-10-30 (WB-21): `Refs #1389: this is its Windows half`; the Windows menu dropped the two modes that did nothing.
  - #1711, 0d1928d3, 2026-10-30 (WB-21b): the two modes came back on Windows as real modes, rendered by core's `code_block_preamble_with_verbosity` (`code.rs:251`, FFI `lib.rs:8015`).
  - Mac: `CodePrefs.swift:35` still offers "Preamble + first line"; `CodeBlockView.swift:99` still calls `codeBlockPreamble(language:source:)` alone; `SettingsView.swift:627` still has the fixed sample copy.
- **What remains:** the mac half. Two routes now exist and they conflict: plan 43 PR 52 (remove the modes, RD6) and #1651 (make them real by consuming core's function; its body says `Refs #1526, #1389`). Doing both removes the modes and then restores them. Owner: plan 43; the choice between the two is the owner's.
- **What is wrong on the issue:** the body says Windows "doesn't apply it to code blocks either" (false since #1711); the RD6 comment says the modes are removed on both hosts "now" (Windows removed them and then restored them as real modes the same day).
- **Confidence:** high.
- **Drafted comment:**
  > Correction and status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). On Windows the body no longer holds: #1710 (0621532c, plan 44 WB-21) removed the two modes that did nothing, and #1711 (0d1928d3, WB-21b) brought them back the same day as real modes rendered by core's `code_block_preamble_with_verbosity`. The mac half is open: `CodePrefs.swift` still offers the modes and `CodeBlockView.swift:99` still speaks the preamble alone. Two routes now exist for it, plan 43 PR 52 (remove the modes, RD6) and #1651 (consume core's function); doing both would remove the modes and then restore them, so the owner picks one. This issue stays open until the mac half merges.

### #1395 — core: the unindexed create and save caveats run an unpunctuated reason into the next sentence

- **Result:** LEAVE OPEN
- **Owner:** plan 43 PR 39. Plan 44 §1 keeps it out because it "changes a sentence both hosts speak; mac VoiceOver pass owed". Not owned by plans 44–46.
- **Evidence:** nothing delivered since #1396 (2026-10-17, the empty-reason case, already on the issue). Tree: `a11y.rs:2249-2252` still formats `"{name} was written but not indexed: {reason} It will appear…"`; `a11y.rs:3608-3609` still formats `"Saved, but not indexed yet: {detail} It will appear after the next scan."`; the test at `a11y.rs:6878-6886` pins the run-on.
- **What remains:** all of it: apply `lead_then_sentence` to both templates, update the goldens and corpus rows. Core plus Windows tests can be done here; the VoiceOver pass cannot.
- **Note for the plan:** this is exactly the kind of change the brief's §6 takes in ("core changes a Mac user hears… fixed and closed on Windows evidence", with a ledger line), and it needs no owner decision or FFI change. It is in Group C only because plan 44 parked it with plan 43. Whether plan 47 takes it is a question for the owner (see "What looks wrong in the brief").
- **Confidence:** high.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): not started; the text above and the 2026-10-16 narrowing are still accurate. `CreatePublishedUnindexed` with a non-empty reason (`a11y.rs:2249`) and `vault_error_detail(SavedButUnindexed)` (`a11y.rs:3608`) still splice the reason with no closing period. Owner: plan 43 PR 39, because both hosts speak the sentence and a VoiceOver pass is owed. [Plan 47: keep this line, or say the change is taken here under the "Mac work owed" ledger.]

### #1398 — A .base whose YAML does not parse cannot be repaired, and its error does not say the YAML is broken

- **Result:** LEAVE OPEN
- **Owner:** plan 43 PR 35 (RD5: core-owned failed-state copy on both hosts). Plan 44 §1 lists it as out, interlocked with WB-14. Not owned by plans 44–46.
- **Evidence:** #1693, e885eea0, 2026-10-29 (WB-14, closes #1397) built the Windows failed group with host literals: its body says the three strings are `BasePhrase` constants "until plan 43 PR 35 (#1398) moves failed-state copy into core", and that a base whose YAML does not parse "loads Failed and editable, but core refuses the repair edit… That is #1398's". Tree: `BaseDocumentViewModel.cs:1784` `NoExecutableViews = "No executable base views were found."`; core's `ParseFailed` warning (`bases/mod.rs:303-325`) still reaches hosts only on an execute result.
- **What remains:** the "now" part of RD5 on both hosts (core's failed-state message says the YAML could not be read, with the parse message). The "later" parts (a raw-definition editor, a confirmed replace) are features with no home yet.
- **Confidence:** high.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): not started. Since #1693 (e885eea0, plan 44 WB-14) Windows shows the same failed group as the mac, with host literals and "No executable base views were found." (`BaseDocumentViewModel.cs:1784`), so neither host says the YAML could not be read. Owner: plan 43 PR 35 (RD5: core-owned failed-state copy on both hosts); Windows adopts that copy in the same PR. The raw-definition editor and the confirmed replace are still features with no milestone.

### #1434 — core: creating, renaming and moving files likely fail on exFAT vaults (RENAME_EXCL returns ENOTSUP)

- **Result:** LEAVE OPEN
- **Owner:** plan 44 WB-27 for the Windows proof (delivered, `Refs`); plan 43 PR 38 for the macOS probe and refusal. Closure matrix: closes in plan 43 PR 38 "after WB-27 has merged too".
- **Evidence:**
  - #1725, 500d4382, merged 2026-11-01 (WB-27): `Refs #1434`, `Refs #1470`. Env-gated facts in `crates/slate-core/src/vault/weak_volume_tests.rs`; SMB probed over `\\localhost\C$`; no replacing rename observed, so no refusal branch shipped.
  - #1728, dfddb4fa, 2026-11-01: docs only; FAT32 and exFAT rows in `24_structural_identity_capability_matrix.md` (probe P-W2) from the owner's elevated session. No-replace holds; a case-only rename does not happen on either volume, filed as #1727 (plan 47 Group B).
- **What remains:** what the issue asks: run Slate on a macOS exFAT image, decide and ship the truthful refusal (RD9), and the capability-gated facts. Owner: plan 43 PR 38; needs a Mac.
- **Confidence:** high for the Windows evidence; "macOS not started" rests on `git log`.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). The Windows arm is done: #1725 (500d4382, plan 44 WB-27) proved the no-replace create, rename and move on an SMB share, and #1728 (dfddb4fa) recorded the FAT32 and exFAT runs in the capability matrix (probe P-W2). No replacing rename was observed, so no refusal shipped on Windows; the case-only rename that does nothing on FAT32 and exFAT is #1727. What this issue asks for, the macOS exFAT run and the truthful refusal (RD9), is plan 43 PR 38 and has not started; it needs a Mac.

### #1468 — core: after a Move to Trash, verify that what arrived is the confirmed item (T2)

- **Result:** LEAVE OPEN
- **Owner:** plan 44 WB-30 (the shared outcome contract and the Windows back end) and plan 43 PR 41 (macOS Rust half, mac host, the `~/.Trash` check). Closure matrix: whichever merges last; gate "PR 34's identity primitive; both platforms merged".
- **Evidence:** **WB-30 has not started.** No commit names it beyond plan docs; `TrashOutcome` appears only in a comment in `vault/trash_handoff_tests.rs`; no `IFileOperation` in `crates/slate-core/src`. Its prerequisites: WB-24's real Recycle Bin results are on `main` (#1920, c311b6dd, 2026-11-12; its body does not mention #1468 or the outcome model, so the model was left to WB-30); WB-28 is on `main` (#1729, #1731); **plan 43 PR 34's identity primitive (#1469) is not.**
- **What remains:** all of it. WB-30 is blocked on plan 43 PR 34, which sits in the paused mac queue behind 22c.
- **Confidence:** high.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): not started. The shared outcome contract and the Windows back end are plan 44 WB-30, which has not begun: it waits for the identity primitive of plan 43 PR 34 (#1469), which is not on `main`. Its other two prerequisites are met: WB-24's real Recycle Bin results (#1920, c311b6dd) and WB-28 (#1729, #1731). The macOS Rust half, the mac host and step 1's `~/.Trash` check are plan 43 PR 41. Plan 47 does not take this.

### #1469 — core + mac: identity-checked structural undo, and refuse undo where file IDs are not identities

- **Result:** LEAVE OPEN
- **Owner:** plan 43 PR 34 (`rust, swift, windows`; W4, after 22c). Plan 44 §1 lists its mac adoption as out. Not owned by plans 44–46; plan 44's WB-30 depends on it.
- **Evidence:** nothing delivered (`git log --grep "#1469"` finds only #1471, the 2026-10-17 contract record). Tree: the mac batch undo still calls `session.undoBatchMove(opId:)` (`AppState.swift:1719`); the FFI still has both `undo_batch_move` and Windows' `undo_batch_move_if_identities` (`lib.rs:1209`).
- **What remains:** all of it. Owner: plan 43 PR 34; needs a Mac.
- **Confidence:** high.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): not started. The mac batch undo still calls `undoBatchMove` without identities (`AppState.swift:1719`). Owner: plan 43 PR 34, which has not merged. Plan 44 WB-30 (#1468) cannot start until this PR's core identity primitive is on `main`, so this is the first link in that chain.

### #1470 — core: no-replace renames on SMB (unprobed) and Linux NFS likely fail, and FAT32 fakes RENAME_SWAP

- **Result:** LEAVE OPEN
- **Owner:** as #1434: WB-27 (Windows arm, delivered) and plan 43 PR 38.
- **Evidence:** as #1434 (#1725 500d4382; #1728 dfddb4fa). The Windows SMB probe is the table in #1725's body.
- **What remains:** the macOS smbfs probe (to-do 1), the behaviour with #1434 (to-do 2), the capability-gated provider fact (to-do 3): plan 43 PR 38. The Linux NFS finding waits for a Linux host; no plan owns it.
- **Confidence:** high for the Windows evidence; "macOS not started" rests on `git log`.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). On Windows an SMB share was probed by #1725 (500d4382, plan 44 WB-27): an occupied name refuses, a free name lands, and a case-only rename keeps the entry and its id. #1728 (dfddb4fa) added the FAT32 and exFAT rows to the capability matrix. Still open here: the macOS smbfs probe, the capability-gated provider fact and the refusal, all plan 43 PR 38 (RD9, with #1434). The Linux NFS note waits for a Linux host and has no owner yet.

### #1484 — core: with two sessions in one process, the older one's no-change rescan runs about 1.8x slower

- **Result:** LEAVE OPEN (and correct #1653, which is not in Group C)
- **Owner:** plan 44 WB-31 (delivered, `Refs`). Closure matrix: "by hand after a macOS measurement confirms the remedy (the mac follow-up #1653), or the owner withdraws the macOS observation (WD21)".
- **Evidence:**
  - #1909, 0c5e2794, 2026-11-12 (WB-31): `Refs #1484`. `benches/two_sessions_bench.rs`; two guards. Its body: the remedy (idle-cache release) was dropped by the owner on 2026-11-11; "not delivered: the macOS measurement (#1653)".
  - #1911, 86bb013a, 2026-11-12: libsqlite3-sys 0.39.1, which builds SQLite without `SQLITE_ENABLE_MEMORY_MANAGEMENT`.
  - #1918, dbf8480a, 2026-11-12: the single-session regression #1912 (closed).
  - #1926, 1ec9bf31, 2026-11-13 (closes #1923): the two-sessions timing guard reads medians and confirms with a fresh reading.
- **What remains:** a macOS run of the bench at current `main`, or the owner's withdrawal. Owner: #1653 (mac; no plan 43 row exists for it). Needs a Mac.
- **Side correction:** #1653's body says "before and after its remedy". No remedy shipped. Suggested comment for #1653 below.
- **Confidence:** high.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): the 2026-11-12 comment still stands, with two additions. #1926 (1ec9bf31) made the two-sessions timing guard read medians and confirm with a fresh reading, and #1918 (dbf8480a) removed the single-session slowdown filed as #1912. This stays open for one thing: a macOS run of `benches/two_sessions_bench.rs` on `main` (#1653), or the owner's word that the macOS observation is withdrawn (WD21). It needs a Mac, so plan 47 does not take it.
- **Drafted comment for #1653:**
  > Correction (plan 47 audit, 2026-11-13): no remedy shipped. The idle-cache release was dropped on 2026-11-11, once libsqlite3-sys 0.39.1 (#1911) removed the shared page pool. The measurement owed is one run of `benches/two_sessions_bench.rs` on a Mac at current `main`: the older session's no-change rescan with a second session open, against alone. Windows reads 1.01x to 1.03x (#1484).

### #1493 — deps: move notify from =9.0.0-rc.5 to 9.0.0 when it ships

- **Result:** LEAVE OPEN (watch list)
- **Owner:** plan 44 §8 and plan 45 §8 watch lists ("weekly"). Neither names a person, and no workflow watches the release list.
- **Evidence:** `Cargo.toml:139` `notify = "=9.0.0-rc.6"` (#1553, ed01221e). The issue's stale "rc.5" was already corrected by plan 45 XB-0 in the comment of 2026-10-24, and that comment's line number still holds. `.github/dependabot.yml:75-77` ignores notify. Contract 40 AR-72 not re-read.
- **What remains:** the bump, when 9.0.0 exists and is seven days old. **Unverified:** whether 9.0.0 has shipped since 2026-10-24; the release list was not read.
- **Next check and who:** read crates.io's notify releases. Nobody is named. The plan should name one: an agent at each plan 47 wave start is my suggestion, or the owner.
- **Confidence:** medium (upstream not read).
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): still waiting. `Cargo.toml:139` pins `notify = "=9.0.0-rc.6"`, as the 2026-10-24 correction says, and Dependabot ignores notify (`.github/dependabot.yml:77`). The release list was not re-read in this audit. Next check: [who and when], crates.io's notify releases; once 9.0.0 is seven days old, steps 1 to 4 above apply, starting from rc.6.

### #1513 — core: after the open vault's folder is renamed, FsVaultProvider's reads still resolve by the opened path

- **Result:** LEAVE OPEN + CORRECT
- **Owner:** plan 43 PR 22c (the code; "needs a Mac to verify", plan 44 §1 and plan 45 §1). Not owned by plans 44–46. Plan 44 WB-28 used the issue as the place to agree helper names.
- **Evidence:**
  - Unix: `WATCH_REARM_ENABLED: bool = false` (`session/watch.rs:108`); no 22c commit exists.
  - Windows: #1729, fb131216, 2026-11-01 (WB-28 PR 1, `Refs #1513`): `FsRoot::open_beneath`, `stat_beneath`, `list_beneath`, `same_entry_beneath`, `stored_spelling_beneath` exist under `#[cfg(windows)]` only (`fs_root.rs:839-1106`; `Follow` is `#[cfg(windows)]` at `:1224`), and `fs.rs` calls them under `#[cfg(windows)]` (`:230`, `:240`, `:758`, `:2026-2030`, `:2527`, `:2604`). Its body: "Not delivered: table B's Unix declarations… 22c adds them."
  - WB-28's delivery record (`wb28_descriptor_relative_session_on_windows.md:60-93`): step 0 ran 2026-11-01 with no stop condition, "D2 needs no answer"; "D4 (the by-path fallback) is not needed"; "Table B has Windows bodies only".
- **What remains:** the issue as filed, on macOS and Linux: the provider's reads beneath the held root, the re-arm switched on, its two facts, the 10k-note measurement. Owner: plan 43 22c.
- **What is wrong on the issue:** the body's "Windows follows under Milestone W (22W)" (22W shipped in plan 44 WB-28). In the 2026-11-01 design comment: "PR 1 declares table B on every platform. Its Unix bodies call today's pathname code, so 22c only replaces those bodies" (not delivered that way), and "D2–D4 stay open" (D3 was answered on the issue; D2 and D4 became moot and that is recorded only in the spec).
- **Confidence:** high.
- **Drafted comment:**
  > Correction and status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). The body's last line is out of date: 22W shipped as plan 44 WB-28 (#1729 fb131216, #1731 ed172e07), and on Windows the provider's reads already resolve beneath the held root (`FsRoot::open_beneath`, `stat_beneath`, `list_beneath`). Two points of the 2026-11-01 design comment changed in delivery: table B has Windows bodies only, so 22c adds the Unix declarations as well as their bodies; and D2 and D4 need no answer (FAT volumes answer no `FileIdInfo`, and the SQLite fallback was not needed). What remains is this issue as filed, on macOS and Linux: the reads still go by the opened path, and `WATCH_REARM_ENABLED` is `false` (`session/watch.rs:108`). Owner: plan 43 22c; it needs a Mac.

### #1525 — both hosts: unsaved buffers survive closing, quitting and a crash (hot exit)

- **Result:** LEAVE OPEN + CORRECT
- **Owner:** plan 44 WB-29a (shared design and core store) and WB-29 (Windows half), both delivered; plan 43 PR 29 for the mac half. Closure matrix: whichever of WB-29 and the mac half merges last; gate "both hosts merged and field-passed (WD20)".
- **Evidence:**
  - #1732, 6a0d4b41, 2026-11-02: the design (`wb29a_hot_exit_shared_design_and_core_store.md`). `Refs`.
  - #1734, 114f737d, 2026-11-02: the core store and its FFI. `Refs`.
  - #1735, 27de5b3e, 2026-11-02: WB-29's Windows host design on an amended store.
  - #1738, 43f77563, 2026-11-09: the store amendment (a claim replaces only the claim it names). `Refs`.
  - #1873, b8ca1c20, 2026-11-09: the Windows host, with an agent-operated NVDA pass in its body ("Restored 1 unsaved note."). `Refs #1525… The Mac half is not merged or field-passed, so this PR does not close the issue.`
  - The issue has no comments at all.
- **What remains:** the mac half (plan 43 PR 29, with #1402 and a VoiceOver pass). Needs a Mac. Also from #1873's "not delivered": a human screen-reader pass.
- **What is wrong on the issue:** the title and body name plan 43 PR 29 as the design's owner and list design questions as open ("decide whether core owns the store… whether Windows adopts hot exit"). All are answered and built.
- **Confidence:** high.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). The design questions above are answered in `docs/plans/44_windows_backlog/specs/wb29a_hot_exit_shared_design_and_core_store.md` (#1732, 6a0d4b41): core owns the store, and Windows adopts hot exit. The core store is on `main` (#1734 114f737d, amended by #1738 43f77563), and so is the Windows host with its NVDA pass (#1873 b8ca1c20, plan 44 WB-29). Still open: the mac half, plan 43 PR 29, which consumes the same store and carries #1402 and the VoiceOver pass. This issue stays open until that merges.

### #1526 — core: code_block_preamble takes a verbosity, so the setting can come back on both hosts

- **Result:** LEAVE OPEN + CORRECT
- **Owner:** plan 44 WB-21b (core and Windows, delivered, `Refs`). Closure matrix: closes in "the mac follow-up WB-0 filed, #1651"; gate "VoiceOver pass per mode on mac".
- **Evidence:** #1711, 0d1928d3, 2026-10-30: `Refs #1526: it stays open until the mac consumer (#1651) merges.` Core `code_block_preamble_with_verbosity` (`code.rs:251`), FFI (`lib.rs:8015`), Windows consumers (`ReadingDocumentBuilder.cs`, `ReadingContentViewModel.cs`, `EditorInteractions.cs`). #1651 is open with no comments. The issue has no comments.
- **What remains:** the mac consumer, the Settings samples through core, a VoiceOver pass per mode: #1651. No plan 43 row exists for #1651.
- **What is wrong on the issue:** it asks that `code_block_preamble` itself "takes a verbosity". WD13 delivered a sibling function and left `code_block_preamble` unchanged, on purpose, so the mac compiles untouched. The issue should say so before someone changes the signature.
- **Confidence:** high.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). Core and Windows are done in #1711 (0d1928d3, plan 44 WB-21b). The verbosity is a sibling function, `code_block_preamble_with_verbosity`, over the token stream (WD13); `code_block_preamble` is unchanged, so the mac compiles as before. Windows offers and speaks all three modes, with an NVDA check per mode in that PR. Still open: the mac consumer, the Settings samples through core and a VoiceOver pass per mode, which is #1651. This issue stays open until #1651's PR merges.

### #1560 — math: braille changes from MathCAT 0.7.7 need a transcriber review

- **Result:** LEAVE OPEN (watch list)
- **Owner:** the owner (plan 45 §1: "a person's work, not an agent's"; §8: waits for "a braille transcriber's verdict… when the owner has one").
- **Evidence:** ask 2 is done (the 2026-10-22 comment: daisy/MathCAT#890–#892). Ask 3's golden exists: `crates/slate-core/tests/fixtures/math_golden/expected.txt` (#1587, ab00a34b). #1630 (a1a7fe6f, XB-8) posted its braille movement on the issue on 2026-10-25. No verdict is recorded.
- **What remains:** ask 1, the verdict; then a core PR per corrected item with its golden diff (plan 45 §8 says that PR is deliverable on Windows). **Unverified:** the state of the three upstream reports.
- **Next check and who:** the owner, when a transcriber is available. Nothing for an agent until then.
- **Where plan 47 meets it:** #1628 and #1629 (Group A, core math speech) change the math golden. Any braille line that moves should be posted on #1560 the way XB-8 did, or the review falls further behind.
- **Confidence:** medium (a verdict may exist off the repository).
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): ask 2 and ask 3 stand as the comments above record; ask 1, a transcriber's verdict, is still owed and is the owner's to arrange. The upstream reports (daisy/MathCAT#890 to #892) were not re-read in this audit. [If plan 47 adopts it: plan 47's core math changes (#1628, #1629) will post here any braille line that moves in the golden, as XB-8 did.]

### #1564 — ci: Dependabot PRs cannot run CI under the owner-only Actions policy

- **Result:** LEAVE OPEN
- **Owner:** plan 45 XB-12 (delivered, `Refs`); closure matrix gate: "the owner's Studio step".
- **Evidence:**
  - #1578, 8e173d32, 2026-10-22: the pause.
  - #1637, 9122ed75, 2026-10-25 (XB-12): `Refs #1564`; plan 42 §3.7, both routers, `policy_only_actors` in `ci/mac-runner/allowlist.json`. Its "Not delivered" lists the owner's steps in order.
  - Read-only checks today: `gh api repos/coryj627/slate/actions/policies/7070` still allows only actor 933688 and has `updated_at` 2026-10-10, so `apply-policy.sh --apply` (step 2) has not run. Every `open-pull-requests-limit` in `.github/dependabot.yml` is 0 (lines 46, 60, 87, 101, 116), so steps 3 and 4 have not run. No PR is open.
- **What remains:** the owner's four steps from the 2026-10-25 comment: deploy the home host's new module and `config.json`; `apply-policy.sh --apply`; a PR raising one ecosystem's limit to 1 and a read of its first Dependabot PR's runs; raise the rest. **Unverified:** step 1 (not readable from the repository).
- **Confidence:** high for steps 2 to 4.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). The design and code are on `main` (#1637, 9122ed75, plan 45 XB-12). None of the owner's four steps from the 2026-10-25 comment shows as done: policy 7070 still allows only the owner (last updated 2026-10-10), and every `open-pull-requests-limit` is 0. Whether the home host was redeployed cannot be read from the repository. This stays open until the owner has run those steps, in that order.

### #1565 — ci: main accepts merges onto heads whose CI never finished

- **Result:** LEAVE OPEN (the nearest to closing: one owner command)
- **Owner:** plan 45 XB-0 (step 3) and XB-13 (delivered, `Refs`). Closure matrix gate: "a week of PRs under the lane checks". #1639's body: closes "once seven days of PRs show every lane reporting with no false red… Then this adds the lane checks to `main`'s ruleset."
- **Evidence:**
  - #1577, a0d7a9ad (the `pr gate` check); the ruleset (step 2); #1605, 1aeb0115 (step 3, `AGENTS.md`).
  - #1638, 46d7a34a, 2026-10-25: `main` pushes group by commit.
  - #1639, 0f6dcc13, 2026-10-25: every lane reports on every PR. "not delivered: the ruleset change and the week's watch (the owner's step)".
  - The week, read today with `gh pr checks`: the 55 PRs merged after #1639 up to 2026-11-02 (#1642 to #1757) each show every lane reporting, and no check in a failing state on the merged head (only `pass` and `skipping`). **Unverified:** re-runs. A red that was re-run to green reads as a pass here, and #1323's comments record such re-runs on later PRs (#1853, #1876).
  - The ruleset today: `gh api repos/coryj627/slate/rulesets/25660813` requires `pr gate` only, strict, last updated 2026-10-22.
  - The payload in #1639's body names twelve contexts; each still matches a job `name:` in today's workflows (`pr-gate.yml`, `windows.yml`, `rust.yml`, `swift-tests.yml`, `a11y-check.yml`, `license-headers.yml`, `audit.yml`, `mac-runner-tests.yml`, `windows-runner-tests.yml`). I did not check whether jobs added since (for example the real Recycle Bin job of #1920) should join the list.
- **What remains:** the owner's `gh api -X PUT repos/coryj627/slate/rulesets/25660813 --input ruleset.json` from #1639; then `AGENTS.md:126` ("the lanes are yours to check until the ruleset requires them (#1565)") and `:132` lose their caveat. The `AGENTS.md` edit could ride in plan 47's first chunk, which already edits that file, once the ruleset is live.
- **Why not CLOSE:** the closure matrix's gate (the week) is met on final states, but the issue's own "still open" list is "lane-level required checks", and the ruleset does not require them. A stricter reader could also ask for the re-run history first.
- **Confidence:** medium, for those two reasons.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). Steps 1 to 3 and the lane restructure are on `main` (#1577, #1605, #1638 46d7a34a, #1639 0f6dcc13). The week's watch is met as far as final states show: the 55 PRs merged between #1639 and 2026-11-02 each report every lane, with no failing check on the merged head (`gh pr checks`; re-runs along the way were not counted). The ruleset still requires `pr gate` alone (id 25660813, last updated 2026-10-22), and the twelve check names in #1639's payload all still exist. What remains is the owner's `gh api -X PUT` from #1639, then the two `AGENTS.md` lines that cite this issue.

### #1596 — ci: setup-python v7.1.0 and runner 2.338.1 wait for the release cooldown until 2026-10-29

- **Result:** LEAVE OPEN + CORRECT
- **Owner:** plan 45 XB-6 ("dated, on or after 2026-10-29, in any free slot"). **XB-6 was never delivered**; it is the one plan 45 chunk with no PR.
- **Evidence:** no commit mentions setup-python since #1539 (2026-10-22). Tree: eleven `actions/setup-python@5fda3b95… # v7.0.0` lines: `audit.yml:108`, `mac-ci-pilot.yml:52,74,125,175`, `mac-runner-tests.yml:58`, `windows-ci-pilot.yml:55,382`, `windows-runner-tests.yml:108`, `windows.yml:688,787`. `ci/windows-runner/golden/versions.json:2` `"runnerVersion": "2.338.0"`. The issue has no comments.
- **What remains:** both moves. The cooldown ended on 2026-10-29, so nothing blocks it but an owner. The golden rebuild itself is the owner's (D7). **Unverified:** whether releases newer than v7.1.0 and 2.338.1 exist; the PR re-reads both lists when it opens.
- **What is wrong on the issue:** "9 `uses:` lines" in five files. It is eleven in six: `windows.yml:688` and `:787` are new (`git log -S` points at #1656 and #1700).
- **Where plan 47 meets it:** `ci/windows-runner/golden/versions.json` is also what #1614 and #1903 (plan 47's CI chunk) touch. One PR for the three is cheaper than a rebase, but the brief's non-goal ("re-planning a chunk plans 44–46 already own") needs the owner's word first.
- **Confidence:** high.
- **Drafted comment:**
  > Correction and status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): not delivered, and both cooldowns ended on 2026-10-29. There are now eleven `actions/setup-python` pins at v7.0.0, not nine: `windows.yml:688` and `:787` were added after this was filed. `ci/windows-runner/golden/versions.json` still names runner 2.338.0. Owner: plan 45 XB-6, which no PR has delivered [plan 47: or "taken into plan 47's CI chunk with #1614 and #1903"]. The PR re-reads both release lists when it opens and takes the newest release past the cooldown.

### #1756 — windows: the checks the wpf design spikes left open are done

- **Result:** LEAVE OPEN + CORRECT
- **Owner:** plan 46 (DS-1, DS-4, DS-5 for the three "waiting" checks, all delivered) and the owner (`/design-sync`). Closure matrix: "the optional 200% runs by the captures in §7. The last of those, and the owner's `/design-sync`, close it."
- **Evidence:**
  - The three "checks that wait for other work" are ticked on the issue: #1822 (d65c852e, DS-1), #1825 (2865d6ae, DS-4), #1827 (2dc6f805, DS-5).
  - All twenty plan 46 chunks have merged (#1821 to #1858), and DS-0's per-chunk issues #1802 to #1820 are all closed.
  - Optional 200% runs, by spike: spike 1 (Segoe UI Variable families) — #1823's discovery pass, "launched app, dark, 200%: 66 captures"; spike 2 (ligatures) — #1822, "Launched app, dark, 200%"; spike 3 (Snap Layouts) — #1844, recorded on the issue 2026-11-06; spike 4 (ClearType in popups) — #1825 ("physical capture at 200%", `wpf-feasibility.md:107`) and #1829; spike 5 ran at 200% originally. **Spike 6 (non-adorner selection with right-to-left text): no 200% record found.** Its switch is in the app (`TextBoxSelection.cs:27`). My search was the PR bodies of wave 1 and `wpf-feasibility.md`, not every PR, so "none exists" is unverified.
  - `/design-sync`: the box is unticked. `design/windows/github.md` says "Last sync: 2026-11-01T23:36:17Z" and has one commit (#1736, 2026-11-02). **Unverified:** whether the owner ran a sync that did not touch that file.
- **What remains:** the owner's `/design-sync` (plan 46 §4 rule 7: only the owner starts it). Optionally a 200% look at spike 6; the box is labelled optional.
- **What is wrong on the issue:** the 2026-11-04 comments say the spiked features "aren't in the app yet" and that nothing can close before wave 1. All five waves are in.
- **Confidence:** medium (the owner's step is not visible from the repository; the spike 6 search was not exhaustive).
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). All twenty chunks of plan 46 have merged (#1821 to #1858), so the 2026-11-04 notes that these features are not in the app are out of date. The optional 200% check now has a launched-app record for spike 1 (#1823), spike 2 (#1822), spike 3 (#1844) and spike 4 (#1825, #1829); none was found for spike 6, selection across right-to-left text. The owner's `/design-sync` box is unticked, and `design/windows/github.md` still gives 2026-11-01 as the last sync. This stays open for that step.

### #480 — Security: citationberg is a Git patch for quick-xml 0.41 — migrate to the registry-published fix

- **Result:** LEAVE OPEN (watch list)
- **Owner:** plan 44 §8 ("monthly"); plan 43 §11.3 watch list. No person named.
- **Evidence:** `Cargo.toml:176` still patches `citationberg` to commit 06a591e2; `.cargo/audit.toml` is `ignore = []`; `crates/slate-core/Cargo.toml:81` `hayagriva = "0.10"`. The issue's 2026-10-09 triage comment already corrected the title and the exit criteria; nothing on it is stale.
- **What remains:** the registry migration when a release exists. `docs/runbooks/citation-dependency-policy.md:90-99` requires "complete Rust and native Mac/Windows qualification" in the same change and forbids closing before it, so the change needs a Mac. **Unverified:** whether citationberg or hayagriva has published a release with quick-xml 0.41 or later.
- **Next check and who:** read the two crates' release lists. Nobody is named; an agent at a plan 47 wave start can do the read, the migration cannot be plan 47's.
- **Confidence:** medium (upstream not read).
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): unchanged since the 2026-10-09 triage. `Cargo.toml:176` still patches citationberg to commit 06a591e2, `.cargo/audit.toml` has no ignores, and `hayagriva` is 0.10. The registries were not re-read in this audit. Next check: [who and when]. The removal needs the runbook's native Mac and Windows qualification, so it is not a plan 47 change.

### #940 — Session open: descriptor-relative interior resolution (A→B→A open race)

- **Result:** LEAVE OPEN + CORRECT
- **Owner:** plan 43 22a (delivered), 22b (mac host half, not delivered); plan 44 WB-28 for 22W (delivered, `Refs`). Closure matrix: "by hand after 22b and 22W, per RD15".
- **Evidence:**
  - 22a: #1518, 3f0b45de, 2026-10-19.
  - 22W: #1729, fb131216, 2026-11-01 (`Refs #940`), and #1731, ed172e07, 2026-11-01 (`Closes #1475`, `Refs #940`).
  - 22b: **not delivered.** No commit names it except the plan document (#1524). Its fact `a_renamed_open_vault_keeps_working_and_can_show_its_new_path` exists only in `22_descriptor_relative_session_design.md:816`; that design's own header (`:6-7`) still says 22b is not delivered; the issue's 2026-10-19 comment says the fact needs #1513, which is open.
  - The issue has no milestone (`gh issue view 940 --json milestone` is null).
- **What remains:** 22b (design §7: a mac host display and canonicalisation audit, and the host fact above). Owner: plan 43; it follows 22c (#1513) and needs a Mac.
- **What is wrong on the issue:** the 2026-11-01 comment says "With 22b and 22W both delivered, this issue can be closed by hand (RD15) once #1731 merges." 22b is not delivered. RD15's plan ("after 22b… moves to Milestone W, with 22W as its only remaining part") has been overtaken the other way round: 22W is done and 22b is what is left, so nothing remains for Milestone W.
- **Confidence:** high.
- **Drafted comment:**
  > Correction and status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). 22W is on `main`: #1729 (fb131216) and #1731 (ed172e07), plan 44 WB-28. The 2026-11-01 comment is wrong about 22b: the mac host half has not been delivered (no plan 43 PR after 22a has merged, and its fact `a_renamed_open_vault_keeps_working_and_can_show_its_new_path` is not in the tree; it depends on #1513). So RD15's move to Milestone W no longer applies: nothing is left for that milestone, and what remains is plan 43's 22b, after 22c. This stays open until 22b merges or the owner ends it by hand.

### #998 — uniffi 0.32 blocked: uniffi-bindgen-cs has no 0.32 tag

- **Result:** LEAVE OPEN + CORRECT (watch list)
- **Owner:** plan 44 §8 ("weekly") and WB-32 (the local spike, `Refs`). **WB-32 has not been run**: its deliverable is a docs PR recording the spike, and no such commit exists. Closure matrix: closes in "the dependency bump when a tag exists".
- **Evidence:** `Cargo.toml:23` `uniffi = "0.31"`. `apps/slate-windows/uniffi-bindgen-cs.version` is `v0.11.1+v0.31.0` (#1538, 834f3573). Readers: `windows.yml:637-650`, `.github/actions/windows-native-build/action.yml:54-67`. `generate-bindings.ps1` no longer names a tag. `swift-tests.yml` has no `cache:` line. `.github/dependabot.yml:72-74` ignores `uniffi` minor and major updates; there is no Renovate config. The issue has no comments.
- **What remains:** the bump, when a generator release supports 0.32. **Unverified:** the state of NordSecurity/uniffi-bindgen-cs#176 and its tags.
- **What is wrong on the issue:** the "three pins" (`windows.yml:63`, `:69`, `generate-bindings.ps1:10`) are now one file; "newest tag v0.11.0+v0.31.0" (the pin is v0.11.1); blocker 2's workaround (`cache: rust` in `swift-tests.yml`) no longer exists to drop; the "Renovate ignore" is a Dependabot ignore, already in place.
- **Next check and who:** look for a `v0.x+v0.32.0` tag. Nobody is named. WB-32 is a free-slot Windows spike that needs no Mac.
- **Confidence:** medium (upstream not read).
- **Drafted comment:**
  > Correction and status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). The pins named above have moved: the generator's tag lives in one file, `apps/slate-windows/uniffi-bindgen-cs.version` (now `v0.11.1+v0.31.0`, #1538), read by `windows.yml:637-650` and `.github/actions/windows-native-build/action.yml:54-67`. `swift-tests.yml` no longer sets `cache: rust`, so blocker 2 needs re-checking rather than the workaround, and the suggested bot pause exists as Dependabot's `uniffi` ignore (`.github/dependabot.yml:72-74`). Plan 44's WB-32 spike against NordSecurity/uniffi-bindgen-cs#176 has not been run. Upstream was not re-read in this audit; next check: [who and when], for a tag that names uniffi 0.32.

### #746 — Windows: graph view (W6-2)

- **Result:** LEAVE OPEN
- **Owner:** the owner (Milestone W human-AT residual; plan 44 §1 and §9: "the owner's sign-offs").
- **Evidence:** #1221, 97dc5c0b, 2026-09-15 (the close-out; `closingIssuesReferences` empty after the body was reworded). `docs/plans/18_windows_port/reports/w6_2_graph_at_checklist.md`: last commit 675dec01 (2026-09-23); 42 occurrences of "Pending"; its header says every human cell is Pending until a named run. I counted the word, I did not read each row.
- **What remains:** the owner's sign-off of the eleven-row checklist (FD-3, FD-6). **Unverified:** whether the checklist's routes still match the shell after plan 46 (the rail became an icon strip with a More menu, the menus open from the logo, the title bar changed); the file predates all of that.
- **Confidence:** medium (a human pass may exist that is not written down; rows not read one by one).
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): unchanged. `reports/w6_2_graph_at_checklist.md` was last edited on 2026-09-23 and its human cells still read Pending. This stays open for the owner's sign-off of that checklist (FD-3, FD-6). The checklist's routes were written before plan 46 changed the rail, the menus and the title bar, and were not re-read here.

### #747 — Windows: editor AutomationPeer — TextPattern + semantic span ranges (W7-1)

- **Result:** LEAVE OPEN
- **Owner:** the owner (human acceptance residual).
- **Evidence:** #1227, 541f7ceb, 2026-09-18 (implementation; `closingIssuesReferences` empty); #1232, fb2c1078, 2026-09-19 (review remediation). `reports/w2_editor_at_checklist.md`: last commit 39dd2457 (2026-09-30); 44 occurrences of "Pending". `parity_matrix.md:291`: "implemented; CI + human AT pending".
- **What remains:** as the 2026-09-18 comment says: audible confirmation, the remaining manual routes and live IME, a licensed JAWS pass. Braille is owner-deferred.
- **Confidence:** medium, as #746.
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13): unchanged since the 2026-09-18 comment. The implementation is #1227 (541f7ceb) with #1232 (fb2c1078), and `reports/w2_editor_at_checklist.md` still has its human cells Pending. Open for the owner: audible confirmation, the remaining manual routes and live IME, and a licensed JAWS pass. Braille stays owner-deferred.

### #750 — Windows: JAWS/NVDA conformance matrix, axe-windows CI gate, per-surface AT checklists (W7-4, rolling)

- **Result:** LEAVE OPEN + CORRECT
- **Owner:** the owner's AT passes, then Milestone W's W8-6 (final acceptance). Rolling by design.
- **Evidence:** #1226, 55dbb603, 2026-09-17: the authoring PR the 2026-09-16 note asks for (`WcMatrixEvidenceCensus.cs`, `_at_pass_template.md`, the five checklists `w1_shell`, `w2_editor`, `w3_content`, `w4_panels`, `w5_commands`, the W7 rows in `parity_matrix.md`). #1231, 9f5a2bf2, 2026-09-19: its review remediation. `parity_matrix.md:294`: "instrument authored; CI + human AT pending". All seven checklist files still contain "Pending" (25 to 44 occurrences each). `w_c_matrix.md` has been kept up by later PRs (last: #1873). The issue has no comments.
- **What remains:** obligation 2 (per wave close), obligation 3 (the human NVDA and JAWS cells), obligation 4 (the W8-6 hand-off).
- **What is wrong on the issue:** obligation 1 reads as still to do ("One authoring PR now"); it merged on 2026-09-17 and the issue never says so. The counts in the note (30 journeys, 46 rows) are older than three plans of work. I did not recount them reliably, so the comment gives no new numbers.
- **Confidence:** medium (counts not re-derived; human passes may exist off the repository).
- **Drafted comment:**
  > Status at `main` 1ec9bf31 (plan 47 audit, 2026-11-13). Obligation 1, the authoring PR, is done: #1226 (55dbb603) added `WcMatrixEvidenceCensus`, `_at_pass_template.md`, the five checklists and the W7 parity rows, and #1231 (9f5a2bf2) took its review. The counts in the 2026-09-16 note have moved with later waves and are not restated here. What remains is the rolling part: the human NVDA and JAWS cells, still Pending in all seven checklists, and the W8-6 hand-off. Owner: the owner's AT passes, then Milestone W's W8-6.

---

## Summary table

| Issue | Result | Owner of what remains | What remains | Confidence |
|---|---|---|---|---|
| #1323 | LEAVE OPEN + CORRECT | plan 43 PR 28 | one mac fact (`LeafContextMatrixTests`) | medium |
| #1330 | LEAVE OPEN | **nobody** (WB-11 merged without it) | Narrator silent on Ctrl+A | high |
| #1370 | LEAVE OPEN | plan 43 PR 32 | all of it | high |
| #1389 | LEAVE OPEN + CORRECT | plan 43 PR 52, or #1651 | the mac half | high |
| #1395 | LEAVE OPEN | plan 43 PR 39 | all of it | high |
| #1398 | LEAVE OPEN | plan 43 PR 35 | core's failed-state copy, both hosts | high |
| #1434 | LEAVE OPEN | plan 43 PR 38 | the macOS probe and refusal | high |
| #1468 | LEAVE OPEN | plan 44 WB-30 (not started), plan 43 PR 41 | all of it | high |
| #1469 | LEAVE OPEN | plan 43 PR 34 | all of it | high |
| #1470 | LEAVE OPEN | plan 43 PR 38 | the macOS SMB probe, the fact, the refusal | high |
| #1484 | LEAVE OPEN | #1653 (mac) or the owner | a macOS measurement | high |
| #1493 | LEAVE OPEN | watch list, nobody named | notify 9.0.0 | medium |
| #1513 | LEAVE OPEN + CORRECT | plan 43 22c | the macOS and Linux reads; the re-arm | high |
| #1525 | LEAVE OPEN + CORRECT | plan 43 PR 29 | the mac half | high |
| #1526 | LEAVE OPEN + CORRECT | #1651 (mac) | the mac consumer and VoiceOver passes | high |
| #1560 | LEAVE OPEN | the owner | a transcriber's verdict | medium |
| #1564 | LEAVE OPEN | the owner | four steps | high |
| #1565 | LEAVE OPEN | the owner | one ruleset `PUT`, then two `AGENTS.md` lines | medium |
| #1596 | LEAVE OPEN + CORRECT | plan 45 XB-6 (never delivered) | both pin moves | high |
| #1756 | LEAVE OPEN + CORRECT | the owner | `/design-sync`; optionally spike 6 at 200% | medium |
| #480 | LEAVE OPEN | watch list, nobody named | a registry release, then a Mac-and-Windows qualification | medium |
| #940 | LEAVE OPEN + CORRECT | plan 43 22b | the mac host half | high |
| #998 | LEAVE OPEN + CORRECT | watch list; plan 44 WB-32 (not run) | an upstream tag, then the bump | medium |
| #746 | LEAVE OPEN | the owner | the human AT checklist sign-off | medium |
| #747 | LEAVE OPEN | the owner | the human acceptance residual, JAWS | medium |
| #750 | LEAVE OPEN + CORRECT | the owner, then W8-6 | the human cells; the W8-6 hand-off | medium |

Totals: CLOSE 0; LEAVE OPEN 16; LEAVE OPEN + CORRECT 10.

## Owner decisions that would turn a LEAVE OPEN into a CLOSE

None of these is mine to make; each is a row the plan's decision table could carry.

1. **#1565.** Run the ruleset `PUT` from #1639 (the evidence above is the week's watch). Then the issue closes and `AGENTS.md:126` and `:132` drop their caveat.
2. **#940.** The race the issue reports is shut on all three platforms (22a, 22W). 22b is an audit and one host fact that cannot pass before #1513. The owner could end #940 by hand now and let #1513 carry that fact; the closure matrix already says "by hand".
3. **#1323.** Its only open item is one mac fact with no issue of its own. Either it stays as that fact's carrier (then it wants a new title, which is the owner's edit), or the fact moves onto plan 43 PR 28's row and the issue ends.
4. **#1389.** Choose between plan 43 PR 52 and #1651 for the mac half. If #1651, PR 52 is retired and #1389 ends with #1651's merge, alongside #1526.

## Undelivered chunks in plans 44–46 (by `git log` at 1ec9bf31)

**Plan 44** — 34 of 36 chunks have merged. Not delivered:

| Chunk | State | Where plan 47 meets it |
|---|---|---|
| WB-30 (Trash verification T2; `Refs #1468`) | Not started. Blocked: plan 43 PR 34's identity primitive (#1469) is not on `main`, and PR 34 is in the paused mac queue behind 22c. Plan 44 §3.1 left its start "decided when W10 is reached"; no decision is recorded. | Plan 44 §6 orders `vault/fs.rs`, `windows_entry.rs` and the trash files as WB-24 → WB-27 → WB-28 → WB-30. Plan 47's weak-volume chunk (#1727) and SQLite or core chunks that touch `fs.rs` land before WB-30 and WB-30 rebases. WB-30 also takes the next `a11y.rs` corpus slot after any plan 47 corpus change. |
| WB-32 (uniffi 0.32 spike; `Refs #998`) | Not run; no report PR. | Shares no file. A free-slot Windows spike. |

Delivered with a remainder (not undelivered chunks, but the plan should know):

- WB-7 merged with `Refs #1323` by design (see #1323).
- WB-11 merged without #1330, which its row said it closes.
- WB-21 and WB-21b: the mac half of #1389 and #1651.
- WB-27: the case-only rename on FAT32 and exFAT is #1727 (plan 47 Group B).
- WB-28 PR 1 (#1729) "not delivered": the gated FAT32/exFAT session fact on the VHDs and a real no-drive-letter volume fact; both need the owner's elevated session. No issue tracks them (not checked further).
- WB-29: "not delivered: a human screen-reader pass" (#1873).
- WB-31: the remedy was dropped by the owner; the macOS measurement is #1653.

**Plan 45** — 15 of 16 chunks have merged. Not delivered:

| Chunk | State | Where plan 47 meets it |
|---|---|---|
| XB-6 (setup-python and the runner pin; #1596) | No PR. Unblocked since 2026-10-29. | `ci/windows-runner/golden/versions.json` is shared with #1614 and #1903 in plan 47's CI chunk; two of the eleven workflow lines are in `windows.yml`, which plan 47's CI chunk may also edit. |

Owner steps still outstanding on delivered chunks: XB-12 (#1564, four steps), XB-13 (#1565, the ruleset).

**Plan 46** — all twenty chunks have merged, in every wave: DS-0 #1821, DS-1 #1822, DS-2 #1823, DS-3 #1824, DS-4 #1825, DS-5 #1827, DS-9 #1828, DS-6 #1829, DS-7 #1831, DS-8 #1833, DS-10 #1840, DS-11 #1841, DS-12 #1844, DS-14 #1847, DS-15 #1850, DS-13 #1852, DS-16 #1853, DS-17 #1856, DS-18 #1857, DS-19 #1858. The per-chunk issues #1802 to #1820 are closed. Outstanding: the owner's `/design-sync` (#1756). Nothing of plan 46 is left for plan 47 to sequence around.

**Plan 43 §11.4** (for reference, since eight Group C issues wait on it): no PR from 26 onward has merged. Group C depends on PRs 22b, 22c, 28, 29, 32, 34, 35, 38, 39, 41 and 52.

## What looks wrong in the brief's Group C list

1. **Group C produces no closes.** The brief's goal 3 expects some. The plan should say so plainly: the first chunk posts 26 status comments (27 with #1653), and three issues close only after an owner action.
2. **#748 and #749 are mislabelled.** The brief's §2 calls them "Milestone W's own Wave 8 epics (#748, #749, #751–#756)". They are W7-2 and W7-3: delivered (#1229 df52b6c0; #1228), open for their human AT rows only (`parity_matrix.md:292-293`, "implemented; CI + human AT pending"), exactly like #747 and #750. Plan 44 §1 groups "#746–#750" as human-AT residuals and "#751–#756" as Wave 8. Either add #748 and #749 to Group C's Milestone W line or correct the non-goal's wording. I did not audit them beyond that (#748 has no comments; #749's last comment, 2026-09-18, states its residual).
3. **Five Group C issues are not owned by plans 44–46.** #1370, #1395, #1398, #1469 and #1513 are plan 43's (PRs 32, 39, 35, 34, 22c); plan 44 only names them as out. The heading "owned by plans 44–46 or their watch lists" does not cover them. The audit result is the same, but their comments must name plan 43, and the plan should say plan 43's queue is paused.
4. **#1395 fits the brief's own §6.** It is a core wording change both hosts speak, deliverable and testable on Windows, with no owner decision and no FFI change: the same shape as #1628, #1629 and #1660, which the brief takes in with a "VoiceOver passes owed" ledger line. It sits in Group C only because plan 44 parked it with plan 43's PR 39. The owner should say whether plan 47 takes it.
5. **#1330 has no owner.** It is an open Windows defect whose owning chunk merged without it. Goal 2 ("leave nothing unowned") needs it to have a chunk here, a decision row, or a milestone. It is Windows-deliverable (Narrator on this machine).
6. **#1596's chunk never ran.** XB-6 is a small, unblocked CI chunk on files plan 47's CI chunk already touches. The non-goal against re-planning plan 44–46 chunks forbids taking it without the owner's word; leaving it means a rebase and an issue that stays open for no reason.
7. **Nothing is missing from Group C among unmilestoned issues.** I listed all 214 open issues and checked every unmilestoned or Milestone W issue (146) outside groups A, B, C and the brief's named outs: 49 remain, 48 of them mac-only by title, label or plan 43 row, and #642 (the l10n umbrella, "no action" in plan 44 §1). The mac ones include follow-ups that plans 44 and 45 filed and that no plan 43 row owns: #1651, #1652, #1653, #1654, #1662, #1679, #1720 (zero mentions in `43_mac_backlog_delivery/00_program.md`). They are out of plan 47's scope, but two Group C closes hang on them (#1526 on #1651, #1484 on #1653).
8. **Watch lists name no one.** #480, #998 and #1493 say "weekly" or "monthly" and no person or automation does the check (Dependabot is paused and ignores notify and uniffi anyway). The plan's watch list should name who reads the three release lists and when.

## Stale text seen on the way (not Group C; for the plan's corrections section)

- `docs/plans/45_windows_followups_plan.md:3-6`: "Not adopted; nothing in this plan has started." Fifteen of its sixteen chunks have merged.
- `docs/plans/46_windows_design_system_plan.md:3-5`: "Later waves wait for the owner." All five waves have merged. Its §2.4 says plan 44's WB-29 is still open; WB-29 merged as #1873.
- `docs/plans/43_mac_backlog_delivery/22_descriptor_relative_session_design.md:6-7`: says 22W is not delivered; it is (#1729, #1731).
- `docs/plans/44_windows_backlog_plan.md` closure matrix, row #1484, and #1653's body: both speak of measuring "the remedy"; the remedy was dropped.
- #940's milestone was never set (RD15), and should not be now.
