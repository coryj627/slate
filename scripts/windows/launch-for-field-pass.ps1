# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Launches Slate for a screen-reader field pass (plan 44 §4 rule 4) on a
# device-state root of its own, so the pass never reads or writes the
# developer's %LOCALAPPDATA%\Slate (#1331):
#
#   pwsh -NoProfile -File scripts/windows/launch-for-field-pass.ps1 [-Vault '<path>[|<path>...]'] [-Seed] [-Keep] [-Exe <path>] [-Root <existing>]
#
# -Vault is ONE string: one vault folder, or several joined by '|'. `pwsh
# -File` cannot bind an array (its arguments arrive as literal strings, so
# `-Vault $a,$b` from PowerShell reaches this script as two tokens), and '|'
# is the one separator no Windows path can hold. PositionalBinding = $false
# turns that old spelling into a binder error before anything runs.
#
# Without -Root the root is a fresh $env:TEMP\slate-field-pass-<stamp>,
# deleted when the app exits unless -Keep; with -Root an existing root is
# reused, never deleted, so a pass that relaunches reads the device state it
# wrote. -Seed writes the vaults into the root's recent-vaults.json, the
# first the most recent. The app starts with the first vault as its one
# argument; SLATE_TEST_* seams set in the calling window reach it.

[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$Vault,
    [switch]$Seed,
    [switch]$Keep,
    [string]$Exe,
    [string]$Root
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Stop-Launch([string] $Reason) {
    [Console]::Error.WriteLine("launch-for-field-pass: $Reason")
    exit 2
}

# ---- the vault list, checked before a root is made or anything starts ----
$vaults = @()
if ($PSBoundParameters.ContainsKey('Vault')) {
    $entries = @($Vault.Split('|') | ForEach-Object { $_.Trim() })
    if ($entries.Count -eq 0 -or @($entries | Where-Object { $_ -eq '' }).Count -gt 0) {
        Stop-Launch "-Vault holds an empty entry: '$Vault' (several vaults are joined by '|', with no empty entry)"
    }
    if ($entries.Count -gt 8) {
        Stop-Launch "-Vault names $($entries.Count) vaults; the recents hold at most 8"
    }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $entries) {
        if (-not [IO.Path]::IsPathFullyQualified($entry)) {
            Stop-Launch "'$entry' is not a fully qualified path"
        }
        if (-not (Test-Path -LiteralPath $entry -PathType Container)) {
            Stop-Launch "'$entry' is not an existing folder"
        }
        $full = [IO.Path]::GetFullPath($entry)
        if (-not $seen.Add([IO.Path]::TrimEndingDirectorySeparator($full))) {
            Stop-Launch "'$entry' names a folder the list already holds"
        }
        $vaults += $full
    }
}
if ($Seed -and $vaults.Count -eq 0) {
    Stop-Launch '-Seed needs -Vault: there is nothing to seed'
}

# ---- the app, and the root it will run on, checked before anything is made ----
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $Exe) {
    $Exe = Join-Path $repoRoot 'apps\slate-windows\src\SlateWindows\bin\Release\net10.0-windows\SlateWindows.exe'
}
if (-not (Test-Path -LiteralPath $Exe -PathType Leaf)) {
    Stop-Launch "-Exe '$Exe' does not exist (build the Release app first)"
}
# Slate runs one instance per user and session, whatever its file is named:
# a launch while one is open hands its vault to that instance and exits, and
# the pass would run on the instance's own device state, the developer's
# profile. So no launch while any Slate runs.
$open = @(Get-Process -Name 'SlateWindows' -ErrorAction SilentlyContinue)
if ($open.Count -gt 0) {
    Stop-Launch "close Slate first: pid $(($open | ForEach-Object Id) -join ', ') would receive this launch and run it on your own device state"
}
$profileSlate = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::Combine(
    [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData), 'Slate'))
$deviceStores = @(
    'recent-vaults.json', 'window-state.json', 'preferences.json', 'command-palette-recents.json',
    'file-recents', 'sidebar-write-journal', 'logs')
$reused = $PSBoundParameters.ContainsKey('Root')
if ($reused) {
    if ([string]::IsNullOrWhiteSpace($Root)) {
        Stop-Launch "-Root is empty"
    }
    # The profile first, whether or not it exists yet: a field pass never
    # runs on the developer's own device state.
    $Root = [IO.Path]::GetFullPath($Root)
    $trimmed = [IO.Path]::TrimEndingDirectorySeparator($Root)
    if ($trimmed -ieq $profileSlate -or $trimmed.StartsWith($profileSlate + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Stop-Launch "-Root '$Root' is your own device state ($profileSlate); a field pass needs a root of its own"
    }
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        Stop-Launch "-Root '$Root' is not an existing folder"
    }
    $foreign = @(Get-ChildItem -LiteralPath $Root -Force | Where-Object { $deviceStores -notcontains $_.Name })
    if ($foreign.Count -gt 0) {
        Stop-Launch "-Root '$Root' holds more than device state: $(($foreign | ForEach-Object Name) -join ', ')"
    }
}
else {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $Root = Join-Path ([IO.Path]::GetTempPath()) "slate-field-pass-$stamp"
    $n = 2
    while (Test-Path -LiteralPath $Root) {
        $Root = Join-Path ([IO.Path]::GetTempPath()) "slate-field-pass-$stamp-$n"
        $n++
    }
    New-Item -ItemType Directory -Path $Root | Out-Null
}
$env:SLATE_LOCAL_APP_DATA = $Root
$env:SLATE_LOG_DIR = Join-Path $Root 'logs'

if ($Seed) {
    # RecentVault.FromPath's shape (RecentVaultsStore.cs): the full path, the
    # last segment as the display name, and strictly decreasing open times,
    # so the first vault is the most recent. -InputObject keeps one vault an
    # array: a piped one-element array would serialize as a bare object,
    # which the store reads as no recents.
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $records = for ($i = 0; $i -lt $vaults.Count; $i++) {
        $name = [IO.Path]::GetFileName([IO.Path]::TrimEndingDirectorySeparator($vaults[$i]))
        if (-not $name) { $name = $vaults[$i] }
        [ordered]@{ path = $vaults[$i]; displayName = $name; lastOpenedMs = $now - (1000 * $i) }
    }
    $json = ConvertTo-Json -InputObject @($records) -Depth 3
    [IO.File]::WriteAllText((Join-Path $Root 'recent-vaults.json'), $json, [Text.UTF8Encoding]::new($false))
}

Write-Output "Root: $Root"

# ---- the app: the first vault as its one argument, never split ----
$code = 0
try {
    $start = [Diagnostics.ProcessStartInfo]::new($Exe)
    $start.UseShellExecute = $false
    if ($vaults.Count -gt 0) { $start.ArgumentList.Add($vaults[0]) }
    $process = [Diagnostics.Process]::Start($start)
    $process.WaitForExit()
    $code = $process.ExitCode
}
finally {
    if (-not $Keep -and -not $reused) {
        Remove-Item -LiteralPath $Root -Recurse -Force -ErrorAction SilentlyContinue
    }
}
exit $code
