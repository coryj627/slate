# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Text-level guards over the workflows that pick Windows runners
# and over what branch-cleanup.yml owns (the weekly claude/* branch report;
# its script's behaviour is BranchCleanup.Tests.ps1). YAML parsing happens
# on push (GitHub refuses a malformed workflow at the first run); these
# tests pin the runner contract and the delete switch themselves, and the
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
    # The Namespace fallback rule every build/test lane and the nightly stress job share.
    $script:ns = "(vars.WINDOWS_RUNNER_MODE == 'namespace' || github.run_attempt != '1')"
    function Get-RunsOn([string]$Tag) {
        "runs-on: `${{ $ns && format('namespace-profile-winx64-fast{0};overrides.cache-tag=$Tag', github.ref != 'refs/heads/main' && '-pr' || '') || 'windows-latest' }}"
    }
    # One whole line at job-key indentation, so a step-level copy of it does not count.
    function Get-JobLine([string]$Line) { '(?m)^    ' + [regex]::Escape($Line) + '[ \t]*\r?$' }
    # Consecutive whole lines, each verbatim.
    function Get-Lines([string[]]$Lines) { '(?m)^' + (($Lines | ForEach-Object { [regex]::Escape($_) }) -join '[ \t]*\r?\n') + '[ \t]*\r?$' }
    # The steps of a job block, each from its `      - ` to the next one or the end of the block.
    function Get-Steps([string]$Block) { @([regex]::Matches($Block, '(?ms)^      - .*?(?=^      - |\z)') | ForEach-Object { $_.Value }) }
}

Describe 'windows.yml runners: GitHub-hosted first, Namespace as the fallback' {
    It 'has no routing job and no lane that waits on one' {
        Get-JobBlock $windows 'route' | Should -BeNullOrEmpty
        $windows | Should -Not -Match 'needs\.route' -Because 'nothing writes route outputs any more'
        $windows | Should -Not -Match 'ci/windows-runner/route' -Because 'the router no longer starts the lane'
        $windows | Should -Not -Match '(?m)^[ \t]*[^#\s][^\r\n]*slate-win-' -Because 'the home pool is parked (the header may still name it)'
    }
    It 'every build/test lane runs on windows-latest unless the mode is namespace or the run is a re-run, with its own Namespace cache tag' {
        foreach ($pair in @(@('rust-tests', 'slate-windows-rust'), @('windows', 'slate-windows-app'), @('windows-model-shard', 'slate-windows-model'))) {
            $block = Get-JobBlock $windows $pair[0]
            $block | Should -Match (Get-JobLine (Get-RunsOn $pair[1])) -Because "lane $($pair[0])"
            $block | Should -Not -Match '(?m)^    needs:' -Because "lane $($pair[0]) starts at once"
        }
        ([regex]::Matches($windows, [regex]::Escape($ns))).Count | Should -Be 3
    }
    It 'gates every cache step on the runner the lane actually got' {
        foreach ($job in 'rust-tests', 'windows', 'windows-model-shard') {
            $steps = Get-Steps (Get-JobBlock $windows $job)
            $namespaceSteps = @($steps | Where-Object { $_.Contains('namespacelabs/nscloud-cache-action@') -or $_.Contains('$env:NSC_CACHE_PATH') })
            $namespaceSteps.Count | Should -BeGreaterThan 0
            foreach ($step in $namespaceSteps) {
                $step | Should -Match "(?m)^        if: \`$\{\{ (always\(\) && )?runner\.environment != 'github-hosted' \}\}[ \t]*\r?$" -Because "a Namespace cache step of $job"
            }
            $githubSteps = @($steps | Where-Object { $_ -match 'uses: actions/cache(/restore|/save)?@' -and -not $_.Contains('uniffi-bindgen-cs.exe') })
            $githubSteps.Count | Should -BeGreaterThan 0
            foreach ($step in $githubSteps) {
                $step | Should -Match "(?m)^        if: \`$\{\{ .*runner\.environment == 'github-hosted'.* \}\}[ \t]*\r?$" -Because "a GitHub cache step of $job"
            }
        }
        ([regex]::Matches($windows, 'namespacelabs/nscloud-cache-action')).Count | Should -Be 3
    }
    It 'saves the GitHub cache only from the rust and app lanes, only on main pushes, under per-attempt keys' {
        ([regex]::Matches($windows, '(?m)^        uses: actions/cache/save@')).Count | Should -Be 2
        Get-JobBlock $windows 'windows-model-shard' | Should -Not -Match 'actions/cache/save@' -Because 'the shards only read the app lane''s entries'
        foreach ($pair in @(@('rust-tests', 'tests'), @('windows', 'app'))) {
            $block = Get-JobBlock $windows $pair[0]
            $block.Contains("if: `${{ !cancelled() && runner.environment == 'github-hosted' && github.event_name == 'push' && github.ref == 'refs/heads/main' }}") | Should -BeTrue
            $block | Should -Match ([regex]::Escape("key: windows-cargo-$($pair[1])-") + '.*' + [regex]::Escape('-${{ github.sha }}-${{ github.run_id }}-${{ github.run_attempt }}'))
        }
        Get-JobBlock $windows 'windows-model-shard' | Should -Match ([regex]::Escape('key: windows-cargo-app-'))
    }
    It 'runs the shell gate on windows-latest in every mode, only after a green app build' {
        $flaui = Get-JobBlock $windows 'flaui'
        $flaui | Should -Match (Get-JobLine 'runs-on: windows-latest')
        $flaui | Should -Match (Get-JobLine "if: `${{ !cancelled() && needs.windows.result == 'success' }}")
    }
    It 'every build/test lane prints its runner first, and the mode only through the environment' {
        $record = '    steps:', '      - name: Record the runner', '        env:', '          WINDOWS_RUNNER_MODE: ${{ vars.WINDOWS_RUNNER_MODE }}',
            '        run: Write-Host "runner=${{ runner.name }} environment=${{ runner.environment }} attempt=${{ github.run_attempt }} mode=''$env:WINDOWS_RUNNER_MODE''"'
        foreach ($job in 'rust-tests', 'windows', 'windows-model-shard') {
            (Get-JobBlock $windows $job) | Should -Match (Get-Lines $record) -Because "$job's first step names the runner it got"
        }
    }
    It 'the gate waits on every lane, runs whatever they did, and names the mode and attempt when it fails' {
        $gate = Get-JobBlock $windows 'gate'
        $gate | Should -Match ([regex]::Escape('needs: [rust-tests, windows, windows-model, flaui]'))
        # Without always() a skipped lane skips the gate, and a skipped required check counts as passing.
        $gate | Should -Match (Get-JobLine 'if: ${{ always() }}')
        $gate.Contains('mode=''$WINDOWS_RUNNER_MODE'' attempt=${{ github.run_attempt }}"') | Should -BeTrue
        $gate | Should -Match ('(?m)^          ' + [regex]::Escape('WINDOWS_RUNNER_MODE: ${{ vars.WINDOWS_RUNNER_MODE }}') + '[ \t]*\r?$')
    }
    It 'compares the variable only with namespace' {
        $reads = @([regex]::Matches($windows + "`n" + $nightly, "vars\.WINDOWS_RUNNER_MODE (==|!=) '([^']*)'"))
        $reads.Count | Should -Be 4 -Because 'windows.yml: three lanes; nightly.yml: the stress job'
        foreach ($read in $reads) { $read.Groups[2].Value | Should -Be 'namespace' }
    }
    It 'documents the modes, the re-run rule and the parked home pool in the header' {
        $end = $windows.IndexOf("`nname: windows")
        $end | Should -BeGreaterThan 0
        $header = $windows.Substring(0, $end)
        $header | Should -Not -Match '(?m)^[ \t]*[^#\s]' -Because 'everything before name: windows is the header comment'
        $header | Should -Match 'self-hosted-windows-runner\.md'
        $joined = $header -replace '\r?\n#\s*', ' '
        foreach ($phrase in 'GitHub-hosted windows-latest first, Namespace as the fallback', 'exactly `namespace`', 'github.run_attempt above 1', 'is parked', 'a5037b35') {
            $joined | Should -Match ([regex]::Escape($phrase))
        }
    }
}

Describe 'nightly.yml pool switch' {
    It 'runs the Windows stress job on windows-latest unless the mode is namespace or the run is a re-run, and restores the GitHub cache on both' {
        $nightly.Contains("runs-on: `${{ $ns && 'nscloud-windows-2022-amd64-8x16' || 'windows-latest' }}") | Should -BeTrue
        $nightly | Should -Not -Match 'slate-win-'
        $stress = Get-JobBlock $nightly 'windows-full-stress'
        $stress | Should -Match 'uses: \./\.github/actions/windows-native-build[ \t]*\r?\n'
        $stress | Should -Not -Match '(?m)^          cache:' -Because 'the action''s default restores the GitHub cache'
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
    It 'runs the branch-cleanup behaviour test when its script changes, on pull requests and on main' {
        ([regex]::Matches($lane, (Get-Lines @("      - 'ci/branch-cleanup/**'")))).Count | Should -Be 2
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
        $pin = [regex]::Match($lane, 'uses: actions/checkout@[0-9a-f]{40}  # v\d+').Value
        $pin | Should -Not -BeNullOrEmpty
        $steps = Get-Steps $report
        $steps.Count | Should -Be 2
        $steps[0] | Should -Match (Get-Lines @("        $pin", '        with:', '          sparse-checkout: ci/branch-cleanup', '          persist-credentials: false'))
        $steps[1] | Should -Match (Get-Lines @('        run: bash ci/branch-cleanup/branch-cleanup.sh'))
        ([regex]::Matches($cleanup, '(?m)^[ \t]*(?:- )?run:')).Count | Should -Be 1
    }
}
