using System;
using System.Globalization;
using System.Windows.Data;

namespace PadForge.Converters
{
    /// <summary>
    /// Converts an unsigned 16-bit axis value (0–65535) to a percentage (0.0–100.0).
    /// Useful for binding axis values to progress bars and text displays.
    /// </summary>
    public sealed class AxisToPercentConverter : IValueConverter
    {
        private const double MaxAxisValue = 65535.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double raw;

            if (value is int i)
                raw = i;
            else if (value is ushort u)
                raw = u;
            else if (value is double d)
                raw = d;
            else
                return 0.0;

            double percent = Math.Clamp(raw / MaxAxisValue * 100.0, 0.0, 100.0);

            // If the target is a string (e.g., for a TextBlock), format with one decimal.
            if (targetType == typeof(string))
                return percent.ToString("F1", culture) + "%";

            return percent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
