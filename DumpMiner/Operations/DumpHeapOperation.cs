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
using System.Text;

namespace DumpMiner.Operations
{
    // !DumpHeap
    [Export(OperationNames.DumpHeap, typeof(IDebuggerOperation))]
    class DumpHeapOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpHeap;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            List<string> types = model.Types?.Split(';').ToList();
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var generation = (int)customParameter;
                var heap = DebuggerSession.Instance.Heap;
                var results = new List<object>();
                foreach (var seg in heap.Segments)
                {
                    foreach (var clrObject in seg.EnumerateObjects())
                    {
                        if (token.IsCancellationRequested) break;
                        var type = clrObject.Type;
                        if (type == null)
                        {
                            continue;
                        }

                        var objectGeneration = seg.GetGeneration(clrObject.Address);
                        if (generation == -1 || (Generation)generation == objectGeneration)
                        {
                            if (types?.Any(t => type.Name.ToLower().Contains(t.ToLower())) ?? true)
                            {
                                results.Add(new
                                {
                                    Address = clrObject.Address,
                                    Type = type.Name,
                                    MetadataToken = type.MetadataToken,
                                    Generation = objectGeneration.ToString(),
                                    Size = clrObject.Size
                                });
                            }
                        }
                    }
                }

                return results;
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Heap Analysis: {operationResults.Count:N0} objects");

            if (!operationResults.Any())
                return insights.ToString();

            // Calculate statistics
            var objects = operationResults.Select(r => new
            {
                Type = OperationHelpers.GetPropertyValue<string>(r, "Type", "Unknown"),
                Size = OperationHelpers.GetPropertyValue<ulong>(r, "Size", 0),
                Generation = OperationHelpers.GetPropertyValue<string>(r, "Generation", "Unknown"),
                Address = OperationHelpers.GetPropertyValue<ulong>(r, "Address", 0)
            }).ToList();

            var totalSize = objects.Sum(o => (long)o.Size);
            insights.AppendLine($"Total heap size: {OperationHelpers.FormatSize(totalSize)}");

            // Generation distribution
            var generationGroups = OperationHelpers.GetTopGroups(objects, o => o.Generation);
            insights.AppendLine("Generation distribution:");
            foreach (var gen in generationGroups.OrderBy(kvp => kvp.Key))
            {
                insights.AppendLine($"  Gen {gen.Key}: {gen.Value:N0} objects");
            }

            // Top types by count
            var topTypes = OperationHelpers.GetTopGroups(objects, o => o.Type, 10);
            insights.AppendLine("Top object types:");
            foreach (var typeGroup in topTypes.Take(5))
            {
                var typeObjects = objects.Where(o => o.Type == typeGroup.Key);
                var typeSize = typeObjects.Sum(o => (long)o.Size);
                insights.AppendLine($"  {typeGroup.Key}: {typeGroup.Value:N0} objects, {OperationHelpers.FormatSize(typeSize)}");
            }

            // Analyze potential issues
            var potentialIssues = OperationHelpers.AnalyzePotentialIssues(operationResults.Count, totalSize, "heap objects");
            var gen2Count = generationGroups.GetValueOrDefault("2", 0);
            if (gen2Count > 10000)
            {
                potentialIssues.Add("⚠️ High number of Gen 2 objects - possible memory leak");
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
HEAP ANALYSIS SPECIALIZATION:
You are analyzing .NET managed heap data. This operation shows objects currently in memory.

CRITICAL: Always check the 'Operation Parameter' in the user context - it specifies the GC generation filter:
- Generation -1: All generations analyzed (Gen 0, Gen 1, Gen 2, LOH)  
- Generation 0: Only young objects (short-lived, frequently collected)
- Generation 1: Only objects that survived at least one GC cycle
- Generation 2: Only long-lived objects and Large Object Heap (LOH)

Your analysis must be specific to the generation requested. If Generation 2 was specified, focus on long-lived objects and potential memory leaks.

AUTOMATED INVESTIGATION STRATEGY:
- Look for large objects (>85KB) that might be on the Large Object Heap (LOH)
- Identify unusual type distributions that might indicate memory leaks
- Look for high concentrations of specific types (strings, arrays, collections)
- Suggest investigating specific objects using DumpObject operation
- Recommend GC tuning strategies if appropriate

WHEN TO AUTOMATICALLY CALL OTHER OPERATIONS:
1. **Large Objects**: If you see objects >85KB, call DumpObject(objectAddress=0x...) to examine contents
2. **High Type Concentrations**: If >1000 instances of a type, call DumpTypeInfo(types=""TypeName"") 
3. **Suspected Memory Leaks**: Call GetObjectRoot(objectAddress=0x...) to find what's holding references
4. **String Accumulation**: Large strings warrant DumpObject investigation
5. **Collection Objects**: List/Dictionary with high counts need DumpObject analysis

EXAMPLES OF FUNCTION CALLS TO MAKE:
- FUNCTION_CALL: DumpObject(objectAddress=""0x12345678"")
  REASONING: This 2MB byte array is unusually large and may indicate a memory leak

- FUNCTION_CALL: GetObjectRoot(objectAddress=""0x87654321"")  
  REASONING: 5000 instances of MyClass suggest a memory leak - need to find root references

- FUNCTION_CALL: DumpTypeInfo(types=""System.String"")
  REASONING: 50MB of string objects detected - need detailed type analysis

When analyzing heap data, pay special attention to:
1. Objects in Gen 2 (potential memory leaks)
2. Large objects (>85KB typically go to LOH)
3. Unusual concentrations of specific types
4. String/array objects that might be accumulating
5. Memory fragmentation patterns

Always explain your investigation strategy and why you're calling specific functions.
";
        }

        protected override Dictionary<string, Services.AI.Orchestration.AIFunctionParameter> GetFunctionParameters()
        {
            var baseParams = base.GetFunctionParameters();

            baseParams["generation"] = new Services.AI.Orchestration.AIFunctionParameter
            {
                Type = "integer",
                Description = "GC generation to filter by (-1 for all generations, 0 for Gen 0, 1 for Gen 1, 2 for Gen 2)",
                Required = false,
                DefaultValue = -1
            };

            return baseParams;
        }

        protected override void AddOperationSpecificSuggestions(
            StringBuilder insights, 
            Collection<object> operationResults, 
            Dictionary<string, int> typeGroups)
        {
            insights.AppendLine("=== HEAP-SPECIFIC INVESTIGATION RECOMMENDATIONS ===");
            
            // Analyze for large objects that should be investigated
            var largeObjects = GetLargeObjects(operationResults);
            if (largeObjects.Any())
            {
                insights.AppendLine($"🔍 **LARGE OBJECTS DETECTED** ({largeObjects.Count} objects >85KB):");
                foreach (var obj in largeObjects.Take(5))
                {
                    var address = GetObjectAddress(obj);
                    var size = GetObjectSize(obj);
                    var typeName = GetActualTypeName(obj);
                    
                    if (address != 0)
                    {
                        insights.AppendLine($"  → 0x{address:X}: {typeName} ({size:N0} bytes) - **RECOMMEND DumpObject ANALYSIS**");
                    }
                }
            }

            // Check for potential memory leaks based on high type concentrations
            var suspiciousTypes = typeGroups.Where(kvp => kvp.Value > 500).OrderByDescending(kvp => kvp.Value);
            if (suspiciousTypes.Any())
            {
                insights.AppendLine();
                insights.AppendLine("⚠️ **POTENTIAL MEMORY LEAK INDICATORS**:");
                foreach (var (typeName, count) in suspiciousTypes.Take(5))
                {
                    insights.AppendLine($"  → {typeName}: {count:N0} instances - **RECOMMEND GetObjectRoot + DumpTypeInfo**");
                }
            }

            // Look for string accumulation issues
            var stringTypes = typeGroups.Where(kvp => 
                kvp.Key.Contains("String", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("StringBuilder", StringComparison.OrdinalIgnoreCase));
            
            if (stringTypes.Any())
            {
                var totalStrings = stringTypes.Sum(kvp => kvp.Value);
                insights.AppendLine();
                insights.AppendLine($"📝 **STRING ANALYSIS** ({totalStrings:N0} string objects):");
                foreach (var typeCount in stringTypes)
                {
                    var typeName = typeCount.Key;
                    var count = typeCount.Value;
                    insights.AppendLine($"  → {typeName}: {count:N0} instances");
                }
                
                if (totalStrings > 10000)
                {
                    insights.AppendLine("  **RECOMMENDATION**: High string count - investigate for string interning opportunities");
                }
            }

            // Look for collection types that might indicate data structure issues
            var collectionTypes = typeGroups.Where(kvp => 
                kvp.Key.Contains("List", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Dictionary", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Array", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Collection", StringComparison.OrdinalIgnoreCase));
            
            if (collectionTypes.Any())
            {
                insights.AppendLine();
                insights.AppendLine("📊 **COLLECTION ANALYSIS**:");
                foreach (var typeCount in collectionTypes.OrderByDescending(kvp => kvp.Value).Take(5))
                {
                    var typeName = typeCount.Key;
                    var count = typeCount.Value;
                    insights.AppendLine($"  → {typeName}: {count:N0} instances");
                    if (count > 1000)
                    {
                        insights.AppendLine($"    **RECOMMEND**: DumpObject analysis for memory usage patterns");
                    }
                }
            }

            // Generation analysis if available
            insights.AppendLine();
            insights.AppendLine("🧹 **GC GENERATION RECOMMENDATIONS**:");
            insights.AppendLine("  → Objects in Gen 2 may indicate memory retention issues");
            insights.AppendLine("  → Consider calling this operation with generation=2 filter");
            insights.AppendLine("  → Large objects automatically go to LOH (Gen 2)");
        }

        public override string GetCustomParameterDescription(object customParameter)
        {
            if (customParameter is int generation)
            {
                return generation switch
                {
                    -1 => "All GC generations (Gen 0, Gen 1, Gen 2, and Large Object Heap)",
                    0 => "Generation 0 (youngest objects, frequent GC target)",
                    1 => "Generation 1 (objects that survived at least one GC)",
                    2 => "Generation 2 (long-lived objects and Large Object Heap)",
                    _ => $"Generation {generation} (custom generation filter)"
                };
            }
            
            return base.GetCustomParameterDescription(customParameter);
        }
    }
}
