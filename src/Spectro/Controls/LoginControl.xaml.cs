using Spectro.ViewModels;
using Microsoft.Extensions.DependencyInjection;

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
    }
}
