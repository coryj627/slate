# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force
}

Describe 'Get-LaneFromLabels' {
    It 'returns the lane when exactly one lane label is present' {
        Get-LaneFromLabels -Labels @('self-hosted', 'Windows', 'X64', 'slate-win-app') | Should -Be 'app'
    }
    It 'returns null when no lane label is present' {
        Get-LaneFromLabels -Labels @('self-hosted', 'windows-latest') | Should -BeNullOrEmpty
    }
    It 'returns null for two lane labels' {
        Get-LaneFromLabels -Labels @('slate-win-app', 'slate-win-model') | Should -BeNullOrEmpty
    }
    It 'ignores an unknown lane name' {
        Get-LaneFromLabels -Labels @('slate-win-nightly') | Should -BeNullOrEmpty
    }
    It 'ignores labels that merely contain the prefix' {
        Get-LaneFromLabels -Labels @('slate-win-app-old', 'xslate-win-app') | Should -BeNullOrEmpty
    }
    It 'handles an empty label list' {
        Get-LaneFromLabels -Labels @() | Should -BeNullOrEmpty
    }
    It 'normalises the lane to lower case' {
        Get-LaneFromLabels -Labels @('SLATE-WIN-APP') | Should -BeExactly 'app'
    }
}

Describe 'New-RunnerName' {
    It 'produces slate-win-`<lane`>-`<8 hex`>' {
        New-RunnerName -Lane 'model' | Should -Match '^slate-win-model-[0-9a-f]{8}$'
    }
    It 'is unique across calls' {
        (1..20 | ForEach-Object { New-RunnerName -Lane 'rust' } | Sort-Object -Unique).Count | Should -Be 20
    }
}

Describe 'Split-KvpChunks / Join-KvpChunks' {
    It 'splits into 1000-character chunks with a count item' {
        $text = 'a' * 2500
        $items = Split-KvpChunks -Text $text
        $items['slate.jit.count'] | Should -Be '3'
        $items['slate.jit.0'].Length | Should -Be 1000
        $items['slate.jit.2'].Length | Should -Be 500
        $items.Keys.Count | Should -Be 4
    }
    It 'round-trips an arbitrary payload' {
        $text = -join ((1..3333) | ForEach-Object { [char](33 + ($_ % 90)) })
        Join-KvpChunks -Items (Split-KvpChunks -Text $text) | Should -Be $text
    }
    It 'round-trips a payload that is an exact multiple of the chunk size (boundary)' {
        $text = 'b' * 3000
        $items = Split-KvpChunks -Text $text
        $items['slate.jit.count'] | Should -Be '3'
        Join-KvpChunks -Items $items | Should -Be $text
    }
    It 'round-trips an empty payload' {
        $items = Split-KvpChunks -Text ''
        $items['slate.jit.count'] | Should -Be '0'
        Join-KvpChunks -Items $items | Should -Be ''
    }
    It 'never emits a value longer than 1024 characters at any chunk size' {
        foreach ($size in 1, 7, 999, 1000) {
            $items = Split-KvpChunks -Text ('c' * 2048) -ChunkSize $size
            foreach ($k in $items.Keys) { $items[$k].Length | Should -BeLessOrEqual 1024 }
        }
    }
    It 'refuses a chunk size above 1024' {
        { Split-KvpChunks -Text 'x' -ChunkSize 1025 } | Should -Throw
    }
    It 'returns null when the count item is missing' {
        Join-KvpChunks -Items @{ 'slate.jit.0' = 'x' } | Should -BeNullOrEmpty
    }
    It 'returns null when a chunk is missing' {
        Join-KvpChunks -Items @{ 'slate.jit.count' = '2'; 'slate.jit.0' = 'x' } | Should -BeNullOrEmpty
    }
    It 'honours a custom prefix' {
        $items = Split-KvpChunks -Text 'hello' -Prefix 'p'
        $items['p.count'] | Should -Be '1'
        Join-KvpChunks -Items $items -Prefix 'p' | Should -Be 'hello'
    }
}

Describe 'ConvertTo-DateTimeOffset' {
    It 'keeps the instant for a Z string, an offset string, and every datetime kind' {
        $expected = [datetimeoffset]::Parse('2026-10-10T10:00:00Z', [cultureinfo]::InvariantCulture)
        (ConvertTo-DateTimeOffset -Value '2026-10-10T10:00:00Z').UtcDateTime | Should -Be $expected.UtcDateTime
        (ConvertTo-DateTimeOffset -Value '2026-10-10T12:00:00+02:00').UtcDateTime | Should -Be $expected.UtcDateTime
        (ConvertTo-DateTimeOffset -Value ([datetime]::SpecifyKind([datetime]'2026-10-10T10:00:00', 'Utc'))).UtcDateTime | Should -Be $expected.UtcDateTime
        (ConvertTo-DateTimeOffset -Value ([datetime]::SpecifyKind([datetime]'2026-10-10T10:00:00', 'Utc')).ToLocalTime()).UtcDateTime | Should -Be $expected.UtcDateTime
        (ConvertTo-DateTimeOffset -Value ([datetime]'2026-10-10T10:00:00')).UtcDateTime | Should -Be $expected.UtcDateTime
        (ConvertTo-DateTimeOffset -Value $expected) | Should -Be $expected
    }
    It 'treats a zone-less string as UTC' {
        (ConvertTo-DateTimeOffset -Value '2026-10-10T10:00:00').Offset | Should -Be ([timespan]::Zero)
    }
}

Describe 'Select-QueuedLaneJobs' {
    BeforeAll {
        $script:job = {
            param($id, $run, $labels, $status, $created)
            [pscustomobject]@{ id = $id; run_id = $run; labels = $labels; status = $status; created_at = $created }
        }
    }
    It 'keeps only queued jobs with exactly one lane label, oldest first' {
        $jobs = @(
            (& $job 3 10 @('slate-win-app') 'queued' '2026-10-10T10:00:05Z'),
            (& $job 1 10 @('slate-win-rust') 'queued' '2026-10-10T10:00:01Z'),
            (& $job 2 11 @('slate-win-app', 'slate-win-model') 'queued' '2026-10-10T10:00:00Z'),
            (& $job 4 11 @('slate-win-model') 'in_progress' '2026-10-10T09:00:00Z'),
            (& $job 5 12 @('ubuntu-latest') 'queued' '2026-10-10T09:00:00Z')
        )
        $result = Select-QueuedLaneJobs -Jobs $jobs
        @($result).Count | Should -Be 2
        $result[0].JobId | Should -Be 1
        $result[0].Lane | Should -Be 'rust'
        $result[1].JobId | Should -Be 3
        $result[1].RunId | Should -Be 10
        $result[1].CreatedAt | Should -BeOfType [datetimeoffset]
    }
    It 'returns an empty array for no jobs' {
        @(Select-QueuedLaneJobs -Jobs @()).Count | Should -Be 0
    }
    It 'reads created_at as the right instant when the job came through ConvertFrom-Json' {
        $jobs = @('{"id":7,"run_id":10,"labels":["slate-win-app"],"status":"queued","created_at":"2026-10-10T10:00:00Z"}' | ConvertFrom-Json)
        $result = Select-QueuedLaneJobs -Jobs $jobs
        $result[0].CreatedAt.UtcDateTime.ToString('o') | Should -Be '2026-10-10T10:00:00.0000000Z'
    }
    It 'tolerates a job with no labels property value' {
        $jobs = @((& $job 9 10 @() 'queued' '2026-10-10T10:00:00Z'))
        @(Select-QueuedLaneJobs -Jobs $jobs).Count | Should -Be 0
    }
}

Describe 'Select-JobsToAdmit' {
    BeforeAll {
        $script:now = [datetimeoffset]::Parse('2026-10-10T12:00:00Z')
        $script:cand = {
            param($id, $lane)
            [pscustomobject]@{ JobId = [int64]$id; RunId = [int64]1; Lane = $lane; CreatedAt = $script:now }
        }
    }
    It 'admits up to the free slot count in order' {
        $c = @((& $cand 1 'app'), (& $cand 2 'rust'), (& $cand 3 'model'))
        $r = Select-JobsToAdmit -Candidates $c -ActiveVms @{} -FreeSlots 2 -Retries @{} -Now $now
        @($r).Count | Should -Be 2
        $r[0].JobId | Should -Be 1
        $r[1].JobId | Should -Be 2
    }
    It 'admits nothing when no slot is free' {
        $c = @((& $cand 1 'app'))
        @(Select-JobsToAdmit -Candidates $c -ActiveVms @{} -FreeSlots 0 -Retries @{} -Now $now).Count | Should -Be 0
    }
    It 'skips a job that already has a VM' {
        $c = @((& $cand 1 'app'), (& $cand 2 'rust'))
        $active = @{ 'slate-win-app-00000001' = @{ JobId = [int64]1 } }
        $r = Select-JobsToAdmit -Candidates $c -ActiveVms $active -FreeSlots 2 -Retries @{} -Now $now
        @($r).Count | Should -Be 1
        $r[0].JobId | Should -Be 2
    }
    It 'skips a job in back-off and admits it once the back-off elapses' {
        $c = @((& $cand 1 'app'))
        $retries = @{ '1' = @{ Count = 1; NextAt = $now.AddSeconds(60).ToString('o') } }
        @(Select-JobsToAdmit -Candidates $c -ActiveVms @{} -FreeSlots 1 -Retries $retries -Now $now).Count | Should -Be 0
        @(Select-JobsToAdmit -Candidates $c -ActiveVms @{} -FreeSlots 1 -Retries $retries -Now $now.AddSeconds(61)).Count | Should -Be 1
    }
    It 'never admits a job at the retry cap' {
        $c = @((& $cand 1 'app'))
        $retries = @{ '1' = @{ Count = 3; NextAt = $now.AddDays(-1).ToString('o') } }
        @(Select-JobsToAdmit -Candidates $c -ActiveVms @{} -FreeSlots 1 -Retries $retries -Now $now -RetryCap 3).Count | Should -Be 0
    }
}

Describe 'Register-JobRetry' {
    It 'creates and increments the retry record with a back-off' {
        $now = [datetimeoffset]::Parse('2026-10-10T12:00:00Z')
        $retries = @{}
        Register-JobRetry -Retries $retries -JobId 42 -Now $now -BackoffSeconds 600
        $retries['42'].Count | Should -Be 1
        [datetimeoffset]$retries['42'].NextAt | Should -Be $now.AddSeconds(600)
        Register-JobRetry -Retries $retries -JobId 42 -Now $now.AddSeconds(700) -BackoffSeconds 600
        $retries['42'].Count | Should -Be 2
    }
}

Describe 'Test-VmExpired' {
    It 'expires at max plus grace, not before' {
        $start = [datetimeoffset]::Parse('2026-10-10T12:00:00Z')
        Test-VmExpired -StartedAt $start -MaxMinutes 100 -Now $start.AddMinutes(109) | Should -BeFalse
        Test-VmExpired -StartedAt $start -MaxMinutes 100 -Now $start.AddMinutes(110) | Should -BeTrue
    }
    It 'honours a custom grace' {
        $start = [datetimeoffset]::Parse('2026-10-10T12:00:00Z')
        Test-VmExpired -StartedAt $start -MaxMinutes 30 -Now $start.AddMinutes(31) -GraceMinutes 0 | Should -BeTrue
    }
}

Describe 'Get-StaleRunnerNames' {
    It 'returns offline slate-win runners that are not active' {
        $runners = @(
            [pscustomobject]@{ id = 1; name = 'slate-win-app-aaaaaaaa'; status = 'offline' },
            [pscustomobject]@{ id = 2; name = 'slate-win-rust-bbbbbbbb'; status = 'online' },
            [pscustomobject]@{ id = 3; name = 'other-runner'; status = 'offline' },
            [pscustomobject]@{ id = 4; name = 'slate-win-model-cccccccc'; status = 'offline' }
        )
        $stale = @(Get-StaleRunnerNames -Runners $runners -ActiveNames @('slate-win-model-cccccccc'))
        $stale | Should -Be @('slate-win-app-aaaaaaaa')
    }
    It 'returns nothing for an empty list' {
        @(Get-StaleRunnerNames -Runners @()).Count | Should -Be 0
    }
}

Describe 'Test-CommitEligible' {
    BeforeAll {
        $script:goodJob = [pscustomobject]@{ id = 1; status = 'completed'; conclusion = 'success'; runner_name = 'slate-win-app-deadbeef'; run_id = 10 }
        $script:goodRun = [pscustomobject]@{ event = 'push'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
        $script:eligible = {
            param($job, $run, [bool]$forced, [int]$parent, [int]$fork)
            Test-CommitEligible -Job $job -Run $run -RunnerName 'slate-win-app-deadbeef' -ForcedOff $forced -ParentGeneration $parent -ForkGeneration $fork
        }
    }
    It 'commits a green push to main of the trusted repository with an unchanged generation' {
        $d = & $eligible $goodJob $goodRun $false 3 3
        $d.Eligible | Should -BeTrue
        $d.Reason | Should -Be 'trusted main'
    }
    It 'commits schedule and workflow_dispatch events on main' {
        foreach ($event in 'schedule', 'workflow_dispatch') {
            $run = [pscustomobject]@{ event = $event; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
            (& $eligible $goodJob $run $false 0 0).Eligible | Should -BeTrue
        }
    }
    It 'discards a pull_request event' {
        $run = [pscustomobject]@{ event = 'pull_request'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
        $d = & $eligible $goodJob $run $false 3 3
        $d.Eligible | Should -BeFalse
        $d.Reason | Should -Be 'event: pull_request'
    }
    It 'discards a push to another branch' {
        $run = [pscustomobject]@{ event = 'push'; head_branch = 'feature'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
        (& $eligible $goodJob $run $false 3 3).Reason | Should -Be 'branch: feature'
    }
    It 'discards a case variant of the trusted branch (git refs are case-sensitive)' {
        $run = [pscustomobject]@{ event = 'push'; head_branch = 'Main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
        (& $eligible $goodJob $run $false 3 3).Reason | Should -Be 'branch: Main'
    }
    It 'discards a case variant of a trusted event' {
        $run = [pscustomobject]@{ event = 'PUSH'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
        (& $eligible $goodJob $run $false 3 3).Reason | Should -Be 'event: PUSH'
    }
    It 'discards a branch that differs from main only by a default-ignorable code point' {
        $branch = "ma$([char]0x00AD)in"
        $run = [pscustomobject]@{ event = 'push'; head_branch = $branch; head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
        $d = & $eligible $goodJob $run $false 3 3
        $d.Eligible | Should -BeFalse
        $d.Reason | Should -BeExactly "branch: $branch"
    }
    It 'accepts a repository name that differs only by case' {
        $run = [pscustomobject]@{ event = 'push'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'CoryJ627/Slate' } }
        (& $eligible $goodJob $run $false 3 3).Eligible | Should -BeTrue
    }
    It 'refuses to decide without the forced-off flag and both generations' {
        $parameters = (Get-Command Test-CommitEligible).Parameters
        foreach ($name in 'ForcedOff', 'ParentGeneration', 'ForkGeneration') {
            @($parameters[$name].Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] } | ForEach-Object { $_.Mandatory }) | Should -Contain $true -Because "$name must be supplied"
        }
    }
    It 'discards a run from another repository' {
        $run = [pscustomobject]@{ event = 'push'; head_branch = 'main'; head_repository = [pscustomobject]@{ full_name = 'someone/slate' } }
        (& $eligible $goodJob $run $false 3 3).Reason | Should -Be 'repository: someone/slate'
    }
    It 'discards when head_repository is null (deleted fork) without throwing' {
        $run = [pscustomobject]@{ event = 'push'; head_branch = 'main'; head_repository = $null }
        $d = & $eligible $goodJob $run $false 3 3
        $d.Eligible | Should -BeFalse
        $d.Reason | Should -Be 'repository: '
    }
    It 'discards a failed or cancelled job' {
        foreach ($c in 'failure', 'cancelled', $null) {
            $job = [pscustomobject]@{ id = 1; status = 'completed'; conclusion = $c; runner_name = 'slate-win-app-deadbeef'; run_id = 10 }
            $d = & $eligible $job $goodRun $false 3 3
            $d.Eligible | Should -BeFalse
            $d.Reason | Should -Be "conclusion: $c"
        }
    }
    It 'discards when the host forced the VM off' {
        (& $eligible $goodJob $goodRun $true 3 3).Reason | Should -Be 'guest was forced off'
    }
    It 'discards when the parent generation moved since the fork' {
        (& $eligible $goodJob $goodRun $false 4 3).Reason | Should -Be 'generation moved: fork 3, parent 4'
    }
    It 'discards when no job resolved' {
        (& $eligible $null $goodRun $false 3 3).Reason | Should -Be 'no job resolved for runner'
    }
    It 'discards when the job ran on a different runner' {
        $job = [pscustomobject]@{ id = 1; status = 'completed'; conclusion = 'success'; runner_name = 'other'; run_id = 10 }
        (& $eligible $job $goodRun $false 3 3).Reason | Should -Be 'runner mismatch: other'
    }
    It 'discards when the run is missing' {
        (& $eligible $goodJob $null $false 3 3).Reason | Should -Be 'no run'
    }
    It 'honours custom trusted repo, branch and events' {
        $run = [pscustomobject]@{ event = 'push'; head_branch = 'release'; head_repository = [pscustomobject]@{ full_name = 'x/y' } }
        $d = Test-CommitEligible -Job $goodJob -Run $run -RunnerName 'slate-win-app-deadbeef' -ForcedOff $false -ParentGeneration 1 -ForkGeneration 1 -TrustedRepo 'x/y' -TrustedBranch 'release' -TrustedEvents @('push')
        $d.Eligible | Should -BeTrue
    }
}

Describe 'Resolve-RunnerJob' {
    It 'returns the admitted job when it ran on this runner' {
        $jobs = @{ 1 = [pscustomobject]@{ id = 1; runner_name = 'slate-win-app-deadbeef' } }
        $r = Resolve-RunnerJob -RunnerName 'slate-win-app-deadbeef' -AdmittedJobId 1 -CandidateJobIds @(2, 3) -GetJob { param($id) $jobs[[int]$id] }
        $r.id | Should -Be 1
    }
    It 'falls back to a candidate when another job took the runner' {
        $jobs = @{
            1 = [pscustomobject]@{ id = 1; runner_name = 'slate-win-app-other' }
            2 = [pscustomobject]@{ id = 2; runner_name = 'slate-win-app-deadbeef' }
        }
        $calls = [System.Collections.ArrayList]::new()
        $r = Resolve-RunnerJob -RunnerName 'slate-win-app-deadbeef' -AdmittedJobId 1 -CandidateJobIds @(1, 2, 3) -GetJob { param($id) [void]$calls.Add($id); $jobs[[int]$id] }
        $r.id | Should -Be 2
        @($calls) | Should -Be @(1, 2)
    }
    It 'returns null when nothing matches' {
        Resolve-RunnerJob -RunnerName 'slate-win-app-deadbeef' -AdmittedJobId 1 -CandidateJobIds @() -GetJob { param($id) $null } | Should -BeNullOrEmpty
    }
}

Describe 'Get-CiHostConfig' {
    It 'loads the repository config and derives the paths' {
        $cfg = Get-CiHostConfig -Path (Join-Path $PSScriptRoot '..' 'config.json')
        $cfg.Owner | Should -Be 'coryj627'
        @($cfg.Slots).Count | Should -Be 2
        $cfg.Slots[1].Ip | Should -Be '10.77.0.12'
        $cfg.Lanes['shell'].Cache | Should -BeFalse
        $cfg.Lanes['app'].MaxMinutes | Should -Be 100
        $cfg.GoldenPath | Should -Be 'C:\slate-ci\golden\win11-runner.vhdx'
        $cfg.CacheDir | Should -Be 'C:\slate-ci\cache'
        $cfg.VmDir | Should -Be 'C:\slate-ci\vms'
        $cfg.StateDir | Should -Be 'C:\slate-ci\state'
        $cfg.LogDir | Should -Be 'C:\slate-ci\logs'
        @($cfg.TrustedEvents) | Should -Be @('push', 'schedule', 'workflow_dispatch')
    }
    It 'rejects a config missing a required key' {
        $p = Join-Path $TestDrive 'bad.json'
        '{ "Owner": "x" }' | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*Repo*'
    }
    It 'rejects an unknown lane' {
        $p = Join-Path $TestDrive 'lane.json'
        $raw = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        $raw.Lanes['bogus'] = @{ Cache = $true; MaxMinutes = 5 }
        $raw | ConvertTo-Json -Depth 6 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*bogus*'
    }
    It 'rejects a lane key that differs from a lane only by case' {
        $p = Join-Path $TestDrive 'lanecase.json'
        $raw = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        $raw.Lanes.Remove('app')
        $raw.Lanes['App'] = @{ Cache = $true; MaxMinutes = 100 }
        $raw | ConvertTo-Json -Depth 6 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw "*unknown lane 'App'*"
    }
    It 'rejects a null Slots value' {
        $p = Join-Path $TestDrive 'noslots.json'
        $raw = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        $raw.Slots = $null
        $raw | ConvertTo-Json -Depth 6 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*at least one slot is required*'
    }
    It 'throws when the file is missing' {
        { Get-CiHostConfig -Path (Join-Path $TestDrive 'absent.json') -ErrorAction Continue } | Should -Throw '*absent.json*'
    }
    It 'rejects a Lanes value that is not an object' {
        $p = Join-Path $TestDrive 'lanesnull.json'
        $raw = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        $raw.Lanes = $null
        $raw | ConvertTo-Json -Depth 6 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*Lanes must be an object*'
    }
}

Describe 'Journal' {
    It 'returns a fresh journal when the file does not exist' {
        $j = Read-Journal -Path (Join-Path $TestDrive 'none.json')
        $j.Vms.Count | Should -Be 0
        $j.Retries.Count | Should -Be 0
        $j.SeenJobs.Count | Should -Be 0
    }
    It 'round-trips VM entries, retries and seen jobs' {
        $p = Join-Path $TestDrive 'journal.json'
        $j = New-Journal
        $j.Vms['slate-win-app-00000001'] = @{ Name = 'slate-win-app-00000001'; Lane = 'app'; Slot = 1; SlotIp = '10.77.0.11'; JobId = [int64]123456789012; RunId = [int64]5; Dir = 'C:\slate-ci\vms\x'; CachePath = 'C:\slate-ci\vms\x\cache.vhdx'; ForkGeneration = 7; Phase = 'handed'; StartedAt = '2026-10-10T12:00:00.0000000+00:00'; HandedAt = '2026-10-10T12:01:00.0000000+00:00'; RunnerId = 99; Claimed = $true }
        $j.Retries['42'] = @{ Count = 2; NextAt = '2026-10-10T12:10:00.0000000+00:00' }
        $j.SeenJobs['42'] = @{ RunId = [int64]5; Lane = 'app'; FirstSeenAt = '2026-10-10T12:00:00.0000000+00:00' }
        Write-Journal -Path $p -Journal $j
        $back = Read-Journal -Path $p
        $back.Vms['slate-win-app-00000001'].JobId | Should -Be 123456789012
        $back.Vms['slate-win-app-00000001'].Claimed | Should -BeTrue
        $back.Vms['slate-win-app-00000001'].ForkGeneration | Should -Be 7
        $back.Retries['42'].Count | Should -Be 2
        $back.SeenJobs['42'].Lane | Should -Be 'app'
        (Get-ChildItem $TestDrive -Filter '*.tmp').Count | Should -Be 0
    }
    It 'moves a corrupt journal aside and starts fresh' {
        $p = Join-Path $TestDrive 'corrupt.json'
        '{ "Vms": { "x": ' | Set-Content $p
        $j = Read-Journal -Path $p
        $j.Vms.Count | Should -Be 0
        (Get-ChildItem $TestDrive -Filter 'corrupt.json.corrupt-*').Count | Should -Be 1
        Test-Path $p | Should -BeFalse
    }
    It 'fills in missing top-level keys from an older journal' {
        $p = Join-Path $TestDrive 'old.json'
        '{ "Vms": {} }' | Set-Content $p
        $j = Read-Journal -Path $p
        $j.Retries.Count | Should -Be 0
        $j.SeenJobs.Count | Should -Be 0
    }
    It 'replaces the journal in place on repeated writes' {
        $p = Join-Path $TestDrive 'repeat.json'
        $j = New-Journal
        $j.Retries['1'] = @{ Count = 1; NextAt = '2026-10-10T12:00:00.0000000+00:00' }
        Write-Journal -Path $p -Journal $j
        $j.Retries['1'].Count = 2
        Write-Journal -Path $p -Journal $j
        (Read-Journal -Path $p).Retries['1'].Count | Should -Be 2
        Test-Path "$p.tmp" | Should -BeFalse
    }
    It 'never replaces the journal when the temp write fails' {
        $p = Join-Path $TestDrive 'guarded.json'
        $j = New-Journal
        $j.Retries['1'] = @{ Count = 1; NextAt = '2026-10-10T12:00:00.0000000+00:00' }
        Write-Journal -Path $p -Journal $j
        $before = Get-Content -Raw $p
        # A stale read-only temp file makes the real Set-Content fail. The
        # caller runs in its own runspace with default error handling (no
        # try/catch, preference Continue): inside Pester every call is under
        # a try/catch, and the runner's global Stop would hide the bug.
        'stale' | Set-Content "$p.tmp"
        Set-ItemProperty -LiteralPath "$p.tmp" -Name IsReadOnly -Value $true
        $j.Retries['1'].Count = 2
        $ps = [powershell]::Create()
        try {
            $null = $ps.AddScript({
                param($module, $path, $journal)
                Import-Module $module -Force
                Write-Journal -Path $path -Journal $journal
            }).AddArgument((Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1')).AddArgument($p).AddArgument($j)
            { $null = $ps.Invoke() } | Should -Throw
        } finally { $ps.Dispose() }
        Get-Content -Raw $p | Should -Be $before
    }
}

Describe 'Write-CiLog' {
    It 'appends a timestamped line and creates the file' {
        $p = Join-Path $TestDrive 'logs' 'o.log'
        Write-CiLog -Path $p -Level 'info' -Message 'hello'
        Write-CiLog -Path $p -Level 'warn' -Message 'again'
        $lines = Get-Content $p
        $lines.Count | Should -Be 2
        $lines[0] | Should -Match '^\d{4}-\d{2}-\d{2}T[0-9:.]+(\+|-)\d{2}:\d{2} \[info\] hello$'
        $lines[1] | Should -Match '\[warn\] again$'
    }
}
