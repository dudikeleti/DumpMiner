using System;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using DumpMiner.Common;
using DumpMiner.Debugger;
using DumpMiner.Services.Configuration;
using DumpMiner.Infrastructure.Mef;
using DumpMiner.ViewModels;
using DumpMiner.Services.AI;
using DumpMiner.Services.AI.Bootstrap;
using FirstFloor.ModernUI.Presentation;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Extensions.Logging;

namespace DumpMiner
{
    public partial class App : Application
    {
        public static CompositionContainer Container { get; set; }
        internal static IDialogService Dialog { get; private set; }
        public static AIHelper AIHelper { get; private set; }

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

            Dialog = App.Container.GetExport<IDialogService>().Value;

            // Initialize AI Services
            InitializeAIServices();

            DebuggerSession.Instance.OnDetach += OnDetach;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var logger = Log.ForContext<App>();

            try
            {
                logger.Information("Application shutting down");
                DebuggerSession.Instance.Detach();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during application shutdown");
            }
            finally
            {
                Log.CloseAndFlush();
                base.OnExit(e);
            }
        }

        private void LoadAppearanceSettings()
        {
            var configService = ConfigurationService.Instance;
            var appearance = configService.Configuration.Appearance;

            // Set accent color
            if (System.Windows.Media.ColorConverter.ConvertFromString(appearance.AccentColor) is Color accentColor)
            {
                AppearanceManager.Current.AccentColor = accentColor;
            }

            // Set theme
            if (appearance.Theme == ThemeType.Dark)
            {
                AppearanceManager.Current.ThemeSource = AppearanceManager.DarkThemeSource;
            }
            else
            {
                AppearanceManager.Current.ThemeSource = AppearanceManager.LightThemeSource;
            }

            // Set font size
            AppearanceManager.Current.FontSize = appearance.FontSize == FontSizeType.Large
                ? FontSize.Large
                : FontSize.Small;
        }

        private async void InitializeAIServices()
        {
            var logger = Log.ForContext<App>();

            try
            {
                // Initialize Serilog first
                InitializeLogging();

                logger.Information("Initializing AI Services...");

                // Initialize AI services for DI
                AIHelper = await ServiceRegistration.CreateAIHelperAsync();

                // Bootstrap AI services in MEF container with Serilog
                var msLogger = new SerilogLoggerFactory(Log.Logger).CreateLogger("AIBootstrap");
                AIBootstrap.Initialize(Container, msLogger);

                // Run verification to check registration
                AIBootstrap.VerifyRegistration(Container, msLogger);

                logger.Information("AI Services initialized successfully!");
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "Failed to initialize AI services");
                // Don't crash the application if AI services fail to initialize
            }
        }

        private void InitializeLogging()
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .CreateLogger();

                var logger = Log.ForContext<App>();
                logger.Information("Logging initialized for {Application} v{Version}",
                    "DumpMiner", Assembly.GetExecutingAssembly().GetName().Version);
            }
            catch (Exception ex)
            {
                // Fallback to basic console logging if Serilog fails
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.Debug()
                    .CreateLogger();

                var logger = Log.ForContext<App>();
                logger.Error(ex, "Failed to initialize logging from configuration, using fallback");
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
