using System;
using System.Collections.Generic;
using System.Linq;

namespace NewsBlurSharp.Extensions
{
    internal static class DictionaryExtensions
    {
        internal static string ToQueryString(this Dictionary<string, string> options)
        {
            return options.Aggregate("", (s, pair) =>
                s + string.Format("{0}{1}={2}", s.Length > 0 ? "&" : "",
                    Uri.EscapeDataString(pair.Key),
                    pair.Value.ToString().UriEncode()));
        }

        internal static void AddIfNotNull<T>(this Dictionary<string, string> postData, string key, T? item) where T : struct
        {
            if (item.HasValue)
            {
                postData.Add(key, item.Value.ToString());
            }
        }

        internal static void AddIfNotNull<T>(this Dictionary<string, string> postData, string key, T? item, Func<T, string> func) where T : struct
        {
            if (item.HasValue)
            {
                postData.Add(key, func(item.Value));
            }
        }

        internal static void AddIfNotNull(this Dictionary<string, string> postData, string key, bool? item)
        {
            if (item.HasValue)
            {
                postData.Add(key, item.Value.ToString());
            }
        }

        internal static void AddIfNotNull(this Dictionary<string, string> postData, string key, string item)
        {
            if (!string.IsNullOrEmpty(item))
            {
                postData.Add(key, item);
            }
        }

        internal static void AddIfNotNull<T>(this Dictionary<string, string> postData, string key, List<T> item)
        {
            if (item != null && item.Any())
            {
                var data = $"{string.Join(",", item)}";
                postData.Add(key, data);
            }
        }

        internal static T GetValue<T>(this List<KeyValuePair<string, object>> items, string key)
        {
            var item = items.FirstOrDefault(x => x.Key == key);
            if (item.Value == null)
            {
                return default(T);
            }

            var value = (T)item.Value;
            return value;
        }
    }
}