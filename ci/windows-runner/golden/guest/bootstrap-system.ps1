# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Guest task slate-bootstrap-system: SYSTEM, at startup, Windows
# PowerShell 5.1. A no-op until the golden build's completion marker
# exists (so it stays quiet during the image build). Reads the host's
# KVP items, configures the static network, finds the cache volume,
# writes .env and jit.cfg for the runner task, then signals `ready`.
# Any failure publishes slate.error as a guest KVP item (the host logs it),
# writes bootstrap-error.txt and shuts the VM down 30 s later; the host
# sees Off, resolves no successful job, and discards.
$ErrorActionPreference = 'Stop'
$runnerDir = 'C:\actions-runner'
$marker = 'C:\Users\runner\.slate-golden-complete'
$logPath = Join-Path $runnerDir 'bootstrap-system.log'

function Write-Log([string]$Message) {
    Add-Content -LiteralPath $logPath -Value ('{0:o} {1}' -f (Get-Date), $Message)
}

if (-not (Test-Path -LiteralPath $marker)) { exit 0 }

try {
    foreach ($stale in 'ready', 'jit.cfg', 'bootstrap-error.txt') {
        Remove-Item -LiteralPath (Join-Path $runnerDir $stale) -Force -ErrorAction SilentlyContinue
    }
    # Inside the try: a missing or unloadable module copy is a bootstrap
    # failure like any other (error file and shutdown), not a silent exit.
    Import-Module 'C:\slate-guest\SlateCiHost.psm1' -Force
    Write-Log 'waiting for KVP items'
    $kvpKey = 'HKLM:\SOFTWARE\Microsoft\Virtual Machine\External'
    # The host sends the JIT chunks first and these items after them in
    # one AddKvpItems call; the guest applies items one at a time, so a
    # poll can see the whole JIT config before the rest. Wait for all.
    $requiredItems = 'slate.ip', 'slate.gateway', 'slate.dns', 'slate.cache', 'slate.lane'
    $deadline = (Get-Date).AddSeconds(300)
    $items = @{}
    $jit = $null
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $kvpKey) {
            $items = Select-SlateKvpItems -Properties (Get-ItemProperty -LiteralPath $kvpKey -ErrorAction SilentlyContinue)
            $jit = Join-KvpChunks -Items $items
            if ($jit -and @($requiredItems | Where-Object { -not $items.Contains($_) }).Count -eq 0) { break }
        }
        Start-Sleep -Seconds 2
    }
    if (-not $jit) {
        throw 'no JIT config arrived within 300 s'
    }
    foreach ($required in $requiredItems) {
        if (-not $items.Contains($required)) { throw "KVP item $required missing" }
    }
    Write-Log ('config received: lane {0}, job {1}' -f $items['slate.lane'], $items['slate.job'])

    # A fork's synthetic NIC is a new device on its first boot; give it time.
    $adapter = $null
    $adapterDeadline = (Get-Date).AddSeconds(60)
    while (-not $adapter -and (Get-Date) -lt $adapterDeadline) {
        $adapter = Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'Up' } | Sort-Object ifIndex | Select-Object -First 1
        if (-not $adapter) { Start-Sleep -Seconds 2 }
    }
    if (-not $adapter) { throw 'no network adapter came up within 60 s' }
    Set-NetIPInterface -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -Dhcp Disabled
    Get-NetIPAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue
    Get-NetRoute -InterfaceIndex $adapter.ifIndex -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue |
        Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue
    New-NetIPAddress -InterfaceIndex $adapter.ifIndex -IPAddress $items['slate.ip'] -PrefixLength 24 -DefaultGateway $items['slate.gateway'] | Out-Null
    Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses ($items['slate.dns'] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    Write-Log ('network: {0} via {1}' -f $items['slate.ip'], $items['slate.gateway'])

    $envLines = @()
    if ($items['slate.cache'] -eq '1') {
        $volume = $null
        $volumeDeadline = (Get-Date).AddSeconds(90)
        while (-not $volume -and (Get-Date) -lt $volumeDeadline) {
            $volume = Get-Volume -FileSystemLabel 'slate-cache' -ErrorAction SilentlyContinue |
                Where-Object { $_.DriveLetter } | Select-Object -First 1
            if (-not $volume) { Start-Sleep -Seconds 3 }
        }
        if (-not $volume) { throw 'cache volume slate-cache did not mount' }
        $root = '{0}:\cache' -f $volume.DriveLetter
        foreach ($sub in 'cargo\registry', 'cargo\git', 'target', 'nuget') {
            New-Item -ItemType Directory -Force -Path (Join-Path $root $sub) | Out-Null
        }
        # SYSTEM created these and runner builds into them (CARGO_TARGET_DIR,
        # NUGET_PACKAGES, the cargo junctions), whatever the volume root grants.
        # The grant re-walks the whole cache tree, so it runs once: a trusted
        # commit carries the ACE into the lane parent and later boots skip it.
        $hasAce = @((Get-Acl -LiteralPath $root).Access | Where-Object {
                $_.IdentityReference.Value -like '*\runner' -and $_.AccessControlType -eq 'Allow' -and
                (($_.FileSystemRights -band [System.Security.AccessControl.FileSystemRights]::Modify) -eq [System.Security.AccessControl.FileSystemRights]::Modify)
            }).Count -gt 0
        if (-not $hasAce) {
            & icacls.exe $root /grant 'runner:(OI)(CI)M' | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "icacls exited $LASTEXITCODE granting runner the cache" }
            Write-Log 'cache acl: granted runner modify on the cache root'
        } else {
            Write-Log 'cache acl: runner already has modify on the cache root; grant skipped'
        }
        $envLines += "NSC_CACHE_PATH=$root"
        $envLines += "CARGO_TARGET_DIR=$root\target"
        $envLines += "NUGET_PACKAGES=$root\nuget"
        $envLines += "SLATE_CACHE_ROOT=$root"
        # The volume letter is not fixed, so the Defender exclusion is added here.
        Add-MpPreference -ExclusionPath $root -ErrorAction SilentlyContinue
        Write-Log "cache at $root"
    }
    Set-Content -LiteralPath (Join-Path $runnerDir '.env') -Value $envLines -Encoding ascii

    $cfgPath = Join-Path $runnerDir 'jit.cfg'
    # Restrict the file while it is still empty, then write the config into
    # it: the secret never sits under the directory's inherited ACL.
    New-Item -ItemType File -Path $cfgPath -Force | Out-Null
    & icacls.exe $cfgPath /inheritance:r /grant 'runner:R' /grant 'SYSTEM:F' | Out-Null
    # A native command's failure never throws, even under Stop: check it,
    # so jit.cfg is never handed over with its inherited ACL.
    if ($LASTEXITCODE -ne 0) { throw "icacls exited $LASTEXITCODE restricting jit.cfg" }
    Set-Content -LiteralPath $cfgPath -Value $jit -NoNewline -Encoding ascii
    Set-Content -LiteralPath (Join-Path $runnerDir 'ready') -Value 'ok'
    Write-Log 'ready'
} catch {
    $failure = [string]$_
    try {
        # Publish the failure as the guest KVP item slate.error for the host's
        # log: this VM, and the logs on its disk, are discarded. No New-Item
        # -Force: on an existing key it deletes the key's values and subkeys
        # (Guest\Parameter holds the host's data).
        try {
            $guestPool = 'HKLM:\SOFTWARE\Microsoft\Virtual Machine\Guest'
            if (-not (Test-Path -LiteralPath $guestPool)) { New-Item -Path $guestPool | Out-Null }
            Set-ItemProperty -LiteralPath $guestPool -Name 'slate.error' -Value $failure.Substring(0, [math]::Min(1000, $failure.Length))
        } catch {
            # Best effort only: it must never keep the VM from shutting down.
        }
        Write-Log "ERROR: $failure"
        Set-Content -LiteralPath (Join-Path $runnerDir 'bootstrap-error.txt') -Value $failure
    } finally {
        # Shut down even when the log or the error file cannot be written. In
        # 30 s, not at once: the host ticks every 10 s and can read slate.error
        # only while the VM runs.
        & shutdown.exe /s /f /t 30 /c 'slate bootstrap failed'
        Write-Log "shutdown in 30 s, so the host can read slate.error while the VM runs (exit $LASTEXITCODE)"
    }
}
