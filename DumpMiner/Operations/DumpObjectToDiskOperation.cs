using DumpMiner.Common;
using DumpMiner.Operations.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Models;
using DumpMiner.Debugger;
using System.Text;
using DumpMiner.ObjectExtractors;
using Microsoft.Win32;
using System.Windows;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpObjectToDisk, typeof(IDebuggerOperation))]
    internal class DumpObjectToDiskOperation : BaseAIOperation
    {
        public override string Name => OperationNames.DumpObjectToDisk;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var address = model.ObjectAddress;
                var typeName = model.Types;
                var size = (ulong)customParameter
                    ;
                if (address == 0)
                {
                    App.Dialog.ShowDialog("Selected object address is zero. Cannot dump.", title: "Error");
                    return null;
                }

                if (size == 0)
                {
                    App.Dialog.ShowDialog("Selected object size is zero. Cannot dump.", title: "Error");
                    return null;
                }

                if (size > int.MaxValue)
                {
                    App.Dialog.ShowDialog("Selected object size is too large. Cannot dump.", title: "Error");
                    return null;
                }

                try
                {
                    byte[] buffer = new byte[size];
                    var bytesRead = DebuggerSession.Instance.DataTarget.DataReader.Read(address, buffer);
                    if (bytesRead <= 0)
                    {
                        App.Dialog.ShowDialog("Could not read process memory.", title: "Error");
                        return null;
                    }

                    if ((uint)bytesRead < size)
                    {
                        byte[] buffer2 = new byte[bytesRead];
                        Array.Copy(buffer, buffer2, bytesRead);
                        buffer = buffer2;
                    }

                    string baseFileName = $"pid_{DebuggerSession.Instance.AttachedTo.id?.ToString() ?? "none"}_obj_{address:x16}_{bytesRead:x8}";

                    var file = new SaveFileDialog
                    {
                        Filter = "All files (*.*)|*.*",
                        Title = "Choose a file name without extensions",
                        CheckPathExists = true,
                        OverwritePrompt = true,
                        InitialDirectory = Environment.CurrentDirectory,
                        FileName = baseFileName
                    };

                    var result = file.ShowDialog();
                    if (!result.HasValue || !result.Value || string.IsNullOrEmpty(file.FileName))
                    {
                        return null;
                    }

                    var dumpDescription = new StringBuilder();
                    dumpDescription.AppendLine("DumpMiner object dump");
                    dumpDescription.AppendLine($"Time: {DateTime.Now.ToString(CultureInfo.CurrentCulture)}");
                    dumpDescription.AppendLine($"Process ID: {DebuggerSession.Instance.AttachedTo.id?.ToString() ?? "N/A"}");
                    dumpDescription.AppendLine($"Process Name: {DebuggerSession.Instance.AttachedTo.name ?? "N/A"}");
                    dumpDescription.AppendLine($"Dumped object type: {typeName}");
                    dumpDescription.AppendLine($"Dumped object address: 0x{address:x16} ({address})");
                    dumpDescription.AppendLine($"Dumped object size: 0x{size:x8} ({size})");

                    File.WriteAllBytes(file.FileName + ".bin", buffer);
                    File.WriteAllText(file.FileName + ".txt", dumpDescription.ToString());

                    App.Dialog.ShowDialog($"Dumped {bytesRead} raw bytes from address 0x{address:x16} to {file.FileName}.bin", title: "Info");

                    var extractor = ObjectExtraction.FindExtractor(typeName);
                    if (extractor == null)
                    {
                        return new[] { new { Status = "Dumped", BytesWritten = bytesRead, FilePath = file.FileName + ".bin" } };
                    }

                    if (App.Dialog.ShowDialog($"The type {typeName} can be extracted from memory. Would you like to do this?",
                            "Extract?", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    {
                        return new[] { new { Status = "Dumped", BytesWritten = bytesRead, FilePath = file.FileName + ".bin" } };
                    }

                    string extractFileName = file.FileName + extractor.GetFileNameSuffix();
                    var success = extractor.Extract(extractFileName, address, size, typeName).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (success)
                    {
                        App.Dialog.ShowDialog($"Extraction completed successfully to {file.FileName}");
                        return new[] { new { Status = "Dumped and Extracted", BytesWritten = bytesRead, FilePath = file.FileName + ".bin", ExtractedPath = extractFileName } };
                    }
                    else
                    {
                        App.Dialog.ShowDialog("Extraction failed.", title: "Error");
                        try
                        {
                            File.Delete(extractFileName);
                        }
                        catch
                        {
                            // ignored
                        }
                        return new[] { new { Status = "Dumped (Extraction Failed)", BytesWritten = bytesRead, FilePath = file.FileName + ".bin" } };
                    }

                }
                catch (Exception ex)
                {
                    App.Dialog.ShowDialog($"An exception occurred while dumping the object: {ex}", title: "Error");
                    return new[] { new { Status = "Failed", Error = ex.Message } };
                }
            });
        }

        public override string GetAIInsights(Collection<object> operationResults)
        {
            var insights = new System.Text.StringBuilder();
            insights.AppendLine($"Object Dump Analysis: {operationResults.Count} operations");

            if (!operationResults.Any()) 
            {
                insights.AppendLine("⚠️ No dump operations completed");
                return insights.ToString();
            }

            var result = operationResults.FirstOrDefault();
            if (result != null)
            {
                var status = OperationHelpers.GetPropertyValue<string>(result, "Status", "Unknown");
                var bytesWritten = OperationHelpers.GetPropertyValue<int>(result, "BytesWritten", 0);
                var filePath = OperationHelpers.GetPropertyValue<string>(result, "FilePath", "");
                var extractedPath = OperationHelpers.GetPropertyValue<string>(result, "ExtractedPath", "");
                var error = OperationHelpers.GetPropertyValue<string>(result, "Error", "");

                insights.AppendLine($"Status: {status}");
                
                if (bytesWritten > 0)
                {
                    insights.AppendLine($"Bytes written: {OperationHelpers.FormatSize(bytesWritten)}");
                }

                if (!string.IsNullOrEmpty(filePath))
                {
                    insights.AppendLine($"Raw dump file: {Path.GetFileName(filePath)}");
                }

                if (!string.IsNullOrEmpty(extractedPath))
                {
                    insights.AppendLine($"Extracted file: {Path.GetFileName(extractedPath)}");
                }

                if (!string.IsNullOrEmpty(error))
                {
                    insights.AppendLine($"Error: {error}");
                }

                // Analyze potential issues
                if (status.Contains("Failed"))
                {
                    insights.AppendLine("\nPotential Issues:");
                    insights.AppendLine("  🚨 Dump operation failed - check memory permissions");
                }
                else if (bytesWritten > 100_000_000) // 100MB
                {
                    insights.AppendLine("\nNote:");
                    insights.AppendLine("  ⚠️ Large object dumped - consider memory usage impact");
                }
            }

            insights.AppendLine("\nKey Information:");
            insights.AppendLine("- Raw binary data saved for external analysis");
            insights.AppendLine("- Object extractors can convert specific types to usable formats");
            insights.AppendLine("- Use hex editors to examine raw memory layout");

            return insights.ToString();
        }

        public override string GetSystemPromptAdditions()
        {
            return @"
OBJECT DUMP ANALYSIS SPECIALIZATION:
- Focus on memory extraction and forensic analysis
- Analyze dump success/failure patterns
- Identify extractable object types and formats
- Look for memory corruption or access issues

When analyzing object dump data, pay attention to:
1. Dump operation success/failure rates
2. Object size patterns and memory usage
3. File format extraction capabilities
4. Memory access violations or corruption
5. Opportunities for specialized object extractors
";
        }
    }
}
