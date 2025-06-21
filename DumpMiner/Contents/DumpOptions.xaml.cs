using System.Windows;
using DumpMiner.Common;
using DumpMiner.Infrastructure.Mef;

namespace DumpMiner.Contents
{
    /// <summary>
    /// Interaction logic for DumpOptions.xaml
    /// </summary>
    [Content("/DumpOptions")]
    public partial class DumpOptions : IHasViewModel
    {
        public DumpOptions()
        {
            InitializeComponent();
            ExtendedData = new System.Collections.Generic.Dictionary<string, object>();
        }

        public string ViewModelName => ViewModelNames.DumpOptionsViewModel;

        public bool IsViewModelLoaded { get; set; }

        public System.Collections.Generic.Dictionary<string, object> ExtendedData { get; private set; }
    }
} 