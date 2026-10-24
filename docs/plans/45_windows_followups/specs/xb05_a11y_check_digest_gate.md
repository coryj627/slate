# XB-5 — A digest mismatch stops the a11y score from being computed

Issues: #1575. Size S. Lanes: a11y-check (the PR's own run: the workflow's `paths` include its own file), windows-runner-tests (the Pester fact). AT: —. Wave: X2, slot 3. Depends on: nothing. Parallel with: XB-2, XB-4b. Decision: none.

## Goal

When `actions/download-artifact` fails on the analysis results, for any reason, the score job never reads the files it extracted: nothing is uploaded to Code Scanning, no score comment is posted, and the floor step fails saying the download failed. A missing artifact keeps today's plain-words report.

## Current state on main

- `.github/workflows/a11y-check.yml`: triggers `:19-28` (`pull_request` and `push main`, paths `apps/slate-mac/**/*.swift` and the workflow file); `analyze` job `:79-176` on `${{ needs.route.outputs.label }}` (`:84`; same-repo guard `:83`), writes `a11y-results.json` (`:160-162`) and `a11y-results.sarif` (`:164-166`), uploads `a11y-results` (`:168-176`, `if-no-files-found: warn`).
- Score job `a11y-check` `:180-358` on `ubuntu-latest` (`:187`), `if: !cancelled() && needs.analyze.result != 'skipped'` (`:186`): checkout `:197-200`; **download** `:202-208` (`actions/download-artifact@9000827…  # v8.0.2`, `name: a11y-results`, `continue-on-error: true`, comment `:206-207` "A missing artifact means the analysis died … the floor step below reports that"); upload-sarif `:210-218` (`if: hashFiles('a11y-results.sarif') != ''`, `continue-on-error: true`); PR score comment `:220-283` (`if: pull_request && hashFiles('a11y-results.json') != ''`, `continue-on-error: true`); **Enforce score floor** `:285-358` (`MIN_SCORE` `:287-288`, from `:51` = 100): missing JSON → `core.setFailed("… was not produced — a11y-check likely crashed …")` (`:295-301`); then score/`totalErrors`/`MIN_SCORE` validation and the two failure conditions (`:348-357`).
- download-artifact v8 extracts before it verifies the digest, so a digest mismatch leaves `a11y-results.json` and `.sarif` on disk, the step fails, `continue-on-error` carries on, and the three later steps use the files. No digest is captured anywhere else in `.github` (`.github/actions/windows-pilot-binaries/action.yml:50` is unrelated).
- `ci/windows-runner/tests/Workflows.Tests.ps1` does not load `a11y-check.yml`; `ci/mac-runner/tests` cover the admission hook, not workflow shapes; `mac-runner-tests.yml` triggers on `a11y-check.yml` (`:18-23`) but runs the mac-runner tests only.

## Reference

The issue's two options (drop `continue-on-error` and handle "no results" explicitly, or delete the extracted files on failure). actions/download-artifact's documented behaviour (digest check after extraction, in v8).

## Design

> **Delivered shape (2026-10-24).** Step 2 below keys the floor step's message on the download step's outcome. But a missing artifact fails that step too, and the Goal keeps today's plain-words report for that case. So the discard step records `discarded=true` when it removed files, and the floor step reads `RESULTS_DISCARDED`, `RESULTS_DOWNLOAD` and `ANALYZE_RESULT`. Discarded files fail with "The analysis results were extracted, but the download step failed (a digest mismatch or an interrupted download), so they were discarded and no score was computed." A download that failed with nothing extracted after a successful analysis fails with "The analysis job succeeded, but its results could not be downloaded, so no score was computed." Anything else keeps the "was not produced" sentence. The Pester fact also pins that the floor step has no `if:` and no `continue-on-error:`, and that each missing-results branch is a `setFailed` that returns. The forced missing-artifact run in Tests was not made: it would cost a mac analysis run, and it would only show the unchanged "was not produced" sentence (see the PR). #1616 tracks the stale score comment and the malformed-JSON stack trace, both of which predate this change.

1. **Name the step and act on its outcome.** `:202-208` gains `id: results` and keeps `continue-on-error: true` (so a missing artifact still reaches the floor step's plain-words message). A new step right after it:
   ```yaml
         - name: Discard results the download did not verify
           if: steps.results.outcome != 'success'
           run: rm -f a11y-results.json a11y-results.sarif
   ```
   With the files gone, upload-sarif and the score comment skip by their `hashFiles` guards, and the floor step takes its missing-file branch.
2. **Say why.** The floor step reads `env: RESULTS_DOWNLOAD: ${{ steps.results.outcome }}` and its missing-file message (`:295-301`) becomes: when `RESULTS_DOWNLOAD == 'failure'`, "the analysis results could not be downloaded or their digest did not match, so no score was computed — see the download step"; otherwise today's sentence.
3. **Pin it.** `Workflows.Tests.ps1` loads `a11y-check.yml` (`:15-18`) and gains `Describe 'a11y-check.yml'`: `It 'discards the downloaded results when the download step did not succeed, before anything reads them'` (the download step has `id: results`; the next step's `if:` is `steps.results.outcome != 'success'` and its `run:` removes both files; both the upload-sarif and the score-comment steps come after it and keep their `hashFiles` guards). `windows-runner-tests.yml`'s path lists (`:11-25`, `:28-42`) add `.github/workflows/a11y-check.yml`; its path-filter fact (`:200-205`) is extended.
4. Not changed: the `analyze` job, the upload, `MIN_SCORE`, the score-comment script, the SARIF upload.

## Tests

- The Pester fact (red on `main`).
- The PR's own `a11y-check.yml` run: green (the normal path still computes the score). The failure path cannot be forced on the PR without a corrupt artifact; the witness is the step shape plus one manual run on a scratch branch where the download `name` is deliberately wrong (artifact missing → the floor step's missing-file message) — record its run id in the PR; the digest-mismatch path itself is argued from the action's documented order and the `rm`, not run.

## Docs and registers

- `.github/workflows/a11y-check.yml`, `windows-runner-tests.yml`, `Workflows.Tests.ps1`.
- Nothing else.

## Verification

```powershell
Import-Module Pester -RequiredVersion 5.9.1 -Force
./ci/windows-runner/tests/Invoke-Tests.ps1             # the new fact red on main, green after
gh pr checks <N>                                       # a11y-check (analyze + a11y-check jobs) and windows-runner-tests green
```

CI lanes: `a11y-check.yml` (its `analyze` job runs on the mac route; the PR is the owner's, same-repo), `windows-runner-tests.yml`. AT: none.

## Acceptance

- [ ] A failed download removes both result files before any step reads them; the floor step names the download as the cause.
- [ ] Pester fact green; `windows-runner-tests.yml` watches the workflow file.
- [ ] The PR's a11y-check run is green; the forced missing-artifact run shows the new message.
- [ ] `Closes #1575`. Not delivered: a live digest-mismatch reproduction (argued from the action's order).

## Brief

Read `.github/workflows/a11y-check.yml:180-300`, `ci/windows-runner/tests/Workflows.Tests.ps1:10-50`, `:200-212`, `:254-275`, `.github/workflows/windows-runner-tests.yml:9-45`. Build: `id: results` on the download step (`:202-208`, keep `continue-on-error: true`); a following step `Discard results the download did not verify` with `if: steps.results.outcome != 'success'` and `run: rm -f a11y-results.json a11y-results.sarif`; the floor step gets `env: RESULTS_DOWNLOAD: ${{ steps.results.outcome }}` and, when it is `failure`, fails with "the analysis results could not be downloaded or their digest did not match, so no score was computed — see the download step"; `Workflows.Tests.ps1` loads `a11y-check.yml` and pins the id, the discard step's `if`/`run`, its position before upload-sarif and the comment step, and their `hashFiles` guards; `windows-runner-tests.yml` paths add the workflow. Push a scratch branch with the download `name` misspelt to see the missing-artifact message, record the run id, delete the branch. Done when Pester is green (red first), `a11y-check.yml` and `windows-runner-tests.yml` are green on the PR. `Closes #1575`. Commit: `ci: the a11y score is never computed from results whose download did not verify`.
