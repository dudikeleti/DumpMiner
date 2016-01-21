using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace DumpMiner.Infrastructure.Mef
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class CommandAttribute : ExportAttribute
    {
        public CommandAttribute(string commandUri)
            : base(commandUri, typeof(ICommand))
        {
            CommandUri = commandUri;
        }
        public string CommandUri { get; private set; }
    }
}
