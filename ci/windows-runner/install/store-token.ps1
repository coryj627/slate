#Requires -Version 7
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Stores the orchestrator's fine-grained PAT DPAPI-bound to slate-ci-host
# without ever logging on as that account: the elevated caller writes
# the token to an ACL'd temp file and starts the on-demand task
# slate-ci-store-token, which runs this script with -Convert as
# slate-ci-host, Export-Clixml's a SecureString (DPAPI for that user)
# and deletes the plaintext. The caller verifies both.
[CmdletBinding()]
param([switch]$Convert, [string]$Root = 'C:\slate-ci')

$ErrorActionPreference = 'Stop'
$plainPath = Join-Path $Root 'state\token.txt'
$xmlPath = Join-Path $Root 'state\token.xml'

if ($Convert) {
    if (-not (Test-Path -LiteralPath $plainPath)) { throw "nothing to convert at $plainPath" }
    $token = (Get-Content -Raw -LiteralPath $plainPath).Trim()
    ConvertTo-SecureString -String $token -AsPlainText -Force | Export-Clixml -LiteralPath $xmlPath
    Remove-Item -LiteralPath $plainPath -Force
    exit 0
}

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'run this elevated' }
$task = Get-ScheduledTask -TaskName 'slate-ci-store-token' -ErrorAction SilentlyContinue
if (-not $task) { throw 'run install/setup-host.ps1 first' }
# The account is whatever setup-host.ps1 (-Account) registered the task for.
$account = [string]$task.Principal.UserId
if (-not $account) { throw 'the task slate-ci-store-token names no account; re-run install/setup-host.ps1' }

$secure = Read-Host -Prompt 'Fine-grained PAT for coryj627/slate (hidden)' -AsSecureString
$plain = [System.Net.NetworkCredential]::new('', $secure).Password.Trim()
if ($plain -notmatch '^github_pat_[A-Za-z0-9_]{20,}$') { throw 'that is not a fine-grained PAT (github_pat_...)' }
# Restricted while still empty, so the token is only ever written under
# this ACL (Administrators and the task's account), never the inherited
# one. The account deletes the file after reading it: Remove-Item -Force
# needs delete and write-attributes on the file, because the Modify it
# holds on state\ does not include delete-child.
New-Item -ItemType File -Force -Path $plainPath | Out-Null
try {
    & icacls.exe $plainPath /inheritance:r /grant 'Administrators:F' /grant "${account}:(R,D,WA)" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls exited $LASTEXITCODE restricting $plainPath" }
    Set-Content -LiteralPath $plainPath -Value $plain -NoNewline -Encoding ascii
    Remove-Variable plain, secure

    Start-ScheduledTask -TaskName 'slate-ci-store-token'
    # The task consumes the plaintext (it deletes the file once token.xml is
    # written), so wait for that and not for the task's state, which can
    # still read Ready a moment after Start-ScheduledTask returns.
    $deadline = (Get-Date).AddSeconds(60)
    while ((Test-Path -LiteralPath $plainPath) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
    $info = Get-ScheduledTaskInfo -TaskName 'slate-ci-store-token'
    if (Test-Path -LiteralPath $plainPath) {
        Remove-Item -LiteralPath $plainPath -Force
        throw "the conversion task did not consume the token within 60 s (LastTaskResult $($info.LastTaskResult)); plaintext deleted, nothing stored"
    }
} finally {
    # Whatever failed (Ctrl+C included), the plaintext never outlives this run.
    if (Test-Path -LiteralPath $plainPath) { Remove-Item -LiteralPath $plainPath -Force }
}
if (-not (Test-Path -LiteralPath $xmlPath)) { throw 'token.xml missing after conversion' }
Write-Host "token stored at $xmlPath (readable only by $account via DPAPI)"
