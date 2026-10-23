# XB-13 — Every lane reports on every PR, so `main`'s ruleset can require them

Issues: #1565 (what remains after XB-0 writes step 3; closes here after a week of PRs under the lane checks). Size M. Lanes: every PR workflow (each twin runs on the PR that adds it), windows-runner-tests (the Pester facts). AT: —. Wave: X5, slot 3. Depends on: XB-0 (the merge procedure on `main`). Parallel with: XB-10b, XB-12. Decision: none (the owner's decision of 2026-10-22 names this as the follow-up). Codex adversarial review per plan 44 §4 rule 5 (size M).

## Goal

A PR that does not touch a lane's paths still gets a green check under that lane's required name, so `main`'s ruleset can require the lanes themselves and not only `pr gate`; a queued `main` run is never dropped by a later push; and after a week of PRs with no false red, the owner adds the lane checks to ruleset 25660813.

## Current state on main

- **Path-filtered PR lanes** (`on.pull_request.paths`): `a11y-check.yml` (`:19-23`), `audit.yml` (`:12-24`), `license-headers.yml` (`:10-18`), `mac-ci-pilot.yml` (`:5-11`), `mac-runner-tests.yml` (`:8-15`), `rust.yml` (`:36-44`), `swift-tests.yml` (`:22-46`), `windows-ci-pilot.yml` (`:5-10`), `windows-runner-tests.yml` (`:9-25`), `windows.yml` (`:111-151`; "keep both lists identical" `:123-132`, enforced by `scripts/check_windows_docs_trigger.py`). `pr-gate.yml` is the only workflow on every PR (`:15-17`, no paths; one job `pr gate` `:26-27`; pinned by `Workflows.Tests.ps1:254-273`).
- **Check names the ruleset would require:** `windows.yml`'s `gate` aggregate `build + test (windows x64)` (`:916-937`, `needs: [route, rust-tests, windows, windows-model, flaui]`, `if: always()`, fails unless every lane is `success`; "the historical required-check identity" `:891-894`; pinned `Workflows.Tests.ps1:100-113`); `rust.yml` jobs (`fmt / clippy` `:66`, the test jobs); `swift-tests.yml`, `a11y-check.yml`'s `Report and enforce the floor` (`:181`), `license-headers.yml`, `audit.yml`, the two runner-tests workflows. Read each job's `name:` when building the list.
- **Concurrency.** `windows.yml:195-197` `group: ${{ github.workflow }}-${{ github.ref }}`, `cancel-in-progress: ${{ github.event_name == 'pull_request' }}` (unchanged since #956). On `main`, cancel-in-progress is false, but GitHub keeps at most one *pending* run per group: when a run is in progress and another is pending, a third push cancels the pending one — which is how #1540's push dropped 834f3573's run. Every other lane uses the same form (`rust.yml:57-59`, `swift-tests.yml:75-77`, …); `nightly.yml:42-45` uses `queue: max`.
- **Ruleset** 25660813: `required_status_checks` = [`pr gate` (integration 15368)], `strict_required_status_checks_policy: true`, enforcement active (read 2026-10-23).
- No workflow has a skipped-success twin; none uses changed-files detection.

## Reference

GitHub's documented pattern for required checks on path-filtered workflows (a second workflow with the same job name, triggered by `paths-ignore` of the same list, that succeeds); GitHub's concurrency semantics (one in-progress plus one pending per group). AGENTS.md "Git and PRs" (after XB-0).

## Design

1. **Twins.** For each path-filtered PR workflow W with required job names J₁…Jₙ: a workflow `W-paths-unchanged.yml` (`name:` identical to W's, so the check context matches) with `on.pull_request.paths-ignore` equal to W's `paths` list and `branches: [main]`, `permissions: {}`, no checkout, and one job per Jᵢ whose `name:` is exactly Jᵢ, `runs-on: ubuntu-latest`, `timeout-minutes: 2`, one `run: echo "<W>: no path in this PR triggers the lane; reported as passed (#1565)"`. Since `paths` triggers when any changed file matches and `paths-ignore` skips only when every changed file matches, exactly one of W and its twin runs for any PR (the twin also handles the "no files changed" edge as GitHub documents). The twin's job list is the required set only: for `windows.yml`, the single `build + test (windows x64)`; for the others, the job(s) the owner will require (list them in the PR).
2. **One source of truth for the lists.** `scripts/check_windows_docs_trigger.py` grows into `scripts/check_lane_twins.py` (or a sibling): for every W with a twin, the twin's `paths-ignore` equals W's `paths` (both PR lists), the twin's `name:` equals W's, and the twin's job names are a subset of W's job names. It runs where `check_windows_docs_trigger.py` runs today (`windows.yml`'s ubuntu aggregate and `windows-runner-tests.yml`; find the step) and has a `scripts/tests/test_lane_twins.py`.
3. **No dropped `main` runs.** For every lane that runs on `push: main`, the concurrency group becomes per-commit on pushes: `group: ${{ github.workflow }}-${{ github.event_name == 'pull_request' && github.ref || github.sha }}` with `cancel-in-progress` unchanged (PRs still cancel superseded pushes; `main` runs never share a group, so none is pending behind another). Alternative, if the owner prefers fewer concurrent `main` runs: `queue: max` as `nightly.yml:42-45` already uses; the PR says which shipped. `Workflows.Tests.ps1` pins the chosen form on `windows.yml` (and the Pester suite's other loaded workflows).
4. **The ruleset, the owner's step, after a week.** The PR body carries the exact `gh api -X PUT repos/coryj627/slate/rulesets/25660813` payload adding each lane's check context (integration 15368) to `required_status_checks`, to be applied by the owner once seven days of PRs show every twin reporting (`gh pr checks` on the merged PRs of that week). This chunk does not change the ruleset.
5. Not changed: any lane's own paths, jobs or steps; `pr-gate.yml`; the lanes' `push` triggers.

## Tests

- `Workflows.Tests.ps1`: `Describe 'lane twins'`: for each twin, `It 'mirrors <W>'s paths as paths-ignore, carries its name and its required job names, and does nothing else'` (no `uses:` but none needed, no `secrets.`, no checkout, `permissions: {}`); `It 'main runs are never queued behind one another'` (the concurrency form).
- `scripts/tests/test_lane_twins.py` (unittest, `-I`): a fixture pair in sync passes; one path missing from either side fails; a job name not in W fails.
- The PR itself: every twin runs on it (the PR adds the twins, so every lane's own file is touched too? No — adding `W-paths-unchanged.yml` does not touch W's paths unless W lists `.github/workflows/**`; read each list: lanes that list only their own file do not run, their twins do; the PR body quotes the check list showing each context reported exactly once).
- A follow-up scratch PR touching nothing (a whitespace change in `README.md`): every twin reports, no lane runs; record its check list.

## Docs and registers

- The new twin workflows; `scripts/check_lane_twins.py` + test; `Workflows.Tests.ps1`; `windows-runner-tests.yml` paths (the twins' files and the script).
- AGENTS.md "Git and PRs": one sentence that every lane now reports on every PR (after the ruleset step, the owner may add "the lanes are required checks").
- #1565: the week-long watch and the ruleset payload.

## Verification

```powershell
python -I scripts/check_lane_twins.py                     # or --check
python -I -m unittest discover -s scripts/tests -p "test_lane_twins.py"
python -I scripts/check_windows_docs_trigger.py
Import-Module Pester -RequiredVersion 5.9.1 -Force; ./ci/windows-runner/tests/Invoke-Tests.ps1
gh pr checks <N>                                          # each required context exactly once
gh pr checks <scratch PR>                                 # twins only
gh api repos/coryj627/slate/rulesets/25660813 --jq '.rules[0].parameters.required_status_checks'   # unchanged by this PR
```

CI lanes: the twins and `windows-runner-tests.yml` on the PR. AT: none.

## Acceptance

- [ ] Every path-filtered PR lane has a twin that reports its required job names when its paths did not change; the script and the Pester facts keep them in sync.
- [ ] `main` pushes never queue behind one another (per-commit group or `queue: max`), pinned.
- [ ] The PR body carries the ruleset payload and the week's watch; the scratch PR's check list recorded.
- [ ] `Refs #1565` at merge; `Closes #1565` after the owner applies the ruleset (a comment then closes it). Not delivered: the ruleset change (owner).

## Brief

Read every `.github/workflows/*.yml` `on:`, `paths:`, `concurrency:` and job `name:` lines (the survey table in plan 45's XB-13 spec §Current state), `.github/workflows/pr-gate.yml`, `windows.yml:880-940`, `nightly.yml:40-46`, `scripts/check_windows_docs_trigger.py`, `scripts/tests/test_windows_docs_trigger.py`, `ci/windows-runner/tests/Workflows.Tests.ps1:94-113`, `:200-212`, `:254-273`. Build: a `<W>-paths-unchanged.yml` twin per path-filtered PR lane (same `name:`, `paths-ignore` = W's PR `paths`, `permissions: {}`, no checkout, one echo job per required job name, `timeout-minutes: 2`); `scripts/check_lane_twins.py` + `scripts/tests/test_lane_twins.py` run where the docs-trigger check runs; the per-commit concurrency group for `main` pushes on every lane (or `queue: max`; say which); `Workflows.Tests.ps1` facts; AGENTS.md sentence; `windows-runner-tests.yml` paths. Open a scratch PR touching only whitespace to show the twins alone report; quote both check lists. Put the ruleset `PUT` payload in the PR body for the owner to apply after a week. Done when the script, the python tests and Pester are green, the PR's checks show each context once, the scratch PR's checks show twins only; `Refs #1565`. Commit: `ci: every path-filtered lane reports a passed check on the prs it does not run for, and main runs never queue behind one another`.
