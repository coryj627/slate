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

# ---- orchestrator state machine (host-only; adapters injected) ----

function Get-FreeSlots {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal)
    $used = @{}
    foreach ($vm in $Journal.Vms.Values) { $used[[string]$vm.Slot] = $true }
    $free = @()
    foreach ($slot in @($Config.Slots | Sort-Object { [int]$_.Index })) {
        if (-not $used.ContainsKey([string]$slot.Index)) { $free += $slot }
    }
    return @($free)
}

function Remove-ActiveVm {
    # Every failure path: deregister, power off, drop the cache fork,
    # delete the VM, optionally schedule a retry. Never commits.
    [CmdletBinding()]
    param($Config, $Journal, [hashtable]$Adapters, [string]$Name, [string]$Reason, [bool]$Retry, [datetimeoffset]$Now)
    $vm = $Journal.Vms[$Name]
    & $Adapters.Log 'warn' "${Name}: discard ($Reason)"
    if ($vm.RunnerId) { try { & $Adapters.RemoveRunner $vm.RunnerId } catch { & $Adapters.Log 'warn' "${Name}: RemoveRunner: $_" } }
    try { & $Adapters.StopVmForce $Name } catch { & $Adapters.Log 'warn' "${Name}: StopVmForce: $_" }
    if ($vm.CachePath) { try { & $Adapters.DiscardCache $vm.CachePath } catch { & $Adapters.Log 'warn' "${Name}: DiscardCache: $_" } }
    try { & $Adapters.RemoveVm $Name $vm.Dir } catch { & $Adapters.Log 'warn' "${Name}: RemoveVm: $_" }
    if ($Retry -and $vm.JobId) {
        Register-JobRetry -Retries $Journal.Retries -JobId ([int64]$vm.JobId) -Now $Now -BackoffSeconds ([int]$Config.RetryBackoffSeconds)
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

function Complete-ActiveVm {
    # The VM reached Off by itself. Ask GitHub what ran, then commit or
    # discard. An API failure propagates: the entry stays in the journal
    # and the next tick tries again (never orphan, never commit blindly).
    # A commit that must wait for a same-lane sibling is remembered as
    # PendingCommit so later ticks skip the API lookups.
    [CmdletBinding()]
    param($Config, $Journal, [hashtable]$Adapters, [string]$Name)
    $vm = $Journal.Vms[$Name]
    if ($vm.PendingCommit) {
        $sibling = Test-SiblingRunning -Journal $Journal -Adapters $Adapters -Name $Name -Lane $vm.Lane
        if ($sibling) { return }
        $parentGeneration = [int](& $Adapters.GetGeneration $vm.Lane)
        if ($parentGeneration -eq [int]$vm.ForkGeneration) {
            $generation = & $Adapters.CommitCache $vm.Lane $vm.CachePath
            & $Adapters.Log 'info' "${Name}: commit $($vm.Lane) generation $generation (after waiting for a sibling)"
        } else {
            & $Adapters.DiscardCache $vm.CachePath
            & $Adapters.Log 'info' "${Name}: discard (generation moved while waiting: fork $($vm.ForkGeneration), parent $parentGeneration)"
        }
        & $Adapters.RemoveVm $Name $vm.Dir
        $Journal.Vms.Remove($Name)
        return
    }
    $candidates = @()
    foreach ($key in @($Journal.SeenJobs.Keys)) { $candidates += [int64]$key }
    $job = Resolve-RunnerJob -RunnerName $Name -AdmittedJobId ([int64]$vm.JobId) -CandidateJobIds $candidates -GetJob $Adapters.GetJob
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
    $state = & $Adapters.GetVmState $Name
    if ($state -eq 'Unknown') {
        & $Adapters.Log 'warn' "${Name}: Hyper-V did not answer; leaving the VM alone until the next tick"
        return
    }
    if ($state -eq 'Off') {
        Complete-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name
        return
    }
    if ($state -ne 'Running') {
        Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason "vm state $state" -Retry $true -Now $Now
        return
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
        Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $Name -Reason "expired after $maxMinutes min + grace" -Retry $false -Now $Now
        return
    }
    if (-not $vm.Claimed) {
        $handedAt = ConvertTo-DateTimeOffset -Value $vm.HandedAt
        $waited = ($Now - $handedAt).TotalSeconds
        $timeout = [int]$Config.UnclaimedTimeoutSeconds
        if ($waited -ge $timeout) {
            $runner = & $Adapters.GetRunner $vm.RunnerId
            if ($null -ne $runner -and $runner.busy) {
                $vm.Claimed = $true
                & $Adapters.Log 'info' "${Name}: claimed"
                return
            }
            $job = & $Adapters.GetJob ([int64]$vm.JobId)
            if ($null -ne $job -and $job.status -eq 'in_progress' -and [string]$job.runner_name -eq $Name) {
                $vm.Claimed = $true
                & $Adapters.Log 'info' "${Name}: claimed (job in progress)"
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

function Invoke-Admission {
    [CmdletBinding()]
    param($Config, $Journal, [hashtable]$Adapters, [datetimeoffset]$Now)
    $raw = @(& $Adapters.GetQueuedJobs)
    $queued = @(Select-QueuedLaneJobs -Jobs $raw -Lanes @($Config.Lanes.Keys))
    foreach ($q in $queued) {
        $key = [string]$q.JobId
        if (-not $Journal.SeenJobs.Contains($key)) {
            $Journal.SeenJobs[$key] = @{ RunId = [int64]$q.RunId; Lane = $q.Lane; FirstSeenAt = $Now.ToString('o') }
        }
    }
    foreach ($key in @($Journal.SeenJobs.Keys)) {
        $seen = ConvertTo-DateTimeOffset -Value $Journal.SeenJobs[$key].FirstSeenAt
        if ($Now -gt $seen.AddHours(24)) { $Journal.SeenJobs.Remove($key) }
    }
    $free = @(Get-FreeSlots -Config $Config -Journal $Journal)
    $admit = @(Select-JobsToAdmit -Candidates $queued -ActiveVms $Journal.Vms -FreeSlots $free.Count -Retries $Journal.Retries -Now $Now -RetryCap ([int]$Config.RetryCap))
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
            }
            & $Adapters.StartVm $name
            & $Adapters.Log 'info' "${name}: provisioned for job $($job.JobId) (lane $($job.Lane), slot $($slot.Index), fork generation $($created.ForkGeneration))"
        } catch {
            & $Adapters.Log 'error' "${name}: provisioning failed: $_"
            if ($Journal.Vms.Contains($name)) {
                Remove-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $name -Reason 'provisioning failed' -Retry $true -Now $Now
            } else {
                Register-JobRetry -Retries $Journal.Retries -JobId ([int64]$job.JobId) -Now $Now -BackoffSeconds ([int]$Config.RetryBackoffSeconds)
                try { & $Adapters.RemoveVm $name (Join-WinPath $Config.VmDir $name) } catch { & $Adapters.Log 'warn' "${name}: RemoveVm: $_" }
            }
        }
    }
}

function Invoke-OrchestratorTick {
    # One pass: advance or settle every active VM, then admit queued jobs.
    # A failure in one VM never blocks the others; a discovery failure
    # just means no admission this tick.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)]$Journal,
        [Parameter(Mandatory)][hashtable]$Adapters,
        [datetimeoffset]$Now = [datetimeoffset]::UtcNow
    )
    foreach ($name in @($Journal.Vms.Keys)) {
        try { Update-ActiveVm -Config $Config -Journal $Journal -Adapters $Adapters -Name $name -Now $Now }
        catch { & $Adapters.Log 'error' "${name}: $_" }
    }
    try { Invoke-Admission -Config $Config -Journal $Journal -Adapters $Adapters -Now $Now }
    catch { & $Adapters.Log 'error' "admission: $_" }
}

function Invoke-StartupSweep {
    # After a host restart nothing in the journal can be trusted: every
    # slate-win-* runner that is offline and every slate-win-* VM is
    # removed (cache children die with their VM directory; parents are
    # never touched) and the journal starts empty.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)]$Journal, [Parameter(Mandatory)][hashtable]$Adapters)
    $runners = @(& $Adapters.ListRunners)
    foreach ($name in @(Get-StaleRunnerNames -Runners $runners)) {
        $runner = $runners | Where-Object { $_.name -eq $name } | Select-Object -First 1
        try { & $Adapters.RemoveRunner $runner.id; & $Adapters.Log 'info' "sweep: removed stale runner $name" }
        catch { & $Adapters.Log 'warn' "sweep: runner ${name}: $_" }
    }
    foreach ($name in @(& $Adapters.ListVms)) {
        try {
            & $Adapters.StopVmForce $name
            & $Adapters.RemoveVm $name (Join-WinPath $Config.VmDir $name)
            & $Adapters.Log 'info' "sweep: removed vm $name"
        } catch { & $Adapters.Log 'warn' "sweep: vm ${name}: $_" }
    }
    try { & $Adapters.CleanVmDirs @() } catch { & $Adapters.Log 'warn' "sweep: CleanVmDirs: $_" }
    $Journal.Vms = @{}
    $Journal.Retries = @{}
    $Journal.SeenJobs = @{}
}

Export-ModuleMember -Function Get-LaneFromLabels, New-RunnerName, Split-KvpChunks, Join-KvpChunks,
    ConvertTo-DateTimeOffset, Select-QueuedLaneJobs, Select-JobsToAdmit, Register-JobRetry, Test-VmExpired, Get-StaleRunnerNames, Test-CommitEligible, Resolve-RunnerJob, Get-CiHostConfig, New-Journal, Read-Journal, Write-Journal, Write-CiLog,
    Get-FreeSlots, Remove-ActiveVm, Test-SiblingRunning, Complete-ActiveVm, Update-ActiveVm, Invoke-Admission, Invoke-OrchestratorTick, Invoke-StartupSweep
