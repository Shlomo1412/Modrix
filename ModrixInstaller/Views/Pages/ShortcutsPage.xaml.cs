using ModrixInstaller.ViewModels.Pages;
using System.Windows.Controls;

namespace ModrixInstaller.Views.Pages
{
    public partial class ShortcutsPage : Page
    {
        public ShortcutsViewModel ViewModel { get; }

        public ShortcutsPage(ShortcutsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}