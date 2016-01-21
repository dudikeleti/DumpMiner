using DumpMiner.Common;

namespace DumpMiner.Infrastructure.Mef
{
    public interface IViewModelLoader
    {
        BaseViewModel Load(IHasViewModel viewVm);
        void Unload<T>();
    }
}
