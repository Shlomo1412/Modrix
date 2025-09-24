using ModrixInstaller.ViewModels.Pages;
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
    }
}