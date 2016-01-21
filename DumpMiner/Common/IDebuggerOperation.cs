using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Models;

namespace DumpMiner.Common
{
    public interface IDebuggerOperation
    {
        string Name { get; }
        Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customeParameter);
    }
}
