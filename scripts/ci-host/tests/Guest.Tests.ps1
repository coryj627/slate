# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force
    $script:guestDir = Join-Path $PSScriptRoot '..' 'golden' 'guest'
    function Test-ScriptParses([string]$Path) {
        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$errors)
        return (@($errors).Count -eq 0)
    }
}

Describe 'Select-SlateKvpItems' {
    It 'keeps only slate.* properties as strings' {
        $props = [pscustomobject]@{ PSPath = 'x'; PSProvider = 'Registry'; 'slate.jit.count' = 2; 'slate.jit.0' = 'ab'; 'slate.jit.1' = 'cd'; 'slate.lane' = 'app'; OtherKey = 'ignored' }
        $items = Select-SlateKvpItems -Properties $props
        $items.Keys.Count | Should -Be 4
        $items['slate.jit.count'] | Should -BeOfType [string]
        Join-KvpChunks -Items $items | Should -Be 'abcd'
        $items.Contains('OtherKey') | Should -BeFalse
    }
    It 'returns an empty table when nothing matches' {
        (Select-SlateKvpItems -Properties ([pscustomobject]@{ PSPath = 'x' })).Keys.Count | Should -Be 0
    }
    It 'returns an empty table for a key that has no values yet' {
        # Get-ItemProperty outputs nothing for a registry key without values
        # (the External key before the host's items arrive), so the argument is null.
        (Select-SlateKvpItems -Properties (@() | Write-Output)).Keys.Count | Should -Be 0
    }
}

Describe 'guest scripts' {
    It 'parse as PowerShell' {
        Test-ScriptParses (Join-Path $guestDir 'bootstrap-system.ps1') | Should -BeTrue
        Test-ScriptParses (Join-Path $guestDir 'bootstrap-runner.ps1') | Should -BeTrue
    }
    It 'are no-ops without the golden completion marker' {
        foreach ($f in 'bootstrap-system.ps1', 'bootstrap-runner.ps1') {
            (Get-Content -Raw (Join-Path $guestDir $f)) | Should -Match '\.slate-golden-complete'
        }
    }
    It 'system bootstrap reads the External KVP key, labels the cache by volume label and writes the four env lines' {
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-system.ps1')
        $text | Should -Match 'Virtual Machine\\External'
        $text | Should -Match "FileSystemLabel 'slate-cache'"
        foreach ($v in 'NSC_CACHE_PATH', 'CARGO_TARGET_DIR', 'NUGET_PACKAGES', 'SLATE_CACHE_ROOT') { $text | Should -Match $v }
        $text | Should -Match 'Select-SlateKvpItems'
        $text | Should -Match 'Join-KvpChunks'
        $text | Should -Match 'Add-MpPreference -ExclusionPath \$root'
        $text | Should -Match 'C:\\slate-guest\\SlateCiHost\.psm1'
    }
    It 'runner bootstrap never recurses into a junction, launches run.cmd --jitconfig and always shuts down' {
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-runner.ps1')
        $text | Should -Match 'ReparsePoint'
        $text | Should -Match "'--jitconfig'"
        $text | Should -Match 'finally'
        $text | Should -Match 'shutdown\.exe /s /f /t 0'
    }
    It 'runner bootstrap deletes jit.cfg right after reading it and again in finally, never with -Force' {
        # jit.cfg grants runner only R; the delete itself is allowed by the
        # directory's delete-child right, but -Force first clears attributes.
        $code = @((Get-Content -Raw (Join-Path $guestDir 'bootstrap-runner.ps1')) -split "`n" | Where-Object { $_ -notmatch '^\s*#' })
        $deletes = @(0..($code.Count - 1) | Where-Object { $code[$_] -match 'Remove-Item' -and $code[$_] -match 'jit\.cfg' })
        $deletes.Count | Should -Be 2
        foreach ($i in $deletes) { $code[$i] | Should -Not -Match '-Force' }
        $reads = @(0..($code.Count - 1) | Where-Object { $code[$_] -match '\$jit = Get-Content' })
        $reads.Count | Should -Be 1
        $code[$reads[0] + 1] | Should -Match 'Remove-Item .*jit\.cfg'
    }
    It 'runner bootstrap waits for the runner process only, never its whole process tree' {
        # On 5.1 the Start-Process wait switch waits for every descendant, so a
        # process a job leaves behind would hold the VM until the lane cap.
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-runner.ps1')
        $text | Should -Match 'WaitForExit\(\)'
        $text | Should -Not -Match '-Wait'
        # Without a cached handle 5.1 reports no ExitCode once the process is gone.
        $text | Should -Match '\$process\.Handle'
    }
    It 'both scripts force every shutdown so no application can veto it, and log its exit code' {
        foreach ($f in 'bootstrap-system.ps1', 'bootstrap-runner.ps1') {
            $code = @((Get-Content -Raw (Join-Path $guestDir $f)) -split "`n" | Where-Object { $_ -notmatch '^\s*#' })
            $calls = @(0..($code.Count - 1) | Where-Object { $code[$_] -match '^\s*&\s*shutdown\.exe' })
            $calls.Count | Should -BeGreaterThan 0
            foreach ($i in $calls) {
                $code[$i] | Should -Match ' /f '
                $code[$i + 1] | Should -Match 'Write-Log .*\$LASTEXITCODE'
            }
        }
    }
    It 'system bootstrap waits up to 60 s for the network adapter to come up' {
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-system.ps1')
        $text | Should -Match ([regex]::Escape('AddSeconds(60)'))
        $text | Should -Match 'no network adapter came up within 60 s'
    }
    It 'system bootstrap lets runner write the cache and checks the exit code of every icacls call' {
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-system.ps1')
        $text | Should -Match ([regex]::Escape("icacls.exe `$root /grant 'runner:(OI)(CI)M'"))
        $code = @($text -split "`n" | Where-Object { $_ -notmatch '^\s*#' })
        $calls = @(0..($code.Count - 1) | Where-Object { $code[$_] -match 'icacls\.exe' })
        $calls.Count | Should -Be 2
        foreach ($i in $calls) { $code[$i + 1] | Should -Match '\$LASTEXITCODE -ne 0' }
    }
    It 'system bootstrap grants runner the cache root only when the root lacks that grant' {
        # The grant re-walks the whole cache tree; once a trusted commit carries
        # the ACE into the lane parent, later boots must skip it.
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-system.ps1')
        $check = $text.IndexOf('Get-Acl -LiteralPath $root')
        $check | Should -BeGreaterThan -1
        $text.IndexOf("icacls.exe `$root /grant 'runner:(OI)(CI)M'") | Should -BeGreaterThan $check
        $text | Should -Match ([regex]::Escape('if (-not $hasAce)'))
    }
    It 'system bootstrap publishes its failure as the guest KVP item slate.error before logging it' {
        # The VM and the logs on its disk are discarded; the host reads the
        # item instead. New-Item -Force on an existing registry key deletes
        # its values and subkeys (Guest\Parameter holds the host's data).
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-system.ps1')
        $text | Should -Match 'Virtual Machine\\Guest'
        $text | Should -Match ([regex]::Escape("-Name 'slate.error'"))
        $text | Should -Match ([regex]::Escape('[math]::Min(1000'))
        $text.IndexOf("-Name 'slate.error'") | Should -BeLessThan $text.IndexOf('Write-Log "ERROR: ')
        $creates = @($text -split "`n" | Where-Object { $_ -match 'New-Item' -and $_ -match '\$guestPool' })
        $creates.Count | Should -Be 1
        $creates[0] | Should -Not -Match '-Force'
    }
    It 'runner bootstrap holds a failed VM up for 600 s for vmconnect, then still shuts down' {
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-runner.ps1')
        $text | Should -Match 'holding the VM up for 600 s'
        $hold = $text.IndexOf('Start-Sleep -Seconds 600')
        $hold | Should -BeGreaterThan $text.IndexOf('} catch {')
        $hold | Should -BeLessThan $text.IndexOf('} finally {')
    }
    It 'system bootstrap restricts jit.cfg while it is empty, before the config is written into it' {
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-system.ps1')
        $create = $text.IndexOf('New-Item -ItemType File -Path $cfgPath')
        $restrict = $text.IndexOf('icacls.exe $cfgPath')
        $write = $text.IndexOf('Set-Content -LiteralPath $cfgPath -Value $jit')
        $create | Should -BeGreaterThan -1
        $restrict | Should -BeGreaterThan $create
        $write | Should -BeGreaterThan $restrict
    }
    It 'runner bootstrap sets a real cargo cache directory aside instead of deleting it' {
        # The golden image's populated registry would otherwise be deleted file
        # by file on every boot.
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-runner.ps1')
        $text | Should -Match 'Rename-Item -LiteralPath \$link'
        $text | Should -Match '\.golden-'
        $text | Should -Not -Match 'Remove-Item -LiteralPath \$link'
    }
    It 'polls tolerate transient errors and the runner waits up to 480 s for ready' {
        $system = Get-Content -Raw (Join-Path $guestDir 'bootstrap-system.ps1')
        $system | Should -Match ([regex]::Escape('Get-ItemProperty -LiteralPath $kvpKey -ErrorAction SilentlyContinue'))
        $system | Should -Match ([regex]::Escape('Get-NetAdapter -ErrorAction SilentlyContinue'))
        $runner = Get-Content -Raw (Join-Path $guestDir 'bootstrap-runner.ps1')
        $runner | Should -Match ([regex]::Escape('AddSeconds(480)'))
        $runner | Should -Match 'ready signal did not arrive within 480 s'
    }
    It 'system bootstrap trims the DNS list and drops empty entries' {
        $line = @((Get-Content -Raw (Join-Path $guestDir 'bootstrap-system.ps1')) -split "`n" | Where-Object { $_ -match 'Set-DnsClientServerAddress' })
        $line.Count | Should -Be 1
        $line[0] | Should -Match ([regex]::Escape('ForEach-Object { $_.Trim() }'))
        $line[0] | Should -Match ([regex]::Escape('Where-Object { $_ }'))
    }
    It 'neither script logs or throws the JIT config' {
        foreach ($f in 'bootstrap-system.ps1', 'bootstrap-runner.ps1') {
            $lines = @((Get-Content -Raw (Join-Path $guestDir $f)) -split "`n" | Where-Object { $_ -match 'Write-Log|throw' })
            $lines.Count | Should -BeGreaterThan 0
            @($lines | Where-Object { $_ -match '\$jit' }) | Should -BeNullOrEmpty
        }
    }
    It 'the module and both guest scripts use only Windows PowerShell 5.1 syntax' {
        # pwsh parses PowerShell 7 syntax without complaint, so walk the AST for
        # it. Type names are compared as strings: several of these AST types do
        # not exist on Windows PowerShell 5.1.
        $files = @(
            [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1')),
            [System.IO.Path]::GetFullPath((Join-Path $guestDir 'bootstrap-system.ps1')),
            [System.IO.Path]::GetFullPath((Join-Path $guestDir 'bootstrap-runner.ps1'))
        )
        foreach ($file in $files) {
            $tokens = $null
            $errors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile($file, [ref]$tokens, [ref]$errors)
            @($errors).Count | Should -Be 0 -Because $file
            $guestScript = $file -notlike '*.psm1'
            $found = @($ast.FindAll({
                        param($node)
                        $type = $node.GetType().Name
                        if ($type -eq 'TernaryExpressionAst' -or $type -eq 'PipelineChainAst') { return $true }
                        if ($type -eq 'BinaryExpressionAst' -and [string]$node.Operator -eq 'QuestionQuestion') { return $true }
                        if ($type -eq 'AssignmentStatementAst' -and [string]$node.Operator -eq 'QuestionQuestionEquals') { return $true }
                        if (($type -eq 'MemberExpressionAst' -or $type -eq 'InvokeMemberExpressionAst' -or $type -eq 'IndexExpressionAst') -and
                            $node.PSObject.Properties['NullConditional'] -and $node.NullConditional) { return $true }
                        # 5.1 cmdlets lack these parameters; the module's host-only
                        # functions may use -AsHashtable, the guest scripts may not.
                        if ($guestScript -and $type -eq 'CommandParameterAst' -and ($node.ParameterName -eq 'Parallel' -or $node.ParameterName -eq 'AsHashtable')) { return $true }
                        return $false
                    }, $true))
            @($found | ForEach-Object { '{0}:{1}: {2}' -f (Split-Path -Leaf $file), $_.Extent.StartLineNumber, $_.Extent.Text }) | Should -BeNullOrEmpty
        }
        # Where Windows PowerShell exists, its own parser must accept them too.
        $windowsPowerShell = Get-Command 'powershell.exe' -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($windowsPowerShell) {
            $template = 'foreach ($f in @(FILES)) { $t = $null; $e = $null; [void][System.Management.Automation.Language.Parser]::ParseFile($f, [ref]$t, [ref]$e); "{0}|{1}" -f @($e).Count, $f; foreach ($x in @($e)) { "  {0}: {1}" -f $x.Extent.StartLineNumber, $x.Message } }'
            $list = ($files | ForEach-Object { "'" + $_.Replace("'", "''") + "'" }) -join ','
            $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($template.Replace('FILES', $list)))
            $output = @(& $windowsPowerShell.Source -NoProfile -NonInteractive -EncodedCommand $encoded 2>$null)
            $report = $output -join "`n"
            $counts = @($output | Where-Object { $_ -match '^\d+\|' })
            $counts.Count | Should -Be $files.Count -Because $report
            foreach ($line in $counts) { $line | Should -Match '^0\|' -Because $report }
        }
    }
}
