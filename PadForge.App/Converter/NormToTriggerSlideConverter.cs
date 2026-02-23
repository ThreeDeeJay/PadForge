using System;
using System.Globalization;
using System.Windows.Data;

namespace PadForge.Converters
{
    /// <summary>
    /// Converts a normalized value (0.0–1.0) to a Canvas.Top position for a sliding bar.
    /// ConverterParameter = "containerHeight,barHeight"
    /// At value 0: bar is at bottom (top = containerHeight - barHeight).
    /// At value 1: bar is at top (top = 0).
    /// </summary>
    public sealed class NormToTriggerSlideConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double norm = 0;
            if (value is double d) norm = d;
            else if (value is float f) norm = f;

            double containerH = 98, barH = 20;
            if (parameter is string s)
            {
                var parts = s.Split(',');
                if (parts.Length >= 1)
                    double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out containerH);
                if (parts.Length >= 2)
                    double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out barH);
            }

            double maxTravel = containerH - barH;
            return Math.Clamp((1.0 - norm) * maxTravel, 0, maxTravel);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
