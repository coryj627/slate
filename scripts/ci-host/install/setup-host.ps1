#Requires -Version 7
#Requires -RunAsAdministrator
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# One-time, elevated host setup for the self-hosted Windows runner
# (idempotent: re-run after a reboot or to refresh C:\slate-ci\bin).
# Creates the unprivileged orchestrator account, the directory tree and
# ACLs, the isolated NAT switch and host firewall rule, the formatted
# cache parents, copies scripts/ci-host into place and registers the
# scheduled tasks. Everything after this runs as slate-ci-host.
[CmdletBinding()]
param(
    [string]$Root = 'C:\slate-ci',
    [string]$Account = 'slate-ci-host',
    [string]$SwitchName = 'slate-ci',
    [string]$NatPrefix = '10.77.0.0/24',
    [string]$Gateway = '10.77.0.1',
    [string[]]$CacheLanes = @('rust', 'app', 'model'),
    [int]$CacheGB = 60
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Import-Module (Join-Path $source 'SlateCiHost.psm1') -Force

Write-Host '1/9 Hyper-V'
$feature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All
if ($feature.State -ne 'Enabled') {
    Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All -NoRestart | Out-Null
    Write-Warning 'Hyper-V was enabled. Reboot, then run this script again.'
    exit 3
}

Write-Host '2/9 directories'
foreach ($dir in 'bin', 'golden', 'cache', 'vms', 'state', 'logs') {
    New-Item -ItemType Directory -Force -Path (Join-Path $Root $dir) | Out-Null
}

Write-Host "3/9 account $Account (Hyper-V Administrators only, no interactive logon)"
$password = New-RandomPassword -Length 32
$secure = ConvertTo-SecureString -String $password -AsPlainText -Force
if (Get-LocalUser -Name $Account -ErrorAction SilentlyContinue) {
    Set-LocalUser -Name $Account -Password $secure -PasswordNeverExpires $true
    # An administrator's reset (not a change) of a local account's password
    # leaves the DPAPI master key the stored token was encrypted with
    # unreadable: remove the token so the orchestrator reports it missing
    # instead of failing to decrypt it.
    $tokenPath = Join-Path $Root 'state\token.xml'
    if (Test-Path -LiteralPath $tokenPath) {
        Remove-Item -LiteralPath $tokenPath -Force
        Write-Warning "the password reset makes the stored token unreadable for ${Account}; it was removed: run store-token.ps1 again"
    }
} else {
    # -Description accepts at most 48 characters.
    New-LocalUser -Name $Account -Password $secure -PasswordNeverExpires -UserMayNotChangePassword -AccountNeverExpires `
        -Description 'Slate CI orchestrator (Hyper-V Administrators)' | Out-Null
}
if (-not (Get-LocalGroupMember -Group 'Hyper-V Administrators' -Member $Account -ErrorAction SilentlyContinue)) {
    Add-LocalGroupMember -Group 'Hyper-V Administrators' -Member $Account
}
if (Get-LocalGroupMember -Group 'Administrators' -Member $Account -ErrorAction SilentlyContinue) {
    Remove-LocalGroupMember -Group 'Administrators' -Member $Account
}
Set-LocalUserRights -Sid (Get-LocalUser -Name $Account).SID.Value

Write-Host '4/9 ACLs'
# The grant reaches everything below by inheritance. No /T: it would also
# stamp an explicit Modify on golden that /inheritance:r keeps; /grant:r
# leaves the account read-only there whatever it held before.
& icacls.exe $Root /grant "${Account}:(OI)(CI)M" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "icacls exited $LASTEXITCODE granting $Account on $Root" }
& icacls.exe (Join-Path $Root 'golden') /inheritance:r /grant 'Administrators:(OI)(CI)F' /grant 'SYSTEM:(OI)(CI)F' /grant:r "${Account}:(OI)(CI)RX" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "icacls exited $LASTEXITCODE restricting $Root\golden" }

Write-Host "5/9 switch $SwitchName with NAT $NatPrefix"
# WinNAT supports one NAT per host and a second one leaves both in an
# unknown state, so a NAT that another product created stops the setup.
$otherNat = @(Get-NetNat | Where-Object { $_.Name -ne $SwitchName })
if ($otherNat.Count -gt 0) {
    $found = ($otherNat | ForEach-Object { '{0} {1}' -f $_.Name, $_.InternalIPInterfaceAddressPrefix }) -join ', '
    throw "another NAT exists ($found): Windows supports one NAT per host, so $NatPrefix cannot be added beside it; resolve it, then re-run"
}
if (-not (Get-VMSwitch -Name $SwitchName -ErrorAction SilentlyContinue)) { New-VMSwitch -Name $SwitchName -SwitchType Internal | Out-Null }
$alias = "vEthernet ($SwitchName)"
$deadline = (Get-Date).AddSeconds(30)
while (-not (Get-NetAdapter -Name $alias -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
if (-not (Get-NetIPAddress -InterfaceAlias $alias -IPAddress $Gateway -ErrorAction SilentlyContinue)) {
    New-NetIPAddress -InterfaceAlias $alias -IPAddress $Gateway -PrefixLength 24 | Out-Null
}
if (-not (Get-NetNat -Name $SwitchName -ErrorAction SilentlyContinue)) {
    New-NetNat -Name $SwitchName -InternalIPInterfaceAddressPrefix $NatPrefix | Out-Null
}

Write-Host '6/9 host firewall: VM subnet may route through the host, never talk to it'
$ruleName = 'slate-ci: block VM subnet to host'
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Block -RemoteAddress $NatPrefix -LocalAddress $Gateway -InterfaceAlias $alias -Profile Any | Out-Null
}

Write-Host "7/9 cache parents ($CacheGB GB dynamic, NTFS, label slate-cache)"
foreach ($lane in $CacheLanes) {
    $path = Join-Path $Root "cache\$lane.vhdx"
    $genPath = Join-Path $Root "cache\$lane.gen"
    if ((Test-Path -LiteralPath $path) -and (Test-Path -LiteralPath $genPath)) { Write-Host "  $lane exists, kept"; continue }
    if (Test-Path -LiteralPath $path) {
        # The .gen file is written last: a parent without one was left by an
        # interrupted run (possibly unformatted or unlabelled), so rebuild it.
        Write-Warning "$lane.vhdx has no $lane.gen (an earlier run stopped part way); rebuilding it"
        Dismount-VHD -Path $path -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $path -Force
    }
    New-VHD -Path $path -SizeBytes ([int64]$CacheGB * 1GB) -Dynamic | Out-Null
    $disk = Mount-VHD -Path $path -Passthru | Get-Disk
    try {
        Initialize-Disk -Number $disk.Number -PartitionStyle GPT
        $partition = New-Partition -DiskNumber $disk.Number -UseMaximumSize -AssignDriveLetter
        Format-Volume -Partition $partition -FileSystem NTFS -NewFileSystemLabel 'slate-cache' -Confirm:$false | Out-Null
        New-Item -ItemType Directory -Force -Path ('{0}:\cache' -f $partition.DriveLetter) | Out-Null
    } finally {
        Dismount-VHD -Path $path
    }
    Set-Content -LiteralPath $genPath -Value '0' -NoNewline
    Write-Host "  $lane created"
}

Write-Host "8/9 bin <- $source"
& robocopy.exe $source (Join-Path $Root 'bin') /MIR /XD tests /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy exited $LASTEXITCODE" }
(& git -C $source rev-parse HEAD) | Set-Content -LiteralPath (Join-Path $Root 'bin\install-commit.txt')

Write-Host '9/9 scheduled tasks'
$pwsh = (Get-Command pwsh).Source
# A zero ExecutionTimeLimit means "no limit" (PT0S); the runbook verifies
# the task's "Stop the task if it runs longer than" box is unchecked.
$loopSettings = New-ScheduledTaskSettingsSet -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit (New-TimeSpan -Seconds 0) -MultipleInstances IgnoreNew -StartWhenAvailable
# Relaunch every minute if the loop has exited (RestartCount covers a
# failed start; a non-zero exit is not reliably retried). IgnoreNew
# keeps a single instance while one is running. No -RepetitionDuration:
# an empty duration means indefinitely ([TimeSpan]::MaxValue becomes
# P99999999DT23H59M59S, which Task Scheduler rejects as out of range).
$loopTrigger = New-ScheduledTaskTrigger -AtStartup
$loopTrigger.Repetition = (New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 1)).Repetition
Register-ScheduledTask -TaskName 'slate-ci-orchestrator' -Force `
    -Action (New-ScheduledTaskAction -Execute $pwsh -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$Root\bin\orchestrator.ps1`"" -WorkingDirectory "$Root\bin") `
    -Trigger $loopTrigger -User $Account -Password $password -RunLevel Limited -Settings $loopSettings | Out-Null
Register-ScheduledTask -TaskName 'slate-ci-store-token' -Force `
    -Action (New-ScheduledTaskAction -Execute $pwsh -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$Root\bin\install\store-token.ps1`" -Convert -Root `"$Root`"") `
    -User $Account -Password $password -RunLevel Limited -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 5)) | Out-Null
Remove-Variable password, secure

Write-Host ''
Write-Host 'Host setup complete. Next:'
Write-Host "  1. $Root\bin\install\store-token.ps1   (elevated; stores the PAT for $Account)"
Write-Host "  2. $source\golden\build-golden.ps1 -IsoPath <Win11 ISO>   (elevated; ~40 min)"
Write-Host "  3. Start-ScheduledTask slate-ci-orchestrator; Get-Content $Root\logs\orchestrator-*.log -Tail 20"
