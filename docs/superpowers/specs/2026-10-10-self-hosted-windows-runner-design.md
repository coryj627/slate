# Self-hosted Windows CI runner on CDESK — Design

**Status:** Sections 1–6 approved in conversation on 2026-10-10. Written spec
awaiting owner review before an implementation plan is written.

## Goal

Run every Windows GitHub Actions job for `coryj627/slate` on the owner's
desktop (`CDESK`: Ryzen 7 9800X3D, 64 GB, 2 TB NVMe, Windows 11 Pro 26H2,
Hyper-V installed) instead of Namespace and GitHub-hosted `windows-latest`,
with Namespace and `windows-latest` retained as a one-command fallback. The
design optimises for isolation first and wall time second, because the
repository is public and GitHub's hardening guide says self-hosted runners
"should almost never be used for public repositories".

Today's Windows jobs and where they run:

| Job (workflow) | Runner today | Last green main timing |
|---|---|---|
| `rust-tests` (windows.yml) | `namespace-profile-winx64-fast[-pr]`, tag `slate-windows-rust` | 2 min |
| `windows` app build + test (windows.yml) | same profile, tag `slate-windows-app` | 28 min (21 min is the serial WPF test step) |
| `windows-model-shard` ×2 (windows.yml) | same profile, tag `slate-windows-model` | 6 min each |
| `flaui` shell accessibility gate (windows.yml) | GitHub-hosted `windows-latest` | 17 min, after the app lane via artifact |
| `windows-full-stress` (nightly.yml) | GitHub-hosted `windows-latest` | nightly only |
| `build-app` / `model-shard` / `shell` (windows-ci-pilot.yml) | `windows-latest` or direct `nscloud-windows-2022-amd64-{4x8,8x16}` | dispatch only |

Critical path of a main push today: app lane (28 min) → shell gate (17 min)
≈ 45 min wall.

## Owner decisions recorded (2026-10-10)

- **D-1 Scope:** everything Windows moves, including the shell gate and the
  nightly stress job, not only the Namespace lanes.
- **D-2 Capacity:** CI may use half the box at all times: 8 vCPU and 24 GB
  across at most two concurrent VMs (4 vCPU + 12 GB each).
- **D-3 Fallback:** manual. One repository variable selects the pool; the
  owner flips it with one `gh` command. No automatic watchdog, no new
  secrets in the repository.
- **D-4 Guest OS:** Windows 11 Pro, licensed, built from the
  `Win11_25H2_English_x64_v2.iso` already on the host. The owner supplies a
  Windows 11 Pro product key for the VM image (spare or purchased). This
  matches the OS Slate's users and the NVDA/Narrator passes run on.
- **D-5 Approach:** A — one throwaway VM per job (below). B (persistent VM
  with checkpoint restore) and C (runner on the host) were rejected.

## Chosen approach and rejected alternatives

**A. Throwaway VM per job (chosen).** A host-side orchestrator watches the
repository's queued Windows jobs. For each one it clones a fresh VM from a
read-only golden Windows 11 disk, registers a single-use just-in-time (JIT)
runner inside it, and destroys the VM when the job ends. Per-lane cache disks
are forked copy-on-write for every job and merged back only after the host
confirms, from GitHub's own record of the job, that it was a green push to
`main`. Cost: ~40 s of boot per job and an orchestrator of roughly 1,500
lines of PowerShell with tests.

**B. Persistent VM with checkpoint restore (rejected).** Simpler and ~30 s
faster per job, but GitHub guarantees one-job-per-instance only for
ephemeral runners, the runner credential persists across jobs, and the cache
must live either inside the restored image (lost every job) or on a disk
every job can write (no main-vs-PR trust).

**C. Runner directly on the host (rejected).** Fastest and zero isolation. On
a public repository this hands any approved PR the desktop, the LAN, the
tailnet and whatever is unlocked on it.

**Windows containers (rejected).** No interactive desktop, so the UIA shell
gate cannot run.

## Threat model

Assets on the host: the owner's development environment, 1Password and
other logged-in sessions, Tailscale access to other machines, the LAN, and
the `gh` credential with `repo` scope. Assets in CI: the trusted cache
lineages (poisoning them would let attacker-built artifacts reach `main`
builds) and the read-only `GITHUB_TOKEN`.

| Threat | Control |
|---|---|
| Fork PR runs arbitrary code on the runner | Fork-PR approval policy set to "all external contributors"; every fork PR waits for an owner click, every time. |
| Approved-but-malicious job escapes the runner | Job runs as a standard user inside a Gen2 Hyper-V VM that exists only for that job and is deleted afterwards. |
| Job reaches the host, LAN or tailnet | Hyper-V extended port ACLs on the VM adapter deny RFC1918, 100.64/10, 169.254/16 and all IPv6; host firewall drops inbound from the VM subnet. The host is a router for the VM, never a server. |
| Job steals a credential | No secret valid outside the VM exists in it: the product key is cleared with `slmgr /cpky`, the auto-logon password only opens a disposable isolated guest. The JIT config is single-use and expires with the job. The orchestrator's PAT lives DPAPI-encrypted on the host under a dedicated account the VM cannot reach. |
| Job poisons a trusted cache | Every job gets a copy-on-write fork; the fork is merged only when the host verifies provenance (push/schedule/dispatch on `main` of `coryj627/slate`, conclusion success, clean guest shutdown). Labels carry no trust. |
| One runner serves two jobs | JIT runners are ephemeral by construction; GitHub assigns at most one job. The host additionally deletes the VM after the first job. |
| Host compromise via the orchestrator account | `slate-ci-host` is a member of Hyper-V Administrators only, has no interactive logon, and can write only under `C:\slate-ci`. |
| Renovate branch pulls a malicious crate whose build script runs | Same VM isolation; the PR fork is discarded. After merge the code is in `main` regardless. |

Residual risk accepted by the owner: an approved fork PR, or the owner's own
branch, can run code in a throwaway VM with outbound Internet access and a
read-only `GITHUB_TOKEN`. That is the same exposure GitHub-hosted runners
have.

## Architecture

### 1. Host layout

Paths (all on the NVMe):

```
C:\slate-ci\
  bin\            pinned copy of scripts/ci-host/ from the repo (install step records the commit)
  golden\win11-runner.vhdx        read-only golden guest disk
  cache\rust.vhdx  app.vhdx  model.vhdx   per-lane cache parents (dynamic VHDX, NTFS, volume label slate-cache, formatted by install/setup-host.ps1) + <lane>.gen counters
  vms\<runner-name>\              os.vhdx and cache.vhdx differencing children, VM config
  state\                          orchestrator journal (seen jobs, active VMs)
  logs\                           rolling orchestrator logs
```

Accounts and secrets:

- `slate-ci-host`: local account, member of Hyper-V Administrators only,
  "deny log on locally", password stored nowhere after setup (the Scheduled
  Task runs it with a stored credential).
- The orchestrator runs from a Scheduled Task "At startup", as
  `slate-ci-host`, whether or not a user is logged on, restart on failure
  every minute, PowerShell 7.
- One fine-grained PAT scoped to `coryj627/slate` with **Actions: read** (list
  runs and jobs) and **Administration: write** (generate JIT configs, delete
  stale runners), one-year expiry. Stored with `Export-Clixml` of a
  `SecureString` under the `slate-ci-host` profile, which DPAPI binds to that
  account. Rotation is a runbook item.

Network:

- Internal vSwitch `slate-ci` with host NAT `10.77.0.0/24` (`New-NetNat`);
  the host owns `10.77.0.1`. Each VM slot has a fixed address (`10.77.0.11`,
  `10.77.0.12`) handed to the guest over KVP. DNS is the static public
  resolvers the host hands over in `slate.dns` (the golden image carries
  the same defaults). IPv6 disabled in the guest.
- Per-VM extended ACLs (`Add-VMNetworkAdapterExtendedAcl`), outbound deny to
  `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `100.64.0.0/10`,
  `169.254.0.0/16`, `::/0`; then allow all else. Packets to the Internet
  carry public destinations and pass; packets addressed to the host's
  `10.77.0.1`, the LAN (`192.168.0.0/24` here), WSL/Default Switch ranges
  and Tailscale are dropped at the switch port.
- Host Windows Firewall: block inbound on the `vEthernet (slate-ci)`
  interface from `10.77.0.0/24`.

Resources: two VM slots, each 4 vCPU and 12 GB static memory. Guest video
pinned to 1920×1080 with `Set-VMVideo`. Host power plan already never sleeps
on AC; the runbook records that and the hibernate-off requirement.

Disk budget: golden ~40 GB, three cache parents 60 GB max each (dynamic),
two live children up to ~15 GB each. Plan for 200 GB against the 311 GB
currently free; the footprint report in every lane shows the trend.

### 2. VM lifecycle

Labels a job may request: `slate-win-rust`, `slate-win-app`,
`slate-win-model`, `slate-win-shell`. The lane name after `slate-win-` picks
the cache parent (`shell` uses no cache disk; the nightly stress job uses
`slate-win-app`). The orchestrator registers each JIT runner with the
default labels plus exactly one of these.

State machine per job, run by a single-threaded loop with a 10 s tick:

1. **Discover.** `GET /repos/coryj627/slate/actions/runs?status=queued` and
   `?status=in_progress` (a run is `in_progress` while later jobs are still
   queued), then `GET /runs/{id}/jobs?filter=latest`; keep jobs with
   `status == queued` whose `labels` contain exactly one `slate-win-*`
   label. Record `job_id`, `run_id`, lane, `created_at`. Budget: ~3 API
   calls per tick when idle, well inside the 5,000/h limit.
2. **Admit.** While a slot is free, take the oldest queued job not already
   assigned. One VM per job; a job is never admitted twice.
3. **Provision.** `New-VHD -Differencing` for `os.vhdx` from the golden disk
   and `cache.vhdx` from the lane parent; read and record the parent's
   generation number. `New-VM` Gen2, 4 vCPU, 12 GB static, `slate-ci`
   switch, fresh local key protector and `Enable-VMTPM`, extended ACLs
   applied, automatic stop action TurnOff, checkpoints disabled.
4. **Register.** `POST /actions/runners/generate-jitconfig` with
   `name = slate-win-<lane>-<8 hex>`, `runner_group_id = 1`, `labels =
   [slate-win-<lane>]`. Keep `encoded_jit_config` only in memory.
5. **Hand off.** `Start-VM`; wait for the Heartbeat integration service to
   report OK (max 3 min, else discard). Write the config into the guest with
   `Msvm_VirtualSystemManagementService.AddKvpItems` as items
   `slate.jit.count`, `slate.jit.0 … slate.jit.N` (each ≤ 1,000 characters;
   `Msvm_KvpExchangeDataItem.Data` has MAXLEN 1024), plus `slate.lane`,
   `slate.ip`, `slate.gateway`, `slate.dns`, `slate.cache` (`1` or `0`)
   and `slate.job`.
6. **Run (guest).** Two tasks baked into the golden image, both no-ops
   until the golden build's completion marker exists.
   `slate-bootstrap-system` (SYSTEM, at startup) polls
   `HKLM:\SOFTWARE\Microsoft\Virtual Machine\External` for the chunks
   (max 5 min), sets the static IP, gateway and DNS from the KVP items,
   and, when `slate.cache` is `1`, locates the cache volume by its
   `slate-cache` label, adds a Defender real-time exclusion for its resolved
   root, creates `cache\{cargo\registry,cargo\git,target,nuget}` on it and
   writes `C:\actions-runner\.env` (`NSC_CACHE_PATH`, `CARGO_TARGET_DIR`,
   `NUGET_PACKAGES`, `SLATE_CACHE_ROOT`); with `0` (the shell lane) it
   writes an empty `.env` and skips the volume. It then writes the config to
   `C:\actions-runner\jit.cfg` readable only by `runner` and touches
   `C:\actions-runner\ready`. `slate-runner-logon` (`runner`, interactive,
   at logon) waits for `ready`, junctions
   `%USERPROFILE%\.cargo\{registry,git}` onto `SLATE_CACHE_ROOT` when `.env`
   defines it, runs `run.cmd --jitconfig <config>` in the interactive
   session, deletes `jit.cfg`, and runs `shutdown /s /t 0`. A bootstrap
   failure writes `bootstrap-error.txt` and shuts down. The host does not
   read the guest's logs; a repeated bootstrap failure is diagnosed by
   connecting to a live VM with `vmconnect` before it shuts down (follow-up:
   the SYSTEM task publishing `slate.error` over guest KVP).
7. **Settle (host).** On VM state `Off`: look up which job ran on this
   runner name (first the admitted `job_id`, else any recently seen
   candidate, via `GET /actions/jobs/{job_id}` → `runner_name`), fetch its
   run, apply the commit predicate (section 3), `Merge-VHD` or delete the
   cache child, `Remove-VM`, delete the directory, release the slot.

Failure handling:

- VM still running past the lane's `timeout-minutes` + 10 min: `Stop-VM
  -TurnOff`, discard, log at error.
- Runner registered but no job claims it within 5 min and the admitted job
  is no longer queued: `DELETE /actions/runners/{id}`, turn off, discard.
- Heartbeat never arrives, KVP never read, or any API error during
  provisioning: discard and retry the job on the next tick, with a
  10 min back-off and at most three provisioning attempts per job in total
  (the initial attempt plus two retries; `RetryCap` 3 counts failures), then
  log and stop admitting that job (it fails GitHub's 24 h queue wait, which
  the fallback variable is for).
- Orchestrator start: delete every offline runner named `slate-win-*`,
  remove every VM named `slate-win-*` and its directory, and delete orphaned
  cache children (never parents).
- Host reboot mid-job: the job fails on GitHub as a lost runner; the start
  sweep cleans up; the owner re-runs the job.

### 3. Cache trust

Every job gets a differencing child of the lane's parent. Reads are free for
everyone, including fork PRs, and give PRs the warm `main` graph.

Commit predicate, evaluated on the host from the API after the VM is off:

- the job that ran on this runner name has `conclusion == success`;
- its run has `event ∈ {push, schedule, workflow_dispatch}`,
  `head_branch == main`, `head_repository.full_name == coryj627/slate`;
- the host did not force the VM off (the guest reached `Off` by its own
  `shutdown`), which is the host's own record, not a guest claim;
- the parent's generation number equals the one recorded at fork time.

If all hold: `Merge-VHD` child → parent, increment `<lane>.gen`. Otherwise
delete the child. This is first-write-wins per generation; windows.yml's
`concurrency` group serialises `main` runs, so two trusted jobs of one lane
rarely overlap, and when they do the loser is discarded cleanly rather than
corrupting the chain.

Capacity: each parent is a dynamic VHDX with a 60 GB maximum. The existing
"Cache mount state (pre-build)" and "Cache footprint report" steps measure
`NSC_CACHE_PATH` and already filter `$RECYCLE.BIN` and `System Volume
Information`, so they work unchanged on the cache volume (the guest exports
its actual `<letter>:\cache` path). `Optimize-VHD` on the parents is a
monthly runbook item.

### 4. Guest golden image

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
uniffi-bindgen-cs), and reboots. The specialize pass also sets
`PreventDeviceEncryption=1`, so the vTPM every VM carries never triggers
Windows 11 automatic device encryption of a disposable disk. The same pass
names the computer `slate-win`; phase 1 disables sleep and hibernation,
defers Windows Update, enables long paths, excludes `C:\actions-runner`,
`C:\dotnet` and the `runner` profile's cargo and rustup trees from Defender
real-time scanning (the cache root is excluded at job time, since its drive
letter is not fixed), and uses the `provision` account only during the
build. The per-user script writes the completion marker
`C:\Users\runner\.slate-golden-complete` and shuts down. The host verifies
the marker, removes the build VM and marks the VHDX read-only. Before the
final reboot the provisioning script runs `slmgr /cpky`, which clears the
product key from the registry (Windows keeps an installed retail key
readable by standard users in `DigitalProductId` until then), and deletes
the rendered unattend and `C:\provision\secrets.json`. The auto-logon
password remains in the guest registry, as auto-logon requires; it is useful
only inside a VM that is isolated and disposable.

Toolchain, matching the Namespace image the lanes run on today:

- Visual Studio 2022 Build Tools: `VCTools` workload, `VC.Tools.ARM64`,
  Windows 11 SDK.
- `rustup` with the pinned toolchain from `rust-toolchain.toml` (1.97.1) and
  the `x86_64-pc-windows-msvc` and `aarch64-pc-windows-msvc` targets
  pre-materialised.
- .NET 10 SDK (current 10.0.4xx) installed to `C:\dotnet`, owned by
  `runner`, with machine-wide `DOTNET_ROOT` and `DOTNET_INSTALL_DIR` pointing
  there and `C:\dotnet` first on `PATH`. `actions/setup-dotnet` still runs
  in the workflow: it finds the version there and skips the download, and
  when a newer 10.0.x patch exists it can install it without elevation
  (the default `C:\Program Files\dotnet` would fail for a standard user).
- `rustup` installed per-user as `runner` (so `rustup target add` and
  `cargo install` write to `runner`'s own profile); VS Build Tools and
  Python are machine-wide.
- Python 3.13, git, 7-Zip.
- `actions-runner` 2.338.0 unpacked at `C:\actions-runner`, owned by
  `runner` (its runtime files: `.env`, `jit.cfg`, `ready`,
  `bootstrap-error.txt`, the two bootstrap logs, `_work`). The guest scripts
  `bootstrap-system.ps1` and `bootstrap-runner.ps1` and a copy of
  `SlateCiHost.psm1` live in `C:\slate-guest`, writable only by
  Administrators and SYSTEM (`runner` reads), so a job cannot alter what
  SYSTEM runs at the next boot. The two scheduled tasks
  `slate-bootstrap-system` (SYSTEM, at startup) and `slate-runner-logon`
  (`runner`, interactive, at logon) run those scripts.
- `uniffi-bindgen-cs` at the tag pinned in
  `apps/slate-windows/uniffi-bindgen-cs.version`, in `%USERPROFILE%\.cargo\bin`
  of `runner`.
- `scripts/ci-host/golden/versions.json` is the golden image's single pin
  file (runner version and SHA-256, Rust toolchain, uniffi-bindgen-cs tag,
  .NET channel, Python and Git versions, resolvers); the Pester suite
  asserts its Rust and bindgen pins equal `rust-toolchain.toml` and
  `apps/slate-windows/uniffi-bindgen-cs.version`.

Refresh triggers (runbook): the Rust toolchain pin, the uniffi-bindgen-cs
tag, the runner version (GitHub refuses runners older than its support
window), or the .NET SDK band changes. A refresh rebuilds the golden disk;
cache parents are untouched.

### 5. Workflow changes

Repository variable `SLATE_WINDOWS_POOL`: `home` (default) or `namespace`.
Every Windows `runs-on` becomes an expression on it; nothing else about job
topology changes.

| Job | `home` | `namespace` (today's value, verbatim) |
|---|---|---|
| windows.yml `rust-tests` | `slate-win-rust` | `namespace-profile-winx64-fast[-pr];overrides.cache-tag=slate-windows-rust` |
| windows.yml `windows` | `slate-win-app` | `…;overrides.cache-tag=slate-windows-app` |
| windows.yml `windows-model-shard` | `slate-win-model` | `…;overrides.cache-tag=slate-windows-model` |
| windows.yml `flaui` | `slate-win-shell` | `windows-latest` |
| nightly.yml `windows-full-stress` | `slate-win-app` | `windows-latest` |
| windows-ci-pilot.yml candidates | new `home` choice: `build-app` → `slate-win-app`, `model-shard` → `slate-win-model`, `shell` → `slate-win-shell` (the other candidates keep their hosted shell) | unchanged |

Cache steps: the two `namespacelabs/nscloud-cache-action` steps gain
`if: ${{ vars.SLATE_WINDOWS_POOL == 'namespace' }}`. The attestation and
footprint steps run on both pools unchanged. The `actions/cache` step for
`uniffi-bindgen-cs` stays; on `home` the binary is already present and the
GitHub cache (shared across runners) hits anyway. The "Assert runner
architecture" steps are pool-neutral.

Build commands do not change: `generate-bindings.ps1` already honours
`CARGO_TARGET_DIR`, cargo honours it everywhere, NuGet honours
`NUGET_PACKAGES`, and the two junctions cover the registry and git caches.

The `flaui` job keeps its artifact handoff and the `gate` aggregate keeps
its four `needs`, so "build + test (windows x64)" means what it meant.
Collapsing the shell gate into the app lane on `home` is a follow-up, not
part of this change.

Fallback and return:

```bash
gh variable set SLATE_WINDOWS_POOL --body namespace
```

```bash
gh variable set SLATE_WINDOWS_POOL --body home
```

Header comments in windows.yml and nightly.yml record the pool switch and
point at the runbook; the 2026-07-31 Namespace rationale stays as history.

### 6. Repository settings (owner actions)

- Fork-PR workflow approval → **all external contributors**
  (`PUT /repos/coryj627/slate/actions/permissions/fork-pr-contributor-approval`
  with `approval_policy: all_external_contributors`).
- Default `GITHUB_TOKEN` permission stays read-only (already the case).
- No new repository secrets. The only new repository object is the
  `SLATE_WINDOWS_POOL` variable.
- Recommended, recorded in the runbook, not done by this work: branch
  protection on `main` requiring "build + test (windows x64)".

### 7. Orchestrator design

Location in the repo: `scripts/ci-host/` (PowerShell 7, SPDX headers as in
`generate-bindings.ps1`).

```
scripts/ci-host/
  SlateCiHost.psm1          pure logic: queue selection, label parsing, admission,
                            commit predicate, KVP chunking, timeouts, state journal
  adapters/GitHub.ps1       REST calls (Invoke-RestMethod with the PAT)
  adapters/HyperV.ps1       New-VHD/New-VM/Start-VM/KVP/Merge-VHD wrappers
  orchestrator.ps1          the loop; wires adapters into the module
  install/setup-host.ps1    one-time elevated steps (features, account, switch, NAT,
                            ACL template, firewall rule, task, directories, ACLs)
  install/store-token.ps1   runs as slate-ci-host; stores the PAT
  golden/build-golden.ps1   golden image build (elevated, DISM-applied install.wim)
  golden/unattend.xml       template; golden/versions.json pins
  golden/provision-guest.ps1, golden/provision-runner-user.ps1   the two in-guest phases
  golden/guest/bootstrap-system.ps1, golden/guest/bootstrap-runner.ps1   the job-VM tasks
  tests/*.Tests.ps1         Pester 5, adapters mocked
```

Every GitHub and Hyper-V call goes through an adapter function so the module
is testable without a VM or a token. The journal under `C:\slate-ci\state`
survives restarts so an in-flight VM is settled, not orphaned. Logs are
plain text with one line per state transition and never contain a JIT
config or token.

## Performance expectations

The serial WPF test step and the UIA gate are single-thread bound; the
9800X3D's per-core speed and V-cache are the main win, not core count.

| Job | Today | Estimate on `home` |
|---|---|---|
| rust-tests | 2 min | 2 min |
| app build + test | 28 min | 12–15 min |
| model shard (each) | 6 min | 4–5 min |
| shell gate | 17 min | 6–8 min |
| Boot + register overhead | ~30 s (Namespace) | ~40 s per job |
| Main push, wall | ~45 min | ~25 min with two slots (app → shell in one slot; rust → model0 → model1 in the other) |

These are estimates; acceptance records real numbers.

## Testing

- **Unit (Pester 5, CI on `ubuntu-latest` with pwsh, path-filtered to
  `scripts/ci-host/**`):** queue selection and ordering, exactly-one-label
  rule, admission with slot limits and retry caps, the commit predicate for
  every event/branch/repository/conclusion/shutdown/generation combination,
  KVP chunking at the 1,000-character boundary and reassembly, timeout
  arithmetic, startup sweep decisions, journal round-trip.
- **Host integration (manual, recorded in the runbook):** provision a VM
  from the golden disk, confirm from inside the guest that `10.77.0.1`,
  `192.168.0.49`, the tailnet and WSL ranges are unreachable while
  `github.com` is, confirm the `slate-cache` volume mounts and the
  junctions resolve, confirm the desktop session exists (`query session`).
- **Acceptance:**
  1. `workflow_dispatch` of windows.yml on a branch runs green on `home`.
  2. A push to `main` runs green and the orchestrator log shows three
     commits (rust, app, model) with generation increments; the shell lane
     shows no cache.
  3. The next `main` run's pre-build attestation reads warm for all three.
  4. A branch run after that reads warm and the log shows "discard
     (provenance: pull_request)".
  5. Flip to `namespace`, dispatch, green; flip back, dispatch, green.
  6. Nightly dispatch runs the stress job on `slate-win-app`.
  7. Kill a VM mid-job: the job fails on GitHub, the sweep cleans up, the
     parent generation is unchanged.

## Operations

Runbook `docs/runbooks/self-hosted-windows-runner.md` covers: host install
(the elevated `setup-host.ps1` run needs one UAC approval on the console;
everything after runs unprivileged), token creation and storage, golden
image build and refresh, the fallback flip, draining (stop the task; VMs
finish or are turned off after the timeout), log locations, disk budget and
the 200 GB planning figure, monthly `Optimize-VHD`, runner version policy,
and what a host reboot does to in-flight jobs.

## Out of scope

- Automatic fallback (D-3).
- Collapsing the shell gate into the app lane.
- Moving mac or Linux lanes.
- Branch protection on `main` (recommended only).
- Nested virtualisation, GPU passthrough, or more than two slots.

## Open items

- Owner supplies the Windows 11 Pro key at golden-image build time (D-4).
- Owner runs the elevated host setup step at the console (one UAC prompt).
- Owner creates the fine-grained PAT and runs `store-token.ps1` as
  `slate-ci-host`.
