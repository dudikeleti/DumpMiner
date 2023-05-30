﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Models;

namespace DumpMiner.Operations
{
    [Export(OperationNames.DumpHeapSegments, typeof(IDebuggerOperation))]
    class DumpHeapSegmentsOperation : IDebuggerOperation
    {
        public string Name => OperationNames.DumpHeapSegments;

        public async Task<IEnumerable<object>> Execute(OperationModel model, CancellationToken token, object customParameter)
        {
            return await DebuggerSession.Instance.ExecuteOperation(() =>
            {
                var heap = DebuggerSession.Instance.Heap;
                var enumerable = from segment in heap.Segments
                                 let type = segment.IsEphemeral ? "Ephemeral" : segment.IsLarge ? "Large" : "Ephemeral"
                                 select new
                                 {
                                     Start = segment.Start,
                                     End = segment.End,
                                     Committed = segment.CommittedEnd,
                                     Reserved = segment.ReservedEnd,
                                     ProcessorAffinity = segment.ProcessorAffinity,
                                     Type = type,
                                     Length = segment.Length,
                                     NotInUse = segment.CommittedEnd - segment.End
                                 };
                return enumerable.ToList();
            });
        }

        public async Task<string> AskGpt(OperationModel model, Collection<object> items, CancellationToken token, object parameter)
        {
            throw new System.NotImplementedException();
        }
    }
}
