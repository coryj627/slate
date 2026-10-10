#!/bin/bash
# Roll a changed admission hook into the images without rebuilding the
# toolchain layer: stage 3e in place on slate-mac-toolchain, then a warm
# rebuild (about 4 minutes in all). Runs as slate-ci:
#
#   ci/mac-runner/host/run-as-slate-ci.sh bash /Users/slate-ci/.slate-runner/mac-runner/image/refresh-hook.sh
#
# build-warm.sh sets the controller's recycle flag, so the idle job VM is
# replaced with one from the new image.
set -euo pipefail
export PATH=/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin

state="$HOME/.slate-runner"
tree="${SLATE_RUNNER_TREE:-$state/mac-runner}"
cd "$tree/image"

export PKR_VAR_admin_password
PKR_VAR_admin_password="$(cat "$state/guest-admin-password")"

echo "== stage 3e: hook into slate-mac-toolchain (in place)"
packer init 03e-hook.pkr.hcl >/dev/null
packer build 03e-hook.pkr.hcl
unset PKR_VAR_admin_password   # build-warm.sh reads the file itself

echo "== warm layer"
bash "$tree/image/build-warm.sh"
