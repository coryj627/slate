#!/bin/bash
# Write the repository's workflow execution policy from ci/mac-runner/allowlist.json.
# docs/plans/42_self_hosted_mac_runner_plan.md §3.2 control 2 and §3.6.
#
# The policy allows only the allow-list's actors and only its policy_events,
# for every workflow (D3). Idempotent: updates the policy of this name if it
# exists, creates it otherwise. Needs the owner's gh login (repo admin).
#
#   ci/mac-runner/github/apply-policy.sh            # show the plan and the current policies
#   ci/mac-runner/github/apply-policy.sh --apply    # write it
set -euo pipefail

here="$(cd "$(dirname "$0")/.." && pwd)"
allowlist="$here/allowlist.json"
POLICY_NAME="slate mac runner: owner-only workflow execution"
REPO="$(/usr/bin/python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["repository"])' "$allowlist")"

body="$(/usr/bin/python3 -I - "$allowlist" "$POLICY_NAME" <<'PY'
import json, sys
data = json.load(open(sys.argv[1]))
actors = [{"id": int(a["id"]), "type": a.get("type", "User")} for a in data["actors"]]
events = list(data["policy_events"])
policy = {
    "name": sys.argv[2],
    "enforcement": "active",
    "rules": [
        {"type": "restrict_actions_actors", "parameters": {"allowed_actors": actors}},
        {"type": "restrict_action_events", "parameters": {"allowed_events": events}},
    ],
}
print(json.dumps(policy, indent=2))
PY
)"

echo "== repository: $REPO"
echo "== policy to apply:"
echo "$body"
echo
echo "== current policies:"
existing="$(gh api "repos/$REPO/actions/policies" 2>/dev/null || echo '{"policies":[]}')"
printf '%s' "$existing" | /usr/bin/python3 -I -c '
import json, sys
d = json.load(sys.stdin)
for p in d.get("policies", []):
    print("  id={} name={!r} enforcement={}".format(p.get("id"), p.get("name"), p.get("enforcement")))
if not d.get("policies"):
    print("  (none)")'

if [ "${1:-}" != "--apply" ]; then
  echo
  echo "dry run; pass --apply to write the policy"
  exit 0
fi

policy_id="$(printf '%s' "$existing" | /usr/bin/python3 -I -c '
import json, sys
d = json.load(sys.stdin)
for p in d.get("policies", []):
    if p.get("name") == sys.argv[1]:
        print(p["id"]); break' "$POLICY_NAME")"

if [ -n "$policy_id" ]; then
  echo "== updating policy $policy_id"
  printf '%s' "$body" | gh api -X PUT "repos/$REPO/actions/policies/$policy_id" --input - >/dev/null
else
  echo "== creating policy"
  printf '%s' "$body" | gh api -X POST "repos/$REPO/actions/policies" --input - >/dev/null
fi

echo "== policies now:"
# The list omits rules; fetch each policy for them.
for id in $(gh api "repos/$REPO/actions/policies" --jq '.policies[].id'); do
  gh api "repos/$REPO/actions/policies/$id" --jq '"  id=\(.id) name=\(.name) enforcement=\(.enforcement) rules=\([.rules[]?.type] | join(","))"'
done
