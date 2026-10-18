# Self-hosted Windows runner (CDESK)

Design: `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`.
Code: `ci/windows-runner/`. The installed copy is `C:\slate-ci\bin`, and the
commit it came from is in `C:\slate-ci\bin\install-commit.txt`.
Tests: `.github/workflows/windows-runner-tests.yml` (check
`module, adapters, scripts`). Cache policy: `docs/runbooks/ci-cache-policy.md`.

A Windows lane routed home runs in a throwaway Hyper-V VM on the owner's
desktop. The orchestrator loop is the scheduled task
`slate-ci-orchestrator`. It runs as the account `slate-ci-host`, a member
of Hyper-V Administrators only, which Microsoft treats as equivalent to an
administrator (see "Security model in one screen"). Every 10 seconds it
polls the routed workflows,
`RoutedWorkflows` in `ci/windows-runner/config.json`, for queued jobs
labelled `slate-win-*`. For each job it forks a VM from the read-only golden
disk and a copy-on-write child of the lane's cache disk. It hands in a
single-use JIT runner config over KVP. The guest shuts down when its job
ends. The loop then merges the cache child only when GitHub's record shows
a green push, schedule or dispatch on `main` of `coryj627/slate`, and the
loop did not force the VM off. Labels carry no trust.

Run every command that touches Hyper-V, the scheduled tasks or anything
under `C:\slate-ci` in an elevated PowerShell 7 window. `C:\slate-ci` does
not inherit permissions from `C:\`, so a normal prompt cannot even read its
logs. `gh` commands work in any prompt.

## Pool switch (fallback and return)

The repository variable `WINDOWS_RUNNER_MODE` routes `windows.yml` and
`nightly.yml`. In `windows.yml` a routing job (`Route Windows lanes`,
`ci/windows-runner/route/route.py`) reads it together with the state this
host publishes, when its state changes and at least once a minute, in the
repository variable `WINDOWS_HOME_STATE` (a heartbeat, whether it serves,
its slots, what runs, what it has queued), and decides a pool per lane.
The shell gate runs on GitHub-hosted `windows-latest` in every mode but
`home-only`. A lane that goes home carries one of the labels
`slate-win-rust`, `slate-win-app`, `slate-win-model` or `slate-win-shell`.
The routing job cannot fail a run: if it errors, times out or does not
run, each lane takes the vendor.

- `home`: home first. A lane takes a free slot; when none is free it queues
  at home only if it would still finish inside the run's critical path (the
  app lane plus the shell gate), and otherwise runs on Namespace at once.
  Lanes are considered in vendor-cost order: app build + test, model shard
  0, model shard 1, rust tests; the app lane never queues. The router's
  estimates live in `route.py`'s `DURATIONS` table (app 26 min at home,
  model 25, rust 11; the shell gate 13 on `windows-latest`), with 110 s per
  slot handover. A state whose `at` is more than 180 s (three minutes)
  before the routing job's own clock, or one that says the host is not
  serving or has no slot, sends every lane to Namespace by itself. Routing
  resumes as soon as the loop publishes again, and no run has to strand to
  discover an outage. The reference is the routing job's clock because
  GitHub resolves `vars` when the job starts, and a run's creation time
  never moves across re-runs or a concurrency hold. The nightly stress job
  queues home unconditionally, with the label `slate-win-app`.
- `home-only`: every lane queues home, as `home` did before 2026-10-11, and
  the shell gate runs home too. The published state is ignored. It is for
  validating a golden refresh and for fidelity runs.
- `namespace`: the 2026-07-31 arrangement, verbatim; the routing job does
  not run. The rust, app and model lanes run on the Namespace profile
  `namespace-profile-winx64-fast` (`-fast-pr` off `main`), with the cache
  tags `slate-windows-rust`, `slate-windows-app` and `slate-windows-model`.
  The shell gate and the nightly stress job run on `windows-latest`.
- Any other value, a typo or an unset variable included, means `namespace`.
  The comparison ignores case but not spaces: set the value without them.

What `home` costs and saves, from the 2026 runs. With the durations above,
an idle pool keeps app, shard 0 and rust home and pays Namespace about six
minutes for shard 1. Over a month at the 2026 run rate the rule spends
about 3,900 Namespace minutes. Plain overflow would spend about 4,500, and
all-Namespace about 10,350. A single push on an idle pool takes about 40
minutes, against about 35 all-Namespace.

Reading a run's routing: the routing job's step summary has the mode, the
state it read (its age against the routing job's clock, serving, slots,
running, queued, earlier routers), the free slots and the deadline, and a
row per lane with its pool and reason. It ends with one `Replay:` line to
paste into a Bash shell at the repository root, which reproduces the
decision. Each routed lane's first step prints `runner=…`, the runner it
landed on, and the aggregate gate's failure line prints
`route=<result> router-attempt='<attempt>'` and the pools. GitHub reports
a `continue-on-error` job's result as it sees fit (it can read `success`
for a routing job that failed); an empty `router-attempt=''` in that line
is the sign the router produced nothing, and the lanes then took the
vendor.

Fork pull requests never run on the pool, in any mode, `home-only`
included: the router sends them to the vendor, the host never provisions
for a job whose run comes from another repository (`admission: skipped job
<id> of <repo> (not the trusted repository)`), and a VM whose runner GitHub
handed to such a job is forced off within a tick (`handoff: <vm> runs job
<id> of <repo> (not the trusted repository); forcing it off`). Every fork
pull request still waits for your approval first (see "Before you start"
under Install), and that approval is the control: what the host adds is
that a fork gets at most seconds on a throwaway VM. One gap stays: the host
reads only the routed workflows' jobs, so a fork that adds a workflow of
its own naming a home label is not evicted. The approval click is the
control for that case.

A fork or Dependabot run that cannot read repository variables sees the
mode as unset. The routing job does not run, every lane takes the vendor
whatever the mode says, and the run never waits on this host, even while it
is down.

The pilot, `windows-ci-pilot.yml`, ignores the variable. Its `runner` input
picks the pool, and `home` is one of the choices. On `home`, its `cache`
input only switches the GitHub-scoped cache restore. `cache=false` is not a
cold build there: `CARGO_TARGET_DIR` and `NUGET_PACKAGES` live on the
lane's warm cache disk. A dispatch on `main` is also a trusted run, so the
host commits its cache fork like any other. Dispatch the pilot from a
branch unless you mean to warm the caches.

Fallback, when the host is down, being rebuilt or misbehaving:

```powershell
gh variable set WINDOWS_RUNNER_MODE --body namespace --repo coryj627/slate
```

Return:

```powershell
gh variable set WINDOWS_RUNNER_MODE --body home --repo coryj627/slate
```

Confirm after either flip:

```powershell
gh variable get WINDOWS_RUNNER_MODE --repo coryj627/slate
```

Jobs already queued for `slate-win-*` keep waiting for the home pool for up
to 24 hours; nothing re-routes a queued job. Flip to `namespace` first,
then cancel the run, then re-run it in full: in the run's page choose
"Re-run jobs", then "Re-run all jobs", or run `gh run rerun <id>` (not
`--failed`). A partial re-run reuses the routing job's earlier answer and
the lanes then take the vendor expression, which is safe, but it cannot
bring a cancelled lane back. While a stranded lane waits, the `main`
concurrency group holds later `main` runs behind it. The flip never
touches caches on either side.

The published state, the repository variable `WINDOWS_HOME_STATE`, shows
the last tick's view of the pool. It changes whenever a VM or the queue
changes, and at least once a minute. Read it with:

```powershell
gh variable get WINDOWS_HOME_STATE --repo coryj627/slate
```

It is one line of JSON: `v` (always 1), `at` (the tick's UTC time),
`serving`, `slots`, `running` and `queued`, the last two being lists of
`lane` and `since`. `queued` also counts jobs whose run the host has not
read yet; the host admits none of them until it has. The loop writes the
variable with its token, which needs Variables: Read and write (see
Install, step 1).

You create it once, before the loop first runs on this code. Under `home`,
the value below says the host is not serving, so every lane goes to
Namespace until the loop publishes; `home-only` ignores the state. If you
skip this step, the host's first publish creates the variable.

```powershell
gh variable set WINDOWS_HOME_STATE --repo coryj627/slate --body '{"v":1,"at":"1970-01-01T00:00:00Z","serving":false,"slots":0,"running":[],"queued":[]}'
```

## Install (one time, in order)

Before you start:

- PowerShell 7.4 or later, installed from the MSI, at
  `C:\Program Files\PowerShell\7\pwsh.exe`. Setup registers the tasks with
  that build and refuses to run under the Microsoft Store build: its
  `pwsh` alias under `WindowsApps` is one `slate-ci-host` cannot start, and
  its packaged context cannot reach the Dism module (`Class not
  registered`). Both builds can be installed side by side; what matters is
  which one your elevated window runs, which step 2 checks. This should
  print the MSI path:

  ```powershell
  (Get-Command pwsh).Source
  ```

- About 200 GB free on `C:`.
- No other Windows NAT. Windows supports one per host, so setup stops if
  `Get-NetNat` lists anything but `slate-ci`.
- No third-party NDIS protocol driver bound to the network adapters.
  Realtek's Ethernet diagnostic and teaming utility installs three
  (`rtk_vlanpt`, `rtk_teampt`, `rtk_ndisprot`), and with them present
  Hyper-V cannot create the switch's host adapter: step 5/9 fails with
  `Internal miniport create failed … Cannot create a file when that file
  already exists`. This should print nothing:

  ```powershell
  netcfg -s n | Select-String 'rtk_'
  ```

  If it lists them and no VLAN or team is in use, remove them in the
  elevated window, one at a time: `netcfg -u rtk_vlanpt`, then
  `netcfg -u rtk_teampt`, then `netcfg -u rtk_ndisprot`. Adapters rebind
  for a moment after each. Reinstalling the Realtek utility puts them
  back.
- The ISO, here `C:\Users\cory\Downloads\Windows11_Client_x64_en-us_26300_9457.iso`,
  and the Windows 11 Pro product key in the password manager.
- A clean checkout of the commit to install, normally an up-to-date `main`.
  Setup copies the working tree but records only `HEAD`.
- Fork pull requests need approval before any job can run here. Require it
  for all external contributors:

  ```powershell
  gh api -X PUT repos/coryj627/slate/actions/permissions/fork-pr-contributor-approval -f approval_policy=all_external_contributors
  ```

  Check it. Expect `{"approval_policy":"all_external_contributors"}`.

  ```powershell
  gh api repos/coryj627/slate/actions/permissions/fork-pr-contributor-approval
  ```

1. **PAT.** On GitHub, create a fine-grained personal access token:
   - name `slate-ci-host-cdesk`;
   - resource owner `coryj627`;
   - repository access: only `coryj627/slate`;
   - permissions: Actions: Read; Administration: Read and write;
     Variables: Read and write (the published state); GitHub adds Metadata
     read-only by itself;
   - expiry: one year.

   This is the mac controller's set, so one token recipe serves both hosts.
   Keep it in the password manager. You paste it in step 4, and again after
   any `-ResetAccount`.
2. **Elevated window.** From any pwsh, open an elevated one running the MSI
   build by its full path (a bare `pwsh` can resolve to the Store build),
   and approve the UAC prompt:

   ```powershell
   Start-Process 'C:\Program Files\PowerShell\7\pwsh.exe' -Verb RunAs
   ```

   In the new window, confirm which build it is. Expect
   `C:\Program Files\PowerShell\7\pwsh.exe`, never a `WindowsApps` path:

   ```powershell
   (Get-Process -Id $PID).Path
   ```

   Then go to the checkout:

   ```powershell
   Set-Location C:\dev\slate
   ```

   Use this window for steps 3 to 7.
3. **Host setup.**

   ```powershell
   .\ci\windows-runner\install\setup-host.ps1
   ```

   It prints `1/9` to `9/9`, then `Host setup complete. Next:`. It creates:
   - the account `slate-ci-host`: Hyper-V Administrators only, denied local
     and remote interactive logon, allowed to log on as a batch job;
   - `C:\slate-ci` with `bin`, `golden`, `cache`, `vms`, `state` and `logs`.
     The tree does not inherit from `C:\`. Administrators and SYSTEM have
     full control, `slate-ci-host` has Modify, `bin` and `golden` are
     read-only for it, and on `golden` and `cache` it may also change
     permissions: Hyper-V, acting as the account, writes each VM's access
     entry onto every disk of the chain it attaches;
   - the internal switch `slate-ci`, host address `10.77.0.1`, NAT
     `10.77.0.0/24` (the host address takes the prefix's length, and the
     script refuses a gateway outside the prefix before it creates
     anything);
   - the host firewall rule `slate-ci: block VM subnet to host`;
   - the cache parents `rust.vhdx`, `app.vhdx` and `model.vhdx` in
     `C:\slate-ci\cache`: 60 GB dynamic, NTFS, label `slate-cache`, each
     with a `.gen` file at 0;
   - `C:\slate-ci\bin`, a mirror of `ci/windows-runner` without the tests;
   - three scheduled tasks: `slate-ci-orchestrator`, the loop, registered
     disabled; `slate-ci-store-token`, which step 4 runs; and
     `slate-ci-golden-prune`, which removes the previous golden image once
     the current one is proven (see "Refreshing the golden image").

   If Hyper-V was off, the script enables it, asks for a reboot and exits.
   Reboot, open the elevated window again and re-run it.

   The loop stays disabled until step 7, so nothing runs before the golden
   disk passes its product-key check.

   Then exclude the VM disks from the host's Defender real-time scanning,
   which otherwise scans every write to them, as Microsoft recommends for
   Hyper-V hosts. In the same elevated window:

   ```powershell
   Add-MpPreference -ExclusionPath C:\slate-ci\golden, C:\slate-ci\cache, C:\slate-ci\vms
   ```

4. **Token.**

   ```powershell
   C:\slate-ci\bin\install\store-token.ps1
   ```

   Paste the PAT at the hidden prompt. Expect
   `token stored at C:\slate-ci\state\token.xml (readable only by slate-ci-host via DPAPI)`.
   The script writes the token to a restricted plaintext file and runs the
   task `slate-ci-store-token`, which encrypts it as `slate-ci-host`. The
   plaintext is deleted whatever happens. If the script says the conversion
   task did not consume the token, run it again: the account's first
   profile load can outlast its 60-second wait.
5. **Golden image** (40 to 60 minutes; the script gives up after 150):

   ```powershell
   .\ci\windows-runner\golden\build-golden.ps1 -IsoPath C:\Users\cory\Downloads\Windows11_Client_x64_en-us_26300_9457.iso
   ```

   Type the Windows 11 Pro key at the hidden prompt. The script applies
   `install.wim` with DISM to a new 120 GB dynamic disk. It boots the disk
   once on the Default Switch as the VM `slate-golden-build`, and prints a
   `provisioning (Running)` line every 30 seconds. Phase 1 installs the
   toolchain as the temporary admin `provision`. Phase 2 installs rustup and
   uniffi-bindgen-cs as `runner`. The script then prints the end of the
   phase 1 log and checks the completion marker
   `C:\Users\runner\.slate-golden-complete`, and that no secret is left in
   the image. It deletes the build VM and marks the disk read-only. The
   last line is
   `Golden image ready and read-only: C:\slate-ci\golden\win11-runner.vhdx`.

   If it fails, see "The golden build fails" under "When things go wrong".
6. **Product-key check.** The build refuses to seal an image whose
   registry still holds the key (see "Checking the product key in the
   golden image": its steps 9 and 11 are what the build runs). Confirm it
   by hand on the sealed disk all the same: the check passes when step 9
   counts `0` and step 11 prints `absent`. No job may run on the image
   before it passes. If it fails, leave the loop disabled and record it as
   a finding.
7. **Enable the loop**, only after step 6 passed. Before you enable the
   loop, create the published state once with the command under "Pool
   switch". If you skip it, the host creates the variable on its first
   publish; until then a missing state sends every lane to Namespace.
   Then enable the loop:

   ```powershell
   Enable-ScheduledTask -TaskName slate-ci-orchestrator
   ```

   Within a minute the log shows
   `orchestrator start (pid …, user slate-ci-host, config C:\slate-ci\bin\config.json)`.
   `sweep:` lines follow only if there was something to remove. Find the
   newest log:

   ```powershell
   $log = Get-ChildItem C:\slate-ci\logs\orchestrator-*.log | Sort-Object Name | Select-Object -Last 1
   ```

   Read its end:

   ```powershell
   Get-Content $log.FullName -Tail 20
   ```

   Starting the task by hand is optional:

   ```powershell
   Start-ScheduledTask -TaskName slate-ci-orchestrator
   ```

8. **Flip the pool to `home`** (see "Pool switch").

## Host integration pass

Run this once, during the install and the first home runs (Task 14 of the
plan). Each item is something the code reviews could not settle away from
the real host. Tick it with what you saw. If an item fails, record what you
saw before changing anything.

### On the pull request, before the install

- [ ] `windows-runner-tests.yml` ran green on the pull request. It never ran
  while the branch was unpushed.

  ```powershell
  gh run list --workflow windows-runner-tests.yml --repo coryj627/slate --limit 1
  ```

- [ ] That run's pwsh on `ubuntu-latest` is 7.4 or later. The GitHub adapter
  and `orchestrator.ps1` require 7.4. The step "Ensure Pester 5" prints the
  Pester version, then the pwsh version.
  1. Get the run's id:

     ```powershell
     $run = gh run list --workflow windows-runner-tests.yml --repo coryj627/slate --limit 1 --json databaseId --jq '.[0].databaseId'
     ```

  2. Print the step's last two lines:

     ```powershell
     gh run view $run --repo coryj627/slate --log | Select-String 'Ensure Pester 5' | Select-Object -Last 2
     ```

### First install

- [ ] Step 3/9 of `setup-host.ps1` passes. It runs `secedit /export` and
  `secedit /configure` and stops on any non-zero exit. Whether secedit exits
  non-zero for a mere warning is unverified. If the step stops with
  `secedit /configure exited <n>`, read `C:\Windows\security\logs\scesrv.log`
  and record it.
- [ ] The three logon rights list the account once each, by SID, also after
  a second run of `setup-host.ps1`. This proves the UTF-16 export round trip
  and rules out a name-plus-SID double entry. See "Checking the logon
  rights" below.
- [ ] The loop task has both triggers, each repeating every minute with no
  end.
  1. Get the task:

     ```powershell
     $task = Get-ScheduledTask -TaskName slate-ci-orchestrator
     ```

  2. List the trigger types. Expect `MSFT_TaskBootTrigger` and
     `MSFT_TaskTimeTrigger`:

     ```powershell
     $task.Triggers.CimClass.CimClassName
     ```

  3. List their repetition. Expect `PT1M` twice, with empty durations:

     ```powershell
     $task.Triggers.Repetition | Select-Object Interval, Duration
     ```
- [ ] The loop task stays disabled until Install, step 7. Before that step,
  this prints `Disabled`, and no `orchestrator-*.log` exists yet:

  ```powershell
  (Get-ScheduledTask -TaskName slate-ci-orchestrator).State
  ```
- [ ] The loop task has no time limit, keeps one instance and runs on
  battery, which is what a UPS looks like to Windows:

  ```powershell
  (Get-ScheduledTask -TaskName slate-ci-orchestrator).Settings | Select-Object ExecutionTimeLimit, MultipleInstances, DisallowStartIfOnBatteries, StopIfGoingOnBatteries
  ```

  Expect `PT0S`, `IgnoreNew`, `False`, `False`. `PT0S` is Task Scheduler's
  unchecked "Stop the task if it runs longer than" box.
- [ ] The orchestrator and store-token tasks start the MSI pwsh, not the
  Store alias:

  ```powershell
  (Get-ScheduledTask -TaskName slate-ci-orchestrator, slate-ci-store-token).Actions.Execute
  ```

  Expect `C:\Program Files\PowerShell\7\pwsh.exe` twice. A path under
  `WindowsApps` cannot start as `slate-ci-host`. Install the MSI build, then
  register the tasks again with `-ResetAccount` (see "Re-running setup").
- [ ] `store-token.ps1` succeeds on its first run. If it says the conversion
  task did not consume the token, note that, then run it again.
- [ ] The tree's permissions:

  ```powershell
  icacls C:\slate-ci
  ```

  Expect Administrators and SYSTEM with `(OI)(CI)(F)`, `slate-ci-host` with
  `(OI)(CI)(M)`, and no inherited `(I)` entries.

  ```powershell
  icacls C:\slate-ci\bin
  ```

  Expect `slate-ci-host` with `(OI)(CI)(RX)`.

  ```powershell
  icacls C:\slate-ci\golden
  ```

  Expect `slate-ci-host` with `(OI)(CI)(RX,WDAC)`: read, plus the right to
  change permissions, which Hyper-V needs when it attaches a chain of
  differencing disks.

  ```powershell
  icacls C:\slate-ci\cache
  ```

  Expect `slate-ci-host` with `(OI)(CI)(M,WDAC)`.
- [ ] The firewall rule sits on the switch's interface:

  ```powershell
  Get-NetFirewallRule -DisplayName 'slate-ci: block VM subnet to host' | Get-NetFirewallInterfaceFilter
  ```

  Expect `vEthernet (slate-ci)`.

  ```powershell
  Get-NetFirewallRule -DisplayName 'slate-ci: block VM subnet to host' | Get-NetFirewallAddressFilter
  ```

  Expect the remote address `10.77.0.0/24`, which Windows may print with the
  mask `255.255.255.0`, and the local address `Any`: whichever of the
  host's addresses a packet from the VM subnet names on that interface (the
  gateway, the LAN or tailnet address, multicast, broadcast), it is dropped.
  Traffic the host forwards for the NAT never meets an inbound rule; the
  pilot's isolation step, which reaches `github.com:443` from a job VM,
  confirms the rule did not block it.
- [ ] The cache parents came up without a prompt. Step 7/9 printed
  `rust created`, `app created` and `model created`. No "format the disk"
  dialog took focus while it ran; if one did, Windows lettered the raw
  partition as soon as it existed (an open question from the reviews).

  ```powershell
  Get-ChildItem C:\slate-ci\cache | Select-Object Name, Length
  ```

  Expect `app`, `model` and `rust`, each as `.gen` and `.vhdx`.

### First golden build

- [ ] The EFI partition gets a letter after its retype. The build gets past
  `Applying 'Windows 11 Pro'` without `the EFI system partition got no drive letter`
  or `bcdboot exited`, and the VM boots.
- [ ] The hash-pinned downloads match their pins: phase 1 gets past the
  Python, Git and runner downloads, and phase 2 past rustup-init, without
  `sha256 mismatch`. If one stops there, compare its hash in
  `ci/windows-runner/golden/versions.json` (`pythonToolcacheSha256`,
  `gitSha256`, `pwshSha256`, `runnerSha256`, `rustupInitSha256`) with the one published where its
  `sources` entry points; for the runner, the SHA-256 that its GitHub
  release page lists for `actions-runner-win-x64-2.338.0.zip`. Do that
  before changing either.
- [ ] The two evergreen downloads pass their signature check: phase 1 gets
  past `vs_BuildTools.exe` and `dotnet-install.ps1` without `signature of`
  or `is signed by`. Microsoft serves a new build of each at every release,
  which no hash can pin, so the build requires a valid Authenticode
  signature from `Microsoft Corporation` instead. If the check stops the
  build, do not bypass it: a bad signature means the download was altered
  or redirected, or the image's root certificates or Internet access are
  broken.
- [ ] Every `icacls … /setowner runner /T /C` in phase 1 exits 0: the build
  does not stop on `icacls exited <n> for C:\actions-runner (owner)` or
  `icacls exited <n> for C:\dotnet (owner)`.
- [ ] `slmgr /cpky` left no readable product key. This is Install, step 6,
  before step 7 enables the loop. See "Checking the product key in the
  golden image" below.
- [ ] The `runner` password never expires; auto-logon would stop working
  once it did, 42 days after the build by default. Inside a job VM, or the
  build VM while phase 2 runs, open its console
  (`vmconnect.exe localhost <vm>`), start PowerShell and run:

  ```powershell
  net user runner
  ```

  Expect the line `Password expires` to end in `Never`.
- [ ] `runner` can create symbolic links. Inside a job VM, or the build VM
  once phase 2 runs, in the same console:

  ```powershell
  whoami /priv
  ```

  Expect `SeCreateSymbolicLinkPrivilege` in the list. Without it the three
  `livesync_windows_*symlink*` tests of `slate-core` fail with os error
  1314 while every other test passes.
- [ ] IPv6 is off inside a job VM, not only on the build VM's adapter. The
  image sets `Tcpip6\Parameters\DisabledComponents` to `0xFF`, which
  applies to the new adapter every job VM gets. In the same console:

  ```powershell
  Get-NetIPAddress -AddressFamily IPv6 -ErrorAction SilentlyContinue; Get-NetAdapterBinding -ComponentID ms_tcpip6
  ```

  Expect no IPv6 address at all, and `Enabled` reading `False` for the
  adapter.
- [ ] Nothing pops over the desktop of a job VM. The build turns OneDrive
  off by policy and drops its first-logon entries from the Default profile
  before the first boot, phase 2 takes its logon entries and Windows'
  sign-in pages out of the runner profile, and the build checks all of it
  in the sealed disk, rejecting an image where OneDrive installed itself
  or registered a task (its startup task fires ten minutes after logon, so
  look late). In a job VM's console, twelve
  minutes or more after logon:

  ```powershell
  Get-Process OneDrive -ErrorAction SilentlyContinue; Get-ScheduledTask -TaskName 'OneDrive*' -ErrorAction SilentlyContinue; Get-ItemProperty HKCU:\Software\Microsoft\Windows\CurrentVersion\Run, HKCU:\Software\Microsoft\Windows\CurrentVersion\RunOnce
  ```

  Expect no process, no task and no `OneDrive` or `OneDriveSetup` value.
  The shell gate's focus waits and its key-driven disk assertions name
  the foreground window when they fail (see "When things go wrong").
- [ ] The guest's clock did not jump after boot. The bootstrap waits run on
  a stopwatch, so a jump cannot cut them short, but a jump still shows
  where the guest came from. Read the bootstrap log of a finished job VM
  (see "Reading a job VM's logs") and compare its first and last
  timestamps with the VM's `StartedAt` in `C:\slate-ci\state\journal.json`,
  which is UTC: the log should start within a minute of it and run for
  about the job's length. A step of hours between two neighbouring lines
  means Hyper-V corrected the clock; record it.
- [ ] The loop starts by itself within a minute of step 7's
  `Enable-ScheduledTask`, with no `Start-ScheduledTask`. This proves the
  per-minute relaunch.

### First home runs

Start with a branch run on `home`, which never commits a cache. The items
about commits need the first main push.

- [ ] The loop's share of the API budget is small. Note the remaining
  budget before a busy hour (several jobs queued or running) and again
  after it:

  ```powershell
  gh api rate_limit --jq .resources.core
  ```

  The loop's listing calls are conditional: an unchanged answer is a `304`,
  which GitHub does not count, so an idle hour spends almost nothing and a
  busy hour a few hundred of the 5,000, never thousands. If
  `remaining` fell by more than a thousand with nothing else signed in as
  you busy, record the `used` figures and look for runs of other
  workflows in the jobs calls (the `RoutedWorkflows` filter in
  `config.json` should keep them out).

```powershell
gh workflow run windows.yml --ref <branch> --repo coryj627/slate
```

- [ ] The published state works. Within a minute of a job starting, the
  value changes (its `at` and its `running` list):

  ```powershell
  gh variable get WINDOWS_HOME_STATE --repo coryj627/slate
  ```

  The token's page on GitHub (Settings, Developer settings, Personal access
  tokens, Fine-grained tokens) lists Variables: Read and write.
- [ ] The vTPM works as `slate-ci-host`. The first
  `provisioned for job …` line appears, with no `vTPM could not be enabled`.
- [ ] A live job VM carries all 20 port ACL rules: 18 Deny rules, IPv6
  included, then 2 Allow rules.
  1. Pick a job VM:

     ```powershell
     $vm = Get-VM | Where-Object Name -like 'slate-win-*' | Select-Object -First 1
     ```

  2. List its rules:

     ```powershell
     Get-VMNetworkAdapterExtendedAcl -VMName $vm.Name | Select-Object Direction, Action, RemoteIPAddress, Weight
     ```

  Expect 18 Deny rows at weights 200 down to 183: inbound and outbound for
  each of `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `100.64.0.0/10`,
  `169.254.0.0/16`, `224.0.0.0/4` (multicast), `255.255.255.255/32`
  (broadcast), `0.0.0.0/8` and `::/0`. Then 2 Allow rows for `0.0.0.0/0`
  at weight 1.
- [ ] The KVP handoff works: `handed off (runner …, job …)` follows
  `provisioned`, with no `handoff failed: KVP: …`. This proves that
  `AddKvpItems` accepts the serialised client-only instances.
- [ ] `slate-ci-host` can create, merge and delete disks under
  `C:\slate-ci`. A `provisioned` line proves `New-VHD` in `vms`. The first
  main push's `commit <lane> generation 1` proves `Merge-VHD` and the
  `.gen` write in `cache`. An empty `vms` after the runs proves the
  deletes:

  ```powershell
  Get-ChildItem C:\slate-ci\vms
  ```

  A permission gap shows as `denied` or `0x80070005` in the log. Expect no
  match:

  ```powershell
  Select-String -Path C:\slate-ci\logs\orchestrator-*.log -Pattern 'denied|0x80070005'
  ```

- [ ] A trusted commit never collides with a running same-lane sibling. On
  the first main push, a model shard that finishes while the other runs
  logs `commit of model waits until sibling … is off`. Either way, one shard
  commits and the other is discarded with `generation moved`.
- [ ] `Get-VM` reports a vanished VM the way the loop expects, as
  `slate-ci-host`. See "Removing a job VM by hand" below.
- [ ] A job VM reaches the Internet and nothing else. First refresh the
  pilot's target list (see "Refreshing the pilot's isolation targets"
  below). Then dispatch the pilot on `home`:

  ```powershell
  gh workflow run windows-ci-pilot.yml --ref <branch> -f runner=home -f cache=false --repo coryj627/slate
  ```

  Expect the step `Isolation evidence (home pool only)` to print `false`
  for every private target and `true` for `github.com:443`, and the whole
  pilot green. Keep `isolation.json` from the artifact
  `windows-pilot-native-<run id>-<attempt>` with the pull request. While
  the pilot's build runs, also check the router's public address by hand
  (step 4 of "Refreshing the pilot's isolation targets").
- [ ] The cache volume mounts in the guest. Each cached lane's step "Cache
  mount state (pre-build)" lists `cargo`, `target` and `nuget` under the
  cache root. A cold parent shows `0.00 GiB` rows: the guest creates the
  folders first, so `cache volume empty (cold mount)` never prints on
  `home`.
- [ ] The guest has its interactive desktop: the shell gate (`flaui`)
  passes. It drives the app through UI Automation, which needs a desktop.
- [ ] A restart keeps the job in flight. While one job VM is running and
  its log shows `handed off`, restart the loop:

  ```powershell
  Stop-ScheduledTask -TaskName slate-ci-orchestrator
  ```

  Within a minute the log shows a new `orchestrator start`, then
  `sweep: kept running vm <vm> (job <id>, handed off)`, and no `startup:`
  or `journal:` error. The job finishes and settles as usual (`commit` or
  `discard`, never `shut down without running a job`). That proves the
  relaunch, the journal's read-back and the reconciliation.
  `C:\slate-ci\state` holds no `journal.json.corrupt-*`.
- [ ] No `slate-win-*` runner is left after that job settles. The sweep
  removes every runner no kept VM owns, online or offline; a running loop
  never touches a runner that may still take a job.

  ```powershell
  gh api repos/coryj627/slate/actions/runners --jq '.runners[] | {name, status}'
  ```

### Checking the logon rights

1. Print the account's SID:

   ```powershell
   (Get-LocalUser slate-ci-host).SID.Value
   ```

2. Export the rights:

   ```powershell
   secedit /export /cfg $env:TEMP\slate-rights.inf /areas USER_RIGHTS
   ```

3. Show the three rights:

   ```powershell
   Select-String -Path $env:TEMP\slate-rights.inf -Pattern 'SeDenyInteractiveLogonRight|SeDenyRemoteInteractiveLogonRight|SeBatchLogonRight'
   ```

   Expect the SID, with a leading `*`, once on each line, and no
   `slate-ci-host` by name.
4. Delete the export:

   ```powershell
   Remove-Item $env:TEMP\slate-rights.inf
   ```

### Checking the product key in the golden image

`slmgr /cpky` should leave no product key that a job could read. Check the
sealed disk before any job runs on it: at Install, step 6, and after every
rebuild (see "Refreshing the golden image"), while the loop task is
disabled.

1. Mount the disk read-only:

   ```powershell
   $disk = Mount-VHD -Path C:\slate-ci\golden\win11-runner.vhdx -ReadOnly -Passthru | Get-Disk
   ```

2. List its drive letters. The EFI partition may have one as well as the
   Windows volume:

   ```powershell
   $letters = ($disk | Get-Partition | Where-Object DriveLetter).DriveLetter
   ```

3. Keep the Windows volume, as the golden build does:

   ```powershell
   $letter = $letters | Where-Object { Test-Path "$($_):\Windows\System32" }
   ```

4. Copy the software hive out:

   ```powershell
   Copy-Item "${letter}:\Windows\System32\config\SOFTWARE" "$env:TEMP\golden-SOFTWARE"
   ```

5. Dismount the disk:

   ```powershell
   Dismount-VHD -Path C:\slate-ci\golden\win11-runner.vhdx
   ```

6. Load the copy:

   ```powershell
   reg load HKLM\slate-golden "$env:TEMP\golden-SOFTWARE"
   ```

7. Name the key you will read:

   ```powershell
   $cv = 'HKLM:\slate-golden\Microsoft\Windows NT\CurrentVersion'
   ```

8. Read `DigitalProductId`:

   ```powershell
   $id = (Get-ItemProperty $cv).DigitalProductId
   ```

9. Count the key bytes still set, bytes 52 to 66. Expect `0`; a missing
   value also counts `0`. Any other count means the key may still decode
   from the image.

   ```powershell
   @($id | Select-Object -Skip 52 -First 15 | Where-Object { $_ -ne 0 }).Count
   ```

10. Read the clear-text copy Windows may keep:

    ```powershell
    $k = (Get-ItemProperty "$cv\SoftwareProtectionPlatform" -ErrorAction SilentlyContinue).BackupProductKeyDefault
    ```

11. Print only its last five characters, or `absent`. Phase 1 deletes
    this value, so an image built from the current scripts prints `absent`.
    On an older image, Windows may keep its own default key for the
    edition here rather than yours: compare with the host's value, which
    is the same edition default, and with the last group of your key. Five
    characters that match your key mean the image holds it in clear text,
    readable by every user in the guest.

    ```powershell
    (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SoftwareProtectionPlatform').BackupProductKeyDefault.Substring(24)
    ```

    ```powershell
    if ($k) { $k.Substring($k.Length - 5) } else { 'absent' }
    ```

12. Release the hive:

    ```powershell
    [gc]::Collect()
    ```

13. Unload it:

    ```powershell
    reg unload HKLM\slate-golden
    ```

14. Delete the copy:

    ```powershell
    Remove-Item "$env:TEMP\golden-SOFTWARE*" -Force
    ```

If step 9 or 11 finds the key, leave the loop task disabled, keep the pool
on `namespace`, and record it as a finding before any job runs on the
image.

### Refreshing the pilot's isolation targets

The pilot's step `Isolation evidence (home pool only)` in
`.github/workflows/windows-ci-pilot.yml` probes a fixed `$forbidden` list.
Two entries, `172.24.160.1` and `172.19.80.1`, were the WSL and Default
Switch gateways on 2026-10-10. Windows picks new ones at every host boot.

1. List the host's virtual adapters:

   ```powershell
   Get-NetIPAddress -InterfaceAlias 'vEthernet*' -AddressFamily IPv4 | Select-Object InterfaceAlias, IPAddress
   ```

2. Compare the WSL and Default Switch addresses with the list.
   `10.77.0.1` is fixed by setup. The other entries are LAN and tailnet
   addresses; check them too if the LAN changed.
3. If they differ, put the current ones in the list on the branch you
   dispatch from, then commit and push. The dispatch runs that branch's
   copy of the workflow.
4. Check the home router's public (WAN) address too. It is a public
   address, so the port ACL lets a job reach it, and with it whatever the
   router serves there: remote administration, UPnP, or ports forwarded
   into the LAN. Never put it in the list: the repository, the run's log
   and `isolation.json` are all public. Check it by hand instead, while the
   pilot's build runs:
   1. Read the address from the router's status page.
   2. Open the console of the pilot build's job VM, the `slate-win-app-*`
      one that `Get-VM` lists:

      ```powershell
      vmconnect.exe localhost <vm>
      ```

   3. On the `runner` desktop, start PowerShell and probe each port the
      router serves or forwards, 443 and 80 at least. `<wan>` is the
      address:

      ```powershell
      Test-NetConnection <wan> -Port 443
      ```

   Expect `TcpTestSucceeded : False` for every port. A port that answers is
   reachable from every job: turn the service off on the router, or record
   it as an accepted risk in "Security model in one screen".

### Removing a job VM by hand

This checks how `Get-VM` reports a VM that no longer exists, as
`slate-ci-host`. Use a branch run, because its job fails.

1. Start a branch run on `home`:

   ```powershell
   gh workflow run windows.yml --ref <branch> --repo coryj627/slate
   ```

2. A few seconds later, get its id:

   ```powershell
   $run = gh run list --workflow windows.yml --repo coryj627/slate --limit 1 --json databaseId --jq '.[0].databaseId'
   ```

3. Repeat this until a job shows `in_progress` with a `slate-win-*`
   runner:

   ```powershell
   gh api "repos/coryj627/slate/actions/runs/$run/jobs" --jq '.jobs[] | {name, status, runner_name}'
   ```

   A `handed off` line alone is too early. A VM whose job is still queued
   is the wrong target: removing it only logs `discard (vm state Missing)`
   with a retry, and the job runs on a fresh VM after the 10-minute
   back-off.
4. Pick that job's VM. `<vm>` is its `runner_name`:

   ```powershell
   $vm = Get-VM -Name <vm>
   ```

5. Turn it off and remove it in one go, faster than the loop's 10-second
   tick:

   ```powershell
   $vm | Stop-VM -TurnOff -Force -Passthru | Remove-VM -Force
   ```

6. Expect `<vm>: discard (vm state Missing)` within a few seconds, and the
   job failing on GitHub as a lost runner. If
   `<vm>: Hyper-V did not answer; leaving the VM alone until the next tick`
   repeats instead, `Get-VM`'s not-found error has another shape for
   `slate-ci-host`, and the slot stays held until the loop restarts. Record
   it. If the log shows `shut down without running a job` or
   `discard (conclusion: …)` instead, a tick saw the VM off before it was
   gone: repeat on another job.

## Daily operation

- **Logs.** `C:\slate-ci\logs\orchestrator-<yyyy-MM-dd>.log`, one file per
  day. Each entry starts with a timestamp, `[info]`, `[warn]` or `[error]`,
  then the message. A message that spans lines continues on the following
  lines without a timestamp: GitHub's JSON reply to a failed call does.
  Find the newest file:

  ```powershell
  $log = Get-ChildItem C:\slate-ci\logs\orchestrator-*.log | Sort-Object Name | Select-Object -Last 1
  ```

  Read its end (add `-Wait` to follow it):

  ```powershell
  Get-Content $log.FullName -Tail 20
  ```

  VMs are named `slate-win-<lane>-<8 hex>`. A job's lines, in order:
  1. `<vm>: provisioned for job <id> (lane <lane>, slot <n>, fork generation <g>)`
  2. `<vm>: handed off (runner <id>, job <id>)`
  3. `<vm>: claimed (job <id> in progress)` as soon as a poll's listing
     shows the runner on a job, usually within a minute of the handoff.
     Before it, sometimes
     `<vm>: runs job <y>, not job <x> it was provisioned for, which stays queued for a VM of its own`:
     GitHub hands a runner whichever queued job of the lane matches first,
     and job `<x>` then gets the next free slot. `<vm>: claimed`, or
     `<vm>: claimed (job <status> on this runner)`, comes from the
     five-minute check instead, when the listing showed nothing.
  4. Sometimes
     `<vm>: job <id> is <status> on GitHub while its VM is off; waiting up to 6 ticks for it to complete`,
     usually with `in_progress`: GitHub's record lagged the guest's
     shutdown. The loop asks again every tick and decides within a minute
     on what GitHub reports then.
  5. One of `<vm>: commit <lane> generation <n> (trusted main)`,
     `<vm>: discard (<reason>)`, or, for the shell lane,
     `<vm>: done, lane shell has no cache (conclusion: <conclusion>)`.
  6. After a green job on a VM provisioned after the current golden's seal,
     the lane counts as accepted on that image; once rust, app, model and
     shell all do (a `home-only` run does all four):
     `golden: rust, app, model and shell are green on the current image`.
     The `slate-ci-golden-prune` task (SYSTEM, daily at 04:00) then deletes
     `win11-runner.prev.vhdx` and logs
     `golden: removed win11-runner.prev.vhdx (the current image is accepted on every lane)`,
     or `golden: the previous image stays (…)`, naming what is missing: the
     current image is not yet accepted on every lane, is unsealed, or an
     older VM exists. The task logs nothing when there is no previous image,
     and a failed prune logs `golden: prune failed: …`.

  Normal discard reasons: `event: pull_request` and `branch: <name>` (not a
  trusted main run), `conclusion: failure`, and
  `generation moved: fork <f>, parent <p>` (another job committed first).
  The others are under "When things go wrong".

  Other lines, outside a job's sequence:
  - `admission: skipped job` and `handoff: … forcing it off` name jobs this
    host refuses to serve. Both are under "When things go wrong".
  - `publish: …` is a failed state publish, logged at most once a minute.
  - `logs: removed <name>` is the once-a-day pruning of log files older than
    `LogRetentionDays` (30, in `config.json`). `logs: <name>: <error>` is a
    file it could not delete; it is tried again the next day.
- **Inside a VM.** `C:\actions-runner\bootstrap-system.log` is the SYSTEM
  task's log, `C:\actions-runner\bootstrap-runner.log` the `runner` task's,
  and `C:\actions-runner\_diag\` holds the runner's own logs. The guest
  scripts live in `C:\slate-guest`, which only the guest's Administrators
  and SYSTEM can write. A VM's disk is deleted when the loop settles it;
  "Reading a job VM's logs" under "When things go wrong" shows how to keep
  one.
- **State.** `C:\slate-ci\state\journal.json` holds:
  - the journal keys: `Vms` (the active VMs), `Retries`, `SeenJobs`,
    `LogsPrunedOn` (the day the logs were last pruned) and `GoldenAccepted`
    (which lanes went green on the current golden: its stamp, `Lanes` and
    `Complete`);
  - a `SeenJobs` entry's keys: `FirstSeenAt`, `Lane`, `RunId`,
    `RunCheckedAt`, `Foreign` and `Repo` (the run's head repository,
    stored when the run is read; empty when GitHub names none). `Foreign`
    and `Repo` stay null until the entry's run was read;
  - the published-state keys: `PublishedState` (the last published JSON),
    `PublishedAt` and `PublishFailedAt`. A `PublishedAt` or
    `PublishFailedAt` that does not parse counts as never and is rewritten
    on the next tick.

  The loop rewrites the journal every tick and reads it only at start,
  where the startup sweep keeps only the entries of handed-off VMs that are
  still running and drops the rest, so editing those entries changes
  nothing. The `slate-ci-golden-prune` task reads `GoldenAccepted` and never
  writes the journal. A journal that does not parse is moved to
  `journal.json.corrupt-<UTC time>` at start. `token.xml` is the encrypted
  PAT; a `token.txt` should never be there.
- **Cache generations.** They move only on trusted commits:

  ```powershell
  Get-ChildItem C:\slate-ci\cache\*.gen | ForEach-Object { '{0} {1}' -f $_.BaseName, (Get-Content -Raw $_.FullName) }
  ```

- **One loop only.** Do not run `orchestrator.ps1` by hand, `-Once`
  included. Only `slate-ci-host` can decrypt the token, so a manual run
  stops at start. A named mutex keeps a second loop from running: a second
  copy under the task's account logs
  `another orchestrator instance holds the mutex; exiting` and exits with
  code 3.
- **The task's last result.** While the loop runs,
  `Get-ScheduledTaskInfo -TaskName slate-ci-orchestrator` shows
  `LastTaskResult` `2147946720` (`0x800710E0`, "the operator or
  administrator has refused the request") and a `LastRunTime` within the
  last minute. That is the steady state, not a failure: the task's
  triggers fire every minute and each new launch is refused while an
  instance is running. `0` with an older `LastRunTime` means the loop is
  not being relaunched; `267009` (`0x41301`) means it is running and no
  trigger has fired since.
- **Stale runners.** Only the startup sweep removes `slate-win-*` runners
  from GitHub, and it removes every one that no kept VM owns, online or
  offline, so nothing is left behind after a restart. A running loop never
  touches a runner that may still take a job.
- **Activation.** Job VMs may report Windows as not activated. The key was
  cleared from the image with `slmgr /cpky` so no job can read it. This is
  cosmetic, by design.
- **Capacity.** Two slots, 4 vCPU and 12 GB each. Queued jobs are admitted
  oldest first. The router sends a lane here only when a slot is free or
  the queue costs the run no time; the host publishes its slot count, so
  nothing else needs to know it. Under `home-only` the spec expected app
  then shell in one slot, and rust then both model shards in the other.
- **Time caps.** `MaxMinutes` in `ci/windows-runner/config.json` is 70 for
  rust, 100 for app and model, and 30 for shell. Ten minutes past its lane's
  cap, counted from provisioning, the loop forces a VM off and discards it
  without a retry. No routed job's `timeout-minutes` exceeds its lane's cap,
  so GitHub times a job out first. If you raise a timeout, raise the cap
  with it (see "Re-running setup").
- **Disk.** The golden disk is about 40 GB. Each cache parent grows to at
  most 60 GB, and each live child to about 15 GB. Plan for 200 GB. Monthly,
  with the loop paused (see "Pausing and draining"), compact each parent.
  Mounting it read-only first lets `Optimize-VHD` reclaim deleted files'
  space, not only zeroed blocks. For the rust lane:
  1. Mount it read-only, without a drive letter:

     ```powershell
     Mount-VHD -Path C:\slate-ci\cache\rust.vhdx -ReadOnly -NoDriveLetter
     ```

  2. Compact it:

     ```powershell
     Optimize-VHD -Path C:\slate-ci\cache\rust.vhdx -Mode Full
     ```

  3. Dismount it:

     ```powershell
     Dismount-VHD -Path C:\slate-ci\cache\rust.vhdx
     ```

  Repeat the three steps with `app.vhdx` and `model.vhdx`.
- **Host reboot or Windows Update.** Jobs in flight fail on GitHub as lost
  runners: their VMs are turned off with the host. At boot the loop starts,
  and its sweep removes those VMs and their runners. Re-run the failed
  jobs (a partial re-run takes the vendor; use "Re-run all jobs" to route
  again, see "Pool switch"). Drain first when you can. Windows Update
  restarts this host on its own for servicing updates (it did on
  2026-10-11 at 03:29, with no one logged on); set active hours wide, or
  "notify me when a restart is required", under Settings, Windows Update,
  Advanced options, if an overnight restart would cost a run. Nobody needs
  to log on afterwards: the loop runs as a task. What it does need is the
  network: on that
  reboot the 10GbE adapter had no link for 47 minutes (`aqnic650` events
  14 and 15 in the System log), the loop logged
  `startup: No such host is known` every 30 s and exited, and it came
  back by itself within a minute of the link returning. If that repeats,
  look at the adapter's power and Energy-Efficient Ethernet settings
  (`Get-NetAdapterAdvancedProperty -Name 'Ethernet 4'`).
- **Power.** Keep host sleep and hibernation off on AC power. A sleeping
  host stalls every job.
- **Watching a live job VM.** `vmconnect.exe localhost <vm>` opens its
  console, the interactive `runner` desktop. The console is a picture of the
  guest's screen; NVDA's OCR (`NVDA+R`) reads it. Do not stop a VM by hand
  unless you mean its job to fail and its cache fork to be discarded. The
  loop knows only that it did not force a VM off itself: a VM you turn off
  just after a green main job looks to it like one that shut down cleanly.

### Host cleanup

Three mechanisms tidy up. The loop prunes its own logs once a day and
deletes those older than `LogRetentionDays` (30): see the "Logs" bullet
above. The elevated `slate-ci-golden-prune` task deletes the previous
golden image once the current one is accepted on every lane: see step 9 of
"Refreshing the golden image" and its row in "Security model in one
screen". The weekly branch report is a GitHub workflow, not part of this
host. It lists `claude/*` branches and deletes only when dispatched with
`delete`; branches younger than seven days are kept. It is described in
section 10 of
`docs/superpowers/specs/2026-10-11-windows-overflow-routing-design.md` and
ships in its own pull request.

## Pausing and draining

The loop task has two triggers: one at startup, and one that began a minute
after the task was registered. Both repeat every minute without end, and
the task keeps one instance at a time. A loop that exits is back within a
minute. So `Stop-ScheduledTask` on its own is a restart, not a pause. A
restart's sweep keeps the VMs whose handoff completed and that are still
running, and their jobs finish; a VM still waiting for its handoff is
discarded and its job retried. To keep the loop stopped, disable the task
first.

Drain before maintenance:

1. Flip the pool to `namespace` (see "Pool switch").
2. Let the home pool finish its work. Jobs queued for it before the flip
   still run there as slots free up. To move a waiting run to Namespace
   instead, cancel it and re-run it in full (see "Pool switch"). Recent
   runs and their state:

   ```powershell
   gh run list --repo coryj627/slate --limit 10
   ```

   Cancel, then re-run:

   ```powershell
   gh run cancel <run id> --repo coryj627/slate
   ```

   ```powershell
   gh run rerun <run id> --repo coryj627/slate
   ```

3. Wait until no job VM is left. This prints nothing when drained:

   ```powershell
   Get-VM | Where-Object Name -like 'slate-win-*'
   ```

4. Disable the task:

   ```powershell
   Disable-ScheduledTask -TaskName slate-ci-orchestrator
   ```

5. Stop the running loop:

   ```powershell
   Stop-ScheduledTask -TaskName slate-ci-orchestrator
   ```

6. Check that no loop process is left. This prints nothing:

   ```powershell
   Get-Process pwsh -IncludeUserName | Where-Object UserName -like '*\slate-ci-host'
   ```

Resume:

1. Enable the task. The loop starts within a minute, or at once with
   `Start-ScheduledTask -TaskName slate-ci-orchestrator`.

   ```powershell
   Enable-ScheduledTask -TaskName slate-ci-orchestrator
   ```

2. Check the log for a new `orchestrator start` (see "Daily operation").
3. Flip the pool back to `home`.

## Refreshing the golden image

Rebuild when one of these changes: the `rust-toolchain.toml` channel,
`apps/slate-windows/uniffi-bindgen-cs.version`, the runner version (GitHub
refuses runners outside its support window), or the .NET SDK band.

Rebuild too when the guest scripts, the provisioning scripts, or the
module's KVP functions (`Select-SlateKvpItems`, `Join-KvpChunks`, and their
host-side partner `Split-KvpChunks`) change. The image carries its own
copies of the guest scripts and the module, so reinstalling only the host
code leaves the guest out of step. If the KVP format moves on one side
only, every handoff ends in `no JIT config arrived within 300 s`.

Rebuild from a newer ISO when Windows itself needs servicing: the image
takes no Windows updates. If every VM starts logging
`shut down without running a job` after a GitHub runner release, suspect
the runner version first.

For a pin change, update `ci/windows-runner/golden/versions.json` on a
branch and merge it. The Pester suite checks its Rust and bindgen pins
against the repository's. The runner, Python, Git and rustup-init pins each
carry a SHA-256 (`runnerSha256`, `pythonToolcacheSha256`, `gitSha256`,
`pwshSha256`, `rustupInitSha256`): when a version moves, take its new hash from the page
its `sources` entry names, and update that entry too. Python is the
`win32-x64` archive of `actions/python-versions` for the pinned version
(`pythonToolcacheUrl`, whose release tag carries a build number): the
build setup-python would install, placed in the runner's tool cache by
the image build because setup-python's installer needs administrator
rights. Have the Windows 11 Pro key ready: the build asks for it again.

1. Drain and pause the loop (see "Pausing and draining"). The task stays
   disabled until step 7.
2. Pull the merged `main` into the checkout, and open the elevated window
   there.
3. Nothing to move: the build rotates the sealed disk to
   `win11-runner.prev.vhdx` itself (step 5), replacing an older rollback.
4. Refresh `bin`. A plain re-run keeps the orchestrator and store-token
   tasks as they are, so the loop stays disabled:

   ```powershell
   .\ci\windows-runner\install\setup-host.ps1
   ```

5. Build. `-Rotate` makes the build rename the sealed disk first, and it
   prints `Rotated the sealed image to …`. It refuses while any
   `slate-win-*` VM exists, so stop the loop and let them finish first
   (step 1):

   ```powershell
   .\ci\windows-runner\golden\build-golden.ps1 -IsoPath C:\Users\cory\Downloads\Windows11_Client_x64_en-us_26300_9457.iso -Rotate
   ```

6. Check the product key on the rebuilt disk: "Checking the product key in
   the golden image". If it finds the key, roll back (below).
7. Enable the task, only after step 6 passed. The new disk is sealed, so
   the loop starts within a minute. Check the log for `orchestrator start`.

   ```powershell
   Enable-ScheduledTask -TaskName slate-ci-orchestrator
   ```

8. Set the pool to `home-only`, then dispatch `windows.yml` on a branch:
   every lane, the shell gate included, runs on the new image.

   ```powershell
   gh variable set WINDOWS_RUNNER_MODE --body home-only --repo coryj627/slate
   ```

   ```powershell
   gh workflow run windows.yml --ref <branch> --repo coryj627/slate
   ```

9. Once that run is green, set the pool back to `home`:

   ```powershell
   gh variable set WINDOWS_RUNNER_MODE --body home --repo coryj627/slate
   ```

   The loop logs `golden: rust, app, model and shell are green on the current image`,
   and the prune task deletes the old disk at 04:00. To delete it sooner:

   ```powershell
   Start-ScheduledTask -TaskName slate-ci-golden-prune
   ```

Cache parents survive a golden rebuild untouched.

### Rolling back a refresh

If the build fails, the product-key check finds the key, or the branch run
on the new disk is red, put the old disk back.

1. If step 8 set the pool to `home-only`, set it to `namespace`. If
   step 7 enabled the loop, pause it again: steps 3 to 6 of the drain.
2. If the build left its VM, remove it:

   ```powershell
   Remove-VM -Name slate-golden-build -Force
   ```

3. Remove the new disk, if there is one. `-Force` also removes a sealed,
   read-only disk:

   ```powershell
   Remove-Item -LiteralPath C:\slate-ci\golden\win11-runner.vhdx -Force
   ```

4. Put the old disk back:

   ```powershell
   Rename-Item -LiteralPath C:\slate-ci\golden\win11-runner.prev.vhdx -NewName win11-runner.vhdx
   ```

   The prune task cannot remove a rollback you have just restored: the
   acceptance it reads is of the image it was recorded on, and a restored
   image has a different stamp.
5. Enable the task, only now that the old disk is back. The loop starts
   within a minute, on the old disk:

   ```powershell
   Enable-ScheduledTask -TaskName slate-ci-orchestrator
   ```

6. Set the pool back to `home`.

If the same change also moved host code that the old image cannot talk to,
such as the KVP functions, put the previous commit's host code back too
(see "Re-running setup").

## Re-running setup

A plain re-run of `setup-host.ps1` keeps `slate-ci-host`, its password and
the orchestrator and store-token tasks, and so the stored token, as long as
those two tasks exist. It refreshes `C:\slate-ci\bin` from the checkout and
repairs anything missing. The running loop keeps the code it started with,
so restart it after a refresh. `slate-ci-golden-prune` holds no password
and re-registers on every run.

If the orchestrator or store-token task is missing, a plain re-run takes
the reset path. It prints
`a scheduled task is missing: the password is reset so both can be registered`,
sets a new password, registers the orchestrator and store-token tasks
again, the loop task disabled, and deletes `token.xml`. Run
`store-token.ps1` again afterwards, then enable the loop task
(`Enable-ScheduledTask -TaskName slate-ci-orchestrator`).

To install new host code (the module, the adapters, `orchestrator.ps1`,
`config.json` or the install scripts), follow the steps below. If the
change touches what the guest runs, which is the guest or provisioning
scripts or the module's KVP functions, rebuild the golden image instead
(see "Refreshing the golden image"). That procedure refreshes `bin` too.

1. Merge it, pull `main` into the checkout, and open the elevated window
   there.
2. Drain and pause the loop.
3. Re-run setup. Expect
   `Host refreshed; slate-ci-host, its password and both tasks were kept.`

   ```powershell
   .\ci\windows-runner\install\setup-host.ps1
   ```

4. Compare the installed commit with the checkout's. The two must match:

   ```powershell
   Get-Content C:\slate-ci\bin\install-commit.txt
   ```

   ```powershell
   git rev-parse HEAD
   ```

5. If this is the first time the host publishes its state, create
   `WINDOWS_HOME_STATE` once first (the command is under "Pool switch").
   Until the new code runs, a missing or stale state sends every lane to
   Namespace. Then resume the loop and flip the pool back to `home`.

Changes to the task definitions (triggers, settings, the `pwsh` path) take
effect only with `-ResetAccount`. It removes the orchestrator and
store-token tasks, sets a new random password and registers those tasks
again, the loop task disabled. An administrator's password reset makes the
stored token unreadable, so it also deletes `token.xml`.

1. Drain and pause the loop. Pausing first matters: whether
   `Unregister-ScheduledTask` removes the task while the loop runs is
   unverified.
2. Reset:

   ```powershell
   .\ci\windows-runner\install\setup-host.ps1 -ResetAccount
   ```

3. Store the token again, the same PAT or a new one (then revoke the old):

   ```powershell
   C:\slate-ci\bin\install\store-token.ps1
   ```

4. Enable the new loop task, which setup registered disabled. Within a
   minute the log shows `orchestrator start`:

   ```powershell
   Enable-ScheduledTask -TaskName slate-ci-orchestrator
   ```

5. Flip the pool back to `home`.

Any re-run rebuilds a cache parent that is missing or has no `.gen` file,
empty, at generation 0. To reset one lane's cache, pause the loop, delete
its parent, re-run setup and resume. For the app lane:

```powershell
Remove-Item C:\slate-ci\cache\app.vhdx
```

## Rotating the token

The PAT expires a year after you create it. Once it has expired or been
revoked, nothing runs. Every tick then logs an `admission:` error, and the
lines after it, which carry no timestamp, read `"message": "Bad credentials"`
and `"status": "401"`. Rotate it before then.

1. Create the new PAT as in Install, step 1, with the same three
   permissions: without Variables, the host cannot publish its state and
   the router sends every lane to Namespace. If GitHub rejects the old
   name, give it a date, such as `slate-ci-host-cdesk-2027-10`.
2. Store it. The running loop keeps the old token in memory until it
   restarts:

   ```powershell
   C:\slate-ci\bin\install\store-token.ps1
   ```

3. Flip the pool to `namespace` and wait until no job VM is left (steps 1
   to 3 of the drain).
4. Restart the loop, which reads the token at start. It is back within a
   minute:

   ```powershell
   Stop-ScheduledTask -TaskName slate-ci-orchestrator
   ```

5. Check the log for a new `orchestrator start`, and flip the pool back to
   `home`.
6. Revoke the old PAT on GitHub.

## When things go wrong

- **`Get-WindowsOptionalFeature: … Class not registered` at `1/9 Hyper-V`,
  or `this is the Store build of PowerShell`.** The elevated window is the
  Microsoft Store build of PowerShell 7, whose packaged context cannot
  reach the Dism module. Close it and open the MSI build as in Install,
  step 2. Setup reads Hyper-V through `dism.exe` and refuses the Store
  build before it creates anything, so nothing was changed.
- **`New-VMSwitch: … Internal miniport create failed … Cannot create a file
  when that file already exists (0x800700B7)` at `5/9`.** The System log
  holds `Microsoft-Windows-Hyper-V-VmSwitch` event 76,
  `Create Non-Lightweight Miniport NIC … Object Name already exists`: a
  third-party NDIS protocol driver bound to every adapter kept Hyper-V
  from creating the switch's host adapter. On this host it was Realtek's
  three (see "Before you start"). Hyper-V rolls the switch back, so nothing
  is left behind: remove the drivers and run the setup again. If it fails
  the same way without them, reboot and run it again.
- **`<vm>: provisioning failed: Failed to add device 'Virtual Hard Disk' …
  Failed to set security info for 'C:\slate-ci\golden\win11-runner.vhdx':
  'Access is denied'`.** Hyper-V writes an access entry for the VM's
  virtual account onto every disk of the differencing chain, acting as
  `slate-ci-host`, which could not change permissions on the golden disk
  (or, for a cached lane, on the cache parent). Setup grants that right
  (`WDAC` on `golden` and `cache`): disable the loop task, run
  `setup-host.ps1` again from an updated checkout, then enable the task.
  The restart resets the job's retry count, and the job, still queued on
  GitHub, is admitted again.
- **A job sits in `Set up Python` for ever.** `actions/setup-python`
  installs Python on Windows with an all-users installer that needs
  administrator rights; as `runner` the job waits on a UAC prompt on the
  VM's desktop (visible with `vmconnect.exe localhost <vm>`) until the lane
  cap reclaims it. The image build puts the pinned Python into the runner's
  tool cache so the step finds it and installs nothing; a job that hangs
  there runs on an image built before that, so rebuild the golden disk
  ("Refreshing the golden image") and cancel the run.
- **A job fails its first step with `##[error]pwsh: command not found`.**
  The image has Windows PowerShell 5.1 only and no PowerShell 7, which
  every lane's `shell: pwsh` needs. An image built from the current scripts
  carries the pinned PowerShell 7 MSI (`pwshVersion`, `pwshSha256`); a job
  that fails this way runs on an older one. Rebuild the golden disk.
- **The Rust platform witnesses fail only the three
  `sync_detect::tests::livesync_windows_*symlink*` tests.** They create
  symbolic links, which needs `SeCreateSymbolicLinkPrivilege`; hosted
  runners have it as administrators, and the image grants it to `runner`
  in phase 1. A job that fails this way runs on an image built before
  that. Rebuild the golden disk.
- **The shell accessibility gate fails one keyboard-driven test on a home
  VM, and its message says `The foreground window is 'Microsoft OneDrive'`
  (or names another window that is not Slate).** A window popped over
  Slate on the job VM's desktop and took the keystrokes. Steps driven by
  UI Automation patterns do not care which window is in front; typed ones
  do, so which test fails depends on when the window came, and a re-run
  passes. The gate's focus waits and its key-driven disk assertions name
  the foreground window when they fail. An image built from the current
  scripts turns OneDrive off by policy and drops its first-logon entries
  from the Default profile before the first boot, takes its logon entries
  and Windows' sign-in pages out of the runner profile (phase 2), and
  verifies all of it in the sealed disk, rejecting an image where OneDrive
  installed itself or registered a task; a job that fails this way runs
  on an older one. Rebuild the golden disk. A different
  window's name in the message is a new one: turn it off beside OneDrive
  (the offline edit in `build-golden.ps1`, or phase 2 for a per-user
  switch), add it to the build's checks, and rebuild.
- **`<vm>: provisioning failed: 'MicrosoftWindows' matches none of the secure boot templates.`**
  Hyper-V's management service answered the firmware step's template
  lookup with an empty list. Seen on 2026-10-17 from 18:09 to 04:38 local
  the next day: every VM failed the same way, 72 in a row, with no loop
  restart, no host reboot, and nothing in the System log at the moment it
  began or the moment it ended; it cleared by itself. The breaker routes
  every lane away meanwhile and the jobs resume once it serves again. In
  the elevated window, `(Get-VMHost).SecureBootTemplates` should list
  `MicrosoftWindows`, `MicrosoftUEFICertificateAuthority` and
  `OpenSourceShieldedVM`. If it lists nothing: disable the loop first (its
  Hyper-V calls fail during the restart, and a VM mid-handoff would be
  discarded with a strike), restart the management service
  (`Restart-Service vmms`, which leaves running VMs alone), enable the
  loop, and look at the `Microsoft-Windows-Hyper-V-VMMS-Admin` log around
  the first failure.
- **`startup: No such host is known` every 30 s.** The host cannot
  resolve `api.github.com`: no network, most often after a reboot while
  the LAN adapter has no link yet. The loop exits and is relaunched each
  minute, and starts by itself once the network is back; nothing to do
  but find out why the network was down (see "Host reboot or Windows
  Update"). The same line once, between normal ticks, is a DNS blip.
- **`GitHub API: GET … answered on attempt 2 after a connection failure`
  now and then.** The GET lost its connection before GitHub's answer
  arrived (a reset mid-response, a refused or dropped connection, a name
  that did not resolve) and the adapter sent it again after 1 s, then 2 s,
  and got an answer. A few a day are normal and cost nothing.
  `admission: Unable to read data from the transport connection …`,
  `… The response ended prematurely`, `… No connection could be made …`
  or `… No such host is known` means the attempts failed in a row: three
  in all, fast ones, with at most one slow one (5 s or longer) among them
  whose connection failed while being set up (the connect, the TLS
  handshake or the name lookup); or a slow one after the connection was
  up, which is not repeated; or one that can never be repeated, a socket
  that timed out (a connect Windows gave up on after 21 s: `A connection
  attempt failed because the connected party did not properly respond …`)
  or an attempt at 25 s or beyond. A call takes about a minute at worst.
  That tick admits nothing and publishes nothing, and the next tick tries
  afresh. What this host has seen is a TLS handshake that stalls 19 s on
  a new connection and is then reset
  (`HttpRequestException(SecureConnectionError) > IOException >
  SocketException(ConnectionReset)`), while the next tick connects at
  once; such a failure is tried again once, and a run of them points at
  the host's network path rather than at GitHub. Many of either close together
  point at the host's network (see "Host reboot or Windows Update").
  Only GETs are retried: a repeated `generate-jitconfig` would register a
  second runner; the variable write retries after a minute by itself, and
  a failed runner delete is logged at warn and left to the next startup
  sweep. Every GET that fails without an HTTP status and is not retried
  leaves one `info` line first: `GitHub API: GET … gave up on attempt N
  of 3 after X s (the last attempt | a slow failure after the connection
  was up | a second slow failure | a socket that timed out | too slow
  to try again | not a connection failure): <exception types>`, where
  `X s` is that attempt's own time, not the call's. That line is the
  evidence for tuning the rule: the reason says which branch refused the
  retry, and an unfamiliar type says the classifier does not know the
  shape. A GET that GitHub answered with a status (a 403, Get-GhRunner's
  404) leaves no such line.
- **`token missing at …` every minute.** No token is stored: run
  `store-token.ps1`. After `-ResetAccount`, store the token before you
  enable the loop task.
- **`golden disk missing or not sealed (read-only) at …` every minute.** The
  loop task is enabled, but the golden disk does not exist yet, is being
  rebuilt, or its build failed before sealing it. Disable the task until a
  sealed disk has passed its product-key check (Install, step 6):

  ```powershell
  Disable-ScheduledTask -TaskName slate-ci-orchestrator
  ```
- **`startup: …` every minute.** The loop failed before its first tick, and
  the task retries every minute. `Key not valid for use in specified state`
  means `slate-ci-host` cannot decrypt the token: store it again. If
  GitHub's JSON reply follows the line, read it as in the next item.
- **GitHub's JSON reply after an `admission:`, `handoff:`, `publish:`,
  `startup:` or `<vm>:` line.**
  A failed API call logs the prefixed line, then GitHub's reply on the next
  lines, without timestamps. `"message": "Bad credentials"` with
  `"status": "401"` means the PAT expired or was revoked: rotate it.
  `"status": "403"` or `"status": "429"` with
  `"message": "API rate limit exceeded for user ID …"` means the hourly
  budget is spent; GitHub's response carried
  `x-ratelimit-remaining: 0`. The budget is your personal 5,000 requests an
  hour, shared with `gh` and anything else signed in as you, and it refills
  at the reset time. Any other `"status": "403"` means the PAT lacks a
  permission: compare it with Install, step 1. This finds them, with the
  four lines before each:

  ```powershell
  Select-String -Path C:\slate-ci\logs\orchestrator-*.log -Pattern '"status": "(40[13]|429)"' -Context 4,0
  ```
- **`GitHub API budget low: <n> requests left until the reset at <time> …`.**
  Fewer than 500 requests are left in the current hour of the budget the
  PAT shares with `gh`. The loop warns once per hourly window. Something
  else signed in as you is busy, or the loop is polling more runs than
  usual. Check the remaining budget (this call does not count against it):

  ```powershell
  gh api rate_limit --jq .resources.core
  ```
- **`<vm>: provisioning failed: … vTPM could not be enabled …`.** The VM
  could not get its virtual TPM as `slate-ci-host`, and the job is retried.
  The error names two causes. Neither has been seen yet, so record what
  fixes it here.
  - The host guardian is not in local mode. `Get-HgsClientConfiguration`
    should show `Mode : Local`; `Set-HgsClientConfiguration -EnableLocalMode`
    sets it.
  - `slate-ci-host` cannot use the local key protector. The local guardian,
    `UntrustedGuardian`, keeps its two certificates in the computer store
    `Shielded VM Local Certificates`. They exist once an elevated session
    has made a local key protector, as the golden build does. Granting
    `slate-ci-host` read access to both private keys is the likely fix.
- **`<vm>: guest reports: …`, then
  `<vm>: shut down without running a job (guest bootstrap failure?) guest error: …`.**
  The guest's SYSTEM bootstrap failed. It publishes its error as the guest
  KVP item `slate.error`, which the loop logs while the VM runs, and shuts
  the VM down 30 seconds later. The job is retried after 10 minutes, at most
  three attempts in all. Typical errors: `no JIT config arrived within 300 s`,
  `no network adapter came up within 60 s`,
  `cache volume slate-cache did not mount`.
- **`shut down without running a job` with an empty guest error.** The
  runner side failed, or the runner exited without taking the job. After a
  runner-side failure the VM stays up for 600 seconds before it shuts down,
  so you can look inside: see "Reading a job VM's logs" below, or watch it
  with `vmconnect`.
- **`discard (no heartbeat)`.** The guest did not boot within 3 minutes.
  Suspect the golden image. The job is retried.
- **`handoff failed: …`, then `discard (handoff failed)`.** The runner
  registration or the KVP write failed. The job is retried.
- **`discard (unclaimed and the admitted job is no longer queued)`.** No job
  claimed the runner within 5 minutes, and the job went elsewhere or was
  cancelled.
- **`discard (unclaimed while the job stays queued)`.** The runner did not
  take its job within 15 minutes. The job is retried.
- **`expired after <n> min + grace; forcing off`.** The VM still ran 10
  minutes past its lane's cap, counted from provisioning. It is discarded
  and its job is not retried.
- **A job stays queued and the log says nothing about it.** Check the mode,
  then that the loop runs (recent log lines), then that the job's workflow
  is listed in `RoutedWorkflows` in `ci/windows-runner/config.json`: the
  loop reads no other workflow's jobs. Then the retry cap: after three
  failed attempts the loop stops admitting that job without a log line.
  Cancel the run and re-run it; to the loop its jobs are then new. A restart
  clears the retry table when no VM is kept, and after a breaker trip in
  any case.
- **`journal: …` every tick.** The journal cannot be written. A leftover
  `C:\slate-ci\state\journal.json.tmp` that cannot be replaced, for example
  one made read-only, blocks every write. After a restart the same fault
  stops the start itself, so it shows as `startup: …` every minute. Delete
  the file:

  ```powershell
  Remove-Item -LiteralPath C:\slate-ci\state\journal.json.tmp -Force
  ```
- **`Hyper-V did not answer` every tick.** Check that the Virtual Machine
  Management service runs: `Get-Service vmms`. The slot stays held until
  Hyper-V answers.
- **`teardown failed, will retry`.** Hyper-V would not delete a VM. Its slot
  stays held, and the loop tries again every tick.
- **`admission: skipped job <id> of <repo> (not the trusted repository)`.**
  The job's run comes from another repository, such as a fork pull request
  whose copy of the workflow named a home label. The host never provisions
  a VM for it and logs this once per job. Nothing to do. The job stays
  queued on GitHub and runs nowhere here.
- **`admission: job <id>: run <run> could not be read: <error>`.** The host
  reads each queued job's run once, to learn which repository it comes
  from, and does not admit that job before it has. It retries once a
  minute, logs only the first failure, and the job waits meanwhile.
  GitHub's reply follows the line: read it as in "GitHub's JSON reply"
  above.
- **`handoff: <vm> runs job <id> of <repo> (<why>); forcing it off`.**
  GitHub handed the VM's runner to a job this host does not serve.
  `<why>` is `not the trusted repository` for a job of another repository,
  and `no lane label` for a job of this repository without exactly one
  lane label. The VM is forced off within a tick and its cache fork is
  discarded, with
  `<vm>: discard (runner taken by job <id> of <repo> (<why>))` after it. No
  retry strike is given. The job the VM was provisioned for stays queued
  and is admitted again in the same tick. When the run cannot be read and
  the job has no lane label, `<repo>` reads `an unknown repository`.
- **`handoff: <vm>: run <run> of job <id> could not be read: <error>`.**
  The host cannot yet tell whether the job that took the runner is a
  fork's. It logs this at most once a minute per job, and the VM keeps
  running that job until the run can be read.
- **`<vm>: shut down without a job the host can resolve, although its runner was seen busy; no retry`.**
  The runner was seen on a job, but the host cannot say which one, for
  example a job it does not track. The image works, so no retry strike is
  given against the job the VM was provisioned for, and the cache fork is
  discarded. A job that started and finished between two ticks is never
  seen on the runner: it ends as `shut down without running a job` above,
  and the job the VM was provisioned for takes one retry strike.
- **`health: not serving for at least 15 min: 3 failures in a row, last '<reason>' …`.**
  Three VMs in a row ended in a discard of the host's making
  (`provisioning failed`, `handoff failed`, `no heartbeat`,
  `shut down without running a job`, a VM unclaimed while its job stayed
  queued, a VM that died) with no runner taking its job between them.
  The host publishes `serving: false`, so in `home` mode the router sends
  every lane to Namespace (`home-only` ignores the state); the jobs
  already queued home keep their retries, and a failure among them
  restarts the cool-down without another line. A job that reaches its
  retry cap while the host fails is not stranded: at the half-open, and
  when a runner takes its job after a trip, the retry strikes are
  forgotten if they span more than one lane (`health: retry strikes of N
  jobs forgotten (lanes …) …; they are admitted again`) and the jobs the
  host gave up on run first, as the probes; strikes that all fall on one
  lane are kept (`… kept: all on lane X, which looks like that lane's
  fault`), that job stays capped as before (cancel its run and re-run it),
  and the host serves the other lanes. On a host that keeps failing the
  struck jobs retry in cycles of about 35 minutes, three failed
  provisionings per job per cycle. The cause is in the lines
  just before it (Hyper-V, the golden or cache disks, the NAT, the PAT's
  runner permission). Fix it and restart the loop (Disable, Stop,
  Enable): the restart forgets the failures and logs
  `health: N failures in a row forgotten at start`, and after a trip
  forgets the retry strikes too, whatever their lanes. Left alone,
  fifteen minutes after its last failure the host logs `health: serving
  again after 15 min without a failure; the next failure trips it at
  once`; if the forgiven jobs fail in that same tick the state never says
  serving and the log shows the pair of lines every cycle, otherwise the
  state looks healthy again and the next run's home lanes probe as well.
  `health: a runner took its job; N failures in a row forgotten` means it
  recovered by itself. `BreakerFailures` (3) and `BreakerCooldownSeconds`
  (900) in `config.json`.
- **`publish: <error>`.** The host could not write `WINDOWS_HOME_STATE`.
  The loop logs it at most once a minute and keeps serving. Once the
  published state is three minutes old, the router sends every lane to
  Namespace. A `"status": "403"` after the line usually means the PAT lacks
  Variables: Read and write (Install, step 1). A tick whose admission
  threw publishes nothing, so a host that cannot list or admit jobs routes
  away within three minutes too.
- **The golden build fails.** See "The golden build fails" below.

### The golden build fails

The script prints the error and leaves the VM `slate-golden-build`. Inside
the image, the phase 1 log is `C:\provision\provision.log` and its error
`C:\provision\provision-error.txt`. The phase 2 log is
`C:\Users\runner\provision-runner-user.log` and its error
`C:\Users\runner\provision-runner-user-error.txt`. To read them from the
host:

1. Turn the VM off, if it still runs:

   ```powershell
   Stop-VM -Name slate-golden-build -TurnOff -Force
   ```

2. Mount the disk read-only:

   ```powershell
   $disk = Mount-VHD -Path C:\slate-ci\golden\win11-runner.vhdx -ReadOnly -Passthru | Get-Disk
   ```

3. List its drive letters. The EFI partition may have one as well as the
   Windows volume:

   ```powershell
   $letters = ($disk | Get-Partition | Where-Object DriveLetter).DriveLetter
   ```

4. Keep the Windows volume, as the golden build does:

   ```powershell
   $letter = $letters | Where-Object { Test-Path "$($_):\Windows\System32" }
   ```

5. Read the end of the phase 1 log:

   ```powershell
   Get-Content "${letter}:\provision\provision.log" -Tail 40
   ```

6. Read the end of the phase 2 log, if phase 1 finished:

   ```powershell
   Get-Content "${letter}:\Users\runner\provision-runner-user.log" -Tail 40
   ```

7. Dismount:

   ```powershell
   Dismount-VHD -Path C:\slate-ci\golden\win11-runner.vhdx
   ```

To try again, remove the VM if there is one, and the unsealed disk. Then
repeat Install, step 5:

```powershell
Remove-VM -Name slate-golden-build -Force
```

```powershell
Remove-Item -LiteralPath C:\slate-ci\golden\win11-runner.vhdx -Force
```

A `-Rotate` refuses an unsealed leftover; remove `win11-runner.vhdx` by
hand, then rebuild. In a refresh the sealed disk is already at
`win11-runner.prev.vhdx`, and the rebuild leaves it there. A leftover
`win11-runner.prev.rotating.vhdx` from an interrupted rotation makes the
next `-Rotate` refuse; if `win11-runner.prev.vhdx` is missing, rename the
leftover back to it by hand (it is the older rollback) and run the build
again. If `win11-runner.prev.vhdx` is present, the rotation had already
moved the live image there; delete the leftover instead.

### Reading a job VM's logs

A job VM's disk is untrusted: its job could have written anything to it,
including files made to attack whatever opens them on the host. Mount it
only when its job never started (`shut down without running a job`) or
ran your own branch. For any other VM, above all one that ran a fork pull
request, watch it live with `vmconnect` instead. The procedure mounts the
disk read-only and without drive letters, on a folder, so Explorer,
AutoPlay and the search indexer never open it.

The loop deletes a job VM's disk when it settles the VM. To keep one and
read its logs with a screen reader, pause the loop first. If the VM is
running a job, that job fails. Any other job VM is removed, uncommitted,
when the loop resumes.

1. Pause the loop: steps 4 to 6 of the drain.
2. Turn the VM off:

   ```powershell
   Stop-VM -Name <vm> -TurnOff -Force
   ```

3. Mount its system disk read-only, without drive letters:

   ```powershell
   $disk = Mount-VHD -Path C:\slate-ci\vms\<vm>\os.vhdx -ReadOnly -NoDriveLetter -Passthru | Get-Disk
   ```

4. Pick its Windows volume, the one basic-data partition. The EFI and
   reserved partitions have other types:

   ```powershell
   $windows = $disk | Get-Partition | Where-Object GptType -eq '{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}'
   ```

5. Make an empty folder to mount it on:

   ```powershell
   $mount = New-Item -ItemType Directory -Path "$env:TEMP\slate-vm-logs"
   ```

6. Mount the volume on the folder:

   ```powershell
   $windows | Add-PartitionAccessPath -AccessPath $mount.FullName
   ```

7. Read the runner task's log:

   ```powershell
   Get-Content "$($mount.FullName)\actions-runner\bootstrap-runner.log"
   ```

8. Read the SYSTEM task's log:

   ```powershell
   Get-Content "$($mount.FullName)\actions-runner\bootstrap-system.log"
   ```

   The runner's own logs are in `actions-runner\_diag` on the same volume.
9. Take the volume off the folder:

   ```powershell
   $windows | Remove-PartitionAccessPath -AccessPath $mount.FullName
   ```

10. Dismount:

    ```powershell
    Dismount-VHD -Path C:\slate-ci\vms\<vm>\os.vhdx
    ```

11. Delete the folder, now empty again:

    ```powershell
    Remove-Item $mount.FullName
    ```

12. Resume the loop. Its startup sweep removes the VM.

## Security model in one screen

| Boundary | Control |
|---|---|
| Who can run code here | Only approved workflows. Every fork pull request waits for an owner click, every time. Only `home` and `home-only` route here; a fork pull request is sent to the vendor by the router in every mode, refused at admission and evicted at handoff by this host, and still waits for an owner click first. A fork or Dependabot run that cannot read the variable goes to Namespace and `windows-latest`. Non-fork branches are the owner's own commits (and Renovate's). |
| What a job can touch | A VM that exists for that job only, as the standard user `runner`, with no secret valid outside the VM. Its JIT runner config is single-use. |
| Where a job can connect | The Internet, through the host's NAT. Hyper-V port ACLs drop everything to and from 10/8, 172.16/12, 192.168/16, 100.64/10 (Tailscale), 169.254/16, multicast 224/4, broadcast 255.255.255.255, 0/8 and all IPv6; the image also turns IPv6 off. The host firewall drops inbound traffic from the VM subnet to any address of the host on that interface. |
| What survives a job | Only a cache merge, and only after the host verifies from GitHub's API: a green push, schedule or dispatch on `main` of this repository, a VM the loop did not force off, and an unchanged parent generation. |
| What the host account can do | `slate-ci-host` is a Hyper-V Administrator, not an Administrator, but Microsoft treats membership in Hyper-V Administrators as equivalent to administrator rights on the host. Its limits stop mistakes and interactive use; they do not contain a compromise of the account or of the code it runs. It cannot log on interactively or remotely. Inside `C:\slate-ci` it writes `state`, `cache`, `vms` and `logs`; `bin` and `golden` are read-only for it, though on `golden` and `cache` it can change permissions, which Hyper-V needs when it attaches a disk chain. Like any standard account, it can still create folders elsewhere on `C:\`. It holds the one PAT: Actions read, Administration read and write, Variables read and write, this repository only. Variables write reaches every repository variable, not only `WINDOWS_HOME_STATE`. |
| `slate-ci-golden-prune` task | SYSTEM, daily at 04:00; deletes one fixed path only when the module's predicate holds for the current, sealed image; the orchestrator's account gains no right on `golden`. |
| Who can change what runs | Only an elevated administrator writes `C:\slate-ci\bin`, the golden disk and the tasks. In the guest, only Administrators and SYSTEM can write the bootstrap scripts in `C:\slate-guest`. |
| Residual risk, accepted | A job reaches the Internet with a read-only `GITHUB_TOKEN`, as on GitHub-hosted runners. The home router's public (WAN) address is a public destination, so the port ACL passes it: whatever the router serves there (remote administration, UPnP, ports forwarded into the LAN) is reachable from a job. Keep those off, and check by hand (step 4 of "Refreshing the pilot's isolation targets"). |

Recommended, not done by the runner work: branch protection on `main`
requiring "build + test (windows x64)".

## Observed timings

Task 14 of the plan fills the home columns from the acceptance runs. The
spec estimated app 12–15 min, shell 6–8 min, and a main push at about
25 min.

| Job | Namespace / hosted (2026-10-09) | home (first green, 2026-10-11) | home (warm) |
|---|---|---|---|
| rust tests | 2 min | 10 min 34 s | |
| app build + test | 28 min | 27 min 18 s | |
| app model shard (each) | 6 min | 38 min 16 s and 38 min 53 s | |
| shell accessibility gate | 17 min | 15 min 48 s | |
| main push, wall | 45 min | 1 h 25 min (a branch dispatch, see below) | |

### windows.yml, first green run on the home pool

Run 38125616320 (2026-10-11): a `workflow_dispatch` of the branch
`claude/home-pilot-smoke` with the pool already flipped to `home`, so
every cache parent was still empty and the run's forks were discarded.
Rust tests 10 min 34 s, app build + test 27 min 18 s, the model shards
38 min 16 s and 38 min 53 s, the shell gate 15 min 48 s; 1 h 25 min wall
through two slots. Every handoff took 12–14 s, and each slot turned over
6–14 s after the host saw its job end (GitHub reports the completion
first, which adds up to a minute).

The model shards took 8× their hosted time because every workspace
created a Windows spell checker and the model harness builds a workspace
per case: releasing the checker costs about 180 ms on Windows 11, and the
Windows Server images Namespace uses offer no checker at all, so the cost
never showed there. PR #1350 binds it on first use. With that fix (run
38129938362, a branch run) a shard took 17 min 9 s with the other slot
idle and 26 min 21 s beside the app job's tests: the test step 11 min and
19 min against 31, cleanup 27 ms a case against 159 (hosted: 4.7). The
rest of the gap is the VM itself: `sessionSetup` and `arrangement` run
3–8× their hosted time, worst beside a busy neighbour, and that part is
not yet attributed. One candidate is
cheap to test in the next golden build: the runner user's `%TEMP%`, where
the harness copies a vault per case and the session writes its index,
lies outside the guest's Defender exclusions (`provision-guest.ps1`
excludes the runner, .NET, cargo and rustup trees, and the job volume).

### Pilot comparison (windows-ci-pilot.yml)

The pilot's `home` candidate first went green on 2026-10-11 (run
38106842944: a branch dispatch with `cache=false`, every cache parent
still empty). The `hosted` column is run 38080504707 of 2026-10-10 on
`windows-latest`; its later jobs were cancelled by a newer push, so only
the build job compares.

| Job | hosted (`windows-latest`) | home, cold cache |
|---|---|---|
| Build, platform witnesses and app | 53 min 37 s | 41 min 31 s, of which app tests 19 min 48 s |
| Complete hosted shell gate | not run | 15 min 54 s |
| Complete model, each shard | not run | 28 min 47 s |
| Handoff to the job's first step | | 14 s |
| Whole run, wall | | 1 h 44 min: four VM jobs through two slots |

The whole-run figure is bounded by the two slots, not by the VMs: the
second model shard waited for a slot.
