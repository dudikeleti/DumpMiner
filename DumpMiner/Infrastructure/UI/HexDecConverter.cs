using System;
using System.Windows;
using System.Windows.Data;

namespace DumpMiner.Infrastructure.UI
{
    class HexDecConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ulong u && u == 0 ||
                value == null ||
                (value as string)?.Length <= 4)
            {
                return DependencyProperty.UnsetValue;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            ulong result;
            try
            {

                if (value == null)
                {
                    return DependencyProperty.UnsetValue;
                }

                if (value is string str && str.Length > 1 && (str[1] == 'x' || str[1] == 'X'))
                {
                    result = System.Convert.ToUInt64(str, 16);
                }
                else
                {
                    result = System.Convert.ToUInt64(value);
                }
            }
            catch (Exception)
            {
                return value;
            }

            return result;

        }
    }
}
