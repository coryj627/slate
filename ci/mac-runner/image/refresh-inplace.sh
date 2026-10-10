#!/bin/bash
# Apply one or more in-place stages to slate-mac-toolchain, then rebuild the
# warm layer so jobs get the result. About 4 to 6 minutes, against 12 for a
# full toolchain rebuild. Runs as slate-ci:
#
#   ci/mac-runner/host/run-as-slate-ci.sh bash \
#     /Users/slate-ci/.slate-runner/mac-runner/image/refresh-inplace.sh 03f-python.pkr.hcl
#
# In-place stages: 03d-analyzer, 03e-hook, 03f-python. Fresh toolchain builds
# include all of them already. build-warm.sh sets the controller's recycle
# flag, so the idle job VM is replaced from the new image.
set -euo pipefail
export PATH=/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin
[ $# -ge 1 ] || { echo "usage: $0 <stage.pkr.hcl> [more...]" >&2; exit 2; }

state="$HOME/.slate-runner"
tree="${SLATE_RUNNER_TREE:-$state/mac-runner}"
cd "$tree/image"

export PKR_VAR_admin_password
PKR_VAR_admin_password="$(cat "$state/guest-admin-password")"

for stage in "$@"; do
  echo "== $stage on slate-mac-toolchain (in place)"
  packer init "$stage" >/dev/null
  packer build "$stage"
done
unset PKR_VAR_admin_password   # build-warm.sh reads the file itself

echo "== warm layer"
bash "$tree/image/build-warm.sh"
