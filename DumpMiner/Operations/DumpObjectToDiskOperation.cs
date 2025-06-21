using DumpMiner.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
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
    internal class DumpObjectToDiskOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpObjectToDisk;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
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
                        return null;
                    }

                    if (App.Dialog.ShowDialog($"The type {typeName} can be extracted from memory. Would you like to do this?",
                            "Extract?", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    {
                        return null;
                    }

                    string extractFileName = file.FileName + extractor.GetFileNameSuffix();
                    var success = extractor.Extract(extractFileName, address, size, typeName).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (success)
                    {
                        App.Dialog.ShowDialog($"Extraction completed successfully to {file.FileName}");
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
                    }

                }
                catch (Exception ex)
                {
                    App.Dialog.ShowDialog($"An exception occurred while dumping the object: {ex}", title: "Error");
                }

                return null;
            });
        }

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object customParameter)
        {
            throw new NotImplementedException();
        }
    }
}
