#Requires -Version 7
#Requires -RunAsAdministrator
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Builds the read-only golden disk without running Windows Setup: apply
# install.wim to a new VHDX with DISM, make it bootable, inject the
# rendered unattend and the provisioning payload, boot it once on a
# switch with Internet (Default Switch), wait for the two provisioning
# phases to shut it down, verify the completion marker, seal the file.
# The product key is read from a hidden prompt and never written to disk
# outside the rendered unattend inside the image (deleted by phase 1).
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$IsoPath,
    [string]$OutPath = 'C:\slate-ci\golden\win11-runner.vhdx',
    [int]$SizeGB = 120,
    [string]$SwitchName = 'Default Switch',
    [string]$ImageName = 'Windows 11 Pro',
    [int]$TimeoutMinutes = 150,
    [switch]$Rotate
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force
$goldenDir = $PSScriptRoot
$vmName = 'slate-golden-build'
# Mount-DiskImage and Dismount-DiskImage need an absolute path.
$IsoPath = (Resolve-Path -LiteralPath $IsoPath).Path

if (Get-VM -Name $vmName -ErrorAction SilentlyContinue) { throw "VM $vmName exists; remove it first" }
if ($Rotate -and (Test-Path -LiteralPath $OutPath)) {
    # Nothing moves while a runner VM exists: its disk is a differencing
    # child of the live image.
    if (Get-VM -Name 'slate-win-*' -ErrorAction SilentlyContinue) { throw 'runner VMs exist; stop the loop and let them finish first' }
    # Only a sealed image becomes the rollback; an unsealed one is a failed
    # build's leftover and must be removed by hand first (runbook: "The
    # golden build fails"). This is the one elevated moment in a refresh.
    if (-not (Get-Item -LiteralPath $OutPath).IsReadOnly) { throw "$OutPath is not sealed (a failed build left it); remove it first, see the runbook 'The golden build fails'" }
    $outDir = Split-Path -Parent $OutPath
    $prevPath = Join-Path $outDir 'win11-runner.prev.vhdx'
    $rotatingPath = Join-Path $outDir 'win11-runner.prev.rotating.vhdx'
    if (Test-Path -LiteralPath $rotatingPath) { throw "$rotatingPath exists (an earlier rotation stopped part-way): rename it back to win11-runner.prev.vhdx if that file is missing, else delete it, then retry" }
    # The older rollback is set aside, not deleted, until the live image has
    # become the rollback: a failed rename puts it back. It then goes
    # through the shared helper.
    if (Test-Path -LiteralPath $prevPath) { Rename-Item -LiteralPath $prevPath -NewName 'win11-runner.prev.rotating.vhdx' }
    try {
        Rename-Item -LiteralPath $OutPath -NewName 'win11-runner.prev.vhdx'
    } catch {
        if (Test-Path -LiteralPath $rotatingPath) { Rename-Item -LiteralPath $rotatingPath -NewName 'win11-runner.prev.vhdx' }
        throw
    }
    Remove-SealedFile -Path $rotatingPath
    Write-Host "Rotated the sealed image to $prevPath"
}
if (Test-Path -LiteralPath $OutPath) { throw "$OutPath exists; move it aside to rebuild, or pass -Rotate for a sealed image" }

$keySecure = Read-Host -Prompt 'Windows 11 Pro product key (XXXXX-XXXXX-XXXXX-XXXXX-XXXXX, hidden)' -AsSecureString
$productKey = [System.Net.NetworkCredential]::new('', $keySecure).Password.Trim().ToUpperInvariant()
$provisionPassword = New-RandomPassword -Length 24
$runnerPassword = New-RandomPassword -Length 24
$unattend = Expand-UnattendTemplate -TemplatePath (Join-Path $goldenDir 'unattend.xml') -ProductKey $productKey -ProvisionPassword $provisionPassword -RunnerPassword $runnerPassword
Remove-Variable productKey, keySecure

Write-Host "Mounting $IsoPath"
$image = Mount-DiskImage -ImagePath $IsoPath -PassThru
try {
    $isoLetter = ($image | Get-Volume).DriveLetter
    $wim = Get-ChildItem -LiteralPath "${isoLetter}:\sources" | Where-Object { $_.Name -in 'install.wim', 'install.esd' } | Select-Object -First 1
    if (-not $wim) { throw 'install.wim/install.esd not found in the ISO' }
    $index = (Get-WindowsImage -ImagePath $wim.FullName | Where-Object { $_.ImageName -eq $ImageName }).ImageIndex
    if (-not $index) { throw "image '$ImageName' not found in $($wim.FullName)" }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutPath) | Out-Null
    New-VHD -Path $OutPath -SizeBytes ([int64]$SizeGB * 1GB) -Dynamic | Out-Null
    $disk = Mount-VHD -Path $OutPath -Passthru | Get-Disk
    try {
        Initialize-Disk -Number $disk.Number -PartitionStyle GPT
        # The ESP is created as basic data, formatted, then retyped and given
        # a letter for bcdboot: Format-Volume is not relied on to format a
        # partition that already has the ESP type (Microsoft's
        # Convert-WindowsImage uses this order on Windows 10 and later).
        $efi = New-Partition -DiskNumber $disk.Number -Size 260MB -GptType '{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}'
        Format-Volume -Partition $efi -FileSystem FAT32 -NewFileSystemLabel 'System' -Confirm:$false | Out-Null
        $efi | Set-Partition -GptType '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}'
        $efi | Add-PartitionAccessPath -AssignDriveLetter
        $efi = Get-Partition -DiskNumber $disk.Number -PartitionNumber $efi.PartitionNumber
        if (-not $efi.DriveLetter) { throw 'the EFI system partition got no drive letter' }
        New-Partition -DiskNumber $disk.Number -Size 16MB -GptType '{e3c9e316-0b5c-4db8-817d-f92df00215ae}' | Out-Null
        $windows = New-Partition -DiskNumber $disk.Number -UseMaximumSize -GptType '{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}' -AssignDriveLetter
        Format-Volume -Partition $windows -FileSystem NTFS -NewFileSystemLabel 'Windows' -Confirm:$false | Out-Null
        $w = '{0}:' -f $windows.DriveLetter
        $s = '{0}:' -f $efi.DriveLetter
        Write-Host "Applying '$ImageName' (index $index) to $w (several minutes)"
        Expand-WindowsImage -ImagePath $wim.FullName -Index $index -ApplyPath "$w\" | Out-Null
        & bcdboot.exe "$w\Windows" /s $s /f UEFI | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "bcdboot exited $LASTEXITCODE" }
        New-Item -ItemType Directory -Force -Path "$w\Windows\Panther", "$w\provision\guest" | Out-Null
        Set-Content -LiteralPath "$w\Windows\Panther\unattend.xml" -Value $unattend -Encoding utf8
        Copy-Item -LiteralPath (Join-Path $goldenDir 'provision-guest.ps1'), (Join-Path $goldenDir 'provision-runner-user.ps1'), (Join-Path $goldenDir 'versions.json') -Destination "$w\provision"
        Copy-Item -Path (Join-Path $goldenDir 'guest' '*.ps1') -Destination "$w\provision\guest"
        Copy-Item -LiteralPath (Join-Path $goldenDir '..' 'SlateCiHost.psm1') -Destination "$w\provision"
        @{ runnerPassword = $runnerPassword } | ConvertTo-Json | Set-Content -LiteralPath "$w\provision\secrets.json" -Encoding utf8
    } finally {
        Dismount-VHD -Path $OutPath
    }
} finally {
    Dismount-DiskImage -ImagePath $IsoPath | Out-Null
}
Remove-Variable unattend, provisionPassword, runnerPassword

Write-Host 'Booting the build VM (phase 1 installs ~3 GB of tooling; phase 2 builds uniffi-bindgen-cs)'
New-VM -Name $vmName -Generation 2 -MemoryStartupBytes 12GB -VHDPath $OutPath -SwitchName $SwitchName | Out-Null
Set-VM -Name $vmName -ProcessorCount 4 -StaticMemory -AutomaticCheckpointsEnabled $false -CheckpointType Disabled -AutomaticStopAction TurnOff
Set-VMFirmware -VMName $vmName -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows
Set-VMKeyProtector -VMName $vmName -NewLocalKeyProtector
Enable-VMTPM -VMName $vmName
Set-VMVideo -VMName $vmName -ResolutionType Single -HorizontalResolution 1920 -VerticalResolution 1080
Start-VM -Name $vmName
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
while ((Get-VM -Name $vmName).State -ne 'Off') {
    if ((Get-Date) -gt $deadline) { throw "build VM still running after $TimeoutMinutes min; inspect with: vmconnect.exe localhost $vmName" }
    Start-Sleep -Seconds 30
    Write-Host ('  {0:HH:mm:ss} provisioning ({1})' -f (Get-Date), (Get-VM -Name $vmName).State)
}

Write-Host 'Verifying the image'
$disk = Mount-VHD -Path $OutPath -ReadOnly -Passthru | Get-Disk
try {
    $volume = $disk | Get-Partition | Get-Volume | Where-Object { $_.DriveLetter -and (Test-Path -LiteralPath ('{0}:\Windows\System32' -f $_.DriveLetter)) } | Select-Object -First 1
    if (-not $volume) { throw 'Windows volume not found in the built disk' }
    $w = '{0}:' -f $volume.DriveLetter
    if (Test-Path -LiteralPath "$w\provision\provision.log") { Get-Content -LiteralPath "$w\provision\provision.log" -Tail 15 }
    if (Test-Path -LiteralPath "$w\provision\provision-error.txt") { throw ('phase 1 failed: ' + (Get-Content -LiteralPath "$w\provision\provision-error.txt" -Raw)) }
    if (Test-Path -LiteralPath "$w\Users\runner\provision-runner-user-error.txt") { throw ('phase 2 failed: ' + (Get-Content -LiteralPath "$w\Users\runner\provision-runner-user-error.txt" -Raw)) }
    if (-not (Test-Path -LiteralPath "$w\Users\runner\.slate-golden-complete")) { throw 'completion marker C:\Users\runner\.slate-golden-complete missing' }
    if (Test-Path -LiteralPath "$w\provision\secrets.json") { throw 'secrets.json still present in the image' }
    # The answer file Setup ran from and the copies it caches (the product
    # key survives in them; phase 1 removes all three).
    foreach ($cached in "$w\Windows\Panther\unattend.xml", "$w\Windows\Panther\Unattend", "$w\Windows\System32\Sysprep\Panther\unattend.xml") {
        if (Test-Path -LiteralPath $cached) { throw "rendered unattend still present in the image: $cached" }
    }
    # The product key must not survive in the image's registry: the key
    # bytes of DigitalProductId cleared (slmgr /cpky) and no clear-text
    # SoftwareProtectionPlatform\BackupProductKeyDefault, which Users can
    # read. Read from a copy of the SOFTWARE hive, loaded under a temporary
    # name; the runbook's manual check does the same.
    $hiveCopy = Join-Path $env:TEMP ('golden-verify-' + [guid]::NewGuid().ToString('N'))
    Copy-Item -LiteralPath "$w\Windows\System32\config\SOFTWARE" -Destination $hiveCopy
    $hiveName = 'HKLM\slate-golden-verify'
    & reg.exe load $hiveName $hiveCopy | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "reg load exited $LASTEXITCODE" }
    try {
        $cv = 'HKLM:\slate-golden-verify\Microsoft\Windows NT\CurrentVersion'
        $id = (Get-ItemProperty -Path $cv -ErrorAction SilentlyContinue).DigitalProductId
        $keyBytes = @($id | Select-Object -Skip 52 -First 15 | Where-Object { $_ -ne 0 }).Count
        if ($keyBytes -ne 0) { throw "DigitalProductId still holds $keyBytes key bytes: slmgr /cpky did not clear the product key" }
        $backup = (Get-ItemProperty -Path "$cv\SoftwareProtectionPlatform" -ErrorAction SilentlyContinue).BackupProductKeyDefault
        if ($backup) { throw 'SoftwareProtectionPlatform\BackupProductKeyDefault still holds a product key in clear text' }
        # Nothing may pop over Slate during the shell gate: OneDrive off by
        # policy (phase 1), or its sign-in window takes the keystrokes.
        $oneDrive = (Get-ItemProperty -Path 'HKLM:\slate-golden-verify\Policies\Microsoft\Windows\OneDrive' -ErrorAction SilentlyContinue).DisableFileSyncNGSC
        if ($oneDrive -ne 1) { throw 'OneDrive is not off by policy (Policies\Microsoft\Windows\OneDrive\DisableFileSyncNGSC is not 1): its sign-in window would take the shell gate''s keystrokes' }
    } finally {
        [gc]::Collect()
        & reg.exe unload $hiveName | Out-Null
        Remove-Item -LiteralPath "$hiveCopy*" -Force -ErrorAction SilentlyContinue
    }
    # And nothing starts OneDrive from runner's profile at logon (phase 2
    # removes the Run entries its setup leaves). Same method, on a copy of
    # the profile hive.
    $userHiveCopy = Join-Path $env:TEMP ('golden-verify-' + [guid]::NewGuid().ToString('N'))
    Copy-Item -LiteralPath "$w\Users\runner\NTUSER.DAT" -Destination $userHiveCopy
    $userHiveName = 'HKLM\slate-golden-verify-runner'
    & reg.exe load $userHiveName $userHiveCopy | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "reg load exited $LASTEXITCODE for runner's profile hive" }
    try {
        $run = Get-ItemProperty -Path 'HKLM:\slate-golden-verify-runner\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue
        foreach ($name in 'OneDrive', 'OneDriveSetup') {
            if ($run -and $run.PSObject.Properties[$name]) { throw "runner's Run key still starts $name at logon: its window would take the shell gate's keystrokes" }
        }
    } finally {
        [gc]::Collect()
        & reg.exe unload $userHiveName | Out-Null
        Remove-Item -LiteralPath "$userHiveCopy*" -Force -ErrorAction SilentlyContinue
    }
} finally {
    Dismount-VHD -Path $OutPath
}
Remove-VM -Name $vmName -Force
Set-ItemProperty -LiteralPath $OutPath -Name IsReadOnly -Value $true
Write-Host "Golden image ready and read-only: $OutPath"
