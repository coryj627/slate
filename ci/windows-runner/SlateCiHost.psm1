# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Pure logic for the self-hosted Windows runner host (spec:
# docs/superpowers/specs/2026-10-10-self-hosted-windows-runner-design.md).
# Every GitHub and Hyper-V call lives in ci/windows-runner/adapters and is
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
    # Flattens the API's queued job objects into the admission shape; drops
    # anything not queued and anything not carrying exactly one lane label.
    [CmdletBinding()]
    param([object[]]$Jobs = @(), [string[]]$Lanes = $script:DefaultLanes)
    $result = @()
    foreach ($job in @($Jobs)) {
        if ($null -eq $job) { continue }
        if ([string]$job.status -ne 'queued') { continue }
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

function Select-HostRunnerJobs {
    # The in-progress jobs on this host's runners (slate-win-* runners named
    # in -RunnerNames, compared ordinally), from the whole listing rather
    # than the lane-filtered queue: a job without exactly one lane label can
    # take a runner too, and the handoff reconciliation must see it. Lane is
    # $null for such a job.
    [CmdletBinding()]
    param([object[]]$Jobs = @(), [string[]]$RunnerNames = @(), [string[]]$Lanes = $script:DefaultLanes)
    $ours = [System.Collections.Generic.HashSet[string]]::new([string[]]@($RunnerNames), [System.StringComparer]::Ordinal)
    $result = @()
    foreach ($job in @($Jobs)) {
        if ($null -eq $job -or [string]$job.status -ne 'in_progress') { continue }
        if (-not $job.PSObject.Properties['runner_name'] -or $null -eq $job.runner_name) { continue }
        $runnerName = [string]$job.runner_name
        if (-not $runnerName.StartsWith('slate-win-', [System.StringComparison]::Ordinal) -or -not $ours.Contains($runnerName)) { continue }
        $labels = @()
        if ($job.PSObject.Properties['labels'] -and $null -ne $job.labels) { $labels = @($job.labels) }
        $result += [pscustomobject]@{
            JobId      = [int64]$job.id
            RunId      = [int64]$job.run_id
            Lane       = Get-LaneFromLabels -Labels $labels -Lanes $Lanes
            RunnerName = $runnerName
        }
    }
    return @($result)
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

function Get-HostHealth {
    # The breaker's record in the journal (overflow spec OR-D4): failures
    # in a row since a runner last took its job, the last one's time and
    # reason, when the breaker tripped, and when it half-opened after the
    # cool-down. A journal from before the record reads as healthy.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Journal)
    if (-not ($Journal['Health'] -is [System.Collections.IDictionary])) { $Journal['Health'] = @{} }
    $health = $Journal['Health']
    foreach ($key in 'Failures', 'LastFailureAt', 'LastReason', 'TrippedAt', 'HalfOpenAt') {
        if (-not $health.Contains($key)) { $health[$key] = $(if ($key -eq 'Failures') { 0 } else { $null }) }
    }
    if ($null -eq $health['Failures']) { $health['Failures'] = 0 }
    return $health
}

function Test-HostCooledDown {
    # True when the breaker never tripped, or tripped a full cool-down ago.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Health, [Parameter(Mandatory)][datetimeoffset]$Now)
    if ($null -eq $Health['TrippedAt']) { return $true }
    $tripped = ConvertTo-DateTimeOffset -Value $Health['TrippedAt']
    return ($Now -ge $tripped.AddSeconds([int]$Config.BreakerCooldownSeconds))
}

function Test-HostServing {
    # The breaker's verdict for the published state: serving while the
    # failures in a row stay under the cap, or once the cool-down has
    # passed (half-open: the router sends one lane's worth to try, and
    # the next failure trips it again at once).
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][datetimeoffset]$Now)
    $health = Get-HostHealth -Journal $Journal
    if ([int]$health['Failures'] -lt [int]$Config.BreakerFailures) { return $true }
    return (Test-HostCooledDown -Config $Config -Health $health -Now $Now)
}

function Register-HostFailure {
    # One more discard the host is to blame for (a VM it could not
    # provision, hand off, hear from, or that shut down without running a
    # job). At the cap, with no trip in force, the breaker trips: the next
    # published state says not serving, and the log says why, once.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)][string]$Reason, [Parameter(Mandatory)][datetimeoffset]$Now)
    $health = Get-HostHealth -Journal $Journal
    $health['Failures'] = [int]$health['Failures'] + 1
    $health['LastFailureAt'] = $Now.ToString('o')
    $health['LastReason'] = $Reason
    if ([int]$health['Failures'] -lt [int]$Config.BreakerFailures) { return }
    if (-not (Test-HostCooledDown -Config $Config -Health $health -Now $Now)) { return }
    $health['TrippedAt'] = $Now.ToString('o')
    $health['HalfOpenAt'] = $null
    $minutes = [int][math]::Round([int]$Config.BreakerCooldownSeconds / 60.0)
    & $Adapters.Log 'warn' ("health: not serving for {0} min: {1} failures in a row, last '{2}' (the router sends every lane to the vendor until then; fix the host and restart the loop, or wait)" -f $minutes, $health['Failures'], $Reason)
}

function Register-HostSuccess {
    # A runner took its job: provisioning, boot, handoff, network and
    # registration all worked, so the failures before it are forgotten.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters)
    $health = Get-HostHealth -Journal $Journal
    $failures = [int]$health['Failures']
    if ($failures -gt 0) { & $Adapters.Log 'info' "health: a runner took its job; $failures failures in a row forgotten" }
    Reset-HostHealth -Journal $Journal
}

function Reset-HostHealth {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Journal)
    $health = Get-HostHealth -Journal $Journal
    foreach ($key in 'LastFailureAt', 'LastReason', 'TrippedAt', 'HalfOpenAt') { $health[$key] = $null }
    $health['Failures'] = 0
}

function Update-HostHealth {
    # Once a tick: the moment the cool-down passes is logged, once, so the
    # log explains why the router started sending work home again.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)][datetimeoffset]$Now)
    $health = Get-HostHealth -Journal $Journal
    if ($null -eq $health['TrippedAt'] -or $null -ne $health['HalfOpenAt']) { return }
    if (-not (Test-HostCooledDown -Config $Config -Health $health -Now $Now)) { return }
    $health['HalfOpenAt'] = $Now.ToString('o')
    $minutes = [int][math]::Round([int]$Config.BreakerCooldownSeconds / 60.0)
    & $Adapters.Log 'info' "health: serving again after the $minutes min cool-down; the next failure trips it at once"
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
    # slate-win-* runners that no active VM owns. Offline ones by default: a
    # running loop never touches a runner that may still take a job. The
    # startup sweep passes -IncludeOnline, because every VM it does not keep
    # is about to be turned off, and GitHub keeps reporting such a runner
    # online for a while after that.
    [CmdletBinding()]
    param([object[]]$Runners = @(), [string[]]$ActiveNames = @(), [switch]$IncludeOnline)
    $names = @()
    foreach ($runner in @($Runners)) {
        if ($null -eq $runner) { continue }
        if ($runner.name -like 'slate-win-*' -and ($IncludeOnline -or $runner.status -eq 'offline') -and ($ActiveNames -notcontains $runner.name)) {
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

function Get-RunRepository {
    # The run's head repository, or '' when it has none (a deleted fork) or
    # GitHub left the name out: a missing property would throw under strict mode.
    param($Run)
    if ($null -eq $Run -or -not $Run.PSObject.Properties['head_repository'] -or $null -eq $Run.head_repository) { return '' }
    if (-not $Run.head_repository.PSObject.Properties['full_name']) { return '' }
    return [string]$Run.head_repository.full_name
}

function Format-RunRepository {
    # The head repository for a log line, never an empty name.
    param($Run)
    $repo = Get-RunRepository -Run $Run
    if ($repo) { return $repo }
    return 'an unknown repository'
}

function Test-TrustedRun {
    # A null head repository (a deleted fork) is not trusted.
    param($Run, [Parameter(Mandatory)][string]$TrustedRepo)
    $repo = Get-RunRepository -Run $Run
    return [bool]$repo -and [string]::Equals($repo, $TrustedRepo, [System.StringComparison]::OrdinalIgnoreCase)
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
        if ($null -ne $job -and [string]::Equals([string]$job.runner_name, $RunnerName, [System.StringComparison]::Ordinal)) { return $job }
    }
    return $null
}

# ---- host-only: config, journal, log (pwsh 7; -AsHashtable) ----

function ConvertTo-IPv4UInt32 {
    # The address as a big-endian unsigned integer, so prefixes compare by
    # shifting. Anything but a dotted IPv4 address throws.
    param([Parameter(Mandatory)][string]$Address)
    # IPAddress.TryParse also accepts short forms such as '10.77.0', so the
    # dotted-quad shape is checked first.
    $parsed = $null
    if ($Address -notmatch '^\d{1,3}(\.\d{1,3}){3}$' -or -not [System.Net.IPAddress]::TryParse($Address, [ref]$parsed) -or $parsed.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) {
        throw "'$Address' is not a dotted IPv4 address"
    }
    $bytes = $parsed.GetAddressBytes()
    [Array]::Reverse($bytes)
    return [BitConverter]::ToUInt32($bytes, 0)
}

function Test-IPv4InPrefix {
    # True when an IPv4 address lies inside a CIDR prefix. The host install
    # uses it to refuse a gateway outside the NAT prefix it is asked to
    # create: New-NetNat does not check the pair, and a mismatch leaves the
    # job VMs without a route. Malformed input throws instead of answering.
    param(
        [Parameter(Mandatory)][string]$Address,
        [Parameter(Mandatory)][string]$Prefix
    )
    $parts = $Prefix -split '/', 2
    if ($parts.Count -ne 2 -or $parts[1] -notmatch '^\d{1,2}$' -or [int]$parts[1] -gt 32) {
        throw "prefix '$Prefix' is not a.b.c.d/n with n from 0 to 32"
    }
    $length = [int]$parts[1]
    $network = [int64](ConvertTo-IPv4UInt32 -Address $parts[0])
    $candidate = [int64](ConvertTo-IPv4UInt32 -Address $Address)
    if ($length -eq 0) { return $true }
    $shift = 32 - $length
    return (($network -shr $shift) -eq ($candidate -shr $shift))
}

function Join-WinPath {
    # Host paths are Windows paths even when the suite runs on Linux CI,
    # where Join-Path would insert a forward slash.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Base, [Parameter(Mandatory)][string]$Child)
    return ($Base.TrimEnd('\') + '\' + $Child.TrimStart('\'))
}

function Get-CiHostPaths {
    # The one statement of the host tree: the loop, the install scripts and
    # the prune task derive every path from it.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Root)
    return @{
        GoldenPath     = Join-WinPath $Root 'golden\win11-runner.vhdx'
        GoldenPrevPath = Join-WinPath $Root 'golden\win11-runner.prev.vhdx'
        CacheDir       = Join-WinPath $Root 'cache'
        VmDir          = Join-WinPath $Root 'vms'
        StateDir       = Join-WinPath $Root 'state'
        LogDir         = Join-WinPath $Root 'logs'
        JournalPath    = Join-WinPath $Root 'state\journal.json'
    }
}

function Get-CiHostConfig {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    # A missing or unreadable file must throw, never yield a half config.
    $ErrorActionPreference = 'Stop'
    $config = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -AsHashtable
    $required = 'Owner', 'Repo', 'Root', 'SwitchName', 'Gateway', 'Dns', 'Slots', 'Vcpu', 'MemoryGB', 'Lanes',
        'TickSeconds', 'HeartbeatTimeoutSeconds', 'UnclaimedTimeoutSeconds', 'RetryCap', 'RetryBackoffSeconds',
        'TrustedRepo', 'TrustedBranch', 'TrustedEvents', 'RoutedWorkflows', 'NatPrefix'
    foreach ($key in $required) {
        if (-not $config.Contains($key)) { throw "config ${Path}: missing required key '$key'" }
    }
    # Discovery reads the jobs of these workflows' runs only, so an empty or
    # malformed list would admit nothing, silently.
    $routed = $config.RoutedWorkflows
    if (-not ($routed -is [System.Collections.IList]) -or $routed.Count -lt 1 -or
        @($routed | Where-Object { -not ($_ -is [string]) -or -not $_ }).Count -gt 0) {
        throw "config ${Path}: RoutedWorkflows must be a non-empty array of workflow paths"
    }
    # Older config files predate the key: default it rather than crash-loop
    # a host installed before it existed.
    if (-not $config.Contains('LogRetentionDays')) { $config['LogRetentionDays'] = 30 }
    $days = $config.LogRetentionDays
    if (-not ($days -is [int] -or $days -is [int64] -or $days -is [double]) -or [double]$days -ne [math]::Floor([double]$days) -or [int]$days -lt 1) {
        throw "config ${Path}: LogRetentionDays must be a whole number of days, 1 or more"
    }
    $config['LogRetentionDays'] = [int]$days
    # The health breaker (overflow spec OR-D4): failures in a row that make
    # the host publish "not serving", and for how long. Older files
    # predate both keys.
    foreach ($pair in @(@('BreakerFailures', 3), @('BreakerCooldownSeconds', 900))) {
        $key = $pair[0]
        if (-not $config.Contains($key)) { $config[$key] = $pair[1] }
        $value = $config[$key]
        if (-not ($value -is [int] -or $value -is [int64] -or $value -is [double]) -or [double]$value -ne [math]::Floor([double]$value) -or [int]$value -lt 1) {
            throw "config ${Path}: $key must be a whole number, 1 or more"
        }
        $config[$key] = [int]$value
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
    # One subnet for the host, the NAT and the guests: setup-host.ps1 creates
    # NatPrefix, the loop hands its length to each guest as slate.prefix, so
    # the gateway and every slot must lie inside it, or the VMs have no route.
    $natPrefix = [string]$config.NatPrefix
    if ($natPrefix -notmatch '^(\d{1,3}\.){3}\d{1,3}/([1-9]|[12]\d|3[0-2])$') { throw "config ${Path}: NatPrefix must be a.b.c.d/n with n from 1 to 32, got '$natPrefix'" }
    if (-not (Test-IPv4InPrefix -Address ([string]$config.Gateway) -Prefix $natPrefix)) { throw "config ${Path}: Gateway $($config.Gateway) is outside NatPrefix $natPrefix" }
    foreach ($slot in @($config.Slots)) {
        if ($null -eq $slot -or -not ($slot -is [System.Collections.IDictionary]) -or -not $slot.Contains('Index') -or -not $slot.Contains('Ip')) { throw "config ${Path}: every slot needs Index and Ip" }
        if (-not (Test-IPv4InPrefix -Address ([string]$slot.Ip) -Prefix $natPrefix)) { throw "config ${Path}: slot $($slot.Index) address $($slot.Ip) is outside NatPrefix $natPrefix" }
        if ([string]$slot.Ip -eq [string]$config.Gateway) { throw "config ${Path}: slot $($slot.Index) uses the gateway address $($slot.Ip)" }
    }
    $config['PrefixLength'] = [int]($natPrefix -split '/')[1]
    $paths = Get-CiHostPaths -Root $config.Root
    foreach ($key in $paths.Keys) { $config[$key] = $paths[$key] }
    return $config
}

# Every top-level journal key, present from creation and after a read of
# an older file: the state machine reads them with index syntax and a
# missing key would throw under strict mode.
$script:JournalDefaults = @{ Vms = @{}; Retries = @{}; SeenJobs = @{}; PublishedState = $null; PublishedAt = $null; PublishFailedAt = $null; LogsPrunedOn = $null; GoldenAccepted = $null; Health = @{} }

function New-Journal {
    $journal = @{}
    foreach ($key in $script:JournalDefaults.Keys) {
        $value = $script:JournalDefaults[$key]
        $journal[$key] = $(if ($value -is [hashtable]) { @{} } else { $value })
    }
    return $journal
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
    foreach ($key in $script:JournalDefaults.Keys) {
        if (-not $journal.Contains($key)) { $journal[$key] = $(if ($script:JournalDefaults[$key] -is [hashtable]) { @{} } else { $null }) }
        elseif ($null -eq $journal[$key] -and $script:JournalDefaults[$key] -is [hashtable]) { $journal[$key] = @{} }
    }
    return $journal
}

function Read-JournalSnapshot {
    # A reader's view (the prune task): never moves a broken journal aside,
    # never writes; a missing or unparseable file is an empty journal.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return (New-Journal) }
    try {
        $journal = Get-Content -Raw -LiteralPath $Path -ErrorAction Stop | ConvertFrom-Json -AsHashtable
        if ($null -eq $journal -or -not ($journal -is [System.Collections.IDictionary])) { return (New-Journal) }
    } catch { return (New-Journal) }
    foreach ($key in $script:JournalDefaults.Keys) {
        if (-not $journal.Contains($key)) { $journal[$key] = $(if ($script:JournalDefaults[$key] -is [hashtable]) { @{} } else { $null }) }
    }
    return $journal
}

function Test-RecentlyAt {
    # The once-a-minute gates: true only when a journal timestamp parses and
    # is less than -WithinSeconds old. Null, empty or a value that does not
    # parse (a hand-edited journal) counts as never, so it cannot throw out
    # of a gate or hold one shut.
    [CmdletBinding()]
    param([AllowNull()]$Value, [Parameter(Mandatory)][datetimeoffset]$Now, [Parameter(Mandatory)][int]$WithinSeconds)
    if ($null -eq $Value -or ($Value -is [string] -and -not $Value)) { return $false }
    try { $at = ConvertTo-DateTimeOffset -Value $Value } catch { return $false }
    return (($Now - $at).TotalSeconds -lt $WithinSeconds)
}

function Remove-SealedFile {
    # A sealed image is read-only: clear the bit, then delete. Missing is fine.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    $ErrorActionPreference = 'Stop'
    if (-not (Test-Path -LiteralPath $Path)) { return }
    Set-ItemProperty -LiteralPath $Path -Name IsReadOnly -Value $false
    Remove-Item -LiteralPath $Path -Force
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

function Get-OrchestratorLogDate {
    # The local day the daily log file is named by, in the invariant
    # culture: a Hijri or Buddhist year (ar-SA, th-TH) would name a file the
    # retention reads as another date.
    [CmdletBinding()]
    param([Parameter(Mandatory)][datetimeoffset]$Now)
    return $Now.ToLocalTime().ToString('yyyy-MM-dd', [cultureinfo]::InvariantCulture)
}

function Get-OrchestratorLogName {
    # The one statement of the daily log file's name: the loop and the prune
    # task write it, Get-ExpiredLogNames parses it.
    [CmdletBinding()]
    param([Parameter(Mandatory)][datetimeoffset]$Now)
    return ('orchestrator-{0}.log' -f (Get-OrchestratorLogDate -Now $Now))
}

function Get-ExpiredLogNames {
    # Dated orchestrator logs older than the retention, by the local date in
    # the name (Get-OrchestratorLogName names them with the local date).
    # Names that do not parse are kept: a human can decide about those.
    [CmdletBinding()]
    param([string[]]$Names = @(), [Parameter(Mandatory)][string]$Today, [Parameter(Mandatory)][int]$RetentionDays)
    $cutoff = [datetime]::ParseExact($Today, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture).AddDays(-$RetentionDays)
    $expired = @()
    foreach ($name in @($Names)) {
        if ($name -notmatch '^orchestrator-(\d{4}-\d{2}-\d{2})\.log$') { continue }
        $date = [datetime]::MinValue
        if (-not [datetime]::TryParseExact($Matches[1], 'yyyy-MM-dd', [cultureinfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$date)) { continue }
        if ($date -lt $cutoff) { $expired += $name }
    }
    return @($expired)
}

function Invoke-LogPrune {
    # Once per local day (journal LogsPrunedOn), and at the first tick after
    # a start (the sweep clears the day). A delete that fails is logged and
    # the day still counts as done: the file is tried again tomorrow.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)][string]$Today)
    if ([string]$Journal['LogsPrunedOn'] -eq $Today) { return }
    $names = @(& $Adapters.ListLogs)
    foreach ($name in @(Get-ExpiredLogNames -Names $names -Today $Today -RetentionDays ([int]$Config.LogRetentionDays))) {
        try { & $Adapters.RemoveLog $name; & $Adapters.Log 'info' "logs: removed $name" }
        catch { & $Adapters.Log 'warn' "logs: ${name}: $_" }
    }
    $Journal['LogsPrunedOn'] = $Today
}

# ---- orchestrator state machine (host-only; adapters injected) ----

function Get-FreeSlots {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal)
    $used = @{}
    foreach ($vm in $Journal.Vms.Values) {
        # An Off VM waiting to merge its cache (PendingCommit) holds no CPU,
        # memory or address: its slot serves the next job meanwhile.
        if ($vm.Contains('PendingCommit') -and $vm['PendingCommit']) { continue }
        $used[[string]$vm.Slot] = $true
    }
    $free = @()
    foreach ($slot in @($Config.Slots | Sort-Object { [int]$_.Index })) {
        if (-not $used.ContainsKey([string]$slot.Index)) { $free += $slot }
    }
    return @($free)
}

function ConvertTo-StateTime {
    # Invariant culture: a calendar or time separator of the host's locale
    # would publish a state the router cannot parse.
    param([Parameter(Mandatory)][datetimeoffset]$Value)
    return $Value.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ', [cultureinfo]::InvariantCulture)
}

function Get-PublishedState {
    # The state the router reads (overflow spec §4): the heartbeat, the
    # slots the host can serve with (a VM in teardown holds one it cannot),
    # every VM that holds a slot with its provisioning time, and the jobs it
    # would admit with unlimited slots, oldest first (admission counts the
    # ones whose run is still unknown among them). A PendingCommit VM is
    # Off and holds no slot (Get-FreeSlots agrees).
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [object[]]$Queued = @(), [Parameter(Mandatory)][datetimeoffset]$Now)
    $held = 0
    $holding = @()
    foreach ($vm in @($Journal.Vms.Values)) {
        if ($vm.Contains('PendingCommit') -and $vm['PendingCommit']) { continue }
        if ([string]$vm['Phase'] -eq 'discarding') { $held++; continue }
        $holding += [pscustomobject]@{ Lane = [string]$vm['Lane']; Since = ConvertTo-DateTimeOffset -Value $vm['StartedAt'] }
    }
    # Ordered by instant, never by text: a journal round trip turns the
    # times into [datetime], whose text order is not their order in time.
    $running = @()
    foreach ($entry in @($holding | Sort-Object Since, Lane)) {
        $running += [ordered]@{ lane = $entry.Lane; since = ConvertTo-StateTime $entry.Since }
    }
    $slots = @($Config.Slots).Count - $held
    # Not $queued: PowerShell names ignore case, so it would be $Queued, the parameter.
    $waiting = @()
    foreach ($job in @($Queued | Sort-Object CreatedAt, JobId)) {
        $waiting += [ordered]@{ lane = [string]$job.Lane; since = ConvertTo-StateTime $job.CreatedAt }
    }
    # Not serving while the breaker is tripped (OR-D4): the router then
    # sends every lane to the vendor instead of queueing on a failing host.
    $serving = ($slots -ge 1) -and (Test-HostServing -Config $Config -Journal $Journal -Now $Now)
    return [ordered]@{ v = 1; at = ConvertTo-StateTime $Now; serving = $serving; slots = [int]$slots; running = @($running); queued = @($waiting) }
}

function Publish-HomeState {
    # Once per change, and at least once a minute as a heartbeat; a failed
    # write is logged and never stops the tick (the router treats a stale
    # state as a dead host, the conservative outcome), and the next attempt
    # waits until that failure (PublishFailedAt) is a minute old, whatever
    # changed meanwhile. A PublishedAt that does not parse (a hand-edited
    # journal) counts as never published: the next publish overwrites it, so
    # it cannot stop publishing.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)]$State, [Parameter(Mandatory)][datetimeoffset]$Now)
    if (Test-RecentlyAt -Value $Journal['PublishFailedAt'] -Now $Now -WithinSeconds 60) { return }
    $json = $State | ConvertTo-Json -Compress -Depth 5
    $previous = [string]$Journal['PublishedState']
    $fresh = Test-RecentlyAt -Value $Journal['PublishedAt'] -Now $Now -WithinSeconds 60
    # The heartbeat field alone differs every tick: compare without it.
    $sameButTime = $previous -and (($previous -replace '"at":"[^"]*"', '') -eq ($json -replace '"at":"[^"]*"', ''))
    if ($sameButTime -and $fresh) { return }
    try {
        & $Adapters.PublishState $json
        $Journal['PublishedState'] = $json
        $Journal['PublishedAt'] = $Now.ToString('o')
        $Journal['PublishFailedAt'] = $null
    } catch {
        Write-PublishFailure -Journal $Journal -Adapters $Adapters -Now $Now -Level 'warn' -Failure $_
    }
}

function Write-PublishFailure {
    # One log line a minute (PublishFailedAt) for a publish that keeps
    # failing, whether the write failed or the state could not be built.
    # It runs inside the tick's last catch, so it never throws: a value that
    # does not parse (a hand-edited journal) counts as no earlier failure.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)][datetimeoffset]$Now, [Parameter(Mandatory)][string]$Level, [Parameter(Mandatory)]$Failure)
    if (Test-RecentlyAt -Value $Journal['PublishFailedAt'] -Now $Now -WithinSeconds 60) { return }
    $Journal['PublishFailedAt'] = $Now.ToString('o')
    & $Adapters.Log $Level "publish: $Failure"
}

function Remove-ActiveVm {
    # Every failure path: deregister, power off, drop the cache fork,
    # optionally schedule a retry, then delete the VM. Never commits. If
    # the delete fails, the entry stays in phase 'discarding' (its slot
    # stays held) and Update-ActiveVm finishes the teardown on a later
    # tick; the retry is registered here, once, before that can happen.
    # Every adapter call is voided: admission calls this, and admission's
    # output is the queue it publishes.
    [CmdletBinding()]
    param($Config, $Journal, [hashtable]$Adapters, [string]$Name, [string]$Reason, [bool]$Retry, [datetimeoffset]$Now)
    $vm = $Journal.Vms[$Name]
    $null = & $Adapters.Log 'warn' "${Name}: discard ($Reason)"
    if ($vm.RunnerId) { try { $null = & $Adapters.RemoveRunner $vm.RunnerId } catch { $null = & $Adapters.Log 'warn' "${Name}: RemoveRunner: $_" } }
    try { $null = & $Adapters.StopVmForce $Name } catch { $null = & $Adapters.Log 'warn' "${Name}: StopVmForce: $_" }
    if ($vm.CachePath) { try { $null = & $Adapters.DiscardCache $vm.CachePath } catch { $null = & $Adapters.Log 'warn' "${Name}: DiscardCache: $_" } }
    if ($Retry -and $vm.JobId) {
        Register-JobRetry -Retries $Journal.Retries -JobId ([int64]$vm.JobId) -Now $Now -BackoffSeconds ([int]$Config.RetryBackoffSeconds)
        Register-HostFailure -Config $Config -Journal $Journal -Adapters $Adapters -Reason $Reason -Now $Now
    }
    try {
        $null = & $Adapters.RemoveVm $Name $vm.Dir
    } catch {
        $null = & $Adapters.Log 'error' "${Name}: teardown failed, will retry: $_"
        $vm.Phase = 'discarding'
        return
    }
    $Journal.Vms.Remove($Name)
}

function Test-SiblingRunning {
    # Merge-VHD cannot write a cache parent while another VM holds a child
    # of it open, so a commit waits until every same-lane sibling is Off.
    [CmdletBinding()]
    param($Journal, [hashtable]$Adapters, [string]$Name, [string]$Lane)
    foreach ($other in @($Journal.Vms.Values)) {
        if ($other.Name -eq $Name -or $other.Lane -ne $Lane -or -not $other.CachePath) { continue }
        if ((& $Adapters.GetVmState $other.Name) -ne 'Off') { return $other.Name }
    }
    return $null
}

$script:AcceptanceLanes = 'rust', 'app', 'model', 'shell'

function Register-GoldenAcceptance {
    # Facts only (overflow spec OR-D7): a green job on a VM provisioned after
    # the golden's seal vouches for its lane on that image. A new seal starts
    # over. Stamps are compared as instants: a journal round trip turns them
    # into [datetime]. Deletion is decided elsewhere, by Test-GoldenPrevRemovable.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)]$Vm)
    $stamp = & $Adapters.GetGoldenStamp
    if (-not $stamp) { return }
    $sealed = ConvertTo-DateTimeOffset -Value $stamp
    $accepted = $Journal['GoldenAccepted']
    if ($null -eq $accepted -or (ConvertTo-DateTimeOffset -Value $accepted['Golden']) -ne $sealed) {
        $accepted = @{ Golden = [string]$stamp; Lanes = @(); Complete = $false }
        $Journal['GoldenAccepted'] = $accepted
    }
    if ((ConvertTo-DateTimeOffset -Value $Vm.StartedAt) -le $sealed) { return }
    if (@($accepted['Lanes']) -notcontains [string]$Vm.Lane) { $accepted['Lanes'] = @(@($accepted['Lanes']) + [string]$Vm.Lane) }
    if ($accepted['Complete']) { return }
    foreach ($lane in $script:AcceptanceLanes) { if (@($accepted['Lanes']) -notcontains $lane) { return } }
    $accepted['Complete'] = $true
    & $Adapters.Log 'info' "golden: $($script:AcceptanceLanes[0..2] -join ', ') and $($script:AcceptanceLanes[3]) are green on the current image"
}

function Test-GoldenPrevRemovable {
    # The one statement of when the previous image may go: the acceptance is
    # of THIS image (stamps equal as instants), this image is sealed, every
    # lane vouched, and no VM directory predates the seal.
    [CmdletBinding()]
    param($Accepted, [AllowNull()][string]$CurrentStamp, [bool]$CurrentSealed, [string[]]$VmCreationTimes = @(), [string[]]$Lanes = $script:AcceptanceLanes)
    if ($null -eq $Accepted -or -not $CurrentStamp -or -not $CurrentSealed) { return $false }
    if ($null -eq $Accepted['Golden']) { return $false }
    $sealed = ConvertTo-DateTimeOffset -Value $CurrentStamp
    if ((ConvertTo-DateTimeOffset -Value $Accepted['Golden']) -ne $sealed) { return $false }
    foreach ($lane in $Lanes) { if (@($Accepted['Lanes']) -notcontains $lane) { return $false } }
    foreach ($created in @($VmCreationTimes)) { if ((ConvertTo-DateTimeOffset -Value $created) -le $sealed) { return $false } }
    return $true
}

function Complete-ActiveVm {
    # The VM reached Off and the loop did not force it off. Ask GitHub what
    # ran, then commit or discard. An API failure propagates: the entry
    # stays in the journal and the next tick tries again (never orphan,
    # never commit blindly). GitHub's record can lag the guest's shutdown,
    # so a job not yet completed gets up to six ticks (SettleWaits) before
    # the predicate runs on whatever GitHub reports then. A commit that
    # must wait for a same-lane sibling is remembered as PendingCommit so
    # later ticks skip the API lookups. Off with no job resolved means the
    # guest never ran one (a bootstrap failure): the runner is freed and
    # the job retried with back-off, so a broken image cannot provision VMs
    # in a tight loop. A runner seen busy (Claimed) did run a job, one the
    # host cannot resolve: the image works, so no retry is struck against
    # the job the VM was provisioned for.
    [CmdletBinding()]
    param($Config, $Journal, [hashtable]$Adapters, [string]$Name, [datetimeoffset]$Now)
    $vm = $Journal.Vms[$Name]
    if ($vm.PendingCommit) {
        $sibling = Test-SiblingRunning -Journal $Journal -Adapters $Adapters -Name $Name -Lane $vm.Lane
        if ($sibling) { return }
        $parentGeneration = [int](& $Adapters.GetGeneration $vm.Lane)
        if ($parentGeneration -eq [int]$vm.ForkGeneration) {
            $generation = & $Adapters.CommitCache $vm.Lane $vm.CachePath
            & $Adapters.Log 'info' "${Name}: commit $($vm.Lane) generation $generation (after waiting for a sibling)"
            try { Register-GoldenAcceptance -Journal $Journal -Adapters $Adapters -Vm $vm } catch { & $Adapters.Log 'warn' "golden: $_" }
        } else {
            & $Adapters.DiscardCache $vm.CachePath
            & $Adapters.Log 'info' "${Name}: discard (generation moved while waiting: fork $($vm.ForkGeneration), parent $parentGeneration)"
        }
        & $Adapters.RemoveVm $Name $vm.Dir
        $Journal.Vms.Remove($Name)
        return
    }
    # The runner carries its lane's label only, so a seen job of another
    # lane cannot have taken it: those are never looked up (API budget).
    $candidates = @()
    foreach ($key in @($Journal.SeenJobs.Keys)) {
        $seen = $Journal.SeenJobs[$key]
        if ($null -ne $seen -and [string]$seen['Lane'] -ceq [string]$vm.Lane) { $candidates += [int64]$key }
    }
    $job = Resolve-RunnerJob -RunnerName $Name -AdmittedJobId ([int64]$vm.JobId) -CandidateJobIds $candidates -GetJob $Adapters.GetJob
    if ($null -eq $job) {
        $seenBusy = [bool]$vm['Claimed']
        if ($seenBusy) {
            & $Adapters.Log 'warn' "${Name}: shut down without a job the host can resolve, although its runner was seen busy; no retry"
        } else {
            # The guest's slate.error was kept by Update-ActiveVm while the VM ran.
            # One last read is only a fallback (Hyper-V has no guest items for an
            # Off VM, so it normally finds nothing); it never stops the teardown.
            $guestError = [string]$vm['GuestError']
            if (-not $guestError) {
                try { $guestError = [string](& $Adapters.GetGuestError $Name) } catch { $guestError = '' }
            }
            & $Adapters.Log 'warn' "${Name}: shut down without running a job (guest bootstrap failure?) guest error: $guestError"
        }
        if ($vm.RunnerId) { try { & $Adapters.RemoveRunner $vm.RunnerId } catch { & $Adapters.Log 'warn' "${Name}: RemoveRunner: $_" } }
        if ($vm.CachePath) { & $Adapters.DiscardCache $vm.CachePath }
        & $Adapters.RemoveVm $Name $vm.Dir
        if (-not $seenBusy) {
            Register-JobRetry -Retries $Journal.Retries -JobId ([int64]$vm.JobId) -Now $Now -BackoffSeconds ([int]$Config.RetryBackoffSeconds)
            Register-HostFailure -Config $Config -Journal $Journal -Adapters $Adapters -Reason 'shut down without running a job' -Now $Now
        }
        $Journal.Vms.Remove($Name)
        return
    }
    $status = ''
    if ($job.PSObject.Properties['status']) { $status = [string]$job.status }
    if ($status -cne 'completed' -and [int]$vm['SettleWaits'] -lt 6) {
        $vm['SettleWaits'] = [int]$vm['SettleWaits'] + 1
        if ([int]$vm['SettleWaits'] -eq 1) {
            & $Adapters.Log 'info' "${Name}: job $($job.id) is $status on GitHub while its VM is off; waiting up to 6 ticks for it to complete"
        }
        return
    }
    if ($null -ne $job -and [string]$job.conclusion -eq 'success') {
        try { Register-GoldenAcceptance -Journal $Journal -Adapters $Adapters -Vm $vm } catch { & $Adapters.Log 'warn' "golden: $_" }
    }
    $run = $null
    if ($null -ne $job) { $run = & $Adapters.GetRun ([int64]$job.run_id) }
    if ($vm.CachePath) {
        $parentGeneration = [int](& $Adapters.GetGeneration $vm.Lane)
        $decision = Test-CommitEligible -Job $job -Run $run -RunnerName $Name -ForcedOff $false `
            -ParentGeneration $parentGeneration -ForkGeneration ([int]$vm.ForkGeneration) `
            -TrustedRepo $Config.TrustedRepo -TrustedBranch $Config.TrustedBranch -TrustedEvents @($Config.TrustedEvents)
        if ($decision.Eligible) {
            $sibling = Test-SiblingRunning -Journal $Journal -Adapters $Adapters -Name $Name -Lane $vm.Lane
            if ($sibling) {
                $vm.PendingCommit = $true
                if ($null -ne $job) { $Journal.SeenJobs.Remove([string]$job.id) }
                & $Adapters.Log 'info' "${Name}: commit of $($vm.Lane) waits until sibling $sibling is off"
                return
            }
            $generation = & $Adapters.CommitCache $vm.Lane $vm.CachePath
            & $Adapters.Log 'info' "${Name}: commit $($vm.Lane) generation $generation ($($decision.Reason))"
        } else {
            & $Adapters.DiscardCache $vm.CachePath
            & $Adapters.Log 'info' "${Name}: discard ($($decision.Reason))"
        }
    } else {
        $outcome = 'no job resolved'
        if ($null -ne $job) { $outcome = "conclusion: $($job.conclusion)" }
        & $Adapters.Log 'info' "${Name}: done, lane $($vm.Lane) has no cache ($outcome)"
    }
    & $Adapters.RemoveVm $Name $vm.Dir
    if ($null -ne $job) { $Journal.SeenJobs.Remove([string]$job.id) }
    $Journal.Vms.Remove($Name)
}

function Update-ActiveVm {
    [CmdletBinding()]
    param($Config, $Journal, [hashtable]$Adapters, [string]$Name, [datetimeoffset]$Now)
    $vm = $Journal.Vms[$Name]
    if ($vm.Phase -eq 'discarding') {
        # An earlier teardown could not delete the VM: finish it. The
        # runner and the retry were handled then. This VM never settles
        # or commits, and its slot stays held until it is gone. A VM that
        # Hyper-V no longer has is dropped as it is; the CleanVmDirs of
        # the startup sweep removes any directory left behind.
        $state = & $Adapters.GetVmState $Name
        if ($state -eq 'Missing') {
            $Journal.Vms.Remove($Name)
            & $Adapters.Log 'info' "${Name}: teardown finished (the VM is gone)"
            return
        }
        if ($state -eq 'Unknown') {
            & $Adapters.Log 'warn' "${Name}: Hyper-V did not answer; the teardown waits for the next tick"
            return
        }
        try {
            & $Adapters.StopVmForce $Name
            & $Adapters.RemoveVm $Name $vm.Dir
        } catch {
            & $Adapters.Log 'error' "${Name}: teardown failed, will retry: $_"
            return
        }
        $Journal.Vms.Remove($Name)
        & $Adapters.Log 'info' "${Name}: teardown finished"
        return
    }
    $state = & $Adapters.GetVmState $Name
    if ($state -eq 'Unknown') {
        & $Adapters.Log 'warn' "${Name}: Hyper-V did not answer; leaving the VM alone until the next tick"
        return
    }
    if ($state -eq 'Off') {
        Complete-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Now $Now
        return
    }
    if ($state -ne 'Running') {
        Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason "vm state $state" -Retry $true -Now $Now
        return
    }
    if (($vm.Phase -eq 'provisioned' -or $vm.Phase -eq 'handed') -and -not $vm.Claimed) {
        # A failed guest bootstrap publishes slate.error and stays up 30 s;
        # Hyper-V exposes guest KVP items only while the VM runs, so keep it
        # now for the settle log. Best effort, never fatal. Once the runner
        # is claimed the bootstrap is behind it, so the WMI read stops. Index
        # syntax: a missing key would throw under strict mode as a property.
        try { $guestError = & $Adapters.GetGuestError $Name } catch { $guestError = $null }
        if ($guestError -and [string]$guestError -ne [string]$vm['GuestError']) {
            $vm['GuestError'] = [string]$guestError
            & $Adapters.Log 'warn' "${Name}: guest reports: $guestError"
        }
    }
    $startedAt = ConvertTo-DateTimeOffset -Value $vm.StartedAt
    if ($vm.Phase -eq 'provisioned') {
        $heartbeat = & $Adapters.GetHeartbeat $Name
        if ($heartbeat -eq 'OK') {
            # The handoff is not retryable in place: AddKvpItems rejects
            # keys that already exist and a second registration would leak
            # a JIT runner per tick. Any failure discards this VM and
            # schedules the job for a fresh one.
            try {
                $registration = & $Adapters.NewJitRunner $Name @("slate-win-$($vm.Lane)")
                $vm.RunnerId = $registration.RunnerId
                $items = Split-KvpChunks -Text ([string]$registration.EncodedJitConfig)
                $items['slate.lane'] = [string]$vm.Lane
                $items['slate.ip'] = [string]$vm.SlotIp
                $items['slate.gateway'] = [string]$Config.Gateway
                $items['slate.prefix'] = [string]$Config.PrefixLength
                $items['slate.dns'] = [string]$Config.Dns
                $items['slate.cache'] = $(if ($vm.CachePath) { '1' } else { '0' })
                $items['slate.job'] = [string]$vm.JobId
                & $Adapters.SendKvp $Name $items
            } catch {
                & $Adapters.Log 'error' "${Name}: handoff failed: $_"
                Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason 'handoff failed' -Retry $true -Now $Now
                return
            }
            $vm.Phase = 'handed'
            $vm.HandedAt = $Now.ToString('o')
            & $Adapters.Log 'info' "${Name}: handed off (runner $($registration.RunnerId), job $($vm.JobId))"
        } elseif ($Now -ge $startedAt.AddSeconds([int]$Config.HeartbeatTimeoutSeconds)) {
            Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason 'no heartbeat' -Retry $true -Now $Now
        }
        return
    }
    $maxMinutes = [int]$Config.Lanes[$vm.Lane].MaxMinutes
    if (Test-VmExpired -StartedAt $startedAt -MaxMinutes $maxMinutes -Now $Now) {
        & $Adapters.Log 'error' "${Name}: expired after $maxMinutes min + grace; forcing off"
        Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason "expired after $maxMinutes min + grace" -Retry $false -Now $Now
        return
    }
    if (-not $vm.Claimed) {
        $handedAt = ConvertTo-DateTimeOffset -Value $vm.HandedAt
        $waited = ($Now - $handedAt).TotalSeconds
        $timeout = [int]$Config.UnclaimedTimeoutSeconds
        if ($waited -ge $timeout) {
            # Once a minute, not every tick: nothing changes between ticks and
            # each check is two API calls (ClaimCheckedAt; index syntax, see
            # above; a value that does not parse counts as never checked).
            if (Test-RecentlyAt -Value $vm['ClaimCheckedAt'] -Now $Now -WithinSeconds 60) { return }
            $vm['ClaimCheckedAt'] = $Now.ToString('o')
            $runner = & $Adapters.GetRunner $vm.RunnerId
            if ($null -ne $runner -and $runner.busy) {
                $vm.Claimed = $true
                & $Adapters.Log 'info' "${Name}: claimed"
                Register-HostSuccess -Journal $Journal -Adapters $Adapters
                return
            }
            $job = & $Adapters.GetJob ([int64]$vm.JobId)
            if ($null -ne $job -and [string]::Equals([string]$job.runner_name, $Name, [System.StringComparison]::Ordinal)) {
                # Running or already completed on this runner: the VM is on
                # its way to Off, so settle it then (a completed single-use
                # runner is already deleted and would otherwise look unclaimed).
                $vm.Claimed = $true
                & $Adapters.Log 'info' "${Name}: claimed (job $($job.status) on this runner)"
                Register-HostSuccess -Journal $Journal -Adapters $Adapters
                return
            }
            if ($null -eq $job -or $job.status -ne 'queued') {
                Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason 'unclaimed and the admitted job is no longer queued' -Retry $false -Now $Now
            } elseif ($waited -ge 3 * $timeout) {
                Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason 'unclaimed while the job stays queued' -Retry $true -Now $Now
            }
        }
    }
}

function Update-SeenJob {
    # A lane job's SeenJobs entry (created on first sight; an older entry
    # gains Foreign, Repo and RunCheckedAt), and while its run is unknown
    # (Foreign $null), one read of the run at most once a minute
    # (RunCheckedAt; a value that does not parse counts as never checked),
    # whose answer is remembered as Foreign, with the run's head repository
    # as Repo ('' when GitHub names none), and never read again. Admission
    # and the handoff reconciliation both learn a job's run through here.
    # Returns, for the caller's log: Seen (the entry), Learned (this call
    # read the run), Run (that run), Failure (the error of a read that
    # failed now) and FirstTry (no earlier read was recorded).
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters, [Parameter(Mandatory)]$Job, [Parameter(Mandatory)][datetimeoffset]$Now)
    $key = [string]$Job.JobId
    if (-not $Journal.SeenJobs.Contains($key)) {
        $Journal.SeenJobs[$key] = @{ RunId = [int64]$Job.RunId; Lane = $Job.Lane; FirstSeenAt = $Now.ToString('o'); Foreign = $null; Repo = $null; RunCheckedAt = $null }
    }
    $seen = $Journal.SeenJobs[$key]
    if (-not $seen.Contains('Foreign')) { $seen['Foreign'] = $null; $seen['RunCheckedAt'] = $null }
    if (-not $seen.Contains('Repo')) { $seen['Repo'] = $null }
    $result = [pscustomobject]@{ Seen = $seen; Learned = $false; Run = $null; Failure = $null; FirstTry = $false }
    if ($null -ne $seen['Foreign']) { return $result }
    $checked = $seen['RunCheckedAt']
    if (Test-RecentlyAt -Value $checked -Now $Now -WithinSeconds 60) { return $result }
    $seen['RunCheckedAt'] = $Now.ToString('o')
    $result.FirstTry = -not $checked
    try {
        $result.Run = & $Adapters.GetRun ([int64]$Job.RunId)
        $seen['Repo'] = Get-RunRepository -Run $result.Run
        $seen['Foreign'] = -not (Test-TrustedRun -Run $result.Run -TrustedRepo $Config.TrustedRepo)
        $result.Learned = $true
    } catch {
        $result.Failure = $_
    }
    return $result
}

function Invoke-Admission {
    [CmdletBinding()]
    param($Config, $Journal, [hashtable]$Adapters, [datetimeoffset]$Now)
    $raw = @(& $Adapters.GetLaneJobs)
    $queued = @(Select-QueuedLaneJobs -Jobs $raw -Lanes @($Config.Lanes.Keys))
    # Only this repository's runs get a VM (overflow spec OR-D5): a fork
    # pull request runs its own workflow file and can name the home label,
    # so the check lives here, where the fork cannot edit it. Each job's run
    # is read once and remembered; a read that fails is retried once a
    # minute and logged on its first failure, and the job waits meanwhile.
    # A job whose run is still unknown is never admitted but counts in the
    # published queue (the conservative direction: the router must not see
    # a slot free that this job will take once its run is read).
    $admissible = @()
    $publishable = @()
    foreach ($q in $queued) {
        $check = Update-SeenJob -Config $Config -Journal $Journal -Adapters $Adapters -Job $q -Now $Now
        if ($check.Failure -and $check.FirstTry) {
            & $Adapters.Log 'warn' "admission: job $($q.JobId): run $($q.RunId) could not be read: $($check.Failure)"
        }
        if ($check.Learned -and $check.Seen['Foreign']) {
            & $Adapters.Log 'warn' "admission: skipped job $($q.JobId) of $(Format-RunRepository -Run $check.Run) (not the trusted repository)"
        }
        if ($check.Seen['Foreign'] -eq $false) { $admissible += $q }
        if ($check.Seen['Foreign'] -ne $true) { $publishable += $q }
    }
    # GitHub hands a runner whichever queued job matches its label first,
    # not the job this host provisioned the VM for. A handed VM whose
    # runner the listing shows on another job is claimed by that job, and
    # the job it was provisioned for is still queued and free to get a VM
    # of its own. Without this, two model shards queued together ran one
    # after the other: the second was held as "already has a VM" until the
    # first VM, which had taken it, finished. The jobs on the host's
    # runners come from the whole listing, not the lane-filtered queue: a
    # job without a lane label can take a runner too (overflow spec §7).
    $onRunners = @(Select-HostRunnerJobs -Jobs $raw -RunnerNames @($Journal.Vms.Keys) -Lanes @($Config.Lanes.Keys))
    foreach ($name in @($Journal.Vms.Keys)) {
        $vm = $Journal.Vms[$name]
        if ($vm.Phase -ne 'handed') { continue }
        $running = @($onRunners | Where-Object { [string]::Equals($_.RunnerName, $name, [System.StringComparison]::Ordinal) })
        if ($running.Count -eq 0) { continue }
        $job = $running[0]
        if ([int64]$job.JobId -ne [int64]$vm.JobId) {
            # Not the job it was provisioned for. A job without a lane label
            # is never one this host would admit, whatever its run. For a
            # lane job the answer remembered on SeenJobs comes first; an
            # unknown run is read at most once a minute, each failed read is
            # logged, and the VM is left alone until the answer is known.
            $evict = $true
            $repo = Format-RunRepository -Run $null
            $why = 'not the trusted repository'
            if ($job.Lane) {
                $check = Update-SeenJob -Config $Config -Journal $Journal -Adapters $Adapters -Job $job -Now $Now
                if ($check.Failure) { & $Adapters.Log 'warn' "handoff: ${name}: run $($job.RunId) of job $($job.JobId) could not be read: $($check.Failure)" }
                if ($null -eq $check.Seen['Foreign']) { continue }
                $evict = [bool]$check.Seen['Foreign']
                # The repository remembered with the answer names it in the
                # log: the run is never read again for that.
                if ($check.Seen['Repo']) { $repo = [string]$check.Seen['Repo'] }
            } else {
                # A job without a lane label has no SeenJobs entry: its run is
                # read here, only to name the repository, so a failed read
                # never delays forcing the VM off. The log says what is true:
                # it goes for the label, unless its run is read as foreign.
                $run = $null
                try { $run = & $Adapters.GetRun ([int64]$job.RunId) } catch { $run = $null }
                $repo = Format-RunRepository -Run $run
                if ($null -eq $run -or (Test-TrustedRun -Run $run -TrustedRepo $Config.TrustedRepo)) { $why = 'no lane label' }
            }
            if ($evict) {
                & $Adapters.Log 'warn' "handoff: ${name} runs job $($job.JobId) of $repo ($why); forcing it off"
                Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $name -Reason "runner taken by job $($job.JobId) of $repo ($why)" -Retry $false -Now $Now
                continue
            }
            & $Adapters.Log 'info' "${name}: runs job $($job.JobId), not job $($vm.JobId) it was provisioned for, which stays queued for a VM of its own"
            $vm.JobId = [int64]$job.JobId
            $vm.RunId = [int64]$job.RunId
        }
        if (-not $vm.Claimed) {
            $vm.Claimed = $true
            & $Adapters.Log 'info' "${name}: claimed (job $($job.JobId) in progress)"
            Register-HostSuccess -Journal $Journal -Adapters $Adapters
        }
    }
    foreach ($key in @($Journal.SeenJobs.Keys)) {
        $seen = ConvertTo-DateTimeOffset -Value $Journal.SeenJobs[$key].FirstSeenAt
        if ($Now -gt $seen.AddHours(24)) { $Journal.SeenJobs.Remove($key) }
    }
    # A retry entry outlives its job by at most a day: GitHub drops a job
    # from the queue after 24 h, so an older entry can only block a job id
    # that no longer exists and grow the journal.
    foreach ($key in @($Journal.Retries.Keys)) {
        $next = ConvertTo-DateTimeOffset -Value $Journal.Retries[$key]['NextAt']
        if ($Now -gt $next.AddHours(24)) { $Journal.Retries.Remove($key) }
    }
    $free = @(Get-FreeSlots -Config $Config -Journal $Journal)
    $admit = @(Select-JobsToAdmit -Candidates $admissible -ActiveVms $Journal.Vms -FreeSlots $free.Count -Retries $Journal.Retries -Now $Now -RetryCap ([int]$Config.RetryCap))
    for ($i = 0; $i -lt $admit.Count; $i++) {
        $job = $admit[$i]
        $slot = $free[$i]
        $name = New-RunnerName -Lane $job.Lane
        try {
            $created = & $Adapters.NewVm $name $job.Lane
            $Journal.Vms[$name] = @{
                Name = $name; Lane = $job.Lane; Slot = [int]$slot.Index; SlotIp = [string]$slot.Ip
                JobId = [int64]$job.JobId; RunId = [int64]$job.RunId
                Dir = [string]$created.Dir; CachePath = $created.CachePath; ForkGeneration = [int]$created.ForkGeneration
                Phase = 'provisioned'; StartedAt = $Now.ToString('o'); HandedAt = $null; RunnerId = $null; Claimed = $false; PendingCommit = $false
                GuestError = $null; SettleWaits = 0; ClaimCheckedAt = $null
            }
            # Voided, as in Remove-ActiveVm: this function's output is the queue it publishes.
            $null = & $Adapters.StartVm $name
            & $Adapters.Log 'info' "${name}: provisioned for job $($job.JobId) (lane $($job.Lane), slot $($slot.Index), fork generation $($created.ForkGeneration))"
        } catch {
            & $Adapters.Log 'error' "${name}: provisioning failed: $_"
            if ($Journal.Vms.Contains($name)) {
                Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $name -Reason 'provisioning failed' -Retry $true -Now $Now
            } else {
                Register-JobRetry -Retries $Journal.Retries -JobId ([int64]$job.JobId) -Now $Now -BackoffSeconds ([int]$Config.RetryBackoffSeconds)
                Register-HostFailure -Config $Config -Journal $Journal -Adapters $Adapters -Reason 'provisioning failed' -Now $Now
                try { $null = & $Adapters.RemoveVm $name (Join-WinPath $Config.VmDir $name) } catch { & $Adapters.Log 'warn' "${name}: RemoveVm: $_" }
            }
        }
    }
    return @(Select-JobsToAdmit -Candidates $publishable -ActiveVms $Journal.Vms -FreeSlots ([int]::MaxValue) -Retries $Journal.Retries -Now $Now -RetryCap ([int]$Config.RetryCap))
}

function Invoke-OrchestratorTick {
    # One pass: advance or settle every active VM, admit queued jobs, then
    # publish the pool's state for the router (overflow spec §4). A failure
    # in one VM never blocks the others. A discovery failure means no
    # admission this tick, and no publish either: the last state ages into
    # stale, so a host that cannot list or admit routes away by itself
    # instead of looking idle. A state that cannot be built or written is
    # logged once a minute and never stops the tick.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)]$Journal,
        [Parameter(Mandatory)][hashtable]$Adapters,
        [datetimeoffset]$Now = [datetimeoffset]::UtcNow
    )
    try { Invoke-LogPrune -Config $Config -Journal $Journal -Adapters $Adapters -Today (Get-OrchestratorLogDate -Now $Now) }
    catch { & $Adapters.Log 'error' "logs: $_" }
    foreach ($name in @($Journal.Vms.Keys)) {
        try { Update-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $name -Now $Now }
        catch { & $Adapters.Log 'error' "${name}: $_" }
    }
    $queued = $null
    try { $queued = @(Invoke-Admission -Config $Config -Journal $Journal -Adapters $Adapters -Now $Now) }
    catch { & $Adapters.Log 'error' "admission: $_" }
    try { Update-HostHealth -Config $Config -Journal $Journal -Adapters $Adapters -Now $Now }
    catch { & $Adapters.Log 'error' "health: $_" }
    if ($null -ne $queued) {
        try { Publish-HomeState -Journal $Journal -Adapters $Adapters -State (Get-PublishedState -Config $Config -Journal $Journal -Queued $queued -Now $Now) -Now $Now }
        catch { Write-PublishFailure -Journal $Journal -Adapters $Adapters -Now $Now -Level 'error' -Failure $_ }
    }
}

function Invoke-StartupSweep {
    # Reconcile the journal with the host instead of trusting either. A VM
    # the journal shows handed off (its JIT config delivered) that Hyper-V
    # still reports Running is kept, with its slot, the seen jobs and the
    # retries: a loop relaunch after a crash, or a manual run, must not
    # kill the jobs in flight. Everything else goes: every other slate-win-*
    # VM is turned off and removed (cache children die with their VM
    # directory; parents are never touched), every slate-win-* runner no
    # kept VM owns is deleted whatever its status (its VM is gone or about
    # to be), and the journal keeps only the kept entries. A host reboot
    # turns every VM off (AutomaticStopAction TurnOff), so after one nothing
    # is kept and the journal starts empty. A VM still waiting for its
    # handoff is never kept: the handoff is not idempotent (AddKvpItems
    # rejects existing keys, a second JIT registration would leak a runner).
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters)
    $kept = @{}
    foreach ($name in @($Journal.Vms.Keys)) {
        $vm = $Journal.Vms[$name]
        if ($null -ne $vm -and $vm.Contains('Phase') -and $vm['Phase'] -eq 'handed' -and (& $Adapters.GetVmState $name) -eq 'Running') {
            $kept[$name] = $true
            & $Adapters.Log 'info' "sweep: kept running vm $name (job $($vm['JobId']), handed off)"
        }
    }
    $runners = @(& $Adapters.ListRunners)
    foreach ($name in @(Get-StaleRunnerNames -Runners $runners -ActiveNames @($kept.Keys) -IncludeOnline)) {
        $runner = $runners | Where-Object { $_.name -eq $name } | Select-Object -First 1
        try { & $Adapters.RemoveRunner $runner.id; & $Adapters.Log 'info' "sweep: removed stale runner $name" }
        catch { & $Adapters.Log 'warn' "sweep: runner ${name}: $_" }
    }
    foreach ($name in @(& $Adapters.ListVms)) {
        if ($kept.ContainsKey($name)) { continue }
        try {
            & $Adapters.StopVmForce $name
            & $Adapters.RemoveVm $name (Join-WinPath $Config.VmDir $name)
            & $Adapters.Log 'info' "sweep: removed vm $name"
        } catch { & $Adapters.Log 'warn' "sweep: vm ${name}: $_" }
    }
    try { & $Adapters.CleanVmDirs @($kept.Keys) } catch { & $Adapters.Log 'warn' "sweep: CleanVmDirs: $_" }
    foreach ($name in @($Journal.Vms.Keys)) {
        if (-not $kept.ContainsKey($name)) { $Journal.Vms.Remove($name) }
    }
    if ($kept.Count -eq 0) {
        $Journal.Retries = @{}
        $Journal.SeenJobs = @{}
    }
    $Journal['LogsPrunedOn'] = $null
    # The breaker: a restart is what follows a repair, so the failures in a
    # row are forgotten and the next state says serving.
    $failures = [int](Get-HostHealth -Journal $Journal)['Failures']
    if ($failures -gt 0) { & $Adapters.Log 'info' "health: $failures failures in a row forgotten at start" }
    Reset-HostHealth -Journal $Journal
}

function Select-SlateKvpItems {
    # Guest side: the host's KVP items appear as registry values under
    # HKLM\SOFTWARE\Microsoft\Virtual Machine\External. Keep ours.
    # Get-ItemProperty outputs nothing for a key with no values yet (the
    # key can exist before the host's items arrive): that is no items.
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowNull()]$Properties)
    $items = @{}
    if ($null -eq $Properties) { return $items }
    foreach ($property in $Properties.PSObject.Properties) {
        if ($property.Name -like 'slate.*') { $items[$property.Name] = [string]$property.Value }
    }
    return $items
}

function New-RandomPassword {
    # Host-only. Alphanumeric so it survives every quoting context the
    # unattend, winlogon registry and scheduled-task registration use.
    [CmdletBinding()]
    param([int]$Length = 24)
    $alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789'
    $chars = New-Object char[] $Length
    for ($i = 0; $i -lt $Length; $i++) {
        $chars[$i] = $alphabet[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32(0, $alphabet.Length)]
    }
    return (-join $chars)
}

function Expand-UnattendTemplate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TemplatePath,
        [Parameter(Mandatory)][string]$ProductKey,
        [Parameter(Mandatory)][string]$ProvisionPassword,
        [Parameter(Mandatory)][string]$RunnerPassword
    )
    if ($ProductKey -notmatch '^[A-Z0-9]{5}(-[A-Z0-9]{5}){4}$') { throw 'product key must look like XXXXX-XXXXX-XXXXX-XXXXX-XXXXX' }
    $text = Get-Content -Raw -LiteralPath $TemplatePath
    $text = $text.Replace('__PRODUCT_KEY__', $ProductKey)
    $text = $text.Replace('__PROVISION_PASSWORD__', [System.Security.SecurityElement]::Escape($ProvisionPassword))
    $text = $text.Replace('__RUNNER_PASSWORD__', [System.Security.SecurityElement]::Escape($RunnerPassword))
    if ($text -match '__[A-Z_]+__') { throw "unattend placeholder left unrendered: $($Matches[0])" }
    return $text
}

function Add-SidToUserRight {
    # secedit INF surgery: make sure "*<sid>" is listed under <right> in
    # [Privilege Rights]. Pure so it can be tested; Set-LocalUserRights
    # applies it.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$IniText,
        [Parameter(Mandatory)][string]$Right,
        [Parameter(Mandatory)][string]$Sid
    )
    $entry = "*$Sid"
    $lines = @($IniText -split "`r?`n")
    $out = @()
    $inSection = $false
    $sectionSeen = $false
    $done = $false
    foreach ($line in $lines) {
        if ($line -match '^\[(.+)\]\s*$') {
            if ($inSection -and -not $done) { $out += "$Right = $entry"; $done = $true }
            $inSection = ($Matches[1] -eq 'Privilege Rights')
            if ($inSection) { $sectionSeen = $true }
            $out += $line
            continue
        }
        if ($inSection -and $line -match ('^\s*' + [regex]::Escape($Right) + '\s*=\s*(.*)$')) {
            $values = @($Matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
            if ($values -notcontains $entry) { $values += $entry }
            $out += "$Right = $($values -join ',')"
            $done = $true
            continue
        }
        $out += $line
    }
    if (-not $sectionSeen) { $out += '[Privilege Rights]' }
    if (-not $done) { $out += "$Right = $entry" }
    return ($out -join "`r`n")
}

function Set-LocalUserRights {
    # Elevated. By default the orchestrator account's rights: deny
    # interactive and remote-interactive logon, grant batch logon (scheduled
    # tasks). The golden build's phase 1 uses it too, to give runner the
    # one privilege a job needs beyond a standard user's: creating symbolic
    # links (Windows PowerShell 5.1 there, so nothing 7-only here).
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Sid,
        [string[]]$Rights = @('SeDenyInteractiveLogonRight', 'SeDenyRemoteInteractiveLogonRight', 'SeBatchLogonRight')
    )
    # secedit sets each listed right to exactly its listed holders, so a
    # template that lost the exported rights (a failed read, an export
    # without the section) would strip every other holder: any failure
    # throws before /configure, whatever the caller's preference.
    $ErrorActionPreference = 'Stop'
    $dir = Join-Path $env:TEMP ('slate-secedit-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    try {
        $inf = Join-Path $dir 'rights.inf'
        $db = Join-Path $dir 'rights.sdb'
        & secedit.exe /export /cfg $inf /areas USER_RIGHTS | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "secedit /export exited $LASTEXITCODE" }
        $text = Get-Content -Raw -LiteralPath $inf
        if ($text -notmatch '(?m)^\[Privilege Rights\]') { throw "secedit /export wrote no [Privilege Rights] section to $inf" }
        foreach ($right in $Rights) {
            $text = Add-SidToUserRight -IniText $text -Right $right -Sid $Sid
        }
        Set-Content -LiteralPath $inf -Value $text -Encoding unicode
        & secedit.exe /configure /db $db /cfg $inf /areas USER_RIGHTS | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "secedit /configure exited $LASTEXITCODE" }
    } finally {
        Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Export-ModuleMember -Function Get-LaneFromLabels, New-RunnerName, Split-KvpChunks, Join-KvpChunks,
    ConvertTo-DateTimeOffset, Select-QueuedLaneJobs, Select-HostRunnerJobs, Select-JobsToAdmit, Register-JobRetry, Get-HostHealth, Test-HostServing, Register-HostFailure, Register-HostSuccess, Reset-HostHealth, Update-HostHealth, Test-VmExpired, Get-StaleRunnerNames, Test-CommitEligible, Resolve-RunnerJob, Test-IPv4InPrefix, Get-CiHostConfig, New-Journal, Read-Journal, Write-Journal, Write-CiLog,
    Get-FreeSlots, Remove-ActiveVm, Test-SiblingRunning, Complete-ActiveVm, Update-ActiveVm, Invoke-Admission, Invoke-OrchestratorTick, Invoke-StartupSweep, Select-SlateKvpItems,
    Get-PublishedState, Publish-HomeState, Get-RunRepository, Test-TrustedRun, Get-OrchestratorLogDate, Get-OrchestratorLogName, Get-ExpiredLogNames, Invoke-LogPrune, Register-GoldenAcceptance, Test-GoldenPrevRemovable,
    Get-CiHostPaths, Read-JournalSnapshot, Remove-SealedFile,
    New-RandomPassword, Expand-UnattendTemplate, Add-SidToUserRight, Set-LocalUserRights
