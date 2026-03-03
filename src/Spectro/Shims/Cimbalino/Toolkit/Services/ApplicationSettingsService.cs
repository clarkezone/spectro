using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace Cimbalino.Toolkit.Services
{
    public class ApplicationSettingsService : IApplicationSettingsService
    {
        public IApplicationSettingsServiceHandler Local { get; } = new ApplicationSettingsServiceHandler(ApplicationData.Current.LocalSettings);
        public IApplicationSettingsServiceHandler Roaming { get; } = new ApplicationSettingsServiceHandler(ApplicationData.Current.RoamingSettings);
        public IApplicationSettingsServiceHandler Legacy { get; } = new ApplicationSettingsServiceHandler(ApplicationData.Current.LocalSettings);

        private class ApplicationSettingsServiceHandler : IApplicationSettingsServiceHandler
        {
            private readonly ApplicationDataContainer _container;

            public ApplicationSettingsServiceHandler(ApplicationDataContainer container)
            {
                _container = container;
            }

            public bool Contains(string key) => _container.Values.ContainsKey(key);

            public T Get<T>(string key) => Get(key, default);

            public T Get<T>(string key, T defaultValue)
                => _container.Values.TryGetValue(key, out var value) && value is T castValue ? castValue : defaultValue;

            public void Set<T>(string key, T value) => _container.Values[key] = value;

            public void Remove(string key) => _container.Values.Remove(key);

            public Task<IEnumerable<KeyValuePair<string, object>>> GetValuesAsync()
                => Task.FromResult((IEnumerable<KeyValuePair<string, object>>)new Dictionary<string, object>(_container.Values));
        }
    }
}
