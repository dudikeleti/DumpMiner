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

namespace DumpMiner.Operations
{
    [Export(OperationNames.GetObjectSize, typeof(IDebuggerOperation))]
    class GetObjectSizeOperation : IDebuggerOperation
    {
        private CancellationToken _token;
        public string Name => OperationNames.GetObjectSize;

        public async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                _token = token;
                uint count;
                ulong size;
                GetObjSize(DebuggerSession.Instance.Heap, model.ObjectAddress, out count, out size);
                var enumerable = new List<object> { new { ReferencedCount = count, TotalSize = size } };
                var results = new List<object>();
                foreach (var item in enumerable)
                {
                    results.Add(item);
                    if (token.IsCancellationRequested)
                        break;
                }
                return results;
            });
        }

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new System.NotImplementedException();
        }

        private void GetObjSize(ClrHeap heap, ulong obj, out uint count, out ulong size)
        {
            // Evaluation stack
            var eval = new Stack<ulong>();
            var considered = new HashSet<ulong>();

            count = 0;
            size = 0;
            eval.Push(obj);

            while (eval.Count > 0)
            {
                obj = eval.Pop();
                if (!considered.Add(obj))
                    continue;

                ClrType type = heap.GetObjectType(obj);
                if (type == null)
                    continue;

                count++;
                size += heap.GetObject(obj).Size;

                // Manually enumerate all references from this object
                EnumerateObjectReferences(heap, obj, type, considered, eval);
            }
        }

        // Helper to enumerate all references from an object (fields, arrays, structs)
        private void EnumerateObjectReferences(ClrHeap heap, ulong obj, ClrType type, HashSet<ulong> considered, Stack<ulong> eval)
        {
            if (type == null || type.IsFree)
                return;

            if (type.IsArray)
            {
                var arrayObj = heap.GetObject(obj);
                int len = arrayObj.AsArray().Length;
                var compType = type.ComponentType;
                if (compType != null)
                {
                    if (compType.IsObjectReference)
                    {
                        for (int i = 0; i < len; i++)
                        {
                            ulong elementAddress = type.GetArrayElementAddress(obj, i);
                            if (elementAddress != 0 && considered.Add(elementAddress))
                                eval.Push(elementAddress);
                        }
                    }
                    else if (compType.ElementType == ClrElementType.Struct)
                    {
                        for (int i = 0; i < len; i++)
                        {
                            ulong elementAddress = type.GetArrayElementAddress(obj, i);
                            EnumerateStructReferences(heap, elementAddress, compType, considered, eval);
                        }
                    }
                }
                return;
            }

            foreach (var field in type.Fields)
            {
                if (field.IsObjectReference)
                {
                    ulong address = field.GetAddress(obj, false);
                    if (address != 0 && considered.Add(address))
                        eval.Push(address);
                }
                else if (field.ElementType == ClrElementType.Struct && field.Type != null)
                {
                    ulong structAddress = field.GetAddress(obj, false);
                    EnumerateStructReferences(heap, structAddress, field.Type, considered, eval);
                }
            }
        }

        // Helper to enumerate references in a struct
        private void EnumerateStructReferences(ClrHeap heap, ulong structAddress, ClrType structType, HashSet<ulong> considered, Stack<ulong> eval)
        {
            if (structType == null) return;
            foreach (var field in structType.Fields)
            {
                if (field.IsObjectReference)
                {
                    ulong address = field.GetAddress(structAddress, false);
                    if (address != 0 && considered.Add(address))
                        eval.Push(address);
                }
                else if (field.ElementType == ClrElementType.Struct && field.Type != null)
                {
                    ulong nestedStructAddress = field.GetAddress(structAddress, false);
                    EnumerateStructReferences(heap, nestedStructAddress, field.Type, considered, eval);
                }
            }
        }
    }
}
