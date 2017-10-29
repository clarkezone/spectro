using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spectro.Core.Interfaces;
using Moq;
using System.Threading.Tasks;

namespace Spectro.ViewModels.Tests
{
    [TestClass]
    public class NavigationRootViewmodelTests
    {
        [TestMethod]
        public void TestLogin()
        {
            //TODO this doesn't pass 
            var mockCreds = new Mock<ICredentialsPrompt>();
            mockCreds.Setup(mc => mc.GetUsernamePassword()).Returns(("un","pw"));

            var mock = new Mock<INewsBlurService>();
            
            mock.Setup(fw => fw.Login(mockCreds.Object)).Returns(Task.CompletedTask);
            
            NavigationRootViewModel model = new NavigationRootViewModel(mock.Object, null);
            model.LoginLogoutCommand.Execute(null);
        }
    }
}
