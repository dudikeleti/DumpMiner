using System.Collections.Generic;
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
    [Export(OperationNames.DumpMethods, typeof(IDebuggerOperation))]
    class DumpMethodsOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpMethods;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customeParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                ClrType type = DebuggerSession.Instance.Runtime.GetHeap().GetTypeByName(model.Types);

                if (type == null) return new List<object>();
                var enumerable = from method in type.Methods
                                 where method != null
                                 select new
                                 {
                                     MetadataToken = method.MetadataToken,
                                     Signature = method.GetFullSignature(),
                                     CompilationType = method.CompilationType,
                                     IsStatic = method.IsStatic
                                 };
                return enumerable.ToList();
            });
        }
    }
}
