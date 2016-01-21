using System;
using System.ComponentModel.Composition;
using DumpMiner.Common;

namespace DumpMiner.Infrastructure.Mef
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class)]
    public class ViewModelAttribute : ExportAttribute
    {
        public ViewModelAttribute(string name)
            : base(typeof(BaseViewModel))
        {
            Name = name;
        }

        public string Name { get; private set; }
    }
}
