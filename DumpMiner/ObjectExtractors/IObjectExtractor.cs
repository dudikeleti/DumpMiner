using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DumpMiner.ObjectExtractors
{
    interface IObjectExtractor
    {
        string GetFileNameSuffix();
        Task<bool> Extract(Stream output, ulong address, ulong size, string typeName);
    }
}
