[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$VMName,
    [Parameter(Mandatory)][string]$HyperloopPath,
    [Parameter(Mandatory)][string]$FeedTitle,
    [Parameter(Mandatory)][string]$StoryTitle,
    [Parameter(Mandatory)][ValidatePattern('^\d+:[A-Za-z0-9_-]+$')][string]$StoryHash,
    [string]$WindowHandle,
    [ValidateSet('All', 'RoundTrips', 'OfflineRestart')][string]$Scenario = 'All'
)

$ErrorActionPreference = 'Stop'
$aumid = 'A3C06F23-C3A9-4304-A172-22F2D2F7A78C_1z32rh13vfry6!App'
$window = $null
$networkChanged = $false
$adapter = $null
$originalSwitch = $null
$events = [Collections.Generic.List[object]]::new()
$stage = 'preflight'
$stateScript = '& {' + (Get-Content (Join-Path $PSScriptRoot 'Get-SpectroE2EState.ps1') -Raw) +
    "`n} -StoryHash '$StoryHash'"

function Invoke-Hl([string[]]$Arguments) {
    $raw = & $HyperloopPath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Hyperloop failed during $stage ($($Arguments[0])): $($raw -join ' ')" }
    $result = ($raw -join "`n") | ConvertFrom-Json
    foreach ($property in @('success', 'pass', 'met', 'ready', 'invoked')) {
        if ($null -ne $result.$property -and -not $result.$property) {
            throw "Hyperloop $property assertion failed during $stage ($($Arguments[0]))."
        }
    }
    if ($result.error) { throw "Hyperloop error during ${stage}: $($result.code)." }
    return $result
}
function Read-State {
    $result = Invoke-Hl @('shell', $VMName, '--name', 'Read only E2E synchronization state', '--script', $stateScript)
    return $result.stdout | ConvertFrom-Json
}
function Assert-State([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Record-Step([string]$Name) {
    $events.Add([pscustomobject]@{ step = $Name; passed = $true; time = [DateTimeOffset]::UtcNow.ToString('O') })
}
function Invoke-Web([string]$Action) {
    & node (Join-Path $PSScriptRoot 'newsblur-web-e2e.cjs') $Action $FeedTitle $StoryTitle $StoryHash
    if ($LASTEXITCODE -ne 0) { throw "Web $Action assertion failed during $stage." }
}
function Invoke-Control([string]$Id) {
    $null = Invoke-Hl @('uia-invoke', $VMName, '--hwnd', $window, '--id', $Id, '--pattern', 'invoke')
}
function Element-Exists([string]$Id) {
    $result = & $HyperloopPath assert $VMName --hwnd $window --automation-id $Id --condition exists |
        ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $result.error) { throw "Could not inspect $Id." }
    return [bool]$result.pass
}
function Ensure-Authenticated {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
    do {
        if (Element-Exists 'SignIn') {
            & (Join-Path $PSScriptRoot 'Invoke-NewsBlurDesktopLogin.ps1') -VMName $VMName `
                -WindowHandle $window -HyperloopPath $HyperloopPath
            return
        }
        if (Element-Exists 'StoryList') { return }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw 'Spectro did not reach login or the reader within 60 seconds.'
}
function Open-Fixture {
    $null = Invoke-Hl @('wait', $VMName, '--hwnd', $window, '--condition', 'elementexists',
        '--value', 'Downloaded stories', '--timeout', '60')
    if (Element-Exists 'BackToStories') { Invoke-Control 'BackToStories' }
    $null = Invoke-Hl @('wait', $VMName, '--hwnd', $window, '--condition', 'elementexists',
        '--value', 'All stories', '--timeout', '60')
    $null = Invoke-Hl @('uia-invoke', $VMName, '--hwnd', $window, '--name', 'All stories', '--pattern', 'pick', '--value', 'select')
    $null = Invoke-Hl @('wait', $VMName, '--hwnd', $window, '--condition', 'elementexists',
        '--value', $StoryTitle, '--timeout', '60')
    $null = Invoke-Hl @('uia-invoke', $VMName, '--hwnd', $window, '--name', $StoryTitle, '--pattern', 'pick', '--value', 'select')
}
function Sync-And-Wait {
    $previous = (Read-State).lastSync
    Invoke-Control 'Sync'
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(90)
    do {
        Start-Sleep -Seconds 1
        $state = Read-State
        if ($state.lastSync -and $state.lastSync -ne $previous -and
            $state.pendingCount -eq 0 -and $state.uploadedCount -eq 0) {
            return $state
        }
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "No new successful sync checkpoint with an empty mutation queue during $stage."
}

try {
    $null = Invoke-Hl @('context', $VMName, '--title', 'Running the real desktop and web E2E suite')
    $network = Invoke-Hl @('network-emulation', $VMName, '--action', 'status')
    Assert-State (-not $network.emulationActive -and -not $network.disabledAdapters) 'Existing network emulation must be restored before this test.'
    $adapters = @(Get-VMNetworkAdapter -VMName $VMName)
    Assert-State ($adapters.Count -eq 1 -and $adapters[0].Connected) 'The test requires one connected Hyper-V network adapter.'
    $adapter = $adapters[0]
    $originalSwitch = [string]$adapter.SwitchName
    Assert-State (-not [string]::IsNullOrWhiteSpace($originalSwitch)) 'The original VM switch could not be recorded.'
    Invoke-Web 'unsaved'
    if ($WindowHandle) {
        $window = $WindowHandle
    }
    else {
        $existing = & $HyperloopPath find-window $VMName --process Spectro.App | ConvertFrom-Json
        if (($LASTEXITCODE -ne 0 -or $existing.error) -and
            $existing.code -notin @('HL_NOT_FOUND', 'HL_WINDOW_NOT_FOUND')) {
            throw 'Could not discover existing Spectro windows.'
        }
        $windows = @($existing.windows | Where-Object className -eq 'WinUIDesktopWin32WindowClass')
        Assert-State ($windows.Count -le 1) 'Close extra Spectro instances before testing.'
        $window = if ($windows.Count -eq 1) { $windows[0].hwnd } else {
            (Invoke-Hl @('launch', $VMName, '--aumid', $aumid)).hwnd
        }
    }
    Ensure-Authenticated
    Open-Fixture
    $state = Sync-And-Wait
    Assert-State ($state.fixtureRead -eq 1 -and $state.fixtureSaved -eq 0) 'Fixture must start read and unsaved on both clients.'
    Invoke-Web 'read'
    Record-Step 'Authenticated cached launch and baseline synchronization'

    if ($Scenario -ne 'OfflineRestart') {
        $stage = 'desktop-save'
        Invoke-Control 'ReaderSave'
        $state = Sync-And-Wait
        Assert-State ($state.fixtureSaved -eq 1) 'Desktop save did not persist.'
        Invoke-Web 'saved'
        Record-Step 'Spectro save appears on NewsBlur web and server'

        $stage = 'web-unsave'
        Invoke-Web 'unsave'
        $state = Sync-And-Wait
        Assert-State ($state.fixtureSaved -eq 0) 'Web unsave did not reconcile locally.'
        Record-Step 'NewsBlur web unsave appears in Spectro'

        $stage = 'web-unread'
        Invoke-Web 'mark-unread'
        $state = Sync-And-Wait
        Assert-State ($state.fixtureRead -eq 0) 'Web unread did not reconcile locally.'
        $null = Invoke-Hl @('wait', $VMName, '--hwnd', $window, '--condition', 'elementexists', '--value', 'Mark as read', '--type', 'button')
        Record-Step 'NewsBlur web unread appears in the selected Spectro article'

        $stage = 'desktop-read'
        Invoke-Control 'ReaderRead'
        $state = Sync-And-Wait
        Assert-State ($state.fixtureRead -eq 1) 'Desktop read did not persist.'
        Invoke-Web 'read'
        Record-Step 'Spectro read appears on NewsBlur web and canonical unread set'
    }

    if ($Scenario -ne 'RoundTrips') {
        $stage = 'offline-restart'
        $null = Invoke-Hl @('context', $VMName, '--title', 'Testing offline changes across an app restart')
        $networkChanged = $true
        Disconnect-VMNetworkAdapter -VMName $VMName -Name $adapter.Name
        Assert-State (-not (Get-VMNetworkAdapter -VMName $VMName -Name $adapter.Name).Connected) 'The VM did not disconnect.'
        Invoke-Control 'ReaderSave'
        $state = Read-State
        Assert-State ($state.fixtureSaved -eq 1 -and $state.pendingCount -gt 0) 'Offline save was not queued durably.'
        $null = Invoke-Hl @('close', $VMName, '--hwnd', $window)
        $window = (Invoke-Hl @('launch', $VMName, '--aumid', $aumid)).hwnd
        Open-Fixture
        $state = Read-State
        Assert-State ($state.fixtureSaved -eq 1 -and $state.pendingCount -gt 0) 'Queued save did not survive offline restart.'
        Invoke-Web 'unsaved'
        Record-Step 'Offline save survives restart while NewsBlur remains unchanged'

        $stage = 'reconnect'
        Connect-VMNetworkAdapter -VMName $VMName -Name $adapter.Name -SwitchName $originalSwitch
        $networkChanged = $false
        $state = Sync-And-Wait
        Assert-State ($state.fixtureSaved -eq 1) 'Reconnect lost the queued save.'
        Invoke-Web 'saved'
        Record-Step 'Reconnect uploads queued save and clears durable queues'

        $stage = 'restore-fixture'
        Invoke-Control 'ReaderSave'
        $state = Sync-And-Wait
        Assert-State ($state.fixtureSaved -eq 0 -and $state.fixtureRead -eq 1) 'Fixture was not restored.'
        Invoke-Web 'unsaved'
        Record-Step 'Fixture restored to read and unsaved'
    }
}
catch {
    $events.Add([pscustomobject]@{ step = $stage; passed = $false; message = $_.Exception.Message })
    throw
}
finally {
    try {
        if ($networkChanged) {
            Connect-VMNetworkAdapter -VMName $VMName -Name $adapter.Name -SwitchName $originalSwitch
        }
    }
    finally {
        $null = Invoke-Hl @('release', $VMName)
        $events | ConvertTo-Json -Depth 4
    }
}
