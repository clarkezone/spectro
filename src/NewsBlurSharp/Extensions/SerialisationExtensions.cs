using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NewsBlurSharp.Extensions
{
    internal static class SerialisationExtensions
    {
        internal static Task<TReturnType> DeserialiseAsync<TReturnType>(this string json, JsonSerializerSettings settings = null)
        {
            if (settings == null)
            {
                return Task.Factory.StartNew(() => JsonConvert.DeserializeObject<TReturnType>(json));
            } else
            {
                return Task.Factory.StartNew(() => JsonConvert.DeserializeObject<TReturnType>(json,settings));
            }
        }

        internal static Task<string> SerialiseAsync(this object item)
        {
            return Task.Factory.StartNew(() => JsonConvert.SerializeObject(item));
        }
    }
}