using ModrixInstaller.ViewModels.Windows;
using ModrixInstaller.Views.Pages;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace ModrixInstaller.Views.Windows
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindowViewModel ViewModel { get; }

        public MainWindow(MainWindowViewModel viewModel)
        {
            ViewModel = viewModel;
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
                    nameof(WelcomePage) => new WelcomePage(new ModrixInstaller.ViewModels.Pages.WelcomePageViewModel()),
                    nameof(LicensePage) => new LicensePage(new ModrixInstaller.ViewModels.Pages.LicensePageViewModel(new ModrixInstaller.Services.LicenseService())),
                    nameof(InstallationOptionsPage) => new InstallationOptionsPage(new ModrixInstaller.ViewModels.Pages.InstallationOptionsViewModel(new ModrixInstaller.Services.ConfigurationService())),
                    nameof(InstallationProgressPage) => new InstallationProgressPage(new ModrixInstaller.ViewModels.Pages.InstallationProgressViewModel(new ModrixInstaller.Services.InstallationService(), new ModrixInstaller.Services.ConfigurationService())),
                    nameof(CompletePage) => new CompletePage(new ModrixInstaller.ViewModels.Pages.CompletePageViewModel(new ModrixInstaller.Services.ConfigurationService())),
                    _ => null
                };

                if (page != null)
                {
                    MainFrame.Navigate(page);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
    }
}