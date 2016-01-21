using System.Collections.Generic;

namespace DumpMiner.Common
{
    public interface IHasViewModel
    {
        bool IsViewModelLoaded { get; set; }
        string ViewModelName { get; }
        Dictionary<string, object> ExtendedData { get; }
    }
}
