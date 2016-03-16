using System;
using System.ComponentModel.Composition;

namespace DumpMiner.Infrastructure.Mef
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class)]
    public class ViewAttribute : ContentAttribute
    {
        public ViewAttribute(string contentUri, string displayName = "")
            : base(contentUri)
        {
            DisplayName = displayName;
        }

        public string DisplayName { get; private set; }
    }
}
