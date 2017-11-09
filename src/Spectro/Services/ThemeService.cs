using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Cimbalino.Toolkit.Services;
using Spectro.Core.Interfaces;
using Spectro.Core.Services;
using Spectro.Extensions;
using Spectro.Helpers;

namespace Spectro.Services
{
    public class ThemeService : IThemeService
    {
        private const string RequestedThemeKey = "RequestedTheme";
        private readonly IApplicationSettingsService _applicationSettingsService;

        public ThemeService(IApplicationSettingsService applicationSettingsService)
        {
            _applicationSettingsService = applicationSettingsService;
        }

        public SpectroTheme CurrentTheme { get; private set; } = SpectroTheme.Default;

        public async Task Initialize()
        {
            var json = _applicationSettingsService.Local.Get(RequestedThemeKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var theme = await Json.ToObjectAsync<SpectroTheme>(json);
            await SetTheme(theme);
        }

        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        public async Task SetTheme(SpectroTheme theme)
        {
            CurrentTheme = theme;
            if (Window.Current.Content is FrameworkElement frameworkElement)
            {
                frameworkElement.RequestedTheme = theme.ToElementTheme();
            }

            _applicationSettingsService.Local.Set(RequestedThemeKey, await Json.StringifyAsync(theme));

            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(CurrentTheme));
        }
    }
}
