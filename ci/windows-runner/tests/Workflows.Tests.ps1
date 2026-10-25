# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Text-level guards over the three workflows that read WINDOWS_RUNNER_MODE,
# over what branch-cleanup.yml owns (the weekly claude/* branch report;
# its script's behaviour is BranchCleanup.Tests.ps1), over the PR gate that
# main's ruleset requires, over the pause on Dependabot version updates, over
# a11y-check.yml's gate on results whose download failed (#1575), and over the
# .NET SDK pin every setup-dotnet step reads (#1568), and over the Windows
# lane's clippy step (#1570).
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
    $script:a11y = Get-Content -Raw (Join-Path $wf 'a11y-check.yml')
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
    It 'has one routing job on hosted Linux that runs only on home or home-only, and only when the lane runs, with both permissions, the sparse checkout, the token, the step id the outputs read, a five-minute timeout and continue-on-error on the job and both steps' {
        $route = Get-JobBlock $windows 'route'
        $route | Should -Not -BeNullOrEmpty
        $route | Should -Match 'name: Route Windows lanes'
        # #1565: skipped with the lane when a pull request touches none of its paths; run when the decision failed.
        $route | Should -Match (Get-JobLine 'needs: [changes]')
        $route | Should -Match (Get-JobLine "if: `${{ !cancelled() && needs.changes.outputs.run != 'false' && (vars.WINDOWS_RUNNER_MODE == 'home' || vars.WINDOWS_RUNNER_MODE == 'home-only') }}")
        $route | Should -Match 'runs-on: ubuntu-latest'
        $route | Should -Match (Get-JobLine 'timeout-minutes: 5')
        # On the job, so a timeout or a lost runner cannot fail the run either; and on each of the two steps.
        $route | Should -Match (Get-JobLine 'continue-on-error: true') -Because 'step-level continue-on-error does not cover a job timeout or a lost runner'
        ([regex]::Matches($route, '(?m)^        continue-on-error: true[ \t]*\r?$')).Count | Should -Be 2
        $route | Should -Match '(?m)^\s+permissions:\r?\n\s+actions: read\r?\n\s+contents: read'
        # The router and the allow-list its bot rule reads (plan 42 §3.7), as patterns: cone mode would not bring a file.
        $route | Should -Match '(?m)^          sparse-checkout: \|\r?\n            /ci/windows-runner/route/\r?\n            /ci/mac-runner/allowlist\.json\r?\n          sparse-checkout-cone-mode: false'
        $route | Should -Match 'persist-credentials: false'
        foreach ($entry in 'GH_TOKEN: ${{ github.token }}', 'WINDOWS_RUNNER_MODE: ${{ vars.WINDOWS_RUNNER_MODE }}', 'WINDOWS_HOME_STATE: ${{ vars.WINDOWS_HOME_STATE }}',
            'ROUTE_LANES: rust,app,model-0,model-1', 'ROUTE_ATTEMPT: ${{ github.run_attempt }}', 'ROUTE_RUN_ID: ${{ github.run_id }}',
            'ROUTE_EVENT: ${{ github.event_name }}', 'ROUTE_HEAD_REPO: ${{ github.event.pull_request.head.repo.full_name }}', 'ROUTE_REPO: ${{ github.repository }}',
            'ROUTE_ACTOR: ${{ github.actor }}', 'ROUTE_TRIGGERING_ACTOR: ${{ github.triggering_actor }}', 'ROUTE_PR_AUTHOR: ${{ github.event.pull_request.user.login }}',
            'ROUTE_HEAD_REF: ${{ github.head_ref || github.ref_name }}', 'ROUTE_ALLOWLIST: ci/mac-runner/allowlist.json') {
            $route.Contains($entry) | Should -BeTrue -Because "the routing step needs $entry"
        }
        # Every output below reads steps.route: the id belongs to the step that runs the router, or every output is
        # empty and every lane takes the vendor without a word.
        $steps = Get-Steps $route
        $router = @($steps | Where-Object { $_ -match '(?m)^        run: python3 ci/windows-runner/route/route\.py[ \t]*\r?$' })
        $router.Count | Should -Be 1 -Because 'one step runs the router'
        $router[0] | Should -Match '(?m)^        id: route[ \t]*\r?$' -Because 'the step that runs the router is the one the outputs read'
        $checkout = @($steps | Where-Object { $_.Contains('/ci/windows-runner/route/') })
        $checkout.Count | Should -Be 1
        $checkout[0] | Should -Not -Match '(?m)^        id:' -Because 'the checkout step carries no id'
        foreach ($output in 'attempt', 'rust-pool', 'app-pool', 'model-0-pool', 'model-1-pool') {
            $route.Contains("      ${output}: `${{ steps.route.outputs.$output }}") | Should -BeTrue -Because "output $output"
        }
    }
    It 'every routed lane depends on changes and route, runs when route was skipped or failed, and uses the router only in its own attempt with today''s vendor string as the fallback, verbatim' {
        $lanePredicate = "if: `${{ !cancelled() && needs.changes.outputs.run != 'false' }}"
        foreach ($pair in @(@('rust-tests', 'rust', 'rust'), @('windows', 'app', 'app'))) {
            $block = Get-JobBlock $windows $pair[0]
            $block | Should -Match (Get-JobLine 'needs: [changes, route]')
            $block | Should -Match (Get-JobLine $lanePredicate) -Because "lane $($pair[0]) must run when route was skipped or failed"
            $block.Contains("runs-on: `${{ $(Get-Predicate "$($pair[1])-pool") && 'slate-win-$($pair[1])' || $($vendor[$pair[2]]) }}") | Should -BeTrue -Because "lane $($pair[0])"
            $block | Should -Match (Get-CacheGate (Get-Predicate "$($pair[1])-pool")) -Because "the cache gate of $($pair[0])"
        }
        $shards = Get-JobBlock $windows 'windows-model-shard'
        $shards | Should -Match (Get-JobLine 'needs: [changes, route]')
        $shards | Should -Match (Get-JobLine $lanePredicate) -Because 'the model shards must run when route was skipped or failed'
        $selector = "(needs.route.outputs.attempt == github.run_attempt && ((matrix.shard == 0 && needs.route.outputs.model-0-pool == 'home') || (matrix.shard == 1 && needs.route.outputs.model-1-pool == 'home')))"
        $shards.Contains("runs-on: `${{ $selector && 'slate-win-model' || $($vendor.model) }}") | Should -BeTrue
        $shards | Should -Match (Get-CacheGate $selector) -Because 'the cache gate of windows-model-shard'
        ([regex]::Matches($windows, 'namespacelabs/nscloud-cache-action')).Count | Should -Be 3
    }
    It 'runs the shell gate on windows-latest in every mode but home-only, and for a fork pull request or a Dependabot run even then, and only after a green app build whatever happened to route' {
        $flaui = Get-JobBlock $windows 'flaui'
        $flaui | Should -Match '(?m)^    runs-on: \$\{\{ .* \|\| ''windows-latest'' \}\}[ \t]*\r?$' -Because 'today''s provider is the fallback, verbatim'
        # home-only sends it home only for a run of this repository: a fork never goes home, and neither does a run
        # Dependabot started or re-ran (plan 42 §3.7).
        $flaui | Should -Match (Get-JobLine "runs-on: `${{ (vars.WINDOWS_RUNNER_MODE == 'home-only' && (github.event_name != 'pull_request' || github.event.pull_request.head.repo.full_name == github.repository) && github.actor != 'dependabot[bot]' && github.triggering_actor != 'dependabot[bot]' && github.event.pull_request.user.login != 'dependabot[bot]' && !startsWith(github.head_ref || github.ref_name, 'dependabot/')) && 'slate-win-shell' || 'windows-latest' }}")
        $flaui | Should -Match (Get-JobLine 'needs: [changes, windows]')
        $flaui | Should -Match (Get-JobLine "if: `${{ !cancelled() && needs.changes.outputs.run != 'false' && needs.windows.result == 'success' }}")
    }
    It 'the gate waits on route too, runs whatever route did, and names the pools, the routing job''s result and the router''s attempt, quoted, when it fails' {
        $gate = Get-JobBlock $windows 'gate'
        $gate | Should -Match (Get-JobLine 'needs: [changes, route, rust-tests, windows, windows-model, flaui]')
        # Without always() the gate is skipped with a skipped route (every run outside home and home-only), and a
        # skipped required check counts as passing. It is skipped, and so passes, only when changes decided that
        # the pull request touches none of the lane's paths (#1565); a failed decision runs it with the lanes.
        $gate | Should -Match (Get-JobLine "if: `${{ always() && needs.changes.outputs.run != 'false' }}")
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
        # One list, on.push.paths: lane-changes.yml reads it for pull requests too (#1565).
        ([regex]::Matches($windows, [regex]::Escape("- 'ci/windows-runner/route/**'"))).Count | Should -Be 1
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

Describe 'the .NET SDK pin' {
    # dotnet resolves global.json upward from the working directory, and every CI dotnet runs from the repository
    # root, so the pin lives there (#1568). setup-dotnet installs the newest SDK for a latest* rollForward, so the
    # pin is exact; the step after each setup fails the job unless dotnet resolves exactly that version.
    BeforeAll {
        $script:root = Join-Path $PSScriptRoot '..' '..' '..'
        $script:sdkPin = Get-Content -Raw (Join-Path $root 'global.json') | ConvertFrom-Json
        # Every workflow and every composite action, so a setup-dotnet added anywhere is held to the pin.
        $script:setupFiles = @{}
        foreach ($file in @(Get-ChildItem (Join-Path $root '.github' 'workflows') -File -Include '*.yml', '*.yaml' -Recurse) +
            @(Get-ChildItem (Join-Path $root '.github' 'actions') -File -Include 'action.yml', 'action.yaml' -Recurse)) {
            $setupFiles[$file.FullName.Substring((Resolve-Path $root).Path.Length + 1)] = Get-Content -Raw $file.FullName
        }
        $script:recordBody = @(
            '$pinned = (Get-Content -Raw global.json | ConvertFrom-Json).sdk.version',
            '$resolved = (dotnet --version).Trim()',
            'Write-Host "global.json pins $pinned; dotnet resolves $resolved"',
            'if ($resolved -ne $pinned) { throw "dotnet resolved SDK $resolved, not the pinned $pinned" }')
    }
    It 'global.json at the root names one exact SDK with rollForward patch, and no other tracked global.json shadows it' {
        $sdkPin.sdk.version | Should -Match '^\d+\.\d+\.\d{3}$' -Because 'a full SDK version, never a band or a prerelease'
        $sdkPin.sdk.rollForward | Should -BeExactly 'patch' -Because 'a latest* rollForward makes setup-dotnet install the newest SDK on its release day'
        $tracked = @(git -C $root ls-files | Where-Object { $_ -match '(^|/)global\.json$' })
        $tracked | Should -Be @('global.json') -Because 'dotnet run below a second global.json would read that pin first'
    }
    It 'every setup-dotnet step in every workflow and action reads the root global.json, names no version, and is followed by the step that fails unless dotnet resolves the pin' {
        $count = 0
        foreach ($name in $setupFiles.Keys) {
            $text = $setupFiles[$name]
            if ($text -notmatch 'actions/setup-dotnet@') { continue }
            $text | Should -Not -Match 'dotnet-version:' -Because "$name must take the version from global.json alone"
            $steps = @(foreach ($match in [regex]::Matches($text, '(?ms)^(?<indent>[ ]+)- (?:(?!^\k<indent>- ).)*')) { $match.Value })
            for ($i = 0; $i -lt $steps.Count; $i++) {
                if ($steps[$i] -notmatch 'uses: actions/setup-dotnet@') { continue }
                $count++
                $steps[$i] | Should -Match '(?m)^[ ]+global-json-file: global\.json[ \t]*\r?$' -Because "every setup-dotnet in $name reads the pin"
                $indent = [regex]::Match($steps[$i], '^[ ]+').Value
                # The record step's lines, exactly: nothing can be added to soften it (an if:, a continue-on-error:,
                # an early exit) or taken out of it.
                $expected = @("$indent- name: Record the .NET SDK")
                if ($name -like '*action.y*ml') { $expected += "$indent  shell: pwsh" }
                $expected += "$indent  run: |"
                $expected += @($recordBody | ForEach-Object { "$indent    $_" })
                $lines = @($steps[$i + 1] -split '\r?\n' | Where-Object { $_ -match '\S' -and $_ -notmatch '^[ \t]*#' } | ForEach-Object { $_.TrimEnd() })
                $lines | Should -Be $expected -Because "the step after setup-dotnet in $name checks the pin and nothing else"
            }
        }
        $count | Should -Be 6 -Because 'three in windows.yml, two in the pilot, one in the composite build action; a removed one leaves dotnet on the image''s SDK'
    }
    It 'the flaui job downloads the artifact to the workspace root before it sets up .NET, and the artifact carries global.json' {
        $steps = Get-Steps (Get-JobBlock $windows 'flaui')
        $steps[0] | Should -Match (Get-Lines @('      - name: Download FlaUI gate binaries'))
        $steps[0] | Should -Match (Get-Lines @('          path: .'))
        $steps[1] | Should -Match 'uses: actions/setup-dotnet@'
        $upload = @(Get-Steps (Get-JobBlock $windows 'windows') | Where-Object { $_ -match '- name: Upload FlaUI gate binaries' })
        $upload.Count | Should -Be 1
        $upload[0] | Should -Match (Get-Lines @('          path: |', '            global.json'))
    }
    It 'a change to the pin runs the Windows lane and these guards, on pull requests and on main' {
        # Each lane's one path list, on.push.paths, which lane-changes.yml reads for pull requests (#1565).
        ([regex]::Matches($windows, (Get-Lines @("      - 'global.json'")))).Count | Should -Be 1
        foreach ($path in "      - '**/global.json'", "      - '.github/actions/windows-native-build/action.yml'") {
            ([regex]::Matches($lane, (Get-Lines @($path)))).Count | Should -Be 1 -Because "$path must be in windows-runner-tests.yml's path list"
        }
    }
}

Describe 'a11y-check.yml' {
    # download-artifact extracts first and checks the digest after, so a mismatch fails the step with the
    # unverified files on disk (#1575). The score must never be computed, uploaded or posted from them.
    It 'discards the downloaded results when the download step did not succeed, before anything reads them' {
        $steps = Get-Steps (Get-JobBlock $a11y 'a11y-check')
        $download = [array]::IndexOf($steps, @($steps | Where-Object { $_ -match '(?m)^      - name: Download the analysis results' })[0])
        $download | Should -BeGreaterOrEqual 0
        $steps[$download] | Should -Match (Get-Lines @('        id: results'))
        $steps[$download] | Should -Match (Get-Lines @('        continue-on-error: true')) -Because 'a missing artifact must still reach the floor step''s plain-words report'
        $discard = $steps[$download + 1]
        $discard | Should -Match (Get-Lines @(
            '      - name: Discard results the download did not verify',
            '        id: discard',
            "        if: steps.results.outcome != 'success'",
            '        run: |',
            '          if [ -e a11y-results.json ] || [ -e a11y-results.sarif ]; then',
            '            echo "discarded=true" >> "$GITHUB_OUTPUT"',
            '          fi',
            '          rm -f a11y-results.json a11y-results.sarif'))
        $discard | Should -Not -Match '(?m)^        continue-on-error:' -Because 'a discard that fails must stop the job before anything reads the files'
        # Every later step that reads the files is guarded on their existence, and none comes before the discard.
        foreach ($reader in @(
                @('Upload SARIF to GitHub Code Scanning', "        if: hashFiles('a11y-results.sarif') != ''"),
                @('Post score summary on PR', "        if: github.event_name == 'pull_request' && hashFiles('a11y-results.json') != ''"))) {
            $index = [array]::IndexOf($steps, @($steps | Where-Object { $_ -match ('(?m)^      - name: ' + [regex]::Escape($reader[0])) })[0])
            $index | Should -BeGreaterThan ($download + 1) -Because "$($reader[0]) must run after the discard"
            $steps[$index] | Should -Match (Get-Lines @($reader[1]))
        }
        $floor = @($steps | Where-Object { $_ -match '(?m)^      - name: Enforce score floor' })
        $floor.Count | Should -Be 1
        [array]::IndexOf($steps, $floor[0]) | Should -BeGreaterThan ($download + 1)
        # The floor always runs and always fails without results: no if:, no continue-on-error:, and each
        # missing-results branch is a setFailed that returns.
        $floor[0] | Should -Not -Match '(?m)^        (if|continue-on-error):' -Because 'a skipped or softened floor turns a discarded download green'
        $floor[0] | Should -Match (Get-Lines @(
            '          RESULTS_DOWNLOAD: ${{ steps.results.outcome }}',
            '          RESULTS_DISCARDED: ${{ steps.discard.outputs.discarded }}',
            '          ANALYZE_RESULT: ${{ needs.analyze.result }}'))
        $floor[0] | Should -Match (Get-Lines @(
            '            if (!fs.existsSync(resultsPath)) {',
            "              if (process.env.RESULTS_DISCARDED === 'true') {",
            '                core.setFailed('))
        $floor[0] | Should -Match (Get-Lines @(
            '                  `the "Download the analysis results" step.`',
            '                );',
            '                return;',
            '              }',
            "              if (process.env.RESULTS_DOWNLOAD === 'failure' && process.env.ANALYZE_RESULT === 'success') {",
            '                core.setFailed('))
        $floor[0] | Should -Match (Get-Lines @(
            '                  `computed. See the "Download the analysis results" step.`',
            '                );',
            '                return;',
            '              }',
            '              core.setFailed('))
        $missing = [regex]::Match($floor[0], '(?s)if \(!fs\.existsSync\(resultsPath\)\) \{.*?const results = JSON\.parse').Value
        $missing | Should -Not -BeNullOrEmpty
        $missing | Should -Not -Match 'core\.(warning|notice|info)\(' -Because 'every missing-results branch fails the check'
        ([regex]::Matches($missing, 'core\.setFailed\(')).Count | Should -Be 3
        ([regex]::Matches($missing, '(?m)^[ ]+return;[ \t]*\r?$')).Count | Should -Be 3
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
    # The lane keeps one path list, on.push.paths, and lane-changes.yml reads it for pull requests (#1565).
    It 'runs these guards when a routed workflow changes, on pull requests and on main' {
        # rust.yml is read by the rust lint fact: a change to its clippy line must run these guards too.
        foreach ($routed in @('windows.yml', 'nightly.yml', 'windows-ci-pilot.yml', 'branch-cleanup.yml', 'pr-gate.yml', 'a11y-check.yml', 'rust.yml')) {
            ([regex]::Matches($lane, [regex]::Escape("- '.github/workflows/$routed'"))).Count | Should -Be 1 -Because "$routed must be in the lane's path list"
        }
    }
    It 'runs the branch-cleanup behaviour test when its script changes, on pull requests and on main' {
        ([regex]::Matches($lane, (Get-Lines @("      - 'ci/branch-cleanup/**'")))).Count | Should -Be 1
    }
    It 'runs the Dependabot guard when its config changes, on pull requests and on main' {
        ([regex]::Matches($lane, (Get-Lines @("      - '.github/dependabot.yml'")))).Count | Should -Be 1
    }
    It 'runs the lane decision''s guards and the script''s tests when a lane, lane-changes.yml or the script changes' {
        $paths = @('a11y-check', 'audit', 'license-headers', 'mac-runner-tests', 'rust', 'swift-tests', 'windows-runner-tests', 'windows', 'lane-changes' |
                ForEach-Object { ".github/workflows/$_.yml" }) + @('scripts/lane_changes.py', 'scripts/tests/test_lane_changes.py')
        foreach ($path in $paths) {
            ([regex]::Matches($lane, (Get-Lines @("      - '$path'")))).Count | Should -Be 1 -Because "$path must be in the lane's path list"
        }
        $step = [regex]::Match($lane, "(?s)- name: Run the lane decision's tests.*?(?=\n      - name:|\z)").Value
        $step | Should -Not -BeNullOrEmpty
        $step | Should -Match '(?m)^        if: \$\{\{ !cancelled\(\) \}\}[ \t]*\r?$' -Because 'one red suite cannot hide another'
        $step | Should -Match (Get-Lines @('        run: python -I -m unittest discover -v -s scripts/tests -p test_lane_changes.py'))
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
    # every PR is re-tested on the current main before it merges. Until the owner adds the lanes' checks to
    # the ruleset (each lane now reports on every pull request: 'lane change detection' below), it is the only
    # required check. This job must report on EVERY pull request to main, and must not be skippable.
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

Describe 'lane change detection' {
    # #1565 (plan 45 XB-13): every lane triggers on every pull request, and its first job, changes, decides from
    # the lane's one path list (on.push.paths) whether the pull request touches the lane. Every other job is
    # skipped when it does not, and GitHub reports a skipped job as passed, so each lane's checks report on every
    # pull request and main's ruleset can require them. A failed or missing decision must run the lane: a job
    # that needs changes without a status function is skipped when changes fails, and that skip would report as
    # passed with nothing tested.
    BeforeAll {
        $script:decider = Get-Content -Raw (Join-Path $wf 'lane-changes.yml')
        function Get-JobIds([string]$Text) {
            $at = [regex]::Match($Text, '(?m)^jobs:[ \t]*\r?$')
            if (-not $at.Success) { return @() }
            @([regex]::Matches($Text.Substring($at.Index), '(?m)^  ([A-Za-z0-9_-]+):[ \t]*\r?$') | ForEach-Object { $_.Groups[1].Value })
        }
        # A job-level key's one-line value; '' for a key whose value is a block, $null for no key.
        function Get-JobKey([string]$Block, [string]$Key) {
            $m = [regex]::Match($Block, "(?m)^    ${Key}:[ \t]*(.*?)[ \t]*\r?$")
            if ($m.Success) { return $m.Groups[1].Value }
            return $null
        }
        function Get-NeedsList([string]$Value) {
            if (-not $Value) { return @() }
            if ($Value -match '^\[(.*)\]$') { return @($Matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
            return @($Value)
        }
        # Whether an if: skips the job on a decision of 'false' and runs it on a failed or missing one: a status
        # function, then the decision, then only && terms. A top-level || binds looser than the decision and could
        # run the job on 'false'; inside parentheses it cannot.
        function Test-Decided([string]$If) {
            $m = [regex]::Match($If, '^\$\{\{ (?:always\(\)|!cancelled\(\)) && needs\.changes\.outputs\.run != ''false''(?<rest>(?: && .+)?) \}\}$')
            if (-not $m.Success) { return $false }
            $rest = $m.Groups['rest'].Value
            while ($rest -match '\([^()]*\)') { $rest = $rest -replace '\([^()]*\)', '' }
            return -not $rest.Contains('||')
        }
        # Every job but changes either decides for itself (a status function, needs changes, Test-Decided), or
        # has no status function, does not need changes, and needs a job that is skipped with the lane: GitHub
        # skips it with that job, and runs it as before when that job runs.
        function Get-GateProblems([string]$Text) {
            $jobs = @{}
            $problems = @()
            foreach ($id in Get-JobIds $Text) {
                if ($id -eq 'changes') { continue }
                $block = Get-JobBlock $Text $id
                $needs = Get-JobKey $block 'needs'
                if ($needs -eq '') { $problems += "${id}: write needs on one line"; continue }
                $needsList = @(Get-NeedsList $needs)
                $if = Get-JobKey $block 'if'
                if ($null -ne $if -and ($if -eq '' -or $if -match '^[>|]')) { $problems += "${id}: write if on one line"; continue }
                # A job that is not in needs reads as empty: needs.changes.outputs.run == 'true' without changes in
                # needs skips the job on every pull request, and the skip reports as passed.
                if ($if -match '(?i)\bneeds\s*\[') { $problems += "${id}: read needs as needs.<job>"; continue }
                $unread = @([regex]::Matches("$if", '(?i)(?<![\w.-])needs\.([A-Za-z0-9_-]+)') | ForEach-Object { $_.Groups[1].Value } |
                        Where-Object { $needsList -notcontains $_ })
                if ($unread.Count -gt 0) { $problems += "${id}: reads needs.$($unread[0]), which it does not need"; continue }
                $jobs[$id] = @{ Needs = $needsList; If = $if }
            }
            $decided = @{}
            do {
                $grew = $false
                foreach ($id in @($jobs.Keys)) {
                    if ($decided.ContainsKey($id)) { continue }
                    $job = $jobs[$id]
                    $status = $job.If -and ($job.If -match '(?<![\w.])(?:always|cancelled|failure|success)\(\)')
                    if ($status) { $ok = ($job.Needs -contains 'changes') -and (Test-Decided $job.If) }
                    else { $ok = ($job.Needs -notcontains 'changes') -and @($job.Needs | Where-Object { $decided.ContainsKey($_) }).Count -gt 0 }
                    if ($ok) { $decided[$id] = $true; $grew = $true }
                }
            } while ($grew)
            foreach ($id in @($jobs.Keys | Sort-Object)) {
                if (-not $decided.ContainsKey($id)) { $problems += "${id}: needs=[$($jobs[$id].Needs -join ', ')] if=$($jobs[$id].If)" }
            }
            return $problems
        }
        $script:fixture = @(
            'jobs:',
            '  changes:',
            '    name: changes',
            '    uses: ./.github/workflows/lane-changes.yml',
            '  a:',
            '    needs: [changes]',
            "    if: `${{ !cancelled() && needs.changes.outputs.run != 'false' }}",
            '  b:',
            '    needs: a',
            '') -join "`n"
    }
    It 'triggers on every pull request, with no path filter, and keeps one path list, on.push.paths: <Workflow>' -ForEach @(
        @{ Workflow = 'a11y-check' }, @{ Workflow = 'audit' }, @{ Workflow = 'license-headers' }, @{ Workflow = 'mac-runner-tests' },
        @{ Workflow = 'rust' }, @{ Workflow = 'swift-tests' }, @{ Workflow = 'windows-runner-tests' }, @{ Workflow = 'windows' }
    ) {
        $text = Get-Content -Raw (Join-Path $wf "$Workflow.yml")
        $on = [regex]::Match($text, '(?ms)^on:[ \t]*\r?\n.*?(?=^\S)').Value
        $on | Should -Not -BeNullOrEmpty
        $on | Should -Match '(?m)^  pull_request:[ \t]*\r?\n(?!    )' -Because 'any pull_request filter keeps the lane from reporting on some pull requests'
        $on | Should -Not -Match '(?m)^[ \t]+paths-ignore:'
        ([regex]::Matches($on, '(?m)^    paths:[ \t]*\r?$')).Count | Should -Be 1
        $on | Should -Match (Get-Lines @('  push:', '    branches: [main]', '    paths:'))
    }
    It 'starts with the changes job, which hands lane-changes.yml the lane''s own workflow file: <Workflow>' -ForEach @(
        @{ Workflow = 'a11y-check' }, @{ Workflow = 'audit' }, @{ Workflow = 'license-headers' }, @{ Workflow = 'mac-runner-tests' },
        @{ Workflow = 'rust' }, @{ Workflow = 'swift-tests' }, @{ Workflow = 'windows-runner-tests' }, @{ Workflow = 'windows' }
    ) {
        $text = Get-Content -Raw (Join-Path $wf "$Workflow.yml")
        @(Get-JobIds $text)[0] | Should -BeExactly 'changes'
        $changes = Get-JobBlock $text 'changes'
        $changes | Should -Match (Get-Lines @('  changes:', '    name: changes', '    uses: ./.github/workflows/lane-changes.yml', '    permissions:',
                '      contents: read', '    with:', "      workflow: .github/workflows/$Workflow.yml"))
        $changes | Should -Not -Match '(?m)^    (if|needs|secrets):' -Because 'the decision runs on every event, first'
    }
    It 'skips every other job when the pull request touches none of the lane''s paths, and runs it when the decision failed: <Workflow>' -ForEach @(
        @{ Workflow = 'a11y-check' }, @{ Workflow = 'audit' }, @{ Workflow = 'license-headers' }, @{ Workflow = 'mac-runner-tests' },
        @{ Workflow = 'rust' }, @{ Workflow = 'swift-tests' }, @{ Workflow = 'windows-runner-tests' }, @{ Workflow = 'windows' }
    ) {
        $text = Get-Content -Raw (Join-Path $wf "$Workflow.yml")
        (Get-JobIds $text).Count | Should -BeGreaterThan 1
        $problems = @(Get-GateProblems $text)
        $problems.Count | Should -Be 0 -Because ($problems -join '; ')
    }
    It 'the rule refuses a job that skips on a failed decision, runs on a false one, or reads a decision it does not need' {
        @(Get-GateProblems $fixture).Count | Should -Be 0 -Because 'the fixture itself passes'
        $decided = "    if: `${{ !cancelled() && needs.changes.outputs.run != 'false' }}"
        foreach ($case in @(
                # The implicit success(): a failed changes skips a, and the skip reports as passed.
                @('a', ($fixture.Replace("`n$decided", ''))),
                @('a', ($fixture.Replace($decided, "    if: `${{ needs.changes.outputs.run == 'true' }}"))),
                @('a', ($fixture.Replace($decided, "    if: `${{ !cancelled() }}"))),
                @('a', ($fixture.Replace($decided, "    if: `${{ !cancelled() && needs.changes.outputs.run != 'false' || github.event_name == 'push' }}"))),
                @('a', ($fixture.Replace($decided, "    if: always() && needs.changes.outputs.run != 'false'"))),
                # Without changes in needs, needs.changes is empty and never 'false'.
                @('a', ($fixture.Replace('    needs: [changes]', '    needs: [route]'))),
                @('b', ($fixture.Replace('    needs: a', "    needs: a`n    if: `${{ always() }}"))),
                @('b', ($fixture.Replace('    needs: a', '    needs: [changes, a]'))),
                # Without a status function b keeps its implicit success(), but its if: reads a job it does not need.
                @('b', ($fixture.Replace('    needs: a', "    needs: a`n    if: `${{ needs.changes.outputs.run == 'true' }}"))),
                # A block scalar hides the status function from a one-line reading.
                @('b', ($fixture.Replace('    needs: a', "    needs: a`n    if: >-`n      `${{ always() }}"))),
                @('b', ($fixture.Replace("    needs: a`n", ''))))) {
            $problems = @(Get-GateProblems $case[1])
            ($problems -join '; ') | Should -Match ('^(?:.*; )?' + $case[0] + ':') -Because "job $($case[0]) breaks the rule in:`n$($case[1])"
        }
        # Parentheses keep an || inside the job's own condition.
        $grouped = $fixture.Replace($decided, "    if: `${{ !cancelled() && needs.changes.outputs.run != 'false' && (github.event_name == 'push' || github.event.pull_request.head.repo.full_name == github.repository) }}")
        @(Get-GateProblems $grouped).Count | Should -Be 0
    }
    It 'every workflow a pull request starts is a lane that decides, the PR gate, or a pilot nothing requires' {
        # A new path-filtered workflow would never report on some pull requests; it joins the lists above instead.
        $known = 'a11y-check', 'audit', 'license-headers', 'mac-runner-tests', 'rust', 'swift-tests', 'windows-runner-tests', 'windows',
        'pr-gate', 'mac-ci-pilot', 'windows-ci-pilot'
        foreach ($file in Get-ChildItem $wf -Filter '*.yml') {
            $text = Get-Content -Raw $file.FullName
            $on = [regex]::Match($text, '(?ms)^on:.*?(?=^\S)').Value
            if ($on -notmatch '\bpull_request') { continue }
            $known | Should -Contain $file.BaseName -Because "$($file.Name) runs on pull requests"
        }
    }
    It 'lane-changes.yml decides on pull requests only, with a read-only token, from the merge commit and its base, and expands nothing in its script' {
        $decider | Should -Match (Get-Lines @('on:', '  workflow_call:'))
        $decider | Should -Not -Match '(?m)^  (pull_request|pull_request_target|push|workflow_dispatch|schedule):'
        $decider | Should -Match (Get-Lines @('permissions: {}'))
        $decider | Should -Match (Get-Lines @('        value: ${{ jobs.match.outputs.run }}'))
        $decider | Should -Not -Match 'secrets\.'
        (Get-JobIds $decider) -join ', ' | Should -BeExactly 'match'
        $match = Get-JobBlock $decider 'match'
        $match | Should -Match (Get-Lines @('    runs-on: ubuntu-latest'))
        $match | Should -Match (Get-Lines @('    permissions:', '      contents: read'))
        $match | Should -Match (Get-Lines @('    outputs:', '      run: ${{ steps.match.outputs.run }}'))
        $pin = [regex]::Match($lane, 'uses: actions/checkout@[0-9a-f]{40}  # v\d+(?:\.\d+)*').Value
        $pin | Should -Not -BeNullOrEmpty
        $steps = Get-Steps $match
        $steps.Count | Should -Be 2
        # The script and the workflows, as patterns: cone mode would bring every file at the root as well.
        $steps[0] | Should -Match (Get-Lines @("        if: github.event_name == 'pull_request'", "        $pin", '        with:', '          fetch-depth: 2',
                '          filter: blob:none', '          sparse-checkout: |', '            /scripts/lane_changes.py', '            /.github/workflows/',
                '          sparse-checkout-cone-mode: false', '          persist-credentials: false'))
        $steps[1] | Should -Match (Get-Lines @('        id: match', '        env:', '          EVENT: ${{ github.event_name }}', '          WORKFLOW: ${{ inputs.workflow }}', '        run: |'))
        # Outside a pull request the trigger's own path filter chose the run.
        $steps[1] | Should -Match (Get-Lines @('          if [ "$EVENT" != pull_request ]; then'))
        $steps[1] | Should -Match (Get-Lines @('            echo "run=true" >> "$GITHUB_OUTPUT"', '            exit 0', '          fi'))
        # A pull request that changes the decision runs every lane, so a decision that wrongly answers false cannot
        # skip the tests that would catch it; a failed diff fails the job, which runs the lane too.
        $steps[1] | Should -Match (Get-Lines @(
                '          if ! decider="$(git diff --name-only --no-renames HEAD^1 HEAD -- scripts/lane_changes.py .github/workflows/lane-changes.yml)"; then',
                '            echo "::error::cannot diff the merge commit with its base parent, so the lane runs."',
                '            exit 1',
                '          fi',
                '          if [ -n "$decider" ]; then',
                '            echo "This pull request changes the lane decision itself, so the lane runs: $decider"',
                '            echo "run=true" >> "$GITHUB_OUTPUT"',
                '            exit 0',
                '          fi',
                '          python3 -I scripts/lane_changes.py "$WORKFLOW"'))
        $steps[1].Substring($steps[1].IndexOf('        run: |')) | Should -Not -Match '\$\{\{' -Because 'the inputs reach the script through env:'
    }
    Context 'the mac lanes'' required checks' {
        # On the mac lanes a job is skipped when the one it needs failed (route, then the XCTest suite or the
        # analysis), and a skipped job reports as passed. Each lane's gate reports on every pull request that touches
        # the lane and fails unless the lane ran; the ruleset requires the gate (#1565), as windows.yml's gate.
        BeforeAll {
            $script:bash = $null
            if ($IsWindows) {
                # Git for Windows' bash, beside git: the bash on PATH can be WSL's (BranchCleanup.Tests.ps1).
                $git = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
                $dir = if ($git) { Split-Path $git.Source } else { '' }
                while ($dir -and -not $script:bash) {
                    $candidate = Join-Path $dir 'bin' 'bash.exe'
                    if (Test-Path $candidate) { $script:bash = $candidate }
                    $dir = Split-Path $dir
                }
            }
            else {
                $script:bash = (Get-Command bash -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
            }
            # The gate's run: block, its lines dedented.
            function Get-GateScript([string]$Text) {
                $gate = Get-JobBlock $Text 'gate'
                $at = [regex]::Match($gate, '(?m)^        run: \|[ \t]*\r?\n')
                if (-not $at.Success) { return '' }
                $lines = @()
                foreach ($line in ($gate.Substring($at.Index + $at.Length) -split '\r?\n')) {
                    if ($line -match '^          ') { $lines += $line.Substring(10) } elseif ($line.Trim()) { break }
                }
                return ($lines -join "`n") + "`n"
            }
            # Runs the gate's script with the needed jobs' results, returns its exit code.
            function Invoke-Gate([string]$Body, [hashtable]$Results) {
                $file = Join-Path $TestDrive 'gate.sh'
                [IO.File]::WriteAllText($file, $Body)
                $saved = @{}
                foreach ($key in $Results.Keys) { $saved[$key] = [Environment]::GetEnvironmentVariable($key); [Environment]::SetEnvironmentVariable($key, $Results[$key]) }
                try { & $bash ($file -replace '\\', '/') *> $null; return $LASTEXITCODE }
                finally { foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) } }
            }
        }
        It 'waits on every other job, reports on every pull request that touches the lane, and reads the results only through env: <Workflow>' -ForEach @(
            @{ Workflow = 'a11y-check'; Name = 'accessibility check (mac)' }, @{ Workflow = 'swift-tests'; Name = 'swift tests (mac)' }
        ) {
            $text = Get-Content -Raw (Join-Path $wf "$Workflow.yml")
            $gate = Get-JobBlock $text 'gate'
            $gate | Should -Match (Get-Lines @('  gate:', "    name: $Name"))
            $needs = @(Get-NeedsList (Get-JobKey $gate 'needs'))
            ($needs | Sort-Object) -join ',' | Should -BeExactly ((@(Get-JobIds $text) | Where-Object { $_ -ne 'gate' } | Sort-Object) -join ',') -Because 'the gate waits on every job of the lane'
            $gate | Should -Match (Get-JobLine "if: `${{ always() && needs.changes.outputs.run != 'false' }}")
            $gate | Should -Match (Get-JobLine 'permissions: {}')
            foreach ($job in @($needs | Where-Object { $_ -ne 'changes' })) {
                $gate | Should -Match ('(?m)^          [A-Z0-9_]+: ' + [regex]::Escape("`${{ needs.$job.result }}") + '[ \t]*\r?$') -Because "the gate reads $job's result"
            }
            $gate | Should -Match (Get-Lines @("          FORK: `${{ github.event_name == 'pull_request' && github.event.pull_request.head.repo.full_name != github.repository }}"))
            Get-GateScript $text | Should -Not -Match '\$\{\{' -Because 'the results reach the script through env:'
        }
        It 'the accessibility gate fails unless the routing, the analysis and the floor passed, and lets a fork''s pull request skip both' {
            if (-not $bash) { if ($env:GITHUB_ACTIONS -eq 'true') { throw 'bash is required on CI' }; Set-ItResult -Skipped -Because 'no bash (on Windows, Git for Windows'' bash beside git)'; return }
            $body = Get-GateScript (Get-Content -Raw (Join-Path $wf 'a11y-check.yml'))
            $body | Should -Not -BeNullOrEmpty
            foreach ($case in @(
                    @(0, 'success', 'success', 'success', 'false'),
                    @(1, 'failure', 'skipped', 'skipped', 'false'),
                    @(1, 'failure', 'skipped', 'skipped', 'true'),
                    @(0, 'success', 'skipped', 'skipped', 'true'),
                    @(1, 'success', 'skipped', 'skipped', 'false'),
                    @(1, 'success', 'success', 'failure', 'false'),
                    @(1, 'success', 'failure', 'success', 'false'),
                    @(1, 'success', 'success', 'failure', 'true'),
                    @(1, 'success', 'cancelled', 'skipped', 'false'))) {
                $code = Invoke-Gate $body @{ ROUTE_RESULT = $case[1]; ANALYZE_RESULT = $case[2]; FLOOR_RESULT = $case[3]; FORK = $case[4] }
                $code | Should -Be $case[0] -Because "route=$($case[1]) analyze=$($case[2]) floor=$($case[3]) fork=$($case[4])"
            }
        }
        It 'the swift gate fails unless every job passed, and lets a fork''s pull request skip the XCTest suite' {
            if (-not $bash) { if ($env:GITHUB_ACTIONS -eq 'true') { throw 'bash is required on CI' }; Set-ItResult -Skipped -Because 'no bash (on Windows, Git for Windows'' bash beside git)'; return }
            $body = Get-GateScript (Get-Content -Raw (Join-Path $wf 'swift-tests.yml'))
            $body | Should -Not -BeNullOrEmpty
            foreach ($case in @(
                    @(0, 'success', 'success', 'success', 'success', 'false'),
                    @(1, 'failure', 'skipped', 'success', 'success', 'false'),
                    # A fork's skipped suite is accepted only when the routing itself passed.
                    @(1, 'failure', 'skipped', 'success', 'success', 'true'),
                    @(0, 'success', 'skipped', 'success', 'success', 'true'),
                    @(1, 'success', 'skipped', 'success', 'success', 'false'),
                    @(1, 'success', 'failure', 'success', 'success', 'false'),
                    @(1, 'success', 'cancelled', 'success', 'success', 'true'),
                    @(1, 'success', 'success', 'failure', 'success', 'false'),
                    @(1, 'success', 'skipped', 'success', 'skipped', 'true'))) {
                $code = Invoke-Gate $body @{ ROUTE_RESULT = $case[1]; XCTEST_RESULT = $case[2]; SDK_RESULT = $case[3]; BUNDLE_RESULT = $case[4]; FORK = $case[5] }
                $code | Should -Be $case[0] -Because "route=$($case[1]) xctest=$($case[2]) sdk=$($case[3]) bundle=$($case[4]) fork=$($case[5])"
            }
        }
    }
}

Describe 'main runs' {
    # #1565: one pending run per concurrency group, and a later push cancels
    # it, so a lane that groups pushes by branch can drop a main run.
    It 'every lane that runs on a push to main groups pushes by commit and pull requests by branch: <Lane>' -ForEach @(
        @{ Lane = 'a11y-check' }, @{ Lane = 'audit' }, @{ Lane = 'license-headers' }, @{ Lane = 'mac-runner-tests' },
        @{ Lane = 'rust' }, @{ Lane = 'swift-tests' }, @{ Lane = 'windows-runner-tests' }, @{ Lane = 'windows' }
    ) {
        $text = Get-Content -Raw (Join-Path $PSScriptRoot '..' '..' '..' '.github' 'workflows' "$Lane.yml")
        $text | Should -Match '(?m)^concurrency:\r?\n  group: \$\{\{ github\.workflow \}\}-\$\{\{ github\.event_name == ''pull_request'' && github\.ref \|\| github\.sha \}\}\r?\n  cancel-in-progress: \$\{\{ github\.event_name == ''pull_request'' \}\}'
    }
    It 'no workflow that runs on a push to main groups pushes by ref alone' {
        foreach ($file in Get-ChildItem (Join-Path $PSScriptRoot '..' '..' '..' '.github' 'workflows') -Filter '*.yml') {
            $text = Get-Content -Raw $file.FullName
            if ($text -notmatch '(?m)^  push:') { continue }
            $text | Should -Not -Match '(?m)^  group: \$\{\{ github\.workflow \}\}-\$\{\{ github\.ref \}\}\s*$' -Because "$($file.Name) would let a push to main drop a queued main run"
        }
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
