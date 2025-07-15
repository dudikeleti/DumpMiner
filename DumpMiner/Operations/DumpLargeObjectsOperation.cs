using System;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Operations.Shared;
using Microsoft.Diagnostics.Runtime;
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

            List<string> types = model.Types?.Split(';').ToList();
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                var results = new List<object>();
                var progressReporter = model.ProgressReporter;
                var startTime = DateTime.Now;

                // Phase 1: Initialize and enumerate segments
                progressReporter?.ReportPhase("Initializing", "Preparing heap segments for analysis");
                progressReporter?.ReportProgress(0, "Initializing analysis", $"Searching for objects larger than {FormatSize((long)size)}");

                var segmentsObjectsDictionary = new Dictionary<ClrSegment, IEnumerable<ClrObject>>();
                var segments = heap.Segments.ToList();
                var totalSegments = segments.Count;
                var currentSegment = 0;

                // Pre-enumerate objects for better progress tracking
                foreach (var segment in segments)
                {
                    currentSegment++;
                    progressReporter?.ReportProgress((currentSegment * 10) / totalSegments,
                        $"Enumerating segment {currentSegment} of {totalSegments}",
                        "Preparing objects for analysis");

                    segmentsObjectsDictionary[segment] = segment.EnumerateObjects().ToList();
                }

                // Phase 2: Analyze objects in each segment
                progressReporter?.ReportPhase("Analyzing Objects", "Searching for large objects");

                var totalObjectsProcessed = 0L;
                var totalObjectsFound = segmentsObjectsDictionary.Values.Sum(objs => objs.Count());
                currentSegment = 0;

                foreach (var kvp in segmentsObjectsDictionary)
                {
                    var seg = kvp.Key;
                    var segmentObjects = kvp.Value.ToList();
                    currentSegment++;

                    var segmentObjectCount = 0;
                    var totalSegmentObjects = segmentObjects.Count;

                    progressReporter?.ReportProgress(10 + (currentSegment * 80) / totalSegments,
                        $"Segment {currentSegment} of {totalSegments}",
                        $"Analyzing {totalSegmentObjects:N0} objects in segment");

                    foreach (var obj in segmentObjects)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        segmentObjectCount++;
                        totalObjectsProcessed++;

                        // Report progress every 5000 objects for better responsiveness
                        if (segmentObjectCount % 5000 == 0)
                        {
                            var overallProgress = 10 + (int)((totalObjectsProcessed * 80.0) / totalObjectsFound);
                            progressReporter?.ReportProgress(overallProgress,
                                $"Analyzing {totalObjectsProcessed:N0} of {totalObjectsFound:N0} objects",
                                $"Segment {currentSegment}: {segmentObjectCount:N0}/{totalSegmentObjects:N0} objects, {results.Count:N0} large objects found");
                        }

                        var type = heap.GetObjectType(obj);
                        if (type == null)
                            continue;

                        if (types?.Any(t => type.Name.ToLower().Contains(t.ToLower())) ?? true)
                        {
                            // Calculate object size directly using ClrMD for much better performance
                            ulong objectSize = obj.Size;

                            // For more accurate size calculation, include referenced objects if needed, GetObjectSizeOperation does recursive analysis
                            if (objectSize >= size)
                            {
                                results.Add(new { Address = obj.Address, Type = type.Name, Generation = seg.GetGeneration(obj), Size = objectSize });
                            }
                        }
                    }
                }

                // Final completion reporting
                var processingTime = DateTime.Now - startTime;
                progressReporter?.ReportCompleted(totalObjectsProcessed, processingTime);

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
