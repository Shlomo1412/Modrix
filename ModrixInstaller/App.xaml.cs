using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using ModrixInstaller.Services;
using ModrixInstaller.ViewModels.Pages;
using ModrixInstaller.ViewModels.Windows;
using ModrixInstaller.Views.Pages;
using ModrixInstaller.Views.Windows;
using System.IO;
using System.Windows.Threading;
using Wpf.Ui;

namespace ModrixInstaller
{
    public partial class App
    {
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)); })
            .ConfigureServices((context, services) =>
            {
                services.AddHostedService<ApplicationHostService>();

                services.AddHttpClient<IGitHubService, GitHubService>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<ITaskBarService, TaskBarService>();

                services.AddSingleton<IGitHubService, GitHubService>();
                services.AddSingleton<IInstallationService, InstallationService>();

                services.AddSingleton<LicenseViewModel>();
                services.AddSingleton<LicensePage>();
                
                services.AddSingleton<ShortcutsViewModel>();
                services.AddSingleton<ShortcutsPage>();
                
                services.AddSingleton<InstallerViewModel>();
                services.AddSingleton<InstallerPage>();

                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            }).Build();

        public static IServiceProvider Services => _host.Services;

        private async void OnStartup(object sender, StartupEventArgs e) => await _host.StartAsync();
        private async void OnExit(object sender, ExitEventArgs e) { await _host.StopAsync(); _host.Dispose(); }
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) { }
    }
}
