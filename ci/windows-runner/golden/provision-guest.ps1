# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Golden image, phase 1 (Windows PowerShell 5.1, as the temporary admin
# `provision`, launched by the unattend's first-logon command). Installs
# the machine-wide toolchain, the runner and its two tasks, then hands
# the next boot to `runner`, whose logon task slate-provision-runner-user
# runs provision-runner-user.ps1.
# Pins come from versions.json; the auto-logon password from secrets.json
# (deleted at the end, together with the rendered unattend).
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = 'C:\provision'
$logPath = Join-Path $root 'provision.log'
$dl = Join-Path $root 'dl'

function Write-Log([string]$Message) {
    Add-Content -LiteralPath $logPath -Value ('{0:o} {1}' -f (Get-Date), $Message)
    Write-Host $Message
}
function Get-Download([string]$Url, [string]$Path, [string]$Sha256, [string]$Signer) {
    # Every download is checked one way or the other: a pinned SHA-256 for
    # a versioned URL, or the Authenticode signature for an evergreen one
    # (aka.ms/vs and dot.net serve a new build at every release, which no
    # hash can pin). Both run with administrator rights in the image.
    Write-Log "download $Url"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $Url -OutFile $Path -UseBasicParsing
    if ($Sha256) {
        $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $Sha256.ToLowerInvariant()) { throw "sha256 mismatch for ${Path}: $actual" }
    }
    if ($Signer) {
        $signature = Get-AuthenticodeSignature -LiteralPath $Path
        if ($signature.Status -ne 'Valid') { throw "signature of $Path is $($signature.Status): $($signature.StatusMessage)" }
        $subject = [string]$signature.SignerCertificate.Subject
        if ($subject -notmatch ('(^|, )O=' + [regex]::Escape($Signer) + '(,|$)')) { throw "$Path is signed by '$subject', not by $Signer" }
    }
}
function Invoke-Installer([string]$File, [string]$Arguments) {
    Write-Log "run $File $Arguments"
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) { throw "$File exited $($process.ExitCode)" }
}

try {
    $versions = Get-Content -Raw -LiteralPath (Join-Path $root 'versions.json') | ConvertFrom-Json
    $secrets = Get-Content -Raw -LiteralPath (Join-Path $root 'secrets.json') | ConvertFrom-Json
    New-Item -ItemType Directory -Force -Path $dl | Out-Null
    Write-Log 'provisioning start'

    # Machine policy: never sleep, long paths, no automatic updates,
    # Defender excludes the build trees, IPv6 off (the job switch denies it anyway).
    & powercfg.exe /change standby-timeout-ac 0 | Out-Null
    & powercfg.exe /change monitor-timeout-ac 0 | Out-Null
    & powercfg.exe /change hibernate-timeout-ac 0 | Out-Null
    & powercfg.exe /hibernate off | Out-Null
    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' -Name LongPathsEnabled -Value 1 -Type DWord
    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Force | Out-Null
    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Name NoAutoUpdate -Value 1 -Type DWord
    Add-MpPreference -ExclusionPath 'C:\actions-runner', 'C:\dotnet', 'C:\Users\runner\.cargo', 'C:\Users\runner\.rustup' -ErrorAction SilentlyContinue
    # Each job VM boots with a new network adapter, so the binding change
    # covers only this build; DisabledComponents 0xFF turns IPv6 off for
    # every adapter from the next boot. The key always exists: no New-Item
    # (-Force on an existing key deletes its values).
    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters' -Name DisabledComponents -Value 0xFF -Type DWord
    Get-NetAdapter | Disable-NetAdapterBinding -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue

    # Nothing in runner's session may pop over Slate: the shell gate types
    # into the app, and the foreground window gets the keystrokes, whatever
    # it is. OneDrive's sign-in window did, a few minutes after a job VM's
    # auto-logon (the gate's failure message names the foreground window).
    # OneDrive goes off by policy, which OneDrive.exe and OneDriveSetup.exe
    # both honour, and the Default profile loses the Run entry that installs
    # OneDrive at a user's first logon, before runner's. The hive is loaded
    # in place: no process holds it until that logon. Phase 2 clears
    # runner's own profile, and the build verifies both.
    $oneDrivePolicy = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive'
    if (-not (Test-Path -LiteralPath $oneDrivePolicy)) { New-Item -Path $oneDrivePolicy | Out-Null }
    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive' -Name DisableFileSyncNGSC -Value 1 -Type DWord
    $defaultHive = 'HKU\slate-default-profile'
    & reg.exe load $defaultHive 'C:\Users\Default\NTUSER.DAT' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "reg load exited $LASTEXITCODE for the Default profile" }
    try {
        Remove-ItemProperty -Path 'Registry::HKEY_USERS\slate-default-profile\Software\Microsoft\Windows\CurrentVersion\Run' -Name OneDriveSetup -ErrorAction SilentlyContinue
    } finally {
        [gc]::Collect()
        & reg.exe unload $defaultHive | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Log "reg unload exited $LASTEXITCODE for the Default profile (the restart releases it)" }
    }
    Write-Log 'OneDrive off by policy; Default profile Run\OneDriveSetup removed'

    # Visual Studio 2022 Build Tools: C++ x64 + ARM64 and the Windows 11 SDK.
    Get-Download -Url $versions.vsBuildToolsUrl -Path (Join-Path $dl 'vs_BuildTools.exe') -Signer 'Microsoft Corporation'
    Invoke-Installer (Join-Path $dl 'vs_BuildTools.exe') ('--quiet --wait --norestart --nocache ' +
        '--add Microsoft.VisualStudio.Workload.VCTools ' +
        '--add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 ' +
        '--add Microsoft.VisualStudio.Component.VC.Tools.ARM64 ' +
        '--add Microsoft.VisualStudio.Component.Windows11SDK.22621 --includeRecommended')

    # .NET SDK into C:\dotnet, owned by runner later, so setup-dotnet can
    # update it without elevation.
    Get-Download -Url 'https://dot.net/v1/dotnet-install.ps1' -Path (Join-Path $dl 'dotnet-install.ps1') -Signer 'Microsoft Corporation'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dl 'dotnet-install.ps1') -Channel $versions.dotnetChannel -InstallDir 'C:\dotnet' -NoPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet-install exited $LASTEXITCODE" }
    [Environment]::SetEnvironmentVariable('DOTNET_ROOT', 'C:\dotnet', 'Machine')
    [Environment]::SetEnvironmentVariable('DOTNET_INSTALL_DIR', 'C:\dotnet', 'Machine')
    [Environment]::SetEnvironmentVariable('DOTNET_CLI_TELEMETRY_OPTOUT', '1', 'Machine')
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    [Environment]::SetEnvironmentVariable('Path', "C:\dotnet;$machinePath", 'Machine')

    # Hash-pinned in versions.json (its sources object names where each was published).
    Get-Download -Url ('https://github.com/git-for-windows/git/releases/download/{0}/Git-{1}-64-bit.exe' -f $versions.gitTag, $versions.gitVersion) -Path (Join-Path $dl 'git.exe') -Sha256 $versions.gitSha256
    Invoke-Installer (Join-Path $dl 'git.exe') '/VERYSILENT /NORESTART /NOCANCEL /SP- /o:PathOption=Cmd'

    # PowerShell 7: every Windows lane runs its steps with `shell: pwsh`,
    # which the runner resolves from PATH (ADD_PATH=1). No Microsoft
    # Update hook, no telemetry.
    $pwshMsi = Join-Path $dl 'pwsh.msi'
    Get-Download -Url ('https://github.com/PowerShell/PowerShell/releases/download/v{0}/PowerShell-{0}-win-x64.msi' -f $versions.pwshVersion) -Path $pwshMsi -Sha256 $versions.pwshSha256
    Invoke-Installer 'msiexec.exe' ('/i "{0}" /quiet /norestart ADD_PATH=1 REGISTER_MANIFEST=1 USE_MU=0 ENABLE_MU=0 DISABLE_TELEMETRY=1' -f $pwshMsi)
    if (-not (Test-Path -LiteralPath 'C:\Program Files\PowerShell\7\pwsh.exe')) { throw 'PowerShell 7 did not install to C:\Program Files\PowerShell\7' }

    # The runner, hash-verified, plus the guest tasks' scripts and the module.
    $zip = Join-Path $dl 'runner.zip'
    Get-Download -Url ('https://github.com/actions/runner/releases/download/v{0}/actions-runner-win-x64-{0}.zip' -f $versions.runnerVersion) -Path $zip -Sha256 $versions.runnerSha256
    New-Item -ItemType Directory -Force -Path 'C:\actions-runner' | Out-Null
    Expand-Archive -Path $zip -DestinationPath 'C:\actions-runner' -Force
    # Python lives in the runner's tool cache, installed for all users now:
    # actions/setup-python's installer needs administrator rights, which a
    # job running as `runner` does not have (it waits on a UAC prompt for
    # ever). This is the build setup-python itself would install, from
    # actions/python-versions, so a job asking for 3.13 finds it in the
    # cache and installs nothing. Done before the tree is handed to runner.
    $toolCache = 'C:\actions-runner\_work\_tool'
    $pyZip = Join-Path $dl 'python-toolcache.zip'
    Get-Download -Url $versions.pythonToolcacheUrl -Path $pyZip -Sha256 $versions.pythonToolcacheSha256
    $pyDir = Join-Path $dl 'python-toolcache'
    Expand-Archive -Path $pyZip -DestinationPath $pyDir -Force
    New-Item -ItemType Directory -Force -Path $toolCache | Out-Null
    $env:AGENT_TOOLSDIRECTORY = $toolCache
    # setup.ps1 copies the installer by a path relative to the current
    # directory, so it runs from its own folder. Its output goes to files
    # and then into this log (a 2>&1 on a native command would be raised as
    # an error under Stop on 5.1), so a failure can be read on the host.
    $setupOut = Join-Path $dl 'python-toolcache.out.txt'
    $setupErr = Join-Path $dl 'python-toolcache.err.txt'
    $process = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $pyDir 'setup.ps1')) `
        -WorkingDirectory $pyDir -Wait -PassThru -NoNewWindow -RedirectStandardOutput $setupOut -RedirectStandardError $setupErr
    foreach ($f in $setupOut, $setupErr) {
        if (Test-Path -LiteralPath $f) { Get-Content -LiteralPath $f | ForEach-Object { Write-Log "  python-toolcache: $_" } }
    }
    if ($process.ExitCode -ne 0) { throw "python tool-cache setup.ps1 exited $($process.ExitCode)" }
    $pyHome = Join-Path $toolCache ('Python\{0}\x64' -f $versions.pythonVersion)
    if (-not (Test-Path -LiteralPath "$pyHome.complete")) { throw "python tool-cache install left no $pyHome.complete marker" }
    # On the machine PATH too, for anything that calls python without setup-python.
    [Environment]::SetEnvironmentVariable('Path', "$pyHome;$pyHome\Scripts;" + [Environment]::GetEnvironmentVariable('Path', 'Machine'), 'Machine')
    # Scripts SYSTEM will run live where runner cannot write them.
    New-Item -ItemType Directory -Force -Path 'C:\slate-guest' | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'guest\bootstrap-system.ps1'), (Join-Path $root 'guest\bootstrap-runner.ps1'), (Join-Path $root 'SlateCiHost.psm1') -Destination 'C:\slate-guest'
    # A native command's failure never throws, even under Stop: check each one.
    & icacls.exe 'C:\slate-guest' /inheritance:r /grant 'Administrators:(OI)(CI)F' /grant 'SYSTEM:(OI)(CI)F' /grant 'runner:(OI)(CI)RX' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls exited $LASTEXITCODE for C:\slate-guest" }
    foreach ($dir in 'C:\actions-runner', 'C:\dotnet') {
        & icacls.exe $dir /setowner runner /T /C | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "icacls exited $LASTEXITCODE for $dir (owner)" }
        & icacls.exe $dir /grant 'runner:(OI)(CI)F' /T /C | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "icacls exited $LASTEXITCODE for $dir (grant)" }
    }
    & icacls.exe $root /grant 'runner:(OI)(CI)RX' /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls exited $LASTEXITCODE for $root" }

    $systemAction = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\slate-guest\bootstrap-system.ps1'
    Register-ScheduledTask -TaskName 'slate-bootstrap-system' -Action $systemAction -Trigger (New-ScheduledTaskTrigger -AtStartup) `
        -Principal (New-ScheduledTaskPrincipal -UserId 'NT AUTHORITY\SYSTEM' -RunLevel Highest) `
        -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 1) -StartWhenAvailable) -Force | Out-Null
    $runnerAction = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File C:\slate-guest\bootstrap-runner.ps1'
    Register-ScheduledTask -TaskName 'slate-runner-logon' -Action $runnerAction -Trigger (New-ScheduledTaskTrigger -AtLogOn -User 'runner') `
        -Principal (New-ScheduledTaskPrincipal -UserId 'runner' -LogonType Interactive -RunLevel Limited) `
        -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 4) -StartWhenAvailable) -Force | Out-Null

    Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object {
        Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ServerAddresses ($versions.dns -split ',')
    }

    # Hand the next boot to runner: auto-logon and the per-user provisioning.
    # Not an HKLM RunOnce: Windows runs those only when an administrator logs
    # on, and runner is a standard user. The logon task stays in the image;
    # provision-runner-user.ps1 is a no-op once the completion marker exists.
    # Auto-logon fails once the password expires (42 days by default), and
    # every job VM would then sit at the logon screen until the host gives up.
    Set-LocalUser -Name runner -PasswordNeverExpires $true
    # Jobs create symbolic links (std::os::windows::fs::symlink_* in the
    # vault tests), which hosted runners can because they run as an
    # administrator: runner, a standard user, gets that one privilege. It
    # applies at runner's next logon, which the reboot below brings.
    Import-Module (Join-Path $root 'SlateCiHost.psm1') -Force
    $runnerSid = (New-Object System.Security.Principal.NTAccount('runner')).Translate([System.Security.Principal.SecurityIdentifier]).Value
    Set-LocalUserRights -Sid $runnerSid -Rights 'SeCreateSymbolicLinkPrivilege'
    $winlogon = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
    Set-ItemProperty -Path $winlogon -Name AutoAdminLogon -Value '1'
    Set-ItemProperty -Path $winlogon -Name DefaultUserName -Value 'runner'
    Set-ItemProperty -Path $winlogon -Name DefaultPassword -Value $secrets.runnerPassword
    Remove-ItemProperty -Path $winlogon -Name AutoLogonCount -ErrorAction SilentlyContinue
    $phase2Action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\provision\provision-runner-user.ps1'
    Register-ScheduledTask -TaskName 'slate-provision-runner-user' -Action $phase2Action -Trigger (New-ScheduledTaskTrigger -AtLogOn -User 'runner') `
        -Principal (New-ScheduledTaskPrincipal -UserId 'runner' -LogonType Interactive -RunLevel Limited) `
        -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 2) -StartWhenAvailable) -Force | Out-Null
    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name EnableLUA -Value 1 -Type DWord

    # Windows keeps an installed retail key readable by standard users in
    # DigitalProductId; clear it now (the image is activated) so no job can read it.
    & cscript.exe //B C:\Windows\System32\slmgr.vbs /cpky | Out-Null
    # /cpky clears the key bytes of DigitalProductId but leaves the
    # clear-text copy SoftwareProtectionPlatform\BackupProductKeyDefault,
    # which BUILTIN\Users can read; the build verifies both are gone.
    Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SoftwareProtectionPlatform' -Name BackupProductKeyDefault -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $root 'secrets.json') -Force
    # Setup keeps copies of the processed answer file, product key included
    # (it blanks the passwords, not the key): the one it ran from, and its
    # caches under Panther\Unattend and Sysprep\Panther.
    Remove-Item -LiteralPath 'C:\Windows\Panther\unattend.xml' -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath 'C:\Windows\Panther\Unattend' -Recurse -Force -ErrorAction SilentlyContinue
    Get-ChildItem -LiteralPath 'C:\Windows\System32\Sysprep\Panther' -Filter '*.xml' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $dl -Recurse -Force
    # Auto-logon is runner's now and nothing needs the temporary admin again;
    # disabling the logged-on account takes effect at its next logon.
    Disable-LocalUser -Name 'provision'
    Write-Log 'phase 1 complete; rebooting into runner'
    Restart-Computer -Force
} catch {
    Write-Log "ERROR: $_"
    Set-Content -LiteralPath (Join-Path $root 'provision-error.txt') -Value ([string]$_)
    & shutdown.exe /s /t 10 /c 'slate provisioning failed'
}
