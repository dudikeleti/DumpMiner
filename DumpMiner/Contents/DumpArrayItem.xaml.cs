using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Infrastructure.Mef;
using DumpMiner.Infrastructure.UI;
using DumpMiner.Models;
using FirstFloor.ModernUI.Windows;

namespace DumpMiner.Contents
{
    [OperationView("/OperationTypes/DumpArrayItem", "Dump Array Item")]
    public partial class DumpArrayItem : IContent, IHasViewModel
    {
        private const string SizeText = "Selected object size is: {0}";
        public DumpArrayItem()
        {
            InitializeComponent();
            ExtendedData = new Dictionary<string, object> { ["OperationName"] = OperationNames.DumpArrayItem };
            OperationView.SelectionChange += OperationView_SelectionChange;
            SizeTextBlock.Text = string.Format(SizeText, string.Empty);
        }

        private async void OperationView_SelectionChange(object sender, SelectionChangedEventArgs e)
        {
            var item = OperationView.SelectedItem as ClrObject.ClrObjectModel;
            if (item == null)
                return;

            var operation = App.Container.GetExportedValue<IDebuggerOperation>(OperationNames.GetObjectSize);
            if (operation == null)
                return;

            SizeTextBlock.Text = string.Format(SizeText, string.Empty);
            ulong address;
            ulong offset;
            if (!ulong.TryParse(item.Address.ToString(), out address))
                return;
            if (!ulong.TryParse(item.Offset.ToString(), out offset))
                return;

            dynamic result = (await operation.Execute(new OperationModel { ObjectAddress = address + offset }, default(CancellationToken), null)).FirstOrDefault();
            if (result != null)
            {
                var size = new BytesToKbOrMbConverter().Convert(result.TotalSize, null, null, null);
                SizeTextBlock.Text = string.Format(SizeText, size);
            }
        }

        public void OnFragmentNavigation(FirstFloor.ModernUI.Windows.Navigation.FragmentNavigationEventArgs e)
        {
        }

        public void OnNavigatedFrom(FirstFloor.ModernUI.Windows.Navigation.NavigationEventArgs e)
        {
        }

        public void OnNavigatedTo(FirstFloor.ModernUI.Windows.Navigation.NavigationEventArgs e)
        {
        }

        public void OnNavigatingFrom(FirstFloor.ModernUI.Windows.Navigation.NavigatingCancelEventArgs e)
        {
        }

        public string ViewModelName => ViewModelNames.BaseOperationViewModel;

        public bool IsViewModelLoaded { get; set; }

        public Dictionary<string, object> ExtendedData { get; }
    }
}
