using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Reader;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpSourceCode, typeof(IDebuggerOperation))]
    internal class DumpSourceCodeOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpSourceCode;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            // todo: support list of methods
            return new object[] { new SourceCode { Code = GetSourceCode((int)model.ObjectAddress) } };
        }

        private string GetSourceCode(int metadataToken)
        {
            var dumpPath = DebuggerSession.Instance.AttachedTo.name;
            var dumpData = new Dump(dumpPath);
            var pathToOutputModules = $"{Environment.CurrentDirectory}\\DumpOutputModules";
            Directory.CreateDirectory(pathToOutputModules);
            Console.WriteLine($"Saving all modules to {pathToOutputModules}");
            dumpData.SaveAllModules(pathToOutputModules, true);
            var dlls = Directory.EnumerateFiles(pathToOutputModules).Where(f => Path.GetFileNameWithoutExtension(f).Equals(Path.GetFileNameWithoutExtension(dumpPath))).Select(f => new FileInfo(f));
            var settings = new DecompilerSettings();
            settings.ThrowOnAssemblyResolveErrors = false;
            var resolver = new UniversalAssemblyResolver(dlls.First().FullName, false, null);
            var decompiler = new CSharpDecompiler(dlls.First().FullName, resolver, settings);

            string code = decompiler.DecompileAsString(MetadataTokens.EntityHandle(metadataToken));
            return code;
        }


        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object customParameter)
        {
            var prompt = model.GptPrompt.ToString();
            if (prompt.Contains("{c#}"))
            {
                var assemblyCode = string.Join(Environment.NewLine, items.Cast<SourceCode>());
                model.GptPrompt = model.GptPrompt.Replace("{c#}", assemblyCode);
            }

            return await Gpt.Ask(new[] { "You are an assembly code and a C# code expert." }, new[] { $"{model.GptPrompt}" });
        }

        internal class SourceCode
        {
            public string Code { get; set; }

            public override string ToString()
            {
                return Code;
            }
        }
    }
}
