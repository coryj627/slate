#!/usr/bin/env bash
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Stale claude/* branches: a weekly report, and a delete only when asked
# (.github/workflows/branch-cleanup.yml runs this). The repository already
# deletes merged pull-request branches, so the claude/* branches that
# remain are unmerged work or never-reviewed WIP. Nothing here deletes a
# tip main does not already contain, a tip younger than
# BRANCH_MIN_AGE_DAYS, a branch an open pull request is based on or comes
# from (deleting a head closes its pull request), or a branch whose tip
# moved after it was classified. A listing or pull-request snapshot that
# fails, or a branch that cannot be read, compared or deleted, turns the
# run red instead of leaving a quiet report.
#
# Environment: REPO (owner/name); DELETE ('true' deletes, anything else
# only reports); GITHUB_STEP_SUMMARY (the table); BRANCH_MIN_AGE_DAYS
# (default 7). GitHub is reached only through the gh on PATH:
# ci/windows-runner/tests/BranchCleanup.Tests.ps1 puts a fake one first
# and pins every call. Besides gh it needs only bash, awk, date, mktemp and
# rm (gh's own --jq does the JSON), so the same test runs in Git Bash. Spec:
# docs/superpowers/specs/2026-10-11-windows-overflow-routing-design.md §10.

set -uo pipefail

# Workflow-command text: % first, then CR and LF, so the runner shows what was sent.
annotate() {
  local text=$2
  text=${text//'%'/'%25'}
  text=${text//$'\r'/'%0D'}
  text=${text//$'\n'/'%0A'}
  printf '::%s::%s\n' "$1" "$text"
}

# One summary-table row; every cell loses CR and LF and has its | escaped.
row() {
  local line='|' cell
  for cell in "$@"; do
    cell=${cell//$'\r'/}
    cell=${cell//$'\n'/}
    line+=" ${cell//'|'/'\|'} |"
  done
  printf '%s\n' "$line" >>"$GITHUB_STEP_SUMMARY"
}

# A branch name as one URL path segment: byte by byte (LC_ALL=C), unreserved
# characters kept, every other byte as %XX, so a / in the name is %2F.
urlencode() {
  local LC_ALL=C text=$1 encoded='' char i
  for ((i = 0; i < ${#text}; i++)); do
    char=${text:i:1}
    case $char in
      [A-Za-z0-9._~-]) encoded+=$char ;;
      *) printf -v char '%%%02X' "'$char"; encoded+=$char ;;
    esac
  done
  printf '%s' "$encoded"
}

# The run fails the same way whatever failed: an ::error:: (with gh's text,
# if any) and a red-run note under the table.
fail_run() {
  annotate error "$1${2:+: $2}"
  printf '\n%s; this run is red.\n' "$1" >>"$GITHUB_STEP_SUMMARY"
  exit 1
}

if [ -z "${GITHUB_STEP_SUMMARY:-}" ]; then
  annotate error "GITHUB_STEP_SUMMARY is not set; nothing was classified or deleted"
  exit 1
fi
[[ ${REPO:-} =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || fail_run "REPO is not owner/name; nothing was classified or deleted"
min_age_days=${BRANCH_MIN_AGE_DAYS:-7}
# 0 to 9999 days, no leading zero: bash arithmetic reads 08 as bad octal, and a
# huge value overflows the floor negative.
[[ $min_age_days =~ ^(0|[1-9][0-9]{0,3})$ ]] || fail_run "BRANCH_MIN_AGE_DAYS is not a whole number of days from 0 to 9999; nothing was classified or deleted"

errlog=$(mktemp) || fail_run "could not create a temporary file; nothing was classified or deleted"
trap 'rm -f "$errlog"' EXIT
# Why a read failed: its error text, or else the answer it gave.
why() {
  local text
  text=$(<"$errlog")
  printf '%s' "${text:-unexpected answer \"${1:0:200}\"}"
}

{
  echo "## claude/* branches ($(date -u +%Y-%m-%dT%H:%MZ), delete=${DELETE:-false}, minimum age $min_age_days days)"
  echo
  echo "| branch | sha | ahead of main | last commit | open PR as head | open PR as base | action |"
  echo "|---|---|---|---|---|---|---|"
} >>"$GITHUB_STEP_SUMMARY"

# A failed listing fails the run: an empty report must never read as a clean one.
if ! refs=$(gh api "repos/$REPO/git/matching-refs/heads/claude/?per_page=100" --paginate --jq '.[].ref' 2>"$errlog"); then
  fail_run "could not list the claude/* branches; nothing was classified or deleted" "$(why "$refs")"
fi
if [ -z "$refs" ]; then
  row '(none)' '' '' '' '' '' 'no claude/* branches'
  exit 0
fi

# One snapshot of the open pull requests before any branch is looked at, as
# tab-separated lines through gh's own --jq: "open<TAB>N", then N lines of
# "head<TAB>base". Every delete decision depends on it, so a snapshot that
# fails, is empty or malformed, is shorter than its own count, or may have
# been cut at the limit fails the run. (Without the count line, an empty
# answer would read as no open pull request.)
if ! prs=$(gh pr list --repo "$REPO" --state open --limit 1000 --json headRefName,baseRefName \
  --jq '"open\t\(length)", (.[] | "\(.headRefName)\t\(.baseRefName)")' 2>"$errlog"); then
  fail_run "could not read the open pull requests; nothing was classified or deleted" "$(why "$prs")"
fi
if ! awk -F'\t' '
    NR == 1 { if (NF != 2 || $1 != "open" || $2 !~ /^[0-9]+$/) bad = 1; n = $2; next }
    NF != 2 || $1 == "" || $2 == "" { bad = 1 }
    END { exit (bad || NR - 1 != n || n >= 1000) }' <<<"$prs" 2>"$errlog"; then
  fail_run "the open pull requests could not be read as one complete list; nothing was classified or deleted" "$(why "$prs")"
fi

tip_re='^([0-9a-f]{40}) ([0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z)$'
now=$(date -u +%s)
failures=0
while IFS= read -r ref <&3; do
  [ -n "$ref" ] || continue
  branch=${ref#refs/heads/}
  if [[ $ref != refs/heads/claude/* ]]; then
    annotate error "unexpected ref $ref; not classified"
    row "$ref" '?' '?' '?' '?' '?' 'skipped: not a claude/* branch'
    failures=$((failures + 1)); continue
  fi
  encoded=$(urlencode "$branch")

  # The tip as classified: its sha (compared with main, and re-read before any
  # delete) and its committer date (the age floor).
  if ! tip=$(gh api "repos/$REPO/branches/$encoded" --jq '.commit.sha + " " + .commit.commit.committer.date' 2>"$errlog") \
    || [[ ! $tip =~ $tip_re ]]; then
    annotate warning "could not read the last commit of $branch: $(why "$tip")"
    row "$branch" '?' '?' '?' '?' '?' 'skipped: last commit lookup failed'
    failures=$((failures + 1)); continue
  fi
  sha=${BASH_REMATCH[1]}
  last=${BASH_REMATCH[2]}
  if ! tip_epoch=$(date -u -d "$last" +%s 2>"$errlog") || [[ ! $tip_epoch =~ ^[0-9]+$ ]]; then
    annotate warning "could not read the last commit date of $branch: $(why "$tip_epoch")"
    row "$branch" "${sha:0:8}" '?' "$last" '?' '?' 'skipped: last commit lookup failed'
    failures=$((failures + 1)); continue
  fi

  # Compared by that sha, not by name: the commit main must contain is the one a delete removes.
  if ! ahead=$(gh api "repos/$REPO/compare/main...$sha?per_page=1" --jq '.ahead_by' 2>"$errlog") \
    || [[ ! $ahead =~ ^[0-9]+$ ]]; then
    annotate warning "could not compare $branch with main: $(why "$ahead")"
    row "$branch" "${sha:0:8}" '?' "$last" '?' '?' 'skipped: compare failed'
    failures=$((failures + 1)); continue
  fi

  young=''
  [ $((now - tip_epoch)) -ge $((min_age_days * 86400)) ] || young=1

  # Exact-name matches in the snapshot (the name reaches awk through the
  # environment, so no escape in it is interpreted).
  as_head=$(BRANCH=$branch awk -F'\t' 'NR > 1 && $1 == ENVIRON["BRANCH"] { n++ } END { print n + 0 }' <<<"$prs" 2>"$errlog")
  if [[ ! $as_head =~ ^[0-9]+$ ]]; then
    annotate warning "could not count the open pull requests from $branch: $(why "$as_head")"
    row "$branch" "${sha:0:8}" "$ahead" "$last" '?' '?' 'skipped: pull request lookup failed'
    failures=$((failures + 1)); continue
  fi
  as_base=$(BRANCH=$branch awk -F'\t' 'NR > 1 && $2 == ENVIRON["BRANCH"] { n++ } END { print n + 0 }' <<<"$prs" 2>"$errlog")
  if [[ ! $as_base =~ ^[0-9]+$ ]]; then
    annotate warning "could not count the open pull requests into $branch: $(why "$as_base")"
    row "$branch" "${sha:0:8}" "$ahead" "$last" "$as_head" '?' 'skipped: pull request lookup failed'
    failures=$((failures + 1)); continue
  fi

  # The one condition that allows a delete: main contains the tip, the tip is
  # old enough, and no open pull request uses the branch as head or as base.
  if [ "$ahead" = "0" ] && [ -z "$young" ] && [ "$as_head" = "0" ] && [ "$as_base" = "0" ]; then
    if [ "${DELETE:-false}" = "true" ]; then
      # The tip must still be the one classified: a push since then is never deleted unseen.
      if ! current=$(gh api "repos/$REPO/git/ref/heads/$encoded" --jq '.object.sha' 2>"$errlog") \
        || [[ ! $current =~ ^[0-9a-f]{40}$ ]]; then
        annotate warning "could not re-read the tip of $branch before deleting it: $(why "$current")"
        action='skipped: tip re-check failed'
        failures=$((failures + 1))
      elif [ "$current" != "$sha" ]; then
        annotate warning "$branch moved since classification (${sha:0:8} to ${current:0:8}); kept"
        action='kept: moved since classification'
        failures=$((failures + 1))
      elif gh api -X DELETE "repos/$REPO/git/refs/heads/$encoded" >/dev/null 2>"$errlog"; then
        action='deleted'
      else
        annotate warning "could not delete $branch: $(why '')"
        action='delete failed'
        failures=$((failures + 1))
      fi
    else
      action='would delete'
    fi
  else
    reasons=''
    [ "$ahead" = "0" ] || reasons="ahead $ahead"
    [ -z "$young" ] || reasons="${reasons:+$reasons, }too young"
    [ "$as_head" = "0" ] || reasons="${reasons:+$reasons, }open PR head"
    [ "$as_base" = "0" ] || reasons="${reasons:+$reasons, }open PR base"
    action="kept: $reasons"
  fi
  row "$branch" "${sha:0:8}" "$ahead" "$last" "$as_head" "$as_base" "$action"
  printf '%s: %s\n' "$branch" "$action"
done 3< <(printf '%s\n' "$refs")

[ "$failures" -eq 0 ] || fail_run "$failures branch(es) could not be classified or deleted"
