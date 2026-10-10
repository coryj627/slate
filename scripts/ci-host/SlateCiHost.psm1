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

function ConvertTo-DateTimeOffset {
    # pwsh's JSON deserialiser turns ISO strings into [datetime] (Kind Utc
    # for a trailing Z); a [string] cast would then drop the zone and a
    # later Parse would read it as local time. Accept every shape once.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Value)
    if ($Value -is [datetimeoffset]) { return $Value }
    if ($Value -is [datetime]) {
        if ($Value.Kind -eq [System.DateTimeKind]::Unspecified) { $Value = [datetime]::SpecifyKind($Value, [System.DateTimeKind]::Utc) }
        return [datetimeoffset]$Value
    }
    $styles = [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::RoundtripKind
    return [datetimeoffset]::Parse([string]$Value, [cultureinfo]::InvariantCulture, $styles)
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
            CreatedAt = ConvertTo-DateTimeOffset -Value $job.created_at
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
            if ((ConvertTo-DateTimeOffset -Value $retry['NextAt']) -gt $Now) { continue }
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

function Test-CommitEligible {
    # The whole cache-trust decision, from GitHub's record of what ran and
    # the host's own record of how the VM stopped. Labels play no part.
    [CmdletBinding()]
    param(
        $Job,
        $Run,
        [Parameter(Mandatory)][string]$RunnerName,
        [Parameter(Mandatory)][bool]$ForcedOff,
        [Parameter(Mandatory)][int]$ParentGeneration,
        [Parameter(Mandatory)][int]$ForkGeneration,
        [ValidateNotNullOrEmpty()][string]$TrustedRepo = 'coryj627/slate',
        [ValidateNotNullOrEmpty()][string]$TrustedBranch = 'main',
        [ValidateNotNullOrEmpty()][string[]]$TrustedEvents = @('push', 'schedule', 'workflow_dispatch')
    )
    # Comparisons are ordinal: PowerShell's string operators compare
    # linguistically and drop default-ignorable code points (a soft hyphen
    # inside 'main' would match). Git refs, runner names and the event are
    # compared exactly; the repository ignores case because GitHub
    # repository names do.
    function Deny([string]$reason) { return [pscustomobject]@{ Eligible = $false; Reason = $reason } }
    if ($null -eq $Job) { return (Deny 'no job resolved for runner') }
    if (-not [string]::Equals([string]$Job.runner_name, $RunnerName, [System.StringComparison]::Ordinal)) { return (Deny "runner mismatch: $($Job.runner_name)") }
    if (-not [string]::Equals([string]$Job.conclusion, 'success', [System.StringComparison]::Ordinal)) { return (Deny "conclusion: $($Job.conclusion)") }
    if ($ForcedOff) { return (Deny 'guest was forced off') }
    if ($null -eq $Run) { return (Deny 'no run') }
    if ([Array]::IndexOf([string[]]$TrustedEvents, [string]$Run.event) -lt 0) { return (Deny "event: $($Run.event)") }
    if (-not [string]::Equals([string]$Run.head_branch, $TrustedBranch, [System.StringComparison]::Ordinal)) { return (Deny "branch: $($Run.head_branch)") }
    $repoName = ''
    if ($null -ne $Run.head_repository -and $Run.head_repository.PSObject.Properties['full_name']) { $repoName = [string]$Run.head_repository.full_name }
    if (-not [string]::Equals($repoName, $TrustedRepo, [System.StringComparison]::OrdinalIgnoreCase)) { return (Deny "repository: $repoName") }
    if ($ParentGeneration -ne $ForkGeneration) { return (Deny "generation moved: fork $ForkGeneration, parent $ParentGeneration") }
    return [pscustomobject]@{ Eligible = $true; Reason = 'trusted main' }
}

function Resolve-RunnerJob {
    # Any queued job with matching labels may have taken this runner, so
    # ask GitHub which one did: the admitted job first, then every other
    # recently seen candidate.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RunnerName,
        [int64]$AdmittedJobId = 0,
        [int64[]]$CandidateJobIds = @(),
        [Parameter(Mandatory)][scriptblock]$GetJob
    )
    $ids = @()
    if ($AdmittedJobId -gt 0) { $ids += $AdmittedJobId }
    foreach ($id in @($CandidateJobIds)) { if ($ids -notcontains $id) { $ids += $id } }
    foreach ($id in $ids) {
        $job = & $GetJob $id
        if ($null -ne $job -and [string]$job.runner_name -eq $RunnerName) { return $job }
    }
    return $null
}

# ---- host-only: config, journal, log (pwsh 7; -AsHashtable) ----

function Join-WinPath {
    # Host paths are Windows paths even when the suite runs on Linux CI,
    # where Join-Path would insert a forward slash.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Base, [Parameter(Mandatory)][string]$Child)
    return ($Base.TrimEnd('\') + '\' + $Child.TrimStart('\'))
}

function Get-CiHostConfig {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    # A missing or unreadable file must throw, never yield a half config.
    $ErrorActionPreference = 'Stop'
    $config = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -AsHashtable
    $required = 'Owner', 'Repo', 'Root', 'SwitchName', 'Gateway', 'Dns', 'Slots', 'Vcpu', 'MemoryGB', 'Lanes',
        'TickSeconds', 'HeartbeatTimeoutSeconds', 'UnclaimedTimeoutSeconds', 'RetryCap', 'RetryBackoffSeconds',
        'TrustedRepo', 'TrustedBranch', 'TrustedEvents'
    foreach ($key in $required) {
        if (-not $config.Contains($key)) { throw "config ${Path}: missing required key '$key'" }
    }
    if (-not ($config.Lanes -is [System.Collections.IDictionary])) { throw "config ${Path}: Lanes must be an object" }
    foreach ($lane in @($config.Lanes.Keys)) {
        if ($script:DefaultLanes -cnotcontains $lane) { throw "config ${Path}: unknown lane '$lane'" }
        if (-not ($config.Lanes[$lane] -is [System.Collections.IDictionary])) { throw "config ${Path}: lane '$lane' must be an object" }
        foreach ($k in 'Cache', 'MaxMinutes') {
            if (-not $config.Lanes[$lane].Contains($k)) { throw "config ${Path}: lane '$lane' missing '$k'" }
        }
    }
    if ($null -eq $config.Slots -or @($config.Slots).Count -lt 1) { throw "config ${Path}: at least one slot is required" }
    $config['GoldenPath'] = Join-WinPath $config.Root 'golden\win11-runner.vhdx'
    $config['CacheDir'] = Join-WinPath $config.Root 'cache'
    $config['VmDir'] = Join-WinPath $config.Root 'vms'
    $config['StateDir'] = Join-WinPath $config.Root 'state'
    $config['LogDir'] = Join-WinPath $config.Root 'logs'
    return $config
}

function New-Journal {
    return @{ Vms = @{}; Retries = @{}; SeenJobs = @{} }
}

function Read-Journal {
    # A corrupt journal must never stop the host: it is moved aside with a
    # timestamp (for forensics) and an empty one takes its place. The
    # startup sweep then reconciles live VMs and runners from scratch.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return (New-Journal) }
    $journal = $null
    try {
        # -ErrorAction Stop: a locked file is otherwise a non-terminating
        # error and the IOException clause below would never run.
        $journal = Get-Content -Raw -LiteralPath $Path -ErrorAction Stop | ConvertFrom-Json -AsHashtable
        if ($null -eq $journal -or -not ($journal -is [System.Collections.IDictionary])) { throw 'journal is not an object' }
    } catch [System.IO.IOException], [System.UnauthorizedAccessException] {
        # Unreadable (locked, access denied) is not corrupt: never overwrite
        # a journal we could not read; the task restarts and retries.
        throw
    } catch {
        $aside = '{0}.corrupt-{1}' -f $Path, (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
        Move-Item -LiteralPath $Path -Destination $aside -Force
        return (New-Journal)
    }
    foreach ($key in 'Vms', 'Retries', 'SeenJobs') {
        if (-not $journal.Contains($key) -or $null -eq $journal[$key]) { $journal[$key] = @{} }
    }
    return $journal
}

function Write-Journal {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)]$Journal)
    # A failed temp write or open must throw before the replace, or a bad
    # temp file would be published (cmdlet errors are non-terminating).
    $ErrorActionPreference = 'Stop'
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $tmp = "$Path.tmp"
    $Journal | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $tmp -Encoding utf8
    # The three-argument overload replaces the destination atomically
    # (Move-Item -Force deletes, then renames).
    [System.IO.File]::Move($tmp, $Path, $true)
}

function Write-CiLog {
    # One line per state transition; callers never pass a JIT config or
    # token into Message.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][ValidateSet('info', 'warn', 'error')][string]$Level,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Message
    )
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $line = '{0} [{1}] {2}' -f [datetimeoffset]::Now.ToString('o'), $Level, $Message
    Add-Content -LiteralPath $Path -Value $line -Encoding utf8
}

Export-ModuleMember -Function Get-LaneFromLabels, New-RunnerName, Split-KvpChunks, Join-KvpChunks,
    ConvertTo-DateTimeOffset, Select-QueuedLaneJobs, Select-JobsToAdmit, Register-JobRetry, Test-VmExpired, Get-StaleRunnerNames, Test-CommitEligible, Resolve-RunnerJob, Get-CiHostConfig, New-Journal, Read-Journal, Write-Journal, Write-CiLog
