﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DumpMiner.Common;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;

namespace DumpMiner.Debugger
{
    internal class DebuggerSession : IDebuggerSession
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, int dwFlags);
        private const int LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100;

        #region members and props
        private readonly Task _workerTask;
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
            _workerTask = new Task(() => { });
            _workerTask.Start();
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
            if (append)
            {
                foreach (var s in path)
                {
                    DataTarget.SymbolLocator.SymbolPath += ";" + s;
                }

            }
            else
            {
                DataTarget.SymbolLocator.SymbolPath = path.Single();
            }

            if (string.IsNullOrEmpty(DataTarget.SymbolLocator.SymbolCache))
            {
                DataTarget.SymbolLocator.SymbolCache = Properties.Resources.SymbolCache;
            }
        }

        public async Task<IEnumerable<object>> ExecuteOperation(Func<IEnumerable<object>> operation)
        {
            IEnumerable<object> result = null;
            if (DumpReader == CrashDumpReader.ClrMD)
            {
                await Task.Run(() => { result = operation(); });
            }
            else
            {
                await _workerTask.ContinueWith(t => { result = operation(); });
            }
            return result;
        }

        #region Attach\Detach
        public async Task<bool> LoadDump(string fileName, CrashDumpReader readerType)
        {
            if (IsAttached)
            {
                return true;
            }

            DumpReader = readerType;
            if (readerType == CrashDumpReader.DbgEng)
            {
                return await _workerTask.ContinueWith(task => LoadDump(fileName));
            }

            return await Task.Run(() => LoadDump(fileName)).ConfigureAwait(false);
        }

        private bool LoadDump(string fileName)
        {
            DataTarget = DataTarget.LoadCrashDump(fileName, DumpReader);
            var result = CreateRuntime();
            if (!result.succeeded)
            {
                App.Container.GetExport<IDialogService>().Value.ShowDialog(result.error);
                Dispose(true);
            }

            _dumpPath = fileName;
            return result.succeeded;
        }

        public void Attach(Process process, uint milliseconds)
        {
            if (IsAttached)
            {
                return;
            }

            _attachedProcess = process;

            _workerTask.ContinueWith(task =>
            {
                try
                {
                    DataTarget = DataTarget.AttachToProcess(_attachedProcess.Id, milliseconds, AttachFlag.NonInvasive);
                    var result = CreateRuntime();
                    if (!result.succeeded)
                    {
                        Dispose(true);
                        App.Container.GetExport<IDialogService>().Value.ShowDialog(result.error);
                        return;
                    }

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
            }).Wait();
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

                AttachedTime = GetAttachedTime();
                SetSymbolPath(new[] { Environment.CurrentDirectory, Properties.Resources.SymbolCache, Properties.Resources.DllsFolder });
                // Runtime.RuntimeFlushed += runtime => Heap = Runtime.Heap;
                return (true, string.Empty);
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        }

        private DateTime? GetAttachedTime()
        {
            try
            {
                var dbgCtrl2 = (IDebugControl2)Runtime.DataTarget.DebuggerInterface;
                if (dbgCtrl2 == null)
                {
                    if (_attachedProcess != null)
                    {
                        return null;
                    }

                    return DateTime.MinValue;
                }

                dbgCtrl2.GetCurrentTimeDate(out var secondsSinceUnix);
                var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                return origin.AddSeconds(secondsSinceUnix).ToLocalTime();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Detach()
        {
            _workerTask.ContinueWith(t =>
            {
                try
                {
                    DataTarget.DebuggerInterface?.DetachProcesses();
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
            });
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
}
