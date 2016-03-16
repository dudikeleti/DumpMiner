using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using DumpMiner.Common;
using DumpMiner.Infrastructure.Mef;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.ViewModels
{
    [ViewModel(ViewModelNames.OperationTypesViewModel)]
    public class OperationTypesViewModel : BaseViewModel
    {
        [ImportingConstructor]
        public OperationTypesViewModel([ImportMany]IEnumerable<Lazy<IContent, IViewMetadata>> views)
        {
            var collection = from view in views
                             where !string.IsNullOrEmpty(view.Metadata.DisplayName)
                             select
                                 new Link
                                 {
                                     DisplayName = view.Metadata.DisplayName,
                                     Source = new Uri(view.Metadata.ContentUri, UriKind.Relative)
                                 };
            _operations = new LinkCollection(collection);
        }

        private LinkCollection _operations;
        public LinkCollection Operations
        {
            get { return _operations; }
            set
            {
                if (value == _operations) return;
                _operations = value;
                OnPropertyChanged();
            }
        }
    }
}
