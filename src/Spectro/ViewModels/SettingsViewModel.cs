using System.Windows.Input;

using Windows.ApplicationModel;
using GalaSoft.MvvmLight.Command;
using Spectro.Core.Interfaces;
using Spectro.Core.Services;

namespace Spectro.ViewModels
{
    public class SettingsViewModel : SpectroViewModelBase
    {
        private readonly IThemeService _themeService;

        // TODO WTS: Add other settings as necessary. For help see https://github.com/Microsoft/WindowsTemplateStudio/blob/master/docs/pages/settings.md
        private SpectroTheme _elementTheme;
        private string _versionDescription;

        public SettingsViewModel(IThemeService themeService)
        {
            _themeService = themeService;
            _elementTheme = _themeService.CurrentTheme;
        }

        public SpectroTheme ElementTheme
        {
            get => _elementTheme;
            set => Set(ref _elementTheme, value);
        }


        public string VersionDescription
        {
            get => _versionDescription;
            set => Set(ref _versionDescription, value);
        }

        private ICommand _switchThemeCommand;

        public ICommand SwitchThemeCommand
        {
            get
            {
                if (_switchThemeCommand == null)
                {
                    _switchThemeCommand = new RelayCommand<SpectroTheme>(
                        async param =>
                        {
                            await _themeService.SetTheme(param);
                        });
                }

                return _switchThemeCommand;
            }
        }

        public void Initialize()
        {
            VersionDescription = GetVersionDescription();
        }

        private string GetVersionDescription()
        {
            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            return $"{package.DisplayName} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }
}
