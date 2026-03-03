using System;
using Windows.UI.Xaml.Data;

namespace Spectro.Shims.Converters
{
    public class NegativeBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => !(value is bool flag && flag);

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => !(value is bool flag && flag);
    }
}
