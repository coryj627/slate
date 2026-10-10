# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Pure logic for the self-hosted Windows runner host (spec:
# docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md).
# Every GitHub and Hyper-V call lives in scripts/ci-host/adapters and is
# passed in as a scriptblock, so this module is tested without a VM or a
# token. The guest imports this module too (Windows PowerShell 5.1), so
# keep the syntax 5.1-compatible; -AsHashtable appears only in host-only
# functions.

Set-StrictMode -Version Latest

$script:DefaultLanes = @('rust', 'app', 'model', 'shell')

function Get-LaneFromLabels {
    # Exactly one slate-win-<lane> label selects a lane; zero or two mean
    # the job is not ours (a PR can put any label it likes in runs-on).
    # GitHub matches labels case-insensitively, so the lane is lower-cased:
    # it becomes a config key and part of the runner name.
    [CmdletBinding()]
    param([string[]]$Labels = @(), [string[]]$Lanes = $script:DefaultLanes)
    $found = @()
    foreach ($label in @($Labels)) {
        if ($label -match '^slate-win-([a-z]+)$') {
            $lane = $Matches[1].ToLowerInvariant()
            if ($Lanes -contains $lane) { $found += $lane }
        }
    }
    if ($found.Count -eq 1) { return $found[0] }
    return $null
}

function New-RunnerName {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Lane)
    return 'slate-win-{0}-{1}' -f $Lane, [guid]::NewGuid().ToString('N').Substring(0, 8)
}

function Split-KvpChunks {
    # Msvm_KvpExchangeDataItem.Data has MAXLEN 1024; chunk below it.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Text,
        [string]$Prefix = 'slate.jit',
        [int]$ChunkSize = 1000
    )
    if ($ChunkSize -lt 1 -or $ChunkSize -gt 1024) { throw "ChunkSize must be 1..1024 (KVP Data MAXLEN is 1024), got $ChunkSize" }
    $items = New-Object System.Collections.Specialized.OrderedDictionary
    $count = [int][math]::Ceiling($Text.Length / [double]$ChunkSize)
    for ($i = 0; $i -lt $count; $i++) {
        $start = $i * $ChunkSize
        $length = [math]::Min($ChunkSize, $Text.Length - $start)
        $items["$Prefix.$i"] = $Text.Substring($start, $length)
    }
    $items["$Prefix.count"] = [string]$count
    return $items
}

function Join-KvpChunks {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary]$Items,
        [string]$Prefix = 'slate.jit'
    )
    $countKey = "$Prefix.count"
    if (-not $Items.Contains($countKey)) { return $null }
    $count = [int]$Items[$countKey]
    $builder = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $count; $i++) {
        $key = "$Prefix.$i"
        if (-not $Items.Contains($key)) { return $null }
        [void]$builder.Append([string]$Items[$key])
    }
    return $builder.ToString()
}

function Select-QueuedLaneJobs {
    # Flattens the API's job objects into the admission shape; drops
    # anything not queued or not carrying exactly one lane label.
    [CmdletBinding()]
    param([object[]]$Jobs = @(), [string[]]$Lanes = $script:DefaultLanes)
    $result = @()
    foreach ($job in @($Jobs)) {
        if ($null -eq $job -or $job.status -ne 'queued') { continue }
        $labels = @()
        if ($job.PSObject.Properties['labels'] -and $null -ne $job.labels) { $labels = @($job.labels) }
        $lane = Get-LaneFromLabels -Labels $labels -Lanes $Lanes
        if (-not $lane) { continue }
        $result += [pscustomobject]@{
            JobId     = [int64]$job.id
            RunId     = [int64]$job.run_id
            Lane      = $lane
            CreatedAt = [datetimeoffset]::Parse([string]$job.created_at, [cultureinfo]::InvariantCulture)
        }
    }
    return @($result | Sort-Object CreatedAt, JobId)
}

function Select-JobsToAdmit {
    # FIFO admission into free slots; one VM per job; back-off and cap
    # from the retry table.
    [CmdletBinding()]
    param(
        [object[]]$Candidates = @(),
        [System.Collections.IDictionary]$ActiveVms = @{},
        [int]$FreeSlots = 0,
        [System.Collections.IDictionary]$Retries = @{},
        [Parameter(Mandatory)][datetimeoffset]$Now,
        [int]$RetryCap = 3
    )
    $activeJobIds = @{}
    foreach ($vm in $ActiveVms.Values) {
        if ($null -ne $vm -and $vm.Contains('JobId') -and $null -ne $vm['JobId']) { $activeJobIds[[string]$vm['JobId']] = $true }
    }
    $admit = @()
    foreach ($candidate in @($Candidates)) {
        if ($admit.Count -ge $FreeSlots) { break }
        $key = [string]$candidate.JobId
        if ($activeJobIds.ContainsKey($key)) { continue }
        if ($Retries.Contains($key)) {
            $retry = $Retries[$key]
            if ([int]$retry['Count'] -ge $RetryCap) { continue }
            if ([datetimeoffset]::Parse([string]$retry['NextAt'], [cultureinfo]::InvariantCulture) -gt $Now) { continue }
        }
        $admit += $candidate
    }
    return @($admit)
}

function Register-JobRetry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary]$Retries,
        [Parameter(Mandatory)][int64]$JobId,
        [Parameter(Mandatory)][datetimeoffset]$Now,
        [int]$BackoffSeconds = 600
    )
    $key = [string]$JobId
    $count = 0
    if ($Retries.Contains($key)) { $count = [int]$Retries[$key]['Count'] }
    $Retries[$key] = @{ Count = $count + 1; NextAt = $Now.AddSeconds($BackoffSeconds).ToString('o') }
}

function Test-VmExpired {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][datetimeoffset]$StartedAt,
        [Parameter(Mandatory)][int]$MaxMinutes,
        [Parameter(Mandatory)][datetimeoffset]$Now,
        [int]$GraceMinutes = 10
    )
    return ($Now -ge $StartedAt.AddMinutes($MaxMinutes + $GraceMinutes))
}

function Get-StaleRunnerNames {
    [CmdletBinding()]
    param([object[]]$Runners = @(), [string[]]$ActiveNames = @())
    $names = @()
    foreach ($runner in @($Runners)) {
        if ($null -eq $runner) { continue }
        if ($runner.name -like 'slate-win-*' -and $runner.status -eq 'offline' -and ($ActiveNames -notcontains $runner.name)) {
            $names += [string]$runner.name
        }
    }
    return @($names)
}

Export-ModuleMember -Function Get-LaneFromLabels, New-RunnerName, Split-KvpChunks, Join-KvpChunks,
    Select-QueuedLaneJobs, Select-JobsToAdmit, Register-JobRetry, Test-VmExpired, Get-StaleRunnerNames
