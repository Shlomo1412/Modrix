using ModrixInstaller.ViewModels.Pages;
using System.Windows.Controls;

namespace ModrixInstaller.Views.Pages
{
    public partial class InstallationProgressPage : Page
    {
        public InstallationProgressViewModel ViewModel { get; }

        public InstallationProgressPage(InstallationProgressViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;

            InitializeComponent();

            // Start installation when page is loaded
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Start the installation process
            await ViewModel.StartInstallationAsync();
        }
    }
}