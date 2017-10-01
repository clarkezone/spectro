using System.Threading;
using NewsBlurSharp.Model;

namespace NewsBlurSharp.Extensions
{
    internal static class CancellationExtensions
    {
        internal static void ThrowNewsBlurExceptionIfCancelled(this CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                throw new NewsBlurException();
            }
        }
    }
}