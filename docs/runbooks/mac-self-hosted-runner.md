# Mac self-hosted runner: operating the Studio runner

The design and its history are in `docs/plans/42_self_hosted_mac_runner_plan.md`.
This runbook is the day-to-day: what runs where, how to drive it, and how to
recover. Everything here runs on the Mac Studio unless it says GitHub.

## What is where

| Thing | Place | Owner |
|---|---|---|
| Controller, `runnerctl`, admission hook sources, Softnet copy | `/usr/local/libexec/slate-runner/` | root; installed by `ci/mac-runner/host/admin-setup.sh` |
| LaunchDaemons: controller and nightly warm rebuild | `/Library/LaunchDaemons/com.slate.mac-runner*.plist` | root |
| Images `slate-mac-{vanilla,base,toolchain,warm}` | `slate-ci`'s `~/.tart/vms` | slate-ci |
| Token, guest passwords, builder SSH key, keychain password | `/Users/slate-ci/.slate-runner/` (mode 600) | slate-ci |
| Flags, allow-list cache, markers | `/Users/slate-ci/.slate-runner/state/` | slate-ci |
| Logs | `/Users/slate-ci/.slate-runner/logs/` | slate-ci |
| Synced copy of `ci/mac-runner` for builds | `/Users/slate-ci/.slate-runner/mac-runner/` | slate-ci, written by `run-as-slate-ci.sh` |
| Allow-list of record | `ci/mac-runner/allowlist.json` on `main` | the repo |

Guest accounts: `admin` (provisioning only, sudo asks the host-held password),
`runner` (auto-logged in, runs hook and job, no sudo), `builder` (nightly
warm build over SSH, never logged in).

The runner's environment comes from `/Users/runner/actions-runner/.env`
alone, installed from `ci/mac-runner/image/data/runner.env`: the hook path,
`PATH`, the Rust homes and the tool cache. The runner never reads `.path`.
The guest has no Homebrew, so `PATH` has no `/opt/homebrew/bin`; a job that
needs a tool gets it from the toolchain image, not from `brew`.

## Everyday

```bash
ci/mac-runner/host/runnerctl status        # flags, heartbeat, VMs, log tail
ci/mac-runner/host/runnerctl pause         # finish the current job, then stop taking work
ci/mac-runner/host/runnerctl resume
ci/mac-runner/host/runnerctl logs 200
```

Pausing clears the heartbeat at once, so new mac jobs route to Namespace
within the route job's 10-minute window. Use it before heavy local work, a
call, or a reboot.

Routing override, on GitHub: repository variable `MAC_RUNNER_MODE` set to
`studio`, `namespace` or `auto` (`gh variable set MAC_RUNNER_MODE --body namespace`).

## The services

```bash
sudo launchctl bootstrap system /Library/LaunchDaemons/com.slate.mac-runner.plist
sudo launchctl bootstrap system /Library/LaunchDaemons/com.slate.mac-runner.warm-rebuild.plist
sudo launchctl kickstart -k system/com.slate.mac-runner      # restart after a controller update
sudo launchctl bootout system/com.slate.mac-runner           # stop for good
```

After a host reboot the controller starts once FileVault is unlocked at the
console. Until then the heartbeat is stale and jobs go to Namespace.

## Images

| Layer | Rebuild when | Command (as the owner) | Time |
|---|---|---|---|
| base | macOS point release | `ci/mac-runner/host/run-as-slate-ci.sh bash /Users/slate-ci/.slate-runner/mac-runner/image/build-base.sh` | ~10 min plus IPSW download |
| toolchain | Xcode, Rust pin, analyzer pin, monthly | `... image/build-toolchain.sh` | ~8 min |
| warm | nightly at 03:00 (automatic), or `runnerctl rebuild-warm` | `... image/build-warm.sh` | ~3 min |

Inputs the toolchain build expects in `/Users/slate-ci/.slate-runner/cache/`:
`Xcode-<build>.tar` (from the host's `/Applications/Xcode.app`), the Actions
runner and guest agent tarballs with their checksums verified, `rustup-init.sh`.
The Phase 0 log in the plan records how each was produced.

`build-toolchain.sh` boots the result in job mode and probes it: who runs
`tart exec`, Finder Apple Events, Xcode, Rust, hook ownership, sudo, SIP. A
failed probe leaves the stage VM when `KEEP_STAGE=1`.

Each build keeps the previous generation as `<layer>.prev`. Roll back:

```bash
ci/mac-runner/host/run-as-slate-ci.sh tart rename slate-mac-warm slate-mac-warm.bad
ci/mac-runner/host/run-as-slate-ci.sh tart rename slate-mac-warm.prev slate-mac-warm
ci/mac-runner/host/runnerctl recycle
```

Disk: track with `df -h /`, not `tart list`; layers share blocks. Budget is
90 GB (D4). The controller warns under 40 GB free and idles under 20.

## Adding a person or tool

1. Edit `ci/mac-runner/allowlist.json` on a branch: add `{id, login, type}`
   to `actors` (IDs from `gh api users/<login> --jq .id`; bots as
   `users/<name>%5Bbot%5D`). Merge. The controller picks it up on the next job.
2. Run `ci/mac-runner/github/apply-policy.sh --apply` with your own `gh`
   login. This rewrites the workflow execution policy. It is deliberate: a
   compromised `main` can widen the hook's list but not the policy.
3. If the tool opens PRs under its own identity and GitHub refuses them,
   control 1 (collaborators only) is the cause; decide per plan §3.6.

## Tokens

The controller's fine-grained token (repo `coryj627/slate` only;
Administration read/write, Variables read/write; 90 days) lives at
`/Users/slate-ci/.slate-runner/github-token`. Rotate:

```bash
pbpaste | ci/mac-runner/host/runnerctl set-token     # or type it, then Ctrl-D
ci/mac-runner/host/runnerctl status                  # "token file: present"
```

The controller reads the file on every API call, so no restart is needed.

## When things go wrong

- **Jobs queue, nothing runs.** `runnerctl status`: paused? tripped? token
  present? softnet ok? free disk? Then `runnerctl logs`. The heartbeat going
  stale already routes new jobs to Namespace; a job already queued for the
  Studio waits up to 24 h unless re-run.
- **"tripped".** A host kernel panic report is newer than the last clean
  job. Read `/Library/Logs/DiagnosticReports/*.panic`, then
  `runnerctl clear-panic` once you have a view.
- **Softnet root rule failed.** The root-owned copy or the sudoers rule is
  gone, usually after a reinstall. Re-run `admin-setup.sh` (with
  `SOFTNET_SOURCE=` for a release Homebrew lacks).
- **Finder tests hang about 120 s each.** The Finder grant is missing in the
  image. Rebuild the toolchain; `build-toolchain.sh`'s probe must print
  "Macintosh HD".
- **Runner refused by GitHub (version too old).** The nightly warm build
  installs the latest runner; check the warm-rebuild log and that the
  controller recycled its idle VM.
- **Admission hook denies an owner job.** Read the "Set up runner" step: the
  line names the failing check. Compare with `allowlist.json` on `main` and
  the cached copy in `state/`.
- **Host feels sluggish during jobs.** `runnerctl pause`, then lower `CPU`
  in the warm build (the VM shape is set on the warm image) and in
  `controller.py`'s `SLATE_VM_CPU`.

## Checks before cut-over (plan Phase 3)

1. Hook proven live: a job dispatched against an image whose allow-list
   lacks the owner fails at "Set up runner" with no workflow step run.
2. `mac-ci-pilot.yml` candidate `self-hosted-tart` passes every gate.
3. `NARROW=1 sudo bash ci/mac-runner/host/admin-setup.sh`: the owner can then
   run only `runnerctl` as slate-ci. `run-as-slate-ci.sh` stops working by
   design; image rebuilds go through `runnerctl rebuild-warm` or the
   LaunchDaemon.
