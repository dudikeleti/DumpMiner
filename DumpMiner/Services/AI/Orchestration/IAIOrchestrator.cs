using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Models;

namespace DumpMiner.Services.AI.Orchestration
{
    /// <summary>
    /// Core AI orchestrator that manages all AI interactions across operations
    /// </summary>
    public interface IAIOrchestrator
    {
        /// <summary>
        /// Analyzes operation results with full context awareness
        /// </summary>
        Task<AIAnalysisResult> AnalyzeOperationAsync(
            string operationName,
            OperationModel operationModel,
            Collection<object> operationResults,
            string userQuery = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes AI-driven function calls (calling other operations)
        /// </summary>
        Task<AIFunctionCallResult> ExecuteFunctionCallAsync(
            AIFunctionCall functionCall,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets conversation history for an operation
        /// </summary>
        Task<List<ConversationMessage>> GetConversationHistoryAsync(
            string operationName,
            OperationModel operationModel);

        /// <summary>
        /// Clears conversation history for an operation
        /// </summary>
        Task ClearConversationHistoryAsync(
            string operationName,
            OperationModel operationModel);

        /// <summary>
        /// Checks if AI services are available
        /// </summary>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Gets available AI function calls for the current context
        /// </summary>
        IEnumerable<AIFunctionDefinition> GetAvailableFunctions();
    }

    /// <summary>
    /// Result of AI analysis
    /// </summary>
    public class AIAnalysisResult
    {
        public string Content { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<AIFunctionCall> SuggestedFunctionCalls { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool FromCache { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// AI function call definition
    /// </summary>
    public class AIFunctionCall
    {
        public string FunctionName { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string Reasoning { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of AI function call execution
    /// </summary>
    public class AIFunctionCallResult
    {
        public bool IsSuccess { get; set; }
        public object Result { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
    }

    /// <summary>
    /// AI function definition for operations
    /// </summary>
    public class AIFunctionDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, AIFunctionParameter> Parameters { get; set; } = new();
        public string OperationName { get; set; } = string.Empty;
    }

    /// <summary>
    /// AI function parameter definition
    /// </summary>
    public class AIFunctionParameter
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Required { get; set; }
        public object DefaultValue { get; set; }
    }
} 