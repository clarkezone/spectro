using Windows.ApplicationModel.Resources;
using Spectro.Core.Interfaces;

namespace Spectro.Services
{
    public class TranslationService : ITranslationService
    {
        private readonly ResourceLoader _resourceLoader = new ResourceLoader();

        public string GetString(string key) => _resourceLoader.GetString(key);
    }
}
