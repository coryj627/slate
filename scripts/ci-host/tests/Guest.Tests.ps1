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
        $text | Should -Match 'shutdown\.exe /s /t 0'
    }
    It 'runner bootstrap deletes jit.cfg without -Force (it may read the file, not rewrite its attributes)' {
        # jit.cfg grants runner only R; the delete itself is allowed by the
        # directory's delete-child right, but -Force first clears attributes.
        $text = Get-Content -Raw (Join-Path $guestDir 'bootstrap-runner.ps1')
        $line = @($text -split "`n" | Where-Object { $_ -match 'Remove-Item' -and $_ -match 'jit\.cfg' })
        $line.Count | Should -Be 1
        $line[0] | Should -Not -Match '-Force'
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
