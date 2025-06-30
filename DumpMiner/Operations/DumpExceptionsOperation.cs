using System;
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
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpExceptions, typeof(IDebuggerOperation))]
    class DumpExceptionsOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpExceptions;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;

                //TODO: Add support of inner exceptions
                //var heap = DebuggerSession.Instance.Heap;
                var enumerable = from obj in heap.EnumerateObjects()
                                 let type = heap.GetObjectType(obj)
                                 where type != null && type.IsException
                                 let ex = heap.GetObject(obj).AsException()
                                 from frame in ex.StackTrace
                                 let o = new
                                 {
                                     Address = ex.Address,
                                     Name = ex.Type.Name,
                                     Message = ex.Message,
                                     HResult = ex.HResult,
                                     DisplayString = frame.ToString(),
                                     InstructionPointer = frame.InstructionPointer,
                                     StackPointer = frame.StackPointer,
                                     Method = frame.Method,
                                     Kind = frame.Kind,
                                     ModuleName = frame.Method?.Type.Module.Name
                                 }
                                 group o by o.Address;

                var results = new List<object>();
                foreach (var item in enumerable)
                {
                    results.Add(item);
                    if (token.IsCancellationRequested)
                        break;
                }

                return results;
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Exception Analysis: {operationResults.Count} exception groups");

            if (!operationResults.Any()) 
            {
                insights.AppendLine("✅ No exceptions found - application appears stable");
                return insights.ToString();
            }

            // Group exceptions by type
            var exceptionTypes = new Dictionary<string, int>();
            var modules = new Dictionary<string, int>();
            var hResults = new Dictionary<int, int>();

            foreach (var exGroup in operationResults)
            {
                // Extract first item from group to get exception info
                var firstEx = OperationHelpers.GetPropertyValue(exGroup, "First");
                if (firstEx != null)
                {
                    var exName = OperationHelpers.GetPropertyValue<string>(firstEx, "Name", "Unknown");
                    var hResult = OperationHelpers.GetPropertyValue<int>(firstEx, "HResult", 0);
                    var moduleName = OperationHelpers.GetPropertyValue<string>(firstEx, "ModuleName", "Unknown");

                    exceptionTypes[exName] = exceptionTypes.GetValueOrDefault(exName, 0) + 1;
                    if (hResult != 0) hResults[hResult] = hResults.GetValueOrDefault(hResult, 0) + 1;
                    if (!string.IsNullOrEmpty(moduleName)) modules[moduleName] = modules.GetValueOrDefault(moduleName, 0) + 1;
                }
            }

            insights.AppendLine("Exception types:");
            foreach (var exType in exceptionTypes.OrderByDescending(kvp => kvp.Value).Take(5))
            {
                insights.AppendLine($"  {exType.Key}: {exType.Value} instances");
            }

            if (modules.Count > 0)
            {
                insights.AppendLine("\nTop modules with exceptions:");
                foreach (var module in modules.OrderByDescending(kvp => kvp.Value).Take(3))
                {
                    insights.AppendLine($"  {module.Key}: {module.Value} exceptions");
                }
            }

            // Analyze potential issues
            var potentialIssues = OperationHelpers.AnalyzePotentialIssues(operationResults.Count, 0, "exceptions");
            
            if (exceptionTypes.ContainsKey("System.OutOfMemoryException"))
                potentialIssues.Add("🚨 OutOfMemoryException detected - critical memory issue");
            if (exceptionTypes.ContainsKey("System.StackOverflowException"))
                potentialIssues.Add("🚨 StackOverflowException detected - infinite recursion likely");
            if (exceptionTypes.ContainsKey("System.AccessViolationException"))
                potentialIssues.Add("🚨 AccessViolationException detected - memory corruption possible");

            if (potentialIssues.Count > 0)
            {
                insights.AppendLine("\nPotential Issues:");
                foreach (var issue in potentialIssues)
                {
                    insights.AppendLine($"  {issue}");
                }
            }

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Check exception messages for root causes");
            insights.AppendLine("- Use DumpClrStack to see where exceptions occurred");
            insights.AppendLine("- Investigate modules with high exception counts");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
EXCEPTION ANALYSIS SPECIALIZATION:
- Focus on exception patterns and root cause analysis
- Identify critical exceptions like OutOfMemoryException, StackOverflowException
- Analyze exception frequency and distribution across modules
- Look for cascading failure patterns

When analyzing exception data, pay attention to:
1. Critical system exceptions that indicate severe issues
2. Exception frequency patterns (single vs repeated)
3. Module correlation - which modules throw most exceptions
4. Stack trace patterns for debugging guidance
5. Exception chaining and inner exception relationships
";
        }
    }
}
