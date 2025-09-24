using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModrixInstaller.Services;
using ModrixInstaller.ViewModels.Pages;
using ModrixInstaller.ViewModels.Windows;
using ModrixInstaller.Views.Pages;
using ModrixInstaller.Views.Windows;
using System.Windows;
using System.Windows.Threading;

namespace ModrixInstaller
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Show main window
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Services
            services.AddSingleton<InstallationService>();
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<LicenseService>();

            // ViewModels
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<WelcomePageViewModel>();
            services.AddTransient<LicensePageViewModel>();
            services.AddTransient<InstallationOptionsViewModel>();
            services.AddTransient<InstallationProgressViewModel>();
            services.AddTransient<CompletePageViewModel>();

            // Views
            services.AddSingleton<MainWindow>();
            services.AddTransient<WelcomePage>();
            services.AddTransient<LicensePage>();
            services.AddTransient<InstallationOptionsPage>
();
            services.AddTransient<InstallationProgressPage>();
            services.AddTransient<CompletePage>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            // This method is called by the XAML event handler
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            // This method is called by the XAML event handler
        }
    }
}
