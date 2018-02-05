using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DumpMiner.Common
{
    public class BaseViewModel : INotifyPropertyChanged, IDisposable
    {
        public ManualResetEventSlim Mre = new ManualResetEventSlim(false);
        public Action<BaseViewModel> ViewModelDisposed;

        private bool _isLoading;
        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        private string _viewModelName;
        public virtual string ViewModelName
        {
            get { return _viewModelName ?? GetType().Name; }
            protected set
            {
                if (_viewModelName == value) return;
                _viewModelName = value;
                OnPropertyChanged();
            }
        }

        // indicates the last operation error
        public string LastError { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            _isLoading = false;
            PropertyChanged = null;
            ViewModelDisposed?.Invoke(this);
            ViewModelDisposed = null;
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
