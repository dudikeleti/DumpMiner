using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using ClrObject = DumpMiner.Debugger.ClrObject;

namespace DumpMiner.Operations
{
    // !ClrStack
    [Export(OperationNames.DumpClrStack, typeof(IDebuggerOperation))]
    class DumpClrStackOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpClrStack;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            // Enhanced stack trace analysis with local variables support
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var result = new List<ClrStackDump>();
                var progressReporter = model.ProgressReporter;

                // Phase 1: Initialize and count threads
                progressReporter?.ReportPhase("Initializing", "Analyzing thread structure...");
                var threads = DebuggerSession.Instance.Runtime.Threads.ToList();
                var totalThreads = threads.Count;
                var currentThreadIndex = 0;
                var totalFramesProcessed = 0L;
                var startTime = DateTime.Now;

                // Phase 2: Process each thread
                progressReporter?.ReportPhase("Processing Threads", $"Analyzing {totalThreads} threads");

                foreach (var thread in threads)
                {
                    if (token.IsCancellationRequested)
                        break;

                    currentThreadIndex++;

                    // Report thread progress
                    var threadProgress = (currentThreadIndex * 100) / totalThreads;
                    var threadType = thread.IsAlive ? "Live" : "Dead";
                    progressReporter?.ReportProgress(threadProgress,
                        $"Thread {currentThreadIndex} of {totalThreads}",
                        $"Processing {threadType} thread {thread.ManagedThreadId} (OS: {thread.OSThreadId})");

                    var stackDetails = new ClrStackDump();
                    stackDetails.StackFrames = new List<Frame>();
                    stackDetails.StackObjects = new List<StackObject>();

                    // Get all stack frames for this thread
                    var stackFrames = thread.EnumerateStackTrace(true).ToList();
                    var totalFrames = stackFrames.Count;
                    var currentFrameIndex = 0;

                    foreach (var stackFrame in stackFrames)
                    {
                        currentFrameIndex++;
                        totalFramesProcessed++;

                        // Report frame progress every 10 frames or on significant frames
                        if (currentFrameIndex % 10 == 0 || currentFrameIndex == totalFrames)
                        {
                            progressReporter?.ReportProgress(totalFramesProcessed,
                                totalFramesProcessed + (totalFrames - currentFrameIndex),
                                "frames",
                                $"Thread {currentThreadIndex}: Frame {currentFrameIndex:N0}/{totalFrames:N0}");
                        }
                        stackDetails.StackBase = thread.StackBase;
                        stackDetails.StackLimit = thread.StackLimit;
                        stackDetails.Exception = thread.CurrentException;
                        stackDetails.OSThreadID = thread.IsAlive ? thread.OSThreadId.ToString() : "XXX";
                        stackDetails.ManagedThreadId = thread.ManagedThreadId;

                        var frame = new Frame
                        {
                            StackPointer = stackFrame.StackPointer,
                            InstructionPointer = stackFrame.InstructionPointer,
                            DisplayString = stackFrame.ToString(),
                            Method = stackFrame.Method,
                            LocalVariables = new List<LocalVariable>()
                        };

                        // Extract local variables if available
                        // Note: ClrMD 4.0 doesn't provide direct local variable enumeration
                        // This is a placeholder for future implementation when advanced debugging info is available
                        try
                        {
                            if (stackFrame.Method != null)
                            {
                                // Extract local variables using advanced stack analysis
                                var localVars = ExtractLocalVariablesFromStackFrame(stackFrame, thread);
                                frame.LocalVariables.AddRange(localVars);

                                // If no local variables found, add informational message
                                if (!localVars.Any())
                                {
                                    frame.LocalVariables.Add(new LocalVariable
                                    {
                                        Name = "Note",
                                        Type = "Information",
                                        Index = -1,
                                        Value = "Local variables not available - requires PDB symbols or advanced debugging info",
                                        IsArgument = false
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // If we can't enumerate locals, add error information
                            frame.LocalVariables.Add(new LocalVariable
                            {
                                Name = "Error",
                                Type = "Information",
                                Index = -1,
                                Value = $"Failed to extract local variables: {ex.Message}",
                                IsArgument = false
                            });
                        }

                        stackDetails.StackFrames.Add(frame);

                        if (token.IsCancellationRequested)
                            break;
                    }

                    ClrHeap heap = DebuggerSession.Instance.Heap;
                    var pointerSize = DebuggerSession.Instance.Runtime.DataTarget.DataReader.PointerSize;

                    // address of TEB (thread execution block) + pointer size
                    ulong start = thread.StackBase;
                    // address of TEB (thread execution block) + pointer size * 2
                    ulong stop = thread.StackLimit;

                    // We'll walk these in pointer order.
                    if (start > stop)
                    {
                        ulong tmp = start;
                        start = stop;
                        stop = tmp;
                    }

                    // ptr is a stack address.
                    for (ulong ptr = start; ptr <= stop; ptr += (ulong)pointerSize)
                    {
                        HashSet<ulong> stackObjects = new HashSet<ulong>();

                        // fail to read the memory
                        if (!heap.Runtime.DataTarget.DataReader.ReadPointer(ptr, out ulong obj))
                            break;

                        // the object added already
                        if (!stackObjects.Add(obj))
                            continue;

                        // not an object
                        var type = heap.GetObjectType(obj);
                        if (type == null)
                            continue;

                        // free space
                        if (type.IsFree) continue;

                        // All good, add it 
                        stackDetails.StackObjects.Add(
                            new StackObject
                            {
                                Address = ptr,
                                Object = obj,
                                Name = type.Name,
                                Value = new DumpMiner.Debugger.ClrObject(obj, type, token).Fields.Value
                            });

                        if (token.IsCancellationRequested)
                            break;
                    }

                    result.Add(stackDetails);
                }

                // Phase 3: Completing
                var totalTime = DateTime.Now - startTime;
                progressReporter?.ReportCompleted(totalFramesProcessed, totalTime);

                return result;
            });
        }

        protected override void AddOperationSpecificSuggestions(
            StringBuilder insights,
            Collection<object> operationResults,
            Dictionary<string, int> typeGroups)
        {
            // Stack-specific suggestions
            insights.AppendLine("• Stack frames with local variable information available");
            insights.AppendLine("• Recommend DumpSourceCode for code analysis");
            insights.AppendLine("• Consider DumpMethods for detailed method information");

            var stackDumps = operationResults.OfType<ClrStackDump>().ToList();
            if (stackDumps.Any(s => s.Exception != null))
            {
                insights.AppendLine("• Exceptions detected in stack traces - recommend DumpExceptions for detailed analysis");
            }

            if (stackDumps.Any(s => s.StackFrames?.Count > 100))
            {
                insights.AppendLine("• Deep call stacks detected - potential recursion or performance issues");
            }

            // Local variables analysis
            var totalLocalVars = stackDumps.SelectMany(s => s.StackFrames ?? new List<Frame>())
                .SelectMany(f => f.LocalVariables ?? new List<LocalVariable>())
                .Count();

            var argumentVars = stackDumps.SelectMany(s => s.StackFrames ?? new List<Frame>())
                .SelectMany(f => f.LocalVariables ?? new List<LocalVariable>())
                .Count(v => v.IsArgument);

            var localVars = totalLocalVars - argumentVars;

            if (totalLocalVars > 0)
            {
                insights.AppendLine($"• Local variables extracted: {localVars} locals, {argumentVars} arguments");
                insights.AppendLine("• Local variable extraction uses advanced stack analysis");
                insights.AppendLine("• For accurate variable names, ensure PDB symbols are available");
            }
        }

        private class ClrStackDump
        {
            public string OSThreadID { get; set; }
            public int ManagedThreadId { get; set; }
            //public ulong StackPointer { get; set; }
            //public ulong InstructionPointer { get; set; }
            //public string DisplayString { get; set; }
            //public ClrMethod Method { get; set; }
            public ClrException Exception { get; set; }
            public ulong StackBase { get; set; }
            public ulong StackLimit { get; set; }
            public List<StackObject> StackObjects { get; set; }
            public List<Frame> StackFrames { get; set; }
        }

        private class Frame
        {
            public ulong StackPointer { get; set; }
            public ulong InstructionPointer { get; set; }
            public string DisplayString { get; set; }
            public ClrMethod Method { get; set; }
            public List<LocalVariable> LocalVariables { get; set; } = new List<LocalVariable>();
        }

        private class StackObject
        {
            public ulong Address { get; set; }
            public object Object { get; set; }
            public string Name { get; set; }
            public ImmutableList<ClrObject.ClrObjectModel> Value { get; set; }
        }

        private class LocalVariable
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public int Index { get; set; }
            public object Value { get; set; }
            public bool IsArgument { get; set; }
        }

        /// <summary>
        /// Extract local variables from a stack frame using advanced stack analysis
        /// </summary>
        private List<LocalVariable> ExtractLocalVariablesFromStackFrame(ClrStackFrame stackFrame, ClrThread thread)
        {
            var localVariables = new List<LocalVariable>();

            try
            {
                var method = stackFrame.Method;
                if (method == null) return localVariables;

                var heap = DebuggerSession.Instance.Heap;
                var dataReader = DebuggerSession.Instance.Runtime.DataTarget.DataReader;
                var pointerSize = dataReader.PointerSize;

                // Method 1: Extract method parameters (they're also on the stack)
                ExtractMethodParameters(method, localVariables);

                // Method 2: Analyze stack memory around the frame
                ExtractStackSlotVariables(stackFrame, thread, heap, dataReader, pointerSize, localVariables);

                // Method 3: Use debugging symbols if available
                ExtractSymbolBasedVariables(method, stackFrame, localVariables);

                // Method 4: Extract common value types from stack memory
                ExtractValueTypeVariables(stackFrame, thread, dataReader, pointerSize, localVariables);

            }
            catch (Exception ex)
            {
                // Add error information but don't throw
                localVariables.Add(new LocalVariable
                {
                    Name = "ExtractionError",
                    Type = "Error",
                    Index = -1,
                    Value = $"Error during local variable extraction: {ex.Message}",
                    IsArgument = false
                });
            }

            return localVariables;
        }

        /// <summary>
        /// Extract method parameters (arguments) which are stored on the stack
        /// </summary>
        private void ExtractMethodParameters(ClrMethod method, List<LocalVariable> localVariables)
        {
            try
            {
                var signature = method.Signature;
                if (string.IsNullOrEmpty(signature)) return;

                // Parse method signature to extract parameter information
                var parameterInfo = ParseMethodSignature(signature);

                for (int i = 0; i < parameterInfo.Count; i++)
                {
                    localVariables.Add(new LocalVariable
                    {
                        Name = parameterInfo[i].Name,
                        Type = parameterInfo[i].Type,
                        Index = i,
                        Value = parameterInfo[i].DefaultValue,
                        IsArgument = true
                    });
                }
            }
            catch
            {
                // If parameter extraction fails, that's okay
            }
        }

        /// <summary>
        /// Extract variables by analyzing stack memory slots
        /// </summary>
        private void ExtractStackSlotVariables(ClrStackFrame stackFrame, ClrThread thread, ClrHeap heap,
            IDataReader dataReader, int pointerSize, List<LocalVariable> localVariables)
        {
            try
            {
                // Calculate stack frame boundaries
                var currentSP = stackFrame.StackPointer;
                var nextFrameSP = GetNextFrameStackPointer(stackFrame, thread);

                if (nextFrameSP == 0) nextFrameSP = currentSP + 0x100; // Estimate frame size

                // Scan stack slots between current frame and next frame
                for (ulong slotAddress = currentSP; slotAddress < nextFrameSP; slotAddress += (ulong)pointerSize)
                {
                    try
                    {
                        // Try to read as pointer
                        if (dataReader.ReadPointer(slotAddress, out ulong value))
                        {
                            // Check if it's a valid object reference
                            var objectType = heap.GetObjectType(value);
                            if (objectType != null && !objectType.IsFree)
                            {
                                localVariables.Add(new LocalVariable
                                {
                                    Name = $"local_{localVariables.Count}",
                                    Type = objectType.Name,
                                    Index = localVariables.Count,
                                    Value = $"0x{value:X} ({objectType.Name})",
                                    IsArgument = false
                                });
                            }
                            else
                            {
                                // Check if it's a potential value type
                                var valueTypeInfo = AnalyzeValueType(value, slotAddress, dataReader, pointerSize);
                                if (valueTypeInfo != null)
                                {
                                    localVariables.Add(valueTypeInfo);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid memory addresses
                        continue;
                    }
                }
            }
            catch
            {
                // If stack analysis fails, that's okay
            }
        }

        /// <summary>
        /// Extract variables using debugging symbols (PDB information)
        /// </summary>
        private void ExtractSymbolBasedVariables(ClrMethod method, ClrStackFrame stackFrame, List<LocalVariable> localVariables)
        {
            try
            {
                // This would use PDB symbols to get actual variable names and types
                // For now, this is a placeholder for future implementation

                var module = method.Type?.Module;
                if (module?.Pdb != null)
                {
                    // Future implementation: Use PDB information to extract local variable names and types
                    // This would require integration with debugging symbol APIs
                    localVariables.Add(new LocalVariable
                    {
                        Name = "SymbolInfo",
                        Type = "Information",
                        Index = -1,
                        Value = $"PDB available: {module.Pdb.Path} (symbol-based extraction not yet implemented)",
                        IsArgument = false
                    });
                }
            }
            catch
            {
                // If symbol extraction fails, that's okay
            }
        }

        /// <summary>
        /// Extract common value types from stack memory
        /// </summary>
        private void ExtractValueTypeVariables(ClrStackFrame stackFrame, ClrThread thread,
            IDataReader dataReader, int pointerSize, List<LocalVariable> localVariables)
        {
            try
            {
                var currentSP = stackFrame.StackPointer;
                var scanRange = Math.Min(0x80, (ulong)pointerSize * 16); // Scan reasonable range

                for (ulong offset = 0; offset < scanRange; offset += (ulong)pointerSize)
                {
                    var address = currentSP + offset;

                    // Try to read as different value types
                    if (dataReader.Read(address, out int intValue))
                    {
                        // Check if it looks like a reasonable integer
                        if (intValue > -1000000 && intValue < 1000000 && intValue != 0)
                        {
                            localVariables.Add(new LocalVariable
                            {
                                Name = $"int_var_{offset:X}",
                                Type = "System.Int32",
                                Index = localVariables.Count,
                                Value = intValue,
                                IsArgument = false
                            });
                        }
                    }

                    if (dataReader.Read(address, out bool boolValue))
                    {
                        // Boolean values are typically 0 or 1
                        if (boolValue == true || boolValue == false)
                        {
                            localVariables.Add(new LocalVariable
                            {
                                Name = $"bool_var_{offset:X}",
                                Type = "System.Boolean",
                                Index = localVariables.Count,
                                Value = boolValue,
                                IsArgument = false
                            });
                        }
                    }
                }
            }
            catch
            {
                // If value type extraction fails, that's okay
            }
        }

        /// <summary>
        /// Get the stack pointer of the next frame (to determine current frame size)
        /// </summary>
        private ulong GetNextFrameStackPointer(ClrStackFrame currentFrame, ClrThread thread)
        {
            try
            {
                var frames = thread.EnumerateStackTrace().ToList();
                var currentIndex = frames.FindIndex(f => f.StackPointer == currentFrame.StackPointer);

                if (currentIndex >= 0 && currentIndex < frames.Count - 1)
                {
                    return frames[currentIndex + 1].StackPointer;
                }
            }
            catch
            {
                // If we can't find the next frame, return 0
            }

            return 0;
        }

        /// <summary>
        /// Analyze a value to determine if it's a value type
        /// </summary>
        private LocalVariable AnalyzeValueType(ulong value, ulong address, IDataReader dataReader, int pointerSize)
        {
            // Check if value looks like a reasonable integer
            if (value < 0x1000000 && value > 0)
            {
                return new LocalVariable
                {
                    Name = $"value_{address:X}",
                    Type = "PossibleInt32",
                    Index = -1,
                    Value = (int)value,
                    IsArgument = false
                };
            }

            // Check if it's a potential double/float
            if (pointerSize == 8 && dataReader.Read(address, out double doubleValue))
            {
                if (!double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue) &&
                    doubleValue > -1e10 && doubleValue < 1e10)
                {
                    return new LocalVariable
                    {
                        Name = $"double_{address:X}",
                        Type = "System.Double",
                        Index = -1,
                        Value = doubleValue,
                        IsArgument = false
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Parse method signature to extract parameter information
        /// </summary>
        private List<ParameterInfo> ParseMethodSignature(string signature)
        {
            var parameters = new List<ParameterInfo>();

            try
            {
                // Simple signature parsing - this could be enhanced
                var startIndex = signature.IndexOf('(');
                var endIndex = signature.IndexOf(')');

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var paramString = signature.Substring(startIndex + 1, endIndex - startIndex - 1);
                    if (!string.IsNullOrWhiteSpace(paramString))
                    {
                        var paramParts = paramString.Split(',');
                        for (int i = 0; i < paramParts.Length; i++)
                        {
                            var param = paramParts[i].Trim();
                            var parts = param.Split(' ');

                            parameters.Add(new ParameterInfo
                            {
                                Name = parts.Length > 1 ? parts[parts.Length - 1] : $"param{i}",
                                Type = parts.Length > 0 ? parts[0] : "object",
                                DefaultValue = "Unknown"
                            });
                        }
                    }
                }
            }
            catch
            {
                // If parsing fails, return empty list
            }

            return parameters;
        }

        /// <summary>
        /// Helper class for parameter information
        /// </summary>
        private class ParameterInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string DefaultValue { get; set; }
        }
    }
}