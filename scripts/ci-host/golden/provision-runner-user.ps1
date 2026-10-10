# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Golden image, phase 2 (Windows PowerShell 5.1, as `runner`, via the
# logon task slate-provision-runner-user that provision-guest.ps1
# registers). rustup and uniffi-bindgen-cs live in the runner profile so
# `rustup target add` and `cargo install` work unelevated in jobs. Writes
# the completion marker LAST and shuts down; the host then verifies and
# seals the disk.

# The logon task stays in the image and fires in every job VM: once the
# marker exists this is a no-op, and must never reach the shutdown below.
if (Test-Path -LiteralPath (Join-Path $env:USERPROFILE '.slate-golden-complete')) { exit 0 }
$ErrorActionPreference = 'Stop'
$logPath = Join-Path $env:USERPROFILE 'provision-runner-user.log'
function Write-Log([string]$Message) { Add-Content -LiteralPath $logPath -Value ('{0:o} {1}' -f (Get-Date), $Message) }

try {
    $versions = Get-Content -Raw -LiteralPath 'C:\provision\versions.json' | ConvertFrom-Json
    $dl = Join-Path $env:TEMP 'slate-provision'
    New-Item -ItemType Directory -Force -Path $dl | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Write-Log 'phase 2 start'

    $rustupInit = Join-Path $dl 'rustup-init.exe'
    Invoke-WebRequest -Uri 'https://static.rust-lang.org/rustup/dist/x86_64-pc-windows-msvc/rustup-init.exe' -OutFile $rustupInit -UseBasicParsing
    $process = Start-Process -FilePath $rustupInit -ArgumentList @('-y', '--no-modify-path', '--profile', 'minimal',
        '--default-toolchain', $versions.rustToolchain, '--component', 'rustfmt', '--component', 'clippy',
        '--target', 'aarch64-pc-windows-msvc') -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ne 0) { throw "rustup-init exited $($process.ExitCode)" }
    $cargoBin = Join-Path $env:USERPROFILE '.cargo\bin'
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    [Environment]::SetEnvironmentVariable('Path', "$cargoBin;$userPath", 'User')
    $env:Path = "$cargoBin;$env:Path"
    Write-Log (& rustc --version)

    & cargo install --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag $versions.bindgenTag uniffi-bindgen-cs --locked
    if ($LASTEXITCODE -ne 0) { throw "cargo install uniffi-bindgen-cs exited $LASTEXITCODE" }
    Write-Log (& uniffi-bindgen-cs --version)

    Remove-Item -LiteralPath $dl -Recurse -Force
    Set-Content -LiteralPath (Join-Path $env:USERPROFILE '.slate-golden-complete') -Value ('{0:o}' -f (Get-Date))
    Write-Log 'golden complete'
} catch {
    Write-Log "ERROR: $_"
    Set-Content -LiteralPath (Join-Path $env:USERPROFILE 'provision-runner-user-error.txt') -Value ([string]$_)
} finally {
    & shutdown.exe /s /t 10 /c 'slate golden build finished'
}
