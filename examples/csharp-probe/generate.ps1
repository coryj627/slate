# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Build slate_uniffi as a cdylib and generate the C# bindings for the
# W0-1 probe into generated/. Idempotent; safe to re-run.
#
# Respects CARGO_TARGET_DIR. On machines where the repo sits on a network
# share, point it at local disk before running (builds into the share are
# painfully slow). -Locked passes --locked to cargo (the CI lanes'
# pin-everything convention; both Cargo.locks are committed).

param([switch]$Locked)

$ErrorActionPreference = 'Stop'
$cargoFlags = @()
if ($Locked) { $cargoFlags += '--locked' }

$probeDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $probeDir '..\..')).Path

$targetDir = $env:CARGO_TARGET_DIR
if (-not $targetDir) { $targetDir = Join-Path $repoRoot 'target' }

Write-Host "==> cargo build -p slate-uniffi (debug)"
Push-Location $repoRoot
try {
    cargo build -p slate-uniffi @cargoFlags
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

Write-Host "==> cargo build (csbindgen shim; emits ShimProbe/generated/NativeMethods.g.cs)"
Push-Location (Join-Path $probeDir 'shim')
try {
    cargo build @cargoFlags
    if ($LASTEXITCODE -ne 0) { throw "shim cargo build failed ($LASTEXITCODE)" }
}
finally { Pop-Location }

# The shim is its own workspace: without CARGO_TARGET_DIR it builds into
# shim/target, not the repo-root target dir.
$shimTargetDir = $env:CARGO_TARGET_DIR
if (-not $shimTargetDir) { $shimTargetDir = Join-Path $probeDir 'shim\target' }
$shimDll = Join-Path $shimTargetDir 'debug\slate_csabi_shim.dll'
if (-not (Test-Path $shimDll)) { throw "expected shim cdylib not found: $shimDll" }
Copy-Item $shimDll (Join-Path $probeDir 'ShimProbe\generated\slate_csabi_shim.dll') -Force

Write-Host "==> done: $genDir + ShimProbe/generated"
