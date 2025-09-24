using ModrixInstaller.ViewModels.Pages;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ModrixInstaller.Views.Pages
{
    public partial class WelcomePage : Page
    {
        public WelcomePageViewModel ViewModel { get; }

        public WelcomePage(WelcomePageViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;

            InitializeComponent();
        }

        private void OpenWebsite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Shlomo1412/Modrix",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void OpenDocumentation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Shlomo1412/Modrix/wiki",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void ReportIssues_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Shlomo1412/Modrix/issues",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}