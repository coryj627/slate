#!/usr/bin/env python3
"""Host controller for the slate self-hosted mac runner.

docs/plans/42_self_hosted_mac_runner_plan.md, section 4. Runs as the slate-ci
account from a LaunchDaemon (controller/com.slate.mac-runner.plist). Loop:

  clone slate-mac-warm -> boot in job mode (Softnet, host blocked)
  -> stream the allow-list from main and a single-use JIT runner config in
  -> start the runner, which serves exactly one job and exits
  -> stop and delete the VM, remove any leftover registration -> repeat.

While healthy and not paused it refreshes the MAC_RUNNER_HEARTBEAT repository
variable every five minutes; the route job sends mac work to Namespace when
that goes stale (section 6.1). `runnerctl` (same directory) drives it through
flag files in the state directory.

Stdlib only; Python 3.9 compatible (the host's /usr/bin/python3).
"""

import base64
import datetime
import json
import os
import re
import shutil
import signal
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request

HOME = os.path.expanduser("~")
STATE_DIR = os.path.join(HOME, ".slate-runner")           # secrets live here, 0700
STATE = os.path.join(STATE_DIR, "state")                  # flags, cache, markers
LOGS = os.path.join(STATE_DIR, "logs")
TOKEN_FILE = os.path.join(STATE_DIR, "github-token")
KEYCHAIN_PW = os.path.join(STATE_DIR, "keychain-password")
TREE = os.environ.get("SLATE_RUNNER_TREE", os.path.join(STATE_DIR, "mac-runner"))

REPO = os.environ.get("SLATE_REPO", "coryj627/slate")
ALLOWLIST_PATH_IN_REPO = "ci/mac-runner/allowlist.json"
WARM_VM = os.environ.get("SLATE_WARM_VM", "slate-mac-warm")
LABEL = os.environ.get("SLATE_RUNNER_LABEL", "slate-mac-tart")
CPU = int(os.environ.get("SLATE_VM_CPU", "12"))
MEMORY_MB = int(os.environ.get("SLATE_VM_MEMORY_MB", "16384"))
SOFTNET = "/usr/local/libexec/slate-runner/bin/softnet"
PATH = "/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin"

HEARTBEAT_VAR = "MAC_RUNNER_HEARTBEAT"
HEARTBEAT_INTERVAL = 300
BOOT_TIMEOUT = 180            # seconds until the guest agent must answer
JOB_TIMEOUT = 75 * 60         # watchdog once a job is running
IDLE_RECYCLE = 6 * 3600       # replace an idle VM so it picks up new images
IDLE_SLEEP = 30
BUILDING_STALE = 2 * 3600     # a `building` marker older than this is ignored
# Cycle outcomes after which the loop waits a minute instead of trying again at once.
BACKOFF_PREFIXES = ("clone failed", "vm exited", "guest agent", "no jit", "could not",
                    "idle: no allow-list", "cycle crashed", "runner exited without a job",
                    "job ended without a result")
MIN_FREE_GB_WARN = 40
MIN_FREE_GB_STOP = 20
GUEST_RUNNER_HOME = "/Users/runner"
GUEST_STATE = GUEST_RUNNER_HOME + "/.slate-runner"
PANIC_DIR = "/Library/Logs/DiagnosticReports"

_stop = threading.Event()


# --- logging --------------------------------------------------------------

def log(msg):
    stamp = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    line = "{} {}".format(stamp, msg)
    if sys.stdout.isatty():   # under launchd the file below is the log; stdout would only duplicate it
        print(line, flush=True)
    try:
        os.makedirs(LOGS, exist_ok=True)
        path = os.path.join(LOGS, "controller.log")
        if os.path.exists(path) and os.path.getsize(path) > 20 * 1024 * 1024:
            os.replace(path, path + ".1")
        with open(path, "a", encoding="utf-8") as handle:
            handle.write(line + "\n")
    except OSError:
        pass


# --- small helpers --------------------------------------------------------

def run(cmd, timeout=120, check=False, input_bytes=None):
    """subprocess.run that never raises on a timeout: a command that hangs
    (tart exec before the guest agent is up, for one) comes back as exit 124."""
    env = dict(os.environ, PATH=PATH, HOME=HOME)
    try:
        return subprocess.run(cmd, env=env, input=input_bytes, capture_output=True, timeout=timeout, check=check)
    except subprocess.TimeoutExpired as exc:
        return subprocess.CompletedProcess(cmd, 124, exc.stdout or b"", (exc.stderr or b"") + "\ntimed out after {}s".format(timeout).encode())


def flag(name):
    return os.path.exists(os.path.join(STATE, name))


def set_flag(name, content=""):
    os.makedirs(STATE, exist_ok=True)
    with open(os.path.join(STATE, name), "w", encoding="utf-8") as handle:
        handle.write(content)


def clear_flag(name):
    try:
        os.remove(os.path.join(STATE, name))
    except FileNotFoundError:
        pass


def read_state(name, default=""):
    try:
        with open(os.path.join(STATE, name), "r", encoding="utf-8") as handle:
            return handle.read().strip()
    except OSError:
        return default


def building_active():
    """True while build-warm.sh's marker is present and fresh. A marker older
    than BUILDING_STALE comes from a build that died without cleaning up; it
    is cleared and logged so the runner does not idle forever."""
    path = os.path.join(STATE, "building")
    try:
        age = time.time() - os.path.getmtime(path)
    except OSError:
        return False
    if age > BUILDING_STALE:
        log("building marker is {:.1f} h old; clearing it".format(age / 3600))
        clear_flag("building")
        return False
    return True


def stop_reason():
    """Why an idle VM should be replaced now, or None."""
    for name in ("paused", "tripped", "recycle"):
        if flag(name):
            return name
    if building_active():
        return "building"
    return None


def advertise_healthy():
    """Whether the heartbeat may say the Studio takes jobs."""
    return not (flag("paused") or flag("tripped") or building_active())


def token():
    with open(TOKEN_FILE, "r", encoding="utf-8") as handle:
        return handle.read().strip()


def github(method, path, body=None, auth=True, timeout=30):
    """Call the GitHub REST API. Returns (status, parsed_json_or_None)."""
    url = "https://api.github.com" + path
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("X-GitHub-Api-Version", "2022-11-28")
    req.add_header("User-Agent", "slate-mac-runner-controller")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    if auth:
        try:
            req.add_header("Authorization", "Bearer " + token())
        except OSError as exc:
            log("github {} {} skipped: token unreadable ({})".format(method, path, exc))
            return 0, None
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            raw = resp.read()
            return resp.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as exc:
        try:
            detail = exc.read().decode(errors="replace")[:300]
        except OSError:
            detail = ""
        log("github {} {} -> {} {}".format(method, path, exc.code, detail))
        return exc.code, None
    except (urllib.error.URLError, OSError, ValueError) as exc:
        log("github {} {} failed: {}".format(method, path, exc))
        return 0, None


# --- health -----------------------------------------------------------------

def softnet_ok():
    try:
        return run(["sudo", "-n", SOFTNET, "--version"], timeout=20).returncode == 0
    except (OSError, subprocess.TimeoutExpired):
        return False


def free_gb():
    return shutil.disk_usage("/").free / 1e9


def newest_panic():
    try:
        panics = [os.path.join(PANIC_DIR, f) for f in os.listdir(PANIC_DIR) if f.endswith(".panic")]
    except OSError:
        return None
    if not panics:
        return None
    return max(panics, key=os.path.getmtime)


def check_panic_breaker():
    """Trip if a host panic report is newer than our last clean job."""
    panic = newest_panic()
    if panic is None:
        return
    last_clean = os.path.join(STATE, "last-clean-job")
    last = os.path.getmtime(last_clean) if os.path.exists(last_clean) else 0
    if os.path.getmtime(panic) > last and not flag("tripped"):
        set_flag("tripped", "host panic report newer than last clean job: {}\n".format(panic))
        log("TRIPPED: {} is newer than the last clean job; idling until runnerctl clear-panic".format(panic))


def unlock_keychain():
    """Virtualization.framework (macOS 15+) keeps its host key in this user's
    login keychain and needs it unlocked, which under launchd nobody else does.
    The keychain is the one loginwindow created when the account logged in
    once (Phase 3); its password is the account password, kept in
    ~/.slate-runner/keychain-password."""
    try:
        with open(KEYCHAIN_PW, "r", encoding="utf-8") as handle:
            password = handle.read().strip()
    except OSError:
        log("keychain: no password file; skipping")
        return
    # The framework stores its host key in the *login* keychain, so that is
    # the one to unlock. A login keychain made with `security create-keychain`
    # refuses its own password on macOS 27; the account has to log in once so
    # loginwindow creates it, with the account password (Tart FAQ, "headless
    # machines"). The stored password is that account password.
    login_kc = os.path.join(HOME, "Library/Keychains/login.keychain-db")
    if not os.path.exists(login_kc):
        log("keychain: {} does not exist; log in once as this account to create it".format(login_kc))
        return
    # Full path: the short name resolves through the user's keychain search
    # list, which a login may rewrite.
    # `security` takes the password only as an argument (no stdin form). It is
    # visible in the process list for the fraction of a second the command
    # runs, to the host's three accounts; the keychain guards only the VM host
    # key, and the same string sits in a file only slate-ci can read. Accepted.
    res = run(["security", "unlock-keychain", "-p", password, login_kc], timeout=20)
    if res.returncode == 0:
        log("keychain: unlocked {}".format(login_kc))
        settings = run(["security", "set-keychain-settings", login_kc], timeout=20)  # no auto-lock
        if settings.returncode != 0:
            log("keychain: could not clear the auto-lock timeout on {}: {}".format(
                login_kc, settings.stderr.decode(errors="replace").strip()))
    else:
        log("keychain: could NOT unlock {}: {}".format(login_kc, res.stderr.decode(errors="replace").strip()))


# --- heartbeat --------------------------------------------------------------

class Heartbeat:
    """Writes the MAC_RUNNER_HEARTBEAT variable: the time while healthy, "0" when
    not. A healthy beat is throttled to one per HEARTBEAT_INTERVAL, except right
    after a "0": the route job must see the Studio come back without a gap."""

    def __init__(self, clock=time.time):
        self.clock = clock
        self.last_sent = 0.0
        self.last_value = None

    def send(self, healthy):
        now = self.clock()
        value = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(now)) if healthy else "0"
        if not healthy and self.last_value == "0":
            return
        if healthy and self.last_value not in (None, "0") and now - self.last_sent < HEARTBEAT_INTERVAL:
            return
        status, _ = github("PATCH", "/repos/{}/actions/variables/{}".format(REPO, HEARTBEAT_VAR),
                           {"name": HEARTBEAT_VAR, "value": value})
        if status == 404:
            status, _ = github("POST", "/repos/{}/actions/variables".format(REPO),
                               {"name": HEARTBEAT_VAR, "value": value})
        if status in (201, 204):
            self.last_sent = now
            self.last_value = value
        else:
            log("heartbeat not updated (status {})".format(status))


# --- allow-list -------------------------------------------------------------

def fetch_allowlist():
    """Allow-list from main, else the last good copy, else None."""
    cache = os.path.join(STATE, "allowlist.json")
    status, body = github("GET", "/repos/{}/contents/{}?ref=main".format(REPO, ALLOWLIST_PATH_IN_REPO), auth=False)
    if status == 200 and isinstance(body, dict) and body.get("encoding") == "base64":
        try:
            raw = base64.b64decode(body["content"])
            data = json.loads(raw)
            if int(data["repository_id"]) and data["actors"]:
                os.makedirs(STATE, exist_ok=True)
                with open(cache, "wb") as handle:
                    handle.write(raw)
                return raw
        except (KeyError, ValueError, TypeError) as exc:
            log("allow-list from main is malformed: {}".format(exc))
    try:
        with open(cache, "rb") as handle:
            log("allow-list: using cached copy")
            return handle.read()
    except OSError:
        log("allow-list: none available")
        return None


# --- tart ------------------------------------------------------------------

def tart(*args, timeout=120):
    return run(["tart"] + list(args), timeout=timeout)


def list_vms():
    """VM names from `tart list`, header dropped; empty when tart fails."""
    res = tart("list")
    names = []
    for line in res.stdout.decode(errors="replace").splitlines()[1:]:
        parts = line.split()
        if len(parts) >= 2:
            names.append(parts[1])
    return names


def vm_exists(name):
    return name in list_vms()


def stop_and_delete(vm):
    tart("stop", vm, timeout=90)
    time.sleep(2)
    if vm_exists(vm):
        res = tart("delete", vm)
        if res.returncode != 0:
            log("delete {} failed: {}".format(vm, res.stderr.decode(errors="replace").strip()))


def exec_ready(vm):
    return tart("exec", vm, "true", timeout=15).returncode == 0


def guest_write(vm, path, content, mode="600"):
    script = "umask 077; mkdir -p \"$(dirname '{p}')\" && cat > '{p}' && chmod {m} '{p}'".format(p=path, m=mode)
    res = run(["tart", "exec", "-i", vm, "/bin/sh", "-c", script], input_bytes=content, timeout=60)
    return res.returncode == 0


def tail_of(path, limit=600):
    try:
        with open(path, "rb") as handle:
            return handle.read().decode(errors="replace").strip()[-limit:]
    except OSError:
        return ""


def keep_tart_log(path, failed):
    """One file for the last VM that failed to boot; a good cycle leaves nothing."""
    try:
        if failed:
            os.replace(path, os.path.join(LOGS, "tart-last-failure.log"))
        else:
            os.remove(path)
    except OSError:
        pass


# --- runner registration ----------------------------------------------------

def jit_config(name):
    status, body = github("POST", "/repos/{}/actions/runners/generate-jitconfig".format(REPO), {
        "name": name, "runner_group_id": 1, "labels": [LABEL], "work_folder": "_work",
    })
    if status == 201 and body and body.get("encoded_jit_config"):
        return body["encoded_jit_config"], body.get("runner", {}).get("id")
    log("jit config request failed (status {})".format(status))
    return None, None


def deregister(name, runner_id):
    """Remove a registration GitHub did not remove itself."""
    status, body = github("GET", "/repos/{}/actions/runners?per_page=100".format(REPO))
    if status != 200 or not body:
        return
    for runner in body.get("runners", []):
        if runner.get("name") == name or (runner_id and runner.get("id") == runner_id):
            github("DELETE", "/repos/{}/actions/runners/{}".format(REPO, runner["id"]))
            log("removed leftover registration {} ({})".format(name, runner["id"]))


# --- one job --------------------------------------------------------------

class RunnerOutput(threading.Thread):
    """Reads the runner's output: notices when a job starts and how it ended."""

    RESULT = re.compile(r"\bJob .* completed with result: (\w+)")

    def __init__(self, proc, vm):
        super().__init__(daemon=True)
        self.proc = proc
        self.vm = vm
        self.job_started_at = None
        self.job_result = None      # Succeeded, Failed, Canceled; None if the runner died first
        self.lines = 0

    def run(self):
        for raw in self.proc.stdout:
            line = raw.decode(errors="replace").rstrip()
            self.lines += 1
            if "Running job:" in line and self.job_started_at is None:
                self.job_started_at = time.time()
                set_flag("job-running", self.vm + "\n")   # build-warm.sh waits on this
                log("[{}] {}".format(self.vm, line.strip()))
                continue
            match = self.RESULT.search(line)
            if match:
                self.job_result = match.group(1)
                log("[{}] {}".format(self.vm, line.strip()))
            elif "Listening for Jobs" in line or "unknown error code" in line:
                log("[{}] {}".format(self.vm, line.strip()))


def runner_outcome(reader, returncode):
    """How the runner process ended, from what it printed."""
    if reader.job_started_at is None:
        return "runner exited without a job (code {})".format(returncode)
    took = int(time.time() - reader.job_started_at)
    if reader.job_result is not None:
        return "job finished in {} s: {}".format(took, reader.job_result)
    # The listener prints the result line before it exits. Without one it was
    # killed, which is what the admission hook does on a refusal.
    return "job ended without a result after {} s (runner killed: admission refusal or crash, code {})".format(
        took, returncode)


class CycleAbort(Exception):
    """A cycle that ends before the runner served a job; the message is the outcome."""


def one_cycle(hb):
    vm = "job-" + time.strftime("%Y%m%d-%H%M%S", time.gmtime())
    runner_id = None
    registered = False
    outcome = "no job"
    vm_proc = None
    vm_err = None
    vm_log = os.path.join(LOGS, vm + ".tart.log")
    try:
        allowlist = fetch_allowlist()
        if allowlist is None:
            raise CycleAbort("idle: no allow-list")

        res = tart("clone", WARM_VM, vm, timeout=300)
        if res.returncode != 0:
            raise CycleAbort("clone failed: " + res.stderr.decode(errors="replace").strip())
        res = tart("set", vm, "--cpu", str(CPU), "--memory", str(MEMORY_MB))
        if res.returncode != 0:
            raise CycleAbort("could not set the VM shape: " + res.stderr.decode(errors="replace").strip())

        env = dict(os.environ, PATH=PATH, HOME=HOME)
        # tart's stderr goes to a file, never a pipe: nothing would read a pipe
        # for the hours a VM may live, and once full it blocks tart or Softnet.
        os.makedirs(LOGS, exist_ok=True)
        vm_err = open(vm_log, "wb")
        vm_proc = subprocess.Popen(
            ["tart", "run", vm, "--no-graphics", "--net-softnet-block=@host", "--root-disk-opts=sync=none"],
            env=env, stdout=subprocess.DEVNULL, stderr=vm_err)
        deadline = time.time() + BOOT_TIMEOUT
        while time.time() < deadline:
            if vm_proc.poll() is not None:
                raise CycleAbort("vm exited during boot (code {}): {}".format(
                    vm_proc.returncode, tail_of(vm_log) or "no stderr"))
            if exec_ready(vm):
                break
            time.sleep(2)
        else:
            raise CycleAbort("guest agent did not answer within {} s".format(BOOT_TIMEOUT))

        if not guest_write(vm, GUEST_STATE + "/allowlist.json", allowlist, mode="644"):
            raise CycleAbort("could not write allow-list into the guest")
        encoded, runner_id = jit_config(vm)
        if not encoded:
            raise CycleAbort("no jit config")
        registered = True   # generate-jitconfig registers the runner at once
        if not guest_write(vm, GUEST_STATE + "/jit", encoded.encode()):
            raise CycleAbort("could not write jit config into the guest")

        runner = subprocess.Popen(
            ["tart", "exec", vm, "/bin/bash", "-lc",
             "cd ~/actions-runner && exec ./run.sh --jitconfig \"$(cat ~/.slate-runner/jit)\""],
            env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        reader = RunnerOutput(runner, vm)
        reader.start()
        log("[{}] runner started (registration {})".format(vm, runner_id))
        started = time.time()

        while runner.poll() is None:
            if _stop.is_set():
                outcome = "controller stopping"
                break
            # Paused or building: a running job finishes, but the Studio stops
            # advertising itself, so nothing new queues on its label meanwhile.
            hb.send(healthy=advertise_healthy())
            now = time.time()
            if reader.job_started_at is None:
                reason = stop_reason()
                if reason:
                    outcome = "idle VM replaced ({})".format(reason)
                    break
                if now - started > IDLE_RECYCLE:
                    outcome = "idle VM recycled after {} h".format(IDLE_RECYCLE // 3600)
                    break
            elif now - reader.job_started_at > JOB_TIMEOUT:
                outcome = "WATCHDOG: job exceeded {} min".format(JOB_TIMEOUT // 60)
                log("[{}] {}".format(vm, outcome))
                break
            time.sleep(5)
        else:
            reader.join(timeout=15)   # the last lines may still be in flight
            outcome = runner_outcome(reader, runner.returncode)
            if reader.job_result is not None:
                set_flag("last-clean-job", vm + "\n")
        clear_flag("recycle")
        return outcome
    except CycleAbort as exc:
        outcome = str(exc)
        return outcome
    finally:
        if vm_proc is not None and vm_proc.poll() is None:
            tart("stop", vm, timeout=90)
            try:
                vm_proc.wait(timeout=60)
            except subprocess.TimeoutExpired:
                vm_proc.kill()
        stop_and_delete(vm)
        if registered:
            deregister(vm, runner_id)
        clear_flag("job-running")
        if vm_err is not None:
            vm_err.close()
            keep_tart_log(vm_log, failed=outcome.startswith(("vm exited", "guest agent")))
        log("[{}] {}".format(vm, outcome))


# --- main loop --------------------------------------------------------------

def handle_signal(signum, _frame):
    log("signal {}: stopping after the current VM is cleaned up".format(signum))
    _stop.set()


def sweep_leftovers():
    """VMs and registrations a previous controller left behind: a crash, or
    launchd's SIGKILL when a cleanup outran ExitTimeOut. At startup none of
    ours can legitimately exist."""
    for name in list_vms():
        if name.startswith("job-"):
            log("sweep: removing leftover VM {}".format(name))
            stop_and_delete(name)
    if os.path.exists(TOKEN_FILE):
        status, body = github("GET", "/repos/{}/actions/runners?per_page=100".format(REPO))
        runners = body.get("runners", []) if status == 200 and body else []
        for runner in runners:
            if str(runner.get("name", "")).startswith("job-"):
                github("DELETE", "/repos/{}/actions/runners/{}".format(REPO, runner["id"]))
                log("sweep: removed leftover registration {} ({})".format(runner["name"], runner["id"]))
    clear_flag("job-running")


def needs_backoff(outcome):
    """A cycle that ended this way is retried after a pause, not at once."""
    return outcome.startswith(BACKOFF_PREFIXES)


def idle_reason():
    """Why no VM should boot right now, or None."""
    if flag("paused"):
        return "paused"
    if flag("tripped"):
        return "tripped: " + read_state("tripped")
    if building_active():
        return "warm image rebuilding (state/building)"
    if not os.path.exists(TOKEN_FILE):
        return "no token at {}".format(TOKEN_FILE)
    if not softnet_ok():
        return "softnet root rule failed"
    free = free_gb()
    if free < MIN_FREE_GB_STOP:
        return "free disk {:.0f} GB under {} GB".format(free, MIN_FREE_GB_STOP)
    if not vm_exists(WARM_VM):
        return "warm image {} missing".format(WARM_VM)
    return None


def main():
    os.makedirs(STATE, exist_ok=True)
    signal.signal(signal.SIGTERM, handle_signal)
    signal.signal(signal.SIGINT, handle_signal)
    log("controller starting as {} (repo {}, warm {}, {} vCPU, {} MB)".format(
        os.environ.get("USER", "?"), REPO, WARM_VM, CPU, MEMORY_MB))
    unlock_keychain()
    check_panic_breaker()
    sweep_leftovers()
    hb = Heartbeat()
    idle_logged = None

    while not _stop.is_set():
        reason = idle_reason()
        if reason:
            if idle_logged != reason:
                log("idle: " + reason)
                idle_logged = reason
            hb.send(healthy=False)
            _stop.wait(IDLE_SLEEP)
            continue
        idle_logged = None
        free = free_gb()
        if free < MIN_FREE_GB_WARN:
            log("warning: free disk {:.0f} GB".format(free))

        try:
            outcome = one_cycle(hb)
        except Exception as exc:  # a bug must not take the controller down with it
            import traceback
            log("cycle crashed: {}: {}\n{}".format(type(exc).__name__, exc, traceback.format_exc()))
            outcome = "cycle crashed"
        if needs_backoff(outcome):
            hb.send(healthy=False)
            _stop.wait(60)

    hb.send(healthy=False)
    log("controller stopped")


if __name__ == "__main__":
    sys.exit(main())
