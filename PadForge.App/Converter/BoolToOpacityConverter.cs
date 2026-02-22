using System;
using System.Globalization;
using System.Windows.Data;

namespace PadForge.Converters
{
    /// <summary>
    /// Converts a boolean to an opacity value.
    /// true = 1.0 (fully visible), false = 0.2 (dimmed).
    /// Optional ConverterParameter formats:
    ///   Single value (e.g., "0"):   overrides the "false" opacity; true stays 1.0.
    ///   Two values (e.g., "0.1,0.6"): first = false opacity, second = true opacity.
    /// </summary>
    public sealed class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool pressed = value is bool b && b;
            double dimOpacity = 0.2;
            double brightOpacity = 1.0;

            if (parameter is string s)
            {
                var parts = s.Split(',');
                if (parts.Length >= 1 && double.TryParse(parts[0].Trim(),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double val1))
                {
                    dimOpacity = val1;
                }
                if (parts.Length >= 2 && double.TryParse(parts[1].Trim(),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double val2))
                {
                    brightOpacity = val2;
                }
            }

            return pressed ? brightOpacity : dimOpacity;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
