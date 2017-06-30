using System;
using System.Text;

namespace NewsBlurSharp.Extensions
{
    internal static class StringExtensions
    {
        internal static string UriEncode(this string value)
        {
            var sb = new StringBuilder();

            for (var i = 0; i < value.Length; i += 32766)
            {
                sb.Append(Uri.EscapeDataString(value.Substring(i, Math.Min(32766, value.Length - i))));
            }

            return sb.ToString();
        }
    }
}