using Spectro.ViewModels;

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
    }
}
