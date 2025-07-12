using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace DumpMiner.Infrastructure.UI
{
    public class ModelToContextConverter : IValueConverter
    {
        private static readonly Dictionary<string, int> OpenAIModelContext = new()
        {
            ["gpt-4.1"] = 1000000,
            ["gpt-4.1-mini"] = 1000000,
            ["gpt-4.1-nano"] = 1000000,
            ["o3"] = 200000,
            ["o4-mini"] = 200000,
            ["o3-mini"] = 200000,
            ["o1"] = 200000,
            ["gpt-4o"] = 128000,
            ["gpt-4o-mini"] = 128000,
            ["gpt-4.5"] = 128000,
        };

        private static readonly Dictionary<string, int> AnthropicModelContext = new()
        {
            ["claude-opus-4"] = 200000,
            ["claude-sonnet-4"] = 200000,
            ["claude-sonnet-3.7"] = 200000,
            ["claude-sonnet-3.5"] = 200000,
            ["claude-haiku-3.5"] = 200000,
        };

        private static readonly Dictionary<string, int> GoogleModelContext = new()
        {
            ["gemini-2.5-pro"] = 1048576,
            ["gemini-2.5-flash"] = 1048576,
            ["gemini-2.0-flash"] = 1048576,
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string model && !string.IsNullOrEmpty(model))
            {
                int context = 0;
                if (OpenAIModelContext.TryGetValue(model, out context) ||
                    AnthropicModelContext.TryGetValue(model, out context) ||
                    GoogleModelContext.TryGetValue(model, out context))
                {
                    return context >= 1000000 ? $"{context / 1000000}M" : $"{context / 1000}K";
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