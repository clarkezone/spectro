namespace Spectro.Core.Interfaces
{
    public interface IApplicationInformationService
    {
        string AppVersion { get; }
        string AppName { get; }
    }
}
