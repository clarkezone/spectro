using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NewsBlurSharp.Extensions
{
    internal static class SerialisationExtensions
    {
        internal static Task<TReturnType> DeserialiseAsync<TReturnType>(this string json)
        {
            return Task.Factory.StartNew(() => JsonConvert.DeserializeObject<TReturnType>(json));
        }

        internal static Task<string> SerialiseAsync(this object item)
        {
            return Task.Factory.StartNew(() => JsonConvert.SerializeObject(item));
        }
    }
}