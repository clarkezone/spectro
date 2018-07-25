using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using Spectro.Core.Interfaces;
using Spectro.Core.Services;

namespace Spectro.ViewModels
{
    public class SettingsViewModel : SpectroViewModelBase
    {
        private readonly IThemeService _themeService;
        private readonly IApplicationInformationService _applicationInformationService;

        // TODO WTS: Add other settings as necessary. For help see https://github.com/Microsoft/WindowsTemplateStudio/blob/master/docs/pages/settings.md
        private SpectroTheme _elementTheme;

        public SettingsViewModel(
            IThemeService themeService,
            IApplicationInformationService applicationInformationService)
        {
            _themeService = themeService;
            _applicationInformationService = applicationInformationService;
            _elementTheme = _themeService.CurrentTheme;
        }

        public SpectroTheme ElementTheme
        {
            get => _elementTheme;
            set => Set(ref _elementTheme, value);
        }

        public string VersionDescription => $"{_applicationInformationService.AppName} - {_applicationInformationService.AppVersion}";

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
    }
}
