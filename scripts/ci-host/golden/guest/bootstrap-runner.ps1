# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Guest task slate-runner-logon: the standard user `runner`, interactive
# session, at logon, Windows PowerShell 5.1. A no-op until the golden
# completion marker exists. Waits for the SYSTEM bootstrap's `ready`,
# junctions the cargo caches onto the cache volume, runs ONE job through
# run.cmd --jitconfig in this desktop session (UIA needs it), then shuts
# the VM down no matter what happened.
$ErrorActionPreference = 'Stop'
$runnerDir = 'C:\actions-runner'
$marker = Join-Path $env:USERPROFILE '.slate-golden-complete'
$logPath = Join-Path $runnerDir 'bootstrap-runner.log'

function Write-Log([string]$Message) {
    Add-Content -LiteralPath $logPath -Value ('{0:o} {1}' -f (Get-Date), $Message)
}

if (-not (Test-Path -LiteralPath $marker)) { exit 0 }

try {
    $ready = Join-Path $runnerDir 'ready'
    $errorFile = Join-Path $runnerDir 'bootstrap-error.txt'
    $deadline = (Get-Date).AddSeconds(360)
    while (-not (Test-Path -LiteralPath $ready) -and (Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $errorFile) { throw ('system bootstrap failed: ' + (Get-Content -LiteralPath $errorFile -Raw)) }
        Start-Sleep -Seconds 2
    }
    if (-not (Test-Path -LiteralPath $ready)) { throw 'ready signal did not arrive within 360 s' }

    $cacheRoot = $null
    foreach ($line in @(Get-Content -LiteralPath (Join-Path $runnerDir '.env') -ErrorAction SilentlyContinue)) {
        if ($line -like 'SLATE_CACHE_ROOT=*') { $cacheRoot = $line.Substring('SLATE_CACHE_ROOT='.Length) }
    }
    if ($cacheRoot) {
        foreach ($pair in @(@('registry', 'cargo\registry'), @('git', 'cargo\git'))) {
            $link = Join-Path (Join-Path $env:USERPROFILE '.cargo') $pair[0]
            $target = Join-Path $cacheRoot $pair[1]
            if (Test-Path -LiteralPath $link) {
                $item = Get-Item -LiteralPath $link -Force
                if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                    # A junction: delete the link only, never its target's contents.
                    [IO.Directory]::Delete($link)
                } else {
                    Remove-Item -LiteralPath $link -Recurse -Force
                }
            }
            New-Item -ItemType Junction -Path $link -Target $target | Out-Null
        }
        Write-Log "cargo junctions -> $cacheRoot"
    }

    $jit = Get-Content -LiteralPath (Join-Path $runnerDir 'jit.cfg') -Raw
    Write-Log 'starting runner'
    $process = Start-Process -FilePath (Join-Path $runnerDir 'run.cmd') -ArgumentList @('--jitconfig', $jit) `
        -WorkingDirectory $runnerDir -NoNewWindow -PassThru -Wait
    Write-Log ('runner exited {0}' -f $process.ExitCode)
} catch {
    Write-Log "ERROR: $_"
} finally {
    # No -Force: it first rewrites the file's attributes, which runner (R on
    # jit.cfg) may not do; the delete itself rides on the directory's rights.
    Remove-Item -LiteralPath (Join-Path $runnerDir 'jit.cfg') -ErrorAction SilentlyContinue
    & shutdown.exe /s /t 0 /c 'slate job finished'
}
