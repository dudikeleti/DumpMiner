using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpTypeInfo, typeof(IDebuggerOperation))]
    class DumpTypeInfoOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpTypeInfo;

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

                List<object> result = new List<object>();
                foreach (var propertyInfo in type.GetType().GetProperties())
                {
                    object value = propertyInfo.GetValue(type);
                    if (value == null)
                    {
                        continue;
                    }

                    if (propertyInfo.Name == "MetadataToken" || propertyInfo.Name == "MethodTable")
                    {
                        value = $"0x{System.Convert.ToUInt64(value):X8}";
                    }

                    result.Add(new { Name = propertyInfo.Name, Value = value });
                }

                return result;
            });
        }

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new System.NotImplementedException();
        }
    }
}
