using Spectro.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;

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
            DataContext = ViewModelLocator.ServiceProvider.GetRequiredService<LoginViewModel>();
            InitializeComponent();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.Password = PasswordBox.Password;
            }
        }
    }
}
