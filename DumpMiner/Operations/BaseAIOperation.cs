using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Models;
using DumpMiner.Services.AI.Orchestration;

namespace DumpMiner.Operations
{
    /// <summary>
    /// Base class for AI-enabled operations, providing common AI functionality
    /// </summary>
    public abstract class BaseAIOperation : IAIEnabledOperation
    {
        public abstract string Name { get; }

        public abstract Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter);

        public virtual async Task<string> AskAi(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            try
            {
                // Preserve the original operation parameter context for AI analysis
                // This ensures that when AI is asked questions, it has the same context as the original operation
                if (model.CustomParameter == null && parameter != null)
                {
                    // No existing custom parameter, use the provided parameter
                    model.CustomParameter = parameter;
                    model.CustomParameterDescription = GetCustomParameterDescription(parameter);
                }
                else if (model.CustomParameter != null && string.IsNullOrEmpty(model.CustomParameterDescription))
                {
                    // Custom parameter exists but description is missing - regenerate it
                    model.CustomParameterDescription = GetCustomParameterDescription(model.CustomParameter);
                }
                // If model.CustomParameter is already set, preserve it (don't overwrite with null from UI button clicks)
                
                // Get the AI orchestrator from the container
                var orchestrator = App.Container?.GetExportedValue<IAIOrchestrator>();
                if (orchestrator == null)
                {
                    // Try to get the simple AI helper as fallback
                    if (App.AIHelper != null)
                    {
                        var query = model.UserPrompt?.ToString()?.Trim();
                        if (string.IsNullOrEmpty(query))
                        {
                            return "Please provide a question or query for AI analysis.";
                        }

                        var systemPrompt = GetSystemPromptAdditions();
                        var contextPrompt = GetAIInsights(items);
                        
                        return await App.AIHelper.Ask(
                            new[] { systemPrompt, contextPrompt },
                            new[] { query }
                        );
                    }
                    
                    return "AI orchestrator is not available. Please check your AI configuration and ensure the services are properly initialized.";
                }

                // Get user query from the model
                var userQuery = model.UserPrompt?.ToString()?.Trim();

                // Use the orchestrator to analyze the operation
                var result = await orchestrator.AnalyzeOperationAsync(
                    Name,
                    model,
                    items,
                    userQuery,
                    token);

                if (!result.IsSuccess)
                {
                    return result.ErrorMessage ?? "AI analysis failed";
                }

                // Add the conversation to the model
                if (!string.IsNullOrEmpty(userQuery))
                {
                    model.Chat.Add(new ConversationMessage { Role = "user", Content = userQuery });
                }

                model.Chat.Add(new ConversationMessage { Role = "assistant", Content = result.Content });

                // Execute any suggested function calls if applicable
                if (result.SuggestedFunctionCalls?.Any() == true)
                {
                    var functionCallResults = new StringBuilder();
                    functionCallResults.AppendLine(result.Content);
                    functionCallResults.AppendLine();
                    functionCallResults.AppendLine("=== ADDITIONAL ANALYSIS ===");

                    foreach (var functionCall in result.SuggestedFunctionCalls.Take(3)) // Limit to 3 function calls
                    {
                        var functionResult = await orchestrator.ExecuteFunctionCallAsync(functionCall, token);
                        if (functionResult.IsSuccess)
                        {
                            functionCallResults.AppendLine($"Result from {functionCall.FunctionName}:");
                            functionCallResults.AppendLine(functionResult.Result?.ToString() ?? "No data");
                            functionCallResults.AppendLine();
                        }
                    }

                    return functionCallResults.ToString();
                }

                return result.Content;
            }
            catch (Exception ex)
            {
                return $"Error during AI analysis: {ex.Message}";
            }
        }

        /// <summary>
        /// Override this method in specific operations to provide human-readable description
        /// of the custom parameter for AI context
        /// </summary>
        /// <param name="customParameter">The custom parameter passed to the operation</param>
        /// <returns>Human-readable description for AI context</returns>
        public virtual string GetCustomParameterDescription(object customParameter)
        {
            if (customParameter == null)
                return null;
                
            return $"Custom parameter: {customParameter}";
        }

        public virtual AIFunctionDefinition GetAIFunctionDefinition()
        {
            return new AIFunctionDefinition
            {
                Name = Name,
                Description = GetOperationDescription(),
                OperationName = Name,
                Parameters = GetFunctionParameters()
            };
        }

        public virtual string GetAIInsights(Collection<object> operationResults)
        {
            if (operationResults == null || !operationResults.Any())
                return "No results to analyze.";

            var insights = new StringBuilder();
            insights.AppendLine($"Operation: {Name}");
            insights.AppendLine($"Total items: {operationResults.Count}");

            // Get type distribution using actual heap object types
            var typeGroups = operationResults
                .GroupBy(item => GetActualTypeName(item))
                .ToDictionary(g => g.Key, g => g.Count());

            if (typeGroups.Any())
            {
                insights.AppendLine("Type distribution:");
                var sortedTypes = typeGroups.OrderByDescending(kvp => kvp.Value);
                foreach (var typeGroup in sortedTypes.Take(10))
                {
                    insights.AppendLine($"  {typeGroup.Key}: {typeGroup.Value}");
                }

                // Add suggestions for automated investigation
                insights.AppendLine();
                insights.AppendLine("=== AUTOMATED INVESTIGATION SUGGESTIONS ===");
                
                // Suggest investigating large object types
                var topTypes = sortedTypes.Take(3).ToList();
                foreach (var (typeName, count) in topTypes)
                {
                    if (count > 10)
                    {
                        insights.AppendLine($"• High count of {typeName} ({count} instances) - consider DumpTypeInfo for detailed analysis");
                    }
                }

                // Look for specific patterns that warrant investigation
                AddOperationSpecificSuggestions(insights, operationResults, typeGroups);
            }

            return insights.ToString();
        }

        protected virtual void AddOperationSpecificSuggestions(
            StringBuilder insights, 
            Collection<object> operationResults, 
            Dictionary<string, int> typeGroups)
        {
            // Base implementation - operations can override for specific suggestions
            var operationName = Name.ToLower();
            
            if (operationName.Contains("heap"))
            {
                // Heap-specific suggestions
                var largeObjects = GetLargeObjects(operationResults);
                if (largeObjects.Any())
                {
                    insights.AppendLine($"• Found {largeObjects.Count} large objects - recommend DumpObject for detailed analysis");
                    foreach (var obj in largeObjects.Take(3))
                    {
                        var address = GetObjectAddress(obj);
                        if (address != 0)
                        {
                            insights.AppendLine($"  - Large object at 0x{address:X} ({GetObjectSize(obj)} bytes)");
                        }
                    }
                }

                // Check for potential memory leaks
                if (typeGroups.Any(kvp => kvp.Value > 1000))
                {
                    var suspiciousTypes = typeGroups.Where(kvp => kvp.Value > 1000).Take(3);
                    foreach (var (typeName, count) in suspiciousTypes)
                    {
                        insights.AppendLine($"• Potential memory leak: {count} instances of {typeName} - recommend GetObjectRoot analysis");
                    }
                }
            }
            else if (operationName.Contains("exception"))
            {
                // Exception-specific suggestions
                insights.AppendLine("• Multiple exceptions detected - recommend DumpClrStack for stack trace analysis");
                insights.AppendLine("• Consider DumpSourceCode to examine the code where exceptions occurred");
            }
            else if (operationName.Contains("stack"))
            {
                // Stack-specific suggestions
                insights.AppendLine("• Stack frames available - recommend DumpSourceCode for code analysis");
                insights.AppendLine("• Consider DumpMethods for detailed method information");
            }
        }

        protected List<object> GetLargeObjects(Collection<object> operationResults)
        {
            var largeObjects = new List<object>();
            
            foreach (var item in operationResults)
            {
                var size = GetObjectSize(item);
                if (size > 85000) // LOH threshold
                {
                    largeObjects.Add(item);
                }
            }

            return largeObjects.OrderByDescending(GetObjectSize).ToList();
        }

        protected ulong GetObjectAddress(object item)
        {
            try
            {
                var addressProperty = item.GetType().GetProperty("Address");
                if (addressProperty != null && addressProperty.PropertyType == typeof(ulong))
                {
                    return (ulong)addressProperty.GetValue(item);
                }
            }
            catch
            {
                // Ignore errors
            }
            return 0;
        }

        protected long GetObjectSize(object item)
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

        protected string GetActualTypeName(object item)
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

        public virtual string GetSystemPromptAdditions()
        {
            return $"This analysis is for the {Name} operation. " +
                   $"Focus on insights specific to {GetOperationDescription().ToLower()}.";
        }

        protected virtual string GetOperationDescription()
        {
            // Try to get description from OperationNames class
            var descriptions = OperationNames.GetOperationDescriptions();
            return descriptions.TryGetValue(Name, out var description)
                ? description
                : $"Performs {Name} operation on memory dump data";
        }

        protected virtual Dictionary<string, AIFunctionParameter> GetFunctionParameters()
        {
            return new Dictionary<string, AIFunctionParameter>
            {
                ["objectAddress"] = new AIFunctionParameter
                {
                    Type = "string",
                    Description = "Memory address of the object to analyze (in hex format, e.g., '0x12345678')",
                    Required = false
                },
                ["types"] = new AIFunctionParameter
                {
                    Type = "string",
                    Description = "Type filter to limit results to specific object types",
                    Required = false
                },
                ["numOfResults"] = new AIFunctionParameter
                {
                    Type = "integer",
                    Description = "Maximum number of results to return",
                    Required = false,
                    DefaultValue = 100
                },
                ["customParameter"] = new AIFunctionParameter
                {
                    Type = "object",
                    Description = "Operation-specific custom parameter",
                    Required = false
                }
            };
        }
    }
}