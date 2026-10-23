# XB-4a — Every CI `dotnet` runs the SDK `global.json` pins

Issues: #1568. Size S. Lanes: windows (all four jobs), windows-ci-pilot, nightly (the composite action), windows-runner-tests (the Pester fact). AT: —. Wave: X1, slot 3. Depends on: nothing. Parallel with: XB-1, XB-3. Decision: none. Interlock: plan 44's WB-17 adds two steps to the `windows` job after `Build` (`:523-527` in plan 44's numbering, now `:540-560`); this chunk edits the `Set up .NET` steps, different lines; XB-4a lands first. XB-4b (the package pins) waits for this merge.

## Goal

`apps/slate-windows/global.json` (`10.0.402`, `rollForward: latestFeature`) is what every CI `dotnet` resolves, because every `actions/setup-dotnet` step reads it with `global-json-file`, including the FlaUI job that has no checkout. A `dotnet --info` line in each job's log says which SDK ran, and a Pester fact keeps every step on the file.

## Current state on main

- `apps/slate-windows/global.json:1-6` pins `10.0.402`; `dotnet` honours it only when run from `apps/slate-windows` or below. Every CI `dotnet` runs from the repository root.
- `actions/setup-dotnet@a98b568…  # v6.0.0` with only `dotnet-version: '10.0.x'`:
  - `.github/workflows/windows.yml:535-538` (job `windows`, checkout at `:428-434`); `:761-764` (job `windows-model-shard`, checkout `:654-660`); `:849-852` (job `flaui`, **no checkout**: `steps:` at `:848` begins with it; the header `:22-26` and the comment `:860-864` say the job needs no checkout; the artifact download follows at `:854-858` to `path: apps/slate-windows`, and `dotnet test` runs the DLL at `:865-871`).
  - `.github/workflows/windows-ci-pilot.yml:273-276` (`model-shard`, checkout `:268-272`) and `:446-449` (`shell`, checkout `:442-445`).
  - `.github/actions/windows-native-build/action.yml:19-22` (composite; used by `windows-ci-pilot.yml:117` and `nightly.yml:172`).
- The artifact the FlaUI job downloads is built from `apps/slate-windows/src/SlateWindows/bin/Release/**`, `tests/SlateWindows.AccessibilityTests/bin/Release/**`, `tools/GridConformanceHost/bin/Release/**` (`windows.yml:572-582`), so `global.json` is not in it.
- The golden image installs the SDK by channel (`ci/windows-runner/golden/provision-guest.ps1:80`, `versions.json:6` `"dotnetChannel": "10.0"`); on the home pool setup-dotnet finds whatever `C:\dotnet` holds and installs the pinned version into the tool cache if it differs (a few seconds, once per VM).
- `ci/windows-runner/tests/Workflows.Tests.ps1` (294 lines) loads `windows.yml`, `nightly.yml`, `windows-ci-pilot.yml`, `windows-runner-tests.yml` (`:15-18`); helpers `Get-JobBlock` `:19`, `Get-JobLine` `:34`, `Get-Lines` `:40`, `Get-Steps` `:42`; a pin-matching example at `:244`. It runs under `windows-runner-tests.yml` (`:74-80`, Pester 5.9.1 via `Invoke-Tests.ps1`), whose path lists (`:11-25`, `:28-42`) include the three workflows but not `.github/actions/**`.

## Reference

actions/setup-dotnet's `global-json-file` input (reads `sdk.version`, installs that version). The issue's alternative ("stop describing the file as a CI pin") is rejected: the pin is cheap and the Roslyn note in `SlateWindows.Tests.csproj:31-32` depends on it.

## Design

1. **Every setup step reads the file.** In all six places, `with:` becomes `global-json-file: apps/slate-windows/global.json` and the `dotnet-version` line is removed (both together would install two SDKs). The composite action resolves the path relative to the workspace, which both callers check out first (`windows-ci-pilot.yml:117`'s job and `nightly.yml:172`'s job: confirm each has a checkout before the action; they do, or the action's own step fails loudly).
2. **The FlaUI job gets the file without a checkout.** Add `apps/slate-windows/global.json` to the upload list at `windows.yml:577-580`; the download at `:854-858` recreates it at `apps/slate-windows/global.json`. Reorder the job: `Download FlaUI gate binaries` first, `Set up .NET 10` second (the download needs no SDK). Update the comment at `:860-864` ("no project file or source … except `global.json`, carried in the artifact").
3. **A witness line.** A step `Record the .NET SDK` after each setup (`dotnet --info | Select-String '^ Version:|Base Path' | Select-Object -First 2` on Windows jobs; the composite action gains one) so the PR body can quote `10.0.402`.
4. **Pester.** `Workflows.Tests.ps1`: load `.github/actions/windows-native-build/action.yml` too (`:15-18`), and a `Describe 'setup-dotnet pins'` with `It 'every setup-dotnet step reads apps/slate-windows/global.json and names no version'`: for each loaded file, every `uses: actions/setup-dotnet@` must be followed within its step by `global-json-file: apps/slate-windows/global.json` and never by `dotnet-version:`; and `It 'the flaui job downloads the artifact before it sets up .NET, and the artifact carries global.json'` (`Get-Steps` order; the upload list contains the path). `windows-runner-tests.yml`'s two path lists gain `.github/actions/windows-native-build/action.yml`; its own path-filter fact (`:200-205`) is extended if it enumerates the list.
5. Not changed: `global.json` itself, `versions.json` (the image's channel; a follow-up note for the owner's next golden rebuild is in the PR body: pin `dotnetVersion` there too so home VMs never download), `dotnet-version` semantics anywhere else.

## Tests

- The Pester facts above (red first: they fail on `main`'s six steps).
- The lanes themselves: `windows.yml` all four jobs green with `10.0.402` in each `Record the .NET SDK` step; `windows-ci-pilot.yml` and `nightly.yml` are not triggered by this PR's paths unless their files change (`windows-ci-pilot.yml` is: its own file is in its paths `:7-10`), so the pilot runs on the PR; nightly is proved by the composite action's shared shape and the next scheduled run (name it as `not verified on this PR` in the body).

## Docs and registers

- `.github/workflows/windows.yml`, `windows-ci-pilot.yml`, `.github/actions/windows-native-build/action.yml`, `windows-runner-tests.yml` (paths), `Workflows.Tests.ps1`.
- `docs/runbooks/` self-hosted Windows runner runbook: one line that CI pins the SDK through `global.json` and the image's channel install is a cache, not the pin (find the SDK paragraph with `grep -n "dotnet" docs/runbooks/*windows*`).

## Verification

```powershell
Import-Module Pester -RequiredVersion 5.9.1 -Force
./ci/windows-runner/tests/Invoke-Tests.ps1                     # the new facts red on main, green after
python -I scripts/check_windows_docs_trigger.py                # windows.yml's two path lists still identical
gh pr checks <N>                                               # windows.yml four jobs + windows-ci-pilot + windows-runner-tests green
gh run view <windows run id> --log | Select-String 'Version: 10.0.402'   # one hit per job
```

CI lanes: `windows.yml`, `windows-ci-pilot.yml`, `windows-runner-tests.yml`. AT: none.

## Acceptance

- [ ] Six setup-dotnet steps read `global.json`; none names a version; the FlaUI job downloads first and the artifact carries the file.
- [ ] Each Windows job's log shows `Version: 10.0.402`; quoted in the PR.
- [ ] Pester facts green; `windows-runner-tests.yml` watches the composite action's file.
- [ ] `Closes #1568`. Not delivered: nightly's run (next schedule), named in the body.

## Brief

Read `.github/workflows/windows.yml:416-440`, `:530-585`, `:633-665`, `:755-770`, `:842-875`; `.github/workflows/windows-ci-pilot.yml:110-120`, `:265-280`, `:440-452`; `.github/actions/windows-native-build/action.yml`; `.github/workflows/nightly.yml:165-178`; `.github/workflows/windows-runner-tests.yml:9-45`; `ci/windows-runner/tests/Workflows.Tests.ps1:1-50`, `:94-113`, `:200-212`, `:240-250`. Build: every `actions/setup-dotnet` step's `with:` is `global-json-file: apps/slate-windows/global.json` with no `dotnet-version`; `windows.yml:577-580` adds `apps/slate-windows/global.json` to the FlaUI artifact; the `flaui` job downloads before it sets up .NET (comment at `:860-864` updated); a `Record the .NET SDK` step (`dotnet --info` filtered to `Version:` and `Base Path`) after each setup, in the composite action too; `Workflows.Tests.ps1` loads the action file and gains `Describe 'setup-dotnet pins'` with the two facts above; `windows-runner-tests.yml` paths gain the action file. Done when Pester is green (red first on `main`), `check_windows_docs_trigger.py` passes, `windows.yml`'s four jobs, `windows-ci-pilot.yml` and `windows-runner-tests.yml` are green on the PR, and each Windows job's log shows `Version: 10.0.402` (quoted). PR body notes nightly as not run and suggests `dotnetVersion` in `versions.json` for the next golden. `Closes #1568`. Commit: `ci(windows): every setup-dotnet step reads apps/slate-windows/global.json, so ci runs the sdk the repository pins`.
