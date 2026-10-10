# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force

    function New-World {
        # Mutable fake of GitHub + Hyper-V; the adapters read it.
        $script:world = @{
            Queued     = @()
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
        }
        $script:calls = [System.Collections.ArrayList]::new()
        $script:logs = [System.Collections.ArrayList]::new()
    }

    function New-FakeAdapters {
        # The module invokes these long after this function returned, so
        # everything they touch lives at $script: scope (never a local).
        $script:record = { param($name, $arguments) [void]$script:calls.Add(@{ Name = $name; Args = @($arguments) }) }
        $a = @{
            GetQueuedJobs = { & $script:record 'GetQueuedJobs' @(); @($script:world.Queued) }
            GetJob        = { param($id) & $script:record 'GetJob' @($id); $script:world.Jobs[[string]$id] }
            GetRun        = { param($id) & $script:record 'GetRun' @($id); $script:world.Runs[[string]$id] }
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
            Log           = { param($level, $message) [void]$script:logs.Add("[$level] $message") }
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
        # The journal entry's shape: 17 keys, every one present from admission.
        @($vm.Keys | Sort-Object) | Should -Be @('CachePath', 'Claimed', 'Dir', 'ForkGeneration', 'GuestError', 'HandedAt', 'JobId', 'Lane', 'Name', 'PendingCommit', 'Phase', 'RunId', 'RunnerId', 'SettleWaits', 'Slot', 'SlotIp', 'StartedAt')
        $vm.GuestError | Should -Be $null
        $vm.SettleWaits | Should -Be 0
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
    It 'survives a GetQueuedJobs failure' {
        $adapters.GetQueuedJobs = { throw 'api down' }
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
        # GitHub's record can lag the guest's shutdown by a few seconds.
        $world.Jobs['1'] = [pscustomobject]@{ id = 1; run_id = 10; status = 'in_progress'; conclusion = $null; runner_name = $name }
        Set-Run 10
        Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now $t0.AddMinutes(20)
        $journal.Vms.ContainsKey($name) | Should -BeTrue
        $journal.Vms[$name].SettleWaits | Should -Be 1
        (Get-Calls 'DiscardCache').Count | Should -Be 0
        (Get-Calls 'CommitCache').Count | Should -Be 0
        (Get-Calls 'RemoveVm').Count | Should -Be 0
        (Get-Calls 'GetRun').Count | Should -Be 0
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
}

Context 'Invoke-StartupSweep' {
    It 'removes stale slate-win runners and leftover VMs, cleans directories, resets the journal' {
        $world.Runners = @(
            [pscustomobject]@{ id = 1; name = 'slate-win-app-aaaaaaaa'; status = 'offline' },
            [pscustomobject]@{ id = 2; name = 'slate-win-rust-bbbbbbbb'; status = 'online' },
            [pscustomobject]@{ id = 3; name = 'unrelated'; status = 'offline' }
        )
        $world.Vms = @('slate-win-app-aaaaaaaa', 'slate-win-model-cccccccc')
        $journal.Vms['stale'] = @{ Name = 'stale' }
        $journal.Retries['9'] = @{ Count = 1; NextAt = $t0.ToString('o') }
        Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
        @((Get-Calls 'RemoveRunner') | ForEach-Object { $_.Args[0] }) | Should -Be @(1)
        (Get-Calls 'StopVmForce').Count | Should -Be 2
        (Get-Calls 'RemoveVm').Count | Should -Be 2
        (Get-Calls 'RemoveVm')[1].Args[1] | Should -Be 'C:\slate-ci\vms\slate-win-model-cccccccc'
        (Get-Calls 'CleanVmDirs').Count | Should -Be 1
        $journal.Vms.Count | Should -Be 0
        $journal.Retries.Count | Should -Be 0
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
    It 'polls only the configured routed workflows and logs a low API budget at warn' {
        $text = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'orchestrator.ps1')
        $text | Should -Match ([regex]::Escape('New-GitHubAdapters -RoutedWorkflows @($config.RoutedWorkflows)'))
        $hook = '$script:GhLowBudgetWarning = { param($message) & $log ''warn'' $message }'
        $text | Should -Match ([regex]::Escape($hook))
        # Dot-sourcing the adapter resets the hook, so it is set afterwards.
        $text.IndexOf($hook) | Should -BeGreaterThan $text.IndexOf("'GitHub.ps1')")
    }
}
