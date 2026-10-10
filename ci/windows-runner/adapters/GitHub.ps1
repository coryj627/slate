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
# Set by orchestrator.ps1 after it dot-sources this file: a scriptblock
# that takes one message and logs it at warn. Test-GhRateBudget calls it.
$script:GhLowBudgetWarning = $null
$script:GhLowBudgetWarnedReset = $null

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
    # ResponseHeadersVariable: the cmdlet sets ghResponseHeaders in this scope (the rate-limit headers).
    $ghResponseHeaders = $null
    $params = @{ Method = $Method; Uri = "$($script:GhBase)$Path"; Headers = $headers; TimeoutSec = 30; OperationTimeoutSeconds = 30; UserAgent = 'slate-ci-host'; Debug = $false; ResponseHeadersVariable = 'ghResponseHeaders' }
    if ($null -ne $Body) {
        $params.Body = $Body | ConvertTo-Json -Compress -Depth 5
        $params.ContentType = 'application/json'
    }
    try {
        $response = Invoke-RestMethod @params
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
    Test-GhRateBudget -Headers $ghResponseHeaders
    return $response
}

function Get-GhHeader {
    # One response header's first value; header names ignore case.
    [CmdletBinding()]
    param($Headers, [Parameter(Mandatory)][string]$Name)
    if ($null -eq $Headers) { return $null }
    foreach ($key in @($Headers.Keys)) {
        if ([string]::Equals([string]$key, $Name, [System.StringComparison]::OrdinalIgnoreCase)) { return (@($Headers[$key]) | Select-Object -First 1) }
    }
    return $null
}

function Test-GhRateBudget {
    # The PAT draws on the owner's personal budget of 5,000 requests an
    # hour, shared with gh and anything else signed in as the owner. When
    # fewer than 500 remain, warn through $script:GhLowBudgetWarning, once
    # per rate-limit window (the reset time names it). A failing warning
    # never fails the call that succeeded.
    [CmdletBinding()]
    param($Headers, [int]$Threshold = 500)
    if ($null -eq $script:GhLowBudgetWarning) { return }
    $remaining = [string](Get-GhHeader -Headers $Headers -Name 'X-RateLimit-Remaining')
    if ($remaining -notmatch '^\d+$' -or [int64]$remaining -ge $Threshold) { return }
    $reset = [string](Get-GhHeader -Headers $Headers -Name 'X-RateLimit-Reset')
    if ($null -ne $script:GhLowBudgetWarnedReset -and $reset -eq $script:GhLowBudgetWarnedReset) { return }
    $script:GhLowBudgetWarnedReset = $reset
    $resetAt = 'an unknown time'
    if ($reset -match '^\d+$') { $resetAt = [datetimeoffset]::FromUnixTimeSeconds([int64]$reset).ToString('o') }
    try {
        & $script:GhLowBudgetWarning "GitHub API budget low: $remaining requests left until the reset at $resetAt (the PAT shares the owner's 5,000 an hour with gh)"
    } catch {
        # Logging is best effort here.
    }
}

function Get-GhQueuedLaneJobs {
    # A run is in_progress while later jobs of it are still queued, so
    # both run states are listed. A run that moves from queued to
    # in_progress between the two listings is in both; it is read once, or
    # its queued jobs would come back twice and one job could be admitted
    # into two VMs. Only the routed workflows (config RoutedWorkflows)
    # carry lane labels, so only their runs' jobs are read. Cost per tick:
    # two listing calls plus one jobs call per queued or in-progress
    # routed run.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]]$RoutedWorkflows)
    $jobs = @()
    $seenRuns = @{}
    foreach ($status in 'queued', 'in_progress') {
        $runs = Invoke-GhApi -Path "/actions/runs?status=$status&per_page=100"
        foreach ($run in @($runs.workflow_runs)) {
            $runKey = [string]$run.id
            if ($seenRuns.ContainsKey($runKey)) { continue }
            $seenRuns[$runKey] = $true
            # path is the workflow file, such as .github/workflows/windows.yml.
            # The part before any @<ref> suffix is compared, exactly (git
            # paths keep their case).
            $workflow = ''
            if ($run.PSObject.Properties['path']) { $workflow = ([string]$run.path -split '@', 2)[0] }
            if ($RoutedWorkflows -cnotcontains $workflow) { continue }
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
    param([Parameter(Mandatory)][string[]]$RoutedWorkflows)
    # As in New-HyperVAdapters: GetNewClosure() runs a block in a new
    # dynamic module that sees only its captured locals and the global
    # scope, so the functions are captured as locals too and invoked with
    # &; each still runs in the scope that defined it.
    $commands = @{}
    foreach ($command in 'Get-GhQueuedLaneJobs', 'Get-GhJob', 'Get-GhRun', 'New-GhJitRunner', 'Remove-GhRunner', 'Get-GhRunner', 'Get-GhRunners') {
        $commands[$command] = Get-Command -Name $command -CommandType Function -ErrorAction Stop
    }
    $routed = @($RoutedWorkflows)
    $adapters = @{
        GetQueuedJobs = { & $commands['Get-GhQueuedLaneJobs'] -RoutedWorkflows $routed }
        GetJob        = { param($id) & $commands['Get-GhJob'] -JobId $id }
        GetRun        = { param($id) & $commands['Get-GhRun'] -RunId $id }
        NewJitRunner  = { param($name, $labels) & $commands['New-GhJitRunner'] -Name $name -Labels $labels }
        RemoveRunner  = { param($id) & $commands['Remove-GhRunner'] -RunnerId $id }
        GetRunner     = { param($id) & $commands['Get-GhRunner'] -RunnerId $id }
        ListRunners   = { & $commands['Get-GhRunners'] }
    }
    # Bind the routed workflows and the commands into each scriptblock: the
    # module invokes them long after this function has returned.
    foreach ($key in @($adapters.Keys)) { $adapters[$key] = $adapters[$key].GetNewClosure() }
    return $adapters
}
