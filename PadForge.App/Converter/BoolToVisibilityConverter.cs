using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PadForge.Converters
{
    /// <summary>
    /// Converts a boolean to <see cref="Visibility"/>.
    /// true → Visible, false → Collapsed.
    /// Pass "Invert" as the ConverterParameter to reverse the logic.
    /// </summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool b && b;

            if (parameter is string s &&
                s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                bool result = v == Visibility.Visible;

                if (parameter is string s &&
                    s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
                {
                    result = !result;
                }

                return result;
            }

            return false;
        }
    }
}
