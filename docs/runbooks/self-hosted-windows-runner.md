# Self-hosted Windows runner (CDESK)

Design: `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`.
Code: `ci/windows-runner/`. The installed copy is `C:\slate-ci\bin`, and the
commit it came from is in `C:\slate-ci\bin\install-commit.txt`.
Tests: `.github/workflows/windows-runner-tests.yml` (check
`module, adapters, scripts`). Cache policy: `docs/runbooks/ci-cache-policy.md`.

Every Windows job runs in a throwaway Hyper-V VM on the owner's desktop.
The orchestrator loop is the scheduled task `slate-ci-orchestrator`. It runs
as the account `slate-ci-host`, a member of Hyper-V Administrators only.
Every 10 seconds it polls the repository for queued jobs labelled
`slate-win-*`. For each job it forks a VM from the read-only golden disk and
a copy-on-write child of the lane's cache disk. It hands in a single-use JIT
runner config over KVP. After the guest shuts itself down, the loop merges
the cache child only when GitHub's record shows a green push, schedule or
dispatch on `main` of `coryj627/slate`. Labels carry no trust.

Run every command that touches Hyper-V, the scheduled tasks or anything
under `C:\slate-ci` in an elevated PowerShell 7 window. `C:\slate-ci` does
not inherit permissions from `C:\`, so a normal prompt cannot even read its
logs. `gh` commands work in any prompt.

## Pool switch (fallback and return)

The repository variable `WINDOWS_RUNNER_MODE` routes `windows.yml` and
`nightly.yml`.

- `home` sends every Windows job to this host, with the labels
  `slate-win-rust`, `slate-win-app`, `slate-win-model` and
  `slate-win-shell`. The nightly stress job uses `slate-win-app`.
- `namespace` is the 2026-07-31 arrangement, verbatim. The rust, app and
  model lanes run on the Namespace profile `namespace-profile-winx64-fast`
  (`-fast-pr` off `main`), with the cache tags `slate-windows-rust`,
  `slate-windows-app` and `slate-windows-model`. The shell gate and the
  nightly stress job run on GitHub-hosted `windows-latest`.
- Any value other than `namespace` means `home`, a typo or an unset
  variable included. The comparison ignores case, so `Namespace` also
  selects Namespace.

The pilot, `windows-ci-pilot.yml`, ignores the variable. Its `runner` input
picks the pool, and `home` is one of the choices.

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
to 24 hours. Cancel them and re-run them after the flip. The flip never
touches caches on either side.

Fork and Dependabot pull requests may not receive repository variables.
Their Windows jobs then route to `home`, whatever the mode says. Every fork
pull request waits for your approval first (Install, step 7). If the mode
is `namespace` because the host is down, cancel such a run and re-run it
once the host is back.

## Install (one time, in order)

Before you start:

- PowerShell 7.4 or later, installed from the MSI. Setup registers the
  tasks with whatever `pwsh` resolves to, and the Microsoft Store build
  resolves to a `WindowsApps` alias that `slate-ci-host` cannot start. This
  should print `C:\Program Files\PowerShell\7\pwsh.exe`:

  ```powershell
  (Get-Command pwsh).Source
  ```

- About 200 GB free on `C:`.
- No other Windows NAT. Windows supports one per host, so setup stops if
  `Get-NetNat` lists anything but `slate-ci`.
- The ISO, here `C:\Users\cory\Downloads\Win11_25H2_English_x64_v2.iso`,
  and the Windows 11 Pro product key in the password manager.
- A clean checkout of the commit to install, normally an up-to-date `main`.
  Setup copies the working tree but records only `HEAD`.

1. **PAT.** On GitHub, create a fine-grained personal access token:
   - name `slate-ci-host-cdesk`;
   - resource owner `coryj627`;
   - repository access: only `coryj627/slate`;
   - permissions: Actions read-only, and Administration read and write
     (GitHub adds Metadata read-only by itself);
   - expiry: one year.

   Keep it in the password manager. You paste it in step 4, and again after
   any `-ResetAccount`.
2. **Elevated window.** From any pwsh, open an elevated one and approve the
   UAC prompt:

   ```powershell
   Start-Process pwsh -Verb RunAs
   ```

   In the new window, go to the checkout:

   ```powershell
   Set-Location C:\dev\slate
   ```

   Use this window for steps 3 to 6.
3. **Host setup.**

   ```powershell
   .\ci\windows-runner\install\setup-host.ps1
   ```

   It prints `1/9` to `9/9`, then `Host setup complete. Next:`. It creates:
   - the account `slate-ci-host`: Hyper-V Administrators only, denied local
     and remote interactive logon, allowed to log on as a batch job;
   - `C:\slate-ci` with `bin`, `golden`, `cache`, `vms`, `state` and `logs`.
     The tree does not inherit from `C:\`. Administrators and SYSTEM have
     full control, `slate-ci-host` has Modify, and `bin` and `golden` are
     read-only for it;
   - the internal switch `slate-ci`, host address `10.77.0.1`, NAT
     `10.77.0.0/24`;
   - the host firewall rule `slate-ci: block VM subnet to host`;
   - the cache parents `rust.vhdx`, `app.vhdx` and `model.vhdx` in
     `C:\slate-ci\cache`: 60 GB dynamic, NTFS, label `slate-cache`, each
     with a `.gen` file at 0;
   - `C:\slate-ci\bin`, a mirror of `ci/windows-runner` without the tests;
   - two scheduled tasks: `slate-ci-orchestrator`, the loop, and
     `slate-ci-store-token`, which step 4 runs.

   If Hyper-V was off, the script enables it, asks for a reboot and exits.
   Reboot, open the elevated window again and re-run it.

   From now on the loop task launches every minute. Until step 4 it logs
   this and exits, which is expected:
   `token missing at C:\slate-ci\state\token.xml (run install/store-token.ps1)`.
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

   From now on, until step 5 seals the disk, the loop logs this every
   minute, which is also expected:
   `golden disk missing or not sealed (read-only) at C:\slate-ci\golden\win11-runner.vhdx (run golden/build-golden.ps1); exiting until it is sealed`.
5. **Golden image** (40 to 60 minutes; the script gives up after 150):

   ```powershell
   .\ci\windows-runner\golden\build-golden.ps1 -IsoPath C:\Users\cory\Downloads\Win11_25H2_English_x64_v2.iso
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
6. **The loop starts by itself.** Within a minute of the seal, the log shows
   `orchestrator start (pid …, user slate-ci-host, config C:\slate-ci\bin\config.json)`.
   `sweep:` lines follow only if there was something to remove. Read the
   newest log:

   ```powershell
   Get-Content (Get-ChildItem C:\slate-ci\logs\orchestrator-*.log | Sort-Object Name | Select-Object -Last 1).FullName -Tail 20
   ```

   Starting the task by hand is optional:

   ```powershell
   Start-ScheduledTask -TaskName slate-ci-orchestrator
   ```

7. **Fork pull requests need approval.** Require it for all external
   contributors:

   ```powershell
   gh api -X PUT repos/coryj627/slate/actions/permissions/fork-pr-contributor-approval -f approval_policy=all_external_contributors
   ```

   Check it. Expect `{"approval_policy":"all_external_contributors"}`.

   ```powershell
   gh api repos/coryj627/slate/actions/permissions/fork-pr-contributor-approval
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
  Pester version, then the pwsh version:

  ```powershell
  gh run view (gh run list --workflow windows-runner-tests.yml --repo coryj627/slate --limit 1 --json databaseId --jq '.[0].databaseId') --repo coryj627/slate --log | Select-String 'Ensure Pester 5' | Select-Object -Last 2
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
  end:

  ```powershell
  (Get-ScheduledTask -TaskName slate-ci-orchestrator).Triggers | Select-Object @{ n = 'Type'; e = { $_.CimClass.CimClassName } }, @{ n = 'Every'; e = { $_.Repetition.Interval } }, @{ n = 'For'; e = { $_.Repetition.Duration } }
  ```

  Expect `MSFT_TaskBootTrigger` and `MSFT_TaskTimeTrigger`, each `PT1M`,
  with an empty duration.
- [ ] The loop task has no time limit, keeps one instance and runs on
  battery, which is what a UPS looks like to Windows:

  ```powershell
  (Get-ScheduledTask -TaskName slate-ci-orchestrator).Settings | Select-Object ExecutionTimeLimit, MultipleInstances, DisallowStartIfOnBatteries, StopIfGoingOnBatteries
  ```

  Expect `PT0S`, `IgnoreNew`, `False`, `False`. `PT0S` is Task Scheduler's
  unchecked "Stop the task if it runs longer than" box.
- [ ] Both tasks start the MSI pwsh, not the Store alias:

  ```powershell
  Get-ScheduledTask -TaskName slate-ci-orchestrator, slate-ci-store-token | ForEach-Object { $_.Actions.Execute }
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

  ```powershell
  icacls C:\slate-ci\golden
  ```

  Expect `slate-ci-host` with `(OI)(CI)(RX)` on both.
- [ ] The firewall rule sits on the switch's interface:

  ```powershell
  Get-NetFirewallRule -DisplayName 'slate-ci: block VM subnet to host' | Get-NetFirewallInterfaceFilter
  ```

  Expect `vEthernet (slate-ci)`.

  ```powershell
  Get-NetFirewallRule -DisplayName 'slate-ci: block VM subnet to host' | Get-NetFirewallAddressFilter
  ```

  Expect the remote address `10.77.0.0/24`, which Windows may print with the
  mask `255.255.255.0`, and the local address `10.77.0.1`.
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
- [ ] The runner download matches its pin: phase 1 gets past it without
  `sha256 mismatch`. If it stops there, compare `runnerSha256` in
  `ci/windows-runner/golden/versions.json` with the SHA-256 that the
  runner's GitHub release page lists for
  `actions-runner-win-x64-2.338.0.zip`, before changing either.
- [ ] Every `icacls … /setowner runner /T /C` in phase 1 exits 0: the build
  does not stop on `icacls exited <n> for C:\actions-runner (owner)` or
  `icacls exited <n> for C:\dotnet (owner)`.
- [ ] `slmgr /cpky` left no readable product key. Read `DigitalProductId` in
  the sealed image before step 8 flips the pool to `home`. See "Checking
  the product key in the golden image" below.
- [ ] The loop starts by itself within a minute of the seal, with no
  `Start-ScheduledTask`. This proves the per-minute relaunch.

### First home runs

Start with a branch run on `home`, which never commits a cache. The items
about commits need the first main push.

```powershell
gh workflow run windows.yml --ref <branch> --repo coryj627/slate
```

- [ ] The vTPM works as `slate-ci-host`. The first
  `provisioned for job …` line appears, with no `vTPM could not be enabled`.
- [ ] A live job VM carries all 14 port ACL rules, IPv6 included:

  ```powershell
  Get-VMNetworkAdapterExtendedAcl -VMName (Get-VM | Where-Object Name -like 'slate-win-*' | Select-Object -First 1).Name | Select-Object Direction, Action, RemoteIPAddress, Weight
  ```

  Expect 12 Deny rows at weights 200 down to 189: inbound and outbound for
  each of `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `100.64.0.0/10`,
  `169.254.0.0/16` and `::/0`. Then 2 Allow rows for `0.0.0.0/0` at
  weight 1.
- [ ] The KVP handoff works: `handed off (runner …, job …)` follows
  `provisioned`, with no `handoff failed: KVP: …`. This proves that
  `AddKvpItems` accepts the serialised client-only instances.
- [ ] `slate-ci-host` can create, merge and delete disks under
  `C:\slate-ci`. A `provisioned` line proves `New-VHD` in `vms`. The first
  main push's `commit <lane> generation 1` proves `Merge-VHD` and the
  `.gen` write in `cache`. An empty `vms` after the runs proves the
  deletes. Any `Access is denied` in those lines is a permission gap.

  ```powershell
  Get-ChildItem C:\slate-ci\vms
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
  `windows-pilot-native-<run id>-<attempt>` with the pull request.
- [ ] The cache volume mounts in the guest. Each cached lane's step "Cache
  mount state (pre-build)" lists `cargo`, `target` and `nuget` under the
  cache root. A cold parent shows `0.00 GiB` rows: the guest creates the
  folders first, so `cache volume empty (cold mount)` never prints on
  `home`.
- [ ] The guest has its interactive desktop: the shell gate (`flaui`)
  passes. It drives the app through UI Automation, which needs a desktop.
- [ ] A restart is clean. With no job VM running, restart the loop (the
  restart's sweep would turn any job VM off):

  ```powershell
  Stop-ScheduledTask -TaskName slate-ci-orchestrator
  ```

  Within a minute the log shows a new `orchestrator start` and no
  `startup:` or `journal:` error. That proves the relaunch and the
  journal's read-back. `C:\slate-ci\state` holds no `journal.json.corrupt-*`.
- [ ] No offline `slate-win-*` runner is left after that restart. Only the
  startup sweep removes them, never a running loop.

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
sealed disk before any job runs on it.

1. Mount the disk read-only:

   ```powershell
   $disk = Mount-VHD -Path C:\slate-ci\golden\win11-runner.vhdx -ReadOnly -Passthru | Get-Disk
   ```

2. Find its Windows volume:

   ```powershell
   $letter = ($disk | Get-Partition | Where-Object { $_.DriveLetter -and (Test-Path "$($_.DriveLetter):\Windows\System32") }).DriveLetter
   ```

3. Copy the software hive out:

   ```powershell
   Copy-Item "${letter}:\Windows\System32\config\SOFTWARE" "$env:TEMP\golden-SOFTWARE"
   ```

4. Dismount the disk:

   ```powershell
   Dismount-VHD -Path C:\slate-ci\golden\win11-runner.vhdx
   ```

5. Load the copy:

   ```powershell
   reg load HKLM\slate-golden "$env:TEMP\golden-SOFTWARE"
   ```

6. Count the key bytes left in `DigitalProductId`:

   ```powershell
   $id = (Get-ItemProperty 'HKLM:\slate-golden\Microsoft\Windows NT\CurrentVersion').DigitalProductId; if ($null -eq $id) { 'absent' } else { 'non-zero key bytes: ' + @($id[52..66] | Where-Object { $_ -ne 0 }).Count }
   ```

   Expect `absent` or `non-zero key bytes: 0`. Any other count means the
   key may still decode from the image.
7. Look for a clear-text copy. This prints only the last five characters:

   ```powershell
   $k = (Get-ItemProperty 'HKLM:\slate-golden\Microsoft\Windows NT\CurrentVersion\SoftwareProtectionPlatform' -ErrorAction SilentlyContinue).BackupProductKeyDefault; if ($k) { $k.Substring($k.Length - 5) } else { 'absent' }
   ```

   If they match the last group of your key, the image holds the key in
   clear text.
8. Release the hive:

   ```powershell
   [gc]::Collect()
   ```

   ```powershell
   reg unload HKLM\slate-golden
   ```

9. Delete the copy:

   ```powershell
   Remove-Item "$env:TEMP\golden-SOFTWARE*" -Force
   ```

If step 6 or 7 finds the key, keep the pool on `namespace` and record it as
a finding before any job runs on the image.

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

### Removing a job VM by hand

This checks how `Get-VM` reports a VM that no longer exists, as
`slate-ci-host`. Use a branch run, because its job fails.

1. Start a branch run on `home` and wait for a `handed off` line.
2. Pick the VM:

   ```powershell
   $vm = Get-VM | Where-Object Name -like 'slate-win-*' | Select-Object -First 1
   ```

3. Turn it off and remove it in one go, faster than the loop's 10-second
   tick:

   ```powershell
   $vm | Stop-VM -TurnOff -Force -Passthru | Remove-VM -Force
   ```

4. Expect `<vm>: discard (vm state Missing)` within a few seconds, and the
   job failing on GitHub as a lost runner. If
   `<vm>: Hyper-V did not answer; leaving the VM alone until the next tick`
   repeats instead, `Get-VM`'s not-found error has another shape for
   `slate-ci-host`, and the slot stays held until the loop restarts. Record
   it. If the log shows `shut down without running a job` or
   `discard (conclusion: …)` instead, a tick saw the VM off before it was
   gone: repeat on another job.

## Daily operation

- **Logs.** `C:\slate-ci\logs\orchestrator-<yyyy-MM-dd>.log`, one file per
  day. Each line is a timestamp, `[info]`, `[warn]` or `[error]`, then the
  message. Read the newest file (add `-Wait` to follow it):

  ```powershell
  Get-Content (Get-ChildItem C:\slate-ci\logs\orchestrator-*.log | Sort-Object Name | Select-Object -Last 1).FullName -Tail 20
  ```

  VMs are named `slate-win-<lane>-<8 hex>`. A job's lines, in order:
  1. `<vm>: provisioned for job <id> (lane <lane>, slot <n>, fork generation <g>)`
  2. `<vm>: handed off (runner <id>, job <id>)`
  3. `<vm>: claimed`, or `<vm>: claimed (job <status> on this runner)`,
     only for a job still running five minutes after the handoff.
  4. One of `<vm>: commit <lane> generation <n> (trusted main)`,
     `<vm>: discard (<reason>)`, or, for the shell lane,
     `<vm>: done, lane shell has no cache (conclusion: <conclusion>)`.

  Normal discard reasons: `event: pull_request` and `branch: <name>` (not a
  trusted main run), `conclusion: failure`, and
  `generation moved: fork <f>, parent <p>` (another job committed first).
  The others are under "When things go wrong".
- **Inside a VM.** `C:\actions-runner\bootstrap-system.log` is the SYSTEM
  task's log, `C:\actions-runner\bootstrap-runner.log` the `runner` task's,
  and `C:\actions-runner\_diag\` holds the runner's own logs. The guest
  scripts live in `C:\slate-guest`, which only the guest's Administrators
  and SYSTEM can write. A VM's disk is deleted when the loop settles it;
  "Reading a job VM's logs" under "When things go wrong" shows how to keep
  one.
- **State.** `C:\slate-ci\state\journal.json` lists the active VMs, retries
  and seen jobs. The loop rewrites it every tick and reads it only at start,
  where the startup sweep empties it, so editing it changes nothing. A
  journal that does not parse is moved to `journal.json.corrupt-<UTC time>`
  at start. `token.xml` is the encrypted PAT; a `token.txt` should never be
  there.
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
- **Stale runners.** Only the startup sweep removes offline `slate-win-*`
  runners from GitHub. After a crash or a reboot, a few may stay listed as
  offline until the next start. An offline runner takes no job.
- **Activation.** Job VMs may report Windows as not activated. The key was
  cleared from the image with `slmgr /cpky` so no job can read it. This is
  cosmetic, by design.
- **Capacity.** Two slots, 4 vCPU and 12 GB each. Queued jobs are admitted
  oldest first. A main push has five Windows jobs, which run two at a time.
  The spec expected app then shell in one slot, and rust then both model
  shards in the other.
- **Time caps.** `MaxMinutes` in `ci/windows-runner/config.json` is 70 for
  rust, 100 for app and model, and 30 for shell. Ten minutes past its lane's
  cap, counted from provisioning, the loop forces a VM off and discards it
  without a retry. No routed job's `timeout-minutes` exceeds its lane's cap,
  so GitHub times a job out first. If you raise a timeout, raise the cap
  with it (see "Re-running setup").
- **Disk.** The golden disk is about 40 GB. Each cache parent grows to at
  most 60 GB, and each live child to about 15 GB. Plan for 200 GB. Monthly,
  with the loop paused (see "Pausing and draining"), compact the parents:

  ```powershell
  foreach ($lane in 'rust', 'app', 'model') { $p = "C:\slate-ci\cache\$lane.vhdx"; Mount-VHD -Path $p -ReadOnly -NoDriveLetter; Optimize-VHD -Path $p -Mode Full; Dismount-VHD -Path $p }
  ```

  Mounting read-only first lets `Optimize-VHD` reclaim deleted files' space,
  not only zeroed blocks.
- **Host reboot or Windows Update.** Jobs in flight fail on GitHub as lost
  runners: their VMs are turned off with the host. At boot the loop starts,
  and its sweep removes those VMs and the runners already offline. Re-run
  the failed jobs. Drain first when you can.
- **Power.** Keep host sleep and hibernation off on AC power. A sleeping
  host stalls every job.
- **Watching a live job VM.** `vmconnect.exe localhost <vm>` opens its
  console, the interactive `runner` desktop. The console is a picture of the
  guest's screen; NVDA's OCR (`NVDA+R`) reads it. Do not stop a VM by hand
  unless you mean its job to fail and its cache fork to be discarded.

## Pausing and draining

The loop task has two triggers: one at startup, and one that began a minute
after the task was registered. Both repeat every minute without end, and
the task keeps one instance at a time. A loop that exits is back within a
minute. So `Stop-ScheduledTask` on its own is a restart, not a pause. Every
restart's sweep turns off and deletes every job VM, and their jobs fail as
lost runners. To keep the loop stopped, disable the task first.

Drain before maintenance:

1. Flip the pool to `namespace` (see "Pool switch").
2. Let the home pool finish its work. Jobs queued for it before the flip
   still run there as slots free up. To move a waiting run to Namespace
   instead, cancel it and re-run it. Recent runs and their state:

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
refuses runners outside its support window), or the .NET SDK band. Rebuild
from a newer ISO when Windows itself needs servicing: the image takes no
Windows updates. If every VM starts logging
`shut down without running a job` after a GitHub runner release, suspect
the runner version first.

Update `ci/windows-runner/golden/versions.json` on a branch and merge it.
The Pester suite checks its Rust and bindgen pins against the repository's.
Have the Windows 11 Pro key ready: the build asks for it again.

1. Drain and pause the loop (see "Pausing and draining").
2. Pull the merged `main` into the checkout, and open the elevated window
   there.
3. Move the old disk aside. It is read-only, but a rename still works:

   ```powershell
   Rename-Item C:\slate-ci\golden\win11-runner.vhdx win11-runner.prev.vhdx
   ```

4. Refresh `bin`:

   ```powershell
   .\ci\windows-runner\install\setup-host.ps1
   ```

5. Build:

   ```powershell
   .\ci\windows-runner\golden\build-golden.ps1 -IsoPath C:\Users\cory\Downloads\Win11_25H2_English_x64_v2.iso
   ```

6. Enable the task. The new disk is sealed, so the loop starts within a
   minute. Check the log for `orchestrator start`.

   ```powershell
   Enable-ScheduledTask -TaskName slate-ci-orchestrator
   ```

7. Flip the pool back to `home`, then dispatch `windows.yml` on a branch:

   ```powershell
   gh workflow run windows.yml --ref <branch> --repo coryj627/slate
   ```

8. Once that run is green, delete the old disk:

   ```powershell
   Remove-Item C:\slate-ci\golden\win11-runner.prev.vhdx -Force
   ```

Cache parents survive a golden rebuild untouched.

## Re-running setup

A plain re-run of `setup-host.ps1` keeps `slate-ci-host`, its password and
both tasks, and so the stored token. It refreshes `C:\slate-ci\bin` from the
checkout and repairs anything missing. The running loop keeps the code it
started with, so restart it after a refresh.

To install new host code, anything under `ci/windows-runner` that is not
the golden image:

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

5. Resume the loop and flip the pool back to `home`.

Changes to the task definitions (triggers, settings, the `pwsh` path) take
effect only with `-ResetAccount`. It removes both tasks, sets a new random
password and registers both tasks again, enabled. An administrator's
password reset makes the stored token unreadable, so it also deletes
`token.xml`.

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

4. The new task is already enabled. Within a minute the log shows
   `orchestrator start`. Until step 3, it shows `token missing` instead.
5. Flip the pool back to `home`.

Any re-run rebuilds a cache parent that is missing or has no `.gen` file,
empty, at generation 0. To reset one lane's cache, pause the loop, delete
its parent, re-run setup and resume. For the app lane:

```powershell
Remove-Item C:\slate-ci\cache\app.vhdx
```

## Rotating the token

The PAT expires a year after you create it. An expired or revoked PAT
shows as `401 (Unauthorized)` in every tick's error, and nothing runs.
Rotate it before then.

1. Create the new PAT exactly as in Install, step 1.
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

- **`token missing at …` every minute.** No token is stored. Run
  `store-token.ps1`. This is expected after `-ResetAccount` until you do.
- **`golden disk missing or not sealed (read-only) at …` every minute.** The
  golden disk does not exist yet, is being rebuilt, or its build failed
  before sealing it. The loop starts within a minute of a seal.
- **`startup: …` every minute.** The loop failed before its first tick, and
  the task retries every minute. `Key not valid for use in specified state`
  means `slate-ci-host` cannot decrypt the token: store it again. A `401` or
  `403` is the PAT, as in the next item.
- **`401` or `403` in `admission:` lines or `<vm>:` lines.** A `401` means
  the PAT expired or was revoked: rotate it. A `403` means it lacks a
  permission: compare it with Install, step 1.
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
  then that the loop runs (recent log lines). Then the retry cap: after three
  failed attempts the loop stops admitting that job without a log line.
  Cancel the run and re-run it; to the loop its jobs are then new. A restart
  also clears the retry table.
- **`journal: …` every tick.** The journal cannot be written. A leftover
  `C:\slate-ci\state\journal.json.tmp` that cannot be replaced, for example
  one made read-only, blocks every write. Delete it.
- **`Hyper-V did not answer` every tick.** Check that the Virtual Machine
  Management service runs: `Get-Service vmms`. The slot stays held until
  Hyper-V answers.
- **`teardown failed, will retry`.** Hyper-V would not delete a VM. Its slot
  stays held, and the loop tries again every tick.
- **The golden build fails.** The script prints the error and leaves the VM
  `slate-golden-build`. Inside the image, the phase 1 log is
  `C:\provision\provision.log` and its error `C:\provision\provision-error.txt`.
  The phase 2 log is `C:\Users\runner\provision-runner-user.log` and its
  error `C:\Users\runner\provision-runner-user-error.txt`. To read them from
  the host, first turn the VM off if it still runs:

  ```powershell
  Stop-VM -Name slate-golden-build -TurnOff -Force
  ```

  Mount the disk read-only. This prints its drive letter:

  ```powershell
  Mount-VHD -Path C:\slate-ci\golden\win11-runner.vhdx -ReadOnly -Passthru | Get-Disk | Get-Partition | Where-Object DriveLetter | Select-Object DriveLetter
  ```

  Read the logs, for example on `E:`:

  ```powershell
  Get-Content E:\provision\provision.log -Tail 40
  ```

  Then dismount:

  ```powershell
  Dismount-VHD -Path C:\slate-ci\golden\win11-runner.vhdx
  ```

  To try again, remove the VM if there is one, and the unsealed disk. Then
  repeat Install, step 5:

  ```powershell
  Remove-VM -Name slate-golden-build -Force
  ```

  ```powershell
  Remove-Item C:\slate-ci\golden\win11-runner.vhdx
  ```

### Reading a job VM's logs

The loop deletes a job VM's disk when it settles the VM. To keep one and
read its logs with a screen reader, pause the loop first. If the VM is
running a job, that job fails. Any other job VM is removed, uncommitted,
when the loop resumes.

1. Pause the loop: steps 4 to 6 of the drain.
2. Turn the VM off:

   ```powershell
   Stop-VM -Name <vm> -TurnOff -Force
   ```

3. Mount its system disk read-only. This prints its drive letter:

   ```powershell
   Mount-VHD -Path C:\slate-ci\vms\<vm>\os.vhdx -ReadOnly -Passthru | Get-Disk | Get-Partition | Where-Object DriveLetter | Select-Object DriveLetter
   ```

4. Read the logs, for example on `E:`:

   ```powershell
   Get-Content E:\actions-runner\bootstrap-runner.log
   ```

   `bootstrap-system.log` is beside it, and the runner's own logs are in
   `E:\actions-runner\_diag`.
5. Dismount:

   ```powershell
   Dismount-VHD -Path C:\slate-ci\vms\<vm>\os.vhdx
   ```

6. Resume the loop. Its startup sweep removes the VM.

## Security model in one screen

| Boundary | Control |
|---|---|
| Who can run code here | Only approved workflows. Every fork pull request waits for an owner click, every time; fork and Dependabot runs may route to `home` whatever the mode. Non-fork branches are the owner's own commits (and Renovate's). |
| What a job can touch | A VM that exists for that job only, as the standard user `runner`, with no secret valid outside the VM. Its JIT runner config is single-use. |
| Where a job can connect | The Internet, through the host's NAT. Hyper-V port ACLs drop everything to and from 10/8, 172.16/12, 192.168/16, 100.64/10 (Tailscale), 169.254/16 and all IPv6. The host firewall drops inbound traffic from the VM subnet to the host. |
| What survives a job | Only a cache merge, and only after the host verifies from GitHub's API: a green push, schedule or dispatch on `main` of this repository, a guest that shut itself down, and an unchanged parent generation. |
| What the host account can do | `slate-ci-host` is a Hyper-V Administrator, not an Administrator. It cannot log on interactively or remotely. Beyond a standard account's own profile, it can modify only `C:\slate-ci`, where it only reads `bin` and `golden`. It holds the one PAT: Actions read, Administration read and write, this repository only. |
| Who can change what runs | Only an elevated administrator writes `C:\slate-ci\bin`, the golden disk and the tasks. In the guest, only Administrators and SYSTEM can write the bootstrap scripts in `C:\slate-guest`. |

Recommended, not done by the runner work: branch protection on `main`
requiring "build + test (windows x64)".

## Observed timings

Task 14 of the plan fills the home columns from the acceptance runs. The
spec estimated app 12–15 min, shell 6–8 min, and a main push at about
25 min.

| Job | Namespace / hosted (2026-10-09) | home (first green) | home (warm) |
|---|---|---|---|
| rust tests | 2 min | | |
| app build + test | 28 min | | |
| app model shard (each) | 6 min | | |
| shell accessibility gate | 17 min | | |
| main push, wall | 45 min | | |
