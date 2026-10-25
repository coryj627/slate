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

Describe 'Select-HostRunnerJobs' {
    It 'lists the in-progress jobs on the given runners from the whole listing, lane label or not, matching runner names exactly' {
        $jobs = @(
            [pscustomobject]@{ id = 1; run_id = 10; status = 'in_progress'; labels = @('slate-win-app'); runner_name = 'slate-win-app-aaaaaaaa' },
            [pscustomobject]@{ id = 2; run_id = 11; status = 'in_progress'; labels = @('self-hosted'); runner_name = 'slate-win-model-bbbbbbbb' },
            [pscustomobject]@{ id = 3; run_id = 12; status = 'queued'; labels = @('slate-win-app'); runner_name = $null },
            [pscustomobject]@{ id = 4; run_id = 13; status = 'in_progress'; labels = @('slate-win-app'); runner_name = 'SLATE-WIN-APP-AAAAAAAA' },
            [pscustomobject]@{ id = 5; run_id = 14; status = 'in_progress'; labels = @('slate-win-app'); runner_name = 'slate-win-app-cccccccc' },
            [pscustomobject]@{ id = 6; run_id = 15; status = 'in_progress'; runner_name = 'slate-win-rust-dddddddd' },
            [pscustomobject]@{ id = 7; run_id = 16; status = 'in_progress'; labels = @('slate-win-app', 'slate-win-rust'); runner_name = 'slate-win-rust-dddddddd' },
            [pscustomobject]@{ id = 8; run_id = 17; status = 'in_progress'; labels = @('ubuntu-latest'); runner_name = 'GitHub Actions 3' }
        )
        $result = @(Select-HostRunnerJobs -Jobs $jobs -RunnerNames @('slate-win-app-aaaaaaaa', 'slate-win-model-bbbbbbbb', 'slate-win-rust-dddddddd', 'GitHub Actions 3'))
        @($result | ForEach-Object { '{0}/{1}/{2}/{3}' -f $_.JobId, $_.RunId, $_.Lane, $_.RunnerName }) |
            Should -Be @('1/10/app/slate-win-app-aaaaaaaa', '2/11//slate-win-model-bbbbbbbb', '6/15//slate-win-rust-dddddddd', '7/16//slate-win-rust-dddddddd')
    }
    It 'returns an empty array for no jobs or no runners' {
        @(Select-HostRunnerJobs -Jobs @() -RunnerNames @('slate-win-app-aaaaaaaa')).Count | Should -Be 0
        @(Select-HostRunnerJobs -Jobs @([pscustomobject]@{ id = 1; run_id = 10; status = 'in_progress'; labels = @('slate-win-app'); runner_name = 'slate-win-app-aaaaaaaa' }) -RunnerNames @()).Count | Should -Be 0
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
    It 'includes online runners only when asked, never an active VM''s runner' {
        # The startup sweep asks: every VM it does not keep is about to go,
        # and GitHub still reports such a VM's runner online for a while.
        $runners = @(
            [pscustomobject]@{ id = 1; name = 'slate-win-app-aaaaaaaa'; status = 'online' },
            [pscustomobject]@{ id = 2; name = 'slate-win-app-bbbbbbbb'; status = 'offline' },
            [pscustomobject]@{ id = 3; name = 'slate-win-app-kept0000'; status = 'online' },
            [pscustomobject]@{ id = 4; name = 'other-runner'; status = 'online' }
        )
        @(Get-StaleRunnerNames -Runners $runners -ActiveNames @('slate-win-app-kept0000')) | Should -Be @('slate-win-app-bbbbbbbb')
        @(Get-StaleRunnerNames -Runners $runners -ActiveNames @('slate-win-app-kept0000') -IncludeOnline) | Should -Be @('slate-win-app-aaaaaaaa', 'slate-win-app-bbbbbbbb')
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
    It 'compares the runner name ordinally, as the commit predicate does' {
        # GitHub echoes the registered name byte for byte; a name that differs
        # in case is a different runner, never a match.
        $jobs = @{ 1 = [pscustomobject]@{ id = 1; runner_name = 'SLATE-WIN-APP-DEADBEEF' } }
        Resolve-RunnerJob -RunnerName 'slate-win-app-deadbeef' -AdmittedJobId 1 -CandidateJobIds @() -GetJob { param($id) $jobs[[int]$id] } | Should -BeNullOrEmpty
    }
}

Describe 'Test-IPv4InPrefix' {
    It 'accepts the gateway inside the NAT prefix' {
        Test-IPv4InPrefix -Address '10.77.0.1' -Prefix '10.77.0.0/24' | Should -BeTrue
    }
    It 'rejects an address in a neighbouring subnet' {
        Test-IPv4InPrefix -Address '10.77.1.1' -Prefix '10.77.0.0/24' | Should -BeFalse
    }
    It 'follows the prefix length rather than a fixed /24' {
        Test-IPv4InPrefix -Address '10.0.200.1' -Prefix '10.0.0.0/16' | Should -BeTrue
        Test-IPv4InPrefix -Address '10.77.0.200' -Prefix '10.77.0.0/25' | Should -BeFalse
        Test-IPv4InPrefix -Address '10.77.0.1' -Prefix '10.77.0.1/32' | Should -BeTrue
        Test-IPv4InPrefix -Address '10.77.0.2' -Prefix '10.77.0.1/32' | Should -BeFalse
        Test-IPv4InPrefix -Address '192.0.2.1' -Prefix '0.0.0.0/0' | Should -BeTrue
    }
    It 'throws on malformed input instead of answering' {
        { Test-IPv4InPrefix -Address '10.77.0.1' -Prefix '10.77.0.0' } | Should -Throw
        { Test-IPv4InPrefix -Address '10.77.0.1' -Prefix '10.77.0.0/33' } | Should -Throw
        { Test-IPv4InPrefix -Address '10.77.0.1' -Prefix '10.77.0/24' } | Should -Throw
        { Test-IPv4InPrefix -Address 'fe80::1' -Prefix '10.77.0.0/24' } | Should -Throw
        { Test-IPv4InPrefix -Address 'gateway' -Prefix '10.77.0.0/24' } | Should -Throw
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
        @($cfg.PolicyOnlyActors).Count | Should -Be 1
        $cfg.PolicyOnlyActors[0]['Login'] | Should -BeExactly 'dependabot[bot]'
        $cfg.PolicyOnlyActors[0]['Id'] | Should -Be 49699333
        $cfg.PolicyOnlyActors[0]['HeadPrefix'] | Should -BeExactly 'dependabot/'
        @($cfg.RoutedWorkflows) | Should -Be @('.github/workflows/windows.yml', '.github/workflows/nightly.yml', '.github/workflows/windows-ci-pilot.yml')
    }
    It 'requires RoutedWorkflows as a non-empty array of workflow paths' {
        # Discovery reads the jobs of these workflows' runs only: an empty or
        # malformed list would quietly admit nothing at all.
        $p = Join-Path $TestDrive 'routed.json'
        $raw = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        $raw.Remove('RoutedWorkflows')
        $raw | ConvertTo-Json -Depth 6 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw "*missing required key 'RoutedWorkflows'*"
        # Four bad values: an empty array, a bare string, an empty path, a number.
        foreach ($bad in @(@(), '.github/workflows/windows.yml', @(''), @(7))) {
            $raw['RoutedWorkflows'] = $bad
            $raw | ConvertTo-Json -Depth 6 | Set-Content $p
            { Get-CiHostConfig -Path $p } | Should -Throw '*RoutedWorkflows must be a non-empty array of workflow paths*'
        }
    }
    It 'requires PolicyOnlyActors, so a missing or emptied list cannot turn the check off' {
        $p = Join-Path $TestDrive 'policyonly.json'
        $raw = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        $raw.Remove('PolicyOnlyActors')
        $raw | ConvertTo-Json -Depth 6 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*PolicyOnlyActors must be a non-empty array of { Id, Login, HeadPrefix } actors*'
        foreach ($bad in @(@(), 'dependabot[bot]', @(@{ Login = 'dependabot[bot]'; HeadPrefix = 'dependabot/' }), @(@{ Id = 1; Login = ''; HeadPrefix = 'd/' }),
                @(@{ Id = 'x'; Login = 'a'; HeadPrefix = 'd/' }), @(@{ Id = 1; Login = 'a' }), @(@{ Id = 1; Login = 'a'; HeadPrefix = '' }))) {
            $raw['PolicyOnlyActors'] = $bad
            $raw | ConvertTo-Json -Depth 6 | Set-Content $p
            { Get-CiHostConfig -Path $p } | Should -Throw '*PolicyOnlyActors must be a non-empty array of { Id, Login, HeadPrefix } actors*'
        }
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
    It 'derives the prefix length and requires the gateway and every slot inside NatPrefix' {
        # setup-host.ps1 creates the subnet from this file and the guests get
        # its length as slate.prefix: one source of truth, checked on load.
        $cfg = Get-CiHostConfig -Path (Join-Path $PSScriptRoot '..' 'config.json')
        $cfg.NatPrefix | Should -Be '10.77.0.0/24'
        $cfg.PrefixLength | Should -Be 24
        $p = Join-Path $TestDrive 'nat.json'
        $raw = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        foreach ($bad in '10.77.0.0', '10.77.0.0/0', '10.77.0.0/33', '10.77.0/24') {
            $raw['NatPrefix'] = $bad
            $raw | ConvertTo-Json -Depth 6 | Set-Content $p
            { Get-CiHostConfig -Path $p } | Should -Throw '*NatPrefix*' -Because $bad
        }
        $raw['NatPrefix'] = '10.77.0.0/24'
        $raw['Gateway'] = '10.77.1.1'
        $raw | ConvertTo-Json -Depth 6 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*Gateway 10.77.1.1 is outside NatPrefix 10.77.0.0/24*'
        $raw['Gateway'] = '10.77.0.1'
        $raw['Slots'][1]['Ip'] = '10.77.2.12'
        $raw | ConvertTo-Json -Depth 6 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*slot 2 address 10.77.2.12 is outside NatPrefix 10.77.0.0/24*'
        $raw['Slots'][1]['Ip'] = '10.77.0.1'
        $raw | ConvertTo-Json -Depth 6 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*slot 2 uses the gateway address*'
        $raw['Slots'][1]['Ip'] = '10.77.0.12'
        $raw.Remove('NatPrefix')
        $raw | ConvertTo-Json -Depth 6 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw "*missing required key 'NatPrefix'*"
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
    It 'defaults LogRetentionDays to 30 when the file predates the key, and rejects a bad value' {
        $cfg = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        $cfg.Remove('LogRetentionDays')
        $p = Join-Path $TestDrive 'old.json'; $cfg | ConvertTo-Json -Depth 5 | Set-Content $p
        (Get-CiHostConfig -Path $p).LogRetentionDays | Should -Be 30
        $cfg['LogRetentionDays'] = 0
        $cfg | ConvertTo-Json -Depth 5 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*LogRetentionDays*'
    }
    It 'defaults the breaker to 3 failures and a 900 s cool-down when the file predates the keys, and rejects bad values' {
        $cfg = Get-Content -Raw (Join-Path $PSScriptRoot '..' 'config.json') | ConvertFrom-Json -AsHashtable
        $cfg.Remove('BreakerFailures'); $cfg.Remove('BreakerCooldownSeconds')
        $p = Join-Path $TestDrive 'old-breaker.json'; $cfg | ConvertTo-Json -Depth 5 | Set-Content $p
        $loaded = Get-CiHostConfig -Path $p
        $loaded.BreakerFailures | Should -Be 3
        $loaded.BreakerCooldownSeconds | Should -Be 900
        $cfg['BreakerFailures'] = 0
        $cfg | ConvertTo-Json -Depth 5 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*BreakerFailures*'
        $cfg['BreakerFailures'] = 3; $cfg['BreakerCooldownSeconds'] = 'soon'
        $cfg | ConvertTo-Json -Depth 5 | Set-Content $p
        { Get-CiHostConfig -Path $p } | Should -Throw '*BreakerCooldownSeconds*'
    }
}

Describe 'Journal' {
    It 'New-Journal and Read-Journal share one set of top-level keys' {
        $p = Join-Path $TestDrive 'bare.json'
        '{"Vms":{},"Retries":{},"SeenJobs":{}}' | Set-Content $p
        @((New-Journal).Keys | Sort-Object) | Should -Be @('GoldenAccepted', 'Health', 'LogsPrunedOn', 'PublishFailedAt', 'PublishedAt', 'PublishedState', 'Retries', 'SeenJobs', 'Vms' | Sort-Object)
        @((Read-Journal -Path $p).Keys | Sort-Object) | Should -Be @((New-Journal).Keys | Sort-Object)
        (Read-Journal -Path $p).PublishedState | Should -Be $null
    }
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

Describe 'Get-PublishedState' {
    BeforeAll {
        $script:cfg = Get-CiHostConfig -Path (Join-Path $PSScriptRoot '..' 'config.json')
        $script:now = [datetimeoffset]::Parse('2026-10-11T15:04:05Z')
    }
    It 'reports an idle pool' {
        $s = Get-PublishedState -Config $cfg -Journal (New-Journal) -Queued @() -Now $now
        $s.v | Should -Be 1
        $s.at | Should -Be '2026-10-11T15:04:05Z'
        $s.serving | Should -BeTrue
        $s.slots | Should -Be 2
        @($s.running).Count | Should -Be 0
        @($s.queued).Count | Should -Be 0
        ($s | ConvertTo-Json -Compress -Depth 5) | Should -Be '{"v":1,"at":"2026-10-11T15:04:05Z","serving":true,"slots":2,"running":[],"queued":[]}'
    }
    It 'lists every VM that holds a slot with its provisioning time, skips a pending commit, and counts a VM in teardown against the slots' {
        $j = New-Journal
        $j.Vms['a'] = @{ Name = 'a'; Lane = 'app'; Phase = 'handed'; StartedAt = '2026-10-11T14:48:51+00:00'; PendingCommit = $false }
        $j.Vms['b'] = @{ Name = 'b'; Lane = 'model'; Phase = 'handed'; StartedAt = '2026-10-11T14:50:00Z'; PendingCommit = $true }
        $j.Vms['c'] = @{ Name = 'c'; Lane = 'rust'; Phase = 'discarding'; StartedAt = '2026-10-11T14:00:00Z'; PendingCommit = $false }
        $s = Get-PublishedState -Config $cfg -Journal $j -Queued @() -Now $now
        $s.slots | Should -Be 1
        $s.serving | Should -BeTrue
        @($s.running | ForEach-Object { "$($_.lane)@$($_.since)" }) | Should -Be @('app@2026-10-11T14:48:51Z')
    }
    It 'is not serving when every slot is held by a teardown' {
        $j = New-Journal
        $j.Vms['c'] = @{ Name = 'c'; Lane = 'rust'; Phase = 'discarding'; StartedAt = '2026-10-11T14:00:00Z'; PendingCommit = $false }
        $j.Vms['d'] = @{ Name = 'd'; Lane = 'app'; Phase = 'discarding'; StartedAt = '2026-10-11T14:00:00Z'; PendingCommit = $false }
        $s = Get-PublishedState -Config $cfg -Journal $j -Queued @() -Now $now
        $s.slots | Should -Be 0
        $s.serving | Should -BeFalse
    }
    It 'lists the queue oldest first with creation times' {
        $queued = @([pscustomobject]@{ JobId = 2; Lane = 'model'; CreatedAt = [datetimeoffset]::Parse('2026-10-11T14:40:00Z') },
                    [pscustomobject]@{ JobId = 1; Lane = 'rust'; CreatedAt = [datetimeoffset]::Parse('2026-10-11T14:39:00Z') })
        $s = Get-PublishedState -Config $cfg -Journal (New-Journal) -Queued $queued -Now $now
        @($s.queued | ForEach-Object { "$($_.lane)@$($_.since)" }) | Should -Be @('rust@2026-10-11T14:39:00Z', 'model@2026-10-11T14:40:00Z')
    }
    It 'orders running by instant, never by string, whatever type the journal holds the time in' {
        # A [datetime] (here of Kind Local) prints as 10/11/2026 …, which
        # sorts before any ISO string of the same year as text.
        $j = New-Journal
        $j.Vms['a'] = @{ Name = 'a'; Lane = 'app'; Phase = 'handed'; StartedAt = '2026-10-11T14:40:00Z'; PendingCommit = $false }
        $j.Vms['b'] = @{ Name = 'b'; Lane = 'model'; Phase = 'handed'; StartedAt = [datetimeoffset]::Parse('2026-10-11T14:48:51Z').LocalDateTime; PendingCommit = $false }
        $s = Get-PublishedState -Config $cfg -Journal $j -Queued @() -Now $now
        @($s.running | ForEach-Object { "$($_.lane)@$($_.since)" }) | Should -Be @('app@2026-10-11T14:40:00Z', 'model@2026-10-11T14:48:51Z')
    }
    It 'publishes the same state from a journal written and read back' {
        # The round trip turns every time into a [datetime]; across a year
        # boundary their text order is not their order in time.
        $p = Join-Path $TestDrive 'published.json'
        $j = New-Journal
        $j.Vms['a'] = @{ Name = 'a'; Lane = 'app'; Phase = 'handed'; StartedAt = '2025-12-31T23:59:00Z'; PendingCommit = $false }
        $j.Vms['b'] = @{ Name = 'b'; Lane = 'model'; Phase = 'provisioned'; StartedAt = '2026-01-01T00:01:00+00:00'; PendingCommit = $false }
        $j.Vms['c'] = @{ Name = 'c'; Lane = 'rust'; Phase = 'handed'; StartedAt = '2025-12-31T23:00:00Z'; PendingCommit = $true }
        $j.Vms['d'] = @{ Name = 'd'; Lane = 'shell'; Phase = 'discarding'; StartedAt = '2025-12-31T22:00:00Z'; PendingCommit = $false }
        $expected = '{"v":1,"at":"2026-10-11T15:04:05Z","serving":true,"slots":1,"running":[{"lane":"app","since":"2025-12-31T23:59:00Z"},{"lane":"model","since":"2026-01-01T00:01:00Z"}],"queued":[]}'
        (Get-PublishedState -Config $cfg -Journal $j -Queued @() -Now $now | ConvertTo-Json -Compress -Depth 5) | Should -Be $expected
        Write-Journal -Path $p -Journal $j
        $back = Read-Journal -Path $p
        $back.Vms['a'].StartedAt | Should -BeOfType [datetime]
        (Get-PublishedState -Config $cfg -Journal $back -Queued @() -Now $now | ConvertTo-Json -Compress -Depth 5) | Should -Be $expected
    }
    It 'writes every time in the invariant form whatever the culture of the thread (<Culture>)' -ForEach @(@{ Culture = 'th-TH' }, @{ Culture = 'fi-FI' }) {
        # th-TH counts years in the Buddhist era (2569); fi-FI separates the
        # time with dots. Either would publish a state the router cannot read.
        try { $info = [cultureinfo]::GetCultureInfo($Culture) }
        catch { Set-ItResult -Skipped -Because "culture $Culture is not available on this host ($($_.Exception.Message))"; return }
        $thread = [System.Threading.Thread]::CurrentThread
        $before = $thread.CurrentCulture
        $beforeUi = $thread.CurrentUICulture
        try {
            $thread.CurrentCulture = $info
            $thread.CurrentUICulture = $info
            # ConvertTo-StateTime is private: call it inside the module this
            # file imported (InModuleScope is ambiguous when another test file
            # has loaded a second copy).
            $module = (Get-Command Get-PublishedState).Module
            & $module { param($At) ConvertTo-StateTime -Value $At } $now | Should -BeExactly '2026-10-11T15:04:05Z'
            $j = New-Journal
            $j.Vms['a'] = @{ Name = 'a'; Lane = 'app'; Phase = 'handed'; StartedAt = '2026-10-11T14:48:51Z'; PendingCommit = $false }
            $queued = @([pscustomobject]@{ JobId = 1; Lane = 'model'; CreatedAt = [datetimeoffset]::Parse('2026-10-11T14:40:00Z') })
            (Get-PublishedState -Config $cfg -Journal $j -Queued $queued -Now $now | ConvertTo-Json -Compress -Depth 5) |
                Should -BeExactly '{"v":1,"at":"2026-10-11T15:04:05Z","serving":true,"slots":2,"running":[{"lane":"app","since":"2026-10-11T14:48:51Z"}],"queued":[{"lane":"model","since":"2026-10-11T14:40:00Z"}]}'
        } finally {
            $thread.CurrentCulture = $before
            $thread.CurrentUICulture = $beforeUi
        }
    }
}

Describe 'Test-TrustedRun' {
    BeforeAll {
        # The entries the host loads, so the tests and the validated config share one shape.
        $script:policyOnly = @((Get-CiHostConfig -Path (Join-Path $PSScriptRoot '..' 'config.json')).PolicyOnlyActors)
    }
    It 'is <Expected> for <Case>' -ForEach @(
        @{ Case = 'the trusted repository'; Expected = $true; Run = [pscustomobject]@{ head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } } }
        @{ Case = 'the trusted repository in another case'; Expected = $true; Run = [pscustomobject]@{ head_repository = [pscustomobject]@{ full_name = 'CoryJ627/SLATE' } } }
        @{ Case = 'no run'; Expected = $false; Run = $null }
        @{ Case = 'a run without a head_repository property'; Expected = $false; Run = [pscustomobject]@{ id = 10 } }
        @{ Case = 'a null head repository (a deleted fork)'; Expected = $false; Run = [pscustomobject]@{ head_repository = $null } }
        @{ Case = 'a head repository without a full_name property'; Expected = $false; Run = [pscustomobject]@{ head_repository = [pscustomobject]@{ id = 5 } } }
        @{ Case = 'a lookalike name'; Expected = $false; Run = [pscustomobject]@{ head_repository = [pscustomobject]@{ full_name = 'coryj627/slate-evil' } } }
        @{ Case = 'a fork of the same name'; Expected = $false; Run = [pscustomobject]@{ head_repository = [pscustomobject]@{ full_name = 'someone/slate' } } }
    ) {
        Test-TrustedRun -Run $Run -TrustedRepo 'coryj627/slate' | Should -Be $Expected
    }
    # Plan 42 §3.7: the policy lets a policy-only actor's runs start, so the
    # host refuses them, by login or by id, started or re-run by the owner.
    It 'is <Expected> with policy-only actors for <Case>' -ForEach @(
        @{ Case = 'the owner''s run'; Expected = $true; Actor = @{ login = 'coryj627'; id = 933688 }; Trigger = @{ login = 'coryj627'; id = 933688 } }
        @{ Case = 'a run by a policy-only actor'; Expected = $false; Actor = @{ login = 'dependabot[bot]'; id = 49699333 }; Trigger = @{ login = 'dependabot[bot]'; id = 49699333 } }
        @{ Case = 'a policy-only actor''s run re-run by the owner'; Expected = $false; Actor = @{ login = 'dependabot[bot]'; id = 49699333 }; Trigger = @{ login = 'coryj627'; id = 933688 } }
        @{ Case = 'a run re-run by a policy-only actor'; Expected = $false; Actor = @{ login = 'coryj627'; id = 933688 }; Trigger = @{ login = 'dependabot[bot]'; id = 49699333 } }
        @{ Case = 'a policy-only actor matched by id under another login'; Expected = $false; Actor = @{ login = 'renamed[bot]'; id = 49699333 }; Trigger = @{ login = 'renamed[bot]'; id = 49699333 } }
        @{ Case = 'a policy-only login in another case'; Expected = $false; Actor = @{ login = 'Dependabot[BOT]'; id = 1 }; Trigger = @{ login = 'Dependabot[BOT]'; id = 1 } }
        @{ Case = 'a run that names no actor'; Expected = $false; Actor = $null; Trigger = $null }
        @{ Case = 'a run by any bot, listed or not'; Expected = $false; Actor = @{ login = 'renovate[bot]'; id = 29139614; type = 'Bot' }; Trigger = @{ login = 'renovate[bot]'; id = 29139614; type = 'Bot' } }
        @{ Case = 'the owner''s update of a Dependabot branch'; Expected = $false; Branch = 'dependabot/nuget/apps/slate-windows/nuget-0838e9d5cd'; Actor = @{ login = 'coryj627'; id = 933688; type = 'User' }; Trigger = @{ login = 'coryj627'; id = 933688; type = 'User' } }
        @{ Case = 'the owner''s own branch'; Expected = $true; Branch = 'claude/xb-12-dependabot-vendor-only'; Actor = @{ login = 'coryj627'; id = 933688; type = 'User' }; Trigger = @{ login = 'coryj627'; id = 933688; type = 'User' } }
    ) {
        $run = [pscustomobject]@{ head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' } }
        if ($Actor) { $run | Add-Member -NotePropertyName actor -NotePropertyValue ([pscustomobject]$Actor) }
        if ($Trigger) { $run | Add-Member -NotePropertyName triggering_actor -NotePropertyValue ([pscustomobject]$Trigger) }
        if ($Branch) { $run | Add-Member -NotePropertyName head_branch -NotePropertyValue $Branch }
        Test-TrustedRun -Run $run -TrustedRepo 'coryj627/slate' -PolicyOnlyActors $policyOnly | Should -Be $Expected
    }
    It 'refuses a Bot actor even with no policy-only actors configured' {
        $run = [pscustomobject]@{ head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' }; actor = [pscustomobject]@{ login = 'renovate[bot]'; id = 1; type = 'Bot' }; triggering_actor = [pscustomobject]@{ login = 'renovate[bot]'; id = 1; type = 'Bot' } }
        Test-TrustedRun -Run $run -TrustedRepo 'coryj627/slate' | Should -BeFalse
    }
    It 'names why it refuses a run' {
        $owner = [pscustomobject]@{ login = 'coryj627'; id = 933688; type = 'User' }
        $bot = [pscustomobject]@{ login = 'dependabot[bot]'; id = 49699333; type = 'Bot' }
        $fork = [pscustomobject]@{ head_repository = [pscustomobject]@{ full_name = 'someone/slate' }; actor = $owner; triggering_actor = $owner }
        Get-RunRefusal -Run $fork -TrustedRepo 'coryj627/slate' -PolicyOnlyActors $policyOnly | Should -BeExactly 'not the trusted repository'
        $byBot = [pscustomobject]@{ head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' }; actor = $bot; triggering_actor = $bot }
        Get-RunRefusal -Run $byBot -TrustedRepo 'coryj627/slate' -PolicyOnlyActors $policyOnly | Should -BeExactly 'a run by dependabot[bot], a bot this host never admits'
        $ofBranch = [pscustomobject]@{ head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' }; head_branch = 'dependabot/cargo/x'; actor = $owner; triggering_actor = $owner }
        Get-RunRefusal -Run $ofBranch -TrustedRepo 'coryj627/slate' -PolicyOnlyActors $policyOnly | Should -BeExactly "a run of dependabot/cargo/x, a policy-only actor's branch this host never admits"
        $mine = [pscustomobject]@{ head_repository = [pscustomobject]@{ full_name = 'coryj627/slate' }; head_branch = 'main'; actor = $owner; triggering_actor = $owner }
        Get-RunRefusal -Run $mine -TrustedRepo 'coryj627/slate' -PolicyOnlyActors $policyOnly | Should -BeExactly ''
    }
    It 'refuses every policy-only actor the allow-list names, with its branch prefix' {
        # The routers read ci/mac-runner/allowlist.json, this host its own config: one list must cover the other.
        $allow = Get-Content -Raw (Join-Path $PSScriptRoot '..' '..' 'mac-runner' 'allowlist.json') | ConvertFrom-Json -AsHashtable
        $cfg = Get-CiHostConfig -Path (Join-Path $PSScriptRoot '..' 'config.json')
        foreach ($entry in @($allow['policy_only_actors'])) {
            $match = @($cfg.PolicyOnlyActors | Where-Object { [int64]$_['Id'] -eq [int64]$entry['id'] })
            $match.Count | Should -Be 1 -Because "config.json must refuse $($entry['login'])"
            $match[0]['Login'] | Should -BeExactly $entry['login']
            $match[0]['HeadPrefix'] | Should -BeExactly $entry['head_prefix']
        }
    }
    It 'refuses another repository whatever the actor' {
        $run = [pscustomobject]@{ head_repository = [pscustomobject]@{ full_name = 'someone/slate' }; actor = [pscustomobject]@{ login = 'coryj627'; id = 933688 }; triggering_actor = [pscustomobject]@{ login = 'coryj627'; id = 933688 } }
        Test-TrustedRun -Run $run -TrustedRepo 'coryj627/slate' -PolicyOnlyActors $policyOnly | Should -BeFalse
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

Describe 'Get-ExpiredLogNames' {
    It 'selects dated log names older than the retention, by the date in the name' {
        $names = 'orchestrator-2026-09-10.log', 'orchestrator-2026-09-11.log', 'orchestrator-2026-09-12.log', 'orchestrator-2026-10-11.log'
        @(Get-ExpiredLogNames -Names $names -Today '2026-10-11' -RetentionDays 30) | Should -Be @('orchestrator-2026-09-10.log')
    }
    It 'keeps today, names that do not parse and other files' {
        $names = 'orchestrator-2026-10-11.log', 'orchestrator-yesterday.log', 'notes.txt', 'orchestrator-2026-13-40.log'
        @(Get-ExpiredLogNames -Names $names -Today '2026-10-11' -RetentionDays 1).Count | Should -Be 0
    }
    It 'returns nothing for an empty directory' {
        @(Get-ExpiredLogNames -Names @() -Today '2026-10-11' -RetentionDays 30).Count | Should -Be 0
    }
}

Describe 'Get-OrchestratorLogName and Get-OrchestratorLogDate' {
    BeforeAll {
        # Mid-June: no common zone changes its offset within 40 days of it, so
        # the absolute days added below are the local days they look like.
        $script:at = [datetimeoffset]::Parse('2026-06-10T23:30:00Z', [cultureinfo]::InvariantCulture)
    }
    It 'name the daily file by the local date, in the invariant culture' {
        $day = $at.ToLocalTime().ToString('yyyy-MM-dd', [cultureinfo]::InvariantCulture)
        Get-OrchestratorLogDate -Now $at | Should -BeExactly $day
        Get-OrchestratorLogName -Now $at | Should -BeExactly "orchestrator-$day.log"
    }
    It 'name the day by the local date, not the UTC one, for <Case>' -ForEach @(
        # East of UTC the first falls on a later local day than its UTC day, west of UTC the second falls on an
        # earlier one: between them the UTC date is wrong in every zone but UTC itself, whatever zone the host is in.
        @{ Case = 'an instant half an hour before UTC midnight'; Utc = '2026-06-10T23:30:00Z' }
        @{ Case = 'an instant half an hour after UTC midnight'; Utc = '2026-06-11T00:30:00Z' }
    ) {
        $now = [datetimeoffset]::Parse($Utc, [cultureinfo]::InvariantCulture)
        $day = $now.ToLocalTime().ToString('yyyy-MM-dd', [cultureinfo]::InvariantCulture)
        Get-OrchestratorLogDate -Now $now | Should -BeExactly $day
        Get-OrchestratorLogName -Now $now | Should -BeExactly "orchestrator-$day.log"
    }
    It 'agree with the reader: the name the writer gives expires after the retention, never before' {
        $name = Get-OrchestratorLogName -Now $at
        @(Get-ExpiredLogNames -Names @($name) -Today (Get-OrchestratorLogDate -Now $at.AddDays(30)) -RetentionDays 30).Count | Should -Be 0
        @(Get-ExpiredLogNames -Names @($name) -Today (Get-OrchestratorLogDate -Now $at.AddDays(31)) -RetentionDays 30) | Should -Be @($name)
    }
    It 'write a Gregorian year whatever the culture of the thread (th-TH)' {
        # th-TH counts years in the Buddhist era (2569): Get-ExpiredLogNames
        # would read such a name as another date.
        try { $info = [cultureinfo]::GetCultureInfo('th-TH') }
        catch { Set-ItResult -Skipped -Because "culture th-TH is not available on this host ($($_.Exception.Message))"; return }
        $thread = [System.Threading.Thread]::CurrentThread
        $before = $thread.CurrentCulture
        try {
            $thread.CurrentCulture = $info
            Get-OrchestratorLogName -Now $at | Should -Match '^orchestrator-2026-06-1[01]\.log$'
        } finally { $thread.CurrentCulture = $before }
    }
}

Describe 'Test-RecentlyAt' {
    BeforeAll {
        $script:now = [datetimeoffset]::Parse('2026-10-11T12:00:00Z', [cultureinfo]::InvariantCulture)
        # Private: call it inside the module this file imported (InModuleScope
        # is ambiguous when another test file has loaded a second copy).
        $script:module = (Get-Command Get-PublishedState).Module
        function Test-Recently($Value) { & $script:module { param($v, $n) Test-RecentlyAt -Value $v -Now $n -WithinSeconds 60 } $Value $script:now }
    }
    It 'is <Expected> for <Case>' -ForEach @(
        @{ Case = 'null'; Value = $null; Expected = $false }
        @{ Case = 'an empty string'; Value = ''; Expected = $false }
        @{ Case = 'a value that does not parse'; Value = 'garbage'; Expected = $false }
        @{ Case = '59 s ago'; Value = '2026-10-11T11:59:01.0000000+00:00'; Expected = $true }
        @{ Case = '60 s ago'; Value = '2026-10-11T11:59:00.0000000+00:00'; Expected = $false }
        @{ Case = '61 s ago'; Value = '2026-10-11T11:58:59.0000000+00:00'; Expected = $false }
    ) {
        Test-Recently $Value | Should -Be $Expected
    }
    It 'reads a journal round trip''s [datetime] as the instant it was' {
        Test-Recently ([datetime]::SpecifyKind([datetime]'2026-10-11T11:59:30', [System.DateTimeKind]::Utc)) | Should -BeTrue
        Test-Recently ([datetime]::SpecifyKind([datetime]'2026-10-11T11:58:30', [System.DateTimeKind]::Utc)) | Should -BeFalse
    }
}

Describe 'Test-GoldenPrevRemovable' {
    BeforeAll { $script:accepted = @{ Golden = '2026-10-10T22:55:00.0000000Z'; Lanes = @('rust', 'app', 'model', 'shell'); Complete = $true } }
    It 'is true only for a complete acceptance of the current, sealed golden with no older VM' {
        Test-GoldenPrevRemovable -Accepted $accepted -CurrentStamp '2026-10-10T22:55:00.0000000Z' -CurrentSealed $true -VmCreationTimes @('2026-10-11T01:00:00Z') | Should -BeTrue
    }
    It 'compares the stamps as instants, so a journal round trip (a [datetime]) still matches' {
        $roundTripped = @{ Golden = [datetime]::Parse('2026-10-10T22:55:00Z').ToUniversalTime(); Lanes = $accepted.Lanes; Complete = $true }
        Test-GoldenPrevRemovable -Accepted $roundTripped -CurrentStamp '2026-10-10T22:55:00.0000000Z' -CurrentSealed $true -VmCreationTimes @() | Should -BeTrue
    }
    It 'is false for an acceptance of another image (the previous one, after a rotation)' {
        Test-GoldenPrevRemovable -Accepted $accepted -CurrentStamp '2026-10-12T01:00:00Z' -CurrentSealed $true -VmCreationTimes @() | Should -BeFalse
    }
    It 'is false while the current golden is not sealed, for a missing lane, for a VM older than the golden, and without an acceptance' {
        Test-GoldenPrevRemovable -Accepted $accepted -CurrentStamp '2026-10-10T22:55:00.0000000Z' -CurrentSealed $false -VmCreationTimes @() | Should -BeFalse
        Test-GoldenPrevRemovable -Accepted @{ Golden = $accepted.Golden; Lanes = @('rust', 'app', 'model'); Complete = $false } -CurrentStamp $accepted.Golden -CurrentSealed $true -VmCreationTimes @() | Should -BeFalse
        Test-GoldenPrevRemovable -Accepted $accepted -CurrentStamp $accepted.Golden -CurrentSealed $true -VmCreationTimes @('2026-10-10T22:00:00Z') | Should -BeFalse
        Test-GoldenPrevRemovable -Accepted $null -CurrentStamp $accepted.Golden -CurrentSealed $true -VmCreationTimes @() | Should -BeFalse
    }
}

Describe 'Get-CiHostPaths and Read-JournalSnapshot' {
    It 'derives every path of the tree from the root, and Get-CiHostConfig uses them' {
        $paths = Get-CiHostPaths -Root 'C:\slate-ci'
        $paths.GoldenPath | Should -Be 'C:\slate-ci\golden\win11-runner.vhdx'
        $paths.GoldenPrevPath | Should -Be 'C:\slate-ci\golden\win11-runner.prev.vhdx'
        $paths.JournalPath | Should -Be 'C:\slate-ci\state\journal.json'
        $paths.LogDir | Should -Be 'C:\slate-ci\logs'
        $cfg = Get-CiHostConfig -Path (Join-Path $PSScriptRoot '..' 'config.json')
        foreach ($key in 'GoldenPath', 'GoldenPrevPath', 'CacheDir', 'VmDir', 'StateDir', 'LogDir', 'JournalPath') { $cfg[$key] | Should -Be $paths[$key] }
    }
    It 'a snapshot reads a journal without moving anything, and reads a missing or broken file as a new journal' {
        $p = Join-Path $TestDrive 'j.json'
        $j = New-Journal; $j.GoldenAccepted = @{ Golden = '2026-10-10T22:55:00Z'; Lanes = @('rust'); Complete = $false }
        Write-Journal -Path $p -Journal $j
        (Read-JournalSnapshot -Path $p).GoldenAccepted.Lanes | Should -Be @('rust')
        'not json' | Set-Content $p
        (Read-JournalSnapshot -Path $p).GoldenAccepted | Should -Be $null
        Test-Path $p | Should -BeTrue -Because 'a snapshot never moves the loop''s journal aside'
        @(Get-ChildItem $TestDrive -Filter 'j.json.corrupt-*').Count | Should -Be 0
        (Read-JournalSnapshot -Path (Join-Path $TestDrive 'none.json')).Vms.Count | Should -Be 0
    }
    It 'Remove-SealedFile deletes a read-only file and tolerates a missing one' {
        $f = Join-Path $TestDrive 'sealed.bin'; 'x' | Set-Content $f
        Set-ItemProperty -LiteralPath $f -Name IsReadOnly -Value $true
        Remove-SealedFile -Path $f
        Test-Path $f | Should -BeFalse
        { Remove-SealedFile -Path $f } | Should -Not -Throw
    }
}
