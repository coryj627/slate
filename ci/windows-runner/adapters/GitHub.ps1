#Requires -Version 7.4
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# GitHub REST adapter for the self-hosted runner host. The only file that
# talks to api.github.com. The PAT (fine-grained, this repository only,
# issued with the mac controller's permission set: Actions read,
# Administration read and write, Variables read and write for the
# published state; GitHub adds Metadata read by itself) is held as a
# SecureString and decoded per call; nothing here ever logs it or a JIT
# config. orchestrator.ps1 dot-sources this file and passes
# New-GitHubAdapters into the module.

$script:GhBase = $null
$script:GhToken = $null
# Set by orchestrator.ps1 after it dot-sources this file: a scriptblock
# that takes one message and logs it at warn. Test-GhRateBudget calls it.
$script:GhLowBudgetWarning = $null
$script:GhLowBudgetWarnedReset = $null
# Set by orchestrator.ps1 too: a scriptblock that takes one message and
# logs it at info. Invoke-GhApi reports through it a GET that was answered
# on a retry, and one it gave up on.
$script:GhRetryNotice = $null
# A GET that fails at the connection level is tried again after each of
# these waits, in seconds: three attempts in all; the waits add 3 s to a
# tick, and a fast failure's own time is bounded below.
# Only a fast failure is: an attempt that ran this long or longer before
# failing (a connect that Windows gives up after 21 s, a DNS resolver that
# times out) is not waited for three times over.
$script:GhRetryDelays = @(1, 2)
$script:GhRetryFastFailureSeconds = 5
# Path -> @{ ETag; Body; UsedAt } for the conditional listing calls.
$script:GhEtags = @{}

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

function Test-GhConnectionFailure {
    # True when a failed call never got GitHub's answer: the connection was
    # reset, refused, or dropped, or the name did not resolve. A failure
    # before the response keeps the cmdlet's HttpRequestException wrapper
    # (its inner IOException or SocketException says what happened; the
    # cmdlet copies that message into the record, which is what the log
    # shows); a reset or early end mid-body arrives bare, as an IOException.
    # Not one: a timeout (TaskCanceledException or TimeoutException; a
    # stalled body must not be waited for three times over), a TLS failure
    # (AuthenticationException), and an HTTP status (HttpResponseException,
    # no inner exception), which is GitHub's answer for the caller to read.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$ErrorRecord)
    $exception = $ErrorRecord.Exception
    if ($exception -is [System.Net.Http.HttpRequestException]) { $exception = $exception.InnerException }
    return ($exception -is [System.IO.IOException] -or $exception -is [System.Net.Sockets.SocketException])
}

function Send-GhRetryNotice {
    # Through the loop's hook, when one is wired; a notice that throws must
    # not turn an answer into a failure.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Message)
    if ($null -eq $script:GhRetryNotice) { return }
    try { & $script:GhRetryNotice $Message } catch { }
}

function Format-GhExceptionChain {
    # The failure's shape as type names, outer to inner, a socket error's
    # code included: "HttpRequestException > IOException > SocketException(ConnectionReset)".
    # Types only: a message could carry a URL or a reply, never needed here.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Exception)
    $names = @()
    $e = $Exception
    while ($null -ne $e -and $names.Count -lt 6) {
        $name = $e.GetType().Name
        if ($e -is [System.Net.Sockets.SocketException]) { $name = '{0}({1})' -f $name, $e.SocketErrorCode }
        elseif ($e -is [System.Net.Http.HttpRequestException] -and $e.PSObject.Properties['HttpRequestError'] -and [string]$e.HttpRequestError -ne 'Unknown') { $name = '{0}({1})' -f $name, $e.HttpRequestError }
        $names += $name
        $e = $e.InnerException
    }
    return ($names -join ' > ')
}

function Invoke-GhApi {
    [CmdletBinding()]
    param([string]$Method = 'GET', [Parameter(Mandatory)][string]$Path, $Body, [switch]$Conditional)
    if (-not $script:GhBase) { throw 'Initialize-GitHubAdapter has not been called' }
    # Decoded inline, never into a variable: Set-PSDebug -Trace 2 prints every assignment.
    $headers = @{
        Authorization          = 'Bearer ' + [System.Net.NetworkCredential]::new('', $script:GhToken).Password
        Accept                 = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
    }
    # Conditional: the listings the loop repeats every tick send back the
    # ETag GitHub gave last time. An unchanged answer is a 304 with no body,
    # served from the cache here, and GitHub does not count it against the
    # hourly limit.
    $cached = $null
    if ($Conditional -and $script:GhEtags.ContainsKey($Path)) {
        $cached = $script:GhEtags[$Path]
        $headers['If-None-Match'] = [string]$cached.ETag
    }
    # Debug = $false: the web cmdlets' debug stream prints the request
    # headers (the token) and the response body (a JIT config), so neither
    # -Debug nor an inherited $DebugPreference may trace this call.
    # OperationTimeoutSeconds: since 7.4 TimeoutSec bounds only the connection and response headers; a stalled body would block the loop.
    # ResponseHeadersVariable: the cmdlet sets ghResponseHeaders in this scope (the rate-limit headers).
    $ghResponseHeaders = $null
    $ghStatusCode = 0
    $params = @{ Method = $Method; Uri = "$($script:GhBase)$Path"; Headers = $headers; TimeoutSec = 30; OperationTimeoutSeconds = 30; UserAgent = 'slate-ci-host'; Debug = $false; ResponseHeadersVariable = 'ghResponseHeaders' }
    if ($Conditional) {
        # The cmdlet would raise a 304 as a failure; every status is read here
        # instead, and any failure is raised below from what GitHub reported.
        $params.SkipHttpErrorCheck = $true
        $params.StatusCodeVariable = 'ghStatusCode'
    }
    if ($null -ne $Body) {
        $params.Body = $Body | ConvertTo-Json -Compress -Depth 5
        $params.ContentType = 'application/json'
    }
    $attempt = 0
    while ($true) {
        $attempt++
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-RestMethod @params
            break
        } catch {
            # A failed request's error record keeps the request as its
            # TargetObject (the same object as the response's RequestMessage),
            # whose ToString() prints the Authorization header, so Get-Error,
            # Format-List * or serialising the record would show the token.
            # Drop the header before rethrowing; the status code stays readable
            # for Get-GhRunner. A plain exception has no TargetObject.
            if ($_.TargetObject -is [System.Net.Http.HttpRequestMessage]) { [void]$_.TargetObject.Headers.Remove('Authorization') }
            # A GET that lost its connection quickly goes again after a short
            # wait: api.github.com resets one now and then, and without this
            # each reset cost the loop a tick's admission. Only GET: a
            # repeated generate-jitconfig would register a second runner;
            # the variable write retries after a minute by itself, and a
            # failed runner delete is logged and left to the startup sweep.
            # The last attempt, a slow failure, and anything else, is raised.
            $elapsed = $stopwatch.Elapsed.TotalSeconds
            $connection = Test-GhConnectionFailure -ErrorRecord $_
            $slow = $elapsed -ge $script:GhRetryFastFailureSeconds
            $last = $attempt -gt $script:GhRetryDelays.Count
            if ($Method -eq 'GET' -and ($last -or $slow -or -not $connection) -and -not ($_.Exception -is [Microsoft.PowerShell.Commands.HttpResponseException])) {
                # Why this GET goes unretried, with the failure's shape and
                # its time: the evidence the retry rule is tuned from. An
                # HTTP status is GitHub's answer, for the caller, and no news.
                $why = if (-not $connection) { 'not a connection failure' } elseif ($slow) { 'a slow failure' } else { 'the last attempt' }
                try {
                    Send-GhRetryNotice ('GitHub API: {0} {1} gave up on attempt {2} of {3} after {4} s ({5}): {6}' -f $Method, $Path, $attempt, ($script:GhRetryDelays.Count + 1), $elapsed.ToString('F1', [cultureinfo]::InvariantCulture), $why, (Format-GhExceptionChain -Exception $_.Exception))
                } catch { }
            }
            if ($Method -ne 'GET' -or $last -or $slow -or -not $connection) { throw }
            Start-Sleep -Seconds ([int]$script:GhRetryDelays[$attempt - 1])
        }
    }
    # "answered": a conditional call may still raise the status below.
    if ($attempt -gt 1) { Send-GhRetryNotice "GitHub API: $Method $Path answered on attempt $attempt after a connection failure" }
    Test-GhRateBudget -Headers $ghResponseHeaders
    if (-not $Conditional) { return $response }
    if ($ghStatusCode -eq 304 -and $null -ne $cached) {
        $cached.UsedAt = [datetimeoffset]::UtcNow
        return $cached.Body
    }
    if ($ghStatusCode -lt 200 -or $ghStatusCode -ge 300) {
        # The shape the web cmdlets give a failure: the status, then GitHub's
        # reply on the following lines (the runbook greps "status": "403").
        throw "GitHub API $ghStatusCode for ${Path}: $($response | ConvertTo-Json -Depth 3)"
    }
    $etag = [string](Get-GhHeader -Headers $ghResponseHeaders -Name 'ETag')
    if ($etag) { $script:GhEtags[$Path] = @{ ETag = $etag; Body = $response; UsedAt = [datetimeoffset]::UtcNow } }
    else { $script:GhEtags.Remove($Path) }
    Limit-GhEtagCache
    return $response
}

function Limit-GhEtagCache {
    # Paths come and go with the runs they name: once the cache passes 200
    # entries, only the ones used in the last hour stay.
    [CmdletBinding()]
    param()
    if ($script:GhEtags.Count -le 200) { return }
    $cutoff = [datetimeoffset]::UtcNow.AddHours(-1)
    foreach ($key in @($script:GhEtags.Keys)) {
        if ($script:GhEtags[$key].UsedAt -lt $cutoff) { $script:GhEtags.Remove($key) }
    }
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
    # The digit limits keep every cast and conversion below in range, so an
    # odd header can only cost the warning, never the call.
    $remaining = [string](Get-GhHeader -Headers $Headers -Name 'X-RateLimit-Remaining')
    if ($remaining -notmatch '^\d{1,9}$' -or [int]$remaining -ge $Threshold) { return }
    $reset = [string](Get-GhHeader -Headers $Headers -Name 'X-RateLimit-Reset')
    if ($null -ne $script:GhLowBudgetWarnedReset -and $reset -eq $script:GhLowBudgetWarnedReset) { return }
    $script:GhLowBudgetWarnedReset = $reset
    $resetAt = 'an unknown time'
    if ($reset -match '^\d{1,11}$') { $resetAt = [datetimeoffset]::FromUnixTimeSeconds([int64]$reset).ToString('o') }
    try {
        & $script:GhLowBudgetWarning "GitHub API budget low: $remaining requests left until the reset at $resetAt (the PAT shares the owner's 5,000 an hour with gh)"
    } catch {
        # Logging is best effort here.
    }
}

function Get-GhLaneJobs {
    # The queued and running jobs of every queued or in-progress run of a
    # routed workflow. A run is in_progress while later jobs of it are
    # still queued, so both run states are listed. A run that moves from
    # queued to in_progress between the two listings is in both; it is
    # read once, or its queued jobs would come back twice and one job could
    # be admitted into two VMs. Only the routed workflows (config
    # RoutedWorkflows) carry lane labels, so only their runs' jobs are
    # read. The running jobs carry runner_name, which tells the host which
    # job each of its runners took. Cost per tick: two listing calls plus
    # one jobs call per queued or in-progress routed run, each conditional:
    # an unchanged answer is a 304, which GitHub does not count against the
    # hourly limit, so an idle loop spends next to nothing.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]]$RoutedWorkflows)
    $jobs = @()
    $seenRuns = @{}
    foreach ($status in 'queued', 'in_progress') {
        $runs = Invoke-GhApi -Path "/actions/runs?status=$status&per_page=100" -Conditional
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
            $page = Invoke-GhApi -Path "/actions/runs/$($run.id)/jobs?filter=latest&per_page=100" -Conditional
            foreach ($job in @($page.jobs)) {
                if ($job.status -eq 'queued' -or $job.status -eq 'in_progress') { $jobs += $job }
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

function Test-GhNotFound {
    # True when a failed Invoke-GhApi call was GitHub answering 404 (the web
    # cmdlets keep the response on the exception).
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.Management.Automation.ErrorRecord]$ErrorRecord)
    $response = $ErrorRecord.Exception.Response
    return ($null -ne $response -and [int]$response.StatusCode -eq 404)
}

function Get-GhRunner {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int64]$RunnerId)
    try {
        return Invoke-GhApi -Path "/actions/runners/$RunnerId"
    } catch {
        if (Test-GhNotFound -ErrorRecord $_) { return $null }
        throw
    }
}

function Get-GhRunners {
    [CmdletBinding()]
    param()
    return @((Invoke-GhApi -Path '/actions/runners?per_page=100').runners)
}

function Set-GhVariable {
    # The published state (overflow spec §4). PATCH updates; a 404 means the
    # variable does not exist yet and POST creates it. The token needs the
    # Variables permission (read and write).
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][AllowEmptyString()][string]$Value)
    try {
        Invoke-GhApi -Method PATCH -Path "/actions/variables/$Name" -Body @{ name = $Name; value = $Value } | Out-Null
    } catch {
        if (-not (Test-GhNotFound -ErrorRecord $_)) { throw }
        Invoke-GhApi -Method POST -Path '/actions/variables' -Body @{ name = $Name; value = $Value } | Out-Null
    }
}

function New-GitHubAdapters {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]]$RoutedWorkflows)
    # As in New-HyperVAdapters: GetNewClosure() runs a block in a new
    # dynamic module that sees only its captured locals and the global
    # scope, so the functions are captured as locals too and invoked with
    # &; each still runs in the scope that defined it.
    $commands = @{}
    foreach ($command in 'Get-GhLaneJobs', 'Get-GhJob', 'Get-GhRun', 'New-GhJitRunner', 'Remove-GhRunner', 'Get-GhRunner', 'Get-GhRunners', 'Set-GhVariable') {
        $commands[$command] = Get-Command -Name $command -CommandType Function -ErrorAction Stop
    }
    $routed = @($RoutedWorkflows)
    $adapters = @{
        GetLaneJobs   = { & $commands['Get-GhLaneJobs'] -RoutedWorkflows $routed }
        GetJob        = { param($id) & $commands['Get-GhJob'] -JobId $id }
        GetRun        = { param($id) & $commands['Get-GhRun'] -RunId $id }
        NewJitRunner  = { param($name, $labels) & $commands['New-GhJitRunner'] -Name $name -Labels $labels }
        RemoveRunner  = { param($id) & $commands['Remove-GhRunner'] -RunnerId $id }
        GetRunner     = { param($id) & $commands['Get-GhRunner'] -RunnerId $id }
        ListRunners   = { & $commands['Get-GhRunners'] }
        PublishState  = { param($json) & $commands['Set-GhVariable'] -Name 'WINDOWS_HOME_STATE' -Value $json }
    }
    # Bind the routed workflows and the commands into each scriptblock: the
    # module invokes them long after this function has returned.
    foreach ($key in @($adapters.Keys)) { $adapters[$key] = $adapters[$key].GetNewClosure() }
    return $adapters
}
