# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force
    $script:installDir = Join-Path $PSScriptRoot '..' 'install'
    function Test-ScriptParses([string]$Path) {
        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$errors)
        return (@($errors).Count -eq 0)
    }
}

Describe 'Add-SidToUserRight' {
    BeforeAll {
        $script:ini = @"
[Unicode]
Unicode=yes
[Privilege Rights]
SeDenyInteractiveLogonRight = *S-1-5-32-546
SeBatchLogonRight = *S-1-5-32-544,*S-1-5-32-551
[Version]
signature="`$CHICAGO`$"
Revision=1
"@
    }
    It 'appends the SID to an existing right' {
        $out = Add-SidToUserRight -IniText $ini -Right 'SeDenyInteractiveLogonRight' -Sid 'S-1-5-21-1-2-3-1004'
        $out | Should -Match 'SeDenyInteractiveLogonRight = \*S-1-5-32-546,\*S-1-5-21-1-2-3-1004'
        $out | Should -Match 'SeBatchLogonRight = \*S-1-5-32-544,\*S-1-5-32-551'
    }
    It 'is idempotent' {
        $once = Add-SidToUserRight -IniText $ini -Right 'SeBatchLogonRight' -Sid 'S-1-5-21-1-2-3-1004'
        $twice = Add-SidToUserRight -IniText $once -Right 'SeBatchLogonRight' -Sid 'S-1-5-21-1-2-3-1004'
        $twice | Should -Be $once
    }
    It 'adds a missing right inside the section' {
        $out = Add-SidToUserRight -IniText $ini -Right 'SeDenyRemoteInteractiveLogonRight' -Sid 'S-1-5-21-1-2-3-1004'
        $lines = $out -split "`r?`n"
        $section = [array]::IndexOf($lines, '[Privilege Rights]')
        $version = [array]::IndexOf($lines, '[Version]')
        $added = [array]::IndexOf($lines, 'SeDenyRemoteInteractiveLogonRight = *S-1-5-21-1-2-3-1004')
        $added | Should -BeGreaterThan $section
        $added | Should -BeLessThan $version
    }
    It 'creates the section when the export has none' {
        $out = Add-SidToUserRight -IniText "[Unicode]`r`nUnicode=yes" -Right 'SeBatchLogonRight' -Sid 'S-1-5-21-9'
        $out | Should -Match '\[Privilege Rights\]\r?\nSeBatchLogonRight = \*S-1-5-21-9'
    }
}

Describe 'install scripts' {
    It 'parse as PowerShell' {
        Test-ScriptParses (Join-Path $installDir 'setup-host.ps1') | Should -BeTrue
        Test-ScriptParses (Join-Path $installDir 'store-token.ps1') | Should -BeTrue
    }
    It 'setup never adds the account to Administrators and registers both tasks' {
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $text | Should -Match "Add-LocalGroupMember -Group 'Hyper-V Administrators'"
        $text | Should -Not -Match "Add-LocalGroupMember -Group 'Administrators'"
        $text | Should -Match "TaskName 'slate-ci-orchestrator'"
        $text | Should -Match "TaskName 'slate-ci-store-token'"
        $text | Should -Match "NewFileSystemLabel 'slate-cache'"
        $text | Should -Match 'New-NetNat'
        $text | Should -Match 'Set-LocalUserRights'
    }
    It 'setup cuts the tree off from inherited ACLs, starts the loop repeating at once and keeps the account on a re-run' {
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $text | Should -Match 'icacls\.exe \$Root /inheritance:r '
        $text | Should -Match ([regex]::Escape('-At (Get-Date).AddMinutes(1)'))
        $text | Should -Match ([regex]::Escape('-Trigger @($loopTrigger, $repeatTrigger)'))
        $text | Should -Match ([regex]::Escape('[switch]$ResetAccount'))
    }
    It 'store-token only accepts a fine-grained PAT and removes the plaintext' {
        $text = Get-Content -Raw (Join-Path $installDir 'store-token.ps1')
        $text | Should -Match 'github_pat_'
        $text | Should -Match 'Export-Clixml'
        $text | Should -Match 'Remove-Item -LiteralPath \$plainPath'
    }
}
