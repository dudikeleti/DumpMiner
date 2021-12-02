using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DumpMiner.ObjectExtractors
{
    static class ObjectExtraction
    {
        public static IObjectExtractor FindExtractor(string type)
        {
            switch (type)
            {
                case "System.Drawing.Bitmap":
                    return new BitmapExtractor();
            }

            return null;
        }
    }
}
