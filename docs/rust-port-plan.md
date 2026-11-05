# Spectro WinUI 3 to Rust port plan

**Target:** replace the C# application and its services with Rust, using Windows Reactor for the same native WinUI 3 controls and WebView2 for the same article renderer. Preserve the appearance, behavior, account data, and installation/upgrade contract of the reviewed WinUI branch. This is not a redesign or a Rust UI shell around a retained .NET backend.

**Implementation gate:** no port implementation starts until PR #26 has landed, its review changes have been reconciled with this plan and the [Reactor gap report](rust-port-parity-gap-report.md), and the combined plan has been approved. The repository's default branch is `master`.

The source baseline for this plan remains PR #26 at `44c28d223e1d2170cfdf423fa3ff83467e9a3f53`, pending reconciliation with the final application. PR #26 remains open at `5f23f0118da14bf109005357c4156e518b417eaf` as verified on November 5. **Framework reassessment: 2026-11-05.** Reactor evidence now uses windows-rs `db9801679bac964b6367b1d1d1bf8472ab001065`, superseding the October 20 reassessment at `ed16707c4b889698e68016b7add119d98b283e11` and original audit at `9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22`. Source prepares Reactor/WebView/window `0.101.0` and setup `0.100.1`, while the latest published Reactor/setup/WebView versions are still `0.100.0` on November 5 ([release status and sources](rust-port-parity-gap-report.md)). Keep the recorded published baseline `a57cc321bfdbac6bb98c488ba49f399da8fcb98a` separate. At P0 approve a compatible source pin or verify a later release contains the required fixes. This reassessment does not approve implementation or reconcile the C# baseline by itself.

The [updated gap report](rust-port-parity-gap-report.md) records the merged PRs and their revisions. Gaps 1/2/12/14/16 are implemented upstream; gap 13 is partly implemented by #5170. The [maintainer's closure of #5087][reactor-issue-update] explicitly leaves other features for focused issues. U1 now has adoption/verification work plus the remaining AccessibilityView addition, and U6 has adoption/package verification work. U2-U5, optional U7, and V1 retain the outstanding contracts described below.

November's review does not close additional feature gaps, but adds a ListView move/select crash fix (#5222), dependency-property application/reset (#5180), fail-fast native runtime behavior (#5194), component-only app entry points (#5266), bitmap decode and TitleBar hit-testing fixes (#5294/#5296), and a WinAppSDK 2.6.4 dependency baseline (#5228). The report's [post-October change table](rust-port-parity-gap-report.md#changes-since-the-october-20-reassessment) maps these to source evidence and adoption limits.

## 1. Reconcile the two work streams before implementation

| Stream | Deliverable | Boundary |
|---|---|---|
| WinUI review and landing | Final merged application SHA; review findings and fixes; changes to UI, database, session handling, sync, packaging, or acceptance tests | Owned by the parallel WinUI review. This PR does not merge or modify #26. |
| Rust port planning | This architecture, implementation sequence, parity gates, and source-to-Rust ownership map | Documentation only until the combined plan is approved. |
| Reactor dependency adoption and remaining gaps | Approved source/release pins, adoption checks for landed fixes, and focused contracts/fixtures for the remaining section 3 work | Can proceed alongside the Rust implementation after approval; distinguish landed capability from Spectro acceptance. |

At reconciliation, compare the final WinUI SHA with the baseline above, explicitly review changes to the source surfaces in section 2, and update affected gap IDs, test expectations, and migration rules. Produce one approved dependency table with an owner, implementation PR/revision, and completion gate for each work package. A review change to the C# app becomes part of the oracle, not an unexplained deviation in Rust.

Resolve these decisions at that gate:

| Decision | Recommended default | Required evidence or approval |
|---|---|---|
| Product boundary | All-Rust native application; preserve the current feature set and current product limits | No C# interop service in the shipping package; no alternative renderer or new social/global timeline features |
| WinUI runtime | Match the approved C# application's actual WinUI DLLs, resources, and templates | The recorded C# baseline and published setup `0.100.0` use `2.4.0`; audited source now uses `2.6.4` after #5228 ([project][app-project], [release setup][setup-release-pins], [source setup][setup-pins]). Either retain required fixes while aligning metadata, Reactor bootstrap and setup to the approved runtime, or approve a C# runtime update and recapture the oracle first. The metadata tool checks agreement of SDK/bootstrap/setup and WebView pins; regenerate consumers and verify runtime resources/manifests as well ([metadata tool][reactor-metadata]). A setup constant change alone is insufficient. |
| Dependency delivery | Adopt the landed changes together at an immutable compatible revision; use small focused upstream changes for remaining gaps | Commit the Rust lockfile; pin compatible Reactor/setup/WebView/`windows-core`/projection inputs. The report's evidence SHA is an adoption candidate until runtime compatibility is approved. Replace temporary patches with a release only after the same gates pass. |
| Panel padding/borders | Prefer native property additions; permit a `Border` wrapper only if it passes geometry, pixels, focus, and automation checks | Gap 3 is not a mandatory blocker. Do not substitute child margins or change the public acceptance standard. |
| Window integration | Prefer a lifecycle-safe upstream contract; allow an app-owned typed adapter if it proves equivalent behavior and teardown | `run_window` exposes an HWND, not permission to retain a borrowed handle indefinitely. Prove subclass cleanup and callback ownership before using this route. |
| Upgrade identity | Preserve the final approved package identity, application ID, settings, credentials, and database | Use a different, isolated package/data/credential scope for side-by-side parity testing. Do not decide the production identity from the current test manifest alone. |

## 2. Target architecture and source ownership

Add a Rust workspace under `rust\` while retaining the C# app and tests as an executable oracle until cutover. Use five crates, not a one-for-one translation of every .NET project.

| Proposed crate | Responsibility | C# source to port |
|---|---|---|
| `spectro-core` | Domain types and service contracts; offline sync coordinator; presentation state/reducer; reader HTML and text presentation | `Spectro.Domain`; `Spectro.Sync` coordinator/contracts; `Spectro.ViewModels` ([domain][domain], [sync][sync], [presentation][presentation], [HTML][reader-html]) |
| `spectro-newsblur` | NewsBlur HTTP transport, response decoding/validation, and mapping into core remote contracts | `NewsBlurSharp`; `Spectro.Sync\NewsBlurSyncRemoteService.cs` ([client][client], [mapping][remote-mapping]) |
| `spectro-storage` | SQLite schema, migrations, queries, mutation/reconciliation transactions, retention, and bounded bitmap cache | `Spectro.Infrastructure` ([schema][database], [repository][repository], [images][image-cache]) |
| `spectro-windows` | Credential Locker, ApplicationData settings/paths, external URI launching, culture/date helpers, window adapter, and UI-thread WebView operations | `Spectro.App\Services.cs`, `NewsBlurSessionService.cs`, native portions of `MainWindow.xaml.cs` and `MainPage.xaml.cs` ([services][platform-services], [session][session], [window][window]) |
| `spectro-app` | Composition root, Reactor components/messages, native resources/styles, responsive layout, and packaged executable | `AppComposition.cs`, `App.xaml.cs`, `MainWindow.xaml`, `MainPage.xaml`, code-behind, and `ViewItems.cs` ([composition][composition], [resources][xaml-resources], [rows][view-items]) |

`core` must not depend on WinUI, WinRT objects, or a concrete HTTP/SQLite implementation. The NewsBlur and storage crates implement its contracts. The Windows adapter uses those services for session workflows; the application composes them. Do not reproduce reflection, XAML binding infrastructure, or .NET project boundaries where typed Rust state and messages suffice.

Recommended implementation dependencies are `serde`/`serde_json` for explicit wire formats, `rusqlite` with an architecture-correct SQLite build, `reqwest` plus Tokio for HTTP/background work, and the pinned Reactor/WebView/Windows crates for native integration. Select the HTTP TLS/proxy configuration against the Windows transport acceptance cases, rather than inheriting a different default unnoticed. Audited Reactor/WebView/window source (`0.101.0`) and setup (`0.100.1`) declare Rust `1.95` minimum; pin a compatible toolchain. Prepared versions are not yet published as of this review.

### Threading, effects, and lifetime contract

The UI thread owns Reactor components, element references, WinUI objects, WebView instances/event registrations, and HWND integration. Background work owns plain Rust requests, database operations, HTTP, image downloads, and sync state. Pass results as typed messages; do not move COM controls into Tokio tasks or block the dispatcher on network/SQLite work.

Use an application-owned Tokio runtime and a blocking database worker. Reactor's synchronous background-work API is not itself an async HTTP executor. Keep effect cancellation/join handles with the owner that started them.

Compose the application with `App::run_component` or `run_application` and deliver worker results through component senders/completions. #5266 removes AppContext/AppProxy/AppCallback, App::run_with/App::run(view), and show_menu_at; do not base the UI dispatcher or menus on those removed APIs. ComponentContext::run_window remains available for the narrowly approved native adapter ([component APIs][reactor-context]).

Every asynchronous query/selection result carries an account generation and operation version. Navigation, filter, article-selection, reset, and sign-out transitions invalidate older results. Match the C# query/refresh/selection version checks so a slower previous request cannot replace the current collection or article ([presentation][presentation]).

Sync progress may coalesce to the latest state, as C# does with a capacity-one channel and a 200 ms UI update interval. Completion, errors, mutations, and cleanup acknowledgement must not be silently dropped. Check message delivery failures and route actionable errors into the existing status/InfoBar behavior.

Distinguish service failures from Reactor runtime faults. #5194 makes invalid declarations and native failures after startup fail fast; native runtime faults cannot generally be converted into recoverable InfoBar messages. Use result-bearing ElementRef/WebView requests where provided, preserve startup diagnostics, and test abnormal termination/restart against durable transactions and cleanup intent. Shutdown dispatch can be rejected/dropped, so drain required work before closing rather than relying on a final callback. #5274's queue-owned wake scheduling does not remove this obligation. These are framework behavior constraints to reconcile at P0, not permission to weaken the product's recoverable service-error paths.

Sign-out/reset must cancel and drain sync, image, and database work before clearing account content. A UI generation check alone does not prevent an old worker from writing into a newly cleared database. On window destruction, cancel effects and unregister native callbacks before releasing their state. Persist local read/save changes transactionally before reporting them complete.

### Component boundaries

| Component | Owned UI state and responsibilities |
|---|---|
| Application window | TitleBar, Mica, startup size/icon, requested/effective theme, caption colors, activation-scoped shortcuts |
| Shell | Loading/login/reader states; settings overlay; navigation/filter/selection state; retained pane identities |
| Library | Collection/feed/story lists, searches, keyed DataItems, status/progress, dynamic row/menu text |
| Article reader | One retained WebView, readiness, navigation generation/ID, event registrations, article document, optional scroll restoration and focus transfer |
| Settings | Native settings controls, suppression of feedback while populating values, return-focus token, destructive-action dialogs |

Keep native subtrees and WebView under stable keys. Hiding a pane uses native `Visibility.Collapsed`, not removal/recreation. Keep the selected article independently of the current list: marking it read may remove it from Unread without closing its reader.

## 3. Upstream work packages and blocking gates

Gap numbers refer to the [gap report](rust-port-parity-gap-report.md), which preserves its original numbering and distinguishes it from #5087's checklist. For landed properties, consume and verify the existing public/native paths; do not commission the same additions again. Following #5180, remaining scalar properties normally use generated DependencyProperty mappings and native SetValue/ClearValue; missing individual setter methods alone are not a gap. Update schema/generation, filtered metadata/projections, declarations, native application/removal, and focused tests together. Do not hand-edit generated output or write COM vtables. The generator lives under `crates\tools\reactor`; metadata refresh/pin validation is under `crates\tools\reactor-metadata` ([generator][reactor-generator], [metadata tool][reactor-metadata]).

| Package | Current disposition and exact work | Dependency | Acceptance gate |
|---|---|---|---|
| U1: native scalar properties | Landed: 1 (#5105/#5158), 2 (#5106), 12 (#5104), 14 (#5103/#5160), and gap 13 LiveSetting (#5170). Adopt those paths, including live owner-theme dialogs. Remaining addition: gap 13 AccessibilityView for Raw thumbnails. | Approved metadata/runtime pin containing the merged changes | Set, update, and remove each property on actual controls; verify text metrics, spinner behavior, Polite announcements, Raw thumbnail automation, Cancel-default behavior, dialog live theme/shutdown, and removed-property restoration |
| U2: resources and styles | Still open: 4, 5. Scoped named theme brushes/dictionaries, system-color references, `ApplicationPageBackgroundThemeBrush`, and exact QuietButton semantics. Prove existing resource overrides first; add bounded native support as needed, without requiring a general styling API. | Approved runtime; U1 where styled properties are consumed | Light/dark/System/high-contrast changes update existing controls, icons, styles, and popups; hover/pressed/disabled/focus states match, without freezing colors as local solid values |
| U3: list containers/events/menus | 6, 7 ItemClick, 15: generated `ItemContainerStyle`, item activation by stable key, `UIElement.ContextFlyout` on row content | U1/U2 for styled properties/resources | Realized and newly virtualized rows have the same margins/padding/stretch; pointer, Enter/Space, right-click, touch hold, Shift+F10/Menu-key behavior match; stale row/menu callbacks cannot act on another item |
| U4: retained state and focus | 8, 9: native visibility, enabled ContentControl subtree, ListView references, focus capture/restore | Base native declarations | Settings disables the entire reader subtree; closing restores the captured eligible control; Back focuses the list; collapsed WebView/text/list state survives; unavailable focus targets produce an explicit result |
| U5: keyboard/window contract | 10, 11: missing accelerator keys/modifier combinations, activation/teardown, nullable caption setters or a proven typed native adapter | Approved runtime/projection strategy | Commands execute once with WebView focus; shortcuts release on deactivation/close and retry on activation; conflict feedback matches; high contrast clears the foreground override; callbacks cannot outlive the window |
| U6: runtime staging | Landed in #5109: architecture-qualified extraction caches, validation before atomic publication, staging error propagation, and a destination lock covering runtime/WebView2 output copying. Adopt fixed setup and retain its regressions when aligning runtime pins. | Approved runtime pin containing #5109 | x64 then ARM64 and reverse order work using a shared cache; failed extraction/download/copy and interrupted population fail/retry correctly; multiprocess shared-output copying is serialized. Inspect the complete Spectro payload and actual binary architecture separately from setup's selected-file checks. |
| U7: native panel properties | 3: Grid padding/border brush/thickness and StackPanel padding | U1/U2 mechanisms | Direct native geometry matches the source; optional if an approved wrapper passes the same acceptance checks |
| V1: reorder verification | Gap 7: #5222 fixes moving/selecting rich-content rows by reissuing moved DataItem maps. #5187 reports drag reorders as stable keys. Neither removes the large-reorder ReplaceAll path or supplies ItemClick. | U3 plus paired native Spectro fixture | Repeat the historical 101-item reversal on the selected pin and compare real sync/filter/insert/remove/reorder scenarios against C#. Include move+select with custom content. Check anchor/offset, focus, selection, and logical/content continuity; allow #5222's deliberate native map replacement. Issue #5146 is a separate diagnosed WinUI ScrollIntoView bug, not this fix. Resolve reproduced Spectro mismatches before P4 acceptance. |

U1-U6 remain acceptance gates for the slices that consume them; upstream feature development is now concentrated in the remaining U1 AccessibilityView contract and U2-U5. U6 no longer needs the original setup defects reimplemented. These gates do not block the platform-neutral backend after P0/P1. V1 still requires an application-specific resolution; U7 remains optional under a proven wrapper equivalence.

Use the public native builders for typography, icons, NumberBoxes, and `ContentDialog::default_button`. For dialogs, keep the owner and page themes aligned and rely on #5160's native ActualThemeChanged propagation instead of requesting a new per-dialog RequestedTheme property. #5170's descriptions/notification APIs are optional capabilities, not expanded product scope. Verify its automatic live-text announcements against the oracle while implementing the existing Polite status region.

Adjacent landed work also informs acceptance: [#5127][reactor-client-sizing] fixes custom-title-bar sizing, while [#5128][reactor-focus-tests] and [#5141][reactor-keyboard-tests] test focus/keyboard behavior without closing gaps 9-11. #5296 adds TitleBar hit-region refresh; #5294 fixes byte-backed bitmap decode sizing/ownership without adding a public decode-size API. Adopt both and retain pointer-driven chrome and bounded thumbnail-memory checks. [#5133][reactor-webview-tests] removed intermittent Reactor/WebView UI coverage; [#5134's closure][reactor-webview-followup] explicitly leaves live WebView startup in a Reactor window unverified by that suite. Keep P1/P5 readiness, teardown, and native startup tests; closure is not a product fix or native startup pass.

The missing `Microsoft.UI.*` methods in Reactor are its filtered projection, not absent Windows APIs. Generate supplemental AppWindow bindings only for the approved native adapter if needed; do not build a second general-purpose WinUI binding layer.

## 4. Data, protocol, and rendering invariants

### Database and upgrade compatibility

Preserve `spectro.db`, schema version 2, existing table/index/column names, and migration behavior. Preserve WAL, foreign-key enforcement, the 5,000 ms busy timeout, integrity checks, and explicit corruption-recovery behavior. A corrupt or newer-schema database must not be silently replaced with an empty library ([schema][database]).

Keep story identity as `story_hash`; feed/folder identity, query filters, ordering (`published_utc DESC, story_hash`), and the current 500-story query limit must match. List queries omit full article bodies and bound preview text; selecting an article retrieves its full content ([queries][repository-queries]).

Preserve per-field timestamp units: story publication uses Unix seconds, while mutation creation/upload uses milliseconds. Checkpoint JSON must preserve .NET field names, enum representations, dates, and fractional saved/hash timestamps; Rust's default enum serialization is not compatible by assumption.

Local state changes and pending mutations are one transaction. An upload records the exact mutation snapshot; acknowledgement/reconciliation must not erase newer local intent. Pending and uploaded snapshots protect state and retention until the matching remote observation arrives. Incomplete unread/saved inventories cannot turn absent observations into read/unsaved state ([mutation transactions][repository-mutations]).

Before production upgrade testing, demonstrate C# -> Rust -> C# compatibility on isolated copies with pending mutations, uploaded snapshots, pacing deadlines, saved-version checkpoints, and schema-v1 migration. The port should initially remain schema-compatible so rollback is possible. Never run both binaries concurrently against the same account database.

### Authentication, settings, and transport

Preserve the Credential Locker resource `Spectro.NewsBlur.Session` and credential name `session-cookie` in the production identity. Persist the NewsBlur session cookie, never the password. Preserve the current settings keys/types and `System`/`Light`/`Dark` and filter names; remove legacy plaintext keys without migrating their contents ([platform services][platform-services]).

Restore opens the downloaded library on offline/transient verification failure and retains the saved session. Clear it only on the same explicit authentication-rejection paths as the C# service; HTTP 403 is not automatically session revocation. Login validation, Enter in either field, duplicate-submission prevention, password clearing, and secure-save failure feedback must match ([session][session]).

Port the entire used client contract with fake transports. Important wire shapes are:

| Operation | Request shape to preserve |
|---|---|
| Login/logout/profile | POST `api/login` with URL-encoded credentials and `newsblur_sessionid` cookie extraction; POST `api/logout`; GET `social/load_user_profile` |
| Catalog | GET `reader/feeds`, including the existing favicon, folder-structure, and update-count query flags |
| Feed-scoped hash inventory | GET `reader/unread_story_hashes`, repeated `feed_id`, `read_filter=all/unread`, `order=newest`, `include_timestamps=true`; require `unread_feed_story_hashes` |
| Saved inventory | GET `reader/starred_story_hashes`, `include_timestamps=true`; validate the `starred_story_hashes` tuple list |
| Recent bodies | POST `reader/river_stories` with repeated URL-encoded `h` values and `include_hidden=true`, 1-100 hashes |
| Retained saved bodies | GET `reader/starred_stories` with repeated query `h` values, not a POST body |
| Read/save mutations | Existing POST endpoints and `story_hash` forms for read, unread, star, and unstar; preserve operation-result validation |
| Paging/fallback operations | Existing feed/river/starred page calls, unread-hash query, and mark-feed-read contract; do not drop fallback behavior merely because the normal sync uses inventories |

Retain numeric/string response compatibility, fractional timestamps, object/array feed shapes, nested-folder mapping, omitted-versus-empty distinctions, malformed-response errors, cancellation, timeout, and `Retry-After` classification. The transport boundary must not log credentials, cookies, account identifiers, response bodies, or secret-bearing URLs ([client][client], [mapping][remote-mapping]).

### Sync and mutation semantics

Keep one sync per account, maximum four concurrent requests, request-start pacing of at least one second, and saved-body pacing of eight seconds. Preserve the default three-attempt transient retry, bounded exponential delay/jitter, and durable backoff: respect server `Retry-After`; absent that header, HTTP 429 pauses for six minutes plus up to 30 seconds jitter. Manual sync and process restart must not bypass stored deadlines ([sync][sync], [inventory][inventory]).

Keep metadata-first inventory reconciliation, up to 60 recent available articles per active feed, 100-hash body batches, singleton retries for missing clustered siblings, and retained saved-copy refresh by version. Validate capped/omitted unread inventories with the same follow-up scope/page requests; unknown state must not become an authoritative empty set.

Commit successful content batches progressively and publish local revisions so browsing continues during sync. Cancellation/later failure preserves those commits and pending local changes, but does not publish a new successful-sync checkpoint. Saved-batch version checkpoints survive interruption; retention protects unread/saved/pending/uploaded state and the selected recent window.

### Reader and image behavior

Port `ReaderHtmlBuilder`'s actual CSS, HTML structure, text encoding, publication/byline/date formatting, reading-minute calculation, and settings values. Match Windows culture-sensitive search/date labels and invariant numeric CSS output; ASCII lowercasing and a generic English date formatter are not equivalents.

The C# sanitizer includes .NET regex features such as backreferences. Do not paste those expressions into Rust's `regex` crate. Implement the sanitization contract with a bounded tokenizer/parser and a golden valid/malformed-content corpus; preserve the rendered DOM of accepted content. Any intended safety change that changes the oracle's rendering needs explicit reconciliation, not a silent redesign.

Host the existing XAML WebView2 through `windows-webview`'s `reactor` feature and public `webview_result` readiness/error path ([bridge][webview-bridge]). Keep COM/WebView callbacks on the UI thread and retain event registrations for the reader lifetime. No general WebView replacement or `NavigateToString` workaround is needed.

Match these native operations ([reader initialization][reader-initialization], [navigation][reader-navigation]):

- Disable page scripting, web messages, default script dialogs, dev tools, host objects, status bar, and browser accelerator keys before navigating.
- Map `reader.spectro.invalid` to the local article directory with `HostResourceAccessKind::Deny`; write the article file and navigate to a versioned local URL, including articles larger than 2 MiB.
- Preserve the CSP and data-only embedded bitmap cover policy. Opening an article must not fetch remote scripts, frames, styles, or images.
- Allow the current local document and its fragments; cancel other reader navigation and launch permitted HTTP/HTTPS links externally. Handle new-window requests without creating an extra embedded browser.
- Correlate navigation completion with the current navigation ID before transferring focus or restoring scroll. Restore scroll when a newly cached cover triggers re-navigation of the same selected story; preserve the C# behavior for other navigations.
- Sign-out/reset blanks the reader and awaits browsing-profile/article-file/image cleanup rather than merely dropping a control.

Preserve the separate anonymous thumbnail client: HTTPS without URL userinfo; no cookies, credentials, redirects, or automatic decompression; ten-second total deadline; MIME/signature validation; 3 MiB per image; 100 unique URLs per run; 50 MiB cache bound; atomic temp-file publication and cancellation cleanup. Keep the SHA-256 URL-derived `.img` lookup and local-only reads. Reader covers remain limited to 512 KiB. Image failures retain story availability and produce the current warning behavior ([image cache][image-cache]).

### Native layout and interactions

Translate the property/style assignments in `MainPage.xaml` and `MainWindow.xaml`, not their approximate appearance. Preserve app assets, FontIcon glyphs, font families/metrics, resource scope, native container styles, tooltips, automation names/IDs, and control states.

Extract a pure layout function from the combined behavior of `ApplyResponsiveLayout` **and** `ShowReader`; copying only the first method is incorrect. Its inputs include actual client width in DIPs, selected-story state, compact-reader-open state, and browsing state. Test both sides of 820, 900, 1,000, and 1,180 DIPs ([layout][responsive-layout]).

Dense browsing at 820-999 DIPs uses the source row values: title size 12 instead of 14, thumbnail size 56 instead of 80, one summary line instead of two, and padding `8,6,8,6` instead of `12,9,12,9`. Preserve pane/search/header visibility, feed/navigation widths, the compact picker, settings-card width, and Back behavior ([rows][view-items]).

The C# startup `AppWindow.Resize(1440, 960)` sizes the outer window in physical pixels. Reactor's `client_size` uses client-area DIPs, including a custom title bar; passing the same numbers is not equivalent. [#5127][reactor-client-sizing] fixes custom-title-bar sizing and actual client-area observations, so adopt that correction when computing the source-equivalent bounds. Match actual outer/client geometry and DPI through the approved window adapter or a proven conversion. This sizing fix does not supply the activation/caption APIs in gap 11. Preserve native TitleBar drag/caption behavior, icon, Mica, and theme/high-contrast caption overrides ([window][window]).

Opening settings captures focus, disables the whole ReaderHost, and focuses ThemeBox. Closing restores the eligible captured control. Preserve numeric ranges/increments, live reader preference updates, persisted default filter, DownloadImages behavior, and Cancel as the destructive-dialog default. Verify accelerator/hotkey dispatch occurs once across normal controls, WebView focus, settings, activation, and shortcut conflicts.

## 5. Implementation sequence after approval

Each package below should be a bounded PR with its own acceptance evidence. Do not remove the C# oracle early.

| Phase | Deliverable | Prerequisites | Exit gate |
|---|---|---|---|
| P0: approved baseline and oracle | Reconciled plans; final application/runtime/dependency pins; deterministic shared fixture corpus and native visual/interaction baseline | #26 landed; combined plan approved | Every requirement maps to a source/test/gap owner; approved differences are explicit; actual runtime and client/DPI geometry are known |
| P1: Rust foundation and harness | Workspace/toolchain/lockfile with an approved current source/release revision; component-based native startup; isolated fixture/demo/test paths; Rust CI lane | P0; U6 fixed setup; aligned SDK/bootstrap/metadata; accepted runtime-fault policy | x64 and ARM64 compile/package; x64 startup works without machine-installed WinAppSDK dependencies; Debug fixture mode is absent from Release; readiness/native failures are observable; runtime/output cache regressions retained; startup failure and abnormal-exit/restart evidence captured |
| P2A: storage and domain | Core types/contracts; SQLite v1/v2 migration, queries, mutations, reconciliation, retention, recovery; bitmap cache | P1 | Existing repository/image edge cases and cross-language database compatibility pass; no new schema or destructive migration |
| P2B: transport and platform session | NewsBlur client/mapping; fake transport; Credential Locker/settings/paths/launcher/culture adapters | P1 | Request/response fixtures, cancellation/error mapping, secure-session continuity, settings round trips, and offline restore pass |
| P3: sync and presentation state | Full inventory sync; retries/pacing/checkpoints; reducers/effects; query and selection generations; cleanup barriers | P2A/P2B | C# and Rust produce equivalent database/state/request traces under interruption, competing local edits, restart, rate limiting, stale query completion, and sign-out/reset |
| P4: native shell and library | Loading/login/reader shell; landed typography/icons/live regions and list move/select fix; resources/styles; keyed lists/search/filter actions; responsive layout; native menus/status/automation | P3; U1 adoption plus remaining AccessibilityView; open U2-U4 contracts; U7 or approved wrapper | Paired native pixels/UIA/interaction checks pass for browsing and row/control states; move/select rich content works; byte-backed thumbnails stay bounded; V1 reorder gate resolved |
| P5: reader, settings, and window integration | Retained WebView; HTML/CSS; cached covers; reader focus/navigation; adopt NumberBox/default-button/owner-theme fixes; title bar/shortcuts and corrected client sizing | P4; U5 and remaining U1-U4 surfaces | Same article DOM/pixels, >2 MiB local rendering, no prohibited remote requests, focus/Back/settings behavior, live dialog themes/Cancel/shutdown, and browser-focused shortcuts pass |
| P6: production package and upgrade | Rust MSIX staging/signing/activation; same approved identity/data contract; x64/ARM64 artifacts; full parity matrix | P5; U6 | Clean install, C# upgrade, rollback-compatible data, offline restart, cancellation/reset, architecture correctness, and real ARM64 execution pass |
| P7: cutover | Default product/README/build commands point to Rust; C# shipping head retired; retained source/test oracle is deliberately archived | P6; product approval | Rust is the sole shipping implementation; every parity/release gate passes; no unexplained differences or temporary dependency/workaround without an approved disposition |

Critical path: `P0 -> P1 -> (P2A + P2B) -> P3 -> P4 -> P5 -> P6 -> P7`. The framework fixes reduce work within the gates; they do not remove phases or change the five-crate architecture, data/protocol invariants, C# reconciliation, or product approval. After approval, remaining upstream U1-U5 work can proceed independently of P2/P3; adopt U6 at P1 and complete installed-package acceptance at P6. A partial slice is useful development output, not acceptance of a compromised port.

## 6. Verification and completion criteria

### Differential tests: retain the existing behavior as the oracle

Create sanitized, shared JSON/SQL scenario fixtures and compare normalized outputs from C# and Rust. Normalize only explicitly nondeterministic values such as temporary paths/navigation-version tokens. Do not normalize state, timing deadlines, text, counts, request shapes, or errors to make mismatches pass. The application must not invoke .NET to serve production requests.

| Existing evidence to retain | Required Rust/differential cases |
|---|---|
| `NewsBlurSharp.TestsCore` | Authentication/cookie/profile behavior; exact request encoding and repeated keys; feed/object/array/tuple decoding; omitted/invalid payloads; Retry-After/429; timeout/caller cancellation; secret-free diagnostics |
| `Spectro.Infrastructure.Tests` | Schema migration and newer/corrupt DB refusal; exact mutation snapshots and stale remote state; incomplete inventory semantics; protected retention; local count/query results; bounded images, invalid MIME/signatures, streaming limits/cancellation |
| `Spectro.Sync.Tests` | No-change sync without body downloads; 60-per-feed selection; capped/omitted unread inventories; clustering/singleton retry; saved versions across restart; competing local edits; paced requests; durable backoff; committed batches after cancellation |
| `Spectro.ViewModels.Tests` | Offline session restore; login validation/Enter; newest navigation/filter/selection wins; opening unread retains the reader; read/save outside the visible filter; settings/culture/HTML presentation; reset/sign-out and sync status |
| Native integration tests | Real ListView container style/virtualization and ItemClick; rich-content move/select and reorder anchor/focus; context-menu text/key dispatch; retained collapse; disabled subtree; dialogs; title-bar hit-region updates/activation/shortcut teardown; thumbnail decode memory; WebView readiness and cleanup; fail-fast diagnostics/restart |

The existing four deterministic .NET suites and CI remain active until cutover ([CI][ci]). Add the Rust equivalent test/build/package lanes rather than replacing those jobs with a lighter smoke test.

### Pixel and interaction matrix

Capture both applications with identical Windows build, actual WinUI runtime/templates, WebView2 runtime, architecture, font availability, client/outer bounds, monitor DPI, culture/time zone, seeded clock/data, wallpaper/material background, activation, focus/hover state, and scroll. Use isolated fixture databases and original demo assets; README screenshots are checkpoints, not a sufficient oracle.

Use lossless captures and strict pixel comparison with zero differences outside narrowly declared volatile/animated regions. Pause animations or compare settled states where possible. Size mismatch, blank capture, or a shifted control is a failed/inconclusive comparison, not a pass. Whole-window similarity averages or a reduced threshold do not establish pixel identity; inspect individual text, row, button, popup, title-bar, and article regions as well.

| Axis | Required cases |
|---|---|
| Screens/state | Loading, sign-in/validation/failure, empty/populated collections, scoped feeds, all/unread/saved/read, filtered search, normal/dense story rows, progress/cancel/offline/rate-limited feedback |
| Reader | Light/dark/forced colors; cached/no cover; long/large articles, headings/tables/code/links; navigation races; compact Back and reopen of the already-selected article |
| Settings/dialogs | Open/close from each focus location; disabled underlying reader; all settings; invalid/NaN NumberBox input; sign-out/reset Cancel/default/confirm and cleanup |
| Width | Representative wide/medium/compact layouts, plus 819/820/821, 899/900/901, 999/1,000/1,001, and 1,179/1,180/1,181 client DIPs; narrow settings-card sizing |
| Theme/scale | System/Light/Dark, system theme changes while open, high-contrast changes while open; actual 100%/150%/200% DPI and mixed-DPI monitor transitions |
| Control/input | Normal/hover/pressed/disabled/focused/selected states; keyboard traversal, Enter/Space activation, pointer/touch/keyboard context menus; every shortcut with WebView focus and activation/conflict changes |
| Architecture/install | x64 and real ARM64; clean signed MSIX install/activation; C# -> Rust update preserving state; offline restart; uninstall/reinstall behavior; no wrong-architecture runtime reuse |

Reuse `Test-SpectroLibraryUI.ps1`, the fixture-driven library runner, and the release acceptance scenarios where possible ([UI runner][ui-runner], [release gates][release-checklist]). Parameterize package identity/executable selectors for side-by-side runs without weakening state assertions. Preserve automation IDs and names so the same scenarios can drive both implementations.

Reserve live-account acceptance for an explicitly authorized, dedicated test account. Secrets remain outside source/artifacts/logs. A successful outbound mutation requires both remote observation and the corresponding local pending/uploaded acknowledgement, not just an "Up to date" label. Restore the fixture after each live scenario.

### Packaging and cutover gate

Add a Rust-specific staging/packaging path instead of pointing the existing .NET packaging script at a Cargo directory. That script expects Native AOT output, generated manifests, app `resources.pri`, and application XBF files; Reactor's declarative application has a different build-output shape ([packager][package-tool]).

Stage the native executable, approved AppxManifest identity/activation/capabilities/minimum OS, original assets and qualified-resource index where required, architecture-correct WinUI runtime resources/DLLs, and WebView integration DLLs. Use #5109's corrected staging/cache/destination-lock implementation rather than duplicating it. Verify the actual payload, all required files, and PE architecture; setup's selected-file publication checks are not a full Spectro manifest audit. Preserve the self-contained deployment contract and existing x64/ARM64 distribution. Test signing artifacts contain the public certificate only, never private signing material, demo content, account data, or a .NET runtime.

Run the installed package, not merely a loose Cargo executable, for final ApplicationData/Credential Locker/asset/activation checks. Reuse the install/launch assertions with an explicit Rust payload contract ([MSIX checks][msix-tests]); ARM64 cross-compilation is not proof of ARM64 startup.

**The port is complete only when:** all approved baseline features and limits match; differential state/wire/database tests have no unexplained differences; the native pixel/interaction matrix passes; data/session upgrade and cleanup are proven; both architecture packages execute on their target architectures; and the combined-plan/product approval authorizes cutover. Existing unresolved Store-certification and physical-device gates remain unresolved until separately satisfied, not marked complete merely because the language changed.

[app-project]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/Spectro.App.csproj#L1-L67
[composition]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/AppComposition.cs#L1-L122
[domain]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.Domain/IContentRepository.cs#L1-L88
[database]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.Infrastructure/SqliteDatabase.cs#L1-L254
[repository]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.Infrastructure/SqliteContentRepository.cs
[repository-queries]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.Infrastructure/SqliteContentRepository.cs#L311-L369
[repository-mutations]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.Infrastructure/SqliteContentRepository.cs#L436-L639
[client]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/NewsBlurSharp/NewsBlurClient.cs#L101-L418
[remote-mapping]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.Sync/NewsBlurSyncRemoteService.cs#L1-L400
[sync]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.Sync/OfflineFirstSynchronizer.cs#L1-L408
[inventory]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.Sync/OfflineFirstSynchronizer.Inventory.cs#L1-L268
[presentation]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.ViewModels/AppViewModel.cs#L1-L687
[platform-services]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/Services.cs#L28-L147
[session]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/NewsBlurSessionService.cs#L1-L208
[image-cache]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.Infrastructure/StoryImageCache.cs#L1-L298
[reader-html]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.ViewModels/ReaderHtmlBuilder.cs#L1-L108
[reader-initialization]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L324-L386
[reader-navigation]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L735-L815
[responsive-layout]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L521-L608
[window]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainWindow.xaml.cs#L18-L180
[xaml-resources]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml#L10-L79
[view-items]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/ViewItems.cs#L1-L105
[ci]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/.github/workflows/ci.yml
[ui-runner]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/tools/Test-SpectroLibraryUI.ps1
[package-tool]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/tools/New-SpectroMsix.ps1
[msix-tests]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/tools/Test-SpectroMsix.ps1
[release-checklist]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/docs/release-checklist.md
[setup-release-pins]: https://github.com/microsoft/windows-rs/blob/a57cc321bfdbac6bb98c488ba49f399da8fcb98a/crates/libs/reactor-setup/src/lib.rs#L8-L17
[setup-pins]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor-setup/src/lib.rs
[reactor-generator]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor/src/main.rs
[webview-bridge]: https://docs.rs/windows-webview/0.100.0/windows_webview/fn.webview_result.html
[reactor-issue-update]: https://github.com/microsoft/windows-rs/issues/5087#issuecomment-6278409857
[reactor-client-sizing]: https://github.com/microsoft/windows-rs/pull/5127
[reactor-focus-tests]: https://github.com/microsoft/windows-rs/pull/5128
[reactor-keyboard-tests]: https://github.com/microsoft/windows-rs/pull/5141
[reactor-webview-tests]: https://github.com/microsoft/windows-rs/pull/5133
[reactor-metadata]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor-metadata/src/main.rs
[reactor-context]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/component/context.rs
[reactor-webview-followup]: https://github.com/microsoft/windows-rs/issues/5134#issuecomment-6392397764
