using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DumpMiner.Common;
using DumpMiner.Models;
using Microsoft.Extensions.Logging;

namespace DumpMiner.Services.AI.Context
{
    /// <summary>
    /// Implementation of operation context builder
    /// </summary>
    [Export(typeof(IOperationContextBuilder))]
    public class OperationContextBuilder : IOperationContextBuilder
    {
        private readonly ILogger<OperationContextBuilder> _logger;

        [ImportingConstructor]
        public OperationContextBuilder(ILogger<OperationContextBuilder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public OperationContext BuildContext(
            string operationName,
            OperationModel operationModel,
            Collection<object> operationResults,
            string userQuery = null)
        {
            try
            {
                var systemPrompt = GetSystemPrompt(operationName);
                var formattedResults = FormatResultsForAI(operationName, operationResults);
                var insights = ExtractInsights(operationName, operationResults);
                var userPrompt = BuildUserPrompt(operationName, operationModel, userQuery, insights);
                var cacheKey = GenerateCacheKey(operationName, formattedResults, userQuery);

                return new OperationContext
                {
                    OperationName = operationName,
                    SystemPrompt = systemPrompt,
                    UserPrompt = userPrompt,
                    FormattedResults = formattedResults,
                    Insights = insights,
                    ConversationHistory = operationModel.Chat?.ToList() ?? new List<ConversationMessage>(),
                    EstimatedTokens = EstimateTokenCount(systemPrompt + userPrompt + formattedResults),
                    CacheKey = cacheKey,
                    Metadata = BuildMetadata(operationModel, operationResults)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building context for operation {OperationName}", operationName);
                throw;
            }
        }

        public string GetSystemPrompt(string operationName)
        {
            var basePrompt = @"You are an expert .NET memory dump analyst with deep knowledge of CLR internals, garbage collection, and debugging techniques.

Your role is to analyze memory dump data and provide actionable insights for .NET developers to diagnose:
- Memory leaks and high memory usage
- Performance issues and bottlenecks  
- Threading problems and deadlocks
- Exception analysis and crash diagnosis
- Object lifecycle and GC-related issues

Guidelines:
1. Provide clear, specific, and actionable recommendations
2. Explain technical findings in terms that developers can understand and act upon
3. Prioritize the most critical issues first
4. Reference specific memory addresses, object types, and code patterns when relevant
5. Suggest concrete debugging steps and tools when appropriate
6. When you need additional data to complete your analysis, use function calls to gather more information

Available Operations:
";

            basePrompt += OperationNames.GetOperationDescriptionsAsText();

            // Add operation-specific context
            var operationDescriptions = OperationNames.GetOperationDescriptions();
            if (operationDescriptions.TryGetValue(operationName, out var description))
            {
                basePrompt += $"\n\nCurrent Operation Context:\nYou are analyzing results from {operationName}: {description}\n";
            }

            return basePrompt;
        }

        public string FormatResultsForAI(
            string operationName,
            Collection<object> operationResults,
            int maxTokens = 40000)
        {
            if (operationResults == null || !operationResults.Any())
            {
                return "No results available for analysis.";
            }

            var formatter = new StringBuilder();
            formatter.AppendLine($"=== {operationName} RESULTS ===");
            formatter.AppendLine($"Total Items: {operationResults.Count}");
            formatter.AppendLine();

            // Smart sampling based on operation type and result count
            var sampled = SampleResults(operationName, operationResults, maxTokens);

            foreach (var item in sampled)
            {
                formatter.AppendLine(FormatSingleItem(operationName, item));

                // Rough token estimation (4 chars ≈ 1 token)
                if (formatter.Length > maxTokens * 4)
                {
                    formatter.AppendLine("...[Results truncated due to size limits]...");
                    break;
                }
            }

            return formatter.ToString();
        }

        public OperationInsights ExtractInsights(
            string operationName,
            Collection<object> operationResults)
        {
            var insights = new OperationInsights
            {
                TotalItems = operationResults?.Count ?? 0
            };

            if (operationResults == null || !operationResults.Any())
            {
                insights.Summary = "No data available for analysis";
                return insights;
            }

            // Extract type information using the actual heap object type
            var typeGroups = operationResults
                .GroupBy(item => GetActualTypeName(item))
                .ToList();

            insights.ItemTypes = typeGroups.Select(g => g.Key).ToList();
            insights.TypeCounts = typeGroups.ToDictionary(g => g.Key, g => g.Count());

            // Operation-specific insights
            ExtractOperationSpecificInsights(operationName, operationResults, insights);

            // Generate summary
            insights.Summary = GenerateInsightsSummary(operationName, insights);

            return insights;
        }

        private string GetActualTypeName(object item)
        {
            if (item == null) return "null";

            try
            {
                // Try to get the Type property which contains the actual heap object type
                var typeProperty = item.GetType().GetProperty("Type");
                if (typeProperty != null)
                {
                    var typeValue = typeProperty.GetValue(item);
                    return typeValue?.ToString() ?? item.GetType().Name;
                }
            }
            catch
            {
                // Fall back to runtime type if Type property access fails
            }

            return item.GetType().Name;
        }

        private string BuildUserPrompt(
            string operationName,
            OperationModel operationModel,
            string userQuery,
            OperationInsights insights)
        {
            var prompt = new StringBuilder();

            // User's specific question
            if (!string.IsNullOrEmpty(userQuery))
            {
                prompt.AppendLine("USER'S QUESTION:");
                prompt.AppendLine(userQuery);
                prompt.AppendLine();
            }

            // Operation context
            prompt.AppendLine("OPERATION CONTEXT:");
            prompt.AppendLine($"Operation: {operationName}");

            if (operationModel.ObjectAddress != 0)
                prompt.AppendLine($"Focus Address: 0x{operationModel.ObjectAddress:X}");

            if (!string.IsNullOrEmpty(operationModel.Types))
                prompt.AppendLine($"Type Filter: {operationModel.Types}");

            if (operationModel.NumOfResults > 0)
                prompt.AppendLine($"Results Limit: {operationModel.NumOfResults}");

            // Include custom parameter information if available
            if (!string.IsNullOrEmpty(operationModel.CustomParameterDescription))
                prompt.AppendLine($"Operation Parameter: {operationModel.CustomParameterDescription}");
            else if (operationModel.CustomParameter != null)
                prompt.AppendLine($"Operation Parameter: {operationModel.CustomParameter}");

            prompt.AppendLine();

            // Quick insights
            prompt.AppendLine("QUICK INSIGHTS:");
            prompt.AppendLine(insights.Summary);
            prompt.AppendLine();

            // Analysis request
            if (!string.IsNullOrEmpty(userQuery))
            {
                prompt.AppendLine("Please analyze the data below and provide a focused response to the user's question, plus any critical issues you notice:");
            }
            else
            {
                prompt.AppendLine("Please analyze the data below and identify potential issues, performance problems, or debugging recommendations:");
            }

            return prompt.ToString();
        }

        private IEnumerable<object> SampleResults(
            string operationName,
            Collection<object> operationResults,
            int maxTokens)
        {
            var maxItems = Math.Min(operationResults.Count, maxTokens / 50); // Rough estimate

            // Different sampling strategies based on operation type
            return operationName switch
            {
                var name when name.Contains("Exception") => operationResults.Take(maxItems),
                var name when name.Contains("Stack") => operationResults.Take(Math.Min(maxItems, 50)),
                var name when name.Contains("Heap") => SampleHeapResults(operationResults, maxItems),
                _ => operationResults.Take(maxItems)
            };
        }

        private IEnumerable<object> SampleHeapResults(Collection<object> results, int maxItems)
        {
            // For heap operations, sample largest objects first
            try
            {
                var sampled = results
                    .Where(item => item != null)
                    .OrderByDescending(item => GetObjectSize(item))
                    .Take(maxItems / 2)
                    .Concat(results.Take(maxItems / 2))
                    .Distinct()
                    .Take(maxItems);

                return sampled;
            }
            catch
            {
                return results.Take(maxItems);
            }
        }

        private long GetObjectSize(object item)
        {
            try
            {
                var sizeProperty = item.GetType().GetProperty("Size");
                if (sizeProperty != null && sizeProperty.PropertyType == typeof(ulong))
                {
                    return (long)(ulong)sizeProperty.GetValue(item);
                }
            }
            catch
            {
                // Ignore errors
            }
            return 0;
        }

        private string FormatSingleItem(string operationName, object item)
        {
            if (item == null) return "null";

            try
            {
                // Get the actual heap object type name
                string typeName;
                try
                {
                    typeName = operationName switch
                    {
                        var name when name.StartsWith("DumpHeap", StringComparison.InvariantCultureIgnoreCase) => FormatHeapItem(item),
                        _ => FormatGeneralItem(item)
                    };
                }
                catch
                {
                    typeName = item.GetType().Name;
                }

                // Handle common memory dump objects
                var properties = item.GetType().GetProperties()
                    .Where(p => p.CanRead && IsRelevantProperty(p.Name))
                    .Take(10) // Limit properties to avoid overwhelming output
                    .ToList();

                if (!properties.Any())
                {
                    return item.ToString();
                }

                var formatted = new StringBuilder();
                formatted.AppendLine($"{typeName}:");

                foreach (var prop in properties)
                {
                    try
                    {
                        var value = prop.GetValue(item);
                        var formattedValue = FormatPropertyValue(value);
                        formatted.AppendLine($"  {prop.Name}: {formattedValue}");
                    }
                    catch
                    {
                        formatted.AppendLine($"  {prop.Name}: [Error reading value]");
                    }
                }

                return formatted.ToString();
            }
            catch
            {
                return item.ToString();
            }
        }

        private string FormatHeapItem(object item)
        {
            return item?.GetType().GetProperty("Name")?.GetValue(item) as string ?? "unknown";
        }

        private static string FormatGeneralItem(object item)
        {
            string typeName;
            var typeProperty = item.GetType().GetProperty("Type");
            if (typeProperty != null)
            {
                var typeValue = typeProperty.GetValue(item);
                typeName = typeValue?.ToString() ?? item.GetType().Name;
            }
            else
            {
                typeName = item.GetType().Name;
            }

            return typeName;
        }

        private bool IsRelevantProperty(string propertyName)
        {
            var irrelevantProperties = new[] { "GetHashCode", "GetType", "ToString", "Equals" };
            return !irrelevantProperties.Contains(propertyName);
        }

        private string FormatPropertyValue(object value)
        {
            if (value == null) return "null";

            return value switch
            {
                ulong ul when ul > 0x1000 => $"0x{ul:X}",
                byte[] bytes when bytes.Length > 10 => $"byte[{bytes.Length}]",
                string str when str.Length > 100 => str.Substring(0, 100) + "...",
                _ => value.ToString()
            };
        }

        private void ExtractOperationSpecificInsights(
            string operationName,
            Collection<object> operationResults,
            OperationInsights insights)
        {
            try
            {
                switch (operationName)
                {
                    case var name when name.Contains("Exception"):
                        ExtractExceptionInsights(operationResults, insights);
                        break;
                    case var name when name.Contains("Heap"):
                        ExtractHeapInsights(operationResults, insights);
                        break;
                    case var name when name.Contains("Stack"):
                        ExtractStackInsights(operationResults, insights);
                        break;
                    default:
                        ExtractGenericInsights(operationResults, insights);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting operation-specific insights for {OperationName}", operationName);
            }
        }

        private void ExtractExceptionInsights(Collection<object> results, OperationInsights insights)
        {
            insights.KeyFindings.Add($"Found {results.Count} exception objects");
            if (results.Count > 10)
            {
                insights.PotentialIssues.Add("High number of exceptions may indicate stability issues");
            }
        }

        private void ExtractHeapInsights(Collection<object> results, OperationInsights insights)
        {
            var totalSize = results.Sum(GetObjectSize);
            insights.Statistics["TotalHeapSize"] = totalSize;
            insights.KeyFindings.Add($"Total heap size analyzed: {totalSize:N0} bytes");

            if (totalSize > 100_000_000) // 100MB
            {
                insights.PotentialIssues.Add("Large heap size detected - investigate for memory leaks");
            }
        }

        private void ExtractStackInsights(Collection<object> results, OperationInsights insights)
        {
            insights.KeyFindings.Add($"Analyzed {results.Count} stack frames/threads");
        }

        private void ExtractGenericInsights(Collection<object> results, OperationInsights insights)
        {
            insights.KeyFindings.Add($"Processed {results.Count} items of various types");
        }

        private string GenerateInsightsSummary(string operationName, OperationInsights insights)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"Analysis of {insights.TotalItems} items from {operationName}");

            if (insights.TypeCounts.Any())
            {
                var topTypes = insights.TypeCounts.OrderByDescending(kvp => kvp.Value).Take(3);
                summary.AppendLine($"Top types: {string.Join(", ", topTypes.Select(kvp => $"{kvp.Key} ({kvp.Value})"))}");
            }

            if (insights.KeyFindings.Any())
            {
                summary.AppendLine($"Key findings: {string.Join("; ", insights.KeyFindings)}");
            }

            if (insights.PotentialIssues.Any())
            {
                summary.AppendLine($"Potential issues: {string.Join("; ", insights.PotentialIssues)}");
            }

            return summary.ToString();
        }

        private Dictionary<string, object> BuildMetadata(OperationModel operationModel, Collection<object> operationResults)
        {
            return new Dictionary<string, object>
            {
                ["operationModel"] = new
                {
                    operationModel.ObjectAddress,
                    operationModel.Types,
                    operationModel.NumOfResults
                },
                ["resultCount"] = operationResults?.Count ?? 0,
                ["hasConversationHistory"] = operationModel.Chat?.Any() == true,
                ["timestamp"] = DateTimeOffset.UtcNow
            };
        }

        private string GenerateCacheKey(string operationName, string formattedResults, string userQuery)
        {
            var content = $"{operationName}|{formattedResults}|{userQuery ?? ""}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(hash)[..16]; // First 16 chars of hash
        }

        private int EstimateTokenCount(string text)
        {
            // Rough estimation: ~4 characters per token for English text
            return string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}