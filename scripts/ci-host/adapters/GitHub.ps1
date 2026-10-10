#Requires -Version 7.4
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
    # Decoded inline, never into a variable: Set-PSDebug -Trace 2 prints every assignment.
    $headers = @{
        Authorization          = 'Bearer ' + [System.Net.NetworkCredential]::new('', $script:GhToken).Password
        Accept                 = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
    }
    # Debug = $false: the web cmdlets' debug stream prints the request
    # headers (the token) and the response body (a JIT config), so neither
    # -Debug nor an inherited $DebugPreference may trace this call.
    # OperationTimeoutSeconds: since 7.4 TimeoutSec bounds only the connection and response headers; a stalled body would block the loop.
    $params = @{ Method = $Method; Uri = "$($script:GhBase)$Path"; Headers = $headers; TimeoutSec = 30; OperationTimeoutSeconds = 30; UserAgent = 'slate-ci-host'; Debug = $false }
    if ($null -ne $Body) {
        $params.Body = $Body | ConvertTo-Json -Compress -Depth 5
        $params.ContentType = 'application/json'
    }
    try {
        return Invoke-RestMethod @params
    } catch {
        # A failed request's error record keeps the request as its
        # TargetObject (the same object as the response's RequestMessage),
        # whose ToString() prints the Authorization header, so Get-Error,
        # Format-List * or serialising the record would show the token.
        # Drop the header before rethrowing; the status code stays readable
        # for Get-GhRunner. A plain exception has no TargetObject.
        if ($_.TargetObject -is [System.Net.Http.HttpRequestMessage]) { [void]$_.TargetObject.Headers.Remove('Authorization') }
        throw
    }
}

function Get-GhQueuedLaneJobs {
    # A run is in_progress while later jobs of it are still queued, so
    # both run states are listed. Idle cost: two calls per tick. A run
    # that moves from queued to in_progress between the two listings is
    # in both; it is read once, or its queued jobs would come back twice
    # and one job could be admitted into two VMs.
    [CmdletBinding()]
    param()
    $jobs = @()
    $seenRuns = @{}
    foreach ($status in 'queued', 'in_progress') {
        $runs = Invoke-GhApi -Path "/actions/runs?status=$status&per_page=100"
        foreach ($run in @($runs.workflow_runs)) {
            $runKey = [string]$run.id
            if ($seenRuns.ContainsKey($runKey)) { continue }
            $seenRuns[$runKey] = $true
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
