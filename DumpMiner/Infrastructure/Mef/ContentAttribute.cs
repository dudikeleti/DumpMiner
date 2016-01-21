using System;
using System.ComponentModel.Composition;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.Infrastructure.Mef
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class)]
    public class ContentAttribute : ExportAttribute
    {
        public ContentAttribute(string contentUri)
            : base(typeof(IContent))
        {
            ContentUri = contentUri;
        }

        public string ContentUri { get; private set; }
    }
}
