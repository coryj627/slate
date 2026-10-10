# Self-hosted mac runner plan

Status: adopted 2026-10-09. The owner decided D1 to D7 the same day (§8), and
a safety review the same day added §3.6, §4.4 and D8. Phase 0 sets the
VM's final vCPU and memory within D4. Phase 0 started that
day: `tart` and `softnet` are pinned, and the admin setup script is written
but not yet run. Nothing is registered or changed in GitHub settings. Facts in §2 were read from this Mac and
the GitHub API that day; GitHub and Tart behavior was checked against their
docs, changelogs and source the same day. This runner unblocks
`41_mac_backlog_plan.md`, which is on hold until the runner is live.

## 1. Goal

Move the two mac CI lanes, `swift-tests.yml` and `a11y-check.yml`, from
Namespace's `mac-actions` profile to a throwaway Tart VM on the owner's Mac
Studio. The owner set three constraints:

1. The repo stays public, and the runner only ever runs code the owner pushed
   to `coryj627/slate`. Fork code never runs on it. A private repo with a
   public mirror is the fallback if that cannot be made to hold.
2. Tart runs the VM.
3. The runner is as fast as possible without making the Mac Studio
   unpleasant to use.

Done means all of the following hold:

- Both lanes run on the Studio for every owner PR and every main push.
- When the Studio is offline, the same jobs run on Namespace instead,
  without anyone flipping a switch.
- Jobs finish at or under Namespace's warm times (§2.3).
- Fork code cannot reach the runner even with any one GitHub-side control
  switched off. If it ever arrives, the runner rejects it before its first
  step.
- The Namespace mac profile and the `slate-mac` cache tag are retired.

## 2. Facts measured on 2026-10-09

### 2.1 Host

| Item | Value |
|---|---|
| Machine | Mac Studio (Mac17,14), Apple M5 Max |
| CPU | 18 cores: 6 Super and 12 Performance, no efficiency cores |
| Memory | 36 GB |
| Disk | 460 GB, 150 GB free |
| OS | macOS 27.0.1 (26A434) |
| Xcode | 27.0 (27A266a) |
| Tart, Softnet | 2.40.1 and 0.24.0 from Homebrew tap `openai/tools`; Softnet has no root rights yet; no VMs exist |
| Power | system sleep never; restart after power loss on |
| FileVault | on |

FileVault means a host reboot, for example a macOS update, leaves the runner
offline until someone logs in at the console.

Tart's maintainers, Cirrus Labs, joined OpenAI in April 2026. Tart, Softnet,
Orchard and the guest agent now live under `github.com/openai/`, and the old
`cirruslabs/tart` URL redirects there. Since 2.33.0 they are under the
Functional Source License (FSL-1.1-ALv2). It forbids only offering them as a
competing commercial product, so personal and open-source CI on one Mac
needs no paid license. Images are still published at `ghcr.io/cirruslabs/`,
and the image templates are still in `cirruslabs/macos-image-templates`.
Cirrus's image names call macOS 27 "golden-gate".

### 2.2 Repository

| Item | Value |
|---|---|
| Owner | personal account (user ID 933688), public repo, 0 forks |
| Collaborators | coryj627 (admin) only |
| Who may open PRs | collaborators only (`pull_request_creation_policy: collaborators_only`) |
| Last 200 PRs | all by coryj627, all from branches in this repo |
| Last 100 workflow runs | all triggered by coryj627, through `pull_request`, `push`, `schedule` and `workflow_dispatch` only |
| Bots that open PRs | none; no Dependabot or Renovate config |
| Fork PR approval | `first_time_contributors` |
| Workflow execution policies | none; the endpoint answers for this repo |
| Default `GITHUB_TOKEN` | read; cannot approve PRs |
| Action pinning | every external `uses:` is pinned to a full SHA; `sha_pinning_required` is off |
| Branch protection, rulesets | none; no check is required to merge |
| Self-hosted runners | none |

### 2.3 The mac lanes today

| Workflow | Triggers | Recent successful wall time |
|---|---|---|
| `swift-tests.yml` | PR and main push, path-filtered | 2 to 4 min |
| `a11y-check.yml` | PR and main push on Swift files | under 1 min (analyzer cached) |

Volume over the last month: 2 to 32 runs a day, usually under 10.

For reference, this Mac natively ran the debug build in 55 s and the 3,032
XCTest cases in 61 s on 18 workers (October 6 qualification in
`docs/runbooks/ci-cost-and-testing-handoff.md`).

## 3. Security design: keep the repo public

### 3.1 Threats

| # | Threat | Source |
|---|---|---|
| T1 | Fork code runs on the Studio | A fork PR's workflow YAML comes from the fork. It can name the self-hosted label and delete any `if:` guard. |
| T2 | Trusted code runs something hostile | A compromised crate, SwiftPM package or action inside a PR the owner opened. |
| T3 | One job tampers with a later one | Shared caches, a reused VM, a persisted workspace. |
| T4 | A job reaches the host or the LAN | VM networking that reaches the host's services, the router or a NAS. |
| T5 | Credentials leak | The runner-admin token, or a write-scoped `GITHUB_TOKEN` inside the VM. |
| T6 | The Studio becomes unusable | Runaway jobs, memory pressure, a full disk. |

For `pull_request` events, conditions in workflow YAML are not a security
boundary. GitHub runs a PR's workflow from the PR's merge commit, so a fork
could rename the label in `runs-on:` or delete an `if:` guard. Only GitHub's
server-side settings and the runner's own configuration sit outside a PR's
reach, so the design rests on those two.

### 3.2 Controls

The answer to the owner's first question is yes: the repo can stay public
and the runner can serve only the owner's own work. Controls 1, 2, 3 and 5
below each block fork code on their own. Control 4 keeps the runner to this
one repository.

**Enforced by GitHub, before any runner sees a job:**

1. **Only collaborators can open PRs (T1). Already on.** The repo's
   `pull_request_creation_policy` is `collaborators_only`, a setting GitHub
   added in February 2026. Only users with write access can open a PR, and
   the owner is the only collaborator. A fork can exist, but it cannot open
   a PR here, so there is no fork `pull_request` run to worry about. Anyone
   can still comment. Keep it on.
2. **A workflow execution policy (T1, new).** GitHub made these generally
   available on 2026-09-17. A repository policy with
   `enforcement: active` and these two rules covers every workflow:
   - `restrict_actions_actors` allowing only user 933688 (`coryj627`).
   - `restrict_action_events` allowing only `push`, `pull_request`,
     `workflow_dispatch` and `schedule`. The Linux nightly and audit
     workflows need `schedule`; the runner's own hook refuses it.

   A run by anyone else, or from any other event, fails on GitHub's side.
   It covers all workflows rather than the mac files, because a new
   workflow file could otherwise target the runner's label. It blocks
   nothing in use today (§2.2). The policies endpoint answers for this
   repo, but the docs are silent on personal accounts. Phase 1 confirms that
   creating a policy works here. The UI's dry-run mode is Enterprise-only,
   so Phase 1 tests it live.
3. **Fork approval for all external contributors (T1, backstop).** It only
   matters if control 1 is ever turned off. With it, no run triggered by a
   non-collaborator starts until the owner approves that run.
4. **A repository-level runner (T1).** Personal repositories have no runner
   groups. A runner registered on `coryj627/slate` serves that repository
   and no other, by construction.

**Enforced by the runner:**

5. **An admission hook, the last line (T1).** The image sets
   `ACTIONS_RUNNER_HOOK_JOB_STARTED` in the runner's environment. GitHub
   runs that script as the "Set up runner" step, before any action's `pre:`
   step, before checkout and before any workflow step. If it exits
   non-zero, the job does not run and is marked failed. The workflow's
   `env:` is not passed to it, and a workflow cannot override `GITHUB_*`
   variables. It reads the event payload GitHub sent and fails closed,
   including when a field is missing, unless every check passes:
   - The repository ID is slate's. IDs, not names: logins and repository
     names can be renamed and then claimed by someone else.
   - The event is `pull_request`, `push` or `workflow_dispatch` (D1).
     Everything else is rejected, including `schedule`,
     `pull_request_target`, `workflow_run` and `issue_comment`.
   - For `pull_request`, the head repository ID equals the base repository
     ID and the PR author's user ID is on the allow-list.
   - For `push`, the ref is `refs/heads/main`.
   - The triggering actor's user ID is on the allow-list. On a re-run that
     is whoever re-ran it, which is why the PR author is checked separately.

   The allow-list starts with one entry, user 933688 (`coryj627`), and
   grows as §3.6 describes. The hook still stops a fork job if all three
   server-side controls failed at once.

   One nuance, found in the Phase 3 live test: when the hook fails, GitHub
   marks the job failed and skips its ordinary steps, but steps guarded by
   `if: always()` or `if: failure()` still run, without a checkout and with
   the read-only token. A fork PR could put a payload in such a step. So on
   refusal the hook also terminates the runner process itself, which ends
   the job before any further step and makes the controller destroy the VM.
   Either way the hook is the last line, not the first: controls 1 to 3
   stop fork code from ever being dispatched, and the VM boundary contains
   anything that does run.

**Enforced by the VM design:**

6. **A fresh VM for every job (T3).** Each job gets a new APFS clone of the
   image and a single-use just-in-time (JIT) runner registration, which
   GitHub removes after one job. The clone is deleted afterwards, so nothing
   a job writes survives it.
7. **The admin token never enters the VM (T5).** A host controller mints
   each JIT config with a fine-grained token that only the `slate-ci`
   account can read (D5). The endpoint needs repository Administration
   write, so that token never leaves the host. Only the single-use config
   reaches the guest.
8. **Network isolation (T4).** By default Softnet lets the guest send only
   to globally routable IPv4 addresses and to the vmnet gateway. Private
   ranges, link-local, CGNAT and all IPv6 are dropped, and VMs cannot reach
   each other. The gateway is the host itself, so the plan adds
   `--net-softnet-block=@host`. Softnet's DHCP names the gateway as the DNS
   server, so the image sets a public resolver instead. DHCP itself is
   answered inside Softnet before the policy applies, so it keeps working.
   A job reaches the internet but not the Studio or the LAN. No ports are
   exposed into the guest.
9. **A read-only job token (T5).** Mac jobs get `contents: read`. The
   accessibility check's PR comment and SARIF upload move to a hosted Linux
   job that reads the mac job's artifact.
10. **Only trusted warm state (T3).** Build caches are baked into the image
    by the host, from `main` only. PR jobs cannot write anything that
    persists.
11. **Resource caps (T6).** A fixed vCPU count, memory size and disk size,
    `timeout-minutes` on every job, and a controller watchdog that stops the
    VM.

T2 is not prevented, only contained. Hostile code in a PR the owner opened,
for example from a compromised crate, runs in a throwaway VM with no
secrets, no write token and no route to the LAN.

The mac jobs also get an `if:` guard requiring a same-repo head, so a
routine mistake never routes a job to the runner. The guard is a
convenience, not a control.

### 3.3 "Only a PR I have merged"

The owner chose PRs and merges (D1). The runner takes the owner's PRs from
this repo's branches and pushes to main, so the mac checks gate merges. It
also takes manual dispatches, which the qualification pilot in
`mac-ci-pilot.yml` needs. This applies to every workflow that needs the mac
runner. The hook rejects `schedule`, because no mac workflow has one; adding
a scheduled mac job means adding that event to the hook.

Nothing anyone else writes reaches the runner.

### 3.4 Why not a private repo with a public mirror

It remains the fallback if a control in §3.2 is ever shown to be
bypassable. Today it costs more than it buys:

- It only addresses T1, which controls 1 to 5 already close. T2 to T6 need
  the same VM controls either way.
- Self-hosted runners are free on public and private repos today. GitHub
  announced a $0.002 per minute platform charge for private-repo
  self-hosted use from March 2026, then postponed it with no new date.
  Public repos were always exempt.
- Hosted jobs would start drawing on private-repo minutes, 2,000 a month on
  Free or 3,000 on Pro, then $0.006 a minute for Linux and $0.010 for
  Windows. The 16-minute Windows shell accessibility gate on every Windows
  PR and the daily Windows stress job would be the main draw.
- Plan 41 tracks 38 public issues, which would split from the code.
- A sync job and its push credential become a new secret to guard.

### 3.5 Considered and rejected: moving the repo to an organization

Runner groups and their "selected workflows" restriction belong to
organizations, so a transfer looked attractive. It adds almost nothing:

- A free organization gets one default runner group. Creating more needs
  the Team plan.
- Restricting a group to selected workflows is documented only for
  Enterprise Cloud.
- That restriction pins a workflow to a ref such as `refs/heads/main`. PR
  runs use `refs/pull/N/merge`, so the pin would also shut out the owner's
  own PRs.
- The transfer itself moves issues and PRs and redirects URLs, but it
  clears issue assignees other than the owner and adds churn for no gain.

The workflow execution policy in control 2 gives the actor and event
restriction without a transfer.

### 3.6 Adding a person or tool

Codex has opened 23 PRs here so far, every one with the owner as author, so
the controls pass them today. The owner expects Codex and other tools to
open PRs under their own identities later, and wants one place to grant
that. The allow-list is a file on main, `ci/mac-runner/allowlist.json`: a
list of GitHub accounts by numeric ID, each with its login as a note. Codex
would be `chatgpt-codex-connector[bot]`, ID 199175422. Two consumers read
it:

- **The hook.** The controller fetches the file from main before each job
  and streams it into the guest with the JIT config. A merge to main is
  enough. If the fetch fails, the controller keeps its last good copy; with
  no copy at all it starts no VM.
- **The GitHub policy (control 2).** The owner runs
  `ci/mac-runner/github/apply-policy.sh`, which reads the same file and
  rewrites the policy's allowed actors with the owner's own GitHub login.
  This step is deliberate: a compromised main can widen the hook's list,
  but not the policy, so the two stay independent.

Adding a tool is one PR that edits the file, then one run of the script.
Removing one is the same in reverse. The first PR a tool opens under its own
identity also answers an open question: whether control 1, collaborators
only, lets an installed app open PRs at all. If it does not, the owner
chooses between relaxing control 1, with controls 2 and 5 still refusing
anyone not on the list, and keeping tools acting as the owner.

## 4. Architecture

```
Mac Studio (host)
└─ LaunchDaemon: slate-runner-controller, UserName=slate-ci (D5)
   on start: refuse to run if a host panic report is newer than the last
             clean job (circuit breaker, see §9)
   every 5 min while healthy and not paused:
             set repo variable MAC_RUNNER_HEARTBEAT to the current time (D6)
   loop:
     1. tart clone slate-mac-warm job-N; tart set job-N --cpu --memory
     2. tart run job-N --no-graphics
          --net-softnet-block=@host
          --root-disk-opts=sync=none
     3. POST generate-jitconfig with the token only slate-ci can read
     4. tart exec -i job-N, as the guest's runner user: write the allow-list
        from main and the JIT config to 0600 files from stdin, then start
        run.sh --jitconfig
        guest: admission hook -> exactly one job -> runner exits
     5. watchdog: tart stop after 75 min
     6. tart stop and tart delete job-N; remove the registration if it survived
   nightly: rebuild slate-mac-warm from main as the guest's builder user,
            with the default disk sync (§4.4)
```

The controller boots the next VM as soon as it deletes the previous one, so
one idle runner is always waiting and queue time is close to zero.

`tart exec` talks to the guest agent over the VM's control socket and vsock,
not the network, so it works with the host blocked. The guest agent is a
LaunchAgent with no session limit, so commands run as the auto-logged-in
`runner` user inside its GUI session (§4.4). The XCTest suite needs that
session for `NSApplication.shared` and for Finder Apple Events. An SSH
session would not be a GUI session, so jobs never use one; image builds do,
because they need no GUI and must run as other guest accounts.

### 4.1 The host account (D5)

The controller, the images and the token belong to `slate-ci`, a standard
macOS account with no admin rights. A VM escape, or a bug in the
controller, lands in that account and not in the owner's files or
credentials.

- **How it runs.** A LaunchDaemon with `UserName=slate-ci`, the way Orchard
  deploys its workers. It needs no GUI login. After a reboot it starts as
  soon as FileVault is unlocked at the console, and a planned reboot with
  `fdesetup authrestart` skips even that.
- **Its keychain.** Since macOS 15, Virtualization.framework needs the VM
  owner's login keychain unlocked. The controller creates and unlocks a
  dedicated keychain for `slate-ci` at start, as the Tart FAQ describes.
- **The token.** It sits in a file inside `slate-ci`'s home that only that
  account can read. A keychain the daemon must unlock unattended would add
  no protection.
- **Softnet's root rights.** Softnet restarts itself as root with
  `sudo -n` and its own path. A sudoers rule lets `slate-ci`, and only that
  account, do that without a password. The rule names a root-owned copy at
  `/usr/local/libexec/slate-runner/bin/softnet`, which the controller puts
  first on its `PATH`. Pointing the rule at the Homebrew binary would be
  unsafe: the owner's account can write those directories, so anything
  running as the owner could swap the binary and gain root. This is also
  narrower than Tart's default setuid bit, which every local account could
  use. Re-running the setup script refreshes the copy after a deliberate
  Softnet upgrade.
- **The owner acting as `slate-ci`.** A second rule lets the owner's
  account run commands as `slate-ci`, never as root, without a password. It
  is how the Phase 0 to 3 work gets done without a password prompt at every
  step. It also reverses part of D5 in one direction: anything running as
  the owner, this session included, can read the controller's token and
  start VMs. The owner accepted that for Phases 0 to 3 on 2026-10-09.
  Before Phase 4 the rule narrows to the controller's own command, so the
  owner can pause, resume and rebuild but not read the token; Phase 2
  builds that command and Phase 3 checks the rule before cut-over.
- **A separate account for the VM process (D8, open).** Today the VM
  process and the token share `slate-ci`, so a guest escape reaches the
  repo-admin token. Running the VMs as a third account that holds nothing,
  with the controller reaching them through sudo, would close that. The
  owner deferred the decision to Phase 2, informed by Phase 0.
- **Setup.** `ci/mac-runner/host/admin-setup.sh` makes the account, the
  Softnet copy and both rules, and checks each one. The owner runs it once
  with sudo.
- **Disk.** `~/.tart` lives in `slate-ci`'s home, on the same volume.

### 4.2 Why a custom controller

The controller is a small script kept in this repo, not an existing tool.
Cilicon, Tartelet and sand all reach the guest over SSH, which is not a GUI
session, and register with ordinary registration tokens rather than JIT
configs. Orchard has no GitHub runner support of its own. runscaler matches
this design most closely, using JIT configs and `tart exec`, but it is a
very young project. It is worth reading as a reference, not adopting.

### 4.3 No shared folders

The design uses no shared folders (`tart run --dir`). Tart issue
[openai/tart#1308](https://github.com/openai/tart/issues/1308), open and
unanswered, reports repeated host kernel panics on an M5 Max during
headless runs with a read-only shared folder under heavy file traffic.
Everything the guest needs is either baked into the image or streamed
through `tart exec`.

### 4.4 Guest accounts

The nightly warm build runs main's own build scripts. If it ran as the same
user the runner uses, a compromised main could plant something that runs in
that user's session before the next job's hook and tampers with it. The
owner chose to close that (review finding 4, 2026-10-09). The guest has
three accounts, and nothing a build or a job runs can touch what admits the
next job:

| Account | Kind | Used for | Session |
|---|---|---|---|
| `admin` | admin, sudo asks for a password only the host holds | image builds over SSH with Packer | never logged in at run time |
| `runner` | standard, no sudo | the hook and the job; owns `_work` | auto-logged in, so the guest agent and `tart exec` run as it |
| `builder` | standard, no sudo | the nightly warm build over SSH, with a key held on the host | never logged in |

- The hook and the runner binaries are root-owned under
  `/usr/local/slate-runner`, written at image build time. The allow-list
  arrives fresh from the controller just before each job (§3.6).
- The warm build's last step runs as `admin` and hands the finished
  workspace to `runner`. What builder leaves there is data: it executes only
  inside the job, after admission, which is T2.
- builder cannot write runner's home, system launch agents or admin-group
  directories such as `/Applications`, and anything it plants in its own
  home never runs, because it is never logged in.
- Cirrus's images give `admin` passwordless sudo and auto-login. The
  toolchain layer sets a random `admin` password, held only by the
  controller, and switches auto-login to `runner`.
- Builds boot with `--net-softnet-block="out @host"`, which still lets the
  host open SSH connections inward; jobs boot with `@host` blocked in both
  directions.

### 4.5 Images

Cirrus publishes a ready macOS 27 image with Xcode 27.0,
`ghcr.io/cirruslabs/macos-golden-gate-xcode:27`, but it is 85 GB on disk. Its
template downloads every simulator platform and adds Android, Flutter and
other tools slate never uses. That is too much for 150 GB of free disk, and
too much third-party content to trust. The plan builds a lean image with
Packer and Cirrus's public templates instead:

| Layer | Contents | Rebuilt |
|---|---|---|
| `slate-mac-base` | Cirrus's `vanilla-golden-gate` template from Apple's macOS 27.0.x IPSW, on an ASIF disk; `admin` with Remote Login set by Tart's first-boot provisioning; then the `disable-sip` template | on macOS point releases |
| `slate-mac-toolchain` | Xcode 27.0 (27A266a) with no simulator platforms, license accepted and first launch done; rustup with 1.97.1, rustfmt and clippy; the a11y-check analyzer built at `bcaddd56`; the `runner` and `builder` accounts (§4.4) and the `admin` password change; the Actions runner and the admission hook, root-owned under `/usr/local/slate-runner`, the hook outside the runner's directory as GitHub requires; the guest agent; Finder Apple Events grants for runner's processes; auto-login as `runner`; Spotlight, sleep, screensaver and automatic updates off; a public DNS resolver | on an Xcode, Rust pin or analyzer bump, and monthly |
| `slate-mac-warm` | as `builder`: `main` checked out at the runner's work path, then `scripts/build-mac-app.sh --skip-a11y-check`, `swift build --build-tests` and `make swift-cli`; then as `admin`: the latest runner release, and the workspace handed to `runner` | nightly |

Layer builds run with Tart's default disk sync, so a finished layer is fully
on disk before it is cloned. Only job clones use `sync=none` (§5.2).

- Xcode comes from the host's own `/Applications/Xcode.app`, streamed in with
  `ditto` over `tart exec`. That gives the guest the exact build the owner
  uses, with no Apple ID download. The host copy is 3.7 GB. If the copied
  bundle fails signature checks, the fallback is the `.xip`.
- Every layer is built from Apple's IPSW plus scripts kept in this repo and
  Cirrus templates read before use. Pulling Cirrus's prebuilt
  `macos-golden-gate-base` (41 GB on disk) is the shortcut if the vanilla
  build stalls.
- Expected size: about 41 GB for the base, plus Xcode, Rust and the warm
  build products. Phase 0 measures it. Clones and layers share blocks on
  APFS, so a job clone costs only what the job writes.

The warm layer's checkout sits at the exact path the runner uses, and the
self-hosted jobs check out with `clean: false`. A PR job therefore starts
with `target/` and `apps/slate-mac/.build` already built from main.

The previous generation of each layer is kept, so a bad image rolls back with
one command. Images stay local and are never pushed to a registry.

### 4.6 Finder Apple Events in the guest

The Trash tests hang about 120 s each without Automation consent for Finder.
Manual consent profiles are not an option, because privacy (PPPC) profiles
install only through MDM.

GitHub's hosted macOS 27 images write the grants straight into both TCC
databases during the image build (`configure-tccdb-macos-27.sh` in
`actions/runner-images`). On macOS 27 the per-user database moved under
`/private/var/containers/Data/ProtectedSystem/`; that script finds it by
asking `lsof` which file `tccd` holds open. It grants Apple Events to Finder
for `/bin/bash`, `/usr/bin/osascript` and its own agents. Cirrus's equivalent
script grants the guest agent several permissions, but not Apple Events to
Finder, so the plan adds that row.

Writing these rows needs SIP off. Both GitHub's hosted images and Cirrus's
base images ship with SIP off. The plan tries re-enabling SIP after writing
the rows. If the grants stop working, the guest keeps SIP off like GitHub's
hosted images do. That is acceptable here because the guest is discarded
after every job, and the boundary that matters is the VM itself.

Which process TCC holds responsible is not documented. It is most likely the
guest agent, since `tart exec` starts the runner. The image grants Finder
Apple Events, in `runner`'s user database, to the guest agent, the runner
binaries, `/bin/bash`, `/bin/zsh` and `/usr/bin/osascript`. The gate is that the Finder probe and
the XCTest Trash tests pass under the runner inside a VM. The fallback is
approving the prompt once over VNC while building the toolchain layer.

## 5. Performance and the host budget

### 5.1 VM shape

Start at 12 vCPUs and 16 GB, which leaves the host 6 cores and 20 GB.

- The vCPU count is the one knob that reliably protects the host. Tart has
  no CPU pinning, so the guest's vCPUs float across all 18 cores as ordinary
  host threads, and macOS still schedules interactive work alongside them.
- Guest memory is reserved but allocated lazily, and it is pageable host
  memory. Under pressure the VM slows down rather than the host. Tart has no
  memory balloon, so the guest never hands memory back mid-run.
- Only one VM runs at a time. Virtualization.framework refuses a third macOS
  guest, Apple's licence allows two extra copies of macOS per Mac for
  development and testing, and 36 GB fits one well-sized VM.
- Expect about 20% overhead against the host, mostly from disk I/O. A
  maintainer measured an Xcode build at 307 s on an M1 host and 400 to 470 s
  in a VM.
- Phase 0 measured 8, 12 and 14 vCPUs against 12, 16 and 20 GB and picked
  the smallest shape within 10% of the best time: 12 vCPUs and 16 GB (the
  Phase 0 log has the table).

Lowering the VM's scheduling priority is less certain. The guest runs in
Apple's `com.apple.Virtualization.VirtualMachine` XPC service, not inside the
`tart` process, so `nice` or `taskpolicy` on `tart` may never reach the vCPU
threads. Phase 0 tries `taskpolicy` on the XPC service's PID and launchd's
`ProcessType` on the controller, and keeps whichever measurably helps.

### 5.2 Speed levers, biggest first

1. The warm image from main. A cold run took 15 to 30 minutes on hosted
   runners.
2. A pre-booted idle runner, which takes boot time off the critical path.
3. The baked analyzer. The accessibility job loses its cache restore and the
   possible 2-minute build.
4. `sync=none` on the root disk, for job clones only. Apple documents it
   for a VM that runs once to completion, and losing a throwaway VM's
   unflushed writes costs nothing. Layer builds keep the default, because
   their disks are kept. `caching=cached` is measured before adoption,
   because published benchmarks show it helping some workloads and badly
   hurting others.
5. The ASIF disk format, which Tart says is faster on macOS 26 and later.
6. Spotlight off in the guest, and `~/.tart` excluded from the host's
   Spotlight.

### 5.3 Targets to verify in Phase 0

| Metric | Target |
|---|---|
| Next idle runner ready after a job | under 60 s |
| `swift-tests` job, warm | 3 min or less |
| `a11y-check` mac job | 1 min or less |
| Host during a job | no visible lag in ordinary use |

### 5.4 Operating the host

- A `pause` and `resume` command for the controller. Pause lets the current
  job finish and boots no new VM, for heavy local builds or calls.
- Disk: everything `slate-ci` stores fits in 90 GB (D4). The guest's
  virtual disk is capped near 80 GB so one runaway job cannot blow the
  budget; Phase 0 sets the exact size from measured use. The IPSW is
  deleted once the base layer is built. The previous warm layer is always
  kept for rollback, and older base and toolchain layers only while they
  fit. The controller refuses to build a layer that would cross the budget,
  and warns when the host's free space drops under 40 GB.
- After a host reboot the controller starts once FileVault is unlocked at
  the console. Until then the heartbeat goes stale and jobs fall back to
  Namespace (§6.1).

## 6. Workflow changes

- `swift-tests.yml`: add the route job (§6.1) and run on the label it picks;
  drop the Namespace cache action and the cache attestation steps; check
  out with `clean: false` on the Studio path; add `timeout-minutes: 30` and
  the `if:` guard.
- `a11y-check.yml`: add the same route job, and split into a mac job that
  runs the analyzer with
  `contents: read` and uploads the JSON and SARIF, and a hosted Linux job
  that uploads the SARIF, posts the PR comment and enforces the floor.
- `mac-ci-pilot.yml`: add a `self-hosted-tart` candidate. The existing
  harness becomes the qualification gate: 3,032 cases at the reference
  digest, the Release load, and the analyzer's cold and warm passes.
### 6.1 Falling back to Namespace (D6)

The owner chose automatic fallback. GitHub has no built-in "use this runner,
else that one", so each mac workflow starts with a short routing job:

- **The route job** runs on hosted Linux, which is free on public repos and
  takes seconds. It reads two repository variables and outputs the label
  the mac jobs use in `runs-on`.
  - `MAC_RUNNER_MODE` is `auto`, `studio` or `namespace`. It defaults to
    `auto` and is the manual override.
  - `MAC_RUNNER_HEARTBEAT` is the controller's last check-in time. In
    `auto` the route job picks the Studio when it is under 10 minutes old,
    and Namespace otherwise.
- **The heartbeat.** The controller writes it every 5 minutes while healthy.
  `pause` clears it at once, so pausing also sends new jobs to Namespace.
  The controller's token needs Variables write for this, besides
  Administration.
- **The Namespace target** is the direct label
  `nscloud-macos-tahoe-slim-arm64-6x14`, with no cache volume. It already
  passed every gate of the full pilot on October 7
  ([run 37695877260](https://github.com/coryj627/slate/actions/runs/37695877260)),
  Finder Apple Events included. That run's whole cold native phase,
  including the Release build, took 543 s, so a cold fallback run of the
  Swift tests should take under 9 minutes. With no volume there is no
  storage bill and none of the cache-trust questions in
  `docs/runbooks/ci-cache-policy.md`.
- **Steps that differ by route** are conditioned on the route job's output.
  The Studio path uses the warm checkout and the baked analyzer. The
  Namespace path builds cold and restores the analyzer from GitHub's cache
  as `a11y-check.yml` does today.
- **One gap remains.** A job routed to the Studio just before it goes down
  waits in the queue, and GitHub cancels a queued job no runner takes within
  24 hours. Re-running it routes it to Namespace.

None of this is a security control. A PR cannot change repository
variables, and the route only chooses where the owner's own jobs run.

## 7. Phases

### Phase 0: spike on the Studio (no repo or GitHub change)

1. The owner runs `sudo bash ci/mac-runner/host/admin-setup.sh` (§4.1).
   Tart only offers its setuid fix on a terminal, so a daemon needs this
   set up beforehand. `tart` and `softnet` are pinned in Homebrew so an
   upgrade cannot change them underneath the controller; that was done on
   2026-10-09.
2. As `slate-ci`, from a test LaunchDaemon, boot any small VM with
   `--net-softnet-block=@host`. This proves the daemon, the dedicated
   keychain and the sudoers rule work before anything is built on them. If
   macOS asks to allow Claude or Terminal local network access during the
   spike, the owner approves it once; the daemon itself is exempt.
3. Build `slate-mac-base` from the 27.0.x IPSW with Cirrus's templates.
4. Build a draft toolchain layer by hand, recording every step as a script.
5. Pass these gates:
   - The Finder probe answers and the XCTest Trash tests pass, run through
     `tart exec`. Try with SIP re-enabled first.
   - From the guest, the internet works. The host, the router, another LAN
     machine and IPv6 link-local all fail.
   - Build and test timings across the shape grid in §5.1, plus a
     subjective check of the host during a run.
   - Disk used by each layer and by one job clone.
   - A soak of 30 back-to-back clone, boot, build, test and delete cycles
     with no host panic.

Exit: the §5.3 targets are met or consciously revised, the VM shape is set
within D4, and the `slate-ci` daemon setup from D5 works.

#### Phase 0 log

2026-10-09, steps 1 to 3 done, step 4 in progress:

- **Step 1.** The owner ran `admin-setup.sh`. `slate-ci` is uid 502, not an
  admin, home mode 700. Both sudo rules verified, and Tart's own probe
  (`sudo -n softnet --help` through `PATH`) resolves to the root-owned copy.
  `tart` and `softnet` pinned in Homebrew.
- **Keychain.** `security create-keychain` under `sudo -u slate-ci` makes a
  `login.keychain-db` that then refuses its own password, while a keychain
  with any other name unlocks fine. It did not matter: Linux and macOS
  guests both ran as `slate-ci` from the owner's session with no keychain
  error. Still to check from a LaunchDaemon (the plist is written,
  `ci/mac-runner/host/launchd/`, and needs the owner to load it).
- **Step 2, isolation (Ubuntu guest, job mode `--net-softnet-block=@host`).**
  All 16 probes passed: `tart exec` works with the host blocked; internet by
  IP and DNS through 1.1.1.1 work; DNS through the gateway, the host's SSH
  and ICMP, the router, two LAN machines, two RFC1918 addresses, the
  169.254.169.254 metadata address and IPv6 all fail; the host cannot reach
  the guest. Softnet's help text explains the modes: a bare target blocks
  VM-to-target packets one way and stateless, while `out X` and `in X` are
  stateful flows by initiator. A second experiment booted the test image
  under plain NAT, `block out @host`, and `block out @host` plus
  `allow in @host`: in all three the host reached the guest's port 22 and the
  guest could not open a connection to the host. The one earlier failure was
  a timing fluke. Builds use `out @host`; jobs use `@host`.
- **Secrets on the command line.** The first toolchain build passed the guest
  passwords to Packer as `-var` arguments, which `ps` shows to every local
  account. Only `cory`, `slate-ci` and `root` exist on this host, and the
  passwords only matter inside the guest, so they were not rotated. All
  build scripts now pass secrets as `PKR_VAR_*` environment variables.
- **Step 3, base layer.** Packer 1.16.1 from `hashicorp/tap` and the Tart
  plugin v1.21.0, installed for `slate-ci`. `tart create --from-ipsw latest`
  downloaded the 26.6 GB IPSW in 4 min 9 s and installed macOS 27.0.1
  (26A434), the host's own build. Stage 1 (first boot, provisioning,
  settings) took 2 min 30 s and stage 2 (SIP off) 2 min 16 s. `slate-mac-base`
  shows 32 GB used on a 60 GB disk and shares most blocks with the 29 GB
  vanilla VM. The IPSW was deleted afterwards; 149 GB free.
- **Inputs for the toolchain layer.** The host's Xcode 27.0 (27A266a) as a
  9.7 GB tar (the 3.7 GB `du` figure was APFS compression), the Actions
  runner 2.338.0 checked against the SHA in its release notes, the guest
  agent 0.15.0 checked against its checksums file, and `rustup-init.sh`.
- **Step 4, first toolchain attempt.** Xcode uploaded and installed from the
  tar in about 3 minutes, with `-runFirstLaunch` and developer mode done and
  no simulator runtimes. The `runner` (uid 502) and `builder` (uid 503)
  accounts were created as standard users. Two defects stopped the build: a
  fresh macOS has no `/usr/local/bin`, so the guest agent install failed,
  and `sysadminctl -autologin set` returned error 22 for a never-logged-in
  account. Fixed by creating the directory and by writing `/etc/kcpassword`
  the way GitHub's macOS images do. Packer deletes the stage VM on failure;
  `KEEP_STAGE=1` keeps it.
- **Second toolchain attempt, Softnet build mode.** Packer's SSH reached the
  guest through `out @host`, confirming the mode for builds. Auto-login was
  set (`/etc/kcpassword`, 33 bytes). Two more findings: Packer's file
  provisioner turns a one-file directory upload into a file when the
  destination does not exist (fixed by creating the directories first), and
  `csrutil status` reported SIP **enabled**, so stage 2's blind keystrokes
  into recovery had not worked. Stage 2 now synchronises on what the plugin
  reads from the screen (`<wait 'Options'>`, `<click 'Continue'>`, and so
  on) and a new stage 2b boots the VM and fails the build unless SIP reports
  disabled. Stage 3 also refuses to start on a SIP-enabled base.
- **SIP off, third attempt.** The first screen-synchronised run reached
  Recovery's Terminal but then hung for 36 minutes waiting for a username
  prompt: on 27.0.1 (26A434) `csrutil disable` asks the yes/no question and
  then "Enter password for user admin:" with no username step, unlike the
  Cirrus template written for 27.0. The plugin's log records every line it
  reads from the screen, which is how the prompts were found. With the
  sequence matched to those prompts, SIP went off, and stage 2b's normal
  boot reported "System Integrity Protection status: disabled". A `<wait>`
  on text that never appears blocks forever, so build wrappers keep a
  timeout.
- **Toolchain layer built, third attempt.** Stage 3 took 7 min 31 s over
  Softnet build mode, 2 min 41 s of it the analyzer's release build; stage
  3b took 22 s. `slate-mac-toolchain` shows 40 GB used, sharing blocks with
  the base. The probe booted it in job mode and found: `tart exec` runs as
  `runner` in an Aqua session; **Finder answered an Apple Event from the
  runner account** (`get name of startup disk` returned "Macintosh HD" well
  inside the 20 s cap, where an unanswered consent gate would have hung 120
  s); Xcode 27.0 (27A266a); cargo 1.97.1; the hook is root's; runner has no
  sudo; admin's sudo asks for a password; SIP is off. The TCC rows were
  written into runner's per-user database under
  `/private/var/containers/Data/ProtectedSystem/<uuid>/`, found through
  tccd's open files as planned. Re-enabling SIP (stage 3c) has not been
  tried yet; the image keeps SIP off for now, like GitHub's hosted images.
- **Warm layer, first attempt.** On 12 vCPUs and 16 GB, the builder account
  cloned main (84583d9c) and ran the full cold build that `swift-tests.yml`
  runs, `build-mac-app.sh`, `swift build --build-tests` and `make swift-cli`,
  in **139 s**. For scale only, since those phases also ran the tests and a
  Release build: the hosted pilot's cold native phase took 1,844 s and
  Namespace's 543 s. The stage then failed on its last line,
  reading the commit hash as builder after the tree had been handed to
  runner; git refuses the ownership mismatch. Fixed by reading it before the
  hand-off.
- **Softnet 0.24.0 drops inbound packets on some boots.** The second warm
  attempt never got Packer's SSH through: the guest had its address, a
  default route and sshd listening, it reached the internet, and the host
  held its ARP entry, yet host-to-guest connections failed. Softnet issue
  openai/softnet#213 describes exactly this for 0.24.0, "on about 15% of
  boots, guest-to-host still works", and 0.24.1, released 2026-10-09, carries
  the fix (a /29 per VM instead of the /30 that macOS's DHCP mishandles,
  #214). The guest's netmask here was indeed /30. This also explains the
  first isolation probe's build-mode failure. Homebrew's tap still ships
  0.24.0, and its formula is only the GitHub release tarball, so 0.24.1's
  tarball was fetched and checked against the published SHA-256 into
  `slate-ci`'s cache; `admin-setup.sh` now takes `SOFTNET_SOURCE` to install
  it as the root-owned copy. Job mode (`@host`) needs no inbound traffic and
  was never affected. The warm layer was built once over NAT meanwhile.
- **Warm layer, third attempt.** The build took 150 s and then failed on
  the record step: the script's run-as-root helper feeds sudo its password
  on stdin, and wrapping `tee` with it made `tee` consume the password line
  instead of the record. The record is now written to a temporary file and
  installed. Rule for these scripts: the helper never wraps a command that
  reads stdin.
- **Softnet 0.24.1 installed** as the root-owned copy by the owner re-running
  `admin-setup.sh` with `SOFTNET_SOURCE` (2026-10-09, late evening). Homebrew
  still holds the pinned 0.24.0, which nothing uses; it can move to 0.24.1
  when the tap catches up. Both isolation modes were rerun against 0.24.1:
  job mode passed all 16 probes again, and build mode passed all of its 17,
  the host reaching the guest's SSH port while the guest still could not
  open a connection to the host, resolve DNS through it, or reach the LAN.
- **Warm layer built, fourth attempt.** Stage 4 took 175 s in all, 150 s of
  it the build. `slate-mac-warm` records main at 84583d9c and runner 2.338.0,
  and shows 47 GB used on the host. That figure, like the 32 GB and 40 GB of
  the other layers, counts shared blocks: the host's free space went from
  150 GiB before Phase 0 to 131 GiB with all four layers, the Ubuntu test
  image and 9.8 GB of cached inputs present, so the real cost of the whole
  image chain so far is about 19 GiB against the 90 GB budget (D4). Track
  the budget by `df`, not by `tart list`. Inside the guest, the root container
  (about 50 GiB after the recovery partition) has roughly 8 GiB free after
  the warm build: enough for incremental PR jobs, tight for anything more.
  Growing the disk needs a new base created with `--disk-size 80`, or the
  recovery partition removed so the guest agent can resize on boot; a Phase
  2 item.
- **Step 5, timing grid on the warm image** (`phase0-shape-test.sh`, job
  mode, one VM at a time, each step as the runner account on the warm tree):

  | vCPU:GB | boot to exec | `build-mac-app.sh` again | `swift build --build-tests` | `swift test --parallel` | host load before to after |
  |---|---|---|---|---|---|
  | 8:12 | 18 s | 38 s | 25 s | 72 s | 5.2 to 7.7 |
  | 12:16 | 17 s | 35 s | 25 s | 60 s | 8.1 to 11.1 |
  | 14:20 | 17 s | 39 s | 23 s | 58 s | 10.1 to 10.6 |

  The "again" column is `scripts/build-mac-app.sh --skip-a11y-check` on an
  already-built tree; it still regenerates and restages the Swift bindings,
  so SwiftPM recompiles the app module, which is what a PR job pays too.
  Twelve vCPUs are within 4% of fourteen and eight are 24% slower, so the
  §5.1 rule settles **D4 at 12 vCPUs and 16 GB**. A warm PR cycle is about
  2 min 20 s of compute before GitHub's own overheads, against Namespace's 2
  to 4 min wall time (§2.3), and the §5.3 target of 3 min or less looks
  reachable. The host's load average rose to about 11 on 18 cores during
  the test; the owner's subjective check of the Studio during a job is
  still open.
- **Analyzer timing** on a warm clone: 7 s human, 5 s JSON, 4 s SARIF, score
  100 with 20 criteria passed, against the hosted job's cache restore plus a
  possible 2 to 3 minute build.
- **Soak** (`phase0-soak-test.sh`, 30 cycles of clone, boot in job mode,
  `tart exec` as runner, stop, delete): 30 of 30 clean, clone under 1 s,
  boot to a responding guest agent 12 to 19 s (mostly 17 to 18), about 18 s
  per cycle, 548 s in all. No VM or Softnet process left behind, and no
  host panic report before, during or after (openai/tart#1308 did not
  reproduce; the design uses no shared folders).
- **XCTest under the runner account.** The grid's `swift test --parallel`
  exited 0 on every shape in about a minute. With Finder consent missing it
  would have stalled around 120 s per Trash test, so the suite, Trash tests
  included, runs inside the job-mode VM.

#### Phase 0 gate review (2026-10-09, late)

| Gate (§7 step 5, §5.3) | Result |
|---|---|
| Finder probe and Trash tests pass under the runner in a VM | met |
| Internet works; host, router, LAN, IPv6, metadata address fail | met, in job and build mode, Softnet 0.24.1 |
| Timings across the shape grid | met; D4 settled at 12 vCPUs, 16 GB |
| Disk per layer and per clone | met: about 19 GiB real for all layers, clones near zero until they write |
| 30-cycle soak with no host panic | met |
| Next idle runner ready after a job, under 60 s | 18 s to a responding guest; registration adds a few seconds (Phase 3 measures) |
| `swift-tests` warm, 3 min or less | about 2 min 20 s of compute; confirm with the real runner in Phase 3 |
| `a11y-check` mac job, 1 min or less | 16 s for all three modes |
| Host has no visible lag during a job | **open: owner's call**, load average reached about 11 of 18 |
| `slate-ci` works from a LaunchDaemon (D5) | **open**: everything so far ran as `slate-ci` from the owner's session via sudo; the daemon plist is written and needs the owner to load it once |

Two items remain for the owner before Phase 0 closes. Phase 1 (GitHub
settings) and Phase 2 (the controller, hook and allow-list) can start in
parallel with them.

#### Phase 1 and 2 progress (2026-10-10)

- **Phase 1 tooling.** `ci/mac-runner/github/phase1-settings.sh` shows the
  current state and, with `--apply`, sets fork approval, SHA pinning, the two
  repository variables and the workflow execution policy (through
  `apply-policy.sh`, which reads `allowlist.json`). It prints the recipe for
  the controller's token, which only the owner can create. The dry run on
  2026-10-10 found: control 1 on, fork approval `first_time_contributors`,
  SHA pinning off, no variables, no policies.
- **Phase 1 applied by the owner, 2026-10-10.** Fork approval is
  `all_external_contributors`; `sha_pinning_required` is on; the variables
  `MAC_RUNNER_MODE=namespace` and `MAC_RUNNER_HEARTBEAT=0` exist; and the
  workflow execution policy was created as id 7070, `active`, with the actor
  rule (user 933688) and the event rule (push, pull_request,
  workflow_dispatch, schedule). So **personal repositories do support
  workflow execution policies**, closing that open question. The first
  owner push and PR after this will show the policy admitting them; the
  Phase 2 PR is that test. The owner then created the fine-grained token
  (repository `coryj627/slate`; Administration and Variables, read and
  write; expires 2027-01-08) and stored it with `runnerctl set-token`; the
  API answered 200 to the runner and variable reads. A first attempt had
  stored the clipboard's command text instead of the token, caught by that
  same check. **Phase 1 is complete.**
- **Phase 2 code written** under `ci/mac-runner/`: `allowlist.json`;
  `hook/admission.py` with 36 unit tests covering every case listed above
  plus malformed inputs; `route/route.py` with 20 tests; `controller/` with
  the controller, `runnerctl`, and the two LaunchDaemon plists;
  `admin-setup.sh` now installs the controller files root-owned and takes
  `NARROW=1` for the Phase 3 sudo rule; `host/runnerctl` wraps the sudo;
  `.github/workflows/mac-runner-tests.yml` runs the tests and ShellCheck on
  hosted Linux; `docs/runbooks/mac-self-hosted-runner.md`. The toolchain
  image now installs the real hook from `ci/mac-runner/hook/`, so the
  toolchain and warm layers must be rebuilt before Phase 3.
- **D8** stays open until the LaunchDaemon test has run.
- **Phase 3 step 1, first controller start (2026-10-10).** The owner loaded
  both LaunchDaemons. The controller ran as `slate-ci` under launchd,
  reached GitHub with the token, read the cached allow-list, cloned the warm
  image, and then every `tart run` failed within seconds:
  `VZErrorDomain Code=-9 "The virtual machine encountered a security
  error" ... Failed to get current host key ... Failed to create new
  HostKey`. The same command succeeds from the owner's session via sudo.
  Virtualization.framework stores a per-user host key in the **login**
  keychain, so a daemon needs that keychain unlockable (Tart FAQ, "headless
  machines", added 2025-10-08 after openai/tart#1132). A different keychain
  set as default and unlocked does not satisfy it, and `security
  login-keychain -s` is refused on macOS 27. `slate-ci`'s
  `login.keychain-db`, created by `security create-keychain`, refuses its
  own password, which fits the login keychain being bound to the account
  password; `slate-ci`'s was random and discarded. Fix in progress: the
  owner sets `slate-ci`'s account password to the stored keychain password,
  the login keychain is recreated with it, and the controller unlocks it at
  start. Two defects found on the way were fixed first: a controller log
  line that swallowed the failure reason, and `admin-setup.sh` reinstalling
  Homebrew's Softnet 0.24.0 over 0.24.1 on a plain re-run.
- **Resolution (2026-10-10, 05:19 UTC).** Three things were needed together.
  (1) A known account password: `sysadminctl -resetPasswordFor` needs a
  secure-token admin to authorise it on a FileVault Mac, so the owner ran it
  with `-adminUser cory -adminPassword -` and typed their own password.
  (2) A real login keychain: a keychain created by `security
  create-keychain` under the name `login.keychain` refuses its password on
  macOS 27 whatever the password is; the owner logged in once as `slate-ci`
  at the login window (account temporarily unhidden; `CGSession -suspend`
  gets to the login window) and loginwindow created a 35 KB one bound to the
  account password. (3) A security session for the daemon: even that
  keychain refuses to unlock from a `sudo -u slate-ci` shell inside the
  owner's session, but with `SessionCreate` in the LaunchDaemon plist the
  controller's own `security unlock-keychain` succeeded and the first VM
  stayed up. The controller unlocks the keychain by full path, since the
  short name goes through a search list that a login rewrites. D5 holds:
  the controller runs as `slate-ci` from launchd, no GUI session of its own.
  The account's password was on the owner's screen during this and is to be
  rotated once Phase 3 passes.
- **Phase 3 step 1 met (05:20 UTC).** The first VM under the daemon booted
  in 18 s, the JIT registration (id 10468) came online as
  `job-20261010-051941` and the heartbeat variable updated. Note for Phase
  4: a JIT runner carries **only** the labels the controller asks for, here
  `slate-mac-tart`, with none of the usual `self-hosted`, `macOS`, `ARM64`
  defaults, so `runs-on` must name `slate-mac-tart` alone.
- **Phase 3 step 2, the hook live (05:23 UTC).** With the controller's
  cached allow-list swapped for one naming only a non-existent user, the
  pilot was dispatched to `self-hosted-tart` (run 38027327498). Both its
  mac jobs landed on fresh VMs and failed at "Set up runner" with `DENY ...
  actor id 933688 is not on the allow-list`; no checkout happened. Two
  findings: the job's `if: always()` upload steps still ran after the
  refusal (see §3.2 control 5; the hook now terminates the runner on
  refusal, which needs a toolchain and warm rebuild), and the controller
  crashed on a `tart exec` that blocked past its 15 s timeout during boot
  (launchd restarted it; the wrapper now reports a timeout as a failure and
  the loop survives any exception). The real allow-list was restored
  afterwards.
- **Live test, round 2 (05:41 UTC), with the hook that kills the runner on
  refusal:** the `always()` steps still ran. The step log showed the DENY
  line followed at once by "Process completed with exit code 1" and none of
  the hook's own messages: the runner invokes the hook with `bash -e`, so
  errexit ended the script at the failing check before the kill lines. The
  shim now disables errexit first and, on refusal, SIGKILLs the listener,
  the worker and its own parent (SIGTERM would only cancel the job, which
  still runs `always()` steps). A new in-place stage, `03e-hook.pkr.hcl`
  with `refresh-hook.sh`, rolls a hook change into the toolchain and warm
  layers in about four minutes.
- **Live test, round 3 (05:51 UTC): passed.** Run 38028908109, both mac
  jobs refused. GitHub recorded "Set up job" as the only completed step; "Set
  up runner" and every later step, the `always()` uploads included, ended
  with no result, because the worker was killed mid-step. The listener
  reported the job failed and exited, and the controller destroyed the VM
  within ten seconds. **Phase 3 step 2 is met.** `phase3-hook-livetest.sh`
  (`deny`, `check`, `restore`) repeats the test in a few minutes whenever
  the hook changes.
- **Phase 3 step 3, pilot attempt 1 (05:55 UTC, run 38029129798).** With
  the real allow-list back, the hook admitted both mac jobs ("Set up runner"
  succeeded, the admit path live). Both then failed at `actions/setup-python`:
  on a self-hosted Mac it installs the requested Python with `sudo
  installer`, and the runner account has no sudo by design. GitHub's hosted
  images avoid this by pre-installing Pythons in the runner tool cache, where
  `setup-python` finds a matching version and installs nothing. The toolchain
  layer now does the same for the 3.13 series using the same
  actions/python-versions tarball and its `setup.sh`, run as root at build
  time (`scripts/toolchain-python.sh`; in place as `03f-python.pkr.hcl`,
  rolled out with `refresh-inplace.sh`).
- **Pilot attempt 2 (06:06 UTC, run 38029754403)** failed at the same step:
  "Version 3.13 was not found in the local cache". The cache sat under the
  runner's default `_work/_tool`, but `setup-python` hard-codes
  `/Users/runner/hostedtoolcache` on macOS, the hosted images' path, and
  the runner itself takes `RUNNER_TOOL_CACHE` from its environment. The
  cache now lives at `/Users/runner/hostedtoolcache` and the runner's `.env`
  names it, so the runner and every setup action agree.
- **Pilot attempt 3 (06:14 UTC, run 38030237144):** Python and both
  checkouts passed; the pilot's own preflight then failed its ancestry check
  with "Not a valid commit name a64eb393". That frozen source lived on the
  Codex branch, which was deleted after PR 1328 was squash-merged, so a
  checkout of branch refs cannot fetch it; this is a harness problem, not a
  runner one, and would hit the hosted candidates equally. The reference and
  the workflow default moved to c247b865, the same change on `main`, with the
  identical `apps/slate-mac/Tests` tree (5a12505b) and inventory digest.
  Attempt 4 follows.
- **PR A opened 2026-10-10:** coryj627/slate#1335, branch
  `ci/mac-runner-phase2`, with everything above. Its hosted checks are the
  first runs under the workflow execution policy.
- **Pilot candidate.** `self-hosted-tart` (label `slate-mac-tart`) added to
  `scripts/mac-ci-pilot.py`, `mac-ci-pilot.yml`, the pilot's tests and its
  runbook. The pilot checks out `source` fresh and runs a Release build, so it
  needs more guest disk than the 8 GiB the 60 GB chain left; the whole chain
  is being rebuilt from the IPSW with an 80 GB disk (`DISK_SIZE`,
  `REBUILD_VANILLA=1` in `build-base.sh`), the cap §5.4 names. Host cost
  stays what is written, not the logical size.

### Phase 1: GitHub settings (owner, before any runner registers)

- Confirm PR creation stays limited to collaborators.
- Create the workflow execution policy from §3.2 control 2, as `active`.
  Check it the same day: a dispatch by the owner still runs, and a policy
  that temporarily omits the owner blocks that dispatch. If personal repos
  turn out not to support policies, record that here and rely on controls
  1, 3, 4 and 5.
- Set fork PR approval to "all external contributors".
- Turn on "require actions to be pinned to a full-length commit SHA". Every
  workflow already complies.
- Create the fine-grained token for the controller: this repository only,
  Administration read and write for the JIT endpoint, Variables read and
  write for the heartbeat, and a 90-day expiry. Store it where only
  `slate-ci` can read it (§4.1).
- Create the repository variables `MAC_RUNNER_MODE`, set to `namespace`
  until Phase 4, and `MAC_RUNNER_HEARTBEAT`.

None of these has been changed yet.

### Phase 2: PR A, the runner infrastructure (`ci/mac-runner/`)

- Packer templates and scripts for the toolchain and warm layers.
- The admission hook, with unit tests over recorded event payloads: a fork
  PR, an owner PR from this repo, a same-repo PR by another account, a push
  to main, a push to another branch, `schedule`, `pull_request_target`,
  `workflow_run`, a dispatch by the owner and by someone else, a re-run by
  someone else, and a payload with a missing field. A hosted Linux workflow
  runs them on changes to `ci/mac-runner/**`.
- The route job's choice, tested the same way: each mode, a fresh
  heartbeat, a stale one and a missing one.
- The controller, its LaunchDaemon plist for `slate-ci`, `pause` and
  `resume`, the heartbeat, the watchdog and the panic circuit breaker.
- `ci/mac-runner/allowlist.json` and `ci/mac-runner/github/apply-policy.sh`
  (§3.6), with a test that the hook reads the file and refuses an ID not
  in it.
- The guest accounts and root-owned paths of §4.4 in the Packer templates.
- The controller's command for the owner, and the narrowed sudoers rule
  that replaces the Phase 0 to 3 rule (§4.1).
- D8 decided (§4.1), with Phase 0's experience of Tart under a daemon.
- A runbook, `docs/runbooks/mac-self-hosted-runner.md`, covering rebuilds,
  rollback, token rotation and recovery after a reboot.

None of this is secret. Publishing it costs nothing, because the protection
comes from GitHub's settings and the hook's logic, not from hiding them.

### Phase 3: register and qualify (no lane moves yet)

1. Start the controller with the label `slate-mac-tart`.
2. Prove the hook live. Build a test image whose allow-list omits
   `coryj627`, dispatch a job to it, and confirm the job fails at the hook
   with no workflow step run. Restore the real image.
3. Add the `self-hosted-tart` candidate to `mac-ci-pilot.yml` and dispatch
   the full pilot. It must pass the same gates the hosted and Namespace
   candidates passed.
4. Install the narrowed sudoers rule from Phase 2 and confirm the owner's
   account can no longer read the token or run arbitrary commands as
   `slate-ci`. Phase 4 does not start before this.

### Phase 4: PR B, cut over

Move `swift-tests.yml` and `a11y-check.yml` as described in §6, with the
route job, then set `MAC_RUNNER_MODE` to `auto`. Prove the fallback before
relying on it: pause the controller, push a PR, and confirm the jobs run and
pass on Namespace; resume, and confirm the next push runs on the Studio.
Then watch a week of real PRs.

### Phase 5: PR C, retire Namespace mac

Retire the `mac-actions` profile use and the `slate-mac` cache tag by
following `docs/runbooks/ci-cache-policy.md`. Update the workflow comments,
the runbooks and plan 41's §2 verification lanes. Plan 41 then resumes at
Wave 1.

## 8. Owner decisions

All seven were decided by the owner on 2026-10-09.

- **D1, trigger policy: PRs and merges.** The runner takes the owner's PRs
  from this repo's branches, pushes to main, and manual dispatches, for
  every workflow that needs the mac runner (§3.3).
- **D2, public or private: public.** The repo stays public with the §3.2
  controls. A private repo with a public mirror stays the fallback if a
  control is ever shown to be bypassable.
- **D3, workflow policy scope: every workflow.** The policy allows only
  `coryj627` and only `push`, `pull_request`, `workflow_dispatch` and
  `schedule`, for all workflows. A future collaborator or bot needs adding
  to it.
- **D4, VM shape: up to 90 GB.** The Studio has 36 GB of memory, so the
  90 GB is the disk budget for everything `slate-ci` stores: images, job
  clones and the IPSW download. The controller enforces it (§5.4). Phase 0's
  timing grid settled the VM at **12 vCPUs and 16 GB** (Phase 0 log, step
  5).
- **D5, host account: a dedicated account.** `slate-ci`, a standard user,
  runs the controller as a LaunchDaemon (§4.1).
- **D6, offline Studio: fall back to Namespace.** Fallback is automatic
  through the controller's heartbeat, and `MAC_RUNNER_MODE` is the manual
  override (§6.1).
- **D7, guest macOS: macOS 27.** The guest runs macOS 27.0.x, matching this
  Mac and the hosted `xcode-27` image.

From the safety review of 2026-10-09, also decided: the owner-to-`slate-ci`
sudo rule stays for Phases 0 to 3 and narrows before Phase 4 (§4.1); the
guest gets separate build and run accounts (§4.4); layer builds use the
default disk sync (§4.5); the hook compares IDs (§3.2); tools such as Codex
are added through the allow-list (§3.6). Still open:

- **D8, a separate account for the VM process.** Decide in Phase 2 (§4.1).

## 9. Risks

- **Host kernel panic.** Tart issue #1308 reports an M5 Max host panicking
  during headless runs with shared folders. The design uses no shared
  folders, Phase 0 soaks 30 cycles, and the controller stops itself if a
  host panic report is newer than its last clean job.
- **The grants may not survive SIP.** If Finder grants stop working with
  SIP re-enabled, the guest keeps SIP off, as GitHub's hosted images do.
- **Softnet loses its root rights after a Homebrew upgrade.** The tools are
  pinned, and the controller checks `sudo -n softnet --help` before each
  boot. If the check fails, it stops with a clear message and clears the
  heartbeat, so jobs go to Namespace meanwhile.
- **The runner falls behind GitHub's minimum version.** GitHub stops
  queueing jobs to a runner more than 30 days behind the newest release,
  patch releases included, and blocks it at once after a critical security
  release. Releases come roughly monthly; v2.338.0 shipped 2026-10-06. The
  nightly warm layer installs the latest release. It is unverified whether
  a JIT runner updates itself, so the image must not rely on that.
- **Workflow policies may not apply to personal repos.** The endpoint
  answers here, but the docs do not say. Phase 1 tests it. Without it,
  controls 1, 3, 4 and 5 still each stop fork code.
- **A tool's first PR under its own identity is refused.** Control 1 may
  not let an installed app open PRs, and the policy and hook refuse any ID
  not yet on the allow-list. The failure is a blocked PR, never a surprise
  run; §3.6 is the fix.
- **A guest escape reaches the repo-admin token.** The VM process and the
  token share `slate-ci` until D8 is decided. The attacker would need code
  the owner pushed plus a working Virtualization.framework escape. The
  worst outcome is changed settings or a deleted repo, which GitHub can
  restore; keeping macOS current is the main defence.
- **The Studio is offline.** A reboot needs FileVault unlocked at the
  console before the controller starts. Jobs fall back to Namespace
  meanwhile, except one routed to the Studio just before it went down
  (§6.1).
- **Fallback runs are slower.** A cold Namespace run should take under 9
  minutes, against about 3 on a warm Studio. If fallbacks turn out
  frequent, giving the fallback a cache is a later decision.
- **Disk.** The runner's budget is 90 GB of the 150 GB free. If the lean
  image plus a warm build does not fit, the first things to cut are older
  layer generations; moving `slate-ci`'s `~/.tart` to an external SSD with
  `TART_HOME` is the escape hatch.
- **Tart's new owner.** Tart now belongs to OpenAI. Its direction may
  change, but the source is public under FSL, and the plan pins versions.
- **The controller's token is powerful.** Repository administration can
  change settings or delete the repo. It lives only on the host, is scoped
  to this repo and expires quickly.
- **A compromised dependency on main.** The nightly warm build runs main's
  build scripts, so a bad crate could persist in the warm image for a day.
  It is contained by the same VM controls, and every warm layer is rebuilt
  from the toolchain layer.
