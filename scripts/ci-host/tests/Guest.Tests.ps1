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
    It 'runner bootstrap deletes jit.cfg without -Force (it may read the file, not rewrite its attributes)' {
        # jit.cfg grants runner only R; the delete itself is allowed by the
        # directory's delete-child right, but -Force first clears attributes.
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-runner.ps1')
        $line = @($text -split "`n" | Where-Object { $_ -match 'Remove-Item' -and $_ -match 'jit\.cfg' })
        $line.Count | Should -Be 1
        $line[0] | Should -Not -Match '-Force'
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
    It 'both scripts force the shutdown so no application can veto it' {
        foreach ($f in 'bootstrap-system.ps1', 'bootstrap-runner.ps1') {
            $lines = @((Get-Content -Raw (Join-Path $guestDir $f)) -split "`n" | Where-Object { $_ -match 'shutdown\.exe' })
            $lines.Count | Should -BeGreaterThan 0
            foreach ($line in $lines) { $line | Should -Match ' /f ' }
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
    It 'neither script contains PowerShell 7-only syntax' {
        foreach ($f in 'bootstrap-system.ps1', 'bootstrap-runner.ps1') {
            $text = Get-Content -Raw (Join-Path $guestDir $f)
            $text | Should -Not -Match '\?\?'
            $text | Should -Not -Match '-Parallel'
            $text | Should -Not -Match '-AsHashtable'
        }
    }
}
