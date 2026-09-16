# Spectro production migration inventory

This inventory records the completed replacement boundary for the production
WinUI 3, SQLite, Native AOT application. The former UWP head and its legacy
dependencies were removed after the replacement vertical slice passed.

## Application head

| Current surface | Current dependency | Production replacement | Removal gate |
|---|---|---|---|
| Removed `src/Spectro` | .NET UWP and `Windows.UI.Xaml` | Packaged WinUI 3 project in `src/Spectro.App` | Replacement login, local feed list, story list, reader, settings, and responsive shell are implemented |
| `Package.appxmanifest` | Universal/phone template identity | Final Store-reserved Desktop package identity | Store identity is reserved before account-scoped persistence is finalized |
| Activation/navigation services | UWP activation and `Frame` APIs | Windows App SDK activation and explicit shell navigation | WinUI shell owns startup and deep-link routing |
| Application settings | UWP roaming settings | Local typed settings plus Credential Locker for the session | Legacy plaintext keys are deleted and never migrated |

## Persistence and synchronization

| Current surface | Current dependency or risk | Production replacement | Removal gate |
|---|---|---|---|
| Realm-derived `NewsFeed`, `Story`, and `Session` | Persistence types leak into domain and view models | Persistence-neutral domain entities | SQLite repository integration tests are green |
| `RealmDataCacheService` and `IDataCacheService` | Expression-based queries, ambient transaction, thread-affine Realm instances | Explicit asynchronous SQLite repositories and transaction boundaries | All callers use repository contracts |
| `Synchronizer` | Blocking waits, continuation result access, incomplete outbound/reconcile stages | New staged offline-first synchronization coordinator | Scripted interruption and conflict tests are green |
| Removed Fody weaver files and Realm package references | Build-time weaving and Native AOT uncertainty | Explicit AOT-safe presentation code | Legacy cache and weaving paths are absent from the solution |

The SQLite story primary key is the stable NewsBlur `story_hash`. Other service
IDs remain attributes. Retention must protect unread stories, saved stories,
and stories with pending mutations.

`Spectro.Sync` owns the platform-neutral offline-first pipeline. It serializes
work per account and executes initialize, pending upload, catalog refresh,
story/unread/starred fetch, transactional reconciliation, retention, and
checkpoint stages. Successfully uploaded mutation snapshots are recorded in
SQLite and remain authoritative until a later remote snapshot observes the
same value; newer local mutations therefore cannot be acknowledged or
overwritten by stale remote state.

## NewsBlur client

| Current surface | Current dependency or risk | Production replacement | Removal gate |
|---|---|---|---|
| Direct `HttpClient` construction | Difficult deterministic testing and inconsistent lifetime | Injected HTTP transport/client | All endpoint tests use fake handlers |
| Newtonsoft.Json reflection serialization | Native AOT and trimming risk | `System.Text.Json` source-generated contexts | MVP response fixtures parse in Native AOT |
| Live account tests | Credentials, network dependency, mutable service state | Deterministic request/response contract tests | Default test suite has no network dependency |
| Signup/social methods | Outside MVP | Remove from production client surface | No application caller remains |

The MVP endpoint matrix covers login/logout, feeds and folders, river stories,
feed stories, starred stories, unread hashes, read/unread mutations, star and
unstar, and mark-feed-read where the UI uses it.

## UI and controls

| Current surface | Current dependency | Production replacement | Removal gate |
|---|---|---|---|
| Cimbalino shims | Legacy UWP navigation, settings, and behavior APIs | WinUI 3 controls and platform-neutral presentation services | Equivalent WinUI flow is tested |
| Microsoft Toolkit UWP shims | Legacy image and blur helpers | WinUI 3 image loading and supported system materials | No XAML reference remains |
| Placeholder command bars and labels | Incomplete product behavior | Only functional, localized commands | MVP interaction acceptance tests are green |
| Two-column feed/story page | No complete article reader or responsive narrow state | Responsive navigation/feed/story/reader shell | Wide, medium, and narrow Hyperloop checks are green |

## Native AOT and packaging

- Production RuntimeIdentifiers are `win-x64` and `win-arm64`; x86 is excluded.
- Native AOT is enabled in the WinUI 3 project from its first vertical slice.
- SQLite, article rendering, serialization, dependency registration, and
  diagnostics must remain trimming-safe.
- x64 and ARM64 packages must both publish. Each architecture must be installed
  and execute the principal flow before Store submission.
- The final Store identity, publisher, version strategy, and minimum Windows
  version are Phase 0 decisions because they affect package upgrade, app data,
  and Credential Locker continuity.

## Security and privacy

- No credentials, session cookies, account identifiers, or live mutable
  fixtures belong in source or default tests.
- Session credentials use OS-protected storage.
- First launch of the new app deletes legacy plaintext roaming credential and
  cached-profile values without migrating them.
- Sign-out and cache reset delete account SQLite rows, credentials, article
  renderer data, and diagnostic logs.
- Remote article content behavior is documented and reflected in the privacy
  policy before Store submission.
