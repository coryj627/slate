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

Describe 'windows-runner-tests.yml path filters' {
    It 'runs these guards when a routed workflow changes, on pull requests and on main' {
        foreach ($routed in @('windows.yml', 'nightly.yml', 'windows-ci-pilot.yml')) {
            ([regex]::Matches($lane, [regex]::Escape("- '.github/workflows/$routed'"))).Count | Should -Be 2 -Because "$routed must be in both the pull_request and push path filters"
        }
    }
}
