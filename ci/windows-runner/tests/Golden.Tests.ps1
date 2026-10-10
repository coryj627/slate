# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force
    $script:goldenDir = Join-Path $PSScriptRoot '..' 'golden'
    $script:repoRoot = Join-Path $PSScriptRoot '..' '..' '..'
    function Test-ScriptParses([string]$Path) {
        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$errors)
        return (@($errors).Count -eq 0)
    }
}

Describe 'versions.json' {
    BeforeAll { $script:versions = Get-Content -Raw (Join-Path $goldenDir 'versions.json') | ConvertFrom-Json }
    It 'has every key the provisioning scripts read' {
        foreach ($k in 'runnerVersion', 'runnerSha256', 'rustToolchain', 'bindgenTag', 'dotnetChannel', 'pythonVersion', 'gitVersion', 'gitTag', 'vsBuildToolsUrl', 'dns') {
            $versions.PSObject.Properties[$k] | Should -Not -BeNullOrEmpty
        }
        $versions.runnerSha256 | Should -Match '^[0-9a-f]{64}$'
    }
    It 'pins the Rust toolchain the repository pins' {
        $toml = Get-Content -Raw (Join-Path $repoRoot 'rust-toolchain.toml')
        $toml | Should -Match ('channel\s*=\s*"' + [regex]::Escape($versions.rustToolchain) + '"')
    }
    It 'pins the uniffi-bindgen-cs tag the repository pins' {
        (Get-Content -Raw (Join-Path $repoRoot 'apps' 'slate-windows' 'uniffi-bindgen-cs.version')).Trim() | Should -Be $versions.bindgenTag
    }
}

Describe 'New-RandomPassword' {
    It 'returns the requested length from the alphanumeric alphabet and differs per call' {
        $a = New-RandomPassword -Length 24
        $b = New-RandomPassword -Length 24
        $a.Length | Should -Be 24
        $a | Should -Match '^[A-Za-z0-9]{24}$'
        $a | Should -Not -Be $b
    }
}

Describe 'unattend template' {
    It 'is well-formed XML with the three placeholders and no literal secrets' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'unattend.xml')
        { [xml]$text } | Should -Not -Throw
        $text | Should -Match '__PRODUCT_KEY__'
        $text | Should -Match '__PROVISION_PASSWORD__'
        $text | Should -Match '__RUNNER_PASSWORD__'
        $text | Should -Match '<ComputerName>slate-win</ComputerName>'
        $text | Should -Match '<Username>provision</Username>'
        $text | Should -Match 'provision-guest\.ps1'
        $text | Should -Match 'PreventDeviceEncryption'
    }
    It 'renders with escaped values and no placeholder left' {
        $out = Expand-UnattendTemplate -TemplatePath (Join-Path $goldenDir 'unattend.xml') -ProductKey 'ABCDE-FGHIJ-KLMNO-PQRST-UVWXY' -ProvisionPassword 'p<&>w' -RunnerPassword 'r"w'
        $out | Should -Not -Match '__[A-Z_]+__'
        $out | Should -Match '<ProductKey>ABCDE-FGHIJ-KLMNO-PQRST-UVWXY</ProductKey>'
        $out | Should -Match 'p&lt;&amp;&gt;w'
        { [xml]$out } | Should -Not -Throw
    }
    It 'rejects a malformed product key' {
        { Expand-UnattendTemplate -TemplatePath (Join-Path $goldenDir 'unattend.xml') -ProductKey 'nope' -ProvisionPassword 'a' -RunnerPassword 'b' } | Should -Throw '*product key*'
    }
}

Describe 'golden scripts' {
    It 'parse as PowerShell' {
        foreach ($f in 'provision-guest.ps1', 'provision-runner-user.ps1', 'build-golden.ps1') {
            Test-ScriptParses (Join-Path $goldenDir $f) | Should -BeTrue
        }
    }
    It 'provisioning registers both guest tasks, switches auto-logon to runner, restores UAC and deletes the secrets' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $text | Should -Match "TaskName 'slate-bootstrap-system'"
        $text | Should -Match "TaskName 'slate-runner-logon'"
        $text | Should -Match "DefaultUserName -Value 'runner'"
        $text | Should -Match 'EnableLUA -Value 1'
        $text | Should -Match "secrets\.json'\) -Force"
        $text | Should -Match 'Panther\\unattend\.xml'
        $text | Should -Match 'provision-runner-user\.ps1'
        $text | Should -Match 'slmgr\.vbs /cpky'
        $text | Should -Match "Destination 'C:\\slate-guest'"
        $text | Should -Match 'C:\\slate-guest\\bootstrap-system\.ps1'
    }
    It 'the per-user script installs the pinned toolchain and writes the completion marker last' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-runner-user.ps1')
        $text | Should -Match 'rustup-init\.exe'
        $text | Should -Match 'aarch64-pc-windows-msvc'
        $text | Should -Match 'uniffi-bindgen-cs'
        # Last occurrences: the no-op guard at the top also names the marker.
        $text.LastIndexOf('.slate-golden-complete') | Should -BeGreaterThan $text.LastIndexOf('cargo install')
    }
    It 'the build script applies install.wim with DISM, injects the unattend and marks the disk read-only' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'build-golden.ps1')
        $text | Should -Match 'Expand-WindowsImage'
        $text | Should -Match 'bcdboot'
        $text | Should -Match 'Windows\\Panther\\unattend\.xml'
        $text | Should -Match 'IsReadOnly -Value \$true'
        $text | Should -Match '\.slate-golden-complete'
    }
    It 'provisioning hands phase 2 to a runner logon task, because HKLM RunOnce runs only when an administrator logs on' {
        # runner is a standard user, so an HKLM RunOnce entry would never run
        # and the build would sit at runner's desktop until its timeout.
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $text | Should -Match "TaskName 'slate-provision-runner-user'"
        $text | Should -Match ([regex]::Escape('-File C:\provision\provision-runner-user.ps1'))
        $code = @($text -split "`n" | Where-Object { $_ -notmatch '^\s*#' })
        @($code | Where-Object { $_ -match 'RunOnce' }) | Should -BeNullOrEmpty
    }
    It 'the per-user script exits once the marker exists, before anything that can shut the VM down' {
        # Its logon task stays in the image and fires in every job VM.
        $code = @((Get-Content -Raw (Join-Path $goldenDir 'provision-runner-user.ps1')) -split "`n" | Where-Object { $_ -notmatch '^\s*#' })
        $guards = @(0..($code.Count - 1) | Where-Object { $code[$_] -match '\.slate-golden-complete' })
        $guards.Count | Should -BeGreaterThan 0
        $code[$guards[0]] | Should -Match 'Test-Path .*\{ exit 0 \}'
        $tries = @(0..($code.Count - 1) | Where-Object { $code[$_] -match '^\s*try \{' })
        $tries.Count | Should -Be 1
        $guards[0] | Should -BeLessThan $tries[0]
    }
    It 'the build script uses only the documented GPT type GUIDs and formats the ESP before retyping it' {
        # A mistyped type GUID is not a partition Windows mounts or formats.
        # The ESP is created as basic data, formatted, then retyped (the order
        # Microsoft's Convert-WindowsImage uses).
        $text = Get-Content -Raw (Join-Path $goldenDir 'build-golden.ps1')
        $esp = '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}'
        $documented = $esp, '{e3c9e316-0b5c-4db8-817d-f92df00215ae}', '{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}'
        $guids = @([regex]::Matches($text, '\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}') | ForEach-Object { $_.Value })
        $guids.Count | Should -BeGreaterThan 0
        foreach ($guid in $guids) { $documented | Should -Contain $guid }
        @($text -split "`n" | Where-Object { $_ -match 'New-Partition' -and $_.Contains($esp) }) | Should -BeNullOrEmpty
        $format = $text.IndexOf('-FileSystem FAT32')
        $format | Should -BeGreaterThan -1
        $text.IndexOf("Set-Partition -GptType '$esp'") | Should -BeGreaterThan $format
    }
    It 'both phase scripts turn the progress bar off right after the error preference' {
        # Windows PowerShell 5.1 downloads crawl while Invoke-WebRequest draws it.
        foreach ($f in 'provision-guest.ps1', 'provision-runner-user.ps1') {
            $code = @((Get-Content -Raw (Join-Path $goldenDir $f)) -split "`n" | Where-Object { $_ -notmatch '^\s*#' })
            $eap = @(0..($code.Count - 1) | Where-Object { $code[$_] -match ('^\s*' + [regex]::Escape('$ErrorActionPreference = ''Stop''')) })
            $eap.Count | Should -Be 1 -Because $f
            $code[$eap[0] + 1] | Should -Match ('^\s*' + [regex]::Escape('$ProgressPreference = ''SilentlyContinue''')) -Because $f
        }
    }
    It 'provisioning checks the exit code of every icacls call' {
        # A native command's failure never throws, even under Stop; an unchecked
        # one would leave C:\slate-guest writable by every user.
        $code = @((Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')) -split "`n" | Where-Object { $_ -notmatch '^\s*#' })
        $calls = @(0..($code.Count - 1) | Where-Object { $code[$_] -match 'icacls\.exe' })
        $calls.Count | Should -Be 4
        foreach ($i in $calls) { $code[$i + 1] | Should -Match '\$LASTEXITCODE -ne 0' }
    }
    It 'provisioning disables the temporary admin after auto-logon moves to runner and before the restart' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $disable = $text.IndexOf("Disable-LocalUser -Name 'provision'")
        $disable | Should -BeGreaterThan $text.IndexOf("DefaultUserName -Value 'runner'")
        $disable | Should -BeLessThan $text.IndexOf('Restart-Computer -Force')
    }
    It 'the build script resolves -IsoPath to an absolute path before mounting it' {
        # Mount-DiskImage and Dismount-DiskImage need an absolute path.
        $text = Get-Content -Raw (Join-Path $goldenDir 'build-golden.ps1')
        $resolve = $text.IndexOf('$IsoPath = (Resolve-Path -LiteralPath $IsoPath).Path')
        $resolve | Should -BeGreaterThan -1
        $resolve | Should -BeLessThan $text.IndexOf('Mount-DiskImage -ImagePath')
    }
}
