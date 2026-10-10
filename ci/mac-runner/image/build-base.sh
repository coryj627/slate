#!/bin/bash
# Build the slate-mac-base layer (plan §4.5): Apple's IPSW -> first boot and
# base settings -> SIP off. Runs as slate-ci:
#
#   ci/mac-runner/host/run-as-slate-ci.sh bash "$SLATE_RUNNER_TREE"/image/build-base.sh
#
# or, already as slate-ci:  bash ~/.slate-runner/mac-runner/image/build-base.sh
#
# Environment:
#   IPSW              IPSW path, URL or "latest" (default "latest"); used only
#                     when slate-mac-vanilla is (re)created.
#   DISK_SIZE         guest disk in GB (default 80; plan §5.4 caps it near 80
#                     so one runaway job cannot blow the 90 GB budget).
#   REBUILD_VANILLA=1 delete slate-mac-vanilla and recreate it from the IPSW
#                     (needed to change DISK_SIZE; the recovery partition
#                     prevents growing an existing guest).
#   KEEP_STAGES=1     keep the intermediate stage VM on failure.
#
# The guest admin password is created once, letters and digits only, and kept
# at ~/.slate-runner/guest-admin-password (mode 600). It is typed into the
# recovery terminal by stage 2 and used by Packer's SSH, so it also appears
# briefly in process arguments on this host.
set -euo pipefail
export PATH=/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin

state="$HOME/.slate-runner"
tree="${SLATE_RUNNER_TREE:-$state/mac-runner}"
cd "$tree/image"

vm_exists() { tart list 2>/dev/null | awk 'NR>1 {print $2}' | grep -qx "$1"; }

umask 077
mkdir -p "$state"
pw_file="$state/guest-admin-password"
if [ ! -s "$pw_file" ]; then
  openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 32 > "$pw_file"
  echo "created $pw_file"
fi
# Packer reads PKR_VAR_<name> from the environment; -var would put the value
# on the command line, where ps shows it to every local account.
export PKR_VAR_admin_password
PKR_VAR_admin_password="$(cat "$pw_file")"
umask 022

DISK_SIZE="${DISK_SIZE:-80}"
echo "== stage 0: vanilla VM from the IPSW (${DISK_SIZE} GB disk)"
if vm_exists slate-mac-vanilla && [ "${REBUILD_VANILLA:-0}" = 1 ]; then
  vm_exists slate-mac-vanilla.prev && tart delete slate-mac-vanilla.prev
  tart rename slate-mac-vanilla slate-mac-vanilla.prev
fi
if vm_exists slate-mac-vanilla; then
  echo "slate-mac-vanilla exists ($(tart get slate-mac-vanilla 2>/dev/null | awk 'NR==2 {print $4}') GB); not recreating (REBUILD_VANILLA=1 to change)"
else
  tart create slate-mac-vanilla --from-ipsw "${IPSW:-latest}" --disk-format asif --disk-size "$DISK_SIZE"
fi

echo "== stage 1: first boot, admin account, base settings"
vm_exists slate-mac-base-stage1 && tart delete slate-mac-base-stage1
packer init 01-provision.pkr.hcl
PACKER_LOG="${PACKER_LOG:-0}" packer build 01-provision.pkr.hcl

echo "== stage 2: SIP off (recovery boot, typed over VNC)"
packer build 02-disable-sip.pkr.hcl

echo "== stage 2b: verify SIP is off"
packer build 02b-verify-sip.pkr.hcl

echo "== promote"
if vm_exists slate-mac-base; then
  vm_exists slate-mac-base.prev && tart delete slate-mac-base.prev
  tart rename slate-mac-base slate-mac-base.prev
fi
tart rename slate-mac-base-stage1 slate-mac-base
tart list
echo "== done: slate-mac-base"
