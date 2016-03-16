using System;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Reflection;
using DumpMiner.Common;

namespace DumpMiner.Infrastructure.Mef
{
    public class OperationViewAttribute : ViewAttribute
    {
        //private static MethodInfo _getExportMethod;

        public OperationViewAttribute(string contentUri, string displayName = "")
            : base(contentUri, displayName)
        {
            //if (_getExportMethod == null)
            //{
            //    _getExportMethod = (from method in typeof(CompositionContainer).GetMethods()
            //                        where method.Name == "GetExport" && method.IsGenericMethodDefinition
            //                        let genericArgs = method.GetGenericArguments()
            //                        let parameters = method.GetParameters()
            //                        where genericArgs.Length == 1 && parameters.Length == 0
            //                        select method).Single().MakeGenericMethod(new Type[] { type });
            //}

            //var s = typeof(CompositionContainer).GetMethod("GetExportedValue", new Type[0]).MakeGenericMethod(new Type[] { type });
            //Operation = (IDebuggerOperation)s.Invoke(App.Container, null);
            //Operation = (IDebuggerOperation)((dynamic)_getExportMethod.Invoke(App.Container, null)).Value;
        }

        public IDebuggerOperation Operation { get; private set; }
    }
}
