#!/bin/bash
# ACTIONS_RUNNER_HOOK_JOB_STARTED for the slate mac runner. Installed root-owned
# at /usr/local/slate-runner/hooks/ in the guest (plan §3.2 control 5).
# All logic lives in admission.py next to this file; a non-zero exit here
# fails the job before any workflow step runs. Fail closed on any error.
# The runner invokes this with `bash -e -o pipefail`; errexit would end the
# script at the first failing command, before the refusal handling below ran
# (Phase 3 live test, round 2). So: no errexit here.
set +e +o pipefail
set -u
dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -r "$dir/admission.py" ] && /usr/bin/python3 -I "$dir/admission.py"; then
  exit 0
fi
[ -r "$dir/admission.py" ] || echo "::error::slate mac runner admission: hook implementation missing"

# A failed hook marks the job failed, but GitHub still runs steps guarded by
# if: always() or if: failure() (Phase 3 live test, round 1). Nothing from a
# refused job may run at all, so end the runner here: the worker, which is
# this script's parent, dies with the job; the single-use listener then
# exits and the controller destroys the VM. SIGKILL, because the runner
# handles SIGTERM as "cancel the job", which still runs always() steps.
echo "::error::slate mac runner admission: refused; stopping the runner so no step runs"
me="$(id -un)"
listener="$(pgrep -u "$me" -f 'bin/Runner.Listener' 2>/dev/null | tr '\n' ' ')"
worker="$(pgrep -u "$me" -f 'bin/Runner.Worker' 2>/dev/null | tr '\n' ' ')"
echo "stopping listener [$listener] worker [$worker] parent [$PPID]"
# shellcheck disable=SC2086  # the pid lists are meant to split
kill -KILL $listener $worker "$PPID" 2>/dev/null
exit 1
