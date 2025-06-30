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
            //TODO: support local variables 
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var result = new List<ClrStackDump>();
                foreach (var thread in DebuggerSession.Instance.Runtime.Threads)
                {
                    if (token.IsCancellationRequested)
                        break;

                    var stackDetails = new ClrStackDump();
                    stackDetails.StackFrames = new List<Frame>();
                    stackDetails.StackObjects = new List<StackObject>();
                    foreach (var stackFrame in thread.EnumerateStackTrace(true))
                    {
                        stackDetails.StackBase = thread.StackBase;
                        stackDetails.StackLimit = thread.StackLimit;
                        stackDetails.Exception = thread.CurrentException;
                        stackDetails.OSThreadID = thread.IsAlive ? thread.OSThreadId.ToString() : "XXX";
                        stackDetails.ManagedThreadId = thread.ManagedThreadId;
                        stackDetails.StackFrames.Add(
                        new Frame
                        {
                            StackPointer = stackFrame.StackPointer,
                            InstructionPointer = stackFrame.InstructionPointer,
                            DisplayString = stackFrame.ToString(),
                            // FileAndLine = source != null ? source.FilePath + ": " + source.LineNumber : "",
                            Method = stackFrame.Method
                        });

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
                                // Value = new Microsoft.Diagnostics.Runtime.ClrObject(obj, type);
                                Value = new DumpMiner.Debugger.ClrObject(obj, type, token).Fields.Value
                            });

                        if (token.IsCancellationRequested)
                            break;
                    }
                    result.Add(stackDetails);
                }
                return result;
            });
        }

        protected override void AddOperationSpecificSuggestions(
            StringBuilder insights,
            Collection<object> operationResults,
            Dictionary<string, int> typeGroups)
        {
            // Stack-specific suggestions
            insights.AppendLine("• Stack frames available - recommend DumpSourceCode for code analysis");
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
        }

        private class StackObject
        {
            public ulong Address { get; set; }
            public object Object { get; set; }
            public string Name { get; set; }
            public List<ClrObject.ClrObjectModel> Value { get; set; }
        }
    }
}