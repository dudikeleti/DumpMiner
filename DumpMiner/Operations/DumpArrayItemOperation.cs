using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using Microsoft.Diagnostics.Runtime;
using static DumpMiner.Debugger.ClrObject;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpArrayItem, typeof(IDebuggerOperation))]
    class DumpArrayItemOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpArrayItem;

        public async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                ClrType type = heap.GetObjectType(model.ObjectAddress);
                if (type == null)
                {
                    return null;
                }

                if (!type.IsArray)
                {
                    throw new ArgumentException($"Type is not array{Environment.NewLine}Type is {type.Name}");
                }

                if (type.ComponentType != null && !type.ComponentType.HasSimpleValue)
                {
                    throw new NotImplementedException();
                }

                var index = int.Parse(model.Types);
                var itemValue = type.GetArrayElementValue(model.ObjectAddress, index);
                var itemAddress = type.GetArrayElementAddress(model.ObjectAddress, index);
                if (type.ComponentType == null || type.ComponentType.IsPrimitive)
                {
                    // return new List<ClrObjectModel>() { new ClrObjectModel() { Address = itemAddress, Value = itemValue, MetadataToken = type.ComponentType?.MetadataToken ?? 0, Offset = (ulong)(type.ElementSize * index), TypeName = type.ComponentType?.Name ?? type.Name.Replace("[]", string.Empty) } };
                    return new DumpMiner.Debugger.ClrObject(itemAddress, type.ComponentType, token).Fields.Value;
                }

                if (type.ComponentType.Name == "System.String")
                {
                    throw new NotImplementedException();
                }

                if (type.ComponentType?.IsObjectReference == true)
                {
                    return new DumpMiner.Debugger.ClrObject((ulong)itemValue, heap.GetObjectType((ulong)itemValue), token).Fields.Value;
                }

                return null;
            });
        }
    }
}
