using ModrixInstaller.ViewModels.Pages;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ModrixInstaller.Views.Pages
{
    public partial class CompletePage : Page
    {
        public CompletePageViewModel ViewModel { get; }

        public CompletePage(CompletePageViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;

            InitializeComponent();
        }

        private void JoinDiscord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://discord.gg/modrix",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}