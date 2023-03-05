using System.ComponentModel.Composition.Hosting;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Infrastructure;
using DumpMiner.Infrastructure.Mef;
using DumpMiner.ViewModels;
using FirstFloor.ModernUI.Presentation;

namespace DumpMiner
{
    public partial class App : Application
    {
        public static CompositionContainer Container { get; set; }
        private static IViewModelLoader _viewModelLoader;

        protected override void OnStartup(StartupEventArgs e)
        {
            RegisterLoadedHandler();
            base.OnStartup(e);

            // bootstrap MEF composition
            var catalog = new AssemblyCatalog(Assembly.GetExecutingAssembly());
            Container = new CompositionContainer(catalog);

            // retrieve the MefContentLoader export and assign to global resources (so {DynamicResource MefContentLoader} can be resolved)
            var contentLoader = Container.GetExport<MefContentLoader>().Value;
            Resources.Add("MefContentLoader", contentLoader);

            // same for MefLinkNavigator
            var navigator = Container.GetExport<MefLinkNavigator>().Value;
            Resources.Add("MefLinkNavigator", navigator);

            _viewModelLoader = Container.GetExport<MefViewModelLoader>().Value;

            LoadAppearanceSettings();

            DebuggerSession.Instance.OnDetach += OnDetach;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DebuggerSession.Instance.Detach();
            base.OnExit(e);
        }

        private void LoadAppearanceSettings()
        {
            var color = SettingsManager.Instance.ReadSettingValue(SettingsManager.AccentColor);
            var rgb = color.Split(',');
            AppearanceManager.Current.AccentColor = Color.FromRgb(
                byte.Parse(rgb[0], NumberStyles.HexNumber),
                byte.Parse(rgb[1], NumberStyles.HexNumber),
                byte.Parse(rgb[2], NumberStyles.HexNumber));
            // AppearanceManager.Current.AccentColor = Color.FromRgb(0xf0, 0x96, 0x09);
            var theme = SettingsManager.Instance.ReadSettingValue(SettingsManager.Theme);
            var themeValue = theme.Substring(theme.IndexOf(",") + 1);

            if (themeValue.ToLower() == "dark")
            {
                AppearanceManager.Current.ThemeSource = AppearanceManager.DarkThemeSource;
            }
            else
            {
                AppearanceManager.Current.ThemeSource = AppearanceManager.LightThemeSource;
            }
        }

        private void OnDetach()
        {
            _viewModelLoader.Unload<BaseOperationViewModel>();
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            var view = sender as IHasViewModel;
            if (view == null || view.IsViewModelLoaded) return;
            FrameworkElement fe;
            FrameworkContentElement fce;
            if ((fe = view as FrameworkElement) != null)
            {
                fe.DataContext = null;
                fe.DataContext = _viewModelLoader.Load(view);
            }
            else if ((fce = view as FrameworkContentElement) != null)
            {
                fce.DataContext = null;
                fce.DataContext = _viewModelLoader.Load(view);
            }
        }

        #region Initialized

        private void RegisterLoadedHandler()
        {
            // set MyInitialized=true for new windows (happens before Loaded)
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.SizeChangedEvent, new RoutedEventHandler(OnSizeChanged));
            // our loaded handler
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded), true);
            EventManager.RegisterClassHandler(typeof(FrameworkContentElement), FrameworkContentElement.LoadedEvent, new RoutedEventHandler(OnLoaded), true);
        }

        private static void OnSizeChanged(object sender, RoutedEventArgs e)
        {
            SetMyInitialized((Window)sender, true);
        }

        public static readonly DependencyProperty MyInitializedProperty =
            DependencyProperty.RegisterAttached("MyInitialized", typeof(bool), typeof(App),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, OnMyInitializedChanged));

        private static void OnMyInitializedChanged(DependencyObject dpo, DependencyPropertyChangedEventArgs ev)
        {
            if (!(bool)ev.NewValue)
            {
                if (dpo is FrameworkElement)
                    (dpo as FrameworkElement).Loaded -= EmptyRoutedEventHandler;
                if (dpo is FrameworkContentElement)
                    (dpo as FrameworkContentElement).Loaded -= EmptyRoutedEventHandler;
                return;
            }
            //return;
            //throw new ArgumentException("Cannot set to false", ev.Property.Name);

            // registering instance handler unbreaks class handlers
            if (dpo is FrameworkElement)
                (dpo as FrameworkElement).Loaded += EmptyRoutedEventHandler;
            if (dpo is FrameworkContentElement)
                (dpo as FrameworkContentElement).Loaded += EmptyRoutedEventHandler;
        }

        private static readonly RoutedEventHandler EmptyRoutedEventHandler = delegate { };

        public static void SetMyInitialized(UIElement element, bool value)
        {
            element.SetValue(MyInitializedProperty, value);
        }

        public static bool GetMyInitialized(UIElement element)
        {
            return (bool)element.GetValue(MyInitializedProperty);
        }
        #endregion Initialized
    }
}
