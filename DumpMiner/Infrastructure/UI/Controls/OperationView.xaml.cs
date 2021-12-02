using DumpMiner.Debugger;
using DumpMiner.ObjectExtractors;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace DumpMiner.Infrastructure.UI.Controls
{
    public partial class OperationView
    {
        public event SelectionChangedEventHandler SelectionChange;
        public object SelectedItem { get; private set; }
        public OperationView()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty HeaderProperty =
           DependencyProperty.Register(
               "Header", typeof(string), typeof(OperationView),
               new PropertyMetadata(string.Empty, (o, args) =>
               {
                   ((OperationView)o).HeaderTextBlock.Text = (string)args.NewValue;
               }));

        public static readonly DependencyProperty ExplanationProperty =
          DependencyProperty.Register(
              "Explanation", typeof(string), typeof(OperationView),
              new PropertyMetadata(string.Empty, (o, args) =>
              {
                  ((OperationView)o).ExplanationTextBlock.Text = (string)args.NewValue;
              }));

        public static readonly DependencyProperty ObjectAddressVisibilityProperty =
            DependencyProperty.Register(
                "ObjectAddressVisibility", typeof(Visibility), typeof(OperationView),
                new PropertyMetadata(Visibility.Visible, (o, args) =>
                {
                    var view = ((OperationView)o);
                    view.ObjectAddressTextBlock.Visibility = (Visibility)args.NewValue;
                    view.ObjectAddressTextBox.Visibility = (Visibility)args.NewValue;
                }));

        public static readonly DependencyProperty ObjectTypeVisibilityProperty =
           DependencyProperty.Register(
               "ObjectTypeVisibility", typeof(Visibility), typeof(OperationView),
               new PropertyMetadata(Visibility.Visible, (o, args) =>
               {
                   var view = ((OperationView)o);
                   view.ObjectTypesTextBlock.Visibility = (Visibility)args.NewValue;
                   view.ObjectTypesTextBox.Visibility = (Visibility)args.NewValue;
               }));

        public static readonly DependencyProperty ObjectAddressNameProperty =
            DependencyProperty.Register(
                "ObjectAddressNameProperty", typeof(string), typeof(OperationView),
                new PropertyMetadata("Object address", (o, args) =>
                {
                    ((OperationView)o).ObjectAddressTextBlock.Text = (string)args.NewValue;
                }));

        public static readonly DependencyProperty ObjectTypeNameProperty =
            DependencyProperty.Register(
                "ObjectTypeNameProperty", typeof(string), typeof(OperationView),
                new PropertyMetadata("Object types", (o, args) =>
                {
                    ((OperationView)o).ObjectTypesTextBlock.Text = (string)args.NewValue;
                }));

        public static readonly DependencyProperty ItemsViewProperty =
           DependencyProperty.Register(
               "ItemsView", typeof(GridView), typeof(OperationView),
               new PropertyMetadata(null, (o, args) =>
               {
                   ((OperationView)o).ItemsList.View = (GridView)args.NewValue;
               }));


        public static readonly DependencyProperty ItemsTemplateProperty =
          DependencyProperty.Register(
              "ItemsTemplate", typeof(DataTemplate), typeof(OperationView),
              new PropertyMetadata(null, (o, args) =>
              {
                  ((OperationView)o).ItemsList.ItemTemplate = (DataTemplate)args.NewValue;
              }));


        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public string Explanation
        {
            get => (string)GetValue(ExplanationProperty);
            set => SetValue(ExplanationProperty, value);
        }

        public Visibility ObjectAddressVisibility
        {
            get => (Visibility)GetValue(ObjectAddressVisibilityProperty);
            set => SetValue(ObjectAddressVisibilityProperty, value);
        }

        public Visibility ObjectTypeVisibility
        {
            get => (Visibility)GetValue(ObjectTypeVisibilityProperty);
            set => SetValue(ObjectTypeVisibilityProperty, value);
        }

        public string ObjectAddressName
        {
            get => (string)GetValue(ObjectAddressNameProperty);
            set => SetValue(ObjectAddressNameProperty, value);
        }

        public string ObjectTypeName
        {
            get => (string)GetValue(ObjectTypeNameProperty);
            set => SetValue(ObjectTypeNameProperty, value);
        }

        public GridView ItemsView
        {
            get => (GridView)GetValue(ItemsViewProperty);
            set => SetValue(ItemsViewProperty, value);
        }

        public DataTemplate ItemsTemplate
        {
            get => (DataTemplate)GetValue(ItemsTemplateProperty);
            set => SetValue(ItemsTemplateProperty, value);
        }

        private void ItemsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItem = e.AddedItems != null && e.AddedItems.Count > 0 ? e.AddedItems[0] : null;
            SelectionChange?.Invoke(sender, e);
        }

        private async void DumpMenuClicked(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null)
                return;

            var addressProperty = SelectedItem.GetType().GetProperty("Address", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var sizeProperty = SelectedItem.GetType().GetProperty("Size", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (addressProperty == null || sizeProperty == null)
                return;
            ulong address = (ulong)addressProperty.GetValue(SelectedItem);
            ulong size = (ulong)sizeProperty.GetValue(SelectedItem);

            var typeNameProperty = SelectedItem.GetType().GetProperty("Type", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            string typeName = "[unknown]";
            if (typeNameProperty != null)
            {
                typeName = (string)typeNameProperty.GetValue(SelectedItem);
            }

            if (address == 0)
            {
                MessageBox.Show("Selected object address is zero. Cannot dump.", "Error");
                return;
            }
            if (size == 0)
            {
                MessageBox.Show("Selected object size is zero. Cannot dump.", "Error");
                return;
            }
            if (size > int.MaxValue)
            {
                MessageBox.Show("Selected object size is too large. Cannot dump.", "Error");
                return;
            }

            try
            {
                byte[] buffer = new byte[size];
                int bytesRead = 0;
                bool success = DebuggerSession.Instance.DataTarget.ReadProcessMemory(address, buffer, (int)size, out bytesRead);
                if (!success || bytesRead <= 0)
                {
                    MessageBox.Show("Could not read process memory.", "Error");
                    return;
                }
                if ((uint)bytesRead < size)
                {
                    byte[] buffer2 = new byte[bytesRead];
                    Array.Copy(buffer, buffer2, bytesRead);
                    buffer = buffer2;
                }

                string baseFileName = $"pid_{DebuggerSession.Instance.AttachedProcessId?.ToString() ?? "none"}_obj_{address:x16}_{bytesRead:x8}";
                string dumpFileName = baseFileName + ".dump";
                string descFileName = baseFileName + ".txt";
                var dumpDescription = new StringBuilder();
                dumpDescription.AppendLine("DumpMiner object dump");
                dumpDescription.AppendLine($"Time: {DateTime.Now.ToString()}");
                dumpDescription.AppendLine($"Process ID: {DebuggerSession.Instance.AttachedProcessId?.ToString() ?? "N/A"}");
                dumpDescription.AppendLine($"Process Name: {DebuggerSession.Instance.AttachedProcess?.ProcessName ?? "N/A"}");
                dumpDescription.AppendLine($"Dumped object type: {typeName}");
                dumpDescription.AppendLine($"Dumped object address: 0x{address:x16} ({address})");
                dumpDescription.AppendLine($"Dumped object size: 0x{size:x8} ({size})");

                File.WriteAllBytes(dumpFileName, buffer);
                File.WriteAllText(descFileName, dumpDescription.ToString());

                MessageBox.Show($"Dumped {bytesRead} raw bytes from address 0x{address:x16} to {dumpFileName}", "Object dumped");

                var extractor = ObjectExtraction.FindExtractor(typeName);
                if (extractor != null)
                {
                    if (MessageBox.Show($"The type {typeName} can be extracted from memory. Would you like to do this?", "Extract?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        string extractFileName = baseFileName + extractor.GetFileNameSuffix();
                        using (var fs = File.OpenWrite(extractFileName))
                        {
                            // truncate the file if it already existed
                            fs.SetLength(0);
                            success = await extractor.Extract(fs, address, size, typeName);
                        }
                        if (success)
                        {
                            MessageBox.Show("Extraction completed successfully.");
                        }
                        else
                        {
                            MessageBox.Show("Extraction failed.");
                            try { File.Delete(extractFileName); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An exception occurred while dumping the object: {ex.ToString()}", "Error");
            }
        }
    }
}
