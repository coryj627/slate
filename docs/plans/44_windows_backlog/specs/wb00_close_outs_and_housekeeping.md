# WB-0 — Close-outs and housekeeping: #1320 and #1261 close on the facts they lacked, the zip exception is renewed once, the three mac follow-ups exist, and plan 43 §11.4 points here

Issues: #1320 #1261 (Refs #1457). Size S. Lanes: windows, rust (the `audit.yml` gate re-reads `.cargo/audit.toml`). AT: —. Wave: W0. Depends on: nothing. Parallel with: nothing (W0 has one slot). Decision: WD1 (pending; recommended "yes, this plan owns the §2.3 rows and plan 43 §11.4 points here"; under "no": Design 5 is dropped, the rows stay plan 43's and this chunk delivers only items 1–4) and WD18 (pending; recommended "renew once to 2026-12-17"; under "admit the Git patch now": Design 3 becomes a `[patch.crates-io]` admission of MathCAT `main` 793fcb77 in `Cargo.toml` with CI verifying the revision, modelled on the citationberg entry, and the chunk is no longer S).

## Goal

Two issues that `main` already fixed close with the evidence each asked for and a comment naming the fixing merge; the `cargo audit` gate keeps passing past 2026-11-17 under a dated, reasoned renewal instead of failing on a review-by date nobody re-examined; the three mac follow-ups this plan promises exist as issues before the chunks that reference them start; and plan 43 §11.4 says, row by row, which of its PRs this plan now owns. The invariant restored is the one both plans share: every issue closes in exactly one place, with a fact, and no register says something the tree no longer does.

## Current state on main

Where the plan and the code disagree, the code wins; both disagreements are noted inline.

- **#1320.** `apps/slate-windows/src/SlateWindows/Reading/HeadingStyleText.cs`: the attribute walk is over the FlowDocument's own blocks — `ParagraphFrom` drives the loop at `:288-290` and is defined at `:378`; the `TextRangeAdaptor` survives only in `ClampToQuery` (`:342`) and `ParagraphAt` (`:361`); `:19` documents why the adaptor is slow. `apps/slate-windows/tests/SlateWindows.Tests/ReadingViewTests.cs:730` `HugeDocumentSyntheticQueriesHaveBoundedCost` names #1320 (`:725`), runs the three whole-document queries over 10,051 paragraphs and asserts `allocated < 16 * 1024 * 1024` (`:770`); it records the time but, by design (`:727-729`), imposes no time bound. `:681` `SyntheticAttributeWalkSurvivesHugeDocuments` still runs under the default 60 s STA bound (`:4244`). Eleven facts in the file have `Attribute`, `Style` or `Synthetic` in their names (the attribute corpus). *Issue vs code:* the issue asked for a time budget ("under 2 s locally"); `main` chose the allocation bound and a recorded time. This chunk does not add a time gate (that is WB-7's benchmark-tagging question, not a close-out).
- **#1261.** `apps/slate-windows/src/SlateWindows/Canvas/CanvasRendererView.cs:36` subscribes `StateInstalled`; `OnStateInstalled` (`:146-162`) calls `RefreshAutomationChildren()` at `:153`; `RefreshAutomationChildren` (`:180-196`) rebuilds the child cache through `WpfEditorPeerConnection.InvalidateChildren` and raises `RaiseChildrenInvalidated()` only when the list changed; a visibility change refreshes too (`:128-135`). `apps/slate-windows/tests/SlateWindows.Tests/CanvasDocumentTests.cs:5826-5858` `TheBoardAnnouncesAChangedChildListOnceAndAnUnchangedOneNever` pins the invalidated-event count across two pans and a collapse. Missing: the issue's own evidence shape — the child list read *before* layout lists every card *after* it, with no `ResetChildrenCache`. The journey workaround is still there: `apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs:9746-9752` reads the marks from the outline "because the board's peer tree is not refreshed after an install" — a reason that is now false.
- **#1457.** `.cargo/audit.toml:33` `# review-by: 2026-11-17`; the reason paragraph is `:26-32`; the three ignores are `:38-42`; `:17` says one exception is active. `.github/workflows/audit.yml:63-96` fails the gate once any `review-by:` date has passed (`:88`). MathCAT `main` 793fcb77 (2026-10-18) is on zip 9; no crates.io release carries it (plan §2.2).
- **Plan 43 §11.4** (`docs/plans/43_mac_backlog_delivery/00_program.md`): the rows §2.3 moves are at `:441` (21b), `:443` (31), `:439` (22c), `:449` (37), `:456` (44), `:457` (45), `:458` (46), `:459` (47), `:464` (52), `:437` (29), `:453` (41), `:450` (38); 22W is named in the §5 row at `:183` and in §11.3 at `:396`. None points at plan 44 today (`grep -n "plan 44\|44_windows" 00_program.md` is empty).
- **Mac follow-ups:** none of the three exists (check `gh issue list --search "<title words>"` before filing; the plan names them only by description).

## Reference

No mac twin. The behavioural specs are the issues' own "Facts wanted" sections (#1320) and "Expected" paragraph (#1261); WD18's text for the renewal; plan §2.3's table for the pointers; plan §5's closure matrix and WB-21b / WB-25 / WB-31 for the follow-up bodies.

## Design

1. **#1320 close-out.** No product change. Run the attribute corpus and the two huge-document facts locally (commands below), paste the `measurement` line `HugeDocumentSyntheticQueriesHaveBoundedCost` writes (`:764-767`, "10,051 paragraphs, three queries: N ms; N allocated bytes") into the closing comment, cite c247b865 (#1328) and the four line anchors above, and say the time bound was deliberately not added. Close.
2. **#1261 close-out.** One hosted fact (below) that reads the board's children before layout and after it, with no `ResetChildrenCache`, and asserts the after-list names every card the state materialized. Rewrite the comment at `ShellAccessibilityTests.cs:9746-9752`: the outline read stays (it is the shorter, already-correct route), the reason becomes "R-12's marks are the outline's own status column; the board's children are refreshed per install (#1261, c247b865)". Close with a comment pointing at c247b865 and the new fact.
3. **WD18.** Edit `.cargo/audit.toml:33` to `# review-by: 2026-12-17` and append one sentence to `:26-32`: "Renewed once on <date> (WD18, plan 44): MathCAT `main` 793fcb77 moved to zip 9 on 2026-10-18 with no release yet; if none exists by 2026-11-30, admit a Git patch to MathCAT `main` under the citationberg admission pattern (`docs/runbooks/citation-dependency-policy.md`)." Comment #1457 with the same sentence; the issue stays open (`Refs #1457`).
4. **The three mac follow-ups**, filed with `mac:` titles and `Refs` to the chunk that creates the need:
   - "mac: consume `code_block_preamble_with_verbosity`; Settings samples through core; VoiceOver pass per mode" — body: WB-21b ships the sibling FFI function (WD13); mac's menu modes and its Settings sample strings move onto it; the issue closes #1526 after the VoiceOver pass (closure matrix row #1526).
   - "mac: migrate off `switcher_rank_top` and `tag_tree` to the indexed and paged siblings; retire the old entry points" — body: WB-25 adds `switcher_rank_top_indexed` and the paged, cancellable tag tree and keeps the old entry points for mac (WD17); when mac is off them both are deleted.
   - "mac: measure WB-31's two-session remedy on macOS" — body: WD21; #1484's numbers are macOS (M5 Max, 173 ms vs 96 ms); #1484 closes only after this measurement or the owner's withdrawal of the observation.
5. **Plan 43 §11.4 pointers (WD1).** In each row named above, append to the *Wave* cell: `→ plan 44 WB-NN (WD1)` with the chunk from §2.3 (21b→WB-10; 22c→WB-17 for #1510's sentence, the code stays; 37→WB-18 for #1481, #1479 stays; 44→WB-18; 45→WB-19; 46→WB-17 for #1390's text, #1393 #1388 stay; 47→WB-17; 52→WB-21 for #1389's Windows half; 29→WB-29 for the Windows half after the design; 41→WB-30 for the outcome contract and Windows back end; 38→WB-27 for the Windows proof). Row `:443` (PR 31) becomes: `| 31 | Retired on plan 44's adoption (WD1): #1519 is core-only and is WB-10a | #1519 | — | — | — |`. Add one sentence after `:396`: "22W and #1475's identity half are plan 44's WB-28." Nothing else in plan 43 is edited; its frozen records stay.

Deliberately not changed: `HeadingStyleText.cs`, `CanvasRendererView.cs`, the outline-based mark reading, the three ignore IDs.

## Tests

- `CanvasDocumentTests.TheBoardsChildListReadBeforeLayoutNamesEveryCardAfterItWithoutAReset` (hosted, `RunSta`, beside `:5826`): `WriteRowCanvas(9)`, show the visual surface, `ConnectBoardPeer(renderer)` and read `board.GetChildren()` *before* `host.UpdateLayout()` (assert it is empty or a strict subset), pump until settled, read again without calling `ResetChildrenCache`, and assert the names equal the nine cards' names in reading order and `ChildrenInvalidatedForTests` rose by exactly one. Mutation: comment out `RefreshAutomationChildren()` at `CanvasRendererView.cs:153` — the second read stays stale and the fact fails.
- `ReadingViewTests` attribute corpus and `HugeDocumentSyntheticQueriesHaveBoundedCost`: run, not changed; the measurement line goes into the comment.
- No FlaUI journey changes beyond the comment; no census rows.

## Docs and registers

- `.cargo/audit.toml` (`:26-33`) as in Design 3.
- `docs/plans/43_mac_backlog_delivery/00_program.md` rows as in Design 5; no other register.
- `docs/plans/44_windows_backlog_plan.md` closure-matrix rows for #1526 and #1484 gain the follow-up issue numbers.
- Issue comments on #1320, #1261, #1457; the three new issues.
- No `w_c_matrix.md` / `parity_matrix.md` / designations / ledger changes.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb0'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$corpus = 'FullyQualifiedName~ReadingViewTests&(FullyQualifiedName~Attribute|FullyQualifiedName~Style|FullyQualifiedName~Synthetic)'
$board  = 'FullyQualifiedName~CanvasDocumentTests.TheBoard'
foreach ($f in $corpus, $board) {
  $n = (dotnet test $tests -c Release --no-build --list-tests --filter $f | Select-String '^\s{4}\S').Count
  if ($n -lt 1) { throw "filter selects nothing: $f" }   # the corpus selects 11+2, the board filter 2
  dotnet test $tests -c Release --no-build --filter $f
}
$changed = @(git diff --name-only HEAD; git ls-files --others --exclude-standard) | Where-Object { $_ -like '*.cs' }
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $changed
cargo audit   # with the renewed date; the gate's own check is audit.yml:63-96
```

CI lanes: `windows.yml` (always), `audit.yml` (the toml edit). AT pass: none; no speech changes.

## Acceptance

- [ ] `TheBoardsChildListReadBeforeLayoutNamesEveryCardAfterItWithoutAReset` fails with `:153` removed, passes on `main`'s code.
- [ ] `ShellAccessibilityTests.cs:9746-9752` comment no longer claims the board is not refreshed.
- [ ] #1320 closed with the measurement line and c247b865; #1261 closed with the fact's name and c247b865 (`Closes #1320`, `Closes #1261` in the PR body).
- [ ] `.cargo/audit.toml` review-by is 2026-12-17 with the WD18 sentence; `Refs #1457` and a comment on it.
- [ ] The three mac follow-ups exist; their numbers are written into the PR body and into plan 44's closure matrix rows for #1526 and #1484.
- [ ] Plan 43 §11.4 rows `:437 :439 :441 :443 :449 :450 :453 :456-459 :464` carry the pointer; `:443` is the retired form; the sentence after `:396` exists.
- [ ] Not delivered: nothing (if WD18 is answered "admit the patch", say so and open the admission as its own S PR).

## Brief

Read `apps/slate-windows/src/SlateWindows/Reading/HeadingStyleText.cs:280-380`, `apps/slate-windows/tests/SlateWindows.Tests/ReadingViewTests.cs:675-775`, `apps/slate-windows/src/SlateWindows/Canvas/CanvasRendererView.cs:128-200`, `apps/slate-windows/tests/SlateWindows.Tests/CanvasDocumentTests.cs:5815-5860`, `apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs:9740-9760`, `.cargo/audit.toml:17-42`, and `docs/plans/43_mac_backlog_delivery/00_program.md:423-470` and `:396`. Build: (1) one hosted fact `CanvasDocumentTests.TheBoardsChildListReadBeforeLayoutNamesEveryCardAfterItWithoutAReset` — a nine-card row canvas's board peer, read through `board.GetChildren()` before layout and again after the engine settles with no `ResetChildrenCache`, names all nine cards in reading order and raised `ChildrenInvalidatedForTests` exactly once (mutation: delete `RefreshAutomationChildren()` at `CanvasRendererView.cs:153`, the fact fails); (2) the comment at `ShellAccessibilityTests.cs:9746-9752` reworded to "R-12's marks are the outline's own status column; the board's children are refreshed per install (#1261, c247b865)"; (3) `.cargo/audit.toml:33` → `# review-by: 2026-12-17` plus the renewal sentence naming MathCAT 793fcb77 and the 2026-11-30 Git-patch fallback (decision WD18, pending: if the owner instead says "admit the patch now", do a `[patch.crates-io]` admission of 793fcb77 in `Cargo.toml` with CI revision verification and stop here); (4) three `mac:` issues — consume `code_block_preamble_with_verbosity` (closes #1526 after a VoiceOver pass), migrate off `switcher_rank_top`/`tag_tree` (WD17), measure WB-31's remedy on macOS (WD21) — and their numbers written into `docs/plans/44_windows_backlog_plan.md`'s closure-matrix rows for #1526 and #1484; (5) decision WD1, pending: if "yes", append `→ plan 44 WB-NN (WD1)` to the Wave cell of `00_program.md` rows `:437 :439 :441 :449 :450 :453 :456 :457 :458 :459 :464`, replace row `:443` with the retired PR 31 text, and add "22W and #1475's identity half are plan 44's WB-28." after `:396`; if "no", skip (5). Run the `ReadingViewTests` attribute/style/synthetic corpus and paste `HugeDocumentSyntheticQueriesHaveBoundedCost`'s measurement line into #1320's closing comment. Registers: none beyond the toml and plan rows. Done when the new fact passes and failed under its mutation, `dotnet format --verify-no-changes` is clean on the changed `.cs` files, `windows.yml` and `audit.yml` are green, #1320 and #1261 are closed (`Closes #1320`, `Closes #1261`; `Refs #1457`), and nothing is "not delivered". Commit: `chore(plans): #1320 and #1261 close on their verification facts, the zip exception is renewed to 2026-12-17, and plan 43 §11.4 points at plan 44`.
