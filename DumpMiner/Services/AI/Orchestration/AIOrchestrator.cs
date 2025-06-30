using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Models;
using DumpMiner.Services.AI.Caching;
using DumpMiner.Services.AI.Context;
using DumpMiner.Services.AI.Functions;
using DumpMiner.Services.AI.Interfaces;
using Microsoft.Extensions.Logging;

namespace DumpMiner.Services.AI.Orchestration
{
    /// <summary>
    /// Main AI orchestrator implementation
    /// </summary>
    [Export(typeof(IAIOrchestrator))]
    public class AIOrchestrator : IAIOrchestrator
    {
        private readonly IAIServiceManager _aiServiceManager;
        private readonly IOperationContextBuilder _contextBuilder;
        private readonly IAICacheService _cacheService;
        private readonly IAIFunctionRegistry _functionRegistry;
        private readonly ILogger<AIOrchestrator> _logger;

        [ImportingConstructor]
        public AIOrchestrator(
            IAIServiceManager aiServiceManager,
            IOperationContextBuilder contextBuilder,
            IAICacheService cacheService,
            IAIFunctionRegistry functionRegistry,
            ILogger<AIOrchestrator> logger)
        {
            _aiServiceManager = aiServiceManager ?? throw new ArgumentNullException(nameof(aiServiceManager));
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AIAnalysisResult> AnalyzeOperationAsync(
            string operationName,
            OperationModel operationModel,
            Collection<object> operationResults,
            string userQuery = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting AI analysis for operation {OperationName}", operationName);

                // Build comprehensive context
                var context = _contextBuilder.BuildContext(operationName, operationModel, operationResults, userQuery);

                // Check cache first
                var cachedResult = await _cacheService.GetCachedResponseAsync(context.CacheKey, cancellationToken);
                if (cachedResult != null)
                {
                    _logger.LogInformation("Returning cached AI analysis for operation {OperationName}", operationName);
                    cachedResult.FromCache = true;
                    return cachedResult;
                }

                // Check if AI is available
                if (!await IsAvailableAsync())
                {
                    return new AIAnalysisResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "AI service is not available. Please check your API configuration."
                    };
                }

                // Execute AI analysis with function calling
                var result = await ExecuteAIAnalysisWithFunctionCalling(context, cancellationToken);

                // Cache the result
                await _cacheService.SetCachedResponseAsync(
                    context.CacheKey,
                    result,
                    TimeSpan.FromMinutes(30), // Cache for 30 minutes
                    cancellationToken);

                _logger.LogInformation("AI analysis completed for operation {OperationName}", operationName);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during AI analysis for operation {OperationName}", operationName);
                return new AIAnalysisResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"AI analysis failed: {ex.Message}"
                };
            }
        }

        private async Task<AIAnalysisResult> ExecuteAIAnalysisWithFunctionCalling(
            OperationContext context,
            CancellationToken cancellationToken,
            int maxFunctionCalls = 5,
            int currentDepth = 0)
        {
            try
            {
                // Prevent infinite recursion
                if (currentDepth >= maxFunctionCalls)
                {
                    _logger.LogWarning("Maximum function call depth reached ({MaxDepth})", maxFunctionCalls);
                    return new AIAnalysisResult
                    {
                        IsSuccess = true,
                        Content = "Analysis completed with maximum function call depth reached. Consider running additional operations manually for deeper investigation."
                    };
                }

                // Build AI request with enhanced function calling prompts
                var systemPrompt = BuildEnhancedSystemPromptWithFunctions(context, currentDepth);
                var aiRequest = new Models.AIRequest
                {
                    SystemPrompt = systemPrompt,
                    UserPrompt = context.UserPrompt,
                    ConversationHistory = context.ConversationHistory,
                    MaxTokens = CalculateMaxTokens(context),
                    Temperature = 0.1 // Lower temperature for more consistent debugging analysis
                };

                // Execute AI request
                var aiResponse = await _aiServiceManager.CompleteAsync(aiRequest, cancellationToken);

                if (!aiResponse.IsSuccess)
                {
                    return new AIAnalysisResult
                    {
                        IsSuccess = false,
                        ErrorMessage = aiResponse.ErrorMessage ?? "AI request failed"
                    };
                }

                // Parse and execute function calls
                var functionCalls = ParseEnhancedFunctionCalls(aiResponse.Content);
                var analysisContent = new StringBuilder(aiResponse.Content);

                if (functionCalls.Any())
                {
                    _logger.LogInformation("AI suggested {Count} function calls", functionCalls.Count);

                    analysisContent.AppendLine();
                    analysisContent.AppendLine("=== AUTOMATED INVESTIGATION RESULTS ===");

                    // Execute function calls and gather results
                    var functionResults = new List<(AIFunctionCall call, AIFunctionCallResult result)>();

                    foreach (var functionCall in functionCalls.Take(3)) // Limit concurrent function calls
                    {
                        var functionResult = await ExecuteFunctionCallAsync(functionCall, cancellationToken);
                        functionResults.Add((functionCall, functionResult));

                        if (functionResult.IsSuccess)
                        {
                            analysisContent.AppendLine($"🔍 **{functionCall.FunctionName}** (Reason: {functionCall.Reasoning})");

                            // Format function results for display
                            var formattedResult = FormatFunctionResult(functionResult);
                            analysisContent.AppendLine(formattedResult);
                            analysisContent.AppendLine();
                        }
                        else
                        {
                            analysisContent.AppendLine($"❌ **{functionCall.FunctionName}** failed: {functionResult.ErrorMessage}");
                            analysisContent.AppendLine();
                        }
                    }

                    // If we have successful function results, run a follow-up analysis
                    if (functionResults.Any(fr => fr.result.IsSuccess) && currentDepth < maxFunctionCalls - 1)
                    {
                        var followUpContext = BuildFollowUpContext(context, functionResults);
                        var followUpResult = await ExecuteAIAnalysisWithFunctionCalling(
                            followUpContext,
                            cancellationToken,
                            maxFunctionCalls,
                            currentDepth + 1);

                        if (followUpResult.IsSuccess)
                        {
                            analysisContent.AppendLine();
                            analysisContent.AppendLine("=== FOLLOW-UP ANALYSIS ===");
                            analysisContent.AppendLine(followUpResult.Content);
                        }
                    }
                }

                // Build final result
                var result = new AIAnalysisResult
                {
                    Content = analysisContent.ToString(),
                    IsSuccess = true,
                    SuggestedFunctionCalls = functionCalls,
                    Metadata = new Dictionary<string, object>
                    {
                        ["operation"] = context.OperationName,
                        ["tokenUsage"] = aiResponse.Metadata?.TotalTokens ?? 0,
                        ["processingTime"] = aiResponse.Metadata?.ProcessingTimeMs ?? 0,
                        ["estimatedCost"] = aiResponse.Metadata?.EstimatedCost ?? 0m,
                        ["provider"] = aiResponse.Provider.ToString(),
                        ["model"] = aiResponse.Model ?? "unknown",
                        ["functionCallsExecuted"] = functionCalls.Count,
                        ["analysisDepth"] = currentDepth + 1
                    }
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during AI analysis with function calling");
                return new AIAnalysisResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"AI analysis failed: {ex.Message}"
                };
            }
        }

        public async Task<AIFunctionCallResult> ExecuteFunctionCallAsync(
            AIFunctionCall functionCall,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Executing AI function call: {FunctionName}", functionCall.FunctionName);

                // Validate function call
                if (!_functionRegistry.ValidateFunctionCall(functionCall, out var validationError))
                {
                    return new AIFunctionCallResult
                    {
                        IsSuccess = false,
                        ErrorMessage = validationError,
                        OperationName = functionCall.FunctionName
                    };
                }

                // Execute the function
                var result = await _functionRegistry.ExecuteFunctionAsync(functionCall, cancellationToken);

                _logger.LogInformation("AI function call {FunctionName} completed successfully", functionCall.FunctionName);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing AI function call {FunctionName}", functionCall.FunctionName);
                return new AIFunctionCallResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    OperationName = functionCall.FunctionName
                };
            }
        }

        public async Task<List<ConversationMessage>> GetConversationHistoryAsync(
            string operationName,
            OperationModel operationModel)
        {
            // Return the conversation history from the operation model
            return operationModel.Chat?.ToList() ?? new List<ConversationMessage>();
        }

        public async Task ClearConversationHistoryAsync(
            string operationName,
            OperationModel operationModel)
        {
            operationModel.Chat?.Clear();
            await Task.CompletedTask;
        }

        public async Task<bool> IsAvailableAsync()
        {
            return await _aiServiceManager.IsAvailableAsync();
        }

        public IEnumerable<AIFunctionDefinition> GetAvailableFunctions()
        {
            return _functionRegistry.GetAvailableFunctions();
        }

        private string BuildEnhancedSystemPromptWithFunctions(OperationContext context, int currentDepth)
        {
            var systemPrompt = new StringBuilder();
            systemPrompt.AppendLine(context.SystemPrompt);
            systemPrompt.AppendLine();
            systemPrompt.AppendLine("=== AUTOMATED DEBUGGING INVESTIGATION ===");
            systemPrompt.AppendLine("Act as an expert debugging assistant that automatically investigates issues by calling additional operations.");
            systemPrompt.AppendLine("Think like a seasoned developer debugging a crash dump - when you see something suspicious, investigate it further.");
            systemPrompt.AppendLine();
            systemPrompt.AppendLine("⚠️  CRITICAL: When you want to investigate further, you MUST use the exact function call format below!");
            systemPrompt.AppendLine("Do NOT just mention operations in natural language - use the specific format or they won't be executed!");
            systemPrompt.AppendLine();
            systemPrompt.AppendLine("=== FUNCTION CALL FORMAT (MANDATORY) ===");
            systemPrompt.AppendLine("Use this EXACT format when calling functions:");
            systemPrompt.AppendLine("FUNCTION_CALL: OperationName(parameter1=\"value1\", parameter2=\"value2\")");
            systemPrompt.AppendLine("REASONING: Why you are calling this function and what you expect to find");
            systemPrompt.AppendLine();
            systemPrompt.AppendLine("EXAMPLES:");
            systemPrompt.AppendLine("FUNCTION_CALL: DumpObject(objectAddress=\"0x12345678\")");
            systemPrompt.AppendLine("REASONING: This object appears unusually large and may be causing memory pressure");
            systemPrompt.AppendLine();
            systemPrompt.AppendLine("FUNCTION_CALL: DumpException(objectAddress=\"0x87654321\")");
            systemPrompt.AppendLine("REASONING: Multiple exceptions detected, need to examine the exception details");
            systemPrompt.AppendLine();
            systemPrompt.AppendLine("WHEN TO CALL FUNCTIONS:");
            systemPrompt.AppendLine("- Large/unusual objects in heap → Call DumpObject to examine contents");
            systemPrompt.AppendLine("- Exceptions in results → Call DumpException for details");
            systemPrompt.AppendLine("- Suspicious stack frames → Call DumpSourceCode to see the actual code");
            systemPrompt.AppendLine("- High object counts → Call DumpTypeInfo to understand the type");
            systemPrompt.AppendLine("- Memory leaks suspected → Call GetObjectRoot to find references");
            systemPrompt.AppendLine("- Performance issues → Call DumpMethods to examine method details");
            systemPrompt.AppendLine();
            systemPrompt.AppendLine("INVESTIGATION STRATEGY:");
            systemPrompt.AppendLine("1. Start with broad analysis of the current data");
            systemPrompt.AppendLine("2. Identify the most suspicious/interesting findings");
            systemPrompt.AppendLine("3. Call appropriate functions using the EXACT format above");
            systemPrompt.AppendLine("4. Provide comprehensive diagnosis with root cause analysis");
            systemPrompt.AppendLine();
            systemPrompt.AppendLine(_functionRegistry.GetFunctionsAsPrompt());

            if (currentDepth > 0)
            {
                systemPrompt.AppendLine();
                systemPrompt.AppendLine($"=== FOLLOW-UP ANALYSIS (Depth {currentDepth + 1}) ===");
                systemPrompt.AppendLine("You are now analyzing additional data gathered from previous function calls.");
                systemPrompt.AppendLine("Focus on connecting the dots between different pieces of information.");
                systemPrompt.AppendLine("Provide a comprehensive diagnosis based on all available data.");
            }

            return systemPrompt.ToString();
        }

        private List<AIFunctionCall> ParseFunctionCalls(string aiResponse)
        {
            var functionCalls = new List<AIFunctionCall>();

            // Pattern to match function calls: FUNCTION_CALL: functionName(param1=value1, param2=value2)
            var pattern = @"FUNCTION_CALL:\s*(\w+)\((.*?)\)";
            var matches = Regex.Matches(aiResponse, pattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                try
                {
                    var functionName = match.Groups[1].Value;
                    var parametersStr = match.Groups[2].Value;
                    var parameters = ParseFunctionParameters(parametersStr);

                    functionCalls.Add(new AIFunctionCall
                    {
                        FunctionName = functionName,
                        Parameters = parameters,
                        Reasoning = "AI suggested function call based on analysis"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse function call from AI response");
                }
            }

            return functionCalls;
        }

        private List<AIFunctionCall> ParseEnhancedFunctionCalls(string aiResponse)
        {
            var functionCalls = new List<AIFunctionCall>();

            // Enhanced pattern to match function calls with reasoning
            var pattern = @"FUNCTION_CALL:\s*(\w+)\((.*?)\)\s*(?:REASONING:\s*(.*))?";
            var matches = Regex.Matches(aiResponse, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (Match match in matches)
            {
                try
                {
                    var functionName = match.Groups[1].Value;
                    var parametersStr = match.Groups[2].Value;
                    var reasoning = match.Groups[3].Success ? match.Groups[3].Value.Trim() : "AI suggested function call";
                    var parameters = ParseFunctionParameters(parametersStr);

                    functionCalls.Add(new AIFunctionCall
                    {
                        FunctionName = functionName,
                        Parameters = parameters,
                        Reasoning = reasoning
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse enhanced function call from AI response");
                }
            }

            // Fallback to simple parsing if enhanced parsing found nothing
            if (!functionCalls.Any())
            {
                return ParseFunctionCalls(aiResponse);
            }

            // If still no formal function calls found, try natural language parsing
            if (!functionCalls.Any())
            {
                return ParseNaturalLanguageFunctionCalls(aiResponse);
            }

            return functionCalls;
        }

        private Dictionary<string, object> ParseFunctionParameters(string parametersStr)
        {
            var parameters = new Dictionary<string, object>();

            if (string.IsNullOrWhiteSpace(parametersStr))
                return parameters;

            // Split by comma and parse key=value pairs
            var pairs = parametersStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim();
                    var value = keyValue[1].Trim().Trim('"', '\'');
                    parameters[key] = value;
                }
            }

            return parameters;
        }

        private List<AIFunctionCall> ParseNaturalLanguageFunctionCalls(string aiResponse)
        {
            var functionCalls = new List<AIFunctionCall>();

            // Define available operations that the AI might suggest
            var availableOperations = new[]
            {
                "DumpObject", "DumpObjectOperation",
                "GetObjectRoot", "GetObjectRootOperation", 
                "DumpTypeInfo", "DumpTypeInfoOperation",
                "DumpMethods", "DumpMethodsOperation",
                "DumpException", "DumpExceptionOperation", "DumpExceptions", "DumpExceptionsOperation",
                "DumpSourceCode", "DumpSourceCodeOperation",
                "DumpHeap", "DumpHeapOperation",
                "DumpClrStack", "DumpClrStackOperation",
                "DumpModules", "DumpModulesOperation",
                "GetObjectSize", "GetObjectSizeOperation"
            };

            foreach (var operation in availableOperations)
            {
                // Look for mentions of operations in various contexts
                var patterns = new[]
                {
                    $@"use\s+(?:the\s+)?`?{Regex.Escape(operation)}`?",
                    $@"call\s+(?:the\s+)?`?{Regex.Escape(operation)}`?",
                    $@"run\s+(?:the\s+)?`?{Regex.Escape(operation)}`?",
                    $@"execute\s+(?:the\s+)?`?{Regex.Escape(operation)}`?",
                    $@"`{Regex.Escape(operation)}`\s+(?:to|could|would)",
                    $@"could\s+use\s+(?:the\s+)?`?{Regex.Escape(operation)}`?"
                };

                foreach (var pattern in patterns)
                {
                    var matches = Regex.Matches(aiResponse, pattern, RegexOptions.IgnoreCase);
                    if (matches.Count > 0)
                    {
                        // Extract reasoning from the surrounding context
                        var match = matches[0];
                        var reasoning = ExtractReasoningFromContext(aiResponse, match.Index, operation);
                        
                        // Normalize operation name (remove "Operation" suffix if present)
                        var normalizedName = operation.EndsWith("Operation") 
                            ? operation.Substring(0, operation.Length - 9) 
                            : operation;

                        // Check if we already have this operation
                        if (!functionCalls.Any(fc => fc.FunctionName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase)))
                        {
                            functionCalls.Add(new AIFunctionCall
                            {
                                FunctionName = normalizedName,
                                Parameters = new Dictionary<string, object>(), // Will be populated with defaults
                                Reasoning = reasoning
                            });
                        }
                        break; // Found this operation, move to next
                    }
                }
            }

            _logger.LogInformation("Natural language parsing found {Count} function calls", functionCalls.Count);
            return functionCalls;
        }

        private string ExtractReasoningFromContext(string text, int matchIndex, string operation)
        {
            // Extract the sentence containing the operation mention
            var sentenceStart = text.LastIndexOf('.', Math.Max(0, matchIndex - 100));
            if (sentenceStart == -1) sentenceStart = Math.Max(0, matchIndex - 100);
            
            var sentenceEnd = text.IndexOf('.', matchIndex + operation.Length);
            if (sentenceEnd == -1) sentenceEnd = Math.Min(text.Length, matchIndex + operation.Length + 200);
            
            var sentence = text.Substring(sentenceStart, sentenceEnd - sentenceStart).Trim();
            
            // Clean up the sentence
            if (sentence.StartsWith('.')) sentence = sentence.Substring(1).Trim();
            if (sentence.Length > 200) sentence = sentence.Substring(0, 200) + "...";
            
            return string.IsNullOrWhiteSpace(sentence) 
                ? $"AI suggested using {operation} for further investigation" 
                : sentence;
        }

        private int CalculateMaxTokens(OperationContext context)
        {
            // Calculate max tokens based on context size and provider limits
            var baseTokens = 2000; // Base response tokens
            var contextTokens = context.EstimatedTokens;
            var totalAvailable = 128000; // Assume GPT-4 context window

            return Math.Min(baseTokens, totalAvailable - contextTokens - 1000); // Leave buffer
        }

        private int EstimateTokenCount(string text)
        {
            // Rough estimation: ~4 characters per token for English text
            return string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);
        }

        private string FormatFunctionResult(AIFunctionCallResult result)
        {
            if (!result.IsSuccess)
            {
                return $"Error: {result.ErrorMessage}";
            }

            if (result.Result == null)
            {
                return "No data returned";
            }

            // Handle different result types
            if (result.Result is System.Collections.IEnumerable enumerable && !(result.Result is string))
            {
                var items = enumerable.Cast<object>().Take(10).ToList(); // Limit display
                if (!items.Any())
                {
                    return "No items found";
                }

                var formatted = new StringBuilder();
                formatted.AppendLine($"Found {items.Count} items:");

                foreach (var item in items)
                {
                    formatted.AppendLine($"  • {FormatSingleItem(item)}");
                }

                return formatted.ToString();
            }

            return result.Result.ToString();
        }

        private string FormatSingleItem(object item)
        {
            if (item == null) return "null";

            try
            {
                // Get basic info about the object
                var type = item.GetType();
                var properties = type.GetProperties()
                    .Where(p => p.CanRead && IsImportantProperty(p.Name))
                    .Take(3)
                    .ToList();

                if (!properties.Any())
                {
                    return item.ToString();
                }

                var parts = new List<string>();
                foreach (var prop in properties)
                {
                    try
                    {
                        var value = prop.GetValue(item);
                        if (value != null)
                        {
                            parts.Add($"{prop.Name}: {FormatPropertyValue(value)}");
                        }
                    }
                    catch
                    {
                        // Ignore property access errors
                    }
                }

                return parts.Any() ? string.Join(", ", parts) : item.ToString();
            }
            catch
            {
                return item.ToString();
            }
        }

        private bool IsImportantProperty(string propertyName)
        {
            var important = new[] { "Type", "Size", "Address", "Value", "Name", "Message", "StackTrace" };
            return important.Any(p => propertyName.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        private string FormatPropertyValue(object value)
        {
            return value switch
            {
                ulong ul when ul > 0x1000 => $"0x{ul:X}",
                long l when l > 1000000 => $"{l:N0}",
                string str when str.Length > 50 => str.Substring(0, 50) + "...",
                _ => value.ToString()
            };
        }

        private OperationContext BuildFollowUpContext(
            OperationContext originalContext,
            List<(AIFunctionCall call, AIFunctionCallResult result)> functionResults)
        {
            var followUpPrompt = new StringBuilder();
            followUpPrompt.AppendLine("=== FOLLOW-UP INVESTIGATION ===");
            followUpPrompt.AppendLine("Based on the previous analysis, I investigated further by calling additional operations.");
            followUpPrompt.AppendLine("Here are the results of the automated investigation:");
            followUpPrompt.AppendLine();

            foreach (var (call, result) in functionResults.Where(fr => fr.result.IsSuccess))
            {
                followUpPrompt.AppendLine($"**{call.FunctionName}** (Reason: {call.Reasoning})");
                followUpPrompt.AppendLine(FormatFunctionResult(result));
                followUpPrompt.AppendLine();
            }

            followUpPrompt.AppendLine("Please provide a comprehensive analysis that connects all this information.");
            followUpPrompt.AppendLine("Focus on:");
            followUpPrompt.AppendLine("- Root cause analysis");
            followUpPrompt.AppendLine("- Actionable recommendations");
            followUpPrompt.AppendLine("- Priority of issues found");
            followUpPrompt.AppendLine("- Next steps for investigation");

            return new OperationContext
            {
                OperationName = originalContext.OperationName + "_FollowUp",
                SystemPrompt = originalContext.SystemPrompt,
                UserPrompt = followUpPrompt.ToString(),
                FormattedResults = originalContext.FormattedResults,
                Insights = originalContext.Insights,
                ConversationHistory = originalContext.ConversationHistory,
                EstimatedTokens = EstimateTokenCount(followUpPrompt.ToString()),
                CacheKey = originalContext.CacheKey + "_followup",
                Metadata = originalContext.Metadata
            };
        }
    }
}