# CB-3 — The shell gate's binaries keep seven days, and the lane checks contract 38's ledger against its generator

Issues: #1647, #1699. Size S. Lanes: windows (the whole lane runs: `windows.yml` and `scripts/**` are in its path list), windows-runner-tests (the Pester facts). AT: —. Wave: C2, slot 1, first in the slot. Depends on: CB-0 (it edits `AGENTS.md` first) and CB-2 (it edits `windows.yml`'s model step first). Parallel with: CB-6, CB-8. Decision: none. Interlock: CB-5 follows in the same slot because both edit `Workflows.Tests.ps1`; it rebases over this merge and reruns the suite, never hand-merging. Review: on its tests alone (plan §4 rule 9).

## Goal

A rerun of only the shell gate finds its binaries for seven days, not one. The app job runs `python -I scripts/a11y_trigger_ledger.py --check` after the build, so a ledger row placed by hand fails the lane. The failure prints each differing row, so a contributor without a Windows build can copy the generator's rows from the log. Pester pins the retention, the three generated-table checks and the parity-matrix check. The docs-trigger census sees `python -I scripts/…`. `AGENTS.md` says the ledger is generated.

## Current state on main

Read at c2377210. CB-2 will have moved lines below `windows.yml:897`; re-read before coding.

- `.github/workflows/windows.yml:835-846`: `Upload FlaUI gate binaries`, `name: flaui-binaries-${{ github.run_id }}` (`:838`), `retention-days: 1` (`:846`). Its comment `:825-834` says "within the artifact's one-day retention" (`:832-833`). The `flaui` job downloads it at `:1126-1130`. The app results artifact keeps 7 (`:823`), as does the pilot's producer (`windows-ci-pilot.yml:249-252`). Every other `retention-days` in `windows.yml`, `nightly.yml` and the pilot is 7, 14 or 30.
- `windows.yml:679-680` `Build`; comment `:682-686`; `:687-690` `Set up Python (the reconciliation checks)`; `:691-692` contract 34's check; `:693-694` contract 35's; `:695-696` the stale-pointer unit test; `:763-777` `Test`, under `guard-device-state.ps1`. The model job's parity check is `:1096-1097`. No workflow names `a11y_trigger_ledger.py`.
- `scripts/a11y_trigger_ledger.py`: `inventory()` `:40-51` runs one `dotnet test` (`--no-restore --configuration Release`, one fact, no `--no-build`); `cell()` `:58-59` sorts sites by `Member@Ordinal`; `main()` `:116-141` reads `sys.argv` and on a stale ledger exits with one sentence and no rows (`:136`). `--inventory <file>` (`:121`) skips the `dotnet test`.
- The fact is `A11yTriggerParityCensus.cs:323-334`. The census compares sites as a set (`CoverageFailures`, `:42-50`, called on each cell at `:78`), so order is never checked. The ledger block is `docs/plans/38_notification_dispatcher_contracts.md:814-1175`, 238 rows.
- `ci/windows-runner/tests/Workflows.Tests.ps1`: `Describe 'windows.yml FlaUI handoff'` `:204-222` pins the name and `overwrite`, not the retention. Nothing pins the WB-17 checks or the parity check. Helpers: `Get-JobBlock` `:23`, `Get-Lines` `:44`, `Get-Steps` `:46`; the "only keys" pattern is at `:233-235`.
- `scripts/check_windows_docs_trigger.py:66`: `_SCRIPT_RUN` matches only `python3 scripts/…`, so `script_reads()` (`:212-220`) scans neither WB-17 generator. Both build their path from segments (`canvas_reconciliation.py:47`, `graph_reconciliation.py:49`), which `_SCRIPT_DOCS` (`:67`) cannot see either.
- `AGENTS.md:61-64` is the parity-matrix gotcha; CB-0 may have moved it.
- Not verified: that the committed ledger equals the generator's output on the real inventory. The reading matched it only against an inventory rebuilt from the table's own cells.

## Reference

#1647 and #1699 (no comments). Reading `02_ci_and_runner.md`: the two issue sections, cluster items 5 and 6, risks 1, 2, 7 and 8. WB-17 (#1700, 2822a0a4) is the model for the steps and the unit test. Plan 45 §4 rule 2.

## Design

1. **Retention.** `windows.yml:846` becomes `retention-days: 7`. The comment's last clause becomes: "within the artifact's seven days (#1647), the app results artifact's own; after that the gate's download fails with 'Artifact not found', and the way out is 'Re-run all jobs'".

2. **The check and its unit test, in the app job.** Widen the comment at `:682-686` to name contract 38 and #1699. After contract 35's step and before the stale-pointer test:

   ```yaml
         # #1699: the generator runs one fact of the Release unit build, so
         # the step is under the device-state guard, like Test (#1331). A
         # stale ledger prints the rows to copy into the document.
         - name: Verify contract 38's trigger ledger is the generator's output
           run: pwsh -NoProfile -File scripts/windows/guard-device-state.ps1 -- python -I scripts/a11y_trigger_ledger.py --check
   ```

   After the stale-pointer test:

   ```yaml
         - name: Test that the ledger check catches a reordered row
           run: python -I -m unittest discover -v -s scripts/tests -p test_a11y_trigger_ledger_check_flags_a_reordered_row.py
   ```

   No `if:`, no `continue-on-error:`. Nothing else in `windows.yml` changes.

3. **Risk 2 (a `dotnet test` outside the guard).** The step runs under the guard. If the one-fact run changes `%LOCALAPPDATA%\Slate`, the step fails and lists the files. The same fact already runs inside the guarded `Test` step, so this should pass; the guard makes it a checked claim. No `--no-build` is added to the script: a local `--write` over a stale build would write a stale ledger. The PR body quotes the step's duration, which is the cost of the up-to-date build pass. Accepted as they are: the run is before the WER arm step, so a crash there leaves no dump, and only the job's 60-minute timeout bounds a hang.

4. **The script prints the differing rows.** In `a11y_trigger_ledger.py`:
   - `main(argv: list[str] | None = None)` passes `argv` to `parse_args`. It still raises `SystemExit`.
   - At the start, `sys.stdout.reconfigure(encoding="utf-8")` when the stream has that method. `-I` ignores `PYTHONUTF8`, a Windows pipe defaults to the ANSI code page, and nearly every row holds an em dash.
   - On `--check` with `normalized != updated`: print `difflib.unified_diff(normalized.split("\n"), updated.split("\n"), fromfile=f"{DOCUMENT.name} (committed)", tofile=f"{DOCUMENT.name} (generator)", n=0, lineterm="")`, one line each. The diff is of the whole document, so each `@@` header gives the row's line number. Then raise `SystemExit` with: "A11y trigger ledger is stale. In docs/plans/38_notification_dispatcher_contracts.md, replace each '-' row printed above with the '+' row under it (the '@@' header gives the line), or regenerate with --write on a Windows Release build."
   - `render()` and its drift exits are unchanged.

5. **The docs-trigger census.** `_SCRIPT_RUN` becomes `python3?(?:\s+-I)?\s+(scripts/[A-Za-z0-9_./-]+\.py)`. Update the docstring bullet at `:26`.

6. **Found and fixed here (known before coding).** With item 5 alone the census opens the two reconciliation scripts and finds nothing, because they join their path from segments. `script_reads()` also applies the existing `_CHAIN` pattern (`:61`) to each script, literal segments only. Contracts 34 and 35 are already in the path list (`windows.yml:155-156`), so the census stays green.

7. **`AGENTS.md`, "Gotchas", directly after the parity-matrix bullet:**

   ```markdown
   - Adding, removing or moving an `A11yEvent` construction site on either host
     changes the trigger ledger in
     `docs/plans/38_notification_dispatcher_contracts.md`, which is generated.
     With a Windows Release build, rerun
     `python scripts/a11y_trigger_ledger.py --write`. Without one, push:
     `windows.yml` fails on drift (`--check`) and prints each stale row with the
     generator's row under it; replace the `-` rows with the `+` rows.
   ```

8. **If the first real `--check` is red.** Run `--write` on the Release build, commit the diff in this PR, and name it under "Found and fixed here".

9. **Not taken.** `canvas_trigger_ledger.py` and `graph_trigger_ledger.py` have no `--check`, and no workflow runs `test_canvas_trigger_ledger.py` (reading, cluster items 2 to 4). Medium by rule 1: deferred to CB-46.

## Tests

Failing first. Pester, in `Workflows.Tests.ps1` (extend the header comment `:4-10`):

- In `Describe 'windows.yml FlaUI handoff'`:
  - `It 'keeps the binaries seven days, as long as the app job''s own results, so a rerun of the gate alone finds them after the first day'`. Pins `retention-days: 7` inside the `Upload FlaUI gate binaries` step and inside `Upload app test results and hang evidence`.
  - `It 'no artifact in windows.yml, nightly.yml or the pilot is kept under seven days'`. Every `retention-days: N` in the three loaded texts has N of 7 or more, and `windows.yml` has at least four.
- New `Describe 'windows.yml generated tables'`, placed after the FlaUI handoff block (away from the a11y block CB-5 edits):
  - `It 'checks contracts 34, 35 and 38''s generated tables against their generators after the build, unconditionally, before the unit suite'`. In job `windows`, each of the three `Verify contract …` steps exists once, and its only lines besides comments are its `name` and its `run`, verbatim (the guard included for 38). Step order: `Build`, `Set up Python (the reconciliation checks)`, 34, 35, 38, `Test`.
  - `It 'tests that each check catches its drift: a stale pointer and a reordered row'`. The two `Test that …` steps, `name` and `run` only, verbatim, after the 38 step and before `Test`.
  - `It 'checks the parity matrix against its generator in the model job, unconditionally'`. In `windows-model`, the step `Verify the parity matrix is the generator's output` is only its `name` and `run: python3 scripts/generate-parity-matrix.py --check`, after `Checkout`.
- Red on `main`: both handoff facts and the first two generated-table facts. The parity fact pins what exists; show it red once by deleting its step locally, and say so in the PR.

Python, new `scripts/tests/test_a11y_trigger_ledger_check_flags_a_reordered_row.py`, class `LedgerCheckFlagsAReorderedRow`. It is hermetic. A temp folder holds a two-key inventory (`Alpha` with Windows sites `A.cs#M@1`, `A.cs#M@2`, `B.cs#N@1`; `Beta` with one; one Mac site each), a designations file with empty `designated`, `residue` and `roles`, and a prose-only document. Setup loads the module by path, points `CONFIG` and `DOCUMENT` at the temp files, runs `main(["--write", "--inventory", …])`, and checks that `--check` then returns.

- `test_a_reordered_row_fails_the_check_and_both_orders_are_printed`: swap Alpha's first two Windows sites in the document. `--check` raises `SystemExit` whose code is a string containing "stale". Captured stdout has one `-|` line with the hand order, one `+|` line with the generator's order, and an `@@` header with that row's line number.
- `test_write_repairs_a_reordered_row`: after the swap, `--write` then `--check` passes, and the document's bytes are those from before the swap.
- `test_the_printed_rows_pasted_into_the_document_pass_the_check`: add `A.cs#M@3` to the inventory. `--check` fails. Replace each `-` row in the document with the printed `+` row. `--check` passes. This is the Mac contributor's route.

Python, added to `CheckFacts` in `scripts/tests/test_windows_docs_trigger.py` (`make_repo` gains a `scripts/` file):

- `test_a_script_run_as_python_dash_i_is_scanned`: a workflow step `run: python -I scripts/reader.py --check` and a `reader.py` holding `"docs/plans/43_mac_backlog_delivery/x.md"`; the problems name that path.
- `test_a_docs_path_a_script_joins_from_segments_is_a_read`: the same with `os.path.join(ROOT, "docs", "plans", "43_mac_backlog_delivery", "x.md")`.
- `test_the_census_scans_the_three_generators_the_app_job_checks`: on the real tree, `script_reads()` returns contracts 34, 35 and 38 with their three scripts as readers.

**The lane probe** (`AGENTS.md` "Gotchas"). From the PR's head, branch `claude/cb03-probe`. One commit swaps the first two sites in the Windows cell of the `BasesFiltersOpenFailed` row (`38_…contracts.md:874`). Push, then `gh workflow run windows.yml --ref claude/cb03-probe`. Expect the app job red at the contract 38 step, with the `-` row in the hand order, the `+` row, and the sentence. The `+` row must equal `main`'s line 874 character for character once the log's prefix and the `+` are removed; that proves the log carries rows unmangled. Cancel the run once the app job has failed. Delete the branch. The PR body quotes the run id.

## Docs and registers

- `windows.yml`, `Workflows.Tests.ps1`, `a11y_trigger_ledger.py`, `check_windows_docs_trigger.py` and its test, the new `scripts/tests/` file, `AGENTS.md`.
- Contract 38's document only if design item 8 applies. No register row closes. No version is brought in.

## Verification

```powershell
Import-Module Pester -RequiredVersion 5.9.1 -Force
./ci/windows-runner/tests/Invoke-Tests.ps1      # four facts red on main, all green after
python -I -m unittest discover -v -s scripts/tests -p test_a11y_trigger_ledger_check_flags_a_reordered_row.py
python -I -m unittest discover -v -s scripts/tests -p test_windows_docs_trigger.py
python -I scripts/check_windows_docs_trigger.py
# after the Windows recipe (per-worktree bindings, Release build), with no Slate running:
pwsh -NoProfile -File scripts/windows/guard-device-state.ps1 -- python -I scripts/a11y_trigger_ledger.py --check
gh pr checks <N>                                # windows and windows-runner-tests
gh api repos/{owner}/{repo}/actions/runs/<run id>/artifacts --jq '.artifacts[] | select(.name | startswith("flaui-binaries")) | [.created_at, .expires_at]'
```

CI lanes: `windows.yml`, `windows-runner-tests.yml`. AT: none.

## Acceptance

- [ ] `flaui-binaries-<run>` keeps seven days; the PR quotes the artifact's `created_at` and `expires_at` from its own run.
- [ ] The app job runs the contract 38 check under the device-state guard, after the build and before `Test`; the PR quotes the green step's run id and duration.
- [ ] A stale ledger prints each differing row with its line number. The probe run shows the step red on a hand-reordered row, with the `+` row equal to `main`'s. The probe branch is deleted.
- [ ] The new unit test file's three tests pass and run in the app job.
- [ ] Five Pester facts green: four seen red on `main`, the fifth red with its step deleted.
- [ ] The docs-trigger census scans `python -I scripts/…` runs and segment-joined paths; its three new facts pass.
- [ ] `AGENTS.md` carries design item 7's bullet after the parity-matrix bullet.
- [ ] "Found and fixed here" (plan §4 rule 2) lists the segment-joined paths, and the regenerated ledger if item 8 applied, each with its failing test first.
- [ ] The PR body ends with the count (rule 8): found on the way, fixed here, deferred (the two family ledgers and their unrun test, to CB-46), filed.
- [ ] `Closes #1647`, `Closes #1699`. `not delivered: a gate-only rerun after the first day` (the expiry date is the witness).

## Brief

Read `windows.yml:676-700`, `:760-777`, `:813-847` and the `windows-model` job; `Workflows.Tests.ps1:1-48`, `:192-243`; `a11y_trigger_ledger.py`; `check_windows_docs_trigger.py:58-67`, `:212-220` and its test; `test_reconciliation_check_flags_a_stale_pointer.py`; `AGENTS.md` "Gotchas". Build: `retention-days: 7` on `Upload FlaUI gate binaries`, its comment corrected; the two app-job steps of design item 2, verbatim, with no other key; `a11y_trigger_ledger.py` with `main(argv=None)`, UTF-8 stdout, and on a stale `--check` a whole-document unified diff with no context lines before the new exit sentence; the hermetic unit test file with its three tests; `_SCRIPT_RUN` accepting `python`, `python3` and `-I`, and `script_reads()` also reading `"docs", "plans", …` chains, with three new `CheckFacts`; two facts in `Describe 'windows.yml FlaUI handoff'` and three in a new `Describe 'windows.yml generated tables'`, named as in Tests; the `AGENTS.md` bullet verbatim. Run the real `--check` locally under the guard on a Release build; if it is red, `--write` and commit the diff as a found-and-fixed line. Prove the step red on the lane with the `claude/cb03-probe` dispatch (swap two sites in `BasesFiltersOpenFailed`, line 874), compare the logged `+` row with `main`'s, cancel the run, delete the branch. Done when Pester and the Python tests are green (red first) and both lanes are green on the PR. Body: the probe run, the green step's duration, the artifact's expiry, `Closes #1647`, `Closes #1699`, "Found and fixed here", `not delivered: a gate-only rerun after the first day`, the count with the two family ledgers deferred to CB-46. Commit: `ci(windows): the shell gate's binaries keep seven days, and the lane checks contract 38's ledger against its generator`.
