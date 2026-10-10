#!/bin/bash
# Base-layer settings, run inside the guest as admin over SSH by Packer
# (01-provision.pkr.hcl). Nothing here is job-specific.
set -euo pipefail

echo "==> Sleep, display and screensaver off"
sudo pmset -a sleep 0 displaysleep 0 disksleep 0 standby 0 autopoweroff 0 || true
sudo systemsetup -setsleep Off 2>/dev/null || true
sudo systemsetup -setcomputersleep Off 2>/dev/null || true
sudo systemsetup -setdisplaysleep Off 2>/dev/null || true
sudo systemsetup -setharddisksleep Off 2>/dev/null || true
sudo defaults write /Library/Preferences/com.apple.screensaver loginWindowIdleTime -int 0
defaults -currentHost write com.apple.screensaver idleTime -int 0
# Screen lock off for the logged-in admin (needs the account password).
sysadminctl -screenLock off -password "$ADMIN_PASSWORD" 2>/dev/null || true

echo "==> Spotlight off"
sudo mdutil -a -i off >/dev/null

echo "==> Automatic software updates off"
sudo softwareupdate --schedule off >/dev/null 2>&1 || true
for key in AutomaticCheckEnabled AutomaticDownload AutomaticallyInstallMacOSUpdates ConfigDataInstall CriticalUpdateInstall; do
  sudo defaults write /Library/Preferences/com.apple.SoftwareUpdate "$key" -bool false
done
sudo defaults write /Library/Preferences/com.apple.commerce AutoUpdate -bool false

echo "==> Time zone UTC"
sudo systemsetup -settimezone UTC >/dev/null 2>&1 || true

echo "==> Public DNS (Softnet hands out the host as resolver, and jobs block the host)"
service="$(networksetup -listallnetworkservices | grep -v '^An asterisk' | head -1)"
sudo networksetup -setdnsservers "$service" 1.1.1.1 8.8.8.8
echo "    $service -> $(networksetup -getdnsservers "$service" | tr '\n' ' ')"

echo "==> Record"
{
  echo "built=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "macos=$(sw_vers -productVersion) ($(sw_vers -buildVersion))"
  echo "layer=base"
} | sudo tee /etc/slate-image-info >/dev/null
cat /etc/slate-image-info
