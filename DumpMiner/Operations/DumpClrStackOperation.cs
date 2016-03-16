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

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customeParameter)
        {
            //TODO: add support of local variables
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
                            //FileAndLine = source != null ? source.FilePath + ": " + source.LineNumber : "",
                            Method = stackFrame.Method
                        });
                    }

                    // We'll need heap data to find objects on the stack.
                    ClrHeap heap = DebuggerSession.Instance.Runtime.GetHeap();
                    var pointerSize = DebuggerSession.Instance.Runtime.PointerSize;
                    // Walk each pointer aligned address on the stack.  Note that StackBase/StackLimit
                    // is exactly what they are in the TEB.  This means StackBase > StackLimit on AMD64.
                    ulong start = thread.StackBase;
                    ulong stop = thread.StackLimit;

                    // We'll walk these in pointer order.
                    if (start > stop)
                    {
                        ulong tmp = start;
                        start = stop;
                        stop = tmp;
                    }

                    // Walk each pointer aligned address.  Ptr is a stack address.
                    for (ulong ptr = start; ptr <= stop; ptr += (ulong)pointerSize)
                    {
                        // Read the value of this pointer.  If we fail to read the memory, break.  The
                        // stack region should be in the crash dump.
                        ulong obj;
                        if (!DebuggerSession.Instance.Runtime.ReadPointer(ptr, out obj))
                            break;

                        // We check to see if this address is a valid object by simply calling
                        // GetObjectType.  If that returns null, it's not an object.
                        ClrType type = heap.GetObjectType(obj);
                        if (type == null)
                            continue;

                        // Don't print out free objects as there tends to be a lot of them on
                        // the stack.
                        if (type.IsFree) continue;

                        stackDetails.StackObjects.Add(
                            new
                            {
                                Address = ptr,
                                Object = obj,
                                Name = type.Name,
                                Value = new ClrObject(obj, type).Fields.Value
                            });
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