# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Runs the ci-host Pester suite with Pester 5 (the host's built-in 3.4 is
# ignored). Exits non-zero on any failure; used locally and by
# windows-runner-tests.yml.
param([string]$Path = $PSScriptRoot, [string]$Filter)

$ErrorActionPreference = 'Stop'
# A Pester 5 the caller already imported stays (windows-runner-tests.yml
# imports its pinned version first); otherwise the newest from 5.5.0 loads.
if (-not (Get-Module Pester | Where-Object { $_.Version -ge [version]'5.5.0' })) {
    Import-Module Pester -MinimumVersion 5.5.0 -Force
}
$config = New-PesterConfiguration
$config.Run.Path = $Path
$config.Run.Exit = $true
$config.Output.Verbosity = 'Detailed'
if ($Filter) { $config.Filter.FullName = $Filter }
Invoke-Pester -Configuration $config
