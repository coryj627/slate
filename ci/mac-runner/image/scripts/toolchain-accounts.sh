#!/bin/bash
# Toolchain layer: the guest's run-time accounts (plan §4.4).
#   runner  standard, auto-logged in; runs the hook and the job
#   builder standard, SSH key from the host; runs the nightly warm build
# admin keeps provisioning; its sudo is tightened in stage 3b.
# Runs inside the guest as admin (build-time passwordless sudo).
set -euo pipefail

create_user() {
  local name="$1" full="$2" password="$3"
  if id -u "$name" >/dev/null 2>&1; then
    echo "$name exists"
  else
    sudo sysadminctl -addUser "$name" -fullName "$full" -password "$password" -home "/Users/$name" 2>&1 | grep -v -i "secure token" || true
    sudo createhomedir -c -u "$name" >/dev/null 2>&1 || true
  fi
  id -u "$name" >/dev/null
  if dseditgroup -o checkmember -m "$name" admin >/dev/null 2>&1; then
    echo "$name must not be an admin" >&2
    exit 1
  fi
  sudo chmod 750 "/Users/$name"
}

echo "==> Accounts"
create_user runner  "Slate CI runner"  "$RUNNER_PASSWORD"
create_user builder "Slate CI builder" "$BUILDER_PASSWORD"

echo "==> builder: SSH key only"
sudo install -d -o builder -g staff -m 700 /Users/builder/.ssh
printf '%s\n' "$BUILDER_PUBKEY" | sudo tee /Users/builder/.ssh/authorized_keys >/dev/null
sudo chown builder:staff /Users/builder/.ssh/authorized_keys
sudo chmod 600 /Users/builder/.ssh/authorized_keys

echo "==> Remote Login: admin and builder only"
# With the access group absent, Remote Login admits every account.
sudo dseditgroup -o create -q com.apple.access_ssh 2>/dev/null || true
sudo dseditgroup -o edit -a admin   -t user com.apple.access_ssh
sudo dseditgroup -o edit -a builder -t user com.apple.access_ssh
dseditgroup -o checkmember -m runner com.apple.access_ssh >/dev/null 2>&1 && { echo "runner must not have SSH" >&2; exit 1; }
sudo systemsetup -setremotelogin on >/dev/null 2>&1 || true

echo "==> Auto-login as runner"
# sysadminctl -autologin set fails with error 22 for a never-logged-in account,
# so write /etc/kcpassword directly, the way GitHub's macOS images do
# (actions/runner-images, bootstrap-provisioner/kcpassword.py, after Tim
# Sutton's osx-vm-templates, MIT). The password goes in on stdin, not argv.
printf '%s' "$RUNNER_PASSWORD" | sudo /usr/bin/python3 -I -c '
import os, sys
key = [125, 137, 82, 35, 210, 188, 221, 234, 163, 185, 31]
pw = list(sys.stdin.buffer.read())
r = len(pw) % len(key)
if len(pw) == len(key):
    pw += [0]
elif r:
    pw += [0] * (len(key) - r)
out = bytes(b ^ key[i % len(key)] for i, b in enumerate(pw))
fd = os.open("/etc/kcpassword", os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
os.write(fd, out)
os.close(fd)
'
sudo defaults write /Library/Preferences/com.apple.loginwindow autoLoginUser runner
sudo defaults write /Library/Preferences/com.apple.loginwindow autoLoginUserScreenLocked -bool false
echo "    autoLoginUser=$(sudo defaults read /Library/Preferences/com.apple.loginwindow autoLoginUser) kcpassword=$(sudo stat -f '%Sp %z bytes' /etc/kcpassword)"
# Per-user screensaver off for runner (admin's was set in the base layer).
sudo -u runner defaults -currentHost write com.apple.screensaver idleTime -int 0

echo "==> Directories"
sudo install -d -o root   -g wheel -m 755 /usr/local/slate-runner /usr/local/slate-runner/bin /usr/local/slate-runner/hooks
sudo install -d -o runner -g staff -m 755 /Users/runner/toolchains

echo "==> Check"
for u in runner builder; do
  printf '%-8s uid=%s admin=%s ssh=%s\n' "$u" "$(id -u $u)" \
    "$(dseditgroup -o checkmember -m $u admin >/dev/null 2>&1 && echo yes || echo no)" \
    "$(dseditgroup -o checkmember -m $u com.apple.access_ssh >/dev/null 2>&1 && echo yes || echo no)"
done
