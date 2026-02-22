using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PadForge.Converters
{
    /// <summary>
    /// Converts a status string to a <see cref="SolidColorBrush"/> for status indicator display.
    /// "Running"  → Green  (#FF4CAF50)
    /// "Stopped"  → Red    (#FFF44336)
    /// "Warning"  → Orange (#FFFF9800)
    /// "Disabled" → Gray   (#FF9E9E9E)
    /// Other      → Gray   (#FF9E9E9E)
    /// </summary>
    public sealed class StatusToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush =
            new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));

        private static readonly SolidColorBrush RedBrush =
            new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));

        private static readonly SolidColorBrush OrangeBrush =
            new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));

        private static readonly SolidColorBrush GrayBrush =
            new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));

        static StatusToColorConverter()
        {
            GreenBrush.Freeze();
            RedBrush.Freeze();
            OrangeBrush.Freeze();
            GrayBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string;

            if (string.IsNullOrEmpty(status))
                return GrayBrush;

            return status.ToUpperInvariant() switch
            {
                "RUNNING"   => GreenBrush,
                "ONLINE"    => GreenBrush,
                "CONNECTED" => GreenBrush,
                "STOPPED"   => RedBrush,
                "OFFLINE"   => RedBrush,
                "ERROR"     => RedBrush,
                "WARNING"   => OrangeBrush,
                "DISABLED"  => GrayBrush,
                _           => GrayBrush,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
