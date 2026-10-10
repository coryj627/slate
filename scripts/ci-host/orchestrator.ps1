#Requires -Version 7.4
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# The self-hosted Windows runner host loop. Runs as slate-ci-host from
# the slate-ci-orchestrator scheduled task (install/setup-host.ps1).
# Wires the GitHub and Hyper-V adapters into the SlateCiHost module and
# ticks every TickSeconds. -Once runs a single tick (smoke test).
[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'config.json'),
    [switch]$Once
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SlateCiHost.psm1') -Force
. (Join-Path $PSScriptRoot 'adapters' 'GitHub.ps1')
. (Join-Path $PSScriptRoot 'adapters' 'HyperV.ps1')

$config = Get-CiHostConfig -Path $ConfigPath
$log = {
    param($level, $message)
    Write-CiLog -Path (Join-Path $config.LogDir ('orchestrator-{0}.log' -f (Get-Date -Format 'yyyy-MM-dd'))) -Level $level -Message $message
}

$tokenPath = Join-Path $config.StateDir 'token.xml'
if (-not (Test-Path -LiteralPath $tokenPath)) {
    & $log 'error' "token missing at $tokenPath (run install/store-token.ps1)"
    exit 2
}
Initialize-GitHubAdapter -Owner $config.Owner -Repo $config.Repo -Token (Import-Clixml -LiteralPath $tokenPath)

$adapters = @{}
foreach ($set in (New-GitHubAdapters), (New-HyperVAdapters -Config $config)) {
    foreach ($key in $set.Keys) { $adapters[$key] = $set[$key] }
}
$adapters['Log'] = $log

$journalPath = Join-Path $config.StateDir 'journal.json'
$journal = Read-Journal -Path $journalPath
& $log 'info' "orchestrator start (pid $PID, user $env:USERNAME, config $ConfigPath)"
Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
Write-Journal -Path $journalPath -Journal $journal

do {
    try { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now ([datetimeoffset]::UtcNow) }
    catch { & $log 'error' "tick: $_" }
    try { Write-Journal -Path $journalPath -Journal $journal }
    catch { & $log 'error' "journal: $_" }
    if (-not $Once) { Start-Sleep -Seconds ([int]$config.TickSeconds) }
} while (-not $Once)
