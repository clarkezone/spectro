# Spectro release checklist

**Status: pre-release.** Passing automated checks or the visual checkpoints below
does not mean the app is Store-ready. Final product acceptance remains separate
from implementation and build verification.

## Visual redesign evidence

- [x] Reference-sized (879 x 564) browsing layout: separate collection, feed,
      and story panes with preview imagery and working feed search.
- [x] Real rendered light/dark reading views inspected, including the window
      title bar, article toolbar, typography, cover image, and local previews.
- [x] Compact reading/back flow and compact feed selection exercised with UIA.
- [x] Windows high-contrast mode exercised with the actual running app.
- [x] Read/save keyboard commands exercised while WebView2 owns focus.
- [x] Reset confirmation and subsequent sample-library recovery exercised.
- [x] A 3,360,073-byte article inserted into the isolated sample database and
      rendered in WebView2, exceeding the former 2 MiB inline-navigation limit.
- [x] New local unread-count, article-selection, typography, and thumbnail-cache
      regression tests pass (110 deterministic tests in total).
- [ ] User approves the updated visual checkpoints with representative live content.
- [ ] Complete 150%/200% scaling and physical-device accessibility acceptance.

Current screenshots are linked from the README; the original reference remains
at `images/spectro.jpg`. Sample content and illustrations are Debug-only.
Dense feed rows use 32-pixel desktop targets; reader toolbar actions use 44 pixels.
The test display was actually running at 100% scaling. The available DPI-setting
command requires sign-out; resizing the window was not counted as a 150%/200%
scaling test. A normal launch with sample mode disabled was also checked to show
sign-in rather than sample data.

## Code and data safety

- [ ] All deterministic unit, contract, repository, synchronization, and UI
      tests pass.
- [ ] No live credentials, tokens, account identifiers, or sensitive response
      bodies are present in source, history additions, logs, or artifacts.
- [ ] Sign-out and cache reset remove credentials, SQLite account content,
      WebView2 data, and bounded diagnostics.
- [ ] Database migration, corruption recovery, retention, and interrupted-sync
      tests pass without data loss.

## Build and packaging

CI packages both Native AOT architectures as self-contained, test-signed MSIX
artifacts using the Windows development skills packaging playbook. The x64 job
also installs and activates the actual MSIX on its isolated runner before
uploading it. Artifacts include only the package, public certificate, and
[installation instructions](installing-test-builds.md), never the private key.
Test signing and an automated launch check do not satisfy the release gates
below; ARM64 device validation and production signing remain separate.

- [ ] `win-x64` and `win-arm64` publish with Native AOT and no reachable
      trimming warnings.
- [ ] The final Store identity and publisher values are applied.
- [ ] Clean install, upgrade from the previous release candidate, uninstall,
      and reinstall pass.
- [ ] Windows App Certification Kit passes for the final packages.
- [ ] CI retains both architecture artifacts from the release commit.

## Product quality

Live-account automation uses process-scoped environment variables. Set them in
PowerShell 7, then launch the automation session from that same shell:

```powershell
$env:NEWSBLUR_TEST_USERNAME = Read-Host 'NewsBlur test username'
$env:NEWSBLUR_TEST_PASSWORD = Read-Host 'NewsBlur test password' -MaskInput
copilot
```

Use only the dedicated test account. These variables are not encrypted: they
exist in process memory and are inherited by child processes. Do not persist them
with `setx` or user/machine environment settings. An already-running automation
process will not inherit variables set in another terminal. Never print their
values, put them in tool arguments, or upload browser profiles or authentication
traces. VM login must also avoid including secrets in Hyperloop command logs.
After the automation session exits, clear both variables or close the shell:

```powershell
Remove-Item Env:\NEWSBLUR_TEST_USERNAME, Env:\NEWSBLUR_TEST_PASSWORD
```

### Repeatable live desktop/web checks

`tools\Test-NewsBlurE2E.ps1` drives the installed, production-mode desktop app
through Hyperloop and independently verifies NewsBlur's website and authenticated
API through Playwright. Run it serially against an isolated Hyper-V test VM.
Prerequisites:

- PowerShell 7, Node.js, Hyperloop, and permission to manage the VM's network
  adapter from the host. The VM must have exactly one connected adapter.
- A deployed Spectro package, an interactive Windows session, and working
  NewsBlur connectivity on both host and VM. Sample mode must be disabled.
- Playwright and its Chromium browser already installed. Set
  `SPECTRO_PLAYWRIGHT_ROOT` to the directory whose `package.json` resolves
  `playwright`; the default uses the existing `~\.spiderloop\playwright-mcp`
  installation.
- For automatic desktop login: IXPTools-managed VM credentials, PowerShell Direct,
  and the default interactive VM user `AdminUser`. For another user, run
  `Invoke-NewsBlurDesktopLogin.ps1` separately with `-VMUser`, then run the suite.
- One recent, downloaded fixture story with a unique visible title, initially
  read and unsaved. The dedicated account's saved collection must be empty.
  Absence is never inferred from an incomplete page of saved stories.

With the credentials inherited from the shell above:

```powershell
$test = @{
    VMName = 'YourIsolatedTestVM'
    HyperloopPath = 'C:\path\to\Hyperloop.exe'
    FeedTitle = 'Exact subscribed feed title'
    StoryTitle = 'Exact recent story title'
    StoryHash = '123456:fixture_hash'
}
.\tools\Test-NewsBlurE2E.ps1 @test -Scenario RoundTrips
.\tools\Test-NewsBlurE2E.ps1 @test -Scenario OfflineRestart
# -Scenario All runs both sequences.
```

The runner reuses the sole existing main window or launches the app; optional
`-WindowHandle` selects an existing instance. Desktop credentials travel through
PowerShell Direct and a same-user named pipe in memory, not command-line values
or temporary credential files. The browser profile persists locally under
`%LOCALAPPDATA%\Spectro\E2E\browser-profile`; it contains session secrets and must
not be committed, uploaded, or shared.

Successful synchronization requires a **new SQLite sync checkpoint and empty
pending and uploaded mutation queues**, not merely a status label. The
`Get-SpectroE2EState.ps1` guest helper opens the database read-only and emits
counters and fixture flags, never article bodies or credentials. Website checks
wait for the initial live feed refresh to settle before using its context menus.
Read verification checks the canonical unread set before opening the feed, so
the website cannot silently repair a failed desktop read upload.

The offline scenario physically disconnects the VM's Hyper-V network adapter,
checks that it is disconnected, changes the fixture, restarts Spectro, and
confirms the independent online website remains unchanged. It then reconnects,
checks upload acknowledgement, and restores the fixture to read/unsaved. The
original switch is restored in `finally`, including on assertion failure.
Network-emulation success alone is not accepted as proof of being offline.

Failures throw and emit the failing stage; do not treat earlier passing steps as
a passing suite. A failure can leave the fixture changed or queued locally.
Inspect and reconcile it before rerunning; do not sign out or reset while queues
are nonempty. Successful runs restore the fixture, **not the entire account**:
normal browser navigation may mark other visible stories read.

### Live evidence and remaining boundary

Validation on 2026-09-15 UTC established:

| Check | Result |
| --- | --- |
| Real browser login, desktop login, initial sync, and cached launch | Passed; 60 stories downloaded across ten live pages |
| Spectro save to web/server; web unsave to Spectro | Passed in the complete `RoundTrips` scenario |
| Web unread to selected Spectro article; Spectro read to web/canonical unread set | Passed in the complete `RoundTrips` scenario |
| Offline save survives restart; website unchanged; reconnect uploads and clears queues | Passed in a separate complete `OfflineRestart` scenario |
| Sign-out and subsequent real login | Sign-in page appeared, local story and mutation counts became zero, and re-login downloaded the library |
| Final fixture and VM state | Fixture read/unsaved, both queues empty; original network switch and DHCP DNS restored |

These were **separate successful scenarios**, not a claim that every attempt or
one uninterrupted `All` run passed. Earlier attempts exposed browser refresh
timing, ineffective network emulation, and intermittent VM DNS failures. The
successful controlled runs used a temporary VM-only DNS override, restored in
`finally`; the runner deliberately does not change DNS or hide connectivity
failures. Diagnose the test VM's resolver before retrying an offline sync result.

Live testing also exposed and fixed three client defects: numeric unfiled
subscription entries were rejected; short/hidden/repeated pages prematurely
ended story pagination; and the unread response used the wrong JSON field.
The client now consumes `unread_feed_story_hashes` and rejects missing/null or
invalid unread maps rather than clearing unread state silently. Regression
coverage totals **125 passing deterministic tests** (21 API, 39 sync,
15 presentation, 50 infrastructure); x64 Debug build and Native AOT publish
also passed after these fixes.

This does not establish complete credential/WebView2 erasure, real-account reset
coverage, every account shape, ARM64 packaging, Store certification, or the full
device/DPI matrix. The broader release gates below remain open.

### Multi-feed library acceptance

Installation and a one-feed sync are not sufficient product acceptance. Validate
the **signed Release MSIX** against a populated library, including exact story
subsets, selected filters, folder scope, local unread counts, and reading during
sync. Counts in the feed pane describe downloaded stories, not the entire remote
account.

The library updates after each committed batch. Hash inventories select up to
60 recent All stories per active feed; missing bodies are fetched in batches of
100 rather than re-downloading every feed page. Up to four HTTP requests run
concurrently with durable pacing; read/save actions and navigation remain available during sync.
Progress reports subscriptions, unread-state reconciliation, feed/page counts,
retry attempts, and completion. Cancel retains committed pages and local changes.
Changes made during a sync remain queued when necessary, with an explicit
"Changes waiting to sync" status. Thumbnail failures must not replace sync errors.
List queries omit full article bodies; selecting a story loads its full content
without replacing the open article during background refreshes.

The live multi-feed exercise also found two defects that mocked/build-only checks
missed: `flat=true` suppresses NewsBlur's folder tree, and binding custom generic
observable collections failed at Native AOT runtime. The catalog now requests the
hierarchical response and respects explicit inactive subscriptions. The UI updates
native WinUI item collections incrementally, retaining unchanged items rather
than clearing the lists on every page.

`tools\newsblur-library-fixture.cjs` creates reversible subscriptions and two
folders on the dedicated automation account. It verifies the signed-in identity,
preserves pre-existing subscriptions, and journals only fixture ownership/state
under `%LOCALAPPDATA%\Spectro\E2E`. Run `prepare` (or `prepare --large` for 25
added feeds), then `status`; run `cleanup`
after acceptance. Never use it against a personal account or upload its browser
profile. After a failed interaction test, reconcile pending desktop mutations
before cleanup.
`probe` performs read-only identity, inventory, and protocol checks. Interrupted
mutations remain blocked. The explicit `recover-add` command can reconcile only
a journaled pending add whose unique URL, placement, and unchanged ownership
match two fresh catalog reads; it never retransmits or deletes the subscription.
Fixture API requests are durably paced (three seconds globally, eight seconds
for saved bodies). HTTP 429 records a cooldown; rerunning before its reported
deadline makes no HTTP requests. Errors identify the actual endpoint, not just
the outer setup/cleanup stage, without exposing request or response contents.

Copy `tools\Test-SpectroLibraryUI.ps1` and `tools\Get-SpectroE2EState.ps1`
to the same folder inside the isolated VM. Invoke the former in the interactive
user's session with `-WindowHandle` and `-ExpectedVersion`:

- `-Scenario Matrix` compares all rendered story titles, including scrolling,
  with read-only SQLite expectations for global/folder/feed filters and searches.
  It also checks the feed list's unread counts and selected toggle states.
- `-Scenario Progress` requires an empty cache, then measures first usable stories,
  progressive feedback, and navigation before sync completes.
- `-Scenario ProgressActions -FixtureStoryHash <new-fixture-hash>` additionally
  reads and toggles saved state during initial sync. Sync again afterward to
  upload queued changes and verify the fixture is restored.
- `-Scenario Cancel` checks cancellation feedback, retained content, and an
  unchanged successful checkpoint. `-Scenario Offline` requires the host to
  physically disconnect the VM adapter and restore its original switch in
  `finally`; it verifies cached content and failure feedback.

The matrix requires at least four feeds/two populated folders and supports a
bounded snapshot of 10,000 cached stories. It applies the application's 500-row
display limit **after** filtering and scrolls virtualized story and feed lists.
For automation hosts with short command timeouts, split it with
`-MatrixSection Global -MatrixFilter All` (also Unread/Saved/Read),
`-MatrixSection Folders -FolderTitle '<folder>' -MatrixFilter All`
(also Unread/Saved), and `-MatrixSection Search`.
It is an actual running-UI check, not a substitute for the
SQLite presentation and controlled-concurrency regression tests.

Use `Invoke-NewsBlurDesktopLogin.ps1 -SubmitMode PasswordEnter` or
`-SubmitMode UsernameEnter` to exercise native Enter without placing credentials
in command arguments or files. Restart the same signed package and upgrade it
without clearing account data to verify Windows Credential Locker persistence.

Local signed Release measurements on the isolated VM (2026-09-15): usable
stories appeared after **1.6-2.4 seconds**, before initial sync finished in
**8.1-9.1 seconds** with about 300 stories across five active feeds. Read/save
and navigation worked while downloads continued; sampled UI automation queries
remained below **430 ms**. Cancellation retained the library/checkpoint. A
physical offline run enumerated all 299 cached titles; a later real connectivity
failure retained two mutations, which a subsequent retry uploaded successfully.
These are observed fixture results, not an iOS comparison or a performance
guarantee for every account/network.

- [ ] Login, initial sync, cached launch, offline restart, reconnect, refresh,
      read/unread, save/unsave, sign-out, and reset flows pass.
- [ ] Wide, medium, and narrow layouts preserve selection and reading context.
- [ ] Light, dark, contrast, 100%, 150%, and 200% scaling checks pass.
- [ ] Keyboard-only navigation, focus order, accessible names, target sizes,
      clipping, truncation, and contrast checks pass.
- [ ] No known crash, hang, data-loss, credential, severity-1/2, or blocking
      accessibility issue remains.

## Store submission

- [ ] Production icons, tile assets, screenshots, descriptions, privacy URL,
      support URL, publisher information, age rating, and release notes are
      present.
- [ ] A private-flight install and upgrade pass on x64 and ARM64.
- [ ] The final release checklist and known-issues review are approved.
