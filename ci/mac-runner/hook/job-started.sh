#!/bin/bash
# ACTIONS_RUNNER_HOOK_JOB_STARTED for the slate mac runner. Installed root-owned
# at /usr/local/slate-runner/hooks/ in the guest (plan §3.2 control 5).
# All logic lives in admission.py next to this file; a non-zero exit here
# fails the job before any workflow step runs. Fail closed on any error.
set -u
dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -r "$dir/admission.py" ]; then
  /usr/bin/python3 -I "$dir/admission.py"
  rc=$?
else
  echo "::error::slate mac runner admission: hook implementation missing"
  rc=1
fi
[ "$rc" -eq 0 ] && exit 0

# A failed hook marks the job failed, but GitHub still runs steps guarded by
# if: always() or if: failure() (Phase 3 live test, 2026-10-10). Nothing from
# a refused job may run at all, so end the runner here: the worker dies with
# this job, the single-use listener exits, and the controller destroys the VM.
echo "::error::slate mac runner admission: refused; stopping the runner so no step runs"
me="$(id -un)"
pkill -TERM -u "$me" -f 'bin/Runner.Listener' 2>/dev/null || true
pkill -TERM -u "$me" -f 'bin/Runner.Worker' 2>/dev/null || true
exit 1
