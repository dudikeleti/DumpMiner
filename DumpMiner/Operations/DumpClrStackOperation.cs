using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
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
                    stackDetails.StackFrames = new List<Frame>();
                    stackDetails.StackObjects = new List<StackObject>();
                    foreach (var stackFrame in thread.StackTrace)
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

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            var prompt = model.GptPrompt.ToString();
            if (prompt.Contains(Gpt.Variables.callstack))
            {
                var callstack = GetCallstack(items.Cast<ClrStackDump>());
                model.GptPrompt = model.GptPrompt.Replace(Gpt.Variables.callstack, callstack);
            }

            if (prompt.Contains(Gpt.Variables.values))
            {
                var values = GetFrameValues(items.Cast<ClrStackDump>());
                model.GptPrompt = model.GptPrompt.Replace(Gpt.Variables.values, values);
            }

            return await Gpt.Ask(new[] { "You are an assembly code and a C# code expert." }, new[] { $"{model.GptPrompt}" });
        }

        private string GetFrameValues(IEnumerable<ClrStackDump> callstack)
        {
            // todo: format values in a sensible json
            return string.Join(Environment.NewLine,
                callstack.SelectMany(frame =>
                    frame.StackObjects.Select(so =>
                        $"{so.Name}: {string.Join(Environment.NewLine, so.Value.Select(v => $"{v.FieldName} = {v.Value}"))}")));
        }

        private string GetCallstack(IEnumerable<ClrStackDump> callstack)
        {
            return string.Join(Environment.NewLine,
                callstack.SelectMany(frame => frame.StackFrames.Select(frame2 => frame2.DisplayString)));
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