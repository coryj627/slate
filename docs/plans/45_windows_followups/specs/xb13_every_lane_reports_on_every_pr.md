# XB-13 — Every lane reports on every PR, so `main`'s ruleset can require them

Issues: #1565 (what remains after XB-0 writes step 3; closes here after a week of PRs under the lane checks). Size M. Lanes: every PR workflow (each runs on the PR that changes it), windows-runner-tests (the Pester facts). AT: —. Wave: X5, slot 3. Depends on: XB-0 (the merge procedure on `main`). Parallel with: XB-10b, XB-12. Decision: the owner's of 2026-10-22 names this as the follow-up; the owner's of 2026-10-25 on #1565 takes in-workflow change detection over twin workflows (Design). Codex adversarial review per plan 44 §4 rule 5 (size M).

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

The first draft gave each lane a twin, `<W>-paths-unchanged.yml`, triggered by `paths-ignore` of W's list. That does not hold. GitHub runs a `paths-ignore` workflow whenever any changed file is outside its list, so a PR touching files both inside and outside W's paths runs W and its twin under one check name, and the twin's green could stand for a failing lane. The owner took option 1 of the comment on #1565 (2026-10-25), in-workflow change detection:

1. **Every lane triggers on every PR.** The eight lanes (`a11y-check`, `audit`, `license-headers`, `mac-runner-tests`, `rust`, `swift-tests`, `windows-runner-tests`, `windows`) drop `on.pull_request.paths`. Each keeps one path list, `on.push.paths`, which GitHub applies to pushes to `main`. The two pilots (`mac-ci-pilot.yml`, `windows-ci-pilot.yml`) keep their filters: nothing requires them.
2. **The decision.** Each lane's first job, `changes`, calls `.github/workflows/lane-changes.yml` with the lane's own file.
   - On a pull request it checks out the merge commit and its parents (depth 2, trees only, sparse) and runs `scripts/lane_changes.py`.
   - The script lists `git diff --name-only --no-renames HEAD^1 HEAD` and matches it against the lane's `on.push.paths` with GitHub's filter-pattern rules: `*`, `**`, `**/` as zero or more folders, `!` negation, and the last match decides. It refuses `?`, `+` and `[]`.
   - It writes `run=true` or `run=false`. On any other event the decision is `run=true`.
   - `check_windows_docs_trigger.py` imports the same matcher.
3. **The gates.** Two kinds of job decide for themselves: a job with no other needs, and a job whose `if:` has a status function.
   - Each of those needs `changes` and opens its `if:` with `!cancelled() && needs.changes.outputs.run != 'false'`. The aggregates that already used `always()` keep it.
   - Any other term follows with `&&`, and an `||` stays inside parentheses.
   - Every other job reaches `changes` through a gated job and keeps its implicit `success()`.
   - So a `false` decision skips every job, and a skipped job reports as passed. A failed or cancelled decision leaves `run` empty, and the lane runs.
   - Required-check names do not change. `windows.yml`'s `build + test (windows x64)` is skipped with the lane, and so passes. When the lane runs, it still fails unless every lane job succeeds.
4. **No dropped `main` runs.** Shipped first as #1638 (46d7a34a): every lane groups pushes by `github.sha` and pull requests by `github.ref`.
5. **The ruleset, the owner's step, after a week.** The PR body carries the `gh api -X PUT repos/coryj627/slate/rulesets/25660813` payload, which adds each lane's check context (integration 15368). The owner applies it once seven days of PRs show every lane reporting.
6. **Not changed:** any lane job's runner, steps or name; `pr-gate.yml`'s trigger and job; the lanes' push triggers and their lists.

## Tests

- `Workflows.Tests.ps1`, `Describe 'lane change detection'`:
  - per lane: the bare `pull_request` trigger and the one `on.push.paths` list;
  - the `changes` job comes first and hands over the lane's own file;
  - every other job is gated by the rule in Design 3, checked as a fixpoint over `needs`;
  - a fixture fact shows the rule refuses each way of breaking it;
  - `lane-changes.yml`'s trigger, permissions and checkout, and its script expands no expression.
- The routing, path-list and SDK-pin facts move to the gated `needs:` and `if:` lines and to one list.
- `scripts/tests/test_lane_changes.py`:
  - the cheat sheet's patterns, and the refused syntax;
  - a real repository with a merge commit: a lane path changed; no lane path changed while `main` moved in one; a rename out of the paths; a head that is not a merge; an unreadable list.
- `scripts/tests/test_windows_docs_trigger.py`, `test_mac_sdk.py` and `test_mac_bundle_resources.py` read one list.
- Mutations: each of nine breaks fails a fact. Among them:
  - a lane job with an implicit `success()` on `changes`;
  - a status function without the decision;
  - a `pull_request` list put back;
  - a top-level `||`;
  - a job that never needs `changes`;
  - another lane's file handed over;
  - an expression in the script.
- The PR itself edits every lane's own file, so every lane runs on it. A draft scratch PR stacked on the branch, touching only `README.md`, shows the other half: every lane's `changes` decides `false`, and every other check reports as skipped.

## Docs and registers

- New: `lane-changes.yml`, `scripts/lane_changes.py` and its test.
- The eight lanes, and the header comments of `pr-gate.yml` and `windows.yml`.
- `windows-runner-tests.yml`: its list gains every lane, the reusable workflow, the script and its test, plus a step that runs the script's tests.
- AGENTS.md:
  - every lane reports on every PR;
  - the lanes are the author's to check until the ruleset requires them;
  - merge step 3 no longer says a push drops a queued run (#1638).
- #1565: the owner's choice, the ruleset payload and the week's watch.

## Verification

```powershell
python -I -m unittest discover -v -s scripts/tests -p test_lane_changes.py
python -I -m unittest discover -s scripts/tests -p test_windows_docs_trigger.py
python3 scripts/check_windows_docs_trigger.py
Import-Module Pester -RequiredVersion 5.9.1 -Force; ./ci/windows-runner/tests/Invoke-Tests.ps1
gh pr checks <N>                 # every lane ran; each required context once
gh pr checks <scratch PR>        # every lane's changes decided false; the rest skipped
gh api repos/coryj627/slate/rulesets/25660813 --jq '.rules[] | select(.type == "required_status_checks")'   # unchanged by this PR
```

## Acceptance

- [ ] Every lane triggers on every PR, decides in its first job from its one path list, and skips every other job when the PR touches none of its paths. A failed decision runs the lane. The Pester rule and the script's tests pin this.
- [x] `main` pushes never queue behind one another (#1638).
- [ ] The PR body carries the ruleset payload and the week's watch, and the scratch PR's check list is recorded.
- [ ] `Refs #1565` at merge; `Closes #1565` after the owner applies the ruleset (a comment then closes it). Not delivered: the ruleset change (owner).

## Brief

The shipped design is Design 1-3 above; the first draft's twins are superseded. Read `.github/workflows/lane-changes.yml`, `scripts/lane_changes.py`, and `Workflows.Tests.ps1`'s 'lane change detection'.

To add a lane:
1. Drop its `pull_request` paths.
2. Add the `changes` job first, with the lane's own file.
3. Gate its root jobs and its status-function jobs as Design 3 says.
4. Add its file to `windows-runner-tests.yml`'s list and to the Pester `-ForEach` lists.

Commit: `ci: every lane reports on every pull request, its jobs skipped when the pull request touches none of its paths`.
