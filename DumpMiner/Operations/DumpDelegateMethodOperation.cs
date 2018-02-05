using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpDelegateMethod, typeof(IDebuggerOperation))]
    class DumpDelegateMethodOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpDelegateMethod;

        public async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customeParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Runtime.GetHeap();

                // the first 8 bytes is some padding (maybe the first 5 is code stub and the rest some flags)
                // so we need to read the 7th byte to get the offset to first MethodDesc
                // and the 6th byte to see the offset inside the MethodDescs slots
                // then reading from the 8th byte + offset to first MethodDesc + offset to actual method
                // will give us the correct MethodDesc

                // It seems that in x86 process there isn't offsets at all, 
                // so just skip the 8 bytes padding and you will get the correct MethodDesc

                ulong methodHandle;
                if (DebuggerSession.Instance.Runtime.PointerSize == 4)
                {
                    heap.ReadPointer(model.ObjectAddress + 8, out methodHandle);
                }
                else
                {
                    var offsetToFirstMethodDescByte = new byte[1];
                    var offsetInMethodDescTableByte = new byte[1];
                    heap.ReadMemory(model.ObjectAddress + 7, offsetToFirstMethodDescByte, 0, 1);
                    heap.ReadMemory(model.ObjectAddress + 6, offsetInMethodDescTableByte, 0, 1);
                    // Its only for x64 bit so multiply by 8 bytes
                    heap.ReadPointer(model.ObjectAddress + 8 + offsetToFirstMethodDescByte[0] * 8ul, out var firstMethodDesc);
                    methodHandle = firstMethodDesc + offsetInMethodDescTableByte[0] * 8ul;
                }

                var method = DebuggerSession.Instance.Runtime.GetMethodByHandle(methodHandle);

                //var methodPtrWithOffset = model.ObjectAddress + 5;
                ////heap.ReadPointer( model.ObjectAddress, out ulong someVal3);
                ////heap.ReadPointer(methodPtrWithOffset, out ulong someVal4);
                //heap.ReadPointer(methodPtrWithOffset + 2, out ulong someVal1);
                //heap.ReadPointer(methodPtrWithOffset + 1, out ulong someVal2);
                //heap.ReadPointer(methodPtrWithOffset + (someVal1 & 0xFF) * 8 + 3, out ulong baseMethodDesc);
                ////var offset = Environment.Is64BitProcess ? (someVal2 & 0xFF) * 8 : (someVal2 & 0xFF) * 4;
                //var offset = (someVal2 & 0xFF) * (ulong)DebuggerSession.Instance.Runtime.PointerSize;
                //var handle = baseMethodDesc + offset;
                //var method = DebuggerSession.Instance.Runtime.GetMethodByHandle(handle);

                if (method == null)
                {
                    return new[] { new { Signature = "Method not found" } };
                }

                return new[]
                {
                    new
                    {
                        Signature = method.GetFullSignature(),
                        MetadataToken = method.MetadataToken,
                        MethodDesc = method.MethodDesc,
                        CompilationType = method.CompilationType,
                        EnclosingType = method.Type
                    }
                };
            });
        }
    }
}
