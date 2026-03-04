using System.Collections.Generic;
using System.Threading.Tasks;

namespace Spectro.Core.Interfaces
{
    public interface IApplicationSettingsServiceHandler
    {
        bool Contains(string key);
        T Get<T>(string key);
        T Get<T>(string key, T defaultValue);
        void Set<T>(string key, T value);
        void Remove(string key);
        Task<IEnumerable<KeyValuePair<string, object>>> GetValuesAsync();
    }

    public interface IApplicationSettingsService
    {
        IApplicationSettingsServiceHandler Local { get; }
        IApplicationSettingsServiceHandler Roaming { get; }
        IApplicationSettingsServiceHandler Legacy { get; }
    }
}
