# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Text-level guards over the three workflows that read WINDOWS_RUNNER_MODE,
# over what branch-cleanup.yml owns (the weekly claude/* branch report;
# its script's behaviour is BranchCleanup.Tests.ps1), over the PR gate that
# main's ruleset requires, and over the pause on Dependabot version updates.
# YAML parsing happens on push (GitHub refuses a malformed workflow at the
# first run); these tests pin the routing contract, the delete switch, the
# gate and the pause themselves, and the path filters that make
# windows-runner-tests.yml run them.

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

Describe 'windows.yml rust lint' {
    # rust.yml lints on Linux, where cfg(windows) code never compiles (#1570); this lane lints what Windows builds.
    It 'the windows rust lane runs clippy with warnings as errors, rust.yml''s invocation verbatim, unconditionally, after both suites' {
        $command = 'cargo clippy --workspace --all-targets --locked -- -D warnings'
        $rustLint = Get-Content -Raw (Join-Path $wf 'rust.yml')
        $rustLint | Should -Match (Get-Lines @("        run: $command")) -Because 'the Windows step mirrors rust.yml''s lint job'
        $steps = Get-Steps (Get-JobBlock $windows 'rust-tests')
        $clippy = @($steps | Where-Object { $_ -match (Get-Lines @('      - name: clippy (windows x64, -D warnings)')) })
        $clippy.Count | Should -Be 1
        # Its only keys are the name and the command: no if:, continue-on-error: or shell override can soften it.
        $keys = @($clippy[0] -split '\r?\n' | Where-Object { $_ -match '\S' -and $_ -notmatch '^[ \t]*#' } | ForEach-Object { $_.TrimEnd() })
        $keys | Should -Be @('      - name: clippy (windows x64, -D warnings)', "        run: $command")
        # After the suites, so a lint finding never hides the Windows test results.
        foreach ($suiteName in 'slate-core non-census test suite', 'slate-cli test suite') {
            $suite = @($steps | Where-Object { $_ -match ('(?m)^      - name: ' + [regex]::Escape($suiteName)) })
            $suite.Count | Should -Be 1
            [array]::IndexOf($steps, $suite[0]) | Should -BeLessThan ([array]::IndexOf($steps, $clippy[0]))
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
        # rust.yml is read by the rust lint fact: a change to its clippy line must run these guards too.
        foreach ($routed in @('windows.yml', 'nightly.yml', 'windows-ci-pilot.yml', 'branch-cleanup.yml', 'pr-gate.yml', 'rust.yml')) {
            ([regex]::Matches($lane, [regex]::Escape("- '.github/workflows/$routed'"))).Count | Should -Be 2 -Because "$routed must be in both the pull_request and push path filters"
        }
    }
    It 'runs the branch-cleanup behaviour test when its script changes, on pull requests and on main' {
        ([regex]::Matches($lane, (Get-Lines @("      - 'ci/branch-cleanup/**'")))).Count | Should -Be 2
    }
    It 'runs the Dependabot guard when its config changes, on pull requests and on main' {
        ([regex]::Matches($lane, (Get-Lines @("      - '.github/dependabot.yml'")))).Count | Should -Be 2
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
    # The workflow owns when the script runs, with which token, and DELETE; what the script does with them
    # is BranchCleanup.Tests.ps1. (One BeforeAll: Pester 5 keeps only the last of several in a block.)
    BeforeAll {
        $script:cleanup = Get-Content -Raw (Join-Path $wf 'branch-cleanup.yml')
        $script:report = Get-JobBlock $cleanup 'report'
    }
    It 'reports weekly and deletes only on an explicit dispatch with the delete input' {
        $cleanup | Should -Match (Get-Lines @('  schedule:', "    - cron: '0 6 * * 1'", '  workflow_dispatch:', '    inputs:', '      delete:'))
        $cleanup | Should -Match (Get-Lines @('        type: boolean', '        default: false'))
        $cleanup | Should -Match (Get-Lines @('permissions:', '  contents: write', '  pull-requests: read'))
        # DELETE is 'true' only for a dispatch with the input set: this one live definition, at job level beside
        # the token and the repository, and no other. A second one in any form (a step's env, a quoted
        # "DELETE": key, DELETE=) would override it and could let the schedule delete, so the only live
        # (non-comment) line of the workflow that names DELETE at all is that job-level line.
        $line = '      DELETE: ${{ github.event_name == ''workflow_dispatch'' && inputs.delete && ''true'' || ''false'' }}'
        $report | Should -Match (Get-Lines @('    env:', '      GH_TOKEN: ${{ github.token }}', '      REPO: ${{ github.repository }}', $line))
        $live = @([regex]::Matches($cleanup, '(?m)^(?![ \t]*#)[^\r\n]*\bDELETE\b[^\r\n]*') | ForEach-Object { $_.Value.TrimEnd("`r") })
        $live.Count | Should -Be 1 -Because "only the job-level env line may name DELETE; found: $($live -join ' | ')"
        $live[0] | Should -BeExactly $line
    }
    It 'checks out only the script, with the pinned checkout and no persisted token, and runs it as its one command' {
        # The same checkout pin as the other workflows (this lane's).
        $pin = [regex]::Match($lane, 'uses: actions/checkout@[0-9a-f]{40}  # v\d+(?:\.\d+)*').Value
        $pin | Should -Not -BeNullOrEmpty
        $steps = Get-Steps $report
        $steps.Count | Should -Be 2
        $steps[0] | Should -Match (Get-Lines @("        $pin", '        with:', '          sparse-checkout: ci/branch-cleanup', '          persist-credentials: false'))
        $steps[1] | Should -Match (Get-Lines @('        run: bash ci/branch-cleanup/branch-cleanup.sh'))
        ([regex]::Matches($cleanup, '(?m)^[ \t]*(?:- )?run:')).Count | Should -Be 1
    }
}

Describe 'pr-gate.yml' {
    # main's ruleset requires this job's check with "branches must be up to date before merging" (#1565), so
    # every PR is re-tested on the current main before it merges. The lanes are path-filtered and cannot be the
    # required check: a lane a PR does not trigger never reports. This job must report on EVERY pull request
    # to main, and must not be skippable.
    BeforeAll {
        $script:gate = Get-Content -Raw (Join-Path $wf 'pr-gate.yml')
    }
    It 'runs on every pull request to main, with no path, branch or type filter' {
        $gate | Should -Match (Get-Lines @('on:', '  pull_request:', '    branches: [main]'))
        $gate | Should -Not -Match '(?m)^[ \t]*(paths|paths-ignore|branches-ignore|types):'
    }
    It 'reports one unconditional check named "pr gate", with no permissions, secrets or checkout' {
        $gate | Should -Match (Get-Lines @('permissions: {}'))
        ([regex]::Matches($gate, '(?m)^    name: ')).Count | Should -Be 1
        $gate | Should -Match (Get-Lines @('    name: pr gate'))
        $gate | Should -Not -Match '(?m)^[ \t]*(?:- )?if:'
        $gate | Should -Not -Match 'secrets\.'
        $gate | Should -Not -Match 'actions/checkout'
    }
}

Describe 'dependabot.yml' {
    # Version updates are paused (#1564). A Dependabot PR cannot run CI under the owner-only Actions policy
    # (plan 42), so it could only merge untested, and admitting Dependabot through allowlist.json would run
    # dependency code on the owner's hardware. Each ecosystem keeps its settings for the redesign, with a pull
    # request limit of zero, which stops its version updates.
    BeforeAll {
        $script:dependabot = Get-Content -Raw (Join-Path $wf '..' 'dependabot.yml')
    }
    It 'pauses version updates for every ecosystem with a pull request limit of zero' {
        $entries = @([regex]::Matches($dependabot, '(?ms)^  - package-ecosystem: .*?(?=^  - package-ecosystem: |\z)') | ForEach-Object { $_.Value })
        $entries.Count | Should -BeGreaterThan 0
        foreach ($entry in $entries) {
            $name = [regex]::Match($entry, 'package-ecosystem: (\S+)').Groups[1].Value
            $entry | Should -Match '(?m)^    open-pull-requests-limit: 0[ \t]*\r?$' -Because "$name stays paused until the redesign (#1564)"
        }
        # Exactly one limit per ecosystem, so no second key can override a zero.
        ([regex]::Matches($dependabot, '(?m)^[ \t]*open-pull-requests-limit:')).Count | Should -Be $entries.Count
    }
}
