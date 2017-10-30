using Microsoft.Toolkit.Uwp.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spectro.Services
{
    class IconCacheLayer
    {
        public void CacheImage(Uri uri)
        {
            ImageCache.Instance.GetFromCacheAsync(uri);
        }
    }
}
