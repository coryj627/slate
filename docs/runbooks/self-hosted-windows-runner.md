# Self-hosted Windows runner (CDESK)

Design: `docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md`.
Code: `ci/windows-runner/`. The installed copy is `C:\slate-ci\bin`, and the
commit it came from is in `C:\slate-ci\bin\install-commit.txt`.
Tests: `.github/workflows/windows-runner-tests.yml` (check
`module, adapters, scripts`). Cache policy: `docs/runbooks/ci-cache-policy.md`.

Every Windows job runs in a throwaway Hyper-V VM on the owner's desktop.
The orchestrator loop is the scheduled task `slate-ci-orchestrator`. It runs
as the account `slate-ci-host`, a member of Hyper-V Administrators only,
which Microsoft treats as equivalent to an administrator (see "Security
model in one screen"). Every 10 seconds it polls the routed workflows,
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
`nightly.yml`.

- `home` sends every Windows job to this host, with the labels
  `slate-win-rust`, `slate-win-app`, `slate-win-model` and
  `slate-win-shell`. The nightly stress job uses `slate-win-app`.
- `namespace` is the 2026-07-31 arrangement, verbatim. The rust, app and
  model lanes run on the Namespace profile `namespace-profile-winx64-fast`
  (`-fast-pr` off `main`), with the cache tags `slate-windows-rust`,
  `slate-windows-app` and `slate-windows-model`. The shell gate and the
  nightly stress job run on GitHub-hosted `windows-latest`.
- Only `home` selects this host. Any other value, a typo or an unset
  variable included, means the `namespace` arrangement. The comparison
  ignores case, so `Home` also selects this host.

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
to 24 hours. Cancel them and re-run them after the flip. The flip never
touches caches on either side.

Fork and Dependabot pull requests may not receive repository variables. A
run that cannot read the variable routes to Namespace and `windows-latest`,
whatever the mode says, so it never waits on this host, even while it is
down. Every fork pull request still waits for your approval first (see
"Before you start" under Install).

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
     full control, `slate-ci-host` has Modify, and `bin` and `golden` are
     read-only for it;
   - the internal switch `slate-ci`, host address `10.77.0.1`, NAT
     `10.77.0.0/24` (the host address takes the prefix's length, and the
     script refuses a gateway outside the prefix before it creates
     anything);
   - the host firewall rule `slate-ci: block VM subnet to host`;
   - the cache parents `rust.vhdx`, `app.vhdx` and `model.vhdx` in
     `C:\slate-ci\cache`: 60 GB dynamic, NTFS, label `slate-cache`, each
     with a `.gen` file at 0;
   - `C:\slate-ci\bin`, a mirror of `ci/windows-runner` without the tests;
   - two scheduled tasks: `slate-ci-orchestrator`, the loop, registered
     disabled, and `slate-ci-store-token`, which step 4 runs.

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
6. **Product-key check.** Follow "Checking the product key in the golden
   image" below on the sealed disk. It passes when its step 9 counts `0` and
   its step 11 prints `absent`, or five characters other than your key's
   last group. No job may run on the image before it passes. If it fails,
   leave the loop disabled and record it as a finding.
7. **Enable the loop**, only after step 6 passed:

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
- [ ] Both tasks start the MSI pwsh, not the Store alias:

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
  mask `255.255.255.0`, and three local addresses: the gateway `10.77.0.1`,
  multicast `224.0.0.0/4`, which Windows may print as
  `224.0.0.0/240.0.0.0`, and broadcast `255.255.255.255`. Traffic the host
  forwards for the NAT is addressed elsewhere, so the rule leaves it alone.
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
  `ci/windows-runner/golden/versions.json` (`pythonSha256`, `gitSha256`,
  `runnerSha256`, `rustupInitSha256`) with the one published where its
  `sources` entry points; for the runner, the SHA-256 that its GitHub
  release page lists for `actions-runner-win-x64-2.338.0.zip`. Do that
  before changing either.
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
- [ ] IPv6 is off inside a job VM, not only on the build VM's adapter. The
  image sets `Tcpip6\Parameters\DisabledComponents` to `0xFF`, which
  applies to the new adapter every job VM gets. In the same console:

  ```powershell
  Get-NetIPAddress -AddressFamily IPv6 -ErrorAction SilentlyContinue; Get-NetAdapterBinding -ComponentID ms_tcpip6
  ```

  Expect no IPv6 address at all, and `Enabled` reading `False` for the
  adapter.
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

  The loop costs two listing calls a tick plus one jobs call per queued or
  in-progress run of `windows.yml`, `nightly.yml` and the pilot, so a busy
  hour should spend a few hundred of the 5,000, never thousands. If
  `remaining` fell by more than a thousand with nothing else signed in as
  you busy, record the `used` figures and look for runs of other
  workflows in the jobs calls (the `RoutedWorkflows` filter in
  `config.json` should keep them out).

```powershell
gh workflow run windows.yml --ref <branch> --repo coryj627/slate
```

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

11. Print only its last five characters, or `absent`. If they match the
    last group of your key, the image holds the key in clear text.

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
  3. `<vm>: claimed`, or `<vm>: claimed (job <status> on this runner)`,
     only for a job still running five minutes after the handoff.
  4. Sometimes
     `<vm>: job <id> is <status> on GitHub while its VM is off; waiting up to 6 ticks for it to complete`,
     usually with `in_progress`: GitHub's record lagged the guest's
     shutdown. The loop asks again every tick and decides within a minute
     on what GitHub reports then.
  5. One of `<vm>: commit <lane> generation <n> (trusted main)`,
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
  and its sweep removes those VMs and the runners already offline. Re-run
  the failed jobs. Drain first when you can.
- **Power.** Keep host sleep and hibernation off on AC power. A sleeping
  host stalls every job.
- **Watching a live job VM.** `vmconnect.exe localhost <vm>` opens its
  console, the interactive `runner` desktop. The console is a picture of the
  guest's screen; NVDA's OCR (`NVDA+R`) reads it. Do not stop a VM by hand
  unless you mean its job to fail and its cache fork to be discarded. The
  loop knows only that it did not force a VM off itself: a VM you turn off
  just after a green main job looks to it like one that shut down cleanly.

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
carry a SHA-256 (`runnerSha256`, `pythonSha256`, `gitSha256`,
`rustupInitSha256`): when a version moves, take its new hash from the page
its `sources` entry names, and update that entry too. Have the Windows 11
Pro key ready: the build asks for it again.

1. Drain and pause the loop (see "Pausing and draining"). The task stays
   disabled until step 7.
2. Pull the merged `main` into the checkout, and open the elevated window
   there.
3. Move the old disk aside. It is read-only, but a rename still works:

   ```powershell
   Rename-Item C:\slate-ci\golden\win11-runner.vhdx win11-runner.prev.vhdx
   ```

4. Refresh `bin`. A plain re-run keeps both tasks as they are, so the loop
   stays disabled:

   ```powershell
   .\ci\windows-runner\install\setup-host.ps1
   ```

5. Build:

   ```powershell
   .\ci\windows-runner\golden\build-golden.ps1 -IsoPath C:\Users\cory\Downloads\Win11_25H2_English_x64_v2.iso
   ```

6. Check the product key on the rebuilt disk: "Checking the product key in
   the golden image". If it finds the key, roll back (below).
7. Enable the task, only after step 6 passed. The new disk is sealed, so
   the loop starts within a minute. Check the log for `orchestrator start`.

   ```powershell
   Enable-ScheduledTask -TaskName slate-ci-orchestrator
   ```

8. Flip the pool back to `home`, then dispatch `windows.yml` on a branch:

   ```powershell
   gh workflow run windows.yml --ref <branch> --repo coryj627/slate
   ```

9. Once that run is green, delete the old disk:

   ```powershell
   Remove-Item C:\slate-ci\golden\win11-runner.prev.vhdx -Force
   ```

Cache parents survive a golden rebuild untouched.

### Rolling back a refresh

If the build fails, the product-key check finds the key, or the branch run
on the new disk is red, put the old disk back.

1. If step 8 flipped the pool to `home`, flip it back to `namespace`. If
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

5. Enable the task, only now that the old disk is back. The loop starts
   within a minute, on the old disk:

   ```powershell
   Enable-ScheduledTask -TaskName slate-ci-orchestrator
   ```

6. Flip the pool back to `home`.

If the same change also moved host code that the old image cannot talk to,
such as the KVP functions, put the previous commit's host code back too
(see "Re-running setup").

## Re-running setup

A plain re-run of `setup-host.ps1` keeps `slate-ci-host`, its password and
both tasks, and so the stored token, as long as both tasks exist. It
refreshes `C:\slate-ci\bin` from the checkout and repairs anything missing.
The running loop keeps the code it started with, so restart it after a
refresh.

If a task is missing, a plain re-run takes the reset path. It prints
`a scheduled task is missing: the password is reset so both can be registered`,
sets a new password, registers both tasks again, the loop task disabled,
and deletes `token.xml`. Run `store-token.ps1` again afterwards, then
enable the loop task (`Enable-ScheduledTask -TaskName slate-ci-orchestrator`).

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

5. Resume the loop and flip the pool back to `home`.

Changes to the task definitions (triggers, settings, the `pwsh` path) take
effect only with `-ResetAccount`. It removes both tasks, sets a new random
password and registers both tasks again, the loop task disabled. An
administrator's password reset makes the stored token unreadable, so it
also deletes `token.xml`.

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

1. Create the new PAT as in Install, step 1. If GitHub rejects the old
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
- **GitHub's JSON reply after an `admission:`, `startup:` or `<vm>:` line.**
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
  also clears the retry table.
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
| Who can run code here | Only approved workflows. Every fork pull request waits for an owner click, every time. Only `WINDOWS_RUNNER_MODE=home` routes here, so a fork or Dependabot run that cannot read the variable goes to Namespace and `windows-latest`. Non-fork branches are the owner's own commits (and Renovate's). |
| What a job can touch | A VM that exists for that job only, as the standard user `runner`, with no secret valid outside the VM. Its JIT runner config is single-use. |
| Where a job can connect | The Internet, through the host's NAT. Hyper-V port ACLs drop everything to and from 10/8, 172.16/12, 192.168/16, 100.64/10 (Tailscale), 169.254/16, multicast 224/4, broadcast 255.255.255.255, 0/8 and all IPv6; the image also turns IPv6 off. The host firewall drops inbound traffic from the VM subnet to the host's own addresses there: `10.77.0.1`, multicast and broadcast. |
| What survives a job | Only a cache merge, and only after the host verifies from GitHub's API: a green push, schedule or dispatch on `main` of this repository, a VM the loop did not force off, and an unchanged parent generation. |
| What the host account can do | `slate-ci-host` is a Hyper-V Administrator, not an Administrator, but Microsoft treats membership in Hyper-V Administrators as equivalent to administrator rights on the host. Its limits stop mistakes and interactive use; they do not contain a compromise of the account or of the code it runs. It cannot log on interactively or remotely. Inside `C:\slate-ci` it writes `state`, `cache`, `vms` and `logs`; `bin` and `golden` are read-only for it. Like any standard account, it can still create folders elsewhere on `C:\`. It holds the one PAT: Actions read, Administration read and write, this repository only. |
| Who can change what runs | Only an elevated administrator writes `C:\slate-ci\bin`, the golden disk and the tasks. In the guest, only Administrators and SYSTEM can write the bootstrap scripts in `C:\slate-guest`. |
| Residual risk, accepted | A job reaches the Internet with a read-only `GITHUB_TOKEN`, as on GitHub-hosted runners. The home router's public (WAN) address is a public destination, so the port ACL passes it: whatever the router serves there (remote administration, UPnP, ports forwarded into the LAN) is reachable from a job. Keep those off, and check by hand (step 4 of "Refreshing the pilot's isolation targets"). |

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
