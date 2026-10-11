# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Text-level guards over the three workflows that read WINDOWS_RUNNER_MODE.
# YAML parsing happens on push (GitHub refuses a malformed workflow at
# the first run); these tests pin the routing contract itself, and the
# path filters that make windows-runner-tests.yml run them.

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
        $route | Should -Match 'run: python3 ci/windows-runner/route/route\.py'
        # Every output below reads steps.route: without this id they are all empty and every lane takes the vendor.
        $route | Should -Match '(?m)^        id: route[ \t]*\r?$'
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
    It 'the gate waits on route too, runs whatever route did, and names the pools and the routing job''s result when it fails' {
        $gate = Get-JobBlock $windows 'gate'
        $gate | Should -Match ([regex]::Escape('needs: [route, rust-tests, windows, windows-model, flaui]'))
        # Without always() the gate is skipped with a skipped route (every run outside home and home-only), and a
        # skipped required check counts as passing.
        $gate | Should -Match (Get-JobLine 'if: ${{ always() }}')
        $gate.Contains('echo "::error::rust-tests=${{ needs.rust-tests.result }} app=${{ needs.windows.result }} model=${{ needs.windows-model.result }} flaui=${{ needs.flaui.result }} route=${{ needs.route.result }} pools: rust=${{ needs.route.outputs.rust-pool }} app=${{ needs.route.outputs.app-pool }} model-0=${{ needs.route.outputs.model-0-pool }} model-1=${{ needs.route.outputs.model-1-pool }} (router attempt ${{ needs.route.outputs.attempt }} of ${{ github.run_attempt }})"') | Should -BeTrue
    }
    It 'every routed lane prints its runner first' {
        foreach ($job in 'rust-tests', 'windows', 'windows-model-shard') {
            (Get-JobBlock $windows $job) | Should -Match '(?m)^    steps:\r?\n      - name: Record the runner\r?\n        run: [^\n]*\$\{\{ runner\.name \}\}' -Because "$job's first step names the effective pool"
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
        foreach ($routed in @('windows.yml', 'nightly.yml', 'windows-ci-pilot.yml')) {
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
