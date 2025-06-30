using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DumpMiner.Debugger;
using DumpMiner.Services.AI.Models;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using AIStackFrame = DumpMiner.Services.AI.Models.StackFrame;
using AIOperationContext = DumpMiner.Services.AI.Models.OperationContext;

namespace DumpMiner.Services.AI.Context
{
    /// <summary>
    /// Builds context-aware AI requests for dump analysis
    /// </summary>
    public interface IDumpAnalysisContextBuilder
    {
        /// <summary>
        /// Creates dump context from current debugger session
        /// </summary>
        Task<DumpContext> BuildDumpContextAsync();

        /// <summary>
        /// Creates operation context from operation results
        /// </summary>
        AIOperationContext BuildOperationContext(string operationName, Dictionary<string, object> parameters, IEnumerable<object> results);

        /// <summary>
        /// Builds system prompt for dump analysis
        /// </summary>
        string BuildSystemPrompt(string operationName, DumpContext? dumpContext = null);

        /// <summary>
        /// Enhances user prompt with relevant context
        /// </summary>
        string EnhanceUserPrompt(string userPrompt, AIOperationContext? operationContext = null);
    }

    /// <summary>
    /// Implementation of dump analysis context builder
    /// </summary>
    [Export(typeof(IDumpAnalysisContextBuilder))]
    public sealed class DumpAnalysisContextBuilder : IDumpAnalysisContextBuilder
    {
        private readonly ILogger<DumpAnalysisContextBuilder> _logger;

        [ImportingConstructor]
        public DumpAnalysisContextBuilder([Import(AllowDefault = true)] ILogger<DumpAnalysisContextBuilder> logger)
        {
            _logger = logger ?? CreateFallbackLogger();
        }

        /// <summary>
        /// Creates a fallback logger if MEF resolution fails using Serilog
        /// </summary>
        private static ILogger<DumpAnalysisContextBuilder> CreateFallbackLogger()
        {
            var serilogLogger = Serilog.Log.ForContext<DumpAnalysisContextBuilder>();
            return new SerilogLoggerFactory(serilogLogger).CreateLogger<DumpAnalysisContextBuilder>();
        }

        public async Task<DumpContext> BuildDumpContextAsync()
        {
            try
            {
                if (!DebuggerSession.Instance.IsAttached)
                {
                    _logger.LogWarning("No debugger session is attached, returning empty dump context");
                    return new DumpContext();
                }

                var processInfo = BuildProcessInfo();
                var heapStats = await BuildHeapStatisticsAsync();
                var exceptions = await BuildExceptionInfoAsync();
                var threads = await BuildThreadInfoAsync();
                var largeObjects = await BuildLargeObjectsInfoAsync();

                var context = new DumpContext
                {
                    ProcessInfo = processInfo,
                    HeapStats = heapStats,
                    Exceptions = exceptions,
                    Threads = threads,
                    LargeObjects = largeObjects,
                    AdditionalData = await BuildAdditionalDataAsync()
                };

                _logger.LogInformation("Built dump context with {ExceptionCount} exceptions, {ThreadCount} threads, {LargeObjectCount} large objects",
                    exceptions.Count, threads.Count, largeObjects.Count);

                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build dump context");
                return new DumpContext();
            }
        }

        public AIOperationContext BuildOperationContext(string operationName, Dictionary<string, object> parameters, IEnumerable<object> results)
        {
            var resultsList = results?.ToList() ?? new List<object>();

            return new AIOperationContext
            {
                OperationName = operationName,
                Parameters = parameters ?? new Dictionary<string, object>(),
                Results = resultsList,
                ResultCount = resultsList.Count
            };
        }

        public string BuildSystemPrompt(string operationName, DumpContext? dumpContext = null)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine("You are an expert .NET memory dump analyzer assistant helping developers diagnose memory issues, performance problems, and application crashes.");
            prompt.AppendLine();
            prompt.AppendLine("Your expertise includes:");
            prompt.AppendLine("- Memory leak detection and analysis");
            prompt.AppendLine("- Garbage collection issues");
            prompt.AppendLine("- Thread deadlock analysis");
            prompt.AppendLine("- Exception analysis and root cause identification");
            prompt.AppendLine("- Performance bottleneck identification");
            prompt.AppendLine("- Object reference analysis");
            prompt.AppendLine("- Stack trace interpretation");
            prompt.AppendLine();

            if (dumpContext != null)
            {
                prompt.AppendLine("Current dump analysis context:");
                AppendDumpContextToPrompt(prompt, dumpContext);
                prompt.AppendLine();
            }

            prompt.AppendLine($"The user is currently working with the '{operationName}' operation.");
            prompt.AppendLine();
            prompt.AppendLine("Please provide:");
            prompt.AppendLine("1. Clear, actionable insights");
            prompt.AppendLine("2. Specific recommendations for fixing issues");
            prompt.AppendLine("3. Code examples when relevant");
            prompt.AppendLine("4. Explanations suitable for developers at different skill levels");
            prompt.AppendLine("5. Follow-up questions or additional analysis suggestions");

            return prompt.ToString();
        }

        public string EnhanceUserPrompt(string userPrompt, AIOperationContext? operationContext = null)
        {
            if (operationContext == null)
                return userPrompt;

            var enhancedPrompt = new StringBuilder();
            enhancedPrompt.AppendLine($"[Operation: {operationContext.OperationName}]");
            enhancedPrompt.AppendLine($"[Results Count: {operationContext.ResultCount}]");

            if (operationContext.Parameters.Any())
            {
                enhancedPrompt.AppendLine("[Parameters:]");
                foreach (var param in operationContext.Parameters)
                {
                    enhancedPrompt.AppendLine($"  {param.Key}: {param.Value}");
                }
            }

            if (operationContext.Results.Any())
            {
                enhancedPrompt.AppendLine("[Sample Results:]");
                var sampleResults = operationContext.Results.Take(5);
                foreach (var result in sampleResults)
                {
                    enhancedPrompt.AppendLine($"  {FormatResultForPrompt(result)}");
                }

                if (operationContext.ResultCount > 5)
                {
                    enhancedPrompt.AppendLine($"  ... and {operationContext.ResultCount - 5} more results");
                }
            }

            enhancedPrompt.AppendLine();
            enhancedPrompt.AppendLine("[User Question:]");
            enhancedPrompt.AppendLine(userPrompt);

            return enhancedPrompt.ToString();
        }

        private ProcessInfo? BuildProcessInfo()
        {
            try
            {
                var attachedTo = DebuggerSession.Instance.AttachedTo;
                var runtime = DebuggerSession.Instance.Runtime;

                return new ProcessInfo
                {
                    ProcessId = attachedTo.id,
                    ProcessName = attachedTo.name ?? "Unknown",
                    WorkingSetSize = 0, // Would need to get from process
                    ClrVersion = runtime?.ClrInfo?.Version?.ToString() ?? "Unknown",
                    ThreadCount = runtime?.Threads.Count() ?? 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build process info");
                return null;
            }
        }

        private async Task<HeapStatistics?> BuildHeapStatisticsAsync()
        {
            try
            {
                var heap = DebuggerSession.Instance.Heap;
                if (heap == null) return null;

                return await Task.Run(() =>
                {
                    // Calculate heap sizes by enumerating segments
                    long totalSize = 0;

                    foreach (var segment in heap.Segments)
                    {
                        totalSize += (long)segment.Length;
                    }

                    var stats = new HeapStatistics
                    {
                        TotalSize = totalSize,
                        Gen0Size = 0, // Simplified - would need detailed analysis
                        Gen1Size = 0,
                        Gen2Size = 0,
                        LargeObjectHeapSize = 0,
                        ObjectCount = 0 // Would need to enumerate to count
                    };

                    return stats;
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build heap statistics");
                return null;
            }
        }

        private async Task<List<ExceptionInfo>> BuildExceptionInfoAsync()
        {
            try
            {
                var runtime = DebuggerSession.Instance.Runtime;
                if (runtime == null) return new List<ExceptionInfo>();

                return await Task.Run(() =>
                {
                    var exceptions = new List<ExceptionInfo>();

                    foreach (var thread in runtime.Threads)
                    {
                        var currentException = thread.CurrentException;
                        if (currentException != null && currentException.Address != 0)
                        {
                            exceptions.Add(new ExceptionInfo
                            {
                                Type = currentException.Type?.Name ?? "Unknown",
                                Message = "Exception detected", // Simplified message
                                StackTrace = GetStackTrace(thread),
                                Address = currentException.Address
                            });
                        }
                    }

                    return exceptions;
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build exception info");
                return new List<ExceptionInfo>();
            }
        }

        private async Task<List<ThreadInfo>> BuildThreadInfoAsync()
        {
            try
            {
                var runtime = DebuggerSession.Instance.Runtime;
                if (runtime == null) return new List<ThreadInfo>();

                return await Task.Run(() =>
                {
                    var threads = new List<ThreadInfo>();

                    foreach (var thread in runtime.Threads.Take(10)) // Limit to first 10 threads
                    {
                        var stackFrames = thread.EnumerateStackTrace()
                            .Take(5) // Limit stack frames
                            .Select(frame => new AIStackFrame
                            {
                                MethodName = frame.Method?.Name ?? "Unknown",
                                ModuleName = frame.Method?.Type?.Module?.Name ?? "Unknown",
                                InstructionPointer = frame.InstructionPointer
                            })
                            .ToList();

                        threads.Add(new ThreadInfo
                        {
                            ThreadId = (int)thread.OSThreadId,
                            State = thread.IsAlive ? "Alive" : "Dead",
                            StackPointer = thread.StackBase,
                            StackFrames = stackFrames
                        });
                    }

                    return threads;
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build thread info");
                return new List<ThreadInfo>();
            }
        }

        private async Task<List<ObjectInfo>> BuildLargeObjectsInfoAsync()
        {
            try
            {
                var heap = DebuggerSession.Instance.Heap;
                if (heap == null) return new List<ObjectInfo>();

                return await Task.Run(() =>
                {
                    var largeObjects = new List<ObjectInfo>();

                    foreach (var segment in heap.Segments)
                    {
                        foreach (var obj in segment.EnumerateObjects().Take(100)) // Limit objects
                        {
                            if (obj.Size > 85000) // Large object threshold
                            {
                                largeObjects.Add(new ObjectInfo
                                {
                                    Address = obj.Address,
                                    Type = obj.Type?.Name ?? "Unknown",
                                    Size = (long)obj.Size,
                                    Generation = (int)segment.GetGeneration(obj.Address)
                                });
                            }
                        }
                    }

                    return largeObjects.OrderByDescending(o => o.Size).Take(20).ToList();
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build large objects info");
                return new List<ObjectInfo>();
            }
        }

        private async Task<Dictionary<string, object>> BuildAdditionalDataAsync()
        {
            return await Task.Run(() =>
            {
                var additionalData = new Dictionary<string, object>();

                try
                {
                    var attachedTime = DebuggerSession.Instance.AttachedTime;
                    if (attachedTime.HasValue)
                    {
                        additionalData["AttachedTime"] = attachedTime.Value;
                        additionalData["AnalysisDuration"] = DateTime.UtcNow - attachedTime.Value;
                    }

                    additionalData["SessionId"] = Guid.NewGuid().ToString();
                    additionalData["BuildTime"] = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to build additional data");
                }

                return additionalData;
            });
        }

        private void AppendDumpContextToPrompt(StringBuilder prompt, DumpContext context)
        {
            if (context.ProcessInfo != null)
            {
                prompt.AppendLine($"Process: {context.ProcessInfo.ProcessName} (PID: {context.ProcessInfo.ProcessId})");
                prompt.AppendLine($"CLR Version: {context.ProcessInfo.ClrVersion}");
                prompt.AppendLine($"Thread Count: {context.ProcessInfo.ThreadCount}");
            }

            if (context.HeapStats != null)
            {
                prompt.AppendLine($"Heap Size: {FormatBytes(context.HeapStats.TotalSize)}");
                prompt.AppendLine($"Gen0: {FormatBytes(context.HeapStats.Gen0Size)}, Gen1: {FormatBytes(context.HeapStats.Gen1Size)}, Gen2: {FormatBytes(context.HeapStats.Gen2Size)}");
                prompt.AppendLine($"LOH: {FormatBytes(context.HeapStats.LargeObjectHeapSize)}");
            }

            if (context.Exceptions.Any())
            {
                prompt.AppendLine($"Exceptions Found: {context.Exceptions.Count}");
                foreach (var exception in context.Exceptions.Take(3))
                {
                    prompt.AppendLine($"  - {exception.Type}: {exception.Message}");
                }
            }

            if (context.LargeObjects.Any())
            {
                prompt.AppendLine($"Large Objects: {context.LargeObjects.Count}");
                foreach (var obj in context.LargeObjects.Take(3))
                {
                    prompt.AppendLine($"  - {obj.Type}: {FormatBytes(obj.Size)}");
                }
            }
        }

        private string GetExceptionMessage(Microsoft.Diagnostics.Runtime.ClrObject exception)
        {
            try
            {
                var messageField = exception.Type?.GetFieldByName("_message");
                if (messageField != null)
                {
                    var messageObj = messageField.ReadObject(exception.Address, false);
                    if (messageObj.IsValid)
                    {
                        return messageObj.AsString() ?? "No message";
                    }
                }
                return "No message available";
            }
            catch
            {
                return "Failed to read message";
            }
        }

        private string GetStackTrace(Microsoft.Diagnostics.Runtime.ClrThread thread)
        {
            try
            {
                var frames = thread.EnumerateStackTrace()
                    .Take(5)
                    .Select(frame => $"  at {frame.Method?.Name ?? "Unknown"}")
                    .ToList();

                return string.Join(Environment.NewLine, frames);
            }
            catch
            {
                return "Stack trace unavailable";
            }
        }

        private string FormatResultForPrompt(object result)
        {
            if (result == null) return "null";

            var resultType = result.GetType();
            if (resultType.IsAnonymousType())
            {
                return result.ToString() ?? "Anonymous object";
            }

            return $"{resultType.Name}: {result}";
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return $"{number:n1}{suffixes[counter]}";
        }
    }



    /// <summary>
    /// Extension methods for type checking
    /// </summary>
    public static class TypeExtensions
    {
        public static bool IsAnonymousType(this Type type)
        {
            return type.Name.StartsWith("<>") && type.Name.Contains("AnonymousType");
        }
    }
}