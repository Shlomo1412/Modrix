using ModrixInstaller.ViewModels.Pages;
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
    }
}