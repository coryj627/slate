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
        foreach ($k in 'runnerVersion', 'runnerSha256', 'rustToolchain', 'bindgenTag', 'dotnetChannel', 'pythonVersion', 'pythonToolcacheUrl', 'pythonToolcacheSha256',
            'gitVersion', 'gitTag', 'gitSha256', 'pwshVersion', 'pwshSha256', 'rustupVersion', 'rustupInitSha256', 'vsBuildToolsUrl', 'dns') {
            $versions.PSObject.Properties[$k] | Should -Not -BeNullOrEmpty -Because $k
        }
        foreach ($k in 'runnerSha256', 'pythonToolcacheSha256', 'gitSha256', 'pwshSha256', 'rustupInitSha256') {
            $versions.$k | Should -Match '^[0-9a-f]{64}$' -Because $k
        }
        $versions.rustupVersion | Should -Match '^\d+\.\d+\.\d+$'
        $versions.pwshVersion | Should -Match '^7\.\d+\.\d+$'
        # The build setup-python would install: actions/python-versions' win32-x64 archive of the pinned version.
        $versions.pythonToolcacheUrl | Should -BeLike "https://github.com/actions/python-versions/releases/download/$($versions.pythonVersion)-*/python-$($versions.pythonVersion)-win32-x64.zip"
    }
    It 'records where each pinned download hash was published, for the pinned version' {
        # JSON has no comments: the sources object names each hash's origin.
        foreach ($k in 'pythonToolcacheSha256', 'gitSha256', 'pwshSha256', 'rustupInitSha256') {
            $versions.sources.$k | Should -Match '^https://' -Because $k
        }
        $versions.sources.pythonToolcacheSha256 | Should -BeLike "*/actions/python-versions/releases/tag/$($versions.pythonVersion)-*"
        $versions.sources.pwshSha256 | Should -BeLike "*/PowerShell/PowerShell/releases/tag/v$($versions.pwshVersion)"
        $versions.sources.gitSha256 | Should -BeLike "*/releases/tag/$($versions.gitTag)"
        $versions.sources.rustupInitSha256 | Should -BeLike "*/rustup/archive/$($versions.rustupVersion)/x86_64-pc-windows-msvc/rustup-init.exe.sha256"
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
    It 'phase 1 checks the runner, Python and Git downloads against their pinned SHA-256' {
        $code = @((Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')) -split "`n" | Where-Object { $_ -notmatch '^\s*#' })
        @($code | Where-Object { $_ -match '^\s*Get-Download -Url \$versions\.pythonToolcacheUrl -Path \$pyZip -Sha256 \$versions\.pythonToolcacheSha256\s*$' }).Count | Should -Be 1
        @($code | Where-Object { $_ -match '^\s*Get-Download -Url .*-Path \(Join-Path \$dl ''git\.exe''\) -Sha256 \$versions\.gitSha256\s*$' }).Count | Should -Be 1
        @($code | Where-Object { $_ -match '^\s*Get-Download -Url .*-Path \$zip -Sha256 \$versions\.runnerSha256\s*$' }).Count | Should -Be 1
        @($code | Where-Object { $_ -match '^\s*Get-Download -Url .*PowerShell-\{0\}-win-x64\.msi.*-Path \$pwshMsi -Sha256 \$versions\.pwshSha256\s*$' }).Count | Should -Be 1
    }
    It 'phase 1 installs PowerShell 7 on the machine PATH, which every lane''s `shell: pwsh` resolves' {
        # The first home job failed its first pwsh step with "pwsh: command
        # not found": the image had Windows PowerShell 5.1 only.
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $text | Should -Match ([regex]::Escape("Invoke-Installer 'msiexec.exe' ('/i `"{0}`" /quiet /norestart ADD_PATH=1 REGISTER_MANIFEST=1 USE_MU=0 ENABLE_MU=0 DISABLE_TELEMETRY=1' -f `$pwshMsi)"))
        $text | Should -Match ([regex]::Escape("if (-not (Test-Path -LiteralPath 'C:\Program Files\PowerShell\7\pwsh.exe'))"))
        $text.IndexOf('$pwshMsi') | Should -BeLessThan $text.IndexOf('Restart-Computer -Force')
    }
    It 'phase 1 installs Python into the runner tool cache as an administrator, before the tree is handed to runner' {
        # actions/setup-python's Windows installer needs administrator rights
        # (InstallAllUsers=1, HKLM); as `runner` a job would wait on a UAC
        # prompt for ever. A 3.13 already in the cache makes it install nothing.
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $code = @($text -split "`n" | Where-Object { $_ -notmatch '^\s*#' })
        @($code | Where-Object { $_ -match 'python\.org/ftp' }) | Should -BeNullOrEmpty
        $text | Should -Match ([regex]::Escape("`$toolCache = 'C:\actions-runner\_work\_tool'"))
        $text | Should -Match ([regex]::Escape('$env:AGENT_TOOLSDIRECTORY = $toolCache'))
        # setup.ps1 copies the installer by a path relative to the current directory.
        $text | Should -Match ([regex]::Escape("'-File', (Join-Path `$pyDir 'setup.ps1')"))
        $text | Should -Match ([regex]::Escape('-WorkingDirectory $pyDir -Wait -PassThru -NoNewWindow -RedirectStandardOutput $setupOut -RedirectStandardError $setupErr'))
        $text | Should -Match ([regex]::Escape('Write-Log "  python-toolcache: $_"'))
        $text | Should -Match ([regex]::Escape('if (-not (Test-Path -LiteralPath "$pyHome.complete"))'))
        $install = $text.IndexOf('$env:AGENT_TOOLSDIRECTORY = $toolCache')
        $install | Should -BeGreaterThan $text.IndexOf("Expand-Archive -Path `$zip -DestinationPath 'C:\actions-runner'")
        $install | Should -BeLessThan $text.IndexOf('/setowner runner /T /C')
    }
    It 'phase 1 verifies the Authenticode signature of the two evergreen downloads, which no hash can pin' {
        # aka.ms/vs and dot.net serve a new build at every release; both
        # installers run with administrator rights in the image.
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $code = @($text -split "`n" | Where-Object { $_ -notmatch '^\s*#' })
        @($code | Where-Object { $_ -match '^\s*Get-Download -Url \$versions\.vsBuildToolsUrl .*-Signer ''Microsoft Corporation''\s*$' }).Count | Should -Be 1
        @($code | Where-Object { $_ -match '^\s*Get-Download -Url ''https://dot\.net/v1/dotnet-install\.ps1'' .*-Signer ''Microsoft Corporation''\s*$' }).Count | Should -Be 1
        # Every download is checked one way or the other.
        @($code | Where-Object { $_ -match '^\s*Get-Download ' -and $_ -notmatch '-Sha256 |-Signer ' }) | Should -BeNullOrEmpty
        $text | Should -Match 'Get-AuthenticodeSignature -LiteralPath \$Path'
        $text | Should -Match ([regex]::Escape("if (`$signature.Status -ne 'Valid')"))
        $text | Should -Match ([regex]::Escape('O=') + "' \+ \[regex\]::Escape\(\`$Signer\)")
    }
    It 'phase 2 downloads the pinned rustup-init and checks its SHA-256 before running it' {
        # The unversioned rustup-init URL changes with every rustup release,
        # so the hash pins the archived build of rustupVersion.
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-runner-user.ps1')
        $text | Should -Match ([regex]::Escape("'https://static.rust-lang.org/rustup/archive/{0}/x86_64-pc-windows-msvc/rustup-init.exe' -f `$versions.rustupVersion"))
        $text | Should -Not -Match ([regex]::Escape('rustup/dist/'))
        $hash = $text.IndexOf('Get-FileHash -LiteralPath $rustupInit -Algorithm SHA256')
        $hash | Should -BeGreaterThan $text.IndexOf('Invoke-WebRequest')
        $hash | Should -BeLessThan $text.IndexOf('Start-Process -FilePath $rustupInit')
        $text | Should -Match ([regex]::Escape('$versions.rustupInitSha256'))
        $text | Should -Match 'sha256 mismatch'
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
    It 'provisioning grants runner the symbolic-link privilege through the module, before the restart' {
        # Three vault tests create symbolic links with std::os::windows::fs;
        # hosted runners pass them as administrators, and a standard user
        # without SeCreateSymbolicLinkPrivilege gets os error 1314.
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $grant = $text.IndexOf("Set-LocalUserRights -Sid `$runnerSid -Rights 'SeCreateSymbolicLinkPrivilege'")
        $grant | Should -BeGreaterThan -1
        $import = $text.IndexOf("Import-Module (Join-Path `$root 'SlateCiHost.psm1') -Force")
        $import | Should -BeGreaterThan -1
        $import | Should -BeLessThan $grant
        $grant | Should -BeLessThan $text.IndexOf('Restart-Computer -Force')
        $text | Should -Match ([regex]::Escape("NTAccount('runner')"))
    }
    It 'provisioning makes the runner password never expire, before the restart' {
        # Auto-logon stops working once the password expires (42 days by
        # default) and every job would then end unclaimed.
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $line = 'Set-LocalUser -Name runner -PasswordNeverExpires $true'
        $text | Should -Match ([regex]::Escape($line))
        $text.IndexOf($line) | Should -BeLessThan $text.IndexOf('Restart-Computer -Force')
    }
    It 'provisioning turns IPv6 off by policy for every adapter, not only the build adapter' {
        # A job VM gets a new network adapter; the binding change below
        # applies only to the adapters the build VM had.
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $text | Should -Match ([regex]::Escape("Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters' -Name DisabledComponents -Value 0xFF -Type DWord"))
        $text | Should -Match 'Disable-NetAdapterBinding -ComponentID ms_tcpip6'
        # New-Item -Force on an existing key would delete its values.
        @($text -split "`n" | Where-Object { $_ -match 'New-Item' -and $_ -match 'Tcpip6' }) | Should -BeNullOrEmpty
    }
    It 'provisioning disables the temporary admin after auto-logon moves to runner and before the restart' {
        $text = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $disable = $text.IndexOf("Disable-LocalUser -Name 'provision'")
        $disable | Should -BeGreaterThan $text.IndexOf("DefaultUserName -Value 'runner'")
        $disable | Should -BeLessThan $text.IndexOf('Restart-Computer -Force')
    }
    It 'phase 1 removes every cached copy of the answer file and the build checks all three' {
        # Setup caches the processed unattend, product key included, under
        # Panther\Unattend and Sysprep\Panther as well as the file it ran from.
        $guest = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $guest | Should -Match ([regex]::Escape("Remove-Item -LiteralPath 'C:\Windows\Panther\unattend.xml' -Force"))
        $guest | Should -Match ([regex]::Escape("Remove-Item -LiteralPath 'C:\Windows\Panther\Unattend' -Recurse -Force"))
        $guest | Should -Match ([regex]::Escape("Get-ChildItem -LiteralPath 'C:\Windows\System32\Sysprep\Panther' -Filter '*.xml'"))
        $build = Get-Content -Raw (Join-Path $goldenDir 'build-golden.ps1')
        # The full quoted strings: -Match ignores case, so a bare
        # Windows\Panther\Unattend would also match the unattend.xml entry
        # and prove nothing about the directory check.
        foreach ($cached in '"$w\Windows\Panther\unattend.xml"', '"$w\Windows\Panther\Unattend"', '"$w\Windows\System32\Sysprep\Panther\unattend.xml"') {
            $build | Should -Match ([regex]::Escape($cached)) -Because $cached
        }
        $build | Should -Match 'rendered unattend still present in the image'
    }
    It 'phase 1 deletes the clear-text backup of the product key and the build checks the registry for both copies' {
        # slmgr /cpky clears DigitalProductId's key bytes only; Windows keeps
        # SoftwareProtectionPlatform\BackupProductKeyDefault, which Users can read.
        $guest = Get-Content -Raw (Join-Path $goldenDir 'provision-guest.ps1')
        $cpky = $guest.IndexOf('slmgr.vbs /cpky')
        $remove = $guest.IndexOf("Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SoftwareProtectionPlatform' -Name BackupProductKeyDefault")
        $cpky | Should -BeGreaterThan -1
        $remove | Should -BeGreaterThan $cpky
        $remove | Should -BeLessThan $guest.IndexOf('Restart-Computer -Force')
        $build = Get-Content -Raw (Join-Path $goldenDir 'build-golden.ps1')
        $build | Should -Match ([regex]::Escape('Copy-Item -LiteralPath "$w\Windows\System32\config\SOFTWARE"'))
        $build | Should -Match 'reg\.exe load \$hiveName \$hiveCopy'
        $build | Should -Match ([regex]::Escape('Select-Object -Skip 52 -First 15 | Where-Object { $_ -ne 0 }'))
        $build | Should -Match 'DigitalProductId still holds'
        $build | Should -Match 'BackupProductKeyDefault still holds a product key in clear text'
        # Unloaded and deleted whatever the result.
        $build | Should -Match 'finally \{\s+\[gc\]::Collect\(\)\s+& reg\.exe unload \$hiveName'
        $build | Should -Match ([regex]::Escape('Remove-Item -LiteralPath "$hiveCopy*" -Force'))
    }
    It 'the build script resolves -IsoPath to an absolute path before mounting it' {
        # Mount-DiskImage and Dismount-DiskImage need an absolute path.
        $text = Get-Content -Raw (Join-Path $goldenDir 'build-golden.ps1')
        $resolve = $text.IndexOf('$IsoPath = (Resolve-Path -LiteralPath $IsoPath).Path')
        $resolve | Should -BeGreaterThan -1
        $resolve | Should -BeLessThan $text.IndexOf('Mount-DiskImage -ImagePath')
    }
}
