# Plan 47 reading 02 — CI workflows, runner images, and one comments-only issue

Baseline read: worktree `<worktree>` at `1ec9bf31`.
Issues: #1647, #1699, #1903, #1614, #1616, #1709. All six are OPEN with zero comments
(`gh issue view`, 2026-11-13), so each issue file is the whole issue.

How to read this report:

- A `file:line` fact was read in the worktree unless it is marked **unverified**.
- "Run 43364663470" is `main`'s `windows.yml` run on the baseline commit `1ec9bf31`
  (2026-11-13 04:18Z). I read two of its job logs with read-only `gh run view --log`.
- `main` has moved one commit past the baseline (`c2377210`, #1927). It changes three
  files under `apps/slate-windows/tests/SlateWindows.Tests/` and none in this cluster.
- Nothing in the repo was edited, built or tested. One read-only Python script of mine
  (in the scratchpad) imported `scripts/a11y_trigger_ledger.py` and called its `render()`
  on data rebuilt from the committed document; see #1699.

Shorthand: `WT` = `ci/windows-runner/tests/Workflows.Tests.ps1` (847 lines),
`GT` = `ci/windows-runner/tests/Golden.Tests.ps1` (426 lines). Both run in
`windows-runner-tests.yml` on hosted `ubuntu-latest` under Pester 5.9.1
(`windows-runner-tests.yml:78-104`). That lane is cheap; `windows.yml` is the dear one.

Which lanes a file starts (each lane's `on.push.paths`, read in the worktree):

| File changed | Lanes started |
|---|---|
| `.github/workflows/windows.yml` | windows (all jobs), windows-runner-tests |
| `.github/workflows/a11y-check.yml` | a11y-check (analysis on a mac runner), windows-runner-tests, mac-runner-tests |
| `ci/windows-runner/**` (not `route/`) | windows-runner-tests only |
| `global.json` | windows, windows-runner-tests |
| `scripts/**`, `docs/plans/38_notification_dispatcher_contracts.md` | windows |
| `.github/actions/windows-native-build/**`, `windows-ci-pilot.yml` | windows-ci-pilot (pull requests), windows-runner-tests |
| `apps/slate-mac/**/*.swift` | swift-tests, a11y-check, windows (its list has `apps/slate-mac/**`, `windows.yml:167`), license-headers |
| `apps/slate-windows/**/*.cs` | windows, license-headers |

---

## #1647 — the FlaUI binaries keep one day

- **Verdict:** stands.
- **Current state:**
  - `.github/workflows/windows.yml:835-846`: the app job's `Upload FlaUI gate binaries` step,
    `name: flaui-binaries-${{ github.run_id }}`, `overwrite: true`, `retention-days: 1` at `:846`.
  - `windows.yml:825-834`: the comment over it; `:832-833` says "within the artifact's one-day
    retention".
  - `windows.yml:1126-1130`: the `flaui` job downloads the same name; the job runs only when
    `needs.windows.result == 'success'` (`:1117`).
  - `.github/workflows/windows-ci-pilot.yml:242-252`: the pilot's producer artifact,
    `retention-days: 7` at `:252`, with the comment the issue quotes at `:249-251`.
  - `WT:204-222` (`Describe 'windows.yml FlaUI handoff'`, added by #1649) pins the name, the
    `overwrite`, and that neither step names `run_attempt`. It pins nothing about retention.
  - No commit after #1649 (`923497ab`) touches the step (`git log --grep 1647`, `--grep 1643`).
  - **Unverified:** GitHub's 30-day rerun window, and the 65 MB artifact size (both from the issue).
- **Cause:** the retention was chosen for a handoff inside one attempt. #1643 then named the
  artifact by run id so a gate-only rerun could reuse it, and the retention was not revisited.
- **Fix shape:** in `windows.yml`, `retention-days: 1` becomes `7` (the pilot's value and the app
  results artifact's, `:823`), and the comment at `:832-833` says seven days and what to do after
  (re-run all jobs). In `WT`, a second fact in the FlaUI handoff `Describe`.
- **Failing test first:** `WT`, `Describe 'windows.yml FlaUI handoff'`, new fact, for example
  `It 'keeps the binaries seven days, as long as the app job's own results, so a rerun of the gate alone finds them after the first day'`.
  It pins `retention-days: 7` inside the one upload step (`Get-Steps` + `Get-Lines`). Red on
  `main` today. To end the class rather than the instance, the fact can sweep every workflow:
  each artifact a later job downloads by name or pattern is uploaded with no `retention-days`
  or with at least 7. That sweep is green for every other handoff today (list in the cluster
  section), so it costs nothing extra.
- **Size:** S.
- **Lanes:** windows (the changed step runs in the app job; the PR body quotes the run),
  windows-runner-tests (the fact).
- **Owner step:** none.
- **Owner decision needed:** none. The issue names the pilot's 7. For the record: 7 still leaves
  a gate-only rerun failing on days 8 to 30; 30 would cover GitHub's whole rerun window at about
  four times the storage. I recommend 7.
- **Shares files with:** #1699, #1903 (job half) and #1614 (runtime half) through `windows.yml`;
  #1699, #1616 and #1614 (runtime half) through `WT`.

---

## #1699 — CI never checks contract 38's trigger ledger against its generator

- **Verdict:** stands.
- **Current state:**
  - `scripts/a11y_trigger_ledger.py` (145 lines) has `--write` and `--check` (`:120-121`). It reads
    `docs/plans/18_windows_port/a11y_trigger_designations.json` (`:28`) and rewrites the block
    between `<!-- a11y-trigger-ledger:start -->` and `:end -->` in
    `docs/plans/38_notification_dispatcher_contracts.md` (`:29-31`; the block is lines 814-1175
    of that document, 238 key rows).
  - "The census it reads": `inventory()` (`:40-51`) runs
    `dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-restore --configuration Release --filter FullyQualifiedName=SlateWindows.Tests.Censuses.A11yTriggerParityCensus.SourceInventoryCanBeExportedForTheLedgerGenerator`
    with `SLATE_A11Y_INVENTORY_PATH` set. That fact is
    `apps/slate-windows/tests/SlateWindows.Tests/Censuses/A11yTriggerParityCensus.cs:323-334`;
    the inventory itself is `Support/A11yTriggerInventory.cs` (keys sorted ordinal, `:28`).
    Note the command has `--no-restore` but not `--no-build`: after the Build step it does an
    up-to-date build check, then runs one fact.
  - No workflow names the script (`grep` over `.github/`: the only hits for the three generators
    are `windows.yml:692` and `:694`, the two WB-17 checks).
  - The census compares sites as a set: `CoverageFailures` uses `Except`
    (`A11yTriggerParityCensus.cs:42-50`), called on each row's sites at `:78`. Order is never compared, as the issue says.
  - Where the WB-17 checks sit: `windows.yml:679-680` Build; `:682-686` comment; `:687-690`
    `Set up Python (the reconciliation checks)` (setup-python v7.0.0, 3.13); `:691-692` contract 34
    `python -I scripts/canvas_reconciliation.py --check`; `:693-694` contract 35; `:695-696` the
    unit test `test_reconciliation_check_flags_a_stale_pointer.py`. Merged as `2822a0a4` (#1700).
  - Nothing in `WT` pins those WB-17 steps (`grep reconciliation|ledger|parity` in `WT`: no hits).
    A deleted `--check` step reddens no test today.
- **Is the committed ledger in step today?** As far as reading can tell, yes. I rebuilt an
  inventory from the committed table's own cells, ran the generator's `render()` on it with the
  committed designations file, and compared: the output equals the committed block byte for byte,
  and no Mac or Windows cell is out of the generator's sort order (0 of 238 rows). That covers
  the drift the issue describes (hand-placed order), the static text, the designation column and
  the residue tables. **Unverified:** that the committed site set and key order equal the real
  inventory, which needs the Release build. The census checks the set in CI, and it is green on
  `main`, so the first real `--check` should pass; the spec should still say what to do if it
  does not (run `--write`, commit the diff in the same PR, name it in the body).
- **Cause:** the generator got a `--check` mode but no CI step, and the census that guards the
  ledger is set-based by design.
- **Fix shape:**
  1. `windows.yml`, job `windows`: one step after `:693-694`, before `:695`:
     `- name: Verify contract 38's trigger ledger is the generator's output` /
     `run: python -I scripts/a11y_trigger_ledger.py --check`. No `if:`, no `continue-on-error:`.
     It must come after Build (`:679`) and after the Python setup (`:687`), which is exactly
     where the issue's "after the Build step" puts it. Widen the comment at `:682-686` to name
     contract 38.
  2. `scripts/a11y_trigger_ledger.py`: on a stale ledger, print the differing rows (a unified
     diff of the block) before `SystemExit` (`:136`). Today the message is one sentence with no
     diff. A Mac-only contributor cannot run the generator (it needs the Windows Release build),
     so the lane log has to show the rows to copy. This is the main risk of the change; see Risks.
  3. A permanent unit test beside the WB-17 one: `scripts/tests/test_a11y_trigger_ledger_check_flags_a_reordered_row.py`,
     run by a step after `:695-696`. It works on a temp copy of the document with a temp
     inventory file (`--inventory`, the script's own shortcut, `:122`), swaps two sites in a
     multi-site Windows cell, and asserts `--check` exits non-zero and `--write` repairs it. The
     model is `scripts/tests/test_reconciliation_check_flags_a_stale_pointer.py:34-55`. `main()`
     reads `sys.argv` and the module global `DOCUMENT`, so the test patches both (or `main` gains
     an `argv` parameter, as the reconciliation scripts have).
  4. `WT`: the fact below.
- **Failing test first:** `WT`, new `Describe 'windows.yml generated tables'`, fact
  `It 'checks contracts 34, 35 and 38's generated tables against their generators after the build, unconditionally, and the parity matrix in the model job'`.
  It pins, in the `windows` job: the three `run:` lines verbatim, each step's keys being only
  `name` and `run`, all three after `- name: Build` and before `- name: Test`; and
  `python3 scripts/generate-parity-matrix.py --check` in `windows-model` (`windows.yml:1096-1097`).
  Red on `main` (no contract 38 step). The issue's own "proven to fail on a hand-reordered row,
  probe removed afterwards" is done twice: by the unit test in step 3 (permanent), and by one
  throwaway commit on a scratch branch that swaps two sites in the committed ledger and shows the
  new step red on the lane, as `AGENTS.md` "Gotchas" asks. The PR body quotes both runs.
- **Size:** S.
- **Lanes:** windows (the new step in the app job), windows-runner-tests.
- **Owner step:** none.
- **Owner decision needed:** none.
- **Shares files with:** #1647, #1903 (job half), #1614 (runtime half) through `windows.yml`;
  #1647, #1616, #1614 (runtime half) through `WT`.

---

## #1903 — the home golden image arms WER dumps

- **Verdict:** stands, with one correction. The issue says "the `Install.Tests.ps1`/provisioning
  tests pin the new keys". The provisioning tests are `GT` (`Describe 'golden scripts'`, `GT:83`).
  `Install.Tests.ps1` covers `install/setup-host.ps1`, `store-token.ps1` and `prune-golden.ps1`
  on the host, not the image.
- **Current state:**
  - `windows.yml:721-731` comment, `:732-747` step `Arm Windows Error Reporting dumps for the test
    processes (#1826)`: for `SlateWindows.Tests.exe`, `testhost.exe`, `dotnet.exe` it writes
    `HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\<exe>` with `DumpFolder`
    (ExpandString, the job's `${{ runner.temp }}\slate-app-results`), `DumpType` 2, `DumpCount` 4;
    on failure it prints `::warning::WER dumps are not armed for … (#1903)` (`:745`) and goes on.
    Merged as `c9af6261` (#1902).
  - `windows.yml:763-777` Test; `:801-811` stop the sampler; `:815-823` upload
    `slate-windows-app-results-<run>-<attempt>` (`retention-days: 7`).
  - `apps/slate-windows/tests/SlateWindows.Tests/Censuses/TestHostCrashDumpCensus.cs:53-77` pins the
    arm step (key path, the three names, `DumpFolder`, `DumpType 2`, the warning, armed before
    Test); `:88-98` fails if the job gains any secret or token.
  - `ci/windows-runner/golden/provision-guest.ps1:153-156`: task `slate-runner-logon` runs as
    `runner`, `-RunLevel Limited` (`:155`). The script itself runs elevated as `provision`
    (header `:4-8`). It writes nothing under `Windows Error Reporting` (`grep` over
    `ci/windows-runner` and the runbook for `LocalDumps|Error Reporting|WerSvc`: no hits).
  - `ci/windows-runner/golden/build-golden.ps1:219-232` already verifies registry state in the
    sealed disk's SOFTWARE hive (`Use-GoldenHive … -Copy`), and `GT:388-401` pins those checks by
    text. This is the precedent for checking the new keys at build time.
  - `WINDOWS_RUNNER_MODE` is `namespace` today (`gh variable get`, 2026-11-13). The home pool is
    not serving. In run 43364663470 the app job ran on a Namespace runner and printed
    `WER dumps armed for` all three names.
- **Cause:** the keys are under HKLM; the job runs as a limited user on a home VM, so only the
  image build can write them, and the image build does not.
- **Fix shape (two halves, one issue):**
  - *Image half.* `provision-guest.ps1`: create a fixed folder (for example `C:\slate-dumps`),
    grant `runner` modify on it with a checked `icacls` (the file's own pattern, `:138-147`), and
    write the three `LocalDumps\<exe>` keys with `DumpFolder` = that folder, `DumpType` 2,
    `DumpCount` 4. `build-golden.ps1`: in the sealed-disk verification (`:219`), throw unless the
    three keys hold those values and the folder exists. `docs/runbooks/self-hosted-windows-runner.md`:
    one paragraph under "Refreshing the golden image" (`:1237`) and the probe as a new check.
  - *Job half.* `windows.yml`: the arm step keeps today's path when it can write the keys. When it
    cannot, it reads them (a limited user can read HKLM): if all three exist with `DumpType` 2 it
    says the image armed them and records their `DumpFolder`; otherwise today's warning. A new step
    after Test and before the upload, `if: ${{ always() }}`, moves `*.dmp` from that folder into
    `${{ runner.temp }}\slate-app-results`. On Namespace it is a no-op. The comment at `:727-731`
    is updated. `TestHostCrashDumpCensus.cs`: the arm fact gains the read-back branch, and a new
    fact pins the collect step (always, between Test and Upload, into the uploaded folder).
  - The folder name and the three executable names now live in two files
    (`provision-guest.ps1`, `windows.yml`). Pin their agreement in `GT`, not in the C# census:
    `windows-runner-tests.yml` starts for both files, while `windows.yml` does not start for
    `ci/windows-runner/golden/**`.
- **Failing test first:**
  - `GT`, `Describe 'golden scripts'`: `It 'phase 1 arms Windows Error Reporting dumps for the three
    test processes into a fixed folder runner can write, and the build checks the keys in the
    sealed disk'` (text pins on `provision-guest.ps1` and `build-golden.ps1`), and
    `It 'the app job collects from the folder the image arms, for the same three processes'`
    (the cross-file agreement).
  - `TestHostCrashDumpCensus`: `TheAppJobCollectsDumpsTheImageArmedIntoItsUploadedResults` and the
    extended `TheAppJobArmsWindowsErrorReporting…`.
  - Plan 45 §4 rule 2 says a `windows.yml` change is pinned in `WT`. Here the step's pins already
    live in the C# census, and the cross-file pin fits `GT`. The plan should say that either home
    counts, or this chunk adds a third file to the `WT` queue for no gain.
- **Size:** M for the whole issue (each half is S; the probe run and the runbook make a second day).
- **Lanes:** windows-runner-tests (`GT`); windows (the changed steps and the census, on Namespace:
  proves the admin path still arms and the collect step is a no-op).
- **What an agent can deliver and prove, and what it cannot:**
  - Can: all the code above; both test sets red then green; the Namespace path on the PR's own run;
    and, on a scratch branch, a probe that pre-seeds the three keys to the fixed folder (as the
    image would), runs a process named `dotnet.exe` that calls `RaiseFailFastException`, and shows
    the dump moved into the uploaded artifact. That proves the read-back and the collection.
  - Cannot: that WER writes the dump for a *limited* user into the fixed folder, on a *rebuilt*
    image. That is the issue's "Done when". **Unverified** until the owner's run.
- **Owner step:** (1) a golden rebuild, runbook `:1276-1329` (drain, pull `main`, `setup-host.ps1`,
  `build-golden.ps1 -IsoPath … -Rotate` with the Windows product key, the product-key check,
  re-enable the loop, a `home-only` dispatch). (2) One probe dispatch of `windows.yml` in
  `home-only` mode on a branch that fails fast natively in the app job; confirm a `.dmp` in
  `slate-windows-app-results-<run>-<attempt>`; post the run on #1903; delete the branch. (3) Set
  `WINDOWS_RUNNER_MODE` back to what it was (`namespace` today). The pool is off today, so this
  step also means bringing the host loop back up.
- **Owner decision needed:** *When does #1903 close?* Its "Done when" is the home-VM probe, which
  only the owner can run, and the pool is off. Recommended: the PR merges with `Refs #1903` and
  `not delivered: golden rebuild and the home-VM probe`; the plan's close-out carries one
  "owner steps owed" list (the same shape as the Mac ledger in brief §6); the issue closes when the
  owner posts the probe run. Closing at merge would break the brief's anti-goal ("a fix nobody saw
  fail and then pass").
- **Shares files with:** #1614 (image half: `provision-guest.ps1`, `GT`, the runbook section);
  #1647, #1699, #1614 (runtime half) through `windows.yml`; #1596 (group C) through the same
  golden rebuild.

---

## #1614 — the .NET runtime CI tests on, and the golden image's SDK, are outside the cooldown

- **Verdict:** stands, both parts. Nothing after `eaa3c20b` (#1615, the SDK pin) changes either.
- **Current state — which versions are in play today, and where each is pinned or floats:**

  | Thing | Value today | Where it is set | Pinned or floating |
  |---|---|---|---|
  | SDK CI uses | 10.0.402 | `global.json:3`, `rollForward: patch` (`:4`) | Pinned. Six `setup-dotnet` steps read it, each followed by a `Record the .NET SDK` step that fails on a mismatch: `windows.yml:664`/`:667`, `:1026`/`:1029`, `:1133`/`:1136`; `windows-ci-pilot.yml:275`/`:278`, `:459`/`:462`; `.github/actions/windows-native-build/action.yml:20`/`:23`. Pinned by `WT:245-310` (count 6 at `WT:292`). |
  | `actions/setup-dotnet` | v6.0.0, SHA `a98b5685…` | the six steps above | Pinned. |
  | Runtime `setup-dotnet` adds | **10.0.15** | not in the repo | **Floats.** At that SHA, `src/installer.ts:411-425` runs `dotnet-install -Runtime dotnet -Channel LTS` before the SDK install, with no input to turn it off (read with `gh api`). Run 43364663470: the app job (Namespace) and the shell gate (`windows-latest`) each log `Downloaded … dotnet-runtime-10.0.15-win-x64.zip` / `Installed version is 10.0.15`. |
  | Runtime the pinned SDK bundles | 10.0.13 | follows `global.json` | **Unverified** (the issue's number; 10.0.402 is not installed here). Consistent with this machine: SDK 10.0.404's `Microsoft.NETCoreSdk.BundledVersions.props` says 10.0.15, 10.0.303's says 10.0.11. |
  | Runtime the tests bind to | newest `Microsoft.NETCore.App` 10.0.x in the install root, so 10.0.15 | the host's default roll-forward; no `RollForward`, `RuntimeFrameworkVersion` or `runtimeconfig.template.json` anywhere under `apps/slate-windows` (`grep` over every csproj, props and targets) | **Floats.** **Unverified** by a log line: no step prints `dotnet --list-runtimes`. |
  | `Microsoft.WindowsDesktop.App` (the app is `net10.0-windows`) | whatever the install root holds | — | **Unverified, likely mixed.** The `-Runtime dotnet` install brings only `Microsoft.NETCore.App`, so WPF probably stays on the SDK's own patch while the core framework moves. The fix's first step should print it. |
  | Install root | `%ProgramFiles%\dotnet` on hosted and Namespace runners (the action's default, `installer.ts:337-347`); `C:\dotnet` on a home VM (`DOTNET_INSTALL_DIR`, `provision-guest.ps1:83`) | — | On `windows-latest` the root also holds the image's own runtimes. That image floats too: run 43364663470's shell gate ran on `windows-2025-vs2026` 20261103.298.1. |
  | Golden image SDK | newest SDK of channel `10.0` on the rebuild day | `ci/windows-runner/golden/versions.json:6` `"dotnetChannel": "10.0"`; `provision-guest.ps1:79-81` (`dotnet-install.ps1 -Channel $versions.dotnetChannel -InstallDir 'C:\dotnet'`); key list at `GT:18`; runbook `:1243-1250` | **Floats.** A rebuild today would bake a newer SDK than the pin (10.0.404 exists: it is installed on this machine), and every home job would download 10.0.402 again. |
  | Local dev (this machine) | SDK 10.0.404, runtime 10.0.15 | `rollForward: patch` rolls a missing 10.0.402 forward | Not CI; noted because the fix may move CI's runtime *below* what developers run. |

  The issue's line references still hold: `provision-guest.ps1` "~:80" is `:80`; `installer.ts`
  "~:411-425" is exact.

- **Cause:** #1568 pinned the SDK only. The action installs the newest LTS runtime by design, the
  host rolls forward to the newest patch by default, and the image installs by channel.
- **What "pinned past the 7-day cooldown" concretely means:**
  - *Runtime.* The repo names one runtime version, and every CI job proves it ran on that version
    and no newer one. The simplest rule has one pin and one date: **the runtime is the one the
    pinned SDK bundles** (10.0.13 for 10.0.402, to be confirmed). The check is a line in the
    `Record the .NET SDK` step: the newest `Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App`
    10.0.x that `dotnet --list-runtimes` shows must equal the expected version, written as a literal
    in the repo and pinned by a `WT` fact.
  - *`versions.json`.* `"dotnetChannel": "10.0"` becomes `"dotnetVersion": "<global.json's sdk.version>"`;
    `provision-guest.ps1:80` calls `dotnet-install.ps1 -Version $versions.dotnetVersion`; `GT` gains
    `It 'pins the .NET SDK the repository pins'` beside the Rust and bindgen twins (`GT:40-47`), the
    key list at `GT:18` changes, and a fact pins that provisioning never passes `-Channel`. The
    image then bakes the pin, with its bundled runtime, and a home job downloads no SDK.
    (`dotnet-install.ps1` itself stays an evergreen, signature-checked download, `GT:135-147`.)
- **Dates the implementing PR must read** (from the .NET release list; I looked none up):
  1. The release date of SDK 10.0.402. Plan 45's XB-4a spec records 2026-10-13
     (`xb04a_…md:26`); re-read it.
  2. The runtime version 10.0.402 bundles, and that it shipped the same day.
  3. Only if the PR moves the pin: the release dates of every newer 10.0.4xx SDK (10.0.403 if it
     exists, 10.0.404) and of the runtimes they bundle (10.0.14, 10.0.15), to pick the newest pair
     at least 7 days old when the PR opens. The issue expected a servicing release on 2026-11-10;
     if that is 10.0.15, CI has run on it since its first day.
  4. For the image half: none new when `dotnetVersion` equals the existing pin. If #1596's runner
     bump rides in the same PR (see cluster section), the `actions/runner` release date too.
  The PR body lists each version with its date, as `AGENTS.md` "Git and PRs" requires.
- **Fix shape:**
  - *Image half (S).* `versions.json`, `provision-guest.ps1:77-81`, `GT`, runbook `:1239-1250`.
  - *Runtime half (M; a measurement first).* Step 0, on a scratch branch: add
    `dotnet --list-runtimes` to the Record step and read what Namespace, `windows-latest` and (if
    it is up) a home VM resolve. Then one of three mechanisms, which is the owner decision below.
    Whichever is chosen touches all six setup steps and their Record steps (`windows.yml`,
    `windows-ci-pilot.yml`, the composite action) and `WT:245-310`, because `WT` pins the Record
    step's lines exactly (`WT:258-262`, `:284-289`).
  - Not in this fix: moving the SDK pin. `SlateWindows.Tests.csproj:31-32` ties
    `Microsoft.CodeAnalysis.CSharp` 5.9.0 to "the compiler SDK 10.0.402 bundles"; a pin bump is a
    PR of its own.
- **Failing test first:**
  - Image half: the three `GT` facts above. Red on `main`.
  - Runtime half: `WT`, `Describe 'the .NET SDK pin'`: `It 'every job that sets up .NET fails unless
    the newest 10.0 runtime it can load is the one the pinned SDK bundles'` (the Record body, in
    all six places, count still 6), plus whatever pins the chosen mechanism. On the lane, the proof
    is the Record step's line in each job's log, and a scratch-branch run where the check is red
    with today's 10.0.15 present.
- **Size:** image half S; runtime half M.
- **Lanes:** image half: windows-runner-tests only. Runtime half: windows (three jobs),
  windows-ci-pilot (the action and the pilot file are in its pull-request paths),
  windows-runner-tests. `nightly.yml:165-177` uses the composite action and does not run on a PR:
  `not delivered: nightly`, as XB-4a recorded.
- **What an agent can deliver and prove, and what it cannot:** the image half's files and `GT`
  facts, fully. That a rebuilt image holds exactly the pinned SDK and a home job downloads nothing
  is the owner's rebuild plus one `home-only` run (the Record step and the `setup-dotnet` log
  show it). The runtime half is provable on Namespace and `windows-latest` by the PR's own runs;
  on the home pool only after that same rebuild.
- **Owner step:** the golden rebuild (runbook `:1276-1329`), shared with #1903.
- **Owner decision needed:** *How strictly is the runtime held?* Three options:
  1. **Recommended: no `setup-dotnet`.** Each job installs the pinned SDK's zip itself, by exact
     URL and a hash committed in the repo, into a root private to the job, and exports
     `DOTNET_ROOT`. The run 43364663470 log shows the URL shape
     (`…/dotnet/Sdk/10.0.402/dotnet-sdk-10.0.402-win-x64.zip`). Nothing newer than the pin is
     downloaded or run, which is what the cooldown is for, and it matches how `versions.json`
     pins Git, PowerShell and the runner. Costs: M; six steps become repo-owned script; the
     `flaui` job has no checkout (`windows.yml:1124-1130`), so the script travels inline or in
     the artifact; a home VM uses `C:\dotnet` when it already holds the pin.
  2. Keep `setup-dotnet`, then delete the newer shared frameworks from the install root and
     assert. Smaller, but the newest release's `dotnet.exe` and `hostfxr` still run on their first
     day, and on `windows-latest` it deletes from the image's own install.
  3. Record an owner exception in `AGENTS.md` (a runtime patch is Microsoft's security servicing),
     and only print the runtime in each job. Smallest; it does not do what the issue asks.
  Second question, smaller: *does #1614 close at merge of its last half, with the rebuild on the
  "owner steps owed" list?* Recommended yes; its "Expected" is about the repository, and its note
  already says the image change takes effect at the next rebuild.
- **Shares files with:** #1903 (`provision-guest.ps1`, `GT`, runbook, and `windows.yml` if both
  halves of each land together); #1647 and #1699 (`windows.yml`, `WT`); #1616 (`WT`);
  #1596 (`versions.json`).

---

## #1616 — a11y-check's stale score comment, and the stack trace on malformed results

- **Verdict:** stands, both parts. The last change to the file's score job is `36049113` (#1617,
  XB-5), which the issue postdates.
- **Current state** (`.github/workflows/a11y-check.yml`, 451 lines):
  - `:262-325` step `Post score summary on PR`. Its guard at `:263` is
    `github.event_name == 'pull_request' && hashFiles('a11y-results.json') != ''`, so it skips with
    no file. It finds the bot's comment by heading: `:302-307` match a body starting
    `## ✅ Accessibility score` or `## ❌ Accessibility score`. `:325` `continue-on-error: true`.
  - `:327-418` step `Enforce score floor`. Missing file: three plain sentences, `:340-361`.
    `:362` `const results = JSON.parse(fs.readFileSync(resultsPath, 'utf8'));` with no `try`.
  - `:243-250` the discard step from #1575; `:227-234` the download.
  - `WT:325-386` (`Describe 'a11y-check.yml'`) pins the #1575 shape. Two of its lines pin text
    this fix must change: `WT:348` pins the comment step's exact `if:` (with `hashFiles`), and
    `WT:380` anchors a regex on the literal `const results = JSON.parse`.
- **Corrections and additions to the issue:**
  - The comment step has its own unguarded `JSON.parse` at `:268`. With malformed JSON it throws
    too, softly, so the stale ✅ also stays in the malformed case. The issue lists only the
    missing-file cases under part 1.
  - A fourth stale case: the whole `a11y-check` job is skipped when the analysis was skipped
    (`:211`, `needs.analyze.result != 'skipped'`), which is what happens when `route` fails. The
    gate (`:426-451`) is red and the old ✅ stays.
  - `listComments` at `:297-301` reads one page (30 comments). On a longer PR the old comment is
    not found and a second one is created beside it.
  - **Unverified:** that github-script prints a stack trace for an uncaught throw (the issue's
    wording; I did not read a failing run).
- **Cause:** the comment step is guarded on the file's existence, so it has no "no score" path;
  and the two steps parse the file separately with no error handling.
- **Fix shape** (all in `a11y-check.yml`, score job only):
  1. Floor step: `JSON.parse` in `try`/`catch`; the catch fails with the issue's sentence,
     "a11y-results.json is not valid JSON (<message>); no score was computed. See the analysis job."
  2. One place decides the sentence. Recommended: the floor step gets an `id`, and sets outputs
     `computed` and `reason` before each `setFailed`. The comment step moves after it, runs with
     `if: ${{ !cancelled() && github.event_name == 'pull_request' }}`, reads the two outputs
     through `env:` (never expanded inside the script), and parses the file only when
     `computed == 'true'`. It keeps `continue-on-error: true`. The floor step stays first-class:
     no `if:`, no `continue-on-error:` (`WT:358`).
  3. When no score was computed and the bot's comment exists, the comment step rewrites it to a
     "not computed" heading plus the floor's sentence and a link to the run. It creates nothing
     when there is no earlier comment.
  4. The finder matches a hidden marker (for example `<!-- slate-a11y-score -->`) as well as the
     two old headings, and pages through every comment. Without this the next good run cannot find
     the "not computed" comment and posts a second one.
  5. Optional, same file: let the score job run when a same-repo analysis was skipped, so the
     fourth case is covered. If not taken, the PR body names it.
  The #1575 property is untouched: the discard step still deletes unverified files before either
  step runs.
- **Failing test first:** `WT`, `Describe 'a11y-check.yml'`:
  `It 'when no score is computed, the comment step rewrites the bot's comment with the floor's
  sentence, and malformed results fail in one plain sentence'`. It pins: the floor's `JSON.parse`
  inside a `try` whose `catch` calls `core.setFailed` with "is not valid JSON" and returns; the
  floor's `id` and its two outputs; the comment step's new `if:`, its place after the floor, its
  `env:` reads, and that it does not read the file unless `computed`; the marker in the finder and
  in every body it writes. Red on `main`. The existing fact is edited at `WT:348` and `WT:380`
  in the same change.
  Proof on the lane (plan 45 §4 rule 2): the PR's own run shows the normal comment. A throwaway
  draft PR shows the two failure paths: push 1 normal (✅), push 2 with a wrong download `name`
  (the comment becomes "not computed"), push 3 writing garbage into `a11y-results.json` after the
  download (the plain sentence, no stack trace), push 4 restored (the same comment returns to ✅).
  Then the probe is deleted. XB-5 used the same kind of probe.
- **Size:** S.
- **Lanes:** a11y-check (route, the analysis on a mac runner, the score job), windows-runner-tests
  (`WT`), mac-runner-tests (it lists `a11y-check.yml`; `ci/mac-runner/route/tests/test_workflows.py`
  reads the file's `run:` blocks and pins nothing this fix changes). No windows lane.
- **Owner step:** none.
- **Owner decision needed:** *Update or delete?* The issue allows either. Recommended: update the
  existing comment (it keeps one comment per PR and says why); never create a "not computed"
  comment where there was none.
- **Shares files with:** #1647, #1699 and #1614 (runtime half) through `WT`. Nothing else here
  touches `a11y-check.yml`.

---

## #1709 — two mac comments still name the old `windows.graph.setVerbosity*` rows

- **Verdict:** stands. Comment-only, in two Swift files.
- **Current state:**
  - `apps/slate-mac/Sources/SlateMac/SlateCommands.swift:278-282`: the doc comment over the three
    ids. The stale words are at `:280-282`: "Tag-true, the closed set the Windows chord table
    records as `windows.graph.setVerbosity*` (contract 35 C-9)." The issue's `:281` is the line
    with the old name.
  - `apps/slate-mac/Tests/SlateMacTests/CommandRegistryTests.swift:350-351`: "The rows are the
    closed set the Windows chord table records as `windows.graph.setVerbosity*` (C-9)." The
    issue's `:351` is exact.
  - The correct names, on both hosts: `slate.graph.setVerbosityTerse`, `…Standard`, `…Verbose`.
    Mac: `SlateCommands.swift:283-285`. Windows: `Commands/ChordTable.cs:344-346`,
    `apps/slate-windows/chords.json:1779`, `:1791`, `:1803`.
    `GraphPreferencesTests.cs:255-256` asserts no row starts with `windows.graph.setVerbosity`.
  - The citation the issue asks for exists: `docs/plans/35_graph_contracts.md:9517-9520`,
    "*Addendum 2026-10-30 (WB-21, #1415):* the three levels are registered palette rows on both
    hosts". (The frozen text above it, `:9499-9500` and `:10681`, still says `Unreg`; it is a frozen
    record with an addendum, and is not part of this issue.)
  - `CommandRegistryTests.swift:362` ("Tag-true, as Windows' rows are") is still true. No test
    reads either comment.
- **Cause:** WB-21 (plan 44) left the two comments out on purpose, so a Windows PR would not start
  the mac lanes for comments alone.
- **Fix shape:** both comments say the Windows chord table registers the same three ids
  (contract 35 C-9, addendum 2026-10-30). Two files, comments only.
- **Failing test first:** none is possible; a comment has no behaviour. The PR says so. The proof
  is that the swift lane still compiles the two files and `grep -rn "windows.graph.setVerbosity" apps/slate-mac`
  is empty.
- **Size:** S (minutes).
- **Lanes:** swift-tests, a11y-check, windows (the full lane) and license-headers all start for a
  `.swift` change. That is the cost to avoid: alone, two comment lines buy a full Windows run.
- **Owner step:** none.
- **Owner decision needed:** none.
- **Shares files with:** none in this cluster. See the cut for which PR carries it.

---

## Cluster section

### Same-defect instances found

**Artifact kept for less than the rerun window, and downloaded by a later job**

| Where | Retention | Consumer | Note |
|---|---|---|---|
| `windows.yml:846` `flaui-binaries-<run>` | 1 day | `flaui` job, `:1126-1130` | #1647 |
| `windows-ci-pilot.yml:252` `windows-pilot-binaries-<run>-<attempt>` | 7 | model shards and shell, via `.github/actions/windows-pilot-binaries/action.yml:26-29` | under 30; a dispatch-only pilot |
| `windows-ci-pilot.yml:349` `windows-pilot-model-<run>-<shard>` | 14 | `:386-390` | under 30; pilot |
| `mac-ci-pilot.yml:97`, `:109`, `:148`, `:160` | 14 | `:179-182` | mac; out of scope |
| `windows.yml:1070` `windows-model-<run>-<shard>` | 30 | `:1100-1103` | fine |
| `a11y-check.yml:193-201` `a11y-results` | none set (the repository default; **unverified**) | `:227-231` | fine |

Only the first is shorter than the week the repo already treats as the floor. Evidence-only
artifacts (nothing downloads them) are not this defect: `windows.yml:823` (7), `:1184` (30),
`nightly.yml:197` (7).

**A generated file with a generator and no `--check` in CI, and close relatives**

1. Contract 38's ledger: #1699.
2. `scripts/canvas_trigger_ledger.py` writes the table that is "pasted into"
   `docs/plans/34_canvas_contracts.md` §H (its docstring, `:18-20`). It has no `--check` or
   `--write` mode (only `--report`, `:277`) and no CI step. `CanvasTriggerParityCensus` validates
   the table; **unverified** how strictly (I read only its parser).
3. `scripts/graph_trigger_ledger.py`, the same for `docs/plans/35_graph_contracts.md` §F
   (`:17-19`, `--report` at `:333`).
4. `scripts/tests/test_canvas_trigger_ledger.py` is run by no workflow and no Makefile target
   (every other `scripts/tests/test_*.py` is named in a workflow). A test nobody runs.
5. `scripts/check_windows_docs_trigger.py:66`: `_SCRIPT_RUN` matches only `python3 scripts/…`.
   The WB-17 checks run as `python -I scripts/…` (`windows.yml:692`, `:694`), so the docs-trigger
   census does not scan them, and would not scan the new contract 38 step. No path is missing
   today (contracts 34, 35, 38 and `18_windows_port/**` are all in the lane's list,
   `windows.yml:155-161`), but the census is blind to that spelling.
6. No `WT` fact pins the two WB-17 `--check` steps or the parity-matrix `--check`
   (`windows.yml:1096-1097`).
7. Has a check, for completeness: `apps/slate-windows/chords.json` (`ChordTableTests.cs:265`),
   the parity matrix, `Cargo.lock` (`--locked`), `dotnet format`.

Items 5 and 6 are small and sit in #1699's own files or beside them; I would take them in that
PR. Items 2 to 4 need a `--check` mode written for two generators; that is a chunk, not a ride-along.

**A toolchain version that floats**

| What | Where | State |
|---|---|---|
| .NET runtime | the six `setup-dotnet` steps | #1614 |
| Golden image SDK | `versions.json:6`, `provision-guest.ps1:80` | #1614 |
| Golden image Visual Studio Build Tools | `versions.json:17` (`https://aka.ms/vs/17/release/vs_BuildTools.exe`), `provision-guest.ps1:70-75` | Evergreen: a rebuild installs that day's MSVC. Signature-checked only (`GT:135-147`). Same class as #1614's image half; not filed. |
| `dotnet-install.ps1` | `provision-guest.ps1:79` | Evergreen script, signature-checked. |
| Python patch | `python-version: '3.13'` in 11 steps (`windows.yml:690`, `:789`; `windows-ci-pilot.yml:57`, `:384`; `windows-runner-tests.yml:110`; `audit.yml:110`; `mac-runner-tests.yml:60`; `mac-ci-pilot.yml:54`, `:76`, `:127`, `:177`) | Floats to the newest 3.13.x a runner has or fetches; the home image bakes 3.13.16 (`versions.json:7`). |
| Hosted runner images | `windows-latest` (`windows.yml:1118`, `nightly.yml:165`, the pilot), `ubuntu-latest` (every aggregate and census job) | Float by label; `windows-latest` was `windows-2025-vs2026` 20261103.298.1 on 2026-11-13. A label can pin the major only. |
| Namespace profile image | `namespace-profile-winx64-fast[-pr]` | Dashboard-configured; **unverified** what it carries. Run 43364663470 found SDK 10.0.402 "already installed" there. |
| `actions/setup-python` v7.0.0, runner 2.338.0 | all 11 steps; `versions.json:2-3` | Pinned, but plan 45's XB-6 (#1596, group C) meant to move both and has not landed (`git log --grep 1596`: only the plan commit). |

Pinned and fine: every `uses:` is a 40-character SHA (one, `a11y-check.yml:257`, has its version
in a comment three lines up rather than on the line); Rust 1.99.1; `cargo-nextest@0.9.149`;
`cargo-audit@0.22.2`; Pester 5.9.1; `uniffi-bindgen-cs` by tag with `--locked`; the a11y analyzer
by commit; Xcode by build.

### Recommended cut into PRs

`windows.yml` is touched by four of the six issues and `WT` by four. Under "same-wave PRs share
no files" they cannot run side by side, so this cluster is one agent working a serial chain, not
three agents. Every PR below is small enough for that.

| Order | PR | Closes | Files | Lanes | Why together |
|---|---|---|---|---|---|
| 1 | `ci(windows)`: the shell gate's binaries keep seven days, and the lane checks contract 38's ledger | #1647, #1699 | `windows.yml` (app job only), `WT`, `scripts/a11y_trigger_ledger.py`, one new `scripts/tests/` file, `scripts/check_windows_docs_trigger.py` and its test (item 5) | windows, windows-runner-tests | Same job, same two files; one full Windows run pays for both. Only the ledger step can redden the lane, so a red run still names its cause. No decision needed. |
| 2 | `ci`: a11y-check rewrites its comment when no score is computed, and malformed results fail in a sentence | #1616 | `a11y-check.yml`, `WT` (the a11y `Describe`) | a11y-check, windows-runner-tests, mac-runner-tests | Alone: different workflow, different lanes. After PR 1 only because of `WT`. |
| 3 | `ci(windows)`: the next golden image arms WER dumps and bakes the pinned SDK, and the app job collects the dumps | `Refs #1903`, `Refs #1614` | `provision-guest.ps1`, `build-golden.ps1`, `versions.json`, `GT`, the runbook, `windows.yml` (the WER steps), `TestHostCrashDumpCensus.cs` | windows-runner-tests, windows | Both image changes share three files and wait on the same owner rebuild; one rebuild puts both in force. #1903's job half rides here so the issue is one PR. |
| 4 | `ci(windows)`: every job that sets up .NET runs the runtime the pinned SDK bundles | `Closes #1614` | `windows.yml` (three setup and Record steps), `windows-ci-pilot.yml`, the composite action, `WT` | windows, windows-ci-pilot, windows-runner-tests | Kept apart from PR 3 for the reason plan 45 split XB-4a from XB-4b: it can redden every Windows job, and it waits on an owner decision and a measurement. |
| — | #1709 | #1709 | two Swift files | (see below) | Not a PR of its own. |

- **Order.** 1 and 2 are first-wave work (the brief's step 2) and need no decision. 3 needs the
  #1903 closing decision. 4 needs the #1614 mechanism decision and is the only M; it can sit in a
  later wave without blocking anything.
- **If the author wants PR 1 and PR 2 in parallel:** the only shared file is `WT`. Either take
  plan 45's stated exception ("each adds a fact; same-wave pairs rebase, never hand-merging",
  plan 45 §6) or sequence them. I recommend sequencing; both are under a day.
- **#1709.** Carry it in the first plan 47 PR that already starts the swift and windows lanes
  (any `crates/slate-core` change does). There it adds only the a11y-check and license-headers
  lanes. If it must close in the first wave, PR 1 carries it, at the price of a swift-tests run.
- **#1596 (group C, not mine to plan).** Its part (b), the runner bump in `versions.json`, was
  written by plan 45 to ride "in the PR that prepares the next golden rebuild". PR 3 is that PR.
  The audit should either hand it to PR 3 or say why not; otherwise the owner rebuilds twice.
- **Owner steps, in one place:** one golden rebuild after PR 3 (and after PR 4 if the mechanism
  changes what the image needs), then one `home-only` run and #1903's probe. Nothing else in the
  cluster needs the owner's hands. No ruleset change: no job or check name is added or renamed.

### Risks

1. **The ledger check will catch Mac contributors.** The ledger lists Mac sites too. A mac PR
   that adds or removes an `A11yEvent` construction must update it, and cannot run the generator
   without the Windows Release build. Today the census (a set) accepts a hand-placed row; after
   PR 1 the Windows lane goes red unless the row is in the generator's order. Mitigations: the
   diff output in fix step 2, and one line in `AGENTS.md` "Gotchas" beside the parity-matrix line.
   That `AGENTS.md` line collides with the plan's first chunk, which also edits `AGENTS.md`: put
   it in that chunk or sequence the two.
2. **The ledger check runs a test outside the device-state guard.** The script's `dotnet test`
   (one fact, `a11y_trigger_ledger.py:44-46`) runs before the guarded Test step and outside
   `guard-device-state.ps1`. The fact reads sources and writes one temp file, so it should not
   touch `%LOCALAPPDATA%\Slate`, but nothing checks that. It also adds an up-to-date build pass
   to the app job; **unverified** how many seconds.
3. **#1614's runtime half moves CI's runtime backwards** (10.0.15 to 10.0.13 if the pin stays).
   CI would then test on an older patch than developers and users run. That is the cooldown
   working as written, but the owner should see it before choosing.
4. **What runs today is partly unknown.** No log line shows which `Microsoft.NETCore.App` and
   `Microsoft.WindowsDesktop.App` the test processes load, on any pool. PR 4's first step is to
   print it; its design may change with the answer.
5. **#1903 cannot be finished by an agent,** and the home pool is off. If the owner does not plan
   to bring the pool back, PR 3's WER half is code that waits. The decision row should offer
   "park until the pool returns" as an honest third answer.
6. **WER for a limited user is unproven.** `LocalDumps` is machine-wide, and the image-armed
   `dotnet.exe` key applies to every lane's VM, not only the app job's. Each VM is discarded after
   one job, so stray dumps go with it; the no-secrets guard
   (`TestHostCrashDumpCensus.cs:88-98`) still governs what is published.
7. **`WT` is the cluster's bottleneck.** Three of four PRs edit it, and PR 2 and PR 4 edit
   existing facts, not only add new ones. A hand-merge there can silently drop a pin; rebase and
   rerun the suite each time.
8. **Seven days is a bound, not a cure.** A gate-only rerun on day 8 still fails with
   "Artifact not found". The comment in the workflow should say what to do then.
9. **The a11y probe needs a mac runner in CI** (the Studio or Namespace's). "No Mac available"
   in the brief means no local Mac; if both runners are down, PR 2's lane proof waits.
10. **Cooldown.** PRs 1 and 2 bring in no version. PR 3 brings in none if `dotnetVersion` equals
    the existing pin; a runner bump riding with it needs its date. PR 4 brings in none unless the
    pin moves, and then every date in #1614's list applies.
