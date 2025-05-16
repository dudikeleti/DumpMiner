using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpDisasm;
using System.ComponentModel.Composition;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpDisassemblyMethod, typeof(IDebuggerOperation))]
    class DumpDisassemblyMethodOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpDisassemblyMethod;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token,
            object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                ClrMethod method = DebuggerSession.Instance.Runtime.GetMethodByHandle(model.ObjectAddress);
                var results = new List<AssemblyCode>();

                if (method == null)
                {
                    ClrType type = DebuggerSession.Instance.Heap.GetTypeByName(model.Types);
                    if (type == null)
                    {
                        results.Add(new AssemblyCode
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
                        if (m.MetadataToken != (int)metadataToken)
                        {
                            continue;
                        }

                        method = m;
                        break;
                    }
                }

                if (method == null)
                {
                    results.Add(new AssemblyCode
                    {
                        Instruction = "Can not find method"
                    });
                    return results;
                }

                // use the IL to native mapping to get the end address
                if (method.ILOffsetMap == null)
                {
                    results.Add(new AssemblyCode
                    {
                        Instruction = "The method is not yet jited"
                    });
                    return results;
                }

                // This is the first instruction of the JIT'ed (or NGEN'ed) machine code.
                ulong startAddress = method.NativeCode;
                if (startAddress == 0)
                {
                    results.Add(new AssemblyCode
                    {
                        Instruction = "Unable to disassemble method"
                    });
                    return results;
                }

                foreach (var map in GetCompleteNativeMap(method))
                {
                    // Read the entire code block
                    byte[] codeBytes = new byte[(int)(map.EndAddress - map.StartAddress)];
                    var bytesRead = DebuggerSession.Instance.Runtime.DataTarget.DataReader.Read(map.StartAddress, codeBytes);

                    if (bytesRead != codeBytes.Length)
                    {
                        results.Add(new AssemblyCode
                        {
                            Instruction = $"Failed to read memory for disassembly at address {map.StartAddress:X}"
                        });
                        continue;
                    }

                    var disasm = new Disassembler(
                        codeBytes,
                        ArchitectureMode.x86_64,
                        map.StartAddress,
                        true
                    );

                    foreach (var instruction in disasm.Disassemble())
                    {
                        string mnemonicStr = instruction.Mnemonic.ToString();
        
                        string methodName = GetMethodNameFromAddressOrNull(mnemonicStr, instruction.Operands);
                        string instructionText = instruction.ToString();
        
                        if (!string.IsNullOrEmpty(methodName))
                        {
                            instructionText += $" --> {methodName}";
                        }
        
                        if (mnemonicStr.ToLower().Contains("nop"))
                        {
                            continue;
                        }
        
                        results.Add(new AssemblyCode
                        {
                            Instruction = instructionText
                        });
                    }
                }

                if (results.Count == 0)
                {
                    results.Add(new AssemblyCode
                    {
                        Instruction = $"Can not disassemble method: {method.Signature}"
                    });
                }

                return results;
            });
        }

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            var prompt = model.GptPrompt.ToString();
            if (prompt.Contains(Gpt.Variables.csharp))
            {
                var sourceCodes = (await App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.DumpSourceCode).Execute(model, token, null)).Cast<DumpSourceCodeOperation.SourceCode>();
                model.GptPrompt = model.GptPrompt.Replace(Gpt.Variables.csharp, sourceCodes.First().Code); // todo: support list of methods
            }

            if (prompt.Contains(Gpt.Variables.assembly))
            {
                var assemblyCode = string.Join(Environment.NewLine, items.Cast<AssemblyCode>());
                model.GptPrompt = model.GptPrompt.Replace(Gpt.Variables.assembly, assemblyCode);
            }

            return await Gpt.Ask(new[] { "You are an assembly code and a C# code expert." }, new[] { $"{model.GptPrompt}" });
        }

        private string GetMethodNameFromAddressOrNull(string opCode, Operand[] operands)
        {
            string address = null;
            string address2 = null;

            // Helper function to extract address/immediate from operand
            string GetAddressFromOperand(Operand operand)
            {
                if (operand == null)
                    return null;
                // For immediate, memory, or pointer operands, use Value
                if (operand.Type == SharpDisasm.Udis86.ud_type.UD_OP_IMM ||
                    operand.Type == SharpDisasm.Udis86.ud_type.UD_OP_MEM ||
                    operand.Type == SharpDisasm.Udis86.ud_type.UD_OP_PTR)
                    return operand.Value.ToString("X");
                return null;
            }

            if (opCode.ToLower().StartsWith("j"))
            {
                address = operands.Length > 3 ? GetAddressFromOperand(operands[3]) : null;
            }
            else if (opCode.ToLower() == "call")
            {
                address = operands.Length > 3 ? GetAddressFromOperand(operands[3]) : null;
                address2 = operands.Length > 4 ? GetAddressFromOperand(operands[4]) : null;
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
                methodName = DebuggerSession.Instance.Runtime.GetMethodByInstructionPointer(ulongAddress)?.Signature ??
                             DebuggerSession.Instance.Runtime.GetJitHelperFunctionName(ulongAddress);
            }

            if (methodName == null && address2 != null)
            {
                if (ulong.TryParse(SanitizeAddress(address2),
                        NumberStyles.HexNumber,
                        CultureInfo.CurrentCulture,
                        out ulongAddress))
                {
                    methodName =
                        DebuggerSession.Instance.Runtime.GetMethodByInstructionPointer(ulongAddress)?.Signature ??
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
                    ? new[]
                    {
                        new ILToNativeMap()
                        {
                            StartAddress = hotColdInfo.HotStart,
                            EndAddress = hotColdInfo.HotStart + hotColdInfo.HotSize, ILOffset = -1
                        }
                    }
                    : new[]
                    {
                        new ILToNativeMap()
                        {
                            StartAddress = hotColdInfo.HotStart,
                            EndAddress = hotColdInfo.HotStart + hotColdInfo.HotSize, ILOffset = -1
                        },
                        new ILToNativeMap()
                        {
                            StartAddress = hotColdInfo.ColdStart,
                            EndAddress = hotColdInfo.ColdStart + hotColdInfo.ColdSize, ILOffset = -1
                        }
                    };
            }

            return method.ILOffsetMap
                .Where(map => map.StartAddress < map.EndAddress) // some maps have 0 length?
                .OrderBy(map => map.StartAddress) // we need to print in the machine code order, not IL! #536
                .ToArray();
        }
    }

    class AssemblyCode
    {
        public string Address { get; set; }
        public string OpCode { get; set; }
        public string Instruction { get; set; }

        public override string ToString()
        {
            return $"{Address}: ${OpCode} {Instruction}";
        }
    }
}