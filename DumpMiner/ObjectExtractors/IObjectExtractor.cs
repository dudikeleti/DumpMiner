using System.Threading.Tasks;

namespace DumpMiner.ObjectExtractors
{
    interface IObjectExtractor
    {
        string GetFileNameSuffix();
        Task<bool> Extract(string path, ulong address, ulong size, string typeName);
    }
}
