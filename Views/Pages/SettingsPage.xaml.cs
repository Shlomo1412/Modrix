using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Modrix.ViewModels.Pages;
using Modrix.Views.Windows;
using Modrix.Services;
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

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the uninstall service from the DI container
                var uninstallService = App.Services.GetRequiredService<IUninstallService>();
                
                // Create and show the uninstall dialog
                var uninstallDialog = new UninstallDialog(uninstallService)
                {
                    Owner = Window.GetWindow(this)
                };
                
                uninstallDialog.ShowDialog();
                
                // Note: The UninstallDialog handles the actual uninstallation process
                // and will exit the application if uninstallation is successful
            }
            catch (Exception ex)
            {
                // Show error message if something goes wrong
                var errorDialog = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Error",
                    Content = $"Failed to start uninstallation process:\n\n{ex.Message}",
                    PrimaryButtonText = "OK"
                };
                _ = errorDialog.ShowDialogAsync();
            }
        }
    }
}
