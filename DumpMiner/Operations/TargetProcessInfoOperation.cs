using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Operations.Shared;

namespace DumpMiner.Operations
{
    [Export(OperationNames.TargetProcessInfo, typeof(IDebuggerOperation))]
    class TargetProcessInfoOperation : BaseAIOperation
    {
        public override string Name => OperationNames.TargetProcessInfo;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var runtime = DebuggerSession.Instance.Runtime;
                var infoModel = new TargetProcessInfoOperationModel();
                infoModel.AppDomains = string.Concat(runtime.AppDomains.Select(ad => ad.Name + ", ")).TrimEnd();
                infoModel.AppDomains = infoModel.AppDomains.Remove(infoModel.AppDomains.Length - 1, 1);
                infoModel.AppDomainsCount = runtime.AppDomains.Length;
                infoModel.ThreadsCount = runtime.Threads.Length;
                infoModel.ModulesCount = runtime.AppDomains.Sum(appDomain => appDomain.Modules.Length);
                //infoModel.SymbolPath = runtime.DataTarget.;
                infoModel.ClrVersions = string.Concat(runtime.DataTarget.ClrVersions.Select(clrVer => clrVer.Version + ", ")).TrimEnd();
                infoModel.ClrVersions = infoModel.ClrVersions.Remove(infoModel.ClrVersions.Length - 1, 1);
                //infoModel.DacInfo = string.Concat(runtime.DataTarget.ClrVersions.Select(ver => ver.Dac.FileName + ", ")).TrimEnd();
                //infoModel.DacInfo = infoModel.DacInfo.Remove(infoModel.DacInfo.Length - 1, 1);
                infoModel.DacInfo = string.Join(";", runtime.DataTarget.ClrVersions.Select(ver => string.Join(",", ver.DebuggingLibraries.Select(dl => dl.FileName))));
                infoModel.Architecture = runtime.DataTarget.DataReader.Architecture.ToString();
                infoModel.IsGcServer = runtime.Heap.IsServer;
                infoModel.HeapCount = runtime.Heap.Segments.Length;
                infoModel.CreatedTime = DebuggerSession.Instance.AttachedTime?.ToUniversalTime().ToString("G");
                infoModel.PointerSize = runtime.DataTarget.DataReader.PointerSize;

                var enumerable = from prop in infoModel.GetType().GetProperties()
                                 select new { Name = prop.Name, Value = prop.GetValue(infoModel) };
                return enumerable.ToList();
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Process Information: {operationResults.Count} properties");

            if (!operationResults.Any()) 
                return insights.ToString();

            // Get key process information
            var properties = operationResults.Cast<object>().ToDictionary(
                p => OperationHelpers.GetPropertyValue<string>(p, "Name", "Unknown"),
                p => OperationHelpers.GetPropertyValue(p, "Value")?.ToString() ?? "null"
            );

            // Extract key metrics
            var threadsCount = properties.GetValueOrDefault("ThreadsCount", "0");
            var appDomainsCount = properties.GetValueOrDefault("AppDomainsCount", "0");
            var modulesCount = properties.GetValueOrDefault("ModulesCount", "0");
            var architecture = properties.GetValueOrDefault("Architecture", "Unknown");
            var isGcServer = properties.GetValueOrDefault("IsGcServer", "false");
            var heapCount = properties.GetValueOrDefault("HeapCount", "0");

            insights.AppendLine($"Architecture: {architecture}");
            insights.AppendLine($"Threads: {threadsCount}");
            insights.AppendLine($"AppDomains: {appDomainsCount}");
            insights.AppendLine($"Modules: {modulesCount}");
            insights.AppendLine($"GC Mode: {(isGcServer.ToLower() == "true" ? "Server" : "Workstation")}");
            insights.AppendLine($"Heap Segments: {heapCount}");

            // Analyze for potential issues
            var potentialIssues = new List<string>();
            if (int.TryParse(threadsCount, out var threadCount) && threadCount > 200)
            {
                potentialIssues.Add("⚠️ High thread count - may indicate thread pool exhaustion");
            }

            if (int.TryParse(modulesCount, out var moduleCount) && moduleCount > 500)
            {
                potentialIssues.Add("⚠️ High module count - check for assembly loading issues");
            }

            if (potentialIssues.Any())
            {
                insights.AppendLine("\nPotential Issues:");
                foreach (var issue in potentialIssues)
                {
                    insights.AppendLine($"  {issue}");
                }
            }

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
PROCESS INFORMATION ANALYSIS SPECIALIZATION:
- Focus on overall process health and configuration
- Identify resource usage patterns and potential bottlenecks
- Analyze threading, memory, and module loading patterns
- Provide high-level process diagnostics

When analyzing process information, pay attention to:
1. Thread count for potential threading issues
2. AppDomain count for application architecture
3. Module count for assembly loading patterns
4. GC configuration (Server vs Workstation)
5. Architecture and CLR version compatibility
";
        }
    }
}
