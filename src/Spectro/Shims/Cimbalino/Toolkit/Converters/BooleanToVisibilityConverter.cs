using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Spectro.Shims.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public bool InvertValue { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var boolValue = value is bool flag && flag;
            if (InvertValue)
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var visible = value is Visibility visibility && visibility == Visibility.Visible;
            return InvertValue ? !visible : visible;
        }
    }
}
