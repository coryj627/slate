# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# #1680: what holds the foreground while a job's tests run. A focus-dependent
# fact fails when another process takes the foreground from the test host
# (the home VMs' OneDrive sign-in window, #1355; something on Namespace's
# image). This samples the foreground window every few seconds and logs each
# change: the owning process, its id and the window's title, with the visible
# top-level windows of other processes at that moment.
#
#   pwsh -NoProfile -File foreground-sampler.ps1 -Out <log> [-StopFile <path>] [-IntervalMs 2000] [-MaxMinutes 55]
#
# It reads only: it never activates, focuses or sends input to any window.
# It stops when -StopFile exists, after -MaxMinutes, or when killed.

[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory)] [string] $Out,
    [string] $StopFile,
    [ValidateRange(100, 60000)] [int] $IntervalMs = 2000,
    [ValidateRange(1, 240)] [int] $MaxMinutes = 55
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -Namespace SlateCi -Name Foreground -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int max);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
public delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
'@

function Get-WindowTitle([IntPtr] $Window) {
    $text = [System.Text.StringBuilder]::new(512)
    [void][SlateCi.Foreground]::GetWindowText($Window, $text, $text.Capacity)
    $text.ToString()
}

function Get-OwnerName([uint32] $ProcessId) {
    try { (Get-Process -Id $ProcessId -ErrorAction Stop).ProcessName } catch { '?' }
}

function Write-Line([string] $Text) {
    $stamp = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
    [IO.File]::AppendAllText($Out, "$stamp $Text`n")
}

function Write-VisibleWindows {
    $self = $PID
    $windows = [System.Collections.Generic.List[string]]::new()
    $callback = [SlateCi.Foreground+EnumWindowsProc] {
        param([IntPtr] $window, [IntPtr] $unused)
        if ([SlateCi.Foreground]::IsWindowVisible($window)) {
            $title = Get-WindowTitle $window
            if ($title) {
                $processId = [uint32]0
                [void][SlateCi.Foreground]::GetWindowThreadProcessId($window, [ref] $processId)
                if ($processId -ne $self) {
                    $windows.Add("    visible pid=$processId name=$(Get-OwnerName $processId) title='$title'")
                }
            }
        }
        $true
    }
    [void][SlateCi.Foreground]::EnumWindows($callback, [IntPtr]::Zero)
    if ($windows.Count -gt 0) {
        [IO.File]::AppendAllText($Out, (($windows -join "`n") + "`n"))
    }
}

$directory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Out))
if ($directory) { [void][IO.Directory]::CreateDirectory($directory) }
Write-Line "sampler started pid=$PID interval=${IntervalMs}ms max=${MaxMinutes}min"

$deadline = [DateTime]::UtcNow.AddMinutes($MaxMinutes)
$last = $null
while ([DateTime]::UtcNow -lt $deadline) {
    if ($StopFile -and (Test-Path -LiteralPath $StopFile)) {
        Write-Line 'sampler stopped (stop file)'
        exit 0
    }
    $window = [SlateCi.Foreground]::GetForegroundWindow()
    $processId = [uint32]0
    if ($window -ne [IntPtr]::Zero) {
        [void][SlateCi.Foreground]::GetWindowThreadProcessId($window, [ref] $processId)
    }
    $title = if ($window -ne [IntPtr]::Zero) { Get-WindowTitle $window } else { '' }
    $key = "$window|$processId|$title"
    if ($key -ne $last) {
        Write-Line "foreground pid=$processId name=$(if ($processId) { Get-OwnerName $processId } else { 'none' }) title='$title'"
        Write-VisibleWindows
        $last = $key
    }
    Start-Sleep -Milliseconds $IntervalMs
}
Write-Line 'sampler stopped (time limit)'
