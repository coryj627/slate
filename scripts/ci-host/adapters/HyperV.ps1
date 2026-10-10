#Requires -Version 7
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Hyper-V adapter for the self-hosted runner host: the only file that
# touches Hyper-V. Runs as slate-ci-host (Hyper-V Administrators, not an
# admin). The pure helpers are unit-tested with stubbed cmdlets; the
# wrappers are exercised by the host integration pass in
# docs/runbooks/self-hosted-windows-runner.md.

function Get-ExtendedAclRules {
    # Higher weight is evaluated first. Deny every private, CGNAT
    # (Tailscale), link-local and IPv6 range in both directions, then
    # allow the rest (the Internet). ARP is not IP, so the NAT gateway
    # still resolves as a next hop while 10.77.0.1 itself is unreachable.
    [CmdletBinding()]
    param([string[]]$DenyRanges = @('10.0.0.0/8', '172.16.0.0/12', '192.168.0.0/16', '100.64.0.0/10', '169.254.0.0/16'))
    $rules = @()
    $weight = 200
    foreach ($range in ($DenyRanges + @('::/0'))) {
        foreach ($direction in 'Outbound', 'Inbound') {
            $rules += @{ Direction = $direction; Action = 'Deny'; RemoteIPAddress = $range; Weight = $weight }
            $weight--
        }
    }
    foreach ($direction in 'Outbound', 'Inbound') {
        $rules += @{ Direction = $direction; Action = 'Allow'; RemoteIPAddress = '0.0.0.0/0'; Weight = 1 }
    }
    return $rules
}

function ConvertTo-VmStateLabel {
    [CmdletBinding()]
    param([string]$State)
    switch ($State) {
        'Off'      { return 'Off' }
        'Running'  { return 'Running' }
        'Starting' { return 'Running' }
        'Stopping' { return 'Running' }
        default    { return 'Other' }
    }
}

function Get-CacheGeneration {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CacheDir, [Parameter(Mandatory)][string]$Lane)
    $path = Join-Path $CacheDir "$Lane.gen"
    if (-not (Test-Path -LiteralPath $path)) { return 0 }
    return [int](Get-Content -Raw -LiteralPath $path).Trim()
}

function Set-CacheGeneration {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CacheDir, [Parameter(Mandatory)][string]$Lane, [Parameter(Mandatory)][int]$Value)
    Set-Content -LiteralPath (Join-Path $CacheDir "$Lane.gen") -Value ([string]$Value) -NoNewline
}

function New-RunnerVm {
    # Differencing children of the read-only golden disk and of the lane's
    # cache parent; Gen2, Secure Boot, fresh vTPM, static memory, no
    # checkpoints, TurnOff on host shutdown, port ACLs from the table.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$Lane, [Parameter(Mandatory)]$Config)
    # Every function here that changes state sets Stop first: it must fail
    # loudly whatever the caller's preference is. A cmdlet failure is
    # non-terminating, and a VM without its vTPM or ACLs must never start.
    $ErrorActionPreference = 'Stop'
    $dir = Join-Path $Config.VmDir $Name
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $os = Join-Path $dir 'os.vhdx'
    New-VHD -Path $os -ParentPath $Config.GoldenPath -Differencing | Out-Null
    $cache = $null
    $generation = 0
    if ($Config.Lanes[$Lane].Cache) {
        $generation = Get-CacheGeneration -CacheDir $Config.CacheDir -Lane $Lane
        $cache = Join-Path $dir 'cache.vhdx'
        New-VHD -Path $cache -ParentPath (Join-Path $Config.CacheDir "$Lane.vhdx") -Differencing | Out-Null
    }
    New-VM -Name $Name -Generation 2 -MemoryStartupBytes ([int64]$Config.MemoryGB * 1GB) -VHDPath $os -SwitchName $Config.SwitchName -Path $dir | Out-Null
    Set-VM -Name $Name -ProcessorCount ([int]$Config.Vcpu) -StaticMemory -AutomaticStopAction TurnOff -AutomaticStartAction Nothing -AutomaticCheckpointsEnabled $false -CheckpointType Disabled
    Set-VMFirmware -VMName $Name -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows
    try {
        Set-VMKeyProtector -VMName $Name -NewLocalKeyProtector
        Enable-VMTPM -VMName $Name
    } catch {
        throw "${Name}: vTPM could not be enabled ($_). Grant slate-ci-host the key-protector right or fix the Hyper-V host guardian local mode; see the runbook."
    }
    Set-VMVideo -VMName $Name -ResolutionType Single -HorizontalResolution 1920 -VerticalResolution 1080
    if ($cache) { Add-VMHardDiskDrive -VMName $Name -Path $cache }
    foreach ($rule in Get-ExtendedAclRules) { Add-VMNetworkAdapterExtendedAcl -VMName $Name @rule }
    return @{ Dir = $dir; CachePath = $cache; ForkGeneration = $generation }
}

function Start-RunnerVm {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name)
    $ErrorActionPreference = 'Stop'
    Start-VM -Name $Name | Out-Null
}

function Get-RunnerVmState {
    # Missing only when Hyper-V answered and has no VM by this name. Get-VM
    # -Name reports that as InvalidArgument (ErrorId InvalidParameter) with
    # the name as the target object. Every other failure is Unknown, which
    # the orchestrator must not treat as gone: a permission error, the
    # management service down or timing out, or its ObjectNotFound ("the
    # object was not found ... verify that the Virtual Machine Management
    # service is running"), which carries no target.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name)
    try {
        $vm = Get-VM -Name $Name -ErrorAction Stop
    } catch {
        if ($_.CategoryInfo.Category -eq 'InvalidArgument' -and [string]$_.TargetObject -eq $Name) { return 'Missing' }
        return 'Unknown'
    }
    if ($null -eq $vm) { return 'Missing' }
    return (ConvertTo-VmStateLabel -State ([string]$vm.State))
}

function Get-RunnerVmHeartbeat {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name)
    $service = Get-VMIntegrationService -VMName $Name -Name 'Heartbeat' -ErrorAction Stop
    # The operational-status enum, not the localisable description text.
    if ([string]$service.PrimaryOperationalStatus -eq 'Ok') { return 'OK' }
    return 'NoContact'
}

function Send-RunnerVmKvp {
    # Host-to-guest key/value pairs land in the guest registry under
    # HKLM\SOFTWARE\Microsoft\Virtual Machine\External. Each Data value is
    # at most 1024 characters (the module chunks at 1000).
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][System.Collections.IDictionary]$Items)
    $ErrorActionPreference = 'Stop'
    $ns = 'root\virtualization\v2'
    $vmms = Get-CimInstance -Namespace $ns -ClassName Msvm_VirtualSystemManagementService
    $vm = Get-CimInstance -Namespace $ns -ClassName Msvm_ComputerSystem -Filter "ElementName='$Name'"
    if ($null -eq $vm) { throw "KVP: VM $Name not found in WMI" }
    $serializer = [Microsoft.Management.Infrastructure.Serialization.CimSerializer]::Create()
    $payload = @()
    foreach ($key in $Items.Keys) {
        $item = New-CimInstance -Namespace $ns -ClassName Msvm_KvpExchangeDataItem -ClientOnly -Property @{
            Name = [string]$key; Data = [string]$Items[$key]; Source = [uint16]0
        }
        $bytes = $serializer.Serialize($item, [Microsoft.Management.Infrastructure.Serialization.InstanceSerializationOptions]::None)
        $payload += [System.Text.Encoding]::Unicode.GetString($bytes)
    }
    $result = Invoke-CimMethod -InputObject $vmms -MethodName AddKvpItems -Arguments @{ TargetSystem = $vm; DataItems = [string[]]$payload }
    if ($result.ReturnValue -eq 4096) {
        $job = $result.Job
        $deadline = (Get-Date).AddSeconds(60)
        while ($job.JobState -lt 7 -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 250
            $job = Get-CimInstance -InputObject $job
        }
        if ($job.JobState -ne 7) { throw "KVP: AddKvpItems job state $($job.JobState): $($job.ErrorDescription)" }
    } elseif ($result.ReturnValue -ne 0) {
        throw "KVP: AddKvpItems returned $($result.ReturnValue)"
    }
}

function Stop-RunnerVmForce {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name)
    Stop-VM -Name $Name -TurnOff -Force -ErrorAction SilentlyContinue | Out-Null
}

function Remove-RunnerVm {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [string]$Dir)
    $ErrorActionPreference = 'Stop'
    if ($null -ne (Get-VM -Name $Name -ErrorAction SilentlyContinue)) { Remove-VM -Name $Name -Force }
    if ($Dir -and (Test-Path -LiteralPath $Dir)) { Remove-Item -LiteralPath $Dir -Recurse -Force }
}

function Get-RunnerVmNames {
    [CmdletBinding()]
    param()
    return @(Get-VM | Where-Object { $_.Name -like 'slate-win-*' } | ForEach-Object { $_.Name })
}

function Clear-RunnerVmDirs {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$VmDir, [string[]]$ActiveNames = @())
    $ErrorActionPreference = 'Stop'
    if (-not (Test-Path -LiteralPath $VmDir)) { return }
    foreach ($entry in Get-ChildItem -LiteralPath $VmDir -Directory) {
        if ($ActiveNames -notcontains $entry.Name) { Remove-Item -LiteralPath $entry.FullName -Recurse -Force }
    }
}

function Merge-RunnerCache {
    # Child -> parent merge, then the generation counter moves. Only the
    # orchestrator (single process) writes these files.
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config, [Parameter(Mandatory)][string]$Lane, [Parameter(Mandatory)][string]$ChildPath)
    $ErrorActionPreference = 'Stop'
    $parent = Join-Path $Config.CacheDir "$Lane.vhdx"
    Merge-VHD -Path $ChildPath -DestinationPath $parent
    $next = (Get-CacheGeneration -CacheDir $Config.CacheDir -Lane $Lane) + 1
    Set-CacheGeneration -CacheDir $Config.CacheDir -Lane $Lane -Value $next
    return $next
}

function Remove-RunnerCache {
    [CmdletBinding()]
    param([string]$ChildPath)
    $ErrorActionPreference = 'Stop'
    if ($ChildPath -and (Test-Path -LiteralPath $ChildPath)) { Remove-Item -LiteralPath $ChildPath -Force }
}

function New-HyperVAdapters {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Config)
    # GetNewClosure() runs a block in a new dynamic module that sees only
    # its captured locals and the global scope, not the scope this file
    # was dot-sourced into (a Pester container, or a script run with &),
    # so a block could not call these functions by name. They are
    # captured as locals too and invoked with &; each still runs in the
    # scope that defined it.
    $commands = @{}
    foreach ($command in 'New-RunnerVm', 'Start-RunnerVm', 'Get-RunnerVmState', 'Get-RunnerVmHeartbeat', 'Send-RunnerVmKvp',
        'Stop-RunnerVmForce', 'Remove-RunnerVm', 'Get-RunnerVmNames', 'Clear-RunnerVmDirs', 'Get-CacheGeneration',
        'Merge-RunnerCache', 'Remove-RunnerCache') {
        $commands[$command] = Get-Command -Name $command -CommandType Function -ErrorAction Stop
    }
    $adapters = @{
        NewVm         = { param($name, $lane) & $commands['New-RunnerVm'] -Name $name -Lane $lane -Config $Config }
        StartVm       = { param($name) & $commands['Start-RunnerVm'] -Name $name }
        GetVmState    = { param($name) & $commands['Get-RunnerVmState'] -Name $name }
        GetHeartbeat  = { param($name) & $commands['Get-RunnerVmHeartbeat'] -Name $name }
        SendKvp       = { param($name, $items) & $commands['Send-RunnerVmKvp'] -Name $name -Items $items }
        StopVmForce   = { param($name) & $commands['Stop-RunnerVmForce'] -Name $name }
        RemoveVm      = { param($name, $dir) & $commands['Remove-RunnerVm'] -Name $name -Dir $dir }
        ListVms       = { & $commands['Get-RunnerVmNames'] }
        CleanVmDirs   = { param($active) & $commands['Clear-RunnerVmDirs'] -VmDir $Config.VmDir -ActiveNames @($active) }
        GetGeneration = { param($lane) & $commands['Get-CacheGeneration'] -CacheDir $Config.CacheDir -Lane $lane }
        CommitCache   = { param($lane, $child) & $commands['Merge-RunnerCache'] -Config $Config -Lane $lane -ChildPath $child }
        DiscardCache  = { param($child) & $commands['Remove-RunnerCache'] -ChildPath $child }
    }
    # Bind $Config and the commands into each scriptblock: the module
    # invokes them long after this function has returned.
    foreach ($key in @($adapters.Keys)) { $adapters[$key] = $adapters[$key].GetNewClosure() }
    return $adapters
}
