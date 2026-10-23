# XB-12 — Dependabot can run CI without reaching the owner's hardware

Issues: #1564 (`Refs` until the owner's Studio step; then closes). Size M (design-first). Lanes: windows-runner-tests (route and host tests), mac-runner-tests (the admission hook's tests), windows (the `ROUTE_ACTOR` plumbing). AT: —. Wave: X5, slot 2. Depends on: XD6. Parallel with: XB-10b, XB-13. Decision: XD6. Codex adversarial review per plan 44 §4 rule 5 (size M), on the amendment before code.

## Goal

A Dependabot pull request can trigger workflows again, its jobs run only on vendor runners (Namespace, GitHub-hosted), never on the Studio or the home pool, and `main`'s ruleset gates its merge like any other PR. The shape is written as a plan 42 amendment the owner approves before `open-pull-requests-limit` leaves zero. The one fact nobody has checked — whether GitHub's workflow execution policy can allow a Bot actor at all — is probed first.

## Current state on main

- **The policy.** Plan 42 §3.2 control 2 (`docs/plans/42_self_hosted_mac_runner_plan.md:131-141`): workflow execution policy 7070, `restrict_actions_actors` allowing only user 933688 and `restrict_action_events` (push, pull_request, workflow_dispatch, schedule); `:852-856` records its creation; §3.6 `:273-299` "Adding a person or tool" (`:287-291`: `apply-policy.sh` rewrites the allowed actors from `allowlist.json`; "a compromised main can widen the hook's list, but not the policy"). `ci/mac-runner/allowlist.json`: `actors` (`:8-10`, the owner), `candidates` (`:11-13`, the Codex bot), `hook_events`, `push_refs`, `policy_events` (`:5-7`); `_comment` (`:2`) names its two consumers: the controller streams it into every job VM for the hook, and `ci/mac-runner/github/apply-policy.sh` (`:21`, `:27` `restrict_actions_actors` ← `actors`). Runbook: `docs/runbooks/mac-self-hosted-runner.md:93` "Adding a person or tool".
- **The Studio hook.** `ci/mac-runner/hook/admission.py` `decide` (`:91-144`): repository id (`:100-103`), event (`:106`), `GITHUB_ACTOR_ID ∈ actor_ids` (`:109-111`), `GITHUB_TRIGGERING_ACTOR ∈ actor_logins` (`:112-115`), `sender.id` (`:116-118`), for `pull_request` head and base repo ids and the author id (`:120-131`). Tests `ci/mac-runner/hook/tests/test_admission.py` (`:104`, `:114`, `:121`).
- **The home pool.** `ci/windows-runner/route/route.py`: `is_fork(event, head_repo, repo)` (`:262-264`), `settle_early` sends a fork to the vendor in every mode (`:291-294`), `home-only` sends everything home (`:295-297`), inputs from `ROUTE_EVENT`/`ROUTE_HEAD_REPO`/`ROUTE_REPO` (`:515`; `windows.yml:244-245`); the docstring `:30-32` says routing is not security — the host's admission is. `ci/windows-runner/SlateCiHost.psm1`: `Test-TrustedRun` (`:451-456`, head repo equals `TrustedRepo`), `Update-SeenJob` marks `Foreign` (`:1221`), `Invoke-Admission` admits only non-foreign (`:1229-1254`), `Test-CommitEligible` (`:399-432`) for the cache commit; `config.json:29-31` `TrustedRepo`, `TrustedBranch`, `TrustedEvents`. No actor check anywhere on the Windows side: a Dependabot PR's head is the same repository, so today it would route home and be admitted. `flaui`'s `runs-on` inlines the same-repo test (`windows.yml:846`). Tests: `route/tests/test_route.py` (`:327`, `:333`, `:338`), `tests/SlateCiHost.Tests.ps1` (`:252`, `:692`, `:307`, `:311`), `tests/Orchestrator.Tests.ps1` (`:156`, `:167`, `:628`).
- **Mac lanes.** `swift-tests.yml:114` and `a11y-check.yml:83` run only same-repo PRs; their route (`needs.route.outputs.label`) can fall back to Namespace (plan 42 §6.1 D6).
- **Dependabot.** `.github/dependabot.yml` (`:1-13` the pause and why; five ecosystems each `open-pull-requests-limit: 0`, weekly, `cooldown: default-days: 7`; cargo ignores `:68-76`); `Workflows.Tests.ps1:276-293` holds every limit at zero. Security updates and alerts are off (`:23-24`). #1565's ruleset 25660813 is live (`pr gate`, strict up-to-date). A Dependabot PR's `GITHUB_TOKEN` is read-only and sees no secrets.
- Dependabot's actor: login `dependabot[bot]`, a Bot of app id 29110 (verify the actor id with `gh api users/dependabot%5Bbot%5D --jq .id`).

## Reference

Plan 42 §3 (threats, controls, §3.6); the issue's decision ("a separate policy actor list from the hook's; Dependabot runs routed to vendor runners only; required checks on `main`"); GitHub's workflow execution policy API (`restrict_actions_actors` parameters).

## Design

**Step 0 — the probe (before the amendment is final).** With the owner (it edits a repository setting): read policy 7070 (`gh api` per `apply-policy.sh`), and in a dry run check whether `restrict_actions_actors.allowed_actors` accepts an actor of type `Bot` with Dependabot's id. If the API rejects Bots, the redesign cannot work through the policy; the amendment then records that Dependabot stays off and this chunk closes with that record (`Refs #1564` and a comment). If it accepts, continue.

**Step 1 — the amendment** (`42_self_hosted_mac_runner_plan.md`, new §3.7 "Bots that open PRs", dated; reviewed by codex and the owner before step 2):
- `allowlist.json` gains `"policy_only_actors": [{ "id": <dependabot id>, "login": "dependabot[bot]", "type": "Bot", "note": "version updates; never admitted to the Studio or the home pool" }]`. `apply-policy.sh` writes `actors ∪ policy_only_actors` into the policy; the hook reads `actors` only, so the Studio denies a Dependabot run at admission as it does today (`test_admission.py` pins it).
- Routing: a Dependabot-actor run goes to the vendor on every lane. Windows: `route.py` gains `is_bot_actor(actor, bots)` and `settle_early` treats it like a fork (vendor in every mode, `home-only` included); `windows.yml` passes `ROUTE_ACTOR: ${{ github.actor }}` and `ROUTE_TRIGGERING_ACTOR: ${{ github.triggering_actor }}`; `flaui`'s `runs-on` expression adds `&& github.actor != 'dependabot[bot]'` to its home condition. Host side, belt and braces: `SlateCiHost.psm1` `Test-TrustedRun` also requires the run's `actor.login` and `triggering_actor.login` not in `config.json`'s new `PolicyOnlyActors`, so such a run is `Foreign` and never admitted even if a workflow named the home label. Mac lanes: their `if:` stays same-repo (a Dependabot PR is same-repo), and the route must send a Dependabot run to Namespace: read plan 42 §6.1's route step and add the actor condition there too.
- Merge gate: unchanged (ruleset 25660813, `pr gate`); the Dependabot PR waits for its lanes like any other; the owner merges it with the pinned command after reading the diff, per AGENTS.md.
- The Studio: unchanged code path (the hook denies); the amendment says the owner re-runs `apply-policy.sh` and confirms the policy's actor list, which is the step this plan cannot do.
- Re-enabling: a separate, later PR restores `open-pull-requests-limit` to #1541's values with the `Workflows.Tests.ps1` zero-pin flipped, only after the owner's step and one hand-triggered Dependabot run (`gh api -X POST …/dependabot/updates` or the UI) has shown its runs on vendor runners only.

**Step 2 — the code (this chunk, after the amendment is approved):** the `allowlist.json` field and `apply-policy.sh` union; `route.py` + `windows.yml` + `SlateCiHost.psm1` + `config.json`; the mac route condition; `dependabot.yml` comments updated to point at §3.7 (limits stay zero); `Workflows.Tests.ps1` facts.

## Tests

- `route/tests/test_route.py`: `test_a_policy_only_actor_routes_to_the_vendor_in_every_mode` (home, overflow, home-only), `test_the_triggering_actor_is_checked_too` (a re-run of a Dependabot PR by the owner: still vendor, because the head's author is the bot — decide and pin which actor fields count; the amendment says both).
- `tests/SlateCiHost.Tests.ps1` / `Orchestrator.Tests.ps1`: `It 'treats a run by a policy-only actor as foreign and never admits it'`, `It 'treats a run re-run by the owner on a policy-only actor's PR as foreign'`.
- `ci/mac-runner/hook/tests/test_admission.py`: `test_policy_only_actor_is_denied_by_the_hook_even_though_the_policy_allows_it` (the hook reads `actors` only); `test_validate_allowlist_accepts_policy_only_actors`.
- `apply-policy.sh`: a shell test (beside `phase1-settings.sh`'s tests, if any; else a `scripts/tests/` python test that imports the JSON-building step) proving the union.
- `Workflows.Tests.ps1`: `It 'windows.yml passes ROUTE_ACTOR and ROUTE_TRIGGERING_ACTOR to the router'`, `It 'the flaui job never runs on the home pool for dependabot'`, the existing zero-limit fact unchanged.

## Docs and registers

- `docs/plans/42_self_hosted_mac_runner_plan.md` §3.7 (new, dated) and a line in §8 owner decisions.
- `docs/runbooks/mac-self-hosted-runner.md:93` "Adding a person or tool": a paragraph on policy-only actors.
- `.github/dependabot.yml` header comments.
- #1564: the probe's result, the amendment link, the owner's remaining step.

## Verification

```powershell
gh api users/dependabot%5Bbot%5D --jq '{id, type}'
# step 0 with the owner: read policy 7070; dry-run the allowed_actors shape with a Bot entry (never apply without the owner)
python -I -m unittest discover -s ci/windows-runner/route/tests
python -I -m unittest discover -s ci/mac-runner/hook/tests
Import-Module Pester -RequiredVersion 5.9.1 -Force; ./ci/windows-runner/tests/Invoke-Tests.ps1
gh pr checks <N>      # windows-runner-tests, mac-runner-tests, windows
```

CI lanes: `windows-runner-tests.yml`, `mac-runner-tests.yml`, `windows.yml`. AT: none.

## Acceptance

- [ ] Step-0 probe result recorded on #1564 (Bot actors accepted, or the redesign stopped there).
- [ ] §3.7 amendment reviewed and approved; the code matches it; every new fact green (red first).
- [ ] Limits still zero; the owner's step (apply the policy, one hand-triggered run on vendor runners) named as what closes #1564.
- [ ] `Refs #1564`. Not delivered: the Studio policy application and the re-enable PR (the owner's step, then a follow-up PR).

## Brief

Read `docs/plans/42_self_hosted_mac_runner_plan.md:97-300`, `:576-625`, `:841-860`, `:1202-1240`; `ci/mac-runner/allowlist.json`; `ci/mac-runner/github/apply-policy.sh`; `ci/mac-runner/hook/admission.py:20-30`, `:61-144`; `ci/mac-runner/hook/tests/test_admission.py:95-130`; `ci/windows-runner/route/route.py:15-35`, `:255-300`, `:505-520`; `ci/windows-runner/route/tests/test_route.py:320-345`; `ci/windows-runner/SlateCiHost.psm1:395-460`, `:1190-1260`; `ci/windows-runner/config.json`; `ci/windows-runner/tests/SlateCiHost.Tests.ps1:250-320`, `:690-700`; `tests/Orchestrator.Tests.ps1:150-175`; `.github/workflows/windows.yml:230-250`, `:842-848`; `.github/dependabot.yml`; `ci/windows-runner/tests/Workflows.Tests.ps1:276-293`; `docs/runbooks/mac-self-hosted-runner.md:93-130`. Step 0 with the owner: can policy 7070 allow a Bot actor? Record on #1564; stop if not. Step 1: write §3.7 per Design (policy-only actors in `allowlist.json`, hook unchanged, vendor-only routing by actor on Windows and mac lanes, host-side `Foreign` by actor, ruleset unchanged, re-enable only after the owner's step); codex review; owner approval. Step 2: the code and facts listed under Tests; `dependabot.yml` comments; limits stay zero. Done when the three test suites and the three lanes are green and §3.7 is on `main`; `Refs #1564`. Commit: `ci: dependabot runs are routed to vendor runners only, by a policy-only actor list the admission hook never reads`.
