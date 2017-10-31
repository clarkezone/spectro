using System.Windows.Input;

using Windows.ApplicationModel;
using Windows.UI.Xaml;

using Spectro.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace Spectro.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        // TODO WTS: Add other settings as necessary. For help see https://github.com/Microsoft/WindowsTemplateStudio/blob/master/docs/pages/settings.md
        private ElementTheme _elementTheme = ThemeSelectorService.Theme;

        public ElementTheme ElementTheme
        {
            get { return _elementTheme; }

            set
            {
                if (value != _elementTheme)
                {
                    _elementTheme = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string _versionDescription;

        public string VersionDescription
        {
            get { return _versionDescription; }

            set {

                if (value != _versionDescription)
                {
                    _versionDescription = value;
                    RaisePropertyChanged();
                }
            }
        }

        private ICommand _switchThemeCommand;

        public ICommand SwitchThemeCommand
        {
            get
            {
                if (_switchThemeCommand == null)
                {
                    _switchThemeCommand = new RelayCommand<ElementTheme>(
                        async (param) =>
                        {
                            await ThemeSelectorService.SetThemeAsync(param);
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
