using System;
using System.ComponentModel.Composition;
using System.Linq;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.Infrastructure.Mef
{
    [Export]
    public class MefContentLoader : DefaultContentLoader
    {
        [ImportMany]
        private Lazy<IContent, IContentMetadata>[] Contents { get; set; }

        protected override object LoadContent(Uri uri)
        {
            // lookup the content based on the content uri in the content metadata
            var content = (from c in Contents
                           where c.Metadata.ContentUri == uri.OriginalString
                           select c.Value).FirstOrDefault();

            if (content == null)
            {
                throw new ArgumentException("Invalid uri: " + uri);
            }

            return content;
        }
    }
}
