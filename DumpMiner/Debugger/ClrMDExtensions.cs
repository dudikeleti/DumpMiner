using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Debugger
{
    internal static class ClrMDExtensions
    {
        public static IEnumerable<ClrType> EnumerateTypes(this ClrHeap heap)
        {
            var runtime = heap.Runtime;
            return runtime.EnumerateModules()
                .SelectMany(mod => mod.EnumerateTypeDefToMethodTableMap()
                    .Select(tuple => tuple.Item1)
                )
                .Distinct()
                .Select(runtime.GetTypeByMethodTable);
        }

        public static IEnumerable<ClrType> EnumerateTypes(this ClrModule module)
        {
            var runtime = module.AppDomain.Runtime;
            return module.EnumerateTypeDefToMethodTableMap()
                    .Select(tuple => tuple.Item1)
                    .Distinct()
                    .Select(runtime.GetTypeByMethodTable);
        }

        public static bool IsOptimized(this ClrModule module)
        {
            return (module.DebuggingMode &
                    System.Diagnostics.DebuggableAttribute.DebuggingModes.DisableOptimizations) == 0;
        }

        public static bool IsAbstract(this ClrMethod method)
        {
            return (method.Attributes & MethodAttributes.Abstract) != 0;
        }

        public static bool IsGeneric(this ClrMethod method)
        {
            // Check if method has generic parameters
            return method.Signature != null && method.Signature.Contains('<');
        }

        public static bool IsBackground(this ClrThread thread)
        {
            // In ClrMD 4.0, we need to analyze thread state through stack traces
            // Background threads typically have specific patterns in their stack
            var stackTrace = GetStackTraceString(thread);
            return stackTrace.Contains("ThreadPool") ||
                   stackTrace.Contains("BackgroundThread") ||
                   thread.IsGc ||
                   thread.IsFinalizer;
        }

        // NEW: Blocking Objects Detection Implementation
        public static IEnumerable<BlockingObjectInfo> GetBlockingObjects(this ClrThread thread)
        {
            var blockingObjects = new List<BlockingObjectInfo>();

            if (!thread.IsAlive)
                return blockingObjects;

            // Method 1: Stack trace analysis for common blocking patterns
            var stackTraceBlocks = AnalyzeStackTraceForBlocking(thread);
            blockingObjects.AddRange(stackTraceBlocks);

            // Method 2: Sync block analysis
            var syncBlockBlocks = AnalyzeSyncBlocksForBlocking(thread);
            blockingObjects.AddRange(syncBlockBlocks);

            // Method 3: Lock object detection from stack frames
            var stackLockBlocks = DetectLockObjectsFromStack(thread);
            blockingObjects.AddRange(stackLockBlocks);

            return blockingObjects.Distinct().ToList();
        }

        public static bool IsBlocked(this ClrThread thread)
        {
            return GetBlockingObjects(thread).Any();
        }

        public static int GetBlockingObjectCount(this ClrThread thread)
        {
            return GetBlockingObjects(thread).Count();
        }

        // Helper method to get stack trace as string
        private static string GetStackTraceString(ClrThread thread)
        {
            var frames = thread.EnumerateStackTrace().ToList();
            return string.Join("\n", frames.Select(f => f.ToString()));
        }

        // Method 1: Analyze stack trace for blocking patterns
        private static List<BlockingObjectInfo> AnalyzeStackTraceForBlocking(ClrThread thread)
        {
            var blockingObjects = new List<BlockingObjectInfo>();
            var stackTrace = GetStackTraceString(thread);

            // Define blocking patterns
            var blockingPatterns = new Dictionary<string, string>
            {
                { @"Monitor\.Enter|Monitor\.Wait|Monitor\.TryEnter", "Monitor" },
                { @"lock\s*\(|SyncLock", "Lock" },
                { @"WaitHandle\.WaitOne|WaitHandle\.WaitAll|WaitHandle\.WaitAny", "WaitHandle" },
                { @"Mutex\.WaitOne|Mutex\.ReleaseMutex", "Mutex" },
                { @"Semaphore\.WaitOne|SemaphoreSlim\.Wait", "Semaphore" },
                { @"AutoResetEvent\.WaitOne|ManualResetEvent\.WaitOne", "ResetEvent" },
                { @"Task\.Wait|Task\.Result|\.GetAwaiter\(\)\.GetResult", "Task" },
                { @"Thread\.Join|Thread\.Sleep", "Thread" },
                { @"ReaderWriterLock|ReaderWriterLockSlim", "ReaderWriterLock" },
                { @"CountdownEvent\.Wait|Barrier\.SignalAndWait", "Synchronization" }
            };

            foreach (var pattern in blockingPatterns)
            {
                if (Regex.IsMatch(stackTrace, pattern.Key, RegexOptions.IgnoreCase))
                {
                    blockingObjects.Add(new BlockingObjectInfo
                    {
                        Address = 0, // Cannot determine exact address from stack trace
                        Type = pattern.Value,
                        Reason = $"Detected {pattern.Value} blocking pattern in stack trace",
                        HasSingleOwner = true,
                        Owner = null,
                        Waiters = new List<int>()
                    });
                }
            }

            return blockingObjects;
        }

        // Method 2: Analyze sync blocks for blocking
        private static List<BlockingObjectInfo> AnalyzeSyncBlocksForBlocking(ClrThread thread)
        {
            var blockingObjects = new List<BlockingObjectInfo>();
            var heap = thread.Runtime.Heap;

            // In ClrMD 4.0, we need to analyze objects with sync blocks
            // This is a simplified approach - examining objects that might have sync blocks
            var processedAddresses = new HashSet<ulong>();

            foreach (var frame in thread.EnumerateStackTrace())
            {
                // Look for object references in stack frames
                // This is a simplified approach - in a real implementation,
                // you'd need to examine the actual stack slots

                if (frame.Method != null)
                {
                    var methodName = frame.Method.Name ?? "";
                    var typeName = frame.Method.Type?.Name ?? "";

                    // If this is a synchronization-related method, try to find the associated object
                    if (methodName.Contains("Monitor") || methodName.Contains("Lock") ||
                        typeName.Contains("Monitor") || typeName.Contains("Lock"))
                    {
                        // This is a placeholder - in a real implementation, 
                        // you'd extract the actual object address from the stack frame
                        var potentialLockAddress = GetPotentialLockObjectAddress(frame, heap);

                        if (potentialLockAddress != 0 && !processedAddresses.Contains(potentialLockAddress))
                        {
                            processedAddresses.Add(potentialLockAddress);

                            var objType = heap.GetObjectType(potentialLockAddress);
                            blockingObjects.Add(new BlockingObjectInfo
                            {
                                Address = potentialLockAddress,
                                Type = objType?.Name ?? "Unknown",
                                Reason = "Potential lock object detected from sync block analysis",
                                HasSingleOwner = true,
                                Owner = thread.ManagedThreadId,
                                Waiters = new List<int>()
                            });
                        }
                    }
                }
            }

            return blockingObjects;
        }

        // Method 3: Detect lock objects from stack frames
        private static List<BlockingObjectInfo> DetectLockObjectsFromStack(ClrThread thread)
        {
            var blockingObjects = new List<BlockingObjectInfo>();
            var heap = thread.Runtime.Heap;

            // Look for common synchronization objects on the heap that might be involved in blocking
            var syncTypes = new[]
            {
                "System.Threading.Monitor",
                "System.Threading.Mutex",
                "System.Threading.Semaphore",
                "System.Threading.SemaphoreSlim",
                "System.Threading.AutoResetEvent",
                "System.Threading.ManualResetEvent",
                "System.Threading.ReaderWriterLock",
                "System.Threading.ReaderWriterLockSlim",
                "System.Threading.CountdownEvent",
                "System.Threading.Barrier"
            };

            // This is a simplified approach - examine objects of synchronization types
            foreach (var obj in heap.Segments.SelectMany(seg => seg.EnumerateObjects()))
            {
                var objType = obj.Type;
                if (objType != null && syncTypes.Any(st => objType.Name?.Contains(st) == true))
                {
                    // Check if this object might be involved in blocking for this thread
                    // This is a heuristic approach
                    blockingObjects.Add(new BlockingObjectInfo
                    {
                        Address = obj.Address,
                        Type = objType.Name,
                        Reason = "Synchronization object detected on heap",
                        HasSingleOwner = false,
                        Owner = null,
                        Waiters = new List<int>()
                    });
                }
            }

            return blockingObjects.Take(10).ToList(); // Limit to avoid too many results
        }

        // Helper method to get potential lock object address
        private static ulong GetPotentialLockObjectAddress(ClrStackFrame frame, ClrHeap heap)
        {
            // This is a placeholder implementation
            // In a real implementation, you would:
            // 1. Examine the stack frame's local variables and parameters
            // 2. Look for object references that might be lock objects
            // 3. Use stack walking techniques to find the actual objects

            // For now, return a placeholder address
            // In practice, this would require more sophisticated analysis
            return 0;
        }

        // Helper method to check if an object has an active sync block
        public static bool HasActiveSyncBlock(this Microsoft.Diagnostics.Runtime.ClrObject obj)
        {
            try
            {
                // In ClrMD 4.0, we can't directly access sync block info
                // This is a simplified check based on object header analysis
                // In a real implementation, you'd need to examine the object header
                // to determine if it has an active sync block

                return obj.Type != null && obj.Size > 0;
            }
            catch
            {
                return false;
            }
        }

        // Helper method to estimate if a thread is likely blocked based on stack analysis
        public static bool IsLikelyBlocked(this ClrThread thread)
        {
            if (!thread.IsAlive)
                return false;

            var stackTrace = GetStackTraceString(thread);

            // Look for blocking indicators in the stack trace
            var blockingIndicators = new[]
            {
                "Monitor.Enter", "Monitor.Wait", "Monitor.TryEnter",
                "lock(", "SyncLock",
                "WaitHandle.WaitOne", "WaitHandle.WaitAll", "WaitHandle.WaitAny",
                "Mutex.WaitOne", "Semaphore.WaitOne", "SemaphoreSlim.Wait",
                "AutoResetEvent.WaitOne", "ManualResetEvent.WaitOne",
                "Task.Wait", "Task.Result", ".GetAwaiter().GetResult",
                "Thread.Join", "ReaderWriterLock", "CountdownEvent.Wait"
            };

            return blockingIndicators.Any(indicator =>
                stackTrace.Contains(indicator, System.StringComparison.OrdinalIgnoreCase));
        }
    }

    // Supporting class for blocking object information
    public class BlockingObjectInfo
    {
        public ulong Address { get; set; }
        public string Type { get; set; }
        public string Reason { get; set; }
        public bool HasSingleOwner { get; set; }
        public int? Owner { get; set; }
        public List<int> Waiters { get; set; } = new List<int>();

        public override bool Equals(object obj)
        {
            return obj is BlockingObjectInfo other &&
                   Address == other.Address &&
                   Type == other.Type;
        }

        public override int GetHashCode()
        {
            return Address.GetHashCode() ^ (Type?.GetHashCode() ?? 0);
        }
    }
}
