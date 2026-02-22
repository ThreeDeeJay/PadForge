using System;
using System.Globalization;
using System.Windows.Data;

namespace PadForge.Converters
{
    /// <summary>
    /// Converts a boolean to an install/uninstall label string.
    /// true  → "Installed"   (or "Uninstall" when parameter is "Action")
    /// false → "Not Installed" (or "Install"  when parameter is "Action")
    /// </summary>
    public sealed class BoolToInstallTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInstalled = value is bool b && b;
            bool isAction = parameter is string s &&
                            s.Equals("Action", StringComparison.OrdinalIgnoreCase);

            if (isAction)
                return isInstalled ? "Uninstall" : "Install";

            return isInstalled ? "Installed" : "Not Installed";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
