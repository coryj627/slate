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
}

Describe 'windows.yml routing through the router' {
    It 'has one routing job on hosted Linux that runs only on home or home-only, with both permissions, the sparse checkout, the token and continue-on-error on both steps' {
        $route = Get-JobBlock $windows 'route'
        $route | Should -Not -BeNullOrEmpty
        $route | Should -Match 'name: Route Windows lanes'
        $route.Contains("if: `${{ vars.WINDOWS_RUNNER_MODE == 'home' || vars.WINDOWS_RUNNER_MODE == 'home-only' }}") | Should -BeTrue
        $route | Should -Match 'runs-on: ubuntu-latest'
        $route | Should -Match '(?m)^\s+permissions:\r?\n\s+actions: read\r?\n\s+contents: read'
        $route | Should -Match 'sparse-checkout: ci/windows-runner/route'
        $route | Should -Match 'persist-credentials: false'
        ([regex]::Matches($route, 'continue-on-error: true')).Count | Should -Be 2
        foreach ($env in 'GH_TOKEN: ${{ github.token }}', 'WINDOWS_RUNNER_MODE: ${{ vars.WINDOWS_RUNNER_MODE }}', 'WINDOWS_HOME_STATE: ${{ vars.WINDOWS_HOME_STATE }}',
            'ROUTE_LANES: rust,app,model-0,model-1', 'ROUTE_ATTEMPT: ${{ github.run_attempt }}', 'ROUTE_RUN_ID: ${{ github.run_id }}',
            'ROUTE_EVENT: ${{ github.event_name }}', 'ROUTE_HEAD_REPO: ${{ github.event.pull_request.head.repo.full_name }}', 'ROUTE_REPO: ${{ github.repository }}') {
            $route.Contains($env) | Should -BeTrue -Because "the routing step needs $env"
        }
        $route | Should -Match 'run: python3 ci/windows-runner/route/route\.py'
        foreach ($output in 'attempt', 'rust-pool', 'app-pool', 'model-0-pool', 'model-1-pool') {
            $route.Contains("      ${output}: `${{ steps.route.outputs.$output }}") | Should -BeTrue -Because "output $output"
        }
    }
    It 'every routed lane depends on route, runs when route was skipped or failed, and uses the router only in its own attempt with today''s vendor string as the fallback, verbatim' {
        foreach ($pair in @(@('rust-tests', 'rust', 'rust'), @('windows', 'app', 'app'))) {
            $block = Get-JobBlock $windows $pair[0]
            $block | Should -Match '(?m)^    needs: \[route\]'
            $block.Contains("if: `${{ !cancelled() }}") | Should -BeTrue
            $block.Contains("runs-on: `${{ $(Get-Predicate "$($pair[1])-pool") && 'slate-win-$($pair[1])' || $($vendor[$pair[2]]) }}") | Should -BeTrue -Because "lane $($pair[0])"
            $block.Contains("if: `${{ !$(Get-Predicate "$($pair[1])-pool") }}") | Should -BeTrue -Because "the cache gate of $($pair[0])"
        }
        $shards = Get-JobBlock $windows 'windows-model-shard'
        $shards | Should -Match '(?m)^    needs: \[route\]'
        $shards.Contains("if: `${{ !cancelled() }}") | Should -BeTrue
        $selector = "(needs.route.outputs.attempt == github.run_attempt && ((matrix.shard == 0 && needs.route.outputs.model-0-pool == 'home') || (matrix.shard == 1 && needs.route.outputs.model-1-pool == 'home')))"
        $shards.Contains("runs-on: `${{ $selector && 'slate-win-model' || $($vendor.model) }}") | Should -BeTrue
        $shards.Contains("if: `${{ !$selector }}") | Should -BeTrue
        ([regex]::Matches($windows, 'namespacelabs/nscloud-cache-action')).Count | Should -Be 3
    }
    It 'runs the shell gate on windows-latest in every mode but home-only, and only after a green app build whatever happened to route' {
        $flaui = Get-JobBlock $windows 'flaui'
        $flaui.Contains("runs-on: `${{ vars.WINDOWS_RUNNER_MODE == 'home-only' && 'slate-win-shell' || 'windows-latest' }}") | Should -BeTrue
        $flaui.Contains("if: `${{ !cancelled() && needs.windows.result == 'success' }}") | Should -BeTrue
    }
    It 'the gate waits on route too and names the pools when it fails' {
        $gate = Get-JobBlock $windows 'gate'
        $gate | Should -Match ([regex]::Escape('needs: [route, rust-tests, windows, windows-model, flaui]'))
        $gate | Should -Match 'pools: rust=\$\{\{ needs\.route\.outputs\.rust-pool \}\}'
    }
    It 'every routed lane prints its runner first' {
        foreach ($job in 'rust-tests', 'windows', 'windows-model-shard') {
            (Get-JobBlock $windows $job) | Should -Match '- name: Record the runner'
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
        $windows | Should -Match 'self-hosted-windows-runner\.md'
        $joined = $windows -replace '\r?\n#\s*', ' '
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
        $step = $lane.Substring($lane.IndexOf("Run the router's tests"))
        $step.Substring(0, 400) | Should -Match ([regex]::Escape('if: ${{ !cancelled() }}'))
    }
}
