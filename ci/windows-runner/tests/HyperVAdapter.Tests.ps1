# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    # Stubs with the parameters the assertions inspect; Pester mocks them.
    # [CmdletBinding()] so the adapter's -ErrorAction arguments bind as
    # common parameters (a plain function would reject them).
    function global:New-VHD { [CmdletBinding()] param($Path, $ParentPath, [switch]$Differencing, $SizeBytes, [switch]$Dynamic) }
    function global:New-VM { [CmdletBinding()] param($Name, $Generation, $MemoryStartupBytes, $VHDPath, $SwitchName, $Path) }
    function global:Set-VM { [CmdletBinding()] param($Name, $ProcessorCount, [switch]$StaticMemory, $AutomaticStopAction, $AutomaticStartAction, $AutomaticCheckpointsEnabled, $CheckpointType) }
    function global:Set-VMFirmware { [CmdletBinding()] param($VMName, $EnableSecureBoot, $SecureBootTemplate) }
    function global:Set-VMKeyProtector { [CmdletBinding()] param($VMName, [switch]$NewLocalKeyProtector) }
    function global:Enable-VMTPM { [CmdletBinding()] param($VMName) }
    function global:Set-VMVideo { [CmdletBinding()] param($VMName, $ResolutionType, $HorizontalResolution, $VerticalResolution) }
    function global:Add-VMHardDiskDrive { [CmdletBinding()] param($VMName, $Path) }
    function global:Add-VMNetworkAdapterExtendedAcl { [CmdletBinding()] param($VMName, $Direction, $Action, $RemoteIPAddress, $Weight) }
    function global:Start-VM { [CmdletBinding()] param($Name) }
    function global:Get-VM { [CmdletBinding()] param($Name) }
    function global:Stop-VM { [CmdletBinding()] param($Name, [switch]$TurnOff, [switch]$Force) }
    function global:Remove-VM { [CmdletBinding()] param($Name, [switch]$Force) }
    function global:Get-VMIntegrationService { [CmdletBinding()] param($VMName, $Name) }
    function global:Merge-VHD { [CmdletBinding()] param($Path, $DestinationPath) }
    . (Join-Path $PSScriptRoot '..' 'adapters' 'HyperV.ps1')

    $script:config = @{
        GoldenPath = 'C:\slate-ci\golden\win11-runner.vhdx'
        CacheDir   = Join-Path $TestDrive 'cache'
        VmDir      = Join-Path $TestDrive 'vms'
        SwitchName = 'slate-ci'
        Vcpu       = 4
        MemoryGB   = 12
        Lanes      = @{ app = @{ Cache = $true; MaxMinutes = 100 }; shell = @{ Cache = $false; MaxMinutes = 30 } }
    }
    New-Item -ItemType Directory -Force -Path $script:config.CacheDir, $script:config.VmDir | Out-Null
}

Describe 'Get-ExtendedAclRules' {
    It 'denies every private, CGNAT, link-local, multicast, broadcast, this-network and IPv6 range both ways above a catch-all allow' {
        # Multicast and broadcast are not private ranges, yet they reach the
        # host itself (mDNS, LLMNR, SSDP, NetBIOS) and the catch-all allow
        # would pass them.
        $ranges = '10.0.0.0/8', '172.16.0.0/12', '192.168.0.0/16', '100.64.0.0/10', '169.254.0.0/16',
            '224.0.0.0/4', '255.255.255.255/32', '0.0.0.0/8', '::/0'
        $rules = @(Get-ExtendedAclRules)
        # 18 deny rules (nine ranges, both directions), then the two allow-alls.
        $rules.Count | Should -Be 20
        $denies = @($rules | Where-Object Action -eq 'Deny')
        $denies.Count | Should -Be 18
        foreach ($range in $ranges) {
            foreach ($direction in 'Outbound', 'Inbound') {
                @($denies | Where-Object { $_.RemoteIPAddress -eq $range -and $_.Direction -eq $direction }).Count | Should -Be 1
            }
        }
        # In that order, highest weight first, each weight used once.
        @($denies | ForEach-Object { $_.RemoteIPAddress } | Select-Object -Unique) | Should -Be $ranges
        @($denies | ForEach-Object { $_.Weight }) | Should -Be @(200..183)
        $allows = @($rules | Where-Object Action -eq 'Allow')
        $allows.Count | Should -Be 2
        foreach ($a in $allows) { $a.Weight | Should -Be 1; $a.RemoteIPAddress | Should -Be '0.0.0.0/0' }
    }
}

Describe 'ConvertTo-VmStateLabel' {
    It 'maps Hyper-V states to the three the orchestrator understands' {
        ConvertTo-VmStateLabel -State 'Off' | Should -Be 'Off'
        ConvertTo-VmStateLabel -State 'Running' | Should -Be 'Running'
        ConvertTo-VmStateLabel -State 'Starting' | Should -Be 'Running'
        ConvertTo-VmStateLabel -State 'Stopping' | Should -Be 'Running'
        ConvertTo-VmStateLabel -State 'Saved' | Should -Be 'Other'
        ConvertTo-VmStateLabel -State 'Paused' | Should -Be 'Other'
    }
}

Describe 'cache generation files' {
    It 'reads 0 when absent, then round-trips' {
        Get-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' | Should -Be 0
        Set-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' -Value 5
        Get-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' | Should -Be 5
    }
}

Describe 'New-RunnerVm' {
    BeforeEach {
        Mock New-VHD {}; Mock New-VM {}; Mock Set-VM {}; Mock Set-VMFirmware {}; Mock Set-VMKeyProtector {}
        Mock Enable-VMTPM {}; Mock Set-VMVideo {}; Mock Add-VMHardDiskDrive {}; Mock Add-VMNetworkAdapterExtendedAcl {}
    }
    It 'forks the golden and the lane cache, defines a 4 vCPU / 12 GB Gen2 VM with vTPM and all 20 ACL rules' {
        Set-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' -Value 7
        $r = New-RunnerVm -Name 'slate-win-app-deadbeef' -Lane 'app' -Config $config
        $r.Dir | Should -Be (Join-Path $config.VmDir 'slate-win-app-deadbeef')
        $r.CachePath | Should -Be (Join-Path $r.Dir 'cache.vhdx')
        $r.ForkGeneration | Should -Be 7
        Should -Invoke New-VHD -Times 1 -ParameterFilter { $Path -eq (Join-Path $r.Dir 'os.vhdx') -and $ParentPath -eq 'C:\slate-ci\golden\win11-runner.vhdx' -and $Differencing }
        Should -Invoke New-VHD -Times 1 -ParameterFilter { $Path -eq $r.CachePath -and $ParentPath -eq (Join-Path $config.CacheDir 'app.vhdx') -and $Differencing }
        Should -Invoke New-VM -Times 1 -ParameterFilter { $Name -eq 'slate-win-app-deadbeef' -and $Generation -eq 2 -and $MemoryStartupBytes -eq 12GB -and $SwitchName -eq 'slate-ci' }
        Should -Invoke Set-VM -Times 1 -ParameterFilter { $ProcessorCount -eq 4 -and $StaticMemory -and $AutomaticStopAction -eq 'TurnOff' -and $CheckpointType -eq 'Disabled' }
        Should -Invoke Set-VMKeyProtector -Times 1 -ParameterFilter { $NewLocalKeyProtector }
        Should -Invoke Enable-VMTPM -Times 1
        Should -Invoke Set-VMVideo -Times 1 -ParameterFilter { $HorizontalResolution -eq 1920 -and $VerticalResolution -eq 1080 }
        Should -Invoke Add-VMHardDiskDrive -Times 1 -ParameterFilter { $Path -eq $r.CachePath }
        Should -Invoke Add-VMNetworkAdapterExtendedAcl -Times 20 -Exactly
        Should -Invoke Add-VMNetworkAdapterExtendedAcl -Times 18 -Exactly -ParameterFilter { $Action -eq 'Deny' }
        foreach ($range in '224.0.0.0/4', '255.255.255.255/32', '0.0.0.0/8') {
            Should -Invoke Add-VMNetworkAdapterExtendedAcl -Times 2 -Exactly -ParameterFilter { $Action -eq 'Deny' -and $RemoteIPAddress -eq $range }
        }
    }
    It 'gives the shell lane no cache disk' {
        $r = New-RunnerVm -Name 'slate-win-shell-deadbeef' -Lane 'shell' -Config $config
        $r.CachePath | Should -BeNullOrEmpty
        $r.ForkGeneration | Should -Be 0
        Should -Invoke New-VHD -Times 1 -Exactly
        Should -Invoke Add-VMHardDiskDrive -Times 0
    }
    It 'fails loudly when the vTPM cannot be enabled' {
        Mock Enable-VMTPM { throw 'no key protector' }
        { New-RunnerVm -Name 'slate-win-app-cafecafe' -Lane 'app' -Config $config } | Should -Throw '*vTPM*'
    }
    It 'fails loudly when an ACL rule cannot be applied, even under a Continue error preference' {
        # A real cmdlet failure is non-terminating: under the caller's
        # Continue it would hand back a VM without its network isolation.
        # The runner's global Stop would hide that, so the test sets Continue.
        $ErrorActionPreference = 'Continue'
        Mock Add-VMNetworkAdapterExtendedAcl { Write-Error 'acl failed' }
        { New-RunnerVm -Name 'slate-win-app-acl00000' -Lane 'app' -Config $config } | Should -Throw '*acl failed*'
    }
}

Describe 'Get-RunnerVmState' {
    It 'reports Missing for an absent VM and maps states otherwise' {
        # Get-VM -Name writes this record for a name it cannot find:
        # InvalidArgument, ErrorId InvalidParameter, the name as the target.
        Mock Get-VM { Write-Error -Message 'Hyper-V was unable to find a virtual machine with name "x".' -Category InvalidArgument -ErrorId 'InvalidParameter' -TargetObject 'x' }
        Get-RunnerVmState -Name 'x' | Should -Be 'Missing'
        Mock Get-VM { $null }
        Get-RunnerVmState -Name 'x' | Should -Be 'Missing'
        Mock Get-VM { [pscustomobject]@{ State = 'Off' } }
        Get-RunnerVmState -Name 'x' | Should -Be 'Off'
        Mock Get-VM { [pscustomobject]@{ State = 'Saved' } }
        Get-RunnerVmState -Name 'x' | Should -Be 'Other'
    }
    It 'reports Unknown when Get-VM fails for any reason other than not-found' {
        Mock Get-VM { Write-Error -Message 'denied' -Category PermissionDenied }
        Get-RunnerVmState -Name 'x' | Should -Be 'Unknown'
        Mock Get-VM { throw 'boom' }
        Get-RunnerVmState -Name 'x' | Should -Be 'Unknown'
        # What an unelevated non-member gets: NotSpecified, ErrorId Unspecified, no target.
        Mock Get-VM { Write-Error -Message 'You do not have the required permission to complete this task.' -Category NotSpecified -ErrorId 'Unspecified' }
        Get-RunnerVmState -Name 'x' | Should -Be 'Unknown'
        # ObjectNotFound is the management stack failing ("the object was not
        # found ... verify that the Virtual Machine Management service is
        # running"), not the name lookup; it carries no target.
        Mock Get-VM { Write-Error -Message 'Hyper-V encountered an error trying to access an object because the object was not found.' -Category ObjectNotFound -ErrorId 'ObjectNotFound' }
        Get-RunnerVmState -Name 'x' | Should -Be 'Unknown'
        # An invalid-argument failure that is not about this name.
        Mock Get-VM { Write-Error -Message 'invalid parameter' -Category InvalidArgument -ErrorId 'InvalidParameter' }
        Get-RunnerVmState -Name 'x' | Should -Be 'Unknown'
    }
}

Describe 'Get-RunnerVmHeartbeat' {
    It 'is OK only when the integration service says OK' {
        Mock Get-VMIntegrationService { [pscustomobject]@{ PrimaryOperationalStatus = 'Ok' } }
        Get-RunnerVmHeartbeat -Name 'x' | Should -Be 'OK'
        Mock Get-VMIntegrationService { [pscustomobject]@{ PrimaryOperationalStatus = 'NoContact' } }
        Get-RunnerVmHeartbeat -Name 'x' | Should -Be 'NoContact'
    }
}

Describe 'ConvertFrom-GuestKvpXml' {
    BeforeAll {
        # Two Msvm_KvpExchangeDataItem instances as Hyper-V embeds them (CIM-XML)
        # in Msvm_KvpExchangeComponent.GuestExchangeItems.
        $script:otherItem = '<INSTANCE CLASSNAME="Msvm_KvpExchangeDataItem"><PROPERTY NAME="Caption" PROPAGATED="true" TYPE="string"></PROPERTY><PROPERTY NAME="Data" TYPE="string"><VALUE>10.77.0.11</VALUE></PROPERTY><PROPERTY NAME="Description" PROPAGATED="true" TYPE="string"></PROPERTY><PROPERTY NAME="ElementName" PROPAGATED="true" TYPE="string"></PROPERTY><PROPERTY NAME="Name" TYPE="string"><VALUE>other.item</VALUE></PROPERTY><PROPERTY NAME="Source" TYPE="uint16"><VALUE>2</VALUE></PROPERTY></INSTANCE>'
        $script:errorItem = '<INSTANCE CLASSNAME="Msvm_KvpExchangeDataItem"><PROPERTY NAME="Caption" PROPAGATED="true" TYPE="string"></PROPERTY><PROPERTY NAME="Data" TYPE="string"><VALUE>KVP item slate.ip missing &amp; no &lt;cache&gt;</VALUE></PROPERTY><PROPERTY NAME="Description" PROPAGATED="true" TYPE="string"></PROPERTY><PROPERTY NAME="ElementName" PROPAGATED="true" TYPE="string"></PROPERTY><PROPERTY NAME="Name" TYPE="string"><VALUE>slate.error</VALUE></PROPERTY><PROPERTY NAME="Source" TYPE="uint16"><VALUE>2</VALUE></PROPERTY></INSTANCE>'
    }
    It 'returns the Data of the named item, XML entities decoded' {
        ConvertFrom-GuestKvpXml -Items @($otherItem, $errorItem) -Name 'slate.error' | Should -BeExactly 'KVP item slate.ip missing & no <cache>'
    }
    It 'returns null when no item has the name or there are none, and reads past an unparsable item' {
        ConvertFrom-GuestKvpXml -Items @($otherItem) -Name 'slate.error' | Should -Be $null
        ConvertFrom-GuestKvpXml -Items @() -Name 'slate.error' | Should -Be $null
        ConvertFrom-GuestKvpXml -Items $null -Name 'slate.error' | Should -Be $null
        ConvertFrom-GuestKvpXml -Items @('not <xml', $errorItem) -Name 'slate.error' | Should -BeExactly 'KVP item slate.ip missing & no <cache>'
    }
}

Describe 'Merge-RunnerCache' {
    It 'merges the child into the lane parent and bumps the generation' {
        Mock Merge-VHD {}
        Set-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' -Value 7
        Merge-RunnerCache -Config $config -Lane 'app' -ChildPath 'C:\slate-ci\vms\x\cache.vhdx' | Should -Be 8
        Should -Invoke Merge-VHD -Times 1 -ParameterFilter { $Path -eq 'C:\slate-ci\vms\x\cache.vhdx' -and $DestinationPath -eq (Join-Path $config.CacheDir 'app.vhdx') }
        Get-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' | Should -Be 8
    }
}

Describe 'Remove-RunnerVm / Clear-RunnerVmDirs / Remove-RunnerCache' {
    It 'removes the VM when present and always deletes the directory' {
        Mock Get-VM { $null }
        Mock Remove-VM {}
        $dir = Join-Path $config.VmDir 'slate-win-app-11111111'
        New-Item -ItemType Directory -Force $dir | Out-Null
        Remove-RunnerVm -Name 'slate-win-app-11111111' -Dir $dir
        Test-Path $dir | Should -BeFalse
        Should -Invoke Remove-VM -Times 0
    }
    It 'clears directories that are not active' {
        $keep = Join-Path $config.VmDir 'slate-win-app-keep0000'
        $drop = Join-Path $config.VmDir 'slate-win-app-drop0000'
        New-Item -ItemType Directory -Force $keep, $drop | Out-Null
        Clear-RunnerVmDirs -VmDir $config.VmDir -ActiveNames @('slate-win-app-keep0000')
        Test-Path $keep | Should -BeTrue
        Test-Path $drop | Should -BeFalse
    }
    It 'deletes a cache child file and tolerates a missing one' {
        $child = Join-Path $TestDrive 'child.vhdx'
        'x' | Set-Content $child
        Remove-RunnerCache -ChildPath $child
        Test-Path $child | Should -BeFalse
        { Remove-RunnerCache -ChildPath $child } | Should -Not -Throw
        { Remove-RunnerCache -ChildPath $null } | Should -Not -Throw
    }
}

Describe 'New-HyperVAdapters' {
    It 'exposes exactly the keys the orchestrator invokes' {
        @((New-HyperVAdapters -Config $config).Keys | Sort-Object) | Should -Be @('CleanVmDirs', 'CommitCache', 'DiscardCache', 'GetGeneration', 'GetGuestError', 'GetHeartbeat', 'GetVmState', 'ListVms', 'NewVm', 'RemoveVm', 'SendKvp', 'StartVm', 'StopVmForce')
    }
    It 'binds the config into the closures' {
        Set-CacheGeneration -CacheDir $config.CacheDir -Lane 'app' -Value 3
        $a = New-HyperVAdapters -Config $config
        (& $a.GetGeneration 'app') | Should -Be 3
    }
}
