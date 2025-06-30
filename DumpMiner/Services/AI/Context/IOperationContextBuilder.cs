using System.Collections.Generic;
using System.Collections.ObjectModel;
using DumpMiner.Models;

namespace DumpMiner.Services.AI.Context
{
    /// <summary>
    /// Builds intelligent context for AI analysis from operation results
    /// </summary>
    public interface IOperationContextBuilder
    {
        /// <summary>
        /// Builds comprehensive context for AI analysis
        /// </summary>
        OperationContext BuildContext(
            string operationName,
            OperationModel operationModel,
            Collection<object> operationResults,
            string userQuery = null);

        /// <summary>
        /// Gets the system prompt for a specific operation
        /// </summary>
        string GetSystemPrompt(string operationName);

        /// <summary>
        /// Formats operation results for AI consumption
        /// </summary>
        string FormatResultsForAI(
            string operationName,
            Collection<object> operationResults,
            int maxTokens = 40000);

        /// <summary>
        /// Extracts key insights from operation results
        /// </summary>
        OperationInsights ExtractInsights(
            string operationName,
            Collection<object> operationResults);
    }

    /// <summary>
    /// Comprehensive context for AI analysis
    /// </summary>
    public class OperationContext
    {
        public string OperationName { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string UserPrompt { get; set; } = string.Empty;
        public string FormattedResults { get; set; } = string.Empty;
        public OperationInsights Insights { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public List<ConversationMessage> ConversationHistory { get; set; } = new();
        public int EstimatedTokens { get; set; }
        public string CacheKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// Key insights extracted from operation results
    /// </summary>
    public class OperationInsights
    {
        public int TotalItems { get; set; }
        public List<string> ItemTypes { get; set; } = new();
        public Dictionary<string, int> TypeCounts { get; set; } = new();
        public List<string> KeyFindings { get; set; } = new();
        public List<string> PotentialIssues { get; set; } = new();
        public Dictionary<string, object> Statistics { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
    }
} 