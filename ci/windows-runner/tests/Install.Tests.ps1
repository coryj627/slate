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
    It 'Set-LocalUserRights applies the orchestrator account''s three rights by default and takes others for the guest' {
        # The golden build grants runner SeCreateSymbolicLinkPrivilege through it.
        $parameter = (Get-Command Set-LocalUserRights).Parameters['Rights']
        $parameter | Should -Not -BeNullOrEmpty
        $parameter.ParameterType | Should -Be ([string[]])
        $default = ((Get-Command Set-LocalUserRights).ScriptBlock.Ast.Body.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'Rights' }).DefaultValue.Extent.Text
        $default | Should -Match 'SeDenyInteractiveLogonRight'
        $default | Should -Match 'SeDenyRemoteInteractiveLogonRight'
        $default | Should -Match 'SeBatchLogonRight'
    }
}

Describe 'install scripts' {
    It 'parse as PowerShell' {
        Test-ScriptParses (Join-Path $installDir 'setup-host.ps1') | Should -BeTrue
        Test-ScriptParses (Join-Path $installDir 'store-token.ps1') | Should -BeTrue
        Test-ScriptParses (Join-Path $installDir 'prune-golden.ps1') | Should -BeTrue
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
        # Hyper-V, acting as the caller, writes each VM's access entry onto
        # every disk of a differencing chain when it attaches the child: the
        # account needs WDAC on the golden disk and the cache parents, no more.
        $text | Should -Match ([regex]::Escape('(Join-Path $Root ''golden'') /inheritance:r /grant ''Administrators:(OI)(CI)F'' /grant ''SYSTEM:(OI)(CI)F'' /grant:r "${Account}:(OI)(CI)(RX,WDAC)"'))
        $text | Should -Match ([regex]::Escape('(Join-Path $Root ''cache'') /grant:r "${Account}:(OI)(CI)(M,WDAC)"'))
        $text | Should -Not -Match 'golden[^\r\n]*\(OI\)\(CI\)(RX|M)"'
        $text | Should -Match ([regex]::Escape('-At (Get-Date).AddMinutes(1)'))
        $text | Should -Match ([regex]::Escape('-Trigger @($loopTrigger, $repeatTrigger)'))
        $text | Should -Match ([regex]::Escape('[switch]$ResetAccount'))
    }
    It 'registers the loop task, then disables it until the runbook enables it after the product-key check' {
        # No job may run on the golden disk before its product-key check.
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $register = $text.IndexOf("Register-ScheduledTask -TaskName 'slate-ci-orchestrator'")
        $disable = $text.IndexOf("Disable-ScheduledTask -TaskName 'slate-ci-orchestrator'")
        $register | Should -BeGreaterThan -1
        $disable | Should -BeGreaterThan $register
        $text | Should -Not -Match 'Enable-ScheduledTask -TaskName ''slate-ci-orchestrator'''
        # The closing message says so and names the command.
        $next = $text.Substring($text.IndexOf("'Host setup complete. Next:'"))
        $next | Should -Match 'disabled'
        $next | Should -Match 'product-key check'
        $next | Should -Match 'Enable-ScheduledTask -TaskName slate-ci-orchestrator'
    }
    It 'setup runs only under the MSI build of PowerShell 7, reads Hyper-V through dism.exe and registers the tasks with that build' {
        # The Store (MSIX) build cannot see the Dism module's COM classes
        # ("Class not registered") and its WindowsApps alias is one the task
        # account cannot start.
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $text | Should -Match ([regex]::Escape("`$msiPwsh = Join-Path `$env:ProgramFiles 'PowerShell\7\pwsh.exe'"))
        $text | Should -Match ([regex]::Escape("if (`$thisHost -like '*\WindowsApps\*') { throw"))
        $text | Should -Match 'dism\.exe /Online /English /Get-FeatureInfo /FeatureName:Microsoft-Hyper-V-All'
        $text | Should -Match 'dism\.exe /Online /English /Enable-Feature /FeatureName:Microsoft-Hyper-V-All /All /NoRestart'
        # Code lines only: a comment names the cmdlet that fails.
        $code = @($text -split "`n" | Where-Object { $_ -notmatch '^\s*#' })
        @($code | Where-Object { $_ -match 'Get-WindowsOptionalFeature|Enable-WindowsOptionalFeature' }) | Should -BeNullOrEmpty
        $text | Should -Match ([regex]::Escape('$pwsh = $msiPwsh'))
        $text | Should -Not -Match ([regex]::Escape('(Get-Command pwsh).Source'))
        # The guard sits before anything is created.
        $text.IndexOf('WindowsApps') | Should -BeLessThan $text.IndexOf('1/9 Hyper-V')
    }
    It 'a reset removes both tasks before the password changes' {
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $unregister = $text.IndexOf('Unregister-ScheduledTask')
        $unregister | Should -BeGreaterOrEqual 0
        $unregister | Should -BeLessThan $text.IndexOf('Set-LocalUser -Name')
        $unregister | Should -BeLessThan $text.IndexOf('New-LocalUser -Name')
    }
    It 'bin becomes read-only for the account after the mirror' {
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $binLine = 'icacls.exe (Join-Path $Root ''bin'') /inheritance:r /grant ''Administrators:(OI)(CI)F'' /grant ''SYSTEM:(OI)(CI)F'' /grant:r "${Account}:(OI)(CI)RX"'
        $text | Should -Match ([regex]::Escape($binLine))
        $text.IndexOf($binLine) | Should -BeGreaterThan $text.IndexOf('robocopy.exe')
    }
    It 'the host firewall rule covers every address of the host on the switch interface' {
        # The second layer under the port ACL: whichever host address a packet
        # from the VM subnet names (the gateway, the LAN or tailnet address
        # under the weak-host model, multicast, broadcast), it is dropped on
        # that interface. Forwarded NAT traffic never meets an inbound rule.
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        # A new rule gets it, and a re-run brings an existing rule up to date.
        @([regex]::Matches($text, '(New|Set)-NetFirewallRule -DisplayName \$ruleName -Direction Inbound -Action Block -RemoteAddress \$NatPrefix -LocalAddress Any -InterfaceAlias \$alias -Profile Any')).Count | Should -Be 2
        $text | Should -Not -Match '-LocalAddress \$'
    }
    It 'reads the tree, the switch and the subnet from config.json, so the host, the loop and the guests agree' {
        # Get-CiHostConfig validates NatPrefix and puts the gateway and every
        # slot inside it; the host address takes the prefix length from it.
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $text | Should -Match ([regex]::Escape('$config = Get-CiHostConfig -Path $ConfigPath'))
        foreach ($line in '$Root = [string]$config.Root', '$SwitchName = [string]$config.SwitchName', '$Gateway = [string]$config.Gateway',
            '$NatPrefix = [string]$config.NatPrefix', '$prefixLength = [int]$config.PrefixLength',
            '$CacheLanes = @($config.Lanes.Keys | Where-Object { $config.Lanes[$_].Cache })') {
            $text | Should -Match ([regex]::Escape($line))
        }
        foreach ($p in 'Root', 'SwitchName', 'Gateway', 'NatPrefix', 'CacheLanes') {
            $text | Should -Not -Match ('\[string(\[\])?\]\$' + $p + ' = ') -Because "$p is not a parameter any more"
        }
        $text | Should -Match 'New-NetIPAddress -InterfaceAlias \$alias -IPAddress \$Gateway -PrefixLength \$prefixLength'
        $text | Should -Not -Match 'New-NetIPAddress[^\r\n]*-PrefixLength 24\b'
        # Loaded after the module and before anything is created.
        $text.IndexOf('Get-CiHostConfig') | Should -BeGreaterThan $text.IndexOf('Import-Module')
        $text.IndexOf('Get-CiHostConfig') | Should -BeLessThan $text.IndexOf('1/9 Hyper-V')
    }
    It 'a cache volume is formatted before it gets a drive letter' {
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $text | Should -Not -Match 'New-Partition[^\r\n]*-AssignDriveLetter'
        $text | Should -Match 'Add-PartitionAccessPath -DiskNumber'
    }
    It 'the loop task runs on battery power and the install commit must resolve' {
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $text | Should -Match '-AllowStartIfOnBatteries'
        $text | Should -Match '-DontStopIfGoingOnBatteries'
        $text | Should -Match 'rev-parse HEAD\r?\nif \(\$LASTEXITCODE -ne 0\) \{ throw'
    }
    It 'store-token only accepts a fine-grained PAT and removes the plaintext' {
        $text = Get-Content -Raw (Join-Path $installDir 'store-token.ps1')
        $text | Should -Match 'github_pat_'
        $text | Should -Match 'Export-Clixml'
        $text | Should -Match 'Remove-Item -LiteralPath \$plainPath'
    }
    It 'store-token grants the account the task runs as and waits for the plaintext to be consumed' {
        # The account comes from the task setup-host.ps1 (-Account) registered,
        # never a literal; and Start-ScheduledTask returns before the task's
        # state reads Running, so the wait watches the file, not the state.
        $text = Get-Content -Raw (Join-Path $installDir 'store-token.ps1')
        $text | Should -Match ([regex]::Escape('$account = [string]$task.Principal.UserId'))
        $text | Should -Match ([regex]::Escape('/grant "${account}:(R,D,WA)"'))
        $text | Should -Not -Match 'slate-ci-host:'
        $text | Should -Match ([regex]::Escape('while ((Test-Path -LiteralPath $plainPath) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }'))
        $text | Should -Not -Match "State -eq 'Running'"
    }
    It 'setup registers the elevated prune task for the previous golden image, daily, as SYSTEM, on every run and outside the password-bound pair' {
        $text = Get-Content -Raw (Join-Path $installDir 'setup-host.ps1')
        $start = $text.IndexOf("Register-ScheduledTask -TaskName 'slate-ci-golden-prune'")
        $start | Should -BeGreaterThan -1
        $end = $text.IndexOf("-User 'NT AUTHORITY\SYSTEM'", $start)
        $end | Should -BeGreaterThan $start
        $prune = $text.Substring($start, $end - $start)
        $prune | Should -Match 'prune-golden\.ps1'
        $prune | Should -Match '-Daily -At 4am'
        $prune | Should -Not -Match '\$Account'
        $text.Substring($end, 120) | Should -Match '-RunLevel Highest'
        $text | Should -Match ([regex]::Escape("`$taskNames = @('slate-ci-orchestrator', 'slate-ci-store-token')"))
        $nine = $text.IndexOf("'9/9 scheduled tasks'")
        $start | Should -BeGreaterThan $nine
        $start | Should -BeLessThan $text.IndexOf('if ($keepAccount) {', $nine)
        $text | Should -Match 'slate-ci-golden-prune once the loop has accepted'
    }
}

Describe 'prune-golden.ps1' {
    BeforeEach {
        $script:root = Join-Path $TestDrive ('root-' + [guid]::NewGuid().ToString('N'))
        foreach ($d in 'golden', 'state', 'vms', 'logs', 'bin', 'bin\adapters') { New-Item -ItemType Directory -Force (Join-Path $root $d) | Out-Null }
        Copy-Item (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') (Join-Path $root 'bin')
        Copy-Item (Join-Path $PSScriptRoot '..' 'adapters' 'HyperV.ps1') (Join-Path $root 'bin\adapters')
        $script:golden = Join-Path $root 'golden\win11-runner.vhdx'; 'new' | Set-Content $golden
        $script:prev = Join-Path $root 'golden\win11-runner.prev.vhdx'; 'old' | Set-Content $prev
        (Get-Item $prev).LastWriteTimeUtc = [datetime]'2026-10-10T20:00:00Z'
        (Get-Item $golden).LastWriteTimeUtc = [datetime]'2026-10-10T22:55:00Z'
        Set-ItemProperty -LiteralPath $prev -Name IsReadOnly -Value $true
        Set-ItemProperty -LiteralPath $golden -Name IsReadOnly -Value $true
        $script:journalPath = Join-Path $root 'state\journal.json'
        $script:prune = Join-Path $installDir 'prune-golden.ps1'
        function Set-Accepted([string]$Stamp, [string[]]$Lanes = @('rust', 'app', 'model', 'shell')) {
            $j = New-Journal
            $j.GoldenAccepted = @{ Golden = $Stamp; Lanes = $Lanes; Complete = ($Lanes.Count -eq 4) }
            Write-Journal -Path $script:journalPath -Journal $j
        }
        function Get-PruneLog { Get-Content -Raw (Get-ChildItem (Join-Path $root 'logs') | Select-Object -First 1).FullName }
    }
    It 'deletes the previous image once the current, sealed image is accepted on every lane' {
        Set-Accepted '2026-10-10T22:55:00.0000000Z'
        & $prune -Root $root
        Test-Path $prev | Should -BeFalse
        Test-Path $golden | Should -BeTrue
        Get-PruneLog | Should -Match 'golden: removed win11-runner.prev.vhdx'
    }
    It 'keeps it when the acceptance is of another image (the previous one, right after a rotation)' {
        Set-Accepted '2026-10-10T20:00:00.0000000Z'
        & $prune -Root $root
        Test-Path $prev | Should -BeTrue
        Get-PruneLog | Should -Match 'the previous image stays'
    }
    It 'keeps it while the current image is unsealed, a lane is missing, or a VM older than the image exists' {
        Set-ItemProperty -LiteralPath $golden -Name IsReadOnly -Value $false
        Set-Accepted '2026-10-10T22:55:00.0000000Z'
        & $prune -Root $root
        Test-Path $prev | Should -BeTrue
        Set-ItemProperty -LiteralPath $golden -Name IsReadOnly -Value $true
        Set-Accepted '2026-10-10T22:55:00.0000000Z' @('rust', 'app', 'model')
        & $prune -Root $root
        Test-Path $prev | Should -BeTrue
        Set-Accepted '2026-10-10T22:55:00.0000000Z'
        $old = Join-Path $root 'vms\slate-win-app-00000000'; New-Item -ItemType Directory -Force $old | Out-Null
        (Get-Item $old).CreationTimeUtc = [datetime]'2026-10-10T21:00:00Z'
        & $prune -Root $root
        Test-Path $prev | Should -BeTrue
    }
    It 'does nothing without a previous image, and survives a broken journal' {
        Remove-Item $prev -Force
        Set-Accepted '2026-10-10T22:55:00.0000000Z'
        { & $prune -Root $root } | Should -Not -Throw
        'broken' | Set-Content $journalPath
        'old' | Set-Content $prev
        { & $prune -Root $root } | Should -Not -Throw
        Test-Path $prev | Should -BeTrue
        Test-Path $journalPath | Should -BeTrue
    }
}
