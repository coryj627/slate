# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    . (Join-Path $PSScriptRoot '..' 'adapters' 'GitHub.ps1')
    Initialize-GitHubAdapter -Owner 'coryj627' -Repo 'slate' -Token (ConvertTo-SecureString 'ghp_test' -AsPlainText -Force)
    $script:routed = @('.github/workflows/windows.yml', '.github/workflows/nightly.yml', '.github/workflows/windows-ci-pilot.yml')

    function Set-ResponseHeaders([string]$Name, $Headers) {
        # Plays the cmdlet's part in a mock: -ResponseHeadersVariable sets
        # the named variable in its caller's scope (Invoke-GhApi's), which
        # sits some scopes above a mock body. Invoke-GhApi creates it first.
        for ($scope = 1; $scope -lt 64; $scope++) {
            try { $null = Get-Variable -Name $Name -Scope $scope -ErrorAction Stop } catch { continue }
            Set-Variable -Name $Name -Value $Headers -Scope $scope
            return
        }
        throw "no caller scope holds `$$Name"
    }
}

Describe 'Invoke-GhApi' {
    It 'sends bearer auth, the API version header and the repository base' {
        Mock Invoke-RestMethod { [pscustomobject]@{ ok = $true } }
        Invoke-GhApi -Path '/actions/runners' | Out-Null
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
            $Uri -eq 'https://api.github.com/repos/coryj627/slate/actions/runners' -and
            $Method -eq 'GET' -and
            $Headers.Authorization -eq 'Bearer ghp_test' -and
            $Headers['X-GitHub-Api-Version'] -eq '2022-11-28' -and
            $Headers.Accept -eq 'application/vnd.github+json' -and
            $OperationTimeoutSeconds -eq 30
        }
    }
    It 'serialises a body as JSON for POST' {
        Mock Invoke-RestMethod { [pscustomobject]@{} }
        Invoke-GhApi -Method 'POST' -Path '/x' -Body @{ a = 1 } | Out-Null
        Should -Invoke Invoke-RestMethod -Times 1 -ParameterFilter { $Method -eq 'POST' -and $ContentType -eq 'application/json' -and ($Body | ConvertFrom-Json).a -eq 1 }
    }
    It 'never enables debug tracing' {
        # Pester does not set $PSBoundParameters inside a ParameterFilter;
        # $PesterBoundParameters holds the call's bound parameters.
        Mock Invoke-RestMethod { [pscustomobject]@{} }
        Invoke-GhApi -Path '/actions/runners' -Debug | Out-Null
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter { $PesterBoundParameters.ContainsKey('Debug') -and -not $Debug }
    }
    It 'removes the token from the request kept in the error record' {
        Mock Invoke-RestMethod {
            $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, 'https://api.github.com/repos/coryj627/slate/x')
            $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', 'ghp_test')
            Write-Error -Message 'boom' -TargetObject $request -ErrorAction Stop
        }
        $record = $null
        try { Invoke-GhApi -Path '/x' } catch { $record = $_ }
        "$record" | Should -Be 'boom'
        $record.TargetObject | Should -BeOfType [System.Net.Http.HttpRequestMessage]
        $record.TargetObject.Headers.Contains('Authorization') | Should -BeFalse
        "$($record.TargetObject)" | Should -Not -BeLike '*ghp_test*'
    }
}

Describe 'Invoke-GhApi rate budget' {
    BeforeEach {
        $script:budgetWarnings = [System.Collections.ArrayList]::new()
        $script:GhLowBudgetWarning = { param($message) [void]$script:budgetWarnings.Add($message) }
        $script:GhLowBudgetWarnedReset = $null
    }
    AfterAll {
        $script:GhLowBudgetWarning = $null
        $script:GhLowBudgetWarnedReset = $null
    }
    It 'reads the response headers and warns, with the reset time, when fewer than 500 requests remain' {
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('499'); 'X-RateLimit-Reset' = @('1791640800') }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/actions/runners').ok | Should -BeTrue
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter { $ResponseHeadersVariable -eq 'ghResponseHeaders' }
        $budgetWarnings.Count | Should -Be 1
        $budgetWarnings[0] | Should -Match '\b499 requests left\b'
        $budgetWarnings[0] | Should -Match ([regex]::Escape([datetimeoffset]::FromUnixTimeSeconds(1791640800).ToString('o')))
        $budgetWarnings[0] | Should -Match '5,000'
    }
    It 'matches the header names whatever their case' {
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'x-ratelimit-remaining' = @('12'); 'x-ratelimit-reset' = @('1791640800') }
            [pscustomobject]@{}
        }
        Invoke-GhApi -Path '/x' | Out-Null
        $budgetWarnings.Count | Should -Be 1
        $budgetWarnings[0] | Should -Match '\b12 requests left\b'
    }
    It 'warns once per rate-limit window, and again in the next one' {
        $script:reset = '1791640800'
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('400'); 'X-RateLimit-Reset' = @($script:reset) }
            [pscustomobject]@{}
        }
        1..3 | ForEach-Object { Invoke-GhApi -Path '/x' | Out-Null }
        $budgetWarnings.Count | Should -Be 1
        $script:reset = '1791644400'
        Invoke-GhApi -Path '/x' | Out-Null
        $budgetWarnings.Count | Should -Be 2
    }
    It 'stays quiet with 500 or more left, without the headers, or when no warning is wired' {
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('500'); 'X-RateLimit-Reset' = @('1791640800') }
            [pscustomobject]@{}
        }
        Invoke-GhApi -Path '/x' | Out-Null
        Mock Invoke-RestMethod { [pscustomobject]@{} }
        Invoke-GhApi -Path '/x' | Out-Null
        $budgetWarnings.Count | Should -Be 0
        $script:GhLowBudgetWarning = $null
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('1'); 'X-RateLimit-Reset' = @('1791640800') }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/x').ok | Should -BeTrue
    }
    It 'never turns a successful call into a failure over a malformed budget header' {
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('99999999999999999999'); 'X-RateLimit-Reset' = @('1791640800') }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/x').ok | Should -BeTrue
        $budgetWarnings.Count | Should -Be 0
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('7'); 'X-RateLimit-Reset' = @('99999999999999999') }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/x').ok | Should -BeTrue
        $budgetWarnings.Count | Should -Be 1
        $budgetWarnings[0] | Should -Match 'an unknown time'
    }
    It 'never turns a successful call into a failure when the warning itself throws' {
        $script:GhLowBudgetWarning = { param($message) throw 'log volume full' }
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('3'); 'X-RateLimit-Reset' = @('1791640800') }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/x').ok | Should -BeTrue
    }
}

Describe 'New-GhJitRunner' {
    It 'posts name, runner_group_id 1, labels and work_folder, returning id and config' {
        Mock Invoke-RestMethod { [pscustomobject]@{ runner = [pscustomobject]@{ id = 77 }; encoded_jit_config = 'abc' } }
        $r = New-GhJitRunner -Name 'slate-win-app-deadbeef' -Labels @('slate-win-app')
        $r.RunnerId | Should -Be 77
        $r.EncodedJitConfig | Should -Be 'abc'
        Should -Invoke Invoke-RestMethod -Times 1 -ParameterFilter {
            $body = $Body | ConvertFrom-Json
            $Method -eq 'POST' -and $Uri -like '*/actions/runners/generate-jitconfig' -and
            $body.name -eq 'slate-win-app-deadbeef' -and $body.runner_group_id -eq 1 -and
            @($body.labels) -contains 'slate-win-app' -and $body.work_folder -eq '_work'
        }
    }
}

Describe 'Get-GhQueuedLaneJobs' {
    It 'lists queued and in-progress runs and returns only their queued jobs' {
        Mock Invoke-RestMethod {
            if ($Uri -like '*actions/runs?status=queued*') { return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 1; path = '.github/workflows/windows.yml' }) } }
            if ($Uri -like '*actions/runs?status=in_progress*') { return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 2; path = '.github/workflows/nightly.yml' }) } }
            if ($Uri -like '*/runs/1/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 11; run_id = 1; status = 'queued'; labels = @('slate-win-app'); created_at = '2026-10-10T10:00:00Z' }) }
            }
            if ($Uri -like '*/runs/2/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @(
                    [pscustomobject]@{ id = 21; run_id = 2; status = 'in_progress'; labels = @('slate-win-rust'); created_at = '2026-10-10T10:00:00Z' },
                    [pscustomobject]@{ id = 22; run_id = 2; status = 'queued'; labels = @('slate-win-model'); created_at = '2026-10-10T10:00:01Z' }
                ) }
            }
            throw "unexpected $Uri"
        }
        $jobs = @(Get-GhQueuedLaneJobs -RoutedWorkflows $routed)
        @($jobs | ForEach-Object { $_.id }) | Should -Be @(11, 22)
        Should -Invoke Invoke-RestMethod -Times 4 -Exactly
    }
    It 'returns an empty array when nothing is queued' {
        Mock Invoke-RestMethod { [pscustomobject]@{ workflow_runs = @() } }
        @(Get-GhQueuedLaneJobs -RoutedWorkflows $routed).Count | Should -Be 0
    }
    It 'returns a run''s queued jobs once when it appears in both listings' {
        # The run moved from queued to in_progress between the two listings.
        Mock Invoke-RestMethod {
            if ($Uri -like '*actions/runs?status=*') { return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 1; path = '.github/workflows/windows.yml' }) } }
            if ($Uri -like '*/runs/1/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 11; run_id = 1; status = 'queued'; labels = @('slate-win-app'); created_at = '2026-10-10T10:00:00Z' }) }
            }
            throw "unexpected $Uri"
        }
        $jobs = @(Get-GhQueuedLaneJobs -RoutedWorkflows $routed)
        $jobs.Count | Should -Be 1
        $jobs[0].id | Should -Be 11
        Should -Invoke Invoke-RestMethod -Times 3 -Exactly
    }
    It 'never reads the jobs of a run whose workflow is not routed' {
        # Every other workflow's runs cost a jobs call per tick for nothing:
        # only the routed workflows carry slate-win-* labels.
        Mock Invoke-RestMethod {
            if ($Uri -like '*actions/runs?status=queued*') {
                return [pscustomobject]@{ workflow_runs = @(
                    [pscustomobject]@{ id = 1; path = '.github/workflows/rust.yml' },
                    [pscustomobject]@{ id = 2; path = '.github/workflows/windows-ci-pilot.yml' },
                    [pscustomobject]@{ id = 3 }
                ) }
            }
            if ($Uri -like '*actions/runs?status=in_progress*') {
                return [pscustomobject]@{ workflow_runs = @(
                    [pscustomobject]@{ id = 4; path = '.github/workflows/Windows.yml' },
                    [pscustomobject]@{ id = 5; path = '.github/workflows/windows.yml@refs/heads/main' }
                ) }
            }
            if ($Uri -like '*/runs/2/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 21; run_id = 2; status = 'queued'; labels = @('slate-win-app'); created_at = '2026-10-10T10:00:00Z' }) }
            }
            if ($Uri -like '*/runs/5/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 51; run_id = 5; status = 'queued'; labels = @('slate-win-rust'); created_at = '2026-10-10T10:00:01Z' }) }
            }
            throw "unexpected $Uri"
        }
        $jobs = @(Get-GhQueuedLaneJobs -RoutedWorkflows $routed)
        @($jobs | ForEach-Object { $_.id }) | Should -Be @(21, 51)
        foreach ($skipped in 1, 3, 4) {
            Should -Invoke Invoke-RestMethod -Times 0 -Exactly -ParameterFilter { $Uri -like "*/runs/$skipped/jobs*" }
        }
        Should -Invoke Invoke-RestMethod -Times 4 -Exactly
    }
    It 'requires the routed workflow list' {
        $parameter = (Get-Command Get-GhQueuedLaneJobs).Parameters['RoutedWorkflows']
        @($parameter.Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] } | ForEach-Object { $_.Mandatory }) | Should -Contain $true
    }
}

Describe 'Get-GhRunner' {
    It 'returns null on 404 and rethrows anything else' {
        Mock Invoke-RestMethod {
            $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::NotFound)
            throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('not found', $response)
        }
        Get-GhRunner -RunnerId 5 | Should -BeNullOrEmpty
        Mock Invoke-RestMethod {
            $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::InternalServerError)
            throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('boom', $response)
        }
        { Get-GhRunner -RunnerId 5 } | Should -Throw
    }
}

Describe 'Remove-GhRunner / Get-GhRunners' {
    It 'deletes by id and lists runners' {
        Mock Invoke-RestMethod { [pscustomobject]@{ runners = @([pscustomobject]@{ id = 1; name = 'slate-win-app-a'; status = 'offline' }) } }
        Remove-GhRunner -RunnerId 9
        Should -Invoke Invoke-RestMethod -Times 1 -ParameterFilter { $Method -eq 'DELETE' -and $Uri -like '*/actions/runners/9' }
        @(Get-GhRunners).Count | Should -Be 1
    }
}

Describe 'New-GitHubAdapters' {
    It 'exposes exactly the keys the orchestrator invokes' {
        @((New-GitHubAdapters -RoutedWorkflows $routed).Keys | Sort-Object) | Should -Be @('GetJob', 'GetQueuedJobs', 'GetRun', 'GetRunner', 'ListRunners', 'NewJitRunner', 'RemoveRunner')
    }
    It 'requires the routed workflow list' {
        $parameter = (Get-Command New-GitHubAdapters).Parameters['RoutedWorkflows']
        @($parameter.Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] } | ForEach-Object { $_.Mandatory }) | Should -Contain $true
    }
    It 'binds the routed workflows into the closures the module invokes later' {
        Mock Invoke-RestMethod {
            if ($Uri -like '*actions/runs?status=queued*') {
                return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 1; path = '.github/workflows/windows.yml' }, [pscustomobject]@{ id = 2; path = '.github/workflows/nightly.yml' }) }
            }
            if ($Uri -like '*actions/runs?status=in_progress*') { return [pscustomobject]@{ workflow_runs = @() } }
            if ($Uri -like '*/runs/1/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 11; run_id = 1; status = 'queued'; labels = @('slate-win-app'); created_at = '2026-10-10T10:00:00Z' }) }
            }
            if ($Uri -like '*/actions/jobs/7') { return [pscustomobject]@{ id = 7; runner_name = 'slate-win-app-deadbeef' } }
            throw "unexpected $Uri"
        }
        $a = New-GitHubAdapters -RoutedWorkflows @('.github/workflows/windows.yml')
        @(& $a.GetQueuedJobs | ForEach-Object { $_.id }) | Should -Be @(11)
        Should -Invoke Invoke-RestMethod -Times 0 -Exactly -ParameterFilter { $Uri -like '*/runs/2/jobs*' }
        (& $a.GetJob 7).runner_name | Should -Be 'slate-win-app-deadbeef'
    }
    It 'refuses to work before initialisation' {
        . (Join-Path $PSScriptRoot '..' 'adapters' 'GitHub.ps1')
        { Invoke-GhApi -Path '/x' } | Should -Throw '*Initialize-GitHubAdapter*'
        Initialize-GitHubAdapter -Owner 'coryj627' -Repo 'slate' -Token (ConvertTo-SecureString 'ghp_test' -AsPlainText -Force)
    }
}
