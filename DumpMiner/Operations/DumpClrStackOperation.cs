using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Operations
{
    // !ClrStack
    [Export(OperationNames.DumpClrStack, typeof(IDebuggerOperation))]
    class DumpClrStackOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpClrStack;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
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
                    stackDetails.StackFrames = new List<object>();
                    stackDetails.StackObjects = new List<object>();
                    foreach (var stackFrame in thread.StackTrace)
                    {
                        stackDetails.StackBase = thread.StackBase;
                        stackDetails.StackLimit = thread.StackLimit;
                        stackDetails.Exception = thread.CurrentException;
                        stackDetails.OSThreadID = thread.IsAlive ? thread.OSThreadId.ToString() : "XXX";
                        stackDetails.ManagedThreadId = thread.ManagedThreadId;
                        stackDetails.StackFrames.Add(
                        new
                        {
                            StackPointer = stackFrame.StackPointer,
                            InstructionPointer = stackFrame.InstructionPointer,
                            DisplayString = stackFrame.DisplayString,
                            // FileAndLine = source != null ? source.FilePath + ": " + source.LineNumber : "",
                            Method = stackFrame.Method
                        });

                        if (token.IsCancellationRequested)
                            break;
                    }

                    ClrHeap heap = DebuggerSession.Instance.Heap;
                    var pointerSize = DebuggerSession.Instance.Runtime.PointerSize;

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
                        if (!heap.ReadPointer(ptr, out ulong obj))
                            break;

                        // the object added already
                        if (!stackObjects.Add(obj))
                            continue;

                        // not an object
                        ClrType type = heap.GetObjectType(obj);
                        if (type == null)
                            continue;

                        // free space
                        if (type.IsFree) continue;

                        // All good, add it 
                        stackDetails.StackObjects.Add(
                            new
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
            public List<object> StackObjects { get; set; }
            public List<object> StackFrames { get; set; }
        }
    }
}