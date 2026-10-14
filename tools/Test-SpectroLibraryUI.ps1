param(
    [Parameter(Mandatory)][string]$WindowHandle,
    [Parameter(Mandatory)][string]$ExpectedVersion,
    [ValidateSet('Matrix', 'Progress', 'ProgressActions', 'Cancel', 'Offline')][string]$Scenario = 'Matrix',
    [string]$FixtureStoryHash,
    [ValidateSet('All', 'Global', 'Folders', 'Search')][string]$MatrixSection = 'All',
    [ValidateSet('Any', 'All', 'Unread', 'Saved', 'Read')][string]$MatrixFilter = 'Any',
    [string]$FolderTitle
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$package = Get-AppxPackage -Name A3C06F23-C3A9-4304-A172-22F2D2F7A78C
if ($package.IsDevelopmentMode -or "$($package.Version)" -ne $ExpectedVersion) {
    throw 'The expected signed Release package must be installed, not a development registration.'
}
$root = [Windows.Automation.AutomationElement]::FromHandle(
    [IntPtr]::new([Convert]::ToInt64($WindowHandle, 16)))
$events = [Collections.Generic.List[object]]::new()
function Element([string]$Id) {
    $condition = [Windows.Automation.PropertyCondition]::new(
        [Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $element = $root.FindFirst([Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 50
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($null -eq $element) { throw "Missing UI element: $Id" }
    return $element
}
function Invoke-Control([string]$Id) {
    (Element $Id).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Select-Navigation([string]$Title) {
    $condition = [Windows.Automation.AndCondition]::new(
        [Windows.Automation.PropertyCondition]::new(
            [Windows.Automation.AutomationElement]::NameProperty, $Title),
        [Windows.Automation.PropertyCondition]::new(
            [Windows.Automation.AutomationElement]::ControlTypeProperty,
            [Windows.Automation.ControlType]::ListItem))
    $item = (Element 'NavigationList').FindFirst([Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -eq $item) { throw "Navigation item not found: $Title" }
    $item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
}
function Select-Filter([string]$Filter) {
    $button = Element ($Filter + 'FilterButton')
    $toggle = $button.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)
    if ($toggle.Current.ToggleState -ne [Windows.Automation.ToggleState]::On) { $toggle.Toggle() }
    Start-Sleep -Milliseconds 250
    if ((Element ($Filter + 'FilterButton')).GetCurrentPattern(
            [Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne
            [Windows.Automation.ToggleState]::On) { throw "$Filter filter is not selected" }
}
function Select-Feed([object]$Feed) {
    $name = "$($Feed.title), $($Feed.unread) downloaded unread stories"
    $condition = [Windows.Automation.AndCondition]::new(
        [Windows.Automation.PropertyCondition]::new(
            [Windows.Automation.AutomationElement]::NameProperty, $name),
        [Windows.Automation.PropertyCondition]::new(
            [Windows.Automation.AutomationElement]::ControlTypeProperty,
            [Windows.Automation.ControlType]::ListItem))
    $item = (Element 'FeedList').FindFirst([Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -eq $item) { throw "Feed not found: $($Feed.title)" }
    $item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
}
function Set-Search([string]$Id, [string]$Text) {
    (Element $Id).GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($Text)
    Start-Sleep -Milliseconds 150
}
function Read-State {
    $state = (& (Join-Path $PSScriptRoot 'Get-SpectroE2EState.ps1') -Library -StoryLimit 10000) | ConvertFrom-Json
    if ($state.storyCount -gt 10000) { throw 'The acceptance snapshot exceeds its bounded story limit.' }
    return $state
}
function Record([string]$Name, [object]$Details) {
    $events.Add([pscustomobject]@{ check = $Name; passed = $true; details = $Details })
}
function Assert-Collection([string]$Label, [object[]]$Expected) {
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $subtitle = (Element 'LibrarySubtitle').Current.Name
        if ($subtitle -match '^(\d+) stor' -and [int]$Matches[1] -eq $Expected.Count) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($subtitle -notmatch '^(\d+) stor' -or [int]$Matches[1] -ne $Expected.Count) {
        throw "$Label expected $($Expected.Count) stories, saw '$subtitle'"
    }
    $list = Element 'StoryList'
    $scroll = $list.GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern)
    if ($scroll.Current.VerticallyScrollable) { $scroll.SetScrollPercent(-1, 0) }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $expectedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($story in $Expected) { $null = $expectedNames.Add($story.title) }
    for ($page = 0; $page -lt 300; $page++) {
        Start-Sleep -Milliseconds 70
        $items = $list.FindAll([Windows.Automation.TreeScope]::Children,
            [Windows.Automation.PropertyCondition]::new(
                [Windows.Automation.AutomationElement]::ControlTypeProperty,
                [Windows.Automation.ControlType]::ListItem))
        foreach ($item in $items) {
            $name = $item.Current.Name
            if (-not $expectedNames.Contains($name)) { throw "$Label contains an unexpected story" }
            $null = $seen.Add($name)
        }
        if (-not $scroll.Current.VerticallyScrollable -or $scroll.Current.VerticalScrollPercent -ge 99.99) { break }
        $scroll.Scroll([Windows.Automation.ScrollAmount]::NoAmount,
            [Windows.Automation.ScrollAmount]::LargeIncrement)
    }
    if (-not $seen.SetEquals($expectedNames)) { throw "$Label did not expose every expected story while scrolling" }
    if ($scroll.Current.VerticallyScrollable) { $scroll.SetScrollPercent(-1, 0) }
    Record $Label @{ count = $Expected.Count; distinctTitles = $seen.Count }
}
function Assert-Feeds([string]$Label, [object[]]$Expected) {
    $expectedNames = @($Expected | ForEach-Object { "$($_.title), $($_.unread) downloaded unread stories" } | Sort-Object)
    $expectedSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $expectedNames) { $null = $expectedSet.Add($name) }
    $list = Element 'FeedList'
    $scroll = $list.GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern)
    if ($scroll.Current.VerticallyScrollable) { $scroll.SetScrollPercent(-1, 0) }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($page = 0; $page -lt 150; $page++) {
        Start-Sleep -Milliseconds 70
        $items = $list.FindAll([Windows.Automation.TreeScope]::Children,
            [Windows.Automation.PropertyCondition]::new(
                [Windows.Automation.AutomationElement]::ControlTypeProperty,
                [Windows.Automation.ControlType]::ListItem))
        foreach ($item in $items) {
            $name = $item.Current.Name
            if (-not $expectedSet.Contains($name)) { throw "$Label contains an unexpected feed or unread count" }
            $null = $seen.Add($name)
        }
        if (-not $scroll.Current.VerticallyScrollable -or $scroll.Current.VerticalScrollPercent -ge 99.99) { break }
        $scroll.Scroll([Windows.Automation.ScrollAmount]::NoAmount, [Windows.Automation.ScrollAmount]::LargeIncrement)
    }
    if ($scroll.Current.VerticallyScrollable) { $scroll.SetScrollPercent(-1, 0) }
    if (-not $expectedSet.SetEquals($seen)) {
        throw "$Label feed titles/unread counts do not match SQLite"
    }
    Record $Label @{ feeds = $Expected.Count }
}

try {
    if ($Scenario -eq 'Matrix') {
        $state = Read-State
        if ($state.feeds.Count -lt 4 -or $state.folders.Count -lt 2) {
            throw 'Use a representative isolated library with at least four feeds and two folders.'
        }
        foreach ($filter in @('All', 'Unread', 'Saved', 'Read') | Where-Object {
            $MatrixSection -in @('All', 'Global') -and ($MatrixFilter -eq 'Any' -or $_ -eq $MatrixFilter)
        }) {
            $title = switch ($filter) { All { 'All stories' } Read { 'Read stories' } default { $filter } }
            Select-Navigation $title
            $expected = @($state.stories | Where-Object {
                $filter -eq 'All' -or ($filter -eq 'Unread' -and -not $_.read) -or
                ($filter -eq 'Saved' -and $_.saved) -or ($filter -eq 'Read' -and $_.read)
            } | Select-Object -First 500)
            Assert-Collection "Global $filter exact stories" $expected
            $feeds = @($state.feeds | Where-Object {
                $filter -eq 'All' -or ($filter -eq 'Unread' -and $_.unread -gt 0) -or
                ($filter -eq 'Saved' -and $_.saved -gt 0) -or ($filter -eq 'Read' -and $_.total -gt $_.unread)
            })
            Assert-Feeds "Global $filter feed counts" $feeds
        }
        foreach ($folder in $state.folders | Where-Object {
            $MatrixSection -in @('All', 'Folders') -and $_.feedIds.Count -gt 0 -and
            (-not $FolderTitle -or $_.title -eq $FolderTitle)
        }) {
            Select-Navigation 'All stories'
            Select-Navigation $folder.title
            foreach ($filter in @('All', 'Unread', 'Saved') | Where-Object {
                $MatrixFilter -eq 'Any' -or $_ -eq $MatrixFilter
            }) {
                Select-Filter $filter
                $expected = @($state.stories | Where-Object {
                    $_.feedId -in $folder.feedIds -and
                    ($filter -eq 'All' -or ($filter -eq 'Unread' -and -not $_.read) -or ($filter -eq 'Saved' -and $_.saved))
                } | Select-Object -First 500)
                Assert-Collection "Folder $($folder.title) / $filter exact stories" $expected
                $feeds = @($state.feeds | Where-Object {
                    $_.id -in $folder.feedIds -and
                    ($filter -eq 'All' -or ($filter -eq 'Unread' -and $_.unread -gt 0) -or ($filter -eq 'Saved' -and $_.saved -gt 0))
                })
                Assert-Feeds "Folder $($folder.title) / $filter feed counts" $feeds
            }
            Select-Filter 'All'
            $feed = $state.feeds | Where-Object { $_.id -in $folder.feedIds } | Select-Object -First 1
            Select-Feed $feed
            foreach ($filter in @('All', 'Unread', 'Saved') | Where-Object {
                $MatrixFilter -eq 'Any' -or $_ -eq $MatrixFilter
            }) {
                Select-Filter $filter
                $expected = @($state.stories | Where-Object {
                    $_.feedId -eq $feed.id -and
                    ($filter -eq 'All' -or ($filter -eq 'Unread' -and -not $_.read) -or ($filter -eq 'Saved' -and $_.saved))
                } | Select-Object -First 500)
                Assert-Collection "Selected feed $($feed.title) / $filter exact stories" $expected
                $feeds = @($state.feeds | Where-Object {
                    $_.id -in $folder.feedIds -and
                    ($filter -eq 'All' -or ($filter -eq 'Unread' -and $_.unread -gt 0) -or ($filter -eq 'Saved' -and $_.saved -gt 0))
                })
                Assert-Feeds "Selected feed retains folder scope / $filter" $feeds
            }
        }
        if ($MatrixSection -in @('All', 'Search')) {
            Select-Navigation 'All stories'
            $searchFeed = $state.feeds | Where-Object total -gt 0 | Select-Object -First 1
            Set-Search 'FeedSearch' $searchFeed.title
            Assert-Feeds 'Feed search narrows the displayed subscriptions' @($state.feeds | Where-Object {
                $_.title.IndexOf($searchFeed.title, [StringComparison]::CurrentCultureIgnoreCase) -ge 0
            })
            Set-Search 'FeedSearch' ''
            $searchStory = $state.stories | Select-Object -First 1
            Set-Search 'StorySearch' $searchStory.title
            Assert-Collection 'Story search narrows the displayed collection' @($searchStory)
            Set-Search 'StorySearch' ''
        }
    }
    else {
        $before = Read-State
        if (($Scenario -in @('Progress', 'ProgressActions') -and $before.storyCount -ne 0) -or
            $before.pendingCount -ne 0 -or $before.uploadedCount -ne 0) {
            throw 'Progress acceptance requires an empty test cache and no queued mutations.'
        }
        $watch = [Diagnostics.Stopwatch]::StartNew()
        Invoke-Control 'Sync'
        $samples = [Collections.Generic.List[object]]::new()
        $browsedWhileSyncing = $false
        $actionsWhileSyncing = $false
        $canceled = $false
        $maxQueryMs = 0
        do {
            $query = [Diagnostics.Stopwatch]::StartNew()
            $progress = (Element 'SyncProgress').Current.Name
            $syncing = -not (Element 'Sync').Current.IsEnabled
            $subtitle = (Element 'LibrarySubtitle').Current.Name
            $query.Stop()
            $maxQueryMs = [Math]::Max($maxQueryMs, $query.ElapsedMilliseconds)
            $count = if ($subtitle -match '^(\d+) stor') { [int]$Matches[1] } else { 0 }
            $samples.Add([pscustomobject]@{
                ms = $watch.ElapsedMilliseconds; syncing = $syncing; stories = $count; progress = $progress
            })
            if ($syncing -and $count -gt 0 -and -not $browsedWhileSyncing) {
                Select-Navigation 'All stories'
                Start-Sleep -Milliseconds 100
                if ((Element 'LibraryHeading').Current.Name -ne 'All stories') {
                    throw 'Navigation did not change the rendered collection during sync.'
                }
                $browsedWhileSyncing = $true
            }
            if ($Scenario -eq 'ProgressActions' -and $syncing -and $count -gt 0 -and -not $actionsWhileSyncing) {
                $available = Read-State
                $fixture = $available.stories | Where-Object hash -eq $FixtureStoryHash
                if ($fixture) {
                    if ($fixture.feedId -in @(5771943, 10310620)) {
                        throw 'Reading mutations must target a newly created fixture feed, not a baseline subscription.'
                    }
                    $condition = [Windows.Automation.AndCondition]::new(
                        [Windows.Automation.PropertyCondition]::new(
                            [Windows.Automation.AutomationElement]::NameProperty, [string]$fixture.title),
                        [Windows.Automation.PropertyCondition]::new(
                            [Windows.Automation.AutomationElement]::ControlTypeProperty,
                            [Windows.Automation.ControlType]::ListItem))
                    $item = (Element 'StoryList').FindFirst([Windows.Automation.TreeScope]::Descendants, $condition)
                    if ($null -eq $item) { throw 'Downloaded fixture did not appear in the live story list.' }
                    $item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
                    $deadline = [DateTime]::UtcNow.AddSeconds(5)
                    do {
                        Start-Sleep -Milliseconds 100
                        $read = (Read-State).stories | Where-Object hash -eq $FixtureStoryHash
                    } while (-not $read.read -and [DateTime]::UtcNow -lt $deadline)
                    if (-not $read.read -or (Element 'Sync').Current.IsEnabled) {
                        throw 'The selected fixture was not marked read while sync was still running.'
                    }
                    Invoke-Control 'ReaderSave'
                    Start-Sleep -Milliseconds 150
                    $saved = (Read-State).stories | Where-Object hash -eq $FixtureStoryHash
                    if ($saved.saved -eq $fixture.saved -or (Element 'Sync').Current.IsEnabled) {
                        throw 'Save did not change the downloaded fixture while sync was running.'
                    }
                    Invoke-Control 'ReaderSave'
                    if (-not $fixture.read) { Invoke-Control 'ReaderRead' }
                    $actionsWhileSyncing = $true
                    Record 'Read and save respond during live sync' @{ fixtureHash = $FixtureStoryHash }
                }
            }
            if ($Scenario -eq 'Cancel' -and $syncing -and $progress -match 'page' -and -not $canceled) {
                Invoke-Control 'CancelSync'
                $canceled = $true
            }
            Start-Sleep -Milliseconds 100
        } while ($syncing -and $watch.Elapsed.TotalSeconds -lt 120)
        $after = Read-State
        if ($Scenario -eq 'Cancel') {
            if (-not $canceled -or $syncing -or $after.lastSync -ne $before.lastSync -or
                $after.storyCount -lt $before.storyCount -or (Element 'SyncProgress').Current.Name -notmatch 'canceled') {
                throw 'Cancellation did not preserve cached data/checkpoint and report cancellation.'
            }
        }
        elseif ($Scenario -eq 'Offline') {
            if ($syncing -or $after.lastSync -ne $before.lastSync -or $after.storyCount -ne $before.storyCount -or
                (Element 'SyncProgress').Current.Name -notmatch 'Offline|delayed') {
                throw 'Offline sync did not preserve cached content/checkpoint and show useful failure feedback.'
            }
            Select-Navigation 'All stories'
            Assert-Collection 'Offline library remains browsable' @($after.stories | Select-Object -First 500)
        }
        elseif ($syncing -or $after.storyCount -eq 0 -or -not $after.lastSync -or -not $browsedWhileSyncing) {
            throw 'Initial sync did not progressively expose usable stories before successful completion.'
        }
        if ($Scenario -eq 'ProgressActions' -and -not $actionsWhileSyncing) {
            throw 'Read/save interactions were not observed before sync completed.'
        }
        if ($maxQueryMs -gt 1000) { throw "UI automation responses exceeded 1000 ms ($maxQueryMs ms)." }
        Record "$Scenario sync with responsive navigation" @{
            elapsedMs = $watch.ElapsedMilliseconds; maximumUiQueryMs = $maxQueryMs; samples = $samples
            finalStoryCount = $after.storyCount
        }
    }
    if ($events.Count -eq 0) { throw 'No acceptance checks matched the requested matrix scope.' }
    @{ passed = $true; version = $ExpectedVersion; scenario = $Scenario; checks = $events } |
        ConvertTo-Json -Depth 8 -Compress
}
catch {
    @{ passed = $false; scenario = $Scenario; completedChecks = $events; error = $_.Exception.Message } |
        ConvertTo-Json -Depth 8 -Compress
    throw
}
