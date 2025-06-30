using System;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Operations.Shared;
using Microsoft.Diagnostics.Runtime;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClrObject = Microsoft.Diagnostics.Runtime.ClrObject;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpLargeObjects, typeof(IDebuggerOperation))]
    class DumpLargeObjectsOperation : BaseAIOperation
    {
        //object lockObject = new object();
        public override string Name => OperationNames.DumpLargeObjects;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            ulong size;
            if (!ulong.TryParse(customParameter.ToString(), out size))
                return null;

            var operation = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.GetObjectSize);
            if (operation == null)
                return null;

            List<string> types = model.Types?.Split(';').ToList();
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                var results = new List<object>();

                var segmentsObjectsDictionary = new Dictionary<ClrSegment, IEnumerable<ClrObject>>();
                foreach (var segment in heap.Segments)
                {
                    segmentsObjectsDictionary[segment] = segment.EnumerateObjects().AsParallel();
                }

                foreach (var kvp in segmentsObjectsDictionary)
                {
                    var seg = kvp.Key;
                    foreach (var obj in kvp.Value)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var type = heap.GetObjectType(obj);
                        if (type == null)
                            continue;

                        if (types?.Any(t => type.Name.ToLower().Contains(t.ToLower())) ?? true)
                        {
                            dynamic result = operation.Execute(new OperationModel { ObjectAddress = obj }, token, null).Result.FirstOrDefault();
                            if (result == null || result.TotalSize < size)
                                continue;
                            results.Add(new { Address = obj, Type = type.Name, Generation = seg.GetGeneration(obj), Size = result.TotalSize });
                        }   
                    }
                }

                //It will not work properly because in the end i must be serial because the debugger operation must run on the same thread that attach to dump\process
                //It will work if we are inspecting a dump file and the dump reader is ClrMD
                //Parallel.ForEach(
                //    // The values to be aggregated 
                //    heapObjects,

                //    // The local initial partial result
                //    () => new List<object>(),

                //    // The loop body
                //    (obj, loopState, partialResult) =>
                //    {
                //        if (token.IsCancellationRequested)
                //            return partialResult;

                //        var type = heap.GetObjectType(obj);
                //        dynamic result = operation.Execute(new OperationModel { ObjectAddress = obj }, token, null).Result.FirstOrDefault();
                //        if (result != null && result.TotalSize >= size)
                //            partialResult.Add(new { Address = obj, Type = type.Name, Generation = heap.GetGeneration(obj), Size = result.TotalSize });
                //        return partialResult;
                //    },

                //    // The final step of each local context            
                //    (localPartialSum) =>
                //    {
                //        // Enforce serial access to single, shared result
                //        lock (lockObject)
                //        {
                //            results.AddRange(localPartialSum);
                //        }
                //    });

                
                return results;
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Large Objects Analysis: {operationResults.Count} large objects");

            if (!operationResults.Any()) 
            {
                insights.AppendLine("✅ No large objects found above specified threshold");
                return insights.ToString();
            }

            // Calculate statistics
            var objects = operationResults.Select(r => new 
            { 
                Type = OperationHelpers.GetPropertyValue<string>(r, "Type", "Unknown"),
                Size = OperationHelpers.GetPropertyValue<ulong>(r, "Size", 0),
                Generation = OperationHelpers.GetPropertyValue<string>(r, "Generation", "Unknown"),
                Address = OperationHelpers.GetPropertyValue<ulong>(r, "Address", 0)
            }).ToList();

            var totalSize = objects.Sum(o => (long)o.Size);
            insights.AppendLine($"Total size of large objects: {OperationHelpers.FormatSize(totalSize)}");

            // Generation distribution
            var generationGroups = OperationHelpers.GetTopGroups(objects, o => o.Generation);
            insights.AppendLine("Generation distribution:");
            foreach (var gen in generationGroups.OrderBy(kvp => kvp.Key))
            {
                var genObjects = objects.Where(o => o.Generation == gen.Key);
                var genSize = genObjects.Sum(o => (long)o.Size);
                insights.AppendLine($"  Gen {gen.Key}: {gen.Value:N0} objects, {OperationHelpers.FormatSize(genSize)}");
            }

            // Top types by size
            var topTypes = objects.GroupBy(o => o.Type)
                .Select(g => new { Type = g.Key, Count = g.Count(), TotalSize = g.Sum(o => (long)o.Size) })
                .OrderByDescending(t => t.TotalSize)
                .Take(5);

            insights.AppendLine("Top large object types:");
            foreach (var typeGroup in topTypes)
            {
                insights.AppendLine($"  {typeGroup.Type}: {typeGroup.Count:N0} objects, {OperationHelpers.FormatSize(typeGroup.TotalSize)}");
            }

            // Analyze potential issues
            var potentialIssues = OperationHelpers.AnalyzePotentialIssues(operationResults.Count, totalSize, "large objects");
            var lohObjects = generationGroups.GetValueOrDefault("2", 0); // Gen 2 often contains LOH objects
            if (lohObjects > 100)
            {
                potentialIssues.Add("⚠️ Many objects in Gen 2 - likely Large Object Heap pressure");
            }

            if (potentialIssues.Any())
            {
                insights.AppendLine("\nPotential Issues:");
                foreach (var issue in potentialIssues)
                {
                    insights.AppendLine($"  {issue}");
                }
            }

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Large objects (>85KB) go to Large Object Heap (LOH)");
            insights.AppendLine("- LOH is only collected during Gen 2 GC");
            insights.AppendLine("- Consider using ArrayPool for large arrays");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
LARGE OBJECTS ANALYSIS SPECIALIZATION:
- Focus on Large Object Heap (LOH) impact and memory pressure
- Identify objects >85KB that affect GC performance
- Analyze generation distribution patterns for large objects
- Look for optimization opportunities with large allocations

When analyzing large object data, pay attention to:
1. Objects in Gen 2 (Large Object Heap candidates)
2. Array types that could use ArrayPool optimization
3. String concatenation patterns creating large strings
4. Byte arrays and buffers that might be reusable
5. Memory fragmentation caused by large allocations
";
        }

        protected override Dictionary<string, Services.AI.Orchestration.AIFunctionParameter> GetFunctionParameters()
        {
            var baseParams = base.GetFunctionParameters();
            
            baseParams["sizeThreshold"] = new Services.AI.Orchestration.AIFunctionParameter
            {
                Type = "integer",
                Description = "Minimum size threshold in bytes for objects to be considered large",
                Required = false,
                DefaultValue = 85000 // LOH threshold
            };

            return baseParams;
        }

        public override string GetCustomParameterDescription(object customParameter)
        {
            if (customParameter is int or ulong or long)
            {
                var sizeBytes = Convert.ToInt64(customParameter);
                if (sizeBytes <= 0)
                    return "Size threshold: Default (85KB - Large Object Heap threshold)";
                
                return $"Size threshold: {sizeBytes:N0} bytes ({FormatSize(sizeBytes)})";
            }
            
            return base.GetCustomParameterDescription(customParameter);
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            else if (bytes >= 1024)
                return $"{bytes / 1024.0:F1} KB";
            else
                return $"{bytes} bytes";
        }
    }
}
