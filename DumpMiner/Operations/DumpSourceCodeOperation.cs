using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;
using DumpMiner.Reader;
using DumpMiner.Services.AI;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.Extensions.Logging;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpSourceCode, typeof(IDebuggerOperation))]
    internal class DumpSourceCodeOperation : BaseAIOperation
    {
        private readonly ILogger<DumpSourceCodeOperation> _logger = LoggingExtensions.CreateLogger<DumpSourceCodeOperation>();

        public override string Name => OperationNames.DumpSourceCode;

        public override async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
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
            _logger.LogInformation($"Saving all modules to {pathToOutputModules}");
            dumpData.SaveAllModules(pathToOutputModules, true);
            var dlls = Directory.EnumerateFiles(pathToOutputModules).Where(f => Path.GetFileNameWithoutExtension(f).Equals(Path.GetFileNameWithoutExtension(dumpPath))).Select(f => new FileInfo(f));
            var settings = new DecompilerSettings();
            settings.ThrowOnAssemblyResolveErrors = false;
            var resolver = new UniversalAssemblyResolver(dlls.First().FullName, false, null);
            var decompiler = new CSharpDecompiler(dlls.First().FullName, resolver, settings);

            string code = decompiler.DecompileAsString(MetadataTokens.EntityHandle(metadataToken));
            return code;
        }


        protected override void AddOperationSpecificSuggestions(
            StringBuilder insights,
            Collection<object> operationResults,
            Dictionary<string, int> typeGroups)
        {
            var sourceCodeItems = operationResults.OfType<SourceCode>().ToList();

            if (sourceCodeItems.Any())
            {
                insights.AppendLine($"• Source code analysis: {sourceCodeItems.Count} code blocks available");
                insights.AppendLine("• Examine decompiled code for memory leaks, performance issues, and threading problems");
                insights.AppendLine("• Consider DumpMethods for method-level performance analysis");
                insights.AppendLine("• Use DumpObject to investigate specific object instances referenced in the code");
            }
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
