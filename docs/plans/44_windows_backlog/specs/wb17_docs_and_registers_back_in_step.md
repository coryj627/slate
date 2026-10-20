# WB-17 — The Windows registers say what the tree does, and CI checks the two generated tables

Issues: #1384 #1381 #1390 #1376 #1386 #1510. Size S. Lanes: windows (the unit lane's `--check` steps), rust (a doc comment in `slate-uniffi`; `rust.yml`). AT: — (no speech changes; `chords.json` reasons and designation reasons are not spoken). Wave: W6, slot 2. Depends on: plan 43 PRs 7 (#1113), 6 (#1105), 8 (#1286), 20b/20c (#1507/#1267) merged — all are (the issues were filed on their merges). Parallel with: WB-16, WB-18 (no shared files; §6 keeps WB-3 and WB-17 apart on contract 38's ledger — WB-3 is W1, so this is satisfied). Decision: none.

## Goal

The chord table's two divergence reasons say the divergences are retired and cite their contracts; `chords.json` is the table's projection again; the Windows tree's two citations of a removed mac announcement point at contract 29 S8; contract 38's `EmbedPreviewShown` designation and its ledger row record that AR-23 closed on mac; contracts 34 and 35 carry the tables `canvas_reconciliation.py` and `graph_reconciliation.py` would regenerate today, and the Windows lane runs both `--check`s so they cannot drift again; and the `on_external_change` FFI doc says what both hosts do.

## Current state on main

- **#1384 / #1381, `apps/slate-windows/src/SlateWindows/Commands/ChordTable.cs`.** `SearchOverlayArrowDivergence` `:1678-1680` ("SD-1: Windows navigates results with the arrow keys where mac has no arrow handling at all; mac converges under #1113."), used at `:1837` and `:1840`; `PaletteKeysDivergence` `:1681-1683` ("PD-1: … mac deliberately handles none of them; mac converges under #1105."), used at `:1821`, `:1823`, `:1825`, `:1827`. Projected into `apps/slate-windows/chords.json` `reason` fields at `:4139`, `:4152`, `:4165`, `:4178` (palette) and `:4217`, `:4230` (search). `tests/SlateWindows.Tests/ChordTableTests.cs:234` `ChordsJson_IsExactlyTheTablesProjection` pins the file; `SLATE_CHORDS_UPDATE=1` regenerates (`:27`, `:257-266`). Stale comments: `MainWindow.Palette.cs:254-256`, `CommandPaletteViewModel.cs:446`, `:721`, `:730`, `:741`, `tests/SlateWindows.Tests/CommandPaletteTests.cs:334`, `CommandPaletteTests.View.cs:207`. Both #1113 and #1105 are closed; contract 29 SD-1 is retired at `docs/plans/29_search_overlay_contracts.md:407-420` (mac moves focus, no wrap, Keyboard navigation per #1377); contract 28 PD-1 is retired by #1105.
- **#1384's comment, the S8 citation.** `tests/SlateWindows.Tests/SearchOverlayViewModelTests.cs:1054` and `src/SlateWindows/Search/SearchOverlayViewModel.cs:217` cite `SearchOverlay.swift:537-552` (the on-focus announcement #1412 removed). Contract 29 S8 is `29_search_overlay_contracts.md:168-186`; its #1412 note is `:184-186` ("Windows' `RowSelected` stays basename-only").
- **#1390.** `docs/plans/18_windows_port/a11y_trigger_designations.json:267` (`EmbedPreviewShown.reason`) says in the present tense that mac's `EmbedView` composes its labels, "accepted as AR-23 (contract 40), mac follow-up #1286". `docs/plans/38_notification_dispatcher_contracts.md:825` repeats it (generated). `scripts/a11y_trigger_ledger.py:40-50` regenerates by running `dotnet test … A11yTriggerParityCensus.SourceInventoryCanBeExportedForTheLedgerGenerator` (`--write`, `--check`: `:11-12`); `A11yTriggerParityCensus` (`tests/…/Censuses/A11yTriggerParityCensus.cs`) requires the ledger row to carry the JSON reason verbatim. Contract 40 AR-23 (`40_nvda_matrix_remediation_contracts.md:107`) already carries "**Closed on mac, 2026-10-16 (#1286, plan 43 PR 8)**" and names #1388 for the query-block cards.
- **#1376 / #1386.** `scripts/canvas_reconciliation.py --check` and `scripts/graph_reconciliation.py --check` exit 1 on this tree without the binding and would still differ with it (the (c) line pointers moved). `declared_names()` (`canvas_reconciliation.py:162-178`) reads every `.cs` under the shell including `src/SlateUniffi/generated/slate_uniffi.cs`, which is generated at build and not tracked (`git ls-files` has no `slate_uniffi.cs`; `generate-bindings.ps1` makes it). `CLOSED` maps: `canvas_reconciliation.py:290-294` (CD-30, CD-36, CD-38), `graph_reconciliation.py:269-275` (B-D11, C-D7, C-D9, C-D17, D-D12) — the hand annotations the issues feared losing are already generator-owned. `register_index` (`canvas:297-309`) writes `Recorded divergences, line N` from the live document. `--write`/`--check` in `main` (`canvas:371-392`). The censuses verify (b) and only count (c): `CanvasReconciliationCensus.TheRegistersDecisionsAndIssuesArePinned` (`:126-144`, counts 48 CD / 5 CR / 7 D rows), `GraphReconciliationCensus` (`:142`). Nothing in `windows.yml` runs either `--check`; `generate-parity-matrix.py --check` runs at `:805` in the `windows-model` aggregate job, which is `runs-on: ubuntu-latest` (`:787`) — no binding there, so the new steps cannot go beside it. The app lane (`windows`, `:396-570`) generates bindings at `:520-521`, builds `:523-524`, tests `:527-534`, format-gates `:564-565`.
- **#1510.** `crates/slate-uniffi/src/lib.rs:5531-5534`: "Answer with a Watcher-reason rescan (PRs 20b and 20c wire it; until then a no-op)." Both hosts are wired: Windows `UiVaultEventListener.OnExternalChange` at `apps/slate-windows/src/SlateWindows/VaultLifecycleViewModel.cs:2111` (`:956` the teardown note), mac `VaultEventAdapter.onExternalChange()` at `apps/slate-mac/Sources/SlateMac/AppState+History.swift:128` (class at `:82`). Neither generated binding is tracked, so the doc change regenerates nothing checked in.
- Plan 43 claims: PR 46 lists #1390, 22c lists #1510, PR 47 lists #1376 #1386; plan 44 §2.3 moves all four here (WD1; WB-0 writes the pointers).

## Reference

Contract 29 SD-1 (`:407-420`, the retirement paragraph) and S8 (`:168-186`); contract 28 PD-1's retirement by #1105 (`grep -n "PD-1" docs/plans/28_palette_contracts.md`); contract 40 AR-23 (`:107`); the generators' own doctrine (`canvas_reconciliation.py:29-36`: "`--check` is what CanvasReconciliationCensus mirrors"). No behaviour changes anywhere.

## Design

1. **ChordTable.cs** `:1678-1683`: `SearchOverlayArrowDivergence` = "SD-1 (contract 29, retired by #1113): both hosts' arrows walk the results. Mac moves keyboard focus where Windows moves a selection, so mac's list does not wrap, and mac's arrows need Keyboard navigation on (#1377)."; `PaletteKeysDivergence` = "PD-1 (contract 28, retired by #1105): both hosts handle Home, End, Page Up and Page Down by the Windows rules." Constant names unchanged (the rows at `:1821-1840` reference them). Regenerate: `$env:SLATE_CHORDS_UPDATE='1'; dotnet test … --filter ChordsJson_IsExactlyTheTablesProjection`, then unset and re-run the pin. Reword the seven comments to "PD-1 was retired by #1105; both hosts handle these keys" (no behaviour change).
2. **S8 citation**: `SearchOverlayViewModelTests.cs:1054` and `SearchOverlayViewModel.cs:217` → "Basename only (contract 29 S8; mac's on-focus announcement was removed by #1412, VoiceOver's read of the focused row is its voice)".
3. **Designation** `a11y_trigger_designations.json:267`: keep the clause and append #1390's suggested sentence: "closed on mac by #1286 (plan 43 PR 8): the labels are core's `resolved_embed_title`; the query-block base cards remain host-composed (#1388)". Then `python scripts/a11y_trigger_ledger.py --write` on this tree (bindings generated, Release build present) → `38:825` carries the new reason verbatim; `--check` clean.
4. **Reconciliation tables**: with the binding generated, `python -I scripts/canvas_reconciliation.py --write` and `python -I scripts/graph_reconciliation.py --write`; commit the regenerated blocks in `docs/plans/34_canvas_contracts.md` and `35_graph_contracts.md` (the generated region between the scripts' `START`/`END` markers only; the owner-recorded records above them are untouched). Check the `CLOSED` entries render. Then **`windows.yml`**: two steps in the `windows` job after `Build` (`:523-524`) and before `Test`:
   ```yaml
         - name: Verify contract 34's reconciliation tables are the generator's output
           run: python scripts/canvas_reconciliation.py --check
         - name: Verify contract 35's reconciliation tables are the generator's output
           run: python scripts/graph_reconciliation.py --check
   ```
   (`python`, not `python3`, on the Windows runners; `:226` uses `python3` on ubuntu. Confirm the image's launcher with `python --version` in the step if in doubt.) Not in the `windows-model` aggregate (`:787`, ubuntu, no binding).
5. **lib.rs** `:5531-5534`: "Plan 43 PR 20a (#1267): files changed outside this session — one call per coalesced burst, from the watcher's thread, no session lock held, no paths. Both hosts answer with a Watcher-reason rescan: mac through `VaultEventAdapter.onExternalChange` (PR 20c), Windows through `UiVaultEventListener.OnExternalChange` (PR 20b, #1507)." `cargo doc`-clean; the regenerated bindings are build products.
6. Deliberately not changed: any chord, any key handling, the designation's `status`, the (a) tables' sources, the censuses' row counts (unless the regeneration changes a count — then the pin at `CanvasReconciliationCensus.cs:129-131` / the graph twin is updated with the reason in the PR body), plan 43's own records.

## Tests

No new product facts (nothing behavioural). The pins that must pass, and what fails if the chunk is incomplete:
- `ChordTableTests.ChordsJson_IsExactlyTheTablesProjection` — fails until `chords.json` is regenerated (the first failing fact: edit the constants first, watch it fail, regenerate).
- `ChordTableTests.EveryRow_EitherFollowsTheModifierRule_OrRecordsADivergence` (`:144`) — the reasons still record a divergence (keep the `SD-1`/`PD-1` tokens at the front of the strings if the fact keys on them; read `:144-215` before wording).
- `A11yTriggerParityCensus.EveryBindingKeyAndEveryConstructionIsCoveredInBothDirections` and the verbatim-reason rule — fail until the ledger is regenerated.
- `CanvasReconciliationCensus.TheEvidenceTableCarriesOneRowPerKey`, `TheRegistersDecisionsAndIssuesArePinned`; `GraphReconciliationCensus` twins — pass before and after; the new CI steps are the real witness for (c).
- `scripts/tests/`: add `test_reconciliation_check_flags_a_stale_pointer.py` (new, beside `test_windows_docs_trigger.py`): a fixture doc with one record moved by a line makes `--check` exit 1 and `--write` fix it (the generators' `main` is importable). Mutation: make `register_index` emit a constant line.
- No FlaUI journey; no census rows added.

## Docs and registers

- `apps/slate-windows/chords.json` regenerated (six `reason` fields).
- `docs/plans/18_windows_port/a11y_trigger_designations.json:267`; `docs/plans/38_notification_dispatcher_contracts.md` ledger regenerated (`:825` row).
- `docs/plans/34_canvas_contracts.md` and `35_graph_contracts.md` generated blocks regenerated.
- `.github/workflows/windows.yml` two steps.
- `crates/slate-uniffi/src/lib.rs` doc comment.
- Nothing in `parity_matrix.md` (`generate-parity-matrix.py --check` must still pass; the chord reasons are not in it — verify by running it) or `w_c_matrix.md`.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb17'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$env:SLATE_CHORDS_UPDATE = '1'
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~ChordTableTests.ChordsJson_IsExactlyTheTablesProjection"
Remove-Item Env:SLATE_CHORDS_UPDATE
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~ChordTableTests|FullyQualifiedName~A11yTriggerParityCensus|FullyQualifiedName~CanvasReconciliationCensus|FullyQualifiedName~GraphReconciliationCensus|FullyQualifiedName~CommandDriftTests"
python -I scripts/a11y_trigger_ledger.py --write; python -I scripts/a11y_trigger_ledger.py --check
python -I scripts/canvas_reconciliation.py --write; python -I scripts/canvas_reconciliation.py --check
python -I scripts/graph_reconciliation.py --write;  python -I scripts/graph_reconciliation.py --check
python -I scripts/generate-parity-matrix.py --check
python -I -m unittest discover -s scripts/tests -p "test_reconciliation*.py"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/src/SlateWindows/Commands/ChordTable.cs apps/slate-windows/src/SlateWindows/MainWindow.Palette.cs apps/slate-windows/src/SlateWindows/CommandPaletteViewModel.cs apps/slate-windows/src/SlateWindows/Search/SearchOverlayViewModel.cs apps/slate-windows/tests/SlateWindows.Tests/CommandPaletteTests.cs apps/slate-windows/tests/SlateWindows.Tests/CommandPaletteTests.View.cs apps/slate-windows/tests/SlateWindows.Tests/SearchOverlayViewModelTests.cs
cargo doc -p slate-uniffi --no-deps; make ci
```

CI lanes: `windows.yml` (with the two new steps green on the PR itself), `rust.yml` (the lib.rs comment; fmt/clippy unaffected). AT pass: none.

## Acceptance

- [ ] Both constants reworded; `chords.json` regenerated and its pin passes; the seven comments and the two S8 citations updated.
- [ ] Designation `:267` appended; ledger `--check` clean; `38:825` carries the sentence.
- [ ] Contracts 34 and 35 regenerated; both `--check`s clean locally and as `windows.yml` steps that ran green on the PR.
- [ ] `lib.rs:5531-5534` names both hosts' adapters.
- [ ] `scripts/tests/test_reconciliation_check_flags_a_stale_pointer.py` passes and fails under its mutation.
- [ ] `Closes #1384`, `Closes #1381`, `Closes #1390`, `Closes #1376`, `Closes #1386`, `Closes #1510` (all six close here per §2.3; plan 43's PRs 46, 22c and 47 keep their code and lose these numbers — WB-0's pointers say so).
- [ ] Not delivered: nothing. If a census row count changes on regeneration, the updated pin and the reason are in the PR body.

## Brief

Read `ChordTable.cs:1660-1690` and `:1815-1842`, `ChordTableTests.cs:20-40`, `:144-215`, `:234-270`, the seven stale comment sites, `SearchOverlayViewModelTests.cs:1050-1058`, `SearchOverlayViewModel.cs:214-220`, `a11y_trigger_designations.json:260-270`, `scripts/a11y_trigger_ledger.py:1-50`, `scripts/canvas_reconciliation.py:160-180`, `:284-312`, `:368-395`, `scripts/graph_reconciliation.py:265-282`, `windows.yml:396-570` and `:784-806`, `lib.rs:5528-5536`, contract 29 `:168-186` and `:407-420`, contract 40 `:107`. Reword the two constants, regenerate `chords.json`, fix the comments and citations, append the designation and regenerate the ledger, regenerate contracts 34 and 35 and add the two `--check` steps to the `windows` job, reword the FFI doc, add the script test. Done when every pin passes, all four `--check`s are clean locally and in the lane, and the six issues close. Commit: `docs(windows): the chord reasons, the S8 citations, contract 38's AR-23 row, contracts 34 and 35's tables and the on_external_change doc say what the tree does, and CI checks the two generated tables`.
