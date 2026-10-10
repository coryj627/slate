#!/bin/bash
# Phase 3 step 2 of docs/plans/42_self_hosted_mac_runner_plan.md: prove the
# admission hook live. Runs on the host as the owner (uses the broad
# Phase 0 to 3 sudo rule and the owner's gh login).
#
#   phase3-hook-livetest.sh deny  <pair-id>   swap the controller's cached allow-list for one
#                                             naming nobody real, replace the idle VM, dispatch
#                                             the pilot to self-hosted-tart, print the run URL
#   phase3-hook-livetest.sh restore           put the real allow-list back and replace the idle VM
#   phase3-hook-livetest.sh check <run-id>    show each mac job's steps: after a refusal, nothing
#                                             but "Set up job" and "Set up runner" may have run
#
# Why the cache: until ci/mac-runner/allowlist.json is on main the controller
# serves its cached copy, so the swap is what the next job VM receives.
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
state=/Users/slate-ci/.slate-runner/state
BRANCH="${BRANCH:-ci/mac-runner-phase2}"
as_ci() { sudo -n -u slate-ci -H "$@"; }

wait_for_new_runner() {
  local old="$1" vm="" n=0
  echo "waiting for the replacement VM to register..."
  for _ in $(seq 1 50); do
    sleep 3
    vm="$(as_ci env PATH=/opt/homebrew/bin:/usr/bin:/bin tart list 2>/dev/null | awk '/ job-/ {print $2}' | head -1)"
    [ -n "$vm" ] && [ "$vm" != "$old" ] || continue
    n="$(gh api repos/coryj627/slate/actions/runners --jq "[.runners[] | select(.name == \"$vm\" and .status == \"online\")] | length")"
    [ "$n" = 1 ] && { echo "online: $vm"; return 0; }
  done
  echo "no replacement runner came online" >&2; return 1
}

case "${1:-}" in
  deny)
    pair="${2:?pair id, e.g. mac-pair-studio-hooktest-2}"
    old="$(as_ci env PATH=/opt/homebrew/bin:/usr/bin:/bin tart list 2>/dev/null | awk '/ job-/ {print $2}' | head -1)"
    as_ci test -f "$state/allowlist.real.json" || as_ci cp "$state/allowlist.json" "$state/allowlist.real.json"
    /usr/bin/python3 -I - "$here/allowlist.json" <<'PY' | as_ci bash -c 'cat > ~/.slate-runner/state/allowlist.json && chmod 644 ~/.slate-runner/state/allowlist.json'
import json, sys
d = json.load(open(sys.argv[1]))
d["actors"] = [{"id": 1, "login": "ghost", "type": "User", "note": "hook live test: nobody real"}]
json.dump(d, sys.stdout, indent=2)
PY
    echo "cache now allows only 'ghost'"
    "$here/host/runnerctl" recycle
    wait_for_new_runner "$old"
    gh workflow run mac-ci-pilot.yml --ref "$BRANCH" -f runner=self-hosted-tart -f pair_id="$pair"
    sleep 8
    gh run list --workflow mac-ci-pilot.yml --branch "$BRANCH" --limit 1 --json databaseId,url --jq '.[0] | "run \(.databaseId) \(.url)"'
    echo "then: $0 check <run-id>   and afterwards   $0 restore"
    ;;
  restore)
    as_ci cp "$state/allowlist.real.json" "$state/allowlist.json"
    as_ci /usr/bin/python3 -I -c 'import json; d=json.load(open("/Users/slate-ci/.slate-runner/state/allowlist.json")); print("cache restored; actors:", [a["login"] for a in d["actors"]])'
    "$here/host/runnerctl" recycle
    ;;
  check)
    run="${2:?run id}"
    gh run view "$run" --json status,conclusion,jobs --jq '"run \(.status) \(.conclusion // "-")", (.jobs[] | select(.name | test("self-hosted-tart")) | "  job \(.name): \(.status) \(.conclusion // "-")", (.steps[]? | select(.conclusion != null and .conclusion != "skipped") | "      step \(.name): \(.conclusion)"))'
    echo
    echo "pass = every mac job shows only 'Set up job' (success) and 'Set up runner' (failure), nothing after"
    ;;
  *) sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'; exit 2 ;;
esac
