using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
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

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                ClrMethod method = DebuggerSession.Instance.Runtime.GetMethodByHandle(model.ObjectAddress);
                var results = new List<object>();

                if (method == null)
                {
                    ClrType type = DebuggerSession.Instance.Heap.GetTypeByName(model.Types);
                    if (type == null)
                    {
                        results.Add(new
                        {
                            Instruction = "Can not find type"
                        });
                        return results;
                    }

                    ulong metadataToken = model.ObjectAddress;
                    foreach (ClrMethod m in type.Methods)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        // add also method name?
                        if (m.MetadataToken != metadataToken)
                        {
                            continue;
                        }

                        method = m;
                        break;
                    }
                }

                if (method == null)
                {
                    results.Add(new
                    {
                        Instruction = "Can not find method"
                    });
                    return results;
                }

                // This is the first instruction of the JIT'ed (or NGEN'ed) machine code.
                ulong startAddress = method.NativeCode;

                // use the IL to native mapping to get the end address
                if (method.ILOffsetMap == null)
                {
                    results.Add(new
                    {
                        Instruction = "The method is not yet jited"
                    });
                    return results;
                }

                // ulong endAddress = method.ILOffsetMap.Select(entry => entry.EndAddress).Max();

                foreach (var map in GetCompleteNativeMap(method))
                {
                    var dbgCtrl = (IDebugControl)DebuggerSession.Instance.DataTarget.DebuggerInterface;
                    int size = Math.Max(1000000, (int)(map.EndAddress + 1 - map.StartAddress));
                    var sb = new StringBuilder(size);

                    ulong nextInstruction = 0;
                    while (nextInstruction < map.EndAddress)
                    {
                        var result = dbgCtrl.Disassemble(startAddress, DEBUG_DISASM.EFFECTIVE_ADDRESS, sb, size, out var disassemblySize, out nextInstruction);
                        startAddress = nextInstruction;
                        if (result < 0)
                        {
                            continue;
                        }

                        var disassembly = sb.ToString().Split(' ').ToList();
                        disassembly.RemoveAll(s => s == "");
                        var address = disassembly.Count > 1 ? disassembly[0] + "  " + disassembly[1] : string.Empty;
                        var opcode = disassembly.Count > 2 ? disassembly[2] : string.Empty;
                        var instruction = disassembly.Count > 5
                            ? $"{disassembly[3]} {disassembly[4]} {disassembly[5]}"
                            : disassembly.Count > 4
                                ? $"{disassembly[3]} {disassembly[4]}"
                                : disassembly.Count > 3
                                    ? disassembly[3]
                                    : string.Empty;

                        var methodName = GetMethodNameFromAddressOrNull(opcode, disassembly);
                        if (string.IsNullOrEmpty(methodName) == false)
                        {
                            instruction = instruction.Replace("\n", string.Empty);
                            instruction += " --> " + methodName;
                        }

                        results.Add(new
                        {
                            Address = address,
                            OpCode = opcode,
                            Instruction = instruction
                        });
                    }
                }

                if (results.Count == 0)
                    results.Add(new
                    {
                        Instruction = $"Can not disassemble method: {method.GetFullSignature()}"
                    });
                return results;
            });
        }

        private string GetMethodNameFromAddressOrNull(string opCode, List<string> disassembly)
        {
            string address = null;
            string address2 = null;
            if (opCode.ToLower().StartsWith("j"))
            {
                address = disassembly.Count > 3 ? disassembly[3] : null;
            }
            else if (opCode.ToLower() == "call")
            {
                address = disassembly.Count > 3 ? disassembly[3] : null;
                address2 = disassembly.Count > 4 ? disassembly[4] : null;
            }
            else
            {
                return null;
            }

            if (address == null)
            {
                return null;
            }

            string methodName = null;
            if (ulong.TryParse(SanitizeAddress(address),
                    NumberStyles.HexNumber,
                    CultureInfo.CurrentCulture,
                    out var ulongAddress))
            {
                methodName = DebuggerSession.Instance.Runtime.GetMethodByAddress(ulongAddress)?.GetFullSignature() ??
                             DebuggerSession.Instance.Runtime.GetJitHelperFunctionName(ulongAddress);
            }

            if (methodName == null && address2 != null)
            {
                if (ulong.TryParse(SanitizeAddress(address2),
                        NumberStyles.HexNumber,
                        CultureInfo.CurrentCulture,
                        out ulongAddress))
                {
                    methodName = DebuggerSession.Instance.Runtime.GetMethodByAddress(ulongAddress)?.GetFullSignature() ??
                                 DebuggerSession.Instance.Runtime.GetJitHelperFunctionName(ulongAddress);
                }
            }

            return methodName;
        }

        private string SanitizeAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return address;
            }

            return address.Replace(Environment.NewLine, string.Empty).Replace("`", string.Empty)
                .Replace("(", string.Empty).Replace(")", string.Empty);
        }

        private static ILToNativeMap[] GetCompleteNativeMap(ClrMethod method)
        {
            // it's better to use one single map rather than few small ones
            // it's simply easier to get next instruction when decoding ;)
            var hotColdInfo = method.HotColdInfo;
            if (hotColdInfo.HotSize > 0 && hotColdInfo.HotStart > 0)
            {
                return hotColdInfo.ColdSize <= 0
                    ? new[] { new ILToNativeMap() { StartAddress = hotColdInfo.HotStart, EndAddress = hotColdInfo.HotStart + hotColdInfo.HotSize, ILOffset = -1 } }
                    : new[]
                    {
                        new ILToNativeMap() { StartAddress = hotColdInfo.HotStart, EndAddress = hotColdInfo.HotStart + hotColdInfo.HotSize, ILOffset = -1 },
                        new ILToNativeMap() { StartAddress = hotColdInfo.ColdStart, EndAddress = hotColdInfo.ColdStart + hotColdInfo.ColdSize, ILOffset = -1 }
                    };
            }

            return method.ILOffsetMap
                .Where(map => map.StartAddress < map.EndAddress) // some maps have 0 length?
                .OrderBy(map => map.StartAddress) // we need to print in the machine code order, not IL! #536
                .ToArray();
        }
    }
}
