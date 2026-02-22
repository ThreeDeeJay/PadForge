using System;
using System.Globalization;
using System.Windows.Data;

namespace PadForge.Converters
{
    /// <summary>
    /// Converts a normalized value (0.0–1.0) to a pixel position on a canvas.
    /// The ConverterParameter specifies the canvas dimension (width or height) as a double or string.
    /// Example XAML usage:
    ///   Canvas.Left="{Binding ThumbLX, Converter={StaticResource NormToCanvasConverter}, ConverterParameter=120}"
    /// This maps 0.0 → 0px, 0.5 → 60px, 1.0 → 120px.
    /// An optional offset (half the indicator size) can be subtracted by passing "dimension,offset"
    /// as the parameter, e.g., "120,6" means map to 0–120 then subtract 6.
    /// </summary>
    public sealed class NormToCanvasConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double normalized;

            if (value is double d)
                normalized = d;
            else if (value is float f)
                normalized = f;
            else if (value is int i)
                normalized = i / 65535.0; // convenience: accept raw axis value
            else
                return 0.0;

            normalized = Math.Clamp(normalized, 0.0, 1.0);

            double canvasSize = 100.0; // default
            double offset = 0.0;

            if (parameter != null)
            {
                string paramStr = parameter.ToString();
                string[] parts = paramStr.Split(',');

                if (parts.Length >= 1 && double.TryParse(parts[0].Trim(),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double size))
                {
                    canvasSize = size;
                }

                if (parts.Length >= 2 && double.TryParse(parts[1].Trim(),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double off))
                {
                    offset = off;
                }
            }

            return (normalized * canvasSize) - offset;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
