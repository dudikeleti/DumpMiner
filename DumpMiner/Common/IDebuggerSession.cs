using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DumpMiner.Debugger;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Common
{
    interface IDebuggerSession : IDisposable
    {
        Action OnDetach { get; set; }
        void Attach(Process process, uint milliseconds);
        Task<bool> LoadDump(string fileName, CrashDumpReader readerType);
        void Detach();
        bool IsAttached { get; }
        void SetSymbolPath(string[] path, bool append);
        DataTarget DataTarget { get; }
        ClrRuntime Runtime { get; }
        ClrHeap Heap { get; }
        DateTime? AttachedTime { get; }
        Task<IEnumerable<object>?> ExecuteOperation(Func<IEnumerable<object>> operation);
        (int? id, string name) AttachedTo { get; }
    }
}
