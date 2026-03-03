using System;
using System.Threading.Tasks;

namespace Microsoft.Toolkit.Uwp.UI
{
    public class ImageCache
    {
        public static ImageCache Instance { get; } = new ImageCache();

        public Task<object> GetFromCacheAsync(Uri uri) => Task.FromResult<object>(null);
    }
}
