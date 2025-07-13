using System.Collections.Generic;

namespace DumpMiner.Common
{
    /// <summary>
    /// Service for providing comprehensive help content for operations
    /// </summary>
    public interface IHelpService
    {
        /// <summary>
        /// Gets detailed help content for a specific operation
        /// </summary>
        /// <param name="operationName">The name of the operation</param>
        /// <returns>Detailed help content including usage examples and troubleshooting</returns>
        OperationHelpContent GetOperationHelp(string operationName);

        /// <summary>
        /// Gets all available operations with their help content
        /// </summary>
        /// <returns>Dictionary mapping operation names to their help content</returns>
        Dictionary<string, OperationHelpContent> GetAllOperationHelp();

        /// <summary>
        /// Searches for help content based on keywords
        /// </summary>
        /// <param name="keywords">Keywords to search for</param>
        /// <returns>List of matching help content</returns>
        List<OperationHelpContent> SearchHelp(string keywords);

        /// <summary>
        /// Gets getting started guide for new users
        /// </summary>
        /// <returns>Getting started content</returns>
        string GetGettingStartedGuide();

        /// <summary>
        /// Gets troubleshooting guide for common issues
        /// </summary>
        /// <returns>Troubleshooting content</returns>
        string GetTroubleshootingGuide();
    }

    /// <summary>
    /// Represents comprehensive help content for an operation
    /// </summary>
    public class OperationHelpContent
    {
        /// <summary>
        /// The operation name
        /// </summary>
        public string OperationName { get; set; }

        /// <summary>
        /// Display name for the operation
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Brief description of what the operation does
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// WinDbg command equivalent (if applicable)
        /// </summary>
        public string WinDbgEquivalent { get; set; }

        /// <summary>
        /// When to use this operation
        /// </summary>
        public string WhenToUse { get; set; }

        /// <summary>
        /// Required input parameters
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; }

        /// <summary>
        /// Step-by-step usage examples
        /// </summary>
        public List<UsageExample> Examples { get; set; }

        /// <summary>
        /// Common scenarios where this operation is helpful
        /// </summary>
        public List<string> CommonScenarios { get; set; }

        /// <summary>
        /// Output explanation - what the results mean
        /// </summary>
        public string OutputExplanation { get; set; }

        /// <summary>
        /// Common troubleshooting tips
        /// </summary>
        public List<string> TroubleshootingTips { get; set; }

        /// <summary>
        /// Related operations that work well together
        /// </summary>
        public List<string> RelatedOperations { get; set; }

        /// <summary>
        /// Performance considerations
        /// </summary>
        public string PerformanceNotes { get; set; }

        /// <summary>
        /// Best practices for using this operation
        /// </summary>
        public List<string> BestPractices { get; set; }

        public OperationHelpContent()
        {
            Parameters = new List<ParameterInfo>();
            Examples = new List<UsageExample>();
            CommonScenarios = new List<string>();
            TroubleshootingTips = new List<string>();
            RelatedOperations = new List<string>();
            BestPractices = new List<string>();
        }
    }

    /// <summary>
    /// Represents information about an operation parameter
    /// </summary>
    public class ParameterInfo
    {
        /// <summary>
        /// Parameter name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Parameter description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Whether the parameter is required
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Example values for the parameter
        /// </summary>
        public string ExampleValue { get; set; }

        /// <summary>
        /// Additional notes about the parameter
        /// </summary>
        public string Notes { get; set; }
    }

    /// <summary>
    /// Represents a usage example for an operation
    /// </summary>
    public class UsageExample
    {
        /// <summary>
        /// Title of the example
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Description of the scenario
        /// </summary>
        public string Scenario { get; set; }

        /// <summary>
        /// Step-by-step instructions
        /// </summary>
        public List<string> Steps { get; set; }

        /// <summary>
        /// Expected output or results
        /// </summary>
        public string ExpectedOutput { get; set; }

        /// <summary>
        /// Analysis tips for the results
        /// </summary>
        public string AnalysisTips { get; set; }

        public UsageExample()
        {
            Steps = new List<string>();
        }
    }
} 