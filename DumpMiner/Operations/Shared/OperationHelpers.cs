using System;
using System.Collections.Generic;
using System.Linq;

namespace DumpMiner.Operations.Shared
{
    /// <summary>
    /// Shared utility methods for AI-enabled operations
    /// </summary>
    public static class OperationHelpers
    {
        /// <summary>
        /// Safely gets a property value from an object using reflection
        /// </summary>
        public static object GetPropertyValue(object obj, string propertyName)
        {
            try
            {
                return obj?.GetType().GetProperty(propertyName)?.GetValue(obj);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a property value and converts it to the specified type
        /// </summary>
        public static T GetPropertyValue<T>(object obj, string propertyName, T defaultValue = default)
        {
            try
            {
                var value = GetPropertyValue(obj, propertyName);
                if (value is T typedValue)
                    return typedValue;
                if (value != null && typeof(T) == typeof(string))
                    return (T)(object)value.ToString();
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Formats memory addresses consistently
        /// </summary>
        public static string FormatAddress(ulong address)
        {
            return $"0x{address:X}";
        }

        /// <summary>
        /// Formats memory sizes with appropriate units
        /// </summary>
        public static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F2} KB";
            return $"{bytes} bytes";
        }

        /// <summary>
        /// Groups objects by a property and returns top groups
        /// </summary>
        public static Dictionary<string, int> GetTopGroups<T>(IEnumerable<T> items, Func<T, string> groupSelector, int topCount = 10)
        {
            try
            {
                return items
                    .GroupBy(groupSelector)
                    .OrderByDescending(g => g.Count())
                    .Take(topCount)
                    .ToDictionary(g => g.Key ?? "Unknown", g => g.Count());
            }
            catch
            {
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Checks if a collection contains potential issues based on size
        /// </summary>
        public static List<string> AnalyzePotentialIssues(int itemCount, long totalSize = 0, string itemType = "items")
        {
            var issues = new List<string>();

            if (itemCount > 100000)
                issues.Add($"⚠️ High number of {itemType} ({itemCount:N0}) - investigate for memory leaks");
            
            if (totalSize > 500_000_000) // 500MB
                issues.Add($"⚠️ Large total size ({FormatSize(totalSize)}) - memory pressure concern");

            return issues;
        }

        /// <summary>
        /// Creates a summary of collection insights
        /// </summary>
        public static string CreateInsightsSummary(
            int itemCount, 
            string operationName, 
            Dictionary<string, int> topTypes = null, 
            List<string> keyFindings = null, 
            List<string> potentialIssues = null)
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"Analysis of {itemCount:N0} items from {operationName}");

            if (topTypes?.Any() == true)
            {
                var topTypesList = topTypes.Take(3).Select(kvp => $"{kvp.Key} ({kvp.Value:N0})");
                summary.AppendLine($"Top types: {string.Join(", ", topTypesList)}");
            }

            if (keyFindings?.Any() == true)
            {
                summary.AppendLine($"Key findings: {string.Join("; ", keyFindings)}");
            }

            if (potentialIssues?.Any() == true)
            {
                summary.AppendLine($"Potential issues: {string.Join("; ", potentialIssues)}");
            }

            return summary.ToString();
        }
    }
} 