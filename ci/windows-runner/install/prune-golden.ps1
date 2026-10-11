#Requires -Version 7
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Deletes golden\win11-runner.prev.vhdx when, and only when, the module's
# Test-GoldenPrevRemovable holds for the CURRENT image: the orchestrator's
# journal records every lane green on it, it is sealed, and no VM from
# before it exists. Runs as SYSTEM from the slate-ci-golden-prune task
# (install/setup-host.ps1), daily: the orchestrator's account has read and
# execute on golden by policy. Reads the journal without side effects.
# Appends one line to the orchestrator log whenever a previous image exists
# (nothing when there is none). Never fails the task.
[CmdletBinding()]
param([string]$Root = 'C:\slate-ci')

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $Root 'bin' 'SlateCiHost.psm1') -Force
. (Join-Path $Root 'bin' 'adapters' 'HyperV.ps1')
$paths = Get-CiHostPaths -Root $Root
$log = {
    param($level, $message)
    Write-CiLog -Path (Join-Path $paths.LogDir ('orchestrator-{0}.log' -f (Get-Date).ToString('yyyy-MM-dd', [cultureinfo]::InvariantCulture))) -Level $level -Message $message
}
try {
    if (-not (Test-Path -LiteralPath $paths.GoldenPrevPath)) { exit 0 }
    $journal = Read-JournalSnapshot -Path $paths.JournalPath
    $removable = Test-GoldenPrevRemovable -Accepted $journal['GoldenAccepted'] `
        -CurrentStamp (Get-GoldenStamp -GoldenPath $paths.GoldenPath) `
        -CurrentSealed (Get-GoldenSealed -GoldenPath $paths.GoldenPath) `
        -VmCreationTimes @(Get-VmCreationTimes -VmDir $paths.VmDir)
    if (-not $removable) {
        & $log 'info' 'golden: the previous image stays (the current image is not yet accepted on every lane, is unsealed, or an older VM exists)'
        exit 0
    }
    Remove-SealedFile -Path $paths.GoldenPrevPath
    & $log 'info' 'golden: removed win11-runner.prev.vhdx (the current image is accepted on every lane)'
} catch {
    try { & $log 'error' "golden: prune failed: $_" } catch { }
}
exit 0
