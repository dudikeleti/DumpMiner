﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
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

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                ClrType type = DebuggerSession.Instance.Heap.GetTypeByName(model.Types) ??
                               DebuggerSession.Instance.Heap.GetObjectType(model.ObjectAddress) ??
                               DebuggerSession.Instance.Heap.GetTypeByMethodTable(model.ObjectAddress);
                
                if (type == null)
                {
                    return new List<object>();
                }

                var enumerable = from method in type.Methods
                                 where method != null
                                 select new
                                 {
                                     MetadataToken = method.MetadataToken,
                                     Signature = method.Signature,
                                     CompilationType = method.CompilationType,
                                     IsStatic = method.Attributes | MethodAttributes.Static,
                                     MethodDesc = method.MethodDesc
                                 };
                return enumerable.ToList();
            });
        }

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new System.NotImplementedException();
        }
    }
}
