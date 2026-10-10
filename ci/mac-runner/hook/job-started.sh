#!/bin/bash
# ACTIONS_RUNNER_HOOK_JOB_STARTED for the slate mac runner. Installed root-owned
# at /usr/local/slate-runner/hooks/ in the guest (plan §3.2 control 5).
# All logic lives in admission.py next to this file; a non-zero exit here
# fails the job before any workflow step runs. Fail closed on any error.
set -u
dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ ! -r "$dir/admission.py" ]; then
  echo "::error::slate mac runner admission: hook implementation missing"
  exit 1
fi
exec /usr/bin/python3 -I "$dir/admission.py"
