# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Runs the ci-host Pester suite with Pester 5 (the host's built-in 3.4 is
# ignored). Exits non-zero on any failure; used locally and by ci-host.yml.
param([string]$Path = $PSScriptRoot, [string]$Filter)

$ErrorActionPreference = 'Stop'
Import-Module Pester -MinimumVersion 5.5.0 -Force
$config = New-PesterConfiguration
$config.Run.Path = $Path
$config.Run.Exit = $true
$config.Output.Verbosity = 'Detailed'
if ($Filter) { $config.Filter.FullName = $Filter }
Invoke-Pester -Configuration $config
