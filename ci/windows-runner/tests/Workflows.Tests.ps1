# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Text-level guards over the three workflows that read WINDOWS_RUNNER_MODE
# and over branch-cleanup.yml (the weekly claude/* branch report, whose
# delete step must stay fail-closed). YAML parsing happens on push
# (GitHub refuses a malformed workflow at the first run); these tests pin
# the routing and deletion contracts themselves, and the path filters
# that make windows-runner-tests.yml run them.

BeforeAll {
    $script:wf = Join-Path $PSScriptRoot '..' '..' '..' '.github' 'workflows'
    $script:windows = Get-Content -Raw (Join-Path $wf 'windows.yml')
    $script:nightly = Get-Content -Raw (Join-Path $wf 'nightly.yml')
    $script:pilot = Get-Content -Raw (Join-Path $wf 'windows-ci-pilot.yml')
    $script:lane = Get-Content -Raw (Join-Path $wf 'windows-runner-tests.yml')
}

Describe 'windows.yml pool switch' {
    It 'routes a Windows lane home only on home, and to today''s Namespace strings, verbatim, otherwise' {
        foreach ($pair in @(@('rust', 'slate-windows-rust'), @('app', 'slate-windows-app'), @('model', 'slate-windows-model'))) {
            $lane = $pair[0]; $tag = $pair[1]
            $expected = "runs-on: `${{ vars.WINDOWS_RUNNER_MODE == 'home' && 'slate-win-$lane' || format('namespace-profile-winx64-fast{0};overrides.cache-tag=$tag', github.ref != 'refs/heads/main' && '-pr' || '') }}"
            $windows.Contains($expected) | Should -BeTrue -Because "lane $lane must carry the exact expression"
        }
        $windows.Contains("runs-on: `${{ vars.WINDOWS_RUNNER_MODE == 'home' && 'slate-win-shell' || 'windows-latest' }}") | Should -BeTrue
    }
    It 'compares the variable with home only, so unset or unknown routes to Namespace/windows-latest' {
        # A run that cannot read repository variables (a fork or Dependabot
        # pull request) must never wait on a host that may be down.
        foreach ($text in $windows, $nightly) {
            ([regex]::Matches($text, "WINDOWS_RUNNER_MODE == 'namespace'")).Count | Should -Be 0
        }
        # windows.yml: four runs-on and three cache gates; nightly.yml: runs-on and cache.
        $reads = @([regex]::Matches($windows + "`n" + $nightly, "vars\.WINDOWS_RUNNER_MODE (==|!=) '([^']*)'"))
        $reads.Count | Should -Be 9
        foreach ($read in $reads) { $read.Groups[2].Value | Should -BeExactly 'home' }
    }
    It 'keeps no bare Namespace or windows-latest runs-on for the Windows lanes' {
        ([regex]::Matches($windows, 'runs-on: namespace-profile')).Count | Should -Be 0
        ([regex]::Matches($windows, 'runs-on: windows-latest')).Count | Should -Be 0
    }
    It 'gates all three Namespace cache mounts off the home pool' {
        ([regex]::Matches($windows, 'namespacelabs/nscloud-cache-action')).Count | Should -Be 3
        ([regex]::Matches($windows, [regex]::Escape("if: `${{ vars.WINDOWS_RUNNER_MODE != 'home' }}"))).Count | Should -Be 3
    }
    It 'documents the switch and its default in the header' {
        $windows | Should -Match 'WINDOWS_RUNNER_MODE'
        $windows | Should -Match 'self-hosted-windows-runner\.md'
        # Comment lines joined, so a wrapped sentence still matches.
        $joined = $windows -replace '\r?\n#\s*', ' '
        $joined | Should -Match ([regex]::Escape('`home` only when the variable is exactly `home`'))
        $joined | Should -Match ([regex]::Escape("anything else, including unset, is today's providers"))
    }
}

Describe 'nightly.yml pool switch' {
    It 'routes the Windows stress job home only on home, and restores the GitHub cache everywhere else' {
        $nightly.Contains("runs-on: `${{ vars.WINDOWS_RUNNER_MODE == 'home' && 'slate-win-app' || 'windows-latest' }}") | Should -BeTrue
        $nightly.Contains("cache: `${{ vars.WINDOWS_RUNNER_MODE != 'home' && 'true' || 'false' }}") | Should -BeTrue
    }
}

Describe 'windows-ci-pilot.yml home candidate' {
    It 'offers home and routes its three jobs to the lane labels' {
        $pilot | Should -Match 'options: \[hosted, namespace-8x16, namespace-4x8, home\]'
        ([regex]::Matches($pilot, "inputs\.runner == 'home' && 'slate-win-app'")).Count | Should -Be 1
        ([regex]::Matches($pilot, "inputs\.runner == 'home' && 'slate-win-model'")).Count | Should -Be 1
        ([regex]::Matches($pilot, "inputs\.runner == 'home' && 'slate-win-shell'")).Count | Should -Be 2
    }
    It 'records isolation evidence on home' {
        $pilot | Should -Match 'Isolation evidence'
        $pilot | Should -Match '10\.77\.0\.1'
        $pilot | Should -Match 'github\.com'
    }
    It 'times a home build out before the host reclaims the app-lane VM' {
        $pilot.Contains("timeout-minutes: `${{ inputs.runner == 'home' && 100 || 120 }}") | Should -BeTrue
    }
}

Describe 'windows-runner-tests.yml Pester pin' {
    It 'installs and imports one pinned Pester 5 before the suite runs' {
        $lane | Should -Match 'Install-Module Pester -RequiredVersion 5\.\d+\.\d+ '
        $lane | Should -Not -Match 'Install-Module Pester -MinimumVersion'
        $import = $lane.IndexOf('Import-Module Pester -RequiredVersion ')
        $import | Should -BeGreaterThan -1
        $import | Should -BeLessThan $lane.IndexOf('./ci/windows-runner/tests/Invoke-Tests.ps1')
        # One version throughout: the install, its presence check and the import.
        $pins = @([regex]::Matches($lane, "(?:-RequiredVersion |\[version\]')(\d+\.\d+\.\d+)") | ForEach-Object { $_.Groups[1].Value })
        $pins.Count | Should -BeGreaterOrEqual 3
        @($pins | Select-Object -Unique) | Should -Be @('5.9.1')
    }
    It 'Invoke-Tests.ps1 keeps the Pester 5 a caller imported, and otherwise loads the newest from 5.5.0' {
        $runner = Get-Content -Raw (Join-Path $PSScriptRoot 'Invoke-Tests.ps1')
        $runner | Should -Match ([regex]::Escape('Import-Module Pester -MinimumVersion 5.5.0 -Force'))
        $runner | Should -Match ([regex]::Escape("if (-not (Get-Module Pester | Where-Object { `$_.Version -ge [version]'5.5.0' }))"))
    }
}

Describe 'windows-runner-tests.yml path filters' {
    It 'runs these guards when a routed workflow changes, on pull requests and on main' {
        foreach ($routed in @('windows.yml', 'nightly.yml', 'windows-ci-pilot.yml', 'branch-cleanup.yml')) {
            ([regex]::Matches($lane, [regex]::Escape("- '.github/workflows/$routed'"))).Count | Should -Be 2 -Because "$routed must be in both the pull_request and push path filters"
        }
    }
}

Describe 'branch-cleanup.yml' {
    BeforeAll {
        $script:cleanup = Get-Content -Raw (Join-Path $wf 'branch-cleanup.yml')
        # Where one whole, live line of the workflow starts, or -1. A commented-out copy ("# ...") or a
        # fragment of a longer line does not count, so a pin cannot be met by text that no longer runs.
        # (One BeforeAll: Pester 5 keeps only the last of several in a block.)
        function Get-LineAt([string]$Text, [string]$Line) {
            $found = [regex]::Match($Text, '(?m)^[ \t]*' + [regex]::Escape($Line) + '[ \t]*\r?$')
            if ($found.Success) { $found.Index } else { -1 }
        }
    }
    It 'reports weekly and deletes only on an explicit dispatch, only claude/* tips main already contains' {
        $cleanup | Should -Match '(?m)^    - cron: ''0 6 \* \* 1'''
        $cleanup | Should -Match '(?m)^      delete:\r?\n        description:'
        $cleanup | Should -Match '(?m)^        type: boolean\r?\n        default: false'
        $cleanup | Should -Match '(?m)^  contents: write\r?\n  pull-requests: read'
        # DELETE is true only for a dispatch with the input set: this one live definition, and no other
        # (a commented-out copy above a live DELETE: 'true' would let the schedule delete).
        Get-LineAt $cleanup 'DELETE: ${{ github.event_name == ''workflow_dispatch'' && inputs.delete && ''true'' || ''false'' }}' | Should -BeGreaterThan -1
        ([regex]::Matches($cleanup, '(?m)^[ \t]*DELETE:')).Count | Should -Be 1
        $cleanup | Should -Not -Match '(?-i)\bDELETE='
        # Only claude/* names get past the prefix line (its neighbours are pinned in the order test below).
        Get-LineAt $cleanup '[[ "$branch" == claude/* ]] || { echo "::error::unexpected branch name $branch"; failures=$((failures + 1)); continue; }' | Should -BeGreaterThan -1
        $cleanup | Should -Not -Match '(?i)(^|\s)--force\b'
    }
    It 'never deletes a branch an open pull request uses, as head or as base, and says why it kept each one' {
        # The one condition that guards the delete: ahead_by 0 and no open pull request on either side.
        Get-LineAt $cleanup 'if [ "$ahead" = "0" ] && [ "$as_head" = "0" ] && [ "$as_base" = "0" ]; then' | Should -BeGreaterThan -1
        # One delete call, behind that gate and inside the DELETE check (the order is pinned below).
        ([regex]::Matches($cleanup, '-X DELETE')).Count | Should -Be 1
        # The table names what happened to a branch, or why it was kept.
        Get-LineAt $cleanup 'action="deleted"' | Should -BeGreaterThan -1
        Get-LineAt $cleanup 'action="would delete"' | Should -BeGreaterThan -1
        Get-LineAt $cleanup '[ "$ahead" = "0" ] || reasons="ahead $ahead"' | Should -BeGreaterThan -1
        Get-LineAt $cleanup '[ "$as_head" = "0" ] || [ "$as_head" = "?" ] || reasons="${reasons:+$reasons, }open PR head"' | Should -BeGreaterThan -1
        Get-LineAt $cleanup '[ "$as_base" = "0" ] || [ "$as_base" = "?" ] || reasons="${reasons:+$reasons, }open PR base"' | Should -BeGreaterThan -1
    }
    It 'fails the run, never a quiet empty report, when the listing fails or any branch could not be handled' {
        # A failed listing: the bash form of core.setFailed (an ::error:: annotation and a non-zero exit).
        $cleanup | Should -Match '(?m)^[ \t]*refs=\$\(gh api "repos/\$REPO/git/matching-refs/heads/claude/" --paginate --jq ''\.\[\]\.ref''\) \|\| \{ echo "::error::[^"\r\n]*"; exit 1; \}[ \t]*\r?$'
        # One branch that cannot be handled warns, the loop goes on, and the step fails at the end.
        $cleanup | Should -Match '(?m)^[ \t]*\[ "\$failures" -eq 0 \] \|\| \{ echo "::error::[^"\r\n]*"; exit 1; \}[ \t]*\r?$'
    }
    It 'reads a failed open-pull-request lookup as an open pull request, never as none' {
        Get-LineAt $cleanup 'as_head=$(gh pr list --repo "$REPO" --state open --head "$branch" --json number --jq ''length'' 2>/dev/null || echo ''?'')' | Should -BeGreaterThan -1
        Get-LineAt $cleanup 'as_base=$(gh pr list --repo "$REPO" --state open --base "$branch" --json number --jq ''length'' 2>/dev/null || echo ''?'')' | Should -BeGreaterThan -1
        # Every gh pr list falls back to ?, which the gate reads as not-zero: a fallback of 0 would read as no open PR.
        $fallbacks = @([regex]::Matches($cleanup, "gh pr list[^\r\n]*\|\| echo '([^']*)'\)"))
        $fallbacks.Count | Should -Be 2
        foreach ($fallback in $fallbacks) { $fallback.Groups[1].Value | Should -BeExactly '?' }
        # Nothing else assigns them.
        ([regex]::Matches($cleanup, '\bas_head=')).Count | Should -Be 1
        ([regex]::Matches($cleanup, '\bas_base=')).Count | Should -Be 1
        # The failed lookup warns, is counted, and is named in the kept reason.
        Get-LineAt $cleanup 'if [ "$as_head" = "?" ] || [ "$as_base" = "?" ]; then' | Should -BeGreaterThan -1
        Get-LineAt $cleanup 'echo "::warning::could not look up the open pull requests of $branch"; lookup_failed=1; failures=$((failures + 1))' | Should -BeGreaterThan -1
        Get-LineAt $cleanup '[ -z "$lookup_failed" ] || reasons="${reasons:+$reasons, }open PR lookup failed"' | Should -BeGreaterThan -1
    }
    It 'reads a failed compare as a failure, never as ahead 0' {
        # The compare line through its failure handling: a fallback such as `|| echo 0` inside the command
        # substitution would turn a failed compare into 0 and leave the warn block as dead code.
        Get-LineAt $cleanup 'if [ -z "$encoded" ] || ! ahead=$(gh api "repos/$REPO/compare/main...$encoded?per_page=1" --jq ''.ahead_by'' 2>&1) || [[ ! "$ahead" =~ ^[0-9]+$ ]]; then' | Should -BeGreaterThan -1
        Get-LineAt $cleanup 'echo "::warning::could not compare $branch with main: ${ahead//$''\n''/ }"' | Should -BeGreaterThan -1
        Get-LineAt $cleanup 'echo "| $branch | ? | ? | ? | ? | skipped: compare failed |" >> "$GITHUB_STEP_SUMMARY"; failures=$((failures + 1)); continue' | Should -BeGreaterThan -1
        # Only the empty initialisation and the compare itself assign ahead.
        ([regex]::Matches($cleanup, '\bahead=')).Count | Should -Be 2
    }
    It 'says why a delete or a tip-date lookup failed, and counts both' {
        # The delete keeps its error text (no /dev/null), as the compare does.
        Get-LineAt $cleanup 'if out=$(gh api -X DELETE "repos/$REPO/git/refs/heads/$encoded" 2>&1); then' | Should -BeGreaterThan -1
        Get-LineAt $cleanup 'echo "::warning::could not delete $branch: ${out//$''\n''/ }"; action="delete failed"; failures=$((failures + 1))' | Should -BeGreaterThan -1
        $cleanup | Should -Not -Match 'DELETE[^\r\n]*/dev/null'
        # The tip date is a lookup like the others: a failure warns, is counted, and skips the branch (so it
        # cannot be deleted in a run whose lookups are failing) instead of becoming a quiet ?.
        Get-LineAt $cleanup 'if ! last=$(gh api "repos/$REPO/branches/$encoded" --jq ''.commit.commit.committer.date'' 2>&1) || [[ ! "$last" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T ]]; then' | Should -BeGreaterThan -1
        Get-LineAt $cleanup 'echo "::warning::could not read the last commit date of $branch: ${last//$''\n''/ }"' | Should -BeGreaterThan -1
        Get-LineAt $cleanup 'echo "| $branch | $ahead | ? | ? | ? | skipped: last commit lookup failed |" >> "$GITHUB_STEP_SUMMARY"; failures=$((failures + 1)); continue' | Should -BeGreaterThan -1
        ([regex]::Matches($cleanup, '\blast=')).Count | Should -Be 1
    }
    It 'looks at a branch in a safe order: name, compare, tip date, pull requests, gate, then the one delete' {
        # Whole, live lines (a commented-out copy has no index), each strictly after the one before.
        $order = [ordered]@{
            'the claude/ prefix check' = Get-LineAt $cleanup '[[ "$branch" == claude/* ]] || { echo "::error::unexpected branch name $branch"; failures=$((failures + 1)); continue; }'
            'the URL-encoded name'     = Get-LineAt $cleanup 'encoded=$(printf ''%s'' "$branch" | jq -sRr @uri)'
            'the compare'              = Get-LineAt $cleanup 'if [ -z "$encoded" ] || ! ahead=$(gh api "repos/$REPO/compare/main...$encoded?per_page=1" --jq ''.ahead_by'' 2>&1) || [[ ! "$ahead" =~ ^[0-9]+$ ]]; then'
            'the tip date'             = Get-LineAt $cleanup 'if ! last=$(gh api "repos/$REPO/branches/$encoded" --jq ''.commit.commit.committer.date'' 2>&1) || [[ ! "$last" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T ]]; then'
            'the head lookup'          = Get-LineAt $cleanup 'as_head=$(gh pr list --repo "$REPO" --state open --head "$branch" --json number --jq ''length'' 2>/dev/null || echo ''?'')'
            'the base lookup'          = Get-LineAt $cleanup 'as_base=$(gh pr list --repo "$REPO" --state open --base "$branch" --json number --jq ''length'' 2>/dev/null || echo ''?'')'
            'the lookup-failure check' = Get-LineAt $cleanup 'if [ "$as_head" = "?" ] || [ "$as_base" = "?" ]; then'
            'the gate'                 = Get-LineAt $cleanup 'if [ "$ahead" = "0" ] && [ "$as_head" = "0" ] && [ "$as_base" = "0" ]; then'
            'the DELETE check'         = Get-LineAt $cleanup 'if [ "$DELETE" = "true" ]; then'
            'the delete'               = Get-LineAt $cleanup 'if out=$(gh api -X DELETE "repos/$REPO/git/refs/heads/$encoded" 2>&1); then'
        }
        $previous = -1
        $previousName = 'the start of the script'
        foreach ($name in $order.Keys) {
            $order[$name] | Should -BeGreaterThan $previous -Because "$name must be a live line after $previousName"
            $previous = $order[$name]
            $previousName = $name
        }
    }
}
