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
    function Get-JobBlock([string]$Text, [string]$Job) {
        $start = $Text.IndexOf("`n  ${Job}:")
        if ($start -lt 0) { return '' }
        $rest = $Text.Substring($start + 1)
        $next = [regex]::Match($rest.Substring(1), '(?m)^(\S|  \S)')
        if ($next.Success) { return $rest.Substring(0, $next.Index + 1) }
        return $rest
    }
    $script:vendor = @{
        rust  = "format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-rust', github.ref != 'refs/heads/main' && '-pr' || '')"
        app   = "format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-app', github.ref != 'refs/heads/main' && '-pr' || '')"
        model = "format('namespace-profile-winx64-fast{0};overrides.cache-tag=slate-windows-model', github.ref != 'refs/heads/main' && '-pr' || '')"
    }
    function Get-Predicate([string]$Pool) { "(needs.route.outputs.attempt == github.run_attempt && needs.route.outputs.${Pool} == 'home')" }
    # One whole line at job-key indentation, so a step-level copy of it does not count.
    function Get-JobLine([string]$Line) { '(?m)^    ' + [regex]::Escape($Line) + '[ \t]*\r?$' }
    # The Namespace cache step: the predicate's negation as its `if`, then its `uses`, with only comment lines between.
    function Get-CacheGate([string]$Predicate) {
        '(?m)^        ' + [regex]::Escape("if: `${{ !$Predicate }}") + '[ \t]*\r?\n(?:[ \t]*#[^\n]*\n)*        uses: namespacelabs/nscloud-cache-action@'
    }
    # Consecutive whole lines, each verbatim.
    function Get-Lines([string[]]$Lines) { '(?m)^' + (($Lines | ForEach-Object { [regex]::Escape($_) }) -join '[ \t]*\r?\n') + '[ \t]*\r?$' }
    # The steps of a job block, each from its `      - ` to the next one or the end of the block.
    function Get-Steps([string]$Block) { @([regex]::Matches($Block, '(?ms)^      - .*?(?=^      - |\z)') | ForEach-Object { $_.Value }) }
}

Describe 'windows.yml routing through the router' {
    It 'has one routing job on hosted Linux that runs only on home or home-only, with both permissions, the sparse checkout, the token, the step id the outputs read, a five-minute timeout and continue-on-error on the job and both steps' {
        $route = Get-JobBlock $windows 'route'
        $route | Should -Not -BeNullOrEmpty
        $route | Should -Match 'name: Route Windows lanes'
        $route.Contains("if: `${{ vars.WINDOWS_RUNNER_MODE == 'home' || vars.WINDOWS_RUNNER_MODE == 'home-only' }}") | Should -BeTrue
        $route | Should -Match 'runs-on: ubuntu-latest'
        $route | Should -Match (Get-JobLine 'timeout-minutes: 5')
        # On the job, so a timeout or a lost runner cannot fail the run either; and on each of the two steps.
        $route | Should -Match (Get-JobLine 'continue-on-error: true') -Because 'step-level continue-on-error does not cover a job timeout or a lost runner'
        ([regex]::Matches($route, '(?m)^        continue-on-error: true[ \t]*\r?$')).Count | Should -Be 2
        $route | Should -Match '(?m)^\s+permissions:\r?\n\s+actions: read\r?\n\s+contents: read'
        $route | Should -Match 'sparse-checkout: ci/windows-runner/route'
        $route | Should -Match 'persist-credentials: false'
        foreach ($entry in 'GH_TOKEN: ${{ github.token }}', 'WINDOWS_RUNNER_MODE: ${{ vars.WINDOWS_RUNNER_MODE }}', 'WINDOWS_HOME_STATE: ${{ vars.WINDOWS_HOME_STATE }}',
            'ROUTE_LANES: rust,app,model-0,model-1', 'ROUTE_ATTEMPT: ${{ github.run_attempt }}', 'ROUTE_RUN_ID: ${{ github.run_id }}',
            'ROUTE_EVENT: ${{ github.event_name }}', 'ROUTE_HEAD_REPO: ${{ github.event.pull_request.head.repo.full_name }}', 'ROUTE_REPO: ${{ github.repository }}') {
            $route.Contains($entry) | Should -BeTrue -Because "the routing step needs $entry"
        }
        # Every output below reads steps.route: the id belongs to the step that runs the router, or every output is
        # empty and every lane takes the vendor without a word.
        $steps = Get-Steps $route
        $router = @($steps | Where-Object { $_ -match '(?m)^        run: python3 ci/windows-runner/route/route\.py[ \t]*\r?$' })
        $router.Count | Should -Be 1 -Because 'one step runs the router'
        $router[0] | Should -Match '(?m)^        id: route[ \t]*\r?$' -Because 'the step that runs the router is the one the outputs read'
        $checkout = @($steps | Where-Object { $_.Contains('sparse-checkout: ci/windows-runner/route') })
        $checkout.Count | Should -Be 1
        $checkout[0] | Should -Not -Match '(?m)^        id:' -Because 'the checkout step carries no id'
        foreach ($output in 'attempt', 'rust-pool', 'app-pool', 'model-0-pool', 'model-1-pool') {
            $route.Contains("      ${output}: `${{ steps.route.outputs.$output }}") | Should -BeTrue -Because "output $output"
        }
    }
    It 'every routed lane depends on route, runs when route was skipped or failed, and uses the router only in its own attempt with today''s vendor string as the fallback, verbatim' {
        foreach ($pair in @(@('rust-tests', 'rust', 'rust'), @('windows', 'app', 'app'))) {
            $block = Get-JobBlock $windows $pair[0]
            $block | Should -Match '(?m)^    needs: \[route\]'
            $block | Should -Match (Get-JobLine 'if: ${{ !cancelled() }}') -Because "lane $($pair[0]) must run when route was skipped or failed"
            $block.Contains("runs-on: `${{ $(Get-Predicate "$($pair[1])-pool") && 'slate-win-$($pair[1])' || $($vendor[$pair[2]]) }}") | Should -BeTrue -Because "lane $($pair[0])"
            $block | Should -Match (Get-CacheGate (Get-Predicate "$($pair[1])-pool")) -Because "the cache gate of $($pair[0])"
        }
        $shards = Get-JobBlock $windows 'windows-model-shard'
        $shards | Should -Match '(?m)^    needs: \[route\]'
        $shards | Should -Match (Get-JobLine 'if: ${{ !cancelled() }}') -Because 'the model shards must run when route was skipped or failed'
        $selector = "(needs.route.outputs.attempt == github.run_attempt && ((matrix.shard == 0 && needs.route.outputs.model-0-pool == 'home') || (matrix.shard == 1 && needs.route.outputs.model-1-pool == 'home')))"
        $shards.Contains("runs-on: `${{ $selector && 'slate-win-model' || $($vendor.model) }}") | Should -BeTrue
        $shards | Should -Match (Get-CacheGate $selector) -Because 'the cache gate of windows-model-shard'
        ([regex]::Matches($windows, 'namespacelabs/nscloud-cache-action')).Count | Should -Be 3
    }
    It 'runs the shell gate on windows-latest in every mode but home-only, and for a fork pull request even then, and only after a green app build whatever happened to route' {
        $flaui = Get-JobBlock $windows 'flaui'
        $flaui | Should -Match '(?m)^    runs-on: \$\{\{ .* \|\| ''windows-latest'' \}\}[ \t]*\r?$' -Because 'today''s provider is the fallback, verbatim'
        # home-only sends it home only for a run of this repository: a fork never goes home.
        $flaui | Should -Match (Get-JobLine "runs-on: `${{ (vars.WINDOWS_RUNNER_MODE == 'home-only' && (github.event_name != 'pull_request' || github.event.pull_request.head.repo.full_name == github.repository)) && 'slate-win-shell' || 'windows-latest' }}")
        $flaui | Should -Match (Get-JobLine "if: `${{ !cancelled() && needs.windows.result == 'success' }}")
    }
    It 'the gate waits on route too, runs whatever route did, and names the pools, the routing job''s result and the router''s attempt, quoted, when it fails' {
        $gate = Get-JobBlock $windows 'gate'
        $gate | Should -Match ([regex]::Escape('needs: [route, rust-tests, windows, windows-model, flaui]'))
        # Without always() the gate is skipped with a skipped route (every run outside home and home-only), and a
        # skipped required check counts as passing.
        $gate | Should -Match (Get-JobLine 'if: ${{ always() }}')
        # GitHub reports a continue-on-error job's result as it sees fit; an empty router-attempt='' is the sign the
        # router produced nothing and the lanes took the vendor.
        $gate.Contains('echo "::error::rust-tests=${{ needs.rust-tests.result }} app=${{ needs.windows.result }} model=${{ needs.windows-model.result }} flaui=${{ needs.flaui.result }} route=$ROUTE_RESULT router-attempt=''$ROUTER_ATTEMPT'' attempt=${{ github.run_attempt }} pools: rust=$RUST_POOL app=$APP_POOL model-0=$MODEL_0_POOL model-1=$MODEL_1_POOL"') | Should -BeTrue
        foreach ($entry in 'ROUTE_RESULT: ${{ needs.route.result }}', 'ROUTER_ATTEMPT: ${{ needs.route.outputs.attempt }}', 'RUST_POOL: ${{ needs.route.outputs.rust-pool }}',
            'APP_POOL: ${{ needs.route.outputs.app-pool }}', 'MODEL_0_POOL: ${{ needs.route.outputs.model-0-pool }}', 'MODEL_1_POOL: ${{ needs.route.outputs.model-1-pool }}') {
            $gate | Should -Match ('(?m)^          ' + [regex]::Escape($entry) + '[ \t]*\r?$') -Because "the gate reads $entry through its step's environment"
        }
    }
    It 'every routed lane prints its runner first, and the router''s attempt only through the environment' {
        $record = '    steps:', '      - name: Record the runner', '        env:', '          ROUTER_ATTEMPT: ${{ needs.route.outputs.attempt }}',
            '        run: Write-Host "runner=${{ runner.name }} environment=${{ runner.environment }} attempt=${{ github.run_attempt }} router-attempt=$env:ROUTER_ATTEMPT"'
        foreach ($job in 'rust-tests', 'windows', 'windows-model-shard') {
            (Get-JobBlock $windows $job) | Should -Match (Get-Lines $record) -Because "$job's first step names the effective pool"
        }
    }
    It 'never expands the routing job''s outputs or result inside a script: a fork''s route.py writes them' {
        # Each run: with its block, which runs while the following lines are indented deeper than the key.
        $runs = @([regex]::Matches($windows, '(?m)^(?<indent>[ ]+)run:[^\n]*(?:\n(?:\k<indent>[ ]+\S[^\n]*|[ \t]*(?=\n)))*') | ForEach-Object { $_.Value })
        @($runs | Where-Object { $_.Contains('router-attempt=$env:ROUTER_ATTEMPT') }).Count | Should -Be 3 -Because 'the three Record the runner scripts are among the blocks'
        @($runs | Where-Object { $_.Contains('echo "::error::rust-tests=') }).Count | Should -Be 1 -Because 'the gate''s multi-line script is among the blocks'
        foreach ($run in $runs) {
            # needs.route (any case, dotted or indexed) anywhere inside one ${{ }} expression, format() arguments included.
            $run | Should -Not -Match '(?i)\$\{\{(?:(?!\}\}).)*needs\s*(?:\.\s*route\b|\[\s*''route''\s*\])' -Because 'a router output in a script runs as shell; pass it through env:'
        }
    }
    It 'keeps no bare vendor runs-on, never compares the variable with namespace, and reads it only where the spec says' {
        ([regex]::Matches($windows, 'runs-on: namespace-profile')).Count | Should -Be 0
        ([regex]::Matches($windows, 'runs-on: windows-latest')).Count | Should -Be 0
        foreach ($text in $windows, $nightly, $pilot) { ([regex]::Matches($text, "WINDOWS_RUNNER_MODE == 'namespace'")).Count | Should -Be 0 }
        # Every comparison in the three workflows is with home or home-only.
        $reads = @([regex]::Matches($windows + "`n" + $nightly + "`n" + $pilot, "vars\.WINDOWS_RUNNER_MODE (==|!=) '([^']*)'"))
        $reads.Count | Should -Be 7 -Because 'windows.yml: the route if (2) and the shell gate (1); nightly.yml: runs-on (2) and cache (2)'
        foreach ($read in $reads) { @('home', 'home-only') | Should -Contain $read.Groups[2].Value }
    }
    It 'runs the workflow when the router changes' {
        ([regex]::Matches($windows, [regex]::Escape("- 'ci/windows-runner/route/**'"))).Count | Should -Be 2
    }
    It 'documents the modes, the published state and the full re-run in the header' {
        # The header is the comment block before the first key; code lines also say home-only and WINDOWS_HOME_STATE.
        $end = $windows.IndexOf("`nname: windows")
        $end | Should -BeGreaterThan 0
        $header = $windows.Substring(0, $end)
        $header | Should -Not -Match '(?m)^[ \t]*[^#\s]' -Because 'everything before name: windows is the header comment'
        $header | Should -Match 'self-hosted-windows-runner\.md'
        $joined = $header -replace '\r?\n#\s*', ' '
        foreach ($phrase in 'home-only', 'WINDOWS_HOME_STATE', 'Re-run all jobs', "anything else, including unset, is today's providers") {
            $joined | Should -Match ([regex]::Escape($phrase))
        }
    }
}

Describe 'nightly.yml pool switch' {
    It 'routes the Windows stress job home on home and home-only, and restores the GitHub cache everywhere else' {
        $nightly.Contains("runs-on: `${{ (vars.WINDOWS_RUNNER_MODE == 'home' || vars.WINDOWS_RUNNER_MODE == 'home-only') && 'slate-win-app' || 'windows-latest' }}") | Should -BeTrue
        $nightly.Contains("cache: `${{ (vars.WINDOWS_RUNNER_MODE != 'home' && vars.WINDOWS_RUNNER_MODE != 'home-only') && 'true' || 'false' }}") | Should -BeTrue
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
    It 'runs the router''s Python tests even when Pester failed' {
        $lane | Should -Match ([regex]::Escape('python -I -m unittest discover -v -s ci/windows-runner/route/tests -t ci/windows-runner/route'))
        # The step runs to the next step or the end of the file.
        $step = [regex]::Match($lane, "(?s)- name: Run the router's tests.*?(?=\n      - name:|\z)").Value
        $step | Should -Not -BeNullOrEmpty
        $step | Should -Match '(?m)^        if: \$\{\{ !cancelled\(\) \}\}[ \t]*\r?$'
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
