# Spectro: specific Reactor gaps blocking WinUI parity

## 1. TextBlock: four missing typography properties

| Required WinUI property | Spectro usage | Missing Reactor contract |
|---|---|---|
| `TextBlock.FontFamily` | `"Georgia"` on loading, login, empty-state, and settings headings; for example, the login heading uses `FontSize="42"` | No `font_family` builder or schema property; no callable `ITextBlock.SetFontFamily` projection. |
| `TextBlock.CharacterSpacing` | Branding `-30`; labels `100`, `130`, `150`, `160` | No `character_spacing` builder or schema property; no callable `ITextBlock.SetCharacterSpacing` projection. |
| `TextBlock.LineHeight` | Story summaries `16`; reader empty-state copy `23` | No `line_height` builder or schema property; no callable `ITextBlock.SetLineHeight` projection. |
| `TextBlock.TextAlignment` | `Center` on wrapped empty-state headings and messages | No `text_alignment` builder or schema property; no callable `ITextBlock.SetTextAlignment` projection. |

**Evidence:** [Spectro TextBlocks][app-xaml]; [Reactor TextBlock schema][schema-text]; [Reactor `ITextBlock_Vtbl`][native-text]. The native projection contains `SetFontFamily: usize`, `SetCharacterSpacing: usize`, `SetTextAlignment: usize`, and `SetLineHeight: usize`: these preserve COM vtable positions but are **not callable Rust bindings**.

**Required change:** add those four schema properties, generate their callable WinUI setters and required types, and wire native property application/removal. `FontFamily` needs a native `FontFamily` value constructed from the family string; `TextAlignment` needs the native enum.

**Blocked parity:** actual font selection, glyph advances, line placement, wrapping, and alignment within a text block. Setting the element's `HorizontalAlignment` does not set the alignment of its text.

## 2. FontIcon: missing FontSize and Foreground

Spectro sets `FontIcon.FontSize` to values including `11`, `16`, `17`, `23`, `28`, `36`, and `40`, and sets `Foreground` to `BrandBrush` or `MutedInkBrush`.

**Missing:** Reactor's `FontIcon` schema declares only `Glyph`. Its filtered `IFontIcon` projection exposes only `SetGlyph`; the `IIconElement` projection has no foreground setter. There is no public `font_size` or `foreground` contract on this declaration.

**Evidence:** [Spectro FontIcon declarations][app-xaml]; [FontIcon schema][schema-icon]; [IFontIcon projection][native-font-icon]; [IIconElement projection][native-icon-element].

**Required change:** project and expose `FontIcon.FontSize` and inherited `IconElement.Foreground`, including theme-resource brush assignment. Use those native properties, not a TextBlock containing an icon character.

**Blocked parity:** the loading/sign-in icons, navigation glyphs, toolbar glyphs, saved/unread indicators, and sync status icon have explicit sizes and colors that cannot currently be assigned.

## 3. Grid and StackPanel: missing native padding and border properties (ergonomic; workaround exists)

| Required property | Spectro usage | Missing Reactor contract |
|---|---|---|
| `Grid.Padding` | Navigation row `12,8`; feed row `8,4`; article toolbar `12,8`; sync footer `22,12`; navigation footer `16,12`; filter footer `8`; story row bound to `PreviewPadding` | Absent from Grid schema; `IGrid.SetPadding` is an unprojected slot. |
| `Grid.BorderBrush` | `RuleBrush` on pane separators, story rows, and toolbar/footer rules | Absent from Grid schema; `IGrid.SetBorderBrush` is an unprojected slot. |
| `Grid.BorderThickness` | StoryPane `1,0`; story row and article toolbar `0,0,0,1`; feed/navigation footer `0,1,0,0` | Absent from Grid schema; `IGrid.SetBorderThickness` is an unprojected slot. |
| `StackPanel.Padding` | Login panel `24,64` | Absent from StackPanel schema; `IStackPanel.SetPadding` is an unprojected slot. |

**Evidence:** [Spectro layout properties][app-xaml]; [Grid/StackPanel schema][schema-panels]; [IGrid projection][native-grid]; [IStackPanel projection][native-stack].

**Required change:** add these properties to their actual native panel declarations and regenerate the setters. Story row padding must update between normal `12,9,12,9` and dense `8,6,8,6` values ([ViewItems][app-view-items]).

**Workaround available today:** Reactor's `Border` declares `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, and `Padding` ([Border schema][schema-border]). Wrapping each panel in a `Border` that carries these properties should produce the same insets, separators, and available width, at the cost of one extra native element per row. Parity of the wrapped layout still needs a screenshot comparison. Replacing panel padding with child margins is **not** equivalent.

**Classification:** convenience gap, not a parity blocker. Adding the native panel properties removes the extra element from every story row.

## 4. Button: QuietButton setters cannot be represented

Spectro's `QuietButton` style requires:

| Setter | Required value | Missing surface |
|---|---|---|
| `Control.Padding` | `10` | No Button schema property; `IControl.SetPadding` is an unprojected slot. |
| `Control.Foreground` | `{ThemeResource InkBrush}` | No Button schema property; `IControl.SetForeground` is an unprojected slot. |
| `Control.BorderThickness` | `0` | No Button schema property, even though a callable native setter is generated for other controls. |

**Evidence:** [QuietButton style][app-styles]; [Button schema][schema-button]; [IControl projection][native-control]; [ButtonStyle enum and resource values][brush-types]; [native style adapter][style-adapter].

`ButtonStyle` accepts only `Default`, `Accent`, `Subtle`, and `TextLink`. It cannot accept the app's `QuietButton` style or an arbitrary typed setter collection. The native style adapter only composes predefined button styles and its restricted theme-brush assignments.

**Required change:** expose the three properties and provide a typed native Button style/setter path capable of representing this style. Preserve style-setter precedence; replacing setters with higher-precedence local values must not change disabled, hover, pressed, or focus rendering.

**Blocked parity:** toolbar/button padding, foreground inheritance into child icons, and border rendering. Choosing `Subtle` because it looks similar is not an equivalent implementation.

## 5. Theme resources: no app-defined ThemeResource brush references or theme dictionaries

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

### ItemClick

| Required native contract | Spectro behavior | Missing surface |
|---|---|---|
| `ListViewBase.IsItemClickEnabled=True` and `ItemClick` | Clicking an already-selected story reopens the article in compact layout | No property/event in Reactor ListView; native `SetIsItemClickEnabled`, `ItemClick`, and `RemoveItemClick` remain unprojected slots. |

`ItemClick` fires for pointer clicks **and** for Enter/Space on a focused item. A `Tapped` handler on the row content handles pointer input only, so it is not equivalent. `SelectionChanged` does not fire when the user clicks the item that is already selected.

**Evidence:** [StoryList XAML][app-story-list]; [selected-story click handler][app-item-click]; [ListView schema][schema-list]; [native ListViewBase slots][native-item-click].

**Required change:** add native item-click registration and dispatch by stable DataItem key. Preserve the distinction between selection changes and clicking an item whose selection is unchanged.

### Collection updates and scroll anchoring

Spectro sets `ItemsStackPanel.ItemsUpdatingScrollMode="KeepItemsInView"`, which is the WinUI default. Reactor needs no new property for it as long as ListView keeps the default `ItemsStackPanel`. (`ItemsPanel`, `ItemsStackPanel`, and `ItemContainerStyle` don't appear in the filtered projection at all.)

Spectro updates its collection incrementally with `RemoveAt`/`Insert` ([incremental sync][app-incremental]). When a reorder moves all but at most one child, Reactor's container relation calls `IVector.ReplaceAll` instead ([container reorder][relations-replace-all]). Smaller reorders use `RemoveAt`/`InsertAt`. Reactor explicitly restores the selected item afterward; it does not explicitly restore the visible anchor, scroll offset, or focused container. The different mutation strategy is a parity risk, not proof by itself that WinUI loses those states.

**Required verification/change:** compare large sync reorders against C# with a scrolled, focused, selected ListView. If `ReplaceAll` changes the visible anchor, offset, or focus, preserve those states explicitly or use keyed mutations for that path. The default `KeepItemsInView` mode does not require a new property.

**Blocked parity:** compact-mode reopening and keyboard activation of the selected story require ItemClick. Scroll stability during large sync reorders is a verification gate rather than a confirmed failure.

## 8. Visibility: no retained Visible/Collapsed property

Spectro changes `UIElement.Visibility` on `LoadingPanel`, `LoginPanel`, `ReaderPanel`, `SettingsPanel`, `NavigationPane`, `FeedPane`, `StoryPane`, `ArticlePane`, `ArticleReader`, search controls, empty states, progress, and Back/Cancel controls.

**Missing:** `Visibility` is absent from shared visual properties and public builders. `IUIElement.SetVisibility` is an unprojected slot.

**Evidence:** [shared visual schema][schema-visual]; [native UIElement slots][native-visibility]; [screen visibility changes][app-screen]; [responsive visibility changes][app-responsive].

**Required change:** expose the native `Visibility` enum and retained property updates. Collapsing must preserve the existing native instance while excluding it from layout, hit testing, focus navigation, and the appropriate automation view.

**Blocked parity:** pane switching and overlays without destroying browser state, text-input state, native selection, or scroll position. Removing/recreating a subtree is not `Collapsed`; opacity zero is not `Collapsed`.

## 9. Settings: missing disabled ContentControl subtree and focus capture/restore

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

## 12. NumberBox: missing SmallChange and SpinButtonPlacementMode

| Control | `SmallChange` | `SpinButtonPlacementMode` |
|---|---|---|
| TextSizeBox | `1` | `Compact` |
| ReadingWidthBox | `20` | `Compact` |
| RetentionBox | `7` | `Compact` |

**Missing:** both properties are absent from the NumberBox schema. `INumberBox.SetSmallChange` and `SetSpinButtonPlacementMode` are unprojected vtable slots.

**Evidence:** [settings NumberBoxes][app-numberboxes]; [NumberBox schema][schema-number]; [INumberBox projection][native-number].

**Required change:** generate and expose the double-valued SmallChange setter and native spin-placement enum/setter. Preserve native input validation and ValueChanged feedback.

**Blocked parity:** spinner appearance and numeric increments. Minimum/maximum/value alone do not specify these behaviors.

## 13. UI Automation: missing LiveSetting and AccessibilityView

| Attached property | Required assignment | Missing surface |
|---|---|---|
| `AutomationProperties.LiveSetting` | `SyncStatusText=Polite` | No attached schema property; native `SetLiveSetting` is an unprojected slot. |
| `AutomationProperties.AccessibilityView` | Story thumbnail `Image=Raw` | No attached schema property; native `SetAccessibilityView` is an unprojected slot. |

**Evidence:** [thumbnail automation assignment][app-story-list]; [sync status assignment][app-status]; [attached-property schema][schema-automation]; [native automation slots][native-automation].

**Required change:** project and expose both attached setters and their enums.

**Blocked parity:** the sync status live-region configuration and exclusion of decorative thumbnails from the Control/Content automation views.

## 14. ContentDialog: missing DefaultButton and RequestedTheme

Spectro's sign-out/reset confirmation sets:

`DefaultButton=ContentDialogButton.Close`, `CloseButtonText="Cancel"`, and `RequestedTheme=RequestedTheme`.

**Missing:** ContentDialog has no DefaultButton property in the schema/builders; the native `SetDefaultButton` slot is unprojected. RequestedTheme is absent from both the dialog declaration and shared visual properties.

**Evidence:** [confirmation construction][app-dialog]; [ContentDialog schema][schema-dialog]; [ContentDialog builders][dialog-builder]; [native DefaultButton slot][native-dialog]; [shared visual schema][schema-visual].

**Required change:** add DefaultButton and explicit dialog RequestedTheme. Preserve Cancel as the default and use the page's requested theme rather than relying on popup inheritance.

**Blocked parity:** default-button emphasis/Enter behavior and explicit theme selection on destructive-action confirmations.

## 15. Context menus: no ContextFlyout on non-button elements

Each story row is a `Grid` with a `Grid.ContextFlyout` `MenuFlyout` holding two `MenuFlyoutItem`s. Their `Text` is bound to `ReadAction`/`SaveAction` (it changes with the item's state), `Tag` is bound to the item, and `Click` invokes `ToggleRead_Click`/`ToggleSaved_Click` ([story context menu][app-context-flyout]). WinUI opens the menu on right-click, a touch press-and-hold, Shift+F10, and the Menu key.

**Missing:**

- Reactor's only menu attachment is `MenuExt::menu` ([menu attachment][menu-ext]). The native adapter applies it as `Button.Flyout` for `Button`/`DropDownButton`, and as items for `MenuBarItem`. Any other target returns `WinUiError::InvalidObject` ([set_menu][hosting-set-menu]).
- `UIElement.ContextFlyout`/`SetContextFlyout`, `ContextRequested`, and `RightTapped` are unprojected slots ([native UIElement slots][native-context-flyout]).

**Required change:** project `UIElement.SetContextFlyout`. Add a `context_menu(Menu)` attachment valid on any UIElement, including DataItem content, and have it re-render item text when state changes. Clicks must be dispatched with the row's stable key.

**Blocked parity:** right-click and keyboard context menus for Mark read/unread and Save/unsave on story rows. A `Button.Flyout` is not equivalent: it adds a visible button and does not respond to right-click or Shift+F10 on the row.

## 16. Reactor setup: wrong-architecture cache reuse and silent deployment failures

### Extracted runtime cache is not architecture-specific

`stage_pkg` creates a cache path keyed by package name/version. `ensure_msix_extracted` selects `MSIX\win10-<target architecture>\Microsoft.WindowsAppRuntime.2.msix`, but extracts it into the same package-level `.msix_extract` directory for every architecture and skips extraction if that directory exists.

**Consequence:** an x64 build followed by an ARM64 build using that cache can copy the x64 runtime into the ARM64 output.

**Required change:** key the extracted runtime cache by target architecture, validate binary architecture, and use an atomic completed-cache marker rather than directory existence.

### Extraction/copy failures can be treated as completed staging

`extract_tar` ignores process exit status; `copy_file` ignores `fs::copy` errors and logs missing files without failing; directory creation can precede unsuccessful extraction.

**Consequence:** packaging can continue with missing runtime files or an incomplete cache that future builds reuse.

**Required change:** propagate download/extract/copy failures, verify required-file completeness, and publish a cache entry only after validation.

**Evidence:** [`ensure_msix_extracted`, copy, cache, download, and extraction functions][setup].

## Gap-filling order and port dependencies

| Work package | Exact changes | Spectro work it unblocks |
|---|---|---|
| Native property coverage | TextBlock's four properties; FontIcon's two; NumberBox's two; dialog properties; UIA attached properties; Grid/StackPanel properties (optional if Border wrapper equivalence is proven) | Direct translation of the XAML's native property assignments |
| Native styles and resources | Named theme brushes/dictionaries; QuietButton setters; generated ListView container styles | Original palettes, contrast behavior, toolbar states, and native row geometry |
| Retained state and focus | Visibility; enabled ContentControl; ListView references; focused-control capture/restore | Responsive navigation, settings exclusion, reader lifetime, keyboard focus |
| Native list events/updates | ItemClick/IsItemClickEnabled; verify large-reorder state continuity and fix any reproduced mismatch | Reopening selected stories; proving scroll/focus parity during sync reorders |
| Context menus | `UIElement.ContextFlyout` projection; `context_menu` attachment on any element | Story-row Mark read / Save menus |
| Keyboard/window integration | Missing keys and modifier combinations; activation/teardown with a safe subclass lifetime; caption setters | Original shortcuts with browser focus and exact window chrome |
| Runtime deployment | Architecture-qualified caches; explicit staging failure; complete-file validation | Correct x64/ARM64 package output |

For each property addition, update Reactor's schema, generated declarations, filtered native projection, and adapter together. Native binding coverage alone does not create a public declaration; a public builder alone does not apply the property to WinUI.

The missing font/layout/control bindings above are **Reactor's filtered WinUI projection**, not absent Windows font or layout functionality. The required windows-rs work is to generate those methods/types and expose them through Reactor. The native window items additionally require AppWindow/Win32 integration rather than a substitute UI.

The [Rust port plan](rust-port-plan.md) maps these gaps to upstream work packages, application phases, and parity acceptance gates.

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
[schema-text]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/tools/reactor/src/schema.toml#L133-L171
[schema-icon]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/tools/reactor/src/schema.toml#L1290-L1294
[schema-panels]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/tools/reactor/src/schema.toml#L407-L451
[schema-button]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/tools/reactor/src/schema.toml#L213-L243
[schema-list]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/tools/reactor/src/schema.toml#L611-L662
[schema-visual]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/tools/reactor/src/schema.toml#L88-L130
[schema-number]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/tools/reactor/src/schema.toml#L794-L824
[schema-automation]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/tools/reactor/src/schema.toml#L69-L85
[schema-dialog]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/tools/reactor/src/schema.toml#L1755-L1783
[native-text]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L19566-L19610
[native-font-icon]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L10404-L10412
[native-icon-element]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L11385-L11387
[native-grid]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L11095-L11123
[native-stack]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L18200-L18225
[native-control]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L8630-L8688
[brush-types]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/declaration.rs#L204-L317
[resource-adapter]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/winui/hosting.rs#L1-L54
[style-adapter]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/winui/hosting.rs#L112-L196
[window-types]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/window.rs#L14-L27
[list-builder]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/generated_declarations.rs#L2974-L3062
[container-builder]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/generated_declarations.rs#L6462-L6490
[list-template]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/winui/relations.rs#L603-L614
[native-item-click]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L12701-L12719
[native-visibility]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L21949-L21953
[reference-api]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/reference.rs#L129-L340
[accelerator-types]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/declaration.rs#L415-L446
[accelerator-adapter]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/winui/hosting.rs#L55-L115
[window-publication]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/component.rs#L574-L581
[native-window]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L22848-L22855
[window-visuals]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/window.rs#L101-L165
[native-number]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L14135-L14171
[native-automation]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L6126-L6131
[dialog-builder]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/generated_declarations.rs#L6337-L6409
[native-dialog]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L8422-L8425
[setup]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor-setup/src/lib.rs#L110-L225
[app-main-window-xaml]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainWindow.xaml#L15
[app-context-flyout]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml#L267-L272
[app-incremental]: https://github.com/clarkezone/spectro/blob/44c28d223e1d2170cfdf423fa3ff83467e9a3f53/src/Spectro.App/MainPage.xaml.cs#L634-L643
[schema-border]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/tools/reactor/src/schema.toml#L276-L300
[relations-replace-all]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/winui/relations.rs#L328-L368
[component-run-window]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/component.rs#L358-L433
[menu-ext]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/declaration.rs#L2166-L2170
[hosting-set-menu]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/winui/hosting.rs#L537-L620
[native-context-flyout]: https://github.com/microsoft/windows-rs/blob/9897c4fe55d00a38cb4445dafbd7aa28e3f5bd22/crates/libs/reactor/src/native/bindings.rs#L21980-L22211
