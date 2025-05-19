





using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using Microsoft.Diagnostics.Runtime;
using SharpDisasm;
using SharpDisasm.Udis86;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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





                /*
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
                */


                //foreach (var map in GetCompleteNativeMap(method))


                var hc = method.HotColdInfo;
                if (hc.HotSize == 0 || hc.HotStart == 0)
                {
                    results.Add(new AssemblyCode
                    {
                        Instruction = "The method is not yet jited"
                    });
                    return results;
                }

                var dt = DebuggerSession.Instance.DataTarget;
                var runtime = DebuggerSession.Instance.Runtime;

                ulong baseAddr = hc.HotStart;
                int size = checked((int)hc.HotSize);
                byte[] bytes = new byte[size];
                var read = dt.DataReader.Read(baseAddr, bytes);
                if (read != size)
                {
                    results.Add(new AssemblyCode
                    {
                        Instruction = $"Failed to read memory for disassembly at address {baseAddr:X}"
                    });
                    return results;
                }

                // 4) Disassemble with SharpDisasm
                var disasm = new Disassembler(
                    bytes,
                    dt.DataReader.PointerSize == 8
                        ? ArchitectureMode.x86_64
                        : ArchitectureMode.x86_32,
                    baseAddr,
                    true);

                //Disassembler.Translator.IncludeAddress = true;
                //Disassembler.Translator.IncludeBinary = true;
                //Disassembler.Translator.ResolveRip = true;

                foreach (var instruction in disasm.Disassemble())
                {
                    if (instruction.Mnemonic.ToString().EndsWith("nop"))
                    {
                        continue;
                    }

                    string instructionText = instruction.ToString();

                    if (instruction.Mnemonic == ud_mnemonic_code.UD_Icall
                        && instruction.Operands.Length > 0)
                    {
                        string methodName = ResolveMethodFromAddress(instruction, baseAddr, bytes);
                        if (!string.IsNullOrEmpty(methodName))
                        {
                            
                            instructionText += $"    --> {methodName}";
                        }
                    }

                    results.Add(new AssemblyCode
                    {
                        OpCode = $"{instruction.Mnemonic,-8}",
                        Address = $"{instruction.Offset:X8}",
                        Instruction = instructionText
                    });
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

        public string ResolveMethodFromAddress(
            Instruction instr,
            ulong codeBaseAddress,
            byte[] codeBytes)
        {
            var dataTarget = DebuggerSession.Instance.DataTarget;
            var runtime = DebuggerSession.Instance.Runtime;

            // 1) Find the OS thread whose stack is executing in our code blob:
            var thread = runtime.Threads
                .FirstOrDefault(t => t.EnumerateStackTrace()
                    .Any(f => f.InstructionPointer >= codeBaseAddress
                              && f.InstructionPointer < codeBaseAddress + (ulong)codeBytes.Length));
            //if (thread == null)
            //    return "No thread owns this code region";

            AMD64Context cpuCtx = default;

            if (thread != null)
            {
                uint osTid = thread.OSThreadId;

                // 2) Grab the raw CONTEXT block via ClrMD v3:
                int ctxSize = Marshal.SizeOf<AMD64Context>();
                byte[] ctxBuf = new byte[ctxSize];
                bool got = dataTarget.DataReader.GetThreadContext(osTid, 0, ctxBuf);
                if (!got)
                    return $"GetThreadContext failed for TID {osTid}";

                cpuCtx = MemoryMarshal.Read<AMD64Context>(ctxBuf);
            }

            // 3) Figure out the native call target
            var op = instr.Operands.First();
            ulong targetAddress;

            switch (op.Type)
            {
                // direct relative/JIMM → absolute VA already in RawValue
                case ud_type.UD_OP_IMM:
                case ud_type.UD_OP_JIMM:
                    targetAddress = Convert.ToUInt64(op.RawValue);
                    break;

                // register-indirect: call rax, rcx, etc.
                case ud_type.UD_OP_REG:
                    if (thread == null)
                    {
                        targetAddress = 0;
                        break;
                    }

                    targetAddress = op.Base switch
                    {
                        ud_type.UD_R_RAX => cpuCtx.Rax,
                        ud_type.UD_R_RCX => cpuCtx.Rcx,
                        ud_type.UD_R_RDX => cpuCtx.Rdx,
                        ud_type.UD_R_RBX => cpuCtx.Rbx,
                        ud_type.UD_R_RSP => cpuCtx.Rsp,
                        ud_type.UD_R_RBP => cpuCtx.Rbp,
                        ud_type.UD_R_RSI => cpuCtx.Rsi,
                        ud_type.UD_R_RDI => cpuCtx.Rdi,
                        ud_type.UD_R_R8 => cpuCtx.R8,
                        ud_type.UD_R_R9 => cpuCtx.R9,
                        ud_type.UD_R_R10 => cpuCtx.R10,
                        ud_type.UD_R_R11 => cpuCtx.R11,
                        ud_type.UD_R_R12 => cpuCtx.R12,
                        ud_type.UD_R_R13 => cpuCtx.R13,
                        ud_type.UD_R_R14 => cpuCtx.R14,
                        ud_type.UD_R_R15 => cpuCtx.R15,
                        _ => throw new NotSupportedException(
                            $"Unsupported REG operand {op.Base}")
                    };
                    break;

                // memory-indirect: can be [imm], [RIP+disp], [reg+disp], [reg+reg*scale+disp], etc.
                case ud_type.UD_OP_MEM:
                    {
                        // 3a) compute effective address of the *pointer slot*
                        //    disp = signed RawValue
                        long disp = Convert.ToInt64(op.RawValue);

                        //    baseVal = (op.Base==NONE ? 0 : register value)
                        ulong baseVal = thread == null ? 0 :op.Base switch
                        {
                            ud_type.UD_NONE => 0UL,
                            ud_type.UD_R_RAX => cpuCtx.Rax,
                            ud_type.UD_R_RCX => cpuCtx.Rcx,
                            ud_type.UD_R_RDX => cpuCtx.Rdx,
                            ud_type.UD_R_RBX => cpuCtx.Rbx,
                            ud_type.UD_R_RSP => cpuCtx.Rsp,
                            ud_type.UD_R_RBP => cpuCtx.Rbp,
                            ud_type.UD_R_RSI => cpuCtx.Rsi,
                            ud_type.UD_R_RDI => cpuCtx.Rdi,
                            ud_type.UD_R_R8 => cpuCtx.R8,
                            ud_type.UD_R_R9 => cpuCtx.R9,
                            ud_type.UD_R_R10 => cpuCtx.R10,
                            ud_type.UD_R_R11 => cpuCtx.R11,
                            ud_type.UD_R_R12 => cpuCtx.R12,
                            ud_type.UD_R_R13 => cpuCtx.R13,
                            ud_type.UD_R_R14 => cpuCtx.R14,
                            ud_type.UD_R_R15 => cpuCtx.R15,
                            ud_type.UD_R_RIP // Udis86 may call RIP “a register”
                                => codeBaseAddress + (ulong)instr.Offset + (ulong)instr.Length,
                            _ => throw new NotSupportedException(
                                $"Unsupported MEM.Base {op.Base}")
                        };

                        //    idxVal = (op.Index==NONE ? 0 : register value * scale)
                        ulong idxVal = thread == null ? 0 : op.Index switch
                        {
                            ud_type.UD_NONE => 0UL,
                            ud_type.UD_R_RAX => cpuCtx.Rax * (ulong)op.Scale,
                            ud_type.UD_R_RCX => cpuCtx.Rcx * (ulong)op.Scale,
                            ud_type.UD_R_RDX => cpuCtx.Rdx * (ulong)op.Scale,
                            ud_type.UD_R_RBX => cpuCtx.Rbx * (ulong)op.Scale,
                            ud_type.UD_R_RSP => cpuCtx.Rsp * (ulong)op.Scale,
                            ud_type.UD_R_RBP => cpuCtx.Rbp * (ulong)op.Scale,
                            ud_type.UD_R_RSI => cpuCtx.Rsi * (ulong)op.Scale,
                            ud_type.UD_R_RDI => cpuCtx.Rdi * (ulong)op.Scale,
                            ud_type.UD_R_R8 => cpuCtx.R8 * (ulong)op.Scale,
                            ud_type.UD_R_R9 => cpuCtx.R9 * (ulong)op.Scale,
                            ud_type.UD_R_R10 => cpuCtx.R10 * (ulong)op.Scale,
                            ud_type.UD_R_R11 => cpuCtx.R11 * (ulong)op.Scale,
                            ud_type.UD_R_R12 => cpuCtx.R12 * (ulong)op.Scale,
                            ud_type.UD_R_R13 => cpuCtx.R13 * (ulong)op.Scale,
                            ud_type.UD_R_R14 => cpuCtx.R14 * (ulong)op.Scale,
                            ud_type.UD_R_R15 => cpuCtx.R15 * (ulong)op.Scale,
                            _ => throw new NotSupportedException(
                                $"Unsupported MEM.Index {op.Index}")
                        };

                        ulong ptrSlot = baseVal + idxVal + (ulong)disp;

                        // 3b) read the *pointer* stored at that slot
                        targetAddress = dataTarget.DataReader.ReadPointer(ptrSlot);
                    }
                    break;

                default:
                    return $"Unsupported CALL operand {op.Type}";
            }

            // 4) Finally map to ClrMethod
            // Map back to managed method or JIT helper
            var m = runtime.GetMethodByInstructionPointer(targetAddress);
            string name = m != null
                ? m.Signature
                : runtime.GetJitHelperFunctionName(targetAddress)
                  ?? $"<unknown 0x{targetAddress:X}>";

            return name;
            // return $"0x{instr.Offset:X8}: {instr.Mnemonic} -> 0x{targetAddress:X8}    {sig}";
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

        private static ILToNativeMap[] GetCompleteNativeMap(ClrMethod method)
        {
            // it's better to use one single map rather than few small ones
            // it's simply easier to get next instruction when decoding ;)
            var hotColdInfo = method.HotColdInfo;
            if (hotColdInfo is { HotSize: > 0, HotStart: > 0 })
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

    //[StructLayout(LayoutKind.Sequential)]
    //public unsafe struct AMD64Context
    //{
    //    public fixed byte Omitted[512];

    //    // integer registers
    //    public ulong Rax, Rcx, Rdx, Rbx;
    //    public ulong Rsp, Rbp, Rsi, Rdi;
    //    public ulong R8, R9, R10, R11;
    //    public ulong R12, R13, R14, R15;

    //    // instruction pointer
    //    public ulong Rip;
    //}
}