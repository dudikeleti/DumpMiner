using System;
using System.Windows.Data;

namespace DumpMiner.Infrastructure.UI
{
    class BytesToKbOrMbConverter : IValueConverter
    {
        private const int Miliion = 1000000;
        private const int Thousand = 1000;
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double val;
            if (!double.TryParse(value.ToString(), out val))
                return value;
            if (val > Miliion)
                return $"{val/Miliion} MB";
            if (val > Thousand)
                return $"{val/Thousand} KB";
            return $"{val} B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
