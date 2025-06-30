using System;
using System.Globalization;
using System.Windows.Data;

namespace DumpMiner.Infrastructure.UI
{
    /// <summary>
    /// Converts between string and integer, handling partial input gracefully
    /// </summary>
    public class StringToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
                return intValue.ToString();
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                // Handle empty string
                if (string.IsNullOrWhiteSpace(stringValue))
                    return 0;
                
                // Handle partial negative input
                if (stringValue == "-")
                    return 0; // Return valid value for partial input
                
                // Try to parse the integer
                if (int.TryParse(stringValue, out int result))
                    return result;
                
                // If parsing fails, return the current value to avoid binding errors
                return 0; // Or you could return Binding.DoNothing to keep current value
            }
            
            return 0;
        }
    }
} 