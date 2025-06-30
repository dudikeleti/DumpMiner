using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Operations.Shared;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpDelegateMethod, typeof(IDebuggerOperation))]
    class DumpDelegateMethodOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpDelegateMethod;

        public override async Task<IEnumerable<object>> Execute(Models.OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                //var heap = DebuggerSession.Instance.Heap;
                var reader = DebuggerSession.Instance.Runtime.DataTarget.DataReader;
                // the first 8 bytes is some padding (maybe the first 5 is code stub and the rest some flags)
                // so we need to read the 7th byte to get the offset to first MethodDesc
                // and the 6th byte to see the offset inside the MethodDescs slots
                // then reading from the 8th byte + offset to first MethodDesc + offset to actual method
                // will give us the correct MethodDesc

                // It seems that in x86 process there isn't offsets at all, 
                // so just skip the 8 bytes padding and you will get the correct MethodDesc

                ulong methodHandle;
                if (DebuggerSession.Instance.Runtime.DataTarget.DataReader.PointerSize == 4)
                {
                    reader.ReadPointer(model.ObjectAddress + 8, out methodHandle);
                }
                else
                {
                    var offsetToFirstMethodDescByte = new byte[1];
                    var offsetInMethodDescTableByte = new byte[1];
                    reader.Read(model.ObjectAddress + 7, offsetToFirstMethodDescByte);
                    reader.Read(model.ObjectAddress + 6, offsetInMethodDescTableByte);
                    // Its only for x64 bit so multiply by 8 bytes
                    reader.ReadPointer(model.ObjectAddress + 8 + offsetToFirstMethodDescByte[0] * 8ul, out var firstMethodDesc);
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
                        Signature = method.Signature,
                        MetadataToken = method.MetadataToken,
                        MethodDesc = method.MethodDesc,
                        CompilationType = method.CompilationType,
                        EnclosingType = method.Type
                    }
                };
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Delegate Method Analysis: {operationResults.Count} methods");

            if (!operationResults.Any()) 
            {
                insights.AppendLine("⚠️ No delegate method found");
                return insights.ToString();
            }

            var method = operationResults.FirstOrDefault();
            if (method != null)
            {
                var signature = OperationHelpers.GetPropertyValue<string>(method, "Signature", "Unknown");
                var compilationType = OperationHelpers.GetPropertyValue(method, "CompilationType");
                var enclosingType = OperationHelpers.GetPropertyValue(method, "EnclosingType");
                
                insights.AppendLine($"Method signature: {signature}");
                insights.AppendLine($"Compilation type: {compilationType}");
                
                if (enclosingType != null)
                {
                    var typeName = OperationHelpers.GetPropertyValue<string>(enclosingType, "Name", "Unknown");
                    insights.AppendLine($"Enclosing type: {typeName}");
                }

                // Analyze compilation type for performance insights
                if (compilationType?.ToString() == "None")
                {
                    insights.AppendLine("\nPotential Issues:");
                    insights.AppendLine("  ⚠️ Method not JIT compiled - may affect performance");
                }
            }

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Delegate methods can affect performance if not properly compiled");
            insights.AppendLine("- Use DumpDisassemblyMethod for detailed assembly analysis");
            insights.AppendLine("- Check if method is being called frequently for optimization");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
DELEGATE METHOD ANALYSIS SPECIALIZATION:
- Focus on delegate method signatures and compilation status
- Analyze performance implications of delegate usage
- Identify potential memory leaks from uncollected delegates
- Look for event handler patterns and subscription issues

When analyzing delegate method data, pay attention to:
1. Method compilation status (JIT vs not compiled)
2. Method signature complexity and performance impact
3. Enclosing type patterns for event handling
4. Potential memory leaks from long-lived delegates
5. Multicast delegate chains that might affect performance
";
        }
    }
}
