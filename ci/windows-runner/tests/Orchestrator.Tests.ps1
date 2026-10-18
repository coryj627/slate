# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force

    function New-World {
        # Mutable fake of GitHub + Hyper-V; the adapters read it.
        $script:world = @{
            Queued     = @()
            InProgress = @()
            Jobs       = @{}
            Runs       = @{}
            VmStates   = @{}
            Heartbeat  = 'OK'
            Runner     = [pscustomobject]@{ status = 'online'; busy = $false }
            Runners    = @()
            Vms        = @()
            Generation = 3
            JitConfig  = ('j' * 2500)
            GuestError = $null
            Published  = [System.Collections.ArrayList]::new()
            Logs       = @('orchestrator-2026-09-01.log', 'orchestrator-2026-10-10.log')
            GoldenStamp = '2026-10-10T11:00:00Z'
        }
        $script:calls = [System.Collections.ArrayList]::new()
        $script:logs = [System.Collections.ArrayList]::new()
    }

    function New-FakeAdapters {
        # The module invokes these long after this function returned, so
        # everything they touch lives at $script: scope (never a local).
        $script:record = { param($name, $arguments) [void]$script:calls.Add(@{ Name = $name; Args = @($arguments) }) }
        $a = @{
            GetLaneJobs   = { & $script:record 'GetLaneJobs' @(); @($script:world.Queued) + @($script:world.InProgress) }
            GetJob        = { param($id) & $script:record 'GetJob' @($id); $script:world.Jobs[[string]$id] }
            GetRun        = { param($id) & $script:record 'GetRun' @($id)
                              if ($script:world.Runs.ContainsKey([string]$id)) { $script:world.Runs[[string]$id] }
                              else { [pscustomobject]@{ id = $id; event = 'push'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } } } }
            NewJitRunner  = { param($name, $labels) & $script:record 'NewJitRunner' @($name, $labels); @{ RunnerId = 77; EncodedJitConfig = $script:world.JitConfig } }
            RemoveRunner  = { param($id) & $script:record 'RemoveRunner' @($id) }
            GetRunner     = { param($id) & $script:record 'GetRunner' @($id); $script:world.Runner }
            ListRunners   = { & $script:record 'ListRunners' @(); @($script:world.Runners) }
            NewVm         = { param($name, $lane) & $script:record 'NewVm' @($name, $lane)
                              $cache = $null; if ($lane -ne 'shell') { $cache = "C:\slate-ci\vms\$name\cache.vhdx" }
                              @{ Dir = "C:\slate-ci\vms\$name"; CachePath = $cache; ForkGeneration = $script:world.Generation } }
            StartVm       = { param($name) & $script:record 'StartVm' @($name) }
            GetVmState    = { param($name) & $script:record 'GetVmState' @($name); if ($script:world.VmStates.ContainsKey($name)) { $script:world.VmStates[$name] } else { 'Running' } }
            GetHeartbeat  = { param($name) & $script:record 'GetHeartbeat' @($name); $script:world.Heartbeat }
            GetGuestError = { param($name) & $script:record 'GetGuestError' @($name); $script:world.GuestError }
            SendKvp       = { param($name, $items) & $script:record 'SendKvp' @($name, $items) }
            StopVmForce   = { param($name) & $script:record 'StopVmForce' @($name) }
            RemoveVm      = { param($name, $dir) & $script:record 'RemoveVm' @($name, $dir) }
            ListVms       = { & $script:record 'ListVms' @(); @($script:world.Vms) }
            CleanVmDirs   = { param($active) & $script:record 'CleanVmDirs' @($active) }
            GetGeneration = { param($lane) & $script:record 'GetGeneration' @($lane); $script:world.Generation }
            CommitCache   = { param($lane, $child) & $script:record 'CommitCache' @($lane, $child); $script:world.Generation + 1 }
            DiscardCache  = { param($child) & $script:record 'DiscardCache' @($child) }
            PublishState  = { param($json) & $script:record 'PublishState' @($json); [void]$script:world.Published.Add($json) }
            ListLogs      = { & $script:record 'ListLogs' @(); @($script:world.Logs) }
            RemoveLog     = { param($name) & $script:record 'RemoveLog' @($name); $script:world.Logs = @($script:world.Logs | Where-Object { $_ -ne $name }) }
            GetGoldenStamp = { & $script:record 'GetGoldenStamp' @(); $script:world.GoldenStamp }
            Log           ={ param($level, $message) [void]$script:logs.Add("[$level] $message") }
        }
        return $a
    }

    # The leading comma keeps the result an array: a lone match would
    # otherwise unroll to its call hashtable, whose .Count is its two keys.
    function Get-Calls([string]$Name) { , @($script:calls | Where-Object { $_.Name -eq $Name }) }

    function New-QueuedJob([int64]$Id, [int64]$RunId, [string]$Lane, [string]$Created = '2026-10-10T12:00:00Z') {
        [pscustomobject]@{ id = $Id; run_id = $RunId; status = 'queued'; labels = @('self-hosted', "slate-win-$Lane"); created_at = $Created }
    }

    function Set-DoneJob([int64]$Id, [int64]$RunId, [string]$RunnerName, [string]$Conclusion = 'success') {
        $script:world.Jobs[[string]$Id] = [pscustomobject]@{ id = $Id; run_id = $RunId; status = 'completed'; conclusion = $Conclusion; runner_name = $RunnerName }
    }

    function Set-Run([int64]$RunId, [string]$Event = 'push', [string]$Branch = 'main', [string]$Repo = 'coryj627/slate') {
        $script:world.Runs[[string]$RunId] = [pscustomobject]@{ id = $RunId; event = $Event; head_branch = $Branch; head_repository = [pscustomobject]@{ full_name = $Repo } }
    }

    $script:t0 = [datetimeoffset]::Parse('2026-10-10T12:00:00Z')
}

Describe 'orchestrator' {

BeforeEach {
    New-World
    $script:config = Get-CiHostConfig -Path (Join-Path $PSScriptRoot '..' 'config.json')
    $script:journal = New-Journal
    $script:adapters = New-FakeAdapters
}

Context 'admission' {
    It 'admits a queued app job into slot 1 and starts a VM' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'NewVm').Count | Should -Be 1
        (Get-Calls 'NewVm')[0].Args[1] | Should -Be 'app'
        (Get-Calls 'StartVm').Count | Should -Be 1
        $journal.Vms.Count | Should -Be 1
        $vm = @($journal.Vms.Values)[0]
        $vm.Phase | Should -Be 'provisioned'
        $vm.JobId | Should -Be 1
        $vm.Slot | Should -Be 1
        $vm.SlotIp | Should -Be '10.77.0.11'
        $vm.ForkGeneration | Should -Be 3
        $journal.SeenJobs['1'].Lane | Should -Be 'app'
        $journal.SeenJobs['1'].Foreign | Should -BeFalse
        $journal.SeenJobs['1'].Repo | Should -Be 'coryj627/slate'
        # The SeenJobs entry's shape (both sides sorted: Sort-Object ignores case).
        @($journal.SeenJobs['1'].Keys | Sort-Object) | Should -Be @('FirstSeenAt', 'Foreign', 'Lane', 'Repo', 'RunCheckedAt', 'RunId' | Sort-Object)
        # The journal entry's shape: 18 keys, every one present from admission.
        @($vm.Keys | Sort-Object) | Should -Be @('CachePath', 'ClaimCheckedAt', 'Claimed', 'Dir', 'ForkGeneration', 'GuestError', 'HandedAt', 'JobId', 'Lane', 'Name', 'PendingCommit', 'Phase', 'RunId', 'RunnerId', 'SettleWaits', 'Slot', 'SlotIp', 'StartedAt')
        $vm.GuestError | Should -Be $null
        $vm.SettleWaits | Should -Be 0
        $vm.ClaimCheckedAt | Should -Be $null
    }
    It 'drops retry entries a day after their back-off and keeps the rest' {
        # GitHub drops a job from its queue after 24 h, so an older entry can
        # only block a job id that no longer exists and grow the journal.
        $journal.Retries['7'] = @{ Count = 3; NextAt = $t0.AddHours(-25).ToString('o') }
        $journal.Retries['8'] = @{ Count = 1; NextAt = $t0.AddHours(-23).ToString('o') }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.Retries.ContainsKey('7') | Should -BeFalse
        $journal.Retries.ContainsKey('8') | Should -BeTrue
    }
    It 'never runs more VMs than slots' {
        $world.Queued = @((New-QueuedJob 1 10 'app' '2026-10-10T11:00:00Z'), (New-QueuedJob 2 10 'rust' '2026-10-10T11:00:01Z'), (New-QueuedJob 3 10 'model' '2026-10-10T11:00:02Z'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.Vms.Count | Should -Be 2
        @($journal.Vms.Values | ForEach-Object { $_.Slot } | Sort-Object) | Should -Be @(1, 2)
        @($journal.Vms.Values | ForEach-Object { $_.JobId } | Sort-Object) | Should -Be @(1, 2)
    }
    It 'does not admit a job at the retry cap' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $journal.Retries['1'] = @{ Count = 3; NextAt = $t0.AddDays(-1).ToString('o') }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.Vms.Count | Should -Be 0
    }
    It 'survives a GetLaneJobs failure' {
        $adapters.GetLaneJobs = { throw 'api down' }
        { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0 } | Should -Not -Throw
        ($logs -join "`n") | Should -Match 'admission: api down'
    }
    It 'registers a retry and cleans up when provisioning throws' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $adapters.NewVm = { throw 'disk full' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.Vms.Count | Should -Be 0
        $journal.Retries['1'].Count | Should -Be 1
        (Get-Calls 'RemoveVm').Count | Should -Be 1
    }
    It 'never admits a job whose run comes from another repository, says so once, and keeps it out of the published queue' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Set-Run 10 'pull_request' 'feature' 'someone/slate'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        (Get-Calls 'NewVm').Count | Should -Be 0
        (Get-Calls 'GetRun').Count | Should -Be 1
        $journal.SeenJobs['1'].Foreign | Should -BeTrue
        ([regex]::Matches(($logs -join "`n"), 'admission: skipped job 1 of someone/slate \(not the trusted repository\)')).Count | Should -Be 1
        @(($journal.PublishedState | ConvertFrom-Json).queued).Count | Should -Be 0
    }
    It 'treats a run without a head repository as foreign' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $world.Runs['10'] = [pscustomobject]@{ id = 10; event = 'pull_request'; head_branch = 'x'; head_repository = $null }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'NewVm').Count | Should -Be 0
        $journal.SeenJobs['1'].Foreign | Should -BeTrue
        $journal.SeenJobs['1'].Repo | Should -BeExactly '' -Because 'the repository is remembered as unknown, not as missing'
        @($logs) | Should -Contain '[warn] admission: skipped job 1 of an unknown repository (not the trusted repository)'
    }
    It 'treats a head repository without a name as foreign, and names an unknown repository' {
        $world.Queued = @((New-QueuedJob 1 10 'app'), (New-QueuedJob 2 11 'rust'))
        $world.Runs['10'] = [pscustomobject]@{ id = 10; event = 'pull_request'; head_branch = 'x'; head_repository = [pscustomobject]@{ id = 5 } }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.SeenJobs['1'].Foreign | Should -BeTrue
        @($journal.Vms.Values | ForEach-Object { $_.JobId }) | Should -Be @(2)
        @($logs) | Should -Contain '[warn] admission: skipped job 1 of an unknown repository (not the trusted repository)'
    }
    It 'retries a run read that failed at most once a minute, logging the first failure only, then admits' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $script:runCalls = 0
        $adapters.GetRun = { param($id) $script:runCalls++; if ($script:runCalls -le 2) { throw '502' }; [pscustomobject]@{ id = $id; event = 'push'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } } }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $script:runCalls | Should -Be 1
        (Get-Calls 'NewVm').Count | Should -Be 0
        ([regex]::Matches(($logs -join "`n"), 'admission: job 1: run 10 could not be read')).Count | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(61)
        $script:runCalls | Should -Be 2
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(122)
        $script:runCalls | Should -Be 3
        (Get-Calls 'NewVm').Count | Should -Be 1
    }
    It 'reads the run of a job whose RunCheckedAt does not parse as if it was never checked, without throwing out of admission' {
        # A hand-edited journal: the once-a-minute gate must not throw on it.
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $journal.SeenJobs['1'] = @{ RunId = [int64]10; Lane = 'app'; FirstSeenAt = $t0.ToString('o'); Foreign = $null; Repo = $null; RunCheckedAt = 'garbage' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        ($logs -join "`n") | Should -Not -Match 'admission:'
        (Get-Calls 'GetRun').Count | Should -Be 1
        $journal.SeenJobs['1'].Foreign | Should -BeFalse
        (ConvertTo-DateTimeOffset -Value $journal.SeenJobs['1'].RunCheckedAt) | Should -Be $t0
        (Get-Calls 'NewVm').Count | Should -Be 1
    }
}

Context 'handoff' {
    BeforeEach {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $world.Heartbeat = 'NoContact'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $script:name = @($journal.Vms.Keys)[0]
    }
    It 'hands off even when reading the guest error throws' {
        $world.Heartbeat = 'OK'
        $adapters.GetGuestError = { param($n) & $script:record 'GetGuestError' @($n); throw 'wmi unavailable' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        (Get-Calls 'GetGuestError').Count | Should -BeGreaterThan 0
        $journal.Vms[$name].Phase | Should -Be 'handed'
        $journal.Vms[$name].GuestError | Should -Be $null
    }
    It 'registers a JIT runner with the lane label and sends chunked KVP once the heartbeat is OK' {
        $world.Heartbeat = 'OK'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        $reg = (Get-Calls 'NewJitRunner')[0]
        $reg.Args[0] | Should -Be $name
        @($reg.Args[1]) | Should -Be @('slate-win-app')
        $kvp = (Get-Calls 'SendKvp')[0].Args[1]
        $kvp['slate.jit.count'] | Should -Be '3'
        $kvp['slate.jit.0'].Length | Should -Be 1000
        $kvp['slate.lane'] | Should -Be 'app'
        $kvp['slate.ip'] | Should -Be '10.77.0.11'
        $kvp['slate.gateway'] | Should -Be '10.77.0.1'
        $kvp['slate.prefix'] | Should -Be '24'
        $kvp['slate.dns'] | Should -Be '1.1.1.1,8.8.8.8'
        $kvp['slate.cache'] | Should -Be '1'
        $kvp['slate.job'] | Should -Be '1'
        $journal.Vms[$name].Phase | Should -Be 'handed'
        $journal.Vms[$name].RunnerId | Should -Be 77
        $journal.Vms[$name].HandedAt | Should -Not -BeNullOrEmpty
    }
    It 'discards the VM, deregisters the runner and retries the job when the KVP handoff fails' {
        $world.Heartbeat = 'OK'
        $adapters.SendKvp = { param($n, $items) & $script:record 'SendKvp' @($n, $items); throw 'AddKvpItems returned 32768' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'RemoveRunner').Count | Should -Be 1
        (Get-Calls 'RemoveRunner')[0].Args[0] | Should -Be 77
        (Get-Calls 'StopVmForce').Count | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        $journal.Retries['1'].Count | Should -Be 1
        ($logs -join "`n") | Should -Match 'handoff failed'
    }
    It 'discards and retries without a runner to deregister when registration itself fails' {
        $world.Heartbeat = 'OK'
        $adapters.NewJitRunner = { param($n, $l) throw '422' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'RemoveRunner').Count | Should -Be 0
        (Get-Calls 'SendKvp').Count | Should -Be 0
        $journal.Retries['1'].Count | Should -Be 1
    }
    It 'keeps waiting for a heartbeat inside the timeout' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(179)
        $journal.Vms[$name].Phase | Should -Be 'provisioned'
        (Get-Calls 'NewJitRunner').Count | Should -Be 0
    }
    It 'discards and retries when no heartbeat arrives within the timeout' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(180)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'StopVmForce').Count | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        (Get-Calls 'CommitCache').Count | Should -Be 0
        (Get-Calls 'RemoveVm').Count | Should -Be 1
        $journal.Retries['1'].Count | Should -Be 1
    }
    It 'keeps a VM whose teardown failed in the journal and finishes the teardown on a later tick' {
        $world.Heartbeat = 'NoContact'
        $adapters.RemoveVm = { param($n, $d) & $script:record 'RemoveVm' @($n, $d); if ((Get-Calls 'RemoveVm').Count -eq 1) { throw 'vmms timeout' } }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(180)
        $journal.Vms[$name].Phase | Should -Be 'discarding'
        $journal.Retries['1'].Count | Should -Be 1
        $journal.Vms.Count | Should -Be 1
        @(Get-FreeSlots -Config $config -Journal $journal).Count | Should -Be 1
        ($logs -join "`n") | Should -Match 'teardown failed, will retry: vmms timeout'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(190)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'RemoveVm').Count | Should -Be 2
        $journal.Retries['1'].Count | Should -Be 1
    }
    It 'drops a VM whose teardown failed once Hyper-V reports it missing, without another RemoveVm' {
        $world.Heartbeat = 'NoContact'
        $adapters.RemoveVm = { param($n, $d) & $script:record 'RemoveVm' @($n, $d); if ((Get-Calls 'RemoveVm').Count -eq 1) { throw 'vmms timeout' } }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(180)
        $journal.Vms[$name].Phase | Should -Be 'discarding'
        $world.VmStates[$name] = 'Missing'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(190)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'RemoveVm').Count | Should -Be 1
        $journal.Retries['1'].Count | Should -Be 1
    }
    It 'leaves a VM whose teardown failed alone while Hyper-V does not answer' {
        $world.Heartbeat = 'NoContact'
        $adapters.RemoveVm = { param($n, $d) & $script:record 'RemoveVm' @($n, $d); throw 'vmms timeout' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(180)
        $world.VmStates[$name] = 'Unknown'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(190)
        $journal.Vms[$name].Phase | Should -Be 'discarding'
        (Get-Calls 'RemoveVm').Count | Should -Be 1
        (Get-Calls 'StopVmForce').Count | Should -Be 1
    }
    It 'discards and retries when the VM has vanished' {
        $world.VmStates[$name] = 'Missing'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        $journal.Vms.Count | Should -Be 0
        $journal.Retries['1'].Count | Should -Be 1
    }
    It 'leaves the VM alone while Hyper-V does not answer' {
        $world.VmStates[$name] = 'Unknown'
        $world.Heartbeat = 'OK'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        $journal.Vms.Count | Should -Be 1
        $journal.Vms[$name].Phase | Should -Be 'provisioned'
        (Get-Calls 'NewJitRunner').Count | Should -Be 0
        (Get-Calls 'StopVmForce').Count | Should -Be 0
        ($logs -join "`n") | Should -Match 'did not answer'
    }
}

Context 'settle' {
    BeforeEach {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0                   # tick 1: provision
        $script:name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)   # tick 2: handoff (heartbeat OK)
        $journal.Vms[$name].Phase | Should -Be 'handed'
        $world.Queued = @()
        $world.VmStates[$name] = 'Off'
    }
    It 'commits the cache after a green push to main with an unchanged generation' {
        Set-DoneJob 1 10 $name
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'CommitCache').Count | Should -Be 1
        (Get-Calls 'CommitCache')[0].Args[0] | Should -Be 'app'
        (Get-Calls 'DiscardCache').Count | Should -Be 0
        (Get-Calls 'RemoveVm').Count | Should -Be 1
        $journal.Vms.Count | Should -Be 0
        $journal.SeenJobs.Count | Should -Be 0
        ($logs -join "`n") | Should -Match 'commit app generation 4'
    }
    It 'discards the cache for a pull_request run' {
        Set-DoneJob 1 10 $name
        Set-Run 10 'pull_request'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'CommitCache').Count | Should -Be 0
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        ($logs -join "`n") | Should -Match 'discard \(event: pull_request\)'
    }
    It 'discards when the parent generation moved' {
        Set-DoneJob 1 10 $name
        Set-Run 10
        $world.Generation = 4
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'CommitCache').Count | Should -Be 0
        ($logs -join "`n") | Should -Match 'generation moved'
    }
    It 'resolves the job through a candidate when another queued job took the runner' {
        # Job 1 ran elsewhere; job 2 (seen queued earlier) actually ran here.
        $journal.SeenJobs['2'] = @{ RunId = 11; Lane = 'app'; FirstSeenAt = $t0.ToString('o') }
        Set-DoneJob 1 10 'slate-win-app-elsewhere'
        Set-DoneJob 2 11 $name
        Set-Run 11
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'CommitCache').Count | Should -Be 1
        $journal.SeenJobs.ContainsKey('2') | Should -BeFalse
    }
    It 'looks up only seen jobs of the VM''s own lane when resolving which job ran on it' {
        # The runner carries its lane's label only, so no other lane's job
        # can have taken it; looking those up would spend API budget. Job 1
        # (admitted) and job 2 (app) ran elsewhere; job 5 is a rust job.
        $journal.SeenJobs['2'] = @{ RunId = 11; Lane = 'app'; FirstSeenAt = $t0.ToString('o') }
        $journal.SeenJobs['5'] = @{ RunId = 12; Lane = 'rust'; FirstSeenAt = $t0.ToString('o') }
        Set-DoneJob 1 10 'slate-win-app-elsewhere'
        Set-DoneJob 2 11 'slate-win-app-another'
        Set-DoneJob 5 12 'slate-win-rust-third'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        @((Get-Calls 'GetJob') | ForEach-Object { [int64]$_.Args[0] } | Sort-Object) | Should -Be @(1, 2)
        $journal.Vms.Count | Should -Be 0
        ($logs -join "`n") | Should -Match 'shut down without running a job'
    }
    It 'leaves the Off VM in the journal when the API fails during settle and settles on the next tick' {
        $adapters.GetJob = { param($id) $script:flakyCalls++; if ($script:flakyCalls -eq 1) { throw '502' }; $script:world.Jobs[[string]$id] }
        $script:flakyCalls = 0
        Set-DoneJob 1 10 $name
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        $journal.Vms.Count | Should -Be 1
        (Get-Calls 'CommitCache').Count | Should -Be 0
        (Get-Calls 'DiscardCache').Count | Should -Be 0
        (Get-Calls 'RemoveVm').Count | Should -Be 0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(21)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'CommitCache').Count | Should -Be 1
    }
    It 'waits while GitHub still reports the job in progress at Off, then commits once it reports completed' {
        $before = (Get-Calls 'GetRun').Count
        # GitHub's record can lag the guest's shutdown by a few seconds.
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'in_progress'; conclusion = $null; runner_name = $name }
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        $journal.Vms.ContainsKey($name) | Should -BeTrue
        $journal.Vms[$name].SettleWaits | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 0
        (Get-Calls 'CommitCache').Count | Should -Be 0
        (Get-Calls 'RemoveVm').Count | Should -Be 0
        (Get-Calls 'GetRun').Count | Should -Be $before
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20).AddSeconds(10)
        $journal.Vms[$name].SettleWaits | Should -Be 2
        Set-DoneJob 1 10 $name
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20).AddSeconds(20)
        (Get-Calls 'CommitCache').Count | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 0
        $journal.Vms.Count | Should -Be 0
        # Logged once, on the first wait.
        @($logs | Where-Object { $_.Contains("${name}: job 1 is in_progress on GitHub") }).Count | Should -Be 1
    }
    It 'stops waiting after six ticks and decides on what GitHub reports then' {
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'in_progress'; conclusion = $null; runner_name = $name }
        Set-Run 10
        for ($i = 0; $i -lt 6; $i++) {
            Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20).AddSeconds(10 * $i)
        }
        $journal.Vms[$name].SettleWaits | Should -Be 6
        (Get-Calls 'DiscardCache').Count | Should -Be 0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(21)
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        (Get-Calls 'CommitCache').Count | Should -Be 0
        ($logs -join "`n") | Should -Match "${name}: discard \(conclusion: \)"
        $journal.Vms.Count | Should -Be 0
    }
    It 'discards when the job failed' {
        Set-DoneJob 1 10 $name 'failure'
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        $journal.Retries.Count | Should -Be 0
    }
    It 'deregisters the runner, registers a retry and discards when the guest shut down without running a job' {
        # Bootstrap failure: the guest publishes slate.error while it still
        # runs (Hyper-V exposes guest KVP items only then) and the host keeps
        # it. Later the VM is Off, GetJob finds no record of job 1 running here
        # (it returns nothing) and job 1 is still queued.
        $world.VmStates[$name] = 'Running'
        $world.GuestError = 'no JIT config arrived within 300 s'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        $journal.Vms[$name].GuestError | Should -Be 'no JIT config arrived within 300 s'
        # The same report on a later tick is not logged again.
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        @($logs | Where-Object { $_.Contains("[warn] ${name}: guest reports: no JIT config arrived within 300 s") }).Count | Should -Be 1
        $readsWhileRunning = (Get-Calls 'GetGuestError').Count
        $world.GuestError = $null
        $world.VmStates[$name] = 'Off'
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'RemoveRunner')[0].Args[0] | Should -Be 77
        $journal.Retries['1'].Count | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        (Get-Calls 'CommitCache').Count | Should -Be 0
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'NewVm').Count | Should -Be 1
        # The stored report is logged; no fallback read is needed at Off.
        (Get-Calls 'GetGuestError').Count | Should -Be $readsWhileRunning
        @($logs | Where-Object { $_.Contains("${name}: shut down without running a job (guest bootstrap failure?) guest error: no JIT config arrived within 300 s") }).Count | Should -Be 1
    }
    It 'still tears down a jobless VM when its guest error cannot be read' {
        # Nothing was captured while it ran, so settle tries one last read.
        $world.VmStates[$name] = 'Off'
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $readsBefore = (Get-Calls 'GetGuestError').Count
        $adapters.GetGuestError = { param($n) & $script:record 'GetGuestError' @($n); throw 'wmi unavailable' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'GetGuestError').Count | Should -Be ($readsBefore + 1)
        $journal.Retries['1'].Count | Should -Be 1
        (Get-Calls 'RemoveVm').Count | Should -Be 1
        $journal.Vms.Count | Should -Be 0
        @($logs | Where-Object { $_.Contains("${name}: shut down without running a job (guest bootstrap failure?) guest error: ") }).Count | Should -Be 1
    }
    It 'waits to commit while a same-lane sibling is still running, then commits without more API calls' {
        # A second app job is admitted into slot 2 and keeps running.
        $world.Queued = @((New-QueuedJob 2 11 'app'))
        $world.VmStates[$name] = 'Running'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(1)
        $sibling = @($journal.Vms.Keys | Where-Object { $_ -ne $name })[0]
        $world.Queued = @()
        $world.VmStates[$name] = 'Off'
        Set-DoneJob 1 10 $name
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'CommitCache').Count | Should -Be 0
        $journal.Vms[$name].PendingCommit | Should -BeTrue
        ($logs -join "`n") | Should -Match "waits until sibling $sibling"
        $apiCallsBefore = (Get-Calls 'GetJob').Count + (Get-Calls 'GetRun').Count
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(21)
        (Get-Calls 'CommitCache').Count | Should -Be 0
        ((Get-Calls 'GetJob').Count + (Get-Calls 'GetRun').Count) | Should -Be $apiCallsBefore
        $world.VmStates[$sibling] = 'Off'
        Set-DoneJob 2 11 $sibling 'failure'
        Set-Run 11
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(22)
        (Get-Calls 'CommitCache').Count | Should -Be 1
        (Get-Calls 'CommitCache')[0].Args[1] | Should -Be "C:\slate-ci\vms\$name\cache.vhdx"
        $journal.Vms.Count | Should -Be 0
    }
    It 'discards a pending commit when the parent generation moved while waiting' {
        $world.Queued = @((New-QueuedJob 2 11 'app'))
        $world.VmStates[$name] = 'Running'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(1)
        $sibling = @($journal.Vms.Keys | Where-Object { $_ -ne $name })[0]
        $world.Queued = @()
        $world.VmStates[$name] = 'Off'
        Set-DoneJob 1 10 $name
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        $journal.Vms[$name].PendingCommit | Should -BeTrue
        $world.VmStates[$sibling] = 'Off'
        $world.Generation = 4
        Set-DoneJob 2 11 $sibling 'failure'
        Set-Run 11
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(22)
        (Get-Calls 'CommitCache').Count | Should -Be 0
        ($logs -join "`n") | Should -Match 'generation moved while waiting'
        $journal.Vms.ContainsKey($name) | Should -BeFalse
    }
    It 'never makes a discard wait for a sibling' {
        $world.Queued = @((New-QueuedJob 2 11 'app'))
        $world.VmStates[$name] = 'Running'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(1)
        $world.Queued = @()
        $world.VmStates[$name] = 'Off'
        Set-DoneJob 1 10 $name
        Set-Run 10 'pull_request'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        $journal.Vms.ContainsKey($name) | Should -BeFalse
    }
    It 'frees the slot of an Off VM waiting to commit, so a third job runs meanwhile' {
        # The waiting VM holds no CPU, memory or address; only its cache
        # child waits for the merge.
        $world.Queued = @((New-QueuedJob 2 11 'app'))
        $world.VmStates[$name] = 'Running'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(1)
        $sibling = @($journal.Vms.Keys | Where-Object { $_ -ne $name })[0]
        $world.Queued = @()
        $world.VmStates[$name] = 'Off'
        Set-DoneJob 1 10 $name
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        $journal.Vms[$name].PendingCommit | Should -BeTrue
        @(Get-FreeSlots -Config $config -Journal $journal).Count | Should -Be 1
        $world.Queued = @((New-QueuedJob 3 12 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(21)
        $journal.Vms.Count | Should -Be 3
        $third = @($journal.Vms.Keys | Where-Object { $_ -ne $name -and $_ -ne $sibling })[0]
        $journal.Vms[$third].Slot | Should -Be $journal.Vms[$name].Slot
        # The pending commit still waits: the new VM is a same-lane sibling.
        (Get-Calls 'CommitCache').Count | Should -Be 0
        $journal.Vms[$name].PendingCommit | Should -BeTrue
    }
}

Context 'handoff reconciliation' {
    It 'frees the provisioned job for a VM of its own once the listing shows its runner on another job' {
        # GitHub hands a runner whichever queued job matches its label first.
        # Two model shards queued together: the first VM is provisioned for
        # job 1 and GitHub gives it job 2; job 1 must not wait for that VM.
        $world.Queued = @((New-QueuedJob 3 10 'shell' '2026-10-10T10:59:00Z'), (New-QueuedJob 1 10 'model' '2026-10-10T11:00:00Z'), (New-QueuedJob 2 10 'model' '2026-10-10T11:00:01Z'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0                    # shell -> slot 1, model job 1 -> slot 2
        $journal.Vms.Count | Should -Be 2
        $modelVm = @($journal.Vms.Values | Where-Object { $_.Lane -eq 'model' })[0]
        $modelVm.JobId | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)    # handoff
        $modelVm.Phase | Should -Be 'handed'
        # GitHub gave the model VM job 2; job 1 is still queued.
        $world.Queued = @((New-QueuedJob 1 10 'model' '2026-10-10T11:00:00Z'))
        $world.InProgress = @([pscustomobject]@{ id = 2; run_id = 10; status = 'in_progress'; labels = @('slate-win-model'); created_at = '2026-10-10T11:00:01Z'; runner_name = $modelVm.Name })
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        $modelVm.JobId | Should -Be 2
        $modelVm.RunId | Should -Be 10
        $modelVm.Claimed | Should -BeTrue
        $journal.SeenJobs.ContainsKey('2') | Should -BeTrue
        ($logs -join "`n") | Should -Match "$($modelVm.Name): runs job 2, not job 1 it was provisioned for"
        $journal.Vms.Count | Should -Be 2
        (Get-Calls 'GetRunner').Count | Should -Be 0
        # The shell VM finishes: its slot goes to job 1 at once.
        $shellVm = @($journal.Vms.Values | Where-Object { $_.Lane -eq 'shell' })[0]
        $world.VmStates[$shellVm.Name] = 'Off'
        Set-DoneJob 3 10 $shellVm.Name
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        $journal.Vms.Count | Should -Be 2
        $third = @($journal.Vms.Values | Where-Object { $_.Name -ne $modelVm.Name })[0]
        $third.Lane | Should -Be 'model'
        $third.JobId | Should -Be 1
        $third.Slot | Should -Be $shellVm.Slot
        (Get-Calls 'NewVm').Count | Should -Be 3
    }
    It 'claims a handed VM from the listing without the runner lookup, and leaves its job alone when they agree' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $world.Queued = @()
        $world.InProgress = @([pscustomobject]@{ id = 1; run_id = 10; status = 'in_progress'; labels = @('slate-win-app'); created_at = '2026-10-10T12:00:00Z'; runner_name = $name })
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        $journal.Vms[$name].Claimed | Should -BeTrue
        $journal.Vms[$name].JobId | Should -Be 1
        ($logs -join "`n") | Should -Match "${name}: claimed \(job 1 in progress\)"
        ($logs -join "`n") | Should -Not -Match 'runs job'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        (Get-Calls 'GetRunner').Count | Should -Be 0
    }
    It 'forces a handed VM off when the job that took its runner comes from another repository, and leaves its own job queued' {
        $world.Queued = @((New-QueuedJob 1 10 'model'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $journal.Vms[$name].Phase | Should -Be 'handed'
        Set-Run 99 'pull_request' 'feature' 'someone/slate'
        $world.InProgress = @([pscustomobject]@{ id = 2; run_id = 99; status = 'in_progress'; labels = @('slate-win-model'); created_at = $t0.ToString('o'); runner_name = $name })
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        (Get-Calls 'StopVmForce').Count | Should -Be 1
        (Get-Calls 'RemoveRunner').Count | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        (Get-Calls 'CommitCache').Count | Should -Be 0
        $journal.Vms.ContainsKey($name) | Should -BeFalse
        $journal.SeenJobs.ContainsKey('1') | Should -BeTrue
        $journal.Retries.ContainsKey('1') | Should -BeFalse
        $journal.SeenJobs['2'].Foreign | Should -BeTrue
        @($logs) | Should -Contain "[warn] handoff: ${name} runs job 2 of someone/slate (not the trusted repository); forcing it off"
        @($logs) | Should -Contain "[warn] ${name}: discard (runner taken by job 2 of someone/slate (not the trusted repository))"
        # The job it was provisioned for gets a VM of its own in the same tick.
        (Get-Calls 'NewVm').Count | Should -Be 2
        @($journal.Vms.Values)[0].JobId | Should -Be 1
    }
    It 'forces a handed VM off when its runner runs a job without a lane label, which the lane listing never shows' {
        # A JIT runner carries only its lane label today, so this is defence
        # in depth: the reconciliation reads the whole listing, not the queue.
        $world.Queued = @((New-QueuedJob 1 10 'model'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        Set-Run 99 'pull_request' 'feature' 'someone/slate'
        $world.InProgress = @([pscustomobject]@{ id = 2; run_id = 99; status = 'in_progress'; labels = @('self-hosted'); created_at = $t0.ToString('o'); runner_name = $name })
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        (Get-Calls 'StopVmForce').Count | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        (Get-Calls 'CommitCache').Count | Should -Be 0
        $journal.Vms.ContainsKey($name) | Should -BeFalse
        $journal.Retries.ContainsKey('1') | Should -BeFalse
        $journal.SeenJobs.ContainsKey('2') | Should -BeFalse -Because 'it is no lane job, so it never becomes a settle candidate'
        @($logs) | Should -Contain "[warn] handoff: ${name} runs job 2 of someone/slate (not the trusted repository); forcing it off"
        @($logs) | Should -Contain "[warn] ${name}: discard (runner taken by job 2 of someone/slate (not the trusted repository))"
        (Get-Calls 'NewVm').Count | Should -Be 2
    }
    It 'forces a handed VM off when its runner runs a job of the trusted repository with <Case>, and says so' -ForEach @(
        @{ Case = 'no lane label'; Labels = @('self-hosted') }
        @{ Case = 'two lane labels'; Labels = @('slate-win-model', 'slate-win-rust') }
    ) {
        # The log says what is true: the run is the trusted repository's, so
        # the job goes for its labels, not its repository.
        $world.Queued = @((New-QueuedJob 1 10 'model'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        Set-Run 98
        $world.InProgress = @([pscustomobject]@{ id = 2; run_id = 98; status = 'in_progress'; labels = $Labels; created_at = $t0.ToString('o'); runner_name = $name })
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        (Get-Calls 'StopVmForce').Count | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        (Get-Calls 'CommitCache').Count | Should -Be 0
        $journal.Vms.ContainsKey($name) | Should -BeFalse
        $journal.Retries.ContainsKey('1') | Should -BeFalse
        @($logs) | Should -Contain "[warn] handoff: ${name} runs job 2 of coryj627/slate (no lane label); forcing it off"
        @($logs) | Should -Contain "[warn] ${name}: discard (runner taken by job 2 of coryj627/slate (no lane label))"
        ($logs -join "`n") | Should -Not -Match 'not the trusted repository'
        (Get-Calls 'NewVm').Count | Should -Be 2
    }
    It 'forces a VM off at once for a job without a lane label even when its run cannot be read, naming an unknown repository' {
        # Only the missing label is known, so that is what the log says.
        $world.Queued = @((New-QueuedJob 1 10 'model'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $adapters.GetRun = { param($id) & $script:record 'GetRun' @($id); throw '502' }
        $world.InProgress = @([pscustomobject]@{ id = 2; run_id = 99; status = 'in_progress'; labels = @('self-hosted', 'Windows'); created_at = $t0.ToString('o'); runner_name = $name })
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        (Get-Calls 'CommitCache').Count | Should -Be 0
        $journal.Vms.ContainsKey($name) | Should -BeFalse
        $journal.Retries.ContainsKey('1') | Should -BeFalse
        @($logs) | Should -Contain "[warn] handoff: ${name} runs job 2 of an unknown repository (no lane label); forcing it off"
        @($logs) | Should -Contain "[warn] ${name}: discard (runner taken by job 2 of an unknown repository (no lane label))"
        (Get-Calls 'NewVm').Count | Should -Be 2
    }
    It 'decides from the answer admission remembered, without reading the run again' {
        # Job 2 was seen queued (no slot was free for it), so its run was read then.
        $world.Queued = @((New-QueuedJob 3 12 'shell' '2026-10-10T10:59:00Z'), (New-QueuedJob 1 10 'model' '2026-10-10T11:00:00Z'), (New-QueuedJob 2 11 'model' '2026-10-10T11:00:01Z'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.SeenJobs['2'].Foreign | Should -BeFalse
        $modelVm = @($journal.Vms.Values | Where-Object { $_.Lane -eq 'model' })[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $world.Queued = @((New-QueuedJob 1 10 'model' '2026-10-10T11:00:00Z'))
        $world.InProgress = @([pscustomobject]@{ id = 2; run_id = 11; status = 'in_progress'; labels = @('slate-win-model'); created_at = '2026-10-10T11:00:01Z'; runner_name = $modelVm.Name })
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        $modelVm.JobId | Should -Be 2
        @((Get-Calls 'GetRun') | Where-Object { [int64]$_.Args[0] -eq 11 }).Count | Should -Be 1
    }
    It 'forces a VM off on the answer admission remembered, naming the repository it remembered, without reading the run again' {
        # Admission refused job 2 (a fork's run); GitHub then gave it this
        # VM's runner, and by now the API cannot read the run.
        $world.Queued = @((New-QueuedJob 3 12 'shell' '2026-10-10T10:59:00Z'), (New-QueuedJob 1 10 'model' '2026-10-10T11:00:00Z'), (New-QueuedJob 2 99 'model' '2026-10-10T11:00:01Z'))
        Set-Run 99 'pull_request' 'feature' 'someone/slate'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.SeenJobs['2'].Foreign | Should -BeTrue
        $journal.SeenJobs['2'].Repo | Should -Be 'someone/slate'
        $modelVm = @($journal.Vms.Values | Where-Object { $_.Lane -eq 'model' })[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $adapters.GetRun = { param($id) & $script:record 'GetRun' @($id); throw '502' }
        $world.Queued = @((New-QueuedJob 1 10 'model' '2026-10-10T11:00:00Z'))
        $world.InProgress = @([pscustomobject]@{ id = 2; run_id = 99; status = 'in_progress'; labels = @('slate-win-model'); created_at = '2026-10-10T11:00:01Z'; runner_name = $modelVm.Name })
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        $journal.Vms.ContainsKey($modelVm.Name) | Should -BeFalse
        $journal.Retries.ContainsKey('1') | Should -BeFalse
        @((Get-Calls 'GetRun') | Where-Object { [int64]$_.Args[0] -eq 99 }).Count | Should -Be 1 -Because 'admission read the run once; the eviction uses what it remembered'
        @($logs) | Should -Contain "[warn] handoff: $($modelVm.Name) runs job 2 of someone/slate (not the trusted repository); forcing it off"
        @($logs) | Should -Contain "[warn] $($modelVm.Name): discard (runner taken by job 2 of someone/slate (not the trusted repository))"
    }
    It 'leaves a VM alone while the run of the job on it cannot be read, says so once a minute, and forces it off once the run reads foreign' {
        $world.Queued = @((New-QueuedJob 1 10 'model'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        Set-Run 99 'pull_request' 'feature' 'someone/slate'
        $script:runReadable = $false
        $adapters.GetRun = { param($id) & $script:record 'GetRun' @($id); if (-not $script:runReadable) { throw '502' }; $script:world.Runs[[string]$id] }
        $world.InProgress = @([pscustomobject]@{ id = 2; run_id = 99; status = 'in_progress'; labels = @('slate-win-model'); created_at = $t0.ToString('o'); runner_name = $name })
        $failed = "handoff: ${name}: run 99 of job 2 could not be read"
        foreach ($s in 20, 30, 50) { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds($s) }
        @((Get-Calls 'GetRun') | Where-Object { [int64]$_.Args[0] -eq 99 }).Count | Should -Be 1
        @($logs | Where-Object { $_.Contains($failed) }).Count | Should -Be 1
        $journal.Vms.ContainsKey($name) | Should -BeTrue
        $journal.Vms[$name].Claimed | Should -BeFalse
        (Get-Calls 'StopVmForce').Count | Should -Be 0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(80)
        @((Get-Calls 'GetRun') | Where-Object { [int64]$_.Args[0] -eq 99 }).Count | Should -Be 2
        @($logs | Where-Object { $_.Contains($failed) }).Count | Should -Be 2
        $script:runReadable = $true
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(140)
        $journal.Vms.ContainsKey($name) | Should -BeFalse
        $journal.Retries.ContainsKey('1') | Should -BeFalse
        @($logs) | Should -Contain "[warn] handoff: ${name} runs job 2 of someone/slate (not the trusted repository); forcing it off"
    }
}

Context 'shell lane' {
    It 'has no cache: neither commit nor discard on completion' {
        $world.Queued = @((New-QueuedJob 5 50 'shell'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0                   # provision
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)   # handoff
        (Get-Calls 'SendKvp')[0].Args[1]['slate.cache'] | Should -Be '0'
        $world.Queued = @()
        $world.VmStates[$name] = 'Off'
        Set-DoneJob 5 50 $name
        Set-Run 50
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(5)
        (Get-Calls 'CommitCache').Count | Should -Be 0
        (Get-Calls 'DiscardCache').Count | Should -Be 0
        (Get-Calls 'GetGeneration').Count | Should -Be 0
        $journal.Vms.Count | Should -Be 0
    }
}

Context 'handed-phase guards' {
    BeforeEach {
        $world.Queued = @((New-QueuedJob 1 10 'model'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0                   # provision
        $script:name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)   # handoff at t0+10s
        $journal.Vms[$name].Phase | Should -Be 'handed'
        $world.Queued = @()
    }
    It 'tears down an expired VM without committing and without a retry' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(110)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'StopVmForce').Count | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        (Get-Calls 'RemoveRunner').Count | Should -Be 1
        $journal.Retries.Count | Should -Be 0
        ($logs -join "`n") | Should -Match '\[error\].*expired'
    }
    # HandedAt is t0+10s, so the 300 s unclaimed check first fires at t0+310s.
    It 'does not check for a claim before the unclaimed timeout' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(200)
        (Get-Calls 'GetRunner').Count | Should -Be 0
        $journal.Vms[$name].Claimed | Should -BeFalse
    }
    It 'marks the runner claimed when GitHub reports it busy' {
        $world.Runner = [pscustomobject]@{ status = 'online'; busy = $true }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        $journal.Vms[$name].Claimed | Should -BeTrue
        (Get-Calls 'StopVmForce').Count | Should -Be 0
    }
    It 'registers no retry when a VM whose runner was seen busy shuts down without a job the host can resolve' {
        # The runner ran something, so the image works: a job the host never
        # saw must not strike the job this VM was provisioned for.
        $world.Runner = [pscustomobject]@{ status = 'online'; busy = $true }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        $journal.Vms[$name].Claimed | Should -BeTrue
        $world.VmStates[$name] = 'Off'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        $journal.Vms.Count | Should -Be 0
        $journal.Retries.Count | Should -Be 0
        (Get-Calls 'RemoveRunner').Count | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 1
        (Get-Calls 'CommitCache').Count | Should -Be 0
        @($logs) | Should -Contain "[warn] ${name}: shut down without a job the host can resolve, although its runner was seen busy; no retry"
        ($logs -join "`n") | Should -Not -Match 'guest bootstrap failure'
    }
    It 'tears down an unclaimed runner whose job vanished' {
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'completed'; conclusion = 'cancelled'; runner_name = $null }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        $journal.Vms.Count | Should -Be 0
        (Get-Calls 'RemoveRunner').Count | Should -Be 1
        $journal.Retries.Count | Should -Be 0
    }
    It 'treats an in-progress job on this runner as claimed' {
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'in_progress'; conclusion = $null; runner_name = $name }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        $journal.Vms[$name].Claimed | Should -BeTrue
    }
    It 'does not claim a job whose runner name differs only in case (ordinal, as the commit predicate compares)' {
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'in_progress'; conclusion = $null; runner_name = $name.ToUpperInvariant() }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        # Another runner took the job, so this VM is unclaimed with its job no longer queued: torn down, no retry.
        $journal.Vms.ContainsKey($name) | Should -BeFalse
        (Get-Calls 'RemoveRunner').Count | Should -Be 1
        $journal.Retries.Count | Should -Be 0
    }
    It 'treats a job that already completed on this runner as claimed and waits for Off (no teardown, no lost commit)' {
        $world.Runner = $null
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'completed'; conclusion = 'success'; runner_name = $name }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        $journal.Vms[$name].Claimed | Should -BeTrue
        (Get-Calls 'StopVmForce').Count | Should -Be 0
        $journal.Vms.Count | Should -Be 1
    }
    It 'keeps waiting while the job stays queued, then retries after three timeouts' {
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'queued'; conclusion = $null; runner_name = $null }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        $journal.Vms.Count | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(920)
        $journal.Vms.Count | Should -Be 0
        $journal.Retries['1'].Count | Should -Be 1
    }
    It 'checks for a claim at most once a minute' {
        # Nothing changes between ticks, and each check is two API calls.
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'queued'; conclusion = $null; runner_name = $null }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        (Get-Calls 'GetRunner').Count | Should -Be 1
        (Get-Calls 'GetJob').Count | Should -Be 1
        $journal.Vms[$name].ClaimCheckedAt | Should -Not -BeNullOrEmpty
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(330)
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(370)
        (Get-Calls 'GetRunner').Count | Should -Be 1
        (Get-Calls 'GetJob').Count | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(381)
        (Get-Calls 'GetRunner').Count | Should -Be 2
        $journal.Vms.Count | Should -Be 1
    }
    It 'checks for a claim when ClaimCheckedAt does not parse, as if it was never checked' {
        # A hand-edited journal: the gate must not throw out of the VM's update.
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'queued'; conclusion = $null; runner_name = $null }
        $journal.Vms[$name]['ClaimCheckedAt'] = 'garbage'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        ($logs -join "`n") | Should -Not -Match '\[error\]'
        (Get-Calls 'GetRunner').Count | Should -Be 1
        (ConvertTo-DateTimeOffset -Value $journal.Vms[$name].ClaimCheckedAt) | Should -Be $t0.AddSeconds(320)
    }
    It 'stops reading the guest error once the runner is claimed' {
        # The bootstrap is behind a claimed runner; the WMI read each tick
        # would run for the whole job for nothing.
        $world.Runner = [pscustomobject]@{ status = 'online'; busy = $true }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(320)
        $journal.Vms[$name].Claimed | Should -BeTrue
        $reads = (Get-Calls 'GetGuestError').Count
        $reads | Should -BeGreaterThan 0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(330)
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(340)
        (Get-Calls 'GetGuestError').Count | Should -Be $reads
    }
}

Context 'publishing the state' {
    It 'publishes after the first tick, again only when the state changed, and at least once a minute' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'PublishState').Count | Should -Be 1
        $journal.PublishedState | Should -Be ([string]$world.Published[0])
        ($journal.PublishedState | ConvertFrom-Json).slots | Should -Be 2
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        (Get-Calls 'PublishState').Count | Should -Be 1
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        (Get-Calls 'PublishState').Count | Should -Be 2
        $state = $journal.PublishedState | ConvertFrom-Json
        @($state.running).Count | Should -Be 1
        $state.running[0].lane | Should -Be 'app'
        $world.Queued = @()
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        (Get-Calls 'PublishState').Count | Should -Be 2
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(81)
        (Get-Calls 'PublishState').Count | Should -Be 3 -Because 'the heartbeat refreshes after 60 s even when nothing changed'
    }
    It 'publishes the queue it would admit with unlimited slots, not the jobs it skipped' {
        $world.Queued = @((New-QueuedJob 1 10 'app'), (New-QueuedJob 2 10 'rust'), (New-QueuedJob 3 10 'model'), (New-QueuedJob 4 10 'app'))
        $journal.Retries['4'] = @{ Count = 3; NextAt = $t0.AddDays(-1).ToString('o') }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $state = $journal.PublishedState | ConvertFrom-Json
        @($state.running).Count | Should -Be 2
        @($state.queued | ForEach-Object { $_.lane }) | Should -Be @('model')
    }
    It 'logs a failed publish once a minute and keeps ticking' {
        $adapters.PublishState = { param($json) throw '403' }
        { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0 } | Should -Not -Throw
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        ([regex]::Matches(($logs -join "`n"), 'publish: 403')).Count | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(70)
        ([regex]::Matches(($logs -join "`n"), 'publish: 403')).Count | Should -Be 2
        $journal.PublishedState | Should -Be $null
    }
    It 'after a failed publish, tries again only once the failure is a minute old, even for a changed state, then publishes' {
        $script:publishFails = $true
        $adapters.PublishState = { param($json) & $script:record 'PublishState' @($json); if ($script:publishFails) { throw '403' }; [void]$script:world.Published.Add($json) }
        $attempts = @()
        for ($s = 0; $s -le 130; $s += 10) {
            # The state changes at t0+70 (a job is admitted): no reason to try sooner.
            if ($s -eq 70) { $world.Queued = @((New-QueuedJob 1 10 'app')) }
            $before = (Get-Calls 'PublishState').Count
            Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds($s)
            if ((Get-Calls 'PublishState').Count -gt $before) { $attempts += $s }
        }
        $attempts | Should -Be @(0, 60, 120)
        ([regex]::Matches(($logs -join "`n"), 'publish: 403')).Count | Should -Be 3
        $journal.PublishedState | Should -Be $null
        $script:publishFails = $false
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(180)
        (Get-Calls 'PublishState').Count | Should -Be 4
        $journal.PublishedState | Should -Be ([string]$world.Published[0])
        @(($journal.PublishedState | ConvertFrom-Json).running).Count | Should -Be 1
        $journal.PublishFailedAt | Should -Be $null
    }
    It 'Publish-HomeState takes no -Config (it never read one)' {
        (Get-Command Publish-HomeState).Parameters.ContainsKey('Config') | Should -BeFalse
    }
    It 'counts a PublishFailedAt that does not parse as never, so a failed publish cannot throw out of the tick' {
        # A hand-edited journal: the failure gate runs inside the tick's last
        # catch, so reading it must never throw.
        $journal.PublishFailedAt = 'garbage'
        $adapters.PublishState = { param($json) & $script:record 'PublishState' @($json); throw '403' }
        { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0 } | Should -Not -Throw
        { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10) } | Should -Not -Throw
        (Get-Calls 'PublishState').Count | Should -Be 1 -Because 'the garbage counts as no failure, and the failure at t0 holds the next attempt for a minute'
        ([regex]::Matches(($logs -join "`n"), 'publish: 403')).Count | Should -Be 1
        ($logs -join "`n") | Should -Not -Match 'not recognized as a valid DateTime'
        (ConvertTo-DateTimeOffset -Value $journal.PublishFailedAt) | Should -Be $t0
    }
    It 'counts a PublishedAt that does not parse as never published, so the next tick publishes and overwrites it' {
        # A hand-edited journal: without the guard every publish attempt
        # threw on the read, and the host stayed stale to the router.
        $journal.PublishedAt = 'garbage'
        { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0 } | Should -Not -Throw
        (Get-Calls 'PublishState').Count | Should -Be 1
        ($logs -join "`n") | Should -Not -Match 'publish:'
        (ConvertTo-DateTimeOffset -Value $journal.PublishedAt) | Should -Be $t0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        (Get-Calls 'PublishState').Count | Should -Be 1 -Because 'the overwritten PublishedAt is a fresh heartbeat again'
    }
    It 'publishes nothing on a tick whose admission threw, so the state ages into stale, and publishes again on the next good tick' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'PublishState').Count | Should -Be 1
        $listing = $adapters.GetLaneJobs
        $adapters.GetLaneJobs = { & $script:record 'GetLaneJobs' @(); throw 'api down' }
        for ($s = 10; $s -le 130; $s += 10) {
            Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds($s)
        }
        (Get-Calls 'GetLaneJobs').Count | Should -Be 14
        (Get-Calls 'PublishState').Count | Should -Be 1 -Because 'a host that cannot list or admit must look dead to the router, not idle'
        ([regex]::Matches(($logs -join "`n"), '\[error\] admission: api down')).Count | Should -Be 13
        $adapters.GetLaneJobs = $listing
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(140)
        (Get-Calls 'PublishState').Count | Should -Be 2
        $journal.PublishedState | Should -Be '{"v":1,"at":"2026-10-10T12:02:20Z","serving":true,"slots":2,"running":[],"queued":[]}'
    }
    It 'logs a state that cannot be built once a minute, as it logs a failed write' {
        # A hand-edited journal: the VM's provisioning time does not parse.
        $journal.Vms['slate-win-app-broken00'] = @{
            Name = 'slate-win-app-broken00'; Lane = 'app'; Slot = 1; SlotIp = '10.77.0.11'; JobId = [int64]1; RunId = [int64]10
            Dir = 'C:\slate-ci\vms\slate-win-app-broken00'; CachePath = $null; ForkGeneration = 3
            Phase = 'handed'; StartedAt = 'not a time'; HandedAt = $t0.ToString('o'); RunnerId = 77; Claimed = $true; PendingCommit = $false
            GuestError = $null; SettleWaits = 0; ClaimCheckedAt = $null
        }
        { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0 } | Should -Not -Throw
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        ([regex]::Matches(($logs -join "`n"), '\[error\] publish: ')).Count | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(70)
        ([regex]::Matches(($logs -join "`n"), '\[error\] publish: ')).Count | Should -Be 2
        (Get-Calls 'PublishState').Count | Should -Be 0
    }
    It 'counts a job whose run is still unknown in the published queue, without admitting it' {
        # The conservative direction: the router must not see a free slot
        # that the host will hand to this job once its run is read.
        $world.Queued = @((New-QueuedJob 1 10 'app' '2026-10-10T11:58:00Z'))
        $adapters.GetRun = { param($id) & $script:record 'GetRun' @($id); throw '502' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'NewVm').Count | Should -Be 0
        $journal.SeenJobs['1'].Foreign | Should -Be $null
        $journal.PublishedState | Should -Be '{"v":1,"at":"2026-10-10T12:00:00Z","serving":true,"slots":2,"running":[],"queued":[{"lane":"app","since":"2026-10-10T11:58:00Z"}]}'
        # The next tick does not read the run again, and the job still counts:
        # the state is unchanged, so nothing is written.
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        (Get-Calls 'GetRun').Count | Should -Be 1
        (Get-Calls 'NewVm').Count | Should -Be 0
        (Get-Calls 'PublishState').Count | Should -Be 1
    }
    It 'never publishes stray adapter output as a queued job' {
        # Admission returns the publishable queue as its output, so anything
        # an adapter writes to the pipeline inside it would join that queue.
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        $adapters.StartVm = { param($n) & $script:record 'StartVm' @($n); throw 'start failed' }
        $adapters.StopVmForce = { param($n) & $script:record 'StopVmForce' @($n); 'stopped' }
        $adapters.DiscardCache = { param($c) & $script:record 'DiscardCache' @($c); 'discarded' }
        $adapters.RemoveVm = { param($n, $d) & $script:record 'RemoveVm' @($n, $d); 'removed' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.Vms.Count | Should -Be 0
        $journal.Retries['1'].Count | Should -Be 1
        (Get-Calls 'PublishState').Count | Should -Be 1
        $journal.PublishedState | Should -Be '{"v":1,"at":"2026-10-10T12:00:00Z","serving":true,"slots":2,"running":[],"queued":[]}'
        # A provisioning that succeeds, with a chatty StartVm.
        $adapters.StartVm = { param($n) & $script:record 'StartVm' @($n); 'started' }
        $world.Queued = @((New-QueuedJob 1 10 'app'), (New-QueuedJob 2 11 'rust'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $journal.Vms.Count | Should -Be 1
        (Get-Calls 'PublishState').Count | Should -Be 2
        $journal.PublishedState | Should -Be '{"v":1,"at":"2026-10-10T12:00:10Z","serving":true,"slots":2,"running":[{"lane":"rust","since":"2026-10-10T12:00:10Z"}],"queued":[]}'
        ($logs -join "`n") | Should -Not -Match 'publish:'
    }
}

Context 'log retention' {
    It 'prunes once per local day, at the first tick, and remembers the day in the journal' {
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        (Get-Calls 'RemoveLog').Count | Should -Be 1
        (Get-Calls 'RemoveLog')[0].Args[0] | Should -Be 'orchestrator-2026-09-01.log'
        $journal.LogsPrunedOn | Should -Be $t0.ToLocalTime().ToString('yyyy-MM-dd', [cultureinfo]::InvariantCulture)
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(5)
        (Get-Calls 'ListLogs').Count | Should -Be 1
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddDays(1)
        (Get-Calls 'ListLogs').Count | Should -Be 2
    }
    It 'logs and continues when a delete fails' {
        $adapters.RemoveLog = { param($name) throw 'in use' }
        { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0 } | Should -Not -Throw
        ($logs -join "`n") | Should -Match 'logs: orchestrator-2026-09-01.log: in use'
        $journal.LogsPrunedOn | Should -Not -Be $null
    }
    It 'the startup sweep forgets the day so the first tick prunes again' {
        $journal.LogsPrunedOn = '2026-10-10'
        Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
        $journal.LogsPrunedOn | Should -Be $null
    }
}

Context 'golden acceptance' {
    BeforeAll {
        function Complete-GreenJob([string]$Lane, [datetimeoffset]$At, [string]$Conclusion = 'success', [string]$Event = 'push') {
            $script:jobId++
            $runId = $script:jobId * 10
            $script:world.Queued = @((New-QueuedJob $script:jobId $runId $Lane))
            Set-Run $runId $Event $(if ($Event -eq 'push') { 'main' } else { 'feature' })
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $At
            $name = @($script:journal.Vms.Keys | Where-Object { $script:journal.Vms[$_].JobId -eq $script:jobId })[0]
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $At.AddSeconds(10)
            $script:world.Queued = @()
            $script:world.VmStates[$name] = 'Off'
            Set-DoneJob $script:jobId $runId $name $Conclusion
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $At.AddMinutes(20)
        }
    }
    BeforeEach { $script:jobId = 100 }
    It 'records each lane that went green on a VM newer than the golden, and logs once when all four did' {
        Complete-GreenJob 'rust' $t0
        $journal.GoldenAccepted.Golden | Should -Be '2026-10-10T11:00:00Z'
        @($journal.GoldenAccepted.Lanes) | Should -Be @('rust')
        $journal.GoldenAccepted.Complete | Should -BeFalse
        Complete-GreenJob 'app' $t0.AddHours(1)
        Complete-GreenJob 'rust' $t0.AddHours(2)
        Complete-GreenJob 'model' $t0.AddHours(3)
        @($journal.GoldenAccepted.Lanes | Sort-Object) | Should -Be @('app', 'model', 'rust')
        $journal.GoldenAccepted.Complete | Should -BeFalse
        Complete-GreenJob 'shell' $t0.AddHours(4)
        $journal.GoldenAccepted.Complete | Should -BeTrue
        ([regex]::Matches(($logs -join "`n"), 'golden: rust, app, model and shell are green on the current image')).Count | Should -Be 1
        Complete-GreenJob 'app' $t0.AddHours(5)
        ([regex]::Matches(($logs -join "`n"), 'green on the current image')).Count | Should -Be 1
    }
    It 'survives the journal being written and read back between two green jobs' {
        Complete-GreenJob 'rust' $t0
        $p = Join-Path $TestDrive 'journal.json'
        Write-Journal -Path $p -Journal $journal
        $script:journal = Read-Journal -Path $p
        Complete-GreenJob 'app' $t0.AddHours(1)
        @($journal.GoldenAccepted.Lanes | Sort-Object) | Should -Be @('app', 'rust')
    }
    It 'ignores a green job on a VM provisioned before the golden was sealed' {
        $world.GoldenStamp = $t0.AddMinutes(5).ToString('o')
        Complete-GreenJob 'rust' $t0
        @($journal.GoldenAccepted.Lanes).Count | Should -Be 0
    }
    It 'starts over when the golden changes' {
        Complete-GreenJob 'rust' $t0
        $world.GoldenStamp = '2026-10-12T01:00:00Z'
        Complete-GreenJob 'rust' $t0.AddDays(2)
        $journal.GoldenAccepted.Golden | Should -Be '2026-10-12T01:00:00Z'
        @($journal.GoldenAccepted.Lanes) | Should -Be @('rust')
    }
    It 'records nothing for a red job, and records a green pull-request job (the image ran it, whatever the cache did)' {
        Complete-GreenJob 'app' $t0 'failure'
        $journal.GoldenAccepted | Should -Be $null
        Complete-GreenJob 'app' $t0.AddHours(1) 'success' 'pull_request'
        @($journal.GoldenAccepted.Lanes) | Should -Be @('app')
    }
    It 'a failing stamp read never wedges the settle' {
        $adapters.GetGoldenStamp = { throw 'disk gone' }
        Complete-GreenJob 'rust' $t0
        $journal.Vms.Count | Should -Be 0
        ($logs -join "`n") | Should -Match 'golden: disk gone'
    }
}

Context 'Invoke-StartupSweep' {
    It 'removes every slate-win runner and VM it does not keep, cleans directories, resets the journal' {
        $world.Runners = @(
            [pscustomobject]@{ id = 1; name = 'slate-win-app-aaaaaaaa'; status = 'offline' },
            [pscustomobject]@{ id = 2; name = 'slate-win-rust-bbbbbbbb'; status = 'online' },
            [pscustomobject]@{ id = 3; name = 'unrelated'; status = 'offline' }
        )
        $world.Vms = @('slate-win-app-aaaaaaaa', 'slate-win-model-cccccccc')
        $journal.Vms['stale'] = @{ Name = 'stale' }
        $journal.Retries['9'] = @{ Count = 1; NextAt = $t0.ToString('o') }
        Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
        # Online or offline: the runner of a VM that is about to go stays
        # listed online for a while after the VM is turned off.
        @((Get-Calls 'RemoveRunner') | ForEach-Object { $_.Args[0] }) | Should -Be @(1, 2)
        (Get-Calls 'StopVmForce').Count | Should -Be 2
        (Get-Calls 'RemoveVm').Count | Should -Be 2
        (Get-Calls 'RemoveVm')[1].Args[1] | Should -Be 'C:\slate-ci\vms\slate-win-model-cccccccc'
        (Get-Calls 'CleanVmDirs').Count | Should -Be 1
        $journal.Vms.Count | Should -Be 0
        $journal.Retries.Count | Should -Be 0
    }
    It 'keeps a handed-off VM that is still running, with its runner, seen job and retries' {
        # A loop relaunch after a crash, or a manual run, must not kill the
        # jobs in flight: the journal is reconciled with Hyper-V, not reset.
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0                   # provision
        $name = @($journal.Vms.Keys)[0]
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)   # handoff
        $journal.Vms[$name].Phase | Should -Be 'handed'
        $world.Queued = @()
        $world.Runners = @(
            [pscustomobject]@{ id = 77; name = $name; status = 'online' },
            [pscustomobject]@{ id = 5; name = 'slate-win-app-gone0000'; status = 'online' }
        )
        $world.Vms = @($name, 'slate-win-app-gone0000')
        $journal.Retries['9'] = @{ Count = 1; NextAt = $t0.ToString('o') }
        Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
        $journal.Vms.ContainsKey($name) | Should -BeTrue
        $journal.Vms[$name].Phase | Should -Be 'handed'
        $journal.SeenJobs.ContainsKey('1') | Should -BeTrue
        $journal.Retries.ContainsKey('9') | Should -BeTrue
        @((Get-Calls 'RemoveRunner') | ForEach-Object { $_.Args[0] }) | Should -Be @(5)
        @((Get-Calls 'StopVmForce') | ForEach-Object { $_.Args[0] }) | Should -Be @('slate-win-app-gone0000')
        @((Get-Calls 'RemoveVm') | ForEach-Object { $_.Args[0] }) | Should -Be @('slate-win-app-gone0000')
        @((Get-Calls 'CleanVmDirs')[0].Args) | Should -Be @($name)
        @(Get-FreeSlots -Config $config -Journal $journal).Count | Should -Be 1
        ($logs -join "`n") | Should -Match "sweep: kept running vm $name \(job 1, handed off\)"
    }
    It 'never keeps a VM still waiting for its handoff, nor one that is not running' {
        # The handoff is not idempotent (a second AddKvpItems fails, a second
        # JIT registration leaks a runner), so a provisioned VM goes.
        $world.Queued = @((New-QueuedJob 1 10 'app'), (New-QueuedJob 2 11 'rust'))
        $world.Heartbeat = 'NoContact'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $names = @($journal.Vms.Keys)
        $world.Vms = $names
        Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
        $journal.Vms.Count | Should -Be 0
        $journal.SeenJobs.Count | Should -Be 0
        @((Get-Calls 'StopVmForce') | ForEach-Object { $_.Args[0] } | Sort-Object) | Should -Be @($names | Sort-Object)
    }
}

Context 'health breaker' {
    # Three retryable discards in a row with no runner claiming its job
    # between them mean the host, not the jobs, is failing: it publishes
    # serving false until fifteen minutes pass without a failure, so the
    # router sends every lane to the vendor, and says so once.
    BeforeEach {
        $script:failingNewVm = { param($n, $l) & $script:record 'NewVm' @($n, $l); throw 'vmms down' }
        $script:workingNewVm = $script:adapters.NewVm
        $script:breakerVm = {
            # A handed-off VM in the journal, claimed, old enough to expire.
            param([string]$Name, [datetimeoffset]$StartedAt)
            $script:journal.Vms[$Name] = @{
                Name = $Name; Lane = 'app'; Slot = 1; SlotIp = '10.77.0.11'; JobId = [int64]1; RunId = [int64]10
                Dir = "C:\slate-ci\vms\$Name"; CachePath = $null; ForkGeneration = 3
                Phase = 'handed'; StartedAt = $StartedAt.ToString('o'); HandedAt = $StartedAt.AddSeconds(10).ToString('o'); RunnerId = 77; Claimed = $true; PendingCommit = $false
                GuestError = $null; SettleWaits = 0; ClaimCheckedAt = $null
            }
        }
        $script:tripNow = {
            # Three failures: two slots at t0, the third job at t0+10.
            $script:adapters.NewVm = $script:failingNewVm
            $script:world.Queued = @((New-QueuedJob 1 10 'app'), (New-QueuedJob 2 11 'rust'), (New-QueuedJob 3 12 'model'))
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $script:t0
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $script:t0.AddSeconds(10)
        }
        $script:twoFailuresThenAVm = {
            # Two failures at t0, then a working VM for job 3 of the lane,
            # provisioned at t0+10 and handed off at t0+20.
            param([string]$Lane = 'model')
            $script:adapters.NewVm = $script:failingNewVm
            $script:world.Queued = @((New-QueuedJob 1 10 'app'), (New-QueuedJob 2 11 'rust'))
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $script:t0
            $script:journal.Health.Failures | Should -Be 2
            $script:adapters.NewVm = $script:workingNewVm
            $script:world.Queued = @((New-QueuedJob 3 12 $Lane))
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $script:t0.AddSeconds(10)
            Invoke-OrchestratorTick -Config $script:config -Journal $script:journal -Adapters $script:adapters -Now $script:t0.AddSeconds(20)
            $script:journal.Health.Failures | Should -Be 2 -Because 'a handoff is not yet a runner at work'
            @($script:journal.Vms.Keys)[0]
        }
    }
    It 'trips on the third failure across different jobs, publishes serving false, and says why once' {
        $adapters.NewVm = $failingNewVm
        $world.Queued = @((New-QueuedJob 1 10 'app'), (New-QueuedJob 2 11 'rust'), (New-QueuedJob 3 12 'model'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.Health.Failures | Should -Be 2
        $journal.PublishedState | Should -Match '"serving":true'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $journal.Health.Failures | Should -Be 3
        $journal.Health.LastReason | Should -Be 'provisioning failed'
        $journal.PublishedState | Should -Be '{"v":1,"at":"2026-10-10T12:00:10Z","serving":false,"slots":2,"running":[],"queued":[]}'
        ($logs -join "`n") | Should -Match "\[warn\] health: not serving for at least 15 min: 3 failures in a row, last 'provisioning failed'"
        # Said once; the next tick changes nothing and writes nothing.
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        ([regex]::Matches(($logs -join "`n"), 'health:')).Count | Should -Be 1
        (Get-Calls 'PublishState').Count | Should -Be 2
    }
    It 'counts a failed handoff, a VM without a heartbeat, and a VM that shut down without running a job' {
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $adapters.SendKvp = { param($n, $i) & $script:record 'SendKvp' @($n, $i); throw 'kvp refused' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $journal.Health.Failures | Should -Be 1
        $journal.Health.LastReason | Should -Be 'handoff failed'
        $adapters.SendKvp = { param($n, $i) & $script:record 'SendKvp' @($n, $i) }
        # The guest's heartbeat gates the handoff: none within the timeout
        # of provisioning is a discard of the host's making.
        $world.Queued = @((New-QueuedJob 2 11 'rust'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        $world.Heartbeat = $null
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20 + 181)
        $journal.Health.Failures | Should -Be 2
        $journal.Health.LastReason | Should -Be 'no heartbeat'
        $journal.PublishedState | Should -Match '"serving":true'
        # Handed off, then Off with its job still queued and no record of a
        # job on this runner: the guest's bootstrap failed. The third strike.
        $world.Heartbeat = 'OK'
        $world.Queued = @((New-QueuedJob 3 12 'model'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(220)
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(230)
        $name = @($journal.Vms.Keys)[0]
        $journal.Vms[$name].Phase | Should -Be 'handed'
        $world.VmStates[$name] = 'Off'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(25)
        ($logs -join "`n") | Should -Match "${name}: shut down without running a job"
        $journal.Health.Failures | Should -Be 3
        $journal.Health.LastReason | Should -Be 'shut down without running a job'
        $journal.PublishedState | Should -Match '"serving":false'
    }
    It 'does not count a discard that is not the host''s fault' {
        # A VM past its lane cap: the job ran too long, the host served it.
        & $script:breakerVm 'slate-win-app-old00000' $t0.AddMinutes(-200)
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        ($logs -join "`n") | Should -Match 'expired after'
        # Nothing recorded: the record stays empty rather than counting.
        [int]$journal.Health.Failures | Should -Be 0
        $journal.PublishedState | Should -Match '"serving":true'
    }
    It 'forgets the failures when GitHub reports the runner busy, and logs the recovery' {
        $null = & $script:twoFailuresThenAVm
        $world.Runner = [pscustomobject]@{ status = 'online'; busy = $true }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20 + 310)
        @($journal.Vms.Values)[0].Claimed | Should -BeTrue
        $journal.Health.Failures | Should -Be 0
        $journal.Health.LastReason | Should -Be $null
        ($logs -join "`n") | Should -Match '\[info\] health: a runner took its job; 2 failures in a row forgotten'
    }
    It 'forgets the failures when the job is found running on this runner' {
        $name = & $script:twoFailuresThenAVm
        $world.Jobs['3'] = [pscustomobject]@{ id = 3; run_id = 12; status = 'in_progress'; runner_name = $name }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20 + 310)
        $journal.Vms[$name].Claimed | Should -BeTrue
        ($logs -join "`n") | Should -Match "${name}: claimed \(job in_progress on this runner\)"
        $journal.Health.Failures | Should -Be 0
    }
    It 'forgets the failures when admission finds the runner on another job of its lane' {
        $name = & $script:twoFailuresThenAVm 'model'
        $world.Queued = @((New-QueuedJob 3 12 'model' '2026-10-10T11:00:00Z'))
        $world.InProgress = @([pscustomobject]@{ id = 4; run_id = 12; status = 'in_progress'; labels = @('slate-win-model'); created_at = '2026-10-10T11:00:01Z'; runner_name = $name })
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        $journal.Vms[$name].JobId | Should -Be 4
        $journal.Vms[$name].Claimed | Should -BeTrue
        $journal.Health.Failures | Should -Be 0
    }
    It 'forgets the failures when a VM completes a job, even a failed one' {
        $name = & $script:twoFailuresThenAVm
        Set-DoneJob 3 12 $name 'failure'
        Set-Run 12
        $world.VmStates[$name] = 'Off'
        # GitHub no longer lists the job as queued, so nothing is re-admitted.
        $world.Queued = @()
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        $journal.Vms.Count | Should -Be 0
        $journal.Health.Failures | Should -Be 0
        ($logs -join "`n") | Should -Match 'a runner took its job; 2 failures in a row forgotten'
    }
    It 'resets once per completed VM, so a teardown that keeps failing cannot hide the failures beside it' {
        # The reviewer's reproduction: one VM is Off with its job done but
        # its cache merge throws every tick, so it stays in the journal and
        # settles again each tick, while the other slot fails provisioning.
        $world.Queued = @((New-QueuedJob 3 12 'model'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $name = @($journal.Vms.Keys)[0]
        $journal.Vms[$name].Phase | Should -Be 'handed'
        Set-DoneJob 3 12 $name 'success'
        Set-Run 12
        $world.VmStates[$name] = 'Off'
        $adapters.CommitCache = { param($lane, $child) & $script:record 'CommitCache' @($lane, $child); throw 'merge failed: disk full' }
        $adapters.NewVm = $failingNewVm
        $world.Queued = @((New-QueuedJob 1 10 'app'), (New-QueuedJob 2 11 'rust'), (New-QueuedJob 5 13 'shell'), (New-QueuedJob 6 14 'app' '2026-10-10T12:00:01Z'))
        $max = 0
        foreach ($s in 20, 30, 40, 50, 60) {
            Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds($s)
            $max = [math]::Max($max, [int]$journal.Health.Failures)
        }
        $journal.Vms.ContainsKey($name) | Should -BeTrue -Because 'the merge keeps failing'
        $max | Should -BeGreaterOrEqual 3
        $journal.PublishedState | Should -Match '"serving":false'
        # The one reset came before any failure (nothing to forget, no line);
        # the later ticks settle the same VM again and forget nothing.
        $journal.Vms[$name].Claimed | Should -BeTrue
        ([regex]::Matches(($logs -join "`n"), 'a runner took its job')).Count | Should -Be 0
    }
    It 'forgets the failures when a foreign job took the runner, which still proves the host works' {
        $name = & $script:twoFailuresThenAVm 'model'
        Set-Run 99 'pull_request' 'feature' 'someone/slate'
        $world.Queued = @((New-QueuedJob 3 12 'model'))
        $world.InProgress = @([pscustomobject]@{ id = 4; run_id = 99; status = 'in_progress'; labels = @('slate-win-model'); created_at = $t0.ToString('o'); runner_name = $name })
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)
        @($logs) | Should -Contain "[warn] handoff: ${name} runs job 4 of someone/slate (not the trusted repository); forcing it off"
        $journal.Health.Failures | Should -Be 0
    }
    It 'serves again fifteen minutes after the last failure, and trips at once on the next one' {
        & $script:tripNow
        $journal.PublishedState | Should -Match '"serving":false'
        # The jobs were cancelled meanwhile: nothing to admit during the cool-down.
        $world.Queued = @()
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10 + 899)
        $journal.PublishedState | Should -Match '"serving":false'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10 + 900)
        $journal.PublishedState | Should -Be '{"v":1,"at":"2026-10-10T12:15:10Z","serving":true,"slots":2,"running":[],"queued":[]}'
        ($logs -join "`n") | Should -Match '\[info\] health: serving again after 15 min without a failure; the next failure trips it at once'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10 + 910)
        ([regex]::Matches(($logs -join "`n"), 'serving again')).Count | Should -Be 1
        $world.Queued = @((New-QueuedJob 4 13 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10 + 920)
        $journal.Health.Failures | Should -Be 4
        $journal.PublishedState | Should -Match '"at":"2026-10-10T12:15:30Z","serving":false'
        ([regex]::Matches(($logs -join "`n"), '\[warn\] health: not serving')).Count | Should -Be 2
        ($logs -join "`n") | Should -Match "not serving for at least 15 min: 4 failures in a row, last 'provisioning failed'"
    }
    It 'restarts the cool-down silently on a failure during it, such as a retried job' {
        & $script:tripNow
        # Jobs 1 and 2 leave their back-off at t0+600 and fail again.
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(620)
        $journal.Health.Failures | Should -Be 5
        (ConvertTo-DateTimeOffset -Value $journal.Health.TrippedAt) | Should -Be $t0.AddSeconds(620)
        ([regex]::Matches(($logs -join "`n"), 'health:')).Count | Should -Be 1 -Because 'a failure during the cool-down is not news'
        $world.Queued = @()
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10 + 900)
        $journal.PublishedState | Should -Match '"serving":false' -Because 'the cool-down counts from the last failure'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(620 + 899)
        $journal.PublishedState | Should -Match '"serving":false'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(620 + 900)
        $journal.PublishedState | Should -Match '"serving":true'
        ([regex]::Matches(($logs -join "`n"), 'serving again')).Count | Should -Be 1
    }
    It 'keeps the record across a journal round trip' {
        & $script:tripNow
        $p = Join-Path $TestDrive 'journal.json'
        Write-Journal -Path $p -Journal $journal
        $script:journal = Read-Journal -Path $p
        $world.Queued = @()
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10 + 899)
        $journal.PublishedState | Should -Match '"serving":false'
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10 + 900)
        $journal.PublishedState | Should -Match '"serving":true'
    }
    It 'forgets the failures at the startup sweep, which is what follows a repair' {
        & $script:tripNow
        $journal.Health.Failures | Should -Be 3
        Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
        $journal.Health.Failures | Should -Be 0
        $journal.Health.TrippedAt | Should -Be $null
        ($logs -join "`n") | Should -Match '\[info\] health: 3 failures in a row forgotten at start'
        $world.Queued = @()
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        $journal.PublishedState | Should -Match '"serving":true'
    }
    It 'forgets every job''s retry strikes when the breaker half-opens, and admits the jobs it gave up on as the probes' {
        # Three jobs strike three times each while the host fails (each
        # strike restarts the cool-down), so all three reach the cap.
        & $script:tripNow
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(610)
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(620)
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(1220)
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(1230)
        foreach ($id in '1', '2', '3') { $journal.Retries[$id].Count | Should -Be 3 -Because "job $id" }
        (Get-Calls 'NewVm').Count | Should -Be 9
        # Capped: nothing admitted and nothing published as queued, even
        # though the host works again and GitHub still lists the jobs.
        $adapters.NewVm = $workingNewVm
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(1240)
        (Get-Calls 'NewVm').Count | Should -Be 9
        $journal.PublishedState | Should -Match '"serving":false,"slots":2,"running":\[\],"queued":\[\]'
        # The cool-down from the last failure passes: the strikes are
        # forgotten in the same tick, two jobs start and the third is queued.
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(1230 + 900)
        ($logs -join "`n") | Should -Match '\[info\] health: serving again after 15 min without a failure'
        ($logs -join "`n") | Should -Match '\[info\] health: retry strikes of 3 jobs forgotten; they are admitted again'
        $journal.Retries.Count | Should -Be 0
        (Get-Calls 'NewVm').Count | Should -Be 11
        $journal.Vms.Count | Should -Be 2
        $journal.PublishedState | Should -Match '"serving":true,"slots":2,"running":\[\{"lane":"app","since":"[^"]+"\},\{"lane":"rust","since":"[^"]+"\}\],"queued":\[\{"lane":"model"'
    }
    It 'forgets the strikes when a runner takes its job after a trip, so a struck job is admitted again at once' {
        & $script:tripNow
        $adapters.NewVm = $workingNewVm
        $world.Queued = @((New-QueuedJob 1 10 'app'), (New-QueuedJob 2 11 'rust'), (New-QueuedJob 3 12 'model'), (New-QueuedJob 4 13 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)     # job 4 provisioned (1-3 in back-off)
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30)     # handed off
        (Get-Calls 'NewVm').Count | Should -Be 4
        $world.Runner = [pscustomobject]@{ status = 'online'; busy = $true }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(30 + 310)
        ($logs -join "`n") | Should -Match 'health: a runner took its job; 3 failures in a row forgotten'
        ($logs -join "`n") | Should -Match 'health: retry strikes of 3 jobs forgotten; they are admitted again'
        $journal.Retries.Count | Should -Be 0
        # Job 1 was in back-off until t0+600; it starts in this very tick.
        (Get-Calls 'NewVm').Count | Should -Be 5
        $journal.Vms.Count | Should -Be 2
    }
    It 'keeps a job''s strikes when a runner takes its job on a host that never tripped' {
        # Two strikes do not trip the breaker; a claim then says nothing
        # about those jobs, which keep their back-off and their cap.
        $null = & $script:twoFailuresThenAVm
        $world.Runner = [pscustomobject]@{ status = 'online'; busy = $true }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20 + 310)
        $journal.Health.Failures | Should -Be 0
        $journal.Retries['1'].Count | Should -Be 1
        $journal.Retries['2'].Count | Should -Be 1
        ($logs -join "`n") | Should -Not -Match 'retry strikes'
    }
    It 'reads an older journal without a health record as healthy, and leaves it alone' {
        $journal.Remove('Health')
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.PublishedState | Should -Match '"serving":true'
        # Nothing was recorded, so nothing was written: the state is built from the journal, not into it.
        $journal.ContainsKey('Health') | Should -BeFalse
    }
    It 'treats a hand-edited record that does not parse as healthy, without failing the tick' {
        $journal.Health = @{ Failures = 3; TrippedAt = 'not a time'; LastReason = 'provisioning failed' }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0
        $journal.PublishedState | Should -Match '"serving":true'
        $journal.Health = @{ Failures = 'many'; TrippedAt = $null }
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(10)
        $journal.PublishedState | Should -Match '"serving":true'
        # The next failure counts from one again.
        $adapters.NewVm = $failingNewVm
        $world.Queued = @((New-QueuedJob 1 10 'app'))
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddSeconds(20)
        $journal.Health.Failures | Should -Be 1
        ($logs -join "`n") | Should -Not -Match '\[error\] health:'
        # The startup sweep reads the same record: a bad count must not stop the loop from starting.
        $journal.Health = @{ Failures = 'many'; TrippedAt = 'not a time' }
        { Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters } | Should -Not -Throw
        $journal.Health.Failures | Should -Be 0
    }
}

}

Describe 'orchestrator.ps1' {
    It 'exits 2 at startup until the golden disk exists and is sealed, right after the token check' {
        # Text-level: the script is never run here.
        $path = Join-Path $PSScriptRoot '..' 'orchestrator.ps1'
        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$null, [ref]$errors)
        @($errors).Count | Should -Be 0
        $text = Get-Content -Raw $path
        $text | Should -Match 'if \(-not \(\(Test-Path -LiteralPath \$config\.GoldenPath\) -and \(Get-Item -LiteralPath \$config\.GoldenPath\)\.IsReadOnly\)\) \{\s+& \$log ''error'' "golden disk missing or not sealed \(read-only\) at [^"\r\n]*"\s+exit 2\s+\}'
        $golden = $text.IndexOf('golden disk missing or not sealed')
        $golden | Should -BeGreaterThan $text.IndexOf('token missing at')
        $golden | Should -BeLessThan $text.IndexOf('Initialize-GitHubAdapter')
    }
    It 'names the daily log file through the module''s helper, as the log pruner parses it' {
        # Text-level: under ar-SA or th-TH, Get-Date -Format writes a Hijri or
        # Buddhist year, a name Get-ExpiredLogNames reads as another date. The
        # module names the file once (Get-OrchestratorLogName, tested there).
        $text = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'orchestrator.ps1')
        $text | Should -Match ([regex]::Escape('Write-CiLog -Path (Join-Path $config.LogDir (Get-OrchestratorLogName -Now ([datetimeoffset]::Now)))'))
        $text | Should -Not -Match 'orchestrator-\{0\}'
        $text | Should -Not -Match 'Get-Date\s+-Format'
    }
    It 'polls only the configured routed workflows, logs a low API budget at warn and a retried GET at info' {
        $text = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'orchestrator.ps1')
        $text | Should -Match ([regex]::Escape('New-GitHubAdapters -RoutedWorkflows @($config.RoutedWorkflows)'))
        # Dot-sourcing the adapter resets the hooks, so they are set afterwards.
        foreach ($hook in '$script:GhLowBudgetWarning = { param($message) & $log ''warn'' $message }',
                          '$script:GhRetryNotice = { param($message) & $log ''info'' $message }') {
            $text | Should -Match ([regex]::Escape($hook)) -Because $hook
            $text.IndexOf($hook) | Should -BeGreaterThan $text.IndexOf("'GitHub.ps1')") -Because $hook
        }
    }
}
