using DumpMiner.Common;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;

namespace DumpMiner.Debugger
{
    internal class DebuggerSession : IDebuggerSession
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, int dwFlags);
        private const int LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100;

        #region members and props
        private readonly ILogger<DebuggerSession> _logger = LoggingExtensions.CreateLogger<DebuggerSession>();
        private static readonly TaskScheduler _singleThreadScheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;
        private Process _attachedProcess;
        private string _dumpPath;
        public static readonly IDebuggerSession Instance = new DebuggerSession();
        public bool IsAttached => DataTarget != null && Runtime != null;
        public DateTime? AttachedTime { get; private set; }
        public Action OnDetach { get; set; }
        public ClrRuntime Runtime { get; private set; }
        public ClrHeap Heap { get; private set; }
        public DataTarget DataTarget { get; private set; }
        public CrashDumpReader DumpReader { get; private set; }

        public (int? id, string name) AttachedTo => _attachedProcess != null ? (_attachedProcess.Id, _attachedProcess.ProcessName) : ((int?)null, _dumpPath);

        #endregion

        private DebuggerSession()
        {
            EnsureProperDebugEngineIsLoaded();
        }

        /// <summary>
        /// https://github.com/Microsoft/clrmd/issues/78
        /// </summary>
        private static void EnsureProperDebugEngineIsLoaded()
        {
            var sysdir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var res = LoadLibraryEx(Path.Combine(sysdir, "dbgeng.dll"), IntPtr.Zero, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR);
            if (res == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void SetSymbolPath(string[] path, bool append = true)
        {
            string symPath = string.Empty;
            symPath = append ? path.Aggregate(symPath, (current, s) => current + (s + ";")) : path.Single();

            DataTarget.SetSymbolPath(symPath);

            //if (string.IsNullOrEmpty(DataTarget.SymbolLocator.SymbolCache))
            //{
            //    DataTarget.SymbolLocator.SymbolCache = Properties.Resources.SymbolCache;
            //}
        }

        public async Task<IEnumerable<object>?> ExecuteOperation(Func<IEnumerable<object>> operation)
        {
            using var debuggerOperation = _logger.LogOperation("Execute debugger operation", operation.Method.Name);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // All operations must be serialized since they access shared DebuggerSession state
                // (Runtime, Heap, DataTarget instances) which could have race conditions even if
                // the underlying ClrMD library is thread-safe
                var result = await Task.Factory.StartNew(operation, CancellationToken.None, TaskCreationOptions.None, _singleThreadScheduler);

                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Operation {Name} failed", operation.Method.Name);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogMemoryUsage($"AfterDebuggerOperation: {operation.Method.Name}");
                _logger.LogPerformance(operation.Method.Name, stopwatch.Elapsed);
            }
        }

        #region Attach\Detach
        public async Task<bool> LoadDump(string fileName, CrashDumpReader readerType)
        {
            if (IsAttached)
            {
                return true;
            }

            DumpReader = readerType;

            // Use the single thread scheduler for all dump loading operations
            return await Task.Factory.StartNew(() => LoadDump(fileName), 
                CancellationToken.None, TaskCreationOptions.None, _singleThreadScheduler);
        }

        private bool LoadDump(string fileName)
        {
            DataTarget = DataTarget.LoadDump(fileName);
            var result = CreateRuntime();
            if (!result.succeeded)
            {
                App.Container.GetExport<IDialogService>().Value.ShowDialog(result.error);
                Dispose(true);
            }

            _dumpPath = fileName;
            AttachedTime = File.GetLastWriteTime(_dumpPath);
            return result.succeeded;
        }

        public void Attach(Process process, uint milliseconds)
        {
            if (IsAttached)
            {
                return;
            }

            _attachedProcess = process;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    // DataTarget = DataTarget.CreateSnapshotAndAttach(_attachedProcess.Id);
                    DataTarget = DataTarget.AttachToProcess(_attachedProcess.Id, true, null);
                    var result = CreateRuntime();
                    if (!result.succeeded)
                    {
                        Dispose(true);
                        App.Container.GetExport<IDialogService>().Value.ShowDialog(result.error);
                        return;
                    }

                    AttachedTime = DateTime.Now;
                    _attachedProcess.Exited += Process_Exited;
                }
                catch (Exception)
                {
                    try
                    {
                        Dispose(true);
                    }
                    catch { }
                }
            }, CancellationToken.None, TaskCreationOptions.None, _singleThreadScheduler).Wait();
        }

        private (bool succeeded, string error) CreateRuntime()
        {
            GC.ReRegisterForFinalize(this);
            try
            {
                var clrVersion = DataTarget.ClrVersions.FirstOrDefault();
                if (clrVersion == null)
                {
                    return (false, "CLR version not found");
                }

                Runtime = clrVersion.CreateRuntime();
                Heap = Runtime.Heap;
                if (Heap == null || !Heap.CanWalkHeap)
                {
                    return (false, "Can't get heap");
                }

                SetSymbolPath(new[] { Environment.CurrentDirectory, Properties.Resources.SymbolCache, Properties.Resources.DllsFolder });
                return (true, string.Empty);
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        }

        public void Detach()
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                }
                finally
                {
                    try
                    {
                        Dispose(true);
                        OnDetach?.Invoke();
                    }
                    catch { }
                }
            }, CancellationToken.None, TaskCreationOptions.None, _singleThreadScheduler);
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            Dispose(true);
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (DataTarget != null)
                {
                    DataTarget.Dispose();
                    DataTarget = null;
                }
                if (_attachedProcess != null)
                {
                    _attachedProcess.Dispose();
                    _attachedProcess = null;
                }

                _dumpPath = null;
                GC.SuppressFinalize(this);
            }
        }

        ~DebuggerSession()
        {
            Dispose(false);
        }

        #endregion
    }

    internal enum CrashDumpReader
    {
        ClrMD,
        DbgEng
    }
}
