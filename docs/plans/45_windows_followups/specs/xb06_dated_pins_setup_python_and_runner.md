# XB-6 — setup-python and the golden image's runner move to their cooled releases

Issues: #1596. Size S (two small PRs). Lanes: XB-6a touches `audit.yml`, `mac-ci-pilot.yml`, `mac-runner-tests.yml`, `windows-ci-pilot.yml`, `windows-runner-tests.yml` (each runs on the PR by its own path filter; the mac pilot runs on the Studio route); XB-6b touches `ci/windows-runner/golden/versions.json` (windows-runner-tests' `Golden.Tests.ps1`). AT: —. Wave: dated — on or after 2026-10-29 02:56Z (setup-python) and 14:14Z (runner), in any free slot. Depends on: XB-0 (the cooldown rule on `main`). Decision: none.

## Goal

Nine `actions/setup-python` pins move to v7.1.0's commit SHA, and the Windows golden image's runner moves to 2.338.1 with its SHA256, each in its own PR once its release is seven days old, each checking the release list for a newer release that is also past the cooldown.

## Current state on main

- **setup-python**, all `actions/setup-python@5fda3b95a4ea91299a34e894583c3862153e4b97  # v7.0.0` with `python-version: '3.13'`: `audit.yml:99`; `mac-ci-pilot.yml:52`, `:74`, `:125`, `:175`; `mac-runner-tests.yml:44`; `windows-ci-pilot.yml:55`, `:373`; `windows-runner-tests.yml:84`. v7.1.0 published 2026-10-22 02:56Z; the tag pointed at `6d4fd22b20465ccc0f7354476588e8df7c9ca0e0` when the issue was written (re-read at open).
- **Runner**: `ci/windows-runner/golden/versions.json:2-3` `"runnerVersion": "2.338.0"`, `"runnerSha256": "f48e0750…"`; read by `provision-guest.ps1:47` and used at `:102` (`actions-runner-win-x64-{0}.zip` with `-Sha256`); `Golden.Tests.ps1:15-24` pins every key present and each hash `^[0-9a-f]{64}$`, `:106` pins the provision line's shape. 2.338.1 published 2026-10-22 14:14Z. The rebuild is the owner's (D7).
- Pin comments follow `#1539`'s rule: `# vX.Y.Z` names the exact tag the SHA is.

## Reference

AGENTS.md "Dependencies" (after XB-0): release dates from the release list; "latest" means newest past the cooldown at open.

## Design

**XB-6a (actions), on or after 2026-10-29 02:56Z.**
1. `gh release list -R actions/setup-python --limit 5` with dates; pick the newest release ≥7 days old (v7.1.0, or newer if one qualifies). `gh api repos/actions/setup-python/git/ref/tags/<tag>` → the commit SHA (dereference an annotated tag through `git/tags/<sha>` if `type` is `tag`).
2. Replace all nine `uses:` SHAs and their `# v7.0.0` comments; nothing else in those files.
3. PR body: the tag, its SHA, its publish date and the open date (the cooldown arithmetic), per AGENTS.md.

**XB-6b (runner), on or after 2026-10-29 14:14Z.**
1. `gh release view v2.338.1 -R actions/runner` (or the newest ≥7 days old): the win-x64 asset's SHA256 from the release notes' table.
2. `versions.json:2-3` updated; `Golden.Tests.ps1` passes unchanged (format pins).
3. PR body: the date arithmetic; "the rebuild is the owner's (D7)". The image does not change until the owner rebuilds; the PR is the preparation the issue asks for. If the owner has a golden-prep PR open at the time, this change rides it instead.

## Tests

- XB-6a: the five workflows' own runs on the PR (each lists its file in its paths). `Workflows.Tests.ps1:244`'s pin-shape pattern covers checkout only; no new fact (a SHA pin's correctness is the release list, not a shape).
- XB-6b: `Golden.Tests.ps1` (`windows-runner-tests.yml`).

## Docs and registers

- The five workflow files (XB-6a); `versions.json` (XB-6b).
- `docs/runbooks/` Windows runner runbook: nothing unless it quotes the runner version (grep `2.338.0`).

## Verification

```powershell
# XB-6a
gh release list -R actions/setup-python --limit 5
gh api repos/actions/setup-python/git/ref/tags/v7.1.0 --jq '.object'
Select-String -Path .github/workflows/*.yml -Pattern 'actions/setup-python@' | Measure-Object   # 9, all the new SHA
gh pr checks <N>
# XB-6b
gh release view v2.338.1 -R actions/runner --json body --jq .body | Select-String 'win-x64'
Import-Module Pester -RequiredVersion 5.9.1 -Force; ./ci/windows-runner/tests/Invoke-Tests.ps1
Select-String -Path docs/runbooks/*.md -Pattern '2\.338\.0'
```

CI lanes: XB-6a — `audit.yml`, `mac-ci-pilot.yml`, `mac-runner-tests.yml`, `windows-ci-pilot.yml`, `windows-runner-tests.yml`; XB-6b — `windows-runner-tests.yml`. AT: none.

## Acceptance

- [ ] XB-6a: nine pins on the chosen tag's SHA with matching comments; the five lanes green; dates in the PR.
- [ ] XB-6b: `versions.json` on the chosen runner with its win-x64 SHA256; `Golden.Tests.ps1` green; the rebuild named as the owner's.
- [ ] `Closes #1596` on whichever of the two merges second (the first writes `Refs #1596`). Not delivered: the golden rebuild (D7).

## Brief

Not before 2026-10-29. XB-6a: read `gh release list -R actions/setup-python --limit 5`; take the newest release ≥7 days old; resolve its commit SHA (`git/ref/tags`, dereferencing an annotated tag); replace the nine `actions/setup-python@5fda3b95…  # v7.0.0` lines (`audit.yml:99`, `mac-ci-pilot.yml:52,74,125,175`, `mac-runner-tests.yml:44`, `windows-ci-pilot.yml:55,373`, `windows-runner-tests.yml:84`) with the new SHA and `# v<tag>`; PR body with the publish and open dates; wait for the five lanes. Commit: `ci: every setup-python pin runs v7.1.0`. XB-6b (after 14:14Z): read the win-x64 SHA256 from `gh release view v2.338.1 -R actions/runner`; update `ci/windows-runner/golden/versions.json:2-3`; Pester green; PR body names the rebuild as the owner's (D7); if a golden-prep PR is open, add it there. Commit: `ci(windows): the next golden image runs actions runner 2.338.1`. The second to merge carries `Closes #1596`.
