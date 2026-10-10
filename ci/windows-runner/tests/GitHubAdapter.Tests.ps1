# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    . (Join-Path $PSScriptRoot '..' 'adapters' 'GitHub.ps1')
    Initialize-GitHubAdapter -Owner 'coryj627' -Repo 'slate' -Token (ConvertTo-SecureString 'ghp_test' -AsPlainText -Force)
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
            if ($Uri -like '*actions/runs?status=queued*') { return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 1 }) } }
            if ($Uri -like '*actions/runs?status=in_progress*') { return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 2 }) } }
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
        $jobs = @(Get-GhQueuedLaneJobs)
        @($jobs | ForEach-Object { $_.id }) | Should -Be @(11, 22)
        Should -Invoke Invoke-RestMethod -Times 4 -Exactly
    }
    It 'returns an empty array when nothing is queued' {
        Mock Invoke-RestMethod { [pscustomobject]@{ workflow_runs = @() } }
        @(Get-GhQueuedLaneJobs).Count | Should -Be 0
    }
    It 'returns a run''s queued jobs once when it appears in both listings' {
        # The run moved from queued to in_progress between the two listings.
        Mock Invoke-RestMethod {
            if ($Uri -like '*actions/runs?status=*') { return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 1 }) } }
            if ($Uri -like '*/runs/1/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 11; run_id = 1; status = 'queued'; labels = @('slate-win-app'); created_at = '2026-10-10T10:00:00Z' }) }
            }
            throw "unexpected $Uri"
        }
        $jobs = @(Get-GhQueuedLaneJobs)
        $jobs.Count | Should -Be 1
        $jobs[0].id | Should -Be 11
        Should -Invoke Invoke-RestMethod -Times 3 -Exactly
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
        @((New-GitHubAdapters).Keys | Sort-Object) | Should -Be @('GetJob', 'GetQueuedJobs', 'GetRun', 'GetRunner', 'ListRunners', 'NewJitRunner', 'RemoveRunner')
    }
    It 'refuses to work before initialisation' {
        . (Join-Path $PSScriptRoot '..' 'adapters' 'GitHub.ps1')
        { Invoke-GhApi -Path '/x' } | Should -Throw '*Initialize-GitHubAdapter*'
        Initialize-GitHubAdapter -Owner 'coryj627' -Repo 'slate' -Token (ConvertTo-SecureString 'ghp_test' -AsPlainText -Force)
    }
}
