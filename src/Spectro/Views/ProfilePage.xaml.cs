using Spectro.ViewModels;

namespace Spectro.Views
{
    public sealed partial class ProfilePage
    {
        public ProfileViewModel Vm => (ProfileViewModel)DataContext;

        public ProfilePage()
        {
            this.InitializeComponent();
        }
    }
}
