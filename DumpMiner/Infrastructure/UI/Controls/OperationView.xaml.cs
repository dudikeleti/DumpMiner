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
    }
}
