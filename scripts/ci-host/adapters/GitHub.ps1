#Requires -Version 7
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# GitHub REST adapter for the self-hosted runner host. The only file that
# talks to api.github.com. The PAT (fine-grained: Actions read,
# Administration read/write, this repository only) is held as a
# SecureString and decoded per call; nothing here ever logs it or a JIT
# config. orchestrator.ps1 dot-sources this file and passes
# New-GitHubAdapters into the module.

$script:GhBase = $null
$script:GhToken = $null

function Initialize-GitHubAdapter {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Owner,
        [Parameter(Mandatory)][string]$Repo,
        [Parameter(Mandatory)][securestring]$Token
    )
    $script:GhBase = "https://api.github.com/repos/$Owner/$Repo"
    $script:GhToken = $Token
}

function Invoke-GhApi {
    [CmdletBinding()]
    param([string]$Method = 'GET', [Parameter(Mandatory)][string]$Path, $Body)
    if (-not $script:GhBase) { throw 'Initialize-GitHubAdapter has not been called' }
    $plain = [System.Net.NetworkCredential]::new('', $script:GhToken).Password
    $headers = @{
        Authorization          = "Bearer $plain"
        Accept                 = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
    }
    $params = @{ Method = $Method; Uri = "$($script:GhBase)$Path"; Headers = $headers; TimeoutSec = 30; UserAgent = 'slate-ci-host' }
    if ($null -ne $Body) {
        $params.Body = $Body | ConvertTo-Json -Compress -Depth 5
        $params.ContentType = 'application/json'
    }
    return Invoke-RestMethod @params
}

function Get-GhQueuedLaneJobs {
    # A run is in_progress while later jobs of it are still queued, so
    # both run states are listed. Idle cost: two calls per tick.
    [CmdletBinding()]
    param()
    $jobs = @()
    foreach ($status in 'queued', 'in_progress') {
        $runs = Invoke-GhApi -Path "/actions/runs?status=$status&per_page=100"
        foreach ($run in @($runs.workflow_runs)) {
            $page = Invoke-GhApi -Path "/actions/runs/$($run.id)/jobs?filter=latest&per_page=100"
            foreach ($job in @($page.jobs)) {
                if ($job.status -eq 'queued') { $jobs += $job }
            }
        }
    }
    return @($jobs)
}

function Get-GhJob {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$JobId)
    return Invoke-GhApi -Path "/actions/jobs/$JobId"
}

function Get-GhRun {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$RunId)
    return Invoke-GhApi -Path "/actions/runs/$RunId"
}

function New-GhJitRunner {
    # runner_group_id 1 is the default group of a user-owned repository.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string[]]$Labels)
    $response = Invoke-GhApi -Method 'POST' -Path '/actions/runners/generate-jitconfig' -Body @{
        name = $Name; runner_group_id = 1; labels = @($Labels); work_folder = '_work'
    }
    return @{ RunnerId = [int64]$response.runner.id; EncodedJitConfig = [string]$response.encoded_jit_config }
}

function Remove-GhRunner {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$RunnerId)
    Invoke-GhApi -Method 'DELETE' -Path "/actions/runners/$RunnerId" | Out-Null
}

function Get-GhRunner {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$RunnerId)
    try {
        return Invoke-GhApi -Path "/actions/runners/$RunnerId"
    } catch {
        $response = $_.Exception.Response
        if ($null -ne $response -and [int]$response.StatusCode -eq 404) { return $null }
        throw
    }
}

function Get-GhRunners {
    [CmdletBinding()]
    param()
    return @((Invoke-GhApi -Path '/actions/runners?per_page=100').runners)
}

function New-GitHubAdapters {
    [CmdletBinding()]
    param()
    return @{
        GetQueuedJobs = { Get-GhQueuedLaneJobs }
        GetJob        = { param($id) Get-GhJob -JobId $id }
        GetRun        = { param($id) Get-GhRun -RunId $id }
        NewJitRunner  = { param($name, $labels) New-GhJitRunner -Name $name -Labels $labels }
        RemoveRunner  = { param($id) Remove-GhRunner -RunnerId $id }
        GetRunner     = { param($id) Get-GhRunner -RunnerId $id }
        ListRunners   = { Get-GhRunners }
    }
}
