using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Spectro.WinUI;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int SettingsHotKeyId = 0x5301;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint ModShift = 0x0004;
    private const uint WmHotKey = 0x0312;
    private static readonly NativeMethods.SubclassProc SubclassProc = WindowSubclassProc;
    private static MainWindow? _current;
    private readonly nint _windowHandle;
    private bool _hotKeyRegistered;
    private readonly HashSet<int> _registeredHotKeys = [];
    private static readonly (int Id, uint Modifiers, VirtualKey Key)[] Shortcuts =
    [
        (SettingsHotKeyId, ModControl, VirtualKey.D),
        (SettingsHotKeyId + 1, ModControl, VirtualKey.R),
        (SettingsHotKeyId + 2, ModControl, VirtualKey.O),
        (SettingsHotKeyId + 3, ModControl | ModShift, VirtualKey.S),
        (SettingsHotKeyId + 4, ModControl | ModShift, VirtualKey.M)
    ];

    public MainWindow()
    {
        InitializeComponent();
        _current = this;
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (!NativeMethods.SetWindowSubclass(
                _windowHandle,
                SubclassProc,
                (nuint)SettingsHotKeyId,
                0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        WindowRoot.ActualThemeChanged += (_, _) => UpdateCaptionTheme();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1440, 960));

        AppWindow.SetIcon("Assets\\AppIcon.ico");

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));

        var settingsAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.D,
            Modifiers = VirtualKeyModifiers.Control
        };
        settingsAccelerator.Invoked += (_, args) =>
        {
            if (RootFrame.Content is MainPage page)
            {
                page.OpenSettingsFromAccelerator();
                args.Handled = true;
            }
        };
        RootFrame.KeyboardAccelerators.Add(settingsAccelerator);

        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
        RegisterSettingsHotKey();
    }

    internal void SetTheme(ElementTheme theme)
    {
        WindowRoot.RequestedTheme = theme;
        UpdateCaptionTheme();
    }

    private void UpdateCaptionTheme()
    {
        var dark = WindowRoot.ActualTheme == ElementTheme.Dark;
        var highContrast = new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
        AppWindow.TitleBar.ButtonForegroundColor = highContrast ? null : dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            UnregisterSettingsHotKey();
            return;
        }

        RegisterSettingsHotKey();
    }

    private void RegisterSettingsHotKey()
    {
        if (_hotKeyRegistered)
        {
            return;
        }

        foreach (var shortcut in Shortcuts)
        {
            if (NativeMethods.RegisterHotKey(_windowHandle, shortcut.Id,
                    shortcut.Modifiers | ModNoRepeat, (uint)shortcut.Key))
                _registeredHotKeys.Add(shortcut.Id);
            else
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Trace.TraceWarning($"Spectro shortcut {shortcut.Key} could not be registered (Win32 {error}).");
                if (RootFrame.Content is MainPage page) page.ReportShortcutConflict(shortcut.Key);
            }
        }

        _hotKeyRegistered = true;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        UnregisterSettingsHotKey();
        NativeMethods.RemoveWindowSubclass(
            _windowHandle,
            SubclassProc,
            (nuint)SettingsHotKeyId);
        if (ReferenceEquals(_current, this))
        {
            _current = null;
        }
    }

    private void UnregisterSettingsHotKey()
    {
        if (_hotKeyRegistered)
        {
            foreach (var id in _registeredHotKeys)
                NativeMethods.UnregisterHotKey(_windowHandle, id);
            _registeredHotKeys.Clear();
            _hotKeyRegistered = false;
        }
    }

    private static nint WindowSubclassProc(
        nint windowHandle,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        if (message == WmHotKey && Shortcuts.FirstOrDefault(shortcut => (nuint)shortcut.Id == wParam) is { Id: > 0 } matched)
        {
            _current?.DispatcherQueue.TryEnqueue(async () =>
            {
                if (_current?.RootFrame.Content is MainPage page)
                {
                    await page.HandleReaderShortcutAsync(matched.Key);
                }
            });
            return 0;
        }

        return NativeMethods.DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private static class NativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate nint SubclassProc(
            nint windowHandle,
            uint message,
            nuint wParam,
            nint lParam,
            nuint subclassId,
            nuint referenceData);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(
            nint windowHandle,
            int id,
            uint modifiers,
            uint virtualKey);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(nint windowHandle, int id);

        [DllImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowSubclass(
            nint windowHandle,
            SubclassProc callback,
            nuint subclassId,
            nuint referenceData);

        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RemoveWindowSubclass(
            nint windowHandle,
            SubclassProc callback,
            nuint subclassId);

        [DllImport("comctl32.dll")]
        internal static extern nint DefSubclassProc(
            nint windowHandle,
            uint message,
            nuint wParam,
            nint lParam);
    }
}
