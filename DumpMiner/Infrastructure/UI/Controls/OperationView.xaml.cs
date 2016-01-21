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
                   var view = o as OperationView;
                   view.HeaderTextBlock.Text = (string)args.NewValue;
               }));

        public static readonly DependencyProperty ExplanationProperty =
          DependencyProperty.Register(
              "Explanation", typeof(string), typeof(OperationView),
              new PropertyMetadata(string.Empty, (o, args) =>
              {
                  var view = o as OperationView;
                  view.ExplanationTextBlock.Text = (string)args.NewValue;
              }));

        public static readonly DependencyProperty ObjectAddressVisibilityProperty =
            DependencyProperty.Register(
                "ObjectAddressVisibility", typeof(Visibility), typeof(OperationView),
                new PropertyMetadata(Visibility.Visible, (o, args) =>
                {
                    var view = o as OperationView;
                    view.ObjectAddressTextBlock.Visibility = (Visibility)args.NewValue;
                    view.ObjectAddressTextBox.Visibility = (Visibility)args.NewValue;
                }));

        public static readonly DependencyProperty ObjectTypeVisibilityProperty =
           DependencyProperty.Register(
               "ObjectTypeVisibility", typeof(Visibility), typeof(OperationView),
               new PropertyMetadata(Visibility.Visible, (o, args) =>
               {
                   var view = o as OperationView;
                   view.ObjectTypesTextBlock.Visibility = (Visibility)args.NewValue;
                   view.ObjectTypesTextBox.Visibility = (Visibility)args.NewValue;
               }));

        public static readonly DependencyProperty ItemsViewProperty =
           DependencyProperty.Register(
               "ItemsView", typeof(GridView), typeof(OperationView),
               new PropertyMetadata(null, (o, args) =>
               {
                   var view = o as OperationView;
                   view.ItemsList.View = args.NewValue == null ? null : (GridView)args.NewValue;
               }));


        public static readonly DependencyProperty ItemsTemplateProperty =
          DependencyProperty.Register(
              "ItemsTemplate", typeof(DataTemplate), typeof(OperationView),
              new PropertyMetadata(null, (o, args) =>
              {
                  var view = o as OperationView;
                  view.ItemsList.ItemTemplate = args.NewValue == null ? null : (DataTemplate)args.NewValue;
              }));


        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public string Explanation
        {
            get { return (string)GetValue(ExplanationProperty); }
            set { SetValue(ExplanationProperty, value); }
        }

        public Visibility ObjectAddressVisibility
        {
            get { return (Visibility)GetValue(ObjectAddressVisibilityProperty); }
            set { SetValue(ObjectAddressVisibilityProperty, value); }
        }

        public Visibility ObjectTypeVisibility
        {
            get { return (Visibility)GetValue(ObjectTypeVisibilityProperty); }
            set { SetValue(ObjectTypeVisibilityProperty, value); }
        }

        public GridView ItemsView
        {
            get { return (GridView)GetValue(ItemsViewProperty); }
            set { SetValue(ItemsViewProperty, value); }
        }

        public DataTemplate ItemsTemplate
        {
            get { return (DataTemplate)GetValue(ItemsTemplateProperty); }
            set { SetValue(ItemsTemplateProperty, value); }
        }

        private void ItemsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItem = e.AddedItems != null && e.AddedItems.Count > 0 ? e.AddedItems[0] : null;
            var handler = SelectionChange;
            if (handler != null)
                handler(sender, e);
        }
    }
}
