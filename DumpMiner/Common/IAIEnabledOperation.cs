using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Models;
using DumpMiner.Services.AI.Orchestration;

namespace DumpMiner.Common
{
    /// <summary>
    /// Interface for operations that support AI integration
    /// </summary>
    public interface IAIEnabledOperation : IDebuggerOperation
    {
        /// <summary>
        /// Gets AI function definition for this operation
        /// </summary>
        AIFunctionDefinition GetAIFunctionDefinition();

        /// <summary>
        /// Gets operation-specific AI insights
        /// </summary>
        string GetAIInsights(Collection<object> operationResults);

        /// <summary>
        /// Gets operation-specific system prompt additions
        /// </summary>
        string GetSystemPromptAdditions();

        /// <summary>
        /// Gets human-readable description of the custom parameter for AI context
        /// </summary>
        string GetCustomParameterDescription(object customParameter);
    }
} 