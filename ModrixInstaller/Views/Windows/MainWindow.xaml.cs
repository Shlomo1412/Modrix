using ModrixInstaller.ViewModels.Windows;
using ModrixInstaller.Views.Pages;
using ModrixInstaller.Services;
using ModrixInstaller.ViewModels.Pages;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace ModrixInstaller.Views.Windows
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindowViewModel ViewModel { get; }
        private readonly ConfigurationService _configurationService;
        private readonly LicenseService _licenseService;
        private readonly InstallationService _installationService;

        public MainWindow(MainWindowViewModel viewModel, ConfigurationService configurationService, LicenseService licenseService, InstallationService installationService)
        {
            ViewModel = viewModel;
            _configurationService = configurationService;
            _licenseService = licenseService;
            _installationService = installationService;
            
            DataContext = ViewModel;

            InitializeComponent();

            // Subscribe to navigation events
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Navigate to first page
            NavigateToCurrentStep();
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.CurrentStep))
            {
                NavigateToCurrentStep();
            }
        }

        private void NavigateToCurrentStep()
        {
            if (ViewModel.CurrentStep?.PageType != null)
            {
                Page? page = ViewModel.CurrentStep.PageType.Name switch
                {
                    nameof(WelcomePage) => new WelcomePage(new WelcomePageViewModel()),
                    nameof(LicensePage) => CreateLicensePage(),
                    nameof(InstallationOptionsPage) => CreateInstallationOptionsPage(),
                    nameof(InstallationProgressPage) => new InstallationProgressPage(new InstallationProgressViewModel(_installationService, _configurationService)),
                    nameof(CompletePage) => new CompletePage(new CompletePageViewModel(_configurationService)),
                    _ => null
                };

                if (page != null)
                {
                    MainFrame.Navigate(page);
                }
            }
        }

        private LicensePage CreateLicensePage()
        {
            var viewModel = new LicensePageViewModel(_licenseService);
            var page = new LicensePage(viewModel);
            
            // Subscribe to license changes to refresh main navigation
            viewModel.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == nameof(LicensePageViewModel.IsLicenseAccepted))
                {
                    ViewModel.RefreshNavigationState();
                }
            };
            
            return page;
        }

        private InstallationOptionsPage CreateInstallationOptionsPage()
        {
            var viewModel = new InstallationOptionsViewModel(_configurationService);
            var page = new InstallationOptionsPage(viewModel);
            
            // Subscribe to configuration changes to refresh main navigation
            viewModel.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == nameof(InstallationOptionsViewModel.IsInstallPathValid) || 
                    e.PropertyName == nameof(InstallationOptionsViewModel.HasEnoughSpace))
                {
                    ViewModel.RefreshNavigationState();
                }
            };
            
            return page;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
    }
}