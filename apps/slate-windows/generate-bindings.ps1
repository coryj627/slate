# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Build slate_uniffi as a cdylib and generate the C# bindings into
# src/SlateUniffi/generated/ (git-ignored, like the Swift bindings —
# CONTRIBUTING "The FFI bindings workflow"). Idempotent; safe to re-run.
#
# Prereqs: repo-pinned Rust toolchain (rust-toolchain.toml) and
# uniffi-bindgen-cs matching the workspace's uniffi minor:
#   cargo install --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag v0.11.0+v0.31.0 uniffi-bindgen-cs --locked
#
# Respects CARGO_TARGET_DIR. -Locked passes --locked to cargo (the CI
# lanes' pin-everything convention).

param([switch]$Locked)

$ErrorActionPreference = 'Stop'
$cargoFlags = @()
if ($Locked) { $cargoFlags += '--locked' }

$appDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $appDir '..\..')).Path

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

$genDir = Join-Path $appDir 'src\SlateUniffi\generated'
New-Item -ItemType Directory -Force $genDir | Out-Null

Write-Host "==> uniffi-bindgen-cs --library $dll"
uniffi-bindgen-cs --library $dll --out-dir $genDir --config (Join-Path $appDir 'uniffi.toml')
if ($LASTEXITCODE -ne 0) { throw "uniffi-bindgen-cs failed ($LASTEXITCODE)" }

# The generated code resolves the native library by name; ship the DLL
# next to the binding source so SlateUniffi.csproj copies it to every
# referencing project's output directory.
Copy-Item $dll (Join-Path $genDir 'slate_uniffi.dll') -Force

Write-Host "==> bindings generated into $genDir"
