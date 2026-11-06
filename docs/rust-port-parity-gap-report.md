# Spectro: Reactor parity gaps and landed fixes

**Reviewed:** 2026-11-05, against windows-rs master at `db9801679bac964b6367b1d1d1bf8472ab001065`. The October 20 reassessment used `ed16707c4b889698e68016b7add119d98b283e11`; the original audit used `9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22`. Spectro examples still refer to PR #26 at `44c28d223e1d2170cfdf423fa3ff83467e9a3f53` pending application-baseline reconciliation. PR #26 remains open at head `7db3a8656f392fd555e5eee760a3d361834cad9e` as verified on November 5.

The [October 19 maintainer update][issue-update] closes umbrella issue #5087 after several fixes landed. It explicitly leaves the remaining features for focused issues; closing the issue does not close every gap below. The November source review finds **no further closure of the original feature gaps** beyond October's five landed areas and partial accessibility support. Later correctness, API, and runtime changes still affect adoption. Gap numbers remain stable for the [port plan](rust-port-plan.md); they differ from the issue's checklist numbers.

**Status meanings:** “Landed” means the missing upstream capability is implemented at the audited revision, with the cited upstream coverage. Spectro adoption and its native parity checks are still required. This review inspected a fresh source checkout, generated declarations/native adapters, tests, commit history, PR metadata, and issue follow-ups; it did not execute Reactor or a Rust Spectro application. [#5316][pr-5316] prepares Reactor/WebView/window `0.101.0` and setup `0.100.1`, all with Rust `1.95` minimum. On November 5 the crates.io APIs still report `0.100.0` as the latest published Reactor/setup/WebView versions ([Reactor][release-reactor], [setup][release-setup], [WebView][release-webview]). Source version preparation is not publication. Pin a compatible source revision or verify a later release before adoption.

| Gap | Current status | Port consequence |
|---|---|---|
| 1: TextBlock typography | Landed: #5105, #5158 | Consume the four native properties; retain typography comparisons |
| 2: FontIcon size/foreground | Landed: #5106 | Consume native properties; app-defined theme brushes still need gap 5 |
| 3: panel padding/borders | Open, optional | Use a proven Border wrapper or request the native builders |
| 4: QuietButton | Open | Prove the exact setters and state precedence through bounded native support |
| 5: theme resources | Open | Named brushes, scoped theme dictionaries, and high contrast remain required |
| 6: ListView container style | Open | Generated row containers still need styling support |
| 7: ItemClick / reorder continuity | Open / verification required | #5222 fixes a move/select crash; ItemClick and large-reorder scroll/focus parity remain outstanding |
| 8: retained visibility | Open | Native collapse is still required |
| 9: disabled content host / focus | Open | ContentControl, ListView references, and focus capture/restore remain required |
| 10: accelerator keys/modifiers | Open | A fork proposal and new tests do not add the required accelerator declarations |
| 11: window lifecycle/caption colors | Open | Lifecycle-owned shortcuts and explicit caption overrides remain required |
| 12: NumberBox options | Landed: #5104 | Consume native increment/spinner properties |
| 13: UI Automation | Partial: #5170 | LiveSetting is available; AccessibilityView is still missing |
| 14: ContentDialog | Landed: #5103, #5160 | Set the default button; use owner-theme propagation instead of duplicating RequestedTheme |
| 15: row context menus | Open | Arbitrary-element native context-menu attachment remains required |
| 16: runtime staging | Landed: #5109 | Adopt fixed setup; retain package architecture/payload and concurrency checks |

## Merged changes used in this reassessment

Merge revisions below are evidence, not independent versions to mix into one build. The audited master revision above contains them together.

| PR | Merged (UTC) | Merge revision | Relevant change |
|---|---|---|---|
| [#5103][pr-5103] | 2026-10-16 | `d3c0d1e528f02edec11c5c2902eb2aa8565219c1` | ContentDialog default button and removal/reset handling |
| [#5104][pr-5104] | 2026-10-16 | `f2dd405ec7e370874c71dd8fce39568c80836c66` | NumberBox SmallChange and spin-button placement |
| [#5105][pr-5105] | 2026-10-16 | `33fefee73549eddef9d9aca2a17f98d9c032f3f8` | TextBlock text alignment |
| [#5106][pr-5106] | 2026-10-16 | `ee70f1e719244b7b5b4328ca9ed99625d3b77a54` | FontIcon size and foreground |
| [#5109][pr-5109] | 2026-10-16 | `9e30330d50b27da94dc70cee13cba4cdccbaabb1` | Architecture-specific completed caches, fatal staging errors, destination locking, and Cargo native-test foundation |
| [#5158][pr-5158] | 2026-10-19 | `afcb0ab1f8cf7a71ec563f659451748b26e7f5b6` | TextBlock font family, character spacing, and line height |
| [#5160][pr-5160] | 2026-10-19 | `0a5ced4d9082c34c694d06b015cfe95f47011c1d` | Dialog theme propagation, live theme updates, and shutdown with an open dialog |
| [#5170][pr-5170] | 2026-10-20 | `b95e10774b625f4a95ba28c7bc3b5ef6a08a6bef` | Live regions, help/full descriptions, and UIA notification requests; resolves focused issue #5135 |

## Changes since the October 20 reassessment

These changes improve correctness or change implementation assumptions; they do not supply the remaining public contracts in gaps 3-11, 13, or 15.

| PR | Merged (UTC) / merge revision | Impact on the port |
|---|---|---|
| [#5180][pr-5180] | 2026-10-21 / `fa8fb815a65514dcc184373e5e5add97d3facc0e` | Most scalar properties now use generated DependencyProperty mappings with SetValue/ClearValue. Missing individual setter methods no longer establish a gap by themselves; public declarations and native application/reset paths are the evidence. |
| [#5187][pr-5187] | 2026-10-21 / `ff0ba648cbe8f84e6fd2b05b06be44f0dae37dd1` | User drag reorders report stable `Vec<Key>` and synchronize retained order. This is not ItemClick or proof of sync-reorder scroll/focus continuity. |
| [#5194][pr-5194] | 2026-10-22 / `c8660706878234e70702815a1e8e95354890543b` | Invalid declarations/native runtime failures after startup fail fast instead of rolling back. Keep recoverable service errors in app messages; do not assume every native failure can reach an InfoBar. Typed imperative-result requests remain available. |
| [#5222][pr-5222] | 2026-10-27 / `d6443f28190f3e6406611a52780a511493265eb8` | Moving and selecting a DataItem with content no longer double-parents its native content. The moved item receives a fresh native map; selection/event identity follows it. The separate ReplaceAll path remains. |
| [#5228][pr-5228] | 2026-10-27 / `2b5f63797a464bbe571b444b1f19dbd26d98c228` | Runtime/metadata move from WinAppSDK 2.5.1 to 2.6.4. The recorded C# baseline uses 2.4.0; align runtime, bootstrap and generated metadata together or approve an oracle runtime update. |
| [#5266][pr-5266] | 2026-10-29 / `69ec56d665d79bbe35ca29593b0bfdfb47303ceb` | AppContext/AppProxy/AppCallback, run_with, App::run(view), and show_menu_at are removed. Use component/application entry points and component senders. ComponentContext::run_window still exists; lifecycle obligations in gap 11 remain. |
| [#5274][pr-5274] | 2026-10-30 / `9ca4a32f717ecee1f25e4db95ecf25915ed9e21b` | Enqueueing native work also wakes its event loop. This does not establish new queue bounds, app cancellation barriers, or lossless shutdown delivery. |
| [#5294][pr-5294] | 2026-11-04 / `73132991b3cde4ab580204bbc62b04f6f810a0a3` | Byte-backed bitmap decoding waits for live attachment and uses a source per consumer. Adopt for thumbnail memory behavior; no decode-size API or change to Spectro's download/cache bounds is implied. |
| [#5296][pr-5296] | 2026-11-04 / `3a7b8832a8e8a22312c66a24a1379b2ea0b1946c` | TitleBar drag regions refresh after controls are inserted, moved, or removed. Retain pointer-driven title-bar acceptance; this does not add activation/caption-color APIs. |
| [#5316][pr-5316] | 2026-11-05 / `db9801679bac964b6367b1d1d1bf8472ab001065` | Prepare new crate versions; select a compatible dependency set and lockfile, not an assumed crates.io release. |

The [current schema][schema-text], [generated declarations][current-builders], [dependency-property adapter][dependency-adapter], and public component/reference types were checked for each remaining gap. Generation now draws metadata from `crates/tools/reactor-metadata`; keep its SDK pin, Reactor bootstrap pin, setup pin, and generated bindings consistent ([metadata tool][metadata-tool]). Source links below target the new module layout.

## 1. TextBlock: typography properties landed

| Required WinUI property | Spectro usage | Current Reactor contract |
|---|---|---|
| `TextBlock.FontFamily` | `"Georgia"` on loading, login, empty-state, and settings headings; for example, the login heading uses `FontSize="42"` | `font_family(...)`, with native FontFamily construction (#5158) |
| `TextBlock.CharacterSpacing` | Branding `-30`; labels `100`, `130`, `150`, `160` | `character_spacing(i32)`, including negative spacing (#5158) |
| `TextBlock.LineHeight` | Story summaries `16`; reader empty-state copy `23` | `line_height(f64)`; zero restores automatic line height (#5158) |
| `TextBlock.TextAlignment` | `Center` on wrapped empty-state headings and messages | `text_alignment(TextAlignment)`, applied to the text itself (#5105) |

**Status: landed.** [#5105][pr-5105] and [#5158][pr-5158] add the public declarations, generated/native handling, and reset behavior. Coverage includes rendered alignment, changing typography on retained controls, automatic line height, and removing overrides. See the [current schema][schema-text] and [generated builders][current-builders].

**Spectro adoption:** port the exact source values and compare font selection, glyph advances, line placement, wrapping, alignment, and property removal against the C# oracle ([TextBlocks][app-xaml]). No additional upstream typography feature is required for these four assignments.

## 2. FontIcon: FontSize and Foreground landed

Spectro sets `FontIcon.FontSize` to values including `11`, `16`, `17`, `23`, `28`, `36`, and `40`, and sets `Foreground` to `BrandBrush` or `MutedInkBrush`.

**Status: landed** in [#5106][pr-5106]. `FontIcon::font_size` and `foreground` use the native size and existing theme-brush paths. Removing overrides restores native sizing and inherited foreground. Upstream coverage exercises retained icons, solid/theme brush transitions, Light/Dark changes, clearing, and inheritance.

**Evidence:** [Spectro FontIcon declarations][app-xaml]; [current FontIcon schema][schema-icon]; [generated builders][current-builders]; [merged change and coverage][pr-5106].

**Spectro adoption:** use these properties for the original glyphs, sizes, and inheritance. `BrandBrush` and `MutedInkBrush` are app-defined resource keys; #5106 does not add named theme dictionaries, so their exact live palette behavior still depends on gap 5.

## 3. Grid and StackPanel: missing native padding and border properties (ergonomic; workaround exists)

| Required property | Spectro usage | Missing Reactor contract |
|---|---|---|
| `Grid.Padding` | Navigation row `12,8`; feed row `8,4`; article toolbar `12,8`; sync footer `22,12`; navigation footer `16,12`; filter footer `8`; story row bound to `PreviewPadding` | Absent from Grid schema/builders and its generated property mapping. |
| `Grid.BorderBrush` | `RuleBrush` on pane separators, story rows, and toolbar/footer rules | Absent from Grid schema/builders. |
| `Grid.BorderThickness` | StoryPane `1,0`; story row and article toolbar `0,0,0,1`; feed/navigation footer `0,1,0,0` | Absent from Grid schema/builders. |
| `StackPanel.Padding` | Login panel `24,64` | Absent from StackPanel schema/builders. |

**Evidence:** [Spectro layout properties][app-xaml]; [Grid/StackPanel schema][schema-panels]; [IGrid projection][native-grid]; [IStackPanel projection][native-stack].

**Optional change:** add these properties to the actual panel declarations and generated dependency-property/brush paths. Story row padding must update between normal `12,9,12,9` and dense `8,6,8,6` values ([ViewItems][app-view-items]).

**Workaround available today:** Reactor's `Border` declares `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, and `Padding` ([Border schema][schema-border]). Wrapping each panel in a `Border` that carries these properties should produce the same insets, separators, and available width, at the cost of one extra native element per row. Accept the wrapper only after paired screenshot/property/input/UIA checks and an explicit, location-specific allowance in the [normalized live visual tree gate](rust-port-plan.md#live-xaml-visual-tree-and-accessibility-comparison). Replacing panel padding with child margins is **not** equivalent.

**Classification:** convenience gap, not a parity blocker. Adding the native panel properties removes the extra element from every story row.

## 4. Button: QuietButton setters cannot be represented

**Status: open.** The current schema still lacks the direct setters below. Existing `resource_overrides` already supports some foreground/border customization; evaluate it before requesting additional API surface.

Spectro's `QuietButton` style requires:

| Setter | Required value | Missing surface |
|---|---|---|
| `Control.Padding` | `10` | No Button schema property/builder or generated Button mapping. |
| `Control.Foreground` | `{ThemeResource InkBrush}` | No direct Button property/builder; existing resource overrides do not supply app-defined InkBrush dictionaries. |
| `Control.BorderThickness` | `0` | No direct Button schema property/builder; evaluate existing resource overrides for equivalent rendering. |

**Evidence:** [QuietButton style][app-styles]; [Button schema][schema-button]; [IControl projection][native-control]; [ButtonStyle enum and resource values][brush-types]; [native style adapter][style-adapter].

`ButtonStyle` accepts only `Default`, `Accent`, `Subtle`, and `TextLink`. It cannot accept the app's `QuietButton` style or an arbitrary typed setter collection. The native style adapter only composes predefined button styles and its restricted theme-brush assignments.

**Required capability:** represent these exact setters with native precedence, using existing resource overrides where equivalence is proven and bounded property/style support for the remainder. The [upstream checklist][issue] does not require an unrestricted styling API. Preserve disabled, hover, pressed, and focus rendering when choosing between native style setters, resource overrides, and local values.

**Blocked parity:** toolbar/button padding, foreground inheritance into child icons, and border rendering. Choosing `Subtle` because it looks similar is not an equivalent implementation.

## 5. Theme resources: no app-defined ThemeResource brush references or theme dictionaries

**Status: open.** Neither the FontIcon changes nor automatic dialog theme propagation adds these app-defined resource contracts.

Spectro defines these brush keys in `ResourceDictionary.ThemeDictionaries`:

`SidebarBrush`, `ListBrush`, `PaperBrush`, `InkBrush`, `MutedInkBrush`, `RuleBrush`, `BrandBrush`, `SelectionBrush`.

Each has light/dark values. The HighContrast dictionary maps them to `SystemColorWindowColor`, `SystemColorWindowTextColor`, `SystemColorHotlightColor`, or `SystemColorHighlightColor`. Controls consume them through `{ThemeResource ...}`, including brushes inside styles.

`MainWindow.xaml` also sets `WindowRoot.Background="{ThemeResource ApplicationPageBackgroundThemeBrush}"` ([MainWindow][app-main-window-xaml]). That key is not one of the eight `ThemeBrush` variants. The nearest variant, `SolidBackground`, resolves to `SolidBackgroundFillColorBaseBrush`, a different resource. The mismatch shows behind the title-bar row.

**Missing:**

- `Brush` cannot reference an arbitrary resource key. Its `Theme` variant accepts a closed `ThemeBrush` enum with eight predefined WinUI keys.
- No declaration for a scoped `ResourceDictionary.ThemeDictionaries` containing Light/Default/HighContrast dictionaries.
- No general Page/Grid resource scope for the app's brush definitions.
- `ColorScheme` carries only Light/Dark; it cannot represent high-contrast state or changed system contrast colors.

**Evidence:** [app theme dictionaries][app-themes]; [Brush/ThemeBrush/ResourceOverrides types][brush-types]; [resource/style application][resource-adapter]; [ColorScheme enum][window-types].

**Required change:** add named theme-resource brush references and scoped theme dictionaries, with native re-resolution on theme/high-contrast changes. Alternatively, an app-owned native theme adapter must supply exactly the same dictionary resolution and change notifications; a one-time solid-color copy is insufficient.

**Blocked parity:** the app-specific palettes, live theme switching, and system high-contrast color mappings.

## 6. ListView: no ItemContainerStyle contract for the generated native containers

**Status: open.** The audited ListView schema/builders still expose neither generated-container styling nor the required container setters.

| Native container style | Required setters |
|---|---|
| `LibraryItemStyle` | `MinHeight=44`, `Padding=0`, `Margin=8,1`, `HorizontalContentAlignment=Stretch` |
| `StoryItemStyle` | `Padding=0`, `Margin=8,2`, `HorizontalContentAlignment=Stretch` |
| `FeedItemStyle` | `MinHeight=32`, `Padding=0`, `Margin=8,0`, `HorizontalContentAlignment=Stretch` |

**Missing:** no `ListView.ItemContainerStyle` declaration or native container-style adapter. `ListViewItem` lacks `Padding` and `HorizontalContentAlignment` builders. Its generic margin/min-height builders do not provide a way to style the containers generated for a ListView's DataItems.

Reactor's ListView accepts `DataItem`, not explicit `ListViewItem`, and its adapter assigns a ContentPresenter-based item template. Styling the View inside `DataItem.content` therefore does not style the native container surrounding that View.

**Evidence:** [Spectro container styles][app-styles]; [ListView/DataItem schema][schema-list]; [ListView public item contract][list-builder]; [ListViewItem builders][container-builder]; [native list template][list-template].

**Required change:** provide a typed `ItemContainerStyle` applied to realized native ListViewItems, including those four setters. Apply it to containers created later through scrolling and incremental insertion, not just currently realized items.

**Blocked parity:** native selection/hover/focus backgrounds, content stretching, row padding, container margins, and minimum row heights.

## 7. ListView: missing ItemClick; whole-list replacement during reorders

**Status: ItemClick remains open; reorder continuity remains an acceptance gate.** The similarly named `ItemClicked` event in the schema belongs to another control, not ListView.

### ItemClick

| Required native contract | Spectro behavior | Missing surface |
|---|---|---|
| `ListViewBase.IsItemClickEnabled=True` and `ItemClick` | Clicking an already-selected story reopens the article in compact layout | No property/event in Reactor ListView; native `SetIsItemClickEnabled`, `ItemClick`, and `RemoveItemClick` remain unprojected slots. |

`ItemClick` fires for pointer clicks **and** for Enter/Space on a focused item. A `Tapped` handler on the row content handles pointer input only, so it is not equivalent. `SelectionChanged` does not fire when the user clicks the item that is already selected.

**Evidence:** [StoryList XAML][app-story-list]; [selected-story click handler][app-item-click]; [ListView schema][schema-list]; [native ListViewBase slots][native-item-click].

**Required change:** add native item-click registration and dispatch by stable DataItem key. Preserve the distinction between selection changes and clicking an item whose selection is unchanged. Generating ListView in #5183 and changing drag callbacks to `Vec<Key>` in [#5187][pr-5187] do not provide this activation event.

### Collection updates and scroll anchoring

Spectro sets `ItemsStackPanel.ItemsUpdatingScrollMode="KeepItemsInView"`, which is the WinUI default. Reactor needs no new property for it as long as ListView keeps the default `ItemsStackPanel`. (`ItemsPanel`, `ItemsStackPanel`, and `ItemContainerStyle` don't appear in the filtered projection at all.)

Spectro updates its collection incrementally with `RemoveAt`/`Insert` ([incremental sync][app-incremental]). When a reorder moves all but at most one child, Reactor's container relation calls `IVector.ReplaceAll` instead ([container reorder][relations-replace-all]). Smaller reorders use `RemoveAt`/`InsertAt`. Reactor explicitly restores the selected item afterward; it does not explicitly restore the visible anchor, scroll offset, or focused container. The different mutation strategy is a parity risk, not proof by itself that WinUI loses those states.

**Required verification/change:** compare large sync reorders against C# with a scrolled, focused, selected ListView. If `ReplaceAll` changes the visible anchor, offset, or focus, preserve those states explicitly or use keyed mutations for that path. The default `KeepItemsInView` mode does not require a new property.

**Scroll/focus evidence:** the [October 16 maintainer repro][reorder-repro] reverses 101 stable-key ListView items after scrolling/focusing row 50; the `ReplaceAll` path resets scroll to the top and moves focus to the new first item. This is historical synthetic evidence, not a fresh execution against November master or proof of Spectro's expected behavior under a real sync scenario. Repeat the scenario on the selected dependency revision before acceptance.

**New ListView fix:** [#5222][pr-5222] fixes a distinct crash when moving and selecting an item with custom content. The incremental path now clears content from the old native map and issues a new map for the moved item; selection/event identity follows the replacement. The [upstream UI regression][list-tests] checks moving/selecting the rich-content row. Include that scenario in adoption tests, and compare logical item/content state rather than requiring the moved native map to retain identity. This fix does not restore anchor/focus around large `ReplaceAll` operations.

**Separate platform bug:** [issue #5146][list-crash] is now closed as not planned. The [October 27 diagnosis][list-crash-diagnosis] identifies a WinUI ScrollIntoView sentinel-container AV, reproduced without Reactor and also affecting the C# host. Reactor does not call ScrollIntoView; this was not closed by #5222. The diagnosis used runtime 2.5.1, so do not claim either failure or repair on 2.6.4 without a repro. Avoid treating a custom scrolling adapter as a proven workaround.

[#5114][pr-5114] improves keyed moves for **owned** native collections and preserves focus on unmoved controls. ListView DataItems use the separate **container** path, which still calls `ReplaceAll` at the threshold ([current implementation][relations-replace-all]). Do not mark this gate closed based on that PR.

**Blocked parity:** compact-mode reopening and keyboard activation of the selected story require ItemClick. Scroll/focus stability remains a Spectro verification gate, with the upstream synthetic failure documented above.

## 8. Visibility: no retained Visible/Collapsed property

**Status: open.** The audited shared visual schema still has no native Visibility property.

Spectro changes `UIElement.Visibility` on `LoadingPanel`, `LoginPanel`, `ReaderPanel`, `SettingsPanel`, `NavigationPane`, `FeedPane`, `StoryPane`, `ArticlePane`, `ArticleReader`, search controls, empty states, progress, and Back/Cancel controls.

**Missing:** `Visibility` is absent from shared visual properties/public builders and their generated native application path. Internal template Visibility bindings do not expose retained app-controlled collapse.

**Evidence:** [shared visual schema][schema-visual]; [native UIElement slots][native-visibility]; [screen visibility changes][app-screen]; [responsive visibility changes][app-responsive].

**Required change:** expose the native `Visibility` enum and retained property updates. Collapsing must preserve the existing native instance while excluding it from layout, hit testing, focus navigation, and the appropriate automation view.

**Blocked parity:** pane switching and overlays without destroying browser state, text-input state, native selection, or scroll position. Removing/recreating a subtree is not `Collapsed`; opacity zero is not `Collapsed`.

## 9. Settings: missing disabled ContentControl subtree and focus capture/restore

**Status: open.** [#5128][pr-5128] adds tests for existing ElementRef focus and stale-reference handling; it does not add ContentControl, ListView references, or capture of the currently focused native control.

Spectro wraps the reader in:

`ContentControl ReaderHost`, with `HorizontalContentAlignment=Stretch` and `VerticalContentAlignment=Stretch`.

Opening settings captures `FocusManager.GetFocusedElement(XamlRoot) as Control`, sets `ReaderHost.IsEnabled=false`, and focuses ThemeBox. Closing settings reenables ReaderHost and calls `Focus(Programmatic)` on the captured Control. Back calls `StoryList.Focus(Programmatic)`.

**Missing:**

- No public ContentControl declaration exposing the content, enabled state, and content alignments.
- No focus-manager capture/restore operation or opaque token for the currently focused native Control.
- ListView has no `element_ref` builder or `ReferenceElement` implementation, preventing the StoryList focus request.

**Evidence:** [ReaderHost XAML][app-reader-host]; [open settings][app-settings-focus]; [close settings][app-close-settings]; [Back handler][app-back]; [ListView builders][list-builder]; [ElementRef operations][reference-api].

**Required change:** add an enabled ContentControl host, ListView reference binding, and a lifetime-safe focused-control capture/restore operation. Restoration must report a removed/unavailable target rather than dereference a stale object.

**Blocked parity:** keyboard exclusion from the underlying reader, exact settings return focus, and Back-to-list focus. Disabling only toolbar buttons does not disable the native reader subtree.

## 10. Keyboard accelerators: missing keys and combined modifiers

**Status: open.** The October 18 issue comment describes a fork/proposed API, not a merged implementation. The audited enums and adapter still have the restrictions below. [#5141][pr-5141] tests keyboard routing and accelerator callback replacement without changing the runtime or declarations.

| Spectro shortcut | Missing Reactor enum value |
|---|---|
| Ctrl+D | `AcceleratorKey::D` |
| Ctrl+O | `AcceleratorKey::O` |
| Ctrl+Shift+S | `AcceleratorKey::S` and a Control+Shift modifier combination |
| Ctrl+Shift+M | `AcceleratorKey::M` and a Control+Shift modifier combination |

**Evidence:** [Page accelerators][app-accelerators]; [AcceleratorKey/AcceleratorModifiers][accelerator-types]; [native accelerator adapter][accelerator-adapter].

`AcceleratorModifiers` has only `None` and `Control`; the adapter translates exactly those two choices. It cannot encode `VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift`.

**Required change:** add the required virtual keys and composable native modifiers, and update the adapter. Attach the accelerators at the same effective root scope.

**Blocked parity:** settings, open-original, save, and mark-read shortcuts.

## 11. Native window: missing activation callbacks and caption-color declarations (no safe built-in API; raw-HWND workaround exists)

**Status: open.** The newer native window/input tests do not add activation observation, teardown-owned shortcut registration, or these explicit caption-color setters. [#5127][pr-5127] fixes custom-title-bar client sizing; [#5296][pr-5296] fixes drag-region refresh after content updates. Neither implements these contracts. `ComponentContext::run_window` survives the removal of the separate imperative app API in #5266.

**Existing workaround:** `ComponentContext::run_window` passes a `WindowHandle` to a closure, and `WindowHandle::as_raw` returns the HWND ([run_window][component-run-window]). With it, an app can install its own window subclass (for `WM_ACTIVATE` and `WM_HOTKEY`) and reach `AppWindow` through `GetWindowIdFromWindow` to set the caption colors. Limits of this workaround:

- The handle is only borrowed for the duration of the closure, but a subclass outlives it. Nothing in Reactor tells the app when the window is about to be destroyed so it can remove the subclass safely.
- The published `windows` crate does not project `Microsoft.UI.*`, so the app needs its own WinAppSDK bindings for `AppWindow`/`AppWindowTitleBar`.

The gaps below are therefore Reactor contract gaps (safety, lifetime, ergonomics), not missing platform capability.

### Activation-scoped shortcuts

Spectro registers Win32 hotkeys with `MOD_NOREPEAT`, unregisters them on `Window.Activated` deactivation and `Window.Closed`, and retries on activation. Its HWND subclass dispatches `WM_HOTKEY`, including while WebView2 owns focus.

**Missing Reactor contract:** window publications expose color scheme, size, and placement observers, but no activation observer. The filtered WinUI `IWindow.Activated`/`RemoveActivated` slots are unprojected. An XAML accelerator declaration does not implement this HWND lifecycle.

**Required change:** expose activation/deactivation and teardown callbacks, or implement an app-owned scoped HWND subclass that handles the same Win32 lifecycle and removes itself before its state is destroyed. This item is a **native integration contract**, not a missing Windows hotkey API.

### Caption colors

Spectro sets:

- `AppWindow.TitleBar.ButtonForegroundColor`: white/black by theme, or null in high contrast.
- `ButtonBackgroundColor`: transparent.
- `ButtonInactiveBackgroundColor`: transparent.

**Missing Reactor contract:** `WindowVisuals` has no caption-color properties, including nullable foreground reset.

**Required change:** add those exact AppWindowTitleBar setters to window appearance, or apply them through a compatible app-owned AppWindow binding. Clearing the foreground override in high contrast is required.

**Evidence:** [Spectro window lifecycle and caption setters][app-window]; [window publication fields][window-publication]; [IWindow event slots][native-window]; [WindowVisuals fields/builders][window-visuals].

**Blocked parity:** active-window-only global shortcut ownership and themed/high-contrast caption buttons.

## 12. NumberBox: SmallChange and SpinButtonPlacementMode landed

| Control | `SmallChange` | `SpinButtonPlacementMode` |
|---|---|---|
| TextSizeBox | `1` | `Compact` |
| ReadingWidthBox | `20` | `Compact` |
| RetentionBox | `7` | `Compact` |

**Status: landed** in [#5104][pr-5104]. `small_change(f64)` and `spin_button_placement_mode(NumberBoxSpinButtonPlacementMode)` expose Hidden, Inline, and Compact, with native application/removal and ownership coverage.

**Evidence:** [settings NumberBoxes][app-numberboxes]; [current NumberBox schema][schema-number]; [generated builders][current-builders]; [merged change][pr-5104].

**Spectro adoption:** use Compact with the source increments `1`, `20`, and `7`. Preserve native validation, ValueChanged feedback, invalid/NaN input behavior, and the app's setting-population feedback suppression. This no longer needs an upstream feature addition.

## 13. UI Automation: LiveSetting landed; AccessibilityView remains missing

| Attached property | Required assignment | Current status |
|---|---|---|
| `AutomationProperties.LiveSetting` | `SyncStatusText=Polite` | Landed in #5170 as `automation_live_setting(AutomationLiveSetting::Polite)` |
| `AutomationProperties.AccessibilityView` | Story thumbnail `Image=Raw` | No attached schema property/builder or generated dependency-property mapping. |

**Status: partial.** [#5170][pr-5170], merged after the umbrella issue closed, resolves focused issue #5135. It adds live settings, HelpText/FullDescription, automatic LiveRegionChanged events for changed text, and ElementRef notification requests. It does **not** add AccessibilityView ([current attached-property schema][schema-automation]).

**Remaining change:** expose `AutomationProperties.AccessibilityView` and its enum so decorative story thumbnails can remain Raw ([assignment][app-story-list]).

**Spectro adoption:** apply Polite to [SyncStatusText][app-status] and verify announcements under real sync updates. New/off regions and layout-only changes stay quiet upstream. Reactor's automatic text-change announcements should be checked against the C# oracle; any intended announcement difference needs reconciliation. The new description/notification APIs are available but are not additional Spectro feature requirements. Thumbnail exclusion from Control/Content automation views remains blocked.

## 14. ContentDialog: default button and owner-theme behavior landed

Spectro's sign-out/reset confirmation sets:

`DefaultButton=ContentDialogButton.Close`, `CloseButtonText="Cancel"`, and `RequestedTheme=RequestedTheme`.

**Status: landed through two changes.** [#5103][pr-5103] adds `default_button(ContentDialogButton::Close)` and reset/ownership handling. [#5160][pr-5160] applies the owner's actual theme at attachment and observes ActualThemeChanged, including updates while the dialog remains open. It also fixes shutdown with a dialog still open.

**Evidence:** [confirmation construction][app-dialog]; [current dialog schema/builders][schema-dialog]; [theme propagation and ownership][dialog-theme]; [upstream native/UI coverage][pr-5160].

**Spectro adoption:** set Close/Cancel as the default and keep the dialog owner on the same requested/effective theme as the Spectro page. Use Reactor's live owner-theme contract; a separate public RequestedTheme builder is no longer required for this scenario. Verify Light/Dark/System/high contrast, changing theme while open, reopening, owner replacement, Enter/Cancel behavior, and closing the window with a dialog open. A future independently themed dialog would be a different requirement.

## 15. Context menus: no ContextFlyout on non-button elements

**Status: open.** The audited MenuExt/native attachment path still does not provide an arbitrary-element ContextFlyout contract.

The old `AppContext::show_menu_at` escape hatch is removed by [#5266][pr-5266]. It cannot serve as the current port's row-menu workaround. ComponentContext::run_window still permits a typed native integration, but that route must implement the full pointer/touch/keyboard and lifetime contract below.

Each story row is a `Grid` with a `Grid.ContextFlyout` `MenuFlyout` holding two `MenuFlyoutItem`s. Their `Text` is bound to `ReadAction`/`SaveAction` (it changes with the item's state), `Tag` is bound to the item, and `Click` invokes `ToggleRead_Click`/`ToggleSaved_Click` ([story context menu][app-context-flyout]). WinUI opens the menu on right-click, a touch press-and-hold, Shift+F10, and the Menu key.

**Missing:**

- Reactor's only menu attachment is `MenuExt::menu` ([menu attachment][menu-ext]). The native adapter applies it as `Button.Flyout` for `Button`/`DropDownButton`, and as items for `MenuBarItem`. Any other target returns `WinUiError::InvalidObject` ([set_menu][hosting-set-menu]).
- `UIElement.ContextFlyout`/`SetContextFlyout`, `ContextRequested`, and `RightTapped` are unprojected slots ([native UIElement slots][native-context-flyout]).

**Required change:** project `UIElement.SetContextFlyout`. Add a `context_menu(Menu)` attachment valid on any UIElement, including DataItem content, and have it re-render item text when state changes. Clicks must be dispatched with the row's stable key.

**Blocked parity:** right-click and keyboard context menus for Mark read/unread and Save/unsave on story rows. A `Button.Flyout` is not equivalent: it adds a visible button and does not respond to right-click or Shift+F10 on the row.

## 16. Reactor setup: architecture caches and staging failures fixed

**Status: landed** in [#5109][pr-5109], confirmed by the [updated runtime-deployment comment][deployment-update]. The original wrong-architecture reuse and ignored-error defects are fixed in the [current setup implementation][setup]:

- Extracted MSIX caches use `.msix_extract-{arch}`. A versioned cache namespace avoids trusting unchecked legacy entries.
- Package/runtime extraction populates a temporary directory, checks required files, and renames it into the completed location only on success. Failed population can be retried; per-entry locks serialize cache publication.
- Download, extraction, directory/read, and copy errors propagate to the build script. `as_self_contained` fails with staging diagnostics.
- A separate destination lock spans runtime **and** WebView2 DLL copying into the shared Cargo profile directory. Cache locks alone did not prevent concurrent output-copy sharing violations.

Upstream regressions cover architecture separation, failed extraction and retry, required-file checks, failed population, concurrent publication, copy failures, and eight processes deploying through the public API to a shared output directory. The PR's live WinUI test foundation is x64-only; architecture build/filesystem coverage does not establish real ARM64 application startup.

**Spectro adoption:** consume the fixed setup revision and preserve these tests when patching its runtime pin. Setup's publication checks use selected required files, not an exhaustive Spectro MSIX payload audit or PE-machine inspection. Verify all required packaged files and actual DLL architecture, interrupted staging/retry, both architecture build orders, shared-output concurrency, installed activation, and target-device execution. The upstream fix removes feature-development work from U6; it does not complete production packaging acceptance.

## Gap-filling order and port dependencies

| Work package | Current disposition | Spectro work it unblocks |
|---|---|---|
| U1: native scalar properties | Adopt landed gaps 1, 2, 12, 14 and LiveSetting from 13; implement only the remaining AccessibilityView contract | Native source property assignments, dialogs, and automation |
| U2: native styles and resources | Still open: gaps 4/5; use existing overrides where proven equivalent | Original palettes, contrast behavior, and toolbar states |
| U3: list containers/events/menus | Still open: gap 6, gap 7 ItemClick, and gap 15 | Native row geometry, selected-item activation, and row menus |
| U4: retained state and focus | Still open: gaps 8/9 | Responsive navigation, settings exclusion, reader lifetime, keyboard focus |
| U5: keyboard/window integration | Still open: gaps 10/11 | Original shortcuts with browser focus and exact window chrome |
| U6: runtime deployment | Upstream fixes landed; adopt, validate runtime alignment, inspect package payloads, and retain failure/concurrency regressions | Correct x64/ARM64 package output |
| U7: panel properties | Optional gap 3; proven Border wrapper remains permitted | Original padding/separators without mandatory upstream expansion |
| V1: ListView continuity | Adopt #5222's move/select fix; rerun the historical reversal and Spectro scenarios; retained ReplaceAll still requires proof | Scroll/focus/selection and logical content continuity during real sync changes |

For each property addition, update Reactor's schema, generated declarations, filtered native projection, and native application/reset paths together. Following #5180, prefer generated dependency-property accessors plus SetValue/ClearValue where supported; do not restore hundreds of direct setter bindings just to follow the original audit. Brush/resource and imperative contracts still need their specialized adapters. Native metadata coverage alone does not create a public declaration; a public builder alone does not apply the property to WinUI.

The remaining layout/control binding gaps concern **Reactor's filtered WinUI projection**, not absent Windows functionality. Remaining additions need generated methods/types and public Reactor contracts together. The native window items additionally require AppWindow/Win32 integration rather than a substitute UI.

The [Rust port plan](rust-port-plan.md#3-upstream-work-packages-and-blocking-gates) carries the same dispositions into P1/P4/P5/P6. Its completion gates require both paired screenshot matches and normalized **live native XAML tree/property** comparison, plus separate UIA/interaction evidence. Raw tree identity is not mandatory; unexplained semantic/property differences cannot pass through a whole-tree similarity score. Framework fixes do not change the five-crate architecture, data/protocol invariants, approved C# baseline requirement, or these final parity/cutover gates. Use focused upstream issues with small examples for remaining features, as requested when #5087 closed.

[app-xaml]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml#L83-L359
[app-styles]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml#L44-L68
[app-themes]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml#L10-L43
[app-view-items]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/ViewItems.cs#L84-L88
[app-story-list]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml#L236-L274
[app-item-click]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L230-L239
[app-screen]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L439-L450
[app-responsive]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L521-L557
[app-reader-host]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml#L112-L116
[app-settings-focus]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L816-L832
[app-close-settings]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L125-L131
[app-back]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L722-L732
[app-accelerators]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml#L73-L79
[app-window]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainWindow.xaml.cs#L18-L180
[app-numberboxes]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml#L350-L355
[app-status]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml#L284-L286
[app-dialog]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L172-L184
[schema-text]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor/src/schema.toml
[schema-icon]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor/src/schema.toml
[schema-panels]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor/src/schema.toml
[schema-button]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor/src/schema.toml
[schema-list]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor/src/schema.toml
[schema-visual]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor/src/schema.toml
[schema-number]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor/src/schema.toml
[schema-automation]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor/src/schema.toml
[schema-dialog]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor/src/schema.toml
[native-grid]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/bindings.rs
[native-stack]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/bindings.rs
[native-control]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/bindings.rs
[brush-types]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/declaration.rs
[resource-adapter]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/winui/style.rs
[style-adapter]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/winui/style.rs
[window-types]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/window.rs
[list-builder]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/generated_declarations.rs
[container-builder]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/generated_declarations.rs
[list-template]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/winui/relations.rs
[native-item-click]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/bindings.rs
[native-visibility]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/bindings.rs
[reference-api]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/reference.rs
[accelerator-types]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/declaration.rs
[accelerator-adapter]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/winui/objects.rs
[window-publication]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/component/window.rs
[native-window]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/bindings.rs
[window-visuals]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/window.rs
[setup]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor-setup/src/lib.rs
[app-main-window-xaml]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainWindow.xaml#L15
[app-context-flyout]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml#L267-L272
[app-incremental]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L634-L643
[schema-border]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor/src/schema.toml
[relations-replace-all]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/winui/relations.rs
[component-run-window]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/component/context.rs
[menu-ext]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/declaration.rs
[hosting-set-menu]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/winui/attachments.rs
[native-context-flyout]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/bindings.rs

[issue]: https://github.com/microsoft/windows-rs/issues/5087
[issue-update]: https://github.com/microsoft/windows-rs/issues/5087#issuecomment-6278409857
[deployment-update]: https://github.com/microsoft/windows-rs/issues/5087#issuecomment-6170600368
[reorder-repro]: https://github.com/microsoft/windows-rs/issues/5087#issuecomment-6205199520
[list-crash]: https://github.com/microsoft/windows-rs/issues/5146
[pr-5103]: https://github.com/microsoft/windows-rs/pull/5103
[pr-5104]: https://github.com/microsoft/windows-rs/pull/5104
[pr-5105]: https://github.com/microsoft/windows-rs/pull/5105
[pr-5106]: https://github.com/microsoft/windows-rs/pull/5106
[pr-5109]: https://github.com/microsoft/windows-rs/pull/5109
[pr-5114]: https://github.com/microsoft/windows-rs/pull/5114
[pr-5127]: https://github.com/microsoft/windows-rs/pull/5127
[pr-5128]: https://github.com/microsoft/windows-rs/pull/5128
[pr-5141]: https://github.com/microsoft/windows-rs/pull/5141
[pr-5158]: https://github.com/microsoft/windows-rs/pull/5158
[pr-5160]: https://github.com/microsoft/windows-rs/pull/5160
[pr-5170]: https://github.com/microsoft/windows-rs/pull/5170
[current-builders]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/generated_declarations.rs
[dialog-theme]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/winui/content_dialog.rs
[pr-5180]: https://github.com/microsoft/windows-rs/pull/5180
[pr-5187]: https://github.com/microsoft/windows-rs/pull/5187
[pr-5194]: https://github.com/microsoft/windows-rs/pull/5194
[pr-5222]: https://github.com/microsoft/windows-rs/pull/5222
[pr-5228]: https://github.com/microsoft/windows-rs/pull/5228
[pr-5266]: https://github.com/microsoft/windows-rs/pull/5266
[pr-5274]: https://github.com/microsoft/windows-rs/pull/5274
[pr-5294]: https://github.com/microsoft/windows-rs/pull/5294
[pr-5296]: https://github.com/microsoft/windows-rs/pull/5296
[pr-5316]: https://github.com/microsoft/windows-rs/pull/5316
[release-reactor]: https://crates.io/api/v1/crates/windows-reactor
[release-setup]: https://crates.io/api/v1/crates/windows-reactor-setup
[release-webview]: https://crates.io/api/v1/crates/windows-webview
[dependency-adapter]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/libs/reactor/src/native/winui/dependency.rs
[metadata-tool]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tools/reactor-metadata/src/main.rs
[list-tests]: https://github.com/microsoft/windows-rs/blob/db9801679bac964b6367b1d1d1bf8472ab001065/crates/tests/libs/reactor/tests/ui/list.rs
[list-crash-diagnosis]: https://github.com/microsoft/windows-rs/issues/5146#issuecomment-6442505010
