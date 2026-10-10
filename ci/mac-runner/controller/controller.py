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
    env = dict(os.environ, PATH=PATH, HOME=HOME)
    return subprocess.run(cmd, env=env, input=input_bytes, capture_output=True, timeout=timeout, check=check)


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
        req.add_header("Authorization", "Bearer " + token())
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
    """Virtualization.framework (macOS 15+) wants this user's login keychain
    unlocked when there is no GUI session, which is the case under launchd.
    Phase 0 found that a keychain named login.keychain refuses its own
    password for this account, so admin-setup designates slate.keychain as
    the login keychain instead; unlock whichever is designated."""
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
    res = run(["security", "unlock-keychain", "-p", password, "login.keychain"], timeout=20)
    if res.returncode == 0:
        run(["security", "set-keychain-settings", "login.keychain"], timeout=20)  # no auto-lock
        log("keychain: unlocked login.keychain")
    else:
        log("keychain: could NOT unlock login.keychain: {}".format(res.stderr.decode(errors="replace").strip()))


# --- heartbeat --------------------------------------------------------------

class Heartbeat:
    def __init__(self):
        self.last_sent = 0.0
        self.last_value = None

    def send(self, healthy):
        value = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()) if healthy else "0"
        if not healthy and self.last_value == "0":
            return
        if healthy and time.time() - self.last_sent < HEARTBEAT_INTERVAL:
            return
        status, _ = github("PATCH", "/repos/{}/actions/variables/{}".format(REPO, HEARTBEAT_VAR),
                           {"name": HEARTBEAT_VAR, "value": value})
        if status == 404:
            status, _ = github("POST", "/repos/{}/actions/variables".format(REPO),
                               {"name": HEARTBEAT_VAR, "value": value})
        if status in (201, 204):
            self.last_sent = time.time()
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


def vm_exists(name):
    res = tart("list")
    for line in res.stdout.decode(errors="replace").splitlines()[1:]:
        parts = line.split()
        if len(parts) >= 2 and parts[1] == name:
            return True
    return False


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
    """Reads the runner's output, notices when a job starts."""

    def __init__(self, proc, vm):
        super().__init__(daemon=True)
        self.proc = proc
        self.vm = vm
        self.job_started_at = None
        self.lines = 0

    def run(self):
        for raw in self.proc.stdout:
            line = raw.decode(errors="replace").rstrip()
            self.lines += 1
            if "Running job:" in line and self.job_started_at is None:
                self.job_started_at = time.time()
                log("[{}] {}".format(self.vm, line.strip()))
            elif "Listening for Jobs" in line or "Job " in line and ("completed" in line or "result" in line):
                log("[{}] {}".format(self.vm, line.strip()))


class CycleAbort(Exception):
    """A cycle that ends before the runner served a job; the message is the outcome."""


def one_cycle(hb):
    vm = "job-" + time.strftime("%Y%m%d-%H%M%S", time.gmtime())
    runner_id = None
    outcome = "no job"
    vm_proc = None
    try:
        allowlist = fetch_allowlist()
        if allowlist is None:
            raise CycleAbort("idle: no allow-list")

        res = tart("clone", WARM_VM, vm, timeout=300)
        if res.returncode != 0:
            raise CycleAbort("clone failed: " + res.stderr.decode(errors="replace").strip())
        tart("set", vm, "--cpu", str(CPU), "--memory", str(MEMORY_MB))

        env = dict(os.environ, PATH=PATH, HOME=HOME)
        vm_proc = subprocess.Popen(
            ["tart", "run", vm, "--no-graphics", "--net-softnet-block=@host", "--root-disk-opts=sync=none"],
            env=env, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
        deadline = time.time() + BOOT_TIMEOUT
        while time.time() < deadline:
            if vm_proc.poll() is not None:
                err = vm_proc.stderr.read().decode(errors="replace").strip()
                raise CycleAbort("vm exited during boot (code {}): {}".format(vm_proc.returncode, err[:600] or "no stderr"))
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
            hb.send(healthy=True)
            now = time.time()
            if reader.job_started_at is None:
                if flag("paused") or flag("tripped") or flag("recycle"):
                    outcome = "idle VM replaced ({})".format("paused" if flag("paused") else "tripped" if flag("tripped") else "recycle")
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
            if reader.job_started_at is not None:
                outcome = "job finished in {} s".format(int(time.time() - reader.job_started_at))
                set_flag("last-clean-job", vm + "\n")
            else:
                outcome = "runner exited without a job (code {})".format(runner.returncode)
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
        deregister(vm, runner_id)
        log("[{}] {}".format(vm, outcome))


# --- main loop --------------------------------------------------------------

def handle_signal(signum, _frame):
    log("signal {}: stopping after the current VM is cleaned up".format(signum))
    _stop.set()


def main():
    os.makedirs(STATE, exist_ok=True)
    signal.signal(signal.SIGTERM, handle_signal)
    signal.signal(signal.SIGINT, handle_signal)
    log("controller starting as {} (repo {}, warm {}, {} vCPU, {} MB)".format(
        os.environ.get("USER", "?"), REPO, WARM_VM, CPU, MEMORY_MB))
    unlock_keychain()
    check_panic_breaker()
    hb = Heartbeat()
    idle_logged = None

    while not _stop.is_set():
        reason = None
        if flag("paused"):
            reason = "paused"
        elif flag("tripped"):
            reason = "tripped: " + read_state("tripped")
        elif not os.path.exists(TOKEN_FILE):
            reason = "no token at {}".format(TOKEN_FILE)
        elif not softnet_ok():
            reason = "softnet root rule failed"
        elif free_gb() < MIN_FREE_GB_STOP:
            reason = "free disk {:.0f} GB under {} GB".format(free_gb(), MIN_FREE_GB_STOP)
        elif not vm_exists(WARM_VM):
            reason = "warm image {} missing".format(WARM_VM)

        if reason:
            if idle_logged != reason:
                log("idle: " + reason)
                idle_logged = reason
            hb.send(healthy=False)
            _stop.wait(IDLE_SLEEP)
            continue
        idle_logged = None
        if free_gb() < MIN_FREE_GB_WARN:
            log("warning: free disk {:.0f} GB".format(free_gb()))

        outcome = one_cycle(hb)
        if outcome.startswith(("clone failed", "vm exited", "guest agent", "no jit", "could not", "idle: no allow-list")):
            hb.send(healthy=False)
            _stop.wait(60)

    hb.send(healthy=False)
    log("controller stopped")


if __name__ == "__main__":
    sys.exit(main())
