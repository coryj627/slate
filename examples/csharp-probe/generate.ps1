# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Build slate_uniffi as a cdylib and generate the C# bindings for the
# W0-1 probe into generated/. Idempotent; safe to re-run.
#
# Respects CARGO_TARGET_DIR. On machines where the repo sits on a network
# share, point it at local disk before running (builds into the share are
# painfully slow).

$ErrorActionPreference = 'Stop'

$probeDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $probeDir '..\..')).Path

$targetDir = $env:CARGO_TARGET_DIR
if (-not $targetDir) { $targetDir = Join-Path $repoRoot 'target' }

Write-Host "==> cargo build -p slate-uniffi (debug)"
Push-Location $repoRoot
try {
    cargo build -p slate-uniffi
    if ($LASTEXITCODE -ne 0) { throw "cargo build failed ($LASTEXITCODE)" }
}
finally { Pop-Location }

$dll = Join-Path $targetDir 'debug\slate_uniffi.dll'
if (-not (Test-Path $dll)) { throw "expected cdylib not found: $dll" }

$genDir = Join-Path $probeDir 'generated'
New-Item -ItemType Directory -Force $genDir | Out-Null

Write-Host "==> uniffi-bindgen-cs --library $dll"
uniffi-bindgen-cs --library $dll --out-dir $genDir
if ($LASTEXITCODE -ne 0) { throw "uniffi-bindgen-cs failed ($LASTEXITCODE)" }

# The generated code resolves the native library by name; ship the DLL
# next to the binding source so the csproj can copy it to the output dir.
Copy-Item $dll (Join-Path $genDir 'slate_uniffi.dll') -Force

Write-Host "==> done: $genDir"
