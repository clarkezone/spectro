using Spectro.ViewModels;

namespace Spectro.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoginPage 
    {
        private LoginViewModel ViewModel => DataContext as LoginViewModel;

        public LoginPage()
        {
            InitializeComponent();
        }
    }
}
