using Spectro.ViewModels;
using Windows.System;
using Windows.UI.Xaml.Input;

namespace Spectro.Controls
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoginControl 
    {
        private LoginViewModel ViewModel => DataContext as LoginViewModel;

        public LoginControl()
        {
            InitializeComponent();
        }

        private void Password_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && ViewModel?.LoginCommand?.CanExecute(null) == true)
            {
                ViewModel.LoginCommand.Execute(null);
            }
        }
    }
}
