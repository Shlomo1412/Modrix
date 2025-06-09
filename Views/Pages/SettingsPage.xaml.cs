using System.Windows.Controls;
using Modrix.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Modrix.Views.Pages
{
    public partial class SettingsPage : Page, INavigableView<SettingsViewModel>
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;

            InitializeComponent();
        }
    }
}
