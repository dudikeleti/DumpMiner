using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Operations.Shared;
using Microsoft.Diagnostics.Runtime;
using ClrObject = Microsoft.Diagnostics.Runtime.ClrObject;

namespace DumpMiner.Operations
{
    [Export(OperationNames.MemoryLeakDetection, typeof(IDebuggerOperation))]
    public class MemoryLeakDetectionOperation : BaseAIOperation
    {
        public override string Name => OperationNames.MemoryLeakDetection;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var leakDetectionResults = new List<MemoryLeakInfo>();

                // 1. Analyze heap statistics for abnormal patterns
                var heapStats = AnalyzeHeapStatistics(token);

                // 2. Detect reference cycles that might prevent GC
                var referenceCycles = DetectReferenceCycles(token);

                // 3. Identify objects with unusually high reference counts
                var highRefCountObjects = IdentifyHighReferenceCountObjects(token);

                // 4. Analyze generation patterns for suspected leaks
                var generationAnomalies = AnalyzeGenerationPatterns(token);

                // 5. Detect common leak patterns (event handlers, static references, etc.)
                var commonLeakPatterns = DetectCommonLeakPatterns(token);

                // Combine all findings into comprehensive leak analysis
                leakDetectionResults.AddRange(heapStats);
                leakDetectionResults.AddRange(referenceCycles);
                leakDetectionResults.AddRange(highRefCountObjects);
                leakDetectionResults.AddRange(generationAnomalies);
                leakDetectionResults.AddRange(commonLeakPatterns);

                return leakDetectionResults.OrderByDescending(l => l.SeverityScore).ToList();
            });
        }

        private List<MemoryLeakInfo> AnalyzeHeapStatistics(CancellationToken token)
        {
            var leaks = new List<MemoryLeakInfo>();
            var heap = DebuggerSession.Instance.Heap;
            var typeStats = new Dictionary<string, TypeStatistics>();

            // Collect statistics for each type
            foreach (var seg in heap.Segments)
            {
                foreach (var obj in seg.EnumerateObjects())
                {
                    if (token.IsCancellationRequested) break;

                    var type = obj.Type;
                    if (type == null) continue;

                    var typeName = type.Name;
                    if (!typeStats.ContainsKey(typeName))
                    {
                        typeStats[typeName] = new TypeStatistics { TypeName = typeName };
                    }

                    var stats = typeStats[typeName];
                    stats.InstanceCount++;
                    stats.TotalSize += (long)obj.Size;
                    stats.Generation = seg.GetGeneration(obj);

                    if (obj.Size > 85000) // Large object heap threshold
                    {
                        stats.LargeObjectCount++;
                    }
                }
            }

            // Analyze for potential leaks
            foreach (var stats in typeStats.Values)
            {
                var severityScore = CalculateLeakSeverity(stats);
                if (severityScore > 0.5) // Threshold for potential leak
                {
                    leaks.Add(new MemoryLeakInfo
                    {
                        LeakType = MemoryLeakType.HighTypeCount,
                        TypeName = stats.TypeName,
                        InstanceCount = stats.InstanceCount,
                        TotalSize = stats.TotalSize,
                        SeverityScore = severityScore,
                        Description = $"High instance count for type {stats.TypeName}: {stats.InstanceCount:N0} objects consuming {OperationHelpers.FormatSize(stats.TotalSize)}",
                        Recommendation = GetRecommendationForType(stats),
                        Evidence = new List<string>
                        {
                            $"Instance count: {stats.InstanceCount:N0}",
                            $"Total memory: {OperationHelpers.FormatSize(stats.TotalSize)}",
                            $"Average size: {stats.TotalSize / Math.Max(1, stats.InstanceCount):N0} bytes",
                            $"Generation: {stats.Generation}",
                            $"Large objects: {stats.LargeObjectCount}"
                        }
                    });
                }
            }

            return leaks;
        }

        private List<MemoryLeakInfo> DetectReferenceCycles(CancellationToken token)
        {
            var leaks = new List<MemoryLeakInfo>();
            var heap = DebuggerSession.Instance.Heap;
            var visited = new HashSet<ulong>();
            var cycleDetector = new ReferenceGraphAnalyzer();

            // Sample objects to check for cycles (checking all would be too expensive)
            var sampleObjects = heap.EnumerateObjects()
                .Where(obj => obj.Type != null && !obj.Type.IsString)
                .Take(10000)
                .ToList();

            foreach (var obj in sampleObjects)
            {
                if (token.IsCancellationRequested) break;
                if (visited.Contains(obj.Address)) continue;

                var cycles = cycleDetector.FindCycles(obj, visited, token);
                foreach (var cycle in cycles)
                {
                    leaks.Add(new MemoryLeakInfo
                    {
                        LeakType = MemoryLeakType.ReferenceCycle,
                        ObjectAddress = cycle.StartAddress,
                        TypeName = cycle.TypeName,
                        SeverityScore = 0.8, // Cycles are serious
                        Description = $"Reference cycle detected involving {cycle.TypeName}",
                        Recommendation = "Investigate circular references and consider using weak references",
                        Evidence = cycle.CyclePath.Select(addr => $"0x{addr:X}").ToList()
                    });
                }
            }

            return leaks;
        }

        private List<MemoryLeakInfo> IdentifyHighReferenceCountObjects(CancellationToken token)
        {
            var leaks = new List<MemoryLeakInfo>();
            var heap = DebuggerSession.Instance.Heap;
            var referenceCounter = new Dictionary<ulong, int>();

            // Count references to each object
            foreach (var obj in heap.EnumerateObjects())
            {
                if (token.IsCancellationRequested) break;

                foreach (var refAddr in obj.EnumerateReferences(false))
                {
                    referenceCounter[refAddr] = referenceCounter.GetValueOrDefault(refAddr, 0) + 1;
                }
            }

            // Find objects with unusually high reference counts
            var highRefObjects = referenceCounter
                .Where(kvp => kvp.Value > 100) // Threshold for high reference count
                .OrderByDescending(kvp => kvp.Value)
                .Take(50);

            foreach (var kvp in highRefObjects)
            {
                var obj = heap.GetObject(kvp.Key);
                if (obj.Type == null) continue;

                leaks.Add(new MemoryLeakInfo
                {
                    LeakType = MemoryLeakType.HighReferenceCount,
                    ObjectAddress = kvp.Key,
                    TypeName = obj.Type.Name,
                    ReferenceCount = kvp.Value,
                    SeverityScore = Math.Min(1.0, kvp.Value / 1000.0),
                    Description = $"Object at 0x{kvp.Key:X} has {kvp.Value} references",
                    Recommendation = "Investigate why this object is referenced so many times",
                    Evidence = new List<string>
                    {
                        $"Reference count: {kvp.Value}",
                        $"Object type: {obj.Type.Name}",
                        $"Object size: {obj.Size} bytes"
                    }
                });
            }

            return leaks;
        }

        private List<MemoryLeakInfo> AnalyzeGenerationPatterns(CancellationToken token)
        {
            var leaks = new List<MemoryLeakInfo>();
            var heap = DebuggerSession.Instance.Heap;
            var generationStats = new Dictionary<string, GenerationStatistics>();

            foreach (var seg in heap.Segments)
            {
                foreach (var obj in seg.EnumerateObjects())
                {
                    if (token.IsCancellationRequested) break;

                    var type = obj.Type;
                    if (type == null) continue;

                    var typeName = type.Name;
                    if (!generationStats.ContainsKey(typeName))
                    {
                        generationStats[typeName] = new GenerationStatistics { TypeName = typeName };
                    }

                    var stats = generationStats[typeName];
                    var generation = seg.GetGeneration(obj);

                    switch (generation)
                    {
                        case Generation.Generation0: stats.Gen0Count++; break;
                        case Generation.Generation1: stats.Gen1Count++; break;
                        case Generation.Generation2: stats.Gen2Count++; break;
                        case Generation.Large: stats.LohCount++; break;
                        case Generation.Pinned: stats.PinnedCount++; break;
                        default: break;
                    }
                }
            }

            // Identify types with unusual generation patterns
            foreach (var stats in generationStats.Values)
            {
                var totalObjects = stats.Gen0Count + stats.Gen1Count + stats.Gen2Count + stats.LohCount;
                if (totalObjects < 100) continue; // Ignore low-count types

                var gen2Percentage = (double)stats.Gen2Count / totalObjects;
                if (gen2Percentage > 0.8) // Most objects in Gen 2 suggests they're not being collected
                {
                    leaks.Add(new MemoryLeakInfo
                    {
                        LeakType = MemoryLeakType.GenerationAnomaly,
                        TypeName = stats.TypeName,
                        InstanceCount = totalObjects,
                        SeverityScore = gen2Percentage,
                        Description = $"Type {stats.TypeName} has {gen2Percentage:P0} of objects in Gen 2",
                        Recommendation = "Investigate why objects are surviving garbage collection",
                        Evidence = new List<string>
                        {
                            $"Gen 0: {stats.Gen0Count:N0} objects",
                            $"Gen 1: {stats.Gen1Count:N0} objects",
                            $"Gen 2: {stats.Gen2Count:N0} objects ({gen2Percentage:P0})",
                            $"LOH: {stats.LohCount:N0} objects"
                        }
                    });
                }
            }

            return leaks;
        }

        private List<MemoryLeakInfo> DetectCommonLeakPatterns(CancellationToken token)
        {
            var leaks = new List<MemoryLeakInfo>();
            var heap = DebuggerSession.Instance.Heap;

            // 1. Event handler leaks
            var eventHandlerLeaks = DetectEventHandlerLeaks(heap, token);
            leaks.AddRange(eventHandlerLeaks);

            // 2. Static reference leaks
            var staticReferenceLeaks = DetectStaticReferenceLeaks(heap, token);
            leaks.AddRange(staticReferenceLeaks);

            // 3. Timer leaks
            var timerLeaks = DetectTimerLeaks(heap, token);
            leaks.AddRange(timerLeaks);

            // 4. Collection leaks (Lists, Dictionaries that keep growing)
            var collectionLeaks = DetectCollectionLeaks(heap, token);
            leaks.AddRange(collectionLeaks);

            return leaks;
        }

        private List<MemoryLeakInfo> DetectEventHandlerLeaks(ClrHeap heap, CancellationToken token)
        {
            var leaks = new List<MemoryLeakInfo>();

            // Look for delegate types that might indicate event handler leaks
            var delegateObjects = heap.EnumerateObjects()
                .Where(obj => obj.Type?.Name?.Contains("Delegate") == true ||
                             obj.Type?.Name?.Contains("EventHandler") == true)
                .ToList();

            if (delegateObjects.Count > 1000) // Threshold for potential leak
            {
                leaks.Add(new MemoryLeakInfo
                {
                    LeakType = MemoryLeakType.EventHandlerLeak,
                    TypeName = "Delegate/EventHandler",
                    InstanceCount = delegateObjects.Count,
                    SeverityScore = Math.Min(1.0, delegateObjects.Count / 10000.0),
                    Description = $"High number of delegate objects: {delegateObjects.Count:N0}",
                    Recommendation = "Check for event handler leaks - ensure events are unsubscribed",
                    Evidence = new List<string>
                    {
                        $"Delegate objects: {delegateObjects.Count:N0}",
                        "Common cause: Event handlers not unsubscribed"
                    }
                });
            }

            return leaks;
        }

        private List<MemoryLeakInfo> DetectStaticReferenceLeaks(ClrHeap heap, CancellationToken token)
        {
            var leaks = new List<MemoryLeakInfo>();
            var runtime = DebuggerSession.Instance.Runtime;

            // Analyze static references
            var staticFieldCount = 0;
            var staticFieldSize = 0L;

            foreach (var appDomain in runtime.AppDomains)
            {
                foreach (var module in appDomain.Modules)
                {
                    foreach (var type in module.EnumerateTypes())
                    {
                        if (token.IsCancellationRequested) break;

                        foreach (var field in type.StaticFields)
                        {
                            staticFieldCount++;
                            try
                            {
                                var fieldValue = field.ReadObject(appDomain);
                                if (fieldValue.IsValid)
                                {
                                    staticFieldSize += (long)fieldValue.Size;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }

            if (staticFieldSize > 50_000_000) // 50MB threshold
            {
                leaks.Add(new MemoryLeakInfo
                {
                    LeakType = MemoryLeakType.StaticReferenceLeak,
                    TypeName = "Static Fields",
                    TotalSize = staticFieldSize,
                    InstanceCount = staticFieldCount,
                    SeverityScore = Math.Min(1.0, staticFieldSize / 100_000_000.0),
                    Description = $"Large amount of memory held by static fields: {OperationHelpers.FormatSize(staticFieldSize)}",
                    Recommendation = "Review static field usage and consider using weak references",
                    Evidence = new List<string>
                    {
                        $"Static field count: {staticFieldCount:N0}",
                        $"Static field memory: {OperationHelpers.FormatSize(staticFieldSize)}"
                    }
                });
            }

            return leaks;
        }

        private List<MemoryLeakInfo> DetectTimerLeaks(ClrHeap heap, CancellationToken token)
        {
            var leaks = new List<MemoryLeakInfo>();

            var timerObjects = heap.EnumerateObjects()
                .Where(obj => obj.Type?.Name?.Contains("Timer") == true)
                .ToList();

            if (timerObjects.Count > 50) // Threshold for potential timer leak
            {
                leaks.Add(new MemoryLeakInfo
                {
                    LeakType = MemoryLeakType.TimerLeak,
                    TypeName = "Timer",
                    InstanceCount = timerObjects.Count,
                    SeverityScore = Math.Min(1.0, timerObjects.Count / 200.0),
                    Description = $"High number of timer objects: {timerObjects.Count:N0}",
                    Recommendation = "Ensure timers are properly disposed when no longer needed",
                    Evidence = new List<string>
                    {
                        $"Timer objects: {timerObjects.Count:N0}",
                        "Common cause: Timers not disposed"
                    }
                });
            }

            return leaks;
        }

        private List<MemoryLeakInfo> DetectCollectionLeaks(ClrHeap heap, CancellationToken token)
        {
            var leaks = new List<MemoryLeakInfo>();
            var largeCollections = new List<(ClrObject obj, int count)>();

            foreach (var obj in heap.EnumerateObjects())
            {
                if (token.IsCancellationRequested) break;

                var type = obj.Type;
                if (type == null) continue;

                var typeName = type.Name;
                if (typeName.Contains("List`1") || typeName.Contains("Dictionary`2") ||
                    typeName.Contains("HashSet`1") || typeName.Contains("Queue`1") ||
                    typeName.Contains("Stack`1"))
                {
                    // Try to get the collection count
                    var count = GetCollectionCount(obj);
                    if (count > 10000) // Large collection threshold
                    {
                        largeCollections.Add((obj, count));
                    }
                }
            }

            // Group by type and report largest collections
            var collectionGroups = largeCollections
                .GroupBy(x => x.obj.Type.Name)
                .OrderByDescending(g => g.Sum(x => x.count))
                .Take(10);

            foreach (var group in collectionGroups)
            {
                var totalCount = group.Sum(x => x.count);
                var instances = group.Count();

                leaks.Add(new MemoryLeakInfo
                {
                    LeakType = MemoryLeakType.CollectionLeak,
                    TypeName = group.Key,
                    InstanceCount = instances,
                    TotalSize = group.Sum(x => (long)x.obj.Size),
                    SeverityScore = Math.Min(1.0, totalCount / 100000.0),
                    Description = $"Large collections of type {group.Key}: {instances} instances with {totalCount:N0} total items",
                    Recommendation = "Review collection usage patterns and consider cleanup strategies",
                    Evidence = new List<string>
                    {
                        $"Collection instances: {instances:N0}",
                        $"Total items: {totalCount:N0}",
                        $"Average items per collection: {totalCount / Math.Max(1, instances):N0}"
                    }
                });
            }

            return leaks;
        }

        private int GetCollectionCount(ClrObject obj)
        {
            try
            {
                // Try to get count from common collection types
                var sizeField = obj.Type.GetFieldByName("_count") ??
                               obj.Type.GetFieldByName("_size") ??
                               obj.Type.GetFieldByName("Count") ??
                               obj.Type.GetFieldByName("Size");

                if (sizeField != null)
                {
                    var value = sizeField.ReadObject(obj.Address, false);
                    if (value.IsValid && value.Type.IsValueType)
                    {
                        return int.Parse(value.ToString());
                    }
                }
            }
            catch { }

            return 0;
        }

        private double CalculateLeakSeverity(TypeStatistics stats)
        {
            double score = 0;

            // High instance count
            if (stats.InstanceCount > 100000) score += 0.4;
            else if (stats.InstanceCount > 10000) score += 0.2;

            // Large memory usage
            if (stats.TotalSize > 100_000_000) score += 0.3; // 100MB
            else if (stats.TotalSize > 10_000_000) score += 0.2; // 10MB

            // Generation analysis
            if (stats.Generation == Generation.Generation2) score += 0.2; // Gen 2 objects are more concerning

            // Large objects
            if (stats.LargeObjectCount > 0) score += 0.1;

            return Math.Min(1.0, score);
        }

        private string GetRecommendationForType(TypeStatistics stats)
        {
            var recommendations = new List<string>();

            if (stats.InstanceCount > 50000)
                recommendations.Add("Consider object pooling or reducing object creation");

            if (stats.TotalSize > 50_000_000)
                recommendations.Add("Review memory usage patterns for this type");

            if (stats.Generation == Generation.Generation2)
                recommendations.Add("Investigate why objects are surviving garbage collection");

            if (stats.LargeObjectCount > 0)
                recommendations.Add("Review large object usage to avoid LOH pressure");

            return recommendations.Any() ? string.Join("; ", recommendations) : "Monitor this type for memory usage patterns";
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new StringBuilder();
            insights.AppendLine($"Memory Leak Detection Analysis: {operationResults.Count} potential issues identified");

            if (!operationResults.Any())
            {
                insights.AppendLine("✅ No significant memory leak patterns detected");
                return insights.ToString();
            }

            var leaks = operationResults.Cast<MemoryLeakInfo>().ToList();

            // Summary by leak type
            var leaksByType = leaks.GroupBy(l => l.LeakType).ToDictionary(g => g.Key, g => g.Count());
            insights.AppendLine("\nLeak Types Found:");
            foreach (var kvp in leaksByType.OrderByDescending(kvp => kvp.Value))
            {
                insights.AppendLine($"  {kvp.Key}: {kvp.Value} issues");
            }

            // Top severity issues
            var topIssues = leaks.OrderByDescending(l => l.SeverityScore).Take(5);
            insights.AppendLine("\nTop Severity Issues:");
            foreach (var issue in topIssues)
            {
                insights.AppendLine($"  🔴 {issue.TypeName}: {issue.Description} (Severity: {issue.SeverityScore:F2})");
            }

            // Memory impact
            var totalLeakedMemory = leaks.Sum(l => l.TotalSize);
            var totalLeakedObjects = leaks.Sum(l => l.InstanceCount);
            insights.AppendLine($"\nEstimated Impact:");
            insights.AppendLine($"  Total leaked memory: {OperationHelpers.FormatSize(totalLeakedMemory)}");
            insights.AppendLine($"  Total leaked objects: {totalLeakedObjects:N0}");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
MEMORY LEAK DETECTION SPECIALIZATION:
You are analyzing sophisticated memory leak detection results. This operation uses advanced algorithms to identify potential memory leaks.

LEAK TYPES EXPLAINED:
- HighTypeCount: Unusually high instance counts for specific types
- ReferenceCycle: Circular references preventing garbage collection
- HighReferenceCount: Objects referenced by many other objects
- GenerationAnomaly: Objects stuck in Gen 2 (not being collected)
- EventHandlerLeak: Accumulation of delegate/event handler objects
- StaticReferenceLeak: Large memory held by static fields
- TimerLeak: Accumulation of timer objects
- CollectionLeak: Large collections that keep growing

ANALYSIS APPROACH:
1. Prioritize high-severity leaks (SeverityScore > 0.8)
2. Look for patterns across multiple leak types
3. Focus on types with large memory impact
4. Consider generation patterns for GC analysis
5. Provide specific, actionable recommendations

AUTOMATED INVESTIGATION STRATEGY:
When you see potential leaks, automatically suggest:
- DumpObject for specific addresses
- GetObjectRoot for reference chain analysis
- DumpTypeInfo for detailed type analysis
- ReferenceGraphAnalysis for complex reference patterns

Always explain the technical implications and provide clear remediation steps.
";
        }
    }

    // Supporting classes
    public class MemoryLeakInfo
    {
        public MemoryLeakType LeakType { get; set; }
        public string TypeName { get; set; }
        public ulong ObjectAddress { get; set; }
        public int InstanceCount { get; set; }
        public long TotalSize { get; set; }
        public int ReferenceCount { get; set; }
        public double SeverityScore { get; set; }
        public string Description { get; set; }
        public string Recommendation { get; set; }
        public List<string> Evidence { get; set; } = new();
    }

    public enum MemoryLeakType
    {
        HighTypeCount,
        ReferenceCycle,
        HighReferenceCount,
        GenerationAnomaly,
        EventHandlerLeak,
        StaticReferenceLeak,
        TimerLeak,
        CollectionLeak
    }

    public class TypeStatistics
    {
        public string TypeName { get; set; }
        public int InstanceCount { get; set; }
        public long TotalSize { get; set; }
        public Generation Generation { get; set; }
        public int LargeObjectCount { get; set; }
    }

    public class GenerationStatistics
    {
        public string TypeName { get; set; }
        public int Gen0Count { get; set; }
        public int Gen1Count { get; set; }
        public int Gen2Count { get; set; }
        public int LohCount { get; set; }
        public int PinnedCount { get; set; }
    }

    public class ReferenceGraphAnalyzer
    {
        public List<ReferenceCycle> FindCycles(ClrObject startObject, HashSet<ulong> globalVisited, CancellationToken token)
        {
            var cycles = new List<ReferenceCycle>();
            var visited = new HashSet<ulong>();
            var path = new List<ulong>();

            FindCyclesRecursive(startObject, visited, globalVisited, path, cycles, token);
            return cycles;
        }

        private void FindCyclesRecursive(ClrObject obj, HashSet<ulong> visited, HashSet<ulong> globalVisited,
            List<ulong> path, List<ReferenceCycle> cycles, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            if (globalVisited.Contains(obj.Address)) return;

            if (visited.Contains(obj.Address))
            {
                // Found a cycle
                var cycleStart = path.IndexOf(obj.Address);
                if (cycleStart >= 0)
                {
                    cycles.Add(new ReferenceCycle
                    {
                        StartAddress = obj.Address,
                        TypeName = obj.Type?.Name ?? "Unknown",
                        CyclePath = path.Skip(cycleStart).ToList()
                    });
                }
                return;
            }

            visited.Add(obj.Address);
            globalVisited.Add(obj.Address);
            path.Add(obj.Address);

            // Follow references (limit depth to avoid infinite recursion)
            if (path.Count < 20)
            {
                foreach (var refAddr in obj.EnumerateReferences(false))
                {
                    if (token.IsCancellationRequested) break;

                    var refObj = obj.Type.Heap.GetObject(refAddr);
                    if (refObj.IsValid)
                    {
                        FindCyclesRecursive(refObj, visited, globalVisited, path, cycles, token);
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            visited.Remove(obj.Address);
        }
    }

    public class ReferenceCycle
    {
        public ulong StartAddress { get; set; }
        public string TypeName { get; set; }
        public List<ulong> CyclePath { get; set; } = new();
    }
}