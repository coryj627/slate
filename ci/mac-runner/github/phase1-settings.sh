#!/bin/bash
# Phase 1 of docs/plans/42_self_hosted_mac_runner_plan.md: the GitHub-side
# settings, applied by the owner with their own gh login (repo admin).
#
#   ci/mac-runner/github/phase1-settings.sh            # show current state and the plan
#   ci/mac-runner/github/phase1-settings.sh --apply    # apply every setting below
#
# What --apply changes (each is read back afterwards):
#   1. Fork PR approval -> all_external_contributors        (§3.2 control 3)
#   2. Actions permissions: require full-SHA pinned actions  (Phase 1)
#   3. Repository variables MAC_RUNNER_MODE=namespace and MAC_RUNNER_HEARTBEAT=0 (§6.1)
#   4. The workflow execution policy from allowlist.json     (§3.2 control 2, via apply-policy.sh)
# What it only checks: PR creation limited to collaborators (control 1).
# What it cannot do: create the fine-grained token; it prints the recipe.
set -euo pipefail

here="$(cd "$(dirname "$0")/.." && pwd)"
REPO="$(/usr/bin/python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["repository"])' "$here/allowlist.json")"
apply=0; [ "${1:-}" = "--apply" ] && apply=1

say() { printf '\n== %s\n' "$*"; }

say "repository $REPO as $(gh api user --jq .login)"
gh api "repos/$REPO" --jq '"  visibility=\(.visibility) pull_request_creation_policy=\(.pull_request_creation_policy // "unset")"'
policy="$(gh api "repos/$REPO" --jq '.pull_request_creation_policy // ""')"
if [ "$policy" != "collaborators_only" ]; then
  echo "  WARNING: control 1 is off; set Settings > General > Features > Restrict pull requests to collaborators"
else
  echo "  ok: only collaborators can open pull requests (control 1)"
fi

say "1. fork pull request approval"
gh api "repos/$REPO/actions/permissions/fork-pr-contributor-approval" --jq '"  current: \(.approval_policy)"'
if [ $apply = 1 ]; then
  gh api -X PUT "repos/$REPO/actions/permissions/fork-pr-contributor-approval" -f approval_policy=all_external_contributors >/dev/null
  gh api "repos/$REPO/actions/permissions/fork-pr-contributor-approval" --jq '"  now:     \(.approval_policy)"'
else
  echo "  plan:    all_external_contributors"
fi

say "2. actions permissions (SHA pinning)"
gh api "repos/$REPO/actions/permissions" --jq '"  current: enabled=\(.enabled) allowed_actions=\(.allowed_actions) sha_pinning_required=\(.sha_pinning_required)"'
if [ $apply = 1 ]; then
  allowed="$(gh api "repos/$REPO/actions/permissions" --jq .allowed_actions)"
  gh api -X PUT "repos/$REPO/actions/permissions" -F enabled=true -f "allowed_actions=$allowed" -F sha_pinning_required=true >/dev/null
  gh api "repos/$REPO/actions/permissions" --jq '"  now:     enabled=\(.enabled) allowed_actions=\(.allowed_actions) sha_pinning_required=\(.sha_pinning_required)"'
else
  echo "  plan:    sha_pinning_required=true (every workflow already pins)"
fi

say "3. repository variables"
gh api "repos/$REPO/actions/variables" --jq '.variables[] | "  current: \(.name)=\(.value)"' || true
for pair in "MAC_RUNNER_MODE=namespace" "MAC_RUNNER_HEARTBEAT=0"; do
  name="${pair%%=*}"; value="${pair#*=}"
  if [ $apply = 1 ]; then
    if gh api "repos/$REPO/actions/variables/$name" >/dev/null 2>&1; then
      echo "  keep:    $name (exists)"
    else
      gh api -X POST "repos/$REPO/actions/variables" -f name="$name" -f value="$value" >/dev/null
      echo "  created: $name=$value"
    fi
  else
    echo "  plan:    $name=$value (created only if missing)"
  fi
done

say "4. workflow execution policy"
if [ $apply = 1 ]; then
  "$here/github/apply-policy.sh" --apply
else
  "$here/github/apply-policy.sh" | sed -n '/current policies/,$p'
  echo "  plan:    apply-policy.sh --apply"
fi

say "5. the controller's token (manual, in the browser)"
cat <<EOF
  https://github.com/settings/personal-access-tokens/new
    Token name:        slate-mac-runner controller
    Resource owner:    coryj627
    Expiration:        90 days
    Repository access: Only select repositories -> coryj627/slate
    Permissions:       Administration: Read and write   (JIT runner registration)
                       Variables:      Read and write   (heartbeat)
                       Metadata:       Read             (added automatically)
  Then store it where only slate-ci can read it (paste, Enter, Ctrl-D):
    sudo -n -u slate-ci -H bash -c 'umask 077; mkdir -p ~/.slate-runner; cat > ~/.slate-runner/github-token'
  Check without printing it:
    sudo -n -u slate-ci -H bash -c 'ls -l ~/.slate-runner/github-token; curl -fsS -H "Authorization: Bearer \$(cat ~/.slate-runner/github-token)" https://api.github.com/repos/$REPO/actions/runners | head -c 200'
EOF

if [ $apply = 0 ]; then
  echo
  echo "dry run; pass --apply to change settings 1 to 4"
fi
