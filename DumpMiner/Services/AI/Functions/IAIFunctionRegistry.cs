using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Models;
using DumpMiner.Services.AI.Orchestration;
using System.Linq;

namespace DumpMiner.Services.AI.Functions
{
    /// <summary>
    /// Registry for AI-callable functions (operations)
    /// </summary>
    public interface IAIFunctionRegistry
    {
        /// <summary>
        /// Registers an operation as an AI function
        /// </summary>
        void RegisterFunction(string operationName, AIFunctionDefinition definition);

        /// <summary>
        /// Gets all available AI functions
        /// </summary>
        IEnumerable<AIFunctionDefinition> GetAvailableFunctions();

        /// <summary>
        /// Gets a specific function definition
        /// </summary>
        AIFunctionDefinition GetFunction(string functionName);

        /// <summary>
        /// Executes an AI function call
        /// </summary>
        Task<AIFunctionCallResult> ExecuteFunctionAsync(
            AIFunctionCall functionCall,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates function call parameters
        /// </summary>
        bool ValidateFunctionCall(AIFunctionCall functionCall, out string validationError);

        /// <summary>
        /// Gets function definitions formatted for AI consumption
        /// </summary>
        string GetFunctionsAsPrompt();
    }

    /// <summary>
    /// Implementation of AI function registry
    /// </summary>
    public class AIFunctionRegistry : IAIFunctionRegistry
    {
        private readonly Dictionary<string, AIFunctionDefinition> _functions = new();
        private readonly Dictionary<string, IDebuggerOperation> _operations = new();

        public void RegisterFunction(string operationName, AIFunctionDefinition definition)
        {
            _functions[operationName] = definition;
            
            try
            {
                var operation = App.Container?.GetExportedValue<IDebuggerOperation>(operationName);
                if (operation != null)
                {
                    _operations[operationName] = operation;
                }
            }
            catch
            {
                // Operation not found, skip registration
            }
        }

        public IEnumerable<AIFunctionDefinition> GetAvailableFunctions()
        {
            return _functions.Values;
        }

        public AIFunctionDefinition GetFunction(string functionName)
        {
            _functions.TryGetValue(functionName, out var definition);
            return definition;
        }

        public async Task<AIFunctionCallResult> ExecuteFunctionAsync(
            AIFunctionCall functionCall,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_operations.TryGetValue(functionCall.FunctionName, out var operation))
                {
                    return new AIFunctionCallResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Operation '{functionCall.FunctionName}' not found",
                        OperationName = functionCall.FunctionName
                    };
                }

                // Convert parameters to OperationModel with enhanced parsing
                var operationModel = new OperationModel();
                
                if (functionCall.Parameters.TryGetValue("objectAddress", out var objAddr))
                {
                    var addressValue = ParseHexAddress(objAddr.ToString());
                    if (addressValue.HasValue)
                    {
                        operationModel.ObjectAddress = addressValue.Value;
                    }
                }

                if (functionCall.Parameters.TryGetValue("types", out var types))
                {
                    operationModel.Types = types.ToString();
                }

                if (functionCall.Parameters.TryGetValue("numOfResults", out var numResults))
                {
                    if (int.TryParse(numResults.ToString(), out var num))
                        operationModel.NumOfResults = Math.Max(1, Math.Min(num, 1000)); // Limit to reasonable range
                }

                // Set default values if not specified
                if (operationModel.NumOfResults <= 0)
                {
                    operationModel.NumOfResults = 100; // Default limit for AI function calls
                }

                // Execute the operation
                var customParameter = functionCall.Parameters.TryGetValue("customParameter", out var customParam) 
                    ? customParam 
                    : null;

                var result = await operation.Execute(operationModel, cancellationToken, customParameter);

                // Limit results to prevent overwhelming output
                var limitedResult = LimitResults(result, operationModel.NumOfResults);

                return new AIFunctionCallResult
                {
                    IsSuccess = true,
                    Result = limitedResult,
                    OperationName = functionCall.FunctionName
                };
            }
            catch (Exception ex)
            {
                return new AIFunctionCallResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    OperationName = functionCall.FunctionName
                };
            }
        }

        private ulong? ParseHexAddress(string addressStr)
        {
            if (string.IsNullOrWhiteSpace(addressStr))
                return null;

            // Handle various hex formats: 0x12345678, 12345678, 0X12345678
            var cleanAddress = addressStr.Trim();
            if (cleanAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                cleanAddress = cleanAddress.Substring(2);
            }

            if (ulong.TryParse(cleanAddress, System.Globalization.NumberStyles.HexNumber, null, out var result))
            {
                return result;
            }

            // Try decimal parsing as fallback
            if (ulong.TryParse(addressStr, out var decimalResult))
            {
                return decimalResult;
            }

            return null;
        }

        private IEnumerable<object> LimitResults(IEnumerable<object> results, int maxResults)
        {
            if (results == null)
                return Enumerable.Empty<object>();

            return results.Take(maxResults);
        }

        public bool ValidateFunctionCall(AIFunctionCall functionCall, out string validationError)
        {
            validationError = string.Empty;

            if (!_functions.TryGetValue(functionCall.FunctionName, out var definition))
            {
                validationError = $"Function '{functionCall.FunctionName}' not found";
                return false;
            }

            // Validate required parameters
            foreach (var param in definition.Parameters)
            {
                if (param.Value.Required && !functionCall.Parameters.ContainsKey(param.Key))
                {
                    validationError = $"Required parameter '{param.Key}' is missing";
                    return false;
                }
            }

            return true;
        }

        public string GetFunctionsAsPrompt()
        {
            var prompt = new System.Text.StringBuilder();
            prompt.AppendLine("=== AVAILABLE FUNCTIONS ===");
            prompt.AppendLine("You can call the following debugging operations:");
            prompt.AppendLine();

            foreach (var function in _functions.Values)
            {
                prompt.AppendLine($"Function: {function.Name}");
                prompt.AppendLine($"Description: {function.Description}");
                prompt.AppendLine("Parameters:");
                
                foreach (var param in function.Parameters)
                {
                    var required = param.Value.Required ? "(required)" : "(optional)";
                    prompt.AppendLine($"  - {param.Key} ({param.Value.Type}) {required}: {param.Value.Description}");
                }
                prompt.AppendLine();
            }

            prompt.AppendLine("To call a function, use the format:");
            prompt.AppendLine("FUNCTION_CALL: functionName(parameter1=value1, parameter2=value2)");
            prompt.AppendLine();

            return prompt.ToString();
        }
    }
} 