#Requires -Version 7.4
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# The self-hosted Windows runner host loop. Runs as slate-ci-host from
# the slate-ci-orchestrator scheduled task (install/setup-host.ps1).
# Wires the GitHub and Hyper-V adapters into the SlateCiHost module and
# ticks every TickSeconds. -Once runs a single tick (smoke test).
# Exit codes: 2 = startup failed (logged), 3 = another instance runs.
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
# The GitHub adapter warns through this when the PAT's hourly budget runs low.
$script:GhLowBudgetWarning = { param($message) & $log 'warn' $message }

# One loop per host: the journal, its .tmp file and the slots are not
# shared, so a second instance (a manual run beside the task) refuses.
$script:instanceMutex = [System.Threading.Mutex]::new($false, 'Global\slate-ci-orchestrator')
if (-not $script:instanceMutex.WaitOne(0)) {
    & $log 'error' 'another orchestrator instance holds the mutex; exiting'
    # Continue overrides the script-wide Stop: a terminating Write-Error
    # would end the script with exit code 1 before exit 3 could run.
    Write-Error 'another orchestrator instance holds the mutex' -ErrorAction Continue
    exit 3
}

try {
    # Startup: any failure is logged and ends the process with exit 2;
    # the scheduled task starts it again. exit still runs the finally.
    try {
        $tokenPath = Join-Path $config.StateDir 'token.xml'
        if (-not (Test-Path -LiteralPath $tokenPath)) {
            & $log 'error' "token missing at $tokenPath (run install/store-token.ps1)"
            exit 2
        }
        # No job may burn its retries against a golden disk that is missing
        # or still being built: build-golden.ps1 marks it read-only only once
        # it is sealed. Exit, and the per-minute relaunch retries until then.
        if (-not ((Test-Path -LiteralPath $config.GoldenPath) -and (Get-Item -LiteralPath $config.GoldenPath).IsReadOnly)) {
            & $log 'error' "golden disk missing or not sealed (read-only) at $($config.GoldenPath) (run golden/build-golden.ps1); exiting until it is sealed"
            exit 2
        }
        Initialize-GitHubAdapter -Owner $config.Owner -Repo $config.Repo -Token (Import-Clixml -LiteralPath $tokenPath)

        $adapters = @{}
        foreach ($set in (New-GitHubAdapters -RoutedWorkflows @($config.RoutedWorkflows)), (New-HyperVAdapters -Config $config)) {
            foreach ($key in $set.Keys) { $adapters[$key] = $set[$key] }
        }
        $adapters['Log'] = $log

        $journalPath = Join-Path $config.StateDir 'journal.json'
        $journal = Read-Journal -Path $journalPath
        & $log 'info' "orchestrator start (pid $PID, user $env:USERNAME, config $ConfigPath)"
        Invoke-StartupSweep -Config $config -Journal $journal -Adapters $adapters
        Write-Journal -Path $journalPath -Journal $journal
    } catch {
        & $log 'error' "startup: $_"
        exit 2
    }

    do {
        try { Invoke-OrchestratorTick -Config $config -Journal $journal -Adapters $adapters -Now ([datetimeoffset]::UtcNow) }
        catch { & $log 'error' "tick: $_" }
        try { Write-Journal -Path $journalPath -Journal $journal }
        catch { & $log 'error' "journal: $_" }
        if (-not $Once) { Start-Sleep -Seconds ([int]$config.TickSeconds) }
    } while (-not $Once)
} finally {
    $script:instanceMutex.ReleaseMutex()
    $script:instanceMutex.Dispose()
}
