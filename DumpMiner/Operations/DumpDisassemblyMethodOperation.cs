using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpDisassemblyMethod, typeof(IDebuggerOperation))]
    class DumpDisassemblyMethodOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpDisassemblyMethod;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customeParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                ClrType type = DebuggerSession.Instance.Runtime.GetHeap().GetTypeByName(model.Types);
                var results = new List<object>();
                if (type == null)
                {
                    results.Add(new
                    {
                        Address = "Can not find this type"
                    });
                    return results;
                }
                ulong metadataToken = model.ObjectAddress;
                foreach (ClrMethod method in type.Methods)
                {
                    if (token.IsCancellationRequested)
                        break;

                    // add also method name?
                    if (!method.Type.Name.StartsWith(model.Types) || method.MetadataToken != metadataToken) continue;
                    // This is the first instruction of the JIT'ed (or NGEN'ed) machine code.
                    ulong startAddress = method.NativeCode;

                    // use the IL to native mapping to get the end address
                    if (method.ILOffsetMap == null)
                    {
                        results.Add(new
                        {
                            Address = "The method is not yet jited"
                        });
                        break;
                    }
                    ulong endAddress = method.ILOffsetMap.Select(entry => entry.EndAddress).Max();
                    // the assembly code is in the range [startAddress, endAddress] inclusive.
                    var dbgCtrl = (IDebugControl)DebuggerSession.Instance.DataTarget.DebuggerInterface;
                    int size = Math.Max(1000000, (int)(endAddress + 1 - startAddress));
                    uint disassemblySize;
                    ulong nextInstruction;
                    var sb = new StringBuilder(size);
                    var result = dbgCtrl.Disassemble(startAddress, DEBUG_DISASM.EFFECTIVE_ADDRESS, sb, size, out disassemblySize, out nextInstruction);
                    var disassembly = sb.ToString().Split(' ').ToList();
                    disassembly.RemoveAll(s => s == "");

                    results.Add(new
                    {
                        Address = disassembly.Count > 1 ? disassembly[0] + "  " + disassembly[1] : "",
                        OpCode = disassembly.Count > 2 ? disassembly[2] : "",
                        Instruction = disassembly.Count > 4 ? disassembly[3] + " " + disassembly[4] : disassembly.Count > 3 ? disassembly[3] : "",
                    });
                    while (nextInstruction < endAddress)
                    {
                        startAddress = nextInstruction;
                        result = dbgCtrl.Disassemble(startAddress, DEBUG_DISASM.EFFECTIVE_ADDRESS, sb, size, out disassemblySize, out nextInstruction);
                        disassembly = sb.ToString().Split(' ').ToList();
                        disassembly.RemoveAll(s => s == "");
                        results.Add(new
                        {
                            Address = disassembly.Count > 1 ? disassembly[0] + "  " + disassembly[1] : "",
                            OpCode = disassembly.Count > 2 ? disassembly[2] : "",
                            Instruction = disassembly.Count > 4 ? disassembly[3] + " " + disassembly[4] : disassembly.Count > 3 ? disassembly[3] : "",
                        });
                    }
                }
                if (results.Count == 0)
                    results.Add(new
                    {
                        Address = "The metadata token does not exist in this type"
                    });
                return results;
            });
        }
    }
}
