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
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DeadlockDetection, typeof(IDebuggerOperation))]
    public class DeadlockDetectionOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DeadlockDetection;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var deadlockResults = new List<DeadlockInfo>();
                var runtime = DebuggerSession.Instance.Runtime;

                // 1. Analyze thread states for blocking patterns
                var threadAnalysis = AnalyzeThreadStates(runtime, token);

                // 2. Build lock ownership graph
                var lockGraph = BuildLockOwnershipGraph(runtime, token);

                // 3. Detect circular wait conditions
                var circularWaits = DetectCircularWaits(lockGraph, threadAnalysis, token);

                // 4. Acccccbkdnuluhvkffldkjghficedgbrblfgvuufulede
                // nalyze sync blocks for contention
                var syncBlockAnalysis = AnalyzeSyncBlockContention(runtime, token);

                // 5. Detect common deadlock patterns
                var commonPatterns = DetectCommonDeadlockPatterns(runtime, threadAnalysis, token);

                deadlockResults.AddRange(circularWaits);
                deadlockResults.AddRange(syncBlockAnalysis);
                deadlockResults.AddRange(commonPatterns);

                return deadlockResults.OrderByDescending(d => d.SeverityScore).ToList();
            });
        }

        private List<ThreadAnalysisInfo> AnalyzeThreadStates(ClrRuntime runtime, CancellationToken token)
        {
            var threadAnalysis = new List<ThreadAnalysisInfo>();

            foreach (var thread in runtime.Threads)
            {
                if (token.IsCancellationRequested) break;

                var analysis = new ThreadAnalysisInfo
                {
                    ManagedThreadId = thread.ManagedThreadId,
                    OSThreadId = thread.OSThreadId,
                    IsAlive = thread.IsAlive,
                    IsBackground = false, // Not available in CLRMD 4.0
                    IsGCThread = thread.IsGc,
                    StackTrace = GetThreadStackTrace(thread),
                    BlockingObject = 0, // Simplified for CLRMD 4.0
                    ThreadState = AnalyzeThreadState(thread)
                };

                // Use our custom blocking detection
                var blockingObjects = thread.GetBlockingObjects().ToList();
                analysis.IsBlocked = blockingObjects.Any();
                analysis.BlockingObjects = blockingObjects.Select(bo => new Operations.BlockingObjectInfo
                {
                    Address = bo.Address,
                    Type = bo.Type,
                    Reason = bo.Reason,
                    HasSingleOwner = bo.HasSingleOwner,
                    Owner = bo.Owner,
                    Waiters = bo.Waiters
                }).ToList();

                threadAnalysis.Add(analysis);
            }

            return threadAnalysis;
        }

        private LockOwnershipGraph BuildLockOwnershipGraph(ClrRuntime runtime, CancellationToken token)
        {
            var graph = new LockOwnershipGraph();
            var heap = runtime.Heap;

            // In CLRMD 4.0, sync blocks are accessed differently
            // We'll build the lock ownership graph from blocking objects instead
            var processedObjects = new HashSet<ulong>();

            foreach (var thread in runtime.Threads)
            {
                if (token.IsCancellationRequested) break;

                foreach (var blockingObj in thread.GetBlockingObjects())
                {
                    if (processedObjects.Contains(blockingObj.Address))
                        continue;

                    processedObjects.Add(blockingObj.Address);

                    var lockInfo = new LockInfo
                    {
                        SyncBlockIndex = 0, // Not available in CLRMD 4.0
                        ObjectAddress = blockingObj.Address,
                        ObjectType = blockingObj.Address != 0 ? heap.GetObjectType(blockingObj.Address)?.Name ?? "Unknown" : "Unknown",
                        IsHeld = blockingObj.HasSingleOwner,
                        HoldingThread = blockingObj.Owner,
                        MonitorHeldCount = 1, // Simplified for CLRMD 4.0
                        WaitingThreads = new List<int>()
                    };

                    // Find all threads waiting on this object
                    foreach (var waitingThread in runtime.Threads)
                    {
                        foreach (var waitingBlockingObj in waitingThread.GetBlockingObjects())
                        {
                            if (waitingBlockingObj.Address == blockingObj.Address && waitingThread.ManagedThreadId != lockInfo.HoldingThread)
                            {
                                lockInfo.WaitingThreads.Add(waitingThread.ManagedThreadId);
                            }
                        }
                    }

                    graph.Locks.Add(blockingObj.Address, lockInfo);
                }
            }

            // Build thread-to-lock relationships
            foreach (var thread in runtime.Threads)
            {
                if (token.IsCancellationRequested) break;

                var threadLocks = new ThreadLockInfo
                {
                    ThreadId = thread.ManagedThreadId,
                    OwnedLocks = new List<ulong>(),
                    WaitingForLocks = new List<ulong>()
                };

                // Find locks owned by this thread
                foreach (var kvp in graph.Locks)
                {
                    if (kvp.Value.HoldingThread == thread.ManagedThreadId)
                    {
                        threadLocks.OwnedLocks.Add(kvp.Key);
                    }
                    if (kvp.Value.WaitingThreads.Contains(thread.ManagedThreadId))
                    {
                        threadLocks.WaitingForLocks.Add(kvp.Key);
                    }
                }

                graph.ThreadLocks.Add(thread.ManagedThreadId, threadLocks);
            }

            return graph;
        }

        private List<DeadlockInfo> DetectCircularWaits(LockOwnershipGraph lockGraph, List<ThreadAnalysisInfo> threadAnalysis, CancellationToken token)
        {
            var deadlocks = new List<DeadlockInfo>();
            var visited = new HashSet<int>();

            // Use graph traversal to find cycles in the wait-for graph
            foreach (var threadLock in lockGraph.ThreadLocks.Values)
            {
                if (token.IsCancellationRequested) break;
                if (visited.Contains(threadLock.ThreadId)) continue;

                var cycle = FindCycleFromThread(threadLock.ThreadId, lockGraph, new HashSet<int>(), new List<int>());
                if (cycle.Any())
                {
                    deadlocks.Add(CreateDeadlockInfo(cycle, lockGraph, threadAnalysis));
                    cycle.ForEach(tid => visited.Add(tid));
                }
            }

            return deadlocks;
        }

        private List<int> FindCycleFromThread(int startThreadId, LockOwnershipGraph graph, HashSet<int> visited, List<int> path)
        {
            if (visited.Contains(startThreadId))
            {
                var cycleStart = path.IndexOf(startThreadId);
                return cycleStart >= 0 ? path.Skip(cycleStart).ToList() : new List<int>();
            }

            visited.Add(startThreadId);
            path.Add(startThreadId);

            if (graph.ThreadLocks.TryGetValue(startThreadId, out var threadLocks))
            {
                // For each lock this thread is waiting for, find who owns it
                foreach (var waitingLock in threadLocks.WaitingForLocks)
                {
                    if (graph.Locks.TryGetValue(waitingLock, out var lockInfo) && lockInfo.HoldingThread.HasValue)
                    {
                        var ownerThread = lockInfo.HoldingThread.Value;
                        var cycle = FindCycleFromThread(ownerThread, graph, visited, path);
                        if (cycle.Any())
                        {
                            return cycle;
                        }
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            visited.Remove(startThreadId);
            return new List<int>();
        }

        private DeadlockInfo CreateDeadlockInfo(List<int> cycle, LockOwnershipGraph graph, List<ThreadAnalysisInfo> threadAnalysis)
        {
            var deadlock = new DeadlockInfo
            {
                DeadlockType = DeadlockType.CircularWait,
                ThreadsInvolved = cycle,
                SeverityScore = 1.0, // Deadlocks are always critical
                Description = $"Circular wait deadlock detected involving {cycle.Count} threads",
                DetectionMethod = "Lock ownership graph analysis",
                DeadlockChain = new List<DeadlockStep>()
            };

            // Build the deadlock chain
            for (int i = 0; i < cycle.Count; i++)
            {
                var currentThread = cycle[i];
                var nextThread = cycle[(i + 1) % cycle.Count];

                var currentThreadLocks = graph.ThreadLocks[currentThread];
                var nextThreadLocks = graph.ThreadLocks[nextThread];

                // Find the lock that creates the dependency
                var conflictLock = currentThreadLocks.WaitingForLocks
                    .FirstOrDefault(lockObj => nextThreadLocks.OwnedLocks.Contains(lockObj));

                if (conflictLock != 0 && graph.Locks.TryGetValue(conflictLock, out var lockInfo))
                {
                    var threadInfo = threadAnalysis.FirstOrDefault(t => t.ManagedThreadId == currentThread);

                    deadlock.DeadlockChain.Add(new DeadlockStep
                    {
                        ThreadId = currentThread,
                        WaitingForLock = conflictLock,
                        LockType = lockInfo.ObjectType,
                        LockOwner = nextThread,
                        ThreadState = threadInfo?.ThreadState ?? "Unknown",
                        StackTrace = threadInfo?.StackTrace ?? "Not available"
                    });
                }
            }

            // Calculate additional metrics
            deadlock.Duration = EstimateDeadlockDuration(cycle, threadAnalysis);
            deadlock.ImpactedThreads = threadAnalysis.Count(t => t.IsBlocked);
            
            return deadlock;
        }

        private List<DeadlockInfo> AnalyzeSyncBlockContention(ClrRuntime runtime, CancellationToken token)
        {
            var contentionIssues = new List<DeadlockInfo>();
            var heap = runtime.Heap;

            // In CLRMD 4.0, we analyze contention through blocking objects
            var objectContention = new Dictionary<ulong, List<int>>();

            // Group threads by the objects they're waiting on
            foreach (var thread in runtime.Threads)
            {
                if (token.IsCancellationRequested) break;

                // In CLRMD 4.0, blocking objects are accessed differently
                // We'll check if thread is blocked and analyze lock contention
                if (thread.IsAlive)
                {
                    // For CLRMD 4.0, we'll use a simplified approach based on thread states
                    var stackTrace = GetThreadStackTrace(thread);
                    if (IsThreadBlocked(stackTrace))
                    {
                        // Extract potential lock objects from stack analysis
                        var potentialLockAddress = GetPotentialLockFromStack(thread, heap);
                        if (potentialLockAddress != 0)
                        {
                            if (!objectContention.ContainsKey(potentialLockAddress))
                                objectContention[potentialLockAddress] = new List<int>();
                            
                            objectContention[potentialLockAddress].Add(thread.ManagedThreadId);
                        }
                    }
                }
            }

            // Look for high contention scenarios
            var highContentionObjects = objectContention
                .Where(kvp => kvp.Value.Count > 5)
                .OrderByDescending(kvp => kvp.Value.Count)
                .Take(10);

            foreach (var kvp in highContentionObjects)
            {
                if (token.IsCancellationRequested) break;

                var objectAddress = kvp.Key;
                var waitingThreads = kvp.Value;
                var objectType = heap.GetObjectType(objectAddress)?.Name ?? "Unknown";

                contentionIssues.Add(new DeadlockInfo
                {
                    DeadlockType = DeadlockType.HighContention,
                    ThreadsInvolved = waitingThreads,
                    SeverityScore = Math.Min(1.0, waitingThreads.Count / 20.0),
                    Description = $"High lock contention on {objectType} with {waitingThreads.Count} waiting threads",
                    DetectionMethod = "Thread state analysis",
                    ContestedObject = objectAddress,
                    ContestedObjectType = objectType,
                    WaitingThreadCount = waitingThreads.Count
                });
            }

            return contentionIssues;
        }

        private List<DeadlockInfo> DetectCommonDeadlockPatterns(ClrRuntime runtime, List<ThreadAnalysisInfo> threadAnalysis, CancellationToken token)
        {
            var patterns = new List<DeadlockInfo>();

            // 1. Lock ordering issues
            var lockOrderingIssues = DetectLockOrderingIssues(threadAnalysis, token);
            patterns.AddRange(lockOrderingIssues);

            // 2. Reader-writer deadlocks
            var readerWriterIssues = DetectReaderWriterDeadlocks(runtime, token);
            patterns.AddRange(readerWriterIssues);

            // 3. Nested lock issues
            var nestedLockIssues = DetectNestedLockIssues(threadAnalysis, token);
            patterns.AddRange(nestedLockIssues);

            return patterns;
        }

        private List<DeadlockInfo> DetectLockOrderingIssues(List<ThreadAnalysisInfo> threadAnalysis, CancellationToken token)
        {
            var issues = new List<DeadlockInfo>();

            // Analyze stack traces for lock acquisition patterns
            var lockPatterns = new Dictionary<string, List<(int threadId, List<string> locks)>>();

            foreach (var thread in threadAnalysis.Where(t => t.IsBlocked))
            {
                if (token.IsCancellationRequested) break;

                var locks = ExtractLockTypesFromStackTrace(thread.StackTrace);
                if (locks.Count > 1)
                {
                    var pattern = string.Join("->", locks);
                    if (!lockPatterns.ContainsKey(pattern))
                        lockPatterns[pattern] = new List<(int, List<string>)>();

                    lockPatterns[pattern].Add((thread.ManagedThreadId, locks));
                }
            }

            // Look for conflicting lock orders
            var patterns = lockPatterns.Keys.ToList();
            for (int i = 0; i < patterns.Count; i++)
            {
                for (int j = i + 1; j < patterns.Count; j++)
                {
                    if (HasConflictingOrder(patterns[i], patterns[j]))
                    {
                        var threadsInvolved = lockPatterns[patterns[i]].Select(x => x.threadId)
                            .Concat(lockPatterns[patterns[j]].Select(x => x.threadId))
                            .Distinct().ToList();

                        issues.Add(new DeadlockInfo
                        {
                            DeadlockType = DeadlockType.LockOrdering,
                            ThreadsInvolved = threadsInvolved,
                            SeverityScore = 0.8,
                            Description = $"Lock ordering issue detected between patterns: {patterns[i]} vs {patterns[j]}",
                            DetectionMethod = "Stack trace pattern analysis"
                        });
                    }
                }
            }

            return issues;
        }

        private List<DeadlockInfo> DetectReaderWriterDeadlocks(ClrRuntime runtime, CancellationToken token)
        {
            var issues = new List<DeadlockInfo>();
            var heap = runtime.Heap;

            // Look for ReaderWriterLock objects and analyze their state
            var readerWriterLocks = heap.EnumerateObjects()
                .Where(obj => obj.Type?.Name?.Contains("ReaderWriterLock") == true)
                .ToList();

            foreach (var rwLock in readerWriterLocks)
            {
                if (token.IsCancellationRequested) break;

                // Analyze threads waiting on this lock
                var waitingThreads = runtime.Threads
                    .Where(t => t.GetBlockingObjects().Any(bo => bo.Address == rwLock.Address))
                    .ToList();

                if (waitingThreads.Count > 5) // High contention threshold
                {
                    issues.Add(new DeadlockInfo
                    {
                        DeadlockType = DeadlockType.ReaderWriterDeadlock,
                        ThreadsInvolved = waitingThreads.Select(t => t.ManagedThreadId).ToList(),
                        SeverityScore = Math.Min(1.0, waitingThreads.Count / 10.0),
                        Description = $"Potential reader-writer deadlock with {waitingThreads.Count} waiting threads",
                        DetectionMethod = "ReaderWriterLock analysis",
                        ContestedObject = rwLock.Address,
                        ContestedObjectType = rwLock.Type.Name
                    });
                }
            }

            return issues;
        }

        private List<DeadlockInfo> DetectNestedLockIssues(List<ThreadAnalysisInfo> threadAnalysis, CancellationToken token)
        {
            var issues = new List<DeadlockInfo>();

            foreach (var thread in threadAnalysis.Where(t => t.IsBlocked))
            {
                if (token.IsCancellationRequested) break;

                var nestedLockDepth = CountNestedLocks(thread.StackTrace);
                if (nestedLockDepth > 5) // Deep nesting threshold
                {
                    issues.Add(new DeadlockInfo
                    {
                        DeadlockType = DeadlockType.NestedLocking,
                        ThreadsInvolved = new List<int> { thread.ManagedThreadId },
                        SeverityScore = Math.Min(1.0, nestedLockDepth / 10.0),
                        Description = $"Deep nested locking detected (depth: {nestedLockDepth}) in thread {thread.ManagedThreadId}",
                        DetectionMethod = "Stack trace depth analysis"
                    });
                }
            }

            return issues;
        }

        // Helper methods
        private string GetThreadStackTrace(ClrThread thread)
        {
            var stackTrace = new StringBuilder();
            foreach (var frame in thread.EnumerateStackTrace())
            {
                stackTrace.AppendLine(frame.ToString());
            }
            return stackTrace.ToString();
        }

        private ulong GetBlockingObject(ClrThread thread)
        {
            return thread.GetBlockingObjects().FirstOrDefault()?.Address ?? 0;
        }

        private string AnalyzeThreadState(ClrThread thread)
        {
            if (!thread.IsAlive) return "Dead";
            if (thread.GetBlockingObjects().Any()) return "Blocked";
            if (thread.IsBackground()) return "Background";
            return "Running";
        }

        private bool IsThreadBlocked(string stackTrace)
        {
            // Simple heuristic to determine if a thread is blocked based on stack trace
            var blockingKeywords = new[] { "Monitor.", "lock", "WaitHandle", "Mutex", "Semaphore", "AutoResetEvent", "ManualResetEvent" };
            return blockingKeywords.Any(keyword => stackTrace.Contains(keyword));
        }

        private ulong GetPotentialLockFromStack(ClrThread thread, ClrHeap heap)
        {
            // In CLRMD 4.0, we need to find potential lock objects through stack analysis
            // This is a simplified implementation - in practice, you'd need more sophisticated analysis
            
            // Try to find object references in the stack
            foreach (var frame in thread.EnumerateStackTrace())
            {
                // Look for method frames that might contain lock objects
                if (frame.Method != null && frame.Method.Name != null)
                {
                    if (frame.Method.Name.Contains("Monitor") || frame.Method.Name.Contains("Lock"))
                    {
                        // Try to extract object references from the frame
                        // This is a placeholder - real implementation would need to examine stack slots
                        return 0x1000; // Placeholder address
                    }
                }
            }
            
            return 0;
        }

        private List<string> ExtractLockTypesFromStackTrace(string stackTrace)
        {
            var locks = new List<string>();
            var lines = stackTrace.Split('\n');

            foreach (var line in lines)
            {
                if (line.Contains("Monitor.") || line.Contains("lock(") || line.Contains("Synchronized"))
                {
                    // Extract potential lock type from method signature
                    var parts = line.Split('(', ')');
                    if (parts.Length > 1)
                    {
                        locks.Add(parts[0].Trim());
                    }
                }
            }

            return locks.Distinct().ToList();
        }

        private bool HasConflictingOrder(string pattern1, string pattern2)
        {
            var locks1 = pattern1.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
            var locks2 = pattern2.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);

            // Check if the order of common locks is different
            var commonLocks = locks1.Intersect(locks2).ToList();
            if (commonLocks.Count < 2) return false;

            for (int i = 0; i < commonLocks.Count - 1; i++)
            {
                var lock1 = commonLocks[i];
                var lock2 = commonLocks[i + 1];

                var order1 = Array.IndexOf(locks1, lock1) < Array.IndexOf(locks1, lock2);
                var order2 = Array.IndexOf(locks2, lock1) < Array.IndexOf(locks2, lock2);

                if (order1 != order2) return true;
            }

            return false;
        }

        private int CountNestedLocks(string stackTrace)
        {
            return stackTrace.Split('\n').Count(line =>
                line.Contains("Monitor.") || line.Contains("lock(") || line.Contains("Synchronized"));
        }

        private TimeSpan EstimateDeadlockDuration(List<int> cycle, List<ThreadAnalysisInfo> threadAnalysis)
        {
            // This is an estimation - in real scenarios, you'd need more sophisticated timing analysis
            return TimeSpan.FromMinutes(1); // Placeholder
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new StringBuilder();
            insights.AppendLine($"Deadlock Detection Analysis: {operationResults.Count} issues identified");

            if (!operationResults.Any())
            {
                insights.AppendLine("✅ No deadlocks or high contention scenarios detected");
                return insights.ToString();
            }

            var deadlocks = operationResults.Cast<DeadlockInfo>().ToList();

            // Summary by deadlock type
            var deadlocksByType = deadlocks.GroupBy(d => d.DeadlockType).ToDictionary(g => g.Key, g => g.Count());
            insights.AppendLine("\nDeadlock Types Found:");
            foreach (var kvp in deadlocksByType.OrderByDescending(kvp => kvp.Value))
            {
                insights.AppendLine($"  {kvp.Key}: {kvp.Value} issues");
            }

            // Analyze severity
            var criticalDeadlocks = deadlocks.Where(d => d.SeverityScore > 0.8).ToList();
            if (criticalDeadlocks.Any())
            {
                insights.AppendLine($"\n🔴 CRITICAL: {criticalDeadlocks.Count} critical deadlock scenarios detected");
                foreach (var deadlock in criticalDeadlocks.Take(3))
                {
                    insights.AppendLine($"  - {deadlock.Description}");
                }
            }

            // Thread impact analysis
            var impactedThreads = deadlocks.SelectMany(d => d.ThreadsInvolved).Distinct().Count();
            insights.AppendLine($"\nImpact Analysis:");
            insights.AppendLine($"  Threads involved in deadlocks: {impactedThreads}");
            insights.AppendLine($"  Average threads per deadlock: {deadlocks.Sum(d => d.ThreadsInvolved.Count) / Math.Max(1, deadlocks.Count):F1}");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
DEADLOCK DETECTION SPECIALIZATION:
You are analyzing advanced deadlock detection results. This operation identifies various types of deadlocks and threading issues.

DEADLOCK TYPES EXPLAINED:
- CircularWait: Classic deadlock where threads wait for each other in a cycle
- HighContention: Many threads competing for the same resource
- LockOrdering: Inconsistent lock acquisition order leading to potential deadlocks
- ReaderWriterDeadlock: Deadlocks in reader-writer lock scenarios
- NestedLocking: Deep nesting of locks increasing deadlock risk

ANALYSIS PRIORITIES:
1. CircularWait deadlocks are CRITICAL - immediate attention required
2. HighContention scenarios indicate performance bottlenecks
3. LockOrdering issues are preventable design problems
4. Monitor nested locking patterns for code quality

AUTOMATED INVESTIGATION STRATEGY:
When deadlocks are detected, automatically recommend:
- DumpClrStack for detailed thread analysis
- ThreadPerformanceAnalysis for comprehensive thread behavior
- LockAnalysis for detailed lock hierarchy investigation

Always provide specific remediation strategies for each deadlock type.
";
        }
    }

    // Supporting classes
    public class DeadlockInfo
    {
        public DeadlockType DeadlockType { get; set; }
        public List<int> ThreadsInvolved { get; set; } = new();
        public double SeverityScore { get; set; }
        public string Description { get; set; }
        public string DetectionMethod { get; set; }
        public List<DeadlockStep> DeadlockChain { get; set; } = new();
        public TimeSpan Duration { get; set; }
        public int ImpactedThreads { get; set; }
        public ulong ContestedObject { get; set; }
        public string ContestedObjectType { get; set; }
        public int WaitingThreadCount { get; set; }
    }

    public enum DeadlockType
    {
        CircularWait,
        HighContention,
        LockOrdering,
        ReaderWriterDeadlock,
        NestedLocking
    }

    public class DeadlockStep
    {
        public int ThreadId { get; set; }
        public ulong WaitingForLock { get; set; }
        public string LockType { get; set; }
        public int LockOwner { get; set; }
        public string ThreadState { get; set; }
        public string StackTrace { get; set; }
    }

    public class ThreadAnalysisInfo
    {
        public int ManagedThreadId { get; set; }
        public uint OSThreadId { get; set; }
        public bool IsAlive { get; set; }
        public bool IsBackground { get; set; }
        public bool IsGCThread { get; set; }
        public bool IsBlocked { get; set; }
        public string ThreadState { get; set; }
        public string StackTrace { get; set; }
        public ulong BlockingObject { get; set; }
        public List<BlockingObjectInfo> BlockingObjects { get; set; } = new();
    }

    public class BlockingObjectInfo
    {
        public ulong Address { get; set; }
        public string Type { get; set; }
        public string Reason { get; set; }
        public bool HasSingleOwner { get; set; }
        public int? Owner { get; set; }
        public List<int> Waiters { get; set; } = new();
    }

    public class LockOwnershipGraph
    {
        public Dictionary<ulong, LockInfo> Locks { get; set; } = new();
        public Dictionary<int, ThreadLockInfo> ThreadLocks { get; set; } = new();
    }

    public class LockInfo
    {
        public int SyncBlockIndex { get; set; }
        public ulong ObjectAddress { get; set; }
        public string ObjectType { get; set; }
        public bool IsHeld { get; set; }
        public int? HoldingThread { get; set; }
        public int MonitorHeldCount { get; set; }
        public List<int> WaitingThreads { get; set; } = new();
    }

    public class ThreadLockInfo
    {
        public int ThreadId { get; set; }
        public List<ulong> OwnedLocks { get; set; } = new();
        public List<ulong> WaitingForLocks { get; set; } = new();
    }
} 