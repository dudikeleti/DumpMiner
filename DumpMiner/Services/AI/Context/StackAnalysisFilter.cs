using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.Runtime;
using ClrObject = DumpMiner.Debugger.ClrObject;

namespace DumpMiner.Services.AI.Context
{
    /// <summary>
    /// Smart filtering for call stack analysis to reduce AI payload size
    /// </summary>
    public class StackAnalysisFilter
    {
        private readonly StackAnalysisOptions _options;

        public StackAnalysisFilter(StackAnalysisOptions options = null)
        {
            _options = options ?? new StackAnalysisOptions();
        }

        /// <summary>
        /// Create a filtered, AI-friendly summary of call stack data
        /// </summary>
        public string CreateAIAnalysisPayload<T>(IEnumerable<T> callstackItems) where T : class
        {
            var summary = new StringBuilder();

            // Get reflection info for the generic type
            var itemList = callstackItems.ToList();
            if (!itemList.Any()) return "No call stack data available.";

            // Thread Overview
            summary.AppendLine("=== THREAD OVERVIEW ===");
            summary.AppendLine($"Total Threads: {itemList.Count}");

            var threadsWithExceptions = GetThreadsWithExceptions(itemList).ToList();
            if (threadsWithExceptions.Any())
            {
                summary.AppendLine($"Threads with Exceptions: {threadsWithExceptions.Count()}");
            }

            // Top priority Threads
            var threads = GetImportantThreads(itemList)
                .Take(_options.MaxDetailedThreads);

            summary.AppendLine("\n=== DETAILED ANALYSIS ===");

            foreach (var thread in threads)
            {
                var threadId = GetThreadId(thread);
                var stackFrames = GetStackFrames(thread);

                summary.AppendLine($"\nThread {threadId}:");

                // Filter user frames only
                var userFrames = FilterUserCodeFrames(stackFrames)
                    .Take(_options.MaxFramesPerThread);

                foreach (var frame in userFrames)
                {
                    summary.AppendLine($"  {GetFrameDisplay(frame)}");
                }

                var objects = GetStackObjects(thread)
                    .Take(_options.MaxObjectsPerThread)
                    .ToList();

                if (objects.Any())
                {
                    summary.AppendLine($"\nThread objects:");
                }

                foreach (var o in objects)
                {
                    var value = GetObjectValue(o);
                    if (value?.Count > 0)
                    {
                        summary.AppendLine($"  {value[0].TypeName ?? "unknown"}: {value[0].Value}");
                    }
                }

                // Check size limit
                if (summary.Length > _options.MaxTotalPayloadChars)
                {
                    summary.AppendLine("... (truncated)");
                    break;
                }
            }

            return summary.ToString();
        }

        private IEnumerable<T> GetThreadsWithExceptions<T>(IEnumerable<T> items)
        {
            return items.Where(item => GetExceptionInfo(item) != null);
        }

        private IEnumerable<T> GetImportantThreads<T>(IEnumerable<T> items)
        {
            return items.OrderByDescending(GetPriorityScore);
        }

        private int GetPriorityScore<T>(T item)
        {
            int score = 0;

            // Exception = high priority
            if (GetExceptionInfo(item) != null)
            {
                score += 1000;
            }

            // Deep stack = medium priority
            var frameCount = GetStackFrames(item)?.Count() ?? 0;
            if (frameCount > 50)
            {
                score += 100;
            }

            var threadId = GetThreadId(item);
            if (threadId == "1")
            {
                score += 100;
            }

            return score;
        }

        private IEnumerable<object> FilterUserCodeFrames(IEnumerable<object> frames)
        {
            if (frames == null) return [];

            return frames.Where(frame =>
            {
                var display = GetFrameDisplay(frame);
                if (string.IsNullOrEmpty(display)) return false;

                // Filter out system/framework code
                var systemPrefixes = new[]
                {
                    "System.", "Microsoft.", "mscorlib", "netstandard",
                    "WindowsBase", "PresentationCore", "PresentationFramework",
                    "clr!", "ntdll!", "kernel32!", "user32!", "[InlinedCallFrame]"
                };

                return !systemPrefixes.Any(prefix => display.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            });
        }

        private string GetThreadId<T>(T item) => GetProperty<int?>(item, "ManagedThreadId")?.ToString() ?? "?";
        private string GetExceptionInfo<T>(T item) => GetProperty<object>(item, "Exception")?.ToString();
        private IEnumerable<object> GetStackFrames<T>(T item) => GetProperty<IEnumerable<object>>(item, "StackFrames");
        private string GetFrameDisplay(object frame) => GetProperty<string>(frame, "DisplayString");
        private IEnumerable<object> GetStackObjects<T>(T item) => GetProperty<IEnumerable<object>>(item, "StackObjects");
        private List<ClrObject.ClrObjectModel> GetObjectValue(object o) => GetProperty<List<ClrObject.ClrObjectModel>>(o, "Value");

        private TProperty GetProperty<TProperty>(object obj, string propertyName)
        {
            if (obj == null) return default(TProperty);

            var type = obj.GetType();
            var property = type.GetProperty(propertyName);
            if (property != null && property.CanRead)
            {
                try
                {
                    var value = property.GetValue(obj);
                    if (value is TProperty result)
                        return result;
                }
                catch { }
            }

            return default(TProperty);
        }
    }

    /// <summary>
    /// Configuration options for stack analysis filtering
    /// </summary>
    public class StackAnalysisOptions
    {
        /// <summary>
        /// Maximum number of threads to analyze in detail
        /// </summary>
        public int MaxDetailedThreads { get; set; } = 5;

        /// <summary>
        /// Maximum stack frames per thread
        /// </summary>
        public int MaxFramesPerThread { get; set; } = 20;

        /// <summary>
        /// Maximum objects per  thread
        /// </summary>
        public int MaxObjectsPerThread { get; set; } = 5;

        /// <summary>
        /// Maximum total payload size in characters
        /// </summary>
        public int MaxTotalPayloadChars { get; set; } = 50_000; // ~50KB
    }
}