using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;

namespace DumpMiner.Operations
{
    [Export(OperationNames.TypeFromHandle, typeof(IDebuggerOperation))]
    class TypeFromHandleOperation : IDebuggerOperation
    {
        public string Name => OperationNames.TypeFromHandle;

        public async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;

                var type = heap.GetTypeByMethodTable(model.ObjectAddress);

                if (type == null)
                {
                    return new[] { new { Name = "Type not found" } };
                }

                return new[]
                 {
                        new
                        {
                            Name = type.Name,
                            BaseTYpe = type.BaseType.Name,
                            MetadataToken = type.MetadataToken,
                            MethodTable = type.MethodTable,
                        }
                };
            });
        }

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new System.NotImplementedException();
        }
    }
}
