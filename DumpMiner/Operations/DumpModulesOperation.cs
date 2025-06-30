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
    [Export(OperationNames.DumpModules, typeof(IDebuggerOperation))]
    class DumpModulesOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpModules;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            List<string> types = model.Types?.Split(';').ToList();
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var enumerable = from appDomain in DebuggerSession.Instance.Runtime.AppDomains
                                 from module in appDomain.Modules
                                 let name = module.Name
                                 where !string.IsNullOrEmpty(name) && (types == null || types.Any(t => name.ToLower().Contains(t.ToLower())))
                                 select new
                                 {
                                     Name = name.Substring(name.LastIndexOf('\\') + 1),
                                     MetadataAddress = module.MetadataAddress,
                                     ImageBase = module.ImageBase,
                                     FilePath = name.Substring(0, name.LastIndexOf('\\')),
                                     Size = module.Size,
                                     IsDynamic = module.IsDynamic,
                                     IsOptimized = module.IsOptimized(),
                                 };
                return enumerable.ToList();
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Module Analysis: {operationResults.Count} loaded modules");

            if (!operationResults.Any()) 
                return insights.ToString();

            // Calculate total size
            var modules = operationResults.Select(r => new 
            { 
                Name = OperationHelpers.GetPropertyValue<string>(r, "Name", "Unknown"),
                Size = OperationHelpers.GetPropertyValue<ulong>(r, "Size", 0),
                IsDynamic = OperationHelpers.GetPropertyValue<bool>(r, "IsDynamic", false),
                FilePath = OperationHelpers.GetPropertyValue<string>(r, "FilePath", "")
            }).ToList();

            var totalSize = modules.Sum(m => (long)m.Size);
            var dynamicCount = modules.Count(m => m.IsDynamic);

            insights.AppendLine($"Total module size: {OperationHelpers.FormatSize(totalSize)}");
            insights.AppendLine($"Dynamic modules: {dynamicCount}");

            // Group by file path (assembly location)
            var pathGroups = OperationHelpers.GetTopGroups(modules, m => m.FilePath, 5);
            if (pathGroups.Any())
            {
                insights.AppendLine("Top module locations:");
                foreach (var pathGroup in pathGroups)
                {
                    insights.AppendLine($"  {pathGroup.Key}: {pathGroup.Value} modules");
                }
            }

            if (dynamicCount > 50)
            {
                insights.AppendLine("⚠️ High number of dynamic modules - may indicate dynamic code generation");
            }

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
MODULE ANALYSIS SPECIALIZATION:
- Focus on loaded assemblies and their characteristics
- Identify unusual module loading patterns
- Analyze dynamic code generation scenarios
- Look for version conflicts or duplicate assemblies

When analyzing module data, pay attention to:
1. Dynamic modules (JIT compilation, reflection emit)
2. Module sizes and memory usage
3. Assembly loading patterns
4. Potential version conflicts
5. Security or trust level issues
";
        }
    }
}