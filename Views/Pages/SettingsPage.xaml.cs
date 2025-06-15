using System.Windows.Controls;
using Modrix.ViewModels.Pages;
using Modrix.Views.Windows;
using Wpf.Ui.Abstractions.Controls;

namespace Modrix.Views.Pages
{
    public partial class SettingsPage : Page, INavigableView<SettingsViewModel>
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        private void ExploreSourceCode_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new ExploreSourceCodeDialog();
                dialog.Owner = Application.Current.Windows.Count > 0 ? Application.Current.Windows[0] : null;
                dialog.ShowDialog();
            });
        }
    }
}
