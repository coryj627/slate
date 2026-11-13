# CB-5 — a11y-check rewrites its comment when no score is computed, and malformed results fail in one sentence

Issues: #1616. Size S. Lanes: a11y-check (the PR's own run; its analysis job needs a mac runner in CI, the Studio or Namespace's), windows-runner-tests (the Pester fact), mac-runner-tests (starts for this file; pins nothing this PR changes). AT: —. Wave: C2, slot 1, after CB-3. Depends on: CB-3 merged (both edit `Workflows.Tests.ps1`). Parallel with: CB-6, CB-8. Decision: CD4, which the owner answers before C0 (plan §3); this spec encodes its recommendation (rewrite the existing comment, never create one). Interlock: `Workflows.Tests.ps1` goes CB-3, CB-5, CB-44; rebase and rerun the suite, never hand-merge. Review: on its tests alone (plan §4 rule 9).

## Goal

When the score job computes no score, the bot's existing score comment on the pull request is rewritten to say so, with the sentence the floor step failed with and a link to the run. Where the bot had no comment, none is created. Results that are not valid JSON, or hold no valid score, fail the floor step in one plain sentence with no stack trace. No verdict changes: every case here is already a red check.

## Current state on main

Read at c2377210. `.github/workflows/a11y-check.yml` is 451 lines.

- Score job `a11y-check` `:205-418`. Its `if:` (`:211`) ends `needs.analyze.result != 'skipped'`. `analyze` (`:104-201`) has no status function, so it is skipped when `route` (`:72-102`) fails, and the score job with it.
- Steps in order: download `:227-234` (`id: results`); discard `:243-250` (`id: discard`); SARIF upload `:252-260`; `Post score summary on PR` `:262-325`; `Enforce score floor` `:327-418`.
- Comment step: guard `:263` is `github.event_name == 'pull_request' && hashFiles('a11y-results.json') != ''`. Its own unguarded `JSON.parse` is `:268`; `:270` calls `.toFixed` on whatever `score` holds. `listComments` `:297-301` reads one page. The finder `:302-307` takes a Bot comment whose body starts `## ✅ Accessibility score` or `## ❌ Accessibility score`. `createComment` `:316-321`. `continue-on-error: true` `:325`.
- Floor step: no `id`, `if:` or `continue-on-error:`. Env `:329-333`. Missing file: three `core.setFailed` branches, each with `return;` (`:340-361`). `:362` is `const results = JSON.parse(…)` with no `try`; `:363` is `results.score || {}`, which throws on a JSON `null`. Shape checks `:366-384`, `MIN_SCORE` checks `:391-406`, `core.info` `:407`, the two verdicts `:408-418`.
- `ci/windows-runner/tests/Workflows.Tests.ps1` (847 lines; CB-3 shifts these numbers, so find the fact by name): `Describe 'a11y-check.yml'` `:325-386` holds one fact (`:328`). It pins the comment step's `if:` with `hashFiles` (`:348`), anchors a regex on `const results = JSON\.parse` (`:380`), opens each missing-file branch with `core.setFailed(` (`:366`, `:373`, `:379`) and counts three of them and three `return;` (`:383-384`). `Test-Decided` (`:548-554`) allows `||` in a job's `if:` only inside parentheses.
- `ci/mac-runner/route/tests/test_workflows.py` reads the file's `run:` blocks and the route step's env; nothing below touches either.
- **Unverified:** that github-script v9.0.0 prints a stack trace for an uncaught throw (the issue's wording; push B records what it prints), and `github.paginate` (from the action's README, not read at the pinned SHA).

## Reference

#1616 (no comments). Reading `02_ci_and_runner.md`: the #1616 section, cluster PR 2, risks 7 and 9. XB-5's spec and delivered shape (#1575).

## Design

All in the score job. The download, discard and SARIF steps, `MIN_SCORE`, `analyze` and the gate's script do not change.

1. **The floor step words every "no score" outcome.** It gains `id: floor` and stays first-class. Its script opens with:
   ```js
   const noScore = (reason) => {
     core.setOutput('computed', 'false');
     core.setOutput('reason', reason);
     core.setFailed(reason);
   };
   ```
   Every failure before a verdict becomes `noScore(…); return;`, sentence unchanged: the three missing-file branches, the two shape checks, the three `MIN_SCORE` checks. Two are new. A missing-file branch before the default one: `process.env.ANALYZE_RESULT === 'skipped'` gives `The analysis did not run, so no score was computed. See the "Route mac jobs" job.` And the parse:
   ```js
   let results;
   try {
     results = JSON.parse(fs.readFileSync(resultsPath, 'utf8'));
   } catch (error) {
     noScore(
       `${resultsPath} is not valid JSON (${error.message}); no score was computed. See the analysis job.`
     );
     return;
   }
   const s = (results && results.score) || {};
   ```
   `core.setOutput('computed', 'true');` is one line, directly before `core.info(…)`. The two verdicts after it stay `core.setFailed`. The step then has ten `noScore(` calls and three `core.setFailed(`.
2. **The comment step moves after the floor step:**
   ```yaml
         - name: Post score summary on PR
           if: ${{ !cancelled() && github.event_name == 'pull_request' }}
           uses: actions/github-script@3a2844b7e9c422d3c10d287c895573f7108da1b3  # v9.0.0
           env:
             SCORE_COMPUTED: ${{ steps.floor.outputs.computed }}
             NO_SCORE_REASON: ${{ steps.floor.outputs.reason }}
   ```
   It keeps `continue-on-error: true`, and its script expands no `${{ }}`. It opens with `const marker = '<!-- slate-a11y-score -->';` and `const computed = process.env.SCORE_COMPUTED === 'true';`.
   - `if (computed) {`: today's body from the file, with `''` and `marker` appended. The file is read only in this branch.
   - `else`: the reason is `NO_SCORE_REASON`, or `The score floor step did not report a result.` when it is empty (a failed discard skips the floor). It is data: one line, backticks replaced by `'`, cut at 500 characters, inside a `text` code fence, so it cannot render or mention anyone. Body: `## ❌ Accessibility score: not computed`; the fenced reason; `The check is red, and any score this comment showed before is out of date.` with a link to the run built from `GITHUB_SERVER_URL`, `GITHUB_REPOSITORY` and `GITHUB_RUN_ID`; `''`; `marker`.
   - Finder: `await github.paginate(github.rest.issues.listComments, { owner, repo, issue_number, per_page: 100 })`, then the first Bot comment whose body includes `marker` or starts with either old heading. Heading first and marker last, so comments written before this change are still found.
   - `if (existing)` update; `else if (computed)` create; `else` `core.info('No score was computed and this pull request has no score comment; none is created.')`.
3. **The score job runs when a same-repo pull request's analysis was skipped.** `:211` becomes `if: ${{ !cancelled() && needs.changes.outputs.run != 'false' && (needs.analyze.result != 'skipped' || (github.event_name == 'pull_request' && github.event.pull_request.head.repo.full_name == github.repository)) }}`. There a skipped analysis means `route` failed, so the gate is red already. A fork and a push to `main` skip as today. The comments at `:208-210`, `:236-242`, `:323-324` and `:420-425` are brought in line.

**Every stale-comment case:**

| Case | Today | This PR |
|---|---|---|
| No artifact; the analysis died (`:356-359`) | step skipped at `:263`; ✅ stays | Fixed |
| Download failed after a good analysis (`:349-354`) | same | Fixed; push D |
| Results discarded on a digest mismatch (`:341-347`) | same | Fixed by the same path; not delivered on the lane (a mismatch cannot be forced, as XB-5) |
| Malformed JSON: the comment step's parse (`:268`) throws softly, the floor's (`:362`) hard | ✅ stays; a stack trace | Fixed, both; pushes B, C |
| Analysis skipped because `route` failed (`:211`) | job skipped; ✅ stays | Fixed for same-repo pull requests; push E |
| Over 30 comments (`:297-301`) | comment not found; a second is created | Fixed by paging, pinned by text; not delivered on the lane (30 filler comments, each a notification) |
| Valid JSON, no valid score (found writing this spec) | `150` posts "✅ 150.0 / 100" beside a red floor; a non-number throws at `:270`, ✅ stays; `null` throws at `:363` with a stack trace | Fixed: `computed` is set only after the floor's checks. "Found and fixed here" |
| A cancelled run | nothing runs | Not delivered: it posts nothing; the next run corrects the comment |
| Second comments the paging defect already made | — | Not delivered: the finder takes the oldest; duplicates are not removed |

If CD4 is answered "delete", the `else` branch deletes `existing` in place of the rewrite; the rest stands. The probe then expects, at C, D and E, that the comment is gone and nothing replaces it, and at F that a new ✅ comment is created; the PR body records the new comment's id beside F.

## Tests

- **New Pester fact,** red on `main`, in `Describe 'a11y-check.yml'`: `It 'when no score is computed, the comment step rewrites the bot''s comment with the floor''s sentence and never creates one, and malformed results fail in one plain sentence'`. It pins:
  1. `        id: floor` on the floor step, and the five `noScore` helper lines, verbatim;
  2. exactly three `core.setFailed(` and ten `noScore(` calls in the floor step, each call's `);` followed by a `return;` line;
  3. the parse, `let results;` to `noScore(`, verbatim; the sentence `is not valid JSON (${error.message}); no score was computed. See the analysis job.`; one `JSON.parse` in the step; `const s = (results && results.score) || {};`;
  4. `core.setOutput('computed', 'true');` once, after the last `noScore(`, directly before `core.info(`;
  5. the `ANALYZE_RESULT === 'skipped'` branch and its sentence;
  6. the comment step after the floor step, its `if:` and two `env:` lines verbatim, no `hashFiles`, `continue-on-error: true`, no `${{` in its script, and `steps.floor.outputs` nowhere else in the job;
  7. one `readFileSync` in the comment step, after `if (computed) {`;
  8. the marker constant once; a `marker,` line ends both bodies (count 2); the finder holds `c.body.includes(marker)` and both `startsWith`; `github.paginate(github.rest.issues.listComments` and `per_page: 100`; no bare `await github.rest.issues.listComments(`;
  9. one `createComment`, on the line after `} else if (computed) {`;
  10. the job's `if:` from step 3, verbatim (`Get-JobLine`).
- **The existing fact changes in the same commit.** The reading names `:348` and `:380`; this design moves more. `:346-352`: the comment step's row leaves the reader loop (the SARIF row stays). `:366`, `:373`, `:379`: `core.setFailed(` becomes `noScore(`. `:380`: the anchor becomes `let results;`. `:383-384`: count `noScore\(` and `return;`, four each. `:358` stays as written.
- **On the lane.** The PR's own a11y-check run is green and leaves one ✅ comment ending in the marker. The failure paths are shown on a throwaway draft PR, branch `claude/cb05-probe` from `main`, titled `probe: do not merge (CB-5)`. Wait for each run to end before the next push (a push cancels the run in flight). Read only the a11y-check run; other lanes may go red on a probe. After each run record the run id and the bot comment's id, `updated_at` and first line.
  - **A** (main's workflow): add one comment line to `a11y-check.yml`. Green; the bot creates the ✅ comment, id K.
  - **B** (main's workflow): add a step after the discard step, `run: printf 'garbage' > a11y-results.json`. The defect, seen failing: the floor's log shows the uncaught error (quote it); K is untouched, still ✅.
  - **C**: take `a11y-check.yml` from the fix branch; keep the garbage step. The floor fails with the one "is not valid JSON" sentence; K reads "not computed" with it.
  - **D**: drop the garbage step; misspell the download `name`. The floor says the results could not be downloaded; K is rewritten, found by its marker alone.
  - **E**: restore the name; add a step `run: exit 1` to `route`. No mac runner. The score job runs; the floor says the analysis did not run; K is rewritten.
  - **F**: remove that step. Green; K is back to ✅ with the marker; one bot comment.
  - **G**: delete K (`gh api -X DELETE`), then repeat E's edit. No mac runner. The floor is red; the PR has no bot comment.
  - Then `gh pr close <probe> --delete-branch`. A, B, C, D and F each cost one analysis on a mac runner.

## Docs and registers

- `.github/workflows/a11y-check.yml`; `ci/windows-runner/tests/Workflows.Tests.ps1` (and its header comment, `:8`).
- No contract register, no plan document, no version (github-script keeps its SHA).

## Verification

```powershell
Import-Module Pester -RequiredVersion 5.9.1 -Force
./ci/windows-runner/tests/Invoke-Tests.ps1    # both facts red before the workflow edit, the suite green after
python -m unittest discover -s ci/mac-runner/route/tests -t ci/mac-runner/route    # green, unchanged
gh pr checks <N>    # a11y-check, windows-runner-tests, mac-runner-tests
gh api "repos/{owner}/{repo}/issues/<probe>/comments" --paginate --jq '.[] | select(.user.type=="Bot") | {id, updated_at, head: (.body | split("\n")[0])}'
```

CI lanes the PR starts: `a11y-check.yml`, `windows-runner-tests.yml`, `mac-runner-tests.yml`. No windows lane. If both mac runners are down, the lane proof waits (reading risk 9).

## Acceptance

- [ ] CD4 is answered. If "delete", the Design's last line applies, and the fact's name and pin 9 follow it.
- [ ] The new fact was red first and is green; the existing fact's edits are in the same commit; the suite was rerun after rebasing on CB-3.
- [ ] The PR's own a11y-check run is green, with one ✅ comment ending in the marker.
- [ ] The probe's seven run ids are in the PR body with what each showed (B the defect, C the sentence and the rewrite, D and E the rewrite, F the same comment back to ✅, G nothing created; under CD4 "delete", C to E show the comment deleted and F a new comment, by id). The probe is closed and its branch deleted.
- [ ] `Closes #1616`. `not delivered:` a live digest mismatch; a live run past 30 comments; a cancelled run; removal of old duplicate comments.
- [ ] "Found and fixed here" lists the two finds (results with no valid score reach the comment as a score or a soft throw; a JSON `null` throws in the floor), each with its pin.
- [ ] The body ends with the count: found on the way, fixed here, deferred, filed (2, 2, 0, 0 unless the work finds more).

## Brief

Read `.github/workflows/a11y-check.yml:205-451` and the `a11y-check.yml` fact in `ci/windows-runner/tests/Workflows.Tests.ps1` (`:325-386` at c2377210; helpers `:23-46`, `Test-Decided` `:548-554`); rebase on CB-3 first. Write the new Pester fact (Tests, pins 1 to 10) and the edits to the existing one, see them red, then build Design 1 to 3. The floor step gets `id: floor` and words every no-score outcome through `noScore`, with outputs `computed` and `reason`, the parse in `try`/`catch`, and the skipped-analysis branch. `Post score summary on PR` moves after it with `if: ${{ !cancelled() && github.event_name == 'pull_request' }}`, reads the two outputs through `env:`, reads the file only when computed, rewrites the bot's existing comment to "not computed" with the floor's sentence (or deletes it, under CD4 "delete"), creates nothing where there is none, ends every body with `<!-- slate-a11y-score -->`, and pages the finder. The job's `if:` lets a same-repo pull request with a skipped analysis run it. Correct the four comments that describe the old shape. Prove it with the PR's own run and probe pushes A to G, then close and delete the probe. The PR needs a mac runner in CI and goes on its tests alone. Done when the suite is green (red first), the three lanes are green on the PR, and the body has the run ids, `Closes #1616`, the `not delivered` lines, "Found and fixed here" and the count. Commit: `ci: a11y-check rewrites its comment when no score is computed, and malformed results fail in a sentence`.
