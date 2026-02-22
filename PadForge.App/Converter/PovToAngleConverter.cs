using System;
using System.Globalization;
using System.Windows.Data;

namespace PadForge.Converters
{
    /// <summary>
    /// Converts a POV hat value in centidegrees (0–35999) to a rotation angle in degrees (0.0–359.99).
    /// A value of -1 (centered / no direction) converts to <see cref="double.NaN"/>,
    /// which can be used by the UI to hide the directional indicator.
    /// </summary>
    public sealed class PovToAngleConverter : IValueConverter
    {
        /// <summary>
        /// Centidegrees value indicating no direction is pressed (hat centered).
        /// </summary>
        public const int Centered = -1;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int centidegrees;

            if (value is int i)
                centidegrees = i;
            else if (value is long l)
                centidegrees = (int)l;
            else
                return double.NaN;

            if (centidegrees < 0)
                return double.NaN;

            // Centidegrees → degrees.  e.g., 9000 → 90.0 (East / Right).
            double angle = centidegrees / 100.0;

            // Clamp to valid range.
            if (angle < 0.0 || angle >= 360.0)
                return double.NaN;

            return angle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
