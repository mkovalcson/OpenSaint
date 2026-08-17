using System;
using System.Globalization;
using System.Windows.Data;

namespace ServoAnimator
{
    /// <summary>Returns half of a positive numeric value. Used to keep the
    /// expanded Description editors at exactly half the current servo-grid height.</summary>
    public sealed class HalfHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && d > 0) return d / 2.0;
            return 125.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
