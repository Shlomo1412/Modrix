using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modrix.Models;
using Modrix.Services;
using Modrix.ViewModels.Pages;
using Modrix.ViewModels.Windows;
using Modrix.Views.Pages;
using Modrix.Views.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;
using IThemeService = Modrix.Services.IThemeService;

namespace Modrix
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {

        private static IConnectivityService? _connectivityService;

        
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)); })
            .ConfigureServices((context, services) =>
            {
                

                


                services.AddNavigationViewPageProvider();

                services.AddSingleton<IConnectivityService, ConnectivityService>();




                services.AddHostedService<ApplicationHostService>();

                // Theme manipulation
                services.AddSingleton<Services.IThemeService, Services.ThemeService>();
                // TaskBar manipulation
                services.AddSingleton<ITaskBarService, TaskBarService>();
                services.AddSingleton<Modrix.Services.IModrixTaskBarService, Modrix.Services.ModrixTaskBarService>();

                // Service containing navigation, same as INavigationWindow... but without window
                services.AddSingleton<INavigationService, NavigationService>();

                // Main window with navigation
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                services.AddSingleton<DashboardPage>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<DataPage>();
                services.AddSingleton<DataViewModel>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>(sp =>
                new SettingsViewModel(
                    sp.GetRequiredService<IThemeService>(),
                    sp.GetRequiredService<IContentDialogService>(),
                    sp.GetRequiredService<IConfiguration>()));
                services.AddSingleton<ProjectWorkspace>();
                services.AddSingleton<ProjectWorkspaceViewModel>();

                //Resources Page
                services.AddTransient<ResourcesPageViewModel>();
                services.AddTransient<ResourcesPage>();

                // Console Page
                services.AddSingleton<ConsolePage>();

                // IDE Page
                services.AddSingleton<IDEPageViewModel>();
                services.AddSingleton<IDEPage>();

                //NewsPage
                services.AddSingleton<NewsPage>();

                //FeedbackPage
                services.AddSingleton<FeedbackPage>();

                //Content dialog
                services.AddSingleton<IContentDialogService, ContentDialogService>();


            }).Build();

        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            await _host.StartAsync();

            // Get the connectivity service
            _connectivityService = Services.GetService<IConnectivityService>();

            // Load saved theme:
            var themeService = Services.GetRequiredService<IThemeService>();
            var saved = themeService.LoadTheme();
            ApplicationThemeManager.Apply(saved);

            var dialogService = Services.GetRequiredService<IContentDialogService>();
            var navigationWindow = Services.GetService<INavigationWindow>();
            if (navigationWindow is MainWindow main)
            {
                dialogService.SetDialogHost(main.DialogHost);
            }

            // Update Taskbar JumpList with all projects
            var allProjects = Modrix.Services.TemplateManager.LoadAllProjects();
            var taskBarService = Services.GetRequiredService<Modrix.Services.IModrixTaskBarService>();
            taskBarService?.UpdateJumpListWithProjects(allProjects);

            // Handle JumpList activation (open project from JumpList)
            var args = Environment.GetCommandLineArgs();
            var openArg = args.FirstOrDefault(a => a.StartsWith("--open-project="));
            if (openArg != null)
            {
                var projectPath = openArg.Substring("--open-project=".Length).Trim('"');
                if (!string.IsNullOrEmpty(projectPath) && System.IO.Directory.Exists(projectPath))
                {
                    // Try to open the project workspace window
                    var project = allProjects.FirstOrDefault(p => p.Location == projectPath);
                    if (project != null)
                    {
                        var viewModel = Services.GetRequiredService<ProjectWorkspaceViewModel>();
                        var navigationViewPageProvider = Services.GetRequiredService<INavigationViewPageProvider>();
                        var navigationService = Services.GetRequiredService<INavigationService>();
                        var workspaceWindow = new ProjectWorkspace(viewModel, navigationViewPageProvider, navigationService);
                        workspaceWindow.LoadProject(project);
                        workspaceWindow.Show();
                        workspaceWindow.Activate();
                        return;
                    }
                }
            }

            // Check connectivity and show snackbar if offline
            if (_connectivityService?.IsInternetAvailable() == false)
            {
                if (navigationWindow is MainWindow mainWindow)
                {
                    mainWindow.ShowOfflineSnackbar();
                }
            }
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();

            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }
    }
}
