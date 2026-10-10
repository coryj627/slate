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
function Get-Download([string]$Url, [string]$Path, [string]$Sha256) {
    Write-Log "download $Url"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $Url -OutFile $Path -UseBasicParsing
    if ($Sha256) {
        $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $Sha256.ToLowerInvariant()) { throw "sha256 mismatch for ${Path}: $actual" }
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

    # Visual Studio 2022 Build Tools: C++ x64 + ARM64 and the Windows 11 SDK.
    Get-Download $versions.vsBuildToolsUrl (Join-Path $dl 'vs_BuildTools.exe')
    Invoke-Installer (Join-Path $dl 'vs_BuildTools.exe') ('--quiet --wait --norestart --nocache ' +
        '--add Microsoft.VisualStudio.Workload.VCTools ' +
        '--add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 ' +
        '--add Microsoft.VisualStudio.Component.VC.Tools.ARM64 ' +
        '--add Microsoft.VisualStudio.Component.Windows11SDK.22621 --includeRecommended')

    # .NET SDK into C:\dotnet, owned by runner later, so setup-dotnet can
    # update it without elevation.
    Get-Download 'https://dot.net/v1/dotnet-install.ps1' (Join-Path $dl 'dotnet-install.ps1')
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dl 'dotnet-install.ps1') -Channel $versions.dotnetChannel -InstallDir 'C:\dotnet' -NoPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet-install exited $LASTEXITCODE" }
    [Environment]::SetEnvironmentVariable('DOTNET_ROOT', 'C:\dotnet', 'Machine')
    [Environment]::SetEnvironmentVariable('DOTNET_INSTALL_DIR', 'C:\dotnet', 'Machine')
    [Environment]::SetEnvironmentVariable('DOTNET_CLI_TELEMETRY_OPTOUT', '1', 'Machine')
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    [Environment]::SetEnvironmentVariable('Path', "C:\dotnet;$machinePath", 'Machine')

    Get-Download ('https://www.python.org/ftp/python/{0}/python-{0}-amd64.exe' -f $versions.pythonVersion) (Join-Path $dl 'python.exe')
    Invoke-Installer (Join-Path $dl 'python.exe') '/quiet InstallAllUsers=1 PrependPath=1 Include_test=0'

    Get-Download ('https://github.com/git-for-windows/git/releases/download/{0}/Git-{1}-64-bit.exe' -f $versions.gitTag, $versions.gitVersion) (Join-Path $dl 'git.exe')
    Invoke-Installer (Join-Path $dl 'git.exe') '/VERYSILENT /NORESTART /NOCANCEL /SP- /o:PathOption=Cmd'

    # The runner, hash-verified, plus the guest tasks' scripts and the module.
    $zip = Join-Path $dl 'runner.zip'
    Get-Download ('https://github.com/actions/runner/releases/download/v{0}/actions-runner-win-x64-{0}.zip' -f $versions.runnerVersion) $zip $versions.runnerSha256
    New-Item -ItemType Directory -Force -Path 'C:\actions-runner' | Out-Null
    Expand-Archive -Path $zip -DestinationPath 'C:\actions-runner' -Force
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
    Remove-Item -LiteralPath (Join-Path $root 'secrets.json') -Force
    Remove-Item -LiteralPath 'C:\Windows\Panther\unattend.xml' -Force -ErrorAction SilentlyContinue
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
