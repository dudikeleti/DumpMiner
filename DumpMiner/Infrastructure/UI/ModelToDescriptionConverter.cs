using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace DumpMiner.Infrastructure.UI
{
    public class ModelToDescriptionConverter : IValueConverter
    {
        private static readonly Dictionary<string, string> OpenAIModelDescriptions = new()
        {
            ["gpt-4.1"] = "Most capable model with massive context",
            ["gpt-4.1-mini"] = "High performance, cost-efficient with 1M context",
            ["gpt-4.1-nano"] = "Fastest, cheapest with 1M context",
            ["o3"] = "Advanced reasoning and complex problem-solving",
            ["o4-mini"] = "Fast reasoning model, cost-efficient",
            ["o3-mini"] = "Lightweight reasoning model",
            ["o1"] = "General reasoning model",
            ["gpt-4o"] = "Multimodal model with vision and audio",
            ["gpt-4o-mini"] = "Compact multimodal model",
            ["gpt-4.5"] = "Enhanced general intelligence model",
        };

        private static readonly Dictionary<string, string> AnthropicModelDescriptions = new()
        {
            ["claude-opus-4"] = "Most capable model for complex tasks",
            ["claude-sonnet-4"] = "High-performance balanced model",
            ["claude-sonnet-3.7"] = "High intelligence with extended thinking",
            ["claude-sonnet-3.5"] = "Intelligent model for various tasks",
            ["claude-haiku-3.5"] = "Fastest model for simple tasks",
        };

        private static readonly Dictionary<string, string> GoogleModelDescriptions = new()
        {
            ["gemini-2.5-pro"] = "Most capable for complex reasoning",
            ["gemini-2.5-flash"] = "Fast and efficient general purpose",
            ["gemini-2.0-flash"] = "Fast general-purpose model",
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string model && !string.IsNullOrEmpty(model))
            {
                if (OpenAIModelDescriptions.TryGetValue(model, out var description) ||
                    AnthropicModelDescriptions.TryGetValue(model, out description) ||
                    GoogleModelDescriptions.TryGetValue(model, out description))
                {
                    return description;
                }
            }

            return "No description available";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 