#!/bin/bash
# One-time admin setup on the Mac Studio for the self-hosted mac runner.
# See docs/plans/42_self_hosted_mac_runner_plan.md, §4.1 and Phase 0 step 1.
#
# Run it from the owner's own account:
#
#   sudo bash ci/mac-runner/host/admin-setup.sh
#
# Running it again is safe; every step checks what is already there.
#
# 1. Creates slate-ci, a hidden standard (non-admin) account. Its password is
#    random and never shown; the controller never logs in with it, and an
#    admin can reset it if a GUI login is ever needed.
# 2. Installs a root-owned copy of Softnet at
#    /usr/local/libexec/slate-runner/bin/softnet. Softnet re-runs itself
#    through `sudo -n <its own path>`, so the sudo rule must name a binary
#    only root can replace. The Homebrew copy sits in directories the
#    owner's account can write, which would let anything running as the
#    owner swap it and gain root.
# 3. Adds /etc/sudoers.d/slate-runner, checked by visudo before it is
#    installed:
#      - slate-ci may run that Softnet copy as root, and nothing else;
#      - the owner may run commands as slate-ci, never as root through this
#        rule, so the owner's sessions can build images and drive the
#        runner without a password. This is for Phases 0 to 3 of the plan;
#        Phase 2 replaces it with a rule for one controller command, and
#        Phase 3 checks that before the runner takes real jobs.
# 4. Checks all of the above, including that slate-ci cannot run anything
#    else as root.
#
# After a deliberate `brew upgrade softnet`, run this again to refresh the
# root-owned copy.
#
# Phase 2 additions (plan §4.1, §4.2):
# 5. Installs the controller, runnerctl and the admission hook sources
#    root-owned under /usr/local/libexec/slate-runner/bin, and the two
#    LaunchDaemon plists (controller, nightly warm rebuild) into
#    /Library/LaunchDaemons. Loading them is a separate, explicit step:
#      sudo launchctl bootstrap system /Library/LaunchDaemons/com.slate.mac-runner.plist
#      sudo launchctl bootstrap system /Library/LaunchDaemons/com.slate.mac-runner.warm-rebuild.plist
# 6. With NARROW=1, replaces the broad "owner may run anything as slate-ci"
#    rule by one that allows only runnerctl (Phase 3 step 4, before any real
#    job). Without NARROW the broad Phase 0 to 3 rule stays.

set -euo pipefail

RUNNER_USER="slate-ci"
RUNNER_HOME="/Users/${RUNNER_USER}"
LIBEXEC="/usr/local/libexec/slate-runner"
SOFTNET_DST="${LIBEXEC}/bin/softnet"
RUNNERCTL_DST="${LIBEXEC}/bin/runnerctl"
SUDOERS_FILE="/etc/sudoers.d/slate-runner"
REPO_TREE="$(cd "$(dirname "$0")/.." && pwd)"   # ci/mac-runner in the owner's checkout

die() { echo "error: $*" >&2; exit 1; }
step() { echo; echo "==> $*"; }

[[ $EUID -eq 0 ]] || die "run this with sudo"
OWNER="${SUDO_USER:-}"
[[ -n "$OWNER" && "$OWNER" != "root" ]] \
  || die "run this with sudo from the owner's own account, not from a root shell"

# --- 1. The account ---------------------------------------------------------
step "Account ${RUNNER_USER}"
if id -u "$RUNNER_USER" >/dev/null 2>&1; then
  echo "already exists"
else
  # No admin credentials are passed, so sysadminctl warns that the account
  # cannot unlock FileVault. That is intended: only the owner unlocks the
  # disk at boot.
  sysadminctl -addUser "$RUNNER_USER" -fullName "Slate CI runner" \
    -password "$(openssl rand -base64 32)" -home "$RUNNER_HOME"
  id -u "$RUNNER_USER" >/dev/null 2>&1 || die "sysadminctl did not create ${RUNNER_USER}"
  echo "created"
fi

if dseditgroup -o checkmember -m "$RUNNER_USER" admin >/dev/null 2>&1; then
  die "${RUNNER_USER} is in the admin group; it must be a standard account"
fi

[[ -d "$RUNNER_HOME" ]] || createhomedir -c -u "$RUNNER_USER" >/dev/null
[[ -d "$RUNNER_HOME" ]] || die "home directory ${RUNNER_HOME} is missing"
chown "${RUNNER_USER}:staff" "$RUNNER_HOME"
chmod 700 "$RUNNER_HOME"
dscl . -create "/Users/${RUNNER_USER}" IsHidden 1
echo "standard account, home ${RUNNER_HOME} (mode 700), hidden from the login window"

# --- 2. Root-owned Softnet --------------------------------------------------
step "Root-owned Softnet copy"
# Source: SOFTNET_SOURCE if given; otherwise the newest of Homebrew's binary
# and any verified release under slate-ci's cache (softnet-<ver>/softnet).
# Never downgrades an installed copy unless FORCE_SOFTNET=1: on 2026-10-10 a
# plain re-run put Homebrew's 0.24.0 back over the 0.24.1 that fixes the
# inbound-drop bug (openai/softnet#213).
softnet_version() { "$1" --version 2>/dev/null | awk '{print $2}' | cut -d- -f1; }
if [[ -n "${SOFTNET_SOURCE:-}" ]]; then
  [[ -f "$SOFTNET_SOURCE" ]] || die "SOFTNET_SOURCE=$SOFTNET_SOURCE is not a file"
  SOFTNET_SRC="$(realpath "$SOFTNET_SOURCE")"
else
  SOFTNET_SRC=""
  for cand in /opt/homebrew/bin/softnet "$RUNNER_HOME"/.slate-runner/cache/softnet-*/softnet; do
    [[ -x "$cand" ]] || continue
    if [[ -z "$SOFTNET_SRC" ]] || [[ "$(printf '%s\n%s\n' "$(softnet_version "$SOFTNET_SRC")" "$(softnet_version "$cand")" | sort -V | tail -1)" == "$(softnet_version "$cand")" ]]; then
      SOFTNET_SRC="$(realpath "$cand")"
    fi
  done
  [[ -n "$SOFTNET_SRC" ]] || die "Softnet is not installed (brew install openai/tools/softnet)"
fi
src_ver="$(softnet_version "$SOFTNET_SRC")"
echo "source: $SOFTNET_SRC (${src_ver:-version unknown})"
if [[ -x "$SOFTNET_DST" ]]; then
  dst_ver="$(softnet_version "$SOFTNET_DST")"
  newest="$(printf '%s\n%s\n' "$dst_ver" "$src_ver" | sort -V | tail -1)"
  if [[ "$newest" == "$dst_ver" && "$dst_ver" != "$src_ver" && "${FORCE_SOFTNET:-0}" != 1 ]]; then
    echo "installed copy is $dst_ver, newer than the source; keeping it (FORCE_SOFTNET=1 to downgrade)"
    SOFTNET_SRC="$SOFTNET_DST"
  fi
fi

for d in /usr/local/libexec "$LIBEXEC" "${LIBEXEC}/bin"; do
  mkdir -p "$d"
  chown root:wheel "$d"
  chmod 755 "$d"
done

# Every directory on the way to the copy must be root-owned and writable by
# root alone, or someone else could replace the copy.
for d in / /usr /usr/local /usr/local/libexec "$LIBEXEC" "${LIBEXEC}/bin"; do
  [[ "$(stat -f '%u' "$d")" == "0" ]] || die "$d is not owned by root"
  perm="$(stat -f '%Lp' "$d")"
  (( (8#$perm & 8#022) == 0 )) || die "$d is writable by group or others (mode $perm)"
done

install -o root -g wheel -m 755 "$SOFTNET_SRC" "$SOFTNET_DST"
"$SOFTNET_DST" --help >/dev/null || die "the Softnet copy does not run"
echo "installed ${SOFTNET_DST} from ${SOFTNET_SRC}"

# --- 3. sudo rules ----------------------------------------------------------
step "sudo rules in ${SUDOERS_FILE}"
grep -Eq '^[#@]includedir /(private/)?etc/sudoers\.d' /etc/sudoers \
  || die "/etc/sudoers does not include /etc/sudoers.d"

tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT
if [[ "${NARROW:-0}" == 1 ]]; then
  owner_rule="${OWNER} ALL=(${RUNNER_USER}) NOPASSWD: ${RUNNERCTL_DST}"
  owner_note="# The owner may run runnerctl as slate-ci, and nothing else (Phase 3 step 4)."
else
  owner_rule="${OWNER} ALL=(${RUNNER_USER}) NOPASSWD: ALL"
  owner_note="# The owner may act as slate-ci (Phases 0 to 3 only; NARROW=1 replaces this). Never grants root."
fi
cat >"$tmp" <<EOF
# Self-hosted mac runner: docs/plans/42_self_hosted_mac_runner_plan.md §4.1.
# slate-ci may start the root-owned Softnet copy, and nothing else, as root.
# Softnet drops root once the VM's network interface is set up.
${RUNNER_USER} ALL=(root) NOPASSWD: ${SOFTNET_DST}
${owner_note}
${owner_rule}
EOF
visudo -cf "$tmp" >/dev/null || die "the new rules failed visudo's check; nothing was installed"
install -o root -g wheel -m 440 "$tmp" "$SUDOERS_FILE"
if ! visudo -c >/dev/null; then
  rm -f "$SUDOERS_FILE"
  die "sudo's configuration was invalid with the new file, so it was removed again"
fi
echo "installed"
if grep -Eq '^Defaults.*secure_path' /etc/sudoers; then
  echo "note: /etc/sudoers sets secure_path; Phase 0 must confirm Tart still finds the copy"
fi

# --- 4. Checks --------------------------------------------------------------
step "Checks"
sudo -u "$RUNNER_USER" -H sudo -n "$SOFTNET_DST" --help >/dev/null \
  || die "${RUNNER_USER} cannot start Softnet through sudo"
echo "ok: ${RUNNER_USER} can start Softnet as root without a password"

if sudo -u "$RUNNER_USER" -H sudo -n /usr/bin/true 2>/dev/null; then
  die "${RUNNER_USER} can run other commands as root; check ${SUDOERS_FILE}"
fi
echo "ok: ${RUNNER_USER} cannot run anything else as root"

# Read the owner's rules back instead of trying them: the password this
# terminal just cached would make a live test pass even without the rule.
if [[ "${NARROW:-0}" == 1 ]]; then
  sudo -l -U "$OWNER" | grep -Eq "\(${RUNNER_USER}\) NOPASSWD: ${RUNNERCTL_DST}" \
    || die "sudo does not list the runnerctl rule for ${OWNER}"
  sudo -l -U "$OWNER" | grep -Eq "\(${RUNNER_USER}\) NOPASSWD: ALL" \
    && die "the broad rule is still present"
  echo "ok: ${OWNER} can run only runnerctl as ${RUNNER_USER}"
else
  sudo -l -U "$OWNER" | grep -Eq "\(${RUNNER_USER}\) NOPASSWD: ALL" \
    || die "sudo does not list the rule letting ${OWNER} act as ${RUNNER_USER}"
  echo "ok: ${OWNER} can act as ${RUNNER_USER} without a password (broad rule; NARROW=1 later)"
fi

# --- 5. Controller, runnerctl, hook sources, LaunchDaemons -------------------
step "Controller files (root-owned) and LaunchDaemon plists"
for f in controller/controller.py controller/runnerctl.sh hook/admission.py hook/job-started.sh \
         controller/com.slate.mac-runner.plist controller/com.slate.mac-runner.warm-rebuild.plist; do
  [[ -f "$REPO_TREE/$f" ]] || die "missing $REPO_TREE/$f (run from the slate checkout)"
done
install -o root -g wheel -m 755 "$REPO_TREE/controller/controller.py" "${LIBEXEC}/bin/controller.py"
install -o root -g wheel -m 755 "$REPO_TREE/controller/runnerctl.sh"  "$RUNNERCTL_DST"
install -d -o root -g wheel -m 755 "${LIBEXEC}/hooks"
install -o root -g wheel -m 644 "$REPO_TREE/hook/admission.py"   "${LIBEXEC}/hooks/admission.py"
install -o root -g wheel -m 755 "$REPO_TREE/hook/job-started.sh" "${LIBEXEC}/hooks/job-started.sh"
/usr/bin/python3 -I -m py_compile "${LIBEXEC}/bin/controller.py" "${LIBEXEC}/hooks/admission.py"
bash -n "$RUNNERCTL_DST"
for p in com.slate.mac-runner com.slate.mac-runner.warm-rebuild; do
  plutil -lint "$REPO_TREE/controller/$p.plist" >/dev/null
  install -o root -g wheel -m 644 "$REPO_TREE/controller/$p.plist" "/Library/LaunchDaemons/$p.plist"
done
echo "installed controller.py, runnerctl, hooks/ under ${LIBEXEC}; plists in /Library/LaunchDaemons (not loaded)"
if launchctl print system/com.slate.mac-runner >/dev/null 2>&1; then
  echo "note: com.slate.mac-runner is loaded; restart it to pick up the new controller:"
  echo "      sudo launchctl kickstart -k system/com.slate.mac-runner"
fi

echo
echo "Done."
echo "  load the services (first time):"
echo "    sudo launchctl bootstrap system /Library/LaunchDaemons/com.slate.mac-runner.plist"
echo "    sudo launchctl bootstrap system /Library/LaunchDaemons/com.slate.mac-runner.warm-rebuild.plist"
echo "  drive it:  ci/mac-runner/host/runnerctl status"
