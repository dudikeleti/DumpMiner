using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace DumpMiner.Infrastructure.UI
{
    public class ModelToOutputConverter : IValueConverter
    {
        private static readonly Dictionary<string, int> OpenAIModelOutput = new()
        {
            ["gpt-4.1"] = 32768,
            ["gpt-4.1-mini"] = 32768,
            ["gpt-4.1-nano"] = 32768,
            ["o3"] = 100000,
            ["o4-mini"] = 100000,
            ["o3-mini"] = 100000,
            ["o1"] = 100000,
            ["gpt-4o"] = 16384,
            ["gpt-4o-mini"] = 16384,
            ["gpt-4.5"] = 16384,
        };

        private static readonly Dictionary<string, int> AnthropicModelOutput = new()
        {
            ["claude-opus-4"] = 32000,
            ["claude-sonnet-4"] = 64000,
            ["claude-sonnet-3.7"] = 64000,
            ["claude-sonnet-3.5"] = 8192,
            ["claude-haiku-3.5"] = 8192,
        };

        private static readonly Dictionary<string, int> GoogleModelOutput = new()
        {
            ["gemini-2.5-pro"] = 65536,
            ["gemini-2.5-flash"] = 65536,
            ["gemini-2.0-flash"] = 65536,
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string model && !string.IsNullOrEmpty(model))
            {
                int output = 0;
                if (OpenAIModelOutput.TryGetValue(model, out output) ||
                    AnthropicModelOutput.TryGetValue(model, out output) ||
                    GoogleModelOutput.TryGetValue(model, out output))
                {
                    return output >= 1000000 ? $"{output / 1000000}M" : $"{output / 1000}K";
                }
            }

            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 