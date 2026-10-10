# Self-hosted Windows Runner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run every Windows CI job for `coryj627/slate` in a throwaway Hyper-V VM on the owner's desktop, with provenance-gated cache merges and a one-variable fallback to today's Namespace and `windows-latest` providers.

**Architecture:** A PowerShell orchestrator (`scripts/ci-host/`) runs on the host as an unprivileged Hyper-V Administrator, polls the repository's queued `slate-win-*` jobs, and for each one clones a VM from a read-only golden Windows 11 disk plus a copy-on-write fork of that lane's cache disk, hands a single-use JIT runner config into the guest over Hyper-V KVP, and after the guest shuts itself down merges the cache fork only when GitHub's record shows a green push/schedule/dispatch on `main` of this repository. All GitHub and Hyper-V calls sit behind adapter scriptblocks so the state machine is unit-tested with fakes. Workflows select the pool with one repository variable.

**Tech Stack:** PowerShell 7.6 on the host (module written 5.1-compatible because the guest runs Windows PowerShell), Pester 5 on `ubuntu-latest`, Hyper-V (Gen2, vTPM, extended port ACLs, NetNat), GitHub REST (`generate-jitconfig`, runs/jobs), DISM `Expand-WindowsImage` for the golden disk, GitHub Actions YAML.

**Spec:** `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`

## Global Constraints

- Lane labels are exactly `slate-win-rust`, `slate-win-app`, `slate-win-model`, `slate-win-shell`; a job must carry exactly one.
- Runner names are `slate-win-<lane>-<8 hex>`.
- Two VM slots, 4 vCPU and 12 GB static each; slot IPs `10.77.0.11` and `10.77.0.12`; gateway `10.77.0.1`; NAT prefix `10.77.0.0/24`; switch name `slate-ci`.
- Per-VM extended ACLs deny `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `100.64.0.0/10`, `169.254.0.0/16`, `::/0`, both directions; allow everything else.
- KVP values are at most 1,000 characters (Hyper-V MAXLEN is 1024); keys `slate.jit.count`, `slate.jit.<n>`, `slate.lane`, `slate.ip`, `slate.gateway`, `slate.dns`, `slate.cache`, `slate.job`.
- Commit predicate: job `conclusion == success`, run `event ∈ {push, schedule, workflow_dispatch}`, `head_branch == main`, `head_repository.full_name == coryj627/slate`, guest shut down by itself, parent generation unchanged since fork.
- Lane caps: rust 70 min, app 100 min, model 100 min, shell 30 min (lane `timeout-minutes` + 10, app covers the 90-min nightly stress job); `shell` has no cache disk.
- Heartbeat wait 180 s; unclaimed-runner check at 300 s; retry cap 3 with 600 s back-off; tick 10 s.
- Cache parents are dynamic VHDX, 60 GB maximum, NTFS, volume label `slate-cache`, generation counter in `<lane>.gen` next to the parent.
- Host paths: `C:\slate-ci\{bin,golden,cache,vms,state,logs}`; golden disk `C:\slate-ci\golden\win11-runner.vhdx`.
- Host account `slate-ci-host`: member of Hyper-V Administrators only, denied interactive logon; PAT stored as `C:\slate-ci\state\token.xml` via `Export-Clixml` under that account.
- Guest: standard user `runner`, auto-logon, runner at `C:\actions-runner` (2.338.0, sha256 `f48e0750a21812bca5f82de5f7f5aeae71abee647fab5a582f1742d07eba455f`), .NET at `C:\dotnet`, rustup 1.97.1 with `aarch64-pc-windows-msvc`, uniffi-bindgen-cs `v0.11.0+v0.31.0`, Python 3.13.15, Git 2.55.0.5.
- Workflows read `vars.SLATE_WINDOWS_POOL`; `namespace` reproduces today's `runs-on` verbatim; anything else (including unset) means `home`.
- New `.ps1` files carry the SPDX header used by `apps/slate-windows/generate-bindings.ps1`; commit messages follow the repo's `type(scope): summary` style.
- Nothing in the module may use PowerShell 7-only syntax (`??`, ternary, `-Parallel`); `-AsHashtable` is allowed only in host-only functions (journal, config).

## Review Focus

1. A job carrying two lane labels (`slate-win-app` and `slate-win-model`) must be ignored, never admitted, and never crash discovery (Task 2 test "two lane labels").
2. A JIT config whose length is an exact multiple of 1,000 characters, or empty, must round-trip through chunking with the exact count (Task 2 tests "boundary" and "empty").
3. A run whose `head_repository` is null (fork deleted after the PR) must discard with a reason rather than throw (Task 4 test "null head_repository").
4. A corrupted or truncated `journal.json` must not stop the orchestrator: it is moved aside and a fresh journal starts (Task 5 test "corrupt journal").
5. A GitHub API error during settle must leave the Off VM in the journal for the next tick instead of orphaning it or committing blindly (Task 8 test "settle retries after API error").

---

### Task 1: Spec amendments from implementation review

**Files:**
- Modify: `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`

Three details changed when the guest scripts were designed: the standard `runner` user cannot write the guest-to-host KVP registry key or configure networking, so the guest uses two tasks (SYSTEM at startup for network, disk and config; `runner` at logon for the runner itself) and the host decides "shut down cleanly" from its own knowledge of whether it forced the stop; the golden disk is built by applying `install.wim` with DISM rather than running Setup from an ISO; the cache disk is found by volume label, not an assumed letter.

- [ ] **Step 1: Replace the guest step and the commit predicate wording**

In section "2. VM lifecycle", replace step 6 with:

```markdown
6. **Run (guest).** Two tasks baked into the golden image, both no-ops
   until the golden build's completion marker exists. `slate-bootstrap-
   system` (SYSTEM, at startup) polls
   `HKLM:\SOFTWARE\Microsoft\Virtual Machine\External` for the chunks
   (max 5 min), sets the static IP, gateway and DNS from the KVP items,
   locates the cache volume by its `slate-cache` label, creates
   `cache\{cargo\registry,cargo\git,target,nuget}` on it, writes
   `C:\actions-runner\.env` (`NSC_CACHE_PATH`, `CARGO_TARGET_DIR`,
   `NUGET_PACKAGES`, `SLATE_CACHE_ROOT`), writes the config to
   `C:\actions-runner\jit.cfg` readable only by `runner`, and touches
   `ready`. `slate-runner-logon` (`runner`, interactive, at logon) waits
   for `ready`, junctions `%USERPROFILE%\.cargo\{registry,git}` onto the
   cache, runs `run.cmd --jitconfig <config>` in the interactive session,
   deletes `jit.cfg`, and runs `shutdown /s /t 0`. A bootstrap failure
   writes `bootstrap-error.txt` and shuts down.
```

In section "3. Cache trust", replace the bullet beginning "the guest reported" with:

```markdown
- the host did not force the VM off (the guest reached `Off` by its own
  `shutdown`), which is the host's own record, not a guest claim;
```

- [ ] **Step 2: Replace the golden-image build description**

In section "4. Guest golden image", replace the first paragraph with:

```markdown
Built once by `scripts/ci-host/golden/build-golden.ps1` (elevated) without
running Windows Setup: it mounts the ISO, applies the "Windows 11 Pro"
index of `install.wim` to a new 120 GB dynamic VHDX with
`Expand-WindowsImage`, makes it bootable with `bcdboot`, drops a rendered
`unattend.xml` into `Windows\Panther` and the provisioning scripts into
`C:\provision`, then boots the disk once on the Default Switch (Internet
for downloads). The unattend creates `provision` (admin, auto-logon once)
and `runner` (standard), and its first-logon command runs
`provision-guest.ps1`; that script installs the machine-wide toolchain,
registers the two guest tasks, switches auto-logon to `runner`, sets a
RunOnce for `provision-runner-user.ps1` (per-user rustup and
uniffi-bindgen-cs), and reboots. The per-user script writes the
completion marker `C:\Users\runner\.slate-golden-complete` and shuts
down. The host verifies the marker, removes the build VM and marks the
VHDX read-only. Product key, auto-logon password and the `provision`
password live only in the rendered unattend and `C:\provision\secrets.json`,
both deleted by the provisioning script before the final reboot (the
auto-logon password remains in the guest registry, as auto-logon
requires; the VM is isolated and disposable).
```

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md
git commit -m "docs(specs): self-hosted runner — two guest tasks, host-observed shutdown, DISM-applied golden disk"
```

---

### Task 2: Module skeleton, Pester harness, CI lane, labels and KVP chunking

**Files:**
- Create: `scripts/ci-host/SlateCiHost.psm1`
- Create: `scripts/ci-host/tests/SlateCiHost.Tests.ps1`
- Create: `scripts/ci-host/tests/Invoke-Tests.ps1`
- Create: `.github/workflows/ci-host.yml`

**Interfaces:**
- Produces: `Get-LaneFromLabels -Labels [string[]] -Lanes [string[]]` → lane string or `$null`; `New-RunnerName -Lane [string]` → `slate-win-<lane>-<8 hex>`; `Split-KvpChunks -Text [string] -Prefix [string] -ChunkSize [int]` → `IDictionary` of `<prefix>.0..n` plus `<prefix>.count`; `Join-KvpChunks -Items [IDictionary] -Prefix [string]` → string or `$null`.

- [ ] **Step 1: Install Pester 5 locally (the host ships Pester 3.4)**

```bash
pwsh -NoProfile -Command "Install-Module Pester -MinimumVersion 5.5.0 -Scope CurrentUser -Force -SkipPublisherCheck; (Get-Module -ListAvailable Pester | Sort-Object Version -Descending | Select-Object -First 1).Version.ToString()"
```

Expected: a version `5.x` printed.

- [ ] **Step 2: Write the test runner script**

`scripts/ci-host/tests/Invoke-Tests.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Runs the ci-host Pester suite with Pester 5 (the host's built-in 3.4 is
# ignored). Exits non-zero on any failure; used locally and by ci-host.yml.
param([string]$Path = $PSScriptRoot, [string]$Filter)

$ErrorActionPreference = 'Stop'
Import-Module Pester -MinimumVersion 5.5.0 -Force
$config = New-PesterConfiguration
$config.Run.Path = $Path
$config.Run.Exit = $true
$config.Output.Verbosity = 'Detailed'
if ($Filter) { $config.Filter.FullName = $Filter }
Invoke-Pester -Configuration $config
```

- [ ] **Step 3: Write the failing tests**

`scripts/ci-host/tests/SlateCiHost.Tests.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force
}

Describe 'Get-LaneFromLabels' {
    It 'returns the lane when exactly one lane label is present' {
        Get-LaneFromLabels -Labels @('self-hosted', 'Windows', 'X64', 'slate-win-app') | Should -Be 'app'
    }
    It 'returns null when no lane label is present' {
        Get-LaneFromLabels -Labels @('self-hosted', 'windows-latest') | Should -BeNullOrEmpty
    }
    It 'returns null for two lane labels' {
        Get-LaneFromLabels -Labels @('slate-win-app', 'slate-win-model') | Should -BeNullOrEmpty
    }
    It 'ignores an unknown lane name' {
        Get-LaneFromLabels -Labels @('slate-win-nightly') | Should -BeNullOrEmpty
    }
    It 'ignores labels that merely contain the prefix' {
        Get-LaneFromLabels -Labels @('slate-win-app-old', 'xslate-win-app') | Should -BeNullOrEmpty
    }
    It 'handles an empty label list' {
        Get-LaneFromLabels -Labels @() | Should -BeNullOrEmpty
    }
}

Describe 'New-RunnerName' {
    It 'produces slate-win-`<lane`>-`<8 hex`>' {
        New-RunnerName -Lane 'model' | Should -Match '^slate-win-model-[0-9a-f]{8}$'
    }
    It 'is unique across calls' {
        (1..20 | ForEach-Object { New-RunnerName -Lane 'rust' } | Sort-Object -Unique).Count | Should -Be 20
    }
}

Describe 'Split-KvpChunks / Join-KvpChunks' {
    It 'splits into 1000-character chunks with a count item' {
        $text = 'a' * 2500
        $items = Split-KvpChunks -Text $text
        $items['slate.jit.count'] | Should -Be '3'
        $items['slate.jit.0'].Length | Should -Be 1000
        $items['slate.jit.2'].Length | Should -Be 500
        $items.Keys.Count | Should -Be 4
    }
    It 'round-trips an arbitrary payload' {
        $text = -join ((1..3333) | ForEach-Object { [char](33 + ($_ % 90)) })
        Join-KvpChunks -Items (Split-KvpChunks -Text $text) | Should -Be $text
    }
    It 'round-trips a payload that is an exact multiple of the chunk size (boundary)' {
        $text = 'b' * 3000
        $items = Split-KvpChunks -Text $text
        $items['slate.jit.count'] | Should -Be '3'
        Join-KvpChunks -Items $items | Should -Be $text
    }
    It 'round-trips an empty payload' {
        $items = Split-KvpChunks -Text ''
        $items['slate.jit.count'] | Should -Be '0'
        Join-KvpChunks -Items $items | Should -Be ''
    }
    It 'never emits a value longer than 1024 characters at any chunk size' {
        foreach ($size in 1, 7, 999, 1000) {
            $items = Split-KvpChunks -Text ('c' * 2048) -ChunkSize $size
            foreach ($k in $items.Keys) { $items[$k].Length | Should -BeLessOrEqual 1024 }
        }
    }
    It 'refuses a chunk size above 1024' {
        { Split-KvpChunks -Text 'x' -ChunkSize 1025 } | Should -Throw
    }
    It 'returns null when the count item is missing' {
        Join-KvpChunks -Items @{ 'slate.jit.0' = 'x' } | Should -BeNullOrEmpty
    }
    It 'returns null when a chunk is missing' {
        Join-KvpChunks -Items @{ 'slate.jit.count' = '2'; 'slate.jit.0' = 'x' } | Should -BeNullOrEmpty
    }
    It 'honours a custom prefix' {
        $items = Split-KvpChunks -Text 'hello' -Prefix 'p'
        $items['p.count'] | Should -Be '1'
        Join-KvpChunks -Items $items -Prefix 'p' | Should -Be 'hello'
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: FAIL, "Could not load module" or "The term 'Get-LaneFromLabels' is not recognized".

- [ ] **Step 5: Write the module skeleton with these four functions**

`scripts/ci-host/SlateCiHost.psm1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Pure logic for the self-hosted Windows runner host (spec:
# docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md).
# Every GitHub and Hyper-V call lives in scripts/ci-host/adapters and is
# passed in as a scriptblock, so this module is tested without a VM or a
# token. The guest imports this module too (Windows PowerShell 5.1), so
# keep the syntax 5.1-compatible; -AsHashtable appears only in host-only
# functions.

Set-StrictMode -Version Latest

$script:DefaultLanes = @('rust', 'app', 'model', 'shell')

function Get-LaneFromLabels {
    # Exactly one slate-win-<lane> label selects a lane; zero or two mean
    # the job is not ours (a PR can put any label it likes in runs-on).
    [CmdletBinding()]
    param([string[]]$Labels = @(), [string[]]$Lanes = $script:DefaultLanes)
    $found = @()
    foreach ($label in @($Labels)) {
        if ($label -match '^slate-win-([a-z]+)$' -and $Lanes -contains $Matches[1]) {
            $found += $Matches[1]
        }
    }
    if ($found.Count -eq 1) { return $found[0] }
    return $null
}

function New-RunnerName {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Lane)
    return 'slate-win-{0}-{1}' -f $Lane, [guid]::NewGuid().ToString('N').Substring(0, 8)
}

function Split-KvpChunks {
    # Msvm_KvpExchangeDataItem.Data has MAXLEN 1024; chunk below it.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Text,
        [string]$Prefix = 'slate.jit',
        [int]$ChunkSize = 1000
    )
    if ($ChunkSize -lt 1 -or $ChunkSize -gt 1024) { throw "ChunkSize must be 1..1024 (KVP Data MAXLEN is 1024), got $ChunkSize" }
    $items = New-Object System.Collections.Specialized.OrderedDictionary
    $count = [int][math]::Ceiling($Text.Length / [double]$ChunkSize)
    for ($i = 0; $i -lt $count; $i++) {
        $start = $i * $ChunkSize
        $length = [math]::Min($ChunkSize, $Text.Length - $start)
        $items["$Prefix.$i"] = $Text.Substring($start, $length)
    }
    $items["$Prefix.count"] = [string]$count
    return $items
}

function Join-KvpChunks {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary]$Items,
        [string]$Prefix = 'slate.jit'
    )
    $countKey = "$Prefix.count"
    if (-not $Items.Contains($countKey)) { return $null }
    $count = [int]$Items[$countKey]
    $builder = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $count; $i++) {
        $key = "$Prefix.$i"
        if (-not $Items.Contains($key)) { return $null }
        [void]$builder.Append([string]$Items[$key])
    }
    return $builder.ToString()
}

Export-ModuleMember -Function Get-LaneFromLabels, New-RunnerName, Split-KvpChunks, Join-KvpChunks
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: every test passes, `Failed: 0`.

- [ ] **Step 7: Add the CI lane**

`.github/workflows/ci-host.yml`:

```yaml
# Pester suite for scripts/ci-host (the self-hosted Windows runner host).
# Pure logic only: adapters are faked, so hosted Linux is enough and no
# runner or cache volume is involved (docs/runbooks/ci-cache-policy.md
# keeps cheap verifier lanes on standard hosted Linux).
name: ci-host

on:
  pull_request:
    paths:
      - 'scripts/ci-host/**'
      - '.github/workflows/ci-host.yml'
  push:
    branches: [main]
    paths:
      - 'scripts/ci-host/**'
      - '.github/workflows/ci-host.yml'

permissions:
  contents: read

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

jobs:
  pester:
    name: ci-host pester
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - name: Checkout
        uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0  # v7

      - name: Ensure Pester 5
        shell: pwsh
        run: |
          $have = Get-Module -ListAvailable Pester | Where-Object { $_.Version -ge [version]'5.5.0' }
          if (-not $have) { Install-Module Pester -MinimumVersion 5.5.0 -Force -Scope CurrentUser -SkipPublisherCheck }
          (Get-Module -ListAvailable Pester | Sort-Object Version -Descending | Select-Object -First 1).Version.ToString()

      - name: Run the suite
        shell: pwsh
        run: ./scripts/ci-host/tests/Invoke-Tests.ps1
```

- [ ] **Step 8: Commit**

```bash
git add scripts/ci-host/SlateCiHost.psm1 scripts/ci-host/tests/SlateCiHost.Tests.ps1 scripts/ci-host/tests/Invoke-Tests.ps1 .github/workflows/ci-host.yml
git commit -m "feat(ci-host): module skeleton — lane labels, runner names, KVP chunking; Pester lane"
```

---

### Task 3: Queue selection, admission, retries, deadlines, stale runners

**Files:**
- Modify: `scripts/ci-host/SlateCiHost.psm1`
- Modify: `scripts/ci-host/tests/SlateCiHost.Tests.ps1`

**Interfaces:**
- Consumes: `Get-LaneFromLabels` (Task 2).
- Produces: `ConvertTo-DateTimeOffset -Value` → `[datetimeoffset]` from a datetimeoffset, a `[datetime]` (Unspecified kind treated as UTC) or an ISO string; `Select-QueuedLaneJobs -Jobs [object[]] -Lanes [string[]]` → `[pscustomobject[]]` with `JobId [int64]`, `RunId [int64]`, `Lane [string]`, `CreatedAt [datetimeoffset]`, sorted oldest first; `Select-JobsToAdmit -Candidates -ActiveVms [IDictionary] -FreeSlots [int] -Retries [IDictionary] -Now [datetimeoffset] -RetryCap [int]` → subset of candidates; `Register-JobRetry -Retries [IDictionary] -JobId [int64] -Now [datetimeoffset] -BackoffSeconds [int]`; `Test-VmExpired -StartedAt [datetimeoffset] -MaxMinutes [int] -Now [datetimeoffset] -GraceMinutes [int]` → bool; `Get-StaleRunnerNames -Runners [object[]] -ActiveNames [string[]]` → `[string[]]`.
- Retry entry shape (journal): `Retries['<jobId>'] = @{ Count = [int]; NextAt = '<ISO 8601>' }`.
- Active VM entry shape used here: `ActiveVms['<name>'].JobId`.

- [ ] **Step 1: Append the failing tests**

Append to `scripts/ci-host/tests/SlateCiHost.Tests.ps1`:

```powershell
Describe 'ConvertTo-DateTimeOffset' {
    It 'keeps the instant for a Z string, an offset string, and every datetime kind' {
        $expected = [datetimeoffset]::Parse('2026-10-10T10:00:00Z', [cultureinfo]::InvariantCulture)
        (ConvertTo-DateTimeOffset -Value '2026-10-10T10:00:00Z').UtcDateTime | Should -Be $expected.UtcDateTime
        (ConvertTo-DateTimeOffset -Value '2026-10-10T12:00:00+02:00').UtcDateTime | Should -Be $expected.UtcDateTime
        (ConvertTo-DateTimeOffset -Value ([datetime]::SpecifyKind([datetime]'2026-10-10T10:00:00', 'Utc'))).UtcDateTime | Should -Be $expected.UtcDateTime
        (ConvertTo-DateTimeOffset -Value ([datetime]::SpecifyKind([datetime]'2026-10-10T10:00:00', 'Utc')).ToLocalTime()).UtcDateTime | Should -Be $expected.UtcDateTime
        (ConvertTo-DateTimeOffset -Value ([datetime]'2026-10-10T10:00:00')).UtcDateTime | Should -Be $expected.UtcDateTime
        (ConvertTo-DateTimeOffset -Value $expected) | Should -Be $expected
    }
    It 'treats a zone-less string as UTC' {
        (ConvertTo-DateTimeOffset -Value '2026-10-10T10:00:00').Offset | Should -Be ([timespan]::Zero)
    }
}

Describe 'Select-QueuedLaneJobs' {
    BeforeAll {
        $script:job = {
            param($id, $run, $labels, $status, $created)
            [pscustomobject]@{ id = $id; run_id = $run; labels = $labels; status = $status; created_at = $created }
        }
    }
    It 'keeps only queued jobs with exactly one lane label, oldest first' {
        $jobs = @(
            (& $job 3 10 @('slate-win-app') 'queued' '2026-10-10T10:00:05Z'),
            (& $job 1 10 @('slate-win-rust') 'queued' '2026-10-10T10:00:01Z'),
            (& $job 2 11 @('slate-win-app', 'slate-win-model') 'queued' '2026-10-10T10:00:00Z'),
            (& $job 4 11 @('slate-win-model') 'in_progress' '2026-10-10T09:00:00Z'),
            (& $job 5 12 @('ubuntu-latest') 'queued' '2026-10-10T09:00:00Z')
        )
        $result = Select-QueuedLaneJobs -Jobs $jobs
        @($result).Count | Should -Be 2
        $result[0].JobId | Should -Be 1
        $result[0].Lane | Should -Be 'rust'
        $result[1].JobId | Should -Be 3
        $result[1].RunId | Should -Be 10
        $result[1].CreatedAt | Should -BeOfType [datetimeoffset]
    }
    It 'returns an empty array for no jobs' {
        @(Select-QueuedLaneJobs -Jobs @()).Count | Should -Be 0
    }
    It 'reads created_at as the right instant when the job came through ConvertFrom-Json' {
        $jobs = @('{"id":7,"run_id":10,"labels":["slate-win-app"],"status":"queued","created_at":"2026-10-10T10:00:00Z"}' | ConvertFrom-Json)
        $result = Select-QueuedLaneJobs -Jobs $jobs
        $result[0].CreatedAt.UtcDateTime.ToString('o') | Should -Be '2026-10-10T10:00:00.0000000Z'
    }
    It 'tolerates a job with no labels property value' {
        $jobs = @((& $job 9 10 @() 'queued' '2026-10-10T10:00:00Z'))
        @(Select-QueuedLaneJobs -Jobs $jobs).Count | Should -Be 0
    }
}

Describe 'Select-JobsToAdmit' {
    BeforeAll {
        $script:now = [datetimeoffset]::Parse('2026-10-10T12:00:00Z')
        $script:cand = {
            param($id, $lane)
            [pscustomobject]@{ JobId = [int64]$id; RunId = [int64]1; Lane = $lane; CreatedAt = $script:now }
        }
    }
    It 'admits up to the free slot count in order' {
        $c = @((& $cand 1 'app'), (& $cand 2 'rust'), (& $cand 3 'model'))
        $r = Select-JobsToAdmit -Candidates $c -ActiveVms @{} -FreeSlots 2 -Retries @{} -Now $now
        @($r).Count | Should -Be 2
        $r[0].JobId | Should -Be 1
        $r[1].JobId | Should -Be 2
    }
    It 'admits nothing when no slot is free' {
        $c = @((& $cand 1 'app'))
        @(Select-JobsToAdmit -Candidates $c -ActiveVms @{} -FreeSlots 0 -Retries @{} -Now $now).Count | Should -Be 0
    }
    It 'skips a job that already has a VM' {
        $c = @((& $cand 1 'app'), (& $cand 2 'rust'))
        $active = @{ 'slate-win-app-00000001' = @{ JobId = [int64]1 } }
        $r = Select-JobsToAdmit -Candidates $c -ActiveVms $active -FreeSlots 2 -Retries @{} -Now $now
        @($r).Count | Should -Be 1
        $r[0].JobId | Should -Be 2
    }
    It 'skips a job in back-off and admits it once the back-off elapses' {
        $c = @((& $cand 1 'app'))
        $retries = @{ '1' = @{ Count = 1; NextAt = $now.AddSeconds(60).ToString('o') } }
        @(Select-JobsToAdmit -Candidates $c -ActiveVms @{} -FreeSlots 1 -Retries $retries -Now $now).Count | Should -Be 0
        @(Select-JobsToAdmit -Candidates $c -ActiveVms @{} -FreeSlots 1 -Retries $retries -Now $now.AddSeconds(61)).Count | Should -Be 1
    }
    It 'never admits a job at the retry cap' {
        $c = @((& $cand 1 'app'))
        $retries = @{ '1' = @{ Count = 3; NextAt = $now.AddDays(-1).ToString('o') } }
        @(Select-JobsToAdmit -Candidates $c -ActiveVms @{} -FreeSlots 1 -Retries $retries -Now $now -RetryCap 3).Count | Should -Be 0
    }
}

Describe 'Register-JobRetry' {
    It 'creates and increments the retry record with a back-off' {
        $now = [datetimeoffset]::Parse('2026-10-10T12:00:00Z')
        $retries = @{}
        Register-JobRetry -Retries $retries -JobId 42 -Now $now -BackoffSeconds 600
        $retries['42'].Count | Should -Be 1
        [datetimeoffset]$retries['42'].NextAt | Should -Be $now.AddSeconds(600)
        Register-JobRetry -Retries $retries -JobId 42 -Now $now.AddSeconds(700) -BackoffSeconds 600
        $retries['42'].Count | Should -Be 2
    }
}

Describe 'Test-VmExpired' {
    It 'expires at max plus grace, not before' {
        $start = [datetimeoffset]::Parse('2026-10-10T12:00:00Z')
        Test-VmExpired -StartedAt $start -MaxMinutes 100 -Now $start.AddMinutes(109) | Should -BeFalse
        Test-VmExpired -StartedAt $start -MaxMinutes 100 -Now $start.AddMinutes(110) | Should -BeTrue
    }
    It 'honours a custom grace' {
        $start = [datetimeoffset]::Parse('2026-10-10T12:00:00Z')
        Test-VmExpired -StartedAt $start -MaxMinutes 30 -Now $start.AddMinutes(31) -GraceMinutes 0 | Should -BeTrue
    }
}

Describe 'Get-StaleRunnerNames' {
    It 'returns offline slate-win runners that are not active' {
        $runners = @(
            [pscustomobject]@{ id = 1; name = 'slate-win-app-aaaaaaaa'; status = 'offline' },
            [pscustomobject]@{ id = 2; name = 'slate-win-rust-bbbbbbbb'; status = 'online' },
            [pscustomobject]@{ id = 3; name = 'other-runner'; status = 'offline' },
            [pscustomobject]@{ id = 4; name = 'slate-win-model-cccccccc'; status = 'offline' }
        )
        $stale = @(Get-StaleRunnerNames -Runners $runners -ActiveNames @('slate-win-model-cccccccc'))
        $stale | Should -Be @('slate-win-app-aaaaaaaa')
    }
    It 'returns nothing for an empty list' {
        @(Get-StaleRunnerNames -Runners @()).Count | Should -Be 0
    }
}
```

- [ ] **Step 2: Run the tests to verify the new ones fail**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: the 17 earlier tests pass; the new ones fail with "not recognized".

- [ ] **Step 3: Implement the five functions**

Insert before the `Export-ModuleMember` line in `scripts/ci-host/SlateCiHost.psm1`:

```powershell
function ConvertTo-DateTimeOffset {
    # pwsh's JSON deserialiser turns ISO strings into [datetime] (Kind Utc
    # for a trailing Z); a [string] cast would then drop the zone and a
    # later Parse would read it as local time. Accept every shape once.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Value)
    if ($Value -is [datetimeoffset]) { return $Value }
    if ($Value -is [datetime]) {
        if ($Value.Kind -eq [System.DateTimeKind]::Unspecified) { $Value = [datetime]::SpecifyKind($Value, [System.DateTimeKind]::Utc) }
        return [datetimeoffset]$Value
    }
    $styles = [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::RoundtripKind
    return [datetimeoffset]::Parse([string]$Value, [cultureinfo]::InvariantCulture, $styles)
}

function Select-QueuedLaneJobs {
    # Flattens the API's job objects into the admission shape; drops
    # anything not queued or not carrying exactly one lane label.
    [CmdletBinding()]
    param([object[]]$Jobs = @(), [string[]]$Lanes = $script:DefaultLanes)
    $result = @()
    foreach ($job in @($Jobs)) {
        if ($null -eq $job -or $job.status -ne 'queued') { continue }
        $labels = @()
        if ($job.PSObject.Properties['labels'] -and $null -ne $job.labels) { $labels = @($job.labels) }
        $lane = Get-LaneFromLabels -Labels $labels -Lanes $Lanes
        if (-not $lane) { continue }
        $result += [pscustomobject]@{
            JobId     = [int64]$job.id
            RunId     = [int64]$job.run_id
            Lane      = $lane
            CreatedAt = ConvertTo-DateTimeOffset -Value $job.created_at
        }
    }
    return @($result | Sort-Object CreatedAt, JobId)
}

function Select-JobsToAdmit {
    # FIFO admission into free slots; one VM per job; back-off and cap
    # from the retry table.
    [CmdletBinding()]
    param(
        [object[]]$Candidates = @(),
        [System.Collections.IDictionary]$ActiveVms = @{},
        [int]$FreeSlots = 0,
        [System.Collections.IDictionary]$Retries = @{},
        [Parameter(Mandatory)][datetimeoffset]$Now,
        [int]$RetryCap = 3
    )
    $activeJobIds = @{}
    foreach ($vm in $ActiveVms.Values) {
        if ($null -ne $vm -and $vm.Contains('JobId') -and $null -ne $vm['JobId']) { $activeJobIds[[string]$vm['JobId']] = $true }
    }
    $admit = @()
    foreach ($candidate in @($Candidates)) {
        if ($admit.Count -ge $FreeSlots) { break }
        $key = [string]$candidate.JobId
        if ($activeJobIds.ContainsKey($key)) { continue }
        if ($Retries.Contains($key)) {
            $retry = $Retries[$key]
            if ([int]$retry['Count'] -ge $RetryCap) { continue }
            if ((ConvertTo-DateTimeOffset -Value $retry['NextAt']) -gt $Now) { continue }
        }
        $admit += $candidate
    }
    return @($admit)
}

function Register-JobRetry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary]$Retries,
        [Parameter(Mandatory)][int64]$JobId,
        [Parameter(Mandatory)][datetimeoffset]$Now,
        [int]$BackoffSeconds = 600
    )
    $key = [string]$JobId
    $count = 0
    if ($Retries.Contains($key)) { $count = [int]$Retries[$key]['Count'] }
    $Retries[$key] = @{ Count = $count + 1; NextAt = $Now.AddSeconds($BackoffSeconds).ToString('o') }
}

function Test-VmExpired {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][datetimeoffset]$StartedAt,
        [Parameter(Mandatory)][int]$MaxMinutes,
        [Parameter(Mandatory)][datetimeoffset]$Now,
        [int]$GraceMinutes = 10
    )
    return ($Now -ge $StartedAt.AddMinutes($MaxMinutes + $GraceMinutes))
}

function Get-StaleRunnerNames {
    [CmdletBinding()]
    param([object[]]$Runners = @(), [string[]]$ActiveNames = @())
    $names = @()
    foreach ($runner in @($Runners)) {
        if ($null -eq $runner) { continue }
        if ($runner.name -like 'slate-win-*' -and $runner.status -eq 'offline' -and ($ActiveNames -notcontains $runner.name)) {
            $names += [string]$runner.name
        }
    }
    return @($names)
}
```

Update the export line:

```powershell
Export-ModuleMember -Function Get-LaneFromLabels, New-RunnerName, Split-KvpChunks, Join-KvpChunks,
    ConvertTo-DateTimeOffset, Select-QueuedLaneJobs, Select-JobsToAdmit, Register-JobRetry, Test-VmExpired, Get-StaleRunnerNames
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: every test in the suite passes.

- [ ] **Step 5: Commit**

```bash
git add scripts/ci-host/SlateCiHost.psm1 scripts/ci-host/tests/SlateCiHost.Tests.ps1
git commit -m "feat(ci-host): queue selection, admission with retry back-off, deadlines, stale-runner selection"
```

---

### Task 4: Commit predicate and runner-to-job resolution

**Files:**
- Modify: `scripts/ci-host/SlateCiHost.psm1`
- Modify: `scripts/ci-host/tests/SlateCiHost.Tests.ps1`

**Interfaces:**
- Produces: `Test-CommitEligible -Job -Run -RunnerName [string] -ForcedOff [bool] -ParentGeneration [int] -ForkGeneration [int] -TrustedRepo [string] -TrustedBranch [string] -TrustedEvents [string[]]` → `[pscustomobject]@{ Eligible [bool]; Reason [string] }`; `Resolve-RunnerJob -RunnerName [string] -AdmittedJobId [int64] -CandidateJobIds [int64[]] -GetJob [scriptblock]` → the API job object whose `runner_name` matches, or `$null`.
- `$Job` is the REST job object (`id`, `status`, `conclusion`, `runner_name`, `run_id`); `$Run` is the REST run object (`event`, `head_branch`, `head_repository.full_name`).

- [ ] **Step 1: Append the failing tests**

```powershell
Describe 'Test-CommitEligible' {
    BeforeAll {
        $script:goodJob = [pscustomobject]@{ id = 1; status = 'completed'; conclusion = 'success'; runner_name = 'slate-win-app-deadbeef'; run_id = 10 }
        $script:goodRun = [pscustomobject]@{ event = 'push'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
        $script:eligible = {
            param($job, $run, [bool]$forced, [int]$parent, [int]$fork)
            Test-CommitEligible -Job $job -Run $run -RunnerName 'slate-win-app-deadbeef' -ForcedOff $forced -ParentGeneration $parent -ForkGeneration $fork
        }
    }
    It 'commits a green push to main of the trusted repository with an unchanged generation' {
        $d = & $eligible $goodJob $goodRun $false 3 3
        $d.Eligible | Should -BeTrue
        $d.Reason | Should -Be 'trusted main'
    }
    It 'commits schedule and workflow_dispatch events on main' {
        foreach ($event in 'schedule', 'workflow_dispatch') {
            $run = [pscustomobject]@{ event = $event; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
            (& $eligible $goodJob $run $false 0 0).Eligible | Should -BeTrue
        }
    }
    It 'discards a pull_request event' {
        $run = [pscustomobject]@{ event = 'pull_request'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
        $d = & $eligible $goodJob $run $false 3 3
        $d.Eligible | Should -BeFalse
        $d.Reason | Should -Be 'event: pull_request'
    }
    It 'discards a push to another branch' {
        $run = [pscustomobject]@{ event = 'push'; head_branch = 'feature'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
        (& $eligible $goodJob $run $false 3 3).Reason | Should -Be 'branch: feature'
    }
    It 'discards a run from another repository' {
        $run = [pscustomobject]@{ event = 'push'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'someone/slate' } }
        (& $eligible $goodJob $run $false 3 3).Reason | Should -Be 'repository: someone/slate'
    }
    It 'discards when head_repository is null (deleted fork) without throwing' {
        $run = [pscustomobject]@{ event = 'push'; head_branch = 'main'; head_repository = $null }
        $d = & $eligible $goodJob $run $false 3 3
        $d.Eligible | Should -BeFalse
        $d.Reason | Should -Be 'repository: '
    }
    It 'discards a failed or cancelled job' {
        foreach ($c in 'failure', 'cancelled', $null) {
            $job = [pscustomobject]@{ id = 1; status = 'completed'; conclusion = $c; runner_name = 'slate-win-app-deadbeef'; run_id = 10 }
            $d = & $eligible $job $goodRun $false 3 3
            $d.Eligible | Should -BeFalse
            $d.Reason | Should -Be "conclusion: $c"
        }
    }
    It 'discards when the host forced the VM off' {
        (& $eligible $goodJob $goodRun $true 3 3).Reason | Should -Be 'guest was forced off'
    }
    It 'discards when the parent generation moved since the fork' {
        (& $eligible $goodJob $goodRun $false 4 3).Reason | Should -Be 'generation moved: fork 3, parent 4'
    }
    It 'discards when no job resolved' {
        (& $eligible $null $goodRun $false 3 3).Reason | Should -Be 'no job resolved for runner'
    }
    It 'discards when the job ran on a different runner' {
        $job = [pscustomobject]@{ id = 1; status = 'completed'; conclusion = 'success'; runner_name = 'other'; run_id = 10 }
        (& $eligible $job $goodRun $false 3 3).Reason | Should -Be 'runner mismatch: other'
    }
    It 'discards when the run is missing' {
        (& $eligible $goodJob $null $false 3 3).Reason | Should -Be 'no run'
    }
    It 'honours custom trusted repo, branch and events' {
        $run = [pscustomobject]@{ event = 'push'; head_branch = 'release'; head_repository = [pscustomobject]@{ full_name = 'x/y' } }
        $d = Test-CommitEligible -Job $goodJob -Run $run -RunnerName 'slate-win-app-deadbeef' -ForcedOff $false -ParentGeneration 1 -ForkGeneration 1 -TrustedRepo 'x/y' -TrustedBranch 'release' -TrustedEvents @('push')
        $d.Eligible | Should -BeTrue
    }
}

Describe 'Resolve-RunnerJob' {
    It 'returns the admitted job when it ran on this runner' {
        $jobs = @{ 1 = [pscustomobject]@{ id = 1; runner_name = 'slate-win-app-deadbeef' } }
        $r = Resolve-RunnerJob -RunnerName 'slate-win-app-deadbeef' -AdmittedJobId 1 -CandidateJobIds @(2, 3) -GetJob { param($id) $jobs[[int]$id] }
        $r.id | Should -Be 1
    }
    It 'falls back to a candidate when another job took the runner' {
        $jobs = @{
            1 = [pscustomobject]@{ id = 1; runner_name = 'slate-win-app-other' }
            2 = [pscustomobject]@{ id = 2; runner_name = 'slate-win-app-deadbeef' }
        }
        $calls = [System.Collections.ArrayList]::new()
        $r = Resolve-RunnerJob -RunnerName 'slate-win-app-deadbeef' -AdmittedJobId 1 -CandidateJobIds @(1, 2, 3) -GetJob { param($id) [void]$calls.Add($id); $jobs[[int]$id] }
        $r.id | Should -Be 2
        @($calls) | Should -Be @(1, 2)
    }
    It 'returns null when nothing matches' {
        Resolve-RunnerJob -RunnerName 'slate-win-app-deadbeef' -AdmittedJobId 1 -CandidateJobIds @() -GetJob { param($id) $null } | Should -BeNullOrEmpty
    }
}
```

- [ ] **Step 2: Run the tests to verify the new ones fail**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: new tests fail with "not recognized".

- [ ] **Step 3: Implement**

Insert before `Export-ModuleMember`:

```powershell
function Test-CommitEligible {
    # The whole cache-trust decision, from GitHub's record of what ran and
    # the host's own record of how the VM stopped. Labels play no part.
    [CmdletBinding()]
    param(
        $Job,
        $Run,
        [Parameter(Mandatory)][string]$RunnerName,
        [Parameter(Mandatory)][bool]$ForcedOff,
        [Parameter(Mandatory)][int]$ParentGeneration,
        [Parameter(Mandatory)][int]$ForkGeneration,
        [ValidateNotNullOrEmpty()][string]$TrustedRepo = 'coryj627/slate',
        [ValidateNotNullOrEmpty()][string]$TrustedBranch = 'main',
        [ValidateNotNullOrEmpty()][string[]]$TrustedEvents = @('push', 'schedule', 'workflow_dispatch')
    )
    # Comparisons are ordinal: PowerShell's string operators compare
    # linguistically and drop default-ignorable code points (a soft hyphen
    # inside 'main' would match). Git refs, runner names and the event are
    # compared exactly; the repository ignores case because GitHub
    # repository names do.
    function Deny([string]$reason) { return [pscustomobject]@{ Eligible = $false; Reason = $reason } }
    if ($null -eq $Job) { return (Deny 'no job resolved for runner') }
    if (-not [string]::Equals([string]$Job.runner_name, $RunnerName, [System.StringComparison]::Ordinal)) { return (Deny "runner mismatch: $($Job.runner_name)") }
    if (-not [string]::Equals([string]$Job.conclusion, 'success', [System.StringComparison]::Ordinal)) { return (Deny "conclusion: $($Job.conclusion)") }
    if ($ForcedOff) { return (Deny 'guest was forced off') }
    if ($null -eq $Run) { return (Deny 'no run') }
    if ([Array]::IndexOf([string[]]$TrustedEvents, [string]$Run.event) -lt 0) { return (Deny "event: $($Run.event)") }
    if (-not [string]::Equals([string]$Run.head_branch, $TrustedBranch, [System.StringComparison]::Ordinal)) { return (Deny "branch: $($Run.head_branch)") }
    $repoName = ''
    if ($null -ne $Run.head_repository -and $Run.head_repository.PSObject.Properties['full_name']) { $repoName = [string]$Run.head_repository.full_name }
    if (-not [string]::Equals($repoName, $TrustedRepo, [System.StringComparison]::OrdinalIgnoreCase)) { return (Deny "repository: $repoName") }
    if ($ParentGeneration -ne $ForkGeneration) { return (Deny "generation moved: fork $ForkGeneration, parent $ParentGeneration") }
    return [pscustomobject]@{ Eligible = $true; Reason = 'trusted main' }
}

function Resolve-RunnerJob {
    # Any queued job with matching labels may have taken this runner, so
    # ask GitHub which one did: the admitted job first, then every other
    # recently seen candidate.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RunnerName,
        [int64]$AdmittedJobId = 0,
        [int64[]]$CandidateJobIds = @(),
        [Parameter(Mandatory)][scriptblock]$GetJob
    )
    $ids = @()
    if ($AdmittedJobId -gt 0) { $ids += $AdmittedJobId }
    foreach ($id in @($CandidateJobIds)) { if ($ids -notcontains $id) { $ids += $id } }
    foreach ($id in $ids) {
        $job = & $GetJob $id
        if ($null -ne $job -and [string]$job.runner_name -eq $RunnerName) { return $job }
    }
    return $null
}
```

Add `Test-CommitEligible, Resolve-RunnerJob` to `Export-ModuleMember`.

- [ ] **Step 4: Run the tests to verify they pass**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: every test in the suite passes.

- [ ] **Step 5: Commit**

```bash
git add scripts/ci-host/SlateCiHost.psm1 scripts/ci-host/tests/SlateCiHost.Tests.ps1
git commit -m "feat(ci-host): provenance commit predicate and runner-to-job resolution"
```

---

### Task 5: Config, journal and log

**Files:**
- Create: `scripts/ci-host/config.json`
- Modify: `scripts/ci-host/SlateCiHost.psm1`
- Modify: `scripts/ci-host/tests/SlateCiHost.Tests.ps1`

**Interfaces:**
- Produces: `Get-CiHostConfig -Path [string]` → `IDictionary` with keys `Owner, Repo, Root, SwitchName, Gateway, Dns, Slots (array of @{Index; Ip}), Vcpu, MemoryGB, Lanes (lane → @{Cache [bool]; MaxMinutes [int]}), TickSeconds, HeartbeatTimeoutSeconds, UnclaimedTimeoutSeconds, RetryCap, RetryBackoffSeconds, TrustedRepo, TrustedBranch, TrustedEvents, GoldenPath, CacheDir, VmDir, StateDir, LogDir`; `New-Journal` → `@{ Vms = @{}; Retries = @{}; SeenJobs = @{} }`; `Read-Journal -Path` (corrupt file → moved to `<path>.corrupt-<timestamp>` and a fresh journal returned); `Write-Journal -Path -Journal` (atomic via temp file + move); `Write-CiLog -Path [string] -Level [string] -Message [string]`.
- Journal VM entry (set in Task 8): `@{ Name; Lane; Slot [int]; SlotIp; JobId [int64]; RunId [int64]; Dir; CachePath; ForkGeneration [int]; Phase 'provisioned'|'handed'; StartedAt ISO; HandedAt ISO|null; RunnerId; Claimed [bool] }`.

- [ ] **Step 1: Write the config file**

`scripts/ci-host/config.json`:

```json
{
  "Owner": "coryj627",
  "Repo": "slate",
  "Root": "C:\\slate-ci",
  "SwitchName": "slate-ci",
  "Gateway": "10.77.0.1",
  "Dns": "1.1.1.1,8.8.8.8",
  "Slots": [
    { "Index": 1, "Ip": "10.77.0.11" },
    { "Index": 2, "Ip": "10.77.0.12" }
  ],
  "Vcpu": 4,
  "MemoryGB": 12,
  "Lanes": {
    "rust":  { "Cache": true,  "MaxMinutes": 70 },
    "app":   { "Cache": true,  "MaxMinutes": 100 },
    "model": { "Cache": true,  "MaxMinutes": 100 },
    "shell": { "Cache": false, "MaxMinutes": 30 }
  },
  "TickSeconds": 10,
  "HeartbeatTimeoutSeconds": 180,
  "UnclaimedTimeoutSeconds": 300,
  "RetryCap": 3,
  "RetryBackoffSeconds": 600,
  "TrustedRepo": "coryj627/slate",
  "TrustedBranch": "main",
  "TrustedEvents": [ "push", "schedule", "workflow_dispatch" ]
}
```

- [ ] **Step 2: Append the failing tests**

```powershell
Describe 'Get-CiHostConfig' {
    It 'loads the repository config and derives the paths' {
        $cfg = Get-CiHostConfig -Path (Join-Path $PSScriptRoot '..' 'config.json')
        $cfg.Owner | Should -Be 'coryj627'
        @($cfg.Slots).Count | Should -Be 2
        $cfg.Slots[1].Ip | Should -Be '10.77.0.12'
        $cfg.Lanes['shell'].Cache | Should -BeFalse
        $cfg.Lanes['app'].MaxMinutes | Should -Be 100
        $cfg.GoldenPath | Should -Be 'C:\slate-ci\golden\win11-runner.vhdx'
        $cfg.CacheDir | Should -Be 'C:\slate-ci\cache'
        $cfg.VmDir | Should -Be 'C:\slate-ci\vms'
        $cfg.StateDir | Should -Be 'C:\slate-ci\state'
        $cfg.LogDir | Should -Be 'C:\slate-ci\logs'
        @($cfg.TrustedEvents) | Should -Be @('push', 'schedule', 'workflow_dispatch')
    }
    It 'rejects a config missing a required key' {
        $p = Join-Path $TestDrive 'bad.json'
        '{ "Owner": "x" }' | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*Repo*'
    }
    It 'rejects an unknown lane' {
        $p = Join-Path $TestDrive 'lane.json'
        $raw = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        $raw.Lanes['bogus'] = @{ Cache = $true; MaxMinutes = 5 }
        $raw | ConvertTo-Json -Depth 6 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*bogus*'
    }
}

Describe 'Journal' {
    It 'returns a fresh journal when the file does not exist' {
        $j = Read-Journal -Path (Join-Path $TestDrive 'none.json')
        $j.Vms.Count | Should -Be 0
        $j.Retries.Count | Should -Be 0
        $j.SeenJobs.Count | Should -Be 0
    }
    It 'round-trips VM entries, retries and seen jobs' {
        $p = Join-Path $TestDrive 'journal.json'
        $j = New-Journal
        $j.Vms['slate-win-app-00000001'] = @{ Name = 'slate-win-app-00000001'; Lane = 'app'; Slot = 1; SlotIp = '10.77.0.11'; JobId = [int64]123456789012; RunId = [int64]5; Dir = 'C:\slate-ci\vms\x'; CachePath = 'C:\slate-ci\vms\x\cache.vhdx'; ForkGeneration = 7; Phase = 'handed'; StartedAt = '2026-10-10T12:00:00.0000000+00:00'; HandedAt = '2026-10-10T12:01:00.0000000+00:00'; RunnerId = 99; Claimed = $true }
        $j.Retries['42'] = @{ Count = 2; NextAt = '2026-10-10T12:10:00.0000000+00:00' }
        $j.SeenJobs['42'] = @{ RunId = [int64]5; Lane = 'app'; FirstSeenAt = '2026-10-10T12:00:00.0000000+00:00' }
        Write-Journal -Path $p -Journal $j
        $back = Read-Journal -Path $p
        $back.Vms['slate-win-app-00000001'].JobId | Should -Be 123456789012
        $back.Vms['slate-win-app-00000001'].Claimed | Should -BeTrue
        $back.Vms['slate-win-app-00000001'].ForkGeneration | Should -Be 7
        $back.Retries['42'].Count | Should -Be 2
        $back.SeenJobs['42'].Lane | Should -Be 'app'
        (Get-ChildItem $TestDrive -Filter '*.tmp').Count | Should -Be 0
    }
    It 'moves a corrupt journal aside and starts fresh' {
        $p = Join-Path $TestDrive 'corrupt.json'
        '{ "Vms": { "x": ' | Set-Content $p
        $j = Read-Journal -Path $p
        $j.Vms.Count | Should -Be 0
        (Get-ChildItem $TestDrive -Filter 'corrupt.json.corrupt-*').Count | Should -Be 1
        Test-Path $p | Should -BeFalse
    }
    It 'fills in missing top-level keys from an older journal' {
        $p = Join-Path $TestDrive 'old.json'
        '{ "Vms": {} }' | Set-Content $p
        $j = Read-Journal -Path $p
        $j.Retries.Count | Should -Be 0
        $j.SeenJobs.Count | Should -Be 0
    }
}

Describe 'Write-CiLog' {
    It 'appends a timestamped line and creates the file' {
        $p = Join-Path $TestDrive 'logs' 'o.log'
        Write-CiLog -Path $p -Level 'info' -Message 'hello'
        Write-CiLog -Path $p -Level 'warn' -Message 'again'
        $lines = Get-Content $p
        $lines.Count | Should -Be 2
        $lines[0] | Should -Match '^\d{4}-\d{2}-\d{2}T[0-9:.]+(\+|-)\d{2}:\d{2} \[info\] hello$'
        $lines[1] | Should -Match '\[warn\] again$'
    }
}
```

- [ ] **Step 3: Run the tests to verify the new ones fail**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: new tests fail with "not recognized".

- [ ] **Step 4: Implement**

Insert before `Export-ModuleMember`:

```powershell
# ---- host-only: config, journal, log (pwsh 7; -AsHashtable) ----

function Join-WinPath {
    # Host paths are Windows paths even when the suite runs on Linux CI,
    # where Join-Path would insert a forward slash.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Base, [Parameter(Mandatory)][string]$Child)
    return ($Base.TrimEnd('\') + '\' + $Child.TrimStart('\'))
}

function Get-CiHostConfig {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    $config = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -AsHashtable
    $required = 'Owner', 'Repo', 'Root', 'SwitchName', 'Gateway', 'Dns', 'Slots', 'Vcpu', 'MemoryGB', 'Lanes',
        'TickSeconds', 'HeartbeatTimeoutSeconds', 'UnclaimedTimeoutSeconds', 'RetryCap', 'RetryBackoffSeconds',
        'TrustedRepo', 'TrustedBranch', 'TrustedEvents'
    foreach ($key in $required) {
        if (-not $config.Contains($key)) { throw "config ${Path}: missing required key '$key'" }
    }
    foreach ($lane in @($config.Lanes.Keys)) {
        if ($script:DefaultLanes -cnotcontains $lane) { throw "config ${Path}: unknown lane '$lane'" }
        foreach ($k in 'Cache', 'MaxMinutes') {
            if (-not $config.Lanes[$lane].Contains($k)) { throw "config ${Path}: lane '$lane' missing '$k'" }
        }
    }
    if ($null -eq $config.Slots -or @($config.Slots).Count -lt 1) { throw "config ${Path}: at least one slot is required" }
    $config['GoldenPath'] = Join-WinPath $config.Root 'golden\win11-runner.vhdx'
    $config['CacheDir'] = Join-WinPath $config.Root 'cache'
    $config['VmDir'] = Join-WinPath $config.Root 'vms'
    $config['StateDir'] = Join-WinPath $config.Root 'state'
    $config['LogDir'] = Join-WinPath $config.Root 'logs'
    return $config
}

function New-Journal {
    return @{ Vms = @{}; Retries = @{}; SeenJobs = @{} }
}

function Read-Journal {
    # A corrupt journal must never stop the host: it is moved aside with a
    # timestamp (for forensics) and an empty one takes its place. The
    # startup sweep then reconciles live VMs and runners from scratch.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return (New-Journal) }
    $journal = $null
    try {
        # -ErrorAction Stop: a locked file is otherwise a non-terminating
        # error and the IOException clause below would never run.
        $journal = Get-Content -Raw -LiteralPath $Path -ErrorAction Stop | ConvertFrom-Json -AsHashtable
        if ($null -eq $journal -or -not ($journal -is [System.Collections.IDictionary])) { throw 'journal is not an object' }
    } catch [System.IO.IOException] {
        # Unreadable (locked) is not corrupt: never overwrite a journal we
        # could not read; the task restarts and retries.
        throw
    } catch {
        $aside = '{0}.corrupt-{1}' -f $Path, (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
        Move-Item -LiteralPath $Path -Destination $aside -Force
        return (New-Journal)
    }
    foreach ($key in 'Vms', 'Retries', 'SeenJobs') {
        if (-not $journal.Contains($key) -or $null -eq $journal[$key]) { $journal[$key] = @{} }
    }
    return $journal
}

function Write-Journal {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)]$Journal)
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $tmp = "$Path.tmp"
    $Journal | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $tmp -Encoding utf8
    # The three-argument overload replaces the destination atomically
    # (Move-Item -Force deletes, then renames).
    [System.IO.File]::Move($tmp, $Path, $true)
}

function Write-CiLog {
    # One line per state transition; callers never pass a JIT config or
    # token into Message.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][ValidateSet('info', 'warn', 'error')][string]$Level,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Message
    )
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $line = '{0} [{1}] {2}' -f [datetimeoffset]::Now.ToString('o'), $Level, $Message
    Add-Content -LiteralPath $Path -Value $line -Encoding utf8
}
```

Add `Get-CiHostConfig, New-Journal, Read-Journal, Write-Journal, Write-CiLog` to `Export-ModuleMember`.

- [ ] **Step 5: Run the tests to verify they pass**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: every test in the suite passes.

- [ ] **Step 6: Commit**

```bash
git add scripts/ci-host/config.json scripts/ci-host/SlateCiHost.psm1 scripts/ci-host/tests/SlateCiHost.Tests.ps1
git commit -m "feat(ci-host): config loader, crash-safe journal, log writer"
```

---

### Task 6: GitHub REST adapter

**Files:**
- Create: `scripts/ci-host/adapters/GitHub.ps1`
- Create: `scripts/ci-host/tests/GitHubAdapter.Tests.ps1`

**Interfaces:**
- Produces: `Initialize-GitHubAdapter -Owner -Repo -Token [securestring]`; `Invoke-GhApi -Method -Path -Body`; `Get-GhQueuedLaneJobs` → REST job objects with `status == queued`; `Get-GhJob -JobId`; `Get-GhRun -RunId`; `New-GhJitRunner -Name -Labels` → `@{ RunnerId [int64]; EncodedJitConfig [string] }`; `Remove-GhRunner -RunnerId`; `Get-GhRunner -RunnerId` (null on 404); `Get-GhRunners`; `New-GitHubAdapters` → hashtable with keys `GetQueuedJobs, GetJob, GetRun, NewJitRunner, RemoveRunner, GetRunner, ListRunners` (the exact keys Task 8 invokes).

- [ ] **Step 1: Write the failing tests**

`scripts/ci-host/tests/GitHubAdapter.Tests.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    . (Join-Path $PSScriptRoot '..' 'adapters' 'GitHub.ps1')
    Initialize-GitHubAdapter -Owner 'coryj627' -Repo 'slate' -Token (ConvertTo-SecureString 'ghp_test' -AsPlainText -Force)
}

Describe 'Invoke-GhApi' {
    It 'sends bearer auth, the API version header and the repository base' {
        Mock Invoke-RestMethod { [pscustomobject]@{ ok = $true } }
        Invoke-GhApi -Path '/actions/runners' | Out-Null
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
            $Uri -eq 'https://api.github.com/repos/coryj627/slate/actions/runners' -and
            $Method -eq 'GET' -and
            $Headers.Authorization -eq 'Bearer ghp_test' -and
            $Headers['X-GitHub-Api-Version'] -eq '2022-11-28' -and
            $Headers.Accept -eq 'application/vnd.github+json'
        }
    }
    It 'serialises a body as JSON for POST' {
        Mock Invoke-RestMethod { [pscustomobject]@{} }
        Invoke-GhApi -Method 'POST' -Path '/x' -Body @{ a = 1 } | Out-Null
        Should -Invoke Invoke-RestMethod -Times 1 -ParameterFilter { $Method -eq 'POST' -and $ContentType -eq 'application/json' -and ($Body | ConvertFrom-Json).a -eq 1 }
    }
}

Describe 'New-GhJitRunner' {
    It 'posts name, runner_group_id 1, labels and work_folder, returning id and config' {
        Mock Invoke-RestMethod { [pscustomobject]@{ runner = [pscustomobject]@{ id = 77 }; encoded_jit_config = 'abc' } }
        $r = New-GhJitRunner -Name 'slate-win-app-deadbeef' -Labels @('slate-win-app')
        $r.RunnerId | Should -Be 77
        $r.EncodedJitConfig | Should -Be 'abc'
        Should -Invoke Invoke-RestMethod -Times 1 -ParameterFilter {
            $body = $Body | ConvertFrom-Json
            $Method -eq 'POST' -and $Uri -like '*/actions/runners/generate-jitconfig' -and
            $body.name -eq 'slate-win-app-deadbeef' -and $body.runner_group_id -eq 1 -and
            @($body.labels) -contains 'slate-win-app' -and $body.work_folder -eq '_work'
        }
    }
}

Describe 'Get-GhQueuedLaneJobs' {
    It 'lists queued and in-progress runs and returns only their queued jobs' {
        Mock Invoke-RestMethod {
            if ($Uri -like '*actions/runs?status=queued*') { return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 1 }) } }
            if ($Uri -like '*actions/runs?status=in_progress*') { return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 2 }) } }
            if ($Uri -like '*/runs/1/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 11; run_id = 1; status = 'queued'; labels = @('slate-win-app'); created_at = '2026-10-10T10:00:00Z' }) }
            }
            if ($Uri -like '*/runs/2/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @(
                    [pscustomobject]@{ id = 21; run_id = 2; status = 'in_progress'; labels = @('slate-win-rust'); created_at = '2026-10-10T10:00:00Z' },
                    [pscustomobject]@{ id = 22; run_id = 2; status = 'queued'; labels = @('slate-win-model'); created_at = '2026-10-10T10:00:01Z' }
                ) }
            }
            throw "unexpected $Uri"
        }
        $jobs = @(Get-GhQueuedLaneJobs)
        @($jobs | ForEach-Object { $_.id }) | Should -Be @(11, 22)
        Should -Invoke Invoke-RestMethod -Times 4 -Exactly
    }
    It 'returns an empty array when nothing is queued' {
        Mock Invoke-RestMethod { [pscustomobject]@{ workflow_runs = @() } }
        @(Get-GhQueuedLaneJobs).Count | Should -Be 0
    }
}

Describe 'Get-GhRunner' {
    It 'returns null on 404 and rethrows anything else' {
        Mock Invoke-RestMethod {
            $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::NotFound)
            throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('not found', $response)
        }
        Get-GhRunner -RunnerId 5 | Should -BeNullOrEmpty
        Mock Invoke-RestMethod {
            $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::InternalServerError)
            throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('boom', $response)
        }
        { Get-GhRunner -RunnerId 5 } | Should -Throw
    }
}

Describe 'Remove-GhRunner / Get-GhRunners' {
    It 'deletes by id and lists runners' {
        Mock Invoke-RestMethod { [pscustomobject]@{ runners = @([pscustomobject]@{ id = 1; name = 'slate-win-app-a'; status = 'offline' }) } }
        Remove-GhRunner -RunnerId 9
        Should -Invoke Invoke-RestMethod -Times 1 -ParameterFilter { $Method -eq 'DELETE' -and $Uri -like '*/actions/runners/9' }
        @(Get-GhRunners).Count | Should -Be 1
    }
}

Describe 'New-GitHubAdapters' {
    It 'exposes exactly the keys the orchestrator invokes' {
        @((New-GitHubAdapters).Keys | Sort-Object) | Should -Be @('GetJob', 'GetQueuedJobs', 'GetRun', 'GetRunner', 'ListRunners', 'NewJitRunner', 'RemoveRunner')
    }
    It 'refuses to work before initialisation' {
        . (Join-Path $PSScriptRoot '..' 'adapters' 'GitHub.ps1')
        { Invoke-GhApi -Path '/x' } | Should -Throw '*Initialize-GitHubAdapter*'
        Initialize-GitHubAdapter -Owner 'coryj627' -Repo 'slate' -Token (ConvertTo-SecureString 'ghp_test' -AsPlainText -Force)
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1 -Path scripts/ci-host/tests/GitHubAdapter.Tests.ps1
```

Expected: FAIL, the adapter file does not exist.

- [ ] **Step 3: Write the adapter**

`scripts/ci-host/adapters/GitHub.ps1`:

```powershell
#Requires -Version 7
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# GitHub REST adapter for the self-hosted runner host. The only file that
# talks to api.github.com. The PAT (fine-grained: Actions read,
# Administration read/write, this repository only) is held as a
# SecureString and decoded per call; nothing here ever logs it or a JIT
# config. orchestrator.ps1 dot-sources this file and passes
# New-GitHubAdapters into the module.

$script:GhBase = $null
$script:GhToken = $null

function Initialize-GitHubAdapter {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Owner,
        [Parameter(Mandatory)][string]$Repo,
        [Parameter(Mandatory)][securestring]$Token
    )
    $script:GhBase = "https://api.github.com/repos/$Owner/$Repo"
    $script:GhToken = $Token
}

function Invoke-GhApi {
    [CmdletBinding()]
    param([string]$Method = 'GET', [Parameter(Mandatory)][string]$Path, $Body)
    if (-not $script:GhBase) { throw 'Initialize-GitHubAdapter has not been called' }
    $plain = [System.Net.NetworkCredential]::new('', $script:GhToken).Password
    $headers = @{
        Authorization          = "Bearer $plain"
        Accept                 = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
    }
    $params = @{ Method = $Method; Uri = "$($script:GhBase)$Path"; Headers = $headers; TimeoutSec = 30; UserAgent = 'slate-ci-host' }
    if ($null -ne $Body) {
        $params.Body = $Body | ConvertTo-Json -Compress -Depth 5
        $params.ContentType = 'application/json'
    }
    return Invoke-RestMethod @params
}

function Get-GhQueuedLaneJobs {
    # A run is in_progress while later jobs of it are still queued, so
    # both run states are listed. Idle cost: two calls per tick.
    [CmdletBinding()]
    param()
    $jobs = @()
    foreach ($status in 'queued', 'in_progress') {
        $runs = Invoke-GhApi -Path "/actions/runs?status=$status&per_page=100"
        foreach ($run in @($runs.workflow_runs)) {
            $page = Invoke-GhApi -Path "/actions/runs/$($run.id)/jobs?filter=latest&per_page=100"
            foreach ($job in @($page.jobs)) {
                if ($job.status -eq 'queued') { $jobs += $job }
            }
        }
    }
    return @($jobs)
}

function Get-GhJob {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$JobId)
    return Invoke-GhApi -Path "/actions/jobs/$JobId"
}

function Get-GhRun {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$RunId)
    return Invoke-GhApi -Path "/actions/runs/$RunId"
}

function New-GhJitRunner {
    # runner_group_id 1 is the default group of a user-owned repository.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string[]]$Labels)
    $response = Invoke-GhApi -Method 'POST' -Path '/actions/runners/generate-jitconfig' -Body @{
        name = $Name; runner_group_id = 1; labels = @($Labels); work_folder = '_work'
    }
    return @{ RunnerId = [int64]$response.runner.id; EncodedJitConfig = [string]$response.encoded_jit_config }
}

function Remove-GhRunner {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$RunnerId)
    Invoke-GhApi -Method 'DELETE' -Path "/actions/runners/$RunnerId" | Out-Null
}

function Get-GhRunner {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$RunnerId)
    try {
        return Invoke-GhApi -Path "/actions/runners/$RunnerId"
    } catch {
        $response = $_.Exception.Response
        if ($null -ne $response -and [int]$response.StatusCode -eq 404) { return $null }
        throw
    }
}

function Get-GhRunners {
    [CmdletBinding()]
    param()
    return @((Invoke-GhApi -Path '/actions/runners?per_page=100').runners)
}

function New-GitHubAdapters {
    [CmdletBinding()]
    param()
    return @{
        GetQueuedJobs = { Get-GhQueuedLaneJobs }
        GetJob        = { param($id) Get-GhJob -JobId $id }
        GetRun        = { param($id) Get-GhRun -RunId $id }
        NewJitRunner  = { param($name, $labels) New-GhJitRunner -Name $name -Labels $labels }
        RemoveRunner  = { param($id) Remove-GhRunner -RunnerId $id }
        GetRunner     = { param($id) Get-GhRunner -RunnerId $id }
        ListRunners   = { Get-GhRunners }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: every test in the suite passes.

- [ ] **Step 5: Commit**

```bash
git add scripts/ci-host/adapters/GitHub.ps1 scripts/ci-host/tests/GitHubAdapter.Tests.ps1
git commit -m "feat(ci-host): GitHub REST adapter — queued jobs, JIT runners, runner lifecycle"
```

---

### Task 7: Hyper-V adapter

**Files:**
- Create: `scripts/ci-host/adapters/HyperV.ps1`
- Create: `scripts/ci-host/tests/HyperVAdapter.Tests.ps1`

**Interfaces:**
- Consumes: `$Config` from `Get-CiHostConfig` (Task 5): `GoldenPath, CacheDir, VmDir, SwitchName, Vcpu, MemoryGB, Lanes`.
- Produces: `Get-ExtendedAclRules` → array of `@{ Direction; Action; RemoteIPAddress; Weight }` (14 rules); `ConvertTo-VmStateLabel -State` → `Off|Running|Other`; `Get-CacheGeneration -CacheDir -Lane` / `Set-CacheGeneration -CacheDir -Lane -Value`; `New-RunnerVm -Name -Lane -Config` → `@{ Dir; CachePath; ForkGeneration }`; `Start-RunnerVm`, `Get-RunnerVmState` (`Missing` when absent), `Get-RunnerVmHeartbeat` (`OK|NoContact`), `Send-RunnerVmKvp -Name -Items`, `Stop-RunnerVmForce`, `Remove-RunnerVm -Name -Dir`, `Get-RunnerVmNames`, `Clear-RunnerVmDirs -VmDir -ActiveNames`, `Merge-RunnerCache -Config -Lane -ChildPath` → new generation, `Remove-RunnerCache -ChildPath`; `New-HyperVAdapters -Config` → hashtable with keys `NewVm, StartVm, GetVmState, GetHeartbeat, SendKvp, StopVmForce, RemoveVm, ListVms, CleanVmDirs, GetGeneration, CommitCache, DiscardCache`.

- [ ] **Step 1: Write the failing tests (Hyper-V cmdlets stubbed so the suite runs on Linux)**

`scripts/ci-host/tests/HyperVAdapter.Tests.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    # Stubs with the parameters the assertions inspect; Pester mocks them.
    # [CmdletBinding()] so the adapter's -ErrorAction arguments bind as
    # common parameters (a plain function would reject them).
    function global:New-VHD { [CmdletBinding()] param($Path, $ParentPath, [switch]$Differencing, $SizeBytes, [switch]$Dynamic) }
    function global:New-VM { [CmdletBinding()] param($Name, $Generation, $MemoryStartupBytes, $VHDPath, $SwitchName, $Path) }
    function global:Set-VM { [CmdletBinding()] param($Name, $ProcessorCount, [switch]$StaticMemory, $AutomaticStopAction, $AutomaticStartAction, $AutomaticCheckpointsEnabled, $CheckpointType) }
    function global:Set-VMFirmware { [CmdletBinding()] param($VMName, $EnableSecureBoot, $SecureBootTemplate) }
    function global:Set-VMKeyProtector { [CmdletBinding()] param($VMName, [switch]$NewLocalKeyProtector) }
    function global:Enable-VMTPM { [CmdletBinding()] param($VMName) }
    function global:Set-VMVideo { [CmdletBinding()] param($VMName, $ResolutionType, $HorizontalResolution, $VerticalResolution) }
    function global:Add-VMHardDiskDrive { [CmdletBinding()] param($VMName, $Path) }
    function global:Add-VMNetworkAdapterExtendedAcl { [CmdletBinding()] param($VMName, $Direction, $Action, $RemoteIPAddress, $Weight) }
    function global:Start-VM { [CmdletBinding()] param($Name) }
    function global:Get-VM { [CmdletBinding()] param($Name) }
    function global:Stop-VM { [CmdletBinding()] param($Name, [switch]$TurnOff, [switch]$Force) }
    function global:Remove-VM { [CmdletBinding()] param($Name, [switch]$Force) }
    function global:Get-VMIntegrationService { [CmdletBinding()] param($VMName, $Name) }
    function global:Merge-VHD { [CmdletBinding()] param($Path, $DestinationPath) }
    . (Join-Path $PSScriptRoot '..' 'adapters' 'HyperV.ps1')

    $script:config = @{
        GoldenPath = 'C:\slate-ci\golden\win11-runner.vhdx'
        CacheDir   = Join-Path $TestDrive 'cache'
        VmDir      = Join-Path $TestDrive 'vms'
        SwitchName = 'slate-ci'
        Vcpu       = 4
        MemoryGB   = 12
        Lanes      = @{ app = @{ Cache = $true; MaxMinutes = 100 }; shell = @{ Cache = $false; MaxMinutes = 30 } }
    }
    New-Item -ItemType Directory -Force -Path $script:config.CacheDir, $script:config.VmDir | Out-Null
}

Describe 'Get-ExtendedAclRules' {
    It 'denies every private, CGNAT, link-local and IPv6 range both ways above a catch-all allow' {
        $rules = @(Get-ExtendedAclRules)
        $rules.Count | Should -Be 14
        $denies = @($rules | Where-Object Action -eq 'Deny')
        $denies.Count | Should -Be 12
        foreach ($range in '10.0.0.0/8', '172.16.0.0/12', '192.168.0.0/16', '100.64.0.0/10', '169.254.0.0/16', '::/0') {
            foreach ($direction in 'Outbound', 'Inbound') {
                @($denies | Where-Object { $_.RemoteIPAddress -eq $range -and $_.Direction -eq $direction }).Count | Should -Be 1
            }
        }
        ($denies | ForEach-Object { $_.Weight } | Measure-Object -Minimum).Minimum | Should -BeGreaterThan 1
        $allows = @($rules | Where-Object Action -eq 'Allow')
        $allows.Count | Should -Be 2
        foreach ($a in $allows) { $a.Weight | Should -Be 1; $a.RemoteIPAddress | Should -Be '0.0.0.0/0' }
    }
}

Describe 'ConvertTo-VmStateLabel' {
    It 'maps Hyper-V states to the three the orchestrator understands' {
        ConvertTo-VmStateLabel -State 'Off' | Should -Be 'Off'
        ConvertTo-VmStateLabel -State 'Running' | Should -Be 'Running'
        ConvertTo-VmStateLabel -State 'Starting' | Should -Be 'Running'
        ConvertTo-VmStateLabel -State 'Stopping' | Should -Be 'Running'
        ConvertTo-VmStateLabel -State 'Saved' | Should -Be 'Other'
        ConvertTo-VmStateLabel -State 'Paused' | Should -Be 'Other'
    }
}

Describe 'cache generation files' {
    It 'reads 0 when absent, then round-trips' {
        Get-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' | Should -Be 0
        Set-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' -Value 5
        Get-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' | Should -Be 5
    }
}

Describe 'New-RunnerVm' {
    BeforeEach {
        Mock New-VHD {}; Mock New-VM {}; Mock Set-VM {}; Mock Set-VMFirmware {}; Mock Set-VMKeyProtector {}
        Mock Enable-VMTPM {}; Mock Set-VMVideo {}; Mock Add-VMHardDiskDrive {}; Mock Add-VMNetworkAdapterExtendedAcl {}
    }
    It 'forks the golden and the lane cache, defines a 4 vCPU / 12 GB Gen2 VM with vTPM and 14 ACL rules' {
        Set-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' -Value 7
        $r = New-RunnerVm -Name 'slate-win-app-deadbeef' -Lane 'app' -Config $config
        $r.Dir | Should -Be (Join-Path $config.VmDir 'slate-win-app-deadbeef')
        $r.CachePath | Should -Be (Join-Path $r.Dir 'cache.vhdx')
        $r.ForkGeneration | Should -Be 7
        Should -Invoke New-VHD -Times 1 -ParameterFilter { $Path -eq (Join-Path $r.Dir 'os.vhdx') -and $ParentPath -eq 'C:\slate-ci\golden\win11-runner.vhdx' -and $Differencing }
        Should -Invoke New-VHD -Times 1 -ParameterFilter { $Path -eq $r.CachePath -and $ParentPath -eq (Join-Path $config.CacheDir 'app.vhdx') -and $Differencing }
        Should -Invoke New-VM -Times 1 -ParameterFilter { $Name -eq 'slate-win-app-deadbeef' -and $Generation -eq 2 -and $MemoryStartupBytes -eq 12GB -and $SwitchName -eq 'slate-ci' }
        Should -Invoke Set-VM -Times 1 -ParameterFilter { $ProcessorCount -eq 4 -and $StaticMemory -and $AutomaticStopAction -eq 'TurnOff' -and $CheckpointType -eq 'Disabled' }
        Should -Invoke Set-VMKeyProtector -Times 1 -ParameterFilter { $NewLocalKeyProtector }
        Should -Invoke Enable-VMTPM -Times 1
        Should -Invoke Set-VMVideo -Times 1 -ParameterFilter { $HorizontalResolution -eq 1920 -and $VerticalResolution -eq 1080 }
        Should -Invoke Add-VMHardDiskDrive -Times 1 -ParameterFilter { $Path -eq $r.CachePath }
        Should -Invoke Add-VMNetworkAdapterExtendedAcl -Times 14 -Exactly
    }
    It 'gives the shell lane no cache disk' {
        $r = New-RunnerVm -Name 'slate-win-shell-deadbeef' -Lane 'shell' -Config $config
        $r.CachePath | Should -BeNullOrEmpty
        $r.ForkGeneration | Should -Be 0
        Should -Invoke New-VHD -Times 1 -Exactly
        Should -Invoke Add-VMHardDiskDrive -Times 0
    }
    It 'fails loudly when the vTPM cannot be enabled' {
        Mock Enable-VMTPM { throw 'no key protector' }
        { New-RunnerVm -Name 'slate-win-app-cafecafe' -Lane 'app' -Config $config } | Should -Throw '*vTPM*'
    }
}

Describe 'Get-RunnerVmState' {
    It 'reports Missing for an absent VM and maps states otherwise' {
        Mock Get-VM { throw 'not found' }
        Get-RunnerVmState -Name 'x' | Should -Be 'Missing'
        Mock Get-VM { [pscustomobject]@{ State = 'Off' } }
        Get-RunnerVmState -Name 'x' | Should -Be 'Off'
        Mock Get-VM { [pscustomobject]@{ State = 'Saved' } }
        Get-RunnerVmState -Name 'x' | Should -Be 'Other'
    }
}

Describe 'Get-RunnerVmHeartbeat' {
    It 'is OK only when the integration service says OK' {
        Mock Get-VMIntegrationService { [pscustomobject]@{ PrimaryStatusDescription = 'OK' } }
        Get-RunnerVmHeartbeat -Name 'x' | Should -Be 'OK'
        Mock Get-VMIntegrationService { [pscustomobject]@{ PrimaryStatusDescription = 'No Contact' } }
        Get-RunnerVmHeartbeat -Name 'x' | Should -Be 'NoContact'
    }
}

Describe 'Merge-RunnerCache' {
    It 'merges the child into the lane parent and bumps the generation' {
        Mock Merge-VHD {}
        Set-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' -Value 7
        Merge-RunnerCache -Config $config -Lane 'app' -ChildPath 'C:\slate-ci\vms\x\cache.vhdx' | Should -Be 8
        Should -Invoke Merge-VHD -Times 1 -ParameterFilter { $Path -eq 'C:\slate-ci\vms\x\cache.vhdx' -and $DestinationPath -eq (Join-Path $config.CacheDir 'app.vhdx') }
        Get-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' | Should -Be 8
    }
}

Describe 'Remove-RunnerVm / Clear-RunnerVmDirs / Remove-RunnerCache' {
    It 'removes the VM when present and always deletes the directory' {
        Mock Get-VM { $null }
        Mock Remove-VM {}
        $dir = Join-Path $config.VmDir 'slate-win-app-11111111'
        New-Item -ItemType Directory -Force $dir | Out-Null
        Remove-RunnerVm -Name 'slate-win-app-11111111' -Dir $dir
        Test-Path $dir | Should -BeFalse
        Should -Invoke Remove-VM -Times 0
    }
    It 'clears directories that are not active' {
        $keep = Join-Path $config.VmDir 'slate-win-app-keep0000'
        $drop = Join-Path $config.VmDir 'slate-win-app-drop0000'
        New-Item -ItemType Directory -Force $keep, $drop | Out-Null
        Clear-RunnerVmDirs -VmDir $config.VmDir -ActiveNames @('slate-win-app-keep0000')
        Test-Path $keep | Should -BeTrue
        Test-Path $drop | Should -BeFalse
    }
    It 'deletes a cache child file and tolerates a missing one' {
        $child = Join-Path $TestDrive 'child.vhdx'
        'x' | Set-Content $child
        Remove-RunnerCache -ChildPath $child
        Test-Path $child | Should -BeFalse
        { Remove-RunnerCache -ChildPath $child } | Should -Not -Throw
        { Remove-RunnerCache -ChildPath $null } | Should -Not -Throw
    }
}

Describe 'New-HyperVAdapters' {
    It 'exposes exactly the keys the orchestrator invokes' {
        @((New-HyperVAdapters -Config $config).Keys | Sort-Object) | Should -Be @('CleanVmDirs', 'CommitCache', 'DiscardCache', 'GetGeneration', 'GetHeartbeat', 'GetVmState', 'ListVms', 'NewVm', 'RemoveVm', 'SendKvp', 'StartVm', 'StopVmForce')
    }
    It 'binds the config into the closures' {
        Set-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' -Value 3
        $a = New-HyperVAdapters -Config $config
        (& $a.GetGeneration 'app') | Should -Be 3
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1 -Path scripts/ci-host/tests/HyperVAdapter.Tests.ps1
```

Expected: FAIL, adapter file missing.

- [ ] **Step 3: Write the adapter**

`scripts/ci-host/adapters/HyperV.ps1`:

```powershell
#Requires -Version 7
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Hyper-V adapter for the self-hosted runner host: the only file that
# touches Hyper-V. Runs as slate-ci-host (Hyper-V Administrators, not an
# admin). The pure helpers are unit-tested with stubbed cmdlets; the
# wrappers are exercised by the host integration pass in
# docs/runbooks/self-hosted-windows-runner.md.

function Get-ExtendedAclRules {
    # Higher weight is evaluated first. Deny every private, CGNAT
    # (Tailscale), link-local and IPv6 range in both directions, then
    # allow the rest (the Internet). ARP is not IP, so the NAT gateway
    # still resolves as a next hop while 10.77.0.1 itself is unreachable.
    [CmdletBinding()]
    param([string[]]$DenyRanges = @('10.0.0.0/8', '172.16.0.0/12', '192.168.0.0/16', '100.64.0.0/10', '169.254.0.0/16'))
    $rules = @()
    $weight = 200
    foreach ($range in ($DenyRanges + @('::/0'))) {
        foreach ($direction in 'Outbound', 'Inbound') {
            $rules += @{ Direction = $direction; Action = 'Deny'; RemoteIPAddress = $range; Weight = $weight }
            $weight--
        }
    }
    foreach ($direction in 'Outbound', 'Inbound') {
        $rules += @{ Direction = $direction; Action = 'Allow'; RemoteIPAddress = '0.0.0.0/0'; Weight = 1 }
    }
    return $rules
}

function ConvertTo-VmStateLabel {
    [CmdletBinding()]
    param([string]$State)
    switch ($State) {
        'Off'      { return 'Off' }
        'Running'  { return 'Running' }
        'Starting' { return 'Running' }
        'Stopping' { return 'Running' }
        default    { return 'Other' }
    }
}

function Get-CacheGeneration {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CacheDir, [Parameter(Mandatory)][string]$Lane)
    $path = Join-Path $CacheDir "$Lane.gen"
    if (-not (Test-Path -LiteralPath $path)) { return 0 }
    return [int](Get-Content -Raw -LiteralPath $path).Trim()
}

function Set-CacheGeneration {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CacheDir, [Parameter(Mandatory)][string]$Lane, [Parameter(Mandatory)][int]$Value)
    Set-Content -LiteralPath (Join-Path $CacheDir "$Lane.gen") -Value ([string]$Value) -NoNewline
}

function New-RunnerVm {
    # Differencing children of the read-only golden disk and of the lane's
    # cache parent; Gen2, Secure Boot, fresh vTPM, static memory, no
    # checkpoints, TurnOff on host shutdown, port ACLs from the table.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$Lane, [Parameter(Mandatory)]$Config)
    $dir = Join-Path $Config.VmDir $Name
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $os = Join-Path $dir 'os.vhdx'
    New-VHD -Path $os -ParentPath $Config.GoldenPath -Differencing | Out-Null
    $cache = $null
    $generation = 0
    if ($Config.Lanes[$Lane].Cache) {
        $generation = Get-CacheGeneration -CacheDir $Config.CacheDir -Lane $Lane
        $cache = Join-Path $dir 'cache.vhdx'
        New-VHD -Path $cache -ParentPath (Join-Path $Config.CacheDir "$Lane.vhdx") -Differencing | Out-Null
    }
    New-VM -Name $Name -Generation 2 -MemoryStartupBytes ([int64]$Config.MemoryGB * 1GB) -VHDPath $os -SwitchName $Config.SwitchName -Path $dir | Out-Null
    Set-VM -Name $Name -ProcessorCount ([int]$Config.Vcpu) -StaticMemory -AutomaticStopAction TurnOff -AutomaticStartAction Nothing -AutomaticCheckpointsEnabled $false -CheckpointType Disabled
    Set-VMFirmware -VMName $Name -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows
    try {
        Set-VMKeyProtector -VMName $Name -NewLocalKeyProtector
        Enable-VMTPM -VMName $Name
    } catch {
        throw "${Name}: vTPM could not be enabled ($_). Grant slate-ci-host the key-protector right or fix the Hyper-V host guardian local mode; see the runbook."
    }
    Set-VMVideo -VMName $Name -ResolutionType Single -HorizontalResolution 1920 -VerticalResolution 1080
    if ($cache) { Add-VMHardDiskDrive -VMName $Name -Path $cache }
    foreach ($rule in Get-ExtendedAclRules) { Add-VMNetworkAdapterExtendedAcl -VMName $Name @rule }
    return @{ Dir = $dir; CachePath = $cache; ForkGeneration = $generation }
}

function Start-RunnerVm {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name)
    Start-VM -Name $Name | Out-Null
}

function Get-RunnerVmState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name)
    try { $vm = Get-VM -Name $Name -ErrorAction Stop } catch { return 'Missing' }
    if ($null -eq $vm) { return 'Missing' }
    return (ConvertTo-VmStateLabel -State ([string]$vm.State))
}

function Get-RunnerVmHeartbeat {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name)
    $service = Get-VMIntegrationService -VMName $Name -Name 'Heartbeat' -ErrorAction Stop
    if ([string]$service.PrimaryStatusDescription -eq 'OK') { return 'OK' }
    return 'NoContact'
}

function Send-RunnerVmKvp {
    # Host-to-guest key/value pairs land in the guest registry under
    # HKLM\SOFTWARE\Microsoft\Virtual Machine\External. Each Data value is
    # at most 1024 characters (the module chunks at 1000).
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][System.Collections.IDictionary]$Items)
    $ns = 'root\virtualization\v2'
    $vmms = Get-CimInstance -Namespace $ns -ClassName Msvm_VirtualSystemManagementService
    $vm = Get-CimInstance -Namespace $ns -ClassName Msvm_ComputerSystem -Filter "ElementName='$Name'"
    if ($null -eq $vm) { throw "KVP: VM $Name not found in WMI" }
    $serializer = [Microsoft.Management.Infrastructure.Serialization.CimSerializer]::Create()
    $payload = @()
    foreach ($key in $Items.Keys) {
        $item = New-CimInstance -Namespace $ns -ClassName Msvm_KvpExchangeDataItem -ClientOnly -Property @{
            Name = [string]$key; Data = [string]$Items[$key]; Source = [uint16]0
        }
        $bytes = $serializer.Serialize($item, [Microsoft.Management.Infrastructure.Serialization.InstanceSerializationOptions]::None)
        $payload += [System.Text.Encoding]::Unicode.GetString($bytes)
    }
    $result = Invoke-CimMethod -InputObject $vmms -MethodName AddKvpItems -Arguments @{ TargetSystem = $vm; DataItems = [string[]]$payload }
    if ($result.ReturnValue -eq 4096) {
        $job = $result.Job
        $deadline = (Get-Date).AddSeconds(60)
        while ($job.JobState -lt 7 -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 250
            $job = Get-CimInstance -InputObject $job
        }
        if ($job.JobState -ne 7) { throw "KVP: AddKvpItems job state $($job.JobState): $($job.ErrorDescription)" }
    } elseif ($result.ReturnValue -ne 0) {
        throw "KVP: AddKvpItems returned $($result.ReturnValue)"
    }
}

function Stop-RunnerVmForce {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name)
    Stop-VM -Name $Name -TurnOff -Force -ErrorAction SilentlyContinue | Out-Null
}

function Remove-RunnerVm {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [string]$Dir)
    if ($null -ne (Get-VM -Name $Name -ErrorAction SilentlyContinue)) { Remove-VM -Name $Name -Force }
    if ($Dir -and (Test-Path -LiteralPath $Dir)) { Remove-Item -LiteralPath $Dir -Recurse -Force }
}

function Get-RunnerVmNames {
    [CmdletBinding()]
    param()
    return @(Get-VM | Where-Object { $_.Name -like 'slate-win-*' } | ForEach-Object { $_.Name })
}

function Clear-RunnerVmDirs {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$VmDir, [string[]]$ActiveNames = @())
    if (-not (Test-Path -LiteralPath $VmDir)) { return }
    foreach ($entry in Get-ChildItem -LiteralPath $VmDir -Directory) {
        if ($ActiveNames -notcontains $entry.Name) { Remove-Item -LiteralPath $entry.FullName -Recurse -Force }
    }
}

function Merge-RunnerCache {
    # Child → parent merge, then the generation counter moves. Only the
    # orchestrator (single process) writes these files.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)][string]$Lane, [Parameter(Mandatory)][string]$ChildPath)
    $parent = Join-Path $Config.CacheDir "$Lane.vhdx"
    Merge-VHD -Path $ChildPath -DestinationPath $parent
    $next = (Get-CacheGeneration -CacheDir $Config.CacheDir -Lane $Lane) + 1
    Set-CacheGeneration -CacheDir $Config.CacheDir -Lane $Lane -Value $next
    return $next
}

function Remove-RunnerCache {
    [CmdletBinding()]
    param([string]$ChildPath)
    if ($ChildPath -and (Test-Path -LiteralPath $ChildPath)) { Remove-Item -LiteralPath $ChildPath -Force }
}

function New-HyperVAdapters {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config)
    $adapters = @{
        NewVm         = { param($name, $lane) New-RunnerVm -Name $name -Lane $lane -Config $Config }
        StartVm       = { param($name) Start-RunnerVm -Name $name }
        GetVmState    = { param($name) Get-RunnerVmState -Name $name }
        GetHeartbeat  = { param($name) Get-RunnerVmHeartbeat -Name $name }
        SendKvp       = { param($name, $items) Send-RunnerVmKvp -Name $name -Items $items }
        StopVmForce   = { param($name) Stop-RunnerVmForce -Name $name }
        RemoveVm      = { param($name, $dir) Remove-RunnerVm -Name $name -Dir $dir }
        ListVms       = { Get-RunnerVmNames }
        CleanVmDirs   = { param($active) Clear-RunnerVmDirs -VmDir $Config.VmDir -ActiveNames @($active) }
        GetGeneration = { param($lane) Get-CacheGeneration -CacheDir $Config.CacheDir -Lane $lane }
        CommitCache   = { param($lane, $child) Merge-RunnerCache -Config $Config -Lane $lane -ChildPath $child }
        DiscardCache  = { param($child) Remove-RunnerCache -ChildPath $child }
    }
    # Bind $Config into each scriptblock: the module invokes them long
    # after this function has returned.
    foreach ($key in @($adapters.Keys)) { $adapters[$key] = $adapters[$key].GetNewClosure() }
    return $adapters
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: every test in the suite passes.

- [ ] **Step 5: Commit**

```bash
git add scripts/ci-host/adapters/HyperV.ps1 scripts/ci-host/tests/HyperVAdapter.Tests.ps1
git commit -m "feat(ci-host): Hyper-V adapter — forked disks, Gen2 vTPM VM, port ACLs, KVP, cache merge"
```

---

### Task 8: Orchestrator state machine, startup sweep and the loop script

**Files:**
- Modify: `scripts/ci-host/SlateCiHost.psm1`
- Create: `scripts/ci-host/tests/Orchestrator.Tests.ps1`
- Create: `scripts/ci-host/orchestrator.ps1`

**Interfaces:**
- Consumes: every function from Tasks 2–5; adapter hashtables from Tasks 6–7 merged into one `$Adapters` plus `Log = { param($level, $message) }`.
- Produces: `Invoke-OrchestratorTick -Config -Journal -Adapters [hashtable] -Now [datetimeoffset]`; `Invoke-StartupSweep -Config -Journal -Adapters`; helpers `Get-FreeSlots`, `Remove-ActiveVm`, `Complete-ActiveVm`, `Update-ActiveVm`, `Invoke-Admission` (exported for tests).
- Journal VM entry written here: `@{ Name; Lane; Slot; SlotIp; JobId; RunId; Dir; CachePath; ForkGeneration; Phase; StartedAt; HandedAt; RunnerId; Claimed }`.
- KVP items sent: `slate.jit.count`, `slate.jit.<n>`, `slate.lane`, `slate.ip`, `slate.gateway`, `slate.dns`, `slate.cache` (`'1'|'0'`), `slate.job`.

- [ ] **Step 1: Write the failing tests with fake adapters**

`scripts/ci-host/tests/Orchestrator.Tests.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force

    function New-World {
        # Mutable fake of GitHub + Hyper-V; the adapters read it.
        $script:world = @{
            Queued     = @()
            Jobs       = @{}
            Runs       = @{}
            VmStates   = @{}
            Heartbeat  = 'OK'
            Runner     = [pscustomobject]@{ status = 'online'; busy = $false }
            Runners    = @()
            Vms        = @()
            Generation = 3
            JitConfig  = ('j' * 2500)
        }
        $script:calls = [System.Collections.ArrayList]::new()
        $script:logs = [System.Collections.ArrayList]::new()
    }

    function New-FakeAdapters {
        # The module invokes these long after this function returned, so
        # everything they touch lives at $script: scope (never a local).
        $script:record = { param($name, $arguments) [void]$script:calls.Add(@{ Name = $name; Args = @($arguments) }) }
        $a = @{
            GetQueuedJobs = { & $script:record 'GetQueuedJobs' @(); @($script:world.Queued) }
            GetJob        = { param($id) & $script:record 'GetJob' @($id); $script:world.Jobs[[string]$id] }
            GetRun        = { param($id) & $script:record 'GetRun' @($id); $script:world.Runs[[string]$id] }
            NewJitRunner  = { param($name, $labels) & $script:record 'NewJitRunner' @($name, $labels); @{ RunnerId = 77; EncodedJitConfig = $script:world.JitConfig } }
            RemoveRunner  = { param($id) & $script:record 'RemoveRunner' @($id) }
            GetRunner     = { param($id) & $script:record 'GetRunner' @($id); $script:world.Runner }
            ListRunners   = { & $script:record 'ListRunners' @(); @($script:world.Runners) }
            NewVm         = { param($name, $lane) & $script:record 'NewVm' @($name, $lane)
                              $cache = $null; if ($lane -ne 'shell') { $cache = "C:\slate-ci\vms\$name\cache.vhdx" }
                              @{ Dir = "C:\slate-ci\vms\$name"; CachePath = $cache; ForkGeneration = $script:world.Generation } }
            StartVm       = { param($name) & $script:record 'StartVm' @($name) }
            GetVmState    = { param($name) & $script:record 'GetVmState' @($name); if ($script:world.VmStates.ContainsKey($name)) { $script:world.VmStates[$name] } else { 'Running' } }
            GetHeartbeat  = { param($name) & $script:record 'GetHeartbeat' @($name); $script:world.Heartbeat }
            SendKvp       = { param($name, $items) & $script:record 'SendKvp' @($name, $items) }
            StopVmForce   = { param($name) & $script:record 'StopVmForce' @($name) }
            RemoveVm      = { param($name, $dir) & $script:record 'RemoveVm' @($name, $dir) }
            ListVms       = { & $script:record 'ListVms' @(); @($script:world.Vms) }
            CleanVmDirs   = { param($active) & $script:record 'CleanVmDirs' @($active) }
            GetGeneration = { param($lane) & $script:record 'GetGeneration' @($lane); $script:world.Generation }
            CommitCache   = { param($lane, $child) & $script:record 'CommitCache' @($lane, $child); $script:world.Generation + 1 }
            DiscardCache  = { param($child) & $script:record 'DiscardCache' @($child) }
            Log           = { param($level, $message) [void]$script:logs.Add("[$level] $message") }
        }
        return $a
    }

    function Get-Calls([string]$Name) { @($script:calls | Where-Object { $_.Name -eq $Name }) }

    function New-QueuedJob([int64]$Id, [int64]$RunId, [string]$Lane, [string]$Created = '2026-10-10T12:00:00Z') {
        [pscustomobject]@{ id = $Id; run_id = $RunId; status = 'queued'; labels = @('self-hosted', "slate-win-$Lane"); created_at = $Created }
    }

    function Set-DoneJob([int64]$Id, [int64]$RunId, [string]$RunnerName, [string]$Conclusion = 'success') {
        $script:world.Jobs[[string]$Id] = [pscustomobject]@{ id = $Id; run_id = $RunId; status = 'completed'; conclusion = $Conclusion; runner_name = $RunnerName }
    }

    function Set-Run([int64]$RunId, [string]$Event = 'push', [string]$Branch = 'main', [string]$Repo = 'coryj627/slate') {
        $script:world.Runs[[string]$RunId] = [pscustomobject]@{ id = $RunId; event = $Event; head_branch = $Branch; head_repository = [pscustomobject]@{ full_name = $Repo } }
    }

    $script:t0 = [datetimeoffset]::Parse('2026-10-10T12:00:00Z')
}

Describe 'orchestrator' {

BeforeEach {
    New-World
    $script:config = Get-CiHostConfig -Path (Join-Path $PSScriptRoot '..' 'config.json')
    $script:journal = New-Journal
    $script:adapters = New-FakeAdapters
}

Context 'admission' {
    It 'admits a queued app job into slot 1 and starts a VM' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'NewVm').Count | Should -Be 1
        (Get-Calls 'NewVm')[0].Args[1] | Should -Be 'app'
        (Get-Calls 'StartVm').Count | Should -Be 1
        $journal.Vms.Count | Should -Be 1
        $vm = @($journal.Vms.Values)[0]
        $vm.Phase | Should -Be 'provisioned'
        $vm.JobId | Should -Be 1
        $vm.Slot | Should -Be 1
        $vm.SlotIp | Should -Be '10.77.0.11'
        $vm.ForkGeneration | Should -Be 3
        $journal.SeenJobs['1'].Lane | Should -Be 'app'
    }
    It 'never runs more VMs than slots' {
        $world.Queued = @((New-QueuedJob 1 10 'app' '2026-10-10T11:00:00Z'), (New-QueuedJob 2 10 'rust' '2026-10-10T11:00:01Z'), (New-QueuedJob 3 10 'model' '2026-10-10T11:00:02Z'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.Vms.Count | Should -Be 2
        @($journal.Vms.Values | ForEach-Object { $_.Slot } | Sort-Object) | Should -Be @(1, 2)
        @($journal.Vms.Values | ForEach-Object { $_.JobId } | Sort-Object) | Should -Be @(1, 2)
    }
    It 'does not admit a job at the retry cap' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $journal.Retries['1'] = @{ Count = 3; NextAt = $t0.AddDays(-1).ToString('o') }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.Vms.Count | Should -Be 0
    }
    It 'survives a GetQueuedJobs failure' {
        $adapters.GetQueuedJobs = { throw 'api down' }
        { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0 } | Should -Not -Throw
        ($logs -join "`n") | Should -Match 'admission: api down'
    }
    It 'registers a retry and cleans up when provisioning throws' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $adapters.NewVm = { throw 'disk full' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.Vms.Count | Should -Be 0
        $journal.Retries['1'].Count | Should -Be 1
        (Get-Calls 'RemoveVm').Count | Should -Be 1
    }
}

Context 'handoff' {
    BeforeEach {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $world.Heartbeat = 'NoContact'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $script:name = @($journal.Vms.Keys)[0]
    }
    It 'registers a JIT runner with the lane label and sends chunked KVP once the heartbeat is OK' {
        $world.Heartbeat = 'OK'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        $reg = (Get-Calls 'NewJitRunner')[0]
        $reg.Args[0] | Should -Be $name
        @($reg.Args[1]) | Should -Be @('slate-win-app')
        $kvp = (Get-Calls 'SendKvp')[0].Args[1]
        $kvp['slate.jit.count'] | Should -Be '3'
        $kvp['slate.jit.0'].Length | Should -Be 1000
        $kvp['slate.lane'] | Should -Be 'app'
        $kvp['slate.ip'] | Should -Be '10.77.0.11'
        $kvp['slate.gateway'] | Should -Be '10.77.0.1'
        $kvp['slate.dns'] | Should -Be '1.1.1.1,8.8.8.8'
        $kvp['slate.cache'] | Should -Be '1'
        $kvp['slate.job'] | Should -Be '1'
        $journal.Vms[$name].Phase | Should -Be 'handed'
        $journal.Vms[$name].RunnerId | Should -Be 77
        $journal.Vms[$name].HandedAt | Should -Not -BeNullOrEmpty
    }
    It 'keeps waiting for a heartbeat inside the timeout' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(179)
        $journal.Vms[$name].Phase | Should -Be 'provisioned'
        (Get-Calls 'NewJitRunner').Count | Should -Be 0
    }
    It 'discards and retries when no heartbeat arrives within the timeout' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(180)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'StopVmForce').Count | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        (Get-Calls 'CommitCache').Count | Should -Be 0
        (Get-Calls 'RemoveVm').Count | Should -Be 1
        $journal.Retries['1'].Count | Should -Be 1
    }
    It 'discards and retries when the VM has vanished' {
        $world.VmStates[$name] = 'Missing'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        $journal.Vms.Count | Should -Be 0
        $journal.Retries['1'].Count | Should -Be 1
    }
}

Context 'settle' {
    BeforeEach {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0                   # tick 1: provision
        $script:name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)   # tick 2: handoff (heartbeat OK)
        $journal.Vms[$name].Phase | Should -Be 'handed'
        $world.Queued = @()
        $world.VmStates[$name] = 'Off'
    }
    It 'commits the cache after a green push to main with an unchanged generation' {
        Set-DoneJob 1 10 $name
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'CommitCache').Count | Should -Be 1
        (Get-Calls 'CommitCache')[0].Args[0] | Should -Be 'app'
        (Get-Calls 'DiscardCache').Count | Should -Be 0
        (Get-Calls 'RemoveVm').Count | Should -Be 1
        $journal.Vms.Count | Should -Be 0
        $journal.SeenJobs.Count | Should -Be 0
        ($logs -join "`n") | Should -Match 'commit app generation 4'
    }
    It 'discards the cache for a pull_request run' {
        Set-DoneJob 1 10 $name
        Set-Run 10 'pull_request'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'CommitCache').Count | Should -Be 0
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        ($logs -join "`n") | Should -Match 'discard \(event: pull_request\)'
    }
    It 'discards when the parent generation moved' {
        Set-DoneJob 1 10 $name
        Set-Run 10
        $world.Generation = 4
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'CommitCache').Count | Should -Be 0
        ($logs -join "`n") | Should -Match 'generation moved'
    }
    It 'resolves the job through a candidate when another queued job took the runner' {
        # Job 1 ran elsewhere; job 2 (seen queued earlier) actually ran here.
        $journal.SeenJobs['2'] = @{ RunId = 11; Lane = 'app'; FirstSeenAt = $t0.ToString('o') }
        Set-DoneJob 1 10 'slate-win-app-elsewhere'
        Set-DoneJob 2 11 $name
        Set-Run 11
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'CommitCache').Count | Should -Be 1
        $journal.SeenJobs.ContainsKey('2') | Should -BeFalse
    }
    It 'leaves the Off VM in the journal when the API fails during settle and settles on the next tick' {
        $adapters.GetJob = { param($id) $script:flakyCalls++; if ($script:flakyCalls -eq 1) { throw '502' }; $script:world.Jobs[[string]$id] }
        $script:flakyCalls = 0
        Set-DoneJob 1 10 $name
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        $journal.Vms.Count | Should -Be 1
        (Get-Calls 'CommitCache').Count | Should -Be 0
        (Get-Calls 'DiscardCache').Count | Should -Be 0
        (Get-Calls 'RemoveVm').Count | Should -Be 0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(21)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'CommitCache').Count | Should -Be 1
    }
    It 'discards when the job failed' {
        Set-DoneJob 1 10 $name 'failure'
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        $journal.Retries.Count | Should -Be 0
    }
}

Context 'shell lane' {
    It 'has no cache: neither commit nor discard on completion' {
        $world.Queued = @((New-QueuedJob 5 50 'shell'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0                   # provision
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)   # handoff
        (Get-Calls 'SendKvp')[0].Args[1]['slate.cache'] | Should -Be '0'
        $world.Queued = @()
        $world.VmStates[$name] = 'Off'
        Set-DoneJob 5 50 $name
        Set-Run 50
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(5)
        (Get-Calls 'CommitCache').Count | Should -Be 0
        (Get-Calls 'DiscardCache').Count | Should -Be 0
        (Get-Calls 'GetGeneration').Count | Should -Be 0
        $journal.Vms.Count | Should -Be 0
    }
}

Context 'handed-phase guards' {
    BeforeEach {
        $world.Queued = @((New-QueuedJob 1 10 'model'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0                   # provision
        $script:name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)   # handoff at t0+10s
        $journal.Vms[$name].Phase | Should -Be 'handed'
        $world.Queued = @()
    }
    It 'tears down an expired VM without committing and without a retry' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(110)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'StopVmForce').Count | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        (Get-Calls 'RemoveRunner').Count | Should -Be 1
        $journal.Retries.Count | Should -Be 0
    }
    # HandedAt is t0+10s, so the 300 s unclaimed check first fires at t0+310s.
    It 'does not check for a claim before the unclaimed timeout' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(200)
        (Get-Calls 'GetRunner').Count | Should -Be 0
        $journal.Vms[$name].Claimed | Should -BeFalse
    }
    It 'marks the runner claimed when GitHub reports it busy' {
        $world.Runner = [pscustomobject]@{ status = 'online'; busy = $true }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        $journal.Vms[$name].Claimed | Should -BeTrue
        (Get-Calls 'StopVmForce').Count | Should -Be 0
    }
    It 'tears down an unclaimed runner whose job vanished' {
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'completed'; conclusion = 'cancelled'; runner_name = $null }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'RemoveRunner').Count | Should -Be 1
        $journal.Retries.Count | Should -Be 0
    }
    It 'treats an in-progress job on this runner as claimed' {
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'in_progress'; conclusion = $null; runner_name = $name }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        $journal.Vms[$name].Claimed | Should -BeTrue
    }
    It 'keeps waiting while the job stays queued, then retries after three timeouts' {
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'queued'; conclusion = $null; runner_name = $null }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        $journal.Vms.Count | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(920)
        $journal.Vms.Count | Should -Be 0
        $journal.Retries['1'].Count | Should -Be 1
    }
}

Context 'Invoke-StartupSweep' {
    It 'removes stale slate-win runners and leftover VMs, cleans directories, resets the journal' {
        $world.Runners = @(
            [pscustomobject]@{ id = 1; name = 'slate-win-app-aaaaaaaa'; status = 'offline' },
            [pscustomobject]@{ id = 2; name = 'slate-win-rust-bbbbbbbb'; status = 'online' },
            [pscustomobject]@{ id = 3; name = 'unrelated'; status = 'offline' }
        )
        $world.Vms = @('slate-win-app-aaaaaaaa', 'slate-win-model-cccccccc')
        $journal.Vms['stale'] = @{ Name = 'stale' }
        $journal.Retries['9'] = @{ Count = 1; NextAt = $t0.ToString('o') }
        Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
        @((Get-Calls 'RemoveRunner') | ForEach-Object { $_.Args[0] }) | Should -Be @(1)
        (Get-Calls 'StopVmForce').Count | Should -Be 2
        (Get-Calls 'RemoveVm').Count | Should -Be 2
        (Get-Calls 'RemoveVm')[1].Args[1] | Should -Be 'C:\slate-ci\vms\slate-win-model-cccccccc'
        (Get-Calls 'CleanVmDirs').Count | Should -Be 1
        $journal.Vms.Count | Should -Be 0
        $journal.Retries.Count | Should -Be 0
    }
}

}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1 -Path scripts/ci-host/tests/Orchestrator.Tests.ps1
```

Expected: FAIL with "Invoke-OrchestratorTick is not recognized".

- [ ] **Step 3: Implement the state machine in the module**

Insert before `Export-ModuleMember`:

```powershell
# ---- orchestrator state machine (host-only; adapters injected) ----

function Get-FreeSlots {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal)
    $used = @{}
    foreach ($vm in $Journal.Vms.Values) { $used[[string]$vm.Slot] = $true }
    $free = @()
    foreach ($slot in @($Config.Slots | Sort-Object { [int]$_.Index })) {
        if (-not $used.ContainsKey([string]$slot.Index)) { $free += $slot }
    }
    return @($free)
}

function Remove-ActiveVm {
    # Every failure path: deregister, power off, drop the cache fork,
    # delete the VM, optionally schedule a retry. Never commits.
    [CmdletBinding()]
    param($Config, $Journal, [hashtable]$Adapters, [string]$Name, [string]$Reason, [bool]$Retry, [datetimeoffset]$Now)
    $vm = $Journal.Vms[$Name]
    & $Adapters.Log 'warn' "${Name}: discard ($Reason)"
    if ($vm.RunnerId) { try { & $Adapters.RemoveRunner $vm.RunnerId } catch { & $Adapters.Log 'warn' "${Name}: RemoveRunner: $_" } }
    try { & $Adapters.StopVmForce $Name } catch { & $Adapters.Log 'warn' "${Name}: StopVmForce: $_" }
    if ($vm.CachePath) { try { & $Adapters.DiscardCache $vm.CachePath } catch { & $Adapters.Log 'warn' "${Name}: DiscardCache: $_" } }
    try { & $Adapters.RemoveVm $Name $vm.Dir } catch { & $Adapters.Log 'warn' "${Name}: RemoveVm: $_" }
    if ($Retry -and $vm.JobId) {
        Register-JobRetry -Retries $Journal.Retries -JobId ([int64]$vm.JobId) -Now $Now -BackoffSeconds ([int]$Config.RetryBackoffSeconds)
    }
    $Journal.Vms.Remove($Name)
}

function Complete-ActiveVm {
    # The VM reached Off by itself. Ask GitHub what ran, then commit or
    # discard. An API failure propagates: the entry stays in the journal
    # and the next tick tries again (never orphan, never commit blindly).
    [CmdletBinding()]
    param($Config, $Journal, [hashtable]$Adapters, [string]$Name)
    $vm = $Journal.Vms[$Name]
    $candidates = @()
    foreach ($key in @($Journal.SeenJobs.Keys)) { $candidates += [int64]$key }
    $job = Resolve-RunnerJob -RunnerName $Name -AdmittedJobId ([int64]$vm.JobId) -CandidateJobIds $candidates -GetJob $Adapters.GetJob
    $run = $null
    if ($null -ne $job) { $run = & $Adapters.GetRun ([int64]$job.run_id) }
    if ($vm.CachePath) {
        $parentGeneration = [int](& $Adapters.GetGeneration $vm.Lane)
        $decision = Test-CommitEligible -Job $job -Run $run -RunnerName $Name -ForcedOff $false `
            -ParentGeneration $parentGeneration -ForkGeneration ([int]$vm.ForkGeneration) `
            -TrustedRepo $Config.TrustedRepo -TrustedBranch $Config.TrustedBranch -TrustedEvents @($Config.TrustedEvents)
        if ($decision.Eligible) {
            $generation = & $Adapters.CommitCache $vm.Lane $vm.CachePath
            & $Adapters.Log 'info' "${Name}: commit $($vm.Lane) generation $generation ($($decision.Reason))"
        } else {
            & $Adapters.DiscardCache $vm.CachePath
            & $Adapters.Log 'info' "${Name}: discard ($($decision.Reason))"
        }
    } else {
        $outcome = 'no job resolved'
        if ($null -ne $job) { $outcome = "conclusion: $($job.conclusion)" }
        & $Adapters.Log 'info' "${Name}: done, lane $($vm.Lane) has no cache ($outcome)"
    }
    & $Adapters.RemoveVm $Name $vm.Dir
    if ($null -ne $job) { $Journal.SeenJobs.Remove([string]$job.id) }
    $Journal.Vms.Remove($Name)
}

function Update-ActiveVm {
    [CmdletBinding()]
    param($Config, $Journal, [hashtable]$Adapters, [string]$Name, [datetimeoffset]$Now)
    $vm = $Journal.Vms[$Name]
    $state = & $Adapters.GetVmState $Name
    if ($state -eq 'Off') {
        Complete-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name
        return
    }
    if ($state -ne 'Running') {
        Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason "vm state $state" -Retry $true -Now $Now
        return
    }
    $startedAt = ConvertTo-DateTimeOffset -Value $vm.StartedAt
    if ($vm.Phase -eq 'provisioned') {
        $heartbeat = & $Adapters.GetHeartbeat $Name
        if ($heartbeat -eq 'OK') {
            $registration = & $Adapters.NewJitRunner $Name @("slate-win-$($vm.Lane)")
            $vm.RunnerId = $registration.RunnerId
            $items = Split-KvpChunks -Text ([string]$registration.EncodedJitConfig)
            $items['slate.lane'] = [string]$vm.Lane
            $items['slate.ip'] = [string]$vm.SlotIp
            $items['slate.gateway'] = [string]$Config.Gateway
            $items['slate.dns'] = [string]$Config.Dns
            $items['slate.cache'] = $(if ($vm.CachePath) { '1' } else { '0' })
            $items['slate.job'] = [string]$vm.JobId
            & $Adapters.SendKvp $Name $items
            $vm.Phase = 'handed'
            $vm.HandedAt = $Now.ToString('o')
            & $Adapters.Log 'info' "${Name}: handed off (runner $($registration.RunnerId), job $($vm.JobId))"
        } elseif ($Now -ge $startedAt.AddSeconds([int]$Config.HeartbeatTimeoutSeconds)) {
            Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason 'no heartbeat' -Retry $true -Now $Now
        }
        return
    }
    $maxMinutes = [int]$Config.Lanes[$vm.Lane].MaxMinutes
    if (Test-VmExpired -StartedAt $startedAt -MaxMinutes $maxMinutes -Now $Now) {
        Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason "expired after $maxMinutes min + grace" -Retry $false -Now $Now
        return
    }
    if (-not $vm.Claimed) {
        $handedAt = ConvertTo-DateTimeOffset -Value $vm.HandedAt
        $waited = ($Now - $handedAt).TotalSeconds
        $timeout = [int]$Config.UnclaimedTimeoutSeconds
        if ($waited -ge $timeout) {
            $runner = & $Adapters.GetRunner $vm.RunnerId
            if ($null -ne $runner -and $runner.busy) {
                $vm.Claimed = $true
                & $Adapters.Log 'info' "${Name}: claimed"
                return
            }
            $job = & $Adapters.GetJob ([int64]$vm.JobId)
            if ($null -ne $job -and $job.status -eq 'in_progress' -and [string]$job.runner_name -eq $Name) {
                $vm.Claimed = $true
                & $Adapters.Log 'info' "${Name}: claimed (job in progress)"
                return
            }
            if ($null -eq $job -or $job.status -ne 'queued') {
                Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason 'unclaimed and the admitted job is no longer queued' -Retry $false -Now $Now
            } elseif ($waited -ge 3 * $timeout) {
                Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason 'unclaimed while the job stays queued' -Retry $true -Now $Now
            }
        }
    }
}

function Invoke-Admission {
    [CmdletBinding()]
    param($Config, $Journal, [hashtable]$Adapters, [datetimeoffset]$Now)
    $raw = @(& $Adapters.GetQueuedJobs)
    $queued = @(Select-QueuedLaneJobs -Jobs $raw -Lanes @($Config.Lanes.Keys))
    foreach ($q in $queued) {
        $key = [string]$q.JobId
        if (-not $Journal.SeenJobs.Contains($key)) {
            $Journal.SeenJobs[$key] = @{ RunId = [int64]$q.RunId; Lane = $q.Lane; FirstSeenAt = $Now.ToString('o') }
        }
    }
    foreach ($key in @($Journal.SeenJobs.Keys)) {
        $seen = ConvertTo-DateTimeOffset -Value $Journal.SeenJobs[$key].FirstSeenAt
        if ($Now -gt $seen.AddHours(24)) { $Journal.SeenJobs.Remove($key) }
    }
    $free = @(Get-FreeSlots -Config $Config -Journal $Journal)
    $admit = @(Select-JobsToAdmit -Candidates $queued -ActiveVms $Journal.Vms -FreeSlots $free.Count -Retries $Journal.Retries -Now $Now -RetryCap ([int]$Config.RetryCap))
    for ($i = 0; $i -lt $admit.Count; $i++) {
        $job = $admit[$i]
        $slot = $free[$i]
        $name = New-RunnerName -Lane $job.Lane
        try {
            $created = & $Adapters.NewVm $name $job.Lane
            $Journal.Vms[$name] = @{
                Name = $name; Lane = $job.Lane; Slot = [int]$slot.Index; SlotIp = [string]$slot.Ip
                JobId = [int64]$job.JobId; RunId = [int64]$job.RunId
                Dir = [string]$created.Dir; CachePath = $created.CachePath; ForkGeneration = [int]$created.ForkGeneration
                Phase = 'provisioned'; StartedAt = $Now.ToString('o'); HandedAt = $null; RunnerId = $null; Claimed = $false
            }
            & $Adapters.StartVm $name
            & $Adapters.Log 'info' "${name}: provisioned for job $($job.JobId) (lane $($job.Lane), slot $($slot.Index), fork generation $($created.ForkGeneration))"
        } catch {
            & $Adapters.Log 'error' "${name}: provisioning failed: $_"
            if ($Journal.Vms.Contains($name)) {
                Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $name -Reason 'provisioning failed' -Retry $true -Now $Now
            } else {
                Register-JobRetry -Retries $Journal.Retries -JobId ([int64]$job.JobId) -Now $Now -BackoffSeconds ([int]$Config.RetryBackoffSeconds)
                try { & $Adapters.RemoveVm $name (Join-WinPath $Config.VmDir $name) } catch { & $Adapters.Log 'warn' "${name}: RemoveVm: $_" }
            }
        }
    }
}

function Invoke-OrchestratorTick {
    # One pass: advance or settle every active VM, then admit queued jobs.
    # A failure in one VM never blocks the others; a discovery failure
    # just means no admission this tick.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)]$Journal,
        [Parameter(Mandatory)][hashtable]$Adapters,
        [datetimeoffset]$Now = [datetimeoffset]::UtcNow
    )
    foreach ($name in @($Journal.Vms.Keys)) {
        try { Update-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $name -Now $Now }
        catch { & $Adapters.Log 'error' "${name}: $_" }
    }
    try { Invoke-Admission -Config $Config -Journal $Journal -Adapters $Adapters -Now $Now }
    catch { & $Adapters.Log 'error' "admission: $_" }
}

function Invoke-StartupSweep {
    # After a host restart nothing in the journal can be trusted: every
    # slate-win-* runner that is offline and every slate-win-* VM is
    # removed (cache children die with their VM directory; parents are
    # never touched) and the journal starts empty.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters)
    $runners = @(& $Adapters.ListRunners)
    foreach ($name in @(Get-StaleRunnerNames -Runners $runners)) {
        $runner = $runners | Where-Object { $_.name -eq $name } | Select-Object -First 1
        try { & $Adapters.RemoveRunner $runner.id; & $Adapters.Log 'info' "sweep: removed stale runner $name" }
        catch { & $Adapters.Log 'warn' "sweep: runner ${name}: $_" }
    }
    foreach ($name in @(& $Adapters.ListVms)) {
        try {
            & $Adapters.StopVmForce $name
            & $Adapters.RemoveVm $name (Join-WinPath $Config.VmDir $name)
            & $Adapters.Log 'info' "sweep: removed vm $name"
        } catch { & $Adapters.Log 'warn' "sweep: vm ${name}: $_" }
    }
    try { & $Adapters.CleanVmDirs @() } catch { & $Adapters.Log 'warn' "sweep: CleanVmDirs: $_" }
    $Journal.Vms = @{}
    $Journal.Retries = @{}
    $Journal.SeenJobs = @{}
}
```

Add `Get-FreeSlots, Remove-ActiveVm, Complete-ActiveVm, Update-ActiveVm, Invoke-Admission, Invoke-OrchestratorTick, Invoke-StartupSweep` to `Export-ModuleMember`.

- [ ] **Step 4: Run the tests to verify they pass**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: every test in the suite passes.

- [ ] **Step 5: Write the loop script**

`scripts/ci-host/orchestrator.ps1`:

```powershell
#Requires -Version 7
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# The self-hosted Windows runner host loop. Runs as slate-ci-host from
# the slate-ci-orchestrator scheduled task (install/setup-host.ps1).
# Wires the GitHub and Hyper-V adapters into the SlateCiHost module and
# ticks every TickSeconds. -Once runs a single tick (smoke test).
[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'config.json'),
    [switch]$Once
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SlateCiHost.psm1') -Force
. (Join-Path $PSScriptRoot 'adapters' 'GitHub.ps1')
. (Join-Path $PSScriptRoot 'adapters' 'HyperV.ps1')

$config = Get-CiHostConfig -Path $ConfigPath
$log = {
    param($level, $message)
    Write-CiLog -Path (Join-Path $config.LogDir ('orchestrator-{0}.log' -f (Get-Date -Format 'yyyy-MM-dd'))) -Level $level -Message $message
}

$tokenPath = Join-Path $config.StateDir 'token.xml'
if (-not (Test-Path -LiteralPath $tokenPath)) {
    & $log 'error' "token missing at $tokenPath (run install/store-token.ps1)"
    exit 2
}
Initialize-GitHubAdapter -Owner $config.Owner -Repo $config.Repo -Token (Import-Clixml -LiteralPath $tokenPath)

$adapters = @{}
foreach ($set in (New-GitHubAdapters), (New-HyperVAdapters -Config $config)) {
    foreach ($key in $set.Keys) { $adapters[$key] = $set[$key] }
}
$adapters['Log'] = $log

$journalPath = Join-Path $config.StateDir 'journal.json'
$journal = Read-Journal -Path $journalPath
& $log 'info' "orchestrator start (pid $PID, user $env:USERNAME, config $ConfigPath)"
Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
Write-Journal -Path $journalPath -Journal $journal

do {
    try { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now ([datetimeoffset]::UtcNow) }
    catch { & $log 'error' "tick: $_" }
    try { Write-Journal -Path $journalPath -Journal $journal }
    catch { & $log 'error' "journal: $_" }
    if (-not $Once) { Start-Sleep -Seconds ([int]$config.TickSeconds) }
} while (-not $Once)
```

- [ ] **Step 6: Syntax-check the loop script without running it**

```bash
pwsh -NoProfile -Command "[void][System.Management.Automation.Language.Parser]::ParseFile('scripts/ci-host/orchestrator.ps1', [ref]$null, [ref]$e); if ($e) { $e; exit 1 } else { 'ok' }"
```

Expected: `ok`.

- [ ] **Step 7: Commit**

```bash
git add scripts/ci-host/SlateCiHost.psm1 scripts/ci-host/tests/Orchestrator.Tests.ps1 scripts/ci-host/orchestrator.ps1
git commit -m "feat(ci-host): orchestrator state machine, startup sweep, loop script"
```

---

### Task 9: Guest bootstrap scripts

**Files:**
- Create: `scripts/ci-host/golden/guest/bootstrap-system.ps1`
- Create: `scripts/ci-host/golden/guest/bootstrap-runner.ps1`
- Modify: `scripts/ci-host/SlateCiHost.psm1` (add `Select-SlateKvpItems`)
- Create: `scripts/ci-host/tests/Guest.Tests.ps1`

**Interfaces:**
- Consumes: `Join-KvpChunks` (Task 2); KVP item names from Task 8; the golden image layout from Task 10 (`C:\actions-runner` for runtime files; the two scripts and `SlateCiHost.psm1` in `C:\slate-guest`, writable only by Administrators and SYSTEM; marker `C:\Users\runner\.slate-golden-complete`; cache volume label `slate-cache`).
- Produces: `Select-SlateKvpItems -Properties [psobject]` → hashtable of every `slate.*` property; files in `C:\actions-runner`: `.env`, `jit.cfg`, `ready`, `bootstrap-error.txt`, `bootstrap-system.log`, `bootstrap-runner.log`.
- Both scripts are Windows PowerShell 5.1 (the guest has no pwsh).

- [ ] **Step 1: Write the failing tests**

`scripts/ci-host/tests/Guest.Tests.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force
    $script:guestDir = Join-Path $PSScriptRoot '..' 'golden' 'guest'
    function Test-ScriptParses([string]$Path) {
        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$errors)
        return (@($errors).Count -eq 0)
    }
}

Describe 'Select-SlateKvpItems' {
    It 'keeps only slate.* properties as strings' {
        $props = [pscustomobject]@{ PSPath = 'x'; PSProvider = 'Registry'; 'slate.jit.count' = 2; 'slate.jit.0' = 'ab'; 'slate.jit.1' = 'cd'; 'slate.lane' = 'app'; OtherKey = 'ignored' }
        $items = Select-SlateKvpItems -Properties $props
        $items.Keys.Count | Should -Be 4
        $items['slate.jit.count'] | Should -BeOfType [string]
        Join-KvpChunks -Items $items | Should -Be 'abcd'
        $items.Contains('OtherKey') | Should -BeFalse
    }
    It 'returns an empty table when nothing matches' {
        (Select-SlateKvpItems -Properties ([pscustomobject]@{ PSPath = 'x' })).Keys.Count | Should -Be 0
    }
}

Describe 'guest scripts' {
    It 'parse as PowerShell' {
        Test-ScriptParses (Join-Path $guestDir 'bootstrap-system.ps1') | Should -BeTrue
        Test-ScriptParses (Join-Path $guestDir 'bootstrap-runner.ps1') | Should -BeTrue
    }
    It 'are no-ops without the golden completion marker' {
        foreach ($f in 'bootstrap-system.ps1', 'bootstrap-runner.ps1') {
            (Get-Content -Raw (Join-Path $guestDir $f)) | Should -Match '\.slate-golden-complete'
        }
    }
    It 'system bootstrap reads the External KVP key, labels the cache by volume label and writes the four env lines' {
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-system.ps1')
        $text | Should -Match 'Virtual Machine\\External'
        $text | Should -Match "FileSystemLabel 'slate-cache'"
        foreach ($v in 'NSC_CACHE_PATH', 'CARGO_TARGET_DIR', 'NUGET_PACKAGES', 'SLATE_CACHE_ROOT') { $text | Should -Match $v }
        $text | Should -Match 'Select-SlateKvpItems'
        $text | Should -Match 'Join-KvpChunks'
        $text | Should -Match 'Add-MpPreference -ExclusionPath \$root'
        $text | Should -Match 'C:\\slate-guest\\SlateCiHost\.psm1'
    }
    It 'runner bootstrap never recurses into a junction, launches run.cmd --jitconfig and always shuts down' {
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-runner.ps1')
        $text | Should -Match 'ReparsePoint'
        $text | Should -Match "'--jitconfig'"
        $text | Should -Match 'finally'
        $text | Should -Match 'shutdown\.exe /s /t 0'
    }
    It 'neither script contains PowerShell 7-only syntax' {
        foreach ($f in 'bootstrap-system.ps1', 'bootstrap-runner.ps1') {
            $text = Get-Content -Raw (Join-Path $guestDir $f)
            $text | Should -Not -Match '\?\?'
            $text | Should -Not -Match '-Parallel'
            $text | Should -Not -Match '-AsHashtable'
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1 -Path scripts/ci-host/tests/Guest.Tests.ps1
```

Expected: FAIL ("Select-SlateKvpItems is not recognized", files missing).

- [ ] **Step 3: Add the module helper**

Insert before `Export-ModuleMember` in `scripts/ci-host/SlateCiHost.psm1`:

```powershell
function Select-SlateKvpItems {
    # Guest side: the host's KVP items appear as registry values under
    # HKLM\SOFTWARE\Microsoft\Virtual Machine\External. Keep ours.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Properties)
    $items = @{}
    foreach ($property in $Properties.PSObject.Properties) {
        if ($property.Name -like 'slate.*') { $items[$property.Name] = [string]$property.Value }
    }
    return $items
}
```

Add `Select-SlateKvpItems` to `Export-ModuleMember`.

- [ ] **Step 4: Write the SYSTEM bootstrap**

`scripts/ci-host/golden/guest/bootstrap-system.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Guest task slate-bootstrap-system: SYSTEM, at startup, Windows
# PowerShell 5.1. A no-op until the golden build's completion marker
# exists (so it stays quiet during the image build). Reads the host's
# KVP items, configures the static network, finds the cache volume,
# writes .env and jit.cfg for the runner task, then signals `ready`.
# Any failure writes bootstrap-error.txt and shuts the VM down; the host
# sees Off, resolves no successful job, and discards.
$ErrorActionPreference = 'Stop'
$runnerDir = 'C:\actions-runner'
$marker = 'C:\Users\runner\.slate-golden-complete'
$logPath = Join-Path $runnerDir 'bootstrap-system.log'

function Write-Log([string]$Message) {
    Add-Content -LiteralPath $logPath -Value ('{0:o} {1}' -f (Get-Date), $Message)
}

if (-not (Test-Path -LiteralPath $marker)) { exit 0 }
Import-Module 'C:\slate-guest\SlateCiHost.psm1' -Force

try {
    foreach ($stale in 'ready', 'jit.cfg', 'bootstrap-error.txt') {
        Remove-Item -LiteralPath (Join-Path $runnerDir $stale) -Force -ErrorAction SilentlyContinue
    }
    Write-Log 'waiting for KVP items'
    $kvpKey = 'HKLM:\SOFTWARE\Microsoft\Virtual Machine\External'
    $deadline = (Get-Date).AddSeconds(300)
    $items = @{}
    $jit = $null
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $kvpKey) {
            $items = Select-SlateKvpItems -Properties (Get-ItemProperty -LiteralPath $kvpKey)
            $jit = Join-KvpChunks -Items $items
            if ($jit) { break }
        }
        Start-Sleep -Seconds 2
    }
    if (-not $jit) { throw 'no JIT config arrived within 300 s' }
    foreach ($required in 'slate.ip', 'slate.gateway', 'slate.dns', 'slate.cache', 'slate.lane') {
        if (-not $items.Contains($required)) { throw "KVP item $required missing" }
    }
    Write-Log ('config received: lane {0}, job {1}' -f $items['slate.lane'], $items['slate.job'])

    $adapter = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Sort-Object ifIndex | Select-Object -First 1
    if (-not $adapter) { throw 'no network adapter is up' }
    Set-NetIPInterface -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -Dhcp Disabled
    Get-NetIPAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue
    Get-NetRoute -InterfaceIndex $adapter.ifIndex -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue |
        Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue
    New-NetIPAddress -InterfaceIndex $adapter.ifIndex -IPAddress $items['slate.ip'] -PrefixLength 24 -DefaultGateway $items['slate.gateway'] | Out-Null
    Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses ($items['slate.dns'] -split ',')
    Write-Log ('network: {0} via {1}' -f $items['slate.ip'], $items['slate.gateway'])

    $envLines = @()
    if ($items['slate.cache'] -eq '1') {
        $volume = $null
        $volumeDeadline = (Get-Date).AddSeconds(90)
        while (-not $volume -and (Get-Date) -lt $volumeDeadline) {
            $volume = Get-Volume -FileSystemLabel 'slate-cache' -ErrorAction SilentlyContinue |
                Where-Object { $_.DriveLetter } | Select-Object -First 1
            if (-not $volume) { Start-Sleep -Seconds 3 }
        }
        if (-not $volume) { throw 'cache volume slate-cache did not mount' }
        $root = '{0}:\cache' -f $volume.DriveLetter
        foreach ($sub in 'cargo\registry', 'cargo\git', 'target', 'nuget') {
            New-Item -ItemType Directory -Force -Path (Join-Path $root $sub) | Out-Null
        }
        $envLines += "NSC_CACHE_PATH=$root"
        $envLines += "CARGO_TARGET_DIR=$root\target"
        $envLines += "NUGET_PACKAGES=$root\nuget"
        $envLines += "SLATE_CACHE_ROOT=$root"
        # The volume letter is not fixed, so the Defender exclusion is added here.
        Add-MpPreference -ExclusionPath $root -ErrorAction SilentlyContinue
        Write-Log "cache at $root"
    }
    Set-Content -LiteralPath (Join-Path $runnerDir '.env') -Value $envLines -Encoding ascii

    $cfgPath = Join-Path $runnerDir 'jit.cfg'
    Set-Content -LiteralPath $cfgPath -Value $jit -NoNewline -Encoding ascii
    & icacls.exe $cfgPath /inheritance:r /grant 'runner:R' /grant 'SYSTEM:F' | Out-Null
    Set-Content -LiteralPath (Join-Path $runnerDir 'ready') -Value 'ok'
    Write-Log 'ready'
} catch {
    Write-Log "ERROR: $_"
    Set-Content -LiteralPath (Join-Path $runnerDir 'bootstrap-error.txt') -Value ([string]$_)
    & shutdown.exe /s /t 5 /c 'slate bootstrap failed'
}
```

- [ ] **Step 5: Write the runner bootstrap**

`scripts/ci-host/golden/guest/bootstrap-runner.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Guest task slate-runner-logon: the standard user `runner`, interactive
# session, at logon, Windows PowerShell 5.1. A no-op until the golden
# completion marker exists. Waits for the SYSTEM bootstrap's `ready`,
# junctions the cargo caches onto the cache volume, runs ONE job through
# run.cmd --jitconfig in this desktop session (UIA needs it), then shuts
# the VM down no matter what happened.
$ErrorActionPreference = 'Stop'
$runnerDir = 'C:\actions-runner'
$marker = Join-Path $env:USERPROFILE '.slate-golden-complete'
$logPath = Join-Path $runnerDir 'bootstrap-runner.log'

function Write-Log([string]$Message) {
    Add-Content -LiteralPath $logPath -Value ('{0:o} {1}' -f (Get-Date), $Message)
}

if (-not (Test-Path -LiteralPath $marker)) { exit 0 }

try {
    $ready = Join-Path $runnerDir 'ready'
    $errorFile = Join-Path $runnerDir 'bootstrap-error.txt'
    $deadline = (Get-Date).AddSeconds(360)
    while (-not (Test-Path -LiteralPath $ready) -and (Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $errorFile) { throw ('system bootstrap failed: ' + (Get-Content -LiteralPath $errorFile -Raw)) }
        Start-Sleep -Seconds 2
    }
    if (-not (Test-Path -LiteralPath $ready)) { throw 'ready signal did not arrive within 360 s' }

    $cacheRoot = $null
    foreach ($line in @(Get-Content -LiteralPath (Join-Path $runnerDir '.env') -ErrorAction SilentlyContinue)) {
        if ($line -like 'SLATE_CACHE_ROOT=*') { $cacheRoot = $line.Substring('SLATE_CACHE_ROOT='.Length) }
    }
    if ($cacheRoot) {
        foreach ($pair in @(@('registry', 'cargo\registry'), @('git', 'cargo\git'))) {
            $link = Join-Path (Join-Path $env:USERPROFILE '.cargo') $pair[0]
            $target = Join-Path $cacheRoot $pair[1]
            if (Test-Path -LiteralPath $link) {
                $item = Get-Item -LiteralPath $link -Force
                if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                    # A junction: delete the link only, never its target's contents.
                    [IO.Directory]::Delete($link)
                } else {
                    Remove-Item -LiteralPath $link -Recurse -Force
                }
            }
            New-Item -ItemType Junction -Path $link -Target $target | Out-Null
        }
        Write-Log "cargo junctions -> $cacheRoot"
    }

    $jit = Get-Content -LiteralPath (Join-Path $runnerDir 'jit.cfg') -Raw
    Write-Log 'starting runner'
    $process = Start-Process -FilePath (Join-Path $runnerDir 'run.cmd') -ArgumentList @('--jitconfig', $jit) `
        -WorkingDirectory $runnerDir -NoNewWindow -PassThru -Wait
    Write-Log ('runner exited {0}' -f $process.ExitCode)
} catch {
    Write-Log "ERROR: $_"
} finally {
    Remove-Item -LiteralPath (Join-Path $runnerDir 'jit.cfg') -Force -ErrorAction SilentlyContinue
    & shutdown.exe /s /t 0 /c 'slate job finished'
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: every test in the suite passes.

- [ ] **Step 7: Commit**

```bash
git add scripts/ci-host/golden/guest scripts/ci-host/SlateCiHost.psm1 scripts/ci-host/tests/Guest.Tests.ps1
git commit -m "feat(ci-host): guest bootstrap — SYSTEM network/cache/config task and the interactive runner task"
```

---

### Task 10: Golden image — versions, unattend template, provisioning, build script

**Files:**
- Create: `scripts/ci-host/golden/versions.json`
- Create: `scripts/ci-host/golden/unattend.xml`
- Create: `scripts/ci-host/golden/provision-guest.ps1`
- Create: `scripts/ci-host/golden/provision-runner-user.ps1`
- Create: `scripts/ci-host/golden/build-golden.ps1`
- Modify: `scripts/ci-host/SlateCiHost.psm1` (add `New-RandomPassword`, `Expand-UnattendTemplate`)
- Create: `scripts/ci-host/tests/Golden.Tests.ps1`

**Interfaces:**
- Produces: `New-RandomPassword -Length [int]` → string from `[A-Za-z0-9]` (host-only, .NET `RandomNumberGenerator`); `Expand-UnattendTemplate -TemplatePath -ProductKey -ProvisionPassword -RunnerPassword` → rendered XML string (throws on a malformed key or a leftover placeholder); the golden VHDX at `C:\slate-ci\golden\win11-runner.vhdx` with: users `provision` (admin) and `runner` (standard, auto-logon), `C:\actions-runner` (runner 2.338.0, owned by `runner`), `C:\slate-guest` (the two guest scripts + `SlateCiHost.psm1`, writable only by Administrators and SYSTEM), scheduled tasks `slate-bootstrap-system` and `slate-runner-logon`, `C:\dotnet`, VS 2022 Build Tools, Python, Git, rustup 1.97.1 with the ARM64 target under `C:\Users\runner`, `uniffi-bindgen-cs` in `C:\Users\runner\.cargo\bin`, marker `C:\Users\runner\.slate-golden-complete`.

- [ ] **Step 1: Write the pinned versions**

`scripts/ci-host/golden/versions.json`:

```json
{
  "runnerVersion": "2.338.0",
  "runnerSha256": "f48e0750a21812bca5f82de5f7f5aeae71abee647fab5a582f1742d07eba455f",
  "rustToolchain": "1.97.1",
  "bindgenTag": "v0.11.0+v0.31.0",
  "dotnetChannel": "10.0",
  "pythonVersion": "3.13.15",
  "gitVersion": "2.55.0.5",
  "gitTag": "v2.55.0.windows.5",
  "vsBuildToolsUrl": "https://aka.ms/vs/17/release/vs_BuildTools.exe",
  "dns": "1.1.1.1,8.8.8.8"
}
```

- [ ] **Step 2: Write the unattend template**

`scripts/ci-host/golden/unattend.xml` (placeholders are replaced by `Expand-UnattendTemplate`; never commit a rendered copy):

```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- Slate CI golden image. Applied as Windows\Panther\unattend.xml on a
     DISM-applied disk, so only specialize and oobeSystem run. UAC is off
     during provisioning (the first-logon command needs a full admin
     token) and provision-guest.ps1 turns it back on before the final
     reboot. -->
<unattend xmlns="urn:schemas-microsoft-com:unattend" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State">
  <settings pass="specialize">
    <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
      <ComputerName>slate-win</ComputerName>
      <ProductKey>__PRODUCT_KEY__</ProductKey>
      <TimeZone>UTC</TimeZone>
    </component>
    <component name="Microsoft-Windows-LUA-Settings" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
      <EnableLUA>false</EnableLUA>
    </component>
    <component name="Microsoft-Windows-Deployment" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
      <RunSynchronous>
        <RunSynchronousCommand wcm:action="add">
          <Order>1</Order>
          <Path>reg add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE /v BypassNRO /t REG_DWORD /d 1 /f</Path>
          <Description>Allow OOBE without a Microsoft account</Description>
        </RunSynchronousCommand>
        <RunSynchronousCommand wcm:action="add">
          <Order>2</Order>
          <Path>reg add HKLM\SYSTEM\CurrentControlSet\Control\BitLocker /v PreventDeviceEncryption /t REG_DWORD /d 1 /f</Path>
          <Description>Never auto-encrypt a disposable disk because of the vTPM</Description>
        </RunSynchronousCommand>
      </RunSynchronous>
    </component>
  </settings>
  <settings pass="oobeSystem">
    <component name="Microsoft-Windows-International-Core" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
      <InputLocale>en-US</InputLocale>
      <SystemLocale>en-US</SystemLocale>
      <UILanguage>en-US</UILanguage>
      <UserLocale>en-US</UserLocale>
    </component>
    <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
      <OOBE>
        <HideEULAPage>true</HideEULAPage>
        <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>
        <HideOnlineAccountScreens>true</HideOnlineAccountScreens>
        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>
        <ProtectYourPC>3</ProtectYourPC>
        <SkipMachineOOBE>true</SkipMachineOOBE>
        <SkipUserOOBE>true</SkipUserOOBE>
      </OOBE>
      <UserAccounts>
        <LocalAccounts>
          <LocalAccount wcm:action="add">
            <Name>provision</Name>
            <DisplayName>provision</DisplayName>
            <Group>Administrators</Group>
            <Password>
              <Value>__PROVISION_PASSWORD__</Value>
              <PlainText>true</PlainText>
            </Password>
          </LocalAccount>
          <LocalAccount wcm:action="add">
            <Name>runner</Name>
            <DisplayName>runner</DisplayName>
            <Group>Users</Group>
            <Password>
              <Value>__RUNNER_PASSWORD__</Value>
              <PlainText>true</PlainText>
            </Password>
          </LocalAccount>
        </LocalAccounts>
      </UserAccounts>
      <AutoLogon>
        <Enabled>true</Enabled>
        <Username>provision</Username>
        <LogonCount>1</LogonCount>
        <Password>
          <Value>__PROVISION_PASSWORD__</Value>
          <PlainText>true</PlainText>
        </Password>
      </AutoLogon>
      <FirstLogonCommands>
        <SynchronousCommand wcm:action="add">
          <Order>1</Order>
          <CommandLine>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\provision\provision-guest.ps1</CommandLine>
          <Description>Slate golden provisioning</Description>
          <RequiresUserInput>false</RequiresUserInput>
        </SynchronousCommand>
      </FirstLogonCommands>
    </component>
  </settings>
</unattend>
```

- [ ] **Step 3: Write the failing tests**

`scripts/ci-host/tests/Golden.Tests.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force
    $script:goldenDir = Join-Path $PSScriptRoot '..' 'golden'
    $script:repoRoot = Join-Path $PSScriptRoot '..' '..' '..'
    function Test-ScriptParses([string]$Path) {
        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$errors)
        return (@($errors).Count -eq 0)
    }
}

Describe 'versions.json' {
    BeforeAll { $script:versions = Get-Content -Raw (Join-Path $goldenDir 'versions.json') | ConvertFrom-Json }
    It 'has every key the provisioning scripts read' {
        foreach ($k in 'runnerVersion', 'runnerSha256', 'rustToolchain', 'bindgenTag', 'dotnetChannel', 'pythonVersion', 'gitVersion', 'gitTag', 'vsBuildToolsUrl', 'dns') {
            $versions.PSObject.Properties[$k] | Should -Not -BeNullOrEmpty
        }
        $versions.runnerSha256 | Should -Match '^[0-9a-f]{64}$'
    }
    It 'pins the Rust toolchain the repository pins' {
        $toml = Get-Content -Raw (Join-Path $repoRoot 'rust-toolchain.toml')
        $toml | Should -Match ('channel\s*=\s*"' + [regex]::Escape($versions.rustToolchain) + '"')
    }
    It 'pins the uniffi-bindgen-cs tag the repository pins' {
        (Get-Content -Raw (Join-Path $repoRoot 'apps' 'slate-windows' 'uniffi-bindgen-cs.version')).Trim() | Should -Be $versions.bindgenTag
    }
}

Describe 'New-RandomPassword' {
    It 'returns the requested length from the alphanumeric alphabet and differs per call' {
        $a = New-RandomPassword -Length 24
        $b = New-RandomPassword -Length 24
        $a.Length | Should -Be 24
        $a | Should -Match '^[A-Za-z0-9]{24}$'
        $a | Should -Not -Be $b
    }
}

Describe 'unattend template' {
    It 'is well-formed XML with the three placeholders and no literal secrets' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'unattend.xml')
        { [xml]$text } | Should -Not -Throw
        $text | Should -Match '__PRODUCT_KEY__'
        $text | Should -Match '__PROVISION_PASSWORD__'
        $text | Should -Match '__RUNNER_PASSWORD__'
        $text | Should -Match '<ComputerName>slate-win</ComputerName>'
        $text | Should -Match '<Username>provision</Username>'
        $text | Should -Match 'provision-guest\.ps1'
        $text | Should -Match 'PreventDeviceEncryption'
    }
    It 'renders with escaped values and no placeholder left' {
        $out = Expand-UnattendTemplate -TemplatePath (Join-Path $goldenDir 'unattend.xml') -ProductKey 'ABCDE-FGHIJ-KLMNO-PQRST-UVWXY' -ProvisionPassword 'p<&>w' -RunnerPassword 'r"w'
        $out | Should -Not -Match '__[A-Z_]+__'
        $out | Should -Match '<ProductKey>ABCDE-FGHIJ-KLMNO-PQRST-UVWXY</ProductKey>'
        $out | Should -Match 'p&lt;&amp;&gt;w'
        { [xml]$out } | Should -Not -Throw
    }
    It 'rejects a malformed product key' {
        { Expand-UnattendTemplate -TemplatePath (Join-Path $goldenDir 'unattend.xml') -ProductKey 'nope' -ProvisionPassword 'a' -RunnerPassword 'b' } | Should -Throw '*product key*'
    }
}

Describe 'golden scripts' {
    It 'parse as PowerShell' {
        foreach ($f in 'provision-guest.ps1', 'provision-runner-user.ps1', 'build-golden.ps1') {
            Test-ScriptParses (Join-Path $goldenDir $f) | Should -BeTrue
        }
    }
    It 'provisioning registers both guest tasks, switches auto-logon to runner, restores UAC and deletes the secrets' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $text | Should -Match "TaskName 'slate-bootstrap-system'"
        $text | Should -Match "TaskName 'slate-runner-logon'"
        $text | Should -Match "DefaultUserName -Value 'runner'"
        $text | Should -Match 'EnableLUA -Value 1'
        $text | Should -Match "secrets\.json'\) -Force"
        $text | Should -Match 'Panther\\unattend\.xml'
        $text | Should -Match 'provision-runner-user\.ps1'
        $text | Should -Match 'slmgr\.vbs /cpky'
        $text | Should -Match "Destination 'C:\\\\slate-guest'"
        $text | Should -Match 'C:\\slate-guest\\bootstrap-system\.ps1'
    }
    It 'the per-user script installs the pinned toolchain and writes the completion marker last' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-runner-user.ps1')
        $text | Should -Match 'rustup-init\.exe'
        $text | Should -Match 'aarch64-pc-windows-msvc'
        $text | Should -Match 'uniffi-bindgen-cs'
        $text.IndexOf('.slate-golden-complete') | Should -BeGreaterThan $text.IndexOf('cargo install')
    }
    It 'the build script applies install.wim with DISM, injects the unattend and marks the disk read-only' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'build-golden.ps1')
        $text | Should -Match 'Expand-WindowsImage'
        $text | Should -Match 'bcdboot'
        $text | Should -Match 'Windows\\Panther\\unattend\.xml'
        $text | Should -Match 'IsReadOnly -Value \$true'
        $text | Should -Match '\.slate-golden-complete'
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1 -Path scripts/ci-host/tests/Golden.Tests.ps1
```

Expected: FAIL ("New-RandomPassword is not recognized", scripts missing).

- [ ] **Step 5: Add the two module helpers**

Insert before `Export-ModuleMember`:

```powershell
function New-RandomPassword {
    # Host-only. Alphanumeric so it survives every quoting context the
    # unattend, winlogon registry and scheduled-task registration use.
    [CmdletBinding()]
    param([int]$Length = 24)
    $alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789'
    $chars = New-Object char[] $Length
    for ($i = 0; $i -lt $Length; $i++) {
        $chars[$i] = $alphabet[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32(0, $alphabet.Length)]
    }
    return (-join $chars)
}

function Expand-UnattendTemplate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TemplatePath,
        [Parameter(Mandatory)][string]$ProductKey,
        [Parameter(Mandatory)][string]$ProvisionPassword,
        [Parameter(Mandatory)][string]$RunnerPassword
    )
    if ($ProductKey -notmatch '^[A-Z0-9]{5}(-[A-Z0-9]{5}){4}$') { throw 'product key must look like XXXXX-XXXXX-XXXXX-XXXXX-XXXXX' }
    $text = Get-Content -Raw -LiteralPath $TemplatePath
    $text = $text.Replace('__PRODUCT_KEY__', $ProductKey)
    $text = $text.Replace('__PROVISION_PASSWORD__', [System.Security.SecurityElement]::Escape($ProvisionPassword))
    $text = $text.Replace('__RUNNER_PASSWORD__', [System.Security.SecurityElement]::Escape($RunnerPassword))
    if ($text -match '__[A-Z_]+__') { throw "unattend placeholder left unrendered: $($Matches[0])" }
    return $text
}
```

Add `New-RandomPassword, Expand-UnattendTemplate` to `Export-ModuleMember`.

- [ ] **Step 6: Write the in-guest machine provisioning script (runs once as `provision`, UAC off)**

`scripts/ci-host/golden/provision-guest.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Golden image, phase 1 (Windows PowerShell 5.1, as the temporary admin
# `provision`, launched by the unattend's first-logon command). Installs
# the machine-wide toolchain, the runner and its two tasks, then hands
# the next boot to `runner` whose RunOnce runs provision-runner-user.ps1.
# Pins come from versions.json; the auto-logon password from secrets.json
# (deleted at the end, together with the rendered unattend).
$ErrorActionPreference = 'Stop'
$root = 'C:\provision'
$logPath = Join-Path $root 'provision.log'
$dl = Join-Path $root 'dl'

function Write-Log([string]$Message) {
    Add-Content -LiteralPath $logPath -Value ('{0:o} {1}' -f (Get-Date), $Message)
    Write-Host $Message
}
function Get-Download([string]$Url, [string]$Path, [string]$Sha256) {
    Write-Log "download $Url"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $Url -OutFile $Path -UseBasicParsing
    if ($Sha256) {
        $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $Sha256.ToLowerInvariant()) { throw "sha256 mismatch for ${Path}: $actual" }
    }
}
function Invoke-Installer([string]$File, [string]$Arguments) {
    Write-Log "run $File $Arguments"
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) { throw "$File exited $($process.ExitCode)" }
}

try {
    $versions = Get-Content -Raw -LiteralPath (Join-Path $root 'versions.json') | ConvertFrom-Json
    $secrets = Get-Content -Raw -LiteralPath (Join-Path $root 'secrets.json') | ConvertFrom-Json
    New-Item -ItemType Directory -Force -Path $dl | Out-Null
    Write-Log 'provisioning start'

    # Machine policy: never sleep, long paths, no automatic updates,
    # Defender excludes the build trees, IPv6 off (the job switch denies it anyway).
    & powercfg.exe /change standby-timeout-ac 0 | Out-Null
    & powercfg.exe /change monitor-timeout-ac 0 | Out-Null
    & powercfg.exe /change hibernate-timeout-ac 0 | Out-Null
    & powercfg.exe /hibernate off | Out-Null
    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' -Name LongPathsEnabled -Value 1 -Type DWord
    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Force | Out-Null
    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Name NoAutoUpdate -Value 1 -Type DWord
    Add-MpPreference -ExclusionPath 'C:\actions-runner', 'C:\dotnet', 'C:\Users\runner\.cargo', 'C:\Users\runner\.rustup' -ErrorAction SilentlyContinue
    Get-NetAdapter | Disable-NetAdapterBinding -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue

    # Visual Studio 2022 Build Tools: C++ x64 + ARM64 and the Windows 11 SDK.
    Get-Download $versions.vsBuildToolsUrl (Join-Path $dl 'vs_BuildTools.exe')
    Invoke-Installer (Join-Path $dl 'vs_BuildTools.exe') ('--quiet --wait --norestart --nocache ' +
        '--add Microsoft.VisualStudio.Workload.VCTools ' +
        '--add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 ' +
        '--add Microsoft.VisualStudio.Component.VC.Tools.ARM64 ' +
        '--add Microsoft.VisualStudio.Component.Windows11SDK.22621 --includeRecommended')

    # .NET SDK into C:\dotnet, owned by runner later, so setup-dotnet can
    # update it without elevation.
    Get-Download 'https://dot.net/v1/dotnet-install.ps1' (Join-Path $dl 'dotnet-install.ps1')
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dl 'dotnet-install.ps1') -Channel $versions.dotnetChannel -InstallDir 'C:\dotnet' -NoPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet-install exited $LASTEXITCODE" }
    [Environment]::SetEnvironmentVariable('DOTNET_ROOT', 'C:\dotnet', 'Machine')
    [Environment]::SetEnvironmentVariable('DOTNET_INSTALL_DIR', 'C:\dotnet', 'Machine')
    [Environment]::SetEnvironmentVariable('DOTNET_CLI_TELEMETRY_OPTOUT', '1', 'Machine')
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    [Environment]::SetEnvironmentVariable('Path', "C:\dotnet;$machinePath", 'Machine')

    Get-Download ('https://www.python.org/ftp/python/{0}/python-{0}-amd64.exe' -f $versions.pythonVersion) (Join-Path $dl 'python.exe')
    Invoke-Installer (Join-Path $dl 'python.exe') '/quiet InstallAllUsers=1 PrependPath=1 Include_test=0'

    Get-Download ('https://github.com/git-for-windows/git/releases/download/{0}/Git-{1}-64-bit.exe' -f $versions.gitTag, $versions.gitVersion) (Join-Path $dl 'git.exe')
    Invoke-Installer (Join-Path $dl 'git.exe') '/VERYSILENT /NORESTART /NOCANCEL /SP- /o:PathOption=Cmd'

    # The runner, hash-verified, plus the guest tasks' scripts and the module.
    $zip = Join-Path $dl 'runner.zip'
    Get-Download ('https://github.com/actions/runner/releases/download/v{0}/actions-runner-win-x64-{0}.zip' -f $versions.runnerVersion) $zip $versions.runnerSha256
    New-Item -ItemType Directory -Force -Path 'C:\actions-runner' | Out-Null
    Expand-Archive -Path $zip -DestinationPath 'C:\actions-runner' -Force
    # Scripts SYSTEM will run live where runner cannot write them.
    New-Item -ItemType Directory -Force -Path 'C:\slate-guest' | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'guest\bootstrap-system.ps1'), (Join-Path $root 'guest\bootstrap-runner.ps1'), (Join-Path $root 'SlateCiHost.psm1') -Destination 'C:\slate-guest'
    & icacls.exe 'C:\slate-guest' /inheritance:r /grant 'Administrators:(OI)(CI)F' /grant 'SYSTEM:(OI)(CI)F' /grant 'runner:(OI)(CI)RX' | Out-Null
    foreach ($dir in 'C:\actions-runner', 'C:\dotnet') {
        & icacls.exe $dir /setowner runner /T /C | Out-Null
        & icacls.exe $dir /grant 'runner:(OI)(CI)F' /T /C | Out-Null
    }
    & icacls.exe $root /grant 'runner:(OI)(CI)RX' /T /C | Out-Null

    $systemAction = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\slate-guest\bootstrap-system.ps1'
    Register-ScheduledTask -TaskName 'slate-bootstrap-system' -Action $systemAction -Trigger (New-ScheduledTaskTrigger -AtStartup) `
        -Principal (New-ScheduledTaskPrincipal -UserId 'NT AUTHORITY\SYSTEM' -RunLevel Highest) `
        -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 1) -StartWhenAvailable) -Force | Out-Null
    $runnerAction = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File C:\slate-guest\bootstrap-runner.ps1'
    Register-ScheduledTask -TaskName 'slate-runner-logon' -Action $runnerAction -Trigger (New-ScheduledTaskTrigger -AtLogOn -User 'runner') `
        -Principal (New-ScheduledTaskPrincipal -UserId 'runner' -LogonType Interactive -RunLevel Limited) `
        -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 4) -StartWhenAvailable) -Force | Out-Null

    Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object {
        Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ServerAddresses ($versions.dns -split ',')
    }

    # Hand the next boot to runner: auto-logon and a one-shot per-user provisioning.
    $winlogon = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
    Set-ItemProperty -Path $winlogon -Name AutoAdminLogon -Value '1'
    Set-ItemProperty -Path $winlogon -Name DefaultUserName -Value 'runner'
    Set-ItemProperty -Path $winlogon -Name DefaultPassword -Value $secrets.runnerPassword
    Remove-ItemProperty -Path $winlogon -Name AutoLogonCount -ErrorAction SilentlyContinue
    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce' -Name 'slate-provision-runner-user' `
        -Value 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\provision\provision-runner-user.ps1'
    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name EnableLUA -Value 1 -Type DWord

    # Windows keeps an installed retail key readable by standard users in
    # DigitalProductId; clear it now (the image is activated) so no job can read it.
    & cscript.exe //B C:\Windows\System32\slmgr.vbs /cpky | Out-Null
    Remove-Item -LiteralPath (Join-Path $root 'secrets.json') -Force
    Remove-Item -LiteralPath 'C:\Windows\Panther\unattend.xml' -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $dl -Recurse -Force
    Write-Log 'phase 1 complete; rebooting into runner'
    Restart-Computer -Force
} catch {
    Write-Log "ERROR: $_"
    Set-Content -LiteralPath (Join-Path $root 'provision-error.txt') -Value ([string]$_)
    & shutdown.exe /s /t 10 /c 'slate provisioning failed'
}
```

- [ ] **Step 7: Write the per-user provisioning script (runs once as `runner`)**

`scripts/ci-host/golden/provision-runner-user.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Golden image, phase 2 (Windows PowerShell 5.1, as `runner`, via the
# HKLM RunOnce set by provision-guest.ps1). rustup and uniffi-bindgen-cs
# live in the runner profile so `rustup target add` and `cargo install`
# work unelevated in jobs. Writes the completion marker LAST and shuts
# down; the host then verifies and seals the disk.
$ErrorActionPreference = 'Stop'
$logPath = Join-Path $env:USERPROFILE 'provision-runner-user.log'
function Write-Log([string]$Message) { Add-Content -LiteralPath $logPath -Value ('{0:o} {1}' -f (Get-Date), $Message) }

try {
    $versions = Get-Content -Raw -LiteralPath 'C:\provision\versions.json' | ConvertFrom-Json
    $dl = Join-Path $env:TEMP 'slate-provision'
    New-Item -ItemType Directory -Force -Path $dl | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Write-Log 'phase 2 start'

    $rustupInit = Join-Path $dl 'rustup-init.exe'
    Invoke-WebRequest -Uri 'https://static.rust-lang.org/rustup/dist/x86_64-pc-windows-msvc/rustup-init.exe' -OutFile $rustupInit -UseBasicParsing
    $process = Start-Process -FilePath $rustupInit -ArgumentList @('-y', '--no-modify-path', '--profile', 'minimal',
        '--default-toolchain', $versions.rustToolchain, '--component', 'rustfmt', '--component', 'clippy',
        '--target', 'aarch64-pc-windows-msvc') -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ne 0) { throw "rustup-init exited $($process.ExitCode)" }
    $cargoBin = Join-Path $env:USERPROFILE '.cargo\bin'
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    [Environment]::SetEnvironmentVariable('Path', "$cargoBin;$userPath", 'User')
    $env:Path = "$cargoBin;$env:Path"
    Write-Log (& rustc --version)

    & cargo install --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag $versions.bindgenTag uniffi-bindgen-cs --locked
    if ($LASTEXITCODE -ne 0) { throw "cargo install uniffi-bindgen-cs exited $LASTEXITCODE" }
    Write-Log (& uniffi-bindgen-cs --version)

    Remove-Item -LiteralPath $dl -Recurse -Force
    Set-Content -LiteralPath (Join-Path $env:USERPROFILE '.slate-golden-complete') -Value ('{0:o}' -f (Get-Date))
    Write-Log 'golden complete'
} catch {
    Write-Log "ERROR: $_"
    Set-Content -LiteralPath (Join-Path $env:USERPROFILE 'provision-runner-user-error.txt') -Value ([string]$_)
} finally {
    & shutdown.exe /s /t 10 /c 'slate golden build finished'
}
```

- [ ] **Step 8: Write the host-side build script (elevated)**

`scripts/ci-host/golden/build-golden.ps1`:

```powershell
#Requires -Version 7
#Requires -RunAsAdministrator
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Builds the read-only golden disk without running Windows Setup: apply
# install.wim to a new VHDX with DISM, make it bootable, inject the
# rendered unattend and the provisioning payload, boot it once on a
# switch with Internet (Default Switch), wait for the two provisioning
# phases to shut it down, verify the completion marker, seal the file.
# The product key is read from a hidden prompt and never written to disk
# outside the rendered unattend inside the image (deleted by phase 1).
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$IsoPath,
    [string]$OutPath = 'C:\slate-ci\golden\win11-runner.vhdx',
    [int]$SizeGB = 120,
    [string]$SwitchName = 'Default Switch',
    [string]$ImageName = 'Windows 11 Pro',
    [int]$TimeoutMinutes = 150
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force
$goldenDir = $PSScriptRoot
$vmName = 'slate-golden-build'

if (Test-Path -LiteralPath $OutPath) { throw "$OutPath exists; move it aside to rebuild" }
if (Get-VM -Name $vmName -ErrorAction SilentlyContinue) { throw "VM $vmName exists; remove it first" }

$keySecure = Read-Host -Prompt 'Windows 11 Pro product key (XXXXX-XXXXX-XXXXX-XXXXX-XXXXX, hidden)' -AsSecureString
$productKey = [System.Net.NetworkCredential]::new('', $keySecure).Password.Trim().ToUpperInvariant()
$provisionPassword = New-RandomPassword -Length 24
$runnerPassword = New-RandomPassword -Length 24
$unattend = Expand-UnattendTemplate -TemplatePath (Join-Path $goldenDir 'unattend.xml') -ProductKey $productKey -ProvisionPassword $provisionPassword -RunnerPassword $runnerPassword
Remove-Variable productKey, keySecure

Write-Host "Mounting $IsoPath"
$image = Mount-DiskImage -ImagePath $IsoPath -PassThru
try {
    $isoLetter = ($image | Get-Volume).DriveLetter
    $wim = Get-ChildItem -LiteralPath "${isoLetter}:\sources" | Where-Object { $_.Name -in 'install.wim', 'install.esd' } | Select-Object -First 1
    if (-not $wim) { throw 'install.wim/install.esd not found in the ISO' }
    $index = (Get-WindowsImage -ImagePath $wim.FullName | Where-Object { $_.ImageName -eq $ImageName }).ImageIndex
    if (-not $index) { throw "image '$ImageName' not found in $($wim.FullName)" }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutPath) | Out-Null
    New-VHD -Path $OutPath -SizeBytes ([int64]$SizeGB * 1GB) -Dynamic | Out-Null
    $disk = Mount-VHD -Path $OutPath -Passthru | Get-Disk
    try {
        Initialize-Disk -Number $disk.Number -PartitionStyle GPT
        $efi = New-Partition -DiskNumber $disk.Number -Size 260MB -GptType '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}' -AssignDriveLetter
        Format-Volume -Partition $efi -FileSystem FAT32 -NewFileSystemLabel 'System' -Confirm:$false | Out-Null
        New-Partition -DiskNumber $disk.Number -Size 16MB -GptType '{e3c9e316-0b5c-4db8-817d-f92df00215ae}' | Out-Null
        $windows = New-Partition -DiskNumber $disk.Number -UseMaximumSize -GptType '{ebd0a0a2-b9e5-4433-87de-68b6b72008e2}' -AssignDriveLetter
        Format-Volume -Partition $windows -FileSystem NTFS -NewFileSystemLabel 'Windows' -Confirm:$false | Out-Null
        $w = '{0}:' -f $windows.DriveLetter
        $s = '{0}:' -f $efi.DriveLetter
        Write-Host "Applying '$ImageName' (index $index) to $w (several minutes)"
        Expand-WindowsImage -ImagePath $wim.FullName -Index $index -ApplyPath "$w\" | Out-Null
        & bcdboot.exe "$w\Windows" /s $s /f UEFI | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "bcdboot exited $LASTEXITCODE" }
        New-Item -ItemType Directory -Force -Path "$w\Windows\Panther", "$w\provision\guest" | Out-Null
        Set-Content -LiteralPath "$w\Windows\Panther\unattend.xml" -Value $unattend -Encoding utf8
        Copy-Item -LiteralPath (Join-Path $goldenDir 'provision-guest.ps1'), (Join-Path $goldenDir 'provision-runner-user.ps1'), (Join-Path $goldenDir 'versions.json') -Destination "$w\provision"
        Copy-Item -Path (Join-Path $goldenDir 'guest' '*.ps1') -Destination "$w\provision\guest"
        Copy-Item -LiteralPath (Join-Path $goldenDir '..' 'SlateCiHost.psm1') -Destination "$w\provision"
        @{ runnerPassword = $runnerPassword } | ConvertTo-Json | Set-Content -LiteralPath "$w\provision\secrets.json" -Encoding utf8
    } finally {
        Dismount-VHD -Path $OutPath
    }
} finally {
    Dismount-DiskImage -ImagePath $IsoPath | Out-Null
}
Remove-Variable unattend, provisionPassword, runnerPassword

Write-Host 'Booting the build VM (phase 1 installs ~3 GB of tooling; phase 2 builds uniffi-bindgen-cs)'
New-VM -Name $vmName -Generation 2 -MemoryStartupBytes 12GB -VHDPath $OutPath -SwitchName $SwitchName | Out-Null
Set-VM -Name $vmName -ProcessorCount 4 -StaticMemory -AutomaticCheckpointsEnabled $false -CheckpointType Disabled -AutomaticStopAction TurnOff
Set-VMFirmware -VMName $vmName -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows
Set-VMKeyProtector -VMName $vmName -NewLocalKeyProtector
Enable-VMTPM -VMName $vmName
Set-VMVideo -VMName $vmName -ResolutionType Single -HorizontalResolution 1920 -VerticalResolution 1080
Start-VM -Name $vmName
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
while ((Get-VM -Name $vmName).State -ne 'Off') {
    if ((Get-Date) -gt $deadline) { throw "build VM still running after $TimeoutMinutes min; inspect with: vmconnect.exe localhost $vmName" }
    Start-Sleep -Seconds 30
    Write-Host ('  {0:HH:mm:ss} provisioning ({1})' -f (Get-Date), (Get-VM -Name $vmName).State)
}

Write-Host 'Verifying the image'
$disk = Mount-VHD -Path $OutPath -ReadOnly -Passthru | Get-Disk
try {
    $volume = $disk | Get-Partition | Get-Volume | Where-Object { $_.DriveLetter -and (Test-Path -LiteralPath ('{0}:\Windows\System32' -f $_.DriveLetter)) } | Select-Object -First 1
    if (-not $volume) { throw 'Windows volume not found in the built disk' }
    $w = '{0}:' -f $volume.DriveLetter
    if (Test-Path -LiteralPath "$w\provision\provision.log") { Get-Content -LiteralPath "$w\provision\provision.log" -Tail 15 }
    if (Test-Path -LiteralPath "$w\provision\provision-error.txt") { throw ('phase 1 failed: ' + (Get-Content -LiteralPath "$w\provision\provision-error.txt" -Raw)) }
    if (Test-Path -LiteralPath "$w\Users\runner\provision-runner-user-error.txt") { throw ('phase 2 failed: ' + (Get-Content -LiteralPath "$w\Users\runner\provision-runner-user-error.txt" -Raw)) }
    if (-not (Test-Path -LiteralPath "$w\Users\runner\.slate-golden-complete")) { throw 'completion marker C:\Users\runner\.slate-golden-complete missing' }
    if (Test-Path -LiteralPath "$w\provision\secrets.json") { throw 'secrets.json still present in the image' }
    if (Test-Path -LiteralPath "$w\Windows\Panther\unattend.xml") { throw 'rendered unattend still present in the image' }
} finally {
    Dismount-VHD -Path $OutPath
}
Remove-VM -Name $vmName -Force
Set-ItemProperty -LiteralPath $OutPath -Name IsReadOnly -Value $true
Write-Host "Golden image ready and read-only: $OutPath"
```

- [ ] **Step 9: Run the tests to verify they pass**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: every test in the suite passes.

- [ ] **Step 10: Commit**

```bash
git add scripts/ci-host/golden scripts/ci-host/SlateCiHost.psm1 scripts/ci-host/tests/Golden.Tests.ps1
git commit -m "feat(ci-host): golden image — pinned versions, unattend template, two-phase provisioning, DISM build script"
```

---

### Task 11: Host install scripts

**Files:**
- Create: `scripts/ci-host/install/setup-host.ps1`
- Create: `scripts/ci-host/install/store-token.ps1`
- Modify: `scripts/ci-host/SlateCiHost.psm1` (add `Add-SidToUserRight`, `Set-LocalUserRights`)
- Create: `scripts/ci-host/tests/Install.Tests.ps1`

**Interfaces:**
- Consumes: `New-RandomPassword` (Task 10), `config.json` values (Task 5), `orchestrator.ps1` (Task 8).
- Produces: `Add-SidToUserRight -IniText -Right -Sid` → secedit INF text with `*<sid>` listed under the right (pure); `Set-LocalUserRights -Sid` (host-only: deny interactive and remote-interactive logon, grant batch logon); host objects: account `slate-ci-host`, `C:\slate-ci` tree with ACLs, switch `slate-ci` + NAT + firewall rule, three formatted cache parents with `<lane>.gen = 0`, `C:\slate-ci\bin` (copy of `scripts/ci-host` minus tests, plus `install-commit.txt`), scheduled tasks `slate-ci-orchestrator` (at startup) and `slate-ci-store-token` (on demand), `C:\slate-ci\state\token.xml`.

- [ ] **Step 1: Write the failing tests**

`scripts/ci-host/tests/Install.Tests.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force
    $script:installDir = Join-Path $PSScriptRoot '..' 'install'
    function Test-ScriptParses([string]$Path) {
        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$errors)
        return (@($errors).Count -eq 0)
    }
}

Describe 'Add-SidToUserRight' {
    BeforeAll {
        $script:ini = @"
[Unicode]
Unicode=yes
[Privilege Rights]
SeDenyInteractiveLogonRight = *S-1-5-32-546
SeBatchLogonRight = *S-1-5-32-544,*S-1-5-32-551
[Version]
signature="`$CHICAGO`$"
Revision=1
"@
    }
    It 'appends the SID to an existing right' {
        $out = Add-SidToUserRight -IniText $ini -Right 'SeDenyInteractiveLogonRight' -Sid 'S-1-5-21-1-2-3-1004'
        $out | Should -Match 'SeDenyInteractiveLogonRight = \*S-1-5-32-546,\*S-1-5-21-1-2-3-1004'
        $out | Should -Match 'SeBatchLogonRight = \*S-1-5-32-544,\*S-1-5-32-551'
    }
    It 'is idempotent' {
        $once = Add-SidToUserRight -IniText $ini -Right 'SeBatchLogonRight' -Sid 'S-1-5-21-1-2-3-1004'
        $twice = Add-SidToUserRight -IniText $once -Right 'SeBatchLogonRight' -Sid 'S-1-5-21-1-2-3-1004'
        $twice | Should -Be $once
    }
    It 'adds a missing right inside the section' {
        $out = Add-SidToUserRight -IniText $ini -Right 'SeDenyRemoteInteractiveLogonRight' -Sid 'S-1-5-21-1-2-3-1004'
        $lines = $out -split "`r?`n"
        $section = [array]::IndexOf($lines, '[Privilege Rights]')
        $version = [array]::IndexOf($lines, '[Version]')
        $added = [array]::IndexOf($lines, 'SeDenyRemoteInteractiveLogonRight = *S-1-5-21-1-2-3-1004')
        $added | Should -BeGreaterThan $section
        $added | Should -BeLessThan $version
    }
    It 'creates the section when the export has none' {
        $out = Add-SidToUserRight -IniText "[Unicode]`r`nUnicode=yes" -Right 'SeBatchLogonRight' -Sid 'S-1-5-21-9'
        $out | Should -Match '\[Privilege Rights\]\r?\nSeBatchLogonRight = \*S-1-5-21-9'
    }
}

Describe 'install scripts' {
    It 'parse as PowerShell' {
        Test-ScriptParses (Join-Path $installDir 'setup-host.ps1') | Should -BeTrue
        Test-ScriptParses (Join-Path $installDir 'store-token.ps1') | Should -BeTrue
    }
    It 'setup never adds the account to Administrators and registers both tasks' {
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $text | Should -Match "Add-LocalGroupMember -Group 'Hyper-V Administrators'"
        $text | Should -Not -Match "Add-LocalGroupMember -Group 'Administrators'"
        $text | Should -Match "TaskName 'slate-ci-orchestrator'"
        $text | Should -Match "TaskName 'slate-ci-store-token'"
        $text | Should -Match "NewFileSystemLabel 'slate-cache'"
        $text | Should -Match 'New-NetNat'
        $text | Should -Match 'Set-LocalUserRights'
    }
    It 'store-token only accepts a fine-grained PAT and removes the plaintext' {
        $text = Get-Content -Raw (Join-Path $installDir 'store-token.ps1')
        $text | Should -Match 'github_pat_'
        $text | Should -Match 'Export-Clixml'
        $text | Should -Match 'Remove-Item -LiteralPath \$plainPath'
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1 -Path scripts/ci-host/tests/Install.Tests.ps1
```

Expected: FAIL ("Add-SidToUserRight is not recognized", scripts missing).

- [ ] **Step 3: Add the module helpers**

Insert before `Export-ModuleMember`:

```powershell
function Add-SidToUserRight {
    # secedit INF surgery: make sure "*<sid>" is listed under <right> in
    # [Privilege Rights]. Pure so it can be tested; Set-LocalUserRights
    # applies it.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$IniText,
        [Parameter(Mandatory)][string]$Right,
        [Parameter(Mandatory)][string]$Sid
    )
    $entry = "*$Sid"
    $lines = @($IniText -split "`r?`n")
    $out = @()
    $inSection = $false
    $sectionSeen = $false
    $done = $false
    foreach ($line in $lines) {
        if ($line -match '^\[(.+)\]\s*$') {
            if ($inSection -and -not $done) { $out += "$Right = $entry"; $done = $true }
            $inSection = ($Matches[1] -eq 'Privilege Rights')
            if ($inSection) { $sectionSeen = $true }
            $out += $line
            continue
        }
        if ($inSection -and $line -match ('^\s*' + [regex]::Escape($Right) + '\s*=\s*(.*)$')) {
            $values = @($Matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
            if ($values -notcontains $entry) { $values += $entry }
            $out += "$Right = $($values -join ',')"
            $done = $true
            continue
        }
        $out += $line
    }
    if (-not $sectionSeen) { $out += '[Privilege Rights]' }
    if (-not $done) { $out += "$Right = $entry" }
    return ($out -join "`r`n")
}

function Set-LocalUserRights {
    # Host-only, elevated. Deny interactive and remote-interactive logon,
    # grant batch logon (scheduled tasks) for the orchestrator account.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Sid)
    $dir = Join-Path $env:TEMP ('slate-secedit-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    try {
        $inf = Join-Path $dir 'rights.inf'
        $db = Join-Path $dir 'rights.sdb'
        & secedit.exe /export /cfg $inf /areas USER_RIGHTS | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "secedit /export exited $LASTEXITCODE" }
        $text = Get-Content -Raw -LiteralPath $inf
        foreach ($right in 'SeDenyInteractiveLogonRight', 'SeDenyRemoteInteractiveLogonRight', 'SeBatchLogonRight') {
            $text = Add-SidToUserRight -IniText $text -Right $right -Sid $Sid
        }
        Set-Content -LiteralPath $inf -Value $text -Encoding unicode
        & secedit.exe /configure /db $db /cfg $inf /areas USER_RIGHTS | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "secedit /configure exited $LASTEXITCODE" }
    } finally {
        Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
```

Add `Add-SidToUserRight, Set-LocalUserRights` to `Export-ModuleMember`.

- [ ] **Step 4: Write the host setup script**

`scripts/ci-host/install/setup-host.ps1`:

```powershell
#Requires -Version 7
#Requires -RunAsAdministrator
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# One-time, elevated host setup for the self-hosted Windows runner
# (idempotent: re-run after a reboot or to refresh C:\slate-ci\bin).
# Creates the unprivileged orchestrator account, the directory tree and
# ACLs, the isolated NAT switch and host firewall rule, the formatted
# cache parents, copies scripts/ci-host into place and registers the
# scheduled tasks. Everything after this runs as slate-ci-host.
[CmdletBinding()]
param(
    [string]$Root = 'C:\slate-ci',
    [string]$Account = 'slate-ci-host',
    [string]$SwitchName = 'slate-ci',
    [string]$NatPrefix = '10.77.0.0/24',
    [string]$Gateway = '10.77.0.1',
    [string[]]$CacheLanes = @('rust', 'app', 'model'),
    [int]$CacheGB = 60
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Import-Module (Join-Path $source 'SlateCiHost.psm1') -Force

Write-Host '1/9 Hyper-V'
$feature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All
if ($feature.State -ne 'Enabled') {
    Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All -NoRestart | Out-Null
    Write-Warning 'Hyper-V was enabled. Reboot, then run this script again.'
    exit 3
}

Write-Host '2/9 directories'
foreach ($dir in 'bin', 'golden', 'cache', 'vms', 'state', 'logs') {
    New-Item -ItemType Directory -Force -Path (Join-Path $Root $dir) | Out-Null
}

Write-Host "3/9 account $Account (Hyper-V Administrators only, no interactive logon)"
$password = New-RandomPassword -Length 32
$secure = ConvertTo-SecureString -String $password -AsPlainText -Force
if (Get-LocalUser -Name $Account -ErrorAction SilentlyContinue) {
    Set-LocalUser -Name $Account -Password $secure -PasswordNeverExpires $true
} else {
    New-LocalUser -Name $Account -Password $secure -PasswordNeverExpires -UserMayNotChangePassword -AccountNeverExpires `
        -Description 'Slate CI host orchestrator (Hyper-V Administrators only)' | Out-Null
}
if (-not (Get-LocalGroupMember -Group 'Hyper-V Administrators' -Member $Account -ErrorAction SilentlyContinue)) {
    Add-LocalGroupMember -Group 'Hyper-V Administrators' -Member $Account
}
if (Get-LocalGroupMember -Group 'Administrators' -Member $Account -ErrorAction SilentlyContinue) {
    Remove-LocalGroupMember -Group 'Administrators' -Member $Account
}
Set-LocalUserRights -Sid (Get-LocalUser -Name $Account).SID.Value

Write-Host '4/9 ACLs'
& icacls.exe $Root /grant "${Account}:(OI)(CI)M" /T /C | Out-Null
& icacls.exe (Join-Path $Root 'golden') /inheritance:r /grant 'Administrators:(OI)(CI)F' /grant 'SYSTEM:(OI)(CI)F' /grant "${Account}:(OI)(CI)RX" | Out-Null

Write-Host "5/9 switch $SwitchName with NAT $NatPrefix"
if (-not (Get-VMSwitch -Name $SwitchName -ErrorAction SilentlyContinue)) { New-VMSwitch -Name $SwitchName -SwitchType Internal | Out-Null }
$alias = "vEthernet ($SwitchName)"
$deadline = (Get-Date).AddSeconds(30)
while (-not (Get-NetAdapter -Name $alias -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
if (-not (Get-NetIPAddress -InterfaceAlias $alias -IPAddress $Gateway -ErrorAction SilentlyContinue)) {
    New-NetIPAddress -InterfaceAlias $alias -IPAddress $Gateway -PrefixLength 24 | Out-Null
}
if (-not (Get-NetNat -Name $SwitchName -ErrorAction SilentlyContinue)) {
    New-NetNat -Name $SwitchName -InternalIPInterfaceAddressPrefix $NatPrefix | Out-Null
}

Write-Host '6/9 host firewall: VM subnet may route through the host, never talk to it'
$ruleName = 'slate-ci: block VM subnet to host'
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Block -RemoteAddress $NatPrefix -LocalAddress $Gateway -InterfaceAlias $alias -Profile Any | Out-Null
}

Write-Host "7/9 cache parents ($CacheGB GB dynamic, NTFS, label slate-cache)"
foreach ($lane in $CacheLanes) {
    $path = Join-Path $Root "cache\$lane.vhdx"
    if (Test-Path -LiteralPath $path) { Write-Host "  $lane exists, kept"; continue }
    New-VHD -Path $path -SizeBytes ([int64]$CacheGB * 1GB) -Dynamic | Out-Null
    $disk = Mount-VHD -Path $path -Passthru | Get-Disk
    try {
        Initialize-Disk -Number $disk.Number -PartitionStyle GPT
        $partition = New-Partition -DiskNumber $disk.Number -UseMaximumSize -AssignDriveLetter
        Format-Volume -Partition $partition -FileSystem NTFS -NewFileSystemLabel 'slate-cache' -Confirm:$false | Out-Null
        New-Item -ItemType Directory -Force -Path ('{0}:\cache' -f $partition.DriveLetter) | Out-Null
    } finally {
        Dismount-VHD -Path $path
    }
    Set-Content -LiteralPath (Join-Path $Root "cache\$lane.gen") -Value '0' -NoNewline
    Write-Host "  $lane created"
}

Write-Host "8/9 bin <- $source"
& robocopy.exe $source (Join-Path $Root 'bin') /MIR /XD tests /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy exited $LASTEXITCODE" }
(& git -C $source rev-parse HEAD) | Set-Content -LiteralPath (Join-Path $Root 'bin\install-commit.txt')

Write-Host '9/9 scheduled tasks'
$pwsh = (Get-Command pwsh).Source
# A zero ExecutionTimeLimit means "no limit" (PT0S); the runbook verifies
# the task's "Stop the task if it runs longer than" box is unchecked.
$loopSettings = New-ScheduledTaskSettingsSet -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit (New-TimeSpan -Seconds 0) -MultipleInstances IgnoreNew -StartWhenAvailable
Register-ScheduledTask -TaskName 'slate-ci-orchestrator' -Force `
    -Action (New-ScheduledTaskAction -Execute $pwsh -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$Root\bin\orchestrator.ps1`"" -WorkingDirectory "$Root\bin") `
    -Trigger (New-ScheduledTaskTrigger -AtStartup) -User $Account -Password $password -RunLevel Limited -Settings $loopSettings | Out-Null
Register-ScheduledTask -TaskName 'slate-ci-store-token' -Force `
    -Action (New-ScheduledTaskAction -Execute $pwsh -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$Root\bin\install\store-token.ps1`" -Convert -Root `"$Root`"") `
    -User $Account -Password $password -RunLevel Limited -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 5)) | Out-Null
Remove-Variable password, secure

Write-Host ''
Write-Host 'Host setup complete. Next:'
Write-Host "  1. $Root\bin\install\store-token.ps1   (elevated; stores the PAT for $Account)"
Write-Host "  2. $source\golden\build-golden.ps1 -IsoPath <Win11 ISO>   (elevated; ~40 min)"
Write-Host "  3. Start-ScheduledTask slate-ci-orchestrator; Get-Content $Root\logs\orchestrator-*.log -Tail 20"
```

- [ ] **Step 5: Write the token store script**

`scripts/ci-host/install/store-token.ps1`:

```powershell
#Requires -Version 7
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Stores the orchestrator's fine-grained PAT DPAPI-bound to slate-ci-host
# without ever logging on as that account: the elevated caller writes
# the token to an ACL'd temp file and starts the on-demand task
# slate-ci-store-token, which runs this script with -Convert as
# slate-ci-host, Export-Clixml's a SecureString (DPAPI for that user)
# and deletes the plaintext. The caller verifies both.
[CmdletBinding()]
param([switch]$Convert, [string]$Root = 'C:\slate-ci')

$ErrorActionPreference = 'Stop'
$plainPath = Join-Path $Root 'state\token.txt'
$xmlPath = Join-Path $Root 'state\token.xml'

if ($Convert) {
    if (-not (Test-Path -LiteralPath $plainPath)) { throw "nothing to convert at $plainPath" }
    $token = (Get-Content -Raw -LiteralPath $plainPath).Trim()
    ConvertTo-SecureString -String $token -AsPlainText -Force | Export-Clixml -LiteralPath $xmlPath
    Remove-Item -LiteralPath $plainPath -Force
    exit 0
}

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'run this elevated' }
if (-not (Get-ScheduledTask -TaskName 'slate-ci-store-token' -ErrorAction SilentlyContinue)) { throw 'run install/setup-host.ps1 first' }

$secure = Read-Host -Prompt 'Fine-grained PAT for coryj627/slate (hidden)' -AsSecureString
$plain = [System.Net.NetworkCredential]::new('', $secure).Password.Trim()
if ($plain -notmatch '^github_pat_[A-Za-z0-9_]{20,}$') { throw 'that is not a fine-grained PAT (github_pat_...)' }
Set-Content -LiteralPath $plainPath -Value $plain -NoNewline -Encoding ascii
& icacls.exe $plainPath /inheritance:r /grant 'Administrators:F' /grant 'slate-ci-host:R' | Out-Null
Remove-Variable plain, secure

Start-ScheduledTask -TaskName 'slate-ci-store-token'
$deadline = (Get-Date).AddSeconds(60)
do { Start-Sleep -Seconds 1 } while ((Get-ScheduledTask -TaskName 'slate-ci-store-token').State -eq 'Running' -and (Get-Date) -lt $deadline)
$info = Get-ScheduledTaskInfo -TaskName 'slate-ci-store-token'
if (Test-Path -LiteralPath $plainPath) {
    Remove-Item -LiteralPath $plainPath -Force
    throw "the conversion task did not consume the token (LastTaskResult $($info.LastTaskResult)); plaintext deleted, nothing stored"
}
if (-not (Test-Path -LiteralPath $xmlPath)) { throw 'token.xml missing after conversion' }
Write-Host "token stored at $xmlPath (readable only by slate-ci-host via DPAPI)"
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: every test in the suite passes.

- [ ] **Step 7: Commit**

```bash
git add scripts/ci-host/install scripts/ci-host/SlateCiHost.psm1 scripts/ci-host/tests/Install.Tests.ps1
git commit -m "feat(ci-host): host setup — unprivileged account, NAT switch, cache parents, tasks; DPAPI token store"
```

---

### Task 12: Workflow pool switch

**Files:**
- Modify: `.github/workflows/windows.yml` (header comment; `runs-on` of `rust-tests`, `windows`, `windows-model-shard`, `flaui`; `if:` on the three `nscloud-cache-action` steps)
- Modify: `.github/workflows/nightly.yml` (`windows-full-stress` `runs-on` and the native-build `cache` input)
- Modify: `.github/workflows/windows-ci-pilot.yml` (`home` candidate; isolation evidence step)
- Create: `scripts/ci-host/tests/Workflows.Tests.ps1`

**Interfaces:**
- Consumes: repository variable `SLATE_WINDOWS_POOL` (`home` default, `namespace` fallback), lane labels from the Global Constraints, `NSC_CACHE_PATH` exported by the guest (Task 9).
- Produces: workflows that route by the variable; the Namespace strings are kept verbatim so `namespace` reproduces today's runs exactly.

- [ ] **Step 1: Write the failing tests**

`scripts/ci-host/tests/Workflows.Tests.ps1`:

```powershell
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Text-level guards over the three workflows that read SLATE_WINDOWS_POOL.
# YAML parsing happens on push (GitHub refuses a malformed workflow at
# the first run); these tests pin the routing contract itself.

BeforeAll {
    $script:wf = Join-Path $PSScriptRoot '..' '..' '..' '.github' 'workflows'
    $script:windows = Get-Content -Raw (Join-Path $wf 'windows.yml')
    $script:nightly = Get-Content -Raw (Join-Path $wf 'nightly.yml')
    $script:pilot = Get-Content -Raw (Join-Path $wf 'windows-ci-pilot.yml')
}

Describe 'windows.yml pool switch' {
    It 'routes every Windows lane by the variable with today''s Namespace strings kept verbatim' {
        foreach ($pair in @(@('rust', 'slate-windows-rust'), @('app', 'slate-windows-app'), @('model', 'slate-windows-model'))) {
            $lane = $pair[0]; $tag = $pair[1]
            $expected = "runs-on: `${{ vars.SLATE_WINDOWS_POOL == 'namespace' && format('namespace-profile-winx64-fast{0};overrides.cache-tag=$tag', github.ref != 'refs/heads/main' && '-pr' || '') || 'slate-win-$lane' }}"
            $windows.Contains($expected) | Should -BeTrue -Because "lane $lane must carry the exact expression"
        }
        $windows.Contains("runs-on: `${{ vars.SLATE_WINDOWS_POOL == 'namespace' && 'windows-latest' || 'slate-win-shell' }}") | Should -BeTrue
    }
    It 'keeps no bare Namespace or windows-latest runs-on for the Windows lanes' {
        ([regex]::Matches($windows, 'runs-on: namespace-profile')).Count | Should -Be 0
        ([regex]::Matches($windows, 'runs-on: windows-latest')).Count | Should -Be 0
    }
    It 'gates all three Namespace cache mounts on the namespace pool' {
        ([regex]::Matches($windows, 'namespacelabs/nscloud-cache-action')).Count | Should -Be 3
        ([regex]::Matches($windows, [regex]::Escape("if: `${{ vars.SLATE_WINDOWS_POOL == 'namespace' }}"))).Count | Should -Be 3
    }
    It 'documents the switch in the header' {
        $windows | Should -Match 'SLATE_WINDOWS_POOL'
        $windows | Should -Match 'self-hosted-windows-runner\.md'
    }
}

Describe 'nightly.yml pool switch' {
    It 'routes the Windows stress job and disables the GitHub-cache restore on home' {
        $nightly.Contains("runs-on: `${{ vars.SLATE_WINDOWS_POOL == 'namespace' && 'windows-latest' || 'slate-win-app' }}") | Should -BeTrue
        $nightly.Contains("cache: `${{ vars.SLATE_WINDOWS_POOL == 'namespace' && 'true' || 'false' }}") | Should -BeTrue
    }
}

Describe 'windows-ci-pilot.yml home candidate' {
    It 'offers home and routes its three jobs to the lane labels' {
        $pilot | Should -Match 'options: \[hosted, namespace-8x16, namespace-4x8, home\]'
        ([regex]::Matches($pilot, "inputs\.runner == 'home' && 'slate-win-app'")).Count | Should -Be 1
        ([regex]::Matches($pilot, "inputs\.runner == 'home' && 'slate-win-model'")).Count | Should -Be 1
        ([regex]::Matches($pilot, "inputs\.runner == 'home' && 'slate-win-shell'")).Count | Should -Be 2
    }
    It 'records isolation evidence on home' {
        $pilot | Should -Match 'Isolation evidence'
        $pilot | Should -Match '10\.77\.0\.1'
        $pilot | Should -Match 'github\.com'
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1 -Path scripts/ci-host/tests/Workflows.Tests.ps1
```

Expected: FAIL on every `Should -BeTrue`.

- [ ] **Step 3: Edit windows.yml — the four `runs-on` lines**

Replace, in job `rust-tests`:

```yaml
    runs-on: namespace-profile-winx64-fast${{ github.ref != 'refs/heads/main' && '-pr' || '' }};overrides.cache-tag=slate-windows-rust
```

with:

```yaml
    runs-on: ${{ vars.SLATE_WINDOWS_POOL == 'namespace' && format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-rust', github.ref != 'refs/heads/main' && '-pr' || '') || 'slate-win-rust' }}
```

In job `windows`, the same line with `slate-windows-app` / `slate-win-app`. In job `windows-model-shard`, with `slate-windows-model` / `slate-win-model`.

In job `flaui`, replace:

```yaml
    needs: [windows]
    runs-on: windows-latest
```

with:

```yaml
    needs: [windows]
    runs-on: ${{ vars.SLATE_WINDOWS_POOL == 'namespace' && 'windows-latest' || 'slate-win-shell' }}
```

- [ ] **Step 4: Edit windows.yml — gate the three Namespace cache mounts**

For the step `- name: Cache Rust build (Namespace NVMe cache)` (rust-tests) and both steps named `- name: Cache Rust build + NuGet packages (Namespace NVMe cache)` (windows, windows-model-shard), insert this line directly after the `- name:` line, before the comment and `uses:`:

```yaml
        if: ${{ vars.SLATE_WINDOWS_POOL == 'namespace' }}
```

Resulting shape for the first one:

```yaml
      - name: Cache Rust build (Namespace NVMe cache)
        if: ${{ vars.SLATE_WINDOWS_POOL == 'namespace' }}
        # `rust` mode mounts ~/.cargo/{registry,git,.global-cache} and
```

The attestation and footprint steps stay as they are: on `home` the guest exports `NSC_CACHE_PATH=D:\cache` (or whatever letter the volume took) and they read it unchanged.

- [ ] **Step 5: Edit windows.yml — header comment**

Replace the paragraph that begins `# Runner selection recorded per §W0-2 item 3, revised 2026-07-31:` (through `# history at a5037b35.`) with:

```yaml
# Runner selection, revised 2026-10-10: the repository variable
# SLATE_WINDOWS_POOL routes every Windows lane. `home` (the default when
# unset) targets the self-hosted pool on the owner's desktop — one
# throwaway Hyper-V VM per job with labels slate-win-{rust,app,model,
# shell}; docs/runbooks/self-hosted-windows-runner.md has the design,
# the fallback flip and the trust model. `namespace` reproduces the
# 2026-07-31 arrangement verbatim: namespace-profile-winx64-fast[-pr]
# (dashboard-configured, Cache Volumes enabled) for the build/test lanes
# and GitHub-hosted windows-latest for the shell gate. Flip with
# `gh variable set SLATE_WINDOWS_POOL --body namespace` (and back with
# `--body home`); nothing else changes. The per-attempt actions/cache
# scheme both replaced is in git history at a5037b35.
#
# On `home` the cache is a per-lane VHDX forked copy-on-write for every
# job and merged back by the host ONLY after GitHub's record shows a
# green push/schedule/dispatch on main of this repository — labels
# carry no trust there either. The attestation/footprint steps below
# read NSC_CACHE_PATH on both pools (the guest exports it for the
# mounted cache volume).
```

- [ ] **Step 6: Edit nightly.yml**

In job `windows-full-stress` replace:

```yaml
    runs-on: windows-latest
```

with:

```yaml
    runs-on: ${{ vars.SLATE_WINDOWS_POOL == 'namespace' && 'windows-latest' || 'slate-win-app' }}
```

and replace:

```yaml
      - name: Build native Windows test host
        uses: ./.github/actions/windows-native-build
```

with:

```yaml
      - name: Build native Windows test host
        uses: ./.github/actions/windows-native-build
        with:
          # On home the cargo target and NuGet folders already live on the
          # lane's cache volume (CARGO_TARGET_DIR / NUGET_PACKAGES from the
          # guest); the GitHub-cache restore would only waste minutes.
          cache: ${{ vars.SLATE_WINDOWS_POOL == 'namespace' && 'true' || 'false' }}
```

- [ ] **Step 7: Edit windows-ci-pilot.yml**

1. `options: [hosted, namespace-8x16, namespace-4x8]` → `options: [hosted, namespace-8x16, namespace-4x8, home]`, and extend the input description: `Native/app/model candidate; every candidate also runs the complete hosted shell gate (home runs its own shell gate on slate-win-shell).`
2. Job `build-app` `runs-on` → `${{ inputs.runner == 'home' && 'slate-win-app' || inputs.runner == 'namespace-4x8' && 'nscloud-windows-2022-amd64-4x8' || inputs.runner == 'namespace-8x16' && 'nscloud-windows-2022-amd64-8x16' || 'windows-latest' }}`.
3. Job `model-shard` `runs-on` → the same with `'slate-win-model'` in place of `'slate-win-app'`.
4. Job `shell` `runs-on: windows-latest` → `runs-on: ${{ inputs.runner == 'home' && 'slate-win-shell' || 'windows-latest' }}`, and in its provenance step `executionRunner = 'windows-latest'` → `executionRunner = '${{ inputs.runner == 'home' && 'slate-win-shell' || 'windows-latest' }}'`.
5. In `build-app`, both occurrences of `${{ github.event_name == 'workflow_dispatch' && inputs.cache && 'true' || 'false' }}` (the `cacheRestore` metadata and the native-build `cache:` input) → `${{ github.event_name == 'workflow_dispatch' && inputs.cache && inputs.runner != 'home' && 'true' || 'false' }}`.
6. In `build-app`, insert after the step `Record runner and toolchain` and before `Upload runner metadata before the build`:

```yaml
      - name: Isolation evidence (home pool only)
        if: ${{ inputs.runner == 'home' }}
        # The job VM must reach the Internet and nothing else: not the
        # host's NAT address, not the LAN, not the WSL/Default Switch
        # ranges, not the tailnet. Any private target that answers fails
        # the pilot; the result is recorded with the runner metadata.
        run: |
          $forbidden = @('10.77.0.1', '192.168.0.1', '192.168.0.49', '172.24.160.1', '172.19.80.1', '100.100.100.100')
          $report = [ordered]@{}
          $leak = $false
          foreach ($target in $forbidden) {
            $reachable = Test-NetConnection -ComputerName $target -Port 445 -InformationLevel Quiet -WarningAction SilentlyContinue
            $ping = Test-Connection -ComputerName $target -Count 1 -Quiet -ErrorAction SilentlyContinue
            $report[$target] = @{ tcp445 = [bool]$reachable; icmp = [bool]$ping }
            if ($reachable -or $ping) { $leak = $true }
          }
          $github = Test-NetConnection -ComputerName 'github.com' -Port 443 -InformationLevel Quiet -WarningAction SilentlyContinue
          $report['github.com:443'] = [bool]$github
          $report | ConvertTo-Json | Set-Content "$env:RUNNER_TEMP\windows-pilot\isolation.json"
          $report | ConvertTo-Json
          if ($leak) { throw 'a private address answered from inside the job VM' }
          if (-not $github) { throw 'github.com:443 unreachable from inside the job VM' }
```

and add `${{ runner.temp }}\windows-pilot\isolation.json` to the `path:` list of the step `Upload toolchain evidence before the app phase`.

- [ ] **Step 8: Run the tests to verify they pass**

```bash
pwsh -NoProfile -File scripts/ci-host/tests/Invoke-Tests.ps1
```

Expected: every test in the suite passes.

- [ ] **Step 9: Create the variable BEFORE the branch is merged, set to today's providers**

```bash
gh variable set SLATE_WINDOWS_POOL --body namespace --repo coryj627/slate
```

```bash
gh variable get SLATE_WINDOWS_POOL --repo coryj627/slate
```

Expected: `namespace`. With this value every job resolves to exactly what it runs on today, so merging the workflow change is a no-op until the host is live (Task 14 flips it).

- [ ] **Step 10: Commit**

```bash
git add .github/workflows/windows.yml .github/workflows/nightly.yml .github/workflows/windows-ci-pilot.yml scripts/ci-host/tests/Workflows.Tests.ps1
git commit -m "ci(windows): SLATE_WINDOWS_POOL routes every Windows lane to the home pool or verbatim to Namespace/windows-latest"
```

---

### Task 13: Runbook and cache-policy cross-reference

**Files:**
- Create: `docs/runbooks/self-hosted-windows-runner.md`
- Modify: `docs/runbooks/ci-cache-policy.md` (one paragraph under "Remaining operating decisions")

**Interfaces:**
- Consumes: everything above; this is the document the owner operates from.

- [ ] **Step 1: Write the runbook**

`docs/runbooks/self-hosted-windows-runner.md`:

```markdown
# Self-hosted Windows runner (CDESK)

Design: `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`.
Code: `scripts/ci-host/` (installed copy at `C:\slate-ci\bin`, commit in
`C:\slate-ci\bin\install-commit.txt`).

Every Windows job runs in a throwaway Hyper-V VM on the owner's desktop.
The orchestrator (`slate-ci-orchestrator` scheduled task, account
`slate-ci-host`, Hyper-V Administrators only) polls the repository for
queued `slate-win-*` jobs, forks a VM from the read-only golden disk and
a copy-on-write child of the lane's cache disk, hands in a single-use
JIT runner config over KVP, and after the guest shuts itself down merges
the cache child only when GitHub's record shows a green push, schedule
or dispatch on `main` of `coryj627/slate`. Labels carry no trust.

## Pool switch (fallback and return)

The repository variable `SLATE_WINDOWS_POOL` routes `windows.yml`,
`nightly.yml` and the pilot. `home` (also when unset) is this host;
`namespace` is the 2026-07-31 arrangement verbatim (Namespace profiles
for the four lanes, `windows-latest` for the shell gate and the nightly
stress job).

Fallback when the host is down, being rebuilt or misbehaving:

    gh variable set SLATE_WINDOWS_POOL --body namespace --repo coryj627/slate

Return:

    gh variable set SLATE_WINDOWS_POOL --body home --repo coryj627/slate

Jobs already queued for `slate-win-*` keep waiting for the home pool for
up to 24 h; cancel and re-run them after flipping. The flip never
touches caches on either side.

## Install (one time, in order)

1. **PAT.** Fine-grained, name `slate-ci-host-cdesk`, resource owner
   `coryj627`, repository access `coryj627/slate` only, permissions
   Actions: read, Administration: read and write, one-year expiry. Keep
   it in the password manager until step 3.
2. **Host setup (elevated, one UAC prompt).** From the repository
   checkout:

       Start-Process pwsh -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -NoExit -File scripts\ci-host\install\setup-host.ps1'

   Creates `slate-ci-host`, `C:\slate-ci`, the `slate-ci` switch with NAT
   `10.77.0.0/24`, the host firewall rule, the three cache parents and
   both scheduled tasks. Re-run any time to refresh `C:\slate-ci\bin`
   from the checkout (it mirrors `scripts/ci-host` minus tests).
3. **Token (elevated).** `C:\slate-ci\bin\install\store-token.ps1`,
   paste the PAT at the hidden prompt. It is stored DPAPI-bound to
   `slate-ci-host` at `C:\slate-ci\state\token.xml`; the plaintext file
   exists for seconds and the script verifies it is gone.
4. **Golden image (elevated, ~40–60 min).**

       scripts\ci-host\golden\build-golden.ps1 -IsoPath C:\Users\cory\Downloads\Win11_25H2_English_x64_v2.iso

   Prompts for the Windows 11 Pro key (hidden). Applies `install.wim`
   with DISM, boots once on the Default Switch to install the toolchain,
   verifies `C:\Users\runner\.slate-golden-complete`, seals the VHDX
   read-only at `C:\slate-ci\golden\win11-runner.vhdx`. If it fails, the
   build VM `slate-golden-build` is left for inspection:
   `vmconnect.exe localhost slate-golden-build`; logs are
   `C:\provision\provision.log` and `C:\Users\runner\provision-runner-user.log`
   inside the image.
5. **Start.** `Start-ScheduledTask slate-ci-orchestrator`, then
   `Get-Content C:\slate-ci\logs\orchestrator-*.log -Tail 20` should show
   `orchestrator start` and the sweep. The task restarts itself every
   minute on failure and at every boot; confirm in Task Scheduler that
   "Stop the task if it runs longer than" is unchecked.
6. **Repository.** Fork-PR approval → all external contributors:

       gh api -X PUT repos/coryj627/slate/actions/permissions/fork-pr-contributor-approval -f approval_policy=all_external_contributors

   Then flip the pool to `home` (above).

## Daily operation

- **Logs:** `C:\slate-ci\logs\orchestrator-<date>.log`, one line per
  transition (`provisioned`, `handed off`, `claimed`, `commit <lane>
  generation N`, `discard (<reason>)`, `sweep: …`). Inside a VM:
  `C:\actions-runner\bootstrap-system.log`, `bootstrap-runner.log`,
  `_diag\`.
- **State:** `C:\slate-ci\state\journal.json` (active VMs, retries, seen
  jobs). A corrupt journal is moved to `journal.json.corrupt-<time>` and
  the sweep rebuilds from live state.
- **Cache generations:** `Get-Content C:\slate-ci\cache\*.gen`. They move
  only on trusted commits.
- **Activation:** job VMs may report Windows as not activated: the key is
  cleared from the image with `slmgr /cpky` after activation so no job can
  read it. Cosmetic, by design.
- **Capacity:** two slots (4 vCPU + 12 GB each). A main push runs app →
  shell in one slot and rust → model 0 → model 1 in the other.
- **Disk:** golden ~40 GB, cache parents up to 60 GB each, live children
  up to ~15 GB each; plan 200 GB. Monthly, with the task stopped:
  `Optimize-VHD -Path C:\slate-ci\cache\<lane>.vhdx -Mode Full` (elevated).
- **Drain:** `Stop-ScheduledTask slate-ci-orchestrator` lets nothing new
  start; running VMs finish their job and shut down, but nobody settles
  them until the task restarts (the sweep then removes them WITHOUT
  committing). To drain gracefully: flip the pool to `namespace`, wait for
  the log to show zero active VMs, then stop the task.
- **Host reboot or Windows Update:** in-flight jobs fail on GitHub as
  lost runners; the startup sweep removes their VMs and offline runners;
  re-run the jobs. Host sleep is disabled on AC; keep it so.
- **Inspecting a live job VM:** `vmconnect.exe localhost <slate-win-…>`
  (the console is an interactive `runner` desktop). Do not stop it by
  hand unless you intend the job to fail and the cache fork to be
  discarded.

## Refreshing the golden image

Rebuild when any of these change: `rust-toolchain.toml` channel,
`apps/slate-windows/uniffi-bindgen-cs.version`, the runner version
(GitHub refuses runners outside its support window), the .NET SDK band,
or Windows itself needs servicing. Update `scripts/ci-host/golden/versions.json`
(the Pester suite checks it against the repository pins), merge, then:

1. Flip the pool to `namespace` and wait for zero active VMs.
2. `Stop-ScheduledTask slate-ci-orchestrator`.
3. Move the old disk aside: `Rename-Item C:\slate-ci\golden\win11-runner.vhdx win11-runner.prev.vhdx`
   (elevated; it is read-only).
4. Re-run `setup-host.ps1` (refreshes `bin`), then `build-golden.ps1`.
5. Start the task, flip back to `home`, dispatch `windows.yml` on a
   branch, delete the `.prev` disk once green.

Cache parents survive a golden rebuild untouched.

## Rotating the token

Create the new PAT, run `store-token.ps1` (elevated), then
`Stop-ScheduledTask slate-ci-orchestrator; Start-ScheduledTask slate-ci-orchestrator`
(the token is read at start). Revoke the old PAT.

## Security model in one screen

| Boundary | Control |
|---|---|
| Who can run code here | Only approved workflows: fork PRs need an owner click every time; non-fork branches are the owner's own commits (and Renovate's). |
| What a job can touch | A VM that exists for that job only, as a standard user, with no secrets inside. |
| Where a job can connect | The Internet via host NAT. Hyper-V port ACLs drop everything to 10/8, 172.16/12, 192.168/16, 100.64/10 (Tailscale), 169.254/16 and all IPv6; the host firewall drops inbound from the VM subnet to the host. |
| What survives a job | Only a cache merge, and only after the host verifies provenance from GitHub's API (green push/schedule/dispatch on `main` of this repo, guest shut itself down, parent generation unchanged). |
| What the host account can do | `slate-ci-host` is a Hyper-V Administrator, not an Administrator; cannot log on interactively; writes only under `C:\slate-ci`; holds the one PAT (Actions read, Administration write on this repo). |

Recommended, not done by the runner work: branch protection on `main`
requiring "build + test (windows x64)".

## Observed timings

Fill in from the acceptance runs (runbook task 14 of the plan); the
spec's estimates were app 12–15 min, shell 6–8 min, main push ~25 min.

| Job | Namespace / hosted (2026-10-09) | home (first green) | home (warm) |
|---|---|---|---|
| rust tests | 2 min | | |
| app build + test | 28 min | | |
| app model shard (each) | 6 min | | |
| shell accessibility gate | 17 min | | |
| main push, wall | 45 min | | |
```

- [ ] **Step 2: Cross-reference from the cache policy runbook**

Append to the end of the "Remaining operating decisions" section in `docs/runbooks/ci-cache-policy.md`:

```markdown
The self-hosted `home` pool (`docs/runbooks/self-hosted-windows-runner.md`)
does not use Namespace volumes or GitHub caches for the Windows lanes. Its
per-lane VHDX parents are forked copy-on-write for every job and merged
back only after the host verifies, from GitHub's API, that the job was a
green push/schedule/dispatch on `main` of this repository and that the
guest shut itself down. That decision is made by the host, never by a
label a PR can request, so no provider policy needs establishing for it.
When the pool variable is flipped to `namespace`, the Namespace tags above
are in use again and everything in this document applies unchanged.
```

- [ ] **Step 3: Commit**

```bash
git add docs/runbooks/self-hosted-windows-runner.md docs/runbooks/ci-cache-policy.md
git commit -m "docs(runbooks): self-hosted Windows runner — install, fallback flip, golden refresh, security model"
```

---

### Task 14: Host bring-up and acceptance

Operational, on CDESK, with the owner at the console for the elevated
steps. Nothing here is code; every step has a command and an expected
observation. Record observations in the runbook's "Observed timings"
table as a final commit.

**Files:**
- Modify: `docs/runbooks/self-hosted-windows-runner.md` (timings table)

- [ ] **Step 1: Push the branch and confirm the Pester lane is green**

```bash
git push -u origin HEAD
```

```bash
gh pr create --draft --title "ci(windows): self-hosted runner on CDESK — throwaway Hyper-V VM per job, provenance-gated caches, pool switch" --body-file docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md
```

```bash
gh run list --workflow ci-host.yml --limit 1
```

Expected: `success`. The `windows.yml` run on the PR still routes to Namespace because the variable is `namespace` (Task 12 step 9); it must be green too.

- [ ] **Step 2: Repository policy**

```bash
gh api -X PUT repos/coryj627/slate/actions/permissions/fork-pr-contributor-approval -f approval_policy=all_external_contributors
```

```bash
gh api repos/coryj627/slate/actions/permissions/fork-pr-contributor-approval
```

Expected: `{"approval_policy":"all_external_contributors"}`.

- [ ] **Step 3: Host setup (owner, elevated)**

From the checkout root in a normal pwsh:

```bash
Start-Process pwsh -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -NoExit -File scripts\ci-host\install\setup-host.ps1'
```

Expected in the elevated window: lines `1/9` … `9/9`, "Host setup complete", no red. Verify as a normal user:

```bash
Get-LocalGroupMember 'Hyper-V Administrators'; Get-VMSwitch slate-ci; Get-NetNat; Get-ScheduledTask slate-ci-orchestrator, slate-ci-store-token | Select-Object TaskName, State; Get-ChildItem C:\slate-ci\cache
```

Expected: `slate-ci-host` in the group; switch `slate-ci` Internal; NAT `slate-ci` on `10.77.0.0/24`; both tasks `Ready`; `rust.vhdx app.vhdx model.vhdx` and three `.gen` files.

- [ ] **Step 4: Store the token (owner, elevated)**

```bash
Start-Process pwsh -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -NoExit -File C:\slate-ci\bin\install\store-token.ps1'
```

Paste the PAT at the hidden prompt. Expected: `token stored at C:\slate-ci\state\token.xml`; `Test-Path C:\slate-ci\state\token.txt` is `False`.

- [ ] **Step 5: Build the golden image (owner, elevated, ~40–60 min)**

```bash
Start-Process pwsh -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -NoExit -File scripts\ci-host\golden\build-golden.ps1 -IsoPath C:\Users\cory\Downloads\Win11_25H2_English_x64_v2.iso'
```

Enter the Windows 11 Pro key at the hidden prompt. Expected: "Applying 'Windows 11 Pro'", a stream of `provisioning (Running)` lines, the tail of `provision.log` ending in `phase 1 complete`, then "Golden image ready and read-only". If the loop exceeds 150 min, open `vmconnect.exe localhost slate-golden-build` and read the logs named in the runbook before deciding anything.

- [ ] **Step 6: Start the orchestrator and watch the sweep**

```bash
Start-ScheduledTask slate-ci-orchestrator; Start-Sleep 15; Get-Content (Get-ChildItem C:\slate-ci\logs\orchestrator-*.log | Sort-Object LastWriteTime | Select-Object -Last 1).FullName -Tail 10
```

Expected: `orchestrator start (pid …, user slate-ci-host …)` and no `error` lines. `gh api repos/coryj627/slate/actions/runners --jq .total_count` is `0` (nothing registered until a job queues).

- [ ] **Step 7: First home run on the branch (main is untouched: its windows.yml still hardcodes Namespace)**

```bash
gh variable set SLATE_WINDOWS_POOL --body home --repo coryj627/slate
```

```bash
gh workflow run windows.yml --ref claude/windows-github-actions-runner-a0acb4 && Start-Sleep 20 && gh run list --workflow windows.yml --limit 1
```

Watch both sides:

```bash
gh run watch $(gh run list --workflow windows.yml --limit 1 --json databaseId --jq '.[0].databaseId')
```

```bash
Get-Content (Get-ChildItem C:\slate-ci\logs\orchestrator-*.log | Sort-Object LastWriteTime | Select-Object -Last 1).FullName -Wait -Tail 5
```

Expected log sequence per job: `provisioned for job …` → `handed off (runner …)` → (optionally `claimed`) → `discard (event: workflow_dispatch … branch: claude/…)` — a branch dispatch is not `main`, so caches are discarded by design. Expected GitHub: all four lanes plus the shell gate green on runners named `slate-win-*`. If a lane fails, its artifact `slate-windows-app-results-*` and the VM's `bootstrap-*.log` (visible in the orchestrator log only as the discard reason; connect to a live VM with `vmconnect` to read them) are the first stops.

- [ ] **Step 8: Isolation evidence through the pilot**

```bash
gh workflow run windows-ci-pilot.yml --ref claude/windows-github-actions-runner-a0acb4 -f runner=home -f cache=false
```

Expected: the `Isolation evidence (home pool only)` step prints `false` for every private target and `true` for `github.com:443`; the whole pilot is green. Download `windows-pilot-native-*` and keep `isolation.json` with the PR.

- [ ] **Step 9: Fallback round trip**

```bash
gh variable set SLATE_WINDOWS_POOL --body namespace --repo coryj627/slate && gh workflow run windows.yml --ref claude/windows-github-actions-runner-a0acb4
```

Expected: the lanes run on `namespace-profile-winx64-fast-pr` and `windows-latest` exactly as before this branch, green, and the orchestrator log shows nothing new. Then:

```bash
gh variable set SLATE_WINDOWS_POOL --body home --repo coryj627/slate
```

- [ ] **Step 10: Merge and watch the first trusted commits**

Mark the PR ready and merge (squash, as the repository does). The merge push runs `windows.yml` on `main` on the home pool.

```bash
gh run watch $(gh run list --workflow windows.yml --branch main --limit 1 --json databaseId --jq '.[0].databaseId'); Get-Content C:\slate-ci\cache\*.gen
```

Expected: green; the log shows `commit rust generation 1`, `commit app generation 1`, `commit model generation 1` (the second model shard logs `discard (generation moved: fork 0, parent 1)`, which is first-write-wins working); the three `.gen` files read `1`; the shell lane logs `done, lane shell has no cache`.

- [ ] **Step 11: Warm attestation and PR discard**

Push any trivial Windows-touching commit to `main` (or re-run the last main workflow):

```bash
gh workflow run windows.yml --ref main
```

Expected: the "Cache mount state (pre-build)" step of each lane lists non-zero GiB under `cargo`, `target`, `nuget` (warm), and the generations advance to `2`. Then open a throwaway branch PR touching `apps/slate-windows/README.md` or similar; its run reads warm too and the log ends in `discard (event: pull_request)` for every lane.

- [ ] **Step 12: Nightly on the home pool**

```bash
gh workflow run nightly.yml --ref main
```

Expected: `Windows full-tier native stress` runs on a `slate-win-app-*` runner and the app generation advances (schedule/dispatch on `main` commits).

- [ ] **Step 13: Lost-VM behaviour**

While a branch job is running (dispatch one), from an elevated pwsh:

```bash
Stop-VM -Name (Get-VM | Where-Object Name -like 'slate-win-*' | Select-Object -First 1 -ExpandProperty Name) -TurnOff -Force
```

Expected: the job fails on GitHub within a minute (runner lost); the orchestrator logs `discard (conclusion: failure)` or `discard (no job resolved for runner)` and removes the VM; `.gen` files are unchanged; the next tick admits nothing for that job (it is no longer queued).

- [ ] **Step 14: Record timings and close**

Fill the "Observed timings" table in `docs/runbooks/self-hosted-windows-runner.md` from the `main` runs in steps 10 and 11 (`gh run view <id> --json jobs`), then:

```bash
git add docs/runbooks/self-hosted-windows-runner.md
git commit -m "docs(runbooks): self-hosted Windows runner — observed timings from the first home runs"
git push
```

Expected end state: `SLATE_WINDOWS_POOL=home`, three cache generations ≥ 2, the orchestrator task `Running`, the runbook's table filled, and Namespace one `gh variable set` away.
