using ModrixInstaller.ViewModels.Pages;
using System.Windows.Controls;

namespace ModrixInstaller.Views.Pages
{
    public partial class InstallationOptionsPage : Page
    {
        public InstallationOptionsViewModel ViewModel { get; }

        public InstallationOptionsPage(InstallationOptionsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;

            InitializeComponent();
        }
    }
}