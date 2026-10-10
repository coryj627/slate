#!/bin/bash
# Run a command as slate-ci with the runner's PATH, after syncing this repo's
# ci/mac-runner tree into slate-ci's home. slate-ci cannot read the owner's
# checkout, so everything it runs is copied there first.
#
#   ci/mac-runner/host/run-as-slate-ci.sh <command> [args...]
#
# Relies on the Phase 0 to 3 sudoers rule from admin-setup.sh.

set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"   # ci/mac-runner
target_home=/Users/slate-ci
runner_path=/usr/local/libexec/slate-runner/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin

# A fresh copy swapped into place, so files deleted or renamed here disappear
# there too; extracting over the old tree would keep them, and the nightly
# plist runs whatever sits in that tree.
tar -C "$(dirname "$here")" --exclude '.DS_Store' -cf - mac-runner \
  | sudo -n -u slate-ci -H bash -c '
      set -e
      mkdir -p "$HOME/.slate-runner" && cd "$HOME/.slate-runner"
      rm -rf mac-runner.new mac-runner.old
      mkdir mac-runner.new
      tar -C mac-runner.new -xf -
      chmod -R go-rwx mac-runner.new
      [ ! -d mac-runner ] || mv mac-runner mac-runner.old
      mv mac-runner.new/mac-runner mac-runner
      rm -rf mac-runner.new mac-runner.old'

cd /
exec sudo -n -u slate-ci -H env PATH="$runner_path" HOME="$target_home" SLATE_RUNNER_TREE="$target_home/.slate-runner/mac-runner" "$@"
