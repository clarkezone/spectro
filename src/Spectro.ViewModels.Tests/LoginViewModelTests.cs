using System.Threading.Tasks;
using Moq;
using Spectro.Core.Interfaces;
using Xunit;

namespace Spectro.ViewModels.Tests
{
    public class LoginViewModelTests
    {
        private readonly Mock<IAuthenticationService> _authenticationMock = new Mock<IAuthenticationService>();
        private readonly Mock<ISpectroNavigationService> _navigationMock = new Mock<ISpectroNavigationService>();

        private readonly LoginViewModel _subject;

        public LoginViewModelTests()
        {
            _subject = new LoginViewModel(_authenticationMock.Object, _navigationMock.Object);
        }

        [Fact]
        public async Task TestSuccessfulLogin()
        {
            _subject.Username = "u";
            _subject.Password = "p";

            _authenticationMock.Setup(x => x.Login("u", "p")).ReturnsAsync(true);

            await _subject.LoginCommand.ExecuteAsync(null);

            _navigationMock.Verify(x => x.NavigateToRootNavigation(null));
            
            //TODO this doesn't pass 
            //var mockCreds = new Mock<ICredentialsPrompt>();
            //mockCreds.Setup(mc => mc.GetUsernamePassword()).Returns(("un","pw"));

            //var mock = new Mock<INewsBlurService>();

            ////mock.Setup(fw => fw.Login(mockCreds.Object)).Returns(Task.CompletedTask);

            //NavigationRootViewModel model = new NavigationRootViewModel(mock.Object, null);
            //model.LoginLogoutCommand.Execute(null);
        }

        [Fact]
        public async Task TestUnsuccessfulLogin()
        {
            _subject.Username = "something";
            _subject.Password = "whatever";

            _authenticationMock.Setup(x => x.Login("u", "p")).ReturnsAsync(true);

            await _subject.LoginCommand.ExecuteAsync(null);

            _navigationMock.Verify(x => x.NavigateToRootNavigation(null), Times.Never);
        }
    }
}
