using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Infrastructure;
using DumpMiner.Infrastructure.Mef;
using DumpMiner.Models;
using FirstFloor.ModernUI.Presentation;

namespace DumpMiner.ViewModels
{
    [ViewModel(ViewModelNames.BaseOperationViewModel)]
    public class BaseOperationViewModel : BaseViewModel
    {
        private readonly List<object[]> _results;
        private int _resultsCurrentIndex;
        public OperationModel Model { get; set; }
        protected CancellationTokenSource CancellationTokenSource;
        private readonly TimeSpan _defaultTimeout;

        public BaseOperationViewModel()
        {
            CancelOperationVisibility = Visibility.Collapsed;
            Model = new OperationModel();
            _results = new List<object[]>();
            var setting = SettingsManager.Instance.ReadSettingValue(SettingsManager.DefaultTimeout);
            if (int.TryParse(setting, out var timeout))
            {
                _defaultTimeout = TimeSpan.FromSeconds(timeout);
            }
            else
            {
                _defaultTimeout = TimeSpan.FromSeconds(60);
            }
        }

        public virtual IDebuggerOperation Operation { get; set; }

        private int _count;
        public int Count
        {
            get { return _count; }
            set
            {
                _count = value;
                OnPropertyChanged();
            }
        }

        private bool _isGptEnabled;
        public bool IsGptEnabled
        {
            get { return _isGptEnabled; }
            set
            {
                _isGptEnabled = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<object> _items;
        public ObservableCollection<object> Items
        {
            get { return _items; }
            set
            {
                _items = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<GptChat> Conversation
        {
            get { return Model.Chat; }
            set
            {
                Model.Chat = value;
                OnPropertyChanged();
            }
        }

        private string _gptQuestion;
        public string GptQuestion
        {
            get { return _gptQuestion; }
            set
            {
                _gptQuestion = value;
                OnPropertyChanged();
            }
        }

        public StringBuilder GptPrompt
        {
            get { return Model.GptPrompt; }
            set
            {
                Model.GptPrompt = value;
                OnPropertyChanged();
            }
        }

        public int NumOfResults
        {
            get { return Model.NumOfResults; }
            set
            {
                Model.NumOfResults = value;
                OnPropertyChanged();
            }
        }

        public string Types
        {
            get { return Model.Types; }
            set
            {
                Model.Types = value;
                OnPropertyChanged();
            }
        }

        private Visibility _cancelOperationVisibility;
        public Visibility CancelOperationVisibility
        {
            get { return _cancelOperationVisibility; }
            set
            {
                _cancelOperationVisibility = value;
                OnPropertyChanged();
            }
        }

        public ulong ObjectAddress
        {
            get { return Model.ObjectAddress; }
            set
            {
                Model.ObjectAddress = value;
                OnPropertyChanged();
            }
        }

        public virtual async void ExecuteOperation(object o)
        {
            if (!DebuggerSession.Instance.IsAttached)
            {
                DebuggerSession.Instance.Detach();
                App.Container.GetExport<IDialogService>().Value.ShowDialog("Process is detached");
                return;
            }
            CancelOperationVisibility = Visibility.Visible;
            Items = null;
            Count = 0;
            IsLoading = true;
            IEnumerable<object> result = null;
            CancellationTokenSource = new CancellationTokenSource(_defaultTimeout);
            try
            {
                result = await Operation.Execute(Model, CancellationTokenSource.Token, o);
            }
            catch (OperationCanceledException)
            {
                App.Container.GetExport<IDialogService>().Value.ShowDialog("Operation is canceled");
            }
            catch (Exception ex)
            {
                App.Container.GetExport<IDialogService>().Value.ShowDialog($"Exception{Environment.NewLine}{ex.Message}{Environment.NewLine}{ex.StackTrace.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)[0]}");
            }

            if (result != null)
            {
                Items = new ObservableCollection<object>(result);
                if (Items.Count > 0)
                    _results.Add(Items.ToArray());
            }

            OnOperationCompleted();
            //Observe(result, _cancellationToken.Token).SubscribeOn(Scheduler.Default).Buffer(TimeSpan.FromMilliseconds(100))
            //    .ObserveOn(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher, DispatcherPriority.Normal))
            //    .Subscribe(
            //        onNext =>
            //        {
            //            if (onNext == null) return;
            //            foreach (var value in onNext)
            //                Items.Add(value);
            //            Count = Items.Count;
            //            OnPropertyChanged("Count");
            //        },
            //        ex =>
            //        {
            //            OnOperationCompleted();
            //            if (ex is OperationCanceledException)
            //                App.Container.GetExport<IDialogService>().Value.ShowDialog("Operation is canceled");
            //        },
            //        OnOperationCompleted);
        }

        public async void AskGpt(object o)
        {
            if (!DebuggerSession.Instance.IsAttached)
            {
                DebuggerSession.Instance.Detach();
                App.Container.GetExport<IDialogService>().Value.ShowDialog("Process is detached");
                return;
            }

            Conversation.Add(new GptChat { Text = GptQuestion, Type = "@user" });

            CancelOperationVisibility = Visibility.Visible;
            IsLoading = true;
            CancellationTokenSource = new CancellationTokenSource(_defaultTimeout);
            GptPrompt.Append(GptQuestion);
            try
            {
                var gptResult = await Operation.AskGpt(Model, Items, CancellationTokenSource.Token, o);
                Conversation.Add(new GptChat { Text = gptResult, Type = "@gpt" });
                GptPrompt.Append(gptResult);
            }
            catch (OperationCanceledException)
            {
                App.Container.GetExport<IDialogService>().Value.ShowDialog("Operation is canceled");
            }
            catch (Exception ex)
            {
                App.Container.GetExport<IDialogService>().Value.ShowDialog($"Exception{Environment.NewLine}{ex.Message}{Environment.NewLine}{ex.StackTrace.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)[0]}");
            }

            IsLoading = false;
            CancelOperationVisibility = Visibility.Collapsed;
            CancellationTokenSource.Dispose();
            CancellationTokenSource = null;
            GptQuestion = null;
        }

        private void OnOperationCompleted()
        {
            IsLoading = false;
            CancelOperationVisibility = Visibility.Collapsed;
            if (Items != null)
                Count = Items.Count;
            if (Count > 0)
                _resultsCurrentIndex = _results.Count - 1;

            IsGptEnabled = true;
            CancellationTokenSource.Dispose();
            CancellationTokenSource = null;
            ((RelayCommand)GoToNextResultCommand).OnCanExecuteChanged();
            ((RelayCommand)GoToPreResultCommand).OnCanExecuteChanged();
        }

        //private IObservable<object> Observe(IEnumerable<object> enumerable, CancellationToken token)
        //{
        //return Observable.Create<object>(observer =>
        // {
        //     try
        //     {
        //         var items = new List<object>();
        //         int count = 0;
        //         foreach (var item in enumerable)
        //         {
        //             token.ThrowIfCancellationRequested();
        //             items.Add(item);
        //             observer.OnNext(item);
        //             if (++count > 500)
        //             {
        //                 Thread.Sleep(8);
        //                 count = 0;
        //             }
        //         }
        //         if (items.Count > 0)
        //             _results.Add(items.ToArray());
        //         observer.OnCompleted();
        //     }
        //     catch (Exception ex)
        //     {
        //         observer.OnError(ex);
        //     }

        //     return Disposable.Empty;
        // });
        //}

        private RelayCommand _cancelOperationCommand;
        [Command("cmd://OperationCommands/CancelOperationCommand")]
        public RelayCommand CancelOperationCommand
        {
            get
            {
                return _cancelOperationCommand ?? (_cancelOperationCommand = new RelayCommand(o =>
                {
                    if (CancellationTokenSource != null)
                        CancellationTokenSource.Cancel();
                }));
            }
        }

        private ICommand _executeOperationCommand;
        public virtual ICommand ExecuteOperationCommand
        {
            get
            {
                return _executeOperationCommand ??
                    (_executeOperationCommand = new RelayCommand(ExecuteOperation,
                        o => Operation != null && DebuggerSession.Instance.IsAttached));
            }
        }

        private ICommand _goToPreResultCommand;
        public ICommand GoToPreResultCommand
        {
            get
            {
                return _goToPreResultCommand ??
                    (_goToPreResultCommand = new RelayCommand(o =>
                    {
                        Items = new ObservableCollection<object>(_results[--_resultsCurrentIndex]);
                        Count = Items.Count;
                    },
                        o => _results.Count > 0 && _resultsCurrentIndex > 0 && Operation != null && DebuggerSession.Instance.IsAttached));
            }
        }

        private ICommand _goToNextResultCommand;
        public ICommand GoToNextResultCommand
        {
            get
            {
                return _goToNextResultCommand ??
                    (_goToNextResultCommand = new RelayCommand(o =>
                    {
                        Items = new ObservableCollection<object>(_results[++_resultsCurrentIndex]);
                        Count = Items.Count;
                    },
                        o => _results.Count > 0 && _resultsCurrentIndex + 1 < _results.Count && Operation != null && DebuggerSession.Instance.IsAttached));
            }
        }

        private ICommand _askGptCommand;
        public ICommand AskGptCommand
        {
            get
            {
                return _askGptCommand ??
                       (_askGptCommand = new RelayCommand(AskGpt,
                           o => _results.Count > 0 && Operation != null && DebuggerSession.Instance.IsAttached && !string.IsNullOrEmpty(GptQuestion)));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Items = null;
                Conversation = null;
                GptPrompt = null;
                Types = null;
                ObjectAddress = 0;
                NumOfResults = 0;
                _results.Clear();
                _resultsCurrentIndex = -1;
                //Operation = null;
            }
            base.Dispose(disposing);
        }
    }
}
