using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Operations.Shared;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpMethods, typeof(IDebuggerOperation))]
    class DumpMethodsOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpMethods;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                ClrType type = DebuggerSession.Instance.Heap.GetTypeByName(model.Types) ??
                               DebuggerSession.Instance.Heap.GetObjectType(model.ObjectAddress) ??
                               DebuggerSession.Instance.Heap.GetTypeByMethodTable(model.ObjectAddress);
                
                if (type == null)
                {
                    return new List<object>();
                }

                var enumerable = from method in type.Methods
                                 where method != null
                                 select new
                                 {
                                     MetadataToken = method.MetadataToken,
                                     Signature = method.Signature,
                                     CompilationType = method.CompilationType,
                                     IsStatic = method.Attributes | MethodAttributes.Static,
                                     MethodDesc = method.MethodDesc
                                 };
                return enumerable.ToList();
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Method Analysis: {operationResults.Count} methods");

            if (!operationResults.Any()) 
            {
                insights.AppendLine("⚠️ No methods found - type may not exist or address invalid");
                return insights.ToString();
            }

            var methods = operationResults.Select(r => new 
            { 
                Signature = OperationHelpers.GetPropertyValue<string>(r, "Signature", "Unknown"),
                CompilationType = OperationHelpers.GetPropertyValue(r, "CompilationType")?.ToString() ?? "Unknown",
                IsStatic = OperationHelpers.GetPropertyValue<int>(r, "IsStatic", 0) != 0
            }).ToList();

            // Compilation type distribution
            var compilationGroups = OperationHelpers.GetTopGroups(methods, m => m.CompilationType);
            if (compilationGroups.Any())
            {
                insights.AppendLine("Compilation types:");
                foreach (var compType in compilationGroups)
                {
                    insights.AppendLine($"  {compType.Key}: {compType.Value} methods");
                }
            }

            var staticCount = methods.Count(m => m.IsStatic);
            var instanceCount = methods.Count - staticCount;
            insights.AppendLine($"Static methods: {staticCount}");
            insights.AppendLine($"Instance methods: {instanceCount}");

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Use DumpDisassemblyMethod to see method implementation");
            insights.AppendLine("- Use DumpSourceCode to view original source if available");
            insights.AppendLine("- Check CompilationType for JIT optimization level");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
METHOD ANALYSIS SPECIALIZATION:
- Focus on method compilation and optimization patterns
- Identify methods that might be performance bottlenecks
- Analyze JIT compilation states and optimization levels
- Look for unusual method patterns or signatures

When analyzing method data, pay attention to:
1. Compilation types (JITted, Interpreted, etc.)
2. Method signatures that seem unusual
3. Static vs instance method distribution
4. Methods that might benefit from optimization
";
        }
    }
}
