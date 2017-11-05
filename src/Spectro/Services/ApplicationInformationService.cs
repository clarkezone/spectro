using Windows.ApplicationModel;
using Spectro.Core.Interfaces;

namespace Spectro.Services
{
    public class ApplicationInformationService : IApplicationInformationService
    {
        public ApplicationInformationService()
        {
            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            AppName = package.DisplayName;
            AppVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }

        public string AppVersion { get; }
        public string AppName { get; }
    }
}
