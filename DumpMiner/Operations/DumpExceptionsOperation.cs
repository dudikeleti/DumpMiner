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

                // Enhanced exception analysis with inner exception support
                var exceptionResults = new List<object>();
                
                foreach (var obj in heap.EnumerateObjects())
                {
                    if (token.IsCancellationRequested) break;
                    
                    var type = heap.GetObjectType(obj);
                    if (type == null || !type.IsException) continue;
                    
                    var ex = heap.GetObject(obj).AsException();
                    var exceptionChain = new List<object>();
                    
                    // Process the main exception and all inner exceptions
                    var currentEx = ex;
                    var depth = 0;
                    
                    while (currentEx != null && depth < 10) // Limit depth to prevent infinite loops
                    {
                        var stackFrames = new List<object>();
                        
                        foreach (var frame in currentEx.StackTrace)
                        {
                            stackFrames.Add(new
                            {
                                Address = currentEx.Address,
                                ExceptionType = currentEx.Type.Name,
                                Message = currentEx.Message,
                                HResult = currentEx.HResult,
                                Depth = depth,
                                IsInnerException = depth > 0,
                                DisplayString = frame.ToString(),
                                InstructionPointer = frame.InstructionPointer,
                                StackPointer = frame.StackPointer,
                                Method = frame.Method,
                                Kind = frame.Kind,
                                ModuleName = frame.Method?.Type.Module.Name
                            });
                        }
                        
                        // If no stack frames, still add the exception info
                        if (!stackFrames.Any())
                        {
                            stackFrames.Add(new
                            {
                                Address = currentEx.Address,
                                ExceptionType = currentEx.Type.Name,
                                Message = currentEx.Message,
                                HResult = currentEx.HResult,
                                Depth = depth,
                                IsInnerException = depth > 0,
                                DisplayString = $"Exception: {currentEx.Type.Name}",
                                InstructionPointer = 0UL,
                                StackPointer = 0UL,
                                Method = (Microsoft.Diagnostics.Runtime.ClrMethod)null,
                                Kind = Microsoft.Diagnostics.Runtime.ClrStackFrameKind.Unknown,
                                ModuleName = currentEx.Type.Module?.Name
                            });
                        }
                        
                        exceptionChain.AddRange(stackFrames);
                        
                        // Move to inner exception using ClrMD 4.0 approach
                        currentEx = GetInnerException(currentEx);
                        depth++;
                    }
                    
                    if (exceptionChain.Any())
                    {
                        exceptionResults.Add(exceptionChain.GroupBy(e => 
                            new { 
                                Address = OperationHelpers.GetPropertyValue<ulong>(e, "Address", 0),
                                Depth = OperationHelpers.GetPropertyValue<int>(e, "Depth", 0)
                            }).ToList());
                    }
                }
                
                var enumerable = exceptionResults.SelectMany(group => 
                    group as IEnumerable<object> ?? new List<object>());

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
                // Extract exception info from the structured result
                var exceptionInfo = exGroup as IEnumerable<object>;
                if (exceptionInfo != null)
                {
                    foreach (var ex in exceptionInfo)
                    {
                        var exName = OperationHelpers.GetPropertyValue<string>(ex, "ExceptionType", "Unknown");
                        var hResult = OperationHelpers.GetPropertyValue<int>(ex, "HResult", 0);
                        var moduleName = OperationHelpers.GetPropertyValue<string>(ex, "ModuleName", "Unknown");
                        var isInner = OperationHelpers.GetPropertyValue<bool>(ex, "IsInnerException", false);

                        var key = isInner ? $"{exName} (Inner)" : exName;
                        exceptionTypes[key] = exceptionTypes.GetValueOrDefault(key, 0) + 1;
                        if (hResult != 0) hResults[hResult] = hResults.GetValueOrDefault(hResult, 0) + 1;
                        if (!string.IsNullOrEmpty(moduleName)) modules[moduleName] = modules.GetValueOrDefault(moduleName, 0) + 1;
                    }
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

        private Microsoft.Diagnostics.Runtime.ClrException GetInnerException(Microsoft.Diagnostics.Runtime.ClrException exception)
        {
            if (exception == null) return null;

            // Try to find inner exception field in ClrMD 4.0
            // Look for common inner exception field names
            var innerExceptionFieldNames = new[] { "_innerException", "inner_exception", "InnerException" };
            
            foreach (var fieldName in innerExceptionFieldNames)
            {
                try
                {
                    var innerField = exception.Type.GetFieldByName(fieldName);
                    if (innerField != null)
                    {
                        var innerObj = innerField.ReadObject(exception.Address, false);
                        if (innerObj.IsValid && innerObj.Type?.IsException == true)
                        {
                            return innerObj.AsException();
                        }
                    }
                }
                catch
                {
                    // Ignore errors and try next field name
                }
            }

            return null;
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
6. Inner exception depth - deep nesting may indicate cascading failures
7. Root cause analysis through inner exception chains
";
        }
    }
}
