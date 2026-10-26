# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# The whole-run witness of #1331: runs a command (a `dotnet test` of either
# Windows test project, the shell gate's included) and proves afterwards that
# it left the developer's device state, %LOCALAPPDATA%\Slate, exactly as it
# found it — the command and every SlateWindows.exe it started, which are
# grandchildren of `dotnet test` through the test host.
#
#   pwsh -NoProfile -File guard-device-state.ps1 [-Profile <folder>] -- <command> [<argument>...]
#
# It snapshots the profile (every file's relative path, length and last-write
# time; a folder that does not exist is empty, so creating it is a change),
# records the SlateWindows.exe processes already running, runs the command,
# waits up to 60 s for every SlateWindows.exe that was not running before to
# exit (a survivor could still write after the second snapshot, so it fails
# the run), snapshots again and prints every file created, changed or
# removed. Exit code: the command's own when it is nonzero (a failing run
# stays failing), else 1 when the profile changed or a launch survived, else
# 0. A usage error exits 2 having run nothing.
#
# No `param` block, no [CmdletBinding()] and no [Parameter()] on purpose: an
# advanced script hands the command line to PowerShell's parameter binder,
# which under `pwsh -File` refuses the `--` itself and binds dotnet's own
# `-c` and `-v` to its common parameters. This one reads $args, where every
# token after `--` arrives verbatim. (A `-name:value` token still arrives
# split under `-File`; no caller passes one.)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Usage {
    [Console]::Error.WriteLine('usage: pwsh -NoProfile -File guard-device-state.ps1 [-Profile <folder>] -- <command> [<argument>...]')
    exit 2
}

$tokens = @($args)
$index = 0
$profileFolder = $null
if ($tokens.Count -ge 2 -and $tokens[0] -ceq '-Profile') {
    $profileFolder = [string]$tokens[1]
    if ([string]::IsNullOrWhiteSpace($profileFolder)) { Write-Usage }
    $index = 2
}
if ($tokens.Count -le $index -or $tokens[$index] -cne '--') { Write-Usage }
$index++
if ($tokens.Count -le $index) { Write-Usage }
$command = @($tokens[$index..($tokens.Count - 1)] | ForEach-Object { [string]$_ })

if ($null -eq $profileFolder) {
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        [Console]::Error.WriteLine('guard-device-state: LOCALAPPDATA is not set, so there is no profile to guard')
        exit 2
    }
    $profileFolder = Join-Path $env:LOCALAPPDATA 'Slate'
}
$profileFolder = [IO.Path]::GetFullPath($profileFolder)
Write-Output "guard-device-state: profile $profileFolder"

# Every file by its relative path, with its length and last-write time, and
# every folder, the profile's own included as ".", so a folder created or
# removed is a change too.
function Get-Snapshot([string] $Folder) {
    $entries = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    if (-not [IO.Directory]::Exists($Folder)) { return $entries }
    $entries['.'] = 'folder'
    $options = [IO.EnumerationOptions]::new()
    $options.RecurseSubdirectories = $true
    $options.IgnoreInaccessible = $true
    $options.AttributesToSkip = [IO.FileAttributes]0
    foreach ($entry in [IO.DirectoryInfo]::new($Folder).EnumerateFileSystemInfos('*', $options)) {
        $relative = [IO.Path]::GetRelativePath($Folder, $entry.FullName)
        if ($entry -is [IO.FileInfo]) {
            $entries[$relative] = "$($entry.Length):$($entry.LastWriteTimeUtc.Ticks)"
        }
        else {
            $entries[$relative + [IO.Path]::DirectorySeparatorChar] = 'folder'
        }
    }
    return $entries
}

function Get-SlateProcessIds {
    return @(Get-Process -Name 'SlateWindows' -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
}

$before = Get-Snapshot $profileFolder
$runningBefore = Get-SlateProcessIds

$commandExit = 0
try {
    $rest = @()
    if ($command.Count -gt 1) { $rest = @($command[1..($command.Count - 1)]) }
    & $command[0] @rest
    $commandExit = $LASTEXITCODE
    if ($null -eq $commandExit) { $commandExit = 0 }
}
catch {
    [Console]::Error.WriteLine("guard-device-state: the command did not run: $($_.Exception.Message)")
    $commandExit = 1
}

# The shell gate's launches outlive `dotnet test` only by accident; give each
# up to 60 s in all to exit before the second snapshot.
$deadline = [DateTime]::UtcNow.AddSeconds(60)
$survivors = @()
do {
    $survivors = @(Get-SlateProcessIds | Where-Object { $runningBefore -notcontains $_ })
    if ($survivors.Count -eq 0) { break }
    Start-Sleep -Milliseconds 250
} while ([DateTime]::UtcNow -lt $deadline)

$after = Get-Snapshot $profileFolder
$changes = [Collections.Generic.List[string]]::new()
foreach ($entry in $after.GetEnumerator()) {
    $was = $null
    if (-not $before.TryGetValue($entry.Key, [ref]$was)) { $changes.Add("created $($entry.Key)") }
    elseif ($was -ne $entry.Value) { $changes.Add("changed $($entry.Key)") }
}
foreach ($key in $before.Keys) {
    if (-not $after.ContainsKey($key)) { $changes.Add("removed $key") }
}
$changes.Sort([StringComparer]::OrdinalIgnoreCase)

foreach ($change in $changes) { Write-Output "guard-device-state: $change" }
foreach ($id in $survivors) { Write-Output "guard-device-state: SlateWindows.exe pid $id was still running 60 s after the command ended" }
if ($changes.Count -gt 0) {
    Write-Output "guard-device-state: the run changed $($changes.Count) file(s) under $profileFolder"
}
elseif ($survivors.Count -eq 0) {
    Write-Output "guard-device-state: $profileFolder unchanged"
}

if ($commandExit -ne 0) { exit $commandExit }
if ($changes.Count -gt 0 -or $survivors.Count -gt 0) { exit 1 }
exit 0
