using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DumpMiner.Infrastructure.UI
{
    class BiggerThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || !int.TryParse(value.ToString(), out int res))
            {
                return DependencyProperty.UnsetValue;
            }

            return res > 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
