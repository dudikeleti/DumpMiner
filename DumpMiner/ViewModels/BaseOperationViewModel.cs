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
using DumpMiner.Services.Configuration;
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

            var configService = ConfigurationService.Instance;
            var timeoutMs = configService.Configuration.General.DefaultTimeoutMs;
            _defaultTimeout = TimeSpan.FromMilliseconds(timeoutMs);
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

        private bool _isAiEnabled;
        public bool IsAiEnabled
        {
            get { return _isAiEnabled; }
            set
            {
                _isAiEnabled = value;
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

        private object _selectedItem;
        public object SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ConversationMessage> Conversation
        {
            get { return Model.Chat; }
            set
            {
                Model.Chat = value;
                OnPropertyChanged();
            }
        }

        private string _aiQuestion;
        public string AiQuestion
        {
            get { return _aiQuestion; }
            set
            {
                _aiQuestion = value;
                OnPropertyChanged();
            }
        }

        public StringBuilder UserPrompt
        {
            get { return Model.UserPrompt; }
            set
            {
                Model.UserPrompt = value;
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

            // Store the custom parameter in the model for AI context
            Model.CustomParameter = o;

            // Set the custom parameter description if the operation supports AI
            if (Operation is IAIEnabledOperation aiOperation)
            {
                Model.CustomParameterDescription = aiOperation.GetCustomParameterDescription(o);
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
        }

        public async void AskAi(object o)
        {
            if (!DebuggerSession.Instance.IsAttached)
            {
                DebuggerSession.Instance.Detach();
                App.Container.GetExport<IDialogService>().Value.ShowDialog("Process is detached");
                return;
            }

            // Ensure we preserve the original operation context for AI analysis
            // If no parameter is passed (like from UI button), use null but preserve existing CustomParameter
            var parameterToPass = o;

            // Don't add messages here - let the operation handle conversation management
            CancelOperationVisibility = Visibility.Visible;
            IsLoading = true;
            CancellationTokenSource = new CancellationTokenSource(_defaultTimeout);
            UserPrompt.Append(AiQuestion);
            try
            {
                var aiResult = await Operation.AskAi(Model, Items, CancellationTokenSource.Token, parameterToPass);
                // Messages are now handled internally in the operation
                // Note: UserPrompt is now handled internally in operations
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
            AiQuestion = null;
            // Clear the user prompt for next interaction
            UserPrompt.Clear();
        }

        private void OnOperationCompleted()
        {
            IsLoading = false;
            CancelOperationVisibility = Visibility.Collapsed;
            if (Items != null)
                Count = Items.Count;
            if (Count > 0)
                _resultsCurrentIndex = _results.Count - 1;

            IsAiEnabled = true;
            CancellationTokenSource.Dispose();
            CancellationTokenSource = null;
            ((RelayCommand)GoToNextResultCommand).OnCanExecuteChanged();
            ((RelayCommand)GoToPreResultCommand).OnCanExecuteChanged();
        }

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

        private ICommand _askAiCommand;
        public ICommand AskAiCommand
        {
            get
            {
                return _askAiCommand ??
                       (_askAiCommand = new RelayCommand(AskAi,
                           o => _results.Count > 0 && Operation != null && DebuggerSession.Instance.IsAttached && !string.IsNullOrEmpty(AiQuestion)));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Items = null;
                Conversation = null;
                UserPrompt = null;
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
